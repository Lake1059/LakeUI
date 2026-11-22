Imports System.Numerics
Imports Vortice.DirectWrite
Partial Public Class ModernListBox
    Implements ID3D12Renderable
    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If Width < 1 OrElse Height < 1 Then Return

        Dim w As Integer = ClientRectangle.Width
        Dim h As Integer = ClientRectangle.Height
        Dim hasRadius As Boolean = 边框圆角半径 > 0
        Dim sourceRect As New RectangleF(0, 0, w, h)
        Dim boundsRect As New RectangleF(0, 0, w, h)
        Dim s As Single = DpiScale()
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * s / 2.0F
            boundsRect.Inflate(-half, -half)
        End If
        Dim bc As Color = If(Focused, 有焦点时边框颜色, 边框颜色)

        Dim effBg As Color = 背景颜色

        预计算滚动条布局()

        DrawBackground_DX12(context, hasRadius, sourceRect, boundsRect, bc, effBg)

        Using context.PushClip(获取内容裁剪矩形())
            绘制全部项背景与图标_DX12(context)
            绘制拖选框_DX12(context)
            绘制拖动排序指示线_DX12(context)
            绘制滚动条_DX12(context)
            绘制全部项文本_DX12(context)
        End Using

        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            context.FillRoundedRectangle(boundsRect, If(hasRadius, 边框圆角半径 * s, 0.0F), 禁用时遮罩颜色)
        End If
    End Sub

    Private Sub DrawBackground_DX12(context As D3D12_PaintContext, hasRadius As Boolean, sourceRect As RectangleF, boundsRect As RectangleF, borderClr As Color, bgClr As Color)
        Dim s As Single = DpiScale()
        Dim radius As Single = If(hasRadius, 边框圆角半径 * s, 0.0F)
        If _backgroundSource IsNot Nothing Then context.DrawBackgroundSource(_backgroundSource, Rectangle.Round(sourceRect))
        If _backgroundSource Is Nothing AndAlso MyBase.BackColor.A > 0 Then context.FillRectangle(sourceRect, MyBase.BackColor)
        context.FillRoundedRectangle(boundsRect, radius, bgClr)
        context.StrokeRoundedRectangle(boundsRect, radius, borderClr, 边框宽度 * s)
    End Sub

    Private Sub 绘制滚动条_DX12(context As D3D12_PaintContext)
        If _scrollBar.TrackRect.IsEmpty Then Return
        Dim s As Single = DpiScale()
        Dim width As Single = Math.Max(1.0F, 滚动条宽度 * s)
        Dim trackArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.TrackRect.Y, width, _scrollBar.TrackRect.Height)
        Dim thumbArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.ThumbRect.Y, width, _scrollBar.ThumbRect.Height)
        context.FillRoundedRectangle(trackArea, Math.Min(width / 2.0F, trackArea.Height / 2.0F), 滚动条轨道颜色)
        Dim thumbColor = If(_scrollBar.IsDragging OrElse _scrollBar.IsHover, 滚动条悬停颜色, 滚动条颜色)
        context.FillRoundedRectangle(thumbArea, Math.Min(width / 2.0F, thumbArea.Height / 2.0F), thumbColor)
    End Sub

    Private Sub 绘制全部项背景与图标_DX12(context As D3D12_PaintContext)
        If _items.Count = 0 Then Return
        Dim contentRect = 获取内容区域()
        If contentRect.Height <= 0 OrElse contentRect.Width <= 0 Then Return

        Dim s As Single = DpiScale()
        Dim scaledH As Integer = CInt(Math.Round(行高 * s))
        Dim scaledPadL As Integer = CInt(Math.Round(项左内边距 * s))
        Dim scaledCbLeft As Integer = CInt(Math.Round(复选框左边距 * s))
        Dim scaledIconW As Integer = CInt(Math.Round(图标尺寸.Width * s))
        Dim scaledIconH As Integer = CInt(Math.Round(图标尺寸.Height * s))

        Dim inset As Integer = 获取边框内边距()
        Dim scrollW As Integer = If(Not _scrollBar.TrackRect.IsEmpty, Width - inset - _scrollBar.VisualLeft, 0)
        Dim availW As Integer = contentRect.Width - scrollW

        Dim animatingSingleSelection As Boolean =
            _selectionAnimActive AndAlso _selectedIndices.Count = 1 AndAlso _selectionAnimTargetIndex >= 0

        If animatingSingleSelection Then
            context.FillRectangle(New RectangleF(contentRect.X, _selectionAnimCurrentY, availW, _selectionAnimCurrentH), 项选中颜色)
        End If

        If _hoverAnimActive AndAlso _hoverIndex >= 0 AndAlso Not _selectedIndices.Contains(_hoverIndex) Then
            context.FillRectangle(New RectangleF(contentRect.X, _hoverAnimCurrentY, availW, _hoverAnimCurrentH), 项悬停颜色)
        End If

        Dim visCount As Integer = 估算可见行数()
        For i As Integer = 0 To visCount - 1
            Dim idx As Integer = i + _scrollOffset
            If idx >= _items.Count Then Exit For
            Dim itemY As Integer = contentRect.Y + i * scaledH
            If itemY + scaledH > contentRect.Bottom Then Exit For

            Dim itemRect As New RectangleF(contentRect.X, itemY, availW, scaledH)
            If _selectedIndices.Contains(idx) Then
                If Not (animatingSingleSelection AndAlso idx = _selectionAnimTargetIndex) Then
                    context.FillRectangle(itemRect, 项选中颜色)
                End If
            ElseIf idx = _hoverIndex AndAlso Not _hoverAnimActive Then
                context.FillRectangle(itemRect, 项悬停颜色)
            End If

            Dim itemText As String = _items(idx)
            Dim textX As Integer = contentRect.X + scaledPadL

            If 显示复选框 Then
                Dim cbX As Integer = contentRect.X + scaledCbLeft
                Dim cbY As Integer = itemY + (scaledH - CInt(Math.Round(复选框大小 * s))) \ 2
                绘制复选框_DX12(context, cbX, cbY, 获取复选状态(idx))
                textX = contentRect.X + 获取复选框区域宽度()
            End If

            Dim icon As Image = Nothing
            If _itemIcons.TryGetIcon(itemText, icon) AndAlso icon IsNot Nothing AndAlso scaledIconW > 0 AndAlso scaledIconH > 0 Then
                Dim iconX As Integer = textX
                Dim iconY As Integer = itemY + (scaledH - scaledIconH) \ 2
                context.DrawImage(icon, New RectangleF(iconX, iconY, scaledIconW, scaledIconH))
            End If
        Next
    End Sub

    Private Sub 绘制全部项文本_DX12(context As D3D12_PaintContext)
        If _items.Count = 0 Then Return
        Dim contentRect = 获取内容区域()
        If contentRect.Height <= 0 OrElse contentRect.Width <= 0 Then Return

        Dim s As Single = DpiScale()
        Dim scaledH As Integer = CInt(Math.Round(行高 * s))
        Dim scaledPadL As Integer = CInt(Math.Round(项左内边距 * s))
        Dim scaledIconW As Integer = CInt(Math.Round(图标尺寸.Width * s))
        Dim scaledIconMR As Integer = CInt(Math.Round(图标右边距 * s))
        Dim inset As Integer = 获取边框内边距()
        Dim scrollW As Integer = If(Not _scrollBar.TrackRect.IsEmpty, Width - inset - _scrollBar.VisualLeft, 0)
        Dim availW As Integer = contentRect.Width - scrollW

        Dim visCount As Integer = 估算可见行数()
        For i As Integer = 0 To visCount - 1
            Dim idx As Integer = i + _scrollOffset
            If idx >= _items.Count Then Exit For
            Dim itemY As Integer = contentRect.Y + i * scaledH
            If itemY + scaledH > contentRect.Bottom Then Exit For

            Dim itemText As String = _items(idx)
            Dim textX As Integer = contentRect.X + scaledPadL
            If 显示复选框 Then textX = contentRect.X + 获取复选框区域宽度()

            Dim icon As Image = Nothing
            If _itemIcons.TryGetIcon(itemText, icon) AndAlso icon IsNot Nothing Then
                textX += scaledIconW + scaledIconMR
            End If

            Dim textRight As Integer = contentRect.X + availW - scaledPadL
            Dim textWidth As Integer = textRight - textX
            If textWidth > 0 Then
                Dim colorArgs As New ItemForeColorEventArgs(idx, itemText, ForeColor)
                RaiseEvent ItemForeColorNeeded(Me, colorArgs)
                context.DrawText(itemText, Font, If(colorArgs.ForeColor.IsEmpty, ForeColor, colorArgs.ForeColor), New RectangleF(textX, itemY, textWidth, scaledH), DpiScale(), TextAlignment.Leading, ParagraphAlignment.Center)
            End If
        Next
    End Sub

    Private Sub 绘制复选框_DX12(context As D3D12_PaintContext, x As Integer, y As Integer, state As CheckStateEnum)
        Dim s As Single = DpiScale()
        Dim scaledSize As Single = 复选框大小 * s
        Dim scaledRadius As Single = 复选框圆角半径 * s
        Dim scaledBW As Single = 复选框边框宽度 * s
        Dim scaledMarkW As Single = 复选框标记线宽 * s
        Dim rect As New RectangleF(x, y, scaledSize, scaledSize)

        context.FillRoundedRectangle(rect, scaledRadius, 复选框背景颜色)
        context.StrokeRoundedRectangle(rect, scaledRadius, 复选框边框颜色, scaledBW)

        Dim inset As Single = scaledSize * 0.2F
        Select Case state
            Case CheckStateEnum.Checked
                Dim br = 复选框勾选颜色
                If br.A > 0 Then
                    Dim p1 As New Vector2(x + inset, y + scaledSize * 0.5F)
                    Dim p2 As New Vector2(x + scaledSize * 0.4F, y + scaledSize - inset)
                    Dim p3 As New Vector2(x + scaledSize - inset, y + inset)
                    context.DrawJoinedRoundLines(p1, p2, p3, br, scaledMarkW)
                End If
            Case CheckStateEnum.Crossed
                Dim br = 复选框叉选颜色
                If br.A > 0 Then
                    context.DrawLine(New Vector2(x + inset, y + inset), New Vector2(x + scaledSize - inset, y + scaledSize - inset), br, scaledMarkW)
                    context.DrawLine(New Vector2(x + scaledSize - inset, y + inset), New Vector2(x + inset, y + scaledSize - inset), br, scaledMarkW)
                End If
        End Select
    End Sub

    Private Sub 绘制拖选框_DX12(context As D3D12_PaintContext)
        If Not _isDragSelecting Then Return
        Dim rect As Rectangle = 获取拖选矩形()
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim rectF As New RectangleF(rect.X, rect.Y, rect.Width, rect.Height)
        context.FillRectangle(rectF, 选框填充颜色)
        context.DrawRectangle(rectF, 选框边框颜色, 1.0F)
    End Sub

    Private Sub 绘制拖动排序指示线_DX12(context As D3D12_PaintContext)
        If Not _isDragReordering OrElse _dragReorderInsertIndex < 0 Then Return
        Dim contentRect = 获取内容区域()
        Dim scaledH As Integer = CInt(Math.Round(行高 * DpiScale()))
        Dim lineY As Integer
        If _dragReorderInsertIndex >= _items.Count Then
            lineY = contentRect.Y + Math.Min(_items.Count - _scrollOffset, 估算可见行数()) * scaledH
        Else
            lineY = 获取项Y坐标(_dragReorderInsertIndex)
            If lineY < 0 Then Return
        End If
        Dim br = 拖动排序指示线颜色
        If br.A > 0 Then context.DrawLine(New Vector2(contentRect.X, lineY), New Vector2(contentRect.Right, lineY), br, 拖动排序指示线宽 * DpiScale())
    End Sub
End Class
