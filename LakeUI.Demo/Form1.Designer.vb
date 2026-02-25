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
        SuspendLayout()
        ' 
        ' ExcellentButton1
        ' 
        ExcellentButton1.BackColor2 = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        ExcellentButton1.BorderRadius = 10
        ExcellentButton1.BorderSize = 2
        ExcellentButton1.Icon = CType(resources.GetObject("ExcellentButton1.Icon"), Image)
        ExcellentButton1.IconPadding = 10
        ExcellentButton1.Location = New Point(273, 154)
        ExcellentButton1.Name = "ExcellentButton1"
        ExcellentButton1.Size = New Size(200, 70)
        ExcellentButton1.SubText = "Subtitle"
        ExcellentButton1.SuperSamplingScale = ExcellentButton.SuperSamplingScaleEnum.x2
        ExcellentButton1.TabIndex = 0
        ' 
        ' Form1
        ' 
        AutoScaleDimensions = New SizeF(120F, 120F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(24), CByte(24), CByte(24))
        ClientSize = New Size(800, 450)
        Controls.Add(ExcellentButton1)
        Font = New Font("MiSans Medium", 10.7999992F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ForeColor = SystemColors.Control
        Name = "Form1"
        Text = "Form1"
        ResumeLayout(False)
    End Sub

    Friend WithEvents ExcellentButton1 As ExcellentButton

End Class
