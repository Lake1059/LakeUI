Imports System.ComponentModel
Imports System.Drawing.Drawing2D

''' <summary>
''' 像素级缩放图片框控件，使用最近邻插值保持像素清晰。
''' 支持自定义滚动条、边框、鼠标拖拽平移、滚轮缩放，以及可调整大小的矩形框选功能。
''' </summary>
<DefaultEvent("SelectionChanged")>
Public Class PixelPictureBox

    ''' <summary>当框选区域发生变化时引发。</summary>
    Public Event SelectionChanged As EventHandler

#Region "构造"

    Public Sub New()
        InitializeComponent()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.Selectable, True)
        UpdateStyles()
    End Sub

#End Region

#Region "辅助方法"

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            更新滚动区域()
            Me.Invalidate()
        End If
    End Sub

    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function

    ''' <summary>如果启用了强制居中模式，则将框选矩形居中到图片中心；否则原样返回。</summary>
    Private Function 应用强制居中(r As Rectangle) As Rectangle
        If Not _selectionForceCenter OrElse _image Is Nothing Then Return r
        Dim cx As Integer = (_image.Width - r.Width) \ 2
        Dim cy As Integer = (_image.Height - r.Height) \ 2
        Return New Rectangle(Math.Max(0, cx), Math.Max(0, cy), r.Width, r.Height)
    End Function

#End Region

#Region "外观属性 - 背景"

    Private 背景颜色 As Color = Color.FromArgb(36, 36, 36)
    <Category("LakeUI"), Description("背景颜色"), DefaultValue(GetType(Color), "36, 36, 36"), Browsable(True)>
    Public Property BackColor1 As Color
        Get
            Return 背景颜色
        End Get
        Set(value As Color)
            SetValue(背景颜色, value)
        End Set
    End Property

#End Region

