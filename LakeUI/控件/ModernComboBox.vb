Imports System.ComponentModel
Imports System.Drawing.Drawing2D

<DefaultEvent("SelectedIndexChanged")>
Public Class ModernComboBox
    Public Event SelectedIndexChanged As EventHandler
    Public Shadows Event TextChanged As EventHandler
    Public Event DropDownOpened As EventHandler
    Public Event DropDownClosed As EventHandler

    Public Class ItemCollection
        Inherits ObjectModel.Collection(Of String)

        Private _owner As ModernComboBox

        Friend Sub New(owner As ModernComboBox)
            _owner = owner
        End Sub

        Public Overloads Sub AddRange(collection As IEnumerable(Of String))
            For Each s In collection
                Add(s)
            Next
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As String)
            MyBase.InsertItem(index, item)
            If _owner._selectedIndex >= 0 AndAlso index <= _owner._selectedIndex Then
                _owner._selectedIndex += 1
            End If
            _owner.Invalidate()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            MyBase.RemoveItem(index)
            If _owner._selectedIndex = index Then
                _owner._selectedIndex = -1
                _owner._text = String.Empty
                _owner.OnItemsTextChanged()
            ElseIf _owner._selectedIndex > index Then
                _owner._selectedIndex -= 1
            End If
            _owner.Invalidate()
        End Sub

        Protected Overrides Sub ClearItems()
            MyBase.ClearItems()
            Dim hadSelection As Boolean = _owner._selectedIndex >= 0
            _owner._selectedIndex = -1
            If hadSelection Then
                _owner._text = String.Empty
                _owner.OnItemsTextChanged()
            End If
            _owner.Invalidate()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, item As String)
            MyBase.SetItem(index, item)
            If _owner._selectedIndex = index Then
                _owner._text = item
            End If
            _owner.Invalidate()
        End Sub
    End Class

    <TypeConverter(GetType(ExpandableObjectConverter))>
    Public Class ToolTipEntry
        Private _itemText As String = ""
        Private _toolTipText As String = ""

        Public Sub New()
        End Sub

        Public Sub New(itemText As String, toolTipText As String)
            _itemText = If(itemText, "")
            _toolTipText = If(toolTipText, "")
        End Sub

        <Category("LakeUI"), Description("对应的下拉项文本")>
        Public Property ItemText As String
            Get
                Return _itemText
            End Get
            Set(value As String)
                _itemText = If(value, "")
            End Set
        End Property

        <Category("LakeUI"), Description("工具提示内容")>
        Public Property ToolTipText As String
            Get
                Return _toolTipText
            End Get
            Set(value As String)
                _toolTipText = If(value, "")
            End Set
        End Property

        Public Overrides Function ToString() As String
            If String.IsNullOrEmpty(_itemText) Then Return "(空)"
            Return _itemText
        End Function
    End Class

    Public Class ToolTipEntryCollection
        Inherits ObjectModel.Collection(Of ToolTipEntry)

        Private ReadOnly _dict As New Dictionary(Of String, String)(StringComparer.Ordinal)

        Public Sub New()
        End Sub

        Public Function TryGetToolTip(itemText As String, ByRef tipText As String) As Boolean
            Return _dict.TryGetValue(itemText, tipText)
        End Function

        Private Sub RebuildDictionary()
            _dict.Clear()
            For Each entry In Me
                If Not String.IsNullOrEmpty(entry.ItemText) Then
                    _dict(entry.ItemText) = entry.ToolTipText
                End If
            Next
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As ToolTipEntry)
            MyBase.InsertItem(index, item)
            RebuildDictionary()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            MyBase.RemoveItem(index)
            RebuildDictionary()
        End Sub

        Protected Overrides Sub ClearItems()
            MyBase.ClearItems()
            _dict.Clear()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, item As ToolTipEntry)
            MyBase.SetItem(index, item)
            RebuildDictionary()
        End Sub
    End Class

#Region "字段"
    Private _text As String = String.Empty
    Private _caretCol As Integer = 0
    Private _selAnchorCol As Integer = 0
    Private _hasSelection As Boolean = False
    Private _caretVisible As Boolean = True
    Private _caretBlinkTimer As New Timer() With {.Interval = 530}
    Private _scrollXOffset As Integer = 0
    Private _mouseDownSelecting As Boolean = False
    Private _imeComposing As Boolean = False

    Private _items As ItemCollection
    Private _selectedIndex As Integer = -1
    Private _droppedDown As Boolean = False
    Private _dropDownForm As DropDownListForm = Nothing

    Private Enum MouseStateEnum
        Normal
        Hover
        Pressed
    End Enum
    Private 鼠标状态 As MouseStateEnum = MouseStateEnum.Normal
    Private _mouseOverArrow As Boolean = False
    Private _itemToolTips As ToolTipEntryCollection
#End Region

