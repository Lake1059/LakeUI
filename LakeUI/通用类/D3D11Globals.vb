Imports Vortice.Direct2D1
Imports Vortice.Direct3D11
Imports Vortice.DXGI

' 注意：不 Import Vortice.Direct3D 命名空间，避免与 Vortice.Direct2D1.FeatureLevel 重名冲突。
' 文件内统一使用全限定 Vortice.Direct3D.FeatureLevel / Vortice.Direct3D.DriverType。

''' <summary>
''' D3D11 + D2D 1.1 Device 进程级单例（V2 阶段 A 基础设施 / 设备丢失自动重建）。
'''
''' === 设计目标 ===
''' • 进程内只创建一份 <see cref="ID3D11Device"/> + <see cref="ID2D1Device"/>，所有 Form 共享。
''' • 仍保留 V2 现有 <see cref="ID2D1DCRenderTarget"/> 出口不变，本模块只是"额外提供"
'''   <see cref="ID2D1DeviceContext"/> 入口给愿意走 D2D 1.1 effect / 跨 Form bitmap 缓存的路径使用。
''' • 任何创建失败（设计器、RDP 无 D3D11、显卡驱动不支持）一律静默返回 Nothing，
'''   调用方按"无 DeviceContext"路径继续运行，不影响 DC RT 主路径。
'''
''' === 设备丢失（device lost / TDR / 远程桌面切换）处理 ===
''' • D3D11 设备在以下情况会进入"丢失"状态：驱动崩溃 / GPU 重置（TDR）/ 系统休眠唤醒 /
'''   切换到远程桌面会话 / GPU 被强占（独占全屏切换）。
''' • 任何 D2D 调用都可能抛出 HRESULT = D2DERR_RECREATE_TARGET (0x8899000C)、
'''   DXGI_ERROR_DEVICE_REMOVED (0x887A0005)、DXGI_ERROR_DEVICE_RESET (0x887A0007) 等。
''' • <see cref="HandleDeviceLost"/> 提供一个统一入口：传入异常 → 判断是否设备级错误 → 失效全部资源；
'''   <see cref="DeviceLost"/> 事件让 per-form 的 <see cref="WindowCompositor"/> 收到通知，
'''   及时释放与旧设备绑定的 DeviceContext / Bitmap1 等资源；
'''   下一次 <see cref="GetD2DDevice"/> 调用会按需重建一份全新设备。
''' • 上层调用方（例如 <c>ThisIsYourWindow.PaintWindow</c>）应把所有访问 DeviceContext 的代码用 Try 包裹，
'''   Catch 后调用 <see cref="HandleDeviceLost"/> 并降级到 DC RT 路径，等待下一帧自动恢复。
'''
''' === 与 D2DHelper.GetD2DFactory 的关系 ===
''' • <c>D2DHelper.GetD2DFactory()</c> 返回 <see cref="ID2D1Factory"/>，仍按原逻辑创建 DC RT 给现有控件用。
'''   阶段 A 将其内部实例升级为 <see cref="ID2D1Factory1"/>（接口向下兼容），并通过
'''   <see cref="D2DHelper.GetD2DFactory1"/> 暴露给本模块以创建 D2D Device。
''' • 注意：DC RT（factory.CreateDCRenderTarget）走的是 D2D 隐式 D3D10 设备，与本模块的
'''   显式 D3D11 设备并非同一对象，因此 DC RT 与 DeviceContext 在阶段 A 不共享资源
'''   （brush / bitmap 不能互通）。阶段 B 会把 DC RT 改为也由本设备创建以解除该限制。
'''
''' === 线程要求 ===
''' • 单例创建受 _lock 保护；只在 UI 线程使用 DeviceContext。
''' • 创建出的 D3D11Device 使用 SingleThreaded flag，多线程并发访问会触发驱动断言。
''' • <see cref="DeviceLost"/> 事件在调用 <see cref="HandleDeviceLost"/> 或 <see cref="InvalidateDevice"/>
'''   的线程上同步触发，订阅方应假定在 UI 线程；订阅方不应在事件回调里再访问 D3D11Globals 的 D2D 资源。
''' </summary>
Public Module D3D11Globals

    Private ReadOnly _lock As New Object()
    Private _initialized As Boolean
    Private _d3dDevice As ID3D11Device
    Private _dxgiDevice As IDXGIDevice
    Private _d2dDevice As ID2D1Device
    Private _isWarp As Boolean
    Private _deviceGeneration As Integer
    Private _lastCreateFailedTick As Long = Long.MinValue
    Private Const CreateFailureRetryDelayMs As Long = 2000

    ''' <summary>
    ''' 设备丢失或被显式失效时触发。所有持有 per-device 资源（DeviceContext / Bitmap1 / Brush 等）的对象
    ''' 必须订阅该事件并立刻释放对应资源，避免在下次帧上访问已死设备时再次抛错。
    ''' <para>
    ''' 触发线程 = 调用 <see cref="HandleDeviceLost"/> / <see cref="InvalidateDevice"/> 的线程，
    ''' 实际场景下 = UI 线程；订阅方不要在回调里再调用本模块的 Get / Create 方法以避免重入。
    ''' </para>
    ''' </summary>
    Public Event DeviceLost As EventHandler

    ''' <summary>当前 D2D Device 是否由 WARP 软件渲染器承载（RDP / 无 GPU 环境）。</summary>
    Public ReadOnly Property IsWarp As Boolean
        Get
            Return _isWarp
        End Get
    End Property

    ''' <summary>
    ''' 设备代号。每次设备被重建会递增；外部缓存可用它判断"我手里的 DeviceContext 是不是过期了"。
    ''' </summary>
    Public ReadOnly Property DeviceGeneration As Integer
        Get
            Return _deviceGeneration
        End Get
    End Property

    ''' <summary>
    ''' 取（按需创建）进程级 <see cref="ID2D1Device"/>。创建失败返回 <c>Nothing</c>。
    ''' 设备丢失后首次调用会自动重建（计数器 <see cref="DeviceGeneration"/> 增加）。
    ''' </summary>
    Public Function GetD2DDevice() As ID2D1Device
        If _initialized Then Return _d2dDevice
        Dim nowTicks As Long = Environment.TickCount64
        SyncLock _lock
            If _initialized Then Return _d2dDevice
            If _lastCreateFailedTick <> Long.MinValue AndAlso
               nowTicks - _lastCreateFailedTick < CreateFailureRetryDelayMs Then
                Return Nothing
            End If
            Try
                EnsureCreatedNoLock()
            Catch
                ' 静默失败：保留 _d2dDevice = Nothing，所有 GetD2DDevice 调用都返回 Nothing。
                ReleaseAllNoLock()
            End Try
            If _d2dDevice IsNot Nothing Then
                _initialized = True
                _lastCreateFailedTick = Long.MinValue
            Else
                _initialized = False
                _lastCreateFailedTick = Environment.TickCount64
            End If
        End SyncLock
        Return _d2dDevice
    End Function

    ''' <summary>
    ''' 从进程级 D2D Device 创建一个 <see cref="ID2D1DeviceContext"/>。调用方负责 Dispose。
    ''' 设备不可用时返回 <c>Nothing</c>。
    ''' </summary>
    Public Function CreateDeviceContext() As ID2D1DeviceContext
        Dim dev = GetD2DDevice()
        If dev Is Nothing Then Return Nothing
        Try
            Return dev.CreateDeviceContext(DeviceContextOptions.None)
        Catch ex As Exception
            ' 创建 DeviceContext 时也可能踩到设备级错误。
            HandleDeviceLost(ex)
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' 给定一个异常，判断是否属于"设备丢失"类错误。
    ''' 涵盖 D2DERR_RECREATE_TARGET / DXGI_ERROR_DEVICE_REMOVED / _RESET / _HUNG / _NOT_FOUND / _DRIVER_INTERNAL_ERROR。
    ''' </summary>
    Public Function IsDeviceLostException(ex As Exception) As Boolean
        If ex Is Nothing Then Return False
        Dim hr As Integer = ex.HResult
        Select Case hr
            Case &H8899000C   ' D2DERR_RECREATE_TARGET
            Case &H887A0005   ' DXGI_ERROR_DEVICE_REMOVED
            Case &H887A0007   ' DXGI_ERROR_DEVICE_RESET
            Case &H887A0006   ' DXGI_ERROR_DEVICE_HUNG
            Case &H887A0020   ' DXGI_ERROR_DRIVER_INTERNAL_ERROR
            Case &H887A0002   ' DXGI_ERROR_NOT_FOUND（在 device removed 后枚举适配器时常见）
            Case Else
                Return False
        End Select
        Return True
    End Function

    ''' <summary>
    ''' 统一的设备丢失处理入口。<paramref name="ex"/> 是设备级错误时立即失效全部资源并触发 <see cref="DeviceLost"/>；
    ''' 不是设备级错误则不做任何事，原异常应由上层处理（例如继续抛出）。
    ''' 返回 <c>True</c> 表示本次确实是设备丢失，调用方应 swallow 异常并降级到 DC RT 路径，等待下一帧自动恢复。
    ''' </summary>
    Public Function HandleDeviceLost(ex As Exception) As Boolean
        If Not IsDeviceLostException(ex) Then Return False
        InvalidateDevice()
        Return True
    End Function

    ''' <summary>
    ''' 主动失效当前设备（释放 D2D / DXGI / D3D11 对象、触发 <see cref="DeviceLost"/>），
    ''' 下次 <see cref="GetD2DDevice"/> 会重建一份全新设备。
    ''' 调用方应在调用前确保自己不再持有任何对本设备资源的引用，否则后续 Dispose 顺序可能引发异常（已被 try 吞掉）。
    ''' </summary>
    Public Sub InvalidateDevice()
        Dim hadDevice As Boolean
        SyncLock _lock
            hadDevice = _initialized OrElse _d2dDevice IsNot Nothing OrElse
                        _dxgiDevice IsNot Nothing OrElse _d3dDevice IsNot Nothing
            ReleaseAllNoLock()
            _initialized = False
            _lastCreateFailedTick = Long.MinValue
        End SyncLock
        If Not hadDevice Then Return
        Try
            RaiseEvent DeviceLost(Nothing, EventArgs.Empty)
        Catch
            ' 任何订阅方异常都不能阻断设备失效流程。
        End Try
    End Sub

    Private Sub EnsureCreatedNoLock()
        Dim factory1 As ID2D1Factory1 = D2DHelper.GetD2DFactory1()
        If factory1 Is Nothing Then Return

        ' BgraSupport 是 D2D interop 的硬性要求；SingleThreaded 与 D2D Factory 单例策略保持一致。
        Dim flags As DeviceCreationFlags = DeviceCreationFlags.BgraSupport Or DeviceCreationFlags.Singlethreaded
        Dim levels As Vortice.Direct3D.FeatureLevel() = New Vortice.Direct3D.FeatureLevel() {
            Vortice.Direct3D.FeatureLevel.Level_11_1,
            Vortice.Direct3D.FeatureLevel.Level_11_0,
            Vortice.Direct3D.FeatureLevel.Level_10_1,
            Vortice.Direct3D.FeatureLevel.Level_10_0,
            Vortice.Direct3D.FeatureLevel.Level_9_3,
            Vortice.Direct3D.FeatureLevel.Level_9_2,
            Vortice.Direct3D.FeatureLevel.Level_9_1
        }

        Dim hwOk As Boolean = TryCreateDevice(Vortice.Direct3D.DriverType.Hardware, flags, levels)
        If Not hwOk Then
            ' 硬件失败（如 RDP 早期 / 驱动异常）：退回 WARP 软件渲染器，行为可用、性能下降。
            If TryCreateDevice(Vortice.Direct3D.DriverType.Warp, flags, levels) Then
                _isWarp = True
            Else
                Return
            End If
        End If

        _dxgiDevice = _d3dDevice.QueryInterface(Of IDXGIDevice)()
        _d2dDevice = factory1.CreateDevice(_dxgiDevice)
        _deviceGeneration += 1
    End Sub

    Private Function TryCreateDevice(driver As Vortice.Direct3D.DriverType, flags As DeviceCreationFlags, levels As Vortice.Direct3D.FeatureLevel()) As Boolean
        Try
            ' Vortice 提供的便捷重载：返回 ID3D11Device，不输出 ImmediateContext（D2D 不需要它）。
            Dim dev As ID3D11Device = D3D11.D3D11CreateDevice(driver, flags, levels)
            If dev Is Nothing Then Return False
            _d3dDevice = dev
            Return True
        Catch
            Return False
        End Try
    End Function

    Private Sub ReleaseAllNoLock()
        If _d2dDevice IsNot Nothing Then
            Try : _d2dDevice.Dispose() : Catch : End Try
            _d2dDevice = Nothing
        End If
        If _dxgiDevice IsNot Nothing Then
            Try : _dxgiDevice.Dispose() : Catch : End Try
            _dxgiDevice = Nothing
        End If
        If _d3dDevice IsNot Nothing Then
            Try : _d3dDevice.Dispose() : Catch : End Try
            _d3dDevice = Nothing
        End If
        _isWarp = False
    End Sub

End Module
