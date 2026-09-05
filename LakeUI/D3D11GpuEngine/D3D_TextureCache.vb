''' <summary>
''' D3D_TextureCache 是新核心统一的 GPU 预算缓存，覆盖 background snapshot、image texture、text layer、blur intermediate 和 offscreen layer。
''' 它按 device generation 判定资源是否过期，预算以 GPU bytes 为主；CPU bytes 只统计必要 staging 和极小读回。
''' 它不在正在绘制的 target 上执行 trim，调用方必须在 BeginFrame 外或确认资源不再被当前帧引用时清理。
''' </summary>
Public NotInheritable Class D3D_TextureCache
    Implements D3D_IRenderCacheOwner, IDisposable

    Private ReadOnly _entries As New Dictionary(Of Object, D3D_TextureCacheEntry)()
    Private ReadOnly _使用顺序 As New LinkedList(Of D3D_TextureCacheEntry)()
    Private ReadOnly _retiredResources As New List(Of D3D_TextureCacheEntry)()
    Private ReadOnly _retiredLock As New Object()
    Private _totalGpuBytes As Long
    Private _retiredGpuBytes As Long
    Private _retiredDisposeScheduled As Integer
    Private _frameUseDepth As Integer
    Private _trimPending As Boolean
    Private _维护上下文 As Threading.SynchronizationContext
    Private _维护已排队 As Boolean
    Private _disposed As Boolean

    Public Property BudgetBytes As Long = 256L * 1024L * 1024L

    Public Sub New()
        SyncBudget()
        D3D_GpuCache.Register(Me)
    End Sub

    Public ReadOnly Property TotalGpuBytes As Long
        Get
            Return _totalGpuBytes + _retiredGpuBytes
        End Get
    End Property

    Private ReadOnly Property CacheBytes As Long Implements D3D_IRenderCacheOwner.CacheBytes
        Get
            Return _totalGpuBytes + _retiredGpuBytes
        End Get
    End Property

    Private ReadOnly Property OldestUseTick As Long Implements D3D_IRenderCacheOwner.OldestUseTick
        Get
            If _frameUseDepth > 0 Then Return Long.MaxValue
            If _使用顺序.First Is Nothing Then Return Long.MaxValue
            Return _使用顺序.First.Value.LastUsed
        End Get
    End Property

    Private Function TrimOldest() As Boolean Implements D3D_IRenderCacheOwner.TrimOldest
        If _frameUseDepth > 0 Then
            _trimPending = True
            Return False
        End If
        If _entries.Count = 0 Then Return False
        Dim 最旧项 = _使用顺序.First.Value
        RemoveEntry(最旧项.Key, 最旧项)
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
                If entry.使用节点 IsNot _使用顺序.Last Then
                    _使用顺序.Remove(entry.使用节点)
                    _使用顺序.AddLast(entry.使用节点)
                End If
                Return DirectCast(entry.Resource, T)
            End If

            RemoveEntry(key, entry)
        End If

        Dim resource = factory()
        If resource Is Nothing Then Return Nothing

        entry = New D3D_TextureCacheEntry(key, resource, generation, Math.Max(0, gpuBytes), NextClock())
        _entries(key) = entry
        entry.使用节点 = _使用顺序.AddLast(entry)
        _totalGpuBytes += entry.GpuBytes
        RequestBudgetTrim(protectedKey:=key)
        Return resource
    End Function

    Friend Sub BeginFrameUse()
        If _disposed Then Return
        If TypeOf Threading.SynchronizationContext.Current Is WindowsFormsSynchronizationContext Then
            _维护上下文 = Threading.SynchronizationContext.Current
        End If
        _frameUseDepth += 1
    End Sub

    Friend Sub EndFrameUse()
        If _frameUseDepth > 0 Then _frameUseDepth -= 1
        If _frameUseDepth > 0 Then Return

        ScheduleRetiredResourceRelease()
        If Not _trimPending OrElse _维护已排队 OrElse _维护上下文 Is Nothing Then Return
        _维护已排队 = True
        _维护上下文.Post(AddressOf 执行预算维护, Nothing)
    End Sub

    Private Sub 执行预算维护(状态 As Object)
        _维护已排队 = False
        If _disposed OrElse Not _trimPending OrElse _frameUseDepth > 0 Then Return
        _trimPending = False
        TrimToBudget(force:=False)
        D3D_GpuCache.TrimToBudget(Me)
    End Sub

    Private Sub RequestBudgetTrim(Optional protectedKey As Object = Nothing)
        SyncBudget()
        If _frameUseDepth > 0 Then
            _trimPending = True
            Return
        End If

        TrimToBudget(force:=False, protectedKey:=protectedKey)
        ' 新资源创建完成后才允许触发全局预算维护；这是低频、可控的入口。
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
            Dim 最旧节点 = _使用顺序.First
            If Object.Equals(最旧节点.Value.Key, protectedKey) Then 最旧节点 = 最旧节点.Next
            If 最旧节点 Is Nothing Then Exit While
            Dim 最旧项 = 最旧节点.Value
            RemoveEntry(最旧项.Key, 最旧项)
        End While
    End Sub

    Public Sub ReleaseAll() Implements D3D_IRenderCacheOwner.ReleaseAll
        For Each entry In _entries.Values.ToArray()
            RetireOrDispose(entry)
        Next
        _entries.Clear()
        _使用顺序.Clear()
        _totalGpuBytes = 0
        If _frameUseDepth = 0 Then DisposeRetiredResources()
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
        _使用顺序.Remove(entry.使用节点)
        _totalGpuBytes -= entry.GpuBytes
        RetireOrDispose(entry)
    End Sub

    Private Sub RetireOrDispose(entry As D3D_TextureCacheEntry)
        If entry Is Nothing OrElse entry.Resource Is Nothing Then Return
        If _frameUseDepth > 0 Then
            SyncLock _retiredLock
                _retiredResources.Add(entry)
                _retiredGpuBytes += entry.GpuBytes
            End SyncLock
        Else
            DisposeEntry(entry)
        End If
    End Sub

    Private Sub ScheduleRetiredResourceRelease()
        If Threading.Interlocked.CompareExchange(_retiredDisposeScheduled, 1, 0) <> 0 Then Return
        Dim retired As D3D_TextureCacheEntry() = Nothing
        SyncLock _retiredLock
            If _retiredResources.Count > 0 Then
                retired = _retiredResources.ToArray()
                _retiredResources.Clear()
            End If
        End SyncLock
        If retired Is Nothing Then
            Threading.Interlocked.Exchange(_retiredDisposeScheduled, 0)
            Return
        End If
        Threading.ThreadPool.QueueUserWorkItem(
            Sub(state)
                Try
                    For Each entry In retired
                        DisposeEntry(entry)
                    Next
                Finally
                    Dim 已释放字节数 As Long = retired.Sum(Function(项) Math.Max(0L, 项.GpuBytes))
                    SyncLock _retiredLock
                        _retiredGpuBytes = Math.Max(0L, _retiredGpuBytes - 已释放字节数)
                    End SyncLock
                    Threading.Interlocked.Exchange(_retiredDisposeScheduled, 0)
                    Dim more As Boolean
                    SyncLock _retiredLock
                        more = _retiredResources.Count > 0
                    End SyncLock
                    If more AndAlso _frameUseDepth = 0 Then ScheduleRetiredResourceRelease()
                End Try
            End Sub)
    End Sub

    Private Sub DisposeRetiredResources()
        Dim retired As D3D_TextureCacheEntry()
        SyncLock _retiredLock
            If _retiredResources.Count = 0 Then Return
            retired = _retiredResources.ToArray()
            _retiredResources.Clear()
        End SyncLock
        For Each entry In retired
            DisposeEntry(entry)
        Next
        Dim 已释放字节数 As Long = retired.Sum(Function(项) Math.Max(0L, 项.GpuBytes))
        SyncLock _retiredLock
            _retiredGpuBytes = Math.Max(0L, _retiredGpuBytes - 已释放字节数)
        End SyncLock
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
        Public Property 使用节点 As LinkedListNode(Of D3D_TextureCacheEntry)
    End Class
End Class
