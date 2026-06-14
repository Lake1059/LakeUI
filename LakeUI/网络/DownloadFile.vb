Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Text.Json
Imports System.Threading

''' <summary>
''' 通用文件下载器：基于 HttpClient，支持 http/https 以及任何走 HTTP 协议代理转发的远程协议（例如 frp）。
''' 支持单/多线程、断点续传（暂停/继续）、完整进度统计与 UI 线程友好的事件回调。
''' </summary>
Public Class DownloadFile
    Implements IDisposable

#Region "  共享 HttpClient  "

    ' 共享单例：避免端口/句柄泄漏；超时由 CancellationToken 控制
    Private Shared ReadOnly _sharedClient As New Lazy(Of HttpClient)(
        Function()
            Dim handler As New SocketsHttpHandler() With {
                .AllowAutoRedirect = True,
                .AutomaticDecompression = DecompressionMethods.All,
                .PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                .ConnectTimeout = TimeSpan.FromSeconds(30)
            }
            Dim c As New HttpClient(handler, disposeHandler:=True) With {
                .Timeout = Timeout.InfiniteTimeSpan
            }
            c.DefaultRequestHeaders.UserAgent.ParseAdd("LakeUI-Downloader/2.0")
            Return c
        End Function)

#End Region

#Region "  事件  "

    ''' <summary>下载完成时触发（在调用 StartAsync 时的同步上下文/UI 线程触发）。</summary>
    Public Event DownloadCompleted As EventHandler(Of DownloadCompletedEventArgs)

    ''' <summary>下载失败（异常或被取消）时触发。Pause 引发的暂停不会触发此事件。</summary>
    Public Event DownloadFailed As EventHandler(Of DownloadFailedEventArgs)

    ''' <summary>进度更新（按 ProgressInterval 节流，默认 250ms）。</summary>
    Public Event ProgressChanged As EventHandler(Of DownloadProgressEventArgs)

#End Region

#Region "  公共属性  "

    ''' <summary>下载 URL。</summary>
    Public Property Url As String

    ''' <summary>保存到本地的完整路径。</summary>
    Public Property SavePath As String

    ''' <summary>线程数，默认 1（单线程）。仅当服务器支持 Range 时才会真正并行。</summary>
    Public Property ThreadCount As Integer = 1

    ''' <summary>进度事件节流间隔，默认 500ms。</summary>
    Public Property ProgressInterval As TimeSpan = TimeSpan.FromMilliseconds(500)

    ''' <summary>单次读取/写入缓冲区大小（字节），默认 1MB（1048576）。越大越节省 CPU，越小越节省内存。</summary>
    Public Property BufferSize As Integer = 1024 * 1024

    ''' <summary>附加到每个下载请求的 HTTP 头，例如 Authorization。</summary>
    Public ReadOnly Property RequestHeaders As IDictionary(Of String, String) = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>已下载字节数。</summary>
    Public ReadOnly Property BytesDownloaded As Long
        Get
            Return Interlocked.Read(_bytesDownloaded)
        End Get
    End Property

    ''' <summary>文件总大小；未知时为 -1。</summary>
    Public ReadOnly Property TotalBytes As Long
        Get
            Return _totalBytes
        End Get
    End Property

    ''' <summary>瞬时速度（字节/秒）。</summary>
    Public ReadOnly Property SpeedBytesPerSecond As Double
        Get
            Return _speed
        End Get
    End Property

    ''' <summary>已消耗时间。</summary>
    Public ReadOnly Property Elapsed As TimeSpan
        Get
            Return _accumulatedElapsed + If(_stopwatch IsNot Nothing AndAlso _stopwatch.IsRunning, _stopwatch.Elapsed, TimeSpan.Zero)
        End Get
    End Property

    ''' <summary>预计剩余时间；未知时为 TimeSpan.MaxValue。</summary>
    Public ReadOnly Property EstimatedTimeRemaining As TimeSpan
        Get
            If _totalBytes <= 0 OrElse _speed <= 0 Then Return TimeSpan.MaxValue
            Dim remain = _totalBytes - BytesDownloaded
            If remain <= 0 Then Return TimeSpan.Zero
            Try
                Return TimeSpan.FromSeconds(remain / _speed)
            Catch
                Return TimeSpan.MaxValue
            End Try
        End Get
    End Property

    ''' <summary>当前状态。</summary>
    Public ReadOnly Property State As DownloadState
        Get
            Return _state
        End Get
    End Property

