Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Threading.Tasks

''' <summary>
''' 毛玻璃 / 亚克力效果的核心渲染器。
''' 在常驻后台线程上抓取桌面 DC（窗口背后区域）→ 下采样 → 多次水平 / 垂直 box blur →
''' 缓存为 <see cref="Bitmap"/>，供 <see cref="ThisIsYourWindow"/> 在 Paint 时铺满整个客户区。
''' 通过 <see cref="SetWindowDisplayAffinity"/>（Win10 19041+）确保自身不被拍到。
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
    Private ReadOnly _signal As New AutoResetEvent(False)
    Private ReadOnly _thread As Thread
    Private _disposed As Integer = 0

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
    Private _parallelism As Integer = Environment.ProcessorCount
    Private _noiseScale As Single = 1.0F

    ' ── 待处理请求（最新覆盖最旧，永不堆积）──
    Private _pendingX As Integer
    Private _pendingY As Integer
    Private _pendingW As Integer
    Private _pendingH As Integer
    Private _pendingCommitAverage As Integer
    Private _hasPending As Integer

    ' ── 当前帧（双缓冲：UI 读 _currentFrame，Worker 写 _spareFrame，结束后在 _frameLock 内交换）──
    Private ReadOnly _frameLock As New Object()
    Private _currentFrame As Bitmap
    Private _spareFrame As Bitmap

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
    ' ── 噪点平铺笔刷缓存（按 (opacity, tile) 失效）──
    Private _noiseBrush As TextureBrush
    Private _noiseBrushOpacity As Byte = 0
    Private _noiseBrushTile As Integer = 0

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
        _thread = New Thread(AddressOf WorkerLoop) With {
            .IsBackground = True,
            .Name = "LakeUI.BackdropRenderer"
        }
        _thread.Start()
    End Sub

    Public Sub ApplyParameters(radius As Integer, passes As Integer, downsample As Integer,
                                parallelism As Integer, noiseScale As Single)
        Volatile.Write(_radius, Math.Max(1, radius))
        Volatile.Write(_passes, Math.Max(1, Math.Min(5, passes)))
        Volatile.Write(_downsample, Math.Max(1, downsample))
        Volatile.Write(_parallelism, Math.Max(1, parallelism))
        _noiseScale = Math.Max(0.1F, noiseScale)
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

    Public ReadOnly Property HasFrame As Boolean
        Get
            SyncLock _frameLock
                Return _currentFrame IsNot Nothing
            End SyncLock
        End Get
    End Property

    ''' <summary>请求一帧抓屏 + 模糊。多次调用只保留最新一次。</summary>
    ''' <param name="commitAverage">是否将本帧平均色发布为"已稳定"——仅在首帧 / 拖动结束帧使用。</param>
    Public Sub RequestFrame(formBounds As Rectangle, commitAverage As Boolean)
        If Volatile.Read(_disposed) <> 0 Then Return
        _pendingX = formBounds.X
        _pendingY = formBounds.Y
        _pendingW = formBounds.Width
        _pendingH = formBounds.Height
        Volatile.Write(_pendingCommitAverage, If(commitAverage, 1, 0))
        Volatile.Write(_hasPending, 1)
        _signal.Set()
    End Sub

    Private Sub WorkerLoop()
        Do
            _signal.WaitOne()
            If Volatile.Read(_disposed) <> 0 Then Return
            If Interlocked.Exchange(_hasPending, 0) = 0 Then Continue Do
            Try
                Dim bounds As New Rectangle(_pendingX, _pendingY, _pendingW, _pendingH)
                Dim commitAvg As Boolean = (Interlocked.Exchange(_pendingCommitAverage, 0) <> 0)
                ProcessFrame(bounds, commitAvg)
            Catch
                ' 后台线程绝不能抛出
            End Try
        Loop
    End Sub

    Private Sub ProcessFrame(bounds As Rectangle, commitAvg As Boolean)
        Dim w As Integer = bounds.Width
        Dim h As Integer = bounds.Height
        If w < 4 OrElse h < 4 Then Return

        Dim down As Integer = Volatile.Read(_downsample)
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
                    gs.InterpolationMode = InterpolationMode.HighQualityBilinear
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
                    gs.InterpolationMode = InterpolationMode.HighQualityBilinear
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

        ' 3. 模糊
        Dim radius As Integer = Math.Max(1, Volatile.Read(_radius) \ Math.Max(1, down))
        Dim passes As Integer = Volatile.Read(_passes)
        Dim parallelism As Integer = Volatile.Read(_parallelism)
        BoxBlur(small, radius, passes, parallelism)

        ' 4. 平均色（直接对模糊后小图采样取均值，避免再过一次 GDI+ 缩放）
        Dim avg As Integer = ComputeAverage(small)
        Volatile.Write(_candidateAverage, avg)
        Dim publishedNow As Boolean = False
        If commitAvg OrElse Volatile.Read(_publishedAverage) = -1 Then
            Volatile.Write(_publishedAverage, avg)
            publishedNow = True
        End If

        ' 5. 交换前后帧：旧的 _currentFrame 退役为下次的 _spareFrame，避免分配
        SyncLock _frameLock
            Dim previousCurrent As Bitmap = _currentFrame
            _currentFrame = small
            _spareFrame = previousCurrent
        End SyncLock

        ' 6. 触发 UI 重绘
        Try
            If _host IsNot Nothing AndAlso _host.IsHandleCreated AndAlso Not _host.IsDisposed Then
                _host.BeginInvoke(CType(Sub()
                                            If Not _host.IsDisposed Then
                                                If publishedNow Then RaiseEvent AverageCommitted(Me, EventArgs.Empty)
                                                _host.Invalidate()
                                            End If
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
            _capturedBitmap = New Bitmap(w, h, PixelFormat.Format32bppArgb)
        Catch
            _capturedBitmap = Nothing
        End Try
        Return _capturedBitmap
    End Function

#Region "Box Blur"

    Private Sub BoxBlur(bmp As Bitmap, radius As Integer, passes As Integer, parallelism As Integer)
        If radius < 1 Then Return
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

            Dim pOpts As New ParallelOptions With {.MaxDegreeOfParallelism = Math.Max(1, parallelism)}

            For pass As Integer = 1 To passes
                ' 水平：src → dst
                Parallel.For(0, h, pOpts, Sub(y) BoxBlurRowH(src, dst, y, w, stride, radius))
                ' 垂直：dst → src
                Parallel.For(0, w, pOpts, Sub(x) BoxBlurColV(dst, src, x, h, stride, radius))
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
            If a > 0 AndAlso a < 255 Then
                r = Math.Min(255, CInt(r * 255 \ a))
                g = Math.Min(255, CInt(g * 255 \ a))
                b = Math.Min(255, CInt(b * 255 \ a))
            End If
            Return (a << 24) Or (r << 16) Or (g << 8) Or b
        Finally
            bmp.UnlockBits(data)
        End Try
    End Function

    ''' <summary>把当前缓存帧拉伸绘制到目标矩形（覆盖整个客户区，含标题栏）。</summary>
    Public Sub DrawTo(g As Graphics, target As Rectangle)
        SyncLock _frameLock
            If _currentFrame Is Nothing Then Return
            Dim oldInterp = g.InterpolationMode
            Dim oldOffset = g.PixelOffsetMode
            ' 源已模糊，普通 Bilinear 即可，HQ 在大窗口下显著更慢
            g.InterpolationMode = InterpolationMode.Bilinear
            g.PixelOffsetMode = PixelOffsetMode.Half
            g.DrawImage(_currentFrame, target)
            g.InterpolationMode = oldInterp
            g.PixelOffsetMode = oldOffset
        End SyncLock
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
        Return Darken(avg.Value, If(active, 0.55, 0.7))
    End Function

    Public Function DeriveShadowColor(fallback As Color) As Color
        Dim avg As Color? = GetPublishedAverageColor()
        If Not avg.HasValue Then Return fallback
        Return Darken(avg.Value, 0.75)
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
        _signal.Set()
        Try
            _thread.Join(500)
        Catch
        End Try
        SyncLock _frameLock
            _currentFrame?.Dispose()
            _currentFrame = Nothing
            _spareFrame?.Dispose()
            _spareFrame = Nothing
        End SyncLock
        _capturedBitmap?.Dispose()
        _capturedBitmap = Nothing
        _noiseBitmap?.Dispose()
        _noiseBitmap = Nothing
        _noiseBrush?.Dispose()
        _noiseBrush = Nothing
        _blurBufferA = Nothing
        _blurBufferB = Nothing
        _signal.Dispose()
    End Sub

End Class
