Imports System.Numerics
Imports Vortice.Direct2D1

''' <summary>
''' D3D_WindowCompositor 是新窗口级 GPU 场景根，不是旧 WindowCompositor 的升级版或改名版。
''' 它绑定 WinForms Form 的 HWND，持有 swap chain/DirectComposition host、D2D DeviceContext、窗口级 GPU cache、background graph、dirty region、DPI 和 resize 状态。
''' 它不负责迁移现有控件，不要求 Demo 配合，不处理旧 PaintScopeV2 三层结构，也不为普通 WinForms 子控件重新引入 CPU 截图或 HDC 合成。
''' <para>
''' 资源生命周期：Form 拥有本 compositor；compositor 拥有窗口级 target 和缓存；控件 RenderGpu 只能使用传入 D3D_PaintContext，不持有跨帧 GPU 对象。
''' 该类绑定 device generation，设备丢失时释放所有窗口级 GPU 资源，下一帧按需重建。
''' 驱动更新、TDR、休眠恢复、远程桌面切换或显示适配器重置都按 device lost 处理。
''' 如果设备丢失发生在 BeginDraw/EndDraw/Present 当前帧内部，本类先标记 pending lost，退出 BeginFrame/EndFrame 后再释放资源，
''' 避免在 D2D 调用栈中 Dispose target，同时保证下一帧不会复用旧驱动/旧设备对象。
''' </para>
''' <para>
''' 线程边界：所有 BeginFrame/EndFrame/ResizeTarget/Present 调用必须发生在 UI 线程；不允许嵌套 BeginFrame。
''' 普通 WinForms 子控件只作为外部 HWND/WinForms paint 边界处理，不作为高质量 GPU BackgroundSource。
''' </para>
''' </summary>
Public NotInheritable Class D3D_WindowCompositor
    Implements IDisposable

    Private ReadOnly _form As Form
    Private ReadOnly _deviceManager As D3D_DeviceManager
    Private _swapChainHost As D3D_SwapChainHost = New D3D_SwapChainHost()
    Private ReadOnly _directCompositionHost As New D3D_DirectCompositionHost()
    Private ReadOnly _dirtyRegion As New D3D_DirtyRegionTracker()
    Private ReadOnly _scheduler As D3D_FrameScheduler
    Private _deviceContext As ID2D1DeviceContext
    Private _frameGeneration As Integer
    Private _deviceGeneration As Integer = -1
    Private _inFrame As Boolean
    Private _deviceLostPending As Boolean
    Private _lastFrameDeviceLost As Boolean
    Private _disposed As Boolean
    Private _dpiContext As V3_DpiContext

    Public Sub New(form As Form, deviceManager As D3D_DeviceManager)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))
        If deviceManager Is Nothing Then Throw New ArgumentNullException(NameOf(deviceManager))

        _form = form
        _deviceManager = deviceManager
        _scheduler = New D3D_FrameScheduler(Me)
        _dpiContext = V3_DpiContext.FromControl(form)

        BrushCache = New D3D_BrushCache()
        GeometryCache = New D3D_GeometryCache(_deviceManager)
        TextureCache = New D3D_TextureCache()
        ImageCache = New D3D_ImageCache(TextureCache)
        TextRenderer = New D3D_TextRenderer(_deviceManager)
        BackgroundGraph = New D3D_BackgroundGraph(TextureCache)
        BackdropRenderer = New D3D_BackdropRenderer(ImageCache)
        FrameGraph = D3D_FrameGraph.CreateDefault()

        AddHandler _form.HandleDestroyed, AddressOf OnFormHandleDestroyed
        AddHandler _form.Resize, AddressOf OnFormResize
        AddHandler _form.Disposed, AddressOf OnFormDisposed
        AddHandler _form.DpiChanged, AddressOf OnFormDpiChanged
    End Sub

    Public ReadOnly Property Form As Form
        Get
            Return _form
        End Get
    End Property

    Public ReadOnly Property IsDisposed As Boolean
        Get
            Return _disposed
        End Get
    End Property

    Public ReadOnly Property IsPainting As Boolean
        Get
            Return _inFrame
        End Get
    End Property

    Public ReadOnly Property DeviceContext As ID2D1DeviceContext
        Get
            Return _deviceContext
        End Get
    End Property

    Public ReadOnly Property BrushCache As D3D_BrushCache
    Public ReadOnly Property GeometryCache As D3D_GeometryCache
    Public ReadOnly Property TextureCache As D3D_TextureCache
    Public ReadOnly Property ImageCache As D3D_ImageCache
    Public ReadOnly Property TextRenderer As D3D_TextRenderer
    Public ReadOnly Property BackgroundGraph As D3D_BackgroundGraph
    Public ReadOnly Property BackdropRenderer As D3D_BackdropRenderer
    Public ReadOnly Property FrameGraph As D3D_FrameGraph

    Public Property TextQuality As D3D_TextQualityMode = D3D_TextQualityMode.Auto

    ''' <summary>
    ''' 开始窗口级 GPU 帧：绑定 swap chain target，清理 transient state，设置 DPI/文本质量并 BeginDraw。
    ''' 调用方不得嵌套 BeginFrame，也不得在返回的 D3D_PaintContext 外保存任何 target 相关对象。
    ''' 如果 BeginDraw 后续步骤遇到设备丢失，本方法会中止活动帧、标记 pending lost，并返回 Nothing 让下一帧重建。
    ''' </summary>
    Public Function BeginFrame(Optional explicitDirtyRegion As IReadOnlyList(Of Rectangle) = Nothing) As D3D_PaintContext
        If _disposed Then Return Nothing
        If _form.IsDisposed OrElse Not _form.IsHandleCreated Then Return Nothing
        If _form.WindowState = FormWindowState.Minimized Then Return Nothing
        If _inFrame Then Throw New InvalidOperationException("D3D_WindowCompositor does not allow nested BeginFrame.")
        _lastFrameDeviceLost = False

        Try
            EnsureResources()

            Dim dirty = If(explicitDirtyRegion, _dirtyRegion.SnapshotAndClear())
            _deviceContext.Target = _swapChainHost.TargetBitmap
            _deviceContext.Transform = Matrix3x2.Identity
            _deviceContext.AntialiasMode = AntialiasMode.PerPrimitive
            TextRenderer.ConfigureDeviceContext(_deviceContext, TextQuality, targetHasAlpha:=False)

            _deviceContext.BeginDraw()
            _inFrame = True
            _frameGeneration += 1

            Dim clearColor = D3D_PaintContext.ToColor4(_form.BackColor)
            _deviceContext.Clear(clearColor)

            Return New D3D_PaintContext(
                Me,
                _deviceContext,
                Matrix3x2.Identity,
                New RectangleF(0, 0, Math.Max(1, _form.ClientSize.Width), Math.Max(1, _form.ClientSize.Height)),
                _dpiContext.Scale,
                TextQuality,
                _frameGeneration,
                _deviceManager.DeviceGeneration,
                dirty)
        Catch ex As Exception
            If _deviceManager.HandleDeviceLost(ex) Then
                _lastFrameDeviceLost = True
                MarkDeviceLost()
                AbortActiveFrame()
                CompletePendingDeviceLost()
                Return Nothing
            End If

            AbortActiveFrame()
            Throw
        End Try
    End Function

    ''' <summary>
    ''' 结束窗口级 GPU 帧：Dispose frame context、EndDraw、解绑 target，然后按需 Present。控件不能自行提交 Present/Commit。
    ''' 返回 True 只表示本帧已经成功提交到 swap chain；present=False、无活动帧或设备丢失都会返回 False。
    ''' </summary>
    Public Function EndFrame(context As D3D_PaintContext, Optional present As Boolean = True) As Boolean
        If Not _inFrame Then Return False

        Dim framePresented As Boolean = False
        Try
            If context IsNot Nothing Then context.Dispose()
            _deviceContext.EndDraw()

            If present Then framePresented = _swapChainHost.Present()
        Catch ex As Exception
            If _deviceManager.HandleDeviceLost(ex) Then
                _lastFrameDeviceLost = True
                MarkDeviceLost()
            Else
                Throw
            End If
        Finally
            _inFrame = False
            If _deviceContext IsNot Nothing Then
                Try : _deviceContext.Target = Nothing : Catch : End Try
            End If
            CompletePendingDeviceLost()
        End Try

        Return framePresented AndAlso Not _lastFrameDeviceLost
    End Function

    ''' <summary>
    ''' 便于核心级验证的绘制入口。drawAction 为空时只清空并 Present 一帧，用于证明 swap chain 路线可用且不经过 HDC。
    ''' 如果 drawAction 抛出异常，本方法仍会 EndDraw/解绑 target，但不会 Present 未完成内容。
    ''' </summary>
    Public Function RenderFrame(drawAction As Action(Of D3D_PaintContext)) As Boolean
        Dim context = BeginFrame()
        If context Is Nothing Then Return False

        Dim framePresented As Boolean = False
        Dim shouldPresent As Boolean = True
        Try
            If drawAction IsNot Nothing Then drawAction(context)
        Catch
            shouldPresent = False
            Throw
        Finally
            framePresented = EndFrame(context, present:=shouldPresent)
        End Try
        Return framePresented
    End Function

    ''' <summary>
    ''' 请求下一帧。dirty region 合并在窗口级完成；这里不会立即绘制，也不会调用 WinForms Invalidate 触发旧 OnPaint 路线。
    ''' </summary>
    Public Sub RequestRender(dirtyRect As Rectangle)
        If _disposed Then Return
        _dirtyRegion.Add(dirtyRect)
        _scheduler.RequestFrame()
    End Sub

    Friend Sub RenderScheduledFrame()
        If _disposed Then Return
        RenderFrame(Nothing)
    End Sub

    ''' <summary>
    ''' 释放窗口级 target、D2D context 和所有 generation-aware cache。下一帧会从 D3D_DeviceManager 重新获取 device。
    ''' 如果调用发生在帧内，只设置 pending 标记；真正释放延迟到 EndFrame/AbortActiveFrame 后。
    ''' </summary>
    Public Sub HandleDeviceLost()
        MarkDeviceLost()
        If _inFrame Then
            _lastFrameDeviceLost = True
            Return
        End If

        CompletePendingDeviceLost()
    End Sub

    Private Sub MarkDeviceLost()
        _deviceLostPending = True
        _deviceGeneration = -1
    End Sub

    Private Sub CompletePendingDeviceLost()
        If Not _deviceLostPending OrElse _inFrame Then Return
        _deviceLostPending = False

        ReleaseWindowGpuResources(recreateSwapChainHost:=Not _disposed)
        BrushCache.Invalidate()
        GeometryCache.Invalidate()
        TextureCache.ReleaseAll()
        ImageCache.Invalidate()
        TextRenderer.Invalidate()
        BackgroundGraph.Invalidate()
        BackdropRenderer.Invalidate()
        _deviceGeneration = -1

        If Not _disposed AndAlso Not _form.IsDisposed AndAlso _form.IsHandleCreated AndAlso _form.WindowState <> FormWindowState.Minimized Then
            RequestRender(New Rectangle(Point.Empty, _form.ClientSize))
        End If
    End Sub

    Private Sub AbortActiveFrame()
        If Not _inFrame Then Return

        ' BeginDraw 已经成功但 Clear/绘制/设备操作抛错时，必须结束 D2D 帧并解绑 target，避免 compositor 永久停在 in-frame 状态。
        Try
            If _deviceContext IsNot Nothing Then _deviceContext.EndDraw()
        Catch
        End Try

        _inFrame = False
        If _deviceContext IsNot Nothing Then
            Try : _deviceContext.Target = Nothing : Catch : End Try
        End If
    End Sub

    ''' <summary>
    ''' 调整窗口呈现目标。调用方必须保证当前不在 BeginFrame 内；Resize 只释放 DXGI/D2D target，不走 HDC 重绘。
    ''' ResizeBuffers 遇到驱动更新、TDR、休眠恢复等设备错误时会进入统一 device lost 路径。
    ''' </summary>
    Public Sub ResizeTarget()
        If _disposed OrElse _inFrame OrElse _deviceContext Is Nothing Then Return
        Try
            _deviceContext.Target = Nothing
            _swapChainHost.ResizeTarget(_form.ClientSize)
            _swapChainHost.EnsureTarget(_deviceManager, _deviceContext, _form.Handle, _form.ClientSize)
            RequestRender(New Rectangle(Point.Empty, _form.ClientSize))
        Catch ex As Exception
            If _deviceManager.HandleDeviceLost(ex) Then
                HandleDeviceLost()
            Else
                Throw
            End If
        End Try
    End Sub

    Private Sub EnsureResources()
        If _deviceContext Is Nothing OrElse _deviceGeneration <> _deviceManager.DeviceGeneration Then
            ReleaseWindowGpuResources(recreateSwapChainHost:=True)
            _deviceContext = _deviceManager.CreateDeviceContext()
            _deviceGeneration = _deviceManager.DeviceGeneration
            BrushCache.Invalidate()
            GeometryCache.Invalidate()
            TextureCache.InvalidateGeneration(_deviceGeneration)
            TextRenderer.Invalidate()
            BackgroundGraph.Invalidate()
        End If

        _dpiContext = V3_DpiContext.FromControl(_form)
        _swapChainHost.EnsureTarget(_deviceManager, _deviceContext, _form.Handle, _form.ClientSize)
    End Sub

    Private Sub ReleaseWindowGpuResources(recreateSwapChainHost As Boolean)
        If _deviceContext IsNot Nothing Then
            Try : _deviceContext.Target = Nothing : Catch : End Try
        End If

        Try : _swapChainHost.Dispose() : Catch : End Try
        If recreateSwapChainHost Then _swapChainHost = New D3D_SwapChainHost()

        If _deviceContext IsNot Nothing Then
            Try : _deviceContext.Dispose() : Catch : End Try
            _deviceContext = Nothing
        End If
    End Sub

    Private Sub OnFormResize(sender As Object, e As EventArgs)
        ResizeTarget()
    End Sub

    Private Sub OnFormDpiChanged(sender As Object, e As EventArgs)
        _dpiContext = V3_DpiContext.FromControl(_form)
        ResizeTarget()
    End Sub

    Private Sub OnFormHandleDestroyed(sender As Object, e As EventArgs)
        Dispose()
    End Sub

    Private Sub OnFormDisposed(sender As Object, e As EventArgs)
        Dispose()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True

        RemoveHandler _form.HandleDestroyed, AddressOf OnFormHandleDestroyed
        RemoveHandler _form.Resize, AddressOf OnFormResize
        RemoveHandler _form.Disposed, AddressOf OnFormDisposed
        RemoveHandler _form.DpiChanged, AddressOf OnFormDpiChanged

        _scheduler.Dispose()
        _directCompositionHost.Dispose()
        MarkDeviceLost()
        CompletePendingDeviceLost()
        BackdropRenderer.Dispose()
        BackgroundGraph.Dispose()
        TextRenderer.Dispose()
        ImageCache.Dispose()
        TextureCache.Dispose()
        GeometryCache.Dispose()
        BrushCache.Dispose()
        D3D_RenderCore.UnregisterCompositor(_form, Me)
        GC.SuppressFinalize(Me)
    End Sub
End Class
