Imports System.Drawing.Imaging
Imports System.Numerics
Imports System.Runtime.InteropServices
Imports Vortice.Direct2D1
Imports Vortice.DirectWrite
Imports Vortice.DXGI

''' <summary>
''' Direct2D / DirectWrite 全局共享层（V2 体系的"共享基座"）。
'''
''' 旧兼容模块原本同时承担：①全局工厂与质量策略 / 类型转换 / 资源缓存
''' （进程级，跨控件复用），②控件级 <c>PaintScope</c> 渲染管线（含 SSAA BitmapRT 缓存）。
''' V2 的窗口级合成器（<see cref="WindowCompositor"/> / <see cref="PaintScopeV2"/>）已完全取代后者，
''' 因此本文件只保留①里的"全局基座"，控件级 PaintScope 已从代码库移除。
'''
''' 全局设置项集中放在 <see cref="GlobalOptions"/>；本模块只负责执行 D2D / DWrite 资源创建、
''' 类型转换、上传缓存与把当前全局设置应用到 RenderTarget。
''' </summary>
Public Module D2DGlobals

#Region "DPI"

    ' V2 文本/DPI 统一入口：
    ' WinForms 在 PerMonitorV2 下会在 DPI 改变时调整控件字体与 AutoScaleDimensions，
    ' 但嵌入到标签页里的非 TopLevel Form 往往不会自然收到完整的窗口 DPI 上下文。
    ' 因此所有 D2D/DirectWrite 绘制路径都通过这里取“当前宿主窗口 DPI”，并用窗口句柄缓存补齐
    ' 句柄尚未可见、WinForms DeviceDpi 尚未同步、或嵌入 Form 需要继承宿主 DPI 的情况。
    <DllImport("user32.dll")>
    Private Function GetDpiForWindow(hwnd As IntPtr) As UInteger
    End Function

    Private ReadOnly _windowDpiLock As New Object()
    Private ReadOnly _windowDpi As New Dictionary(Of IntPtr, Integer)()

    Private Function TryGetDpiForWindow(hwnd As IntPtr) As Integer
        If hwnd = IntPtr.Zero Then Return 0
        Try
            Dim dpi As Integer = CInt(GetDpiForWindow(hwnd))
            If dpi > 0 Then Return dpi
        Catch
        End Try
        Return 0
    End Function

    Private Function TryGetCachedWindowDpi(hwnd As IntPtr) As Integer
        If hwnd = IntPtr.Zero Then Return 0
        SyncLock _windowDpiLock
            Dim cachedDpi As Integer = 0
            If _windowDpi.TryGetValue(hwnd, cachedDpi) AndAlso cachedDpi > 0 Then Return cachedDpi
        End SyncLock
        Return 0
    End Function

    Private Function TryGetDpiFromFormWindow(form As Form) As Integer
        If form Is Nothing OrElse form.IsDisposed OrElse Not form.IsHandleCreated Then Return 0

        Dim hwnd = form.Handle
        Dim windowDpi As Integer = TryGetDpiForWindow(hwnd)
        If windowDpi > 0 Then
            Dim cachedDpi As Integer = TryGetCachedWindowDpi(hwnd)
            If cachedDpi <> windowDpi Then SetWindowDpi(hwnd, windowDpi)
            Return windowDpi
        End If

        Return TryGetCachedWindowDpi(hwnd)
    End Function

    Private Function ResolveDpiAuthorityForm(form As Form) As Form
        If form Is Nothing OrElse form.IsDisposed Then Return form

        Try
            If Not form.TopLevel AndAlso form.Parent IsNot Nothing AndAlso Not form.Parent.IsDisposed Then
                Dim host = form.Parent.FindForm()
                If host IsNot Nothing AndAlso Not host.IsDisposed AndAlso host IsNot form Then Return host
            End If
        Catch
        End Try

        Return form
    End Function

    Friend Sub SetWindowDpi(hwnd As IntPtr, dpi As Integer)
        If hwnd = IntPtr.Zero OrElse dpi <= 0 Then Return
        SyncLock _windowDpiLock
            _windowDpi(hwnd) = dpi
        End SyncLock
    End Sub

    Friend Sub ClearWindowDpi(hwnd As IntPtr)
        If hwnd = IntPtr.Zero Then Return
        SyncLock _windowDpiLock
            _windowDpi.Remove(hwnd)
        End SyncLock
    End Sub

    Friend Function ExtractDpiFromWParam(wParam As IntPtr) As Integer
        Dim v As Long = wParam.ToInt64()
        Dim dpiX As Integer = CInt(v And &HFFFFL)
        Dim dpiY As Integer = CInt((v >> 16) And &HFFFFL)
        Return Math.Max(dpiX, dpiY)
    End Function

    Public Function GetCurrentDpi(control As Control) As Integer
        If control IsNot Nothing AndAlso Not control.IsDisposed Then
            Dim form As Form = Nothing
            Try
                form = If(TypeOf control Is Form, DirectCast(control, Form), control.FindForm())
            Catch
                form = Nothing
            End Try

            If form IsNot Nothing AndAlso Not form.IsDisposed AndAlso form.IsHandleCreated Then
                Dim authorityForm As Form = ResolveDpiAuthorityForm(form)
                If authorityForm IsNot Nothing AndAlso Not authorityForm.IsDisposed AndAlso authorityForm.IsHandleCreated Then
                    Dim authorityDpi As Integer = TryGetDpiFromFormWindow(authorityForm)
                    If authorityDpi > 0 Then
                        If authorityForm IsNot form AndAlso form.IsHandleCreated Then SetWindowDpi(form.Handle, authorityDpi)
                        Return authorityDpi
                    End If
                End If

                Dim formDpi As Integer = TryGetDpiFromFormWindow(form)
                If formDpi > 0 Then Return formDpi
            End If

            If control.IsHandleCreated Then
                Dim dpi As Integer = TryGetDpiForWindow(control.Handle)
                If dpi > 0 Then Return dpi
            End If

            Try
                If control.DeviceDpi > 0 Then Return control.DeviceDpi
            Catch
            End Try
        End If

        Return 96
    End Function

    Public Function GetCurrentDpiScale(control As Control) As Single
        Return Math.Max(0.01F, CSng(GetCurrentDpi(control)) / 96.0F)
    End Function

    ''' <summary>
    ''' 返回 DirectWrite 应使用的物理像素字号。
    ''' </summary>
    ''' <remarks>
    ''' 不要在普通控件文字路径继续使用 <c>font.SizeInPoints * 96 / 72 * DpiScale</c>。
    ''' WinForms 自动缩放后 <see cref="Font.SizeInPoints"/> 已经可能包含当前 DPI 的缩放结果，
    ''' 再乘当前 DPI 会二次放大；而未激活的嵌入页面又可能仍保留 96 DPI 字体。
    ''' 这里用 <c>Font.Height / Font.GetHeight(96)</c> 反推出字体实际基准 DPI，
    ''' 让 DirectWrite 的字号跟 WinForms/GDI 当前字体状态一致。
    ''' </remarks>
    Public Function GetDWriteFontSizePx(font As Font, dpiScale As Single) As Single
        If font Is Nothing Then Return 1.0F

        Dim fallbackDpi As Integer = 96
        If Not Single.IsNaN(dpiScale) AndAlso Not Single.IsInfinity(dpiScale) AndAlso dpiScale > 0.0F Then
            fallbackDpi = Math.Max(1, CInt(Math.Round(dpiScale * 96.0F)))
        End If

        Dim baselineDpi As Integer = GetDWriteFontBaselineDpi(font, fallbackDpi)
        Return Math.Max(1.0F, CSng(font.SizeInPoints * baselineDpi / 72.0F))
    End Function

    ''' <summary>
    ''' 返回与 <see cref="GetDWriteFontSizePx"/> 同源的布局行高，避免布局与绘制分别套用 DPI。
    ''' </summary>
    Public Function GetDWriteLineHeightPx(font As Font, dpiScale As Single) As Single
        If font Is Nothing Then Return 1.0F

        Dim sizePx As Single = GetDWriteFontSizePx(font, dpiScale)
        Dim designPx As Single = CSng(font.SizeInPoints * 96.0F / 72.0F)
        Try
            Dim designHeight As Single = font.GetHeight(96.0F)
            If designPx > 0.01F AndAlso designHeight > 0.01F Then
                Return Math.Max(1.0F, sizePx * designHeight / designPx)
            End If
        Catch
        End Try

        Return Math.Max(1.0F, sizePx)
    End Function

    Public Function GetDWriteFontBaselineDpi(font As Font, fallbackDpi As Integer) As Integer
        fallbackDpi = Math.Max(1, fallbackDpi)
        If font Is Nothing Then Return fallbackDpi

        Try
            Dim height96 As Single = font.GetHeight(96.0F)
            Dim currentHeight As Single = CSng(font.Height)
            If height96 > 0.01F AndAlso currentHeight > 0.01F Then
                Dim inferredDpi As Single = currentHeight * 96.0F / height96
                If Not Single.IsNaN(inferredDpi) AndAlso Not Single.IsInfinity(inferredDpi) AndAlso
                   inferredDpi >= 48.0F AndAlso inferredDpi <= 768.0F Then
                    Return NormalizeDWriteFontBaselineDpi(inferredDpi, fallbackDpi)
                End If
            End If
        Catch
        End Try

        Return fallbackDpi
    End Function

    Private Function NormalizeDWriteFontBaselineDpi(inferredDpi As Single, fallbackDpi As Integer) As Integer
        Dim rounded As Integer = CInt(Math.Round(inferredDpi / 12.0F, MidpointRounding.AwayFromZero) * 12.0F)
        If rounded < 48 OrElse rounded > 768 Then Return Math.Max(1, fallbackDpi)
        Return rounded
    End Function

