Public Class Form_ModernColorDialog
    Private Sub ModernButton5_Click(sender As Object, e As EventArgs) Handles ModernButton5.Click
        Dim a As New ModernColorDialog
        a.Show(Me)
    End Sub

    Private Sub ModernButton6_Click(sender As Object, e As EventArgs) Handles ModernButton6.Click
        Dim a As New ModernColorDialog
        a.ShowDialog(Me)
    End Sub
End Class