#End Region

#Region "  私有字段  "

    Private _bytesDownloaded As Long
    Private _totalBytes As Long = -1
    Private _speed As Double
    Private _state As DownloadState = DownloadState.Idle
    Private _stopwatch As Stopwatch
    Private _accumulatedElapsed As TimeSpan = TimeSpan.Zero

    Private _cts As CancellationTokenSource
    Private _runTask As Task
    Private _syncContext As SynchronizationContext

    Private _lastTickBytes As Long
    Private _lastTickTime As DateTime
    Private _lastProgressReport As DateTime
    Private _lastStateFlush As DateTime
    Private _bytesAtLastStateFlush As Long
    Private _progressLock As New Object()

    ''' <summary>持久化 .lkdl 状态文件的最大间隔，默认 10 秒。设为 TimeSpan.Zero 则仅依赖 StateFlushBytes 阈值。</summary>
    Public Property StateFlushInterval As TimeSpan = TimeSpan.FromSeconds(10)

    ''' <summary>持久化 .lkdl 状态文件的字节阈值，默认 1MB。每下载足该量即写一次状态。设为 0 则仅依赖 StateFlushInterval。</summary>
    Public Property StateFlushBytes As Long = 1024L * 1024L

    Private _segments As List(Of SegmentState)
    Private _supportsRange As Boolean

    ' 暂停标志：True 表示用户主动暂停，不触发 Failed 事件
    Private _pauseRequested As Boolean

    Private _disposed As Boolean

#End Region

#Region "  公共方法  "

    ''' <summary>
    ''' 开始或恢复下载。返回的 Task 在下载结束（成功/失败/暂停/取消）时完成。
    ''' 异常不会从此 Task 抛出（会通过 DownloadFailed 事件回调）。
    ''' </summary>
    Public Function StartAsync(Optional cancellationToken As CancellationToken = Nothing) As Task
        If _disposed Then Throw New ObjectDisposedException(NameOf(DownloadFile))
        If String.IsNullOrWhiteSpace(Url) Then Throw New InvalidOperationException("Url 不能为空。")
        If String.IsNullOrWhiteSpace(SavePath) Then Throw New InvalidOperationException("SavePath 不能为空。")
        If _state = DownloadState.Running Then Return _runTask

        ' 捕获调用线程上下文（UI 线程），用于事件回调
        _syncContext = If(SynchronizationContext.Current, New SynchronizationContext())

        _pauseRequested = False
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
        _state = DownloadState.Running

        _runTask = Task.Run(Function() RunAsync(_cts.Token))
        Return _runTask
    End Function

    ''' <summary>暂停下载（保留 .part 和 .lkdl 状态文件，可后续 StartAsync 恢复）。</summary>
    Public Function PauseAsync() As Task
        If _state <> DownloadState.Running Then Return Task.CompletedTask
        _pauseRequested = True
        _cts?.Cancel()
        Return If(_runTask, Task.CompletedTask)
    End Function

    ''' <summary>取消下载并删除临时文件与状态。</summary>
    Public Async Function CancelAsync() As Task
        _pauseRequested = False
        _cts?.Cancel()
        If _runTask IsNot Nothing Then
            Try
                Await _runTask.ConfigureAwait(False)
            Catch
            End Try
        End If
        TryDeleteTempFiles()
        _state = DownloadState.Cancelled
    End Function

#End Region

