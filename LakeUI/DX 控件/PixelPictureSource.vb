Imports System.Collections.Generic
Imports System.Numerics
Imports D2D = Vortice.Direct2D1

Public Enum PixelPictureColorSpace
    Unknown = 0
    SRgb = 1
    DisplayP3 = 2
    AdobeRgb = 3
    Rec709 = 4
    Rec2020 = 5
    ScRgb = 6
    Hdr10 = 7
    Hlg = 8
End Enum

Public Enum PixelPictureTransferFunction
    Unknown = 0
    SRgb = 1
    Gamma22 = 2
    Linear = 3
    Pq = 4
    Hlg = 5
End Enum

Public Enum PixelPicturePixelFormat
    Unknown = 0
    Bgra32 = 1
    Rgba32 = 2
    Rgba16Float = 3
    Rgba32Float = 4
    Rgb10A2 = 5
    Rgb565 = 6
End Enum

Public Structure PixelPicturePixel
    Private ReadOnly _x As Long
    Private ReadOnly _y As Long
    Private ReadOnly _red As Single
    Private ReadOnly _green As Single
    Private ReadOnly _blue As Single
    Private ReadOnly _alpha As Single
    Private ReadOnly _colorSpace As PixelPictureColorSpace
    Private ReadOnly _transferFunction As PixelPictureTransferFunction
    Private ReadOnly _pixelFormat As PixelPicturePixelFormat
    Private ReadOnly _isHdr As Boolean

    Public Sub New(x As Long,
                   y As Long,
                   red As Single,
                   green As Single,
                   blue As Single,
                   alpha As Single,
                   colorSpace As PixelPictureColorSpace,
                   transferFunction As PixelPictureTransferFunction,
                   pixelFormat As PixelPicturePixelFormat,
                   isHdr As Boolean)
        _x = x
        _y = y
        _red = red
        _green = green
        _blue = blue
        _alpha = alpha
        _colorSpace = colorSpace
        _transferFunction = transferFunction
        _pixelFormat = pixelFormat
        _isHdr = isHdr
    End Sub

    Public ReadOnly Property X As Long
        Get
            Return _x
        End Get
    End Property

    Public ReadOnly Property Y As Long
        Get
            Return _y
        End Get
    End Property

    Public ReadOnly Property Red As Single
        Get
            Return _red
        End Get
    End Property

    Public ReadOnly Property Green As Single
        Get
            Return _green
        End Get
    End Property

    Public ReadOnly Property Blue As Single
        Get
            Return _blue
        End Get
    End Property

    Public ReadOnly Property Alpha As Single
        Get
            Return _alpha
        End Get
    End Property

    Public ReadOnly Property ColorSpace As PixelPictureColorSpace
        Get
            Return _colorSpace
        End Get
    End Property

    Public ReadOnly Property TransferFunction As PixelPictureTransferFunction
        Get
            Return _transferFunction
        End Get
    End Property

    Public ReadOnly Property PixelFormat As PixelPicturePixelFormat
        Get
            Return _pixelFormat
        End Get
    End Property

    Public ReadOnly Property IsHdr As Boolean
        Get
            Return _isHdr
        End Get
    End Property
End Structure

Public NotInheritable Class PixelPicturePixelEventArgs
    Inherits EventArgs

    Public Sub New(clientLocation As Point)
        Me.ClientLocation = clientLocation
        HasPixel = False
    End Sub

    Public Sub New(clientLocation As Point, pixel As PixelPicturePixel)
        Me.ClientLocation = clientLocation
        Me.Pixel = pixel
        HasPixel = True
    End Sub

    Public ReadOnly Property ClientLocation As Point
    Public ReadOnly Property HasPixel As Boolean
    Public ReadOnly Property Pixel As PixelPicturePixel
End Class

