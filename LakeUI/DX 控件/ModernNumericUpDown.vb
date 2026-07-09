Imports System.ComponentModel
Imports System.Globalization
Imports System.Numerics
Imports Vortice.Direct2D1

<DefaultEvent("ValueChanged")>
Public Class ModernNumericUpDown
    Implements V3_IGpuRenderable, V3_IGpuInvalidationSource

    Public Event ValueChanged As EventHandler
    Public Shadows Event TextChanged As EventHandler
    Public Event UpButtonClick As EventHandler
    Public Event DownButtonClick As EventHandler

#Region "字段"
    Private ReadOnly _textRenderer As SingleLineTextBoxRenderer
    Private ReadOnly _repeatTimer As New Timer()
    Private _mouseDownSelecting As Boolean = False

    Private Enum MouseStateEnum
        Normal
        Hover
        Pressed
    End Enum

    Private Enum SpinButtonPart
        None
        Up
        Down
    End Enum

    Private 鼠标状态 As MouseStateEnum = MouseStateEnum.Normal
    Private _hoverButton As SpinButtonPart = SpinButtonPart.None
    Private _pressedButton As SpinButtonPart = SpinButtonPart.None
    Private _repeatStarted As Boolean = False

    Private Const WM_GETDLGCODE As Integer = &H87
    Private Const WM_CHAR As Integer = &H102
    Private Const DLGC_WANTCHARS As Integer = &H80
    Private Const DLGC_WANTALLKEYS As Integer = &H4
#End Region

#Region "初始化"
    Public Sub New()
        _textRenderer = New SingleLineTextBoxRenderer(Me) With {
            .TextFilter = AddressOf FilterInsertText,
            .CandidateValidator = AddressOf IsPotentialNumericText,
            .Text = "0"
        }
        AddHandler _textRenderer.TextChanged, AddressOf TextRenderer_TextChanged
        InitializeComponent()
        AddHandler _repeatTimer.Tick, AddressOf RepeatTimer_Tick
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.Selectable Or
                 ControlStyles.StandardClick Or
                 ControlStyles.SupportsTransparentBackColor, True)
        UpdateStyles()
        If Me.Size = Size.Empty Then Me.Size = New Size(120, 30)
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        _textRenderer.StartCaretBlink()
    End Sub

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        _textRenderer.StopCaretBlink()
        _repeatTimer.Stop()
        MyBase.OnHandleDestroyed(e)
    End Sub
#End Region

#Region "数值属性"
    Private 最小值 As Double = 0
    <Category("LakeUI"), Description("最小值"), DefaultValue(0.0), Browsable(True)>
    Public Property Minimum As Double
        Get
            Return 最小值
        End Get
        Set(value As Double)
            If value = 最小值 Then Return
            最小值 = value
            If 最大值 < 最小值 Then 最大值 = 最小值
            SetValueCore(当前值, True, True)
            请求V3渲染()
        End Set
    End Property

    Private 最大值 As Double = 100
    <Category("LakeUI"), Description("最大值"), DefaultValue(100.0), Browsable(True)>
    Public Property Maximum As Double
        Get
            Return 最大值
        End Get
        Set(value As Double)
            If value = 最大值 Then Return
            最大值 = value
            If 最小值 > 最大值 Then 最小值 = 最大值
            SetValueCore(当前值, True, True)
            请求V3渲染()
        End Set
    End Property

    Private 当前值 As Double = 0
    <Category("LakeUI"), Description("当前值"), DefaultValue(0.0), Browsable(True)>
    Public Property Value As Double
        Get
            Return 当前值
        End Get
        Set(value As Double)
            SetValueCore(value, True, False)
        End Set
    End Property

    Private 小步进值 As Double = 1
    <Category("LakeUI"), Description("上下按钮、方向键和鼠标滚轮的步进值"), DefaultValue(1.0), Browsable(True)>
    Public Property SmallChange As Double
        Get
            Return 小步进值
        End Get
        Set(value As Double)
            小步进值 = Math.Max(0.0000001, Math.Abs(value))
        End Set
    End Property

    <Category("LakeUI"), Description("上下按钮、方向键和鼠标滚轮的步进值"), DefaultValue(1.0), Browsable(True)>
    Public Property Increment As Double
        Get
            Return 小步进值
        End Get
        Set(value As Double)
            SmallChange = value
        End Set
    End Property

    Private 大步进值 As Double = 10
    <Category("LakeUI"), Description("Page Up/Down 的步进值"), DefaultValue(10.0), Browsable(True)>
    Public Property LargeChange As Double
        Get
            Return 大步进值
        End Get
        Set(value As Double)
            大步进值 = Math.Max(0.0000001, Math.Abs(value))
        End Set
    End Property

    Private 小数位数 As Integer = -1
    <Category("LakeUI"), Description("显示的小数位数，-1 为不限制并自动去除末尾 0"), DefaultValue(-1), Browsable(True)>
    Public Property DecimalPlaces As Integer
        Get
            Return 小数位数
        End Get
        Set(value As Integer)
            value = Math.Max(-1, value)
            If 小数位数 = value Then Return
            小数位数 = value
            SetValueCore(当前值, True, True)
            请求V3渲染()
        End Set
    End Property

    <Category("LakeUI"), Description("是否允许键盘编辑文本"), DefaultValue(True), Browsable(True)>
    Public Property Editable As Boolean
        Get
            Return _textRenderer.Editable
        End Get
        Set(value As Boolean)
            If _textRenderer.Editable = value Then Return
            _textRenderer.Editable = value
            Cursor = Cursors.Default
            请求V3渲染()
        End Set
    End Property

    <Category("LakeUI"), Description("当前显示文本"), DefaultValue(GetType(String), "0"), Browsable(True),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
    Public Overrides Property Text As String
        Get
            Return _textRenderer.Text
        End Get
        Set(value As String)
            SetTextFromExternal(If(value, ""))
        End Set
    End Property

    Private Function ShouldSerializeText() As Boolean
        Return False
    End Function

    Public Overrides Sub ResetText()
        SetValueCore(0, True, False)
    End Sub
