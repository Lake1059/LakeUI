''' <summary>
''' D3D_TextureCache 是新核心统一的 GPU 预算缓存，覆盖 background snapshot、image texture、text layer、blur intermediate 和 offscreen layer。
''' 它按 device generation 判定资源是否过期，预算以 GPU bytes 为主；CPU bytes 只统计必要 staging 和极小读回。
''' 它不在正在绘制的 target 上执行 trim，调用方必须在 BeginFrame 外或确认资源不再被当前帧引用时清理。
''' </summary>
Public NotInheritable Class D3D_TextureCache
    Implements IDisposable

    Private ReadOnly _entries As New Dictionary(Of String, D3D_TextureCacheEntry)(StringComparer.Ordinal)
    Private _clock As Long
    Private _totalGpuBytes As Long
    Private _disposed As Boolean

    Public Property BudgetBytes As Long = 256L * 1024L * 1024L

    Public ReadOnly Property TotalGpuBytes As Long
        Get
            Return _totalGpuBytes
        End Get
    End Property

    ''' <summary>
    ''' 获取或创建 GPU texture-like 资源。factory 只能创建当前 device generation 的资源；旧 generation 命中会被释放并重建。
    ''' </summary>
    Public Function AcquireTexture(Of T As IDisposable)(key As String,
                                                        generation As Integer,
                                                        gpuBytes As Long,
                                                        factory As Func(Of T)) As T
        If _disposed Then Throw New ObjectDisposedException(NameOf(D3D_TextureCache))
        If String.IsNullOrEmpty(key) Then Throw New ArgumentException("Texture cache key is required.", NameOf(key))
        If factory Is Nothing Then Throw New ArgumentNullException(NameOf(factory))

        Dim entry As D3D_TextureCacheEntry = Nothing
        If _entries.TryGetValue(key, entry) Then
            If entry.Generation = generation AndAlso TypeOf entry.Resource Is T Then
                entry.LastUsed = NextClock()
                Return DirectCast(entry.Resource, T)
            End If

            RemoveEntry(key, entry)
        End If

        Dim resource = factory()
        If resource Is Nothing Then Return Nothing

        entry = New D3D_TextureCacheEntry(key, resource, generation, Math.Max(0, gpuBytes), NextClock())
        _entries(key) = entry
        _totalGpuBytes += entry.GpuBytes
        TrimToBudget(force:=False, protectedKey:=key)
        Return resource
    End Function

    ''' <summary>
    ''' 释放指定 key 的缓存资源。Release 不能在资源作为当前帧 target 时调用。
    ''' </summary>
    Public Sub Release(key As String)
        If String.IsNullOrEmpty(key) Then Return
        Dim entry As D3D_TextureCacheEntry = Nothing
        If _entries.TryGetValue(key, entry) Then RemoveEntry(key, entry)
    End Sub

    ''' <summary>
    ''' 释放指定前缀的一组资源。用于 ImageCache 等上层缓存只清理自己的 key 空间，避免误删 background snapshot 或 blur intermediate。
    ''' </summary>
    Public Sub ReleaseByPrefix(prefix As String)
        If String.IsNullOrEmpty(prefix) Then Return
        Dim keys = _entries.Keys.Where(Function(k) k.StartsWith(prefix, StringComparison.Ordinal)).ToArray()
        For Each key In keys
            Release(key)
        Next
    End Sub

    Public Sub InvalidateGeneration(generation As Integer)
        Dim keys = _entries.Values.Where(Function(e) e.Generation <> generation).Select(Function(e) e.Key).ToArray()
        For Each key In keys
            Release(key)
        Next
    End Sub

    ''' <summary>
    ''' 按 LRU 修剪到 GPU budget。force=True 时释放所有非空资源；调用方必须避开正在绘制的 target。
    ''' protectedKey 用于保护刚创建并即将返回给调用方的资源；即使单个资源超过预算，也不能在返回前把它 Dispose。
    ''' </summary>
    Public Sub TrimToBudget(force As Boolean, Optional protectedKey As String = Nothing)
        If force Then
            ReleaseAll()
            Return
        End If

        While _totalGpuBytes > BudgetBytes AndAlso _entries.Count > 0
            Dim victim = _entries.Values.
                Where(Function(e) Not String.Equals(e.Key, protectedKey, StringComparison.Ordinal)).
                OrderBy(Function(e) e.LastUsed).
                FirstOrDefault()
            If victim Is Nothing Then Exit While
            RemoveEntry(victim.Key, victim)
        End While
    End Sub

    Public Sub ReleaseAll()
        For Each entry In _entries.Values.ToArray()
            DisposeEntry(entry)
        Next
        _entries.Clear()
        _totalGpuBytes = 0
    End Sub

    Private Function NextClock() As Long
        _clock += 1
        Return _clock
    End Function

    Private Sub RemoveEntry(key As String, entry As D3D_TextureCacheEntry)
        If Not _entries.Remove(key) Then Return
        _totalGpuBytes -= entry.GpuBytes
        DisposeEntry(entry)
    End Sub

    Private Shared Sub DisposeEntry(entry As D3D_TextureCacheEntry)
        If entry Is Nothing OrElse entry.Resource Is Nothing Then Return
        Try : entry.Resource.Dispose() : Catch : End Try
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        ReleaseAll()
        GC.SuppressFinalize(Me)
    End Sub

    Private NotInheritable Class D3D_TextureCacheEntry
        Public Sub New(key As String, resource As IDisposable, generation As Integer, gpuBytes As Long, lastUsed As Long)
            Me.Key = key
            Me.Resource = resource
            Me.Generation = generation
            Me.GpuBytes = gpuBytes
            Me.LastUsed = lastUsed
        End Sub

        Public ReadOnly Property Key As String
        Public ReadOnly Property Resource As IDisposable
        Public ReadOnly Property Generation As Integer
        Public ReadOnly Property GpuBytes As Long
        Public Property LastUsed As Long
    End Class
End Class
