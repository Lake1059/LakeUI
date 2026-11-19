' UI-thread dependency edges. Record the sampled version so an already-current consumer is left alone.
Friend Module D3D12_BackgroundDependencies
    Private NotInheritable Class Edge
        Public Source As Control
        Public Consumer As Control
        Public Version As Long
        Public ReadOnly Coordinates As New List(Of Control)()

        Public Sub Changed(sender As Object, e As EventArgs)
            If Not Consumer.IsDisposed AndAlso Consumer.Visible Then D3D_V5Presentation.RequestRenderBatched(Consumer)
        End Sub

        Public Sub TopologyChanged(sender As Object, e As EventArgs)
            Remove(Consumer)
            Changed(sender, e)
        End Sub

        Public Sub Removed(sender As Object, e As EventArgs)
            Remove(Consumer)
        End Sub
    End Class

    Private ReadOnly edges As New Dictionary(Of Control, Edge)()
    Private ReadOnly consumers As New Dictionary(Of Control, HashSet(Of Edge))()

    Friend Function Accept(consumer As Control, source As Control) As Boolean
        If source Is Nothing OrElse source.IsDisposed OrElse consumer.IsDisposed Then Return False
        Dim node = source
        Dim seen As New HashSet(Of Control)()
        While node IsNot Nothing
            If node Is consumer OrElse Not seen.Add(node) Then Return False
            Dim provider = TryCast(node, D3D_IBackgroundSourceProvider)
            Dim explicitSource As Control = Nothing
            node = If(provider IsNot Nothing AndAlso provider.TryGetBackgroundSource(explicitSource) AndAlso explicitSource IsNot Nothing,
                      explicitSource, D3D_ControlSurfaceRegistry.ResolveNearestGpuAncestor(node))
        End While
        Return True
    End Function

    Friend Sub Sample(consumer As Control, source As Control, version As Long)
        Dim edge As Edge = Nothing
        If edges.TryGetValue(consumer, edge) Then
            If edge.Source Is source Then
                edge.Version = version
                Return
            End If
            Remove(consumer)
        End If
        edge = New Edge With {.Consumer = consumer, .Source = source, .Version = version}
        edges.Add(consumer, edge)
        Dim setOfEdges As HashSet(Of Edge) = Nothing
        If Not consumers.TryGetValue(source, setOfEdges) Then
            setOfEdges = New HashSet(Of Edge)()
            consumers.Add(source, setOfEdges)
        End If
        setOfEdges.Add(edge)
        Dim watch As New HashSet(Of Control)()
        For Each root In {consumer, source}
            Dim node = root
            While node IsNot Nothing AndAlso watch.Add(node)
                edge.Coordinates.Add(node)
                AddHandler node.LocationChanged, AddressOf edge.Changed
                AddHandler node.SizeChanged, AddressOf edge.Changed
                AddHandler node.ParentChanged, AddressOf edge.TopologyChanged
                node = node.Parent
            End While
        Next
        AddHandler source.Disposed, AddressOf edge.Removed
        AddHandler consumer.Disposed, AddressOf edge.Removed
        AddHandler source.VisibleChanged, AddressOf edge.Changed
        If TypeOf source Is Form Then AddHandler source.Invalidated, AddressOf edge.Changed
    End Sub

    Friend Sub Published(source As Control, version As Long)
        Dim setOfEdges As HashSet(Of Edge) = Nothing
        If Not consumers.TryGetValue(source, setOfEdges) Then Return
        For Each edge In setOfEdges
            If edge.Version <> version Then edge.Changed(source, EventArgs.Empty)
        Next
    End Sub

    Friend Sub Remove(consumer As Control)
        Dim edge As Edge = Nothing
        If Not edges.Remove(consumer, edge) Then Return
        Dim setOfEdges = consumers(edge.Source)
        setOfEdges.Remove(edge)
        If setOfEdges.Count = 0 Then consumers.Remove(edge.Source)
        For Each node In edge.Coordinates
            RemoveHandler node.LocationChanged, AddressOf edge.Changed
            RemoveHandler node.SizeChanged, AddressOf edge.Changed
            RemoveHandler node.ParentChanged, AddressOf edge.TopologyChanged
        Next
        RemoveHandler edge.Source.VisibleChanged, AddressOf edge.Changed
        If TypeOf edge.Source Is Form Then RemoveHandler edge.Source.Invalidated, AddressOf edge.Changed
        RemoveHandler edge.Source.Disposed, AddressOf edge.Removed
        RemoveHandler consumer.Disposed, AddressOf edge.Removed
    End Sub
End Module
