Public Class Form_EasyStatesPanel
    Private 状态序号 As Integer = 0
    Private 紧凑模式 As Boolean = False

    Private Sub Form_EasyStatesPanel_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        初始化状态()
    End Sub

    Private Sub 初始化状态()
        EasyStatesPanel1.Items.BeginUpdate()
        Try
            EasyStatesPanel1.Items.Clear()
            EasyStatesPanel1.Items.Add("构建", "net10.0-windows / Debug")
            EasyStatesPanel1.Items.Add("渲染", "V3 GPU 已就绪")
            EasyStatesPanel1.Items.Add("动画", "可选平滑滚动")
            EasyStatesPanel1.Items.Add("主题", "窗口级 GPU 合成")
            EasyStatesPanel1.Items.Add("缓存", "文本格式缓存命中")
            EasyStatesPanel1.Items.Add("输入", "鼠标滚轮可滚动")
            EasyStatesPanel1.Items.Add("资源", "按需创建与释放")
            EasyStatesPanel1.Items.Add("状态", "轻量卡片布局")
            EasyStatesPanel1.Items.Add("示例", "可运行时追加")
            EasyStatesPanel1.Items.Add("封装", "属性面板友好")
            EasyStatesPanel1.Items.Add("滚动条", "自绘 Hover 状态")
            EasyStatesPanel1.Items.Add("禁用", "覆盖遮罩支持")
            状态序号 = EasyStatesPanel1.Items.Count
        Finally
            EasyStatesPanel1.Items.EndUpdate()
        End Try

        更新状态文本()
        EasyStatesPanel1.Redraw()
    End Sub

    Private Sub ModernButton5_Click(sender As Object, e As EventArgs) Handles ModernButton5.Click
        状态序号 += 1
        EasyStatesPanel1.Items.Add("任务 " & 状态序号.ToString("00"), "运行时追加的状态卡片")
        EasyStatesPanel1.Redraw()
        更新状态文本()
    End Sub

    Private Sub ModernButton6_Click(sender As Object, e As EventArgs) Handles ModernButton6.Click
        紧凑模式 = Not 紧凑模式
        EasyStatesPanel1.CardPreferredWidth = If(紧凑模式, 132, 178)
        EasyStatesPanel1.CardMinWidth = If(紧凑模式, 100, 120)
        ModernButton6.Text = If(紧凑模式, "舒展宽度", "紧凑宽度")
        EasyStatesPanel1.Redraw()
    End Sub

    Private Sub ModernButton7_Click(sender As Object, e As EventArgs) Handles ModernButton7.Click
        EasyStatesPanel1.SmoothScroll = Not EasyStatesPanel1.SmoothScroll
        ModernButton7.Text = If(EasyStatesPanel1.SmoothScroll, "平滑滚动：开", "平滑滚动：关")
    End Sub

    Private Sub 更新状态文本()
        Label7.Text = "当前状态卡片：" & EasyStatesPanel1.Items.Count & " 项"
    End Sub
End Class
