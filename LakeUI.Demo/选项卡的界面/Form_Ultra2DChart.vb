Public Class Form_Ultra2DChart
    Private ReadOnly 随机数 As New Random(1059)
    Private ReadOnly 图例位置列表 As Ultra2DChart.LegendPositionEnum() = {
        Ultra2DChart.LegendPositionEnum.Bottom,
        Ultra2DChart.LegendPositionEnum.Top,
        Ultra2DChart.LegendPositionEnum.Right,
        Ultra2DChart.LegendPositionEnum.None
    }
    Private 图例位置索引 As Integer = 0
    Private 显示值标签 As Boolean = True

    Private Sub Form_Ultra2DChart_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        初始化图表样式()
        载入示例数据(False)
    End Sub

    Private Sub 初始化图表样式()
        Ultra2DChart1.Title = "示例业务趋势"
        Ultra2DChart1.XAxisTitle = "月份"
        Ultra2DChart1.YAxisTitle = "指数"
        Ultra2DChart1.YAxisLabelFormat = "0"
        Ultra2DChart1.ValueLabelFormat = "0"
        Ultra2DChart1.ShowValueLabels = 显示值标签
        Ultra2DChart1.LegendPosition = 图例位置列表(图例位置索引)
        Ultra2DChart1.ShowVerticalGridLines = False
        Ultra2DChart1.ColumnGroupWidthRatio = 0.66F
        Ultra2DChart1.ColumnSeriesSpacing = 4.0F
    End Sub

    Private Sub 载入示例数据(使用随机扰动 As Boolean)
        Dim 销售额() As Double = {26, 34, 31, 45, 52, 61, 58, 72}
        Dim 活跃度() As Double = {18, 25, 29, 37, 41, 48, 55, 63}
        Dim 满意度() As Double = {72, 75, 73, 78, 82, 84, 86, 88}

        If 使用随机扰动 Then
            For i As Integer = 0 To 销售额.Length - 1
                销售额(i) += 随机数.Next(-6, 9)
                活跃度(i) += 随机数.Next(-5, 8)
                满意度(i) += 随机数.Next(-3, 4)
            Next
        End If

        Ultra2DChart1.BeginUpdate()
        Try
            Ultra2DChart1.ClearData()
            Ultra2DChart1.SetCategories("1月", "2月", "3月", "4月", "5月", "6月", "7月", "8月")

            Dim 柱状 = Ultra2DChart1.AddSeries("销售额", Ultra2DChart.ChartSeriesTypeEnum.Column, 销售额)
            柱状.Color = Color.FromArgb(88, 166, 255)
            柱状.GradientColor = Color.FromArgb(132, 225, 188)
            柱状.BorderThickness = 1.0F
            柱状.ColumnCornerRadius = 4.0F

            Dim 第二柱状 = Ultra2DChart1.AddSeries("活跃度", Ultra2DChart.ChartSeriesTypeEnum.Column, 活跃度)
            第二柱状.Color = Color.FromArgb(255, 184, 108)
            第二柱状.GradientColor = Color.FromArgb(255, 111, 145)
            第二柱状.BorderThickness = 1.0F
            第二柱状.ColumnCornerRadius = 4.0F

            Dim 折线 = Ultra2DChart1.AddSeries("满意度", Ultra2DChart.ChartSeriesTypeEnum.Line, 满意度)
            折线.Color = Color.FromArgb(198, 120, 221)
            折线.LineThickness = 3.0F
            折线.MarkerShape = Ultra2DChart.MarkerShapeEnum.Diamond
            折线.MarkerSize = 8.0F
            折线.MarkerBorderThickness = 2.0F
            折线.ShowValueLabels = Ultra2DChart.SeriesValueLabelModeEnum.Show
        Finally
            Ultra2DChart1.EndUpdate()
        End Try
    End Sub

    Private Sub ModernButton5_Click(sender As Object, e As EventArgs) Handles ModernButton5.Click
        载入示例数据(True)
    End Sub

    Private Sub ModernButton6_Click(sender As Object, e As EventArgs) Handles ModernButton6.Click
        显示值标签 = Not 显示值标签
        Ultra2DChart1.ShowValueLabels = 显示值标签
        ModernButton6.Text = If(显示值标签, "值标签：开启", "值标签：关闭")
    End Sub

    Private Sub ModernButton7_Click(sender As Object, e As EventArgs) Handles ModernButton7.Click
        图例位置索引 = (图例位置索引 + 1) Mod 图例位置列表.Length
        Ultra2DChart1.LegendPosition = 图例位置列表(图例位置索引)
        ModernButton7.Text = "图例：" & Ultra2DChart1.LegendPosition.ToString()
    End Sub
End Class
