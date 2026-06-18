Imports System.ComponentModel
Imports Vortice.Direct2D1

<DefaultEvent("CheckedChanged")>
Public Class BooleanSwitch

    Public Event CheckedChanged As EventHandler

    Public Sub New()
        InitializeComponent()
        动画助手.DirtyProvider = AddressOf 开关动画脏区
    End Sub

#Region "绘制"
    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' V2: BackgroundSource 已指定时跳过基类填底，由 OnPaint 显式画穿透层。
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Me.Width <= 0 OrElse Me.Height <= 0 Then Return

        Dim 极限矩形区域 As New RectangleF(0, 0, Me.Width - 1, Me.Height - 1)
        Dim s As Single = DpiScale()
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * s / 2.0F
            极限矩形区域.Inflate(-half, -half)
        End If

        Dim ssaa As Integer = D2DHelperV2.GetEffectiveSsaaScale(超采样倍率)

        Using scope = D2DHelperV2.BeginPaint(e, Me, ssaa)
            If scope Is Nothing Then Return  ' 设计期 / 无 Form

            If _backgroundSource IsNot Nothing Then
                BackgroundPenetrationV2.PaintBackground(Me, scope, _backgroundSource)
            End If

            绘制图形内容_D2D(scope.GraphicsLayer, scope.Compositor.BrushCache, 极限矩形区域)
            scope.FlushGraphics()

            If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
                Using geo = RectangleRenderer.创建圆角矩形几何(极限矩形区域, CSng(Math.Floor(极限矩形区域.Height / 2.0F)))
                    RectangleRenderer.绘制圆角背景_D2D(scope.DCRenderTarget, geo, 极限矩形区域, 禁用时遮罩颜色, Color.Empty, System.Windows.Forms.Orientation.Vertical, scope.Compositor.BrushCache)
                End Using
            End If
        End Using
    End Sub

    Private Sub 绘制图形内容_D2D(rt As ID2D1RenderTarget, brushCache As D2DGlobals.SolidColorBrushCache, 极限矩形区域 As RectangleF)
        Dim 轨道颜色 As Color = 获取当前轨道颜色()
        Dim 滑块颜色 As Color = 获取当前滑块颜色()
        Dim 当前边框颜色 As Color = 获取当前边框颜色()

        Dim 圆角半径 As Single = CSng(Math.Floor(极限矩形区域.Height / 2.0F))
        Using geo = RectangleRenderer.创建圆角矩形几何(极限矩形区域, 圆角半径)
            Dim brush = brushCache.Get(rt, 轨道颜色)
            If brush IsNot Nothing Then rt.FillGeometry(geo, brush)
            Dim s As Single = DpiScale()
            RectangleRenderer.绘制圆角边框_D2D(rt, geo, 当前边框颜色, 边框宽度 * s, brushCache)
        End Using

        Dim _滑块边距 As Single = 滑块边距值 * DpiScale()
        Dim 滑块直径 As Single = 极限矩形区域.Height - _滑块边距 * 2
        If 滑块直径 <= 0 Then Return
        Dim 滑块最小X As Single = 极限矩形区域.X + _滑块边距
        Dim 滑块最大X As Single = 极限矩形区域.Right - _滑块边距 - 滑块直径
        Dim 滑块X As Single = 滑块最小X + (滑块最大X - 滑块最小X) * 动画助手.Progress
        Dim 滑块Y As Single = 极限矩形区域.Y + _滑块边距
        Dim 滑块区域 As New RectangleF(滑块X, 滑块Y, 滑块直径, 滑块直径)
        RectangleRenderer.填充椭圆_D2D(rt, 滑块区域, 滑块颜色, brushCache)
    End Sub

    Private Function 获取当前轨道颜色() As Color
        Return 颜色插值(获取状态轨道颜色(False), 获取状态轨道颜色(True), 动画助手.Progress)
    End Function

    Private Function 获取状态轨道颜色(isOn As Boolean) As Color
        If isOn Then
            Select Case 鼠标状态
                Case MouseStateEnum.Hover
                    If 鼠标移上时开启轨道颜色 <> Color.Empty Then Return 鼠标移上时开启轨道颜色
                Case MouseStateEnum.Pressed
                    If 鼠标按下时开启轨道颜色 <> Color.Empty Then Return 鼠标按下时开启轨道颜色
            End Select
            Return 开启时轨道颜色
        Else
            Select Case 鼠标状态
                Case MouseStateEnum.Hover
                    If 鼠标移上时关闭轨道颜色 <> Color.Empty Then Return 鼠标移上时关闭轨道颜色
                Case MouseStateEnum.Pressed
                    If 鼠标按下时关闭轨道颜色 <> Color.Empty Then Return 鼠标按下时关闭轨道颜色
            End Select
            Return 关闭时轨道颜色
        End If
    End Function

    Private Function 获取当前滑块颜色() As Color
        Select Case 鼠标状态
            Case MouseStateEnum.Hover
                If 鼠标移上时滑块颜色 <> Color.Empty Then Return 鼠标移上时滑块颜色
            Case MouseStateEnum.Pressed
                If 鼠标按下时滑块颜色 <> Color.Empty Then Return 鼠标按下时滑块颜色
        End Select
        Return 滑块基础颜色
    End Function

    Private Function 获取当前边框颜色() As Color
        Select Case 鼠标状态
            Case MouseStateEnum.Hover
                If 鼠标移上时边框颜色值 <> Color.Empty Then Return 鼠标移上时边框颜色值
            Case MouseStateEnum.Pressed
                If 鼠标按下时边框颜色值 <> Color.Empty Then Return 鼠标按下时边框颜色值
        End Select
        Return 边框颜色
    End Function

    Private Shared Function 颜色插值(c1 As Color, c2 As Color, t As Single) As Color
        Return Color.FromArgb(
            字节插值(c1.A, c2.A, t),
            字节插值(c1.R, c2.R, t),
            字节插值(c1.G, c2.G, t),
            字节插值(c1.B, c2.B, t))
    End Function

    Private Shared Function 字节插值(a As Integer, b As Integer, t As Single) As Integer
        Return Math.Clamp(CInt(a + (b - a) * t), 0, 255)
    End Function
