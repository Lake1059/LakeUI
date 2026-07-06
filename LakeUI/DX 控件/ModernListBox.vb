Imports System.ComponentModel
Imports System.Drawing.Drawing2D
Imports System.Numerics
Imports Vortice.Direct2D1
Imports Vortice.DirectWrite

<DefaultEvent("SelectedIndexChanged")>
Public Class ModernListBox
    Implements V3_IGpuRenderable, V3_IGpuInvalidationSource

    Public Event SelectedIndexChanged As EventHandler
    Public Event ItemClick As EventHandler(Of ItemEventArgs)
    Public Event ItemDoubleClick As EventHandler(Of ItemEventArgs)
    Public Event ItemCheckStateChanged As EventHandler(Of ItemEventArgs)
    Public Event ItemOrderChanged As EventHandler

    Public Class ItemEventArgs
        Inherits EventArgs
        Public ReadOnly Property Index As Integer
        Public Sub New(index As Integer)
            Me.Index = index
        End Sub
    End Class

#Region "数据模型"

    Public Enum CheckStateEnum
        Unchecked = 0
        Checked = 1
        Crossed = 2
    End Enum

    Public Enum ToolTipSideEnum
        Left = 0
        Right = 1
    End Enum

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

        <Category("LakeUI"), Description("对应的项文本")>
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

    <TypeConverter(GetType(ExpandableObjectConverter))>
    Public Class IconEntry
        Private _itemText As String = ""
        Private _icon As Image = Nothing

        Public Sub New()
        End Sub

        Public Sub New(itemText As String, icon As Image)
            _itemText = If(itemText, "")
            _icon = icon
        End Sub

        <Category("LakeUI"), Description("对应的项文本")>
        Public Property ItemText As String
            Get
                Return _itemText
            End Get
            Set(value As String)
                _itemText = If(value, "")
            End Set
        End Property

        <Category("LakeUI"), Description("图标")>
        Public Property Icon As Image
            Get
                Return _icon
            End Get
            Set(value As Image)
                _icon = value
            End Set
        End Property

        Public Overrides Function ToString() As String
            If String.IsNullOrEmpty(_itemText) Then Return "(空)"
            Return _itemText
        End Function
    End Class

    Public Class IconEntryCollection
        Inherits ObjectModel.Collection(Of IconEntry)

        Private ReadOnly _dict As New Dictionary(Of String, Image)(StringComparer.Ordinal)

        Public Sub New()
        End Sub

        Public Function TryGetIcon(itemText As String, ByRef icon As Image) As Boolean
            Return _dict.TryGetValue(itemText, icon)
        End Function

        Private Sub RebuildDictionary()
            _dict.Clear()
            For Each entry In Me
                If Not String.IsNullOrEmpty(entry.ItemText) Then
                    _dict(entry.ItemText) = entry.Icon
                End If
            Next
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As IconEntry)
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

        Protected Overrides Sub SetItem(index As Integer, item As IconEntry)
            MyBase.SetItem(index, item)
            RebuildDictionary()
        End Sub
    End Class

#End Region

#Region "集合"

    Public Class ItemCollection
        Inherits ObjectModel.Collection(Of String)

        Private ReadOnly _owner As ModernListBox

        Friend Sub New(owner As ModernListBox)
            _owner = owner
        End Sub

        Private Sub InvalidateOwner()
            If _owner._updateCount <= 0 Then _owner.请求V3渲染()
        End Sub

        Public Overloads Sub AddRange(collection As IEnumerable(Of String))
            _owner.BeginInternalUpdate()
            Try
                For Each s In collection
                    Add(s)
                Next
            Finally
                _owner.EndInternalUpdate(True)
            End Try
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As String)
            MyBase.InsertItem(index, item)
            If _owner._selectedIndex >= 0 AndAlso index <= _owner._selectedIndex Then
                _owner._selectedIndex += 1
            End If
            Dim adjusted As New HashSet(Of Integer)
            For Each i In _owner._selectedIndices
                adjusted.Add(If(i >= index, i + 1, i))
            Next
            _owner._selectedIndices = adjusted
            _owner.调整复选状态索引_插入(index)
            _owner.校正滚动偏移()
            InvalidateOwner()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            MyBase.RemoveItem(index)
            If _owner._selectedIndex = index Then
                _owner._selectedIndex = -1
                _owner.OnSelectionChanged()
            ElseIf _owner._selectedIndex > index Then
                _owner._selectedIndex -= 1
            End If
            _owner._selectedIndices.RemoveWhere(Function(i) i = index)
            Dim adjusted As New HashSet(Of Integer)
            For Each i In _owner._selectedIndices
                adjusted.Add(If(i > index, i - 1, i))
            Next
            _owner._selectedIndices = adjusted
            _owner.调整复选状态索引_移除(index)
            _owner.校正滚动偏移()
            InvalidateOwner()
        End Sub

        Protected Overrides Sub ClearItems()
            MyBase.ClearItems()
            _owner._selectedIndex = -1
            _owner._selectedIndices.Clear()
            _owner._checkStates.Clear()
            _owner._scrollOffset = 0
            InvalidateOwner()
            _owner.OnSelectionChanged()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, item As String)
            MyBase.SetItem(index, item)
            InvalidateOwner()
        End Sub
    End Class

#End Region

#Region "字段"
    Private _backgroundSource As Control = Nothing

    <Category("LakeUI"),
     Description("背景采样源（V3 背景图）。设置后将跨越任意层级直接采样此控件的绘制内容作为透明背景。"),
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

    Private _items As ItemCollection
    Friend _checkStates As New Dictionary(Of Integer, CheckStateEnum)
    Private _selectedIndex As Integer = -1
    Private _selectedIndices As New HashSet(Of Integer)
    Private _selectionAnchor As Integer = -1
    Private _scrollOffset As Integer = 0
    Private ReadOnly _scrollBar As New V3_ScrollBarRenderer()
    Private _hoverIndex As Integer = -1
    Private _itemToolTips As ToolTipEntryCollection
    Private _itemIcons As IconEntryCollection

    ' 悬停动画
    Private ReadOnly _hoverAnim As New V3_AnimationHelper(Me) With {.Duration = 0}
    Private _hoverAnimFromY As Single
    Private _hoverAnimFromH As Single
    Private _hoverAnimToY As Single
    Private _hoverAnimToH As Single
    Private _hoverAnimActive As Boolean = False

    ' 拖选
    Private _mouseDownInContent As Boolean = False
    Private _mouseDownPos As Point
    Private _isDragSelecting As Boolean = False
    Private _dragCurrent As Point
    Private _dragPreSelectedIndices As New HashSet(Of Integer)
    Private Const DragThreshold As Integer = 4

    ' 拖动排序
    Private _isDragReordering As Boolean = False
    Private _dragReorderSourceIndex As Integer = -1
    Private _dragReorderSourceIndices As New List(Of Integer)
    Private _dragReorderInsertIndex As Integer = -1
    Private _mouseDownInDragSelectZone As Boolean = False

    ' 复选框连续拖涂
    Private _isCheckDragging As Boolean = False
    Private _checkDragState As CheckStateEnum = CheckStateEnum.Unchecked
    Private _checkDragSourceIndex As Integer = -1
    Private _checkDragLastIndex As Integer = -1
    Private _checkDragApplied As Boolean = False

    ' 工具提示
    Private _tipForm As FloatingToolTipForm = Nothing
    Private _tipHoverIndex As Integer = -1
    Private _updateCount As Integer = 0
    Private _pendingSelectionChanged As Boolean = False
#End Region

#Region "属性 - 集合"

    <DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
     Editor("System.Windows.Forms.Design.StringCollectionEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing"),
     Category("LakeUI"), Description("列表项集合"), Browsable(True)>
    Public ReadOnly Property Items As ItemCollection
        Get
            Return _items
        End Get
    End Property

    <DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
     Editor("System.ComponentModel.Design.CollectionEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing"),
     Category("LakeUI"), Description("项工具提示映射（ItemText → ToolTipText）"), Browsable(True)>
    Public ReadOnly Property ItemToolTips As ToolTipEntryCollection
        Get
            Return _itemToolTips
        End Get
    End Property

    <DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
     Editor("System.ComponentModel.Design.CollectionEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing"),
     Category("LakeUI"), Description("项图标映射（ItemText → Icon）"), Browsable(True)>
    Public ReadOnly Property ItemIcons As IconEntryCollection
        Get
            Return _itemIcons
        End Get
    End Property

#End Region

#Region "属性 - 选中"

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SelectedIndex As Integer
        Get
            Return _selectedIndex
        End Get
        Set(value As Integer)
            If value < -1 OrElse value >= _items.Count Then value = -1
            If _selectedIndex = value Then Return
            _selectedIndex = value
            _selectedIndices.Clear()
            If _selectedIndex >= 0 Then _selectedIndices.Add(_selectedIndex)
            _selectionAnchor = _selectedIndex
            请求V3渲染()
            RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
        End Set
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property SelectedItem As String
        Get
            If _selectedIndex >= 0 AndAlso _selectedIndex < _items.Count Then
                Return _items(_selectedIndex)
            End If
            Return Nothing
        End Get
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property SelectedIndices As IReadOnlyCollection(Of Integer)
        Get
            Return _selectedIndices
        End Get
    End Property

    <Browsable(False)>
    Public ReadOnly Property SelectedItems As List(Of String)
        Get
            Dim result As New List(Of String)
            For Each idx In _selectedIndices.OrderBy(Function(x) x)
                If idx >= 0 AndAlso idx < _items.Count Then result.Add(_items(idx))
            Next
            Return result
        End Get
    End Property

