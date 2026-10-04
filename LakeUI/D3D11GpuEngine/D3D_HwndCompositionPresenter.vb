Imports System.Numerics
Imports Vortice.Direct2D1
Imports Vortice.DirectComposition
Imports Vortice.DXGI

''' <summary>每控件 HWND 合成表面；按外到内更新后由共享设备一次发布。</summary>
Friend NotInheritable Class D3D_HwndCompositionPresenter
    Implements IDisposable, D3D_IRenderCacheOwner, D3D_IRenderCachePriority

    Private Shared ReadOnly _pending As New HashSet(Of D3D_HwndCompositionPresenter)()
    Private Shared _batchDepth As Integer
    Private ReadOnly _owner As Control
    Private ReadOnly _deviceManager As D3D_DeviceManager
    Private _handle As IntPtr
    Private _size As Size
    Private _surfaceFormat As Format = Format.B8G8R8A8_UNorm
    Private _generation As Integer = -1
    Private _context As ID2D1DeviceContext
    Private _sdrColorTransform As D3D_BitmapColorTransfer
    Private _target As IDCompositionTarget
    Private _visual As IDCompositionVisual
    Private _surface As IDCompositionSurface
    Private _surfaceNeedsBinding As Boolean
    Private _presenting As Boolean
    Private _lastUsed As Long
    Private _presentedSurfaceRevision As Long
    Private _pendingSurfaceRevision As Long
    Private _pendingRenderMilliseconds As Double
    Private _pendingUpdateMilliseconds As Double
    Private _disposed As Boolean

    Friend Property SurfaceProbe As Action(Of ID2D1DeviceContext, ID2D1Bitmap1, Rectangle)

    Friend Sub New(owner As Control, deviceManager As D3D_DeviceManager)
        _owner = owner
        _deviceManager = deviceManager
        D3D_GpuCache.Register(Me)
    End Sub

    Friend Shared Sub BeginBatch()
        _batchDepth += 1
    End Sub

    Friend Shared ReadOnly Property IsBatchActive As Boolean
        Get
            Return _batchDepth > 0
        End Get
    End Property

    Friend Shared Sub EndBatch()
        _batchDepth -= 1
        If _batchDepth = 0 Then CommitPending()
    End Sub

    Private Shared Sub CommitPending()
        If _pending.Count = 0 Then Return
        Dim 开始时间 = D3D_RefreshDiagnostics.Start()
        Try
            D3D_RenderCore.DeviceManager.CompositionDevice.Commit().CheckError()
        Catch 异常 As Exception
            ' 发布失败不能确认修订号；设备故障走现有恢复广播，句柄竞态保留表面重试。
            If D3D_RenderCore.DeviceManager.HandleDeviceLost(异常) Then Return
            For Each 呈现器 In _pending
                呈现器._pendingSurfaceRevision = 0
                D3D_V5Presentation.RetryPresentation(呈现器._owner)
            Next
            _pending.Clear()
            If CUInt(CLng(异常.HResult) And &HFFFFFFFFL) = &H80070005UI Then Return
            Throw
        End Try
        D3D_RefreshDiagnostics.Record(开始时间, "CompositionCommit")
        Dim 发布时间 = Stopwatch.GetTimestamp()
        For Each 呈现器 In _pending
            呈现器._presentedSurfaceRevision = 呈现器._pendingSurfaceRevision
            呈现器._pendingSurfaceRevision = 0
            D3D_RefreshDiagnostics.Record(开始时间, "CompositionPublished", 呈现器._owner)
            D3D_RenderDiagnostics.V5FrameSubmitted(呈现器._pendingRenderMilliseconds,
                呈现器._pendingUpdateMilliseconds, 发布时间, 呈现器._owner)
        Next
        _pending.Clear()
    End Sub

    Private ReadOnly Property CacheBytes As Long Implements D3D_IRenderCacheOwner.CacheBytes
        Get
            Return CLng(_size.Width) * CLng(_size.Height) * If(_surfaceFormat = Format.R16G16B16A16_Float, 8L, 4L)
        End Get
    End Property

    Private ReadOnly Property EvictionPriority As Integer Implements D3D_IRenderCachePriority.EvictionPriority
        Get
            Return 0
        End Get
    End Property

    Private ReadOnly Property OldestUseTick As Long Implements D3D_IRenderCacheOwner.OldestUseTick
        Get
            If _presenting OrElse CacheBytes <= 0 OrElse D3D_ControlTreeWalker.IsEffectivelyVisible(_owner) Then Return Long.MaxValue
            Return If(_lastUsed <= 0, Long.MaxValue - 1, _lastUsed)
        End Get
    End Property

    Private Function TrimOldest() As Boolean Implements D3D_IRenderCacheOwner.TrimOldest
        If _presenting OrElse CacheBytes <= 0 OrElse D3D_ControlTreeWalker.IsEffectivelyVisible(_owner) Then Return False
        释放设备资源()
        Return True
    End Function

    Private Sub ReleaseAllBudgeted() Implements D3D_IRenderCacheOwner.ReleaseAll
        If Not _presenting Then 释放设备资源()
    End Sub

    Friend Function Present(surface As D3D_ControlSurface,
                            renderMilliseconds As Double) As Boolean
        If _disposed OrElse surface Is Nothing OrElse surface.Bitmap Is Nothing Then Return False
        If _owner.IsDisposed OrElse Not _owner.IsHandleCreated OrElse Not D3D_ControlTreeWalker.IsEffectivelyVisible(_owner) Then Return False
        If _owner.ClientSize.Width <= 0 OrElse _owner.ClientSize.Height <= 0 Then Return False
        Prepare()
        Dim 更新开始时间 = Stopwatch.GetTimestamp()
        _presenting = True
        Try
            Dim 更新偏移 As Vortice.Mathematics.Int2
            Dim 更新表面 = _surface.BeginDraw(Of IDXGISurface)(Nothing, 更新偏移)
            Try
                Using 更新表面
                    Dim 位图属性 As New BitmapProperties1(
                        New Vortice.DCommon.PixelFormat(_surfaceFormat, Vortice.DCommon.AlphaMode.Ignore),
                        96.0F, 96.0F, BitmapOptions.Target Or BitmapOptions.CannotDraw)
                    Using 目标位图 = _context.CreateBitmapFromDxgiSurface(更新表面, 位图属性)
                        _context.Target = 目标位图
                        _context.Transform = Matrix3x2.CreateTranslation(更新偏移.X, 更新偏移.Y)
                        _context.BeginDraw()
                        Try
                            Dim 目标区域 As New Vortice.RawRectF(0, 0, _size.Width, _size.Height)
                            Dim 来源区域 As New Vortice.RawRectF(0, 0,
                                surface.LogicalSize.Width * surface.SampleScale,
                                surface.LogicalSize.Height * surface.SampleScale)
                            ' 合成表面可能位于共享纹理中的非零偏移；清除和绘制都限制在本控件范围。
                            _context.PushAxisAlignedClip(目标区域, AntialiasMode.Aliased)
                            Try
                                _context.Clear(New Vortice.Mathematics.Color4(0, 0, 0, 1))
                                If surface.SurfaceFormat = Format.R16G16B16A16_Float AndAlso _surfaceFormat = Format.B8G8R8A8_UNorm Then
                                    DrawSdrSurface(surface.Bitmap, 来源区域)
                                Else
                                    _context.DrawBitmap(surface.Bitmap, 目标区域, 1.0F, InterpolationMode.HighQualityCubic, 来源区域, New Nullable(Of System.Numerics.Matrix4x4)())
                                End If
                            Finally
                                _context.PopAxisAlignedClip()
                            End Try
                        Finally
                            Try
                                _context.EndDraw()
                            Finally
                                _context.Target = Nothing
                            End Try
                        End Try
                        SurfaceProbe?.Invoke(_context, 目标位图, New Rectangle(更新偏移.X, 更新偏移.Y, _size.Width, _size.Height))
                    End Using
                End Using
            Finally
                _context.Target = Nothing
                _surface.EndDraw().CheckError()
            End Try
            If _surfaceNeedsBinding Then
                _visual.SetContent(_surface).CheckError()
                _surfaceNeedsBinding = False
            End If
            _pendingSurfaceRevision = surface.Revision
            _pendingRenderMilliseconds = renderMilliseconds
            _pendingUpdateMilliseconds = Stopwatch.GetElapsedTime(更新开始时间).TotalMilliseconds
            _pending.Add(Me)
            _lastUsed = D3D_GpuCache.NextTick()
            If _batchDepth > 0 Then Return True
            CommitPending()
            Return _presentedSurfaceRevision = surface.Revision
        Finally
            _presenting = False
        End Try
    End Function

    Friend Sub Prepare()
        If _disposed OrElse _owner.IsDisposed OrElse Not _owner.IsHandleCreated Then Return
        Dim 开始时间 = D3D_RefreshDiagnostics.Start()
        _deviceManager.EnsureCreated()
        Dim 设备代次 = _deviceManager.DeviceGeneration
        Dim 窗口句柄 = _owner.Handle
        Dim 目标尺寸 = New Size(Math.Max(1, _owner.ClientSize.Width), Math.Max(1, _owner.ClientSize.Height))
        If _generation <> 设备代次 OrElse _handle <> 窗口句柄 Then
            释放设备资源()
            Try
                Dim 设备 = _deviceManager.CompositionDevice
                _context = _deviceManager.CreateDeviceContext()
                设备.CreateTargetForHwnd(窗口句柄, False, _target).CheckError()
                设备.CreateVisual(_visual).CheckError()
                _target.SetRoot(_visual).CheckError()
                _generation = 设备代次
                _handle = 窗口句柄
            Catch
                释放设备资源()
                Throw
            End Try
        End If
        Dim floatSource = TryCast(_owner, D3D_IFloatPresentationSource)
        Dim requestedFormat = If(floatSource IsNot Nothing AndAlso floatSource.UseFloatPresentation, Format.R16G16B16A16_Float, Format.B8G8R8A8_UNorm)
        If 目标尺寸 <> _size OrElse _surface Is Nothing OrElse requestedFormat <> _surfaceFormat Then
            Dim 新表面 As IDCompositionSurface = Nothing
            _deviceManager.CompositionDevice.CreateSurface(CUInt(目标尺寸.Width), CUInt(目标尺寸.Height),
                requestedFormat, AlphaMode.Ignore, 新表面).CheckError()
            _surfaceFormat = requestedFormat
            ' 新尺寸完成绘制后再替换 visual 内容，失败重试期间保留原来的可见内容。
            _surface?.Dispose()
            _surface = 新表面
            _surfaceNeedsBinding = True
            _pending.Remove(Me)
            _size = 目标尺寸
            _presentedSurfaceRevision = 0
            _pendingSurfaceRevision = 0
            D3D_RenderDiagnostics.V5PresenterRecreate()
        End If
        D3D_RefreshDiagnostics.Record(开始时间, "PresenterResources", _owner)
    End Sub

    Private Sub DrawSdrSurface(bitmap As ID2D1Bitmap1, sourceRegion As Vortice.RawRectF)
        If _sdrColorTransform Is Nothing Then _sdrColorTransform = New D3D_BitmapColorTransfer(_context)
        Dim destination As New Vortice.RawRectF(0, 0, _size.Width, _size.Height)
        _sdrColorTransform.Draw(bitmap, destination, sourceRegion, InterpolationMode.HighQualityCubic, linearOutput:=False)
    End Sub

    Friend Function HasPresented(surface As D3D_ControlSurface) As Boolean
        Return Not _disposed AndAlso surface IsNot Nothing AndAlso surface.Bitmap IsNot Nothing AndAlso
            _surface IsNot Nothing AndAlso _generation = surface.DeviceGeneration AndAlso
            _handle = _owner.Handle AndAlso _size = _owner.ClientSize AndAlso
            (_presentedSurfaceRevision = surface.Revision OrElse _pendingSurfaceRevision = surface.Revision)
    End Function

    Friend Sub HandleDeviceLost()
        释放设备资源()
    End Sub

    Private Sub 释放设备资源()
        _pending.Remove(Me)
        If _context IsNot Nothing Then _context.Target = Nothing
        _surface?.Dispose()
        _visual?.Dispose()
        _target?.Dispose()
        _sdrColorTransform?.Dispose()
        _sdrColorTransform = Nothing
        _context?.Dispose()
        _surface = Nothing
        _surfaceNeedsBinding = False
        _visual = Nothing
        _target = Nothing
        _context = Nothing
        _handle = IntPtr.Zero
        _size = Size.Empty
        _generation = -1
        _presentedSurfaceRevision = 0
        _pendingSurfaceRevision = 0
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        释放设备资源()
    End Sub
End Class
