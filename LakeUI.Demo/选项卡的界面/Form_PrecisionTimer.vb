Public Class Form_PrecisionTimer
    Private _开始时间 As Long
    Private _上次显示时间 As Long
    Private Sub PrecisionTimer1_Tick(sender As Object, e As EventArgs) Handles PrecisionTimer1.Tick
        Dim now = Stopwatch.GetTimestamp()
        If _开始时间 = 0 Then _开始时间 = now
        If now - _上次显示时间 < Stopwatch.Frequency \ 60 Then Return
        _上次显示时间 = now
        Me.Label5.Text = Stopwatch.GetElapsedTime(_开始时间).ToString()
    End Sub

    Private Sub ModernButton5_Click(sender As Object, e As EventArgs) Handles ModernButton5.Click
        _开始时间 = Stopwatch.GetTimestamp()
        _上次显示时间 = 0
        Me.Label5.Text = TimeSpan.Zero.ToString()
        PrecisionTimer1.Start()
    End Sub

    Private Sub ModernButton6_Click(sender As Object, e As EventArgs) Handles ModernButton6.Click
        PrecisionTimer1.Stop()
    End Sub
End Class