#Region "属性"
    <Category("LakeUI"), Description("主要文本"), DefaultValue(GetType(String), ""), Browsable(True),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
    Public Overrides Property Text As String
        Get
            Return _text
        End Get
        Set(value As String)
            Dim v As String = If(value, "")
            If _text = v Then Return
            _text = v
            _caretCol = 0
            _scrollXOffset = 0
            ClearSelection()
            Invalidate()
            RaiseEvent TextChanged(Me, EventArgs.Empty)
        End Set
    End Property
    Private Function ShouldSerializeText() As Boolean
        Return Not String.IsNullOrEmpty(Text)
    End Function
    Public Overrides Sub ResetText()
        Text = String.Empty
    End Sub

    <DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
     Editor("System.Windows.Forms.Design.StringCollectionEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing"),
     Category("LakeUI"), Description("下拉列表项"), Browsable(True)>
    Public ReadOnly Property Items As ItemCollection
        Get
            Return _items
        End Get
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SelectedIndex As Integer
        Get
            Return _selectedIndex
        End Get
        Set(value As Integer)
            If value < -1 OrElse value >= _items.Count Then value = -1
            If _selectedIndex = value Then Return
            _selectedIndex = value
            If _selectedIndex >= 0 Then
                _text = _items(_selectedIndex)
            End If
            _caretCol = _text.Length
            _scrollXOffset = 0
            ClearSelection()
            Invalidate()
            RaiseEvent TextChanged(Me, EventArgs.Empty)
            RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
        End Set
    End Property

    <Category("LakeUI"), Description("选中文本"), Browsable(False)>
    Public ReadOnly Property SelectedItem As String
        Get
            If _selectedIndex >= 0 AndAlso _selectedIndex < _items.Count Then
                Return _items(_selectedIndex)
            End If
            Return Nothing
        End Get
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property DroppedDown As Boolean
        Get
            Return _droppedDown
        End Get
        Set(value As Boolean)
            If value Then
                OpenDropDown()
            Else
                CloseDropDown()
            End If
        End Set
    End Property

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

    Private 渐变方向 As Orientation = Orientation.Vertical
    <Category("LakeUI"), Description("渐变方向"), DefaultValue(GetType(Orientation), "Vertical"), Browsable(True)>
    Public Property BackColorOrientation As Orientation
        Get
            Return 渐变方向
        End Get
        Set(value As Orientation)
            SetValue(渐变方向, value)
        End Set
    End Property

    Private 文本颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("文本颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Overrides Property ForeColor As Color
        Get
            Return 文本颜色
        End Get
        Set(value As Color)
            SetValue(文本颜色, value)
        End Set
    End Property

    Private 行高 As Integer = 25
    <Category("LakeUI"), Description("行高"), DefaultValue(GetType(Integer), "25"), Browsable(True)>
    Public Property LineHeight As Integer
        Get
            Return 行高
        End Get
        Set(value As Integer)
            行高 = Math.Max(10, value)
            Invalidate()
        End Set
    End Property

    Private 光标线宽 As Integer = 2
    <Category("LakeUI"), Description("光标线宽"), DefaultValue(GetType(Integer), "2"), Browsable(True)>
    Public Property CaretWidth As Integer
        Get
            Return 光标线宽
        End Get
        Set(value As Integer)
            光标线宽 = Math.Max(1, value)
            Invalidate()
        End Set
    End Property

    Private 光标颜色 As Color = Color.FromArgb(220, 220, 220)
    <Category("LakeUI"), Description("光标颜色"), DefaultValue(GetType(Color), "220,220,220"), Browsable(True)>
    Public Property CaretColor As Color
        Get
            Return 光标颜色
        End Get
        Set(value As Color)
            SetValue(光标颜色, value)
        End Set
    End Property

    Private 选区背景色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("选区背景色"), DefaultValue(GetType(Color), "80,80,80"), Browsable(True)>
    Public Property SelectionColor As Color
        Get
            Return 选区背景色
        End Get
        Set(value As Color)
            SetValue(选区背景色, value)
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
        Set(v As Color)
            SetValue(有焦点时边框颜色, v)
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

    Private 边框圆角半径 As Integer = 0
    <Category("LakeUI"), Description("边框圆角半径"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property BorderRadius As Integer
        Get
            Return 边框圆角半径
        End Get
        Set(value As Integer)
            SetValue(边框圆角半径, value)
        End Set
    End Property

    Private 启用编辑 As Boolean = False
    <Category("LakeUI"), Description("是否允许用户编辑文本"), DefaultValue(GetType(Boolean), "False"), Browsable(True)>
    Public Property Editable As Boolean
        Get
            Return 启用编辑
        End Get
        Set(value As Boolean)
            启用编辑 = value
            Invalidate()
        End Set
    End Property

    Public Enum TextAlignMode
        Left = 0
        Center = 1
        Right = 2
    End Enum
    Private 文本对齐 As TextAlignMode = TextAlignMode.Left
    <Category("LakeUI"), Description("文本对齐方式"), DefaultValue(TextAlignMode.Left), Browsable(True)>
    Public Property TextAlign As TextAlignMode
        Get
            Return 文本对齐
        End Get
        Set(value As TextAlignMode)
            SetValue(文本对齐, value)
        End Set
    End Property

    Private 水印文本 As String = ""
    <Category("LakeUI"), Description("水印文本"), DefaultValue(GetType(String), ""), Browsable(True)>
    Public Property WaterText As String
        Get
            Return 水印文本
        End Get
        Set(value As String)
            SetValue(水印文本, value)
        End Set
    End Property

    Private 水印颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("水印颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property WaterTextForeColor As Color
        Get
            Return 水印颜色
        End Get
        Set(value As Color)
            SetValue(水印颜色, value)
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

    Private 箭头颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("下拉箭头颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property ArrowColor As Color
        Get
            Return 箭头颜色
        End Get
        Set(value As Color)
            SetValue(箭头颜色, value)
        End Set
    End Property

    Private 箭头大小 As Integer = 15
    <Category("LakeUI"), Description("下拉箭头大小"), DefaultValue(GetType(Integer), "15"), Browsable(True)>
    Public Property ArrowSize As Integer
        Get
            Return 箭头大小
        End Get
        Set(value As Integer)
            箭头大小 = Math.Max(4, value)
            Invalidate()
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

    Private 显示分隔线 As Boolean = False
    <Category("LakeUI"), Description("是否显示箭头区域旁的分隔线"), DefaultValue(GetType(Boolean), "False"), Browsable(True)>
    Public Property ShowSeparator As Boolean
        Get
            Return 显示分隔线
        End Get
        Set(value As Boolean)
            SetValue(显示分隔线, value)
        End Set
    End Property

    Private 下拉背景颜色 As Color = Color.FromArgb(36, 36, 36)
    <Category("LakeUI"), Description("下拉列表背景颜色"), DefaultValue(GetType(Color), "36,36,36"), Browsable(True)>
    Public Property DropDownBackColor As Color
        Get
            Return 下拉背景颜色
        End Get
        Set(value As Color)
            SetValue(下拉背景颜色, value)
        End Set
    End Property

    Private 下拉悬停颜色 As Color = Color.FromArgb(60, 60, 60)
    <Category("LakeUI"), Description("下拉列表悬停颜色"), DefaultValue(GetType(Color), "60,60,60"), Browsable(True)>
    Public Property DropDownHoverColor As Color
        Get
            Return 下拉悬停颜色
        End Get
        Set(value As Color)
            SetValue(下拉悬停颜色, value)
        End Set
    End Property

    Private 下拉选中颜色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("下拉列表选中颜色"), DefaultValue(GetType(Color), "80,80,80"), Browsable(True)>
    Public Property DropDownSelectedColor As Color
        Get
            Return 下拉选中颜色
        End Get
        Set(value As Color)
            SetValue(下拉选中颜色, value)
        End Set
    End Property

    Private 下拉项高度 As Integer = 30
    <Category("LakeUI"), Description("下拉列表项高度"), DefaultValue(GetType(Integer), "30"), Browsable(True)>
    Public Property DropDownItemHeight As Integer
        Get
            Return 下拉项高度
        End Get
        Set(value As Integer)
            下拉项高度 = Math.Max(10, value)
        End Set
    End Property

    Private 最大下拉项数 As Integer = 8
    <Category("LakeUI"), Description("下拉列表最大可见项数"), DefaultValue(GetType(Integer), "8"), Browsable(True)>
    Public Property MaxDropDownItems As Integer
        Get
            Return 最大下拉项数
        End Get
        Set(value As Integer)
            最大下拉项数 = Math.Max(1, value)
        End Set
    End Property

    Private 下拉边框颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("下拉列表边框颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property DropDownBorderColor As Color
        Get
            Return 下拉边框颜色
        End Get
        Set(value As Color)
            SetValue(下拉边框颜色, value)
        End Set
    End Property

    Private 下拉边框宽度 As Integer = 1
    <Category("LakeUI"), Description("下拉列表边框宽度"), DefaultValue(GetType(Integer), "1"), Browsable(True)>
    Public Property DropDownBorderSize As Integer
        Get
            Return 下拉边框宽度
        End Get
        Set(value As Integer)
            SetValue(下拉边框宽度, value)
        End Set
    End Property

    Private 下拉圆角半径 As Integer = 0
    <Category("LakeUI"), Description("下拉列表圆角半径"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property DropDownBorderRadius As Integer
        Get
            Return 下拉圆角半径
        End Get
        Set(value As Integer)
            SetValue(下拉圆角半径, value)
        End Set
    End Property

    Private 下拉高亮左侧偏移 As Integer = 0
    <Category("LakeUI"), Description("下拉列表高亮区域左侧偏移量（正值外扩，负值内缩）"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property DropDownHighlightInsetLeft As Integer
        Get
            Return 下拉高亮左侧偏移
        End Get
        Set(value As Integer)
            SetValue(下拉高亮左侧偏移, value)
        End Set
    End Property

    Private 下拉高亮右侧偏移 As Integer = 0
    <Category("LakeUI"), Description("下拉列表高亮区域右侧偏移量（正值外扩，负值内缩）"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property DropDownHighlightInsetRight As Integer
        Get
            Return 下拉高亮右侧偏移
        End Get
        Set(value As Integer)
            SetValue(下拉高亮右侧偏移, value)
        End Set
    End Property

    Private 下拉间距 As Integer = 0
    <Category("LakeUI"), Description("下拉列表与主体的垂直间距"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property DropDownGap As Integer
        Get
            Return 下拉间距
        End Get
        Set(value As Integer)
            下拉间距 = value
        End Set
    End Property

    Private 下拉滚动条宽度 As Integer = 10
    <Category("LakeUI"), Description("下拉列表滚动条宽度"), DefaultValue(GetType(Integer), "10"), Browsable(True)>
    Public Property DropDownScrollBarWidth As Integer
        Get
            Return 下拉滚动条宽度
        End Get
        Set(value As Integer)
            下拉滚动条宽度 = Math.Max(2, value)
        End Set
    End Property

    Private 下拉滚动条颜色 As Color = Color.FromArgb(140, 140, 140)
    <Category("LakeUI"), Description("下拉列表滚动条滑块颜色"), DefaultValue(GetType(Color), "140, 140, 140"), Browsable(True)>
    Public Property DropDownScrollBarColor As Color
        Get
            Return 下拉滚动条颜色
        End Get
        Set(value As Color)
            下拉滚动条颜色 = value
        End Set
    End Property

    Private 下拉滚动条悬停颜色 As Color = Color.FromArgb(200, 200, 200)
    <Category("LakeUI"), Description("下拉列表滚动条滑块悬停/拖拽颜色"), DefaultValue(GetType(Color), "200, 200, 200"), Browsable(True)>
    Public Property DropDownScrollBarHoverColor As Color
        Get
            Return 下拉滚动条悬停颜色
        End Get
        Set(value As Color)
            下拉滚动条悬停颜色 = value
        End Set
    End Property

    Private 下拉滚动条轨道颜色 As Color = Color.FromArgb(20, 255, 255, 255)
    <Category("LakeUI"), Description("下拉列表滚动条轨道颜色"), DefaultValue(GetType(Color), "20, 255, 255, 255"), Browsable(True)>
    Public Property DropDownScrollBarTrackColor As Color
        Get
            Return 下拉滚动条轨道颜色
        End Get
        Set(value As Color)
            下拉滚动条轨道颜色 = value
        End Set
    End Property

    Private 下拉内边距 As Padding = Padding.Empty
    <Category("LakeUI"), Description("下拉列表内边距"), DefaultValue(GetType(Padding), "0, 0, 0, 0"), Browsable(True)>
    Public Property DropDownPadding As Padding
        Get
            Return 下拉内边距
        End Get
        Set(value As Padding)
            下拉内边距 = value
        End Set
    End Property
    Private Function ShouldSerializeDropDownPadding() As Boolean
        Return 下拉内边距 <> Padding.Empty
    End Function
    Private Sub ResetDropDownPadding()
        下拉内边距 = Padding.Empty
    End Sub

    Private 下拉展开关闭动画时长 As Integer = 150
    <Category("LakeUI"), Description("下拉列表展开/关闭动画时长（毫秒），0 = 无动画"), DefaultValue(GetType(Integer), "150"), Browsable(True)>
    Public Property DropDownAnimationDuration As Integer
        Get
            Return 下拉展开关闭动画时长
        End Get
        Set(value As Integer)
            If value < 0 Then value = 0
            SetValue(下拉展开关闭动画时长, value)
        End Set
    End Property

    Private 下拉悬停动画时长 As Integer = 200
    <Category("LakeUI"), Description("下拉列表悬停高亮移动动画时长（毫秒），0 = 无动画"), DefaultValue(GetType(Integer), "200"), Browsable(True)>
    Public Property DropDownHoverAnimationDuration As Integer
        Get
            Return 下拉悬停动画时长
        End Get
        Set(value As Integer)
            If value < 0 Then value = 0
            SetValue(下拉悬停动画时长, value)
        End Set
    End Property

    Private 下拉动画帧率 As Integer = 60
    <Category("LakeUI"), Description(Class1.动画帧率描述词), DefaultValue(60), Browsable(True)>
    Public Property DropDownAnimationFPS As Integer
        Get
            Return 下拉动画帧率
        End Get
        Set(value As Integer)
            下拉动画帧率 = Math.Max(0, value)
        End Set
    End Property

    <DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
     Editor("System.ComponentModel.Design.CollectionEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing"),
     Category("LakeUI"), Description("下拉项工具提示映射（ItemText → ToolTipText）"), Browsable(True)>
    Public ReadOnly Property ItemToolTips As ToolTipEntryCollection
        Get
            Return _itemToolTips
        End Get
    End Property

    Private 提示背景颜色 As Color = Color.FromArgb(50, 50, 50)
    <Category("LakeUI"), Description("工具提示背景颜色"), DefaultValue(GetType(Color), "50,50,50"), Browsable(True)>
    Public Property ToolTipBackColor As Color
        Get
            Return 提示背景颜色
        End Get
        Set(value As Color)
            SetValue(提示背景颜色, value)
        End Set
    End Property

    Private 提示文本颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("工具提示文本颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property ToolTipForeColor As Color
        Get
            Return 提示文本颜色
        End Get
        Set(value As Color)
            SetValue(提示文本颜色, value)
        End Set
    End Property

    Private 提示边框颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("工具提示边框颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property ToolTipBorderColor As Color
        Get
            Return 提示边框颜色
        End Get
        Set(value As Color)
            SetValue(提示边框颜色, value)
        End Set
    End Property

    Private 提示边框宽度 As Integer = 1
    <Category("LakeUI"), Description("工具提示边框宽度"), DefaultValue(GetType(Integer), "1"), Browsable(True)>
    Public Property ToolTipBorderSize As Integer
        Get
            Return 提示边框宽度
        End Get
        Set(value As Integer)
            SetValue(提示边框宽度, value)
        End Set
    End Property

    Private 提示圆角半径 As Integer = 0
    <Category("LakeUI"), Description("工具提示圆角半径"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property ToolTipBorderRadius As Integer
        Get
            Return 提示圆角半径
        End Get
        Set(value As Integer)
            SetValue(提示圆角半径, value)
        End Set
    End Property

    Private 提示内边距 As New Padding(10, 10, 10, 10)
    <Category("LakeUI"), Description("工具提示内边距"), DefaultValue(GetType(Padding), "10, 10, 10, 10"), Browsable(True)>
    Public Property ToolTipPadding As Padding
        Get
            Return 提示内边距
        End Get
        Set(value As Padding)
            提示内边距 = value
        End Set
    End Property

    Private 提示最大宽度 As Integer = 300
    <Category("LakeUI"), Description("工具提示最大宽度"), DefaultValue(GetType(Integer), "300"), Browsable(True)>
    Public Property ToolTipMaxWidth As Integer
        Get
            Return 提示最大宽度
        End Get
        Set(value As Integer)
            提示最大宽度 = Math.Max(50, value)
        End Set
    End Property

    Private 提示间距 As Integer = 0
    <Category("LakeUI"), Description("工具提示与下拉列表的水平间距"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property ToolTipGap As Integer
        Get
            Return 提示间距
        End Get
        Set(value As Integer)
            提示间距 = value
        End Set
    End Property
#End Region

#Region "初始化"
    Public Sub New()
        InitializeComponent()
        _items = New ItemCollection(Me)
        _itemToolTips = New ToolTipEntryCollection()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.Selectable Or
                 ControlStyles.StandardClick, True)
        UpdateStyles()
        AddHandler _caretBlinkTimer.Tick, Sub()
                                              _caretVisible = Not _caretVisible
                                              Invalidate()
                                          End Sub
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        If 启用编辑 Then
            ImeHelper.AssociateDefault(Handle)
        End If
        _caretBlinkTimer.Start()
    End Sub

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        _caretBlinkTimer.Stop()
        CloseDropDown()
        MyBase.OnHandleDestroyed(e)
    End Sub
#End Region

#Region "绘制"
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim w As Integer = ClientRectangle.Width
        Dim h As Integer = ClientRectangle.Height
        Dim hasRadius As Boolean = 边框圆角半径 > 0
        Dim boundsRect As New RectangleF(0, 0, w - 1, h - 1)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * DpiScale() / 2.0F
            boundsRect.Inflate(-half, -half)
        End If
        Dim bc As Color = If(Focused, 有焦点时边框颜色, 边框颜色)
        Dim effBg As Color = 背景颜色
        Dim effBg2 As Color = 背景渐变颜色

        If Not 启用编辑 Then
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
        End If
        Dim _ssaa As Integer = If(Class1.GlobalSSAA > 1, Class1.GlobalSSAA, 超采样倍率)
        If _ssaa > 1 Then
            Using bmp As New Bitmap(w * _ssaa, h * _ssaa)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.ScaleTransform(_ssaa, _ssaa)
                    DrawBackground(g, hasRadius, boundsRect, bc, effBg, effBg2)
                    DrawSeparatorAndArrow(g, w, h, bc)
                End Using
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                e.Graphics.DrawImage(bmp, 0, 0, w, h)
            End Using
        Else
            DrawBackground(e.Graphics, hasRadius, boundsRect, bc, effBg, effBg2)
            DrawSeparatorAndArrow(e.Graphics, w, h, bc)
        End If

        DrawTextContent(e.Graphics, w, h)

        If Not Enabled Then
            Using brush As New SolidBrush(Color.FromArgb(120, 0, 0, 0))
                e.Graphics.FillRectangle(brush, 0, 0, w, h)
            End Using
        End If
    End Sub

    Private Sub DrawBackground(g As Graphics, hasRadius As Boolean, boundsRect As RectangleF, borderClr As Color, bgClr As Color, bgClr2 As Color)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        g.InterpolationMode = InterpolationMode.HighQualityBicubic
        Dim s As Single = DpiScale()
        If hasRadius Then
            Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(boundsRect, 边框圆角半径 * s)
                RectangleRenderer.绘制圆角背景(g, path, boundsRect, bgClr, bgClr2, 渐变方向)
                RectangleRenderer.绘制圆角边框(g, path, borderClr, 边框宽度 * s)
            End Using
        Else
            RectangleRenderer.绘制矩形背景(g, boundsRect, bgClr, bgClr2, 渐变方向)
            RectangleRenderer.绘制矩形边框(g, boundsRect, borderClr, 边框宽度 * s)
        End If
    End Sub

    Private Sub DrawTextContent(g As Graphics, w As Integer, h As Integer)
        g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit
        Dim bi As Integer = CInt(边框宽度 * DpiScale())
        Dim textLeft As Integer = Math.Max(Padding.Left, bi)
        Dim textTop As Integer = Math.Max(Padding.Top, bi)
        Dim textRight As Integer = Math.Max(Padding.Right, bi)
        Dim textBottom As Integer = Math.Max(Padding.Bottom, bi)
        Dim textWidth As Integer = w - textLeft - textRight - ArrowAreaWidth
        Dim textHeight As Integer = h - textTop - textBottom
        g.SetClip(New Rectangle(textLeft, textTop, textWidth, textHeight))

        Dim singleLineY As Integer = textTop + (textHeight - 行高) \ 2
        Dim isEmpty As Boolean = String.IsNullOrEmpty(_text)

        If isEmpty AndAlso Not String.IsNullOrEmpty(水印文本) Then
            Dim waterAlignOff As Integer = GetAlignOffsetX(水印文本, textWidth)
            TextRenderer.DrawText(g, 水印文本, Font,
                New Point(textLeft + waterAlignOff, singleLineY + (行高 - FontHeight) \ 2),
                水印颜色, TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
        End If

        If Not isEmpty Then
            If _hasSelection Then
                DrawSelection(g, singleLineY, textLeft, textWidth)
            End If
            Dim alignOff As Integer = GetAlignOffsetX(_text, textWidth)
            Dim textY As Integer = singleLineY + (行高 - FontHeight) \ 2
            TextRenderer.DrawText(g, _text, Font,
                New Point(textLeft + alignOff - _scrollXOffset, textY),
                ForeColor, TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
        End If

        If Focused AndAlso _caretVisible AndAlso 启用编辑 Then
            DrawCaret(g, textLeft, textTop)
        End If

        g.ResetClip()
    End Sub

    Private Sub DrawSelection(g As Graphics, lineY As Integer, textLeft As Integer, textWidth As Integer)
        Dim minC As Integer = Math.Min(_selAnchorCol, _caretCol)
        Dim maxC As Integer = Math.Max(_selAnchorCol, _caretCol)
        Dim alignOff As Integer = GetAlignOffsetX(_text, textWidth)
        Dim x1 As Integer = textLeft + alignOff + MeasureWidth(_text.Substring(0, minC)) - _scrollXOffset
        Dim x2 As Integer = textLeft + alignOff + MeasureWidth(_text.Substring(0, maxC)) - _scrollXOffset
        If x2 <= x1 Then Return
        Using br As New SolidBrush(选区背景色)
            g.FillRectangle(br, x1, lineY, x2 - x1, 行高)
        End Using
    End Sub

    Private Sub DrawCaret(g As Graphics, textLeft As Integer, textTop As Integer)
        Dim bi As Integer = CInt(边框宽度 * DpiScale())
        Dim textHeight As Integer = ClientRectangle.Height - Math.Max(Padding.Top, bi) - Math.Max(Padding.Bottom, bi)
        Dim textWidth As Integer = ClientRectangle.Width - textLeft - Math.Max(Padding.Right, bi) - ArrowAreaWidth
        Dim alignOff As Integer = GetAlignOffsetX(_text, textWidth)
        Dim cx As Integer = textLeft + alignOff + MeasureWidth(_text.Substring(0, _caretCol)) - _scrollXOffset
        Dim lineY As Integer = textTop + (textHeight - 行高) \ 2
        Dim caretH As Integer = 行高 - 2
        Dim caretY As Integer = lineY + (行高 - caretH) \ 2
        Using br As New SolidBrush(光标颜色)
            g.FillRectangle(br, cx, caretY, CInt(光标线宽 * DpiScale()), caretH)
        End Using
    End Sub

    Private Sub DrawSeparatorAndArrow(g As Graphics, w As Integer, h As Integer, borderClr As Color)
        Dim s As Single = DpiScale()
        Dim aaw As Integer = ArrowAreaWidth
        Dim bi As Integer = CInt(边框宽度 * s)
        Dim sepX As Single = w - aaw - If(bi > 0, bi / 2.0F, 0)
        Dim topInset As Integer = Math.Max(Padding.Top, bi)
        Dim bottomInset As Integer = Math.Max(Padding.Bottom, bi)

        If 启用编辑 AndAlso _mouseOverArrow Then
            Dim arrowBgClr As Color = Color.Empty
            Select Case 鼠标状态
                Case MouseStateEnum.Hover
                    arrowBgClr = 鼠标移上时背景颜色
                Case MouseStateEnum.Pressed
                    arrowBgClr = 鼠标按下时背景颜色
            End Select
            If arrowBgClr <> Color.Empty Then
                Dim fillX As Single = sepX + If(bi > 0, bi / 2.0F, 0)
                Using br As New SolidBrush(arrowBgClr)
                    g.FillRectangle(br, fillX, topInset, w - fillX - bi, h - topInset - bottomInset)
                End Using
            End If
        End If

        If 显示分隔线 AndAlso 边框宽度 > 0 Then
            Using pen As New Pen(borderClr, 边框宽度 * s)
                g.DrawLine(pen, sepX, topInset, sepX, h - bottomInset)
            End Using
        End If

        Dim effArrowClr As Color = 箭头颜色
        Dim isArrowActive As Boolean = Not 启用编辑 OrElse _mouseOverArrow
        If isArrowActive Then
            Select Case 鼠标状态
                Case MouseStateEnum.Hover
                    If 鼠标移上时箭头颜色 <> Color.Empty Then effArrowClr = 鼠标移上时箭头颜色
                Case MouseStateEnum.Pressed
                    If 鼠标按下时箭头颜色 <> Color.Empty Then effArrowClr = 鼠标按下时箭头颜色
            End Select
        End If

        Dim squareLeft As Single = w - aaw
        Dim centerX As Single = squareLeft + aaw / 2.0F
        Dim centerY As Single = h / 2.0F
        Dim scaledArrow As Single = 箭头大小 * s
        Dim arrW As Single = scaledArrow
        Dim arrH As Single = scaledArrow * Math.Sqrt(3.0) / 2.0
        Dim verts() As PointF = {
            New PointF(centerX - arrW / 2.0F, centerY - arrH / 2.0F),
            New PointF(centerX + arrW / 2.0F, centerY - arrH / 2.0F),
            New PointF(centerX, centerY + arrH / 2.0F)
        }
        Dim cr As Single = Math.Max(scaledArrow * 0.2F, 1.0F)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using path As New GraphicsPath()
            For i As Integer = 0 To 2
                Dim curr As PointF = verts(i)
                Dim prv As PointF = verts((i + 2) Mod 3)
                Dim nxt As PointF = verts((i + 1) Mod 3)
                Dim d1x As Single = prv.X - curr.X, d1y As Single = prv.Y - curr.Y
                Dim d2x As Single = nxt.X - curr.X, d2y As Single = nxt.Y - curr.Y
                Dim l1 As Single = Math.Sqrt(d1x * d1x + d1y * d1y)
                Dim l2 As Single = Math.Sqrt(d2x * d2x + d2y * d2y)
                Dim a As New PointF(curr.X + cr * d1x / l1, curr.Y + cr * d1y / l1)
                Dim b As New PointF(curr.X + cr * d2x / l2, curr.Y + cr * d2y / l2)
                Dim cp1 As New PointF(a.X + 2.0F / 3.0F * (curr.X - a.X), a.Y + 2.0F / 3.0F * (curr.Y - a.Y))
                Dim cp2 As New PointF(b.X + 2.0F / 3.0F * (curr.X - b.X), b.Y + 2.0F / 3.0F * (curr.Y - b.Y))
                If i > 0 Then path.AddLine(path.GetLastPoint(), a)
                path.AddBezier(a, cp1, cp2, b)
            Next
            path.CloseFigure()
            Using br As New SolidBrush(effArrowClr)
                g.FillPath(br, path)
            End Using
        End Using
    End Sub
#End Region

#Region "消息处理 (WndProc)"
    Protected Overrides Sub WndProc(ByRef m As Message)
        Select Case m.Msg
            Case WM_GETDLGCODE
                m.Result = New IntPtr(DLGC_WANTCHARS Or DLGC_WANTALLKEYS)
                Return
            Case WM_IME_STARTCOMPOSITION
                If 启用编辑 Then
                    _imeComposing = True
                    UpdateImeWindow()
                End If
                MyBase.WndProc(m)
            Case WM_IME_ENDCOMPOSITION
                _imeComposing = False
                MyBase.WndProc(m)
            Case WM_IME_COMPOSITION
                If 启用编辑 Then
                    Dim lp As Integer = m.LParam.ToInt32()
                    UpdateImeWindow()
                    If (lp And GCS_RESULTSTR) <> 0 Then
                        Dim result As String = ImeHelper.GetResultString(Handle)
                        If result IsNot Nothing Then
                            InsertTextCore(result)
                        End If
                    Else
                        MyBase.WndProc(m)
                    End If
                Else
                    MyBase.WndProc(m)
                End If
            Case WM_CHAR
                If Not _imeComposing Then
                    HandleWmChar(m.WParam.ToInt32())
                End If
            Case Else
                MyBase.WndProc(m)
        End Select
    End Sub
#End Region

#Region "字符输入 (WM_CHAR)"
    Private Sub HandleWmChar(charCode As Integer)
        Select Case charCode
            Case 1  ' Ctrl+A
                SelectAll()
            Case 3  ' Ctrl+C
                CopySelection()
            Case 22 ' Ctrl+V
                If 启用编辑 Then PasteText()
            Case 24 ' Ctrl+X
                If 启用编辑 Then CutSelection()
            Case 8  ' Backspace
                If 启用编辑 Then HandleBackspace()
            Case Else
                If 启用编辑 Then
                    Dim ch As Char = ChrW(charCode)
                    If Not Char.IsControl(ch) Then
                        DeleteSelection()
                        InsertTextCore(ch.ToString())
                    End If
                End If
        End Select
        ResetCaretBlink()
    End Sub
#End Region

#Region "键盘导航 (OnKeyDown)"
    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If Not 启用编辑 Then
            Select Case e.KeyCode
                Case Keys.Down
                    If Not _droppedDown Then
                        OpenDropDown()
                    Else
                        If _selectedIndex < _items.Count - 1 Then
                            SelectedIndex = _selectedIndex + 1
                        End If
                    End If
                    e.Handled = True
                Case Keys.Up
                    If _droppedDown Then
                        If _selectedIndex > 0 Then
                            SelectedIndex = _selectedIndex - 1
                        End If
                    End If
                    e.Handled = True
                Case Keys.Enter, Keys.Space
                    If _droppedDown Then
                        CloseDropDown()
                    Else
                        OpenDropDown()
                    End If
                    e.Handled = True
                Case Keys.Escape
                    If _droppedDown Then CloseDropDown()
                    e.Handled = True
            End Select
            Return
        End If
        Dim shift As Boolean = e.Shift
        Dim ctrl As Boolean = e.Control
        Select Case e.KeyCode
            Case Keys.Left
                If ctrl Then MoveCaretWordLeft(shift) Else MoveCaret(-1, shift)
                e.Handled = True
            Case Keys.Right
                If ctrl Then MoveCaretWordRight(shift) Else MoveCaret(1, shift)
                e.Handled = True
            Case Keys.Home
                MoveCaretHome(shift)
                e.Handled = True
            Case Keys.End
                MoveCaretEnd(shift)
                e.Handled = True
            Case Keys.Delete
                HandleDelete()
                e.Handled = True
            Case Keys.Down
                If Not _droppedDown Then
                    OpenDropDown()
                Else
                    If _selectedIndex < _items.Count - 1 Then
                        SelectedIndex = _selectedIndex + 1
                    End If
                End If
                e.Handled = True
            Case Keys.Up
                If _droppedDown Then
                    If _selectedIndex > 0 Then
                        SelectedIndex = _selectedIndex - 1
                    End If
                End If
                e.Handled = True
            Case Keys.Escape
                If _droppedDown Then CloseDropDown()
                e.Handled = True
            Case Keys.Enter
                If _droppedDown Then CloseDropDown()
                e.Handled = True
        End Select
        If e.Handled Then ResetCaretBlink()
    End Sub

    Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
        Select Case keyData And Keys.KeyCode
            Case Keys.Left, Keys.Right, Keys.Up, Keys.Down,
                 Keys.Home, Keys.End, Keys.Delete, Keys.Enter,
                 Keys.Escape, Keys.Space
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
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        鼠标状态 = MouseStateEnum.Normal
        _mouseOverArrow = False
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not Enabled Then Return
        Focus()
        鼠标状态 = MouseStateEnum.Pressed
        If e.Button = MouseButtons.Left Then
            Dim aaw As Integer = ArrowAreaWidth
            Dim arrowRect As New Rectangle(ClientRectangle.Width - aaw, 0, aaw, ClientRectangle.Height)
            If arrowRect.Contains(e.Location) OrElse Not 启用编辑 Then
                If _droppedDown Then
                    CloseDropDown()
                Else
                    OpenDropDown()
                End If
                Invalidate()
                Return
            End If
            _mouseDownSelecting = True
            Dim col As Integer = HitTestCol(e.X)
            _caretCol = col
            _selAnchorCol = _caretCol
            _hasSelection = False
            ResetCaretBlink()
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim aaw As Integer = ArrowAreaWidth
        Dim arrowRect As New Rectangle(ClientRectangle.Width - aaw, 0, aaw, ClientRectangle.Height)
        Dim prevOverArrow As Boolean = _mouseOverArrow
        _mouseOverArrow = arrowRect.Contains(e.Location)
        If 启用编辑 Then
            Cursor = If(_mouseOverArrow, Cursors.Default, Cursors.IBeam)
        Else
            Cursor = Cursors.Default
        End If
        If _mouseDownSelecting AndAlso e.Button = MouseButtons.Left AndAlso 启用编辑 Then
            Dim col As Integer = HitTestCol(e.X)
            _caretCol = col
            _hasSelection = (_caretCol <> _selAnchorCol)
            EnsureCaretVisible()
            Invalidate()
        ElseIf _mouseOverArrow <> prevOverArrow Then
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        _mouseDownSelecting = False
        鼠标状态 = If(ClientRectangle.Contains(e.Location), MouseStateEnum.Hover, MouseStateEnum.Normal)
        Invalidate()
    End Sub

    Private Function HitTestCol(x As Integer) As Integer
        Dim bi As Integer = CInt(边框宽度 * DpiScale())
        Dim textLeft As Integer = Math.Max(Padding.Left, bi)
        Dim textWidth As Integer = ClientRectangle.Width - textLeft - Math.Max(Padding.Right, bi) - ArrowAreaWidth
        Dim alignOff As Integer = GetAlignOffsetX(_text, textWidth)
        Return FindColFromX(_text, x - textLeft - alignOff + _scrollXOffset)
    End Function

    Private Function FindColFromX(lineStr As String, x As Integer) As Integer
        Return TextRenderHelper.FindColFromX(lineStr, x, Font, 行高)
    End Function
#End Region

#Region "光标移动"
    Private Sub MoveCaret(deltaCol As Integer, extend As Boolean)
        If Not extend AndAlso _hasSelection AndAlso deltaCol <> 0 Then
            Dim minC As Integer = Math.Min(_selAnchorCol, _caretCol)
            Dim maxC As Integer = Math.Max(_selAnchorCol, _caretCol)
            _caretCol = If(deltaCol < 0, minC, maxC)
            ClearSelection()
            EnsureCaretVisible()
            Invalidate()
            Return
        End If
        If Not extend Then _selAnchorCol = _caretCol
        _caretCol += deltaCol
        _caretCol = Math.Max(0, Math.Min(_text.Length, _caretCol))
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        Invalidate()
    End Sub

    Private Sub MoveCaretHome(extend As Boolean)
        If Not extend Then _selAnchorCol = _caretCol
        _caretCol = 0
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        Invalidate()
    End Sub

    Private Sub MoveCaretEnd(extend As Boolean)
        If Not extend Then _selAnchorCol = _caretCol
        _caretCol = _text.Length
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        Invalidate()
    End Sub

    Private Sub MoveCaretWordLeft(extend As Boolean)
        If Not extend Then _selAnchorCol = _caretCol
        If _caretCol > 0 Then
            Dim c As Integer = _caretCol - 1
            While c > 0 AndAlso Char.IsWhiteSpace(_text(c - 1))
                c -= 1
            End While
            While c > 0 AndAlso Not Char.IsWhiteSpace(_text(c - 1))
                c -= 1
            End While
            _caretCol = c
        End If
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        Invalidate()
    End Sub

    Private Sub MoveCaretWordRight(extend As Boolean)
        If Not extend Then _selAnchorCol = _caretCol
        If _caretCol < _text.Length Then
            Dim c As Integer = _caretCol
            While c < _text.Length AndAlso Not Char.IsWhiteSpace(_text(c))
                c += 1
            End While
            While c < _text.Length AndAlso Char.IsWhiteSpace(_text(c))
                c += 1
            End While
            _caretCol = c
        End If
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        Invalidate()
    End Sub

    Private Sub UpdateSelectionFromAnchor(extend As Boolean)
        If extend Then
            _hasSelection = (_caretCol <> _selAnchorCol)
        Else
            ClearSelection()
        End If
    End Sub

    Private Sub EnsureCaretVisible()
        Dim bi As Integer = CInt(边框宽度 * DpiScale())
        Dim textLeft As Integer = Math.Max(Padding.Left, bi)
        Dim areaW As Integer = ClientRectangle.Width - textLeft - Math.Max(Padding.Right, bi) - ArrowAreaWidth
        If areaW <= 0 Then Return
        Dim caretX As Integer = MeasureWidth(_text.Substring(0, _caretCol))
        If 文本对齐 <> TextAlignMode.Left Then
            Dim lineW As Integer = MeasureWidth(_text)
            If lineW < areaW Then
                _scrollXOffset = 0
                Return
            End If
        End If
        Dim margin As Integer = CInt(光标线宽 * DpiScale()) + 2
        If caretX - _scrollXOffset < 0 Then
            _scrollXOffset = Math.Max(0, caretX - margin)
        ElseIf caretX - _scrollXOffset >= areaW - margin Then
            _scrollXOffset = caretX - areaW + margin
        End If
    End Sub
#End Region

#Region "文本编辑核心"
    Private Sub InsertTextCore(text As String)
        DeleteSelection()
        Dim clean As String = text.Replace(vbCr, "").Replace(vbLf, "")
        _text = String.Concat(_text.AsSpan(0, _caretCol), clean, _text.AsSpan(_caretCol))
        _caretCol += clean.Length
        NotifyTextChanged()
    End Sub

    Private Sub HandleBackspace()
        If _hasSelection Then
            DeleteSelection()
        ElseIf _caretCol > 0 Then
            _text = String.Concat(_text.AsSpan(0, _caretCol - 1), _text.AsSpan(_caretCol))
            _caretCol -= 1
        End If
        NotifyTextChanged()
    End Sub

    Private Sub HandleDelete()
        If _hasSelection Then
            DeleteSelection()
        ElseIf _caretCol < _text.Length Then
            _text = String.Concat(_text.AsSpan(0, _caretCol), _text.AsSpan(_caretCol + 1))
        End If
        NotifyTextChanged()
    End Sub

    Private Sub DeleteSelection()
        If Not _hasSelection Then Return
        Dim minC As Integer = Math.Min(_selAnchorCol, _caretCol)
        Dim maxC As Integer = Math.Max(_selAnchorCol, _caretCol)
        _text = String.Concat(_text.AsSpan(0, minC), _text.AsSpan(maxC))
        _caretCol = minC
        ClearSelection()
    End Sub
#End Region

#Region "选区"
    Public Sub SelectAll()
        _selAnchorCol = 0
        _caretCol = _text.Length
        _hasSelection = _text.Length > 0
        Invalidate()
    End Sub

    Private Sub ClearSelection()
        _hasSelection = False
        _selAnchorCol = _caretCol
    End Sub

    Private Function GetSelectedText() As String
        If Not _hasSelection Then Return ""
        Dim minC As Integer = Math.Min(_selAnchorCol, _caretCol)
        Dim maxC As Integer = Math.Max(_selAnchorCol, _caretCol)
        Return _text.Substring(minC, maxC - minC)
    End Function
#End Region

#Region "剪贴板"
    Private Sub CopySelection()
        If _hasSelection Then
            Try
                Clipboard.SetText(GetSelectedText())
            Catch
            End Try
        End If
    End Sub

    Private Sub CutSelection()
        If _hasSelection AndAlso 启用编辑 Then
            CopySelection()
            DeleteSelection()
            NotifyTextChanged()
        End If
    End Sub

    Private Sub PasteText()
        If Not 启用编辑 Then Return
        Try
            If Clipboard.ContainsText() Then
                InsertTextCore(Clipboard.GetText())
            End If
        Catch
        End Try
    End Sub
#End Region

#Region "查找"
    Public Function FindString(s As String) As Integer
        Return FindString(s, -1)
    End Function

    Public Function FindString(s As String, startIndex As Integer) As Integer
        If String.IsNullOrEmpty(s) Then Return -1
        For i As Integer = startIndex + 1 To _items.Count - 1
            If _items(i).StartsWith(s, StringComparison.CurrentCultureIgnoreCase) Then
                Return i
            End If
        Next
        Return -1
    End Function

    Public Function FindStringExact(s As String) As Integer
        Return FindStringExact(s, -1)
    End Function

    Public Function FindStringExact(s As String, startIndex As Integer) As Integer
        If s Is Nothing Then Return -1
        For i As Integer = startIndex + 1 To _items.Count - 1
            If String.Equals(_items(i), s, StringComparison.CurrentCultureIgnoreCase) Then
                Return i
            End If
        Next
        Return -1
    End Function
#End Region

#Region "下拉列表"
    Private Sub OpenDropDown()
        If _items.Count = 0 Then Return
        If _droppedDown Then Return

        _droppedDown = True
        _dropDownForm = New DropDownListForm(Me)
        _dropDownForm.ShowDropDown()
        RaiseEvent DropDownOpened(Me, EventArgs.Empty)
    End Sub

    Friend Sub CloseDropDown()
        If Not _droppedDown Then Return
        If _dropDownForm IsNot Nothing Then
            If _dropDownForm.正在关闭动画 Then Return
            If 下拉展开关闭动画时长 > 0 AndAlso _dropDownForm.IsHandleCreated AndAlso Not _dropDownForm.IsDisposed Then
                _dropDownForm.开始关闭动画()
                Return
            End If
            _droppedDown = False
            _dropDownForm.关闭并释放()
            _dropDownForm = Nothing
        Else
            _droppedDown = False
        End If
        Invalidate()
    End Sub

    Friend Sub OnDropDownClosed()
        _droppedDown = False
        _dropDownForm = Nothing
        Invalidate()
        RaiseEvent DropDownClosed(Me, EventArgs.Empty)
    End Sub

    Friend Sub OnDropDownItemClicked(index As Integer)
        If index >= 0 AndAlso index < _items.Count Then
            SelectedIndex = index
        End If
        CloseDropDown()
    End Sub

    Private Class DropDownListForm
        Inherits PopupForm
        Implements IMessageFilter

        Private _owner As ModernComboBox
        Private _hoverIndex As Integer = -1
        Private _pressedIndex As Integer = -1
        Private _scrollOffset As Integer = 0
        Private _scrollBarVisible As Boolean = False
        Private _scrollBar As New ScrollBarRenderer()
#Disable Warning IDE0044
        Private _finalHeight As Integer
        Private _originPt As Point
        Private _useIdle As Boolean = False

        ' 展开/关闭动画
        Private ReadOnly 展开关闭秒表 As New Stopwatch()
        Private 展开关闭计时器 As Timer
        Private 展开关闭动画中 As Boolean = False
        Friend 正在关闭动画 As Boolean = False

        ' 悬停动画
        Private ReadOnly 悬停秒表 As New Stopwatch()
        Private 悬停计时器 As Timer
#Enable Warning IDE0044
        Private 悬停动画起始Y As Single = -1
        Private 悬停动画目标Y As Single = -1
        Private 悬停动画当前Y As Single = -1
        Private 悬停动画起始高度 As Single = 0
        Private 悬停动画目标高度 As Single = 0
        Private 悬停动画当前高度 As Single = 0
        Private 悬停动画中 As Boolean = False
        Private 悬停动画显示 As Boolean = False

        Private Const WM_LBUTTONDOWN As Integer = &H201
        Private Const WM_RBUTTONDOWN As Integer = &H204
        Private Const WM_MBUTTONDOWN As Integer = &H207
        Private Const WM_NCLBUTTONDOWN As Integer = &HA1

        Public Sub New(owner As ModernComboBox)
            _owner = owner
            Me.DoubleBuffered = True
            Me.BackColor = owner.BackColor
            Me.AutoScaleMode = AutoScaleMode.Dpi

            _useIdle = (owner.下拉动画帧率 <= 0)
            If Not _useIdle Then
                Dim interval As Integer = Math.Max(1, 1000 \ owner.下拉动画帧率)
                展开关闭计时器 = New Timer() With {.Interval = interval}
                悬停计时器 = New Timer() With {.Interval = interval}
            End If

            Dim visCount As Integer = Math.Min(owner._items.Count, owner.最大下拉项数)
            Dim s As Single = owner.DpiScale()
            Dim bw As Integer = CInt(owner.下拉边框宽度 * s)
            Dim itemH As Integer = CInt(owner.下拉项高度 * s)
            Dim pad As Padding = owner.下拉内边距
            _finalHeight = visCount * itemH + bw * 2 + pad.Top + pad.Bottom
            _originPt = owner.PointToScreen(New Point(0, owner.Height + owner.下拉间距))
            Dim scr As Screen = Screen.FromControl(owner)
            If _originPt.Y + _finalHeight > scr.WorkingArea.Bottom Then
                _originPt = owner.PointToScreen(New Point(0, -_finalHeight - owner.下拉间距))
            End If
            Me.Location = _originPt
            Me.Size = New Size(owner.Width, _finalHeight)

            _scrollBarVisible = owner._items.Count > owner.最大下拉项数
            If owner._selectedIndex >= 0 Then
                Dim maxOff As Integer = Math.Max(0, owner._items.Count - visCount)
                _scrollOffset = Math.Max(0, Math.Min(maxOff, owner._selectedIndex - visCount \ 2))
            End If
        End Sub

        Friend Sub ShowDropDown()
            Application.AddMessageFilter(Me)
            If _owner.下拉展开关闭动画时长 > 0 Then
                Me.Size = New Size(Me.Width, 1)
                Me.Show()
                展开关闭动画中 = True
                正在关闭动画 = False
                展开关闭秒表.Restart()
                启动展开关闭驱动()
            Else
                Me.Show()
            End If
        End Sub

        Friend Sub 开始关闭动画()
            If 正在关闭动画 Then Return
            正在关闭动画 = True
            展开关闭动画中 = True
            展开关闭秒表.Restart()
            启动展开关闭驱动()
        End Sub

        Private Sub 启动展开关闭驱动()
            If _useIdle Then
                AddHandler Application.Idle, AddressOf 展开关闭帧更新
            Else
                AddHandler 展开关闭计时器.Tick, AddressOf 展开关闭帧更新
                展开关闭计时器.Start()
            End If
        End Sub

        Private Sub 停止展开关闭驱动()
            If _useIdle Then
                RemoveHandler Application.Idle, AddressOf 展开关闭帧更新
            Else
                展开关闭计时器.Stop()
                RemoveHandler 展开关闭计时器.Tick, AddressOf 展开关闭帧更新
            End If
        End Sub

        Friend Sub 关闭并释放()
            关闭工具提示()
            停止悬停动画()
            停止展开关闭驱动()
            If 展开关闭计时器 IsNot Nothing Then 展开关闭计时器.Dispose()
            If 悬停计时器 IsNot Nothing Then 悬停计时器.Dispose()
            Application.RemoveMessageFilter(Me)
            If Not IsDisposed Then Close()
        End Sub

        Private Sub 展开关闭帧更新(sender As Object, e As EventArgs)
            Dim duration As Integer = _owner.下拉展开关闭动画时长
            If duration <= 0 Then
                停止展开关闭驱动()
                展开关闭动画中 = False
                If 正在关闭动画 Then
                    完成关闭()
                Else
                    Me.Size = New Size(Me.Width, _finalHeight)
                End If
                Return
            End If

            Dim elapsed As Double = 展开关闭秒表.Elapsed.TotalMilliseconds
            Dim t As Single = CSng(Math.Min(elapsed / duration, 1.0))
            Dim eased As Single = 1.0F - CSng(Math.Pow(1.0 - t, 3))

            If 正在关闭动画 Then
                Dim newH As Integer = Math.Max(1, CInt(_finalHeight * (1.0F - eased)))
                Me.Size = New Size(Me.Width, newH)
            Else
                Dim newH As Integer = Math.Max(1, CInt(_finalHeight * eased))
                Me.Size = New Size(Me.Width, newH)
            End If
            Invalidate()

            If t >= 1.0F Then
                停止展开关闭驱动()
                展开关闭动画中 = False
                If 正在关闭动画 Then
                    完成关闭()
                Else
                    Me.Size = New Size(Me.Width, _finalHeight)
                    Invalidate()
                End If
            End If
        End Sub

        Private Sub 完成关闭()
            正在关闭动画 = False
            展开关闭动画中 = False
            关闭并释放()
            _owner.OnDropDownClosed()
        End Sub

        Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
            Select Case m.Msg
                Case WM_LBUTTONDOWN, WM_RBUTTONDOWN, WM_MBUTTONDOWN, WM_NCLBUTTONDOWN
                    Dim screenPos As Point = Control.MousePosition
                    If Not Bounds.Contains(screenPos) Then
                        Dim ownerScreen As Rectangle = _owner.RectangleToScreen(_owner.ClientRectangle)
                        If Not ownerScreen.Contains(screenPos) Then
                            BeginInvoke(Sub() _owner.CloseDropDown())
                        End If
                    End If
            End Select
            Return False
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim w As Integer = ClientRectangle.Width
            Dim h As Integer = ClientRectangle.Height
            Dim s As Single = _owner.DpiScale()
            Dim bw As Integer = CInt(_owner.下拉边框宽度 * s)
            Dim radius As Integer = CInt(_owner.下拉圆角半径 * s)
            Dim boundsRect As New RectangleF(0, 0, w - 1, h - 1)
            If bw > 0 Then
                Dim half As Single = bw / 2.0F
                boundsRect.Inflate(-half, -half)
            End If

            Dim scale As Integer = _owner.超采样倍率
            If scale > 1 Then
                Using bmp As New Bitmap(w * scale, h * scale)
                    Using g As Graphics = Graphics.FromImage(bmp)
                        g.ScaleTransform(scale, scale)
                        DrawDropDownBackground(g, boundsRect, radius, bw)
                    End Using
                    e.Graphics.CompositingQuality = CompositingQuality.HighQuality
                    e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                    e.Graphics.DrawImage(bmp, 0, 0, w, h)
                End Using
            Else
                DrawDropDownBackground(e.Graphics, boundsRect, radius, bw)
            End If

            DrawDropDownItems(e.Graphics, w, h)

            If _scrollBarVisible Then
                _scrollBar.ComputeLayout(w, h, bw, radius,
                    _owner.下拉内边距.Top, _owner.下拉内边距.Bottom,
                    _owner.下拉滚动条宽度,
                    _owner._items.Count, Math.Min(_owner._items.Count, _owner.最大下拉项数), _scrollOffset)
                _scrollBar.Draw(e.Graphics, w, h, bw, radius,
                    _owner.下拉滚动条宽度,
                    _owner.下拉滚动条轨道颜色, _owner.下拉滚动条颜色, _owner.下拉滚动条悬停颜色)
            End If
        End Sub

        Private Sub DrawDropDownBackground(g As Graphics, boundsRect As RectangleF, radius As Integer, bw As Integer)
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.PixelOffsetMode = PixelOffsetMode.HighQuality
            g.InterpolationMode = InterpolationMode.HighQualityBicubic
            If radius > 0 Then
                Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(boundsRect, radius)
                    Using br As New SolidBrush(_owner.下拉背景颜色)
                        g.FillPath(br, path)
                    End Using
                    If bw > 0 Then
                        Using pen As New Pen(_owner.下拉边框颜色, bw)
                            pen.LineJoin = LineJoin.Round
                            g.DrawPath(pen, path)
                        End Using
                    End If
                End Using
            Else
                Using br As New SolidBrush(_owner.下拉背景颜色)
                    g.FillRectangle(br, boundsRect.X, boundsRect.Y, boundsRect.Width, boundsRect.Height)
                End Using
                If bw > 0 Then
                    Using pen As New Pen(_owner.下拉边框颜色, bw)
                        g.DrawRectangle(pen, boundsRect.X, boundsRect.Y, boundsRect.Width, boundsRect.Height)
                    End Using
                End If
            End If
        End Sub

        Private Sub DrawDropDownItems(g As Graphics, w As Integer, h As Integer)
            g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit
            Dim s As Single = _owner.DpiScale()
            Dim bw As Integer = CInt(_owner.下拉边框宽度 * s)
            Dim radius As Integer = CInt(_owner.下拉圆角半径 * s)
            Dim pad As Padding = _owner.下拉内边距
            Dim inset As Integer = Math.Max(bw, 1)
            Dim rightCorr As Integer = If(bw >= 2, 1, 0)
            Dim scrollW As Integer = If(_scrollBarVisible, _scrollBar.GetReservedWidth(w, inset), 0)
            Dim itemH As Integer = CInt(_owner.下拉项高度 * s)
            Dim visCount As Integer = Math.Min(_owner._items.Count, _owner.最大下拉项数)

            If radius > 0 Then
                Using clipPath As GraphicsPath = RectangleRenderer.创建圆角矩形路径(
                    New RectangleF(inset, inset, w - inset * 2 - rightCorr - scrollW, h - inset * 2),
                    Math.Max(0, radius - inset))
                    g.SetClip(clipPath)
                End Using
            Else
                g.SetClip(New Rectangle(inset, inset, w - inset * 2 - rightCorr - scrollW, h - inset * 2))
            End If

            ' 绘制悬停高亮
            Dim hlL As Integer = inset - _owner.下拉高亮左侧偏移
            Dim hlR As Integer = inset + rightCorr - _owner.下拉高亮右侧偏移
            If 悬停动画显示 Then
                Dim highlightRect As New RectangleF(
                    hlL, 悬停动画当前Y, w - hlL - hlR - scrollW, 悬停动画当前高度)
                Using br As New SolidBrush(_owner.下拉悬停颜色)
                    g.FillRectangle(br, highlightRect.X, highlightRect.Y, highlightRect.Width, highlightRect.Height)
                End Using
            End If

            For i As Integer = 0 To visCount - 1
                Dim idx As Integer = i + _scrollOffset
                If idx >= _owner._items.Count Then Exit For
                Dim itemY As Integer = bw + pad.Top + i * itemH
                Dim itemRect As New Rectangle(hlL, itemY, w - hlL - hlR - scrollW, itemH)

                If idx = _owner._selectedIndex Then
                    Using br As New SolidBrush(_owner.下拉选中颜色)
                        g.FillRectangle(br, itemRect)
                    End Using
                ElseIf idx = _hoverIndex AndAlso Not 悬停动画显示 Then
                    Using br As New SolidBrush(_owner.下拉悬停颜色)
                        g.FillRectangle(br, itemRect)
                    End Using
                End If

                Dim textRect As New Rectangle(itemRect.X + pad.Left + 4, itemRect.Y, itemRect.Width - pad.Left - pad.Right - 8, itemRect.Height)
                TextRenderer.DrawText(g, _owner._items(idx), _owner.Font, textRect, _owner.ForeColor,
                    TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding)
            Next
            g.ResetClip()
        End Sub

        Private Function GetItemIndexAtY(y As Integer) As Integer
            Dim s As Single = _owner.DpiScale()
            Dim bw As Integer = CInt(_owner.下拉边框宽度 * s)
            Dim itemH As Integer = CInt(_owner.下拉项高度 * s)
            Dim idx As Integer = (y - bw - _owner.下拉内边距.Top) \ itemH + _scrollOffset
            If idx < 0 OrElse idx >= _owner._items.Count Then Return -1
            Return idx
        End Function

        Private Function GetItemRect(index As Integer) As RectangleF
            Dim s As Single = _owner.DpiScale()
            Dim bw As Integer = CInt(_owner.下拉边框宽度 * s)
            Dim pad As Padding = _owner.下拉内边距
            Dim itemH As Integer = CInt(_owner.下拉项高度 * s)
            Dim visIdx As Integer = index - _scrollOffset
            Dim itemY As Single = bw + pad.Top + visIdx * itemH
            Return New RectangleF(0, itemY, ClientRectangle.Width, itemH)
        End Function

        Private Sub 更新悬停动画()
            If _hoverIndex >= 0 AndAlso _hoverIndex < _owner._items.Count Then
                Dim rect = GetItemRect(_hoverIndex)
                Dim targetY As Single = rect.Y
                Dim targetH As Single = rect.Height

                If _owner.下拉悬停动画时长 <= 0 OrElse Not 悬停动画显示 Then
                    悬停动画起始Y = targetY
                    悬停动画目标Y = targetY
                    悬停动画当前Y = targetY
                    悬停动画起始高度 = targetH
                    悬停动画目标高度 = targetH
                    悬停动画当前高度 = targetH
                    悬停动画显示 = True
                    停止悬停动画()
                    Return
                End If

                悬停动画起始Y = 悬停动画当前Y
                悬停动画目标Y = targetY
                悬停动画起始高度 = 悬停动画当前高度
                悬停动画目标高度 = targetH
                悬停动画显示 = True
                悬停秒表.Restart()
                If Not 悬停动画中 Then
                    悬停动画中 = True
                    If _useIdle Then
                        AddHandler Application.Idle, AddressOf 悬停帧更新
                    Else
                        AddHandler 悬停计时器.Tick, AddressOf 悬停帧更新
                        悬停计时器.Start()
                    End If
                End If
            Else
                悬停动画显示 = False
                停止悬停动画()
            End If
        End Sub

        Private Sub 悬停帧更新(sender As Object, e As EventArgs)
            Dim duration = _owner.下拉悬停动画时长
            If duration <= 0 Then
                悬停动画当前Y = 悬停动画目标Y
                悬停动画当前高度 = 悬停动画目标高度
                停止悬停动画()
                Invalidate()
                Return
            End If

            Dim elapsed As Double = 悬停秒表.Elapsed.TotalMilliseconds
            Dim t As Single = CSng(Math.Min(elapsed / duration, 1.0))
            Dim eased As Single = 1.0F - CSng(Math.Pow(1.0 - t, 3))
            悬停动画当前Y = 悬停动画起始Y + (悬停动画目标Y - 悬停动画起始Y) * eased
            悬停动画当前高度 = 悬停动画起始高度 + (悬停动画目标高度 - 悬停动画起始高度) * eased

            If t >= 1.0F Then
                悬停动画当前Y = 悬停动画目标Y
                悬停动画当前高度 = 悬停动画目标高度
                停止悬停动画()
            End If
            Invalidate()
        End Sub

        Private Sub 停止悬停动画()
            If 悬停动画中 Then
                悬停动画中 = False
                If _useIdle Then
                    RemoveHandler Application.Idle, AddressOf 悬停帧更新
                Else
                    悬停计时器.Stop()
                    RemoveHandler 悬停计时器.Tick, AddressOf 悬停帧更新
                End If
                悬停秒表.Stop()
            End If
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            If _scrollBar.IsDragging Then
                Dim total As Integer = _owner._items.Count
                Dim vis As Integer = Math.Min(total, _owner.最大下拉项数)
                _scrollOffset = _scrollBar.DragMove(e.Y, total, vis)
                Invalidate()
                Return
            End If
            If _scrollBarVisible Then
                If _scrollBar.UpdateHover(e.Location) Then Invalidate()
            End If
            Dim idx As Integer = GetItemIndexAtY(e.Y)
            If idx <> _hoverIndex Then
                _hoverIndex = idx
                更新悬停动画()
                更新工具提示()
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If _scrollBarVisible Then
                If _scrollBar.BeginDrag(e.Location, _scrollOffset) Then Return
                Dim total As Integer = _owner._items.Count
                Dim vis As Integer = Math.Min(total, _owner.最大下拉项数)
                Dim newOff As Integer = _scrollBar.TrackClick(e.Location, _scrollOffset, total, vis)
                If newOff <> _scrollOffset Then
                    _scrollOffset = newOff
                    Invalidate()
                    Return
                End If
            End If
            _pressedIndex = GetItemIndexAtY(e.Y)
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _scrollBar.EndDrag()
            Dim idx As Integer = GetItemIndexAtY(e.Y)
            If _pressedIndex >= 0 AndAlso idx = _pressedIndex Then
                _owner.OnDropDownItemClicked(idx)
            End If
            _pressedIndex = -1
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            If _hoverIndex <> -1 Then
                _hoverIndex = -1
                更新悬停动画()
                关闭工具提示()
                Invalidate()
            End If
            If _scrollBar.ResetHover() Then Invalidate()
        End Sub

        Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
            MyBase.OnMouseWheel(e)
            If Not _scrollBarVisible Then Return
            Dim total As Integer = _owner._items.Count
            Dim vis As Integer = Math.Min(total, _owner.最大下拉项数)
            _scrollOffset = ScrollBarRenderer.HandleWheel(e.Delta, _scrollOffset, total, vis)
            关闭工具提示()
            Invalidate()
        End Sub

        Private _tipForm As ToolTipForm = Nothing

        Private Sub 更新工具提示()
            If _hoverIndex >= 0 AndAlso _hoverIndex < _owner._items.Count Then
                Dim itemText As String = _owner._items(_hoverIndex)
                Dim tipText As String = Nothing
                If _owner._itemToolTips.TryGetToolTip(itemText, tipText) AndAlso Not String.IsNullOrEmpty(tipText) Then
                    If _tipForm Is Nothing OrElse _tipForm.IsDisposed Then
                        _tipForm = New ToolTipForm(_owner)
                    End If
                    Dim itemRect = GetItemRect(_hoverIndex)
                    Dim screenPt As Point = Me.PointToScreen(New Point(Me.Width + _owner.提示间距, CInt(itemRect.Y)))
                    _tipForm.ShowTip(tipText, screenPt)
                    Return
                End If
            End If
            关闭工具提示()
        End Sub

        Private Sub 关闭工具提示()
            If _tipForm IsNot Nothing AndAlso Not _tipForm.IsDisposed Then
                _tipForm.Close()
                _tipForm.Dispose()
            End If
            _tipForm = Nothing
        End Sub
    End Class

    Private Class ToolTipForm
        Inherits PopupForm

        Private ReadOnly _owner As ModernComboBox
        Private _tipText As String = ""

        Public Sub New(owner As ModernComboBox)
            _owner = owner
            Me.DoubleBuffered = True
            Me.AutoScaleMode = AutoScaleMode.Dpi
            Me.BackColor = owner.提示背景颜色
        End Sub

        Public Sub ShowTip(text As String, screenLocation As Point)
            _tipText = text
            Dim pad As Padding = _owner.提示内边距
            Dim bw As Integer = _owner.提示边框宽度
            Dim maxW As Integer = _owner.提示最大宽度
            Dim contentW As Integer = maxW - pad.Left - pad.Right - bw * 2
            If contentW < 10 Then contentW = 10

            Dim measured As Size = TextRenderer.MeasureText(_tipText, _owner.Font,
                New Size(contentW, Integer.MaxValue),
                TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding)

            Dim w As Integer = Math.Min(maxW, measured.Width + pad.Left + pad.Right + bw * 2)
            Dim h As Integer = measured.Height + pad.Top + pad.Bottom + bw * 2

            Me.Size = New Size(w, h)

            Dim scr As Screen = Screen.FromPoint(screenLocation)
            Dim loc As Point = screenLocation
            If loc.X + w > scr.WorkingArea.Right Then
                loc.X = screenLocation.X - Me.Width - _owner.Width - _owner.提示间距 * 2
                If loc.X < scr.WorkingArea.Left Then loc.X = scr.WorkingArea.Left
            End If
            If loc.Y + h > scr.WorkingArea.Bottom Then
                loc.Y = scr.WorkingArea.Bottom - h
            End If
            Me.Location = loc

            If Not Visible Then Me.Show()
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim w As Integer = ClientRectangle.Width
            Dim h As Integer = ClientRectangle.Height
            Dim bw As Integer = _owner.提示边框宽度
            Dim radius As Integer = _owner.提示圆角半径
            Dim pad As Padding = _owner.提示内边距
            ' 先用背景色填满整个客户区，防止底层 Form 默认色透出
            Using bgBr As New SolidBrush(_owner.提示背景颜色)
                e.Graphics.FillRectangle(bgBr, 0, 0, w, h)
            End Using

            Dim boundsRect As New RectangleF(0, 0, w - 1, h - 1)
            If bw > 0 Then
                Dim half As Single = bw / 2.0F
                boundsRect.Inflate(-half, -half)
            End If

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality
            If radius > 0 Then
                Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(boundsRect, radius)
                    Using br As New SolidBrush(_owner.提示背景颜色)
                        e.Graphics.FillPath(br, path)
                    End Using
                    If bw > 0 Then
                        Using pen As New Pen(_owner.提示边框颜色, bw)
                            pen.LineJoin = LineJoin.Round
                            e.Graphics.DrawPath(pen, path)
                        End Using
                    End If
                End Using
            Else
                If bw > 0 Then
                    Using pen As New Pen(_owner.提示边框颜色, bw)
                        e.Graphics.DrawRectangle(pen, boundsRect.X, boundsRect.Y, boundsRect.Width, boundsRect.Height)
                    End Using
                End If
            End If

            e.Graphics.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit
            Dim textRect As New Rectangle(bw + pad.Left, bw + pad.Top,
                w - bw * 2 - pad.Left - pad.Right,
                h - bw * 2 - pad.Top - pad.Bottom)
            TextRenderer.DrawText(e.Graphics, _tipText, _owner.Font, textRect, _owner.提示文本颜色,
                TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding Or TextFormatFlags.Left Or TextFormatFlags.Top)
        End Sub
    End Class
#End Region

#Region "输入法 IME"
    Private Sub UpdateImeWindow()
        If Not IsHandleCreated OrElse Not 启用编辑 Then Return
        Dim bi As Integer = CInt(边框宽度 * DpiScale())
        Dim imeLeft As Integer = Math.Max(Padding.Left, bi)
        Dim imeTop As Integer = Math.Max(Padding.Top, bi)
        Dim textWidth As Integer = ClientRectangle.Width - imeLeft - Math.Max(Padding.Right, bi) - ArrowAreaWidth
        Dim alignOff As Integer = GetAlignOffsetX(_text, textWidth)
        Dim cx As Integer = imeLeft + alignOff + MeasureWidth(_text.Substring(0, _caretCol)) - _scrollXOffset
        Dim textHeight As Integer = ClientRectangle.Height - imeTop - Math.Max(Padding.Bottom, bi)
        Dim cy As Integer = imeTop + (textHeight - 行高) \ 2 + 行高
        ImeHelper.SetCompositionPosition(Handle, cx, cy)
    End Sub
#End Region

#Region "辅助"
    Private ReadOnly Property ArrowAreaWidth As Integer
        Get
            Return Me.Height
        End Get
    End Property

    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function

    Private Function MeasureWidth(text As String) As Integer
        Return TextRenderHelper.MeasureTextWidth(text, Font, 行高)
    End Function

    Private Function GetAlignOffsetX(lineStr As String, areaWidth As Integer) As Integer
        If 文本对齐 = TextAlignMode.Left Then Return 0
        Dim textW As Integer = MeasureWidth(lineStr)
        If textW >= areaWidth Then Return 0
        Select Case 文本对齐
            Case TextAlignMode.Center
                Return (areaWidth - textW) \ 2
            Case TextAlignMode.Right
                Return areaWidth - textW
            Case Else
                Return 0
        End Select
    End Function

    Private Sub NotifyTextChanged()
        EnsureCaretVisible()
        Invalidate()
        RaiseEvent TextChanged(Me, EventArgs.Empty)
    End Sub

    Friend Sub OnItemsTextChanged()
        Invalidate()
        RaiseEvent TextChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub ResetCaretBlink()
        _caretVisible = True
        _caretBlinkTimer.Stop()
        _caretBlinkTimer.Start()
        Invalidate()
    End Sub

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            Invalidate()
        End If
    End Sub
#End Region

#Region "事件"
    Protected Overrides Sub OnGotFocus(e As EventArgs)
        MyBase.OnGotFocus(e)
        If IsHandleCreated AndAlso 启用编辑 Then
            ImeHelper.AssociateDefault(Handle)
            UpdateImeWindow()
        End If
        _caretVisible = True
        _caretBlinkTimer.Start()
        Invalidate()
    End Sub

    Protected Overrides Sub OnLostFocus(e As EventArgs)
        MyBase.OnLostFocus(e)
        _caretBlinkTimer.Stop()
        _caretVisible = False
        Invalidate()
    End Sub

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        EnsureCaretVisible()
        Invalidate()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        If _droppedDown Then Return
        If _items.Count = 0 Then Return
        If e.Delta > 0 Then
            If _selectedIndex > 0 Then SelectedIndex = _selectedIndex - 1
        ElseIf e.Delta < 0 Then
            If _selectedIndex < _items.Count - 1 Then SelectedIndex = _selectedIndex + 1
        End If
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        If Not Enabled Then
            If _droppedDown Then CloseDropDown()
            _caretBlinkTimer.Stop()
            _caretVisible = False
            鼠标状态 = MouseStateEnum.Normal
        End If
        Invalidate()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        Invalidate()
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