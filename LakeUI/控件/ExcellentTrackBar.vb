Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Drawing.Drawing2D

<DefaultEvent("ValueChanged")>
Public Class ExcellentTrackBar

    Public Event ValueChanged As EventHandler

    Public Sub New()
        InitializeComponent()
        SetStyle(ControlStyles.Selectable, True)
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
        <DefaultValue(0)>
        Public Property Position As Integer = 0
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

        Protected Overrides Sub InsertItem(index As Integer, item As TrackLabel)
            ' 在末尾追加时自动递增 Position（设计器和运行时均生效）
            If index = Me.Count AndAlso Me.Count > 0 AndAlso item.Position = 0 Then
                item.Position = Me.Max(Function(l) l.Position) + 1
            End If
            MyBase.InsertItem(index, item)
            _owner.Invalidate()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            MyBase.RemoveItem(index)
            _owner.Invalidate()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, item As TrackLabel)
            MyBase.SetItem(index, item)
            _owner.Invalidate()
        End Sub

        Protected Overrides Sub ClearItems()
            MyBase.ClearItems()
            _owner.Invalidate()
        End Sub
    End Class

    Public Class StringItemCollection
        Inherits Collection(Of String)

        Private ReadOnly _owner As ExcellentTrackBar

        Friend Sub New(owner As ExcellentTrackBar)
            _owner = owner
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As String)
            MyBase.InsertItem(index, item)
            _owner.SyncStringRange()
            _owner.Invalidate()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            MyBase.RemoveItem(index)
            _owner.SyncStringRange()
            _owner.Invalidate()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, item As String)
            MyBase.SetItem(index, item)
            _owner.Invalidate()
        End Sub

        Protected Overrides Sub ClearItems()
            MyBase.ClearItems()
            _owner.SyncStringRange()
            _owner.Invalidate()
        End Sub
    End Class
#End Region

