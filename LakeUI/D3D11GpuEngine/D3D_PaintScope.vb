Imports System.Numerics
Imports System.Runtime.InteropServices
Imports Vortice.Direct2D1

''' <summary>
''' 一次 V3 控件 OnPaint 的 GPU 绘制会话。它只持有当前 HDC、脏区、1× scratch target
''' 以及按需创建的整控件 SSAA target；所有资源在本次调用结束时归还窗口 compositor。
''' </summary>
Friend NotInheritable Class D3D_PaintScope
    Implements IDisposable

    Private ReadOnly _compositor As D3D_WindowCompositor
    Private ReadOnly _graphics As Graphics
    Private ReadOnly _hdc As IntPtr
    Private ReadOnly _control As Control
    Private ReadOnly _width As Integer
    Private ReadOnly _height As Integer
    Private ReadOnly _dirtyRect As Rectangle
    Private ReadOnly _ssaaScale As Integer
    Private ReadOnly _coverage As V3_IGpuDirtyRegionCoverage

    Private _deviceContext As ID2D1DeviceContext
    Private _ownsDeviceContext As Boolean
    Private _deviceGeneration As Integer = -1
    Private _baseTarget As ID2D1Bitmap1
    Private _baseTargetWidth As Integer
    Private _baseTargetHeight As Integer
    Private _ssaaTarget As ID2D1Bitmap1
    Private _ssaaTargetWidth As Integer
    Private _ssaaTargetHeight As Integer
    Private _interop As ID2D1GdiInteropRenderTarget
    Private _drawing As Boolean
    Private _clipPushed As Boolean
    Private _targetHealthy As Boolean = True
    Private _textureFrameUseStarted As Boolean
    Private _backdropFrameUseStarted As Boolean
    Private _disposed As Boolean

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeRect
        Public Left, Top, Right, Bottom As Integer
    End Structure

    <DllImport("user32.dll")>
    Private Shared Function GetClientRect(hWnd As IntPtr, ByRef lpRect As NativeRect) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    Private Shared Function GetNativeControlSize(control As Control) As Size
        If control Is Nothing Then Return Size.Empty
        If Not control.IsHandleCreated Then Return control.Size

        Dim rect As NativeRect
        If GetClientRect(control.Handle, rect) Then
            Dim width = Math.Max(0, rect.Right - rect.Left)
            Dim height = Math.Max(0, rect.Bottom - rect.Top)
            If width > 0 AndAlso height > 0 Then Return New Size(width, height)
        End If
        Return control.Size
    End Function

    Friend Sub New(compositor As D3D_WindowCompositor,
                   graphics As Graphics,
                   hdc As IntPtr,
                   control As Control,
                   dirtyRect As Rectangle,
                   renderable As V3_IGpuRenderable)
        _compositor = compositor
        _graphics = graphics
        _hdc = hdc
        _control = control
        Dim nativeSize = GetNativeControlSize(control)
        _width = Math.Max(1, nativeSize.Width)
        _height = Math.Max(1, nativeSize.Height)
        _dirtyRect = NormalizeDirtyRect(dirtyRect, _width, _height)
        _coverage = TryCast(renderable, V3_IGpuDirtyRegionCoverage)

        Dim localScale As Integer = 1
        Dim source = TryCast(renderable, V3_ISuperSamplingSource)
        If source IsNot Nothing Then
            Try : localScale = CInt(source.SuperSamplingScale) : Catch : localScale = 1 : End Try
        End If
        _ssaaScale = Math.Max(1, Math.Min(4, GlobalOptions.GetEffectiveSsaaScale(localScale)))
    End Sub

    Friend Function CreateContext() As D3D_PaintContext
        If _disposed OrElse _deviceContext IsNot Nothing Then Return Nothing
        If _compositor Is Nothing OrElse _compositor.IsDisposed Then Return Nothing

        _deviceContext = _compositor.AcquireDeviceContext(_ownsDeviceContext, _deviceGeneration)
        If _deviceContext Is Nothing Then Return Nothing

        Try
            _baseTarget = _compositor.RentGpuPaintTarget(
                _deviceContext,
                _dirtyRect.Width,
                _dirtyRect.Height,
                _deviceGeneration,
                superSampled:=False,
                _baseTargetWidth,
                _baseTargetHeight)
            If _baseTarget Is Nothing Then Return Nothing

            Dim fullyCovered = CoversDirtyRegion()
            PrepareBaseTarget(fullyCovered)

            Dim renderTarget = _baseTarget
            Dim targetWidth = _baseTargetWidth
            Dim targetHeight = _baseTargetHeight
            Dim transform = Matrix3x2.CreateTranslation(-_dirtyRect.X, -_dirtyRect.Y)

            If _ssaaScale > 1 Then
                Dim requestedWidth = Math.Max(1, _dirtyRect.Width * _ssaaScale)
                Dim requestedHeight = Math.Max(1, _dirtyRect.Height * _ssaaScale)
                _ssaaTarget = _compositor.RentGpuPaintTarget(
                    _deviceContext,
                    requestedWidth,
                    requestedHeight,
                    _deviceGeneration,
                    superSampled:=True,
                    _ssaaTargetWidth,
                    _ssaaTargetHeight)
                If _ssaaTarget Is Nothing Then Throw New InvalidOperationException("SSAA scratch target allocation failed.")
                PrepareSsaaTarget(fullyCovered, requestedWidth, requestedHeight)
                renderTarget = _ssaaTarget
                targetWidth = requestedWidth
                targetHeight = requestedHeight
                transform = Matrix3x2.CreateTranslation(-_dirtyRect.X, -_dirtyRect.Y) * Matrix3x2.CreateScale(_ssaaScale)
                D3D_RenderDiagnostics.SsaaPaint()
            Else
                D3D_RenderDiagnostics.StandardPaint()
            End If

            _deviceContext.PushAxisAlignedClip(New Vortice.RawRectF(0, 0, targetWidth, targetHeight), AntialiasMode.Aliased)
            _clipPushed = True
            _deviceContext.Transform = transform
            Dim targetHasAlpha = _ssaaScale > 1
            _compositor.TextRenderer.ConfigureDeviceContext(_deviceContext, _compositor.TextQuality, targetHasAlpha)

            Dim dpi = V3_DpiContext.FromControl(_control)
            Return New D3D_PaintContext(
                _compositor,
                _deviceContext,
                transform,
                New RectangleF(0, 0, _width, _height),
                dpi.Scale,
                _compositor.TextQuality,
                targetHasAlpha:=targetHasAlpha,
                frameGeneration:=0,
                deviceGeneration:=_deviceGeneration,
                dirtyRectangle:=_dirtyRect,
                beginTextureUse:=AddressOf BeginTextureFrameUse,
                beginBackdropUse:=AddressOf BeginBackdropFrameUse)
        Catch
            _targetHealthy = False
            ReleaseGpuResources()
            Throw
        End Try
    End Function

    Private Function CoversDirtyRegion() As Boolean
        If _coverage Is Nothing Then Return False
        Try
            Dim covered = _coverage.CoversDirtyRegion(_dirtyRect)
            If covered Then D3D_RenderDiagnostics.CoverageCopySkip()
            Return covered
        Catch
            Return False
        End Try
    End Function

    Private Sub PrepareBaseTarget(fullyCovered As Boolean)
        BeginTarget(_baseTarget)
        If fullyCovered Then
            _deviceContext.Clear(New Vortice.Mathematics.Color4(0, 0, 0, 1))
        Else
            CopyDestinationIntoBaseTarget()
        End If
        If _ssaaScale > 1 Then EndTarget()
    End Sub

    Private Sub PrepareSsaaTarget(fullyCovered As Boolean, requestedWidth As Integer, requestedHeight As Integer)
        BeginTarget(_ssaaTarget)
        _deviceContext.Clear(New Vortice.Mathematics.Color4(0, 0, 0, 1))
        If Not fullyCovered Then
            _deviceContext.DrawBitmap(
                _baseTarget,
                New Nullable(Of Vortice.RawRectF)(New Vortice.RawRectF(0, 0, requestedWidth, requestedHeight)),
                1.0F,
                InterpolationMode.Linear,
                New Nullable(Of Vortice.RawRectF)(New Vortice.RawRectF(0, 0, _dirtyRect.Width, _dirtyRect.Height)),
                Nothing)
        End If
    End Sub

    Private Sub BeginTarget(target As ID2D1Bitmap1)
        _deviceContext.Target = target
        _deviceContext.Transform = Matrix3x2.Identity
        _deviceContext.AntialiasMode = AntialiasMode.PerPrimitive
        _deviceContext.BeginDraw()
        _drawing = True
    End Sub

    Private Sub EndTarget()
        If _clipPushed Then
            _deviceContext.PopAxisAlignedClip()
            _clipPushed = False
        End If
        _deviceContext.Transform = Matrix3x2.Identity
        If _drawing Then
            _deviceContext.EndDraw()
            _drawing = False
        End If
    End Sub

    Private Sub CopyDestinationIntoBaseTarget()
        Dim targetHdc As IntPtr = IntPtr.Zero
        Try
            _interop = _deviceContext.QueryInterface(Of ID2D1GdiInteropRenderTarget)()
            targetHdc = _interop.GetDC(DcInitializeMode.Copy)
            If targetHdc = IntPtr.Zero Then Throw New InvalidOperationException("GPU target HDC is unavailable.")
            If Not BitBlt(targetHdc, 0, 0, _dirtyRect.Width, _dirtyRect.Height, _hdc, _dirtyRect.X, _dirtyRect.Y, SRCCOPY) Then
                Throw New InvalidOperationException("Inbound dirty-region copy failed.")
            End If
            D3D_RenderDiagnostics.InboundCopy(CLng(_dirtyRect.Width) * CLng(_dirtyRect.Height) * 4L)
        Finally
            If _interop IsNot Nothing AndAlso targetHdc <> IntPtr.Zero Then
                Try : _interop.ReleaseDC(Nothing) : Catch : End Try
            End If
        End Try
    End Sub

    Private Sub FlushToHdc()
        If _deviceContext Is Nothing OrElse _baseTarget Is Nothing Then Return

        If _ssaaTarget IsNot Nothing Then
            EndTarget()
            BeginTarget(_baseTarget)
            _deviceContext.DrawBitmap(
                _ssaaTarget,
                New Nullable(Of Vortice.RawRectF)(New Vortice.RawRectF(0, 0, _dirtyRect.Width, _dirtyRect.Height)),
                1.0F,
                InterpolationMode.HighQualityCubic,
                New Nullable(Of Vortice.RawRectF)(New Vortice.RawRectF(0, 0, _dirtyRect.Width * _ssaaScale, _dirtyRect.Height * _ssaaScale)),
                Nothing)
        End If

        If _clipPushed Then
            _deviceContext.PopAxisAlignedClip()
            _clipPushed = False
        End If
        _deviceContext.Transform = Matrix3x2.Identity

        Dim sourceHdc As IntPtr = IntPtr.Zero
        Try
            If _interop Is Nothing Then _interop = _deviceContext.QueryInterface(Of ID2D1GdiInteropRenderTarget)()
            sourceHdc = _interop.GetDC(DcInitializeMode.Copy)
            If sourceHdc = IntPtr.Zero Then Throw New InvalidOperationException("GPU output HDC is unavailable.")
            If Not BitBlt(_hdc, _dirtyRect.X, _dirtyRect.Y, _dirtyRect.Width, _dirtyRect.Height, sourceHdc, 0, 0, SRCCOPY) Then
                Throw New InvalidOperationException("Outbound dirty-region copy failed.")
            End If
            D3D_RenderDiagnostics.OutboundCopy(CLng(_dirtyRect.Width) * CLng(_dirtyRect.Height) * 4L)
        Finally
            If _interop IsNot Nothing AndAlso sourceHdc <> IntPtr.Zero Then
                Try : _interop.ReleaseDC(Nothing) : Catch : End Try
            End If
        End Try

        EndTarget()
    End Sub

    Private Sub BeginTextureFrameUse()
        If _textureFrameUseStarted Then Return
        _compositor.TextureCache.BeginFrameUse()
        _textureFrameUseStarted = True
    End Sub

    Private Sub BeginBackdropFrameUse()
        If _backdropFrameUseStarted Then Return
        _compositor.BackdropRenderer.BeginFrameUse()
        _backdropFrameUseStarted = True
    End Sub

    Private Sub ReleaseGpuResources()
        If _clipPushed AndAlso _deviceContext IsNot Nothing Then
            Try : _deviceContext.PopAxisAlignedClip() : Catch : End Try
            _clipPushed = False
        End If
        If _drawing AndAlso _deviceContext IsNot Nothing Then
            Try : _deviceContext.EndDraw() : Catch : _targetHealthy = False : End Try
            _drawing = False
        End If
        If _deviceContext IsNot Nothing Then
            Try : _deviceContext.Transform = Matrix3x2.Identity : Catch : End Try
            Try : _deviceContext.Target = Nothing : Catch : End Try
        End If
        If _interop IsNot Nothing Then
            Try : _interop.Dispose() : Catch : End Try
            _interop = Nothing
        End If

        ReturnTarget(_ssaaTarget, _ssaaTargetWidth, _ssaaTargetHeight, superSampled:=True)
        _ssaaTarget = Nothing
        ReturnTarget(_baseTarget, _baseTargetWidth, _baseTargetHeight, superSampled:=False)
        _baseTarget = Nothing

        If _backdropFrameUseStarted Then
            Try : _compositor.BackdropRenderer.EndFrameUse() : Catch : End Try
            _backdropFrameUseStarted = False
        End If
        If _textureFrameUseStarted Then
            Try : _compositor.TextureCache.EndFrameUse() : Catch : End Try
            _textureFrameUseStarted = False
        End If
        If _deviceContext IsNot Nothing Then
            Try : _compositor.ReleaseDeviceContext(_deviceContext, _ownsDeviceContext) : Catch : End Try
        End If
        _deviceContext = Nothing
    End Sub

    Private Sub ReturnTarget(target As ID2D1Bitmap1, width As Integer, height As Integer, superSampled As Boolean)
        If target Is Nothing Then Return
        If _targetHealthy Then
            Try
                _compositor.ReturnGpuPaintTarget(target, width, height, _deviceGeneration, superSampled)
                Return
            Catch
            End Try
        End If
        Try
            _compositor.DiscardGpuPaintTarget(target, width, height, superSampled)
        Catch
            Try : target.Dispose() : Catch : End Try
        End Try
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        Dim pending As Exception = Nothing
        Try
            Try
                FlushToHdc()
            Catch ex As Exception
                _targetHealthy = False
                If Not _compositor.NotifyDeviceContextException(ex) Then pending = ex
            Finally
                ReleaseGpuResources()
            End Try
        Finally
            Try : _graphics.ReleaseHdc(_hdc) : Catch : End Try
        End Try
        If pending IsNot Nothing Then Throw pending
    End Sub

    Private Shared Function NormalizeDirtyRect(rect As Rectangle, width As Integer, height As Integer) As Rectangle
        Dim bounds As New Rectangle(0, 0, Math.Max(1, width), Math.Max(1, height))
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return bounds
        Dim clipped = Rectangle.Intersect(bounds, rect)
        Return If(clipped.Width > 0 AndAlso clipped.Height > 0, clipped, bounds)
    End Function

    Private Const SRCCOPY As Integer = &HCC0020

    <DllImport("gdi32.dll", SetLastError:=True)>
    Private Shared Function BitBlt(hdcDest As IntPtr,
                                   xDest As Integer,
                                   yDest As Integer,
                                   width As Integer,
                                   height As Integer,
                                   hdcSource As IntPtr,
                                   xSource As Integer,
                                   ySource As Integer,
                                   rasterOperation As Integer) As Boolean
    End Function
End Class