#End Region

#Region "属性 - 复选框查询"

    Private Function 获取复选状态(index As Integer) As CheckStateEnum
        Dim state As CheckStateEnum = CheckStateEnum.Unchecked
        _checkStates.TryGetValue(index, state)
        Return state
    End Function

    Private Sub 设置复选状态(index As Integer, state As CheckStateEnum)
        If state = CheckStateEnum.Unchecked Then
            _checkStates.Remove(index)
        Else
            _checkStates(index) = state
        End If
    End Sub

    Private Function 下一个复选状态(current As CheckStateEnum) As CheckStateEnum
        Select Case current
            Case CheckStateEnum.Unchecked
                Return CheckStateEnum.Checked
            Case CheckStateEnum.Checked
                Return If(启用叉选, CheckStateEnum.Crossed, CheckStateEnum.Unchecked)
            Case CheckStateEnum.Crossed
                Return CheckStateEnum.Unchecked
            Case Else
                Return CheckStateEnum.Unchecked
        End Select
    End Function

    Public Function GetCheckState(index As Integer) As CheckStateEnum
        Return 获取复选状态(index)
    End Function

    Public Sub SetCheckState(index As Integer, state As CheckStateEnum)
        If index < 0 OrElse index >= _items.Count Then Return
        设置复选状态(index, state)
        请求V3渲染()
        RaiseEvent ItemCheckStateChanged(Me, New ItemEventArgs(index))
    End Sub

    <Browsable(False)>
    Public ReadOnly Property CheckedItems As List(Of String)
        Get
            Return GetItemsByCheckState(CheckStateEnum.Checked)
        End Get
    End Property

    <Browsable(False)>
    Public ReadOnly Property CrossedItems As List(Of String)
        Get
            Return GetItemsByCheckState(CheckStateEnum.Crossed)
        End Get
    End Property

    <Browsable(False)>
    Public ReadOnly Property UncheckedItems As List(Of String)
        Get
            Return GetItemsByCheckState(CheckStateEnum.Unchecked)
        End Get
    End Property

    <Browsable(False)>
    Public ReadOnly Property CheckedIndices As List(Of Integer)
        Get
            Return GetIndicesByCheckState(CheckStateEnum.Checked)
        End Get
    End Property

    <Browsable(False)>
    Public ReadOnly Property CrossedIndices As List(Of Integer)
        Get
            Return GetIndicesByCheckState(CheckStateEnum.Crossed)
        End Get
    End Property

    Public Function GetItemsByCheckState(state As CheckStateEnum) As List(Of String)
        Dim result As New List(Of String)
        For i = 0 To _items.Count - 1
            If 获取复选状态(i) = state Then result.Add(_items(i))
        Next
        Return result
    End Function

    Public Function GetIndicesByCheckState(state As CheckStateEnum) As List(Of Integer)
        Dim result As New List(Of Integer)
        For i = 0 To _items.Count - 1
            If 获取复选状态(i) = state Then result.Add(i)
        Next
        Return result
    End Function

#End Region

#Region "属性 - 背景"

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

#End Region

#Region "属性 - 文本"

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

#End Region

#Region "属性 - 边框"

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

#End Region

#Region "属性 - 项"

    Private 行高 As Integer = 30
    <Category("LakeUI"), Description("行高"), DefaultValue(GetType(Integer), "30"), Browsable(True)>
    Public Property ItemHeight As Integer
        Get
            Return 行高
        End Get
        Set(value As Integer)
            行高 = Math.Max(10, value)
            校正滚动偏移()
            请求V3渲染()
        End Set
    End Property

    Private 项悬停颜色 As Color = Color.FromArgb(60, 60, 60)
    <Category("LakeUI"), Description("项悬停背景颜色"), DefaultValue(GetType(Color), "60,60,60"), Browsable(True)>
    Public Property ItemHoverColor As Color
        Get
            Return 项悬停颜色
        End Get
        Set(value As Color)
            SetValue(项悬停颜色, value)
        End Set
    End Property

    Private 项选中颜色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("项选中背景颜色"), DefaultValue(GetType(Color), "80,80,80"), Browsable(True)>
    Public Property ItemSelectedColor As Color
        Get
            Return 项选中颜色
        End Get
        Set(value As Color)
            SetValue(项选中颜色, value)
        End Set
    End Property

    Private 项左内边距 As Integer = 5
    <Category("LakeUI"), Description("项文本左内边距"), DefaultValue(GetType(Integer), "5"), Browsable(True)>
    Public Property ItemPaddingLeft As Integer
        Get
            Return 项左内边距
        End Get
        Set(value As Integer)
            SetValue(项左内边距, value)
        End Set
    End Property

#End Region

