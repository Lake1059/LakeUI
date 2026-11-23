' UI-thread graph of sampled client-coordinate regions. A control can sample both an
' explicit surface and an inherited fallback, or several sources in one renderer.
Friend Module D3D12_BackgroundDependencies
    Private NotInheritable Class Edge
        Public Source As Control
        Public Consumer As Control
        Public Region As Rectangle
        Public Offset As Point?
        Public Frame As Long
        Public ReadOnly Coordinates As New List(Of Control)()

        Public Sub Changed(sender As Object, e As EventArgs)
            InvalidateConsumer(Consumer, Rectangle.Empty)
        End Sub

        Public Sub Moved(sender As Object, e As EventArgs)
            ' Moving a window/container that carries both ends leaves the sampling transform intact.
            If Source.IsHandleCreated AndAlso Consumer.IsHandleCreated AndAlso
               Offset.HasValue AndAlso Consumer.PointToClient(Source.PointToScreen(Point.Empty)) = Offset.Value Then Return
            Changed(sender, e)
        End Sub

        Public Sub VisibilityChanged(sender As Object, e As EventArgs)
            If Not D3D_V5Presentation.IsChangingBoundPageVisibility(Source) Then Changed(sender, e)
        End Sub

        Public Sub NativeInvalidated(sender As Object, e As InvalidateEventArgs)
            InvalidateEdge(Me, e.InvalidRect)
        End Sub

        Public Sub TopologyChanged(sender As Object, e As EventArgs)
            RemoveEdge(Me)
            Changed(sender, e)
        End Sub

    End Class

    Private ReadOnly sourcesByConsumer As New Dictionary(Of Control, Dictionary(Of Control, Edge))()
    Private ReadOnly consumersBySource As New Dictionary(Of Control, HashSet(Of Edge))()
    Private frameSequence As Long

    Friend Function IsSurfaceSource(control As Control) As Boolean
        Return Not control.IsDisposed AndAlso (TypeOf control Is Form OrElse
            (D3D_V5Presentation.IsV5Control(control) AndAlso TypeOf control Is ID3D12Renderable))
    End Function

    Friend Function Accept(consumer As Control, source As Control) As Boolean
        ' Reject both declared cycles (including not-yet-published surfaces) and cycles
        ' through fallback edges already sampled by the renderers.
        If consumer Is source OrElse source.IsDisposed OrElse consumer.IsDisposed Then Return False
        Dim seen As New HashSet(Of Control)()
        Dim node = source
        While node IsNot Nothing
            If node Is consumer OrElse Not seen.Add(node) Then Return False
            Dim provider = TryCast(node, D3D_IBackgroundSourceProvider)
            Dim explicitSource As Control = Nothing
            node = If(provider IsNot Nothing AndAlso provider.TryGetBackgroundSource(explicitSource) AndAlso explicitSource IsNot Nothing AndAlso
                      Not explicitSource.IsDisposed, explicitSource, node.Parent)
        End While
        seen.Clear()
        Return Not DependsOn(source, consumer, seen)
    End Function

    Private Function DependsOn(node As Control, target As Control, seen As HashSet(Of Control)) As Boolean
        If node Is target Then Return True
        Dim inputs As Dictionary(Of Control, Edge) = Nothing
        If Not seen.Add(node) OrElse Not sourcesByConsumer.TryGetValue(node, inputs) Then Return False
        For Each edge In inputs.Values
            If Not edge.Region.IsEmpty AndAlso DependsOn(edge.Source, target, seen) Then Return True
        Next
        Return False
    End Function

    Friend Function NextFrame() As Long
        frameSequence += 1
        Return frameSequence
    End Function

    Friend Sub Sample(consumer As Control, source As Control, frame As Long, region As Rectangle, offset As Point?, complete As Boolean)
        Dim sources As Dictionary(Of Control, Edge) = Nothing
        If Not sourcesByConsumer.TryGetValue(consumer, sources) Then
            sources = New Dictionary(Of Control, Edge)()
            sourcesByConsumer.Add(consumer, sources)
            AddHandler consumer.Disposed, AddressOf ConsumerDisposed
        End If
        Dim edge As Edge = Nothing
        If Not sources.TryGetValue(source, edge) Then
            edge = New Edge With {.Consumer = consumer, .Source = source}
            sources.Add(source, edge)
            Dim setOfEdges As HashSet(Of Edge) = Nothing
            If Not consumersBySource.TryGetValue(source, setOfEdges) Then
                setOfEdges = New HashSet(Of Edge)()
                consumersBySource.Add(source, setOfEdges)
            End If
            setOfEdges.Add(edge)
            Dim watch As New HashSet(Of Control)()
            For Each root In {consumer, source}
                Dim node = root
                While node IsNot Nothing AndAlso watch.Add(node)
                    edge.Coordinates.Add(node)
                    AddHandler node.LocationChanged, AddressOf edge.Moved
                    AddHandler node.SizeChanged, AddressOf edge.Changed
                    AddHandler node.ParentChanged, AddressOf edge.TopologyChanged
                    node = node.Parent
                End While
            Next
            AddHandler source.HandleCreated, AddressOf edge.Changed
            AddHandler source.HandleDestroyed, AddressOf edge.Changed
            AddHandler source.Disposed, AddressOf edge.TopologyChanged
            AddHandler source.VisibleChanged, AddressOf edge.VisibilityChanged
            If TypeOf source Is Form Then
                AddHandler source.BackColorChanged, AddressOf edge.Changed
                AddHandler source.Invalidated, AddressOf edge.NativeInvalidated
            End If
        End If
        If complete AndAlso edge.Frame <> frame Then edge.Region = Rectangle.Empty
        edge.Frame = frame
        If offset.HasValue Then edge.Offset = offset
        If Not region.IsEmpty Then edge.Region = If(edge.Region.IsEmpty, region, Rectangle.Union(edge.Region, region))
    End Sub

    Friend Sub EndFrame(consumer As Control, frame As Long, complete As Boolean)
        If Not complete Then Return
        Dim sources As Dictionary(Of Control, Edge) = Nothing
        If Not sourcesByConsumer.TryGetValue(consumer, sources) Then Return
        ' Avoid snapshots/allocations on the stable path.
        For Each edge In sources.Values
            If edge.Frame <> frame Then
                For Each old In sources.Values.Where(Function(item) item.Frame <> frame).ToArray()
                    RemoveEdge(old)
                Next
                Exit For
            End If
        Next
    End Sub

    Friend Sub SourceChanged(source As Control, Optional dirty As Rectangle = Nothing)
        Dim setOfEdges As HashSet(Of Edge) = Nothing
        If Not consumersBySource.TryGetValue(source, setOfEdges) Then Return
        For Each edge In setOfEdges
            InvalidateEdge(edge, dirty)
        Next
    End Sub

    Private Sub InvalidateEdge(edge As Edge, dirty As Rectangle)
        If dirty.IsEmpty Then
            InvalidateConsumer(edge.Consumer, Rectangle.Empty)
        ElseIf Not edge.Region.IsEmpty Then
            Dim mapped = Rectangle.Intersect(dirty, edge.Region)
            If mapped.Width <= 0 OrElse mapped.Height <= 0 Then Return
            mapped.Offset(edge.Offset.GetValueOrDefault())
            InvalidateConsumer(edge.Consumer, mapped)
        End If
    End Sub

    Private Sub InvalidateConsumer(consumer As Control, region As Rectangle)
        If Not consumer.IsDisposed Then D3D_V5Presentation.RequestRenderBatched(consumer, region)
    End Sub

    Friend Function HasVisibleConsumers(source As Control) As Boolean
        Dim seen As HashSet(Of Control) = Nothing
        Return HasVisibleConsumers(source, seen)
    End Function

    Private Function HasVisibleConsumers(source As Control, ByRef seen As HashSet(Of Control)) As Boolean
        Dim setOfEdges As HashSet(Of Edge) = Nothing
        If Not consumersBySource.TryGetValue(source, setOfEdges) Then Return False
        If seen IsNot Nothing AndAlso Not seen.Add(source) Then Return False
        For Each edge In setOfEdges
            If edge.Region.IsEmpty OrElse edge.Consumer.IsDisposed Then Continue For
            If edge.Consumer.Visible Then Return True
            If Not consumersBySource.ContainsKey(edge.Consumer) Then Continue For
            If seen Is Nothing Then seen = New HashSet(Of Control) From {source}
            If HasVisibleConsumers(edge.Consumer, seen) Then Return True
        Next
        Return False
    End Function

    Private Sub ConsumerDisposed(sender As Object, e As EventArgs)
        Remove(DirectCast(sender, Control))
    End Sub

    Friend Sub Remove(consumer As Control)
        Dim sources As Dictionary(Of Control, Edge) = Nothing
        If Not sourcesByConsumer.TryGetValue(consumer, sources) Then Return
        For Each edge In sources.Values.ToArray()
            RemoveEdge(edge)
        Next
    End Sub

    Private Sub RemoveEdge(edge As Edge)
        Dim sources As Dictionary(Of Control, Edge) = Nothing
        If Not sourcesByConsumer.TryGetValue(edge.Consumer, sources) OrElse Not sources.Remove(edge.Source) Then Return
        If sources.Count = 0 Then
            sourcesByConsumer.Remove(edge.Consumer)
            RemoveHandler edge.Consumer.Disposed, AddressOf ConsumerDisposed
        End If
        Dim setOfEdges = consumersBySource(edge.Source)
        setOfEdges.Remove(edge)
        If setOfEdges.Count = 0 Then consumersBySource.Remove(edge.Source)
        For Each node In edge.Coordinates
            RemoveHandler node.LocationChanged, AddressOf edge.Moved
            RemoveHandler node.SizeChanged, AddressOf edge.Changed
            RemoveHandler node.ParentChanged, AddressOf edge.TopologyChanged
        Next
        RemoveHandler edge.Source.HandleCreated, AddressOf edge.Changed
        RemoveHandler edge.Source.HandleDestroyed, AddressOf edge.Changed
        RemoveHandler edge.Source.Disposed, AddressOf edge.TopologyChanged
        RemoveHandler edge.Source.VisibleChanged, AddressOf edge.VisibilityChanged
        If TypeOf edge.Source Is Form Then
            RemoveHandler edge.Source.BackColorChanged, AddressOf edge.Changed
            RemoveHandler edge.Source.Invalidated, AddressOf edge.NativeInvalidated
        End If
    End Sub
End Module
