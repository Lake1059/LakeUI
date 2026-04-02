<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form2
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
        Label1 = New Label()
        SuspendLayout()
        ' 
        ' Label1
        ' 
        Label1.Dock = DockStyle.Fill
        Label1.Location = New Point(0, 0)
        Label1.Name = "Label1"
        Label1.Padding = New Padding(10)
        Label1.Size = New Size(334, 161)
        Label1.TabIndex = 0
        Label1.Text = "完全体需要 Windows 11 较新版本，Windows 10 上强制最外层固定颜色的单边框，无法去除；不支持最外层圆角功能，因为 WinForms 自身不擅长窗口级透明，还浪费性能，至于分层阴影也是窗口级透明为什么那个不浪费性能那你别管。"
        ' 
        ' Form2
        ' 
        AutoScaleDimensions = New SizeF(96F, 96F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(24), CByte(24), CByte(24))
        ClientSize = New Size(334, 161)
        Controls.Add(Label1)
        ForeColor = Color.Silver
        Margin = New Padding(2)
        Name = "Form2"
        Text = "ThisIsYourWindow"
        ResumeLayout(False)
    End Sub

    Friend WithEvents Label1 As Label
End Class