Public Interface IPixelPictureSource
    ReadOnly Property Width As Long
    ReadOnly Property Height As Long
    ReadOnly Property FrameCount As Integer
    ReadOnly Property ColorSpace As PixelPictureColorSpace
    ReadOnly Property TransferFunction As PixelPictureTransferFunction
    ReadOnly Property PixelFormat As PixelPicturePixelFormat
    ReadOnly Property IsHdr As Boolean
    ReadOnly Property LoopCount As Integer

    Function GetFrameDelayMilliseconds(frameIndex As Integer) As Integer

    ''' <summary>
    ''' Renders only the requested source rectangle. Tile decoders should use this request to select
    ''' a pyramid level and upload only visible tiles. The control never decodes or creates images.
    ''' </summary>
    Function Present(context As D3D_PaintContext,
                     destination As RectangleF,
                     source As PixelPictureRegion,
                     frameIndex As Integer,
                     interpolation As D2D.InterpolationMode) As Boolean

    Function TryGetPixel(x As Long,
                         y As Long,
                         frameIndex As Integer,
                         ByRef pixel As PixelPicturePixel) As Boolean
End Interface

Public NotInheritable Class PixelPictureGpuFrame
    Public Sub New(image As D2D.ID2D1Image, Optional frameDelayMilliseconds As Integer = 100)
        If image Is Nothing Then Throw New ArgumentNullException(NameOf(image))
        Me.Image = image
        Me.FrameDelayMilliseconds = Math.Max(1, frameDelayMilliseconds)
    End Sub

    Public ReadOnly Property Image As D2D.ID2D1Image
    Public ReadOnly Property FrameDelayMilliseconds As Integer
End Class

