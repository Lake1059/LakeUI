Imports System.Drawing.Imaging
Imports System.Numerics
Imports Vortice.Direct2D1
Imports Vortice.DirectWrite
Imports Vortice.DXGI

''' <summary>
''' Direct2D / DirectWrite 全局共享层。所有控件接入 D2D 渲染时都应通过本模块。
'''
''' === 标准接入流程（控件作者必读） ===
'''   1. 控件字段：
'''        Private _dcRT As ID2D1DCRenderTarget
'''        Private ReadOnly _ssaaCache As New D2DHelper.BitmapRTCache()
'''        Private ReadOnly _iconCache As New D2DHelper.D2DBitmapCache()  ' 每个长期使用的 Image 一个
'''   2. OnPaint：
'''        If _dcRT Is Nothing Then _dcRT = D2DHelper.CreateDCRenderTarget()
'''        Using scope = D2DHelper.BeginPaint(e, Me, _dcRT, ssaa, _ssaaCache)
'''            ' 在 scope.GraphicsRenderTarget 上画"图形层"（背景/边框/图片/几何/遮罩）
'''            scope.FlushGraphics()
'''            ' 在 scope.DCRenderTarget 上画文字（DirectWrite 子像素质量）
'''        End Using
'''   3. Dispose 中：_ssaaCache.Dispose() / _iconCache.Dispose() / _dcRT?.Dispose()
'''   4. Resize / DPI 改变只需 Invalidate()；BitmapRTCache 会自动按 (Width, Height, ssaa) 重建。
'''
''' === 职责 ===
''' • 工厂单例（D2D / DWrite，进程内线程安全）。
''' • 全局质量策略（图形抗锯齿、文本渲染模式：ClearType / Grayscale / Aliased）。
''' • 通用类型转换（GDI Color / RectangleF ↔ D2D Color4 / Rect）。
''' • DC 渲染目标（ID2D1DCRenderTarget）的创建。
''' • GDI Image / Bitmap → ID2D1Bitmap 上传 + 跨帧缓存（D2DBitmapCache）。
''' • Paint 作用域（PaintScope，含基于 BitmapRenderTarget 的 D2D-SSAA 流程）。
''' • SSAA 离屏 BitmapRT 跨帧复用（BitmapRTCache）。
''' • Layer 推入辅助（PushGeometryClip，使用 D2D 内部 layer 池）。
'''
''' === 设计原则 ===
''' • 工厂为进程单例。
''' • DC RT 与窗口 DC 强相关，无法跨控件共享，由各控件自行持有。
''' • SSAA 仅用于"图形层"（背景/边框/图片/几何/遮罩）；文字应在 DC RT 上由 DirectWrite
'''   直接绘制以保留 ClearType 子像素信息。所以 PaintScope 同时暴露
'''   GraphicsRenderTarget（图形层）与 DCRenderTarget（文字层）。
''' • 任何"每帧创建/销毁"的 D2D 资源都应通过本模块的缓存/池化辅助接入。
''' </summary>
Public Module D2DHelper

