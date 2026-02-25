<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class Form1
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(Form1))
        ExcellentButton1 = New ExcellentButton()
        ExcellentButton2 = New ExcellentButton()
        ExcellentButton3 = New ExcellentButton()
        ModernTextBox1 = New ModernTextBox()
        SuspendLayout()
        ' 
        ' ExcellentButton1
        ' 
        ExcellentButton1.BackColor2 = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        ExcellentButton1.BorderRadius = 10
        ExcellentButton1.BorderSize = 2
        ExcellentButton1.Icon = CType(resources.GetObject("ExcellentButton1.Icon"), Image)
        ExcellentButton1.IconPadding = 10
        ExcellentButton1.Location = New Point(548, 48)
        ExcellentButton1.Name = "ExcellentButton1"
        ExcellentButton1.Size = New Size(207, 70)
        ExcellentButton1.SubText = "Subtitle"
        ExcellentButton1.TabIndex = 0
        ExcellentButton1.Text = "ExButton1"
        ' 
        ' ExcellentButton2
        ' 
        ExcellentButton2.Location = New Point(548, 142)
        ExcellentButton2.Name = "ExcellentButton2"
        ExcellentButton2.Size = New Size(207, 62)
        ExcellentButton2.TabIndex = 2
        ExcellentButton2.Text = "ExcellentButton2"
        ' 
        ' ExcellentButton3
        ' 
        ExcellentButton3.Location = New Point(548, 210)
        ExcellentButton3.Name = "ExcellentButton3"
        ExcellentButton3.Size = New Size(207, 62)
        ExcellentButton3.TabIndex = 3
        ExcellentButton3.Text = "ExcellentButton3"
        ' 
        ' ModernTextBox1
        ' 
        ModernTextBox1.BorderRadius = 10
        ModernTextBox1.BorderSize = 2
        ModernTextBox1.CaretColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        ModernTextBox1.Location = New Point(246, 100)
        ModernTextBox1.MultiLine = True
        ModernTextBox1.Name = "ModernTextBox1"
        ModernTextBox1.Padding = New Padding(10)
        ModernTextBox1.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernTextBox1.Size = New Size(200, 150)
        ModernTextBox1.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        ModernTextBox1.TabIndex = 4
        ModernTextBox1.Text = "ModernTextBox1"
        ' 
        ' Form1
        ' 
        AutoScaleDimensions = New SizeF(120F, 120F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(24), CByte(24), CByte(24))
        ClientSize = New Size(800, 450)
        Controls.Add(ModernTextBox1)
        Controls.Add(ExcellentButton3)
        Controls.Add(ExcellentButton2)
        Controls.Add(ExcellentButton1)
        Font = New Font("MiSans Medium", 10.7999992F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ForeColor = SystemColors.Control
        Name = "Form1"
        Text = "Form1"
        ResumeLayout(False)
    End Sub

    Friend WithEvents ExcellentButton1 As ExcellentButton
    Friend WithEvents ExcellentButton2 As ExcellentButton
    Friend WithEvents ExcellentButton3 As ExcellentButton
    Friend WithEvents ModernTextBox1 As ModernTextBox

End Class
