Imports System.Numerics
Imports Vortice.DirectWrite
Partial Public Class ProgressRing
    Implements ID3D12Renderable
    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse Me.Width < 1 OrElse Me.Height < 1 Then Return

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), MyBase.BackColor)
        End If

        Select Case 动画样式
            Case StyleEnum.Win11
                绘制Win11样式_DX12(context)
            Case StyleEnum.Win10
                绘制Win10样式_DX12(context)
        End Select

        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            context.FillEllipse(New RectangleF(0.5F, 0.5F, Width - 1, Height - 1), 禁用时遮罩颜色)
        End If
    End Sub

    Private Sub 绘制Win11样式_DX12(context As D3D12_PaintContext)
        Dim s As Single = DpiScale()
        Dim 绘制区域 As RectangleF = 获取内容绘制区域()
        ProgressRingRenderer12.RenderWin11(context, 绘制区域, 圆弧颜色, 圆弧厚度值 * s,
                                       动画运行中, 秒表.Elapsed.TotalMilliseconds,
                                       DesignMode, 动画周期时长)
    End Sub

    Private Sub 绘制Win10样式_DX12(context As D3D12_PaintContext)
        Dim s As Single = DpiScale()
        Dim 绘制区域 As RectangleF = 获取内容绘制区域()
        ProgressRingRenderer12.RenderWin10(context, 绘制区域, 圆弧颜色, 圆弧厚度值 * s,
                                              动画运行中, 秒表.Elapsed.TotalMilliseconds,
                                              DesignMode, 动画周期时长)
    End Sub
End Class
