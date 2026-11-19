Imports Vortice.DirectWrite

Partial Public Class MemberWall
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        EnsureLayout()
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
        Dim backColorMask As Color = MyBase.BackColor

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
        End If
        If _backgroundSource Is Nothing AndAlso backColorMask.A > 0 AndAlso backColorMask.A < 255 Then
            context.FillRoundedRectangle(rect, radius, backColorMask)
        End If
        If _backColor1.A > 0 Then context.FillRoundedRectangle(rect, radius, _backColor1)
        If _borderSize > 0 AndAlso _borderColor.A > 0 Then context.StrokeRoundedRectangle(rect, radius, _borderColor, _borderSize * s)
    End Sub

    Private Sub DrawCards_DX12(context As D3D12_PaintContext)
        If _cardsViewportRect.Width <= 0 OrElse _cardsViewportRect.Height <= 0 Then Return
        Using context.PushClip(_cardsViewportRect)
            Dim s As Single = DpiScale()
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

                Dim borderSize As Integer = If(it.BorderSize >= 0, it.BorderSize, _cardBorderSize)
                If borderSize > 0 Then
                    Dim half As Single = borderSize * s / 2.0F
                    drawRect.Inflate(-half, -half)
                End If

                Dim bg As Color = If(it.BackColor.IsEmpty, _cardBackColor, it.BackColor)
                Dim bc As Color = If(it.BorderColor.IsEmpty, _cardBorderColor, it.BorderColor)
                Dim radius As Single = _cardRadius * s

                If bg.A > 0 Then context.FillRoundedRectangle(drawRect, radius, bg)
                DrawCardOverlay_DX12(context, drawRect, radius, l.Index)
                If borderSize > 0 AndAlso bc.A > 0 Then context.StrokeRoundedRectangle(drawRect, radius, bc, borderSize * s)
            Next
        End Using
    End Sub

    Private Sub DrawCardOverlay_DX12(context As D3D12_PaintContext, rect As RectangleF, radius As Single, index As Integer)
        Dim overlay As Color = Color.Empty
        If _pressedIndex = index Then
            overlay = _pressedOverlayColor
        ElseIf _hoverIndex = index Then
            overlay = _hoverOverlayColor
        End If
        If overlay.A > 0 Then context.FillRoundedRectangle(rect, radius, overlay)
    End Sub

    Private Sub DrawCardTexts_DX12(context As D3D12_PaintContext)
        If _cardsViewportRect.Width <= 0 OrElse _cardsViewportRect.Height <= 0 Then Return
        Using context.PushClip(_cardsViewportRect)
            Dim s As Single = DpiScale()
            Dim padX As Single = _cardHorizontalPadding * s
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

                Dim textRect As New RectangleF(drawRect.X + padX, drawRect.Y, Math.Max(0.0F, drawRect.Width - padX * 2.0F), drawRect.Height)
                If textRect.Width <= 0 OrElse textRect.Height <= 0 Then Continue For
                Dim fc As Color = If(it.ForeColor.IsEmpty, MyBase.ForeColor, it.ForeColor)
                context.DrawText(If(it.Text, ""), Font, fc, textRect, s, TextAlignment.Center, ParagraphAlignment.Center)
            Next
        End Using
    End Sub

    Private Sub DrawScrollBar_DX12(context As D3D12_PaintContext)
        If _scrollBar.TrackRect.IsEmpty Then Return
        Dim s As Single = DpiScale()
        Dim width As Single = Math.Max(1.0F, _scrollBarWidth * s)
        Dim trackArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.TrackRect.Y, width, _scrollBar.TrackRect.Height)
        Dim thumbArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.ThumbRect.Y, width, _scrollBar.ThumbRect.Height)
        context.FillRoundedRectangle(trackArea, Math.Min(width / 2.0F, trackArea.Height / 2.0F), _scrollBarTrackColor)
        Dim thumbColor = If(_scrollBar.IsDragging OrElse _scrollBar.IsHover, _scrollBarThumbHoverColor, _scrollBarThumbColor)
        context.FillRoundedRectangle(thumbArea, Math.Min(width / 2.0F, thumbArea.Height / 2.0F), thumbColor)
    End Sub

    Private Sub DrawDisabledOverlay_DX12(context As D3D12_PaintContext)
        Dim s As Single = DpiScale()
        Dim rect As New RectangleF(0, 0, Width, Height)
        If _borderSize > 0 Then
            Dim half As Single = _borderSize * s / 2.0F
            rect.Inflate(-half, -half)
        End If
        context.FillRoundedRectangle(rect, _borderRadius * s, _disabledOverlayColor)
    End Sub
End Class