#End Region

#Region "全局质量策略"

    ''' <summary>
    ''' 丢弃缓存的 Outline RenderingParams。请优先通过
    ''' <see cref="GlobalOptions.InvalidateOutlineRenderingParams"/> 调用。
    ''' </summary>
    Public Sub InvalidateOutlineRenderingParams()
        SyncLock _factoryLock
            If _outlineRenderingParams IsNot Nothing Then
                Try : _outlineRenderingParams.Dispose() : Catch : End Try
                _outlineRenderingParams = Nothing
            End If
        End SyncLock
    End Sub

    ''' <summary>
    ''' 将全局抗锯齿与文本质量策略一次性应用到指定 RT。
    ''' 任何新建的 RT（DC RT 或 BitmapRT）都应在 BeginDraw 前调用一次。
    ''' V2 的 <see cref="PaintScopeV2"/> 已在内部对 DC RT 与 SSAA BitmapRT 自动调用，控件作者通常无需手动调用。
    ''' </summary>
    Public Sub ApplyGlobalQuality(rt As ID2D1RenderTarget)
        If rt Is Nothing Then Return
        rt.AntialiasMode = GlobalOptions.GlobalAntialiasMode
        Select Case GlobalOptions.GlobalTextQuality
            Case GlobalOptions.TextQualityMode.Grayscale
                rt.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Grayscale
                rt.TextRenderingParams = Nothing
            Case GlobalOptions.TextQualityMode.Aliased
                rt.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Aliased
                rt.TextRenderingParams = Nothing
            Case GlobalOptions.TextQualityMode.Outline
                ' Outline 渲染模式产生的是几何 alpha 覆盖，不携带子像素信息，
                ' 必须搭配 Grayscale TextAntialiasMode；用 Cleartype 反而会触发 D2D 内部回退路径。
                rt.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Grayscale
                rt.TextRenderingParams = GetOutlineRenderingParams()
            Case Else
                rt.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Cleartype
                rt.TextRenderingParams = Nothing
        End Select
    End Sub

    Private _outlineRenderingParams As IDWriteRenderingParams

    Private Function GetOutlineRenderingParams() As IDWriteRenderingParams
        If _outlineRenderingParams Is Nothing Then
            SyncLock _factoryLock
                If _outlineRenderingParams Is Nothing Then
                    Dim dw = GetDWriteFactory()
                    Dim opt = GlobalOptions.OutlineText

                    Dim dw2 As IDWriteFactory2 = TryCast(dw, IDWriteFactory2)
                    If dw2 IsNot Nothing Then
                        Try
                            _outlineRenderingParams = dw2.CreateCustomRenderingParams(
                                opt.Gamma,
                                opt.EnhancedContrast,
                                opt.GrayscaleEnhancedContrast,
                                opt.ClearTypeLevel,
                                opt.PixelGeometry,
                                opt.RenderingMode,
                                opt.GridFitMode)
                        Catch
                            _outlineRenderingParams = Nothing
                        End Try
                    End If

                    If _outlineRenderingParams Is Nothing Then
                        _outlineRenderingParams = dw.CreateCustomRenderingParams(
                            opt.Gamma,
                            opt.EnhancedContrast,
                            opt.ClearTypeLevel,
                            opt.PixelGeometry,
                            opt.RenderingMode)
                    End If
                End If
            End SyncLock
        End If
        Return _outlineRenderingParams
    End Function

#End Region

#Region "工厂"

    Private _d2dFactory As ID2D1Factory1
    Private _dwFactory As IDWriteFactory
    Private ReadOnly _factoryLock As New Object()
    Private _roundStrokeStyle As ID2D1StrokeStyle
    Private _roundStrokeStyleWithDashCap As ID2D1StrokeStyle
    Private ReadOnly _strokeStyles As New Dictionary(Of Integer, ID2D1StrokeStyle)()
    Private ReadOnly _bitmapCacheRegistry As New List(Of WeakReference(Of D2DBitmapCache))()
    Private ReadOnly _bitmapCacheRegistryLock As New Object()

    Friend Sub CleanupD2DResources(level As D2DCacheCleanupLevel)
        Select Case level
            Case D2DCacheCleanupLevel.TrimToBudget
                TrimRegisteredBitmapCaches()
                TrimFontResolveCacheToCurrentLimit()

            Case D2DCacheCleanupLevel.ReleaseVolatileCaches
                InvalidateOutlineRenderingParams()
                TrimRegisteredBitmapCaches()
                TrimFontResolveCacheToCurrentLimit()

            Case D2DCacheCleanupLevel.ReleaseAllCaches,
                 D2DCacheCleanupLevel.ReleaseRenderTargets,
                 D2DCacheCleanupLevel.RecreateDevice
                InvalidateOutlineRenderingParams()
                InvalidateRegisteredBitmapCaches()
                ClearFontResolveCache()

            Case Else
                InvalidateRegisteredBitmapCaches()
                ClearFontResolveCache()
                ReleaseFactoryResources()
        End Select
    End Sub

    Private Sub ReleaseFactoryResources()
        SyncLock _factoryLock
            If _outlineRenderingParams IsNot Nothing Then
                Try : _outlineRenderingParams.Dispose() : Catch : End Try
                _outlineRenderingParams = Nothing
            End If
            For Each style In _strokeStyles.Values
                Try : style.Dispose() : Catch : End Try
            Next
            _strokeStyles.Clear()
            _roundStrokeStyle = Nothing
            _roundStrokeStyleWithDashCap = Nothing
            If _d2dFactory IsNot Nothing Then
                Try : _d2dFactory.Dispose() : Catch : End Try
                _d2dFactory = Nothing
            End If
            If _dwFactory IsNot Nothing Then
                Try : _dwFactory.Dispose() : Catch : End Try
                _dwFactory = Nothing
            End If
        End SyncLock
    End Sub

    Private Sub RegisterBitmapCache(cache As D2DBitmapCache)
        If cache Is Nothing Then Return
        SyncLock _bitmapCacheRegistryLock
            CompactBitmapCacheRegistryNoLock()
            _bitmapCacheRegistry.Add(New WeakReference(Of D2DBitmapCache)(cache))
        End SyncLock
    End Sub

    Private Sub TrimRegisteredBitmapCaches()
        SyncLock _bitmapCacheRegistryLock
            For Each wr In _bitmapCacheRegistry
                Dim cache As D2DBitmapCache = Nothing
                If wr.TryGetTarget(cache) AndAlso cache IsNot Nothing Then
                    Try : cache.TrimToCurrentBudget() : Catch : End Try
                End If
            Next
            CompactBitmapCacheRegistryNoLock()
        End SyncLock
    End Sub

    Private Sub InvalidateRegisteredBitmapCaches()
        SyncLock _bitmapCacheRegistryLock
            For Each wr In _bitmapCacheRegistry
                Dim cache As D2DBitmapCache = Nothing
                If wr.TryGetTarget(cache) AndAlso cache IsNot Nothing Then
                    Try : cache.Invalidate() : Catch : End Try
                End If
            Next
            CompactBitmapCacheRegistryNoLock()
        End SyncLock
    End Sub

    Private Sub CompactBitmapCacheRegistryNoLock()
        For i As Integer = _bitmapCacheRegistry.Count - 1 To 0 Step -1
            Dim cache As D2DBitmapCache = Nothing
            If Not _bitmapCacheRegistry(i).TryGetTarget(cache) OrElse cache Is Nothing Then
                _bitmapCacheRegistry.RemoveAt(i)
            End If
        Next
    End Sub

    ''' <summary>进程级 <see cref="ID2D1Factory"/> 单例（SingleThreaded）。不要 Dispose。</summary>
    Public Function GetD2DFactory() As ID2D1Factory
        Return GetD2DFactory1()
    End Function

    ''' <summary>
    ''' 进程级 <see cref="ID2D1Factory1"/> 单例（SingleThreaded）。不要 Dispose。
    ''' <para>
    ''' V2 阶段 A 把内部 factory 实例升级为 <c>ID2D1Factory1</c>，
    ''' 既不影响所有现有 <c>GetD2DFactory</c> 调用（ID2D1Factory1 派生自 ID2D1Factory），
    ''' 又能让 <see cref="D3D11Globals"/> 通过 <c>CreateDevice(dxgiDevice)</c> 建出 D2D Device。
    ''' </para>
    ''' </summary>
    Public Function GetD2DFactory1() As ID2D1Factory1
        If _d2dFactory Is Nothing Then
            SyncLock _factoryLock
                If _d2dFactory Is Nothing Then
                    _d2dFactory = D2D1.D2D1CreateFactory(Of ID2D1Factory1)(Vortice.Direct2D1.FactoryType.SingleThreaded)
                End If
            End SyncLock
        End If
        Return _d2dFactory
    End Function

    ''' <summary>进程级 <see cref="IDWriteFactory"/> 单例（Shared）。不要 Dispose。</summary>
    Public Function GetDWriteFactory() As IDWriteFactory
        If _dwFactory Is Nothing Then
            SyncLock _factoryLock
                If _dwFactory Is Nothing Then
                    _dwFactory = DWrite.DWriteCreateFactory(Of IDWriteFactory)(Vortice.DirectWrite.FactoryType.Shared)
                End If
            End SyncLock
        End If
        Return _dwFactory
    End Function

    ''' <summary>
    ''' 进程级圆头实线描边样式。StrokeStyle 由 D2D factory 创建，不绑定 RenderTarget，可跨控件复用。
    ''' </summary>
    Public Function GetRoundStrokeStyle(Optional roundDashCap As Boolean = False) As ID2D1StrokeStyle
        If roundDashCap Then
            If _roundStrokeStyleWithDashCap Is Nothing Then
                SyncLock _factoryLock
                    If _roundStrokeStyleWithDashCap Is Nothing Then
                        _roundStrokeStyleWithDashCap = GetStrokeStyle(CapStyle.Round, CapStyle.Round, CapStyle.Round)
                    End If
                End SyncLock
            End If
            Return _roundStrokeStyleWithDashCap
        End If

        If _roundStrokeStyle Is Nothing Then
            SyncLock _factoryLock
                If _roundStrokeStyle Is Nothing Then
                    _roundStrokeStyle = GetStrokeStyle(CapStyle.Round, CapStyle.Round, CapStyle.Flat)
                End If
            End SyncLock
        End If
        Return _roundStrokeStyle
    End Function

    Friend Function GetStrokeStyle(startCap As CapStyle, endCap As CapStyle,
                                   Optional dashCap As CapStyle = CapStyle.Flat,
                                   Optional lineJoin As Vortice.Direct2D1.LineJoin = Vortice.Direct2D1.LineJoin.Round) As ID2D1StrokeStyle
        Dim key As Integer = (CInt(startCap) << 24) Xor (CInt(endCap) << 16) Xor (CInt(dashCap) << 8) Xor CInt(lineJoin)
        SyncLock _factoryLock
            Dim style As ID2D1StrokeStyle = Nothing
            If _strokeStyles.TryGetValue(key, style) Then Return style
            style = CreateStrokeStyle(startCap, endCap, dashCap, lineJoin)
            _strokeStyles(key) = style
            Return style
        End SyncLock
    End Function

    Private Function CreateStrokeStyle(startCap As CapStyle, endCap As CapStyle,
                                       dashCap As CapStyle,
                                       lineJoin As Vortice.Direct2D1.LineJoin) As ID2D1StrokeStyle
        Return GetD2DFactory().CreateStrokeStyle(
            New StrokeStyleProperties With {
                .StartCap = startCap,
                .EndCap = endCap,
                .DashCap = dashCap,
                .LineJoin = lineJoin,
                .DashStyle = Vortice.Direct2D1.DashStyle.Solid,
                .MiterLimit = 10.0F
            })
    End Function

