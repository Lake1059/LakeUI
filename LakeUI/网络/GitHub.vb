Imports System.IO
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks

''' <summary>
''' 封装常用的 GitHub 单次网络请求。
''' 所有方法均提供异步实现（推荐），同时提供同步包装：
''' 同步包装在 UI 线程调用时会通过消息泵循环等待，既不阻塞界面，又能在调用点等到结果再返回。
''' </summary>
Public Class GitHub

#Region " HttpClient "

    ''' <summary>GitHub API 要求必须提供 User-Agent，否则会返回 403。</summary>
    Private Const UserAgent As String = "LakeUI-GitHubClient"

    ''' <summary>共享 HttpClient，避免端口耗尽。</summary>
    Private Shared ReadOnly _client As HttpClient = CreateClient()

    Private Shared Function CreateClient() As HttpClient
        Dim handler As New HttpClientHandler() With {
            .AllowAutoRedirect = True,
            .AutomaticDecompression = Net.DecompressionMethods.GZip Or Net.DecompressionMethods.Deflate
        }
        Dim c As New HttpClient(handler) With {
            .Timeout = TimeSpan.FromSeconds(30)
        }
        c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent)
        c.DefaultRequestHeaders.Accept.Add(New MediaTypeWithQualityHeaderValue("application/vnd.github+json"))
        Return c
    End Function

#End Region

#Region " 异步 API "

    ''' <summary>
    ''' GitHub 发行版资源文件信息。
    ''' </summary>
    Public NotInheritable Class GitHubReleaseAssetInfo

        ''' <summary>资源文件名。</summary>
        Public Property FileName As String

        ''' <summary>资源文件下载地址。</summary>
        Public Property DownloadUrl As String

    End Class

    ''' <summary>
    ''' GitHub 发行版信息。
    ''' </summary>
    Public NotInheritable Class GitHubReleaseInfo

        ''' <summary>是否正确完成请求与解析。</summary>
        Public Property IsSuccess As Boolean

        ''' <summary>失败时的错误信息；成功时为空。</summary>
        Public Property ErrorMessage As String

        ''' <summary>发行版 tag_name。</summary>
        Public Property TagName As String

        ''' <summary>发行版附件资源列表。</summary>
        Public Property Assets As List(Of GitHubReleaseAssetInfo) = New List(Of GitHubReleaseAssetInfo)()

    End Class

    ''' <summary>
    ''' 获取仓库最新发行版（Release）中所有附件资源（assets）的下载地址。
    ''' </summary>
    ''' <param name="owner">仓库所有者，例如 "Lake1059"。</param>
    ''' <param name="repo">仓库名，例如 "LakeUI"。</param>
    ''' <param name="includePrerelease">是否包含预发行版本。False 时只取正式版。</param>
    ''' <param name="cancellationToken">取消令牌。</param>
    ''' <returns>包含 tag_name 与附件资源列表的发行版信息。</returns>
    Public Shared Async Function GetLatestReleaseAssetUrlsAsync(owner As String,
                                                                repo As String,
                                                                Optional includePrerelease As Boolean = False,
                                                                Optional cancellationToken As CancellationToken = Nothing) As Task(Of GitHubReleaseInfo)
        Try
            Return Await GetLatestReleaseAssetUrlsCoreAsync(owner, repo, includePrerelease, cancellationToken).ConfigureAwait(False)
        Catch ex As Exception
            Return New GitHubReleaseInfo With {
                .IsSuccess = False,
                .ErrorMessage = ex.Message
            }
        End Try
    End Function

    Private Shared Async Function GetLatestReleaseAssetUrlsCoreAsync(owner As String,
                                                                     repo As String,
                                                                     includePrerelease As Boolean,
                                                                     cancellationToken As CancellationToken) As Task(Of GitHubReleaseInfo)
        ValidateOwnerRepo(owner, repo)
        Dim url As String
        If includePrerelease Then
            ' /releases 按创建时间倒序返回，包含 prerelease。
            url = $"https://api.github.com/repos/{owner}/{repo}/releases?per_page=10"
        Else
            ' /releases/latest 已经排除 draft 与 prerelease。
            url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest"
        End If

        Dim json As String = Await GetStringAsync(url, cancellationToken).ConfigureAwait(False)
        Dim result As New GitHubReleaseInfo With {.IsSuccess = True}

        Using doc As JsonDocument = JsonDocument.Parse(json)
            Dim releaseElement As JsonElement
            If includePrerelease Then
                If doc.RootElement.ValueKind <> JsonValueKind.Array OrElse doc.RootElement.GetArrayLength() = 0 Then
                    Return result
                End If
                releaseElement = doc.RootElement(0)
            Else
                releaseElement = doc.RootElement
            End If

            Dim tag As JsonElement = Nothing
            If releaseElement.TryGetProperty("tag_name", tag) AndAlso tag.ValueKind = JsonValueKind.String Then
                result.TagName = tag.GetString()
            End If

            Dim assets As JsonElement = Nothing
            If releaseElement.TryGetProperty("assets", assets) AndAlso assets.ValueKind = JsonValueKind.Array Then
                For Each asset As JsonElement In assets.EnumerateArray()
                    Dim dl As JsonElement = Nothing
                    Dim nm As JsonElement = Nothing
                    If asset.TryGetProperty("browser_download_url", dl) AndAlso dl.ValueKind = JsonValueKind.String Then
                        Dim fileName As String
                        If asset.TryGetProperty("name", nm) AndAlso nm.ValueKind = JsonValueKind.String Then
                            fileName = nm.GetString()
                        Else
                            ' 兜底：从下载地址末段截取
                            Dim u As String = dl.GetString()
                            Dim slash As Integer = u.LastIndexOf("/"c)
                            fileName = If(slash >= 0 AndAlso slash < u.Length - 1, u.Substring(slash + 1), u)
                        End If
                        result.Assets.Add(New GitHubReleaseAssetInfo With {
                            .FileName = fileName,
                            .DownloadUrl = dl.GetString()
                        })
                    End If
                Next
            End If
        End Using

        Return result
    End Function

    ''' <summary>
    ''' 获取仓库中指定路径文件的可直接下载地址（download_url）。
    ''' </summary>
    ''' <param name="owner">仓库所有者。</param>
    ''' <param name="repo">仓库名。</param>
    ''' <param name="path">仓库内的相对路径，例如 "README.md" 或 "src/Foo.vb"。</param>
    ''' <param name="branch">分支或 commit/tag；为 Nothing 时使用默认分支。</param>
    ''' <param name="cancellationToken">取消令牌。</param>
    Public Shared Async Function GetFileDownloadUrlAsync(owner As String,
                                                         repo As String,
                                                         path As String,
                                                         Optional branch As String = Nothing,
                                                         Optional cancellationToken As CancellationToken = Nothing) As Task(Of String)
        ValidateOwnerRepo(owner, repo)
        If String.IsNullOrWhiteSpace(path) Then
            Throw New ArgumentException("path 不能为空。", NameOf(path))
        End If

        Dim url As String = $"https://api.github.com/repos/{owner}/{repo}/contents/{EncodePath(path)}"
        If Not String.IsNullOrWhiteSpace(branch) Then
            url &= "?ref=" & Uri.EscapeDataString(branch)
        End If

        Dim json As String = Await GetStringAsync(url, cancellationToken).ConfigureAwait(False)
        Using doc As JsonDocument = JsonDocument.Parse(json)
            ' contents API 在目标为文件时返回对象；为目录时返回数组。
            If doc.RootElement.ValueKind = JsonValueKind.Array Then
                Throw New InvalidOperationException($"路径 '{path}' 指向的是目录，不是单个文件。")
            End If
            Dim dl As JsonElement = Nothing
            If doc.RootElement.TryGetProperty("download_url", dl) AndAlso dl.ValueKind = JsonValueKind.String Then
                Return dl.GetString()
            End If
        End Using

        Throw New InvalidOperationException($"未能在 '{path}' 的响应中找到 download_url。")
    End Function

    ''' <summary>
    ''' 获取仓库中指定文本文件的内容（按 UTF-8 解码）。
    ''' </summary>
    ''' <param name="owner">仓库所有者。</param>
    ''' <param name="repo">仓库名。</param>
    ''' <param name="path">仓库内的相对路径。</param>
    ''' <param name="branch">分支或 commit/tag；为 Nothing 时使用默认分支。</param>
    ''' <param name="cancellationToken">取消令牌。</param>
    Public Shared Async Function GetFileTextAsync(owner As String,
                                                   repo As String,
                                                   path As String,
                                                   Optional branch As String = Nothing,
                                                   Optional cancellationToken As CancellationToken = Nothing) As Task(Of String)
        Dim downloadUrl As String = Await GetFileDownloadUrlAsync(owner, repo, path, branch, cancellationToken).ConfigureAwait(False)
        If String.IsNullOrEmpty(downloadUrl) Then
            Throw New InvalidOperationException("无法获取文件下载地址。")
        End If

        Using req As New HttpRequestMessage(HttpMethod.Get, downloadUrl)
            ' 直接命中 raw.githubusercontent.com，不需要 GitHub API 的 Accept。
            Using resp As HttpResponseMessage = Await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(False)
                Await EnsureSuccessAsync(resp).ConfigureAwait(False)
                Return Await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False)
            End Using
        End Using
    End Function

#End Region

#Region " 同步包装（UI 线程友好） "

    ''' <inheritdoc cref="GetLatestReleaseAssetUrlsAsync"/>
    Public Shared Function GetLatestReleaseAssetUrls(owner As String,
                                                      repo As String,
                                                      Optional includePrerelease As Boolean = False) As GitHubReleaseInfo
        Return WaitOnCurrentThread(GetLatestReleaseAssetUrlsAsync(owner, repo, includePrerelease))
    End Function

    ''' <inheritdoc cref="GetFileDownloadUrlAsync"/>
    Public Shared Function GetFileDownloadUrl(owner As String,
                                               repo As String,
                                               path As String,
                                               Optional branch As String = Nothing) As String
        Return WaitOnCurrentThread(GetFileDownloadUrlAsync(owner, repo, path, branch))
    End Function

    ''' <inheritdoc cref="GetFileTextAsync"/>
    Public Shared Function GetFileText(owner As String,
                                        repo As String,
                                        path As String,
                                        Optional branch As String = Nothing) As String
        Return WaitOnCurrentThread(GetFileTextAsync(owner, repo, path, branch))
    End Function

    ''' <summary>
    ''' 等待 Task 完成并返回结果：
    ''' 如果当前线程是 UI 线程，则在等待过程中持续抽取消息泵，避免界面卡死；
    ''' 如果在后台线程上调用，则普通阻塞等待。
    ''' </summary>
    Private Shared Function WaitOnCurrentThread(Of T)(task As Task(Of T)) As T
        If task Is Nothing Then Throw New ArgumentNullException(NameOf(task))

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

#Region " 内部辅助 "

    Private Shared Sub ValidateOwnerRepo(owner As String, repo As String)
        If String.IsNullOrWhiteSpace(owner) Then Throw New ArgumentException("owner 不能为空。", NameOf(owner))
        If String.IsNullOrWhiteSpace(repo) Then Throw New ArgumentException("repo 不能为空。", NameOf(repo))
    End Sub

    ''' <summary>对 URL 路径段做编码，但保留 '/'。</summary>
    Private Shared Function EncodePath(path As String) As String
        Dim parts() As String = path.Replace("\"c, "/"c).Trim("/"c).Split("/"c)
        For i As Integer = 0 To parts.Length - 1
            parts(i) = Uri.EscapeDataString(parts(i))
        Next
        Return String.Join("/"c, parts)
    End Function

    Private Shared Async Function GetStringAsync(url As String, cancellationToken As CancellationToken) As Task(Of String)
        Using req As New HttpRequestMessage(HttpMethod.Get, url)
            Using resp As HttpResponseMessage = Await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(False)
                Await EnsureSuccessAsync(resp).ConfigureAwait(False)
                Return Await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False)
            End Using
        End Using
    End Function

    ''' <summary>把非 2xx 响应转换为带响应体提示的 HttpRequestException。</summary>
    Private Shared Async Function EnsureSuccessAsync(resp As HttpResponseMessage) As Task
        If resp.IsSuccessStatusCode Then Return

        Dim body As String = String.Empty
        Try
            body = Await resp.Content.ReadAsStringAsync().ConfigureAwait(False)
        Catch
            ' 忽略读取响应体的异常
        End Try

        Dim snippet As String = If(body.Length > 300, body.Substring(0, 300) & "…", body)
        Throw New HttpRequestException($"GitHub 请求失败：{CInt(resp.StatusCode)} {resp.ReasonPhrase}。{snippet}")
    End Function

#End Region

End Class
