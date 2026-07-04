Imports Vortice.Direct2D1

''' <summary>
''' V2 窗口级合成器：每个顶层 Form 一份实例，集中持有该窗口内所有控件共享的 D2D / DWrite 资源。
'''
''' === 持有的资源 ===
''' • 1 个共享 <see cref="ID2D1DCRenderTarget"/>（背景层 / 文字层 / 1× 图形层共用）
'''   - 每次 <see cref="BeginPaint"/> BindDC 到当前控件 HDC；窗口内所有控件共用同一份 RT 实例。
'''   - DC RT 不支持嵌套 BindDC / BeginDraw；同一 Form 内绘制重入时直接返回 Nothing，由调用方跳过本次 V2 绘制。
'''   - <see cref="D2DGlobals.SolidColorBrushCache"/> 内部按 RT 分桶，因此 BindDC 不会让 brush 失效。
''' • 1 个 <see cref="D2DGlobals.SolidColorBrushCache"/>（按 RT 分桶；<see cref="BrushCache"/>）。
''' • 1 个 <see cref="D2DGlobals.TextFormatCache"/>（DWrite TextFormat 与 RT 无关，全 Form 通用）。
''' • 一组 <see cref="D2DGlobals.D2DBitmapCache"/>：按 Image 弱引用建索引，用于图标 / 背景图复用上传。
''' • 一个 SSAA <see cref="ID2D1BitmapRenderTarget"/> 池：按分桶像素尺寸 (W,H) 共享 1 份，避免每帧分配/释放。
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
''' • SSAA 池接入进程级 GPU 总预算。极端大窗口 + 高倍率 SSAA 仍然可能瞬时占用较高显存；
'''   但归还池时会交给全局 LRU 尽快回落到预算内。
''' • DPI 变更会释放 DC RT、TextFormat、位图上传和 SSAA 池，下一帧按新 DPI 全量重建。
''' • <see cref="GetBitmapCache"/> 不再强持有 <see cref="Image"/>；源图被替换或释放后索引会在后续修剪中自动脱落。
''' </summary>
Public NotInheritable Class WindowCompositor
    Implements IDisposable, IRenderCacheOwner

    Private ReadOnly _form As Form
    Private ReadOnly _unregisterOnDispose As Boolean
    Private _formHwnd As IntPtr
    Private _dcRT As ID2D1DCRenderTarget
    Private _deviceContext As ID2D1DeviceContext
    Private _deviceContextGeneration As Integer
    Private _deviceLostHandlerAttached As Boolean
    Private _disposed As Boolean
    Private _activePaintScopes As Integer
    Private Const TransientCacheTrimInterval As Integer = 4096
    Private _paintScopesSinceTransientTrim As Integer
    Private _lastBrushCacheLimit As Integer = Integer.MinValue
    Private _lastTextFormatCacheLimit As Integer = Integer.MinValue
    Private _lastGpuBudgetBytes As Long = Long.MinValue
    Private _lastObservedDpi As Integer

    ''' <summary>Image → D2DBitmapCache 弱索引；为长期存在的图标 / 背景图复用 D2D 上传。</summary>
    Private NotInheritable Class BitmapCacheEntry
        Public SourceRef As WeakReference(Of Image)
        Public Cache As D2DGlobals.D2DBitmapCache
        Public LastUsed As Long
    End Class

    Private ReadOnly _bitmapCaches As New List(Of BitmapCacheEntry)()
    Private _bitmapCacheClock As Long

    ''' <summary>共享的 SolidColorBrush 缓存（按 RT 切换自动失效）。</summary>
    Public ReadOnly Property BrushCache As New D2DGlobals.SolidColorBrushCache()

    ''' <summary>共享的 DirectWrite TextFormat 缓存。</summary>
    Public ReadOnly Property TextFormatCache As New D2DGlobals.TextFormatCache()

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

    Friend ReadOnly Property IsPainting As Boolean
        Get
            Return _activePaintScopes > 0
        End Get
    End Property

    Friend Sub SynchronizeDpi(newDpi As Integer)
        If _disposed OrElse newDpi <= 0 Then Return
        _lastObservedDpi = newDpi
        Try
            If _form IsNot Nothing AndAlso Not _form.IsDisposed AndAlso _form.IsHandleCreated Then
                _formHwnd = _form.Handle
                D2DGlobals.SetWindowDpi(_formHwnd, newDpi)
            End If
        Catch
        End Try
    End Sub

    Friend Sub New(form As Form, Optional unregisterOnDispose As Boolean = True)
        _form = form
        _unregisterOnDispose = unregisterOnDispose
        _formHwnd = If(form.IsHandleCreated, form.Handle, IntPtr.Zero)
        GpuCache.Register(Me)
        AddHandler form.HandleCreated, AddressOf OnFormHandleCreated
        AddHandler form.HandleDestroyed, AddressOf OnFormHandleDestroyed
        If _unregisterOnDispose Then AddHandler form.DpiChanged, AddressOf OnFormDpiChanged
        If _unregisterOnDispose AndAlso form.IsHandleCreated Then
            _lastObservedDpi = D2DGlobals.GetCurrentDpi(form)
            D2DGlobals.SetWindowDpi(_formHwnd, _lastObservedDpi)
        End If
    End Sub

    Private Sub OnFormHandleCreated(sender As Object, e As EventArgs)
        Dim frm = TryCast(sender, Form)
        If frm Is Nothing OrElse Not frm.IsHandleCreated Then Return
        _formHwnd = frm.Handle
        If _unregisterOnDispose Then
            _lastObservedDpi = D2DGlobals.GetCurrentDpi(frm)
            D2DGlobals.SetWindowDpi(_formHwnd, _lastObservedDpi)
        End If
    End Sub

    Private Sub OnFormHandleDestroyed(sender As Object, e As EventArgs)
        If _unregisterOnDispose Then
            Try
                D2DGlobals.ClearWindowDpi(_formHwnd)
            Catch
            End Try
        End If
        Dispose()
    End Sub

    Private Sub OnFormDpiChanged(sender As Object, e As DpiChangedEventArgs)
        Dim frm = TryCast(sender, Form)
        If frm Is Nothing OrElse frm.IsDisposed Then Return

        Dim newDpi As Integer = 0
        Try
            newDpi = e.DeviceDpiNew
        Catch
            newDpi = 0
        End Try
        If newDpi <= 0 Then newDpi = D2DGlobals.GetCurrentDpi(frm)
        If newDpi <= 0 Then Return
        If newDpi = _lastObservedDpi Then
            SynchronizeDpi(newDpi)
            Return
        End If

        SynchronizeDpi(newDpi)
        D2DHelperV2.NotifyDpiChanged(frm, newDpi)
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
            _dcRT = D2DGlobals.CreateDCRenderTarget()
        End If
        Return _dcRT
    End Function

    ''' <summary>
    ''' 取（按需创建）本窗口的 <see cref="ID2D1DeviceContext"/>（D2D 1.1 入口）。
    ''' <para>
    ''' 阶段 A 行为：首次访问时通过 <see cref="D3D11Globals.CreateDeviceContext"/> 创建一份
    ''' per-form 的 DeviceContext，与本窗口的 DC RT 同生共死。LakeUI 不提供 GPU 不支持时的
    ''' 降级路线；除 compositor 已释放外，创建失败会直接向调用方抛出。
    ''' </para>
    ''' <para>
    ''' <b>设备丢失处理</b>：本属性会在第一次成功创建 DeviceContext 时订阅
    ''' <see cref="D3D11Globals.DeviceLost"/>；一旦进程级设备失效，订阅回调会立即
    ''' Dispose 当前 DeviceContext，下一次访问本属性会用新设备重建。
    ''' 此外每次访问都会比对 <see cref="D3D11Globals.DeviceGeneration"/>，
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
            _deviceContext = D3D11Globals.CreateDeviceContext()
            _deviceContextGeneration = D3D11Globals.DeviceGeneration
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
    ''' 返回 <c>True</c> 表示确实是设备丢失，调用方应 swallow 本帧并请求下一帧重画。
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
        _deviceContextGeneration = 0
    End Sub

    Private Sub ReleaseDCRenderTargetNoLock()
        Try : BrushCache.Invalidate() : Catch : End Try
        ReleaseBitmapCacheIndexNoLock()
        ReleaseSsaaPoolNoLock()
        If _dcRT IsNot Nothing Then
            Try : _dcRT.Dispose() : Catch : End Try
            _dcRT = Nothing
        End If
    End Sub

    Friend Function CleanupD2DResources(level As D2DCacheCleanupLevel) As Boolean
        If _disposed Then Return False
        If _activePaintScopes > 0 Then Return False

        Select Case level
            Case D2DCacheCleanupLevel.TrimToBudget
                TrimTransientCaches()

            Case D2DCacheCleanupLevel.ReleaseVolatileCaches
                Try : BrushCache.Invalidate() : Catch : End Try
                ReleaseSsaaPoolNoLock()
                ResetTransientTrimState()

            Case D2DCacheCleanupLevel.ReleaseAllCaches
                Try : BrushCache.Invalidate() : Catch : End Try
                Try : TextFormatCache.Invalidate() : Catch : End Try
                ReleaseBitmapCacheIndexNoLock()
                ReleaseSsaaPoolNoLock()
                ResetTransientTrimState()

            Case D2DCacheCleanupLevel.ReleaseRenderTargets,
                 D2DCacheCleanupLevel.RecreateDevice
                Try : TextFormatCache.Invalidate() : Catch : End Try
                ReleaseDCRenderTargetNoLock()
                ReleaseDeviceContextNoLock()
                ResetTransientTrimState()

            Case Else
                Dispose()
        End Select

        Return True
    End Function

    ''' <summary>
    ''' 取（按需创建）一份 <see cref="Image"/> 对应的 <see cref="D2DGlobals.D2DBitmapCache"/>。
    ''' Key 为 Image 引用本身（不复制，不哈希像素）。
    ''' </summary>
    ''' <remarks>
    ''' 适用于 ImageList 中长期存在的图标 / 背景图。索引只弱持有源 Image；
    ''' 调用方仍不要把临时 Bitmap（如 OnPaint 内 new 出来的）放进来。
    ''' </remarks>
    Friend Function GetBitmapCache(src As Image) As D2DGlobals.D2DBitmapCache
        If _disposed OrElse src Is Nothing Then Return Nothing
        For i As Integer = _bitmapCaches.Count - 1 To 0 Step -1
            Dim entry = _bitmapCaches(i)
            Dim entrySource As Image = Nothing
            If entry Is Nothing OrElse entry.SourceRef Is Nothing OrElse
               Not entry.SourceRef.TryGetTarget(entrySource) OrElse entrySource Is Nothing Then
                If entry IsNot Nothing Then Try : entry.Cache?.Dispose() : Catch : End Try
                _bitmapCaches.RemoveAt(i)
                Continue For
            End If
            If entrySource Is src Then
                entry.LastUsed = NextBitmapCacheClock()
                Return entry.Cache
            End If
        Next
        Dim cache = New D2DGlobals.D2DBitmapCache()
        _bitmapCaches.Add(New BitmapCacheEntry With {
            .SourceRef = New WeakReference(Of Image)(src),
            .Cache = cache,
            .LastUsed = NextBitmapCacheClock()
        })
        TrimBitmapCacheIndex(src)
        Return cache
    End Function

    Friend Function ReleaseBitmapCache(src As Image) As Boolean
        If _disposed OrElse src Is Nothing Then Return False
        Dim released As Boolean
        For i As Integer = _bitmapCaches.Count - 1 To 0 Step -1
            Dim entry = _bitmapCaches(i)
            Dim entrySource As Image = Nothing
            If entry Is Nothing OrElse entry.SourceRef Is Nothing OrElse
               Not entry.SourceRef.TryGetTarget(entrySource) OrElse entrySource Is Nothing OrElse entrySource Is src Then
                _bitmapCaches.RemoveAt(i)
                Try : entry?.Cache?.Dispose() : Catch : End Try
                released = True
            End If
        Next
        Return released
    End Function

    Private Function NextBitmapCacheClock() As Long
        _bitmapCacheClock += 1
        Return _bitmapCacheClock
    End Function

    Private Sub TrimBitmapCacheIndex(protectedImage As Image)
        For i As Integer = _bitmapCaches.Count - 1 To 0 Step -1
            Dim entry = _bitmapCaches(i)
            Dim entrySource As Image = Nothing
            If entry Is Nothing OrElse entry.SourceRef Is Nothing OrElse
               Not entry.SourceRef.TryGetTarget(entrySource) OrElse entrySource Is Nothing Then
                _bitmapCaches.RemoveAt(i)
                Try : entry?.Cache?.Dispose() : Catch : End Try
            End If
        Next
    End Sub

    Private Sub ReleaseBitmapCacheIndexNoLock()
        For Each entry In _bitmapCaches
            Try : entry.Cache.Dispose() : Catch : End Try
        Next
        _bitmapCaches.Clear()
    End Sub

    ''' <summary>
    ''' SSAA 离屏 RT 池：按分桶后的 (pixelW, pixelH) 共享一份 <see cref="ID2D1BitmapRenderTarget"/>。
    '''
    ''' V1 / V2 早期方案是控件内 Create + 帧末 Dispose，避免显存按控件数堆积，但代价是每个 OnPaint
    ''' 在 UI 线程上做一次 GPU 资源分配/释放 —— 多个控件在同一动画 tick 重绘时会形成周期性卡顿。
    ''' 这里改为按 SSAA 像素尺寸分桶池化：窗口内同桶共享 1 份；不同尺寸由 LRU 字节预算约束。
    ''' </summary>
    Private NotInheritable Class SsaaPoolEntry
        Public RenderTarget As ID2D1BitmapRenderTarget
        Public PixelW As Integer
        Public PixelH As Integer
        Public Bytes As Long
        Public LastUsed As Long
    End Class

    Private ReadOnly _ssaaPool As New Dictionary(Of Long, SsaaPoolEntry)()
    Private _ssaaPoolBytes As Long
    Private Shared Function MakeSsaaKey(w As Integer, h As Integer) As Long
        Return (CLng(w) << 32) Or (CLng(h) And &HFFFFFFFFL)
    End Function

    Private Shared Function BucketPixels(value As Integer) As Integer
        value = Math.Max(1, value)
        Dim bucketSize As Integer = Math.Max(1, GlobalOptions.SsaaBucketSize)
        Return CInt(Math.Ceiling(value / CDbl(bucketSize)) * bucketSize)
    End Function

    Private Shared Function EstimateBytes(pixelW As Integer, pixelH As Integer) As Long
        Return CLng(Math.Max(1, pixelW)) * CLng(Math.Max(1, pixelH)) * 4L
    End Function

    ''' <summary>
    ''' 从 SSAA 池借出至少满足指定像素尺寸的 BitmapRT；池中没有时由 <paramref name="owner"/> 即时创建。
    ''' </summary>
    ''' <param name="owner">用于 CreateCompatibleRenderTarget 的来源 RT，通常即 DC RT。</param>
    Friend Function RentSsaaRT(owner As ID2D1RenderTarget, pixelW As Integer, pixelH As Integer,
                               ByRef rentedPixelW As Integer, ByRef rentedPixelH As Integer) As ID2D1BitmapRenderTarget
        If _disposed OrElse owner Is Nothing Then Return Nothing
        rentedPixelW = BucketPixels(pixelW)
        rentedPixelH = BucketPixels(pixelH)
        Dim key As Long = MakeSsaaKey(rentedPixelW, rentedPixelH)
        Dim entry As SsaaPoolEntry = Nothing
        If _ssaaPool.TryGetValue(key, entry) Then
            _ssaaPool.Remove(key)
            _ssaaPoolBytes -= entry.Bytes
            If _ssaaPoolBytes < 0 Then _ssaaPoolBytes = 0
            entry.LastUsed = GpuCache.NextTick()
            Return entry.RenderTarget
        End If
        Return owner.CreateCompatibleRenderTarget(New Vortice.Mathematics.SizeI(rentedPixelW, rentedPixelH))
    End Function

    ''' <summary>
    ''' 归还 SSAA RT 至池；同桶池中已有时，把多余的 RT 直接 Dispose 以维持 (W,H) → 1 份的不变式。
    ''' compositor 已 Dispose 时也会直接 Dispose 入参，保证无泄漏。
    ''' </summary>
    Friend Sub ReturnSsaaRT(rt As ID2D1BitmapRenderTarget, pixelW As Integer, pixelH As Integer)
        If rt Is Nothing Then Return
        If _disposed Then
            ReleaseRenderTargetCaches(rt)
            Try : rt.Dispose() : Catch : End Try
            Return
        End If
        pixelW = BucketPixels(pixelW)
        pixelH = BucketPixels(pixelH)
        Dim key As Long = MakeSsaaKey(pixelW, pixelH)
        If _ssaaPool.ContainsKey(key) Then
            ReleaseRenderTargetCaches(rt)
            Try : rt.Dispose() : Catch : End Try
            Return
        End If
        Dim entry As New SsaaPoolEntry With {
            .RenderTarget = rt,
            .PixelW = pixelW,
            .PixelH = pixelH,
            .Bytes = EstimateBytes(pixelW, pixelH),
            .LastUsed = GpuCache.NextTick()
        }
        _ssaaPool(key) = entry
        _ssaaPoolBytes += entry.Bytes
        GpuCache.TrimToBudget(Me)
    End Sub

    Private Sub ReleaseSsaaPoolNoLock()
        For Each kv In _ssaaPool
            ReleaseRenderTargetCaches(kv.Value.RenderTarget)
            Try : kv.Value.RenderTarget.Dispose() : Catch : End Try
        Next
        _ssaaPool.Clear()
        _ssaaPoolBytes = 0
    End Sub

    Private Function TrimSsaaPool() As Boolean
        Dim oldestKey As Long = 0
        Dim oldestEntry As SsaaPoolEntry = Nothing
        For Each kv In _ssaaPool
            If oldestEntry Is Nothing OrElse kv.Value.LastUsed < oldestEntry.LastUsed Then
                oldestKey = kv.Key
                oldestEntry = kv.Value
            End If
        Next
        If oldestEntry Is Nothing Then Return False
        _ssaaPool.Remove(oldestKey)
        _ssaaPoolBytes -= oldestEntry.Bytes
        If _ssaaPoolBytes < 0 Then _ssaaPoolBytes = 0
        ReleaseRenderTargetCaches(oldestEntry.RenderTarget)
        Try : oldestEntry.RenderTarget.Dispose() : Catch : End Try
        Return True
    End Function

    Private Sub ReleaseRenderTargetCaches(rt As ID2D1RenderTarget)
        If rt Is Nothing Then Return
        Try : BrushCache.InvalidateFor(rt) : Catch : End Try
        For Each entry In _bitmapCaches
            Try : entry.Cache.InvalidateFor(rt) : Catch : End Try
        Next
    End Sub

    ''' <summary>
    ''' 开始一次 V2 绘制作用域。内部完成：取 HDC → BindDC → BeginDraw → 应用全局抗锯齿 → 按需创建 SSAA RT。
    ''' 返回的 <see cref="PaintScopeV2"/> 必须 Using 释放（Dispose 内 FlushGraphics + EndDraw + ReleaseHdc + 退出重入标记）。
    ''' 若当前 Form 的共享 DC RT 正在绘制，返回 Nothing，避免嵌套 BindDC 触发 D2DERR_WRONG_STATE。
    ''' </summary>
    Friend Function BeginPaint(e As PaintEventArgs, control As Control, ssaaScale As Integer,
                               Optional disposeCompositorWithScope As Boolean = False,
                               Optional returnCompositorToBackgroundSamplingPool As Boolean = False) As PaintScopeV2
        If _disposed Then Return Nothing
        If _activePaintScopes > 0 Then Return Nothing
        Dim dcRT = GetOrCreateDCRenderTarget()
        If dcRT Is Nothing Then Return Nothing
        Dim hdc As IntPtr = e.Graphics.GetHdc()
        _activePaintScopes += 1
        Try
            Return New PaintScopeV2(Me, e.Graphics, hdc, dcRT, control.Width, control.Height, ssaaScale, e.ClipRectangle,
                                    disposeCompositorWithScope, returnCompositorToBackgroundSamplingPool)
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
        If ShouldTrimTransientCaches() Then TrimTransientCaches()
    End Sub

    Private Function ShouldTrimTransientCaches() As Boolean
        If _disposed Then Return False

        Dim brushLimit As Integer = Math.Max(0, GlobalOptions.BrushCacheLimit)
        Dim textFormatLimit As Integer = Math.Max(0, GlobalOptions.TextFormatCacheLimit)
        Dim gpuBudgetBytes As Long = Math.Max(0L, GlobalOptions.GpuCacheBudgetBytes)

        If brushLimit <> _lastBrushCacheLimit OrElse
           textFormatLimit <> _lastTextFormatCacheLimit OrElse
           gpuBudgetBytes <> _lastGpuBudgetBytes Then
            _lastBrushCacheLimit = brushLimit
            _lastTextFormatCacheLimit = textFormatLimit
            _lastGpuBudgetBytes = gpuBudgetBytes
            _paintScopesSinceTransientTrim = 0
            Return True
        End If

        _paintScopesSinceTransientTrim += 1
        If _paintScopesSinceTransientTrim < TransientCacheTrimInterval Then Return False
        _paintScopesSinceTransientTrim = 0
        Return True
    End Function

    Friend Sub TrimTransientCaches()
        If _disposed Then Return
        Try : BrushCache.TrimToCurrentLimit() : Catch : End Try
        Try : TextFormatCache.TrimToCurrentLimit() : Catch : End Try
        For Each entry In _bitmapCaches
            Try : entry.Cache.TrimToCurrentBudget() : Catch : End Try
        Next
        Try : TrimBitmapCacheIndex(Nothing) : Catch : End Try
        Try : GpuCache.TrimToBudget() : Catch : End Try
    End Sub

    Private Sub ResetTransientTrimState()
        _paintScopesSinceTransientTrim = 0
        _lastBrushCacheLimit = Integer.MinValue
        _lastTextFormatCacheLimit = Integer.MinValue
        _lastGpuBudgetBytes = Long.MinValue
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        Try
            RemoveHandler _form.HandleCreated, AddressOf OnFormHandleCreated
        Catch
        End Try
        Try
            RemoveHandler _form.HandleDestroyed, AddressOf OnFormHandleDestroyed
        Catch
        End Try
        Try
            RemoveHandler _form.DpiChanged, AddressOf OnFormDpiChanged
        Catch
        End Try
        If _unregisterOnDispose Then
            Try
                D2DGlobals.ClearWindowDpi(_formHwnd)
            Catch
            End Try
        End If
        Try : BrushCache.Dispose() : Catch : End Try
        Try : TextFormatCache.Dispose() : Catch : End Try
        For Each entry In _bitmapCaches
            Try : entry.Cache.Dispose() : Catch : End Try
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

    Public ReadOnly Property CacheBytes As Long Implements IRenderCacheOwner.CacheBytes
        Get
            Return _ssaaPoolBytes
        End Get
    End Property

    Public ReadOnly Property OldestUseTick As Long Implements IRenderCacheOwner.OldestUseTick
        Get
            Dim oldest As Long = Long.MaxValue
            For Each entry In _ssaaPool.Values
                If entry IsNot Nothing AndAlso entry.LastUsed < oldest Then oldest = entry.LastUsed
            Next
            Return oldest
        End Get
    End Property

    Public Function TrimOldest() As Boolean Implements IRenderCacheOwner.TrimOldest
        Return TrimSsaaPool()
    End Function

    Public Sub ReleaseAll() Implements IRenderCacheOwner.ReleaseAll
        ReleaseSsaaPoolNoLock()
    End Sub
End Class
