<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class ModernTextBox
    Inherits System.Windows.Forms.UserControl

    'UserControl 重写释放以清理组件列表。
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing Then
                _caretBlinkTimer.Stop()
                _caretBlinkTimer.Dispose()
                _scrollAnimationHelper.Dispose()
                _autoScrollTimer.Stop()
                _autoScrollTimer.Dispose()
                If components IsNot Nothing Then
                    components.Dispose()
                End If
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
        SuspendLayout()
        ' 
        ' ModernTextBox
        ' 
        AutoScaleDimensions = New SizeF(96F, 96F)
        AutoScaleMode = AutoScaleMode.Dpi
        Margin = New Padding(2, 2, 2, 2)
        Name = "ModernTextBox"
        Size = New Size(120, 32)
        ResumeLayout(False)
    End Sub

End Class
