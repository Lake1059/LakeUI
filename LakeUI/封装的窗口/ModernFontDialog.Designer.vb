<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class ModernFontDialog
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
        Panel1 = New Panel()
        ModernListBox1 = New ModernListBox()
        Label2 = New Label()
        ModernTextBox1 = New ModernTextBox()
        Panel2 = New Panel()
        Panel5 = New Panel()
        Panel7 = New Panel()
        Label10 = New Label()
        BooleanSwitch2 = New BooleanSwitch()
        Label7 = New Label()
        Panel6 = New Panel()
        Label9 = New Label()
        BooleanSwitch1 = New BooleanSwitch()
        Label8 = New Label()
        Panel4 = New Panel()
        ModernListBox3 = New ModernListBox()
        Label5 = New Label()
        ModernTextBox3 = New ModernTextBox()
        Label6 = New Label()
        Panel3 = New Panel()
        ModernListBox2 = New ModernListBox()
        Label3 = New Label()
        ModernTextBox2 = New ModernTextBox()
        Label4 = New Label()
        Panel8 = New Panel()
        ModernButton2 = New ModernButton()
        Label11 = New Label()
        ModernButton1 = New ModernButton()
        Panel9 = New Panel()
        Label16 = New Label()
        Label15 = New Label()
        Label14 = New Label()
        Label13 = New Label()
        Label12 = New Label()
        Panel1.SuspendLayout()
        Panel2.SuspendLayout()
        Panel5.SuspendLayout()
        Panel7.SuspendLayout()
        Panel6.SuspendLayout()
        Panel4.SuspendLayout()
        Panel3.SuspendLayout()
        Panel8.SuspendLayout()
        Panel9.SuspendLayout()
        SuspendLayout()
        ' 
        ' Label1
        ' 
        Label1.AutoSize = True
        Label1.Dock = DockStyle.Top
        Label1.Location = New Point(20, 20)
        Label1.Name = "Label1"
        Label1.Padding = New Padding(0, 0, 0, 10)
        Label1.Size = New Size(37, 30)
        Label1.TabIndex = 0
        Label1.Text = "字体"
        ' 
        ' Panel1
        ' 
        Panel1.Controls.Add(ModernListBox1)
        Panel1.Controls.Add(Label2)
        Panel1.Controls.Add(ModernTextBox1)
        Panel1.Controls.Add(Label1)
        Panel1.Dock = DockStyle.Left
        Panel1.Location = New Point(0, 0)
        Panel1.Name = "Panel1"
        Panel1.Padding = New Padding(20, 20, 0, 20)
        Panel1.Size = New Size(300, 611)
        Panel1.TabIndex = 1
        ' 
        ' ModernListBox1
        ' 
        ModernListBox1.BorderRadius = 10
        ModernListBox1.BorderSize = 2
        ModernListBox1.Dock = DockStyle.Fill
        ModernListBox1.Location = New Point(20, 100)
        ModernListBox1.Margin = New Padding(2, 2, 2, 2)
        ModernListBox1.Name = "ModernListBox1"
        ModernListBox1.Padding = New Padding(10)
        ModernListBox1.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernListBox1.ScrollBarWidth = 12
        ModernListBox1.Size = New Size(280, 491)
        ModernListBox1.TabIndex = 3
        ' 
        ' Label2
        ' 
        Label2.Dock = DockStyle.Top
        Label2.Location = New Point(20, 90)
        Label2.Name = "Label2"
        Label2.Size = New Size(280, 10)
        Label2.TabIndex = 2
        ' 
        ' ModernTextBox1
        ' 
        ModernTextBox1.BorderRadius = 10
        ModernTextBox1.BorderSize = 2
        ModernTextBox1.CaretColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        ModernTextBox1.Dock = DockStyle.Top
        ModernTextBox1.Location = New Point(20, 50)
        ModernTextBox1.Margin = New Padding(2, 2, 2, 2)
        ModernTextBox1.Name = "ModernTextBox1"
        ModernTextBox1.Padding = New Padding(13, 0, 13, 0)
        ModernTextBox1.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernTextBox1.Size = New Size(280, 40)
        ModernTextBox1.TabIndex = 1
        ModernTextBox1.WaterText = "选择字体"
        ' 
        ' Panel2
        ' 
        Panel2.Controls.Add(Panel5)
        Panel2.Controls.Add(Panel4)
        Panel2.Controls.Add(Panel3)
        Panel2.Dock = DockStyle.Top
        Panel2.Location = New Point(300, 0)
        Panel2.Name = "Panel2"
        Panel2.Size = New Size(584, 300)
        Panel2.TabIndex = 2
        ' 
        ' Panel5
        ' 
        Panel5.Controls.Add(Panel7)
        Panel5.Controls.Add(Label7)
        Panel5.Controls.Add(Panel6)
        Panel5.Controls.Add(Label8)
        Panel5.Dock = DockStyle.Fill
        Panel5.Location = New Point(400, 0)
        Panel5.Name = "Panel5"
        Panel5.Padding = New Padding(20)
        Panel5.Size = New Size(184, 300)
        Panel5.TabIndex = 4
        ' 
        ' Panel7
        ' 
        Panel7.Controls.Add(Label10)
        Panel7.Controls.Add(BooleanSwitch2)
        Panel7.Dock = DockStyle.Top
        Panel7.Location = New Point(20, 90)
        Panel7.Name = "Panel7"
        Panel7.Size = New Size(144, 30)
        Panel7.TabIndex = 4
        ' 
        ' Label10
        ' 
        Label10.Dock = DockStyle.Fill
        Label10.Location = New Point(55, 0)
        Label10.Name = "Label10"
        Label10.Padding = New Padding(10, 0, 0, 0)
        Label10.Size = New Size(89, 30)
        Label10.TabIndex = 1
        Label10.Text = "下划线"
        Label10.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' BooleanSwitch2
        ' 
        BooleanSwitch2.Dock = DockStyle.Left
        BooleanSwitch2.KnobColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        BooleanSwitch2.Location = New Point(0, 0)
        BooleanSwitch2.Margin = New Padding(2, 2, 2, 2)
        BooleanSwitch2.Name = "BooleanSwitch2"
        BooleanSwitch2.Size = New Size(55, 30)
        BooleanSwitch2.TabIndex = 0
        ' 
        ' Label7
        ' 
        Label7.Dock = DockStyle.Top
        Label7.Location = New Point(20, 80)
        Label7.Name = "Label7"
        Label7.Size = New Size(144, 10)
        Label7.TabIndex = 3
        ' 
        ' Panel6
        ' 
        Panel6.Controls.Add(Label9)
        Panel6.Controls.Add(BooleanSwitch1)
        Panel6.Dock = DockStyle.Top
        Panel6.Location = New Point(20, 50)
        Panel6.Name = "Panel6"
        Panel6.Size = New Size(144, 30)
        Panel6.TabIndex = 1
        ' 
        ' Label9
        ' 
        Label9.Dock = DockStyle.Fill
        Label9.Location = New Point(55, 0)
        Label9.Name = "Label9"
        Label9.Padding = New Padding(10, 0, 0, 0)
        Label9.Size = New Size(89, 30)
        Label9.TabIndex = 1
        Label9.Text = "删除线"
        Label9.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' BooleanSwitch1
        ' 
        BooleanSwitch1.Dock = DockStyle.Left
        BooleanSwitch1.KnobColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        BooleanSwitch1.Location = New Point(0, 0)
        BooleanSwitch1.Margin = New Padding(2, 2, 2, 2)
        BooleanSwitch1.Name = "BooleanSwitch1"
        BooleanSwitch1.Size = New Size(55, 30)
        BooleanSwitch1.TabIndex = 0
        ' 
        ' Label8
        ' 
        Label8.AutoSize = True
        Label8.Dock = DockStyle.Top
        Label8.Location = New Point(20, 20)
        Label8.Name = "Label8"
        Label8.Padding = New Padding(0, 0, 0, 10)
        Label8.Size = New Size(37, 30)
        Label8.TabIndex = 0
        Label8.Text = "效果"
        ' 
        ' Panel4
        ' 
        Panel4.Controls.Add(ModernListBox3)
        Panel4.Controls.Add(Label5)
        Panel4.Controls.Add(ModernTextBox3)
        Panel4.Controls.Add(Label6)
        Panel4.Dock = DockStyle.Left
        Panel4.Location = New Point(200, 0)
        Panel4.Name = "Panel4"
        Panel4.Padding = New Padding(20, 20, 0, 20)
        Panel4.Size = New Size(200, 300)
        Panel4.TabIndex = 3
        ' 
        ' ModernListBox3
        ' 
        ModernListBox3.BorderRadius = 10
        ModernListBox3.BorderSize = 2
        ModernListBox3.Dock = DockStyle.Fill
        ModernListBox3.Location = New Point(20, 100)
        ModernListBox3.Margin = New Padding(2, 2, 2, 2)
        ModernListBox3.Name = "ModernListBox3"
        ModernListBox3.Padding = New Padding(10)
        ModernListBox3.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernListBox3.ScrollBarWidth = 12
        ModernListBox3.Size = New Size(180, 180)
        ModernListBox3.TabIndex = 3
        ' 
        ' Label5
        ' 
        Label5.Dock = DockStyle.Top
        Label5.Location = New Point(20, 90)
        Label5.Name = "Label5"
        Label5.Size = New Size(180, 10)
        Label5.TabIndex = 2
        ' 
        ' ModernTextBox3
        ' 
        ModernTextBox3.BorderRadius = 10
        ModernTextBox3.BorderSize = 2
        ModernTextBox3.CaretColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        ModernTextBox3.Dock = DockStyle.Top
        ModernTextBox3.Location = New Point(20, 50)
        ModernTextBox3.Margin = New Padding(2, 2, 2, 2)
        ModernTextBox3.Name = "ModernTextBox3"
        ModernTextBox3.Padding = New Padding(13, 0, 13, 0)
        ModernTextBox3.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernTextBox3.Size = New Size(180, 40)
        ModernTextBox3.TabIndex = 1
        ModernTextBox3.WaterText = "选择字号"
        ' 
        ' Label6
        ' 
        Label6.AutoSize = True
        Label6.Dock = DockStyle.Top
        Label6.Location = New Point(20, 20)
        Label6.Name = "Label6"
        Label6.Padding = New Padding(0, 0, 0, 10)
        Label6.Size = New Size(37, 30)
        Label6.TabIndex = 0
        Label6.Text = "字号"
        ' 
        ' Panel3
        ' 
        Panel3.Controls.Add(ModernListBox2)
        Panel3.Controls.Add(Label3)
        Panel3.Controls.Add(ModernTextBox2)
        Panel3.Controls.Add(Label4)
        Panel3.Dock = DockStyle.Left
        Panel3.Location = New Point(0, 0)
        Panel3.Name = "Panel3"
        Panel3.Padding = New Padding(20, 20, 0, 20)
        Panel3.Size = New Size(200, 300)
        Panel3.TabIndex = 2
        ' 
        ' ModernListBox2
        ' 
        ModernListBox2.BorderRadius = 10
        ModernListBox2.BorderSize = 2
        ModernListBox2.Dock = DockStyle.Fill
        ModernListBox2.Location = New Point(20, 100)
        ModernListBox2.Margin = New Padding(2, 2, 2, 2)
        ModernListBox2.Name = "ModernListBox2"
        ModernListBox2.Padding = New Padding(10)
        ModernListBox2.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernListBox2.ScrollBarWidth = 12
        ModernListBox2.Size = New Size(180, 180)
        ModernListBox2.TabIndex = 3
        ' 
        ' Label3
        ' 
        Label3.Dock = DockStyle.Top
        Label3.Location = New Point(20, 90)
        Label3.Name = "Label3"
        Label3.Size = New Size(180, 10)
        Label3.TabIndex = 2
        ' 
        ' ModernTextBox2
        ' 
        ModernTextBox2.BorderRadius = 10
        ModernTextBox2.BorderSize = 2
        ModernTextBox2.CaretColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        ModernTextBox2.Dock = DockStyle.Top
        ModernTextBox2.Location = New Point(20, 50)
        ModernTextBox2.Margin = New Padding(2, 2, 2, 2)
        ModernTextBox2.Name = "ModernTextBox2"
        ModernTextBox2.Padding = New Padding(13, 0, 13, 0)
        ModernTextBox2.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernTextBox2.Size = New Size(180, 40)
        ModernTextBox2.TabIndex = 1
        ModernTextBox2.WaterText = "选择字形"
        ' 
        ' Label4
        ' 
        Label4.AutoSize = True
        Label4.Dock = DockStyle.Top
        Label4.Location = New Point(20, 20)
        Label4.Name = "Label4"
        Label4.Padding = New Padding(0, 0, 0, 10)
        Label4.Size = New Size(37, 30)
        Label4.TabIndex = 0
        Label4.Text = "字形"
        ' 
        ' Panel8
        ' 
        Panel8.Controls.Add(ModernButton2)
        Panel8.Controls.Add(Label11)
        Panel8.Controls.Add(ModernButton1)
        Panel8.Dock = DockStyle.Bottom
        Panel8.Location = New Point(300, 531)
        Panel8.Name = "Panel8"
        Panel8.Padding = New Padding(20)
        Panel8.Size = New Size(584, 80)
        Panel8.TabIndex = 3
        ' 
        ' ModernButton2
        ' 
        ModernButton2.BackColor2 = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        ModernButton2.BorderRadius = 10
        ModernButton2.BorderSize = 2
        ModernButton2.Dock = DockStyle.Right
        ModernButton2.HoverBorderColor = Color.CornflowerBlue
        ModernButton2.Location = New Point(314, 20)
        ModernButton2.Margin = New Padding(2)
        ModernButton2.Name = "ModernButton2"
        ModernButton2.PressedBorderColor = Color.MediumSlateBlue
        ModernButton2.Size = New Size(120, 40)
        ModernButton2.TabIndex = 5
        ModernButton2.Text = "确定"
        ' 
        ' Label11
        ' 
        Label11.Dock = DockStyle.Right
        Label11.Location = New Point(434, 20)
        Label11.Name = "Label11"
        Label11.Size = New Size(10, 40)
        Label11.TabIndex = 4
        ' 
        ' ModernButton1
        ' 
        ModernButton1.BackColor2 = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        ModernButton1.BorderRadius = 10
        ModernButton1.BorderSize = 2
        ModernButton1.Dock = DockStyle.Right
        ModernButton1.HoverBorderColor = Color.CornflowerBlue
        ModernButton1.Location = New Point(444, 20)
        ModernButton1.Margin = New Padding(2)
        ModernButton1.Name = "ModernButton1"
        ModernButton1.PressedBorderColor = Color.MediumSlateBlue
        ModernButton1.Size = New Size(120, 40)
        ModernButton1.TabIndex = 0
        ModernButton1.Text = "取消"
        ' 
        ' Panel9
        ' 
        Panel9.Controls.Add(Label16)
        Panel9.Controls.Add(Label15)
        Panel9.Controls.Add(Label14)
        Panel9.Controls.Add(Label13)
        Panel9.Controls.Add(Label12)
        Panel9.Dock = DockStyle.Fill
        Panel9.Location = New Point(300, 300)
        Panel9.Name = "Panel9"
        Panel9.Padding = New Padding(20, 0, 20, 0)
        Panel9.Size = New Size(584, 231)
        Panel9.TabIndex = 4
        ' 
        ' Label16
        ' 
        Label16.AutoSize = True
        Label16.Dock = DockStyle.Top
        Label16.ForeColor = Color.White
        Label16.Location = New Point(20, 130)
        Label16.Name = "Label16"
        Label16.Padding = New Padding(0, 0, 0, 10)
        Label16.Size = New Size(312, 30)
        Label16.TabIndex = 5
        Label16.Text = "+-*/~！@#￥%……&&*（）-= —— · <>?:;""'[]{}|\"
        ' 
        ' Label15
        ' 
        Label15.AutoSize = True
        Label15.Dock = DockStyle.Top
        Label15.ForeColor = Color.White
        Label15.Location = New Point(20, 100)
        Label15.Name = "Label15"
        Label15.Padding = New Padding(0, 0, 0, 10)
        Label15.Size = New Size(191, 30)
        Label15.TabIndex = 4
        Label15.Text = "永字八法；横竖撇捺折钩点。"
        ' 
        ' Label14
        ' 
        Label14.AutoSize = True
        Label14.Dock = DockStyle.Top
        Label14.ForeColor = Color.White
        Label14.Location = New Point(20, 70)
        Label14.Name = "Label14"
        Label14.Padding = New Padding(0, 0, 0, 10)
        Label14.Size = New Size(315, 30)
        Label14.TabIndex = 3
        Label14.Text = "The quick brown fox jumps over the lazy dog."
        ' 
        ' Label13
        ' 
        Label13.AutoSize = True
        Label13.Dock = DockStyle.Top
        Label13.ForeColor = Color.White
        Label13.Location = New Point(20, 40)
        Label13.Name = "Label13"
        Label13.Padding = New Padding(0, 0, 0, 10)
        Label13.Size = New Size(219, 30)
        Label13.TabIndex = 2
        Label13.Text = "敏捷的棕色狐狸跳过了懒惰的狗。"
        ' 
        ' Label12
        ' 
        Label12.AutoSize = True
        Label12.Dock = DockStyle.Top
        Label12.Location = New Point(20, 0)
        Label12.Name = "Label12"
        Label12.Padding = New Padding(0, 0, 0, 20)
        Label12.Size = New Size(37, 40)
        Label12.TabIndex = 1
        Label12.Text = "预览"
        ' 
        ' ModernFontDialog
        ' 
        AutoScaleDimensions = New SizeF(96F, 96F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(48), CByte(48), CByte(48))
        ClientSize = New Size(884, 611)
        Controls.Add(Panel9)
        Controls.Add(Panel8)
        Controls.Add(Panel2)
        Controls.Add(Panel1)
        Font = New Font("Microsoft YaHei UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ForeColor = Color.Silver
        MaximizeBox = False
        MinimizeBox = False
        MinimumSize = New Size(900, 650)
        Name = "ModernFontDialog"
        ShowIcon = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.Manual
        Text = "FontDialog"
        Panel1.ResumeLayout(False)
        Panel1.PerformLayout()
        Panel2.ResumeLayout(False)
        Panel5.ResumeLayout(False)
        Panel5.PerformLayout()
        Panel7.ResumeLayout(False)
        Panel6.ResumeLayout(False)
        Panel4.ResumeLayout(False)
        Panel4.PerformLayout()
        Panel3.ResumeLayout(False)
        Panel3.PerformLayout()
        Panel8.ResumeLayout(False)
        Panel9.ResumeLayout(False)
        Panel9.PerformLayout()
        ResumeLayout(False)
    End Sub

    Friend WithEvents Label1 As Label
    Friend WithEvents Panel1 As Panel
    Friend WithEvents ModernTextBox1 As ModernTextBox
    Friend WithEvents Label2 As Label
    Friend WithEvents ModernListBox1 As ModernListBox
    Friend WithEvents Panel2 As Panel
    Friend WithEvents Panel4 As Panel
    Friend WithEvents ModernListBox3 As ModernListBox
    Friend WithEvents Label5 As Label
    Friend WithEvents ModernTextBox3 As ModernTextBox
    Friend WithEvents Label6 As Label
    Friend WithEvents Panel3 As Panel
    Friend WithEvents ModernListBox2 As ModernListBox
    Friend WithEvents Label3 As Label
    Friend WithEvents ModernTextBox2 As ModernTextBox
    Friend WithEvents Label4 As Label
    Friend WithEvents Panel5 As Panel
    Friend WithEvents Label8 As Label
    Friend WithEvents Panel6 As Panel
    Friend WithEvents Label7 As Label
    Friend WithEvents Label9 As Label
    Friend WithEvents BooleanSwitch1 As BooleanSwitch
    Friend WithEvents Panel7 As Panel
    Friend WithEvents Label10 As Label
    Friend WithEvents BooleanSwitch2 As BooleanSwitch
    Friend WithEvents Panel8 As Panel
    Friend WithEvents ModernButton1 As ModernButton
    Friend WithEvents Label11 As Label
    Friend WithEvents ModernButton2 As ModernButton
    Friend WithEvents Panel9 As Panel
    Friend WithEvents Label12 As Label
    Friend WithEvents Label15 As Label
    Friend WithEvents Label14 As Label
    Friend WithEvents Label13 As Label
    Friend WithEvents Label16 As Label
End Class
