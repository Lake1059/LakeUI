Partial Friend Class ExOverlayBackdropForm
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then Return
        context.FillRectangle(New RectangleF(0, 0, ClientSize.Width, ClientSize.Height), Color.FromArgb(255, Me.BackColor))
    End Sub
End Class

Partial Friend Class ExOverlayMsgBoxHostForm
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then Return

        context.FillRectangle(New RectangleF(0, 0, ClientSize.Width, ClientSize.Height), Color.FromArgb(255, 主题.OverlayBackColor))

        Dim cardRect As RectangleF = 卡片区域
        Dim cardRadius As Single = If(DwmWindowStyle.PopupUsesRoundedCorners,
                                      Math.Max(0.0F, 主题.CardBorderRadius * SC),
                                      0.0F)
        Dim glass = MessageDialogRendering12.DrawBackdrop(context, cardRect, 毛玻璃)
        If Not glass Then
            MessageDialogRendering12.FillRoundedRectangle(context, cardRect, 主题.CardBackColor, cardRadius)
            context.FillShape(按钮区域, 0, 主题.ButtonAreaBackColor, maskRadius:=cardRadius, mask:=cardRect)
        End If

        MessageDialogRendering12.DrawText(context, 标题文本, 标题字体, 标题区域, 主题.TitleForeColor,
            TextFormatFlags.WordBreak Or TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding, SC)
        MessageDialogRendering12.DrawText(context, 消息文本, 消息字体, 消息区域, 主题.MessageForeColor,
            TextFormatFlags.WordBreak Or TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding, SC)
        MessageDialogRendering12.DrawImage(context, 消息图标位图, 图标区域)
        MessageDialogRendering12.DrawRoundedRectangle(context, cardRect, 主题.CardBorderColor, 1.0F, cardRadius)
    End Sub
End Class

Partial Friend Class ExOverlayMsgBoxForm
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then Return

        Dim cardRect As New RectangleF(0, 0, ClientSize.Width, ClientSize.Height)
        Dim cardRadius As Single = If(DwmWindowStyle.IsCornerModeSupported,
                                      DwmWindowStyle.PopupCornerRadiusLogical * SC,
                                      0.0F)
        Dim glass = MessageDialogRendering12.DrawBackdrop(context, cardRect, 毛玻璃)
        If Not glass Then
            MessageDialogRendering12.FillRoundedRectangle(context, cardRect, 主题.CardBackColor, cardRadius)
            context.FillShape(按钮区域, 0, 主题.ButtonAreaBackColor, maskRadius:=cardRadius, mask:=cardRect)
        End If

        MessageDialogRendering12.DrawText(context, 标题标签.Text, 标题字体, 标题区域, 主题.TitleForeColor,
            TextFormatFlags.WordBreak Or TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding, SC)
        MessageDialogRendering12.DrawText(context, 消息标签.Text, 消息字体, 消息区域, 主题.MessageForeColor,
            TextFormatFlags.WordBreak Or TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding, SC)
        MessageDialogRendering12.DrawImage(context, 消息图标位图, 图标区域)
        MessageDialogRendering12.DrawRoundedRectangle(context,
            cardRect, 主题.CardBorderColor, 1.0F, cardRadius)
    End Sub
End Class
