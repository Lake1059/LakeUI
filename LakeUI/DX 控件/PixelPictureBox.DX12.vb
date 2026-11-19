Public Interface IPixelPictureSource12
    Function Present12(context As D3D12_PaintContext, destination As RectangleF, source As PixelPictureRegion,
                       frameIndex As Integer, nearest As Boolean) As Boolean
End Interface

Partial Public Class PixelPictureBox
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If Width <= 0 OrElse Height <= 0 Then Return
        UpdateScrollRegion()
        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
        Else
            context.FillRectangle(ClientRectangle, _backColor1)
        End If
        If _borderSize > 0 Then context.DrawRectangle(ClientRectangle, _borderColor, _borderSize)
        If _source IsNot Nothing Then
            Dim source = TryCast(_source, IPixelPictureSource12)
            If source Is Nothing Then Throw New NotSupportedException("PixelPictureBox source must implement IPixelPictureSource12 for native DX12 rendering.")
            Dim viewport = ViewportBounds()
            Dim visible = VisibleImageBounds()
            If viewport.Width > 0 AndAlso viewport.Height > 0 AndAlso visible.Width > 0 AndAlso visible.Height > 0 Then
                Using context.PushClip(viewport)
                    source.Present12(context, ImageToClient(visible), visible, _frameIndex, _zoom >= 1)
                End Using
            End If
        End If
        DrawSelection12(context)
        DrawScrollBars12(context)
    End Sub

    Private Sub DrawSelection12(context As D3D12_PaintContext)
        If _source Is Nothing OrElse Not _showSelection OrElse _selectionRect.Width <= 0 OrElse _selectionRect.Height <= 0 Then Return
        Dim screen = ImageToClient(New RectangleF(_selectionRect.X, _selectionRect.Y, _selectionRect.Width, _selectionRect.Height))
        If _selectionFillColor.A > 0 Then context.FillRectangle(screen, _selectionFillColor)
        context.DrawRectangle(screen, _selectionBorderColor, _selectionBorderSize)
        For Each position As HandlePosition In HandleList
            Dim bounds = HandleBounds(position, screen)
            context.FillRectangle(bounds, _handleColor)
            context.DrawRectangle(bounds, _handleBorderColor, 1)
        Next
    End Sub

    Private Sub DrawScrollBars12(context As D3D12_PaintContext)
        If Not _verticalTrack.IsEmpty Then
            context.FillRoundedRectangle(_verticalTrack, _verticalTrack.Width / 2, _scrollBarTrackColor)
            context.FillRoundedRectangle(_verticalThumb, _verticalThumb.Width / 2, If(_dragMode = DragMode.VerticalScroll OrElse (_pointerInside AndAlso _verticalThumb.Contains(_pointerLocation)), _scrollBarThumbHoverColor, _scrollBarThumbColor))
        End If
        If Not _horizontalTrack.IsEmpty Then
            context.FillRoundedRectangle(_horizontalTrack, _horizontalTrack.Height / 2, _scrollBarTrackColor)
            context.FillRoundedRectangle(_horizontalThumb, _horizontalThumb.Height / 2, If(_dragMode = DragMode.HorizontalScroll OrElse (_pointerInside AndAlso _horizontalThumb.Contains(_pointerLocation)), _scrollBarThumbHoverColor, _scrollBarThumbColor))
        End If
    End Sub
End Class
