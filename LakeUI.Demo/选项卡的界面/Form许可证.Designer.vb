<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form许可证
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
        ModernTextBox1 = New ModernTextBox()
        ModernPanel1.SuspendLayout()
        SuspendLayout()
        ' 
        ' ModernPanel1
        ' 
        ModernPanel1.BorderSize = 0
        ModernPanel1.Controls.Add(ModernTextBox1)
        ModernPanel1.Dock = DockStyle.Fill
        ModernPanel1.Location = New Point(0, 0)
        ModernPanel1.Name = "ModernPanel1"
        ModernPanel1.Size = New Size(702, 582)
        ModernPanel1.TabIndex = 4
        ' 
        ' ModernTextBox1
        ' 
        ModernTextBox1.BackColor1 = Color.Transparent
        ModernTextBox1.BorderSize = 0
        ModernTextBox1.CaretColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        ModernTextBox1.Dock = DockStyle.Fill
        ModernTextBox1.Font = New Font("Microsoft YaHei UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ModernTextBox1.LinkDetection = True
        ModernTextBox1.Location = New Point(0, 0)
        ModernTextBox1.Margin = New Padding(2, 2, 2, 2)
        ModernTextBox1.MaxUndoCount = 0
        ModernTextBox1.MultiLine = True
        ModernTextBox1.Name = "ModernTextBox1"
        ModernTextBox1.Padding = New Padding(23, 20, 23, 20)
        ModernTextBox1.ReadOnly = True
        ModernTextBox1.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernTextBox1.Size = New Size(702, 582)
        ModernTextBox1.TabIndex = 4
        ModernTextBox1.Text = "ModernTextBox1"
        ' 
        ' Form许可证
        ' 
        AutoScaleDimensions = New SizeF(7F, 17F)
        AutoScaleMode = AutoScaleMode.Font
        BackColor = Color.FromArgb(CByte(36), CByte(36), CByte(36))
        ClientSize = New Size(702, 582)
        Controls.Add(ModernPanel1)
        ForeColor = Color.Silver
        Margin = New Padding(2, 3, 2, 3)
        Name = "Form许可证"
        Text = "Form许可证"
        ModernPanel1.ResumeLayout(False)
        ResumeLayout(False)
    End Sub

    Friend WithEvents ModernPanel1 As ModernPanel
    Friend WithEvents ModernTextBox1 As ModernTextBox
End Class
