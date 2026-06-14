Imports System.Collections.Concurrent
Imports System.Net
Imports System.Net.Http
Imports System.Net.NetworkInformation
Imports System.Net.Sockets
Imports System.Text
Imports System.Text.Json
Imports System.Threading

''' <summary>
''' 封装服务器 JSON 单次网络请求，并支持按调用参数进行 SRV 实际地址解析。
''' </summary>
''' <remarks>
''' 推荐使用异步方法：Await SRV_JsonSever.GetJsonAsync(Of 配置类型)("https://example.com/config.json")。
''' 需要同步调用时可使用：Dim cfg = SRV_JsonSever.GetJson(Of 配置类型)("https://example.com/config.json")。
''' 同步包装在 UI 线程调用时会通过消息泵循环等待，既不阻塞界面，又能在调用点等到结果再返回。
''' 需要 SRV 解析时传入服务名，例如 srvServiceName:="_service"，内部会查询 _service._tcp.主机名；
''' 若解析到 SRV 目标，则拼接为 协议://SRV目标主机:SRV端口/原路径 后再请求，使 TLS 证书校验目标主机。
''' 若没有传入 SRV 服务名、没有 SRV 记录或 DNS 查询失败，则自动使用原始请求地址。
''' </remarks>
Public Class SRV_JsonSever

#Region " 共享 HTTP 客户端 "

    ''' <summary>共享 HttpClient，避免端口耗尽。</summary>
    Private Shared ReadOnly _client As HttpClient = CreateClient()

    ''' <summary>
    ''' 创建用于 JSON 请求的 HttpClient。
    ''' </summary>
    ''' <returns>配置完成的 HttpClient 实例。</returns>
    Private Shared Function CreateClient() As HttpClient
        Dim handler As New SocketsHttpHandler() With {
            .AllowAutoRedirect = True,
            .AutomaticDecompression = Net.DecompressionMethods.GZip Or Net.DecompressionMethods.Deflate
        }
        Return New HttpClient(handler) With {
            .Timeout = TimeSpan.FromSeconds(30)
        }
    End Function

#End Region

#Region " 异步 API "

    ''' <summary>
    ''' 从指定服务器地址获取 JSON，并反序列化为指定 Class 结构。
    ''' </summary>
    ''' <typeparam name="T">目标 Class 类型。</typeparam>
    ''' <param name="serverAddress">服务器 JSON 地址，例如 "https://example.com/config.json"。</param>
    ''' <param name="options">JSON 反序列化选项；为 Nothing 时使用默认设置。</param>
    ''' <param name="cancellationToken">取消令牌。</param>
    ''' <param name="srvServiceName">SRV 服务名，例如 _service；为 Nothing 时不进行 SRV 解析。</param>
    ''' <returns>反序列化后的目标对象。</returns>
    ''' <remarks>
    ''' 使用方法：Await WebSever.GetJsonAsync(Of 配置类型)("https://example.com/config.json", srvServiceName:="_service")。
    ''' </remarks>
    Public Shared Async Function GetJsonAsync(Of T)(serverAddress As String,
                                                    Optional options As JsonSerializerOptions = Nothing,
                                                    Optional cancellationToken As CancellationToken = Nothing,
                                                    Optional srvServiceName As String = Nothing) As Task(Of T)
        ValidateServerAddress(serverAddress)

        Dim requestAddress As String = Await ResolveRequestAddressAsync(serverAddress, srvServiceName, cancellationToken).ConfigureAwait(False)
        Dim json As String = Await GetStringAsync(requestAddress, cancellationToken).ConfigureAwait(False)
        Dim result As T = JsonSerializer.Deserialize(Of T)(json, options)
        If result Is Nothing Then
            Throw New InvalidOperationException("JSON 反序列化结果为空。")
        End If

        Return result
    End Function

    Public Shared Async Function GetJsonAsync(serverAddress As String,
                                                    Optional options As JsonSerializerOptions = Nothing,
                                                    Optional cancellationToken As CancellationToken = Nothing,
                                                    Optional srvServiceName As String = Nothing) As Task(Of String)
        ValidateServerAddress(serverAddress)

        Dim requestAddress As String = Await ResolveRequestAddressAsync(serverAddress, srvServiceName, cancellationToken).ConfigureAwait(False)
        Return Await GetStringAsync(requestAddress, cancellationToken).ConfigureAwait(False)
    End Function

    Public Shared Async Function PostJsonAsync(Of TRequest, TResponse)(serverAddress As String,
                                                                       requestBody As TRequest,
                                                                       Optional options As JsonSerializerOptions = Nothing,
                                                                       Optional cancellationToken As CancellationToken = Nothing,
                                                                       Optional srvServiceName As String = Nothing) As Task(Of TResponse)
        ValidateServerAddress(serverAddress)

        Dim requestAddress As String = Await ResolveRequestAddressAsync(serverAddress, srvServiceName, cancellationToken).ConfigureAwait(False)
        Dim json As String = JsonSerializer.Serialize(requestBody, options)

        Using content As New StringContent(json, Encoding.UTF8, "application/json")
            Using response As HttpResponseMessage = Await _client.PostAsync(requestAddress, content, cancellationToken).ConfigureAwait(False)
                Dim responseText As String = Await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False)
                If Not response.IsSuccessStatusCode Then Throw New HttpRequestException($"{CInt(response.StatusCode)} {response.ReasonPhrase}: {responseText}")

                Dim result As TResponse = JsonSerializer.Deserialize(Of TResponse)(responseText, options)
                If result Is Nothing Then Throw New InvalidOperationException("JSON 反序列化结果为空。")
                Return result
            End Using
        End Using
    End Function