#Region "  核心实现  "

    Private Async Function RunAsync(token As CancellationToken) As Task
        _stopwatch = Stopwatch.StartNew()
        _lastTickBytes = BytesDownloaded
        _lastTickTime = DateTime.UtcNow
        _lastProgressReport = DateTime.MinValue

        Try
            ' 1. 探测远程文件大小 / Range 支持
            Await ProbeRemoteAsync(token).ConfigureAwait(False)

            ' 2. 准备本地文件 & 分段
            PrepareSegments()

            ' 3. 并行下载所有未完成段
            Dim partPath = SavePath & ".part"
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(partPath)))

            If _segments.Count = 1 AndAlso Not _supportsRange Then
                Await DownloadSingleStreamAsync(partPath, token).ConfigureAwait(False)
            Else
                ' 预分配（首次）
                Using fs As New FileStream(partPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite)
                    If _totalBytes > 0 AndAlso fs.Length < _totalBytes Then fs.SetLength(_totalBytes)
                End Using

                Dim pending = _segments.Where(Function(s) s.Downloaded < s.Length).
                                        Select(Function(s) DownloadSegmentAsync(partPath, s, token)).ToArray()
                If pending.Length > 0 Then
                    Await Task.WhenAll(pending).ConfigureAwait(False)
                End If
            End If

            token.ThrowIfCancellationRequested()

            ' 4. 完成：重命名 part -> 最终文件
            FinalizeFile(partPath)

            _stopwatch.Stop()
            _accumulatedElapsed += _stopwatch.Elapsed
            _state = DownloadState.Completed
            ReportProgress(force:=True)
            RaiseOnContext(Sub() RaiseEvent DownloadCompleted(Me, New DownloadCompletedEventArgs(SavePath, BytesDownloaded, Elapsed)))

        Catch ex As OperationCanceledException
            _stopwatch.Stop()
            _accumulatedElapsed += _stopwatch.Elapsed
            If _pauseRequested Then
                _state = DownloadState.Paused
                SaveStateFile()
                ReportProgress(force:=True)
            Else
                _state = DownloadState.Cancelled
                RaiseOnContext(Sub() RaiseEvent DownloadFailed(Me, New DownloadFailedEventArgs(ex, "下载已取消。")))
            End If
        Catch ex As Exception
            _stopwatch.Stop()
            _accumulatedElapsed += _stopwatch.Elapsed
            _state = DownloadState.Failed
            ' 保存状态以便用户后续重试
            Try : SaveStateFile() : Catch : End Try
            RaiseOnContext(Sub() RaiseEvent DownloadFailed(Me, New DownloadFailedEventArgs(ex, ex.Message)))
        End Try
    End Function

    Private Async Function ProbeRemoteAsync(token As CancellationToken) As Task
        ' 优先 HEAD；部分服务器（含 frp 转发）不支持 HEAD，回退到 GET Range:0-0
        Dim length As Long = -1
        Dim acceptRange As Boolean = False

        Try
            Using req As New HttpRequestMessage(HttpMethod.Head, Url)
                ApplyRequestHeaders(req)
                Using resp = Await _sharedClient.Value.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(False)
                    If resp.IsSuccessStatusCode Then
                        length = If(resp.Content.Headers.ContentLength, -1L)
                        acceptRange = resp.Headers.AcceptRanges.Any(Function(s) s.Equals("bytes", StringComparison.OrdinalIgnoreCase))
                    End If
                End Using
            End Using
        Catch ex As Exception When TypeOf ex IsNot OperationCanceledException
            ' 忽略，走 GET 探测
        End Try

        If length < 0 Then
            Using req As New HttpRequestMessage(HttpMethod.Get, Url)
                ApplyRequestHeaders(req)
                req.Headers.Range = New RangeHeaderValue(0, 0)
                Using resp = Await _sharedClient.Value.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(False)
                    If resp.StatusCode = HttpStatusCode.PartialContent Then
                        acceptRange = True
                        If resp.Content.Headers.ContentRange IsNot Nothing AndAlso resp.Content.Headers.ContentRange.Length.HasValue Then
                            length = resp.Content.Headers.ContentRange.Length.Value
                        End If
                    ElseIf resp.IsSuccessStatusCode Then
                        length = If(resp.Content.Headers.ContentLength, -1L)
                    Else
                        resp.EnsureSuccessStatusCode()
                    End If
                End Using
            End Using
        End If

        _totalBytes = length
        _supportsRange = acceptRange AndAlso length > 0
    End Function

    Private Sub PrepareSegments()
        ' 尝试加载已存在的 .lkdl
        Dim state = TryLoadStateFile()
        If state IsNot Nothing AndAlso state.TotalBytes = _totalBytes AndAlso state.Url = Url AndAlso state.Segments IsNot Nothing AndAlso state.Segments.Count > 0 Then
            _segments = state.Segments
            Interlocked.Exchange(_bytesDownloaded, _segments.Sum(Function(s) s.Downloaded))
            Return
        End If

        ' 重新切分
        _segments = New List(Of SegmentState)()
        Interlocked.Exchange(_bytesDownloaded, 0)

        If Not _supportsRange OrElse _totalBytes <= 0 OrElse ThreadCount <= 1 Then
            _segments.Add(New SegmentState With {.Index = 0, .Start = 0, .Length = If(_totalBytes > 0, _totalBytes, -1), .Downloaded = 0})
            Return
        End If

        Dim n = Math.Max(1, ThreadCount)
        Dim segLen = _totalBytes \ n
        Dim cur As Long = 0
        For i = 0 To n - 1
            Dim len = If(i = n - 1, _totalBytes - cur, segLen)
            _segments.Add(New SegmentState With {.Index = i, .Start = cur, .Length = len, .Downloaded = 0})
            cur += len
        Next
    End Sub

    Private Async Function DownloadSingleStreamAsync(partPath As String, token As CancellationToken) As Task
        ' 服务器不支持 Range 或未知大小：从头开始（无法续传），用临时 part 文件
        If File.Exists(partPath) Then File.Delete(partPath)
        Interlocked.Exchange(_bytesDownloaded, 0)

        Using req As New HttpRequestMessage(HttpMethod.Get, Url)
            ApplyRequestHeaders(req)
            Using resp = Await _sharedClient.Value.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(False)
                resp.EnsureSuccessStatusCode()
                If _totalBytes < 0 Then _totalBytes = If(resp.Content.Headers.ContentLength, -1L)

                Using inStream = Await resp.Content.ReadAsStreamAsync(token).ConfigureAwait(False)
                    Using outStream As New FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.Read, BufferSize, useAsync:=True)
                        Dim buf(BufferSize - 1) As Byte
                        Dim read As Integer
                        Do
                            read = Await inStream.ReadAsync(buf.AsMemory(0, buf.Length), token).ConfigureAwait(False)
                            If read <= 0 Then Exit Do
                            Await outStream.WriteAsync(buf.AsMemory(0, read), token).ConfigureAwait(False)
                            Interlocked.Add(_bytesDownloaded, read)
                            _segments(0).Downloaded = BytesDownloaded
                            ReportProgress()
                        Loop
                    End Using
                End Using
            End Using
        End Using
    End Function

    Private Async Function DownloadSegmentAsync(partPath As String, seg As SegmentState, token As CancellationToken) As Task
        Dim segStart = seg.Start + seg.Downloaded
        Dim segEnd = seg.Start + seg.Length - 1
        If segStart > segEnd Then Return

        Using req As New HttpRequestMessage(HttpMethod.Get, Url)
            ApplyRequestHeaders(req)
            req.Headers.Range = New RangeHeaderValue(segStart, segEnd)
            Using resp = Await _sharedClient.Value.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(False)
                If resp.StatusCode <> HttpStatusCode.PartialContent AndAlso resp.StatusCode <> HttpStatusCode.OK Then
                    resp.EnsureSuccessStatusCode()
                End If

                Using inStream = Await resp.Content.ReadAsStreamAsync(token).ConfigureAwait(False)
                    Using outStream As New FileStream(partPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite, BufferSize, useAsync:=True)
                        outStream.Seek(segStart, SeekOrigin.Begin)
                        Dim buf(BufferSize - 1) As Byte
                        Dim read As Integer
                        Do
                            read = Await inStream.ReadAsync(buf.AsMemory(0, buf.Length), token).ConfigureAwait(False)
                            If read <= 0 Then Exit Do
                            Await outStream.WriteAsync(buf.AsMemory(0, read), token).ConfigureAwait(False)
                            Interlocked.Add(seg.Downloaded, read)
                            Interlocked.Add(_bytesDownloaded, read)
                            ReportProgress()
                        Loop
                        Await outStream.FlushAsync(token).ConfigureAwait(False)
                    End Using
                End Using
            End Using
        End Using
    End Function

    Private Sub FinalizeFile(partPath As String)
        If File.Exists(SavePath) Then File.Delete(SavePath)
        File.Move(partPath, SavePath)
        TryDeleteStateFile()
    End Sub

    Private Sub ApplyRequestHeaders(req As HttpRequestMessage)
        If RequestHeaders.Count = 0 Then Return

        For Each header As KeyValuePair(Of String, String) In RequestHeaders
            If String.IsNullOrWhiteSpace(header.Key) OrElse header.Value Is Nothing Then Continue For

            req.Headers.Remove(header.Key)
            req.Headers.TryAddWithoutValidation(header.Key, header.Value)
        Next
    End Sub

