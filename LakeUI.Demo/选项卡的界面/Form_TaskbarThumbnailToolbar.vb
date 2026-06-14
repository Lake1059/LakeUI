Public Class Form_TaskbarThumbnailToolbar
    Private Sub Form_TaskbarThumbnailToolbar_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        TaskbarThumbnailToolbar1.Buttons.Add(New ThumbnailToolbarButton(ThumbnailToolbarIcon.Previous, "上一曲"))
        TaskbarThumbnailToolbar1.Buttons.Add(New ThumbnailToolbarButton(ThumbnailToolbarIcon.Rewind, "快退"))
        TaskbarThumbnailToolbar1.Buttons.Add(New ThumbnailToolbarButton(ThumbnailToolbarIcon.Play, "播放"))
        TaskbarThumbnailToolbar1.Buttons.Add(New ThumbnailToolbarButton(ThumbnailToolbarIcon.FastForward, "快进"))
        TaskbarThumbnailToolbar1.Buttons.Add(New ThumbnailToolbarButton(ThumbnailToolbarIcon.[Next], "下一曲"))
    End Sub
    Private Sub ModernButton5_Click(sender As Object, e As EventArgs) Handles ModernButton5.Click
        TaskbarThumbnailToolbar1.Attach(Form1)
    End Sub

    Private Sub ModernButton6_Click(sender As Object, e As EventArgs) Handles ModernButton6.Click
        TaskbarThumbnailToolbar1.Detach()
    End Sub

End Class