#End Region

#Region "外观属性"
    Private 背景颜色 As Color = Color.FromArgb(36, 36, 36)
    <Category("LakeUI"), Description("背景基础颜色"), DefaultValue(GetType(Color), "36,36,36"), Browsable(True)>
    Public Property BackColor1 As Color
        Get
            Return 背景颜色
        End Get
        Set(value As Color)
            SetValue(背景颜色, value)
        End Set
    End Property

    Private 背景渐变颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("背景渐变颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property BackColor2 As Color
        Get
            Return 背景渐变颜色
        End Get
        Set(value As Color)
            SetValue(背景渐变颜色, value)
        End Set
    End Property

    Private 渐变方向 As System.Windows.Forms.Orientation = System.Windows.Forms.Orientation.Vertical
    <Category("LakeUI"), Description("渐变方向"), DefaultValue(GetType(System.Windows.Forms.Orientation), "Vertical"), Browsable(True)>
    Public Property BackColorOrientation As System.Windows.Forms.Orientation
        Get
            Return 渐变方向
        End Get
        Set(value As System.Windows.Forms.Orientation)
            SetValue(渐变方向, value)
        End Set
    End Property

    <Category("LakeUI"), Description("文本颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Overrides Property ForeColor As Color
        Get
            Return _textRenderer.ForeColor
        End Get
        Set(value As Color)
            If _textRenderer.ForeColor = value Then Return
            _textRenderer.ForeColor = value
            请求V3渲染()
        End Set
    End Property

    <Category("LakeUI"), Description("行高"), DefaultValue(GetType(Integer), "25"), Browsable(True)>
    Public Property LineHeight As Integer
        Get
            Return _textRenderer.LineHeight
        End Get
        Set(value As Integer)
            _textRenderer.LineHeight = Math.Max(10, value)
            请求V3渲染()
        End Set
    End Property

    <Category("LakeUI"), Description("光标线宽"), DefaultValue(GetType(Integer), "2"), Browsable(True)>
    Public Property CaretWidth As Integer
        Get
            Return _textRenderer.CaretWidth
        End Get
        Set(value As Integer)
            _textRenderer.CaretWidth = Math.Max(1, value)
            请求V3渲染()
        End Set
    End Property

    <Category("LakeUI"), Description("光标颜色"), DefaultValue(GetType(Color), "220,220,220"), Browsable(True)>
    Public Property CaretColor As Color
        Get
            Return _textRenderer.CaretColor
        End Get
        Set(value As Color)
            If _textRenderer.CaretColor = value Then Return
            _textRenderer.CaretColor = value
            请求V3渲染()
        End Set
    End Property

    <Category("LakeUI"), Description("选区背景色"), DefaultValue(GetType(Color), "80,80,80"), Browsable(True)>
    Public Property SelectionColor As Color
        Get
            Return _textRenderer.SelectionColor
        End Get
        Set(value As Color)
            If _textRenderer.SelectionColor = value Then Return
            _textRenderer.SelectionColor = value
            请求V3渲染()
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

    Private 有焦点时边框颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("有焦点时边框颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property BorderColorFocus As Color
        Get
            Return 有焦点时边框颜色
        End Get
        Set(value As Color)
            SetValue(有焦点时边框颜色, value)
        End Set
    End Property

    Private 边框宽度 As Integer = 1
    <Category("LakeUI"), Description("边框宽度"), DefaultValue(GetType(Integer), "1"), Browsable(True)>
    Public Property BorderSize As Integer
        Get
            Return 边框宽度
        End Get
        Set(value As Integer)
            边框宽度 = Math.Max(0, value)
            SyncTextRendererLayout()
            请求V3渲染()
        End Set
    End Property

    Private 边框圆角半径 As Integer = 0
    <Category("LakeUI"), Description("边框圆角半径"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property BorderRadius As Integer
        Get
            Return 边框圆角半径
        End Get
        Set(value As Integer)
            边框圆角半径 = Math.Max(0, value)
            请求V3渲染()
        End Set
    End Property

    Public Enum TextAlignMode
        Left = 0
        Center = 1
        Right = 2
    End Enum

    <Category("LakeUI"), Description("文本对齐方式"), DefaultValue(TextAlignMode.Left), Browsable(True)>
    Public Property TextAlign As TextAlignMode
        Get
            Return CType(_textRenderer.TextAlign, TextAlignMode)
        End Get
        Set(value As TextAlignMode)
            If CType(_textRenderer.TextAlign, TextAlignMode) = value Then Return
            _textRenderer.TextAlign = CType(value, SingleLineTextBoxRenderer.TextAlignMode)
            请求V3渲染()
        End Set
    End Property

    <Category("LakeUI"), Description("水印文本"), DefaultValue(GetType(String), ""), Browsable(True)>
    Public Property WaterText As String
        Get
            Return _textRenderer.WaterText
        End Get
        Set(value As String)
            _textRenderer.WaterText = If(value, "")
            请求V3渲染()
        End Set
    End Property

    <Category("LakeUI"), Description("水印颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property WaterTextForeColor As Color
        Get
            Return _textRenderer.WaterTextForeColor
        End Get
        Set(value As Color)
            If _textRenderer.WaterTextForeColor = value Then Return
            _textRenderer.WaterTextForeColor = value
            请求V3渲染()
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

    Private 禁用时遮罩颜色 As Color = Color.FromArgb(120, 0, 0, 0)
    <Category("LakeUI"), Description("禁用（Enabled = False）时覆盖在主体区域上的遮罩颜色。"), DefaultValue(GetType(Color), "120, 0, 0, 0"), Browsable(True)>
    Public Property DisabledOverlayColor As Color
        Get
            Return 禁用时遮罩颜色
        End Get
        Set(value As Color)
            SetValue(禁用时遮罩颜色, value)
        End Set
    End Property

    Private 鼠标移上时背景颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时背景颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverBackColor1 As Color
        Get
            Return 鼠标移上时背景颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时背景颜色, value)
        End Set
    End Property

    Private 鼠标移上时渐变颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时渐变颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverBackColor2 As Color
        Get
            Return 鼠标移上时渐变颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时渐变颜色, value)
        End Set
    End Property

    Private 鼠标移上时边框颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时边框颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverBorderColor As Color
        Get
            Return 鼠标移上时边框颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时边框颜色, value)
        End Set
    End Property

    Private 鼠标按下时背景颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时背景颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedBackColor1 As Color
        Get
            Return 鼠标按下时背景颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时背景颜色, value)
        End Set
    End Property

    Private 鼠标按下时渐变颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时渐变颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedBackColor2 As Color
        Get
            Return 鼠标按下时渐变颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时渐变颜色, value)
        End Set
    End Property

    Private 鼠标按下时边框颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时边框颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedBorderColor As Color
        Get
            Return 鼠标按下时边框颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时边框颜色, value)
        End Set
    End Property
