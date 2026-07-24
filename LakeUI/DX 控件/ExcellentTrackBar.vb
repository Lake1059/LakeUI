Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports D2D = Vortice.Direct2D1
Imports DW = Vortice.DirectWrite

<DefaultEvent("ValueChanged")>
Public Class ExcellentTrackBar
    Implements V3_IGpuRenderable, V3_IGpuInvalidationSource, V3_ISuperSamplingSource

#Region "背景源"
    Private _backgroundSource As Control = Nothing

    <Category("LakeUI"),
     Description("背景采样源。设置后记录关联源控件；V3 渲染由窗口合成器统一调度。"),
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

    Public Event ValueChanged As EventHandler

    Public Sub New()
        InitializeComponent()
        SetStyle(ControlStyles.Selectable, True)
        动画助手.DirtyProvider = AddressOf 滑块动画脏区
        标签列表 = New TrackLabelCollection(Me)
        字符串集合 = New StringItemCollection(Me)
    End Sub

#Region "内部类型"
    Public Enum TrackOrientationEnum
        Horizontal
        Vertical
    End Enum

    Public Enum ThumbTextModeEnum
        ''' <summary>不显示文字</summary>
        None
        ''' <summary>显示当前数值</summary>
        Value
        ''' <summary>显示字符串列表中对应的项目</summary>
        StringItem
        ''' <summary>显示自定义文字</summary>
        Custom
    End Enum

    Public Enum LabelSideEnum
        ''' <summary>横向时为上方，纵向时为左侧</summary>
        TopOrLeft
        ''' <summary>横向时为下方，纵向时为右侧</summary>
        BottomOrRight
    End Enum

    Public Class TrackLabel
        ''' <summary>对应的值（数值模式）或索引（字符串列表模式）</summary>
        <DefaultValue(0.0)>
        Public Property Position As Double = 0
        ''' <summary>显示的文字，字符串列表模式下为空时自动使用列表项目文字</summary>
        <DefaultValue("")>
        Public Property Text As String = ""
        <DefaultValue(GetType(LabelSideEnum), "BottomOrRight")>
        Public Property Side As LabelSideEnum = LabelSideEnum.BottomOrRight
        Public Overrides Function ToString() As String
            Return $"[{Position}] {If(String.IsNullOrEmpty(Text), "(自动)", Text)} · {Side}"
        End Function
    End Class

    Public Class TrackLabelCollection
        Inherits Collection(Of TrackLabel)

        Private ReadOnly _owner As ExcellentTrackBar

        Friend Sub New(owner As ExcellentTrackBar)
            _owner = owner
        End Sub

        Private Sub InvalidateOwner()
            _owner?.请求V3渲染()
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As TrackLabel)
            ' 在末尾追加时自动递增 Position（设计器和运行时均生效）
            ' 仅当 Position 0 已被其他标签占用时才递增，避免无法在零位添加标签
            If index = Me.Count AndAlso Me.Count > 0 AndAlso item.Position = 0.0 AndAlso
               Me.Any(Function(l) l.Position = 0.0) Then
                item.Position = Me.Max(Function(l) l.Position) + 1
            End If
            MyBase.InsertItem(index, item)
            InvalidateOwner()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            MyBase.RemoveItem(index)
            InvalidateOwner()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, item As TrackLabel)
            MyBase.SetItem(index, item)
            InvalidateOwner()
        End Sub

        Protected Overrides Sub ClearItems()
            MyBase.ClearItems()
            InvalidateOwner()
        End Sub
    End Class

    Public Class StringItemCollection
        Inherits Collection(Of String)

        Private ReadOnly _owner As ExcellentTrackBar

        Friend Sub New(owner As ExcellentTrackBar)
            _owner = owner
        End Sub

        Private Sub InvalidateOwner()
            _owner?.请求V3渲染()
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As String)
            MyBase.InsertItem(index, item)
            _owner.SyncStringRange()
            InvalidateOwner()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            MyBase.RemoveItem(index)
            _owner.SyncStringRange()
            InvalidateOwner()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, item As String)
            MyBase.SetItem(index, item)
            InvalidateOwner()
        End Sub

        Protected Overrides Sub ClearItems()
            MyBase.ClearItems()
            _owner.SyncStringRange()
            InvalidateOwner()
        End Sub
    End Class
#End Region