#End Region

#Region " 同步包装（UI 线程友好） "

    ''' <summary>
    ''' 从指定服务器地址同步获取 JSON，并反序列化为指定 Class 结构。
    ''' </summary>
    ''' <typeparam name="T">目标 Class 类型。</typeparam>
    ''' <param name="serverAddress">服务器 JSON 地址，例如 "https://example.com/config.json"。</param>
    ''' <param name="options">JSON 反序列化选项；为 Nothing 时使用默认设置。</param>
    ''' <param name="srvServiceName">SRV 服务名，例如 _service；为 Nothing 时不进行 SRV 解析。</param>
    ''' <returns>反序列化后的目标对象。</returns>
    ''' <remarks>
    ''' 使用方法：Dim cfg = SRV_JsonSever.GetJson(Of 配置类型)("https://example.com/config.json", srvServiceName:="_service")。
    ''' UI 线程中会通过消息泵等待任务完成；后台线程中会普通阻塞等待。
    ''' </remarks>
    Public Shared Function GetJson(Of T)(serverAddress As String,
                                         Optional options As JsonSerializerOptions = Nothing,
                                         Optional srvServiceName As String = Nothing) As T
        Return WaitOnCurrentThread(GetJsonAsync(Of T)(serverAddress, options, Nothing, srvServiceName))
    End Function

    ''' <summary>
    ''' 等待 Task 完成并返回结果：
    ''' 如果当前线程是 UI 线程，则在等待过程中持续抽取消息泵，避免界面卡死；
    ''' 如果在后台线程上调用，则普通阻塞等待。
    ''' </summary>
    ''' <typeparam name="T">Task 结果类型。</typeparam>
    ''' <param name="task">要等待的任务。</param>
    ''' <returns>Task 的执行结果。</returns>
    Private Shared Function WaitOnCurrentThread(Of T)(task As Task(Of T)) As T
        ArgumentNullException.ThrowIfNull(task)

        Dim isUIThread As Boolean = (System.Windows.Forms.Application.MessageLoop AndAlso
                                     SynchronizationContext.Current IsNot Nothing)

        If isUIThread Then
            While Not task.IsCompleted
                System.Windows.Forms.Application.DoEvents()
                Thread.Sleep(10)
            End While
        End If

        Try
            ' 已完成或后台线程：直接同步取结果，会抛出原始异常（而不是 AggregateException）。
            Return task.GetAwaiter().GetResult()
        Catch ex As OperationCanceledException
            Throw
        End Try
    End Function

#End Region

