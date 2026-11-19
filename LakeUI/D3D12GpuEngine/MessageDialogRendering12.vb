Imports System.Numerics
Imports Vortice.DirectWrite

Friend Module MessageDialogRendering12
    Friend Function DrawBackdrop(context As D3D12_PaintContext, bounds As RectangleF, controller As MessageDialogBackdropController) As Boolean
        If Not MessageDialogRendering.IsGlassEnabled() Then Return False
        If controller IsNot Nothing Then Return controller.Draw12(context, bounds)
        If MessageDialogOptions.BackdropMode <> PopupBackdropMode.Image OrElse MessageDialogOptions.BackdropImage Is Nothing Then Return False
        context.DrawBlurredBackdrop(MessageDialogOptions.BackdropImage, bounds, MessageDialogOptions.BackdropBlurRadius,
                                   MessageDialogOptions.BackdropBlurPasses, MessageDialogOptions.BackdropDownsampleFactor)
        context.FillRectangle(bounds, MessageDialogOptions.BackdropTintColor)
        context.DrawNoise(bounds, MessageDialogOptions.BackdropNoiseScale, MessageDialogOptions.BackdropNoiseOpacity)
        Return True
    End Function

    Friend Sub FillRoundedRectangle(context As D3D12_PaintContext, bounds As RectangleF, color As Color, radius As Single)
        context.FillRoundedRectangle(bounds, radius, color)
    End Sub

    Friend Sub DrawRoundedRectangle(context As D3D12_PaintContext, bounds As RectangleF, color As Color, width As Single, radius As Single)
        If width <= 0 OrElse color.A = 0 Then Return
        Dim half = width / 2
        bounds.Inflate(-half, -half)
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        context.StrokeRoundedRectangle(bounds, Math.Max(0, radius - half), color, width)
    End Sub

    Friend Sub DrawText(context As D3D12_PaintContext, text As String, font As Font, rect As RectangleF, color As Color,
                        flags As TextFormatFlags, dpiScale As Single)
        If font Is Nothing OrElse String.IsNullOrEmpty(text) OrElse rect.Width <= 0 OrElse rect.Height <= 0 OrElse color.A = 0 Then Return
        Dim horizontal = If(flags.HasFlag(TextFormatFlags.HorizontalCenter), TextAlignment.Center,
                            If(flags.HasFlag(TextFormatFlags.Right), TextAlignment.Trailing, TextAlignment.Leading))
        Dim vertical = If(flags.HasFlag(TextFormatFlags.VerticalCenter), ParagraphAlignment.Center,
                          If(flags.HasFlag(TextFormatFlags.Bottom), ParagraphAlignment.Far, ParagraphAlignment.Near))
        context.DrawText(text, font, color, rect, dpiScale, horizontal, vertical, flags.HasFlag(TextFormatFlags.WordBreak),
                         trim:=flags.HasFlag(TextFormatFlags.EndEllipsis))
    End Sub

    Friend Sub DrawImage(context As D3D12_PaintContext, image As Image, bounds As RectangleF)
        context.DrawImage(image, bounds)
    End Sub

    Friend Sub DrawCloseButton(context As D3D12_PaintContext, rect As RectangleF, hovered As Boolean, pressed As Boolean,
                               normalForeColor As Color, hoverForeColor As Color, hoverBackColor As Color, dpiScale As Single)
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        If pressed Then
            context.FillRectangle(rect, Color.FromArgb(180, hoverBackColor.R, hoverBackColor.G, hoverBackColor.B))
        ElseIf hovered Then
            context.FillRectangle(rect, hoverBackColor)
        End If
        Dim glyphColor = If(hovered OrElse pressed, hoverForeColor, normalForeColor)
        If glyphColor.A = 0 Then Return
        Dim cx = rect.X + rect.Width / 2
        Dim cy = rect.Y + rect.Height / 2
        Dim half = 5 * dpiScale
        Dim width = Math.Max(1, 1.2F * dpiScale)
        context.DrawLine(New Vector2(cx - half, cy - half), New Vector2(cx + half, cy + half), glyphColor, width)
        context.DrawLine(New Vector2(cx + half, cy - half), New Vector2(cx - half, cy + half), glyphColor, width)
    End Sub
End Module