#Region "属性 - 复选框"

    Private 显示复选框 As Boolean = False
    <Category("LakeUI"), Description("是否显示复选框"), DefaultValue(GetType(Boolean), "False"), Browsable(True)>
    Public Property ShowCheckBox As Boolean
        Get
            Return 显示复选框
        End Get
        Set(value As Boolean)
            SetValue(显示复选框, value)
        End Set
    End Property

    Private 复选框大小 As Integer = 20
    <Category("LakeUI"), Description("复选框大小"), DefaultValue(GetType(Integer), "20"), Browsable(True)>
    Public Property CheckBoxSize As Integer
        Get
            Return 复选框大小
        End Get
        Set(value As Integer)
            复选框大小 = Math.Max(8, value)
            请求V3渲染()
        End Set
    End Property

    Private 复选框左边距 As Integer = 5
    <Category("LakeUI"), Description("复选框左边距"), DefaultValue(GetType(Integer), "5"), Browsable(True)>
    Public Property CheckBoxMarginLeft As Integer
        Get
            Return 复选框左边距
        End Get
        Set(value As Integer)
            SetValue(复选框左边距, value)
        End Set
    End Property

    Private 复选框右边距 As Integer = 10
    <Category("LakeUI"), Description("复选框右边距"), DefaultValue(GetType(Integer), "10"), Browsable(True)>
    Public Property CheckBoxMarginRight As Integer
        Get
            Return 复选框右边距
        End Get
        Set(value As Integer)
            SetValue(复选框右边距, value)
        End Set
    End Property

    Private 复选框边框颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("复选框边框颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property CheckBoxBorderColor As Color
        Get
            Return 复选框边框颜色
        End Get
        Set(value As Color)
            SetValue(复选框边框颜色, value)
        End Set
    End Property

    Private 复选框边框宽度 As Integer = 1
    <Category("LakeUI"), Description("复选框边框宽度"), DefaultValue(GetType(Integer), "1"), Browsable(True)>
    Public Property CheckBoxBorderWidth As Integer
        Get
            Return 复选框边框宽度
        End Get
        Set(value As Integer)
            SetValue(复选框边框宽度, value)
        End Set
    End Property

    Private 复选框圆角半径 As Integer = 0
    <Category("LakeUI"), Description("复选框圆角半径"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property CheckBoxBorderRadius As Integer
        Get
            Return 复选框圆角半径
        End Get
        Set(value As Integer)
            SetValue(复选框圆角半径, value)
        End Set
    End Property

    Private 复选框背景颜色 As Color = Color.FromArgb(56, 56, 56)
    <Category("LakeUI"), Description("复选框背景颜色"), DefaultValue(GetType(Color), "56, 56, 56"), Browsable(True)>
    Public Property CheckBoxBackColor As Color
        Get
            Return 复选框背景颜色
        End Get
        Set(value As Color)
            SetValue(复选框背景颜色, value)
        End Set
    End Property

    Private 复选框勾选颜色 As Color = Color.LimeGreen
    <Category("LakeUI"), Description("复选框勾选标记颜色"), DefaultValue(GetType(Color), "LimeGreen"), Browsable(True)>
    Public Property CheckBoxCheckedColor As Color
        Get
            Return 复选框勾选颜色
        End Get
        Set(value As Color)
            SetValue(复选框勾选颜色, value)
        End Set
    End Property

    Private 复选框叉选颜色 As Color = Color.IndianRed
    <Category("LakeUI"), Description("复选框叉选标记颜色"), DefaultValue(GetType(Color), "IndianRed"), Browsable(True)>
    Public Property CheckBoxCrossedColor As Color
        Get
            Return 复选框叉选颜色
        End Get
        Set(value As Color)
            SetValue(复选框叉选颜色, value)
        End Set
    End Property

    Private 复选框标记线宽 As Integer = 2
    <Category("LakeUI"), Description("复选框内部标记线宽"), DefaultValue(GetType(Integer), "2"), Browsable(True)>
    Public Property CheckBoxMarkWidth As Integer
        Get
            Return 复选框标记线宽
        End Get
        Set(value As Integer)
            复选框标记线宽 = Math.Max(1, value)
            请求V3渲染()
        End Set
    End Property

#End Region

#Region "属性 - 图标"

    Private 图标尺寸 As New Size(20, 20)
    <Category("LakeUI"), Description("项图标绘制尺寸"), DefaultValue(GetType(Size), "20, 20"), Browsable(True)>
    Public Property IconSize As Size
        Get
            Return 图标尺寸
        End Get
        Set(value As Size)
            SetValue(图标尺寸, value)
        End Set
    End Property

    Private 图标右边距 As Integer = 10
    <Category("LakeUI"), Description("图标右边距"), DefaultValue(GetType(Integer), "10"), Browsable(True)>
    Public Property IconMarginRight As Integer
        Get
            Return 图标右边距
        End Get
        Set(value As Integer)
            SetValue(图标右边距, value)
        End Set
    End Property

#End Region

#Region "属性 - 滚动条"

    Private 滚动条宽度 As Integer = 10
    <Category("LakeUI"), Description("滚动条宽度"), DefaultValue(GetType(Integer), "10"), Browsable(True)>
    Public Property ScrollBarWidth As Integer
        Get
            Return 滚动条宽度
        End Get
        Set(value As Integer)
            滚动条宽度 = Math.Max(2, value)
            请求V3渲染()
        End Set
    End Property

    Private Shared ReadOnly 默认滚动条颜色 As Color = Color.FromArgb(140, 140, 140)
    Private 滚动条颜色 As Color = 默认滚动条颜色
    <Category("LakeUI"), Description("滚动条滑块颜色"), Browsable(True)>
    Public Property ScrollBarColor As Color
        Get
            Return 滚动条颜色
        End Get
        Set(value As Color)
            SetValue(滚动条颜色, value)
        End Set
    End Property

    Private Function ShouldSerializeScrollBarColor() As Boolean
        Return 滚动条颜色 <> 默认滚动条颜色
    End Function

    Private Sub ResetScrollBarColor()
        ScrollBarColor = 默认滚动条颜色
    End Sub

    Private Shared ReadOnly 默认滚动条悬停颜色 As Color = Color.FromArgb(200, 200, 200)
    Private 滚动条悬停颜色 As Color = 默认滚动条悬停颜色
    <Category("LakeUI"), Description("滚动条滑块悬停/拖拽颜色"), Browsable(True)>
    Public Property ScrollBarHoverColor As Color
        Get
            Return 滚动条悬停颜色
        End Get
        Set(value As Color)
            SetValue(滚动条悬停颜色, value)
        End Set
    End Property

    Private Function ShouldSerializeScrollBarHoverColor() As Boolean
        Return 滚动条悬停颜色 <> 默认滚动条悬停颜色
    End Function

    Private Sub ResetScrollBarHoverColor()
        ScrollBarHoverColor = 默认滚动条悬停颜色
    End Sub

    Private Shared ReadOnly 默认滚动条轨道颜色 As Color = Color.FromArgb(20, 255, 255, 255)
    Private 滚动条轨道颜色 As Color = 默认滚动条轨道颜色
    <Category("LakeUI"), Description("滚动条轨道颜色"), Browsable(True)>
    Public Property ScrollBarTrackColor As Color
        Get
            Return 滚动条轨道颜色
        End Get
        Set(value As Color)
            SetValue(滚动条轨道颜色, value)
        End Set
    End Property

    Private Function ShouldSerializeScrollBarTrackColor() As Boolean
        Return 滚动条轨道颜色 <> 默认滚动条轨道颜色
    End Function

    Private Sub ResetScrollBarTrackColor()
        ScrollBarTrackColor = 默认滚动条轨道颜色
    End Sub

#End Region

#Region "属性 - 工具提示"

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
    <Category("LakeUI"), Description("工具提示与列表框的水平间距（逻辑像素，可为负数）"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property ToolTipGap As Integer
        Get
            Return 提示间距
        End Get
        Set(value As Integer)
            提示间距 = value
        End Set
    End Property

    Private 提示默认侧 As ToolTipSideEnum = ToolTipSideEnum.Right
    <Category("LakeUI"), Description("工具提示默认显示在列表框左侧还是右侧"), DefaultValue(GetType(ToolTipSideEnum), "Right"), Browsable(True)>
    Public Property ToolTipSide As ToolTipSideEnum
        Get
            Return 提示默认侧
        End Get
        Set(value As ToolTipSideEnum)
            提示默认侧 = value
        End Set
    End Property

#End Region

#Region "属性 - 行为"

    Private 允许多选 As Boolean = True
    <Category("LakeUI"), Description("是否允许多选"), DefaultValue(True), Browsable(True)>
    Public Property MultiSelect As Boolean
        Get
            Return 允许多选
        End Get
        Set(value As Boolean)
            If 允许多选 = value Then Return
            允许多选 = value
            If Not value AndAlso _selectedIndices.Count > 1 Then
                Dim first = _selectedIndices.Min()
                _selectedIndices.Clear()
                _selectedIndices.Add(first)
                _selectedIndex = first
                请求V3渲染()
                RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
            End If
        End Set
    End Property

    Private 启用叉选 As Boolean = False
    <Category("LakeUI"), Description("是否启用叉选状态（三态循环），False 时复选框只在勾选和空之间切换"), DefaultValue(False), Browsable(True)>
    Public Property EnableCrossState As Boolean
        Get
            Return 启用叉选
        End Get
        Set(value As Boolean)
            启用叉选 = value
        End Set
    End Property

    Private 拖选区域宽度 As Integer = 30
    <Category("LakeUI"), Description("同时启用多选和拖动排序时，控件右侧强制框选区域的宽度"), DefaultValue(GetType(Integer), "30"), Browsable(True)>
    Public Property DragSelectZoneWidth As Integer
        Get
            Return 拖选区域宽度
        End Get
        Set(value As Integer)
            拖选区域宽度 = Math.Max(0, value)
        End Set
    End Property

    Private 允许拖动排序 As Boolean = False
    <Category("LakeUI"), Description("是否允许拖动排序"), DefaultValue(False), Browsable(True)>
    Public Property AllowDragReorder As Boolean
        Get
            Return 允许拖动排序
        End Get
        Set(value As Boolean)
            允许拖动排序 = value
        End Set
    End Property

    Private 拖动排序指示线颜色 As Color = Color.CornflowerBlue
    <Category("LakeUI"), Description("拖动排序时的插入位置指示线颜色"), DefaultValue(GetType(Color), "CornflowerBlue"), Browsable(True)>
    Public Property DragReorderLineColor As Color
        Get
            Return 拖动排序指示线颜色
        End Get
        Set(value As Color)
            SetValue(拖动排序指示线颜色, value)
        End Set
    End Property

    Private 拖动排序指示线宽 As Integer = 2
    <Category("LakeUI"), Description("拖动排序指示线宽"), DefaultValue(GetType(Integer), "2"), Browsable(True)>
    Public Property DragReorderLineWidth As Integer
        Get
            Return 拖动排序指示线宽
        End Get
        Set(value As Integer)
            SetValue(拖动排序指示线宽, value)
        End Set
    End Property

#End Region

#Region "属性 - 拖选框"

    Private 选框填充颜色 As Color = Color.FromArgb(64, 255, 255, 255)
    <Category("LakeUI"), Description("拖选框填充颜色"), DefaultValue(GetType(Color), "64, 255, 255, 255"), Browsable(True)>
    Public Property SelectionRectFillColor As Color
        Get
            Return 选框填充颜色
        End Get
        Set(value As Color)
            SetValue(选框填充颜色, value)
        End Set
    End Property

    Private 选框边框颜色 As Color = Color.CornflowerBlue
    <Category("LakeUI"), Description("拖选框边框颜色"), DefaultValue(GetType(Color), "CornflowerBlue"), Browsable(True)>
    Public Property SelectionRectBorderColor As Color
        Get
            Return 选框边框颜色
        End Get
        Set(value As Color)
            SetValue(选框边框颜色, value)
        End Set
    End Property

#End Region

#Region "属性 - 动画"

    Private 动画时长 As Integer = 0
    <Category("LakeUI"), Description(GlobalOptions.动画时长描述词), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property AnimationDuration As Integer
        Get
            Return 动画时长
        End Get
        Set(value As Integer)
            动画时长 = Math.Max(0, value)
            _hoverAnim.Duration = 动画时长
        End Set
    End Property

    <Category("LakeUI"), Description(GlobalOptions.动画帧率描述词), DefaultValue(60), Browsable(True)>
    Public Property AnimationFPS As Integer
        Get
            Return _hoverAnim.FPS
        End Get
        Set(value As Integer)
            _hoverAnim.FPS = Math.Max(0, value)
        End Set
    End Property

#End Region

#Region "属性 - SSAA"

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

#End Region

#Region "初始化"

    Public Sub New()
        _items = New ItemCollection(Me)
        _itemToolTips = New ToolTipEntryCollection()
        _itemIcons = New IconEntryCollection()
        InitializeComponent()
        _hoverAnim.DirtyProvider = AddressOf 悬停动画脏区
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.Selectable Or
                 ControlStyles.StandardClick, True)
        UpdateStyles()
    End Sub

    Protected Overrides ReadOnly Property DefaultPadding As Padding
        Get
            Return New Padding(0)
        End Get
    End Property

    Protected Overrides ReadOnly Property DefaultMargin As Padding
        Get
            Return New Padding(0)
        End Get
    End Property

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub

#End Region

#Region "布局计算"

    Private Function 获取边框内边距() As Integer
        Dim s As Single = DpiScale()
        Dim bw As Integer = CInt(Math.Round(边框宽度 * s))
        Dim br As Integer = CInt(Math.Round(边框圆角半径 * s))
        Return Math.Max(bw, If(br > 0, br \ 2, 0))
    End Function

    Private Function 获取内容区域() As Rectangle
        Dim inset As Integer = 获取边框内边距()
        Dim x As Integer = inset + Padding.Left
        Dim y As Integer = inset + Padding.Top
        Dim w As Integer = Width - inset * 2 - Padding.Horizontal
        Dim h As Integer = Height - inset * 2 - Padding.Vertical
        Return New Rectangle(x, y, Math.Max(0, w), Math.Max(0, h))
    End Function

    Private Function 估算可见行数() As Integer
        Dim contentRect = 获取内容区域()
        Dim scaledH As Integer = CInt(Math.Round(行高 * DpiScale()))
        If scaledH <= 0 Then Return 1
        Return Math.Max(1, contentRect.Height \ scaledH)
    End Function

    Private Sub 校正滚动偏移()
        If _items.Count = 0 Then
            _scrollOffset = 0
            Return
        End If
        Dim visCount As Integer = 估算可见行数()
        Dim maxOff As Integer = Math.Max(0, _items.Count - visCount)
        _scrollOffset = Math.Max(0, Math.Min(_scrollOffset, maxOff))
    End Sub

    Private Function 获取项Y坐标(index As Integer) As Integer
        If index < _scrollOffset OrElse index >= _items.Count Then Return -1
        Dim contentRect = 获取内容区域()
        Return contentRect.Y + (index - _scrollOffset) * CInt(Math.Round(行高 * DpiScale()))
    End Function

    Private Function 获取项矩形(index As Integer) As Rectangle
        Dim y As Integer = 获取项Y坐标(index)
        If y < 0 Then Return Rectangle.Empty
        Dim contentRect = 获取内容区域()
        Dim scaledH As Integer = CInt(Math.Round(行高 * DpiScale()))
        Return New Rectangle(contentRect.X, y, contentRect.Width, scaledH)
    End Function

    Private Function 获取复选框区域宽度() As Integer
        If Not 显示复选框 Then Return 0
        Dim s As Single = DpiScale()
        Return CInt(Math.Round(复选框左边距 * s)) + CInt(Math.Round(复选框大小 * s)) + CInt(Math.Round(复选框右边距 * s))
    End Function

    Private Function 是否在拖选区域(mouseX As Integer) As Boolean
        If Not 允许多选 Then Return False
        If Not 允许拖动排序 Then Return True
        Dim contentRect = 获取内容区域()
        Dim inset As Integer = 获取边框内边距()
        Dim scrollW As Integer = If(Not _scrollBar.TrackRect.IsEmpty, Width - inset - _scrollBar.VisualLeft, 0)
        Dim zoneLeft As Integer = contentRect.Right - scrollW - CInt(Math.Round(拖选区域宽度 * DpiScale()))
        Return mouseX >= zoneLeft
    End Function

    Private Function 应该拖选() As Boolean
        If Not 允许多选 Then Return False
        If Not 允许拖动排序 Then Return True
        Return _mouseDownInDragSelectZone
    End Function

#End Region

#Region "绘制"

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        If Width < 1 OrElse Height < 1 Then Return

        Dim w As Integer = ClientRectangle.Width
        Dim h As Integer = ClientRectangle.Height
        Dim hasRadius As Boolean = 边框圆角半径 > 0
        Dim sourceRect As New RectangleF(0, 0, w, h)
        Dim boundsRect As New RectangleF(0, 0, w, h)
        Dim s As Single = DpiScale()
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * s / 2.0F
            boundsRect.Inflate(-half, -half)
        End If
        Dim bc As Color = If(Focused, 有焦点时边框颜色, 边框颜色)

        Dim effBg As Color = 背景颜色

        预计算滚动条布局()

        DrawBackground_GPU(context, hasRadius, sourceRect, boundsRect, bc, effBg)

        Using context.PushClip(获取内容裁剪矩形())
            绘制全部项背景与图标_GPU(context)
            绘制拖选框_GPU(context)
            绘制拖动排序指示线_GPU(context)
            绘制滚动条_GPU(context)
            绘制全部项文本_GPU(context)
        End Using

        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            填充圆角矩形_GPU(context, boundsRect, If(hasRadius, 边框圆角半径 * s, 0.0F), 禁用时遮罩颜色)
        End If
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Private Function 获取内容裁剪矩形() As RectangleF
        Dim inset As Integer = 获取边框内边距()
        Return New RectangleF(inset, inset, Math.Max(0, Width - inset * 2), Math.Max(0, Height - inset * 2))
    End Function

    Private Sub DrawBackground_GPU(context As D3D_PaintContext, hasRadius As Boolean, sourceRect As RectangleF, boundsRect As RectangleF, borderClr As Color, bgClr As Color)
        Dim s As Single = DpiScale()
        Dim radius As Single = If(hasRadius, 边框圆角半径 * s, 0.0F)
        If _backgroundSource IsNot Nothing Then context.DrawBackgroundSource(Me, _backgroundSource, sourceRect)
        填充圆角矩形_GPU(context, boundsRect, radius, bgClr)
        绘制圆角边框_GPU(context, boundsRect, radius, borderClr, 边框宽度 * s)
    End Sub

    Private Sub 绘制滚动条_GPU(context As D3D_PaintContext)
        If _scrollBar.TrackRect.IsEmpty Then Return
        Dim s As Single = DpiScale()
        Dim width As Single = Math.Max(1.0F, 滚动条宽度 * s)
        Dim trackArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.TrackRect.Y, width, _scrollBar.TrackRect.Height)
        Dim thumbArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.ThumbRect.Y, width, _scrollBar.ThumbRect.Height)
        填充圆角矩形_GPU(context, trackArea, Math.Min(width / 2.0F, trackArea.Height / 2.0F), 滚动条轨道颜色)
        Dim thumbColor = If(_scrollBar.IsDragging OrElse _scrollBar.IsHover, 滚动条悬停颜色, 滚动条颜色)
        填充圆角矩形_GPU(context, thumbArea, Math.Min(width / 2.0F, thumbArea.Height / 2.0F), thumbColor)
    End Sub

    Private Sub 绘制全部项背景与图标_GPU(context As D3D_PaintContext)
        If _items.Count = 0 Then Return
        Dim contentRect = 获取内容区域()
        If contentRect.Height <= 0 OrElse contentRect.Width <= 0 Then Return

        Dim s As Single = DpiScale()
        Dim scaledH As Integer = CInt(Math.Round(行高 * s))
        Dim scaledPadL As Integer = CInt(Math.Round(项左内边距 * s))
        Dim scaledCbLeft As Integer = CInt(Math.Round(复选框左边距 * s))
        Dim scaledIconW As Integer = CInt(Math.Round(图标尺寸.Width * s))
        Dim scaledIconH As Integer = CInt(Math.Round(图标尺寸.Height * s))

        Dim inset As Integer = 获取边框内边距()
        Dim scrollW As Integer = If(Not _scrollBar.TrackRect.IsEmpty, Width - inset - _scrollBar.VisualLeft, 0)
        Dim availW As Integer = contentRect.Width - scrollW

        If _hoverAnimActive AndAlso _hoverIndex >= 0 AndAlso Not _selectedIndices.Contains(_hoverIndex) Then
            Dim t As Single = _hoverAnim.Progress
            Dim animY As Single = _hoverAnimFromY + (_hoverAnimToY - _hoverAnimFromY) * t
            Dim animH As Single = _hoverAnimFromH + (_hoverAnimToH - _hoverAnimFromH) * t
            context.FillRectangle(New RectangleF(contentRect.X, animY, availW, animH), 项悬停颜色)
        End If

        Dim visCount As Integer = 估算可见行数()
        For i As Integer = 0 To visCount - 1
            Dim idx As Integer = i + _scrollOffset
            If idx >= _items.Count Then Exit For
            Dim itemY As Integer = contentRect.Y + i * scaledH
            If itemY + scaledH > contentRect.Bottom Then Exit For

            Dim itemRect As New RectangleF(contentRect.X, itemY, availW, scaledH)
            If _selectedIndices.Contains(idx) Then
                context.FillRectangle(itemRect, 项选中颜色)
            ElseIf idx = _hoverIndex AndAlso Not _hoverAnimActive Then
                context.FillRectangle(itemRect, 项悬停颜色)
            End If

            Dim itemText As String = _items(idx)
            Dim textX As Integer = contentRect.X + scaledPadL

            If 显示复选框 Then
                Dim cbX As Integer = contentRect.X + scaledCbLeft
                Dim cbY As Integer = itemY + (scaledH - CInt(Math.Round(复选框大小 * s))) \ 2
                绘制复选框_GPU(context, cbX, cbY, 获取复选状态(idx))
                textX = contentRect.X + 获取复选框区域宽度()
            End If

            Dim icon As Image = Nothing
            If _itemIcons.TryGetIcon(itemText, icon) AndAlso icon IsNot Nothing AndAlso scaledIconW > 0 AndAlso scaledIconH > 0 Then
                Dim iconX As Integer = textX
                Dim iconY As Integer = itemY + (scaledH - scaledIconH) \ 2
                context.DrawImage(icon, New RectangleF(iconX, iconY, scaledIconW, scaledIconH))
            End If
        Next
    End Sub

    Private Sub 绘制全部项文本_GPU(context As D3D_PaintContext)
        If _items.Count = 0 Then Return
        Dim contentRect = 获取内容区域()
        If contentRect.Height <= 0 OrElse contentRect.Width <= 0 Then Return

        Dim s As Single = DpiScale()
        Dim scaledH As Integer = CInt(Math.Round(行高 * s))
        Dim scaledPadL As Integer = CInt(Math.Round(项左内边距 * s))
        Dim scaledIconW As Integer = CInt(Math.Round(图标尺寸.Width * s))
        Dim scaledIconMR As Integer = CInt(Math.Round(图标右边距 * s))
        Dim inset As Integer = 获取边框内边距()
        Dim scrollW As Integer = If(Not _scrollBar.TrackRect.IsEmpty, Width - inset - _scrollBar.VisualLeft, 0)
        Dim availW As Integer = contentRect.Width - scrollW

        Dim visCount As Integer = 估算可见行数()
        For i As Integer = 0 To visCount - 1
            Dim idx As Integer = i + _scrollOffset
            If idx >= _items.Count Then Exit For
            Dim itemY As Integer = contentRect.Y + i * scaledH
            If itemY + scaledH > contentRect.Bottom Then Exit For

            Dim itemText As String = _items(idx)
            Dim textX As Integer = contentRect.X + scaledPadL
            If 显示复选框 Then textX = contentRect.X + 获取复选框区域宽度()

            Dim icon As Image = Nothing
            If _itemIcons.TryGetIcon(itemText, icon) AndAlso icon IsNot Nothing Then
                textX += scaledIconW + scaledIconMR
            End If

            Dim textRight As Integer = contentRect.X + availW - scaledPadL
            Dim textWidth As Integer = textRight - textX
            If textWidth > 0 Then
                context.DrawText(itemText, Font, ForeColor, New RectangleF(textX, itemY, textWidth, scaledH), TextAlignment.Leading, ParagraphAlignment.Center)
            End If
        Next
    End Sub

    Private Sub 绘制复选框_GPU(context As D3D_PaintContext, x As Integer, y As Integer, state As CheckStateEnum)
        Dim s As Single = DpiScale()
        Dim scaledSize As Single = 复选框大小 * s
        Dim scaledRadius As Single = 复选框圆角半径 * s
        Dim scaledBW As Single = 复选框边框宽度 * s
        Dim scaledMarkW As Single = 复选框标记线宽 * s
        Dim rect As New RectangleF(x, y, scaledSize, scaledSize)

        填充圆角矩形_GPU(context, rect, scaledRadius, 复选框背景颜色)
        绘制圆角边框_GPU(context, rect, scaledRadius, 复选框边框颜色, scaledBW)

        Dim inset As Single = scaledSize * 0.2F
        Select Case state
            Case CheckStateEnum.Checked
                Dim br = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, 复选框勾选颜色, context.DeviceGeneration)
                If br IsNot Nothing Then
                    Using geo = D3D_RenderCore.DeviceManager.D2DFactory.CreatePathGeometry()
                        Using sink = geo.Open()
                            sink.BeginFigure(New Vector2(x + inset, y + scaledSize * 0.5F), FigureBegin.Hollow)
                            sink.AddLine(New Vector2(x + scaledSize * 0.4F, y + scaledSize - inset))
                            sink.AddLine(New Vector2(x + scaledSize - inset, y + inset))
                            sink.EndFigure(FigureEnd.Open)
                            sink.Close()
                        End Using
                        context.DeviceContext.DrawGeometry(geo, br, scaledMarkW)
                    End Using
                End If
            Case CheckStateEnum.Crossed
                Dim br = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, 复选框叉选颜色, context.DeviceGeneration)
                If br IsNot Nothing Then
                    context.DeviceContext.DrawLine(New Vector2(x + inset, y + inset), New Vector2(x + scaledSize - inset, y + scaledSize - inset), br, scaledMarkW)
                    context.DeviceContext.DrawLine(New Vector2(x + scaledSize - inset, y + inset), New Vector2(x + inset, y + scaledSize - inset), br, scaledMarkW)
                End If
        End Select
    End Sub

    Private Sub 绘制拖选框_GPU(context As D3D_PaintContext)
        If Not _isDragSelecting Then Return
        Dim rect As Rectangle = 获取拖选矩形()
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim rectF As New RectangleF(rect.X, rect.Y, rect.Width, rect.Height)
        context.FillRectangle(rectF, 选框填充颜色)
        context.DrawRectangle(rectF, 选框边框颜色, 1.0F)
    End Sub

    Private Sub 绘制拖动排序指示线_GPU(context As D3D_PaintContext)
        If Not _isDragReordering OrElse _dragReorderInsertIndex < 0 Then Return
        Dim contentRect = 获取内容区域()
        Dim scaledH As Integer = CInt(Math.Round(行高 * DpiScale()))
        Dim lineY As Integer
        If _dragReorderInsertIndex >= _items.Count Then
            lineY = contentRect.Y + Math.Min(_items.Count - _scrollOffset, 估算可见行数()) * scaledH
        Else
            lineY = 获取项Y坐标(_dragReorderInsertIndex)
            If lineY < 0 Then Return
        End If
        Dim br = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, 拖动排序指示线颜色, context.DeviceGeneration)
        If br IsNot Nothing Then context.DeviceContext.DrawLine(New Vector2(contentRect.X, lineY), New Vector2(contentRect.Right, lineY), br, 拖动排序指示线宽 * DpiScale())
    End Sub

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

    Private Function CreateContentClip_D2D(rt As ID2D1RenderTarget, hasRadius As Boolean, s As Single) As D2DContentClipScope
        Dim inset As Integer = 获取边框内边距()
        Dim clipRect As New RectangleF(inset, inset, Math.Max(0, Width - inset * 2), Math.Max(0, Height - inset * 2))
        Dim radius As Single = If(hasRadius, Math.Max(0, 边框圆角半径 * s - 边框宽度 * s), 0)
        Return New D2DContentClipScope(rt, clipRect, radius)
    End Function

    Private NotInheritable Class D2DContentClipScope
        Implements IDisposable

        Private ReadOnly _rt As ID2D1RenderTarget
        Private ReadOnly _usesLayer As Boolean
        Private ReadOnly _geo As ID2D1Geometry
        Private ReadOnly _active As Boolean

        Public Sub New(rt As ID2D1RenderTarget, clipRect As RectangleF, radius As Single)
            If rt Is Nothing OrElse clipRect.Width <= 0 OrElse clipRect.Height <= 0 Then Return
            _rt = rt
            If radius > 0 Then
                _geo = D3D_RectangleRenderer.创建圆角矩形几何(clipRect, radius)
                D3D_D2DInterop.PushGeometryClip(rt, _geo, clipRect)
                _usesLayer = True
            Else
                rt.PushAxisAlignedClip(New Vortice.RawRectF(clipRect.X, clipRect.Y, clipRect.Right, clipRect.Bottom), AntialiasMode.PerPrimitive)
            End If
            _active = True
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If _active Then
                If _usesLayer Then
                    _rt.PopLayer()
                Else
                    _rt.PopAxisAlignedClip()
                End If
            End If
            If _geo IsNot Nothing Then _geo.Dispose()
        End Sub
    End Class

    Private Sub 预计算滚动条布局()
        If _items.Count = 0 Then
            _scrollBar.ThumbRect = Rectangle.Empty
            _scrollBar.TrackRect = Rectangle.Empty
            _scrollBar.VisualLeft = Width
            Return
        End If
        Dim visCount As Integer = 估算可见行数()
        If _items.Count > visCount OrElse _scrollOffset > 0 Then
            Dim contentRect = 获取内容区域()
            Dim inset As Integer = 获取边框内边距()
            Dim s As Single = DpiScale()
            _scrollBar.ComputeLayout(Width, Height, CInt(Math.Round(边框宽度 * s)), CInt(Math.Round(边框圆角半径 * s)),
                contentRect.Y - inset, Height - contentRect.Bottom, CInt(Math.Round(滚动条宽度 * s)),
                _items.Count, visCount, _scrollOffset)
        Else
            _scrollBar.ThumbRect = Rectangle.Empty
            _scrollBar.TrackRect = Rectangle.Empty
            _scrollBar.VisualLeft = Width
        End If
    End Sub

