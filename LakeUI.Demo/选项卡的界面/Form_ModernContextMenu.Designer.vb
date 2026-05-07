<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form_ModernContextMenu
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
        Dim ModernMenuItem1 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(Form_ModernContextMenu))
        Dim ModernMenuItem2 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        Dim ModernMenuItem3 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        Dim ModernMenuItem4 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        Dim ModernMenuItem5 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        ModernPanel1 = New ModernPanel()
        Label5 = New Label()
        Panel1 = New Panel()
        ModernButton4 = New ModernButton()
        Label4 = New Label()
        ModernButton3 = New ModernButton()
        Label3 = New Label()
        ModernButton2 = New ModernButton()
        Label2 = New Label()
        ModernButton1 = New ModernButton()
        Label1 = New Label()
        ModernContextMenu1 = New ModernContextMenu()
        ModernPanel1.SuspendLayout()
        Panel1.SuspendLayout()
        SuspendLayout()
        ' 
        ' ModernPanel1
        ' 
        ModernPanel1.BorderSize = 0
        ModernPanel1.Controls.Add(Label5)
        ModernPanel1.Controls.Add(Panel1)
        ModernPanel1.Controls.Add(Label1)
        ModernPanel1.Dock = DockStyle.Fill
        ModernContextMenu1.SetEnableMenu(ModernPanel1, True)
        ModernPanel1.Location = New Point(0, 0)
        ModernPanel1.Margin = New Padding(2)
        ModernPanel1.Name = "ModernPanel1"
        ModernPanel1.Padding = New Padding(20)
        ModernPanel1.ScrollBarMode = ModernPanel.ScrollMode.None
        ModernPanel1.Size = New Size(638, 509)
        ModernPanel1.TabIndex = 40
        ' 
        ' Label5
        ' 
        Label5.AutoSize = True
        Label5.Dock = DockStyle.Top
        Label5.Font = New Font("Microsoft YaHei UI", 10F)
        Label5.Location = New Point(20, 120)
        Label5.Margin = New Padding(2, 0, 2, 0)
        Label5.Name = "Label5"
        Label5.Padding = New Padding(0, 20, 0, 10)
        Label5.Size = New Size(299, 50)
        Label5.TabIndex = 43
        Label5.Text = "可在此页右键体验（无限帧率动画+专门调制）"
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
        Panel1.Size = New Size(598, 50)
        Panel1.TabIndex = 35
        ' 
        ' ModernButton4
        ' 
        ModernButton4.BackColor1 = Color.FromArgb(CByte(40), CByte(220), CByte(220), CByte(220))
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
        ModernButton3.BackColor1 = Color.FromArgb(CByte(40), CByte(220), CByte(220), CByte(220))
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
        ModernButton2.BackColor1 = Color.FromArgb(CByte(40), CByte(220), CByte(220), CByte(220))
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
        ModernButton1.BackColor1 = Color.FromArgb(CByte(40), CByte(220), CByte(220), CByte(220))
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
        Label1.Size = New Size(435, 50)
        Label1.TabIndex = 34
        Label1.Text = "现代化上下文菜单 ModernContextMenu"
        ' 
        ' ModernContextMenu1
        ' 
        ModernContextMenu1.BackdropBlurPasses = 1
        ModernContextMenu1.BackdropBlurRadius = 10
        ModernContextMenu1.BackdropMaxParallelism = 2
        ModernContextMenu1.BackdropMode = ModernContextMenu.BackdropModeEnum.Auto
        ModernContextMenu1.BackdropTintColor = Color.FromArgb(CByte(80), CByte(0), CByte(0), CByte(0))
        ModernContextMenu1.BorderColor = Color.FromArgb(CByte(80), CByte(220), CByte(220), CByte(220))
        ModernContextMenu1.BorderSize = 2
        ModernContextMenu1.HoverAnimationFPS = 0
        ModernContextMenu1.HoverBackColor = Color.FromArgb(CByte(60), CByte(220), CByte(220), CByte(220))
        ModernContextMenu1.HoverRadius = 5
        ModernContextMenu1.ItemHeight = 35
        ModernMenuItem1.Font = Nothing
        ModernMenuItem1.Icon = CType(resources.GetObject("ModernMenuItem1.Icon"), Image)
        ModernMenuItem1.Text = "完全自绘的上下文菜单"
        ModernMenuItem2.Font = Nothing
        ModernMenuItem2.Icon = CType(resources.GetObject("ModernMenuItem2.Icon"), Image)
        ModernMenuItem2.Text = "更轻，更快，更舒适，更自由"
        ModernMenuItem3.Font = Nothing
        ModernMenuItem3.IsSeparator = True
        ModernMenuItem4.Font = Nothing
        ModernMenuItem4.IsDescription = True
        ModernMenuItem4.Text = "支持设置小字说明"
        ModernMenuItem5.Font = New Font("宋体", 10.2F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ModernMenuItem5.ForeColor = Color.FromArgb(CByte(255), CByte(192), CByte(192))
        ModernMenuItem5.Text = "每个菜单项都支持独立设置字体和颜色"
        ModernContextMenu1.Items.Add(ModernMenuItem1)
        ModernContextMenu1.Items.Add(ModernMenuItem2)
        ModernContextMenu1.Items.Add(ModernMenuItem3)
        ModernContextMenu1.Items.Add(ModernMenuItem4)
        ModernContextMenu1.Items.Add(ModernMenuItem5)
        ModernContextMenu1.MenuFont = New Font("Microsoft YaHei UI", 10F)
        ModernContextMenu1.MenuPadding = New Padding(10)
        ModernContextMenu1.PopupAnimationFPS = 0
        ModernContextMenu1.PressedBackColor = Color.FromArgb(CByte(80), CByte(220), CByte(220), CByte(220))
        ModernContextMenu1.SeparatorColor = Color.FromArgb(CByte(80), CByte(220), CByte(220), CByte(220))
        ModernContextMenu1.SeparatorHeight = 20
        ModernContextMenu1.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        ' 
        ' Form_ModernContextMenu
        ' 
        AutoScaleDimensions = New SizeF(96F, 96F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(36), CByte(36), CByte(36))
        ClientSize = New Size(638, 509)
        Controls.Add(ModernPanel1)
        Font = New Font("Microsoft YaHei UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ForeColor = Color.Silver
        Name = "Form_ModernContextMenu"
        Text = "Form_ModernContextMenu"
        ModernPanel1.ResumeLayout(False)
        ModernPanel1.PerformLayout()
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
    Friend WithEvents Label5 As Label
    Friend WithEvents ModernContextMenu1 As ModernContextMenu
End Class
