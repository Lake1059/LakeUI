''' <summary>
''' 合并同一 UI 拍内的刷新请求，并按控件树深度从外到内派发。
''' </summary>
''' <remarks>
''' 该模块只负责统一 invalidate 的时机和顺序，不同步调用 Paint。
''' 这样 resize / layout / 背景穿透失效可以被合并到下一次消息循环，
''' 避免每个控件在同一轮尺寸变化中各自立即发起大量重复刷新。
'''
''' 调用原则：
''' • 普通控件内部状态变化且只影响自身时，优先调用 <see cref="Request(Control, Rectangle, Boolean, Boolean)"/>
'''   传入精确脏区，避免把兄弟控件和子控件一起唤醒。
''' • 容器尺寸变化、字体变化、主题变化这类确实会影响子树布局/绘制的场景，才使用
'''   <see cref="RequestFull(Control, Boolean, Boolean)"/> 且传 <c>invalidateChildren:=True</c>。
''' • 标题栏按钮 hover、标题文字、光标闪烁等局部 UI 反馈不应通过本调度器扩散到客户区；
'''   这些场景保持控件自己的 <c>Invalidate(rect, False)</c> 更可控。
''' • immediate 标记会在 flush 后调用 <c>Update()</c>，只适合字体切换、同步截图等必须
'''   立刻得到新画面的路径；动画和 resize 热路径不要滥用。
'''
''' 坑点：
''' • 本模块不是布局系统，不会调用 PerformLayout；它只是延迟和排序 Invalidate。
''' • WinForms 的 InvalidateChildren 会把刷新扩散到子控件。为了保持外到内顺序，本模块把子树
'''   展开成独立请求并按树深排序，而不是直接把 True 传给父控件 Invalidate。
''' • 如果在 flush 过程中又排入请求，会进入下一轮 flush，避免递归重入 Paint。
''' </remarks>
Public Interface IOuterToInnerRefreshFilter
    Function ShouldSuppressOuterToInnerRefresh(target As Control,
                                               rect As Rectangle,
                                               hasFull As Boolean,
                                               invalidateChildren As Boolean,
                                               fromChildrenExpansion As Boolean) As Boolean
End Interface