#End Region

#Region "鼠标状态"
    Private Enum MouseStateEnum
        Normal
        Hover
        Pressed
    End Enum
    Private 鼠标状态 As MouseStateEnum = MouseStateEnum.Normal
    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        If Not Enabled Then Return
        鼠标状态 = MouseStateEnum.Hover
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If Not Enabled Then Return
        鼠标状态 = MouseStateEnum.Normal
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not Enabled Then Return
        鼠标状态 = MouseStateEnum.Pressed
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If Not Enabled Then Return
        鼠标状态 = If(ClientRectangle.Contains(e.Location), MouseStateEnum.Hover, MouseStateEnum.Normal)
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Protected Overrides Sub OnClick(e As EventArgs)
        MyBase.OnClick(e)
        Checked = Not Checked
    End Sub
    Protected Overrides Sub OnDoubleClick(e As EventArgs)
        MyBase.OnDoubleClick(e)
        Checked = Not Checked
    End Sub
    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        If Not Enabled Then
            鼠标状态 = MouseStateEnum.Normal
            动画助手.StopAnimation()
        End If
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        D2DHelperV2.RefreshFontDependentRendering(Me)
    End Sub
#End Region

#Region "通用"
    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            OuterToInnerRefreshScheduler.RequestFull(Me)
        End If
    End Sub

    Private ReadOnly 动画助手 As New AnimationHelperV2(Me)

    Private Sub 开关动画脏区(helper As AnimationHelperV2, owner As Control, sink As AnimationHelperV2.InvalidateRegionSink)
        sink.Add(计算动画脏区())
    End Sub

    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function

    Private Function 计算动画脏区() As Rectangle
        If Me.Width <= 0 OrElse Me.Height <= 0 Then Return Rectangle.Empty
        Dim inflate As Integer = Math.Max(2, CInt(Math.Ceiling((边框宽度 + 2) * DpiScale())))
        Dim rect As New Rectangle(0, 0, Me.Width, Me.Height)
        rect.Inflate(inflate, inflate)
        Return Rectangle.Intersect(Me.ClientRectangle, rect)
    End Function
