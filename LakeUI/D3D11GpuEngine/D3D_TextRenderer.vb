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
''' 后续迁移控件不要直接调用旧 D2DTextRenderer；文本测量可复用 DirectWrite 思路，但绘制入口必须通过本类。
''' ClearType 在透明 composition target 上可能不稳定，因此 text target 策略必须显式。
''' </summary>
Public NotInheritable Class D3D_TextRenderer
    Implements IDisposable

    Private ReadOnly _manager As D3D_DeviceManager
    Private ReadOnly _formats As New Dictionary(Of String, D3D_TextFormatCacheEntry)(StringComparer.Ordinal)
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
                        Optional hAlign As TextAlignment = TextAlignment.Leading)
        If String.IsNullOrEmpty(text) OrElse font Is Nothing Then Return

        ConfigureDeviceContext(context.DeviceContext, context.TextQuality, targetHasAlpha:=False)
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        Dim format = GetTextFormat(font, context.DpiScale, hAlign)
        context.DeviceContext.DrawText(text, format, D3D_PaintContext.ToRawRect(layoutRect), brush, DrawTextOptions.Clip)
    End Sub

    Public Sub Invalidate()
        For Each entry In _formats.Values
            Try : entry.Format.Dispose() : Catch : End Try
        Next
        _formats.Clear()
    End Sub

    Private Function GetTextFormat(font As Font, dpiScale As Single, hAlign As TextAlignment) As IDWriteTextFormat
        Dim generation = _manager.DeviceGeneration
        Dim sizePx = Math.Max(1.0F, CSng(font.SizeInPoints * 96.0F / 72.0F * Math.Max(0.01F, dpiScale)))
        Dim weight = If(font.Bold, Vortice.DirectWrite.FontWeight.Bold, Vortice.DirectWrite.FontWeight.Normal)
        Dim style = If(font.Italic, Vortice.DirectWrite.FontStyle.Italic, Vortice.DirectWrite.FontStyle.Normal)
        Dim family = If(font.FontFamily Is Nothing, "Segoe UI", font.FontFamily.Name)
        Dim key = generation.ToString(Globalization.CultureInfo.InvariantCulture) & ":" &
                  family & ":" &
                  CInt(weight).ToString(Globalization.CultureInfo.InvariantCulture) & ":" &
                  CInt(style).ToString(Globalization.CultureInfo.InvariantCulture) & ":" &
                  sizePx.ToString("R", Globalization.CultureInfo.InvariantCulture) & ":" &
                  CInt(hAlign).ToString(Globalization.CultureInfo.InvariantCulture)

        Dim entry As D3D_TextFormatCacheEntry = Nothing
        If _formats.TryGetValue(key, entry) Then
            entry.LastUsed = NextClock()
            Return entry.Format
        End If

        Dim format = _manager.DWriteFactory.CreateTextFormat(family, Nothing, weight, style, FontStretch.Normal, sizePx)
        format.TextAlignment = hAlign
        format.ParagraphAlignment = ParagraphAlignment.Near
        format.WordWrapping = WordWrapping.NoWrap

        _formats(key) = New D3D_TextFormatCacheEntry(format, generation, NextClock())
        TrimFormatCache()
        Return format
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
        Const MaxEntries As Integer = 256
        While _formats.Count > MaxEntries
            Dim victim = _formats.OrderBy(Function(kv) kv.Value.LastUsed).First()
            _formats.Remove(victim.Key)
            Try : victim.Value.Format.Dispose() : Catch : End Try
        End While
    End Sub

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
