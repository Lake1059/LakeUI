Imports System.Numerics
Imports Vortice.DirectWrite
Imports DW = Vortice.DirectWrite
Partial Public Class ModernCheckBox
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse Me.Width <= 0 OrElse Me.Height <= 0 Then Return

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), MyBase.BackColor)
        End If

        绘制图形内容_DX12(context)
        绘制文本_DX12(context)
        绘制禁用遮罩_DX12(context)
    End Sub

    Private Sub 绘制图形内容_DX12(context As D3D12_PaintContext)
        Dim s As Single = DpiScale()
        Dim 当前框边框宽度 As Single = 框边框宽度 * s
        Dim 框区域 As RectangleF = 计算框区域(s)

        Dim 当前框背景色 As Color = 获取当前框背景颜色()
        Dim 当前框边框色 As Color = 获取鼠标状态颜色(框边框颜色值, 鼠标移上时框边框颜色, 鼠标按下时框边框颜色)

        If 当前模式 = CheckModeEnum.CheckBox Then
            绘制方框_DX12(context, 框区域, 当前框背景色, 当前框边框色, 当前框边框宽度, s)
        Else
            绘制圆框_DX12(context, 框区域, 当前框背景色, 当前框边框色, 当前框边框宽度, s)
        End If
    End Sub

    Private Sub 绘制禁用遮罩_DX12(context As D3D12_PaintContext)
        If Enabled OrElse 禁用时遮罩颜色.A <= 0 Then Return

        Dim s As Single = DpiScale()
        Dim 边框偏移 As Single = 框边框宽度 * s / 2.0F
        Dim 遮罩区域 As RectangleF = 计算框外缘区域(s)
        If 当前模式 = CheckModeEnum.CheckBox Then
            Dim 圆角 As Single = 框圆角半径 * s + 边框偏移
            context.FillShape(遮罩区域, 圆角, 禁用时遮罩颜色)
        Else
            context.FillEllipse(遮罩区域, 禁用时遮罩颜色)
        End If
    End Sub

    Private Sub 绘制方框_DX12(context As D3D12_PaintContext, 框区域 As RectangleF, 背景色 As Color, 边框色 As Color, 边框宽 As Single, s As Single)
        Dim 圆角 As Single = 框圆角半径 * s
        context.FillShape(框区域, 圆角, 背景色)
        context.StrokeRoundedRectangle(框区域, 圆角, 边框色, 边框宽)

        Dim progress As Single = 动画助手.Progress
        If progress > 0.001F Then 绘制勾号_DX12(context, 框区域, progress, s)
    End Sub

    Private Sub 绘制勾号_DX12(context As D3D12_PaintContext, 框区域 As RectangleF, progress As Single, s As Single)
        Dim 当前勾号色 As Color = 获取鼠标状态颜色(勾号颜色值, 鼠标移上时勾号颜色, 鼠标按下时勾号颜色)
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

    Private Sub 绘制圆框_DX12(context As D3D12_PaintContext, 框区域 As RectangleF, 背景色 As Color, 边框色 As Color, 边框宽 As Single, s As Single)
        context.FillEllipse(框区域, 背景色)
        context.StrokeEllipse(框区域, 边框色, 边框宽)

        Dim progress As Single = 动画助手.Progress
        If progress > 0.001F Then
            Dim 当前勾号色 As Color = 获取鼠标状态颜色(勾号颜色值, 鼠标移上时勾号颜色, 鼠标按下时勾号颜色)
            Dim 最大半径 As Single = (框区域.Width / 2.0F) - 框内边距 * s - 框边框宽度 * s / 2.0F
            If 最大半径 < 1 Then 最大半径 = 1
            Dim 当前半径 As Single = 最大半径 * progress
            Dim cx As Single = 框区域.X + 框区域.Width / 2.0F
            Dim cy As Single = 框区域.Y + 框区域.Height / 2.0F
            Dim inner As New RectangleF(cx - 当前半径, cy - 当前半径, 当前半径 * 2, 当前半径 * 2)
            context.FillEllipse(inner, 当前勾号色)
        End If
    End Sub

    Private Sub 绘制文本_DX12(context As D3D12_PaintContext)
        Dim s As Single = DpiScale()
        Dim 框尺寸 As Single = 操作框尺寸 * s
        Dim 间距 As Single = 框文本间距 * s
        Dim 边框偏移 As Single = 框边框宽度 * s / 2.0F
        Dim 文本X As Single = Me.Padding.Left + 边框偏移 + 框尺寸 + 间距
        Dim 文本可用宽度 As Single = Me.Width - 文本X - Me.Padding.Right
        If 文本可用宽度 <= 0 Then Return

        Dim mainText As String = If(MyBase.Text, "")
        If String.IsNullOrEmpty(mainText) AndAlso String.IsNullOrEmpty(次要文本) Then Return

        Dim mainY As Single = 计算主文本Y(s)

        If Not String.IsNullOrEmpty(次要文本) Then
            Dim mainH As Single = 获取主文本行高()
            Dim subH As Single = 获取次文本行高()
            Dim gap As Single = 主次文本间距 * s
            context.DrawText(mainText, Me.Font, 文本颜色, New RectangleF(文本X, mainY, 文本可用宽度, mainH), s, Vortice.DirectWrite.TextAlignment.Leading, Vortice.DirectWrite.ParagraphAlignment.Near)
            Dim subFont = 获取次文本字体()
            If subFont IsNot Nothing Then
                context.DrawText(次要文本, subFont, 次要文本颜色, New RectangleF(文本X, mainY + mainH + gap, 文本可用宽度, subH), s, Vortice.DirectWrite.TextAlignment.Leading, Vortice.DirectWrite.ParagraphAlignment.Near)
            End If
        Else
            Dim mainH As Single = 获取主文本行高()
            context.DrawText(mainText, Me.Font, 文本颜色, New RectangleF(文本X, mainY, 文本可用宽度, mainH), s, Vortice.DirectWrite.TextAlignment.Leading, Vortice.DirectWrite.ParagraphAlignment.Near)
        End If
    End Sub
End Class
