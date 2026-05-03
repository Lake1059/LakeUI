Imports System.Collections.Concurrent
Imports System.ComponentModel
Imports System.Runtime.InteropServices
Imports System.Threading
Imports Microsoft.Win32.SafeHandles

''' <summary>
''' 高精度定时器，基于 Windows 10 高分辨率可等待计时器 (CREATE_WAITABLE_TIMER_HIGH_RESOLUTION) +
''' Stopwatch 漂移修正 + SpinWait 精磨实现，刻度可稳定到 ±1 毫秒量级。
''' 自身计时永远在单条后台调度线程上完成；事件代码可选择两种派发模式：
''' Blocking — 等价于 WinForms Timer：处理完事件代码再开始计算下一刻度，事件回到 UI 线程；
''' NonBlocking — 到时间立即推进下一刻度，不影响事件代码执行，事件在后台 worker 线程上执行。
''' 仅支持 Windows 10 1803+ 与 .NET 6 及以上目标。
''' </summary>
<DesignerCategory("Component")>
<DefaultEvent("Tick")>
<ToolboxItem(True)>
Public Class PrecisionTimer
    Inherits Component

#Region "枚举"

    ''' <summary>事件分发模式。</summary>
    Public Enum DispatchModeEnum
        ''' <summary>等价于原版 WinForms Timer：到时间执行完事件代码后再开始计算下一刻度；事件在 SynchronizingObject 所在 (UI) 线程上同步触发。</summary>
        Blocking
        ''' <summary>到时间立即开始下一刻度的计算，不等事件代码完成；事件代码在后台 worker 线程上执行。</summary>
        NonBlocking
    End Enum

    ''' <summary>当事件处理时长超过 Interval 时的处理策略。</summary>
    Public Enum OverrunPolicyEnum
        ''' <summary>排队执行，所有 Tick 都不会被丢弃，可能堆积。</summary>
        Queue
        ''' <summary>立即并发触发，由多个 worker 线程并行处理 (依赖 WorkerThreadCount &gt; 1)。</summary>
        Concurrent
        ''' <summary>正在处理中的 Tick 数量已达 WorkerThreadCount 时，丢弃后续 Tick 并累计 MissedTickCount。</summary>
        Drop
    End Enum

#End Region

#Region "Win32 定义"

    Private Const CREATE_WAITABLE_TIMER_HIGH_RESOLUTION As UInteger = &H2UI
    Private Const TIMER_ALL_ACCESS As UInteger = &H1F0003UI

    <DllImport("kernel32.dll", SetLastError:=True, CharSet:=CharSet.Unicode)>
    Private Shared Function CreateWaitableTimerExW(lpTimerAttributes As IntPtr, lpTimerName As String, dwFlags As UInteger, dwDesiredAccess As UInteger) As IntPtr
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function SetWaitableTimer(hTimer As IntPtr, ByRef pDueTime As Long, lPeriod As Integer, pfnCompletionRoutine As IntPtr, lpArgToCompletionRoutine As IntPtr, fResume As Boolean) As Boolean
    End Function

    <DllImport("winmm.dll", EntryPoint:="timeBeginPeriod", SetLastError:=True)>
    Private Shared Function timeBeginPeriod(uPeriod As UInteger) As UInteger
    End Function

    <DllImport("winmm.dll", EntryPoint:="timeEndPeriod", SetLastError:=True)>
    Private Shared Function timeEndPeriod(uPeriod As UInteger) As UInteger
    End Function

    ''' <summary>把 SafeWaitHandle 包装成可参与 WaitHandle.WaitAny 的等待对象。</summary>
    Private NotInheritable Class Win32等待句柄
        Inherits WaitHandle

        Public Sub New(handle As SafeWaitHandle)
            Me.SafeWaitHandle = handle
        End Sub
    End Class

#End Region

#Region "进程级 timeBeginPeriod 引用计数"

    Private Shared _高精度时钟引用计数 As Integer = 0

    Private Shared Sub 启用高精度时钟()
        If Interlocked.Increment(_高精度时钟引用计数) = 1 Then
            timeBeginPeriod(1UI)
        End If
    End Sub

    Private Shared Sub 关闭高精度时钟()
        If Interlocked.Decrement(_高精度时钟引用计数) = 0 Then
            timeEndPeriod(1UI)
        End If
    End Sub

