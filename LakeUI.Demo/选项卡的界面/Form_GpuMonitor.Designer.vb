<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form_GpuMonitor
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
        ModernTextBox1 = New ModernTextBox()
        Panel7 = New Panel()
        ModernButton6 = New ModernButton()
        Label11 = New Label()
        ModernButton5 = New ModernButton()
        Label7 = New Label()
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
        Panel7.SuspendLayout()
        Panel1.SuspendLayout()
        SuspendLayout()
        ' 
        ' ModernPanel1
        ' 
        ModernPanel1.BorderSize = 0
        ModernPanel1.Controls.Add(Panel3)
        ModernPanel1.Controls.Add(Panel7)
        ModernPanel1.Controls.Add(Label7)
        ModernPanel1.Controls.Add(Panel1)
        ModernPanel1.Controls.Add(Label1)
        ModernPanel1.Dock = DockStyle.Fill
        ModernPanel1.Location = New Point(0, 0)
        ModernPanel1.Margin = New Padding(2)
        ModernPanel1.Name = "ModernPanel1"
        ModernPanel1.Padding = New Padding(20)
        ModernPanel1.Size = New Size(724, 599)
        ModernPanel1.TabIndex = 36
        ' 
        ' Panel3
        ' 
        Panel3.Controls.Add(ModernTextBox1)
        Panel3.Dock = DockStyle.Fill
        Panel3.Location = New Point(20, 255)
        Panel3.Name = "Panel3"
        Panel3.Padding = New Padding(0, 20, 0, 0)
        Panel3.Size = New Size(672, 312)
        Panel3.TabIndex = 42
        ' 
        ' ModernTextBox1
        ' 
        ModernTextBox1.BorderRadius = 10
        ModernTextBox1.BorderSize = 2
        ModernTextBox1.Dock = DockStyle.Fill
        ModernTextBox1.EnableSyntaxHighlight = True
        ModernTextBox1.Font = New Font("Microsoft YaHei UI", 10F)
        ModernTextBox1.LinkDetection = True
        ModernTextBox1.Location = New Point(0, 20)
        ModernTextBox1.Margin = New Padding(2, 2, 2, 2)
        ModernTextBox1.MultiLine = True
        ModernTextBox1.Name = "ModernTextBox1"
        ModernTextBox1.Padding = New Padding(10)
        ModernTextBox1.ShowLineNumbers = True
        ModernTextBox1.Size = New Size(672, 292)
        ModernTextBox1.TabIndex = 39
        ' 
        ' Panel7
        ' 
        Panel7.Controls.Add(ModernButton6)
        Panel7.Controls.Add(Label11)
        Panel7.Controls.Add(ModernButton5)
        Panel7.Dock = DockStyle.Top
        Panel7.Location = New Point(20, 210)
        Panel7.Name = "Panel7"
        Panel7.Padding = New Padding(0, 10, 0, 0)
        Panel7.Size = New Size(672, 45)
        Panel7.TabIndex = 52
        ' 
        ' ModernButton6
        ' 
        ModernButton6.BackColor2 = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        ModernButton6.BorderRadius = 10
        ModernButton6.BorderSize = 2
        ModernButton6.Dock = DockStyle.Left
        ModernButton6.HoverBorderColor = Color.CornflowerBlue
        ModernButton6.Location = New Point(130, 10)
        ModernButton6.Margin = New Padding(2)
        ModernButton6.Name = "ModernButton6"
        ModernButton6.PressedBorderColor = Color.MediumSlateBlue
        ModernButton6.Size = New Size(120, 35)
        ModernButton6.TabIndex = 9
        ModernButton6.Text = "停止刷新"
        ' 
        ' Label11
        ' 
        Label11.Dock = DockStyle.Left
        Label11.Location = New Point(120, 10)
        Label11.Name = "Label11"
        Label11.Size = New Size(10, 35)
        Label11.TabIndex = 10
        ' 
        ' ModernButton5
        ' 
        ModernButton5.BackColor2 = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        ModernButton5.BorderRadius = 10
        ModernButton5.BorderSize = 2
        ModernButton5.Dock = DockStyle.Left
        ModernButton5.HoverBorderColor = Color.CornflowerBlue
        ModernButton5.Location = New Point(0, 10)
        ModernButton5.Margin = New Padding(2)
        ModernButton5.Name = "ModernButton5"
        ModernButton5.PressedBorderColor = Color.MediumSlateBlue
        ModernButton5.Size = New Size(120, 35)
        ModernButton5.TabIndex = 11
        ModernButton5.Text = "开始刷新"
        ' 
        ' Label7
        ' 
        Label7.AutoSize = True
        Label7.Dock = DockStyle.Top
        Label7.Font = New Font("Microsoft YaHei UI", 10F)
        Label7.Location = New Point(20, 120)
        Label7.Name = "Label7"
        Label7.Padding = New Padding(0, 20, 0, 10)
        Label7.Size = New Size(524, 90)
        Label7.TabIndex = 43
        Label7.Text = "无需驱动，无需管理员权限，只需装好驱动即可" & vbCrLf & "这个 GPU 监控器不是控件，而是一个类，通过调用静态方法即可轻松实现数据获取" & vbCrLf & "只有N卡能获取到更多信息，这就是为什么老黄敢加价，只有老黄的生态是完善了的"
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
        Panel1.Size = New Size(672, 50)
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
        ModernButton2.Text = "微软科技"
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
        Label1.Size = New Size(266, 50)
        Label1.TabIndex = 34
        Label1.Text = "显卡监控器 GpuMonitor"
        ' 
        ' Form_GpuMonitor
        ' 
        AutoScaleDimensions = New SizeF(96F, 96F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(36), CByte(36), CByte(36))
        ClientSize = New Size(724, 599)
        Controls.Add(ModernPanel1)
        Font = New Font("Microsoft YaHei UI", 10F)
        ForeColor = Color.Silver
        Name = "Form_GpuMonitor"
        Text = "Form_GpuMonitor"
        ModernPanel1.ResumeLayout(False)
        ModernPanel1.PerformLayout()
        Panel3.ResumeLayout(False)
        Panel7.ResumeLayout(False)
        Panel1.ResumeLayout(False)
        ResumeLayout(False)
    End Sub

    Friend WithEvents ModernPanel1 As ModernPanel
    Friend WithEvents Panel3 As Panel
    Friend WithEvents ModernTextBox1 As ModernTextBox
    Friend WithEvents Label7 As Label
    Friend WithEvents Panel1 As Panel
    Friend WithEvents ModernButton4 As ModernButton
    Friend WithEvents Label4 As Label
    Friend WithEvents ModernButton3 As ModernButton
    Friend WithEvents Label3 As Label
    Friend WithEvents ModernButton2 As ModernButton
    Friend WithEvents Label2 As Label
    Friend WithEvents ModernButton1 As ModernButton
    Friend WithEvents Label1 As Label
    Friend WithEvents Panel7 As Panel
    Friend WithEvents ModernButton6 As ModernButton
    Friend WithEvents Label11 As Label
    Friend WithEvents ModernButton5 As ModernButton
End Class