#End Region

#Region "  进度 / 速度  "

    Private Sub ReportProgress(Optional force As Boolean = False)
        SyncLock _progressLock
            Dim now = DateTime.UtcNow
            If Not force AndAlso (now - _lastProgressReport) < ProgressInterval Then Return
            _lastProgressReport = now

            Dim cur = BytesDownloaded
            Dim deltaBytes = cur - _lastTickBytes
            Dim deltaSecs = (now - _lastTickTime).TotalSeconds
            If deltaSecs > 0 Then
                Dim instant = deltaBytes / deltaSecs
                ' 指数平滑（EMA）
                If _speed <= 0 Then
                    _speed = instant
                Else
                    _speed = _speed * 0.7 + instant * 0.3
                End If
            End If
            _lastTickBytes = cur
            _lastTickTime = now

            Dim args = New DownloadProgressEventArgs(cur, _totalBytes, _speed, Elapsed, EstimatedTimeRemaining)
            RaiseOnContext(Sub() RaiseEvent ProgressChanged(Me, args))

            ' 周期性持久化分段进度：达到字节阈值或时间阈值任一即写盘，进程被强杀时也能续传
            If _supportsRange AndAlso _segments IsNot Nothing AndAlso _segments.Count > 0 Then
                Dim byTime = StateFlushInterval > TimeSpan.Zero AndAlso (now - _lastStateFlush) >= StateFlushInterval
                Dim byBytes = StateFlushBytes > 0 AndAlso (cur - _bytesAtLastStateFlush) >= StateFlushBytes
                If byTime OrElse byBytes Then
                    _lastStateFlush = now
                    _bytesAtLastStateFlush = cur
                    SaveStateFile()
                End If
            End If
        End SyncLock
    End Sub

    Private Sub RaiseOnContext(action As Action)
        Dim ctx = _syncContext
        If ctx Is Nothing Then
            Try : action() : Catch : End Try
        Else
            ctx.Post(Sub(o)
                         Try
                             action()
                         Catch
                         End Try
                     End Sub, Nothing)
        End If
    End Sub

