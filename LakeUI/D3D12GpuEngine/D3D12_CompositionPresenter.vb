Imports System.Numerics
Imports Vortice.Direct3D12
Imports Vortice.DXGI
Imports Vortice.Mathematics

''' <summary>Retains a DX12 control surface for a slot in its host window atlas.</summary>
Friend NotInheritable Class D3D12_CompositionPresenter
    Implements IDisposable

    Private NotInheritable Class BackBufferFrame
        ' Each slot owns its cached glyphs until its last consuming fence completes.
        Public ReadOnly Glyphs As New Dictionary(Of Object, D3D12_ImagePipeline.GlyphBatch)()
        Public ReadOnly GlyphPages As New List(Of D3D12_GlyphPage)()
        Public GlyphBytes As Long
        Public Sub ClearGlyphs()
            For Each batch In Glyphs.Values
                batch.Dispose()
            Next
            For Each page In GlyphPages
                page.Dispose()
            Next
            GlyphPages.Clear()
            Glyphs.Clear()
            GlyphBytes = 0
        End Sub
        Public BackdropKey As Object
        Public Backdrop As ID3D12Resource
        Public Resource As ID3D12Resource
        Public Rtv As CpuDescriptorHandle
        Public FenceValue As Long
        Public ReadOnly Uploads As New List(Of IDisposable)()
        Public ReadOnly UploadArena As New D3D12_UploadArena()
        Public ReadOnly DescriptorArena As New D3D12_DescriptorArena()
    End Class

    Private NotInheritable Class LinearTile
        Public Texture As ID3D12Resource
        Public Bytes As Long
        Public LastUse As Long
    End Class

    Private ReadOnly linearTiles As New Dictionary(Of Object, LinearTile)()
    Private ReadOnly newTiles As New List(Of Object)()
    Private linearTileBytes As Long
    Private tileClock As Long
    Private Const TileCacheBytes As Long = 16L * 1024 * 1024
    Private Const BufferCount As Integer = 2
    Private ReadOnly _owner As Control
    Private ReadOnly _deviceManager As D3D12_DeviceManager
    Private _recordingAllocator As ID3D12CommandAllocator
    Private _rtvHeap As ID3D12DescriptorHeap
    Private _commandList As ID3D12GraphicsCommandList
    Private imagePipeline As D3D12_ImagePipeline
    Private _rectanglePipeline As D3D12_RectanglePipeline
    Private _frames As BackBufferFrame()
    Private contentPublished As Boolean
    Friend Property FrameClip As Drawing.Rectangle
    Private collectingGlyphs As D3D12_ImagePipeline.GlyphBatch
    Private collectingCoverage As List(Of GlyphCoverage)
    Private coverageOrigin As Point
    Private ReadOnly coverageCache As New Dictionary(Of Object, List(Of GlyphCoverage))()
    Private ReadOnly glyphCoverageOrder As New Queue(Of Object)()
    Private glyphCoverageBytes As Long
    Private Const CoverageCacheLimit As Long = 4L * 1024 * 1024

    Private Structure GlyphCoverage
        Public Subpixel As Boolean
        Public Pixels As Byte()
        Public Bounds As RectangleF
        Public Width As Integer
        Public Height As Integer
    End Structure
    Private recordingFrame As BackBufferFrame
    Private recordedRegion As Rectangle
    Private _rtvIncrement As Integer
    Private _handle As IntPtr
    Private _size As Size
    Private _disposed As Boolean
    Friend Property PendingDamage As Rectangle
    Friend Property ParkedNode As LinkedListNode(Of D3D12_CompositionPresenter)
    Friend Property ParkedBytes As Long
    Private parkedContent As ID3D12Resource
    Friend Property ChromeKey As Object
    Private _renderCount As Long
    Private _framePublishedToAtlas As Boolean
    Friend Property Atlas As D3D12_WindowAtlas
    Private slotIndex As Integer
    Friend Property SampleScale As Integer = 1
    Friend Property FloatTarget As Boolean
    Friend Property FloatOutput As Boolean
    Private resolved As ID3D12Resource
    Private resolveHeap As ID3D12DescriptorHeap

    Private Function RequestedScale() As Integer
        Return Math.Clamp(GlobalOptions.GetEffectiveSsaaScale(CInt(If(TryCast(_owner, D3D_ISuperSamplingSource)?.SuperSamplingScale, GlobalOptions.SuperSamplingScaleEnum.OFF))), 1, 4)
    End Function

    Private Function RequestedFloatOutput() As Boolean
        Return If(TryCast(_owner, D3D_IFloatPresentationSource)?.UseFloatPresentation, False)
    End Function

    Private Function RequestedFloat() As Boolean
        Return If(TryCast(_owner, D3D_IFloatRenderingSource)?.UseFloatRendering, False) OrElse If(TryCast(_owner, D3D_IFloatPresentationSource)?.UseFloatPresentation, False)
    End Function

    Friend ReadOnly Property OwnerControl As Control
        Get
            Return _owner
        End Get
    End Property

    Friend ReadOnly Property WindowHandle As IntPtr
        Get
            Return _handle
        End Get
    End Property

    Friend ReadOnly Property BufferSize As Size
        Get
            Return _size
        End Get
    End Property

    Friend ReadOnly Property RenderCount As Long
        Get
            Return _renderCount
        End Get
    End Property

    Friend Sub New(owner As Control, deviceManager As D3D12_DeviceManager)
        _owner = owner
        _deviceManager = deviceManager
        _handle = owner.Handle
        _size = New Size(owner.ClientSize.Width, owner.ClientSize.Height)
        SampleScale = RequestedScale()
        FloatTarget = RequestedFloat()
        FloatOutput = RequestedFloatOutput()
    End Sub

    Friend Function MatchesOwner() As Boolean
        Return _size = New Size(_owner.ClientSize.Width, _owner.ClientSize.Height) AndAlso SampleScale = RequestedScale() AndAlso
            FloatTarget = RequestedFloat() AndAlso FloatOutput = RequestedFloatOutput()
    End Function

    Friend Sub Resize()
        If MatchesOwner() Then Return
        If _frames IsNot Nothing Then
            If Not _deviceManager.WaitForIdle(TimeSpan.FromSeconds(2)) Then
                Throw New TimeoutException("DX12 queue did not drain before resizing the control surface.")
            End If
            Dim release = TakeFrameResources()
            release()
        End If
        ReleaseParkedContent()
        _framePublishedToAtlas = False
        SampleScale = RequestedScale()
        FloatTarget = RequestedFloat()
        FloatOutput = RequestedFloatOutput()
        _size = New Size(_owner.ClientSize.Width, _owner.ClientSize.Height)
    End Sub

    Friend Sub Park(retirements As List(Of Action))
        _framePublishedToAtlas = False
        If _frames Is Nothing Then Return ' Already snapshot-only (or never drew).
        If contentPublished Then parkedContent = RetainedContent.QueryInterface(Of ID3D12Resource)()
        retirements.Add(TakeFrameResources())
    End Sub

    Friend Sub StageRetainedContent()
        Atlas.Stage(Me, New Rectangle(Point.Empty, New Drawing.Size(_size.Width, _size.Height)))
    End Sub

    Private Sub ReleaseParkedContent()
        Dim texture = parkedContent
        If texture Is Nothing Then Return
        parkedContent = Nothing
        _deviceManager.RetireAfterGpu(AddressOf texture.Dispose)
    End Sub

    Private Sub RenderClearRegion(color As Color4, requestedRegion As Rectangle)
        If _disposed Then Throw New ObjectDisposedException(NameOf(D3D12_CompositionPresenter))
        If _owner.IsDisposed OrElse Not _owner.IsHandleCreated OrElse _owner.Handle <> _handle Then
            Throw New InvalidOperationException("The target HWND changed during presenter lifetime.")
        End If

        Dim region = Rectangle.Intersect(New Rectangle(0, 0, _size.Width, _size.Height), requestedRegion)
        If region.Width <= 0 OrElse region.Height <= 0 Then Return

        Dim standalone = recordingFrame Is Nothing
        If standalone Then BeginRecording()
        Try
            _commandList.ClearRenderTargetView(recordingFrame.Rtv, color, {
                New Vortice.RawRect(region.Left * SampleScale, region.Top * SampleScale, region.Right * SampleScale, region.Bottom * SampleScale)
            })
            If standalone Then SubmitRecording()
        Catch
            CancelRecording()
            Throw
        End Try
    End Sub

    ' An atlas presenter has a persistent render target shared by both command allocator slots.
    Friend Sub RenderFrame(draw As Action(Of D3D12_PaintContext), Optional dirty As Rectangle = Nothing)
        ArgumentNullException.ThrowIfNull(draw)
        BeginRecording()
        Dim context As D3D12_PaintContext = Nothing
        Dim success As Boolean
        Try
            If contentPublished AndAlso SampleScale = 1 AndAlso Not FloatTarget AndAlso Not dirty.IsEmpty Then
                Dim clippedDirty = Rectangle.Intersect(FrameClip, dirty)
                If clippedDirty.Width > 0 AndAlso clippedDirty.Height > 0 Then FrameClip = clippedDirty
            End If
            recordedRegion = FrameClip
            context = New D3D12_PaintContext(Me)
            RenderClearRegion(New Color4(0, 0, 0, 0), recordedRegion)
            draw(context)
            SubmitRecording()
            success = True
        Catch
            CancelRecording()
            Throw
        Finally
            context?.EndFrame(success)
        End Try
    End Sub

    Private Sub BeginRecording()
        ObjectDisposedException.ThrowIf(_disposed, Me)
        If recordingFrame IsNot Nothing Then Throw New InvalidOperationException("Nested DX12 frames are not supported.")
        If Not _owner.IsHandleCreated OrElse _owner.Handle <> _handle Then Throw New InvalidOperationException("Target HWND changed.")
        If _frames Is Nothing Then CreateFrameResources()
        Dim frame = _frames(slotIndex)
        If Not _deviceManager.WaitForFence(frame.FenceValue, TimeSpan.FromSeconds(2)) Then Throw New TimeoutException("Back buffer is busy.")
        ReleaseFrameUpload(frame)
        frame.UploadArena.Reset()
        frame.DescriptorArena.Reset()
        If frame.GlyphBytes >= 12 * 1024 * 1024 OrElse frame.Glyphs.Count >= 768 Then frame.ClearGlyphs()
        _recordingAllocator = _deviceManager.AcquireCommandAllocator()
        Try
            _commandList.Reset(_recordingAllocator, Nothing)
        Catch
            _deviceManager.CancelCommandAllocator()
            _recordingAllocator = Nothing
            Throw
        End Try
        recordingFrame = frame
        _commandList.ResourceBarrierTransition(frame.Resource, ResourceStates.PixelShaderResource, ResourceStates.RenderTarget)
        FrameClip = _owner.ClientRectangle
        recordedRegion = FrameClip
    End Sub

    Private Sub CancelRecording()
        If recordingFrame Is Nothing Then
            If _recordingAllocator IsNot Nothing Then
                _deviceManager.CancelCommandAllocator()
                _recordingAllocator = Nothing
            End If
            Return
        End If
        _commandList.Close()
        If _recordingAllocator IsNot Nothing Then
            _deviceManager.CancelCommandAllocator()
            _recordingAllocator = Nothing
        End If
        recordingFrame.Backdrop?.Dispose()
        recordingFrame.Backdrop = Nothing
        recordingFrame.BackdropKey = Nothing
        recordingFrame.ClearGlyphs()
        For Each key In newTiles
            Dim entry As LinearTile = Nothing
            If linearTiles.Remove(key, entry) Then
                linearTileBytes -= entry.Bytes
                entry.Texture.Dispose()
            End If
        Next
        newTiles.Clear()
        ReleaseFrameUpload(recordingFrame)
        recordingFrame = Nothing
    End Sub

    Private Sub SubmitRecording()
        Dim frame = recordingFrame
        _commandList.ResourceBarrierTransition(frame.Resource, ResourceStates.RenderTarget, ResourceStates.PixelShaderResource)
        If SampleScale > 1 OrElse (FloatTarget AndAlso Not FloatOutput) Then ResolveFrame(frame)
        _commandList.Close()
        recordingFrame = Nothing
        _deviceManager.DirectQueue.ExecuteCommandLists(New ID3D12CommandList() {_commandList})
        ' The allocator and uploads must remain alive even when publication fails.
        frame.FenceValue = _deviceManager.Signal()
        _deviceManager.CompleteCommandAllocator(frame.FenceValue)
        _recordingAllocator = Nothing
        newTiles.Clear()
        _deviceManager.CacheSubmittedBackdrop(frame.BackdropKey, frame.Backdrop)
        contentPublished = True
        ReleaseParkedContent()
        slotIndex = 1 - slotIndex
        _renderCount += 1
        Atlas?.Stage(Me, recordedRegion)
        ' All copies and draws share the direct queue. Subsequent members of a batch
        ' can sample this revision before the atlas is presented, without a GPU wait.
        D3D12_BackgroundDependencies.SourceChanged(_owner, recordedRegion)
    End Sub

    Private Sub ResolveFrame(frame As BackBufferFrame)
        Dim device = _deviceManager.Device
        If resolved Is Nothing Then
            Dim format = If(FloatOutput, Vortice.DXGI.Format.R16G16B16A16_Float, Vortice.DXGI.Format.B8G8R8A8_UNorm)
            Dim desc = ResourceDescription.Texture2D(format, CUInt(_size.Width), CUInt(_size.Height), 1, 1, flags:=ResourceFlags.AllowRenderTarget)
            resolved = device.CreateCommittedResource(HeapType.Default, desc, ResourceStates.PixelShaderResource)
            resolveHeap = device.CreateDescriptorHeap(New DescriptorHeapDescription(DescriptorHeapType.RenderTargetView, 1))
            device.CreateRenderTargetView(resolved, Nothing, resolveHeap.GetCPUDescriptorHandleForHeapStart())
        End If
        Dim rtv = resolveHeap.GetCPUDescriptorHandleForHeapStart()
        _commandList.ResourceBarrierTransition(resolved, ResourceStates.PixelShaderResource, ResourceStates.RenderTarget)
        _commandList.ClearRenderTargetView(rtv, New Color4(0, 0, 0, 0))
        Dim full As New Rectangle(0, 0, _size.Width, _size.Height)
        _deviceManager.GetImages(FloatOutput).DrawTexture(_commandList, frame.Uploads, rtv, frame.Resource, full, _size, 1, full, resolve:=SampleScale, encode:=FloatTarget AndAlso Not FloatOutput, descriptorArena:=frame.DescriptorArena)
        _commandList.ResourceBarrierTransition(resolved, ResourceStates.RenderTarget, ResourceStates.PixelShaderResource)
    End Sub

    Friend ReadOnly Property RetainedContent As ID3D12Resource
        Get
            If Not contentPublished Then Return parkedContent
            Return If(resolved, _frames(0).Resource)
        End Get
    End Property

    Friend ReadOnly Property HasPublishedFrame As Boolean
        Get
            Return Not _disposed AndAlso _framePublishedToAtlas AndAlso _owner.IsHandleCreated AndAlso _handle = _owner.Handle AndAlso
                _size.Width = _owner.ClientSize.Width AndAlso _size.Height = _owner.ClientSize.Height
        End Get
    End Property

    Friend Sub AcknowledgeAtlasPublication()
        _framePublishedToAtlas = True
        RenderBackendSwitching.ObservePublication(_owner)
    End Sub

    Friend Sub DrawSource(source As D3D12_CompositionPresenter, dest As Rectangle, clip As Rectangle)
        If recordingFrame Is Nothing Then Throw New InvalidOperationException("Drawing requires an open frame.")
        If source._deviceManager IsNot _deviceManager OrElse source.recordingFrame IsNot Nothing Then Return
        clip = Rectangle.Intersect(clip, FrameClip)
        If clip.Width <= 0 OrElse clip.Height <= 0 Then Return
        Dim texture = source.RetainedContent.QueryInterface(Of ID3D12Resource)()
        recordingFrame.Uploads.Add(texture)
        If imagePipeline Is Nothing Then imagePipeline = _deviceManager.GetImages(FloatTarget)
        imagePipeline.DrawTexture(_commandList, recordingFrame.Uploads, recordingFrame.Rtv, texture, dest, _size, 1.0F, clip, scale:=SampleScale,
                                  encode:=Not FloatTarget AndAlso texture.Description.Format = Format.R16G16B16A16_Float, descriptorArena:=recordingFrame.DescriptorArena)
    End Sub

    Friend Sub DrawPremultipliedImage(width As Integer, height As Integer, bgra As Byte(), dest As RectangleF, opacity As Single,
                                     Optional maskBounds As RectangleF = Nothing, Optional radius As Single = 0, Optional corners As Integer = 0,
                                     Optional glyphInk As Color4? = Nothing, Optional subpixel As Boolean = False)
        If recordingFrame Is Nothing Then Throw New InvalidOperationException("Drawing requires an open frame.")
        Dim bytesPerPixel = If(glyphInk.HasValue AndAlso Not subpixel, 1, 4)
        If width <= 0 OrElse height <= 0 OrElse bgra Is Nothing OrElse bgra.LongLength <> CLng(width) * height * bytesPerPixel Then
            Throw New ArgumentException("Expected a tightly packed premultiplied BGRA8 source.", NameOf(bgra))
        End If
        If dest.Width <= 0 OrElse dest.Height <= 0 OrElse opacity <= 0 Then Return
        If imagePipeline Is Nothing Then imagePipeline = _deviceManager.GetImages(FloatTarget)
        Dim glyphPageWidth = If(subpixel, D3D12_GlyphPage.SubpixelWidth, D3D12_GlyphPage.GrayWidth)
        If glyphInk.HasValue AndAlso collectingGlyphs IsNot Nothing AndAlso width <= glyphPageWidth AndAlso height <= D3D12_GlyphPage.MaxHeight Then
            Dim pages = recordingFrame.GlyphPages
            Dim uv As RectangleF
            Dim page As D3D12_GlyphPage = Nothing
            Dim allocatedHeight = 0
            For Each candidate In pages
                If candidate.Subpixel <> subpixel Then Continue For
                allocatedHeight += candidate.Height
                page = candidate
            Next
            Dim packed = page IsNot Nothing AndAlso page.Pack(_commandList, width, height, bgra, uv)
            If Not packed Then
                Dim heightBudget = If(subpixel, 2, 4) * D3D12_GlyphPage.MaxHeight
                ' Keep the first texture and its staging buffer small for ordinary controls.
                Dim requestedHeight = If(allocatedHeight = 0 AndAlso height <= D3D12_GlyphPage.InitialHeight,
                                         D3D12_GlyphPage.InitialHeight, D3D12_GlyphPage.MaxHeight)
                Dim pageHeight = Math.Min(requestedHeight, heightBudget - allocatedHeight)
                If height <= pageHeight Then
                    page = New D3D12_GlyphPage(_deviceManager.Device, subpixel, pageHeight)
                    pages.Add(page)
                    packed = page.Pack(_commandList, width, height, bgra, uv)
                End If
            End If
            If packed Then
                collectingGlyphs.Runs.Add((page.Heap, dest, uv, subpixel))
                imagePipeline.DrawDescriptor(_commandList, recordingFrame.Rtv, page.Heap, dest, _size, 1, FrameClip, uv:=uv, scale:=SampleScale, glyphInk:=glyphInk, subpixel:=subpixel)
                Return
            End If
        End If
        imagePipeline.Draw(_commandList, recordingFrame.Uploads, recordingFrame.Rtv, width, height, bgra, dest, _size, Math.Clamp(opacity, 0.0F, 1.0F), FrameClip, collectingGlyphs, maskBounds, radius, corners, scale:=SampleScale,
                           uploadFormat:=If(bytesPerPixel = 1, Format.R8_UNorm, Format.B8G8R8A8_UNorm), glyphInk:=glyphInk, subpixel:=subpixel, uploadArena:=recordingFrame.UploadArena, descriptorArena:=recordingFrame.DescriptorArena)
    End Sub

    Friend Sub DrawGlyphCoverage(width As Integer, height As Integer, pixels As Byte(), bounds As RectangleF, ink As Color4, Optional subpixel As Boolean = False)
        If collectingCoverage IsNot Nothing Then
            Dim localBounds = bounds
            localBounds.Offset(-coverageOrigin.X, -coverageOrigin.Y)
            collectingCoverage.Add(New GlyphCoverage With {.Width = width, .Height = height, .Pixels = pixels, .Bounds = localBounds, .Subpixel = subpixel})
        End If
        DrawPremultipliedImage(width, height, pixels, bounds, 1, glyphInk:=ink, subpixel:=subpixel)
    End Sub

    Private Sub DrawOrRasterizeCoverage(key As Object, ink As Color4, createRuns As Action, placement As Point)
        Dim coverage As List(Of GlyphCoverage) = Nothing
        If coverageCache.TryGetValue(key, coverage) Then
            For Each run In coverage
                Dim dest = run.Bounds
                dest.Offset(placement.X, placement.Y)
                DrawPremultipliedImage(run.Width, run.Height, run.Pixels, dest, 1, glyphInk:=ink, subpixel:=run.Subpixel)
            Next
            Return
        End If
        coverage = New List(Of GlyphCoverage)()
        collectingCoverage = coverage
        coverageOrigin = placement
        Try
            createRuns()
        Finally
            collectingCoverage = Nothing
        End Try
        Dim bytes = coverage.Sum(Function(run) run.Pixels.LongLength) + 128L
        If bytes > CoverageCacheLimit Then Return
        While glyphCoverageBytes + bytes > CoverageCacheLimit OrElse coverageCache.Count >= 2048
            Dim expired = glyphCoverageOrder.Dequeue()
            glyphCoverageBytes -= coverageCache(expired).Sum(Function(run) run.Pixels.LongLength) + 128L
            coverageCache.Remove(expired)
        End While
        coverageCache.Add(key, coverage)
        glyphCoverageOrder.Enqueue(key)
        glyphCoverageBytes += bytes
    End Sub

    Friend Sub DrawLinearTile(width As Integer, height As Integer, getPixels As Func(Of Byte()), dest As RectangleF,
                              uv As RectangleF, nearest As Boolean, key As Object, Optional sourceFormat As Format = Format.R16G16B16A16_Float,
                              Optional cubicSampling As Boolean = False)
        If recordingFrame Is Nothing Then Throw New InvalidOperationException("Drawing requires an open frame.")
        If dest.Width <= 0 OrElse dest.Height <= 0 OrElse Not dest.IntersectsWith(FrameClip) Then Return
        If imagePipeline Is Nothing Then imagePipeline = _deviceManager.GetImages(FloatTarget)
        Dim entry As LinearTile = Nothing
        Dim texture As ID3D12Resource
        If key IsNot Nothing AndAlso linearTiles.TryGetValue(key, entry) Then
            texture = entry.Texture
        Else
            Dim bytes = CLng(width) * height * If(sourceFormat = Format.R16G16B16A16_Float, 8, 4)
            Dim pixels = getPixels()
            If width <= 0 OrElse height <= 0 OrElse pixels Is Nothing OrElse pixels.LongLength <> bytes Then Throw New ArgumentException("Unexpected cached texture size.")
            texture = imagePipeline.UploadTexture(_commandList, recordingFrame.Uploads, recordingFrame.Uploads, width, height, pixels, sourceFormat, recordingFrame.UploadArena)
            Dim allocation = ((bytes + 65535) \ 65536) * 65536
            If key IsNot Nothing AndAlso allocation <= TileCacheBytes Then
                While linearTileBytes + allocation > TileCacheBytes
                    Dim oldest = linearTiles.MinBy(Function(pair) pair.Value.LastUse)
                    linearTiles.Remove(oldest.Key)
                    linearTileBytes -= oldest.Value.Bytes
                    oldest.Value.Texture.Dispose() ' Every in-flight draw holds its own COM reference.
                End While
                entry = New LinearTile With {.Texture = texture.QueryInterface(Of ID3D12Resource)(), .Bytes = allocation}
                linearTiles.Add(key, entry)
                linearTileBytes += allocation
                newTiles.Add(key)
            End If
        End If
        If entry IsNot Nothing Then
            tileClock += 1L
            entry.LastUse = tileClock
            recordingFrame.Uploads.Add(texture.QueryInterface(Of ID3D12Resource)())
        End If
        imagePipeline.DrawTexture(_commandList, recordingFrame.Uploads, recordingFrame.Rtv, texture, dest, _size, 1, FrameClip,
                                  scale:=SampleScale, encode:=Not FloatTarget AndAlso sourceFormat = Format.R16G16B16A16_Float,
                                  uv:=uv, pointSample:=nearest, descriptorArena:=recordingFrame.DescriptorArena, cubicSampling:=cubicSampling)
    End Sub

    Private Shared ReadOnly noisePixels As Byte() = MakeNoise()

    Private Shared Function MakeNoise() As Byte()
        Dim pixels(D3D_NoiseTexture.Edge * D3D_NoiseTexture.Edge * 4 - 1) As Byte
        Dim random As New Random(1059)
        For i = 0 To pixels.Length - 1 Step 4
            Dim gray = CByte(random.Next(256))
            pixels(i) = gray
            pixels(i + 1) = gray
            pixels(i + 2) = gray
            pixels(i + 3) = 255
        Next
        Return pixels
    End Function

    Friend Sub DrawNoise(bounds As RectangleF, scale As Single, opacity As Byte)
        If opacity = 0 Then Return
        If imagePipeline Is Nothing Then imagePipeline = _deviceManager.GetImages(FloatTarget)
        Const edge = D3D_NoiseTexture.Edge
        imagePipeline.Draw(_commandList, recordingFrame.Uploads, recordingFrame.Rtv, edge, edge, noisePixels,
                           bounds, _size, opacity / 255.0F, FrameClip, tile:=1.0F / Math.Max(1.0F, edge * scale), scale:=SampleScale, uploadArena:=recordingFrame.UploadArena, descriptorArena:=recordingFrame.DescriptorArena)
    End Sub

    Friend Sub DrawBlurredBackdrop(image As Image, dest As RectangleF, radius As Integer, passes As Integer, downsample As Integer)
        If recordingFrame Is Nothing Then Throw New InvalidOperationException("Drawing requires an open frame.")
        If dest.Width <= 0 OrElse dest.Height <= 0 OrElse image Is Nothing Then Return
        Dim down = If(radius > 0 AndAlso passes > 0, Math.Max(1, downsample), 1)
        Dim size As New Size(Math.Max(1, CInt(Math.Ceiling(dest.Width / down))), Math.Max(1, CInt(Math.Ceiling(dest.Height / down))))
        Dim target As New Rectangle(0, 0, size.Width, size.Height)
        Dim factor = Math.Max(size.Width / CSng(image.Width), size.Height / CSng(image.Height))
        Dim scaled As New SizeF(image.Width * factor, image.Height * factor)
        Dim cover As New RectangleF((size.Width - scaled.Width) / 2, (size.Height - scaled.Height) / 2, scaled.Width, scaled.Height)
        If imagePipeline Is Nothing Then imagePipeline = _deviceManager.GetImages(FloatTarget)
        Dim key = (image, D3D_HdrOutput.ImageRevision, size, radius, passes, down)
        Dim output As ID3D12Resource = Nothing
        Dim frame = recordingFrame
        If Not Equals(key, frame.BackdropKey) Then
            Dim cached = _deviceManager.AcquireBackdrop(key)
            If cached IsNot Nothing Then
                If frame.Backdrop IsNot Nothing Then frame.Uploads.Add(frame.Backdrop)
                frame.Backdrop = cached
                frame.BackdropKey = key
            End If
        End If
        If Not Equals(key, frame.BackdropKey) Then
            ' Old output can have earlier draws in this recording; retire it at this slot's next fence.
            If frame.Backdrop IsNot Nothing Then frame.Uploads.Add(frame.Backdrop)
            frame.Backdrop = Nothing
            frame.BackdropKey = Nothing
            Dim rtv As CpuDescriptorHandle
            output = CreateEffectTarget(size, rtv)
            _deviceManager.GetImages().Draw(_commandList, frame.Uploads, rtv, image.Width, image.Height, D3D12_PaintContext.DecodeImage(image), cover, size, 1, target, uploadArena:=frame.UploadArena, descriptorArena:=frame.DescriptorArena)
            _commandList.ResourceBarrierTransition(output, ResourceStates.RenderTarget, ResourceStates.PixelShaderResource)
            If radius > 0 AndAlso passes > 0 Then
                Dim sigma = CSng(Math.Sqrt(passes / 3.0R) * radius / down)
                For axis = 0 To 1
                    Dim nextTarget = CreateEffectTarget(size, rtv)
                    _deviceManager.GetImages().DrawTexture(_commandList, frame.Uploads, rtv, output, target, size, 1, target,
                                              sigma:=sigma, stepX:=If(axis = 0, 1.0F / size.Width, 0), stepY:=If(axis = 1, 1.0F / size.Height, 0))
                    _commandList.ResourceBarrierTransition(nextTarget, ResourceStates.RenderTarget, ResourceStates.PixelShaderResource)
                    output = nextTarget
                Next
            End If
            frame.Backdrop = output.QueryInterface(Of ID3D12Resource)()
            frame.BackdropKey = key
        End If
        imagePipeline.DrawTexture(_commandList, frame.Uploads, frame.Rtv, frame.Backdrop, dest, _size, 1, FrameClip, scale:=SampleScale, descriptorArena:=frame.DescriptorArena)
    End Sub

    Private Function CreateEffectTarget(size As Size, ByRef rtv As CpuDescriptorHandle) As ID3D12Resource
        Dim device = _deviceManager.Device
        Dim desc = ResourceDescription.Texture2D(Format.B8G8R8A8_UNorm, CUInt(size.Width), CUInt(size.Height), 1, 1, flags:=ResourceFlags.AllowRenderTarget)
        Dim resource = device.CreateCommittedResource(HeapType.Default, desc, ResourceStates.RenderTarget)
        recordingFrame.Uploads.Add(resource)
        Dim heap = device.CreateDescriptorHeap(New DescriptorHeapDescription(DescriptorHeapType.RenderTargetView, 1))
        recordingFrame.Uploads.Add(heap)
        rtv = heap.GetCPUDescriptorHandleForHeapStart()
        device.CreateRenderTargetView(resource, Nothing, rtv)
        _commandList.ClearRenderTargetView(rtv, New Color4(0, 0, 0, 0))
        Return resource
    End Function

    Friend Sub DrawGlyphBatch(key As Object, ink As Color4, createRuns As Action, Optional placement As Point = Nothing)
        key = (key, SampleScale)
        Dim frame = recordingFrame
        Dim batch As D3D12_ImagePipeline.GlyphBatch = Nothing
        If frame.Glyphs.TryGetValue(key, batch) Then
            For Each run In batch.Runs
                Dim dest = run.Dest
                dest.Offset(placement.X - batch.Placement.X, placement.Y - batch.Placement.Y)
                imagePipeline.DrawDescriptor(_commandList, frame.Rtv, run.Heap, dest, _size, 1.0F, FrameClip, uv:=run.UV, scale:=SampleScale, glyphInk:=ink, subpixel:=run.Subpixel)
            Next
            Return
        End If
        If frame.GlyphBytes >= 12 * 1024 * 1024 OrElse frame.Glyphs.Count >= 768 Then
            DrawOrRasterizeCoverage(key, ink, createRuns, placement)
            Return
        End If
        batch = New D3D12_ImagePipeline.GlyphBatch() With {.Placement = placement}
        collectingGlyphs = batch
        Try
            DrawOrRasterizeCoverage(key, ink, createRuns, placement)
            frame.Glyphs.Add(key, batch)
            frame.GlyphBytes += batch.EstimatedBytes
        Catch
            ' Keep any partially recorded resources alive if the caller handles the error.
            frame.Uploads.Add(batch)
            Throw
        Finally
            collectingGlyphs = Nothing
        End Try
    End Sub

    Friend Sub RenderRectangle(bounds As Rectangle, color As Color4)
        RenderGeometry(bounds, color, color, bounds.Top, Math.Max(1, bounds.Height), 0, 0, False)
    End Sub

    Friend Sub RenderLinearGradient(bounds As Rectangle, startColor As Color4, endColor As Color4)
        RenderGeometry(bounds, startColor, endColor, bounds.Top, Math.Max(1, bounds.Height), 0, 0, False)
    End Sub

    Friend Sub RenderRoundedRectangle(bounds As RectangleF, radius As Single, color As Color4, Optional stroke As Single = 0)
        Dim effectiveRadius = Math.Clamp(radius, 0.0F, Math.Max(0.0F, Math.Min(bounds.Width, bounds.Height) / 2.0F))
        RenderContour(bounds, color, effectiveRadius, stroke, D3D12_GeometryKind.RoundedRectangle)
    End Sub

    Friend Sub RenderEllipse(bounds As RectangleF, color As Color4, stroke As Single)
        RenderContour(bounds, color, 0, stroke, D3D12_GeometryKind.Ellipse)
    End Sub

    Private Sub RenderContour(bounds As RectangleF, color As Color4, radius As Single, stroke As Single, kind As D3D12_GeometryKind)
        If bounds.Width <= 0 OrElse bounds.Height <= 0 OrElse Not Single.IsFinite(stroke) Then Return
        ' Keep the centreline exact. Only the drawing bounds include the outer stroke and AA fringe.
        RenderGeometry(bounds, color, color, 0, 1, radius, stroke, stroke > 0,
                       kind:=kind, coveragePadding:=stroke / 2.0F + 1.0F / SampleScale)
    End Sub

    Friend Sub RenderLine(start As Vector2, finish As Vector2, width As Single, color As Color4, roundCaps As Boolean)
        Dim delta = finish - start
        Dim length = delta.Length()
        If Not Single.IsFinite(length) OrElse Not Single.IsFinite(width) OrElse width <= 0 Then Return
        Dim half = width / 2.0F
        If length = 0 Then
            If roundCaps Then RenderEllipse(New RectangleF(start.X - half, start.Y - half, width, width), color, 0)
            Return
        End If

        ' Limit shading to a quad around the segment, including the samples at its edges.
        Dim fringe = 1.0F / SampleScale
        Dim axis = delta / length
        Dim side = New Vector2(-axis.Y, axis.X) * (half + fringe)
        Dim cap = axis * (If(roundCaps, half, 0.0F) + fringe)
        Dim a = start - cap + side
        Dim b = finish + cap + side
        Dim c = finish + cap - side
        Dim d = start - cap - side
        Dim p0 As New PointF(a.X, a.Y)
        Dim p1 As New PointF(b.X, b.Y)
        Dim p2 As New PointF(c.X, c.Y)
        Dim p3 As New PointF(d.X, d.Y)
        Dim bounds = RectangleF.FromLTRB(Math.Min(start.X, finish.X), Math.Min(start.Y, finish.Y),
                                         Math.Max(start.X, finish.X), Math.Max(start.Y, finish.Y))
        RenderGeometry(bounds, color, color, 0, 1, 0, width, False,
                       triangleVertices:={p0, p1, p2, p0, p2, p3},
                       kind:=If(roundCaps, D3D12_GeometryKind.RoundLine, D3D12_GeometryKind.FlatLine),
                       linePoints:=New Vector4(start.X, start.Y, finish.X, finish.Y), coveragePadding:=half + fringe)
    End Sub

    Friend Sub RenderJoinedRoundLines(start As Vector2, joint As Vector2, finish As Vector2, width As Single, color As Color4)
        If Not Single.IsFinite(width) OrElse width <= 0 Then Return
        Dim low = Vector2.Min(Vector2.Min(start, joint), finish)
        Dim high = Vector2.Max(Vector2.Max(start, joint), finish)
        Dim bounds = RectangleF.FromLTRB(low.X, low.Y, high.X, high.Y)
        RenderGeometry(bounds, color, color, 0, 1, 0, width, False,
                       kind:=D3D12_GeometryKind.JoinedRoundLines,
                       linePoints:=New Vector4(start.X, start.Y, finish.X, finish.Y), lineJoin:=joint,
                       coveragePadding:=width / 2.0F + 1.0F / SampleScale)
    End Sub

    Friend Sub RenderRectangleStroke(bounds As Rectangle, strokeWidth As Single, color As Color4)
        If strokeWidth <= 0 OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim effectiveStroke = Math.Min(strokeWidth, Math.Min(bounds.Width, bounds.Height) / 2.0F)
        RenderGeometry(bounds, color, color, bounds.Top, Math.Max(1, bounds.Height), 0,
            effectiveStroke, True)
    End Sub

    Friend Sub DrawShape(bounds As Rectangle, radius As Single, first As Color4, last As Color4, gradient As RectangleF,
                         horizontal As Boolean, maskRadius As Single, maskBox As RectangleF)
        If bounds.Width < 1 OrElse bounds.Height < 1 Then Return
        Dim extent = If(horizontal, gradient.Width, gradient.Height)
        RenderGeometry(bounds, first, last, If(horizontal, gradient.Left, gradient.Top), Math.Max(extent, 1),
                       Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2.0F), 0, False,
                       horizontal:=horizontal, maskRadius:=maskRadius, maskBox:=maskBox)
    End Sub

    Friend Sub DrawArc(area As RectangleF, width As Single, start As Single, sweep As Single, first As Color4, last As Color4, fraction As Single)
        If sweep <= 0 OrElse width <= 0 OrElse area.Width <= 0 OrElse area.Height <= 0 Then Return
        Dim radius = Math.Min(area.Width, area.Height) / 2.0F
        area.Inflate(width / 2 + 1, width / 2 + 1)
        Dim arc As New System.Numerics.Vector4(CSng(start * Math.PI / 180), CSng(Math.Min(sweep, 360) * Math.PI / 180), radius, fraction)
        RenderGeometry(Rectangle.Round(area), first, last, 0, 1, 0, width, False, arc:=arc)
    End Sub

    Friend Sub RenderPanelShape(bounds As Rectangle, color As Color4, radius As Single, stroke As Single, mask As Integer)
        radius = Math.Clamp(radius, 0, Math.Max(0, Math.Min(bounds.Width, bounds.Height) / 2.0F))
        RenderGeometry(bounds, color, color, 0, 1, radius, stroke, stroke > 0, cornerMask:=mask)
    End Sub

    Private Sub RenderGeometry(bounds As RectangleF,
                                       startColor As Color4,
                                       endColor As Color4,
                                       gradientTop As Single,
                                       gradientHeight As Single,
                                       cornerRadius As Single,
                                       strokeWidth As Single,
                                       strokeMode As Boolean,
                                       Optional triangleVertices As PointF() = Nothing,
                                       Optional triangleCoverage As Single() = Nothing,
                                       Optional cornerMask As Integer = 15, Optional horizontal As Boolean = False,
                                       Optional maskRadius As Single = 0, Optional maskBox As RectangleF = Nothing,
                                       Optional arc As Vector4 = Nothing,
                                        Optional kind As D3D12_GeometryKind = D3D12_GeometryKind.Legacy,
                                        Optional linePoints As Vector4 = Nothing, Optional lineJoin As Vector2 = Nothing,
                                        Optional coveragePadding As Single = 0)
        If _disposed Then Throw New ObjectDisposedException(NameOf(D3D12_CompositionPresenter))
        If _owner.IsDisposed OrElse Not _owner.IsHandleCreated OrElse _owner.Handle <> _handle Then
            Throw New InvalidOperationException("The target HWND changed during presenter lifetime.")
        End If
        Dim drawBounds = bounds
        drawBounds.Inflate(coveragePadding, coveragePadding)
        Dim clip As New Rectangle(0, 0, _size.Width, _size.Height)
        If recordingFrame IsNot Nothing Then clip.Intersect(FrameClip)
        Dim intersection = RectangleF.Intersect(clip, drawBounds)
        If intersection.Width <= 0 OrElse intersection.Height <= 0 Then Return
        Dim clipped = Rectangle.FromLTRB(CInt(Math.Floor(intersection.Left)), CInt(Math.Floor(intersection.Top)),
                                         CInt(Math.Ceiling(intersection.Right)), CInt(Math.Ceiling(intersection.Bottom)))

        Dim standalone = recordingFrame Is Nothing
        If standalone Then BeginRecording()
        Try
            If _rectanglePipeline Is Nothing Then _rectanglePipeline = _deviceManager.GetShapes(FloatTarget)
            Dim vertexCount As Integer
            Dim upload = If(triangleVertices Is Nothing,
                _rectanglePipeline.RectangleVertices(vertexCount),
                _rectanglePipeline.UploadTriangles(_size, triangleVertices, vertexCount, triangleCoverage, recordingFrame.UploadArena, recordingFrame.Uploads))
            _rectanglePipeline.RecordDraw(_commandList, upload, vertexCount, _size, clipped, bounds, recordingFrame.Rtv,
                startColor, endColor, gradientTop, gradientHeight, cornerRadius, strokeWidth, strokeMode, cornerMask,
                horizontal, maskRadius, maskBox, arc, SampleScale, kind, linePoints, lineJoin)
            If standalone Then SubmitRecording()
        Catch
            CancelRecording()
            Throw
        End Try
    End Sub

    Friend Sub DrawTriangles(points As PointF(), color As Color4)
        If points.Length < 3 Then Return
        Dim bounds = PolygonBounds(points)
        If bounds.Width > 0 AndAlso bounds.Height > 0 Then RenderGeometry(bounds, color, color, 0, 1, 0, 0, False, points)
    End Sub

    Friend Sub RenderPolygon(points As PointF(), color As Color4)
        Dim triangles = TriangulateSimplePolygon(points)
        Dim vertices As New List(Of PointF)(triangles)
        Dim coverage As New List(Of Single)(Enumerable.Repeat(1.0F, triangles.Length))
        AppendAntialiasFringe(points, vertices, coverage, 1.0F)
        Dim bounds = PolygonBounds(points)
        bounds.Inflate(1, 1)
        RenderGeometry(bounds, color, color, bounds.Top, Math.Max(1, bounds.Height), 0, 0, False,
            vertices.ToArray(), coverage.ToArray())
    End Sub

    Private Shared Sub AppendAntialiasFringe(contour As PointF(), vertices As List(Of PointF), coverage As List(Of Single), width As Single)
        Dim outwardSign = If(PolygonSignedArea(contour) > 0, 1.0R, -1.0R)
        For i = 0 To contour.Length - 1
            Dim a = contour(i)
            Dim b = contour((i + 1) Mod contour.Length)
            Dim dx = CDbl(b.X) - a.X
            Dim dy = CDbl(b.Y) - a.Y
            Dim length = Math.Sqrt(dx * dx + dy * dy)
            If length < 0.00000001R Then Continue For
            Dim nx = CSng(outwardSign * dy / length * width)
            Dim ny = CSng(outwardSign * -dx / length * width)
            Dim outerA As New PointF(a.X + nx, a.Y + ny)
            Dim outerB As New PointF(b.X + nx, b.Y + ny)
            vertices.AddRange({a, b, outerB, a, outerB, outerA})
            coverage.AddRange({1.0F, 1.0F, 0.0F, 1.0F, 0.0F, 0.0F})
        Next
    End Sub

    Friend Sub RenderTransformedPolygon(points As PointF(), transform As System.Numerics.Matrix3x2, color As Color4)
        ValidatePolygonTransform(transform)
        If points Is Nothing Then Throw New ArgumentNullException(NameOf(points))
        If points.Length < 3 Then Throw New ArgumentException("At least three polygon points are required.", NameOf(points))
        Dim transformed(points.Length - 1) As PointF
        For i = 0 To points.Length - 1
            transformed(i) = TransformPoint(points(i), transform)
        Next
        RenderPolygon(transformed, color)
    End Sub

    Friend Sub RenderCubicBezierPath(startPoint As PointF,
                                     cubicSegments As PointF(),
                                     transform As System.Numerics.Matrix3x2,
                                     color As Color4,
                                     Optional flatnessTolerance As Single = 0.35F)
        If cubicSegments Is Nothing OrElse cubicSegments.Length < 3 OrElse cubicSegments.Length Mod 3 <> 0 Then
            Throw New ArgumentException("Closed cubic paths require control-point triples (control1, control2, endpoint).", NameOf(cubicSegments))
        End If
        If Not Single.IsFinite(flatnessTolerance) OrElse flatnessTolerance <= 0 Then
            Throw New ArgumentOutOfRangeException(NameOf(flatnessTolerance))
        End If
        ValidatePolygonTransform(transform)
        Dim transformedStart = TransformPoint(startPoint, transform)
        Dim transformedControls(cubicSegments.Length - 1) As PointF
        For i = 0 To cubicSegments.Length - 1
            transformedControls(i) = TransformPoint(cubicSegments(i), transform)
        Next
        Dim lastPoint = transformedControls(transformedControls.Length - 1)
        If DistanceSquared(transformedStart, lastPoint) > 0.0001R Then
            Throw New ArgumentException("Cubic path must end at its starting point.", NameOf(cubicSegments))
        End If

        Dim flattened As New List(Of PointF) From {transformedStart}
        Dim current = transformedStart
        For i = 0 To transformedControls.Length - 1 Step 3
            FlattenCubic(current, transformedControls(i), transformedControls(i + 1), transformedControls(i + 2),
                flatnessTolerance, 0, flattened)
            current = transformedControls(i + 2)
        Next
        If flattened.Count > 1 AndAlso DistanceSquared(flattened(0), flattened(flattened.Count - 1)) < 0.00000001R Then
            flattened.RemoveAt(flattened.Count - 1)
        End If
        RenderPolygon(flattened.ToArray(), color)
    End Sub

    Private Shared Function TransformPoint(point As PointF, transform As System.Numerics.Matrix3x2) As PointF
        If Not Single.IsFinite(point.X) OrElse Not Single.IsFinite(point.Y) Then
            Throw New ArgumentException("Path coordinates must be finite.", NameOf(point))
        End If
        Dim result = New PointF(
            point.X * transform.M11 + point.Y * transform.M21 + transform.M31,
            point.X * transform.M12 + point.Y * transform.M22 + transform.M32)
        If Not Single.IsFinite(result.X) OrElse Not Single.IsFinite(result.Y) Then
            Throw New ArgumentException("Transformed path coordinates must be finite.", NameOf(transform))
        End If
        Return result
    End Function

    Private Shared Sub ValidatePolygonTransform(transform As System.Numerics.Matrix3x2)
        If Not Single.IsFinite(transform.M11) OrElse Not Single.IsFinite(transform.M12) OrElse
           Not Single.IsFinite(transform.M21) OrElse Not Single.IsFinite(transform.M22) OrElse
           Not Single.IsFinite(transform.M31) OrElse Not Single.IsFinite(transform.M32) Then
            Throw New ArgumentException("Polygon transform must contain finite values.", NameOf(transform))
        End If
        Dim determinant = CDbl(transform.M11) * transform.M22 - CDbl(transform.M12) * transform.M21
        If Math.Abs(determinant) < 0.00000001R Then Throw New ArgumentException("Polygon transform must be invertible.", NameOf(transform))
    End Sub

    Private Shared Sub FlattenCubic(p0 As PointF, p1 As PointF, p2 As PointF, p3 As PointF,
                                    tolerance As Single, depth As Integer, output As List(Of PointF))
        If depth >= 12 OrElse Math.Max(DistanceToLine(p1, p0, p3), DistanceToLine(p2, p0, p3)) <= tolerance Then
            output.Add(p3)
            Return
        End If
        Dim p01 = Midpoint(p0, p1)
        Dim p12 = Midpoint(p1, p2)
        Dim p23 = Midpoint(p2, p3)
        Dim p012 = Midpoint(p01, p12)
        Dim p123 = Midpoint(p12, p23)
        Dim p0123 = Midpoint(p012, p123)
        FlattenCubic(p0, p01, p012, p0123, tolerance, depth + 1, output)
        FlattenCubic(p0123, p123, p23, p3, tolerance, depth + 1, output)
    End Sub

    Private Shared Function Midpoint(a As PointF, b As PointF) As PointF
        Return New PointF((a.X + b.X) * 0.5F, (a.Y + b.Y) * 0.5F)
    End Function

    Private Shared Function DistanceToLine(point As PointF, start As PointF, finish As PointF) As Double
        Dim dx = CDbl(finish.X) - start.X
        Dim dy = CDbl(finish.Y) - start.Y
        Dim length = Math.Sqrt(dx * dx + dy * dy)
        If length < 0.00000001R Then Return Math.Sqrt(DistanceSquared(point, start))
        Return Math.Abs(dx * (CDbl(point.Y) - start.Y) - dy * (CDbl(point.X) - start.X)) / length
    End Function

    Private Shared Function TriangulateSimplePolygon(points As PointF()) As PointF()
        If points Is Nothing OrElse points.Length < 3 Then Throw New ArgumentException("At least three polygon points are required.", NameOf(points))
        For Each p In points
            If Not Single.IsFinite(p.X) OrElse Not Single.IsFinite(p.Y) Then
                Throw New ArgumentException("Polygon coordinates must be finite.", NameOf(points))
            End If
        Next
        Dim vertices As New List(Of PointF)(points)
        RemoveCollinearVertices(vertices)
        If vertices.Count < 3 Then Throw New ArgumentException("Polygon is degenerate.", NameOf(points))
        RejectSelfIntersections(vertices, points)
        Dim signedArea = PolygonSignedArea(vertices)
        If Math.Abs(signedArea) < 0.0001R Then Throw New ArgumentException("Polygon has zero area.", NameOf(points))
        Dim winding As Integer = Math.Sign(signedArea)
        Dim remaining As New List(Of Integer)(Enumerable.Range(0, vertices.Count))
        Dim triangles As New List(Of PointF)((vertices.Count - 2) * 3)
        Dim maxIterations = vertices.Count * vertices.Count
        Dim iterations As Integer

        While remaining.Count > 3 AndAlso iterations < maxIterations
            iterations += 1
            Dim clippedEar = False
            For i = 0 To remaining.Count - 1
                Dim previousIndex = remaining((i + remaining.Count - 1) Mod remaining.Count)
                Dim currentIndex = remaining(i)
                Dim nextIndex = remaining((i + 1) Mod remaining.Count)
                Dim a = vertices(previousIndex)
                Dim b = vertices(currentIndex)
                Dim c = vertices(nextIndex)
                If winding * Cross(a, b, c) <= 0 Then Continue For

                Dim containsVertex = False
                For Each candidate In remaining
                    If candidate = previousIndex OrElse candidate = currentIndex OrElse candidate = nextIndex Then Continue For
                    If IsPointInTriangle(vertices(candidate), a, b, c, winding) Then
                        containsVertex = True
                        Exit For
                    End If
                Next
                If containsVertex Then Continue For

                triangles.Add(a)
                triangles.Add(b)
                triangles.Add(c)
                remaining.RemoveAt(i)
                clippedEar = True
                Exit For
            Next
            If Not clippedEar Then Throw New ArgumentException("Polygon could not be triangulated without overlap.", NameOf(points))
        End While
        If remaining.Count <> 3 Then Throw New ArgumentException("Polygon triangulation did not converge.", NameOf(points))
        triangles.Add(vertices(remaining(0)))
        triangles.Add(vertices(remaining(1)))
        triangles.Add(vertices(remaining(2)))
        Return triangles.ToArray()
    End Function

    Private Shared Sub RemoveCollinearVertices(vertices As List(Of PointF))
        For i = 0 To vertices.Count - 1
            If DistanceSquared(vertices(i), vertices((i + 1) Mod vertices.Count)) < 0.00000001R Then
                Throw New ArgumentException("Zero-length polygon edges are not supported.", NameOf(vertices))
            End If
        Next
        Dim changed = True
        While changed AndAlso vertices.Count >= 3
            changed = False
            For i = 0 To vertices.Count - 1
                Dim previous = vertices((i + vertices.Count - 1) Mod vertices.Count)
                Dim current = vertices(i)
                Dim nextPoint = vertices((i + 1) Mod vertices.Count)
                If Math.Abs(Cross(previous, current, nextPoint)) < 0.0001R AndAlso IsBetween(previous, current, nextPoint) Then
                    vertices.RemoveAt(i)
                    changed = True
                    Exit For
                End If
            Next
        End While
    End Sub

    Private Shared Sub RejectSelfIntersections(vertices As List(Of PointF), originalPoints As PointF())
        For i = 0 To vertices.Count - 1
            Dim a = vertices(i)
            Dim b = vertices((i + 1) Mod vertices.Count)
            For j = i + 1 To vertices.Count - 1
                If j = i OrElse j = (i + 1) Mod vertices.Count OrElse (j + 1) Mod vertices.Count = i Then Continue For
                Dim c = vertices(j)
                Dim d = vertices((j + 1) Mod vertices.Count)
                If SegmentsIntersect(a, b, c, d) Then
                    Throw New ArgumentException("Self-intersecting polygons are not supported.", NameOf(originalPoints))
                End If
            Next
        Next
    End Sub

    Private Shared Function PolygonSignedArea(vertices As IList(Of PointF)) As Double
        Dim area As Double
        For i = 0 To vertices.Count - 1
            Dim a = vertices(i)
            Dim b = vertices((i + 1) Mod vertices.Count)
            area += CDbl(a.X) * b.Y - CDbl(b.X) * a.Y
        Next
        Return area * 0.5R
    End Function

    Private Shared Function Cross(a As PointF, b As PointF, c As PointF) As Double
        Return (CDbl(b.X) - a.X) * (CDbl(c.Y) - a.Y) - (CDbl(b.Y) - a.Y) * (CDbl(c.X) - a.X)
    End Function

    Private Shared Function DistanceSquared(a As PointF, b As PointF) As Double
        Dim dx = CDbl(a.X) - b.X
        Dim dy = CDbl(a.Y) - b.Y
        Return dx * dx + dy * dy
    End Function

    Private Shared Function IsBetween(a As PointF, b As PointF, c As PointF) As Boolean
        Return (CDbl(b.X) - a.X) * (b.X - c.X) + (CDbl(b.Y) - a.Y) * (b.Y - c.Y) <= 0
    End Function

    Private Shared Function IsPointInTriangle(point As PointF, a As PointF, b As PointF, c As PointF, winding As Integer) As Boolean
        Return winding * Cross(a, b, point) >= 0 AndAlso
               winding * Cross(b, c, point) >= 0 AndAlso
               winding * Cross(c, a, point) >= 0
    End Function

    Private Shared Function SegmentsIntersect(a As PointF, b As PointF, c As PointF, d As PointF) As Boolean
        Dim abC = Cross(a, b, c)
        Dim abD = Cross(a, b, d)
        Dim cdA = Cross(c, d, a)
        Dim cdB = Cross(c, d, b)
        If Math.Sign(abC) <> Math.Sign(abD) AndAlso Math.Sign(cdA) <> Math.Sign(cdB) Then Return True
        Return (Math.Abs(abC) < 0.0001R AndAlso IsBetween(a, c, b)) OrElse
               (Math.Abs(abD) < 0.0001R AndAlso IsBetween(a, d, b)) OrElse
               (Math.Abs(cdA) < 0.0001R AndAlso IsBetween(c, a, d)) OrElse
               (Math.Abs(cdB) < 0.0001R AndAlso IsBetween(c, b, d))
    End Function

    Private Shared Function PolygonBounds(points As PointF()) As Rectangle
        Dim left = points.Min(Function(p) p.X)
        Dim top = points.Min(Function(p) p.Y)
        Dim right = points.Max(Function(p) p.X)
        Dim bottom = points.Max(Function(p) p.Y)
        Return Rectangle.FromLTRB(CInt(Math.Floor(left)), CInt(Math.Floor(top)), CInt(Math.Ceiling(right)), CInt(Math.Ceiling(bottom)))
    End Function

    Private Shared Sub ReleaseFrameUpload(frame As BackBufferFrame)
        For Each upload In frame.Uploads
            upload.Dispose()
        Next
        frame.Uploads.Clear()
    End Sub

    Private Function AcquireFrameTarget(index As Integer) As ID3D12Resource
        ' All producers, background consumers and atlas copies are ordered on the same DIRECT queue.
        ' Only allocators and host-written uploads need separate in-flight slots.
        If index > 0 Then Return _frames(0).Resource.QueryInterface(Of ID3D12Resource)()
        Dim desc = ResourceDescription.Texture2D(If(FloatTarget, Format.R16G16B16A16_Float, Format.B8G8R8A8_UNorm), CUInt(_size.Width * SampleScale), CUInt(_size.Height * SampleScale), 1, 1, flags:=ResourceFlags.AllowRenderTarget)
        Return _deviceManager.Device.CreateCommittedResource(HeapType.Default, desc, ResourceStates.PixelShaderResource)
    End Function

    Private Sub CreateFrameResources()
        Dim heapDescription As New DescriptorHeapDescription(DescriptorHeapType.RenderTargetView,
            CUInt(BufferCount), DescriptorHeapFlags.None, 0)
        _rtvHeap = _deviceManager.Device.CreateDescriptorHeap(Of ID3D12DescriptorHeap)(heapDescription)
        _rtvIncrement = CInt(_deviceManager.Device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView))
        _frames = New BackBufferFrame(BufferCount - 1) {}
        Dim handle = _rtvHeap.GetCPUDescriptorHandleForHeapStart()
        For i = 0 To BufferCount - 1
            Dim frame As New BackBufferFrame With {
                .Resource = AcquireFrameTarget(i),
                .Rtv = handle
            }
            _deviceManager.Device.CreateRenderTargetView(frame.Resource, Nothing, frame.Rtv)
            _frames(i) = frame
            handle.Ptr = New UIntPtr(handle.Ptr.ToUInt64() + CULng(_rtvIncrement))
        Next
        Dim allocator = _deviceManager.AcquireCommandAllocator()
        Try
            _commandList = _deviceManager.Device.CreateCommandList(Of ID3D12GraphicsCommandList)(
                0, CommandListType.Direct, allocator, Nothing)
            _commandList.Close()
        Finally
            _deviceManager.CancelCommandAllocator()
        End Try
    End Sub

    Private Function TakeFrameResources() As Action
        Dim frames = _frames
        Dim list = _commandList
        Dim rtv = _rtvHeap
        Dim resolveRtv = resolveHeap
        Dim resolvedTexture = resolved
        Dim tiles = linearTiles.Values.ToArray()
        _frames = Nothing
        _commandList = Nothing
        _rtvHeap = Nothing
        resolveHeap = Nothing
        resolved = Nothing
        contentPublished = False
        slotIndex = 0
        linearTiles.Clear()
        linearTileBytes = 0
        imagePipeline = Nothing
        _rectanglePipeline = Nothing
        coverageCache.Clear()
        glyphCoverageOrder.Clear()
        glyphCoverageBytes = 0
        Return Sub()
                   list?.Dispose()
                   If frames IsNot Nothing Then
                       For Each frame In frames
                           If frame Is Nothing Then Continue For
                           ReleaseFrameUpload(frame)
                           frame.Backdrop?.Dispose()
                           frame.UploadArena.Dispose()
                           frame.DescriptorArena.Dispose()
                           frame.ClearGlyphs()
                           frame.Resource?.Dispose()
                       Next
                   End If
                   For Each tile In tiles
                       tile.Texture.Dispose()
                   Next
                   resolveRtv?.Dispose()
                   resolvedTexture?.Dispose()
                   rtv?.Dispose()
               End Sub
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        _framePublishedToAtlas = False
        If _frames IsNot Nothing OrElse _commandList IsNot Nothing Then _deviceManager.RetireAfterGpu(TakeFrameResources())
        ReleaseParkedContent()
        _handle = IntPtr.Zero
        GC.SuppressFinalize(Me)
    End Sub
End Class
