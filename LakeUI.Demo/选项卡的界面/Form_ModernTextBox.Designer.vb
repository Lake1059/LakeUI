<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form_ModernTextBox
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
        ModernPanel1 = New ModernPanel()
        Panel3 = New Panel()
        HtmlColorLabel2 = New HtmlColorLabel()
        HtmlColorLabel1 = New HtmlColorLabel()
        ModernTextBox2 = New ModernTextBox()
        Label5 = New Label()
        Panel2 = New Panel()
        ModernTextBox1 = New ModernTextBox()
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
        Panel3.SuspendLayout()
        Panel2.SuspendLayout()
        Panel1.SuspendLayout()
        SuspendLayout()
        ' 
        ' ModernPanel1
        ' 
        ModernPanel1.BorderSize = 0
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
        ModernPanel1.Size = New Size(753, 604)
        ModernPanel1.TabIndex = 35
        ' 
        ' Panel3
        ' 
        Panel3.Controls.Add(HtmlColorLabel2)
        Panel3.Controls.Add(HtmlColorLabel1)
        Panel3.Controls.Add(ModernTextBox2)
        Panel3.Dock = DockStyle.Fill
        Panel3.Location = New Point(20, 255)
        Panel3.Name = "Panel3"
        Panel3.Size = New Size(713, 329)
        Panel3.TabIndex = 42
        ' 
        ' HtmlColorLabel2
        ' 
        HtmlColorLabel2.AutoSize = True
        HtmlColorLabel2.AutoSizeMode = AutoSizeMode.GrowAndShrink
        HtmlColorLabel2.Dock = DockStyle.Top
        HtmlColorLabel2.Font = New Font("Microsoft YaHei UI", 10F)
        HtmlColorLabel2.Location = New Point(288, 61)
        HtmlColorLabel2.Margin = New Padding(2, 2, 2, 2)
        HtmlColorLabel2.Name = "HtmlColorLabel2"
        HtmlColorLabel2.Padding = New Padding(20, 0, 0, 20)
        HtmlColorLabel2.Size = New Size(425, 81)
        HtmlColorLabel2.TabIndex = 41
        HtmlColorLabel2.Text = "完美支持输入法，定制所有颜色包括选中焦点，支持设定撤回次数；还支持一个轻量的富文本功能，专为日志输出调优的行插入功能等。"
        ' 
        ' HtmlColorLabel1
        ' 
        HtmlColorLabel1.AutoSize = True
        HtmlColorLabel1.AutoSizeMode = AutoSizeMode.GrowAndShrink
        HtmlColorLabel1.Dock = DockStyle.Top
        HtmlColorLabel1.Font = New Font("Microsoft YaHei UI", 10F)
        HtmlColorLabel1.Location = New Point(288, 0)
        HtmlColorLabel1.Margin = New Padding(2, 2, 2, 2)
        HtmlColorLabel1.Name = "HtmlColorLabel1"
        HtmlColorLabel1.Padding = New Padding(20, 0, 0, 20)
        HtmlColorLabel1.Size = New Size(425, 61)
        HtmlColorLabel1.TabIndex = 40
        HtmlColorLabel1.Text = "这是真正从头制作的文本框，没有依赖原版 TextBox，因此性能占用会比原版高一些，但换来了极致的输入体验。"
        ' 
        ' ModernTextBox2
        ' 
        ModernTextBox2.BorderRadius = 10
        ModernTextBox2.BorderSize = 2
        ModernTextBox2.CaretColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        ModernTextBox2.Dock = DockStyle.Left
        ModernTextBox2.Font = New Font("Microsoft YaHei UI", 10F)
        ModernTextBox2.Location = New Point(0, 0)
        ModernTextBox2.Margin = New Padding(2, 2, 2, 2)
        ModernTextBox2.MultiLine = True
        ModernTextBox2.Name = "ModernTextBox2"
        ModernTextBox2.Padding = New Padding(10)
        ModernTextBox2.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernTextBox2.Size = New Size(288, 329)
        ModernTextBox2.TabIndex = 39
        ModernTextBox2.WaterText = "ModernTextBox2"
        ' 
        ' Label5
        ' 
        Label5.AutoSize = True
        Label5.Dock = DockStyle.Top
        Label5.Font = New Font("Microsoft YaHei UI", 10F)
        Label5.Location = New Point(20, 205)
        Label5.Name = "Label5"
        Label5.Padding = New Padding(0, 20, 0, 10)
        Label5.Size = New Size(65, 50)
        Label5.TabIndex = 41
        Label5.Text = "多行模式"
        ' 
        ' Panel2
        ' 
        Panel2.Controls.Add(ModernTextBox1)
        Panel2.Dock = DockStyle.Top
        Panel2.Location = New Point(20, 170)
        Panel2.Name = "Panel2"
        Panel2.Size = New Size(713, 35)
        Panel2.TabIndex = 40
        ' 
        ' ModernTextBox1
        ' 
        ModernTextBox1.BorderRadius = 10
        ModernTextBox1.BorderSize = 2
        ModernTextBox1.CaretColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        ModernTextBox1.Dock = DockStyle.Left
        ModernTextBox1.Font = New Font("Microsoft YaHei UI", 10F)
        ModernTextBox1.Location = New Point(0, 0)
        ModernTextBox1.Margin = New Padding(2, 2, 2, 2)
        ModernTextBox1.Name = "ModernTextBox1"
        ModernTextBox1.Padding = New Padding(10, 0, 10, 0)
        ModernTextBox1.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernTextBox1.Size = New Size(288, 35)
        ModernTextBox1.TabIndex = 39
        ModernTextBox1.WaterText = "ModernTextBox1"
        ' 
        ' Label6
        ' 
        Label6.AutoSize = True
        Label6.Dock = DockStyle.Top
        Label6.Font = New Font("Microsoft YaHei UI", 10F)
        Label6.Location = New Point(20, 120)
        Label6.Name = "Label6"
        Label6.Padding = New Padding(0, 20, 0, 10)
        Label6.Size = New Size(65, 50)
        Label6.TabIndex = 38
        Label6.Text = "单行模式"
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
        Panel1.Size = New Size(713, 50)
        Panel1.TabIndex = 35
        ' 
        ' ModernButton4
        ' 
        ModernButton4.BackColor1 = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        ModernButton4.BorderRadius = 10
        ModernButton4.BorderSize = 0
        ModernButton4.Dock = DockStyle.Left
        ModernButton4.Font = New Font("Microsoft YaHei UI", 10.8F, FontStyle.Bold, GraphicsUnit.Point, CByte(134))
        ModernButton4.Location = New Point(424, 0)
        ModernButton4.Margin = New Padding(2)
        ModernButton4.Name = "ModernButton4"
        ModernButton4.Size = New Size(80, 50)
        ModernButton4.SubText = "性能负载"
        ModernButton4.TabIndex = 6
        ModernButton4.Text = "中"
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
        ModernButton3.Location = New Point(296, 0)
        ModernButton3.Margin = New Padding(2)
        ModernButton3.Name = "ModernButton3"
        ModernButton3.Size = New Size(120, 50)
        ModernButton3.SubText = "动画支持"
        ModernButton3.TabIndex = 4
        ModernButton3.Text = "无"
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
        ModernButton2.Text = "天顶星科技"
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
        Label1.Size = New Size(331, 50)
        Label1.TabIndex = 34
        Label1.Text = "现代化文本框 ModernTextBox"
        ' 
        ' Form_ModernTextBox
        ' 
        AutoScaleDimensions = New SizeF(96F, 96F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(36), CByte(36), CByte(36))
        ClientSize = New Size(753, 604)
        Controls.Add(ModernPanel1)
        ForeColor = Color.Silver
        Name = "Form_ModernTextBox"
        Text = "Form_ModernTextBox"
        ModernPanel1.ResumeLayout(False)
        ModernPanel1.PerformLayout()
        Panel3.ResumeLayout(False)
        Panel3.PerformLayout()
        Panel2.ResumeLayout(False)
        Panel1.ResumeLayout(False)
        ResumeLayout(False)
    End Sub

    Friend WithEvents ModernPanel1 As ModernPanel
    Friend WithEvents Panel1 As Panel
    Friend WithEvents ModernButton4 As ModernButton
    Friend WithEvents Label4 As Label
    Friend WithEvents ModernButton3 As ModernButton
    Friend WithEvents Label3 As Label
    Friend WithEvents ModernButton2 As ModernButton
    Friend WithEvents Label2 As Label
    Friend WithEvents ModernButton1 As ModernButton
    Friend WithEvents Label1 As Label
    Friend WithEvents Label6 As Label
    Friend WithEvents ModernTextBox1 As ModernTextBox
    Friend WithEvents Panel3 As Panel
    Friend WithEvents ModernTextBox2 As ModernTextBox
    Friend WithEvents Label5 As Label
    Friend WithEvents Panel2 As Panel
    Friend WithEvents HtmlColorLabel2 As HtmlColorLabel
    Friend WithEvents HtmlColorLabel1 As HtmlColorLabel
End Class