#End Region

#Region "  状态持久化（.lkdl）  "

    Private Function StateFilePath() As String
        Return SavePath & ".lkdl"
    End Function

    Private Sub SaveStateFile()
        Try
            If _segments Is Nothing Then Return
            ' 拍快照避免被其他线程修改
            Dim segSnap = _segments.Select(Function(s) New SegmentState With {
                .Index = s.Index, .Start = s.Start, .Length = s.Length, .Downloaded = Interlocked.Read(s.Downloaded)
            }).ToList()
            Dim snapshot As New PersistState With {
                .Url = Url,
                .TotalBytes = _totalBytes,
                .Segments = segSnap
            }
            Dim json = JsonSerializer.Serialize(snapshot)
            Dim path = StateFilePath()
            ' 原子写：先写 tmp 再替换，避免进程被杀时留下半截 JSON
            Dim tmp = path & ".tmp"
            File.WriteAllText(tmp, json, Encoding.UTF8)
            If File.Exists(path) Then
                File.Replace(tmp, path, Nothing)
            Else
                File.Move(tmp, path)
            End If
        Catch
        End Try
    End Sub

    Private Function TryLoadStateFile() As PersistState
        Try
            Dim p = StateFilePath()
            If Not File.Exists(p) Then Return Nothing
            Dim json = File.ReadAllText(p, Encoding.UTF8)
            Return JsonSerializer.Deserialize(Of PersistState)(json)
        Catch
            Return Nothing
        End Try
    End Function

    Private Sub TryDeleteStateFile()
        Try
            Dim p = StateFilePath()
            If File.Exists(p) Then File.Delete(p)
        Catch
        End Try
    End Sub

    Private Sub TryDeleteTempFiles()
        Try
            Dim part = SavePath & ".part"
            If File.Exists(part) Then File.Delete(part)
        Catch
        End Try
        TryDeleteStateFile()
    End Sub

