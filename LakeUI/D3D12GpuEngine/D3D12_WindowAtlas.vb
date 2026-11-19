Imports Vortice.Direct3D12
Imports Vortice.DirectComposition
Imports Vortice.DXGI

' All child HWNDs of one host sample independent rectangles in one flip chain.
Friend NotInheritable Class D3D12_WindowAtlas
    Implements IDisposable

    Private NotInheritable Class Slot
        Public Presenter As D3D12_CompositionPresenter
        Public Target As IDCompositionTarget
        Public Visual As IDCompositionVisual
        Public PendingRegions(1) As Rectangle
        Public Bounds As Rectangle
    End Class

    Private ReadOnly manager As D3D12_DeviceManager
    Private ReadOnly composition As IDCompositionDevice
    Private ReadOnly factory As IDXGIFactory2
    Private ReadOnly slots As New Dictionary(Of D3D12_CompositionPresenter, Slot)()
    Private ReadOnly pending As New HashSet(Of D3D12_CompositionPresenter)()
    Private chain As IDXGISwapChain3
    Private atlasSize As Size
    Private activeFormat As Format = Format.B8G8R8A8_UNorm
    Private rtvHeap As ID3D12DescriptorHeap
    Private ReadOnly descriptors As D3D12_DescriptorArena() = {New D3D12_DescriptorArena(), New D3D12_DescriptorArena()}
    Private ReadOnly conversions As List(Of IDisposable)() = {New List(Of IDisposable)(), New List(Of IDisposable)()}
    Friend ReadOnly Property ColorSpace As ColorSpaceType
        Get
            Return If(activeFormat = Format.R16G16B16A16_Float, ColorSpaceType.RgbFullG10NoneP709, ColorSpaceType.RgbFullG22NoneP709)
        End Get
    End Property

    Private Function RequestedFormat() As Format
        Return If(slots.Keys.Any(Function(presenter) presenter.FloatOutput AndAlso presenter.OwnerControl.Visible), Format.R16G16B16A16_Float, Format.B8G8R8A8_UNorm)
    End Function

    Private allocators(1) As ID3D12CommandAllocator
    Private buffers(1) As ID3D12Resource
    Private fences(1) As Long
    Private commands As ID3D12GraphicsCommandList
    Private layoutDirty As Boolean = True
    Friend ReadOnly Property Count As Integer
        Get
            Return slots.Count
        End Get
    End Property

    Friend Sub New(manager As D3D12_DeviceManager)
        Me.manager = manager
        factory = Global.Vortice.DXGI.DXGI.CreateDXGIFactory2(Of IDXGIFactory2)(False)
        composition = DComp.DCompositionCreateDevice2(Of IDCompositionDevice)(Nothing)
    End Sub

    Friend Sub Attach(presenter As D3D12_CompositionPresenter)
        Dim slot As New Slot With {.Presenter = presenter}
        Try
            composition.CreateTargetForHwnd(presenter.WindowHandle, False, slot.Target).CheckError()
            composition.CreateVisual(slot.Visual).CheckError()
            slot.Target.SetRoot(slot.Visual).CheckError()
            slots.Add(presenter, slot)
            layoutDirty = True
        Catch
            slot.Visual?.Dispose()
            slot.Target?.Dispose()
            Throw
        End Try
    End Sub

    Friend Sub Detach(presenter As D3D12_CompositionPresenter)
        Dim slot As Slot = Nothing
        If Not slots.Remove(presenter, slot) Then Return
        pending.Remove(presenter)
        Try
            slot.Target.SetRoot(Nothing).CheckError()
            slot.Visual.SetContent(Nothing).CheckError()
            composition.Commit().CheckError()
        Catch When manager.IsDeviceRemoved
            ' DirectComposition can lose its source at the same time as the queue.
        Finally
            slot.Visual.Dispose()
            slot.Target.Dispose()
        End Try
        layoutDirty = True
    End Sub

    Friend Sub Stage(presenter As D3D12_CompositionPresenter, region As Rectangle)
        Dim slot = slots(presenter)
        If slot.Bounds.Size <> presenter.OwnerControl.ClientSize Then layoutDirty = True
        For index = 0 To 1
            Dim previous = slot.PendingRegions(index)
            slot.PendingRegions(index) = If(previous.IsEmpty, region, Rectangle.Union(previous, region))
        Next
        pending.Add(presenter)
    End Sub

    Private Sub Arrange()
        Dim ordered = slots.Values.OrderByDescending(Function(v) v.Presenter.BufferSize.Height).ToArray()
        Dim maxWidth = ordered.Max(Function(v) v.Presenter.BufferSize.Width)
        Dim bestArea As Long = Long.MaxValue
        Dim nextSize As Size
        Dim positions As New Dictionary(Of Slot, Rectangle)()
        ' Try shelf widths at actual control boundaries to avoid a mostly empty square atlas.
        Dim widths As New HashSet(Of Integer) From {maxWidth}
        For Each slot In ordered
            Dim w = slot.Presenter.BufferSize.Width
            For Each existing In widths.ToArray()
                If existing + w <= Math.Min(8192, maxWidth * 3) Then widths.Add(existing + w)
                If widths.Count > 128 Then Exit For
            Next
            If widths.Count > 128 Then Exit For
        Next
        For Each width In widths
            Dim x, y, row, usedWidth As Integer
            For Each slot In ordered
                Dim size = slot.Presenter.BufferSize
                If x + size.Width > width Then
                    x = 0
                    y += row
                    row = 0
                End If
                positions(slot) = New Rectangle(x, y, size.Width, size.Height)
                x += size.Width
                usedWidth = Math.Max(usedWidth, x)
                row = Math.Max(row, size.Height)
            Next
            Dim height = y + row
            Dim area = CLng(usedWidth) * height
            If height > 16384 OrElse area >= bestArea Then Continue For
            bestArea = area
            nextSize = New Size(usedWidth, height)
            For Each slot In ordered
                slot.Bounds = positions(slot)
            Next
        Next
        If nextSize.IsEmpty Then Throw New NotSupportedException("Visible HWND textures exceed the DX12 atlas dimensions.")
        Dim requested = RequestedFormat()
        If nextSize <> atlasSize OrElse requested <> activeFormat Then RebuildChain(nextSize, requested)
        For Each slot In slots.Values
            Dim r = slot.Bounds
            Array.Fill(slot.PendingRegions, New Rectangle(Point.Empty, r.Size))
            slot.Visual.SetContent(chain).CheckError()
            slot.Visual.SetClip(New Vortice.RawRectF(r.Left, r.Top, r.Right, r.Bottom)).CheckError()
            slot.Visual.SetOffsetX(CSng(-r.Left)).CheckError()
            slot.Visual.SetOffsetY(CSng(-r.Top)).CheckError()
        Next
    End Sub

    Private Sub Drain()
        If Not manager.WaitForIdle(TimeSpan.FromSeconds(3)) Then Throw New TimeoutException("Window atlas queue is busy.")
    End Sub

    Private Sub RebuildChain(size As Size, format As Format)
        If chain IsNot Nothing Then Drain()
        rtvHeap?.Dispose()
        rtvHeap = Nothing
        For i = 0 To 1
            ReleaseConversions(i)
            buffers(i)?.Dispose()
            buffers(i) = Nothing
        Next
        If chain Is Nothing Then
            Dim desc As New SwapChainDescription1(CUInt(size.Width), CUInt(size.Height), format, False,
                Usage.RenderTargetOutput, 2, Scaling.Stretch, SwapEffect.FlipSequential, AlphaMode.Premultiplied, SwapChainFlags.None)
            Using created = factory.CreateSwapChainForComposition(manager.DirectQueue, desc, Nothing)
                chain = created.QueryInterface(Of IDXGISwapChain3)()
            End Using
            For i = 0 To 1
                allocators(i) = manager.Device.CreateCommandAllocator(Of ID3D12CommandAllocator)(CommandListType.Direct)
            Next
            commands = manager.Device.CreateCommandList(Of ID3D12GraphicsCommandList)(0, CommandListType.Direct, allocators(0), Nothing)
            commands.Close()
        Else
            chain.ResizeBuffers(2, CUInt(size.Width), CUInt(size.Height), format, SwapChainFlags.None).CheckError()
        End If
        activeFormat = format
        chain.SetColorSpace1(ColorSpace)
        For i = 0 To 1
            buffers(i) = chain.GetBuffer(Of ID3D12Resource)(CUInt(i))
        Next
        If activeFormat = Format.R16G16B16A16_Float Then
            rtvHeap = manager.Device.CreateDescriptorHeap(New DescriptorHeapDescription(DescriptorHeapType.RenderTargetView, 2))
            For i = 0 To 1
                manager.Device.CreateRenderTargetView(buffers(i), Nothing, RenderView(i))
            Next
        End If
        atlasSize = size
    End Sub

    Friend Sub Flush()
        If pending.Count = 0 Then Return
        If RequestedFormat() <> activeFormat Then layoutDirty = True
        If layoutDirty Then Arrange()
        Dim index = CInt(chain.CurrentBackBufferIndex)
        If Not manager.WaitForFence(fences(index), TimeSpan.FromSeconds(3)) Then Throw New TimeoutException("Atlas buffer is busy.")
        ReleaseConversions(index)
        descriptors(index).Reset()
        allocators(index).Reset()
        commands.Reset(allocators(index), Nothing)
        Dim copied As New List(Of Vortice.RawRect)()
        Dim dst = buffers(index)
        commands.ResourceBarrierTransition(dst, ResourceStates.Present, ResourceStates.CopyDest)
        For Each slot In slots.Values
            Dim dirty = slot.PendingRegions(index)
            If dirty.IsEmpty Then Continue For
            ' Keep stale damage pending while a hidden HWND has no frame to publish.
            If Not layoutDirty AndAlso Not slot.Presenter.OwnerControl.Visible AndAlso Not pending.Contains(slot.Presenter) Then Continue For
            Dim source = slot.Presenter.RetainedContent
            If source Is Nothing Then Continue For
            Dim destRegion = dirty
            destRegion.Offset(slot.Bounds.Location)
            copied.Add(New Vortice.RawRect(destRegion.Left, destRegion.Top, destRegion.Right, destRegion.Bottom))
            slot.PendingRegions(index) = Rectangle.Empty
            If source.Description.Format <> activeFormat Then
                ' SDR content is decoded into the same scRGB atlas when a host contains both kinds of slot.
                If rtvHeap Is Nothing Then Continue For
                commands.ResourceBarrierTransition(dst, ResourceStates.CopyDest, ResourceStates.RenderTarget)
                Dim rtv = RenderView(index)
                commands.ClearRenderTargetView(rtv, New Vortice.Mathematics.Color4(0, 0, 0, 0), {copied.Last()})
                Dim area As New Vortice.Mathematics.Size(atlasSize.Width, atlasSize.Height)
                manager.GetImages(True).DrawTexture(commands, conversions(index), rtv, source, slot.Bounds, area, 1, destRegion, pointSample:=True, descriptorArena:=descriptors(index))
                commands.ResourceBarrierTransition(dst, ResourceStates.RenderTarget, ResourceStates.CopyDest)
                Continue For
            End If
            commands.ResourceBarrierTransition(source, ResourceStates.PixelShaderResource, ResourceStates.CopySource)
            Dim sourceRegion As New Vortice.Mathematics.Box(dirty.Left, dirty.Top, 0, dirty.Right, dirty.Bottom, 1)
            commands.CopyTextureRegion(New TextureCopyLocation(dst, 0UI), CUInt(destRegion.X), CUInt(destRegion.Y), 0,
                                       New TextureCopyLocation(source, 0UI), sourceRegion)
            commands.ResourceBarrierTransition(source, ResourceStates.CopySource, ResourceStates.PixelShaderResource)
        Next
        commands.ResourceBarrierTransition(dst, ResourceStates.CopyDest, ResourceStates.Present)
        commands.Close()
        manager.DirectQueue.ExecuteCommandLists({DirectCast(commands, ID3D12CommandList)})
        fences(index) = manager.Signal()
        If layoutDirty Then
            chain.Present(0, PresentFlags.None).CheckError()
        Else
            chain.Present1(0, PresentFlags.None, New PresentParameters With {.DirtyRectangles = copied.ToArray()}).CheckError()
        End If
        If layoutDirty Then composition.Commit().CheckError()
        layoutDirty = False
        Dim members = pending.ToArray()
        pending.Clear()
        For Each member In members
            member.AcknowledgeAtlasPublication()
        Next
        RenderBackendStartup.PublishedFrame(RenderBackend.Direct3D12)
    End Sub

    Private Function RenderView(index As Integer) As CpuDescriptorHandle
        Dim handle = rtvHeap.GetCPUDescriptorHandleForHeapStart()
        Dim stepBytes = manager.Device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView)
        handle.Ptr = New UIntPtr(handle.Ptr.ToUInt64() + CULng(index) * stepBytes)
        Return handle
    End Function

    Private Sub ReleaseConversions(index As Integer)
        For Each resource In conversions(index)
            resource.Dispose()
        Next
        conversions(index).Clear()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        manager.RetireAfterGpu(AddressOf ReleaseGpuResources)
        composition.Dispose()
        factory.Dispose()
    End Sub

    Private Sub ReleaseGpuResources()
        commands?.Dispose()
        rtvHeap?.Dispose()
        For i = 0 To 1
            ReleaseConversions(i)
            descriptors(i).Dispose()
            buffers(i)?.Dispose()
            allocators(i)?.Dispose()
        Next
        chain?.Dispose()
    End Sub
End Class
