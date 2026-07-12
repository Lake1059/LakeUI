Public Enum D3DCacheCleanupLevel
    TrimToBudget = 0
    ReleaseVolatileCaches = 1
    ReleaseAllCaches = 2
    ReleaseRenderTargets = 3
    RecreateDevice = 4
    ReleaseEverything = 5
End Enum

Public Module D3D_PaintBridge
    <ThreadStatic>
    Private _backgroundSamplingPaintDepth As Integer
    <ThreadStatic>
    Private _deferredFontRefreshDepth As Integer

    Friend ReadOnly Property IsBackgroundSamplingPaint As Boolean
        Get
            Return _backgroundSamplingPaintDepth > 0
        End Get
    End Property

    Friend Function EnterBackgroundSamplingPaint() As IDisposable
        _backgroundSamplingPaintDepth += 1
        Return New CounterScope(Sub() _backgroundSamplingPaintDepth = Math.Max(0, _backgroundSamplingPaintDepth - 1))
    End Function

    Friend Function EnterDeferredFontRefresh() As IDisposable
        _deferredFontRefreshDepth += 1
        Return New CounterScope(Sub() _deferredFontRefreshDepth = Math.Max(0, _deferredFontRefreshDepth - 1))
    End Function

    Public Sub InvalidateTextFormatCache(control As Control)
        D3D_RenderCore.InvalidateExistingTextResources(control)
    End Sub

    Public Sub RefreshFontDependentRendering(control As Control,
                                              Optional invalidateChildren As Boolean = True,
                                              Optional immediate As Boolean = True)
        InvalidateTextFormatCache(control)
        If control Is Nothing OrElse control.IsDisposed Then Return
        If _deferredFontRefreshDepth > 0 Then immediate = False
        OuterToInnerRefreshScheduler.RequestFull(control, invalidateChildren, immediate)
    End Sub

    Friend Function IsPainting(control As Control) As Boolean
        Return D3D_RenderCore.HasActivePaint(control)
    End Function

    Public Function CleanupD2DResources(level As D3DCacheCleanupLevel,
                                        Optional owner As Control = Nothing,
                                        Optional invalidateAfterCleanup As Boolean = False) As Integer
        Dim targetForm = If(level = D3DCacheCleanupLevel.ReleaseEverything, Nothing, D3D_RenderCore.ResolveCompositorForm(owner))
        Dim hasActivePaint = D3D_RenderCore.HasActivePaint(targetForm)
        Dim cleaned = D3D_RenderCore.CleanupD2DResources(level, targetForm, invalidateAfterCleanup)

        If Not hasActivePaint Then
            D3D_BackgroundPenetration.CleanupD2DResources(level, targetForm)
            D3D_BackdropSurfaceRenderer.CleanupAllD2DResources(level, targetForm)
            MarkdownViewerCore.CleanupAllD2DResources(level, targetForm)

            If level = D3DCacheCleanupLevel.TrimToBudget Then
                D3D_CpuCache.TrimToBudget()
                D3D_GpuCache.TrimToBudget()
            ElseIf level = D3DCacheCleanupLevel.ReleaseEverything Then
                D3D_CpuCache.ReleaseAll()
                D3D_GpuCache.ReleaseAll()
            End If

            If level >= D3DCacheCleanupLevel.RecreateDevice Then
                D3D_DeviceGlobals.InvalidateDevice()
                D3D_RenderCore.InvalidateDeviceForCleanup()
            End If
            If targetForm Is Nothing Then D3D_D2DInterop.CleanupD2DResources(level)
        End If

        Return cleaned
    End Function

    Public Function ResetRenderCore(Optional owner As Control = Nothing,
                                    Optional invalidateAfterCleanup As Boolean = False) As Integer
        Return CleanupD2DResources(D3DCacheCleanupLevel.ReleaseEverything, owner, invalidateAfterCleanup)
    End Function

    Public Function ReleaseImageD2DCache(image As Image,
                                         Optional owner As Control = Nothing,
                                         Optional invalidateAfterCleanup As Boolean = False) As Integer
        Return D3D_RenderCore.ReleaseImageCache(image, owner, invalidateAfterCleanup)
    End Function

    Friend Function BeginGpuPaint(e As PaintEventArgs, control As Control) As D3D_PaintScope
        Return BeginGpuPaint(e, control, TryCast(control, V3_IGpuRenderable))
    End Function

    Friend Function BeginGpuPaint(e As PaintEventArgs,
                                  control As Control,
                                  renderable As V3_IGpuRenderable) As D3D_PaintScope
        If e Is Nothing OrElse control Is Nothing OrElse renderable Is Nothing Then Return Nothing
        Dim compositor = D3D_RenderCore.GetWindowCompositor(control)
        If compositor Is Nothing OrElse compositor.IsDisposed Then Return Nothing
        Dim hdc = e.Graphics.GetHdc()
        Try
            Return New D3D_PaintScope(compositor, e.Graphics, hdc, control, e.ClipRectangle, renderable)
        Catch
            Try : e.Graphics.ReleaseHdc(hdc) : Catch : End Try
            Throw
        End Try
    End Function

    Public Function PaintRenderable(e As PaintEventArgs,
                                    control As Control,
                                    renderable As V3_IGpuRenderable) As Boolean
        If e Is Nothing OrElse control Is Nothing OrElse renderable Is Nothing Then Return False
        If control.IsDisposed OrElse control.Width <= 0 OrElse control.Height <= 0 Then Return False

        Try
            Using scope = BeginGpuPaint(e, control, renderable)
                If scope Is Nothing Then Return False
                Using context = scope.CreateContext()
                    If context Is Nothing Then Return False
                    renderable.RenderGpu(context)
                End Using
            End Using
            Return True
        Catch ex As Exception
            If Not D3D_RenderCore.DeviceManager.HandleDeviceLost(ex) Then Throw
            Return False
        End Try
    End Function

    Private NotInheritable Class CounterScope
        Implements IDisposable

        Private _release As Action

        Friend Sub New(release As Action)
            _release = release
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Dim release = _release
            _release = Nothing
            release?.Invoke()
        End Sub
    End Class
End Module
