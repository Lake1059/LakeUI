Public Class Form_MarkDownViewer
    Private Sub Form_MarkDownViewer_Load(sender As Object, e As EventArgs) Handles MyBase.Load

    End Sub

    Private Sub Form_MarkDownViewer_SizeChanged(sender As Object, e As EventArgs) Handles Me.SizeChanged
        Me.ModernTextBox1.Width = (Panel1.Width - Me.JustEmptyControl2.Width) * 0.5
    End Sub

    Private Sub ModernTextBox1_TextChanged(sender As Object, e As EventArgs) Handles ModernTextBox1.TextChanged
        Me.MarkDownViewer1.Text = Me.ModernTextBox1.Text
    End Sub

End Class