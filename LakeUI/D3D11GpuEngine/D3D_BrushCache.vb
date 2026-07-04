Imports Vortice.Direct2D1

''' <summary>
''' D3D_BrushCache 管理窗口级 D2D brush 资源。
''' 它持有 GPU 对象，绑定 device generation；generation 变化或 device lost 时必须整体释放。
''' 它不允许控件跨帧持有 ID2D1Brush，RenderGpu 只能通过 D3D_PaintContext/Compositor 请求画刷。
''' </summary>
Public NotInheritable Class D3D_BrushCache
    Implements IDisposable

    Private ReadOnly _solidBrushes As New Dictionary(Of String, D3D_BrushCacheEntry)(StringComparer.Ordinal)
    Private _clock As Long
    Private _disposed As Boolean

    Public Property MaxSolidBrushes As Integer = 256

    Public Function GetSolidBrush(context As ID2D1DeviceContext, color As System.Drawing.Color, generation As Integer) As ID2D1SolidColorBrush
        If _disposed Then Throw New ObjectDisposedException(NameOf(D3D_BrushCache))
        If context Is Nothing Then Throw New ArgumentNullException(NameOf(context))

        Dim key = generation.ToString(Globalization.CultureInfo.InvariantCulture) & ":" & color.ToArgb().ToString(Globalization.CultureInfo.InvariantCulture)
        Dim entry As D3D_BrushCacheEntry = Nothing
        If _solidBrushes.TryGetValue(key, entry) Then
            entry.LastUsed = NextClock()
            Return entry.Brush
        End If

        Dim brush = context.CreateSolidColorBrush(D3D_PaintContext.ToColor4(color))
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

    Private Sub Trim(protectedKey As String)
        While _solidBrushes.Count > Math.Max(0, MaxSolidBrushes)
            Dim victim = _solidBrushes.
                Where(Function(kv) Not String.Equals(kv.Key, protectedKey, StringComparison.Ordinal)).
                OrderBy(Function(kv) kv.Value.LastUsed).
                FirstOrDefault()
            If victim.Key Is Nothing Then Exit While
            _solidBrushes.Remove(victim.Key)
            Try : victim.Value.Brush.Dispose() : Catch : End Try
        End While
    End Sub

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
