Public Class Form_MemberWall
    Private Sub Form_MemberWall_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        初始化成员()
    End Sub

    Private Sub 初始化成员()
        MemberWall1.Items.BeginUpdate()
        Try
            MemberWall1.Items.Clear()

            Dim 核心 = MemberWall1.Items.Add("Core Team")
            核心.ForeColor = Color.White
            核心.BackColor = Color.FromArgb(88, 166, 255)
            核心.BorderColor = Color.FromArgb(150, 210, 255)

            Dim 维护者 = MemberWall1.Items.Add("Maintainers")
            维护者.ForeColor = Color.White
            维护者.BackColor = Color.FromArgb(198, 120, 221)
            维护者.BorderColor = Color.FromArgb(225, 176, 240)

            MemberWall1.Items.AddRange(New String() {
                "LakeUI", "1059 Studio", "Direct2D", "WinForms", "Designer",
                "Rendering", "Animation", "Tooling", "Documentation", "Testing",
                "Community", "Sponsor", "Contributor", "Preview", "Release",
                "ModernButton", "ModernTextBox", "UltraDetailListView", "Ultra2DChart",
                "MemberWall", "EasyStatesPanel", "Notifications", "Performance",
                "Accessibility", "Theme", "Packaging", "Examples", "Core Controls"
            })
        Finally
            MemberWall1.Items.EndUpdate()
        End Try

        MemberWall1.Redraw()
    End Sub

    Private Sub ModernTextBox1_TextChanged(sender As Object, e As EventArgs) Handles ModernTextBox1.TextChanged
        MemberWall1.Search(ModernTextBox1.Text)
    End Sub

    Private Sub ModernButton5_Click(sender As Object, e As EventArgs) Handles ModernButton5.Click
        ModernTextBox1.Text = ""
        MemberWall1.Enabled = True
        ModernButton7.Text = "禁用态"
    End Sub

    Private Sub ModernButton6_Click(sender As Object, e As EventArgs) Handles ModernButton6.Click
        ModernTextBox1.Text = "Core"
    End Sub

    Private Sub ModernButton7_Click(sender As Object, e As EventArgs) Handles ModernButton7.Click
        MemberWall1.Enabled = Not MemberWall1.Enabled
        ModernButton7.Text = If(MemberWall1.Enabled, "禁用态", "启用")
    End Sub

    Private Sub MemberWall1_ItemClick(sender As Object, e As MemberWall.MemberItemEventArgs) Handles MemberWall1.ItemClick
        Label7.Text = $"已点击：{e.Item.Text}（Index {e.Index}）"
    End Sub
End Class
