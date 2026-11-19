Imports System.Numerics
Imports Vortice.DirectWrite
Partial Public Class RoundDashBoard
    Implements ID3D12Renderable
    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse Me.Width < 1 OrElse Me.Height < 1 Then Return

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), MyBase.BackColor)
        End If

        Dim 中心X As Single = 0
        Dim 中心Y As Single = 0
        Dim progress As Single = 动画助手.Progress
        Dim hasContent As Boolean = 绘制图形内容_DX12(context, 中心X, 中心Y, progress)
        If hasContent Then 绘制中心文字_DX12(context, 中心X, 中心Y, progress)

        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            context.FillEllipse(New RectangleF(0.5F, 0.5F, Width - 1, Height - 1), 禁用时遮罩颜色)
        End If
    End Sub

    Private Function 绘制图形内容_DX12(context As D3D12_PaintContext, ByRef 中心X As Single, ByRef 中心Y As Single, ByRef progress As Single) As Boolean
        Dim s As Single = DpiScale()

        Dim 内容宽 As Single = Me.Width - Padding.Left - Padding.Right
        Dim 内容高 As Single = Me.Height - Padding.Top - Padding.Bottom
        Dim 正方形边长 As Single = Math.Min(内容宽, 内容高)
        中心X = Padding.Left + 内容宽 / 2.0F
        中心Y = Padding.Top + 内容高 / 2.0F
        Dim 外径 As Single = 外圈半径
        Dim 厚度值 As Single = 圆弧厚度 * s

        Dim 最大半径 As Single = 正方形边长 / 2.0F - 1
        If 外径 > 最大半径 Then 外径 = 最大半径
        If 外径 < 1 Then Return False

        Dim 画笔宽度 As Single = Math.Min(厚度值, 外径)
        Dim 绘制半径 As Single = 外径 - 画笔宽度 / 2.0F
        If 绘制半径 < 1 Then Return False

        Dim 绘制矩形 As New RectangleF(中心X - 绘制半径, 中心Y - 绘制半径, 绘制半径 * 2, 绘制半径 * 2)
        Dim 实际起始角 As Single = 起始角度 - 90
        Dim 实际扫过角 As Single = 表盘角度

        绘制圆弧_DX12(context, 绘制矩形, 轨道背景颜色, 画笔宽度, 实际起始角, 实际扫过角)

        progress = 动画助手.Progress
        If progress > 0.001F Then
            Dim 填充扫过角 As Single = 实际扫过角 * progress
            If 填充渐变颜色 <> Color.Empty Then
                绘制渐变弧_DX12(context, 绘制矩形, 画笔宽度, 实际起始角, 填充扫过角, 实际扫过角)
            Else
                绘制圆弧_DX12(context, 绘制矩形, 填充基础颜色, 画笔宽度, 实际起始角, 填充扫过角)
            End If

            If 显示指针 Then 绘制指针_DX12(context, 中心X, 中心Y, 外径, 画笔宽度, 实际起始角 + 填充扫过角, s)
        End If

        Return True
    End Function

    Private Sub 绘制圆弧_DX12(context As D3D12_PaintContext, rect As RectangleF, color As Color, penWidth As Single, startAngle As Single, sweepAngle As Single)
        If context Is Nothing OrElse color.A <= 0 OrElse penWidth <= 0 OrElse sweepAngle <= 0 Then Return
        context.DrawArc(rect, penWidth, startAngle, sweepAngle, color)
    End Sub

    Private Sub 绘制指针_DX12(context As D3D12_PaintContext, 中心X As Single, 中心Y As Single, 外径 As Single, 画笔宽度 As Single, 角度 As Single, s As Single)
        Dim 指针角度弧度 As Double = 角度 * Math.PI / 180.0
        Dim cosVal As Single = CSng(Math.Cos(指针角度弧度))
        Dim sinVal As Single = CSng(Math.Sin(指针角度弧度))
        Dim 指针内半径 As Single = Math.Max(0, 外径 - 画笔宽度 - 指针长度值 * s)
        Dim 指针外半径 As Single = 外径 + 2

        context.DrawRoundLine(
            New Vector2(中心X + cosVal * 指针内半径, 中心Y + sinVal * 指针内半径),
            New Vector2(中心X + cosVal * 指针外半径, 中心Y + sinVal * 指针外半径),
            指针颜色值, 指针宽度值 * s)
    End Sub

    Private Sub 绘制中心文字_DX12(context As D3D12_PaintContext, 中心X As Single, 中心Y As Single, progress As Single)
        If 中心文字模式 = CenterTextModeEnum.None Then Return

        Dim 文字内容 As String = 获取中心文字内容(progress)
        If String.IsNullOrEmpty(文字内容) Then Return

        Dim textRect As New RectangleF(中心X - Me.Width / 2.0F, 中心Y - Me.Height / 2.0F, Me.Width, Me.Height)
        context.DrawText(文字内容, Me.Font, Me.ForeColor, textRect, DpiScale(), TextAlignment.Center, ParagraphAlignment.Center)
    End Sub

    Private Sub 绘制渐变弧_DX12(context As D3D12_PaintContext, rect As RectangleF, penWidth As Single, startAngle As Single, fillSweep As Single, totalSweep As Single)
        If context Is Nothing OrElse penWidth <= 0 OrElse fillSweep <= 0 Then Return
        Dim fraction = If(渐变模式 = FillGradientModeEnum.FixedPosition AndAlso totalSweep > 0, fillSweep / totalSweep, 1.0F)
        context.DrawArc(rect, penWidth, startAngle, fillSweep, 填充基础颜色, 填充渐变颜色, fraction)
    End Sub
End Class
