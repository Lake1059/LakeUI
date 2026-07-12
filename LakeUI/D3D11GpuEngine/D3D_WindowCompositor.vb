Imports Microsoft.Win32
Imports Vortice.Direct2D1

''' <summary>
''' Form 级 V3 资源容器。
''' 当前唯一主链路是 WinForms per-control OnPaint + D3D_PaintScope；本类只持有共享缓存、
''' 文字/图片/Backdrop 服务和设备失效协调，不创建 swapchain、不渲染整窗、不管理子 HWND 透明转发。
''' </summary>
Public NotInheritable Class D3D_WindowCompositor
    Implements D3D_IRenderCacheOwner, IDisposable

    Private ReadOnly _form As Form
    Private ReadOnly _deviceManager As D3D_DeviceManager
    Private _deviceContext As ID2D1DeviceContext
    Private _deviceContextGeneration As Integer
    Private _deviceContextInUse As Boolean
    Private _disposed As Boolean
    Private ReadOnly _paintTargets As New List(Of D3D_PaintTargetEntry)()
    Private _paintTargetBytes As Long
    Private _ssaaIdleTimer As Timer

    Public Sub New(form As Form, deviceManager As D3D_DeviceManager)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))
        If deviceManager Is Nothing Then Throw New ArgumentNullException(NameOf(deviceManager))

        _form = form
        _deviceManager = deviceManager

        BrushCache = New D3D_BrushCache()
        GeometryCache = New D3D_GeometryCache(_deviceManager)
        TextureCache = New D3D_TextureCache()
        ImageCache = New D3D_ImageCache(TextureCache)
        TextRenderer = New D3D_TextRenderer(_deviceManager)
        BackdropRenderer = New D3D_BackdropRenderer(ImageCache, _deviceManager)
        D3D_GpuCache.Register(Me)

        AddHandler _form.HandleDestroyed, AddressOf OnFormHandleDestroyed
        AddHandler _form.HandleCreated, AddressOf OnFormHandleCreated
        AddHandler _form.Resize, AddressOf OnFormInvalidatingEvent
        AddHandler _form.DpiChanged, AddressOf OnFormInvalidatingEvent
        AddHandler _form.VisibleChanged, AddressOf OnFormVisibleChanged
        AddHandler _form.Disposed, AddressOf OnFormDisposed
        AddHandler SystemEvents.DisplaySettingsChanged, AddressOf OnDisplaySettingsChanged
    End Sub

    Public ReadOnly Property Form As Form
        Get
            Return _form
        End Get
    End Property

    Public ReadOnly Property IsDisposed As Boolean
        Get
            Return _disposed
        End Get
    End Property

    Public ReadOnly Property IsPainting As Boolean
        Get
            Return _deviceContextInUse
        End Get
    End Property

    Public ReadOnly Property DeviceContext As ID2D1DeviceContext
        Get
            If _disposed Then Return Nothing
            Return _deviceContext
        End Get
    End Property

    Public ReadOnly Property BrushCache As D3D_BrushCache
    Public ReadOnly Property GeometryCache As D3D_GeometryCache
    Public ReadOnly Property TextureCache As D3D_TextureCache
    Public ReadOnly Property ImageCache As D3D_ImageCache
    Public ReadOnly Property TextRenderer As D3D_TextRenderer
    Public ReadOnly Property BackdropRenderer As D3D_BackdropRenderer

    Public Property TextQuality As D3D_TextQualityMode = D3D_TextQualityMode.Auto

    Private ReadOnly Property CacheBytes As Long Implements D3D_IRenderCacheOwner.CacheBytes
        Get
            Return _paintTargetBytes
        End Get
    End Property

    Private ReadOnly Property OldestUseTick As Long Implements D3D_IRenderCacheOwner.OldestUseTick
        Get
            If _paintTargets.Count = 0 Then Return Long.MaxValue
            Return _paintTargets.Min(Function(entry) entry.LastUsed)
        End Get
    End Property

    Private Function TrimOldest() As Boolean Implements D3D_IRenderCacheOwner.TrimOldest
        If _deviceContextInUse OrElse _paintTargets.Count = 0 Then Return False
        Dim victim = _paintTargets.OrderBy(Function(entry) entry.LastUsed).FirstOrDefault()
        If victim Is Nothing Then Return False
        RemovePaintTarget(victim, reportEviction:=True)
        Return True
    End Function

    Private Sub ReleaseAll() Implements D3D_IRenderCacheOwner.ReleaseAll
        ReleasePaintTargetsNoThrow()
    End Sub

    Public Sub HandleDeviceLost()
        HandleDeviceLost(requestRender:=True)
    End Sub

    Friend Sub HandleDeviceLost(requestRender As Boolean)
        If _disposed Then Return

        ReleaseDeviceContextNoThrow()
        ReleasePaintTargetsNoThrow()
        BrushCache.Invalidate()
        GeometryCache.Invalidate()
        TextureCache.ReleaseAll()
        ImageCache.Invalidate()
        TextRenderer.Invalidate()
        BackdropRenderer.Invalidate()
        If requestRender Then RequestFullFormRender()
    End Sub

    Friend Function CleanupD2DResources(level As D3DCacheCleanupLevel) As Boolean
        If _disposed Then Return False
        If _deviceContextInUse Then Return False

        Select Case level
            Case D3DCacheCleanupLevel.TrimToBudget
                Try : TextureCache.TrimToBudget(force:=False) : Catch : End Try

            Case D3DCacheCleanupLevel.ReleaseVolatileCaches
                ReleaseVolatileCachesNoThrow()

            Case D3DCacheCleanupLevel.ReleaseAllCaches
                ReleaseWindowCachesNoThrow(includeGeometry:=True)

            Case D3DCacheCleanupLevel.ReleaseRenderTargets
                ReleaseDeviceContextNoThrow()
                ReleasePaintTargetsNoThrow()
                ReleaseWindowCachesNoThrow(includeGeometry:=False)

            Case D3DCacheCleanupLevel.RecreateDevice
                ReleaseDeviceContextNoThrow()
                ReleasePaintTargetsNoThrow()
                ReleaseWindowCachesNoThrow(includeGeometry:=True)

            Case Else
                Dispose()
        End Select

        Return True
    End Function

    Friend Function ReleaseImageCache(image As Image) As Boolean
        If _disposed OrElse image Is Nothing Then Return False
        If _deviceContextInUse Then Return False
        Return ImageCache.ReleaseImage(image)
    End Function

    Private Sub ReleaseVolatileCachesNoThrow()
        Try : BrushCache.Invalidate() : Catch : End Try
        Try : BackdropRenderer.Invalidate() : Catch : End Try
        Try : TextureCache.TrimToBudget(force:=False) : Catch : End Try
        Try : D3D_GpuCache.TrimToBudget() : Catch : End Try
    End Sub

    Private Sub ReleaseWindowCachesNoThrow(includeGeometry As Boolean)
        Try : BrushCache.Invalidate() : Catch : End Try
        If includeGeometry Then
            Try : GeometryCache.Invalidate() : Catch : End Try
        End If
        Try : TextureCache.ReleaseAll() : Catch : End Try
        Try : ImageCache.Invalidate() : Catch : End Try
        Try : TextRenderer.Invalidate() : Catch : End Try
        Try : BackdropRenderer.Invalidate() : Catch : End Try
        Try : ReleasePaintTargetsNoThrow() : Catch : End Try
    End Sub

    ''' <summary>
    ''' 租借当前窗体/设备专属的 scratch target。1× GDI target 最多保留两张空闲项；
    ''' SSAA target 最多保留一张，并在空闲两秒后主动释放。
    ''' </summary>
    Friend Function RentGpuPaintTarget(context As ID2D1DeviceContext,
                                       width As Integer,
                                       height As Integer,
                                       generation As Integer,
                                       superSampled As Boolean,
                                       ByRef rentedWidth As Integer,
                                       ByRef rentedHeight As Integer) As ID2D1Bitmap1
        rentedWidth = RoundPaintTargetDimension(width)
        rentedHeight = RoundPaintTargetDimension(height)
        If _disposed OrElse context Is Nothing OrElse generation <> _deviceManager.DeviceGeneration Then Return Nothing

        Dim requiredWidth = rentedWidth
        Dim requiredHeight = rentedHeight

        Dim entry = _paintTargets.
            Where(Function(item) item.Generation = generation AndAlso item.SuperSampled = superSampled AndAlso
                                      item.Width >= requiredWidth AndAlso item.Height >= requiredHeight).
            OrderBy(Function(item) CLng(item.Width) * CLng(item.Height)).
            FirstOrDefault()
        If entry IsNot Nothing Then
            _paintTargets.Remove(entry)
            rentedWidth = entry.Width
            rentedHeight = entry.Height
            D3D_RenderDiagnostics.PaintTargetPoolHit()
            Return entry.Target
        End If

        Dim props As New BitmapProperties1(
            New Vortice.DCommon.PixelFormat(
                Vortice.DXGI.Format.B8G8R8A8_UNorm,
                If(superSampled, Vortice.DCommon.AlphaMode.Premultiplied, Vortice.DCommon.AlphaMode.Ignore)),
            96.0F,
            96.0F,
            If(superSampled, BitmapOptions.Target, BitmapOptions.Target Or BitmapOptions.GdiCompatible))
        D3D_RenderDiagnostics.PaintTargetPoolAllocation(superSampled)
        Dim target = context.CreateBitmap(New Vortice.Mathematics.SizeI(rentedWidth, rentedHeight), IntPtr.Zero, 0UI, props)
        Dim targetBytes = EstimatePaintTargetBytes(rentedWidth, rentedHeight)
        _paintTargetBytes += targetBytes
        D3D_RenderDiagnostics.PaintTargetBytesChanged(targetBytes, superSampled)
        D3D_GpuCache.TrimToBudget()
        Return target
    End Function

    Friend Sub ReturnGpuPaintTarget(target As ID2D1Bitmap1,
                                    rentedWidth As Integer,
                                    rentedHeight As Integer,
                                    generation As Integer,
                                    superSampled As Boolean)
        If target Is Nothing Then Return
        If _disposed OrElse generation <> _deviceManager.DeviceGeneration OrElse rentedWidth <= 0 OrElse rentedHeight <= 0 Then
            DiscardGpuPaintTarget(target, rentedWidth, rentedHeight, superSampled)
            Return
        End If

        Dim entry As New D3D_PaintTargetEntry With {
            .Target = target,
            .Width = rentedWidth,
            .Height = rentedHeight,
            .Generation = generation,
            .SuperSampled = superSampled,
            .Bytes = EstimatePaintTargetBytes(rentedWidth, rentedHeight),
            .LastUsed = D3D_GpuCache.NextTick()
        }
        _paintTargets.Add(entry)
        TrimIdlePaintTargets(superSampled)
        If superSampled Then RestartSsaaIdleTimer()
    End Sub

    Friend Sub DiscardGpuPaintTarget(target As ID2D1Bitmap1, rentedWidth As Integer, rentedHeight As Integer, superSampled As Boolean)
        If target Is Nothing Then Return
        Dim bytes = EstimatePaintTargetBytes(rentedWidth, rentedHeight)
        _paintTargetBytes = Math.Max(0L, _paintTargetBytes - bytes)
        D3D_RenderDiagnostics.PaintTargetBytesChanged(-bytes, superSampled)
        Try : target.Dispose() : Catch : End Try
    End Sub

    Private Shared Function RoundPaintTargetDimension(value As Integer) As Integer
        value = Math.Max(1, value)
        Dim bucket = If(value <= 256, 16, If(value <= 1024, 32, 64))
        Return Math.Max(bucket, CInt(Math.Ceiling(value / CDbl(bucket))) * bucket)
    End Function

    Private Shared Function EstimatePaintTargetBytes(width As Integer, height As Integer) As Long
        Return CLng(Math.Max(1, width)) * CLng(Math.Max(1, height)) * 4L
    End Function

    Private Sub ReleasePaintTargetsNoThrow()
        StopSsaaIdleTimer()
        Dim standardReleasedBytes = _paintTargets.Where(Function(entry) Not entry.SuperSampled).Sum(Function(entry) entry.Bytes)
        Dim ssaaReleasedBytes = _paintTargets.Where(Function(entry) entry.SuperSampled).Sum(Function(entry) entry.Bytes)
        Dim releasedBytes = standardReleasedBytes + ssaaReleasedBytes
        For Each entry In _paintTargets.ToArray()
            Try : entry.Target.Dispose() : Catch : End Try
        Next
        _paintTargets.Clear()
        _paintTargetBytes = Math.Max(0L, _paintTargetBytes - releasedBytes)
        D3D_RenderDiagnostics.PaintTargetBytesChanged(-standardReleasedBytes, superSampled:=False)
        D3D_RenderDiagnostics.PaintTargetBytesChanged(-ssaaReleasedBytes, superSampled:=True)
    End Sub

    Private Sub RemovePaintTarget(entry As D3D_PaintTargetEntry, reportEviction As Boolean)
        If entry Is Nothing OrElse Not _paintTargets.Remove(entry) Then Return
        _paintTargetBytes -= entry.Bytes
        If _paintTargetBytes < 0 Then _paintTargetBytes = 0
        Try : entry.Target.Dispose() : Catch : End Try
        If reportEviction Then D3D_RenderDiagnostics.PaintTargetPoolEviction()
        D3D_RenderDiagnostics.PaintTargetBytesChanged(-entry.Bytes, entry.SuperSampled)
    End Sub

    Private Sub TrimIdlePaintTargets(superSampled As Boolean)
        Dim limit = If(superSampled, 1, 2)
        Do
            Dim matching = _paintTargets.Where(Function(item) item.SuperSampled = superSampled).ToArray()
            If matching.Length <= limit Then Exit Do
            RemovePaintTarget(matching.OrderBy(Function(item) item.LastUsed).First(), reportEviction:=True)
        Loop
    End Sub

    Private Sub RestartSsaaIdleTimer()
        If _ssaaIdleTimer Is Nothing Then
            _ssaaIdleTimer = New Timer() With {.Interval = 2000}
            AddHandler _ssaaIdleTimer.Tick, AddressOf OnSsaaIdleTimerTick
        End If
        _ssaaIdleTimer.Stop()
        _ssaaIdleTimer.Start()
    End Sub

    Private Sub StopSsaaIdleTimer()
        If _ssaaIdleTimer Is Nothing Then Return
        _ssaaIdleTimer.Stop()
        RemoveHandler _ssaaIdleTimer.Tick, AddressOf OnSsaaIdleTimerTick
        _ssaaIdleTimer.Dispose()
        _ssaaIdleTimer = Nothing
    End Sub

    Private Sub OnSsaaIdleTimerTick(sender As Object, e As EventArgs)
        If _ssaaIdleTimer IsNot Nothing Then _ssaaIdleTimer.Stop()
        For Each entry In _paintTargets.Where(Function(item) item.SuperSampled).ToArray()
            RemovePaintTarget(entry, reportEviction:=True)
        Next
    End Sub

    Private NotInheritable Class D3D_PaintTargetEntry
        Public Target As ID2D1Bitmap1
        Public Width As Integer
        Public Height As Integer
        Public Generation As Integer
        Public SuperSampled As Boolean
        Public Bytes As Long
        Public LastUsed As Long
    End Class

    Friend Function NotifyDeviceContextException(ex As Exception) As Boolean
        Dim isLost = _deviceManager.HandleDeviceLost(ex)
        If isLost Then ReleaseDeviceContextNoThrow()
        Return isLost
    End Function

    Friend Function AcquireDeviceContext(ByRef ownsContext As Boolean, ByRef deviceGeneration As Integer) As ID2D1DeviceContext
        ownsContext = False
        deviceGeneration = -1
        If _disposed Then Return Nothing

        If _deviceContextInUse Then
            ownsContext = True
            Dim context = _deviceManager.CreateDeviceContext()
            deviceGeneration = _deviceManager.DeviceGeneration
            Return context
        End If

        If _deviceContext IsNot Nothing AndAlso _deviceContextGeneration <> _deviceManager.DeviceGeneration Then
            ReleaseDeviceContextNoThrow()
        End If

        If _deviceContext Is Nothing Then
            _deviceContext = _deviceManager.CreateDeviceContext()
            _deviceContextGeneration = _deviceManager.DeviceGeneration
        End If

        _deviceContextInUse = True
        deviceGeneration = _deviceContextGeneration
        Return _deviceContext
    End Function

    Friend Sub ReleaseDeviceContext(context As ID2D1DeviceContext, ownsContext As Boolean)
        If context Is Nothing Then Return

        If ownsContext Then
            Try : context.Target = Nothing : Catch : End Try
            Try : context.Dispose() : Catch : End Try
            Return
        End If

        If Object.ReferenceEquals(context, _deviceContext) Then
            Try : _deviceContext.Target = Nothing : Catch : End Try
            _deviceContextInUse = False
        End If
    End Sub

    Private Sub ReleaseDeviceContextNoThrow()
        _deviceContextInUse = False
        If _deviceContext IsNot Nothing Then
            Try : _deviceContext.Target = Nothing : Catch : End Try
            Try : _deviceContext.Dispose() : Catch : End Try
            _deviceContext = Nothing
        End If
        _deviceContextGeneration = -1
    End Sub

    Private Sub RequestFullFormRender()
        If _disposed OrElse _form Is Nothing OrElse _form.IsDisposed Then Return

        Try
            If _form.IsHandleCreated Then
                OuterToInnerRefreshScheduler.RequestFull(_form, invalidateChildren:=True)
            Else
                _form.Invalidate(True)
            End If
        Catch
        End Try
    End Sub

    Private Sub OnFormHandleCreated(sender As Object, e As EventArgs)
        RequestFullFormRender()
    End Sub

    Private Sub OnFormHandleDestroyed(sender As Object, e As EventArgs)
        If _form IsNot Nothing AndAlso _form.RecreatingHandle AndAlso Not _form.IsDisposed Then
            HandleDeviceLost()
            Return
        End If

        Dispose()
    End Sub

    Private Sub OnFormInvalidatingEvent(sender As Object, e As EventArgs)
        RequestFullFormRender()
    End Sub

    Private Sub OnFormVisibleChanged(sender As Object, e As EventArgs)
        If _form Is Nothing OrElse _form.IsDisposed OrElse Not _form.Visible Then Return
        RequestFullFormRender()
    End Sub

    Private Sub OnDisplaySettingsChanged(sender As Object, e As EventArgs)
        If _disposed Then Return
        If _form IsNot Nothing AndAlso Not _form.IsDisposed AndAlso _form.IsHandleCreated AndAlso _form.InvokeRequired Then
            Try
                _form.BeginInvoke(CType(Sub() OnDisplaySettingsChanged(sender, e), MethodInvoker))
            Catch
            End Try
            Return
        End If

        HandleDeviceLost()
    End Sub

    Private Sub OnFormDisposed(sender As Object, e As EventArgs)
        Dispose()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True

        Try : RemoveHandler _form.HandleDestroyed, AddressOf OnFormHandleDestroyed : Catch : End Try
        Try : RemoveHandler _form.HandleCreated, AddressOf OnFormHandleCreated : Catch : End Try
        Try : RemoveHandler _form.Resize, AddressOf OnFormInvalidatingEvent : Catch : End Try
        Try : RemoveHandler _form.DpiChanged, AddressOf OnFormInvalidatingEvent : Catch : End Try
        Try : RemoveHandler _form.VisibleChanged, AddressOf OnFormVisibleChanged : Catch : End Try
        Try : RemoveHandler _form.Disposed, AddressOf OnFormDisposed : Catch : End Try
        Try : RemoveHandler SystemEvents.DisplaySettingsChanged, AddressOf OnDisplaySettingsChanged : Catch : End Try

        ReleaseDeviceContextNoThrow()
        ReleasePaintTargetsNoThrow()
        Try : BackdropRenderer.Dispose() : Catch : End Try
        Try : TextRenderer.Dispose() : Catch : End Try
        Try : ImageCache.Dispose() : Catch : End Try
        Try : TextureCache.Dispose() : Catch : End Try
        Try : GeometryCache.Dispose() : Catch : End Try
        Try : BrushCache.Dispose() : Catch : End Try
        Try : D3D_RenderCore.UnregisterCompositor(_form, Me) : Catch : End Try

        GC.SuppressFinalize(Me)
    End Sub
End Class
