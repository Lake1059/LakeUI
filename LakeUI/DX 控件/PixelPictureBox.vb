Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Drawing
Imports D2D = Vortice.Direct2D1

<DefaultEvent("SelectionChanged")>
Public Class PixelPictureBox
    Inherits UserControl
    Implements D3D_IGpuRenderable, D3D_IGpuInvalidationSource, D3D_IBackgroundSourceProvider, V5_IGpuPresentationSource, D3D_IFloatPresentationSource, D3D_IFloatRenderingSource, D3D_IBackgroundOpacitySource

    ''' <summary>选区被程序设置、清空或鼠标完成绘制、移动、缩放后触发；交互过程可通过 SelectionRect 查询实时结果。</summary>
    Public Event SelectionChanged As EventHandler
    ''' <summary>图像源更换且视图、帧索引和选区完成重置后触发。</summary>
    Public Event SourceChanged As EventHandler
    ''' <summary>鼠标对应的原始像素或动图帧变化时触发；离开图片时 HasPixel 为 False。</summary>
    Public Event PixelHovered As EventHandler(Of PixelPicturePixelEventArgs)
    ''' <summary>当前实际缩放倍率变化时触发；平滑缩放期间可以多次触发。</summary>
    Public Event ZoomChanged As EventHandler

    Public Sub New()
        InitializeComponent()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw Or ControlStyles.Selectable, True)
        UpdateStyles()
        _motion.DirtyProvider = AddressOf AnimationDirtyRegion
        _motion.FPS = 120
    End Sub

    Private ReadOnly _motion As New D3D_AnimationHelper(Me)
    Private _source As IPixelPictureSource
    Private _backgroundSource As Control
    Private _frameIndex As Integer
    Private _completedLoops As Integer
    Private _nextFrameTicks As Long
    Private _lastMotionTicks As Long
    Private _zoom As Double
    Private _targetZoom As Double
    Private ReadOnly _zoomMotion As New PixelPictureZoomMotion()
    Private _zoomAnchor As Point
    Private _zoomAnchorImage As PixelPicturePoint
    Private _zoomRunning As Boolean
    Private _animationEnded As Boolean
    Private _motionDirty As Boolean
    Private _refreshQueued As Integer
    Private _sourceNotification As IPixelPictureInvalidationSource
    Private _minZoom As Double = 1.0
    Private _maxZoom As Double = 64.0
    Private _scrollX As Double
    Private _scrollY As Double
    Private _pointerLocation As Point
    Private _pointerInside As Boolean
    Private _lastPixelKey As String
    Private _verticalTrack As RectangleF
    Private _horizontalTrack As RectangleF
    Private _verticalThumb As RectangleF
    Private _horizontalThumb As RectangleF
    Private _showVerticalScroll As Boolean
    Private _showHorizontalScroll As Boolean

    Private _selectionRect As Rectangle
    Private _dragMode As DragMode
    Private _dragStart As Point
    Private _dragScrollX As Double
    Private _dragScrollY As Double
    Private _dragSelectionStart As Rectangle
    Private _activeHandle As HandlePosition

    Private Enum DragMode
        None
        Pan
        MoveSelection
        ResizeSelection
        DrawSelection
        VerticalScroll
        HorizontalScroll
    End Enum

    Private Enum HandlePosition
        None
        TopLeft
        TopCenter
        TopRight
        MiddleLeft
        MiddleRight
        BottomLeft
        BottomCenter
        BottomRight
    End Enum

    Private Shared ReadOnly HandleList As HandlePosition() = {
        HandlePosition.TopLeft, HandlePosition.TopCenter, HandlePosition.TopRight,
        HandlePosition.MiddleLeft, HandlePosition.MiddleRight,
        HandlePosition.BottomLeft, HandlePosition.BottomCenter, HandlePosition.BottomRight}

    Private _backColor1 As Color = Color.FromArgb(36, 36, 36)
    ''' <summary>获取或设置控件底色，可带透明度；未指定 BackgroundSource 时覆盖在父级 GPU 背景上，图片留白区使用此颜色。</summary>
    <Category("LakeUI"), DefaultValue(GetType(Color), "36, 36, 36")>
    Public Property BackColor1 As Color
        Get
            Return _backColor1
        End Get
        Set(value As Color)
            If _backColor1 = value Then Return
            _backColor1 = value
            RequestRender()
        End Set
    End Property

    Private _borderColor As Color = Color.Gray
    ''' <summary>获取或设置控件外边框颜色。</summary>
    <Category("LakeUI"), DefaultValue(GetType(Color), "Gray")>
    Public Property BorderColor As Color
        Get
            Return _borderColor
        End Get
        Set(value As Color)
            If _borderColor = value Then Return
            _borderColor = value
            RequestRender()
        End Set
    End Property

    Private _borderSize As Integer = 1
    ''' <summary>获取或设置控件外边框宽度；单位为逻辑像素，0 表示不绘制。</summary>
    <Category("LakeUI"), DefaultValue(1)>
    Public Property BorderSize As Integer
        Get
            Return _borderSize
        End Get
        Set(value As Integer)
            If _borderSize = Math.Max(0, value) Then Return
            _borderSize = Math.Max(0, value)
            RequestRender()
        End Set
    End Property

    Private _scrollBarWidth As Integer = 10
    ''' <summary>获取或设置滚动条宽度；单位为逻辑像素，绘制时按控件 DPI 缩放。</summary>
    <Category("LakeUI"), DefaultValue(10)>
    Public Property ScrollBarWidth As Integer
        Get
            Return _scrollBarWidth
        End Get
        Set(value As Integer)
            If _scrollBarWidth = Math.Max(1, value) Then Return
            _scrollBarWidth = Math.Max(1, value)
            RequestRender()
        End Set
    End Property

    Private _scrollBarTrackColor As Color = Color.FromArgb(20, 20, 20)
    ''' <summary>获取或设置滚动条轨道颜色，支持透明度。</summary>
    <Category("LakeUI")>
    <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property ScrollBarTrackColor As Color
        Get
            Return _scrollBarTrackColor
        End Get
        Set(value As Color)
            If _scrollBarTrackColor = value Then Return
            _scrollBarTrackColor = value
            RequestRender()
        End Set
    End Property

    Private _scrollBarThumbColor As Color = Color.FromArgb(80, 80, 80)
    ''' <summary>获取或设置滚动条滑块的正常颜色。</summary>
    <Category("LakeUI")>
    <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property ScrollBarThumbColor As Color
        Get
            Return _scrollBarThumbColor
        End Get
        Set(value As Color)
            If _scrollBarThumbColor = value Then Return
            _scrollBarThumbColor = value
            RequestRender()
        End Set
    End Property

    Private _scrollBarThumbHoverColor As Color = Color.FromArgb(120, 120, 120)
    ''' <summary>获取或设置滚动条滑块悬停或拖动时的颜色。</summary>
    <Category("LakeUI")>
    <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property ScrollBarThumbHoverColor As Color
        Get
            Return _scrollBarThumbHoverColor
        End Get
        Set(value As Color)
            If _scrollBarThumbHoverColor = value Then Return
            _scrollBarThumbHoverColor = value
            RequestRender()
        End Set
    End Property

    Private _selectionBorderColor As Color = Color.FromArgb(200, 60, 60)
    ''' <summary>获取或设置像素选区的边框颜色。</summary>
    <Category("LakeUI")>
    <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SelectionBorderColor As Color
        Get
            Return _selectionBorderColor
        End Get
        Set(value As Color)
            If _selectionBorderColor = value Then Return
            _selectionBorderColor = value
            RequestRender()
        End Set
    End Property

    Private _selectionBorderSize As Integer = 2
    ''' <summary>获取或设置选区边框的绘制宽度；单位为控件客户区像素。</summary>
    <Category("LakeUI"), DefaultValue(2)>
    Public Property SelectionBorderSize As Integer
        Get
            Return _selectionBorderSize
        End Get
        Set(value As Integer)
            If _selectionBorderSize = Math.Max(1, value) Then Return
            _selectionBorderSize = Math.Max(1, value)
            RequestRender()
        End Set
    End Property

    Private _selectionFillColor As Color = Color.FromArgb(30, 200, 60, 60)
    ''' <summary>获取或设置选区内部填充颜色，支持透明度；不会修改源图像像素。</summary>
    <Category("LakeUI")>
    <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SelectionFillColor As Color
        Get
            Return _selectionFillColor
        End Get
        Set(value As Color)
            If _selectionFillColor = value Then Return
            _selectionFillColor = value
            RequestRender()
        End Set
    End Property

    Private _handleColor As Color = Color.White
    ''' <summary>获取或设置选区八个缩放手柄的填充颜色。</summary>
    <Category("LakeUI")>
    <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property HandleColor As Color
        Get
            Return _handleColor
        End Get
        Set(value As Color)
            If _handleColor = value Then Return
            _handleColor = value
            RequestRender()
        End Set
    End Property

    Private _handleBorderColor As Color = Color.FromArgb(200, 60, 60)
    ''' <summary>获取或设置选区缩放手柄的边框颜色。</summary>
    <Category("LakeUI")>
    <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property HandleBorderColor As Color
        Get
            Return _handleBorderColor
        End Get
        Set(value As Color)
            If _handleBorderColor = value Then Return
            _handleBorderColor = value
            RequestRender()
        End Set
    End Property

    Private _handleSize As Integer = 10
    ''' <summary>获取或设置缩放手柄的屏幕边长；单位为控件客户区像素，不随图像缩放。</summary>
    <Category("LakeUI"), DefaultValue(10)>
    Public Property HandleSize As Integer
        Get
            Return _handleSize
        End Get
        Set(value As Integer)
            If _handleSize = Math.Max(1, value) Then Return
            _handleSize = Math.Max(1, value)
            RequestRender()
        End Set
    End Property

    Private _handleHitTolerance As Integer = 6
    ''' <summary>获取或设置手柄可点击范围向外扩展的距离；单位为控件客户区像素。</summary>
    <Category("LakeUI"), DefaultValue(6)>
    Public Property HandleHitTolerance As Integer
        Get
            Return _handleHitTolerance
        End Get
        Set(value As Integer)
            If _handleHitTolerance = Math.Max(0, value) Then Return
            _handleHitTolerance = Math.Max(0, value)
        End Set
    End Property

    Private _borderHitTolerance As Integer = 6
    ''' <summary>获取或设置选区边框可拖动范围向内和向外扩展的距离；单位为控件客户区像素。</summary>
    <Category("LakeUI"), DefaultValue(6)>
    Public Property BorderHitTolerance As Integer
        Get
            Return _borderHitTolerance
        End Get
        Set(value As Integer)
            If _borderHitTolerance = Math.Max(1, value) Then Return
            _borderHitTolerance = Math.Max(1, value)
        End Set
    End Property

    Private _showSelection As Boolean = True
    ''' <summary>获取或设置是否绘制和允许操作像素选区；右键绘制，左键拖动边框或缩放手柄。</summary>
    <Category("LakeUI"), DefaultValue(True)>
    Public Property ShowSelection As Boolean
        Get
            Return _showSelection
        End Get
        Set(value As Boolean)
            If _showSelection = value Then Return
            _showSelection = value
            If Not value Then Cursor = Cursors.Default
            RequestRender()
        End Set
    End Property

    Private _selectionAspectRatio As Single
    ''' <summary>获取或设置绘制和缩放选区的宽高比；0 表示自由比例，正值表示宽度除以高度。</summary>
    <Category("LakeUI"), DefaultValue(0.0F)>
    Public Property SelectionAspectRatio As Single
        Get
            Return _selectionAspectRatio
        End Get
        Set(value As Single)
            If _selectionAspectRatio = Math.Max(0, value) Then Return
            _selectionAspectRatio = Math.Max(0, value)
        End Set
    End Property

    Private _selectionForceCenter As Boolean
    ''' <summary>获取或设置是否强制将选区置于源图像中心；启用后禁止通过边框移动选区，仍可缩放。</summary>
    <Category("LakeUI"), DefaultValue(False)>
    Public Property SelectionForceCenter As Boolean
        Get
            Return _selectionForceCenter
        End Get
        Set(value As Boolean)
            If _selectionForceCenter = value Then Return
            _selectionForceCenter = value
            If value Then _selectionRect = CenterSelection(_selectionRect)
            If _pointerInside Then UpdateInteractionCursor(_pointerLocation)
            RequestRender()
        End Set
    End Property

    Private _maxPixelSize As Integer = 64
    ''' <summary>获取或设置最大图像缩放倍率；单位为单个源像素对应的客户区像素边长，最小为 1。</summary>
    <Category("LakeUI"), Description("最大缩放倍率对应的像素屏幕边长"), DefaultValue(64)>
    Public Property MaxPixelSize As Integer
        Get
            Return _maxPixelSize
        End Get
        Set(value As Integer)
            If _maxPixelSize = Math.Max(1, value) Then Return
            _maxPixelSize = Math.Max(1, value)
            UpdateZoomRange()
            RequestRender()
        End Set
    End Property

    ''' <summary>获取或设置由调用方提供的已解码图像源；控件仅呈现，不解码也不释放源。更换源会重置选区、帧索引和视图。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property Source As IPixelPictureSource
        Get
            Return _source
        End Get
        Set(value As IPixelPictureSource)
            If value IsNot Nothing AndAlso (value.Width <= 0 OrElse value.Height <= 0 OrElse value.FrameCount <= 0) Then Throw New ArgumentException(NameOf(value))
            If ReferenceEquals(_source, value) Then Return
            StopMotion()
            If _sourceNotification IsNot Nothing Then RemoveHandler _sourceNotification.ContentChanged, AddressOf SourceContentChanged
            _source = value
            Cursor = Cursors.Default
            _sourceNotification = TryCast(value, IPixelPictureInvalidationSource)
            If _sourceNotification IsNot Nothing Then AddHandler _sourceNotification.ContentChanged, AddressOf SourceContentChanged
            _frameIndex = 0
            _completedLoops = 0
            _animationEnded = False
            _lastPixelKey = Nothing
            _selectionRect = Rectangle.Empty
            Cursor = Cursors.Default
            _zoom = 0
            _targetZoom = 0
            _scrollX = 0
            _scrollY = 0
            UpdateZoomRange()
            _zoomMotion.Reset(Math.Log(_zoom))
            UpdateScrollRegion()
            UpdateMotionState()
            RequestRender()
            RaiseEvent SourceChanged(Me, EventArgs.Empty)
        End Set
    End Property

    ''' <summary>获取源图像的原始宽度，单位为像素；没有源时返回 0，使用 Long 支持巨幅图像。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property ImageWidth As Long
        Get
            Return If(_source Is Nothing, 0, _source.Width)
        End Get
    End Property

    ''' <summary>获取源图像的原始高度，单位为像素；没有源时返回 0，使用 Long 支持巨幅图像。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property ImageHeight As Long
        Get
            Return If(_source Is Nothing, 0, _source.Height)
        End Get
    End Property

    ''' <summary>获取源 RGB 数值的色域声明；色域与动态范围独立，scRGB 和宽色域本身不代表 HDR。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property ColorSpace As PixelPictureColorSpace
        Get
            Return If(_source Is Nothing, PixelPictureColorSpace.Unknown, _source.ColorSpace)
        End Get
    End Property

    ''' <summary>获取源数据是否声明为 HDR；PQ/HLG 为 HDR，线性数据由提供方显式声明，不依据浮点格式或宽色域推断。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property IsHdr As Boolean
        Get
            Return _source IsNot Nothing AndAlso _source.IsHdr
        End Get
    End Property

    ''' <summary>获取是否要求最终 HWND 合成表面使用线性 scRGB FP16；仅 HDR 图片启用，SDR 图片输出为 sRGB BGRA8。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property UseFloatPresentation As Boolean Implements D3D_IFloatPresentationSource.UseFloatPresentation
        Get
            Return IsHdr
        End Get
    End Property

    ''' <summary>获取是否要求内部渲染表面使用线性 FP16；有图片时启用以保留色彩精度，不代表最终输出为 HDR。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property UseFloatRendering As Boolean Implements D3D_IFloatRenderingSource.UseFloatRendering
        Get
            Return _source IsNot Nothing
        End Get
    End Property

    ''' <summary>获取当前动图帧的零基索引；没有源或源为静态图片时为 0。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property FrameIndex As Integer
        Get
            Return _frameIndex
        End Get
    End Property

    ''' <summary>获取连续缩放动画的目标倍率；动画未结束时可与当前 Zoom 不同。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property TargetZoom As Double
        Get
            Return _targetZoom
        End Get
    End Property

    Private _animationDuration As Integer = 300
    ''' <summary>获取或设置缩放阻尼的响应时长，单位为毫秒；0 表示立即缩放，连续操作会保留运动速度并更新目标。</summary>
    <Category("LakeUI"), DefaultValue(300), Description(GlobalOptions.动画时长描述词)>
    Public Property AnimationDuration As Integer
        Get
            Return _animationDuration
        End Get
        Set(value As Integer)
            If _animationDuration = Math.Max(0, value) Then Return
            _animationDuration = Math.Max(0, value)
            If _animationDuration = 0 AndAlso _zoomRunning Then
                ApplyZoom(_targetZoom)
                _zoomRunning = False
                UpdateMotionState()
                RequestRender()
            End If
        End Set
    End Property

    ''' <summary>获取或设置缩放和动图呈现共用的动画调度帧率；0 表示使用动画调度器的不限帧模式。</summary>
    <Category("LakeUI"), DefaultValue(120), Description(GlobalOptions.动画帧率描述词)>
    Public Property AnimationFPS As Integer
        Get
            Return _motion.FPS
        End Get
        Set(value As Integer)
            _motion.FPS = Math.Max(0, value)
        End Set
    End Property

    Private _playAnimation As Boolean = True
    ''' <summary>获取或设置是否播放源的动图帧；暂停不影响缩放动画，源的帧间隔和循环次数决定播放节奏。</summary>
    <Category("LakeUI"), DefaultValue(True)>
    Public Property PlayAnimation As Boolean
        Get
            Return _playAnimation
        End Get
        Set(value As Boolean)
            If _playAnimation = value Then Return
            _playAnimation = value
            UpdateMotionState()
        End Set
    End Property

    ''' <summary>获取当前实际缩放倍率；1 表示单个源像素对应一个客户区像素，动画中实时更新。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property Zoom As Double
        Get
            Return _zoom
        End Get
    End Property

    ''' <summary>获取或设置源图像像素坐标中的整数选区；右侧和底部为不包含边界，设置时裁剪到图像范围并应用居中约束。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SelectionRect As Rectangle
        Get
            Return _selectionRect
        End Get
        Set(value As Rectangle)
            Dim clamped = CenterSelection(ClampSelection(value))
            If _selectionRect = clamped Then Return
            _selectionRect = clamped
            If _pointerInside Then UpdateInteractionCursor(_pointerLocation)
            RequestRender()
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Set
    End Property

    ''' <summary>获取或设置选区的原始像素尺寸；通过 SelectionRect 应用图像范围和居中约束。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SelectionSize As Size
        Get
            Return _selectionRect.Size
        End Get
        Set(value As Size)
            SelectionRect = New Rectangle(_selectionRect.Location, value)
        End Set
    End Property

    Public Enum SelectionCorner
        TopLeft
        TopRight
        BottomLeft
        BottomRight
    End Enum

    Public Sub ClearSelection()
        _selectionRect = Rectangle.Empty
        Cursor = Cursors.Default
        RequestRender()
        RaiseEvent SelectionChanged(Me, EventArgs.Empty)
    End Sub

    ''' <summary>获取鼠标当前位置对应的原始源像素及编码信息；鼠标在图片外或源无法提供像素时返回 Nothing，不读取屏幕颜色。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property CurrentPixel As PixelPicturePixel?
        Get
            Dim pixel As PixelPicturePixel
            If _pointerInside AndAlso TryGetPixelAt(_pointerLocation, pixel) Then Return pixel
            Return Nothing
        End Get
    End Property

    ''' <summary>获取或设置显式 GPU 背景来源；指定后直接采样该来源，不叠加 BackColor1。Nothing 时使用自动父级背景和 BackColor1。</summary>
    <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If ReferenceEquals(_backgroundSource, value) Then Return
            _backgroundSource = D3D_BackgroundPenetration.SetBackgroundSource(Me, _backgroundSource, value)
            RequestRender()
        End Set
    End Property

    Private ReadOnly Property IsBackgroundOpaque As Boolean Implements D3D_IBackgroundOpacitySource.IsBackgroundOpaque
        Get
            Return _backColor1.A = 255
        End Get
    End Property

    Public Function TryGetBackgroundSource(ByRef source As Control) As Boolean Implements D3D_IBackgroundSourceProvider.TryGetBackgroundSource
        source = _backgroundSource
        Return source IsNot Nothing
    End Function

    Public Sub RequestSourceRefresh()
        SourceContentChanged(Me, EventArgs.Empty)
    End Sub

    ''' <summary>设置缩放目标倍率；连续调用会保留阻尼运动的速度，并围绕指定客户区位置缩放。</summary>
    ''' <param name="value">目标倍率，自动限制在适应视口倍率与 MaxPixelSize 之间。</param>
    ''' <param name="anchor">缩放锚点的客户区坐标；Nothing 表示客户区中心。</param>
    Public Sub SetZoom(value As Double, Optional anchor As Point? = Nothing)
        If _source Is Nothing Then Return
        ZoomTo(value, If(anchor.HasValue, anchor.Value, New Point(ClientSize.Width \ 2, ClientSize.Height \ 2)))
    End Sub

    ''' <summary>将图像缩放目标重置为适应视口的最小倍率。</summary>
    Public Sub ResetZoom()
        SetZoom(_minZoom)
    End Sub

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        D3D_PaintBridge.PaintRenderable(e, Me, Me)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements D3D_IGpuRenderable.RenderGpu
        If context Is Nothing OrElse Width <= 0 OrElse Height <= 0 Then Return
        Dim originalLinearColors = context.UseLinearColors
        context.UseLinearColors = UseFloatRendering
        Try
            UpdateScrollRegion()
            DrawBackground(context)
            If _source IsNot Nothing Then DrawSource(context)
            DrawSelection(context)
            DrawScrollBars(context)
        Finally
            context.UseLinearColors = originalLinearColors
        End Try
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements D3D_IGpuInvalidationSource.GetRenderBounds
        Return ClientRectangle
    End Function

    Private Sub DrawBackground(context As D3D_PaintContext)
        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(Me, _backgroundSource, New RectangleF(0, 0, Width, Height))
        Else
            context.FillRectangle(ClientRectangle, _backColor1)
        End If
        If _borderSize > 0 Then context.DrawRectangle(ClientRectangle, _borderColor, _borderSize)
    End Sub

    Private Sub DrawSource(context As D3D_PaintContext)
        Dim viewport = ViewportBounds()
        Dim visible = VisibleImageBounds()
        If viewport.Width <= 0 OrElse viewport.Height <= 0 OrElse visible.Width <= 0 OrElse visible.Height <= 0 Then Return
        Using context.PushClip(viewport)
            Dim mode = If(_zoom >= 1.0, D2D.InterpolationMode.NearestNeighbor, D2D.InterpolationMode.Linear)
            _source.Present(context, ImageToClient(visible), visible, _frameIndex, mode)
        End Using
    End Sub

    Private Sub DrawSelection(context As D3D_PaintContext)
        If _source Is Nothing OrElse Not _showSelection OrElse _selectionRect.Width <= 0 OrElse _selectionRect.Height <= 0 Then Return
        Dim screen = ImageToClient(New RectangleF(_selectionRect.X, _selectionRect.Y, _selectionRect.Width, _selectionRect.Height))
        If _selectionFillColor.A > 0 Then context.FillRectangle(screen, _selectionFillColor)
        context.DrawRectangle(screen, _selectionBorderColor, _selectionBorderSize)
        For Each position As HandlePosition In HandleList
            Dim bounds = HandleBounds(position, screen)
            context.FillRectangle(bounds, _handleColor)
            context.DrawRectangle(bounds, _handleBorderColor, 1)
        Next
    End Sub

    Private Sub UpdateZoomRange()
        If _source Is Nothing Then
            _minZoom = 1
            _maxZoom = 1
            _zoom = 1
            _targetZoom = 1
            Return
        End If
        Dim viewport = FullViewportBounds()
        _minZoom = If(viewport.Width <= 0 OrElse viewport.Height <= 0, 1.0, Math.Min(viewport.Width / _source.Width, viewport.Height / _source.Height))
        _maxZoom = Math.Max(_minZoom, CDbl(_maxPixelSize))
        If _zoom <= 0 Then _zoom = _minZoom
        If _targetZoom <= 0 Then _targetZoom = _zoom
        ClampZoom()
    End Sub

    Private Sub ClampZoom()
        _minZoom = Math.Max(Double.Epsilon, _minZoom)
        _maxZoom = Math.Max(_minZoom, _maxZoom)
        _targetZoom = Math.Max(_minZoom, Math.Min(_maxZoom, _targetZoom))
        _zoom = Math.Max(_minZoom, Math.Min(_maxZoom, _zoom))
    End Sub

    Private Sub ZoomTo(value As Double, anchor As Point)
        If _source Is Nothing Then Return
        If Not Double.IsFinite(value) OrElse value <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(value))
        UpdateZoomRange()
        Dim ticks = Stopwatch.GetTimestamp()
        If _zoomRunning Then AdvanceZoom(ticks)
        _zoomAnchor = anchor
        _zoomAnchorImage = ClientToImage(anchor)
        _targetZoom = Math.Max(_minZoom, Math.Min(_maxZoom, value))
        _zoomMotion.Target = Math.Log(_targetZoom)
        _zoomRunning = Math.Abs(_targetZoom - _zoom) > _zoom * 0.00000001 OrElse Math.Abs(_zoomMotion.Velocity) > 0.000001
        If _animationDuration = 0 OrElse Not IsHandleCreated Then
            ApplyZoom(_targetZoom)
            _zoomMotion.Reset(Math.Log(_zoom))
            _zoomRunning = False
        End If
        _lastMotionTicks = ticks
        UpdateMotionState()
        RequestRender()
    End Sub

    Private Sub ApplyZoom(value As Double)
        _zoom = Math.Clamp(value, _minZoom, _maxZoom)
        UpdateScrollRegion()
        Dim viewport = ViewportBounds()
        Dim offsetX = Math.Max(0, (viewport.Width - _source.Width * _zoom) / 2.0)
        Dim offsetY = Math.Max(0, (viewport.Height - _source.Height * _zoom) / 2.0)
        _scrollX = _zoomAnchorImage.X * _zoom - (_zoomAnchor.X - viewport.X - offsetX)
        _scrollY = _zoomAnchorImage.Y * _zoom - (_zoomAnchor.Y - viewport.Y - offsetY)
        UpdateScrollRegion()
        _motionDirty = True
        RaiseEvent ZoomChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub AdvanceZoom(ticks As Long)
        Dim seconds = Math.Max(0, (ticks - _lastMotionTicks) / CDbl(Stopwatch.Frequency))
        _lastMotionTicks = ticks
        _zoomRunning = _zoomMotion.Advance(seconds, _animationDuration)
        ApplyZoom(Math.Exp(_zoomMotion.Value))
    End Sub

    Private Sub UpdateMotionState()
        If _source Is Nothing OrElse Not Visible OrElse Not IsHandleCreated OrElse (Not _zoomRunning AndAlso Not AnimationCanAdvance()) Then
            StopMotion()
            Return
        End If
        If _motion.IsFrameLoopRunning Then Return
        _lastMotionTicks = Stopwatch.GetTimestamp()
        _nextFrameTicks = _lastMotionTicks + MillisecondsToTicks(_source.GetFrameDelayMilliseconds(_frameIndex))
        _motion.StartFrameLoop(AddressOf MotionTick)
    End Sub

    Private Function AnimationCanAdvance() As Boolean
        Return _source IsNot Nothing AndAlso _source.FrameCount > 1 AndAlso _playAnimation AndAlso Not _animationEnded
    End Function

    Private Sub MotionTick(sender As Object, e As EventArgs)
        If _source Is Nothing Then Return
        Dim now = Stopwatch.GetTimestamp()
        If _zoomRunning Then AdvanceZoom(now)

        If AnimationCanAdvance() AndAlso now >= _nextFrameTicks Then
            Dim advanced = 0
            Do
                _frameIndex += 1
                If _frameIndex >= _source.FrameCount Then
                    _frameIndex = 0
                    _completedLoops += 1
                    If _source.LoopCount > 0 AndAlso _completedLoops >= _source.LoopCount Then
                        _frameIndex = _source.FrameCount - 1
                        _animationEnded = True
                        Exit Do
                    End If
                End If
                _nextFrameTicks += MillisecondsToTicks(Math.Max(1, _source.GetFrameDelayMilliseconds(_frameIndex)))
                advanced += 1
            Loop While advanced < Math.Max(1, _source.FrameCount * 2) AndAlso now >= _nextFrameTicks
            If advanced >= _source.FrameCount * 2 AndAlso now >= _nextFrameTicks Then _nextFrameTicks = now + MillisecondsToTicks(_source.GetFrameDelayMilliseconds(_frameIndex))
            _motionDirty = True
        End If
        If _motionDirty Then UpdatePixelUnderPointer()
        UpdateMotionState()
    End Sub

    Private Sub StopMotion()
        If _motion.IsFrameLoopRunning Then _motion.StopFrameLoop()
        _lastMotionTicks = 0
        _nextFrameTicks = 0
    End Sub

    Private Sub AnimationDirtyRegion(helper As D3D_AnimationHelper, owner As Control, sink As D3D_AnimationHelper.InvalidateRegionSink)
        If _motionDirty Then
            sink.InvalidateAll()
            _motionDirty = False
        Else
            sink.SuppressInvalidate()
        End If
    End Sub

    Private Function FullViewportBounds() As RectangleF
        Dim inset = Math.Max(0, CInt(Math.Round(_borderSize * DpiScale())))
        Return New RectangleF(inset, inset, Math.Max(0, Width - inset * 2), Math.Max(0, Height - inset * 2))
    End Function

    Private Function ViewportBounds() As RectangleF
        Dim full = FullViewportBounds()
        Dim reserve = _scrollBarWidth * DpiScale() + 2
        Return New RectangleF(full.X, full.Y, Math.Max(0, full.Width - If(_showVerticalScroll, reserve, 0)), Math.Max(0, full.Height - If(_showHorizontalScroll, reserve, 0)))
    End Function

    ''' <summary>获取视口中可见的源图像区域；单位为原始像素，使用双精度表示以支持巨幅图像和分块请求。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property VisibleRegion As PixelPictureRegion
        Get
            Return VisibleImageBounds()
        End Get
    End Property

    Private Function VisibleImageBounds() As PixelPictureRegion
        If _source Is Nothing OrElse _zoom <= 0 Then Return Nothing
        Dim viewport = ViewportBounds()
        Dim left = Math.Max(0, _scrollX / _zoom)
        Dim top = Math.Max(0, _scrollY / _zoom)
        Dim right = Math.Min(_source.Width, left + viewport.Width / _zoom)
        Dim bottom = Math.Min(_source.Height, top + viewport.Height / _zoom)
        Return New PixelPictureRegion(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top))
    End Function

    Private Function ImageToClient(imageRect As RectangleF) As RectangleF
        Return ImageToClient(New PixelPictureRegion(imageRect.X, imageRect.Y, imageRect.Width, imageRect.Height))
    End Function

    ''' <summary>将原始图像像素区域转换为客户区矩形，应用实时缩放、居中和滚动偏移。</summary>
    Public Function ImageToClient(imageRect As PixelPictureRegion) As RectangleF
        Dim viewport = ViewportBounds()
        Dim imageWidth = If(_source Is Nothing, 0, _source.Width * _zoom)
        Dim imageHeight = If(_source Is Nothing, 0, _source.Height * _zoom)
        Dim offsetX = If(imageWidth < viewport.Width, (viewport.Width - imageWidth) / 2.0, 0)
        Dim offsetY = If(imageHeight < viewport.Height, (viewport.Height - imageHeight) / 2.0, 0)
        Return New RectangleF(CSng(viewport.X + offsetX + imageRect.X * _zoom - _scrollX), CSng(viewport.Y + offsetY + imageRect.Y * _zoom - _scrollY), CSng(imageRect.Width * _zoom), CSng(imageRect.Height * _zoom))
    End Function

    ''' <summary>将客户区坐标转换为原始图像像素坐标；无源时返回 (-1, -1)，图片外坐标不钳制。</summary>
    Public Function ClientToImage(client As Point) As PixelPicturePoint
        If _source Is Nothing OrElse _zoom <= 0 Then Return New PixelPicturePoint(-1, -1)
        Dim viewport = ViewportBounds()
        Dim imageWidth = _source.Width * _zoom
        Dim imageHeight = _source.Height * _zoom
        Dim offsetX = If(imageWidth < viewport.Width, (viewport.Width - imageWidth) / 2.0, 0)
        Dim offsetY = If(imageHeight < viewport.Height, (viewport.Height - imageHeight) / 2.0, 0)
        Return New PixelPicturePoint((client.X - viewport.X - offsetX + _scrollX) / _zoom, (client.Y - viewport.Y - offsetY + _scrollY) / _zoom)
    End Function

    Private Function UpdateScrollRegion() As Boolean
        If _source Is Nothing Then
            _scrollX = 0
            _scrollY = 0
            _showVerticalScroll = False
            _showHorizontalScroll = False
            _verticalTrack = RectangleF.Empty
            _horizontalTrack = RectangleF.Empty
            Return False
        End If
        Dim full = FullViewportBounds()
        Dim reserve = _scrollBarWidth * DpiScale() + 2
        _showVerticalScroll = _source.Height * _zoom > full.Height + 0.001
        _showHorizontalScroll = _source.Width * _zoom > full.Width + 0.001
        If _showVerticalScroll AndAlso _source.Width * _zoom > full.Width - reserve Then _showHorizontalScroll = True
        If _showHorizontalScroll AndAlso _source.Height * _zoom > full.Height - reserve Then _showVerticalScroll = True
        Dim viewport = ViewportBounds()
        Dim maxX = Math.Max(0, _source.Width * _zoom - viewport.Width)
        Dim maxY = Math.Max(0, _source.Height * _zoom - viewport.Height)
        Dim oldX = _scrollX
        Dim oldY = _scrollY
        _scrollX = Math.Max(0, Math.Min(maxX, _scrollX))
        _scrollY = Math.Max(0, Math.Min(maxY, _scrollY))
        Dim barWidth = Math.Max(1, _scrollBarWidth * DpiScale())
        _verticalTrack = If(_showVerticalScroll, New RectangleF(viewport.Right + 2, viewport.Top, barWidth, viewport.Height), RectangleF.Empty)
        _horizontalTrack = If(_showHorizontalScroll, New RectangleF(viewport.Left, viewport.Bottom + 2, viewport.Width, barWidth), RectangleF.Empty)
        Dim thumbHeight = Math.Min(viewport.Height, Math.Max(20 * DpiScale(), viewport.Height * CDbl(viewport.Height) / (_source.Height * _zoom)))
        Dim thumbWidth = Math.Min(viewport.Width, Math.Max(20 * DpiScale(), viewport.Width * CDbl(viewport.Width) / (_source.Width * _zoom)))
        _verticalThumb = New RectangleF(_verticalTrack.X, CSng(viewport.Y + If(maxY > 0, _scrollY / maxY * (viewport.Height - thumbHeight), 0)), barWidth, CSng(thumbHeight))
        _horizontalThumb = New RectangleF(CSng(viewport.X + If(maxX > 0, _scrollX / maxX * (viewport.Width - thumbWidth), 0)), _horizontalTrack.Y, CSng(thumbWidth), barWidth)
        Return oldX <> _scrollX OrElse oldY <> _scrollY
    End Function

    Private Sub DrawScrollBars(context As D3D_PaintContext)
        If Not _verticalTrack.IsEmpty Then
            context.FillRoundedRectangle(_verticalTrack, _verticalTrack.Width / 2, _scrollBarTrackColor)
            context.FillRoundedRectangle(_verticalThumb, _verticalThumb.Width / 2, If(_dragMode = DragMode.VerticalScroll OrElse (_pointerInside AndAlso _verticalThumb.Contains(_pointerLocation)), _scrollBarThumbHoverColor, _scrollBarThumbColor))
        End If
        If Not _horizontalTrack.IsEmpty Then
            context.FillRoundedRectangle(_horizontalTrack, _horizontalTrack.Height / 2, _scrollBarTrackColor)
            context.FillRoundedRectangle(_horizontalThumb, _horizontalThumb.Height / 2, If(_dragMode = DragMode.HorizontalScroll OrElse (_pointerInside AndAlso _horizontalThumb.Contains(_pointerLocation)), _scrollBarThumbHoverColor, _scrollBarThumbColor))
        End If
    End Sub

    Public Function TryGetPixelAt(location As Point, ByRef pixel As PixelPicturePixel) As Boolean
        pixel = Nothing
        If _source Is Nothing OrElse Not ViewportBounds().Contains(location) Then Return False
        Dim imagePoint = ClientToImage(location)
        If imagePoint.X < 0 OrElse imagePoint.Y < 0 OrElse imagePoint.X >= _source.Width OrElse imagePoint.Y >= _source.Height Then Return False
        Return _source.TryGetPixel(CLng(Math.Floor(imagePoint.X)), CLng(Math.Floor(imagePoint.Y)), _frameIndex, pixel)
    End Function

    Private Sub UpdatePixelUnderPointer()
        Dim pixel As PixelPicturePixel
        If _pointerInside AndAlso TryGetPixelAt(_pointerLocation, pixel) Then
            Dim key = $"{pixel.X}:{pixel.Y}:{_frameIndex}"
            If key <> _lastPixelKey Then
                _lastPixelKey = key
                RaiseEvent PixelHovered(Me, New PixelPicturePixelEventArgs(_pointerLocation, pixel))
            End If
        ElseIf _lastPixelKey IsNot Nothing Then
            _lastPixelKey = Nothing
            RaiseEvent PixelHovered(Me, New PixelPicturePixelEventArgs(_pointerLocation))
        End If
    End Sub

    Private Function ClampSelection(value As Rectangle) As Rectangle
        If _source Is Nothing OrElse value.Width <= 0 OrElse value.Height <= 0 Then Return Rectangle.Empty
        Dim bounds = New Rectangle(0, 0, CInt(Math.Min(Integer.MaxValue, _source.Width)), CInt(Math.Min(Integer.MaxValue, _source.Height)))
        Return Rectangle.Intersect(value, bounds)
    End Function

    Private Function CenterSelection(value As Rectangle) As Rectangle
        If Not _selectionForceCenter OrElse _source Is Nothing OrElse value.Width <= 0 OrElse value.Height <= 0 Then Return value
        Return New Rectangle(Math.Max(0, CInt((_source.Width - value.Width) \ 2)), Math.Max(0, CInt((_source.Height - value.Height) \ 2)), value.Width, value.Height)
    End Function

    Private Function HandleBounds(handle As HandlePosition, selection As RectangleF) As RectangleF
        Dim half = _handleSize / 2.0F
        Dim x = If(handle = HandlePosition.TopLeft OrElse handle = HandlePosition.MiddleLeft OrElse handle = HandlePosition.BottomLeft, selection.Left, If(handle = HandlePosition.TopRight OrElse handle = HandlePosition.MiddleRight OrElse handle = HandlePosition.BottomRight, selection.Right, selection.Left + selection.Width / 2.0F))
        Dim y = If(handle = HandlePosition.TopLeft OrElse handle = HandlePosition.TopCenter OrElse handle = HandlePosition.TopRight, selection.Top, If(handle = HandlePosition.BottomLeft OrElse handle = HandlePosition.BottomCenter OrElse handle = HandlePosition.BottomRight, selection.Bottom, selection.Top + selection.Height / 2.0F))
        Return New RectangleF(x - half, y - half, _handleSize, _handleSize)
    End Function

    Private Function SelectionClientBounds() As RectangleF
        If Not _showSelection OrElse _selectionRect.Width <= 0 OrElse _selectionRect.Height <= 0 Then Return RectangleF.Empty
        Return ImageToClient(New RectangleF(_selectionRect.X, _selectionRect.Y, _selectionRect.Width, _selectionRect.Height))
    End Function

    Private Function HitHandle(location As Point, screen As RectangleF) As HandlePosition
        If Not _showSelection OrElse _selectionRect.Width <= 0 Then Return HandlePosition.None
        For Each position In HandleList
            Dim bounds = HandleBounds(position, screen)
            bounds.Inflate(_handleHitTolerance, _handleHitTolerance)
            If bounds.Contains(location) Then Return position
        Next
        Return HandlePosition.None
    End Function

    Private Function HitSelectionBorder(location As Point, screen As RectangleF) As Boolean
        If Not _showSelection OrElse _selectionRect.Width <= 0 Then Return False
        Dim outer = screen
        outer.Inflate(_borderHitTolerance, _borderHitTolerance)
        Dim inner = screen
        inner.Inflate(-_borderHitTolerance, -_borderHitTolerance)
        Return outer.Contains(location) AndAlso Not inner.Contains(location)
    End Function

    Private Function CursorForHandle(handle As HandlePosition) As Cursor
        Select Case handle
            Case HandlePosition.TopLeft, HandlePosition.BottomRight
                Return Cursors.SizeNWSE
            Case HandlePosition.TopRight, HandlePosition.BottomLeft
                Return Cursors.SizeNESW
            Case HandlePosition.TopCenter, HandlePosition.BottomCenter
                Return Cursors.SizeNS
            Case HandlePosition.MiddleLeft, HandlePosition.MiddleRight
                Return Cursors.SizeWE
            Case Else
                Return Cursors.Default
        End Select
    End Function

    Private Sub UpdateInteractionCursor(location As Point)
        If _dragMode <> DragMode.None Then
            Select Case _dragMode
                Case DragMode.ResizeSelection
                    Cursor = CursorForHandle(_activeHandle)
                Case DragMode.MoveSelection, DragMode.Pan
                    Cursor = Cursors.SizeAll
                Case DragMode.DrawSelection
                    Cursor = Cursors.Cross
                Case DragMode.VerticalScroll
                    Cursor = Cursors.SizeNS
                Case DragMode.HorizontalScroll
                    Cursor = Cursors.SizeWE
                Case Else
                    Cursor = Cursors.Default
            End Select
            Return
        End If

        If Not _pointerInside OrElse _source Is Nothing OrElse Not _showSelection Then
            Cursor = Cursors.Default
            Return
        End If

        Dim selection = SelectionClientBounds()
        Dim handle = HitHandle(location, selection)
        If handle <> HandlePosition.None Then
            Cursor = CursorForHandle(handle)
        ElseIf HitSelectionBorder(location, selection) AndAlso Not _selectionForceCenter Then
            Cursor = Cursors.SizeAll
        Else
            Cursor = Cursors.Default
        End If
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        Focus()
        If _source Is Nothing Then Return
        StopZoomInteraction()
        UpdateScrollRegion()
        If e.Button = MouseButtons.Left Then
            If _showVerticalScroll AndAlso _verticalThumb.Contains(e.Location) Then
                _dragMode = DragMode.VerticalScroll
            ElseIf _showHorizontalScroll AndAlso _horizontalThumb.Contains(e.Location) Then
                _dragMode = DragMode.HorizontalScroll
            ElseIf _verticalTrack.Contains(e.Location) Then
                _scrollY += If(e.Y < _verticalThumb.Top, -ViewportBounds().Height, ViewportBounds().Height)
                UpdateScrollRegion()
                RequestRender()
                Return
            ElseIf _horizontalTrack.Contains(e.Location) Then
                _scrollX += If(e.X < _horizontalThumb.Left, -ViewportBounds().Width, ViewportBounds().Width)
                UpdateScrollRegion()
                RequestRender()
                Return
            Else
                Dim selection = SelectionClientBounds()
                Dim position = HitHandle(e.Location, selection)
                If position <> HandlePosition.None Then
                    _dragMode = DragMode.ResizeSelection
                    _activeHandle = position
                ElseIf HitSelectionBorder(e.Location, selection) AndAlso Not _selectionForceCenter Then
                    _dragMode = DragMode.MoveSelection
                Else
                    _dragMode = DragMode.Pan
                End If
            End If
            _dragStart = e.Location
            _dragScrollX = _scrollX
            _dragScrollY = _scrollY
            _dragSelectionStart = _selectionRect
            Capture = True
            UpdateInteractionCursor(e.Location)
        ElseIf e.Button = MouseButtons.Right AndAlso _showSelection Then
            _dragMode = DragMode.DrawSelection
            _dragStart = e.Location
            Dim imagePoint = ClientToImage(e.Location)
            _dragSelectionStart = New Rectangle(CInt(Math.Max(0, Math.Min(_source.Width - 1, Math.Floor(imagePoint.X)))), CInt(Math.Max(0, Math.Min(_source.Height - 1, Math.Floor(imagePoint.Y)))), 0, 0)
            _selectionRect = _dragSelectionStart
            Capture = True
            UpdateInteractionCursor(e.Location)
            RequestRender()
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        _pointerLocation = e.Location
        _pointerInside = ClientRectangle.Contains(e.Location)
        UpdateInteractionCursor(e.Location)
        Select Case _dragMode
            Case DragMode.Pan
                _scrollX = _dragScrollX - (e.X - _dragStart.X)
                _scrollY = _dragScrollY - (e.Y - _dragStart.Y)
                UpdateScrollRegion()
            Case DragMode.MoveSelection
                Dim startPoint = ClientToImage(_dragStart)
                Dim currentPoint = ClientToImage(e.Location)
                Dim dx = CInt(Math.Round(currentPoint.X - startPoint.X))
                Dim dy = CInt(Math.Round(currentPoint.Y - startPoint.Y))
                _selectionRect = CenterSelection(ClampSelection(New Rectangle(_dragSelectionStart.X + dx, _dragSelectionStart.Y + dy, _dragSelectionStart.Width, _dragSelectionStart.Height)))
            Case DragMode.DrawSelection
                UpdateSelectionFromDrag(e.Location)
            Case DragMode.ResizeSelection
                UpdateSelectionFromResize(e.Location)
            Case DragMode.VerticalScroll
                Dim travel = _verticalTrack.Height - _verticalThumb.Height
                If travel > 0 Then _scrollY = _dragScrollY + (e.Y - _dragStart.Y) * (_source.Height * _zoom - ViewportBounds().Height) / travel
                UpdateScrollRegion()
            Case DragMode.HorizontalScroll
                Dim travel = _horizontalTrack.Width - _horizontalThumb.Width
                If travel > 0 Then _scrollX = _dragScrollX + (e.X - _dragStart.X) * (_source.Width * _zoom - ViewportBounds().Width) / travel
                UpdateScrollRegion()
        End Select
        UpdatePixelUnderPointer()
        RequestRender()
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        Dim mode = _dragMode
        _dragMode = DragMode.None
        _activeHandle = HandlePosition.None
        Capture = False
        _pointerLocation = e.Location
        _pointerInside = ClientRectangle.Contains(e.Location)
        UpdateInteractionCursor(e.Location)
        If mode = DragMode.MoveSelection OrElse mode = DragMode.ResizeSelection OrElse mode = DragMode.DrawSelection Then RaiseEvent SelectionChanged(Me, EventArgs.Empty)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        _pointerInside = False
        If Not Capture Then Cursor = Cursors.Default
        UpdatePixelUnderPointer()
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        If _source Is Nothing OrElse e.Delta = 0 OrElse Not ViewportBounds().Contains(e.Location) Then Return
        ZoomTo(_targetZoom * Math.Pow(1.25, e.Delta / 120.0), e.Location)
    End Sub

    Private Sub UpdateSelectionFromDrag(location As Point)
        Dim currentPoint = ClientToImage(location)
        Dim x = CInt(Math.Clamp(Math.Floor(currentPoint.X), 0, Math.Min(Integer.MaxValue - 1L, _source.Width)))
        Dim y = CInt(Math.Clamp(Math.Floor(currentPoint.Y), 0, Math.Min(Integer.MaxValue - 1L, _source.Height)))
        Dim left = Math.Min(_dragSelectionStart.X, x)
        Dim top = Math.Min(_dragSelectionStart.Y, y)
        Dim width = Math.Abs(x - _dragSelectionStart.X)
        Dim height = Math.Abs(y - _dragSelectionStart.Y)
        If _selectionAspectRatio > 0 AndAlso width > 0 Then
            height = Math.Max(1, CInt(Math.Min(Integer.MaxValue - 1.0, Math.Round(width / _selectionAspectRatio))))
            If y < _dragSelectionStart.Y Then top = _dragSelectionStart.Y - height
        End If
        _selectionRect = CenterSelection(ClampSelection(New Rectangle(left, top, width, height)))
    End Sub

    Private Sub UpdateSelectionFromResize(location As Point)
        Dim imagePoint = ClientToImage(location)
        Dim sourceRect = _dragSelectionStart
        Dim left = sourceRect.Left
        Dim top = sourceRect.Top
        Dim right = sourceRect.Right
        Dim bottom = sourceRect.Bottom
        Select Case _activeHandle
            Case HandlePosition.TopLeft : left = CInt(imagePoint.X) : top = CInt(imagePoint.Y)
            Case HandlePosition.TopCenter : top = CInt(imagePoint.Y)
            Case HandlePosition.TopRight : right = CInt(imagePoint.X) : top = CInt(imagePoint.Y)
            Case HandlePosition.MiddleLeft : left = CInt(imagePoint.X)
            Case HandlePosition.MiddleRight : right = CInt(imagePoint.X)
            Case HandlePosition.BottomLeft : left = CInt(imagePoint.X) : bottom = CInt(imagePoint.Y)
            Case HandlePosition.BottomCenter : bottom = CInt(imagePoint.Y)
            Case HandlePosition.BottomRight : right = CInt(imagePoint.X) : bottom = CInt(imagePoint.Y)
        End Select
        If right < left Then Dim swap = left : left = right : right = swap
        If bottom < top Then Dim swap = top : top = bottom : bottom = swap
        Dim result = New Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top))
        If _selectionAspectRatio > 0 Then result.Height = Math.Max(1, CInt(Math.Round(result.Width / _selectionAspectRatio)))
        _selectionRect = CenterSelection(ClampSelection(result))
    End Sub

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        StopZoomInteraction()
        UpdateZoomRange()
        _zoomMotion.Reset(Math.Log(_zoom))
        UpdateScrollRegion()
        RequestRender()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        RequestRender()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        StopZoomInteraction()
        UpdateZoomRange()
        _zoomMotion.Reset(Math.Log(_zoom))
        UpdateScrollRegion()
        RequestRender()
    End Sub

    Private Function DpiScale() As Single
        Return D3D_DpiContext.FromControl(Me).Scale
    End Function

    Private Sub RequestRender()
        If IsDisposed Then Return
        D3D_InvalidationRouter.RequestRender(Me, ClientRectangle)
    End Sub

    Private Shared Function MillisecondsToTicks(milliseconds As Integer) As Long
        Return Math.Max(1, CLng(Stopwatch.Frequency * (Math.Max(1, milliseconds) / 1000.0)))
    End Function

    Private Sub OnDisposed(sender As Object, e As EventArgs) Handles MyBase.Disposed
        StopMotion()
        _motion.Dispose()
    End Sub

    Private Sub DisposeD2DResources()
        StopMotion()
        If _sourceNotification IsNot Nothing Then RemoveHandler _sourceNotification.ContentChanged, AddressOf SourceContentChanged
        _motion.Dispose()
        _source = Nothing
    End Sub

    Private Sub StopZoomInteraction()
        _zoomRunning = False
        _targetZoom = _zoom
        _zoomMotion.Reset(Math.Log(Math.Max(Double.Epsilon, _zoom)))
        UpdateMotionState()
    End Sub

    Protected Overrides Sub OnMouseCaptureChanged(args As EventArgs)
        MyBase.OnMouseCaptureChanged(args)
        If Not Capture Then
            _dragMode = DragMode.None
            _activeHandle = HandlePosition.None
            Cursor = Cursors.Default
        End If
    End Sub

    Protected Overrides Sub OnKeyDown(args As KeyEventArgs)
        MyBase.OnKeyDown(args)
        If args.KeyCode = Keys.Delete Then
            ClearSelection()
            args.Handled = True
        End If
    End Sub

    Private Sub SourceContentChanged(sender As Object, args As EventArgs)
        If IsDisposed OrElse Not IsHandleCreated OrElse Threading.Interlocked.Exchange(_refreshQueued, 1) <> 0 Then Return
        Try
            BeginInvoke(Sub()
                            Threading.Interlocked.Exchange(_refreshQueued, 0)
                            If IsDisposed Then Return
                            UpdatePixelUnderPointer()
                            RequestRender()
                        End Sub)
        Catch ex As InvalidOperationException
            Threading.Interlocked.Exchange(_refreshQueued, 0)
        End Try
    End Sub

    Protected Overrides Sub OnHandleCreated(args As EventArgs)
        MyBase.OnHandleCreated(args)
        UpdateMotionState()
    End Sub

    Protected Overrides Sub OnVisibleChanged(args As EventArgs)
        MyBase.OnVisibleChanged(args)
        UpdateMotionState()
    End Sub

    Protected Overrides Sub OnHandleDestroyed(args As EventArgs)
        StopMotion()
        Threading.Interlocked.Exchange(_refreshQueued, 0)
        MyBase.OnHandleDestroyed(args)
    End Sub
End Class
