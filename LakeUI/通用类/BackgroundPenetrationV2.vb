Imports System.Drawing.Imaging
Imports Vortice.Direct2D1

''' <summary>
''' V2 透明背景穿透实现。<see cref="TransparentBackgroundCache"/> 的精简继任者。
'''
''' === 与 V1 (<see cref="TransparentBackgroundCache"/>) 的差异 ===
''' • V2 必须显式指定背景源（child 控件的 <c>BackgroundSource</c> 属性）。未指定 → 不采样、不绘制，
'''   完全不画背景层。不再自动回退到 child.Parent，避免无意识的递归与采样链。
''' • 每个 source 在窗口内共享一份 GDI Bitmap 作权威，按 source 的
'''   <see cref="Control.Invalidated"/> / <see cref="Control.Resize"/> / <see cref="Control.Disposed"/> 失效。
'''   Invalidated 带矩形时只重采 dirty rect，并只丢弃相交的 D2D 裁剪缓存。
''' • D2D 上传不再保留整张 source 位图，而是按当前 <see cref="PaintScopeV2.ClipRectangle"/>
'''   建立 source crop → RT 的小位图缓存；同一 RT / 同一 crop 命中时直接复用。
''' • 不区分 GDI / D2D 消费者：GDI Bitmap 负责重采与 dirty rect 合并，ID2D1Bitmap 只作为当前 RT 的上传缓存。
'''
''' === BackColor 与背景穿透的优先级（V2 强约束） ===
'''   1) 指定了 BackgroundSource → 跳过 BackColor，直接调本类画穿透层。
'''   2) 未指定 BackgroundSource 且 BackColor.A &gt; 0 → 直接以 BackColor 填底，本类不参与。
'''   3) 未指定 BackgroundSource 且 BackColor.A = 0 → 不画背景层（由 WinForms 默认透明逻辑处理）。
'''
''' === 用法 ===
'''   ' 在 OnPaint 内、画图形层之前：
'''   If _backgroundSource IsNot Nothing Then
'''       BackgroundPenetrationV2.PaintBackground(Me, scope, _backgroundSource)
'''   End If
'''
''' === 注意 ===
''' • source 自身若也是带 BackgroundSource 的 V2 透明控件，本类仍采样 source 本身，保留其中间
'''   背景、遮罩、边框、背景图片与子控件等视觉层。采样期间若遇到同窗口 V2 重入，会由
'''   D2DHelperV2 使用临时离屏 compositor 绘制到当前 GDI Bitmap，避免嵌套共享 BindDC。
''' • 裁剪缓存数量由 <see cref="GlobalOptions.BackgroundPenetrationCropCacheMaxEntriesPerSource"/> 控制；
'''   这是显存友好的近似 LRU，而不是长期持有每个透明控件的完整背景贴图。
''' • <see cref="Invalidate"/> 用于换主题等需要立即重采的极端场景，常规情况下不需要手动调用。
'''
''' === 线程要求 ===
''' UI 线程。对 _cache 字典有锁，但 GDI / D2D 调用本身仍假定在 UI 线程。
''' </summary>
Public Module BackgroundPenetrationV2

    Private Class Entry
        Public Class CropEntry
            Public SourceRect As Rectangle
            Public Width As Integer
            Public Height As Integer
            Public D2DBmp As ID2D1Bitmap
            Public D2DOwnerRT As WeakReference
        End Class

        Public Bmp As Bitmap
        Public Width As Integer
        Public Height As Integer
        Public FullDirty As Boolean = True
        Public ReadOnly DirtyRects As New List(Of Rectangle)()
        Public Painting As Boolean
        Public ReadOnly Crops As New List(Of CropEntry)()
        Public IsSolidColor As Boolean
        Public SolidArgb As Integer
    End Class

    Private ReadOnly _cache As New Dictionary(Of Control, Entry)

#Region "公开 API"

    ''' <summary>
    ''' 把 <paramref name="source"/> 在 <paramref name="child"/> 所在区域的内容采样后画到
    ''' <c>scope.BackgroundLayer</c>。
    ''' </summary>
    ''' <param name="child">透明控件本人。决定采样目标矩形（child.Width × child.Height）。</param>
    ''' <param name="scope">当前 V2 绘制作用域，背景层来自 <see cref="PaintScopeV2.BackgroundLayer"/>。</param>
    ''' <param name="source">
    ''' 显式背景源。<c>Nothing</c> 或已 Dispose 时本方法直接返回，背景层将保持上一次状态（通常是清空）。
    ''' V2 不再隐式回退到 <c>child.Parent</c>。
    ''' </param>
    Public Sub PaintBackground(child As Control, scope As PaintScopeV2, source As Control)
        If child Is Nothing OrElse scope Is Nothing Then Return
        If source Is Nothing OrElse source.IsDisposed Then Return
        Dim sw As Integer = source.Width, sh As Integer = source.Height
        If sw <= 0 OrElse sh <= 0 Then Return

        Dim childBounds As New Rectangle(0, 0, child.Width, child.Height)
        Dim destRect As Rectangle = Rectangle.Intersect(childBounds, scope.ClipRectangle)
        If destRect.Width <= 0 OrElse destRect.Height <= 0 Then Return

        Dim offset As Point = ComputeOffset(child, source)
        Dim srcRect As New Rectangle(offset.X + destRect.X, offset.Y + destRect.Y, destRect.Width, destRect.Height)

        Dim rt = scope.BackgroundLayer
        Dim isSolid As Boolean
        Dim solidColor As Color = Color.Empty
        Dim d2dBmp = AcquireD2DBitmap(source, rt, srcRect, isSolid, solidColor)
        If isSolid Then
            Dim brushCache = scope.Compositor?.BrushCache
            If brushCache IsNot Nothing Then
                rt.FillRectangle(D2DHelper.ToD2DRect(destRect), brushCache.Get(rt, solidColor))
            Else
                Using b = rt.CreateSolidColorBrush(D2DHelper.ToColor4(solidColor))
                    rt.FillRectangle(D2DHelper.ToD2DRect(destRect), b)
                End Using
            End If
            Return
        End If
        If d2dBmp Is Nothing Then Return
        rt.DrawBitmap(d2dBmp,
            D2DHelper.ToD2DRect(destRect),
            1.0F,
            BitmapInterpolationMode.Linear,
            D2DHelper.ToD2DRect(New Rectangle(0, 0, destRect.Width, destRect.Height)))
    End Sub

    ''' <summary>
    ''' 显式让指定背景源的缓存立刻置脏，下一次 <see cref="PaintBackground"/> 会重采。
    ''' </summary>
    ''' <remarks>
    ''' 常规情况下 source 的 <see cref="Control.Invalidated"/> 已经自动置脏，本方法只用于
    ''' 切换主题 / 强制刷新等不会触发 Invalidated 的场景。
    ''' </remarks>
    Public Sub Invalidate(source As Control)
        If source Is Nothing Then Return
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then
                entry.FullDirty = True
                InvalidateCropEntries(entry, Nothing)
            End If
        End SyncLock
    End Sub

