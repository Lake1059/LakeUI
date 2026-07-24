Imports System.Numerics
Imports System.Globalization
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

    Private _clipStack As Stack(Of IDisposable)
    Private ReadOnly _beginTextureUse As Action
    Private ReadOnly _beginBackdropUse As Action
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
                   dirtyRectangle As Rectangle,
                   beginTextureUse As Action,
                   beginBackdropUse As Action)
        Me.Compositor = compositor
        Me.DeviceContext = deviceContext
        Me.LocalToWindowTransform = localToWindowTransform
        Me.ClipBounds = clipBounds
        Me.DpiScale = dpiScale
        Me.TextQuality = textQuality
        Me.TargetHasAlpha = targetHasAlpha
        Me.FrameGeneration = frameGeneration
        Me.DeviceGeneration = deviceGeneration
        Me.DirtyRectangle = dirtyRectangle
        _beginTextureUse = beginTextureUse
        _beginBackdropUse = beginBackdropUse
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
    Public ReadOnly Property DirtyRectangle As Rectangle

    ''' <summary>返回指定本地矩形是否与当前脏区相交，供大型控件跳过确定不可见的绘制命令。</summary>
    Public Function IntersectsDirty(rect As RectangleF) As Boolean
        If rect.Width <= 0.0F OrElse rect.Height <= 0.0F Then Return False
        If DirtyRectangle.Width <= 0 OrElse DirtyRectangle.Height <= 0 Then Return True
        Dim rounded = Rectangle.Ceiling(rect)
        Return DirtyRectangle.IntersectsWith(rounded)
    End Function

    Public Function IntersectsDirty(rect As Rectangle) As Boolean
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return False
        If DirtyRectangle.Width <= 0 OrElse DirtyRectangle.Height <= 0 Then Return True
        Return DirtyRectangle.IntersectsWith(rect)
    End Function

    Private Function CanCullOutsideDirty(rect As RectangleF, Optional inflate As Single = 0.0F) As Boolean
        If rect.Width <= 0.0F OrElse rect.Height <= 0.0F Then Return True
        ' 控件传入的边界使用本地坐标。调用方临时改写 Transform（例如旋转图表文字）时，
        ' 无法用未变换边界可靠裁剪，因此保守放行。
        If DeviceContext Is Nothing OrElse DeviceContext.Transform <> LocalToWindowTransform Then Return False
        If inflate > 0.0F Then rect.Inflate(inflate, inflate)
        Return Not IntersectsDirty(rect)
    End Function

    ''' <summary>
    ''' 绘制填充矩形。调用方只传业务颜色；D3D_BrushCache 负责跨帧 brush 生命周期。
    ''' </summary>
    Public Sub FillRectangle(rect As RectangleF, color As System.Drawing.Color)
        If color.A = 0 OrElse CanCullOutsideDirty(rect) Then Return
        Dim brush = Compositor.BrushCache.GetSolidBrush(DeviceContext, color, DeviceGeneration)
        DeviceContext.FillRectangle(ToRawRect(rect), brush)
    End Sub

    ''' <summary>
    ''' 绘制矩形边框。该方法使用窗口级 brush cache，不在控件内创建长期画刷。
    ''' </summary>
    Public Sub DrawRectangle(rect As RectangleF, color As System.Drawing.Color, Optional strokeWidth As Single = 1.0F)
        If color.A = 0 OrElse strokeWidth <= 0.0F OrElse CanCullOutsideDirty(rect, Math.Max(0.1F, strokeWidth) / 2.0F) Then Return
        Dim brush = Compositor.BrushCache.GetSolidBrush(DeviceContext, color, DeviceGeneration)
        DeviceContext.DrawRectangle(ToRawRect(rect), brush, Math.Max(0.1F, strokeWidth))
    End Sub

    Public Sub FillRoundedRectangle(rect As RectangleF, radius As Single, color As System.Drawing.Color)
        If color.A = 0 OrElse CanCullOutsideDirty(rect) Then Return
        Dim brush = Compositor.BrushCache.GetSolidBrush(DeviceContext, color, DeviceGeneration)
        FillRoundedRectangle(rect, radius, DirectCast(brush, ID2D1Brush))
    End Sub

    Public Sub FillRoundedRectangle(rect As RectangleF, radius As Single, brush As ID2D1Brush)
        If brush Is Nothing OrElse CanCullOutsideDirty(rect) Then Return
        radius = NormalizeRoundedRadius(rect, radius)
        If radius <= 0 Then
            DeviceContext.FillRectangle(ToRawRect(rect), brush)
            Return
        End If

        If TypeOf brush Is ID2D1SolidColorBrush Then
            ' 纯色不受坐标平移影响：缓存原点几何，仅用当前变换平移到目标位置。
            ' key 不包含 X/Y，滚动中的卡片不会按每个位置污染几何缓存。
            Dim localRect As New RectangleF(0.0F, 0.0F, rect.Width, rect.Height)
            Dim geometry = GetRoundedRectangleGeometry(localRect, radius)
            If geometry Is Nothing Then Return
            Dim oldTransform = DeviceContext.Transform
            Try
                DeviceContext.Transform = Matrix3x2.CreateTranslation(rect.X, rect.Y) * oldTransform
                DeviceContext.FillGeometry(geometry, brush)
            Finally
                DeviceContext.Transform = oldTransform
            End Try
            Return
        End If

        ' 渐变画刷使用目标坐标，不能通过平移规范化；只为这类少量路径保留临时几何。
        Using geometry = D3D_RenderCore.DeviceManager.D2DFactory.CreateRoundedRectangleGeometry(New RoundedRectangle(rect, radius, radius))
            DeviceContext.FillGeometry(geometry, brush)
        End Using
    End Sub

    Public Function GetRoundedRectangleGeometry(rect As RectangleF, radius As Single) As ID2D1Geometry
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return Nothing
        radius = NormalizeRoundedRadius(rect, radius)
        If radius <= 0 Then Return Nothing

        Return Compositor.GeometryCache.GetOrCreateGeometry(
            CreateRoundedRectangleGeometryKey(rect, radius),
            Function() D3D_RenderCore.DeviceManager.D2DFactory.CreateRoundedRectangleGeometry(New RoundedRectangle(rect, radius, radius)))
    End Function

    Public Sub DrawRoundedRectangle(rect As RectangleF, radius As Single, color As System.Drawing.Color, Optional strokeWidth As Single = 1.0F)
        If color.A = 0 OrElse strokeWidth <= 0 OrElse CanCullOutsideDirty(rect, Math.Max(0.1F, strokeWidth) / 2.0F) Then Return
        Dim brush = Compositor.BrushCache.GetSolidBrush(DeviceContext, color, DeviceGeneration)
        DrawRoundedRectangle(rect, radius, DirectCast(brush, ID2D1Brush), strokeWidth)
    End Sub

    Public Sub DrawRoundedRectangle(rect As RectangleF, radius As Single, brush As ID2D1Brush, Optional strokeWidth As Single = 1.0F)
        If brush Is Nothing OrElse strokeWidth <= 0 OrElse CanCullOutsideDirty(rect, Math.Max(0.1F, strokeWidth) / 2.0F) Then Return
        radius = NormalizeRoundedRadius(rect, radius)
        If radius <= 0 Then
            DeviceContext.DrawRectangle(ToRawRect(rect), brush, Math.Max(0.1F, strokeWidth))
            Return
        End If

        DeviceContext.DrawRoundedRectangle(New RoundedRectangle(rect, radius, radius), brush, Math.Max(0.1F, strokeWidth))
    End Sub

    ''' <summary>
    ''' 获取由现有 GPU 总预算管理的两色线性渐变画刷。停止点和画刷跨帧复用，
    ''' 起止坐标按本次目标矩形更新；调用方不得 Dispose 返回值。
    ''' </summary>
    Public Function GetLinearGradientBrush(bounds As RectangleF,
                                           startColor As System.Drawing.Color,
                                           endColor As System.Drawing.Color,
                                           direction As System.Windows.Forms.Orientation,
                                           Optional reverse As Boolean = False) As ID2D1LinearGradientBrush
        If bounds.Width <= 0.0F OrElse bounds.Height <= 0.0F Then Return Nothing
        BeginTextureUse()
        Dim key As New D3D_LinearGradientCacheKey(DeviceContext,
                                                  DeviceGeneration,
                                                  D3D_HdrOutput.VectorColorRevision,
                                                  startColor.ToArgb(),
                                                  endColor.ToArgb(),
                                                  direction,
                                                  reverse)
        Dim brush = Compositor.TextureCache.AcquireTexture(
            key,
            DeviceGeneration,
            512L,
            Function()
                Dim stops() As GradientStop = {
                    New GradientStop With {.Position = 0.0F, .Color = ToColor4(startColor)},
                    New GradientStop With {.Position = 1.0F, .Color = ToColor4(endColor)}}
                Using stopCollection = DeviceContext.CreateGradientStopCollection(stops)
                    Return DeviceContext.CreateLinearGradientBrush(
                        New LinearGradientBrushProperties(Vector2.Zero, Vector2.One),
                        stopCollection)
                End Using
            End Function)
        If brush Is Nothing Then Return Nothing

        Dim startPoint As Vector2
        Dim endPoint As Vector2
        If direction = System.Windows.Forms.Orientation.Vertical Then
            startPoint = New Vector2(bounds.X, If(reverse, bounds.Bottom, bounds.Y))
            endPoint = New Vector2(bounds.X, If(reverse, bounds.Y, bounds.Bottom))
        Else
            startPoint = New Vector2(If(reverse, bounds.Right, bounds.X), bounds.Y)
            endPoint = New Vector2(If(reverse, bounds.X, bounds.Right), bounds.Y)
        End If
        brush.StartPoint = startPoint
        brush.EndPoint = endPoint
        Return brush
    End Function

    ''' <summary>
    ''' 绘制图片，图片上传和缩放策略由 D3D_ImageCache 处理；控件不得缓存预缩放 CPU bitmap。
    ''' </summary>
    Public Sub DrawImage(image As Image,
                         destination As RectangleF,
                         Optional source As RectangleF? = Nothing,
                         Optional opacity As Single = 1.0F,
                         Optional frameIndex As Integer = 0,
                         Optional interpolation As InterpolationMode = InterpolationMode.Linear)
        If image Is Nothing OrElse opacity <= 0.0F OrElse CanCullOutsideDirty(destination) Then Return
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
    ''' 绘制文本，文本质量由 D3D_TextRenderer 控制。
    ''' </summary>
    Public Sub DrawText(text As String,
                        font As Font,
                        color As System.Drawing.Color,
                        layoutRect As RectangleF,
                        Optional hAlign As Vortice.DirectWrite.TextAlignment = Vortice.DirectWrite.TextAlignment.Leading,
                        Optional vAlign As Vortice.DirectWrite.ParagraphAlignment = Vortice.DirectWrite.ParagraphAlignment.Near,
                        Optional wordWrap As Boolean = False)
        If CanCullOutsideDirty(layoutRect) Then Return
        Compositor.TextRenderer.DrawText(Me, text, font, color, layoutRect, hAlign, vAlign, wordWrap)
    End Sub

    Public Sub DrawText(text As String,
                        font As Font,
                        color As System.Drawing.Color,
                        layoutRect As RectangleF,
                        flags As TextFormatFlags)
        If CanCullOutsideDirty(layoutRect) Then Return
        Compositor.TextRenderer.DrawText(Me, text, font, color, layoutRect, flags)
    End Sub

    ''' <summary>
    ''' 推入窗口 surface 上的轴对齐 clip。返回对象必须在同一 RenderGpu 调用栈中 Dispose，不能跨帧保存。
    ''' </summary>
    Public Function PushClip(rect As RectangleF) As IDisposable
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return NoopClipScope.Instance
        Dim scope As New D3D_ClipScope(DeviceContext, rect)
        If _clipStack Is Nothing Then _clipStack = New Stack(Of IDisposable)()
        _clipStack.Push(scope)
        Return scope
    End Function

    ''' <summary>
    ''' 推入几何 clip。返回对象必须在同一 RenderGpu 调用栈中 Dispose，不能跨帧保存。
    ''' </summary>
    Public Function PushGeometryClip(geometry As ID2D1Geometry, contentBounds As RectangleF) As IDisposable
        If geometry Is Nothing OrElse contentBounds.Width <= 0 OrElse contentBounds.Height <= 0 Then Return NoopClipScope.Instance
        Dim scope As New D3D_GeometryClipScope(DeviceContext, geometry, contentBounds)
        If _clipStack Is Nothing Then _clipStack = New Stack(Of IDisposable)()
        _clipStack.Push(scope)
        Return scope
    End Function

    Friend Shared Function ToRawRect(rect As RectangleF) As Vortice.RawRectF
        Return New Vortice.RawRectF(rect.Left, rect.Top, rect.Right, rect.Bottom)
    End Function

    Private Shared Function NormalizeRoundedRadius(rect As RectangleF, radius As Single) As Single
        If radius <= 0 Then Return 0.0F
        Return Math.Min(radius, Math.Min(rect.Width / 2.0F, rect.Height / 2.0F))
    End Function

    Private Shared Function CreateRoundedRectangleGeometryKey(rect As RectangleF, radius As Single) As String
        Return "rr:" &
            SingleToKey(rect.X) & ":" &
            SingleToKey(rect.Y) & ":" &
            SingleToKey(rect.Width) & ":" &
            SingleToKey(rect.Height) & ":" &
            SingleToKey(radius)
    End Function

    Private Structure D3D_LinearGradientCacheKey
        Implements IEquatable(Of D3D_LinearGradientCacheKey)

        Private ReadOnly _context As ID2D1DeviceContext
        Private ReadOnly _generation As Integer
        Private ReadOnly _hdrRevision As Integer
        Private ReadOnly _startArgb As Integer
        Private ReadOnly _endArgb As Integer
        Private ReadOnly _direction As System.Windows.Forms.Orientation
        Private ReadOnly _reverse As Boolean

        Friend Sub New(context As ID2D1DeviceContext,
                       generation As Integer,
                       hdrRevision As Integer,
                       startArgb As Integer,
                       endArgb As Integer,
                       direction As System.Windows.Forms.Orientation,
                       reverse As Boolean)
            _context = context
            _generation = generation
            _hdrRevision = hdrRevision
            _startArgb = startArgb
            _endArgb = endArgb
            _direction = direction
            _reverse = reverse
        End Sub

        Public Overloads Function Equals(other As D3D_LinearGradientCacheKey) As Boolean Implements IEquatable(Of D3D_LinearGradientCacheKey).Equals
            Return ReferenceEquals(_context, other._context) AndAlso
                   _generation = other._generation AndAlso
                   _hdrRevision = other._hdrRevision AndAlso
                   _startArgb = other._startArgb AndAlso
                   _endArgb = other._endArgb AndAlso
                   _direction = other._direction AndAlso
                   _reverse = other._reverse
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            Return TypeOf obj Is D3D_LinearGradientCacheKey AndAlso Equals(DirectCast(obj, D3D_LinearGradientCacheKey))
        End Function

        Public Overrides Function GetHashCode() As Integer
            Dim hash = HashCode.Combine(Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_context),
                                        _generation,
                                        _hdrRevision,
                                        _startArgb,
                                        _endArgb,
                                        CInt(_direction))
            Return HashCode.Combine(hash, _reverse)
        End Function
    End Structure

    Private Shared Function SingleToKey(value As Single) As String
        Return BitConverter.SingleToInt32Bits(value).ToString("X8", CultureInfo.InvariantCulture)
    End Function

    Friend Shared Function ToColor4(color As System.Drawing.Color) As Color4
        Return D3D_HdrOutput.MapColor4(color)
    End Function

    Friend Shared Function ToRawColor4(color As System.Drawing.Color) As Color4
        Return D3D_HdrOutput.ToRawColor4(color)
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True

        If _clipStack IsNot Nothing Then
            While _clipStack.Count > 0
                Try : _clipStack.Pop().Dispose() : Catch : End Try
            End While
        End If

        GC.SuppressFinalize(Me)
    End Sub

    Friend Sub BeginBackdropUse()
        _beginBackdropUse?.Invoke()
    End Sub

    Friend Sub BeginTextureUse()
        _beginTextureUse?.Invoke()
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

    Private NotInheritable Class D3D_GeometryClipScope
        Implements IDisposable

        Private ReadOnly _context As ID2D1DeviceContext
        Private _disposed As Boolean

        Public Sub New(context As ID2D1DeviceContext, geometry As ID2D1Geometry, contentBounds As RectangleF)
            _context = context
            Dim parameters As New LayerParameters With {
                .ContentBounds = ToRawRect(contentBounds),
                .GeometricMask = geometry,
                .MaskAntialiasMode = AntialiasMode.PerPrimitive,
                .MaskTransform = Matrix3x2.Identity,
                .Opacity = 1.0F,
                .OpacityBrush = Nothing,
                .LayerOptions = LayerOptions.None
            }
            _context.PushLayer(parameters, Nothing)
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then Return
            _disposed = True
            _context.PopLayer()
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
