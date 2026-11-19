Imports Vortice.Direct3D12
Imports Vortice.DXGI
Imports Vortice.Mathematics

Friend NotInheritable Class D3D12_ImagePipeline
    Implements IDisposable

    Private ReadOnly device As ID3D12Device
    Private ReadOnly linearTarget As Boolean
    Private ReadOnly targetFormat As Format
    Private signature As ID3D12RootSignature
    Private state As ID3D12PipelineState
    Private subpixelState As ID3D12PipelineState

    Friend Sub New(device As ID3D12Device, Optional targetFormat As Format = Format.B8G8R8A8_UNorm)
        Me.device = device
        Me.targetFormat = targetFormat
        linearTarget = targetFormat = Format.R16G16B16A16_Float
        Dim sampler As New StaticSamplerDescription(ShaderVisibility.Pixel, 0, 0) With {
            .Filter = Filter.MinMagMipLinear, .AddressU = TextureAddressMode.Clamp,
            .AddressV = TextureAddressMode.Clamp, .AddressW = TextureAddressMode.Clamp}
        Dim repeat As New StaticSamplerDescription(ShaderVisibility.Pixel, 1, 0) With {
            .Filter = Filter.MinMagMipLinear, .AddressU = TextureAddressMode.Wrap, .AddressV = TextureAddressMode.Wrap, .AddressW = TextureAddressMode.Wrap}
        Dim description As New RootSignatureDescription(RootSignatureFlags.None, {
            New RootParameter(New RootConstants(0, 0, 32), ShaderVisibility.All),
            New RootParameter(New RootDescriptorTable(New DescriptorRange(DescriptorRangeType.ShaderResourceView, 1, 0)), ShaderVisibility.Pixel)
        }, {sampler, repeat})
        Try
            signature = device.CreateRootSignature(description, RootSignatureVersion.Version1)
            state = CreatePipelineState(targetFormat, subpixel:=False)
        Catch
            Dispose()
            Throw
        End Try
    End Sub

    Private Function CreatePipelineState(targetFormat As Format, subpixel As Boolean) As ID3D12PipelineState
        ' RGB coverage uses dual-source blending and preserves the opaque destination alpha.
        Dim pipeline As New GraphicsPipelineStateDescription With {
            .RootSignature = signature, .VertexShader = D3D12ShaderCompiler.Load("Image.vs"),
            .PixelShader = D3D12ShaderCompiler.Load(If(subpixel, "ImageSubpixel.ps", "Image.ps")),
            .BlendState = If(subpixel, New BlendDescription(Blend.One, Blend.InverseSource1Color, Blend.Zero, Blend.One), BlendDescription.AlphaBlend),
            .SampleMask = UInteger.MaxValue, .RasterizerState = RasterizerDescription.CullNone,
            .DepthStencilState = DepthStencilDescription.None, .PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
            .RenderTargetFormats = {targetFormat}, .SampleDescription = New SampleDescription(1, 0)}
        Return device.CreateGraphicsPipelineState(pipeline)
    End Function

    Friend Sub Draw(list As ID3D12GraphicsCommandList, resources As List(Of IDisposable), rtv As CpuDescriptorHandle,
                    width As Integer, height As Integer, pixels As Byte(), dest As Drawing.RectangleF, surface As Size, opacity As Single, clip As Drawing.Rectangle,
                    Optional retained As GlyphBatch = Nothing, Optional maskBounds As Drawing.RectangleF = Nothing,
                    Optional radius As Single = 0, Optional corners As Integer = 0, Optional tile As Single = 0, Optional scale As Integer = 1, Optional uploadFormat As Format = Format.B8G8R8A8_UNorm,
                    Optional sourceUV As Drawing.RectangleF = Nothing, Optional pointSample As Boolean = False, Optional glyphInk As Color4? = Nothing, Optional subpixel As Boolean = False,
                    Optional uploadArena As D3D12_UploadArena = Nothing, Optional descriptorArena As D3D12_DescriptorArena = Nothing)
        Dim texture = UploadTexture(list, resources, If(retained?.Resources, resources), width, height, pixels, uploadFormat, uploadArena)
        DrawTexture(list, resources, rtv, texture, dest, surface, opacity, clip, retained, maskBounds, radius, corners, If(tile > 0, -1, 0), tile, tile, scale:=scale, uv:=sourceUV, pointSample:=pointSample, glyphInk:=glyphInk, subpixel:=subpixel, descriptorArena:=descriptorArena)
    End Sub

    Friend Function UploadTexture(list As ID3D12GraphicsCommandList, resources As List(Of IDisposable), owners As List(Of IDisposable),
                                  width As Integer, height As Integer, pixels As Byte(), uploadFormat As Format, Optional arena As D3D12_UploadArena = Nothing) As ID3D12Resource
        Dim rowBytes = width * If(uploadFormat = Format.R16G16B16A16_Float, 8, If(uploadFormat = Format.R8_UNorm, 1, 4))
        Dim desc = ResourceDescription.Texture2D(uploadFormat, CUInt(width), CUInt(height), 1, 1)
        Dim texture = device.CreateCommittedResource(HeapType.Default, desc, ResourceStates.CopyDest)
        owners.Add(texture)
        Dim layout(0) As PlacedSubresourceFootPrint
        Dim rows(0) As UInteger
        Dim sizes(0) As ULong
        Dim total As ULong
        device.GetCopyableFootprints(desc, 0, 1, 0, layout, rows, sizes, total)
        Dim upload As ID3D12Resource
        Dim baseOffset As Integer
        If arena IsNot Nothing Then
            Dim allocation = arena.Allocate(device, CInt(total), 512, resources)
            upload = allocation.Resource
            baseOffset = allocation.Offset
        Else
            upload = device.CreateCommittedResource(HeapType.Upload, ResourceDescription.Buffer(total), ResourceStates.GenericRead)
            resources.Add(upload)
        End If
        layout(0).Offset = CULng(baseOffset)
        Dim mapped = upload.Map(Of Byte)(0, baseOffset + CInt(total))
        Try
            For y = 0 To height - 1
                pixels.AsSpan(y * rowBytes, rowBytes).CopyTo(mapped.Slice(baseOffset + y * CInt(layout(0).Footprint.RowPitch), rowBytes))
            Next
        Finally
            upload.Unmap(0, New Range(CUInt(baseOffset), CUInt(baseOffset + CInt(total))))
        End Try
        list.CopyTextureRegion(New TextureCopyLocation(texture, 0UI), 0, 0, 0, New TextureCopyLocation(upload, layout(0)), Nothing)
        list.ResourceBarrierTransition(texture, ResourceStates.CopyDest, ResourceStates.PixelShaderResource)
        Return texture
    End Function

    Friend Sub DrawTexture(list As ID3D12GraphicsCommandList, resources As List(Of IDisposable), rtv As CpuDescriptorHandle,
                           texture As ID3D12Resource, dest As Drawing.RectangleF, surface As Size, opacity As Single, clip As Drawing.Rectangle,
                           Optional retained As GlyphBatch = Nothing, Optional maskBounds As Drawing.RectangleF = Nothing,
                    Optional radius As Single = 0, Optional corners As Integer = 0, Optional sigma As Single = 0,
                    Optional stepX As Single = 0, Optional stepY As Single = 0, Optional scale As Integer = 1, Optional resolve As Integer = 0, Optional encode As Boolean = False,
                           Optional uv As Drawing.RectangleF = Nothing, Optional pointSample As Boolean = False, Optional glyphInk As Color4? = Nothing, Optional subpixel As Boolean = False, Optional descriptorArena As D3D12_DescriptorArena = Nothing)
        Dim heap As ID3D12DescriptorHeap
        Dim gpu As GpuDescriptorHandle
        If retained Is Nothing AndAlso descriptorArena IsNot Nothing Then
            heap = descriptorArena.Allocate(device, texture, gpu)
        Else
            heap = device.CreateDescriptorHeap(New DescriptorHeapDescription(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView, 1, DescriptorHeapFlags.ShaderVisible))
            Dim owners = If(retained?.Resources, resources)
            owners.Add(heap)
            device.CreateShaderResourceView(texture, Nothing, heap.GetCPUDescriptorHandleForHeapStart())
            gpu = heap.GetGPUDescriptorHandleForHeapStart()
        End If
        If retained IsNot Nothing Then
            retained.Runs.Add((heap, dest, uv, subpixel))
            retained.EstimatedBytes += 65536L + ((CLng(texture.Description.Width) * CLng(texture.Description.Height) * If(texture.Description.Format = Format.R8_UNorm, 1, 4) + 65535) \ 65536) * 65536
        End If
        DrawDescriptor(list, rtv, heap, dest, surface, opacity, clip, maskBounds, radius, corners, sigma, stepX, stepY, uv, scale, resolve, encode, pointSample, texture.Description.Format <> Format.R16G16B16A16_Float, glyphInk, subpixel, gpu)
    End Sub

    Friend NotInheritable Class GlyphBatch
        Implements IDisposable
        Friend ReadOnly Resources As New List(Of IDisposable)()
        Friend ReadOnly Runs As New List(Of (Heap As ID3D12DescriptorHeap, Dest As Drawing.RectangleF, UV As Drawing.RectangleF, Subpixel As Boolean))()
        Friend EstimatedBytes As Long
        Friend Placement As Drawing.Point
        Public Sub Dispose() Implements IDisposable.Dispose
            For Each resource In Resources
                resource.Dispose()
            Next
        End Sub
    End Class

    Friend Sub DrawDescriptor(list As ID3D12GraphicsCommandList, rtv As CpuDescriptorHandle, heap As ID3D12DescriptorHeap,
                              dest As Drawing.RectangleF, surface As Size, opacity As Single, clip As Drawing.Rectangle, Optional maskBounds As Drawing.RectangleF = Nothing,
                              Optional radius As Single = 0, Optional corners As Integer = 0, Optional sigma As Single = 0,
                    Optional stepX As Single = 0, Optional stepY As Single = 0, Optional uv As Drawing.RectangleF = Nothing, Optional scale As Integer = 1, Optional resolve As Integer = 0,
                              Optional encode As Boolean = False, Optional pointSample As Boolean = False, Optional sourceSrgb As Boolean = True, Optional glyphInk As Color4? = Nothing, Optional subpixel As Boolean = False,
                              Optional descriptor As GpuDescriptorHandle = Nothing)
        If uv.IsEmpty Then uv = New Drawing.RectangleF(0, 0, 1, 1)
        Dim ink = glyphInk.GetValueOrDefault()
        If subpixel AndAlso subpixelState Is Nothing Then
            subpixelState = CreatePipelineState(targetFormat, subpixel:=True)
        End If
        list.SetPipelineState(If(subpixel, subpixelState, state))
        list.SetGraphicsRootSignature(signature)
        list.SetDescriptorHeaps(heap)
        list.SetGraphicsRootDescriptorTable(1, If(descriptor.Ptr = 0, heap.GetGPUDescriptorHandleForHeapStart(), descriptor))
        list.SetGraphicsRoot32BitConstants(0, New Single() {dest.Left, dest.Top, dest.Right, dest.Bottom, surface.Width, surface.Height, opacity, radius, maskBounds.Left, maskBounds.Top, maskBounds.Right, maskBounds.Bottom, corners, sigma, stepX, stepY, uv.X, uv.Y, uv.Width, uv.Height, scale, resolve, If(encode, 1, 0), If(pointSample, 1, 0), If(linearTarget AndAlso sourceSrgb, 1, 0),
                                                       If(glyphInk.HasValue, 1, 0), 0, 0, ink.R, ink.G, ink.B, ink.A}, 0)
        list.RSSetViewport(New Viewport(0, 0, surface.Width * scale, surface.Height * scale, 0, 1))
        list.RSSetScissorRect(New Vortice.RawRect(clip.Left * scale, clip.Top * scale, clip.Right * scale, clip.Bottom * scale))
        list.OMSetRenderTargets(rtv, Nothing)
        list.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleStrip)
        list.DrawInstanced(4, 1, 0, 0)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        subpixelState?.Dispose()
        state?.Dispose()
        signature?.Dispose()
    End Sub
End Class
