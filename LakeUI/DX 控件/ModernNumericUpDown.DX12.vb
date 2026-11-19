Imports System.Numerics
Partial Public Class ModernNumericUpDown
    Implements ID3D12Renderable
    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        Dim w As Integer = ClientRectangle.Width
        Dim h As Integer = ClientRectangle.Height
        If w <= 0 OrElse h <= 0 Then Return

        Dim hasRadius As Boolean = 边框圆角半径 > 0
        Dim sourceRect As New RectangleF(0, 0, w, h)
        Dim boundsRect As New RectangleF(0, 0, w, h)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * DpiScale() / 2.0F
            boundsRect.Inflate(-half, -half)
        End If

        Dim bc As Color = If(Focused, 有焦点时边框颜色, 边框颜色)
        Dim effBg As Color = 背景颜色
        Dim effBg2 As Color = 背景渐变颜色
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

        绘制背景_DX12(context, hasRadius, sourceRect, boundsRect, effBg, effBg2)
        SyncTextRendererLayout()
        _textRenderer.DrawGpu12(context)
        绘制按钮_DX12(context, w, h)
        绘制边框_DX12(context, hasRadius, boundsRect, bc)

        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            context.FillRoundedRectangle(boundsRect, If(hasRadius, 边框圆角半径 * DpiScale(), 0.0F), 禁用时遮罩颜色)
        End If
    End Sub

    Private Sub 绘制背景_DX12(context As D3D12_PaintContext, hasRadius As Boolean, sourceRect As RectangleF, boundsRect As RectangleF, bgClr As Color, bgClr2 As Color)
        Dim backColorMask As Color = MyBase.BackColor
        Dim hasMask As Boolean = _backgroundSource Is Nothing AndAlso backColorMask.A > 0 AndAlso backColorMask.A < 255
        Dim fillColor As Color = If(bgClr.A > 0, bgClr, bgClr2)
        Dim hasFill As Boolean = fillColor.A > 0
        Dim hasBackgroundSource As Boolean = _backgroundSource IsNot Nothing
        If Not hasBackgroundSource AndAlso Not hasMask AndAlso Not hasFill Then Return
        Dim s As Single = DpiScale()
        Dim fillRect As RectangleF = boundsRect
        Dim radius As Single = If(hasRadius, 边框圆角半径 * s, 0.0F)
        If hasBackgroundSource Then context.DrawBackgroundSource(_backgroundSource, Rectangle.Round(sourceRect))
        If hasMask Then context.FillRoundedRectangle(fillRect, radius, backColorMask)
        If hasFill Then context.FillRoundedRectangle(fillRect, radius, fillColor)
    End Sub

    Private Sub 绘制边框_DX12(context As D3D12_PaintContext, hasRadius As Boolean, boundsRect As RectangleF, borderClr As Color)
        If 边框宽度 <= 0 OrElse borderClr.A = 0 Then Return
        Dim s As Single = DpiScale()
        context.StrokeRoundedRectangle(boundsRect, If(hasRadius, 边框圆角半径 * s, 0.0F), borderClr, 边框宽度 * s)
    End Sub

    Private Sub 绘制按钮_DX12(context As D3D12_PaintContext, w As Integer, h As Integer)
        Dim s As Single = DpiScale()
        Dim aaw As Integer = ActualButtonAreaWidth
        If aaw <= 0 Then Return
        Dim bi As Integer = CInt(边框宽度 * s)
        Dim sepX As Single = w - aaw - If(bi > 0, bi / 2.0F, 0)
        Dim topInset As Integer = Math.Max(Padding.Top, bi)
        Dim bottomInset As Integer = Math.Max(Padding.Bottom, bi)
        Dim rightInset As Integer = bi
        Dim buttonLeft As Single = sepX + If(bi > 0, bi / 2.0F, 0)
        Dim buttonWidth As Single = w - buttonLeft - rightInset
        Dim buttonHeight As Single = h - topInset - bottomInset
        If buttonWidth <= 0 OrElse buttonHeight <= 0 Then Return

        Dim upRect As New RectangleF(buttonLeft, topInset, buttonWidth, buttonHeight / 2.0F)
        Dim downRect As New RectangleF(buttonLeft, topInset + buttonHeight / 2.0F, buttonWidth, buttonHeight / 2.0F)
        绘制按钮背景_DX12(context, upRect, SpinButtonPart.Up)
        绘制按钮背景_DX12(context, downRect, SpinButtonPart.Down)

        If 分隔线宽度 > 0 AndAlso 分隔线颜色.A > 0 Then
            If 分隔线颜色.A > 0 Then
                Dim lineWidth As Single = 分隔线宽度 * s
                context.DrawLine(New Vector2(sepX, topInset), New Vector2(sepX, h - bottomInset), 分隔线颜色, lineWidth)
                Dim midY As Single = topInset + buttonHeight / 2.0F
                context.DrawLine(New Vector2(buttonLeft, midY), New Vector2(w - rightInset, midY), 分隔线颜色, lineWidth)
            End If
        End If

        Dim upArrowColor As Color = 获取箭头颜色(SpinButtonPart.Up)
        Dim downArrowColor As Color = 获取箭头颜色(SpinButtonPart.Down)
        Dim centerX As Single = buttonLeft + buttonWidth / 2.0F
        If upArrowColor.A > 0 Then
            绘制圆角三角形_DX12(context, New PointF(centerX, upRect.Y + upRect.Height / 2.0F), True, upArrowColor)
        End If
        If downArrowColor.A > 0 Then
            绘制圆角三角形_DX12(context, New PointF(centerX, downRect.Y + downRect.Height / 2.0F), False, downArrowColor)
        End If
    End Sub

    Private Sub 绘制按钮背景_DX12(context As D3D12_PaintContext, rect As RectangleF, part As SpinButtonPart)
        Dim c1 As Color = 按钮背景颜色
        Dim c2 As Color = 按钮背景渐变颜色
        If _pressedButton = part AndAlso 鼠标状态 = MouseStateEnum.Pressed Then
            If 鼠标按下时按钮背景颜色 <> Color.Empty Then c1 = 鼠标按下时按钮背景颜色
            If 鼠标按下时按钮渐变颜色 <> Color.Empty Then c2 = 鼠标按下时按钮渐变颜色
        ElseIf _hoverButton = part AndAlso 鼠标状态 = MouseStateEnum.Hover Then
            If 鼠标移上时按钮背景颜色 <> Color.Empty Then c1 = 鼠标移上时按钮背景颜色
            If 鼠标移上时按钮渐变颜色 <> Color.Empty Then c2 = 鼠标移上时按钮渐变颜色
        End If
        Dim fillColor As Color = If(c1.A > 0, c1, c2)
        If fillColor = Color.Empty OrElse fillColor.A = 0 Then Return
        context.FillRectangle(rect, fillColor)
    End Sub

    Private Sub 绘制圆角三角形_DX12(context As D3D12_PaintContext, center As PointF, up As Boolean, color As Color)
        Dim width = 箭头大小 * DpiScale()
        If width <= 0 Then Return
        Dim h = CSng(width * Math.Sqrt(3) / 2)
        Dim sign = If(up, 1, -1)
        Dim vertices = {New Vector2(center.X, center.Y - sign * h / 2), New Vector2(center.X + width / 2, center.Y + sign * h / 2),
                        New Vector2(center.X - width / 2, center.Y + sign * h / 2)}
        context.FillRoundedTriangle(vertices, Math.Max(width * 0.2F, 1), color)
    End Sub
End Class
