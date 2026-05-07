<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form基本信息
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
        Label1 = New Label()
        ModernPanel1.SuspendLayout()
        SuspendLayout()
        ' 
        ' ModernPanel1
        ' 
        ModernPanel1.BackColor = Color.Transparent
        ModernPanel1.BackColor1 = Color.Transparent
        ModernPanel1.BorderSize = 0
        ModernPanel1.Controls.Add(ModernTextBox1)
        ModernPanel1.Controls.Add(Label1)
        ModernPanel1.Dock = DockStyle.Fill
        ModernPanel1.Location = New Point(0, 0)
        ModernPanel1.Name = "ModernPanel1"
        ModernPanel1.Padding = New Padding(16)
        ModernPanel1.ScrollBarMode = ModernPanel.ScrollMode.None
        ModernPanel1.Size = New Size(734, 589)
        ModernPanel1.TabIndex = 2
        ' 
        ' ModernTextBox1
        ' 
        ModernTextBox1.BackColor1 = Color.FromArgb(CByte(80), CByte(0), CByte(0), CByte(0))
        ModernTextBox1.BorderRadius = 20
        ModernTextBox1.BorderSize = 0
        ModernTextBox1.CaretColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        ModernTextBox1.Dock = DockStyle.Fill
        ModernTextBox1.Font = New Font("Microsoft YaHei UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ModernTextBox1.Location = New Point(16, 62)
        ModernTextBox1.Margin = New Padding(2, 2, 2, 2)
        ModernTextBox1.MultiLine = True
        ModernTextBox1.Name = "ModernTextBox1"
        ModernTextBox1.Padding = New Padding(16)
        ModernTextBox1.ReadOnly = True
        ModernTextBox1.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernTextBox1.SelectionColor = Color.FromArgb(CByte(40), CByte(220), CByte(220), CByte(220))
        ModernTextBox1.Size = New Size(702, 511)
        ModernTextBox1.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        ModernTextBox1.TabIndex = 3
        ModernTextBox1.Text = "ModernTextBox1"
        ' 
        ' Label1
        ' 
        Label1.AutoSize = True
        Label1.Dock = DockStyle.Top
        Label1.Font = New Font("Microsoft YaHei UI", 16.2F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        Label1.Location = New Point(16, 16)
        Label1.Margin = New Padding(2, 0, 2, 0)
        Label1.Name = "Label1"
        Label1.Padding = New Padding(0, 0, 0, 16)
        Label1.Size = New Size(133, 46)
        Label1.TabIndex = 2
        Label1.Text = "湖界 LakeUI"
        ' 
        ' Form基本信息
        ' 
        AutoScaleDimensions = New SizeF(96F, 96F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(36), CByte(36), CByte(36))
        ClientSize = New Size(734, 589)
        Controls.Add(ModernPanel1)
        ForeColor = Color.Silver
        Margin = New Padding(2)
        Name = "Form基本信息"
        Text = "Form基本信息"
        ModernPanel1.ResumeLayout(False)
        ModernPanel1.PerformLayout()
        ResumeLayout(False)
    End Sub

    Friend WithEvents ModernPanel1 As ModernPanel
    Friend WithEvents ModernTextBox1 As ModernTextBox
    Friend WithEvents Label1 As Label
End Class