#End Region

#Region "按钮与分隔线属性"
    Private 按钮区域宽度 As Integer = 30
    <Category("LakeUI"), Description("右侧上下按钮区域宽度（96DPI 逻辑像素，会按当前 DPI 缩放）"), DefaultValue(30), Browsable(True)>
    Public Property ButtonAreaWidth As Integer
        Get
            Return 按钮区域宽度
        End Get
        Set(value As Integer)
            按钮区域宽度 = Math.Max(1, value)
            SyncTextRendererLayout()
            请求V3渲染()
        End Set
    End Property

    Private 按钮背景颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("右侧按钮背景基础颜色，Empty 时使用控件背景"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property ButtonBackColor1 As Color
        Get
            Return 按钮背景颜色
        End Get
        Set(value As Color)
            SetValue(按钮背景颜色, value)
        End Set
    End Property

    Private 按钮背景渐变颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("右侧按钮背景渐变颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property ButtonBackColor2 As Color
        Get
            Return 按钮背景渐变颜色
        End Get
        Set(value As Color)
            SetValue(按钮背景渐变颜色, value)
        End Set
    End Property

    Private 鼠标移上时按钮背景颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上右侧按钮时的背景颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverButtonBackColor1 As Color
        Get
            Return 鼠标移上时按钮背景颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时按钮背景颜色, value)
        End Set
    End Property

    Private 鼠标移上时按钮渐变颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上右侧按钮时的背景渐变颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverButtonBackColor2 As Color
        Get
            Return 鼠标移上时按钮渐变颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时按钮渐变颜色, value)
        End Set
    End Property

    Private 鼠标按下时按钮背景颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下右侧按钮时的背景颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedButtonBackColor1 As Color
        Get
            Return 鼠标按下时按钮背景颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时按钮背景颜色, value)
        End Set
    End Property

    Private 鼠标按下时按钮渐变颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下右侧按钮时的背景渐变颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedButtonBackColor2 As Color
        Get
            Return 鼠标按下时按钮渐变颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时按钮渐变颜色, value)
        End Set
    End Property

    Private 箭头颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("上下箭头颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property ArrowColor As Color
        Get
            Return 箭头颜色
        End Get
        Set(value As Color)
            SetValue(箭头颜色, value)
        End Set
    End Property

    Private 鼠标移上时箭头颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时箭头颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverArrowColor As Color
        Get
            Return 鼠标移上时箭头颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时箭头颜色, value)
        End Set
    End Property

    Private 鼠标按下时箭头颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时箭头颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedArrowColor As Color
        Get
            Return 鼠标按下时箭头颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时箭头颜色, value)
        End Set
    End Property

    Private 箭头大小 As Integer = 10
    <Category("LakeUI"), Description("上下箭头大小"), DefaultValue(GetType(Integer), "10"), Browsable(True)>
    Public Property ArrowSize As Integer
        Get
            Return 箭头大小
        End Get
        Set(value As Integer)
            箭头大小 = Math.Max(4, value)
            请求V3渲染()
        End Set
    End Property

    Private 分隔线颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("文本框与按钮、上下按钮之间共用的分隔线颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property DividerColor As Color
        Get
            Return 分隔线颜色
        End Get
        Set(value As Color)
            SetValue(分隔线颜色, value)
        End Set
    End Property

    Private 分隔线宽度 As Integer = 1
    <Category("LakeUI"), Description("文本框与按钮、上下按钮之间共用的分隔线宽度，0 为不显示"), DefaultValue(GetType(Integer), "1"), Browsable(True)>
    Public Property DividerSize As Integer
        Get
            Return 分隔线宽度
        End Get
        Set(value As Integer)
            分隔线宽度 = Math.Max(0, value)
            请求V3渲染()
        End Set
    End Property
#End Region

#Region "V3 背景穿透"
    Private _backgroundSource As Control = Nothing
    <Category("LakeUI"),
     Description("背景采样源（超容器背景映射）。设置后将跨越任意层级直接采样此控件的绘制内容作为透明背景；为空时不进行背景采样。"),
     DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource IsNot value Then
                _backgroundSource = D3D_BackgroundPenetration.SetBackgroundSource(Me, _backgroundSource, value)
                请求V3渲染()
            End If
        End Set
    End Property
#End Region

#Region "绘制"
    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        Dim w As Integer = ClientRectangle.Width
        Dim h As Integer = ClientRectangle.Height
        If w <= 0 OrElse h <= 0 Then Return

        Dim hasRadius As Boolean = 边框圆角半径 > 0
        Dim sourceRect As New RectangleF(0, 0, w, h)
        Dim boundsRect As New RectangleF(0, 0, w, h)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * DpiScale() / 2.0F
            boundsRect.Inflate(-half, -half)
        End If

        Dim bc As Color = If(Focused, 有焦点时边框颜色, 边框颜色)
        Dim effBg As Color = 背景颜色
        Dim effBg2 As Color = 背景渐变颜色
        Select Case 鼠标状态
            Case MouseStateEnum.Hover
                If 鼠标移上时背景颜色 <> Color.Empty Then effBg = 鼠标移上时背景颜色
                If 鼠标移上时渐变颜色 <> Color.Empty Then effBg2 = 鼠标移上时渐变颜色
                If 鼠标移上时边框颜色 <> Color.Empty Then bc = 鼠标移上时边框颜色
            Case MouseStateEnum.Pressed
                If 鼠标按下时背景颜色 <> Color.Empty Then effBg = 鼠标按下时背景颜色
                If 鼠标按下时渐变颜色 <> Color.Empty Then effBg2 = 鼠标按下时渐变颜色
                If 鼠标按下时边框颜色 <> Color.Empty Then bc = 鼠标按下时边框颜色
        End Select

        绘制背景_GPU(context, hasRadius, sourceRect, boundsRect, effBg, effBg2)
        SyncTextRendererLayout()
        _textRenderer.DrawGpu(context)
        绘制按钮_GPU(context, w, h)
        绘制边框_GPU(context, hasRadius, boundsRect, bc)

        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            填充圆角矩形_GPU(context, boundsRect, If(hasRadius, 边框圆角半径 * DpiScale(), 0.0F), 禁用时遮罩颜色)
        End If
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Private Sub 绘制背景_GPU(context As D3D_PaintContext, hasRadius As Boolean, sourceRect As RectangleF, boundsRect As RectangleF, bgClr As Color, bgClr2 As Color)
        Dim backColorMask As Color = MyBase.BackColor
        Dim hasMask As Boolean = backColorMask.A > 0 AndAlso backColorMask.A < 255
        Dim fillColor As Color = If(bgClr.A > 0, bgClr, bgClr2)
        Dim hasFill As Boolean = fillColor.A > 0
        Dim hasBackgroundSource As Boolean = _backgroundSource IsNot Nothing
        If Not hasBackgroundSource AndAlso Not hasMask AndAlso Not hasFill Then Return
        Dim s As Single = DpiScale()
        Dim fillRect As RectangleF = boundsRect
        Dim radius As Single = If(hasRadius, 边框圆角半径 * s, 0.0F)
        If hasBackgroundSource Then context.DrawBackgroundSource(Me, _backgroundSource, sourceRect)
        If hasMask Then 填充圆角矩形_GPU(context, fillRect, radius, backColorMask)
        If hasFill Then 填充圆角矩形_GPU(context, fillRect, radius, fillColor)
    End Sub

    Private Sub 绘制边框_GPU(context As D3D_PaintContext, hasRadius As Boolean, boundsRect As RectangleF, borderClr As Color)
        If 边框宽度 <= 0 OrElse borderClr.A = 0 Then Return
        Dim s As Single = DpiScale()
        绘制圆角边框_GPU(context, boundsRect, If(hasRadius, 边框圆角半径 * s, 0.0F), borderClr, 边框宽度 * s)
    End Sub

    Private Sub 绘制按钮_GPU(context As D3D_PaintContext, w As Integer, h As Integer)
        Dim s As Single = DpiScale()
        Dim aaw As Integer = ActualButtonAreaWidth
        If aaw <= 0 Then Return
        Dim bi As Integer = CInt(边框宽度 * s)
        Dim sepX As Single = w - aaw - If(bi > 0, bi / 2.0F, 0)
        Dim topInset As Integer = Math.Max(Padding.Top, bi)
        Dim bottomInset As Integer = Math.Max(Padding.Bottom, bi)
        Dim rightInset As Integer = bi
        Dim buttonLeft As Single = sepX + If(bi > 0, bi / 2.0F, 0)
        Dim buttonWidth As Single = w - buttonLeft - rightInset
        Dim buttonHeight As Single = h - topInset - bottomInset
        If buttonWidth <= 0 OrElse buttonHeight <= 0 Then Return

        Dim upRect As New RectangleF(buttonLeft, topInset, buttonWidth, buttonHeight / 2.0F)
        Dim downRect As New RectangleF(buttonLeft, topInset + buttonHeight / 2.0F, buttonWidth, buttonHeight / 2.0F)
        绘制按钮背景_GPU(context, upRect, SpinButtonPart.Up)
        绘制按钮背景_GPU(context, downRect, SpinButtonPart.Down)

        If 分隔线宽度 > 0 AndAlso 分隔线颜色.A > 0 Then
            Dim br = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, 分隔线颜色, context.DeviceGeneration)
            If br IsNot Nothing Then
                Dim lineWidth As Single = 分隔线宽度 * s
                context.DeviceContext.DrawLine(New Vector2(sepX, topInset), New Vector2(sepX, h - bottomInset), br, lineWidth)
                Dim midY As Single = topInset + buttonHeight / 2.0F
                context.DeviceContext.DrawLine(New Vector2(buttonLeft, midY), New Vector2(w - rightInset, midY), br, lineWidth)
            End If
        End If

        Dim upArrowColor As Color = 获取箭头颜色(SpinButtonPart.Up)
        Dim downArrowColor As Color = 获取箭头颜色(SpinButtonPart.Down)
        Dim centerX As Single = buttonLeft + buttonWidth / 2.0F
        If upArrowColor.A > 0 Then
            绘制圆角三角形_GPU(context, New PointF(centerX, upRect.Y + upRect.Height / 2.0F), True, upArrowColor)
        End If
        If downArrowColor.A > 0 Then
            绘制圆角三角形_GPU(context, New PointF(centerX, downRect.Y + downRect.Height / 2.0F), False, downArrowColor)
        End If
    End Sub

    Private Sub 绘制按钮背景_GPU(context As D3D_PaintContext, rect As RectangleF, part As SpinButtonPart)
        Dim c1 As Color = 按钮背景颜色
        Dim c2 As Color = 按钮背景渐变颜色
        If _pressedButton = part AndAlso 鼠标状态 = MouseStateEnum.Pressed Then
            If 鼠标按下时按钮背景颜色 <> Color.Empty Then c1 = 鼠标按下时按钮背景颜色
            If 鼠标按下时按钮渐变颜色 <> Color.Empty Then c2 = 鼠标按下时按钮渐变颜色
        ElseIf _hoverButton = part AndAlso 鼠标状态 = MouseStateEnum.Hover Then
            If 鼠标移上时按钮背景颜色 <> Color.Empty Then c1 = 鼠标移上时按钮背景颜色
            If 鼠标移上时按钮渐变颜色 <> Color.Empty Then c2 = 鼠标移上时按钮渐变颜色
        End If
        Dim fillColor As Color = If(c1.A > 0, c1, c2)
        If fillColor = Color.Empty OrElse fillColor.A = 0 Then Return
        context.FillRectangle(rect, fillColor)
    End Sub

    Private Sub 绘制圆角三角形_GPU(context As D3D_PaintContext, center As PointF, up As Boolean, color As Color)
        Dim path = 创建圆角三角形几何(center, up)
        If path Is Nothing Then Return
        Try
            Dim br = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
            If br IsNot Nothing Then context.DeviceContext.FillGeometry(path, br)
        Finally
            path.Dispose()
        End Try
    End Sub

    Private Function 创建圆角三角形几何(center As PointF, up As Boolean) As ID2D1PathGeometry
        Dim scaledArrow As Single = 箭头大小 * DpiScale()
        Dim arrW As Single = scaledArrow
        Dim arrH As Single = CSng(scaledArrow * Math.Sqrt(3.0) / 2.0)
        Dim verts() As PointF
        If up Then
            verts = {
                New PointF(center.X, center.Y - arrH / 2.0F),
                New PointF(center.X + arrW / 2.0F, center.Y + arrH / 2.0F),
                New PointF(center.X - arrW / 2.0F, center.Y + arrH / 2.0F)
            }
        Else
            verts = {
                New PointF(center.X - arrW / 2.0F, center.Y - arrH / 2.0F),
                New PointF(center.X + arrW / 2.0F, center.Y - arrH / 2.0F),
                New PointF(center.X, center.Y + arrH / 2.0F)
            }
        End If
        Dim cr As Single = Math.Max(scaledArrow * 0.2F, 1.0F)

        Dim path As ID2D1PathGeometry = D3D_RenderCore.DeviceManager.D2DFactory.CreatePathGeometry()
        Dim sink As ID2D1GeometrySink = path.Open()
        Try
            For i As Integer = 0 To 2
                Dim curr As PointF = verts(i)
                Dim prv As PointF = verts((i + 2) Mod 3)
                Dim nxt As PointF = verts((i + 1) Mod 3)
                Dim d1x As Single = prv.X - curr.X
                Dim d1y As Single = prv.Y - curr.Y
                Dim d2x As Single = nxt.X - curr.X
                Dim d2y As Single = nxt.Y - curr.Y
                Dim l1 As Single = CSng(Math.Sqrt(d1x * d1x + d1y * d1y))
                Dim l2 As Single = CSng(Math.Sqrt(d2x * d2x + d2y * d2y))
                Dim a As New Vector2(curr.X + cr * d1x / l1, curr.Y + cr * d1y / l1)
                Dim b As New Vector2(curr.X + cr * d2x / l2, curr.Y + cr * d2y / l2)
                Dim cp1 As New Vector2(a.X + 2.0F / 3.0F * (curr.X - a.X), a.Y + 2.0F / 3.0F * (curr.Y - a.Y))
                Dim cp2 As New Vector2(b.X + 2.0F / 3.0F * (curr.X - b.X), b.Y + 2.0F / 3.0F * (curr.Y - b.Y))
                If i = 0 Then
                    sink.BeginFigure(a, FigureBegin.Filled)
                Else
                    sink.AddLine(a)
                End If
                sink.AddBezier(New BezierSegment With {.Point1 = cp1, .Point2 = cp2, .Point3 = b})
            Next
            sink.EndFigure(FigureEnd.Closed)
            sink.Close()
        Finally
            sink.Dispose()
        End Try
        Return path
    End Function

    Private Sub 填充圆角矩形_GPU(context As D3D_PaintContext, rect As RectangleF, radius As Single, color As Color)
        If color.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If radius <= 0 Then
            context.DeviceContext.FillRectangle(D3D_PaintContext.ToRawRect(rect), brush)
            Return
        End If
        context.FillRoundedRectangle(rect, radius, brush)
    End Sub

    Private Sub 绘制圆角边框_GPU(context As D3D_PaintContext, rect As RectangleF, radius As Single, color As Color, strokeWidth As Single)
        If color.A = 0 OrElse strokeWidth <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If radius <= 0 Then
            context.DeviceContext.DrawRectangle(D3D_PaintContext.ToRawRect(rect), brush, strokeWidth)
            Return
        End If
        context.DrawRoundedRectangle(rect, radius, brush, strokeWidth)
    End Sub


    Private Function 获取箭头颜色(part As SpinButtonPart) As Color
        Dim result As Color = 箭头颜色
        If _pressedButton = part AndAlso 鼠标状态 = MouseStateEnum.Pressed Then
            If 鼠标按下时箭头颜色 <> Color.Empty Then result = 鼠标按下时箭头颜色
        ElseIf _hoverButton = part AndAlso 鼠标状态 = MouseStateEnum.Hover Then
            If 鼠标移上时箭头颜色 <> Color.Empty Then result = 鼠标移上时箭头颜色
        End If
        Return result
    End Function
