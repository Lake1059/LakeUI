''' <summary>
''' D3D_RenderCore 是新 GPU 核心入口，替代旧 D3D_PaintBridge 的职责但不兼容旧 API。
''' 它管理进程级 D3D_DeviceManager、为顶层 Form 创建 D3D_WindowCompositor 资源容器、路由 RequestRender，并处理冷启动级重置。
''' 它不迁移控件、不调用 Demo，也不向后提供旧 HDC/D2D DC RenderTarget 回退。
''' 当前唯一呈现链路是 WinForms per-control OnPaint + D3D_PaintScope 回贴 HDC。
''' <para>
''' 后续迁移控件不要再直接使用 Graphics.GetHdc，不要自己创建 D3D 设备，只能通过 PaintBridge/PaintScope 获取 D3D_PaintContext。
''' 设备资源跟随设备代号；跨设备代号缓存必须丢弃。
''' </para>
''' </summary>
Public NotInheritable Class D3D_RenderCore
    Private Shared ReadOnly _deviceManager As New D3D_DeviceManager()
    Private Shared ReadOnly _compositorsLock As New Object()
    Private Shared ReadOnly _compositors As New Dictionary(Of Form, D3D_WindowCompositor)()

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
    ''' 获取或创建指定 Form 的 V3 资源容器。它不创建 swapchain，也不参与 WinForms 控件堆叠。
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

        Dim bounds = dirtyRect
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then bounds = New Rectangle(Point.Empty, control.Size)
        bounds = Rectangle.Intersect(New Rectangle(Point.Empty, control.Size), bounds)
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then bounds = New Rectangle(Point.Empty, control.Size)

        OuterToInnerRefreshScheduler.Request(control, bounds)

        NotifyControlInvalidated(control, bounds)
    End Sub

    Friend Shared Sub NotifyControlInvalidated(control As Control, dirtyRect As Rectangle)
        If control Is Nothing OrElse control.IsDisposed Then Return

        Try : D3D_BackgroundPenetration.Invalidate(control, dirtyRect) : Catch : End Try
    End Sub

    Public Shared Sub InvalidateExistingTextResources(control As Control)
        Dim compositor = TryGetExistingWindowCompositor(control)
        If compositor Is Nothing Then Return

        Try : compositor.TextRenderer.Invalidate() : Catch : End Try
    End Sub

    Friend Shared Function CleanupD2DResources(level As D3DCacheCleanupLevel,
                                               Optional owner As Control = Nothing,
                                               Optional invalidateAfterCleanup As Boolean = True) As Integer
        Dim targetForm As Form = If(level = D3DCacheCleanupLevel.ReleaseEverything, Nothing, ResolveCompositorForm(owner))
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
            Try : D3D_GpuCache.TrimToBudget() : Catch : End Try
        End If

        If invalidateAfterCleanup Then
            For Each form In invalidateForms
                RequestFullFormRender(form)
            Next
        End If

        Return cleaned
    End Function

    Friend Shared Function HasActivePaint(Optional owner As Control = Nothing) As Boolean
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
                OuterToInnerRefreshScheduler.RequestFull(form, invalidateChildren:=True)
            End If
        Catch
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
        D3D_BackgroundPenetration.Invalidate(source)
    End Sub

    Public Shared Sub InvalidateBackgroundSource(source As Control, dirtyRect As Rectangle)
        D3D_BackgroundPenetration.Invalidate(source, dirtyRect)
    End Sub

    Friend Shared Sub InvalidateBackgroundSnapshots(control As Control)
        D3D_BackgroundPenetration.Invalidate(control)
    End Sub

    ''' <summary>
    ''' 冷启动级重置：先让所有窗口资源容器释放可重建缓存，再失效进程级设备。
    ''' InvalidateDevice 会再次广播设备丢失事件；合成器的处理必须保持幂等，用于覆盖驱动更新、TDR 恢复后手动重置等场景。
    ''' 下一次 RequestRender 会按新的设备代号按需重建设备和缓存。
    ''' </summary>
    Public Shared Sub ResetRenderCore()
        CleanupD2DResources(D3DCacheCleanupLevel.ReleaseEverything, owner:=Nothing, invalidateAfterCleanup:=False)
        _deviceManager.InvalidateDevice()
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
        SyncLock _compositorsLock
            snapshot = _compositors.Values.Where(Function(c) c IsNot Nothing).ToList()
        End SyncLock

        For Each compositor In snapshot
            Try : compositor.HandleDeviceLost() : Catch : End Try
        Next
    End Sub
End Class
