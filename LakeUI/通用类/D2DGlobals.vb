Imports System.Drawing.Imaging
Imports System.Numerics
Imports Vortice.Direct2D1
Imports Vortice.DirectWrite
Imports Vortice.DXGI

''' <summary>
''' Direct2D / DirectWrite 全局共享层（V2 体系的"共享基座"）。
'''
''' V1 时代的 <c>D2DHelper</c> 模块原本同时承担：①全局工厂与质量策略 / 类型转换 / 资源缓存
''' （进程级，跨控件复用），②控件级 <c>PaintScope</c> 渲染管线（含 SSAA BitmapRT 缓存）。
''' V2 的窗口级合成器（<see cref="WindowCompositor"/> / <see cref="PaintScopeV2"/>）已完全取代后者，
''' 因此本文件只保留①里的"全局基座"，控件级 PaintScope 已从代码库移除。
'''
''' 这里保留模块名 <c>D2DHelper</c>，让所有 <c>D2DHelper.ToColor4 / ToD2DRect / ApplyGlobalQuality / ...</c>
''' 的调用沿用原路径无需大改。
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
    ''' V2 的 <see cref="PaintScopeV2"/> 已在内部对 DC RT 与 SSAA BitmapRT 自动调用，控件作者通常无需手动调用。
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

    Private Function GetOutlineRenderingParams() As IDWriteRenderingParams
        If _outlineRenderingParams Is Nothing Then
            SyncLock _factoryLock
                If _outlineRenderingParams Is Nothing Then
                    Dim dw = GetDWriteFactory()
                    Dim opt = OutlineText

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

    ''' <summary>直接从 GDI <see cref="Bitmap"/> 上传为 <see cref="ID2D1Bitmap"/>。调用方负责 Dispose。</summary>
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

#Region "缓存：Bitmap / Brush / TextFormat"

    ''' <summary>
    ''' 缓存一个 GDI <see cref="Image"/> 上传得到的 <see cref="ID2D1Bitmap"/>，避免每帧重新上传。
    ''' 仅当源 Image 引用或目标 RT 发生变化时重新上传。
    ''' </summary>
    Public Class D2DBitmapCache
        Implements IDisposable

        Private _src As Image
        Private _rt As ID2D1RenderTarget
        Private _bmp As ID2D1Bitmap

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

    Private ReadOnly _fontResolveCache As New Dictionary(Of FontResolveKey, ResolvedTextFont)(16)
    Private ReadOnly _fontResolveLock As New Object()

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
            Dim cached As ResolvedTextFont = Nothing
            If _fontResolveCache.TryGetValue(key, cached) Then Return cached
        End SyncLock

        Dim resolved = ResolveTextFontNameUncached(fallback)

        SyncLock _fontResolveLock
            _fontResolveCache(key) = resolved
        End SyncLock

        Return resolved
    End Function

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

        Private ReadOnly _buckets As New Dictionary(Of ID2D1RenderTarget, Dictionary(Of Integer, ID2D1SolidColorBrush))(4)

        Public Function [Get](rt As ID2D1RenderTarget, c As Color) As ID2D1SolidColorBrush
            If rt Is Nothing Then Return Nothing
            Dim bucket As Dictionary(Of Integer, ID2D1SolidColorBrush) = Nothing
            If Not _buckets.TryGetValue(rt, bucket) Then
                bucket = New Dictionary(Of Integer, ID2D1SolidColorBrush)(8)
                _buckets(rt) = bucket
            End If
            Dim key As Integer = c.ToArgb()
            Dim b As ID2D1SolidColorBrush = Nothing
            If bucket.TryGetValue(key, b) Then Return b
            b = rt.CreateSolidColorBrush(ToColor4(c))
            bucket(key) = b
            Return b
        End Function

        Public Sub Invalidate()
            InvalidateInternal()
        End Sub

        Public Sub InvalidateFor(rt As ID2D1RenderTarget)
            If rt Is Nothing Then Return
            Dim bucket As Dictionary(Of Integer, ID2D1SolidColorBrush) = Nothing
            If _buckets.TryGetValue(rt, bucket) Then
                For Each b In bucket.Values
                    Try : b.Dispose() : Catch : End Try
                Next
                _buckets.Remove(rt)
            End If
        End Sub

        Private Sub InvalidateInternal()
            For Each bucket In _buckets.Values
                For Each b In bucket.Values
                    Try : b.Dispose() : Catch : End Try
                Next
            Next
            _buckets.Clear()
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Invalidate()
        End Sub
    End Class

    ''' <summary>
    ''' 跨帧复用 <see cref="IDWriteTextFormat"/>。按 (family, weight, style, sizePx, textAlignment, paragraphAlignment) 缓存。
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
        End Structure

        Private ReadOnly _map As New Dictionary(Of Key, IDWriteTextFormat)(4)

        Public Function [Get](family As String, weight As Vortice.DirectWrite.FontWeight,
                              style As Vortice.DirectWrite.FontStyle, sizePx As Single,
                              textAlign As Vortice.DirectWrite.TextAlignment,
                              paraAlign As Vortice.DirectWrite.ParagraphAlignment,
                              trimChar As Boolean) As IDWriteTextFormat
            Return GetResolved(ResolveTextFont(family, weight, style, Vortice.DirectWrite.FontStretch.Normal),
                               sizePx, textAlign, paraAlign, trimChar)
        End Function

        Public Function [Get](font As Font, sizePx As Single,
                              textAlign As Vortice.DirectWrite.TextAlignment,
                              paraAlign As Vortice.DirectWrite.ParagraphAlignment,
                              trimChar As Boolean) As IDWriteTextFormat
            Return GetResolved(ResolveTextFont(font), sizePx, textAlign, paraAlign, trimChar)
        End Function

        Private Function GetResolved(resolved As ResolvedTextFont, sizePx As Single,
                                    textAlign As Vortice.DirectWrite.TextAlignment,
                                    paraAlign As Vortice.DirectWrite.ParagraphAlignment,
                                    trimChar As Boolean) As IDWriteTextFormat
            Dim k As New Key With {
                .Family = If(resolved.Family, ""),
                .Weight = resolved.Weight,
                .Style = resolved.Style,
                .Stretch = resolved.Stretch,
                .SizePx = sizePx,
                .TextAlign = textAlign,
                .ParaAlign = paraAlign,
                .Trim = trimChar
            }
            Dim fmt As IDWriteTextFormat = Nothing
            If _map.TryGetValue(k, fmt) Then Return fmt
            fmt = CreateTextFormat(resolved, sizePx)
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

        Public Sub Invalidate()
            For Each f In _map.Values
                Try : f.Dispose() : Catch : End Try
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