#End Region

#Region "消息与键盘"
    Protected Overrides Sub WndProc(ByRef m As Message)
        Select Case m.Msg
            Case WM_GETDLGCODE
                m.Result = New IntPtr(DLGC_WANTCHARS Or DLGC_WANTALLKEYS)
                Return
            Case WM_CHAR
                HandleWmChar(m.WParam.ToInt32())
            Case Else
                MyBase.WndProc(m)
        End Select
    End Sub

    Private Sub HandleWmChar(charCode As Integer)
        Select Case charCode
            Case 1
                SelectAll()
            Case 3
                _textRenderer.CopySelection()
            Case 22
                If Editable Then _textRenderer.PasteText()
            Case 24
                If Editable Then _textRenderer.CutSelection()
            Case 8
                If Editable Then _textRenderer.HandleBackspace()
            Case 13
                CommitText()
            Case Else
                If Editable Then
                    Dim ch As Char = ChrW(charCode)
                    If Not Char.IsControl(ch) Then _textRenderer.InsertText(ch.ToString())
                End If
        End Select
        _textRenderer.ResetCaretBlink()
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If Not Enabled Then Return
        SyncTextRendererLayout()
        Dim shift As Boolean = e.Shift
        Dim ctrl As Boolean = e.Control
        Select Case e.KeyCode
            Case Keys.Up
                StepValue(小步进值)
                e.Handled = True
            Case Keys.Down
                StepValue(-小步进值)
                e.Handled = True
            Case Keys.PageUp
                StepValue(大步进值)
                e.Handled = True
            Case Keys.PageDown
                StepValue(-大步进值)
                e.Handled = True
            Case Keys.Left
                If ctrl Then _textRenderer.MoveCaretWordLeft(shift) Else _textRenderer.MoveCaret(-1, shift)
                e.Handled = True
            Case Keys.Right
                If ctrl Then _textRenderer.MoveCaretWordRight(shift) Else _textRenderer.MoveCaret(1, shift)
                e.Handled = True
            Case Keys.Home
                If ctrl Then
                    Value = 最小值
                Else
                    _textRenderer.MoveCaretHome(shift)
                End If
                e.Handled = True
            Case Keys.End
                If ctrl Then
                    Value = 最大值
                Else
                    _textRenderer.MoveCaretEnd(shift)
                End If
                e.Handled = True
            Case Keys.Delete
                If Editable Then _textRenderer.HandleDelete()
                e.Handled = True
            Case Keys.Enter
                CommitText()
                e.Handled = True
            Case Keys.Escape
                UpdateTextFromValue(True)
                e.Handled = True
        End Select
        If e.Handled Then _textRenderer.ResetCaretBlink()
    End Sub

    Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
        Select Case keyData And Keys.KeyCode
            Case Keys.Left, Keys.Right, Keys.Up, Keys.Down,
                 Keys.Home, Keys.End, Keys.Delete, Keys.Enter,
                 Keys.Escape, Keys.PageUp, Keys.PageDown
                Return True
        End Select
        Return MyBase.IsInputKey(keyData)
    End Function

    Protected Overrides Function IsInputChar(charCode As Char) As Boolean
        Return True
    End Function
