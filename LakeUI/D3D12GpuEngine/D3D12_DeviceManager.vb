Imports System.Threading
Imports System.Threading.Tasks
Imports Vortice.Direct3D12
Imports Vortice.Direct3D12.Debug
Imports Vortice.DXGI

''' <summary>
''' 独立的原生 D3D12 设备基础设施。此类型不与 D3D11 绘制入口互通，也不表示完整 DX12 后端已可用。
''' </summary>
Friend NotInheritable Class D3D12_DeviceManager
    Implements IDisposable

    Private ReadOnly _syncRoot As New Object()
    Private ReadOnly _requestedAdapterLuid As Nullable(Of Vortice.Luid)
    Private _factory As IDXGIFactory4
    Private _adapter As IDXGIAdapter1
    Private _device As ID3D12Device
    Private _directQueue As ID3D12CommandQueue
    Private _fence As ID3D12Fence
    Private _fenceEvent As EventWaitHandle
    Private _generation As Integer
    Private _nextFenceValue As Long
    Private _disposed As Boolean

    Friend Sub New()
    End Sub

    Friend Sub New(adapterLuid As Vortice.Luid)
        _requestedAdapterLuid = adapterLuid
    End Sub

    Friend ReadOnly Property DeviceGeneration As Integer
        Get
            Return Volatile.Read(_generation)
        End Get
    End Property

    Friend ReadOnly Property AdapterDescription As String
        Get
            EnsureCreated()
            Return _adapter.Description1.Description
        End Get
    End Property

    Friend ReadOnly Property AdapterLuid As Vortice.Luid
        Get
            EnsureCreated()
            Return _adapter.Description1.Luid
        End Get
    End Property

    Friend ReadOnly Property Device As ID3D12Device
        Get
            EnsureCreated()
            Return _device
        End Get
    End Property

    Friend ReadOnly Property DirectQueue As ID3D12CommandQueue
        Get
            EnsureCreated()
            Return _directQueue
        End Get
    End Property

    Friend Sub EnsureCreated()
        If _disposed Then Throw New ObjectDisposedException(NameOf(D3D12_DeviceManager))
        If _device IsNot Nothing Then Return

        SyncLock _syncRoot
            If _device IsNot Nothing Then Return
            If _disposed Then Throw New ObjectDisposedException(NameOf(D3D12_DeviceManager))

            RenderBackendStartup.RequireFactory(RenderBackend.Direct3D12)
            Try
                _factory = Global.Vortice.DXGI.DXGI.CreateDXGIFactory1(Of IDXGIFactory4)()
                If _requestedAdapterLuid.HasValue Then
                    _adapter = _factory.EnumAdapterByLuid(Of IDXGIAdapter1)(_requestedAdapterLuid.Value)
                Else
                    _factory.EnumAdapters1(0, _adapter).CheckError()
                End If

                _device = D3D12.D3D12CreateDevice(Of ID3D12Device)(_adapter, Vortice.Direct3D.FeatureLevel.Level_11_0)
                Dim queueDescription As New CommandQueueDescription(CommandListType.Direct)
                _directQueue = _device.CreateCommandQueue(Of ID3D12CommandQueue)(queueDescription)
                _fence = _device.CreateFence(Of ID3D12Fence)(0UL, FenceFlags.None)
                _fenceEvent = New EventWaitHandle(False, EventResetMode.AutoReset)
                _generation = RenderBackendStartup.NextGeneration()
            Catch ex As Exception
                ReleaseResourcesNoLock()
                Throw
            End Try
        End SyncLock
    End Sub

    Friend ReadOnly Property IsDeviceRemoved As Boolean
        Get
            Return _device IsNot Nothing AndAlso _device.DeviceRemovedReason.Failure
        End Get
    End Property

    Friend Sub CheckDevice()
        If _device IsNot Nothing Then _device.DeviceRemovedReason.CheckError()
    End Sub

    Friend Function Signal() As Long
        EnsureCreated()
        CheckDevice()
        Dim value = Interlocked.Increment(_nextFenceValue)
        _directQueue.Signal(_fence, CULng(value)).CheckError()
        Return value
    End Function

    Friend Function WaitForFence(value As Long, timeout As TimeSpan) As Boolean
        EnsureCreated()
        CheckDevice()
        If value <= 0 OrElse _fence.CompletedValue >= CULng(value) Then Return True
        _fence.SetEventOnCompletion(CULng(value), _fenceEvent.SafeWaitHandle.DangerousGetHandle())
        Dim elapsed = Stopwatch.StartNew()
        While _fence.CompletedValue < CULng(value)
            Dim remaining = timeout - elapsed.Elapsed
            If remaining <= TimeSpan.Zero Then Return False
            If Not _fenceEvent.WaitOne(remaining) Then Return False
            CheckDevice()
        End While
        CheckDevice()
        Return True
    End Function

    Friend Function WaitForIdle(timeout As TimeSpan) As Boolean
        Return WaitForFence(Signal(), timeout)
    End Function

    ' The queue and fence references also keep the retired generation alive after a backend switch.
    Friend Sub RetireAfterGpu(release As Action)
        If _device Is Nothing OrElse _directQueue Is Nothing OrElse _fence Is Nothing OrElse IsDeviceRemoved Then
            release()
            Return
        End If
        Dim value = CULng(Interlocked.Increment(_nextFenceValue))
        Dim signaled = _directQueue.Signal(_fence, value).Success
        If (signaled AndAlso _fence.CompletedValue >= value) OrElse IsDeviceRemoved Then
            release()
            Return
        End If
        Dim device = _device.QueryInterface(Of ID3D12Device)()
        Dim queue = _directQueue.QueryInterface(Of ID3D12CommandQueue)()
        ' If the normal signal failed, retry on an independent fence so a late signal cannot move the frame fence backwards.
        Dim fence = If(signaled, _fence.QueryInterface(Of ID3D12Fence)(), device.CreateFence(Of ID3D12Fence)(0, FenceFlags.None))
        Dim retirement As Task = Task.Run(Async Function() As Task
                     Using heldDevice = device, heldQueue = queue, heldFence = fence
                         While Not heldDevice.DeviceRemovedReason.Failure
                             If Not signaled Then signaled = heldQueue.Signal(heldFence, value).Success
                             If signaled AndAlso heldFence.CompletedValue >= value Then Exit While
                             Await Task.Delay(25).ConfigureAwait(False)
                         End While
                         release()
                     End Using
                 End Function)
    End Sub

    Private shapes As D3D12_RectanglePipeline
    Private images As D3D12_ImagePipeline

    Private floatShapes As D3D12_RectanglePipeline
    Private floatImages As D3D12_ImagePipeline

    Friend Function GetShapes(Optional floatTarget As Boolean = False) As D3D12_RectanglePipeline
        If floatTarget Then
            If floatShapes Is Nothing Then floatShapes = New D3D12_RectanglePipeline(Device, Format.R16G16B16A16_Float)
            Return floatShapes
        End If
        If shapes Is Nothing Then shapes = New D3D12_RectanglePipeline(Device)
        Return shapes
    End Function

    Friend Function GetImages(Optional floatTarget As Boolean = False) As D3D12_ImagePipeline
        If floatTarget Then
            If floatImages Is Nothing Then floatImages = New D3D12_ImagePipeline(Device, Format.R16G16B16A16_Float)
            Return floatImages
        End If
        If images Is Nothing Then images = New D3D12_ImagePipeline(Device)
        Return images
    End Function

    Private sharedBackdropKey As Object
    Private sharedBackdrop As ID3D12Resource

    Friend Function AcquireBackdrop(key As Object) As ID3D12Resource
        If Not Equals(sharedBackdropKey, key) Then Return Nothing
        Return sharedBackdrop?.QueryInterface(Of ID3D12Resource)()
    End Function

    ' Publish only after execution on the shared DIRECT queue. Every consumer owns a fence-held reference.
    Friend Sub CacheSubmittedBackdrop(key As Object, texture As ID3D12Resource)
        If key Is Nothing OrElse Equals(sharedBackdropKey, key) Then Return
        Dim replacement = texture.QueryInterface(Of ID3D12Resource)()
        sharedBackdrop?.Dispose()
        sharedBackdrop = replacement
        sharedBackdropKey = key
    End Sub

    Private Sub ReleaseResourcesNoLock()
        Dim retired As IDisposable() = {floatShapes, floatImages, shapes, images, sharedBackdrop}
        RetireAfterGpu(Sub()
                           For Each resource In retired
                               resource?.Dispose()
                           Next
                       End Sub)
        floatShapes = Nothing
        floatImages = Nothing
        shapes = Nothing
        images = Nothing
        sharedBackdrop = Nothing
        sharedBackdropKey = Nothing
        _fenceEvent?.Dispose()
        _fenceEvent = Nothing
        _fence?.Dispose()
        _fence = Nothing
        _directQueue?.Dispose()
        _directQueue = Nothing
        _device?.Dispose()
        _device = Nothing
        _adapter?.Dispose()
        _adapter = Nothing
        _factory?.Dispose()
        _factory = Nothing
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        SyncLock _syncRoot
            If _disposed Then Return
            _disposed = True
            ReleaseResourcesNoLock()
        End SyncLock
        GC.SuppressFinalize(Me)
    End Sub
End Class
