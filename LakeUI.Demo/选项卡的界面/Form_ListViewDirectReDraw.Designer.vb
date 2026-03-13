<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form_ListViewDirectReDraw
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
        Dim ListViewItem1 As ListViewItem = New ListViewItem(New ListViewItem.ListViewSubItem() {New System.Windows.Forms.ListViewItem.ListViewSubItem(Nothing, "列表视图项"), New System.Windows.Forms.ListViewItem.ListViewSubItem(Nothing, "列表视图子项", Color.Silver, Color.FromArgb(CByte(48), CByte(48), CByte(48)), New Font("宋体", 10.8F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))), New System.Windows.Forms.ListViewItem.ListViewSubItem(Nothing, "列表视图子项", Color.CornflowerBlue, Color.FromArgb(CByte(48), CByte(48), CByte(48)), New Font("Microsoft YaHei UI", 10F))}, -1)
        Dim ListViewItem2 As ListViewItem = New ListViewItem(New ListViewItem.ListViewSubItem() {New System.Windows.Forms.ListViewItem.ListViewSubItem(Nothing, "列表视图项"), New System.Windows.Forms.ListViewItem.ListViewSubItem(Nothing, "列表视图子项", Color.Silver, Color.FromArgb(CByte(48), CByte(48), CByte(48)), New Font("华文新魏", 10.7999992F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))), New System.Windows.Forms.ListViewItem.ListViewSubItem(Nothing, "列表视图子项", Color.IndianRed, Color.FromArgb(CByte(48), CByte(48), CByte(48)), New Font("Microsoft YaHei UI", 10F))}, -1)
        Dim ListViewItem3 As ListViewItem = New ListViewItem(New ListViewItem.ListViewSubItem() {New System.Windows.Forms.ListViewItem.ListViewSubItem(Nothing, "列表视图项"), New System.Windows.Forms.ListViewItem.ListViewSubItem(Nothing, "列表视图子项", Color.Silver, Color.FromArgb(CByte(48), CByte(48), CByte(48)), New Font("华文楷体", 10.8F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))), New System.Windows.Forms.ListViewItem.ListViewSubItem(Nothing, "列表视图子项", Color.YellowGreen, Color.FromArgb(CByte(48), CByte(48), CByte(48)), New Font("Microsoft YaHei UI", 10F))}, -1)
        Dim ListViewItem4 As ListViewItem = New ListViewItem("列表视图项")
        Dim ListViewItem5 As ListViewItem = New ListViewItem("列表视图项")
        Dim ListViewItem6 As ListViewItem = New ListViewItem("列表视图项")
        Dim ListViewItem7 As ListViewItem = New ListViewItem("列表视图项")
        Dim ListViewItem8 As ListViewItem = New ListViewItem("列表视图项")
        Dim ListViewItem9 As ListViewItem = New ListViewItem("列表视图项")
        Dim ListViewItem10 As ListViewItem = New ListViewItem("列表视图项")
        ModernPanel1 = New ModernPanel()
        ListView1 = New ListView()
        ColumnHeader1 = New ColumnHeader()
        ColumnHeader2 = New ColumnHeader()
        ColumnHeader3 = New ColumnHeader()
        Label3 = New Label()
        ModernTextBox2 = New ModernTextBox()
        Label2 = New Label()
        ModernTextBox1 = New ModernTextBox()
        Label6 = New Label()
        Panel1 = New Panel()
        ModernButton4 = New ModernButton()
        Label4 = New Label()
        ModernButton3 = New ModernButton()
        Label8 = New Label()
        ModernButton2 = New ModernButton()
        Label10 = New Label()
        ModernButton1 = New ModernButton()
        Label11 = New Label()
        ModernPanel1.SuspendLayout()
        Panel1.SuspendLayout()
        SuspendLayout()
        ' 
        ' ModernPanel1
        ' 
        ModernPanel1.BorderSize = 0
        ModernPanel1.Controls.Add(ListView1)
        ModernPanel1.Controls.Add(Label3)
        ModernPanel1.Controls.Add(ModernTextBox2)
        ModernPanel1.Controls.Add(Label2)
        ModernPanel1.Controls.Add(ModernTextBox1)
        ModernPanel1.Controls.Add(Label6)
        ModernPanel1.Controls.Add(Panel1)
        ModernPanel1.Controls.Add(Label11)
        ModernPanel1.Dock = DockStyle.Fill
        ModernPanel1.Location = New Point(0, 0)
        ModernPanel1.Margin = New Padding(2)
        ModernPanel1.Name = "ModernPanel1"
        ModernPanel1.Padding = New Padding(20)
        ModernPanel1.Size = New Size(784, 618)
        ModernPanel1.TabIndex = 40
        ' 
        ' ListView1
        ' 
        ListView1.BackColor = Color.FromArgb(CByte(48), CByte(48), CByte(48))
        ListView1.BorderStyle = BorderStyle.FixedSingle
        ListView1.Columns.AddRange(New ColumnHeader() {ColumnHeader1, ColumnHeader2, ColumnHeader3})
        ListView1.Dock = DockStyle.Fill
        ListView1.Font = New Font("Microsoft YaHei UI", 10F)
        ListView1.ForeColor = Color.Silver
        ListView1.FullRowSelect = True
        ListView1.Items.AddRange(New ListViewItem() {ListViewItem1, ListViewItem2, ListViewItem3, ListViewItem4, ListViewItem5, ListViewItem6, ListViewItem7, ListViewItem8, ListViewItem9, ListViewItem10})
        ListView1.Location = New Point(20, 460)
        ListView1.Margin = New Padding(2)
        ListView1.Name = "ListView1"
        ListView1.Size = New Size(744, 138)
        ListView1.TabIndex = 43
        ListView1.UseCompatibleStateImageBehavior = False
        ListView1.View = View.Details
        ' 
        ' ColumnHeader1
        ' 
        ColumnHeader1.Width = 200
        ' 
        ' ColumnHeader2
        ' 
        ColumnHeader2.Width = 200
        ' 
        ' ColumnHeader3
        ' 
        ColumnHeader3.Width = 200
        ' 
        ' Label3
        ' 
        Label3.AutoSize = True
        Label3.Dock = DockStyle.Top
        Label3.Font = New Font("Microsoft YaHei UI", 10F)
        Label3.Location = New Point(20, 420)
        Label3.Margin = New Padding(2, 0, 2, 0)
        Label3.Name = "Label3"
        Label3.Padding = New Padding(0, 10, 0, 10)
        Label3.Size = New Size(107, 40)
        Label3.TabIndex = 42
        Label3.Text = "可于此体验效果"
        ' 
        ' ModernTextBox2
        ' 
        ModernTextBox2.BackColor1 = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        ModernTextBox2.BorderRadius = 10
        ModernTextBox2.BorderSize = 0
        ModernTextBox2.CaretColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        ModernTextBox2.Dock = DockStyle.Top
        ModernTextBox2.Font = New Font("Microsoft YaHei UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ModernTextBox2.ForeColor = Color.Salmon
        ModernTextBox2.Location = New Point(20, 300)
        ModernTextBox2.Margin = New Padding(2, 2, 2, 2)
        ModernTextBox2.MultiLine = True
        ModernTextBox2.Name = "ModernTextBox2"
        ModernTextBox2.Padding = New Padding(10, 8, 8, 8)
        ModernTextBox2.ReadOnly = True
        ModernTextBox2.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernTextBox2.Size = New Size(744, 120)
        ModernTextBox2.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        ModernTextBox2.TabIndex = 41
        ModernTextBox2.Text = "ModernTextBox2"
        ' 
        ' Label2
        ' 
        Label2.AutoSize = True
        Label2.Dock = DockStyle.Top
        Label2.Font = New Font("Microsoft YaHei UI", 10F)
        Label2.Location = New Point(20, 260)
        Label2.Margin = New Padding(2, 0, 2, 0)
        Label2.Name = "Label2"
        Label2.Padding = New Padding(0, 10, 0, 10)
        Label2.Size = New Size(182, 40)
        Label2.TabIndex = 40
        Label2.Text = "❗此控件的重绘有较多限制"
        ' 
        ' ModernTextBox1
        ' 
        ModernTextBox1.BackColor1 = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        ModernTextBox1.BorderRadius = 10
        ModernTextBox1.BorderSize = 0
        ModernTextBox1.CaretColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        ModernTextBox1.Dock = DockStyle.Top
        ModernTextBox1.Font = New Font("Microsoft YaHei UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ModernTextBox1.Location = New Point(20, 140)
        ModernTextBox1.Margin = New Padding(2, 2, 2, 2)
        ModernTextBox1.MultiLine = True
        ModernTextBox1.Name = "ModernTextBox1"
        ModernTextBox1.Padding = New Padding(10, 8, 8, 8)
        ModernTextBox1.ReadOnly = True
        ModernTextBox1.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernTextBox1.Size = New Size(744, 120)
        ModernTextBox1.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        ModernTextBox1.TabIndex = 39
        ModernTextBox1.Text = "ModernTextBox1"
        ' 
        ' Label6
        ' 
        Label6.Dock = DockStyle.Top
        Label6.Font = New Font("Microsoft YaHei UI", 10F)
        Label6.Location = New Point(20, 120)
        Label6.Name = "Label6"
        Label6.Size = New Size(744, 20)
        Label6.TabIndex = 38
        ' 
        ' Panel1
        ' 
        Panel1.Controls.Add(ModernButton4)
        Panel1.Controls.Add(Label4)
        Panel1.Controls.Add(ModernButton3)
        Panel1.Controls.Add(Label8)
        Panel1.Controls.Add(ModernButton2)
        Panel1.Controls.Add(Label10)
        Panel1.Controls.Add(ModernButton1)
        Panel1.Dock = DockStyle.Top
        Panel1.Location = New Point(20, 70)
        Panel1.Name = "Panel1"
        Panel1.Size = New Size(744, 50)
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
        ModernButton4.Text = "增益"
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
        ' Label8
        ' 
        Label8.Dock = DockStyle.Left
        Label8.Location = New Point(288, 0)
        Label8.Margin = New Padding(2, 0, 2, 0)
        Label8.Name = "Label8"
        Label8.Size = New Size(8, 50)
        Label8.TabIndex = 3
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
        ModernButton2.Text = "原版科技"
        ModernButton2.TextAlign = ModernButton.TextAlignEnum.Left
        ' 
        ' Label10
        ' 
        Label10.Dock = DockStyle.Left
        Label10.Location = New Point(120, 0)
        Label10.Margin = New Padding(2, 0, 2, 0)
        Label10.Name = "Label10"
        Label10.Size = New Size(8, 50)
        Label10.TabIndex = 1
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
        ModernButton1.Text = "原版重绘"
        ModernButton1.TextAlign = ModernButton.TextAlignEnum.Left
        ' 
        ' Label11
        ' 
        Label11.AutoSize = True
        Label11.Dock = DockStyle.Top
        Label11.Font = New Font("Microsoft YaHei UI", 16.2F, FontStyle.Bold, GraphicsUnit.Point, CByte(134))
        Label11.Location = New Point(20, 20)
        Label11.Name = "Label11"
        Label11.Padding = New Padding(0, 0, 0, 20)
        Label11.Size = New Size(442, 50)
        Label11.TabIndex = 34
        Label11.Text = "列表视图原地重绘 ListViewDirectReDraw"
        ' 
        ' Form_ListViewDirectReDraw
        ' 
        AutoScaleDimensions = New SizeF(96F, 96F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(36), CByte(36), CByte(36))
        ClientSize = New Size(784, 618)
        Controls.Add(ModernPanel1)
        ForeColor = Color.Silver
        Margin = New Padding(2)
        Name = "Form_ListViewDirectReDraw"
        Text = "Form_ListViewDirectReDraw"
        ModernPanel1.ResumeLayout(False)
        ModernPanel1.PerformLayout()
        Panel1.ResumeLayout(False)
        ResumeLayout(False)
    End Sub

    Friend WithEvents ModernPanel1 As ModernPanel
    Friend WithEvents ListView1 As ListView
    Friend WithEvents ColumnHeader1 As ColumnHeader
    Friend WithEvents ColumnHeader2 As ColumnHeader
    Friend WithEvents ColumnHeader3 As ColumnHeader
    Friend WithEvents Label3 As Label
    Friend WithEvents ModernTextBox2 As ModernTextBox
    Friend WithEvents Label2 As Label
    Friend WithEvents ModernTextBox1 As ModernTextBox
    Friend WithEvents Label6 As Label
    Friend WithEvents Panel1 As Panel
    Friend WithEvents ModernButton4 As ModernButton
    Friend WithEvents Label4 As Label
    Friend WithEvents ModernButton3 As ModernButton
    Friend WithEvents Label8 As Label
    Friend WithEvents ModernButton2 As ModernButton
    Friend WithEvents Label10 As Label
    Friend WithEvents ModernButton1 As ModernButton
    Friend WithEvents Label11 As Label
End Class