#Region " SRV 解析 "

    ''' <summary>DNS SRV 记录类型编号。</summary>
    Private Const DnsSrvType As Integer = 33

    ''' <summary>SRV 查询结果缓存，键为完整 SRV 查询名。</summary>
    Private Shared ReadOnly _srvCache As New ConcurrentDictionary(Of String, SrvCacheItem)()

    ''' <summary>缓存后的 SRV 解析结果。</summary>
    Private NotInheritable Class SrvCacheItem
        ''' <summary>解析到的目标终结点；为 Nothing 表示短期缓存“无 SRV 记录”。</summary>
        Public Property Endpoint As DnsEndPoint

        ''' <summary>缓存过期时间。</summary>
        Public Property ExpiresAt As DateTimeOffset
    End Class

    ''' <summary>DNS SRV 记录内容。</summary>
    Private NotInheritable Class SrvRecord
        ''' <summary>优先级，数值越小优先级越高。</summary>
        Public Property Priority As Integer

        ''' <summary>同优先级内的权重。</summary>
        Public Property Weight As Integer

        ''' <summary>目标服务端口。</summary>
        Public Property Port As Integer

        ''' <summary>目标主机名。</summary>
        Public Property Target As String

        ''' <summary>DNS 记录 TTL 秒数。</summary>
        Public Property TimeToLive As Integer
    End Class

    ''' <summary>
    ''' 按 SRV 服务名将逻辑请求地址解析为实际请求地址。
    ''' </summary>
    ''' <param name="serverAddress">调用方传入的逻辑请求地址。</param>
    ''' <param name="srvServiceName">SRV 服务名，例如 _service；为空时不进行 SRV 解析。</param>
    ''' <param name="cancellationToken">取消令牌。</param>
    ''' <returns>实际请求地址；未启用 SRV 或没有 SRV 记录时返回原地址。</returns>
    Private Shared Async Function ResolveRequestAddressAsync(serverAddress As String,
                                                             srvServiceName As String,
                                                             cancellationToken As CancellationToken) As Task(Of String)
        If String.IsNullOrWhiteSpace(srvServiceName) Then Return serverAddress

        Dim uri As New Uri(serverAddress, UriKind.Absolute)
        Dim endpoint As DnsEndPoint = Await ResolveSrvEndpointAsync(uri.Host, srvServiceName, cancellationToken).ConfigureAwait(False)
        If endpoint Is Nothing Then Return serverAddress

        Dim builder As New UriBuilder(uri) With {
            .Host = endpoint.Host,
            .Port = endpoint.Port
        }
        Return builder.Uri.AbsoluteUri
    End Function

    ''' <summary>
    ''' 按指定服务名查询 SRV 记录，并返回应请求的目标终结点。
    ''' </summary>
    ''' <param name="host">逻辑入口主机名。</param>
    ''' <param name="serviceName">SRV 服务名，例如 _service。</param>
    ''' <param name="cancellationToken">取消令牌。</param>
    ''' <returns>SRV 目标终结点；没有可用 SRV 记录时返回 Nothing。</returns>
    Private Shared Async Function ResolveSrvEndpointAsync(host As String,
                                                           serviceName As String,
                                                           cancellationToken As CancellationToken) As Task(Of DnsEndPoint)
        Dim queryName As String = $"{serviceName.Trim().TrimEnd("."c)}._tcp.{host.TrimEnd("."c)}"
        Dim cacheKey As String = queryName.ToLowerInvariant()
        Dim now As DateTimeOffset = DateTimeOffset.UtcNow
        Dim cached As SrvCacheItem = Nothing
        If _srvCache.TryGetValue(cacheKey, cached) AndAlso cached.ExpiresAt > now Then
            Return cached.Endpoint
        End If

        Dim records As List(Of SrvRecord) = Await QuerySrvRecordsAsync(queryName, cancellationToken).ConfigureAwait(False)
        Dim selected As SrvRecord = SelectSrvRecord(records)
        If selected Is Nothing OrElse String.IsNullOrWhiteSpace(selected.Target) OrElse selected.Target = "." Then
            _srvCache(cacheKey) = New SrvCacheItem With {.Endpoint = Nothing, .ExpiresAt = now.AddMinutes(5)}
            Return Nothing
        End If

        Dim resolved As New DnsEndPoint(selected.Target.TrimEnd("."c), selected.Port)
        Dim ttlSeconds As Integer = Math.Max(30, selected.TimeToLive)
        _srvCache(cacheKey) = New SrvCacheItem With {.Endpoint = resolved, .ExpiresAt = now.AddSeconds(ttlSeconds)}
        Return resolved
    End Function

    ''' <summary>
    ''' 依次向本机配置的 DNS 服务器查询 SRV 记录。
    ''' </summary>
    ''' <param name="queryName">完整 SRV 查询名。</param>
    ''' <param name="cancellationToken">取消令牌。</param>
    ''' <returns>查询到的 SRV 记录集合；查询失败时返回空集合。</returns>
    Private Shared Async Function QuerySrvRecordsAsync(queryName As String,
                                                       cancellationToken As CancellationToken) As Task(Of List(Of SrvRecord))
        For Each dnsAddress As IPAddress In GetDnsServerAddresses()
            cancellationToken.ThrowIfCancellationRequested()

            Try
                Dim records As List(Of SrvRecord) = Await QuerySrvRecordsAsync(queryName, dnsAddress, cancellationToken).ConfigureAwait(False)
                If records.Count > 0 Then Return records
            Catch ex As OperationCanceledException
                Throw
            Catch
                ' 当前 DNS 服务器不可用时尝试下一个
            End Try
        Next

        Return New List(Of SrvRecord)()
    End Function

    ''' <summary>
    ''' 向指定 DNS 服务器发送 UDP SRV 查询并解析响应。
    ''' </summary>
    ''' <param name="queryName">完整 SRV 查询名。</param>
    ''' <param name="dnsAddress">DNS 服务器地址。</param>
    ''' <param name="cancellationToken">取消令牌。</param>
    ''' <returns>DNS 响应中的 SRV 记录集合。</returns>
    Private Shared Async Function QuerySrvRecordsAsync(queryName As String,
                                                       dnsAddress As IPAddress,
                                                       cancellationToken As CancellationToken) As Task(Of List(Of SrvRecord))
        Dim queryId As Integer = Random.Shared.Next(0, UShort.MaxValue + 1)
        Dim query As Byte() = BuildSrvQuery(queryName, queryId)

        Using udp As New UdpClient(dnsAddress.AddressFamily)
            udp.Connect(New IPEndPoint(dnsAddress, 53))
            Await udp.SendAsync(query, query.Length).ConfigureAwait(False)

            Dim response As UdpReceiveResult = Await udp.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(False)
            Return ParseSrvResponse(response.Buffer, queryId)
        End Using
    End Function

    ''' <summary>
    ''' 获取当前系统已启用网络适配器配置的 DNS 服务器地址。
    ''' </summary>
    ''' <returns>DNS 服务器地址集合。</returns>
    Private Shared Function GetDnsServerAddresses() As List(Of IPAddress)
        Dim result As New List(Of IPAddress)()

        For Each adapter As NetworkInterface In NetworkInterface.GetAllNetworkInterfaces()
            If adapter.OperationalStatus <> OperationalStatus.Up Then Continue For

            For Each address As IPAddress In adapter.GetIPProperties().DnsAddresses
                If address.AddressFamily = AddressFamily.InterNetwork OrElse address.AddressFamily = AddressFamily.InterNetworkV6 Then
                    If Not result.Contains(address) Then result.Add(address)
                End If
            Next
        Next

        Return result
    End Function

    ''' <summary>
    ''' 构造标准 DNS SRV 查询报文。
    ''' </summary>
    ''' <param name="queryName">完整 SRV 查询名。</param>
    ''' <param name="queryId">DNS 查询 ID。</param>
    ''' <returns>DNS 查询报文字节数组。</returns>
    Private Shared Function BuildSrvQuery(queryName As String, queryId As Integer) As Byte()
        Dim buffer As New List(Of Byte)()
        AddUInt16(buffer, queryId)
        AddUInt16(buffer, &H100)
        AddUInt16(buffer, 1)
        AddUInt16(buffer, 0)
        AddUInt16(buffer, 0)
        AddUInt16(buffer, 0)

        For Each label As String In queryName.Split("."c)
            Dim bytes As Byte() = Encoding.ASCII.GetBytes(label)
            If bytes.Length = 0 OrElse bytes.Length > 63 Then Throw New InvalidOperationException("SRV 查询名称无效。")
            buffer.Add(CByte(bytes.Length))
            buffer.AddRange(bytes)
        Next

        buffer.Add(0)
        AddUInt16(buffer, DnsSrvType)
        AddUInt16(buffer, 1)
        Return buffer.ToArray()
    End Function

    ''' <summary>
    ''' 解析 DNS 响应报文中的 SRV 记录。
    ''' </summary>
    ''' <param name="message">DNS 响应报文字节数组。</param>
    ''' <param name="queryId">期望的 DNS 查询 ID。</param>
    ''' <returns>响应中的 SRV 记录集合。</returns>
    Private Shared Function ParseSrvResponse(message As Byte(), queryId As Integer) As List(Of SrvRecord)
        Dim records As New List(Of SrvRecord)()
        If message.Length < 12 OrElse ReadUInt16(message, 0) <> queryId Then Return records
        If (ReadUInt16(message, 2) And &HF) <> 0 Then Return records

        Dim questionCount As Integer = ReadUInt16(message, 4)
        Dim answerCount As Integer = ReadUInt16(message, 6)
        Dim offset As Integer = 12

        For i As Integer = 0 To questionCount - 1
            ReadDnsName(message, offset)
            offset += 4
            If offset > message.Length Then Return records
        Next

        For i As Integer = 0 To answerCount - 1
            ReadDnsName(message, offset)
            If offset + 10 > message.Length Then Return records

            Dim recordType As Integer = ReadUInt16(message, offset)
            Dim recordClass As Integer = ReadUInt16(message, offset + 2)
            Dim ttl As Integer = ReadInt32(message, offset + 4)
            Dim dataLength As Integer = ReadUInt16(message, offset + 8)
            offset += 10

            Dim dataEnd As Integer = offset + dataLength
            If dataEnd > message.Length Then Return records

            If recordType = DnsSrvType AndAlso recordClass = 1 AndAlso dataLength >= 7 Then
                Dim targetOffset As Integer = offset + 6
                records.Add(New SrvRecord With {
                    .Priority = ReadUInt16(message, offset),
                    .Weight = ReadUInt16(message, offset + 2),
                    .Port = ReadUInt16(message, offset + 4),
                    .Target = ReadDnsName(message, targetOffset),
                    .TimeToLive = ttl
                })
            End If

            offset = dataEnd
        Next

        Return records
    End Function

    ''' <summary>
    ''' 按 RFC 2782 的 priority 与 weight 规则选择一个 SRV 目标。
    ''' </summary>
    ''' <param name="records">候选 SRV 记录集合。</param>
    ''' <returns>选中的 SRV 记录；没有候选项时返回 Nothing。</returns>
    Private Shared Function SelectSrvRecord(records As List(Of SrvRecord)) As SrvRecord
        If records Is Nothing OrElse records.Count = 0 Then Return Nothing

        Dim bestPriority As Integer = Integer.MaxValue
        For Each record As SrvRecord In records
            If record.Priority < bestPriority Then bestPriority = record.Priority
        Next

        Dim candidates As New List(Of SrvRecord)()
        Dim totalWeight As Integer = 0
        For Each record As SrvRecord In records
            If record.Priority = bestPriority Then
                candidates.Add(record)
                totalWeight += Math.Max(0, record.Weight)
            End If
        Next

        If candidates.Count = 0 Then Return Nothing
        If totalWeight <= 0 Then Return candidates(Random.Shared.Next(candidates.Count))

        Dim pick As Integer = Random.Shared.Next(1, totalWeight + 1)
        Dim current As Integer = 0
        For Each record As SrvRecord In candidates
            current += Math.Max(0, record.Weight)
            If pick <= current Then Return record
        Next

        Return candidates(candidates.Count - 1)
    End Function

    ''' <summary>
    ''' 以网络字节序写入 16 位无符号整数。
    ''' </summary>
    ''' <param name="buffer">目标缓冲区。</param>
    ''' <param name="value">要写入的数值。</param>
    Private Shared Sub AddUInt16(buffer As List(Of Byte), value As Integer)
        buffer.Add(CByte((value >> 8) And &HFF))
        buffer.Add(CByte(value And &HFF))
    End Sub

    ''' <summary>
    ''' 以网络字节序读取 16 位无符号整数。
    ''' </summary>
    ''' <param name="buffer">源缓冲区。</param>
    ''' <param name="offset">读取起始偏移。</param>
    ''' <returns>读取到的数值。</returns>
    Private Shared Function ReadUInt16(buffer As Byte(), offset As Integer) As Integer
        Return (CInt(buffer(offset)) << 8) Or buffer(offset + 1)
    End Function

    ''' <summary>
    ''' 以网络字节序读取 32 位整数。
    ''' </summary>
    ''' <param name="buffer">源缓冲区。</param>
    ''' <param name="offset">读取起始偏移。</param>
    ''' <returns>读取到的数值。</returns>
    Private Shared Function ReadInt32(buffer As Byte(), offset As Integer) As Integer
        Return (CInt(buffer(offset)) << 24) Or
               (CInt(buffer(offset + 1)) << 16) Or
               (CInt(buffer(offset + 2)) << 8) Or
               buffer(offset + 3)
    End Function

    ''' <summary>
    ''' 读取 DNS 报文中的域名，支持 DNS 名称压缩指针。
    ''' </summary>
    ''' <param name="message">DNS 报文字节数组。</param>
    ''' <param name="offset">读取起始偏移；返回时指向域名后的下一个字节。</param>
    ''' <returns>解析出的域名。</returns>
    Private Shared Function ReadDnsName(message As Byte(), ByRef offset As Integer) As String
        Dim labels As New List(Of String)()
        Dim position As Integer = offset
        Dim jumped As Boolean = False
        Dim jumpCount As Integer = 0

        While position < message.Length
            Dim length As Integer = message(position)

            If (length And &HC0) = &HC0 Then
                If position + 1 >= message.Length Then Throw New InvalidOperationException("DNS 名称压缩指针无效。")
                Dim pointer As Integer = ((length And &H3F) << 8) Or message(position + 1)
                If pointer >= message.Length Then Throw New InvalidOperationException("DNS 名称压缩指针越界。")
                If Not jumped Then offset = position + 2
                position = pointer
                jumped = True
                jumpCount += 1
                If jumpCount > 16 Then Throw New InvalidOperationException("DNS 名称压缩指针循环。")
            ElseIf length = 0 Then
                If Not jumped Then offset = position + 1
                Exit While
            Else
                position += 1
                If position + length > message.Length Then Throw New InvalidOperationException("DNS 名称长度无效。")
                labels.Add(Encoding.ASCII.GetString(message, position, length))
                position += length
            End If
        End While

        Return String.Join(".", labels)
    End Function

