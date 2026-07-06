Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Drawing.Design
Imports System.Drawing.Drawing2D
Imports Vortice.Direct2D1

<DefaultEvent("SelectedIndexChanged")>
Public Class UltraDetailListView
    Implements V3_IGpuRenderable, V3_IGpuInvalidationSource

    Public Event SelectedIndexChanged As EventHandler
    Public Event ItemClick As EventHandler(Of ListItemEventArgs)
    Public Event ItemDoubleClick As EventHandler(Of ListItemEventArgs)
    Public Event ItemOrderChanged As EventHandler
    Public Event AfterLabelEdit As EventHandler(Of LabelEditEventArgs)

    Public Class ListItemEventArgs
        Inherits EventArgs
        Public ReadOnly Property Item As ListItem
        Public ReadOnly Property DisplayRowIndex As Integer
        Public ReadOnly Property ColumnIndex As Integer
        Public Sub New(item As ListItem, displayRowIndex As Integer)
            Me.New(item, displayRowIndex, -1)
        End Sub
        Public Sub New(item As ListItem, displayRowIndex As Integer, columnIndex As Integer)
            Me.Item = item
            Me.DisplayRowIndex = displayRowIndex
            Me.ColumnIndex = columnIndex
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
        Private _text As String = ""

        <DefaultValue(""), Description("文本内容")>
        Public Property Text As String
            Get
                Return _text
            End Get
            Set(value As String)
                value = If(value, "")
                If String.Equals(_text, value, StringComparison.Ordinal) Then Return
                _text = value
                If Owner IsNot Nothing Then Owner.InvalidateItemTextResources()
            End Set
        End Property

        Friend Property Owner As UltraDetailListView

        Private _font As Font = Nothing
        <Description("字体，留空时使用控件默认字体")>
        Public Property Font As Font
            Get
                Return _font
            End Get
            Set(value As Font)
                If Object.ReferenceEquals(_font, value) Then Return
                _font = value
                If Owner IsNot Nothing Then Owner.InvalidateItemFontResources()
            End Set
        End Property

        <DefaultValue(GetType(Color), ""), Description("前景色，留空时使用控件默认项文本颜色")>
        Public Property ForeColor As Color = Color.Empty

        Friend CachedHeight As Integer = -1
        Friend CachedMeasureVersion As Integer = -1
        Friend CachedMeasureWidth As Integer = -1
        Friend CachedMeasureFlags As TextFormatFlags
        Friend CachedMeasureFontHash As Integer = 0

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
        Private _text As String = ""

        <DefaultValue(""), Description("主文本")>
        Public Property Text As String
            Get
                Return _text
            End Get
            Set(value As String)
                value = If(value, "")
                If String.Equals(_text, value, StringComparison.Ordinal) Then Return
                _text = value
                If Owner IsNot Nothing Then Owner.InvalidateItemTextResources()
            End Set
        End Property

        Friend Property Owner As UltraDetailListView

        Private _font As Font = Nothing
        <Description("字体，留空时使用控件默认字体")>
        Public Property Font As Font
            Get
                Return _font
            End Get
            Set(value As Font)
                If Object.ReferenceEquals(_font, value) Then Return
                _font = value
                If Owner IsNot Nothing Then Owner.InvalidateItemFontResources()
            End Set
        End Property

        <DefaultValue(GetType(Color), ""), Description("前景色，留空时使用控件默认项文本颜色")>
        Public Property ForeColor As Color = Color.Empty

        ''' <summary>同一单元格内主文本下方的附加文本行。</summary>
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
         Editor(GetType(Design.CollectionEditor), GetType(UITypeEditor)),
         Description("附加文本行")>
        Public Property ExtraLines As New List(Of TextLine)

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Tag As Object = Nothing

        Friend CachedContentHeight As Integer = -1
        Friend CachedContentVersion As Integer = -1
        Friend CachedContentWidth As Integer = -1
        Friend CachedContentFlags As TextFormatFlags
        Friend CachedContentFontHash As Integer = 0
        Friend CachedMainHeight As Integer = -1
        Friend CachedMainVersion As Integer = -1
        Friend CachedMainWidth As Integer = -1
        Friend CachedMainFlags As TextFormatFlags
        Friend CachedMainFontHash As Integer = 0

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
        ''' <summary>计算项高度时的"上部"内容高度（不含 BottomLines、不含 ItemPadding.Vertical）。</summary>
        Friend CachedUpperPartHeight As Integer = 0
        ''' <summary>BottomLines 整体所占高度（含 BottomLinesSpacing 与逐行间距）。</summary>
        Friend CachedBottomLinesHeight As Integer = 0
        ''' <summary>用于无效化校验：缓存计算时所依据的 (列宽签名 + DPI + Font hash) 的 hash。</summary>
        Friend CachedSignature As Long = 0

        Public Sub New()
        End Sub
        Public Sub New(ParamArray subItems() As ListSubItem)
            Me.SubItems.AddRange(subItems)
        End Sub
        Public Sub InvalidateCache()
            CachedHeight = -1
            CachedUpperPartHeight = 0
            CachedBottomLinesHeight = 0
            CachedSignature = 0
            For Each subItem In SubItems
                InvalidateSubItemMeasureCache(subItem)
            Next
            For Each line In BottomLines
                InvalidateTextLineMeasureCache(line)
            Next
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

        <DefaultValue(GetType(Color), ""), Description("分组文字颜色，留空时使用控件的 GroupForeColor")>
        Public Property ForeColor As Color = Color.Empty

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
        Private _text As String = ""
        Private _width As Integer = 100
        Private _headerPadding As Padding = New Padding(10, 0, 0, 0)
        Private _wordWrapHeightFixed As Boolean = False

        Friend Property Owner As UltraDetailListView

        <DefaultValue(""), Description("列标题文本")>
        Public Property Text As String
            Get
                Return _text
            End Get
            Set(value As String)
                value = If(value, "")
                If String.Equals(_text, value, StringComparison.Ordinal) Then Return
                _text = value
                If Owner IsNot Nothing Then Owner.InvalidateColumnVisual()
            End Set
        End Property

        <DefaultValue(100), Description("列宽度")>
        Public Property Width As Integer
            Get
                Return _width
            End Get
            Set(value As Integer)
                If _width = value Then Return
                _width = value
                If Owner IsNot Nothing Then Owner.InvalidateColumnLayout()
            End Set
        End Property

        <DefaultValue(GetType(Padding), "10, 0, 0, 0"), Description("列标题文字内边距")>
        Public Property HeaderPadding As Padding
            Get
                Return _headerPadding
            End Get
            Set(value As Padding)
                If _headerPadding.Equals(value) Then Return
                _headerPadding = value
                If Owner IsNot Nothing Then Owner.InvalidateColumnVisual()
            End Set
        End Property

        <DefaultValue(False), Description("是否允许慢速单击编辑此列的子项主文本")>
        Public Property AllowLabelEdit As Boolean = False

        <DefaultValue(False), Description("自动换行时是否固定高度（不参与项高度计算）；启用后此列内容超出由其他列决定的行高时以省略号截断")>
        Public Property WordWrapHeightFixed As Boolean
            Get
                Return _wordWrapHeightFixed
            End Get
            Set(value As Boolean)
                If _wordWrapHeightFixed = value Then Return
                _wordWrapHeightFixed = value
                If Owner IsNot Nothing Then Owner.InvalidateColumnLayout()
            End Set
        End Property

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
            UltraDetailListView.添加范围时挂起更新(_owner, collection, AddressOf Add)
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As ListColumn)
            If item IsNot Nothing Then item.Owner = _owner
            MyBase.InsertItem(index, item)
            _owner.全部项高度缓存失效()
            _owner.RefreshItems()
        End Sub
        Protected Overrides Sub RemoveItem(index As Integer)
            Dim oldItem = Me(index)
            If oldItem IsNot Nothing AndAlso Object.ReferenceEquals(oldItem.Owner, _owner) Then oldItem.Owner = Nothing
            MyBase.RemoveItem(index)
            _owner.全部项高度缓存失效()
            _owner.RefreshItems()
        End Sub
        Protected Overrides Sub SetItem(index As Integer, item As ListColumn)
            Dim oldItem = Me(index)
            If oldItem IsNot Nothing AndAlso Object.ReferenceEquals(oldItem.Owner, _owner) Then oldItem.Owner = Nothing
            If item IsNot Nothing Then item.Owner = _owner
            MyBase.SetItem(index, item)
            _owner.全部项高度缓存失效()
            _owner.RefreshItems()
        End Sub
        Protected Overrides Sub ClearItems()
            For Each oldItem In Me
                If oldItem IsNot Nothing AndAlso Object.ReferenceEquals(oldItem.Owner, _owner) Then oldItem.Owner = Nothing
            Next
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
            UltraDetailListView.添加范围时挂起更新(_owner, collection, AddressOf Add)
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
            UltraDetailListView.添加范围时挂起更新(_owner, collection, AddressOf Add)
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As ListItem)
            _owner.AttachItem(item)
            MyBase.InsertItem(index, item)
            _owner.RefreshItems()
        End Sub
        Protected Overrides Sub RemoveItem(index As Integer)
            Dim oldItem = Me(index)
            _owner.DetachItem(oldItem)
            MyBase.RemoveItem(index)
            _owner.RefreshItems()
        End Sub
        Protected Overrides Sub SetItem(index As Integer, item As ListItem)
            Dim oldItem = Me(index)
            _owner.DetachItem(oldItem)
            _owner.AttachItem(item)
            MyBase.SetItem(index, item)
            _owner.RefreshItems()
        End Sub
        Protected Overrides Sub ClearItems()
            For Each oldItem In Me
                _owner.DetachItem(oldItem)
            Next
            MyBase.ClearItems()
            _owner.RefreshItems()
        End Sub
    End Class

#End Region

#Region "构造"

    Public Sub New()
        InitializeComponent()
        _hoverAnim.DirtyProvider = AddressOf 悬停动画脏区
    End Sub

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
        ''' <summary>当 Type=Item 时，此项在 _items 中的索引；GroupHeader 时为 -1。</summary>
        Public ItemIndex As Integer
        ''' <summary>该行内容顶部相对内容区域 Top（已含本行前 spacing）的累计 Y。
        ''' 由 重建显示行布局() 一次性计算，命中测试与定位走二分。</summary>
        Public Top As Integer
        ''' <summary>本行前的 spacing 高度（项间距 + 分组前后的 ContentPadding）。</summary>
        Public Spacing As Integer
    End Class

    Private _displayRows As New List(Of DisplayRow)
    ''' <summary>所有显示行 Top 的副本，仅 Item 行用于二分。结构见 重建显示行布局()。</summary>
    Private _rowTops As Integer() = Array.Empty(Of Integer)()
    ''' <summary>所有显示行 (Top + Spacing + Height) = 下一行可用 Y 起点。</summary>
    Private _rowBottoms As Integer() = Array.Empty(Of Integer)()

    Private Sub 重建显示列表()
        取消标签编辑等待()
        If _editTextBox IsNot Nothing Then 结束标签编辑(True)

        ' 记录当前选中的项引用，用于重建后重新映射
        Dim prevSelectedItems As List(Of ListItem) = Nothing
        Dim prevAnchorItem As ListItem = Nothing
        If _selectedIndices.Count > 0 OrElse _selectionAnchor >= 0 Then
            prevSelectedItems = New List(Of ListItem)(_selectedIndices.Count)
            For Each idx In _selectedIndices
                If idx >= 0 AndAlso idx < _displayRows.Count AndAlso _displayRows(idx).Type = DisplayRowType.Item Then
                    prevSelectedItems.Add(_displayRows(idx).Item)
                End If
            Next
            If _selectionAnchor >= 0 AndAlso _selectionAnchor < _displayRows.Count AndAlso
               _displayRows(_selectionAnchor).Type = DisplayRowType.Item Then
                prevAnchorItem = _displayRows(_selectionAnchor).Item
            End If
        End If

        _displayRows.Clear()
        Dim groupHeaderH As Integer = Dpi(分组标题高度)

        ' 按 GroupName 分桶（O(N)），避免按分组重新遍历整个 _items 集合 (原 O(G·N))
        Dim ungrouped As New List(Of Integer)(_items.Count)
        Dim grouped As Dictionary(Of String, List(Of Integer)) = Nothing
        If _groups.Count > 0 Then
            grouped = New Dictionary(Of String, List(Of Integer))(_groups.Count, StringComparer.Ordinal)
            For Each grp In _groups
                If Not grouped.ContainsKey(grp.Name) Then grouped(grp.Name) = New List(Of Integer)()
            Next
        End If
        For i As Integer = 0 To _items.Count - 1
            Dim it = _items(i)
            Dim gn = it.GroupName
            If String.IsNullOrEmpty(gn) Then
                ungrouped.Add(i)
            ElseIf grouped IsNot Nothing AndAlso grouped.ContainsKey(gn) Then
                grouped(gn).Add(i)
            Else
                ungrouped.Add(i)
            End If
        Next

        ' 输出无分组项
        For Each idx In ungrouped
            Dim it = _items(idx)
            _displayRows.Add(New DisplayRow With {
                .Type = DisplayRowType.Item,
                .Item = it,
                .ItemIndex = idx,
                .Height = 计算项高度(it)
            })
        Next

        ' 输出各分组
        If grouped IsNot Nothing Then
            For Each grp In _groups
                _displayRows.Add(New DisplayRow With {
                    .Type = DisplayRowType.GroupHeader,
                    .Group = grp,
                    .ItemIndex = -1,
                    .Height = groupHeaderH
                })
                If Not grp.IsCollapsed Then
                    For Each idx In grouped(grp.Name)
                        Dim it = _items(idx)
                        _displayRows.Add(New DisplayRow With {
                            .Type = DisplayRowType.Item,
                            .Item = it,
                            .ItemIndex = idx,
                            .Height = 计算项高度(it)
                        })
                    Next
                End If
            Next
        End If

        ' 重建 (Spacing/Top) 布局缓存 —— 命中测试与可见行估算依赖此结构
        重建显示行布局()

        ' 重新映射选中索引：用 Item -> 显示行索引 一次性查询表，避免 O(N²)
        _selectedIndices.Clear()
        _selectedMin = -1
        If prevSelectedItems IsNot Nothing AndAlso (prevSelectedItems.Count > 0 OrElse prevAnchorItem IsNot Nothing) Then
            Dim itemToRow As New Dictionary(Of ListItem, Integer)(_displayRows.Count)
            For i As Integer = 0 To _displayRows.Count - 1
                If _displayRows(i).Type = DisplayRowType.Item Then itemToRow(_displayRows(i).Item) = i
            Next
            For Each it In prevSelectedItems
                Dim ri As Integer
                If itemToRow.TryGetValue(it, ri) Then
                    If _selectedIndices.Add(ri) Then
                        If _selectedMin = -1 OrElse ri < _selectedMin Then _selectedMin = ri
                    End If
                End If
            Next
            _selectionAnchor = -1
            If prevAnchorItem IsNot Nothing Then
                Dim ai As Integer
                If itemToRow.TryGetValue(prevAnchorItem, ai) Then _selectionAnchor = ai
            End If
        End If

        校正滚动偏移()
        校正横向滚动偏移()
        _hoverAnimActive = False
        _hoverRowIndex = -1
        _columnXDirty = True
    End Sub

    ''' <summary>计算每行 Top/Spacing 并填充 _rowTops/_rowBottoms 数组。
    ''' Top 表示"行内容（不含前 spacing）"相对内容区域上沿（已减去 _scrollOffset 的 0 起点）的位置，
    ''' 因为 Spacing 与 _scrollOffset 相关，重建显示行布局() 必须在 _scrollOffset 变化后再次调用以更新偏移敏感的 spacing。
    ''' 但为了能进行二分查找，我们选择"假设 _scrollOffset = 0"的稳定坐标系，命中测试时再加上当前 _scrollOffset 决定的偏移量。</summary>
    Private Sub 重建显示行布局()
        Dim n = _displayRows.Count
        If _rowTops.Length < n Then
            ReDim _rowTops(Math.Max(n, 16) - 1)
            ReDim _rowBottoms(Math.Max(n, 16) - 1)
        End If
        Dim spacing As Integer = Dpi(项间距)
        Dim contentTopPad As Integer = 获取有效内容上边距()
        Dim contentBotPad As Integer = 获取有效内容下边距()
        Dim y As Integer = 0
        For i As Integer = 0 To n - 1
            Dim row = _displayRows(i)
            Dim s As Integer = 0
            If i > 0 Then
                s = spacing
                Dim prev = _displayRows(i - 1)
                If prev.Type = DisplayRowType.GroupHeader AndAlso row.Type = DisplayRowType.Item Then
                    s += contentTopPad
                ElseIf prev.Type = DisplayRowType.Item AndAlso row.Type = DisplayRowType.GroupHeader Then
                    s += contentBotPad
                End If
            End If
            row.Spacing = s
            y += s
            row.Top = y
            _rowTops(i) = y
            y += row.Height
            _rowBottoms(i) = y
        Next
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
        InvalidateMeasureCache()
        AttachAllItems()
        重建显示列表()
        请求V3渲染()
    End Sub

