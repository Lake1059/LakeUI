''' <summary>
''' D3D_InvalidationRouter 是 GPU 控件迁移的非渲染失效入口。
''' 阶段 1 之后它只请求 WinForms 重新触发目标控件自己的 OnPaint，不再调度窗口级整树渲染。
''' </summary>
Friend NotInheritable Class D3D_InvalidationRouter
    Private Sub New()
    End Sub

    ''' <summary>
    ''' 控件状态变化时调用。这里不立即绘制，只合并到 WinForms 自身的失效/重绘队列。
    ''' </summary>
    Public Shared Sub RequestRender(control As Control, dirtyRect As Rectangle)
        If control Is Nothing OrElse control.IsDisposed Then Return

        ' V5 控件也必须进入统一的外到内调度批次。直接同步提交会让绑定页
        ' 在父级表面准备好之前先渲染，背景映射因此可能采样到旧的黑色表面。
        ' 调度器会按控件树深度排序，并对 V5 目标直接提交 GPU 帧。
        ' V5-MIGRATION-REMOVE: Remove WinForms invalidation routing after all controls use direct V5 presentation.
        Dim bounds = dirtyRect
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then
            Dim source = TryCast(control, D3D_IGpuInvalidationSource)
            bounds = If(source IsNot Nothing, source.GetRenderBounds(), New Rectangle(Point.Empty, control.Size))
        End If

        bounds = Rectangle.Intersect(New Rectangle(Point.Empty, control.Size), bounds)
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return

        OuterToInnerRefreshScheduler.Request(control, bounds)
    End Sub
End Class
