Imports System.Collections.Concurrent
Imports System.ComponentModel

''' <summary>
''' 由 <see cref="PrecisionTimer"/> 驱动的高精度动画助手，替代原 AnimationHelper 的
''' WinForms.Timer / Application.Idle 实现，用于 ModernButton 等控件的过渡动画。
''' 多个动画助手按 FPS 共享同一进程级广播驱动器，避免每个动画各自启停 PrecisionTimer
''' 带来的线程/句柄抖动与竞态。
''' </summary>
Friend Class HighFreqAnimator
    Implements IDisposable

    Public Enum EasingModeEnum
        ''' <summary>缓出（默认）：从快到慢</summary>
        EaseOut
        ''' <summary>缓入缓出：从慢到快再到慢，有"阻力感"</summary>
        EaseInOut
    End Enum

#Region "进程级共享驱动器"

    ''' <summary>
    ''' 按 FPS 复用的进程级广播驱动器：所有同帧率的动画订阅同一个 PrecisionTimer。
    ''' 订阅者数量降为 0 时停止底层定时器，但保留实例以便复用。
    ''' </summary>
    Private NotInheritable Class 共享驱动器
        Private Shared ReadOnly _驱动表 As New Dictionary(Of Integer, 共享驱动器)()
        Private Shared ReadOnly _表锁 As New Object()

        Private ReadOnly _定时器 As PrecisionTimer
        Private ReadOnly _订阅者 As New ConcurrentDictionary(Of HighFreqAnimator, Byte)()

        Private Sub New(fps As Integer)
            _定时器 = New PrecisionTimer() With {
                .DispatchMode = PrecisionTimer.DispatchModeEnum.NonBlocking,
                .OverrunPolicy = PrecisionTimer.OverrunPolicyEnum.Drop,
                .WorkerThreadCount = 1,
                .AutoReset = True,
                .Interval = Math.Max(1, CInt(1000.0 / fps))
            }
            AddHandler _定时器.Tick, AddressOf 广播
        End Sub

        Private Sub 广播(sender As Object, e As EventArgs)
            For Each kv In _订阅者
                Try
                    kv.Key.OnDriverTick()
                Catch
                End Try
            Next
        End Sub

        Public Shared Function 订阅(animator As HighFreqAnimator, fps As Integer) As 共享驱动器
            SyncLock _表锁
                Dim drv As 共享驱动器 = Nothing
                If Not _驱动表.TryGetValue(fps, drv) Then
                    drv = New 共享驱动器(fps)
                    _驱动表(fps) = drv
                End If
                If drv._订阅者.TryAdd(animator, 0) Then
                    If drv._订阅者.Count = 1 Then
                        Try : drv._定时器.Start() : Catch : End Try
                    End If
                End If
                Return drv
            End SyncLock
        End Function

        Public Shared Sub 退订(animator As HighFreqAnimator, drv As 共享驱动器)
            If drv Is Nothing Then Return
            SyncLock _表锁
                Dim dummy As Byte
                If drv._订阅者.TryRemove(animator, dummy) Then
                    If drv._订阅者.IsEmpty Then
                        Try : drv._定时器.Stop() : Catch : End Try
                    End If
                End If
            End SyncLock
        End Sub
    End Class

