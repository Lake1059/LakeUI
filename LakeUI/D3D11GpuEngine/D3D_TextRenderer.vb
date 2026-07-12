Imports Vortice.Direct2D1
Imports Vortice.DirectWrite

Public Enum D3D_TextQualityMode
    Auto = 0
    ClearTypeCompatible = 1
    Outline = 2
    Grayscale = 3
End Enum

''' <summary>
''' D3D_TextRenderer 是新核心唯一的文字绘制入口。
''' 它集中管理 ClearType/MacType 兼容、Outline 高质量策略位、Grayscale 透明目标稳定路线，并允许 Auto 根据 target alpha 选择。
''' 当前基础实现先通过 DirectWrite/D2D 文本入口完成模式切换；后续如果需要真正几何描边或独立 text layer，必须扩展本类，不能让控件绕过核心自建文字管线。
''' 文本测量可复用 DirectWrite 服务，但绘制入口必须通过本类。
''' ClearType 在透明 composition target 上可能不稳定，因此 text target 策略必须显式。
''' </summary>
Public NotInheritable Class D3D_TextRenderer
    Implements IDisposable

    Private ReadOnly _manager As D3D_DeviceManager
    Private ReadOnly _formats As New Dictionary(Of D3D_TextFormatKey, D3D_TextFormatCacheEntry)()
    Private _clock As Long
    Private _disposed As Boolean

    Public Sub New(manager As D3D_DeviceManager)
        _manager = manager
    End Sub

    ''' <summary>
    ''' 设置当前 DeviceContext 的文字抗锯齿模式。透明 target 默认走 Grayscale，非透明 target 可走 ClearTypeCompatible。
    ''' </summary>
    Public Sub ConfigureDeviceContext(context As ID2D1DeviceContext, mode As D3D_TextQualityMode, targetHasAlpha As Boolean)
        If context Is Nothing Then Return

        Select Case ResolveMode(mode, targetHasAlpha)
            Case D3D_TextQualityMode.ClearTypeCompatible
                context.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Cleartype
                context.TextRenderingParams = Nothing
            Case D3D_TextQualityMode.Outline
                context.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Grayscale
                context.TextRenderingParams = Nothing
            Case Else
                context.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Grayscale
                context.TextRenderingParams = Nothing
        End Select
    End Sub

    Public Sub DrawText(context As D3D_PaintContext,
                        text As String,
                        font As Font,
                        color As System.Drawing.Color,
                        layoutRect As RectangleF,
                        Optional hAlign As TextAlignment = TextAlignment.Leading,
                        Optional vAlign As ParagraphAlignment = ParagraphAlignment.Near,
                        Optional wordWrap As Boolean = False)
        If Not CanDrawText(context, text, font, color, layoutRect) Then Return

        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        Dim format = GetTextFormat(font, context.DpiScale, hAlign, vAlign, wordWrap, trimChar:=False)
        If format Is Nothing OrElse brush Is Nothing Then Return
        context.DeviceContext.DrawText(text, format, D3D_PaintContext.ToRawRect(layoutRect), brush, DrawTextOptions.Clip)
    End Sub

    Public Sub DrawText(context As D3D_PaintContext,
                        text As String,
                        font As Font,
                        color As System.Drawing.Color,
                        layoutRect As RectangleF,
                        flags As TextFormatFlags)
        If Not CanDrawText(context, text, font, color, layoutRect) Then Return

        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        Dim format = GetTextFormat(
            font,
            context.DpiScale,
            MapTextAlignment(flags),
            MapParagraphAlignment(flags),
            ShouldWordWrap(flags),
            trimChar:=(flags And TextFormatFlags.EndEllipsis) = TextFormatFlags.EndEllipsis)
        If format Is Nothing OrElse brush Is Nothing Then Return

        context.DeviceContext.DrawText(text, format, D3D_PaintContext.ToRawRect(layoutRect), brush, DrawTextOptions.Clip)
    End Sub

    Public Sub Invalidate()
        For Each entry In _formats.Values
            Try : entry.Format.Dispose() : Catch : End Try
        Next
        _formats.Clear()
    End Sub

    Private Function GetTextFormat(font As Font,
                                   dpiScale As Single,
                                   hAlign As TextAlignment,
                                   vAlign As ParagraphAlignment,
                                   wordWrap As Boolean,
                                   trimChar As Boolean) As IDWriteTextFormat
        Dim generation = _manager.DeviceGeneration
        Dim sizePx = D3D_D2DInterop.GetDWriteFontSizePx(font, dpiScale)
        Dim resolved = D3D_D2DInterop.ResolveTextFont(font)
        Dim key As New D3D_TextFormatKey(generation, resolved.Family, resolved.Weight, resolved.Style,
                                         resolved.Stretch, sizePx, hAlign, vAlign, wordWrap, trimChar)

        Dim entry As D3D_TextFormatCacheEntry = Nothing
        If _formats.TryGetValue(key, entry) Then
            entry.LastUsed = NextClock()
            Return entry.Format
        End If

        Dim format = D3D_D2DInterop.CreateTextFormat(resolved, sizePx)
        format.TextAlignment = hAlign
        format.ParagraphAlignment = vAlign
        format.WordWrapping = If(wordWrap, WordWrapping.Wrap, WordWrapping.NoWrap)
        D3D_TextMeasureHelper.ApplyUniformLineSpacing(format, font, dpiScale)
        If trimChar Then
            Try
                format.SetTrimming(New Trimming With {.Granularity = TrimmingGranularity.Character}, Nothing)
            Catch
            End Try
        End If

        _formats(key) = New D3D_TextFormatCacheEntry(format, generation, NextClock())
        TrimFormatCache()
        Return format
    End Function

    Private Shared Function CanDrawText(context As D3D_PaintContext,
                                        text As String,
                                        font As Font,
                                        color As System.Drawing.Color,
                                        layoutRect As RectangleF) As Boolean
        If context Is Nothing OrElse context.DeviceContext Is Nothing Then Return False
        If String.IsNullOrEmpty(text) OrElse font Is Nothing Then Return False
        If color.A = 0 Then Return False
        If layoutRect.Width <= 0.0F OrElse layoutRect.Height <= 0.0F Then Return False
        Return True
    End Function

    Private Shared Function MapTextAlignment(flags As TextFormatFlags) As TextAlignment
        If (flags And TextFormatFlags.HorizontalCenter) = TextFormatFlags.HorizontalCenter Then Return TextAlignment.Center
        If (flags And TextFormatFlags.Right) = TextFormatFlags.Right Then Return TextAlignment.Trailing
        Return TextAlignment.Leading
    End Function

    Private Shared Function MapParagraphAlignment(flags As TextFormatFlags) As ParagraphAlignment
        If (flags And TextFormatFlags.VerticalCenter) = TextFormatFlags.VerticalCenter Then Return ParagraphAlignment.Center
        If (flags And TextFormatFlags.Bottom) = TextFormatFlags.Bottom Then Return ParagraphAlignment.Far
        Return ParagraphAlignment.Near
    End Function

    Private Shared Function ShouldWordWrap(flags As TextFormatFlags) As Boolean
        Return (flags And TextFormatFlags.WordBreak) = TextFormatFlags.WordBreak AndAlso
               (flags And TextFormatFlags.SingleLine) <> TextFormatFlags.SingleLine
    End Function

    Private Shared Function ResolveMode(mode As D3D_TextQualityMode, targetHasAlpha As Boolean) As D3D_TextQualityMode
        If mode <> D3D_TextQualityMode.Auto Then Return mode
        Return If(targetHasAlpha, D3D_TextQualityMode.Grayscale, D3D_TextQualityMode.ClearTypeCompatible)
    End Function

    Private Function NextClock() As Long
        _clock += 1
        Return _clock
    End Function

    Private Sub TrimFormatCache()
        Dim maxEntries As Integer = Math.Max(0, GlobalOptions.TextFormatCacheLimit)
        While _formats.Count > maxEntries
            Dim victim = _formats.OrderBy(Function(kv) kv.Value.LastUsed).First()
            _formats.Remove(victim.Key)
            Try : victim.Value.Format.Dispose() : Catch : End Try
        End While
    End Sub

    Private Structure D3D_TextFormatKey
        Implements IEquatable(Of D3D_TextFormatKey)

        Private ReadOnly _generation As Integer
        Private ReadOnly _family As String
        Private ReadOnly _weight As FontWeight
        Private ReadOnly _style As Vortice.DirectWrite.FontStyle
        Private ReadOnly _stretch As FontStretch
        Private ReadOnly _sizePx As Single
        Private ReadOnly _hAlign As TextAlignment
        Private ReadOnly _vAlign As ParagraphAlignment
        Private ReadOnly _wordWrap As Boolean
        Private ReadOnly _trimChar As Boolean

        Friend Sub New(generation As Integer, family As String, weight As FontWeight,
                       style As Vortice.DirectWrite.FontStyle, stretch As FontStretch, sizePx As Single,
                       hAlign As TextAlignment, vAlign As ParagraphAlignment, wordWrap As Boolean, trimChar As Boolean)
            _generation = generation
            _family = family
            _weight = weight
            _style = style
            _stretch = stretch
            _sizePx = sizePx
            _hAlign = hAlign
            _vAlign = vAlign
            _wordWrap = wordWrap
            _trimChar = trimChar
        End Sub

        Public Overloads Function Equals(other As D3D_TextFormatKey) As Boolean Implements IEquatable(Of D3D_TextFormatKey).Equals
            Return _generation = other._generation AndAlso
                   String.Equals(_family, other._family, StringComparison.Ordinal) AndAlso
                   _weight = other._weight AndAlso _style = other._style AndAlso _stretch = other._stretch AndAlso
                   _sizePx.Equals(other._sizePx) AndAlso _hAlign = other._hAlign AndAlso _vAlign = other._vAlign AndAlso
                   _wordWrap = other._wordWrap AndAlso _trimChar = other._trimChar
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            Return TypeOf obj Is D3D_TextFormatKey AndAlso Equals(DirectCast(obj, D3D_TextFormatKey))
        End Function

        Public Overrides Function GetHashCode() As Integer
            Dim hash = HashCode.Combine(_generation, StringComparer.Ordinal.GetHashCode(If(_family, "")), CInt(_weight), CInt(_style), CInt(_stretch), _sizePx)
            Return HashCode.Combine(hash, CInt(_hAlign), CInt(_vAlign), _wordWrap, _trimChar)
        End Function
    End Structure

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        Invalidate()
        GC.SuppressFinalize(Me)
    End Sub

    Private NotInheritable Class D3D_TextFormatCacheEntry
        Public Sub New(format As IDWriteTextFormat, generation As Integer, lastUsed As Long)
            Me.Format = format
            Me.Generation = generation
            Me.LastUsed = lastUsed
        End Sub

        Public ReadOnly Property Format As IDWriteTextFormat
        Public ReadOnly Property Generation As Integer
        Public Property LastUsed As Long
    End Class
End Class
