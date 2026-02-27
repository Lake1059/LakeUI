''' <summary>
''' 通用动画进度助手，封装秒表 + 计时器 / Application.Idle 驱动的缓动动画。
''' </summary>
Friend Class AnimationHelper
    Implements IDisposable

    Private ReadOnly _秒表 As New Stopwatch()
    Private ReadOnly _计时器 As New System.Windows.Forms.Timer()
    Private ReadOnly _所有者 As Control
    Private _进度 As Single = 0.0F
    Private _起始进度 As Single = 0.0F
    Private _目标 As Single = 0.0F
    Private _动画中 As Boolean = False
    Private _使用空闲驱动 As Boolean = False
    Private _当前段时长 As Double = 0

    ''' <summary>动画时长 (毫秒)，0 = 无动画</summary>
    Public Property Duration As Integer = 300

    ''' <summary>动画帧率上限，0 = 不限制 (使用 Application.Idle)</summary>
    Public Property FPS As Integer = 60

    ''' <summary>当前动画进度</summary>
    Public ReadOnly Property Progress As Single
        Get
            Return _进度
        End Get
    End Property

    Public Sub New(owner As Control)
        _所有者 = owner
    End Sub

    ''' <summary>立即设置进度，不播放动画</summary>
    Public Sub SetImmediate(value As Single)
        StopAnimation()
        _进度 = value
        _所有者.Invalidate()
    End Sub

    ''' <summary>启动动画，从当前进度平滑过渡到目标值</summary>
    Public Sub AnimateTo(target As Single)
        _目标 = target
        If Not _所有者.IsHandleCreated OrElse Duration <= 0 Then
            _进度 = _目标
            _所有者.Invalidate()
            Return
        End If
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
            _使用空闲驱动 = (FPS <= 0)
            If _使用空闲驱动 Then
                AddHandler Application.Idle, AddressOf 更新帧
            Else
                _计时器.Interval = Math.Max(1, CInt(1000.0 / FPS))
                AddHandler _计时器.Tick, AddressOf 更新帧
                _计时器.Start()
            End If
        End If
    End Sub

    Private Sub 更新帧(sender As Object, e As EventArgs)
        Dim elapsed As Double = _秒表.Elapsed.TotalMilliseconds
        Dim t As Single = CSng(Math.Min(elapsed / _当前段时长, 1.0))
        Dim eased As Single = 1.0F - CSng(Math.Pow(1.0 - t, 3))
        _进度 = _起始进度 + (_目标 - _起始进度) * eased
        If t >= 1.0F Then
            _进度 = _目标
            StopAnimation()
        End If
        _所有者.Invalidate()
    End Sub

    Public Sub StopAnimation()
        If _动画中 Then
            _动画中 = False
            If _使用空闲驱动 Then
                RemoveHandler Application.Idle, AddressOf 更新帧
            Else
                _计时器.Stop()
                RemoveHandler _计时器.Tick, AddressOf 更新帧
            End If
            _秒表.Stop()
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        StopAnimation()
        _计时器.Dispose()
    End Sub
End Class
