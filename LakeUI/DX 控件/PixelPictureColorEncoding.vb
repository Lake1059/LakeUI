''' <summary>已解码 RGB 数值的色彩解释；不涉及文件或图片编解码。</summary>
Public NotInheritable Class PixelPictureColorEncoding
    Public Sub New(Optional colorSpace As PixelPictureColorSpace = PixelPictureColorSpace.SRgb,
                   Optional transferFunction As PixelPictureTransferFunction = PixelPictureTransferFunction.SRgb,
                   Optional referenceWhiteNits As Single = 80.0F,
                   Optional hlgPeakNits As Single = 1000.0F)
        If Not Single.IsFinite(referenceWhiteNits) OrElse referenceWhiteNits <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(referenceWhiteNits))
        If Not Single.IsFinite(hlgPeakNits) OrElse hlgPeakNits < 400 Then Throw New ArgumentOutOfRangeException(NameOf(hlgPeakNits))
        If colorSpace = PixelPictureColorSpace.Unknown Then Throw New ArgumentException("必须声明像素色域。", NameOf(colorSpace))
        If transferFunction = PixelPictureTransferFunction.Unknown Then Throw New ArgumentException("必须声明传递函数。", NameOf(transferFunction))
        Me.ColorSpace = colorSpace
        Me.TransferFunction = transferFunction
        Me.ReferenceWhiteNits = referenceWhiteNits
        Me.HlgPeakNits = hlgPeakNits
    End Sub

    Public ReadOnly Property ColorSpace As PixelPictureColorSpace
    Public ReadOnly Property TransferFunction As PixelPictureTransferFunction
    Public ReadOnly Property ReferenceWhiteNits As Single
    Public ReadOnly Property HlgPeakNits As Single
    Public ReadOnly Property IsHdr As Boolean
        Get
            Return TransferFunction = PixelPictureTransferFunction.Pq OrElse TransferFunction = PixelPictureTransferFunction.Hlg OrElse ColorSpace = PixelPictureColorSpace.ScRgb
        End Get
    End Property

    Friend Function ToLinearScRgb(red As Single, green As Single, blue As Single) As System.Numerics.Vector3
        Dim linearRed = Decode(red)
        Dim linearGreen = Decode(green)
        Dim linearBlue = Decode(blue)
        If TransferFunction = PixelPictureTransferFunction.Hlg Then
            Dim luminance = Math.Max(0, 0.2627 * linearRed + 0.678 * linearGreen + 0.0593 * linearBlue)
            Dim systemGamma = 1.2 + 0.42 * Math.Log10(HlgPeakNits / 1000.0)
            Dim gain = Math.Pow(luminance, systemGamma - 1.0) * HlgPeakNits / 80.0
            linearRed *= gain
            linearGreen *= gain
            linearBlue *= gain
        End If
        Select Case ColorSpace
            Case PixelPictureColorSpace.DisplayP3
                Return New System.Numerics.Vector3(CSng(1.224745 * linearRed - 0.224904 * linearGreen),
                                                   CSng(-0.042058 * linearRed + 1.042081 * linearGreen),
                                                   CSng(-0.019642 * linearRed - 0.078655 * linearGreen + 1.098537 * linearBlue))
            Case PixelPictureColorSpace.AdobeRgb
                Return New System.Numerics.Vector3(CSng(1.398355 * linearRed - 0.398355 * linearGreen),
                                                   CSng(linearGreen), CSng(-0.042929 * linearGreen + 1.042929 * linearBlue))
            Case PixelPictureColorSpace.Rec2020, PixelPictureColorSpace.Hdr10, PixelPictureColorSpace.Hlg
                Return New System.Numerics.Vector3(CSng(1.660491 * linearRed - 0.587641 * linearGreen - 0.07285 * linearBlue),
                                                   CSng(-0.12455 * linearRed + 1.1329 * linearGreen - 0.008349 * linearBlue),
                                                   CSng(-0.018151 * linearRed - 0.100579 * linearGreen + 1.11873 * linearBlue))
            Case Else
                Return New System.Numerics.Vector3(CSng(linearRed), CSng(linearGreen), CSng(linearBlue))
        End Select
    End Function

    Private Function Decode(value As Single) As Double
        If Not Single.IsFinite(value) Then Return 0
        Dim magnitude = Math.Abs(CDbl(value))
        Dim sign = Math.Sign(value)
        Dim linear As Double
        Select Case TransferFunction
            Case PixelPictureTransferFunction.Linear
                linear = value
            Case PixelPictureTransferFunction.Pq
                Dim power = Math.Pow(Math.Clamp(CDbl(value), 0, 1), 32.0 / 2523.0)
                Return Math.Pow(Math.Max(power - 3424.0 / 4096.0, 0) / (2413.0 / 128.0 - 2392.0 / 128.0 * power), 16384.0 / 2610.0) * 10000.0 / 80.0
            Case PixelPictureTransferFunction.Hlg
                Dim signal = Math.Clamp(CDbl(value), 0, 1)
                Return If(signal <= 0.5, signal * signal / 3.0, (Math.Exp((signal - 0.55991073) / 0.17883277) + 0.28466892) / 12.0)
            Case PixelPictureTransferFunction.Gamma22
                linear = sign * Math.Pow(magnitude, If(ColorSpace = PixelPictureColorSpace.AdobeRgb, 563.0 / 256.0, 2.2))
            Case Else
                linear = sign * If(magnitude <= 0.04045, magnitude / 12.92, Math.Pow((magnitude + 0.055) / 1.055, 2.4))
        End Select
        Return linear * ReferenceWhiteNits / 80.0
    End Function
End Class

''' <summary>一个已解码的直通 RGBA float32 tile。数组在构造时复制；源数据与 HDR 查询值不被钳制到 SDR。</summary>
Public NotInheritable Class PixelPictureTile
    Private ReadOnly _pixels As Single()

    Public Sub New(width As Integer, height As Integer, pixels As Single(), encoding As PixelPictureColorEncoding)
        If width <= 0 OrElse height <= 0 OrElse width > 4096 OrElse height > 4096 Then Throw New ArgumentOutOfRangeException(NameOf(width))
        If pixels Is Nothing OrElse pixels.LongLength <> CLng(width) * height * 4 Then Throw New ArgumentException("需要紧密排列的 RGBA float32。", NameOf(pixels))
        ArgumentNullException.ThrowIfNull(encoding)
        Me.Width = width
        Me.Height = height
        Me.Encoding = encoding
        _pixels = DirectCast(pixels.Clone(), Single())
    End Sub

    Public ReadOnly Property Width As Integer
    Public ReadOnly Property Height As Integer
    Public ReadOnly Property Encoding As PixelPictureColorEncoding

    Friend Function Sample(x As Integer, y As Integer, imageX As Long, imageY As Long) As PixelPicturePixel
        Dim offset = (y * Width + x) * 4
        Return New PixelPicturePixel(imageX, imageY, _pixels(offset), _pixels(offset + 1), _pixels(offset + 2), _pixels(offset + 3), Encoding.ColorSpace, Encoding.TransferFunction, PixelPicturePixelFormat.Rgba32Float, Encoding.IsHdr)
    End Function

    Friend Function PrepareUpload() As Byte()
        Dim bytes(Width * Height * 8 - 1) As Byte
        For offset As Integer = 0 To _pixels.Length - 1 Step 4
            Dim color = Encoding.ToLinearScRgb(_pixels(offset), _pixels(offset + 1), _pixels(offset + 2))
            Dim alpha = If(Single.IsFinite(_pixels(offset + 3)), Math.Clamp(_pixels(offset + 3), 0.0F, 1.0F), 0.0F)
            WriteHalf(bytes, offset * 2, color.X * alpha)
            WriteHalf(bytes, offset * 2 + 2, color.Y * alpha)
            WriteHalf(bytes, offset * 2 + 4, color.Z * alpha)
            WriteHalf(bytes, offset * 2 + 6, alpha)
        Next
        Return bytes
    End Function

    Private Shared Sub WriteHalf(bytes As Byte(), offset As Integer, value As Single)
        Dim halfValue = CType(Math.Clamp(value, -65504.0F, 65504.0F), Half)
        Dim bits = BitConverter.HalfToUInt16Bits(halfValue)
        bytes(offset) = CByte(bits And 255US)
        bytes(offset + 1) = CByte(bits >> 8)
    End Sub
End Class