#End Region

#Region "属性"
    Private 已选中 As Boolean = False
    <Category("LakeUI"), Description("开关状态"), DefaultValue(False), Browsable(True)>
    Public Property Checked As Boolean
        Get
            Return 已选中
        End Get
        Set(value As Boolean)
            If 已选中 <> value Then
                已选中 = value
                动画助手.AnimateTo(If(value, 1.0F, 0.0F))
                RaiseEvent CheckedChanged(Me, EventArgs.Empty)
            End If
        End Set
    End Property

    Private 超采样倍率 As Integer = 1
    <Category("LakeUI"), Description(GlobalOptions.超采样抗锯齿描述词), DefaultValue(GetType(GlobalOptions.SuperSamplingScaleEnum), "OFF"), Browsable(True)>
    Public Property SuperSamplingScale As GlobalOptions.SuperSamplingScaleEnum
        Get
            Return 超采样倍率
        End Get
        Set(value As GlobalOptions.SuperSamplingScaleEnum)
            SetValue(超采样倍率, value)
        End Set
    End Property

    Private 开启时轨道颜色 As Color = Color.FromArgb(0, 120, 215)
    <Category("LakeUI"), Description("开启时轨道颜色"), DefaultValue(GetType(Color), "0, 120, 215"), Browsable(True)>
    Public Property TrackColorOn As Color
        Get
            Return 开启时轨道颜色
        End Get
        Set(value As Color)
            SetValue(开启时轨道颜色, value)
        End Set
    End Property

    Private 关闭时轨道颜色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("关闭时轨道颜色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
    Public Property TrackColorOff As Color
        Get
            Return 关闭时轨道颜色
        End Get
        Set(value As Color)
            SetValue(关闭时轨道颜色, value)
        End Set
    End Property

    Private 滑块基础颜色 As Color = Color.FromArgb(220, 220, 220)
    <Category("LakeUI"), Description("滑块颜色"), DefaultValue(GetType(Color), "220, 220, 220"), Browsable(True)>
    Public Property KnobColor As Color
        Get
            Return 滑块基础颜色
        End Get
        Set(value As Color)
            SetValue(滑块基础颜色, value)
        End Set
    End Property

    Private 滑块边距值 As Integer = 3
    <Category("LakeUI"), Description("滑块与轨道的边距"), DefaultValue(3), Browsable(True)>
    Public Property KnobPadding As Integer
        Get
            Return 滑块边距值
        End Get
        Set(value As Integer)
            SetValue(滑块边距值, value)
        End Set
    End Property

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

    Private 边框宽度 As Integer = 0
    <Category("LakeUI"), Description("边框宽度"), DefaultValue(0), Browsable(True)>
    Public Property BorderSize As Integer
        Get
            Return 边框宽度
        End Get
        Set(value As Integer)
            SetValue(边框宽度, value)
        End Set
    End Property

    Private _backgroundSource As Control = Nothing
    <Category("LakeUI"),
     Description("背景采样源（V2 透明背景穿透）。设置后将跨越任意层级直接采样此控件的绘制内容作为透明背景。"),
     DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource IsNot value Then
                解除背景穿透消费者()
                _backgroundSource = BackgroundPenetrationV2.SetConsumerSource(Me, _backgroundSource, value)
                OuterToInnerRefreshScheduler.RequestFull(Me)
            End If
        End Set
    End Property

    Private Sub 解除背景穿透消费者()
        If _backgroundSource Is Nothing Then Return
        Try : BackgroundPenetrationV2.UnregisterConsumer(Me, _backgroundSource) : Catch : End Try
    End Sub

    Private 禁用时遮罩颜色 As Color = Color.FromArgb(120, 0, 0, 0)
    <Category("LakeUI"), Description("禁用（Enabled = False）时覆盖在主体区域上的遮罩颜色（受圆角裁剪，不影响圆角外的透明区域）。"), DefaultValue(GetType(Color), "120, 0, 0, 0"), Browsable(True)>
    Public Property DisabledOverlayColor As Color
        Get
            Return 禁用时遮罩颜色
        End Get
        Set(value As Color)
            SetValue(禁用时遮罩颜色, value)
        End Set
    End Property

    <Category("LakeUI"), Description(GlobalOptions.动画时长描述词), DefaultValue(300), Browsable(True)>
    Public Property AnimationDuration As Integer
        Get
            Return 动画助手.Duration
        End Get
        Set(value As Integer)
            动画助手.Duration = Math.Max(0, value)
        End Set
    End Property

    <Category("LakeUI"), Description(GlobalOptions.动画帧率描述词), DefaultValue(60), Browsable(True)>
    Public Property AnimationFPS As Integer
        Get
            Return 动画助手.FPS
        End Get
        Set(value As Integer)
            动画助手.FPS = Math.Max(0, value)
        End Set
    End Property

    Private 鼠标移上时开启轨道颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时开启轨道颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverTrackColorOn As Color
        Get
            Return 鼠标移上时开启轨道颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时开启轨道颜色, value)
        End Set
    End Property

    Private 鼠标移上时关闭轨道颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时关闭轨道颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverTrackColorOff As Color
        Get
            Return 鼠标移上时关闭轨道颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时关闭轨道颜色, value)
        End Set
    End Property

    Private 鼠标移上时滑块颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时滑块颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverKnobColor As Color
        Get
            Return 鼠标移上时滑块颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时滑块颜色, value)
        End Set
    End Property

    Private 鼠标移上时边框颜色值 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时边框颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverBorderColor As Color
        Get
            Return 鼠标移上时边框颜色值
        End Get
        Set(value As Color)
            SetValue(鼠标移上时边框颜色值, value)
        End Set
    End Property

    Private 鼠标按下时开启轨道颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时开启轨道颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedTrackColorOn As Color
        Get
            Return 鼠标按下时开启轨道颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时开启轨道颜色, value)
        End Set
    End Property

    Private 鼠标按下时关闭轨道颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时关闭轨道颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedTrackColorOff As Color
        Get
            Return 鼠标按下时关闭轨道颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时关闭轨道颜色, value)
        End Set
    End Property

    Private 鼠标按下时滑块颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时滑块颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedKnobColor As Color
        Get
            Return 鼠标按下时滑块颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时滑块颜色, value)
        End Set
    End Property

    Private 鼠标按下时边框颜色值 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时边框颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedBorderColor As Color
        Get
            Return 鼠标按下时边框颜色值
        End Get
        Set(value As Color)
            SetValue(鼠标按下时边框颜色值, value)
        End Set
    End Property