#Region "全局质量策略"

    ''' <summary>D2D 文本渲染模式。</summary>
    Public Enum TextQualityMode
        ''' <summary>ClearType（默认）：兼容 MacType / 第三方钩子，保留子像素信息。</summary>
        ClearType
        ''' <summary>灰度抗锯齿：稳定、无彩边，但锐度略低于 ClearType。</summary>
        Grayscale
        ''' <summary>不抗锯齿：仅用于像素艺术 / 极小字号场景。</summary>
        Aliased
        ''' <summary>
        ''' Outline（仿 MacType "几何渲染"档）：使用 DirectWrite 的 RenderingMode.Outline，
        ''' 把字形当作纯矢量几何路径直接交给 D2D 抗锯齿管线绘制，
        ''' <b>完全跳过字体的 TrueType hinting 字节码与 GASP 表</b>。
        ''' 优点：彻底绕过"小字号禁用抗锯齿/强制贴格"策略，所有字号统一最高质量；
        ''' 副作用：Outline 模式不支持子像素 ClearType，会自动落回灰度 AA；极小字号（&lt; 10pt）会显得稍"虚"。
        ''' 启用本模式后 MacType 等基于 GDI 的钩子对 D2D 路径无效（D2D 本来就不经过 GDI）。
        ''' </summary>
        Outline
    End Enum

    ''' <summary>全局图形抗锯齿模式（不影响文字）。</summary>
    Public Property GlobalAntialiasMode As AntialiasMode = AntialiasMode.PerPrimitive

    ''' <summary>全局文本质量。默认 ClearType 以兼容第三方文字渲染钩子。</summary>
    Public Property GlobalTextQuality As TextQualityMode = TextQualityMode.ClearType

    ''' <summary>
    ''' Outline 模式的可调参数。所有字段都可在运行时改写；改写后调用
    ''' <see cref="InvalidateOutlineRenderingParams"/> 让下一帧生效。
    '''
    ''' 默认值是仿 MacType "几何渲染"档：
    '''   gamma 1.4 ─ 比系统默认 (~2.0) 低，黑字更"重"、灰阶更平滑；
    '''   enhancedContrast 0.5 ─ 适度的笔画强化（Outline 模式下过高会让字过粗失真）；
    '''   grayscaleEnhancedContrast 1.0 ─ 灰度路径上的 stem darkening；
    '''   clearTypeLevel 0 ─ Outline 模式不使用子像素；
    '''   RenderingMode = Outline；GridFitMode = Disabled。
    ''' </summary>
    Public Class OutlineTextOptions
        Public Property Gamma As Single = 1.4F
        Public Property EnhancedContrast As Single = 0.5F
        Public Property GrayscaleEnhancedContrast As Single = 1.0F
        Public Property ClearTypeLevel As Single = 0.0F
        Public Property PixelGeometry As Vortice.DirectWrite.PixelGeometry = Vortice.DirectWrite.PixelGeometry.Rgb
        Public Property RenderingMode As Vortice.DirectWrite.RenderingMode = Vortice.DirectWrite.RenderingMode.Outline
        Public Property GridFitMode As Vortice.DirectWrite.GridFitMode = Vortice.DirectWrite.GridFitMode.Disabled
    End Class

    ''' <summary>Outline 模式的可调参数。</summary>
    Public ReadOnly Property OutlineText As New OutlineTextOptions()

    ''' <summary>修改 <see cref="OutlineText"/> 后调用，丢弃缓存的 RenderingParams 让下一帧重建。</summary>
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
    ''' PaintScope 已在内部对 DC RT 与离屏 BitmapRT 自动调用，控件作者通常无需手动调用。
    ''' </summary>
    Public Sub ApplyGlobalQuality(rt As ID2D1RenderTarget)
        If rt Is Nothing Then Return
        rt.AntialiasMode = GlobalAntialiasMode
        Select Case GlobalTextQuality
            Case TextQualityMode.Grayscale
                rt.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Grayscale
                rt.TextRenderingParams = Nothing
            Case TextQualityMode.Aliased
                rt.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Aliased
                rt.TextRenderingParams = Nothing
            Case TextQualityMode.Outline
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

    ''' <summary>
    ''' 获取（或惰性创建）Outline 模式用的进程级 <see cref="IDWriteRenderingParams"/>。
    ''' 关键：RenderingMode = Outline，DirectWrite 走纯几何路径绘制字形，完全跳过 hinting / GASP。
    ''' </summary>
    Private Function GetOutlineRenderingParams() As IDWriteRenderingParams
        If _outlineRenderingParams Is Nothing Then
            SyncLock _factoryLock
                If _outlineRenderingParams Is Nothing Then
                    Dim dw = GetDWriteFactory()
                    Dim opt = OutlineText

                    ' 优先尝试 IDWriteFactory2 路径，可同时设置 GridFitMode 与 grayscaleEnhancedContrast
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

                    ' Fallback：基础 API 仅能设置 RenderingMode（不能关 GridFit / 无灰度对比度）
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

    Private _d2dFactory As ID2D1Factory
    Private _dwFactory As IDWriteFactory
    Private ReadOnly _factoryLock As New Object()

    ''' <summary>
    ''' 获取进程级 <see cref="ID2D1Factory"/> 单例（SingleThreaded）。
    ''' 用于创建几何（PathGeometry / RoundedRectangleGeometry 等）以及 DC RT。
    ''' 不要 Dispose 返回值。
    ''' </summary>
    Public Function GetD2DFactory() As ID2D1Factory
        If _d2dFactory Is Nothing Then
            SyncLock _factoryLock
                If _d2dFactory Is Nothing Then
                    _d2dFactory = D2D1.D2D1CreateFactory(Of ID2D1Factory)(Vortice.Direct2D1.FactoryType.SingleThreaded)
                End If
            End SyncLock
        End If
        Return _d2dFactory
    End Function

    ''' <summary>
    ''' 获取进程级 <see cref="IDWriteFactory"/> 单例（Shared）。
    ''' 用于 CreateTextFormat / CreateTextLayout。不要 Dispose 返回值。
    ''' </summary>
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

