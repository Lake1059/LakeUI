Imports System.Numerics
Imports DW = Vortice.DirectWrite

Partial Public Class Ultra2DChart
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If Me.Width <= 0 OrElse Me.Height <= 0 Then Return

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), MyBase.BackColor)
        End If

        Dim layout = 获取布局()
        If layout.ChartRect.Width > 0 AndAlso layout.ChartRect.Height > 0 Then
            Using context.PushClip(layout.ChartRect)
                绘制图形层_DX12(context, layout)
                绘制文字层_DX12(context, layout)
            End Using
        End If

        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), 禁用时遮罩颜色)
        End If
    End Sub

    Private Sub 绘制图形层_DX12(context As D3D12_PaintContext, layout As ChartLayoutInfo)
        If layout Is Nothing OrElse layout.PlotRect.Width <= 0 OrElse layout.PlotRect.Height <= 0 Then Return
        Dim s As Single = layout.Scale

        If 绘图区背景颜色.A > 0 Then context.FillRectangle(layout.PlotRect, 绘图区背景颜色)

        绘制网格与坐标轴_DX12(context, layout, s)

        For Each draw In layout.SeriesDraws
            If draw.Series.ChartType = ChartSeriesTypeEnum.Column Then
                For Each col In draw.Columns
                    If col.Rect.Width <= 0 OrElse col.Rect.Height <= 0 Then Continue For
                    context.FillRoundedRectangle(col.Rect, col.CornerRadius, col.FillColor)
                    If col.BorderThickness > 0 Then context.StrokeRoundedRectangle(col.Rect, col.CornerRadius, col.BorderColor, col.BorderThickness)
                Next
            End If
        Next

        For Each draw In layout.SeriesDraws
            If draw.Series.ChartType <> ChartSeriesTypeEnum.Line Then Continue For
            For Each segment In draw.LineSegments
                If segment.Points.Count < 2 Then Continue For
                Dim points = segment.Points.Select(Function(p) New Vector2(p.X, p.Y)).ToArray()
                context.DrawHistory(points, points.Length, draw.Color, 0, draw.Series.LineThickness * s)
            Next
            For Each marker In draw.Markers
                绘制标记_DX12(context, marker)
            Next
        Next

        If 绘图区边框粗细 > 0 AndAlso 绘图区边框颜色.A > 0 Then
            context.DrawRectangle(layout.PlotRect, 绘图区边框颜色, 绘图区边框粗细 * s)
        End If

        绘制折线悬停十字线_DX12(context, layout, s)
    End Sub

    Private Sub 绘制网格与坐标轴_DX12(context As D3D12_PaintContext, layout As ChartLayoutInfo, s As Single)
        Dim plot = layout.PlotRect
        If plot.Width <= 0 OrElse plot.Height <= 0 Then Return

        If 显示水平网格线 AndAlso 网格线颜色.A > 0 AndAlso 网格线粗细 > 0 Then
            Dim gridBrush = 网格线颜色
            If gridBrush.A > 0 Then
                For Each tick In layout.TickLabels
                    context.DrawLine(New Vector2(plot.Left, tick.Y), New Vector2(plot.Right, tick.Y), gridBrush, 网格线粗细 * s)
                Next
            End If
        End If

        If 显示垂直网格线 AndAlso 网格线颜色.A > 0 AndAlso 网格线粗细 > 0 AndAlso layout.CategoryCount > 0 Then
            Dim gridBrush = 网格线颜色
            If gridBrush.A > 0 Then
                For Each x In layout.XAxisTicks
                    context.DrawLine(New Vector2(x, plot.Top), New Vector2(x, plot.Bottom), gridBrush, 网格线粗细 * s)
                Next
            End If
        End If

        If 坐标轴线颜色.A > 0 AndAlso 坐标轴线粗细 > 0 Then
            Dim axisBrush = 坐标轴线颜色
            If axisBrush.A > 0 Then
                context.DrawLine(New Vector2(plot.Left, plot.Bottom), New Vector2(plot.Right, plot.Bottom), axisBrush, 坐标轴线粗细 * s)
                context.DrawLine(New Vector2(plot.Left, plot.Top), New Vector2(plot.Left, plot.Bottom), axisBrush, 坐标轴线粗细 * s)
                If layout.ValueMin < 0 AndAlso layout.ValueMax > 0 Then
                    context.DrawLine(New Vector2(plot.Left, layout.ZeroY), New Vector2(plot.Right, layout.ZeroY), axisBrush, 坐标轴线粗细 * s)
                End If
            End If
        End If

        If 坐标轴刻度线颜色.A > 0 AndAlso 坐标轴刻度线粗细 > 0 Then
            Dim tickBrush = 坐标轴刻度线颜色
            If tickBrush.A > 0 Then
                Dim tickLen As Single = 主要刻度长度 * s
                If 显示Y轴刻度线 Then
                    For Each tick In layout.TickLabels
                        context.DrawLine(New Vector2(plot.Left - tickLen, tick.Y), New Vector2(plot.Left, tick.Y), tickBrush, 坐标轴刻度线粗细 * s)
                    Next
                End If
                If 显示X轴刻度线 Then
                    For Each x In layout.XAxisTicks
                        context.DrawLine(New Vector2(x, plot.Bottom), New Vector2(x, plot.Bottom + tickLen), tickBrush, 坐标轴刻度线粗细 * s)
                    Next
                End If
            End If
        End If
    End Sub

    Private Sub 绘制标记_DX12(context As D3D12_PaintContext, marker As MarkerDrawInfo)
        If marker.Shape = MarkerShapeEnum.None OrElse marker.Size <= 0 Then Return
        Dim half As Single = marker.Size / 2.0F
        Dim rect As New RectangleF(marker.Center.X - half, marker.Center.Y - half, marker.Size, marker.Size)
        Select Case marker.Shape
            Case MarkerShapeEnum.Circle
                context.FillEllipse(rect, marker.FillColor)
                context.StrokeEllipse(rect, marker.BorderColor, marker.BorderThickness)
            Case MarkerShapeEnum.Square
                context.FillRectangle(rect, marker.FillColor)
                context.DrawRectangle(rect, marker.BorderColor, marker.BorderThickness)
            Case MarkerShapeEnum.Diamond
                Dim points = {New PointF(marker.Center.X, rect.Top), New PointF(rect.Right, marker.Center.Y),
                              New PointF(marker.Center.X, rect.Bottom), New PointF(rect.Left, marker.Center.Y)}
                context.FillPolygon(points, marker.FillColor)
                If marker.BorderThickness > 0 AndAlso marker.BorderColor.A > 0 Then
                    For i = 0 To points.Length - 1
                        Dim first = points(i)
                        Dim last = points((i + 1) Mod points.Length)
                        context.DrawLine(New Vector2(first.X, first.Y), New Vector2(last.X, last.Y), marker.BorderColor, marker.BorderThickness)
                    Next
                End If
        End Select
    End Sub

    Private Sub 绘制文字层_DX12(context As D3D12_PaintContext, layout As ChartLayoutInfo)
        If layout Is Nothing Then Return

        If 显示标题 AndAlso Not String.IsNullOrEmpty(标题文本) Then
            绘制文本_DX12(context, 标题文本, 获取标题字体(), 标题颜色, layout.TitleRect, DW.TextAlignment.Center, DW.ParagraphAlignment.Center)
        End If

        If 显示Y轴标签 Then
            For Each tick In layout.TickLabels
                绘制文本_DX12(context, tick.Text, 获取坐标轴标签字体(), 坐标轴标签颜色, tick.TextRect, DW.TextAlignment.Trailing, DW.ParagraphAlignment.Center)
            Next
        End If

        If 显示X轴标签 Then
            For Each cat In layout.CategoryLabels
                If cat.Rotated Then
                    绘制旋转文本_DX12(context, cat.Text, 获取坐标轴标签字体(), 坐标轴标签颜色, cat.TextRect, X轴标签旋转角度)
                Else
                    绘制文本_DX12(context, cat.Text, 获取坐标轴标签字体(), 坐标轴标签颜色, cat.TextRect, DW.TextAlignment.Center, DW.ParagraphAlignment.Near)
                End If
            Next
        End If

        If Not String.IsNullOrEmpty(X轴标题文本) Then
            绘制文本_DX12(context, X轴标题文本, 获取坐标轴标题字体(), 坐标轴标题颜色, layout.XAxisTitleRect, DW.TextAlignment.Center, DW.ParagraphAlignment.Center)
        End If

        If Not String.IsNullOrEmpty(Y轴标题文本) Then
            绘制垂直标题_DX12(context, Y轴标题文本, 获取坐标轴标题字体(), 坐标轴标题颜色, layout.YAxisTitleRect)
        End If

        If 图例位置 <> LegendPositionEnum.None Then
            绘制图例_DX12(context, layout)
        End If

        For Each label In layout.ValueLabels
            绘制文本_DX12(context, label.Text, 获取值标签字体(), label.Color, label.Rect, DW.TextAlignment.Center, DW.ParagraphAlignment.Center)
        Next

    End Sub

    Private Sub 绘制折线悬停十字线_DX12(context As D3D12_PaintContext, layout As ChartLayoutInfo, s As Single)
        Dim hoverTarget = 获取折线悬停目标(layout)
        If Not hoverTarget.HasTarget Then Return

        Dim plot = layout.PlotRect
        Dim lineBrush = 折线悬停十字线颜色
        If lineBrush.A > 0 AndAlso 折线悬停十字线粗细 > 0 Then
            context.DrawLine(New Vector2(hoverTarget.Point.X, plot.Top), New Vector2(hoverTarget.Point.X, plot.Bottom), lineBrush, 折线悬停十字线粗细 * s)
            context.DrawLine(New Vector2(plot.Left, hoverTarget.Point.Y), New Vector2(plot.Right, hoverTarget.Point.Y), lineBrush, 折线悬停十字线粗细 * s)
        End If

        Dim highlightBrush = hoverTarget.Color
        Dim borderBrush = 折线悬停吸附点边框颜色
        Dim radius As Single = Math.Max(2.0F, 折线悬停吸附点半径 * s)
        Dim ellipse As New RectangleF(hoverTarget.Point.X - radius, hoverTarget.Point.Y - radius, radius * 2, radius * 2)
        context.FillEllipse(ellipse, highlightBrush)
        context.StrokeEllipse(ellipse, borderBrush, 折线悬停吸附点边框粗细 * s)
    End Sub

    Private Sub 绘制图例_DX12(context As D3D12_PaintContext, layout As ChartLayoutInfo)
        For Each item In layout.LegendItems
            Dim markerBrush = item.Color
            If markerBrush.A > 0 Then
                If item.Series.ChartType = ChartSeriesTypeEnum.Column Then
                    context.FillRectangle(item.MarkerRect, markerBrush)
                Else
                    Dim cy As Single = item.MarkerRect.Y + item.MarkerRect.Height / 2.0F
                    context.DrawLine(New Vector2(item.MarkerRect.Left, cy), New Vector2(item.MarkerRect.Right, cy), markerBrush, Math.Max(1.0F, item.Series.LineThickness * layout.Scale))
                    Dim r As Single = Math.Min(item.MarkerRect.Width, item.MarkerRect.Height) / 3.0F
                    context.FillEllipse(New RectangleF(item.MarkerRect.X + item.MarkerRect.Width / 2 - r, cy - r, 2 * r, 2 * r), markerBrush)
                End If
            End If
            绘制文本_DX12(context, item.Text, 获取图例字体(), 图例文字颜色, item.TextRect, DW.TextAlignment.Leading, DW.ParagraphAlignment.Center)
        Next
    End Sub

    Private Sub 绘制文本_DX12(context As D3D12_PaintContext, text As String, font As Font, color As Color, rect As RectangleF, align As DW.TextAlignment, paraAlign As DW.ParagraphAlignment)
        If String.IsNullOrEmpty(text) OrElse color.A <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        context.DrawText(text, font, color, rect, DpiScale(), align, paraAlign)
    End Sub

    Private Sub 绘制旋转文本_DX12(context As D3D12_PaintContext, text As String, font As Font, color As Color, rect As RectangleF, angle As Single)
        If String.IsNullOrEmpty(text) OrElse color.A <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim rad = angle * CSng(Math.PI / 180.0)
        Dim anchor As New Vector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2)
        context.DrawTransformedText(text, font, color, rect, DpiScale(), Matrix3x2.CreateRotation(rad, anchor))
    End Sub

    Private Sub 绘制垂直标题_DX12(context As D3D12_PaintContext, text As String, font As Font, color As Color, rect As RectangleF)
        Dim transform = Matrix3x2.CreateRotation(-CSng(Math.PI / 2.0F)) * Matrix3x2.CreateTranslation(rect.X, rect.Bottom)
        context.DrawTransformedText(text, font, color, New RectangleF(0, 0, rect.Height, rect.Width), DpiScale(), transform)
    End Sub
End Class