#End Region

#Region "鼠标处理"
    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        鼠标状态 = MouseStateEnum.Hover
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        鼠标状态 = MouseStateEnum.Normal
        _hoverButton = SpinButtonPart.None
        If _pressedButton = SpinButtonPart.None Then Cursor = Cursors.Default
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not Enabled Then Return
        Focus()
        鼠标状态 = MouseStateEnum.Pressed
        If e.Button = MouseButtons.Left Then
            SyncTextRendererLayout()
            Dim part As SpinButtonPart = HitTestButton(e.Location)
            If part <> SpinButtonPart.None Then
                _pressedButton = part
                StepByButton(part)
                StartRepeatTimer()
                请求V3渲染()
                Return
            End If
            If Editable Then
                _mouseDownSelecting = True
                _textRenderer.BeginMouseSelection(e.X)
            End If
            请求V3渲染()
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        SyncTextRendererLayout()
        Dim prevHover As SpinButtonPart = _hoverButton
        _hoverButton = HitTestButton(e.Location)
        Cursor = If(_hoverButton = SpinButtonPart.None AndAlso Editable, Cursors.IBeam, Cursors.Default)
        If _mouseDownSelecting AndAlso e.Button = MouseButtons.Left AndAlso Editable Then
            _textRenderer.UpdateMouseSelection(e.X)
        ElseIf prevHover <> _hoverButton Then
            请求V3渲染()
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        _mouseDownSelecting = False
        _pressedButton = SpinButtonPart.None
        StopRepeatTimer()
        鼠标状态 = If(ClientRectangle.Contains(e.Location), MouseStateEnum.Hover, MouseStateEnum.Normal)
        _hoverButton = HitTestButton(e.Location)
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        If Not Enabled Then Return
        Focus()
        StepValue(If(e.Delta > 0, 小步进值, -小步进值))
    End Sub

    Private Function HitTestButton(point As Point) As SpinButtonPart
        Dim aaw As Integer = ActualButtonAreaWidth
        If aaw <= 0 Then Return SpinButtonPart.None
        Dim buttonRect As New Rectangle(ClientRectangle.Width - aaw, 0, aaw, ClientRectangle.Height)
        If Not buttonRect.Contains(point) Then Return SpinButtonPart.None
        Return If(point.Y < buttonRect.Top + buttonRect.Height / 2.0F, SpinButtonPart.Up, SpinButtonPart.Down)
    End Function
