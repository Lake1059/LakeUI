Imports Vortice.Direct3D12

''' <summary>Frame-owned upload memory. Reset is legal only after the frame's fence completes.</summary>
Friend NotInheritable Class D3D12_UploadArena
    Implements IDisposable

    Private Const PageSize As Integer = 128 * 1024
    Private Const MaxPages As Integer = 4
    Private ReadOnly pages As New List(Of ID3D12Resource)()
    Private pageIndex As Integer
    Private cursor As Integer

    Friend Structure Allocation
        Public ReadOnly Resource As ID3D12Resource
        Public ReadOnly Offset As Integer

        Public Sub New(resource As ID3D12Resource, offset As Integer)
            Me.Resource = resource
            Me.Offset = offset
        End Sub
    End Structure

    Friend Sub Reset()
        pageIndex = 0
        cursor = 0
    End Sub

    Friend Function Allocate(device As ID3D12Device, size As Integer, alignment As Integer, overflow As List(Of IDisposable)) As Allocation
        If size <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(size))
        If alignment <= 0 OrElse (alignment And (alignment - 1)) <> 0 Then Throw New ArgumentOutOfRangeException(NameOf(alignment))
        If size <= PageSize Then
            Dim start = (cursor + alignment - 1) And Not (alignment - 1)
            If start + size > PageSize Then
                pageIndex += 1
                start = 0
                cursor = 0
            End If
            If pageIndex < MaxPages Then
                If pageIndex = pages.Count Then
                    pages.Add(CreateUpload(device, PageSize))
                End If
                cursor = start + size
                Return New Allocation(pages(pageIndex), start)
            End If
        End If
        Dim dedicated = CreateUpload(device, size)
        overflow.Add(dedicated)
        Return New Allocation(dedicated, 0)
    End Function

    Private Shared Function CreateUpload(device As ID3D12Device, bytes As Integer) As ID3D12Resource
        Return device.CreateCommittedResource(HeapType.Upload, ResourceDescription.Buffer(CULng(bytes)), ResourceStates.GenericRead)
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        For Each page In pages
            page.Dispose()
        Next
        pages.Clear()
    End Sub
End Class
