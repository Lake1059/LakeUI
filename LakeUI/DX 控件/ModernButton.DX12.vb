Imports Vortice.DirectWrite
Partial Public Class ModernButton
    Implements ID3D12Renderable
    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse Me.Width <= 0 OrElse Me.Height <= 0 Then Return

        Dim 是否有圆角 As Boolean = 边框圆角半径 > 0
        Dim s As Single = DpiScale()
        Dim sourceRect As New RectangleF(0, 0, Me.Width, Me.Height)
        Dim 极限矩形区域 As New RectangleF(0, 0, Me.Width, Me.Height)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * s / 2.0F
            极限矩形区域.Inflate(-half, -half)
        End If
        Dim 内容矩形区域 As New RectangleF(
            极限矩形区域.X + Me.Padding.Left,
            极限矩形区域.Y + Me.Padding.Top,
            Math.Max(0.0F, 极限矩形区域.Width - Me.Padding.Horizontal),
            Math.Max(0.0F, 极限矩形区域.Height - Me.Padding.Vertical))
        Dim 图标宽度 As Single = 计算图标占用的水平宽度(内容矩形区域, s)

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, Rectangle.Round(sourceRect))
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillShape(极限矩形区域, If(是否有圆角, 边框圆角半径 * s, 0.0F), MyBase.BackColor)
        End If

        绘制图形内容_DX12(context, 是否有圆角, 极限矩形区域, 内容矩形区域, 图标宽度, s)
        绘制文本_DX12(context, 内容矩形区域, 图标宽度, s)

        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            context.FillShape(极限矩形区域, If(是否有圆角, 边框圆角半径 * s, 0.0F), 禁用时遮罩颜色)
        End If

    End Sub

    Private Sub 绘制图形内容_DX12(context As D3D12_PaintContext, 是否有圆角 As Boolean, 极限矩形区域 As RectangleF, 内容矩形区域 As RectangleF, 图标宽度 As Single, s As Single)
        Dim 背景颜色缓存值 As Color
        Dim 渐变颜色缓存值 As Color
        Dim 边框颜色缓存值 As Color
        If 颜色动画已启用 Then
            Dim 目标背景 As Color = Nothing, 目标渐变 As Color = Nothing, 目标边框 As Color = Nothing
            根据鼠标状态分配颜色(目标背景, 目标渐变, 目标边框)
            Dim t As Single = 动画助手.Progress
            背景颜色缓存值 = 颜色插值(动画前背景颜色, 目标背景, t)
            渐变颜色缓存值 = 颜色插值(动画前渐变颜色, 目标渐变, t)
            边框颜色缓存值 = 颜色插值(动画前边框颜色, 目标边框, t)
        Else
            根据鼠标状态分配颜色(背景颜色缓存值, 渐变颜色缓存值, 边框颜色缓存值)
        End If

        Dim radius As Single = If(是否有圆角, 边框圆角半径 * s, 0.0F)
        If 背景颜色缓存值.A > 0 OrElse 渐变颜色缓存值.A > 0 Then
            context.FillShape(极限矩形区域, radius, 背景颜色缓存值, 渐变颜色缓存值, 渐变方向)
        End If

        If 背景图片 IsNot Nothing Then context.DrawMaskedImage(背景图片, 内容矩形区域, 极限矩形区域, radius, 15)
        绘制点击涟漪_DX12(context, 极限矩形区域, radius)
        绘制长按遮罩_DX12(context, 极限矩形区域, radius)

        If 边框颜色缓存值.A > 0 AndAlso 边框宽度 > 0 Then
            context.StrokeRoundedRectangle(极限矩形区域, radius, 边框颜色缓存值, 边框宽度 * s)
        End If

        绘制图标_DX12(context, 内容矩形区域, 图标宽度, s)
    End Sub

    Private Sub 绘制长按遮罩_DX12(context As D3D12_PaintContext, area As RectangleF, maskRadius As Single)
        If Not 长按确认已启用 Then Return
        Dim progress As Single = 长按动画助手.Progress
        If progress < 0.001F Then Return

        Dim maskRect As RectangleF
        If 长按遮罩方向 = HoldClickDirectionEnum.LeftToRight Then
            maskRect = New RectangleF(area.X, area.Y, area.Width * progress, area.Height)
        Else
            Dim w As Single = area.Width * progress
            maskRect = New RectangleF(area.Right - w, area.Y, w, area.Height)
        End If

        context.FillShape(maskRect, 0, 长按遮罩颜色, maskRadius:=maskRadius, mask:=area)
    End Sub

    Private Sub 绘制图标_DX12(context As D3D12_PaintContext, 内容矩形区域 As RectangleF, iconSize As Single, s As Single)
        If 图标 Is Nothing OrElse iconSize <= 0 Then Return
        Dim iconX As Single = 内容矩形区域.X + 图标边距 * s
        Dim iconY As Single = 内容矩形区域.Y + (内容矩形区域.Height - iconSize) / 2.0F
        context.DrawImage(图标,
                          New RectangleF(iconX, iconY, iconSize, iconSize))
    End Sub

    Private Sub 绘制文本_DX12(context As D3D12_PaintContext, 内容矩形区域 As RectangleF, 图标宽度 As Single, s As Single)
        Dim _图标边距 As Single = 图标边距 * s
        Dim _边框圆角半径 As Single = 边框圆角半径 * s
        Dim 图标占用总宽度 As Single = If(图标宽度 > 0, 图标宽度 + _图标边距, 0)
        Dim 文本绘制区域 As New RectangleF(
            内容矩形区域.X + 图标占用总宽度 + _边框圆角半径,
            内容矩形区域.Y,
            内容矩形区域.Width - 图标占用总宽度 - _边框圆角半径 * 2,
            内容矩形区域.Height)
        If 文本绘制区域.Width <= 0 OrElse 文本绘制区域.Height <= 0 Then Return

        Dim align As Vortice.DirectWrite.TextAlignment
        Select Case 文字对齐方位
            Case TextAlignEnum.Left : align = Vortice.DirectWrite.TextAlignment.Leading
            Case TextAlignEnum.Right : align = Vortice.DirectWrite.TextAlignment.Trailing
            Case Else : align = Vortice.DirectWrite.TextAlignment.Center
        End Select

        Dim mainText As String = 解析助记键文本(If(MyBase.Text, "")).DisplayText
        If String.IsNullOrEmpty(mainText) AndAlso String.IsNullOrEmpty(次要文本) Then Return

        If Not String.IsNullOrEmpty(次要文本) Then
            Dim mainH As Single = 获取文本高度(mainText, Me.Font, _mainTextHeightCacheKey, _mainTextHeightCache)
            Dim subFont = 获取次文本字体()
            If subFont IsNot Nothing Then
                Dim subH As Single = 获取文本高度(次要文本, subFont, _subTextHeightCacheKey, _subTextHeightCache)
                Dim gap As Single = 主次文本间距 * s
                Dim totalH As Single = mainH + gap + subH
                Dim startY As Single = 文本绘制区域.Y + (文本绘制区域.Height - totalH) / 2.0F
                context.DrawText(mainText, Me.Font, 文本颜色, New RectangleF(文本绘制区域.X, startY, 文本绘制区域.Width, mainH), s, align, ParagraphAlignment.Near)
                context.DrawText(次要文本, subFont, 次要文本颜色, New RectangleF(文本绘制区域.X, startY + mainH + gap, 文本绘制区域.Width, subH), s, align, ParagraphAlignment.Near)
            End If
        Else
            context.DrawText(mainText, Me.Font, 文本颜色, 文本绘制区域, s, align, ParagraphAlignment.Center)
        End If
    End Sub

    Private Sub 绘制点击涟漪_DX12(context As D3D12_PaintContext, area As RectangleF, maskRadius As Single)
        If Not 点击涟漪已启用 OrElse 长按确认已启用 OrElse 点击涟漪颜色.A <= 0 Then Return
        Dim progress = Math.Clamp(涟漪动画助手.Progress, 0.0F, 1.0F)
        If progress <= 0.0F OrElse progress >= 1.0F Then Return

        Dim dx = Math.Max(Math.Abs(涟漪中心.X - area.Left), Math.Abs(area.Right - 涟漪中心.X))
        Dim dy = Math.Max(Math.Abs(涟漪中心.Y - area.Top), Math.Abs(area.Bottom - 涟漪中心.Y))
        Dim radius = CSng(Math.Sqrt(dx * dx + dy * dy) * progress)
        Dim alpha = Math.Clamp(CInt(点击涟漪颜色.A * (1.0F - progress)), 0, 255)
        If radius <= 0.0F OrElse alpha <= 0 Then Return

        Dim ink = Color.FromArgb(alpha, 点击涟漪颜色)
        Using context.PushClip(area)
            context.FillShape(New RectangleF(涟漪中心.X - radius, 涟漪中心.Y - radius, 2 * radius, 2 * radius), -1, ink, maskRadius:=maskRadius, mask:=area)
        End Using
    End Sub
End Class