#End Region

#Region "选区"
    Public Sub SelectAll()
        _textRenderer.SelectAll()
    End Sub
#End Region

#Region "数值核心"
    Private Sub TextRenderer_TextChanged(sender As Object, e As EventArgs)
        RaiseEvent TextChanged(Me, EventArgs.Empty)
        Dim parsed As Double
        If TryParseNumericText(_textRenderer.Text, parsed) AndAlso parsed >= 最小值 AndAlso parsed <= 最大值 Then
            SetValueCore(parsed, False, False)
        End If
    End Sub

    Private Sub StepByButton(part As SpinButtonPart)
        If part = SpinButtonPart.Up Then
            StepValue(小步进值)
            RaiseEvent UpButtonClick(Me, EventArgs.Empty)
        ElseIf part = SpinButtonPart.Down Then
            StepValue(-小步进值)
            RaiseEvent DownButtonClick(Me, EventArgs.Empty)
        End If
    End Sub

    Private Sub StepValue(delta As Double)
        Dim nextValue As Double
        Try
            nextValue = CDbl(CDec(当前值) + CDec(delta))
        Catch
            nextValue = 当前值 + delta
        End Try
        SetValueCore(nextValue, True, False)
        请求V3渲染()
    End Sub

    Private Sub SetValueCore(value As Double, updateText As Boolean, forceTextUpdate As Boolean)
        Dim newValue As Double = ClampValue(value)
        If 小数位数 >= 0 Then newValue = Math.Round(newValue, 小数位数, MidpointRounding.AwayFromZero)
        newValue = ClampValue(newValue)
        Dim changed As Boolean = newValue <> 当前值
        当前值 = newValue
        If updateText Then
            If forceTextUpdate OrElse _textRenderer.Text <> FormatValue(当前值) Then UpdateTextFromValue(True)
        End If
        If changed Then RaiseEvent ValueChanged(Me, EventArgs.Empty)
    End Sub

    Private Function ClampValue(value As Double) As Double
        If Double.IsNaN(value) Then Return 最小值
        If Double.IsPositiveInfinity(value) Then Return 最大值
        If Double.IsNegativeInfinity(value) Then Return 最小值
        Return Math.Max(最小值, Math.Min(最大值, value))
    End Function

    Private Sub UpdateTextFromValue(raiseTextChanged As Boolean)
        Dim displayText As String = FormatValue(当前值)
        _textRenderer.SetText(displayText, displayText.Length, True, raiseTextChanged)
    End Sub

    Private Function FormatValue(value As Double) As String
        Dim rounded As Double = value
        If 小数位数 >= 0 Then
            rounded = Math.Round(value, 小数位数, MidpointRounding.AwayFromZero)
        End If
        If rounded = 0 Then rounded = 0
        Dim text As String
        If 小数位数 >= 0 Then
            text = rounded.ToString("F" & 小数位数, CultureInfo.CurrentCulture)
        Else
            text = rounded.ToString(CultureInfo.CurrentCulture)
        End If
        Dim decimalSeparator As String = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator
        If text.Contains(decimalSeparator) Then
            text = text.TrimEnd("0"c)
            If text.EndsWith(decimalSeparator, StringComparison.Ordinal) Then
                text = text.Substring(0, text.Length - decimalSeparator.Length)
            End If
        End If
        Return text
    End Function

    Private Function FilterInsertText(text As String) As String
        Dim decimalSeparator As String = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator
        Dim sb As New System.Text.StringBuilder(text.Length)
        For Each ch As Char In text.Replace(vbCr, "").Replace(vbLf, "")
            If Char.IsDigit(ch) Then
                sb.Append(ch)
            ElseIf ch = "-"c OrElse ch = ChrW(&H2212) Then
                sb.Append("-"c)
            ElseIf 小数位数 <> 0 AndAlso (ch = "."c OrElse ch = ","c OrElse decimalSeparator.Contains(ch)) Then
                sb.Append(decimalSeparator)
            End If
        Next
        Return sb.ToString()
    End Function

    Private Function IsPotentialNumericText(text As String) As Boolean
        If text Is Nothing Then Return False
        If text = "" Then Return True
        Dim decimalSeparator As String = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator
        If text = "-" AndAlso 最小值 < 0 Then Return True
        If 小数位数 <> 0 Then
            If text = decimalSeparator Then Return True
            If text = "-" & decimalSeparator AndAlso 最小值 < 0 Then Return True
        End If
        Dim normalized As String = NormalizeNumericText(text)
        Dim minusCount As Integer = normalized.Count(Function(ch) ch = "-"c)
        If minusCount > 1 Then Return False
        If normalized.Contains("-"c) AndAlso Not normalized.StartsWith("-", StringComparison.Ordinal) Then Return False
        If normalized.StartsWith("-", StringComparison.Ordinal) AndAlso 最小值 >= 0 Then Return False
        If 小数位数 = 0 AndAlso normalized.Contains("."c) Then Return False
        If normalized.Count(Function(ch) ch = "."c) > 1 Then Return False
        Dim parsed As Double
        Return TryParseNumericText(text, parsed)
    End Function

    Private Function TryParseNumericText(text As String, ByRef value As Double) As Boolean
        value = 0
        If String.IsNullOrWhiteSpace(text) Then Return False
        Dim normalized As String = NormalizeNumericText(text)
        If normalized = "-" OrElse normalized = "." OrElse normalized = "-." Then Return False
        Return Double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, value)
    End Function

    Private Function NormalizeNumericText(text As String) As String
        Dim decimalSeparator As String = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator
        Dim normalized As String = text.Trim()
        normalized = normalized.Replace(ChrW(&H2212), "-"c)
        If decimalSeparator <> "." Then normalized = normalized.Replace(decimalSeparator, ".")
        normalized = normalized.Replace(","c, "."c)
        Return normalized
    End Function

    Private Sub SetTextFromExternal(text As String)
        If Not IsPotentialNumericText(text) Then Return
        Dim parsed As Double
        If TryParseNumericText(text, parsed) Then
            SetValueCore(parsed, True, False)
            Return
        End If
        _textRenderer.SetText(text, text.Length, True, True)
    End Sub

    Private Sub CommitText()
        Dim parsed As Double
        If TryParseNumericText(_textRenderer.Text, parsed) Then
            SetValueCore(parsed, True, True)
        Else
            UpdateTextFromValue(True)
        End If
    End Sub