#End Region

#Region " 内部辅助 "

    ''' <summary>
    ''' 验证服务器地址是否为 HTTP/HTTPS 绝对地址。
    ''' </summary>
    ''' <param name="serverAddress">待验证的服务器地址。</param>
    Private Shared Sub ValidateServerAddress(serverAddress As String)
        If String.IsNullOrWhiteSpace(serverAddress) Then Throw New ArgumentException("serverAddress 不能为空。", NameOf(serverAddress))

        Dim uri As Uri = Nothing
        If Not Uri.TryCreate(serverAddress, UriKind.Absolute, uri) OrElse
           (uri.Scheme <> Uri.UriSchemeHttp AndAlso uri.Scheme <> Uri.UriSchemeHttps) Then
            Throw New ArgumentException("serverAddress 必须是有效的 HTTP/HTTPS 绝对地址。", NameOf(serverAddress))
        End If
    End Sub

    ''' <summary>
    ''' 发送 GET 请求并读取响应字符串。
    ''' </summary>
    ''' <param name="url">请求地址。</param>
    ''' <param name="cancellationToken">取消令牌。</param>
    ''' <returns>响应正文字符串。</returns>
    Private Shared Async Function GetStringAsync(url As String, cancellationToken As CancellationToken) As Task(Of String)
        Using req As New HttpRequestMessage(HttpMethod.Get, url)
            Using resp As HttpResponseMessage = Await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(False)
                Await EnsureSuccessAsync(resp).ConfigureAwait(False)
                Return Await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False)
            End Using
        End Using
    End Function

    ''' <summary>把非 2xx 响应转换为带响应体提示的 HttpRequestException。</summary>
    ''' <param name="resp">HTTP 响应消息。</param>
    ''' <returns>表示异步检查过程的 Task。</returns>
    Private Shared Async Function EnsureSuccessAsync(resp As HttpResponseMessage) As Task
        If resp.IsSuccessStatusCode Then Return

        Dim body As String = String.Empty
        Try
            body = Await resp.Content.ReadAsStringAsync().ConfigureAwait(False)
        Catch
            ' 忽略读取响应体的异常
        End Try

        Dim snippet As String = If(body.Length > 300, String.Concat(body.AsSpan(0, 300), "…"), body)
        Throw New HttpRequestException($"服务器 JSON 请求失败：{CInt(resp.StatusCode)} {resp.ReasonPhrase}。{snippet}")
    End Function

#End Region

End Class
