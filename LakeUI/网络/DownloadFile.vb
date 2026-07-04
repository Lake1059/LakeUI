Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports System.Threading

''' <summary>
''' 通用文件下载器：基于 HttpClient，支持 http/https 以及任何走 HTTP 协议代理转发的远程协议（例如 frp）。
''' 支持单/多线程、断点续传（暂停/继续）、完整进度统计与 UI 线程友好的事件回调。
''' </summary>
Public Class DownloadFile
    Implements IDisposable

    Private Const ResumeRewindBytes As Long = 1024L * 1024L

#Region "  共享 HttpClient  "

    ' 共享单例：避免端口/句柄泄漏；超时由 CancellationToken 控制
    Private Shared ReadOnly _sharedClient As New Lazy(Of HttpClient)(
        Function()
            Dim handler As New SocketsHttpHandler() With {
                .AllowAutoRedirect = True,
                .AutomaticDecompression = DecompressionMethods.None,
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

    ''' <summary>可选的 SHA256 校验值（64 位十六进制）。设置后下载完成会自动校验，失败会清理临时文件并重新下载一次。</summary>
    Public Property ExpectedSha256 As String
        Get
            Return _expectedSha256
        End Get
        Set(value As String)
            _expectedSha256 = NormalizeSha256(value)
        End Set
    End Property

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
    Private _expectedSha256 As String
    Private _remoteETag As String
    Private _remoteLastModified As DateTimeOffset?

    ' 暂停标志：True 表示用户主动暂停，不触发 Failed 事件
    Private _pauseRequested As Boolean

    Private _disposed As Boolean

#End Region

#Region "  公共方法  "

    ''' <summary>
    ''' 开始或恢复下载。返回的 Task 在下载结束（成功/失败/暂停/取消）时完成。
    ''' 异常不会从此 Task 抛出（会通过 DownloadFailed 事件回调）。
    ''' </summary>
    Public Function StartAsync(Optional cancellationToken As CancellationToken = Nothing,
                               Optional expectedSha256 As String = Nothing) As Task
        If _disposed Then Throw New ObjectDisposedException(NameOf(DownloadFile))
        If String.IsNullOrWhiteSpace(Url) Then Throw New InvalidOperationException("Url 不能为空。")
        If String.IsNullOrWhiteSpace(SavePath) Then Throw New InvalidOperationException("SavePath 不能为空。")
        If BufferSize <= 0 Then Throw New InvalidOperationException("BufferSize 必须大于 0。")
        If _state = DownloadState.Running Then Return _runTask
        If expectedSha256 IsNot Nothing Then Me.ExpectedSha256 = expectedSha256

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
            Dim partPath = SavePath & ".part"
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(partPath)))

            Dim sha256RetryUsed = False
            Do
                Await DownloadToPartAsync(partPath, token).ConfigureAwait(False)
                token.ThrowIfCancellationRequested()

                ValidateDownloadedFile(partPath)

                If Not String.IsNullOrWhiteSpace(_expectedSha256) Then
                    Dim actualSha256 = Await ComputeFileSha256Async(partPath, token).ConfigureAwait(False)
                    If Not actualSha256.Equals(_expectedSha256, StringComparison.OrdinalIgnoreCase) Then
                        TryDeleteDownloadState(partPath)
                        If Not sha256RetryUsed Then
                            sha256RetryUsed = True
                            ResetRuntimeProgress()
                            Continue Do
                        End If

                        _segments = Nothing
                        Interlocked.Exchange(_bytesDownloaded, 0)
                        Throw New InvalidDataException($"SHA256 校验失败。期望 {_expectedSha256}，实际 {actualSha256}。")
                    End If
                End If

                Exit Do
            Loop

            ' 通过完整性校验后再重命名 part -> 最终文件
            FinalizeFile(partPath)

            _stopwatch.Stop()
            _accumulatedElapsed += _stopwatch.Elapsed
            _state = DownloadState.Completed
            ReportProgress(force:=True)
            RaiseOnContext(Sub() RaiseEvent DownloadCompleted(Me, New DownloadCompletedEventArgs(SavePath, BytesDownloaded, Elapsed)))

        Catch ex As OperationCanceledException When token.IsCancellationRequested
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
        Catch ex As OperationCanceledException
            _stopwatch.Stop()
            _accumulatedElapsed += _stopwatch.Elapsed
            _state = DownloadState.Failed
            Try : SaveStateFile() : Catch : End Try
            RaiseOnContext(Sub() RaiseEvent DownloadFailed(Me, New DownloadFailedEventArgs(ex, "下载被异常中断。")))
        Catch ex As Exception
            _stopwatch.Stop()
            _accumulatedElapsed += _stopwatch.Elapsed
            _state = DownloadState.Failed
            ' 保存状态以便用户后续重试
            Try : SaveStateFile() : Catch : End Try
            RaiseOnContext(Sub() RaiseEvent DownloadFailed(Me, New DownloadFailedEventArgs(ex, ex.Message)))
        End Try
    End Function

    Private Async Function DownloadToPartAsync(partPath As String, token As CancellationToken) As Task
        ' 1. 探测远程文件大小 / Range 支持
        Await ProbeRemoteAsync(token).ConfigureAwait(False)

        ' 2. 准备本地文件 & 分段
        PrepareSegments(partPath)

        ' 3. 并行下载所有未完成段
        If _segments.Count = 1 AndAlso Not _supportsRange Then
            Await DownloadSingleStreamAsync(partPath, token).ConfigureAwait(False)
            Return
        End If

        ' 预分配（首次）。如果远端长度已知，也截断旧的过长 .part，防止最终文件带上历史尾部。
        Using fs As New FileStream(partPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite)
            If _totalBytes >= 0 AndAlso fs.Length <> _totalBytes Then fs.SetLength(_totalBytes)
        End Using

        ' 用一个联动 CTS：任意一段失败时立即通知其余段退出，
        ' 避免失败后其他线程仍继续写入导致状态混乱，续传时偏移出错。
        Using failCts As CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token)
            Dim failToken = failCts.Token
            Dim pending = _segments.Where(Function(s) s.Downloaded < s.Length).
                                    Select(Function(s) WrapSegment(partPath, s, failCts, failToken)).ToArray()
            If pending.Length <= 0 Then Return

            Try
                Await Task.WhenAll(pending).ConfigureAwait(False)
            Catch ex As OperationCanceledException When Not token.IsCancellationRequested
                ' 是由某段失败触发的内部取消（failCts），重新抛出第一个真正的业务异常
                Dim realEx = pending.Where(Function(t) t.IsFaulted).
                                     SelectMany(Function(t) t.Exception.InnerExceptions).
                                     FirstOrDefault()
                If realEx IsNot Nothing Then Throw New Exception(realEx.Message, realEx)
                Throw
            End Try
        End Using
    End Function

    Private Async Function ProbeRemoteAsync(token As CancellationToken) As Task
        ' 优先 HEAD；部分服务器（含 frp 转发）不支持 HEAD，回退到 GET Range:0-0
        Dim length As Long = -1
        Dim acceptRange As Boolean = False
        _remoteETag = Nothing
        _remoteLastModified = Nothing

        Try
            Using req As New HttpRequestMessage(HttpMethod.Head, Url)
                ApplyRequestHeaders(req)
                Using resp = Await _sharedClient.Value.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(False)
                    If resp.IsSuccessStatusCode Then
                        CaptureRemoteValidators(resp)
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
                    If resp.IsSuccessStatusCode OrElse resp.StatusCode = HttpStatusCode.PartialContent Then CaptureRemoteValidators(resp)
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

    Private Sub PrepareSegments(partPath As String)
        ' 尝试加载已存在的 .lkdl
        If _supportsRange Then
            Dim state = TryLoadStateFile()
            If IsStateUsableForResume(state, partPath) Then
                _segments = state.Segments
                Interlocked.Exchange(_bytesDownloaded, _segments.Sum(Function(s) s.Downloaded))
                _lastTickBytes = BytesDownloaded
                _bytesAtLastStateFlush = BytesDownloaded
                Return
            End If

            If state IsNot Nothing Then TryDeleteDownloadState(partPath)
        End If

        ' 重新切分
        _segments = New List(Of SegmentState)()
        Interlocked.Exchange(_bytesDownloaded, 0)

        If Not _supportsRange OrElse _totalBytes <= 0 OrElse ThreadCount <= 1 Then
            _segments.Add(New SegmentState With {.Index = 0, .Start = 0, .Length = If(_totalBytes >= 0, _totalBytes, -1), .Downloaded = 0})
            Return
        End If

        Dim n = Math.Max(1, ThreadCount)
        If _totalBytes > 0 AndAlso CLng(n) > _totalBytes Then n = CInt(_totalBytes)
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
                ValidateRemoteValidators(resp)
                If _totalBytes < 0 Then _totalBytes = If(resp.Content.Headers.ContentLength, -1L)
                Dim expectedLength = If(resp.Content.Headers.ContentLength, -1L)
                If _totalBytes >= 0 AndAlso expectedLength >= 0 AndAlso expectedLength <> _totalBytes Then
                    Throw New InvalidDataException($"远端文件长度在下载过程中发生变化。探测值 {_totalBytes}，实际响应 {expectedLength}。")
                End If

                Using inStream = Await resp.Content.ReadAsStreamAsync(token).ConfigureAwait(False)
                    Using outStream As New FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.Read, BufferSize, FileOptions.Asynchronous Or FileOptions.WriteThrough)
                        Dim buf(BufferSize - 1) As Byte
                        Dim read As Integer
                        Dim downloadedThisRequest As Long = 0
                        Do
                            read = Await inStream.ReadAsync(buf.AsMemory(0, buf.Length), token).ConfigureAwait(False)
                            If read <= 0 Then Exit Do
                            Await outStream.WriteAsync(buf.AsMemory(0, read), token).ConfigureAwait(False)
                            downloadedThisRequest += read
                            Interlocked.Add(_bytesDownloaded, read)
                            _segments(0).Downloaded = BytesDownloaded
                            ReportProgress()
                        Loop
                        Await outStream.FlushAsync(token).ConfigureAwait(False)
                        If expectedLength >= 0 AndAlso downloadedThisRequest <> expectedLength Then
                            Throw New EndOfStreamException($"下载提前结束。期望 {expectedLength} 字节，实际 {downloadedThisRequest} 字节。")
                        End If
                        If _totalBytes >= 0 AndAlso BytesDownloaded <> _totalBytes Then
                            Throw New EndOfStreamException($"下载字节数不完整。期望 {_totalBytes} 字节，实际 {BytesDownloaded} 字节。")
                        End If
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
            ApplyIfRangeValidator(req)
            Using resp = Await _sharedClient.Value.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(False)
                If resp.StatusCode <> HttpStatusCode.PartialContent Then
                    If resp.IsSuccessStatusCode Then
                        Throw New InvalidDataException("服务器未按 Range 请求返回 206 Partial Content，无法安全执行分段下载。")
                    End If

                    resp.EnsureSuccessStatusCode()
                End If

                ValidateRemoteValidators(resp)
                Dim expectedBytes = segEnd - segStart + 1
                ValidateContentRange(resp, segStart, segEnd, expectedBytes)

                Using inStream = Await resp.Content.ReadAsStreamAsync(token).ConfigureAwait(False)
                    Using outStream As New FileStream(partPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite, BufferSize, FileOptions.Asynchronous Or FileOptions.WriteThrough)
                        outStream.Seek(segStart, SeekOrigin.Begin)
                        Dim buf(BufferSize - 1) As Byte
                        Dim read As Integer
                        Dim written As Long = 0
                        While written < expectedBytes
                            Dim toRead = CInt(Math.Min(buf.Length, expectedBytes - written))
                            read = Await inStream.ReadAsync(buf.AsMemory(0, toRead), token).ConfigureAwait(False)
                            If read <= 0 Then
                                Throw New EndOfStreamException($"分段 {seg.Index} 下载提前结束。期望 {expectedBytes} 字节，实际 {written} 字节。")
                            End If
                            Await outStream.WriteAsync(buf.AsMemory(0, read), token).ConfigureAwait(False)
                            written += read
                            Interlocked.Add(seg.Downloaded, read)
                            Interlocked.Add(_bytesDownloaded, read)
                            ReportProgress()
                        End While
                        Await outStream.FlushAsync(token).ConfigureAwait(False)
                    End Using
                End Using
            End Using
        End Using
    End Function

    ''' <summary>
    ''' 包装 DownloadSegmentAsync：若该段因网络/服务器错误（非用户取消）失败，
    ''' 立即触发 failCts 取消其余所有段，防止状态不一致导致续传文件损坏。
    ''' </summary>
    Private Async Function WrapSegment(partPath As String, seg As SegmentState,
                                       failCts As CancellationTokenSource,
                                       token As CancellationToken) As Task
        Try
            Await DownloadSegmentAsync(partPath, seg, token).ConfigureAwait(False)
        Catch ex As OperationCanceledException When token.IsCancellationRequested
            Throw   ' 正常取消，直接传播
        Catch ex As OperationCanceledException
            Try : failCts.Cancel() : Catch : End Try
            Throw New IOException("分段下载被异常中断。", ex)
        Catch
            ' 业务异常（网络断开、服务器返回错误等）：取消其他段后重新抛出
            Try : failCts.Cancel() : Catch : End Try
            Throw
        End Try
    End Function

    Private Sub FinalizeFile(partPath As String)
        If File.Exists(SavePath) Then
            File.Replace(partPath, SavePath, Nothing)
        Else
            File.Move(partPath, SavePath)
        End If
        TryDeleteStateFile()
    End Sub

    Private Sub ApplyRequestHeaders(req As HttpRequestMessage)
        If RequestHeaders.Count > 0 Then
            For Each header As KeyValuePair(Of String, String) In RequestHeaders
                If String.IsNullOrWhiteSpace(header.Key) OrElse header.Value Is Nothing Then Continue For

                req.Headers.Remove(header.Key)
                req.Headers.TryAddWithoutValidation(header.Key, header.Value)
            Next
        End If

        req.Headers.Remove("Accept-Encoding")
        req.Headers.AcceptEncoding.Clear()
        req.Headers.AcceptEncoding.Add(New StringWithQualityHeaderValue("identity"))
        req.Headers.Remove("Range")
    End Sub

    Private Sub CaptureRemoteValidators(resp As HttpResponseMessage)
        If resp.Headers.ETag IsNot Nothing Then _remoteETag = resp.Headers.ETag.ToString()
        If resp.Content IsNot Nothing AndAlso resp.Content.Headers.LastModified.HasValue Then
            _remoteLastModified = resp.Content.Headers.LastModified.Value
        End If
    End Sub

    Private Function IsStateUsableForResume(state As PersistState, partPath As String) As Boolean
        If state Is Nothing OrElse state.Segments Is Nothing OrElse state.Segments.Count = 0 Then Return False
        If Not File.Exists(partPath) Then Return False
        If Not String.Equals(state.Url, Url, StringComparison.Ordinal) Then Return False
        If state.TotalBytes <> _totalBytes Then Return False
        If Not RemoteValidatorsMatch(state) Then Return False

        Dim segments = state.Segments.OrderBy(Function(s) s.Start).ThenBy(Function(s) s.Index).ToList()
        Dim partLength = New FileInfo(partPath).Length
        Dim expectedStart As Long = 0

        For Each seg In segments
            If seg.Length < 0 OrElse seg.Downloaded < 0 Then Return False
            If seg.Start <> expectedStart Then Return False
            If seg.Downloaded > seg.Length Then Return False

            Dim persistedLength = Math.Max(0, Math.Min(seg.Length, partLength - seg.Start))
            If seg.Downloaded > persistedLength Then seg.Downloaded = persistedLength
            If seg.Downloaded > 0 Then seg.Downloaded = Math.Max(0, seg.Downloaded - Math.Min(ResumeRewindBytes, seg.Downloaded))

            expectedStart += seg.Length
        Next

        If expectedStart <> _totalBytes Then Return False
        state.Segments = segments
        Return True
    End Function

    Private Function RemoteValidatorsMatch(state As PersistState) As Boolean
        If Not String.Equals(state.ETag, _remoteETag, StringComparison.Ordinal) Then
            If Not String.IsNullOrEmpty(state.ETag) OrElse Not String.IsNullOrEmpty(_remoteETag) Then Return False
        End If

        If state.LastModified.HasValue <> _remoteLastModified.HasValue Then Return False
        If state.LastModified.HasValue AndAlso state.LastModified.Value <> _remoteLastModified.Value Then Return False
        Return True
    End Function

    Private Sub ApplyIfRangeValidator(req As HttpRequestMessage)
        If Not String.IsNullOrEmpty(_remoteETag) Then
            Dim etag As EntityTagHeaderValue = Nothing
            If EntityTagHeaderValue.TryParse(_remoteETag, etag) Then
                req.Headers.IfRange = New RangeConditionHeaderValue(etag)
                Return
            End If
        End If

        If _remoteLastModified.HasValue Then req.Headers.IfRange = New RangeConditionHeaderValue(_remoteLastModified.Value)
    End Sub

    Private Sub ValidateRemoteValidators(resp As HttpResponseMessage)
        If Not String.IsNullOrEmpty(_remoteETag) AndAlso resp.Headers.ETag IsNot Nothing Then
            If Not String.Equals(resp.Headers.ETag.ToString(), _remoteETag, StringComparison.Ordinal) Then
                Throw New InvalidDataException("远端文件 ETag 在下载过程中发生变化，已停止以避免拼接损坏文件。")
            End If
        End If

        If _remoteLastModified.HasValue AndAlso resp.Content IsNot Nothing AndAlso resp.Content.Headers.LastModified.HasValue Then
            If resp.Content.Headers.LastModified.Value <> _remoteLastModified.Value Then
                Throw New InvalidDataException("远端文件 Last-Modified 在下载过程中发生变化，已停止以避免拼接损坏文件。")
            End If
        End If
    End Sub

    Private Sub ValidateContentRange(resp As HttpResponseMessage,
                                     expectedStart As Long,
                                     expectedEnd As Long,
                                     expectedBytes As Long)
        Dim range = resp.Content.Headers.ContentRange
        If range Is Nothing OrElse Not range.From.HasValue OrElse Not range.To.HasValue Then
            Throw New InvalidDataException("服务器返回的 206 响应缺少有效 Content-Range，无法安全执行分段下载。")
        End If

        If range.From.Value <> expectedStart OrElse range.To.Value <> expectedEnd Then
            Throw New InvalidDataException($"服务器返回的 Content-Range 不匹配。期望 {expectedStart}-{expectedEnd}，实际 {range.From.Value}-{range.To.Value}。")
        End If

        If range.Length.HasValue AndAlso _totalBytes > 0 AndAlso range.Length.Value <> _totalBytes Then
            Throw New InvalidDataException($"远端文件长度在分段下载过程中发生变化。探测值 {_totalBytes}，实际 {range.Length.Value}。")
        End If

        If resp.Content.Headers.ContentLength.HasValue AndAlso resp.Content.Headers.ContentLength.Value <> expectedBytes Then
            Throw New InvalidDataException($"服务器返回的分段长度不匹配。期望 {expectedBytes} 字节，实际 {resp.Content.Headers.ContentLength.Value} 字节。")
        End If
    End Sub

    Private Sub ValidateDownloadedFile(partPath As String)
        If Not File.Exists(partPath) Then Throw New FileNotFoundException("下载临时文件不存在。", partPath)

        Dim actualLength = New FileInfo(partPath).Length
        If _totalBytes >= 0 AndAlso actualLength <> _totalBytes Then
            Throw New InvalidDataException($"下载文件长度不匹配。期望 {_totalBytes} 字节，实际 {actualLength} 字节。")
        End If

        If _supportsRange Then
            If _segments Is Nothing OrElse _segments.Count = 0 Then Throw New InvalidDataException("分段状态缺失，无法确认下载完整性。")

            Dim totalSegmentLength As Long = 0
            For Each seg In _segments
                If seg.Length < 0 Then Throw New InvalidDataException($"分段 {seg.Index} 长度无效。")
                Dim downloaded = Interlocked.Read(seg.Downloaded)
                If downloaded <> seg.Length Then
                    Throw New InvalidDataException($"分段 {seg.Index} 未完整下载。期望 {seg.Length} 字节，实际 {downloaded} 字节。")
                End If
                totalSegmentLength += seg.Length
            Next

            If _totalBytes >= 0 AndAlso totalSegmentLength <> _totalBytes Then
                Throw New InvalidDataException($"分段总长度不匹配。期望 {_totalBytes} 字节，实际 {totalSegmentLength} 字节。")
            End If
        End If

        If _totalBytes >= 0 AndAlso BytesDownloaded <> _totalBytes Then
            Throw New InvalidDataException($"下载计数不匹配。期望 {_totalBytes} 字节，实际 {BytesDownloaded} 字节。")
        End If
    End Sub

    Private Async Function ComputeFileSha256Async(path As String, token As CancellationToken) As Task(Of String)
        Using fs As New FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous Or FileOptions.SequentialScan)
            Using sha = SHA256.Create()
                Dim hash = Await sha.ComputeHashAsync(fs, token).ConfigureAwait(False)
                Return Convert.ToHexString(hash).ToLowerInvariant()
            End Using
        End Using
    End Function

    Private Shared Function NormalizeSha256(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then Return Nothing

        Dim normalized = value.Trim()
        If normalized.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) Then normalized = normalized.Substring("sha256:".Length)
        normalized = normalized.Replace(" ", "").Replace("-", "").Replace(":", "").ToLowerInvariant()

        If normalized.Length <> 64 Then Throw New ArgumentException("SHA256 必须是 64 位十六进制字符串。", NameOf(value))
        For Each ch As Char In normalized
            If Not Uri.IsHexDigit(ch) Then Throw New ArgumentException("SHA256 只能包含十六进制字符。", NameOf(value))
        Next

        Return normalized
    End Function

    Private Sub ResetRuntimeProgress()
        _segments = Nothing
        _supportsRange = False
        _totalBytes = -1
        _speed = 0
        Interlocked.Exchange(_bytesDownloaded, 0)
        _lastTickBytes = 0
        _lastTickTime = DateTime.UtcNow
        _lastProgressReport = DateTime.MinValue
        _lastStateFlush = DateTime.MinValue
        _bytesAtLastStateFlush = 0
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
                .ETag = _remoteETag,
                .LastModified = _remoteLastModified,
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
            If File.Exists(p & ".tmp") Then File.Delete(p & ".tmp")
        Catch
        End Try
    End Sub

    Private Sub TryDeleteTempFiles()
        TryDeleteDownloadState(SavePath & ".part")
    End Sub

    Private Sub TryDeleteDownloadState(partPath As String)
        Try
            If File.Exists(partPath) Then File.Delete(partPath)
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
    ''' <summary>
    ''' 已下载字节数。必须声明为字段（而非属性），才能让 Interlocked.Add/Read 直接操作
    ''' backing memory，保证多线程并发累加时的原子性。若改为属性，VB.NET 会先将属性值
    ''' 复制到临时变量再写回，整个过程不是原子操作，多线程下会丢失更新。
    ''' </summary>
    <System.Text.Json.Serialization.JsonInclude>
    Public Downloaded As Long
End Class

''' <summary>持久化到 .lkdl 的状态。</summary>
Public Class PersistState
    Public Property Url As String
    Public Property TotalBytes As Long
    Public Property ETag As String
    Public Property LastModified As DateTimeOffset?
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
