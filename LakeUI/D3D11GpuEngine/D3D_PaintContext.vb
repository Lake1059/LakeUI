Imports System.Numerics
Imports Vortice.Direct2D1
Imports Vortice.Mathematics

''' <summary>
''' D3D_PaintContext 是后续 GPU 控件迁移唯一允许接收的绘制上下文，替代旧 D3D_PaintScope。
''' 它提供当前 ID2D1DeviceContext、控件本地到窗口 surface 的 transform、clip、DPI scale、文字质量、背景采样入口、frame generation 和 device generation。
''' FrameGeneration 只描述窗口帧序号，DeviceGeneration 只描述底层 D3D/D2D 设备代号；长期 GPU 缓存必须使用 DeviceGeneration 判定是否过期。
''' 它不拥有长期 GPU 资源，不提交 Present，不访问 PaintEventArgs、Graphics、HDC 或 BitBlt。
''' <para>
''' 后续控件迁移接入模板：
''' 1. 控件实现 V3_IGpuRenderable。
''' 2. 控件状态变化时调用 V3_InvalidationRouter.RequestRender(Me, dirtyRect)。
''' 3. RenderGpu 内只使用传入的 D3D_PaintContext。
''' 4. 不允许在 RenderGpu 内创建长期 GPU 缓存；长期缓存必须走 D3D_WindowCompositor 的缓存服务。
''' 5. 不允许在 RenderGpu 内访问 PaintEventArgs、Graphics、HDC、BitBlt。
''' </para>
''' </summary>
Public NotInheritable Class D3D_PaintContext
    Implements IDisposable

    Private ReadOnly _clipStack As New Stack(Of D3D_ClipScope)()
    Private _disposed As Boolean

    Friend Sub New(compositor As D3D_WindowCompositor,
                   deviceContext As ID2D1DeviceContext,
                   localToWindowTransform As Matrix3x2,
                   clipBounds As RectangleF,
                   dpiScale As Single,
                   textQuality As D3D_TextQualityMode,
                   targetHasAlpha As Boolean,
                   frameGeneration As Integer,
                   deviceGeneration As Integer,
                   dirtyRegion As IReadOnlyList(Of Rectangle))
        Me.Compositor = compositor
        Me.DeviceContext = deviceContext
        Me.LocalToWindowTransform = localToWindowTransform
        Me.ClipBounds = clipBounds
        Me.DpiScale = dpiScale
        Me.TextQuality = textQuality
        Me.TargetHasAlpha = targetHasAlpha
        Me.FrameGeneration = frameGeneration
        Me.DeviceGeneration = deviceGeneration
        Me.DirtyRegion = dirtyRegion
    End Sub

    Public ReadOnly Property Compositor As D3D_WindowCompositor
    Public ReadOnly Property DeviceContext As ID2D1DeviceContext
    Public ReadOnly Property LocalToWindowTransform As Matrix3x2
    Public ReadOnly Property ClipBounds As RectangleF
    Public ReadOnly Property DpiScale As Single
    Public ReadOnly Property TextQuality As D3D_TextQualityMode
    Public ReadOnly Property TargetHasAlpha As Boolean
    Public ReadOnly Property FrameGeneration As Integer
    Public ReadOnly Property DeviceGeneration As Integer
    Public ReadOnly Property DirtyRegion As IReadOnlyList(Of Rectangle)

    ''' <summary>
    ''' 绘制填充矩形。调用方只传业务颜色；D3D_BrushCache 负责跨帧 brush 生命周期。
    ''' </summary>
    Public Sub FillRectangle(rect As RectangleF, color As System.Drawing.Color)
        Dim brush = Compositor.BrushCache.GetSolidBrush(DeviceContext, color, DeviceGeneration)
        DeviceContext.FillRectangle(ToRawRect(rect), brush)
    End Sub

    ''' <summary>
    ''' 绘制矩形边框。该方法使用窗口级 brush cache，不在控件内创建长期画刷。
    ''' </summary>
    Public Sub DrawRectangle(rect As RectangleF, color As System.Drawing.Color, Optional strokeWidth As Single = 1.0F)
        Dim brush = Compositor.BrushCache.GetSolidBrush(DeviceContext, color, DeviceGeneration)
        DeviceContext.DrawRectangle(ToRawRect(rect), brush, Math.Max(0.1F, strokeWidth))
    End Sub

    ''' <summary>
    ''' 绘制图片，图片上传和缩放策略由 D3D_ImageCache 处理；控件不得缓存预缩放 CPU bitmap。
    ''' </summary>
    Public Sub DrawImage(image As Image,
                         destination As RectangleF,
                         Optional source As RectangleF? = Nothing,
                         Optional opacity As Single = 1.0F,
                         Optional frameIndex As Integer = 0,
                         Optional interpolation As InterpolationMode = InterpolationMode.Linear)
        Compositor.ImageCache.DrawImage(Me, image, destination, source, opacity, frameIndex, interpolation)
    End Sub

    ''' <summary>
    ''' 按显式 source 关系采样背景。主链路使用 D3D_BackgroundPenetration 的 CPU backing + D2D 上传缓存，
    ''' 不再递归生成窗口级 GPU snapshot。
    ''' </summary>
    ''' <param name="destination">控件本地目标矩形；局部背景采样必须显式传局部矩形，不能隐式扩成全控件。</param>
    Public Function DrawBackgroundSource(consumer As Control, source As Control, destination As RectangleF) As Boolean
        If consumer Is Nothing OrElse consumer.IsDisposed Then Return False
        If source Is Nothing OrElse source.IsDisposed Then Return False
        If consumer.Width <= 0 OrElse consumer.Height <= 0 Then Return False
        If destination.Width <= 0 OrElse destination.Height <= 0 Then
            destination = New RectangleF(0, 0, consumer.Width, consumer.Height)
        End If
        D3D_BackgroundPenetration.PaintBackground(consumer, Me, source, destination)
        Return True
    End Function

    ''' <summary>
    ''' 绘制文本，文本质量由 D3D_TextRenderer 控制；后续迁移控件不要直接调用旧 D3D_TextInterop。
    ''' </summary>
    Public Sub DrawText(text As String,
                        font As Font,
                        color As System.Drawing.Color,
                        layoutRect As RectangleF,
                        Optional hAlign As Vortice.DirectWrite.TextAlignment = Vortice.DirectWrite.TextAlignment.Leading,
                        Optional vAlign As Vortice.DirectWrite.ParagraphAlignment = Vortice.DirectWrite.ParagraphAlignment.Near,
                        Optional wordWrap As Boolean = False)
        Compositor.TextRenderer.DrawText(Me, text, font, color, layoutRect, hAlign, vAlign, wordWrap)
    End Sub

    Public Sub DrawText(text As String,
                        font As Font,
                        color As System.Drawing.Color,
                        layoutRect As RectangleF,
                        flags As TextFormatFlags)
        Compositor.TextRenderer.DrawText(Me, text, font, color, layoutRect, flags)
    End Sub

    ''' <summary>
    ''' 推入窗口 surface 上的轴对齐 clip。返回对象必须在同一 RenderGpu 调用栈中 Dispose，不能跨帧保存。
    ''' </summary>
    Public Function PushClip(rect As RectangleF) As IDisposable
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return NoopClipScope.Instance
        Dim scope As New D3D_ClipScope(DeviceContext, rect)
        _clipStack.Push(scope)
        Return scope
    End Function

    Friend Shared Function ToRawRect(rect As RectangleF) As Vortice.RawRectF
        Return New Vortice.RawRectF(rect.Left, rect.Top, rect.Right, rect.Bottom)
    End Function

    Friend Shared Function ToColor4(color As System.Drawing.Color) As Color4
        Return New Color4(color.R / 255.0F, color.G / 255.0F, color.B / 255.0F, color.A / 255.0F)
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True

        While _clipStack.Count > 0
            Try : _clipStack.Pop().Dispose() : Catch : End Try
        End While

        GC.SuppressFinalize(Me)
    End Sub

    Private NotInheritable Class D3D_ClipScope
        Implements IDisposable

        Private ReadOnly _context As ID2D1DeviceContext
        Private _disposed As Boolean

        Public Sub New(context As ID2D1DeviceContext, rect As RectangleF)
            _context = context
            _context.PushAxisAlignedClip(ToRawRect(rect), AntialiasMode.PerPrimitive)
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then Return
            _disposed = True
            _context.PopAxisAlignedClip()
        End Sub
    End Class

    Private NotInheritable Class NoopClipScope
        Implements IDisposable

        Public Shared ReadOnly Instance As New NoopClipScope()

        Private Sub New()
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
        End Sub
    End Class
End Class
