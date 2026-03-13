Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Drawing.Design
Imports System.Drawing.Drawing2D

<DefaultEvent("SelectedIndexChanged")>
Public Class UltraDetailListView

    Public Event SelectedIndexChanged As EventHandler
    Public Event ItemClick As EventHandler(Of ListItemEventArgs)
    Public Event ItemDoubleClick As EventHandler(Of ListItemEventArgs)
    Public Event ItemOrderChanged As EventHandler
    Public Event AfterLabelEdit As EventHandler(Of LabelEditEventArgs)

    Public Class ListItemEventArgs
        Inherits EventArgs
        Public ReadOnly Property Item As ListItem
        Public ReadOnly Property DisplayRowIndex As Integer
        Public Sub New(item As ListItem, displayRowIndex As Integer)
            Me.Item = item
            Me.DisplayRowIndex = displayRowIndex
        End Sub
    End Class

    Public Class LabelEditEventArgs
        Inherits EventArgs
        Public ReadOnly Property Item As ListItem
        Public ReadOnly Property DisplayRowIndex As Integer
        Public ReadOnly Property ColumnIndex As Integer
        ''' <summary>编辑前的原始文本。</summary>
        Public ReadOnly Property OldLabel As String
        ''' <summary>编辑后的新文本；事件处理程序可修改此值以替换最终写入的文本。</summary>
        Public Property Label As String
        Public Property CancelEdit As Boolean = False
        Public Sub New(item As ListItem, displayRowIndex As Integer, columnIndex As Integer, oldLabel As String, label As String)
            Me.Item = item
            Me.DisplayRowIndex = displayRowIndex
            Me.ColumnIndex = columnIndex
            Me.OldLabel = oldLabel
            Me.Label = label
        End Sub
    End Class

#Region "数据模型"

    ''' <summary>附加文本行，可独立设置字体和颜色。</summary>
    <TypeConverter(GetType(ExpandableObjectConverter))>
    Public Class TextLine
        <DefaultValue(""), Description("文本内容")>
        Public Property Text As String = ""

        <Description("字体，留空时使用控件默认字体")>
        Public Property Font As Font = Nothing

        <DefaultValue(GetType(Color), ""), Description("前景色，留空时使用控件默认项文本颜色")>
        Public Property ForeColor As Color = Color.Empty

        Public Sub New()
        End Sub
        Public Sub New(text As String)
            Me.Text = text
        End Sub
        Public Sub New(text As String, font As Font, foreColor As Color)
            Me.Text = text
            Me.Font = font
            Me.ForeColor = foreColor
        End Sub

        Private Function ShouldSerializeFont() As Boolean
            Return Font IsNot Nothing
        End Function
        Private Sub ResetFont()
            Font = Nothing
        End Sub

        Public Overrides Function ToString() As String
            Return If(String.IsNullOrEmpty(Text), "(空)", Text)
        End Function
    End Class

    ''' <summary>子项 (对应一列的单元格)，与原版 ListViewSubItem 结构一致；
    ''' 通过 ExtraLines 添加同一单元格内的附加文本行。</summary>
    <TypeConverter(GetType(ExpandableObjectConverter))>
    Public Class ListSubItem
        <DefaultValue(""), Description("主文本")>
        Public Property Text As String = ""

        <Description("字体，留空时使用控件默认字体")>
        Public Property Font As Font = Nothing

        <DefaultValue(GetType(Color), ""), Description("前景色，留空时使用控件默认项文本颜色")>
        Public Property ForeColor As Color = Color.Empty

        ''' <summary>同一单元格内主文本下方的附加文本行。</summary>
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
         Editor(GetType(Design.CollectionEditor), GetType(UITypeEditor)),
         Description("附加文本行")>
        Public Property ExtraLines As New List(Of TextLine)

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Tag As Object = Nothing

        Public Sub New()
        End Sub
        Public Sub New(text As String)
            Me.Text = text
        End Sub
        Public Sub New(text As String, font As Font, foreColor As Color)
            Me.Text = text
            Me.Font = font
            Me.ForeColor = foreColor
        End Sub
        Private Function ShouldSerializeFont() As Boolean
            Return Font IsNot Nothing
        End Function
        Private Sub ResetFont()
            Font = Nothing
        End Sub
        Private Function ShouldSerializeExtraLines() As Boolean
            Return ExtraLines.Count > 0
        End Function

        Public Overrides Function ToString() As String
            Return If(String.IsNullOrEmpty(Text), "(空)", Text)
        End Function
    End Class

    ''' <summary>项 (对应一行)，与原版 ListViewItem 结构一致；
    ''' 通过 Group 属性指定所属分组。</summary>
    <TypeConverter(GetType(ExpandableObjectConverter))>
    Public Class ListItem
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
         Editor(GetType(Design.CollectionEditor), GetType(UITypeEditor)),
         Description("子项集合")>
        Public Property SubItems As New List(Of ListSubItem)

        ''' <summary>所属分组名称，空字符串表示无分组。</summary>
        <DefaultValue(""),
         Description("所属分组的名称，留空表示无分组")>
        Public Property GroupName As String = ""

        <DefaultValue(GetType(Image), ""), Description("项图标")>
        Public Property Icon As Image = Nothing

        <DefaultValue(False), Description("是否高亮（绘制高亮边框）")>
        Public Property Checked As Boolean = False

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Tag As Object = Nothing

        ''' <summary>底部附加文本行，无视列宽与图标，以整行宽度显示在所有子项内容下方。</summary>
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
         Editor(GetType(Design.CollectionEditor), GetType(UITypeEditor)),
         Description("底部附加文本行，以整行宽度显示在所有内容下方")>
        Public Property BottomLines As New List(Of TextLine)

        Friend CachedHeight As Integer = -1

        Public Sub New()
        End Sub
        Public Sub New(ParamArray subItems() As ListSubItem)
            Me.SubItems.AddRange(subItems)
        End Sub
        Public Sub InvalidateCache()
            CachedHeight = -1
        End Sub

        Private Function ShouldSerializeIcon() As Boolean
            Return Icon IsNot Nothing
        End Function
        Private Sub ResetIcon()
            Icon = Nothing
        End Sub
        Private Function ShouldSerializeBottomLines() As Boolean
            Return BottomLines.Count > 0
        End Function

        Public Overrides Function ToString() As String
            If SubItems.Count = 0 Then Return "(空项)"
            Return SubItems(0).ToString()
        End Function
    End Class

    ''' <summary>分组定义。项通过 ListItem.GroupName 引用此对象的 Name。</summary>
    <TypeConverter(GetType(ExpandableObjectConverter))>
    Public Class ListGroup
        <DefaultValue(""), Description("分组名称，用于项的 GroupName 匹配")>
        Public Property Name As String = ""

        <DefaultValue(""), Description("分组显示文本")>
        Public Property Text As String = ""

        <DefaultValue(False), Description("是否折叠")>
        Public Property IsCollapsed As Boolean = False

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Tag As Object = Nothing

        Public Sub New()
        End Sub
        Public Sub New(name As String, Optional text As String = "")
            Me.Name = name
            Me.Text = If(String.IsNullOrEmpty(text), name, text)
        End Sub

        Public Overrides Function ToString() As String
            If String.IsNullOrEmpty(Name) AndAlso String.IsNullOrEmpty(Text) Then Return "(未命名分组)"
            If String.IsNullOrEmpty(Text) Then Return Name
            If String.IsNullOrEmpty(Name) Then Return Text
            Return $"{Name} - {Text}"
        End Function
    End Class

    ''' <summary>列定义。</summary>
    <TypeConverter(GetType(ExpandableObjectConverter))>
    Public Class ListColumn
        <DefaultValue(""), Description("列标题文本")>
        Public Property Text As String = ""

        <DefaultValue(100), Description("列宽度")>
        Public Property Width As Integer = 100

        <DefaultValue(GetType(Padding), "10, 0, 0, 0"), Description("列标题文字内边距")>
        Public Property HeaderPadding As Padding = New Padding(10, 0, 0, 0)

        <DefaultValue(False), Description("是否允许慢速单击编辑此列的子项主文本")>
        Public Property AllowLabelEdit As Boolean = False

        Public Sub New()
        End Sub
        Public Sub New(text As String, Optional width As Integer = 100)
            Me.Text = text
            Me.Width = width
        End Sub

        Public Overrides Function ToString() As String
            Return If(String.IsNullOrEmpty(Text), "(未命名列)", $"{Text} ({Width})")
        End Function
    End Class

#End Region

#Region "集合类型"

    Public Class ListColumnCollection
        Inherits Collection(Of ListColumn)

        Private ReadOnly _owner As UltraDetailListView

        Friend Sub New(owner As UltraDetailListView)
            _owner = owner
        End Sub

        Public Overloads Sub AddRange(collection As IEnumerable(Of ListColumn))
            For Each entry In collection
                Add(entry)
            Next
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As ListColumn)
            MyBase.InsertItem(index, item)
            _owner.全部项高度缓存失效()
            _owner.RefreshItems()
        End Sub
        Protected Overrides Sub RemoveItem(index As Integer)
            MyBase.RemoveItem(index)
            _owner.全部项高度缓存失效()
            _owner.RefreshItems()
        End Sub
        Protected Overrides Sub SetItem(index As Integer, item As ListColumn)
            MyBase.SetItem(index, item)
            _owner.全部项高度缓存失效()
            _owner.RefreshItems()
        End Sub
        Protected Overrides Sub ClearItems()
            MyBase.ClearItems()
            _owner.全部项高度缓存失效()
            _owner.RefreshItems()
        End Sub
    End Class

    Public Class ListGroupCollection
        Inherits Collection(Of ListGroup)

        Private ReadOnly _owner As UltraDetailListView

        Friend Sub New(owner As UltraDetailListView)
            _owner = owner
        End Sub

        Public Overloads Sub AddRange(collection As IEnumerable(Of ListGroup))
            For Each entry In collection
                Add(entry)
            Next
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As ListGroup)
            MyBase.InsertItem(index, item)
            _owner.RefreshItems()
        End Sub
        Protected Overrides Sub RemoveItem(index As Integer)
            MyBase.RemoveItem(index)
            _owner.RefreshItems()
        End Sub
        Protected Overrides Sub SetItem(index As Integer, item As ListGroup)
            MyBase.SetItem(index, item)
            _owner.RefreshItems()
        End Sub
        Protected Overrides Sub ClearItems()
            MyBase.ClearItems()
            _owner.RefreshItems()
        End Sub
    End Class

    Public Class ListItemCollection
        Inherits Collection(Of ListItem)

        Private ReadOnly _owner As UltraDetailListView

        Friend Sub New(owner As UltraDetailListView)
            _owner = owner
        End Sub

        Public Overloads Sub AddRange(collection As IEnumerable(Of ListItem))
            For Each entry In collection
                Add(entry)
            Next
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As ListItem)
            MyBase.InsertItem(index, item)
            _owner.RefreshItems()
        End Sub
        Protected Overrides Sub RemoveItem(index As Integer)
            MyBase.RemoveItem(index)
            _owner.RefreshItems()
        End Sub
        Protected Overrides Sub SetItem(index As Integer, item As ListItem)
            MyBase.SetItem(index, item)
            _owner.RefreshItems()
        End Sub
        Protected Overrides Sub ClearItems()
            MyBase.ClearItems()
            _owner.RefreshItems()
        End Sub
    End Class

#End Region

#Region "显示行"

    Private Enum DisplayRowType
        GroupHeader
        Item
    End Enum

    Private Class DisplayRow
        Public Type As DisplayRowType
        Public Group As ListGroup
        Public Item As ListItem
        Public Height As Integer
    End Class

    Private _displayRows As New List(Of DisplayRow)

    Private Sub 重建显示列表()
        取消标签编辑等待()
        If _editTextBox IsNot Nothing Then 结束标签编辑(True)

        ' 记录当前选中的项引用，用于重建后重新映射
        Dim prevSelectedItems As New List(Of ListItem)
        Dim prevAnchorItem As ListItem = Nothing
        For Each idx In _selectedIndices
            If idx >= 0 AndAlso idx < _displayRows.Count AndAlso _displayRows(idx).Type = DisplayRowType.Item Then
                prevSelectedItems.Add(_displayRows(idx).Item)
            End If
        Next
        If _selectionAnchor >= 0 AndAlso _selectionAnchor < _displayRows.Count AndAlso
           _displayRows(_selectionAnchor).Type = DisplayRowType.Item Then
            prevAnchorItem = _displayRows(_selectionAnchor).Item
        End If

        _displayRows.Clear()

        ' 无分组的项
        For Each itm In _items
            If String.IsNullOrEmpty(itm.GroupName) Then
                _displayRows.Add(New DisplayRow With {
                    .Type = DisplayRowType.Item,
                    .Item = itm,
                    .Height = 计算项高度(itm)
                })
            End If
        Next

        ' 按分组输出
        For Each grp In _groups
            _displayRows.Add(New DisplayRow With {
                .Type = DisplayRowType.GroupHeader,
                .Group = grp,
                .Height = 分组标题高度
            })
            If Not grp.IsCollapsed Then
                For Each itm In _items
                    If String.Equals(itm.GroupName, grp.Name, StringComparison.Ordinal) Then
                        _displayRows.Add(New DisplayRow With {
                            .Type = DisplayRowType.Item,
                            .Item = itm,
                            .Height = 计算项高度(itm)
                        })
                    End If
                Next
            End If
        Next

        ' 重新映射选中索引
        _selectedIndices.Clear()
        If prevSelectedItems.Count > 0 Then
            For i = 0 To _displayRows.Count - 1
                If _displayRows(i).Type = DisplayRowType.Item AndAlso prevSelectedItems.Contains(_displayRows(i).Item) Then
                    _selectedIndices.Add(i)
                End If
            Next
        End If
        If prevAnchorItem IsNot Nothing Then
            _selectionAnchor = -1
            For i = 0 To _displayRows.Count - 1
                If _displayRows(i).Type = DisplayRowType.Item AndAlso _displayRows(i).Item Is prevAnchorItem Then
                    _selectionAnchor = i
                    Exit For
                End If
            Next
        End If

        校正滚动偏移()
        校正横向滚动偏移()
        _hoverAnimActive = False
        _hoverRowIndex = -1
    End Sub

    Private Sub 校正滚动偏移()
        If _displayRows.Count = 0 Then
            _scrollOffset = 0
            Return
        End If
        Dim maxOff As Integer = Math.Max(0, _displayRows.Count - 1)
        _scrollOffset = Math.Max(0, Math.Min(_scrollOffset, maxOff))
    End Sub

