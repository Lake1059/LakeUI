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
''' • 默认 <see cref="WordWrapping.NoWrap"/>；需要换行的传 <see cref="TextFormatFlags.WordBreak"/>。
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

        Dim ownsFormat As Boolean = False
        Dim fmt = AcquireTextFormat(font, dpiScale, flags, textFormatCache, ownsFormat)
        If fmt Is Nothing Then Return
        Dim brush = If(brushCache IsNot Nothing,
                       DirectCast(brushCache.[Get](rt, color), ID2D1Brush),
                       DirectCast(rt.CreateSolidColorBrush(D2DHelper.ToColor4(color)), ID2D1Brush))
        Dim ownsBrush As Boolean = (brushCache Is Nothing)
        Try
            rt.DrawText(text, fmt, D2DHelper.ToD2DRect(rect), brush, DrawTextOptions.Clip)
        Finally
            If ownsBrush AndAlso brush IsNot Nothing Then
                Try : brush.Dispose() : Catch : End Try
            End If
            If ownsFormat AndAlso fmt IsNot Nothing Then
                Try : fmt.Dispose() : Catch : End Try
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
        Dim ownsFormat As Boolean = False
        Dim fmt = AcquireTextFormat(font, dpiScale, flags, textFormatCache, ownsFormat)
        If fmt Is Nothing Then Return
        Dim brush = If(brushCache IsNot Nothing,
                       DirectCast(brushCache.[Get](rt, color), ID2D1Brush),
                       DirectCast(rt.CreateSolidColorBrush(D2DHelper.ToColor4(color)), ID2D1Brush))
        Dim ownsBrush As Boolean = (brushCache Is Nothing)
        Try
            rt.DrawText(text, fmt, New Vortice.Mathematics.Rect(x, y, 100000.0F, 100000.0F), brush)
        Finally
            If ownsBrush AndAlso brush IsNot Nothing Then
                Try : brush.Dispose() : Catch : End Try
            End If
            If ownsFormat AndAlso fmt IsNot Nothing Then
                Try : fmt.Dispose() : Catch : End Try
            End If
        End Try
    End Sub

    ''' <summary>按与 DrawText 相同的 DirectWrite 格式测量文本布局尺寸。</summary>
    Public Function MeasureText(text As String, font As Font, proposedSize As Size, flags As TextFormatFlags,
                                dpiScale As Single,
                                Optional textFormatCache As D2DHelper.TextFormatCache = Nothing) As Size
        If String.IsNullOrEmpty(text) OrElse font Is Nothing Then Return Size.Empty
        Dim ownsFormat As Boolean = False
        Dim fmt = AcquireTextFormat(font, dpiScale, flags, textFormatCache, ownsFormat)
        If fmt Is Nothing Then Return Size.Empty
        Try
            Dim layoutW As Single = NormalizeLayoutExtent(proposedSize.Width)
            Dim layoutH As Single = NormalizeLayoutExtent(proposedSize.Height)
            Using layout = D2DHelper.GetDWriteFactory().CreateTextLayout(text, fmt, layoutW, layoutH)
                Dim m = layout.Metrics
                Return New Size(
                    CInt(Math.Ceiling(Math.Max(0.0F, m.WidthIncludingTrailingWhitespace))),
                    CInt(Math.Ceiling(Math.Max(0.0F, m.Height))))
            End Using
        Finally
            If ownsFormat AndAlso fmt IsNot Nothing Then
                Try : fmt.Dispose() : Catch : End Try
            End If
        End Try
    End Function

    ''' <summary>测量文本宽度（像素）。返回 ceil 的整数，与 TextRenderer.MeasureText 语义接近。</summary>
    Public Function MeasureWidth(text As String, font As Font, dpiScale As Single) As Integer
        If String.IsNullOrEmpty(text) OrElse font Is Nothing Then Return 0
        Dim flags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
        Return MeasureText(text, font, New Size(Integer.MaxValue, Integer.MaxValue), flags, dpiScale).Width
    End Function

    ''' <summary>测量行高（像素，含 ascent/descent）。常用于绘制前确定垂直布局。</summary>
    Public Function MeasureLineHeight(font As Font, dpiScale As Single) As Integer
        If font Is Nothing Then Return 0
        Dim flags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
        Return MeasureText("Ag", font, New Size(Integer.MaxValue, Integer.MaxValue), flags, dpiScale).Height
    End Function

    ''' <summary>测量 TextLayout 的完整 Metrics（宽 + 高）；调用方可用于精确布局。</summary>
    Public Function MeasureSize(text As String, font As Font, dpiScale As Single) As SizeF
        If String.IsNullOrEmpty(text) OrElse font Is Nothing Then Return SizeF.Empty
        Dim flags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
        Dim sz = MeasureText(text, font, New Size(Integer.MaxValue, Integer.MaxValue), flags, dpiScale)
        Return New SizeF(sz.Width, sz.Height)
    End Function

    Private Function AcquireTextFormat(font As Font, dpiScale As Single, flags As TextFormatFlags,
                                       cache As D2DHelper.TextFormatCache,
                                       ByRef ownsFormat As Boolean) As IDWriteTextFormat
        Dim sizePx As Single = font.SizeInPoints * (96.0F / 72.0F) * dpiScale
        Dim textAlign As TextAlignment = MapTextAlignment(flags)
        Dim paraAlign As ParagraphAlignment = MapParagraphAlignment(flags)
        Dim trimChar As Boolean = (flags And TextFormatFlags.EndEllipsis) = TextFormatFlags.EndEllipsis
        Dim wordWrap As Boolean = (flags And TextFormatFlags.WordBreak) = TextFormatFlags.WordBreak AndAlso
                                  (flags And TextFormatFlags.SingleLine) <> TextFormatFlags.SingleLine
        ownsFormat = (cache Is Nothing)
        If cache IsNot Nothing Then
            Return cache.[Get](font, sizePx, textAlign, paraAlign, trimChar, wordWrap)
        End If
        Dim fmt = D2DHelper.CreateTextFormat(font, sizePx)
        fmt.TextAlignment = textAlign
        fmt.ParagraphAlignment = paraAlign
        fmt.WordWrapping = If(wordWrap, WordWrapping.Wrap, WordWrapping.NoWrap)
        If trimChar Then
            Try : fmt.SetTrimming(New Trimming With {.Granularity = TrimmingGranularity.Character}, Nothing) : Catch : End Try
        End If
        Return fmt
    End Function

    Private Function NormalizeLayoutExtent(value As Integer) As Single
        If value <= 0 Then Return 1.0F
        If value = Integer.MaxValue Then Return Single.MaxValue
        Return CSng(value)
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
