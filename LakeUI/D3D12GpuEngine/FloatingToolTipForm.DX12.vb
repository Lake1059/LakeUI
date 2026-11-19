Imports Vortice.DirectWrite

Partial Public NotInheritable Class FloatingToolTipForm
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then Return

        Dim w As Integer = ClientSize.Width
        Dim h As Integer = ClientSize.Height
        Dim bw As Integer = BorderWidth()
        Dim bounds As New RectangleF(0, 0, w, h)
        Dim hasBackdrop As Boolean = _backdrop IsNot Nothing AndAlso _backdrop.Draw12(context, bounds)

        DrawBackground_DX12(context, bw, w, h, Not hasBackdrop)

        Dim textRect As RectangleF = GetTextRectangle()
        DrawSelection_DX12(context, textRect)
        context.DrawText(_tipText, TipFont(), _style.ForeColor, textRect,
                         OwnerDpiScale(), wordWrap:=True)
    End Sub

    Private Sub DrawSelection_DX12(context As D3D12_PaintContext, textRect As RectangleF)
        If Not IsSelectableCopyEnabled() Then Return
        Dim length As Integer = SelectionLength()
        If context Is Nothing OrElse length <= 0 OrElse textRect.Width <= 0 OrElse textRect.Height <= 0 Then Return

        Dim start As Integer = SelectionStart()
        Using fmt = CreateTipTextFormat()
            If fmt Is Nothing Then Return
            Using layout = D3D_D2DInterop.GetDWriteFactory().CreateTextLayout(_tipText, fmt,
                                                                          Math.Max(1.0F, textRect.Width),
                                                                          Math.Max(1.0F, textRect.Height))
                Dim metrics(Math.Max(1, length + 1) - 1) As HitTestMetrics
                Dim actual As UInteger = 0
                Try
                    layout.HitTestTextRange(CUInt(start), CUInt(length), textRect.X, textRect.Y, metrics, actual)
                Catch
                    Return
                End Try
                If actual <= 0 Then Return

                Dim count As Integer = Math.Min(metrics.Length, CInt(actual))
                For i As Integer = 0 To count - 1
                    Dim m = metrics(i)
                    If m.Width <= 0.0F OrElse m.Height <= 0.0F Then Continue For
                    context.FillRectangle(New RectangleF(m.Left, m.Top, m.Width, m.Height), EffectiveSelectionFocusColor())
                Next
            End Using
        End Using
    End Sub

    Private Sub DrawBackground_DX12(context As D3D12_PaintContext, bw As Integer, w As Integer, h As Integer, fillBackground As Boolean)
        Dim radius As Single = Math.Min(BorderRadius(), Math.Min(w, h) / 2.0F)
        Dim fillColor As Color = ToolTipFillColor()

        If radius > 0 Then
            Dim outerRect As New RectangleF(0, 0, Math.Max(1, w), Math.Max(1, h))
            Dim half As Single = bw / 2.0F
            Dim innerRect As RectangleF = outerRect
            If bw > 0 Then innerRect.Inflate(-half, -half)
            Dim outerRadius As Single = Math.Min(radius, Math.Min(outerRect.Width, outerRect.Height) / 2.0F)
            Dim innerRadius As Single = Math.Max(0.0F, Math.Min(outerRadius - half, Math.Min(innerRect.Width, innerRect.Height) / 2.0F))
            If fillBackground Then context.FillRoundedRectangle(innerRect, innerRadius, fillColor)
            If bw > 0 AndAlso _style.BorderColor.A > 0 Then context.StrokeRoundedRectangle(outerRect, outerRadius, _style.BorderColor, bw)
            Return
        End If

        If fillBackground Then context.FillRectangle(New RectangleF(0, 0, w, h), fillColor)
        If bw > 0 AndAlso _style.BorderColor.A > 0 Then DrawSquareBorder_DX12(context, w, h, bw)
    End Sub

    Private Sub DrawSquareBorder_DX12(context As D3D12_PaintContext, w As Integer, h As Integer, bw As Integer)
        Dim border As Integer = Math.Min(bw, Math.Min(w, h))
        If border <= 0 Then Return

        context.FillRectangle(New RectangleF(0, 0, w, border), _style.BorderColor)
        If h > border Then context.FillRectangle(New RectangleF(0, h - border, w, border), _style.BorderColor)

        Dim middleHeight As Integer = h - border * 2
        If middleHeight > 0 Then
            context.FillRectangle(New RectangleF(0, border, border, middleHeight), _style.BorderColor)
            If w > border Then context.FillRectangle(New RectangleF(w - border, border, border, middleHeight), _style.BorderColor)
        End If
    End Sub
End Class
