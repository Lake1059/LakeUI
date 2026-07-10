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
    Private ReadOnly _paintTargets As New Dictionary(Of String, D3D_PaintTargetEntry)(StringComparer.Ordinal)
    Private _paintTargetBytes As Long

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
        D3D_BackdropSurfaceRenderer = New D3D_BackdropRenderer(ImageCache, _deviceManager)
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
    Public ReadOnly Property D3D_BackdropSurfaceRenderer As D3D_BackdropRenderer

    Public Property TextQuality As D3D_TextQualityMode = D3D_TextQualityMode.Auto

    Private ReadOnly Property CacheBytes As Long Implements D3D_IRenderCacheOwner.CacheBytes
        Get
            Return _paintTargetBytes
        End Get
    End Property

    Private ReadOnly Property OldestUseTick As Long Implements D3D_IRenderCacheOwner.OldestUseTick
        Get
            If _paintTargets.Count = 0 Then Return Long.MaxValue
            Return _paintTargets.Values.Min(Function(entry) entry.LastUsed)
        End Get
    End Property

    Private Function TrimOldest() As Boolean Implements D3D_IRenderCacheOwner.TrimOldest
        If _deviceContextInUse OrElse _paintTargets.Count = 0 Then Return False
        Dim victim = _paintTargets.Values.OrderBy(Function(entry) entry.LastUsed).FirstOrDefault()
        If victim Is Nothing Then Return False
        RemovePaintTarget(victim.Key, victim, reportEviction:=True)
        Return True
    End Function

    Private Sub ReleaseAll() Implements D3D_IRenderCacheOwner.ReleaseAll
        ReleasePaintTargetsNoThrow()
    End Sub

    Public Sub HandleDeviceLost()
        If _disposed Then Return

        ReleaseDeviceContextNoThrow()
        ReleasePaintTargetsNoThrow()
        BrushCache.Invalidate()
        GeometryCache.Invalidate()
        TextureCache.ReleaseAll()
        ImageCache.Invalidate()
        TextRenderer.Invalidate()
        D3D_BackdropSurfaceRenderer.Invalidate()
        RequestFullFormRender()
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
        Try : D3D_BackdropSurfaceRenderer.Invalidate() : Catch : End Try
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
        Try : D3D_BackdropSurfaceRenderer.Invalidate() : Catch : End Try
        Try : ReleasePaintTargetsNoThrow() : Catch : End Try
    End Sub

    ''' <summary>
    ''' 租借当前窗体/设备专属的 GDI-compatible target。空闲池每个 64px 桶最多保留一个，
    ''' 因而不改变控件独立绘制模型，也不会累积 N 个控件对应的常驻 target。
    ''' </summary>
    Friend Function RentGpuPaintTarget(context As ID2D1DeviceContext,
                                       width As Integer,
                                       height As Integer,
                                       generation As Integer,
                                       ByRef rentedWidth As Integer,
                                       ByRef rentedHeight As Integer) As ID2D1Bitmap1
        rentedWidth = RoundPaintTargetDimension(width)
        rentedHeight = RoundPaintTargetDimension(height)
        If _disposed OrElse context Is Nothing OrElse generation <> _deviceManager.DeviceGeneration Then Return Nothing

        Dim key = BuildPaintTargetKey(generation, rentedWidth, rentedHeight)
        Dim entry As D3D_PaintTargetEntry = Nothing
        If _paintTargets.TryGetValue(key, entry) Then
            _paintTargets.Remove(key)
            _paintTargetBytes -= entry.Bytes
            If _paintTargetBytes < 0 Then _paintTargetBytes = 0
            D3D_RenderDiagnostics.PaintTargetPoolHit()
            Return entry.Target
        End If

        Dim props As New BitmapProperties1(
            New Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Ignore),
            96.0F,
            96.0F,
            BitmapOptions.Target Or BitmapOptions.GdiCompatible)
        D3D_RenderDiagnostics.PaintTargetPoolAllocation()
        Return context.CreateBitmap(New Vortice.Mathematics.SizeI(rentedWidth, rentedHeight), IntPtr.Zero, 0UI, props)
    End Function

    Friend Sub ReturnGpuPaintTarget(target As ID2D1Bitmap1,
                                    rentedWidth As Integer,
                                    rentedHeight As Integer,
                                    generation As Integer)
        If target Is Nothing Then Return
        If _disposed OrElse generation <> _deviceManager.DeviceGeneration OrElse rentedWidth <= 0 OrElse rentedHeight <= 0 Then
            Try : target.Dispose() : Catch : End Try
            Return
        End If

        Dim key = BuildPaintTargetKey(generation, rentedWidth, rentedHeight)
        Dim existing As D3D_PaintTargetEntry = Nothing
        If _paintTargets.TryGetValue(key, existing) Then RemovePaintTarget(key, existing, reportEviction:=True)

        Dim entry As New D3D_PaintTargetEntry With {
            .Key = key,
            .Target = target,
            .Bytes = CLng(rentedWidth) * CLng(rentedHeight) * 4L,
            .LastUsed = D3D_GpuCache.NextTick()
        }
        _paintTargets(key) = entry
        _paintTargetBytes += entry.Bytes
    End Sub

    Private Shared Function RoundPaintTargetDimension(value As Integer) As Integer
        Const bucket As Integer = 64
        value = Math.Max(1, value)
        Return Math.Max(bucket, CInt(Math.Ceiling(value / CDbl(bucket))) * bucket)
    End Function

    Private Shared Function BuildPaintTargetKey(generation As Integer, width As Integer, height As Integer) As String
        Return generation.ToString(Globalization.CultureInfo.InvariantCulture) & ":" &
               width.ToString(Globalization.CultureInfo.InvariantCulture) & "x" &
               height.ToString(Globalization.CultureInfo.InvariantCulture)
    End Function

    Private Sub ReleasePaintTargetsNoThrow()
        For Each entry In _paintTargets.Values.ToArray()
            Try : entry.Target.Dispose() : Catch : End Try
        Next
        _paintTargets.Clear()
        _paintTargetBytes = 0
    End Sub

    Private Sub RemovePaintTarget(key As String, entry As D3D_PaintTargetEntry, reportEviction As Boolean)
        If entry Is Nothing OrElse Not _paintTargets.Remove(key) Then Return
        _paintTargetBytes -= entry.Bytes
        If _paintTargetBytes < 0 Then _paintTargetBytes = 0
        Try : entry.Target.Dispose() : Catch : End Try
        If reportEviction Then D3D_RenderDiagnostics.PaintTargetPoolEviction()
    End Sub

    Private NotInheritable Class D3D_PaintTargetEntry
        Public Key As String
        Public Target As ID2D1Bitmap1
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
        Try : D3D_BackdropSurfaceRenderer.Dispose() : Catch : End Try
        Try : TextRenderer.Dispose() : Catch : End Try
        Try : ImageCache.Dispose() : Catch : End Try
        Try : TextureCache.Dispose() : Catch : End Try
        Try : GeometryCache.Dispose() : Catch : End Try
        Try : BrushCache.Dispose() : Catch : End Try
        Try : D3D_RenderCore.UnregisterCompositor(_form, Me) : Catch : End Try

        GC.SuppressFinalize(Me)
    End Sub
End Class
