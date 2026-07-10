Imports Vortice.Direct2D1

''' <summary>
''' D3D_GeometryCache 管理窗口级或进程级可复用几何。
''' 它持有 D2D geometry 对象，但不持有 render target；资源仍需在 device/factory 生命周期改变时按 generation 失效。
''' 控件不能在 RenderGpu 内创建长期 geometry，必须通过 compositor 的 D3D_GeometryCache 获取。
''' </summary>
Public NotInheritable Class D3D_GeometryCache
    Implements IDisposable

    Private Const MaxCachedGeometries As Integer = 512

    Private ReadOnly _manager As D3D_DeviceManager
    Private ReadOnly _geometries As New Dictionary(Of String, D3D_GeometryCacheEntry)(StringComparer.Ordinal)
    Private ReadOnly _lruKeys As New LinkedList(Of String)()
    Private _disposed As Boolean

    Public Sub New(manager As D3D_DeviceManager)
        _manager = manager
    End Sub

    Public Function GetOrCreateGeometry(key As String, factory As Func(Of ID2D1Geometry)) As ID2D1Geometry
        If _disposed Then Throw New ObjectDisposedException(NameOf(D3D_GeometryCache))
        If String.IsNullOrEmpty(key) Then Throw New ArgumentException("Geometry cache key is required.", NameOf(key))
        If factory Is Nothing Then Throw New ArgumentNullException(NameOf(factory))

        Dim generation = _manager.DeviceGeneration
        Dim entry As D3D_GeometryCacheEntry = Nothing
        If _geometries.TryGetValue(key, entry) Then
            If entry.Generation = generation Then
                Touch(entry)
                Return entry.Geometry
            End If
            Release(key)
        End If

        Dim geometry = factory()
        If geometry Is Nothing Then Return Nothing
        Dim node = _lruKeys.AddLast(key)
        _geometries(key) = New D3D_GeometryCacheEntry(geometry, generation, node)
        TrimExcess()
        Return geometry
    End Function

    Public Sub Release(key As String)
        Dim entry As D3D_GeometryCacheEntry = Nothing
        If Not _geometries.TryGetValue(key, entry) Then Return
        _geometries.Remove(key)
        If entry.LruNode IsNot Nothing Then _lruKeys.Remove(entry.LruNode)
        Try : entry.Geometry.Dispose() : Catch : End Try
    End Sub

    Public Sub ReleaseByPrefix(prefix As String)
        If String.IsNullOrEmpty(prefix) Then Return
        Dim keys = _geometries.Keys.Where(Function(key) key.StartsWith(prefix, StringComparison.Ordinal)).ToArray()
        For Each key In keys
            Release(key)
        Next
    End Sub

    Public Sub Invalidate()
        For Each entry In _geometries.Values
            Try : entry.Geometry.Dispose() : Catch : End Try
        Next
        _geometries.Clear()
        _lruKeys.Clear()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        Invalidate()
        GC.SuppressFinalize(Me)
    End Sub

    Private Sub Touch(entry As D3D_GeometryCacheEntry)
        If entry.LruNode Is Nothing OrElse entry.LruNode.List Is Nothing Then Return
        _lruKeys.Remove(entry.LruNode)
        _lruKeys.AddLast(entry.LruNode)
    End Sub

    Private Sub TrimExcess()
        While _geometries.Count > MaxCachedGeometries AndAlso _lruKeys.First IsNot Nothing
            Release(_lruKeys.First.Value)
        End While
    End Sub

    Private NotInheritable Class D3D_GeometryCacheEntry
        Public Sub New(geometry As ID2D1Geometry, generation As Integer, lruNode As LinkedListNode(Of String))
            Me.Geometry = geometry
            Me.Generation = generation
            Me.LruNode = lruNode
        End Sub

        Public ReadOnly Property Geometry As ID2D1Geometry
        Public ReadOnly Property Generation As Integer
        Public ReadOnly Property LruNode As LinkedListNode(Of String)
    End Class
End Class
