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
    Private _standardPaints As Long
    Private _ssaaPaints As Long
    Private _paintTargetCurrentBytes As Long
    Private _paintTargetPeakBytes As Long
    Private _budgetScans As Long
    Private _backgroundTopologyHits As Long
    Private _backgroundTopologyRebuilds As Long
    Private _ssaaTargetAllocations As Long
    Private _ssaaTargetCurrentBytes As Long
    Private _ssaaTargetPeakBytes As Long

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
        Interlocked.Exchange(_standardPaints, 0)
        Interlocked.Exchange(_ssaaPaints, 0)
        Interlocked.Exchange(_paintTargetCurrentBytes, 0)
        Interlocked.Exchange(_paintTargetPeakBytes, 0)
        Interlocked.Exchange(_budgetScans, 0)
        Interlocked.Exchange(_backgroundTopologyHits, 0)
        Interlocked.Exchange(_backgroundTopologyRebuilds, 0)
        Interlocked.Exchange(_ssaaTargetAllocations, 0)
        Interlocked.Exchange(_ssaaTargetCurrentBytes, 0)
        Interlocked.Exchange(_ssaaTargetPeakBytes, 0)
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
            .CacheEvictions = Interlocked.Read(_cacheEvictions),
            .StandardPaints = Interlocked.Read(_standardPaints),
            .SsaaPaints = Interlocked.Read(_ssaaPaints),
            .PaintTargetCurrentBytes = Interlocked.Read(_paintTargetCurrentBytes),
            .PaintTargetPeakBytes = Interlocked.Read(_paintTargetPeakBytes),
            .BudgetScans = Interlocked.Read(_budgetScans),
            .BackgroundTopologyHits = Interlocked.Read(_backgroundTopologyHits),
            .BackgroundTopologyRebuilds = Interlocked.Read(_backgroundTopologyRebuilds),
            .SsaaTargetAllocations = Interlocked.Read(_ssaaTargetAllocations),
            .SsaaTargetCurrentBytes = Interlocked.Read(_ssaaTargetCurrentBytes),
            .SsaaTargetPeakBytes = Interlocked.Read(_ssaaTargetPeakBytes)
        }
    End Function

    Friend Sub PaintTargetPoolHit()
        If _enabled Then Interlocked.Increment(_paintTargetPoolHits)
    End Sub

    Friend Sub PaintTargetPoolAllocation(Optional superSampled As Boolean = False)
        If Not _enabled Then Return
        Interlocked.Increment(_paintTargetPoolAllocations)
        If superSampled Then Interlocked.Increment(_ssaaTargetAllocations)
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

    Friend Sub StandardPaint()
        If _enabled Then Interlocked.Increment(_standardPaints)
    End Sub

    Friend Sub SsaaPaint()
        If _enabled Then Interlocked.Increment(_ssaaPaints)
    End Sub

    Friend Sub PaintTargetBytesChanged(delta As Long, Optional superSampled As Boolean = False)
        If Not _enabled OrElse delta = 0 Then Return
        Dim current = Interlocked.Add(_paintTargetCurrentBytes, delta)
        If current < 0 Then
            Interlocked.Exchange(_paintTargetCurrentBytes, 0)
            current = 0
        End If
        Do
            Dim peak = Interlocked.Read(_paintTargetPeakBytes)
            If current <= peak OrElse Interlocked.CompareExchange(_paintTargetPeakBytes, current, peak) = peak Then Exit Do
        Loop
        If superSampled Then
            Dim ssaaCurrent = Interlocked.Add(_ssaaTargetCurrentBytes, delta)
            If ssaaCurrent < 0 Then
                Interlocked.Exchange(_ssaaTargetCurrentBytes, 0)
                ssaaCurrent = 0
            End If
            Do
                Dim peak = Interlocked.Read(_ssaaTargetPeakBytes)
                If ssaaCurrent <= peak OrElse Interlocked.CompareExchange(_ssaaTargetPeakBytes, ssaaCurrent, peak) = peak Then Exit Do
            Loop
        End If
    End Sub

    Friend Sub BudgetScan()
        If _enabled Then Interlocked.Increment(_budgetScans)
    End Sub

    Friend Sub BackgroundTopologyHit()
        If _enabled Then Interlocked.Increment(_backgroundTopologyHits)
    End Sub

    Friend Sub BackgroundTopologyRebuild()
        If _enabled Then Interlocked.Increment(_backgroundTopologyRebuilds)
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
    Public StandardPaints As Long
    Public SsaaPaints As Long
    Public PaintTargetCurrentBytes As Long
    Public PaintTargetPeakBytes As Long
    Public BudgetScans As Long
    Public BackgroundTopologyHits As Long
    Public BackgroundTopologyRebuilds As Long
    Public SsaaTargetAllocations As Long
    Public SsaaTargetCurrentBytes As Long
    Public SsaaTargetPeakBytes As Long
End Structure