#End Region

#Region "鼠标处理"

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        更新悬停(-1)
        延迟关闭工具提示(True)
        _scrollBar.ResetHover()
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not Enabled Then Return
        Focus()

        If e.Button = MouseButtons.Left Then
            ' 滚动条拖拽
            If _scrollBar.BeginDrag(e.Location, _scrollOffset) Then Return
            If Not _scrollBar.TrackRect.IsEmpty Then
                Dim visCount = 估算可见行数()
                Dim newOff = _scrollBar.TrackClick(e.Location, _scrollOffset, _items.Count, visCount)
                If newOff <> _scrollOffset Then
                    _scrollOffset = newOff
                    请求V3渲染()
                    Return
                End If
            End If

            ' 复选框点击检测
            Dim hitIdx As Integer = 命中测试(e.Y)
            If 显示复选框 AndAlso hitIdx >= 0 Then
                Dim contentRect = 获取内容区域()
                Dim _s As Single = DpiScale()
                Dim cbLeft As Integer = contentRect.X + CInt(Math.Round(复选框左边距 * _s))
                Dim cbRight As Integer = cbLeft + CInt(Math.Round(复选框大小 * _s))
                If e.X >= cbLeft AndAlso e.X <= cbRight Then
                    _isCheckDragging = True
                    _checkDragState = 获取复选状态(hitIdx)
                    _checkDragSourceIndex = hitIdx
                    _checkDragLastIndex = hitIdx
                    _checkDragApplied = False
                    Return
                End If
            End If

            _mouseDownPos = e.Location
            _mouseDownInContent = True
            _isDragSelecting = False
            _isDragReordering = False
            _dragReorderSourceIndex = hitIdx
            _mouseDownInDragSelectZone = 是否在拖选区域(e.X)
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)

        ' 复选框连续拖涂
        If _isCheckDragging Then
            Dim hitIdx = 命中测试(e.Y)
            If hitIdx >= 0 AndAlso hitIdx <> _checkDragLastIndex AndAlso 显示复选框 Then
                Dim contentRect = 获取内容区域()
                Dim _s As Single = DpiScale()
                Dim cbLeft As Integer = contentRect.X + CInt(Math.Round(复选框左边距 * _s))
                Dim cbRight As Integer = cbLeft + CInt(Math.Round(复选框大小 * _s))
                If e.X >= cbLeft AndAlso e.X <= cbRight Then
                    _checkDragApplied = True
                    If 获取复选状态(hitIdx) <> _checkDragState Then
                        设置复选状态(hitIdx, _checkDragState)
                        RaiseEvent ItemCheckStateChanged(Me, New ItemEventArgs(hitIdx))
                    End If
                    _checkDragLastIndex = hitIdx
                    请求V3渲染()
                End If
            End If
            Return
        End If

        If _scrollBar.IsDragging Then
            关闭工具提示()
            Dim visCount = 估算可见行数()
            _scrollOffset = _scrollBar.DragMove(e.Y, _items.Count, visCount)
            Dim hitIdx = 命中测试(e.Y)
            更新悬停(hitIdx)
            请求V3渲染()
            Return
        End If

        ' 拖动排序
        If _isDragReordering Then
            关闭工具提示()
            Dim insertIdx = 计算拖动排序插入位置(e.Y)
            _dragReorderInsertIndex = insertIdx
            请求V3渲染()
            Return
        End If

        ' 拖选/拖排序检测
        If e.Button = MouseButtons.Left AndAlso _mouseDownInContent Then
            If Not _isDragSelecting AndAlso Not _isDragReordering Then
                If Math.Abs(e.X - _mouseDownPos.X) > DragThreshold OrElse Math.Abs(e.Y - _mouseDownPos.Y) > DragThreshold Then
                    If 应该拖选() Then
                        _isDragSelecting = True
                        _dragPreSelectedIndices = New HashSet(Of Integer)(_selectedIndices)
                    ElseIf 允许拖动排序 AndAlso _dragReorderSourceIndex >= 0 Then
                        _isDragReordering = True
                        If _selectedIndices.Contains(_dragReorderSourceIndex) AndAlso _selectedIndices.Count > 1 Then
                            _dragReorderSourceIndices = _selectedIndices.OrderBy(Function(x) x).ToList()
                        Else
                            _dragReorderSourceIndices = New List(Of Integer) From {_dragReorderSourceIndex}
                        End If
                        _dragReorderInsertIndex = _dragReorderSourceIndex
                        请求V3渲染()
                        Return
                    ElseIf 允许多选 Then
                        _isDragSelecting = True
                        _dragPreSelectedIndices = New HashSet(Of Integer)(_selectedIndices)
                    End If
                End If
            End If
            If _isDragSelecting Then
                关闭工具提示()
                _dragCurrent = e.Location
                更新拖选(e)
                请求V3渲染()
                Return
            End If
        End If

        If _scrollBar.UpdateHover(e.Location) Then 请求V3渲染()

        Dim hitRow As Integer = 命中测试(e.Y)
        更新悬停(hitRow)
        If hitRow >= 0 Then
            更新工具提示(hitRow)
        Else
            延迟关闭工具提示()
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)

        ' 结束复选框拖涂
        If _isCheckDragging Then
            If Not _checkDragApplied AndAlso _checkDragSourceIndex >= 0 AndAlso _checkDragSourceIndex < _items.Count Then
                设置复选状态(_checkDragSourceIndex, 下一个复选状态(_checkDragState))
                请求V3渲染()
                RaiseEvent ItemCheckStateChanged(Me, New ItemEventArgs(_checkDragSourceIndex))
            End If
            _isCheckDragging = False
            _checkDragSourceIndex = -1
            _checkDragLastIndex = -1
            _checkDragApplied = False
            Return
        End If

        ' 完成拖动排序
        If _isDragReordering Then
            _isDragReordering = False
            执行多项拖动排序()
            _dragReorderSourceIndex = -1
            _dragReorderSourceIndices.Clear()
            _dragReorderInsertIndex = -1
            _mouseDownInContent = False
            请求V3渲染()
            Return
        End If

        If _isDragSelecting Then
            _isDragSelecting = False
            _mouseDownInContent = False
            _scrollBar.EndDrag()
            请求V3渲染()
            Return
        End If

        If _mouseDownInContent Then
            _mouseDownInContent = False
            Dim hitIdx As Integer = 命中测试(e.Y)
            处理点击选择(hitIdx, e)
        End If

        _scrollBar.EndDrag()
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnMouseDoubleClick(e As MouseEventArgs)
        MyBase.OnMouseDoubleClick(e)
        Dim hitIdx = 命中测试(e.Y)
        If hitIdx >= 0 Then
            If 显示复选框 Then
                设置复选状态(hitIdx, 下一个复选状态(获取复选状态(hitIdx)))
                请求V3渲染()
                RaiseEvent ItemCheckStateChanged(Me, New ItemEventArgs(hitIdx))
            End If
            RaiseEvent ItemDoubleClick(Me, New ItemEventArgs(hitIdx))
        End If
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        关闭工具提示()
        If _items.Count = 0 Then Return
        Dim visCount = 估算可见行数()
        Dim newOff = V3_ScrollBarRenderer.HandleWheel(e.Delta, _scrollOffset, _items.Count, visCount, 3)
        If newOff <> _scrollOffset Then
            _scrollOffset = newOff
            Dim hitIdx = 命中测试(e.Y)
            更新悬停(hitIdx)
            请求V3渲染()
        End If
    End Sub

