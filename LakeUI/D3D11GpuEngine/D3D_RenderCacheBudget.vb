''' <summary>
''' 进程级渲染缓存预算协调器。GPU 与 CPU 缓存分别注册 owner，
''' 由 owner 自己负责释放最旧条目，协调器只做总量统计与全局 LRU 调度。
''' </summary>
Friend Interface D3D_IRenderCacheOwner
    ReadOnly Property CacheBytes As Long
    ReadOnly Property OldestUseTick As Long
    Function TrimOldest() As Boolean
    Sub ReleaseAll()
End Interface

Friend NotInheritable Class D3D_RenderCacheBudgetCoordinator
    Private ReadOnly _lock As New Object()
    Private ReadOnly _trimLock As New Object()
    Private ReadOnly _owners As New List(Of WeakReference(Of D3D_IRenderCacheOwner))()
    Private _trimActive As Boolean

    Friend Sub Register(owner As D3D_IRenderCacheOwner)
        If owner Is Nothing Then Return
        SyncLock _lock
            CompactNoLock()
            For Each wr In _owners
                Dim existing As D3D_IRenderCacheOwner = Nothing
                If wr.TryGetTarget(existing) AndAlso ReferenceEquals(existing, owner) Then Return
            Next
            _owners.Add(New WeakReference(Of D3D_IRenderCacheOwner)(owner))
        End SyncLock
    End Sub

    Friend Sub TrimToBudget(budget As Long,
                            protectedOwner As D3D_IRenderCacheOwner,
                            evictionCallback As Action)
        D3D_RenderDiagnostics.BudgetScan()
        budget = Math.Max(0L, budget)

        SyncLock _trimLock
            ' 淘汰表面可能结束帧使用范围并在同一 UI 线程再次请求清理；
            ' 外层清理尚未结束时禁止递归进入协调器。
            If _trimActive Then Return
            _trimActive = True
            Try
            Dim 失败所有者 As New HashSet(Of D3D_IRenderCacheOwner)(ReferenceEqualityComparer.Instance)
            Dim 守卫次数 As Integer = 0
            Do
                Dim 总字节数 As Long = 0
                Dim 最旧所有者 As D3D_IRenderCacheOwner = Nothing
                Dim 最旧时钟 As Long = Long.MaxValue

                For Each 所有者 In SnapshotOwners()
                    Dim 字节数 As Long
                    Try
                        字节数 = Math.Max(0L, 所有者.CacheBytes)
                    Catch
                        失败所有者.Add(所有者)
                        Continue For
                    End Try
                    总字节数 = SaturatingAdd(总字节数, 字节数)
                    If 字节数 <= 0 OrElse ReferenceEquals(所有者, protectedOwner) OrElse 失败所有者.Contains(所有者) Then Continue For

                    Dim 使用时钟 As Long
                    Try
                        使用时钟 = 所有者.OldestUseTick
                    Catch
                        失败所有者.Add(所有者)
                        Continue For
                    End Try
                    If 使用时钟 < 最旧时钟 Then
                        最旧时钟 = 使用时钟
                        最旧所有者 = 所有者
                    End If
                Next

                If 总字节数 <= budget OrElse 最旧所有者 Is Nothing Then Exit Do

                Dim 已淘汰 As Boolean
                Try
                    已淘汰 = 最旧所有者.TrimOldest()
                Catch
                    已淘汰 = False
                End Try
                If Not 已淘汰 Then
                    ' 正在绘制或后台处理中的 owner 暂时不可回收；本轮跳过它，
                    ' 继续处理其他全局 LRU 候选项。
                    失败所有者.Add(最旧所有者)
                    Continue Do
                End If

                evictionCallback?.Invoke()
                守卫次数 += 1
            Loop While 守卫次数 < 4096
            Finally
                _trimActive = False
            End Try
        End SyncLock
    End Sub

    Friend Sub ReleaseAll()
        SyncLock _trimLock
            Dim owners As List(Of D3D_IRenderCacheOwner) = SnapshotOwners()
            For Each owner In owners
                Try : owner.ReleaseAll() : Catch : End Try
            Next
        End SyncLock
    End Sub

    Private Function SnapshotOwners() As List(Of D3D_IRenderCacheOwner)
        Dim result As New List(Of D3D_IRenderCacheOwner)()
        SyncLock _lock
            CompactNoLock()
            For Each wr In _owners
                Dim owner As D3D_IRenderCacheOwner = Nothing
                If wr.TryGetTarget(owner) AndAlso owner IsNot Nothing Then result.Add(owner)
            Next
        End SyncLock
        Return result
    End Function

    Friend Function TotalCacheBytes() As Long
        Dim total As Long
        For Each owner In SnapshotOwners()
            Try
                total = SaturatingAdd(total, Math.Max(0L, owner.CacheBytes))
            Catch
            End Try
        Next
        Return total
    End Function

    Private Shared Function SaturatingAdd(current As Long, value As Long) As Long
        If value <= 0 Then Return current
        If current >= Long.MaxValue - value Then Return Long.MaxValue
        Return current + value
    End Function

    Private Sub CompactNoLock()
        For i As Integer = _owners.Count - 1 To 0 Step -1
            Dim owner As D3D_IRenderCacheOwner = Nothing
            If Not _owners(i).TryGetTarget(owner) OrElse owner Is Nothing Then _owners.RemoveAt(i)
        Next
    End Sub
