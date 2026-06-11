<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form_BreadcrumbNavigationBar
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
        Dim BreadcrumbItem1 As LakeUI.BreadcrumbNavigationBar.BreadcrumbItem = New BreadcrumbNavigationBar.BreadcrumbItem()
        Dim BreadcrumbItem2 As LakeUI.BreadcrumbNavigationBar.BreadcrumbItem = New BreadcrumbNavigationBar.BreadcrumbItem()
        Dim BreadcrumbItem3 As LakeUI.BreadcrumbNavigationBar.BreadcrumbItem = New BreadcrumbNavigationBar.BreadcrumbItem()
        Dim BreadcrumbItem4 As LakeUI.BreadcrumbNavigationBar.BreadcrumbItem = New BreadcrumbNavigationBar.BreadcrumbItem()
        ModernPanel1 = New ModernPanel()
        HtmlColorLabel2 = New HtmlColorLabel()
        BreadcrumbNavigationBar1 = New BreadcrumbNavigationBar()
        HtmlColorLabel1 = New HtmlColorLabel()
        Panel1 = New Panel()
        ModernButton4 = New ModernButton()
        Label4 = New JustEmptyControl()
        ModernButton3 = New ModernButton()
        Label3 = New JustEmptyControl()
        ModernButton2 = New ModernButton()
        Label2 = New JustEmptyControl()
        ModernButton1 = New ModernButton()
        Label1 = New Label()
        ModernPanel1.SuspendLayout()
        Panel1.SuspendLayout()
        SuspendLayout()
        ' 
        ' ModernPanel1
        ' 
        ModernPanel1.BorderSize = 0
        ModernPanel1.Controls.Add(HtmlColorLabel2)
        ModernPanel1.Controls.Add(BreadcrumbNavigationBar1)
        ModernPanel1.Controls.Add(HtmlColorLabel1)
        ModernPanel1.Controls.Add(Panel1)
        ModernPanel1.Controls.Add(Label1)
        ModernPanel1.Dock = DockStyle.Fill
        ModernPanel1.Location = New Point(0, 0)
        ModernPanel1.Margin = New Padding(2)
        ModernPanel1.Name = "ModernPanel1"
        ModernPanel1.Padding = New Padding(20)
        ModernPanel1.ScrollBarMode = ModernPanel.ScrollMode.None
        ModernPanel1.Size = New Size(812, 669)
        ModernPanel1.TabIndex = 38
        ' 
        ' HtmlColorLabel2
        ' 
        HtmlColorLabel2.AutoSize = True
        HtmlColorLabel2.AutoSizeMode = AutoSizeMode.GrowAndShrink
        HtmlColorLabel2.Dock = DockStyle.Top
        HtmlColorLabel2.Location = New Point(20, 241)
        HtmlColorLabel2.Margin = New Padding(2)
        HtmlColorLabel2.Name = "HtmlColorLabel2"
        HtmlColorLabel2.Padding = New Padding(0, 20, 0, 20)
        HtmlColorLabel2.Size = New Size(772, 61)
        HtmlColorLabel2.TabIndex = 38
        HtmlColorLabel2.Text = "这个玩意我不怎么用，如果有难受的点要及时提"
        ' 
        ' BreadcrumbNavigationBar1
        ' 
        BreadcrumbNavigationBar1.BackColor = Color.FromArgb(CByte(40), CByte(0), CByte(0), CByte(0))
        BreadcrumbNavigationBar1.Dock = DockStyle.Top
        BreadcrumbItem1.HasDropDown = True
        BreadcrumbItem1.Text = "面包"
        BreadcrumbItem2.HasDropDown = True
        BreadcrumbItem2.Text = "面包"
        BreadcrumbItem3.HasDropDown = True
        BreadcrumbItem3.Text = "面包"
        BreadcrumbItem4.Text = "还没找到路是吧"
        BreadcrumbNavigationBar1.Items.Add(BreadcrumbItem1)
        BreadcrumbNavigationBar1.Items.Add(BreadcrumbItem2)
        BreadcrumbNavigationBar1.Items.Add(BreadcrumbItem3)
        BreadcrumbNavigationBar1.Items.Add(BreadcrumbItem4)
        BreadcrumbNavigationBar1.Location = New Point(20, 201)
        BreadcrumbNavigationBar1.Name = "BreadcrumbNavigationBar1"
        BreadcrumbNavigationBar1.NodeHoverBackColor = Color.FromArgb(CByte(40), CByte(220), CByte(220), CByte(220))
        BreadcrumbNavigationBar1.NodePressedBackColor = Color.FromArgb(CByte(60), CByte(220), CByte(220), CByte(220))
        BreadcrumbNavigationBar1.Padding = New Padding(10, 0, 0, 0)
        BreadcrumbNavigationBar1.Size = New Size(772, 40)
        BreadcrumbNavigationBar1.TabIndex = 37
        BreadcrumbNavigationBar1.TabStop = False
        ' 
        ' HtmlColorLabel1
        ' 
        HtmlColorLabel1.AutoSize = True
        HtmlColorLabel1.AutoSizeMode = AutoSizeMode.GrowAndShrink
        HtmlColorLabel1.Dock = DockStyle.Top
        HtmlColorLabel1.Location = New Point(20, 120)
        HtmlColorLabel1.Margin = New Padding(2)
        HtmlColorLabel1.Name = "HtmlColorLabel1"
        HtmlColorLabel1.Padding = New Padding(0, 20, 0, 20)
        HtmlColorLabel1.Size = New Size(772, 81)
        HtmlColorLabel1.TabIndex = 36
        HtmlColorLabel1.Text = "面包屑导航 Breadcrumb Navigation 这个概念来自童话故事" & ChrW(8220) & "汉赛尔和格莱特" & ChrW(8221) & "，当汉赛尔和格莱特穿过森林时，不小心迷路了，但是他们发现沿途走过的地方都撒下了面包屑，让这些面包屑来帮助他们找到回家的路。"
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
        Panel1.Size = New Size(772, 50)
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
        ModernButton3.Location = New Point(296, 0)
        ModernButton3.Margin = New Padding(2)
        ModernButton3.Name = "ModernButton3"
        ModernButton3.Size = New Size(120, 50)
        ModernButton3.SubText = "交互表现"
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
        ModernButton2.BackColor1 = Color.FromArgb(CByte(40), CByte(220), CByte(220), CByte(220))
        ModernButton2.BorderRadius = 10
        ModernButton2.BorderSize = 0
        ModernButton2.Dock = DockStyle.Left
        ModernButton2.Font = New Font("Microsoft YaHei UI", 10.8F, FontStyle.Bold, GraphicsUnit.Point, CByte(134))
        ModernButton2.Location = New Point(128, 0)
        ModernButton2.Margin = New Padding(2)
        ModernButton2.Name = "ModernButton2"
        ModernButton2.Size = New Size(160, 50)
        ModernButton2.SubText = "技术路线"
        ModernButton2.TabIndex = 2
        ModernButton2.Text = "面包科技"
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
        ModernButton1.SubText = "实现方式"
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
        Label1.Size = New Size(449, 50)
        Label1.TabIndex = 34
        Label1.Text = "面包屑导航条 BreadcrumbNavigationBar"
        ' 
        ' Form_BreadcrumbNavigationBar
        ' 
        AutoScaleDimensions = New SizeF(96F, 96F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(36), CByte(36), CByte(36))
        ClientSize = New Size(812, 669)
        Controls.Add(ModernPanel1)
        Font = New Font("Microsoft YaHei UI", 10F)
        ForeColor = Color.Silver
        Name = "Form_BreadcrumbNavigationBar"
        Text = "Form_BreadcrumbNavigationBar"
        ModernPanel1.ResumeLayout(False)
        ModernPanel1.PerformLayout()
        Panel1.ResumeLayout(False)
        ResumeLayout(False)
    End Sub

    Friend WithEvents ModernPanel1 As ModernPanel
    Friend WithEvents Panel1 As Panel
    Friend WithEvents ModernButton4 As ModernButton
    Friend WithEvents Label4 As JustEmptyControl
    Friend WithEvents ModernButton3 As ModernButton
    Friend WithEvents Label3 As JustEmptyControl
    Friend WithEvents ModernButton2 As ModernButton
    Friend WithEvents Label2 As JustEmptyControl
    Friend WithEvents ModernButton1 As ModernButton
    Friend WithEvents Label1 As Label
    Friend WithEvents HtmlColorLabel1 As HtmlColorLabel
    Friend WithEvents BreadcrumbNavigationBar1 As BreadcrumbNavigationBar
    Friend WithEvents HtmlColorLabel2 As HtmlColorLabel
End Class