''' <summary>Direct GPU image source. Resource ownership stays with the caller.</summary>
Public NotInheritable Class PixelPictureGpuSource
    Implements IPixelPictureSource

    Private ReadOnly _width As Long
    Private ReadOnly _height As Long
    Private ReadOnly _frames As IReadOnlyList(Of PixelPictureGpuFrame)
    Private ReadOnly _colorSpace As PixelPictureColorSpace
    Private ReadOnly _transferFunction As PixelPictureTransferFunction
    Private ReadOnly _pixelFormat As PixelPicturePixelFormat
    Private ReadOnly _isHdr As Boolean

    Public Sub New(width As Long,
                   height As Long,
                   frames As IEnumerable(Of PixelPictureGpuFrame),
                   Optional colorSpace As PixelPictureColorSpace = PixelPictureColorSpace.ScRgb,
                   Optional transferFunction As PixelPictureTransferFunction = PixelPictureTransferFunction.Linear,
                   Optional pixelFormat As PixelPicturePixelFormat = PixelPicturePixelFormat.Rgba16Float,
                   Optional isHdr As Boolean = False)
        If width <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(width))
        If height <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(height))
        If frames Is Nothing Then Throw New ArgumentNullException(NameOf(frames))
        If colorSpace <> PixelPictureColorSpace.ScRgb OrElse transferFunction <> PixelPictureTransferFunction.Linear Then Throw New ArgumentException("GPU 帧必须已转换为线性 scRGB；编码色域请使用 PixelPictureTiledSource 或外部 GPU 色彩管理。")

        Dim frameList As New List(Of PixelPictureGpuFrame)(frames)
        If frameList.Count = 0 Then Throw New ArgumentException("At least one frame is required.", NameOf(frames))
        If frameList.Any(Function(frame) frame Is Nothing) Then Throw New ArgumentException(NameOf(frames))

        _width = width
        _height = height
        _frames = frameList.AsReadOnly()
        _colorSpace = colorSpace
        _transferFunction = transferFunction
        _pixelFormat = pixelFormat
        _isHdr = isHdr OrElse colorSpace = PixelPictureColorSpace.Hdr10 OrElse colorSpace = PixelPictureColorSpace.Hlg OrElse colorSpace = PixelPictureColorSpace.ScRgb
    End Sub

    Public ReadOnly Property Width As Long Implements IPixelPictureSource.Width
        Get
            Return _width
        End Get
    End Property

    Public ReadOnly Property Height As Long Implements IPixelPictureSource.Height
        Get
            Return _height
        End Get
    End Property

    Public ReadOnly Property FrameCount As Integer Implements IPixelPictureSource.FrameCount
        Get
            Return _frames.Count
        End Get
    End Property

    Public ReadOnly Property ColorSpace As PixelPictureColorSpace Implements IPixelPictureSource.ColorSpace
        Get
            Return _colorSpace
        End Get
    End Property

    Public ReadOnly Property TransferFunction As PixelPictureTransferFunction Implements IPixelPictureSource.TransferFunction
        Get
            Return _transferFunction
        End Get
    End Property

    Public ReadOnly Property PixelFormat As PixelPicturePixelFormat Implements IPixelPictureSource.PixelFormat
        Get
            Return _pixelFormat
        End Get
    End Property

    Public ReadOnly Property IsHdr As Boolean Implements IPixelPictureSource.IsHdr
        Get
            Return _isHdr
        End Get
    End Property

    Public ReadOnly Property LoopCount As Integer Implements IPixelPictureSource.LoopCount
        Get
            Return 0
        End Get
    End Property

    Public Function GetFrameDelayMilliseconds(frameIndex As Integer) As Integer Implements IPixelPictureSource.GetFrameDelayMilliseconds
        Return _frames(NormalizeFrameIndex(frameIndex)).FrameDelayMilliseconds
    End Function

    Public Function Present(context As D3D_PaintContext,
                            destination As RectangleF,
                            source As PixelPictureRegion,
                            frameIndex As Integer,
                            interpolation As D2D.InterpolationMode) As Boolean Implements IPixelPictureSource.Present
        If context Is Nothing OrElse destination.Width <= 0 OrElse destination.Height <= 0 OrElse source.Width <= 0 OrElse source.Height <= 0 Then Return False

        Dim frame = _frames(NormalizeFrameIndex(frameIndex))
        If frame.Image Is Nothing Then Return False

        Dim scaleX As Single = destination.Width / source.Width
        Dim scaleY As Single = destination.Height / source.Height
        If Single.IsNaN(scaleX) OrElse Single.IsNaN(scaleY) OrElse Single.IsInfinity(scaleX) OrElse Single.IsInfinity(scaleY) Then Return False

        Dim previousTransform = context.DeviceContext.Transform
        Try
            context.BeginTextureUse()
            Dim bitmap = TryCast(frame.Image, D2D.ID2D1Bitmap)
            If bitmap IsNot Nothing Then
                context.DeviceContext.DrawBitmap(bitmap, D3D_PaintContext.ToRawRect(destination), 1.0F, interpolation, D3D_PaintContext.ToRawRect(source.ToRectangleF()), Nothing)
                Return True
            End If
            context.DeviceContext.Transform = Matrix3x2.CreateScale(scaleX, scaleY) *
                                              Matrix3x2.CreateTranslation(destination.X, destination.Y) *
                                              previousTransform
            context.DeviceContext.DrawImage(frame.Image,
                                            New Nullable(Of Vector2)(),
                                            D3D_PaintContext.ToRawRect(source.ToRectangleF()),
                                            interpolation,
                                            D2D.CompositeMode.SourceOver)
            Return True
        Finally
            context.DeviceContext.Transform = previousTransform
        End Try
    End Function

    Public Function TryGetPixel(x As Long,
                                y As Long,
                                frameIndex As Integer,
                                ByRef pixel As PixelPicturePixel) As Boolean Implements IPixelPictureSource.TryGetPixel
        pixel = Nothing
        Return False
    End Function

    Private Function NormalizeFrameIndex(frameIndex As Integer) As Integer
        Return Math.Max(0, Math.Min(_frames.Count - 1, frameIndex))
    End Function
End Class

''' <summary>Callback source for tiled, streamed, HDR, wide-gamut, and decoder-owned images.</summary>
Public NotInheritable Class PixelPictureCallbackSource
    Implements IPixelPictureSource

    Public Delegate Function RenderDelegate(context As D3D_PaintContext,
                                             destination As RectangleF,
                                             source As PixelPictureRegion,
                                             frameIndex As Integer,
                                             interpolation As D2D.InterpolationMode) As Boolean

    Public Delegate Function PixelReaderDelegate(x As Long,
                                                 y As Long,
                                                 frameIndex As Integer,
                                                 ByRef pixel As PixelPicturePixel) As Boolean

    Private ReadOnly _width As Long
    Private ReadOnly _height As Long
    Private ReadOnly _render As RenderDelegate
    Private ReadOnly _pixelReader As PixelReaderDelegate
    Private ReadOnly _frameDelays As Integer()
    Private ReadOnly _colorSpace As PixelPictureColorSpace
    Private ReadOnly _transferFunction As PixelPictureTransferFunction
    Private ReadOnly _pixelFormat As PixelPicturePixelFormat
    Private ReadOnly _isHdr As Boolean
    Private ReadOnly _loopCount As Integer

    Public Sub New(width As Long,
                   height As Long,
                   render As RenderDelegate,
                   Optional pixelReader As PixelReaderDelegate = Nothing,
                   Optional frameDelaysMilliseconds As IEnumerable(Of Integer) = Nothing,
                   Optional loopCount As Integer = 0,
                   Optional colorSpace As PixelPictureColorSpace = PixelPictureColorSpace.Unknown,
                   Optional transferFunction As PixelPictureTransferFunction = PixelPictureTransferFunction.Unknown,
                   Optional pixelFormat As PixelPicturePixelFormat = PixelPicturePixelFormat.Unknown,
                   Optional isHdr As Boolean = False)
        If width <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(width))
        If height <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(height))
        If render Is Nothing Then Throw New ArgumentNullException(NameOf(render))

        _width = width
        _height = height
        _render = render
        _pixelReader = pixelReader
        _frameDelays = NormalizeFrameDelays(frameDelaysMilliseconds)
        _loopCount = Math.Max(0, loopCount)
        _colorSpace = colorSpace
        _transferFunction = transferFunction
        _pixelFormat = pixelFormat
        _isHdr = isHdr OrElse colorSpace = PixelPictureColorSpace.Hdr10 OrElse colorSpace = PixelPictureColorSpace.Hlg OrElse colorSpace = PixelPictureColorSpace.ScRgb
    End Sub

    Public ReadOnly Property Width As Long Implements IPixelPictureSource.Width
        Get
            Return _width
        End Get
    End Property

    Public ReadOnly Property Height As Long Implements IPixelPictureSource.Height
        Get
            Return _height
        End Get
    End Property

    Public ReadOnly Property FrameCount As Integer Implements IPixelPictureSource.FrameCount
        Get
            Return _frameDelays.Length
        End Get
    End Property

    Public ReadOnly Property ColorSpace As PixelPictureColorSpace Implements IPixelPictureSource.ColorSpace
        Get
            Return _colorSpace
        End Get
    End Property

    Public ReadOnly Property TransferFunction As PixelPictureTransferFunction Implements IPixelPictureSource.TransferFunction
        Get
            Return _transferFunction
        End Get
    End Property

    Public ReadOnly Property PixelFormat As PixelPicturePixelFormat Implements IPixelPictureSource.PixelFormat
        Get
            Return _pixelFormat
        End Get
    End Property

    Public ReadOnly Property IsHdr As Boolean Implements IPixelPictureSource.IsHdr
        Get
            Return _isHdr
        End Get
    End Property

    Public ReadOnly Property LoopCount As Integer Implements IPixelPictureSource.LoopCount
        Get
            Return _loopCount
        End Get
    End Property

    Public Function GetFrameDelayMilliseconds(frameIndex As Integer) As Integer Implements IPixelPictureSource.GetFrameDelayMilliseconds
        Return _frameDelays(NormalizeFrameIndex(frameIndex))
    End Function

    Public Function Present(context As D3D_PaintContext,
                            destination As RectangleF,
                            source As PixelPictureRegion,
                            frameIndex As Integer,
                            interpolation As D2D.InterpolationMode) As Boolean Implements IPixelPictureSource.Present
        Return _render(context, destination, source, NormalizeFrameIndex(frameIndex), interpolation)
    End Function

    Public Function TryGetPixel(x As Long,
                                y As Long,
                                frameIndex As Integer,
                                ByRef pixel As PixelPicturePixel) As Boolean Implements IPixelPictureSource.TryGetPixel
        pixel = Nothing
        If _pixelReader Is Nothing Then Return False
        Return _pixelReader(x, y, NormalizeFrameIndex(frameIndex), pixel)
    End Function

    Private Function NormalizeFrameIndex(frameIndex As Integer) As Integer
        Return Math.Max(0, Math.Min(_frameDelays.Length - 1, frameIndex))
    End Function

    Private Shared Function NormalizeFrameDelays(values As IEnumerable(Of Integer)) As Integer()
        If values Is Nothing Then Return New Integer() {100}
        Dim list As New List(Of Integer)()
        For Each value In values
            list.Add(Math.Max(1, value))
        Next
        If list.Count = 0 Then list.Add(100)
        Return list.ToArray()
    End Function
End Class