#End Region

#Region "类型转换"

    ''' <summary>GDI <see cref="Color"/> → D2D <see cref="Vortice.Mathematics.Color4"/>（含 Alpha）。</summary>
    Public Function ToColor4(c As Color) As Vortice.Mathematics.Color4
        Return New Vortice.Mathematics.Color4(c.R / 255.0F, c.G / 255.0F, c.B / 255.0F, c.A / 255.0F)
    End Function

    ''' <summary>GDI <see cref="Color"/> → <see cref="Vortice.Mathematics.Color4"/>，但 Alpha 用 [0,1] 显式覆盖。</summary>
    Public Function ToColor4(c As Color, overrideAlpha As Single) As Vortice.Mathematics.Color4
        Return New Vortice.Mathematics.Color4(c.R / 255.0F, c.G / 255.0F, c.B / 255.0F, Math.Max(0F, Math.Min(1.0F, overrideAlpha)))
    End Function

    ''' <summary><see cref="RectangleF"/> → D2D <see cref="Vortice.Mathematics.Rect"/>（location + size，保留浮点精度）。</summary>
    Public Function ToD2DRect(r As RectangleF) As Vortice.Mathematics.Rect
        Return New Vortice.Mathematics.Rect(r.X, r.Y, r.Width, r.Height)
    End Function

    ''' <summary>整数 <see cref="Rectangle"/> → D2D <see cref="Vortice.Mathematics.Rect"/>。</summary>
    Public Function ToD2DRect(r As Rectangle) As Vortice.Mathematics.Rect
        Return New Vortice.Mathematics.Rect(r.X, r.Y, r.Width, r.Height)
    End Function

#End Region