#End Region

#Region "类型转换"

    Public Function ToColor4(c As Color) As Vortice.Mathematics.Color4
        Return New Vortice.Mathematics.Color4(c.R / 255.0F, c.G / 255.0F, c.B / 255.0F, c.A / 255.0F)
    End Function

    Public Function ToColor4(c As Color, overrideAlpha As Single) As Vortice.Mathematics.Color4
        Return New Vortice.Mathematics.Color4(c.R / 255.0F, c.G / 255.0F, c.B / 255.0F, Math.Max(0F, Math.Min(1.0F, overrideAlpha)))
    End Function

    Public Function ToD2DRect(r As RectangleF) As Vortice.Mathematics.Rect
        Return New Vortice.Mathematics.Rect(r.X, r.Y, r.Width, r.Height)
    End Function

    Public Function ToD2DRect(r As Rectangle) As Vortice.Mathematics.Rect
        Return New Vortice.Mathematics.Rect(r.X, r.Y, r.Width, r.Height)
    End Function

#End Region

#Region "DC 渲染目标"

    ''' <summary>
    ''' 创建一个未绑定 DC 的 <see cref="ID2D1DCRenderTarget"/>。
    ''' 像素格式：B8G8R8A8_UNorm，AlphaMode = Ignore（与 GDI HDC 兼容）。
    ''' V2 由 <see cref="WindowCompositor"/> 共享一份；外部不再需要直接调用本函数。
    ''' </summary>
    Public Function CreateDCRenderTarget() As ID2D1DCRenderTarget
        Dim rtp As New RenderTargetProperties(
            RenderTargetType.Default,
            New Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Ignore),
            96.0F, 96.0F,
            RenderTargetUsage.None,
            Vortice.Direct2D1.FeatureLevel.Default)
        Return GetD2DFactory().CreateDCRenderTarget(rtp)
    End Function

#End Region

#Region "Bitmap 上传"

    ''' <summary>
    ''' 把 GDI <see cref="Image"/> 上传为 <see cref="ID2D1Bitmap"/>（Premultiplied BGRA）。
    ''' 调用方负责 Dispose。仅适合一次性上传；每帧使用的图标 / 背景图应通过
    ''' <see cref="D2DBitmapCache.GetBitmap"/> 复用。
    ''' </summary>
    Public Function CreateBitmapFromImage(rt As ID2D1RenderTarget, img As Image) As ID2D1Bitmap
        If rt Is Nothing OrElse img Is Nothing Then Return Nothing

        Try
            Dim sourceBitmap = TryCast(img, Bitmap)
            If sourceBitmap IsNot Nothing AndAlso sourceBitmap.PixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppPArgb Then
                ' Safe fast path: this only uploads CPU bitmap pixels into the same D2D RenderTarget.
                ' It does not use the D2D1.1 DeviceContext path, whose resources cannot be shared
                ' with the current DC RT stage and previously caused invisible image results.
                Dim directBitmap = CreateBitmapFromGdi(rt, sourceBitmap)
                If directBitmap IsNot Nothing Then Return directBitmap
            End If
        Catch
            ' Fall back to the normalization path below; some Bitmap instances can still reject
            ' pixel-format access or LockBits because of their backing store or lifetime.
        End Try

        Dim bmp As New Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb)
        bmp.SetResolution(img.HorizontalResolution, img.VerticalResolution)
        Using g = Graphics.FromImage(bmp)
            g.DrawImage(img, 0, 0, img.Width, img.Height)
        End Using
        Try
            Return CreateBitmapFromGdi(rt, bmp)
        Finally
            bmp.Dispose()
        End Try
    End Function

    ''' <summary>直接从 GDI <see cref="Bitmap"/> 上传为 <see cref="ID2D1Bitmap"/>。调用方负责 Dispose。</summary>
    Public Function CreateBitmapFromGdi(rt As ID2D1RenderTarget, bmp As Bitmap) As ID2D1Bitmap
        If rt Is Nothing OrElse bmp Is Nothing Then Return Nothing
        Return CreateBitmapFromGdi(rt, bmp, New Rectangle(0, 0, bmp.Width, bmp.Height))
    End Function

    ''' <summary>直接从 GDI <see cref="Bitmap"/> 的指定区域上传为 <see cref="ID2D1Bitmap"/>。调用方负责 Dispose。</summary>
    Public Function CreateBitmapFromGdi(rt As ID2D1RenderTarget, bmp As Bitmap, sourceRect As Rectangle) As ID2D1Bitmap
        If rt Is Nothing OrElse bmp Is Nothing Then Return Nothing
        sourceRect = Rectangle.Intersect(New Rectangle(0, 0, bmp.Width, bmp.Height), sourceRect)
        If sourceRect.Width <= 0 OrElse sourceRect.Height <= 0 Then Return Nothing

        Dim data As BitmapData = bmp.LockBits(
            sourceRect,
            ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb)
        Try
            Dim props As New BitmapProperties(
                New Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied))
            Return rt.CreateBitmap(
                New Vortice.Mathematics.SizeI(sourceRect.Width, sourceRect.Height),
                data.Scan0, CUInt(data.Stride), props)
        Finally
            bmp.UnlockBits(data)
        End Try
    End Function

