<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form_ExcellentTrackBar
    Inherits System.Windows.Forms.Form

    'Form 重写 Dispose，以清理组件列表。
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Windows 窗体设计器所必需的
    Private components As System.ComponentModel.IContainer

    '注意: 以下过程是 Windows 窗体设计器所必需的
    '可以使用 Windows 窗体设计器修改它。  
    '不要使用代码编辑器修改它。
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Dim TrackLabel1 As LakeUI.ExcellentTrackBar.TrackLabel = New ExcellentTrackBar.TrackLabel()
        Dim TrackLabel2 As LakeUI.ExcellentTrackBar.TrackLabel = New ExcellentTrackBar.TrackLabel()
        Dim TrackLabel3 As LakeUI.ExcellentTrackBar.TrackLabel = New ExcellentTrackBar.TrackLabel()
        Dim TrackLabel4 As LakeUI.ExcellentTrackBar.TrackLabel = New ExcellentTrackBar.TrackLabel()
        Dim TrackLabel5 As LakeUI.ExcellentTrackBar.TrackLabel = New ExcellentTrackBar.TrackLabel()
        Dim TrackLabel6 As LakeUI.ExcellentTrackBar.TrackLabel = New ExcellentTrackBar.TrackLabel()
        Dim TrackLabel7 As LakeUI.ExcellentTrackBar.TrackLabel = New ExcellentTrackBar.TrackLabel()
        ModernPanel1 = New ModernPanel()
        Label9 = New Label()
        Panel4 = New Panel()
        ExcellentTrackBar3 = New ExcellentTrackBar()
        Label7 = New Label()
        Panel3 = New Panel()
        ExcellentTrackBar2 = New ExcellentTrackBar()
        Label5 = New Label()
        Panel2 = New Panel()
        ExcellentTrackBar1 = New ExcellentTrackBar()
        Label6 = New Label()
        Panel1 = New Panel()
        ModernButton4 = New ModernButton()
        Label4 = New Label()
        ModernButton3 = New ModernButton()
        Label3 = New Label()
        ModernButton2 = New ModernButton()
        Label2 = New Label()
        ModernButton1 = New ModernButton()
        Label1 = New Label()
        ModernPanel1.SuspendLayout()
        Panel4.SuspendLayout()
        Panel3.SuspendLayout()
        Panel2.SuspendLayout()
        Panel1.SuspendLayout()
        SuspendLayout()
        ' 
        ' ModernPanel1
        ' 
        ModernPanel1.BorderSize = 0
        ModernPanel1.Controls.Add(Label9)
        ModernPanel1.Controls.Add(Panel4)
        ModernPanel1.Controls.Add(Label7)
        ModernPanel1.Controls.Add(Panel3)
        ModernPanel1.Controls.Add(Label5)
        ModernPanel1.Controls.Add(Panel2)
        ModernPanel1.Controls.Add(Label6)
        ModernPanel1.Controls.Add(Panel1)
        ModernPanel1.Controls.Add(Label1)
        ModernPanel1.Dock = DockStyle.Fill
        ModernPanel1.Location = New Point(0, 0)
        ModernPanel1.Margin = New Padding(2)
        ModernPanel1.Name = "ModernPanel1"
        ModernPanel1.Padding = New Padding(20)
        ModernPanel1.Size = New Size(661, 553)
        ModernPanel1.TabIndex = 39
        ' 
        ' Label9
        ' 
        Label9.AutoSize = True
        Label9.Dock = DockStyle.Top
        Label9.Font = New Font("Microsoft YaHei UI", 10F)
        Label9.ForeColor = Color.Gray
        Label9.Location = New Point(20, 430)
        Label9.Name = "Label9"
        Label9.Size = New Size(471, 20)
        Label9.TabIndex = 45
        Label9.Text = "此控件的动画对帧率要求较高，如果目标场景性能允许可以考虑开无限帧率"
        ' 
        ' Panel4
        ' 
        Panel4.Controls.Add(ExcellentTrackBar3)
        Panel4.Dock = DockStyle.Top
        Panel4.Location = New Point(20, 330)
        Panel4.Name = "Panel4"
        Panel4.Size = New Size(621, 100)
        Panel4.TabIndex = 44
        ' 
        ' ExcellentTrackBar3
        ' 
        ExcellentTrackBar3.AnimationDuration = 500
        ExcellentTrackBar3.AnimationFPS = 0
        ExcellentTrackBar3.BackColor = Color.FromArgb(CByte(36), CByte(36), CByte(36))
        ExcellentTrackBar3.Dock = DockStyle.Left
        ExcellentTrackBar3.LabelFont = New Font("MiSans Medium", 9F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ExcellentTrackBar3.LabelLineLength = 15
        TrackLabel2.Position = 1
        TrackLabel2.Side = ExcellentTrackBar.LabelSideEnum.TopOrLeft
        TrackLabel3.Position = 2
        TrackLabel4.Position = 3
        TrackLabel4.Side = ExcellentTrackBar.LabelSideEnum.TopOrLeft
        TrackLabel5.Position = 4
        TrackLabel6.Position = 5
        TrackLabel6.Side = ExcellentTrackBar.LabelSideEnum.TopOrLeft
        TrackLabel7.Position = 6
        ExcellentTrackBar3.Labels.Add(TrackLabel1)
        ExcellentTrackBar3.Labels.Add(TrackLabel2)
        ExcellentTrackBar3.Labels.Add(TrackLabel3)
        ExcellentTrackBar3.Labels.Add(TrackLabel4)
        ExcellentTrackBar3.Labels.Add(TrackLabel5)
        ExcellentTrackBar3.Labels.Add(TrackLabel6)
        ExcellentTrackBar3.Labels.Add(TrackLabel7)
        ExcellentTrackBar3.Location = New Point(0, 0)
        ExcellentTrackBar3.Margin = New Padding(2, 2, 2, 2)
        ExcellentTrackBar3.Maximum = 6
        ExcellentTrackBar3.Name = "ExcellentTrackBar3"
        ExcellentTrackBar3.Size = New Size(416, 100)
        ExcellentTrackBar3.StringItems.Add("veryslow")
        ExcellentTrackBar3.StringItems.Add("slower")
        ExcellentTrackBar3.StringItems.Add("slow")
        ExcellentTrackBar3.StringItems.Add("medium")
        ExcellentTrackBar3.StringItems.Add("fast")
        ExcellentTrackBar3.StringItems.Add("faster")
        ExcellentTrackBar3.StringItems.Add("veryfast")
        ExcellentTrackBar3.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        ExcellentTrackBar3.TabIndex = 24
        ExcellentTrackBar3.ThumbBorderWidth = 0
        ExcellentTrackBar3.ThumbColor = Color.IndianRed
        ExcellentTrackBar3.ThumbHeight = 28
        ExcellentTrackBar3.ThumbRadius = 5
        ExcellentTrackBar3.ThumbTextMode = ExcellentTrackBar.ThumbTextModeEnum.StringItem
        ExcellentTrackBar3.ThumbWidth = 80
        ExcellentTrackBar3.UseStringItems = True
        ExcellentTrackBar3.Value = 3
        ' 
        ' Label7
        ' 
        Label7.AutoSize = True
        Label7.Dock = DockStyle.Top
        Label7.Font = New Font("Microsoft YaHei UI", 10F)
        Label7.Location = New Point(20, 280)
        Label7.Name = "Label7"
        Label7.Padding = New Padding(0, 20, 0, 10)
        Label7.Size = New Size(331, 50)
        Label7.TabIndex = 43
        Label7.Text = "高阶场景，显示当前值和指定刻度线，无限动画帧率"
        ' 
        ' Panel3
        ' 
        Panel3.Controls.Add(ExcellentTrackBar2)
        Panel3.Dock = DockStyle.Top
        Panel3.Location = New Point(20, 250)
        Panel3.Name = "Panel3"
        Panel3.Size = New Size(621, 30)
        Panel3.TabIndex = 42
        ' 
        ' ExcellentTrackBar2
        ' 
        ExcellentTrackBar2.AnimationDuration = 500
        ExcellentTrackBar2.AnimationFPS = 120
        ExcellentTrackBar2.Dock = DockStyle.Left
        ExcellentTrackBar2.Location = New Point(0, 0)
        ExcellentTrackBar2.Margin = New Padding(2, 2, 2, 2)
        ExcellentTrackBar2.Name = "ExcellentTrackBar2"
        ExcellentTrackBar2.Size = New Size(288, 30)
        ExcellentTrackBar2.TabIndex = 1
        ExcellentTrackBar2.ThumbBorderWidth = 0
        ExcellentTrackBar2.ThumbColor = Color.FromArgb(CByte(255), CByte(128), CByte(0))
        ExcellentTrackBar2.ThumbRadius = 10
        ExcellentTrackBar2.Value = 50
        ' 
        ' Label5
        ' 
        Label5.AutoSize = True
        Label5.Dock = DockStyle.Top
        Label5.Font = New Font("Microsoft YaHei UI", 10F)
        Label5.Location = New Point(20, 200)
        Label5.Name = "Label5"
        Label5.Padding = New Padding(0, 20, 0, 10)
        Label5.Size = New Size(239, 50)
        Label5.TabIndex = 41
        Label5.Text = "拉大圆角半径可变成圆，动画 120fps"
        ' 
        ' Panel2
        ' 
        Panel2.Controls.Add(ExcellentTrackBar1)
        Panel2.Dock = DockStyle.Top
        Panel2.Location = New Point(20, 170)
        Panel2.Name = "Panel2"
        Panel2.Size = New Size(621, 30)
        Panel2.TabIndex = 40
        ' 
        ' ExcellentTrackBar1
        ' 
        ExcellentTrackBar1.Dock = DockStyle.Left
        ExcellentTrackBar1.Location = New Point(0, 0)
        ExcellentTrackBar1.Margin = New Padding(2, 2, 2, 2)
        ExcellentTrackBar1.Name = "ExcellentTrackBar1"
        ExcellentTrackBar1.Size = New Size(288, 30)
        ExcellentTrackBar1.TabIndex = 0
        ExcellentTrackBar1.Value = 50
        ' 
        ' Label6
        ' 
        Label6.AutoSize = True
        Label6.Dock = DockStyle.Top
        Label6.Font = New Font("Microsoft YaHei UI", 10F)
        Label6.Location = New Point(20, 120)
        Label6.Name = "Label6"
        Label6.Padding = New Padding(0, 20, 0, 10)
        Label6.Size = New Size(345, 50)
        Label6.TabIndex = 38
        Label6.Text = "默认样式，默认动画关闭；竖向模式也有，此处不演示"
        ' 
        ' Panel1
        ' 
        Panel1.Controls.Add(ModernButton4)
        Panel1.Controls.Add(Label4)
        Panel1.Controls.Add(ModernButton3)
        Panel1.Controls.Add(Label3)
        Panel1.Controls.Add(ModernButton2)
        Panel1.Controls.Add(Label2)
        Panel1.Controls.Add(ModernButton1)
        Panel1.Dock = DockStyle.Top
        Panel1.Location = New Point(20, 70)
        Panel1.Name = "Panel1"
        Panel1.Size = New Size(621, 50)
        Panel1.TabIndex = 35
        ' 
        ' ModernButton4
        ' 
        ModernButton4.BackColor1 = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        ModernButton4.BorderRadius = 10
        ModernButton4.BorderSize = 0
        ModernButton4.Dock = DockStyle.Left
        ModernButton4.Font = New Font("Microsoft YaHei UI", 10.8F, FontStyle.Bold, GraphicsUnit.Point, CByte(134))
        ModernButton4.ForeColor = Color.YellowGreen
        ModernButton4.Location = New Point(424, 0)
        ModernButton4.Margin = New Padding(2)
        ModernButton4.Name = "ModernButton4"
        ModernButton4.Size = New Size(80, 50)
        ModernButton4.SubText = "性能负载"
        ModernButton4.TabIndex = 6
        ModernButton4.Text = "低"
        ModernButton4.TextAlign = ModernButton.TextAlignEnum.Left
        ' 
        ' Label4
        ' 
        Label4.Dock = DockStyle.Left
        Label4.Location = New Point(416, 0)
        Label4.Margin = New Padding(2, 0, 2, 0)
        Label4.Name = "Label4"
        Label4.Size = New Size(8, 50)
        Label4.TabIndex = 5
        ' 
        ' ModernButton3
        ' 
        ModernButton3.BackColor1 = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        ModernButton3.BorderRadius = 10
        ModernButton3.BorderSize = 0
        ModernButton3.Dock = DockStyle.Left
        ModernButton3.Font = New Font("Microsoft YaHei UI", 10.8F, FontStyle.Bold, GraphicsUnit.Point, CByte(134))
        ModernButton3.ForeColor = Color.Orchid
        ModernButton3.Location = New Point(296, 0)
        ModernButton3.Margin = New Padding(2)
        ModernButton3.Name = "ModernButton3"
        ModernButton3.Size = New Size(120, 50)
        ModernButton3.SubText = "动画支持"
        ModernButton3.TabIndex = 4
        ModernButton3.Text = "位移动画"
        ModernButton3.TextAlign = ModernButton.TextAlignEnum.Left
        ' 
        ' Label3
        ' 
        Label3.Dock = DockStyle.Left
        Label3.Location = New Point(288, 0)
        Label3.Margin = New Padding(2, 0, 2, 0)
        Label3.Name = "Label3"
        Label3.Size = New Size(8, 50)
        Label3.TabIndex = 3
        ' 
        ' ModernButton2
        ' 
        ModernButton2.BackColor1 = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        ModernButton2.BorderRadius = 10
        ModernButton2.BorderSize = 0
        ModernButton2.Dock = DockStyle.Left
        ModernButton2.Font = New Font("Microsoft YaHei UI", 10.8F, FontStyle.Bold, GraphicsUnit.Point, CByte(134))
        ModernButton2.Location = New Point(128, 0)
        ModernButton2.Margin = New Padding(2)
        ModernButton2.Name = "ModernButton2"
        ModernButton2.Size = New Size(160, 50)
        ModernButton2.SubText = "技术偏好"
        ModernButton2.TabIndex = 2
        ModernButton2.Text = "曹操科技"
        ModernButton2.TextAlign = ModernButton.TextAlignEnum.Left
        ' 
        ' Label2
        ' 
        Label2.Dock = DockStyle.Left
        Label2.Location = New Point(120, 0)
        Label2.Margin = New Padding(2, 0, 2, 0)
        Label2.Name = "Label2"
        Label2.Size = New Size(8, 50)
        Label2.TabIndex = 1
        ' 
        ' ModernButton1
        ' 
        ModernButton1.BackColor1 = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        ModernButton1.BorderRadius = 10
        ModernButton1.BorderSize = 0
        ModernButton1.Dock = DockStyle.Left
        ModernButton1.Font = New Font("Microsoft YaHei UI", 10.8F, FontStyle.Bold, GraphicsUnit.Point, CByte(134))
        ModernButton1.ForeColor = Color.YellowGreen
        ModernButton1.Location = New Point(0, 0)
        ModernButton1.Margin = New Padding(2)
        ModernButton1.Name = "ModernButton1"
        ModernButton1.Size = New Size(120, 50)
        ModernButton1.SubText = "制作类型"
        ModernButton1.TabIndex = 0
        ModernButton1.Text = "全新绘制"
        ModernButton1.TextAlign = ModernButton.TextAlignEnum.Left
        ' 
        ' Label1
        ' 
        Label1.AutoSize = True
        Label1.Dock = DockStyle.Top
        Label1.Font = New Font("Microsoft YaHei UI", 16.2F, FontStyle.Bold, GraphicsUnit.Point, CByte(134))
        Label1.Location = New Point(20, 20)
        Label1.Name = "Label1"
        Label1.Padding = New Padding(0, 0, 0, 20)
        Label1.Size = New Size(349, 50)
        Label1.TabIndex = 34
        Label1.Text = "极好的滑动条 ExcellentTrackBar"
        ' 
        ' Form_ExcellentTrackBar
        ' 
        AutoScaleDimensions = New SizeF(96F, 96F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(36), CByte(36), CByte(36))
        ClientSize = New Size(661, 553)
        Controls.Add(ModernPanel1)
        Font = New Font("Microsoft YaHei UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ForeColor = Color.Silver
        Name = "Form_ExcellentTrackBar"
        Text = "Form_ExcellentTrackBar"
        ModernPanel1.ResumeLayout(False)
        ModernPanel1.PerformLayout()
        Panel4.ResumeLayout(False)
        Panel3.ResumeLayout(False)
        Panel2.ResumeLayout(False)
        Panel1.ResumeLayout(False)
        ResumeLayout(False)
    End Sub

    Friend WithEvents ModernPanel1 As ModernPanel
    Friend WithEvents Panel3 As Panel
    Friend WithEvents ExcellentTrackBar2 As ExcellentTrackBar
    Friend WithEvents Label5 As Label
    Friend WithEvents Panel2 As Panel
    Friend WithEvents ExcellentTrackBar1 As ExcellentTrackBar
    Friend WithEvents Label6 As Label
    Friend WithEvents Panel1 As Panel
    Friend WithEvents ModernButton4 As ModernButton
    Friend WithEvents Label4 As Label
    Friend WithEvents ModernButton3 As ModernButton
    Friend WithEvents Label3 As Label
    Friend WithEvents ModernButton2 As ModernButton
    Friend WithEvents Label2 As Label
    Friend WithEvents ModernButton1 As ModernButton
    Friend WithEvents Label1 As Label
    Friend WithEvents Panel4 As Panel
    Friend WithEvents Label7 As Label
    Friend WithEvents ExcellentTrackBar3 As ExcellentTrackBar
    Friend WithEvents Label9 As Label
End Class
