''' <summary>有界 CPU 测量缓存；不保留 Font、Control 或 COM 对象，参与现有 CPU 预算和 LRU 淘汰。</summary>
Friend Module D3D_TextMeasurementCache
    Private ReadOnly _sync As New Object()
    Private ReadOnly _values As New Dictionary(Of Key, CacheEntry)()
    Private ReadOnly _order As New LinkedList(Of Key)()
    Private ReadOnly _budgetOwner As New BudgetOwner()
    Private _bytes As Long
    Private Const Capacity As Integer = 512

    Sub New()
        D3D_CpuCache.Register(_budgetOwner)
    End Sub

    Private NotInheritable Class CacheEntry
        Public Value As SizeF
        Public Node As LinkedListNode(Of Key)
        Public LastUsed As Long
        Public Bytes As Long
    End Class

    Private NotInheritable Class BudgetOwner
        Implements D3D_IRenderCacheOwner

        Public ReadOnly Property CacheBytes As Long Implements D3D_IRenderCacheOwner.CacheBytes
            Get
                SyncLock _sync
                    Return _bytes
                End SyncLock
            End Get
        End Property

        Public ReadOnly Property OldestUseTick As Long Implements D3D_IRenderCacheOwner.OldestUseTick
            Get
                SyncLock _sync
                    Return If(_order.First Is Nothing, Long.MaxValue, _values(_order.First.Value).LastUsed)
                End SyncLock
            End Get
        End Property

        Public Function TrimOldest() As Boolean Implements D3D_IRenderCacheOwner.TrimOldest
            SyncLock _sync
                Return RemoveOldest()
            End SyncLock
        End Function

        Public Sub ReleaseAll() Implements D3D_IRenderCacheOwner.ReleaseAll
            Clear()
        End Sub
    End Class

    Friend Structure Key
        Implements IEquatable(Of Key)
        Public Text As String
        Public Family As String
        Public Size As Single
        Public Style As FontStyle
        Public Unit As GraphicsUnit
        Public Charset As Byte
        Public Vertical As Boolean
        Public Bounds As Size
        Public Flags As TextFormatFlags
        Public Dpi As Single

        Public Overloads Function Equals(other As Key) As Boolean Implements IEquatable(Of Key).Equals
            Return Text = other.Text AndAlso Family = other.Family AndAlso Size = other.Size AndAlso
                Style = other.Style AndAlso Unit = other.Unit AndAlso Charset = other.Charset AndAlso
                Vertical = other.Vertical AndAlso Bounds = other.Bounds AndAlso Flags = other.Flags AndAlso Dpi = other.Dpi
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            Return TypeOf obj Is Key AndAlso Equals(DirectCast(obj, Key))
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return HashCode.Combine(HashCode.Combine(Text, Family, Size, Style, Unit),
                                    HashCode.Combine(Charset, Vertical, Bounds, Flags, Dpi))
        End Function
    End Structure

    Friend Function CreateKey(text As String, font As Font, bounds As Size, flags As TextFormatFlags, dpi As Single) As Key
        Return New Key With {.Text = text, .Family = font.Name, .Size = font.Size,
            .Style = font.Style, .Unit = font.Unit, .Charset = font.GdiCharSet,
            .Vertical = font.GdiVerticalFont, .Bounds = bounds, .Flags = flags, .Dpi = dpi}
    End Function

    Friend Function TryGet(key As Key, ByRef value As SizeF) As Boolean
        SyncLock _sync
            Dim 条目 As CacheEntry = Nothing
            If Not _values.TryGetValue(key, 条目) Then Return False
            _order.Remove(条目.Node)
            _order.AddLast(条目.Node)
            条目.LastUsed = D3D_CpuCache.NextTick()
            value = 条目.Value
            Return True
        End SyncLock
    End Function

    Friend Sub Store(key As Key, value As SizeF)
        If GlobalOptions.CpuCacheBudgetBytes <= 0 Then Return
        SyncLock _sync
            If _values.ContainsKey(key) Then Return
            While _values.Count >= Capacity
                RemoveOldest()
            End While
            Dim 条目 As New CacheEntry With {.Value = value, .Node = _order.AddLast(key),
                .LastUsed = D3D_CpuCache.NextTick(), .Bytes = 160L + 2L * (key.Text.Length + key.Family.Length)}
            _values.Add(key, 条目)
            _bytes += 条目.Bytes
        End SyncLock
        D3D_CpuCache.TrimToBudget()
    End Sub

    Private Function RemoveOldest() As Boolean
        If _order.First Is Nothing Then Return False
        Dim 缓存键 = _order.First.Value
        _bytes -= _values(缓存键).Bytes
        _values.Remove(缓存键)
        _order.RemoveFirst()
        Return True
    End Function

    Friend Sub Clear()
        SyncLock _sync
            _values.Clear()
            _order.Clear()
            _bytes = 0
        End SyncLock
    End Sub
End Module
