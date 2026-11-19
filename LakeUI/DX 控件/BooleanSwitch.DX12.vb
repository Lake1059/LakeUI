Imports Vortice.DirectWrite
Imports System.Numerics

Partial Public Class BooleanSwitch
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse Me.Width <= 0 OrElse Me.Height <= 0 Then Return

        Dim bounds As New RectangleF(0, 0, Me.Width, Me.Height)
        Dim scale As Single = DpiScale()
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * scale / 2.0F
            bounds.Inflate(-half, -half)
        End If
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return

        Dim sourceRect As New RectangleF(0, 0, Me.Width, Me.Height)
        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, Rectangle.Round(sourceRect))
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(sourceRect, MyBase.BackColor)
        End If

        绘制图形内容_DX12(context, bounds)

        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            填充圆角矩形_DX12(context, bounds, CSng(Math.Floor(bounds.Height / 2.0F)), 禁用时遮罩颜色)
        End If
    End Sub

    Private Sub 绘制图形内容_DX12(context As D3D12_PaintContext, bounds As RectangleF)
        Dim trackColor As Color = 获取当前轨道颜色()
        Dim thumbColor As Color = 获取当前滑块颜色()
        Dim currentBorderColor As Color = 获取当前边框颜色()
        Dim radius As Single = CSng(Math.Floor(bounds.Height / 2.0F))

        填充圆角矩形_DX12(context, bounds, radius, trackColor)

        Dim borderWidth As Single = 边框宽度 * DpiScale()
        If currentBorderColor.A > 0 AndAlso borderWidth > 0 Then
            context.StrokeRoundedRectangle(bounds, radius, currentBorderColor, borderWidth)
        End If

        Dim thumbMargin As Single = 滑块边距值 * DpiScale()
        Dim thumbDiameter As Single = bounds.Height - thumbMargin * 2
        If thumbDiameter <= 0 Then Return
        Dim thumbMinX As Single = bounds.X + thumbMargin
        Dim thumbMaxX As Single = bounds.Right - thumbMargin - thumbDiameter
        Dim thumbX As Single = thumbMinX + (thumbMaxX - thumbMinX) * 动画助手.Progress
        Dim thumbY As Single = bounds.Y + thumbMargin
        Dim thumbRect As New RectangleF(thumbX, thumbY, thumbDiameter, thumbDiameter)
        If thumbColor.A > 0 Then
            context.FillEllipse(thumbRect, thumbColor)
        End If
    End Sub

    Private Sub 填充圆角矩形_DX12(context As D3D12_PaintContext, bounds As RectangleF, radius As Single, color As Color)
        context.FillRoundedRectangle(bounds, radius, color)
    End Sub
End Class
