Imports Vortice.Direct2D1

''' <summary>
''' V2 窗口级合成器：每个顶层 Form 一份实例，集中持有该窗口内所有控件共享的 D2D / DWrite 资源。
'''
''' === 持有的资源 ===
''' • 1 个共享 <see cref="ID2D1DCRenderTarget"/>（背景层 / 文字层 / 1× 图形层共用）
'''   - 每次 <see cref="BeginPaint"/> BindDC 到当前控件 HDC；窗口内所有控件共用同一份 RT 实例。
'''   - DC RT 不支持嵌套 BindDC / BeginDraw；同一 Form 内绘制重入时直接返回 Nothing，由调用方跳过本次 V2 绘制。
'''   - <see cref="D2DHelper.SolidColorBrushCache"/> 内部按 RT 分桶，因此 BindDC 不会让 brush 失效。
''' • 1 个 <see cref="D2DHelper.SolidColorBrushCache"/>（按 RT 分桶；<see cref="BrushCache"/>）。
''' • 1 个 <see cref="D2DHelper.TextFormatCache"/>（DWrite TextFormat 与 RT 无关，全 Form 通用）。
''' • 一组 <see cref="D2DHelper.D2DBitmapCache"/>：按 Image 引用建索引，用于图标 / 背景图复用上传。
''' • 一个 SSAA <see cref="ID2D1BitmapRenderTarget"/> 池：按像素尺寸 (W,H) 共享 1 份，避免每帧分配/释放。
'''
''' === 不持有 ===
''' • 控件级 SSAA 字段：V2 控件不再持有 _ssaaCache，每次绘制从池中 Rent / Return。
''' • 背景采样位图：由 <see cref="BackgroundPenetrationV2"/> 按 source 控件维度独立维护。
'''
''' === 生命周期 ===
''' • Form.HandleDestroyed → <see cref="Dispose"/> → 通知 <see cref="D2DHelperV2.UnregisterCompositor"/> 注销。
''' • Dispose 之后再调用任何 Get/Rent 都返回 Nothing；Return 路径会把传入对象直接 Dispose。
'''
''' === 线程要求 ===
''' • 所有方法都假定在 UI 线程调用；内部仅在 <see cref="D2DHelperV2"/> 的注册表层加锁，
'''   compositor 自身字段（_dcRT、_ssaaPool、_bitmapCaches）不做并发保护。
''' • BeginPaint 仅防重入，不提供跨线程互斥；V2 绘制仍必须在 UI 线程完成。
'''
''' === 已知限制 ===
''' • SSAA 池无 LRU 上限。若控件做"逐像素 Resize 动画"会让池内 key 持续增多；
'''   常用场景（创建后尺寸基本固定）下池规模为 O(同尺寸不同控件 → 1 个 key)。
''' • DPI 变更不重建 DC RT。BindDC 时给的 rect 即逻辑大小，DPI 切换由控件自身 Invalidate 重绘解决。
''' • <see cref="GetBitmapCache"/> 用 <see cref="Image"/> 强引用作 key，若 Image 在外部被 Dispose
'''   而未通知 compositor，缓存会持有失效 Image 至 Form 销毁；推荐优先用 BackgroundPenetrationV2。
''' </summary>
Public NotInheritable Class WindowCompositor
    Implements IDisposable

    Private ReadOnly _form As Form
    Private ReadOnly _unregisterOnDispose As Boolean
    Private _dcRT As ID2D1DCRenderTarget
    Private _deviceContext As ID2D1DeviceContext
    Private _deviceContextProbed As Boolean
    Private _deviceContextGeneration As Integer
    Private _deviceLostHandlerAttached As Boolean
    Private _disposed As Boolean
    Private _activePaintScopes As Integer

    ''' <summary>Image → D2DBitmapCache 映射；为长期存在的图标 / 背景图复用 D2D 上传。</summary>
    Private ReadOnly _bitmapCaches As New Dictionary(Of Image, D2DHelper.D2DBitmapCache)()

    ''' <summary>共享的 SolidColorBrush 缓存（按 RT 切换自动失效）。</summary>
    Public ReadOnly Property BrushCache As New D2DHelper.SolidColorBrushCache()

    ''' <summary>共享的 DirectWrite TextFormat 缓存。</summary>
    Public ReadOnly Property TextFormatCache As New D2DHelper.TextFormatCache()

    ''' <summary>所属 Form。</summary>
    Public ReadOnly Property Form As Form
        Get
            Return _form
        End Get
    End Property

    ''' <summary>是否已释放。</summary>
    Public ReadOnly Property IsDisposed As Boolean
        Get
            Return _disposed
        End Get
    End Property

    Friend Sub New(form As Form, Optional unregisterOnDispose As Boolean = True)
        _form = form
        _unregisterOnDispose = unregisterOnDispose
        AddHandler form.HandleDestroyed, AddressOf OnFormHandleDestroyed
    End Sub

    Private Sub OnFormHandleDestroyed(sender As Object, e As EventArgs)
        Dispose()
    End Sub

    ''' <summary>
    ''' 取（按需创建）共享 DC RT。仅在 UI 线程调用；Dispose 之后返回 <c>Nothing</c>。
    ''' </summary>
    ''' <remarks>
    ''' 返回的 RT 在调用方 <see cref="ID2D1DCRenderTarget.BindDC"/> 之前不可绘制。
    ''' 正常路径下应通过 <see cref="BeginPaint"/> 拿到 <see cref="PaintScopeV2"/>，由后者完成 BindDC + BeginDraw。
    ''' </remarks>
    Friend Function GetOrCreateDCRenderTarget() As ID2D1DCRenderTarget
        If _disposed Then Return Nothing
        If _dcRT Is Nothing Then
            _dcRT = D2DHelper.CreateDCRenderTarget()
        End If
        Return _dcRT
    End Function

    ''' <summary>
    ''' 取（按需创建）本窗口的 <see cref="ID2D1DeviceContext"/>（D2D 1.1 入口）。
    ''' <para>
    ''' 阶段 A 行为：首次访问时通过 <see cref="D3D11Globals.CreateDeviceContext"/> 创建一份
    ''' per-form 的 DeviceContext，与本窗口的 DC RT 同生共死。设计器、RDP、驱动异常等环境下
    ''' 返回 <c>Nothing</c>，调用方应自行回退到 DC RT 路径。
    ''' </para>
    ''' <para>
    ''' <b>设备丢失处理</b>：本属性会在第一次成功创建 DeviceContext 时订阅
    ''' <see cref="D3D11Globals.DeviceLost"/>；一旦进程级设备失效，订阅回调会立即
    ''' Dispose 当前 DeviceContext 并清空 _deviceContextProbed，下一次访问本属性会用
    ''' 新设备重建。此外每次访问都会比对 <see cref="D3D11Globals.DeviceGeneration"/>，
    ''' 一旦发现代号变化也会主动放弃旧 DeviceContext，保证不会把死设备暴露给调用方。
    ''' </para>
    ''' <para>
    ''' 阶段 A 限制：与 <see cref="GetOrCreateDCRenderTarget"/> 创建出的 DC RT <b>不共享 D3D 设备</b>
    ''' （DC RT 是由 factory 隐式创建的 D3D10 设备承载，DeviceContext 是显式 D3D11 设备承载），
    ''' 因此 <c>ID2D1Bitmap</c> / <c>ID2D1SolidColorBrush</c> 不能跨二者通用。
    ''' 阶段 B 会把 DC RT 也改为由本设备创建以解除该限制。
    ''' </para>
    ''' </summary>
    Public ReadOnly Property DeviceContext As ID2D1DeviceContext
        Get
            If _disposed Then Return Nothing
            ' 设备代号变化 → 旧 DeviceContext 必然失效，先扔掉再按"未探测"重新走创建路径。
            If _deviceContext IsNot Nothing AndAlso _deviceContextGeneration <> D3D11Globals.DeviceGeneration Then
                ReleaseDeviceContextNoLock()
            End If
            If _deviceContext IsNot Nothing Then Return _deviceContext
            If _deviceContextProbed Then Return Nothing
            _deviceContextProbed = True
            Try
                _deviceContext = D3D11Globals.CreateDeviceContext()
                _deviceContextGeneration = D3D11Globals.DeviceGeneration
            Catch
                _deviceContext = Nothing
            End Try
            If _deviceContext IsNot Nothing AndAlso Not _deviceLostHandlerAttached Then
                AddHandler D3D11Globals.DeviceLost, AddressOf OnDeviceLost
                _deviceLostHandlerAttached = True
            End If
            Return _deviceContext
        End Get
    End Property

    ''' <summary>
    ''' 通知本 compositor"DeviceContext 抛出了设备级错误"。
    ''' 内部会调用 <see cref="D3D11Globals.HandleDeviceLost"/>；如果确认是设备丢失，
    ''' 还会顺手清空本 compositor 的 DeviceContext 引用以加快下一帧重建。
    ''' 返回 <c>True</c> 表示确实是设备丢失，调用方应 swallow 异常并降级到 DC RT。
    ''' </summary>
    Public Function NotifyDeviceContextException(ex As Exception) As Boolean
        Dim isLost = D3D11Globals.HandleDeviceLost(ex)
        If isLost Then ReleaseDeviceContextNoLock()
        Return isLost
    End Function

    Friend Function NotifyDCRenderTargetException(ex As Exception) As Boolean
        If Not D3D11Globals.IsDeviceLostException(ex) Then Return False
        ReleaseDCRenderTargetNoLock()
        D3D11Globals.HandleDeviceLost(ex)
        Return True
    End Function

    Private Sub OnDeviceLost(sender As Object, e As EventArgs)
        ' D3D11Globals 已在 UI 线程同步触发，这里直接释放即可。
        ReleaseDeviceContextNoLock()
    End Sub

    Private Sub ReleaseDeviceContextNoLock()
        If _deviceContext IsNot Nothing Then
            Try : _deviceContext.Target = Nothing : Catch : End Try
            Try : _deviceContext.Dispose() : Catch : End Try
            _deviceContext = Nothing
        End If
        _deviceContextProbed = False
        _deviceContextGeneration = 0
    End Sub

    Private Sub ReleaseDCRenderTargetNoLock()
        Try : BrushCache.Invalidate() : Catch : End Try
        For Each kv In _bitmapCaches
            Try : kv.Value.Invalidate() : Catch : End Try
        Next
        For Each kv In _ssaaPool
            Try : kv.Value.Dispose() : Catch : End Try
        Next
        _ssaaPool.Clear()
        If _dcRT IsNot Nothing Then
            Try : _dcRT.Dispose() : Catch : End Try
            _dcRT = Nothing
        End If
    End Sub

    ''' <summary>
    ''' 取（按需创建）一份 <see cref="Image"/> 对应的 <see cref="D2DHelper.D2DBitmapCache"/>。
    ''' Key 为 Image 引用本身（不复制，不哈希像素）。
    ''' </summary>
    ''' <remarks>
    ''' 适用于 ImageList 中长期存在的图标 / 背景图。调用方在 Image 被替换时应负责丢弃旧 Image
    ''' 的引用以便随 Form Dispose 时一同清理；不要把临时 Bitmap（如 OnPaint 内 new 出来的）放进来。
    ''' </remarks>
    Friend Function GetBitmapCache(src As Image) As D2DHelper.D2DBitmapCache
        If _disposed OrElse src Is Nothing Then Return Nothing
        Dim cache As D2DHelper.D2DBitmapCache = Nothing
        If Not _bitmapCaches.TryGetValue(src, cache) Then
            cache = New D2DHelper.D2DBitmapCache()
            _bitmapCaches(src) = cache
        End If
        Return cache
    End Function

    ''' <summary>
    ''' SSAA 离屏 RT 池：按 (pixelW, pixelH) 共享一份 <see cref="ID2D1BitmapRenderTarget"/>。
    '''
    ''' V1 / V2 早期方案是控件内 Create + 帧末 Dispose，避免显存按控件数堆积，但代价是每个 OnPaint
    ''' 在 UI 线程上做一次 GPU 资源分配/释放 —— 多个控件在同一动画 tick 重绘时会形成周期性卡顿。
    ''' 这里改为按 SSAA 像素尺寸池化：窗口内同尺寸共享 1 份；不同尺寸数量上限受场景控制（通常 &lt;= 控件类型数）。
    ''' </summary>
    Private ReadOnly _ssaaPool As New Dictionary(Of Long, ID2D1BitmapRenderTarget)()

    Private Shared Function MakeSsaaKey(w As Integer, h As Integer) As Long
        Return (CLng(w) << 32) Or (CLng(h) And &HFFFFFFFFL)
    End Function

    ''' <summary>
    ''' 从 SSAA 池借出指定像素尺寸的 BitmapRT；池中没有时由 <paramref name="owner"/> 即时创建。
    ''' </summary>
    ''' <param name="owner">用于 CreateCompatibleRenderTarget 的来源 RT，通常即 DC RT。</param>
    Friend Function RentSsaaRT(owner As ID2D1RenderTarget, pixelW As Integer, pixelH As Integer) As ID2D1BitmapRenderTarget
        If _disposed Then Return Nothing
        Dim key As Long = MakeSsaaKey(pixelW, pixelH)
        Dim rt As ID2D1BitmapRenderTarget = Nothing
        If _ssaaPool.TryGetValue(key, rt) Then
            _ssaaPool.Remove(key)
            Return rt
        End If
        Return owner.CreateCompatibleRenderTarget(New Vortice.Mathematics.SizeI(pixelW, pixelH))
    End Function

    ''' <summary>
    ''' 归还 SSAA RT 至池；同尺寸池中已有时，把多余的 RT 直接 Dispose 以维持 (W,H) → 1 份的不变式。
    ''' compositor 已 Dispose 时也会直接 Dispose 入参，保证无泄漏。
    ''' </summary>
    Friend Sub ReturnSsaaRT(rt As ID2D1BitmapRenderTarget, pixelW As Integer, pixelH As Integer)
        If rt Is Nothing Then Return
        If _disposed Then
            Try : rt.Dispose() : Catch : End Try
            Return
        End If
        Dim key As Long = MakeSsaaKey(pixelW, pixelH)
        If _ssaaPool.ContainsKey(key) Then
            Try : rt.Dispose() : Catch : End Try
            Return
        End If
        _ssaaPool(key) = rt
    End Sub

    ''' <summary>
    ''' 开始一次 V2 绘制作用域。内部完成：取 HDC → BindDC → BeginDraw → 应用全局抗锯齿 → 按需创建 SSAA RT。
    ''' 返回的 <see cref="PaintScopeV2"/> 必须 Using 释放（Dispose 内 FlushGraphics + EndDraw + ReleaseHdc + 退出重入标记）。
    ''' 若当前 Form 的共享 DC RT 正在绘制，返回 Nothing，避免嵌套 BindDC 触发 D2DERR_WRONG_STATE。
    ''' </summary>
    Friend Function BeginPaint(e As PaintEventArgs, control As Control, ssaaScale As Integer,
                               Optional disposeCompositorWithScope As Boolean = False) As PaintScopeV2
        If _disposed Then Return Nothing
        If _activePaintScopes > 0 Then Return Nothing
        Dim dcRT = GetOrCreateDCRenderTarget()
        If dcRT Is Nothing Then Return Nothing
        Dim hdc As IntPtr = e.Graphics.GetHdc()
        _activePaintScopes += 1
        Try
            Return New PaintScopeV2(Me, e.Graphics, hdc, dcRT, control.Width, control.Height, ssaaScale, disposeCompositorWithScope)
        Catch ex As Exception
            Try : NotifyDCRenderTargetException(ex) : Catch : End Try
            EndPaintScope()
            Try : e.Graphics.ReleaseHdc(hdc) : Catch : End Try
            Throw
        End Try
    End Function

    ''' <summary>结束一次活动绘制作用域，允许下一次 BindDC / BeginDraw。</summary>
    Friend Sub EndPaintScope()
        If _activePaintScopes > 0 Then _activePaintScopes -= 1
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        Try
            RemoveHandler _form.HandleDestroyed, AddressOf OnFormHandleDestroyed
        Catch
        End Try
        Try : BrushCache.Dispose() : Catch : End Try
        Try : TextFormatCache.Dispose() : Catch : End Try
        For Each kv In _bitmapCaches
            Try : kv.Value.Dispose() : Catch : End Try
        Next
        _bitmapCaches.Clear()
        ReleaseDCRenderTargetNoLock()
        If _deviceLostHandlerAttached Then
            Try : RemoveHandler D3D11Globals.DeviceLost, AddressOf OnDeviceLost : Catch : End Try
            _deviceLostHandlerAttached = False
        End If
        ReleaseDeviceContextNoLock()
        If _unregisterOnDispose Then D2DHelperV2.UnregisterCompositor(_form)
    End Sub
End Class
