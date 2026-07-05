Imports System.Numerics
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices
Imports Vortice.Direct2D1
Imports Vortice.Mathematics

''' <summary>
''' D3D_WindowCompositor 是 Form 级 GPU 资源 owner，不再作为窗口级全树合成器使用。
''' 当前主链路由 WinForms 为每个控件触发 OnPaint，控件通过 D3D_PaintBridge/D3D_PaintScope
''' 在自己的 Paint HDC 上完成 D3D11/D2D1.1 绘制与回贴。
''' 本类只保留跨控件共享的 brush/image/text/geometry/cache、device generation 和设备丢失处理。
''' <para>
''' 资源生命周期：Form 拥有本 compositor；compositor 拥有共享缓存；控件 RenderGpu 只能使用传入 D3D_PaintContext，不持有跨帧 GPU 对象。
''' 该类绑定 device generation，设备丢失时释放窗口级共享 GPU 资源，下一次 OnPaint 按需重建。
''' 驱动更新、TDR、休眠恢复、远程桌面切换或显示适配器重置都按 device lost 处理。
''' 如果设备丢失发生在 BeginDraw/EndDraw/Present 当前帧内部，本类先标记 pending lost，退出 BeginFrame/EndFrame 后再释放资源，
''' 避免在 D2D 调用栈中 Dispose target，同时保证下一帧不会复用旧驱动/旧设备对象。
''' </para>
''' <para>
''' 线程边界：所有访问都必须发生在 UI 线程；普通 WinForms 子控件保持自己的 HWND/WinForms paint 边界。
''' 旧的 swap-chain/render-host/全树遍历方法仅作为暂存代码保留，不在主链路调用。
''' </para>
''' </summary>
Public NotInheritable Class D3D_WindowCompositor
    Implements IDisposable

    Private Shared ReadOnly WindowLevelRenderingEnabled As Boolean = False
    Private ReadOnly _form As Form
    Private ReadOnly _deviceManager As D3D_DeviceManager
    Private _swapChainHost As D3D_SwapChainHost = New D3D_SwapChainHost()
    Private _renderHost As RenderHostControl
    Private ReadOnly _directCompositionHost As New D3D_DirectCompositionHost()
    Private ReadOnly _dirtyRegion As New D3D_DirtyRegionTracker()
    Private ReadOnly _scheduler As D3D_FrameScheduler
    Private _deviceContext As ID2D1DeviceContext
    Private _frameGeneration As Integer
    Private _deviceGeneration As Integer = -1
    Private _inFrame As Boolean
    Private _deviceLostPending As Boolean
    Private _lastFrameDeviceLost As Boolean
    Private _unhandledFrameRecoveryPending As Boolean
    Private ReadOnly _pendingTransparentHwnds As New HashSet(Of Control)()
    Private _disposed As Boolean
    Private _dpiContext As V3_DpiContext

    Public Sub New(form As Form, deviceManager As D3D_DeviceManager)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))
        If deviceManager Is Nothing Then Throw New ArgumentNullException(NameOf(deviceManager))

        _form = form
        _deviceManager = deviceManager
        _scheduler = New D3D_FrameScheduler(Me)
        _dpiContext = V3_DpiContext.FromControl(form)

        BrushCache = New D3D_BrushCache()
        GeometryCache = New D3D_GeometryCache(_deviceManager)
        TextureCache = New D3D_TextureCache()
        ImageCache = New D3D_ImageCache(TextureCache)
        TextRenderer = New D3D_TextRenderer(_deviceManager)
        BackgroundGraph = New D3D_BackgroundGraph(TextureCache, _deviceManager, Me)
        D3D_BackdropSurfaceRenderer = New D3D_BackdropRenderer(ImageCache, _deviceManager)
        FrameGraph = D3D_FrameGraph.CreateDefault()

        AddHandler _form.HandleDestroyed, AddressOf OnFormHandleDestroyed
        AddHandler _form.HandleCreated, AddressOf OnFormHandleCreated
        AddHandler _form.Resize, AddressOf OnFormResize
        AddHandler _form.Disposed, AddressOf OnFormDisposed
        AddHandler _form.DpiChanged, AddressOf OnFormDpiChanged
        AddHandler _form.VisibleChanged, AddressOf OnFormVisibleChanged
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
            Return _inFrame
        End Get
    End Property

    Public ReadOnly Property DeviceContext As ID2D1DeviceContext
        Get
            Return _deviceContext
        End Get
    End Property

    Public ReadOnly Property BrushCache As D3D_BrushCache
    Public ReadOnly Property GeometryCache As D3D_GeometryCache
    Public ReadOnly Property TextureCache As D3D_TextureCache
    Public ReadOnly Property ImageCache As D3D_ImageCache
    Public ReadOnly Property TextRenderer As D3D_TextRenderer
    Public ReadOnly Property BackgroundGraph As D3D_BackgroundGraph
    Public ReadOnly Property D3D_BackdropSurfaceRenderer As D3D_BackdropRenderer
    Public ReadOnly Property FrameGraph As D3D_FrameGraph

    Public Property TextQuality As D3D_TextQualityMode = D3D_TextQualityMode.Auto

    Friend ReadOnly Property RenderTargetSize As System.Drawing.Size
        Get
            If _swapChainHost IsNot Nothing AndAlso _swapChainHost.CurrentSize.Width > 0 AndAlso _swapChainHost.CurrentSize.Height > 0 Then
                Return _swapChainHost.CurrentSize
            End If
            Return GetClientPixelSize()
        End Get
    End Property

    ''' <summary>
    ''' 开始窗口级 GPU 帧：绑定 swap chain target，清理 transient state，设置 DPI/文本质量并 BeginDraw。
    ''' 调用方不得嵌套 BeginFrame，也不得在返回的 D3D_PaintContext 外保存任何 target 相关对象。
    ''' 如果 BeginDraw 后续步骤遇到设备丢失，本方法会中止活动帧、标记 pending lost，并返回 Nothing 让下一帧重建。
    ''' </summary>
    Public Function BeginFrame(Optional explicitDirtyRegion As IReadOnlyList(Of Rectangle) = Nothing) As D3D_PaintContext
        If Not WindowLevelRenderingEnabled Then Return Nothing
        If _disposed Then Return Nothing
        If _form.IsDisposed OrElse Not _form.IsHandleCreated Then Return Nothing
        If _form.WindowState = FormWindowState.Minimized Then Return Nothing
        If _inFrame Then Throw New InvalidOperationException("D3D_WindowCompositor does not allow nested BeginFrame.")
        _lastFrameDeviceLost = False

        Try
            EnsureResources()
            Dim renderSize = RenderTargetSize

            Dim dirty = If(explicitDirtyRegion, _dirtyRegion.SnapshotAndClear())
            _deviceContext.Target = _swapChainHost.TargetBitmap
            _deviceContext.Transform = Matrix3x2.Identity
            _deviceContext.AntialiasMode = AntialiasMode.PerPrimitive
            TextRenderer.ConfigureDeviceContext(_deviceContext, TextQuality, targetHasAlpha:=False)

            _deviceContext.BeginDraw()
            _inFrame = True
            TextureCache.BeginFrameUse()
            _frameGeneration += 1

            Dim clearColor = D3D_PaintContext.ToColor4(_form.BackColor)
            _deviceContext.Clear(clearColor)

            Return New D3D_PaintContext(
                Me,
                _deviceContext,
                Matrix3x2.Identity,
                New RectangleF(0, 0, Math.Max(1, renderSize.Width), Math.Max(1, renderSize.Height)),
                _dpiContext.Scale,
                TextQuality,
                targetHasAlpha:=False,
                _frameGeneration,
                _deviceManager.DeviceGeneration,
                dirty)
        Catch ex As Exception
            If _deviceManager.HandleDeviceLost(ex) Then
                _lastFrameDeviceLost = True
                MarkDeviceLost()
                AbortActiveFrame()
                CompletePendingDeviceLost()
                Return Nothing
            End If

            AbortActiveFrame()
            Throw
        End Try
    End Function

    ''' <summary>
    ''' 结束窗口级 GPU 帧：Dispose frame context、EndDraw、解绑 target，然后按需 Present。控件不能自行提交 Present/Commit。
    ''' 返回 True 只表示本帧已经成功提交到 swap chain；present=False、无活动帧或设备丢失都会返回 False。
    ''' </summary>
    Public Function EndFrame(context As D3D_PaintContext, Optional present As Boolean = True) As Boolean
        If Not _inFrame Then Return False

        Dim framePresented As Boolean = False
        Try
            If context IsNot Nothing Then context.Dispose()
            _deviceContext.EndDraw()

            If present Then framePresented = _swapChainHost.Present()
        Catch ex As Exception
            If _deviceManager.HandleDeviceLost(ex) Then
                _lastFrameDeviceLost = True
                MarkDeviceLost()
            Else
                Throw
            End If
        Finally
            _inFrame = False
            If _deviceContext IsNot Nothing Then
                Try : _deviceContext.Target = Nothing : Catch : End Try
            End If
            TextureCache.EndFrameUse()
            CompletePendingDeviceLost()
        End Try

        Return framePresented AndAlso Not _lastFrameDeviceLost
    End Function

    ''' <summary>
    ''' 便于核心级验证的绘制入口。drawAction 为空时只清空并 Present 一帧，用于证明 swap chain 路线可用且不经过 HDC。
    ''' 如果 drawAction 抛出异常，本方法仍会 EndDraw/解绑 target，但不会 Present 未完成内容。
    ''' </summary>
    Public Function RenderFrame(drawAction As Action(Of D3D_PaintContext)) As Boolean
        Dim context = BeginFrame()
        If context Is Nothing Then Return False

        Dim framePresented As Boolean = False
        Dim shouldPresent As Boolean = True
        Try
            If drawAction IsNot Nothing Then drawAction(context)
        Catch
            shouldPresent = False
            Throw
        Finally
            framePresented = EndFrame(context, present:=shouldPresent)
        End Try
        Return framePresented
    End Function

    ''' <summary>
    ''' 请求当前 Form 进入 WinForms 失效队列。该方法不绘制、不 Present，也不创建 render host。
    ''' </summary>
    Public Sub RequestRender(dirtyRect As Rectangle)
        If _disposed Then Return
        Try
            If _form IsNot Nothing AndAlso Not _form.IsDisposed Then
                If _form.IsHandleCreated Then
                    _form.Invalidate(dirtyRect, True)
                Else
                    _form.Invalidate(True)
                End If
            End If
        Catch
        End Try
    End Sub

    Friend Sub RenderScheduledFrame()
        If _disposed Then Return
        RequestFullWindowRender()
    End Sub

    Friend Sub HandleScheduledFrameException(ex As Exception)
        If _disposed Then Return

        If _deviceManager.HandleDeviceLost(ex) Then
            HandleDeviceLost()
            Return
        End If

        AbortActiveFrame()
        If Not _unhandledFrameRecoveryPending AndAlso CanRequestRecoveryFrame() Then
            _unhandledFrameRecoveryPending = True
            RequestFullWindowRender()
        End If
    End Sub

    Private Function CanRequestRecoveryFrame() As Boolean
        If _disposed OrElse _form Is Nothing OrElse _form.IsDisposed Then Return False
        If Not _form.IsHandleCreated OrElse Not _form.Visible Then Return False
        If _form.WindowState = FormWindowState.Minimized Then Return False
        Return True
    End Function

    Private Sub RequestFullWindowRender()
        If _disposed OrElse _form Is Nothing OrElse _form.IsDisposed Then Return
        RequestRender(New Rectangle(Point.Empty, GetClientPixelSize()))
    End Sub

    ''' <summary>
    ''' 已禁用的旧窗口级全帧入口。控件刷新必须走 V3_InvalidationRouter -> WinForms OnPaint。
    ''' </summary>
    <Obsolete("Window-level V3 frame rendering is disabled. Use per-control OnPaint through D3D_PaintBridge.", True)>
    Public Function RenderWindowFrame() As Boolean
        _pendingTransparentHwnds.Clear()
        RequestFullWindowRender()
        ThisIsYourWindow.NotifyV3FrameNotPresented(_form)
        Return False
    End Function

    Private Sub RenderWindowScene(context As D3D_PaintContext)
        If context Is Nothing Then Return

        RenderWindowBase(context)

        RenderGpuChildren(context, _form, New Rectangle(Point.Empty, RenderTargetSize))
    End Sub

    Friend Function RenderBackgroundSourceSnapshot(context As D3D_PaintContext,
                                                   Optional source As Control = Nothing,
                                                   Optional excludedConsumer As Control = Nothing) As Boolean
        If context Is Nothing Then Return False

        If source Is Nothing Then source = _form
        If source Is Nothing OrElse source.IsDisposed Then Return False
        If source Is excludedConsumer Then Return False

        If source Is _form Then
            RenderWindowBase(context)
            RenderGpuChildrenForSnapshot(context, _form, _form, New Rectangle(Point.Empty, RenderTargetSize), excludedConsumer)
            Return True
        End If

        If source.IsDisposed OrElse Not CanRenderControl(source) Then Return False

        Dim sourceBounds As New Rectangle(Point.Empty, source.Size)
        If sourceBounds.Width <= 0 OrElse sourceBounds.Height <= 0 Then Return False

        If TypeOf source Is V3_IGpuRenderable Then
            RenderGpuControl(context, source, sourceBounds, sourceBounds, enableTransparentHwnd:=False)
            RenderChildSubtreeForSnapshot(context, source, source, sourceBounds, sourceBounds, excludedConsumer)
            Return True
        End If

        If ContainsGpuRenderableDescendant(source) Then
            RenderNativeControlFallback(context, source, sourceBounds, sourceBounds, includeChildren:=False)
            RenderGpuChildrenForSnapshot(context, source, source, sourceBounds, excludedConsumer)
            Return True
        End If

        Return RenderNativeControlFallback(context, source, sourceBounds, sourceBounds, includeChildren:=True)
    End Function

    Private Sub RenderWindowBase(context As D3D_PaintContext)
        If context Is Nothing Then Return

        context.DeviceContext.Clear(D3D_PaintContext.ToColor4(_form.BackColor))

        Dim windowChrome As ThisIsYourWindow = Nothing
        If ThisIsYourWindow.TryGetAttached(_form, windowChrome) AndAlso windowChrome IsNot Nothing Then
            windowChrome.RenderGpuWindow(context, _form)
        End If

        Dim formRenderable = TryCast(_form, V3_IGpuRenderable)
        If formRenderable IsNot Nothing Then
            formRenderable.RenderGpu(context)
        End If
    End Sub

    Private Sub RenderGpuChildren(parentContext As D3D_PaintContext, parent As Control, parentClip As Rectangle)
        If parentContext Is Nothing OrElse parent Is Nothing OrElse parent.IsDisposed Then Return
        If parentClip.Width <= 0 OrElse parentClip.Height <= 0 Then Return

        For i As Integer = parent.Controls.Count - 1 To 0 Step -1
            Dim child = parent.Controls(i)
            If IsRenderHostControl(child) Then Continue For
            If Not CanRenderControl(child) Then Continue For

            Dim childBounds = V3_ControlTreeWalker.GetWindowBounds(child, _form)
            If childBounds.Width <= 0 OrElse childBounds.Height <= 0 Then Continue For

            Dim childClip = Rectangle.Intersect(parentClip, childBounds)
            childClip = Rectangle.Intersect(childClip, New Rectangle(Point.Empty, RenderTargetSize))
            If childClip.Width <= 0 OrElse childClip.Height <= 0 Then Continue For

            If TypeOf child Is V3_IGpuRenderable Then
                RenderGpuControl(parentContext, child, childBounds, childClip)
                RenderChildSubtree(parentContext, child, parentClip, childClip)
            ElseIf ContainsGpuRenderableDescendant(child) Then
                QueueTransparentHwnd(child)
                RenderNativeControlFallback(parentContext, child, childBounds, childClip, includeChildren:=False)
                RenderChildSubtree(parentContext, child, parentClip, childClip)
            ElseIf RenderNativeControlFallback(parentContext, child, childBounds, childClip, includeChildren:=True) Then
                Continue For
            ElseIf child.Controls.Count > 0 Then
                RenderChildSubtree(parentContext, child, parentClip, childClip)
            End If
        Next
    End Sub

    Private Sub RenderChildSubtree(parentContext As D3D_PaintContext,
                                   parent As Control,
                                   inheritedClip As Rectangle,
                                   parentPaintClip As Rectangle)
        If parentContext Is Nothing OrElse parent Is Nothing OrElse parent.IsDisposed Then Return
        If parent.Controls.Count <= 0 Then Return

        Dim clientClip = Rectangle.Intersect(parentPaintClip, V3_ControlTreeWalker.GetWindowClientBounds(parent, _form))
        clientClip = Rectangle.Intersect(clientClip, inheritedClip)
        If clientClip.Width <= 0 OrElse clientClip.Height <= 0 Then Return

        RenderGpuChildren(parentContext, parent, clientClip)
    End Sub

    Private Function CanRenderControl(control As Control) As Boolean
        If control Is Nothing OrElse control.IsDisposed Then Return False
        If control.Width <= 0 OrElse control.Height <= 0 Then Return False

        Dim current As Control = control
        While current IsNot Nothing
            If current.IsDisposed OrElse Not current.Visible Then Return False
            current = current.Parent
        End While

        If _form Is Nothing OrElse _form.IsDisposed Then Return False
        If Not _form.Visible OrElse Not _form.IsHandleCreated Then Return False
        If _form.WindowState = FormWindowState.Minimized Then Return False
        If D3D_RenderCore.ResolveCompositorForm(control) IsNot _form Then Return False
        Return True
    End Function

    Private Sub RenderGpuControl(parentContext As D3D_PaintContext,
                                 control As Control,
                                 bounds As Rectangle,
                                 clippedBounds As Rectangle,
                                 Optional enableTransparentHwnd As Boolean = True)
        Dim renderable = TryCast(control, V3_IGpuRenderable)
        If renderable Is Nothing Then Return
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        If clippedBounds.Width <= 0 OrElse clippedBounds.Height <= 0 Then Return

        If enableTransparentHwnd Then QueueTransparentHwnd(control)

        Dim deviceContext = parentContext.DeviceContext
        Dim previousTransform = deviceContext.Transform
        deviceContext.PushAxisAlignedClip(D3D_PaintContext.ToRawRect(clippedBounds), AntialiasMode.Aliased)
        Try
            Dim transform = Matrix3x2.CreateTranslation(bounds.X, bounds.Y)
            deviceContext.Transform = transform
            Using childContext As New D3D_PaintContext(
                Me,
                deviceContext,
                transform,
                New RectangleF(0, 0, control.Width, control.Height),
                V3_DpiContext.FromControl(control).Scale,
                TextQuality,
                parentContext.TargetHasAlpha,
                parentContext.FrameGeneration,
                parentContext.DeviceGeneration,
                parentContext.DirtyRegion)
                renderable.RenderGpu(childContext)
            End Using
        Finally
            deviceContext.Transform = previousTransform
            deviceContext.PopAxisAlignedClip()
        End Try
    End Sub

    Private Sub RenderGpuChildrenForSnapshot(parentContext As D3D_PaintContext,
                                             parent As Control,
                                             root As Control,
                                             parentClip As Rectangle,
                                             Optional excludedControl As Control = Nothing)
        If parentContext Is Nothing OrElse parent Is Nothing OrElse root Is Nothing Then Return
        If parent.IsDisposed OrElse root.IsDisposed Then Return
        If parentClip.Width <= 0 OrElse parentClip.Height <= 0 Then Return

        Dim excludedBranch = GetDirectChildOnPath(parent, excludedControl)
        Dim excludedBranchIndex As Integer = If(excludedBranch IsNot Nothing, parent.Controls.GetChildIndex(excludedBranch), -1)

        For i As Integer = parent.Controls.Count - 1 To 0 Step -1
            Dim child = parent.Controls(i)
            If IsRenderHostControl(child) Then Continue For
            If child Is excludedBranch Then Continue For
            If excludedBranchIndex >= 0 AndAlso i < excludedBranchIndex Then Continue For
            If child Is excludedControl Then Continue For
            If Not CanRenderControl(child) Then Continue For

            Dim childBounds = GetControlBoundsInRoot(child, root)
            If childBounds.Width <= 0 OrElse childBounds.Height <= 0 Then Continue For

            Dim childClip = Rectangle.Intersect(parentClip, childBounds)
            If childClip.Width <= 0 OrElse childClip.Height <= 0 Then Continue For

            If TypeOf child Is V3_IGpuRenderable Then
                RenderGpuControl(parentContext, child, childBounds, childClip, enableTransparentHwnd:=False)
                RenderChildSubtreeForSnapshot(parentContext, child, root, parentClip, childClip, excludedControl)
            ElseIf ContainsGpuRenderableDescendant(child) Then
                RenderNativeControlFallback(parentContext, child, childBounds, childClip, includeChildren:=False)
                RenderChildSubtreeForSnapshot(parentContext, child, root, parentClip, childClip, excludedControl)
            ElseIf RenderNativeControlFallback(parentContext, child, childBounds, childClip, includeChildren:=True) Then
                Continue For
            ElseIf child.Controls.Count > 0 Then
                RenderChildSubtreeForSnapshot(parentContext, child, root, parentClip, childClip, excludedControl)
            End If
        Next
    End Sub

    Private Sub RenderChildSubtreeForSnapshot(parentContext As D3D_PaintContext,
                                              parent As Control,
                                              root As Control,
                                              inheritedClip As Rectangle,
                                              parentPaintClip As Rectangle,
                                              Optional excludedControl As Control = Nothing)
        If parentContext Is Nothing OrElse parent Is Nothing OrElse root Is Nothing Then Return
        If parent.IsDisposed OrElse root.IsDisposed OrElse parent.Controls.Count <= 0 Then Return

        Dim clientClip = Rectangle.Intersect(parentPaintClip, GetControlClientBoundsInRoot(parent, root))
        clientClip = Rectangle.Intersect(clientClip, inheritedClip)
        If clientClip.Width <= 0 OrElse clientClip.Height <= 0 Then Return

        RenderGpuChildrenForSnapshot(parentContext, parent, root, clientClip, excludedControl)
    End Sub

    Private Shared Function GetDirectChildOnPath(parent As Control, descendant As Control) As Control
        If parent Is Nothing OrElse descendant Is Nothing Then Return Nothing
        If parent.IsDisposed OrElse descendant.IsDisposed Then Return Nothing

        Dim current As Control = descendant
        Dim child As Control = Nothing
        While current IsNot Nothing AndAlso current IsNot parent
            child = current
            current = current.Parent
        End While

        If current Is parent Then Return child
        Return Nothing
    End Function

    Private Shared Function GetControlBoundsInRoot(control As Control, root As Control) As Rectangle
        If control Is Nothing OrElse root Is Nothing OrElse control.IsDisposed OrElse root.IsDisposed Then Return Rectangle.Empty
        Try
            Dim topLeft = root.PointToClient(control.PointToScreen(Point.Empty))
            Return New Rectangle(topLeft, control.Size)
        Catch
        End Try

        Dim layoutTopLeft As Point = Point.Empty
        If TryGetControlLocationInAncestor(control, root, layoutTopLeft) Then
            Return New Rectangle(layoutTopLeft, control.Size)
        End If
        Return Rectangle.Empty
    End Function

    Private Shared Function GetControlClientBoundsInRoot(control As Control, root As Control) As Rectangle
        If control Is Nothing OrElse root Is Nothing OrElse control.IsDisposed OrElse root.IsDisposed Then Return Rectangle.Empty
        Try
            Dim topLeft = root.PointToClient(control.PointToScreen(Point.Empty))
            Return New Rectangle(topLeft, control.ClientSize)
        Catch
        End Try

        Dim layoutTopLeft As Point = Point.Empty
        If TryGetControlLocationInAncestor(control, root, layoutTopLeft) Then
            Return New Rectangle(layoutTopLeft, control.ClientSize)
        End If
        Return Rectangle.Empty
    End Function

    Private Shared Function TryGetControlLocationInAncestor(control As Control, ancestor As Control, ByRef topLeft As Point) As Boolean
        topLeft = Point.Empty
        If control Is Nothing OrElse ancestor Is Nothing Then Return False
        If control.IsDisposed OrElse ancestor.IsDisposed Then Return False

        Dim x As Integer = 0
        Dim y As Integer = 0
        Dim current As Control = control
        While current IsNot Nothing AndAlso current IsNot ancestor
            x += current.Left
            y += current.Top
            current = current.Parent
        End While

        If current IsNot ancestor Then Return False
        topLeft = New Point(x, y)
        Return True
    End Function

    Private Function RenderNativeControlFallback(parentContext As D3D_PaintContext,
                                                 control As Control,
                                                 bounds As Rectangle,
                                                 clippedBounds As Rectangle,
                                                 includeChildren As Boolean) As Boolean
        If parentContext Is Nothing OrElse Not ShouldRenderNativeFallback(control, includeChildren) Then Return False
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return False
        If clippedBounds.Width <= 0 OrElse clippedBounds.Height <= 0 Then Return False

        Try
            parentContext.DeviceContext.PushAxisAlignedClip(D3D_PaintContext.ToRawRect(clippedBounds), AntialiasMode.Aliased)
            Try
                Dim label = TryCast(control, Label)
                If label IsNot Nothing AndAlso RenderNativeLabelFallback(parentContext, label, bounds) Then Return True

                Using snapshot = CaptureNativeControlBitmap(control, includeChildren)
                    If snapshot Is Nothing Then Return False
                    Using gpuBitmap = CreateTransientBitmap(parentContext.DeviceContext, snapshot)
                        If gpuBitmap Is Nothing Then Return False

                        Dim dst As New RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height)
                        Dim d2dDst As Vortice.RawRectF? = D3D_PaintContext.ToRawRect(dst)
                        Dim d2dSrc As Vortice.RawRectF? = New Vortice.RawRectF(0, 0, snapshot.Width, snapshot.Height)
                        parentContext.DeviceContext.DrawBitmap(
                            gpuBitmap,
                            d2dDst,
                            1.0F,
                            InterpolationMode.Linear,
                            d2dSrc,
                            Nothing)
                    End Using
                End Using
            Finally
                parentContext.DeviceContext.PopAxisAlignedClip()
            End Try
            Return True
        Catch
            Return False
        End Try
    End Function

    Private Shared Function CaptureNativeControlBitmap(control As Control, includeChildren As Boolean) As Bitmap
        If control Is Nothing OrElse control.IsDisposed OrElse control.Width <= 0 OrElse control.Height <= 0 Then Return Nothing

        Dim bitmapSize = GetNativeWindowSize(control)
        If bitmapSize.Width <= 0 OrElse bitmapSize.Height <= 0 Then bitmapSize = control.Size
        If bitmapSize.Width <= 0 OrElse bitmapSize.Height <= 0 Then Return Nothing

        Dim snapshot As New Bitmap(bitmapSize.Width, bitmapSize.Height, PixelFormat.Format32bppPArgb)
        snapshot.SetResolution(96.0F, 96.0F)

        If control.IsHandleCreated Then
            If TryCaptureNativeHwnd(control, snapshot, usePrintWindow:=False, includeChildren:=includeChildren) Then Return snapshot
            If includeChildren AndAlso TryCaptureNativeHwnd(control, snapshot, usePrintWindow:=True, includeChildren:=True) Then Return snapshot
        End If

        If Not includeChildren Then
            snapshot.Dispose()
            Return Nothing
        End If

        Try
            Using g = Graphics.FromImage(snapshot)
                g.Clear(System.Drawing.Color.Transparent)
            End Using
            control.DrawToBitmap(snapshot, New Rectangle(Point.Empty, bitmapSize))
            If EnsureOpaqueCapturedBitmap(snapshot) Then Return snapshot
        Catch
        End Try

        snapshot.Dispose()
        Return Nothing
    End Function

    Private Shared Function GetNativeWindowSize(control As Control) As System.Drawing.Size
        If control Is Nothing OrElse control.IsDisposed OrElse Not control.IsHandleCreated Then Return System.Drawing.Size.Empty

        Dim rect As NativeRect
        If Not GetWindowRect(control.Handle, rect) Then Return System.Drawing.Size.Empty

        Dim width = rect.Right - rect.Left
        Dim height = rect.Bottom - rect.Top
        If width <= 0 OrElse height <= 0 Then Return System.Drawing.Size.Empty
        Return New System.Drawing.Size(width, height)
    End Function

    Private Shared Function TryCaptureNativeHwnd(control As Control,
                                                 snapshot As Bitmap,
                                                 usePrintWindow As Boolean,
                                                 includeChildren As Boolean) As Boolean
        If control Is Nothing OrElse snapshot Is Nothing OrElse Not control.IsHandleCreated Then Return False

        Try
            If usePrintWindow Then
                If CapturePrintWindow(control.Handle, snapshot, 0UI) Then Return True
                If CapturePrintWindow(control.Handle, snapshot, PW_RENDERFULLCONTENT) Then Return True
                Return False
            End If

            Using g = Graphics.FromImage(snapshot)
                g.Clear(System.Drawing.Color.Transparent)
                Dim hdc = g.GetHdc()
                Try
                    Dim printFlags = PRF_CLIENT Or PRF_ERASEBKGND Or PRF_NONCLIENT
                    Dim printClientFlags = PRF_CLIENT Or PRF_ERASEBKGND
                    If includeChildren Then
                        printFlags = printFlags Or PRF_CHILDREN
                        printClientFlags = printClientFlags Or PRF_CHILDREN
                    End If
                    Dim flags As IntPtr = New IntPtr(printFlags)
                    SendMessage(control.Handle, WM_PRINT, hdc, flags)
                    SendMessage(control.Handle, WM_PRINTCLIENT, hdc, New IntPtr(printClientFlags))
                Finally
                    g.ReleaseHdc(hdc)
                End Try
            End Using

            Return EnsureOpaqueCapturedBitmap(snapshot)
        Catch
            Return False
        End Try
    End Function

    Private Shared Function CapturePrintWindow(hwnd As IntPtr, snapshot As Bitmap, flags As UInteger) As Boolean
        If hwnd = IntPtr.Zero OrElse snapshot Is Nothing Then Return False

        Try
            Using g = Graphics.FromImage(snapshot)
                g.Clear(System.Drawing.Color.Transparent)
                Dim hdc = g.GetHdc()
                Try
                    If Not PrintWindow(hwnd, hdc, flags) Then Return False
                Finally
                    g.ReleaseHdc(hdc)
                End Try
            End Using

            Return EnsureOpaqueCapturedBitmap(snapshot)
        Catch
            Return False
        End Try
    End Function

    Private Shared Function EnsureOpaqueCapturedBitmap(bitmap As Bitmap) As Boolean
        If bitmap Is Nothing OrElse bitmap.Width <= 0 OrElse bitmap.Height <= 0 Then Return False

        Dim rect As New Rectangle(0, 0, bitmap.Width, bitmap.Height)
        Dim data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb)
        Try
            Dim stride = Math.Abs(data.Stride)
            Dim byteCount = stride * bitmap.Height
            Dim buffer(byteCount - 1) As Byte
            Marshal.Copy(data.Scan0, buffer, 0, byteCount)

            Dim hasContent As Boolean = False
            For y As Integer = 0 To bitmap.Height - 1
                Dim row = y * stride
                For x As Integer = 0 To bitmap.Width - 1
                    Dim i = row + x * 4
                    Dim b = buffer(i)
                    Dim g = buffer(i + 1)
                    Dim r = buffer(i + 2)
                    Dim a = buffer(i + 3)
                    If a <> 0 OrElse r <> 0 OrElse g <> 0 OrElse b <> 0 Then hasContent = True
                    buffer(i + 3) = 255
                Next
            Next

            If hasContent Then Marshal.Copy(buffer, 0, data.Scan0, byteCount)
            Return hasContent
        Finally
            bitmap.UnlockBits(data)
        End Try
    End Function

    Private Shared Function RenderNativeLabelFallback(parentContext As D3D_PaintContext,
                                                      label As Label,
                                                      bounds As Rectangle) As Boolean
        If parentContext Is Nothing OrElse label Is Nothing OrElse label.IsDisposed Then Return False
        If String.IsNullOrEmpty(label.Text) AndAlso (label.BackColor = System.Drawing.Color.Transparent OrElse label.BackColor.A = 0) Then Return False

        Try
            If label.BackColor <> System.Drawing.Color.Transparent AndAlso label.BackColor.A > 0 Then
                parentContext.FillRectangle(New RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), label.BackColor)
            End If

            If Not String.IsNullOrEmpty(label.Text) AndAlso label.Font IsNot Nothing Then
                Dim textRect As New RectangleF(
                    bounds.X + label.Padding.Left,
                    bounds.Y + label.Padding.Top,
                    Math.Max(1, bounds.Width - label.Padding.Horizontal),
                    Math.Max(1, bounds.Height - label.Padding.Vertical))
                Dim hAlign As Vortice.DirectWrite.TextAlignment = Vortice.DirectWrite.TextAlignment.Leading
                Dim vAlign As Vortice.DirectWrite.ParagraphAlignment = Vortice.DirectWrite.ParagraphAlignment.Near
                ResolveContentAlignment(label.TextAlign, hAlign, vAlign)
                Dim textColor = If(label.Enabled, label.ForeColor, SystemColors.GrayText)
                If textColor.A > 0 Then
                    parentContext.DrawText(label.Text, label.Font, textColor, textRect, hAlign, vAlign, wordWrap:=True)
                End If
            End If

            Return True
        Catch
            Return False
        End Try
    End Function

    Private Shared Sub ResolveContentAlignment(alignment As ContentAlignment,
                                               ByRef hAlign As Vortice.DirectWrite.TextAlignment,
                                               ByRef vAlign As Vortice.DirectWrite.ParagraphAlignment)
        Select Case alignment
            Case ContentAlignment.TopCenter, ContentAlignment.MiddleCenter, ContentAlignment.BottomCenter
                hAlign = Vortice.DirectWrite.TextAlignment.Center
            Case ContentAlignment.TopRight, ContentAlignment.MiddleRight, ContentAlignment.BottomRight
                hAlign = Vortice.DirectWrite.TextAlignment.Trailing
            Case Else
                hAlign = Vortice.DirectWrite.TextAlignment.Leading
        End Select

        Select Case alignment
            Case ContentAlignment.MiddleLeft, ContentAlignment.MiddleCenter, ContentAlignment.MiddleRight
                vAlign = Vortice.DirectWrite.ParagraphAlignment.Center
            Case ContentAlignment.BottomLeft, ContentAlignment.BottomCenter, ContentAlignment.BottomRight
                vAlign = Vortice.DirectWrite.ParagraphAlignment.Far
            Case Else
                vAlign = Vortice.DirectWrite.ParagraphAlignment.Near
        End Select
    End Sub

    Private Shared Function ShouldRenderNativeFallback(control As Control, includeChildren As Boolean) As Boolean
        If control Is Nothing OrElse control.IsDisposed Then Return False
        If TypeOf control Is Form Then Return False
        If TypeOf control Is V3_IGpuRenderable Then Return False
        If includeChildren AndAlso ContainsGpuRenderableDescendant(control) Then Return False

        Return True
    End Function

    Private Shared Function ContainsGpuRenderableDescendant(control As Control) As Boolean
        If control Is Nothing OrElse control.IsDisposed Then Return False
        For Each child As Control In control.Controls
            If child Is Nothing OrElse child.IsDisposed Then Continue For
            If IsRenderHostControl(child) Then Continue For
            If TypeOf child Is V3_IGpuRenderable Then Return True
            If ContainsGpuRenderableDescendant(child) Then Return True
        Next
        Return False
    End Function

    Private Sub QueueTransparentHwnd(control As Control)
        If control Is Nothing OrElse control.IsDisposed Then Return
        If IsRenderHostControl(control) Then Return
        _pendingTransparentHwnds.Add(control)
    End Sub

    Private Sub ApplyPendingTransparentHwnds()
        ' The old window-level compositor made native child HWNDs transparent and painted them into
        ' one swap-chain frame. The V3 main path is per-control OnPaint, so ordinary HWND styles are
        ' never changed here.
        _pendingTransparentHwnds.Clear()
    End Sub

    Private Shared Function CreateTransientBitmap(context As ID2D1DeviceContext, bitmap As Bitmap) As ID2D1Bitmap1
        If context Is Nothing OrElse bitmap Is Nothing OrElse bitmap.Width <= 0 OrElse bitmap.Height <= 0 Then Return Nothing

        Dim rect As New Rectangle(0, 0, bitmap.Width, bitmap.Height)
        Dim data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb)
        Try
            Dim props As New BitmapProperties1(
                New Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                96.0F,
                96.0F,
                BitmapOptions.None)

            Return context.CreateBitmap(New SizeI(bitmap.Width, bitmap.Height), data.Scan0, CUInt(data.Stride), props)
        Finally
            bitmap.UnlockBits(data)
        End Try
    End Function

    Friend Function DrawMappedBackgroundSource(context As D3D_PaintContext,
                                               consumer As Control,
                                               source As Control,
                                               destination As RectangleF) As Boolean
        Return BackgroundGraph.DrawMappedBackground(context, consumer, source, destination)
    End Function

    Friend Sub UnregisterBackgroundConsumer(consumer As Control, Optional recursive As Boolean = False)
        If BackgroundGraph Is Nothing Then Return
        BackgroundGraph.UnregisterConsumer(consumer, recursive)
    End Sub

    Friend Sub InvalidateBackgroundSource(source As Control)
        If BackgroundGraph Is Nothing Then Return
        BackgroundGraph.InvalidateSource(source)
    End Sub

    Friend Sub InvalidateBackgroundSource(source As Control, dirtyRect As Rectangle)
        If BackgroundGraph Is Nothing Then Return
        BackgroundGraph.InvalidateSource(source, dirtyRect)
    End Sub

    Friend Sub InvalidateBackgroundSnapshots()
        If BackgroundGraph Is Nothing Then Return
        BackgroundGraph.Invalidate()
    End Sub

    Friend Sub InvalidateSnapshotsForRenderedControl(control As Control, dirtyRect As Rectangle)
        If BackgroundGraph Is Nothing Then Return
        BackgroundGraph.InvalidateSnapshotsForRenderedControl(control, dirtyRect)
    End Sub

    ''' <summary>
    ''' 释放窗口级 target、D2D context 和所有 generation-aware cache。下一帧会从 D3D_DeviceManager 重新获取 device。
    ''' 如果调用发生在帧内，只设置 pending 标记；真正释放延迟到 EndFrame/AbortActiveFrame 后。
    ''' </summary>
    Public Sub HandleDeviceLost()
        MarkDeviceLost()
        If _inFrame Then
            _lastFrameDeviceLost = True
            Return
        End If

        CompletePendingDeviceLost()
    End Sub

    Private Sub MarkDeviceLost()
        _deviceLostPending = True
        _deviceGeneration = -1
    End Sub

    Private Sub CompletePendingDeviceLost()
        If Not _deviceLostPending OrElse _inFrame Then Return
        _deviceLostPending = False

        ReleaseWindowGpuResources(recreateSwapChainHost:=Not _disposed)
        BrushCache.Invalidate()
        GeometryCache.Invalidate()
        TextureCache.ReleaseAll()
        ImageCache.Invalidate()
        TextRenderer.Invalidate()
        BackgroundGraph.Invalidate()
        D3D_BackdropSurfaceRenderer.Invalidate()
        _deviceGeneration = -1

        If Not _disposed AndAlso Not _form.IsDisposed AndAlso _form.IsHandleCreated AndAlso _form.WindowState <> FormWindowState.Minimized Then
            RequestFullWindowRender()
        End If
    End Sub

    Private Sub AbortActiveFrame()
        If Not _inFrame Then Return

        ' BeginDraw 已经成功但 Clear/绘制/设备操作抛错时，必须结束 D2D 帧并解绑 target，避免 compositor 永久停在 in-frame 状态。
        Try
            If _deviceContext IsNot Nothing Then _deviceContext.EndDraw()
        Catch
        End Try

        _inFrame = False
        If _deviceContext IsNot Nothing Then
            Try : _deviceContext.Target = Nothing : Catch : End Try
        End If
        TextureCache.EndFrameUse()
    End Sub

    ''' <summary>
    ''' 调整窗口呈现目标。调用方必须保证当前不在 BeginFrame 内；Resize 只释放 DXGI/D2D target，不走 HDC 重绘。
    ''' ResizeBuffers 遇到驱动更新、TDR、休眠恢复等设备错误时会进入统一 device lost 路径。
    ''' </summary>
    Public Sub ResizeTarget()
        If _disposed OrElse _inFrame OrElse _deviceContext Is Nothing Then Return
        If _form Is Nothing OrElse _form.IsDisposed OrElse Not _form.IsHandleCreated Then Return
        Try : _deviceContext.Target = Nothing : Catch : End Try
        RequestFullWindowRender()
    End Sub

    Private Sub EnsureResources()
        If _deviceContext Is Nothing OrElse _deviceGeneration <> _deviceManager.DeviceGeneration Then
            ReleaseWindowGpuResources(recreateSwapChainHost:=True)
            _deviceContext = _deviceManager.CreateDeviceContext()
            _deviceGeneration = _deviceManager.DeviceGeneration
            BrushCache.Invalidate()
            GeometryCache.Invalidate()
            TextureCache.InvalidateGeneration(_deviceGeneration)
            TextRenderer.Invalidate()
            BackgroundGraph.Invalidate()
        End If

        _dpiContext = V3_DpiContext.FromControl(_form)
        Dim renderSize = GetClientPixelSize()
    End Sub

    Private Function GetClientPixelSize() As System.Drawing.Size
        If _form Is Nothing OrElse _form.IsDisposed Then Return New System.Drawing.Size(1, 1)

        If Not _form.IsHandleCreated Then Return NormalizeSize(_form.ClientSize)

        Dim rect As NativeRect
        If GetClientRect(_form.Handle, rect) Then
            Return NormalizeSize(New System.Drawing.Size(rect.Right - rect.Left, rect.Bottom - rect.Top))
        End If

        Return NormalizeSize(_form.ClientSize)
    End Function

    Private Function GetRenderTargetHwnd(renderSize As System.Drawing.Size) As IntPtr
        Dim host = EnsureRenderHost(renderSize)
        If host Is Nothing OrElse host.IsDisposed OrElse Not host.IsHandleCreated Then
            Throw New InvalidOperationException("D3D render host HWND is not available.")
        End If
        Return host.Handle
    End Function

    Private Function EnsureRenderHost(renderSize As System.Drawing.Size) As RenderHostControl
        If _disposed OrElse _form Is Nothing OrElse _form.IsDisposed OrElse Not _form.IsHandleCreated Then Return Nothing

        If _renderHost Is Nothing OrElse _renderHost.IsDisposed Then
            _renderHost = New RenderHostControl()
            _renderHost.Name = "LakeUI_V3_RenderHost"
            _renderHost.TabStop = False
            _form.Controls.Add(_renderHost)
        ElseIf _renderHost.Parent IsNot _form Then
            _form.Controls.Add(_renderHost)
        End If

        Dim normalized = NormalizeSize(renderSize)
        Dim targetBounds As New Rectangle(0, 0, normalized.Width, normalized.Height)
        If _renderHost.Bounds <> targetBounds Then _renderHost.SetBounds(targetBounds.X, targetBounds.Y, targetBounds.Width, targetBounds.Height)
        If Not _renderHost.Visible Then _renderHost.Visible = True
        If Not _renderHost.Enabled Then _renderHost.Enabled = True
        If Not _renderHost.IsHandleCreated Then _renderHost.CreateControl()
        _renderHost.SendToBack()
        Return _renderHost
    End Function

    Private Shared Function NormalizeSize(size As System.Drawing.Size) As System.Drawing.Size
        Return New System.Drawing.Size(Math.Max(1, size.Width), Math.Max(1, size.Height))
    End Function

    Private Shared Function IsRenderHostControl(control As Control) As Boolean
        Return TypeOf control Is RenderHostControl
    End Function

    Private NotInheritable Class RenderHostControl
        Inherits Control

        Private Const WM_NCHITTEST As Integer = &H84
        Private Const HTTRANSPARENT As Integer = -1
        Private Const WS_CLIPSIBLINGS As Integer = &H4000000
        Private Const WS_CLIPCHILDREN As Integer = &H2000000

        Public Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.Opaque Or
                     ControlStyles.UserPaint Or
                     ControlStyles.ResizeRedraw Or
                     ControlStyles.Selectable, True)
            SetStyle(ControlStyles.Selectable, False)
            UpdateStyles()
        End Sub

        Protected Overrides ReadOnly Property CreateParams As CreateParams
            Get
                Dim cp = MyBase.CreateParams
                cp.Style = cp.Style Or WS_CLIPSIBLINGS Or WS_CLIPCHILDREN
                Return cp
            End Get
        End Property

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
        End Sub

        Protected Overrides Sub WndProc(ByRef m As Message)
            If m.Msg = WM_NCHITTEST Then
                m.Result = New IntPtr(HTTRANSPARENT)
                Return
            End If
            MyBase.WndProc(m)
        End Sub
    End Class

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeRect
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function GetClientRect(hWnd As IntPtr, ByRef lpRect As NativeRect) As Boolean
    End Function

    Private Const WM_PRINT As Integer = &H317
    Private Const WM_PRINTCLIENT As Integer = &H318
    Private Const PRF_CLIENT As Integer = &H4
    Private Const PRF_NONCLIENT As Integer = &H2
    Private Const PRF_ERASEBKGND As Integer = &H8
    Private Const PRF_CHILDREN As Integer = &H10
    Private Const PW_RENDERFULLCONTENT As UInteger = &H2UI

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function PrintWindow(hWnd As IntPtr, hdcBlt As IntPtr, nFlags As UInteger) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function GetWindowRect(hWnd As IntPtr, ByRef lpRect As NativeRect) As Boolean
    End Function

    Private Sub ReleaseWindowGpuResources(recreateSwapChainHost As Boolean)
        If _deviceContext IsNot Nothing Then
            Try : _deviceContext.Target = Nothing : Catch : End Try
        End If

        Try : _swapChainHost.Dispose() : Catch : End Try
        If recreateSwapChainHost Then _swapChainHost = New D3D_SwapChainHost()

        If _deviceContext IsNot Nothing Then
            Try : _deviceContext.Dispose() : Catch : End Try
            _deviceContext = Nothing
        End If

        If _disposed Then ReleaseRenderHost()
    End Sub

    Private Sub ReleaseHwndBoundResources()
        If _inFrame Then
            AbortActiveFrame()
        End If

        If _deviceContext IsNot Nothing Then
            Try : _deviceContext.Target = Nothing : Catch : End Try
        End If

        Try : _swapChainHost.Dispose() : Catch : End Try
        _swapChainHost = New D3D_SwapChainHost()
        _dirtyRegion.Clear()
        _unhandledFrameRecoveryPending = False
        ReleaseRenderHost()
    End Sub

    Private Sub ReleaseRenderHost()
        If _renderHost Is Nothing Then Return
        Try
            If Not _renderHost.IsDisposed Then
                If _renderHost.Parent IsNot Nothing Then _renderHost.Parent.Controls.Remove(_renderHost)
                _renderHost.Dispose()
            End If
        Catch
        End Try
        _renderHost = Nothing
    End Sub

    Private Sub OnFormResize(sender As Object, e As EventArgs)
        ResizeTarget()
    End Sub

    Private Sub OnFormHandleCreated(sender As Object, e As EventArgs)
        If _disposed Then Return
        _dpiContext = V3_DpiContext.FromControl(_form)
        RequestFullWindowRender()
    End Sub

    Private Sub OnFormVisibleChanged(sender As Object, e As EventArgs)
        If _disposed OrElse _form Is Nothing OrElse _form.IsDisposed OrElse Not _form.Visible Then Return
        RequestFullWindowRender()
    End Sub

    Private Sub OnFormDpiChanged(sender As Object, e As EventArgs)
        _dpiContext = V3_DpiContext.FromControl(_form)
        ResizeTarget()
    End Sub

    Private Sub OnFormHandleDestroyed(sender As Object, e As EventArgs)
        If _form IsNot Nothing AndAlso _form.RecreatingHandle AndAlso Not _form.IsDisposed Then
            ReleaseHwndBoundResources()
            Return
        End If

        Dispose()
    End Sub

    Private Sub OnFormDisposed(sender As Object, e As EventArgs)
        Dispose()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True

        RemoveHandler _form.HandleDestroyed, AddressOf OnFormHandleDestroyed
        RemoveHandler _form.HandleCreated, AddressOf OnFormHandleCreated
        RemoveHandler _form.Resize, AddressOf OnFormResize
        RemoveHandler _form.Disposed, AddressOf OnFormDisposed
        RemoveHandler _form.DpiChanged, AddressOf OnFormDpiChanged
        RemoveHandler _form.VisibleChanged, AddressOf OnFormVisibleChanged

        _scheduler.Dispose()
        _directCompositionHost.Dispose()
        MarkDeviceLost()
        CompletePendingDeviceLost()
        D3D_BackdropSurfaceRenderer.Dispose()
        BackgroundGraph.Dispose()
        TextRenderer.Dispose()
        ImageCache.Dispose()
        TextureCache.Dispose()
        GeometryCache.Dispose()
        BrushCache.Dispose()
        D3D_RenderCore.UnregisterCompositor(_form, Me)
        GC.SuppressFinalize(Me)
    End Sub
End Class