#End Region

    Private ReadOnly _秒表 As New Stopwatch()
    Private ReadOnly _所有者 As Control
    Private _驱动 As 共享驱动器
    Private _进度 As Single = 0.0F
    Private _起始进度 As Single = 0.0F
    Private _目标 As Single = 0.0F
    Private _动画中 As Boolean = False
    Private _当前段时长 As Double = 0
    Private _缓动模式 As EasingModeEnum = EasingModeEnum.EaseOut
    Private _帧率 As Integer = 30
    Private _已释放 As Boolean = False

    ''' <summary>动画时长 (毫秒)，0 = 无动画</summary>
    Public Property Duration As Integer = 300

    ''' <summary>动画帧率上限，最小 1。变更会在下一次 AnimateTo 时切换到对应共享驱动器。</summary>
    Public Property FPS As Integer
        Get
            Return _帧率
        End Get
        Set(value As Integer)
            _帧率 = Math.Max(1, value)
        End Set
    End Property

    ''' <summary>缓动模式</summary>
    Public Property EasingMode As EasingModeEnum
        Get
            Return _缓动模式
        End Get
        Set(value As EasingModeEnum)
            _缓动模式 = value
        End Set
    End Property

    ''' <summary>当前动画进度</summary>
    Public ReadOnly Property Progress As Single
        Get
            Return _进度
        End Get
    End Property

    Public Sub New(owner As Control)
        _所有者 = owner
        If owner IsNot Nothing Then
            ' 仅在控件真正 Dispose 时退订；HandleDestroyed 不视为终态——
            ' WinForms 会在更换父级/字体/主题/RTL 等情况下销毁并重建句柄，
            ' 此时若把 animator 永久置为已释放，会导致后续动画完全失效（残缺）。
            AddHandler owner.Disposed, AddressOf 所有者已释放
            AddHandler owner.HandleDestroyed, AddressOf 所有者句柄销毁
        End If
    End Sub

    ''' <summary>句柄被销毁（可能即将重建）：仅暂停动画，不标记终态。</summary>
    Private Sub 所有者句柄销毁(sender As Object, e As EventArgs)
        Try
            StopAnimation()
        Catch
        End Try
    End Sub

    ''' <summary>控件 Dispose：永久释放 animator。</summary>
    Private Sub 所有者已释放(sender As Object, e As EventArgs)
        Try
            StopAnimation()
        Catch
        End Try
        _已释放 = True
    End Sub

    ''' <summary>立即设置进度，不播放动画</summary>
    Public Sub SetImmediate(value As Single)
        StopAnimation()
        _进度 = value
        If _所有者 IsNot Nothing AndAlso _所有者.IsHandleCreated Then _所有者.Invalidate()
    End Sub

    ''' <summary>启动动画，从当前进度平滑过渡到目标值</summary>
    Public Sub AnimateTo(target As Single)
        If _已释放 Then Return
        If _所有者 Is Nothing OrElse Not _所有者.IsHandleCreated OrElse Duration <= 0 Then
            ' 早退路径：必须先停止任何正在进行的动画并退订共享驱动器，
            ' 否则会出现订阅泄漏，且后台线程仍会基于"新目标 + 旧起点 + 未重启秒表"
            ' 计算出抖动帧。
            StopAnimation()
            _目标 = target
            _进度 = _目标
            _起始进度 = _目标
            If _所有者 IsNot Nothing AndAlso _所有者.IsHandleCreated Then _所有者.Invalidate()
            Return
        End If
        _目标 = target
        _起始进度 = _进度
        Dim 距离 As Single = Math.Abs(_目标 - _起始进度)
        If 距离 < 0.001F Then
            _进度 = _目标
            StopAnimation()
            _所有者.Invalidate()
            Return
        End If
        _当前段时长 = Duration * 距离
        _秒表.Restart()
        If Not _动画中 Then
            _动画中 = True
        End If
        ' 切换到与当前 FPS 匹配的共享驱动器（如有变化）
        Dim 新驱动 = 共享驱动器.订阅(Me, _帧率)
        If _驱动 IsNot Nothing AndAlso _驱动 IsNot 新驱动 Then
            共享驱动器.退订(Me, _驱动)
        End If
        _驱动 = 新驱动
    End Sub

    ''' <summary>由共享驱动器在每个刻度调用，更新进度并触发重绘。</summary>
    Friend Sub OnDriverTick()
        If Not _动画中 Then Return
        If _已释放 Then
            StopAnimation()
            Return
        End If
        Dim owner = _所有者
        If owner Is Nothing Then
            StopAnimation()
            Return
        End If
        ' 句柄可能因父级/字体/主题切换而暂时销毁后重建——此时仅跳过本帧的 Invalidate，
        ' 让动画进度继续推进，避免出现"残缺停帧"现象。
        If Not owner.IsHandleCreated Then Return
        Dim elapsed As Double = _秒表.Elapsed.TotalMilliseconds
        Dim t As Single = CSng(Math.Min(If(_当前段时长 > 0, elapsed / _当前段时长, 1.0), 1.0))
        Dim eased As Single
        Select Case _缓动模式
            Case EasingModeEnum.EaseInOut
                If t < 0.5F Then
                    eased = 4.0F * t * t * t
                Else
                    eased = 1.0F - CSng(Math.Pow(-2.0 * t + 2.0, 3) / 2.0)
                End If
            Case Else
                eased = 1.0F - CSng(Math.Pow(1.0 - t, 3))
        End Select
        _进度 = _起始进度 + (_目标 - _起始进度) * eased
        Dim 完成 As Boolean = (t >= 1.0F)
        If 完成 Then
            _进度 = _目标
            StopAnimation()
        End If
        ' 由后台线程派发，需要 BeginInvoke 回 UI 线程触发重绘。
        ' 在 IsHandleCreated 检查与 BeginInvoke 实际执行之间存在窗口期：
        ' 控件句柄可能正在被销毁/重建，BeginInvoke 会抛 ObjectDisposedException 或 InvalidOperationException。
        ' 这是正常并发现象，静默忽略本帧；下一帧若句柄重建则会自然恢复，不要在此处 StopAnimation。
        If _已释放 Then Return
        Try
            If owner.IsHandleCreated AndAlso Not owner.IsDisposed Then
                If owner.InvokeRequired Then
                    owner.BeginInvoke(New Action(Sub() owner.Invalidate()))
                Else
                    owner.Invalidate()
                End If
            End If
        Catch ex As ObjectDisposedException
            ' 句柄已释放：忽略本帧
        Catch ex As InvalidOperationException
            ' 句柄尚未创建或正在重建：忽略本帧
        Catch
        End Try
    End Sub

    Public Sub StopAnimation()
        If _动画中 Then
            _动画中 = False
            _秒表.Stop()
            Dim drv = _驱动
            _驱动 = Nothing
            共享驱动器.退订(Me, drv)
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _已释放 Then Return
        _已释放 = True
        Try
            If _所有者 IsNot Nothing Then
                RemoveHandler _所有者.Disposed, AddressOf 所有者已释放
                RemoveHandler _所有者.HandleDestroyed, AddressOf 所有者句柄销毁
            End If
        Catch
        End Try
        StopAnimation()
    End Sub
End Class
