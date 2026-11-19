Imports System.Numerics

Partial Friend Class PonderOverlayForm
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then Return
        Dim p = If(_animation Is Nothing, 1.0F, _animation.Progress)
        context.FillRectangle(New RectangleF(0, 0, ClientSize.Width, ClientSize.Height), Color.FromArgb(CInt(_owner.OverlayOpacity * p), _owner.OverlayColor))
        For Each retained In _visibleHighlights
            If retained.Width > 0 AndAlso retained.Height > 0 Then
                context.FillRectangle(retained, Color.FromArgb(Math.Max(1, CInt(35 * p)), Color.White))
                context.DrawRectangle(retained, _owner.HighlightBorderColor, _owner.HighlightBorderWidth)
            End If
        Next
        If _highlight.Width > 0 AndAlso _highlight.Height > 0 Then
            context.FillRectangle(_highlight, Color.FromArgb(Math.Max(1, CInt(45 * p)), Color.White))
            context.DrawRectangle(_highlight, _owner.HighlightBorderColor, _owner.HighlightBorderWidth)
        End If
        If _item IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(_item.Text) AndAlso _textRect.Width > 0 Then
            context.FillRectangle(_textRect, _owner.TextBackColor)
            context.DrawRectangle(_textRect, _owner.TextBorderColor, _owner.TextBorderWidth)
            Dim inner = New RectangleF(_textRect.X + _owner.TextPadding.Left, _textRect.Y + _owner.TextPadding.Top,
                                       Math.Max(1, _textRect.Width - _owner.TextPadding.Horizontal), Math.Max(1, _textRect.Height - _owner.TextPadding.Vertical))
            context.DrawText(_item.Text, _font, Color.FromArgb(CInt(255 * p), _owner.TextColor), inner, DeviceDpi / 96.0F, Vortice.DirectWrite.TextAlignment.Leading, Vortice.DirectWrite.ParagraphAlignment.Near, True)
            If _item.ConnectorEnabled Then DrawConnector(context, p)
        End If
        DrawButtons(context)
    End Sub

    Private Sub DrawConnector(context As D3D12_PaintContext, p As Single)
        If _highlight.Width <= 0 OrElse _textRect.Width <= 0 Then Return
        Dim start As New Vector2(_highlight.X + _highlight.Width / 2, _highlight.Y + _highlight.Height / 2)
        Dim finish As New Vector2(_textRect.X + _textRect.Width / 2, _textRect.Y + _textRect.Height / 2)
        Dim pen = Color.FromArgb(CInt(255 * p), _owner.ConnectorColor)
        If _owner.ConnectorStyle = Ponder.ConnectorStyleEnum.Straight Then
            context.DrawLine(start, finish, pen, _owner.ConnectorWidth)
        Else
            Dim midX = (start.X + finish.X) / 2.0F
            context.DrawLine(start, New Vector2(midX, start.Y), pen, _owner.ConnectorWidth)
            context.DrawLine(New Vector2(midX, start.Y), New Vector2(midX, finish.Y), pen, _owner.ConnectorWidth)
            context.DrawLine(New Vector2(midX, finish.Y), finish, pen, _owner.ConnectorWidth)
        End If
    End Sub

    Private Sub DrawButtons(context As D3D12_PaintContext)
        For Each pair In _buttons
            Dim rect = pair.Value
            Dim isClose = pair.Key = "close"
            Dim bg As Color = If(isClose AndAlso pair.Key = _hoverButton, _owner.CloseButtonHoverBackColor, If(isClose, _owner.CloseButtonBackColor, Color.FromArgb(170, 32, 32, 32)))
            If bg.A > 0 Then context.FillRoundedRectangle(rect, 4, bg)
            Dim glyphColor As Color = If(isClose, _owner.CloseButtonGlyphColor, Color.White)
            Dim pen = glyphColor
            Dim cx = rect.X + rect.Width / 2, cy = rect.Y + rect.Height / 2
            Dim s = Math.Min(rect.Width, rect.Height) * 0.32F
            Select Case pair.Key
                Case "close"
                    context.DrawLine(New Vector2(cx - s, cy - s), New Vector2(cx + s, cy + s), pen, _owner.CloseButtonGlyphWidth)
                    context.DrawLine(New Vector2(cx + s, cy - s), New Vector2(cx - s, cy + s), pen, _owner.CloseButtonGlyphWidth)
                Case "prev"
                    context.DrawLine(New Vector2(cx + s, cy - s), New Vector2(cx - s, cy), pen, 2)
                    context.DrawLine(New Vector2(cx - s, cy), New Vector2(cx + s, cy + s), pen, 2)
                Case "next"
                    context.DrawLine(New Vector2(cx - s, cy - s), New Vector2(cx + s, cy), pen, 2)
                    context.DrawLine(New Vector2(cx + s, cy), New Vector2(cx - s, cy + s), pen, 2)
                Case "auto"
                    context.DrawText(If(_autoPlay, "||", ">"), _font, glyphColor, rect, DeviceDpi / 96.0F, Vortice.DirectWrite.TextAlignment.Center, Vortice.DirectWrite.ParagraphAlignment.Center, False)
                Case "restart"
                    context.DrawText("R", _font, glyphColor, rect, DeviceDpi / 96.0F, Vortice.DirectWrite.TextAlignment.Center, Vortice.DirectWrite.ParagraphAlignment.Center, False)
            End Select
        Next
    End Sub
End Class
