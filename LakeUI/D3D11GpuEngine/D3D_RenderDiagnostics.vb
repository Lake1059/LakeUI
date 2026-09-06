Imports System.Diagnostics
Imports System.Drawing
Imports System.Threading

''' <summary>
''' GPU 渲染热路径统计。默认关闭；关闭时每个记录点只保留一次布尔判断。
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
    Private _v5SubmittedFrames As Long
    Private _v5RenderMicrosecondsTotal As Long
    Private _v5PresentMicrosecondsTotal As Long
    Private _v5RenderMicrosecondsPeak As Long
    Private _v5PresentMicrosecondsPeak As Long
    Private _v5DirtyRequestedPixels As Long
    Private _v5FullRequestedPixels As Long
    Private _v5InvisibleSkips As Long
    Private _v5DependencyInvalidations As Long
    Private _v5BatchRequests As Long
    Private _v5BatchFlushes As Long
    Private _v5BatchControls As Long
    Private _v5PartialSurfaceRenders As Long
    Private _v5FullSurfaceRenders As Long
    Private _v5DependencyTopologyHits As Long
    Private _v5DependencyTopologyRebuilds As Long
    Private _v5SurfaceRecreates As Long
    Private _v5PresenterRecreates As Long
    Private _v5DeviceLostCount As Long
    Private _v5FrameLatencySkips As Long
    Private _v5SurfaceCurrentBytes As Long
    Private _v5SurfacePeakBytes As Long
    Private _v5BackdropAttempts As Long
    Private _v5BackdropSuccesses As Long
    Private _v5BackdropCycleRejects As Long
    Private _v5BackdropNonV5Rejects As Long
    Private _v5BackdropSurfaceRejects As Long
    Private _v5CrossFormBackdropSuccesses As Long
    Private _v5CrossFormBackdropAttempts As Long
    Private _v5CrossFormBackdropSurfaceRejects As Long
    Private _v5CrossFormBackdropCoordinateRejects As Long
    Private _v5SurfaceRenderFailures As Long
    Private _v5LastSurfaceRenderFailureControl As String = String.Empty
    Private _v5CrossFormProbeConsumer As Control
    Private _v5CrossFormProbeSource As Control
    Private _v5CrossFormSourceFrames As Long
    Private _v5CrossFormLastSourceDirty As Boolean
    Private _v5CrossFormLastSourceRendering As Boolean
    Private _v5CrossFormLastSourceHasBitmap As Boolean
    Private _v5ChromeOverlayCreated As Long
    Private _v5ChromeOverlayCreateFailures As Long
    Private _v5ChromeOverlayDuplicateSuppressions As Long
    Private _v5ChromeOverlayLayoutUpdates As Long
    Private _v5ChromeOverlayVisibilityChanges As Long
    Private _v5ChromeOverlayDpiUpdates As Long
    Private _v5ChromeOverlayFullscreenLayouts As Long
    Private _v5ChromeOverlayDestroyed As Long
    Private _v5ChromeOverlayFallbackPaints As Long
    Private ReadOnly _v5ProbeLock As New Object()
    Private _v5LastBackdropConsumer As String = String.Empty
    Private _v5LastBackdropSource As String = String.Empty
    Private _v5LastBackdropOffsetX As Integer
    Private _v5LastBackdropOffsetY As Integer
    Private _v5LastBackdropSourceRect As RectangleF
    Private _v5LastBackdropDestinationRect As RectangleF
    Private _v5LastChromeOverlayFailure As String = String.Empty
    Private _v5LastSubmittedControl As String = String.Empty
    Private _v5LastSubmittedSize As Size
    Private _v5ModernTabListFrames As Long
    Private _v5HtmlColorLabelFrames As Long
    Private _v5LastTabListBackArgb As Integer
    Private _v5LastTabListStripArgb As Integer
    Private _v5LastTabListStripRect As Rectangle
    Private _v5LastTabListSelectedIndex As Integer = -1
    Private Const V5IntervalCapacity As Integer = 512
    Private ReadOnly _v5IntervalLock As New Object()
    Private ReadOnly _v5Intervals As Double() = New Double(V5IntervalCapacity - 1) {}
    Private _v5IntervalCount As Integer
    Private _v5IntervalCursor As Integer
    Private _v5LastSubmissionTimestamp As Long

    Friend Property Enabled As Boolean
        Get
            Return _enabled
        End Get
        Set(value As Boolean)
            _enabled = value
            If value Then RefreshSurfaceBytes()
        End Set
    End Property

    Friend Sub RefreshSurfaceBytes()
        Dim current = D3D_ControlSurfaceRegistry.GetAllocatedSurfaceBytes()
        Interlocked.Exchange(_v5SurfaceCurrentBytes, current)
        Interlocked.Exchange(_v5SurfacePeakBytes, current)
    End Sub

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
        Interlocked.Exchange(_v5SubmittedFrames, 0)
        Interlocked.Exchange(_v5RenderMicrosecondsTotal, 0)
        Interlocked.Exchange(_v5PresentMicrosecondsTotal, 0)
        Interlocked.Exchange(_v5RenderMicrosecondsPeak, 0)
        Interlocked.Exchange(_v5PresentMicrosecondsPeak, 0)
        Interlocked.Exchange(_v5DirtyRequestedPixels, 0)
        Interlocked.Exchange(_v5FullRequestedPixels, 0)
        Interlocked.Exchange(_v5InvisibleSkips, 0)
        Interlocked.Exchange(_v5DependencyInvalidations, 0)
        Interlocked.Exchange(_v5BatchRequests, 0)
        Interlocked.Exchange(_v5BatchFlushes, 0)
        Interlocked.Exchange(_v5BatchControls, 0)
        Interlocked.Exchange(_v5PartialSurfaceRenders, 0)
        Interlocked.Exchange(_v5FullSurfaceRenders, 0)
        Interlocked.Exchange(_v5DependencyTopologyHits, 0)
        Interlocked.Exchange(_v5DependencyTopologyRebuilds, 0)
        Interlocked.Exchange(_v5SurfaceRecreates, 0)
        Interlocked.Exchange(_v5PresenterRecreates, 0)
        Interlocked.Exchange(_v5DeviceLostCount, 0)
        Interlocked.Exchange(_v5FrameLatencySkips, 0)
        ' Current surface bytes describe live resources and must remain truthful
        ' across a probe reset. Rebase only the peak to the current allocation.
        RefreshSurfaceBytes()
        Interlocked.Exchange(_v5BackdropAttempts, 0)
        Interlocked.Exchange(_v5BackdropSuccesses, 0)
        Interlocked.Exchange(_v5BackdropCycleRejects, 0)
        Interlocked.Exchange(_v5BackdropNonV5Rejects, 0)
        Interlocked.Exchange(_v5BackdropSurfaceRejects, 0)
        Interlocked.Exchange(_v5CrossFormBackdropSuccesses, 0)
        Interlocked.Exchange(_v5CrossFormBackdropAttempts, 0)
        Interlocked.Exchange(_v5CrossFormBackdropSurfaceRejects, 0)
        Interlocked.Exchange(_v5CrossFormBackdropCoordinateRejects, 0)
        Interlocked.Exchange(_v5SurfaceRenderFailures, 0)
        Interlocked.Exchange(_v5CrossFormSourceFrames, 0)
        Interlocked.Exchange(_v5ChromeOverlayCreated, 0)
        Interlocked.Exchange(_v5ChromeOverlayCreateFailures, 0)
        Interlocked.Exchange(_v5ChromeOverlayDuplicateSuppressions, 0)
        Interlocked.Exchange(_v5ChromeOverlayLayoutUpdates, 0)
        Interlocked.Exchange(_v5ChromeOverlayVisibilityChanges, 0)
        Interlocked.Exchange(_v5ChromeOverlayDpiUpdates, 0)
        Interlocked.Exchange(_v5ChromeOverlayFullscreenLayouts, 0)
        Interlocked.Exchange(_v5ChromeOverlayDestroyed, 0)
        Interlocked.Exchange(_v5ChromeOverlayFallbackPaints, 0)
        SyncLock _v5ProbeLock
            _v5LastBackdropConsumer = String.Empty
            _v5LastBackdropSource = String.Empty
            _v5LastBackdropOffsetX = 0
            _v5LastBackdropOffsetY = 0
            _v5LastBackdropSourceRect = RectangleF.Empty
            _v5LastBackdropDestinationRect = RectangleF.Empty
            _v5LastChromeOverlayFailure = String.Empty
            _v5LastSubmittedControl = String.Empty
            _v5LastSubmittedSize = Size.Empty
            Interlocked.Exchange(_v5ModernTabListFrames, 0)
            Interlocked.Exchange(_v5HtmlColorLabelFrames, 0)
            Interlocked.Exchange(_v5LastTabListBackArgb, 0)
            Interlocked.Exchange(_v5LastTabListStripArgb, 0)
            Interlocked.Exchange(_v5LastTabListSelectedIndex, -1)
            SyncLock _v5ProbeLock
            _v5LastTabListStripRect = Rectangle.Empty
            _v5LastSurfaceRenderFailureControl = String.Empty
            _v5CrossFormProbeConsumer = Nothing
            _v5CrossFormProbeSource = Nothing
            _v5CrossFormLastSourceDirty = False
            _v5CrossFormLastSourceRendering = False
            _v5CrossFormLastSourceHasBitmap = False
            End SyncLock
        End SyncLock
        SyncLock _v5IntervalLock
            Array.Clear(_v5Intervals, 0, _v5Intervals.Length)
            _v5IntervalCount = 0
            _v5IntervalCursor = 0
            _v5LastSubmissionTimestamp = 0
        End SyncLock
    End Sub

    Friend Function Snapshot() As D3D_RenderStatistics
        Dim p50 As Double
        Dim p95 As Double
        Dim p99 As Double
        SyncLock _v5IntervalLock
            If _v5IntervalCount > 0 Then
                Dim samples(_v5IntervalCount - 1) As Double
                Array.Copy(_v5Intervals, samples, _v5IntervalCount)
                Array.Sort(samples)
                p50 = Percentile(samples, 0.5R)
                p95 = Percentile(samples, 0.95R)
                p99 = Percentile(samples, 0.99R)
            End If
        End SyncLock

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
            .SsaaTargetPeakBytes = Interlocked.Read(_ssaaTargetPeakBytes),
            .V5SubmittedFrames = Interlocked.Read(_v5SubmittedFrames),
            .V5RenderMillisecondsTotal = Interlocked.Read(_v5RenderMicrosecondsTotal) / 1000.0R,
            .V5PresentMillisecondsTotal = Interlocked.Read(_v5PresentMicrosecondsTotal) / 1000.0R,
            .V5RenderMillisecondsPeak = Interlocked.Read(_v5RenderMicrosecondsPeak) / 1000.0R,
            .V5PresentMillisecondsPeak = Interlocked.Read(_v5PresentMicrosecondsPeak) / 1000.0R,
            .V5DirtyRequestedPixels = Interlocked.Read(_v5DirtyRequestedPixels),
            .V5FullRequestedPixels = Interlocked.Read(_v5FullRequestedPixels),
            .V5InvisibleSkips = Interlocked.Read(_v5InvisibleSkips),
            .V5DependencyInvalidations = Interlocked.Read(_v5DependencyInvalidations),
            .V5BatchRequests = Interlocked.Read(_v5BatchRequests),
            .V5BatchFlushes = Interlocked.Read(_v5BatchFlushes),
            .V5BatchControls = Interlocked.Read(_v5BatchControls),
            .V5PartialSurfaceRenders = Interlocked.Read(_v5PartialSurfaceRenders),
            .V5FullSurfaceRenders = Interlocked.Read(_v5FullSurfaceRenders),
            .V5DependencyTopologyHits = Interlocked.Read(_v5DependencyTopologyHits),
            .V5DependencyTopologyRebuilds = Interlocked.Read(_v5DependencyTopologyRebuilds),
            .V5SurfaceRecreates = Interlocked.Read(_v5SurfaceRecreates),
            .V5PresenterRecreates = Interlocked.Read(_v5PresenterRecreates),
            .V5DeviceLostCount = Interlocked.Read(_v5DeviceLostCount),
            .V5FrameLatencySkips = Interlocked.Read(_v5FrameLatencySkips),
            .V5FrameIntervalMillisecondsP50 = p50,
            .V5FrameIntervalMillisecondsP95 = p95,
            .V5FrameIntervalMillisecondsP99 = p99
        }
    End Function

    Friend Sub V5DirtyRequested(dirtyPixels As Long, fullPixels As Long)
        If Not _enabled Then Return
        Interlocked.Add(_v5DirtyRequestedPixels, Math.Max(0L, dirtyPixels))
        Interlocked.Add(_v5FullRequestedPixels, Math.Max(0L, fullPixels))
    End Sub

    Friend Sub V5InvisibleSkip()
        If _enabled Then Interlocked.Increment(_v5InvisibleSkips)
    End Sub

    Friend Sub V5DependencyInvalidation()
        If _enabled Then Interlocked.Increment(_v5DependencyInvalidations)
    End Sub

    Friend Sub V5BatchRequested()
        If _enabled Then Interlocked.Increment(_v5BatchRequests)
    End Sub

    Friend Sub V5BatchFlushed(controlCount As Integer)
        If Not _enabled Then Return
        Interlocked.Increment(_v5BatchFlushes)
        Interlocked.Add(_v5BatchControls, Math.Max(0, controlCount))
    End Sub

    Friend Sub V5PartialSurfaceRender()
        If _enabled Then Interlocked.Increment(_v5PartialSurfaceRenders)
    End Sub

    Friend Sub V5FullSurfaceRender()
        If _enabled Then Interlocked.Increment(_v5FullSurfaceRenders)
    End Sub

    Friend Sub V5DependencyTopologyHit()
        If _enabled Then Interlocked.Increment(_v5DependencyTopologyHits)
    End Sub

    Friend Sub V5DependencyTopologyRebuild()
        If _enabled Then Interlocked.Increment(_v5DependencyTopologyRebuilds)
    End Sub

    Friend Sub V5SurfaceRecreate()
        If _enabled Then Interlocked.Increment(_v5SurfaceRecreates)
    End Sub

    Friend Sub V5PresenterRecreate()
        If _enabled Then Interlocked.Increment(_v5PresenterRecreates)
    End Sub

    Friend Sub V5DeviceLost()
        If _enabled Then Interlocked.Increment(_v5DeviceLostCount)
    End Sub

    Friend Sub V5FrameLatencySkip()
        If _enabled Then Interlocked.Increment(_v5FrameLatencySkips)
    End Sub

    Friend Sub V5BackdropAttempt()
        If _enabled Then Interlocked.Increment(_v5BackdropAttempts)
    End Sub

    Friend Sub V5BackdropCycleReject()
        If _enabled Then Interlocked.Increment(_v5BackdropCycleRejects)
    End Sub

    Friend Sub V5BackdropNonV5Reject()
        If _enabled Then Interlocked.Increment(_v5BackdropNonV5Rejects)
    End Sub

    Friend Sub V5BackdropSurfaceReject()
        If _enabled Then Interlocked.Increment(_v5BackdropSurfaceRejects)
    End Sub

    Friend Sub V5BackdropSuccess(consumer As Control,
                                 source As Control,
                                 offset As Point,
                                 sourceRect As RectangleF,
                                 destinationRect As RectangleF)
        If Not _enabled Then Return
        Interlocked.Increment(_v5BackdropSuccesses)
        Try
            If consumer.FindForm() IsNot Nothing AndAlso source.FindForm() IsNot Nothing AndAlso
               Not ReferenceEquals(consumer.FindForm(), source.FindForm()) Then
                Interlocked.Increment(_v5CrossFormBackdropSuccesses)
            End If
        Catch
        End Try
        SyncLock _v5ProbeLock
            _v5LastBackdropConsumer = If(consumer Is Nothing, String.Empty, consumer.GetType().FullName)
            _v5LastBackdropSource = If(source Is Nothing, String.Empty, source.GetType().FullName)
            _v5LastBackdropOffsetX = offset.X
            _v5LastBackdropOffsetY = offset.Y
            _v5LastBackdropSourceRect = sourceRect
            _v5LastBackdropDestinationRect = destinationRect
        End SyncLock
    End Sub

    Friend Sub V5CrossFormBackdropAttempt(consumer As Control, source As Control)
        If Not _enabled Then Return
        Try
            If consumer.FindForm() IsNot Nothing AndAlso source.FindForm() IsNot Nothing AndAlso
               Not ReferenceEquals(consumer.FindForm(), source.FindForm()) Then
                Interlocked.Increment(_v5CrossFormBackdropAttempts)
            End If
        Catch
        End Try
    End Sub

    Friend Sub V5CrossFormBackdropSurfaceReject(consumer As Control, source As Control)
        If Not _enabled Then Return
        Try
            If consumer.FindForm() IsNot Nothing AndAlso source.FindForm() IsNot Nothing AndAlso
               Not ReferenceEquals(consumer.FindForm(), source.FindForm()) Then
                Interlocked.Increment(_v5CrossFormBackdropSurfaceRejects)
            End If
        Catch
        End Try
    End Sub

    Friend Sub V5CrossFormBackdropCoordinateReject(consumer As Control, source As Control)
        If Not _enabled Then Return
        Try
            If consumer.FindForm() IsNot Nothing AndAlso source.FindForm() IsNot Nothing AndAlso
               Not ReferenceEquals(consumer.FindForm(), source.FindForm()) Then
                Interlocked.Increment(_v5CrossFormBackdropCoordinateRejects)
            End If
        Catch
        End Try
    End Sub

    Friend Sub V5SurfaceRenderFailure(control As Control)
        If Not _enabled Then Return
        Interlocked.Increment(_v5SurfaceRenderFailures)
        SyncLock _v5ProbeLock
            _v5LastSurfaceRenderFailureControl = If(control Is Nothing, String.Empty, control.GetType().FullName)
        End SyncLock
    End Sub

    Friend Sub V5SurfaceBytesChanged(delta As Long)
        If Not _enabled Then Return
        Dim current = Interlocked.Add(_v5SurfaceCurrentBytes, delta)
        If current < 0 Then
            Interlocked.Exchange(_v5SurfaceCurrentBytes, 0)
            current = 0
        End If
        Do
            Dim peak = Interlocked.Read(_v5SurfacePeakBytes)
            If current <= peak OrElse Interlocked.CompareExchange(_v5SurfacePeakBytes, current, peak) = peak Then Exit Do
        Loop
    End Sub

    Friend Sub SetV5CrossFormProbePair(consumer As Control, source As Control)
        SyncLock _v5ProbeLock
            _v5CrossFormProbeConsumer = consumer
            _v5CrossFormProbeSource = source
        End SyncLock
    End Sub

    Friend Sub V5CrossFormSourceState(consumer As Control, source As Control,
                                      dirty As Boolean, rendering As Boolean, hasBitmap As Boolean)
        If Not _enabled Then Return
        SyncLock _v5ProbeLock
            If ReferenceEquals(consumer, _v5CrossFormProbeConsumer) AndAlso
               ReferenceEquals(source, _v5CrossFormProbeSource) Then
                _v5CrossFormLastSourceDirty = dirty
                _v5CrossFormLastSourceRendering = rendering
                _v5CrossFormLastSourceHasBitmap = hasBitmap
            End If
        End SyncLock
    End Sub

    Friend Sub V5ChromeOverlayCreated(count As Integer)
        If _enabled Then Interlocked.Add(_v5ChromeOverlayCreated, Math.Max(0, count))
    End Sub

    Friend Sub V5ChromeOverlayCreateFailure(ex As Exception)
        If Not _enabled Then Return
        Interlocked.Increment(_v5ChromeOverlayCreateFailures)
        SyncLock _v5ProbeLock
            _v5LastChromeOverlayFailure = If(ex Is Nothing, String.Empty,
                ex.GetType().FullName & ": " & ex.Message)
        End SyncLock
    End Sub

    Friend Sub V5ChromeOverlayDuplicateSuppressed()
        If _enabled Then Interlocked.Increment(_v5ChromeOverlayDuplicateSuppressions)
    End Sub

    Friend Sub V5ChromeOverlayLayoutUpdated(isFullScreen As Boolean)
        If Not _enabled Then Return
        Interlocked.Increment(_v5ChromeOverlayLayoutUpdates)
        If isFullScreen Then Interlocked.Increment(_v5ChromeOverlayFullscreenLayouts)
    End Sub

    Friend Sub V5ChromeOverlayVisibilityChanged()
        If _enabled Then Interlocked.Increment(_v5ChromeOverlayVisibilityChanges)
    End Sub

    Friend Sub V5ChromeOverlayDpiUpdated()
        If _enabled Then Interlocked.Increment(_v5ChromeOverlayDpiUpdates)
    End Sub

    Friend Sub V5ChromeOverlayDestroyed(count As Integer)
        If _enabled Then Interlocked.Add(_v5ChromeOverlayDestroyed, Math.Max(0, count))
    End Sub

    Friend Sub V5ChromeOverlayFallbackPaint()
        If _enabled Then Interlocked.Increment(_v5ChromeOverlayFallbackPaints)
    End Sub

    Friend Sub V5TabListState(control As Control,
                              backArgb As Integer,
                              stripArgb As Integer,
                              stripRect As Rectangle,
                              selectedIndex As Integer)
        If Not _enabled Then Return
        Interlocked.Exchange(_v5LastTabListBackArgb, backArgb)
        Interlocked.Exchange(_v5LastTabListStripArgb, stripArgb)
        Interlocked.Exchange(_v5LastTabListSelectedIndex, selectedIndex)
        SyncLock _v5ProbeLock
            _v5LastTabListStripRect = stripRect
        End SyncLock
    End Sub

    Friend Function GetV5ProbeSnapshot() As D3D_V5ProbeSnapshot
        Dim result As New D3D_V5ProbeSnapshot With {
            .Enabled = _enabled,
            .BackdropAttempts = Interlocked.Read(_v5BackdropAttempts),
            .BackdropSuccesses = Interlocked.Read(_v5BackdropSuccesses),
            .BackdropCycleRejects = Interlocked.Read(_v5BackdropCycleRejects),
            .BackdropNonV5Rejects = Interlocked.Read(_v5BackdropNonV5Rejects),
            .BackdropSurfaceRejects = Interlocked.Read(_v5BackdropSurfaceRejects),
            .CrossFormBackdropSuccesses = Interlocked.Read(_v5CrossFormBackdropSuccesses),
            .CrossFormBackdropAttempts = Interlocked.Read(_v5CrossFormBackdropAttempts),
            .CrossFormBackdropSurfaceRejects = Interlocked.Read(_v5CrossFormBackdropSurfaceRejects),
            .CrossFormBackdropCoordinateRejects = Interlocked.Read(_v5CrossFormBackdropCoordinateRejects),
            .SurfaceRenderFailures = Interlocked.Read(_v5SurfaceRenderFailures),
            .SurfaceCurrentBytes = Interlocked.Read(_v5SurfaceCurrentBytes),
            .SurfacePeakBytes = Interlocked.Read(_v5SurfacePeakBytes),
            .CrossFormSourceFrames = Interlocked.Read(_v5CrossFormSourceFrames),
            .ChromeOverlayCreated = Interlocked.Read(_v5ChromeOverlayCreated),
            .ChromeOverlayCreateFailures = Interlocked.Read(_v5ChromeOverlayCreateFailures),
            .ChromeOverlayDuplicateSuppressions = Interlocked.Read(_v5ChromeOverlayDuplicateSuppressions),
            .ChromeOverlayLayoutUpdates = Interlocked.Read(_v5ChromeOverlayLayoutUpdates),
            .ChromeOverlayVisibilityChanges = Interlocked.Read(_v5ChromeOverlayVisibilityChanges),
            .ChromeOverlayDpiUpdates = Interlocked.Read(_v5ChromeOverlayDpiUpdates),
            .ChromeOverlayFullscreenLayouts = Interlocked.Read(_v5ChromeOverlayFullscreenLayouts),
            .ChromeOverlayDestroyed = Interlocked.Read(_v5ChromeOverlayDestroyed),
            .ChromeOverlayFallbackPaints = Interlocked.Read(_v5ChromeOverlayFallbackPaints),
            .ModernTabListFrames = Interlocked.Read(_v5ModernTabListFrames),
            .HtmlColorLabelFrames = Interlocked.Read(_v5HtmlColorLabelFrames),
            .Render = Snapshot()
        }
        SyncLock _v5ProbeLock
            result.LastBackdropConsumer = _v5LastBackdropConsumer
            result.LastBackdropSource = _v5LastBackdropSource
            result.LastBackdropOffset = New Point(_v5LastBackdropOffsetX, _v5LastBackdropOffsetY)
            result.LastBackdropSourceRect = _v5LastBackdropSourceRect
            result.LastBackdropDestinationRect = _v5LastBackdropDestinationRect
            result.LastChromeOverlayFailure = _v5LastChromeOverlayFailure
            result.LastSubmittedControl = _v5LastSubmittedControl
            result.LastSubmittedSize = _v5LastSubmittedSize
            result.LastTabListBackColorArgb = _v5LastTabListBackArgb
            result.LastTabListStripColorArgb = _v5LastTabListStripArgb
            result.LastTabListSelectedIndex = _v5LastTabListSelectedIndex
            result.LastTabListStripRect = _v5LastTabListStripRect
            result.LastSurfaceRenderFailureControl = _v5LastSurfaceRenderFailureControl
            result.CrossFormLastSourceDirty = _v5CrossFormLastSourceDirty
            result.CrossFormLastSourceRendering = _v5CrossFormLastSourceRendering
            result.CrossFormLastSourceHasBitmap = _v5CrossFormLastSourceHasBitmap
            result.SubmittedFrames = result.Render.V5SubmittedFrames
            result.RenderMillisecondsPeak = result.Render.V5RenderMillisecondsPeak
            result.PresentMillisecondsPeak = result.Render.V5PresentMillisecondsPeak
            result.DirtyRequestedPixels = result.Render.V5DirtyRequestedPixels
            result.FullRequestedPixels = result.Render.V5FullRequestedPixels
            result.BatchRequests = result.Render.V5BatchRequests
            result.BatchFlushes = result.Render.V5BatchFlushes
            result.BatchControls = result.Render.V5BatchControls
            result.PartialSurfaceRenders = result.Render.V5PartialSurfaceRenders
            result.FullSurfaceRenders = result.Render.V5FullSurfaceRenders
            result.DependencyTopologyHits = result.Render.V5DependencyTopologyHits
            result.DependencyTopologyRebuilds = result.Render.V5DependencyTopologyRebuilds
            result.FrameIntervalP50 = result.Render.V5FrameIntervalMillisecondsP50
            result.FrameIntervalP95 = result.Render.V5FrameIntervalMillisecondsP95
            result.FrameIntervalP99 = result.Render.V5FrameIntervalMillisecondsP99
        End SyncLock
        Return result
    End Function

    Friend Sub V5FrameSubmitted(renderMilliseconds As Double,
                                presentMilliseconds As Double,
                                submissionTimestamp As Long,
                                Optional control As Control = Nothing)
        If Not _enabled Then Return
        Interlocked.Increment(_v5SubmittedFrames)
        SyncLock _v5ProbeLock
            _v5LastSubmittedControl = If(control Is Nothing, String.Empty, control.GetType().FullName)
            _v5LastSubmittedSize = If(control Is Nothing, Size.Empty, control.ClientSize)
        End SyncLock
        If control IsNot Nothing Then
            SyncLock _v5ProbeLock
                If ReferenceEquals(control, _v5CrossFormProbeSource) Then Interlocked.Increment(_v5CrossFormSourceFrames)
            End SyncLock
            Select Case control.GetType().Name
                Case "ModernTabListControl"
                    Interlocked.Increment(_v5ModernTabListFrames)
                Case "HtmlColorLabel"
                    Interlocked.Increment(_v5HtmlColorLabelFrames)
            End Select
        End If
        Dim renderUs = Math.Max(0L, CLng(Math.Round(renderMilliseconds * 1000.0R)))
        Dim presentUs = Math.Max(0L, CLng(Math.Round(presentMilliseconds * 1000.0R)))
        Interlocked.Add(_v5RenderMicrosecondsTotal, renderUs)
        Interlocked.Add(_v5PresentMicrosecondsTotal, presentUs)
        UpdateMaximum(_v5RenderMicrosecondsPeak, renderUs)
        UpdateMaximum(_v5PresentMicrosecondsPeak, presentUs)
        SyncLock _v5IntervalLock
            If _v5LastSubmissionTimestamp > 0 AndAlso submissionTimestamp > _v5LastSubmissionTimestamp Then
                Dim intervalMs = (submissionTimestamp - _v5LastSubmissionTimestamp) * 1000.0R / Stopwatch.Frequency
                If intervalMs >= 0 AndAlso intervalMs <= 10000.0R Then
                    _v5Intervals(_v5IntervalCursor) = intervalMs
                    _v5IntervalCursor = (_v5IntervalCursor + 1) Mod V5IntervalCapacity
                    _v5IntervalCount = Math.Min(V5IntervalCapacity, _v5IntervalCount + 1)
                End If
            End If
            _v5LastSubmissionTimestamp = submissionTimestamp
        End SyncLock
    End Sub

    Private Function Percentile(sortedSamples As Double(), p As Double) As Double
        If sortedSamples Is Nothing OrElse sortedSamples.Length = 0 Then Return 0.0R
        Dim index = CInt(Math.Ceiling(p * sortedSamples.Length)) - 1
        index = Math.Max(0, Math.Min(sortedSamples.Length - 1, index))
        Return sortedSamples(index)
    End Function

    Private Sub UpdateMaximum(ByRef target As Long, value As Long)
        Do
            Dim current = Interlocked.Read(target)
            If value <= current OrElse Interlocked.CompareExchange(target, value, current) = current Then Return
        Loop
    End Sub

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

