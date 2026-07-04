Imports Vortice.Direct2D1

''' <summary>
''' D3D_GeometryCache 管理窗口级或进程级可复用几何。
''' 它持有 D2D geometry 对象，但不持有 render target；资源仍需在 device/factory 生命周期改变时按 generation 失效。
''' 控件不能在 RenderGpu 内创建长期 geometry，必须通过 compositor 的 D3D_GeometryCache 获取。
''' </summary>
Public NotInheritable Class D3D_GeometryCache
    Implements IDisposable

    Private ReadOnly _manager As D3D_DeviceManager
    Private ReadOnly _geometries As New Dictionary(Of String, D3D_GeometryCacheEntry)(StringComparer.Ordinal)
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
            If entry.Generation = generation Then Return entry.Geometry
            Release(key)
        End If

        Dim geometry = factory()
        If geometry Is Nothing Then Return Nothing
        _geometries(key) = New D3D_GeometryCacheEntry(geometry, generation)
        Return geometry
    End Function

    Public Sub Release(key As String)
        Dim entry As D3D_GeometryCacheEntry = Nothing
        If Not _geometries.TryGetValue(key, entry) Then Return
        _geometries.Remove(key)
        Try : entry.Geometry.Dispose() : Catch : End Try
    End Sub

    Public Sub Invalidate()
        For Each entry In _geometries.Values
            Try : entry.Geometry.Dispose() : Catch : End Try
        Next
        _geometries.Clear()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        Invalidate()
        GC.SuppressFinalize(Me)
    End Sub

    Private NotInheritable Class D3D_GeometryCacheEntry
        Public Sub New(geometry As ID2D1Geometry, generation As Integer)
            Me.Geometry = geometry
            Me.Generation = generation
        End Sub

        Public ReadOnly Property Geometry As ID2D1Geometry
        Public ReadOnly Property Generation As Integer
    End Class
End Class
