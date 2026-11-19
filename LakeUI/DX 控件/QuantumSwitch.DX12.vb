Imports Vortice.DirectWrite
Imports System.Numerics

Partial Public Class QuantumSwitch
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse Me.Width <= 0 OrElse Me.Height <= 0 Then Return

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), MyBase.BackColor)
        End If

        Dim 极限矩形区域 As New RectangleF(0, 0, Me.Width, Me.Height)
        Dim s As Single = DpiScale()
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * s / 2.0F
            极限矩形区域.Inflate(-half, -half)
        End If
        If 极限矩形区域.Width <= 0 OrElse 极限矩形区域.Height <= 0 Then Return

        Dim 是未观测状态 As Boolean = 观测者模式 AndAlso 鼠标状态 = MouseStateEnum.Normal
        If 是未观测状态 Then
            绘制不确定态_DX12(context, 极限矩形区域)
            绘制不确定态文字_DX12(context, 极限矩形区域)
        Else
            绘制正常态_DX12(context, 极限矩形区域)
        End If

        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            填充圆角矩形_DX12(context, 极限矩形区域, CSng(Math.Floor(极限矩形区域.Height / 2.0F)), 禁用时遮罩颜色)
        End If
    End Sub

    Private Sub 绘制正常态_DX12(context As D3D12_PaintContext, 极限矩形区域 As RectangleF)
        Dim 轨道颜色 As Color = 获取当前轨道颜色()
        Dim 滑块颜色 As Color = 获取当前滑块颜色()
        Dim 当前边框颜色 As Color = 获取当前边框颜色()
        Dim 圆角半径 As Single = CSng(Math.Floor(极限矩形区域.Height / 2.0F))

        填充圆角矩形_DX12(context, 极限矩形区域, 圆角半径, 轨道颜色)
        绘制圆角边框_DX12(context, 极限矩形区域, 圆角半径, 当前边框颜色, 边框宽度 * DpiScale())

        Dim 滑块区域 As RectangleF = 计算当前滑块区域(极限矩形区域, 动画助手.Progress)
        填充椭圆_DX12(context, 滑块区域, 滑块颜色)
    End Sub

    Private Sub 绘制不确定态_DX12(context As D3D12_PaintContext, 极限矩形区域 As RectangleF)
        Dim 圆角半径 As Single = CSng(Math.Floor(极限矩形区域.Height / 2.0F))
        填充圆角矩形_DX12(context, 极限矩形区域, 圆角半径, 不确定态轨道颜色值)
        绘制圆角边框_DX12(context, 极限矩形区域, 圆角半径, 获取当前边框颜色(), 边框宽度 * DpiScale())

        Dim 滑块区域 As RectangleF = 计算不确定态滑块区域(极限矩形区域)
        填充椭圆_DX12(context, 滑块区域, 不确定态滑块颜色值)
    End Sub

    Private Sub 绘制不确定态文字_DX12(context As D3D12_PaintContext, 极限矩形区域 As RectangleF)
        Dim 滑块区域 As RectangleF = 计算不确定态滑块区域(极限矩形区域)
        If 滑块区域.Width <= 0 OrElse 滑块区域.Height <= 0 Then Return

        Dim 字体大小Px As Single = Math.Max(1.0F, 滑块区域.Height * 0.55F)
        Dim 字体大小Pt As Single = 字体大小Px * 72.0F / 96.0F / Math.Max(0.01F, DpiScale())
        Dim font = 获取不确定态字体(字体大小Pt)
        context.DrawText("?", font, 不确定态轨道颜色值, 滑块区域, DpiScale(), Vortice.DirectWrite.TextAlignment.Center, Vortice.DirectWrite.ParagraphAlignment.Center)
    End Sub

    Private Sub 填充圆角矩形_DX12(context As D3D12_PaintContext, bounds As RectangleF, radius As Single, color As Color)
        context.FillRoundedRectangle(bounds, radius, color)
    End Sub

    Private Sub 绘制圆角边框_DX12(context As D3D12_PaintContext, bounds As RectangleF, radius As Single, color As Color, strokeWidth As Single)
        context.StrokeRoundedRectangle(bounds, radius, color, strokeWidth)
    End Sub

    Private Sub 填充椭圆_DX12(context As D3D12_PaintContext, bounds As RectangleF, color As Color)
        context.FillEllipse(bounds, color)
    End Sub
End Class
