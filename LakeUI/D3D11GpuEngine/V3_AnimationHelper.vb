Imports System.Threading

''' <summary>
''' V3 动画核心：线程级统一调度器 + 控件级脏区失效。
'''
''' === V3 迁移契约 ===
''' 1. 所有控件的新动画必须接入 <see cref="V3_AnimationHelper"/>，不再为每个控件单独创建
'''    <see cref="PrecisionTimer"/> 或直接订阅应用级空闲消息。同一 UI 线程内的所有
'''    V3 helper 共享一个高精度调度器，由调度器统一驱动、统一批量失效。
''' 2. <see cref="FPS"/> 语义：大于 0 表示控件自身帧率上限；等于 0 表示不做帧率上限，
'''    但仍由共享 <see cref="PrecisionTimer"/> 驱动，不退回应用级空闲消息。
''' 3. 所有有滚动行为的控件必须提供 <c>AllowSmoothScroll</c> 属性，默认 False。滚动默认走普通
'''    即时模式；当 AllowSmoothScroll=True 时才启用平滑滚动。平滑滚动帧率与控件自身
'''    <c>AnimationFPS</c> 属性同步；没有该属性的滚动控件迁移 V3 时需要新增。
''' 4. 控件必须通过 <see cref="DirtyRegionProvider"/> 或 <see cref="SetDirtyRectProvider"/> 声明
'''    动画帧需要刷新的区域。Provider 可以请求整控件、一个或多个矩形、或跳过本帧失效。
'''    调度器会按控件聚合这些请求，避免同一 tick 内重复 Invalidate。
''' 5. V3 不以降低 SSAA 作为默认动画优化手段。SSAA 是高配置用户主动开启的质量选项，动画帧
'''    应尊重控件和全局 SSAA 设置。后续 SSAA 性能优化应优先放在 D2D/SSAA 离屏缓存层，例如
'''    更长生命周期的 per-control/per-size render target、脏区回采、或可复用中间纹理，而不是
'''    在动画核心里静默降级画质。
''' 6. 调度器暴露 <see cref="GetThreadSchedulerSnapshot"/> 供性能分析：可观察当前 driver、活跃
'''    helper 数、tick 数、请求失效次数与实际 flush 次数。
''' </summary>
Friend Class V3_AnimationHelper
    Implements IDisposable

    Public Enum EasingModeEnum
        ''' <summary>缓出（默认）：从快到慢。</summary>
        EaseOut
        ''' <summary>缓入缓出：从慢到快再到慢。</summary>
        EaseInOut
    End Enum

    Public Delegate Sub DirtyRegionProvider(helper As V3_AnimationHelper, owner As Control, sink As InvalidateRegionSink)

    Public NotInheritable Class InvalidateRegionSink
        Private ReadOnly _rectangles As New List(Of Rectangle)()
        Private _full As Boolean
        Private _suppressed As Boolean

        Friend ReadOnly Property Rectangles As IReadOnlyList(Of Rectangle)
            Get
                Return _rectangles
            End Get
        End Property

        Friend ReadOnly Property IsFullInvalidation As Boolean
            Get
                Return _full
            End Get
        End Property

        Friend ReadOnly Property IsSuppressed As Boolean
            Get
                Return _suppressed
            End Get
        End Property

        ''' <summary>请求失效整个控件。</summary>
        Public Sub InvalidateAll()
            _full = True
            _suppressed = False
            _rectangles.Clear()
        End Sub

        ''' <summary>添加一个控件客户区坐标中的脏矩形。</summary>
        Public Sub Add(rect As Rectangle)
            If _full OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
            _suppressed = False
            _rectangles.Add(rect)
        End Sub

        ''' <summary>本 tick 不触发 Invalidate，用于平滑滚动小位移聚合等场景。</summary>
        Public Sub SuppressInvalidate()
            _suppressed = True
            _full = False
            _rectangles.Clear()
        End Sub
    End Class

    Public NotInheritable Class SchedulerSnapshot
        Friend Sub New(driverMode As String, activeHelpers As Integer, animationCount As Integer,
                       frameLoopCount As Integer, tickCount As Long, requestedInvalidates As Long,
                       flushedInvalidates As Long, droppedInvalidates As Long)
            Me.DriverMode = driverMode
            Me.ActiveHelpers = activeHelpers
            Me.ActiveAnimations = animationCount
            Me.ActiveFrameLoops = frameLoopCount
            Me.TickCount = tickCount
            Me.RequestedInvalidates = requestedInvalidates
            Me.FlushedInvalidates = flushedInvalidates
            Me.DroppedInvalidates = droppedInvalidates
        End Sub

        Public ReadOnly Property DriverMode As String
        Public ReadOnly Property ActiveHelpers As Integer
        Public ReadOnly Property ActiveAnimations As Integer
        Public ReadOnly Property ActiveFrameLoops As Integer
        Public ReadOnly Property TickCount As Long
        Public ReadOnly Property RequestedInvalidates As Long
        Public ReadOnly Property FlushedInvalidates As Long
        Public ReadOnly Property DroppedInvalidates As Long
    End Class

    Private Const ProgressEpsilon As Single = 0.0001F

    Private Shared Function FrameIntervalTicks(fps As Integer) As Long
        fps = Math.Max(1, fps)
        Return Math.Max(1, CLng(Math.Ceiling(Stopwatch.Frequency / CDbl(fps))))
    End Function

    Private ReadOnly _owner As Control
    Private _progress As Single = 0.0F
    Private _startProgress As Single = 0.0F
    Private _target As Single = 0.0F
    Private _animationRunning As Boolean = False
    Private _frameLoopRunning As Boolean = False
    Private _frameLoopHandler As EventHandler
    Private _animationStartTicks As Long = 0
    Private _lastAnimationDispatchTicks As Long = 0
    Private _lastFrameLoopDispatchTicks As Long = 0
    Private _currentSegmentDurationMs As Double = 0
    Private _disposed As Boolean = False
    Private _easingMode As EasingModeEnum = EasingModeEnum.EaseOut
    Private _duration As Integer = 300
    Private _fps As Integer = 60

    Public Sub New(owner As Control)
        ArgumentNullException.ThrowIfNull(owner)
        _owner = owner
    End Sub

    ''' <summary>动画时长（毫秒），0 = 无动画。</summary>
    Public Property Duration As Integer
        Get
            Return _duration
        End Get
        Set(value As Integer)
            _duration = Math.Max(0, value)
        End Set
    End Property

    ''' <summary>动画帧率上限；0 = 不做上限，仍由共享 PrecisionTimer 驱动。</summary>
    Public Property FPS As Integer
        Get
            Return _fps
        End Get
        Set(value As Integer)
            _fps = Math.Max(0, value)
            If IsActive Then CurrentScheduler(_owner).Reconfigure()
        End Set
    End Property

    Public Property EasingMode As EasingModeEnum
        Get
            Return _easingMode
        End Get
        Set(value As EasingModeEnum)
            _easingMode = value
        End Set
    End Property

    ''' <summary>
    ''' 动画帧脏区提供器。未设置时默认整控件失效；设置后由 provider 自行决定整控件、
    ''' 局部矩形、多矩形或跳过本帧。
    ''' </summary>
    Public Property DirtyProvider As DirtyRegionProvider

    ''' <summary>失效时是否同时失效子控件。默认 False。</summary>
    Public Property InvalidateChildren As Boolean = False

    Public ReadOnly Property Progress As Single
        Get
            Return _progress
        End Get
    End Property

    Friend ReadOnly Property Owner As Control
        Get
            Return _owner
        End Get
    End Property

    Friend ReadOnly Property IsAnimationRunning As Boolean
        Get
            Return _animationRunning
        End Get
    End Property

    Friend ReadOnly Property IsFrameLoopRunning As Boolean
        Get
            Return _frameLoopRunning
        End Get
    End Property

    Friend ReadOnly Property IsActive As Boolean
        Get
            Return _animationRunning OrElse _frameLoopRunning
        End Get
    End Property

    Friend ReadOnly Property EffectiveFPS As Integer
        Get
            Return _fps
        End Get
    End Property

    Public Sub SetDirtyRectProvider(provider As Func(Of Rectangle))
        If provider Is Nothing Then
            DirtyProvider = Nothing
        Else
            DirtyProvider =
                Sub(helper, owner, sink)
                    Dim rect = provider()
                    If rect.Width > 0 AndAlso rect.Height > 0 Then
                        sink.Add(rect)
                    Else
                        sink.SuppressInvalidate()
                    End If
                End Sub
        End If
    End Sub

    Public Sub SetImmediate(value As Single)
        StopAnimation()
        _progress = value
        RequestInvalidate()
    End Sub

    Public Sub AnimateTo(target As Single)
        If _disposed Then Return
        _target = target
        If Not _owner.IsHandleCreated OrElse _duration <= 0 Then
            StopAnimation()
            _progress = _target
            RequestInvalidate()
            Return
        End If

        _startProgress = _progress
        Dim distance As Single = Math.Abs(_target - _startProgress)
        If distance < 0.001F Then
            StopAnimation()
            _progress = _target
            RequestInvalidate()
            Return
        End If

        _currentSegmentDurationMs = Math.Max(1.0, _duration * CDbl(distance))
        _animationStartTicks = Stopwatch.GetTimestamp()
        _lastAnimationDispatchTicks = 0
        If Not _animationRunning Then
            _animationRunning = True
            CurrentScheduler(_owner).Register(Me)
        Else
            CurrentScheduler(_owner).Reconfigure()
        End If
    End Sub

    Public Sub StopAnimation()
        If Not _animationRunning Then Return
        _animationRunning = False
        _lastAnimationDispatchTicks = 0
        If Not IsActive Then CurrentScheduler(_owner).Unregister(Me) Else CurrentScheduler(_owner).Reconfigure()
    End Sub

    Public Sub StartFrameLoop(handler As EventHandler)
        ArgumentNullException.ThrowIfNull(handler)
        If _disposed Then Return
        If Not _owner.IsHandleCreated Then Return
        _frameLoopHandler = handler
        _lastFrameLoopDispatchTicks = 0
        If Not _frameLoopRunning Then
            _frameLoopRunning = True
            CurrentScheduler(_owner).Register(Me)
        Else
            CurrentScheduler(_owner).Reconfigure()
        End If
    End Sub

    Public Sub StopFrameLoop()
        If Not _frameLoopRunning Then Return
        _frameLoopRunning = False
        _frameLoopHandler = Nothing
        _lastFrameLoopDispatchTicks = 0
        If Not IsActive Then CurrentScheduler(_owner).Unregister(Me) Else CurrentScheduler(_owner).Reconfigure()
    End Sub

    Friend Function Tick(nowTicks As Long) As Boolean
        If _disposed OrElse _owner.IsDisposed Then
            Dispose()
            Return False
        End If

        Dim updated As Boolean = False

        If _animationRunning AndAlso ShouldDispatch(nowTicks, _lastAnimationDispatchTicks) Then
            _lastAnimationDispatchTicks = nowTicks
            updated = UpdateAnimation(nowTicks)
        End If

        If _frameLoopRunning AndAlso ShouldDispatch(nowTicks, _lastFrameLoopDispatchTicks) Then
            _lastFrameLoopDispatchTicks = nowTicks
            Try
                _frameLoopHandler?.Invoke(Me, EventArgs.Empty)
                updated = True
            Catch
                StopFrameLoop()
            End Try
        End If

        If updated Then RequestInvalidate()
        Return updated
    End Function

    Private Function ShouldDispatch(nowTicks As Long, lastTicks As Long) As Boolean
        If _fps <= 0 Then Return True
        If lastTicks = 0 Then Return True
        Dim intervalTicks As Long = FrameIntervalTicks(_fps)
        Return nowTicks - lastTicks >= intervalTicks
    End Function

    Private Function UpdateAnimation(nowTicks As Long) As Boolean
        Dim elapsedMs As Double = (nowTicks - _animationStartTicks) * 1000.0 / Stopwatch.Frequency
        Dim t As Single = CSng(Math.Min(elapsedMs / _currentSegmentDurationMs, 1.0))
        Dim eased As Single
        Select Case _easingMode
            Case EasingModeEnum.EaseInOut
                If t < 0.5F Then
                    eased = 4.0F * t * t * t
                Else
                    eased = 1.0F - CSng(Math.Pow(-2.0 * t + 2.0, 3) / 2.0)
                End If
            Case Else
                eased = 1.0F - CSng(Math.Pow(1.0 - t, 3))
        End Select

        Dim oldProgress As Single = _progress
        _progress = _startProgress + (_target - _startProgress) * eased
        If t >= 1.0F Then
            _progress = _target
            StopAnimation()
        End If

        Return t >= 1.0F OrElse Math.Abs(_progress - oldProgress) >= ProgressEpsilon
    End Function

    Private Sub RequestInvalidate()
        CurrentScheduler(_owner).RequestInvalidate(Me)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        _animationRunning = False
        _frameLoopRunning = False
        _frameLoopHandler = Nothing
        CurrentScheduler(_owner).Unregister(Me)
    End Sub

    Public Shared Function GetThreadSchedulerSnapshot() As SchedulerSnapshot
        Return CurrentScheduler(Nothing).CreateSnapshot()
    End Function

    <ThreadStatic>
    Private Shared _threadScheduler As FrameScheduler

    Private Shared Function CurrentScheduler(owner As Control) As FrameScheduler
        If _threadScheduler Is Nothing Then _threadScheduler = New FrameScheduler()
        If owner IsNot Nothing Then _threadScheduler.TouchSyncOwner(owner)
        Return _threadScheduler
    End Function

    Private NotInheritable Class FrameScheduler
        Private Enum DriverKind
            None
            Timer
        End Enum

        Private NotInheritable Class InvalidationBucket
            Public Full As Boolean
            Public Children As Boolean
            Public ReadOnly Rectangles As New List(Of Rectangle)()
        End Class

        Private Const MaxDirtyRectsPerControl As Integer = 8
        Private Const FullInvalidateAreaRatio As Double = 0.65

        Private ReadOnly _helpers As New List(Of V3_AnimationHelper)()
        Private ReadOnly _timer As New PrecisionTimer()
        Private ReadOnly _invalidations As New Dictionary(Of Control, InvalidationBucket)()
        Private _syncOwner As Control
        Private _driver As DriverKind = DriverKind.None
        Private _inTick As Boolean
        Private _reconfigurePending As Boolean
        Private _tickCount As Long
        Private _requestedInvalidates As Long
        Private _flushedInvalidates As Long
        Private _droppedInvalidates As Long

        Public Sub New()
            _timer.DispatchMode = PrecisionTimer.DispatchModeEnum.NonBlocking
            _timer.OverrunPolicy = PrecisionTimer.OverrunPolicyEnum.Drop
            _timer.WorkerThreadCount = 1
            AddHandler _timer.Tick, AddressOf TimerTick
        End Sub

        Public Sub TouchSyncOwner(owner As Control)
            If owner Is Nothing OrElse owner.IsDisposed Then Return
            If Not owner.IsHandleCreated Then Return
            If _syncOwner Is Nothing OrElse _syncOwner.IsDisposed OrElse Not _syncOwner.IsHandleCreated Then
                _syncOwner = owner
                _timer.SynchronizingObject = owner
            End If
        End Sub

        Public Sub Register(helper As V3_AnimationHelper)
            If helper Is Nothing OrElse helper.Owner Is Nothing Then Return
            TouchSyncOwner(helper.Owner)
            If Not _helpers.Contains(helper) Then _helpers.Add(helper)
            Reconfigure()
        End Sub

        Public Sub Unregister(helper As V3_AnimationHelper)
            If helper Is Nothing Then Return
            _helpers.Remove(helper)
            Reconfigure()
        End Sub

        Public Sub Reconfigure()
            If _inTick Then
                _reconfigurePending = True
                Return
            End If

            ApplyConfiguration()
        End Sub

        Private Sub ApplyConfiguration()
            _reconfigurePending = False
            CleanupDeadHelpers()
            If _helpers.Count = 0 Then
                SetDriver(DriverKind.None, 0)
                Return
            End If

            If Not EnsureSyncOwnerFromActiveHelpers() Then
                SetDriver(DriverKind.None, 0)
                Return
            End If

            SetDriver(DriverKind.Timer, 1)
        End Sub

        Private Sub SetDriver(kind As DriverKind, interval As Integer)
            If _driver = kind Then
                If kind = DriverKind.Timer AndAlso interval > 0 AndAlso _timer.Interval <> interval Then
                    _timer.Interval = interval
                End If
                If kind = DriverKind.Timer Then _timer.SynchronizingObject = _syncOwner
                If kind = DriverKind.Timer AndAlso Not _timer.IsRunning Then _timer.Start()
                Return
            End If

            If _driver = DriverKind.Timer Then _timer.Stop()

            _driver = kind

            Select Case kind
                Case DriverKind.Timer
                    _timer.Interval = Math.Max(1, interval)
                    _timer.SynchronizingObject = _syncOwner
                    _timer.Start()
            End Select
        End Sub

        Private Function HasUsableSyncOwner() As Boolean
            Return _syncOwner IsNot Nothing AndAlso
                   Not _syncOwner.IsDisposed AndAlso
                   _syncOwner.IsHandleCreated
        End Function

        Private Function EnsureSyncOwnerFromActiveHelpers() As Boolean
            If HasUsableSyncOwner() Then Return True

            _syncOwner = Nothing
            For Each helper In _helpers
                If helper Is Nothing OrElse Not helper.IsActive Then Continue For
                Dim owner = helper.Owner
                If owner Is Nothing OrElse owner.IsDisposed OrElse Not owner.IsHandleCreated Then Continue For
                _syncOwner = owner
                _timer.SynchronizingObject = owner
                Return True
            Next

            _timer.SynchronizingObject = Nothing
            Return False
        End Function

        Private Sub TimerTick(sender As Object, e As EventArgs)
            RunTick()
        End Sub

        Private Sub RunTick()
            If _inTick Then Return
            _inTick = True
            Try
                _tickCount += 1
                Dim nowTicks As Long = Stopwatch.GetTimestamp()
                Dim snapshot = _helpers.ToArray()
                For Each helper In snapshot
                    If helper IsNot Nothing AndAlso helper.IsActive Then helper.Tick(nowTicks)
                Next
                FlushInvalidations()
            Finally
                _inTick = False
                If _reconfigurePending Then ApplyConfiguration()
            End Try
        End Sub

        Public Sub RequestInvalidate(helper As V3_AnimationHelper)
            If helper Is Nothing Then Return
            Dim owner = helper.Owner
            If owner Is Nothing OrElse owner.IsDisposed OrElse Not owner.IsHandleCreated Then
                _droppedInvalidates += 1
                Return
            End If

            Dim sink As New InvalidateRegionSink()
            If helper.DirtyProvider Is Nothing Then
                sink.InvalidateAll()
            Else
                Try
                    helper.DirtyProvider.Invoke(helper, owner, sink)
                Catch
                    sink.InvalidateAll()
                End Try
            End If

            If sink.IsSuppressed Then Return

            Dim hasRects As Boolean = sink.Rectangles.Count > 0
            If Not sink.IsFullInvalidation AndAlso Not hasRects Then Return

            _requestedInvalidates += 1

            Dim bucket As InvalidationBucket = Nothing
            If Not _invalidations.TryGetValue(owner, bucket) Then
                bucket = New InvalidationBucket()
                _invalidations(owner) = bucket
            End If

            bucket.Children = bucket.Children OrElse helper.InvalidateChildren
            If sink.IsFullInvalidation Then
                bucket.Full = True
                bucket.Rectangles.Clear()
            ElseIf Not bucket.Full Then
                For Each rect In sink.Rectangles
                    AddInvalidationRect(owner, bucket, rect)
                Next
            End If

            If Not _inTick Then FlushInvalidations()
        End Sub

        Private Sub AddInvalidationRect(owner As Control, bucket As InvalidationBucket, rect As Rectangle)
            If owner Is Nothing OrElse bucket Is Nothing OrElse bucket.Full Then Return
            rect = Rectangle.Intersect(owner.ClientRectangle, rect)
            If rect.Width <= 0 OrElse rect.Height <= 0 Then Return

            Dim mergedRect As Rectangle = rect
            Dim mergedAny As Boolean
            Do
                mergedAny = False
                For i As Integer = bucket.Rectangles.Count - 1 To 0 Step -1
                    Dim existing = bucket.Rectangles(i)
                    If existing.IntersectsWith(mergedRect) OrElse existing.Contains(mergedRect) OrElse mergedRect.Contains(existing) Then
                        mergedRect = Rectangle.Union(existing, mergedRect)
                        bucket.Rectangles.RemoveAt(i)
                        mergedAny = True
                    End If
                Next
            Loop While mergedAny

            bucket.Rectangles.Add(mergedRect)
            If ShouldPromoteToFullInvalidation(owner, bucket) Then
                bucket.Full = True
                bucket.Rectangles.Clear()
            End If
        End Sub

        Private Function ShouldPromoteToFullInvalidation(owner As Control, bucket As InvalidationBucket) As Boolean
            If bucket.Rectangles.Count <= 0 Then Return False
            If bucket.Rectangles.Count > MaxDirtyRectsPerControl Then Return True

            Dim clientArea As Long = CLng(Math.Max(1, owner.ClientSize.Width)) * CLng(Math.Max(1, owner.ClientSize.Height))
            Dim unionRect As Rectangle = Rectangle.Empty
            Dim totalArea As Long = 0
            For Each rect In bucket.Rectangles
                unionRect = If(unionRect.IsEmpty, rect, Rectangle.Union(unionRect, rect))
                totalArea += CLng(rect.Width) * CLng(rect.Height)
            Next
            Dim unionArea As Long = CLng(Math.Max(0, unionRect.Width)) * CLng(Math.Max(0, unionRect.Height))
            Dim threshold As Long = CLng(clientArea * FullInvalidateAreaRatio)
            Return unionArea >= threshold OrElse totalArea >= threshold
        End Function

        Private Sub FlushInvalidations()
            If _invalidations.Count = 0 Then Return
            Dim pending = _invalidations.ToArray()
            _invalidations.Clear()

            For Each kv In pending
                Dim owner = kv.Key
                Dim bucket = kv.Value
                If owner Is Nothing OrElse owner.IsDisposed OrElse Not owner.IsHandleCreated Then
                    _droppedInvalidates += 1
                    Continue For
                End If
                Try
                    If bucket.Full Then
                        V3_InvalidationRouter.RequestRender(owner, New Rectangle(Point.Empty, owner.ClientSize))
                        _flushedInvalidates += 1
                    Else
                        For Each rect In bucket.Rectangles
                            If rect.Width <= 0 OrElse rect.Height <= 0 Then Continue For
                            V3_InvalidationRouter.RequestRender(owner, rect)
                            _flushedInvalidates += 1
                        Next
                    End If
                Catch
                    _droppedInvalidates += 1
                End Try
            Next
        End Sub

        Private Sub CleanupDeadHelpers()
            For i As Integer = _helpers.Count - 1 To 0 Step -1
                Dim h = _helpers(i)
                If h Is Nothing OrElse h._disposed OrElse h.Owner Is Nothing OrElse h.Owner.IsDisposed OrElse Not h.IsActive Then
                    _helpers.RemoveAt(i)
                End If
            Next
        End Sub

        Public Function CreateSnapshot() As SchedulerSnapshot
            CleanupDeadHelpers()
            Dim activeAnimations As Integer = 0
            Dim activeLoops As Integer = 0
            For Each h In _helpers
                If h.IsAnimationRunning Then activeAnimations += 1
                If h.IsFrameLoopRunning Then activeLoops += 1
            Next
            Return New SchedulerSnapshot(
                _driver.ToString(),
                _helpers.Count,
                activeAnimations,
                activeLoops,
                Interlocked.Read(_tickCount),
                Interlocked.Read(_requestedInvalidates),
                Interlocked.Read(_flushedInvalidates),
                Interlocked.Read(_droppedInvalidates))
        End Function
    End Class
End Class