#End Region

#Region "集合属性"

    Private ReadOnly _columns As New ListColumnCollection(Me)
    Private ReadOnly _groups As New ListGroupCollection(Me)
    Private ReadOnly _items As New ListItemCollection(Me)

    <Category("LakeUI"), Description("列定义集合"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
     Editor(GetType(Design.CollectionEditor), GetType(UITypeEditor))>
    Public ReadOnly Property Columns As ListColumnCollection
        Get
            Return _columns
        End Get
    End Property

    <Category("LakeUI"), Description("分组集合"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
     Editor(GetType(Design.CollectionEditor), GetType(UITypeEditor))>
    Public ReadOnly Property Groups As ListGroupCollection
        Get
            Return _groups
        End Get
    End Property

    <Category("LakeUI"), Description("项集合"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
     Editor(GetType(Design.CollectionEditor), GetType(UITypeEditor))>
    Public ReadOnly Property Items As ListItemCollection
        Get
            Return _items
        End Get
    End Property

    ''' <summary>重建显示列表并刷新控件。修改数据后调用此方法。</summary>
    Public Sub RefreshItems()
        If _updateCount > 0 Then Return
        重建显示列表()
        Me.Invalidate()
    End Sub

#End Region

#Region "辅助方法"

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            Me.Invalidate()
        End If
    End Sub

    Private Sub SetValueWithRebuild(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            重建显示列表()
            Me.Invalidate()
        End If
    End Sub

    Private Sub SetValueWithFullRebuild(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            全部项高度缓存失效()
            重建显示列表()
            Me.Invalidate()
        End If
    End Sub

    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function

    Private Function 获取文本格式标志() As TextFormatFlags
        Dim flags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding
        Return If(自动换行, flags Or TextFormatFlags.WordBreak, flags Or TextFormatFlags.EndEllipsis)
    End Function

    Private Function 获取图标区域宽度(item As ListItem) As Integer
        If item.Icon IsNot Nothing AndAlso 图标尺寸.Width > 0 AndAlso 图标尺寸.Height > 0 Then
            Return 图标尺寸.Width + 图标间距
        End If
        Return 0
    End Function

    Private Function 计算底部文本行高度(item As ListItem, rowWidth As Integer) As Integer
        If item.BottomLines.Count = 0 Then Return 0
        Dim blW As Integer = rowWidth - 项内边距.Horizontal
        Dim flags As TextFormatFlags = 获取文本格式标志()
        Dim proposed As New Size(Math.Max(1, blW), Integer.MaxValue)
        Dim h As Integer = 底部文本行间距
        For Each bl In item.BottomLines
            h += 文本行间距
            h += TextRenderer.MeasureText(If(String.IsNullOrEmpty(bl.Text), "Ag", bl.Text), If(bl.Font, Me.Font), proposed, flags).Height
        Next
        Return h
    End Function

    Private Function 构建项范围(startIdx As Integer, endIdx As Integer) As List(Of Integer)
        Dim range As New List(Of Integer)
        For i = startIdx To endIdx
            If i >= 0 AndAlso i < _displayRows.Count AndAlso _displayRows(i).Type = DisplayRowType.Item Then
                range.Add(i)
            End If
        Next
        Return range
    End Function

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

#Region "外观属性 - 背景"

    Private 背景颜色 As Color = Color.FromArgb(36, 36, 36)
    <Category("LakeUI"), Description("背景颜色"), DefaultValue(GetType(Color), "36, 36, 36"), Browsable(True)>
    Public Property BackgroundColor As Color
        Get
            Return 背景颜色
        End Get
        Set(value As Color)
            SetValue(背景颜色, value)
        End Set
    End Property

#End Region

#Region "外观属性 - 列标题"

    Private 列标题可见 As Boolean = True
    <Category("LakeUI"), Description("是否显示列标题"), DefaultValue(True), Browsable(True)>
    Public Property HeaderVisible As Boolean
        Get
            Return 列标题可见
        End Get
        Set(value As Boolean)
            SetValue(列标题可见, value)
        End Set
    End Property

    Private 列标题高度 As Integer = 30
    <Category("LakeUI"), Description("列标题高度"), DefaultValue(GetType(Integer), "30"), Browsable(True)>
    Public Property HeaderHeight As Integer
        Get
            Return 列标题高度
        End Get
        Set(value As Integer)
            SetValue(列标题高度, value)
        End Set
    End Property

    Private 列标题背景颜色 As Color = Color.FromArgb(30, 30, 30)
    <Category("LakeUI"), Description("列标题背景颜色"), DefaultValue(GetType(Color), "30, 30, 30"), Browsable(True)>
    Public Property HeaderBackColor As Color
        Get
            Return 列标题背景颜色
        End Get
        Set(value As Color)
            SetValue(列标题背景颜色, value)
        End Set
    End Property

    Private 列标题文字颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("列标题文字颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property HeaderForeColor As Color
        Get
            Return 列标题文字颜色
        End Get
        Set(value As Color)
            SetValue(列标题文字颜色, value)
        End Set
    End Property

    Private 列标题分隔线颜色 As Color = Color.FromArgb(60, 60, 60)
    <Category("LakeUI"), Description("列标题分隔线颜色"), DefaultValue(GetType(Color), "60, 60, 60"), Browsable(True)>
    Public Property HeaderBorderColor As Color
        Get
            Return 列标题分隔线颜色
        End Get
        Set(value As Color)
            SetValue(列标题分隔线颜色, value)
        End Set
    End Property

    Private 列标题分隔线宽度 As Integer = 1
    <Category("LakeUI"), Description("列标题分隔线宽度"), DefaultValue(GetType(Integer), "1"), Browsable(True)>
    Public Property HeaderBorderWidth As Integer
        Get
            Return 列标题分隔线宽度
        End Get
        Set(value As Integer)
            SetValue(列标题分隔线宽度, value)
        End Set
    End Property

    Private 允许调整列宽 As Boolean = True
    <Category("LakeUI"), Description("是否允许用户拖动调整列宽"), DefaultValue(True), Browsable(True)>
    Public Property AllowColumnResize As Boolean
        Get
            Return 允许调整列宽
        End Get
        Set(value As Boolean)
            SetValue(允许调整列宽, value)
        End Set
    End Property

#End Region

#Region "外观属性 - 项"

    Private 项文本颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("项默认文本颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property ItemForeColor As Color
        Get
            Return 项文本颜色
        End Get
        Set(value As Color)
            SetValue(项文本颜色, value)
        End Set
    End Property

    Private 项悬停背景颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("项鼠标悬停背景颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property ItemHoverBackColor As Color
        Get
            Return 项悬停背景颜色
        End Get
        Set(value As Color)
            SetValue(项悬停背景颜色, value)
        End Set
    End Property

    Private 项选中背景颜色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("项选中背景颜色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
    Public Property ItemSelectedBackColor As Color
        Get
            Return 项选中背景颜色
        End Get
        Set(value As Color)
            SetValue(项选中背景颜色, value)
        End Set
    End Property

    Private 项内边距 As New Padding(5)
    <Category("LakeUI"), Description("项内部边距"), Browsable(True)>
    Public Property ItemPadding As Padding
        Get
            Return 项内边距
        End Get
        Set(value As Padding)
            SetValueWithFullRebuild(项内边距, value)
        End Set
    End Property

    Private Function ShouldSerializeItemPadding() As Boolean
        Return 项内边距 <> New Padding(5)
    End Function

    Private 文本行间距 As Integer = 0
    <Category("LakeUI"), Description("子项内文本行间距"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property TextLineSpacing As Integer
        Get
            Return 文本行间距
        End Get
        Set(value As Integer)
            SetValueWithFullRebuild(文本行间距, value)
        End Set
    End Property

    Private 自动换行 As Boolean = True
    <Category("LakeUI"), Description("是否自动换行"), DefaultValue(True), Browsable(True)>
    Public Property WordWrap As Boolean
        Get
            Return 自动换行
        End Get
        Set(value As Boolean)
            SetValueWithFullRebuild(自动换行, value)
        End Set
    End Property

    Private 底部文本行间距 As Integer = 0
    <Category("LakeUI"), Description("BottomLines与主体内容之间的距离"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property BottomLinesSpacing As Integer
        Get
            Return 底部文本行间距
        End Get
        Set(value As Integer)
            SetValueWithFullRebuild(底部文本行间距, value)
        End Set
    End Property

    Private 项间距 As Integer = 0
    <Category("LakeUI"), Description("项之间的间距"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property ItemSpacing As Integer
        Get
            Return 项间距
        End Get
        Set(value As Integer)
            SetValue(项间距, value)
        End Set
    End Property

    Private 内容边距 As New Padding(0, 10, 0, 10)
    <Category("LakeUI"), Description("项列表区域的内边距（仅Top和Bottom生效）。值为-1时自动使用ItemPadding对应方向的值"), DefaultValue(GetType(Padding), "0, 10, 0, 10"), Browsable(True)>
    Public Property ContentPadding As Padding
        Get
            Return 内容边距
        End Get
        Set(value As Padding)
            If 内容边距 <> value Then
                内容边距 = value
                Me.Invalidate()
            End If
        End Set
    End Property

    Private Function 获取有效内容上边距() As Integer
        Return If(内容边距.Top < 0, 项内边距.Top, 内容边距.Top)
    End Function

    Private Function 获取有效内容下边距() As Integer
        Return If(内容边距.Bottom < 0, 项内边距.Bottom, 内容边距.Bottom)
    End Function

    Private Function 获取行前间距(rowIndex As Integer) As Integer
        If rowIndex <= _scrollOffset Then Return 0
        Dim spacing = 项间距
        If rowIndex > 0 AndAlso rowIndex < _displayRows.Count Then
            Dim prevRow = _displayRows(rowIndex - 1)
            Dim currRow = _displayRows(rowIndex)
            If prevRow.Type = DisplayRowType.GroupHeader AndAlso currRow.Type = DisplayRowType.Item Then
                spacing += 获取有效内容上边距()
            ElseIf prevRow.Type = DisplayRowType.Item AndAlso currRow.Type = DisplayRowType.GroupHeader Then
                spacing += 获取有效内容下边距()
            End If
        End If
        Return spacing
    End Function

    Private 图标尺寸 As New Size(32, 32)
    <Category("LakeUI"), Description("项图标绘制尺寸"), Browsable(True)>
    Public Property IconSize As Size
        Get
            Return 图标尺寸
        End Get
        Set(value As Size)
            SetValueWithFullRebuild(图标尺寸, value)
        End Set
    End Property

    Private Function ShouldSerializeIconSize() As Boolean
        Return 图标尺寸 <> New Size(32, 32)
    End Function
    Private Sub ResetIconSize()
        IconSize = New Size(32, 32)
    End Sub

    Private 图标间距 As Integer = 5
    <Category("LakeUI"), Description("项图标与文本之间的间距"), DefaultValue(GetType(Integer), "5"), Browsable(True)>
    Public Property IconSpacing As Integer
        Get
            Return 图标间距
        End Get
        Set(value As Integer)
            SetValueWithFullRebuild(图标间距, value)
        End Set
    End Property

#End Region

#Region "外观属性 - 项高亮边框"

    Private 项高亮边框颜色 As Color = Color.CornflowerBlue
    <Category("LakeUI"), Description("Checked=True时的高亮边框颜色"), DefaultValue(GetType(Color), "CornflowerBlue"), Browsable(True)>
    Public Property CheckedBorderColor As Color
        Get
            Return 项高亮边框颜色
        End Get
        Set(value As Color)
            SetValue(项高亮边框颜色, value)
        End Set
    End Property

    Private 项高亮边框宽度 As Integer = 1
    <Category("LakeUI"), Description("Checked=True时的高亮边框宽度"), DefaultValue(GetType(Integer), "1"), Browsable(True)>
    Public Property CheckedBorderWidth As Integer
        Get
            Return 项高亮边框宽度
        End Get
        Set(value As Integer)
            SetValue(项高亮边框宽度, value)
        End Set
    End Property

#End Region

#Region "外观属性 - 分组"

    Private 分组标题高度 As Integer = 30
    <Category("LakeUI"), Description("分组标题高度"), DefaultValue(GetType(Integer), "30"), Browsable(True)>
    Public Property GroupHeight As Integer
        Get
            Return 分组标题高度
        End Get
        Set(value As Integer)
            SetValueWithRebuild(分组标题高度, value)
        End Set
    End Property

    Private 分组背景颜色 As Color = Color.FromArgb(25, 25, 25)
    <Category("LakeUI"), Description("分组标题背景颜色"), DefaultValue(GetType(Color), "25, 25, 25"), Browsable(True)>
    Public Property GroupBackColor As Color
        Get
            Return 分组背景颜色
        End Get
        Set(value As Color)
            SetValue(分组背景颜色, value)
        End Set
    End Property

    Private 分组文字颜色 As Color = Color.CornflowerBlue
    <Category("LakeUI"), Description("分组标题文字颜色"), DefaultValue(GetType(Color), "CornflowerBlue"), Browsable(True)>
    Public Property GroupForeColor As Color
        Get
            Return 分组文字颜色
        End Get
        Set(value As Color)
            SetValue(分组文字颜色, value)
        End Set
    End Property

    Private 分组分隔线颜色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("分组标题底部分隔线颜色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
    Public Property GroupBorderColor As Color
        Get
            Return 分组分隔线颜色
        End Get
        Set(value As Color)
            SetValue(分组分隔线颜色, value)
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

#Region "外观属性 - 更多指示器"

    Private 更多指示器高度 As Integer = 20
    <Category("LakeUI"), Description("更多内容指示器高度"), DefaultValue(GetType(Integer), "20"), Browsable(True)>
    Public Property MoreIndicatorHeight As Integer
        Get
            Return 更多指示器高度
        End Get
        Set(value As Integer)
            SetValue(更多指示器高度, value)
        End Set
    End Property

    Private 更多指示器颜色 As Color = Color.FromArgb(120, 120, 120)
    <Category("LakeUI"), Description("更多内容指示器文字颜色"), DefaultValue(GetType(Color), "120, 120, 120"), Browsable(True)>
    Public Property MoreIndicatorColor As Color
        Get
            Return 更多指示器颜色
        End Get
        Set(value As Color)
            SetValue(更多指示器颜色, value)
        End Set
    End Property

#End Region

#Region "外观属性 - 拖选框"

    Private 选框填充颜色 As Color = Color.FromArgb(64, 255, 255, 255)
    <Category("LakeUI"), Description("拖选框填充颜色（半透明）"), DefaultValue(GetType(Color), "64, 255, 255, 255"), Browsable(True)>
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

#Region "外观属性 - 拖动排序"

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

#Region "外观属性 - 动画"

    Private 动画时长 As Integer = 0
    <Category("LakeUI"), Description(Class1.动画时长描述词), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property AnimationDuration As Integer
        Get
            Return 动画时长
        End Get
        Set(value As Integer)
            动画时长 = Math.Max(0, value)
            _hoverAnim.Duration = 动画时长
        End Set
    End Property

    <Category("LakeUI"), Description(Class1.动画帧率描述词), DefaultValue(60), Browsable(True)>
    Public Property AnimationFPS As Integer
        Get
            Return _hoverAnim.FPS
        End Get
        Set(value As Integer)
            _hoverAnim.FPS = Math.Max(0, value)
        End Set
    End Property

#End Region

#Region "外观属性 - SSAA"

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

#End Region

#Region "行为属性"

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
                Me.Invalidate()
                RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
            End If
        End Set
    End Property

    Private 允许拖动排序 As Boolean = False
    <Category("LakeUI"), Description("是否允许拖动排序（仅在同一分组内）"), DefaultValue(False), Browsable(True)>
    Public Property AllowDragReorder As Boolean
        Get
            Return 允许拖动排序
        End Get
        Set(value As Boolean)
            允许拖动排序 = value
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

#End Region

#Region "内部状态"

    Private _scrollOffset As Integer = 0
    Private ReadOnly _scrollBar As New ScrollBarRenderer()
    Private _hScrollOffset As Integer = 0
    Private ReadOnly _hScrollBar As New ScrollBarRenderer()
    Private ReadOnly _selectedIndices As New HashSet(Of Integer)
    Private _selectionAnchor As Integer = -1
    Private _hoverRowIndex As Integer = -1

    Private _columnResizeIndex As Integer = -1
    Private _columnResizeStartX As Integer = 0
    Private _columnResizeStartWidth As Integer = 0
    Private Const ColumnResizeHitZone As Integer = 4

    Private ReadOnly _hoverAnim As New AnimationHelper(Me) With {.Duration = 150}
    Private _hoverAnimFromY As Single
    Private _hoverAnimFromH As Single
    Private _hoverAnimToY As Single
    Private _hoverAnimToH As Single
    Private _hoverAnimActive As Boolean = False

    ' 拖选状态
    Private _mouseDownInContent As Boolean = False
    Private _mouseDownPos As Point
    Private _isDragSelecting As Boolean = False
    Private _dragCurrent As Point
    Private _dragPreSelectedIndices As New HashSet(Of Integer)
    Private Const DragThreshold As Integer = 4
    Private _updateCount As Integer = 0

    ' 拖动排序状态
    Private _isDragReordering As Boolean = False
    Private _dragReorderSourceIndex As Integer = -1
    Private _dragReorderSourceIndices As New List(Of Integer)
    Private _dragReorderInsertIndex As Integer = -1
    Private _mouseDownInDragSelectZone As Boolean = False

    ' 标签编辑状态
    Private _labelEditTimer As Timer
    Private _labelEditPendingIndex As Integer = -1
    Private _editTextBox As ModernTextBox = Nothing
    Private _editRowIndex As Integer = -1
    Private _editColumnIndex As Integer = -1
    Private _labelEditPendingColumn As Integer = -1

    Private _truncTooltip As ToolTip
    Private _truncTooltipText As String = ""
    Private _truncTooltipActive As Boolean = False

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SelectedIndex As Integer
        Get
            If _selectedIndices.Count = 0 Then Return -1
            Return _selectedIndices.Min()
        End Get
        Set(value As Integer)
            _selectedIndices.Clear()
            If value >= 0 AndAlso value < _displayRows.Count Then
                _selectedIndices.Add(value)
            End If
            _selectionAnchor = value
            Me.Invalidate()
            RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
        End Set
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property SelectedIndices As IReadOnlyCollection(Of Integer)
        Get
            Return _selectedIndices
        End Get
    End Property

    <Browsable(False)>
    Public ReadOnly Property SelectedItem As ListItem
        Get
            Dim idx = SelectedIndex
            If idx < 0 OrElse idx >= _displayRows.Count Then Return Nothing
            Dim row = _displayRows(idx)
            If row.Type = DisplayRowType.Item Then Return row.Item
            Return Nothing
        End Get
    End Property

    <Browsable(False)>
    Public ReadOnly Property SelectedItems As List(Of ListItem)
        Get
            Dim result As New List(Of ListItem)
            For Each idx In _selectedIndices.OrderBy(Function(x) x)
                If idx >= 0 AndAlso idx < _displayRows.Count Then
                    Dim row = _displayRows(idx)
                    If row.Type = DisplayRowType.Item Then result.Add(row.Item)
                End If
            Next
            Return result
        End Get
    End Property

    <Browsable(False)>
    Public ReadOnly Property CheckedItems As List(Of ListItem)
        Get
            Dim result As New List(Of ListItem)
            For Each itm In _items
                If itm.Checked Then result.Add(itm)
            Next
            Return result
        End Get
    End Property

    <Browsable(False)>
    Public ReadOnly Property TopItem As ListItem
        Get
            For i = _scrollOffset To _displayRows.Count - 1
                If _displayRows(i).Type = DisplayRowType.Item Then Return _displayRows(i).Item
            Next
            Return Nothing
        End Get
    End Property

    Private Sub 设置选中集合(indices As IEnumerable(Of Integer))
        Dim changed = False
        Dim newSet As New HashSet(Of Integer)(indices)
        If newSet.Count <> _selectedIndices.Count OrElse Not newSet.SetEquals(_selectedIndices) Then
            _selectedIndices.Clear()
            For Each idx In newSet
                _selectedIndices.Add(idx)
            Next
            changed = True
        End If
        If changed Then
            Me.Invalidate()
            RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
        End If
    End Sub

#End Region

#Region "高度计算"

    Private Function 计算项高度(item As ListItem) As Integer
        If item.CachedHeight >= 0 Then Return item.CachedHeight
        Dim iconAreaW As Integer = 获取图标区域宽度(item)
        Dim maxSubH As Integer = 0
        For i As Integer = 0 To item.SubItems.Count - 1
            Dim sub_ = item.SubItems(i)
            Dim colW As Integer
            If _columns.Count > 0 AndAlso i < _columns.Count Then
                colW = _columns(i).Width
            Else
                colW = 获取内容区域().Width
            End If
            Dim availW As Integer = colW - 项内边距.Horizontal
            If i = 0 Then availW -= iconAreaW
            Dim subH As Integer = 计算子项内容高度(sub_, availW)
            If subH > maxSubH Then maxSubH = subH
        Next
        If maxSubH = 0 Then
            maxSubH = TextRenderer.MeasureText("Ag", Me.Font).Height
        End If
        If iconAreaW > 0 AndAlso 图标尺寸.Height > maxSubH Then maxSubH = 图标尺寸.Height
        maxSubH += 计算底部文本行高度(item, If(_columns.Count > 0, 获取总列宽(), 获取内容区域().Width))
        item.CachedHeight = maxSubH + 项内边距.Vertical
        Return item.CachedHeight
    End Function

    Private Function 计算子项内容高度(sub_ As ListSubItem, availWidth As Integer) As Integer
        Dim f As Font = If(sub_.Font, Me.Font)
        Dim proposed As New Size(Math.Max(1, availWidth), Integer.MaxValue)
        Dim flags As TextFormatFlags = 获取文本格式标志()
        Dim totalH As Integer = TextRenderer.MeasureText(If(String.IsNullOrEmpty(sub_.Text), "Ag", sub_.Text), f, proposed, flags).Height
        For Each line_ In sub_.ExtraLines
            totalH += 文本行间距
            totalH += TextRenderer.MeasureText(If(String.IsNullOrEmpty(line_.Text), "Ag", line_.Text), If(line_.Font, Me.Font), proposed, flags).Height
        Next
        Return totalH
    End Function

    Private Sub 全部项高度缓存失效()
        For Each itm In _items
            itm.InvalidateCache()
        Next
    End Sub

#End Region

#Region "布局计算"

    Private Function 获取边框内边距() As Integer
        Dim s As Single = DpiScale()
        Return CInt(Math.Max(边框宽度 * s, If(边框圆角半径 > 0, 边框圆角半径 * s / 2, 0)))
    End Function

    Private Function 获取列标题区域() As Rectangle
        Dim inset As Integer = 获取边框内边距()
        Dim x As Integer = inset + Me.Padding.Left
        Dim y As Integer = inset + Me.Padding.Top
        Dim w As Integer = Me.Width - inset * 2 - Me.Padding.Horizontal
        Return New Rectangle(x, y, Math.Max(0, w), 列标题高度)
    End Function

    Private Function 获取内容区域() As Rectangle
        Dim inset As Integer = 获取边框内边距()
        Dim x As Integer = inset + Me.Padding.Left
        Dim y As Integer = inset + Me.Padding.Top
        If 列标题可见 AndAlso _columns.Count > 0 Then
            y += 列标题高度
        End If
        Dim w As Integer = Me.Width - inset * 2 - Me.Padding.Horizontal
        Dim h As Integer = Me.Height - inset - Me.Padding.Bottom - y
        If 需要横向滚动条() Then
            h -= (滚动条宽度 + ScrollBarRenderer.Margin * 2)
        End If
        Return New Rectangle(x, y, Math.Max(0, w), Math.Max(0, h))
    End Function

    Private Function 估算可见行数() As Integer
        Dim contentRect = 获取内容区域()
        Dim availH As Integer = contentRect.Height
        If _scrollOffset > 0 Then availH -= 更多指示器高度
        availH -= 获取有效内容上边距() + 获取有效内容下边距()
        Dim count As Integer = 0
        Dim usedH As Integer = 0
        For i As Integer = _scrollOffset To _displayRows.Count - 1
            Dim rowH As Integer = _displayRows(i).Height + 获取行前间距(i)
            If usedH + rowH > availH Then Exit For
            usedH += rowH
            count += 1
        Next
        Return Math.Max(1, count)
    End Function

    Private Function 获取行Y坐标(rowIndex As Integer) As Integer
        If rowIndex < _scrollOffset OrElse rowIndex >= _displayRows.Count Then Return -1
        Dim contentRect = 获取内容区域()
        Dim hasMoreAbove As Boolean = _scrollOffset > 0
        Dim y As Integer = contentRect.Y + If(hasMoreAbove, 更多指示器高度, 0) + 获取有效内容上边距()
        For i As Integer = _scrollOffset To rowIndex - 1
            y += _displayRows(i).Height + 获取行前间距(i + 1)
        Next
        If y >= contentRect.Bottom Then Return -1
        Return y
    End Function

    Private Function 获取列X列表() As List(Of Integer)
        Dim result As New List(Of Integer)
        Dim contentRect = 获取内容区域()
        If _columns.Count = 0 Then
            result.Add(contentRect.X)
            Return result
        End If
        Dim x As Integer = contentRect.X - _hScrollOffset
        For Each col In _columns
            result.Add(x)
            x += col.Width
        Next
        Return result
    End Function

    Private Function 获取总列宽() As Integer
        Dim total As Integer = 0
        For Each col In _columns
            total += col.Width
        Next
        Return total
    End Function

    Private Function 需要横向滚动条() As Boolean
        If _columns.Count = 0 Then Return False
        Dim inset As Integer = 获取边框内边距()
        Dim availW As Integer = Me.Width - inset * 2 - Me.Padding.Horizontal
        Return 获取总列宽() > availW
    End Function

    Private Function 获取可见列宽() As Integer
        Dim contentRect = 获取内容区域()
        Dim inset As Integer = 获取边框内边距()
        Dim vsbReserved As Integer = If(Not _scrollBar.TrackRect.IsEmpty, Me.Width - inset - _scrollBar.VisualLeft, 0)
        Return Math.Max(1, contentRect.Width - vsbReserved)
    End Function

    Private Sub 校正横向滚动偏移()
        If _columns.Count = 0 Then
            _hScrollOffset = 0
            Return
        End If
        If Not 需要横向滚动条() Then
            _hScrollOffset = 0
            Return
        End If
        Dim totalW As Integer = 获取总列宽()
        Dim inset As Integer = 获取边框内边距()
        Dim availW As Integer = Me.Width - inset * 2 - Me.Padding.Horizontal
        Dim maxOff As Integer = Math.Max(0, totalW - availW)
        _hScrollOffset = Math.Max(0, Math.Min(_hScrollOffset, maxOff))
    End Sub

#End Region

#Region "绘制"

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Me.Width < 1 OrElse Me.Height < 1 Then Return
        Dim g As Graphics = e.Graphics
        g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit

        预计算滚动条布局()
        预计算横向滚动条布局()

        Dim _ssaa As Integer = If(Class1.GlobalSSAA > 1, Class1.GlobalSSAA, 超采样倍率)
        If _ssaa > 1 Then
            Using bmp As New Bitmap(Me.Width * _ssaa, Me.Height * _ssaa)
                Using sg As Graphics = Graphics.FromImage(bmp)
                    sg.ScaleTransform(_ssaa, _ssaa)
                    sg.SmoothingMode = SmoothingMode.AntiAlias
                    sg.PixelOffsetMode = PixelOffsetMode.HighQuality
                    绘制背景与边框(sg)
                    绘制滚动条(sg)
                    绘制横向滚动条(sg)
                End Using
                g.CompositingQuality = CompositingQuality.HighQuality
                g.InterpolationMode = InterpolationMode.HighQualityBicubic
                g.DrawImage(bmp, 0, 0, Me.Width, Me.Height)
            End Using
        Else
            绘制背景与边框(g)
        End If

        Dim inset As Integer = 获取边框内边距()
        Dim clipRect As New RectangleF(inset, inset, Me.Width - inset * 2 - 1, Me.Height - inset * 2 - 1)
        If clipRect.Width <= 0 OrElse clipRect.Height <= 0 Then Return
        Dim s As Single = DpiScale()
        If 边框圆角半径 > 0 Then
            Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(clipRect, Math.Max(0, (边框圆角半径 - 边框宽度) * s))
                g.SetClip(path)
            End Using
        Else
            g.SetClip(Rectangle.Round(clipRect))
        End If

        If 列标题可见 AndAlso _columns.Count > 0 Then
            绘制列标题(g)
        End If

        绘制全部行(g)
        绘制拖选框(g)
        绘制拖动排序指示线(g)

        If _ssaa <= 1 Then
            绘制滚动条(g)
            绘制横向滚动条(g)
        End If

        g.ResetClip()
    End Sub

    Private Sub 绘制背景与边框(g As Graphics)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        Dim s As Single = DpiScale()
        Dim boundsRect As New RectangleF(0, 0, Me.Width - 1, Me.Height - 1)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * s / 2.0F
            boundsRect.Inflate(-half, -half)
        End If
        If 边框圆角半径 > 0 Then
            Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(boundsRect, 边框圆角半径 * s)
                Using br As New SolidBrush(背景颜色)
                    g.FillPath(br, path)
                End Using
                RectangleRenderer.绘制圆角边框(g, path, 边框颜色, 边框宽度 * s)
            End Using
        Else
            Using br As New SolidBrush(背景颜色)
                g.FillRectangle(br, boundsRect)
            End Using
            RectangleRenderer.绘制矩形边框(g, boundsRect, 边框颜色, 边框宽度 * s)
        End If
    End Sub

    Private Sub 预计算滚动条布局()
        If _displayRows.Count = 0 Then
            _scrollBar.ThumbRect = Rectangle.Empty
            _scrollBar.TrackRect = Rectangle.Empty
            _scrollBar.VisualLeft = Me.Width
            Return
        End If
        Dim visCount As Integer = 估算可见行数()
        If _displayRows.Count > visCount OrElse _scrollOffset > 0 Then
            Dim contentRect = 获取内容区域()
            Dim inset As Integer = 获取边框内边距()
            Dim extraTop As Integer = contentRect.Y - inset
            Dim hsbSpace As Integer = If(需要横向滚动条(), 滚动条宽度 + ScrollBarRenderer.Margin * 2, 0)
            Dim extraBottom As Integer = Me.Padding.Bottom + hsbSpace
            Dim s As Single = DpiScale()
            _scrollBar.ComputeLayout(Me.Width, Me.Height, CInt(边框宽度 * s), CInt(边框圆角半径 * s),
                extraTop, extraBottom, 滚动条宽度,
                _displayRows.Count, visCount, _scrollOffset)
        Else
            _scrollBar.ThumbRect = Rectangle.Empty
            _scrollBar.TrackRect = Rectangle.Empty
            _scrollBar.VisualLeft = Me.Width
        End If
    End Sub

    Private Sub 预计算横向滚动条布局()
        If Not 需要横向滚动条() Then
            _hScrollBar.ThumbRect = Rectangle.Empty
            _hScrollBar.TrackRect = Rectangle.Empty
            _hScrollBar.VisualTop = Me.Height
            Return
        End If
        Dim totalW As Integer = 获取总列宽()
        Dim contentRect = 获取内容区域()
        Dim inset As Integer = 获取边框内边距()
        Dim vsbReserved As Integer = If(Not _scrollBar.TrackRect.IsEmpty, Me.Width - inset - _scrollBar.VisualLeft, 0)
        Dim visibleW As Integer = Math.Max(1, contentRect.Width - vsbReserved)
        Dim paddingLeft As Integer = contentRect.X - inset
        Dim paddingRight As Integer = Me.Padding.Right + vsbReserved
        Dim s As Single = DpiScale()
        _hScrollBar.ComputeHorizontalLayout(Me.Width, Me.Height, CInt(边框宽度 * s), CInt(边框圆角半径 * s),
            paddingLeft, paddingRight, 滚动条宽度,
            totalW, visibleW, _hScrollOffset)
    End Sub

    Private Sub 绘制横向滚动条(g As Graphics)
        If _hScrollBar.TrackRect.IsEmpty Then Return
        Dim s As Single = DpiScale()
        _hScrollBar.DrawHorizontal(g, Me.Width, Me.Height, CInt(边框宽度 * s), CInt(边框圆角半径 * s),
            滚动条宽度, 滚动条轨道颜色, 滚动条滑块颜色, 滚动条悬停颜色)
    End Sub

    Private Sub 绘制列标题(g As Graphics)
        Dim headerRect As Rectangle = 获取列标题区域()
        Using br As New SolidBrush(列标题背景颜色)
            g.FillRectangle(br, headerRect)
        End Using

        Dim colXList = 获取列X列表()
        Using pen As New Pen(列标题分隔线颜色, 列标题分隔线宽度 * DpiScale())
            For i As Integer = 0 To _columns.Count - 1
                Dim col = _columns(i)
                Dim x As Integer = colXList(i)
                Dim pad As Padding = col.HeaderPadding
                Dim textRect As New Rectangle(x + pad.Left, headerRect.Y + pad.Top, col.Width - pad.Horizontal, headerRect.Height - pad.Vertical)
                TextRenderer.DrawText(g, col.Text, Me.Font, textRect, 列标题文字颜色,
                    TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding)
                g.DrawLine(pen, x + col.Width - 1, headerRect.Y + 4, x + col.Width - 1, headerRect.Bottom - 4)
            Next
            g.DrawLine(pen, headerRect.X, headerRect.Bottom - 1, headerRect.Right, headerRect.Bottom - 1)
        End Using
    End Sub

    Private Sub 绘制全部行(g As Graphics)
        If _displayRows.Count = 0 Then Return
        Dim contentRect = 获取内容区域()
        If contentRect.Height <= 0 OrElse contentRect.Width <= 0 Then Return

        Dim hasMoreAbove As Boolean = _scrollOffset > 0
        Dim topIndicatorH As Integer = If(hasMoreAbove, 更多指示器高度, 0)
        Dim currentY As Integer = contentRect.Y + topIndicatorH + 获取有效内容上边距()
        Dim bottomLimit As Integer = contentRect.Bottom - 获取有效内容下边距()
        Dim lastDrawnIndex As Integer = _scrollOffset - 1

        Dim inset As Integer = 获取边框内边距()
        Dim scrollW As Integer = If(Not _scrollBar.TrackRect.IsEmpty, Me.Width - inset - _scrollBar.VisualLeft, 0)
        Dim availW As Integer = contentRect.Width - scrollW
        If _columns.Count > 0 Then availW = Math.Max(availW, 获取总列宽())
        Dim colXList = 获取列X列表()

        ' 绘制悬停动画高亮
        Using selectedBr As New SolidBrush(项选中背景颜色),
              hoverBr As New SolidBrush(项悬停背景颜色),
              checkedPen As New Pen(项高亮边框颜色, 项高亮边框宽度 * DpiScale())

            If _hoverAnimActive AndAlso _hoverRowIndex >= 0 AndAlso Not _selectedIndices.Contains(_hoverRowIndex) Then
                Dim t As Single = _hoverAnim.Progress
                Dim animY As Single = _hoverAnimFromY + (_hoverAnimToY - _hoverAnimFromY) * t
                Dim animH As Single = _hoverAnimFromH + (_hoverAnimToH - _hoverAnimFromH) * t
                g.FillRectangle(hoverBr, New RectangleF(contentRect.X, animY, availW, animH))
            End If

            For i As Integer = _scrollOffset To _displayRows.Count - 1
                Dim row = _displayRows(i)
                Dim rowH As Integer = row.Height
                Dim spacing As Integer = 获取行前间距(i)
                If currentY + spacing + rowH > bottomLimit Then Exit For

                currentY += spacing
                Dim rowRect As New Rectangle(contentRect.X, currentY, availW, rowH)

                If _selectedIndices.Contains(i) Then
                    g.FillRectangle(selectedBr, rowRect)
                ElseIf i = _hoverRowIndex AndAlso Not _hoverAnimActive Then
                    g.FillRectangle(hoverBr, rowRect)
                End If

                If row.Type = DisplayRowType.GroupHeader Then
                    绘制分组标题行(g, row.Group, rowRect)
                Else
                    绘制项行(g, row.Item, rowRect, colXList)
                    If row.Item.Checked AndAlso 项高亮边框宽度 > 0 Then
                        Dim scaledCheckedBorder As Single = 项高亮边框宽度 * DpiScale()
                        Dim half As Single = scaledCheckedBorder / 2.0F
                        Dim borderRect As New RectangleF(
                            rowRect.X + half, rowRect.Y + half,
                            rowRect.Width - scaledCheckedBorder, rowRect.Height - scaledCheckedBorder)
                        g.DrawRectangle(checkedPen, borderRect.X, borderRect.Y, borderRect.Width, borderRect.Height)
                    End If
                End If

                currentY += rowH
                lastDrawnIndex = i
            Next
        End Using

        Dim hasMoreBelow As Boolean = lastDrawnIndex < _displayRows.Count - 1
        If hasMoreBelow AndAlso currentY < bottomLimit Then
            绘制更多指示器(g, New Rectangle(contentRect.X, currentY, availW, bottomLimit - currentY), False)
        End If
        If hasMoreAbove Then
            绘制更多指示器(g, New Rectangle(contentRect.X, contentRect.Y, availW, topIndicatorH), True)
        End If
    End Sub

    Private Sub 绘制分组标题行(g As Graphics, grp As ListGroup, rect As Rectangle)
        Using br As New SolidBrush(分组背景颜色)
            g.FillRectangle(br, rect)
        End Using

        Const arrowSize As Integer = 12
        Dim arrowX As Integer = rect.X + 10
        Dim arrowY As Integer = rect.Y + (rect.Height - arrowSize) \ 2
        Dim prevSmooth = g.SmoothingMode
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim pts() As PointF
        If grp.IsCollapsed Then
            pts = {New PointF(arrowX, arrowY), New PointF(arrowX + arrowSize, arrowY + arrowSize \ 2), New PointF(arrowX, arrowY + arrowSize)}
        Else
            pts = {New PointF(arrowX, arrowY), New PointF(arrowX + arrowSize, arrowY), New PointF(arrowX + arrowSize \ 2, arrowY + arrowSize)}
        End If
        Using br As New SolidBrush(分组文字颜色)
            g.FillPolygon(br, pts)
        End Using
        g.SmoothingMode = prevSmooth

        Dim textX As Integer = arrowX + arrowSize + 6
        Dim textRect As New Rectangle(textX, rect.Y, rect.Right - textX - 4, rect.Height)
        TextRenderer.DrawText(g, grp.Text, Me.Font, textRect, 分组文字颜色,
            TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding)

        Using pen As New Pen(分组分隔线颜色)
            g.DrawLine(pen, rect.X, rect.Bottom - 1, rect.Right, rect.Bottom - 1)
        End Using
    End Sub

    Private Sub 绘制项行(g As Graphics, item As ListItem, rowRect As Rectangle, colXList As List(Of Integer))
        Dim iconAreaW As Integer = 获取图标区域宽度(item)
        Dim hasIcon As Boolean = iconAreaW > 0
        Dim bottomLinesH As Integer = 计算底部文本行高度(item, rowRect.Width)
        Dim upperPartH As Integer = rowRect.Height - 项内边距.Vertical - bottomLinesH
        Dim flags As TextFormatFlags = 获取文本格式标志()

        Dim colCount As Integer = If(_columns.Count > 0, _columns.Count, 1)
        For colIdx As Integer = 0 To colCount - 1
            If colIdx >= item.SubItems.Count Then Exit For
            Dim sub_ = item.SubItems(colIdx)
            Dim colX As Integer = colXList(Math.Min(colIdx, colXList.Count - 1))
            Dim colW As Integer = If(_columns.Count > 0, _columns(colIdx).Width, rowRect.Width)

            Dim cellRect As New Rectangle(
                colX + 项内边距.Left, rowRect.Y + 项内边距.Top,
                colW - 项内边距.Horizontal, upperPartH)

            If colIdx = 0 AndAlso hasIcon Then
                Dim iconX As Integer = cellRect.X
                Dim iconY As Integer = rowRect.Y + 项内边距.Top + (upperPartH - 图标尺寸.Height) \ 2
                Dim prevMode = g.InterpolationMode
                g.InterpolationMode = InterpolationMode.HighQualityBicubic
                g.DrawImage(item.Icon, New Rectangle(iconX, iconY, 图标尺寸.Width, 图标尺寸.Height))
                g.InterpolationMode = prevMode
                cellRect = New Rectangle(cellRect.X + iconAreaW, cellRect.Y, cellRect.Width - iconAreaW, cellRect.Height)
            End If

            If cellRect.Width <= 0 OrElse cellRect.Height <= 0 Then Continue For

            Dim contentH As Integer = 计算子项内容高度(sub_, cellRect.Width)
            Dim startY As Integer = cellRect.Y + Math.Max(0, (cellRect.Height - contentH) \ 2)
            Dim lineY As Integer = startY
            Dim proposed As New Size(cellRect.Width, Integer.MaxValue)

            ' 主文本
            Dim mf As Font = If(sub_.Font, Me.Font)
            Dim mc As Color = If(sub_.ForeColor <> Color.Empty, sub_.ForeColor, 项文本颜色)
            Dim mh As Integer = TextRenderer.MeasureText(If(String.IsNullOrEmpty(sub_.Text), "Ag", sub_.Text), mf, proposed, flags).Height
            If Not String.IsNullOrEmpty(sub_.Text) Then
                TextRenderer.DrawText(g, sub_.Text, mf, New Rectangle(cellRect.X, lineY, cellRect.Width, mh), mc, flags)
            End If
            lineY += mh

            ' 附加文本行
            For Each line_ In sub_.ExtraLines
                lineY += 文本行间距
                Dim lf As Font = If(line_.Font, Me.Font)
                Dim lc As Color = If(line_.ForeColor <> Color.Empty, line_.ForeColor, 项文本颜色)
                Dim lh As Integer = TextRenderer.MeasureText(If(String.IsNullOrEmpty(line_.Text), "Ag", line_.Text), lf, proposed, flags).Height
                If Not String.IsNullOrEmpty(line_.Text) Then
                    TextRenderer.DrawText(g, line_.Text, lf, New Rectangle(cellRect.X, lineY, cellRect.Width, lh), lc, flags)
                End If
                lineY += lh
            Next
        Next

        ' 底部附加文本行
        If item.BottomLines.Count > 0 Then
            Dim blX As Integer = rowRect.X + 项内边距.Left
            Dim blW As Integer = rowRect.Width - 项内边距.Horizontal
            Dim blY As Integer = rowRect.Y + 项内边距.Top + upperPartH + 底部文本行间距
            Dim blProposed As New Size(Math.Max(1, blW), Integer.MaxValue)
            For Each bl In item.BottomLines
                blY += 文本行间距
                Dim lf As Font = If(bl.Font, Me.Font)
                Dim lc As Color = If(bl.ForeColor <> Color.Empty, bl.ForeColor, 项文本颜色)
                Dim lh As Integer = TextRenderer.MeasureText(If(String.IsNullOrEmpty(bl.Text), "Ag", bl.Text), lf, blProposed, flags).Height
                If Not String.IsNullOrEmpty(bl.Text) Then
                    TextRenderer.DrawText(g, bl.Text, lf, New Rectangle(blX, blY, blW, lh), lc, flags)
                End If
                blY += lh
            Next
        End If
    End Sub

    Private Sub 绘制更多指示器(g As Graphics, rect As Rectangle, isTop As Boolean)
        If rect.Height < 2 Then Return
        Dim c1 As Color = Color.FromArgb(200, 背景颜色)
        Dim c2 As Color = Color.FromArgb(0, 背景颜色)
        Dim pt1 As New Point(rect.X, If(isTop, rect.Y, rect.Bottom - 1))
        Dim pt2 As New Point(rect.X, If(isTop, rect.Bottom - 1, rect.Y))
        Using br As New LinearGradientBrush(pt1, pt2, c1, c2)
            g.FillRectangle(br, rect)
        End Using
        Dim symbol As String = If(isTop, "▲", "▼")
        Using symbolFont As New Font(Me.Font.FontFamily, Math.Max(7, Me.Font.Size - 1), FontStyle.Regular)
            TextRenderer.DrawText(g, symbol, symbolFont, rect, 更多指示器颜色,
                TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        End Using
    End Sub

    Private Sub 绘制滚动条(g As Graphics)
        If _scrollBar.TrackRect.IsEmpty Then Return
        Dim s As Single = DpiScale()
        _scrollBar.Draw(g, Me.Width, Me.Height, CInt(边框宽度 * s), CInt(边框圆角半径 * s),
            滚动条宽度, 滚动条轨道颜色, 滚动条滑块颜色, 滚动条悬停颜色)
    End Sub

    Private Sub 绘制拖选框(g As Graphics)
        If Not _isDragSelecting Then Return
        Dim rect As Rectangle = 获取拖选矩形()
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Using br As New SolidBrush(选框填充颜色)
            g.FillRectangle(br, rect)
        End Using
        Using pen As New Pen(选框边框颜色)
            g.DrawRectangle(pen, rect)
        End Using
    End Sub

    Private Sub 绘制拖动排序指示线(g As Graphics)
        If Not _isDragReordering OrElse _dragReorderInsertIndex < 0 Then Return
        Dim contentRect = 获取内容区域()
        Dim inset = 获取边框内边距()
        Dim scrollW = If(Not _scrollBar.TrackRect.IsEmpty, Me.Width - inset - _scrollBar.VisualLeft, 0)
        Dim availW = contentRect.Width - scrollW
        If _columns.Count > 0 Then availW = Math.Max(availW, 获取总列宽())
        Dim lineY As Integer
        If _dragReorderInsertIndex < _displayRows.Count Then
            lineY = 获取行Y坐标(_dragReorderInsertIndex)
            If lineY < 0 Then Return
        Else
            Dim lastIdx = _dragReorderInsertIndex - 1
            If lastIdx < 0 OrElse lastIdx >= _displayRows.Count Then Return
            lineY = 获取行Y坐标(lastIdx)
            If lineY < 0 Then Return
            lineY += _displayRows(lastIdx).Height
        End If
        Using pen As New Pen(拖动排序指示线颜色, 拖动排序指示线宽 * DpiScale())
            g.DrawLine(pen, contentRect.X, lineY, contentRect.X + availW, lineY)
        End Using
    End Sub

#End Region

#Region "鼠标交互"

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)

        If _scrollBar.IsDragging Then
            隐藏截断提示()
            Dim visCount = 估算可见行数()
            _scrollOffset = _scrollBar.DragMove(e.Y, _displayRows.Count, visCount)
            Dim hitRow = 命中测试行(e.Location)
            更新悬停(hitRow)
            Me.Invalidate()
            Return
        End If

        If _hScrollBar.IsDragging Then
            隐藏截断提示()
            Dim totalW = 获取总列宽()
            Dim visibleW = 获取可见列宽()
            _hScrollOffset = _hScrollBar.DragMoveHorizontal(e.X, totalW, visibleW)
            Me.Invalidate()
            Return
        End If

        If _columnResizeIndex >= 0 Then
            隐藏截断提示()
            Dim delta As Integer = e.X - _columnResizeStartX
            _columns(_columnResizeIndex).Width = Math.Max(30, _columnResizeStartWidth + delta)
            全部项高度缓存失效()
            重建显示列表()
            校正横向滚动偏移()
            Me.Invalidate()
            Return
        End If

        ' 拖选/拖排序检测
        If e.Button = MouseButtons.Left AndAlso _mouseDownInContent Then
            If _isDragReordering Then
                隐藏截断提示()
                _dragReorderInsertIndex = 计算拖动排序插入位置(e.Y)
                Me.Invalidate()
                Return
            End If

            If Not _isDragSelecting Then
                If Math.Abs(e.X - _mouseDownPos.X) > DragThreshold OrElse Math.Abs(e.Y - _mouseDownPos.Y) > DragThreshold Then
                    取消标签编辑等待()
                    If 应该拖选() Then
                        _isDragSelecting = True
                        _dragPreSelectedIndices = New HashSet(Of Integer)(_selectedIndices)
                    ElseIf 允许拖动排序 AndAlso _dragReorderSourceIndex >= 0 AndAlso
                           _dragReorderSourceIndex < _displayRows.Count AndAlso
                           _displayRows(_dragReorderSourceIndex).Type = DisplayRowType.Item Then
                        _isDragReordering = True
                        Dim srcGroup = _displayRows(_dragReorderSourceIndex).Item.GroupName
                        If _selectedIndices.Contains(_dragReorderSourceIndex) AndAlso _selectedIndices.Count > 1 Then
                            Dim allSameGroup = _selectedIndices.All(Function(idx) idx >= 0 AndAlso idx < _displayRows.Count AndAlso
                                _displayRows(idx).Type = DisplayRowType.Item AndAlso
                                String.Equals(_displayRows(idx).Item.GroupName, srcGroup, StringComparison.Ordinal))
                            If allSameGroup Then
                                _dragReorderSourceIndices = _selectedIndices.OrderBy(Function(x) x).ToList()
                            Else
                                _dragReorderSourceIndices = New List(Of Integer) From {_dragReorderSourceIndex}
                            End If
                        Else
                            _dragReorderSourceIndices = New List(Of Integer) From {_dragReorderSourceIndex}
                        End If
                        _dragReorderInsertIndex = _dragReorderSourceIndex
                        Me.Invalidate()
                        Return
                    ElseIf 允许多选 Then
                        _isDragSelecting = True
                        _dragPreSelectedIndices = New HashSet(Of Integer)(_selectedIndices)
                    End If
                End If
            End If
            If _isDragSelecting Then
                隐藏截断提示()
                _dragCurrent = e.Location
                更新拖选(e)
                Me.Invalidate()
                Return
            End If
        End If

        Dim needInvalidate As Boolean = False
        If _scrollBar.UpdateHover(e.Location) Then needInvalidate = True
        If _hScrollBar.UpdateHover(e.Location) Then needInvalidate = True
        If needInvalidate Then Me.Invalidate()

        If 列标题可见 AndAlso _columns.Count > 0 AndAlso 允许调整列宽 Then
            Dim headerRect = 获取列标题区域()
            If headerRect.Contains(e.Location) Then
                Dim resizeCol = 检测列分隔线(e.X)
                Me.Cursor = If(resizeCol >= 0, Cursors.VSplit, Cursors.Default)
                更新悬停(-1)
                隐藏截断提示()
                Return
            End If
        End If

        Me.Cursor = Cursors.Default
        Dim hitRowIdx As Integer = 命中测试行(e.Location)
        更新悬停(hitRowIdx)
        更新截断提示(e.Location)
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)

        取消标签编辑等待()
        If _editTextBox IsNot Nothing Then 结束标签编辑(False)

        _mouseDownInContent = False

        If _scrollBar.BeginDrag(e.Location, _scrollOffset) Then Return
        If Not _scrollBar.TrackRect.IsEmpty Then
            Dim visCount = 估算可见行数()
            Dim newOff = _scrollBar.TrackClick(e.Location, _scrollOffset, _displayRows.Count, visCount)
            If newOff <> _scrollOffset Then
                _scrollOffset = newOff
                Me.Invalidate()
                Return
            End If
        End If

        If _hScrollBar.BeginDragHorizontal(e.Location, _hScrollOffset) Then Return
        If Not _hScrollBar.TrackRect.IsEmpty Then
            Dim totalW = 获取总列宽()
            Dim visibleW = 获取可见列宽()
            Dim newHOff = _hScrollBar.TrackClickHorizontal(e.Location, _hScrollOffset, totalW, visibleW)
            If newHOff <> _hScrollOffset Then
                _hScrollOffset = newHOff
                Me.Invalidate()
                Return
            End If
        End If

        If 列标题可见 AndAlso _columns.Count > 0 AndAlso 允许调整列宽 Then
            Dim headerRect = 获取列标题区域()
            If headerRect.Contains(e.Location) Then
                Dim resizeCol = 检测列分隔线(e.X)
                If resizeCol >= 0 Then
                    _columnResizeIndex = resizeCol
                    _columnResizeStartX = e.X
                    _columnResizeStartWidth = _columns(resizeCol).Width
                    Return
                End If
            End If
        End If

        ' 点击分组标题立即折叠/展开
        Dim hitRow As Integer = 命中测试行(e.Location)
        If hitRow >= 0 AndAlso hitRow < _displayRows.Count Then
            Dim row = _displayRows(hitRow)
            If row.Type = DisplayRowType.GroupHeader Then
                row.Group.IsCollapsed = Not row.Group.IsCollapsed
                重建显示列表()
                Me.Invalidate()
                Return
            End If
        End If

        ' 记录按下位置，用于区分点击与拖选
        _mouseDownPos = e.Location
        _mouseDownInContent = True
        _isDragSelecting = False
        _isDragReordering = False
        _dragReorderSourceIndex = hitRow
        _mouseDownInDragSelectZone = 是否在拖选区域(e.X)
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)

        ' 完成拖动排序
        If _isDragReordering Then
            _isDragReordering = False
            执行拖动排序()
            _dragReorderSourceIndex = -1
            _dragReorderSourceIndices.Clear()
            _dragReorderInsertIndex = -1
            _mouseDownInContent = False
            Me.Invalidate()
            Return
        End If

        If _isDragSelecting Then
            _isDragSelecting = False
            _mouseDownInContent = False
            _scrollBar.EndDrag()
            _hScrollBar.EndDrag()
            _columnResizeIndex = -1
            Me.Invalidate()
            Return
        End If

        If _mouseDownInContent Then
            _mouseDownInContent = False
            Dim hitRow As Integer = 命中测试行(e.Location)
            处理点击选择(hitRow, e)
        End If

        _scrollBar.EndDrag()
        _hScrollBar.EndDrag()
        _columnResizeIndex = -1
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        更新悬停(-1)
        隐藏截断提示()
        Dim needInvalidate2 As Boolean = False
        If _scrollBar.ResetHover() Then needInvalidate2 = True
        If _hScrollBar.ResetHover() Then needInvalidate2 = True
        If needInvalidate2 Then Me.Invalidate()
        Me.Cursor = Cursors.Default
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        隐藏截断提示()
        取消标签编辑等待()
        If _editTextBox IsNot Nothing Then 结束标签编辑(False)
        If (Control.ModifierKeys And Keys.Shift) = Keys.Shift Then
            If _columns.Count > 0 AndAlso 需要横向滚动条() Then
                Dim totalW = 获取总列宽()
                Dim visibleW = 获取可见列宽()
                Dim newHOff = ScrollBarRenderer.HandleHorizontalWheel(e.Delta, _hScrollOffset, totalW, visibleW)
                If newHOff <> _hScrollOffset Then
                    _hScrollOffset = newHOff
                    Me.Invalidate()
                End If
            End If
            Return
        End If
        If _displayRows.Count = 0 Then Return
        Dim visCount = 估算可见行数()
        Dim newOff = ScrollBarRenderer.HandleWheel(e.Delta, _scrollOffset, _displayRows.Count, visCount, 3)
        If newOff <> _scrollOffset Then
            _scrollOffset = newOff
            Dim hitRow = 命中测试行(e.Location)
            更新悬停(hitRow)
            Me.Invalidate()
        End If
    End Sub

    Private Sub 处理点击选择(hitRow As Integer, e As MouseEventArgs)
        Dim ctrlHeld As Boolean = (Control.ModifierKeys And Keys.Control) = Keys.Control
        Dim shiftHeld As Boolean = (Control.ModifierKeys And Keys.Shift) = Keys.Shift

        ' 检测是否是对已选中单项的慢速点击
        Dim wasOnlySelected As Boolean = (Not ctrlHeld AndAlso Not shiftHeld AndAlso
            _selectedIndices.Count = 1 AndAlso _selectedIndices.Contains(hitRow) AndAlso
            hitRow >= 0 AndAlso hitRow < _displayRows.Count AndAlso
            _displayRows(hitRow).Type = DisplayRowType.Item)

        If hitRow < 0 OrElse hitRow >= _displayRows.Count Then
            If Not ctrlHeld AndAlso Not shiftHeld Then
                设置选中集合(Enumerable.Empty(Of Integer))
            End If
            Return
        End If

        Dim row = _displayRows(hitRow)
        If row.Type = DisplayRowType.GroupHeader Then Return

        If 允许多选 AndAlso shiftHeld AndAlso _selectionAnchor >= 0 Then
            Dim range = 构建项范围(Math.Min(_selectionAnchor, hitRow), Math.Max(_selectionAnchor, hitRow))
            If ctrlHeld Then
                Dim combined As New HashSet(Of Integer)(_selectedIndices)
                For Each idx In range
                    combined.Add(idx)
                Next
                设置选中集合(combined)
            Else
                设置选中集合(range)
            End If
        ElseIf 允许多选 AndAlso ctrlHeld Then
            Dim newSet As New HashSet(Of Integer)(_selectedIndices)
            If Not newSet.Remove(hitRow) Then
                newSet.Add(hitRow)
            End If
            _selectionAnchor = hitRow
            设置选中集合(newSet)
        Else
            _selectionAnchor = hitRow
            设置选中集合({hitRow})
        End If

        ' 慢速点击编辑检测
        Dim clickedCol As Integer = 命中测试列(e.Location)
        If wasOnlySelected AndAlso 列可编辑(clickedCol) Then
            开始标签编辑等待(hitRow, clickedCol)
        Else
            取消标签编辑等待()
        End If

        RaiseEvent ItemClick(Me, New ListItemEventArgs(row.Item, hitRow))
    End Sub

#End Region

#Region "双击与键盘交互"

    Protected Overrides Sub OnMouseDoubleClick(e As MouseEventArgs)
        MyBase.OnMouseDoubleClick(e)
        取消标签编辑等待()
        Dim hitRow = 命中测试行(e.Location)
        If hitRow >= 0 AndAlso hitRow < _displayRows.Count Then
            Dim row = _displayRows(hitRow)
            If row.Type = DisplayRowType.Item Then
                RaiseEvent ItemDoubleClick(Me, New ListItemEventArgs(row.Item, hitRow))
            ElseIf row.Type = DisplayRowType.GroupHeader Then
                row.Group.IsCollapsed = Not row.Group.IsCollapsed
                重建显示列表()
                Me.Invalidate()
            End If
        End If
    End Sub

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        Select Case keyData
            Case Keys.Up, Keys.Down, Keys.PageUp, Keys.PageDown, Keys.Home, Keys.End,
                 Keys.Up Or Keys.Shift, Keys.Down Or Keys.Shift,
                 Keys.PageUp Or Keys.Shift, Keys.PageDown Or Keys.Shift,
                 Keys.Home Or Keys.Shift, Keys.End Or Keys.Shift,
                 Keys.A Or Keys.Control, Keys.F2, Keys.Escape
                OnKeyDown(New KeyEventArgs(keyData))
                Return True
        End Select
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If _displayRows.Count = 0 Then Return
        Dim shiftHeld = (e.Modifiers And Keys.Shift) = Keys.Shift

        Select Case e.KeyCode
            Case Keys.A
                If (e.Modifiers And Keys.Control) = Keys.Control Then
                    SelectAll()
                    e.Handled = True
                End If
            Case Keys.F2
                If _selectedIndices.Count = 1 Then
                    Dim firstEditableCol = 查找首个可编辑列()
                    If firstEditableCol >= 0 Then
                        开始标签编辑(_selectedIndices.Min(), firstEditableCol)
                    End If
                End If
                e.Handled = True
            Case Keys.Escape
                If _editTextBox IsNot Nothing Then
                    结束标签编辑(True)
                    e.Handled = True
                ElseIf _isDragReordering Then
                    _isDragReordering = False
                    _dragReorderSourceIndex = -1
                    _dragReorderSourceIndices.Clear()
                    _dragReorderInsertIndex = -1
                    Me.Invalidate()
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

    Private Sub 键盘导航(delta As Integer, shiftHeld As Boolean)
        Dim currentIdx = If(_selectionAnchor >= 0 AndAlso _selectionAnchor < _displayRows.Count, _selectionAnchor, -1)
        If currentIdx < 0 Then
            Dim first = 查找下一个项行(-1, 1)
            If first >= 0 Then
                _selectionAnchor = first
                设置选中集合({first})
                EnsureVisible(first)
            End If
            Return
        End If

        Dim direction = Math.Sign(delta)
        If direction = 0 Then Return
        Dim steps = Math.Abs(delta)
        Dim idx = currentIdx
        For s = 1 To steps
            Dim next_ = 查找下一个项行(idx, direction)
            If next_ < 0 Then Exit For
            idx = next_
        Next
        If idx = currentIdx Then Return

        If shiftHeld AndAlso 允许多选 Then
            设置选中集合(构建项范围(Math.Min(_selectionAnchor, idx), Math.Max(_selectionAnchor, idx)))
        Else
            _selectionAnchor = idx
            设置选中集合({idx})
        End If
        EnsureVisible(idx)
    End Sub

    Private Sub 键盘导航至边缘(toStart As Boolean, shiftHeld As Boolean)
        Dim targetIdx As Integer
        If toStart Then
            targetIdx = 查找下一个项行(-1, 1)
        Else
            targetIdx = 查找下一个项行(_displayRows.Count, -1)
        End If
        If targetIdx < 0 Then Return

        If shiftHeld AndAlso 允许多选 AndAlso _selectionAnchor >= 0 Then
            设置选中集合(构建项范围(Math.Min(_selectionAnchor, targetIdx), Math.Max(_selectionAnchor, targetIdx)))
        Else
            _selectionAnchor = targetIdx
            设置选中集合({targetIdx})
        End If
        EnsureVisible(targetIdx)
    End Sub

    Private Function 查找下一个项行(fromIndex As Integer, direction As Integer) As Integer
        Dim i = fromIndex + direction
        While i >= 0 AndAlso i < _displayRows.Count
            If _displayRows(i).Type = DisplayRowType.Item Then Return i
            i += direction
        End While
        Return -1
    End Function

#End Region

#Region "拖选"

    Private Function 获取拖选矩形() As Rectangle
        Dim x1 As Integer = Math.Min(_mouseDownPos.X, _dragCurrent.X)
        Dim y1 As Integer = Math.Min(_mouseDownPos.Y, _dragCurrent.Y)
        Dim x2 As Integer = Math.Max(_mouseDownPos.X, _dragCurrent.X)
        Dim y2 As Integer = Math.Max(_mouseDownPos.Y, _dragCurrent.Y)
        Return New Rectangle(x1, y1, x2 - x1, y2 - y1)
    End Function

    Private Sub 更新拖选(e As MouseEventArgs)
        If Not 允许多选 Then
            Dim hitRow = 命中测试行(e.Location)
            If hitRow >= 0 AndAlso hitRow < _displayRows.Count AndAlso _displayRows(hitRow).Type = DisplayRowType.Item Then
                _selectionAnchor = hitRow
                设置选中集合({hitRow})
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
        Dim hasMoreAbove As Boolean = _scrollOffset > 0
        Dim topOffset As Integer = If(hasMoreAbove, 更多指示器高度, 0)
        Dim currentY As Integer = contentRect.Y + topOffset + 获取有效内容上边距()
        Dim bottomLimit As Integer = contentRect.Bottom - 获取有效内容下边距()
        Dim rowW As Integer = contentRect.Width
        If _columns.Count > 0 Then rowW = Math.Max(rowW, 获取总列宽())

        For i As Integer = _scrollOffset To _displayRows.Count - 1
            Dim spacing As Integer = 获取行前间距(i)
            Dim rowH As Integer = _displayRows(i).Height
            If currentY + spacing + rowH > bottomLimit Then Exit For
            currentY += spacing
            Dim rowRect As New Rectangle(contentRect.X, currentY, rowW, rowH)
            If dragRect.IntersectsWith(rowRect) AndAlso _displayRows(i).Type = DisplayRowType.Item Then
                result.Add(i)
            End If
            currentY += rowH
        Next
        Return result
    End Function

#End Region

#Region "拖动排序"

    Private Function 是否在拖选区域(mouseX As Integer) As Boolean
        If Not 允许多选 Then Return False
        If Not 允许拖动排序 Then Return True
        Dim contentRect = 获取内容区域()
        Dim inset = 获取边框内边距()
        Dim scrollW = If(Not _scrollBar.TrackRect.IsEmpty, Me.Width - inset - _scrollBar.VisualLeft, 0)
        Dim zoneLeft = contentRect.Right - scrollW - 拖选区域宽度
        Return mouseX >= zoneLeft
    End Function

    Private Function 应该拖选() As Boolean
        If Not 允许多选 Then Return False
        If Not 允许拖动排序 Then Return True
        Return _mouseDownInDragSelectZone
    End Function

    Private Function 计算拖动排序插入位置(mouseY As Integer) As Integer
        If _dragReorderSourceIndex < 0 OrElse _dragReorderSourceIndex >= _displayRows.Count Then Return -1
        Dim sourceGroup As String = _displayRows(_dragReorderSourceIndex).Item.GroupName
        Dim groupRows As New List(Of Integer)
        For i = 0 To _displayRows.Count - 1
            If _displayRows(i).Type = DisplayRowType.Item AndAlso
               String.Equals(_displayRows(i).Item.GroupName, sourceGroup, StringComparison.Ordinal) Then
                groupRows.Add(i)
            End If
        Next
        If groupRows.Count = 0 Then Return -1
        Dim bestSlot As Integer = groupRows(0)
        Dim bestDist As Integer = Integer.MaxValue
        For g = 0 To groupRows.Count
            Dim gapY As Integer
            If g < groupRows.Count Then
                Dim y = 获取行Y坐标(groupRows(g))
                If y < 0 Then Continue For
                gapY = y
            Else
                Dim lastIdx = groupRows(g - 1)
                Dim y = 获取行Y坐标(lastIdx)
                If y < 0 Then Continue For
                gapY = y + _displayRows(lastIdx).Height
            End If
            Dim dist = Math.Abs(mouseY - gapY)
            If dist < bestDist Then
                bestDist = dist
                bestSlot = If(g < groupRows.Count, groupRows(g), groupRows(g - 1) + 1)
            End If
        Next
        Return bestSlot
    End Function

    Private Sub 执行拖动排序()
        If _dragReorderSourceIndices.Count = 0 OrElse _dragReorderInsertIndex < 0 Then Return
        Dim movedItems As New List(Of ListItem)
        For Each dispIdx In _dragReorderSourceIndices.OrderBy(Function(x) x)
            If dispIdx >= 0 AndAlso dispIdx < _displayRows.Count AndAlso _displayRows(dispIdx).Type = DisplayRowType.Item Then
                movedItems.Add(_displayRows(dispIdx).Item)
            End If
        Next
        If movedItems.Count = 0 Then Return

        Dim targetItemsIndex As Integer
        If _dragReorderInsertIndex < _displayRows.Count AndAlso _displayRows(_dragReorderInsertIndex).Type = DisplayRowType.Item Then
            targetItemsIndex = 查找项索引(_displayRows(_dragReorderInsertIndex).Item)
        Else
            Dim groupName = movedItems(0).GroupName
            targetItemsIndex = _items.Count
            For i = _items.Count - 1 To 0 Step -1
                If String.Equals(_items(i).GroupName, groupName, StringComparison.Ordinal) Then
                    targetItemsIndex = i + 1
                    Exit For
                End If
            Next
        End If

        Dim sourceItemsIndices As New List(Of Integer)
        For Each item In movedItems
            sourceItemsIndices.Add(查找项索引(item))
        Next
        sourceItemsIndices.Sort()

        Dim countBefore = sourceItemsIndices.Where(Function(i) i < targetItemsIndex).Count()
        Dim adjustedTarget = targetItemsIndex - countBefore

        BeginUpdate()
        For i = sourceItemsIndices.Count - 1 To 0 Step -1
            _items.RemoveAt(sourceItemsIndices(i))
        Next
        adjustedTarget = Math.Max(0, Math.Min(adjustedTarget, _items.Count))
        For i = 0 To movedItems.Count - 1
            _items.Insert(adjustedTarget + i, movedItems(i))
        Next
        EndUpdate()

        _selectedIndices.Clear()
        For i = 0 To _displayRows.Count - 1
            If _displayRows(i).Type = DisplayRowType.Item AndAlso movedItems.Contains(_displayRows(i).Item) Then
                _selectedIndices.Add(i)
            End If
        Next
        _selectionAnchor = If(_selectedIndices.Count > 0, _selectedIndices.Min(), -1)
        RaiseEvent ItemOrderChanged(Me, EventArgs.Empty)
        RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
    End Sub

    Private Function 查找项索引(item As ListItem) As Integer
        For i = 0 To _items.Count - 1
            If _items(i) Is item Then Return i
        Next
        Return -1
    End Function

#End Region

#Region "标签编辑"

    Private Sub 开始标签编辑等待(displayRowIndex As Integer, columnIndex As Integer)
        取消标签编辑等待()
        _labelEditPendingIndex = displayRowIndex
        _labelEditPendingColumn = columnIndex
        If _labelEditTimer Is Nothing Then
            _labelEditTimer = New Timer()
            AddHandler _labelEditTimer.Tick, AddressOf 标签编辑计时器触发
        End If
        _labelEditTimer.Interval = SystemInformation.DoubleClickTime + 100
        _labelEditTimer.Start()
    End Sub

    Private Sub 取消标签编辑等待()
        _labelEditPendingIndex = -1
        _labelEditPendingColumn = -1
        _labelEditTimer?.Stop()
    End Sub

    Private Sub 标签编辑计时器触发(sender As Object, e As EventArgs)
        _labelEditTimer.Stop()
        Dim idx = _labelEditPendingIndex
        Dim col = _labelEditPendingColumn
        _labelEditPendingIndex = -1
        _labelEditPendingColumn = -1
        If idx >= 0 AndAlso col >= 0 Then 开始标签编辑(idx, col)
    End Sub

    ''' <summary>开始编辑指定行指定列的子项主文本。</summary>
    Public Sub 开始标签编辑(displayRowIndex As Integer, columnIndex As Integer)
        If _editTextBox IsNot Nothing Then 结束标签编辑(True)
        If displayRowIndex < 0 OrElse displayRowIndex >= _displayRows.Count Then Return
        If Not 列可编辑(columnIndex) Then Return
        Dim row = _displayRows(displayRowIndex)
        If row.Type <> DisplayRowType.Item Then Return
        Dim item = row.Item
        If columnIndex >= item.SubItems.Count Then Return

        Dim rowY = 获取行Y坐标(displayRowIndex)
        If rowY < 0 Then Return

        Dim contentRect = 获取内容区域()
        Dim inset = 获取边框内边距()
        Dim scrollW = If(Not _scrollBar.TrackRect.IsEmpty, Me.Width - inset - _scrollBar.VisualLeft, 0)
        Dim availW = contentRect.Width - scrollW
        If _columns.Count > 0 Then availW = Math.Max(availW, 获取总列宽())

        Dim colXList = 获取列X列表()
        Dim colX = colXList(Math.Min(columnIndex, colXList.Count - 1))
        Dim colW = _columns(columnIndex).Width
        Dim iconAreaW = If(columnIndex = 0, 获取图标区域宽度(item), 0)
        Dim cellX = colX + 项内边距.Left + iconAreaW
        Dim cellW = colW - 项内边距.Horizontal - iconAreaW
        If cellW <= 20 Then Return

        _editRowIndex = displayRowIndex
        _editColumnIndex = columnIndex
        Dim sub_ = item.SubItems(columnIndex)

        _editTextBox = New ModernTextBox With {
            .Text = sub_.Text,
            .Font = If(sub_.Font, Me.Font),
            .ForeColor = If(sub_.ForeColor <> Color.Empty, sub_.ForeColor, 项文本颜色),
            .BackColor1 = 背景颜色,
            .BorderSize = 1,
            .BorderColor = 项高亮边框颜色,
            .BorderColorFocus = 项高亮边框颜色,
            .Padding = New Padding(2, 0, 2, 0),
            .Location = New Point(cellX, rowY),
            .Size = New Size(cellW, row.Height)
        }

        Me.Controls.Add(_editTextBox)
        _editTextBox.BringToFront()
        _editTextBox.Focus()
        _editTextBox.[Select](0, sub_.Text.Length)

        AddHandler _editTextBox.PreviewKeyDown, AddressOf 编辑框预览按键
        AddHandler _editTextBox.LostFocus, AddressOf 编辑框失焦
        AddHandler _editTextBox.KeyDown, AddressOf 编辑框按键
    End Sub

    Private Sub 编辑框预览按键(sender As Object, e As PreviewKeyDownEventArgs)
        If e.KeyCode = Keys.Escape Then e.IsInputKey = True
    End Sub

    Private Sub 编辑框失焦(sender As Object, e As EventArgs)
        结束标签编辑(False)
    End Sub

    Private Sub 编辑框按键(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.Return Then
            结束标签编辑(False)
            e.Handled = True
        ElseIf e.KeyCode = Keys.Escape Then
            结束标签编辑(True)
            e.Handled = True
        End If
    End Sub

    Private Sub 结束标签编辑(cancel As Boolean)
        If _editTextBox Is Nothing Then Return
        Dim editBox = _editTextBox
        _editTextBox = Nothing

        RemoveHandler editBox.PreviewKeyDown, AddressOf 编辑框预览按键
        RemoveHandler editBox.LostFocus, AddressOf 编辑框失焦
        RemoveHandler editBox.KeyDown, AddressOf 编辑框按键

        Dim shouldRefocus = editBox.Focused

        If Not cancel AndAlso _editRowIndex >= 0 AndAlso _editRowIndex < _displayRows.Count AndAlso _editColumnIndex >= 0 Then
            Dim row = _displayRows(_editRowIndex)
            If row.Type = DisplayRowType.Item AndAlso _editColumnIndex < row.Item.SubItems.Count Then
                Dim oldText = row.Item.SubItems(_editColumnIndex).Text
                Dim newText = editBox.Text
                Dim args As New LabelEditEventArgs(row.Item, _editRowIndex, _editColumnIndex, oldText, newText)
                RaiseEvent AfterLabelEdit(Me, args)
                If Not args.CancelEdit Then
                    row.Item.SubItems(_editColumnIndex).Text = args.Label
                    row.Item.InvalidateCache()
                    重建显示列表()
                End If
            End If
        End If

        _editRowIndex = -1
        _editColumnIndex = -1
        Me.Controls.Remove(editBox)
        editBox.Dispose()
        Me.Invalidate()
        If shouldRefocus Then Me.Focus()
    End Sub

#End Region

#Region "命中测试"

    Private Function 命中测试行(pt As Point) As Integer
        Dim contentRect = 获取内容区域()
        If Not contentRect.Contains(pt) Then Return -1
        Dim hasMoreAbove As Boolean = _scrollOffset > 0
        Dim topOffset As Integer = If(hasMoreAbove, 更多指示器高度, 0)
        Dim currentY As Integer = contentRect.Y + topOffset + 获取有效内容上边距()
        Dim bottomLimit As Integer = contentRect.Bottom - 获取有效内容下边距()

        For i As Integer = _scrollOffset To _displayRows.Count - 1
            Dim spacing As Integer = 获取行前间距(i)
            Dim rowH As Integer = _displayRows(i).Height
            If currentY + spacing + rowH > bottomLimit Then Exit For
            currentY += spacing
            If pt.Y >= currentY AndAlso pt.Y < currentY + rowH Then
                Return i
            End If
            currentY += rowH
        Next
        Return -1
    End Function

    Private Function 检测列分隔线(mouseX As Integer) As Integer
        If _columns.Count < 1 Then Return -1
        Dim colXList = 获取列X列表()
        For i As Integer = 0 To _columns.Count - 1
            Dim sepX As Integer = colXList(i) + _columns(i).Width
            If Math.Abs(mouseX - sepX) <= ColumnResizeHitZone Then
                Return i
            End If
        Next
        Return -1
    End Function

    Private Function 命中测试列(pt As Point) As Integer
        If _columns.Count = 0 Then Return -1
        Dim colXList = 获取列X列表()
        For i = 0 To _columns.Count - 1
            Dim colX = colXList(i)
            If pt.X >= colX AndAlso pt.X < colX + _columns(i).Width Then
                Return i
            End If
        Next
        Return -1
    End Function

    Private Function 列可编辑(colIndex As Integer) As Boolean
        If colIndex < 0 OrElse _columns.Count = 0 Then Return False
        If colIndex >= _columns.Count Then Return False
        Return _columns(colIndex).AllowLabelEdit
    End Function

    Private Function 查找首个可编辑列() As Integer
        For i = 0 To _columns.Count - 1
            If _columns(i).AllowLabelEdit Then Return i
        Next
        Return -1
    End Function

#End Region

#Region "公共方法"

    ''' <summary>滚动视图使指定行可见。</summary>
    Public Sub EnsureVisible(displayRowIndex As Integer)
        If displayRowIndex < 0 OrElse displayRowIndex >= _displayRows.Count Then Return
        If displayRowIndex < _scrollOffset Then
            _scrollOffset = displayRowIndex
            Me.Invalidate()
            Return
        End If
        Dim changed = False
        While _scrollOffset < displayRowIndex
            If 行是否完全可见(displayRowIndex) Then Exit While
            _scrollOffset += 1
            changed = True
        End While
        If changed Then
            校正滚动偏移()
            Me.Invalidate()
        End If
    End Sub

    Private Function 行是否完全可见(rowIndex As Integer) As Boolean
        If rowIndex < _scrollOffset OrElse rowIndex >= _displayRows.Count Then Return False
        Dim y = 获取行Y坐标(rowIndex)
        If y < 0 Then Return False
        Dim contentRect = 获取内容区域()
        Dim bottomLimit = contentRect.Bottom - 获取有效内容下边距()
        Return y + _displayRows(rowIndex).Height <= bottomLimit
    End Function

    ''' <summary>返回指定坐标处的项，无项时返回 Nothing。</summary>
    Public Function GetItemAt(x As Integer, y As Integer) As ListItem
        Dim idx = 命中测试行(New Point(x, y))
        If idx < 0 OrElse idx >= _displayRows.Count Then Return Nothing
        Dim row = _displayRows(idx)
        If row.Type = DisplayRowType.Item Then Return row.Item
        Return Nothing
    End Function

    ''' <summary>返回指定坐标处的显示行索引，无命中时返回 -1。</summary>
    Public Function HitTest(x As Integer, y As Integer) As Integer
        Return 命中测试行(New Point(x, y))
    End Function

    ''' <summary>选中所有项。仅在 MultiSelect=True 时有效。</summary>
    Public Sub SelectAll()
        If Not 允许多选 Then Return
        Dim all As New List(Of Integer)
        For i = 0 To _displayRows.Count - 1
            If _displayRows(i).Type = DisplayRowType.Item Then all.Add(i)
        Next
        设置选中集合(all)
    End Sub

    ''' <summary>清除所有选中。</summary>
    Public Sub ClearSelection()
        设置选中集合(Enumerable.Empty(Of Integer))
    End Sub

    ''' <summary>挂起 UI 更新，在 EndUpdate 之前对集合的修改不会触发重绘。</summary>
    Public Sub BeginUpdate()
        _updateCount += 1
    End Sub

    ''' <summary>恢复 UI 更新并立即重建显示列表。</summary>
    Public Sub EndUpdate()
        _updateCount -= 1
        If _updateCount <= 0 Then
            _updateCount = 0
            重建显示列表()
            Me.Invalidate()
        End If
    End Sub

#End Region

#Region "截断提示"

    Private Sub 确保工具提示已初始化()
        If _truncTooltip IsNot Nothing Then Return
        _truncTooltip = New ToolTip With {.OwnerDraw = True}
        AddHandler _truncTooltip.Draw, AddressOf 工具提示绘制
        AddHandler _truncTooltip.Popup, AddressOf 工具提示弹出
    End Sub

    Private Sub 工具提示绘制(sender As Object, e As DrawToolTipEventArgs)
        Using br As New SolidBrush(背景颜色)
            e.Graphics.FillRectangle(br, e.Bounds)
        End Using
        Using pen As New Pen(项文本颜色)
            e.Graphics.DrawRectangle(pen, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1)
        End Using
        Dim textRect As New Rectangle(5, 3, e.Bounds.Width - 10, e.Bounds.Height - 6)
        TextRenderer.DrawText(e.Graphics, e.ToolTipText, Me.Font, textRect, 项文本颜色,
            TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding)
    End Sub

    Private Sub 工具提示弹出(sender As Object, e As PopupEventArgs)
        Dim maxW As Integer = Math.Max(200, Me.Width)
        Dim sz = TextRenderer.MeasureText(_truncTooltipText, Me.Font, New Size(maxW - 10, Integer.MaxValue),
            TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding)
        e.ToolTipSize = New Size(Math.Min(sz.Width + 12, maxW), sz.Height + 8)
    End Sub

    Private Sub 更新截断提示(pt As Point)
        Dim text = 获取截断文本(pt)
        If text = _truncTooltipText Then Return
        _truncTooltipText = text
        确保工具提示已初始化()
        If String.IsNullOrEmpty(text) Then
            _truncTooltip.Hide(Me)
            _truncTooltipActive = False
        Else
            _truncTooltip.Show(text, Me, pt.X + 10, pt.Y + 20, 10000)
            _truncTooltipActive = True
        End If
    End Sub

    Private Sub 隐藏截断提示()
        If Not _truncTooltipActive Then Return
        _truncTooltipText = ""
        _truncTooltip?.Hide(Me)
        _truncTooltipActive = False
    End Sub

    Private Function 获取截断文本(pt As Point) As String
        If 自动换行 Then Return ""

        Dim rowIdx = 命中测试行(pt)
        If rowIdx < 0 OrElse rowIdx >= _displayRows.Count Then Return ""
        Dim row = _displayRows(rowIdx)
        If row.Type <> DisplayRowType.Item Then Return ""

        Dim item = row.Item
        Dim rowY = 获取行Y坐标(rowIdx)
        If rowY < 0 Then Return ""

        Dim contentRect = 获取内容区域()
        Dim inset = 获取边框内边距()
        Dim scrollW = If(Not _scrollBar.TrackRect.IsEmpty, Me.Width - inset - _scrollBar.VisualLeft, 0)
        Dim availW = contentRect.Width - scrollW
        If _columns.Count > 0 Then availW = Math.Max(availW, 获取总列宽())
        Dim rowRect As New Rectangle(contentRect.X, rowY, availW, row.Height)

        Dim iconAreaW = 获取图标区域宽度(item)
        Dim colXList = 获取列X列表()
        Dim measureFlags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding

        Dim bottomLinesH As Integer = 计算底部文本行高度(item, rowRect.Width)
        Dim upperPartH = rowRect.Height - 项内边距.Vertical - bottomLinesH

        ' 检测子项区域
        Dim colCount = If(_columns.Count > 0, _columns.Count, 1)
        For colIdx = 0 To colCount - 1
            If colIdx >= item.SubItems.Count Then Exit For
            Dim colX = colXList(Math.Min(colIdx, colXList.Count - 1))
            Dim colW As Integer
            If _columns.Count > 0 Then colW = _columns(colIdx).Width Else colW = rowRect.Width
            If pt.X < colX OrElse pt.X >= colX + colW Then Continue For

            Dim sub_ = item.SubItems(colIdx)
            Dim cellW = colW - 项内边距.Horizontal
            If colIdx = 0 Then cellW -= iconAreaW
            If cellW <= 0 Then Return ""

            Dim contentH = 计算子项内容高度(sub_, cellW)
            Dim cellTop = rowRect.Y + 项内边距.Top
            Dim startY = cellTop + Math.Max(0, (upperPartH - contentH) \ 2)
            Dim lineY = startY
            Dim proposed As New Size(Math.Max(1, cellW), Integer.MaxValue)

            ' 主文本
            Dim mf = If(sub_.Font, Me.Font)
            Dim mh = TextRenderer.MeasureText(If(String.IsNullOrEmpty(sub_.Text), "Ag", sub_.Text), mf, proposed, measureFlags).Height
            If pt.Y >= lineY AndAlso pt.Y < lineY + mh Then
                Return 检测文本截断(sub_.Text, mf, cellW, measureFlags)
            End If
            lineY += mh

            ' 附加文本行
            For Each line_ In sub_.ExtraLines
                lineY += 文本行间距
                Dim lf = If(line_.Font, Me.Font)
                Dim lh = TextRenderer.MeasureText(If(String.IsNullOrEmpty(line_.Text), "Ag", line_.Text), lf, proposed, measureFlags).Height
                If pt.Y >= lineY AndAlso pt.Y < lineY + lh Then
                    Return 检测文本截断(line_.Text, lf, cellW, measureFlags)
                End If
                lineY += lh
            Next

            Return ""
        Next

        ' 检测底部附加文本行区域
        If item.BottomLines.Count > 0 Then
            Dim blW = rowRect.Width - 项内边距.Horizontal
            Dim blY = rowRect.Y + 项内边距.Top + upperPartH + 底部文本行间距
            Dim blProp As New Size(Math.Max(1, blW), Integer.MaxValue)
            For Each bl In item.BottomLines
                blY += 文本行间距
                Dim lf = If(bl.Font, Me.Font)
                Dim lh = TextRenderer.MeasureText(If(String.IsNullOrEmpty(bl.Text), "Ag", bl.Text), lf, blProp, measureFlags).Height
                If pt.Y >= blY AndAlso pt.Y < blY + lh Then
                    Return 检测文本截断(bl.Text, lf, blW, measureFlags)
                End If
                blY += lh
            Next
        End If

        Return ""
    End Function

    Private Function 检测文本截断(text As String, font As Font, availW As Integer, flags As TextFormatFlags) As String
        If String.IsNullOrEmpty(text) Then Return ""
        Dim fullW = TextRenderer.MeasureText(text, font, New Size(Integer.MaxValue, Integer.MaxValue), flags).Width
        If fullW > availW Then Return text
        Return ""
    End Function

#End Region

#Region "悬停动画"

    Private Sub 更新悬停(newIndex As Integer)
        If newIndex = _hoverRowIndex Then Return
        Dim oldIndex As Integer = _hoverRowIndex
        _hoverRowIndex = newIndex

        If 动画时长 <= 0 OrElse Not Me.IsHandleCreated Then
            _hoverAnimActive = False
            Me.Invalidate()
            Return
        End If

        If newIndex < 0 Then
            _hoverAnimActive = False
            Me.Invalidate()
            Return
        End If

        Dim newY = 获取行Y坐标(newIndex)
        Dim newH = _displayRows(newIndex).Height
        If newY < 0 Then
            _hoverAnimActive = False
            Me.Invalidate()
            Return
        End If

        If oldIndex >= 0 AndAlso _hoverAnimActive Then
            Dim t As Single = _hoverAnim.Progress
            _hoverAnimFromY += (_hoverAnimToY - _hoverAnimFromY) * t
            _hoverAnimFromH += (_hoverAnimToH - _hoverAnimFromH) * t
        Else
            _hoverAnimFromY = newY
            _hoverAnimFromH = newH
        End If

        _hoverAnimToY = newY
        _hoverAnimToH = newH
        _hoverAnimActive = True
        _hoverAnim.Duration = 动画时长
        _hoverAnim.SetImmediate(0)
        _hoverAnim.AnimateTo(1)
    End Sub

#End Region

#Region "生命周期"

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        取消标签编辑等待()
        If _editTextBox IsNot Nothing Then 结束标签编辑(True)
        If _columns.Count = 0 Then
            全部项高度缓存失效()
            重建显示列表()
        End If
        校正横向滚动偏移()
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        全部项高度缓存失效()
        重建显示列表()
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        全部项高度缓存失效()
        重建显示列表()
        Me.Invalidate()
    End Sub

    Friend Sub 释放资源()
        _hoverAnim?.Dispose()
        取消标签编辑等待()
        If _editTextBox IsNot Nothing Then
            RemoveHandler _editTextBox.PreviewKeyDown, AddressOf 编辑框预览按键
            RemoveHandler _editTextBox.LostFocus, AddressOf 编辑框失焦
            RemoveHandler _editTextBox.KeyDown, AddressOf 编辑框按键
            _editTextBox.Dispose()
            _editTextBox = Nothing
        End If
        If _labelEditTimer IsNot Nothing Then
            RemoveHandler _labelEditTimer.Tick, AddressOf 标签编辑计时器触发
            _labelEditTimer.Dispose()
            _labelEditTimer = Nothing
        End If
        If _truncTooltip IsNot Nothing Then
            RemoveHandler _truncTooltip.Draw, AddressOf 工具提示绘制
            RemoveHandler _truncTooltip.Popup, AddressOf 工具提示弹出
            _truncTooltip.Dispose()
            _truncTooltip = Nothing
        End If
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