#Region "属性"
    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            请求V3渲染()
        End If
    End Sub

    Private ReadOnly 动画助手 As New V3_AnimationHelper(Me) With {.Duration = 0}

    Private Sub 滑块动画脏区(helper As V3_AnimationHelper, owner As Control, sink As V3_AnimationHelper.InvalidateRegionSink)
        sink.InvalidateAll()
    End Sub

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

    Private Function 计算值比例(val As Double) As Single
        Dim range As Double = 最大值 - 最小值
        If range = 0 Then Return 0.0F
        Return CSng((val - 最小值) / range)
    End Function

    <Category("LakeUI"), Description(GlobalOptions.动画时长描述词), DefaultValue(0), Browsable(True)>
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

    Private 超采样倍率 As Integer = 1
    <Category("LakeUI"), Description(GlobalOptions.超采样抗锯齿描述词), DefaultValue(GetType(GlobalOptions.SuperSamplingScaleEnum), "OFF"), Browsable(True)>
    Public Property SuperSamplingScale As GlobalOptions.SuperSamplingScaleEnum Implements V3_ISuperSamplingSource.SuperSamplingScale
        Get
            Return 超采样倍率
        End Get
        Set(value As GlobalOptions.SuperSamplingScaleEnum)
            SetValue(超采样倍率, value)
        End Set
    End Property

    Private 方向 As TrackOrientationEnum = TrackOrientationEnum.Horizontal
    <Category("LakeUI"), Description("控件方向"), DefaultValue(GetType(TrackOrientationEnum), "Horizontal"), Browsable(True)>
    Public Property Orientation As TrackOrientationEnum
        Get
            Return 方向
        End Get
        Set(value As TrackOrientationEnum)
            SetValue(方向, value)
        End Set
    End Property

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
            If 当前值 < 最小值 Then 当前值 = 最小值
            动画助手.SetImmediate(计算值比例(当前值))
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
            If 当前值 > 最大值 Then 当前值 = 最大值
            动画助手.SetImmediate(计算值比例(当前值))
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
            Dim newVal As Double = Math.Max(最小值, Math.Min(最大值, value))
            If newVal = 当前值 Then Return
            当前值 = newVal
            Dim ratio As Single = 计算值比例(newVal)
            动画助手.AnimateTo(ratio)
            RaiseEvent ValueChanged(Me, EventArgs.Empty)
        End Set
    End Property

    Private 小步进值 As Double = 1
    <Category("LakeUI"), Description("方向键和鼠标滚轮的步进值"), DefaultValue(1.0), Browsable(True)>
    Public Property SmallChange As Double
        Get
            Return 小步进值
        End Get
        Set(value As Double)
            小步进值 = Math.Max(0.001, value)
        End Set
    End Property

    Private 大步进值 As Double = 10
    <Category("LakeUI"), Description("Page Up/Down 的步进值"), DefaultValue(10.0), Browsable(True)>
    Public Property LargeChange As Double
        Get
            Return 大步进值
        End Get
        Set(value As Double)
            大步进值 = Math.Max(0.001, value)
        End Set
    End Property

    Private 字符串集合 As StringItemCollection
    <Category("LakeUI"), Description("字符串列表，启用 UseStringItems 后滑动条的范围将对应列表的索引顺序"),
     Browsable(True),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
     Editor("System.Windows.Forms.Design.StringCollectionEditor, System.Windows.Forms.Design", GetType(System.Drawing.Design.UITypeEditor))>
    Public ReadOnly Property StringItems As StringItemCollection
        Get
            Return 字符串集合
        End Get
    End Property

    Friend Sub SyncStringRange()
        最小值 = 0
        最大值 = If(字符串集合.Count > 0, 字符串集合.Count - 1, 0)
        当前值 = Math.Max(最小值, Math.Min(最大值, 当前值))
        动画助手.SetImmediate(计算值比例(当前值))
    End Sub

    Private 使用字符串列表 As Boolean = False
    <Category("LakeUI"), Description("启用字符串列表模式，范围自动绑定到 StringItems 的索引"), DefaultValue(False), Browsable(True)>
    Public Property UseStringItems As Boolean
        Get
            Return 使用字符串列表
        End Get
        Set(value As Boolean)
            SetValue(使用字符串列表, value)
        End Set
    End Property

    ''' <summary>字符串列表模式下当前选中的字符串，数值模式下返回空字符串</summary>
    <Browsable(False)>
    Public ReadOnly Property CurrentStringItem As String
        Get
            If 使用字符串列表 Then
                Dim idx As Integer = CInt(Math.Round(当前值))
                If idx >= 0 AndAlso idx < 字符串集合.Count Then
                    Return 字符串集合(idx)
                End If
            End If
            Return String.Empty
        End Get
    End Property

    Private 轨道颜色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("轨道背景颜色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
    Public Property TrackColor As Color
        Get
            Return 轨道颜色
        End Get
        Set(value As Color)
            SetValue(轨道颜色, value)
        End Set
    End Property

    Private 轨道填充颜色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("轨道已完成部分的填充颜色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
    Public Property TrackFillColor As Color
        Get
            Return 轨道填充颜色
        End Get
        Set(value As Color)
            SetValue(轨道填充颜色, value)
        End Set
    End Property

    Private 轨道粗细 As Integer = 6
    <Category("LakeUI"), Description("轨道粗细"), DefaultValue(6), Browsable(True)>
    Public Property TrackThickness As Integer
        Get
            Return 轨道粗细
        End Get
        Set(value As Integer)
            SetValue(轨道粗细, value)
        End Set
    End Property

    Private 轨道圆角半径 As Integer = 3
    <Category("LakeUI"), Description("轨道圆角半径"), DefaultValue(3), Browsable(True)>
    Public Property TrackRadius As Integer
        Get
            Return 轨道圆角半径
        End Get
        Set(value As Integer)
            SetValue(轨道圆角半径, value)
        End Set
    End Property

    Private 轨道边框颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("轨道边框颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property TrackBorderColor As Color
        Get
            Return 轨道边框颜色
        End Get
        Set(value As Color)
            SetValue(轨道边框颜色, value)
        End Set
    End Property

    Private 轨道边框宽度 As Integer = 0
    <Category("LakeUI"), Description("轨道边框宽度，0 为不显示"), DefaultValue(0), Browsable(True)>
    Public Property TrackBorderWidth As Integer
        Get
            Return 轨道边框宽度
        End Get
        Set(value As Integer)
            SetValue(轨道边框宽度, value)
        End Set
    End Property

    Private 滑块宽度 As Integer = 20
    <Category("LakeUI"), Description("滑块宽度"), DefaultValue(20), Browsable(True)>
    Public Property ThumbWidth As Integer
        Get
            Return 滑块宽度
        End Get
        Set(value As Integer)
            SetValue(滑块宽度, value)
        End Set
    End Property

    Private 滑块高度 As Integer = 20
    <Category("LakeUI"), Description("滑块高度"), DefaultValue(20), Browsable(True)>
    Public Property ThumbHeight As Integer
        Get
            Return 滑块高度
        End Get
        Set(value As Integer)
            SetValue(滑块高度, value)
        End Set
    End Property

    Private 滑块颜色 As Color = Color.FromArgb(100, 180, 255)
    <Category("LakeUI"), Description("滑块填充颜色"), DefaultValue(GetType(Color), "100,180,255"), Browsable(True)>
    Public Property ThumbColor As Color
        Get
            Return 滑块颜色
        End Get
        Set(value As Color)
            SetValue(滑块颜色, value)
        End Set
    End Property

    Private 滑块渐变颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("滑块渐变颜色，Empty 为纯色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property ThumbGradientColor As Color
        Get
            Return 滑块渐变颜色
        End Get
        Set(value As Color)
            SetValue(滑块渐变颜色, value)
        End Set
    End Property

    Private 滑块边框颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("滑块边框颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property ThumbBorderColor As Color
        Get
            Return 滑块边框颜色
        End Get
        Set(value As Color)
            SetValue(滑块边框颜色, value)
        End Set
    End Property

    Private 滑块边框宽度 As Integer = 1
    <Category("LakeUI"), Description("滑块边框宽度，0 为不显示"), DefaultValue(1), Browsable(True)>
    Public Property ThumbBorderWidth As Integer
        Get
            Return 滑块边框宽度
        End Get
        Set(value As Integer)
            SetValue(滑块边框宽度, value)
        End Set
    End Property

    Private 滑块圆角半径 As Integer = 4
    <Category("LakeUI"), Description("滑块圆角半径"), DefaultValue(4), Browsable(True)>
    Public Property ThumbRadius As Integer
        Get
            Return 滑块圆角半径
        End Get
        Set(value As Integer)
            SetValue(滑块圆角半径, value)
        End Set
    End Property

    Private 滑块文字模式 As ThumbTextModeEnum = ThumbTextModeEnum.None
    <Category("LakeUI"), Description("滑块上的文字显示模式"), DefaultValue(GetType(ThumbTextModeEnum), "None"), Browsable(True)>
    Public Property ThumbTextMode As ThumbTextModeEnum
        Get
            Return 滑块文字模式
        End Get
        Set(value As ThumbTextModeEnum)
            SetValue(滑块文字模式, value)
        End Set
    End Property

    Private 滑块自定义文字 As String = ""
    <Category("LakeUI"), Description("滑块自定义文字，ThumbTextMode 为 Custom 时有效"), DefaultValue(""), Browsable(True)>
    Public Property ThumbCustomText As String
        Get
            Return 滑块自定义文字
        End Get
        Set(value As String)
            SetValue(滑块自定义文字, value)
        End Set
    End Property

    Private 滑块文字小数位数 As Integer = -1
    <Category("LakeUI"), Description("滑块文字显示的小数位数（四舍五入），-1 为不限制"), DefaultValue(-1), Browsable(True)>
    Public Property ThumbTextDecimalPlaces As Integer
        Get
            Return 滑块文字小数位数
        End Get
        Set(value As Integer)
            SetValue(滑块文字小数位数, Math.Max(-1, value))
        End Set
    End Property

    Private 滑块文字颜色 As Color = Color.White
    <Category("LakeUI"), Description("滑块文字颜色"), DefaultValue(GetType(Color), "White"), Browsable(True)>
    Public Property ThumbTextColor As Color
        Get
            Return 滑块文字颜色
        End Get
        Set(value As Color)
            SetValue(滑块文字颜色, value)
        End Set
    End Property

    Private 鼠标移上时滑块颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时的滑块填充颜色，Empty 时使用 ThumbColor"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverThumbColor As Color
        Get
            Return 鼠标移上时滑块颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时滑块颜色, value)
        End Set
    End Property

    Private 鼠标按下时滑块颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时的滑块填充颜色，Empty 时使用 ThumbColor"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedThumbColor As Color
        Get
            Return 鼠标按下时滑块颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时滑块颜色, value)
        End Set
    End Property

    Private 鼠标移上时滑块边框颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时的滑块边框颜色，Empty 时使用 ThumbBorderColor"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverThumbBorderColor As Color
        Get
            Return 鼠标移上时滑块边框颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时滑块边框颜色, value)
        End Set
    End Property

    Private 鼠标按下时滑块边框颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时的滑块边框颜色，Empty 时使用 ThumbBorderColor"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedThumbBorderColor As Color
        Get
            Return 鼠标按下时滑块边框颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时滑块边框颜色, value)
        End Set
    End Property

    Private 标签颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("刻度标签文字颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property LabelColor As Color
        Get
            Return 标签颜色
        End Get
        Set(value As Color)
            SetValue(标签颜色, value)
        End Set
    End Property

    Private 标签字号 As Single = 9.0F
    Private 标签字体缓存 As Font = Nothing
    Private 标签字体缓存键 As String = ""

    <Category("LakeUI"), Description("刻度标签字号，字体名称和样式遵从控件的 Font"), DefaultValue(9.0F), Browsable(True)>
    Public Property LabelSize As Single
        Get
            Return 标签字号
        End Get
        Set(value As Single)
            If Single.IsNaN(value) OrElse Single.IsInfinity(value) Then value = 9.0F
            value = Math.Max(1.0F, value)
            If 标签字号 <> value Then
                标签字号 = value
                释放标签字体缓存()
                请求V3渲染()
            End If
        End Set
    End Property

    Private Function 获取标签字体() As Font
        Dim cacheKey As String = Me.Font.FontFamily.Name & "|" &
                                  CInt(Me.Font.Style).ToString() & "|" &
                                  标签字号.ToString(Globalization.CultureInfo.InvariantCulture)
        If 标签字体缓存 Is Nothing OrElse 标签字体缓存键 <> cacheKey Then
            释放标签字体缓存()
            标签字体缓存 = New Font(Me.Font.FontFamily, 标签字号, Me.Font.Style, GraphicsUnit.Point)
            标签字体缓存键 = cacheKey
        End If
        Return 标签字体缓存
    End Function

    Private Sub 释放标签字体缓存()
        If 标签字体缓存 IsNot Nothing Then
            标签字体缓存.Dispose()
            标签字体缓存 = Nothing
            标签字体缓存键 = ""
        End If
    End Sub

    Private 标签连线颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("刻度标签连线颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property LabelLineColor As Color
        Get
            Return 标签连线颜色
        End Get
        Set(value As Color)
            SetValue(标签连线颜色, value)
        End Set
    End Property

    Private 标签连线长度 As Integer = 6
    <Category("LakeUI"), Description("刻度标签连线长度"), DefaultValue(6), Browsable(True)>
    Public Property LabelLineLength As Integer
        Get
            Return 标签连线长度
        End Get
        Set(value As Integer)
            SetValue(标签连线长度, value)
        End Set
    End Property

    Private 标签列表 As TrackLabelCollection
    <Category("LakeUI"), Description("刻度标签列表，点击 ... 可在设计器中直接编辑"), Browsable(True),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
     Editor("System.ComponentModel.Design.CollectionEditor, System.Windows.Forms.Design", GetType(System.Drawing.Design.UITypeEditor))>
    Public ReadOnly Property Labels As TrackLabelCollection
        Get
            Return 标签列表
        End Get
    End Property

    Public Sub AddLabel(position As Single, text As String, side As LabelSideEnum)
        标签列表.Add(New TrackLabel() With {.Position = position, .Text = text, .Side = side})
    End Sub

    ''' <summary>自动使用下一个递增索引追加标签，无需手动指定 Position</summary>
    Public Sub AddLabel(text As String, side As LabelSideEnum)
        Dim nextPos As Single = If(标签列表.Count = 0, 0F, 标签列表.Max(Function(l) l.Position) + 1)
        标签列表.Add(New TrackLabel() With {.Position = nextPos, .Text = text, .Side = side})
    End Sub

    Public Sub ClearLabels()
        标签列表.Clear()
    End Sub
