Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Numerics
Imports System.Runtime.InteropServices
Imports System.Threading
Imports Vortice.Direct2D1

''' <summary>
''' 毛玻璃 / 亚克力效果的核心渲染器。
''' 按需抓取桌面 DC（窗口背后区域）→ 下采样 → 缓存为源帧；
''' Paint 时优先用 D2D 1.1 GaussianBlur 在 GPU 上处理并贴回 GDI HDC。
''' 通过 <see cref="SetWindowDisplayAffinity"/>（Win10 19041+）确保自身不被拍到。
'''
''' === 线程模型 ===
''' • UI 线程：构造、<see cref="ApplyParameters"/>、<see cref="SetSource"/>、<see cref="RequestFrame"/>、
'''   <see cref="DrawTo(Graphics, Rectangle)"/>、<see cref="DrawTo(ID2D1RenderTarget, Rectangle)"/>、
'''   <see cref="DrawNoise(Graphics, Rectangle, Byte)"/>、<see cref="DrawNoise(ID2D1RenderTarget, Rectangle, Byte)"/>、
'''   <see cref="Dispose"/>。
''' • ThreadPool 工作项：<see cref="DrainPendingFrames"/> + <see cref="ProcessFrame"/>，永不直接访问
'''   <c>_host</c> 的 Handle / IsHandleCreated / IsDisposed（WinForms 会触发跨线程检查），仅使用构造时
'''   缓存的 <c>_hostHandle</c>；最终回 UI 线程靠 <c>_host.BeginInvoke</c>。
''' • 双缓冲交换：UI 读 <c>_currentFrame</c> 源帧，worker 写 <c>_spareFrame</c>，<c>_frameLock</c> 内交换。
''' • Pending 请求：UI 线程在 <c>_pendingLock</c> 内一次性写齐 4 个坐标 + commit 标志；worker
'''   在同一锁内做原子快照，避免坐标分量撕裂；commit 走 sticky 语义（已挂的 true 不会被 false 覆盖）。
''' • 帧版本：每次 worker 交换 _currentFrame 时 <c>Interlocked.Increment(_frameVersion)</c>，
'''   UI 端 D2D 上传缓存据此判断是否要重传。
'''
''' === 关于 D2D 替代 ===
''' • 抓桌面 DC：无 D2D 等价物，仍然必须 <c>BitBlt</c>（DXGI Desktop Duplication 不在本项目权衡范围内）。
''' • blur：优先走 <c>ID2D1Effect</c> + <c>CLSID_D2D1GaussianBlur</c>。由于 V2 的 DC RT 与 D2D 1.1
'''   DeviceContext 在阶段 A 不共享资源，结果通过 GDI-compatible target 的 HDC 贴回；设备不可用时降级 CPU box blur。
''' • <see cref="DrawTo(ID2D1RenderTarget, Rectangle)"/> / <see cref="DrawNoise(ID2D1RenderTarget, Rectangle, Byte)"/>：
'''   已迁移到 D2D。<see cref="ThisIsYourWindow"/> 的标题栏 V2 scope 内优先调用 D2D 重载，
'''   非 V2 路径（或 V2 fallback）仍保留 GDI 重载。
''' • <see cref="ComputeAverage"/>：直接对下采样源帧取平均，避免为了边框/阴影自动色额外回读 GPU。
'''
''' === D2D 资源缓存 ===
''' • <c>_frameD2DBitmap</c>：按 (RT, frameVersion) 失效；帧变 / RT 变才重传。
''' • <c>_noiseD2DBitmap</c> + <c>_noiseD2DBrush</c>：源噪点 128×128 终身不变，brush 按 RT 缓存；
'''   opacity 与 tile scale 每帧便宜地通过 brush.Opacity / brush.Transform 设置，不会引起重建。
''' </summary>
Friend NotInheritable Class BackdropRenderer
    Implements IDisposable