#End Region

#Region "字段"

    Private _间隔毫秒 As Integer = 10
    Private _分发模式 As DispatchModeEnum = DispatchModeEnum.Blocking
    Private _溢出策略 As OverrunPolicyEnum = OverrunPolicyEnum.Queue
    Private _工作线程数 As Integer = 1
    Private _同步对象 As ISynchronizeInvoke
    Private _自动重置 As Boolean = True

    Private _调度线程 As Thread
    Private _工作线程组 As List(Of Thread)
    Private _工作队列 As BlockingCollection(Of Object)
    Private _停止信号 As ManualResetEvent
    Private _高精度句柄 As SafeWaitHandle
    Private _定时器等待 As Win32等待句柄
    Private _正在执行计数 As Integer = 0
    Private _高精度引用已加 As Boolean = False

    Private _运行中 As Integer = 0
    Private _未命中计数 As Long = 0
    Private _最后偏差毫秒 As Double = 0.0

#End Region

#Region "公开属性"

    ''' <summary>
    ''' 定时器触发间隔，单位为毫秒，最小 1。
    ''' 运行中修改会在下一刻度生效，不会影响当前正在等待中的本次刻度。
    ''' 该间隔是以启动时间为基准的累积调度，不会因处理耗时而漂移；若严重落后（超过 2 个间隔）会重设基准以避免赶工。
    ''' </summary>
    <Category("行为"), Description("定时器触发间隔 (毫秒)，最小 1。运行中修改在下一刻度生效。调度采用累积基准不会漂移。"), DefaultValue(10)>
    Public Property Interval As Integer
        Get
            Return _间隔毫秒
        End Get
        Set(value As Integer)
            If value < 1 Then Throw New ArgumentOutOfRangeException(NameOf(value), "Interval 最小为 1 毫秒。")
            _间隔毫秒 = value
        End Set
    End Property

    ''' <summary>
    ''' 事件分发模式，决定“调度线程是否等待事件代码返回”。仅在未运行时可修改。
    ''' <para>Blocking：等价于 WinForms Timer 语义——调度线程同步等待事件代码返回后才开始计算下一刻度，适用于处理不允许起重叠的场景。</para>
    ''' <para>NonBlocking：调度线程到点后立即推进下一刻度，不等待事件代码完成，适用于要求节拍稳定、不能被事件代码延迟拖慢的场景。</para>
    ''' <para>两种模式下“事件代码跑在哪个线程”都由 <see cref="SynchronizingObject"/> 决定，与本属性无关。</para>
    ''' </summary>
    <Category("行为"), Description("事件分发模式。Blocking：等价原版 Timer，处理完事件才计算下一刻度；NonBlocking：到点立即推进下一刻度。事件代码所在线程由 SynchronizingObject 决定。仅在未运行时可修改。"), DefaultValue(GetType(DispatchModeEnum), "Blocking")>
    Public Property DispatchMode As DispatchModeEnum
        Get
            Return _分发模式
        End Get
        Set(value As DispatchModeEnum)
            校验未运行()
            _分发模式 = value
        End Set
    End Property

    ''' <summary>
    ''' 事件处理时长超过 Interval 时的策略，仅对 NonBlocking 模式生效。仅在未运行时可修改。
    ''' <para>Queue：所有 Tick 都进入队列串行消费，不丢弃；处理走慢时队列会堆积。</para>
    ''' <para>Concurrent：Tick 同样进队，但依靠 WorkerThreadCount &gt; 1 实现并发消费；该项仅在未设 SynchronizingObject 时有意义（SynchronizingObject 路径下存在与否依赖目标线程只能串行消息，无法并发）。</para>
    ''' <para>Drop：“正在执行或挂起中”的 Tick 数达到 WorkerThreadCount 时丢弃后续 Tick，并累计到 MissedTickCount；适用于仅关心最新状态、不能堆积的场景 (在 SynchronizingObject 路径下可避免 UI 消息队列被 Tick 淹没)。</para>
    ''' </summary>
    <Category("行为"), Description("处理耗时 > Interval 时的策略。Queue 排队不丢；Concurrent 依靠多 worker 并发消费 (仅未设 SynchronizingObject 时有效)；Drop 超出阈值则丢弃。仅 NonBlocking 模式生效，仅在未运行时可修改。"), DefaultValue(GetType(OverrunPolicyEnum), "Queue")>
    Public Property OverrunPolicy As OverrunPolicyEnum
        Get
            Return _溢出策略
        End Get
        Set(value As OverrunPolicyEnum)
            校验未运行()
            _溢出策略 = value
        End Set
    End Property

    ''' <summary>
    ''' NonBlocking 模式下用于执行事件代码的后台工作线程数量，最小 1。仅在未运行时可修改。
    ''' <para>未设 SynchronizingObject 时：决定事件代码能否并发执行，设为 1 时多个 Tick 会串行消费，设为 N 时最多可同时跑 N 个 Tick。</para>
    ''' <para>设置 SynchronizingObject 时：事件始终在同步对象所在线程串行执行 (无法并发)，此时该属性仅作为 Drop 策略的丢弃阈值使用（挂起中的 Tick 数 ≥ 该值 则丢弃）。</para>
    ''' <para>在 Blocking 模式下此属性被忽略。</para>
    ''' </summary>
    <Category("行为"), Description("NonBlocking 模式下后台工作线程数量 (未设 SyncObj 时) 或 Drop 策略阈值 (已设 SyncObj 时)，最小 1。Blocking 模式下被忽略。仅在未运行时可修改。"), DefaultValue(1)>
    Public Property WorkerThreadCount As Integer
        Get
            Return _工作线程数
        End Get
        Set(value As Integer)
            If value < 1 Then Throw New ArgumentOutOfRangeException(NameOf(value), "WorkerThreadCount 最小为 1。")
            校验未运行()
            _工作线程数 = value
        End Set
    End Property

    ''' <summary>
    ''' 用于把 Tick 事件派发到指定线程 (通常是 UI 线程) 的同步对象，一般设为宿主窗体或任一控件。
    ''' 该属性在 Blocking / NonBlocking 两种模式下都生效，只决定“事件代码跑在哪个线程”，与“是否阻塞下一刻度”无关后者由 <see cref="DispatchMode"/> 决定。
    ''' <para>Blocking + 已设置：事件在同步对象线程同步触发，调度线程等待返回后才推进下一刻度。等价于原版 WinForms Timer。</para>
    ''' <para>Blocking + 未设置：事件在调度线程 (后台) 同步触发，需自行 Invoke 访问 UI。</para>
    ''' <para>NonBlocking + 已设置：调度线程通过 BeginInvoke 异步投递到同步对象线程后立即推进下一刻度；事件代码在该线程串行执行，可直接访问 UI 控件。这是定时刷新 UI 的首选组合。</para>
    ''' <para>NonBlocking + 未设置：事件由 WorkerThreadCount 指定数量的后台 worker 线程执行，如需访问 UI 需自行 Invoke。</para>
    ''' </summary>
    <Category("行为"), Description("用于把 Tick 事件派发到指定线程的同步对象 (通常设为宿主窗体)，在 Blocking 与 NonBlocking 两种模式下均生效，只决定事件代码所在线程。未设置时事件在后台线程触发。")>
    <Browsable(True)>
    <DefaultValue(CType(Nothing, ISynchronizeInvoke))>
    <DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
    Public Property SynchronizingObject As ISynchronizeInvoke
        Get
            Return _同步对象
        End Get
        Set(value As ISynchronizeInvoke)
            _同步对象 = value
        End Set
    End Property

    ''' <summary>
    ''' 是否自动重复触发。True 时按 Interval 持续触发直到调用 Stop；
    ''' False 时仅触发一次 Tick 后自动停止 (等同一次性延时定时器)。
    ''' </summary>
    <Category("行为"), Description("是否自动重复触发。False 时仅触发一次后自动停止，用于一次性延时场景。"), DefaultValue(True)>
    Public Property AutoReset As Boolean
        Get
            Return _自动重置
        End Get
        Set(value As Boolean)
            _自动重置 = value
        End Set
    End Property

    ''' <summary>定时器是否正在运行。Start 后为 True，Stop 调用后立即变为 False (后台清理可能仍在进行)。</summary>
    <Browsable(False)>
    Public ReadOnly Property IsRunning As Boolean
        Get
            Return Interlocked.CompareExchange(_运行中, 0, 0) = 1
        End Get
    End Property

    ''' <summary>
    ''' NonBlocking + Drop 策略下被丢弃的 Tick 次数累计值。其他策略始终为 0。
    ''' 在下一次 Start 时会重置为 0。
    ''' </summary>
    <Browsable(False)>
    Public ReadOnly Property MissedTickCount As Long
        Get
            Return Interlocked.Read(_未命中计数)
        End Get
    End Property

    ''' <summary>
    ''' 上一次 Tick 实际触发时刻与目标时刻的偏差，单位为毫秒。正数表示晚于预期。
    ''' 适合用于诊断精度；代码不应依赖该值作出调度决策。
    ''' </summary>
    <Browsable(False)>
    Public ReadOnly Property LastTickDriftMs As Double
        Get
            Return _最后偏差毫秒
        End Get
    End Property