#Region "外观属性 - 边框"

    Private 边框颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("边框颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property BorderColor As Color
        Get
            Return 边框颜色
        End Get
        Set(value As Color)
            SetValue(边框颜色, value)
        End Set
    End Property

    Private 边框宽度 As Integer = 1
    <Category("LakeUI"), Description("边框宽度"), DefaultValue(GetType(Integer), "1"), Browsable(True)>
    Public Property BorderSize As Integer
        Get
            Return 边框宽度
        End Get
        Set(value As Integer)
            SetValue(边框宽度, value)
        End Set
    End Property

#End Region

#Region "外观属性 - 滚动条"

    Private 滚动条宽度 As Integer = 10
    <Category("LakeUI"), Description("滚动条宽度"), DefaultValue(GetType(Integer), "10"), Browsable(True)>
    Public Property ScrollBarWidth As Integer
        Get
            Return 滚动条宽度
        End Get
        Set(value As Integer)
            SetValue(滚动条宽度, value)
        End Set
    End Property

    Private 滚动条轨道颜色 As Color = Color.FromArgb(20, 20, 20)
    <Category("LakeUI"), Description("滚动条轨道颜色"), DefaultValue(GetType(Color), "20, 20, 20"), Browsable(True)>
    Public Property ScrollBarTrackColor As Color
        Get
            Return 滚动条轨道颜色
        End Get
        Set(value As Color)
            SetValue(滚动条轨道颜色, value)
        End Set
    End Property

    Private 滚动条滑块颜色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("滚动条滑块颜色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
    Public Property ScrollBarThumbColor As Color
        Get
            Return 滚动条滑块颜色
        End Get
        Set(value As Color)
            SetValue(滚动条滑块颜色, value)
        End Set
    End Property

    Private 滚动条悬停颜色 As Color = Color.FromArgb(120, 120, 120)
    <Category("LakeUI"), Description("滚动条滑块悬停颜色"), DefaultValue(GetType(Color), "120, 120, 120"), Browsable(True)>
    Public Property ScrollBarThumbHoverColor As Color
        Get
            Return 滚动条悬停颜色
        End Get
        Set(value As Color)
            SetValue(滚动条悬停颜色, value)
        End Set
    End Property

#End Region

#Region "外观属性 - 框选"

    Private 框选边框颜色 As Color = Color.FromArgb(200, 60, 60)
    <Category("LakeUI"), Description("框选矩形的边框颜色"), DefaultValue(GetType(Color), "200, 60, 60"), Browsable(True)>
    Public Property SelectionBorderColor As Color
        Get
            Return 框选边框颜色
        End Get
        Set(value As Color)
            SetValue(框选边框颜色, value)
        End Set
    End Property

    Private 框选边框宽度 As Integer = 2
    <Category("LakeUI"), Description("框选矩形的边框宽度"), DefaultValue(GetType(Integer), "2"), Browsable(True)>
    Public Property SelectionBorderSize As Integer
        Get
            Return 框选边框宽度
        End Get
        Set(value As Integer)
            SetValue(框选边框宽度, value)
        End Set
    End Property

    Private 框选填充颜色 As Color = Color.FromArgb(30, 200, 60, 60)
    <Category("LakeUI"), Description("框选矩形的填充颜色（半透明）"), DefaultValue(GetType(Color), "30, 200, 60, 60"), Browsable(True)>
    Public Property SelectionFillColor As Color
        Get
            Return 框选填充颜色
        End Get
        Set(value As Color)
            SetValue(框选填充颜色, value)
        End Set
    End Property

    Private 手柄颜色 As Color = Color.White
    <Category("LakeUI"), Description("框选手柄的颜色"), DefaultValue(GetType(Color), "White"), Browsable(True)>
    Public Property HandleColor As Color
        Get
            Return 手柄颜色
        End Get
        Set(value As Color)
            SetValue(手柄颜色, value)
        End Set
    End Property

    Private 手柄边框颜色 As Color = Color.FromArgb(200, 60, 60)
    <Category("LakeUI"), Description("框选手柄的边框颜色"), DefaultValue(GetType(Color), "200, 60, 60"), Browsable(True)>
    Public Property HandleBorderColor As Color
        Get
            Return 手柄边框颜色
        End Get
        Set(value As Color)
            SetValue(手柄边框颜色, value)
        End Set
    End Property

    Private 手柄尺寸 As Integer = 10
    <Category("LakeUI"), Description("框选手柄的边长"), DefaultValue(GetType(Integer), "10"), Browsable(True)>
    Public Property HandleSize As Integer
        Get
            Return 手柄尺寸
        End Get
        Set(value As Integer)
            SetValue(手柄尺寸, value)
        End Set
    End Property

    Private 手柄触发范围 As Integer = 6
    <Category("LakeUI"), Description("手柄命中测试时向外扩展的额外像素距离"), DefaultValue(GetType(Integer), "6"), Browsable(True)>
    Public Property HandleHitTolerance As Integer
        Get
            Return 手柄触发范围
        End Get
        Set(value As Integer)
            手柄触发范围 = Math.Max(0, value)
        End Set
    End Property

    Private 框线触发范围 As Integer = 6
    <Category("LakeUI"), Description("框选边线命中测试的容差像素距离"), DefaultValue(GetType(Integer), "6"), Browsable(True)>
    Public Property BorderHitTolerance As Integer
        Get
            Return 框线触发范围
        End Get
        Set(value As Integer)
            框线触发范围 = Math.Max(1, value)
        End Set
    End Property

#End Region

#Region "行为属性"

    Private _image As Image = Nothing
    <Category("LakeUI"), Description("要显示的图片"), DefaultValue(GetType(Image), Nothing), Browsable(True)>
    Public Property Image As Image
        Get
            Return _image
        End Get
        Set(value As Image)
            _image = value
            _zoomFactor = 0
            更新缩放范围()
            更新滚动区域()
            Me.Invalidate()
        End Set
    End Property

    Private 最大像素边长 As Integer = 64
    <Category("LakeUI"), Description("缩放到最大时每个像素的最大边长（屏幕像素）"), DefaultValue(GetType(Integer), "64"), Browsable(True)>
    Public Property MaxPixelSize As Integer
        Get
            Return 最大像素边长
        End Get
        Set(value As Integer)
            最大像素边长 = Math.Max(1, value)
            更新缩放范围()
            更新滚动区域()
            Me.Invalidate()
        End Set
    End Property

    Private _showSelection As Boolean = True
    <Category("LakeUI"), Description("是否显示框选矩形"), DefaultValue(GetType(Boolean), "True"), Browsable(True)>
    Public Property ShowSelection As Boolean
        Get
            Return _showSelection
        End Get
        Set(value As Boolean)
            SetValue(_showSelection, value)
        End Set
    End Property

    Private _selectionAspectRatio As Single = 0
    ''' <summary>框选矩形的固定宽高比（Width / Height）。0 表示自由比例，> 0 表示固定比例（例如 1.0 为正方形，1.778 为 16:9）。</summary>
    <Category("LakeUI"), Description("框选矩形的固定宽高比（W/H），0 表示自由比例"), DefaultValue(GetType(Single), "0"), Browsable(True)>
    Public Property SelectionAspectRatio As Single
        Get
            Return _selectionAspectRatio
        End Get
        Set(value As Single)
            _selectionAspectRatio = Math.Max(0, value)
        End Set
    End Property

    Private _selectionForceCenter As Boolean = False
    ''' <summary>是否强制将框选矩形居中到图片中心。启用后移动操作被禁用，所有尺寸调整后自动居中。</summary>
    <Category("LakeUI"), Description("是否强制框选居中到图片中心"), DefaultValue(GetType(Boolean), "False"), Browsable(True)>
    Public Property SelectionForceCenter As Boolean
        Get
            Return _selectionForceCenter
        End Get
        Set(value As Boolean)
            If _selectionForceCenter <> value Then
                _selectionForceCenter = value
                If value AndAlso HasSelection Then
                    _selectionRect = 应用强制居中(_selectionRect)
                End If
                Me.Invalidate()
            End If
        End Set
    End Property

    ''' <summary>获取或设置框选区域（图片像素坐标）。启用强制居中时，设置的位置会被覆盖为居中位置。</summary>
    <Category("LakeUI"), Description("框选区域，以图片像素坐标表示"), Browsable(True), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SelectionRect As Rectangle
        Get
            Return _selectionRect
        End Get
        Set(value As Rectangle)
            _selectionRect = 应用强制居中(value)
            Me.Invalidate()
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Set
    End Property

    ''' <summary>获取或设置框选区域的尺寸（图片像素）。启用强制居中时自动居中，否则保持当前位置。</summary>
    <Category("LakeUI"), Description("框选区域的尺寸"), Browsable(True), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SelectionSize As Size
        Get
            Return _selectionRect.Size
        End Get
        Set(value As Size)
            Dim r As New Rectangle(_selectionRect.Location, value)
            _selectionRect = 应用强制居中(约束矩形到图片(r))
            Me.Invalidate()
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Set
    End Property

    ''' <summary>框选矩形的角落枚举。</summary>
    Public Enum SelectionCorner
        TopLeft
        TopRight
        BottomLeft
        BottomRight
    End Enum

    ''' <summary>清除当前框选区域。</summary>
    Public Sub ClearSelection()
        _selectionRect = New Rectangle(0, 0, 0, 0)
        Me.Invalidate()
        RaiseEvent SelectionChanged(Me, EventArgs.Empty)
    End Sub

    ''' <summary>
    ''' 获取框选矩形指定角落的放大镜视图。
    ''' 以角落像素为中心，在指定视野半径内以像素级放大渲染，返回包含框选边线的成品 Image。
    ''' </summary>
    ''' <param name="corner">要查看的角落。</param>
    ''' <param name="viewRadius">视野半径（图片像素数），实际视野为 (2*viewRadius) x (2*viewRadius)。</param>
    ''' <param name="pixelSize">每个图片像素在输出图片中的边长（屏幕像素）。</param>
    ''' <returns>渲染好的放大镜 Image，调用方负责 Dispose；若无图片或无框选则返回 Nothing。</returns>
    Public Function GetCornerMagnifier(corner As SelectionCorner, viewRadius As Integer, pixelSize As Integer) As Image
        If _image Is Nothing Then Return Nothing
        If Not HasSelection Then Return Nothing
        If viewRadius < 1 OrElse pixelSize < 1 Then Return Nothing

        ' 确定角落的图片像素坐标
        Dim cornerX As Integer
        Dim cornerY As Integer
        Select Case corner
            Case SelectionCorner.TopLeft
                cornerX = _selectionRect.Left
                cornerY = _selectionRect.Top
            Case SelectionCorner.TopRight
                cornerX = _selectionRect.Right
                cornerY = _selectionRect.Top
            Case SelectionCorner.BottomLeft
                cornerX = _selectionRect.Left
                cornerY = _selectionRect.Bottom
            Case SelectionCorner.BottomRight
                cornerX = _selectionRect.Right
                cornerY = _selectionRect.Bottom
            Case Else
                Return Nothing
        End Select

        Dim diameter As Integer = viewRadius * 2
        Dim outSize As Integer = diameter * pixelSize
        Dim bmp As New Bitmap(outSize, outSize, Imaging.PixelFormat.Format32bppArgb)

        ' 视野在图片坐标系中的起始位置
        Dim srcX0 As Integer = cornerX - viewRadius
        Dim srcY0 As Integer = cornerY - viewRadius

        Using g As Graphics = Graphics.FromImage(bmp)
            g.InterpolationMode = InterpolationMode.NearestNeighbor
            g.PixelOffsetMode = PixelOffsetMode.Half
            g.SmoothingMode = SmoothingMode.None
            g.Clear(背景颜色)

            ' 计算视野与图片的交集区域，一次性绘制
            Dim clampL As Integer = Math.Max(0, srcX0)
            Dim clampT As Integer = Math.Max(0, srcY0)
            Dim clampR As Integer = Math.Min(_image.Width, srcX0 + diameter)
            Dim clampB As Integer = Math.Min(_image.Height, srcY0 + diameter)
            If clampR > clampL AndAlso clampB > clampT Then
                Dim srcRect As New RectangleF(clampL, clampT, clampR - clampL, clampB - clampT)
                Dim destRect As New RectangleF(
                    CSng(clampL - srcX0) * pixelSize, CSng(clampT - srcY0) * pixelSize,
                    CSng(clampR - clampL) * pixelSize, CSng(clampB - clampT) * pixelSize)
                g.DrawImage(_image, destRect, srcRect, GraphicsUnit.Pixel)
            End If

            ' 绘制框选边线（将框选矩形映射到放大镜坐标系）
            ' 框选边缘在图片坐标中为 _selectionRect.Left/Top/Right/Bottom
            ' 在放大镜中，图片像素 p 对应输出位置 (p - srcX0) * pixelSize
            Dim selL As Single = CSng(_selectionRect.Left - srcX0) * pixelSize
            Dim selT As Single = CSng(_selectionRect.Top - srcY0) * pixelSize
            Dim selR As Single = CSng(_selectionRect.Right - srcX0) * pixelSize
            Dim selB As Single = CSng(_selectionRect.Bottom - srcY0) * pixelSize

            Dim halfPen As Single = 框选边框宽度 / 2.0F
            Dim lineRect As New RectangleF(selL + halfPen, selT + halfPen,
                                            (selR - selL) - 框选边框宽度, (selB - selT) - 框选边框宽度)

            Using pen As New Pen(框选边框颜色, 框选边框宽度)
                pen.Alignment = PenAlignment.Center
                g.DrawRectangle(pen, lineRect.X, lineRect.Y, lineRect.Width, lineRect.Height)
            End Using
        End Using

        Return bmp
    End Function

#End Region

#Region "内部状态"

    Private _zoomFactor As Single = 0
    Private _minZoom As Single = 1
    Private _maxZoom As Single = 64

    Private _scrollX As Integer = 0
    Private _scrollY As Integer = 0

    Private ReadOnly _vScrollBar As New ScrollBarRenderer()
    Private ReadOnly _hScrollBar As New ScrollBarRenderer()

    Private _showVScroll As Boolean = False
    Private _showHScroll As Boolean = False

    Private _selectionRect As New Rectangle(0, 0, 0, 0)

    Private ReadOnly Property HasSelection As Boolean
        Get
            Return _selectionRect.Width > 0 OrElse _selectionRect.Height > 0
        End Get
    End Property

    Private Enum DragMode
        None
        Pan
        MoveSelection
        ResizeSelection
        DrawSelection
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

    Private Shared ReadOnly AllHandlePositions As HandlePosition() = {
        HandlePosition.TopLeft, HandlePosition.TopCenter, HandlePosition.TopRight,
        HandlePosition.MiddleLeft, HandlePosition.MiddleRight,
        HandlePosition.BottomLeft, HandlePosition.BottomCenter, HandlePosition.BottomRight}

    Private _dragMode As DragMode = DragMode.None
    Private _dragStart As Point
    Private _dragScrollStart As Point
    Private _dragSelectionStart As Rectangle
    Private _activeHandle As HandlePosition = HandlePosition.None

#End Region

#Region "缩放计算"

    Private Sub 更新缩放范围()
        If _image Is Nothing Then
            _minZoom = 1
            _maxZoom = 1
            _zoomFactor = 1
            Return
        End If

        Dim viewport As Size = 获取视口大小()
        If viewport.Width < 1 OrElse viewport.Height < 1 Then
            _minZoom = 1
        Else
            Dim zoomW As Single = CSng(viewport.Width) / _image.Width
            Dim zoomH As Single = CSng(viewport.Height) / _image.Height
            _minZoom = Math.Min(zoomW, zoomH)
        End If

        _maxZoom = CSng(最大像素边长)

        If _minZoom > _maxZoom Then _maxZoom = _minZoom

        If _zoomFactor < _minZoom OrElse _zoomFactor = 0 Then
            _zoomFactor = _minZoom
        End If
        If _zoomFactor > _maxZoom Then
            _zoomFactor = _maxZoom
        End If
    End Sub

    Private Function 获取视口大小() As Size
        Dim bw As Integer = CInt(Math.Round(边框宽度 * DpiScale()))
        Dim w As Integer = Me.Width - bw * 2
        Dim h As Integer = Me.Height - bw * 2
        Return New Size(Math.Max(0, w), Math.Max(0, h))
    End Function

    Private Function 获取有效视口大小() As Size
        Dim bw As Integer = CInt(Math.Round(边框宽度 * DpiScale()))
        Dim sbReserve As Integer = CInt(Math.Round(滚动条宽度 * DpiScale())) + ScrollBarRenderer.Margin
        Dim w As Integer = Me.Width - bw * 2 - If(_showVScroll, sbReserve, 0)
        Dim h As Integer = Me.Height - bw * 2 - If(_showHScroll, sbReserve, 0)
        Return New Size(Math.Max(0, w), Math.Max(0, h))
    End Function

    ''' <summary>获取缩放后的图片总像素尺寸。</summary>
    Private Function 获取缩放图片尺寸() As Size
        If _image Is Nothing Then Return Size.Empty
        Return New Size(CInt(Math.Ceiling(_image.Width * _zoomFactor)),
                        CInt(Math.Ceiling(_image.Height * _zoomFactor)))
    End Function

#End Region

#Region "滚动区域计算"

    Private Sub 更新滚动区域()
        If _image Is Nothing Then
            _showVScroll = False
            _showHScroll = False
            _scrollX = 0
            _scrollY = 0
            _vScrollBar.ThumbRect = Rectangle.Empty
            _vScrollBar.TrackRect = Rectangle.Empty
            _hScrollBar.ThumbRect = Rectangle.Empty
            _hScrollBar.TrackRect = Rectangle.Empty
            Return
        End If

        更新缩放范围()

        Dim scaledSize As Size = 获取缩放图片尺寸()
        Dim bw As Integer = CInt(Math.Round(边框宽度 * DpiScale()))
        Dim sbReserve As Integer = CInt(Math.Round(滚动条宽度 * DpiScale())) + ScrollBarRenderer.Margin

        Dim fullW As Integer = Me.Width - bw * 2
        Dim fullH As Integer = Me.Height - bw * 2

        Dim needV As Boolean = scaledSize.Height > fullH
        Dim needH As Boolean = scaledSize.Width > fullW

        If needV AndAlso Not needH Then
            If scaledSize.Width > (fullW - sbReserve) Then needH = True
        End If
        If needH AndAlso Not needV Then
            If scaledSize.Height > (fullH - sbReserve) Then needV = True
        End If

        _showVScroll = needV
        _showHScroll = needH

        Dim viewW As Integer = Math.Max(0, fullW - If(needV, sbReserve, 0))
        Dim viewH As Integer = Math.Max(0, fullH - If(needH, sbReserve, 0))

        Dim maxScrollX As Integer = Math.Max(0, scaledSize.Width - viewW)
        Dim maxScrollY As Integer = Math.Max(0, scaledSize.Height - viewH)
        _scrollX = Math.Max(0, Math.Min(_scrollX, maxScrollX))
        _scrollY = Math.Max(0, Math.Min(_scrollY, maxScrollY))

        Dim scaledBW As Integer = CInt(Math.Round(边框宽度 * DpiScale()))
        Dim scaledSBW As Integer = CInt(Math.Round(滚动条宽度 * DpiScale()))

        If needV Then
            _vScrollBar.ComputeLayout(Me.Width, Me.Height, scaledBW, 0,
                0, If(needH, sbReserve, 0), scaledSBW,
                scaledSize.Height, viewH, _scrollY)
        Else
            _vScrollBar.ThumbRect = Rectangle.Empty
            _vScrollBar.TrackRect = Rectangle.Empty
            _vScrollBar.VisualLeft = Me.Width
        End If

        If needH Then
            Dim vsbReserved As Integer = If(needV, Me.Width - scaledBW - _vScrollBar.VisualLeft, 0)
            _hScrollBar.ComputeHorizontalLayout(Me.Width, Me.Height, scaledBW, 0,
                0, vsbReserved, scaledSBW,
                scaledSize.Width, viewW, _scrollX)
        Else
            _hScrollBar.ThumbRect = Rectangle.Empty
            _hScrollBar.TrackRect = Rectangle.Empty
            _hScrollBar.VisualTop = Me.Height
        End If
    End Sub

#End Region

#Region "坐标转换"

    ''' <summary>计算视口原点偏移（含居中补偿）。</summary>
    Private Sub 计算视口偏移(ByRef originX As Single, ByRef originY As Single)
        Dim bw As Integer = CInt(Math.Round(边框宽度 * DpiScale()))
        Dim viewport As Size = 获取有效视口大小()
        Dim exactW As Single = If(_image IsNot Nothing, _image.Width * _zoomFactor, 0)
        Dim exactH As Single = If(_image IsNot Nothing, _image.Height * _zoomFactor, 0)

        Dim offsetX As Single = If(exactW < viewport.Width,
            (viewport.Width - exactW) / 2.0F, 0)
        Dim offsetY As Single = If(exactH < viewport.Height,
            (viewport.Height - exactH) / 2.0F, 0)

        originX = bw + offsetX - _scrollX
        originY = bw + offsetY - _scrollY
    End Sub

    ''' <summary>将控件客户坐标转换为图片像素坐标。</summary>
    Private Function 客户坐标转图片像素(clientPt As Point) As PointF
        Dim originX As Single = 0, originY As Single = 0
        计算视口偏移(originX, originY)
        Return New PointF((clientPt.X - originX) / _zoomFactor,
                          (clientPt.Y - originY) / _zoomFactor)
    End Function

    ''' <summary>将图片像素坐标转换为控件客户坐标。</summary>
    Private Function 图片像素转客户坐标(imgPt As PointF) As PointF
        Dim originX As Single = 0, originY As Single = 0
        计算视口偏移(originX, originY)
        Return New PointF(imgPt.X * _zoomFactor + originX,
                          imgPt.Y * _zoomFactor + originY)
    End Function

    ''' <summary>将图片像素矩形转换为控件客户矩形。</summary>
    Private Function 图片矩形转客户矩形(imgRect As Rectangle) As RectangleF
        Dim originX As Single = 0, originY As Single = 0
        计算视口偏移(originX, originY)
        Dim x As Single = imgRect.X * _zoomFactor + originX
        Dim y As Single = imgRect.Y * _zoomFactor + originY
        Return New RectangleF(x, y,
                              imgRect.Width * _zoomFactor,
                              imgRect.Height * _zoomFactor)
    End Function

#End Region

#Region "框选手柄"

    Private Function 获取手柄矩形(handlePos As HandlePosition) As RectangleF
        If Not HasSelection Then Return RectangleF.Empty
        Dim selScreen As RectangleF = 图片矩形转客户矩形(_selectionRect)
        Dim hs As Single = 手柄尺寸
        Dim halfH As Single = hs / 2.0F

        Dim cx As Single = selScreen.X + selScreen.Width / 2
        Dim cy As Single = selScreen.Y + selScreen.Height / 2

        Select Case handlePos
            Case HandlePosition.TopLeft
                Return New RectangleF(selScreen.Left - halfH, selScreen.Top - halfH, hs, hs)
            Case HandlePosition.TopCenter
                Return New RectangleF(cx - halfH, selScreen.Top - halfH, hs, hs)
            Case HandlePosition.TopRight
                Return New RectangleF(selScreen.Right - halfH, selScreen.Top - halfH, hs, hs)
            Case HandlePosition.MiddleLeft
                Return New RectangleF(selScreen.Left - halfH, cy - halfH, hs, hs)
            Case HandlePosition.MiddleRight
                Return New RectangleF(selScreen.Right - halfH, cy - halfH, hs, hs)
            Case HandlePosition.BottomLeft
                Return New RectangleF(selScreen.Left - halfH, selScreen.Bottom - halfH, hs, hs)
            Case HandlePosition.BottomCenter
                Return New RectangleF(cx - halfH, selScreen.Bottom - halfH, hs, hs)
            Case HandlePosition.BottomRight
                Return New RectangleF(selScreen.Right - halfH, selScreen.Bottom - halfH, hs, hs)
            Case Else
                Return RectangleF.Empty
        End Select
    End Function

    Private Function 命中测试手柄(clientPt As Point) As HandlePosition
        If Not _showSelection OrElse Not HasSelection Then
            Return HandlePosition.None
        End If
        For Each hp As HandlePosition In AllHandlePositions
            Dim r As RectangleF = 获取手柄矩形(hp)
            r.Inflate(手柄触发范围, 手柄触发范围)
            If r.Contains(clientPt) Then Return hp
        Next
        Return HandlePosition.None
    End Function

    ''' <summary>检测鼠标是否在框选边线上（容差范围内）。</summary>
    Private Function 命中测试框选边线(clientPt As Point) As Boolean
        If Not _showSelection OrElse Not HasSelection Then
            Return False
        End If
        Dim selScreen As RectangleF = 图片矩形转客户矩形(_selectionRect)
        Dim tolerance As Single = Math.Max(框选边框宽度, 框线触发范围)
        Dim outer As RectangleF = RectangleF.Inflate(selScreen, tolerance, tolerance)
        Dim inner As RectangleF = RectangleF.Inflate(selScreen, -tolerance, -tolerance)
        Return outer.Contains(clientPt) AndAlso Not inner.Contains(clientPt)
    End Function

    Private Function 获取手柄光标(hp As HandlePosition) As Cursor
        Select Case hp
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

#End Region

#Region "绘制"

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Me.Width < 1 OrElse Me.Height < 1 Then Return

        更新滚动区域()

        Dim g As Graphics = e.Graphics
        绘制背景与边框(g)
        绘制图片(g)
        绘制框选(g)
        绘制垂直滚动条(g)
        绘制水平滚动条(g)
    End Sub

    Private Sub 绘制背景与边框(g As Graphics)
        Dim s As Single = DpiScale()
        Dim boundsRect As New RectangleF(0, 0, Me.Width, Me.Height)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * s / 2.0F
            boundsRect.Inflate(-half, -half)
        End If
        Using br As New SolidBrush(背景颜色)
            g.FillRectangle(br, boundsRect)
        End Using
        RectangleRenderer.绘制矩形边框(g, boundsRect, 边框颜色, 边框宽度 * s)
    End Sub

    Private Sub 绘制图片(g As Graphics)
        If _image Is Nothing Then Return

        Dim bw As Integer = CInt(Math.Round(边框宽度 * DpiScale()))
        Dim viewport As Size = 获取有效视口大小()
        If viewport.Width < 1 OrElse viewport.Height < 1 Then Return

        Dim exactW As Single = _image.Width * _zoomFactor
        Dim exactH As Single = _image.Height * _zoomFactor

        Dim offsetX As Single = 0
        Dim offsetY As Single = 0
        If exactW < viewport.Width Then
            offsetX = (viewport.Width - exactW) / 2.0F
        End If
        If exactH < viewport.Height Then
            offsetY = (viewport.Height - exactH) / 2.0F
        End If

        Dim destX As Single = bw + offsetX - _scrollX
        Dim destY As Single = bw + offsetY - _scrollY
        Dim destRect As New RectangleF(destX, destY, exactW, exactH)

        Dim clipRect As New Rectangle(bw, bw, viewport.Width, viewport.Height)
        Using oldClip As Region = g.Clip.Clone()
            g.SetClip(clipRect, CombineMode.Intersect)

            g.InterpolationMode = InterpolationMode.NearestNeighbor
            g.PixelOffsetMode = PixelOffsetMode.Half
            g.SmoothingMode = SmoothingMode.None
            g.DrawImage(_image, destRect, New RectangleF(0, 0, _image.Width, _image.Height), GraphicsUnit.Pixel)

            g.Clip = oldClip
        End Using
    End Sub

    Private Sub 绘制框选(g As Graphics)
        If Not _showSelection Then Return
        If Not HasSelection Then Return

        Dim selScreen As RectangleF = 图片矩形转客户矩形(_selectionRect)
        If selScreen.Width < 1 AndAlso selScreen.Height < 1 Then Return

        ' 将矩形对齐到像素边缘，使线条居中在像素格边界上
        Dim snapX As Single = CSng(Math.Round(selScreen.X))
        Dim snapY As Single = CSng(Math.Round(selScreen.Y))
        Dim snapR As Single = CSng(Math.Round(selScreen.Right))
        Dim snapB As Single = CSng(Math.Round(selScreen.Bottom))
        Dim snapped As New RectangleF(snapX, snapY, snapR - snapX, snapB - snapY)

        Using br As New SolidBrush(框选填充颜色)
            g.FillRectangle(br, snapped)
        End Using

        ' 边框居中到像素边缘：向内偏移半个线宽
        Dim halfPen As Single = 框选边框宽度 / 2.0F
        Dim borderRect As New RectangleF(snapped.X + halfPen, snapped.Y + halfPen,
                                          snapped.Width - 框选边框宽度, snapped.Height - 框选边框宽度)
        Dim oldSmooth = g.SmoothingMode
        g.SmoothingMode = SmoothingMode.None
        Using pen As New Pen(框选边框颜色, 框选边框宽度)
            pen.Alignment = PenAlignment.Center
            g.DrawRectangle(pen, borderRect.X, borderRect.Y, borderRect.Width, borderRect.Height)
        End Using
        g.SmoothingMode = oldSmooth

        Using handleBrush As New SolidBrush(手柄颜色), handlePen As New Pen(手柄边框颜色, 1)
            For Each hp As HandlePosition In AllHandlePositions
                Dim hr As RectangleF = 获取手柄矩形(hp)
                If hr.IsEmpty Then Continue For
                Dim hSnap As New RectangleF(CSng(Math.Round(hr.X)), CSng(Math.Round(hr.Y)),
                                             CSng(Math.Round(hr.Width)), CSng(Math.Round(hr.Height)))
                g.FillRectangle(handleBrush, hSnap)
                g.DrawRectangle(handlePen, hSnap.X, hSnap.Y, hSnap.Width, hSnap.Height)
            Next
        End Using
    End Sub

    Private Sub 绘制垂直滚动条(g As Graphics)
        If _vScrollBar.TrackRect.IsEmpty Then Return
        Dim s As Single = DpiScale()
        _vScrollBar.Draw(g, Me.Width, Me.Height, CInt(Math.Round(边框宽度 * s)), 0,
            CInt(Math.Round(滚动条宽度 * s)), 滚动条轨道颜色, 滚动条滑块颜色, 滚动条悬停颜色)
    End Sub

    Private Sub 绘制水平滚动条(g As Graphics)
        If _hScrollBar.TrackRect.IsEmpty Then Return
        Dim s As Single = DpiScale()
        _hScrollBar.DrawHorizontal(g, Me.Width, Me.Height, CInt(Math.Round(边框宽度 * s)), 0,
            CInt(Math.Round(滚动条宽度 * s)), 滚动条轨道颜色, 滚动条滑块颜色, 滚动条悬停颜色)
    End Sub

#End Region

#Region "鼠标交互"

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        Me.Focus()

        If _image Is Nothing Then Return

        ' === 左键 ===
        If e.Button = MouseButtons.Left Then
            ' 滚动条拖拽优先
            If _vScrollBar.BeginDrag(e.Location, _scrollY) Then Return
            If _hScrollBar.BeginDragHorizontal(e.Location, _scrollX) Then Return

            ' 滚动条轨道点击
            Dim viewport As Size = 获取有效视口大小()
            Dim scaledSize As Size = 获取缩放图片尺寸()
            If Not _vScrollBar.TrackRect.IsEmpty Then
                Dim newOff = _vScrollBar.TrackClick(e.Location, _scrollY, scaledSize.Height, viewport.Height)
                If newOff <> _scrollY Then
                    _scrollY = newOff
                    更新滚动区域()
                    Me.Invalidate()
                    Return
                End If
            End If
            If Not _hScrollBar.TrackRect.IsEmpty Then
                Dim newHOff = _hScrollBar.TrackClickHorizontal(e.Location, _scrollX, scaledSize.Width, viewport.Width)
                If newHOff <> _scrollX Then
                    _scrollX = newHOff
                    更新滚动区域()
                    Me.Invalidate()
                    Return
                End If
            End If

            ' 左键仅在手柄或框线上才触发调整
            If _showSelection Then
                Dim hp As HandlePosition = 命中测试手柄(e.Location)
                If hp <> HandlePosition.None Then
                    _dragMode = DragMode.ResizeSelection
                    _activeHandle = hp
                    _dragStart = e.Location
                    _dragSelectionStart = _selectionRect
                    Return
                End If

                If 命中测试框选边线(e.Location) AndAlso Not _selectionForceCenter Then
                    _dragMode = DragMode.MoveSelection
                    _dragStart = e.Location
                    _dragSelectionStart = _selectionRect
                    Return
                End If
            End If

            ' 其他位置左键 = 平移图片
            _dragMode = DragMode.Pan
            _dragStart = e.Location
            _dragScrollStart = New Point(_scrollX, _scrollY)
            Me.Cursor = Cursors.Cross
            Return
        End If

        ' === 右键 = 绘制新框选 ===
        If e.Button = MouseButtons.Right AndAlso _showSelection Then
            _dragMode = DragMode.DrawSelection
            _dragStart = e.Location
            Dim imgPt As PointF = 客户坐标转图片像素(e.Location)
            Dim px As Integer = Math.Max(0, Math.Min(CInt(Math.Floor(imgPt.X)), _image.Width - 1))
            Dim py As Integer = Math.Max(0, Math.Min(CInt(Math.Floor(imgPt.Y)), _image.Height - 1))
            _selectionRect = New Rectangle(px, py, 0, 0)
            _dragSelectionStart = _selectionRect
            Me.Invalidate()
            Return
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)

        ' 滚动条拖拽中
        If _vScrollBar.IsDragging Then
            Dim viewport As Size = 获取有效视口大小()
            _scrollY = _vScrollBar.DragMove(e.Y, 获取缩放图片尺寸().Height, viewport.Height)
            更新滚动区域()
            Me.Invalidate()
            Return
        End If
        If _hScrollBar.IsDragging Then
            Dim viewport As Size = 获取有效视口大小()
            _scrollX = _hScrollBar.DragMoveHorizontal(e.X, 获取缩放图片尺寸().Width, viewport.Width)
            更新滚动区域()
            Me.Invalidate()
            Return
        End If

        Select Case _dragMode
            Case DragMode.Pan
                Dim dx As Integer = e.X - _dragStart.X
                Dim dy As Integer = e.Y - _dragStart.Y
                _scrollX = _dragScrollStart.X - dx
                _scrollY = _dragScrollStart.Y - dy
                更新滚动区域()
                Me.Invalidate()

            Case DragMode.MoveSelection
                If _image IsNot Nothing AndAlso Not _selectionForceCenter Then
                    Dim dx As Single = (e.X - _dragStart.X) / _zoomFactor
                    Dim dy As Single = (e.Y - _dragStart.Y) / _zoomFactor
                    Dim newX As Integer = CInt(Math.Round(_dragSelectionStart.X + dx))
                    Dim newY As Integer = CInt(Math.Round(_dragSelectionStart.Y + dy))
                    newX = Math.Max(0, Math.Min(newX, _image.Width - _dragSelectionStart.Width))
                    newY = Math.Max(0, Math.Min(newY, _image.Height - _dragSelectionStart.Height))
                    _selectionRect = New Rectangle(newX, newY, _dragSelectionStart.Width, _dragSelectionStart.Height)
                    Me.Invalidate()
                End If

            Case DragMode.ResizeSelection
                If _image IsNot Nothing Then
                    应用手柄拖拽(e.Location)
                End If

            Case DragMode.DrawSelection
                If _image IsNot Nothing Then
                    Dim startPt As New PointF(_dragSelectionStart.X, _dragSelectionStart.Y)
                    Dim curPt As PointF = 客户坐标转图片像素(e.Location)
                    Dim x1 As Integer = Math.Floor(startPt.X)
                    Dim y1 As Integer = Math.Floor(startPt.Y)
                    Dim x2 As Integer = Math.Floor(curPt.X)
                    Dim y2 As Integer = Math.Floor(curPt.Y)
                    Dim rx As Integer = Math.Min(x1, x2)
                    Dim ry As Integer = Math.Min(y1, y2)
                    Dim rw As Integer = Math.Abs(x2 - x1)
                    Dim rh As Integer = Math.Abs(y2 - y1)
                    ' 固定比例约束
                    If _selectionAspectRatio > 0 Then
                        rh = CInt(Math.Round(rw / _selectionAspectRatio))
                        If rh < 1 AndAlso rw >= 1 Then rh = 1
                        ' 调整起始 Y 使矩形朝正确方向展开
                        If y2 < y1 Then ry = y1 - rh
                    End If
                    _selectionRect = 应用强制居中(约束矩形到图片(New Rectangle(rx, ry, rw, rh)))
                    Me.Invalidate()
                End If

            Case DragMode.None
                ' 更新光标和悬停
                更新光标(e.Location)

                Dim needInvalidate As Boolean = False
                If _vScrollBar.UpdateHover(e.Location) Then needInvalidate = True
                If _hScrollBar.UpdateHover(e.Location) Then needInvalidate = True
                If needInvalidate Then Me.Invalidate()
        End Select
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        Dim wasDrag As DragMode = _dragMode
        _dragMode = DragMode.None
        _activeHandle = HandlePosition.None
        _vScrollBar.EndDrag()
        _hScrollBar.EndDrag()
        更新光标(e.Location)
        If wasDrag = DragMode.MoveSelection OrElse
           wasDrag = DragMode.ResizeSelection OrElse
           wasDrag = DragMode.DrawSelection Then
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End If
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        Dim needInvalidate As Boolean = False
        If _vScrollBar.ResetHover() Then needInvalidate = True
        If _hScrollBar.ResetHover() Then needInvalidate = True
        If needInvalidate Then Me.Invalidate()
        Me.Cursor = Cursors.Default
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        If _image Is Nothing Then Return

        Dim oldZoom As Single = _zoomFactor

        ' 以鼠标位置为中心计算图片坐标
        Dim imgPt As PointF = 客户坐标转图片像素(e.Location)

        ' 缩放步进
        Dim factor As Single = If(e.Delta > 0, 1.25F, 0.8F)
        _zoomFactor *= factor
        If _zoomFactor < _minZoom Then _zoomFactor = _minZoom
        If _zoomFactor > _maxZoom Then _zoomFactor = _maxZoom

        If _zoomFactor = oldZoom Then Return

        ' 保持鼠标下的图片点不变
        Dim bw As Integer = CInt(Math.Round(边框宽度 * DpiScale()))
        _scrollX = CInt(Math.Round(imgPt.X * _zoomFactor - (e.Location.X - bw)))
        _scrollY = CInt(Math.Round(imgPt.Y * _zoomFactor - (e.Location.Y - bw)))

        更新滚动区域()
        Me.Invalidate()
    End Sub

    Private Sub 更新光标(clientPt As Point)
        If _image Is Nothing Then
            Me.Cursor = Cursors.Default
            Return
        End If

        If _showSelection Then
            Dim hp As HandlePosition = 命中测试手柄(clientPt)
            If hp <> HandlePosition.None Then
                Me.Cursor = 获取手柄光标(hp)
                Return
            End If
            If 命中测试框选边线(clientPt) AndAlso Not _selectionForceCenter Then
                Me.Cursor = Cursors.SizeAll
                Return
            End If
        End If

        Me.Cursor = Cursors.Default
    End Sub

    Private Sub 应用手柄拖拽(clientPt As Point)
        Dim dx As Single = (clientPt.X - _dragStart.X) / _zoomFactor
        Dim dy As Single = (clientPt.Y - _dragStart.Y) / _zoomFactor

        Dim adjustLeft As Boolean = False
        Dim adjustTop As Boolean = False
        Dim adjustRight As Boolean = False
        Dim adjustBottom As Boolean = False

        Select Case _activeHandle
            Case HandlePosition.TopLeft : adjustLeft = True : adjustTop = True
            Case HandlePosition.TopCenter : adjustTop = True
            Case HandlePosition.TopRight : adjustRight = True : adjustTop = True
            Case HandlePosition.MiddleLeft : adjustLeft = True
            Case HandlePosition.MiddleRight : adjustRight = True
            Case HandlePosition.BottomLeft : adjustLeft = True : adjustBottom = True
            Case HandlePosition.BottomCenter : adjustBottom = True
            Case HandlePosition.BottomRight : adjustRight = True : adjustBottom = True
        End Select

        Dim r As Rectangle = _dragSelectionStart
        Dim newX As Integer = r.X
        Dim newY As Integer = r.Y
        Dim newW As Integer = r.Width
        Dim newH As Integer = r.Height

        If adjustLeft Then
            newX = CInt(Math.Round(r.X + dx))
            newW = r.Right - newX
            If newW < 1 Then newW = 1 : newX = r.Right - 1
        End If
        If adjustRight Then
            newW = CInt(Math.Round(r.Width + dx))
            If newW < 1 Then newW = 1
        End If
        If adjustTop Then
            newY = CInt(Math.Round(r.Y + dy))
            newH = r.Bottom - newY
            If newH < 1 Then newH = 1 : newY = r.Bottom - 1
        End If
        If adjustBottom Then
            newH = CInt(Math.Round(r.Height + dy))
            If newH < 1 Then newH = 1
        End If

        ' 固定比例约束
        If _selectionAspectRatio > 0 Then
            Dim isCorner As Boolean = (adjustLeft OrElse adjustRight) AndAlso (adjustTop OrElse adjustBottom)
            Dim isHorizontalEdge As Boolean = (adjustTop OrElse adjustBottom) AndAlso Not adjustLeft AndAlso Not adjustRight
            Dim isVerticalEdge As Boolean = (adjustLeft OrElse adjustRight) AndAlso Not adjustTop AndAlso Not adjustBottom

            If isCorner OrElse isVerticalEdge Then
                ' 以宽度为主，计算对应高度
                newH = CInt(Math.Round(newW / _selectionAspectRatio))
                If newH < 1 Then newH = 1
                ' 固定对边
                If adjustTop Then newY = r.Bottom - newH
                If Not adjustTop AndAlso Not adjustBottom Then
                    Dim centerY As Integer = r.Y + r.Height \ 2
                    newY = centerY - newH \ 2
                End If
            ElseIf isHorizontalEdge Then
                ' 以高度为主，计算对应宽度
                newW = CInt(Math.Round(newH * _selectionAspectRatio))
                If newW < 1 Then newW = 1
                ' 水平方向居中
                Dim centerX As Integer = r.X + r.Width \ 2
                newX = centerX - newW \ 2
            End If
        End If

        _selectionRect = 应用强制居中(约束矩形到图片(New Rectangle(newX, newY, newW, newH)))
        Me.Invalidate()
    End Sub

    Private Function 约束矩形到图片(r As Rectangle) As Rectangle
        If _image Is Nothing Then Return r
        Dim x As Integer = Math.Max(0, r.X)
        Dim y As Integer = Math.Max(0, r.Y)
        Dim w As Integer = r.Width
        Dim h As Integer = r.Height
        If x + w > _image.Width Then w = _image.Width - x
        If y + h > _image.Height Then h = _image.Height - y
        If w < 1 Then w = 1
        If h < 1 Then h = 1
        Return New Rectangle(x, y, w, h)
    End Function

#End Region

#Region "生命周期"

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If e.KeyCode = Keys.Delete Then
            ClearSelection()
            e.Handled = True
        End If
    End Sub

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        更新缩放范围()
        更新滚动区域()
        Me.Invalidate()
    End Sub

#End Region

End Class