#Region "Win32"

    <DllImport("user32.dll")>
    Private Shared Function GetDC(hWnd As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function ReleaseDC(hWnd As IntPtr, hdc As IntPtr) As Integer
    End Function

    <DllImport("gdi32.dll")>
    Private Shared Function BitBlt(hdcDst As IntPtr, x As Integer, y As Integer,
                                    w As Integer, h As Integer,
                                    hdcSrc As IntPtr, x1 As Integer, y1 As Integer,
                                    rop As Integer) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SetWindowDisplayAffinity(hWnd As IntPtr, dwAffinity As Integer) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    Private Const WDA_NONE As Integer = &H0
    Private Const WDA_EXCLUDEFROMCAPTURE As Integer = &H11

    Private Const SRCCOPY As Integer = &HCC0020

#End Region

#Region "字段"

    Private ReadOnly _host As Form
    ' 在 UI 线程构造时缓存的窗口句柄。后台线程绝不能访问 _host.Handle / IsHandleCreated /
    ' IsDisposed —— 那会触发 WinForms 跨线程检查（InvalidOperationException）。
    Private _hostHandle As IntPtr
    Private ReadOnly _workerIdle As New ManualResetEventSlim(True)
    Private ReadOnly _workerLock As New Object()
    Private _disposed As Integer = 0
    Private _workerScheduled As Integer = 0

    ' 0 = Desktop 抓屏；1 = Image 静态源图
    Private _sourceMode As Integer = 0
    Private ReadOnly _sourceLock As New Object()
    Private _sourceImage As Image

    ' 抓屏期间是否临时启用 WDA_EXCLUDEFROMCAPTURE 防止抓到自身。
    ' 当宿主长期保持 WDA_NONE（允许系统截图截到本窗口）时，
    ' 必须在 BitBlt 桌面 DC 的瞬间临时排除自身，否则会抓到镜像反馈。
    Private _transientExcludeOnCapture As Integer = 0

    ' ── 参数（运行时可被 UI 线程修改，工作线程读取）──
    Private _radius As Integer = 24
    Private _passes As Integer = 3
    Private _downsample As Integer = 4
    Private _noiseScale As Single = 1.0F
    Private _effectSettingsVersion As Integer = 0

    ' ── 待处理请求（最新覆盖最旧，永不堆积）──
    ' 全部字段写读均需在 _pendingLock 内：
    '   • UI 线程一次 RequestFrame 写 4 个坐标 + commit + has 标记。
    '   • Worker 线程在 DrainPendingFrames 内一次性快照 + 清零。
    ' 否则可能出现 UI 写到一半 worker 就读 → 边界撕裂（X 是新值，Y 还是旧值）。
    Private ReadOnly _pendingLock As New Object()
    Private _pendingX As Integer
    Private _pendingY As Integer
    Private _pendingW As Integer
    Private _pendingH As Integer
    Private _pendingCommitAverage As Integer   ' sticky：一旦置 1，未消费前不会被覆盖回 0
    Private _hasPending As Integer

    ' ── 当前帧（双缓冲：UI 读 _currentFrame，Worker 写 _spareFrame，结束后在 _frameLock 内交换）──
    Private ReadOnly _frameLock As New Object()
    Private _currentFrame As Bitmap
    Private _spareFrame As Bitmap
    ' 每次 worker 在 _frameLock 内交换 _currentFrame 时自增。UI 端 D2D 上传缓存据此判断是否要重传。
    Private _frameVersion As Integer

    ' ── 当前帧的 D2D 上传缓存（UI 线程独占；按 (RT, frameVersion) 失效）──
    ' 路径：CPU 生成的 _currentFrame (Format32bppPArgb) → ID2D1Bitmap，
    ' 在 DrawTo(rt, target) 命中时仅当 frameVersion 变化或 RT 变化才重传。
    Private _frameD2DBitmap As ID2D1Bitmap
    Private _frameD2DOwnerRT As WeakReference
    Private _frameD2DUploadedVersion As Integer = -1

    ' ── D2D 1.1 GPU blur 缓存（UI 线程独占；设备丢失或目标尺寸变化时重建）──
    Private _gpuContext As ID2D1DeviceContext
    Private _gpuGeneration As Integer = -1
    Private _gpuTarget As ID2D1Bitmap1
    Private _gpuTargetSize As Size = Size.Empty
    Private _gpuSource As ID2D1Bitmap1
    Private _gpuSourceVersion As Integer = -1
    Private _gpuBlurEffect As ID2D1Effect

    ' GPU 不可用时的 CPU 降级帧；不再暴露并行度，始终使用处理器核心数。
    Private _cpuBlurredFrame As Bitmap
    Private _cpuBlurredSourceVersion As Integer = -1
    Private _cpuBlurredSettingsVersion As Integer = -1

    ' ── 抓屏临时位图复用（仅 Auto 模式使用，尺寸 = 窗口逻辑尺寸）──
    Private _capturedBitmap As Bitmap

    ' ── BoxBlur 字节缓冲复用（按 stride*h 容量增长，永不缩小）──
    Private _blurBufferA() As Byte
    Private _blurBufferB() As Byte

    ' ── 平均色 ──
    Private _candidateAverage As Integer
    Private _publishedAverage As Integer = -1

    ' ── 噪点 ──
    Private _noiseBitmap As Bitmap
    ' ── 噪点平铺笔刷缓存（GDI 路径，按 (opacity, tile) 失效）──
    Private _noiseBrush As TextureBrush
    Private _noiseBrushOpacity As Byte = 0
    Private _noiseBrushTile As Integer = 0

    ' ── 噪点的 D2D 上传缓存（UI 线程独占；按 RT 失效）──
    ' 源 _noiseBitmap 终身不变，因此只要 RT 不变就长期复用。
    ' brush 的 Wrap 模式 + ScaleTransform(tile) + Opacity 都在 DrawNoise 时按需更新，
    ' 不需要为不同 (opacity, tile) 维持多份 brush。
    Private _noiseD2DBitmap As ID2D1Bitmap
    Private _noiseD2DBrush As ID2D1BitmapBrush
    Private _noiseD2DOwnerRT As WeakReference
    Private _noiseD2DBrushTile As Integer = 0

#End Region

    ''' <summary>当一帧 commit（覆盖发布平均色）时在 UI 线程触发。用于通知阴影刷新。</summary>
    Public Event AverageCommitted As EventHandler

    Public Sub New(host As Form)
        _host = host
        ' 必须在 UI 线程读取 Handle（会强制创建句柄）。
        ' 之后后台线程通过 _hostHandle 字段访问，避免 InvalidOperationException。
        If host IsNot Nothing Then
            Try
                _hostHandle = host.Handle
            Catch
                _hostHandle = IntPtr.Zero
            End Try
        End If
    End Sub

    Public Sub ApplyParameters(radius As Integer, passes As Integer, downsample As Integer,
                                noiseScale As Single)
        Volatile.Write(_radius, Math.Max(1, radius))
        Volatile.Write(_passes, Math.Max(0, Math.Min(5, passes)))
        Volatile.Write(_downsample, Math.Max(1, downsample))
        _noiseScale = Math.Max(0.1F, noiseScale)
        Interlocked.Increment(_effectSettingsVersion)
        InvalidateDerivedFrameCaches()
    End Sub

    ''' <summary>设置渲染来源：Desktop 抓屏 或 Image 静态源图（按 cover 撑满窗口）。</summary>
    Public Sub SetSource(useImage As Boolean, image As Image)
        Volatile.Write(_sourceMode, If(useImage, 1, 0))
        SyncLock _sourceLock
            _sourceImage = image
        End SyncLock
        ' 切换源后已发布的平均色不再可信
        Volatile.Write(_publishedAverage, -1)
    End Sub

    ''' <summary>
    ''' 配置 Auto 抓屏期间是否临时启用 WDA_EXCLUDEFROMCAPTURE。
    ''' 当宿主长期 WDA_NONE 但又使用 Auto 抓屏时必须开启，否则 BitBlt 会抓到自身产生反馈纹路。
    ''' </summary>
    Public Sub SetTransientExcludeOnCapture(value As Boolean)
        Volatile.Write(_transientExcludeOnCapture, If(value, 1, 0))
    End Sub

    Private Sub InvalidateDerivedFrameCaches()
        If _cpuBlurredFrame IsNot Nothing Then
            Try : _cpuBlurredFrame.Dispose() : Catch : End Try
            _cpuBlurredFrame = Nothing
        End If
        _cpuBlurredSourceVersion = -1
        _cpuBlurredSettingsVersion = -1

        If _frameD2DBitmap IsNot Nothing Then
            Try : _frameD2DBitmap.Dispose() : Catch : End Try
            _frameD2DBitmap = Nothing
        End If
        _frameD2DOwnerRT = Nothing
        _frameD2DUploadedVersion = -1

        If _gpuBlurEffect IsNot Nothing Then
            Try : _gpuBlurEffect.Dispose() : Catch : End Try
            _gpuBlurEffect = Nothing
        End If
    End Sub

    Private Sub DisposeGpuResources()
        If _gpuBlurEffect IsNot Nothing Then
            Try : _gpuBlurEffect.Dispose() : Catch : End Try
            _gpuBlurEffect = Nothing
        End If
        If _gpuSource IsNot Nothing Then
            Try : _gpuSource.Dispose() : Catch : End Try
            _gpuSource = Nothing
        End If
        If _gpuTarget IsNot Nothing Then
            Try : _gpuTarget.Dispose() : Catch : End Try
            _gpuTarget = Nothing
        End If
        If _gpuContext IsNot Nothing Then
            Try : _gpuContext.Dispose() : Catch : End Try
            _gpuContext = Nothing
        End If
        _gpuSourceVersion = -1
        _gpuTargetSize = Size.Empty
        _gpuGeneration = -1
    End Sub

    Public ReadOnly Property HasFrame As Boolean
        Get
            SyncLock _frameLock
                Return _currentFrame IsNot Nothing
            End SyncLock
        End Get
    End Property

    ''' <summary>请求一帧抓屏 + 模糊。多次调用只保留最新一组 bounds；commit 标志按"或"合并不会丢。</summary>
    ''' <param name="formBounds">屏幕坐标系下的窗体外接矩形（用于 BitBlt 抓桌面 DC 的源 rect）。</param>
    ''' <param name="commitAverage">
    ''' 是否将本帧平均色发布为"已稳定"——仅在首帧 / 拖动结束帧使用。
    ''' Sticky 语义：若已有未消费的 commit=true 请求，再来的 commit=false 不会清掉它。
    ''' </param>
    Public Sub RequestFrame(formBounds As Rectangle, commitAverage As Boolean)
        If Volatile.Read(_disposed) <> 0 Then Return
        SyncLock _pendingLock
            _pendingX = formBounds.X
            _pendingY = formBounds.Y
            _pendingW = formBounds.Width
            _pendingH = formBounds.Height
            If commitAverage Then _pendingCommitAverage = 1
            _hasPending = 1
        End SyncLock
        ScheduleWorker()
    End Sub

    Private Sub ScheduleWorker()
        If Volatile.Read(_disposed) <> 0 Then Return
        SyncLock _workerLock
            If Volatile.Read(_disposed) <> 0 Then Return
            If Interlocked.CompareExchange(_workerScheduled, 1, 0) <> 0 Then Return
            _workerIdle.Reset()
            ThreadPool.QueueUserWorkItem(AddressOf DrainPendingFrames)
        End SyncLock
    End Sub

    ''' <summary>
    ''' Worker 主循环：原子快照 pending 状态后处理一帧；处理完循环检查是否又有新 pending。
    ''' 关键不变量：bounds 的 4 个分量 + commitAverage + hasPending 必须在 _pendingLock 内
    ''' 同时读出并清零，避免与 UI 线程的多字段写入交错产生坐标撕裂。
    ''' </summary>
    Private Sub DrainPendingFrames(state As Object)
        Try
            Do While Volatile.Read(_disposed) = 0
                Dim bounds As Rectangle
                Dim commitAvg As Boolean
                SyncLock _pendingLock
                    If _hasPending = 0 Then Exit Do
                    bounds = New Rectangle(_pendingX, _pendingY, _pendingW, _pendingH)
                    commitAvg = (_pendingCommitAverage <> 0)
                    _pendingCommitAverage = 0
                    _hasPending = 0
                End SyncLock
                Try
                    ProcessFrame(bounds, commitAvg)
                Catch
                    ' 后台工作项绝不能抛出
                End Try
            Loop
        Finally
            Volatile.Write(_workerScheduled, 0)
            ' 退出循环到上面 _workerScheduled=0 之间，UI 线程可能又投递了新 pending；
            ' 不需要锁，_hasPending 这里的脏读最差只会触发一次额外的 ScheduleWorker（幂等）。
            If Volatile.Read(_disposed) = 0 AndAlso Volatile.Read(_hasPending) <> 0 Then
                ScheduleWorker()
            Else
                _workerIdle.Set()
            End If
        End Try
    End Sub

    Private Sub ProcessFrame(bounds As Rectangle, commitAvg As Boolean)
        Dim w As Integer = bounds.Width
        Dim h As Integer = bounds.Height
        If w < 4 OrElse h < 4 Then Return

        Dim passes As Integer = Math.Max(0, Volatile.Read(_passes))
        Dim down As Integer = If(passes <= 0, 1, Volatile.Read(_downsample))
        Dim dw As Integer = Math.Max(2, w \ down)
        Dim dh As Integer = Math.Max(2, h \ down)

        ' 1+2. 准备下采样源 —— 复用 _spareFrame 作为下采样目标，避免每帧两次大位图分配。
        Dim small As Bitmap = AcquireSpareFrame(dw, dh)
        Dim useImage As Boolean = (Volatile.Read(_sourceMode) = 1)
        If useImage Then
            ' 从静态源图按 cover 撑满 → 直接缩到 dw×dh
            Dim src As Image
            SyncLock _sourceLock
                src = _sourceImage
            End SyncLock
            If src Is Nothing Then Return
            Try
                Using gs As Graphics = Graphics.FromImage(small)
                    gs.CompositingMode = CompositingMode.SourceCopy
                    gs.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear
                    gs.PixelOffsetMode = PixelOffsetMode.HighQuality
                    Dim sw As Integer, sh As Integer
                    SyncLock src
                        sw = src.Width
                        sh = src.Height
                    End SyncLock
                    If sw < 1 OrElse sh < 1 Then Return
                    ' cover：保持比例放大到刚好覆盖目标，再居中裁切
                    Dim ratio As Double = Math.Max(dw / CDbl(sw), dh / CDbl(sh))
                    Dim drawW As Integer = CInt(Math.Ceiling(sw * ratio))
                    Dim drawH As Integer = CInt(Math.Ceiling(sh * ratio))
                    Dim dx As Integer = (dw - drawW) \ 2
                    Dim dy As Integer = (dh - drawH) \ 2
                    SyncLock src
                        gs.DrawImage(src, New Rectangle(dx, dy, drawW, drawH))
                    End SyncLock
                End Using
            Catch
                Return
            End Try
        Else
            ' 从桌面 DC 抓取（_capturedBitmap 按需扩容复用）
            Dim captured As Bitmap = AcquireCaptureBitmap(w, h)
            If captured Is Nothing Then Return
            ' 若宿主长期 WDA_NONE（允许系统截图截到本窗口），必须在 BitBlt 瞬间
            ' 临时启用 WDA_EXCLUDEFROMCAPTURE，否则桌面 DC 会包含本窗口自身。
            ' 注意：必须使用构造时缓存的 _hostHandle，绝不能在后台线程访问 _host.Handle。
            Dim needTransientExclude As Boolean = (Volatile.Read(_transientExcludeOnCapture) <> 0)
            Dim hostHandle As IntPtr = _hostHandle
            Dim transientApplied As Boolean = False
            If needTransientExclude AndAlso hostHandle <> IntPtr.Zero Then
                transientApplied = SetWindowDisplayAffinity(hostHandle, WDA_EXCLUDEFROMCAPTURE)
            End If
            Try
                Using gCap As Graphics = Graphics.FromImage(captured)
                    Dim screenDC As IntPtr = GetDC(IntPtr.Zero)
                    If screenDC = IntPtr.Zero Then Return
                    Try
                        Dim hdc As IntPtr = gCap.GetHdc()
                        Try
                            BitBlt(hdc, 0, 0, w, h, screenDC, bounds.X, bounds.Y, SRCCOPY)
                        Finally
                            gCap.ReleaseHdc(hdc)
                        End Try
                    Finally
                        ReleaseDC(IntPtr.Zero, screenDC)
                    End Try
                End Using
                Using gs As Graphics = Graphics.FromImage(small)
                    gs.CompositingMode = CompositingMode.SourceCopy
                    gs.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear
                    gs.PixelOffsetMode = PixelOffsetMode.HighQuality
                    gs.DrawImage(captured, New Rectangle(0, 0, dw, dh))
                End Using
            Catch
                Return
            Finally
                ' 立即恢复原状（WDA_NONE）。若设置失败也尝试恢复，避免出现意外残留。
                If transientApplied Then
                    SetWindowDisplayAffinity(hostHandle, WDA_NONE)
                End If
            End Try
        End If

        ' 3. 平均色（直接对下采样源帧采样；blur 不改变整体平均色，避免为了自动色回读 GPU。）
        Dim avg As Integer = ComputeAverage(small)
        Volatile.Write(_candidateAverage, avg)
        Dim publishedNow As Boolean = False
        If commitAvg OrElse Volatile.Read(_publishedAverage) = -1 Then
            Volatile.Write(_publishedAverage, avg)
            publishedNow = True
        End If

        ' 4. 交换前后帧：旧的 _currentFrame 退役为下次的 _spareFrame，避免分配。
        '    同时递增 _frameVersion，让 UI 侧 D2D 上传缓存知道需要重传。
        SyncLock _frameLock
            Dim previousCurrent As Bitmap = _currentFrame
            _currentFrame = small
            _spareFrame = previousCurrent
            Interlocked.Increment(_frameVersion)
        End SyncLock

        ' 5. 触发 UI 重绘
        Try
            Dim host = _host
            If host IsNot Nothing Then
                host.BeginInvoke(CType(Sub()
                                           If host.IsDisposed OrElse Not host.IsHandleCreated Then Return
                                           If publishedNow Then RaiseEvent AverageCommitted(Me, EventArgs.Empty)
                                           host.Invalidate()
                                       End Sub, MethodInvoker))
            End If
        Catch
        End Try
    End Sub

    ''' <summary>取得（必要时分配）合适尺寸的备用下采样位图。</summary>
    Private Function AcquireSpareFrame(dw As Integer, dh As Integer) As Bitmap
        Dim bmp As Bitmap
        SyncLock _frameLock
            bmp = _spareFrame
            _spareFrame = Nothing
        End SyncLock
        If bmp IsNot Nothing AndAlso bmp.Width = dw AndAlso bmp.Height = dh Then
            Return bmp
        End If
        bmp?.Dispose()
        Return New Bitmap(dw, dh, PixelFormat.Format32bppPArgb)
    End Function

    ''' <summary>取得（必要时分配）抓屏临时位图。</summary>
    Private Function AcquireCaptureBitmap(w As Integer, h As Integer) As Bitmap
        Dim bmp As Bitmap = _capturedBitmap
        If bmp IsNot Nothing AndAlso bmp.Width = w AndAlso bmp.Height = h Then Return bmp
        bmp?.Dispose()
        Try
            _capturedBitmap = New Bitmap(w, h, PixelFormat.Format32bppRgb)
        Catch
            _capturedBitmap = Nothing
        End Try
        Return _capturedBitmap
    End Function

#Region "Box Blur"

    Private Sub BoxBlur(bmp As Bitmap, radius As Integer, passes As Integer, parallelism As Integer)
        If radius < 1 OrElse passes < 1 Then Return
        Dim w As Integer = bmp.Width
        Dim h As Integer = bmp.Height
        Dim rect As New Rectangle(0, 0, w, h)
        Dim data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb)
        Try
            Dim stride As Integer = data.Stride
            Dim len As Integer = stride * h
            ' 复用缓冲，按需扩容
            If _blurBufferA Is Nothing OrElse _blurBufferA.Length < len Then ReDim _blurBufferA(len - 1)
            If _blurBufferB Is Nothing OrElse _blurBufferB.Length < len Then ReDim _blurBufferB(len - 1)
            Dim src() As Byte = _blurBufferA
            Dim dst() As Byte = _blurBufferB
            Marshal.Copy(data.Scan0, src, 0, len)

            Dim useParallel As Boolean = parallelism > 1 AndAlso (w * h) >= 8192
            Dim pOpts As ParallelOptions = Nothing
            If useParallel Then pOpts = New ParallelOptions With {.MaxDegreeOfParallelism = Math.Max(1, parallelism)}

            For pass As Integer = 1 To passes
                ' 水平：src → dst
                If useParallel Then
                    Parallel.For(0, h, pOpts, Sub(y) BoxBlurRowH(src, dst, y, w, stride, radius))
                Else
                    For y As Integer = 0 To h - 1
                        BoxBlurRowH(src, dst, y, w, stride, radius)
                    Next
                End If
                ' 垂直：dst → src
                If useParallel Then
                    Parallel.For(0, w, pOpts, Sub(x) BoxBlurColV(dst, src, x, h, stride, radius))
                Else
                    For x As Integer = 0 To w - 1
                        BoxBlurColV(dst, src, x, h, stride, radius)
                    Next
                End If
            Next

            Marshal.Copy(src, 0, data.Scan0, len)
        Finally
            bmp.UnlockBits(data)
        End Try
    End Sub

    ''' <summary>
    ''' 水平 box blur 一行：使用滑动窗口 +clamp 拆头/中/尾三段，
    ''' 内层（中段）取消 if 边界判定以提升 JIT 矢量化与分支预测命中率。
    ''' </summary>
    Private Shared Sub BoxBlurRowH(src() As Byte, dst() As Byte, y As Integer, w As Integer, stride As Integer, r As Integer)
        Dim rowOff As Integer = y * stride
        Dim windowSize As Integer = r * 2 + 1
        Dim sumB As Integer = 0, sumG As Integer = 0, sumR As Integer = 0, sumA As Integer = 0
        ' 初始化窗口（全部 clamp 到 [0, w-1]）
        For i As Integer = -r To r
            Dim xi As Integer = If(i < 0, 0, If(i >= w, w - 1, i))
            Dim o As Integer = rowOff + xi * 4
            sumB += src(o) : sumG += src(o + 1) : sumR += src(o + 2) : sumA += src(o + 3)
        Next

        Dim rightBoundary As Integer = w - 1
        Dim wMid As Integer = w - r - 1   ' xAdd = x + r + 1 < w 等价于 x < wMid

        Dim x As Integer = 0
        ' 头段：xRem 可能 < 0
        Dim headEnd As Integer = Math.Min(r, w - 1)
        Do While x <= headEnd
            Dim o As Integer = rowOff + x * 4
            dst(o) = CByte(sumB \ windowSize)
            dst(o + 1) = CByte(sumG \ windowSize)
            dst(o + 2) = CByte(sumR \ windowSize)
            dst(o + 3) = CByte(sumA \ windowSize)
            Dim xRem As Integer = x - r
            If xRem < 0 Then xRem = 0
            Dim xAdd As Integer = x + r + 1
            If xAdd > rightBoundary Then xAdd = rightBoundary
            Dim oRem As Integer = rowOff + xRem * 4
            Dim oAdd As Integer = rowOff + xAdd * 4
            sumB += CInt(src(oAdd)) - CInt(src(oRem))
            sumG += CInt(src(oAdd + 1)) - CInt(src(oRem + 1))
            sumR += CInt(src(oAdd + 2)) - CInt(src(oRem + 2))
            sumA += CInt(src(oAdd + 3)) - CInt(src(oRem + 3))
            x += 1
        Loop

        ' 中段：xRem ≥ 0 且 xAdd ≤ w-1（无边界判定）
        Do While x < wMid
            Dim o As Integer = rowOff + x * 4
            dst(o) = CByte(sumB \ windowSize)
            dst(o + 1) = CByte(sumG \ windowSize)
            dst(o + 2) = CByte(sumR \ windowSize)
            dst(o + 3) = CByte(sumA \ windowSize)
            Dim oRem As Integer = rowOff + (x - r) * 4
            Dim oAdd As Integer = rowOff + (x + r + 1) * 4
            sumB += CInt(src(oAdd)) - CInt(src(oRem))
            sumG += CInt(src(oAdd + 1)) - CInt(src(oRem + 1))
            sumR += CInt(src(oAdd + 2)) - CInt(src(oRem + 2))
            sumA += CInt(src(oAdd + 3)) - CInt(src(oRem + 3))
            x += 1
        Loop

        ' 尾段：xAdd 可能 ≥ w
        Do While x < w
            Dim o As Integer = rowOff + x * 4
            dst(o) = CByte(sumB \ windowSize)
            dst(o + 1) = CByte(sumG \ windowSize)
            dst(o + 2) = CByte(sumR \ windowSize)
            dst(o + 3) = CByte(sumA \ windowSize)
            Dim xRem As Integer = x - r
            If xRem < 0 Then xRem = 0
            Dim xAdd As Integer = x + r + 1
            If xAdd > rightBoundary Then xAdd = rightBoundary
            Dim oRem As Integer = rowOff + xRem * 4
            Dim oAdd As Integer = rowOff + xAdd * 4
            sumB += CInt(src(oAdd)) - CInt(src(oRem))
            sumG += CInt(src(oAdd + 1)) - CInt(src(oRem + 1))
            sumR += CInt(src(oAdd + 2)) - CInt(src(oRem + 2))
            sumA += CInt(src(oAdd + 3)) - CInt(src(oRem + 3))
            x += 1
        Loop
    End Sub

    ''' <summary>垂直 box blur 一列：同样拆头/中/尾三段以减少内层分支。</summary>
    Private Shared Sub BoxBlurColV(src() As Byte, dst() As Byte, x As Integer, h As Integer, stride As Integer, r As Integer)
        Dim colOff As Integer = x * 4
        Dim windowSize As Integer = r * 2 + 1
        Dim sumB As Integer = 0, sumG As Integer = 0, sumR As Integer = 0, sumA As Integer = 0
        For i As Integer = -r To r
            Dim yi As Integer = If(i < 0, 0, If(i >= h, h - 1, i))
            Dim o As Integer = yi * stride + colOff
            sumB += src(o) : sumG += src(o + 1) : sumR += src(o + 2) : sumA += src(o + 3)
        Next

        Dim bottomBoundary As Integer = h - 1
        Dim hMid As Integer = h - r - 1

        Dim y As Integer = 0
        Dim headEnd As Integer = Math.Min(r, h - 1)
        Do While y <= headEnd
            Dim o As Integer = y * stride + colOff
            dst(o) = CByte(sumB \ windowSize)
            dst(o + 1) = CByte(sumG \ windowSize)
            dst(o + 2) = CByte(sumR \ windowSize)
            dst(o + 3) = CByte(sumA \ windowSize)
            Dim yRem As Integer = y - r
            If yRem < 0 Then yRem = 0
            Dim yAdd As Integer = y + r + 1
            If yAdd > bottomBoundary Then yAdd = bottomBoundary
            Dim oRem As Integer = yRem * stride + colOff
            Dim oAdd As Integer = yAdd * stride + colOff
            sumB += CInt(src(oAdd)) - CInt(src(oRem))
            sumG += CInt(src(oAdd + 1)) - CInt(src(oRem + 1))
            sumR += CInt(src(oAdd + 2)) - CInt(src(oRem + 2))
            sumA += CInt(src(oAdd + 3)) - CInt(src(oRem + 3))
            y += 1
        Loop

        Do While y < hMid
            Dim o As Integer = y * stride + colOff
            dst(o) = CByte(sumB \ windowSize)
            dst(o + 1) = CByte(sumG \ windowSize)
            dst(o + 2) = CByte(sumR \ windowSize)
            dst(o + 3) = CByte(sumA \ windowSize)
            Dim oRem As Integer = (y - r) * stride + colOff
            Dim oAdd As Integer = (y + r + 1) * stride + colOff
            sumB += CInt(src(oAdd)) - CInt(src(oRem))
            sumG += CInt(src(oAdd + 1)) - CInt(src(oRem + 1))
            sumR += CInt(src(oAdd + 2)) - CInt(src(oRem + 2))
            sumA += CInt(src(oAdd + 3)) - CInt(src(oRem + 3))
            y += 1
        Loop

        Do While y < h
            Dim o As Integer = y * stride + colOff
            dst(o) = CByte(sumB \ windowSize)
            dst(o + 1) = CByte(sumG \ windowSize)
            dst(o + 2) = CByte(sumR \ windowSize)
            dst(o + 3) = CByte(sumA \ windowSize)
            Dim yRem As Integer = y - r
            If yRem < 0 Then yRem = 0
            Dim yAdd As Integer = y + r + 1
            If yAdd > bottomBoundary Then yAdd = bottomBoundary
            Dim oRem As Integer = yRem * stride + colOff
            Dim oAdd As Integer = yAdd * stride + colOff
            sumB += CInt(src(oAdd)) - CInt(src(oRem))
            sumG += CInt(src(oAdd + 1)) - CInt(src(oRem + 1))
            sumR += CInt(src(oAdd + 2)) - CInt(src(oRem + 2))
            sumA += CInt(src(oAdd + 3)) - CInt(src(oRem + 3))
            y += 1
        Loop
    End Sub

#End Region

    ''' <summary>
    ''' 直接对模糊后的小图采样取平均色。
    ''' 模糊后图像本身已是低频，无需再过一次 GDI+ 1×1 双线性缩放（那一步开销远高于这里的逐像素均值）。
    ''' 当像素总数较大时按 4 的步长抽样，仍然提供稳定且代表性的均值。
    ''' </summary>
    Private Function ComputeAverage(bmp As Bitmap) As Integer
        Dim w As Integer = bmp.Width
        Dim h As Integer = bmp.Height
        Dim rect As New Rectangle(0, 0, w, h)
        Dim data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb)
        Try
            Dim stride As Integer = data.Stride
            Dim len As Integer = stride * h
            ' 复用 _blurBufferA 作为读取缓冲：BoxBlur 结束后此数组容量必然 ≥ len。
            If _blurBufferA Is Nothing OrElse _blurBufferA.Length < len Then ReDim _blurBufferA(len - 1)
            Dim buf() As Byte = _blurBufferA
            Marshal.Copy(data.Scan0, buf, 0, len)
            Dim total As Integer = w * h
            ' 像素 ≥ 4096 时步长 = 4，否则步长 = 1。32bppPArgb 字节序：B G R A
            Dim step_ As Integer = If(total >= 4096, 4, 1)
            Dim sumB As Long = 0, sumG As Long = 0, sumR As Long = 0, sumA As Long = 0
            Dim count As Long = 0
            Dim y As Integer = 0
            Do While y < h
                Dim rowOff As Integer = y * stride
                Dim x As Integer = 0
                Do While x < w
                    Dim p As Integer = rowOff + x * 4
                    sumB += buf(p)
                    sumG += buf(p + 1)
                    sumR += buf(p + 2)
                    sumA += buf(p + 3)
                    count += 1
                    x += step_
                Loop
                y += step_
            Loop
            If count = 0 Then Return 0
            Dim a As Integer = CInt(sumA \ count)
            Dim r As Integer = CInt(sumR \ count)
            Dim g As Integer = CInt(sumG \ count)
            Dim b As Integer = CInt(sumB \ count)
            ' 32bppPArgb 是预乘的，反演到非预乘以保持 DeriveBorder/ShadowColor 行为一致
            If a >= 16 AndAlso a < 255 Then
                r = Math.Min(255, CInt(r * 255 \ a))
                g = Math.Min(255, CInt(g * 255 \ a))
                b = Math.Min(255, CInt(b * 255 \ a))
            End If
            Return (&HFF << 24) Or (r << 16) Or (g << 8) Or b
        Finally
            bmp.UnlockBits(data)
        End Try
    End Function

    ''' <summary>把当前缓存帧拉伸绘制到目标矩形（覆盖整个客户区，含标题栏）。</summary>
    Public Sub DrawTo(g As Graphics, target As Rectangle)
        If g Is Nothing OrElse target.Width <= 0 OrElse target.Height <= 0 Then Return
        If TryDrawGpuBlur(g, target) Then Return
        DrawCpuFrame(g, target)
    End Sub

    Private Function TryDrawGpuBlur(g As Graphics, target As Rectangle) As Boolean
        If Volatile.Read(_disposed) <> 0 Then Return False
        Dim sourceSize As Size = Size.Empty
        Try
            If Not EnsureGpuContext() Then Return False
            If Not EnsureGpuSource(sourceSize) Then Return False
            If Not EnsureGpuTarget(target.Size) Then Return False

            Dim previousTarget As ID2D1Image = Nothing
            Dim interop As ID2D1GdiInteropRenderTarget = Nothing
            Dim sourceHdc As IntPtr = IntPtr.Zero
            Dim destHdc As IntPtr = IntPtr.Zero
            Dim drawing As Boolean = False

            Try
                previousTarget = _gpuContext.Target
                _gpuContext.Target = _gpuTarget
                _gpuContext.BeginDraw()
                drawing = True
                _gpuContext.Clear(New Vortice.Mathematics.Color4(0, 0, 0, 0))

                Dim oldTransform As Matrix3x2 = _gpuContext.Transform
                Dim sx As Single = target.Width / CSng(Math.Max(1, sourceSize.Width))
                Dim sy As Single = target.Height / CSng(Math.Max(1, sourceSize.Height))
                Try
                    _gpuContext.Transform = Matrix3x2.CreateScale(sx, sy)

                    Dim image As ID2D1Image = GetGpuOutputImage()
                    If image Is Nothing Then Return False
                    Try
                        _gpuContext.DrawImage(image,
                                              New Nullable(Of Vector2)(),
                                              New Nullable(Of Vortice.RawRectF)(),
                                              Vortice.Direct2D1.InterpolationMode.Linear,
                                              CompositeMode.SourceOver)
                    Finally
                        If image IsNot _gpuSource Then
                            Try : image.Dispose() : Catch : End Try
                        End If
                    End Try
                Finally
                    _gpuContext.Transform = oldTransform
                End Try

                interop = _gpuContext.QueryInterface(Of ID2D1GdiInteropRenderTarget)()
                If interop Is Nothing Then Return False

                ' GetDC 必须在 BeginDraw/EndDraw 之间调用；它会隐式 flush 当前 D2D 批次。
                sourceHdc = interop.GetDC(DcInitializeMode.Copy)
                If sourceHdc = IntPtr.Zero Then Return False

                Dim bltOk As Boolean
                Try
                    destHdc = g.GetHdc()
                    If destHdc = IntPtr.Zero Then Return False
                    bltOk = BitBlt(destHdc, target.X, target.Y, target.Width, target.Height,
                                   sourceHdc, 0, 0, SRCCOPY)
                Finally
                    If destHdc <> IntPtr.Zero Then
                        Try : g.ReleaseHdc(destHdc) : Catch : End Try
                        destHdc = IntPtr.Zero
                    End If
                    If sourceHdc <> IntPtr.Zero Then
                        Try : interop.ReleaseDC(Nothing) : Catch : End Try
                        sourceHdc = IntPtr.Zero
                    End If
                End Try

                _gpuContext.EndDraw()
                drawing = False
                Return bltOk
            Finally
                If destHdc <> IntPtr.Zero Then
                    Try : g.ReleaseHdc(destHdc) : Catch : End Try
                End If
                If interop IsNot Nothing AndAlso sourceHdc <> IntPtr.Zero Then
                    Try : interop.ReleaseDC(Nothing) : Catch : End Try
                End If
                If drawing Then
                    Try : _gpuContext.EndDraw() : Catch : End Try
                End If
                If _gpuContext IsNot Nothing Then
                    Try : _gpuContext.Target = previousTarget : Catch : End Try
                End If
                If previousTarget IsNot Nothing Then
                    Try : previousTarget.Dispose() : Catch : End Try
                End If
                If interop IsNot Nothing Then
                    Try : interop.Dispose() : Catch : End Try
                End If
            End Try
        Catch ex As Exception
            If D3D11Globals.HandleDeviceLost(ex) Then DisposeGpuResources()
            Return False
        End Try
    End Function

    Private Function EnsureGpuContext() As Boolean
        If _gpuContext IsNot Nothing AndAlso _gpuGeneration = D3D11Globals.DeviceGeneration Then Return True
        DisposeGpuResources()
        _gpuContext = D3D11Globals.CreateDeviceContext()
        If _gpuContext Is Nothing Then Return False
        _gpuGeneration = D3D11Globals.DeviceGeneration
        Return True
    End Function

    Private Function EnsureGpuSource(ByRef sourceSize As Size) As Boolean
        If _gpuContext Is Nothing Then Return False

        Dim curVer As Integer = Volatile.Read(_frameVersion)
        If _gpuSource IsNot Nothing AndAlso _gpuSourceVersion = curVer Then
            Dim ps = _gpuSource.PixelSize
            sourceSize = New Size(ps.Width, ps.Height)
            Return True
        End If

        If _gpuSource IsNot Nothing Then
            Try : _gpuSource.Dispose() : Catch : End Try
            _gpuSource = Nothing
        End If
        If _gpuBlurEffect IsNot Nothing Then
            Try : _gpuBlurEffect.Dispose() : Catch : End Try
            _gpuBlurEffect = Nothing
        End If

        SyncLock _frameLock
            If _currentFrame Is Nothing Then Return False
            Dim data As BitmapData = _currentFrame.LockBits(
                New Rectangle(0, 0, _currentFrame.Width, _currentFrame.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppPArgb)
            Try
                Dim props As New BitmapProperties1(
                    New Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                    96.0F, 96.0F,
                    BitmapOptions.None)
                _gpuSource = _gpuContext.CreateBitmap(
                    New Vortice.Mathematics.SizeI(_currentFrame.Width, _currentFrame.Height),
                    data.Scan0, CUInt(data.Stride), props)
                _gpuSourceVersion = curVer
                sourceSize = New Size(_currentFrame.Width, _currentFrame.Height)
            Finally
                _currentFrame.UnlockBits(data)
            End Try
        End SyncLock

        Return _gpuSource IsNot Nothing
    End Function

    Private Function EnsureGpuTarget(size As Size) As Boolean
        If _gpuContext Is Nothing OrElse size.Width <= 0 OrElse size.Height <= 0 Then Return False
        If _gpuTarget IsNot Nothing AndAlso _gpuTargetSize = size Then Return True

        If _gpuTarget IsNot Nothing Then
            Try : _gpuTarget.Dispose() : Catch : End Try
            _gpuTarget = Nothing
        End If
        _gpuTargetSize = Size.Empty

        Dim props As New BitmapProperties1(
            New Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
            96.0F, 96.0F,
            BitmapOptions.Target Or BitmapOptions.GdiCompatible)
        _gpuTarget = _gpuContext.CreateBitmap(
            New Vortice.Mathematics.SizeI(size.Width, size.Height),
            IntPtr.Zero, 0UI, props)
        _gpuTargetSize = size
        Return _gpuTarget IsNot Nothing
    End Function

    Private Function GetGpuOutputImage() As ID2D1Image
        If _gpuSource Is Nothing Then Return Nothing
        Dim passes As Integer = Math.Max(0, Volatile.Read(_passes))
        If passes <= 0 Then Return _gpuSource

        If _gpuBlurEffect Is Nothing Then
            _gpuBlurEffect = _gpuContext.CreateEffect(EffectGuids.GaussianBlur)
            _gpuBlurEffect.SetInput(0UI, _gpuSource, True)
        End If

        Dim down As Integer = Math.Max(1, Volatile.Read(_downsample))
        Dim radius As Single = Math.Max(0.1F, Volatile.Read(_radius) / CSng(down))
        Dim sigma As Single = CSng(Math.Sqrt(passes) * radius / Math.Sqrt(3.0))
        SetEffectFloat(_gpuBlurEffect, "StandardDeviation", Math.Max(0.1F, sigma))
        SetEffectEnum(_gpuBlurEffect, "Optimization", CInt(GaussianBlurOptimization.Balanced))
        SetEffectEnum(_gpuBlurEffect, "BorderMode", CInt(BorderMode.Hard))
        Return _gpuBlurEffect.Output
    End Function

    Private Shared Sub SetEffectFloat(effect As ID2D1Effect, name As String, value As Single)
        Dim bytes = BitConverter.GetBytes(value)
        effect.SetValueByName(name, PropertyType.Float, bytes, CUInt(bytes.Length))
    End Sub

    Private Shared Sub SetEffectEnum(effect As ID2D1Effect, name As String, value As Integer)
        Dim bytes = BitConverter.GetBytes(value)
        effect.SetValueByName(name, PropertyType.[Enum], bytes, CUInt(bytes.Length))
    End Sub

    Private Sub DrawCpuFrame(g As Graphics, target As Rectangle)
        If Math.Max(0, Volatile.Read(_passes)) <= 0 Then
            SyncLock _frameLock
                If _currentFrame Is Nothing Then Return
                DrawBitmapFrame(g, _currentFrame, target)
            End SyncLock
            Return
        End If

        Dim frame As Bitmap = GetCpuRenderableFrame()
        If frame Is Nothing Then Return
        DrawBitmapFrame(g, frame, target)
    End Sub

    Private Shared Sub DrawBitmapFrame(g As Graphics, frame As Bitmap, target As Rectangle)
        Dim oldInterp = g.InterpolationMode
        Dim oldOffset = g.PixelOffsetMode
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear
        g.PixelOffsetMode = PixelOffsetMode.Half
        g.DrawImage(frame, target)
        g.InterpolationMode = oldInterp
        g.PixelOffsetMode = oldOffset
    End Sub

    Private Function GetCpuRenderableFrame() As Bitmap
        Dim passes As Integer = Math.Max(0, Volatile.Read(_passes))
        If passes <= 0 Then Return Nothing

        Dim curVer As Integer = Volatile.Read(_frameVersion)
        Dim settingsVer As Integer = Volatile.Read(_effectSettingsVersion)
        If _cpuBlurredFrame IsNot Nothing AndAlso
           _cpuBlurredSourceVersion = curVer AndAlso
           _cpuBlurredSettingsVersion = settingsVer Then
            Return _cpuBlurredFrame
        End If

        Dim working As Bitmap = Nothing
        SyncLock _frameLock
            If _currentFrame Is Nothing Then Return Nothing
            working = New Bitmap(_currentFrame.Width, _currentFrame.Height, PixelFormat.Format32bppPArgb)
            Using gb As Graphics = Graphics.FromImage(working)
                gb.CompositingMode = CompositingMode.SourceCopy
                gb.DrawImageUnscaled(_currentFrame, 0, 0)
            End Using
        End SyncLock

        Dim down As Integer = Math.Max(1, Volatile.Read(_downsample))
        Dim radius As Integer = Math.Max(1, Volatile.Read(_radius) \ down)
        BoxBlur(working, radius, passes, Environment.ProcessorCount)

        If _cpuBlurredFrame IsNot Nothing Then
            Try : _cpuBlurredFrame.Dispose() : Catch : End Try
        End If
        _cpuBlurredFrame = working
        _cpuBlurredSourceVersion = curVer
        _cpuBlurredSettingsVersion = settingsVer
        Return _cpuBlurredFrame
    End Function

    ''' <summary>
    ''' D2D 重载：把当前缓存帧上传成 <see cref="ID2D1Bitmap"/> 并绘制到目标矩形。
    ''' 比 GDI 路径少一次完整的 BLT/采样：D2D 直接走 GPU 双线性 + Premultiplied Alpha 合成。
    ''' </summary>
    ''' <remarks>
    ''' 缓存策略：按 (RT, frameVersion) 失效。
    ''' • 帧未变 + RT 未变 → 复用已上传的 <see cref="ID2D1Bitmap"/>。
    ''' • 帧变化 → 重新上传（同 RT）。
    ''' • RT 变化（DC RT 被外部替换 / compositor 重建）→ 整个上传重做。
    ''' 上传期间持 <c>_frameLock</c>，确保 worker 不会同时交换 _currentFrame。
    ''' </remarks>
    Public Sub DrawTo(rt As ID2D1RenderTarget, target As Rectangle)
        If rt Is Nothing OrElse target.Width <= 0 OrElse target.Height <= 0 Then Return
        Dim d2dBmp As ID2D1Bitmap
        Dim frame As Bitmap = If(Math.Max(0, Volatile.Read(_passes)) > 0, GetCpuRenderableFrame(), Nothing)
        SyncLock _frameLock
            If frame Is Nothing Then frame = _currentFrame
            If frame Is Nothing Then Return
            Dim curVer As Integer = If(frame Is _currentFrame,
                                       Volatile.Read(_frameVersion),
                                       _cpuBlurredSourceVersion Xor _cpuBlurredSettingsVersion)
            Dim ownerAlive As Boolean = _frameD2DOwnerRT IsNot Nothing AndAlso ReferenceEquals(_frameD2DOwnerRT.Target, rt)
            If _frameD2DBitmap Is Nothing OrElse Not ownerAlive OrElse _frameD2DUploadedVersion <> curVer Then
                If _frameD2DBitmap IsNot Nothing Then
                    Try : _frameD2DBitmap.Dispose() : Catch : End Try
                    _frameD2DBitmap = Nothing
                End If
                _frameD2DBitmap = D2DHelper.CreateBitmapFromGdi(rt, frame)
                _frameD2DOwnerRT = New WeakReference(rt)
                _frameD2DUploadedVersion = curVer
            End If
            d2dBmp = _frameD2DBitmap
        End SyncLock
        If d2dBmp Is Nothing Then Return
        rt.DrawBitmap(d2dBmp, D2DHelper.ToD2DRect(target), 1.0F, BitmapInterpolationMode.Linear, Nothing)
    End Sub

    ''' <summary>
    ''' 在目标矩形上叠加噪点纹理（模拟 WinUI 亚克力颗粒）。
    ''' 使用缓存的 <see cref="TextureBrush"/> 一次性平铺整个目标矩形，
    ''' 避免按 tile 循环每次都构造 ImageAttributes / DrawImage。
    ''' </summary>
    Public Sub DrawNoise(g As Graphics, target As Rectangle, opacity As Byte)
        If opacity = 0 OrElse target.Width <= 0 OrElse target.Height <= 0 Then Return
        EnsureNoise()
        If _noiseBitmap Is Nothing Then Return

        Dim tile As Integer = Math.Max(1, CInt(_noiseBitmap.Width * _noiseScale))
        Dim brush As TextureBrush = AcquireNoiseBrush(opacity, tile)
        If brush Is Nothing Then Return

        ' TextureBrush 默认从 (0,0) 开始平铺；偏移使首个 tile 对齐 target.X/Y。
        ' FillRectangle 自身会把绘制范围限制在 target 内，无需额外 SetClip。
        brush.TranslateTransform(target.X, target.Y)
        Try
            g.FillRectangle(brush, target)
        Finally
            brush.ResetTransform()
        End Try
    End Sub

    ''' <summary>
    ''' D2D 重载：用 <see cref="ID2D1BitmapBrush"/> 平铺噪点。
    ''' 相比 GDI <see cref="TextureBrush"/>，省去 CPU 端的 ColorMatrix 合成与 Tile 采样，
    ''' opacity 通过 brush.Opacity 直接给 GPU，scale 通过 brush.Transform 给 GPU。
    ''' </summary>
    ''' <remarks>
    ''' 缓存策略：
    ''' • <see cref="_noiseD2DBitmap"/> 按 RT 缓存，源 <see cref="_noiseBitmap"/> 终身不变 → 长期复用。
    ''' • <see cref="_noiseD2DBrush"/> 同 RT 内复用，每帧仅按需更新 Opacity 与 Transform(scale)。
    ''' </remarks>
    Public Sub DrawNoise(rt As ID2D1RenderTarget, target As Rectangle, opacity As Byte)
        If rt Is Nothing OrElse opacity = 0 OrElse target.Width <= 0 OrElse target.Height <= 0 Then Return
        EnsureNoise()
        If _noiseBitmap Is Nothing Then Return

        Dim tile As Integer = Math.Max(1, CInt(_noiseBitmap.Width * _noiseScale))
        Dim brush As ID2D1BitmapBrush = AcquireNoiseD2DBrush(rt, tile)
        If brush Is Nothing Then Return

        ' Opacity 走 brush.Opacity，每帧便宜地改一下；不需要为不同 opacity 维持多份 brush。
        brush.Opacity = opacity / 255.0F
        ' brush 默认在世界坐标 (0,0) 起铺；用 Translate 把 tile 锚定到 target.X/Y。
        Dim t = Matrix3x2.CreateScale(tile / CSng(_noiseBitmap.Width)) *
                Matrix3x2.CreateTranslation(target.X, target.Y)
        brush.Transform = t
        rt.FillRectangle(D2DHelper.ToD2DRect(target), brush)
    End Sub

    ''' <summary>取得（或重建）匹配 (opacity, tile) 的噪点平铺 TextureBrush。</summary>
    Private Function AcquireNoiseBrush(opacity As Byte, tile As Integer) As TextureBrush
        If _noiseBrush IsNot Nothing AndAlso _noiseBrushOpacity = opacity AndAlso _noiseBrushTile = tile Then
            Return _noiseBrush
        End If
        _noiseBrush?.Dispose()
        _noiseBrush = Nothing
        Try
            Using attr As New ImageAttributes()
                Dim m As New ColorMatrix() With {.Matrix33 = opacity / 255.0F}
                attr.SetColorMatrix(m)
                _noiseBrush = New TextureBrush(_noiseBitmap,
                    New Rectangle(0, 0, _noiseBitmap.Width, _noiseBitmap.Height), attr)
                ' 把源 NxN 缩放到 tile×tile（保持 LakeUI 之前的视觉缩放语义）
                If tile <> _noiseBitmap.Width Then
                    Dim s As Single = tile / CSng(_noiseBitmap.Width)
                    _noiseBrush.ScaleTransform(s, s)
                End If
                _noiseBrush.WrapMode = WrapMode.Tile
            End Using
        Catch
            _noiseBrush?.Dispose()
            _noiseBrush = Nothing
            Return Nothing
        End Try
        _noiseBrushOpacity = opacity
        _noiseBrushTile = tile
        Return _noiseBrush
    End Function

    ''' <summary>
    ''' 取得（或重建）当前 RT 下用于平铺噪点的 <see cref="ID2D1BitmapBrush"/>。
    ''' RT 变了或位图缓存还没建好，会重新上传 + 重新创建 brush。
    ''' </summary>
    Private Function AcquireNoiseD2DBrush(rt As ID2D1RenderTarget, tile As Integer) As ID2D1BitmapBrush
        Dim ownerAlive As Boolean = _noiseD2DOwnerRT IsNot Nothing AndAlso ReferenceEquals(_noiseD2DOwnerRT.Target, rt)
        If Not ownerAlive Then
            ' RT 变了：旧 brush / bitmap 都来自旧 RT，必须释放。
            If _noiseD2DBrush IsNot Nothing Then
                Try : _noiseD2DBrush.Dispose() : Catch : End Try
                _noiseD2DBrush = Nothing
            End If
            If _noiseD2DBitmap IsNot Nothing Then
                Try : _noiseD2DBitmap.Dispose() : Catch : End Try
                _noiseD2DBitmap = Nothing
            End If
            _noiseD2DOwnerRT = Nothing
        End If

        If _noiseD2DBitmap Is Nothing Then
            _noiseD2DBitmap = D2DHelper.CreateBitmapFromGdi(rt, _noiseBitmap)
            If _noiseD2DBitmap Is Nothing Then Return Nothing
            _noiseD2DOwnerRT = New WeakReference(rt)
        End If

        If _noiseD2DBrush Is Nothing Then
            Try
                Dim bbp As New BitmapBrushProperties() With {
                    .ExtendModeX = ExtendMode.Wrap,
                    .ExtendModeY = ExtendMode.Wrap,
                    .InterpolationMode = BitmapInterpolationMode.Linear
                }
                _noiseD2DBrush = rt.CreateBitmapBrush(_noiseD2DBitmap, bbp)
                _noiseD2DBrushTile = tile
            Catch
                Return Nothing
            End Try
        End If
        Return _noiseD2DBrush
    End Function

    Private Sub EnsureNoise()
        If _noiseBitmap IsNot Nothing Then Return
        Const N As Integer = 128
        Dim bmp As New Bitmap(N, N, PixelFormat.Format32bppArgb)
        Dim rng As New Random(1059)
        Dim data = bmp.LockBits(New Rectangle(0, 0, N, N), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
        Try
            Dim stride As Integer = data.Stride
            Dim buf(stride * N - 1) As Byte
            For y As Integer = 0 To N - 1
                Dim rowOff As Integer = y * stride
                For x As Integer = 0 To N - 1
                    Dim v As Byte = CByte(rng.Next(0, 256))
                    Dim o As Integer = rowOff + x * 4
                    buf(o) = v : buf(o + 1) = v : buf(o + 2) = v : buf(o + 3) = 255
                Next
            Next
            Marshal.Copy(buf, 0, data.Scan0, buf.Length)
        Finally
            bmp.UnlockBits(data)
        End Try
        _noiseBitmap = bmp
    End Sub

    Public Function GetPublishedAverageColor() As Color?
        Dim v As Integer = Volatile.Read(_publishedAverage)
        If v = -1 Then Return Nothing
        Return Color.FromArgb((v >> 24) And &HFF, (v >> 16) And &HFF, (v >> 8) And &HFF, v And &HFF)
    End Function

    Public Function DeriveBorderColor(active As Boolean, fallback As Color) As Color
        Dim avg As Color? = GetPublishedAverageColor()
        If Not avg.HasValue Then Return fallback
        Dim c As Color = avg.Value
        Dim luma As Double = RelativeLuma(c)
        If luma < 72 Then
            Return Blend(c, Color.White, If(active, 0.48, 0.34))
        End If
        If luma > 205 Then
            Return Darken(c, If(active, 0.45, 0.58))
        End If
        Return Darken(c, If(active, 0.28, 0.42))
    End Function

    Public Function DeriveShadowColor(fallback As Color) As Color
        Dim avg As Color? = GetPublishedAverageColor()
        If Not avg.HasValue Then Return fallback
        Dim c As Color = avg.Value
        Dim luma As Double = RelativeLuma(c)
        Dim tint As Double = If(luma < 96, 0.5, If(luma > 190, 0.18, 0.3))
        Return Blend(Color.Black, c, tint)
    End Function

    Private Shared Function RelativeLuma(c As Color) As Double
        Return c.R * 0.299 + c.G * 0.587 + c.B * 0.114
    End Function

    Private Shared Function Blend(a As Color, b As Color, amountB As Double) As Color
        Dim t As Double = Math.Max(0.0, Math.Min(1.0, amountB))
        Dim r As Integer = CInt(a.R + (b.R - a.R) * t)
        Dim g As Integer = CInt(a.G + (b.G - a.G) * t)
        Dim blue As Integer = CInt(a.B + (b.B - a.B) * t)
        Return Color.FromArgb(255, r, g, blue)
    End Function

    Private Shared Function Darken(c As Color, k As Double) As Color
        Dim m As Double = 1.0 - k
        Dim r As Integer = CInt(c.R * m) : If r < 0 Then r = 0
        Dim g As Integer = CInt(c.G * m) : If g < 0 Then g = 0
        Dim b As Integer = CInt(c.B * m) : If b < 0 Then b = 0
        Return Color.FromArgb(255, r, g, b)
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If Interlocked.Exchange(_disposed, 1) <> 0 Then Return
        SyncLock _workerLock
        End SyncLock
        Try
            _workerIdle.Wait(500)
        Catch
        End Try
        SyncLock _frameLock
            _currentFrame?.Dispose()
            _currentFrame = Nothing
            _spareFrame?.Dispose()
            _spareFrame = Nothing
            ' D2D 缓存与 _currentFrame 共享 _frameLock，统一在这里释放。
            If _frameD2DBitmap IsNot Nothing Then
                Try : _frameD2DBitmap.Dispose() : Catch : End Try
                _frameD2DBitmap = Nothing
            End If
            _frameD2DOwnerRT = Nothing
            _frameD2DUploadedVersion = -1
            If _cpuBlurredFrame IsNot Nothing Then
                Try : _cpuBlurredFrame.Dispose() : Catch : End Try
                _cpuBlurredFrame = Nothing
            End If
        End SyncLock
        DisposeGpuResources()
        _capturedBitmap?.Dispose()
        _capturedBitmap = Nothing
        _noiseBitmap?.Dispose()
        _noiseBitmap = Nothing
        _noiseBrush?.Dispose()
        _noiseBrush = Nothing
        If _noiseD2DBrush IsNot Nothing Then
            Try : _noiseD2DBrush.Dispose() : Catch : End Try
            _noiseD2DBrush = Nothing
        End If
        If _noiseD2DBitmap IsNot Nothing Then
            Try : _noiseD2DBitmap.Dispose() : Catch : End Try
            _noiseD2DBitmap = Nothing
        End If
        _noiseD2DOwnerRT = Nothing
        _blurBufferA = Nothing
        _blurBufferB = Nothing
        _workerIdle.Dispose()
    End Sub

End Class