End Class

Friend Module D3D_GpuCache
    Private ReadOnly _coordinator As New D3D_RenderCacheBudgetCoordinator()
    Private _tick As Long
    Private _lastBudgetTrimMilliseconds As Long

    Private Const BudgetTrimIntervalMilliseconds As Long = 100L

    Friend Function NextTick() As Long
        Return Threading.Interlocked.Increment(_tick)
    End Function

    Friend Sub Register(owner As D3D_IRenderCacheOwner)
        _coordinator.Register(owner)
    End Sub

    Friend Sub TrimToBudget(Optional protectedOwner As D3D_IRenderCacheOwner = Nothing,
                             Optional immediate As Boolean = False)
        If Not immediate AndAlso Not D3D_CacheThrottle.ShouldRun(_lastBudgetTrimMilliseconds, BudgetTrimIntervalMilliseconds) Then Return
        _coordinator.TrimToBudget(GlobalOptions.GpuCacheBudgetBytes,
                                  protectedOwner,
                                  AddressOf D3D_RenderDiagnostics.CacheEviction)
    End Sub

    Friend Function TotalCacheBytes() As Long
        Return _coordinator.TotalCacheBytes()
    End Function

    Friend Sub ReleaseAll()
        _coordinator.ReleaseAll()
    End Sub
End Module

Friend Module D3D_CpuCache
    Private ReadOnly _coordinator As New D3D_RenderCacheBudgetCoordinator()
    Private _tick As Long
    Private _lastBudgetTrimMilliseconds As Long

    Private Const BudgetTrimIntervalMilliseconds As Long = 100L

    Friend Function NextTick() As Long
        Return Threading.Interlocked.Increment(_tick)
    End Function

    Friend Sub Register(owner As D3D_IRenderCacheOwner)
        _coordinator.Register(owner)
    End Sub

    Friend Sub TrimToBudget(Optional protectedOwner As D3D_IRenderCacheOwner = Nothing,
                             Optional immediate As Boolean = False)
        If Not immediate AndAlso Not D3D_CacheThrottle.ShouldRun(_lastBudgetTrimMilliseconds, BudgetTrimIntervalMilliseconds) Then Return
        _coordinator.TrimToBudget(GlobalOptions.CpuCacheBudgetBytes,
                                  protectedOwner,
                                  AddressOf D3D_RenderDiagnostics.CacheEviction)
    End Sub

    Friend Function TotalCacheBytes() As Long
        Return _coordinator.TotalCacheBytes()
    End Function

    Friend Sub ReleaseAll()
        _coordinator.ReleaseAll()
    End Sub
End Module

''' <summary>缓存预算节流器，避免 GPU 与 CPU 模块重复实现时间窗口和并发闸门。</summary>
Friend Module D3D_CacheThrottle
    Friend Function ShouldRun(ByRef lastTick As Long, intervalMilliseconds As Long) As Boolean
        Dim 当前时刻 = Environment.TickCount64
        Dim 上次时刻 = Threading.Interlocked.Read(lastTick)
        If 当前时刻 - 上次时刻 < intervalMilliseconds Then Return False
        Return Threading.Interlocked.CompareExchange(lastTick, 当前时刻, 上次时刻) = 上次时刻
    End Function
End Module
