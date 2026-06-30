''' <summary>
''' 进程级渲染缓存预算协调器。GPU 与 CPU 缓存分别注册 owner，
''' 由 owner 自己负责释放最旧条目，协调器只做总量统计与全局 LRU 调度。
''' </summary>
Friend Interface IRenderCacheOwner
    ReadOnly Property CacheBytes As Long
    ReadOnly Property OldestUseTick As Long
    Function TrimOldest() As Boolean
    Sub ReleaseAll()
End Interface

Friend Module GpuCache
    Private ReadOnly _lock As New Object()
    Private ReadOnly _owners As New List(Of WeakReference(Of IRenderCacheOwner))()
    Private _tick As Long

    Friend Function NextTick() As Long
        SyncLock _lock
            _tick += 1
            Return _tick
        End SyncLock
    End Function

    Friend Sub Register(owner As IRenderCacheOwner)
        If owner Is Nothing Then Return
        SyncLock _lock
            CompactNoLock()
            For Each wr In _owners
                Dim existing As IRenderCacheOwner = Nothing
                If wr.TryGetTarget(existing) AndAlso ReferenceEquals(existing, owner) Then Return
            Next
            _owners.Add(New WeakReference(Of IRenderCacheOwner)(owner))
        End SyncLock
    End Sub

    Friend Sub TrimToBudget(Optional protectedOwner As IRenderCacheOwner = Nothing)
        TrimToBudget(Math.Max(0L, GlobalOptions.GpuCacheBudgetBytes), protectedOwner)
    End Sub

    Private Sub TrimToBudget(budget As Long, protectedOwner As IRenderCacheOwner)
        Dim guard As Integer = 0
        Do
            Dim total As Long = 0
            Dim oldest As IRenderCacheOwner = Nothing
            Dim oldestTick As Long = Long.MaxValue

            SyncLock _lock
                CompactNoLock()
                For Each wr In _owners
                    Dim owner As IRenderCacheOwner = Nothing
                    If Not wr.TryGetTarget(owner) OrElse owner Is Nothing Then Continue For
                    Dim bytes As Long = Math.Max(0L, owner.CacheBytes)
                    total += bytes
                    If bytes <= 0 OrElse ReferenceEquals(owner, protectedOwner) Then Continue For
                    Dim tick As Long = owner.OldestUseTick
                    If tick < oldestTick Then
                        oldestTick = tick
                        oldest = owner
                    End If
                Next
            End SyncLock

            If total <= budget OrElse oldest Is Nothing Then Exit Do
            If Not oldest.TrimOldest() Then Exit Do
            guard += 1
        Loop While guard < 4096
    End Sub

    Friend Sub ReleaseAll()
        Dim owners As List(Of IRenderCacheOwner) = SnapshotOwners()
        For Each owner In owners
            Try : owner.ReleaseAll() : Catch : End Try
        Next
    End Sub

    Private Function SnapshotOwners() As List(Of IRenderCacheOwner)
        Dim result As New List(Of IRenderCacheOwner)()
        SyncLock _lock
            CompactNoLock()
            For Each wr In _owners
                Dim owner As IRenderCacheOwner = Nothing
                If wr.TryGetTarget(owner) AndAlso owner IsNot Nothing Then result.Add(owner)
            Next
        End SyncLock
        Return result
    End Function

    Private Sub CompactNoLock()
        For i As Integer = _owners.Count - 1 To 0 Step -1
            Dim owner As IRenderCacheOwner = Nothing
            If Not _owners(i).TryGetTarget(owner) OrElse owner Is Nothing Then _owners.RemoveAt(i)
        Next
    End Sub
End Module

Friend Module CpuCache
    Private ReadOnly _lock As New Object()
    Private ReadOnly _owners As New List(Of WeakReference(Of IRenderCacheOwner))()
    Private _tick As Long

    Friend Function NextTick() As Long
        SyncLock _lock
            _tick += 1
            Return _tick
        End SyncLock
    End Function

    Friend Sub Register(owner As IRenderCacheOwner)
        If owner Is Nothing Then Return
        SyncLock _lock
            CompactNoLock()
            For Each wr In _owners
                Dim existing As IRenderCacheOwner = Nothing
                If wr.TryGetTarget(existing) AndAlso ReferenceEquals(existing, owner) Then Return
            Next
            _owners.Add(New WeakReference(Of IRenderCacheOwner)(owner))
        End SyncLock
    End Sub

    Friend Sub TrimToBudget(Optional protectedOwner As IRenderCacheOwner = Nothing)
        TrimToBudget(Math.Max(0L, GlobalOptions.CpuCacheBudgetBytes), protectedOwner)
    End Sub

    Private Sub TrimToBudget(budget As Long, protectedOwner As IRenderCacheOwner)
        Dim guard As Integer = 0
        Do
            Dim total As Long = 0
            Dim oldest As IRenderCacheOwner = Nothing
            Dim oldestTick As Long = Long.MaxValue

            SyncLock _lock
                CompactNoLock()
                For Each wr In _owners
                    Dim owner As IRenderCacheOwner = Nothing
                    If Not wr.TryGetTarget(owner) OrElse owner Is Nothing Then Continue For
                    Dim bytes As Long = Math.Max(0L, owner.CacheBytes)
                    total += bytes
                    If bytes <= 0 OrElse ReferenceEquals(owner, protectedOwner) Then Continue For
                    Dim tick As Long = owner.OldestUseTick
                    If tick < oldestTick Then
                        oldestTick = tick
                        oldest = owner
                    End If
                Next
            End SyncLock

            If total <= budget OrElse oldest Is Nothing Then Exit Do
            If Not oldest.TrimOldest() Then Exit Do
            guard += 1
        Loop While guard < 4096
    End Sub

    Friend Sub ReleaseAll()
        Dim owners As List(Of IRenderCacheOwner) = SnapshotOwners()
        For Each owner In owners
            Try : owner.ReleaseAll() : Catch : End Try
        Next
    End Sub

    Private Function SnapshotOwners() As List(Of IRenderCacheOwner)
        Dim result As New List(Of IRenderCacheOwner)()
        SyncLock _lock
            CompactNoLock()
            For Each wr In _owners
                Dim owner As IRenderCacheOwner = Nothing
                If wr.TryGetTarget(owner) AndAlso owner IsNot Nothing Then result.Add(owner)
            Next
        End SyncLock
        Return result
    End Function

    Private Sub CompactNoLock()
        For i As Integer = _owners.Count - 1 To 0 Step -1
            Dim owner As IRenderCacheOwner = Nothing
            If Not _owners(i).TryGetTarget(owner) OrElse owner Is Nothing Then _owners.RemoveAt(i)
        Next
    End Sub
End Module
