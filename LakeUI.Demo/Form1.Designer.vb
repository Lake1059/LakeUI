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
        ModernTextBox1 = New ModernTextBox()
        ModernComboBox1 = New ModernComboBox()
        BooleanSwitch1 = New BooleanSwitch()
        QuantumSwitch1 = New QuantumSwitch()
        QuantumSwitch2 = New QuantumSwitch()
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
        ' ModernTextBox1
        ' 
        ModernTextBox1.BorderRadius = 10
        ModernTextBox1.BorderSize = 2
        ModernTextBox1.CaretColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        ModernTextBox1.Location = New Point(170, 48)
        ModernTextBox1.MultiLine = True
        ModernTextBox1.Name = "ModernTextBox1"
        ModernTextBox1.Padding = New Padding(10)
        ModernTextBox1.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernTextBox1.Size = New Size(303, 224)
        ModernTextBox1.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        ModernTextBox1.TabIndex = 4
        ModernTextBox1.Text = "ModernTextBox1"
        ' 
        ' ModernComboBox1
        ' 
        ModernComboBox1.BackColor2 = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        ModernComboBox1.BorderRadius = 10
        ModernComboBox1.BorderSize = 2
        ModernComboBox1.CaretColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        ModernComboBox1.DropDownBorderRadius = 10
        ModernComboBox1.DropDownBorderSize = 2
        ModernComboBox1.DropDownGap = 5
        ModernComboBox1.DropDownPadding = New Padding(10)
        ModernComboBox1.DropDownScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernComboBox1.HoverBackColor2 = Color.FromArgb(CByte(80), CByte(80), CByte(80))
        ModernComboBox1.Items.Add("现代化下拉框")
        ModernComboBox1.Items.Add("支持定制超多部件和颜色")
        ModernComboBox1.Items.Add("灵活适应各种交互场景")
        ModernComboBox1.Items.Add("")
        ModernComboBox1.Items.Add("")
        ModernComboBox1.Location = New Point(170, 298)
        ModernComboBox1.Name = "ModernComboBox1"
        ModernComboBox1.Padding = New Padding(15, 0, 10, 0)
        ModernComboBox1.PressedBackColor2 = SystemColors.WindowFrame
        ModernComboBox1.Size = New Size(303, 40)
        ModernComboBox1.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        ModernComboBox1.TabIndex = 5
        ModernComboBox1.Text = "ModernComboBox1"
        ModernComboBox1.WaterText = "水印文字"
        ' 
        ' BooleanSwitch1
        ' 
        BooleanSwitch1.AnimationFPS = 0
        BooleanSwitch1.KnobColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        BooleanSwitch1.Location = New Point(548, 246)
        BooleanSwitch1.Name = "BooleanSwitch1"
        BooleanSwitch1.Size = New Size(75, 40)
        BooleanSwitch1.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        BooleanSwitch1.TabIndex = 6
        ' 
        ' QuantumSwitch1
        ' 
        QuantumSwitch1.AnimationFPS = 0
        QuantumSwitch1.KnobColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        QuantumSwitch1.KnobColorIndeterminate = Color.FromArgb(CByte(180), CByte(180), CByte(180))
        QuantumSwitch1.Location = New Point(548, 330)
        QuantumSwitch1.Name = "QuantumSwitch1"
        QuantumSwitch1.Size = New Size(100, 40)
        QuantumSwitch1.State = QuantumSwitch.QuantumStateEnum.Superposition
        QuantumSwitch1.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        QuantumSwitch1.TabIndex = 7
        ' 
        ' QuantumSwitch2
        ' 
        QuantumSwitch2.AnimationFPS = 0
        QuantumSwitch2.KnobColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        QuantumSwitch2.KnobColorIndeterminate = Color.FromArgb(CByte(180), CByte(180), CByte(180))
        QuantumSwitch2.Location = New Point(668, 330)
        QuantumSwitch2.Name = "QuantumSwitch2"
        QuantumSwitch2.ObserverMode = True
        QuantumSwitch2.Size = New Size(100, 40)
        QuantumSwitch2.State = QuantumSwitch.QuantumStateEnum.Indeterminate
        QuantumSwitch2.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        QuantumSwitch2.TabIndex = 8
        ' 
        ' Form1
        ' 
        AutoScaleDimensions = New SizeF(120F, 120F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(48), CByte(48), CByte(48))
        ClientSize = New Size(1076, 747)
        Controls.Add(QuantumSwitch2)
        Controls.Add(QuantumSwitch1)
        Controls.Add(BooleanSwitch1)
        Controls.Add(ModernComboBox1)
        Controls.Add(ModernTextBox1)
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
    Friend WithEvents ModernTextBox1 As ModernTextBox
    Friend WithEvents ModernComboBox1 As ModernComboBox
    Friend WithEvents BooleanSwitch1 As BooleanSwitch
    Friend WithEvents QuantumSwitch1 As QuantumSwitch
    Friend WithEvents QuantumSwitch2 As QuantumSwitch

End Class
