Imports System.Numerics
Imports System.Globalization
Imports DW = Vortice.DirectWrite

Partial Public Class ModernColorDialog
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse _isClosing OrElse IsDisposed OrElse Disposing Then Return

        初始化D2D颜色对话框()
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

        MessageDialogRendering12.FillRoundedRectangle(context, l.ChromaticityFrame, D2DElementBackColor, l.Radius)
        绘制D2D色域位图_DX12(context)
        绘制D2D准心_DX12(context, l.ChromaticityRect)

        MessageDialogRendering12.FillRoundedRectangle(context, l.SearchBox, D2DElementBackColor, l.Radius)
        MessageDialogRendering12.DrawRoundedRectangle(context, l.SearchBox, If(ReferenceEquals(_d2dActiveTextBox, _d2dTextBoxes(ColorDialogTextBoxKind.Search)), D2DElementBorderColor, Color.Transparent), 1.0F * 取D2D缩放(), l.Radius)
        MessageDialogRendering12.FillRoundedRectangle(context, l.HtmlList, D2DElementBackColor, l.Radius)
        绘制HTML列表图形_DX12(context)

        For Each box In _d2dTextBoxOrder
            If box.Kind = ColorDialogTextBoxKind.Search Then Continue For
            MessageDialogRendering12.FillRoundedRectangle(context, box.Bounds, D2DElementBackColor, l.Radius)
            Dim border = If(ReferenceEquals(_d2dActiveTextBox, box), D2DElementBorderColor, Color.Transparent)
            MessageDialogRendering12.DrawRoundedRectangle(context, box.Bounds, border, 1.0F * 取D2D缩放(), l.Radius)
        Next

        绘制当前颜色预览_DX12(context)
        绘制收藏夹_DX12(context)
        绘制按钮_DX12(context, l.ButtonEyeDropper, ColorDialogButtonKind.EyeDropper)
        绘制按钮_DX12(context, l.ButtonTips, ColorDialogButtonKind.Tips)
        绘制按钮_DX12(context, l.ButtonCopyArgb, ColorDialogButtonKind.CopyArgb)
        绘制按钮_DX12(context, l.ButtonCopyHex, ColorDialogButtonKind.CopyHex)
        绘制按钮_DX12(context, l.ButtonOK, ColorDialogButtonKind.OK)
        绘制按钮_DX12(context, l.ButtonCancel, ColorDialogButtonKind.Cancel)
    End Sub

    Private Sub 绘制D2D文字层_DX12(context As D3D12_PaintContext)
        Dim l = _d2dLayout
        If l Is Nothing Then Return
        Dim flagsLeft As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        Dim flagsRight As TextFormatFlags = TextFormatFlags.Right Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        Dim flagsCenter As TextFormatFlags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        Dim s = 取D2D缩放()

        MessageDialogRendering12.DrawText(context, 取界面文本("Chromaticity", "色域图"), Font, l.ChromaticityTitle, ForeColor, flagsLeft, s)
        MessageDialogRendering12.DrawText(context, 取界面文本("HtmlColors", "HTML 颜色"), Font, l.HtmlTitle, ForeColor, flagsLeft, s)
        MessageDialogRendering12.DrawText(context, 取界面文本("Values", "数值"), Font, l.ValuesTitle, ForeColor, flagsRight, s)

        _d2dTextBoxes(ColorDialogTextBoxKind.Search).Renderer.WaterText = 取界面文本("SearchWatermark", "搜索")
        For Each box In _d2dTextBoxOrder
            box.Renderer.ForeColor = ForeColor
            box.Renderer.SelectionColor = D2DElementBackColor
            box.Renderer.DrawGpu12(context)
        Next

        绘制HTML列表文字_DX12(context)
        绘制数值标签_DX12(context, flagsRight)
        绘制收藏夹标题文字_DX12(context)

        绘制按钮文字_DX12(context, l.ButtonEyeDropper, If(_eyeDropperActive, 取界面文本("EyeDropperCancel", "按 Esc 取消"), 取界面文本("EyeDropper", "取色器")))
        绘制按钮文字_DX12(context, l.ButtonTips, 取界面文本("Tips", "使用技巧"))
        绘制按钮文字_DX12(context, l.ButtonCopyArgb, 取界面文本("CopyArgb", "复制 ARGB"))
        绘制按钮文字_DX12(context, l.ButtonCopyHex, 取界面文本("CopyHex", "复制 HEX"))
        绘制按钮文字_DX12(context, l.ButtonOK, 取界面文本("OK", "确定"))
        绘制按钮文字_DX12(context, l.ButtonCancel, 取界面文本("Cancel", "取消"))

        For i = 0 To 9
            Dim textColor = If(_favoriteSet(i), 收藏夹文字颜色(_favoriteColors(i)), Color.Gray)
            MessageDialogRendering12.DrawText(context, (i + 1).ToString(CultureInfo.InvariantCulture), Font, l.FavoriteRects(i), textColor, flagsCenter, s)
        Next
    End Sub

    Private Sub 绘制D2D色域位图_DX12(context As D3D12_PaintContext)
        Dim l = _d2dLayout
        If l Is Nothing Then Return
        Dim clipRect = l.ChromaticityRect
        If clipRect.Width <= 0 OrElse clipRect.Height <= 0 Then Return

        SyncLock _chromaticityBitmapLock
            Dim bmp = _chromaticityBitmap
            If bmp Is Nothing Then Return

            Try
                If bmp.Width <= 0 OrElse bmp.Height <= 0 Then Return
                context.DrawImage(bmp, clipRect)
            Catch ex As ArgumentException
                Return
            Catch ex As InvalidOperationException
                Return
            End Try
        End SyncLock
    End Sub

    Private Sub 绘制D2D准心_DX12(context As D3D12_PaintContext, rect As RectangleF)
        If rect.Width < 10 OrElse rect.Height < 10 Then Return
        Dim pt = 色度坐标转矩形像素(_markerX, _markerY, rect)
        Dim lum = 0.299 * SelectedColor.R + 0.587 * SelectedColor.G + 0.114 * SelectedColor.B
        Dim invColor As Color = If(lum > 128, Color.Black, Color.White)
        Dim crossSize As Single = 12.0F * 取D2D缩放()
        Dim gap As Single = 4.0F * 取D2D缩放()
        Dim width As Single = 1.5F * 取D2D缩放()
        Dim brush = invColor
        context.DrawLine(New Vector2(pt.X - crossSize, pt.Y), New Vector2(pt.X - gap, pt.Y), brush, width)
        context.DrawLine(New Vector2(pt.X + gap, pt.Y), New Vector2(pt.X + crossSize, pt.Y), brush, width)
        context.DrawLine(New Vector2(pt.X, pt.Y - crossSize), New Vector2(pt.X, pt.Y - gap), brush, width)
        context.DrawLine(New Vector2(pt.X, pt.Y + gap), New Vector2(pt.X, pt.Y + crossSize), brush, width)

        context.StrokeEllipse(New RectangleF(pt.X - crossSize, pt.Y - crossSize, 2 * crossSize, 2 * crossSize), Color.FromArgb(120, invColor), 取D2D缩放())
    End Sub

    Private Sub 绘制HTML列表图形_DX12(context As D3D12_PaintContext)
        Dim clip = 取HTML列表视口矩形()
        If clip.Width > 0 AndAlso clip.Height > 0 Then
            Using context.PushClip(clip)
                Dim firstIndex = HTML首项索引()
                For row = 0 To 可绘制HTML项数量() - 1
                    Dim idx = firstIndex + row
                    If idx < 0 OrElse idx >= _d2dFilteredHtmlColors.Count Then Exit For
                    Dim itemRect = 取HTML列表项矩形(row)
                    If idx = _d2dSelectedHtmlIndex Then
                        MessageDialogRendering12.FillRoundedRectangle(context, itemRect, D2DElementPressedBackColor, 6.0F * 取D2D缩放())
                    ElseIf idx = _d2dHoverHtmlIndex Then
                        MessageDialogRendering12.FillRoundedRectangle(context, itemRect, D2DElementHoverBackColor, 6.0F * 取D2D缩放())
                    End If
                    Dim swatchSize As Single = 16.0F * 取D2D缩放()
                    Dim swatchPadding As Single = Math.Max(0.0F, (itemRect.Height - swatchSize) / 2.0F)
                    Dim swatchRect As New RectangleF(itemRect.X + swatchPadding,
                                                     itemRect.Y + swatchPadding,
                                                     swatchSize,
                                                     swatchSize)
                    Dim c = _d2dFilteredHtmlColors(idx).Value
                    MessageDialogRendering12.FillRoundedRectangle(context, swatchRect, c, 3.0F * 取D2D缩放())
                Next
            End Using
        End If
        绘制HTML滚动条_DX12(context)
    End Sub

    Private Sub 绘制HTML列表文字_DX12(context As D3D12_PaintContext)
        Dim flags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        Dim s = 取D2D缩放()
        Dim clip = 取HTML列表视口矩形()
        If clip.Width <= 0 OrElse clip.Height <= 0 Then Return
        Using context.PushClip(clip)
            Dim firstIndex = HTML首项索引()
            For row = 0 To 可绘制HTML项数量() - 1
                Dim idx = firstIndex + row
                If idx < 0 OrElse idx >= _d2dFilteredHtmlColors.Count Then Exit For
                Dim itemRect = 取HTML列表项矩形(row)
                Dim swatchSize As Single = 16.0F * s
                Dim swatchPadding As Single = Math.Max(0.0F, (itemRect.Height - swatchSize) / 2.0F)
                Dim textX As Single = itemRect.X + swatchPadding + swatchSize + swatchPadding
                Dim textRect As New RectangleF(textX, itemRect.Y, Math.Max(1.0F, itemRect.Right - textX - swatchPadding), itemRect.Height)
                MessageDialogRendering12.DrawText(context, _d2dFilteredHtmlColors(idx).Key, Font, textRect, ForeColor, flags, s)
            Next
        End Using
    End Sub

    Private Sub 绘制数值标签_DX12(context As D3D12_PaintContext, flagsRight As TextFormatFlags)
        For Each kind In {ColorDialogTextBoxKind.R, ColorDialogTextBoxKind.G, ColorDialogTextBoxKind.B,
                          ColorDialogTextBoxKind.H, ColorDialogTextBoxKind.S, ColorDialogTextBoxKind.L,
                          ColorDialogTextBoxKind.A, ColorDialogTextBoxKind.Hex}
            Dim box = _d2dTextBoxes(kind)
            MessageDialogRendering12.DrawText(context, 取数值标签文本(kind), Font, box.LabelRect, ForeColor, flagsRight, 取D2D缩放())
        Next
    End Sub

    Private Sub 绘制当前颜色预览_DX12(context As D3D12_PaintContext)
        Dim r = _d2dLayout.Preview
        MessageDialogRendering12.FillRoundedRectangle(context, r, SelectedColor, _d2dLayout.Radius)
    End Sub

    Private Sub 绘制收藏夹_DX12(context As D3D12_PaintContext)
        For i = 0 To 9
            Dim r = _d2dLayout.FavoriteRects(i)
            Dim fill = If(_favoriteSet(i), Color.FromArgb(_favoriteColors(i).R, _favoriteColors(i).G, _favoriteColors(i).B), D2DFavoriteSlotBackColor)
            If i = _d2dPressedFavorite Then
                fill = 混合颜色(fill, Color.White, 0.18F)
            ElseIf i = _d2dHoverFavorite Then
                fill = 混合颜色(fill, Color.White, 0.1F)
            End If
            MessageDialogRendering12.FillRoundedRectangle(context, r, fill, _d2dLayout.Radius)
        Next
    End Sub

    Private Sub 绘制收藏夹标题文字_DX12(context As D3D12_PaintContext)
        Dim l = _d2dLayout
        If l Is Nothing Then Return
        Dim s = 取D2D缩放()
        Dim flagsMain As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        Dim flagsHint As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Bottom Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        MessageDialogRendering12.DrawText(context, 取界面文本("FavoritesTitle", "收藏夹"), Font, l.FavoriteTitleMain, ForeColor, flagsMain, s)
        Using hintFont As New Font(Font.Name, Math.Max(1.0F, Font.Size - 1.2F), FontStyle.Regular, GraphicsUnit.Point)
            MessageDialogRendering12.DrawText(context,
                                            取界面文本("FavoritesHint", "左键读取，右键写入，中键清除，仅保留在当前应用程序运行周期内"),
                                            hintFont,
                                            l.FavoriteTitleHint,
                                            Color.Gray,
                                            flagsHint,
                                            s)
        End Using
    End Sub

    Private Sub 绘制HTML滚动条_DX12(context As D3D12_PaintContext)
        If Not 需要HTML滚动条() Then Return
        Dim track = _d2dLayout.HtmlScrollBar
        Dim thumb = 计算HTML滚动滑块()
        If track.Width <= 0 OrElse track.Height <= 0 OrElse thumb.Width <= 0 OrElse thumb.Height <= 0 Then Return
        Dim radius As Single = Math.Max(1.0F, track.Width / 2.0F)
        MessageDialogRendering12.FillRoundedRectangle(context, track, Color.FromArgb(30, 220, 220, 220), radius)
        MessageDialogRendering12.FillRoundedRectangle(context, thumb, Color.FromArgb(120, 220, 220, 220), radius)
    End Sub

    Private Sub 绘制按钮_DX12(context As D3D12_PaintContext, rect As RectangleF, kind As ColorDialogButtonKind)
        Dim fill = D2DElementBackColor
        If _d2dPressedButton = kind Then
            fill = D2DElementPressedBackColor
        ElseIf _d2dHoverButton = kind Then
            fill = D2DElementHoverBackColor
        End If
        MessageDialogRendering12.FillRoundedRectangle(context, rect, fill, _d2dLayout.Radius)
    End Sub

    Private Sub 绘制按钮文字_DX12(context As D3D12_PaintContext, rect As RectangleF, text As String)
        Dim flags As TextFormatFlags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        MessageDialogRendering12.DrawText(context, text, Font, rect, ForeColor, flags, 取D2D缩放())
    End Sub
End Class
