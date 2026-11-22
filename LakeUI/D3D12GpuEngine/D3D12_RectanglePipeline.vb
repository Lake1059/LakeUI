Imports System.Numerics
Imports Vortice.Direct3D12
Imports Vortice.DXGI
Imports Vortice.Mathematics

' Keep these values in sync with Geometry.hlsl.
Friend Enum D3D12_GeometryKind
    Legacy = 0
    RoundedRectangle = 1
    Ellipse = 2
    FlatLine = 3
    RoundLine = 4
    JoinedRoundLines = 5
End Enum

''' <summary>Native D3D12 triangle pipeline for solid-color rectangle geometry.</summary>
Friend NotInheritable Class D3D12_RectanglePipeline
    Implements IDisposable

    Private Const VertexStride As Integer = 12
    Private ReadOnly _device As ID3D12Device
    Private ReadOnly targetFormat As Format
    Private _rootSignature As ID3D12RootSignature
    Private _pipelineState As ID3D12PipelineState
    Private _disposed As Boolean

    Friend Sub New(device As ID3D12Device, Optional targetFormat As Format = Format.B8G8R8A8_UNorm)
        Me.targetFormat = targetFormat
        If device Is Nothing Then Throw New ArgumentNullException(NameOf(device))
        _device = device
        Try
            CreatePipeline()
        Catch
            _pipelineState?.Dispose()
            _rootSignature?.Dispose()
            _pipelineState = Nothing
            _rootSignature = Nothing
            Throw
        End Try
    End Sub

    Private Sub CreatePipeline()
        Dim rootParameters As RootParameter() = {
            New RootParameter(New RootConstants(0, 0, 36), ShaderVisibility.Pixel)
        }
        Dim rootDescription As New RootSignatureDescription(RootSignatureFlags.AllowInputAssemblerInputLayout,
            rootParameters, Array.Empty(Of StaticSamplerDescription)())
        Dim rootBlob As Vortice.Direct3D.Blob = Nothing
        Dim serializationError = D3D12.D3D12SerializeVersionedRootSignature(
            New VersionedRootSignatureDescription(rootDescription), rootBlob)
        If Not String.IsNullOrWhiteSpace(serializationError) Then
            rootBlob?.Dispose()
            Throw New InvalidOperationException(serializationError)
        End If
        Try
            _rootSignature = _device.CreateRootSignature(0UI, rootBlob)
        Finally
            rootBlob?.Dispose()
        End Try

        Dim description As New GraphicsPipelineStateDescription With {
            .RootSignature = _rootSignature,
            .VertexShader = New ReadOnlyMemory(Of Byte)(D3D12ShaderCompiler.Load("Geometry.vs")),
            .PixelShader = New ReadOnlyMemory(Of Byte)(D3D12ShaderCompiler.Load("Geometry.ps")),
            .BlendState = BlendDescription.AlphaBlend,
            .SampleMask = UInteger.MaxValue,
            .RasterizerState = RasterizerDescription.CullNone,
            .DepthStencilState = DepthStencilDescription.None,
            .InputLayout = New InputLayoutDescription({
                New InputElementDescription("POSITION", 0, Format.R32G32_Float, 0, 0),
                New InputElementDescription("COVERAGE", 0, Format.R32_Float, offset:=8, slot:=0)
            }),
            .PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
            .RenderTargetFormats = New Format() {targetFormat},
            .DepthStencilFormat = Format.Unknown,
            .SampleDescription = New Vortice.DXGI.SampleDescription(1, 0)
        }
        _pipelineState = _device.CreateGraphicsPipelineState(Of ID3D12PipelineState)(description)
    End Sub

    Private unitQuad As ID3D12Resource

    ' Bounds are already enforced by the scissor and the pixel shader.
    Friend Function RectangleVertices(ByRef vertexCount As Integer) As VertexBufferView
        vertexCount = 6
        If unitQuad Is Nothing Then
            Dim bytes = EncodeTriangleVertices(New Size(1, 1), {
                New PointF(0, 0), New PointF(1, 0), New PointF(0, 1),
                New PointF(0, 1), New PointF(1, 0), New PointF(1, 1)}, vertexCount, Nothing)
            Dim resource = _device.CreateCommittedResource(HeapType.Upload, ResourceDescription.Buffer(CULng(bytes.Length)), ResourceStates.GenericRead)
            Try
                WriteVertices(resource, 0, bytes)
                unitQuad = resource
            Catch
                resource.Dispose()
                Throw
            End Try
        End If
        Return New VertexBufferView(unitQuad.GPUVirtualAddress, CUInt(6 * VertexStride), VertexStride)
    End Function

    Friend Function UploadTriangles(size As Size, points As PointF(), ByRef vertexCount As Integer, coverage As Single(),
                                    arena As D3D12_UploadArena, overflow As List(Of IDisposable)) As VertexBufferView
        Dim bytes = EncodeTriangleVertices(size, points, vertexCount, coverage)
        Dim allocation = arena.Allocate(_device, bytes.Length, 16, overflow)
        WriteVertices(allocation.Resource, allocation.Offset, bytes)
        Return New VertexBufferView(allocation.Resource.GPUVirtualAddress + CULng(allocation.Offset), CUInt(bytes.Length), VertexStride)
    End Function

    Private Function EncodeTriangleVertices(size As Size, points As PointF(), ByRef vertexCount As Integer, coverage As Single()) As Byte()
        If _disposed Then Throw New ObjectDisposedException(NameOf(D3D12_RectanglePipeline))
        If points Is Nothing OrElse points.Length < 3 OrElse points.Length Mod 3 <> 0 Then
            Throw New ArgumentException("Triangle vertex data must contain one or more complete triangles.", NameOf(points))
        End If
        If coverage IsNot Nothing AndAlso coverage.Length <> points.Length Then
            Throw New ArgumentException("Coverage must contain one value per triangle vertex.", NameOf(coverage))
        End If
        vertexCount = points.Length

        Dim vertices(vertexCount * 3 - 1) As Single
        For i = 0 To points.Length - 1
            vertices(i * 3) = points(i).X / CSng(size.Width) * 2.0F - 1.0F
            vertices(i * 3 + 1) = 1.0F - points(i).Y / CSng(size.Height) * 2.0F
            vertices(i * 3 + 2) = If(coverage Is Nothing, 1.0F, Math.Clamp(coverage(i), 0.0F, 1.0F))
        Next
        Dim vertexBytes(vertices.Length * 4 - 1) As Byte
        Buffer.BlockCopy(vertices, 0, vertexBytes, 0, vertexBytes.Length)
        Return vertexBytes
    End Function

    Private Shared Sub WriteVertices(upload As ID3D12Resource, offset As Integer, bytes As Byte())
        Dim mapped = upload.Map(Of Byte)(0, offset + bytes.Length)
        Try
            bytes.AsSpan().CopyTo(mapped.Slice(offset))
        Finally
            upload.Unmap(0, New Range(CUInt(offset), CUInt(offset + bytes.Length)))
        End Try
    End Sub

    Friend Sub RecordDraw(commandList As ID3D12GraphicsCommandList,
                          vertexBuffer As VertexBufferView,
                          vertexCount As Integer,
                          size As Size,
                          scissor As Rectangle,
                          shapeBounds As RectangleF,
                          rtv As CpuDescriptorHandle,
                          startColor As Color4,
                          endColor As Color4,
                          gradientTop As Single,
                          gradientHeight As Single,
                          cornerRadius As Single,
                          strokeWidth As Single,
                          strokeMode As Boolean, cornerMask As Integer, horizontal As Boolean, maskRadius As Single,
                          maskBox As RectangleF, arc As Vector4, scale As Integer, kind As D3D12_GeometryKind,
                          linePoints As Vector4, lineJoin As Vector2)
        If _disposed Then Throw New ObjectDisposedException(NameOf(D3D12_RectanglePipeline))
        If commandList Is Nothing Then Throw New ArgumentNullException(NameOf(commandList))
        commandList.SetGraphicsRootSignature(_rootSignature)
        commandList.SetPipelineState(_pipelineState)
        commandList.SetGraphicsRoot32BitConstants(0, New Single() {
            startColor.R, startColor.G, startColor.B, startColor.A,
            endColor.R, endColor.G, endColor.B, endColor.A,
            gradientTop, gradientHeight,
            shapeBounds.Left, shapeBounds.Top, shapeBounds.Width, shapeBounds.Height, cornerRadius,
            strokeWidth, If(strokeMode, 1.0F, 0.0F), cornerMask, If(horizontal, 1.0F, 0.0F), maskRadius,
            maskBox.Left, maskBox.Top, maskBox.Right, maskBox.Bottom, arc.X, arc.Y, arc.Z, arc.W, scale,
            CSng(kind), linePoints.X, linePoints.Y, linePoints.Z, linePoints.W, lineJoin.X, lineJoin.Y
        }, 0)
        commandList.RSSetViewports(New Viewport() {New Viewport(0, 0, size.Width * scale, size.Height * scale, 0, 1)})
        commandList.RSSetScissorRects(New Vortice.RawRect() {
            New Vortice.RawRect(scissor.Left * scale, scissor.Top * scale, scissor.Right * scale, scissor.Bottom * scale)
        })
        commandList.OMSetRenderTargets(rtv, Nothing)
        commandList.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList)
        commandList.IASetVertexBuffers(0, New VertexBufferView() {vertexBuffer})
        commandList.DrawInstanced(CUInt(vertexCount), 1, 0, 0)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        unitQuad?.Dispose()
        _pipelineState?.Dispose()
        _pipelineState = Nothing
        _rootSignature?.Dispose()
        _rootSignature = Nothing
    End Sub
End Class
