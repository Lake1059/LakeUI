''' <summary>
''' D3D_RenderCore 是 V5 GPU 核心的进程/窗口资源入口。
''' 它管理 D3D_DeviceManager、Form 级 D3D_WindowCompositor、设备代号和冷启动级重置；
''' V5 控件由 D3D_V5Presentation 直接提交到自身 HWND；带删除标记的 GPU HDC 路径
''' 仅作为顶层 chrome 兼容保护和显式 GPU 调用保留。
''' <para>
''' 后续迁移控件不要直接使用 Graphics.GetHdc，不要自己创建 D3D 设备，只能接收 D3D_PaintContext。
''' 设备资源跟随设备代号；跨设备代号缓存必须丢弃。
''' </para>
''' </summary>
Public NotInheritable Class D3D_RenderCore
    ''' <summary>当前生产渲染引擎主版本。</summary>
    Public Const EngineVersion As Integer = 5

    ''' <summary>
    ''' 可选的非阻塞 DXGI 帧延迟闸门。默认关闭，宿主完成队列深度评估后再启用；
    ''' 该闸门绝不在 UI 线程等待。
    ''' </summary>
    Friend Shared Property V5FrameLatencySchedulerEnabled As Boolean

    Private Shared ReadOnly _deviceManager As New D3D_DeviceManager()
    Private Shared ReadOnly _compositorsLock As New Object()
    Private Shared ReadOnly _compositors As New Dictionary(Of Form, D3D_WindowCompositor)()
    Private Shared _suppressDeviceLostRender As Integer

    Shared Sub New()
        AddHandler _deviceManager.DeviceLost, AddressOf HandleProcessDeviceLost
    End Sub

    Private Sub New()
    End Sub

    Public Shared ReadOnly Property DeviceManager As D3D_DeviceManager
        Get
            Return _deviceManager
        End Get
    End Property

    ''' <summary>
    ''' 获取或创建指定 Form 的 GPU 资源容器。它不创建 swapchain，也不参与 WinForms 控件堆叠。
    ''' </summary>
    Public Shared Function GetWindowCompositor(form As Form) As D3D_WindowCompositor
        If form Is Nothing OrElse form.IsDisposed Then Return Nothing

        Dim compositor As D3D_WindowCompositor = Nothing
        SyncLock _compositorsLock
            If _compositors.TryGetValue(form, compositor) Then
                If compositor Is Nothing OrElse compositor.IsDisposed Then
                    _compositors.Remove(form)
                    compositor = Nothing
                End If
            End If

            If compositor Is Nothing Then
                compositor = New D3D_WindowCompositor(form, _deviceManager)
                _compositors(form) = compositor
            End If
        End SyncLock

        Return compositor
    End Function

    Private Shared Function TryGetExistingWindowCompositor(form As Form) As D3D_WindowCompositor
        If form Is Nothing OrElse form.IsDisposed Then Return Nothing

        SyncLock _compositorsLock
            Dim compositor As D3D_WindowCompositor = Nothing
            If Not _compositors.TryGetValue(form, compositor) Then Return Nothing
            If compositor Is Nothing OrElse compositor.IsDisposed Then
                _compositors.Remove(form)
                Return Nothing
            End If
            Return compositor
        End SyncLock
    End Function

    Public Shared Function GetWindowCompositor(control As Control) As D3D_WindowCompositor
        If control Is Nothing OrElse control.IsDisposed Then Return Nothing
        Dim form = ResolveCompositorForm(control)
        Return GetWindowCompositor(form)
    End Function

    Friend Shared Function ResolveCompositorForm(control As Control) As Form
        If control Is Nothing OrElse control.IsDisposed Then Return Nothing

        Dim form As Form = Nothing
        Try
            form = If(TypeOf control Is Form, DirectCast(control, Form), control.FindForm())
        Catch
            form = Nothing
        End Try
        If form Is Nothing OrElse form.IsDisposed Then Return Nothing

        Dim visited As New HashSet(Of Form)()
        Do While form IsNot Nothing AndAlso Not form.IsDisposed AndAlso Not form.TopLevel AndAlso form.Parent IsNot Nothing
            If visited.Contains(form) Then Exit Do
            visited.Add(form)

            Dim host As Form = Nothing
            Try
                host = form.Parent.FindForm()
            Catch
                host = Nothing
            End Try
            If host Is Nothing OrElse host Is form OrElse host.IsDisposed Then Exit Do
            form = host
        Loop

        Return form
    End Function

    ''' <summary>
    ''' 控件迁移的核心失效入口。阶段 1 之后只让目标控件自己的 OnPaint 重新执行。
    ''' </summary>
    Public Shared Sub RequestRender(control As Control, dirtyRect As Rectangle)
        If control Is Nothing OrElse control.IsDisposed Then Return

        Dim 脏区 = dirtyRect
        If 脏区.Width <= 0 OrElse 脏区.Height <= 0 Then
            脏区 = New Rectangle(Point.Empty, control.Size)
        Else
            脏区 = Rectangle.Intersect(New Rectangle(Point.Empty, control.Size), 脏区)
            If 脏区.Width <= 0 OrElse 脏区.Height <= 0 Then Return
        End If

        OuterToInnerRefreshScheduler.Request(control, 脏区)

        NotifyControlInvalidated(control, 脏区)
    End Sub

    Friend Shared Sub NotifyControlInvalidated(control As Control, dirtyRect As Rectangle)
        If control Is Nothing OrElse control.IsDisposed Then Return

        ' V5 表面通过 GPU 表面注册表传播失效；不要再进入旧的 CPU 背景快照路径。
        If D3D_V5Presentation.IsV5Control(control) Then Return

        Try : D3D_BackgroundPenetration.Invalidate(control, dirtyRect) : Catch : End Try
    End Sub

    Public Shared Sub InvalidateExistingTextResources(control As Control)
        Dim compositor = TryGetExistingWindowCompositor(control)
        If compositor Is Nothing Then Return

        Try : compositor.TextRenderer.Invalidate() : Catch : End Try
    End Sub

    Friend Shared Function CleanupD2DResources(level As D3DCacheCleanupLevel,
                                               Optional owner As Control = Nothing,
                                               Optional invalidateAfterCleanup As Boolean = False) As Integer
        Dim targetForm As Form = If(level >= D3DCacheCleanupLevel.RecreateDevice, Nothing, ResolveCompositorForm(owner))
        Dim snapshot As New List(Of D3D_WindowCompositor)()

        SyncLock _compositorsLock
            If targetForm IsNot Nothing Then
                Dim compositor As D3D_WindowCompositor = Nothing
                If _compositors.TryGetValue(targetForm, compositor) AndAlso compositor IsNot Nothing AndAlso Not compositor.IsDisposed Then
                    snapshot.Add(compositor)
                End If
            Else
                For Each compositor In _compositors.Values
                    If compositor IsNot Nothing AndAlso Not compositor.IsDisposed Then snapshot.Add(compositor)
                Next
            End If
        End SyncLock

        Dim cleaned As Integer
        Dim invalidateForms As New List(Of Form)()
        For Each compositor In snapshot
            Dim form = compositor.Form
            If compositor.CleanupD2DResources(level) Then
                cleaned += 1
                AddInvalidateForm(invalidateForms, form)
            End If
        Next
        AddInvalidateForm(invalidateForms, targetForm)

        If level = D3DCacheCleanupLevel.TrimToBudget Then
            Try : D3D_GpuCache.TrimToBudget(immediate:=True) : Catch : End Try
        End If

        If invalidateAfterCleanup Then
            For Each form In invalidateForms
                QueueFullFormRenderAfterCleanup(form)
            Next
        End If

        Return cleaned
    End Function

    Friend Shared Function HasActivePaint(Optional owner As Control = Nothing) As Boolean
        If D3D_V5Presentation.IsRendering Then Return True
        Dim targetForm = ResolveCompositorForm(owner)

        SyncLock _compositorsLock
            If targetForm IsNot Nothing Then
                Dim compositor As D3D_WindowCompositor = Nothing
                If Not _compositors.TryGetValue(targetForm, compositor) Then Return False
                If compositor Is Nothing OrElse compositor.IsDisposed Then Return False
                Return compositor.IsPainting
            End If

            For Each compositor In _compositors.Values
                If compositor IsNot Nothing AndAlso Not compositor.IsDisposed AndAlso compositor.IsPainting Then Return True
            Next
        End SyncLock

        Return False
    End Function

    Friend Shared Function ReleaseImageCache(image As Image,
                                             Optional owner As Control = Nothing,
                                             Optional invalidateAfterCleanup As Boolean = False) As Integer
        If image Is Nothing Then Return 0
        Dim targetForm = ResolveCompositorForm(owner)
        Dim snapshot As New List(Of D3D_WindowCompositor)()

        SyncLock _compositorsLock
            If targetForm IsNot Nothing Then
                Dim compositor As D3D_WindowCompositor = Nothing
                If _compositors.TryGetValue(targetForm, compositor) AndAlso compositor IsNot Nothing AndAlso Not compositor.IsDisposed Then
                    snapshot.Add(compositor)
                End If
            Else
                For Each compositor In _compositors.Values
                    If compositor IsNot Nothing AndAlso Not compositor.IsDisposed Then snapshot.Add(compositor)
                Next
            End If
        End SyncLock

        Dim cleaned As Integer
        Dim invalidateForms As New List(Of Form)()
        For Each compositor In snapshot
            If compositor.ReleaseImageCache(image) Then
                cleaned += 1
                AddInvalidateForm(invalidateForms, compositor.Form)
            End If
        Next

        If invalidateAfterCleanup Then
            For Each form In invalidateForms
                RequestFullFormRender(form)
            Next
        End If

        Return cleaned
    End Function

    Private Shared Sub AddInvalidateForm(forms As List(Of Form), form As Form)
        If forms Is Nothing OrElse form Is Nothing OrElse form.IsDisposed Then Return
        If Not forms.Contains(form) Then forms.Add(form)
    End Sub

    Private Shared Sub RequestFullFormRender(form As Form)
        Try
            If form IsNot Nothing AndAlso Not form.IsDisposed AndAlso form.IsHandleCreated Then
                ' 窗体级 GPU/缓存刷新不能扩散到原生子 HWND。每个 V5 控件拥有自己的表面并独立请求帧；
                ' 原生 WinForms 控件继续走正常 WM_PAINT 路径。
                OuterToInnerRefreshScheduler.RequestFull(form, invalidateChildren:=False)
            End If
        Catch
        End Try
    End Sub

    Friend Shared Function GetCleanupRecoveryForms(Optional owner As Control = Nothing) As Form()
        Dim targetForm = ResolveCompositorForm(owner)
        Dim forms As New List(Of Form)()

        SyncLock _compositorsLock
            If targetForm IsNot Nothing Then
                AddInvalidateForm(forms, targetForm)
            Else
                For Each compositor In _compositors.Values
                    If compositor IsNot Nothing AndAlso Not compositor.IsDisposed Then
                        AddInvalidateForm(forms, compositor.Form)
                    End If
                Next
            End If
        End SyncLock

        Return forms.ToArray()
    End Function

    Friend Shared Sub QueueCleanupRecovery(forms As Form())
        If forms Is Nothing Then Return
        For Each form In forms
            QueueFullFormRenderAfterCleanup(form)
        Next
    End Sub

    Private Shared Sub QueueFullFormRenderAfterCleanup(form As Form)
        If form Is Nothing OrElse form.IsDisposed OrElse Not form.IsHandleCreated Then Return

        Try
            form.BeginInvoke(CType(
                Sub()
                    If form.IsDisposed OrElse Not form.IsHandleCreated Then Return
                    ' Form 自身负责 ThisIsYourWindow 等窗口级装饰；V5 控件各自拥有 HWND
                    ' 交换链，必须单独重新提交，普通 Form.Invalidate 不会唤醒这些表面。
                    RequestFullFormRender(form)
                    D3D_V5Presentation.RequestRenderAfterCleanup(form)
                End Sub,
                MethodInvoker))
        Catch
            ' 句柄正在销毁时无需恢复；HandleCreated 会走正常的首次绘制路径。
        End Try
    End Sub

    Private Shared Function TryGetExistingWindowCompositor(control As Control) As D3D_WindowCompositor
        Dim form = ResolveCompositorForm(control)
        Return TryGetExistingWindowCompositor(form)
    End Function

    Public Shared Sub UnregisterBackgroundConsumer(control As Control, Optional recursive As Boolean = False)
        If control Is Nothing Then Return
        If recursive Then
            D3D_BackgroundPenetration.UnregisterBackgroundConsumer(control)
        Else
            D3D_BackgroundPenetration.UnregisterConsumer(control)
        End If
    End Sub

    Public Shared Sub InvalidateBackgroundSource(source As Control)
        If D3D_V5Presentation.IsV5Control(source) Then
            D3D_ControlSurfaceRegistry.MarkDirty(source)
            D3D_V5Presentation.RequestRender(source)
            Return
        End If
        D3D_BackgroundPenetration.Invalidate(source)
    End Sub

    Public Shared Sub InvalidateBackgroundSource(source As Control, dirtyRect As Rectangle)
        If D3D_V5Presentation.IsV5Control(source) Then
            D3D_ControlSurfaceRegistry.MarkDirty(source, dirtyRect)
            D3D_V5Presentation.RequestRender(source, dirtyRect)
            Return
        End If
        D3D_BackgroundPenetration.Invalidate(source, dirtyRect)
    End Sub

    Friend Shared Sub InvalidateBackgroundSnapshots(control As Control)
        If D3D_V5Presentation.IsV5Control(control) Then
            D3D_ControlSurfaceRegistry.MarkDirty(control)
            D3D_V5Presentation.RequestRender(control)
            Return
        End If
        D3D_BackgroundPenetration.Invalidate(control)
    End Sub

    ''' <summary>
    ''' 冷启动级重置：先让所有窗口资源容器释放可重建缓存，再失效进程级设备。
    ''' InvalidateDevice 会再次广播设备丢失事件；合成器的处理必须保持幂等，用于覆盖驱动更新、TDR 恢复后手动重置等场景。
    ''' 下一次 RequestRender 会按新的设备代号按需重建设备和缓存。
    ''' </summary>
    Public Shared Sub ResetRenderCore()
        D3D_PaintBridge.CleanupD2DResources(D3DCacheCleanupLevel.ReleaseEverything,
                                            owner:=Nothing,
                                            invalidateAfterCleanup:=False)
    End Sub

    Friend Shared Sub InvalidateDeviceForCleanup()
        Threading.Interlocked.Increment(_suppressDeviceLostRender)
        Try
            ' 翻转模型下同一 HWND 同时只能关联一个 swap-chain。必须在进程设备释放前先释放 V5 presenter，
            ' 否则下一 generation 的 CreateSwapChainForHwnd 可能在 DWM 尚持有旧链路时返回 E_ACCESSDENIED。
            D3D_V5Presentation.PrepareForDeviceReset()
            ' D3D_DeviceGlobals 为 popup/背景兼容路径保留；两个设备所有者必须在此有序入口统一失效。
            D3D_DeviceGlobals.InvalidateDevice()
            _deviceManager.InvalidateDevice()
        Finally
            Threading.Interlocked.Decrement(_suppressDeviceLostRender)
        End Try
    End Sub

    Friend Shared Sub UnregisterCompositor(form As Form, compositor As D3D_WindowCompositor)
        If form Is Nothing Then Return
        SyncLock _compositorsLock
            Dim current As D3D_WindowCompositor = Nothing
            If _compositors.TryGetValue(form, current) AndAlso Object.ReferenceEquals(current, compositor) Then
                _compositors.Remove(form)
            End If
        End SyncLock
    End Sub

    Private Shared Sub HandleProcessDeviceLost(sender As Object, e As EventArgs)
        Dim snapshot As List(Of D3D_WindowCompositor)
        Dim requestRender As Boolean = Threading.Volatile.Read(_suppressDeviceLostRender) = 0
        SyncLock _compositorsLock
            snapshot = _compositors.Values.Where(Function(c) c IsNot Nothing).ToList()
        End SyncLock

        For Each compositor In snapshot
            Try : compositor.HandleDeviceLost(requestRender) : Catch : End Try
        Next
    End Sub
End Class
