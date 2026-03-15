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
        ModernTextBox1 = New ModernTextBox()
        SuspendLayout()
        ' 
        ' ModernTextBox1
        ' 
        ModernTextBox1.BackColor1 = Color.FromArgb(CByte(48), CByte(48), CByte(48))
        ModernTextBox1.BorderSize = 0
        ModernTextBox1.CaretColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        ModernTextBox1.Dock = DockStyle.Fill
        ModernTextBox1.Font = New Font("Microsoft YaHei UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ModernTextBox1.LineHeight = 30
        ModernTextBox1.Location = New Point(0, 0)
        ModernTextBox1.Margin = New Padding(2, 2, 2, 2)
        ModernTextBox1.MaxUndoCount = 0
        ModernTextBox1.MultiLine = True
        ModernTextBox1.Name = "ModernTextBox1"
        ModernTextBox1.Padding = New Padding(23, 26, 23, 26)
        ModernTextBox1.ReadOnly = True
        ModernTextBox1.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernTextBox1.Size = New Size(702, 582)
        ModernTextBox1.TabIndex = 3
        ModernTextBox1.Text = "ModernTextBox1"
        ' 
        ' Form许可证
        ' 
        AutoScaleDimensions = New SizeF(7F, 17F)
        AutoScaleMode = AutoScaleMode.Font
        BackColor = Color.FromArgb(CByte(36), CByte(36), CByte(36))
        ClientSize = New Size(702, 582)
        Controls.Add(ModernTextBox1)
        ForeColor = Color.Silver
        Margin = New Padding(2, 3, 2, 3)
        Name = "Form许可证"
        Text = "Form许可证"
        ResumeLayout(False)
    End Sub

    Friend WithEvents ModernTextBox1 As ModernTextBox
End Class