#End Region

#Region "缓存内部"

    Private Function AcquireD2DBitmap(source As Control, rt As ID2D1RenderTarget, sourceRect As Rectangle,
                                      ByRef isSolid As Boolean, ByRef solidColor As Color) As ID2D1Bitmap
        isSolid = False
        solidColor = Color.Empty
        Dim sw As Integer = source.Width, sh As Integer = source.Height
        Dim entry As Entry = Nothing
        Dim needRebuild As Boolean
        SyncLock _cache
            If Not _cache.TryGetValue(source, entry) Then
                entry = New Entry()
                _cache(source) = entry
                AddHandler source.Disposed, AddressOf OnSourceDisposed
                AddHandler source.Invalidated, AddressOf OnSourceInvalidated
                AddHandler source.Resize, AddressOf OnSourceResized
            End If
            If entry.Painting Then Return Nothing
            Dim sizeOk As Boolean = (entry.Bmp IsNot Nothing AndAlso entry.Width = sw AndAlso entry.Height = sh)
            If Not sizeOk Then
                entry.FullDirty = True
                InvalidateCropEntries(entry, Nothing)
            End If
            needRebuild = entry.FullDirty OrElse DirtyRectsIntersect(entry, New Rectangle(0, 0, sw, sh))
            If needRebuild Then entry.Painting = True
        End SyncLock

        If needRebuild Then
            Try
                RebuildGdiBitmap(source, entry, sw, sh)
            Finally
                SyncLock _cache
                    entry.Painting = False
                End SyncLock
            End Try
        End If

        If entry.Bmp Is Nothing Then Return Nothing
        If entry.IsSolidColor Then
            isSolid = True
            solidColor = Color.FromArgb(entry.SolidArgb)
            Return Nothing
        End If

        Return AcquireCropBitmap(entry, rt, sourceRect)
    End Function

    Private Sub RebuildGdiBitmap(source As Control, entry As Entry, sw As Integer, sh As Integer)
        Dim fullRebuild As Boolean = entry.FullDirty OrElse entry.Bmp Is Nothing OrElse entry.Width <> sw OrElse entry.Height <> sh
        If entry.Bmp Is Nothing OrElse entry.Width <> sw OrElse entry.Height <> sh Then
            entry.Bmp?.Dispose()
            entry.Bmp = New Bitmap(sw, sh, PixelFormat.Format32bppPArgb)
            entry.Width = sw
            entry.Height = sh
            fullRebuild = True
        End If

        Dim repaintRects As List(Of Rectangle)
        If fullRebuild OrElse entry.DirtyRects.Count = 0 Then
            repaintRects = New List(Of Rectangle) From {New Rectangle(0, 0, sw, sh)}
        Else
            repaintRects = New List(Of Rectangle)(entry.DirtyRects)
        End If

        Using bg As Graphics = Graphics.FromImage(entry.Bmp)
            For Each repaintRect In repaintRects
                repaintRect = Rectangle.Intersect(New Rectangle(0, 0, sw, sh), repaintRect)
                If repaintRect.Width <= 0 OrElse repaintRect.Height <= 0 Then Continue For
                Dim oldMode = bg.CompositingMode
                bg.CompositingMode = Drawing2D.CompositingMode.SourceCopy
                Using transparentBrush As New SolidBrush(Color.Transparent)
                    bg.FillRectangle(transparentBrush, repaintRect)
                End Using
                bg.CompositingMode = oldMode

                Dim state = bg.Save()
                Try
                    bg.SetClip(repaintRect)
                    Using pea As New PaintEventArgs(bg, repaintRect)
                        Using D2DHelperV2.EnterBackgroundSamplingPaint()
                            InvokePaintBackgroundProxy(source, pea)
                            InvokePaintProxy(source, pea)
                        End Using
                    End Using
                Finally
                    bg.Restore(state)
                End Try
            Next
        End Using
        If fullRebuild Then
            ' 同 V1：修复 D2D + GDI BitBlt 引发的 alpha=0 写穿问题，并顺手识别纯色采样结果。
            Dim solidArgb As Integer
            entry.IsSolidColor = ForceOpaqueAlphaAndDetectSolid(entry.Bmp, solidArgb)
            entry.SolidArgb = solidArgb
        Else
            ForceOpaqueAlpha(entry.Bmp, repaintRects)
            entry.IsSolidColor = False
        End If

        entry.FullDirty = False
        entry.DirtyRects.Clear()
    End Sub

    Private Function AcquireCropBitmap(entry As Entry, rt As ID2D1RenderTarget, sourceBounds As Rectangle) As ID2D1Bitmap
        Dim sourceRect As Rectangle = sourceBounds
        Dim cropWidth As Integer = sourceRect.Width
        Dim cropHeight As Integer = sourceRect.Height
        For Each crop In entry.Crops
            Dim ownerAlive As Boolean = crop.D2DOwnerRT IsNot Nothing AndAlso ReferenceEquals(crop.D2DOwnerRT.Target, rt)
            If ownerAlive AndAlso crop.Width = cropWidth AndAlso crop.Height = cropHeight AndAlso crop.SourceRect.Equals(sourceRect) Then
                Return crop.D2DBmp
            End If
        Next

        Dim newCrop As New Entry.CropEntry With {
            .SourceRect = sourceRect,
            .Width = cropWidth,
            .Height = cropHeight,
            .D2DBmp = CreateCropD2DBitmap(entry.Bmp, sourceRect, cropWidth, cropHeight, rt),
            .D2DOwnerRT = New WeakReference(rt)
        }
        If newCrop.D2DBmp Is Nothing Then Return Nothing
        entry.Crops.Add(newCrop)
        TrimCropEntries(entry, newCrop)
        Return newCrop.D2DBmp
    End Function

    Private Function CreateCropD2DBitmap(src As Bitmap, sourceRect As Rectangle, cropWidth As Integer,
                                         cropHeight As Integer, rt As ID2D1RenderTarget) As ID2D1Bitmap
        If src Is Nothing OrElse rt Is Nothing Then Return Nothing
        Using cropBmp As New Bitmap(cropWidth, cropHeight, PixelFormat.Format32bppPArgb)
            Using g = Graphics.FromImage(cropBmp)
                g.Clear(Color.Transparent)
                Dim srcBounds As New Rectangle(0, 0, src.Width, src.Height)
                Dim srcIntersection = Rectangle.Intersect(srcBounds, sourceRect)
                If srcIntersection.Width > 0 AndAlso srcIntersection.Height > 0 Then
                    Dim destRect As New Rectangle(srcIntersection.X - sourceRect.X, srcIntersection.Y - sourceRect.Y,
                                                  srcIntersection.Width, srcIntersection.Height)
                    g.DrawImage(src, destRect, srcIntersection, GraphicsUnit.Pixel)
                End If
            End Using
            Return D2DHelper.CreateBitmapFromGdi(rt, cropBmp)
        End Using
    End Function

    Private Sub TrimCropEntries(entry As Entry, Optional protectedCrop As Entry.CropEntry = Nothing)
        Dim maxCropsPerSource As Integer = Math.Max(0, GlobalOptions.BackgroundPenetrationCropCacheMaxEntriesPerSource)
        While entry.Crops.Count > maxCropsPerSource
            Dim removeIndex As Integer = -1
            For i As Integer = 0 To entry.Crops.Count - 1
                If protectedCrop Is Nothing OrElse Not ReferenceEquals(entry.Crops(i), protectedCrop) Then
                    removeIndex = i
                    Exit For
                End If
            Next
            If removeIndex < 0 Then Exit While
            Dim crop = entry.Crops(removeIndex)
            Try : crop.D2DBmp?.Dispose() : Catch : End Try
            entry.Crops.RemoveAt(removeIndex)
        End While
    End Sub

    Private Function DirtyRectsIntersect(entry As Entry, rect As Rectangle) As Boolean
        If entry.FullDirty Then Return True
        For Each dirty In entry.DirtyRects
            If dirty.IntersectsWith(rect) Then Return True
        Next
        Return False
    End Function

    Private Sub InvalidateCropEntries(entry As Entry, dirtyRect As Rectangle?)
        For i As Integer = entry.Crops.Count - 1 To 0 Step -1
            Dim crop = entry.Crops(i)
            If Not dirtyRect.HasValue OrElse crop.SourceRect.IntersectsWith(dirtyRect.Value) Then
                Try : crop.D2DBmp?.Dispose() : Catch : End Try
                entry.Crops.RemoveAt(i)
            End If
        Next
    End Sub