#End Region

#Region "重复按钮"
    Private Sub StartRepeatTimer()
        _repeatStarted = False
        _repeatTimer.Stop()
        _repeatTimer.Interval = 400
        _repeatTimer.Start()
    End Sub

    Private Sub StopRepeatTimer()
        _repeatTimer.Stop()
        _repeatStarted = False
    End Sub

    Private Sub RepeatTimer_Tick(sender As Object, e As EventArgs)
        If _pressedButton = SpinButtonPart.None Then
            StopRepeatTimer()
            Return
        End If
        If Not _repeatStarted Then
            _repeatStarted = True
            _repeatTimer.Interval = 70
        End If
        StepByButton(_pressedButton)
    End Sub
#End Region

#Region "辅助"
    Private ReadOnly Property ActualButtonAreaWidth As Integer
        Get
            Return Math.Max(1, CInt(Math.Round(按钮区域宽度 * DpiScale(), MidpointRounding.AwayFromZero)))
        End Get
    End Property

    Private Function DpiScale() As Single
        Return V3_DpiContext.FromControl(Me).Scale
    End Function

    Private Sub 请求V3渲染(Optional immediate As Boolean = False)
        请求V3渲染(New Rectangle(Point.Empty, Me.Size), immediate)
    End Sub

    Private Sub 请求V3渲染(dirtyRect As Rectangle, Optional immediate As Boolean = False)
        If Me.IsDisposed Then Return
        V3_InvalidationRouter.RequestRender(Me, dirtyRect)
    End Sub

    Private Sub SyncTextRendererLayout()
        _textRenderer.BorderSize = 边框宽度
        _textRenderer.RightReservedWidth = ActualButtonAreaWidth
    End Sub

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            请求V3渲染()
        End If
    End Sub
#End Region

#Region "事件"
    Protected Overrides Sub OnGotFocus(e As EventArgs)
        MyBase.OnGotFocus(e)
        _textRenderer.StartCaretBlink()
    End Sub

    Protected Overrides Sub OnLostFocus(e As EventArgs)
        MyBase.OnLostFocus(e)
        CommitText()
        _textRenderer.StopCaretBlink()
        _mouseDownSelecting = False
        _pressedButton = SpinButtonPart.None
        StopRepeatTimer()
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        SyncTextRendererLayout()
        _textRenderer.EnsureCaretVisible()
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        SyncTextRendererLayout()
        _textRenderer.EnsureCaretVisible()
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        If Not Enabled Then
            _textRenderer.StopCaretBlink()
            鼠标状态 = MouseStateEnum.Normal
            _hoverButton = SpinButtonPart.None
            _pressedButton = SpinButtonPart.None
            StopRepeatTimer()
        End If
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        请求V3渲染()
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
