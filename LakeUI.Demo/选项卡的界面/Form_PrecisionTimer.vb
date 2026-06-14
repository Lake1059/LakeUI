Public Class Form_PrecisionTimer
    Dim a As TimeSpan
    Private Sub PrecisionTimer1_Tick(sender As Object, e As EventArgs) Handles PrecisionTimer1.Tick
        a += TimeSpan.FromMilliseconds(1)
        Me.Label5.Text = a.ToString
    End Sub

    Private Sub ModernButton5_Click(sender As Object, e As EventArgs) Handles ModernButton5.Click
        a = New TimeSpan
        PrecisionTimer1.Start()
    End Sub

    Private Sub ModernButton6_Click(sender As Object, e As EventArgs) Handles ModernButton6.Click
        PrecisionTimer1.Stop()
    End Sub
End Class