Public Module OuterToInnerRefreshScheduler

    Private Structure PendingEntry
        Public HasFull As Boolean
        Public Rect As Rectangle
        Public InvalidateChildren As Boolean
        Public FromChildrenExpansion As Boolean
        Public Immediate As Boolean
        Public Sequence As Long
    End Structure

    Private ReadOnly _lock As New Object()
    Private ReadOnly _pending As New Dictionary(Of Control, PendingEntry)()
    Private _flushScheduled As Boolean
    Private _isFlushing As Boolean
    Private _sequence As Long

    Public Sub Request(control As Control, Optional invalidateChildren As Boolean = False, Optional immediate As Boolean = False)
        Queue(control, Nothing, invalidateChildren, immediate)
    End Sub

    Public Sub Request(control As Control, rect As Rectangle, Optional invalidateChildren As Boolean = False, Optional immediate As Boolean = False)
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Queue(control, rect, invalidateChildren, immediate)
    End Sub

    Public Sub RequestFull(control As Control, Optional invalidateChildren As Boolean = False, Optional immediate As Boolean = False)
        Queue(control, Nothing, invalidateChildren, immediate)
    End Sub

    Private Sub Queue(control As Control, rect As Rectangle?, invalidateChildren As Boolean, immediate As Boolean)
        If control Is Nothing OrElse control.IsDisposed Then Return
        If ShouldSuppressRefresh(control, If(rect, Rectangle.Empty), Not rect.HasValue, invalidateChildren, False) Then Return
        If Not GlobalOptions.OuterToInnerRefreshSchedulerEnabled Then
            DirectInvalidate(control, rect, invalidateChildren, immediate)
            Return
        End If

        Dim shouldSchedule As Boolean = False
        SyncLock _lock
            Dim entry As PendingEntry = Nothing
            If _pending.TryGetValue(control, entry) Then
                If rect.HasValue Then
                    If Not entry.HasFull Then
                        entry.Rect = If(entry.Rect.Width <= 0 OrElse entry.Rect.Height <= 0,
                                        rect.Value,
                                        Rectangle.Union(entry.Rect, rect.Value))
                    End If
                Else
                    entry.HasFull = True
                    entry.Rect = Rectangle.Empty
                End If
                entry.InvalidateChildren = entry.InvalidateChildren OrElse invalidateChildren
                entry.FromChildrenExpansion = entry.FromChildrenExpansion AndAlso Not invalidateChildren
                entry.Immediate = entry.Immediate OrElse immediate
                _pending(control) = entry
            Else
                _sequence += 1
                entry = New PendingEntry With {
                    .HasFull = Not rect.HasValue,
                    .Rect = If(rect, Rectangle.Empty),
                    .InvalidateChildren = invalidateChildren,
                    .FromChildrenExpansion = False,
                    .Immediate = immediate,
                    .Sequence = _sequence
                }
                _pending(control) = entry
            End If

            If Not _flushScheduled AndAlso Not _isFlushing Then
                _flushScheduled = True
                shouldSchedule = True
            End If
        End SyncLock

        If immediate AndAlso CanFlushImmediately(control) Then
            FlushNow(control)
        ElseIf shouldSchedule Then
            ScheduleFlush(control)
        End If
    End Sub

    Private Sub DirectInvalidate(control As Control, rect As Rectangle?, invalidateChildren As Boolean, immediate As Boolean)
        Try
            If control Is Nothing OrElse control.IsDisposed OrElse Not control.IsHandleCreated Then Return
            If rect.HasValue Then
                Dim clipped = Rectangle.Intersect(control.ClientRectangle, rect.Value)
                If clipped.Width <= 0 OrElse clipped.Height <= 0 Then Return
                control.Invalidate(clipped, invalidateChildren)
            Else
                control.Invalidate(control.ClientRectangle, invalidateChildren)
            End If
            If immediate AndAlso CanUpdateImmediately(control) Then control.Update()
        Catch
        End Try
    End Sub

    Private Sub ScheduleFlush(preferredControl As Control)
        Dim syncContext = System.Threading.SynchronizationContext.Current
        If syncContext IsNot Nothing Then
            Try
                syncContext.Post(Sub(_state) FlushPending(), Nothing)
                Return
            Catch
            End Try
        End If

        Dim invoker = FindInvoker(preferredControl)
        If invoker IsNot Nothing Then
            Try
                invoker.BeginInvoke(CType(Sub() FlushPending(), MethodInvoker))
                Return
            Catch
            End Try
        End If

        FlushPending()
    End Sub

    Private Sub FlushNow(preferredControl As Control)
        Dim invoker = FindInvoker(preferredControl)
        If invoker IsNot Nothing Then
            Try
                If invoker.InvokeRequired Then
                    invoker.Invoke(CType(Sub() FlushPending(), MethodInvoker))
                Else
                    FlushPending()
                End If
                Return
            Catch
            End Try
        End If

        FlushPending()
    End Sub

    Private Function FindInvoker(preferredControl As Control) As Control
        If preferredControl IsNot Nothing AndAlso Not preferredControl.IsDisposed AndAlso preferredControl.IsHandleCreated Then
            Return preferredControl
        End If

        SyncLock _lock
            For Each ctrl In _pending.Keys
                If ctrl IsNot Nothing AndAlso Not ctrl.IsDisposed AndAlso ctrl.IsHandleCreated Then Return ctrl
            Next
        End SyncLock
        Return Nothing
    End Function

    Private Sub FlushPending()
        SyncLock _lock
            If _isFlushing Then Return
            If _pending.Count = 0 Then
                _flushScheduled = False
                Return
            End If
            _isFlushing = True
            _flushScheduled = False
        End SyncLock

        Try
            Do
                Dim pending As List(Of KeyValuePair(Of Control, PendingEntry)) = Nothing
                SyncLock _lock
                    If _pending.Count = 0 Then Exit Do
                    pending = New List(Of KeyValuePair(Of Control, PendingEntry))(_pending)
                    _pending.Clear()
                End SyncLock

                pending.Sort(
                    Function(a, b)
                        Dim da = GetTreeDepth(a.Key)
                        Dim db = GetTreeDepth(b.Key)
                        If da <> db Then Return da.CompareTo(db)
                        Return a.Value.Sequence.CompareTo(b.Value.Sequence)
                    End Function)

                For Each item In pending
                    Dim ctrl = item.Key
                    If ctrl Is Nothing OrElse ctrl.IsDisposed OrElse Not ctrl.IsHandleCreated Then Continue For

                    Dim entry = item.Value
                    Dim rect As Rectangle
                    If entry.HasFull Then
                        rect = ctrl.ClientRectangle
                    Else
                        rect = Rectangle.Intersect(ctrl.ClientRectangle, entry.Rect)
                    End If
                    If rect.Width <= 0 OrElse rect.Height <= 0 Then Continue For
                    If ShouldSuppressRefresh(ctrl, rect, entry.HasFull, entry.InvalidateChildren, entry.FromChildrenExpansion) Then Continue For

                    Try
                        ctrl.Invalidate(rect, False)
                        If entry.InvalidateChildren AndAlso Not entry.FromChildrenExpansion Then
                            QueueVisibleChildren(ctrl, entry.Immediate)
                        End If
                        If entry.Immediate AndAlso CanUpdateImmediately(ctrl) Then ctrl.Update()
                    Catch
                    End Try
                Next
            Loop
        Finally
            Dim needsAnotherFlush As Boolean = False
            SyncLock _lock
                _isFlushing = False
                If _pending.Count > 0 AndAlso Not _flushScheduled Then
                    _flushScheduled = True
                    needsAnotherFlush = True
                End If
            End SyncLock
            If needsAnotherFlush Then ScheduleFlush(Nothing)
        End Try
    End Sub

    Private Sub QueueVisibleChildren(parent As Control, immediate As Boolean)
        If parent Is Nothing OrElse parent.IsDisposed Then Return
        Try
            For Each child As Control In parent.Controls
                QueueChildForExpansion(child, immediate)
            Next
        Catch
        End Try
    End Sub

    Private Sub QueueChildForExpansion(child As Control, immediate As Boolean)
        If child Is Nothing OrElse child.IsDisposed OrElse Not child.Visible Then Return
        Dim shouldQueue As Boolean = child.Width > 0 AndAlso child.Height > 0
        If shouldQueue Then
            If ShouldSuppressRefresh(child, child.ClientRectangle, True, False, True) Then Return
            SyncLock _lock
                Dim entry As PendingEntry = Nothing
                If _pending.TryGetValue(child, entry) Then
                    entry.HasFull = True
                    entry.Rect = Rectangle.Empty
                    entry.Immediate = entry.Immediate OrElse immediate
                    _pending(child) = entry
                Else
                    _sequence += 1
                    _pending(child) = New PendingEntry With {
                        .HasFull = True,
                        .Rect = Rectangle.Empty,
                        .InvalidateChildren = False,
                        .FromChildrenExpansion = True,
                        .Immediate = immediate,
                        .Sequence = _sequence
                    }
                End If
            End SyncLock
        End If

        Try
            For Each grandChild As Control In child.Controls
                QueueChildForExpansion(grandChild, immediate)
            Next
        Catch
        End Try
    End Sub

    Private Function GetTreeDepth(ctrl As Control) As Integer
        Dim depth As Integer = 0
        Dim current As Control = ctrl
        While current IsNot Nothing
            depth += 1
            current = current.Parent
        End While
        Return depth
    End Function

    Private Function ShouldSuppressRefresh(control As Control,
                                           rect As Rectangle,
                                           hasFull As Boolean,
                                           invalidateChildren As Boolean,
                                           fromChildrenExpansion As Boolean) As Boolean
        Dim current As Control = control
        While current IsNot Nothing
            Dim filter = TryCast(current, IOuterToInnerRefreshFilter)
            If filter IsNot Nothing Then
                Try
                    If filter.ShouldSuppressOuterToInnerRefresh(control, rect, hasFull, invalidateChildren, fromChildrenExpansion) Then Return True
                Catch
                End Try
            End If
            current = current.Parent
        End While
        Return False
    End Function

    Private Function CanUpdateImmediately(control As Control) As Boolean
        If control Is Nothing OrElse control.IsDisposed Then Return False
        If D3D_PaintBridge.IsBackgroundSamplingPaint Then Return False
        If D3D_PaintBridge.IsPainting(control) Then Return False
        Return True
    End Function

    Private Function CanFlushImmediately(control As Control) As Boolean
        SyncLock _lock
            If _isFlushing Then Return False
        End SyncLock
        Return CanUpdateImmediately(control)
    End Function

End Module