#End Region

#Region "缓存：Bitmap / Brush / TextFormat"

    ''' <summary>
    ''' 缓存一个 GDI <see cref="Image"/> 上传得到的 <see cref="ID2D1Bitmap"/>，避免每帧重新上传。
    ''' 仅当源 Image 引用或目标 RT 发生变化时重新上传；缓存项不强持有源 Image。
    ''' </summary>
    ''' <remarks>
    ''' 资源所有权：
    ''' • 调用方拥有 D2DBitmapCache 实例，通常挂在 <see cref="WindowCompositor"/> 的 Image 索引里。
    ''' • cache 内部按 RenderTarget 持有 ID2D1Bitmap；RenderTarget 被释放时，compositor 必须调用
    '''   <see cref="InvalidateFor"/> 清掉对应条目。
    ''' • cache 不强持有源 Image，但 WindowCompositor 的索引字典会强持有 Image key；临时 Bitmap
    '''   不要进入该索引，否则会活到 LRU 清理或 Form Dispose。
    '''
    ''' 坑点：
    ''' • D2D bitmap 与创建它的 RenderTarget 绑定，不能跨 DC RT / SSAA RT / D2D 1.1 DeviceContext 复用。
    ''' • 预算按“同一个 Image 在不同 RT 上的上传副本”估算，不包含源 Image 自身的 RAM。
    ''' </remarks>
    Public Class D2DBitmapCache
        Implements IDisposable

        Private NotInheritable Class Entry
            Public Bitmap As ID2D1Bitmap
            Public SourceRef As WeakReference(Of Image)
            Public RenderTarget As ID2D1RenderTarget
            Public Bytes As Long
            Public LastUsed As Long
        End Class

        Private ReadOnly _entries As New Dictionary(Of ID2D1RenderTarget, Entry)()
        Private _bytes As Long
        Private _clock As Long

        Public Sub New()
            RegisterBitmapCache(Me)
        End Sub

        Public Function GetBitmap(rt As ID2D1RenderTarget, src As Image) As ID2D1Bitmap
            If src Is Nothing OrElse rt Is Nothing Then
                Invalidate()
                Return Nothing
            End If
            Dim entry As Entry = Nothing
            If _entries.TryGetValue(rt, entry) Then
                Dim entrySource As Image = Nothing
                If entry.SourceRef IsNot Nothing AndAlso
                   entry.SourceRef.TryGetTarget(entrySource) AndAlso
                   entrySource Is src AndAlso
                   entry.Bitmap IsNot Nothing Then
                    entry.LastUsed = NextClock()
                    Return entry.Bitmap
                End If
                RemoveEntry(rt)
            End If

            Dim bmp = CreateBitmapFromImage(rt, src)
            If bmp Is Nothing Then Return Nothing

            entry = New Entry With {
                .Bitmap = bmp,
                .SourceRef = New WeakReference(Of Image)(src),
                .RenderTarget = rt,
                .Bytes = EstimateBytes(src),
                .LastUsed = NextClock()
            }
            _entries(rt) = entry
            _bytes += entry.Bytes
            TrimToBudget(entry)
            Return bmp
        End Function

        Public Sub Invalidate()
            For Each entry In _entries.Values
                Try : entry.Bitmap.Dispose() : Catch : End Try
            Next
            _entries.Clear()
            _bytes = 0
        End Sub

        Public Sub InvalidateFor(rt As ID2D1RenderTarget)
            If rt Is Nothing Then Return
            RemoveEntry(rt)
        End Sub

        Private Function NextClock() As Long
            _clock += 1
            Return _clock
        End Function

        Private Shared Function EstimateBytes(src As Image) As Long
            If src Is Nothing Then Return 0
            Return CLng(Math.Max(1, src.Width)) * CLng(Math.Max(1, src.Height)) * 4L
        End Function

        Private Sub RemoveEntry(rt As ID2D1RenderTarget)
            Dim entry As Entry = Nothing
            If Not _entries.TryGetValue(rt, entry) Then Return
            _entries.Remove(rt)
            _bytes -= entry.Bytes
            Try : entry.Bitmap.Dispose() : Catch : End Try
        End Sub

        Private Sub TrimToBudget(Optional protectedEntry As Entry = Nothing)
            Dim budget As Long = Math.Max(0L, GlobalOptions.D2DBitmapCacheBudgetBytes)
            While _bytes > budget AndAlso _entries.Count > 0
                Dim oldestRt As ID2D1RenderTarget = Nothing
                Dim oldestEntry As Entry = Nothing
                For Each kv In _entries
                    If protectedEntry IsNot Nothing AndAlso ReferenceEquals(kv.Value, protectedEntry) Then Continue For
                    If oldestEntry Is Nothing OrElse kv.Value.LastUsed < oldestEntry.LastUsed Then
                        oldestRt = kv.Key
                        oldestEntry = kv.Value
                    End If
                Next
                If oldestRt Is Nothing Then Exit While
                RemoveEntry(oldestRt)
            End While
        End Sub

        Friend Sub TrimToCurrentBudget()
            TrimToBudget()
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Invalidate()
        End Sub
    End Class

    Friend Structure ResolvedTextFont
        Public Family As String
        Public Weight As Vortice.DirectWrite.FontWeight
        Public Style As Vortice.DirectWrite.FontStyle
        Public Stretch As Vortice.DirectWrite.FontStretch
    End Structure

    Private Structure FontResolveKey
        Public FaceName As String
        Public Weight As Vortice.DirectWrite.FontWeight
        Public Style As Vortice.DirectWrite.FontStyle
        Public Stretch As Vortice.DirectWrite.FontStretch
    End Structure

    Private NotInheritable Class FontResolveEntry
        Public Value As ResolvedTextFont
        Public LastUsed As Long
    End Class

    Private ReadOnly _fontResolveCache As New Dictionary(Of FontResolveKey, FontResolveEntry)(16)
    Private ReadOnly _fontResolveLock As New Object()
    Private _fontResolveClock As Long

    Friend Function ResolveTextFont(font As Font) As ResolvedTextFont
        If font Is Nothing Then
            Return CreateFallbackTextFont("", Vortice.DirectWrite.FontWeight.Normal,
                                          Vortice.DirectWrite.FontStyle.Normal,
                                          Vortice.DirectWrite.FontStretch.Normal)
        End If

        Dim familyName As String = If(String.IsNullOrWhiteSpace(font.Name), font.FontFamily.Name, font.Name)
        Return ResolveTextFont(
            familyName,
            If(font.Bold, Vortice.DirectWrite.FontWeight.Bold, Vortice.DirectWrite.FontWeight.Normal),
            If(font.Italic, Vortice.DirectWrite.FontStyle.Italic, Vortice.DirectWrite.FontStyle.Normal),
            Vortice.DirectWrite.FontStretch.Normal)
    End Function

    Friend Function ResolveTextFont(family As String,
                                    weight As Vortice.DirectWrite.FontWeight,
                                    style As Vortice.DirectWrite.FontStyle,
                                    stretch As Vortice.DirectWrite.FontStretch) As ResolvedTextFont
        Dim fallback = CreateFallbackTextFont(family, weight, style, stretch)

        Dim key As New FontResolveKey With {
            .FaceName = fallback.Family,
            .Weight = fallback.Weight,
            .Style = fallback.Style,
            .Stretch = fallback.Stretch
        }

        SyncLock _fontResolveLock
            Dim cached As FontResolveEntry = Nothing
            If _fontResolveCache.TryGetValue(key, cached) Then
                cached.LastUsed = NextFontResolveClock()
                Return cached.Value
            End If
        End SyncLock

        Dim resolved = ResolveTextFontNameUncached(fallback)

        SyncLock _fontResolveLock
            If GlobalOptions.DWriteFontResolveCacheMaxEntries <= 0 Then
                _fontResolveCache.Clear()
                Return resolved
            End If
            _fontResolveCache(key) = New FontResolveEntry With {
                .Value = resolved,
                .LastUsed = NextFontResolveClock()
            }
            TrimFontResolveCache(key)
        End SyncLock

        Return resolved
    End Function

    Private Function NextFontResolveClock() As Long
        _fontResolveClock += 1
        Return _fontResolveClock
    End Function

    Private Sub TrimFontResolveCacheToCurrentLimit()
        SyncLock _fontResolveLock
            TrimFontResolveCache(Nothing, False)
        End SyncLock
    End Sub

    Private Sub ClearFontResolveCache()
        SyncLock _fontResolveLock
            _fontResolveCache.Clear()
            _fontResolveClock = 0
        End SyncLock
    End Sub

    Private Sub TrimFontResolveCache(protectedKey As FontResolveKey)
        TrimFontResolveCache(protectedKey, True)
    End Sub

    Private Sub TrimFontResolveCache(protectedKey As FontResolveKey, hasProtectedKey As Boolean)
        Dim maxEntries As Integer = Math.Max(0, GlobalOptions.DWriteFontResolveCacheMaxEntries)
        While _fontResolveCache.Count > maxEntries AndAlso _fontResolveCache.Count > 0
            Dim oldestKey As FontResolveKey = Nothing
            Dim oldestEntry As FontResolveEntry = Nothing
            Dim found As Boolean
            For Each kv In _fontResolveCache
                If hasProtectedKey AndAlso kv.Key.Equals(protectedKey) Then Continue For
                If oldestEntry Is Nothing OrElse kv.Value.LastUsed < oldestEntry.LastUsed Then
                    oldestKey = kv.Key
                    oldestEntry = kv.Value
                    found = True
                End If
            Next
            If Not found Then Exit While
            _fontResolveCache.Remove(oldestKey)
        End While
    End Sub

    Friend Function CreateTextFormat(font As Font, sizePx As Single) As IDWriteTextFormat
        Return CreateTextFormat(ResolveTextFont(font), sizePx)
    End Function

    Friend Function CreateTextFormat(family As String,
                                     weight As Vortice.DirectWrite.FontWeight,
                                     style As Vortice.DirectWrite.FontStyle,
                                     stretch As Vortice.DirectWrite.FontStretch,
                                     sizePx As Single) As IDWriteTextFormat
        Return CreateTextFormat(ResolveTextFont(family, weight, style, stretch), sizePx)
    End Function

    Friend Function CreateTextFormat(resolved As ResolvedTextFont, sizePx As Single) As IDWriteTextFormat
        Return GetDWriteFactory().CreateTextFormat(resolved.Family, Nothing, resolved.Weight,
                                                   resolved.Style, resolved.Stretch, sizePx)
    End Function

    Private Function CreateFallbackTextFont(family As String,
                                            weight As Vortice.DirectWrite.FontWeight,
                                            style As Vortice.DirectWrite.FontStyle,
                                            stretch As Vortice.DirectWrite.FontStretch) As ResolvedTextFont
        Return New ResolvedTextFont With {
            .Family = If(family, ""),
            .Weight = If(CInt(weight) <= 0, Vortice.DirectWrite.FontWeight.Normal, weight),
            .Style = style,
            .Stretch = If(stretch = Vortice.DirectWrite.FontStretch.Undefined,
                          Vortice.DirectWrite.FontStretch.Normal, stretch)
        }
    End Function

    Private Function ResolveTextFontNameUncached(fallback As ResolvedTextFont) As ResolvedTextFont
        If String.IsNullOrWhiteSpace(fallback.Family) OrElse DWriteFamilyExists(fallback.Family) Then Return fallback

        Dim candidate As ResolvedTextFont = fallback
        Dim familyName As String = fallback.Family.Trim()
        Dim changed As Boolean

        Do
            changed = ConsumeKnownFontNameSuffix(familyName, candidate)
        Loop While changed AndAlso familyName.Length > 0

        If familyName.Length > 0 AndAlso
           Not String.Equals(familyName, fallback.Family, StringComparison.OrdinalIgnoreCase) AndAlso
           DWriteFamilyExists(familyName) Then
            candidate.Family = familyName
            Return candidate
        End If

        Return fallback
    End Function

    Private Function ConsumeKnownFontNameSuffix(ByRef familyName As String,
                                                ByRef resolved As ResolvedTextFont) As Boolean
        If ConsumeSuffix(familyName, "ExtraBlack") OrElse ConsumeSuffix(familyName, "UltraBlack") OrElse
           ConsumeSuffix(familyName, "Extra Black") OrElse ConsumeSuffix(familyName, "Ultra Black") Then
            resolved.Weight = Vortice.DirectWrite.FontWeight.ExtraBlack
            Return True
        End If
        If ConsumeSuffix(familyName, "ExtraBold") OrElse ConsumeSuffix(familyName, "UltraBold") OrElse
           ConsumeSuffix(familyName, "Extra Bold") OrElse ConsumeSuffix(familyName, "Ultra Bold") Then
            resolved.Weight = Vortice.DirectWrite.FontWeight.ExtraBold
            Return True
        End If
        If ConsumeSuffix(familyName, "DemiBold") OrElse ConsumeSuffix(familyName, "SemiBold") OrElse
           ConsumeSuffix(familyName, "Demi Bold") OrElse ConsumeSuffix(familyName, "Semi Bold") Then
            resolved.Weight = Vortice.DirectWrite.FontWeight.DemiBold
            Return True
        End If
        If ConsumeSuffix(familyName, "ExtraLight") OrElse ConsumeSuffix(familyName, "UltraLight") OrElse
           ConsumeSuffix(familyName, "Extra Light") OrElse ConsumeSuffix(familyName, "Ultra Light") Then
            resolved.Weight = Vortice.DirectWrite.FontWeight.ExtraLight
            Return True
        End If
        If ConsumeSuffix(familyName, "SemiLight") OrElse ConsumeSuffix(familyName, "Semi Light") Then
            resolved.Weight = Vortice.DirectWrite.FontWeight.SemiLight
            Return True
        End If
        If ConsumeSuffix(familyName, "Bold") Then
            resolved.Weight = Vortice.DirectWrite.FontWeight.Bold
            Return True
        End If
        If ConsumeSuffix(familyName, "Medium") Then
            resolved.Weight = Vortice.DirectWrite.FontWeight.Medium
            Return True
        End If
        If ConsumeSuffix(familyName, "Regular") OrElse ConsumeSuffix(familyName, "Normal") Then
            resolved.Weight = Vortice.DirectWrite.FontWeight.Normal
            Return True
        End If
        If ConsumeSuffix(familyName, "Light") Then
            resolved.Weight = Vortice.DirectWrite.FontWeight.Light
            Return True
        End If
        If ConsumeSuffix(familyName, "Thin") Then
            resolved.Weight = Vortice.DirectWrite.FontWeight.Thin
            Return True
        End If
        If ConsumeSuffix(familyName, "Black") OrElse ConsumeSuffix(familyName, "Heavy") Then
            resolved.Weight = Vortice.DirectWrite.FontWeight.Black
            Return True
        End If
        If ConsumeSuffix(familyName, "Italic") Then
            resolved.Style = Vortice.DirectWrite.FontStyle.Italic
            Return True
        End If
        If ConsumeSuffix(familyName, "Oblique") Then
            resolved.Style = Vortice.DirectWrite.FontStyle.Oblique
            Return True
        End If
        If ConsumeSuffix(familyName, "UltraCondensed") OrElse ConsumeSuffix(familyName, "Ultra Condensed") Then
            resolved.Stretch = Vortice.DirectWrite.FontStretch.UltraCondensed
            Return True
        End If
        If ConsumeSuffix(familyName, "ExtraCondensed") OrElse ConsumeSuffix(familyName, "Extra Condensed") Then
            resolved.Stretch = Vortice.DirectWrite.FontStretch.ExtraCondensed
            Return True
        End If
        If ConsumeSuffix(familyName, "SemiCondensed") OrElse ConsumeSuffix(familyName, "Semi Condensed") Then
            resolved.Stretch = Vortice.DirectWrite.FontStretch.SemiCondensed
            Return True
        End If
        If ConsumeSuffix(familyName, "Condensed") Then
            resolved.Stretch = Vortice.DirectWrite.FontStretch.Condensed
            Return True
        End If
        If ConsumeSuffix(familyName, "UltraExpanded") OrElse ConsumeSuffix(familyName, "Ultra Expanded") Then
            resolved.Stretch = Vortice.DirectWrite.FontStretch.UltraExpanded
            Return True
        End If
        If ConsumeSuffix(familyName, "ExtraExpanded") OrElse ConsumeSuffix(familyName, "Extra Expanded") Then
            resolved.Stretch = Vortice.DirectWrite.FontStretch.ExtraExpanded
            Return True
        End If
        If ConsumeSuffix(familyName, "SemiExpanded") OrElse ConsumeSuffix(familyName, "Semi Expanded") Then
            resolved.Stretch = Vortice.DirectWrite.FontStretch.SemiExpanded
            Return True
        End If
        If ConsumeSuffix(familyName, "Expanded") Then
            resolved.Stretch = Vortice.DirectWrite.FontStretch.Expanded
            Return True
        End If

        Return False
    End Function

    Private Function ConsumeSuffix(ByRef value As String, suffix As String) As Boolean
        Dim token As String = " " & suffix
        If Not value.EndsWith(token, StringComparison.OrdinalIgnoreCase) Then
            token = "-" & suffix
            If Not value.EndsWith(token, StringComparison.OrdinalIgnoreCase) Then Return False
        End If
        value = value.Substring(0, value.Length - token.Length).TrimEnd(" "c, "-"c)
        Return True
    End Function

    Private Function DWriteFamilyExists(familyName As String) As Boolean
        If String.IsNullOrWhiteSpace(familyName) Then Return False

        Dim collection As IDWriteFontCollection = Nothing
        Try
            collection = GetDWriteFactory().GetSystemFontCollection(False)
            Dim index As UInteger = 0
            Return collection.FindFamilyName(familyName, index)
        Catch
            Return False
        Finally
            If collection IsNot Nothing Then Try : collection.Dispose() : Catch : End Try
        End Try
    End Function

    ''' <summary>
    ''' 跨帧复用 <see cref="ID2D1SolidColorBrush"/>。按 (RT 引用, ARGB) 缓存，
    ''' 避免热路径每帧 <c>Using ... CreateSolidColorBrush ... End Using</c> 造成的 D2D 资源分配开销。
    ''' </summary>
    Public Class SolidColorBrushCache
        Implements IDisposable

        Private NotInheritable Class BrushEntry
            Public Brush As ID2D1SolidColorBrush
            Public LastUsed As Long
        End Class

        Private ReadOnly _buckets As New Dictionary(Of ID2D1RenderTarget, Dictionary(Of Integer, BrushEntry))(4)
        Private _clock As Long

        Public Function [Get](rt As ID2D1RenderTarget, c As Color) As ID2D1SolidColorBrush
            If rt Is Nothing Then Return Nothing
            Dim bucket As Dictionary(Of Integer, BrushEntry) = Nothing
            If Not _buckets.TryGetValue(rt, bucket) Then
                bucket = New Dictionary(Of Integer, BrushEntry)(8)
                _buckets(rt) = bucket
            End If
            Dim key As Integer = c.ToArgb()
            Dim entry As BrushEntry = Nothing
            If bucket.TryGetValue(key, entry) Then
                entry.LastUsed = NextClock()
                Return entry.Brush
            End If
            Dim b = rt.CreateSolidColorBrush(ToColor4(c))
            bucket(key) = New BrushEntry With {.Brush = b, .LastUsed = NextClock()}
            ' Animation colors can create many one-off ARGB brushes; LRU keeps exact colors without unbounded growth.
            TrimBucket(bucket, key)
            Return b
        End Function

        Private Function NextClock() As Long
            _clock += 1
            Return _clock
        End Function

        Private Sub TrimBucket(bucket As Dictionary(Of Integer, BrushEntry), protectedKey As Integer)
            Dim maxEntries As Integer = Math.Max(0, GlobalOptions.D2DBrushCacheMaxEntriesPerRenderTarget)
            While bucket.Count > maxEntries AndAlso bucket.Count > 0
                Dim oldestKey As Integer = 0
                Dim oldestEntry As BrushEntry = Nothing
                For Each kv In bucket
                    If kv.Key = protectedKey Then Continue For
                    If oldestEntry Is Nothing OrElse kv.Value.LastUsed < oldestEntry.LastUsed Then
                        oldestKey = kv.Key
                        oldestEntry = kv.Value
                    End If
                Next
                If oldestEntry Is Nothing Then Exit While
                bucket.Remove(oldestKey)
                Try : oldestEntry.Brush.Dispose() : Catch : End Try
            End While
        End Sub

        Friend Sub TrimToCurrentLimit()
            Dim maxEntries As Integer = Math.Max(0, GlobalOptions.D2DBrushCacheMaxEntriesPerRenderTarget)
            For Each bucket In _buckets.Values
                While bucket.Count > maxEntries AndAlso bucket.Count > 0
                    Dim oldestKey As Integer = 0
                    Dim oldestEntry As BrushEntry = Nothing
                    For Each kv In bucket
                        If oldestEntry Is Nothing OrElse kv.Value.LastUsed < oldestEntry.LastUsed Then
                            oldestKey = kv.Key
                            oldestEntry = kv.Value
                        End If
                    Next
                    If oldestEntry Is Nothing Then Exit While
                    bucket.Remove(oldestKey)
                    Try : oldestEntry.Brush.Dispose() : Catch : End Try
                End While
            Next
        End Sub

        Public Sub Invalidate()
            InvalidateInternal()
        End Sub

        Public Sub InvalidateFor(rt As ID2D1RenderTarget)
            If rt Is Nothing Then Return
            Dim bucket As Dictionary(Of Integer, BrushEntry) = Nothing
            If _buckets.TryGetValue(rt, bucket) Then
                For Each entry In bucket.Values
                    Try : entry.Brush.Dispose() : Catch : End Try
                Next
                _buckets.Remove(rt)
            End If
        End Sub

        Private Sub InvalidateInternal()
            For Each bucket In _buckets.Values
                For Each entry In bucket.Values
                    Try : entry.Brush.Dispose() : Catch : End Try
                Next
            Next
            _buckets.Clear()
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Invalidate()
        End Sub
    End Class

    ''' <summary>
    ''' 跨帧复用 <see cref="IDWriteTextFormat"/>。按 (family, weight, style, sizePx, textAlignment, paragraphAlignment, wrap) 缓存。
    ''' DirectWrite TextFormat 不绑定 RT，可全局共享。
    ''' </summary>
    Public Class TextFormatCache
        Implements IDisposable

        Private Structure Key
            Public Family As String
            Public Weight As Vortice.DirectWrite.FontWeight
            Public Style As Vortice.DirectWrite.FontStyle
            Public Stretch As Vortice.DirectWrite.FontStretch
            Public SizePx As Single
            Public TextAlign As Vortice.DirectWrite.TextAlignment
            Public ParaAlign As Vortice.DirectWrite.ParagraphAlignment
            Public Trim As Boolean
            Public Wrap As Boolean
        End Structure

        Private NotInheritable Class TextFormatEntry
            Public Format As IDWriteTextFormat
            Public LastUsed As Long
        End Class

        Private ReadOnly _map As New Dictionary(Of Key, TextFormatEntry)(4)
        Private _clock As Long

        Public Function [Get](family As String, weight As Vortice.DirectWrite.FontWeight,
                              style As Vortice.DirectWrite.FontStyle, sizePx As Single,
                              textAlign As Vortice.DirectWrite.TextAlignment,
                              paraAlign As Vortice.DirectWrite.ParagraphAlignment,
                              trimChar As Boolean) As IDWriteTextFormat
            Return [Get](family, weight, style, sizePx, textAlign, paraAlign, trimChar, False)
        End Function

        Public Function [Get](family As String, weight As Vortice.DirectWrite.FontWeight,
                              style As Vortice.DirectWrite.FontStyle, sizePx As Single,
                              textAlign As Vortice.DirectWrite.TextAlignment,
                              paraAlign As Vortice.DirectWrite.ParagraphAlignment,
                              trimChar As Boolean,
                              wordWrap As Boolean) As IDWriteTextFormat
            Return GetResolved(ResolveTextFont(family, weight, style, Vortice.DirectWrite.FontStretch.Normal),
                               sizePx, textAlign, paraAlign, trimChar, wordWrap)
        End Function

        Public Function [Get](family As String, weight As Vortice.DirectWrite.FontWeight,
                              style As Vortice.DirectWrite.FontStyle,
                              stretch As Vortice.DirectWrite.FontStretch,
                              sizePx As Single,
                              textAlign As Vortice.DirectWrite.TextAlignment,
                              paraAlign As Vortice.DirectWrite.ParagraphAlignment,
                              trimChar As Boolean,
                              wordWrap As Boolean) As IDWriteTextFormat
            Return GetResolved(ResolveTextFont(family, weight, style, stretch),
                               sizePx, textAlign, paraAlign, trimChar, wordWrap)
        End Function

        Public Function [Get](font As Font, sizePx As Single,
                              textAlign As Vortice.DirectWrite.TextAlignment,
                              paraAlign As Vortice.DirectWrite.ParagraphAlignment,
                              trimChar As Boolean) As IDWriteTextFormat
            Return [Get](font, sizePx, textAlign, paraAlign, trimChar, False)
        End Function

        Public Function [Get](font As Font, sizePx As Single,
                              textAlign As Vortice.DirectWrite.TextAlignment,
                              paraAlign As Vortice.DirectWrite.ParagraphAlignment,
                              trimChar As Boolean,
                              wordWrap As Boolean) As IDWriteTextFormat
            Return GetResolved(ResolveTextFont(font), sizePx, textAlign, paraAlign, trimChar, wordWrap)
        End Function

        Private Function GetResolved(resolved As ResolvedTextFont, sizePx As Single,
                                    textAlign As Vortice.DirectWrite.TextAlignment,
                                    paraAlign As Vortice.DirectWrite.ParagraphAlignment,
                                    trimChar As Boolean,
                                    wordWrap As Boolean) As IDWriteTextFormat
            Dim k As New Key With {
                .Family = If(resolved.Family, ""),
                .Weight = resolved.Weight,
                .Style = resolved.Style,
                .Stretch = resolved.Stretch,
                .SizePx = sizePx,
                .TextAlign = textAlign,
                .ParaAlign = paraAlign,
                .Trim = trimChar,
                .Wrap = wordWrap
            }
            Dim entry As TextFormatEntry = Nothing
            If _map.TryGetValue(k, entry) Then
                entry.LastUsed = NextClock()
                Return entry.Format
            End If
            Dim fmt As IDWriteTextFormat = Nothing
            fmt = CreateTextFormat(resolved, sizePx)
            fmt.TextAlignment = textAlign
            fmt.ParagraphAlignment = paraAlign
            fmt.WordWrapping = If(wordWrap, WordWrapping.Wrap, WordWrapping.NoWrap)
            If trimChar Then
                Try
                    fmt.SetTrimming(New Trimming With {.Granularity = TrimmingGranularity.Character}, Nothing)
                Catch
                End Try
            End If
            _map(k) = New TextFormatEntry With {.Format = fmt, .LastUsed = NextClock()}
            ' TextLayout remains per draw; trimming only TextFormat preserves layout quality while bounding cache lifetime.
            TrimToLimit(k)
            Return fmt
        End Function

        Private Function NextClock() As Long
            _clock += 1
            Return _clock
        End Function

        Private Sub TrimToLimit(protectedKey As Key)
            Dim maxEntries As Integer = Math.Max(0, GlobalOptions.DWriteTextFormatCacheMaxEntriesPerCompositor)
            While _map.Count > maxEntries AndAlso _map.Count > 0
                Dim oldestKey As Key = Nothing
                Dim oldestEntry As TextFormatEntry = Nothing
                Dim found As Boolean = False
                For Each kv In _map
                    If kv.Key.Equals(protectedKey) Then Continue For
                    If oldestEntry Is Nothing OrElse kv.Value.LastUsed < oldestEntry.LastUsed Then
                        oldestKey = kv.Key
                        oldestEntry = kv.Value
                        found = True
                    End If
                Next
                If Not found Then Exit While
                _map.Remove(oldestKey)
                Try : oldestEntry.Format.Dispose() : Catch : End Try
            End While
        End Sub

        Friend Sub TrimToCurrentLimit()
            Dim maxEntries As Integer = Math.Max(0, GlobalOptions.DWriteTextFormatCacheMaxEntriesPerCompositor)
            While _map.Count > maxEntries AndAlso _map.Count > 0
                Dim oldestKey As Key = Nothing
                Dim oldestEntry As TextFormatEntry = Nothing
                Dim found As Boolean = False
                For Each kv In _map
                    If oldestEntry Is Nothing OrElse kv.Value.LastUsed < oldestEntry.LastUsed Then
                        oldestKey = kv.Key
                        oldestEntry = kv.Value
                        found = True
                    End If
                Next
                If Not found Then Exit While
                _map.Remove(oldestKey)
                Try : oldestEntry.Format.Dispose() : Catch : End Try
            End While
        End Sub

        Public Sub Invalidate()
            For Each entry In _map.Values
                Try : entry.Format.Dispose() : Catch : End Try
            Next
            _map.Clear()
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Invalidate()
        End Sub
    End Class

#End Region

#Region "Layer 辅助"

    ''' <summary>
    ''' 用几何裁剪 PushLayer。第二参数传 Nothing 让 D2D 内部 layer 池接管，避免每帧 CreateLayer/Dispose。
    ''' 调用方负责对应的 <see cref="ID2D1RenderTarget.PopLayer"/>。
    ''' </summary>
    Public Sub PushGeometryClip(rt As ID2D1RenderTarget, geo As ID2D1Geometry, contentBounds As RectangleF)
        Dim lp As New LayerParameters With {
            .ContentBounds = New Vortice.RawRectF(contentBounds.X, contentBounds.Y, contentBounds.Right, contentBounds.Bottom),
            .GeometricMask = geo,
            .MaskAntialiasMode = AntialiasMode.PerPrimitive,
            .MaskTransform = Matrix3x2.Identity,
            .Opacity = 1.0F,
            .OpacityBrush = Nothing,
            .LayerOptions = LayerOptions.None
        }
        rt.PushLayer(lp, Nothing)
    End Sub

#End Region

End Module
