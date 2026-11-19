Partial Friend Class ExFloatingBoxForm
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then Return

        Dim bounds As New RectangleF(0, 0, ClientSize.Width, ClientSize.Height)
        Dim windowRadius As Single = If(DwmWindowStyle.IsCornerModeSupported,
                                        DwmWindowStyle.PopupCornerRadiusLogical * SC,
                                        0.0F)
        Dim glass = MessageDialogRendering12.DrawBackdrop(context, bounds, 毛玻璃)
        If Not glass Then
            MessageDialogRendering12.FillRoundedRectangle(context, bounds, 主题.CardBackColor, windowRadius)
        End If

        If 有标题 Then
            MessageDialogRendering12.DrawText(context, 标题标签.Text, 标题字体, 标题标签.Bounds,
                主题.TitleForeColor, TextFormatFlags.WordBreak Or TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding, SC)
        End If
        MessageDialogRendering12.DrawText(context, 消息标签.Text, 消息字体, 消息标签.Bounds,
            主题.MessageForeColor, TextFormatFlags.WordBreak Or TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding, SC)
        MessageDialogRendering12.DrawImage(context, 消息图标位图, 图标区域)
        MessageDialogRendering12.DrawRoundedRectangle(context,
            bounds, 主题.CardBorderColor, 1.0F, windowRadius)
    End Sub
End Class
