Partial Friend Class ExInputBoxForm
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then Return

        Dim bounds As New RectangleF(0, 0, ClientSize.Width, ClientSize.Height)
        Dim windowRadius As Single = If(DwmWindowStyle.IsCornerModeSupported,
                                        DwmWindowStyle.PopupCornerRadiusLogical * SC,
                                        0.0F)
        Dim glass = MessageDialogRendering12.DrawBackdrop(context, bounds, 毛玻璃)

        If Not glass Then
            context.FillShape(标题栏区域, 0, 主题.TitleBarBackColor, maskRadius:=windowRadius, mask:=bounds)
            context.FillShape(内容区域, 0, 主题.ContentBackColor, maskRadius:=windowRadius, mask:=bounds)
            context.FillShape(按钮区域, 0, 主题.ButtonAreaBackColor, maskRadius:=windowRadius, mask:=bounds)
        End If

        Dim 标题文字区域 As New RectangleF(标题左边距, 0, 标题栏区域.Width - 关闭按钮宽 - 标题左边距 * 2, 标题栏高度)
        MessageDialogRendering12.DrawText(context, Me.Text, 标题字体, 标题文字区域, 主题.TitleForeColor,
            TextFormatFlags.VerticalCenter Or TextFormatFlags.Left Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding, SC)

        MessageDialogRendering12.DrawCloseButton(context, 关闭按钮区域, 关闭悬停, 关闭按下,
                                               主题.CloseButtonForeColor, 主题.CloseButtonHoverForeColor,
                                               主题.CloseButtonHoverBackColor, SC)

        MessageDialogRendering12.DrawText(context, 提示标签.Text, 提示字体, 提示标签.Bounds,
            主题.MessageForeColor, 提示文本格式, SC)

        MessageDialogRendering12.DrawRoundedRectangle(context,
            New RectangleF(0, 0, ClientSize.Width, ClientSize.Height),
            主题.FormBorderColor, 1.0F, windowRadius)
    End Sub
End Class
