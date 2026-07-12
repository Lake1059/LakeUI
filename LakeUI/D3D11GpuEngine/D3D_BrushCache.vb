Imports Vortice.Direct2D1

''' <summary>
''' D3D_BrushCache 管理窗口级 D2D brush 资源。
''' 它持有 GPU 对象，绑定 device generation；generation 变化或 device lost 时必须整体释放。
''' 它不允许控件跨帧持有 ID2D1Brush，RenderGpu 只能通过 D3D_PaintContext/Compositor 请求画刷。
''' </summary>
Public NotInheritable Class D3D_BrushCache
    Implements IDisposable

    Private ReadOnly _solidBrushes As New Dictionary(Of D3D_BrushKey, D3D_BrushCacheEntry)()
    Private _clock As Long
    Private _disposed As Boolean

    Public Property MaxSolidBrushes As Integer = 256

    Public Function GetSolidBrush(context As ID2D1DeviceContext, color As System.Drawing.Color, generation As Integer) As ID2D1SolidColorBrush
        Return GetSolidBrushCore(context, color, generation, mapHdr:=True)
    End Function

    Friend Function GetRawSolidBrush(context As ID2D1DeviceContext, color As System.Drawing.Color, generation As Integer) As ID2D1SolidColorBrush
        Return GetSolidBrushCore(context, color, generation, mapHdr:=False)
    End Function

    Private Function GetSolidBrushCore(context As ID2D1DeviceContext,
                                       color As System.Drawing.Color,
                                       generation As Integer,
                                       mapHdr As Boolean) As ID2D1SolidColorBrush
        If _disposed Then Throw New ObjectDisposedException(NameOf(D3D_BrushCache))
        If context Is Nothing Then Throw New ArgumentNullException(NameOf(context))

        Dim hdrRevision = If(mapHdr, D3D_HdrOutput.VectorColorRevision, 0)
        Dim key As New D3D_BrushKey(context, generation, hdrRevision, mapHdr, color.ToArgb())
        Dim entry As D3D_BrushCacheEntry = Nothing
        If _solidBrushes.TryGetValue(key, entry) Then
            entry.LastUsed = NextClock()
            Return entry.Brush
        End If

        Dim brushColor = If(mapHdr, D3D_HdrOutput.MapColor4(color), D3D_HdrOutput.ToRawColor4(color))
        Dim brush = context.CreateSolidColorBrush(brushColor)
        _solidBrushes(key) = New D3D_BrushCacheEntry(brush, generation, NextClock())
        Trim(protectedKey:=key)
        Return brush
    End Function

    Public Sub Invalidate()
        For Each entry In _solidBrushes.Values
            Try : entry.Brush.Dispose() : Catch : End Try
        Next
        _solidBrushes.Clear()
    End Sub

    Private Sub Trim(protectedKey As D3D_BrushKey)
        Dim limit = Math.Max(0, GlobalOptions.BrushCacheLimit)
        MaxSolidBrushes = limit
        While _solidBrushes.Count > limit
            Dim victim = _solidBrushes.
                Where(Function(kv) Not kv.Key.Equals(protectedKey)).
                OrderBy(Function(kv) kv.Value.LastUsed).
                FirstOrDefault()
            If victim.Value Is Nothing Then Exit While
            _solidBrushes.Remove(victim.Key)
            Try : victim.Value.Brush.Dispose() : Catch : End Try
        End While
    End Sub

    Private Structure D3D_BrushKey
        Implements IEquatable(Of D3D_BrushKey)

        Private ReadOnly _context As ID2D1DeviceContext
        Private ReadOnly _generation As Integer
        Private ReadOnly _hdrRevision As Integer
        Private ReadOnly _mapHdr As Boolean
        Private ReadOnly _argb As Integer

        Friend Sub New(context As ID2D1DeviceContext, generation As Integer, hdrRevision As Integer, mapHdr As Boolean, argb As Integer)
            _context = context
            _generation = generation
            _hdrRevision = hdrRevision
            _mapHdr = mapHdr
            _argb = argb
        End Sub

        Public Overloads Function Equals(other As D3D_BrushKey) As Boolean Implements IEquatable(Of D3D_BrushKey).Equals
            Return ReferenceEquals(_context, other._context) AndAlso
                   _generation = other._generation AndAlso _hdrRevision = other._hdrRevision AndAlso
                   _mapHdr = other._mapHdr AndAlso _argb = other._argb
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            Return TypeOf obj Is D3D_BrushKey AndAlso Equals(DirectCast(obj, D3D_BrushKey))
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return HashCode.Combine(Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_context), _generation, _hdrRevision, _mapHdr, _argb)
        End Function
    End Structure

    Private Function NextClock() As Long
        _clock += 1
        Return _clock
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        Invalidate()
        GC.SuppressFinalize(Me)
    End Sub

    Private NotInheritable Class D3D_BrushCacheEntry
        Public Sub New(brush As ID2D1SolidColorBrush, generation As Integer, lastUsed As Long)
            Me.Brush = brush
            Me.Generation = generation
            Me.LastUsed = lastUsed
        End Sub

        Public ReadOnly Property Brush As ID2D1SolidColorBrush
        Public ReadOnly Property Generation As Integer
        Public Property LastUsed As Long
    End Class
End Class
