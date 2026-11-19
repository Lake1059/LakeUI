Imports System.Numerics
Partial Public Class ModernComboBox
    Implements ID3D12Renderable
    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing Then Return
        Dim w As Integer = ClientRectangle.Width
        Dim h As Integer = ClientRectangle.Height
        If w <= 0 OrElse h <= 0 Then Return
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
        Dim effBg2 As Color = 背景渐变颜色

        If Not 启用编辑 Then
            Select Case 鼠标状态
                Case MouseStateEnum.Hover
                    If 鼠标移上时背景颜色 <> Color.Empty Then effBg = 鼠标移上时背景颜色
                    If 鼠标移上时渐变颜色 <> Color.Empty Then effBg2 = 鼠标移上时渐变颜色
                    If 鼠标移上时边框颜色 <> Color.Empty Then bc = 鼠标移上时边框颜色
                Case MouseStateEnum.Pressed
                    If 鼠标按下时背景颜色 <> Color.Empty Then effBg = 鼠标按下时背景颜色
                    If 鼠标按下时渐变颜色 <> Color.Empty Then effBg2 = 鼠标按下时渐变颜色
                    If 鼠标按下时边框颜色 <> Color.Empty Then bc = 鼠标按下时边框颜色
            End Select
        End If

        绘制背景_DX12(context, hasRadius, sourceRect, boundsRect, effBg, effBg2)
        SyncTextRenderer()
        If Not RenderCustomSelectionContentGpu12(context, New RectangleF(0, 0, w, h)) Then _textRenderer.DrawGpu12(context)
        绘制前置文本_DX12(context)
        绘制分隔线与箭头_DX12(context, w, h, bc)
        绘制边框_DX12(context, hasRadius, boundsRect, bc)
        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            context.FillShape(boundsRect, If(hasRadius, 边框圆角半径 * s, 0.0F), 禁用时遮罩颜色)
        End If
    End Sub

    Protected Overridable Function RenderCustomSelectionContentGpu12(context As D3D12_PaintContext, bounds As RectangleF) As Boolean
        Return False
    End Function

    Protected Friend Overridable Function RenderCustomDropDownItemGpu12(context As D3D12_PaintContext, itemIndex As Integer, itemRect As RectangleF, textColor As Color) As Boolean
        Return False
    End Function

    Private Sub 绘制背景_DX12(context As D3D12_PaintContext, hasRadius As Boolean, sourceRect As RectangleF, boundsRect As RectangleF, bgClr As Color, bgClr2 As Color)
        Dim backColorMask As Color = MyBase.BackColor
        Dim hasMask As Boolean = _backgroundSource Is Nothing AndAlso backColorMask.A > 0 AndAlso backColorMask.A < 255
        Dim hasFill As Boolean = bgClr.A > 0 OrElse (bgClr2 <> Color.Empty AndAlso bgClr2.A > 0)
        Dim hasBackgroundSource As Boolean = _backgroundSource IsNot Nothing
        Dim arrowBgClr As Color = Color.Empty
        Dim arrowBgClr2 As Color = Color.Empty
        Dim hasArrowFill As Boolean = 获取箭头区域背景颜色(arrowBgClr, arrowBgClr2)
        If Not hasBackgroundSource AndAlso Not hasMask AndAlso Not hasFill AndAlso Not hasArrowFill Then Return

        Dim s As Single = DpiScale()
        Dim fillRect As RectangleF = boundsRect
        Dim radius As Single = If(hasRadius, 边框圆角半径 * s, 0.0F)
        If hasBackgroundSource Then context.DrawBackgroundSource(_backgroundSource, Rectangle.Round(sourceRect))
        If hasMask Then context.FillShape(fillRect, radius, backColorMask)
        If hasFill Then context.FillShape(fillRect, radius, bgClr, bgClr2, 渐变方向)

        If hasArrowFill Then
            Using context.PushClip(获取箭头区域背景矩形(fillRect))
                context.FillShape(fillRect, radius, arrowBgClr, arrowBgClr2, 渐变方向)
            End Using
        End If
    End Sub

    Private Sub 绘制边框_DX12(context As D3D12_PaintContext, hasRadius As Boolean, boundsRect As RectangleF, borderClr As Color)
        If 边框宽度 <= 0 OrElse borderClr.A = 0 Then Return
        Dim s As Single = DpiScale()
        context.StrokeRoundedRectangle(boundsRect, If(hasRadius, 边框圆角半径 * s, 0.0F), borderClr, 边框宽度 * s)
    End Sub

    Private Sub 绘制分隔线与箭头_DX12(context As D3D12_PaintContext, w As Integer, h As Integer, borderClr As Color)
        Dim s As Single = DpiScale()
        Dim aaw As Integer = ArrowAreaWidth
        Dim bi As Integer = CInt(边框宽度 * s)
        Dim sepX As Single = w - aaw - If(bi > 0, bi / 2.0F, 0)
        Dim topInset As Integer = Math.Max(Padding.Top, bi)
        Dim bottomInset As Integer = Math.Max(Padding.Bottom, bi)

        If 显示分隔线 AndAlso 边框宽度 > 0 Then
            context.DrawLine(New Vector2(sepX, topInset), New Vector2(sepX, h - bottomInset), borderClr, 边框宽度 * s)
        End If

        Dim effArrowClr As Color = 箭头颜色
        Dim isArrowActive As Boolean = Not 启用编辑 OrElse _mouseOverArrow
        If isArrowActive Then
            Select Case 鼠标状态
                Case MouseStateEnum.Hover
                    If 鼠标移上时箭头颜色 <> Color.Empty Then effArrowClr = 鼠标移上时箭头颜色
                Case MouseStateEnum.Pressed
                    If 鼠标按下时箭头颜色 <> Color.Empty Then effArrowClr = 鼠标按下时箭头颜色
            End Select
        End If
        If effArrowClr.A = 0 Then Return

        Dim squareLeft As Single = w - aaw
        Dim centerX As Single = squareLeft + aaw / 2.0F
        Dim centerY As Single = h / 2.0F
        Dim width = 箭头大小 * s
        Dim height = CSng(width * Math.Sqrt(3) / 2)
        context.FillRoundedTriangle({New Vector2(centerX - width / 2, centerY - height / 2), New Vector2(centerX + width / 2, centerY - height / 2),
                                     New Vector2(centerX, centerY + height / 2)}, Math.Max(width * 0.2F, 1), effArrowClr)
    End Sub

    Private Sub 绘制前置文本_DX12(context As D3D12_PaintContext)
        If context Is Nothing OrElse String.IsNullOrEmpty(前置文本) OrElse 前置文本颜色.A = 0 Then Return
        Dim area As RectangleF = 获取未偏移文本区域()
        If area.Width <= 0 OrElse area.Height <= 0 Then Return
        Using context.PushClip(area)
            context.DrawText(前置文本, Font, 前置文本颜色, area,
                             DpiScale(), Vortice.DirectWrite.TextAlignment.Leading,
                             Vortice.DirectWrite.ParagraphAlignment.Center)
        End Using
    End Sub
    Partial Private Class DropDownListForm
        Implements ID3D12Renderable
        Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
            RenderDropDown_DX12(context)
        End Sub

        Private Sub RenderDropDown_DX12(context As D3D12_PaintContext)
            Dim w As Integer = ClientSize.Width
            Dim h As Integer = ClientSize.Height
            If w <= 0 OrElse h <= 0 Then Return

            Dim s As Single = _owner.DpiScale()
            Dim bw As Integer = CInt(_owner.下拉边框宽度 * s)

            If _scrollBarVisible Then
                _scrollBar.ComputeLayout(w, h, bw, 0,
                    _owner.下拉内边距.Top, _owner.下拉内边距.Bottom,
                    _owner.下拉滚动条宽度,
                    _owner._items.Count, Math.Min(_owner._items.Count, _owner.最大下拉项数), _scrollOffset)
            End If

            Dim hasBackdrop = DrawDropDownBackdrop_DX12(context, w, h)
            DrawDropDownBackground_DX12(context, DropDownFillColor(), bw, w, h, Not hasBackdrop)
            DrawDropDownItems_DX12(context, w, h, New DropDownLayout(Me, w))
            DrawDropDownScrollBar_DX12(context, w, h, bw)
        End Sub

        Private Function DrawDropDownBackdrop_DX12(context As D3D12_PaintContext, w As Integer, h As Integer) As Boolean
            If _backdrop Is Nothing OrElse w <= 0 OrElse h <= 0 Then Return False
            Return _backdrop.Draw12(context, New RectangleF(0, 0, w, h))
        End Function

        Private Sub DrawDropDownBackground_DX12(context As D3D12_PaintContext, backColor As Color, bw As Integer, w As Integer, h As Integer, fillBackground As Boolean)
            Dim bounds As New RectangleF(0, 0, w, h)
            Dim radius = 获取下拉窗口圆角半径(w, h)
            If fillBackground Then context.FillRoundedRectangle(bounds, radius, backColor)
            If bw > 0 AndAlso _owner.下拉边框颜色.A > 0 Then DrawDropDownBorder_DX12(context, w, h, bw, radius)
        End Sub

        Private Sub DrawDropDownBorder_DX12(context As D3D12_PaintContext, w As Integer, h As Integer, bw As Integer, radius As Single)
            Dim border As Integer = Math.Min(bw, Math.Min(w, h))
            If border <= 0 Then Return
            Dim half As Single = border / 2.0F
            context.StrokeRoundedRectangle(New RectangleF(half, half, w - border, h - border), Math.Max(0, radius - half), _owner.下拉边框颜色, border)
        End Sub

        Private Sub 绘制悬停焦点_DX12(context As D3D12_PaintContext, w As Integer, layout As DropDownLayout)
            Dim highlightColor As Color = 获取悬停焦点颜色()
            If highlightColor.A = 0 Then Return

            Dim highlightRect As RectangleF = 获取悬停焦点矩形(w, layout)
            If highlightRect.Width <= 0 OrElse highlightRect.Height <= 0 Then Return

            context.FillRoundedRectangle(highlightRect, 获取悬停焦点圆角(highlightRect), highlightColor)
        End Sub

        Private Sub DrawDropDownItems_DX12(context As D3D12_PaintContext, w As Integer, h As Integer, layout As DropDownLayout)
            Dim clipRect As RectangleF = layout.ClipRect(w, h)
            If clipRect.Width <= 0 OrElse clipRect.Height <= 0 Then Return
            Using context.PushClip(clipRect)
                For i As Integer = 0 To layout.VisCount - 1
                    Dim idx As Integer = i + _scrollOffset
                    If idx >= _owner._items.Count Then Exit For
                    Dim itemRect As RectangleF = layout.ItemRect(w, i)

                    If idx = _owner._selectedIndex AndAlso _owner.下拉选中颜色.A > 0 Then
                        context.FillRectangle(itemRect, _owner.下拉选中颜色)
                    End If
                Next

                绘制悬停焦点_DX12(context, w, layout)

                For i As Integer = 0 To layout.VisCount - 1
                    Dim idx As Integer = i + _scrollOffset
                    If idx >= _owner._items.Count Then Exit For
                    Dim itemRect As RectangleF = layout.ItemRect(w, i)
                    Dim textColor As Color = If(idx = _owner._selectedIndex AndAlso _owner.下拉选中文字颜色 <> Color.Empty,
                        _owner.下拉选中文字颜色,
                        _owner.ForeColor)
                    If Not _owner.RenderCustomDropDownItemGpu12(context, idx, itemRect, textColor) Then
                        context.DrawText(_owner._items(idx), _owner.Font, textColor, layout.TextRect(itemRect), _owner.DpiScale(),
                                         Vortice.DirectWrite.TextAlignment.Leading,
                                         Vortice.DirectWrite.ParagraphAlignment.Center)
                    End If
                Next
            End Using
        End Sub

        Private Sub DrawDropDownScrollBar_DX12(context As D3D12_PaintContext, w As Integer, h As Integer, bw As Integer)
            If Not _scrollBarVisible OrElse _scrollBar.TrackRect.IsEmpty Then Return
            Dim width As Single = Math.Max(1.0F, _owner.下拉滚动条宽度)
            Dim trackArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.TrackRect.Y, width, _scrollBar.TrackRect.Height)
            Dim thumbArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.ThumbRect.Y, width, _scrollBar.ThumbRect.Height)
            context.FillRoundedRectangle(trackArea, Math.Min(width / 2.0F, trackArea.Height / 2.0F), _owner.下拉滚动条轨道颜色)
            Dim thumbColor = If(_scrollBar.IsDragging OrElse _scrollBar.IsHover, _owner.下拉滚动条悬停颜色, _owner.下拉滚动条颜色)
            context.FillRoundedRectangle(thumbArea, Math.Min(width / 2.0F, thumbArea.Height / 2.0F), thumbColor)
        End Sub
    End Class
End Class
