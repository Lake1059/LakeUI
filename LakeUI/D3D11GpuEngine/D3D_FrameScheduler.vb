''' <summary>
''' D3D_FrameScheduler 控制窗口级普通刷新和动画刷新节奏。
''' Invalidate 不等于立刻绘制；dirty region 在 D3D_WindowCompositor 内合并，scheduler 只负责把请求压到下一次 UI timer tick。
''' 它不拥有 GPU 对象，不执行控件绘制，也不调用旧 OnPaint/HDC 路径。
''' </summary>
Public NotInheritable Class D3D_FrameScheduler
    Implements IDisposable

    Private ReadOnly _owner As D3D_WindowCompositor
    Private ReadOnly _timer As New Timer()
    Private ReadOnly _clock As New Stopwatch()
    Private _pending As Boolean
    Private _disposed As Boolean
    Private _minimumFrameIntervalMs As Integer = 16

    Public Sub New(owner As D3D_WindowCompositor)
        _owner = owner
        _timer.Interval = _minimumFrameIntervalMs
        AddHandler _timer.Tick, AddressOf OnTick
        _clock.Start()
    End Sub

    Public Property MinimumFrameIntervalMs As Integer
        Get
            Return _minimumFrameIntervalMs
        End Get
        Set(value As Integer)
            _minimumFrameIntervalMs = Math.Max(1, value)
        End Set
    End Property

    ''' <summary>
    ''' 请求下一帧。多次请求只保留一个 pending tick，避免无限制刷 UI message；动画刷新同样由这里节流。
    ''' </summary>
    Public Sub RequestFrame()
        If _disposed Then Return
        _pending = True

        Dim elapsed = CInt(Math.Min(Integer.MaxValue, _clock.ElapsedMilliseconds))
        _timer.Interval = Math.Max(1, _minimumFrameIntervalMs - elapsed)
        If Not _timer.Enabled Then _timer.Start()
    End Sub

    Private Sub OnTick(sender As Object, e As EventArgs)
        If _disposed Then Return
        _timer.Stop()
        If Not _pending Then Return

        _pending = False
        _clock.Restart()
        _owner.RenderScheduledFrame()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        RemoveHandler _timer.Tick, AddressOf OnTick
        _timer.Dispose()
        GC.SuppressFinalize(Me)
    End Sub
End Class