#End Region

#Region "布局计算"
    Private Function 获取标签显示文字(lbl As TrackLabel) As String
        If 使用字符串列表 Then
            Dim idx As Integer = CInt(Math.Round(lbl.Position))
            If idx >= 0 AndAlso idx < 字符串集合.Count Then
                Return If(String.IsNullOrEmpty(lbl.Text), 字符串集合(idx), lbl.Text)
            End If
        End If
        Return lbl.Text
    End Function

    Private Function 计算轨道区域() As RectangleF
        Dim s As Single = DpiScale()
        Dim _轨道粗细 As Single = 轨道粗细 * s
        Dim _滑块宽度 As Single = 滑块宽度 * s
        Dim _滑块高度 As Single = 滑块高度 * s
        Dim _标签连线长度 As Single = 标签连线长度 * s
        Dim fontH As Integer = TextRenderer.MeasureText("A", 获取标签字体()).Height
        Dim padL As Single = Padding.Left
        Dim padR As Single = Padding.Right
        Dim padT As Single = Padding.Top
        Dim padB As Single = Padding.Bottom
        Dim availW As Single = Me.Width - padL - padR
        Dim availH As Single = Me.Height - padT - padB
        ' 单侧标签所需空间：从轨道边缘出发 2px 间隙 + 连线 + 文字
        Dim labelUnit As Single = _轨道粗细 / 2.0F + fontH + _标签连线长度 + 2
        If 方向 = TrackOrientationEnum.Horizontal Then
            Dim hasTop As Boolean = 标签列表.Any(Function(l) l.Side = LabelSideEnum.TopOrLeft)
            Dim hasBot As Boolean = 标签列表.Any(Function(l) l.Side = LabelSideEnum.BottomOrRight)
            ' 轨道中心到内容顶/底的距离：有标签取 labelUnit，无标签取滑块半高
            Dim aboveCenter As Single = If(hasTop, Math.Max(_滑块高度 / 2.0F, labelUnit), _滑块高度 / 2.0F)
            Dim belowCenter As Single = If(hasBot, Math.Max(_滑块高度 / 2.0F, labelUnit), _滑块高度 / 2.0F)
            ' 整体内容在控件内居中
            Dim centerY As Single = padT + (availH - aboveCenter - belowCenter) / 2.0F + aboveCenter
            Return New RectangleF(padL + _滑块宽度 / 2.0F, centerY - _轨道粗细 / 2.0F,
                                  availW - _滑块宽度, _轨道粗细)
        Else
            Dim hasLeft As Boolean = 标签列表.Any(Function(l) l.Side = LabelSideEnum.TopOrLeft)
            Dim hasRight As Boolean = 标签列表.Any(Function(l) l.Side = LabelSideEnum.BottomOrRight)
            ' 纵向标签宽度因文字宽度不同而各异
            Dim maxLeftW As Integer = 0
            Dim maxRightW As Integer = 0
            For Each lbl In 标签列表
                Dim txt = 获取标签显示文字(lbl)
                If String.IsNullOrEmpty(txt) Then Continue For
                Dim w = TextRenderer.MeasureText(txt, 获取标签字体()).Width
                If lbl.Side = LabelSideEnum.TopOrLeft Then
                    If w > maxLeftW Then maxLeftW = w
                Else
                    If w > maxRightW Then maxRightW = w
                End If
            Next
            Dim leftUnit As Single = _轨道粗细 / 2.0F + maxLeftW + _标签连线长度 + 2
            Dim rightUnit As Single = _轨道粗细 / 2.0F + maxRightW + _标签连线长度 + 2
            Dim leftCenter As Single = If(hasLeft, Math.Max(_滑块宽度 / 2.0F, leftUnit), _滑块宽度 / 2.0F)
            Dim rightCenter As Single = If(hasRight, Math.Max(_滑块宽度 / 2.0F, rightUnit), _滑块宽度 / 2.0F)
            Dim centerX As Single = padL + (availW - leftCenter - rightCenter) / 2.0F + leftCenter
            Return New RectangleF(centerX - _轨道粗细 / 2.0F, padT + _滑块高度 / 2.0F,
                                  _轨道粗细, availH - _滑块高度)
        End If
    End Function

    Private Function 计算滑块中心坐标() As Single
        Dim trackRect As RectangleF = 计算轨道区域()
        Dim ratio As Single = 动画助手.Progress
        If 方向 = TrackOrientationEnum.Horizontal Then
            Return trackRect.X + ratio * trackRect.Width
        Else
            Return trackRect.Bottom - ratio * trackRect.Height
        End If
    End Function

    Private Function 计算滑块矩形() As RectangleF
        Dim s As Single = DpiScale()
        Dim _滑块宽度 As Single = 滑块宽度 * s
        Dim _滑块高度 As Single = 滑块高度 * s
        Dim center As Single = 计算滑块中心坐标()
        Dim trackRect As RectangleF = 计算轨道区域()
        If 方向 = TrackOrientationEnum.Horizontal Then
            Dim thumbY As Single = trackRect.Y + trackRect.Height / 2.0F - _滑块高度 / 2.0F
            Return New RectangleF(center - _滑块宽度 / 2.0F, thumbY, _滑块宽度, _滑块高度)
        Else
            Dim thumbX As Single = trackRect.X + trackRect.Width / 2.0F - _滑块宽度 / 2.0F
            Return New RectangleF(thumbX, center - _滑块高度 / 2.0F, _滑块宽度, _滑块高度)
        End If
    End Function

    Private Function 计算值对应轨道坐标(position As Double) As Single
        Dim range As Double = 最大值 - 最小值
        Dim trackRect As RectangleF = 计算轨道区域()
        If range = 0 Then
            Return If(方向 = TrackOrientationEnum.Horizontal, trackRect.X, trackRect.Bottom)
        End If
        Dim ratio As Single = CSng(Math.Max(0, Math.Min(1, (position - 最小值) / range)))
        If 方向 = TrackOrientationEnum.Horizontal Then
            Return trackRect.X + ratio * trackRect.Width
        Else
            Return trackRect.Bottom - ratio * trackRect.Height
        End If
    End Function

    Private Function 计算鼠标响应区域() As RectangleF
        Dim s As Single = DpiScale()
        Dim _滑块宽度 As Single = 滑块宽度 * s
        Dim _滑块高度 As Single = 滑块高度 * s
        Dim _标签连线长度 As Single = 标签连线长度 * s
        Dim trackRect As RectangleF = 计算轨道区域()
        Dim thumbRect As RectangleF = 计算滑块矩形()
        If 方向 = TrackOrientationEnum.Horizontal Then
            Dim top As Single = Math.Min(thumbRect.Y, trackRect.Y - 2 - _标签连线长度)
            Dim bottom As Single = Math.Max(thumbRect.Bottom, trackRect.Bottom + 2 + _标签连线长度)
            Return New RectangleF(trackRect.X - _滑块宽度 / 2.0F, top, trackRect.Width + _滑块宽度, bottom - top)
        Else
            Dim left As Single = Math.Min(thumbRect.X, trackRect.X - 2 - _标签连线长度)
            Dim right As Single = Math.Max(thumbRect.Right, trackRect.Right + 2 + _标签连线长度)
            Return New RectangleF(left, trackRect.Y - _滑块高度 / 2.0F, right - left, trackRect.Height + _滑块高度)
        End If
    End Function
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
        If context Is Nothing OrElse Me.Width <= 0 OrElse Me.Height <= 0 Then Return
        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(Me, _backgroundSource, New RectangleF(0, 0, Me.Width, Me.Height))
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), MyBase.BackColor)
        End If

        Dim thumbRect As RectangleF = 计算滑块矩形()
        绘制轨道_GPU(context)
        绘制标签连线_GPU(context)
        绘制滑块_GPU(context, thumbRect)
        绘制标签文字_GPU(context)
        绘制滑块文字_GPU(context, thumbRect)

        If Not Enabled Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), Color.FromArgb(128, BackColor))
        End If
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Private Sub 绘制轨道_GPU(context As D3D_PaintContext)
        Dim s As Single = DpiScale()
        Dim radius As Single = 轨道圆角半径 * s
        Dim borderWidth As Single = 轨道边框宽度 * s
        Dim trackRect As RectangleF = 计算轨道区域()
        If trackRect.Width <= 0 OrElse trackRect.Height <= 0 Then Return

        填充圆角矩形_GPU(context, trackRect, radius, 轨道颜色)
        绘制圆角边框_GPU(context, trackRect, radius, 轨道边框颜色, borderWidth)

        Dim center As Single = 计算滑块中心坐标()
        Dim fillRect As RectangleF
        If 方向 = TrackOrientationEnum.Horizontal Then
            Dim fillW As Single = center - trackRect.X
            If fillW <= 0 Then Return
            fillRect = New RectangleF(trackRect.X, trackRect.Y, fillW, trackRect.Height)
        Else
            Dim fillH As Single = trackRect.Bottom - center
            If fillH <= 0 Then Return
            fillRect = New RectangleF(trackRect.X, center, trackRect.Width, fillH)
        End If

        If radius > 0 Then
            Dim geo = context.GetRoundedRectangleGeometry(trackRect, radius)
            If geo IsNot Nothing Then
                PushGeometryClip_GPU(context, geo, trackRect)
                Try
                    context.FillRectangle(fillRect, 轨道填充颜色)
                Finally
                    context.DeviceContext.PopLayer()
                End Try
            End If
        Else
            context.FillRectangle(fillRect, 轨道填充颜色)
        End If
    End Sub

    Private Sub 绘制滑块_GPU(context As D3D_PaintContext, thumbRect As RectangleF)
        Dim s As Single = DpiScale()
        Dim radius As Single = 滑块圆角半径 * s
        Dim borderWidth As Single = 滑块边框宽度 * s
        填充圆角矩形_GPU(context, thumbRect, radius, 获取当前滑块颜色(), 滑块渐变颜色, System.Windows.Forms.Orientation.Vertical)
        绘制圆角边框_GPU(context, thumbRect, radius, 获取当前滑块边框颜色(), borderWidth)
    End Sub

    Private Sub 绘制标签连线_GPU(context As D3D_PaintContext)
        If 标签列表.Count = 0 OrElse 标签连线颜色.A = 0 Then Return
        Dim lineLength As Single = 标签连线长度 * DpiScale()
        Dim trackRect As RectangleF = 计算轨道区域()
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, 标签连线颜色, context.DeviceGeneration)
        For Each lbl In 标签列表
            If lbl.Position < 最小值 OrElse lbl.Position > 最大值 Then Continue For
            Dim coord As Single = 计算值对应轨道坐标(lbl.Position)
            If 方向 = TrackOrientationEnum.Horizontal Then
                If lbl.Side = LabelSideEnum.TopOrLeft Then
                    context.DeviceContext.DrawLine(New System.Numerics.Vector2(coord, trackRect.Y - 2), New System.Numerics.Vector2(coord, trackRect.Y - 2 - lineLength), brush, 1.0F)
                Else
                    context.DeviceContext.DrawLine(New System.Numerics.Vector2(coord, trackRect.Bottom + 2), New System.Numerics.Vector2(coord, trackRect.Bottom + 2 + lineLength), brush, 1.0F)
                End If
            Else
                If lbl.Side = LabelSideEnum.TopOrLeft Then
                    context.DeviceContext.DrawLine(New System.Numerics.Vector2(trackRect.X - 2, coord), New System.Numerics.Vector2(trackRect.X - 2 - lineLength, coord), brush, 1.0F)
                Else
                    context.DeviceContext.DrawLine(New System.Numerics.Vector2(trackRect.Right + 2, coord), New System.Numerics.Vector2(trackRect.Right + 2 + lineLength, coord), brush, 1.0F)
                End If
            End If
        Next
    End Sub

    Private Sub 绘制标签文字_GPU(context As D3D_PaintContext)
        If 标签列表.Count = 0 Then Return
        Dim lineLength As Single = 标签连线长度 * DpiScale()
        Dim trackRect As RectangleF = 计算轨道区域()
        Dim labelFont As Font = 获取标签字体()
        For Each lbl In 标签列表
            If lbl.Position < 最小值 OrElse lbl.Position > 最大值 Then Continue For
            Dim displayText As String = 获取标签显示文字(lbl)
            If String.IsNullOrEmpty(displayText) Then Continue For
            Dim textSize As Size = TextRenderer.MeasureText(displayText, labelFont)
            Dim coord As Single = 计算值对应轨道坐标(lbl.Position)
            Dim textRect As RectangleF
            If 方向 = TrackOrientationEnum.Horizontal Then
                Dim textX As Single = coord - textSize.Width / 2.0F
                If lbl.Side = LabelSideEnum.TopOrLeft Then
                    textRect = New RectangleF(textX, trackRect.Y - 2 - lineLength - textSize.Height, textSize.Width, textSize.Height)
                Else
                    textRect = New RectangleF(textX, trackRect.Bottom + 2 + lineLength, textSize.Width, textSize.Height)
                End If
            Else
                Dim textY As Single = coord - textSize.Height / 2.0F
                If lbl.Side = LabelSideEnum.TopOrLeft Then
                    textRect = New RectangleF(trackRect.X - 2 - lineLength - textSize.Width, textY, textSize.Width, textSize.Height)
                Else
                    textRect = New RectangleF(trackRect.Right + 2 + lineLength, textY, textSize.Width, textSize.Height)
                End If
            End If
            If textRect.Width > 0 AndAlso textRect.Height > 0 Then
                context.DrawText(displayText, labelFont, 标签颜色, textRect, DW.TextAlignment.Center, DW.ParagraphAlignment.Center)
            End If
        Next
    End Sub

    Private Sub 绘制滑块文字_GPU(context As D3D_PaintContext, thumbRect As RectangleF)
        If 滑块文字模式 = ThumbTextModeEnum.None Then Return
        Dim displayText As String = 获取滑块显示文字()
        If String.IsNullOrEmpty(displayText) Then Return
        context.DrawText(displayText, Me.Font, 滑块文字颜色, thumbRect, DW.TextAlignment.Center, DW.ParagraphAlignment.Center)
    End Sub

    Private Sub 填充圆角矩形_GPU(context As D3D_PaintContext,
                           rect As RectangleF,
                           radius As Single,
                           color As Color,
                           Optional gradientColor As Color = Nothing,
                           Optional gradientDirection As System.Windows.Forms.Orientation = System.Windows.Forms.Orientation.Horizontal)
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        If color.A = 0 AndAlso (gradientColor = Color.Empty OrElse gradientColor.A = 0) Then Return

        Dim brush As D2D.ID2D1Brush = Nothing
        If gradientColor <> Color.Empty AndAlso gradientColor.A > 0 Then
            brush = 创建线性渐变画刷_GPU(context, rect, color, gradientColor, gradientDirection)
        Else
            brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        End If

        If radius > 0 Then
            context.FillRoundedRectangle(rect, radius, brush)
        Else
            context.DeviceContext.FillRectangle(D3D_PaintContext.ToRawRect(rect), brush)
        End If
    End Sub

    Private Sub 绘制圆角边框_GPU(context As D3D_PaintContext, rect As RectangleF, radius As Single, color As Color, strokeWidth As Single)
        If color.A = 0 OrElse strokeWidth <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If radius > 0 Then
            context.DrawRoundedRectangle(rect, radius, brush, strokeWidth)
        Else
            context.DeviceContext.DrawRectangle(D3D_PaintContext.ToRawRect(rect), brush, strokeWidth)
        End If
    End Sub

    Private Shared Function 创建线性渐变画刷_GPU(context As D3D_PaintContext,
                                             bounds As RectangleF,
                                             baseColor As Color,
                                             gradColor As Color,
                                             gradDir As System.Windows.Forms.Orientation) As D2D.ID2D1LinearGradientBrush
        Return context.GetLinearGradientBrush(bounds, baseColor, gradColor, gradDir)
    End Function

    Private Shared Sub PushGeometryClip_GPU(context As D3D_PaintContext, geo As D2D.ID2D1Geometry, bounds As RectangleF)
        Dim parameters As New D2D.LayerParameters With {
            .ContentBounds = New Vortice.RawRectF(bounds.X, bounds.Y, bounds.Right, bounds.Bottom),
            .GeometricMask = geo,
            .MaskAntialiasMode = D2D.AntialiasMode.PerPrimitive,
            .MaskTransform = System.Numerics.Matrix3x2.Identity,
            .Opacity = 1.0F,
            .OpacityBrush = Nothing,
            .LayerOptions = D2D.LayerOptions.None
        }
        context.DeviceContext.PushLayer(parameters, Nothing)
    End Sub

    Private Function 获取当前滑块颜色() As Color
        Select Case 鼠标状态
            Case MouseStateEnum.Hover
                If 鼠标移上时滑块颜色 <> Color.Empty Then Return 鼠标移上时滑块颜色
            Case MouseStateEnum.Pressed
                If 鼠标按下时滑块颜色 <> Color.Empty Then Return 鼠标按下时滑块颜色
        End Select
        Return 滑块颜色
    End Function

    Private Function 获取当前滑块边框颜色() As Color
        Select Case 鼠标状态
            Case MouseStateEnum.Hover
                If 鼠标移上时滑块边框颜色 <> Color.Empty Then Return 鼠标移上时滑块边框颜色
            Case MouseStateEnum.Pressed
                If 鼠标按下时滑块边框颜色 <> Color.Empty Then Return 鼠标按下时滑块边框颜色
        End Select
        Return 滑块边框颜色
    End Function

    Private Function 格式化显示值(val As Double) As String
        Dim rounded As Double
        If 滑块文字小数位数 >= 0 Then
            rounded = Math.Round(val, 滑块文字小数位数, MidpointRounding.AwayFromZero)
        Else
            rounded = val
        End If
        If rounded = 0 Then rounded = 0
        Dim text As String
        If 滑块文字小数位数 >= 0 Then
            text = rounded.ToString("F" & 滑块文字小数位数)
        Else
            text = rounded.ToString()
        End If
        If text.Contains(".") Then
            text = text.TrimEnd("0"c).TrimEnd("."c)
        End If
        Return text
    End Function

    Private Function 获取滑块显示文字() As String
        Select Case 滑块文字模式
            Case ThumbTextModeEnum.Value
                Return 格式化显示值(当前值)
            Case ThumbTextModeEnum.StringItem
                Dim idx As Integer = CInt(Math.Round(当前值))
                Return If(使用字符串列表 AndAlso idx >= 0 AndAlso idx < 字符串集合.Count,
                          字符串集合(idx), 格式化显示值(当前值))
            Case ThumbTextModeEnum.Custom
                Return 滑块自定义文字
            Case Else
                Return String.Empty
        End Select
    End Function
