Public Class Form_ProgressRing
    Private Sub ModernButton5_Click(sender As Object, e As EventArgs) Handles ModernButton5.Click
        Me.ProgressRing1.StartAnimation()
        Me.ProgressRing2.StartAnimation()
    End Sub

    Private Sub ModernButton6_Click(sender As Object, e As EventArgs) Handles ModernButton6.Click
        Me.ProgressRing1.StopAnimation()
        Me.ProgressRing2.StopAnimation()
    End Sub
End Class