Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Numerics
Imports System.Runtime.InteropServices
Imports System.Runtime.CompilerServices
Imports Vortice.Direct2D1
Imports Vortice.Mathematics

Public Enum D3D_BackdropMode
    None = 0
    Image = 1
    Auto = 2
    CaptionOnly = 3
End Enum

''' <summary>
''' Window-level GPU backdrop renderer used by V3 controls. Image mode runs fully through D2D:
''' image texture -> optional Gaussian blur -> tint -> optional noise.
''' Auto and CaptionOnly remain reserved for a future Desktop Duplication path.
''' </summary>
Public NotInheritable Class D3D_BackdropRenderer
    Implements D3D_IRenderCacheOwner, IDisposable

    Private ReadOnly _imageCache As D3D_ImageCache
    Private ReadOnly _deviceManager As D3D_DeviceManager
    Private _image As Image
    Private _disposed As Boolean

    Private _offscreenContext As ID2D1DeviceContext
    Private _offscreenGeneration As Integer = -1
    Private _outputTarget As ID2D1Bitmap1
    Private _targetSize As Size = Size.Empty
    Private _blurEffect As ID2D1Effect
    Private _blurCacheKey As D3D_BlurCacheKey
    Private _hasBlurCacheKey As Boolean
    Private _lastBlurUseTick As Long
    Private _frameUseDepth As Integer
    Private _trimPending As Boolean

    Private _noiseBitmap As Bitmap
    Private _noiseD2DBitmap As ID2D1Bitmap
    Private _noiseD2DBrush As ID2D1BitmapBrush
    Private _noiseOwnerContext As WeakReference
    Private _noiseGeneration As Integer = -1

    Private _averageImage As Image
    Private _averageImageWidth As Integer
    Private _averageImageHeight As Integer
    Private _averageColor As System.Drawing.Color
    Private _hasAverage As Boolean

    Public Sub New(imageCache As D3D_ImageCache, deviceManager As D3D_DeviceManager)
        If imageCache Is Nothing Then Throw New ArgumentNullException(NameOf(imageCache))
        If deviceManager Is Nothing Then Throw New ArgumentNullException(NameOf(deviceManager))
        _imageCache = imageCache
        _deviceManager = deviceManager
        D3D_GpuCache.Register(Me)
    End Sub

    Private ReadOnly Property CacheBytes As Long Implements D3D_IRenderCacheOwner.CacheBytes
        Get
            Dim total As Long
            If _outputTarget IsNot Nothing Then total += CLng(Math.Max(1, _targetSize.Width)) * CLng(Math.Max(1, _targetSize.Height)) * 4L
            If _noiseD2DBitmap IsNot Nothing AndAlso _noiseBitmap IsNot Nothing Then total += CLng(_noiseBitmap.Width) * CLng(_noiseBitmap.Height) * 4L
            Return total
        End Get
    End Property

    Private ReadOnly Property OldestUseTick As Long Implements D3D_IRenderCacheOwner.OldestUseTick
        Get
            If _frameUseDepth > 0 Then Return Long.MaxValue
            If CacheBytes <= 0 Then Return Long.MaxValue
            Return _lastBlurUseTick
        End Get
    End Property

    Private Function TrimOldest() As Boolean Implements D3D_IRenderCacheOwner.TrimOldest
        If _frameUseDepth > 0 Then
            _trimPending = True
            Return False
        End If
        If CacheBytes <= 0 Then Return False
        DisposeBlurTargets()
        DisposeNoiseD2DResources()
        Return True
    End Function

    Private Sub ReleaseAll() Implements D3D_IRenderCacheOwner.ReleaseAll
        DisposeGpuResources()
    End Sub

    Friend Sub BeginFrameUse()
        If _disposed Then Return
        _frameUseDepth += 1
    End Sub

    Friend Sub EndFrameUse()
        If _frameUseDepth > 0 Then _frameUseDepth -= 1
        If _frameUseDepth > 0 OrElse Not _trimPending Then Return
        _trimPending = False
        D3D_GpuCache.TrimToBudget()
    End Sub

    Public Property Mode As D3D_BackdropMode = D3D_BackdropMode.None
    Public Property TintColor As System.Drawing.Color = System.Drawing.Color.FromArgb(80, System.Drawing.Color.White)
    Public Property BlurRadius As Integer = 24
    Public Property BlurPasses As Integer = 3
    Public Property DownsampleFactor As Integer = 4
    Public Property NoiseOpacity As Byte = 0
    Public Property NoiseScale As Single = 1.0F

    Public Sub SetImage(image As Image)
        If _image Is image Then
            Mode = If(image Is Nothing, D3D_BackdropMode.None, D3D_BackdropMode.Image)
            Return
        End If

        _image = image
        Mode = If(image Is Nothing, D3D_BackdropMode.None, D3D_BackdropMode.Image)
        InvalidateBlurCache()
        InvalidateAverage()
    End Sub

    Public Sub ApplyParameters(radius As Integer, passes As Integer, downsample As Integer, noiseScale As Single)
        Dim nextRadius = Math.Max(1, Math.Min(96, radius))
        Dim nextPasses = Math.Max(0, Math.Min(5, passes))
        Dim nextDownsample = Math.Max(1, downsample)
        Dim nextNoiseScale = Math.Max(0.1F, noiseScale)
        Dim blurChanged = BlurRadius <> nextRadius OrElse BlurPasses <> nextPasses OrElse DownsampleFactor <> nextDownsample
        BlurRadius = nextRadius
        BlurPasses = nextPasses
        DownsampleFactor = nextDownsample
        NoiseScale = nextNoiseScale
        If BlurPasses <= 0 Then DisposeBlurEffect()
        If blurChanged Then InvalidateBlurCache()
    End Sub

    Public Sub DrawImageBackdrop(context As D3D_PaintContext, bounds As RectangleF)
        If context Is Nothing OrElse Mode <> D3D_BackdropMode.Image OrElse _image Is Nothing Then Return
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        context.BeginBackdropUse()

        Try
            Dim image = _image
            If image Is Nothing Then Return

            Dim drewBackdrop As Boolean
            If BlurPasses > 0 AndAlso BlurRadius > 0 Then
                drewBackdrop = DrawBlurredImageBackdrop(context, image, bounds)
            End If

            If Not drewBackdrop Then
                DrawCoverImage(context, image, bounds)
            End If

            If TintColor.A > 0 Then context.FillRectangle(bounds, TintColor)
            If NoiseOpacity > 0 Then DrawNoise(context, bounds, NoiseOpacity)
        Catch ex As Exception
            If _deviceManager.HandleDeviceLost(ex) Then
                DisposeGpuResources()
                context.Compositor.HandleDeviceLost()
                Return
            End If
            Throw
        End Try
    End Sub

    Public Function TryReadAverageColor(context As D3D_PaintContext, ByRef color As System.Drawing.Color) As Boolean
        color = System.Drawing.Color.Empty
        If _disposed OrElse Mode <> D3D_BackdropMode.Image OrElse _image Is Nothing Then Return False

        Dim image = _image
        Dim w As Integer
        Dim h As Integer
        Try
            w = image.Width
            h = image.Height
        Catch
            Return False
        End Try
        If w <= 0 OrElse h <= 0 Then Return False

        If _hasAverage AndAlso _averageImage Is image AndAlso _averageImageWidth = w AndAlso _averageImageHeight = h Then
            color = _averageColor
            Return True
        End If

        If Not TryComputeImageAverage(image, color) Then Return False
        _averageImage = image
        _averageImageWidth = w
        _averageImageHeight = h
        _averageColor = color
        _hasAverage = True
        Return True
    End Function

    Public Sub Invalidate()
        DisposeGpuResources()
        InvalidateAverage()
    End Sub

    Private Sub DrawCoverImage(context As D3D_PaintContext, image As Image, bounds As RectangleF)
        Dim bitmap = _imageCache.GetBitmap(context, image)
        If bitmap Is Nothing Then Return

        Dim sourceRect = ComputeCoverSourceRect(image, bounds)
        Dim dst As Vortice.RawRectF? = D3D_PaintContext.ToRawRect(bounds)
        Dim src As Vortice.RawRectF? = D3D_PaintContext.ToRawRect(sourceRect)
        context.DeviceContext.DrawBitmap(bitmap, dst, 1.0F, Vortice.Direct2D1.InterpolationMode.Linear, src, Nothing)
    End Sub

    Private Function DrawBlurredImageBackdrop(context As D3D_PaintContext,
                                             image As Image,
                                             bounds As RectangleF) As Boolean
        Dim bitmap = _imageCache.GetBitmap(context, image)
        If bitmap Is Nothing Then Return False

        Dim downsample = Math.Max(1, DownsampleFactor)
        Dim targetWidth = Math.Max(1, CInt(Math.Ceiling(bounds.Width / CSng(downsample))))
        Dim targetHeight = Math.Max(1, CInt(Math.Ceiling(bounds.Height / CSng(downsample))))
        Dim size As New Size(targetWidth, targetHeight)

        EnsureOffscreenContext(context.DeviceGeneration)
        EnsureBlurTargets(size)
        If _offscreenContext Is Nothing OrElse _outputTarget Is Nothing Then Return False

        Dim sourceRect = ComputeCoverSourceRect(image, bounds)
        Dim cacheKey = BuildBlurCacheKey(image, bounds, size, context.DeviceGeneration)
        If _hasBlurCacheKey AndAlso _blurCacheKey.Equals(cacheKey) Then
            D3D_RenderDiagnostics.BackdropCacheHit()
        Else
            Using sourceTarget = CreateBlurTarget(size)
                RenderSourceToOffscreen(sourceTarget, bitmap, sourceRect, size)
                RenderBlurToOffscreen(sourceTarget, size)
            End Using
            _blurCacheKey = cacheKey
            _hasBlurCacheKey = True
            D3D_RenderDiagnostics.BackdropRebuild()
        End If
        _lastBlurUseTick = D3D_GpuCache.NextTick()

        Dim dst As Vortice.RawRectF? = D3D_PaintContext.ToRawRect(bounds)
        Dim src As Vortice.RawRectF? = New Vortice.RawRectF(0, 0, size.Width, size.Height)
        context.DeviceContext.DrawBitmap(_outputTarget, dst, 1.0F, Vortice.Direct2D1.InterpolationMode.Linear, src, Nothing)
        Return True
    End Function

    Private Function BuildBlurCacheKey(image As Image, bounds As RectangleF, size As Size, deviceGeneration As Integer) As D3D_BlurCacheKey
        Return New D3D_BlurCacheKey(image, image.Width, image.Height, D3D_HdrOutput.ImageRevision,
                                    BlurRadius, BlurPasses, DownsampleFactor, size,
                                    BitConverter.SingleToInt32Bits(bounds.Width), BitConverter.SingleToInt32Bits(bounds.Height),
                                    deviceGeneration)
    End Function

    Private Sub EnsureOffscreenContext(deviceGeneration As Integer)
        If _offscreenContext IsNot Nothing AndAlso _offscreenGeneration = deviceGeneration Then Return

        DisposeOffscreenResources()
        _offscreenContext = _deviceManager.CreateDeviceContext()
        _offscreenGeneration = deviceGeneration
    End Sub

    Private Sub EnsureBlurTargets(size As Size)
        If _outputTarget IsNot Nothing AndAlso _targetSize = size Then Return

        DisposeBlurTargets()
        _targetSize = Size.Empty

        Dim props As New BitmapProperties1(
            New Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
            96.0F,
            96.0F,
            BitmapOptions.Target)

        _outputTarget = _offscreenContext.CreateBitmap(New SizeI(size.Width, size.Height), IntPtr.Zero, 0UI, props)
        _targetSize = size
    End Sub

    Private Function CreateBlurTarget(size As Size) As ID2D1Bitmap1
        Dim props As New BitmapProperties1(
            New Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
            96.0F, 96.0F, BitmapOptions.Target)
        Return _offscreenContext.CreateBitmap(New SizeI(size.Width, size.Height), IntPtr.Zero, 0UI, props)
    End Function

    Private Sub RenderSourceToOffscreen(sourceTarget As ID2D1Bitmap1, sourceBitmap As ID2D1Bitmap1, sourceRect As RectangleF, size As Size)
        RenderOffscreenTarget(sourceTarget,
            Sub()
                _offscreenContext.Clear(New Color4(0, 0, 0, 0))
                Dim dst As Vortice.RawRectF? = New Vortice.RawRectF(0, 0, size.Width, size.Height)
                Dim src As Vortice.RawRectF? = D3D_PaintContext.ToRawRect(sourceRect)
                _offscreenContext.DrawBitmap(sourceBitmap, dst, 1.0F, Vortice.Direct2D1.InterpolationMode.Linear, src, Nothing)
            End Sub)
    End Sub

    Private Sub RenderBlurToOffscreen(sourceTarget As ID2D1Bitmap1, size As Size)
        RenderOffscreenTarget(_outputTarget,
            Sub()
                _offscreenContext.Clear(New Color4(0, 0, 0, 0))
                Dim output = GetBlurOutput(sourceTarget)
                If output Is Nothing Then Return
                Try
                    _offscreenContext.DrawImage(output,
                                                New Nullable(Of Vector2)(),
                                                New Nullable(Of Vortice.RawRectF)(),
                                                Vortice.Direct2D1.InterpolationMode.Linear,
                                                CompositeMode.SourceOver)
                Finally
                    Try : output.Dispose() : Catch : End Try
                End Try
            End Sub)
    End Sub

    Private Sub RenderOffscreenTarget(target As ID2D1Bitmap1, drawAction As Action)
        Dim previousTarget As ID2D1Image = Nothing
        Dim drawing As Boolean

        Try
            previousTarget = _offscreenContext.Target
            _offscreenContext.Target = target
            _offscreenContext.Transform = Matrix3x2.Identity
            _offscreenContext.AntialiasMode = AntialiasMode.PerPrimitive
            _offscreenContext.BeginDraw()
            drawing = True
            drawAction()
            _offscreenContext.EndDraw()
            drawing = False
        Finally
            If drawing Then
                Try : _offscreenContext.EndDraw() : Catch : End Try
            End If
            Try : _offscreenContext.Target = previousTarget : Catch : End Try
            If previousTarget IsNot Nothing Then
                Try : previousTarget.Dispose() : Catch : End Try
            End If
        End Try
    End Sub

    Private Function GetBlurOutput(sourceTarget As ID2D1Bitmap1) As ID2D1Image
        If sourceTarget Is Nothing Then Return Nothing

        If _blurEffect Is Nothing Then
            _blurEffect = _offscreenContext.CreateEffect(EffectGuids.GaussianBlur)
        End If

        _blurEffect.SetInput(0UI, sourceTarget, True)

        Dim down = Math.Max(1, DownsampleFactor)
        Dim radius = Math.Max(0.1F, BlurRadius / CSng(down))
        Dim sigma = CSng(Math.Sqrt(Math.Max(1, BlurPasses)) * radius / Math.Sqrt(3.0R))
        SetEffectFloat(_blurEffect, "StandardDeviation", sigma)
        SetEffectEnum(_blurEffect, "Optimization", CInt(GaussianBlurOptimization.Balanced))
        SetEffectEnum(_blurEffect, "BorderMode", CInt(BorderMode.Hard))
        Return _blurEffect.Output
    End Function

    Private Sub DrawNoise(context As D3D_PaintContext, bounds As RectangleF, opacity As Byte)
        If context Is Nothing OrElse opacity = 0 OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return

        Dim brush = AcquireNoiseBrush(context)
        If brush Is Nothing Then Return

        brush.Opacity = opacity / 255.0F
        Dim tile = Math.Max(1.0F, _noiseBitmap.Width * NoiseScale)
        brush.Transform =
            Matrix3x2.CreateScale(tile / CSng(_noiseBitmap.Width)) *
            Matrix3x2.CreateTranslation(bounds.X, bounds.Y)
        context.DeviceContext.FillRectangle(D3D_PaintContext.ToRawRect(bounds), brush)
    End Sub

    Private Function AcquireNoiseBrush(context As D3D_PaintContext) As ID2D1BitmapBrush
        EnsureNoiseBitmap()
        If _noiseBitmap Is Nothing Then Return Nothing

        Dim sameContext = _noiseOwnerContext IsNot Nothing AndAlso ReferenceEquals(_noiseOwnerContext.Target, context.DeviceContext)
        If Not sameContext OrElse _noiseGeneration <> context.DeviceGeneration Then
            DisposeNoiseD2DResources()
            _noiseOwnerContext = New WeakReference(context.DeviceContext)
            _noiseGeneration = context.DeviceGeneration
        End If

        If _noiseD2DBitmap Is Nothing Then
            _noiseD2DBitmap = D3D_D2DInterop.CreateBitmapFromGdi(context.DeviceContext, _noiseBitmap)
            If _noiseD2DBitmap Is Nothing Then Return Nothing
        End If

        If _noiseD2DBrush Is Nothing Then
            Dim props As New BitmapBrushProperties() With {
                .ExtendModeX = ExtendMode.Wrap,
                .ExtendModeY = ExtendMode.Wrap,
                .InterpolationMode = BitmapInterpolationMode.Linear
            }
            _noiseD2DBrush = context.DeviceContext.CreateBitmapBrush(_noiseD2DBitmap, props)
        End If

        Return _noiseD2DBrush
    End Function

    Private Sub EnsureNoiseBitmap()
        If _noiseBitmap IsNot Nothing Then Return

        Const n As Integer = 128
        Dim bmp As New Bitmap(n, n, PixelFormat.Format32bppPArgb)
        Dim rng As New Random(1059)
        Dim data = bmp.LockBits(New Rectangle(0, 0, n, n), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb)
        Try
            Dim stride = data.Stride
            Dim buffer(stride * n - 1) As Byte
            For y = 0 To n - 1
                Dim row = y * stride
                For x = 0 To n - 1
                    Dim v = CByte(rng.Next(0, 256))
                    Dim offset = row + x * 4
                    buffer(offset) = v
                    buffer(offset + 1) = v
                    buffer(offset + 2) = v
                    buffer(offset + 3) = 255
                Next
            Next
            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length)
        Finally
            bmp.UnlockBits(data)
        End Try
        _noiseBitmap = bmp
    End Sub

    Private Shared Function ComputeCoverSourceRect(image As Image, destination As RectangleF) As RectangleF
        Dim sourceWidth = Math.Max(1, image.Width)
        Dim sourceHeight = Math.Max(1, image.Height)
        If destination.Width <= 0 OrElse destination.Height <= 0 Then
            Return New RectangleF(0, 0, sourceWidth, sourceHeight)
        End If

        Dim destAspect = destination.Width / destination.Height
        Dim sourceAspect = sourceWidth / CSng(sourceHeight)

        If sourceAspect > destAspect Then
            Dim cropWidth = sourceHeight * destAspect
            Return New RectangleF((sourceWidth - cropWidth) / 2.0F, 0, cropWidth, sourceHeight)
        End If

        Dim cropHeight = sourceWidth / destAspect
        Return New RectangleF(0, (sourceHeight - cropHeight) / 2.0F, sourceWidth, cropHeight)
    End Function

    Private Function TryComputeImageAverage(image As Image, ByRef color As System.Drawing.Color) As Boolean
        color = System.Drawing.Color.Empty
        Try
            Dim sourceWidth = image.Width
            Dim sourceHeight = image.Height
            If sourceWidth <= 0 OrElse sourceHeight <= 0 Then Return False

            Const maxSample As Integer = 64
            Dim sampleWidth = Math.Max(1, Math.Min(maxSample, sourceWidth))
            Dim sampleHeight = Math.Max(1, Math.Min(maxSample, sourceHeight))

            Using sample As New Bitmap(sampleWidth, sampleHeight, PixelFormat.Format32bppPArgb)
                sample.SetResolution(96.0F, 96.0F)
                Using g = Graphics.FromImage(sample)
                    g.CompositingMode = CompositingMode.SourceCopy
                    g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBilinear
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality
                    SyncLock image
                        g.DrawImage(image,
                                    New Rectangle(0, 0, sampleWidth, sampleHeight),
                                    New RectangleF(0, 0, sourceWidth, sourceHeight),
                                    GraphicsUnit.Pixel)
                    End SyncLock
                End Using
                color = ComputeAverageColor(sample)
            End Using

            Return Not color.IsEmpty
        Catch
            Return False
        End Try
    End Function

    Private Shared Function ComputeAverageColor(bitmap As Bitmap) As System.Drawing.Color
        Dim rect As New Rectangle(0, 0, bitmap.Width, bitmap.Height)
        Dim data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb)
        Try
            Dim stride = data.Stride
            Dim length = Math.Abs(stride) * bitmap.Height
            Dim buffer(length - 1) As Byte
            Marshal.Copy(data.Scan0, buffer, 0, length)

            Dim total = bitmap.Width * bitmap.Height
            Dim stepSize = If(total >= 4096, 4, 1)
            Dim sumB As Long
            Dim sumG As Long
            Dim sumR As Long
            Dim sumA As Long
            Dim count As Long

            For y = 0 To bitmap.Height - 1 Step stepSize
                Dim row = y * stride
                For x = 0 To bitmap.Width - 1 Step stepSize
                    Dim offset = row + x * 4
                    sumB += buffer(offset)
                    sumG += buffer(offset + 1)
                    sumR += buffer(offset + 2)
                    sumA += buffer(offset + 3)
                    count += 1
                Next
            Next

            If count <= 0 Then Return System.Drawing.Color.Empty

            Dim a = CInt(sumA \ count)
            Dim r = CInt(sumR \ count)
            Dim g = CInt(sumG \ count)
            Dim b = CInt(sumB \ count)

            If a >= 16 AndAlso a < 255 Then
                r = Math.Min(255, CInt(r * 255 \ a))
                g = Math.Min(255, CInt(g * 255 \ a))
                b = Math.Min(255, CInt(b * 255 \ a))
            End If

            Return System.Drawing.Color.FromArgb(255, r, g, b)
        Finally
            bitmap.UnlockBits(data)
        End Try
    End Function

    Private Shared Sub SetEffectFloat(effect As ID2D1Effect, name As String, value As Single)
        Dim bytes = BitConverter.GetBytes(value)
        effect.SetValueByName(name, PropertyType.Float, bytes, CUInt(bytes.Length))
    End Sub

    Private Shared Sub SetEffectEnum(effect As ID2D1Effect, name As String, value As Integer)
        Dim bytes = BitConverter.GetBytes(value)
        effect.SetValueByName(name, PropertyType.[Enum], bytes, CUInt(bytes.Length))
    End Sub

    Private Sub InvalidateAverage()
        _averageImage = Nothing
        _averageImageWidth = 0
        _averageImageHeight = 0
        _averageColor = System.Drawing.Color.Empty
        _hasAverage = False
    End Sub

    Private Sub DisposeGpuResources()
        DisposeOffscreenResources()
        DisposeNoiseD2DResources()
    End Sub

    Private Sub DisposeOffscreenResources()
        DisposeBlurTargets()
        If _offscreenContext IsNot Nothing Then
            Try : _offscreenContext.Target = Nothing : Catch : End Try
            Try : _offscreenContext.Dispose() : Catch : End Try
            _offscreenContext = Nothing
        End If
        _offscreenGeneration = -1
    End Sub

    Private Sub DisposeBlurTargets()
        DisposeBlurEffect()
        If _outputTarget IsNot Nothing Then
            Try : _outputTarget.Dispose() : Catch : End Try
            _outputTarget = Nothing
        End If
        _targetSize = Size.Empty
        InvalidateBlurCache()
    End Sub

    Private Sub InvalidateBlurCache()
        _hasBlurCacheKey = False
        _lastBlurUseTick = 0
    End Sub

    Private Structure D3D_BlurCacheKey
        Implements IEquatable(Of D3D_BlurCacheKey)

        Private ReadOnly _image As Image
        Private ReadOnly _imageWidth As Integer
        Private ReadOnly _imageHeight As Integer
        Private ReadOnly _imageRevision As Integer
        Private ReadOnly _radius As Integer
        Private ReadOnly _passes As Integer
        Private ReadOnly _downsample As Integer
        Private ReadOnly _size As Size
        Private ReadOnly _boundsWidthBits As Integer
        Private ReadOnly _boundsHeightBits As Integer
        Private ReadOnly _generation As Integer

        Friend Sub New(image As Image, imageWidth As Integer, imageHeight As Integer, imageRevision As Integer,
                       radius As Integer, passes As Integer, downsample As Integer, size As Size,
                       boundsWidthBits As Integer, boundsHeightBits As Integer, generation As Integer)
            _image = image
            _imageWidth = imageWidth
            _imageHeight = imageHeight
            _imageRevision = imageRevision
            _radius = radius
            _passes = passes
            _downsample = downsample
            _size = size
            _boundsWidthBits = boundsWidthBits
            _boundsHeightBits = boundsHeightBits
            _generation = generation
        End Sub

        Public Overloads Function Equals(other As D3D_BlurCacheKey) As Boolean Implements IEquatable(Of D3D_BlurCacheKey).Equals
            Return ReferenceEquals(_image, other._image) AndAlso _imageWidth = other._imageWidth AndAlso
                   _imageHeight = other._imageHeight AndAlso _imageRevision = other._imageRevision AndAlso
                   _radius = other._radius AndAlso _passes = other._passes AndAlso _downsample = other._downsample AndAlso
                   _size = other._size AndAlso _boundsWidthBits = other._boundsWidthBits AndAlso
                   _boundsHeightBits = other._boundsHeightBits AndAlso _generation = other._generation
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            Return TypeOf obj Is D3D_BlurCacheKey AndAlso Equals(DirectCast(obj, D3D_BlurCacheKey))
        End Function

        Public Overrides Function GetHashCode() As Integer
            Dim hash = HashCode.Combine(RuntimeHelpers.GetHashCode(_image), _imageWidth, _imageHeight, _imageRevision, _radius, _passes)
            Return HashCode.Combine(hash, _downsample, _size, _boundsWidthBits, _boundsHeightBits, _generation)
        End Function
    End Structure

    Private Sub DisposeBlurEffect()
        If _blurEffect IsNot Nothing Then
            Try : _blurEffect.Dispose() : Catch : End Try
            _blurEffect = Nothing
        End If
    End Sub

    Private Sub DisposeNoiseD2DResources()
        If _noiseD2DBrush IsNot Nothing Then
            Try : _noiseD2DBrush.Dispose() : Catch : End Try
            _noiseD2DBrush = Nothing
        End If
        If _noiseD2DBitmap IsNot Nothing Then
            Try : _noiseD2DBitmap.Dispose() : Catch : End Try
            _noiseD2DBitmap = Nothing
        End If
        _noiseOwnerContext = Nothing
        _noiseGeneration = -1
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        _image = Nothing
        InvalidateAverage()
        DisposeGpuResources()
        If _noiseBitmap IsNot Nothing Then
            Try : _noiseBitmap.Dispose() : Catch : End Try
            _noiseBitmap = Nothing
        End If
        GC.SuppressFinalize(Me)
    End Sub
End Class
