''' <summary>
''' V3_InvalidationRouter 是 GPU 控件迁移的非渲染失效入口。
''' 阶段 1 之后它只请求 WinForms 重新触发目标控件自己的 OnPaint，不再调度窗口级整树渲染。
''' </summary>
Friend NotInheritable Class V3_InvalidationRouter
    Private Sub New()
    End Sub

    ''' <summary>
    ''' 控件状态变化时调用。这里不立即绘制，只合并到 WinForms 自身的失效/重绘队列。
    ''' </summary>
    Public Shared Sub RequestRender(control As Control, dirtyRect As Rectangle)
        If control Is Nothing OrElse control.IsDisposed Then Return

        Dim bounds = dirtyRect
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then
            Dim source = TryCast(control, V3_IGpuInvalidationSource)
            bounds = If(source IsNot Nothing, source.GetRenderBounds(), New Rectangle(Point.Empty, control.Size))
        End If

        bounds = Rectangle.Intersect(New Rectangle(Point.Empty, control.Size), bounds)
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then bounds = New Rectangle(Point.Empty, control.Size)

        OuterToInnerRefreshScheduler.Request(control, bounds)

        Try
            D3D_RenderCore.NotifyControlInvalidated(control, bounds)
        Catch
        End Try
    End Sub
End Class
