''' <summary>
''' D3D_TextureCache 是新核心统一的 GPU 预算缓存，覆盖 background snapshot、image texture、text layer、blur intermediate 和 offscreen layer。
''' 它按 device generation 判定资源是否过期，预算以 GPU bytes 为主；CPU bytes 只统计必要 staging 和极小读回。
''' 它不在正在绘制的 target 上执行 trim，调用方必须在 BeginFrame 外或确认资源不再被当前帧引用时清理。
''' </summary>
Public NotInheritable Class D3D_TextureCache
    Implements D3D_IRenderCacheOwner, IDisposable

    Private ReadOnly _entries As New Dictionary(Of Object, D3D_TextureCacheEntry)()
    Private _totalGpuBytes As Long
    Private _frameUseDepth As Integer
    Private _trimPending As Boolean
    Private _disposed As Boolean

    Public Property BudgetBytes As Long = 256L * 1024L * 1024L

    Public Sub New()
        SyncBudget()
        D3D_GpuCache.Register(Me)
    End Sub

    Public ReadOnly Property TotalGpuBytes As Long
        Get
            Return _totalGpuBytes
        End Get
    End Property

    Private ReadOnly Property CacheBytes As Long Implements D3D_IRenderCacheOwner.CacheBytes
        Get
            Return _totalGpuBytes
        End Get
    End Property

    Private ReadOnly Property OldestUseTick As Long Implements D3D_IRenderCacheOwner.OldestUseTick
        Get
            If _frameUseDepth > 0 Then Return Long.MaxValue
            If _entries.Count = 0 Then Return Long.MaxValue
            Return _entries.Values.Min(Function(e) e.LastUsed)
        End Get
    End Property

    Private Function TrimOldest() As Boolean Implements D3D_IRenderCacheOwner.TrimOldest
        If _frameUseDepth > 0 Then
            _trimPending = True
            Return False
        End If
        If _entries.Count = 0 Then Return False
        Dim victim = _entries.Values.OrderBy(Function(e) e.LastUsed).FirstOrDefault()
        If victim Is Nothing Then Return False
        RemoveEntry(victim.Key, victim)
        Return True
    End Function

    Friend Function ContainsTexture(Of T As IDisposable)(key As Object, generation As Integer) As Boolean
        If _disposed OrElse key Is Nothing Then Return False
        Dim entry As D3D_TextureCacheEntry = Nothing
        Return _entries.TryGetValue(key, entry) AndAlso
               entry IsNot Nothing AndAlso
               entry.Generation = generation AndAlso
               TypeOf entry.Resource Is T
    End Function

    ''' <summary>
    ''' 获取或创建 GPU texture-like 资源。factory 只能创建当前 device generation 的资源；旧 generation 命中会被释放并重建。
    ''' </summary>
    Public Function AcquireTexture(Of T As IDisposable)(key As Object,
                                                        generation As Integer,
                                                        gpuBytes As Long,
                                                        factory As Func(Of T)) As T
        If _disposed Then Throw New ObjectDisposedException(NameOf(D3D_TextureCache))
        If key Is Nothing Then Throw New ArgumentException("Texture cache key is required.", NameOf(key))
        If factory Is Nothing Then Throw New ArgumentNullException(NameOf(factory))
        SyncBudget()

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
        RequestBudgetTrim(protectedKey:=key)
        Return resource
    End Function

    Friend Sub BeginFrameUse()
        If _disposed Then Return
        _frameUseDepth += 1
    End Sub

    Friend Sub EndFrameUse()
        If _frameUseDepth > 0 Then _frameUseDepth -= 1
        If _frameUseDepth > 0 OrElse Not _trimPending Then Return

        _trimPending = False
        TrimToBudget(force:=False)
        D3D_GpuCache.TrimToBudget()
    End Sub

    Private Sub RequestBudgetTrim(Optional protectedKey As Object = Nothing)
        SyncBudget()
        If _frameUseDepth > 0 Then
            _trimPending = True
            Return
        End If

        TrimToBudget(force:=False, protectedKey:=protectedKey)
        D3D_GpuCache.TrimToBudget(Me)
    End Sub

    ''' <summary>
    ''' 释放指定 key 的缓存资源。Release 不能在资源作为当前帧 target 时调用。
    ''' </summary>
    Public Function Release(key As Object) As Boolean
        If key Is Nothing Then Return False
        Dim entry As D3D_TextureCacheEntry = Nothing
        If Not _entries.TryGetValue(key, entry) Then Return False
        RemoveEntry(key, entry)
        Return True
    End Function

    ''' <summary>
    ''' 释放指定前缀的一组资源。用于 ImageCache 等上层缓存只清理自己的 key 空间，避免误删 background snapshot 或 blur intermediate。
    ''' </summary>
    Public Function ReleaseByPrefix(prefix As String) As Boolean
        If String.IsNullOrEmpty(prefix) Then Return False
        Dim released As Boolean
        Dim keys = _entries.Keys.
            Where(Function(k) TypeOf k Is String AndAlso DirectCast(k, String).StartsWith(prefix, StringComparison.Ordinal)).
            ToArray()
        For Each key In keys
            released = Release(key) OrElse released
        Next
        Return released
    End Function

    Friend Function ReleaseWhere(predicate As Func(Of Object, Boolean)) As Boolean
        If predicate Is Nothing Then Return False
        Dim released As Boolean
        For Each key In _entries.Keys.Where(predicate).ToArray()
            released = Release(key) OrElse released
        Next
        Return released
    End Function

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
    Public Sub TrimToBudget(force As Boolean, Optional protectedKey As Object = Nothing)
        SyncBudget()
        If force Then
            ReleaseAll()
            Return
        End If

        While _totalGpuBytes > BudgetBytes AndAlso _entries.Count > 0
            Dim victim = _entries.Values.
                Where(Function(e) Not Object.Equals(e.Key, protectedKey)).
                OrderBy(Function(e) e.LastUsed).
                FirstOrDefault()
            If victim Is Nothing Then Exit While
            RemoveEntry(victim.Key, victim)
        End While
    End Sub

    Public Sub ReleaseAll() Implements D3D_IRenderCacheOwner.ReleaseAll
        For Each entry In _entries.Values.ToArray()
            DisposeEntry(entry)
        Next
        _entries.Clear()
        _totalGpuBytes = 0
        _trimPending = False
    End Sub

    Private Function NextClock() As Long
        Return D3D_GpuCache.NextTick()
    End Function

    Private Sub SyncBudget()
        BudgetBytes = Math.Max(0L, GlobalOptions.GpuCacheBudgetBytes)
    End Sub

    Private Sub RemoveEntry(key As Object, entry As D3D_TextureCacheEntry)
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
        Public Sub New(key As Object, resource As IDisposable, generation As Integer, gpuBytes As Long, lastUsed As Long)
            Me.Key = key
            Me.Resource = resource
            Me.Generation = generation
            Me.GpuBytes = gpuBytes
            Me.LastUsed = lastUsed
        End Sub

        Public ReadOnly Property Key As Object
        Public ReadOnly Property Resource As IDisposable
        Public ReadOnly Property Generation As Integer
        Public ReadOnly Property GpuBytes As Long
        Public Property LastUsed As Long
    End Class
End Class
