<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form_ModernTabControl
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
        Dim ModernTab1 As LakeUI.ModernTabControl.ModernTab = New ModernTabControl.ModernTab()
        Dim ModernTab2 As LakeUI.ModernTabControl.ModernTab = New ModernTabControl.ModernTab()
        Dim ModernTab3 As LakeUI.ModernTabControl.ModernTab = New ModernTabControl.ModernTab()
        Dim ModernTab4 As LakeUI.ModernTabControl.ModernTab = New ModernTabControl.ModernTab()
        Dim ModernTab5 As LakeUI.ModernTabControl.ModernTab = New ModernTabControl.ModernTab()
        ModernPanel1 = New ModernPanel()
        ModernTabControl1 = New ModernTabControl()
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
        Panel1.SuspendLayout()
        SuspendLayout()
        ' 
        ' ModernPanel1
        ' 
        ModernPanel1.BorderSize = 0
        ModernPanel1.Controls.Add(ModernTabControl1)
        ModernPanel1.Controls.Add(Label6)
        ModernPanel1.Controls.Add(Panel1)
        ModernPanel1.Controls.Add(Label1)
        ModernPanel1.Dock = DockStyle.Fill
        ModernPanel1.Location = New Point(0, 0)
        ModernPanel1.Margin = New Padding(2)
        ModernPanel1.Name = "ModernPanel1"
        ModernPanel1.Padding = New Padding(20)
        ModernPanel1.Size = New Size(708, 592)
        ModernPanel1.TabIndex = 39
        ' 
        ' ModernTabControl1
        ' 
        ModernTabControl1.ContentBackColor = Color.FromArgb(CByte(48), CByte(48), CByte(48))
        ModernTabControl1.Dock = DockStyle.Fill
        ModernTab3.IsSeparator = True
        ModernTabControl1.Items.Add(ModernTab1)
        ModernTabControl1.Items.Add(ModernTab2)
        ModernTabControl1.Items.Add(ModernTab3)
        ModernTabControl1.Items.Add(ModernTab4)
        ModernTabControl1.Items.Add(ModernTab5)
        ModernTabControl1.Location = New Point(20, 220)
        ModernTabControl1.Name = "ModernTabControl1"
        ModernTabControl1.SelectedIndex = 0
        ModernTabControl1.SelectedTab = ModernTab1
        ModernTabControl1.Size = New Size(668, 352)
        ModernTabControl1.TabAlignment = ModernTabControl.TabAlignmentEnum.Center
        ModernTabControl1.TabIndex = 39
        ModernTabControl1.TabItemHoverBackColor = Color.FromArgb(CByte(80), CByte(80), CByte(80))
        ModernTabControl1.TabItemSelectedBackColor = SystemColors.WindowFrame
        ModernTabControl1.TabStripBackColor = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        ModernTabControl1.TabStripHeight = 50
        ' 
        ' Label6
        ' 
        Label6.AutoSize = True
        Label6.Dock = DockStyle.Top
        Label6.Font = New Font("Microsoft YaHei UI", 10F)
        Label6.Location = New Point(20, 120)
        Label6.Name = "Label6"
        Label6.Padding = New Padding(0, 20, 0, 20)
        Label6.Size = New Size(443, 100)
        Label6.TabIndex = 38
        Label6.Text = "与竖向的版本差不多，但没有小字说明功能" & vbCrLf & "相比原版选项卡，这个支持各种对齐和自动调整功能，人机功效更强大" & vbCrLf & "当然与竖向选项卡一样，不是容器，无法在设计器中添加选项卡内容"
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
        Panel1.Size = New Size(668, 50)
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
        ModernButton3.Text = "半数动画"
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
        Label1.Size = New Size(408, 50)
        Label1.TabIndex = 34
        Label1.Text = "现代化横向选项卡 ModernTabControl"
        ' 
        ' Form_ModernTabControl
        ' 
        AutoScaleDimensions = New SizeF(96F, 96F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(36), CByte(36), CByte(36))
        ClientSize = New Size(708, 592)
        Controls.Add(ModernPanel1)
        Font = New Font("Microsoft YaHei UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ForeColor = Color.Silver
        Name = "Form_ModernTabControl"
        Text = "Form_ModernTabControl"
        ModernPanel1.ResumeLayout(False)
        ModernPanel1.PerformLayout()
        Panel1.ResumeLayout(False)
        ResumeLayout(False)
    End Sub

    Friend WithEvents ModernPanel1 As ModernPanel
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
    Friend WithEvents ModernTabControl1 As ModernTabControl
End Class
