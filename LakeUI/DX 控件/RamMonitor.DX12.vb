Imports System.Numerics
Imports Vortice.DirectWrite
Partial Public Class RamMonitor
    Implements ID3D12Renderable
    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If Me.Width <= 0 OrElse Me.Height <= 0 Then Return

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), MyBase.BackColor)
        End If

        Dim history = 获取历史快照()
        绘制图形内容_DX12(context, history)
        绘制文字内容_DX12(context, history)

        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            绘制禁用遮罩_DX12(context)
        End If
    End Sub

    Private Sub 绘制图形内容_DX12(context As D3D12_PaintContext, history As HistorySnapshot)
        Dim s As Single = DpiScale()
        Dim pad As Padding = Me.Padding
        Dim rect As New RectangleF(pad.Left * s, pad.Top * s,
                                   Math.Max(0, Me.Width - (pad.Left + pad.Right) * s),
                                   Math.Max(0, Me.Height - (pad.Top + pad.Bottom) * s))
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return

        绘制背景与边框_DX12(context, rect, s)

        Dim innerPad As Single = 核心内边距值 * s
        Dim inner As New RectangleF(rect.X + innerPad, rect.Y + innerPad,
                                    Math.Max(0, rect.Width - innerPad * 2),
                                    Math.Max(0, rect.Height - innerPad * 2))
        If inner.Width <= 0 OrElse inner.Height <= 0 Then Return

        Dim fontHeight As Single = 文本像素高度(s)
        Dim tp As Padding = 文字内边距值
        Dim stripH As Single = fontHeight + (tp.Top + tp.Bottom) * s
        Dim topText As String = 构造槽位文字(TextSlotPosition.Top)
        Dim bottomText As String = 构造槽位文字(TextSlotPosition.Bottom)
        Dim topStripH As Single = If(Not String.IsNullOrEmpty(topText), Math.Min(stripH, inner.Height), 0)
        Dim bottomStripH As Single = If(Not String.IsNullOrEmpty(bottomText), Math.Min(stripH, Math.Max(0, inner.Height - topStripH)), 0)
        Dim graphRect As New RectangleF(inner.X, inner.Y + topStripH,
                                        inner.Width,
                                        Math.Max(0, inner.Height - topStripH - bottomStripH))

        If graphRect.Height >= 2 AndAlso graphRect.Width >= 2 Then
            Using context.PushClip(rect)
                绘制历史图表_DX12(context, graphRect, s, history)
            End Using
        End If
    End Sub

    Private Sub 绘制历史图表_DX12(context As D3D12_PaintContext, rect As RectangleF, s As Single, history As HistorySnapshot)
        If 图表背景颜色值.A > 0 Then context.FillRectangle(rect, 图表背景颜色值)

        Dim hist = history.Points
        Dim n As Integer = history.Count
        Dim cap As Integer = Math.Max(2, 记录长度值)
        _图表矩形 = rect
        _图表样本数 = n
        If n < 2 Then Return

        Dim step_ As Single = rect.Width / CSng(cap - 1)

        If _inUseTopBuffer.Length < n Then ReDim _inUseTopBuffer(history.Points.Length - 1)
        If _modifiedTopBuffer.Length < n Then ReDim _modifiedTopBuffer(history.Points.Length - 1)
        If _standbyTopBuffer.Length < n Then ReDim _standbyTopBuffer(history.Points.Length - 1)
        Dim inUseTop = _inUseTopBuffer
        Dim modTop = _modifiedTopBuffer
        Dim standbyTop = _standbyTopBuffer
        For i As Integer = 0 To n - 1
            Dim x As Single = rect.Right - CSng(n - 1 - i) * step_
            Dim total As Double = If(hist(i).TotalBytes > 0UL, CDbl(hist(i).TotalBytes), 0.0)
            Dim a As Single = 0, bm As Single = 0, c As Single = 0
            If total > 0.0 Then
                a = CSng(hist(i).InUseBytes / total)
                bm = CSng(hist(i).ModifiedBytes / total)
                c = CSng(hist(i).StandbyBytes / total)
            End If
            a = Math.Max(0, Math.Min(1, a))
            bm = Math.Max(0, Math.Min(1 - a, bm))
            c = Math.Max(0, Math.Min(1 - a - bm, c))
            If bm * rect.Height < 1.0F Then bm = 0
            inUseTop(i) = New Vector2(x, rect.Bottom - a * rect.Height)
            modTop(i) = New Vector2(x, rect.Bottom - (a + bm) * rect.Height)
            standbyTop(i) = New Vector2(x, rect.Bottom - (a + bm + c) * rect.Height)
        Next

        填充堆叠区_DX12(context, 备用填充颜色值, standbyTop, n, rect)
        填充堆叠区_DX12(context, 已修改填充颜色值, modTop, n, rect)
        填充堆叠区_DX12(context, 已用填充颜色值, inUseTop, n, rect)

        If 图表线条颜色值.A > 0 AndAlso 图表线条粗细值 > 0 Then
            context.DrawHistory(standbyTop, n, 图表线条颜色值, 0, 图表线条粗细值 * s)
        End If
    End Sub

    Private Sub 填充堆叠区_DX12(context As D3D12_PaintContext, color As Color, topPts As Vector2(), pointCount As Integer, rect As RectangleF)
        If color.A = 0 Then Return
        If topPts Is Nothing OrElse pointCount < 2 Then Return
        context.DrawHistory(topPts, pointCount, color, rect.Bottom, 0)
    End Sub

    Private Sub 绘制背景与边框_DX12(context As D3D12_PaintContext, rect As RectangleF, s As Single)
        Dim r As Single = 圆角半径值 * s
        If _backgroundSource Is Nothing AndAlso MyBase.BackColor.A > 0 AndAlso MyBase.BackColor.A < 255 Then
            context.FillRoundedRectangle(rect, r, MyBase.BackColor)
        End If
        If 主体背景颜色值.A > 0 Then
            context.FillRoundedRectangle(rect, r, 主体背景颜色值)
        End If
    End Sub

    Private Sub 绘制禁用遮罩_DX12(context As D3D12_PaintContext)
        Dim s As Single = DpiScale()
        Dim pad As Padding = Me.Padding
        Dim rect As New RectangleF(pad.Left * s, pad.Top * s,
                                   Math.Max(0, Me.Width - (pad.Left + pad.Right) * s),
                                   Math.Max(0, Me.Height - (pad.Top + pad.Bottom) * s))
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        context.FillRoundedRectangle(rect, 圆角半径值 * s, 禁用时遮罩颜色)
    End Sub

    Private Sub 绘制文字内容_DX12(context As D3D12_PaintContext, history As HistorySnapshot)
        Dim s As Single = DpiScale()
        Dim pad As Padding = Me.Padding
        Dim rect As New RectangleF(pad.Left * s, pad.Top * s,
                                   Math.Max(0, Me.Width - (pad.Left + pad.Right) * s),
                                   Math.Max(0, Me.Height - (pad.Top + pad.Bottom) * s))
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return

        Dim innerPad As Single = 核心内边距值 * s
        Dim inner As New RectangleF(rect.X + innerPad, rect.Y + innerPad,
                                    Math.Max(0, rect.Width - innerPad * 2),
                                    Math.Max(0, rect.Height - innerPad * 2))
        If inner.Width <= 0 OrElse inner.Height <= 0 Then Return

        Dim fontHeight As Single = 文本像素高度(s)
        Dim tp As Padding = 文字内边距值
        Dim stripH As Single = fontHeight + (tp.Top + tp.Bottom) * s
        Dim topText As String = 构造槽位文字(TextSlotPosition.Top)
        Dim bottomText As String = 构造槽位文字(TextSlotPosition.Bottom)
        Dim topStripH As Single = If(Not String.IsNullOrEmpty(topText), Math.Min(stripH, inner.Height), 0)
        Dim bottomStripH As Single = If(Not String.IsNullOrEmpty(bottomText), Math.Min(stripH, Math.Max(0, inner.Height - topStripH)), 0)
        Dim graphRect As New RectangleF(inner.X, inner.Y + topStripH,
                                        inner.Width,
                                        Math.Max(0, inner.Height - topStripH - bottomStripH))

        If topStripH > 0 Then
            绘制文字_DX12(context, New RectangleF(inner.X, inner.Y, inner.Width, topStripH), topText, s, 顶部文字对齐值)
        End If
        If bottomStripH > 0 Then
            绘制文字_DX12(context, New RectangleF(inner.X, inner.Bottom - bottomStripH, inner.Width, bottomStripH), bottomText, s, 底部文字对齐值)
        End If
        If 启用悬停读数值 AndAlso _悬停有效 AndAlso graphRect.Height >= 2 AndAlso graphRect.Width >= 2 Then
            绘制悬停读数_DX12(context, graphRect, s, history)
        End If
    End Sub

    Private Sub 绘制文字_DX12(context As D3D12_PaintContext, inner As RectangleF, text As String, s As Single, align As ContentAlignment)
        If String.IsNullOrEmpty(text) Then Return
        Dim tp As Padding = 文字内边距值
        Dim textRect As New RectangleF(
            inner.X + tp.Left * s,
            inner.Y + tp.Top * s,
            Math.Max(0, inner.Width - (tp.Left + tp.Right) * s),
            Math.Max(0, inner.Height - (tp.Top + tp.Bottom) * s))
        If textRect.Width <= 0 OrElse textRect.Height <= 0 Then Return

        context.DrawText(If(text, ""), Me.Font, Me.ForeColor, textRect, s, 转文本水平对齐(align), 转文本垂直对齐(align))
    End Sub

    Private Sub 绘制悬停读数_DX12(context As D3D12_PaintContext, graphRect As RectangleF, s As Single, history As HistorySnapshot)
        Dim hist = history.Points
        Dim n As Integer = history.Count
        If n < 1 Then Return
        Dim idx As Integer = 样本索引从X坐标(_悬停X, graphRect)
        If idx < 0 OrElse idx >= n Then Return
        Dim hp = hist(idx)
        Dim total As ULong = hp.TotalBytes
        If total = 0UL Then Return

        Dim cap As Integer = Math.Max(2, 记录长度值)
        Dim step_ As Single = graphRect.Width / CSng(cap - 1)
        Dim xAtSample As Single = graphRect.Right - CSng(n - 1 - idx) * step_

        If 悬停线颜色值.A > 0 Then
            context.DrawLine(New Vector2(xAtSample, graphRect.Top), New Vector2(xAtSample, graphRect.Bottom), 悬停线颜色值, Math.Max(1.0F, 悬停线粗细值 * s))
        End If

        Dim lines As New List(Of (label As String, value As String)) From {
            (RamMonitorStrings.InUse, 格式化字节(hp.InUseBytes)),
            (RamMonitorStrings.Standby, 格式化字节(hp.StandbyBytes)),
            (RamMonitorStrings.Available, 格式化字节(hp.FreeBytes + hp.StandbyBytes))
        }

        Dim lineH As Single = Math.Max(1.0F, System.Windows.Forms.TextRenderer.MeasureText("Ag", Me.Font).Height)
        Dim maxLabel As Single = 0, maxValue As Single = 0
        For i As Integer = 0 To lines.Count - 1
            maxLabel = Math.Max(maxLabel, System.Windows.Forms.TextRenderer.MeasureText(lines(i).label, Me.Font, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine).Width)
            maxValue = Math.Max(maxValue, System.Windows.Forms.TextRenderer.MeasureText(lines(i).value, Me.Font, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine).Width)
        Next

        Dim panelPad As Single = 6 * s
        Dim gap As Single = 4 * s
        Dim innerGap As Single = 6 * s
        Dim panelW As Single = panelPad * 2 + maxLabel + innerGap + maxValue
        Dim panelH As Single = panelPad * 2 + lineH * lines.Count
        Dim showOnLeft As Boolean = (xAtSample - gap - panelW) >= graphRect.Left
        Dim panelX As Single = If(showOnLeft, xAtSample - gap - panelW, xAtSample + gap)
        If Not showOnLeft AndAlso panelX + panelW > graphRect.Right Then panelX = graphRect.Right - panelW
        If panelX < graphRect.Left Then panelX = graphRect.Left
        Dim panelY As Single = graphRect.Top + (graphRect.Height - panelH) / 2.0F
        If panelY < graphRect.Top Then panelY = graphRect.Top
        If panelY + panelH > graphRect.Bottom Then panelY = graphRect.Bottom - panelH
        Dim panelRect As New RectangleF(panelX, panelY, panelW, panelH)

        If 悬停面板背景值.A > 0 Then context.FillRectangle(panelRect, 悬停面板背景值)
        If 悬停面板边框值.A > 0 Then context.DrawRectangle(panelRect, 悬停面板边框值, 1.0F)

        Dim fore As Color = If(悬停面板前景值.A > 0, 悬停面板前景值, Me.ForeColor)
        For i As Integer = 0 To lines.Count - 1
            Dim rowY As Single = panelRect.Y + panelPad + i * lineH
            If showOnLeft Then
                绘制悬停单行_DX12(context, fore, lines(i).value, panelRect.Right - panelPad - maxLabel - innerGap - maxValue, rowY, maxValue, lineH)
                绘制悬停单行_DX12(context, fore, lines(i).label, panelRect.Right - panelPad - maxLabel, rowY, maxLabel, lineH)
            Else
                绘制悬停单行_DX12(context, fore, lines(i).label, panelRect.X + panelPad, rowY, maxLabel, lineH)
                绘制悬停单行_DX12(context, fore, lines(i).value, panelRect.X + panelPad + maxLabel + innerGap, rowY, maxValue, lineH)
            End If
        Next
    End Sub

    Private Sub 绘制悬停单行_DX12(context As D3D12_PaintContext, color As Color, text As String, x As Single, y As Single, w As Single, h As Single)
        If w <= 0 OrElse h <= 0 Then Return
        context.DrawText(If(text, ""), Me.Font, color, New RectangleF(x, y, w, h), DpiScale(), TextAlignment.Leading, ParagraphAlignment.Near)
    End Sub
End Class
