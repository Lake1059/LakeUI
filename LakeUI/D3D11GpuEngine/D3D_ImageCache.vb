Imports System.Drawing.Imaging
Imports System.Runtime.CompilerServices
Imports Vortice.Direct2D1
Imports Vortice.Mathematics

''' <summary>
''' D3D_ImageCache 将 Image/Icon/Bitmap 上传为 GPU bitmap，并按 source identity、尺寸、frame index、device generation 建 key。
''' 它缓存 GPU bitmap，不缓存预缩放 CPU bitmap；图片缩放由 GPU sampler 完成。
''' CPU 解码只允许短生命周期 staging；GIF/多帧图只按 frame index 缓存当前帧，源 Image 所有权仍属于调用方。
''' </summary>
Public NotInheritable Class D3D_ImageCache
    Implements IDisposable

    Private ReadOnly _textureCache As D3D_TextureCache
    Private _disposed As Boolean

    Public Sub New(textureCache As D3D_TextureCache)
        _textureCache = textureCache
    End Sub

    Public Function GetBitmap(context As D3D_PaintContext, image As Image, Optional frameIndex As Integer = 0) As ID2D1Bitmap1
        If _disposed Then Throw New ObjectDisposedException(NameOf(D3D_ImageCache))
        If context Is Nothing Then Throw New ArgumentNullException(NameOf(context))
        If image Is Nothing Then Return Nothing
        If image.Width <= 0 OrElse image.Height <= 0 Then Return Nothing

        Dim generation = context.DeviceGeneration
        Dim key = BuildKey(image, frameIndex, generation)
        Dim bytes = CLng(image.Width) * CLng(image.Height) * 4L

        Return _textureCache.AcquireTexture(Of ID2D1Bitmap1)(
            key,
            generation,
            bytes,
            Function() UploadImage(context.DeviceContext, image))
    End Function

    ''' <summary>
    ''' 绘制图片。cover/zoom/source rect 等策略后续可在此扩展，当前基础能力直接使用 GPU DrawBitmap。
    ''' </summary>
    Public Sub DrawImage(context As D3D_PaintContext,
                         image As Image,
                         destination As RectangleF,
                         Optional source As RectangleF? = Nothing,
                         Optional opacity As Single = 1.0F,
                         Optional frameIndex As Integer = 0,
                         Optional interpolation As InterpolationMode = InterpolationMode.Linear)
        Dim bitmap = GetBitmap(context, image, frameIndex)
        If bitmap Is Nothing Then Return

        Dim dst As Vortice.RawRectF? = D3D_PaintContext.ToRawRect(destination)
        Dim src As Vortice.RawRectF? = Nothing
        If source.HasValue Then src = D3D_PaintContext.ToRawRect(source.Value)

        context.DeviceContext.DrawBitmap(bitmap, dst, Math.Max(0.0F, Math.Min(1.0F, opacity)), interpolation, src, Nothing)
    End Sub

    Public Sub Invalidate()
        _textureCache.ReleaseByPrefix("image:")
    End Sub

    Private Shared Function BuildKey(image As Image, frameIndex As Integer, generation As Integer) As String
        Return "image:" &
               RuntimeHelpers.GetHashCode(image).ToString(Globalization.CultureInfo.InvariantCulture) & ":" &
               image.Width.ToString(Globalization.CultureInfo.InvariantCulture) & "x" &
               image.Height.ToString(Globalization.CultureInfo.InvariantCulture) & ":" &
               frameIndex.ToString(Globalization.CultureInfo.InvariantCulture) & ":" &
               generation.ToString(Globalization.CultureInfo.InvariantCulture)
    End Function

    Private Shared Function UploadImage(context As ID2D1DeviceContext, image As Image) As ID2D1Bitmap1
        Using staging As New Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb)
            staging.SetResolution(96.0F, 96.0F)
            Using g = Graphics.FromImage(staging)
                g.CompositingMode = Drawing2D.CompositingMode.SourceCopy
                g.DrawImage(image, 0, 0, image.Width, image.Height)
            End Using

            Dim rect As New Rectangle(0, 0, staging.Width, staging.Height)
            Dim data = staging.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb)
            Try
                Dim props As New BitmapProperties1(
                    New Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                    96.0F,
                    96.0F,
                    BitmapOptions.None)

                Return context.CreateBitmap(New SizeI(staging.Width, staging.Height), data.Scan0, CUInt(data.Stride), props)
            Finally
                staging.UnlockBits(data)
            End Try
        End Using
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        GC.SuppressFinalize(Me)
    End Sub
End Class
