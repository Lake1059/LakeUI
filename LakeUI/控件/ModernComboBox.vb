Imports System.ComponentModel
Imports System.Numerics
Imports Vortice.Direct2D1

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
                _owner._textRenderer.SetText(String.Empty, 0, True, False)
                _owner.OnItemsTextChanged()
            ElseIf _owner._selectedIndex > index Then
                _owner._selectedIndex -= 1
            End If
            _owner.Invalidate()
        End Sub

        Protected Overrides Sub ClearItems()
            Dim hadItems As Boolean = Count > 0
            MyBase.ClearItems()
            Dim hadSelection As Boolean = _owner._selectedIndex >= 0
            _owner._selectedIndex = -1
            If hadSelection Then
                _owner._textRenderer.SetText(String.Empty, 0, True, False)
                _owner.OnItemsTextChanged()
            End If
            If hadItems Then
                _owner.OnSelectedIndexChangedExternal()
            End If
            _owner.Invalidate()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, item As String)
            MyBase.SetItem(index, item)
            If _owner._selectedIndex = index Then
                _owner._textRenderer.SetText(item, item.Length, True, False)
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

        Public Overloads Sub AddRange(collection As IEnumerable(Of ToolTipEntry))
            If collection Is Nothing Then Return
            For Each entry In collection
                If entry IsNot Nothing Then MyBase.Add(entry)
            Next
            RebuildDictionary()
        End Sub

        Public Overloads Sub AddRange(entries As IEnumerable(Of KeyValuePair(Of String, String)))
            If entries Is Nothing Then Return
            For Each kv In entries
                MyBase.Add(New ToolTipEntry(kv.Key, kv.Value))
            Next
            RebuildDictionary()
        End Sub

        Public Overloads Sub AddRange(ParamArray entries As ToolTipEntry())
            If entries Is Nothing Then Return
            For Each entry In entries
                If entry IsNot Nothing Then MyBase.Add(entry)
            Next
            RebuildDictionary()
        End Sub

        Public Overloads Sub Add(itemText As String, toolTipText As String)
            MyBase.Add(New ToolTipEntry(itemText, toolTipText))
        End Sub

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
    Private ReadOnly _textRenderer As SingleLineTextBoxRenderer
    Private _mouseDownSelecting As Boolean = False
    Private _imeComposing As Boolean = False

    Private _items As ItemCollection
    Private _selectedIndex As Integer = -1
    Private _pendingSelectedIndexChanged As Boolean = False
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

    ' V2：D2D 资源由 WindowCompositor 按顶层 Form 共享管理，本控件不再持有 _dcRT / _ssaaCache。
#End Region

