Public Class Form_PixelPictureBox
    Private _imageLoad As Threading.CancellationTokenSource
    Private _loadedImage As Image
    Private Sub Form_PixelPictureBox_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        PixelPictureBox1.AllowDrop = True
    End Sub

    Private Sub PixelPictureBox1_SelectionChanged(sender As Object, e As EventArgs) Handles PixelPictureBox1.SelectionChanged
        PictureBox1.Image = Nothing
        PictureBox2.Image = Nothing
        PictureBox3.Image = Nothing
        PictureBox4.Image = Nothing
        PictureBox1.Image = PixelPictureBox1.GetCornerMagnifier(PixelPictureBox.SelectionCorner.TopLeft, 5, 10)
        PictureBox2.Image = PixelPictureBox1.GetCornerMagnifier(PixelPictureBox.SelectionCorner.TopRight, 5, 10)
        PictureBox3.Image = PixelPictureBox1.GetCornerMagnifier(PixelPictureBox.SelectionCorner.BottomLeft, 5, 10)
        PictureBox4.Image = PixelPictureBox1.GetCornerMagnifier(PixelPictureBox.SelectionCorner.BottomRight, 5, 10)
    End Sub

    Private Sub PixelPictureBox1_DragEnter(sender As Object, e As DragEventArgs) Handles PixelPictureBox1.DragEnter
        e.Effect = DragDropEffects.Copy
    End Sub

    Private Async Sub PixelPictureBox1_DragDrop(sender As Object, e As DragEventArgs) Handles PixelPictureBox1.DragDrop
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            Dim files = DirectCast(e.Data.GetData(DataFormats.FileDrop), String())
            If files.Length > 0 Then
                _imageLoad?.Cancel()
                Dim load As New Threading.CancellationTokenSource()
                _imageLoad = load
                Try
                    Dim prepared = Await D3D_ImagePreparation.LoadBitmapAsync(files(0), load.Token)
                    If load.IsCancellationRequested OrElse IsDisposed OrElse _imageLoad IsNot load Then
                        prepared.Dispose()
                        Return
                    End If
                    Using D3D_PaintBridge.BeginRenderUpdate(Me)
                        Dim previous = _loadedImage
                        _loadedImage = prepared
                        PixelPictureBox1.Image = prepared
                        previous?.Dispose()
                    End Using
                Catch ex As OperationCanceledException
                Catch ex As Exception
                    If Not IsDisposed AndAlso _imageLoad Is load Then
                        MessageBox.Show("无法加载图片: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    End If
                Finally
                    If _imageLoad Is load Then _imageLoad = Nothing
                    load.Dispose()
                End Try
            End If
        End If
    End Sub

    Private Sub ReleaseLoadedImage(sender As Object, e As EventArgs) Handles Me.Disposed
        _imageLoad?.Cancel()
        _loadedImage?.Dispose()
        _loadedImage = Nothing
    End Sub

    Private Sub Form_PixelPictureBox_SizeChanged(sender As Object, e As EventArgs) Handles Me.SizeChanged
        PictureBox1.Width = PictureBox1.Height
        PictureBox2.Width = PictureBox2.Height
        PictureBox3.Width = PictureBox3.Height
        PictureBox4.Width = PictureBox4.Height
        Dim a = 0
        For Each c As Control In Panel2.Controls
            a += c.Width
        Next
        Panel2.Padding = New Padding((Panel2.Width - a) * 0.5, Panel2.Padding.Top, Panel2.Padding.Right, Panel2.Padding.Bottom)
    End Sub
End Class