#End Region

#Region "命中测试"

    Private Function 命中测试(mouseY As Integer) As Integer
        Dim contentRect = 获取内容区域()
        If mouseY < contentRect.Y OrElse mouseY >= contentRect.Bottom Then Return -1
        Dim relY As Integer = mouseY - contentRect.Y
        Dim scaledH As Integer = CInt(Math.Round(行高 * DpiScale()))
        If scaledH <= 0 Then Return -1
        Dim idx As Integer = relY \ scaledH + _scrollOffset
        If idx < 0 OrElse idx >= _items.Count Then Return -1
        Return idx
    End Function

    Private Function 获取拖选矩形()
        Dim x1 As Integer = Math.Min(_mouseDownPos.X, _dragCurrent.X)
        Dim y1 As Integer = Math.Min(_mouseDownPos.Y, _dragCurrent.Y)
        Dim x2 As Integer = Math.Max(_mouseDownPos.X, _dragCurrent.X)
        Dim y2 As Integer = Math.Max(_mouseDownPos.Y, _dragCurrent.Y)
        Return New Rectangle(x1, y1, x2 - x1, y2 - y1)
    End Function

    Private Function 计算拖动排序插入位置(mouseY As Integer) As Integer
        Dim contentRect = 获取内容区域()
        Dim relY As Integer = mouseY - contentRect.Y
        Dim scaledH As Integer = CInt(Math.Round(行高 * DpiScale()))
        If scaledH <= 0 Then Return 0
        Dim rawSlot As Integer = CInt(Math.Round(relY / scaledH)) + _scrollOffset
        Return Math.Max(0, Math.Min(rawSlot, _items.Count))
    End Function

    Private Sub 执行多项拖动排序()
        If _dragReorderSourceIndices.Count = 0 OrElse _dragReorderInsertIndex < 0 Then Return

        BeginInternalUpdate()
        Try
            Dim sortedSrc = _dragReorderSourceIndices.OrderBy(Function(x) x).ToList()

            Dim insertBefore As Integer = _dragReorderInsertIndex

            Dim countBefore As Integer = sortedSrc.Where(Function(i) i < insertBefore).Count()
            Dim targetSlot As Integer = insertBefore - countBefore

            Dim movedTexts As New List(Of String)
            Dim movedStates As New List(Of CheckStateEnum)
            For Each idx In sortedSrc
                movedTexts.Add(_items(idx))
                movedStates.Add(获取复选状态(idx))
            Next

            For i = sortedSrc.Count - 1 To 0 Step -1
                _items.RemoveAt(sortedSrc(i))
            Next

            targetSlot = Math.Max(0, Math.Min(targetSlot, _items.Count))

            For i = 0 To movedTexts.Count - 1
                _items.Insert(targetSlot + i, movedTexts(i))
                设置复选状态(targetSlot + i, movedStates(i))
            Next

            _selectedIndices.Clear()
            For i = 0 To movedTexts.Count - 1
                _selectedIndices.Add(targetSlot + i)
            Next
            _selectedIndex = targetSlot
            _selectionAnchor = targetSlot

            RaiseEvent ItemOrderChanged(Me, EventArgs.Empty)
            _pendingSelectionChanged = True
        Finally
            EndInternalUpdate(True)
        End Try
    End Sub