#End Region

#Region "  IDisposable  "

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        Try : _cts?.Cancel() : Catch : End Try
        Try : _cts?.Dispose() : Catch : End Try
        GC.SuppressFinalize(Me)
    End Sub

#End Region

End Class

#Region "  辅助类型  "

''' <summary>下载状态枚举。</summary>
Public Enum DownloadState
    Idle
    Running
    Paused
    Completed
    Failed
    Cancelled
End Enum

''' <summary>下载分段（用于多线程 + 续传）。</summary>
Public Class SegmentState
    Public Property Index As Integer
    Public Property [Start] As Long
    Public Property Length As Long
    Public Property Downloaded As Long
End Class

''' <summary>持久化到 .lkdl 的状态。</summary>
Public Class PersistState
    Public Property Url As String
    Public Property TotalBytes As Long
    Public Property Segments As List(Of SegmentState)
End Class

Public Class DownloadProgressEventArgs
    Inherits EventArgs

    Public Sub New(downloaded As Long, total As Long, speed As Double, elapsed As TimeSpan, remaining As TimeSpan)
        Me.BytesDownloaded = downloaded
        Me.TotalBytes = total
        Me.SpeedBytesPerSecond = speed
        Me.Elapsed = elapsed
        Me.EstimatedTimeRemaining = remaining
    End Sub

    Public ReadOnly Property BytesDownloaded As Long
    Public ReadOnly Property TotalBytes As Long
    Public ReadOnly Property SpeedBytesPerSecond As Double
    Public ReadOnly Property Elapsed As TimeSpan
    Public ReadOnly Property EstimatedTimeRemaining As TimeSpan

    ''' <summary>0-1 的进度比例；总大小未知时为 -1。</summary>
    Public ReadOnly Property ProgressRatio As Double
        Get
            If TotalBytes <= 0 Then Return -1
            Return BytesDownloaded / TotalBytes
        End Get
    End Property
End Class

Public Class DownloadCompletedEventArgs
    Inherits EventArgs

    Public Sub New(savePath As String, totalBytes As Long, elapsed As TimeSpan)
        Me.SavePath = savePath
        Me.TotalBytes = totalBytes
        Me.Elapsed = elapsed
    End Sub

    Public ReadOnly Property SavePath As String
    Public ReadOnly Property TotalBytes As Long
    Public ReadOnly Property Elapsed As TimeSpan
End Class

Public Class DownloadFailedEventArgs
    Inherits EventArgs

    Public Sub New(ex As Exception, message As String)
        Me.[Error] = ex
        Me.Message = message
    End Sub

    Public ReadOnly Property [Error] As Exception
    Public ReadOnly Property Message As String
End Class

#End Region
