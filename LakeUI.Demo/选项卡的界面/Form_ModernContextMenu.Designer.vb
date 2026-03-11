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
        Label2 = New Label()
        ModernTextBox1 = New ModernTextBox()
        Label1 = New Label()
        ReDrawContextMenuStrip1 = New ReDrawContextMenuStrip()
        ToolStripSeparator1 = New ToolStripSeparator()
        ToolStripMenuItem1 = New ToolStripMenuItem()
        ToolStripMenuItem2 = New ToolStripMenuItem()
        ToolStripMenuItem3 = New ToolStripMenuItem()
        ToolStripSeparator2 = New ToolStripSeparator()
        ToolStripMenuItem4 = New ToolStripMenuItem()
        ToolStripMenuItem5 = New ToolStripMenuItem()
        ToolStripMenuItem6 = New ToolStripMenuItem()
        ToolStripSeparator3 = New ToolStripSeparator()
        ReDrawContextMenuStrip1.SuspendLayout()
        SuspendLayout()
        ' 
        ' Label2
        ' 
        Label2.AutoSize = True
        Label2.Dock = DockStyle.Top
        Label2.Font = New Font("Microsoft YaHei UI", 10F)
        Label2.Location = New Point(20, 227)
        Label2.Name = "Label2"
        Label2.Padding = New Padding(0, 20, 0, 20)
        Label2.Size = New Size(197, 63)
        Label2.TabIndex = 6
        Label2.Text = "可以在此页右键查看效果"
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
        ModernTextBox1.Size = New Size(903, 150)
        ModernTextBox1.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        ModernTextBox1.TabIndex = 5
        ModernTextBox1.Text = "ModernTextBox1"
        ' 
        ' Label1
        ' 
        Label1.AutoSize = True
        Label1.Dock = DockStyle.Top
        Label1.Font = New Font("Microsoft YaHei UI", 16.2F, FontStyle.Bold, GraphicsUnit.Point, CByte(134))
        Label1.Location = New Point(20, 20)
        Label1.Name = "Label1"
        Label1.Padding = New Padding(0, 0, 0, 20)
        Label1.Size = New Size(619, 57)
        Label1.TabIndex = 4
        Label1.Text = "重绘的上下文菜单 ReDrawContextMenuStrip"
        ' 
        ' ReDrawContextMenuStrip1
        ' 
        ReDrawContextMenuStrip1.BackColor = Color.FromArgb(CByte(36), CByte(36), CByte(36))
        ReDrawContextMenuStrip1.BorderSize = 2
        ReDrawContextMenuStrip1.ForeColor = Color.Silver
        ReDrawContextMenuStrip1.ImageScalingSize = New Size(20, 20)
        ReDrawContextMenuStrip1.Items.AddRange(New ToolStripItem() {ToolStripSeparator1, ToolStripMenuItem1, ToolStripMenuItem2, ToolStripMenuItem3, ToolStripSeparator2, ToolStripMenuItem4, ToolStripMenuItem5, ToolStripMenuItem6, ToolStripSeparator3})
        ReDrawContextMenuStrip1.Name = "ReDrawContextMenuStrip1"
        ReDrawContextMenuStrip1.SeparatorHeight = 2
        ReDrawContextMenuStrip1.SeparatorMarginSize = 10
        ReDrawContextMenuStrip1.ShowCheckMargin = True
        ReDrawContextMenuStrip1.ShowImageMargin = False
        ReDrawContextMenuStrip1.Size = New Size(231, 226)
        ReDrawContextMenuStrip1.SpacerHeight = 10
        ' 
        ' ToolStripSeparator1
        ' 
        ToolStripSeparator1.Name = "ToolStripSeparator1"
        ToolStripSeparator1.Padding = New Padding(0, 5, 0, 5)
        ToolStripSeparator1.Size = New Size(227, 6)
        ToolStripSeparator1.Tag = "null"
        ' 
        ' ToolStripMenuItem1
        ' 
        ToolStripMenuItem1.Name = "ToolStripMenuItem1"
        ToolStripMenuItem1.Padding = New Padding(0, 5, 0, 5)
        ToolStripMenuItem1.Size = New Size(230, 34)
        ToolStripMenuItem1.Text = "ToolStripMenuItem1"
        ' 
        ' ToolStripMenuItem2
        ' 
        ToolStripMenuItem2.Checked = True
        ToolStripMenuItem2.CheckState = CheckState.Checked
        ToolStripMenuItem2.ForeColor = Color.YellowGreen
        ToolStripMenuItem2.Name = "ToolStripMenuItem2"
        ToolStripMenuItem2.Padding = New Padding(0, 5, 0, 5)
        ToolStripMenuItem2.Size = New Size(230, 34)
        ToolStripMenuItem2.Text = "ToolStripMenuItem2"
        ' 
        ' ToolStripMenuItem3
        ' 
        ToolStripMenuItem3.Checked = True
        ToolStripMenuItem3.CheckState = CheckState.Checked
        ToolStripMenuItem3.ForeColor = Color.CornflowerBlue
        ToolStripMenuItem3.Name = "ToolStripMenuItem3"
        ToolStripMenuItem3.Padding = New Padding(0, 5, 0, 5)
        ToolStripMenuItem3.Size = New Size(230, 34)
        ToolStripMenuItem3.Text = "ToolStripMenuItem3"
        ' 
        ' ToolStripSeparator2
        ' 
        ToolStripSeparator2.Name = "ToolStripSeparator2"
        ToolStripSeparator2.Padding = New Padding(0, 5, 0, 5)
        ToolStripSeparator2.Size = New Size(227, 6)
        ToolStripSeparator2.Tag = ""
        ' 
        ' ToolStripMenuItem4
        ' 
        ToolStripMenuItem4.Font = New Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ToolStripMenuItem4.Name = "ToolStripMenuItem4"
        ToolStripMenuItem4.Padding = New Padding(0, 5, 0, 5)
        ToolStripMenuItem4.Size = New Size(230, 34)
        ToolStripMenuItem4.Text = "ToolStripMenuItem4"
        ' 
        ' ToolStripMenuItem5
        ' 
        ToolStripMenuItem5.Font = New Font("华文新魏", 9F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ToolStripMenuItem5.Name = "ToolStripMenuItem5"
        ToolStripMenuItem5.Padding = New Padding(0, 5, 0, 5)
        ToolStripMenuItem5.Size = New Size(230, 34)
        ToolStripMenuItem5.Text = "ToolStripMenuItem5"
        ' 
        ' ToolStripMenuItem6
        ' 
        ToolStripMenuItem6.Font = New Font("华文中宋", 9F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ToolStripMenuItem6.Name = "ToolStripMenuItem6"
        ToolStripMenuItem6.Padding = New Padding(0, 5, 0, 5)
        ToolStripMenuItem6.Size = New Size(230, 34)
        ToolStripMenuItem6.Text = "ToolStripMenuItem6"
        ' 
        ' ToolStripSeparator3
        ' 
        ToolStripSeparator3.Name = "ToolStripSeparator3"
        ToolStripSeparator3.Padding = New Padding(0, 5, 0, 5)
        ToolStripSeparator3.Size = New Size(227, 6)
        ToolStripSeparator3.Tag = "null"
        ' 
        ' Form_ModernContextMenu
        ' 
        AutoScaleDimensions = New SizeF(120F, 120F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(24), CByte(24), CByte(24))
        ClientSize = New Size(943, 775)
        ContextMenuStrip = ReDrawContextMenuStrip1
        Controls.Add(Label2)
        Controls.Add(ModernTextBox1)
        Controls.Add(Label1)
        ForeColor = Color.Silver
        Name = "Form_ModernContextMenu"
        Padding = New Padding(20)
        Text = "Form_ModernContextMenu"
        ReDrawContextMenuStrip1.ResumeLayout(False)
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Friend WithEvents Label2 As Label
    Friend WithEvents ModernTextBox1 As ModernTextBox
    Friend WithEvents Label1 As Label
    Friend WithEvents ReDrawContextMenuStrip1 As ReDrawContextMenuStrip
    Friend WithEvents ToolStripSeparator1 As ToolStripSeparator
    Friend WithEvents ToolStripMenuItem1 As ToolStripMenuItem
    Friend WithEvents ToolStripMenuItem2 As ToolStripMenuItem
    Friend WithEvents ToolStripMenuItem3 As ToolStripMenuItem
    Friend WithEvents ToolStripSeparator2 As ToolStripSeparator
    Friend WithEvents ToolStripMenuItem4 As ToolStripMenuItem
    Friend WithEvents ToolStripMenuItem5 As ToolStripMenuItem
    Friend WithEvents ToolStripMenuItem6 As ToolStripMenuItem
    Friend WithEvents ToolStripSeparator3 As ToolStripSeparator
End Class
