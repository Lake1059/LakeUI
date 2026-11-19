Imports Vortice.Direct3D12

''' <summary>Shader-visible SRV slots owned by one command allocator and reused behind its fence.</summary>
Friend NotInheritable Class D3D12_DescriptorArena
    Implements IDisposable

    Private Const PageSize As Integer = 64
    Private Const RetainedPages As Integer = 2
    Private Const HeapType As DescriptorHeapType = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView
    Private ReadOnly pages As New List(Of ID3D12DescriptorHeap)()
    Private slotIndex As Integer
    Private descriptorIncrement As UInteger

    Friend Sub Reset()
        For i = pages.Count - 1 To RetainedPages Step -1
            pages(i).Dispose()
            pages.RemoveAt(i)
        Next
        slotIndex = 0
    End Sub

    Friend Function Allocate(device As ID3D12Device, texture As ID3D12Resource, ByRef gpu As GpuDescriptorHandle) As ID3D12DescriptorHeap
        Dim pageIndex = slotIndex \ PageSize
        Dim offset = CULng(slotIndex Mod PageSize) * descriptorIncrement
        If pageIndex = pages.Count Then
            pages.Add(device.CreateDescriptorHeap(New DescriptorHeapDescription(HeapType, PageSize, DescriptorHeapFlags.ShaderVisible)))
            If descriptorIncrement = 0 Then descriptorIncrement = device.GetDescriptorHandleIncrementSize(HeapType)
        End If
        slotIndex += 1
        Dim heap = pages(pageIndex)
        Dim cpu = heap.GetCPUDescriptorHandleForHeapStart()
        cpu.Ptr = New UIntPtr(cpu.Ptr.ToUInt64() + offset)
        gpu = heap.GetGPUDescriptorHandleForHeapStart()
        gpu.Ptr += offset
        device.CreateShaderResourceView(texture, Nothing, cpu)
        Return heap
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        For Each heap In pages
            heap.Dispose()
        Next
        pages.Clear()
    End Sub
End Class
