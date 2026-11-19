Imports Vortice.Direct3D12
Imports Vortice.DXGI

' A frame slot is recycled only after its fence. All packed runs share one texture and SRV.
Friend NotInheritable Class D3D12_GlyphPage
    Implements IDisposable
    Friend Const GrayWidth As Integer = 2048
    Friend ReadOnly Subpixel As Boolean
    Private ReadOnly pageWidth As Integer
    Friend Const InitialHeight As Integer = 128
    Friend Const MaxHeight As Integer = 512
    Friend ReadOnly Height As Integer
    Friend Const SubpixelWidth As Integer = 512
    Private ReadOnly bytesPerPixel As Integer
    Private ReadOnly rowPitch As Integer
    Private texture As ID3D12Resource
    Private staging As ID3D12Resource
    Friend ReadOnly Heap As ID3D12DescriptorHeap
    Private x, y, row As Integer
    Private footprint As PlacedSubresourceFootPrint

    Friend Sub New(device As ID3D12Device, subpixel As Boolean, pageHeight As Integer)
        Me.Subpixel = subpixel
        Me.Height = pageHeight
        pageWidth = If(subpixel, SubpixelWidth, GrayWidth)
        bytesPerPixel = If(subpixel, 4, 1)
        rowPitch = pageWidth * bytesPerPixel
        Dim format = If(subpixel, Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DXGI.Format.R8_UNorm)
        Try
            Dim desc = ResourceDescription.Texture2D(format, CUInt(pageWidth), CUInt(Height), 1, 1)
            texture = device.CreateCommittedResource(HeapType.Default, desc, ResourceStates.PixelShaderResource)
            staging = device.CreateCommittedResource(HeapType.Upload, ResourceDescription.Buffer(CULng(rowPitch * Me.Height)), ResourceStates.GenericRead)
            footprint.Footprint = New SubresourceFootPrint With {.Format = format, .Width = CUInt(pageWidth), .Height = CUInt(Height), .Depth = 1, .RowPitch = CUInt(rowPitch)}
            Heap = device.CreateDescriptorHeap(New DescriptorHeapDescription(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView, 1, DescriptorHeapFlags.ShaderVisible))
            device.CreateShaderResourceView(texture, Nothing, Heap.GetCPUDescriptorHandleForHeapStart())
        Catch
            Dispose()
            Throw
        End Try
    End Sub

    Friend Function Pack(list As ID3D12GraphicsCommandList, width As Integer, height As Integer, pixels As Byte(), ByRef uv As Drawing.RectangleF) As Boolean
        If width > pageWidth OrElse height > Me.Height Then Return False
        If x + width > pageWidth Then
            x = 0
            y += row
            row = 0
        End If
        If y + height > Me.Height Then Return False
        Dim mapped = staging.Map(Of Byte)(0, rowPitch * Me.Height)
        Try
            Dim sourcePitch = width * bytesPerPixel
            For j = 0 To height - 1
                pixels.AsSpan(j * sourcePitch, sourcePitch).CopyTo(mapped.Slice((y + j) * rowPitch + x * bytesPerPixel))
            Next
        Finally
            staging.Unmap(0)
        End Try
        list.ResourceBarrierTransition(texture, ResourceStates.PixelShaderResource, ResourceStates.CopyDest)
        Dim box As New Vortice.Mathematics.Box(x, y, 0, x + width, y + height, 1)
        list.CopyTextureRegion(New TextureCopyLocation(texture, 0UI), CUInt(x), CUInt(y), 0, New TextureCopyLocation(staging, footprint), box)
        list.ResourceBarrierTransition(texture, ResourceStates.CopyDest, ResourceStates.PixelShaderResource)
        uv = New Drawing.RectangleF(x / CSng(pageWidth), y / CSng(Me.Height), width / CSng(pageWidth), height / CSng(Me.Height))
        x += width
        row = Math.Max(row, height)
        Return True
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        Heap?.Dispose()
        staging?.Dispose()
        texture?.Dispose()
    End Sub
End Class
