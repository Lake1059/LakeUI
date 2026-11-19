Partial Public Class ModernPanel
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If UseNativeTransparentPresentation OrElse Width < 1 OrElse Height < 1 Then Return
        更新滚动区域()
        Dim scale = DeviceDpi / 96.0F
        Dim stroke = 边框宽度 * scale
        Dim area As New RectangleF(0, 0, Width, Height)
        area.Inflate(-stroke / 2, -stroke / 2)
        If area.Width <= 0 OrElse area.Height <= 0 Then Return
        Dim radius = Math.Clamp(边框圆角半径 * scale, 0, Math.Min(area.Width, area.Height) / 2)
        Dim corners = If(圆角位置.TopLeft, 1, 0) Or If(圆角位置.TopRight, 2, 0) Or
                      If(圆角位置.BottomLeft, 4, 0) Or If(圆角位置.BottomRight, 8, 0)
        If radius <= 0 Then corners = 0
        If _backgroundSource IsNot Nothing Then context.DrawBackgroundSource(_backgroundSource, Rectangle.Round(area))
        context.DrawPanelShape(area, radius, 0, corners, MyBase.BackColor)
        context.DrawPanelShape(area, radius, 0, corners, 背景颜色)
        If _image IsNot Nothing Then
            Dim dest = 计算背景图片目标矩形(area, _image.Width, _image.Height, _imageFillMode)
            Using context.PushClip(area)
                If _animatedImage IsNot Nothing AndAlso _animationFrameDimension IsNot Nothing AndAlso _animationFrameCount > 1 Then
                    SyncLock _animatedImage
                        _animatedImage.SelectActiveFrame(_animationFrameDimension, _animationFrameIndex)
                        context.DrawMaskedImage(_animatedImage, dest, area, radius, corners)
                    End SyncLock
                Else
                    context.DrawMaskedImage(_image, dest, area, radius, corners)
                End If
            End Using
        End If
        context.DrawPanelShape(area, radius, 0, corners, 遮罩颜色)
        If stroke > 0 Then context.DrawPanelShape(area, radius, stroke, corners, 边框颜色)
        绘制垂直滚动条_DX12(context)
        绘制水平滚动条_DX12(context)
    End Sub
    Private Sub 绘制垂直滚动条_DX12(context As D3D12_PaintContext)
        If _vScrollBar.TrackRect.IsEmpty Then Return
        Dim scale As Single = Math.Max(0.01F, DeviceDpi / 96.0F)
        Dim scrollBarWidth As Single = Math.Max(1.0F, 滚动条宽度 * scale)
        Dim trackArea As New RectangleF(_vScrollBar.VisualLeft, _vScrollBar.TrackRect.Y, scrollBarWidth, _vScrollBar.TrackRect.Height)
        Dim thumbArea As New RectangleF(_vScrollBar.VisualLeft, _vScrollBar.ThumbRect.Y, scrollBarWidth, _vScrollBar.ThumbRect.Height)

        If 滚动条轨道颜色.A > 0 Then
            context.FillRoundedRectangle(trackArea, Math.Min(scrollBarWidth / 2.0F, trackArea.Height / 2.0F), 滚动条轨道颜色)
        End If

        Dim thumbColor = If(_vScrollBar.IsDragging OrElse _vScrollBar.IsHover, 滚动条悬停颜色, 滚动条滑块颜色)
        context.FillRoundedRectangle(thumbArea, Math.Min(scrollBarWidth / 2.0F, thumbArea.Height / 2.0F), thumbColor)
    End Sub

    Private Sub 绘制水平滚动条_DX12(context As D3D12_PaintContext)
        If _hScrollBar.TrackRect.IsEmpty Then Return
        Dim scale As Single = Math.Max(0.01F, DeviceDpi / 96.0F)
        Dim scrollBarHeight As Single = Math.Max(1.0F, 滚动条宽度 * scale)
        Dim trackArea As New RectangleF(_hScrollBar.TrackRect.X, _hScrollBar.VisualTop, _hScrollBar.TrackRect.Width, scrollBarHeight)
        Dim thumbArea As New RectangleF(_hScrollBar.ThumbRect.X, _hScrollBar.VisualTop, _hScrollBar.ThumbRect.Width, scrollBarHeight)

        If 滚动条轨道颜色.A > 0 Then
            context.FillRoundedRectangle(trackArea, Math.Min(scrollBarHeight / 2.0F, trackArea.Width / 2.0F), 滚动条轨道颜色)
        End If

        Dim thumbColor = If(_hScrollBar.IsDragging OrElse _hScrollBar.IsHover, 滚动条悬停颜色, 滚动条滑块颜色)
        context.FillRoundedRectangle(thumbArea, Math.Min(scrollBarHeight / 2.0F, thumbArea.Width / 2.0F), thumbColor)
    End Sub

End Class