#End Region

#Region "生命周期"

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        解除背景穿透消费者()
        MyBase.OnHandleDestroyed(e)
    End Sub

    Protected Overrides Sub OnVisibleChanged(e As EventArgs)
        MyBase.OnVisibleChanged(e)
        If Not Me.Visible Then 解除背景穿透消费者()
    End Sub

    Protected Overrides Sub OnParentChanged(e As EventArgs)
        MyBase.OnParentChanged(e)
        If Me.Parent Is Nothing Then 解除背景穿透消费者()
    End Sub

#End Region

#Region "禁用属性"
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property AutoScroll As Boolean
        Get
            Return Nothing
        End Get
        Set(value As Boolean)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property AutoScrollMargin As Size
        Get
            Return Nothing
        End Get
        Set(value As Size)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property AutoScrollMinSize As Size
        Get
            Return Nothing
        End Get
        Set(value As Size)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property AutoSize As Boolean
        Get
            Return Nothing
        End Get
        Set(value As Boolean)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property AutoSizeMode As AutoSizeMode
        Get
            Return Nothing
        End Get
        Set(value As AutoSizeMode)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property BorderStyle As BorderStyle
        Get
            Return Nothing
        End Get
        Set(value As BorderStyle)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property BackgroundImage As Image
        Get
            Return Nothing
        End Get
        Set(value As Image)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property BackgroundImageLayout As ImageLayout
        Get
            Return Nothing
        End Get
        Set(value As ImageLayout)
        End Set
    End Property
#End Region

End Class