#Region "属性"
    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            Me.Invalidate()
        End If
    End Sub

    Private ReadOnly 动画助手 As New AnimationHelper(Me) With {.Duration = 0}

    Private Function 计算值比例(val As Integer) As Single
        Dim range As Integer = 最大值 - 最小值
        If range = 0 Then Return 0.0F
        Return (val - 最小值) / CSng(range)
    End Function

    <Category("LakeUI"), Description(Class1.动画时长描述词), DefaultValue(0), Browsable(True)>
    Public Property AnimationDuration As Integer
        Get
            Return 动画助手.Duration
        End Get
        Set(value As Integer)
            动画助手.Duration = Math.Max(0, value)
        End Set
    End Property

    <Category("LakeUI"), Description("动画帧率上限，设为0则不限制"), DefaultValue(60), Browsable(True)>
    Public Property AnimationFPS As Integer
        Get
            Return 动画助手.FPS
        End Get
        Set(value As Integer)
            动画助手.FPS = Math.Max(0, value)
        End Set
    End Property

    Private 超采样倍率 As Integer = 1
    <Category("LakeUI"), Description(Class1.超采样抗锯齿描述词), DefaultValue(GetType(Class1.SuperSamplingScaleEnum), "OFF"), Browsable(True)>
    Public Property SuperSamplingScale As Class1.SuperSamplingScaleEnum
        Get
            Return 超采样倍率
        End Get
        Set(value As Class1.SuperSamplingScaleEnum)
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

    Private 最小值 As Integer = 0
    <Category("LakeUI"), Description("最小值"), DefaultValue(0), Browsable(True)>
    Public Property Minimum As Integer
        Get
            Return 最小值
        End Get
        Set(value As Integer)
            If value = 最小值 Then Return
            If value > 最大值 Then value = 最大值
            最小值 = value
            If 当前值 < 最小值 Then 当前值 = 最小值
            动画助手.SetImmediate(计算值比例(当前值))
            Me.Invalidate()
        End Set
    End Property

    Private 最大值 As Integer = 100
    <Category("LakeUI"), Description("最大值"), DefaultValue(100), Browsable(True)>
    Public Property Maximum As Integer
        Get
            Return 最大值
        End Get
        Set(value As Integer)
            If value = 最大值 Then Return
            If value < 最小值 Then value = 最小值
            最大值 = value
            If 当前值 > 最大值 Then 当前值 = 最大值
            动画助手.SetImmediate(计算值比例(当前值))
            Me.Invalidate()
        End Set
    End Property

    Private 当前值 As Integer = 0
    <Category("LakeUI"), Description("当前值"), DefaultValue(0), Browsable(True)>
    Public Property Value As Integer
        Get
            Return 当前值
        End Get
        Set(value As Integer)
            Dim newVal As Integer = Math.Max(最小值, Math.Min(最大值, value))
            If newVal = 当前值 Then Return
            当前值 = newVal
            Dim ratio As Single = 计算值比例(newVal)
            动画助手.AnimateTo(ratio)
            RaiseEvent ValueChanged(Me, EventArgs.Empty)
        End Set
    End Property

    Private 小步进值 As Integer = 1
    <Category("LakeUI"), Description("方向键和鼠标滚轮的步进值"), DefaultValue(1), Browsable(True)>
    Public Property SmallChange As Integer
        Get
            Return 小步进值
        End Get
        Set(value As Integer)
            小步进值 = Math.Max(1, value)
        End Set
    End Property

    Private 大步进值 As Integer = 10
    <Category("LakeUI"), Description("Page Up/Down 的步进值"), DefaultValue(10), Browsable(True)>
    Public Property LargeChange As Integer
        Get
            Return 大步进值
        End Get
        Set(value As Integer)
            大步进值 = Math.Max(1, value)
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
            If 使用字符串列表 AndAlso 当前值 >= 0 AndAlso 当前值 < 字符串集合.Count Then
                Return 字符串集合(当前值)
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

    Private 标签字体 As Font = Nothing
    <Category("LakeUI"), Description("刻度标签字体，为空时使用控件的 Font"), DefaultValue(GetType(Font), Nothing), Browsable(True)>
    Public Property LabelFont As Font
        Get
            Return 标签字体
        End Get
        Set(value As Font)
            SetValue(标签字体, value)
        End Set
    End Property

    Private Function 获取标签字体() As Font
        Return If(标签字体, Me.Font)
    End Function

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

    Public Sub AddLabel(position As Integer, text As String, side As LabelSideEnum)
        标签列表.Add(New TrackLabel() With {.Position = position, .Text = text, .Side = side})
    End Sub

    ''' <summary>自动使用下一个递增索引追加标签，无需手动指定 Position</summary>
    Public Sub AddLabel(text As String, side As LabelSideEnum)
        Dim nextPos As Integer = If(标签列表.Count = 0, 0, 标签列表.Max(Function(l) l.Position) + 1)
        标签列表.Add(New TrackLabel() With {.Position = nextPos, .Text = text, .Side = side})
    End Sub

    Public Sub ClearLabels()
        标签列表.Clear()
    End Sub
#End Region

#Region "布局计算"
    Private Function 获取标签显示文字(lbl As TrackLabel) As String
        If 使用字符串列表 AndAlso lbl.Position >= 0 AndAlso lbl.Position < 字符串集合.Count Then
            Return If(String.IsNullOrEmpty(lbl.Text), 字符串集合(lbl.Position), lbl.Text)
        End If
        Return lbl.Text
    End Function

    Private Function 计算轨道区域() As RectangleF
        Dim fontH As Integer = TextRenderer.MeasureText("A", 获取标签字体()).Height
        ' 单侧标签所需空间：从轨道边缘出发 2px 间隙 + 连线 + 文字
        Dim labelUnit As Single = 轨道粗细 / 2.0F + fontH + 标签连线长度 + 2
        If 方向 = TrackOrientationEnum.Horizontal Then
            Dim hasTop As Boolean = 标签列表.Any(Function(l) l.Side = LabelSideEnum.TopOrLeft)
            Dim hasBot As Boolean = 标签列表.Any(Function(l) l.Side = LabelSideEnum.BottomOrRight)
            ' 轨道中心到内容顶/底的距离：有标签取 labelUnit，无标签取滑块半高
            Dim aboveCenter As Single = If(hasTop, Math.Max(滑块高度 / 2.0F, labelUnit), 滑块高度 / 2.0F)
            Dim belowCenter As Single = If(hasBot, Math.Max(滑块高度 / 2.0F, labelUnit), 滑块高度 / 2.0F)
            ' 整体内容在控件内居中
            Dim centerY As Single = (Me.Height - aboveCenter - belowCenter) / 2.0F + aboveCenter
            Return New RectangleF(滑块宽度 / 2.0F, centerY - 轨道粗细 / 2.0F,
                                  Me.Width - 滑块宽度, 轨道粗细)
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
            Dim leftUnit As Single = 轨道粗细 / 2.0F + maxLeftW + 标签连线长度 + 2
            Dim rightUnit As Single = 轨道粗细 / 2.0F + maxRightW + 标签连线长度 + 2
            Dim leftCenter As Single = If(hasLeft, Math.Max(滑块宽度 / 2.0F, leftUnit), 滑块宽度 / 2.0F)
            Dim rightCenter As Single = If(hasRight, Math.Max(滑块宽度 / 2.0F, rightUnit), 滑块宽度 / 2.0F)
            Dim centerX As Single = (Me.Width - leftCenter - rightCenter) / 2.0F + leftCenter
            Return New RectangleF(centerX - 轨道粗细 / 2.0F, 滑块高度 / 2.0F,
                                  轨道粗细, Me.Height - 滑块高度)
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
        Dim center As Single = 计算滑块中心坐标()
        Dim trackRect As RectangleF = 计算轨道区域()
        If 方向 = TrackOrientationEnum.Horizontal Then
            Dim thumbY As Single = trackRect.Y + trackRect.Height / 2.0F - 滑块高度 / 2.0F
            Return New RectangleF(center - 滑块宽度 / 2.0F, thumbY, 滑块宽度, 滑块高度)
        Else
            Dim thumbX As Single = trackRect.X + trackRect.Width / 2.0F - 滑块宽度 / 2.0F
            Return New RectangleF(thumbX, center - 滑块高度 / 2.0F, 滑块宽度, 滑块高度)
        End If
    End Function

    Private Function 计算值对应轨道坐标(position As Integer) As Single
        Dim range As Integer = 最大值 - 最小值
        Dim trackRect As RectangleF = 计算轨道区域()
        If range = 0 Then
            Return If(方向 = TrackOrientationEnum.Horizontal, trackRect.X, trackRect.Bottom)
        End If
        Dim ratio As Single = Math.Max(0, Math.Min(1, (position - 最小值) / CSng(range)))
        If 方向 = TrackOrientationEnum.Horizontal Then
            Return trackRect.X + ratio * trackRect.Width
        Else
            Return trackRect.Bottom - ratio * trackRect.Height
        End If
    End Function

    Private Function 计算鼠标响应区域() As RectangleF
        Dim trackRect As RectangleF = 计算轨道区域()
        Dim thumbRect As RectangleF = 计算滑块矩形()
        If 方向 = TrackOrientationEnum.Horizontal Then
            Dim top As Single = Math.Min(thumbRect.Y, trackRect.Y - 2 - 标签连线长度)
            Dim bottom As Single = Math.Max(thumbRect.Bottom, trackRect.Bottom + 2 + 标签连线长度)
            Return New RectangleF(trackRect.X - 滑块宽度 / 2.0F, top, trackRect.Width + 滑块宽度, bottom - top)
        Else
            Dim left As Single = Math.Min(thumbRect.X, trackRect.X - 2 - 标签连线长度)
            Dim right As Single = Math.Max(thumbRect.Right, trackRect.Right + 2 + 标签连线长度)
            Return New RectangleF(left, trackRect.Y - 滑块高度 / 2.0F, right - left, trackRect.Height + 滑块高度)
        End If
    End Function
#End Region

#Region "绘制"
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim _ssaa As Integer = If(Class1.GlobalSSAA > 1, Class1.GlobalSSAA, 超采样倍率)
        Dim thumbRect As RectangleF = 计算滑块矩形()
        If _ssaa > 1 Then
            Using bmp As New Bitmap(Me.Width * _ssaa, Me.Height * _ssaa)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.ScaleTransform(_ssaa, _ssaa)
                    绘制图形内容(g, thumbRect)
                End Using
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                e.Graphics.DrawImage(bmp, 0, 0, Me.Width, Me.Height)
            End Using
        Else
            绘制图形内容(e.Graphics, thumbRect)
        End If
        ' 文字独立渲染，不经过 SSAA
        绘制标签文字(e.Graphics)
        绘制滑块文字(e.Graphics, thumbRect)
        If Not Enabled Then
            Using brush As New SolidBrush(Color.FromArgb(128, BackColor))
                e.Graphics.FillRectangle(brush, ClientRectangle)
            End Using
        End If
    End Sub

    Private Sub 绘制图形内容(g As Graphics, thumbRect As RectangleF)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        g.InterpolationMode = InterpolationMode.HighQualityBicubic
        绘制轨道(g)
        绘制标签连线(g)
        绘制滑块(g, thumbRect)
    End Sub

    Private Sub 绘制轨道(g As Graphics)
        Dim trackRect As RectangleF = 计算轨道区域()
        If trackRect.Width <= 0 OrElse trackRect.Height <= 0 Then Return
        Dim hasRadius As Boolean = 轨道圆角半径 > 0

        ' 背景轨道
        If hasRadius Then
            Using path = RectangleRenderer.创建圆角矩形路径(trackRect, 轨道圆角半径)
                RectangleRenderer.绘制圆角背景(g, path, trackRect, 轨道颜色, Color.Empty, TrackOrientationEnum.Horizontal)
                If 轨道边框宽度 > 0 AndAlso 轨道边框颜色 <> Color.Empty Then
                    RectangleRenderer.绘制圆角边框(g, path, 轨道边框颜色, 轨道边框宽度)
                End If
            End Using
        Else
            RectangleRenderer.绘制矩形背景(g, trackRect, 轨道颜色, Color.Empty, TrackOrientationEnum.Horizontal)
            If 轨道边框宽度 > 0 AndAlso 轨道边框颜色 <> Color.Empty Then
                RectangleRenderer.绘制矩形边框(g, trackRect, 轨道边框颜色, 轨道边框宽度)
            End If
        End If

        ' 已完成部分填充
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

        If hasRadius Then
            Using clipPath = RectangleRenderer.创建圆角矩形路径(trackRect, 轨道圆角半径)
                g.SetClip(clipPath)
            End Using
        Else
            g.SetClip(trackRect)
        End If
        Using brush As New SolidBrush(轨道填充颜色)
            g.FillRectangle(brush, fillRect)
        End Using
        g.ResetClip()
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

    Private Sub 绘制滑块(g As Graphics, thumbRect As RectangleF)
        Dim currentColor As Color = 获取当前滑块颜色()
        Dim currentBorderColor As Color = 获取当前滑块边框颜色()
        If 滑块圆角半径 > 0 Then
            Using path = RectangleRenderer.创建圆角矩形路径(thumbRect, 滑块圆角半径)
                RectangleRenderer.绘制圆角背景(g, path, thumbRect, currentColor, 滑块渐变颜色, TrackOrientationEnum.Vertical)
                RectangleRenderer.绘制圆角边框(g, path, currentBorderColor, 滑块边框宽度)
            End Using
        Else
            RectangleRenderer.绘制矩形背景(g, thumbRect, currentColor, 滑块渐变颜色, TrackOrientationEnum.Vertical)
            RectangleRenderer.绘制矩形边框(g, thumbRect, currentBorderColor, 滑块边框宽度)
        End If
    End Sub

    Private Sub 绘制标签连线(g As Graphics)
        If 标签列表.Count = 0 Then Return
        Dim trackRect As RectangleF = 计算轨道区域()
        Using pen As New Pen(标签连线颜色, 1)
            For Each lbl In 标签列表
                If lbl.Position < 最小值 OrElse lbl.Position > 最大值 Then Continue For
                Dim coord As Single = 计算值对应轨道坐标(lbl.Position)
                If 方向 = TrackOrientationEnum.Horizontal Then
                    If lbl.Side = LabelSideEnum.TopOrLeft Then
                        g.DrawLine(pen, coord, trackRect.Y - 2, coord, trackRect.Y - 2 - 标签连线长度)
                    Else
                        g.DrawLine(pen, coord, trackRect.Bottom + 2, coord, trackRect.Bottom + 2 + 标签连线长度)
                    End If
                Else
                    If lbl.Side = LabelSideEnum.TopOrLeft Then
                        g.DrawLine(pen, trackRect.X - 2, coord, trackRect.X - 2 - 标签连线长度, coord)
                    Else
                        g.DrawLine(pen, trackRect.Right + 2, coord, trackRect.Right + 2 + 标签连线长度, coord)
                    End If
                End If
            Next
        End Using
    End Sub

    Private Sub 绘制标签文字(g As Graphics)
        If 标签列表.Count = 0 Then Return
        Dim trackRect As RectangleF = 计算轨道区域()
        For Each lbl In 标签列表
            If lbl.Position < 最小值 OrElse lbl.Position > 最大值 Then Continue For
            Dim displayText As String = 获取标签显示文字(lbl)
            If String.IsNullOrEmpty(displayText) Then Continue For
            Dim labelFont As Font = 获取标签字体()
            Dim textSize As Size = TextRenderer.MeasureText(displayText, labelFont)
            Dim coord As Single = 计算值对应轨道坐标(lbl.Position)
            If 方向 = TrackOrientationEnum.Horizontal Then
                Dim textX As Integer = CInt(coord - textSize.Width / 2.0F)
                If lbl.Side = LabelSideEnum.TopOrLeft Then
                    TextRenderer.DrawText(g, displayText, labelFont,
                                          New Point(textX, CInt(trackRect.Y - 2 - 标签连线长度 - textSize.Height)),
                                          标签颜色)
                Else
                    TextRenderer.DrawText(g, displayText, labelFont,
                                          New Point(textX, CInt(trackRect.Bottom + 2 + 标签连线长度)),
                                          标签颜色)
                End If
            Else
                Dim textY As Integer = CInt(coord - textSize.Height / 2.0F)
                If lbl.Side = LabelSideEnum.TopOrLeft Then
                    TextRenderer.DrawText(g, displayText, labelFont,
                                          New Point(CInt(trackRect.X - 2 - 标签连线长度 - textSize.Width), textY),
                                          标签颜色)
                Else
                    TextRenderer.DrawText(g, displayText, labelFont,
                                          New Point(CInt(trackRect.Right + 2 + 标签连线长度), textY),
                                          标签颜色)
                End If
            End If
        Next
    End Sub

    Private Sub 绘制滑块文字(g As Graphics, thumbRect As RectangleF)
        If 滑块文字模式 = ThumbTextModeEnum.None Then Return
        Dim displayText As String
        Select Case 滑块文字模式
            Case ThumbTextModeEnum.Value
                displayText = 当前值.ToString()
            Case ThumbTextModeEnum.StringItem
                displayText = If(使用字符串列表 AndAlso 当前值 >= 0 AndAlso 当前值 < 字符串集合.Count,
                                 字符串集合(当前值), 当前值.ToString())
            Case ThumbTextModeEnum.Custom
                displayText = 滑块自定义文字
            Case Else
                Return
        End Select
        If String.IsNullOrEmpty(displayText) Then Return
        Dim flags As TextFormatFlags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or
                                       TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding
        TextRenderer.DrawText(g, displayText, Me.Font, Rectangle.Round(thumbRect), 滑块文字颜色, flags)
    End Sub
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
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        鼠标状态 = MouseStateEnum.Normal
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not Enabled OrElse e.Button <> MouseButtons.Left Then Return
        鼠标状态 = MouseStateEnum.Pressed
        Me.Focus()
        Me.Invalidate()
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
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        If Not Enabled Then Return
        Value = Math.Max(最小值, Math.Min(最大值, 当前值 + If(e.Delta > 0, 小步进值, -小步进值)))
    End Sub

    Private Sub 更新值从坐标(point As Point)
        Dim trackRect As RectangleF = 计算轨道区域()
        Dim range As Integer = 最大值 - 最小值
        If range = 0 Then Return
        Dim ratio As Single
        If 方向 = TrackOrientationEnum.Horizontal Then
            ratio = (point.X - trackRect.X) / trackRect.Width
        Else
            ratio = (trackRect.Bottom - point.Y) / trackRect.Height
        End If
        Value = CInt(Math.Round(最小值 + Math.Max(0, Math.Min(1, ratio)) * range))
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If Not Enabled Then Return
        Select Case e.KeyCode
            Case Keys.Right, Keys.Up
                Value = Math.Min(最大值, 当前值 + 小步进值)
                e.Handled = True
            Case Keys.Left, Keys.Down
                Value = Math.Max(最小值, 当前值 - 小步进值)
                e.Handled = True
            Case Keys.PageUp
                Value = Math.Min(最大值, 当前值 + 大步进值)
                e.Handled = True
            Case Keys.PageDown
                Value = Math.Max(最小值, 当前值 - 大步进值)
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
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        Me.Invalidate()
    End Sub

End Class