Partial Public Class BreadcrumbNavigationBar
    Implements ID3D12Renderable
    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        RebuildLayout()
        Dim w As Integer = ClientRectangle.Width
        Dim h As Integer = ClientRectangle.Height
        If w < 1 OrElse h < 1 Then Return

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, w, h), MyBase.BackColor)
        End If

        DrawNodesShapes_DX12(context)
        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, w, h), 禁用时遮罩颜色)
        End If
        DrawNodesText_DX12(context)
    End Sub

    Private Sub DrawNodesShapes_DX12(context As D3D12_PaintContext)
        Dim s As Single = DpiScale()
        Dim radius As Single = 节点圆角半径 * s

        For Each l In _layoutCache
            Dim isSelected As Boolean = (Not l.IsOverflow) AndAlso (l.Index = _selectedIndex) AndAlso l.Index >= 0

            Dim textHover As Boolean = (_hoverNodeIndex = l.Index AndAlso Not _hoverIsArrow)
            Dim textPressed As Boolean = (_pressedNodeIndex = l.Index AndAlso Not _pressedIsArrow)
            Dim textHl As Color = Color.Empty
            If textPressed Then
                textHl = 节点按下背景
            ElseIf textHover Then
                textHl = 节点悬停背景
            ElseIf isSelected Then
                textHl = 选中节点背景
            End If
            If textHl <> Color.Empty AndAlso textHl.A > 0 Then context.FillRoundedRectangle(l.TextRect, radius, textHl)

            If l.HasArrow Then
                Dim arrowHover As Boolean = (_hoverNodeIndex = l.Index AndAlso _hoverIsArrow)
                Dim arrowPressed As Boolean = (_pressedNodeIndex = l.Index AndAlso _pressedIsArrow) OrElse (_dropDownArrowIndex = l.Index)
                Dim arrHl As Color = Color.Empty
                If arrowPressed Then
                    arrHl = 节点按下背景
                ElseIf arrowHover Then
                    arrHl = 节点悬停背景
                End If
                If arrHl <> Color.Empty AndAlso arrHl.A > 0 Then context.FillRoundedRectangle(l.ArrowRect, radius, arrHl)

                Dim arrowClr As Color = 箭头颜色
                If (_hoverNodeIndex = l.Index AndAlso _hoverIsArrow) OrElse (_dropDownArrowIndex = l.Index) Then arrowClr = 箭头悬停颜色
                DrawChevron_DX12(context, l.ArrowRect, arrowClr, _dropDownArrowIndex = l.Index)
            End If
        Next
    End Sub

    Private Sub DrawNodesText_DX12(context As D3D12_PaintContext)
        Dim s As Single = DpiScale()
        Dim padX As Integer = CInt(节点文本左右内边距 * s)
        Dim iconSize As Integer = CInt(图标大小 * s)
        Dim iconSpacing As Integer = CInt(图标与文本间距 * s)
        For Each l In _layoutCache
            Dim item As BreadcrumbItem
            If l.IsOverflow Then
                item = _overflowItem
            Else
                If l.Index < 0 OrElse l.Index >= _items.Count Then Continue For
                item = _items(l.Index)
            End If
            If item Is Nothing Then Continue For

            Dim isSelected As Boolean = (Not l.IsOverflow) AndAlso (l.Index = _selectedIndex)
            Dim textColor As Color = If(isSelected, 选中节点文本颜色, ForeColor)

            If l.HasIcon AndAlso item.Image IsNot Nothing Then
                context.DrawImage(item.Image, New RectangleF(l.IconRect.X, l.IconRect.Y, l.IconRect.Width, l.IconRect.Height))
            End If

            Dim textRect As Rectangle = l.TextRect
            Dim align As Vortice.DirectWrite.TextAlignment
            If l.HasIcon Then
                Dim shift As Integer = iconSize + iconSpacing
                textRect = New Rectangle(textRect.X + padX + shift, textRect.Y, textRect.Width - padX - shift, textRect.Height)
                align = Vortice.DirectWrite.TextAlignment.Leading
            Else
                align = Vortice.DirectWrite.TextAlignment.Center
            End If
            If textRect.Width > 0 AndAlso textRect.Height > 0 Then
                context.DrawText(item.Text, Font, textColor, New RectangleF(textRect.X, textRect.Y, textRect.Width, textRect.Height), DpiScale(), align, Vortice.DirectWrite.ParagraphAlignment.Center)
            End If
        Next
    End Sub

    Private Sub DrawChevron_DX12(context As D3D12_PaintContext, area As Rectangle, c As Color, pointDown As Boolean)
        If c.A = 0 Then Return
        Dim s As Single = DpiScale()
        Dim sz As Single = 箭头大小 * s
        Dim cx As Single = area.X + area.Width / 2.0F
        Dim cy As Single = area.Y + area.Height / 2.0F
        Dim half As Single = sz / 2.0F
        Dim hh As Single = half * 0.55F
        Dim p1, p2, p3 As System.Numerics.Vector2
        If pointDown Then
            p1 = New System.Numerics.Vector2(cx - half, cy - hh)
            p2 = New System.Numerics.Vector2(cx, cy + hh)
            p3 = New System.Numerics.Vector2(cx + half, cy - hh)
        Else
            p1 = New System.Numerics.Vector2(cx - hh, cy - half)
            p2 = New System.Numerics.Vector2(cx + hh, cy)
            p3 = New System.Numerics.Vector2(cx - hh, cy + half)
        End If
        Dim strokeWidth = 箭头线宽 * s
        context.DrawRoundLine(p1, p2, c, strokeWidth)
        context.DrawRoundLine(p2, p3, c, strokeWidth)
    End Sub
End Class
