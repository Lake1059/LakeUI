Imports System.Numerics
Imports System.Runtime.InteropServices
Imports Vortice.Direct2D1
Imports Vortice.DXGI

''' <summary>
''' V5 每控件 HWND 翻转模型呈现器。Present(0) 不在 UI 动画线程等待垂直同步，
''' DWM 负责最终合成；交换链和绘制目标随 HWND、尺寸及设备代次重建。
''' </summary>
Friend NotInheritable Class D3D_HwndSwapChainPresenter
    Implements IDisposable, D3D_IRenderCacheOwner

    Private Const BufferCount As UInteger = 2UI
    Private ReadOnly _owner As Control
    Private ReadOnly _deviceManager As D3D_DeviceManager
    Private _handle As IntPtr
    Private _size As Size
    Private _generation As Integer = -1
    Private _context As ID2D1DeviceContext
    Private _swapChain As IDXGISwapChain1
    Private _swapChain2 As IDXGISwapChain2
    Private _frameLatencyWaitable As IntPtr
    Private _target As ID2D1Bitmap1
    Private _presenting As Boolean
    Private _lastUsed As Long
    Private _presentedSurfaceRevision As Long
    Private _disposed As Boolean

    Friend Sub New(owner As Control, deviceManager As D3D_DeviceManager)
        _owner = owner
        _deviceManager = deviceManager
        D3D_GpuCache.Register(Me)
    End Sub

    Private ReadOnly Property CacheBytes As Long Implements D3D_IRenderCacheOwner.CacheBytes
        Get
            If _size.Width <= 0 OrElse _size.Height <= 0 Then Return 0
            ' 翻转模型拥有两个后备缓冲；D2D 目标是其上的视图，
            ' one of them and is therefore not counted a second time.
            Return CLng(_size.Width) * CLng(_size.Height) * 4L * BufferCount
        End Get
    End Property

    Private ReadOnly Property OldestUseTick As Long Implements D3D_IRenderCacheOwner.OldestUseTick
        Get
            If _presenting OrElse CacheBytes <= 0 Then Return Long.MaxValue
            ' 可见交换链是最终显示工作集，只参与总量计量，不作为缓存主动淘汰。
            If _owner IsNot Nothing AndAlso Not _owner.IsDisposed AndAlso _owner.Visible Then Return Long.MaxValue
            Return If(_lastUsed <= 0, Long.MaxValue - 1, _lastUsed)
        End Get
    End Property

    Private Function TrimOldest() As Boolean Implements D3D_IRenderCacheOwner.TrimOldest
        If _presenting OrElse CacheBytes <= 0 Then Return False
        If _owner IsNot Nothing AndAlso Not _owner.IsDisposed AndAlso _owner.Visible Then Return False
        释放设备资源()
        Return True
    End Function

    Private Sub ReleaseAllBudgeted() Implements D3D_IRenderCacheOwner.ReleaseAll
        If Not _presenting Then 释放设备资源()
    End Sub

    Friend Function Present(surface As D3D_ControlSurface) As Boolean
        If _disposed OrElse surface Is Nothing OrElse surface.Bitmap Is Nothing Then Return False
        If _owner Is Nothing OrElse _owner.IsDisposed OrElse Not _owner.IsHandleCreated OrElse Not _owner.Visible Then Return False
        If _owner.ClientSize.Width <= 0 OrElse _owner.ClientSize.Height <= 0 Then Return False

        确保资源()
        If _context Is Nothing OrElse _target Is Nothing OrElse _swapChain Is Nothing Then Return False
        If D3D_RenderCore.V5FrameLatencySchedulerEnabled AndAlso _frameLatencyWaitable <> IntPtr.Zero Then
            If 等待单个对象(_frameLatencyWaitable, 0UI) <> WaitObject0 Then
                D3D_RenderDiagnostics.V5FrameLatencySkip()
                Return False
            End If
        End If

        Dim presented As Boolean
        Try
            _presenting = True
            _context.Target = _target
            _context.Transform = Matrix3x2.Identity
            _context.AntialiasMode = AntialiasMode.PerPrimitive
            _context.BeginDraw()
            Try
                _context.Clear(New Vortice.Mathematics.Color4(0, 0, 0, 1))
                Dim 目标区域 As New Vortice.RawRectF(0, 0, _size.Width, _size.Height)
                Dim 来源区域 As New Vortice.RawRectF(
                    0,
                    0,
                    surface.LogicalSize.Width * surface.SampleScale,
                    surface.LogicalSize.Height * surface.SampleScale)
                ' Preserve the highest-quality scaler for every presentation,
                ' including 1:1 surfaces; visual quality takes precedence here.
                _context.DrawBitmap(surface.Bitmap, 目标区域, 1.0F, InterpolationMode.HighQualityCubic, 来源区域, Nothing)
                _context.EndDraw()
            Catch
                Try : _context.EndDraw() : Catch : End Try
                Throw
            Finally
                _context.Target = Nothing
            End Try

            _swapChain.Present(0UI, PresentFlags.None)
            _presentedSurfaceRevision = surface.Revision
            _lastUsed = D3D_GpuCache.NextTick()
            presented = True
        Finally
            _presenting = False
        End Try

        ' V3 约束：Present 只提交当前交换链，不执行进程级缓存维护。
        ' 全局 owner 扫描/COM 释放必须由资源新增或显式清理入口触发；将其放在
        ' 动画 Present 热路径会把一次偶发的 LRU 扫描放大成周期性 UI 卡顿。
        Return presented
    End Function

    Friend Function HasPresented(surface As D3D_ControlSurface) As Boolean
        Return Not _disposed AndAlso surface IsNot Nothing AndAlso surface.Bitmap IsNot Nothing AndAlso
               _swapChain IsNot Nothing AndAlso _target IsNot Nothing AndAlso
               _generation = surface.DeviceGeneration AndAlso
               _presentedSurfaceRevision = surface.Revision
    End Function

    Private Sub 确保资源()
        _deviceManager.EnsureCreated()
        Dim 设备代次 = _deviceManager.DeviceGeneration
        Dim 窗口句柄 = _owner.Handle
        Dim 目标尺寸 = New Size(Math.Max(1, _owner.ClientSize.Width), Math.Max(1, _owner.ClientSize.Height))

        If 设备代次 <> _generation OrElse 窗口句柄 <> _handle Then
            释放设备资源()
            创建交换链(窗口句柄, 目标尺寸, 设备代次)
            Return
        End If
        If 目标尺寸 = _size Then Return

        _context.Target = Nothing
        安全释放(_target)
        _target = Nothing
        _swapChain.ResizeBuffers(BufferCount, CUInt(目标尺寸.Width), CUInt(目标尺寸.Height), Format.B8G8R8A8_UNorm, SwapChainFlags.None)
        _size = 目标尺寸
        创建绘制目标()
    End Sub

    Private Sub 创建交换链(窗口句柄 As IntPtr, 目标尺寸 As Size, 设备代次 As Integer)
        Try
            _context = _deviceManager.CreateDeviceContext()
            Dim 交换链标志 As SwapChainFlags = If(D3D_RenderCore.V5FrameLatencySchedulerEnabled,
                                                 SwapChainFlags.FrameLatencyWaitableObject,
                                                 SwapChainFlags.None)
            Dim 交换链描述 As New SwapChainDescription1(
                CUInt(目标尺寸.Width),
                CUInt(目标尺寸.Height),
                Format.B8G8R8A8_UNorm,
                False,
                Usage.RenderTargetOutput,
                BufferCount,
                Scaling.Stretch,
                SwapEffect.FlipDiscard,
                AlphaMode.Ignore,
                交换链标志)
            _swapChain = _deviceManager.DXGIFactory.CreateSwapChainForHwnd(
                _deviceManager.D3DDevice,
                窗口句柄,
                交换链描述,
                Nothing,
                Nothing)
            _handle = 窗口句柄
            _size = 目标尺寸
            _generation = 设备代次
            If D3D_RenderCore.V5FrameLatencySchedulerEnabled Then
                Try
                    _swapChain2 = _swapChain.QueryInterface(Of IDXGISwapChain2)()
                    _swapChain2.MaximumFrameLatency = 2UI
                    _frameLatencyWaitable = _swapChain2.FrameLatencyWaitableObject
                Catch
                    安全释放(_swapChain2)
                    _swapChain2 = Nothing
                    _frameLatencyWaitable = IntPtr.Zero
                End Try
            End If
            Try
                _deviceManager.DXGIFactory.MakeWindowAssociation(窗口句柄, WindowAssociationFlags.IgnoreAltEnter)
            Catch
                ' 子 HWND 共用顶层窗口关联时 DXGI 允许这里失败。
            End Try
            创建绘制目标()
            D3D_RenderDiagnostics.V5PresenterRecreate()
        Catch
            释放设备资源()
            Throw
        End Try
    End Sub

    Private Sub 创建绘制目标()
        Using 交换链表面 = _swapChain.GetBuffer(Of IDXGISurface)(0)
            Dim 位图属性 As New BitmapProperties1(
                New Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Ignore),
                96.0F,
                96.0F,
                BitmapOptions.Target Or BitmapOptions.CannotDraw)
            _target = _context.CreateBitmapFromDxgiSurface(交换链表面, 位图属性)
        End Using
    End Sub

    Friend Sub HandleDeviceLost()
        释放设备资源()
    End Sub

    Private Sub 释放设备资源()
        If _context IsNot Nothing Then _context.Target = Nothing
        安全释放(_target)
        安全释放(_swapChain2)
        安全释放(_swapChain)
        安全释放(_context)
        _target = Nothing
        _swapChain = Nothing
        _swapChain2 = Nothing
        _frameLatencyWaitable = IntPtr.Zero
        _context = Nothing
        _handle = IntPtr.Zero
        _size = Size.Empty
        _generation = -1
        _presentedSurfaceRevision = 0
    End Sub

    Private Shared Sub 安全释放(资源 As IDisposable)
        If 资源 Is Nothing Then Return
        Try : 资源.Dispose() : Catch : End Try
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        释放设备资源()
    End Sub

    Private Const WaitObject0 As UInteger = 0UI

    <DllImport("kernel32.dll", EntryPoint:="WaitForSingleObject", SetLastError:=True)>
    Private Shared Function 等待单个对象(句柄 As IntPtr, 毫秒数 As UInteger) As UInteger
    End Function
End Class
