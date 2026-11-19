Imports System.Numerics
Imports Vortice.DirectWrite
Partial Public Class CpuMonitor
    Implements ID3D12Renderable
    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If Me.Width <= 0 OrElse Me.Height <= 0 Then Return

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), MyBase.BackColor)
        End If

        Dim layout = 获取绘制布局()
        If layout IsNot Nothing Then
            绘制图形内容_DX12(context, layout)
            绘制文字内容_DX12(context, layout)
        End If

        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), 禁用时遮罩颜色)
        End If
    End Sub

    Private Sub 绘制图形内容_DX12(context As D3D12_PaintContext, layout As 绘制布局信息)
        Dim usages() As Single = 最近占用
        Dim s As Single = layout.Scale
        Dim canDrawHistory As Boolean = Not layout.Simplified AndAlso 显示历史图表 AndAlso 启用历史记录

        If canDrawHistory Then
            SyncLock 历史锁
                For Each item In layout.Items
                    Dim hist As Single() = If(item.GlobalIndex >= 0 AndAlso item.GlobalIndex < 历史数据.Count, 历史数据(item.GlobalIndex), Nothing)
                    绘制常规核心图形_DX12(context, item, hist, 历史写入位置, s)
                Next
            End SyncLock
        Else
            For Each item In layout.Items
                Dim usage As Single = If(usages IsNot Nothing AndAlso item.GlobalIndex < usages.Length, usages(item.GlobalIndex), 0)
                If layout.Simplified Then
                    绘制简化核心_DX12(context, item, usage, s)
                Else
                    绘制常规核心图形_DX12(context, item, Nothing, 0, s)
                End If
            Next
        End If
    End Sub

    Private Sub 绘制文字内容_DX12(context As D3D12_PaintContext, layout As 绘制布局信息)
        Dim usages() As Single = 最近占用
        Dim s As Single = layout.Scale
        For Each item In layout.Items
            Dim usage As Single = If(usages IsNot Nothing AndAlso item.GlobalIndex < usages.Length, usages(item.GlobalIndex), 0)
            If layout.Simplified Then
                绘制简化核心文字_DX12(context, item, usage, s)
            Else
                绘制常规核心文字_DX12(context, item, usage, s)
            End If
        Next
    End Sub

    Private Sub 绘制常规核心图形_DX12(context As D3D12_PaintContext, item As 核心布局项, hist As Single(), historyStart As Integer, s As Single)
        绘制核心背景与边框_DX12(context, item.CellRect, s)
        If item.InnerRect.Width <= 0 OrElse item.InnerRect.Height <= 0 Then Return

        If 显示历史图表 AndAlso hist IsNot Nothing AndAlso hist.Length >= 2 AndAlso item.GraphRect.Height >= 2 Then
            Using context.PushClip(item.CellRect)
                绘制历史图表_DX12(context, item.GraphRect, hist, historyStart, s)
            End Using
        End If
    End Sub

    Private Sub 绘制常规核心文字_DX12(context As D3D12_PaintContext, item As 核心布局项, usage As Single, s As Single)
        If item.TextRect.Width <= 0 OrElse item.TextRect.Height <= 0 Then Return
        Dim 文本 As String = 构造核心文字(item.GlobalIndex, usage)
        If String.IsNullOrEmpty(文本) Then Return
        绘制文字_DX12(context, item.TextRect, 文本, s)
    End Sub

    Private Sub 绘制简化核心_DX12(context As D3D12_PaintContext, item As 核心布局项, usage As Single, s As Single)
        绘制核心背景与边框_DX12(context, item.CellRect, s)
        If item.InnerRect.Width > 0 AndAlso item.InnerRect.Height > 0 Then
            Dim fillH As Single = item.InnerRect.Height * 限制占用值(usage)
            If fillH > 0 Then
                Using context.PushClip(item.CellRect)
                    Dim fillRect As New RectangleF(item.InnerRect.X, item.InnerRect.Bottom - fillH, item.InnerRect.Width, fillH)
                    context.FillRectangle(fillRect, 选择占用颜色(usage))
                End Using
            End If
        End If
    End Sub

    Private Sub 绘制简化核心文字_DX12(context As D3D12_PaintContext, item As 核心布局项, usage As Single, s As Single)
        If item.TextRect.Width > 0 AndAlso item.TextRect.Height > 0 Then
            绘制文字_DX12(context, item.TextRect, 百分比文本(usage), s)
        End If
    End Sub

    Private Sub 绘制文字_DX12(context As D3D12_PaintContext, inner As RectangleF, text As String, s As Single)
        Dim tp As Padding = 文字内边距值
        Dim textRect As New RectangleF(
            inner.X + tp.Left * s,
            inner.Y + tp.Top * s,
            Math.Max(0, inner.Width - (tp.Left + tp.Right) * s),
            Math.Max(0, inner.Height - (tp.Top + tp.Bottom) * s))
        If textRect.Width <= 0 OrElse textRect.Height <= 0 Then Return

        context.DrawText(If(text, ""), Me.Font, Me.ForeColor, textRect, s, 转文本水平对齐(文字对齐值), 转文本垂直对齐(文字对齐值))
    End Sub

    Private Sub 绘制核心背景与边框_DX12(context As D3D12_PaintContext, rect As RectangleF, s As Single)
        Dim bw As Single = 核心边框粗细值 * s
        Dim r As Single = 圆角半径值 * s
        Dim hasBorder As Boolean = 核心边框颜色值.A > 0 AndAlso bw > 0

        If 核心背景颜色值.A > 0 Then
            context.FillRoundedRectangle(rect, r, 核心背景颜色值)
        End If

        If hasBorder Then
            Dim half As Single = bw * 0.5F
            Dim bRect As RectangleF = RectangleF.Inflate(rect, -half, -half)
            context.StrokeRoundedRectangle(bRect, Math.Max(0, r - half), 核心边框颜色值, bw)
        End If
    End Sub

    Private Sub 绘制历史图表_DX12(context As D3D12_PaintContext, rect As RectangleF, hist As Single(), historyStart As Integer, s As Single)
        If rect.Width < 2 OrElse rect.Height < 2 Then Return
        Dim n As Integer = hist.Length
        If n < 2 Then Return

        If 图表背景颜色值.A > 0 Then context.FillRectangle(rect, 图表背景颜色值)

        Dim step_ As Single = rect.Width / (n - 1)
        If _历史点缓存.Length < n Then
            _历史点缓存 = New Vector2(n - 1) {}
        End If
        Dim pts As Vector2() = _历史点缓存
        For i As Integer = 0 To n - 1
            Dim v As Single = 限制占用值(hist(历史数组索引(i, n, historyStart)))
            pts(i) = New Vector2(rect.X + i * step_, rect.Bottom - v * rect.Height)
        Next

        context.DrawHistory(pts, n, 图表填充颜色值, rect.Bottom, 0)
        Dim lineW As Single = 图表线条粗细值 * s
        If lineW > 0 Then context.DrawHistory(pts, n, 图表线条颜色值, 0, lineW)
        If 占满颜色值.A > 0 Then
            Dim tickW As Single = Math.Max(lineW, 1.5F * s)
            For i As Integer = 0 To n - 1
                If 历史满载(hist(历史数组索引(i, n, historyStart))) Then
                    context.DrawLine(New Vector2(pts(i).X, pts(i).Y), New Vector2(pts(i).X, rect.Bottom), 占满颜色值, tickW)
                End If
            Next
        End If
    End Sub
End Class
