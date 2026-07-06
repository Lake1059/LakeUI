Imports System.Buffers
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices
Imports Vortice.Mathematics

Friend Module D3D_HdrOutput
    Private Const CurveTableSize As Integer = 4096
    Private Const CurveTableMax As Integer = CurveTableSize - 1

    Private ReadOnly _cacheLock As New Object()
    Private _curveCache As HdrCurveCache

    Friend ReadOnly Property VectorColorRevision As Integer
        Get
            If Not ShouldMapVectorColors Then Return 0
            Return GlobalOptions.HDR.Revision
        End Get
    End Property

    Friend ReadOnly Property ImageRevision As Integer
        Get
            If Not ShouldMapImages Then Return 0
            Return GlobalOptions.HDR.Revision
        End Get
    End Property

    Friend ReadOnly Property ShouldMapVectorColors As Boolean
        Get
            Return GlobalOptions.HDR.Enabled AndAlso GlobalOptions.HDR.MapVectorColors
        End Get
    End Property

    Friend ReadOnly Property ShouldMapImages As Boolean
        Get
            Return GlobalOptions.HDR.Enabled AndAlso GlobalOptions.HDR.MapImages
        End Get
    End Property

    Friend Function MapColor4(color As System.Drawing.Color) As Color4
        If Not ShouldMapVectorColors Then Return ToRawColor4(color)
        If color.A = 0 Then Return ToRawColor4(color)

        Dim cache = GetCurveCache()
        Dim cached As Color4
        If cache.TryGetMappedColor4(color.ToArgb(), cached) Then Return cached

        Dim r = cache.SrgbToLinear(color.R)
        Dim g = cache.SrgbToLinear(color.G)
        Dim b = cache.SrgbToLinear(color.B)
        ApplyHdrCurveFast(r, g, b, cache)

        Dim mapped = New Color4(cache.LinearToSrgb(Quantize01(r)),
                                cache.LinearToSrgb(Quantize01(g)),
                                cache.LinearToSrgb(Quantize01(b)),
                                color.A / 255.0F)
        cache.RememberMappedColor4(color.ToArgb(), mapped)
        Return mapped
    End Function

    Friend Function ToRawColor4(color As System.Drawing.Color) As Color4
        Return New Color4(color.R / 255.0F, color.G / 255.0F, color.B / 255.0F, color.A / 255.0F)
    End Function

    Friend Function MapColor(color As System.Drawing.Color) As System.Drawing.Color
        If color.A = 0 OrElse Not ShouldMapVectorColors Then Return color

        Dim cache = GetCurveCache()
        Dim r = cache.SrgbToLinear(color.R)
        Dim g = cache.SrgbToLinear(color.G)
        Dim b = cache.SrgbToLinear(color.B)
        ApplyHdrCurveFast(r, g, b, cache)
        Return System.Drawing.Color.FromArgb(color.A,
                                             cache.LinearToSrgbByte(Quantize01(r)),
                                             cache.LinearToSrgbByte(Quantize01(g)),
                                             cache.LinearToSrgbByte(Quantize01(b)))
    End Function

    Friend Sub MapBitmapForImageUpload(bitmap As Bitmap)
        If bitmap Is Nothing OrElse Not ShouldMapImages Then Return
        If bitmap.Width <= 0 OrElse bitmap.Height <= 0 Then Return

        Dim cache = GetCurveCache()
        Dim rect As New Rectangle(0, 0, bitmap.Width, bitmap.Height)
        Dim data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb)
        Dim buffer() As Byte = Nothing
        Try
            Dim stride = Math.Abs(data.Stride)
            Dim bytes = stride * bitmap.Height
            buffer = ArrayPool(Of Byte).Shared.Rent(bytes)
            Marshal.Copy(data.Scan0, buffer, 0, bytes)

            For y As Integer = 0 To bitmap.Height - 1
                Dim row = y * stride
                For x As Integer = 0 To bitmap.Width - 1
                    Dim idx = row + x * 4
                    Dim a As Integer = buffer(idx + 3)
                    If a = 0 Then Continue For

                    Dim r As Single
                    Dim g As Single
                    Dim b As Single
                    If a >= 255 Then
                        r = cache.SrgbToLinear(buffer(idx + 2))
                        g = cache.SrgbToLinear(buffer(idx + 1))
                        b = cache.SrgbToLinear(buffer(idx))
                    Else
                        Dim alphaOffset = a << 8
                        r = cache.UnpremultipliedLinear(alphaOffset Or buffer(idx + 2))
                        g = cache.UnpremultipliedLinear(alphaOffset Or buffer(idx + 1))
                        b = cache.UnpremultipliedLinear(alphaOffset Or buffer(idx))
                    End If

                    ApplyHdrCurveFast(r, g, b, cache)

                    Dim ri = Quantize01(r)
                    Dim gi = Quantize01(g)
                    Dim bi = Quantize01(b)
                    If a >= 255 Then
                        buffer(idx + 2) = cache.LinearToSrgbByte(ri)
                        buffer(idx + 1) = cache.LinearToSrgbByte(gi)
                        buffer(idx) = cache.LinearToSrgbByte(bi)
                    Else
                        Dim premulOffset = a * CurveTableSize
                        buffer(idx + 2) = cache.PremultipliedByte(premulOffset + ri)
                        buffer(idx + 1) = cache.PremultipliedByte(premulOffset + gi)
                        buffer(idx) = cache.PremultipliedByte(premulOffset + bi)
                    End If
                Next
            Next

            Marshal.Copy(buffer, 0, data.Scan0, bytes)
        Finally
            If buffer IsNot Nothing Then ArrayPool(Of Byte).Shared.Return(buffer)
            bitmap.UnlockBits(data)
        End Try
    End Sub

    Private Function GetCurveCache() As HdrCurveCache
        Dim revision = GlobalOptions.HDR.Revision
        Dim cache = _curveCache
        If cache IsNot Nothing AndAlso cache.Revision = revision Then Return cache

        SyncLock _cacheLock
            cache = _curveCache
            If cache Is Nothing OrElse cache.Revision <> revision Then
                Dim preset = GlobalOptions.HDR.CurvePreset
                cache = New HdrCurveCache(revision, preset.Exposure, preset.Saturation)
                _curveCache = cache
            End If
            Return cache
        End SyncLock
    End Function

    Private Sub ApplyHdrCurveFast(ByRef r As Single, ByRef g As Single, ByRef b As Single, cache As HdrCurveCache)
        If cache.HasSaturation Then
            Dim saturation = cache.Saturation
            Dim luma = 0.2126F * r + 0.7152F * g + 0.0722F * b
            r = Clamp01(luma + (r - luma) * saturation)
            g = Clamp01(luma + (g - luma) * saturation)
            b = Clamp01(luma + (b - luma) * saturation)
        End If

        If Not cache.HasExposure Then
            r = Clamp01(r)
            g = Clamp01(g)
            b = Clamp01(b)
            Return
        End If

        r = cache.ToneMapLinear(Quantize01(r))
        g = cache.ToneMapLinear(Quantize01(g))
        b = cache.ToneMapLinear(Quantize01(b))
    End Sub

    Private Function Quantize01(value As Single) As Integer
        If value <= 0.0F Then Return 0
        If value >= 1.0F Then Return CurveTableMax
        Return CInt(value * CurveTableMax + 0.5F)
    End Function

    Private Function ComputeToneMap(value As Single, exposure As Single, denom As Single) As Single
        If value <= 0.0F Then Return 0.0F
        If denom <= 0.0001F Then Return Clamp01(value)
        Return Clamp01((1.0F - CSng(Math.Exp(-Clamp01(value) * exposure))) / denom)
    End Function

    Private Function ComputeSrgbToLinear(value As Single) As Single
        value = Clamp01(value)
        If value <= 0.04045F Then Return value / 12.92F
        Return CSng(Math.Pow((value + 0.055F) / 1.055F, 2.4F))
    End Function

    Private Function ComputeLinearToSrgb(value As Single) As Single
        value = Clamp01(value)
        If value <= 0.0031308F Then Return value * 12.92F
        Return 1.055F * CSng(Math.Pow(value, 1.0 / 2.4)) - 0.055F
    End Function

    Private Function Clamp01(value As Single) As Single
        If value <= 0.0F Then Return 0.0F
        If value >= 1.0F Then Return 1.0F
        Return value
    End Function

    Private NotInheritable Class HdrCurveCache
        Friend ReadOnly Revision As Integer
        Friend ReadOnly Saturation As Single
        Friend ReadOnly HasSaturation As Boolean
        Friend ReadOnly HasExposure As Boolean
        Friend ReadOnly SrgbToLinear As Single()
        Friend ReadOnly UnpremultipliedLinear As Single()
        Friend ReadOnly ToneMapLinear As Single()
        Friend ReadOnly LinearToSrgb As Single()
        Friend ReadOnly LinearToSrgbByte As Byte()
        Friend ReadOnly PremultipliedByte As Byte()
        Private ReadOnly _color4Lock As New Object()
        Private ReadOnly _color4Cache As New Dictionary(Of Integer, Color4)()

        Friend Sub New(revision As Integer, exposure As Single, saturation As Single)
            Me.Revision = revision
            Me.Saturation = saturation
            HasSaturation = Math.Abs(saturation - 1.0F) > 0.0001F
            HasExposure = exposure > 1.0001F

            SrgbToLinear = New Single(255) {}
            UnpremultipliedLinear = New Single((256 * 256) - 1) {}
            ToneMapLinear = New Single(CurveTableMax) {}
            LinearToSrgb = New Single(CurveTableMax) {}
            LinearToSrgbByte = New Byte(CurveTableMax) {}
            PremultipliedByte = New Byte((256 * CurveTableSize) - 1) {}

            For i As Integer = 0 To 255
                SrgbToLinear(i) = ComputeSrgbToLinear(i / 255.0F)
            Next

            For alpha As Integer = 1 To 255
                Dim alphaOffset = alpha << 8
                For channel As Integer = 0 To 255
                    Dim srgb = Math.Min(1.0F, channel / CSng(alpha))
                    UnpremultipliedLinear(alphaOffset Or channel) = ComputeSrgbToLinear(srgb)
                Next
            Next

            Dim denom = 1.0F - CSng(Math.Exp(-exposure))
            For i As Integer = 0 To CurveTableMax
                Dim value = i / CSng(CurveTableMax)
                ToneMapLinear(i) = If(HasExposure, ComputeToneMap(value, exposure, denom), value)
                Dim srgb = ComputeLinearToSrgb(value)
                LinearToSrgb(i) = srgb
                LinearToSrgbByte(i) = CByte(Math.Max(0, Math.Min(255, CInt(Math.Round(srgb * 255.0F)))))
            Next

            For alpha As Integer = 0 To 255
                Dim premulOffset = alpha * CurveTableSize
                For i As Integer = 0 To CurveTableMax
                    PremultipliedByte(premulOffset + i) =
                        CByte(Math.Max(0, Math.Min(alpha, CInt(Math.Round(LinearToSrgb(i) * alpha)))))
                Next
            Next
        End Sub

        Friend Function TryGetMappedColor4(argb As Integer, ByRef color As Color4) As Boolean
            SyncLock _color4Lock
                Return _color4Cache.TryGetValue(argb, color)
            End SyncLock
        End Function

        Friend Sub RememberMappedColor4(argb As Integer, color As Color4)
            SyncLock _color4Lock
                If _color4Cache.Count >= 2048 AndAlso Not _color4Cache.ContainsKey(argb) Then _color4Cache.Clear()
                _color4Cache(argb) = color
            End SyncLock
        End Sub
    End Class
End Module
