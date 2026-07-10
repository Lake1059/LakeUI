Imports System.Threading
Imports Vortice.Direct2D1
Imports Vortice.Direct3D11
Imports Vortice.DirectWrite
Imports Vortice.DXGI

''' <summary>
''' D3D_DeviceManager 是新 D3D11 GPU 核心的进程级设备所有者。
''' 它负责创建并持有同一条 GPU 路线上的 D3D11 device、DXGI device、D2D1 device、DWrite factory 和 DXGI factory。
''' 它不负责窗口 swapchain、控件绘制、旧 DC RenderTarget、HDC 输出或任何 WARP/CPU 回退。
''' <para>
''' 资源生命周期：本类拥有进程级 device/factory；窗口级 target、bitmap、brush、geometry、text layer 由各自的 D3D_ 缓存或 D3D_WindowCompositor 持有。
''' 所有跨帧 GPU 资源都必须记录 <see cref="DeviceGeneration"/>，generation 改变后必须丢弃并重建。
''' generation 会在首次创建设备、设备显式失效和重建设备时前进；这样驱动更新、TDR、休眠恢复等场景下，
''' 即使下一帧尚未成功重建设备，窗口级缓存也能立刻判断自己已经过期。
''' </para>
''' <para>
''' 线程边界：创建设备使用内部锁保护；D2D device context 只能在 UI 线程绘制调用中使用。
''' 后续控件迁移时不得直接使用 Graphics.GetHdc、不得自建 D3D device，也不得绕过 D3D_WindowCompositor 获取绘制入口。
''' </para>
''' <para>
''' 设备丢失边界：驱动更新、TDR、显示适配器重置、休眠恢复、远程桌面切换等都按同一套 device lost 流程处理。
''' 本类只释放进程级资源并广播失效；窗口级资源由各自 compositor 在 UI 线程释放，随后按需重建。
''' 窗口级 swapchain 和 DirectComposition 宿主路线已移除；当前只通过 per-control paint scope 回贴到 WinForms HDC。
''' </para>
''' </summary>
Public NotInheritable Class D3D_DeviceManager
    Implements IDisposable

    Private ReadOnly _syncRoot As New Object()
    Private _initialized As Boolean
    Private _disposed As Boolean
    Private _d2dFactory As ID2D1Factory1
    Private _dwriteFactory As IDWriteFactory
    Private _d3dDevice As ID3D11Device
    Private _dxgiDevice As IDXGIDevice
    Private _d2dDevice As ID2D1Device
    Private _dxgiFactory As IDXGIFactory2
    Private _deviceGeneration As Integer

    ''' <summary>
    ''' 设备丢失或冷启动级重置时触发一次。订阅者只能释放自己持有的 GPU 资源，不要在回调中重新进入本类创建设备。
    ''' </summary>
    Public Event DeviceLost As EventHandler

    ''' <summary>
    ''' 当前设备 generation。首次创建设备、设备失效和重建都会递增；跨 generation 的 D3D_/D2D/DXGI 缓存全部视为过期。
    ''' </summary>
    Public ReadOnly Property DeviceGeneration As Integer
        Get
            Return _deviceGeneration
        End Get
    End Property

    Public ReadOnly Property D2DFactory As ID2D1Factory1
        Get
            EnsureCreated()
            Return _d2dFactory
        End Get
    End Property

    Public ReadOnly Property DWriteFactory As IDWriteFactory
        Get
            EnsureCreated()
            Return _dwriteFactory
        End Get
    End Property

    Public ReadOnly Property D3DDevice As ID3D11Device
        Get
            EnsureCreated()
            Return _d3dDevice
        End Get
    End Property

    Public ReadOnly Property DXGIDevice As IDXGIDevice
        Get
            EnsureCreated()
            Return _dxgiDevice
        End Get
    End Property

    Public ReadOnly Property D2DDevice As ID2D1Device
        Get
            EnsureCreated()
            Return _d2dDevice
        End Get
    End Property

    Public ReadOnly Property DXGIFactory As IDXGIFactory2
        Get
            EnsureCreated()
            Return _dxgiFactory
        End Get
    End Property

    ''' <summary>
    ''' 按需创建一个 D2D1.1 DeviceContext。调用方拥有返回对象，并必须在窗口 compositor 或缓存释放时 Dispose。
    ''' </summary>
    Public Function CreateDeviceContext() As ID2D1DeviceContext
        EnsureCreated()
        Try
            Return _d2dDevice.CreateDeviceContext(DeviceContextOptions.None)
        Catch ex As Exception
            If HandleDeviceLost(ex) Then Throw
            Throw
        End Try
    End Function

    ''' <summary>
    ''' 统一处理设备丢失。只有 D2DERR_RECREATE_TARGET / DXGI device removed/reset/hung 等设备级错误会触发失效广播。
    ''' HRESULT 会先按 UInt32 规范化，避免 DXGI 负数 HResult 与十六进制常量比较失败。
    ''' </summary>
    Public Function HandleDeviceLost(ex As Exception) As Boolean
        If Not IsDeviceLostException(ex) Then Return False
        InvalidateDevice()
        Return True
    End Function

    Public Shared Function IsDeviceLostException(ex As Exception) As Boolean
        If ex Is Nothing Then Return False

        Dim hresult = NormalizeHResult(ex.HResult)

        Select Case hresult
            Case &H8899000CUI,  ' D2DERR_RECREATE_TARGET
                 &H88990001UI,  ' D2DERR_WRONG_STATE (device teardown/reinstall may expose it during cleanup)
                 &H887A0005UI,  ' DXGI_ERROR_DEVICE_REMOVED
                 &H887A0006UI,  ' DXGI_ERROR_DEVICE_HUNG
                 &H887A0007UI,  ' DXGI_ERROR_DEVICE_RESET
                 &H887A0002UI,  ' DXGI_ERROR_NOT_FOUND
                 &H887A0020UI,  ' DXGI_ERROR_DRIVER_INTERNAL_ERROR
                 &H887A0026UI   ' DXGI_ERROR_ACCESS_LOST
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Shared Function NormalizeHResult(hresult As Integer) As UInteger
        Return CUInt(CLng(hresult) And 4294967295L)
    End Function

    ''' <summary>
    ''' 主动失效当前设备。调用方应先停止正在进行的 BeginFrame/EndFrame；本方法只广播一次，具体资源释放由持有者负责。
    ''' </summary>
    Public Sub InvalidateDevice()
        Dim hadDevice As Boolean

        SyncLock _syncRoot
            hadDevice = _initialized OrElse _d3dDevice IsNot Nothing OrElse _d2dDevice IsNot Nothing
            ReleaseAllNoLock()
            _initialized = False
            If hadDevice Then _deviceGeneration += 1
        End SyncLock

        If Not hadDevice Then Return

        Try
            RaiseEvent DeviceLost(Me, EventArgs.Empty)
        Catch
            ' 设备丢失广播不能被订阅者异常中断；下一帧会按需重建设备。
        End Try
    End Sub

    ''' <summary>
    ''' 确保进程级 D3D11/D2D/DWrite/DXGI 资源已创建。这里不提供 WARP/CPU 回退，硬件 D3D11 不可用时直接抛出。
    ''' </summary>
    Public Sub EnsureCreated()
        If _disposed Then Throw New ObjectDisposedException(NameOf(D3D_DeviceManager))
        If _initialized Then Return

        SyncLock _syncRoot
            If _initialized Then Return
            EnsureCreatedNoLock()
            _initialized = True
        End SyncLock
    End Sub

    Private Sub EnsureCreatedNoLock()
        Try
            _d2dFactory = D3D_D2DInterop.GetD2DFactory1()
            _dwriteFactory = D3D_D2DInterop.GetDWriteFactory()
            _dxgiFactory = Global.Vortice.DXGI.DXGI.CreateDXGIFactory2(Of IDXGIFactory2)(False)

            Dim flags As DeviceCreationFlags = DeviceCreationFlags.BgraSupport Or DeviceCreationFlags.Singlethreaded
            Dim levels As Vortice.Direct3D.FeatureLevel() = {
                Vortice.Direct3D.FeatureLevel.Level_11_1,
                Vortice.Direct3D.FeatureLevel.Level_11_0,
                Vortice.Direct3D.FeatureLevel.Level_10_1,
                Vortice.Direct3D.FeatureLevel.Level_10_0,
                Vortice.Direct3D.FeatureLevel.Level_9_3,
                Vortice.Direct3D.FeatureLevel.Level_9_2,
                Vortice.Direct3D.FeatureLevel.Level_9_1
            }

            _d3dDevice = D3D11.D3D11CreateDevice(Vortice.Direct3D.DriverType.Hardware, flags, levels)
            _dxgiDevice = _d3dDevice.QueryInterface(Of IDXGIDevice)()
            _d2dDevice = _d2dFactory.CreateDevice(_dxgiDevice)
            _deviceGeneration += 1
        Catch
            ReleaseAllNoLock()
            _initialized = False
            Throw
        End Try
    End Sub

    Private Sub ReleaseAllNoLock()
        SafeDispose(_d2dDevice)
        SafeDispose(_dxgiDevice)
        SafeDispose(_d3dDevice)
        SafeDispose(_dxgiFactory)
        _d2dDevice = Nothing
        _dxgiDevice = Nothing
        _d3dDevice = Nothing
        _dxgiFactory = Nothing
        _dwriteFactory = Nothing
        _d2dFactory = Nothing
    End Sub

    Private Shared Sub SafeDispose(resource As IDisposable)
        If resource Is Nothing Then Return
        Try
            resource.Dispose()
        Catch
        End Try
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        SyncLock _syncRoot
            ReleaseAllNoLock()
            _initialized = False
        End SyncLock
        GC.SuppressFinalize(Me)
    End Sub
End Class