#End Region

#Region "选择逻辑"

    Private Sub 处理点击选择(hitIdx As Integer, e As MouseEventArgs)
        Dim ctrlHeld As Boolean = (Control.ModifierKeys And Keys.Control) = Keys.Control
        Dim shiftHeld As Boolean = (Control.ModifierKeys And Keys.Shift) = Keys.Shift

        If hitIdx < 0 OrElse hitIdx >= _items.Count Then
            If Not ctrlHeld AndAlso Not shiftHeld Then
                设置选中集合(Enumerable.Empty(Of Integer))
            End If
            Return
        End If

        If 允许多选 AndAlso shiftHeld AndAlso _selectionAnchor >= 0 Then
            Dim minI = Math.Min(_selectionAnchor, hitIdx)
            Dim range = Enumerable.Range(minI, Math.Max(_selectionAnchor, hitIdx) - minI + 1)
            If ctrlHeld Then
                Dim combined As New HashSet(Of Integer)(_selectedIndices)
                combined.UnionWith(range)
                设置选中集合(combined)
            Else
                设置选中集合(range)
            End If
        ElseIf 允许多选 AndAlso ctrlHeld Then
            Dim newSet As New HashSet(Of Integer)(_selectedIndices)
            If Not newSet.Remove(hitIdx) Then
                newSet.Add(hitIdx)
            End If
            _selectionAnchor = hitIdx
            设置选中集合(newSet)
        Else
            _selectionAnchor = hitIdx
            设置选中集合({hitIdx})
        End If

        RaiseEvent ItemClick(Me, New ItemEventArgs(hitIdx))
    End Sub

    Private Sub 设置选中集合(indices As IEnumerable(Of Integer))
        Dim newSet As New HashSet(Of Integer)(indices)
        If newSet.Count = _selectedIndices.Count AndAlso newSet.SetEquals(_selectedIndices) Then Return
        _selectedIndices = newSet
        _selectedIndex = If(_selectedIndices.Count > 0, _selectedIndices.Min(), -1)
        请求V3渲染()
        RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub 更新拖选(e As MouseEventArgs)
        If Not 允许多选 Then
            Dim hitIdx = 命中测试(e.Y)
            If hitIdx >= 0 Then
                _selectionAnchor = hitIdx
                设置选中集合({hitIdx})
            End If
            Return
        End If

        Dim dragRect As Rectangle = 获取拖选矩形()
        Dim ctrlHeld As Boolean = (Control.ModifierKeys And Keys.Control) = Keys.Control
        Dim hitIndices = 命中测试矩形(dragRect)

        If ctrlHeld Then
            Dim newSet As New HashSet(Of Integer)(_dragPreSelectedIndices)
            For Each idx In hitIndices
                If _dragPreSelectedIndices.Contains(idx) Then
                    newSet.Remove(idx)
                Else
                    newSet.Add(idx)
                End If
            Next
            设置选中集合(newSet)
        Else
            设置选中集合(hitIndices)
        End If
    End Sub

    Private Function 命中测试矩形(dragRect As Rectangle) As List(Of Integer)
        Dim result As New List(Of Integer)
        Dim contentRect = 获取内容区域()
        Dim scaledH As Integer = CInt(Math.Round(行高 * DpiScale()))
        Dim visCount = 估算可见行数()
        For i As Integer = 0 To visCount - 1
            Dim idx As Integer = i + _scrollOffset
            If idx >= _items.Count Then Exit For
            Dim itemY As Integer = contentRect.Y + i * scaledH
            Dim itemRect As New Rectangle(contentRect.X, itemY, contentRect.Width, scaledH)
            If dragRect.IntersectsWith(itemRect) Then
                result.Add(idx)
            End If
        Next
        Return result
    End Function

