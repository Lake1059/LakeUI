''' <summary>
''' V3_ControlTreeWalker 是后续控件迁移使用的非渲染树遍历辅助。
''' 它不创建 GPU 资源，不绘制控件，只枚举实现 V3_IGpuRenderable 的控件并提供窗口坐标映射。
''' </summary>
Friend NotInheritable Class V3_ControlTreeWalker
    Private Sub New()
    End Sub

    Public Shared Iterator Function EnumerateGpuRenderables(root As Control) As IEnumerable(Of Control)
        If root Is Nothing OrElse root.IsDisposed Then Return

        For Each child As Control In root.Controls
            If child Is Nothing OrElse child.IsDisposed Then Continue For
            If TypeOf child Is V3_IGpuRenderable Then Yield child

            For Each nested In EnumerateGpuRenderables(child)
                Yield nested
            Next
        Next
    End Function

    Public Shared Function GetWindowBounds(control As Control, form As Form) As Rectangle
        If control Is Nothing OrElse form Is Nothing OrElse control.IsDisposed OrElse form.IsDisposed Then Return Rectangle.Empty
        Dim topLeft = form.PointToClient(control.PointToScreen(Point.Empty))
        Return New Rectangle(topLeft, control.Size)
    End Function
End Class
