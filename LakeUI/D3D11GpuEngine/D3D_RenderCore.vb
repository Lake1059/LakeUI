''' <summary>
''' D3D_RenderCore 是新 GPU 核心入口，替代旧 D2DHelperV2 的职责但不兼容旧 API。
''' 它管理进程级 D3D_DeviceManager、为顶层 Form 创建 D3D_WindowCompositor、路由 RequestRender，并处理冷启动级重置。
''' 它不迁移控件、不调用 Demo、不兼容 PaintScopeV2，也不向后提供 HDC/D2D DC RenderTarget 回退。
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
        Dim form = If(TypeOf control Is Form, DirectCast(control, Form), control.FindForm())
        Return GetWindowCompositor(form)
    End Function

    ''' <summary>
    ''' 后续控件迁移的核心失效入口。Invalidate 只记录 dirty region 并请求下一帧，不等于立刻绘制。
    ''' </summary>
    Public Shared Sub RequestRender(control As Control, dirtyRect As Rectangle)
        Dim compositor = GetWindowCompositor(control)
        If compositor Is Nothing Then Return

        Dim windowDirty = dirtyRect
        If control IsNot Nothing AndAlso Not control.IsDisposed AndAlso control.FindForm() IsNot Nothing AndAlso Not TypeOf control Is Form Then
            Dim screenPoint = control.PointToScreen(dirtyRect.Location)
            Dim formPoint = control.FindForm().PointToClient(screenPoint)
            windowDirty = New Rectangle(formPoint, dirtyRect.Size)
        End If

        compositor.RequestRender(windowDirty)
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
                    Dim bounds As New RectangleF(0, 0, Math.Max(1, form.ClientSize.Width), Math.Max(1, form.ClientSize.Height))
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
