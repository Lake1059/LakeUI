Imports Vortice.Direct2D1
Imports Vortice.DXGI

''' <summary>
''' D3D_SwapChainHost 拥有绑定 WinForms 顶层 HWND 的 DXGI flip-model swap chain 和对应的 D2D target bitmap。
''' 它负责 CreateSwapChainForHwnd、ResizeBuffers、创建 back buffer 的 ID2D1Bitmap1 target、Present。
''' 它不负责控件遍历、旧 HDC 绘制、DirectComposition visual tree 或任何 CPU backing bitmap。
''' <para>
''' 资源生命周期：swap chain、back buffer target bitmap 由本类持有；D3D_WindowCompositor 在 BeginFrame 时把 DeviceContext.Target 指向该 bitmap。
''' 它绑定 device generation；generation 改变、窗口尺寸改变或 HWND 改变时必须释放并重建。
''' </para>
''' <para>
''' 线程边界：所有方法必须在 UI 线程调用。Commit/Present 只能在 EndDraw 成功后调用。
''' </para>
''' </summary>
Public NotInheritable Class D3D_SwapChainHost
    Implements IDisposable

    Private Const BufferCount As UInteger = 2UI
    Private _swapChain As IDXGISwapChain1
    Private _targetBitmap As ID2D1Bitmap1
    Private _hwnd As IntPtr
    Private _size As Size
    Private _generation As Integer = -1
    Private _disposed As Boolean

    Public ReadOnly Property TargetBitmap As ID2D1Bitmap1
        Get
            Return _targetBitmap
        End Get
    End Property

    Public ReadOnly Property CurrentSize As Size
        Get
            Return _size
        End Get
    End Property

    Public ReadOnly Property DeviceGeneration As Integer
        Get
            Return _generation
        End Get
    End Property

    Public ReadOnly Property HasTarget As Boolean
        Get
            Return _swapChain IsNot Nothing AndAlso _targetBitmap IsNot Nothing
        End Get
    End Property

    ''' <summary>
    ''' 确保 HWND swap chain 和 D2D target bitmap 可用。该方法不会触碰 HDC，也不会读取窗口像素。
    ''' </summary>
    Public Sub EnsureTarget(manager As D3D_DeviceManager, deviceContext As ID2D1DeviceContext, hwnd As IntPtr, clientSize As Size)
        If _disposed Then Throw New ObjectDisposedException(NameOf(D3D_SwapChainHost))
        If manager Is Nothing Then Throw New ArgumentNullException(NameOf(manager))
        If deviceContext Is Nothing Then Throw New ArgumentNullException(NameOf(deviceContext))
        If hwnd = IntPtr.Zero Then Throw New InvalidOperationException("HWND is required for the D3D swap chain.")

        Dim normalizedSize = NormalizeSize(clientSize)
        If _swapChain IsNot Nothing AndAlso
           _targetBitmap IsNot Nothing AndAlso
           _hwnd = hwnd AndAlso
           _generation = manager.DeviceGeneration AndAlso
           _size = normalizedSize Then
            Return
        End If

        If _swapChain Is Nothing OrElse _hwnd <> hwnd OrElse _generation <> manager.DeviceGeneration Then
            ReleaseTargetBitmap()
            ReleaseSwapChain()
            CreateSwapChain(manager, hwnd, normalizedSize)
        ElseIf _size <> normalizedSize Then
            ResizeTarget(normalizedSize)
        End If

        CreateD2DTarget(deviceContext)
    End Sub

    ''' <summary>
    ''' 调整 swap chain back buffer。调用方必须保证当前没有 BeginFrame 正在绘制，且 DeviceContext.Target 已解绑。
    ''' </summary>
    Public Sub ResizeTarget(clientSize As Size)
        If _swapChain Is Nothing Then Return

        Dim normalizedSize = NormalizeSize(clientSize)
        If _size = normalizedSize AndAlso _targetBitmap IsNot Nothing Then Return

        ReleaseTargetBitmap()
        _swapChain.ResizeBuffers(BufferCount, CUInt(normalizedSize.Width), CUInt(normalizedSize.Height), Format.B8G8R8A8_UNorm, SwapChainFlags.None)
        _size = normalizedSize
    End Sub

    ''' <summary>
    ''' 提交当前帧到 DXGI swap chain。它是窗口级 EndFrame 的最后一步，控件 RenderGpu 绝不能自行调用。
    ''' 返回 False 表示当前没有可提交的 swap chain；抛出的 DXGI 设备错误由 D3D_WindowCompositor 统一转为 device lost。
    ''' </summary>
    Public Function Present(Optional syncInterval As UInteger = 1UI) As Boolean
        If _swapChain Is Nothing Then Return False
        _swapChain.Present(syncInterval, PresentFlags.None)
        Return True
    End Function

    Public Function Commit() As Boolean
        Return Present()
    End Function

    Private Sub CreateSwapChain(manager As D3D_DeviceManager, hwnd As IntPtr, size As Size)
        Dim desc As New SwapChainDescription1(
            CUInt(size.Width),
            CUInt(size.Height),
            Format.B8G8R8A8_UNorm,
            False,
            Usage.RenderTargetOutput,
            BufferCount,
            Scaling.Stretch,
            SwapEffect.FlipSequential,
            AlphaMode.Ignore,
            SwapChainFlags.None)

        _swapChain = manager.DXGIFactory.CreateSwapChainForHwnd(manager.D3DDevice, hwnd, desc, Nothing, Nothing)
        _hwnd = hwnd
        _size = size
        _generation = manager.DeviceGeneration

        Try
            manager.DXGIFactory.MakeWindowAssociation(hwnd, WindowAssociationFlags.IgnoreAltEnter)
        Catch
            ' Alt+Enter 管理失败不影响 swap chain 呈现；窗口仍按普通 WinForms 生命周期运行。
        End Try
    End Sub

    Private Sub CreateD2DTarget(deviceContext As ID2D1DeviceContext)
        ReleaseTargetBitmap()

        Dim surface As IDXGISurface = Nothing
        Try
            surface = _swapChain.GetBuffer(Of IDXGISurface)(0UI)
            Dim props As New BitmapProperties1(
                New Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Ignore),
                96.0F,
                96.0F,
                BitmapOptions.Target Or BitmapOptions.CannotDraw)

            _targetBitmap = deviceContext.CreateBitmapFromDxgiSurface(surface, props)
        Finally
            If surface IsNot Nothing Then
                Try : surface.Dispose() : Catch : End Try
            End If
        End Try
    End Sub

    Private Shared Function NormalizeSize(size As Size) As Size
        Return New Size(Math.Max(1, size.Width), Math.Max(1, size.Height))
    End Function

    Private Sub ReleaseTargetBitmap()
        If _targetBitmap Is Nothing Then Return
        Try : _targetBitmap.Dispose() : Catch : End Try
        _targetBitmap = Nothing
    End Sub

    Private Sub ReleaseSwapChain()
        If _swapChain Is Nothing Then Return
        Try : _swapChain.Dispose() : Catch : End Try
        _swapChain = Nothing
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        ReleaseTargetBitmap()
        ReleaseSwapChain()
        GC.SuppressFinalize(Me)
    End Sub
End Class
