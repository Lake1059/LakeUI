<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class AgentDialogueList
    Inherits System.Windows.Forms.UserControl

    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing Then
                If _progressAnimation IsNot Nothing Then
                    _progressAnimation.StopFrameLoop()
                    _progressAnimation.Dispose()
                End If
                If _toolTip IsNot Nothing Then _toolTip.Dispose()
                If _folderFont IsNot Nothing Then _folderFont.Dispose()
                If _dialogueFont IsNot Nothing Then _dialogueFont.Dispose()
                If _subtitleFont IsNot Nothing Then _subtitleFont.Dispose()
                If components IsNot Nothing Then components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    Private components As System.ComponentModel.IContainer

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        SuspendLayout()
        AutoScaleDimensions = New SizeF(96.0!, 96.0!)
        AutoScaleMode = AutoScaleMode.Dpi
        Name = "AgentDialogueList"
        ResumeLayout(False)
    End Sub
End Class
