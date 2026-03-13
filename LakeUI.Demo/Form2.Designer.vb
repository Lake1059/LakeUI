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
        HtmlColorLabel1 = New HtmlColorLabel()
        ModernPanel1 = New ModernPanel()
        ExcellentProgressBar1 = New ExcellentProgressBar()
        RoundDashBoard1 = New RoundDashBoard()
        ModernPanel1.SuspendLayout()
        SuspendLayout()
        ' 
        ' HtmlColorLabel1
        ' 
        HtmlColorLabel1.AutoSize = True
        HtmlColorLabel1.AutoSizeMode = AutoSizeMode.GrowAndShrink
        HtmlColorLabel1.Dock = DockStyle.Top
        HtmlColorLabel1.Font = New Font("微软雅黑", 10.2F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        HtmlColorLabel1.Location = New Point(1, 1)
        HtmlColorLabel1.Margin = New Padding(2, 2, 2, 2)
        HtmlColorLabel1.Name = "HtmlColorLabel1"
        HtmlColorLabel1.Padding = New Padding(10)
        HtmlColorLabel1.Size = New Size(241, 121)
        HtmlColorLabel1.TabIndex = 26
        HtmlColorLabel1.Text = "HtmlColorLabel1 <span style=""color:Green"">这是专用于显示高亮文字的标签控件</span> <span style=""color:CornflowerBlue"">直接写 HTML 的文字颜色标记即可</span> <span style=""color:IndianRed"">支持 HTML 自身颜色、十六进制、RGB、RGBA、HSL</span>"
        ' 
        ' ModernPanel1
        ' 
        ModernPanel1.Controls.Add(HtmlColorLabel1)
        ModernPanel1.Location = New Point(297, 111)
        ModernPanel1.Margin = New Padding(2)
        ModernPanel1.Name = "ModernPanel1"
        ModernPanel1.Size = New Size(244, 159)
        ModernPanel1.TabIndex = 27
        ' 
        ' ExcellentProgressBar1
        ' 
        ExcellentProgressBar1.AnimationFPS = 0
        ExcellentProgressBar1.FillGradientColor = Color.Fuchsia
        ExcellentProgressBar1.Location = New Point(37, 24)
        ExcellentProgressBar1.Margin = New Padding(2, 2, 2, 2)
        ExcellentProgressBar1.Name = "ExcellentProgressBar1"
        ExcellentProgressBar1.Size = New Size(200, 18)
        ExcellentProgressBar1.TabIndex = 28
        ExcellentProgressBar1.Value = 100
        ' 
        ' RoundDashBoard1
        ' 
        RoundDashBoard1.AnimationDuration = 1000
        RoundDashBoard1.AnimationFPS = 0
        RoundDashBoard1.CenterTextFont = New Font("Segoe UI", 14F, FontStyle.Bold)
        RoundDashBoard1.FillGradientColor = Color.FromArgb(CByte(255), CByte(128), CByte(128))
        RoundDashBoard1.Location = New Point(70, 127)
        RoundDashBoard1.Margin = New Padding(2, 2, 2, 2)
        RoundDashBoard1.Name = "RoundDashBoard1"
        RoundDashBoard1.Radius = 60F
        RoundDashBoard1.Size = New Size(146, 143)
        RoundDashBoard1.TabIndex = 29
        RoundDashBoard1.Value = 50
        ' 
        ' Form2
        ' 
        AutoScaleDimensions = New SizeF(96F, 96F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(24), CByte(24), CByte(24))
        ClientSize = New Size(652, 430)
        Controls.Add(RoundDashBoard1)
        Controls.Add(ExcellentProgressBar1)
        Controls.Add(ModernPanel1)
        ForeColor = Color.Silver
        Margin = New Padding(2)
        Name = "Form2"
        Text = "Form2"
        ModernPanel1.ResumeLayout(False)
        ModernPanel1.PerformLayout()
        ResumeLayout(False)
    End Sub
    Friend WithEvents HtmlColorLabel1 As HtmlColorLabel
    Friend WithEvents ModernPanel1 As ModernPanel
    Friend WithEvents ExcellentProgressBar1 As ExcellentProgressBar
    Friend WithEvents RoundDashBoard1 As RoundDashBoard
End Class
