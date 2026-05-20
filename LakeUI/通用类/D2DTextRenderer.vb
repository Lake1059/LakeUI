Imports System.Numerics
Imports Vortice.Direct2D1
Imports Vortice.DirectWrite

''' <summary>
''' 控件层走 D2D + DirectWrite 绘制文字时的最小封装。把 WinForms 的 (Font, Color, Rectangle, TextFormatFlags)
''' 调用约定映射到 DirectWrite TextFormat / TextLayout，再交给当前 RT 的 DrawTextLayout。
'''
''' === 用法（V2 控件 OnPaint 内）===
'''   D2DTextRenderer.DrawText(
'''       scope.TextLayer,
'''       "你好世界",
'''       Me.Font, textRect, Me.ForeColor,
'''       TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis,
'''       DpiScale(),
'''       scope.Compositor.TextFormatCache,
'''       scope.Compositor.BrushCache)
'''
''' === 约束 ===
''' • 必须传入正确的 <paramref name="dpiScale"/>（控件 DpiScale），否则在 HighDPI 下与
'''   GDI TextRenderer 的实际像素尺寸不一致（D2D DC RT 默认按 96 DPI 映射）。
'''   规则同 ModernButton.vb：sizePx = Font.SizeInPoints * (96/72) * DpiScale。
''' • 文本应绘制在 PaintScopeV2.TextLayer（= DC RT），以利用 GDI HDC 的子像素抗锯齿。
''' • 不处理换行：默认 <see cref="WordWrapping.NoWrap"/>；需要换行的传 <see cref="TextFormatFlags.WordBreak"/>。
''' • TextLayout 是一次性资源（按字符串/字号变化），无法跨帧复用；TextFormat 由 cache 复用。
''' </summary>
Public Module D2DTextRenderer

    ''' <summary>
    ''' 在 <paramref name="rt"/> 上按 (Font, Rectangle, Color, Flags) 绘制单行（或带 WordBreak 时多行）文本。
    ''' </summary>
    Public Sub DrawText(rt As ID2D1RenderTarget, text As String, font As Font, rect As RectangleF, color As Color,
                       flags As TextFormatFlags, dpiScale As Single,
                       textFormatCache As D2DHelper.TextFormatCache,
                       brushCache As D2DHelper.SolidColorBrushCache)
        If rt Is Nothing OrElse String.IsNullOrEmpty(text) OrElse font Is Nothing Then Return
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return

        Dim fmt = AcquireTextFormat(font, dpiScale, flags, textFormatCache)
        If fmt Is Nothing Then Return
        Dim brush = If(brushCache IsNot Nothing,
                       DirectCast(brushCache.[Get](rt, color), ID2D1Brush),
                       DirectCast(rt.CreateSolidColorBrush(D2DHelper.ToColor4(color)), ID2D1Brush))
        Dim ownsBrush As Boolean = (brushCache Is Nothing)
        Try
            Dim layoutWidth As Single = Math.Max(1.0F, rect.Width)
            Dim layoutHeight As Single = Math.Max(1.0F, rect.Height)
            Using layout = D2DHelper.GetDWriteFactory().CreateTextLayout(text, fmt, layoutWidth, layoutHeight)
                If (flags And TextFormatFlags.SingleLine) = TextFormatFlags.SingleLine Then
                    layout.WordWrapping = WordWrapping.NoWrap
                End If
                rt.DrawTextLayout(New Vector2(rect.X, rect.Y), layout, brush, DrawTextOptions.Clip)
            End Using
        Finally
            If ownsBrush AndAlso brush IsNot Nothing Then
                Try : brush.Dispose() : Catch : End Try
            End If
        End Try
    End Sub

    Public Sub DrawText(rt As ID2D1RenderTarget, text As String, font As Font, rect As Rectangle, color As Color,
                       flags As TextFormatFlags, dpiScale As Single,
                       textFormatCache As D2DHelper.TextFormatCache,
                       brushCache As D2DHelper.SolidColorBrushCache)
        DrawText(rt, text, font, CType(rect, RectangleF), color, flags, dpiScale, textFormatCache, brushCache)
    End Sub

    ''' <summary>左上角对齐 + NoPadding 的便捷重载（用于已自行算好坐标的场景）。</summary>
    Public Sub DrawTextAt(rt As ID2D1RenderTarget, text As String, font As Font, x As Single, y As Single, color As Color,
                          dpiScale As Single,
                          textFormatCache As D2DHelper.TextFormatCache,
                          brushCache As D2DHelper.SolidColorBrushCache)
        If rt Is Nothing OrElse String.IsNullOrEmpty(text) OrElse font Is Nothing Then Return
        Dim flags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
        Dim fmt = AcquireTextFormat(font, dpiScale, flags, textFormatCache)
        If fmt Is Nothing Then Return
        Dim brush = If(brushCache IsNot Nothing,
                       DirectCast(brushCache.[Get](rt, color), ID2D1Brush),
                       DirectCast(rt.CreateSolidColorBrush(D2DHelper.ToColor4(color)), ID2D1Brush))
        Dim ownsBrush As Boolean = (brushCache Is Nothing)
        Try
            Using layout = D2DHelper.GetDWriteFactory().CreateTextLayout(text, fmt, 100000.0F, 100000.0F)
                layout.WordWrapping = WordWrapping.NoWrap
                rt.DrawTextLayout(New Vector2(x, y), layout, brush)
            End Using
        Finally
            If ownsBrush AndAlso brush IsNot Nothing Then
                Try : brush.Dispose() : Catch : End Try
            End If
        End Try
    End Sub

    ''' <summary>测量文本宽度（像素）。返回 ceil 的整数，与 TextRenderer.MeasureText 语义接近。</summary>
    Public Function MeasureWidth(text As String, font As Font, dpiScale As Single) As Integer
        If String.IsNullOrEmpty(text) OrElse font Is Nothing Then Return 0
        Dim sizePx As Single = font.SizeInPoints * (96.0F / 72.0F) * dpiScale
        Dim weight As FontWeight = If(font.Bold, FontWeight.Bold, FontWeight.Normal)
        Dim style As FontStyle = If(font.Italic, FontStyle.Italic, FontStyle.Normal)
        Dim dw = D2DHelper.GetDWriteFactory()
        Using fmt = dw.CreateTextFormat(font.FontFamily.Name, Nothing, weight, style, FontStretch.Normal, sizePx)
            fmt.WordWrapping = WordWrapping.NoWrap
            Using layout = dw.CreateTextLayout(text, fmt, 100000.0F, 100000.0F)
                Return CInt(Math.Ceiling(layout.Metrics.WidthIncludingTrailingWhitespace))
            End Using
        End Using
    End Function

    ''' <summary>测量行高（像素，含 ascent/descent）。常用于绘制前确定垂直布局。</summary>
    Public Function MeasureLineHeight(font As Font, dpiScale As Single) As Integer
        If font Is Nothing Then Return 0
        Dim sizePx As Single = font.SizeInPoints * (96.0F / 72.0F) * dpiScale
        Dim weight As FontWeight = If(font.Bold, FontWeight.Bold, FontWeight.Normal)
        Dim style As FontStyle = If(font.Italic, FontStyle.Italic, FontStyle.Normal)
        Dim dw = D2DHelper.GetDWriteFactory()
        Using fmt = dw.CreateTextFormat(font.FontFamily.Name, Nothing, weight, style, FontStretch.Normal, sizePx)
            fmt.WordWrapping = WordWrapping.NoWrap
            Using layout = dw.CreateTextLayout("Ag", fmt, 100000.0F, 100000.0F)
                Return CInt(Math.Ceiling(layout.Metrics.Height))
            End Using
        End Using
    End Function

    ''' <summary>测量 TextLayout 的完整 Metrics（宽 + 高）；调用方可用于精确布局。</summary>
    Public Function MeasureSize(text As String, font As Font, dpiScale As Single) As SizeF
        If String.IsNullOrEmpty(text) OrElse font Is Nothing Then Return SizeF.Empty
        Dim sizePx As Single = font.SizeInPoints * (96.0F / 72.0F) * dpiScale
        Dim weight As FontWeight = If(font.Bold, FontWeight.Bold, FontWeight.Normal)
        Dim style As FontStyle = If(font.Italic, FontStyle.Italic, FontStyle.Normal)
        Dim dw = D2DHelper.GetDWriteFactory()
        Using fmt = dw.CreateTextFormat(font.FontFamily.Name, Nothing, weight, style, FontStretch.Normal, sizePx)
            fmt.WordWrapping = WordWrapping.NoWrap
            Using layout = dw.CreateTextLayout(text, fmt, 100000.0F, 100000.0F)
                Dim m = layout.Metrics
                Return New SizeF(m.WidthIncludingTrailingWhitespace, m.Height)
            End Using
        End Using
    End Function

    Private Function AcquireTextFormat(font As Font, dpiScale As Single, flags As TextFormatFlags,
                                       cache As D2DHelper.TextFormatCache) As IDWriteTextFormat
        Dim sizePx As Single = font.SizeInPoints * (96.0F / 72.0F) * dpiScale
        Dim weight As FontWeight = If(font.Bold, FontWeight.Bold, FontWeight.Normal)
        Dim style As FontStyle = If(font.Italic, FontStyle.Italic, FontStyle.Normal)
        Dim textAlign As TextAlignment = MapTextAlignment(flags)
        Dim paraAlign As ParagraphAlignment = MapParagraphAlignment(flags)
        Dim trimChar As Boolean = (flags And TextFormatFlags.EndEllipsis) = TextFormatFlags.EndEllipsis
        If cache IsNot Nothing Then
            Return cache.[Get](font.FontFamily.Name, weight, style, sizePx, textAlign, paraAlign, trimChar)
        End If
        Dim fmt = D2DHelper.GetDWriteFactory().CreateTextFormat(
            font.FontFamily.Name, Nothing, weight, style, FontStretch.Normal, sizePx)
        fmt.TextAlignment = textAlign
        fmt.ParagraphAlignment = paraAlign
        fmt.WordWrapping = WordWrapping.NoWrap
        If trimChar Then
            Try : fmt.SetTrimming(New Trimming With {.Granularity = TrimmingGranularity.Character}, Nothing) : Catch : End Try
        End If
        Return fmt
    End Function

    Private Function MapTextAlignment(flags As TextFormatFlags) As TextAlignment
        ' WinForms 默认 Left；HorizontalCenter / Right 显式覆盖。
        If (flags And TextFormatFlags.HorizontalCenter) = TextFormatFlags.HorizontalCenter Then Return TextAlignment.Center
        If (flags And TextFormatFlags.Right) = TextFormatFlags.Right Then Return TextAlignment.Trailing
        Return TextAlignment.Leading
    End Function

    Private Function MapParagraphAlignment(flags As TextFormatFlags) As ParagraphAlignment
        ' WinForms 默认 Top；VerticalCenter / Bottom 显式覆盖。
        If (flags And TextFormatFlags.VerticalCenter) = TextFormatFlags.VerticalCenter Then Return ParagraphAlignment.Center
        If (flags And TextFormatFlags.Bottom) = TextFormatFlags.Bottom Then Return ParagraphAlignment.Far
        Return ParagraphAlignment.Near
    End Function

End Module
