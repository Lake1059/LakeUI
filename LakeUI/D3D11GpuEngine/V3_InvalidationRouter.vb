''' <summary>
''' V3_InvalidationRouter 是后续 GPU 控件迁移的非渲染失效入口。
''' 它负责把控件 dirty rect 映射到窗口坐标并转交 D3D_WindowCompositor。
''' 它不创建 GPU 资源，不调用 WinForms HDC 绘制，也不要求现有控件在本阶段实现任何接口。
''' </summary>
Friend NotInheritable Class V3_InvalidationRouter
    Private Sub New()
    End Sub

    ''' <summary>
    ''' 后续迁移控件状态变化时调用。这里不会立即绘制；真正帧节奏由 D3D_FrameScheduler 控制。
    ''' </summary>
    Public Shared Sub RequestRender(control As Control, dirtyRect As Rectangle)
        If control Is Nothing OrElse control.IsDisposed Then Return

        Dim bounds = dirtyRect
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then
            Dim source = TryCast(control, V3_IGpuInvalidationSource)
            bounds = If(source IsNot Nothing, source.GetRenderBounds(), New Rectangle(Point.Empty, control.Size))
        End If

        D3D_RenderCore.RequestRender(control, bounds)
    End Sub
End Class