#End Region

#Region "source 事件"

    Private Sub OnSourceInvalidated(sender As Object, e As InvalidateEventArgs)
        Dim source = TryCast(sender, Control)
        If source Is Nothing Then Return
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then
                Dim dirtyRect As Rectangle = If(e IsNot Nothing, e.InvalidRect, Rectangle.Empty)
                If dirtyRect.Width <= 0 OrElse dirtyRect.Height <= 0 Then
                    entry.FullDirty = True
                    InvalidateCropEntries(entry, Nothing)
                Else
                    entry.DirtyRects.Add(dirtyRect)
                    InvalidateCropEntries(entry, dirtyRect)
                End If
            End If
        End SyncLock
    End Sub

    Private Sub OnSourceResized(sender As Object, e As EventArgs)
        Dim source = TryCast(sender, Control)
        If source Is Nothing Then Return
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then
                entry.FullDirty = True
                InvalidateCropEntries(entry, Nothing)
            End If
        End SyncLock
    End Sub

    Private Sub OnSourceDisposed(sender As Object, e As EventArgs)
        Dim source = TryCast(sender, Control)
        If source Is Nothing Then Return
        RemoveHandler source.Disposed, AddressOf OnSourceDisposed
        RemoveHandler source.Invalidated, AddressOf OnSourceInvalidated
        RemoveHandler source.Resize, AddressOf OnSourceResized
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then
                Try : entry.Bmp?.Dispose() : Catch : End Try
                InvalidateCropEntries(entry, Nothing)
                _cache.Remove(source)
            End If
        End SyncLock
    End Sub