#Region "单行文本内核适配"
    Private Property _text As String
        Get
            Return _textRenderer.Text
        End Get
        Set(value As String)
            _textRenderer.SetText(value, _textRenderer.CaretColumn, False, False)
        End Set
    End Property

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
            Dim textChanged As Boolean = _text <> v
            Dim matchIndex As Integer = FindItemIndexExact(v)
            Dim selectedIndexChanged As Boolean = _selectedIndex <> matchIndex
            If Not textChanged AndAlso Not selectedIndexChanged Then Return

            If textChanged Then
                _textRenderer.SetText(v, 0, True, False)
            End If
            _selectedIndex = matchIndex
            Invalidate()
            If textChanged Then RaiseEvent textChanged(Me, EventArgs.Empty)
            If selectedIndexChanged Then RaiseEvent selectedIndexChanged(Me, EventArgs.Empty)
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
            SetSelectedIndexCore(value, False)
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

    Private Shared ReadOnly 默认光标颜色 As Color = Color.FromArgb(220, 220, 220)
    Private 光标颜色 As Color = 默认光标颜色
    <Category("LakeUI"), Description("光标颜色"), Browsable(True)>
    Public Property CaretColor As Color
        Get
            Return 光标颜色
        End Get
        Set(value As Color)
            SetValue(光标颜色, value)
            If _textRenderer IsNot Nothing Then _textRenderer.CaretColor = 光标颜色
        End Set
    End Property

    Private Function ShouldSerializeCaretColor() As Boolean
        Return 光标颜色 <> 默认光标颜色
    End Function

    Private Sub ResetCaretColor()
        CaretColor = 默认光标颜色
    End Sub

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
            If 启用编辑 = value Then Return
            启用编辑 = value
            _textRenderer.Editable = value
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

    Private 下拉毛玻璃模式 As PopupBackdropMode = PopupBackdropMode.None
    <Category("LakeUI - DropDown Backdrop"), Description("下拉列表毛玻璃背景模式。Auto = 展开前截取下拉所在屏幕区域；Image = 使用 DropDownBackdropImage。"), DefaultValue(GetType(PopupBackdropMode), "None"), Browsable(True)>
    Public Property DropDownBackdropMode As PopupBackdropMode
        Get
            Return 下拉毛玻璃模式
        End Get
        Set(value As PopupBackdropMode)
            SetValue(下拉毛玻璃模式, value)
        End Set
    End Property

    Private 下拉毛玻璃图片 As Image = Nothing
    <Category("LakeUI - DropDown Backdrop"), Description("Image 模式下作为下拉列表毛玻璃源的图片。"), DefaultValue(GetType(Image), Nothing), Browsable(True)>
    Public Property DropDownBackdropImage As Image
        Get
            Return 下拉毛玻璃图片
        End Get
        Set(value As Image)
            SetValue(下拉毛玻璃图片, value)
        End Set
    End Property

    Private 下拉毛玻璃Tint颜色 As Color = Color.FromArgb(20, 220, 220, 220)
    <Category("LakeUI - DropDown Backdrop"), Description("下拉列表毛玻璃 tint 叠加颜色（含 Alpha）。"), DefaultValue(GetType(Color), "20, 220, 220, 220"), Browsable(True)>
    Public Property DropDownBackdropTintColor As Color
        Get
            Return 下拉毛玻璃Tint颜色
        End Get
        Set(value As Color)
            SetValue(下拉毛玻璃Tint颜色, value)
        End Set
    End Property

    Private 下拉毛玻璃模糊半径 As Integer = 10
    <Category("LakeUI - DropDown Backdrop"), Description("下拉列表毛玻璃模糊半径（逻辑像素）。1 - 96。"), DefaultValue(10), Browsable(True)>
    Public Property DropDownBackdropBlurRadius As Integer
        Get
            Return 下拉毛玻璃模糊半径
        End Get
        Set(value As Integer)
            SetValue(下拉毛玻璃模糊半径, Math.Max(1, Math.Min(96, value)))
        End Set
    End Property

    Private 下拉毛玻璃模糊次数 As Integer = 1
    <Category("LakeUI - DropDown Backdrop"), Description("下拉列表毛玻璃 box blur 通过次数（0=不模糊，3≈高斯）。"), DefaultValue(1), Browsable(True)>
    Public Property DropDownBackdropBlurPasses As Integer
        Get
            Return 下拉毛玻璃模糊次数
        End Get
        Set(value As Integer)
            SetValue(下拉毛玻璃模糊次数, Math.Max(0, Math.Min(5, value)))
        End Set
    End Property

    Private 下拉毛玻璃下采样 As Integer = 4
    <Category("LakeUI - DropDown Backdrop"), Description("下拉列表毛玻璃下采样倍率（建议 1/2/4/6/8）。"), DefaultValue(4), Browsable(True)>
    Public Property DropDownBackdropDownsampleFactor As Integer
        Get
            Return 下拉毛玻璃下采样
        End Get
        Set(value As Integer)
            SetValue(下拉毛玻璃下采样, Math.Max(1, value))
        End Set
    End Property

    Private 下拉毛玻璃噪点不透明度 As Byte = 0
    <Category("LakeUI - DropDown Backdrop"), Description("下拉列表毛玻璃噪点叠加层不透明度 (0-255)。0 = 关闭噪点。"), DefaultValue(CByte(0)), Browsable(True)>
    Public Property DropDownBackdropNoiseOpacity As Byte
        Get
            Return 下拉毛玻璃噪点不透明度
        End Get
        Set(value As Byte)
            SetValue(下拉毛玻璃噪点不透明度, value)
        End Set
    End Property

    Private 下拉毛玻璃噪点缩放 As Single = 1.0F
    <Category("LakeUI - DropDown Backdrop"), Description("下拉列表毛玻璃噪点 tile 缩放（>1 颗粒变粗）。"), DefaultValue(1.0F), Browsable(True)>
    Public Property DropDownBackdropNoiseScale As Single
        Get
            Return 下拉毛玻璃噪点缩放
        End Get
        Set(value As Single)
            SetValue(下拉毛玻璃噪点缩放, Math.Max(0.1F, value))
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

    Private 下拉选中文字颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("下拉列表选中项文字颜色，Empty 时使用 ForeColor"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property DropDownSelectedForeColor As Color
        Get
            Return 下拉选中文字颜色
        End Get
        Set(value As Color)
            SetValue(下拉选中文字颜色, value)
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

    Private 下拉高亮兼容内边距 As Boolean = False
    <Category("LakeUI"), Description("是否让选项高亮区域兼容内边距（左右方向随 DropDownPadding 收缩）"), DefaultValue(GetType(Boolean), "False"), Browsable(True)>
    Public Property DropDownHighlightMatchPadding As Boolean
        Get
            Return 下拉高亮兼容内边距
        End Get
        Set(value As Boolean)
            SetValue(下拉高亮兼容内边距, value)
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

    Private Shared ReadOnly 默认下拉滚动条颜色 As Color = Color.FromArgb(140, 140, 140)
    Private 下拉滚动条颜色 As Color = 默认下拉滚动条颜色
    <Category("LakeUI"), Description("下拉列表滚动条滑块颜色"), Browsable(True)>
    Public Property DropDownScrollBarColor As Color
        Get
            Return 下拉滚动条颜色
        End Get
        Set(value As Color)
            下拉滚动条颜色 = value
        End Set
    End Property

    Private Function ShouldSerializeDropDownScrollBarColor() As Boolean
        Return 下拉滚动条颜色 <> 默认下拉滚动条颜色
    End Function

    Private Sub ResetDropDownScrollBarColor()
        下拉滚动条颜色 = 默认下拉滚动条颜色
    End Sub

    Private Shared ReadOnly 默认下拉滚动条悬停颜色 As Color = Color.FromArgb(200, 200, 200)
    Private 下拉滚动条悬停颜色 As Color = 默认下拉滚动条悬停颜色
    <Category("LakeUI"), Description("下拉列表滚动条滑块悬停/拖拽颜色"), Browsable(True)>
    Public Property DropDownScrollBarHoverColor As Color
        Get
            Return 下拉滚动条悬停颜色
        End Get
        Set(value As Color)
            下拉滚动条悬停颜色 = value
        End Set
    End Property

    Private Function ShouldSerializeDropDownScrollBarHoverColor() As Boolean
        Return 下拉滚动条悬停颜色 <> 默认下拉滚动条悬停颜色
    End Function

    Private Sub ResetDropDownScrollBarHoverColor()
        下拉滚动条悬停颜色 = 默认下拉滚动条悬停颜色
    End Sub

    Private Shared ReadOnly 默认下拉滚动条轨道颜色 As Color = Color.FromArgb(20, 255, 255, 255)
    Private 下拉滚动条轨道颜色 As Color = 默认下拉滚动条轨道颜色
    <Category("LakeUI"), Description("下拉列表滚动条轨道颜色"), Browsable(True)>
    Public Property DropDownScrollBarTrackColor As Color
        Get
            Return 下拉滚动条轨道颜色
        End Get
        Set(value As Color)
            下拉滚动条轨道颜色 = value
        End Set
    End Property

    Private Function ShouldSerializeDropDownScrollBarTrackColor() As Boolean
        Return 下拉滚动条轨道颜色 <> 默认下拉滚动条轨道颜色
    End Function

    Private Sub ResetDropDownScrollBarTrackColor()
        下拉滚动条轨道颜色 = 默认下拉滚动条轨道颜色
    End Sub

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

    Public Enum DropDownDisplayMode
        Classic = 0
        Overlay = 1
    End Enum
    Private 下拉显示模式 As DropDownDisplayMode = DropDownDisplayMode.Classic
    <Category("LakeUI"), Description("下拉列表显示模式（Classic = 常规下拉；Overlay = 选中项与控件重合）"), DefaultValue(DropDownDisplayMode.Classic), Browsable(True)>
    Public Property DropDownMode As DropDownDisplayMode
        Get
            Return 下拉显示模式
        End Get
        Set(value As DropDownDisplayMode)
            SetValue(下拉显示模式, value)
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

    Private _backgroundSource As Control = Nothing
    ''' <summary>
    ''' 背景采样源（超容器背景映射）。透明背景模式下，控件会调用此控件的绘制流程取像素作为底图，
    ''' 从而实现跨越任意层级的"穿透显示"效果。
    ''' 为 Nothing 时不进行背景采样。
    ''' </summary>
    <Category("LakeUI"),
                  Description("背景采样源（超容器背景映射）。设置后将跨越任意层级直接采样此控件的绘制内容作为透明背景；为空时不进行背景采样。"),
                 DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource IsNot value Then
                _backgroundSource = value
                Me.Invalidate()
            End If
        End Set
    End Property
#End Region

#Region "初始化"
    Public Sub New()
        _textRenderer = New SingleLineTextBoxRenderer(Me)
        AddHandler _textRenderer.TextChanged, Sub() NotifyTextChanged()
        InitializeComponent()
        _items = New ItemCollection(Me)
        _itemToolTips = New ToolTipEntryCollection()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.Selectable Or
                 ControlStyles.StandardClick Or
                 ControlStyles.SupportsTransparentBackColor, True)
        UpdateStyles()
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        If 启用编辑 Then
            ImeHelper.AssociateDefault(Handle)
        End If
        _textRenderer.StartCaretBlink()
    End Sub

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        _textRenderer.StopCaretBlink()
        CloseDropDown()
        MyBase.OnHandleDestroyed(e)
    End Sub
#End Region

#Region "绘制"
    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' V2 契约（与 ModernButton 一致）：
        '   • BackgroundSource 已设置 → 跳过基类填底，背景由 OnPaint 内显式穿透绘制；
        '   • 否则一律走 .NET 自身透明逻辑——半透明 BackColor 由基类把父级背景合成到 HDC，
        '     不透明色由基类填底。BindDC 之后 DC RT 初始像素即正确底图，
        '     避免"HDC 残留 → 乱照父窗体其它区域"的故障。
        '   • 圆角 + 不透明 BackColor 时角落会露出方形底色，
        '     使用方应把 BackColor 设为透明（与 ModernButton 同约定）。
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim w As Integer = ClientRectangle.Width
        Dim h As Integer = ClientRectangle.Height
        If w <= 0 OrElse h <= 0 Then Return
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
        If hasRadius OrElse MyBase.BackColor.A < 255 Then
            ' 透明背景采样统一走 D2D 路径（在下方第 1 个 scope 内贴底图），
            ' 避免 dcRT.EndDraw 把圆角外像素覆盖为黑。
        End If

        Dim ssaa As Integer = Math.Max(1, CInt(超采样倍率))
        If Class1.GlobalSSAA <> Class1.SuperSamplingScaleEnum.OFF Then ssaa = Math.Max(ssaa, CInt(Class1.GlobalSSAA))

        Using scope = D2DHelperV2.BeginPaint(e, Me, ssaa)
            If scope Is Nothing Then Return
            If _backgroundSource IsNot Nothing Then
                BackgroundPenetrationV2.PaintBackground(Me, scope, _backgroundSource)
            End If

            Dim gRT As ID2D1RenderTarget = scope.GraphicsLayer
            绘制背景_D2D(gRT, hasRadius, boundsRect, effBg, effBg2)
            scope.FlushGraphics()

            Dim dcRT As ID2D1DCRenderTarget = scope.DCRenderTarget
            DrawTextContent_D2D(dcRT, w, h)
            绘制分隔线与箭头_D2D(dcRT, w, h, bc)
            绘制边框_D2D(dcRT, hasRadius, GetBorderRenderRect(boundsRect), bc)
            If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
                ' 与 绘制背景_D2D 中 fillRect / 半径 保持一致，保证遮罩圆角曲线与背景圆角完全重合。
                Dim overlayRect As New RectangleF(boundsRect.X, boundsRect.Y, boundsRect.Width + 1, boundsRect.Height + 1)
                If hasRadius Then
                    Using geo = RectangleRenderer.创建圆角矩形几何(overlayRect, 边框圆角半径 * DpiScale())
                        RectangleRenderer.绘制圆角背景_D2D(dcRT, geo, overlayRect, 禁用时遮罩颜色, Color.Empty, 渐变方向)
                    End Using
                Else
                    RectangleRenderer.绘制矩形背景_D2D(dcRT, overlayRect, 禁用时遮罩颜色, Color.Empty, 渐变方向)
                End If
            End If
        End Using
    End Sub

    Private Sub 绘制背景_D2D(rt As ID2D1RenderTarget, hasRadius As Boolean, boundsRect As RectangleF, bgClr As Color, bgClr2 As Color)
        ' BackColor 半透明遮罩层：位于背景层之上、状态填充色之下。
        Dim backColorMask As Color = MyBase.BackColor
        Dim hasMask As Boolean = backColorMask.A > 0 AndAlso backColorMask.A < 255
        Dim hasFill As Boolean = bgClr.A > 0 OrElse (bgClr2 <> Color.Empty AndAlso bgClr2.A > 0)
        Dim arrowBgClr As Color = Color.Empty
        Dim arrowBgClr2 As Color = Color.Empty
        Dim hasArrowFill As Boolean = 获取箭头区域背景颜色(arrowBgClr, arrowBgClr2)
        If Not hasMask AndAlso Not hasFill AndAlso Not hasArrowFill Then Return
        Dim s As Single = DpiScale()
        Dim fillRect As New RectangleF(boundsRect.X, boundsRect.Y, boundsRect.Width + 1, boundsRect.Height + 1)

        If hasArrowFill Then
            绘制分区背景_D2D(rt, hasRadius, fillRect, s, hasMask, backColorMask, hasFill, bgClr, bgClr2, arrowBgClr, arrowBgClr2)
            Return
        End If

        If hasRadius Then
            Using geo = RectangleRenderer.创建圆角矩形几何(fillRect, 边框圆角半径 * s)
                If hasMask Then
                    RectangleRenderer.绘制圆角背景_D2D(rt, geo, fillRect, backColorMask, Color.Empty, 渐变方向)
                End If
                If hasFill Then
                    RectangleRenderer.绘制圆角背景_D2D(rt, geo, fillRect, bgClr, bgClr2, 渐变方向)
                End If
            End Using
        Else
            If hasMask Then
                RectangleRenderer.绘制矩形背景_D2D(rt, fillRect, backColorMask, Color.Empty, 渐变方向)
            End If
            If hasFill Then
                RectangleRenderer.绘制矩形背景_D2D(rt, fillRect, bgClr, bgClr2, 渐变方向)
            End If
        End If
    End Sub

    Private Function 获取箭头区域背景颜色(ByRef bgClr As Color, ByRef bgClr2 As Color) As Boolean
        bgClr = Color.Empty
        bgClr2 = Color.Empty
        If Not 启用编辑 OrElse Not _mouseOverArrow Then Return False

        Select Case 鼠标状态
            Case MouseStateEnum.Hover
                bgClr = 鼠标移上时背景颜色
                bgClr2 = 鼠标移上时渐变颜色
            Case MouseStateEnum.Pressed
                bgClr = 鼠标按下时背景颜色
                bgClr2 = 鼠标按下时渐变颜色
        End Select

        Return bgClr.A > 0 OrElse (bgClr2 <> Color.Empty AndAlso bgClr2.A > 0)
    End Function

    Private Sub 绘制分区背景_D2D(rt As ID2D1RenderTarget, hasRadius As Boolean, fillRect As RectangleF, s As Single,
                              hasMask As Boolean, backColorMask As Color,
                              hasBodyFill As Boolean, bodyBgClr As Color, bodyBgClr2 As Color,
                              arrowBgClr As Color, arrowBgClr2 As Color)
        Dim arrowRect As RectangleF = 获取箭头区域背景矩形(fillRect)
        Dim bodyRect As New RectangleF(fillRect.X, fillRect.Y,
                                       Math.Max(0, arrowRect.X - fillRect.X),
                                       fillRect.Height)

        If hasRadius Then
            Using geo = RectangleRenderer.创建圆角矩形几何(fillRect, 边框圆角半径 * s)
                If hasMask Then
                    RectangleRenderer.绘制圆角背景_D2D(rt, geo, fillRect, backColorMask, Color.Empty, 渐变方向)
                End If
                If hasBodyFill Then
                    绘制裁剪背景_D2D(rt, bodyRect, geo, fillRect, bodyBgClr, bodyBgClr2)
                End If
                绘制裁剪背景_D2D(rt, arrowRect, geo, arrowRect, arrowBgClr, arrowBgClr2)
            End Using
        Else
            If hasMask Then
                RectangleRenderer.绘制矩形背景_D2D(rt, fillRect, backColorMask, Color.Empty, 渐变方向)
            End If
            If hasBodyFill Then
                绘制裁剪背景_D2D(rt, bodyRect, Nothing, fillRect, bodyBgClr, bodyBgClr2)
            End If
            绘制裁剪背景_D2D(rt, arrowRect, Nothing, arrowRect, arrowBgClr, arrowBgClr2)
        End If
    End Sub

    Private Function 获取箭头区域背景矩形(fillRect As RectangleF) As RectangleF
        Dim arrowX As Single = Math.Max(fillRect.X, ClientRectangle.Width - ArrowAreaWidth)
        Dim arrowRight As Single = fillRect.Right
        Return New RectangleF(arrowX, fillRect.Y, Math.Max(0, arrowRight - arrowX), fillRect.Height)
    End Function

    Private Sub 绘制裁剪背景_D2D(rt As ID2D1RenderTarget, clipRect As RectangleF, geo As ID2D1Geometry,
                              brushBounds As RectangleF, bgClr As Color, bgClr2 As Color)
        If clipRect.Width <= 0 OrElse clipRect.Height <= 0 Then Return
        If bgClr.A = 0 AndAlso (bgClr2 = Color.Empty OrElse bgClr2.A = 0) Then Return

        rt.PushAxisAlignedClip(New Vortice.RawRectF(clipRect.X, clipRect.Y, clipRect.Right, clipRect.Bottom), AntialiasMode.PerPrimitive)
        Try
            If geo IsNot Nothing Then
                RectangleRenderer.绘制圆角背景_D2D(rt, geo, brushBounds, bgClr, bgClr2, 渐变方向)
            Else
                RectangleRenderer.绘制矩形背景_D2D(rt, brushBounds, bgClr, bgClr2, 渐变方向)
            End If
        Finally
            rt.PopAxisAlignedClip()
        End Try
    End Sub

    Private Sub 绘制边框_D2D(rt As ID2D1RenderTarget, hasRadius As Boolean, boundsRect As RectangleF, borderClr As Color)
        If 边框宽度 <= 0 OrElse borderClr.A = 0 Then Return
        Dim s As Single = DpiScale()
        If hasRadius Then
            Using geo = RectangleRenderer.创建圆角矩形几何(boundsRect, 边框圆角半径 * s)
                RectangleRenderer.绘制圆角边框_D2D(rt, geo, borderClr, 边框宽度 * s)
            End Using
        Else
            RectangleRenderer.绘制矩形边框_D2D(rt, boundsRect, borderClr, 边框宽度 * s)
        End If
    End Sub

    Private Function GetBorderRenderRect(boundsRect As RectangleF) As RectangleF
        If Not Focused Then Return boundsRect
        Return New RectangleF(boundsRect.X, boundsRect.Y, boundsRect.Width + 1.0F, boundsRect.Height)
    End Function

    Private Sub DrawTextContent_D2D(rt As ID2D1DCRenderTarget, w As Integer, h As Integer)
        SyncTextRenderer()
        _textRenderer.Draw(rt)
    End Sub

    Private Sub 绘制分隔线与箭头_D2D(rt As ID2D1RenderTarget, w As Integer, h As Integer, borderClr As Color)
        Dim s As Single = DpiScale()
        Dim aaw As Integer = ArrowAreaWidth
        Dim bi As Integer = CInt(边框宽度 * s)
        Dim sepX As Single = w - aaw - If(bi > 0, bi / 2.0F, 0)
        Dim topInset As Integer = Math.Max(Padding.Top, bi)
        Dim bottomInset As Integer = Math.Max(Padding.Bottom, bi)

        ' 分隔线
        If 显示分隔线 AndAlso 边框宽度 > 0 Then
            Using br = rt.CreateSolidColorBrush(D2DHelper.ToColor4(borderClr))
                rt.DrawLine(New Vector2(sepX, topInset), New Vector2(sepX, h - bottomInset), br, 边框宽度 * s)
            End Using
        End If

        ' 箭头颜色解析
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
        If effArrowClr.A = 0 Then Return

        ' 箭头几何（圆角三角形 PathGeometry）
        Dim squareLeft As Single = w - aaw
        Dim centerX As Single = squareLeft + aaw / 2.0F
        Dim centerY As Single = h / 2.0F
        Dim scaledArrow As Single = 箭头大小 * s
        Dim arrW As Single = scaledArrow
        Dim arrH As Single = CSng(scaledArrow * Math.Sqrt(3.0) / 2.0)
        Dim verts() As PointF = {
            New PointF(centerX - arrW / 2.0F, centerY - arrH / 2.0F),
            New PointF(centerX + arrW / 2.0F, centerY - arrH / 2.0F),
            New PointF(centerX, centerY + arrH / 2.0F)
        }
        Dim cr As Single = Math.Max(scaledArrow * 0.2F, 1.0F)

        Dim path As ID2D1PathGeometry = D2DHelper.GetD2DFactory().CreatePathGeometry()
        Dim sink As ID2D1GeometrySink = path.Open()
        Try
            Dim firstA As Vector2 = Nothing
            For i As Integer = 0 To 2
                Dim curr As PointF = verts(i)
                Dim prv As PointF = verts((i + 2) Mod 3)
                Dim nxt As PointF = verts((i + 1) Mod 3)
                Dim d1x As Single = prv.X - curr.X, d1y As Single = prv.Y - curr.Y
                Dim d2x As Single = nxt.X - curr.X, d2y As Single = nxt.Y - curr.Y
                Dim l1 As Single = CSng(Math.Sqrt(d1x * d1x + d1y * d1y))
                Dim l2 As Single = CSng(Math.Sqrt(d2x * d2x + d2y * d2y))
                Dim a As New Vector2(curr.X + cr * d1x / l1, curr.Y + cr * d1y / l1)
                Dim b As New Vector2(curr.X + cr * d2x / l2, curr.Y + cr * d2y / l2)
                Dim cp1 As New Vector2(a.X + 2.0F / 3.0F * (curr.X - a.X), a.Y + 2.0F / 3.0F * (curr.Y - a.Y))
                Dim cp2 As New Vector2(b.X + 2.0F / 3.0F * (curr.X - b.X), b.Y + 2.0F / 3.0F * (curr.Y - b.Y))
                If i = 0 Then
                    sink.BeginFigure(a, FigureBegin.Filled)
                    firstA = a
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

        Try
            Using br = rt.CreateSolidColorBrush(D2DHelper.ToColor4(effArrowClr))
                rt.FillGeometry(path, br)
            End Using
        Finally
            path.Dispose()
        End Try
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
                            SetSelectedIndexFromUser(_selectedIndex + 1)
                        End If
                    End If
                    e.Handled = True
                Case Keys.Up
                    If _droppedDown Then
                        If _selectedIndex > 0 Then
                            SetSelectedIndexFromUser(_selectedIndex - 1)
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
                        SetSelectedIndexFromUser(_selectedIndex + 1)
                    End If
                End If
                e.Handled = True
            Case Keys.Up
                If _droppedDown Then
                    If _selectedIndex > 0 Then
                        SetSelectedIndexFromUser(_selectedIndex - 1)
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
            SyncTextRenderer()
            _textRenderer.BeginMouseSelection(e.X)
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
            SyncTextRenderer()
            _textRenderer.UpdateMouseSelection(e.X)
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
        SyncTextRenderer()
        Return _textRenderer.HitTestColumn(x)
    End Function
#End Region

#Region "光标移动"
    Private Sub MoveCaret(deltaCol As Integer, extend As Boolean)
        SyncTextRenderer()
        _textRenderer.MoveCaret(deltaCol, extend)
    End Sub

    Private Sub MoveCaretHome(extend As Boolean)
        SyncTextRenderer()
        _textRenderer.MoveCaretHome(extend)
    End Sub

    Private Sub MoveCaretEnd(extend As Boolean)
        SyncTextRenderer()
        _textRenderer.MoveCaretEnd(extend)
    End Sub

    Private Sub MoveCaretWordLeft(extend As Boolean)
        SyncTextRenderer()
        _textRenderer.MoveCaretWordLeft(extend)
    End Sub

    Private Sub MoveCaretWordRight(extend As Boolean)
        SyncTextRenderer()
        _textRenderer.MoveCaretWordRight(extend)
    End Sub

    Private Sub EnsureCaretVisible()
        SyncTextRenderer()
        _textRenderer.EnsureCaretVisible()
    End Sub
#End Region

#Region "文本编辑核心"
    Private Sub InsertTextCore(text As String)
        SyncTextRenderer()
        _textRenderer.InsertText(text)
    End Sub

    Private Sub HandleBackspace()
        SyncTextRenderer()
        _textRenderer.HandleBackspace()
    End Sub

    Private Sub HandleDelete()
        SyncTextRenderer()
        _textRenderer.HandleDelete()
    End Sub

#End Region

#Region "选区"
    Public Sub SelectAll()
        _textRenderer.SelectAll()
    End Sub

    Private Sub ClearSelection()
        _textRenderer.ClearSelection()
    End Sub

    Private Function GetSelectedText() As String
        Return _textRenderer.GetSelectedText()
    End Function
#End Region

#Region "剪贴板"
    Private Sub CopySelection()
        _textRenderer.CopySelection()
    End Sub

    Private Sub CutSelection()
        _textRenderer.Editable = 启用编辑
        _textRenderer.CutSelection()
    End Sub

    Private Sub PasteText()
        _textRenderer.Editable = 启用编辑
        _textRenderer.PasteText()
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

    Private Function FindItemIndexExact(s As String) As Integer
        If _items Is Nothing Then Return -1
        For i As Integer = 0 To _items.Count - 1
            If String.Equals(_items(i), s, StringComparison.Ordinal) Then
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
        RaisePendingSelectedIndexChanged()
    End Sub

    Friend Sub OnDropDownClosed()
        _droppedDown = False
        _dropDownForm = Nothing
        Invalidate()
        RaiseEvent DropDownClosed(Me, EventArgs.Empty)
        RaisePendingSelectedIndexChanged()
    End Sub

    Friend Sub OnDropDownItemClicked(index As Integer)
        If index >= 0 AndAlso index < _items.Count Then
            SetSelectedIndexFromUser(index)
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

        Private _finalHeight As Integer
        Private _originPt As Point
        Private _useIdle As Boolean = False
        Private _suppressBoundsRender As Boolean = False

        ' Overlay 模式
        Private _overlayMode As Boolean = False
        Private _alignItemScreenY, _alignItemDropdownY, _closeCenterScreenY As Integer

        ' 展开/关闭动画
        Private ReadOnly 展开关闭秒表 As New Stopwatch()
        Private 展开关闭计时器 As PrecisionTimer
        Private 展开关闭驱动运行中 As Boolean = False
        Private 展开关闭动画中 As Boolean = False
        Friend 正在关闭动画 As Boolean = False

        ' 悬停动画
        Private ReadOnly 悬停秒表 As New Stopwatch()
        Private 悬停计时器 As PrecisionTimer
        Private 悬停驱动运行中 As Boolean = False
        Private 悬停动画起始Y As Single = -1, 悬停动画目标Y As Single = -1, 悬停动画当前Y As Single = -1
        Private 悬停动画起始高度, 悬停动画目标高度, 悬停动画当前高度 As Single
        Private 悬停动画中 As Boolean = False
        Private 悬停动画显示 As Boolean = False

        Private _backdrop As PopupBackdropRenderer

        Private Structure DropDownLayout
            Public ReadOnly Bw, Inset, RightCorr, ScrollW, ItemH, VisCount, HlL, HlR As Integer
            Public ReadOnly Pad As Padding

            Public Sub New(ownerForm As DropDownListForm, w As Integer)
                Dim owner = ownerForm._owner
                Dim s As Single = owner.DpiScale()
                Bw = CInt(owner.下拉边框宽度 * s)
                Pad = owner.下拉内边距
                Inset = Math.Max(Bw, 1)
                RightCorr = If(Bw >= 2, 1, 0)
                ScrollW = If(ownerForm._scrollBarVisible, ownerForm._scrollBar.GetReservedWidth(w, Inset), 0)
                ItemH = CInt(owner.下拉项高度 * s)
                VisCount = Math.Min(owner._items.Count, owner.最大下拉项数)
                Dim hlLeft As Integer = Inset - owner.下拉高亮左侧偏移
                Dim hlRight As Integer = Inset + RightCorr - owner.下拉高亮右侧偏移
                If owner.下拉高亮兼容内边距 Then
                    hlLeft += Pad.Left
                    hlRight += Pad.Right
                End If
                HlL = hlLeft
                HlR = hlRight
            End Sub

            Public Function ClipRect(w As Integer, h As Integer) As RectangleF
                Return New RectangleF(Inset, Inset,
                    Math.Max(0, w - Inset * 2 - RightCorr - ScrollW),
                    Math.Max(0, h - Inset * 2))
            End Function

            Public Function ItemRect(w As Integer, visualIndex As Integer) As RectangleF
                Return New RectangleF(HlL, Bw + Pad.Top + visualIndex * ItemH,
                    Math.Max(0, w - HlL - HlR - ScrollW), ItemH)
            End Function

            Public Function TextRect(itemRect As RectangleF) As RectangleF
                Return New RectangleF(itemRect.X + Pad.Left + 4, itemRect.Y,
                    Math.Max(0, itemRect.Width - Pad.Left - Pad.Right - 8), itemRect.Height)
            End Function
        End Structure

        Private Const WM_LBUTTONDOWN As Integer = &H201
        Private Const WM_RBUTTONDOWN As Integer = &H204
        Private Const WM_MBUTTONDOWN As Integer = &H207
        Private Const WM_NCLBUTTONDOWN As Integer = &HA1
        Private Const WM_ACTIVATEAPP As Integer = &H1C

        Public Sub New(owner As ModernComboBox)
            _owner = owner
            Me.DoubleBuffered = True
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer, True)
            Me.BackColor = ToOpaqueColor(_owner.下拉背景颜色)
            ApplyPopupWindowState()
            UpdateStyles()
            Me.AutoScaleMode = AutoScaleMode.Dpi

            _useIdle = (owner.下拉动画帧率 <= 0)
            If Not _useIdle Then
                展开关闭计时器 = CreateFrameTimer(owner.下拉动画帧率)
                悬停计时器 = CreateFrameTimer(owner.下拉动画帧率)
            End If

            Dim visCount As Integer = Math.Min(owner._items.Count, owner.最大下拉项数)
            Dim s As Single = owner.DpiScale()
            Dim bw As Integer = CInt(owner.下拉边框宽度 * s)
            Dim itemH As Integer = CInt(owner.下拉项高度 * s)
            Dim pad As Padding = owner.下拉内边距
            _finalHeight = visCount * itemH + bw * 2 + pad.Top + pad.Bottom
            _overlayMode = (owner.下拉显示模式 = DropDownDisplayMode.Overlay)
            Dim scr As Screen = Screen.FromControl(owner)

            If _overlayMode Then
                Dim alignIndex As Integer = If(owner._selectedIndex >= 0, owner._selectedIndex, 0)
                If alignIndex >= owner._items.Count Then alignIndex = 0

                _scrollOffset = CalculateCenteredScrollOffset(owner._items.Count, visCount, alignIndex)

                Dim visIdx As Integer = alignIndex - _scrollOffset
                _alignItemDropdownY = bw + pad.Top + visIdx * itemH

                Dim comboScreenPt As Point = owner.PointToScreen(New Point(0, 0))
                Dim centerOffset As Integer = (owner.Height - itemH) \ 2
                _alignItemScreenY = comboScreenPt.Y + centerOffset
                _closeCenterScreenY = comboScreenPt.Y + owner.Height \ 2
                _originPt = New Point(comboScreenPt.X, _alignItemScreenY - _alignItemDropdownY)

                If _originPt.Y < scr.WorkingArea.Top Then
                    _originPt.Y = scr.WorkingArea.Top
                    _alignItemScreenY = _originPt.Y + _alignItemDropdownY
                End If
                If _originPt.Y + _finalHeight > scr.WorkingArea.Bottom Then
                    _originPt.Y = scr.WorkingArea.Bottom - _finalHeight
                    _alignItemScreenY = _originPt.Y + _alignItemDropdownY
                End If
            Else
                _originPt = owner.PointToScreen(New Point(0, owner.Height + owner.下拉间距))
                If _originPt.Y + _finalHeight > scr.WorkingArea.Bottom Then
                    _originPt = owner.PointToScreen(New Point(0, -_finalHeight - owner.下拉间距))
                End If
            End If

            Me.Location = _originPt
            Me.Size = New Size(owner.Width, _finalHeight)

            _scrollBarVisible = owner._items.Count > owner.最大下拉项数
            If Not _overlayMode AndAlso owner._selectedIndex >= 0 Then
                _scrollOffset = CalculateCenteredScrollOffset(owner._items.Count, visCount, owner._selectedIndex)
            End If
        End Sub

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            ' 顶层下拉窗体由 OnPaint 里的 D2D 绘制全权接管底色。
        End Sub

        Private Function CreateFrameTimer(fps As Integer) As PrecisionTimer
            Return New PrecisionTimer() With {
                .Interval = Math.Max(1, CInt(Math.Round(1000.0R / Math.Max(1, fps)))),
                .DispatchMode = PrecisionTimer.DispatchModeEnum.NonBlocking,
                .OverrunPolicy = PrecisionTimer.OverrunPolicyEnum.Drop,
                .WorkerThreadCount = 1,
                .SynchronizingObject = Me
            }
        End Function

        Private Shared Function CalculateCenteredScrollOffset(itemCount As Integer, visibleCount As Integer, targetIndex As Integer) As Integer
            If itemCount <= 0 OrElse visibleCount <= 0 OrElse targetIndex < 0 Then Return 0
            Dim maxOffset As Integer = Math.Max(0, itemCount - visibleCount)
            Dim offset As Integer = Math.Max(0, Math.Min(maxOffset, targetIndex - visibleCount \ 2))
            If targetIndex < offset Then offset = targetIndex
            If targetIndex >= offset + visibleCount Then offset = targetIndex - visibleCount + 1
            Return Math.Max(0, Math.Min(maxOffset, offset))
        End Function

        Private Shared Function ToOpaqueColor(color As Color) As Color
            Return Color.FromArgb(255, color.R, color.G, color.B)
        End Function

        Private Function ShouldCaptureTransparentBackground() As Boolean
            Return _owner.下拉毛玻璃模式 = PopupBackdropMode.None AndAlso _owner.下拉背景颜色.A < 255
        End Function

        Private Function HasBackdropFrame() As Boolean
            Return _backdrop IsNot Nothing AndAlso _backdrop.HasFrame
        End Function

        Private Function DropDownFillColor() As Color
            Return ToOpaqueColor(_owner.下拉背景颜色)
        End Function

        Private Sub ApplyPopupWindowState()
            TransparencyKey = Color.Empty
            Opacity = 1.0R
            BackColor = ToOpaqueColor(_owner.下拉背景颜色)
        End Sub

        Private Sub 准备毛玻璃背景()
            If _backdrop Is Nothing Then _backdrop = New PopupBackdropRenderer(Me)
            _backdrop.TransientExcludeOnCapture = False

            If _owner.下拉毛玻璃模式 <> PopupBackdropMode.None Then
                _backdrop.Configure(_owner.下拉毛玻璃模式,
                                    _owner.下拉毛玻璃图片,
                                    _owner.下拉毛玻璃Tint颜色,
                                    _owner.下拉毛玻璃模糊半径,
                                    _owner.下拉毛玻璃模糊次数,
                                    _owner.下拉毛玻璃下采样,
                                    _owner.下拉毛玻璃噪点不透明度,
                                    _owner.下拉毛玻璃噪点缩放)
            ElseIf ShouldCaptureTransparentBackground() Then
                _backdrop.Configure(PopupBackdropMode.Auto,
                                    Nothing,
                                    _owner.下拉背景颜色,
                                    1,
                                    0,
                                    1,
                                    0,
                                    1.0F)
            Else
                _backdrop.Configure(PopupBackdropMode.None,
                                    Nothing,
                                    Color.Transparent,
                                    1,
                                    0,
                                    1,
                                    0,
                                    1.0F)
            End If
            _backdrop.Prepare(New Rectangle(_originPt, New Size(Me.Width, _finalHeight)), True)
        End Sub

        Private Sub 绘制毛玻璃背景(g As Graphics)
            If Not HasBackdropFrame() Then Return
            _backdrop.Draw(g, New Rectangle(0, 0, ClientSize.Width, ClientSize.Height))
        End Sub

        Protected Overrides Sub WndProc(ByRef m As Message)
            MyBase.WndProc(m)
            If m.Msg = WM_ACTIVATEAPP AndAlso m.WParam = IntPtr.Zero Then
                If Not 正在关闭动画 Then BeginInvoke(Sub() _owner.CloseDropDown())
            End If
        End Sub

        Friend Sub ShowDropDown()
            ApplyPopupWindowState()
            准备毛玻璃背景()
            Application.AddMessageFilter(Me)
            If _owner.下拉展开关闭动画时长 > 0 Then
                If _overlayMode Then
                    Me.Location = New Point(_originPt.X, _alignItemScreenY)
                End If
                Me.Size = New Size(Me.Width, 1)
                Me.Show()
                请求重绘(True)
                展开关闭动画中 = True
                正在关闭动画 = False
                展开关闭秒表.Restart()
                启动展开关闭驱动()
            Else
                Me.Show()
                请求重绘(True)
            End If
        End Sub

        Friend Sub RefreshFontResources()
            关闭工具提示()
            请求重绘()
        End Sub

        Friend Sub 开始关闭动画()
            If 正在关闭动画 Then Return
            正在关闭动画 = True
            展开关闭动画中 = True
            展开关闭秒表.Restart()
            启动展开关闭驱动()
        End Sub

        Private Sub 启动展开关闭驱动()
            If 展开关闭驱动运行中 Then Return
            展开关闭驱动运行中 = True
            设置动画驱动(展开关闭计时器, AddressOf 展开关闭帧更新, True)
        End Sub

        Private Sub 停止展开关闭驱动()
            If Not 展开关闭驱动运行中 Then Return
            展开关闭驱动运行中 = False
            设置动画驱动(展开关闭计时器, AddressOf 展开关闭帧更新, False)
        End Sub

        Private Sub 启动悬停驱动()
            If 悬停驱动运行中 Then Return
            悬停驱动运行中 = True
            设置动画驱动(悬停计时器, AddressOf 悬停帧更新, True)
        End Sub

        Private Sub 停止悬停驱动()
            If Not 悬停驱动运行中 Then Return
            悬停驱动运行中 = False
            设置动画驱动(悬停计时器, AddressOf 悬停帧更新, False)
        End Sub

        Private Sub 设置动画驱动(timer As PrecisionTimer, handler As EventHandler, enabled As Boolean)
            If _useIdle Then
                If enabled Then
                    AddHandler Application.Idle, handler
                Else
                    RemoveHandler Application.Idle, handler
                End If
                Return
            End If
            If enabled Then
                AddHandler timer.Tick, handler
                timer.Start()
            Else
                timer.Stop()
                RemoveHandler timer.Tick, handler
            End If
        End Sub

        Private Sub SetBoundsAndRender(location As Point, size As Size, Optional forceRender As Boolean = False, Optional immediate As Boolean = False)
            If size.Width <= 0 OrElse size.Height <= 0 Then Return
            Dim boundsChanged As Boolean = Me.Location <> location OrElse Me.Size <> size
            _suppressBoundsRender = True
            Try
                If boundsChanged Then
                    SetBounds(location.X, location.Y, size.Width, size.Height)
                End If
            Finally
                _suppressBoundsRender = False
            End Try
            If boundsChanged OrElse forceRender Then 请求重绘(immediate)
        End Sub

        Friend Sub 关闭并释放()
            关闭工具提示()
            停止悬停动画()
            停止展开关闭驱动()
            If 展开关闭计时器 IsNot Nothing Then 展开关闭计时器.Dispose()
            If 悬停计时器 IsNot Nothing Then 悬停计时器.Dispose()
            If _backdrop IsNot Nothing Then
                _backdrop.Dispose()
                _backdrop = Nothing
            End If
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
                    SetBoundsAndRender(_originPt, New Size(Me.Width, _finalHeight), True, True)
                End If
                Return
            End If

            Dim elapsed As Double = 展开关闭秒表.Elapsed.TotalMilliseconds
            Dim t As Single = CSng(Math.Min(elapsed / duration, 1.0))
            Dim eased As Single = 1.0F - CSng(Math.Pow(1.0 - t, 3))
            Dim targetLocation As Point = Me.Location
            Dim targetSize As Size = Me.Size

            If _overlayMode Then
                If 正在关闭动画 Then
                    Dim curTopY As Integer = CInt(_originPt.Y + (_closeCenterScreenY - _originPt.Y) * eased)
                    Dim curH As Integer = Math.Max(1, CInt(_finalHeight * (1.0F - eased)))
                    targetLocation = New Point(_originPt.X, curTopY)
                    targetSize = New Size(Me.Width, curH)
                Else
                    Dim topDist As Integer = _alignItemScreenY - _originPt.Y
                    Dim bottomDist As Integer = _finalHeight - topDist
                    Dim curTop As Integer = CInt(topDist * eased)
                    Dim curBottom As Integer = CInt(bottomDist * eased)
                    Dim curH As Integer = Math.Max(1, curTop + curBottom)
                    targetLocation = New Point(_originPt.X, _alignItemScreenY - curTop)
                    targetSize = New Size(Me.Width, curH)
                End If
            Else
                If 正在关闭动画 Then
                    targetSize = New Size(Me.Width, Math.Max(1, CInt(_finalHeight * (1.0F - eased))))
                Else
                    targetSize = New Size(Me.Width, Math.Max(1, CInt(_finalHeight * eased)))
                End If
            End If
            SetBoundsAndRender(targetLocation, targetSize)

            If t >= 1.0F Then
                停止展开关闭驱动()
                展开关闭动画中 = False
                If 正在关闭动画 Then
                    完成关闭()
                Else
                    Dim finalSize As New Size(Me.Width, _finalHeight)
                    If Me.Location <> _originPt OrElse Me.Size <> finalSize Then
                        SetBoundsAndRender(_originPt, finalSize, True, True)
                    End If
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
            绘制毛玻璃背景(e.Graphics)
            RenderDropDown(e)
        End Sub

        Protected Overrides Sub OnSizeChanged(e As EventArgs)
            MyBase.OnSizeChanged(e)
            If Not _suppressBoundsRender Then 请求重绘()
        End Sub

        Protected Overrides Sub OnLocationChanged(e As EventArgs)
            MyBase.OnLocationChanged(e)
            If Not _suppressBoundsRender Then 请求重绘()
        End Sub

        Private Sub 请求重绘(Optional immediate As Boolean = False)
            If IsDisposed OrElse Disposing Then Return
            Invalidate()
            If immediate AndAlso IsHandleCreated Then Update()
        End Sub

        Private Sub RenderDropDown(e As PaintEventArgs)
            Dim w As Integer = ClientSize.Width
            Dim h As Integer = ClientSize.Height
            If w <= 0 OrElse h <= 0 Then Return

            Dim s As Single = _owner.DpiScale()
            Dim bw As Integer = CInt(_owner.下拉边框宽度 * s)

            If _scrollBarVisible Then
                _scrollBar.ComputeLayout(w, h, bw, 0,
                    _owner.下拉内边距.Top, _owner.下拉内边距.Bottom,
                    _owner.下拉滚动条宽度,
                    _owner._items.Count, Math.Min(_owner._items.Count, _owner.最大下拉项数), _scrollOffset)
            End If

            Using scope = D2DHelperV2.BeginPaint(e, Me, 1)
                If scope Is Nothing Then Return
                Dim rt As ID2D1RenderTarget = scope.GraphicsLayer
                D2DHelper.ApplyGlobalQuality(rt)
                rt.Transform = Matrix3x2.Identity
                DrawDropDownBackground_D2D(rt, DropDownFillColor(), bw, w, h, Not HasBackdropFrame())
                DrawDropDownItems_D2D(rt, w, h, New DropDownLayout(Me, w))
                DrawDropDownScrollBar_D2D(rt, w, h, bw)
            End Using
        End Sub

        Private Sub DrawDropDownBackground_D2D(rt As ID2D1RenderTarget, backColor As Color, bw As Integer, w As Integer, h As Integer, fillBackground As Boolean)
            If fillBackground Then
                Using br = rt.CreateSolidColorBrush(D2DHelper.ToColor4(backColor))
                    rt.FillRectangle(New Vortice.Mathematics.Rect(0, 0, w, h), br)
                End Using
            End If

            If bw > 0 AndAlso _owner.下拉边框颜色.A > 0 Then
                DrawDropDownBorder_D2D(rt, w, h, bw)
            End If
        End Sub

        Private Sub DrawDropDownBorder_D2D(rt As ID2D1RenderTarget, w As Integer, h As Integer, bw As Integer)
            Dim border As Integer = Math.Min(bw, Math.Min(w, h))
            If border <= 0 Then Return

            Using br = rt.CreateSolidColorBrush(D2DHelper.ToColor4(_owner.下拉边框颜色))
                rt.FillRectangle(New Vortice.Mathematics.Rect(0, 0, w, border), br)
                If h > border Then rt.FillRectangle(New Vortice.Mathematics.Rect(0, h - border, w, border), br)

                Dim middleHeight As Integer = h - border * 2
                If middleHeight > 0 Then
                    rt.FillRectangle(New Vortice.Mathematics.Rect(0, border, border, middleHeight), br)
                    If w > border Then rt.FillRectangle(New Vortice.Mathematics.Rect(w - border, border, border, middleHeight), br)
                End If
            End Using
        End Sub

        Private Sub DrawDropDownItems_D2D(rt As ID2D1RenderTarget, w As Integer, h As Integer, layout As DropDownLayout)
            Dim s As Single = _owner.DpiScale()
            Dim clipRect As RectangleF = layout.ClipRect(w, h)
            If clipRect.Width <= 0 OrElse clipRect.Height <= 0 Then Return
            Dim hoverBrush = If(_owner.下拉悬停颜色.A > 0, rt.CreateSolidColorBrush(D2DHelper.ToColor4(_owner.下拉悬停颜色)), Nothing)
            Dim selectedBrush = If(_owner.下拉选中颜色.A > 0, rt.CreateSolidColorBrush(D2DHelper.ToColor4(_owner.下拉选中颜色)), Nothing)
            Dim shouldDrawHover As Boolean = _hoverIndex <> _owner._selectedIndex
            rt.PushAxisAlignedClip(New Vortice.RawRectF(clipRect.X, clipRect.Y, clipRect.Right, clipRect.Bottom), AntialiasMode.PerPrimitive)
            Try
                If shouldDrawHover AndAlso 悬停动画显示 AndAlso hoverBrush IsNot Nothing Then
                    Dim highlightRect As New RectangleF(layout.HlL, 悬停动画当前Y, w - layout.HlL - layout.HlR - layout.ScrollW, 悬停动画当前高度)
                    rt.FillRectangle(D2DHelper.ToD2DRect(highlightRect), hoverBrush)
                End If

                For i As Integer = 0 To layout.VisCount - 1
                    Dim idx As Integer = i + _scrollOffset
                    If idx >= _owner._items.Count Then Exit For
                    Dim itemRect As RectangleF = layout.ItemRect(w, i)

                    If idx = _owner._selectedIndex AndAlso selectedBrush IsNot Nothing Then
                        rt.FillRectangle(D2DHelper.ToD2DRect(itemRect), selectedBrush)
                    ElseIf shouldDrawHover AndAlso idx = _hoverIndex AndAlso Not 悬停动画显示 AndAlso hoverBrush IsNot Nothing Then
                        rt.FillRectangle(D2DHelper.ToD2DRect(itemRect), hoverBrush)
                    End If

                    Dim textColor As Color = If(idx = _owner._selectedIndex AndAlso _owner.下拉选中文字颜色 <> Color.Empty,
                        _owner.下拉选中文字颜色,
                        _owner.ForeColor)
                    DrawSingleLineText_D2D(rt, _owner._items(idx), _owner.Font, textColor,
                        layout.TextRect(itemRect), s, True)
                Next
            Finally
                rt.PopAxisAlignedClip()
                If hoverBrush IsNot Nothing Then hoverBrush.Dispose()
                If selectedBrush IsNot Nothing Then selectedBrush.Dispose()
            End Try
        End Sub

        Private Sub DrawDropDownScrollBar_D2D(rt As ID2D1RenderTarget, w As Integer, h As Integer, bw As Integer)
            If Not _scrollBarVisible OrElse _scrollBar.TrackRect.IsEmpty Then Return
            _scrollBar.Draw_D2D(rt, w, h, bw, 0, _owner.下拉滚动条宽度,
                _owner.下拉滚动条轨道颜色,
                _owner.下拉滚动条颜色,
                _owner.下拉滚动条悬停颜色)
        End Sub

        Private Function GetItemIndexAtY(y As Integer) As Integer
            Dim layout As New DropDownLayout(Me, ClientRectangle.Width)
            Dim idx As Integer = (y - layout.Bw - layout.Pad.Top) \ layout.ItemH + _scrollOffset
            If idx < 0 OrElse idx >= _owner._items.Count Then Return -1
            Return idx
        End Function

        Private Function GetItemRect(index As Integer) As RectangleF
            Dim layout As New DropDownLayout(Me, ClientRectangle.Width)
            Return New RectangleF(0, layout.Bw + layout.Pad.Top + (index - _scrollOffset) * layout.ItemH,
                ClientRectangle.Width, layout.ItemH)
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
                    启动悬停驱动()
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
                请求重绘()
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
            请求重绘()
        End Sub

        Private Sub 停止悬停动画()
            If 悬停动画中 Then
                悬停动画中 = False
                停止悬停驱动()
                悬停秒表.Stop()
            End If
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            If _scrollBar.IsDragging Then
                Dim total As Integer = _owner._items.Count
                Dim vis As Integer = Math.Min(total, _owner.最大下拉项数)
                _scrollOffset = _scrollBar.DragMove(e.Y, total, vis)
                请求重绘()
                Return
            End If
            If _scrollBarVisible Then
                If _scrollBar.UpdateHover(e.Location) Then 请求重绘()
            End If
            Dim idx As Integer = GetItemIndexAtY(e.Y)
            If idx <> _hoverIndex Then
                _hoverIndex = idx
                更新悬停动画()
                更新工具提示()
                请求重绘()
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
                    请求重绘()
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
                请求重绘()
            End If
            If _scrollBar.ResetHover() Then 请求重绘()
        End Sub

        Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
            MyBase.OnMouseWheel(e)
            If Not _scrollBarVisible Then Return
            Dim total As Integer = _owner._items.Count
            Dim vis As Integer = Math.Min(total, _owner.最大下拉项数)
            _scrollOffset = ScrollBarRenderer.HandleWheel(e.Delta, _scrollOffset, total, vis)
            关闭工具提示()
            请求重绘()
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
        Private _backdrop As PopupBackdropRenderer

        ' V2：D2D 资源由 WindowCompositor 按顶层 Form 共享管理。

        Public Sub New(owner As ModernComboBox)
            _owner = owner
            Me.DoubleBuffered = True
            Me.AutoScaleMode = AutoScaleMode.Dpi
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer, True)
            ApplyPopupWindowState()
        End Sub

        Public Sub ShowTip(text As String, screenLocation As Point)
            _tipText = text
            Dim pad As Padding = _owner.提示内边距
            Dim bw As Integer = DropDownBorderWidth()
            Dim maxW As Integer = _owner.提示最大宽度
            Dim contentW As Integer = maxW - pad.Left - pad.Right - bw * 2
            If contentW < 10 Then contentW = 10

            Dim measured As Size = MeasureWrappedText_D2D(_tipText, _owner.Font, contentW, _owner.DpiScale())

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

            ApplyPopupWindowState()
            准备毛玻璃背景()
            If Not Visible Then Me.Show()
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            ' 顶层提示窗体由 OnPaint 里的毛玻璃和 D2D 绘制全权接管底色。
        End Sub

        Private Shared Function ToOpaqueColor(color As Color) As Color
            Return Color.FromArgb(255, color.R, color.G, color.B)
        End Function

        Private Function DropDownBorderWidth() As Integer
            Return CInt(_owner.下拉边框宽度 * _owner.DpiScale())
        End Function

        Private Function ShouldCaptureTransparentBackground() As Boolean
            Return _owner.下拉毛玻璃模式 = PopupBackdropMode.None AndAlso _owner.下拉背景颜色.A < 255
        End Function

        Private Function HasBackdropFrame() As Boolean
            Return _backdrop IsNot Nothing AndAlso _backdrop.HasFrame
        End Function

        Private Function ToolTipFillColor() As Color
            Return ToOpaqueColor(_owner.下拉背景颜色)
        End Function

        Private Sub ApplyPopupWindowState()
            TransparencyKey = Color.Empty
            Opacity = 1.0R
            BackColor = ToolTipFillColor()
        End Sub

        Private Sub 准备毛玻璃背景()
            If _backdrop Is Nothing Then _backdrop = New PopupBackdropRenderer(Me)
            _backdrop.TransientExcludeOnCapture = True

            If _owner.下拉毛玻璃模式 <> PopupBackdropMode.None Then
                _backdrop.Configure(_owner.下拉毛玻璃模式,
                                    _owner.下拉毛玻璃图片,
                                    _owner.下拉毛玻璃Tint颜色,
                                    _owner.下拉毛玻璃模糊半径,
                                    _owner.下拉毛玻璃模糊次数,
                                    _owner.下拉毛玻璃下采样,
                                    _owner.下拉毛玻璃噪点不透明度,
                                    _owner.下拉毛玻璃噪点缩放)
            ElseIf ShouldCaptureTransparentBackground() Then
                _backdrop.Configure(PopupBackdropMode.Auto,
                                    Nothing,
                                    _owner.下拉背景颜色,
                                    1,
                                    0,
                                    1,
                                    0,
                                    1.0F)
            Else
                _backdrop.Configure(PopupBackdropMode.None,
                                    Nothing,
                                    Color.Transparent,
                                    1,
                                    0,
                                    1,
                                    0,
                                    1.0F)
            End If
            _backdrop.Prepare(Me.Bounds, True)
        End Sub

        Private Sub 绘制毛玻璃背景(g As Graphics)
            If Not HasBackdropFrame() Then Return
            _backdrop.Draw(g, New Rectangle(0, 0, ClientSize.Width, ClientSize.Height))
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            绘制毛玻璃背景(e.Graphics)

            Dim w As Integer = ClientRectangle.Width
            Dim h As Integer = ClientRectangle.Height
            If w <= 0 OrElse h <= 0 Then Return
            Dim bw As Integer = DropDownBorderWidth()
            Dim pad As Padding = _owner.提示内边距

            Dim ssaa As Integer = 1
            If Class1.GlobalSSAA <> Class1.SuperSamplingScaleEnum.OFF Then ssaa = Math.Max(ssaa, CInt(Class1.GlobalSSAA))

            Using scope = D2DHelperV2.BeginPaint(e, Me, ssaa)
                If scope Is Nothing Then Return
                Dim gRT As ID2D1RenderTarget = scope.GraphicsLayer
                DrawToolTipBackground_D2D(gRT, bw, w, h, Not HasBackdropFrame())
                scope.FlushGraphics()

                Dim textRect As New RectangleF(bw + pad.Left, bw + pad.Top,
                w - bw * 2 - pad.Left - pad.Right,
                h - bw * 2 - pad.Top - pad.Bottom)
                DrawWrappedText_D2D(scope.DCRenderTarget, _tipText, _owner.Font, _owner.提示文本颜色, textRect, _owner.DpiScale())
            End Using
        End Sub

        Private Sub DrawToolTipBackground_D2D(rt As ID2D1RenderTarget, bw As Integer, w As Integer, h As Integer, fillBackground As Boolean)
            If fillBackground Then
                Using br = rt.CreateSolidColorBrush(D2DHelper.ToColor4(ToolTipFillColor()))
                    rt.FillRectangle(New Vortice.Mathematics.Rect(0, 0, w, h), br)
                End Using
            End If

            If bw > 0 AndAlso _owner.下拉边框颜色.A > 0 Then
                DrawToolTipBorder_D2D(rt, w, h, bw)
            End If
        End Sub

        Private Sub DrawToolTipBorder_D2D(rt As ID2D1RenderTarget, w As Integer, h As Integer, bw As Integer)
            Dim border As Integer = Math.Min(bw, Math.Min(w, h))
            If border <= 0 Then Return

            Using br = rt.CreateSolidColorBrush(D2DHelper.ToColor4(_owner.下拉边框颜色))
                rt.FillRectangle(New Vortice.Mathematics.Rect(0, 0, w, border), br)
                If h > border Then rt.FillRectangle(New Vortice.Mathematics.Rect(0, h - border, w, border), br)

                Dim middleHeight As Integer = h - border * 2
                If middleHeight > 0 Then
                    rt.FillRectangle(New Vortice.Mathematics.Rect(0, border, border, middleHeight), br)
                    If w > border Then rt.FillRectangle(New Vortice.Mathematics.Rect(w - border, border, border, middleHeight), br)
                End If
            End Using
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing AndAlso _backdrop IsNot Nothing Then
                _backdrop.Dispose()
                _backdrop = Nothing
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
#End Region

#Region "输入法 IME"
    Private Sub UpdateImeWindow()
        If Not IsHandleCreated OrElse Not 启用编辑 Then Return
        SyncTextRenderer()
        Dim point As Point = _textRenderer.GetCaretImeLocation()
        ImeHelper.SetCompositionPosition(Handle, point.X, point.Y)
    End Sub
#End Region

#Region "辅助"
    Private Sub SyncTextRenderer()
        _textRenderer.Editable = 启用编辑
        _textRenderer.ForeColor = ForeColor
        _textRenderer.LineHeight = 行高
        _textRenderer.CaretWidth = 光标线宽
        _textRenderer.CaretColor = 光标颜色
        _textRenderer.SelectionColor = 选区背景色
        _textRenderer.WaterText = 水印文本
        _textRenderer.WaterTextForeColor = 水印颜色
        _textRenderer.TextAlign = CType(文本对齐, SingleLineTextBoxRenderer.TextAlignMode)
        _textRenderer.BorderSize = 边框宽度
        _textRenderer.RightReservedWidth = ArrowAreaWidth
    End Sub

    Private ReadOnly Property ArrowAreaWidth As Integer
        Get
            Return Me.Height
        End Get
    End Property

    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function

    Private Shared Sub DrawSingleLineText_D2D(rt As ID2D1RenderTarget, text As String, font As Font, foreColor As Color,
                                             rect As RectangleF, dpiScale As Single, endEllipsis As Boolean)
        SingleLineTextBoxRenderer.DrawSingleLineText_D2D(rt, text, font, foreColor, rect, dpiScale, endEllipsis)
    End Sub

    Private Shared Function MeasureWrappedText_D2D(text As String, font As Font, maxWidth As Integer, dpiScale As Single) As Size
        If String.IsNullOrEmpty(text) Then Return Size.Empty
        Using fmt = TextRenderHelper.CreateDWriteTextFormat(font, dpiScale)
            fmt.WordWrapping = Vortice.DirectWrite.WordWrapping.Wrap
            fmt.ParagraphAlignment = Vortice.DirectWrite.ParagraphAlignment.Near
            Using layout = D2DHelper.GetDWriteFactory().CreateTextLayout(text, fmt, maxWidth, Single.MaxValue)
                Dim m = layout.Metrics
                Return New Size(CInt(Math.Ceiling(m.Width)), CInt(Math.Ceiling(m.Height)))
            End Using
        End Using
    End Function

    Private Shared Sub DrawWrappedText_D2D(rt As ID2D1RenderTarget, text As String, font As Font, foreColor As Color,
                                          rect As RectangleF, dpiScale As Single)
        If String.IsNullOrEmpty(text) OrElse foreColor.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Using fmt = TextRenderHelper.CreateDWriteTextFormat(font, dpiScale)
            fmt.WordWrapping = Vortice.DirectWrite.WordWrapping.Wrap
            fmt.ParagraphAlignment = Vortice.DirectWrite.ParagraphAlignment.Near
            Using layout = D2DHelper.GetDWriteFactory().CreateTextLayout(text, fmt, rect.Width, rect.Height)
                Using br = rt.CreateSolidColorBrush(D2DHelper.ToColor4(foreColor))
                    rt.DrawTextLayout(New Vector2(rect.X, rect.Y), layout, br)
                End Using
            End Using
        End Using
    End Sub

    Private Sub NotifyTextChanged()
        Dim matchIndex As Integer = FindItemIndexExact(_text)
        Dim selectedIndexChanged As Boolean = _selectedIndex <> matchIndex
        If selectedIndexChanged Then _selectedIndex = matchIndex
        EnsureCaretVisible()
        Invalidate()
        RaiseEvent TextChanged(Me, EventArgs.Empty)
        If selectedIndexChanged Then RaiseEvent selectedIndexChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub SetSelectedIndexFromUser(value As Integer)
        SetSelectedIndexCore(value, _droppedDown)
    End Sub

    Private Sub SetSelectedIndexCore(value As Integer, deferSelectedIndexChanged As Boolean)
        If value < -1 OrElse value >= _items.Count Then value = -1
        Dim newText As String = If(value >= 0, _items(value), String.Empty)
        Dim selectedIndexChanged As Boolean = _selectedIndex <> value
        Dim textChanged As Boolean = _text <> newText
        If Not selectedIndexChanged AndAlso Not textChanged Then Return

        _selectedIndex = value
        _textRenderer.SetText(newText, newText.Length, True, False)
        Invalidate()
        If textChanged Then RaiseEvent textChanged(Me, EventArgs.Empty)
        If selectedIndexChanged Then RaiseSelectedIndexChanged(deferSelectedIndexChanged)
    End Sub

    Private Sub RaiseSelectedIndexChanged(deferSelectedIndexChanged As Boolean)
        If deferSelectedIndexChanged Then
            _pendingSelectedIndexChanged = True
            Return
        End If
        _pendingSelectedIndexChanged = False
        RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub RaisePendingSelectedIndexChanged()
        If Not _pendingSelectedIndexChanged Then Return
        _pendingSelectedIndexChanged = False
        RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
    End Sub

    Friend Sub OnItemsTextChanged()
        Invalidate()
        RaiseEvent TextChanged(Me, EventArgs.Empty)
    End Sub

    Friend Sub OnSelectedIndexChangedExternal()
        RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub ResetCaretBlink()
        _textRenderer.ResetCaretBlink()
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
        _textRenderer.StartCaretBlink()
    End Sub

    Protected Overrides Sub OnLostFocus(e As EventArgs)
        MyBase.OnLostFocus(e)
        _textRenderer.StopCaretBlink()
    End Sub

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        EnsureCaretVisible()
        Invalidate()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        If _dropDownForm IsNot Nothing AndAlso Not _dropDownForm.IsDisposed Then
            _dropDownForm.RefreshFontResources()
        End If
        MyBase.OnFontChanged(e)
        EnsureCaretVisible()
        D2DHelperV2.RefreshFontDependentRendering(Me)
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
            _textRenderer.StopCaretBlink()
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
