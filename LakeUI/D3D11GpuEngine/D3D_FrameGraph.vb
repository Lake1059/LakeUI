''' <summary>
''' D3D_FrameGraph 表达一帧中的核心渲染依赖：窗口背景、Backdrop、背景源 snapshot、GPU 控件内容入口、文字层、普通 WinForms 边界。
''' 背景映射不能重入绘制；source texture 必须先于 consumer 生成；环形依赖使用上一帧 snapshot 或断开，不允许递归调用控件绘制。
''' 它只保存依赖拓扑，不持有 GPU 对象。
''' </summary>
Public NotInheritable Class D3D_FrameGraph
    Private ReadOnly _nodes As New Dictionary(Of String, D3D_FrameGraphNode)(StringComparer.Ordinal)

    Public Shared Function CreateDefault() As D3D_FrameGraph
        Dim graph As New D3D_FrameGraph()
        graph.AddNode("WindowBackground", D3D_FrameGraphStage.WindowBackground)
        graph.AddNode("Backdrop", D3D_FrameGraphStage.Backdrop)
        graph.AddNode("BackgroundSnapshots", D3D_FrameGraphStage.BackgroundSnapshot)
        graph.AddNode("GpuControls", D3D_FrameGraphStage.GpuControls)
        graph.AddNode("Text", D3D_FrameGraphStage.Text)
        graph.AddNode("WinFormsBoundary", D3D_FrameGraphStage.WinFormsBoundary)
        graph.AddDependency("Backdrop", "WindowBackground")
        graph.AddDependency("BackgroundSnapshots", "WindowBackground")
        graph.AddDependency("GpuControls", "BackgroundSnapshots")
        graph.AddDependency("Text", "GpuControls")
        graph.AddDependency("WinFormsBoundary", "Text")
        Return graph
    End Function

    Public Sub AddNode(key As String, stage As D3D_FrameGraphStage)
        If String.IsNullOrWhiteSpace(key) Then Throw New ArgumentException("Frame graph node key is required.", NameOf(key))
        If Not _nodes.ContainsKey(key) Then _nodes(key) = New D3D_FrameGraphNode(key, stage)
    End Sub

    Public Sub AddDependency(nodeKey As String, dependsOnKey As String)
        If Not _nodes.ContainsKey(nodeKey) Then Throw New InvalidOperationException("Frame graph node does not exist: " & nodeKey)
        If Not _nodes.ContainsKey(dependsOnKey) Then Throw New InvalidOperationException("Frame graph dependency does not exist: " & dependsOnKey)
        _nodes(nodeKey).Dependencies.Add(dependsOnKey)
    End Sub

    Public Function ValidateNoCycles() As Boolean
        Dim visiting As New HashSet(Of String)(StringComparer.Ordinal)
        Dim visited As New HashSet(Of String)(StringComparer.Ordinal)

        For Each key In _nodes.Keys
            If HasCycle(key, visiting, visited) Then Return False
        Next

        Return True
    End Function

    Private Function HasCycle(key As String, visiting As HashSet(Of String), visited As HashSet(Of String)) As Boolean
        If visited.Contains(key) Then Return False
        If visiting.Contains(key) Then Return True

        visiting.Add(key)
        For Each dep In _nodes(key).Dependencies
            If HasCycle(dep, visiting, visited) Then Return True
        Next
        visiting.Remove(key)
        visited.Add(key)
        Return False
    End Function

    Private NotInheritable Class D3D_FrameGraphNode
        Public Sub New(key As String, stage As D3D_FrameGraphStage)
            Me.Key = key
            Me.Stage = stage
        End Sub

        Public ReadOnly Property Key As String
        Public ReadOnly Property Stage As D3D_FrameGraphStage
        Public ReadOnly Property Dependencies As New List(Of String)()
    End Class
End Class

Public Enum D3D_FrameGraphStage
    WindowBackground = 0
    Backdrop = 1
    BackgroundSnapshot = 2
    GpuControls = 3
    Text = 4
    WinFormsBoundary = 5
End Enum
