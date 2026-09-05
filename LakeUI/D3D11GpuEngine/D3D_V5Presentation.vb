Imports System.Diagnostics
Imports System.Reflection

''' <summary>
''' V5 纯 GPU 呈现总入口。它直接在 UI 线程渲染并提交，不依赖 WM_PAINT 合帧；
''' 设备丢失时释放全部 HWND 独占资源，随后由下一次渲染按新设备代次重建。
''' </summary>
Friend NotInheritable Class D3D_V5Presentation
    Private Shared ReadOnly _presenters As New Dictionary(Of Control, D3D_HwndSwapChainPresenter)()
    Private Shared ReadOnly _retryTimers As New Dictionary(Of Control, Timer)()
    Private Shared ReadOnly _bufferingDisabled As New HashSet(Of Control)()
    Private Shared ReadOnly _已订阅控件 As New HashSet(Of Control)()
    Private Shared ReadOnly _queuedRenders As New HashSet(Of Control)()
    Private Shared ReadOnly _queuedRendersLock As New Object()
    <ThreadStatic>
    Private Shared _renderDepth As Integer

    Friend Shared ReadOnly Property IsRendering As Boolean
        Get
            Return _renderDepth > 0
        End Get
    End Property

    Shared Sub New()
        AddHandler D3D_RenderCore.DeviceManager.DeviceLost, AddressOf 设备丢失时
        AddHandler Microsoft.Win32.SystemEvents.DisplaySettingsChanged, AddressOf 显示设置变化时
    End Sub

    Private Sub New()
    End Sub

    Friend Shared Function IsV5Control(control As Control) As Boolean
        Return control IsNot Nothing AndAlso
               TypeOf control Is V5_IGpuPresentationSource
    End Function

    Friend Shared Function Paint(control As Control, renderable As D3D_IGpuRenderable,
                                 Optional 绘制后处理 As Action(Of D3D_PaintContext) = Nothing) As Boolean
        If Not IsV5Control(control) Then Return False
        禁用WinForms双缓冲(control)
        立即渲染(control, renderable, 绘制后处理)
        ' V5 明确禁止失败后回落到 HDC；即使设备正在重建，也由下一帧 GPU 重试。
        Return True
    End Function

    Friend Shared Sub RequestRender(control As Control, Optional dirtyRect As Rectangle = Nothing)
        If Not IsV5Control(control) OrElse control Is Nothing OrElse control.IsDisposed Then Return
        ' 强制约束：容器层级必须严格按“外到内”提交。父容器表面未完成前，
        ' 不得先提交子控件；重入请求只能失效并排队到当前外层帧结束后处理。
        Dim 有效区域 = dirtyRect
        If 有效区域.Width <= 0 OrElse 有效区域.Height <= 0 Then 有效区域 = New Rectangle(Point.Empty, control.Size)
        有效区域 = Rectangle.Intersect(New Rectangle(Point.Empty, control.Size), 有效区域)
        If 有效区域.Width <= 0 OrElse 有效区域.Height <= 0 Then Return
        If Not control.IsHandleCreated OrElse Not control.Visible Then
            D3D_ControlSurfaceRegistry.MarkDirty(control, 有效区域, requestConsumers:=False)
            D3D_RenderDiagnostics.V5InvisibleSkip()
            Return
        End If
        If control.InvokeRequired Then
            Try : control.BeginInvoke(Sub() RequestRender(control, 有效区域)) : Catch : End Try
            Return
        End If
        D3D_ControlSurfaceRegistry.MarkDirty(control, 有效区域, requestConsumers:=False)
        D3D_RenderDiagnostics.V5DirtyRequested(CLng(有效区域.Width) * CLng(有效区域.Height),
                                               CLng(Math.Max(0, control.Width)) * CLng(Math.Max(0, control.Height)))
        If TypeOf control Is V5_ICoalescedPresentationSource Then
            排队渲染(control)
        Else
            立即渲染(control, TryCast(control, D3D_IGpuRenderable))
        End If
    End Sub

    Private Shared Sub 排队渲染(控件 As Control)
        If 控件 Is Nothing OrElse 控件.IsDisposed OrElse Not 控件.IsHandleCreated Then Return

        SyncLock _queuedRendersLock
            If Not _queuedRenders.Add(控件) Then Return
        End SyncLock

        Try
            控件.BeginInvoke(CType(
                Sub()
                    SyncLock _queuedRendersLock
                        _queuedRenders.Remove(控件)
                    End SyncLock
                    If 控件.IsDisposed OrElse Not 控件.IsHandleCreated OrElse Not 控件.Visible Then Return
                    立即渲染(控件, TryCast(控件, D3D_IGpuRenderable))
                End Sub, Action))
        Catch
            SyncLock _queuedRendersLock
                _queuedRenders.Remove(控件)
            End SyncLock
        End Try
    End Sub

    Private Shared Sub 立即渲染(控件 As Control, 可渲染对象 As D3D_IGpuRenderable,
                              Optional 绘制后处理 As Action(Of D3D_PaintContext) = Nothing)
        If 控件 Is Nothing OrElse 可渲染对象 Is Nothing OrElse 控件.IsDisposed Then Return
        If Not 控件.IsHandleCreated OrElse 控件.ClientSize.Width <= 0 OrElse 控件.ClientSize.Height <= 0 Then Return
        If _renderDepth > 0 Then
            控件.Invalidate()
            Return
        End If

        _renderDepth += 1
        Try
            ' 当前调用已经占用一个外层渲染槽。RenderGpu 内部不得同步驱动子控件，
            ' 以免打破外到内顺序或形成父子互相触发的递归。
            Dim 渲染开始时间 = Stopwatch.GetTimestamp()
            Dim 控件表面 = D3D_ControlSurfaceRegistry.RenderControl(控件, 可渲染对象,
                                                                     New Rectangle(Point.Empty, 控件.Size),
                                                                     绘制后处理)
            If 控件表面 Is Nothing Then Return
            Dim 渲染毫秒数 = Stopwatch.GetElapsedTime(渲染开始时间).TotalMilliseconds
            Dim 呈现器 = 获取或创建呈现器(控件)
            ' 映射消费者可能在来源控件收到自身渲染请求前，先完成其持久表面渲染。
            ' 因此仅凭表面为最新状态，不能证明 HWND 交换链已经包含该修订版本。
            If 呈现器.HasPresented(控件表面) Then
                取消重试(控件)
                Return
            End If
            Dim 提交开始时间 = Stopwatch.GetTimestamp()
            If Not 呈现器.Present(控件表面) Then
                排队重试(控件)
                Return
            End If
            Dim 提交毫秒数 = Stopwatch.GetElapsedTime(提交开始时间).TotalMilliseconds
            D3D_RenderDiagnostics.V5FrameSubmitted(渲染毫秒数,
                                                   提交毫秒数,
                                                   Stopwatch.GetTimestamp(),
                                                   控件)
            取消重试(控件)
            D3D_RenderCore.NotifyControlInvalidated(控件, New Rectangle(Point.Empty, 控件.ClientSize))
        Catch 异常 As Exception
            If D3D_RenderCore.DeviceManager.HandleDeviceLost(异常) Then
                排队重试(控件)
                Return
            End If
            If 是交换链临时重建异常(异常) Then
                排队重试(控件)
                Return
            End If
            Throw
        Finally
            _renderDepth -= 1
            ' 保留 RenderGpu 内部产生的失效请求，例如布局或字体重新计算。
            ' 这些请求不能重入提交，因此在当前帧完成后再排队补交一帧。
            If _renderDepth = 0 AndAlso D3D_ControlSurfaceRegistry.IsDirty(控件) AndAlso Not _retryTimers.ContainsKey(控件) Then
                排队渲染(控件)
            End If
        End Try
    End Sub

    Private Shared Function 获取或创建呈现器(控件 As Control) As D3D_HwndSwapChainPresenter
        Dim 呈现器 As D3D_HwndSwapChainPresenter = Nothing
        If _presenters.TryGetValue(控件, 呈现器) Then Return 呈现器
        呈现器 = New D3D_HwndSwapChainPresenter(控件, D3D_RenderCore.DeviceManager)
        _presenters(控件) = 呈现器
        If Not _已订阅控件.Add(控件) Then Return 呈现器
        AddHandler 控件.HandleDestroyed, AddressOf 句柄销毁时
        AddHandler 控件.HandleCreated, AddressOf 句柄创建时
        AddHandler 控件.SizeChanged, AddressOf 控件几何变化时
        AddHandler 控件.LocationChanged, AddressOf 控件几何变化时
        AddHandler 控件.ParentChanged, AddressOf 控件几何变化时
        AddHandler 控件.VisibleChanged, AddressOf 控件几何变化时
        AddHandler 控件.Disposed, AddressOf 控件释放时
        Return 呈现器
    End Function

    Private Shared Sub 禁用WinForms双缓冲(控件 As Control)
        If 控件 Is Nothing OrElse 控件.IsDisposed Then Return
        If _bufferingDisabled.Contains(控件) Then Return

        Try
            ' V5 表面直接呈现到当前 HWND。WinForms 自带的双缓冲复制相当于第二套合成器，
            ' 它可能把空白 GDI 缓冲覆盖到刚提交的 GPU 帧上，透明原生子控件触发父级
            ' WM_PAINT 时尤其明显。
            Dim 属性信息 = 控件.GetType().GetProperty(
                "DoubleBuffered",
                BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic)
            If 属性信息 IsNot Nothing AndAlso 属性信息.CanWrite Then
                属性信息.SetValue(控件, False, Nothing)
            End If
        Catch
            ' 某些框架控件公开了该属性但没有可写设置器；V5 路径仍然有效，
            ' 这里仅作为避免重复合成的优化。
        End Try

        _bufferingDisabled.Add(控件)
    End Sub

    Private Shared Sub 排队重试(控件 As Control)
        If 控件 Is Nothing OrElse 控件.IsDisposed OrElse Not 控件.IsHandleCreated Then Return
        Dim 重试计时器 As Timer = Nothing
        If _retryTimers.TryGetValue(控件, 重试计时器) Then Return
        重试计时器 = New Timer() With {.Interval = 250}
        AddHandler 重试计时器.Tick,
            Sub()
                If 控件.IsDisposed Then
                    取消重试(控件)
                    Return
                End If
                If 控件.IsHandleCreated AndAlso 控件.Visible Then
                    立即渲染(控件, TryCast(控件, D3D_IGpuRenderable))
                Else
                    取消重试(控件)
                End If
            End Sub
        _retryTimers(控件) = 重试计时器
        重试计时器.Start()
    End Sub

    Private Shared Sub 取消重试(控件 As Control)
        Dim 重试计时器 As Timer = Nothing
        If 控件 Is Nothing OrElse Not _retryTimers.TryGetValue(控件, 重试计时器) Then Return
        _retryTimers.Remove(控件)
        重试计时器.Stop()
        重试计时器.Dispose()
    End Sub

    Private Shared Sub 设备丢失时(发送者 As Object, 事件参数 As EventArgs)
        释放设备资源()
        For Each 控件 In _presenters.Keys.ToArray()
            排队重试(控件)
        Next
    End Sub

    Friend Shared Sub PrepareForDeviceReset()
        释放设备资源()
    End Sub

    ''' <summary>
    ''' 在设备级清理完整结束后，按外到内顺序重新提交指定窗体已有的 V5 表面。
    ''' 这里只扫描已经创建过 presenter 的控件，不扩散到普通 WinForms 子控件。
    ''' </summary>
    Friend Shared Sub RequestRenderAfterCleanup(form As Form)
        For Each 控件 In 获取清理恢复目标(form)
            RequestRender(控件)
        Next
    End Sub

    Private Shared Function 获取清理恢复目标(form As Form) As Control()
        If form Is Nothing OrElse form.IsDisposed Then Return Array.Empty(Of Control)()

        Dim targets As New HashSet(Of Control)()
        For Each control In D3D_ControlSurfaceRegistry.GetRecoveryTargets(form)
            targets.Add(control)
        Next
        For Each control In _presenters.Keys.
            Where(Function(控件)
                      If 控件 Is Nothing OrElse 控件.IsDisposed OrElse
                         Not 控件.IsHandleCreated Then Return False
                      Return Object.ReferenceEquals(D3D_RenderCore.ResolveCompositorForm(控件), form)
                  End Function)
            If control.Visible AndAlso D3D_V5Presentation.IsV5Control(control) Then targets.Add(control)
        Next
        Return targets.OrderBy(Function(control) 获取控件树深度(control)).ToArray()
    End Function

    Private Shared Function 获取控件树深度(控件 As Control) As Integer
        Dim 深度 As Integer
        Dim 当前 = If(控件 Is Nothing, Nothing, 控件.Parent)
        While 当前 IsNot Nothing
            深度 += 1
            当前 = 当前.Parent
        End While
        Return 深度
    End Function

    Private Shared Sub 释放设备资源()
        For Each 呈现器 In _presenters.Values
            Try : 呈现器.HandleDeviceLost() : Catch : End Try
        Next
        D3D_ControlSurfaceRegistry.HandleDeviceLost()
    End Sub

    Private Shared Function 是交换链临时重建异常(异常 As Exception) As Boolean
        If 异常 Is Nothing Then Return False
        Return CUInt(CLng(异常.HResult) And &HFFFFFFFFL) = &H80070005UI
    End Function

    Private Shared Sub 句柄销毁时(发送者 As Object, 事件参数 As EventArgs)
        Dim 控件 = TryCast(发送者, Control)
        If 控件 Is Nothing Then Return
        ' 句柄可以在控件未释放时重建，呈现器事件订阅保持到控件释放。
        _bufferingDisabled.Remove(控件)
        Dim 呈现器 As D3D_HwndSwapChainPresenter = Nothing
        If _presenters.TryGetValue(控件, 呈现器) Then
            呈现器.Dispose()
            _presenters.Remove(控件)
        End If
        取消重试(控件)
        SyncLock _queuedRendersLock
            _queuedRenders.Remove(控件)
        End SyncLock
        D3D_ControlSurfaceRegistry.ReleaseSurfaceResources(控件)
        If 控件.RecreatingHandle AndAlso Not 控件.IsDisposed Then
            Try : 控件.BeginInvoke(Sub() RequestRender(控件)) : Catch : End Try
        End If
    End Sub

    Private Shared Sub 控件释放时(发送者 As Object, 事件参数 As EventArgs)
        Dim 控件 = TryCast(发送者, Control)
        If 控件 IsNot Nothing Then
            If _已订阅控件.Remove(控件) Then
                RemoveHandler 控件.HandleDestroyed, AddressOf 句柄销毁时
                RemoveHandler 控件.HandleCreated, AddressOf 句柄创建时
                RemoveHandler 控件.SizeChanged, AddressOf 控件几何变化时
                RemoveHandler 控件.LocationChanged, AddressOf 控件几何变化时
                RemoveHandler 控件.ParentChanged, AddressOf 控件几何变化时
                RemoveHandler 控件.VisibleChanged, AddressOf 控件几何变化时
                RemoveHandler 控件.Disposed, AddressOf 控件释放时
            End If
            _bufferingDisabled.Remove(控件)
        End If
        If 控件 IsNot Nothing Then
            SyncLock _queuedRendersLock
                _queuedRenders.Remove(控件)
            End SyncLock
        End If
        句柄销毁时(发送者, 事件参数)
    End Sub

    Private Shared Sub 句柄创建时(发送者 As Object, 事件参数 As EventArgs)
        RequestRender(TryCast(发送者, Control))
    End Sub

    Private Shared Sub 控件几何变化时(发送者 As Object, 事件参数 As EventArgs)
        Dim 控件 = TryCast(发送者, Control)
        If 控件 Is Nothing Then Return
        Dim 几何更新来源 = TryCast(控件, V5_IGeometryUpdateSource)
        If 几何更新来源 IsNot Nothing AndAlso 几何更新来源.IsGeometryUpdateInProgress Then Return
        If Not 控件.Visible Then
            Dim 呈现器 As D3D_HwndSwapChainPresenter = Nothing
            If _presenters.TryGetValue(控件, 呈现器) Then
                呈现器.Dispose()
                _presenters.Remove(控件)
            End If
            取消重试(控件)
            SyncLock _queuedRendersLock
                _queuedRenders.Remove(控件)
            End SyncLock
            D3D_ControlSurfaceRegistry.ReleaseSurfaceResources(控件)
            Return
        End If
        RequestRender(控件)
    End Sub

    Private Shared Sub 显示设置变化时(发送者 As Object, 事件参数 As EventArgs)
        Dim 所有者 = _presenters.Keys.FirstOrDefault(Function(控件) 控件 IsNot Nothing AndAlso Not 控件.IsDisposed AndAlso 控件.IsHandleCreated)
        If 所有者 Is Nothing Then Return
        Try
            所有者.BeginInvoke(Sub()
                                  If 所有者.IsDisposed Then Return
                                  D3D_RenderCore.ResetRenderCore()
                              End Sub)
        Catch
        End Try
    End Sub
End Class