#End Region

#Region "悬停动画"

    Private Sub 更新悬停(newIndex As Integer)
        If newIndex = _hoverIndex Then Return
        Dim oldIndex As Integer = _hoverIndex
        _hoverIndex = newIndex

        If 动画时长 <= 0 OrElse Not IsHandleCreated Then
            _hoverAnimActive = False
            请求V3渲染()
            Return
        End If

        If newIndex < 0 Then
            _hoverAnimActive = False
            请求V3渲染()
            Return
        End If

        Dim newY = 获取项Y坐标(newIndex)
        If newY < 0 Then
            _hoverAnimActive = False
            请求V3渲染()
            Return
        End If

        If oldIndex >= 0 AndAlso _hoverAnimActive Then
            Dim t As Single = _hoverAnim.Progress
            _hoverAnimFromY += (_hoverAnimToY - _hoverAnimFromY) * t
            _hoverAnimFromH += (_hoverAnimToH - _hoverAnimFromH) * t
        Else
            _hoverAnimFromY = newY
            _hoverAnimFromH = CInt(Math.Round(行高 * DpiScale()))
        End If

        _hoverAnimToY = newY
        _hoverAnimToH = CInt(Math.Round(行高 * DpiScale()))
        _hoverAnimActive = True
        _hoverAnim.Duration = 动画时长
        _hoverAnim.SetImmediate(0)
        _hoverAnim.AnimateTo(1)
    End Sub

    Private Sub 悬停动画脏区(helper As V3_AnimationHelper, owner As Control, sink As V3_AnimationHelper.InvalidateRegionSink)
        If Not _hoverAnimActive Then
            sink.SuppressInvalidate()
            Return
        End If

        Dim dirty = 获取悬停动画脏区()
        If dirty.Width > 0 AndAlso dirty.Height > 0 Then
            sink.Add(dirty)
        Else
            sink.InvalidateAll()
        End If
    End Sub

    Private Function 获取悬停动画脏区() As Rectangle
        Dim contentRect = 获取内容区域()
        If contentRect.Width <= 0 OrElse contentRect.Height <= 0 Then Return ClientRectangle

        Dim inset As Integer = 获取边框内边距()
        Dim scrollW As Integer = If(Not _scrollBar.TrackRect.IsEmpty, Width - inset - _scrollBar.VisualLeft, 0)
        Dim availW As Integer = Math.Max(0, contentRect.Width - scrollW)
        If availW <= 0 Then Return Rectangle.Empty

        Dim top As Integer = CInt(Math.Floor(Math.Min(_hoverAnimFromY, _hoverAnimToY)))
        Dim bottom As Integer = CInt(Math.Ceiling(Math.Max(_hoverAnimFromY + _hoverAnimFromH, _hoverAnimToY + _hoverAnimToH)))
        Dim rect As New Rectangle(contentRect.X, top, availW, Math.Max(0, bottom - top))
        rect.Inflate(2, 2)
        Return Rectangle.Intersect(ClientRectangle, rect)
    End Function

#End Region

