Imports System.Numerics
Imports System.Globalization
Imports DW = Vortice.DirectWrite

Partial Public Class ModernFontDialog
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse _isClosing OrElse IsDisposed OrElse Disposing Then Return

        初始化D2D字体对话框()
        确保D2D布局()

        Dim keepWindowBackdropTransparent As Boolean = 应保持窗口级背景透明()
        ThisIsYourWindow.DrawFormBackdrop12(context, Me, Me)

        If BackColor.A > 0 AndAlso Not keepWindowBackdropTransparent Then
            context.FillRectangle(DisplayRectangle, BackColor)
        End If

        绘制D2D图形层_DX12(context)
        绘制D2D文字层_DX12(context)
    End Sub

    Private Sub 绘制D2D图形层_DX12(context As D3D12_PaintContext)
        Dim l = _d2dLayout
        If l Is Nothing Then Return

        For Each box In _d2dTextBoxOrder
            MessageDialogRendering12.FillRoundedRectangle(context, box.Bounds, D2DElementBackColor, l.Radius)
            Dim border = If(ReferenceEquals(_d2dActiveTextBox, box), D2DElementBorderColor, Color.Transparent)
            MessageDialogRendering12.DrawRoundedRectangle(context, box.Bounds, border, 1.0F * 取D2D缩放(), l.Radius)
        Next

        For Each list In 所有列表()
            绘制列表图形_DX12(context, list)
        Next

        绘制复选框_DX12(context, l.StrikeoutSwitch, _strikeoutChecked)
        绘制复选框_DX12(context, l.UnderlineSwitch, _underlineChecked)
    End Sub

    Private Sub 绘制D2D文字层_DX12(context As D3D12_PaintContext)
        Dim l = _d2dLayout
        If l Is Nothing Then Return
        Dim s = 取D2D缩放()
        Dim flagsTitle As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        Dim flagsMiddleLeft As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        Dim flagsCenter As TextFormatFlags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine

        MessageDialogRendering12.DrawText(context, 取界面文本("FontTitle", "字体"), Font, l.FontTitle, ForeColor, flagsTitle, s)
        MessageDialogRendering12.DrawText(context, 取界面文本("StyleTitle", "字形"), Font, l.StyleTitle, ForeColor, flagsTitle, s)
        MessageDialogRendering12.DrawText(context, 取界面文本("SizeTitle", "字号"), Font, l.SizeTitle, ForeColor, flagsTitle, s)
        MessageDialogRendering12.DrawText(context, 取界面文本("EffectsTitle", "效果"), Font, l.EffectsTitle, ForeColor, flagsTitle, s)
        MessageDialogRendering12.DrawText(context, 取界面文本("PreviewTitle", "预览"), Font, l.PreviewTitle, ForeColor, flagsTitle, s)

        _d2dTextBoxes(FontDialogTextBoxKind.FontName).Renderer.WaterText = 取界面文本("FontWatermark", "选择字体")
        _d2dTextBoxes(FontDialogTextBoxKind.Style).Renderer.WaterText = 取界面文本("StyleWatermark", "选择字形")
        _d2dTextBoxes(FontDialogTextBoxKind.Size).Renderer.WaterText = 取界面文本("SizeWatermark", "选择字号")
        For Each box In _d2dTextBoxOrder
            box.Renderer.ForeColor = ForeColor
            box.Renderer.SelectionColor = D2DElementBackColor
            box.Renderer.DrawGpu12(context)
        Next

        For Each list In 所有列表()
            绘制列表文字_DX12(context, list)
        Next

        MessageDialogRendering12.DrawText(context, 取界面文本("Strikeout", "删除线"), Font, l.StrikeoutText, ForeColor, flagsMiddleLeft, s)
        MessageDialogRendering12.DrawText(context, 取界面文本("Underline", "下划线"), Font, l.UnderlineText, ForeColor, flagsMiddleLeft, s)

        绘制预览文本_DX12(context)
        绘制按钮_DX12(context, l.ButtonOK, FontDialogButtonKind.OK)
        绘制按钮_DX12(context, l.ButtonCancel, FontDialogButtonKind.Cancel)
        MessageDialogRendering12.DrawText(context, 取界面文本("OK", "确定"), Font, l.ButtonOK, ForeColor, flagsCenter, s)
        MessageDialogRendering12.DrawText(context, 取界面文本("Cancel", "取消"), Font, l.ButtonCancel, ForeColor, flagsCenter, s)
    End Sub

    Private Sub 绘制列表图形_DX12(context As D3D12_PaintContext, list As VirtualList)
        MessageDialogRendering12.FillRoundedRectangle(context, list.Bounds, D2DElementBackColor, _d2dLayout.Radius)
        Dim clip = list.Viewport
        If clip.Width > 0 AndAlso clip.Height > 0 Then
            Using context.PushClip(clip)
                Dim firstIndex = list.ScrollIndex
                Dim drawCount = 可绘制列表项数量(list)
                For row = 0 To drawCount - 1
                    Dim idx = firstIndex + row
                    If idx < 0 OrElse idx >= list.Items.Count Then Exit For
                    Dim itemRect = 取列表项矩形(list, row)
                    If idx = list.SelectedIndex Then
                        MessageDialogRendering12.FillRoundedRectangle(context, itemRect, D2DElementPressedBackColor, 6.0F * 取D2D缩放())
                    ElseIf idx = list.HoverIndex Then
                        MessageDialogRendering12.FillRoundedRectangle(context, itemRect, 取列表项悬停颜色(), 6.0F * 取D2D缩放())
                    End If
                Next
            End Using
        End If
        绘制列表滚动条_DX12(context, list)
    End Sub

    Private Sub 绘制列表文字_DX12(context As D3D12_PaintContext, list As VirtualList)
        Dim clip = list.Viewport
        If clip.Width <= 0 OrElse clip.Height <= 0 Then Return
        Dim s = 取D2D缩放()
        Dim flags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        Using context.PushClip(clip)
            Dim firstIndex = list.ScrollIndex
            Dim drawCount = 可绘制列表项数量(list)
            For row = 0 To drawCount - 1
                Dim idx = firstIndex + row
                If idx < 0 OrElse idx >= list.Items.Count Then Exit For
                Dim itemRect = 取列表项矩形(list, row)
                Dim textX As Single = itemRect.X + _d2dLayout.ListItemPaddingLeft
                Dim rightEdge As Single = itemRect.Right
                If 需要列表滚动条(list) Then rightEdge = Math.Min(rightEdge, list.ScrollBarTrack.Left)
                Dim textRect As New RectangleF(textX,
                                                itemRect.Y,
                                                Math.Max(1.0F, rightEdge - textX - _d2dLayout.ListItemPaddingLeft),
                                                itemRect.Height)
                Dim itemFont As Font = Nothing
                Try
                    Select Case list.Kind
                        Case FontDialogListKind.FontName
                            itemFont = 创建字体名称列表项字体(list.Items(idx))
                            MessageDialogRendering12.DrawText(context, list.Items(idx), If(itemFont, Font), textRect, ForeColor, flags, s)
                        Case FontDialogListKind.Style
                            Dim familyName = 取文本框文本(FontDialogTextBoxKind.FontName).Trim()
                            Dim entry = If(idx >= 0 AndAlso idx < _styleEntries.Count, _styleEntries(idx), Nothing)
                            itemFont = 创建字形列表项字体(familyName, entry)
                            MessageDialogRendering12.DrawText(context, list.Items(idx), If(itemFont, Font), textRect, ForeColor, flags, s)
                        Case Else
                            MessageDialogRendering12.DrawText(context, list.Items(idx), Font, textRect, ForeColor, flags, s)
                    End Select
                Finally
                    itemFont?.Dispose()
                End Try
            Next
        End Using
    End Sub

    Private Sub 绘制列表滚动条_DX12(context As D3D12_PaintContext, list As VirtualList)
        If Not 需要列表滚动条(list) Then Return
        Dim track = list.ScrollBarTrack
        Dim thumb = 计算列表滚动滑块(list)
        If track.Width <= 0 OrElse track.Height <= 0 OrElse thumb.Width <= 0 OrElse thumb.Height <= 0 Then Return
        Dim radius As Single = Math.Max(1.0F, track.Width / 2.0F)
        MessageDialogRendering12.FillRoundedRectangle(context, track, Color.FromArgb(30, 220, 220, 220), radius)
        MessageDialogRendering12.FillRoundedRectangle(context, thumb, Color.FromArgb(120, 220, 220, 220), radius)
    End Sub

    Private Sub 绘制复选框_DX12(context As D3D12_PaintContext, rect As RectangleF, checked As Boolean)
        Dim s As Single = 取D2D缩放()
        Dim fill As Color = If(checked, Color.FromArgb(0, 120, 215), D2DElementBackColor)
        MessageDialogRendering12.FillRoundedRectangle(context, rect, fill, 3.0F * s)

        If Not checked Then Return
        Dim brush = Color.White
        Dim points = {
            New Vector2(rect.X + 5.8F * s, rect.Y + 12.6F * s),
            New Vector2(rect.X + 10.0F * s, rect.Y + 16.8F * s),
            New Vector2(rect.X + 18.4F * s, rect.Y + 8.2F * s)
        }
        Dim width = Math.Max(1.5F, 2.1F * s)
        context.DrawLine(points(0), points(1), brush, width)
        context.DrawLine(points(1), points(2), brush, width)
    End Sub

    Private Sub 绘制预览文本_DX12(context As D3D12_PaintContext)
        Dim texts = {
            取界面文本("PreviewSampleChinese", "敏捷的棕色狐狸跳过了懒惰的狗。"),
            取界面文本("PreviewSampleEnglish", "The quick brown fox jumps over the lazy dog."),
            取界面文本("PreviewSampleGlyphs", "永字八法；横竖撇捺折钩点。"),
            取界面文本("PreviewSampleSymbols", "+-*/~！@#￥%……&&*（）-= —— · <>?:;""'[]{}|\")
        }
        For i = 0 To 3
            绘制预览行_DX12(context, texts(i), _d2dLayout.PreviewLines(i))
        Next
    End Sub

    Private Sub 绘制预览行_DX12(context As D3D12_PaintContext, text As String, rect As RectangleF)
        If String.IsNullOrEmpty(text) OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim f = SelectedFont
        Dim s = 取D2D缩放()
        Dim sizePx As Single = Math.Max(1.0F, CSng(f.SizeInPoints * 96.0F / 72.0F * Math.Max(0.01F, s)))
        Dim selectedStyle = 查找字形项(取文本框文本(FontDialogTextBoxKind.Style))
        Dim weight As DW.FontWeight = If(selectedStyle IsNot Nothing, selectedStyle.DWriteWeight, If(f.Bold, DW.FontWeight.Bold, DW.FontWeight.Normal))
        Dim style As DW.FontStyle = If(selectedStyle IsNot Nothing, selectedStyle.DWriteStyle, If(f.Italic, DW.FontStyle.Italic, DW.FontStyle.Normal))
        Dim stretch As DW.FontStretch = If(selectedStyle IsNot Nothing, selectedStyle.DWriteStretch, DW.FontStretch.Normal)
        Using fmt = D3D_RenderCore.DeviceManager.DWriteFactory.CreateTextFormat(f.FontFamily.Name, Nothing, weight, style, stretch, sizePx)
            fmt.TextAlignment = DW.TextAlignment.Leading
            fmt.ParagraphAlignment = DW.ParagraphAlignment.Near
            fmt.WordWrapping = DW.WordWrapping.Wrap
            Using layout = D3D_RenderCore.DeviceManager.DWriteFactory.CreateTextLayout(text, fmt, Math.Max(1.0F, rect.Width), Math.Max(1.0F, rect.Height))
                Dim range As New DW.TextRange(0, text.Length)
                If f.Underline Then layout.SetUnderline(True, range)
                If f.Strikeout Then layout.SetStrikethrough(True, range)
                context.DrawTextLayout(layout, Color.White, New Vector2(rect.X, rect.Y))
            End Using
        End Using
    End Sub

    Private Sub 绘制按钮_DX12(context As D3D12_PaintContext, rect As RectangleF, kind As FontDialogButtonKind)
        Dim fill = D2DElementBackColor
        If _d2dPressedButton = kind Then
            fill = D2DElementPressedBackColor
        ElseIf _d2dHoverButton = kind Then
            fill = D2DElementHoverBackColor
        End If
        MessageDialogRendering12.FillRoundedRectangle(context, rect, fill, _d2dLayout.Radius)
    End Sub
End Class
