Public Class Form_ModernColorDialog
    Private Sub ModernButton5_Click(sender As Object, e As EventArgs) Handles ModernButton5.Click
        Dim a As New ModernColorDialog
        Form1.ThisIsYourWindow1.Attach(a)
        a.MinimumSize = New Size(a.MinimumSize.Width, a.MinimumSize.Height + 15)
        a.Size = a.MinimumSize
        a.Show(Me)
    End Sub

    Private Sub ModernButton6_Click(sender As Object, e As EventArgs) Handles ModernButton6.Click
        Dim a As New ModernColorDialog
        Form1.ThisIsYourWindow1.Attach(a)
        a.MinimumSize = New Size(a.MinimumSize.Width, a.MinimumSize.Height + 15)
        a.Size = a.MinimumSize
        a.ShowDialog(Me)
    End Sub

    Private Sub Form_ModernColorDialog_Load(sender As Object, e As EventArgs) Handles MyBase.Load

    End Sub
End Class