#End Region

#Region "辅助"

    Private Function ComputeOffset(child As Control, source As Control) As Point
        Dim ox As Integer = 0, oy As Integer = 0
        Dim ctrl As Control = child
        While ctrl IsNot Nothing AndAlso ctrl IsNot source
            ox += ctrl.Left
            oy += ctrl.Top
            ctrl = ctrl.Parent
        End While
        If ctrl Is source Then Return New Point(ox, oy)
        Return source.PointToClient(child.PointToScreen(Point.Empty))
    End Function

    Private ReadOnly _invokePaintBackground As Action(Of Control, Control, PaintEventArgs) =
        CreateInvoker("InvokePaintBackground")
    Private ReadOnly _invokePaint As Action(Of Control, Control, PaintEventArgs) =
        CreateInvoker("InvokePaint")

    Private Function CreateInvoker(name As String) As Action(Of Control, Control, PaintEventArgs)
        Dim mi = GetType(Control).GetMethod(name,
            Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic,
            Nothing, New Type() {GetType(Control), GetType(PaintEventArgs)}, Nothing)
        Return CType(mi.CreateDelegate(GetType(Action(Of Control, Control, PaintEventArgs))),
                     Action(Of Control, Control, PaintEventArgs))
    End Function

    Private Sub InvokePaintBackgroundProxy(source As Control, pea As PaintEventArgs)
        _invokePaintBackground(source, source, pea)
    End Sub

    Private Sub InvokePaintProxy(source As Control, pea As PaintEventArgs)
        _invokePaint(source, source, pea)
    End Sub

    ''' <summary>把 32bpp 位图的 alpha 全部置为 255，并检测结果是否为纯色。</summary>
    Private Function ForceOpaqueAlphaAndDetectSolid(bmp As Bitmap, ByRef solidArgb As Integer) As Boolean
        solidArgb = 0
        If bmp Is Nothing Then Return False
        Dim rect As New Rectangle(0, 0, bmp.Width, bmp.Height)
        Dim data As BitmapData = Nothing
        Try
            data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb)
            Dim stride As Integer = data.Stride
            Dim rowBytes As Integer = Math.Abs(stride)
            Dim h As Integer = data.Height
            Dim w As Integer = data.Width
            Dim scan0 As IntPtr = data.Scan0
            Dim row(rowBytes - 1) As Byte
            Dim haveFirst As Boolean = False
            Dim firstB As Byte = 0, firstG As Byte = 0, firstR As Byte = 0
            Dim solid As Boolean = True
            For y As Integer = 0 To h - 1
                Dim rowPtr As IntPtr = IntPtr.Add(scan0, y * stride)
                Runtime.InteropServices.Marshal.Copy(rowPtr, row, 0, rowBytes)
                Dim x As Integer = 3
                For i As Integer = 0 To w - 1
                    Dim b As Byte = row(x - 3)
                    Dim g As Byte = row(x - 2)
                    Dim r As Byte = row(x - 1)
                    If Not haveFirst Then
                        firstB = b : firstG = g : firstR = r
                        haveFirst = True
                    ElseIf solid AndAlso (b <> firstB OrElse g <> firstG OrElse r <> firstR) Then
                        solid = False
                    End If
                    row(x) = 255
                    x += 4
                Next
                Runtime.InteropServices.Marshal.Copy(row, 0, rowPtr, rowBytes)
            Next
            If haveFirst Then solidArgb = Color.FromArgb(255, firstR, firstG, firstB).ToArgb()
            Return solid AndAlso haveFirst
        Catch
            Return False
        Finally
            If data IsNot Nothing Then
                Try : bmp.UnlockBits(data) : Catch : End Try
            End If
        End Try
    End Function

    Private Sub ForceOpaqueAlpha(bmp As Bitmap, rects As IEnumerable(Of Rectangle))
        If bmp Is Nothing OrElse rects Is Nothing Then Return
        Dim bounds As New Rectangle(0, 0, bmp.Width, bmp.Height)
        For Each rect In rects
            rect = Rectangle.Intersect(bounds, rect)
            If rect.Width <= 0 OrElse rect.Height <= 0 Then Continue For
            Dim data As BitmapData = Nothing
            Try
                data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb)
                Dim stride As Integer = data.Stride
                Dim rowBytes As Integer = Math.Abs(stride)
                Dim row(rowBytes - 1) As Byte
                For y As Integer = 0 To data.Height - 1
                    Dim rowPtr As IntPtr = IntPtr.Add(data.Scan0, y * stride)
                    Runtime.InteropServices.Marshal.Copy(rowPtr, row, 0, rowBytes)
                    Dim x As Integer = 3
                    For i As Integer = 0 To data.Width - 1
                        row(x) = 255
                        x += 4
                    Next
                    Runtime.InteropServices.Marshal.Copy(row, 0, rowPtr, rowBytes)
                Next
            Catch
            Finally
                If data IsNot Nothing Then
                    Try : bmp.UnlockBits(data) : Catch : End Try
                End If
            End Try
        Next
    End Sub

#End Region

End Module
