Imports Vortice.DirectWrite
Imports System.Numerics

Partial Public Class EasyStatesPanel
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse Width <= 0 OrElse Height <= 0 Then Return
        EnsureLayout()

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Width, Height), MyBase.BackColor)
        End If
        DrawBackgroundAndFrame_DX12(context)
        DrawCards_DX12(context)
        If _showVScroll Then DrawScrollBar_DX12(context)
        DrawCardTexts_DX12(context)
        If Not Enabled AndAlso _disabledOverlayColor.A > 0 Then DrawDisabledOverlay_DX12(context)
    End Sub

    Private Sub DrawBackgroundAndFrame_DX12(context As D3D12_PaintContext)
        Dim s As Single = DpiScale()
        Dim rect As New RectangleF(0, 0, Width, Height)
        If _borderSize > 0 Then
            Dim half As Single = _borderSize * s / 2.0F
            rect.Inflate(-half, -half)
        End If
        Dim radius As Single = _borderRadius * s
        If _backColor1.A > 0 Then FillRoundedRect_DX12(context, rect, radius, _backColor1)
        If _borderSize > 0 AndAlso _borderColor.A > 0 Then DrawRoundedBorder_DX12(context, rect, radius, _borderColor, _borderSize * s)
    End Sub

    Private Sub DrawCards_DX12(context As D3D12_PaintContext)
        If _cardsViewportRect.Width <= 0 OrElse _cardsViewportRect.Height <= 0 Then Return
        Using context.PushClip(_cardsViewportRect)
            Dim s As Single = DpiScale()
            For Each l In _layoutCache
                Dim drawRect As New RectangleF(
                    _cardsViewportRect.X + l.Bounds.X,
                    _cardsViewportRect.Y + l.Bounds.Y - _scrollOffset,
                    l.Bounds.Width,
                    l.Bounds.Height)
                If drawRect.Bottom < _cardsViewportRect.Top OrElse drawRect.Top > _cardsViewportRect.Bottom Then Continue For

                If _cardBorderSize > 0 Then
                    Dim half As Single = _cardBorderSize * s / 2.0F
                    drawRect.Inflate(-half, -half)
                End If

                Dim radius As Single = _cardRadius * s
                If _cardBackColor.A > 0 Then FillRoundedRect_DX12(context, drawRect, radius, _cardBackColor)
                If _cardBorderSize > 0 AndAlso _cardBorderColor.A > 0 Then DrawRoundedBorder_DX12(context, drawRect, radius, _cardBorderColor, _cardBorderSize * s)
            Next
        End Using
    End Sub

    Private Sub DrawCardTexts_DX12(context As D3D12_PaintContext)
        If _cardsViewportRect.Width <= 0 OrElse _cardsViewportRect.Height <= 0 Then Return
        Using context.PushClip(_cardsViewportRect)
            Dim s As Single = DpiScale()
            Dim padX As Single = _cardHorizontalPadding * s
            Dim padY As Single = _cardVerticalPadding * s
            Dim lineGap As Single = _mainSubTextSpacing * s
            Dim subFont = GetSubTextFont()

            For Each l In _layoutCache
                If l.Index < 0 OrElse l.Index >= _items.Count Then Continue For
                Dim it = _items(l.Index)
                If it Is Nothing Then Continue For

                Dim drawRect As New RectangleF(
                    _cardsViewportRect.X + l.Bounds.X,
                    _cardsViewportRect.Y + l.Bounds.Y - _scrollOffset,
                    l.Bounds.Width,
                    l.Bounds.Height)
                If drawRect.Bottom < _cardsViewportRect.Top OrElse drawRect.Top > _cardsViewportRect.Bottom Then Continue For

                Dim textRect As New RectangleF(drawRect.X + padX,
                                               drawRect.Y + padY,
                                               Math.Max(0.0F, drawRect.Width - padX * 2.0F),
                                               Math.Max(0.0F, drawRect.Height - padY * 2.0F))
                If textRect.Width <= 0 OrElse textRect.Height <= 0 Then Continue For

                Dim mainText As String = If(it.Text, "")
                Dim subText As String = If(it.SubText, "")
                If String.IsNullOrEmpty(mainText) AndAlso String.IsNullOrEmpty(subText) Then Continue For

                Dim mainH As Single = GetCachedTextHeight(mainText, Font, "main")
                Dim subH As Single = GetCachedTextHeight(subText, subFont, "sub")
                Dim totalH As Single = mainH + If(subH > 0, lineGap + subH, 0.0F)
                Dim startY As Single = textRect.Y + Math.Max(0.0F, (textRect.Height - totalH) / 2.0F)

                If Not String.IsNullOrEmpty(mainText) Then
                    context.DrawText(mainText, Font, If(it.ForeColor.IsEmpty, MyBase.ForeColor, it.ForeColor), New RectangleF(textRect.X, startY, textRect.Width, Math.Max(1.0F, mainH)), DpiScale(), TextAlignment.Leading, ParagraphAlignment.Near)
                End If
                If Not String.IsNullOrEmpty(subText) Then
                    context.DrawText(subText, subFont, _subTextForeColor, New RectangleF(textRect.X, startY + mainH + lineGap, textRect.Width, Math.Max(1.0F, subH)), DpiScale(), TextAlignment.Leading, ParagraphAlignment.Near)
                End If
            Next
        End Using
    End Sub

    Private Sub DrawScrollBar_DX12(context As D3D12_PaintContext)
        If _scrollBar.TrackRect.IsEmpty Then Return
        Dim s As Single = DpiScale()
        Dim width As Single = Math.Max(1.0F, _scrollBarWidth * s)
        Dim trackArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.TrackRect.Y, width, _scrollBar.TrackRect.Height)
        Dim thumbArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.ThumbRect.Y, width, _scrollBar.ThumbRect.Height)
        FillRoundedRect_DX12(context, trackArea, Math.Min(width / 2.0F, trackArea.Height / 2.0F), _scrollBarTrackColor)
        Dim thumbColor = If(_scrollBar.IsDragging OrElse _scrollBar.IsHover, _scrollBarThumbHoverColor, _scrollBarThumbColor)
        FillRoundedRect_DX12(context, thumbArea, Math.Min(width / 2.0F, thumbArea.Height / 2.0F), thumbColor)
    End Sub

    Private Sub DrawDisabledOverlay_DX12(context As D3D12_PaintContext)
        Dim s As Single = DpiScale()
        Dim rect As New RectangleF(0, 0, Width, Height)
        If _borderSize > 0 Then
            Dim half As Single = _borderSize * s / 2.0F
            rect.Inflate(-half, -half)
        End If
        FillRoundedRect_DX12(context, rect, _borderRadius * s, _disabledOverlayColor)
    End Sub

    Private Sub FillRoundedRect_DX12(context As D3D12_PaintContext, rect As RectangleF, radius As Single, color As Color)
        context.FillRoundedRectangle(rect, radius, color)
    End Sub

    Private Sub DrawRoundedBorder_DX12(context As D3D12_PaintContext, rect As RectangleF, radius As Single, color As Color, strokeWidth As Single)
        context.StrokeRoundedRectangle(rect, radius, color, strokeWidth)
    End Sub
End Class
