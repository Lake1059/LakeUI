Imports System.Numerics
Imports Vortice.DirectWrite

Partial Public Class UltraDetailListView
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If Me.Width < 1 OrElse Me.Height < 1 Then Return

        预计算滚动条布局()
        预计算横向滚动条布局()

        绘制背景与边框_DX12(context)

        Dim inset As Integer = 获取边框内边距()
        Dim clipRect As New RectangleF(inset, inset, Math.Max(0, Me.Width - inset * 2), Math.Max(0, Me.Height - inset * 2))
        If clipRect.Width > 0 AndAlso clipRect.Height > 0 Then
            Using context.PushClip(clipRect)
                If 列标题可见 AndAlso _columns.Count > 0 Then 绘制列标题形状_DX12(context)
                绘制全部行形状_DX12(context)
                绘制拖选框_DX12(context)
                绘制拖动排序指示线_DX12(context)
                If 列标题可见 AndAlso _columns.Count > 0 Then 绘制列标题_DX12(context)
                绘制全部行_DX12(context)
            End Using
        End If

        绘制滚动条_DX12(context)
        绘制横向滚动条_DX12(context)
    End Sub

    Private Sub 填充矩形_DX12(context As D3D12_PaintContext, rect As RectangleF, color As Color)
        If color.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        context.FillRectangle(rect, color)
    End Sub

    Private Sub 描边矩形_DX12(context As D3D12_PaintContext, rect As RectangleF, color As Color, strokeWidth As Single)
        If color.A = 0 OrElse strokeWidth <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        context.DrawRectangle(rect, color, strokeWidth)
    End Sub

    Private Sub 绘制水平线_DX12(context As D3D12_PaintContext, x1 As Single, x2 As Single, y As Single, color As Color, strokeWidth As Single)
        If color.A = 0 OrElse strokeWidth <= 0 Then Return
        context.DrawLine(New System.Numerics.Vector2(x1, y), New System.Numerics.Vector2(x2, y), color, strokeWidth)
    End Sub

    Private Sub 绘制垂直线_DX12(context As D3D12_PaintContext, x As Single, y1 As Single, y2 As Single, color As Color, strokeWidth As Single)
        If color.A = 0 OrElse strokeWidth <= 0 Then Return
        context.DrawLine(New System.Numerics.Vector2(x, y1), New System.Numerics.Vector2(x, y2), color, strokeWidth)
    End Sub

    Private Sub 绘制项焦点区域_DX12(context As D3D12_PaintContext, rect As RectangleF,
                              fillColor As Color, borderColor As Color, borderWidth As Single,
                              Optional roundTop As Boolean = True, Optional roundBottom As Boolean = True)
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        If fillColor.A = 0 AndAlso (borderColor.A = 0 OrElse borderWidth <= 0) Then Return
        Dim radius As Single = 获取项焦点圆角半径(rect)
        If Not roundTop AndAlso Not roundBottom Then radius = 0
        Dim corners = If(roundTop, 3, 0) Or If(roundBottom, 12, 0)
        context.DrawPanelShape(rect, radius, 0, corners, fillColor)
        If borderWidth > 0 Then context.DrawPanelShape(rect, radius, borderWidth, corners, borderColor)
    End Sub

    Private Sub 绘制背景与边框_DX12(context As D3D12_PaintContext)
        Dim s As Single = DpiScale()
        Dim boundsRect As New RectangleF(0, 0, Me.Width, Me.Height)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * s / 2.0F
            boundsRect.Inflate(-half, -half)
        End If
        Dim radius As Single = If(边框圆角半径 > 0, 边框圆角半径 * s, 0.0F)
        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
        End If
        context.FillRoundedRectangle(boundsRect, radius, 背景颜色)
        If 边框颜色.A > 0 AndAlso 边框宽度 > 0 Then
            context.StrokeRoundedRectangle(boundsRect, radius, 边框颜色, 边框宽度 * s)
        End If
    End Sub

    Private Sub 绘制滚动条_DX12(context As D3D12_PaintContext)
        If _scrollBar.TrackRect.IsEmpty Then Return
        Dim width As Single = Math.Max(1.0F, Dpi(滚动条宽度))
        Dim trackArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.TrackRect.Y, width, _scrollBar.TrackRect.Height)
        Dim thumbArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.ThumbRect.Y, width, _scrollBar.ThumbRect.Height)
        context.FillRoundedRectangle(trackArea, Math.Min(width / 2.0F, trackArea.Height / 2.0F), 滚动条轨道颜色)
        Dim thumbColor = If(_scrollBar.IsDragging OrElse _scrollBar.IsHover, 滚动条悬停颜色, 滚动条滑块颜色)
        context.FillRoundedRectangle(thumbArea, Math.Min(width / 2.0F, thumbArea.Height / 2.0F), thumbColor)
    End Sub

    Private Sub 绘制横向滚动条_DX12(context As D3D12_PaintContext)
        If _hScrollBar.TrackRect.IsEmpty Then Return
        Dim height As Single = Math.Max(1.0F, Dpi(滚动条宽度))
        Dim trackArea As New RectangleF(_hScrollBar.TrackRect.X, _hScrollBar.VisualTop, _hScrollBar.TrackRect.Width, height)
        Dim thumbArea As New RectangleF(_hScrollBar.ThumbRect.X, _hScrollBar.VisualTop, _hScrollBar.ThumbRect.Width, height)
        context.FillRoundedRectangle(trackArea, Math.Min(height / 2.0F, trackArea.Width / 2.0F), 滚动条轨道颜色)
        Dim thumbColor = If(_hScrollBar.IsDragging OrElse _hScrollBar.IsHover, 滚动条悬停颜色, 滚动条滑块颜色)
        context.FillRoundedRectangle(thumbArea, Math.Min(height / 2.0F, thumbArea.Width / 2.0F), thumbColor)
    End Sub

    Private Sub 绘制列标题形状_DX12(context As D3D12_PaintContext)
        Dim headerRect As Rectangle = 获取列标题区域()
        context.FillRectangle(headerRect, 列标题背景颜色)
        确保列X缓存()
        Dim dpiS As Single = DpiScale()
        Dim sw As Single = Math.Max(1.0F, 列标题分隔线宽度 * dpiS)
        For i As Integer = 0 To _columns.Count - 1
            Dim col = _columns(i)
            Dim x As Integer = _columnXCache(i)
            绘制垂直线_DX12(context, x + col.Width - 1, headerRect.Y + 4, headerRect.Bottom - 4, 列标题分隔线颜色, sw)
        Next
        绘制水平线_DX12(context, headerRect.X, headerRect.Right, headerRect.Bottom - 1, 列标题分隔线颜色, sw)
    End Sub

    Private Sub 绘制分组标题行形状_DX12(context As D3D12_PaintContext, grp As ListGroup, rect As Rectangle)
        context.FillRectangle(rect, 分组背景颜色)

        Dim arrowSize As Integer = Dpi(12)
        Dim arrowMargin As Integer = Dpi(10)
        Dim arrowX As Integer = rect.X + arrowMargin
        Dim arrowY As Integer = rect.Y + (rect.Height - arrowSize) \ 2
        Dim effectiveColor As Color = If(grp.ForeColor <> Color.Empty, grp.ForeColor, 分组文字颜色)
        If effectiveColor.A > 0 Then
            Dim width As Single = arrowSize
            Dim points As Vector2()
            If grp.IsCollapsed Then
                points = {New Vector2(arrowX + width / 4, arrowY + width / 8), New Vector2(arrowX + width * 3 / 4, arrowY + width / 2),
                          New Vector2(arrowX + width / 4, arrowY + width * 7 / 8)}
            Else
                points = {New Vector2(arrowX + width / 8, arrowY + width / 4), New Vector2(arrowX + width * 7 / 8, arrowY + width / 4),
                          New Vector2(arrowX + width / 2, arrowY + width * 3 / 4)}
            End If
            context.FillRoundedTriangle(points, Math.Max(1, width / 6), effectiveColor)
        End If

        绘制水平线_DX12(context, rect.X, rect.Right, rect.Bottom - 1, 分组分隔线颜色, 1.0F)
    End Sub

    Private Sub 绘制拖选框_DX12(context As D3D12_PaintContext)
        If Not _isDragSelecting Then Return
        Dim rect As Rectangle = 获取拖选矩形()
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        context.FillRectangle(rect, 选框填充颜色)
        context.DrawRectangle(rect, 选框边框颜色, 1.0F)
    End Sub

    Private Sub 绘制拖动排序指示线_DX12(context As D3D12_PaintContext)
        If Not _isDragReordering OrElse _dragReorderInsertIndex < 0 Then Return
        Dim contentRect = 获取内容区域()
        Dim availW = 获取行绘制宽度(contentRect)
        Dim lineY As Integer
        If _dragReorderInsertIndex < _displayRows.Count Then
            lineY = 获取行Y坐标(_dragReorderInsertIndex)
            If lineY < 0 Then Return
        Else
            Dim lastIdx = _dragReorderInsertIndex - 1
            If lastIdx < 0 OrElse lastIdx >= _displayRows.Count Then Return
            lineY = 获取行Y坐标(lastIdx)
            If lineY < 0 Then Return
            lineY += _displayRows(lastIdx).Height
        End If
        绘制水平线_DX12(context, contentRect.X, contentRect.X + availW, lineY, 拖动排序指示线颜色, 拖动排序指示线宽 * DpiScale())
    End Sub

    Private Sub 绘制全部行形状_DX12(context As D3D12_PaintContext)
        If _displayRows.Count = 0 Then Return
        Dim contentRect = 获取内容区域()
        If contentRect.Height <= 0 OrElse contentRect.Width <= 0 Then Return

        Dim hasMoreAbove As Boolean = _scrollOffset > 0
        Dim topIndicatorH As Integer = If(hasMoreAbove, Dpi(更多指示器高度), 0)
        Dim currentY As Integer = contentRect.Y + topIndicatorH + 获取有效内容上边距()
        Dim bottomLimit As Integer = contentRect.Bottom - 获取有效内容下边距()

        Dim availW As Integer = 获取行绘制宽度(contentRect)
        Dim itemFocusW As Integer = 获取项焦点宽度(contentRect)
        Dim dpiS As Single = DpiScale()

        If _hoverAnimActive AndAlso _hoverRowIndex >= 0 AndAlso
           _hoverRowIndex < _displayRows.Count AndAlso
           _displayRows(_hoverRowIndex).Type = DisplayRowType.Item AndAlso
           Not _selectedIndices.Contains(_hoverRowIndex) Then
            Dim t As Single = _hoverAnim.Progress
            Dim animY As Single = _hoverAnimFromY + (_hoverAnimToY - _hoverAnimFromY) * t
            Dim animH As Single = _hoverAnimFromH + (_hoverAnimToH - _hoverAnimFromH) * t
            绘制项焦点区域_DX12(context, New RectangleF(contentRect.X, animY, itemFocusW, animH),
                              项悬停背景颜色, Color.Empty, 0)
        End If

        For i As Integer = _scrollOffset To _displayRows.Count - 1
            Dim row = _displayRows(i)
            Dim rowH As Integer = row.Height
            Dim spacing As Integer = If(i = _scrollOffset, 0, row.Spacing)
            If currentY + spacing + rowH > bottomLimit Then Exit For
            currentY += spacing
            Dim rowRect As New Rectangle(contentRect.X, currentY, availW, rowH)
            Dim itemFocusRect As New Rectangle(contentRect.X, currentY, itemFocusW, rowH)

            If Not context.IntersectsDirty(rowRect) Then
                currentY += rowH
                Continue For
            End If

            If row.Type = DisplayRowType.GroupHeader Then
                ' 分组背景与项焦点区域保持一致，不能延伸到可视区域的最右侧。
                绘制分组标题行形状_DX12(context, row.Group, itemFocusRect)
            Else
                If _selectedIndices.Contains(i) Then
                    If 项选中背景颜色.A > 0 Then
                        Dim corners = If(项焦点圆角半径 > 0, 获取选中项圆角(i), (Top:=False, Bottom:=False))
                        绘制项焦点区域_DX12(context, itemFocusRect, 项选中背景颜色, Color.Empty, 0, corners.Top, corners.Bottom)
                    End If
                ElseIf i = _hoverRowIndex AndAlso Not _hoverAnimActive Then
                    绘制项焦点区域_DX12(context, itemFocusRect, 项悬停背景颜色, Color.Empty, 0)
                End If
                If 显示复选框 Then 绘制项复选框_DX12(context, row.Item, row.Group, rowRect, i)
            End If
            currentY += rowH
        Next
    End Sub

    Private Sub 绘制列标题_DX12(context As D3D12_PaintContext)
        Dim headerRect As Rectangle = 获取列标题区域()
        确保列X缓存()
        For i As Integer = 0 To _columns.Count - 1
            Dim col = _columns(i)
            Dim x As Integer = _columnXCache(i)
            Dim pad As Padding = Dpi(col.HeaderPadding)
            Dim textRect As New RectangleF(x + pad.Left, headerRect.Y + pad.Top, col.Width - pad.Horizontal, headerRect.Height - pad.Vertical)
            If Not context.IntersectsDirty(textRect) Then Continue For
            Dim drawText As String = 截断文本到宽度(col.Text, Me.Font, CInt(textRect.Width))
            context.DrawText(drawText, Me.Font, 列标题文字颜色, textRect,
                             DpiScale(), Vortice.DirectWrite.TextAlignment.Leading,
                             Vortice.DirectWrite.ParagraphAlignment.Center)
        Next
    End Sub

    Private Sub 绘制全部行_DX12(context As D3D12_PaintContext)
        If _displayRows.Count = 0 Then Return
        Dim contentRect = 获取内容区域()
        If contentRect.Height <= 0 OrElse contentRect.Width <= 0 Then Return

        Dim hasMoreAbove As Boolean = _scrollOffset > 0
        Dim topIndicatorH As Integer = If(hasMoreAbove, Dpi(更多指示器高度), 0)
        Dim currentY As Integer = contentRect.Y + topIndicatorH + 获取有效内容上边距()
        Dim bottomLimit As Integer = contentRect.Bottom - 获取有效内容下边距()
        Dim lastDrawnIndex As Integer = _scrollOffset - 1

        Dim availW As Integer = 获取行绘制宽度(contentRect)
        Dim itemFocusW As Integer = 获取项焦点宽度(contentRect)
        确保列X缓存()

        For i As Integer = _scrollOffset To _displayRows.Count - 1
            Dim row = _displayRows(i)
            Dim rowH As Integer = row.Height
            Dim spacing As Integer = If(i = _scrollOffset, 0, row.Spacing)
            If currentY + spacing + rowH > bottomLimit Then Exit For
            currentY += spacing
            Dim rowRect As New Rectangle(contentRect.X, currentY, availW, rowH)

            If Not context.IntersectsDirty(rowRect) Then
                currentY += rowH
                lastDrawnIndex = i
                Continue For
            End If

            If row.Type = DisplayRowType.GroupHeader Then
                ' 分组文字的边界必须与分组背景及项焦点宽度一致。
                Dim groupRect As New Rectangle(contentRect.X, currentY, itemFocusW, rowH)
                绘制分组标题行_DX12(context, row.Group, groupRect)
            Else
                绘制项行_DX12(context, row.Item, rowRect, i)
            End If

            currentY += rowH
            lastDrawnIndex = i
        Next

        Dim hasMoreBelow As Boolean = lastDrawnIndex < _displayRows.Count - 1
        If hasMoreBelow AndAlso currentY < bottomLimit Then
            绘制更多指示器符号_DX12(context, New Rectangle(contentRect.X, currentY, availW, bottomLimit - currentY), False)
        End If
        If hasMoreAbove Then
            绘制更多指示器符号_DX12(context, New Rectangle(contentRect.X, contentRect.Y, availW, topIndicatorH), True)
        End If
    End Sub

    Private Sub 绘制分组标题行_DX12(context As D3D12_PaintContext, grp As ListGroup, rect As Rectangle)
        Dim effectiveColor As Color = If(grp.ForeColor <> Color.Empty, grp.ForeColor, 分组文字颜色)
        Dim arrowSize As Integer = Dpi(12)
        Dim arrowMargin As Integer = Dpi(10)
        Dim arrowX As Integer = rect.X + arrowMargin
        Dim textX As Integer = arrowX + arrowSize + Dpi(6)
        Dim textRect As New RectangleF(textX, rect.Y, rect.Right - textX - Dpi(4), rect.Height)
        If Not context.IntersectsDirty(textRect) Then Return
        Dim drawText As String = 截断文本到宽度(grp.Text, Me.Font, CInt(textRect.Width))
        context.DrawText(drawText, Me.Font, effectiveColor, textRect,
                         DpiScale(), Vortice.DirectWrite.TextAlignment.Leading,
                         Vortice.DirectWrite.ParagraphAlignment.Center)
    End Sub

    Private Sub 绘制项行_DX12(context As D3D12_PaintContext, item As ListItem, rowRect As Rectangle,
                              Optional displayRowIndex As Integer = -1)
        Dim skipEditingCell As Boolean = (displayRowIndex >= 0 AndAlso displayRowIndex = _editRowIndex AndAlso _editColumnIndex >= 0)
        Dim iconAreaW As Integer = 获取图标区域宽度(item)
        Dim hasIcon As Boolean = iconAreaW > 0
        Dim scaledPadding As Padding = Dpi(项内边距)
        Dim scaledIconSize As Size = Dpi(图标尺寸)
        Dim bottomLinesH As Integer = item.CachedBottomLinesHeight
        Dim upperPartH As Integer = item.CachedUpperPartHeight
        If bottomLinesH = 0 AndAlso upperPartH = 0 Then
            Dim rowFullW As Integer = If(_columns.Count > 0, 获取总列宽(), rowRect.Width)
            bottomLinesH = 计算底部文本行高度(item, rowFullW)
            upperPartH = rowRect.Height - scaledPadding.Vertical - bottomLinesH
        End If
        Dim flags As TextFormatFlags = 获取文本格式标志()

        Dim colCount As Integer = If(_columns.Count > 0, _columns.Count, 1)
        For colIdx As Integer = 0 To colCount - 1
            If colIdx >= item.SubItems.Count Then Exit For
            Dim sub_ = item.SubItems(colIdx)
            Dim colX As Integer = _columnXCache(Math.Min(colIdx, _columnXCount - 1))
            Dim colW As Integer = If(_columns.Count > 0, _columns(colIdx).Width, rowRect.Width)

            Dim colFixed As Boolean = (_columns.Count > 0 AndAlso colIdx < _columns.Count AndAlso _columns(colIdx).WordWrapHeightFixed)
            Dim cellFlags As TextFormatFlags = If(colFixed AndAlso 自动换行,
                (flags And Not TextFormatFlags.WordBreak) Or TextFormatFlags.SingleLine,
                flags)

            Dim cellRect As New Rectangle(
                colX + scaledPadding.Left, rowRect.Y + scaledPadding.Top,
                colW - scaledPadding.Horizontal, upperPartH)

            If colIdx = 0 Then
                Dim checkAreaW = 获取复选框区域宽度()
                cellRect = New Rectangle(cellRect.X + checkAreaW, cellRect.Y, cellRect.Width - checkAreaW, cellRect.Height)
            End If

            If colIdx = 0 AndAlso hasIcon AndAlso item.Icon IsNot Nothing Then
                Dim iconX As Integer = cellRect.X
                Dim iconY As Integer = rowRect.Y + scaledPadding.Top + (upperPartH - scaledIconSize.Height) \ 2
                context.DrawImage(item.Icon,
                                  New RectangleF(iconX, iconY, scaledIconSize.Width, scaledIconSize.Height))
                cellRect = New Rectangle(cellRect.X + iconAreaW, cellRect.Y, cellRect.Width - iconAreaW, cellRect.Height)
            End If

            If cellRect.Width <= 0 OrElse cellRect.Height <= 0 Then Continue For
            If skipEditingCell AndAlso colIdx = _editColumnIndex Then Continue For

            Dim contentH As Integer = 计算子项内容高度(sub_, cellRect.Width, cellFlags)
            If colFixed Then contentH = Math.Min(contentH, cellRect.Height)
            Dim startY As Integer = cellRect.Y + Math.Max(0, (cellRect.Height - contentH) \ 2)
            Dim lineY As Integer = startY

            Dim mf As Font = If(sub_.Font, Me.Font)
            Dim mc As Color = If(sub_.ForeColor <> Color.Empty, sub_.ForeColor, 项文本颜色)
            Dim mh As Integer = 测量子项主文本高度缓存(sub_, cellRect.Width, cellFlags)
            If Not String.IsNullOrEmpty(sub_.Text) Then
                Dim remainH As Integer = cellRect.Bottom - lineY
                If remainH > 0 Then
                    绘制可能截断文本_DX12(context, sub_.Text, mf, New Rectangle(cellRect.X, lineY, cellRect.Width, Math.Min(mh, remainH)), mc, cellFlags)
                End If
            End If
            lineY += mh

            For Each line_ In sub_.ExtraLines
                lineY += Dpi(文本行间距)
                Dim lf As Font = If(line_.Font, Me.Font)
                Dim lc As Color = If(line_.ForeColor <> Color.Empty, line_.ForeColor, 项文本颜色)
                Dim lh As Integer = 测量文本行高度缓存(line_, cellRect.Width, cellFlags)
                If Not String.IsNullOrEmpty(line_.Text) Then
                    Dim remainH As Integer = cellRect.Bottom - lineY
                    If remainH > 0 Then
                        绘制可能截断文本_DX12(context, line_.Text, lf, New Rectangle(cellRect.X, lineY, cellRect.Width, Math.Min(lh, remainH)), lc, cellFlags)
                    End If
                End If
                lineY += lh
            Next
        Next

        If item.BottomLines.Count > 0 Then
            Dim bottomRect = 获取底部文本区域(rowRect, upperPartH)
            Dim blW As Integer = bottomRect.Width
            Dim blY As Integer = bottomRect.Y
            For Each bl In item.BottomLines
                blY += Dpi(文本行间距)
                Dim lf As Font = If(bl.Font, Me.Font)
                Dim lc As Color = If(bl.ForeColor <> Color.Empty, bl.ForeColor, 项文本颜色)
                Dim lh As Integer = 测量文本行高度缓存(bl, blW, flags)
                If Not String.IsNullOrEmpty(bl.Text) Then
                    绘制可能截断文本_DX12(context, bl.Text, lf, New Rectangle(bottomRect.X, blY, blW, lh), lc, flags)
                End If
                blY += lh
            Next
        End If
    End Sub

    Private Sub 绘制可能截断文本_DX12(context As D3D12_PaintContext, text As String, font As Font,
                                   rect As Rectangle, color As Color, flags As TextFormatFlags)
        If String.IsNullOrEmpty(text) OrElse rect.Width <= 0 OrElse rect.Height <= 0 OrElse color.A = 0 Then Return
        If Not context.IntersectsDirty(rect) Then Return
        Dim canWrap As Boolean = (flags And TextFormatFlags.WordBreak) = TextFormatFlags.WordBreak AndAlso
                                 (flags And TextFormatFlags.SingleLine) <> TextFormatFlags.SingleLine
        Dim drawText As String = If(canWrap, text, 截断文本到宽度(text, font, rect.Width))
        context.DrawText(drawText, font, color, rect,
                         DpiScale(), Vortice.DirectWrite.TextAlignment.Leading,
                         Vortice.DirectWrite.ParagraphAlignment.Near,
                         wordWrap:=canWrap)
    End Sub

    Private Sub 绘制更多指示器符号_DX12(context As D3D12_PaintContext, rect As Rectangle, isTop As Boolean)
        If rect.Height < 2 Then Return
        Dim symbol As String = If(isTop, "▲", "▼")
        Dim symbolSize As Single = Math.Max(7, Me.Font.Size - 1)
        If _moreSymbolFont Is Nothing OrElse _moreSymbolFontKey <> symbolSize Then
            _moreSymbolFont?.Dispose()
            _moreSymbolFont = New Font(Me.Font.FontFamily, symbolSize, System.Drawing.FontStyle.Regular)
            _moreSymbolFontKey = symbolSize
        End If
        context.DrawText(symbol, _moreSymbolFont, 更多指示器颜色, rect,
                         DpiScale(), Vortice.DirectWrite.TextAlignment.Center,
                         Vortice.DirectWrite.ParagraphAlignment.Center)
    End Sub

    Private Sub 绘制项复选框_DX12(context As D3D12_PaintContext, item As ListItem, group As ListGroup,
                                  rowRect As Rectangle, index As Integer)
        Dim s = DpiScale()
        Dim rect = 获取项复选框区域(item, rowRect.Y)
        Dim clip = 获取复选框裁剪区域(item, rowRect.Y)
        If clip.Width <= 0 OrElse clip.Height <= 0 Then Return
        Dim isHover = Enabled AndAlso index = _hoverRowIndex
        Dim isPressed = isHover AndAlso _pressedCheckItem Is item
        Dim checkedColor = 获取继承勾选颜色(item, group, Function(a) a.CheckBoxCheckedBackColor, 选中时框背景颜色)
        Dim checkedHover = 获取继承勾选颜色(item, group, Function(a) a.CheckBoxHoverCheckedBackColor, 鼠标移上时选中框背景颜色)
        Dim checkedPressed = 获取继承勾选颜色(item, group, Function(a) a.CheckBoxPressedCheckedBackColor, 鼠标按下时选中框背景颜色)
        checkedColor = 获取复选框状态颜色(checkedColor, checkedHover, checkedPressed, isHover, isPressed)
        Dim uncheckedColor = 获取复选框状态颜色(未选中时框背景颜色, 鼠标移上时未选中框背景颜色, 鼠标按下时未选中框背景颜色, isHover, isPressed)
        Dim borderColor = 获取复选框状态颜色(框边框颜色值, 鼠标移上时框边框颜色, 鼠标按下时框边框颜色, isHover, isPressed)
        Dim markColor = 获取继承勾选颜色(item, group, Function(a) a.CheckBoxCheckMarkColor, 勾号颜色值)
        Dim markHover = 获取继承勾选颜色(item, group, Function(a) a.CheckBoxHoverCheckMarkColor, 鼠标移上时勾号颜色)
        Dim markPressed = 获取继承勾选颜色(item, group, Function(a) a.CheckBoxPressedCheckMarkColor, 鼠标按下时勾号颜色)
        markColor = 获取复选框状态颜色(markColor, markHover, markPressed, isHover, isPressed)
        Dim animation As D3D_AnimationHelper = Nothing
        Dim progress = If(item.Checked, 1.0F, 0.0F)
        If _checkAnimations.TryGetValue(item, animation) Then progress = animation.Progress
        Using context.PushClip(clip)
            context.FillRoundedRectangle(rect, 框圆角半径 * s, 颜色插值(uncheckedColor, checkedColor, progress))
            context.StrokeRoundedRectangle(rect, 框圆角半径 * s, borderColor, 框边框宽度 * s)
            If progress > 0.001F Then 绘制复选框勾号_DX12(context, rect, progress, s, markColor)
            If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
                Dim offset = Math.Max(0, 框边框宽度) * s / 2.0F
                rect.Inflate(offset, offset)
                context.FillRoundedRectangle(rect, 框圆角半径 * s + offset, 禁用时遮罩颜色)
            End If
        End Using
    End Sub

    Private Sub 绘制复选框勾号_DX12(context As D3D12_PaintContext, 框区域 As RectangleF, progress As Single, s As Single, 当前勾号色 As Color)
        If 当前勾号色.A <= 0 Then Return

        Dim 内边距 As Single = 框内边距 * s + 框边框宽度 * s / 2.0F
        Dim x1 As Single = 框区域.X + 内边距
        Dim y1 As Single = 框区域.Y + 框区域.Height * 0.5F
        Dim x2 As Single = 框区域.X + 框区域.Width * 0.4F
        Dim y2 As Single = 框区域.Bottom - 内边距
        Dim x3 As Single = 框区域.Right - 内边距
        Dim y3 As Single = 框区域.Y + 内边距
        Dim 段1长 As Single = CSng(Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1)))
        Dim 段2长 As Single = CSng(Math.Sqrt((x3 - x2) * (x3 - x2) + (y3 - y2) * (y3 - y2)))
        Dim 总长度 As Single = 段1长 + 段2长
        If 总长度 < 0.01F Then Return

        Dim 可见长度 As Single = 总长度 * progress
        Dim strokeWidth = 勾号线宽 * s
        If 可见长度 <= 段1长 Then
            Dim t As Single = If(段1长 > 0, 可见长度 / 段1长, 0F)
            context.DrawRoundLine(
                New Vector2(x1, y1),
                New Vector2(x1 + (x2 - x1) * t, y1 + (y2 - y1) * t),
                当前勾号色, strokeWidth)
        Else
            context.DrawRoundLine(New Vector2(x1, y1), New Vector2(x2, y2), 当前勾号色, strokeWidth)
            Dim 剩余 As Single = 可见长度 - 段1长
            Dim t As Single = If(段2长 > 0, 剩余 / 段2长, 0F)
            context.DrawRoundLine(
                New Vector2(x2, y2),
                New Vector2(x2 + (x3 - x2) * t, y2 + (y3 - y2) * t),
                当前勾号色, strokeWidth)
        End If
    End Sub
End Class
