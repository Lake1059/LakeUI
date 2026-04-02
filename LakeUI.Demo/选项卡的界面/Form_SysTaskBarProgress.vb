Public Class Form_SysTaskBarProgress
    Private Sub ModernButton5_Click(sender As Object, e As EventArgs) Handles ModernButton5.Click
        SysTaskBarProgress.SetProgress(Me.ParentForm.Handle, SysTaskBarProgress.TaskBarProgressState.Normal, 50, 100)
    End Sub

    Private Sub ModernButton6_Click(sender As Object, e As EventArgs) Handles ModernButton6.Click
        SysTaskBarProgress.SetProgress(Me.ParentForm.Handle, SysTaskBarProgress.TaskBarProgressState.Normal, 100, 100)
    End Sub

    Private Sub ModernButton7_Click(sender As Object, e As EventArgs) Handles ModernButton7.Click
        SysTaskBarProgress.SetProgress(Me.ParentForm.Handle, SysTaskBarProgress.TaskBarProgressState.Paused, 50, 100)
    End Sub

    Private Sub ModernButton8_Click(sender As Object, e As EventArgs) Handles ModernButton8.Click
        SysTaskBarProgress.SetProgress(Me.ParentForm.Handle, SysTaskBarProgress.TaskBarProgressState.Paused, 100, 100)
    End Sub

    Private Sub ModernButton9_Click(sender As Object, e As EventArgs) Handles ModernButton9.Click
        SysTaskBarProgress.SetProgress(Me.ParentForm.Handle, SysTaskBarProgress.TaskBarProgressState.Error, 50, 100)
    End Sub

    Private Sub ModernButton10_Click(sender As Object, e As EventArgs) Handles ModernButton10.Click
        SysTaskBarProgress.SetProgress(Me.ParentForm.Handle, SysTaskBarProgress.TaskBarProgressState.Error, 100, 100)
    End Sub

    Private Sub ModernButton12_Click(sender As Object, e As EventArgs) Handles ModernButton12.Click
        SysTaskBarProgress.Clear(Me.ParentForm.Handle)
    End Sub

    Private Sub ModernButton11_Click(sender As Object, e As EventArgs) Handles ModernButton11.Click
        SysTaskBarProgress.SetProgress(Me.ParentForm.Handle, SysTaskBarProgress.TaskBarProgressState.Indeterminate, 100, 100)
    End Sub
End Class