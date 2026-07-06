Imports Microsoft.Win32
Imports Vortice.Direct2D1

''' <summary>
''' Form 级 V3 资源容器。
''' 当前唯一主链路是 WinForms per-control OnPaint + D3D_PaintScope；本类只持有共享缓存、
''' 文字/图片/Backdrop 服务和设备失效协调，不创建 swapchain、不渲染整窗、不管理子 HWND 透明转发。
''' </summary>
Public NotInheritable Class D3D_WindowCompositor
    Implements IDisposable

    Private ReadOnly _form As Form
    Private ReadOnly _deviceManager As D3D_DeviceManager
    Private _deviceContext As ID2D1DeviceContext
    Private _deviceContextGeneration As Integer
    Private _deviceContextInUse As Boolean
    Private _disposed As Boolean

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

    Public Sub HandleDeviceLost()
        If _disposed Then Return

        ReleaseDeviceContextNoThrow()
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
                ReleaseWindowCachesNoThrow(includeGeometry:=False)

            Case D3DCacheCleanupLevel.RecreateDevice
                ReleaseDeviceContextNoThrow()
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
    End Sub

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
