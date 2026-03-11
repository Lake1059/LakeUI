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
        Label1 = New Label()
        ModernTextBox1 = New ModernTextBox()
        Label2 = New Label()
        ModernTextBox2 = New ModernTextBox()
        Label3 = New Label()
        ListView1 = New ListView()
        ColumnHeader1 = New ColumnHeader()
        ColumnHeader2 = New ColumnHeader()
        ColumnHeader3 = New ColumnHeader()
        SuspendLayout()
        ' 
        ' Label1
        ' 
        Label1.AutoSize = True
        Label1.Dock = DockStyle.Top
        Label1.Font = New Font("Microsoft YaHei UI", 16.2F, FontStyle.Bold, GraphicsUnit.Point, CByte(134))
        Label1.Location = New Point(20, 20)
        Label1.Name = "Label1"
        Label1.Padding = New Padding(0, 0, 0, 20)
        Label1.Size = New Size(532, 57)
        Label1.TabIndex = 1
        Label1.Text = "重绘的列表视图 ListViewDirectReDraw"
        ' 
        ' ModernTextBox1
        ' 
        ModernTextBox1.BackColor1 = Color.FromArgb(CByte(48), CByte(48), CByte(48))
        ModernTextBox1.BorderRadius = 10
        ModernTextBox1.BorderSize = 2
        ModernTextBox1.CaretColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        ModernTextBox1.Dock = DockStyle.Top
        ModernTextBox1.Font = New Font("Microsoft YaHei UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ModernTextBox1.Location = New Point(20, 77)
        ModernTextBox1.MultiLine = True
        ModernTextBox1.Name = "ModernTextBox1"
        ModernTextBox1.Padding = New Padding(13, 10, 10, 10)
        ModernTextBox1.ReadOnly = True
        ModernTextBox1.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernTextBox1.Size = New Size(940, 150)
        ModernTextBox1.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        ModernTextBox1.TabIndex = 2
        ModernTextBox1.Text = "ModernTextBox1"
        ' 
        ' Label2
        ' 
        Label2.AutoSize = True
        Label2.Dock = DockStyle.Top
        Label2.Font = New Font("Microsoft YaHei UI", 10F)
        Label2.Location = New Point(20, 227)
        Label2.Name = "Label2"
        Label2.Padding = New Padding(0, 20, 0, 20)
        Label2.Size = New Size(220, 63)
        Label2.TabIndex = 3
        Label2.Text = "❗此控件的重绘有较多限制"
        ' 
        ' ModernTextBox2
        ' 
        ModernTextBox2.BackColor1 = Color.FromArgb(CByte(48), CByte(48), CByte(48))
        ModernTextBox2.BorderRadius = 10
        ModernTextBox2.BorderSize = 2
        ModernTextBox2.CaretColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        ModernTextBox2.Dock = DockStyle.Top
        ModernTextBox2.Font = New Font("Microsoft YaHei UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ModernTextBox2.ForeColor = Color.Salmon
        ModernTextBox2.Location = New Point(20, 290)
        ModernTextBox2.MultiLine = True
        ModernTextBox2.Name = "ModernTextBox2"
        ModernTextBox2.Padding = New Padding(13, 10, 10, 10)
        ModernTextBox2.ReadOnly = True
        ModernTextBox2.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernTextBox2.Size = New Size(940, 150)
        ModernTextBox2.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        ModernTextBox2.TabIndex = 4
        ModernTextBox2.Text = "ModernTextBox2"
        ' 
        ' Label3
        ' 
        Label3.AutoSize = True
        Label3.Dock = DockStyle.Top
        Label3.Font = New Font("Microsoft YaHei UI", 10F)
        Label3.Location = New Point(20, 440)
        Label3.Name = "Label3"
        Label3.Padding = New Padding(0, 20, 0, 20)
        Label3.Size = New Size(129, 63)
        Label3.TabIndex = 5
        Label3.Text = "可于此体验效果"
        ' 
        ' ListView1
        ' 
        ListView1.BackColor = Color.FromArgb(CByte(48), CByte(48), CByte(48))
        ListView1.BorderStyle = BorderStyle.FixedSingle
        ListView1.Columns.AddRange(New ColumnHeader() {ColumnHeader1, ColumnHeader2, ColumnHeader3})
        ListView1.Dock = DockStyle.Top
        ListView1.Font = New Font("Microsoft YaHei UI", 10F)
        ListView1.ForeColor = Color.Silver
        ListView1.FullRowSelect = True
        ListView1.Items.AddRange(New ListViewItem() {ListViewItem1, ListViewItem2, ListViewItem3, ListViewItem4, ListViewItem5, ListViewItem6, ListViewItem7, ListViewItem8, ListViewItem9, ListViewItem10})
        ListView1.Location = New Point(20, 503)
        ListView1.Name = "ListView1"
        ListView1.Size = New Size(940, 200)
        ListView1.TabIndex = 6
        ListView1.UseCompatibleStateImageBehavior = False
        ListView1.View = View.Details
        ' 
        ' ColumnHeader1
        ' 
        ColumnHeader1.Width = 250
        ' 
        ' ColumnHeader2
        ' 
        ColumnHeader2.Width = 250
        ' 
        ' ColumnHeader3
        ' 
        ColumnHeader3.Width = 250
        ' 
        ' Form_ListViewDirectReDraw
        ' 
        AutoScaleDimensions = New SizeF(120F, 120F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(24), CByte(24), CByte(24))
        ClientSize = New Size(980, 771)
        Controls.Add(ListView1)
        Controls.Add(Label3)
        Controls.Add(ModernTextBox2)
        Controls.Add(Label2)
        Controls.Add(ModernTextBox1)
        Controls.Add(Label1)
        ForeColor = Color.Silver
        Name = "Form_ListViewDirectReDraw"
        Padding = New Padding(20)
        Text = "Form_ListViewDirectReDraw"
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Friend WithEvents Label1 As Label
    Friend WithEvents ModernTextBox1 As ModernTextBox
    Friend WithEvents Label2 As Label
    Friend WithEvents ModernTextBox2 As ModernTextBox
    Friend WithEvents Label3 As Label
    Friend WithEvents ListView1 As ListView
    Friend WithEvents ColumnHeader1 As ColumnHeader
    Friend WithEvents ColumnHeader2 As ColumnHeader
    Friend WithEvents ColumnHeader3 As ColumnHeader
End Class
