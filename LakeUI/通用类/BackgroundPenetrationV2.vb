Imports System.Drawing.Imaging
Imports Vortice.Direct2D1

''' <summary>
''' V2 透明背景穿透实现。<see cref="TransparentBackgroundCache"/> 的精简继任者。
'''
''' === 与 V1 (<see cref="TransparentBackgroundCache"/>) 的差异 ===
''' • V2 必须显式指定背景源（child 控件的 <c>BackgroundSource</c> 属性）。未指定 → 不采样、不绘制，
'''   完全不画背景层。不再自动回退到 child.Parent，避免无意识的递归与采样链。
''' • 每个 source 在窗口内共享一份 GDI Bitmap + ID2D1Bitmap，按 source 的
'''   <see cref="Control.Invalidated"/> / <see cref="Control.Resize"/> / <see cref="Control.Disposed"/> 失效。
'''   不再为每个透明子控件维护独立 entry。
''' • 不做 TTL 节流 / Dirty 节流 / LRU 字节预算 —— V2 的设计前提是 source 数量极少（Form 内通常 1~3）。
'''   命中条件：尺寸一致 + 未脏 + D2D RT 没换。
''' • 不区分 GDI / D2D 消费者：始终保留一份 GDI Bitmap 作权威，ID2D1Bitmap 按需上传到当前 RT。
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
''' • <see cref="Invalidate"/> 用于换主题等需要立即重采的极端场景，常规情况下不需要手动调用。
'''
''' === 线程要求 ===
''' UI 线程。对 _cache 字典有锁，但 GDI / D2D 调用本身仍假定在 UI 线程。
''' </summary>
Public Module BackgroundPenetrationV2

    Private Class Entry
        Public Bmp As Bitmap
        Public Width As Integer
        Public Height As Integer
        Public Dirty As Boolean = True
        Public Painting As Boolean
        Public D2DBmp As ID2D1Bitmap
        Public D2DOwnerRT As WeakReference
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

        Dim offset As Point = ComputeOffset(child, source)
        Dim srcRect As New Rectangle(offset.X, offset.Y, child.Width, child.Height)
        Dim destRect As New Rectangle(0, 0, child.Width, child.Height)

        Dim rt = scope.BackgroundLayer
        Dim isSolid As Boolean
        Dim solidColor As Color = Color.Empty
        Dim d2dBmp = AcquireD2DBitmap(source, rt, isSolid, solidColor)
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
            D2DHelper.ToD2DRect(srcRect))
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
            If _cache.TryGetValue(source, entry) Then entry.Dirty = True
        End SyncLock
    End Sub

#End Region

#Region "缓存内部"

    Private Function AcquireD2DBitmap(source As Control, rt As ID2D1RenderTarget,
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
            If Not sizeOk Then entry.Dirty = True
            needRebuild = entry.Dirty
            If needRebuild Then entry.Painting = True
        End SyncLock

        If needRebuild Then
            Try
                RebuildGdiBitmap(source, entry, sw, sh)
                ' GDI 位图变了，丢掉旧的 D2D 上传
                If entry.D2DBmp IsNot Nothing Then
                    Try : entry.D2DBmp.Dispose() : Catch : End Try
                    entry.D2DBmp = Nothing
                    entry.D2DOwnerRT = Nothing
                End If
                entry.Dirty = False
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

        ' 上传 / 命中 D2D 位图
        Dim ownerAlive As Boolean = entry.D2DOwnerRT IsNot Nothing AndAlso ReferenceEquals(entry.D2DOwnerRT.Target, rt)
        If entry.D2DBmp Is Nothing OrElse Not ownerAlive Then
            If entry.D2DBmp IsNot Nothing Then
                Try : entry.D2DBmp.Dispose() : Catch : End Try
            End If
            entry.D2DBmp = D2DHelper.CreateBitmapFromGdi(rt, entry.Bmp)
            entry.D2DOwnerRT = New WeakReference(rt)
        End If
        Return entry.D2DBmp
    End Function

    Private Sub RebuildGdiBitmap(source As Control, entry As Entry, sw As Integer, sh As Integer)
        If entry.Bmp Is Nothing OrElse entry.Width <> sw OrElse entry.Height <> sh Then
            entry.Bmp?.Dispose()
            entry.Bmp = New Bitmap(sw, sh, PixelFormat.Format32bppPArgb)
            entry.Width = sw
            entry.Height = sh
        End If
        Using bg As Graphics = Graphics.FromImage(entry.Bmp)
            bg.Clear(Color.Transparent)
            Using pea As New PaintEventArgs(bg, New Rectangle(0, 0, sw, sh))
                Using D2DHelperV2.EnterBackgroundSamplingPaint()
                    InvokePaintBackgroundProxy(source, pea)
                    InvokePaintProxy(source, pea)
                End Using
            End Using
        End Using
        ' 同 V1：修复 D2D + GDI BitBlt 引发的 alpha=0 写穿问题，并顺手识别纯色采样结果。
        Dim solidArgb As Integer
        entry.IsSolidColor = ForceOpaqueAlphaAndDetectSolid(entry.Bmp, solidArgb)
        entry.SolidArgb = solidArgb
    End Sub

#End Region

#Region "source 事件"

    Private Sub OnSourceInvalidated(sender As Object, e As EventArgs)
        Dim source = TryCast(sender, Control)
        If source Is Nothing Then Return
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then entry.Dirty = True
        End SyncLock
    End Sub

    Private Sub OnSourceResized(sender As Object, e As EventArgs)
        Dim source = TryCast(sender, Control)
        If source Is Nothing Then Return
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then entry.Dirty = True
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
                If entry.D2DBmp IsNot Nothing Then
                    Try : entry.D2DBmp.Dispose() : Catch : End Try
                End If
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

#End Region

End Module