#Region "键盘导航"

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        Select Case keyData
            Case Keys.Up, Keys.Down, Keys.PageUp, Keys.PageDown, Keys.Home, Keys.End,
                 Keys.Up Or Keys.Shift, Keys.Down Or Keys.Shift,
                 Keys.PageUp Or Keys.Shift, Keys.PageDown Or Keys.Shift,
                 Keys.Home Or Keys.Shift, Keys.End Or Keys.Shift,
                 Keys.A Or Keys.Control, Keys.Space
                OnKeyDown(New KeyEventArgs(keyData))
                Return True
        End Select
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If _items.Count = 0 Then Return
        Dim shiftHeld = (e.Modifiers And Keys.Shift) = Keys.Shift

        Select Case e.KeyCode
            Case Keys.A
                If (e.Modifiers And Keys.Control) = Keys.Control Then
                    SelectAll()
                    e.Handled = True
                End If
            Case Keys.Space
                If 显示复选框 AndAlso _selectedIndex >= 0 AndAlso _selectedIndex < _items.Count Then
                    设置复选状态(_selectedIndex, 下一个复选状态(获取复选状态(_selectedIndex)))
                    请求V3渲染()
                    RaiseEvent ItemCheckStateChanged(Me, New ItemEventArgs(_selectedIndex))
                    e.Handled = True
                End If
            Case Keys.Up
                键盘导航(-1, shiftHeld)
                e.Handled = True
            Case Keys.Down
                键盘导航(1, shiftHeld)
                e.Handled = True
            Case Keys.PageUp
                键盘导航(-估算可见行数(), shiftHeld)
                e.Handled = True
            Case Keys.PageDown
                键盘导航(估算可见行数(), shiftHeld)
                e.Handled = True
            Case Keys.Home
                键盘导航至边缘(True, shiftHeld)
                e.Handled = True
            Case Keys.End
                键盘导航至边缘(False, shiftHeld)
                e.Handled = True
        End Select
    End Sub

    Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
        Select Case keyData And Keys.KeyCode
            Case Keys.Left, Keys.Right, Keys.Up, Keys.Down,
                 Keys.Home, Keys.End, Keys.PageUp, Keys.PageDown,
                 Keys.Space
                Return True
        End Select
        Return MyBase.IsInputKey(keyData)
    End Function

    Private Shared ReadOnly int32Array As Integer() = {0}
    Private Sub 键盘导航(delta As Integer, shiftHeld As Boolean)
        Dim currentIdx = If(_selectionAnchor >= 0 AndAlso _selectionAnchor < _items.Count, _selectionAnchor, -1)
        If currentIdx < 0 Then
            If _items.Count > 0 Then
                _selectionAnchor = 0
                设置选中集合(int32Array)
                EnsureVisible(0)
            End If
            Return
        End If

        Dim idx As Integer = Math.Max(0, Math.Min(_items.Count - 1, currentIdx + delta))
        If idx = currentIdx Then Return

        If shiftHeld AndAlso 允许多选 Then
            Dim minI = Math.Min(_selectionAnchor, idx)
            设置选中集合(Enumerable.Range(minI, Math.Max(_selectionAnchor, idx) - minI + 1))
        Else
            _selectionAnchor = idx
            设置选中集合({idx})
        End If
        EnsureVisible(idx)
    End Sub

    Private Sub 键盘导航至边缘(toStart As Boolean, shiftHeld As Boolean)
        If _items.Count = 0 Then Return
        Dim targetIdx As Integer = If(toStart, 0, _items.Count - 1)

        If shiftHeld AndAlso 允许多选 AndAlso _selectionAnchor >= 0 Then
            Dim minI = Math.Min(_selectionAnchor, targetIdx)
            设置选中集合(Enumerable.Range(minI, Math.Max(_selectionAnchor, targetIdx) - minI + 1))
        Else
            _selectionAnchor = targetIdx
            设置选中集合({targetIdx})
        End If
        EnsureVisible(targetIdx)
    End Sub

#End Region

#Region "工具提示"

    Private _tipSourceScreenRect As Rectangle = Rectangle.Empty

    Private Sub 更新工具提示(hitIdx As Integer)
        If hitIdx = _tipHoverIndex AndAlso
           _tipForm IsNot Nothing AndAlso
           Not _tipForm.IsDisposed AndAlso
           _tipForm.Visible Then Return
        _tipHoverIndex = hitIdx

        If hitIdx >= 0 AndAlso hitIdx < _items.Count Then
            Dim itemText As String = _items(hitIdx)
            Dim tipText As String = Nothing
            If _itemToolTips.TryGetToolTip(itemText, tipText) AndAlso Not String.IsNullOrEmpty(tipText) Then
                If _tipForm Is Nothing OrElse _tipForm.IsDisposed Then
                    _tipForm = New FloatingToolTipForm(Me)
                End If
                Dim itemRect As Rectangle = 获取项矩形(hitIdx)
                If itemRect.IsEmpty Then
                    关闭工具提示()
                    Return
                End If
                _tipSourceScreenRect = RectangleToScreen(itemRect)
                Dim gap As Integer = ScaledToolTipGap()
                Dim screenPt As Point = Me.PointToScreen(New Point(Me.Width + gap, itemRect.Y))
                Dim preferredSide As FloatingToolTipSide = If(提示默认侧 = ToolTipSideEnum.Left,
                                                              FloatingToolTipSide.Left,
                                                              FloatingToolTipSide.Right)
                _tipForm.ShowTip(tipText, screenPt, CreateToolTipStyle(),
                                 Math.Max(0, Me.Width + gap * 2),
                                 preferredSide)
                Return
            End If
        End If
        关闭工具提示()
    End Sub

    Private Sub 延迟关闭工具提示(Optional keepOwnerBounds As Boolean = False)
        If _tipForm Is Nothing OrElse _tipForm.IsDisposed Then Return
        If keepOwnerBounds Then
            _tipForm.ScheduleCloseIfPointerOutside(180, RectangleToScreen(ClientRectangle))
        ElseIf Not _tipSourceScreenRect.IsEmpty Then
            _tipForm.ScheduleCloseIfPointerOutside(180, _tipSourceScreenRect)
        Else
            _tipForm.ScheduleCloseIfPointerOutside(180)
        End If
    End Sub

    Private Sub 关闭工具提示()
        _tipHoverIndex = -1
        _tipSourceScreenRect = Rectangle.Empty
        If _tipForm IsNot Nothing AndAlso Not _tipForm.IsDisposed Then
            _tipForm.Close()
            _tipForm.Dispose()
        End If
        _tipForm = Nothing
    End Sub

    Private Function CreateToolTipStyle() As FloatingToolTipStyle
        Return New FloatingToolTipStyle With {
            .Font = Me.Font,
            .BackColor = 提示背景颜色,
            .ForeColor = 提示文本颜色,
            .BorderColor = 提示边框颜色,
            .BorderSize = Math.Max(0, 提示边框宽度),
            .BorderRadius = Math.Max(0, 提示圆角半径),
            .Padding = 提示内边距,
            .MaxWidth = Math.Max(50, 提示最大宽度)
        }
    End Function

    Private Function ScaledToolTipGap() As Integer
        Return CInt(Math.Round(提示间距 * DpiScale(), MidpointRounding.AwayFromZero))
    End Function

#End Region

#Region "公共方法"

    Public Sub EnsureVisible(index As Integer)
        If index < 0 OrElse index >= _items.Count Then Return
        Dim visCount = 估算可见行数()
        If index < _scrollOffset Then
            _scrollOffset = index
            请求V3渲染()
        ElseIf index >= _scrollOffset + visCount Then
            _scrollOffset = index - visCount + 1
            校正滚动偏移()
            请求V3渲染()
        End If
    End Sub

    Public Sub SelectAll()
        If Not 允许多选 Then Return
        设置选中集合(Enumerable.Range(0, _items.Count))
    End Sub

    Public Sub ClearSelection()
        设置选中集合(Enumerable.Empty(Of Integer))
    End Sub

    Public Sub SetAllCheckState(state As CheckStateEnum)
        _checkStates.Clear()
        If state <> CheckStateEnum.Unchecked Then
            For i = 0 To _items.Count - 1
                _checkStates(i) = state
            Next
        End If
        请求V3渲染()
    End Sub

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

    Public Function GetItemAt(x As Integer, y As Integer) As String
        Dim idx = 命中测试(y)
        If idx < 0 OrElse idx >= _items.Count Then Return Nothing
        Return _items(idx)
    End Function

    Public Function HitTest(x As Integer, y As Integer) As Integer
        Return 命中测试(y)
    End Function

#End Region

#Region "事件"

    Protected Overrides Sub OnGotFocus(e As EventArgs)
        MyBase.OnGotFocus(e)
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnLostFocus(e As EventArgs)
        MyBase.OnLostFocus(e)
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        校正滚动偏移()
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        关闭工具提示()
        MyBase.OnFontChanged(e)
        校正滚动偏移()
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        If Not Enabled Then
            关闭工具提示()
        End If
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        校正滚动偏移()
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        校正滚动偏移()
        请求V3渲染()
    End Sub

#End Region

#Region "辅助"

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

    Private Sub BeginInternalUpdate()
        _updateCount += 1
    End Sub

    Private Sub EndInternalUpdate(Optional invalidateAfter As Boolean = True)
        _updateCount -= 1
        If _updateCount > 0 Then Return
        _updateCount = 0
        校正滚动偏移()
        If _pendingSelectionChanged Then
            _pendingSelectionChanged = False
            RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
        End If
        If invalidateAfter Then 请求V3渲染()
    End Sub

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            If _updateCount <= 0 Then 请求V3渲染()
        End If
    End Sub

    Friend Sub OnSelectionChanged()
        If _updateCount > 0 Then
            _pendingSelectionChanged = True
            Return
        End If
        请求V3渲染()
        RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub 调整复选状态索引_插入(insertedIndex As Integer)
        Dim adjusted As New Dictionary(Of Integer, CheckStateEnum)
        For Each kvp In _checkStates
            If kvp.Key >= insertedIndex Then
                adjusted(kvp.Key + 1) = kvp.Value
            Else
                adjusted(kvp.Key) = kvp.Value
            End If
        Next
        _checkStates = adjusted
    End Sub

    Private Sub 调整复选状态索引_移除(removedIndex As Integer)
        Dim adjusted As New Dictionary(Of Integer, CheckStateEnum)
        For Each kvp In _checkStates
            If kvp.Key = removedIndex Then
                Continue For
            ElseIf kvp.Key > removedIndex Then
                adjusted(kvp.Key - 1) = kvp.Value
            Else
                adjusted(kvp.Key) = kvp.Value
            End If
        Next
        _checkStates = adjusted
    End Sub

    Friend Sub 释放资源()
        _hoverAnim?.Dispose()
        关闭工具提示()
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
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property MaximumSize As Size
        Get
            Return Nothing
        End Get
        Set(value As Size)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property MinimumSize As Size
        Get
            Return Nothing
        End Get
        Set(value As Size)
        End Set
    End Property
#End Region

End Class