''' <summary>V5 运行时探针快照；用于自动化验收，不依赖截图或人工视觉判断。</summary>
Public Structure D3D_V5ProbeSnapshot
    Public Enabled As Boolean
    Public BackdropAttempts As Long
    Public BackdropSuccesses As Long
    Public BackdropCycleRejects As Long
    Public BackdropNonV5Rejects As Long
    Public BackdropSurfaceRejects As Long
    Public CrossFormBackdropSuccesses As Long
    Public CrossFormBackdropAttempts As Long
    Public CrossFormBackdropSurfaceRejects As Long
    Public CrossFormBackdropCoordinateRejects As Long
    Public SurfaceRenderFailures As Long
    Public LastSurfaceRenderFailureControl As String
    Public SurfaceCurrentBytes As Long
    Public SurfacePeakBytes As Long
    Public CrossFormSourceFrames As Long
    Public CrossFormLastSourceDirty As Boolean
    Public CrossFormLastSourceRendering As Boolean
    Public CrossFormLastSourceHasBitmap As Boolean
    Public ChromeOverlayCreated As Long
    Public ChromeOverlayCreateFailures As Long
    Public ChromeOverlayDuplicateSuppressions As Long
    Public ChromeOverlayLayoutUpdates As Long
    Public ChromeOverlayVisibilityChanges As Long
    Public ChromeOverlayDpiUpdates As Long
    Public ChromeOverlayFullscreenLayouts As Long
    Public ChromeOverlayDestroyed As Long
    Public ChromeOverlayFallbackPaints As Long
    Public LastBackdropConsumer As String
    Public LastBackdropSource As String
    Public LastBackdropOffset As Point
    Public LastBackdropSourceRect As RectangleF
    Public LastBackdropDestinationRect As RectangleF
    Public LastChromeOverlayFailure As String
    Public LastSubmittedControl As String
    Public LastSubmittedSize As Size
    Public ModernTabListFrames As Long
    Public HtmlColorLabelFrames As Long
    Public LastTabListBackColorArgb As Integer
    Public LastTabListStripColorArgb As Integer
    Public LastTabListSelectedIndex As Integer
    Public LastTabListStripRect As Rectangle
    Public SubmittedFrames As Long
    Public RenderMillisecondsPeak As Double
    Public PresentMillisecondsPeak As Double
    Public DirtyRequestedPixels As Long
    Public FullRequestedPixels As Long
    Public BatchRequests As Long
    Public BatchFlushes As Long
    Public BatchControls As Long
    Public PartialSurfaceRenders As Long
    Public FullSurfaceRenders As Long
    Public DependencyTopologyHits As Long
    Public DependencyTopologyRebuilds As Long
    Public FrameIntervalP50 As Double
    Public FrameIntervalP95 As Double
    Public FrameIntervalP99 As Double
    Friend Render As D3D_RenderStatistics
End Structure

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
    Public V5SubmittedFrames As Long
    Public V5RenderMillisecondsTotal As Double
    Public V5PresentMillisecondsTotal As Double
    Public V5RenderMillisecondsPeak As Double
    Public V5PresentMillisecondsPeak As Double
    Public V5DirtyRequestedPixels As Long
    Public V5FullRequestedPixels As Long
    Public V5InvisibleSkips As Long
    Public V5DependencyInvalidations As Long
    Public V5BatchRequests As Long
    Public V5BatchFlushes As Long
    Public V5BatchControls As Long
    Public V5PartialSurfaceRenders As Long
    Public V5FullSurfaceRenders As Long
    Public V5DependencyTopologyHits As Long
    Public V5DependencyTopologyRebuilds As Long
    Public V5SurfaceRecreates As Long
    Public V5PresenterRecreates As Long
    Public V5DeviceLostCount As Long
    Public V5FrameLatencySkips As Long
    Public V5FrameIntervalMillisecondsP50 As Double
    Public V5FrameIntervalMillisecondsP95 As Double
    Public V5FrameIntervalMillisecondsP99 As Double
End Structure