#Region "DC 渲染目标"

    ''' <summary>
    ''' 创建一个未绑定 DC 的 <see cref="ID2D1DCRenderTarget"/>。
    ''' 像素格式：B8G8R8A8_UNorm，AlphaMode = Ignore（与 GDI HDC 兼容）。
    ''' 控件首次 OnPaint 时按需创建一次并字段保存，控件 Dispose 时释放。
    ''' 不要在每帧重新创建。BindDC/BeginDraw/EndDraw 由 <see cref="PaintScope"/> 负责。
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
    ''' 调用方负责 Dispose。**仅适合一次性 / 偶发上传**：每帧都会调用的图像（图标、背景图）
    ''' 应改用 <see cref="D2DBitmapCache.GetBitmap"/> 以避免重复 GPU 上传。
    ''' </summary>
    Public Function CreateBitmapFromImage(rt As ID2D1RenderTarget, img As Image) As ID2D1Bitmap
        If img Is Nothing Then Return Nothing
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

    ''' <summary>
    ''' 直接从 GDI <see cref="Bitmap"/> 上传为 <see cref="ID2D1Bitmap"/>。
    ''' 比 <see cref="CreateBitmapFromImage"/> 少一次 Bitmap 包装；同样建议通过
    ''' <see cref="D2DBitmapCache"/> 缓存。调用方负责 Dispose。
    ''' </summary>
    Public Function CreateBitmapFromGdi(rt As ID2D1RenderTarget, bmp As Bitmap) As ID2D1Bitmap
        If rt Is Nothing OrElse bmp Is Nothing Then Return Nothing
        Dim data As BitmapData = bmp.LockBits(
            New Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb)
        Try
            Dim props As New BitmapProperties(
                New Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied))
            Return rt.CreateBitmap(
                New Vortice.Mathematics.SizeI(bmp.Width, bmp.Height),
                data.Scan0, CUInt(data.Stride), props)
        Finally
            bmp.UnlockBits(data)
        End Try
    End Function

#End Region

#Region "Paint Scope（含 D2D-SSAA）"

    ''' <summary>
    ''' D2D 绘制作用域。封装 GetHdc / BindDC / BeginDraw / EndDraw / ReleaseHdc，
    ''' 并在 SSAA 倍率 &gt; 1 时提供高分辨率 BitmapRenderTarget 作为图形层目标。
    '''
    ''' 使用模式：
    '''   Using scope = D2DHelper.BeginPaint(e, Me, dcRT, ssaa, _ssaaCache)
    '''       ' 1) 在 scope.GraphicsRenderTarget 上画图形（SSAA 启用时自动 SetTransform(scale)）
    '''       scope.FlushGraphics()
    '''       ' 2) 在 scope.DCRenderTarget 上画文字（DirectWrite 子像素质量）
    '''   End Using
    '''
    ''' 说明：
    '''   - 传入 cache 为 Nothing 时每帧重建 BitmapRT；传入 <see cref="BitmapRTCache"/>
    '''     后会按 (Width, Height, ssaa) 命中复用，极大减少开销。
    '''   - 控件应在 Dispose 时释放 cache。
    '''   - 请勿在 Using 体外引用 GraphicsRenderTarget / DCRenderTarget。
    ''' </summary>
    Public Class PaintScope
        Implements IDisposable

        Private ReadOnly _g As Graphics
        Private ReadOnly _hdc As IntPtr
        Private ReadOnly _w As Integer
        Private ReadOnly _h As Integer
        Private ReadOnly _ssaa As Integer
        Private ReadOnly _cache As BitmapRTCache
        Private _bitmapRT As ID2D1BitmapRenderTarget
        Private _bitmapRTOwned As Boolean

        ''' <summary>始终绑定到当前控件 HDC 的 RT，文字应当在它上面绘制。</summary>
        Public ReadOnly Property DCRenderTarget As ID2D1DCRenderTarget

        ''' <summary>当前用于绘制图形的 RT。SSAA 启用时为高分辨率 BitmapRT，否则等同 DCRenderTarget。</summary>
        Public Property GraphicsRenderTarget As ID2D1RenderTarget

        ''' <summary>SSAA 倍率（1 表示禁用）。</summary>
        Public ReadOnly Property SsaaScale As Integer
            Get
                Return _ssaa
            End Get
        End Property

        Friend Sub New(g As Graphics, hdc As IntPtr, dcRT As ID2D1DCRenderTarget, w As Integer, h As Integer, ssaa As Integer, cache As BitmapRTCache)
            _g = g
            _hdc = hdc
            _w = w
            _h = h
            _ssaa = Math.Max(1, ssaa)
            _cache = cache
            DCRenderTarget = dcRT

            dcRT.BindDC(hdc, New Vortice.RawRect(0, 0, w, h))
            dcRT.BeginDraw()
            ApplyGlobalQuality(dcRT)
            dcRT.Transform = Matrix3x2.Identity

            If _ssaa > 1 Then
                If _cache IsNot Nothing AndAlso _cache.Rt IsNot Nothing AndAlso _cache.W = w AndAlso _cache.H = h AndAlso _cache.Ssaa = _ssaa Then
                    _bitmapRT = _cache.Rt
                    _bitmapRTOwned = False
                Else
                    If _cache IsNot Nothing Then _cache.Invalidate()
                    Dim desired As New Vortice.Mathematics.SizeI(w * _ssaa, h * _ssaa)
                    _bitmapRT = dcRT.CreateCompatibleRenderTarget(desired)
                    ApplyGlobalQuality(_bitmapRT)
                    If _cache IsNot Nothing Then
                        _cache.Rt = _bitmapRT
                        _cache.W = w : _cache.H = h : _cache.Ssaa = _ssaa
                        _bitmapRTOwned = False
                    Else
                        _bitmapRTOwned = True
                    End If
                End If
                _bitmapRT.BeginDraw()
                _bitmapRT.Clear(New Vortice.Mathematics.Color4(0F, 0F, 0F, 0F))
                _bitmapRT.Transform = Matrix3x2.CreateScale(_ssaa)
                GraphicsRenderTarget = _bitmapRT
            Else
                GraphicsRenderTarget = dcRT
            End If
        End Sub

        ''' <summary>把 BitmapRT 的图形结果回采到 DCRenderTarget。SSAA 关闭时为 no-op。</summary>
        Public Sub FlushGraphics()
            If _bitmapRT Is Nothing Then Return
            Try
                _bitmapRT.Transform = Matrix3x2.Identity
                _bitmapRT.EndDraw()
                ' 注意：_bitmapRT.Bitmap 是 BitmapRT 内部拥有的资源，调用方不得 Dispose，
                ' 否则下一帧从缓存复用 BitmapRT 时会触发 ExecutionEngineException。
                Dim bmp = _bitmapRT.Bitmap
                DCRenderTarget.DrawBitmap(
                    bmp,
                    New Vortice.Mathematics.Rect(0, 0, _w, _h),
                    1.0F,
                    BitmapInterpolationMode.Linear,
                    New Vortice.Mathematics.Rect(0, 0, _w * _ssaa, _h * _ssaa))
            Finally
                If _bitmapRTOwned Then
                    Try : _bitmapRT.Dispose() : Catch : End Try
                End If
                _bitmapRT = Nothing
                GraphicsRenderTarget = DCRenderTarget
            End Try
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Try
                FlushGraphics()
            Catch
            End Try
            Try
                DCRenderTarget.EndDraw()
            Catch
            End Try
            Try
                _g.ReleaseHdc(_hdc)
            Catch
            End Try
        End Sub
    End Class

    ''' <summary>开始一个 D2D 绘制作用域（自动管理 HDC / BeginDraw / SSAA）。无 BitmapRT 缓存，每帧重建 SSAA 离屏目标。</summary>
    Public Function BeginPaint(e As PaintEventArgs, control As Control, dcRT As ID2D1DCRenderTarget, ssaaScale As Integer) As PaintScope
        Return BeginPaint(e, control, dcRT, ssaaScale, Nothing)
    End Function

    ''' <summary>
    ''' 开始一个 D2D 绘制作用域。带 <see cref="BitmapRTCache"/> 的重载，可在控件多帧之间复用 SSAA 离屏 BitmapRT。
    ''' 强烈建议任何启用 SSAA 的控件都走这个重载。
    ''' </summary>
    Public Function BeginPaint(e As PaintEventArgs, control As Control, dcRT As ID2D1DCRenderTarget, ssaaScale As Integer, cache As BitmapRTCache) As PaintScope
        Dim hdc As IntPtr = e.Graphics.GetHdc()
        Try
            Return New PaintScope(e.Graphics, hdc, dcRT, control.Width, control.Height, ssaaScale, cache)
        Catch
            Try : e.Graphics.ReleaseHdc(hdc) : Catch : End Try
            Throw
        End Try
    End Function

#End Region

#Region "缓存：BitmapRT / Bitmap"

    ''' <summary>
    ''' 跨帧复用 SSAA 离屏 <see cref="ID2D1BitmapRenderTarget"/>。一个控件应当持有一个实例，
    ''' 在 OnPaint 中传给 <see cref="BeginPaint"/>，<see cref="PaintScope"/> 会自动按 (Width, Height, ssaa)
    ''' 命中缓存或重建。控件 Dispose 时调用 <see cref="Dispose"/>。
    ''' </summary>
    Public Class BitmapRTCache
        Implements IDisposable

        Friend Rt As ID2D1BitmapRenderTarget
        Friend W As Integer
        Friend H As Integer
        Friend Ssaa As Integer

        ''' <summary>主动失效（例如父 DC RT 重建后）。</summary>
        Public Sub Invalidate()
            If Rt IsNot Nothing Then
                Try : Rt.Dispose() : Catch : End Try
                Rt = Nothing
            End If
            W = 0 : H = 0 : Ssaa = 0
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Invalidate()
        End Sub
    End Class

    ''' <summary>
    ''' 缓存一个 GDI <see cref="Image"/> 上传得到的 <see cref="ID2D1Bitmap"/>，避免每帧重新上传。
    ''' 仅当源 Image 引用或目标 RT 发生变化时重新上传。控件持有，Dispose 时释放。
    ''' </summary>
    Public Class D2DBitmapCache
        Implements IDisposable

        Private _src As Image
        Private _rt As ID2D1RenderTarget
        Private _bmp As ID2D1Bitmap

        ''' <summary>获取或上传。<paramref name="src"/> 为 Nothing 时返回 Nothing 并清空缓存。</summary>
        Public Function GetBitmap(rt As ID2D1RenderTarget, src As Image) As ID2D1Bitmap
            If src Is Nothing OrElse rt Is Nothing Then
                Invalidate()
                Return Nothing
            End If
            If _bmp IsNot Nothing AndAlso _src Is src AndAlso _rt Is rt Then Return _bmp
            Invalidate()
            _bmp = CreateBitmapFromImage(rt, src)
            _src = src
            _rt = rt
            Return _bmp
        End Function

        Public Sub Invalidate()
            If _bmp IsNot Nothing Then
                Try : _bmp.Dispose() : Catch : End Try
                _bmp = Nothing
            End If
            _src = Nothing
            _rt = Nothing
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Invalidate()
        End Sub
    End Class

    ''' <summary>
    ''' 跨帧复用 <see cref="ID2D1SolidColorBrush"/>。按 (RT 引用, ARGB) 缓存，
    ''' 避免热路径每帧 <c>Using ... CreateSolidColorBrush ... End Using</c> 造成的 D2D 资源分配开销。
    '''
    ''' 用法（控件层）：
    '''   Dim brush = _brushCache.Get(rt, color)
    '''   rt.FillRectangle(rect, brush)
    ''' 控件 <see cref="IDisposable.Dispose"/> 时调用本类的 <see cref="Dispose"/>。
    '''
    ''' 注意：
    '''   - SolidColorBrush 必须在创建它的 RT 仍然有效时才可使用；当 RT 引用切换（例如 SSAA
    '''     BitmapRT 重建）时缓存自动失效。
    '''   - 不处理 RT 的 RecreateTarget 错误（与本仓库其它 D2D 缓存保持一致）。
    ''' </summary>
    Public Class SolidColorBrushCache
        Implements IDisposable

        Private _rt As ID2D1RenderTarget
        Private ReadOnly _map As New Dictionary(Of Integer, ID2D1SolidColorBrush)(8)

        ''' <summary>取得（或惰性创建）指定颜色的 brush。</summary>
        Public Function [Get](rt As ID2D1RenderTarget, c As Color) As ID2D1SolidColorBrush
            If rt Is Nothing Then Return Nothing
            If Not ReferenceEquals(rt, _rt) Then
                ' RT 切换：旧 brush 关联到旧 RT，必须丢弃。
                InvalidateInternal()
                _rt = rt
            End If
            Dim key As Integer = c.ToArgb()
            Dim b As ID2D1SolidColorBrush = Nothing
            If _map.TryGetValue(key, b) Then Return b
            b = rt.CreateSolidColorBrush(ToColor4(c))
            _map(key) = b
            Return b
        End Function

        ''' <summary>主动清空（例如父 RT 即将 Dispose）。</summary>
        Public Sub Invalidate()
            InvalidateInternal()
            _rt = Nothing
        End Sub

        Private Sub InvalidateInternal()
            For Each b In _map.Values
                Try : b.Dispose() : Catch : End Try
            Next
            _map.Clear()
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Invalidate()
        End Sub
    End Class

    ''' <summary>
    ''' 跨帧复用 <see cref="IDWriteTextFormat"/>。按 (family, weight, style, sizePx, textAlignment, paragraphAlignment) 缓存。
    ''' DirectWrite TextFormat 不绑定 RT，可全局共享；本类仍按控件实例持有以便 Dispose 时一次性释放。
    ''' </summary>
    Public Class TextFormatCache
        Implements IDisposable

        Private Structure Key
            Public Family As String
            Public Weight As Vortice.DirectWrite.FontWeight
            Public Style As Vortice.DirectWrite.FontStyle
            Public SizePx As Single
            Public TextAlign As Vortice.DirectWrite.TextAlignment
            Public ParaAlign As Vortice.DirectWrite.ParagraphAlignment
            Public Trim As Boolean
        End Structure

        Private ReadOnly _map As New Dictionary(Of Key, IDWriteTextFormat)(4)

        ''' <summary>
        ''' 取得（或创建）匹配的 TextFormat。<paramref name="trimChar"/> = True 表示 SetTrimming(Character)。
        ''' </summary>
        Public Function [Get](family As String, weight As Vortice.DirectWrite.FontWeight,
                              style As Vortice.DirectWrite.FontStyle, sizePx As Single,
                              textAlign As Vortice.DirectWrite.TextAlignment,
                              paraAlign As Vortice.DirectWrite.ParagraphAlignment,
                              trimChar As Boolean) As IDWriteTextFormat
            Dim k As New Key With {
                .Family = If(family, ""),
                .Weight = weight,
                .Style = style,
                .SizePx = sizePx,
                .TextAlign = textAlign,
                .ParaAlign = paraAlign,
                .Trim = trimChar
            }
            Dim fmt As IDWriteTextFormat = Nothing
            If _map.TryGetValue(k, fmt) Then Return fmt
            fmt = GetDWriteFactory().CreateTextFormat(k.Family, Nothing, weight, style,
                                                      Vortice.DirectWrite.FontStretch.Normal, sizePx)
            fmt.TextAlignment = textAlign
            fmt.ParagraphAlignment = paraAlign
            fmt.WordWrapping = WordWrapping.NoWrap
            If trimChar Then
                Try
                    fmt.SetTrimming(New Trimming With {.Granularity = TrimmingGranularity.Character}, Nothing)
                Catch
                End Try
            End If
            _map(k) = fmt
            Return fmt
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            For Each f In _map.Values
                Try : f.Dispose() : Catch : End Try
            Next
            _map.Clear()
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