#End Region

#Region "事件"

    ''' <summary>
    ''' 到达刻度时触发。线程上下文取决于 <see cref="DispatchMode"/> 与 <see cref="SynchronizingObject"/>：
    ''' <para>Blocking + SyncObj 已设：在同步对象所在线程 (通常 UI 线程) 同步触发，调度线程等代码返回后才推进下一刻度。</para>
    ''' <para>Blocking + 未设：在调度线程 (后台) 同步触发。</para>
    ''' <para>NonBlocking + SyncObj 已设：在同步对象所在线程异步触发（调度线程不等）。</para>
    ''' <para>NonBlocking + 未设：在 worker 后台线程触发。</para>
    ''' 请根据上下文决定是否需要 Control.Invoke。事件代码抛出的异常会被吞没以保证调度线程存活，请在代码内部自行处理。
    ''' </summary>
    <Category("行为"), Description("到达刻度时触发；线程上下文由 DispatchMode 与 SynchronizingObject 决定。")>
    Public Event Tick As EventHandler

#End Region

#Region "启动 / 停止 / 资源管理"

    Private Sub 校验未运行()
        If IsRunning Then Throw New InvalidOperationException("PrecisionTimer 运行时无法修改此属性。")
    End Sub

    ''' <summary>
    ''' 启动定时器。在设计期调用无效。重复调用幂等，已运行时不会开启第二个实例。
    ''' 启动后会创建一条后台调度线程以及 (NonBlocking 模式下) <see cref="WorkerThreadCount"/> 条 worker 线程；
    ''' 同时进程级 timeBeginPeriod(1) 引用计数 +1，提升系统时钟分辨率。
    ''' 请勿在 Tick 事件代码内同步调用 Stop，应通过 ThreadPool.QueueUserWorkItem 或 BeginInvoke 等方式异步发起。
    ''' </summary>
    Public Sub Start()
        If DesignMode Then Return
        If Interlocked.CompareExchange(_运行中, 1, 0) <> 0 Then Return

        Try
            ' 创建高精度可等待计时器 (Win10 1803+)，失败则回退到普通可等待计时器
            Dim h As IntPtr = CreateWaitableTimerExW(IntPtr.Zero, Nothing, CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, TIMER_ALL_ACCESS)
            If h = IntPtr.Zero Then
                h = CreateWaitableTimerExW(IntPtr.Zero, Nothing, 0UI, TIMER_ALL_ACCESS)
                If h = IntPtr.Zero Then
                    Throw New Win32Exception(Marshal.GetLastWin32Error(), "无法创建可等待计时器。")
                End If
            End If
            _高精度句柄 = New SafeWaitHandle(h, True)
            _定时器等待 = New Win32等待句柄(_高精度句柄)
            _停止信号 = New ManualResetEvent(False)

            启用高精度时钟()
            _高精度引用已加 = True

            If _分发模式 = DispatchModeEnum.NonBlocking AndAlso _同步对象 Is Nothing Then
                ' 仅在 NonBlocking + 未设 SynchronizingObject 时需要 worker 池。
                ' 设置了 SynchronizingObject 时，Tick 会被 BeginInvoke 到它所在线程异步执行，不需额外线程。
                _工作队列 = New BlockingCollection(Of Object)()
                _工作线程组 = New List(Of Thread)()
                For i As Integer = 0 To _工作线程数 - 1
                    Dim t As New Thread(AddressOf 工作线程主循环) With {
                        .IsBackground = True,
                        .Name = "LakeUI.PrecisionTimer.Worker#" & i.ToString()
                    }
                    _工作线程组.Add(t)
                    t.Start()
                Next
            End If

            Interlocked.Exchange(_未命中计数, 0)
            Interlocked.Exchange(_正在执行计数, 0)
            _最后偏差毫秒 = 0.0

            _调度线程 = New Thread(AddressOf 调度线程主循环) With {
                .IsBackground = True,
                .Name = "LakeUI.PrecisionTimer.Scheduler",
                .Priority = ThreadPriority.AboveNormal
            }
            _调度线程.Start()
        Catch
            Interlocked.Exchange(_运行中, 0)
            清理资源()
            Throw
        End Try
    End Sub

    ''' <summary>
    ''' 停止定时器并释放后台线程与内核句柄。重复调用幂等。
    ''' 调用后 <see cref="IsRunning"/> 立即返回 False，但调度线程与 worker 可能需要短暂时间才能退出。
    ''' 在 UI 线程上调用是安全的：内部会自动检测并改走后台异步 Join 路径，以避免与事件派发中的 Control.Invoke 产生死锁。
    ''' 不允许在 Tick 事件代码内同步调用 (会同步 Join 自己)，请使用异步方式发起。
    ''' 需要完全释放资源时应调用 <see cref="Component.Dispose()"/>。
    ''' </summary>
    Public Sub [Stop]()
        If Interlocked.CompareExchange(_运行中, 0, 1) <> 1 Then Return

        Try
            _停止信号?.Set()
        Catch
        End Try

        ' 是否需要走异步 Join 路径：
        ' 1) SynchronizingObject 已设置且当前正在它所属的线程上 (典型 UI 线程)；
        ' 2) 即便没有设置 SynchronizingObject，只要当前线程拥有 WinForms SynchronizationContext
        '    (亦即 UI 线程)，调度线程或 worker 也可能通过 Control.Invoke 反向等待该线程，
        '    在此线程上同步 Join 同样会死锁——因此一并按异步处理。
        Dim sync = _同步对象
        Dim 需要异步Join As Boolean = (sync IsNot Nothing AndAlso Not sync.InvokeRequired) _
            OrElse TypeOf SynchronizationContext.Current Is System.Windows.Forms.WindowsFormsSynchronizationContext

        If 需要异步Join Then
            Dim 调度 = _调度线程
            Dim 工作组 = _工作线程组
            Dim 队列 = _工作队列
            ThreadPool.QueueUserWorkItem(
                Sub()
                    Try
                        If 调度 IsNot Nothing AndAlso 调度.IsAlive Then 调度.Join()
                        Try
                            队列?.CompleteAdding()
                        Catch
                        End Try
                        If 工作组 IsNot Nothing Then
                            For Each t In 工作组
                                Try
                                    If t.IsAlive Then t.Join()
                                Catch
                                End Try
                            Next
                        End If
                    Finally
                        清理资源()
                    End Try
                End Sub)
        Else
            Try
                If _调度线程 IsNot Nothing AndAlso _调度线程.IsAlive AndAlso Thread.CurrentThread IsNot _调度线程 Then
                    _调度线程.Join()
                End If
                Try
                    _工作队列?.CompleteAdding()
                Catch
                End Try
                If _工作线程组 IsNot Nothing Then
                    For Each t In _工作线程组
                        Try
                            If t.IsAlive AndAlso Thread.CurrentThread IsNot t Then t.Join()
                        Catch
                        End Try
                    Next
                End If
            Finally
                清理资源()
            End Try
        End If
    End Sub

    Private Sub 清理资源()
        Try
            If _高精度引用已加 Then
                关闭高精度时钟()
                _高精度引用已加 = False
            End If
        Catch
        End Try

        Try
            _工作队列?.Dispose()
        Catch
        End Try
        _工作队列 = Nothing
        _工作线程组 = Nothing
        _调度线程 = Nothing

        Try
            _停止信号?.Dispose()
        Catch
        End Try
        _停止信号 = Nothing

        ' Win32等待句柄 与 _高精度句柄 共享同一个 SafeWaitHandle，关闭其一即可
        Try
            _定时器等待?.Close()
        Catch
        End Try
        _定时器等待 = Nothing
        _高精度句柄 = Nothing
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing Then
                [Stop]()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

#End Region

#Region "调度线程"

    Private Sub 调度线程主循环()
        Try
            Dim 频率 As Long = Stopwatch.Frequency
            Dim 间隔Tick As Long = CLng(频率 * (_间隔毫秒 / 1000.0))
            If 间隔Tick < 1 Then 间隔Tick = 1
            Dim 起始Tick As Long = Stopwatch.GetTimestamp()
            Dim 已触发次数 As Long = 0
            Dim 等待句柄() As WaitHandle = {_停止信号, _定时器等待}
            ' 自旋阈值：剩余时间小于约 0.5 ms 时切换到 SpinWait 精磨
            Dim 自旋阈值Tick As Long = 频率 \ 2000

            Do
                ' 允许 Interval 在运行时调整 (下一刻度生效)
                Dim 当前间隔毫秒 As Integer = _间隔毫秒
                间隔Tick = CLng(频率 * (当前间隔毫秒 / 1000.0))
                If 间隔Tick < 1 Then 间隔Tick = 1

                已触发次数 += 1
                Dim 目标Tick As Long = 起始Tick + 已触发次数 * 间隔Tick

                ' 若已严重落后 (>= 2 个间隔)，跳到当前时间，避免赶工风暴
                Dim 现在 As Long = Stopwatch.GetTimestamp()
                If 现在 - 目标Tick > 间隔Tick * 2 Then
                    起始Tick = 现在
                    已触发次数 = 1
                    目标Tick = 起始Tick + 间隔Tick
                End If

                Dim 剩余Tick As Long = 目标Tick - Stopwatch.GetTimestamp()
                If 剩余Tick > 自旋阈值Tick Then
                    ' 100ns 单位的相对到期时间 (负数 = 相对当前)
                    Dim 等待Tick As Long = 剩余Tick - 自旋阈值Tick
                    Dim 剩余100ns As Long = CLng(等待Tick * (10000000.0 / 频率))
                    If 剩余100ns < 1 Then 剩余100ns = 1
                    Dim 到期 As Long = -剩余100ns
                    Dim 设置成功 As Boolean = SetWaitableTimer(_高精度句柄.DangerousGetHandle(), 到期, 0, IntPtr.Zero, IntPtr.Zero, False)
                    If 设置成功 Then
                        Dim idx As Integer = WaitHandle.WaitAny(等待句柄)
                        If idx = 0 Then Return
                    Else
                        ' 极少数情况：可等待计时器无法设置，降级为可被停止信号唤醒的睡眠
                        Dim 睡眠毫秒 As Integer = CInt(Math.Min(Integer.MaxValue, 等待Tick * 1000.0 / 频率))
                        If 睡眠毫秒 < 1 Then 睡眠毫秒 = 1
                        If _停止信号.WaitOne(睡眠毫秒) Then Return
                    End If
                End If

                ' SpinWait 精磨到目标
                Do While Stopwatch.GetTimestamp() < 目标Tick
                    If _停止信号.WaitOne(0) Then Return
                    Thread.SpinWait(40)
                Loop

                Dim 实际Tick As Long = Stopwatch.GetTimestamp()
                _最后偏差毫秒 = (实际Tick - 目标Tick) * 1000.0 / 频率

                ' 派发 Tick
                If _分发模式 = DispatchModeEnum.Blocking Then
                    派发Blocking()
                    If _停止信号.WaitOne(0) Then Return
                    ' Blocking 下若处理时长 > 1 个间隔，重设基准避免堆积赶工
                    Dim 结束Tick As Long = Stopwatch.GetTimestamp()
                    If 结束Tick - 目标Tick > 间隔Tick Then
                        起始Tick = 结束Tick
                        已触发次数 = 0
                    End If
                Else
                    派发NonBlocking()
                End If

                If Not _自动重置 Then
                    ' 单次触发：异步停止 (避免在调度线程内 Join 自己)
                    ThreadPool.QueueUserWorkItem(Sub() [Stop]())
                    Return
                End If
            Loop
        Catch
            ' 不让任何异常逃出调度线程
        End Try
    End Sub

#End Region

#Region "派发"

    Private Sub 派发Blocking()
        ' 注意：此处使用 sync.Invoke 同步派发以避免 IAsyncResult / 内核句柄泄漏。
        ' 若调用方在 UI 线程上调用 Stop，UI 线程的 Join 已通过 ThreadPool 异步路径避免死锁。
        Dim sync = _同步对象
        Try
            If sync IsNot Nothing AndAlso sync.InvokeRequired Then
                sync.Invoke(New Action(Sub() RaiseEvent Tick(Me, EventArgs.Empty)), Nothing)
            Else
                RaiseEvent Tick(Me, EventArgs.Empty)
            End If
        Catch
        End Try
    End Sub

    Private Sub 派发NonBlocking()
        Dim sync = _同步对象
        Try
            ' Drop 策略：根据"正在执行或排队中"的 Tick 数决定是否丢弃；
            ' 阈值为 WorkerThreadCount。在 SyncObj 路径下，该值代表允许同时挂起在目标线程消息队列中的 Tick 数。
            If _溢出策略 = OverrunPolicyEnum.Drop Then
                If Interlocked.Increment(_正在执行计数) > _工作线程数 Then
                    Interlocked.Decrement(_正在执行计数)
                    Interlocked.Increment(_未命中计数)
                    Return
                End If
            End If

            If sync IsNot Nothing Then
                ' NonBlocking + SynchronizingObject：调度线程立即返回；
                ' Tick 代码在同步对象所在线程 (通常 UI 线程) 异步执行。
                ' 不访问返回的 IAsyncResult.AsyncWaitHandle、不调 EndInvoke，避免内核句柄泄漏；
                ' Drop 计数的减少与事件异常处理都在委托体内完成。
                Dim 处理 As Action =
                    Sub()
                        Try
                            RaiseEvent Tick(Me, EventArgs.Empty)
                        Catch
                        Finally
                            If _溢出策略 = OverrunPolicyEnum.Drop Then
                                Interlocked.Decrement(_正在执行计数)
                            End If
                        End Try
                    End Sub
                Try
                    sync.BeginInvoke(处理, Nothing)
                Catch
                    ' 投递失败 (例如目标已销毁)：回滚计数
                    If _溢出策略 = OverrunPolicyEnum.Drop Then
                        Interlocked.Decrement(_正在执行计数)
                    End If
                End Try
            Else
                ' NonBlocking + 未设 SyncObj：走 worker 池。Queue / Concurrent / Drop 都入队；
                ' 差异在消费并发度 (worker 线程数) 与 Drop 阈值。
                Try
                    _工作队列.Add(Nothing)
                Catch
                    If _溢出策略 = OverrunPolicyEnum.Drop Then
                        Interlocked.Decrement(_正在执行计数)
                    End If
                End Try
            End If
        Catch
        End Try
    End Sub

    Private Sub 工作线程主循环()
        Try
            For Each item In _工作队列.GetConsumingEnumerable()
                Try
                    RaiseEvent Tick(Me, EventArgs.Empty)
                Catch
                End Try
                If _溢出策略 = OverrunPolicyEnum.Drop Then
                    Interlocked.Decrement(_正在执行计数)
                End If
            Next
        Catch
        End Try
    End Sub

#End Region

End Class
