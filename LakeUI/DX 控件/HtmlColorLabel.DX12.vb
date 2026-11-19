Imports Vortice.DirectWrite
Imports System.Numerics

Partial Public Class HtmlColorLabel
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse Me.Width <= 0 OrElse Me.Height <= 0 Then Return

        Dim 是否有圆角 As Boolean = 边框圆角半径 > 0
        Dim sourceRect As New RectangleF(0, 0, Me.Width, Me.Height)
        Dim 极限矩形区域 As New RectangleF(0, 0, Me.Width, Me.Height)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * DpiScale() / 2.0F
            极限矩形区域.Inflate(-half, -half)
        End If
        Dim 内容矩形区域 As New RectangleF(
            极限矩形区域.X + Me.Padding.Left,
            极限矩形区域.Y + Me.Padding.Top,
            极限矩形区域.Width - Me.Padding.Horizontal,
            极限矩形区域.Height - Me.Padding.Vertical)

        绘制图形内容_DX12(context, 是否有圆角, sourceRect, 极限矩形区域)
        绘制文本内容_DX12(context, 内容矩形区域)

        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            填充圆角矩形_DX12(context, 极限矩形区域, If(是否有圆角, 边框圆角半径 * DpiScale(), 0.0F), 禁用时遮罩颜色)
        End If
    End Sub

    Private Sub 绘制图形内容_DX12(context As D3D12_PaintContext, 是否有圆角 As Boolean, sourceRect As RectangleF, 极限矩形区域 As RectangleF)
        Dim s As Single = DpiScale()
        Dim r As Single = If(是否有圆角, 边框圆角半径 * s, 0.0F)
        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, Rectangle.Round(sourceRect))
        ElseIf MyBase.BackColor.A > 0 Then
            填充圆角矩形_DX12(context, 极限矩形区域, r, MyBase.BackColor)
        End If
        If 背景基础颜色 <> Color.Empty AndAlso 背景基础颜色.A > 0 Then
            填充圆角矩形_DX12(context, 极限矩形区域, r, 背景基础颜色)
        End If
        If 边框颜色.A > 0 AndAlso 边框宽度 > 0 Then
            绘制圆角边框_DX12(context, 极限矩形区域, r, 边框颜色, 边框宽度 * s)
        End If
    End Sub

    Private Sub 绘制文本内容_DX12(context As D3D12_PaintContext, 内容矩形区域 As RectangleF)
        _infoIconBounds = Rectangle.Empty
        ' 自动尺寸按边框的向上取整物理厚度预留空间；内容尺寸应向下取整，
        ' 避免半像素边框使 Rectangle.Round 多出 1px，从而让 Top/Middle/Bottom
        ' 在没有实际剩余空间时仍出现不同的 Y 坐标。
        Dim 内容区域 As New Rectangle(
            CInt(Math.Round(内容矩形区域.X)),
            CInt(Math.Round(内容矩形区域.Y)),
            Math.Max(0, CInt(Math.Floor(内容矩形区域.Width))),
            Math.Max(0, CInt(Math.Floor(内容矩形区域.Height))))
        Dim 原始文本 = MyBase.Text
        If Not ShouldShowInfoIcon() AndAlso Not String.IsNullOrEmpty(原始文本) AndAlso 是否纯文本(原始文本) Then
            绘制纯文本_DX12(context, 原始文本, 内容区域)
            Return
        End If

        Dim 行列表 = 计算文本布局(内容区域.Width)
        If 行列表.Count = 0 Then Return
        Dim 总高度 As Integer = 0
        For Each 行 In 行列表
            总高度 += 行.行高
        Next
        If 行列表.Count > 1 Then 总高度 += 行距 * (行列表.Count - 1)

        Dim 起始Y As Integer
        Select Case 文字对齐方位
            Case TextAlignEnum.BottomLeft, TextAlignEnum.BottomRight
                起始Y = 内容区域.Y + 内容区域.Height - 总高度
            Case TextAlignEnum.Center, TextAlignEnum.MiddleLeft, TextAlignEnum.MiddleRight
                起始Y = 内容区域.Y + (内容区域.Height - 总高度) \ 2
            Case Else
                起始Y = 内容区域.Y
        End Select

        Dim 当前Y As Integer = 起始Y
        For Each 行 In 行列表
            Dim 对齐偏移 As Single
            Select Case 文字对齐方位
                Case TextAlignEnum.Center
                    对齐偏移 = (内容区域.Width - 行.行宽) / 2.0F
                Case TextAlignEnum.TopRight, TextAlignEnum.BottomRight, TextAlignEnum.MiddleRight
                    对齐偏移 = 内容区域.Width - 行.行宽
                Case Else
                    对齐偏移 = 0
            End Select

            For Each 单元 In 行.单元列表
                If 单元.类型 = 渲染单元类型.水平间距 Then Continue For
                Dim yOffset As Integer
                If 单元.类型 = 渲染单元类型.信息图标 Then
                    yOffset = (行.行高 - 单元.高度) \ 2
                Else
                    yOffset = If(行内垂直对齐方式 = InlineAlignEnum.Center, (行.行高 - 单元.高度) \ 2, 行.行高 - 单元.高度)
                End If

                Dim drawPoint As New PointF(内容区域.X + 对齐偏移 + 单元.X偏移, 当前Y + yOffset)
                Select Case 单元.类型
                    Case 渲染单元类型.信息图标
                        Dim iconRect As New RectangleF(drawPoint.X, drawPoint.Y, 单元.宽度, 单元.高度)
                        绘制信息图标_DX12(context, iconRect)
                        _infoIconBounds = Rectangle.Ceiling(iconRect)
                    Case Else
                        Dim unitFont = 获取缓存字体(单元.字号, 单元.字体名称, 单元.字体样式)
                        绘制单元_DX12(context, 单元.文本, unitFont, 单元.颜色, drawPoint, 单元.宽度, 单元.高度)
                End Select
            Next
            当前Y += 行.行高 + 行距
        Next
    End Sub

    Private Sub 绘制纯文本_DX12(context As D3D12_PaintContext, text As String, 内容区域 As Rectangle)
        Dim ta As Vortice.DirectWrite.TextAlignment
        Dim pa As Vortice.DirectWrite.ParagraphAlignment
        Select Case 文字对齐方位
            Case TextAlignEnum.TopLeft : ta = Vortice.DirectWrite.TextAlignment.Leading : pa = Vortice.DirectWrite.ParagraphAlignment.Near
            Case TextAlignEnum.TopRight : ta = Vortice.DirectWrite.TextAlignment.Trailing : pa = Vortice.DirectWrite.ParagraphAlignment.Near
            Case TextAlignEnum.Center : ta = Vortice.DirectWrite.TextAlignment.Center : pa = Vortice.DirectWrite.ParagraphAlignment.Center
            Case TextAlignEnum.BottomLeft : ta = Vortice.DirectWrite.TextAlignment.Leading : pa = Vortice.DirectWrite.ParagraphAlignment.Far
            Case TextAlignEnum.BottomRight : ta = Vortice.DirectWrite.TextAlignment.Trailing : pa = Vortice.DirectWrite.ParagraphAlignment.Far
            Case TextAlignEnum.MiddleLeft : ta = Vortice.DirectWrite.TextAlignment.Leading : pa = Vortice.DirectWrite.ParagraphAlignment.Center
            Case TextAlignEnum.MiddleRight : ta = Vortice.DirectWrite.TextAlignment.Trailing : pa = Vortice.DirectWrite.ParagraphAlignment.Center
            Case Else : ta = Vortice.DirectWrite.TextAlignment.Leading : pa = Vortice.DirectWrite.ParagraphAlignment.Near
        End Select
        context.DrawText(text, Me.Font, 文本颜色, 内容区域, DpiScale(), ta, pa, wordWrap:=True)
    End Sub

    Private Sub 绘制单元_DX12(context As D3D12_PaintContext, text As String, font As Font, color As Color,
                           pt As PointF, w As Integer, h As Integer)
        If String.IsNullOrEmpty(text) Then Return
        context.DrawText(text, font, color, New RectangleF(pt.X, pt.Y, Math.Max(1, w + 2), Math.Max(1, h + 2)), DpiScale(),
                         Vortice.DirectWrite.TextAlignment.Leading,
                         Vortice.DirectWrite.ParagraphAlignment.Near)
    End Sub

    Private Sub 绘制信息图标_DX12(context As D3D12_PaintContext, rect As RectangleF)
        If rect.Width <= 0 OrElse rect.Height <= 0 OrElse 信息图标颜色.A = 0 Then Return

        Dim lineWidth As Single = Math.Max(0.5F, 信息图标线条粗细 * DpiScale())
        Dim inset As Single = lineWidth / 2.0F
        Dim circleRect As New RectangleF(rect.X + inset, rect.Y + inset,
                                         Math.Max(1.0F, rect.Width - lineWidth),
                                         Math.Max(1.0F, rect.Height - lineWidth))
        context.StrokeEllipse(circleRect, 信息图标颜色, lineWidth)

        Dim cx As Single = rect.X + rect.Width / 2.0F
        Dim glyphWidth As Single = Math.Max(lineWidth, rect.Width * 0.15F)
        Dim topDotR As Single = glyphWidth / 2.0F
        Dim glyphTop As Single = rect.Y + rect.Height * 0.27F
        Dim glyphBottom As Single = rect.Y + rect.Height * 0.74F
        Dim gap As Single = Math.Max(glyphWidth * 0.65F, rect.Height * 0.055F)
        Dim dotCenterY As Single = glyphTop + topDotR
        context.FillEllipse(New RectangleF(cx - topDotR, dotCenterY - topDotR, 2 * topDotR, 2 * topDotR), 信息图标颜色)

        Dim stemTop As Single = dotCenterY + topDotR + gap
        Dim stemHeight As Single = Math.Max(glyphWidth, glyphBottom - stemTop)
        context.FillRectangle(New RectangleF(cx - glyphWidth / 2.0F, stemTop, glyphWidth, stemHeight), 信息图标颜色)
    End Sub

    Private Sub 填充圆角矩形_DX12(context As D3D12_PaintContext, rect As RectangleF, radius As Single, color As Color)
        context.FillRoundedRectangle(rect, radius, color)
    End Sub

    Private Sub 绘制圆角边框_DX12(context As D3D12_PaintContext, rect As RectangleF, radius As Single, color As Color, strokeWidth As Single)
        context.StrokeRoundedRectangle(rect, radius, color, strokeWidth)
    End Sub
End Class
