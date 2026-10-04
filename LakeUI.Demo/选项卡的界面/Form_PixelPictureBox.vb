Imports System.Threading

Public Class Form_PixelPictureBox
    Private Const SampleEdge As Long = 262144
    Private ReadOnly _sampleEncoding As New PixelPictureColorEncoding(PixelPictureColorSpace.ScRgb, PixelPictureTransferFunction.Linear, isHdr:=True)
    Private _sampleSource As PixelPictureTiledSource

    Private Sub Form_PixelPictureBox_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        PixelPictureBox1.AllowDrop = False
        _sampleSource = New PixelPictureTiledSource(SampleEdge, SampleEdge, AddressOf ProvideSampleTile, _sampleEncoding,
            frameDelaysMilliseconds:=Enumerable.Repeat(100, 12))
        PixelPictureBox1.Source = _sampleSource
        Label6.Text = "262144 × 262144 HDR 动图：滚轮连续缩放、左键拖动、右键框选。控件外按需生成 tile，不解码文件。"
    End Sub

    Private Sub PixelPictureBox1_SelectionChanged(sender As Object, e As EventArgs) Handles PixelPictureBox1.SelectionChanged
        Label6.Text = $"选择区域：{PixelPictureBox1.SelectionRect}；原始像素值由 Source 提供，不读取屏幕颜色。"
    End Sub

    Private Sub PixelPictureBox1_DragEnter(sender As Object, e As DragEventArgs) Handles PixelPictureBox1.DragEnter
        e.Effect = If(e.Data.GetDataPresent(DataFormats.FileDrop), DragDropEffects.Copy, DragDropEffects.None)
    End Sub

    Private Sub PixelPictureBox1_DragDrop(sender As Object, e As DragEventArgs) Handles PixelPictureBox1.DragDrop
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            Label6.Text = "PixelPictureBox 只负责 GPU 呈现；请在控件外将文件解码为 IPixelPictureSource，再赋给 Source。"
        End If
    End Sub

    Private Sub ReleaseLoadedImage(sender As Object, e As EventArgs) Handles Me.Disposed
        _sampleSource?.Dispose()
    End Sub

    Private Function ProvideSampleTile(request As PixelPictureTileRequest, cancellationToken As CancellationToken) As Task(Of PixelPictureTile)
        Dim width = CInt(Math.Min(request.TileSize, Math.Ceiling(SampleEdge / request.Scale) - request.Column * request.TileSize))
        Dim height = CInt(Math.Min(request.TileSize, Math.Ceiling(SampleEdge / request.Scale) - request.Row * request.TileSize))
        Dim pixels(width * height * 4 - 1) As Single
        Dim phase = request.FrameIndex / 12.0 * Math.PI * 2
        For row As Integer = 0 To height - 1
            cancellationToken.ThrowIfCancellationRequested()
            Dim imageY = (request.Row * request.TileSize + row) * request.Scale / SampleEdge
            For column As Integer = 0 To width - 1
                Dim imageX = (request.Column * request.TileSize + column) * request.Scale / SampleEdge
                Dim pulse = 0.5 + 0.5 * Math.Sin(imageX * Math.PI * 8 + phase)
                Dim offset = (row * width + column) * 4
                pixels(offset) = CSng(imageX * 4)
                pixels(offset + 1) = CSng(imageY * 2)
                pixels(offset + 2) = CSng(pulse * 3)
                pixels(offset + 3) = 1
            Next
        Next
        Return Task.FromResult(New PixelPictureTile(width, height, pixels, _sampleEncoding))
    End Function

    Private Sub PixelPictureBox1_PixelHovered(sender As Object, e As PixelPicturePixelEventArgs) Handles PixelPictureBox1.PixelHovered
        If Not e.HasPixel Then Return
        Dim pixel = e.Pixel
        Label1.Text = $"({pixel.X}, {pixel.Y}) · linear scRGB = ({pixel.Red:F3}, {pixel.Green:F3}, {pixel.Blue:F3}, {pixel.Alpha:F3})"
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
