''' <summary>
''' 初始化和切页使用的同步 UI 线程刷新事务。
''' 请求累积到最外层作用域结束后再按外到内派发。
''' 不得跨 Await 或模态消息循环持有作用域。
''' </summary>
Friend NotInheritable Class D3D_RenderUpdate
    <ThreadStatic>
    Private Shared _depth As Integer
    <ThreadStatic>
    Private Shared _roots As HashSet(Of Control)
    <ThreadStatic>
    Private Shared _committing As Boolean
    <ThreadStatic>
    Private Shared _completed As List(Of Action)

    Friend Shared Sub AfterCommit(回调 As Action)
        If Not IsActive Then
            回调()
            Return
        End If
        If _completed Is Nothing Then _completed = New List(Of Action)()
        _completed.Add(回调)
    End Sub

    Friend Shared ReadOnly Property IsCommitting As Boolean
        Get
            Return _committing
        End Get
    End Property

    Friend Shared ReadOnly Property IsActive As Boolean
        Get
            Return _depth > 0
        End Get
    End Property

    Friend Shared Function Begin(root As Control) As IDisposable
        ArgumentNullException.ThrowIfNull(root)
        If root.IsDisposed Then Throw New ObjectDisposedException(root.Name)
        If root.InvokeRequired Then Throw New InvalidOperationException("Render updates must run on the owning UI thread.")
        If _roots Is Nothing Then _roots = New HashSet(Of Control)()
        _roots.Add(root)
        _depth += 1
        Return New UpdateScope()
    End Function

    Private NotInheritable Class UpdateScope
        Implements IDisposable
        Private ReadOnly _threadId As Integer = Environment.CurrentManagedThreadId
        Private ReadOnly _started As Long = D3D_RefreshDiagnostics.Start()
        Private _disposed As Boolean

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then Return
            If Environment.CurrentManagedThreadId <> _threadId Then Throw New InvalidOperationException("Dispose the render update on its original UI thread.")
            _disposed = True
            D3D_RefreshDiagnostics.Record(_started, "UpdateScope")
            _depth -= 1
            If _depth <> 0 Then Return
            Dim 根控件集合 = _roots.ToArray()
            _roots.Clear()
            Dim 完成回调 = If(_completed?.ToArray(), Array.Empty(Of Action)())
            _completed?.Clear()
            _committing = True
            Try
                For Each root In 根控件集合
                    If root.IsDisposed Then Continue For
                    ' 外层作用域已覆盖此子树，包括事务期间新加入的控件。
                    If 根控件集合.Any(Function(其他根) 其他根 IsNot root AndAlso Not 其他根.IsDisposed AndAlso
                                     D3D_ControlTreeWalker.IsDescendantOrSelf(root, 其他根)) Then Continue For
                    OuterToInnerRefreshScheduler.RequestFull(root, invalidateChildren:=True)
                Next
                OuterToInnerRefreshScheduler.ResumeAfterRenderUpdate()
            Finally
                _committing = False
            End Try
            Try
                D3D_V5Presentation.ResumeAfterRenderUpdate()
            Finally
                For Each 回调 In 完成回调
                    回调()
                Next
            End Try
        End Sub
    End Class
End Class