#End Region

#Region "鼠标交互"
    Private Enum MouseStateEnum
        Normal
        Hover
        Pressed
    End Enum
    Private 鼠标状态 As MouseStateEnum = MouseStateEnum.Normal

    Private 正在拖动 As Boolean = False
    Private 拖动偏移 As Integer = 0

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        鼠标状态 = MouseStateEnum.Hover
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        鼠标状态 = MouseStateEnum.Normal
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not Enabled OrElse e.Button <> MouseButtons.Left Then Return
        鼠标状态 = MouseStateEnum.Pressed
        Me.Focus()
        请求V3渲染()
        Dim thumbRect As RectangleF = 计算滑块矩形()
        If thumbRect.Contains(e.Location) Then
            正在拖动 = True
            If 方向 = TrackOrientationEnum.Horizontal Then
                拖动偏移 = e.X - CInt(thumbRect.X + thumbRect.Width / 2.0F)
            Else
                拖动偏移 = e.Y - CInt(thumbRect.Y + thumbRect.Height / 2.0F)
            End If
        ElseIf 计算鼠标响应区域().Contains(e.Location) Then
            更新值从坐标(e.Location)
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If Not Enabled OrElse Not 正在拖动 Then Return
        Dim pt As Point = If(方向 = TrackOrientationEnum.Horizontal,
                             New Point(e.X - 拖动偏移, e.Y),
                             New Point(e.X, e.Y - 拖动偏移))
        更新值从坐标(pt)
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        正在拖动 = False
        鼠标状态 = If(ClientRectangle.Contains(e.Location), MouseStateEnum.Hover, MouseStateEnum.Normal)
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        If Not Enabled Then Return
        Dim delta As Double = If(e.Delta > 0, 小步进值, -小步进值)
        If 方向 = TrackOrientationEnum.Horizontal Then delta = -delta
        Dim nextValue As Double = NumericValuePrecision.RemoveStepNoise(NumericValuePrecision.AddStep(当前值, delta), 最小值, Math.Abs(delta))
        Value = Math.Max(最小值, Math.Min(最大值, nextValue))
    End Sub

    Private Sub 更新值从坐标(point As Point)
        Dim trackRect As RectangleF = 计算轨道区域()
        Dim range As Double = 最大值 - 最小值
        If range = 0 Then Return
        Dim ratio As Double
        If 方向 = TrackOrientationEnum.Horizontal Then
            ratio = (point.X - trackRect.X) / trackRect.Width
        Else
            ratio = (trackRect.Bottom - point.Y) / trackRect.Height
        End If
        Dim rawVal As Double = 最小值 + Math.Max(0.0, Math.Min(1.0, ratio)) * range
        Value = NumericValuePrecision.SnapToIncrement(rawVal, 最小值, 小步进值)
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If Not Enabled Then Return
        Select Case e.KeyCode
            Case Keys.Right, Keys.Up
                Value = Math.Min(最大值, NumericValuePrecision.RemoveStepNoise(NumericValuePrecision.AddStep(当前值, 小步进值), 最小值, 小步进值))
                e.Handled = True
            Case Keys.Left, Keys.Down
                Value = Math.Max(最小值, NumericValuePrecision.RemoveStepNoise(NumericValuePrecision.AddStep(当前值, -小步进值), 最小值, 小步进值))
                e.Handled = True
            Case Keys.PageUp
                Value = Math.Min(最大值, NumericValuePrecision.RemoveStepNoise(NumericValuePrecision.AddStep(当前值, 大步进值), 最小值, 大步进值))
                e.Handled = True
            Case Keys.PageDown
                Value = Math.Max(最小值, NumericValuePrecision.RemoveStepNoise(NumericValuePrecision.AddStep(当前值, -大步进值), 最小值, 大步进值))
                e.Handled = True
            Case Keys.Home
                Value = 最小值
                e.Handled = True
            Case Keys.End
                Value = 最大值
                e.Handled = True
        End Select
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

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        释放标签字体缓存()
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        请求V3渲染()
    End Sub

End Class
