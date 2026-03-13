<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form_BooleanSwitch
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
        Label7 = New Label()
        BooleanSwitch2 = New BooleanSwitch()
        Label5 = New Label()
        Panel2 = New Panel()
        BooleanSwitch1 = New BooleanSwitch()
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
        ModernPanel1.Size = New Size(693, 557)
        ModernPanel1.TabIndex = 37
        ' 
        ' Panel3
        ' 
        Panel3.Controls.Add(Label7)
        Panel3.Controls.Add(BooleanSwitch2)
        Panel3.Dock = DockStyle.Top
        Panel3.Location = New Point(20, 250)
        Panel3.Name = "Panel3"
        Panel3.Size = New Size(653, 30)
        Panel3.TabIndex = 42
        ' 
        ' Label7
        ' 
        Label7.Dock = DockStyle.Fill
        Label7.ForeColor = Color.Gray
        Label7.Location = New Point(60, 0)
        Label7.Name = "Label7"
        Label7.Padding = New Padding(10, 0, 0, 0)
        Label7.Size = New Size(593, 30)
        Label7.TabIndex = 1
        Label7.Text = "CPU ：这辈子画过的帧率最高的图"
        Label7.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' BooleanSwitch2
        ' 
        BooleanSwitch2.AnimationDuration = 1000
        BooleanSwitch2.AnimationFPS = 0
        BooleanSwitch2.Dock = DockStyle.Left
        BooleanSwitch2.KnobColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        BooleanSwitch2.Location = New Point(0, 0)
        BooleanSwitch2.Margin = New Padding(2, 2, 2, 2)
        BooleanSwitch2.Name = "BooleanSwitch2"
        BooleanSwitch2.Size = New Size(60, 30)
        BooleanSwitch2.TabIndex = 0
        BooleanSwitch2.TrackColorOff = Color.IndianRed
        BooleanSwitch2.TrackColorOn = Color.OliveDrab
        ' 
        ' Label5
        ' 
        Label5.AutoSize = True
        Label5.Dock = DockStyle.Top
        Label5.Font = New Font("Microsoft YaHei UI", 10F)
        Label5.Location = New Point(20, 200)
        Label5.Name = "Label5"
        Label5.Padding = New Padding(0, 20, 0, 10)
        Label5.Size = New Size(219, 50)
        Label5.TabIndex = 41
        Label5.Text = "无限帧率慢放，可以试试打断动画"
        ' 
        ' Panel2
        ' 
        Panel2.Controls.Add(BooleanSwitch1)
        Panel2.Dock = DockStyle.Top
        Panel2.Location = New Point(20, 170)
        Panel2.Name = "Panel2"
        Panel2.Size = New Size(653, 30)
        Panel2.TabIndex = 40
        ' 
        ' BooleanSwitch1
        ' 
        BooleanSwitch1.Dock = DockStyle.Left
        BooleanSwitch1.KnobColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        BooleanSwitch1.Location = New Point(0, 0)
        BooleanSwitch1.Margin = New Padding(2, 2, 2, 2)
        BooleanSwitch1.Name = "BooleanSwitch1"
        BooleanSwitch1.Size = New Size(50, 30)
        BooleanSwitch1.TabIndex = 0
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
        Label6.Text = "默认样式"
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
        Panel1.Size = New Size(653, 50)
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
        ModernButton3.Text = "全程动画"
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
        ModernButton2.Text = "人体工学科技"
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
        Label1.Size = New Size(271, 50)
        Label1.TabIndex = 34
        Label1.Text = "布尔开关 BooleanSwitch"
        ' 
        ' Form_BooleanSwitch
        ' 
        AutoScaleDimensions = New SizeF(96F, 96F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(36), CByte(36), CByte(36))
        ClientSize = New Size(693, 557)
        Controls.Add(ModernPanel1)
        ForeColor = Color.Silver
        Name = "Form_BooleanSwitch"
        Text = "Form_BooleanSwitch"
        ModernPanel1.ResumeLayout(False)
        ModernPanel1.PerformLayout()
        Panel3.ResumeLayout(False)
        Panel2.ResumeLayout(False)
        Panel1.ResumeLayout(False)
        ResumeLayout(False)
    End Sub

    Friend WithEvents ModernPanel1 As ModernPanel
    Friend WithEvents Panel2 As Panel
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
    Friend WithEvents Panel3 As Panel
    Friend WithEvents BooleanSwitch2 As BooleanSwitch
    Friend WithEvents Label5 As Label
    Friend WithEvents BooleanSwitch1 As BooleanSwitch
    Friend WithEvents Label7 As Label
End Class
