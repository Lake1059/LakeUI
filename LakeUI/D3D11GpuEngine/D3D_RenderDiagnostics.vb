Imports System.Threading

''' <summary>
''' V3 渲染热路径统计。默认关闭；关闭时每个记录点只保留一次布尔判断。
''' 该类型仅供程序集内部诊断和压测使用，不构成控件公开 API。
''' </summary>
Friend Module D3D_RenderDiagnostics
    Private _enabled As Boolean
    Private _paintTargetPoolHits As Long
    Private _paintTargetPoolAllocations As Long
    Private _paintTargetPoolEvictions As Long
    Private _inboundCopyBytes As Long
    Private _outboundCopyBytes As Long
    Private _coverageCopySkips As Long
    Private _backdropCacheHits As Long
    Private _backdropRebuilds As Long
    Private _backgroundPartialUploadBytes As Long
    Private _backgroundFullUploadBytes As Long
    Private _cacheEvictions As Long

    Friend Property Enabled As Boolean
        Get
            Return _enabled
        End Get
        Set(value As Boolean)
            _enabled = value
        End Set
    End Property

    Friend Sub Reset()
        Interlocked.Exchange(_paintTargetPoolHits, 0)
        Interlocked.Exchange(_paintTargetPoolAllocations, 0)
        Interlocked.Exchange(_paintTargetPoolEvictions, 0)
        Interlocked.Exchange(_inboundCopyBytes, 0)
        Interlocked.Exchange(_outboundCopyBytes, 0)
        Interlocked.Exchange(_coverageCopySkips, 0)
        Interlocked.Exchange(_backdropCacheHits, 0)
        Interlocked.Exchange(_backdropRebuilds, 0)
        Interlocked.Exchange(_backgroundPartialUploadBytes, 0)
        Interlocked.Exchange(_backgroundFullUploadBytes, 0)
        Interlocked.Exchange(_cacheEvictions, 0)
    End Sub

    Friend Function Snapshot() As D3D_RenderStatistics
        Return New D3D_RenderStatistics With {
            .PaintTargetPoolHits = Interlocked.Read(_paintTargetPoolHits),
            .PaintTargetPoolAllocations = Interlocked.Read(_paintTargetPoolAllocations),
            .PaintTargetPoolEvictions = Interlocked.Read(_paintTargetPoolEvictions),
            .InboundCopyBytes = Interlocked.Read(_inboundCopyBytes),
            .OutboundCopyBytes = Interlocked.Read(_outboundCopyBytes),
            .CoverageCopySkips = Interlocked.Read(_coverageCopySkips),
            .BackdropCacheHits = Interlocked.Read(_backdropCacheHits),
            .BackdropRebuilds = Interlocked.Read(_backdropRebuilds),
            .BackgroundPartialUploadBytes = Interlocked.Read(_backgroundPartialUploadBytes),
            .BackgroundFullUploadBytes = Interlocked.Read(_backgroundFullUploadBytes),
            .CacheEvictions = Interlocked.Read(_cacheEvictions)
        }
    End Function

    Friend Sub PaintTargetPoolHit()
        If _enabled Then Interlocked.Increment(_paintTargetPoolHits)
    End Sub

    Friend Sub PaintTargetPoolAllocation()
        If _enabled Then Interlocked.Increment(_paintTargetPoolAllocations)
    End Sub

    Friend Sub PaintTargetPoolEviction()
        If _enabled Then Interlocked.Increment(_paintTargetPoolEvictions)
    End Sub

    Friend Sub InboundCopy(bytes As Long)
        If _enabled Then Interlocked.Add(_inboundCopyBytes, Math.Max(0L, bytes))
    End Sub

    Friend Sub OutboundCopy(bytes As Long)
        If _enabled Then Interlocked.Add(_outboundCopyBytes, Math.Max(0L, bytes))
    End Sub

    Friend Sub CoverageCopySkip()
        If _enabled Then Interlocked.Increment(_coverageCopySkips)
    End Sub

    Friend Sub BackdropCacheHit()
        If _enabled Then Interlocked.Increment(_backdropCacheHits)
    End Sub

    Friend Sub BackdropRebuild()
        If _enabled Then Interlocked.Increment(_backdropRebuilds)
    End Sub

    Friend Sub BackgroundPartialUpload(bytes As Long)
        If _enabled Then Interlocked.Add(_backgroundPartialUploadBytes, Math.Max(0L, bytes))
    End Sub

    Friend Sub BackgroundFullUpload(bytes As Long)
        If _enabled Then Interlocked.Add(_backgroundFullUploadBytes, Math.Max(0L, bytes))
    End Sub

    Friend Sub CacheEviction()
        If _enabled Then Interlocked.Increment(_cacheEvictions)
    End Sub
End Module

Friend Structure D3D_RenderStatistics
    Public PaintTargetPoolHits As Long
    Public PaintTargetPoolAllocations As Long
    Public PaintTargetPoolEvictions As Long
    Public InboundCopyBytes As Long
    Public OutboundCopyBytes As Long
    Public CoverageCopySkips As Long
    Public BackdropCacheHits As Long
    Public BackdropRebuilds As Long
    Public BackgroundPartialUploadBytes As Long
    Public BackgroundFullUploadBytes As Long
    Public CacheEvictions As Long
End Structure
