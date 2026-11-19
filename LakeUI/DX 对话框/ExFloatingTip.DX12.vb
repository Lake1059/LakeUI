Partial Friend Class ExFloatingTipForm
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

        Dim textRect As New RectangleF(
            卡片边框宽度 + 卡片内边距,
            卡片边框宽度 + 卡片内边距,
            Me.ClientSize.Width - (卡片边框宽度 + 卡片内边距) * 2,
            Me.ClientSize.Height - (卡片边框宽度 + 卡片内边距) * 2)
        MessageDialogRendering12.DrawText(context, 消息文本, 消息字体, textRect,
            主题.MessageForeColor, 文本标志, SC)

        If 卡片边框宽度 > 0 Then
            MessageDialogRendering12.DrawRoundedRectangle(context,
                bounds, 主题.CardBorderColor, CSng(卡片边框宽度), windowRadius)
        End If
    End Sub
End Class
