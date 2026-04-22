Public Class Form2
    Private Sub Form2_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        DwmWindowStyle.SetCornerMode(Me.Handle, DwmWindowStyle.CornerMode.Square)
        DwmWindowStyle.SetDarkMode(Me.Handle, True)
    End Sub
End Class