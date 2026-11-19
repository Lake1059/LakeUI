Partial Public Class SingleLineTextBoxRenderer
    Public Sub DrawGpu12(context As D3D12_PaintContext)
        If context Is Nothing Then Return
        Dim area As RectangleF = GetTextArea()
        If area.Width <= 0 OrElse area.Height <= 0 Then Return

        Using context.PushClip(area)
            Dim singleLineY As Integer = CInt(area.Y + (area.Height - LineHeight) \ 2)
            Dim isEmpty As Boolean = String.IsNullOrEmpty(_text)

            If isEmpty AndAlso Not String.IsNullOrEmpty(WaterText) Then
                Dim waterAlignOff As Integer = GetAlignOffsetX(WaterText, CInt(area.Width))
                context.DrawText(WaterText, _owner.Font, WaterTextForeColor,
                                 New RectangleF(area.X + waterAlignOff, area.Y, area.Width, area.Height),
                                 DpiScale(), Vortice.DirectWrite.TextAlignment.Leading,
                                 Vortice.DirectWrite.ParagraphAlignment.Center)
            End If

            If Not isEmpty Then
                If _hasSelection Then DrawSelection_DX12(context, singleLineY, CInt(area.X), CInt(area.Width))
                Dim alignOff As Integer = GetAlignOffsetX(_text, CInt(area.Width))
                context.DrawText(_text, _owner.Font, ForeColor,
                                 New RectangleF(area.X + alignOff - _scrollXOffset, area.Y, Short.MaxValue, area.Height),
                                 DpiScale(), Vortice.DirectWrite.TextAlignment.Leading,
                                 Vortice.DirectWrite.ParagraphAlignment.Center)
            End If

            If IsFocused() AndAlso _caretVisible AndAlso Editable Then
                DrawCaret_DX12(context, CInt(area.X), CInt(area.Y))
            End If
        End Using
    End Sub

    Private Sub DrawSelection_DX12(context As D3D12_PaintContext, lineY As Integer, textLeft As Integer, textWidth As Integer)
        Dim minC As Integer = Math.Min(_selAnchorCol, _caretCol)
        Dim maxC As Integer = Math.Max(_selAnchorCol, _caretCol)
        Dim alignOff As Integer = GetAlignOffsetX(_text, textWidth)
        Dim x1 As Integer = textLeft + alignOff + MeasurePrefixWidth(minC) - _scrollXOffset
        Dim x2 As Integer = textLeft + alignOff + MeasurePrefixWidth(maxC) - _scrollXOffset
        If x2 <= x1 Then Return
        context.FillRectangle(New RectangleF(x1, lineY, x2 - x1, LineHeight), SelectionColor)
    End Sub

    Private Sub DrawCaret_DX12(context As D3D12_PaintContext, textLeft As Integer, textTop As Integer)
        Dim area As RectangleF = GetTextArea()
        Dim alignOff As Integer = GetAlignOffsetX(_text, CInt(area.Width))
        Dim cx As Integer = textLeft + alignOff + MeasurePrefixWidth(_caretCol) - _scrollXOffset
        Dim lineY As Integer = CInt(textTop + (area.Height - LineHeight) \ 2)
        Dim caretH As Integer = LineHeight - 2
        Dim caretY As Integer = lineY + (LineHeight - caretH) \ 2
        context.FillRectangle(New RectangleF(cx, caretY, CInt(CaretWidth * DpiScale()), caretH), CaretColor)
    End Sub
End Class
