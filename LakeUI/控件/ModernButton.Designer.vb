<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class ModernButton
    Inherits System.Windows.Forms.UserControl

    'UserControl1 overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing Then
                动画助手?.Dispose()
                If components IsNot Nothing Then
                    components.Dispose()
                End If
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
        SuspendLayout()
        ' 
        ' ModernButton
        ' 
        AutoScaleDimensions = New SizeF(96F, 96F)
        AutoScaleMode = AutoScaleMode.Dpi
        DoubleBuffered = True
        Margin = New Padding(2, 2, 2, 2)
        Name = "ModernButton"
        Size = New Size(120, 35)
        ResumeLayout(False)
    End Sub

End Class
