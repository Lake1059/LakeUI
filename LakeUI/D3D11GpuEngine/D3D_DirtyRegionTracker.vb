''' <summary>
''' D3D_DirtyRegionTracker 管理窗口级 dirty region 合并。
''' 它只记录窗口坐标矩形，不触发绘制、不访问控件、不调用 WinForms Invalidate，也不保存任何 GPU 对象。
''' 资源生命周期由 D3D_WindowCompositor 拥有；线程边界为 UI 线程。
''' </summary>
Public NotInheritable Class D3D_DirtyRegionTracker
    Private ReadOnly _regions As New List(Of Rectangle)()

    ''' <summary>
    ''' 合并 dirty rect。空 rect 会被忽略；窗口级合并避免多个控件请求无限制刷 UI message。
    ''' </summary>
    Public Sub Add(rect As Rectangle)
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return

        Dim merged = rect
        Dim changed As Boolean
        Do
            changed = False
            For i As Integer = _regions.Count - 1 To 0 Step -1
                If _regions(i).IntersectsWith(merged) OrElse IsAdjacent(_regions(i), merged) Then
                    merged = Rectangle.Union(_regions(i), merged)
                    _regions.RemoveAt(i)
                    changed = True
                End If
            Next
        Loop While changed

        _regions.Add(merged)
    End Sub

    Public Sub AddRange(rects As IEnumerable(Of Rectangle))
        If rects Is Nothing Then Return
        For Each rect In rects
            Add(rect)
        Next
    End Sub

    ''' <summary>
    ''' 取得并清空当前 dirty region。BeginFrame 调用后，本帧拥有该快照；后续请求进入下一帧。
    ''' </summary>
    Public Function SnapshotAndClear() As IReadOnlyList(Of Rectangle)
        Dim snapshot = _regions.ToArray()
        _regions.Clear()
        Return snapshot
    End Function

    Public Sub Clear()
        _regions.Clear()
    End Sub

    Private Shared Function IsAdjacent(a As Rectangle, b As Rectangle) As Boolean
        Dim inflated = a
        inflated.Inflate(1, 1)
        Return inflated.IntersectsWith(b)
    End Function
End Class