#End Region

#Region "辅助方法"

    Private Shared Sub 添加范围时挂起更新(Of T)(owner As UltraDetailListView, collection As IEnumerable(Of T), addAction As Action(Of T))
        If owner Is Nothing OrElse collection Is Nothing OrElse addAction Is Nothing Then Return
        owner.BeginUpdate()
        Try
            For Each entry In collection
                addAction(entry)
            Next
        Finally
            owner.EndUpdate()
        End Try
    End Sub

    Private Structure TextMeasureKey
        Implements IEquatable(Of TextMeasureKey)

        Public Text As String
        Public FontHash As Integer
        Public ProposedWidth As Integer
        Public ProposedHeight As Integer
        Public Flags As TextFormatFlags
        Public DpiX96 As Integer
        Public Version As Integer

        Public Overloads Function Equals(other As TextMeasureKey) As Boolean Implements IEquatable(Of TextMeasureKey).Equals
            Return FontHash = other.FontHash AndAlso
                   ProposedWidth = other.ProposedWidth AndAlso
                   ProposedHeight = other.ProposedHeight AndAlso
                   Flags = other.Flags AndAlso
                   DpiX96 = other.DpiX96 AndAlso
                   Version = other.Version AndAlso
                   String.Equals(Text, other.Text, StringComparison.Ordinal)
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            Return TypeOf obj Is TextMeasureKey AndAlso Equals(DirectCast(obj, TextMeasureKey))
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return System.HashCode.Combine(Text, FontHash, ProposedWidth, ProposedHeight, Flags, DpiX96, Version)
        End Function
    End Structure

    Private _measureVersion As Integer
    Private ReadOnly _textMeasureCache As New Dictionary(Of TextMeasureKey, Size)(512)
    Private Const MaxTextMeasureCacheEntries As Integer = 4096

    Friend Sub InvalidateItemTextResources()
        InvalidateMeasureCache()
        全部项高度缓存失效()
        If _updateCount <= 0 Then
            重建显示列表()
            请求V3渲染()
        End If
    End Sub

    Friend Sub InvalidateColumnLayout()
        InvalidateMeasureCache()
        全部项高度缓存失效()
        _columnXDirty = True
        If _updateCount <= 0 Then
            重建显示列表()
            请求V3渲染()
        End If
    End Sub

    Friend Sub InvalidateColumnVisual()
        InvalidateV3TextResources()
        If _updateCount <= 0 Then 请求V3渲染()
    End Sub

    Private Sub InvalidateV3TextResources()
        D3D_RenderCore.InvalidateExistingTextResources(Me)
    End Sub

    Private Function AttachItem(item As ListItem) As Boolean
        If item Is Nothing Then Return False
        Dim changed As Boolean = False
        For Each subItem In item.SubItems
            changed = AttachSubItem(subItem) OrElse changed
        Next
        For Each line In item.BottomLines
            changed = AttachTextLine(line) OrElse changed
        Next
        If changed Then item.InvalidateCache()
        Return changed
    End Function

    Private Sub AttachAllItems()
        For Each item In _items
            AttachItem(item)
        Next
    End Sub

    Private Sub DetachItem(item As ListItem)
        If item Is Nothing Then Return
        For Each subItem In item.SubItems
            DetachSubItem(subItem)
        Next
        For Each line In item.BottomLines
            DetachTextLine(line)
        Next
        item.InvalidateCache()
    End Sub

    Private Function AttachSubItem(subItem As ListSubItem) As Boolean
        If subItem Is Nothing Then Return False
        Dim changed As Boolean = Not Object.ReferenceEquals(subItem.Owner, Me)
        subItem.Owner = Me
        For Each line In subItem.ExtraLines
            changed = AttachTextLine(line) OrElse changed
        Next
        Return changed
    End Function

    Private Sub DetachSubItem(subItem As ListSubItem)
        If subItem Is Nothing Then Return
        If Object.ReferenceEquals(subItem.Owner, Me) Then subItem.Owner = Nothing
        For Each line In subItem.ExtraLines
            DetachTextLine(line)
        Next
    End Sub

    Private Function AttachTextLine(line As TextLine) As Boolean
        If line Is Nothing Then Return False
        Dim changed As Boolean = Not Object.ReferenceEquals(line.Owner, Me)
        line.Owner = Me
        Return changed
    End Function

    Private Sub DetachTextLine(line As TextLine)
        If line IsNot Nothing AndAlso Object.ReferenceEquals(line.Owner, Me) Then line.Owner = Nothing
    End Sub

    Private Sub InvalidateMeasureCache()
        _measureVersion += 1
        _textMeasureCache.Clear()
    End Sub

    Private Function GetMeasureFontHash(font As Font) As Integer
        If font Is Nothing Then Return 0
        Return System.HashCode.Combine(font.FontFamily.Name, font.Style, font.SizeInPoints, font.Unit)
    End Function

    Private Shared Sub InvalidateTextLineMeasureCache(line As TextLine)
        If line Is Nothing Then Return
        line.CachedHeight = -1
        line.CachedMeasureVersion = -1
        line.CachedMeasureWidth = -1
        line.CachedMeasureFlags = 0
        line.CachedMeasureFontHash = 0
    End Sub

    Private Shared Sub InvalidateSubItemMeasureCache(subItem As ListSubItem)
        If subItem Is Nothing Then Return
        subItem.CachedContentHeight = -1
        subItem.CachedContentVersion = -1
        subItem.CachedContentWidth = -1
        subItem.CachedContentFlags = 0
        subItem.CachedContentFontHash = 0
        subItem.CachedMainHeight = -1
        subItem.CachedMainVersion = -1
        subItem.CachedMainWidth = -1
        subItem.CachedMainFlags = 0
        subItem.CachedMainFontHash = 0
        For Each line In subItem.ExtraLines
            InvalidateTextLineMeasureCache(line)
        Next
    End Sub

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            请求V3渲染()
        End If
    End Sub

    Private Sub SetValueWithRebuild(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            重建显示列表()
            请求V3渲染()
        End If
    End Sub

    Private Sub SetValueWithFullRebuild(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            全部项高度缓存失效()
            重建显示列表()
            请求V3渲染()
        End If
    End Sub

    Private Function DpiScale() As Single
        Return V3_DpiContext.FromControl(Me).Scale
    End Function

    Private Function Dpi(value As Integer) As Integer
        Return CInt(value * DpiScale())
    End Function

    Private Function Dpi(value As Padding) As Padding
        Dim s As Single = DpiScale()
        Return New Padding(CInt(value.Left * s), CInt(value.Top * s), CInt(value.Right * s), CInt(value.Bottom * s))
    End Function

    Private Function Dpi(value As Size) As Size
        Dim s As Single = DpiScale()
        Return New Size(CInt(value.Width * s), CInt(value.Height * s))
    End Function

    Private Function 获取文本格式标志() As TextFormatFlags
        Dim flags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding
        Return If(自动换行, flags Or TextFormatFlags.WordBreak, flags Or TextFormatFlags.EndEllipsis)
    End Function

    Private Function 截断文本到宽度(text As String, font As Font, availW As Integer) As String
        If String.IsNullOrEmpty(text) Then Return ""
        If availW <= 0 Then Return TruncationSuffix.TrimStart()

        Dim measureFlags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
        Dim fullW As Integer = 测量文本宽度(text, font, measureFlags)
        If fullW <= availW Then Return text

        Dim suffix As String = TruncationSuffix
        Dim suffixW As Integer = 测量文本宽度(suffix, font, measureFlags)
        If suffixW >= availW Then Return TruncationSuffix.TrimStart()

        Dim lo As Integer = 0
        Dim hi As Integer = text.Length
        While lo < hi
            Dim mid As Integer = (lo + hi + 1) \ 2
            If 测量文本宽度(text.Substring(0, mid), font, measureFlags) + suffixW <= availW Then
                lo = mid
            Else
                hi = mid - 1
            End If
        End While
        Return text.Substring(0, lo) & suffix
    End Function

    Private Function 测量文本(text As String, font As Font, proposedSize As Size, flags As TextFormatFlags) As Size
        If String.IsNullOrEmpty(text) Then Return Size.Empty
        Dim useFont As Font = If(font IsNot Nothing, font, Me.Font)
        If useFont Is Nothing Then Return Size.Empty

        Dim dpiS As Single = DpiScale()
        Dim key As New TextMeasureKey With {
            .Text = text,
            .FontHash = GetMeasureFontHash(useFont),
            .ProposedWidth = proposedSize.Width,
            .ProposedHeight = proposedSize.Height,
            .Flags = flags,
            .DpiX96 = CInt(Math.Round(96.0F * dpiS)),
            .Version = _measureVersion
        }

        Dim cached As Size = Size.Empty
        If _textMeasureCache.TryGetValue(key, cached) Then Return cached

        Dim measured = D3D_TextInterop.MeasureText(text, useFont, proposedSize, flags, dpiS)
        If _textMeasureCache.Count >= MaxTextMeasureCacheEntries Then _textMeasureCache.Clear()
        _textMeasureCache(key) = measured
        Return measured
    End Function

    Private Function 测量文本高度(text As String, font As Font, proposedSize As Size, flags As TextFormatFlags) As Integer
        Return 测量文本(text, font, proposedSize, flags).Height
    End Function

    Private Function 测量文本宽度(text As String, font As Font, flags As TextFormatFlags) As Integer
        If String.IsNullOrEmpty(text) Then Return 0
        Return 测量文本(text, font, New Size(Integer.MaxValue, Integer.MaxValue),
                         flags Or TextFormatFlags.SingleLine).Width
    End Function

    Private Function 获取图标区域宽度(item As ListItem) As Integer
        If item.Icon IsNot Nothing AndAlso 图标尺寸.Width > 0 AndAlso 图标尺寸.Height > 0 Then
            Return Dpi(图标尺寸).Width + Dpi(图标间距)
        End If
        Return 0
    End Function

    Private Function 计算底部文本行高度(item As ListItem, rowWidth As Integer) As Integer
        If item.BottomLines.Count = 0 Then Return 0
        Dim blW As Integer = rowWidth - Dpi(项内边距).Horizontal
        Dim flags As TextFormatFlags = 获取文本格式标志()
        Dim proposed As New Size(Math.Max(1, blW), Integer.MaxValue)
        Dim h As Integer = Dpi(底部文本行间距)
        For Each bl In item.BottomLines
            h += Dpi(文本行间距)
            h += 测量文本行高度缓存(bl, proposed.Width, flags)
        Next
        Return h
    End Function

    Private Function 测量文本行高度缓存(line As TextLine, width As Integer, flags As TextFormatFlags) As Integer
        If line Is Nothing Then
            Return 测量文本高度("", Me.Font, New Size(Math.Max(1, width), Integer.MaxValue), flags)
        End If

        Dim f As Font = If(line.Font, Me.Font)
        Dim fontHash = GetMeasureFontHash(f)
        width = Math.Max(1, width)
        If line.CachedHeight >= 0 AndAlso
           line.CachedMeasureVersion = _measureVersion AndAlso
           line.CachedMeasureWidth = width AndAlso
           line.CachedMeasureFlags = flags AndAlso
           line.CachedMeasureFontHash = fontHash Then
            Return line.CachedHeight
        End If

        Dim h = 测量文本高度(line.Text, f, New Size(width, Integer.MaxValue), flags)
        line.CachedHeight = h
        line.CachedMeasureVersion = _measureVersion
        line.CachedMeasureWidth = width
        line.CachedMeasureFlags = flags
        line.CachedMeasureFontHash = fontHash
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

    ''' <summary>取/创建 D2D Solid 画刷（仅在当前 RT 上使用，复用按颜色键）。</summary>

    Private Function 获取项焦点圆角半径(rect As RectangleF) As Single
        If 项焦点圆角半径 <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return 0.0F
        Dim radius As Single = 项焦点圆角半径 * DpiScale()
        Return Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2.0F)
    End Function

    ''' <summary>D2D 绘制列标题背景与分隔线（不绘文字）。</summary>

    ''' <summary>D2D 绘制分组行背景、贝塞尔三角箭头、底部分隔线（不绘组名文字）。</summary>

    ''' <summary>把 GDI+ GraphicsPath 转换为 D2D PathGeometry。仅支持线段与三次贝塞尔，足够本控件使用。</summary>
    Private Shared Function 路径转D2D几何(path As GraphicsPath) As Vortice.Direct2D1.ID2D1PathGeometry
        If path Is Nothing OrElse path.PointCount = 0 Then Return Nothing
        Dim pts() As PointF = path.PathPoints
        Dim types() As Byte = path.PathTypes
        Dim geo = D3D_RenderCore.DeviceManager.D2DFactory.CreatePathGeometry()
        Dim sink = geo.Open()
        Try
            Dim figureOpen As Boolean = False
            Dim i As Integer = 0
            While i < pts.Length
                Dim t As Byte = types(i)
                Dim ptType As Byte = t And &H7
                If ptType = 0 Then ' Start
                    If figureOpen Then
                        sink.EndFigure(Vortice.Direct2D1.FigureEnd.Open)
                        figureOpen = False
                    End If
                    sink.BeginFigure(New System.Numerics.Vector2(pts(i).X, pts(i).Y), Vortice.Direct2D1.FigureBegin.Filled)
                    figureOpen = True
                    i += 1
                ElseIf ptType = 1 Then ' Line
                    sink.AddLine(New System.Numerics.Vector2(pts(i).X, pts(i).Y))
                    Dim closed As Boolean = (t And &H80) <> 0
                    i += 1
                    If closed AndAlso figureOpen Then
                        sink.EndFigure(Vortice.Direct2D1.FigureEnd.Closed)
                        figureOpen = False
                    End If
                ElseIf ptType = 3 Then ' Bezier (3 points)
                    If i + 2 < pts.Length Then
                        Dim seg As New Vortice.Direct2D1.BezierSegment With {
                            .Point1 = New System.Numerics.Vector2(pts(i).X, pts(i).Y),
                            .Point2 = New System.Numerics.Vector2(pts(i + 1).X, pts(i + 1).Y),
                            .Point3 = New System.Numerics.Vector2(pts(i + 2).X, pts(i + 2).Y)}
                        sink.AddBezier(seg)
                        Dim closeFlag As Byte = types(i + 2)
                        i += 3
                        If (closeFlag And &H80) <> 0 AndAlso figureOpen Then
                            sink.EndFigure(Vortice.Direct2D1.FigureEnd.Closed)
                            figureOpen = False
                        End If
                    Else
                        Exit While
                    End If
                Else
                    i += 1
                End If
            End While
            If figureOpen Then sink.EndFigure(Vortice.Direct2D1.FigureEnd.Closed)
            sink.Close()
        Finally
            sink.Dispose()
        End Try
        Return geo
    End Function

    ''' <summary>D2D 绘制更多指示器形状。当前仅保留符号层，背景保持透明。</summary>

    ''' <summary>D2D 绘制拖选框（半透明填充 + 边框）。</summary>

    ''' <summary>D2D 绘制拖动排序指示线。</summary>

    ''' <summary>D2D 绘制全部行的形状层（背景/选中/悬停/Checked 边框/分组背景与箭头/更多指示器渐变）。</summary>

    Private Sub 释放GDI缓存()
        _moreSymbolFont?.Dispose()
        _moreSymbolFont = Nothing
        _moreSymbolFontKey = 0F
    End Sub

    ''' <summary>确保 _columnXCache 是最新的。所有需要列 X 的路径调用此方法后用 _columnXCache 数组与 _columnXCount。
    ''' 通过比较"期望首列 X"与缓存首列 X 自检失效，避免外部分散地维护脏标记。</summary>
    Private Sub 确保列X缓存()
        Dim n = _columns.Count
        Dim contentRect = 获取内容区域()
        Dim expectedFirst As Integer = If(n = 0, contentRect.X, contentRect.X - _hScrollOffset)
        If Not _columnXDirty AndAlso _columnXCount = Math.Max(n, 1) AndAlso
           _columnXCache.Length >= _columnXCount AndAlso _columnXCache(0) = expectedFirst Then
            Return
        End If
        If _columnXCache.Length < Math.Max(n, 1) Then
            ReDim _columnXCache(Math.Max(n, 4) - 1)
        End If
        If n = 0 Then
            _columnXCache(0) = contentRect.X
            _columnXCount = 1
        Else
            Dim x As Integer = contentRect.X - _hScrollOffset
            For i As Integer = 0 To n - 1
                _columnXCache(i) = x
                x += _columns(i).Width
            Next
            _columnXCount = n
        End If
        _columnXDirty = False
    End Sub

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

    Private _backgroundSource As Control = Nothing
    ''' <summary>
    ''' 背景采样源（超容器背景映射）。设置后控件会通过 V3 背景图采样此控件作为底图，
    ''' 从而实现跨越任意层级的"穿透显示"效果；为 Nothing 时不进行背景采样。
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
                _backgroundSource = D3D_BackgroundPenetration.SetBackgroundSource(Me, _backgroundSource, value)
                请求V3渲染()
            End If
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

    Private 项焦点圆角半径 As Integer = 0
    <Category("LakeUI"), Description("项焦点背景圆角半径"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property ItemCornerRadius As Integer
        Get
            Return 项焦点圆角半径
        End Get
        Set(value As Integer)
            SetValue(项焦点圆角半径, Math.Max(0, value))
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
                请求V3渲染()
            End If
        End Set
    End Property

    Private Function 获取有效内容上边距() As Integer
        Return Dpi(If(内容边距.Top < 0, 项内边距.Top, 内容边距.Top))
    End Function

    Private Function 获取有效内容下边距() As Integer
        Return Dpi(If(内容边距.Bottom < 0, 项内边距.Bottom, 内容边距.Bottom))
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

    Private Shared ReadOnly 默认滚动条轨道颜色 As Color = Color.FromArgb(20, 20, 20)
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

    Private Shared ReadOnly 默认滚动条滑块颜色 As Color = Color.FromArgb(80, 80, 80)
    Private 滚动条滑块颜色 As Color = 默认滚动条滑块颜色
    <Category("LakeUI"), Description("滚动条滑块颜色"), Browsable(True)>
    Public Property ScrollBarThumbColor As Color
        Get
            Return 滚动条滑块颜色
        End Get
        Set(value As Color)
            SetValue(滚动条滑块颜色, value)
        End Set
    End Property

    Private Function ShouldSerializeScrollBarThumbColor() As Boolean
        Return 滚动条滑块颜色 <> 默认滚动条滑块颜色
    End Function

    Private Sub ResetScrollBarThumbColor()
        ScrollBarThumbColor = 默认滚动条滑块颜色
    End Sub

    Private Shared ReadOnly 默认滚动条悬停颜色 As Color = Color.FromArgb(120, 120, 120)
    Private 滚动条悬停颜色 As Color = 默认滚动条悬停颜色
    <Category("LakeUI"), Description("滚动条滑块悬停颜色"), Browsable(True)>
    Public Property ScrollBarThumbHoverColor As Color
        Get
            Return 滚动条悬停颜色
        End Get
        Set(value As Color)
            SetValue(滚动条悬停颜色, value)
        End Set
    End Property

    Private Function ShouldSerializeScrollBarThumbHoverColor() As Boolean
        Return 滚动条悬停颜色 <> 默认滚动条悬停颜色
    End Function

    Private Sub ResetScrollBarThumbHoverColor()
        ScrollBarThumbHoverColor = 默认滚动条悬停颜色
    End Sub

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

#Region "外观属性 - SSAA"

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

#Region "外观属性 - 工具提示"

    Private 提示背景颜色 As Color = Color.FromArgb(50, 50, 50)
    <Category("LakeUI"), Description("工具提示背景颜色"), DefaultValue(GetType(Color), "50,50,50"), Browsable(True)>
    Public Property ToolTipBackColor As Color
        Get
            Return 提示背景颜色
        End Get
        Set(value As Color)
            提示背景颜色 = value
            If _truncTooltip IsNot Nothing AndAlso Not _truncTooltip.IsDisposed AndAlso _truncTooltip.Visible Then 更新截断提示(PointToClient(Cursor.Position), True)
        End Set
    End Property

    Private 提示文本颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("工具提示文本颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property ToolTipForeColor As Color
        Get
            Return 提示文本颜色
        End Get
        Set(value As Color)
            提示文本颜色 = value
            If _truncTooltip IsNot Nothing AndAlso Not _truncTooltip.IsDisposed AndAlso _truncTooltip.Visible Then 更新截断提示(PointToClient(Cursor.Position), True)
        End Set
    End Property

    Private 提示边框颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("工具提示边框颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property ToolTipBorderColor As Color
        Get
            Return 提示边框颜色
        End Get
        Set(value As Color)
            提示边框颜色 = value
            If _truncTooltip IsNot Nothing AndAlso Not _truncTooltip.IsDisposed AndAlso _truncTooltip.Visible Then 更新截断提示(PointToClient(Cursor.Position), True)
        End Set
    End Property

    Private 提示边框宽度 As Integer = 1
    <Category("LakeUI"), Description("工具提示边框宽度"), DefaultValue(GetType(Integer), "1"), Browsable(True)>
    Public Property ToolTipBorderSize As Integer
        Get
            Return 提示边框宽度
        End Get
        Set(value As Integer)
            提示边框宽度 = Math.Max(0, value)
            If _truncTooltip IsNot Nothing AndAlso Not _truncTooltip.IsDisposed AndAlso _truncTooltip.Visible Then 更新截断提示(PointToClient(Cursor.Position), True)
        End Set
    End Property

    Private 提示圆角半径 As Integer = 0
    <Category("LakeUI"), Description("工具提示圆角半径"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property ToolTipBorderRadius As Integer
        Get
            Return 提示圆角半径
        End Get
        Set(value As Integer)
            提示圆角半径 = Math.Max(0, value)
            If _truncTooltip IsNot Nothing AndAlso Not _truncTooltip.IsDisposed AndAlso _truncTooltip.Visible Then 更新截断提示(PointToClient(Cursor.Position), True)
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
            If _truncTooltip IsNot Nothing AndAlso Not _truncTooltip.IsDisposed AndAlso _truncTooltip.Visible Then 更新截断提示(PointToClient(Cursor.Position), True)
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
            If _truncTooltip IsNot Nothing AndAlso Not _truncTooltip.IsDisposed AndAlso _truncTooltip.Visible Then 更新截断提示(PointToClient(Cursor.Position), True)
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
                Dim first = _selectedMin
                _selectedIndices.Clear()
                _selectedIndices.Add(first)
                _selectedMin = first
                请求V3渲染()
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
            SetValue(拖选区域宽度, Math.Max(0, value))
        End Set
    End Property

#End Region

#Region "内部状态"

    Private _scrollOffset As Integer = 0
    Private ReadOnly _scrollBar As New V3_ScrollBarRenderer()
    Private _hScrollOffset As Integer = 0
    Private ReadOnly _hScrollBar As New V3_ScrollBarRenderer()
    Private ReadOnly _selectedIndices As New HashSet(Of Integer)
    ''' <summary>选中集合中的最小索引；-1 表示无选中。维护此值避免 SelectedIndex 每次 O(N) 扫描。</summary>
    Private _selectedMin As Integer = -1
    Private _selectionAnchor As Integer = -1
    Private _hoverRowIndex As Integer = -1

    ' --- 帧级缓存：列 X 坐标 ---
    Private _columnXCache As Integer() = Array.Empty(Of Integer)()
    Private _columnXCount As Integer = 0
    Private _columnXDirty As Boolean = True

    Private _moreSymbolFont As Font = Nothing
    Private _moreSymbolFontKey As Single = 0F

    ' --- V3 绘制上下文 ---

    Private _columnResizeIndex As Integer = -1
    Private _columnResizeStartX As Integer = 0
    Private _columnResizeStartWidth As Integer = 0
    Private Const ColumnResizeHitZone As Integer = 4

    Private ReadOnly _hoverAnim As New V3_AnimationHelper(Me) With {.Duration = 150}
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

    Private _truncTooltip As FloatingToolTipForm
    Private _truncTooltipText As String = ""
    Private _truncTooltipActive As Boolean = False
    Private _truncTooltipSourceScreenRect As Rectangle = Rectangle.Empty
    Private Const TruncationSuffix As String = " ..."

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SelectedIndex As Integer
        Get
            Return _selectedMin
        End Get
        Set(value As Integer)
            _selectedIndices.Clear()
            _selectedMin = -1
            _selectionAnchor = -1
            If value >= 0 AndAlso value < _displayRows.Count AndAlso _displayRows(value).Type = DisplayRowType.Item Then
                _selectedIndices.Add(value)
                _selectedMin = value
                _selectionAnchor = value
            End If
            请求V3渲染()
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
            Dim n = _selectedIndices.Count
            Dim result As New List(Of ListItem)(n)
            If n = 0 Then Return result
            ' 用一个数组排序避免 OrderBy 的 IEnumerable 分配
            Dim arr(n - 1) As Integer
            Dim i As Integer = 0
            For Each idx In _selectedIndices
                arr(i) = idx
                i += 1
            Next
            Array.Sort(arr)
            For Each idx In arr
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
        Dim newSet As New HashSet(Of Integer)
        If indices IsNot Nothing Then
            For Each idx In indices
                If idx >= 0 AndAlso idx < _displayRows.Count AndAlso _displayRows(idx).Type = DisplayRowType.Item Then
                    newSet.Add(idx)
                End If
            Next
        End If
        If newSet.Count <> _selectedIndices.Count OrElse Not newSet.SetEquals(_selectedIndices) Then
            _selectedIndices.Clear()
            _selectedMin = -1
            For Each idx In newSet
                If _selectedIndices.Add(idx) Then
                    If _selectedMin = -1 OrElse idx < _selectedMin Then _selectedMin = idx
                End If
            Next
            changed = True
        End If
        If changed Then
            请求V3渲染()
            RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
        End If
    End Sub

#End Region

#Region "高度计算"

    Private Function 计算项高度(item As ListItem) As Integer
        ' 缓存签名：列宽序列 + DPI + Font hash + 自动换行。任何影响测量的因素改动都通过 全部项高度缓存失效() 显式清空。
        If item.CachedHeight >= 0 Then Return item.CachedHeight
        Dim iconAreaW As Integer = 获取图标区域宽度(item)
        Dim scaledPadding As Padding = Dpi(项内边距)
        Dim maxSubH As Integer = 0
        Dim colCount = _columns.Count
        Dim contentW As Integer = If(colCount > 0, 0, 获取内容区域().Width)
        For i As Integer = 0 To item.SubItems.Count - 1
            Dim sub_ = item.SubItems(i)
            Dim colW As Integer = If(colCount > 0 AndAlso i < colCount, _columns(i).Width, contentW)
            Dim availW As Integer = colW - scaledPadding.Horizontal
            If i = 0 Then availW -= iconAreaW
            Dim isFixed As Boolean = (colCount > 0 AndAlso i < colCount AndAlso _columns(i).WordWrapHeightFixed)
            If Not isFixed Then
                Dim subH As Integer = 计算子项内容高度(sub_, availW)
                If subH > maxSubH Then maxSubH = subH
            End If
        Next
        If maxSubH = 0 Then
            Dim lineFlags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
            maxSubH = 测量文本高度("Ag", Me.Font, New Size(Integer.MaxValue, Integer.MaxValue), lineFlags)
        End If
        Dim scaledIconH As Integer = If(iconAreaW > 0, Dpi(图标尺寸).Height, 0)
        If scaledIconH > maxSubH Then maxSubH = scaledIconH
        Dim rowFullW As Integer = If(colCount > 0, 获取总列宽(), 获取内容区域().Width)
        Dim bottomH As Integer = 计算底部文本行高度(item, rowFullW)
        item.CachedUpperPartHeight = maxSubH
        item.CachedBottomLinesHeight = bottomH
        item.CachedHeight = maxSubH + bottomH + scaledPadding.Vertical
        Return item.CachedHeight
    End Function

    Private Function 计算子项内容高度(sub_ As ListSubItem, availWidth As Integer) As Integer
        Return 计算子项内容高度(sub_, availWidth, 获取文本格式标志())
    End Function

    Private Function 计算子项内容高度(sub_ As ListSubItem, availWidth As Integer, flags As TextFormatFlags) As Integer
        If sub_ Is Nothing Then Return 0
        Dim width = Math.Max(1, availWidth)
        Dim fontHash = GetSubItemMeasureFontHash(sub_)
        If sub_.CachedContentHeight >= 0 AndAlso
           sub_.CachedContentVersion = _measureVersion AndAlso
           sub_.CachedContentWidth = width AndAlso
           sub_.CachedContentFlags = flags AndAlso
           sub_.CachedContentFontHash = fontHash Then
            Return sub_.CachedContentHeight
        End If

        Dim totalH As Integer = 测量子项主文本高度缓存(sub_, width, flags)
        For Each line_ In sub_.ExtraLines
            totalH += Dpi(文本行间距)
            totalH += 测量文本行高度缓存(line_, width, flags)
        Next

        sub_.CachedContentHeight = totalH
        sub_.CachedContentVersion = _measureVersion
        sub_.CachedContentWidth = width
        sub_.CachedContentFlags = flags
        sub_.CachedContentFontHash = fontHash
        Return totalH
    End Function

    Private Function 测量子项主文本高度缓存(sub_ As ListSubItem, availWidth As Integer, flags As TextFormatFlags) As Integer
        If sub_ Is Nothing Then Return 0
        Dim width = Math.Max(1, availWidth)
        Dim f As Font = If(sub_.Font, Me.Font)
        Dim fontHash = GetMeasureFontHash(f)
        If sub_.CachedMainHeight >= 0 AndAlso
           sub_.CachedMainVersion = _measureVersion AndAlso
           sub_.CachedMainWidth = width AndAlso
           sub_.CachedMainFlags = flags AndAlso
           sub_.CachedMainFontHash = fontHash Then
            Return sub_.CachedMainHeight
        End If

        Dim h = 测量文本高度(sub_.Text, f, New Size(width, Integer.MaxValue), flags)
        sub_.CachedMainHeight = h
        sub_.CachedMainVersion = _measureVersion
        sub_.CachedMainWidth = width
        sub_.CachedMainFlags = flags
        sub_.CachedMainFontHash = fontHash
        Return h
    End Function

    Private Function GetSubItemMeasureFontHash(sub_ As ListSubItem) As Integer
        If sub_ Is Nothing Then Return 0
        Dim hash As New System.HashCode()
        hash.Add(GetMeasureFontHash(If(sub_.Font, Me.Font)))
        For Each line In sub_.ExtraLines
            hash.Add(GetMeasureFontHash(If(line.Font, Me.Font)))
        Next
        Return hash.ToHashCode()
    End Function

    Private Sub 全部项高度缓存失效()
        InvalidateMeasureCache()
        For Each itm In _items
            itm.InvalidateCache()
        Next
    End Sub

    Friend Sub InvalidateItemFontResources()
        InvalidateV3TextResources()
        全部项高度缓存失效()
        _columnXDirty = True
        If _updateCount <= 0 Then
            重建显示列表()
            请求V3渲染()
        End If
        请求V3渲染()
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
        Return New Rectangle(x, y, Math.Max(0, w), Dpi(列标题高度))
    End Function

    Private Function 获取内容区域() As Rectangle
        Dim inset As Integer = 获取边框内边距()
        Dim x As Integer = inset + Me.Padding.Left
        Dim y As Integer = inset + Me.Padding.Top
        If 列标题可见 AndAlso _columns.Count > 0 Then
            y += Dpi(列标题高度)
        End If
        Dim w As Integer = Me.Width - inset * 2 - Me.Padding.Horizontal
        Dim h As Integer = Me.Height - inset - Me.Padding.Bottom - y
        If 需要横向滚动条() Then
            h -= (Dpi(滚动条宽度) + V3_ScrollBarRenderer.Margin * 2)
        End If
        Return New Rectangle(x, y, Math.Max(0, w), Math.Max(0, h))
    End Function

    Private Function 估算可见行数() As Integer
        If _displayRows.Count = 0 Then Return 1
        Dim contentRect = 获取内容区域()
        Dim availH As Integer = contentRect.Height
        If _scrollOffset > 0 Then availH -= Dpi(更多指示器高度)
        availH -= 获取有效内容上边距() + 获取有效内容下边距()
        Dim baseTop As Integer = If(_scrollOffset >= 0 AndAlso _scrollOffset < _displayRows.Count, _rowTops(_scrollOffset), 0)
        Dim count As Integer = 0
        For i As Integer = _scrollOffset To _displayRows.Count - 1
            Dim usedH As Integer = _rowBottoms(i) - baseTop
            If usedH > availH Then Exit For
            count += 1
        Next
        Return Math.Max(1, count)
    End Function

    Private Function 获取行Y坐标(rowIndex As Integer) As Integer
        If rowIndex < _scrollOffset OrElse rowIndex >= _displayRows.Count Then Return -1
        Dim contentRect = 获取内容区域()
        Dim hasMoreAbove As Boolean = _scrollOffset > 0
        Dim baseY As Integer = contentRect.Y + If(hasMoreAbove, Dpi(更多指示器高度), 0) + 获取有效内容上边距()
        Dim baseTop As Integer = _rowTops(_scrollOffset)
        Dim y As Integer = baseY + (_rowTops(rowIndex) - baseTop)
        If y >= contentRect.Bottom Then Return -1
        Return y
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
        Return Math.Max(1, contentRect.Width - 获取垂直滚动条占位宽度())
    End Function

    Private Function 获取垂直滚动条占位宽度() As Integer
        If _scrollBar.TrackRect.IsEmpty Then Return 0
        Dim inset As Integer = 获取边框内边距()
        Return Math.Max(0, Me.Width - inset - _scrollBar.VisualLeft)
    End Function

    Private Function 获取行绘制宽度(contentRect As Rectangle) As Integer
        Dim visibleW As Integer = Math.Max(0, contentRect.Width - 获取垂直滚动条占位宽度())
        If _columns.Count > 0 Then Return Math.Max(visibleW, 获取总列宽())
        Return visibleW
    End Function

    Private Function 获取项焦点宽度(contentRect As Rectangle) As Integer
        If _columns.Count > 0 Then
            Dim rightEdge As Integer = contentRect.X - _hScrollOffset + 获取总列宽()
            Return Math.Max(0, rightEdge - contentRect.X)
        End If

        Return Math.Max(0, contentRect.Width - 获取垂直滚动条占位宽度())
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
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me, 1) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        If Me.Width < 1 OrElse Me.Height < 1 Then Return

        预计算滚动条布局()
        预计算横向滚动条布局()

        绘制背景与边框_GPU(context)

        Dim inset As Integer = 获取边框内边距()
        Dim clipRect As New RectangleF(inset, inset, Math.Max(0, Me.Width - inset * 2), Math.Max(0, Me.Height - inset * 2))
        If clipRect.Width > 0 AndAlso clipRect.Height > 0 Then
            Using context.PushClip(clipRect)
                If 列标题可见 AndAlso _columns.Count > 0 Then 绘制列标题形状_GPU(context)
                绘制全部行形状_GPU(context)
                绘制拖选框_GPU(context)
                绘制拖动排序指示线_GPU(context)
                If 列标题可见 AndAlso _columns.Count > 0 Then 绘制列标题_GPU(context)
                绘制全部行_GPU(context)
            End Using
        End If

        绘制滚动条_GPU(context)
        绘制横向滚动条_GPU(context)
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Private Sub 请求V3渲染(Optional immediate As Boolean = False)
        请求V3渲染(New Rectangle(Point.Empty, Me.Size), immediate)
    End Sub

    Private Sub 请求V3渲染(dirtyRect As Rectangle, Optional immediate As Boolean = False)
        If IsDisposed Then Return
        V3_InvalidationRouter.RequestRender(Me, dirtyRect)
    End Sub

    Private Sub 填充矩形_GPU(context As D3D_PaintContext, rect As RectangleF, color As Color)
        If color.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        context.FillRectangle(rect, color)
    End Sub

    Private Sub 描边矩形_GPU(context As D3D_PaintContext, rect As RectangleF, color As Color, strokeWidth As Single)
        If color.A = 0 OrElse strokeWidth <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        context.DrawRectangle(rect, color, strokeWidth)
    End Sub

    Private Sub 绘制水平线_GPU(context As D3D_PaintContext, x1 As Single, x2 As Single, y As Single, color As Color, strokeWidth As Single)
        If color.A = 0 OrElse strokeWidth <= 0 Then Return
        Dim br = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If br IsNot Nothing Then context.DeviceContext.DrawLine(New System.Numerics.Vector2(x1, y), New System.Numerics.Vector2(x2, y), br, strokeWidth)
    End Sub

    Private Sub 绘制垂直线_GPU(context As D3D_PaintContext, x As Single, y1 As Single, y2 As Single, color As Color, strokeWidth As Single)
        If color.A = 0 OrElse strokeWidth <= 0 Then Return
        Dim br = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If br IsNot Nothing Then context.DeviceContext.DrawLine(New System.Numerics.Vector2(x, y1), New System.Numerics.Vector2(x, y2), br, strokeWidth)
    End Sub

    Private Sub 填充圆角矩形_GPU(context As D3D_PaintContext, rect As RectangleF, radius As Single, color As Color)
        If color.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim br = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If radius <= 0 Then
            context.DeviceContext.FillRectangle(D3D_PaintContext.ToRawRect(rect), br)
            Return
        End If
        Using geo = D3D_RenderCore.DeviceManager.D2DFactory.CreateRoundedRectangleGeometry(New RoundedRectangle(rect, radius, radius))
            context.DeviceContext.FillGeometry(geo, br)
        End Using
    End Sub

    Private Sub 绘制圆角边框_GPU(context As D3D_PaintContext, rect As RectangleF, radius As Single, color As Color, strokeWidth As Single)
        If color.A = 0 OrElse strokeWidth <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim br = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If radius <= 0 Then
            context.DeviceContext.DrawRectangle(D3D_PaintContext.ToRawRect(rect), br, strokeWidth)
            Return
        End If
        Using geo = D3D_RenderCore.DeviceManager.D2DFactory.CreateRoundedRectangleGeometry(New RoundedRectangle(rect, radius, radius))
            context.DeviceContext.DrawGeometry(geo, br, strokeWidth)
        End Using
    End Sub

    Private Sub 绘制项焦点区域_GPU(context As D3D_PaintContext, rect As RectangleF,
                              fillColor As Color, borderColor As Color, borderWidth As Single)
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim radius As Single = 获取项焦点圆角半径(rect)
        If fillColor.A > 0 Then 填充圆角矩形_GPU(context, rect, radius, fillColor)
        If borderColor.A > 0 AndAlso borderWidth > 0 Then 绘制圆角边框_GPU(context, rect, radius, borderColor, borderWidth)
    End Sub

    Private Sub 绘制背景与边框_GPU(context As D3D_PaintContext)
        Dim s As Single = DpiScale()
        Dim boundsRect As New RectangleF(0, 0, Me.Width, Me.Height)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * s / 2.0F
            boundsRect.Inflate(-half, -half)
        End If
        Dim radius As Single = If(边框圆角半径 > 0, 边框圆角半径 * s, 0.0F)
        填充圆角矩形_GPU(context, boundsRect, radius, 背景颜色)
        If 边框颜色.A > 0 AndAlso 边框宽度 > 0 Then
            绘制圆角边框_GPU(context, boundsRect, radius, 边框颜色, 边框宽度 * s)
        End If
    End Sub

    Private Sub 绘制滚动条_GPU(context As D3D_PaintContext)
        If _scrollBar.TrackRect.IsEmpty Then Return
        Dim width As Single = Math.Max(1.0F, Dpi(滚动条宽度))
        Dim trackArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.TrackRect.Y, width, _scrollBar.TrackRect.Height)
        Dim thumbArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.ThumbRect.Y, width, _scrollBar.ThumbRect.Height)
        填充圆角矩形_GPU(context, trackArea, Math.Min(width / 2.0F, trackArea.Height / 2.0F), 滚动条轨道颜色)
        Dim thumbColor = If(_scrollBar.IsDragging OrElse _scrollBar.IsHover, 滚动条悬停颜色, 滚动条滑块颜色)
        填充圆角矩形_GPU(context, thumbArea, Math.Min(width / 2.0F, thumbArea.Height / 2.0F), thumbColor)
    End Sub

    Private Sub 绘制横向滚动条_GPU(context As D3D_PaintContext)
        If _hScrollBar.TrackRect.IsEmpty Then Return
        Dim height As Single = Math.Max(1.0F, Dpi(滚动条宽度))
        Dim trackArea As New RectangleF(_hScrollBar.TrackRect.X, _hScrollBar.VisualTop, _hScrollBar.TrackRect.Width, height)
        Dim thumbArea As New RectangleF(_hScrollBar.ThumbRect.X, _hScrollBar.VisualTop, _hScrollBar.ThumbRect.Width, height)
        填充圆角矩形_GPU(context, trackArea, Math.Min(height / 2.0F, trackArea.Width / 2.0F), 滚动条轨道颜色)
        Dim thumbColor = If(_hScrollBar.IsDragging OrElse _hScrollBar.IsHover, 滚动条悬停颜色, 滚动条滑块颜色)
        填充圆角矩形_GPU(context, thumbArea, Math.Min(height / 2.0F, thumbArea.Width / 2.0F), thumbColor)
    End Sub

    Private Sub 绘制列标题形状_GPU(context As D3D_PaintContext)
        Dim headerRect As Rectangle = 获取列标题区域()
        填充矩形_GPU(context, headerRect, 列标题背景颜色)
        确保列X缓存()
        Dim dpiS As Single = DpiScale()
        Dim sw As Single = Math.Max(1.0F, 列标题分隔线宽度 * dpiS)
        For i As Integer = 0 To _columns.Count - 1
            Dim col = _columns(i)
            Dim x As Integer = _columnXCache(i)
            绘制垂直线_GPU(context, x + col.Width - 1, headerRect.Y + 4, headerRect.Bottom - 4, 列标题分隔线颜色, sw)
        Next
        绘制水平线_GPU(context, headerRect.X, headerRect.Right, headerRect.Bottom - 1, 列标题分隔线颜色, sw)
    End Sub

    Private Sub 绘制分组标题行形状_GPU(context As D3D_PaintContext, grp As ListGroup, rect As Rectangle)
        填充矩形_GPU(context, rect, 分组背景颜色)

        Dim arrowSize As Integer = Dpi(12)
        Dim arrowMargin As Integer = Dpi(10)
        Dim arrowX As Integer = rect.X + arrowMargin
        Dim arrowY As Integer = rect.Y + (rect.Height - arrowSize) \ 2
        Dim effectiveColor As Color = If(grp.ForeColor <> Color.Empty, grp.ForeColor, 分组文字颜色)
        Using path As GraphicsPath = 创建圆角箭头路径(arrowX, arrowY, arrowSize, grp.IsCollapsed)
            Using geo = 路径转D2D几何(path)
                If geo IsNot Nothing Then
                    Dim br = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, effectiveColor, context.DeviceGeneration)
                    If br IsNot Nothing Then context.DeviceContext.FillGeometry(geo, br)
                End If
            End Using
        End Using

        绘制水平线_GPU(context, rect.X, rect.Right, rect.Bottom - 1, 分组分隔线颜色, 1.0F)
    End Sub

    Private Sub 绘制拖选框_GPU(context As D3D_PaintContext)
        If Not _isDragSelecting Then Return
        Dim rect As Rectangle = 获取拖选矩形()
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        填充矩形_GPU(context, rect, 选框填充颜色)
        描边矩形_GPU(context, rect, 选框边框颜色, 1.0F)
    End Sub

    Private Sub 绘制拖动排序指示线_GPU(context As D3D_PaintContext)
        If Not _isDragReordering OrElse _dragReorderInsertIndex < 0 Then Return
        Dim contentRect = 获取内容区域()
        Dim availW = 获取行绘制宽度(contentRect)
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
        绘制水平线_GPU(context, contentRect.X, contentRect.X + availW, lineY, 拖动排序指示线颜色, 拖动排序指示线宽 * DpiScale())
    End Sub

    Private Sub 绘制全部行形状_GPU(context As D3D_PaintContext)
        If _displayRows.Count = 0 Then Return
        Dim contentRect = 获取内容区域()
        If contentRect.Height <= 0 OrElse contentRect.Width <= 0 Then Return

        Dim hasMoreAbove As Boolean = _scrollOffset > 0
        Dim topIndicatorH As Integer = If(hasMoreAbove, Dpi(更多指示器高度), 0)
        Dim currentY As Integer = contentRect.Y + topIndicatorH + 获取有效内容上边距()
        Dim bottomLimit As Integer = contentRect.Bottom - 获取有效内容下边距()

        Dim availW As Integer = 获取行绘制宽度(contentRect)
        Dim itemFocusW As Integer = 获取项焦点宽度(contentRect)
        Dim dpiS As Single = DpiScale()

        If _hoverAnimActive AndAlso _hoverRowIndex >= 0 AndAlso
           _hoverRowIndex < _displayRows.Count AndAlso
           _displayRows(_hoverRowIndex).Type = DisplayRowType.Item AndAlso
           Not _selectedIndices.Contains(_hoverRowIndex) Then
            Dim t As Single = _hoverAnim.Progress
            Dim animY As Single = _hoverAnimFromY + (_hoverAnimToY - _hoverAnimFromY) * t
            Dim animH As Single = _hoverAnimFromH + (_hoverAnimToH - _hoverAnimFromH) * t
            绘制项焦点区域_GPU(context, New RectangleF(contentRect.X, animY, itemFocusW, animH),
                              项悬停背景颜色, Color.Empty, 0)
        End If

        For i As Integer = _scrollOffset To _displayRows.Count - 1
            Dim row = _displayRows(i)
            Dim rowH As Integer = row.Height
            Dim spacing As Integer = If(i = _scrollOffset, 0, row.Spacing)
            If currentY + spacing + rowH > bottomLimit Then Exit For
            currentY += spacing
            Dim rowRect As New Rectangle(contentRect.X, currentY, availW, rowH)
            Dim itemFocusRect As New Rectangle(contentRect.X, currentY, itemFocusW, rowH)

            If row.Type = DisplayRowType.GroupHeader Then
                绘制分组标题行形状_GPU(context, row.Group, rowRect)
            Else
                If _selectedIndices.Contains(i) Then
                    绘制项焦点区域_GPU(context, itemFocusRect, 项选中背景颜色, Color.Empty, 0)
                ElseIf i = _hoverRowIndex AndAlso Not _hoverAnimActive Then
                    绘制项焦点区域_GPU(context, itemFocusRect, 项悬停背景颜色, Color.Empty, 0)
                End If
                If row.Item.Checked Then
                    绘制项焦点区域_GPU(context, itemFocusRect, Color.Empty, 项高亮边框颜色, Math.Max(1.0F, 项高亮边框宽度 * dpiS))
                End If
            End If
            currentY += rowH
        Next
    End Sub

    Private Sub 绘制列标题_GPU(context As D3D_PaintContext)
        Dim headerRect As Rectangle = 获取列标题区域()
        确保列X缓存()
        For i As Integer = 0 To _columns.Count - 1
            Dim col = _columns(i)
            Dim x As Integer = _columnXCache(i)
            Dim pad As Padding = Dpi(col.HeaderPadding)
            Dim textRect As New RectangleF(x + pad.Left, headerRect.Y + pad.Top, col.Width - pad.Horizontal, headerRect.Height - pad.Vertical)
            Dim drawText As String = 截断文本到宽度(col.Text, Me.Font, CInt(textRect.Width))
            context.DrawText(drawText, Me.Font, 列标题文字颜色, textRect,
                             Vortice.DirectWrite.TextAlignment.Leading,
                             Vortice.DirectWrite.ParagraphAlignment.Center)
        Next
    End Sub

    Private Sub 绘制全部行_GPU(context As D3D_PaintContext)
        If _displayRows.Count = 0 Then Return
        Dim contentRect = 获取内容区域()
        If contentRect.Height <= 0 OrElse contentRect.Width <= 0 Then Return

        Dim hasMoreAbove As Boolean = _scrollOffset > 0
        Dim topIndicatorH As Integer = If(hasMoreAbove, Dpi(更多指示器高度), 0)
        Dim currentY As Integer = contentRect.Y + topIndicatorH + 获取有效内容上边距()
        Dim bottomLimit As Integer = contentRect.Bottom - 获取有效内容下边距()
        Dim lastDrawnIndex As Integer = _scrollOffset - 1

        Dim availW As Integer = 获取行绘制宽度(contentRect)
        确保列X缓存()

        For i As Integer = _scrollOffset To _displayRows.Count - 1
            Dim row = _displayRows(i)
            Dim rowH As Integer = row.Height
            Dim spacing As Integer = If(i = _scrollOffset, 0, row.Spacing)
            If currentY + spacing + rowH > bottomLimit Then Exit For
            currentY += spacing
            Dim rowRect As New Rectangle(contentRect.X, currentY, availW, rowH)

            If row.Type = DisplayRowType.GroupHeader Then
                绘制分组标题行_GPU(context, row.Group, rowRect)
            Else
                绘制项行_GPU(context, row.Item, rowRect, i)
            End If

            currentY += rowH
            lastDrawnIndex = i
        Next

        Dim hasMoreBelow As Boolean = lastDrawnIndex < _displayRows.Count - 1
        If hasMoreBelow AndAlso currentY < bottomLimit Then
            绘制更多指示器符号_GPU(context, New Rectangle(contentRect.X, currentY, availW, bottomLimit - currentY), False)
        End If
        If hasMoreAbove Then
            绘制更多指示器符号_GPU(context, New Rectangle(contentRect.X, contentRect.Y, availW, topIndicatorH), True)
        End If
    End Sub

    Private Sub 绘制分组标题行_GPU(context As D3D_PaintContext, grp As ListGroup, rect As Rectangle)
        Dim effectiveColor As Color = If(grp.ForeColor <> Color.Empty, grp.ForeColor, 分组文字颜色)
        Dim arrowSize As Integer = Dpi(12)
        Dim arrowMargin As Integer = Dpi(10)
        Dim arrowX As Integer = rect.X + arrowMargin
        Dim textX As Integer = arrowX + arrowSize + Dpi(6)
        Dim textRect As New RectangleF(textX, rect.Y, rect.Right - textX - Dpi(4), rect.Height)
        Dim drawText As String = 截断文本到宽度(grp.Text, Me.Font, CInt(textRect.Width))
        context.DrawText(drawText, Me.Font, effectiveColor, textRect,
                         Vortice.DirectWrite.TextAlignment.Leading,
                         Vortice.DirectWrite.ParagraphAlignment.Center)
    End Sub

    Private Sub 绘制项行_GPU(context As D3D_PaintContext, item As ListItem, rowRect As Rectangle,
                              Optional displayRowIndex As Integer = -1)
        Dim skipEditingCell As Boolean = (displayRowIndex >= 0 AndAlso displayRowIndex = _editRowIndex AndAlso _editColumnIndex >= 0)
        Dim iconAreaW As Integer = 获取图标区域宽度(item)
        Dim hasIcon As Boolean = iconAreaW > 0
        Dim scaledPadding As Padding = Dpi(项内边距)
        Dim scaledIconSize As Size = Dpi(图标尺寸)
        Dim bottomLinesH As Integer = item.CachedBottomLinesHeight
        Dim upperPartH As Integer = item.CachedUpperPartHeight
        If bottomLinesH = 0 AndAlso upperPartH = 0 Then
            Dim rowFullW As Integer = If(_columns.Count > 0, 获取总列宽(), rowRect.Width)
            bottomLinesH = 计算底部文本行高度(item, rowFullW)
            upperPartH = rowRect.Height - scaledPadding.Vertical - bottomLinesH
        End If
        Dim flags As TextFormatFlags = 获取文本格式标志()

        Dim colCount As Integer = If(_columns.Count > 0, _columns.Count, 1)
        For colIdx As Integer = 0 To colCount - 1
            If colIdx >= item.SubItems.Count Then Exit For
            Dim sub_ = item.SubItems(colIdx)
            Dim colX As Integer = _columnXCache(Math.Min(colIdx, _columnXCount - 1))
            Dim colW As Integer = If(_columns.Count > 0, _columns(colIdx).Width, rowRect.Width)

            Dim colFixed As Boolean = (_columns.Count > 0 AndAlso colIdx < _columns.Count AndAlso _columns(colIdx).WordWrapHeightFixed)
            Dim cellFlags As TextFormatFlags = If(colFixed AndAlso 自动换行,
                (flags And Not TextFormatFlags.WordBreak) Or TextFormatFlags.SingleLine,
                flags)

            Dim cellRect As New Rectangle(
                colX + scaledPadding.Left, rowRect.Y + scaledPadding.Top,
                colW - scaledPadding.Horizontal, upperPartH)

            If colIdx = 0 AndAlso hasIcon AndAlso item.Icon IsNot Nothing Then
                Dim iconX As Integer = cellRect.X
                Dim iconY As Integer = rowRect.Y + scaledPadding.Top + (upperPartH - scaledIconSize.Height) \ 2
                context.DrawImage(item.Icon, New RectangleF(iconX, iconY, scaledIconSize.Width, scaledIconSize.Height))
                cellRect = New Rectangle(cellRect.X + iconAreaW, cellRect.Y, cellRect.Width - iconAreaW, cellRect.Height)
            End If

            If cellRect.Width <= 0 OrElse cellRect.Height <= 0 Then Continue For
            If skipEditingCell AndAlso colIdx = _editColumnIndex Then Continue For

            Dim contentH As Integer = 计算子项内容高度(sub_, cellRect.Width, cellFlags)
            If colFixed Then contentH = Math.Min(contentH, cellRect.Height)
            Dim startY As Integer = cellRect.Y + Math.Max(0, (cellRect.Height - contentH) \ 2)
            Dim lineY As Integer = startY

            Dim mf As Font = If(sub_.Font, Me.Font)
            Dim mc As Color = If(sub_.ForeColor <> Color.Empty, sub_.ForeColor, 项文本颜色)
            Dim mh As Integer = 测量子项主文本高度缓存(sub_, cellRect.Width, cellFlags)
            If Not String.IsNullOrEmpty(sub_.Text) Then
                Dim remainH As Integer = cellRect.Bottom - lineY
                If remainH > 0 Then
                    绘制可能截断文本_GPU(context, sub_.Text, mf, New Rectangle(cellRect.X, lineY, cellRect.Width, Math.Min(mh, remainH)), mc, cellFlags)
                End If
            End If
            lineY += mh

            For Each line_ In sub_.ExtraLines
                lineY += Dpi(文本行间距)
                Dim lf As Font = If(line_.Font, Me.Font)
                Dim lc As Color = If(line_.ForeColor <> Color.Empty, line_.ForeColor, 项文本颜色)
                Dim lh As Integer = 测量文本行高度缓存(line_, cellRect.Width, cellFlags)
                If Not String.IsNullOrEmpty(line_.Text) Then
                    Dim remainH As Integer = cellRect.Bottom - lineY
                    If remainH > 0 Then
                        绘制可能截断文本_GPU(context, line_.Text, lf, New Rectangle(cellRect.X, lineY, cellRect.Width, Math.Min(lh, remainH)), lc, cellFlags)
                    End If
                End If
                lineY += lh
            Next
        Next

        If item.BottomLines.Count > 0 Then
            Dim blX As Integer = rowRect.X + scaledPadding.Left
            Dim blW As Integer = rowRect.Width - scaledPadding.Horizontal
            Dim blY As Integer = rowRect.Y + scaledPadding.Top + upperPartH + Dpi(底部文本行间距)
            For Each bl In item.BottomLines
                blY += Dpi(文本行间距)
                Dim lf As Font = If(bl.Font, Me.Font)
                Dim lc As Color = If(bl.ForeColor <> Color.Empty, bl.ForeColor, 项文本颜色)
                Dim lh As Integer = 测量文本行高度缓存(bl, blW, flags)
                If Not String.IsNullOrEmpty(bl.Text) Then
                    绘制可能截断文本_GPU(context, bl.Text, lf, New Rectangle(blX, blY, blW, lh), lc, flags)
                End If
                blY += lh
            Next
        End If
    End Sub

    Private Sub 绘制可能截断文本_GPU(context As D3D_PaintContext, text As String, font As Font,
                                   rect As Rectangle, color As Color, flags As TextFormatFlags)
        If String.IsNullOrEmpty(text) OrElse rect.Width <= 0 OrElse rect.Height <= 0 OrElse color.A = 0 Then Return
        Dim canWrap As Boolean = (flags And TextFormatFlags.WordBreak) = TextFormatFlags.WordBreak AndAlso
                                 (flags And TextFormatFlags.SingleLine) <> TextFormatFlags.SingleLine
        Dim drawText As String = If(canWrap, text, 截断文本到宽度(text, font, rect.Width))
        context.DrawText(drawText, font, color, rect,
                         Vortice.DirectWrite.TextAlignment.Leading,
                         Vortice.DirectWrite.ParagraphAlignment.Near)
    End Sub

    Private Sub 绘制更多指示器符号_GPU(context As D3D_PaintContext, rect As Rectangle, isTop As Boolean)
        If rect.Height < 2 Then Return
        Dim symbol As String = If(isTop, "▲", "▼")
        Dim symbolSize As Single = Math.Max(7, Me.Font.Size - 1)
        If _moreSymbolFont Is Nothing OrElse _moreSymbolFontKey <> symbolSize Then
            _moreSymbolFont?.Dispose()
            _moreSymbolFont = New Font(Me.Font.FontFamily, symbolSize, FontStyle.Regular)
            _moreSymbolFontKey = symbolSize
        End If
        context.DrawText(symbol, _moreSymbolFont, 更多指示器颜色, rect,
                         Vortice.DirectWrite.TextAlignment.Center,
                         Vortice.DirectWrite.ParagraphAlignment.Center)
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
            Dim hsbSpace As Integer = If(需要横向滚动条(), Dpi(滚动条宽度) + V3_ScrollBarRenderer.Margin * 2, 0)
            Dim extraBottom As Integer = Me.Padding.Bottom + hsbSpace
            Dim s As Single = DpiScale()
            _scrollBar.ComputeLayout(Me.Width, Me.Height, CInt(边框宽度 * s), CInt(边框圆角半径 * s),
                extraTop, extraBottom, Dpi(滚动条宽度),
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
        Dim vsbReserved As Integer = 获取垂直滚动条占位宽度()
        Dim visibleW As Integer = Math.Max(1, contentRect.Width - vsbReserved)
        Dim paddingLeft As Integer = contentRect.X - inset
        Dim paddingRight As Integer = Me.Padding.Right + vsbReserved
        Dim s As Single = DpiScale()
        _hScrollBar.ComputeHorizontalLayout(Me.Width, Me.Height, CInt(边框宽度 * s), CInt(边框圆角半径 * s),
            paddingLeft, paddingRight, Dpi(滚动条宽度),
            totalW, visibleW, _hScrollOffset)
    End Sub

    ''' <summary>
    ''' 创建一个使用三次贝塞尔曲线代替每个角的圆角三角形箭头路径。
    ''' isCollapsed=True 时为指向右的箭头(▶)，否则为指向下的箭头(▼)。
    ''' </summary>
    Private Function 创建圆角箭头路径(x As Integer, y As Integer, size As Integer, isCollapsed As Boolean) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim p1 As PointF, p2 As PointF, p3 As PointF
        If isCollapsed Then
            p1 = New PointF(x, y)
            p2 = New PointF(x + size, y + size / 2.0F)
            p3 = New PointF(x, y + size)
        Else
            p1 = New PointF(x, y)
            p2 = New PointF(x + size, y)
            p3 = New PointF(x + size / 2.0F, y + size)
        End If
        ' 角点向边内"收"的距离与控制柄长度。t 越大越圆。
        Dim t As Single = size * 0.18F
        AddRoundedCorner(path, p3, p1, p2, t, isFirst:=True)
        AddRoundedCorner(path, p1, p2, p3, t)
        AddRoundedCorner(path, p2, p3, p1, t)
        path.CloseFigure()
        Return path
    End Function

    ''' <summary>把顶点 v 替换为：从前邻边收 t 距离的进入点 → 贝塞尔曲线（控制点= v）→ 出边收 t 距离的离开点。</summary>
    Private Shared Sub AddRoundedCorner(path As GraphicsPath, prev As PointF, v As PointF, [next] As PointF, t As Single, Optional isFirst As Boolean = False)
        Dim inPt As PointF = LerpToward(v, prev, t)
        Dim outPt As PointF = LerpToward(v, [next], t)
        If isFirst Then
            path.StartFigure()
            path.AddLine(inPt, inPt) ' 占位起点（GraphicsPath 没有显式 MoveTo，需通过 AddLine 触发）
        Else
            path.AddLine(path.GetLastPoint(), inPt)
        End If
        ' 用三次贝塞尔逼近圆角：两控制点都落在 v 上，曲线自然在 inPt-outPt 之间通过 v 弯过去
        path.AddBezier(inPt, v, v, outPt)
    End Sub

    ''' <summary>从 a 朝 b 方向移动距离 d；若距离不足则返回 b。</summary>
    Private Shared Function LerpToward(a As PointF, b As PointF, d As Single) As PointF
        Dim dx As Single = b.X - a.X
        Dim dy As Single = b.Y - a.Y
        Dim len As Single = CSng(Math.Sqrt(dx * dx + dy * dy))
        If len <= d OrElse len < 0.0001F Then Return b
        Dim k As Single = d / len
        Return New PointF(a.X + dx * k, a.Y + dy * k)
    End Function

    ''' <summary>D2D 文字层：列标题文本 + 列分隔线（分隔线之前已在形状层画好；这里仅文本）。</summary>

    ''' <summary>D2D 文字层：行文字 + 行图标 + 分组箭头/文字 + 更多指示器符号。</summary>


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
            请求V3渲染()
            Return
        End If

        If _hScrollBar.IsDragging Then
            隐藏截断提示()
            Dim totalW = 获取总列宽()
            Dim visibleW = 获取可见列宽()
            _hScrollOffset = _hScrollBar.DragMoveHorizontal(e.X, totalW, visibleW)
            请求V3渲染()
            Return
        End If

        If _columnResizeIndex >= 0 Then
            隐藏截断提示()
            Dim delta As Integer = e.X - _columnResizeStartX
            _columns(_columnResizeIndex).Width = Math.Max(30, _columnResizeStartWidth + delta)
            校正横向滚动偏移()
            Return
        End If

        ' 拖选/拖排序检测
        If e.Button = MouseButtons.Left AndAlso _mouseDownInContent Then
            If _isDragReordering Then
                隐藏截断提示()
                _dragReorderInsertIndex = 计算拖动排序插入位置(e.Y)
                请求V3渲染()
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
                            Dim allSameGroup As Boolean = True
                            For Each idx In _selectedIndices
                                If idx < 0 OrElse idx >= _displayRows.Count OrElse
                                   _displayRows(idx).Type <> DisplayRowType.Item OrElse
                                   Not String.Equals(_displayRows(idx).Item.GroupName, srcGroup, StringComparison.Ordinal) Then
                                    allSameGroup = False
                                    Exit For
                                End If
                            Next
                            If allSameGroup Then
                                _dragReorderSourceIndices = New List(Of Integer)(_selectedIndices)
                                _dragReorderSourceIndices.Sort()
                            Else
                                _dragReorderSourceIndices = New List(Of Integer) From {_dragReorderSourceIndex}
                            End If
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
                隐藏截断提示()
                _dragCurrent = e.Location
                更新拖选(e)
                请求V3渲染()
                Return
            End If
        End If

        Dim needInvalidate As Boolean = False
        If _scrollBar.UpdateHover(e.Location) Then needInvalidate = True
        If _hScrollBar.UpdateHover(e.Location) Then needInvalidate = True
        If needInvalidate Then 请求V3渲染()

        If 列标题可见 AndAlso _columns.Count > 0 AndAlso 允许调整列宽 Then
            Dim headerRect = 获取列标题区域()
            If headerRect.Contains(e.Location) Then
                Dim resizeCol = 检测列分隔线(e.X)
                Me.Cursor = If(resizeCol >= 0, Cursors.VSplit, Cursors.Default)
                更新悬停(-1)
                If resizeCol >= 0 Then
                    隐藏截断提示()
                Else
                    更新截断提示(e.Location)
                End If
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
        If e.Button <> MouseButtons.Left Then Return

        If _scrollBar.BeginDrag(e.Location, _scrollOffset) Then Return
        If Not _scrollBar.TrackRect.IsEmpty Then
            Dim visCount = 估算可见行数()
            Dim newOff = _scrollBar.TrackClick(e.Location, _scrollOffset, _displayRows.Count, visCount)
            If newOff <> _scrollOffset Then
                _scrollOffset = newOff
                请求V3渲染()
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
                请求V3渲染()
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
                请求V3渲染()
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
            请求V3渲染()
            Return
        End If

        If _isDragSelecting Then
            _isDragSelecting = False
            _mouseDownInContent = False
            _scrollBar.EndDrag()
            _hScrollBar.EndDrag()
            _columnResizeIndex = -1
            请求V3渲染()
            Return
        End If

        If e.Button = MouseButtons.Left AndAlso _mouseDownInContent Then
            _mouseDownInContent = False
            Dim hitRow As Integer = 命中测试行(e.Location)
            处理点击选择(hitRow, e)
        Else
            _mouseDownInContent = False
        End If

        _scrollBar.EndDrag()
        _hScrollBar.EndDrag()
        _columnResizeIndex = -1
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        更新悬停(-1)
        延迟隐藏截断提示(True)
        Dim needInvalidate2 As Boolean = False
        If _scrollBar.ResetHover() Then needInvalidate2 = True
        If _hScrollBar.ResetHover() Then needInvalidate2 = True
        If needInvalidate2 Then 请求V3渲染()
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
                Dim newHOff = V3_ScrollBarRenderer.HandleHorizontalWheel(e.Delta, _hScrollOffset, totalW, visibleW)
                If newHOff <> _hScrollOffset Then
                    _hScrollOffset = newHOff
                    请求V3渲染()
                End If
            End If
            Return
        End If
        If _displayRows.Count = 0 Then Return
        Dim visCount = 估算可见行数()
        Dim newOff = V3_ScrollBarRenderer.HandleWheel(e.Delta, _scrollOffset, _displayRows.Count, visCount, 3)
        If newOff <> _scrollOffset Then
            _scrollOffset = newOff
            Dim hitRow = 命中测试行(e.Location)
            更新悬停(hitRow)
            请求V3渲染()
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
                设置选中集合(Array.Empty(Of Integer)())
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

        RaiseEvent ItemClick(Me, New ListItemEventArgs(row.Item, hitRow, clickedCol))
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
                RaiseEvent ItemDoubleClick(Me, New ListItemEventArgs(row.Item, hitRow, 命中测试列(e.Location)))
            ElseIf row.Type = DisplayRowType.GroupHeader Then
                row.Group.IsCollapsed = Not row.Group.IsCollapsed
                重建显示列表()
                请求V3渲染()
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
                        开始标签编辑(_selectedMin, firstEditableCol)
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
                    请求V3渲染()
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

#End Region

#Region "拖动排序"

    Private Function 是否在拖选区域(mouseX As Integer) As Boolean
        If Not 允许多选 Then Return False
        If Not 允许拖动排序 Then Return True
        Dim contentRect = 获取内容区域()
        Dim zoneLeft = contentRect.Right - 获取垂直滚动条占位宽度() - Dpi(拖选区域宽度)
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
        _dragReorderSourceIndices.Sort()
        For Each dispIdx In _dragReorderSourceIndices
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
            Dim itemIndex As Integer = 查找项索引(item)
            If itemIndex < 0 Then Return
            sourceItemsIndices.Add(itemIndex)
        Next
        sourceItemsIndices.Sort()

        Dim countBefore As Integer = 0
        For Each sourceIndex In sourceItemsIndices
            If sourceIndex < targetItemsIndex Then countBefore += 1
        Next
        Dim adjustedTarget = targetItemsIndex - countBefore

        BeginUpdate()
        Try
            For i = sourceItemsIndices.Count - 1 To 0 Step -1
                _items.RemoveAt(sourceItemsIndices(i))
            Next
            adjustedTarget = Math.Max(0, Math.Min(adjustedTarget, _items.Count))
            For i = 0 To movedItems.Count - 1
                _items.Insert(adjustedTarget + i, movedItems(i))
            Next
        Finally
            EndUpdate()
        End Try

        _selectedIndices.Clear()
        _selectedMin = -1
        Dim movedSet As New HashSet(Of ListItem)(movedItems)
        For i = 0 To _displayRows.Count - 1
            If _displayRows(i).Type = DisplayRowType.Item AndAlso movedSet.Contains(_displayRows(i).Item) Then
                If _selectedIndices.Add(i) Then
                    If _selectedMin = -1 OrElse i < _selectedMin Then _selectedMin = i
                End If
            End If
        Next
        _selectionAnchor = _selectedMin
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

    Private NotInheritable Class LabelEditTextBox
        Inherits ModernTextBox

        Private _frozenBackground As Image

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property FrozenBackground As Image
            Get
                Return _frozenBackground
            End Get
            Set(value As Image)
                If Object.ReferenceEquals(_frozenBackground, value) Then Return
                If _frozenBackground IsNot Nothing Then
                    Try : _frozenBackground.Dispose() : Catch : End Try
                End If
                _frozenBackground = value
                Invalidate()
            End Set
        End Property

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            If _frozenBackground IsNot Nothing Then
                If BackColor.A > 0 Then
                    Using b As New SolidBrush(BackColor)
                        e.Graphics.FillRectangle(b, ClientRectangle)
                    End Using
                End If
                e.Graphics.DrawImage(_frozenBackground, New Rectangle(0, 0, Width, Height))
                Return
            End If
            MyBase.OnPaintBackground(e)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing AndAlso _frozenBackground IsNot Nothing Then
                Try : _frozenBackground.Dispose() : Catch : End Try
                _frozenBackground = Nothing
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class

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

        确保列X缓存()
        Dim colX = _columnXCache(Math.Min(columnIndex, _columnXCount - 1))
        Dim colW = _columns(columnIndex).Width
        Dim iconAreaW = If(columnIndex = 0, 获取图标区域宽度(item), 0)
        Dim scaledPadding As Padding = Dpi(项内边距)
        Dim cellX = colX + scaledPadding.Left + iconAreaW
        Dim cellW = colW - scaledPadding.Horizontal - iconAreaW
        If cellW <= 20 Then Return

        _editRowIndex = displayRowIndex
        _editColumnIndex = columnIndex
        Dim sub_ = item.SubItems(columnIndex)
        Dim editDirtyRect As New Rectangle(cellX, rowY, cellW, row.Height)
        Dim editBackground As Image = 创建编辑框背景快照(editDirtyRect)
        Dim fallbackBackColor As Color = 获取编辑框回退背景颜色(displayRowIndex)

        _editTextBox = New LabelEditTextBox With {
            .Text = sub_.Text,
            .Font = If(sub_.Font, Me.Font),
            .ForeColor = If(sub_.ForeColor <> Color.Empty, sub_.ForeColor, 项文本颜色),
            .BackColor1 = Color.Transparent,
            .BackColor = fallbackBackColor,
            .FrozenBackground = editBackground,
            .BorderSize = 1,
            .BorderColor = 项高亮边框颜色,
            .BorderColorFocus = 项高亮边框颜色,
            .Padding = New Padding(2, 0, 2, 0),
            .Location = New Point(cellX, rowY),
            .Size = New Size(cellW, row.Height)
        }

        ' 让 list view 的背景/文本缓存失效，避免编辑框采样时拍到旧文字。
        请求V3渲染(editDirtyRect)

        Me.Controls.Add(_editTextBox)
        _editTextBox.BringToFront()
        _editTextBox.Focus()
        _editTextBox.[Select](0, sub_.Text.Length)

        AddHandler _editTextBox.PreviewKeyDown, AddressOf 编辑框预览按键
        AddHandler _editTextBox.LostFocus, AddressOf 编辑框失焦
        AddHandler _editTextBox.KeyDown, AddressOf 编辑框按键
    End Sub

    Private Function 创建编辑框背景快照(cellRect As Rectangle) As Image
        If cellRect.Width <= 0 OrElse cellRect.Height <= 0 Then Return Nothing
        If Me.Width <= 0 OrElse Me.Height <= 0 Then Return Nothing

        Dim fullBmp As Bitmap = Nothing
        Try
            fullBmp = New Bitmap(Me.Width, Me.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb)
            Using g = Graphics.FromImage(fullBmp)
                g.Clear(Color.Transparent)
                Using pea As New PaintEventArgs(g, Me.ClientRectangle)
                    InvokePaintBackground(Me, pea)
                    InvokePaint(Me, pea)
                End Using
            End Using

            Dim clipped = Rectangle.Intersect(Me.ClientRectangle, cellRect)
            If clipped.Width <= 0 OrElse clipped.Height <= 0 Then Return Nothing

            Dim crop As New Bitmap(cellRect.Width, cellRect.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb)
            Using g = Graphics.FromImage(crop)
                g.Clear(Color.Transparent)
                g.DrawImage(fullBmp,
                            New Rectangle(clipped.X - cellRect.X, clipped.Y - cellRect.Y, clipped.Width, clipped.Height),
                            clipped,
                            GraphicsUnit.Pixel)
            End Using
            Return crop
        Catch
            Return Nothing
        Finally
            If fullBmp IsNot Nothing Then
                Try : fullBmp.Dispose() : Catch : End Try
            End If
        End Try
    End Function

    Private Function 获取编辑框回退背景颜色(displayRowIndex As Integer) As Color
        Dim baseColor As Color = 合成到不透明底(背景颜色, 获取背景回退底色())

        If _selectedIndices.Contains(displayRowIndex) Then
            baseColor = AlphaBlend(项选中背景颜色, baseColor)
        ElseIf displayRowIndex = _hoverRowIndex Then
            baseColor = AlphaBlend(项悬停背景颜色, baseColor)
        End If

        Return baseColor
    End Function

    Private Function 获取背景回退底色() As Color
        Dim fallback As Color = SystemColors.Control
        Try
            If Parent IsNot Nothing Then fallback = Parent.BackColor
        Catch
        End Try
        If fallback.A < 255 Then fallback = Color.FromArgb(255, fallback.R, fallback.G, fallback.B)
        Return fallback
    End Function

    Private Shared Function 合成到不透明底(over As Color, under As Color) As Color
        If over.A >= 255 Then Return Color.FromArgb(255, over.R, over.G, over.B)
        Return AlphaBlend(over, under)
    End Function

    Private Shared Function AlphaBlend(over As Color, under As Color) As Color
        If over.A <= 0 Then Return Color.FromArgb(255, under.R, under.G, under.B)
        If over.A >= 255 Then Return Color.FromArgb(255, over.R, over.G, over.B)

        Dim a As Integer = over.A
        Dim inv As Integer = 255 - a
        Dim r As Integer = (over.R * a + under.R * inv + 127) \ 255
        Dim g As Integer = (over.G * a + under.G * inv + 127) \ 255
        Dim b As Integer = (over.B * a + under.B * inv + 127) \ 255
        Return Color.FromArgb(255, r, g, b)
    End Function

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
                End If
            End If
        End If

        _editRowIndex = -1
        _editColumnIndex = -1
        Me.Controls.Remove(editBox)
        editBox.Dispose()
        请求V3渲染()
        If shouldRefocus Then Me.Focus()
    End Sub

#End Region

#Region "命中测试"

    Private Function 命中测试行(pt As Point) As Integer
        Dim contentRect = 获取内容区域()
        If Not contentRect.Contains(pt) Then Return -1
        If _displayRows.Count = 0 OrElse _scrollOffset >= _displayRows.Count Then Return -1
        Dim hasMoreAbove As Boolean = _scrollOffset > 0
        Dim baseY As Integer = contentRect.Y + If(hasMoreAbove, Dpi(更多指示器高度), 0) + 获取有效内容上边距()
        Dim bottomLimit As Integer = contentRect.Bottom - 获取有效内容下边距()
        Dim baseTop As Integer = _rowTops(_scrollOffset)
        Dim ptOffset As Integer = pt.Y - baseY + baseTop  ' 转换到稳定坐标系
        If pt.Y < baseY Then Return -1
        ' 在 _rowTops[_scrollOffset .. count-1] 中二分查找首个 _rowBottoms(i) > ptOffset 的 i
        Dim lo As Integer = _scrollOffset, hi As Integer = _displayRows.Count - 1
        While lo <= hi
            Dim mid As Integer = (lo + hi) >> 1
            If _rowBottoms(mid) <= ptOffset Then
                lo = mid + 1
            Else
                hi = mid - 1
            End If
        End While
        If lo >= _displayRows.Count Then Return -1
        ' 行 lo 的内容范围: [_rowTops(lo), _rowBottoms(lo))；ptOffset 落在其内即命中
        If ptOffset < _rowTops(lo) Then Return -1
        ' 同时校验该行在屏幕上未越过底部限制
        Dim rowScreenBottom As Integer = baseY + (_rowBottoms(lo) - baseTop)
        If rowScreenBottom > bottomLimit Then Return -1
        If _displayRows(lo).Type = DisplayRowType.Item AndAlso Not 项焦点区域包含点(pt, contentRect) Then Return -1
        Return lo
    End Function

    Private Function 项焦点区域包含点(pt As Point, contentRect As Rectangle) As Boolean
        Dim itemFocusW As Integer = 获取项焦点宽度(contentRect)
        If itemFocusW <= 0 Then Return False
        Return pt.X >= contentRect.X AndAlso pt.X < contentRect.X + itemFocusW
    End Function

    Private Function 命中测试矩形(dragRect As Rectangle) As List(Of Integer)
        Dim result As New List(Of Integer)
        If _displayRows.Count = 0 OrElse _scrollOffset >= _displayRows.Count Then Return result
        Dim contentRect = 获取内容区域()
        Dim hasMoreAbove As Boolean = _scrollOffset > 0
        Dim baseY As Integer = contentRect.Y + If(hasMoreAbove, Dpi(更多指示器高度), 0) + 获取有效内容上边距()
        Dim bottomLimit As Integer = contentRect.Bottom - 获取有效内容下边距()
        Dim baseTop As Integer = _rowTops(_scrollOffset)
        Dim rowW As Integer = 获取行绘制宽度(contentRect)
        For i As Integer = _scrollOffset To _displayRows.Count - 1
            Dim rowScreenTop As Integer = baseY + (_rowTops(i) - baseTop)
            Dim rowScreenBottom As Integer = baseY + (_rowBottoms(i) - baseTop)
            If rowScreenBottom > bottomLimit Then Exit For
            Dim rowRect As New Rectangle(contentRect.X, rowScreenTop, rowW, rowScreenBottom - rowScreenTop)
            If dragRect.IntersectsWith(rowRect) AndAlso _displayRows(i).Type = DisplayRowType.Item Then
                result.Add(i)
            End If
        Next
        Return result
    End Function

    Private Function 检测列分隔线(mouseX As Integer) As Integer
        If _columns.Count < 1 Then Return -1
        确保列X缓存()
        Dim hit As Integer = Dpi(ColumnResizeHitZone)
        For i As Integer = 0 To _columns.Count - 1
            Dim sepX As Integer = _columnXCache(i) + _columns(i).Width
            If Math.Abs(mouseX - sepX) <= hit Then
                Return i
            End If
        Next
        Return -1
    End Function

    Private Function 命中测试列(pt As Point) As Integer
        If _columns.Count = 0 Then Return -1
        确保列X缓存()
        For i = 0 To _columns.Count - 1
            Dim colX = _columnXCache(i)
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
            请求V3渲染()
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
            请求V3渲染()
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
        设置选中集合(Array.Empty(Of Integer)())
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
            请求V3渲染()
        End If
    End Sub

#End Region

#Region "截断提示"

    Private Sub 确保工具提示已初始化()
        If _truncTooltip IsNot Nothing AndAlso Not _truncTooltip.IsDisposed Then Return
        _truncTooltip = New FloatingToolTipForm(Me)
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

    Private Sub 更新截断提示(pt As Point, Optional forceRefresh As Boolean = False)
        Dim sourceRect As Rectangle = Rectangle.Empty
        Dim text = 获取截断文本(pt, sourceRect)
        If Not forceRefresh AndAlso
           text = _truncTooltipText AndAlso
           _truncTooltip IsNot Nothing AndAlso
           Not _truncTooltip.IsDisposed AndAlso
           _truncTooltip.Visible Then Return
        If String.IsNullOrEmpty(text) Then
            延迟隐藏截断提示()
        Else
            _truncTooltipText = text
            _truncTooltipSourceScreenRect = RectangleToScreen(sourceRect)
            确保工具提示已初始化()
            Dim screenPt As Point = PointToScreen(New Point(pt.X + Dpi(12), pt.Y + Dpi(20)))
            _truncTooltip.ShowTip(text, screenPt, CreateToolTipStyle())
            _truncTooltipActive = True
        End If
    End Sub

    Private Sub 隐藏截断提示()
        _truncTooltipText = ""
        If _truncTooltip IsNot Nothing AndAlso Not _truncTooltip.IsDisposed Then
            _truncTooltip.Close()
            _truncTooltip.Dispose()
        End If
        _truncTooltip = Nothing
        _truncTooltipActive = False
        _truncTooltipSourceScreenRect = Rectangle.Empty
    End Sub

    Private Sub 延迟隐藏截断提示(Optional keepOwnerBounds As Boolean = False)
        If Not _truncTooltipActive Then Return
        If _truncTooltip Is Nothing OrElse _truncTooltip.IsDisposed Then
            _truncTooltipText = ""
            _truncTooltipActive = False
            _truncTooltipSourceScreenRect = Rectangle.Empty
            Return
        End If
        If keepOwnerBounds Then
            _truncTooltip.ScheduleCloseIfPointerOutside(180, RectangleToScreen(ClientRectangle))
        ElseIf Not _truncTooltipSourceScreenRect.IsEmpty Then
            _truncTooltip.ScheduleCloseIfPointerOutside(180, _truncTooltipSourceScreenRect)
        Else
            _truncTooltip.ScheduleCloseIfPointerOutside(180)
        End If
    End Sub

    Private Function 获取截断文本(pt As Point, ByRef sourceRect As Rectangle) As String
        sourceRect = Rectangle.Empty
        Dim headerRect As Rectangle = 获取列标题区域()
        If 列标题可见 AndAlso _columns.Count > 0 AndAlso headerRect.Contains(pt) Then
            确保列X缓存()
            Dim colIdx As Integer = 命中测试列(pt)
            If colIdx >= 0 Then
                Dim col = _columns(colIdx)
                Dim pad As Padding = Dpi(col.HeaderPadding)
                Dim textRect As New Rectangle(_columnXCache(colIdx) + pad.Left, headerRect.Y + pad.Top, col.Width - pad.Horizontal, headerRect.Height - pad.Vertical)
                If textRect.Contains(pt) Then
                    Dim truncated = 检测文本截断(col.Text, Me.Font, textRect.Width,
                                             TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
                    If Not String.IsNullOrEmpty(truncated) Then sourceRect = textRect
                    Return truncated
                End If
            End If
        End If

        Dim rowIdx = 命中测试行(pt)
        If rowIdx < 0 OrElse rowIdx >= _displayRows.Count Then Return ""
        Dim row = _displayRows(rowIdx)
        Dim rowY = 获取行Y坐标(rowIdx)
        If rowY < 0 Then Return ""

        If row.Type = DisplayRowType.GroupHeader Then
            Dim groupContentRect = 获取内容区域()
            Dim groupRowRect As New Rectangle(groupContentRect.X, rowY, 获取行绘制宽度(groupContentRect), row.Height)
            Dim arrowSize As Integer = Dpi(12)
            Dim arrowMargin As Integer = Dpi(10)
            Dim textX As Integer = groupRowRect.X + arrowMargin + arrowSize + Dpi(6)
            Dim textRect As New Rectangle(textX, groupRowRect.Y, groupRowRect.Right - textX - Dpi(4), groupRowRect.Height)
            If textRect.Contains(pt) Then
                Dim truncated = 检测文本截断(row.Group.Text, Me.Font, textRect.Width,
                                         TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
                If Not String.IsNullOrEmpty(truncated) Then sourceRect = textRect
                Return truncated
            End If
            Return ""
        End If

        If row.Type <> DisplayRowType.Item Then Return ""

        Dim item = row.Item

        Dim contentRect = 获取内容区域()
        Dim availW = 获取行绘制宽度(contentRect)
        Dim rowRect As New Rectangle(contentRect.X, rowY, availW, row.Height)

        Dim iconAreaW = 获取图标区域宽度(item)
        确保列X缓存()
        Dim scaledPadding As Padding = Dpi(项内边距)

        Dim bottomLinesH As Integer = 计算底部文本行高度(item, rowRect.Width)
        Dim upperPartH = rowRect.Height - scaledPadding.Vertical - bottomLinesH

        ' 检测子项区域
        Dim colCount = If(_columns.Count > 0, _columns.Count, 1)
        For colIdx = 0 To colCount - 1
            If colIdx >= item.SubItems.Count Then Exit For
            Dim colX = _columnXCache(Math.Min(colIdx, _columnXCount - 1))
            Dim colW As Integer
            If _columns.Count > 0 Then colW = _columns(colIdx).Width Else colW = rowRect.Width
            If pt.X < colX OrElse pt.X >= colX + colW Then Continue For

            Dim sub_ = item.SubItems(colIdx)
            Dim cellW = colW - scaledPadding.Horizontal
            If colIdx = 0 Then cellW -= iconAreaW
            If cellW <= 0 Then Return ""

            Dim colFixed As Boolean = (_columns.Count > 0 AndAlso colIdx < _columns.Count AndAlso _columns(colIdx).WordWrapHeightFixed)
            Dim cellFlags As TextFormatFlags = If(colFixed AndAlso 自动换行,
                TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine,
                获取文本格式标志())
            Dim contentH = 计算子项内容高度(sub_, cellW, cellFlags)
            Dim cellTop = rowRect.Y + scaledPadding.Top
            Dim startY = cellTop + Math.Max(0, (upperPartH - contentH) \ 2)
            Dim lineY = startY

            ' 主文本
            Dim mf = If(sub_.Font, Me.Font)
            Dim mh = 测量子项主文本高度缓存(sub_, cellW, cellFlags)
            If pt.Y >= lineY AndAlso pt.Y < lineY + mh Then
                Dim textRect As New Rectangle(colX + scaledPadding.Left + If(colIdx = 0, iconAreaW, 0), lineY, cellW, mh)
                Dim truncated = 检测文本截断(sub_.Text, mf, cellW, cellFlags)
                If Not String.IsNullOrEmpty(truncated) Then sourceRect = textRect
                Return truncated
            End If
            lineY += mh

            ' 附加文本行
            For Each line_ In sub_.ExtraLines
                lineY += Dpi(文本行间距)
                Dim lf = If(line_.Font, Me.Font)
                Dim lh = 测量文本行高度缓存(line_, cellW, cellFlags)
                If pt.Y >= lineY AndAlso pt.Y < lineY + lh Then
                    Dim textRect As New Rectangle(colX + scaledPadding.Left + If(colIdx = 0, iconAreaW, 0), lineY, cellW, lh)
                    Dim truncated = 检测文本截断(line_.Text, lf, cellW, cellFlags)
                    If Not String.IsNullOrEmpty(truncated) Then sourceRect = textRect
                    Return truncated
                End If
                lineY += lh
            Next

            Return ""
        Next

        ' 检测底部附加文本行区域
        If item.BottomLines.Count > 0 Then
            Dim blW = rowRect.Width - scaledPadding.Horizontal
            Dim blY = rowRect.Y + scaledPadding.Top + upperPartH + Dpi(底部文本行间距)
            For Each bl In item.BottomLines
                blY += Dpi(文本行间距)
                Dim lf = If(bl.Font, Me.Font)
                Dim lh = 测量文本行高度缓存(bl, blW, 获取文本格式标志())
                If pt.Y >= blY AndAlso pt.Y < blY + lh Then
                    Dim textRect As New Rectangle(rowRect.X + scaledPadding.Left, blY, blW, lh)
                    Dim truncated = 检测文本截断(bl.Text, lf, blW, 获取文本格式标志())
                    If Not String.IsNullOrEmpty(truncated) Then sourceRect = textRect
                    Return truncated
                End If
                blY += lh
            Next
        End If

        Return ""
    End Function

    Private Function 检测文本截断(text As String, font As Font, availW As Integer, flags As TextFormatFlags) As String
        If String.IsNullOrEmpty(text) Then Return ""
        If availW <= 0 Then Return text
        Dim wraps As Boolean = (flags And TextFormatFlags.WordBreak) = TextFormatFlags.WordBreak AndAlso
                               (flags And TextFormatFlags.SingleLine) <> TextFormatFlags.SingleLine
        If wraps Then Return ""
        Dim fullW = 测量文本宽度(text, font, flags)
        If fullW > availW Then Return text
        Return ""
    End Function

#End Region

#Region "悬停动画"

    Private Sub 更新悬停(newIndex As Integer)
        If newIndex < 0 OrElse newIndex >= _displayRows.Count OrElse _displayRows(newIndex).Type <> DisplayRowType.Item Then
            newIndex = -1
        End If
        If newIndex = _hoverRowIndex Then Return
        Dim oldIndex As Integer = _hoverRowIndex
        _hoverRowIndex = newIndex

        If 动画时长 <= 0 OrElse Not Me.IsHandleCreated Then
            _hoverAnimActive = False
            请求V3渲染()
            Return
        End If

        If newIndex < 0 Then
            _hoverAnimActive = False
            请求V3渲染()
            Return
        End If

        Dim newY = 获取行Y坐标(newIndex)
        Dim newH = _displayRows(newIndex).Height
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
            _hoverAnimFromH = newH
        End If

        _hoverAnimToY = newY
        _hoverAnimToH = newH
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

        Dim itemFocusW As Integer = 获取项焦点宽度(contentRect)
        If itemFocusW <= 0 Then Return Rectangle.Empty

        Dim top As Integer = CInt(Math.Floor(Math.Min(_hoverAnimFromY, _hoverAnimToY)))
        Dim bottom As Integer = CInt(Math.Ceiling(Math.Max(_hoverAnimFromY + _hoverAnimFromH, _hoverAnimToY + _hoverAnimToH)))
        Dim rect As New Rectangle(contentRect.X, top, itemFocusW, Math.Max(0, bottom - top))
        rect.Inflate(2, 2)
        Return Rectangle.Intersect(ClientRectangle, rect)
    End Function

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
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        InvalidateV3TextResources()
        全部项高度缓存失效()
        _moreSymbolFont?.Dispose()
        _moreSymbolFont = Nothing
        _moreSymbolFontKey = 0F
        _columnXDirty = True
        重建显示列表()
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        全部项高度缓存失效()
        _moreSymbolFont?.Dispose()
        _moreSymbolFont = Nothing
        _moreSymbolFontKey = 0F
        _columnXDirty = True
        重建显示列表()
        请求V3渲染()
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
        隐藏截断提示()
        释放GDI缓存()
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
