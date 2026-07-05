''' <summary>
''' D3D_RenderCore 是新 GPU 核心入口，替代旧 D3D_PaintBridge 的职责但不兼容旧 API。
''' 它管理进程级 D3D_DeviceManager、为顶层 Form 创建 D3D_WindowCompositor、路由 RequestRender，并处理冷启动级重置。
''' 它不迁移控件、不调用 Demo、不兼容 D3D_PaintScope，也不向后提供 HDC/D2D DC RenderTarget 回退。
''' <para>
''' 后续迁移控件不要再直接使用 Graphics.GetHdc，不要自己创建 D3D device，只能通过窗口 compositor 获取 D3D_PaintContext。
''' 设备资源跟随 generation；跨 generation 缓存必须丢弃。
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
    ''' 获取或创建指定 Form 的窗口级 GPU compositor。该入口只绑定顶层 HWND，不要求任何现有控件配合。
    ''' </summary>
    Public Shared Function GetWindowCompositor(form As Form) As D3D_WindowCompositor
        If form Is Nothing OrElse form.IsDisposed Then Return Nothing

        SyncLock _compositorsLock
            Dim compositor As D3D_WindowCompositor = Nothing
            If _compositors.TryGetValue(form, compositor) Then
                If compositor IsNot Nothing AndAlso Not compositor.IsDisposed Then Return compositor
                _compositors.Remove(form)
            End If

            compositor = New D3D_WindowCompositor(form, _deviceManager)
            _compositors(form) = compositor
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
    ''' 控件迁移的核心失效入口。阶段 1 之后只让目标控件自己的 OnPaint 重新执行，
    ''' 不再创建或调度窗口级 compositor。
    ''' </summary>
    Public Shared Sub RequestRender(control As Control, dirtyRect As Rectangle)
        If control Is Nothing OrElse control.IsDisposed Then Return

        Dim bounds = dirtyRect
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then bounds = New Rectangle(Point.Empty, control.Size)
        bounds = Rectangle.Intersect(New Rectangle(Point.Empty, control.Size), bounds)
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then bounds = New Rectangle(Point.Empty, control.Size)

        Try
            If control.IsHandleCreated Then
                control.Invalidate(bounds)
            Else
                control.Invalidate()
            End If
        Catch
        End Try

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

    Private Shared Function TryGetExistingWindowCompositor(control As Control) As D3D_WindowCompositor
        Dim form = ResolveCompositorForm(control)
        If form Is Nothing Then Return Nothing

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
    ''' 冷启动级重置：先让所有窗口 compositor 停止使用窗口级 GPU target/cache，再失效进程级 device。
    ''' InvalidateDevice 会再次广播 DeviceLost；compositor 的处理必须保持幂等，用于覆盖驱动更新、TDR 恢复后手动重置等场景。
    ''' 下一次 RequestRender 会按新的 DeviceGeneration 按需重建设备、swap chain 和缓存。
    ''' </summary>
    Public Shared Sub ResetRenderCore()
        Dim snapshot As List(Of D3D_WindowCompositor)
        SyncLock _compositorsLock
            snapshot = _compositors.Values.Where(Function(c) c IsNot Nothing).ToList()
        End SyncLock

        For Each compositor In snapshot
            Try : compositor.HandleDeviceLost() : Catch : End Try
        Next

        _deviceManager.InvalidateDevice()
    End Sub

    ''' <summary>
    ''' 核心级验证入口：在指定 Form 的 HWND swap chain 上绘制一帧矩形和文字并 Present。
    ''' 该方法不修改 Demo、不要求任何现有控件迁移，也不通过 Graphics/HDC 输出；失败时返回 False 并把异常写入 errorMessage。
    ''' </summary>
    Public Shared Function RenderValidationFrame(form As Form, Optional ByRef errorMessage As String = Nothing) As Boolean
        errorMessage = Nothing
        If form Is Nothing OrElse form.IsDisposed OrElse Not form.IsHandleCreated Then
            errorMessage = "Form must be alive and have an HWND before validating the D3D11 GPU path."
            Return False
        End If

        Try
            Dim compositor = GetWindowCompositor(form)
            If compositor Is Nothing Then
                errorMessage = "D3D_WindowCompositor is not available."
                Return False
            End If

            Dim presented = compositor.RenderFrame(
                Sub(context)
                    Dim renderSize = compositor.RenderTargetSize
                    Dim bounds As New RectangleF(0, 0, Math.Max(1, renderSize.Width), Math.Max(1, renderSize.Height))
                    context.FillRectangle(bounds, form.BackColor)
                    context.FillRectangle(New RectangleF(8, 8, 160, 48), System.Drawing.Color.FromArgb(220, 40, 120, 220))
                    context.DrawText("LakeUI D3D11 GPU", form.Font, System.Drawing.Color.White, New RectangleF(16, 18, 220, 32))
                End Sub)
            If Not presented Then
                errorMessage = "Validation frame was not presented; the window may be minimized/unavailable or the device was lost and queued for rebuild."
                Return False
            End If
            Return True
        Catch ex As Exception
            errorMessage = ex.Message
            If _deviceManager.HandleDeviceLost(ex) Then Return False
            Return False
        End Try
    End Function

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
