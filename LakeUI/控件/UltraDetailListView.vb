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

        Friend Property Owner As UltraDetailListView

        Private _font As Font = Nothing
        <Description("字体，留空时使用控件默认字体")>
        Public Property Font As Font
            Get
                Return _font
            End Get
            Set(value As Font)
                _font = value
                If Owner IsNot Nothing Then Owner.InvalidateItemFontResources()
            End Set
        End Property

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

        Friend Property Owner As UltraDetailListView

        Private _font As Font = Nothing
        <Description("字体，留空时使用控件默认字体")>
        Public Property Font As Font
            Get
                Return _font
            End Get
            Set(value As Font)
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
        <DefaultValue(""), Description("列标题文本")>
        Public Property Text As String = ""

        <DefaultValue(100), Description("列宽度")>
        Public Property Width As Integer = 100

        <DefaultValue(GetType(Padding), "10, 0, 0, 0"), Description("列标题文字内边距")>
        Public Property HeaderPadding As Padding = New Padding(10, 0, 0, 0)

        <DefaultValue(False), Description("是否允许慢速单击编辑此列的子项主文本")>
        Public Property AllowLabelEdit As Boolean = False

        <DefaultValue(False), Description("自动换行时是否固定高度（不参与项高度计算）；启用后此列内容超出由其他列决定的行高时以省略号截断")>
        Public Property WordWrapHeightFixed As Boolean = False

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

    ''' <summary>取/创建 D2D Solid 画刷（仅在当前 RT 上使用，复用按颜色键）。</summary>
    Private Function 获取D2D画刷(rt As Vortice.Direct2D1.ID2D1RenderTarget, color As Color) As Vortice.Direct2D1.ID2D1SolidColorBrush
        Return _当前合成器.BrushCache.Get(rt, color)
    End Function

    Private Sub 填充矩形_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget, rect As RectangleF, color As Color)
        If color.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        rt.FillRectangle(D2DHelper.ToD2DRect(rect), 获取D2D画刷(rt, color))
    End Sub

    Private Sub 描边矩形_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget, rect As RectangleF, color As Color, strokeWidth As Single)
        If color.A = 0 OrElse strokeWidth <= 0 Then Return
        rt.DrawRectangle(D2DHelper.ToD2DRect(rect), 获取D2D画刷(rt, color), strokeWidth)
    End Sub

    Private Sub 绘制水平线_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget, x1 As Single, x2 As Single, y As Single, color As Color, strokeWidth As Single)
        If color.A = 0 OrElse strokeWidth <= 0 Then Return
        Dim br = 获取D2D画刷(rt, color)
        rt.DrawLine(New System.Numerics.Vector2(x1, y), New System.Numerics.Vector2(x2, y), br, strokeWidth)
    End Sub

    Private Sub 绘制垂直线_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget, x As Single, y1 As Single, y2 As Single, color As Color, strokeWidth As Single)
        If color.A = 0 OrElse strokeWidth <= 0 Then Return
        Dim br = 获取D2D画刷(rt, color)
        rt.DrawLine(New System.Numerics.Vector2(x, y1), New System.Numerics.Vector2(x, y2), br, strokeWidth)
    End Sub

    Private Sub 绘制背景与边框_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget, brushCache As D2DHelper.SolidColorBrushCache)
        Dim s As Single = DpiScale()
        Dim boundsRect As New RectangleF(0, 0, Me.Width - 1, Me.Height - 1)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * s / 2.0F
            boundsRect.Inflate(-half, -half)
        End If
        If 边框圆角半径 > 0 Then
            Using geo = RectangleRenderer.创建圆角矩形几何(boundsRect, 边框圆角半径 * s)
                RectangleRenderer.绘制圆角背景_D2D(rt, geo, boundsRect, 背景颜色, Color.Empty, 0, brushCache)
                If 边框颜色.A > 0 AndAlso 边框宽度 > 0 Then
                    RectangleRenderer.绘制圆角边框_D2D(rt, geo, 边框颜色, 边框宽度 * s, brushCache)
                End If
            End Using
        Else
            RectangleRenderer.绘制矩形背景_D2D(rt, boundsRect, 背景颜色, Color.Empty, 0, brushCache)
            If 边框颜色.A > 0 AndAlso 边框宽度 > 0 Then
                RectangleRenderer.绘制矩形边框_D2D(rt, boundsRect, 边框颜色, 边框宽度 * s, brushCache)
            End If
        End If
    End Sub

    Private Sub 绘制滚动条_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget)
        If _scrollBar.TrackRect.IsEmpty Then Return
        Dim s As Single = DpiScale()
        _scrollBar.Draw_D2D(rt, Me.Width, Me.Height, CInt(边框宽度 * s), CInt(边框圆角半径 * s),
            Dpi(滚动条宽度), 滚动条轨道颜色, 滚动条滑块颜色, 滚动条悬停颜色)
    End Sub

    Private Sub 绘制横向滚动条_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget)
        If _hScrollBar.TrackRect.IsEmpty Then Return
        Dim s As Single = DpiScale()
        _hScrollBar.DrawHorizontal_D2D(rt, Me.Width, Me.Height, CInt(边框宽度 * s), CInt(边框圆角半径 * s),
            Dpi(滚动条宽度), 滚动条轨道颜色, 滚动条滑块颜色, 滚动条悬停颜色)
    End Sub

    ''' <summary>D2D 绘制列标题背景与分隔线（不绘文字）。</summary>
    Private Sub 绘制列标题形状_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget)
        Dim headerRect As Rectangle = 获取列标题区域()
        填充矩形_D2D(rt, headerRect, 列标题背景颜色)
        确保列X缓存()
        Dim dpiS As Single = DpiScale()
        Dim sw As Single = Math.Max(1.0F, 列标题分隔线宽度 * dpiS)
        For i As Integer = 0 To _columns.Count - 1
            Dim col = _columns(i)
            Dim x As Integer = _columnXCache(i)
            绘制垂直线_D2D(rt, x + col.Width - 1, headerRect.Y + 4, headerRect.Bottom - 4, 列标题分隔线颜色, sw)
        Next
        绘制水平线_D2D(rt, headerRect.X, headerRect.Right, headerRect.Bottom - 1, 列标题分隔线颜色, sw)
    End Sub

    ''' <summary>D2D 绘制分组行背景、贝塞尔三角箭头、底部分隔线（不绘组名文字）。</summary>
    Private Sub 绘制分组标题行形状_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget, grp As ListGroup, rect As Rectangle)
        填充矩形_D2D(rt, rect, 分组背景颜色)

        Dim arrowSize As Integer = Dpi(12)
        Dim arrowMargin As Integer = Dpi(10)
        Dim arrowX As Integer = rect.X + arrowMargin
        Dim arrowY As Integer = rect.Y + (rect.Height - arrowSize) \ 2
        Dim effectiveColor As Color = If(grp.ForeColor <> Color.Empty, grp.ForeColor, 分组文字颜色)
        Using path As GraphicsPath = 创建圆角箭头路径(arrowX, arrowY, arrowSize, grp.IsCollapsed)
            Using geo = 路径转D2D几何(path)
                If geo IsNot Nothing Then
                    rt.FillGeometry(geo, 获取D2D画刷(rt, effectiveColor), Nothing)
                End If
            End Using
        End Using

        绘制水平线_D2D(rt, rect.X, rect.Right, rect.Bottom - 1, 分组分隔线颜色, 1.0F)
    End Sub

    ''' <summary>把 GDI+ GraphicsPath 转换为 D2D PathGeometry。仅支持线段与三次贝塞尔，足够本控件使用。</summary>
    Private Shared Function 路径转D2D几何(path As GraphicsPath) As Vortice.Direct2D1.ID2D1PathGeometry
        If path Is Nothing OrElse path.PointCount = 0 Then Return Nothing
        Dim pts() As PointF = path.PathPoints
        Dim types() As Byte = path.PathTypes
        Dim geo = D2DHelper.GetD2DFactory().CreatePathGeometry()
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

    ''' <summary>D2D 绘制更多指示器渐变背景（不绘 ▲▼ 符号）。</summary>
    Private Sub 绘制更多指示器形状_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget, rect As Rectangle, isTop As Boolean)
        If rect.Height < 2 Then Return
        Dim c1 As Color = Color.FromArgb(200, 背景颜色)
        Dim c2 As Color = Color.FromArgb(0, 背景颜色)
        Dim startPt As New System.Numerics.Vector2(rect.X, If(isTop, rect.Y, rect.Bottom - 1))
        Dim endPt As New System.Numerics.Vector2(rect.X, If(isTop, rect.Bottom - 1, rect.Y))
        Dim stops() As Vortice.Direct2D1.GradientStop = {
            New Vortice.Direct2D1.GradientStop With {.Position = 0F, .Color = D2DHelper.ToColor4(c1)},
            New Vortice.Direct2D1.GradientStop With {.Position = 1.0F, .Color = D2DHelper.ToColor4(c2)}}
        Dim gsc = rt.CreateGradientStopCollection(stops, Vortice.Direct2D1.Gamma.StandardRgb, Vortice.Direct2D1.ExtendMode.Clamp)
        Try
            Dim props As New Vortice.Direct2D1.LinearGradientBrushProperties With {.StartPoint = startPt, .EndPoint = endPt}
            Using br = rt.CreateLinearGradientBrush(props, gsc)
                rt.FillRectangle(D2DHelper.ToD2DRect(rect), br)
            End Using
        Finally
            gsc.Dispose()
        End Try
    End Sub

    ''' <summary>D2D 绘制拖选框（半透明填充 + 边框）。</summary>
    Private Sub 绘制拖选框_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget)
        If Not _isDragSelecting Then Return
        Dim rect As Rectangle = 获取拖选矩形()
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        填充矩形_D2D(rt, rect, 选框填充颜色)
        描边矩形_D2D(rt, rect, 选框边框颜色, 1.0F)
    End Sub

    ''' <summary>D2D 绘制拖动排序指示线。</summary>
    Private Sub 绘制拖动排序指示线_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget)
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
        绘制水平线_D2D(rt, contentRect.X, contentRect.X + availW, lineY, 拖动排序指示线颜色, 拖动排序指示线宽 * DpiScale())
    End Sub

    ''' <summary>D2D 绘制全部行的形状层（背景/选中/悬停/Checked 边框/分组背景与箭头/更多指示器渐变）。</summary>
    Private Sub 绘制全部行形状_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget)
        If _displayRows.Count = 0 Then Return
        Dim contentRect = 获取内容区域()
        If contentRect.Height <= 0 OrElse contentRect.Width <= 0 Then Return

        Dim hasMoreAbove As Boolean = _scrollOffset > 0
        Dim topIndicatorH As Integer = If(hasMoreAbove, Dpi(更多指示器高度), 0)
        Dim currentY As Integer = contentRect.Y + topIndicatorH + 获取有效内容上边距()
        Dim bottomLimit As Integer = contentRect.Bottom - 获取有效内容下边距()
        Dim lastDrawnIndex As Integer = _scrollOffset - 1

        Dim inset As Integer = 获取边框内边距()
        Dim scrollW As Integer = If(Not _scrollBar.TrackRect.IsEmpty, Me.Width - inset - _scrollBar.VisualLeft, 0)
        Dim availW As Integer = contentRect.Width - scrollW
        If _columns.Count > 0 Then availW = Math.Max(availW, 获取总列宽())
        Dim dpiS As Single = DpiScale()

        If _hoverAnimActive AndAlso _hoverRowIndex >= 0 AndAlso Not _selectedIndices.Contains(_hoverRowIndex) Then
            Dim t As Single = _hoverAnim.Progress
            Dim animY As Single = _hoverAnimFromY + (_hoverAnimToY - _hoverAnimFromY) * t
            Dim animH As Single = _hoverAnimFromH + (_hoverAnimToH - _hoverAnimFromH) * t
            填充矩形_D2D(rt, New RectangleF(contentRect.X, animY, availW, animH), 项悬停背景颜色)
        End If

        For i As Integer = _scrollOffset To _displayRows.Count - 1
            Dim row = _displayRows(i)
            Dim rowH As Integer = row.Height
            Dim spacing As Integer = If(i = _scrollOffset, 0, row.Spacing)
            If currentY + spacing + rowH > bottomLimit Then Exit For
            currentY += spacing
            Dim rowRect As New Rectangle(contentRect.X, currentY, availW, rowH)

            If _selectedIndices.Contains(i) Then
                填充矩形_D2D(rt, rowRect, 项选中背景颜色)
            ElseIf i = _hoverRowIndex AndAlso Not _hoverAnimActive Then
                填充矩形_D2D(rt, rowRect, 项悬停背景颜色)
            End If

            If row.Type = DisplayRowType.GroupHeader Then
                绘制分组标题行形状_D2D(rt, row.Group, rowRect)
            ElseIf row.Item.Checked AndAlso 项高亮边框宽度 > 0 Then
                Dim sb As Single = 项高亮边框宽度 * dpiS
                Dim half As Single = sb / 2.0F
                描边矩形_D2D(rt, New RectangleF(rowRect.X + half, rowRect.Y + half, rowRect.Width - sb, rowRect.Height - sb), 项高亮边框颜色, sb)
            End If

            currentY += rowH
            lastDrawnIndex = i
        Next

        Dim hasMoreBelow As Boolean = lastDrawnIndex < _displayRows.Count - 1
        If hasMoreBelow AndAlso currentY < bottomLimit Then
            绘制更多指示器形状_D2D(rt, New Rectangle(contentRect.X, currentY, availW, bottomLimit - currentY), False)
        End If
        If hasMoreAbove Then
            绘制更多指示器形状_D2D(rt, New Rectangle(contentRect.X, contentRect.Y, availW, topIndicatorH), True)
        End If
    End Sub

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        释放D2D资源()
        MyBase.OnHandleDestroyed(e)
    End Sub

    Private Sub 释放D2D资源()
        For Each c In _iconBitmaps.Values
            Try : c.Dispose() : Catch : End Try
        Next
        _iconBitmaps.Clear()
    End Sub

    Private Sub 释放GDI缓存()
        _moreSymbolFont?.Dispose()
        _moreSymbolFont = Nothing
        _moreSymbolFontKey = 0F
        释放D2D资源()
    End Sub

    Private Function 获取D2D位图(rt As Vortice.Direct2D1.ID2D1RenderTarget, src As Image) As Vortice.Direct2D1.ID2D1Bitmap
        If src Is Nothing OrElse rt Is Nothing Then Return Nothing
        If _当前合成器 IsNot Nothing Then Return _当前合成器.GetBitmapCache(src).GetBitmap(rt, src)
        Dim cache As D2DHelper.D2DBitmapCache = Nothing
        If Not _iconBitmaps.TryGetValue(src, cache) Then
            cache = New D2DHelper.D2DBitmapCache()
            _iconBitmaps(src) = cache
        End If
        Return cache.GetBitmap(rt, src)
    End Function

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
    ''' 背景采样源（超容器背景映射）。设置后控件会通过 V2 背景穿透采样此控件作为底图，
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
                _backgroundSource = value
                Me.Invalidate()
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
        Return Dpi(If(内容边距.Top < 0, 项内边距.Top, 内容边距.Top))
    End Function

    Private Function 获取有效内容下边距() As Integer
        Return Dpi(If(内容边距.Bottom < 0, 项内边距.Bottom, 内容边距.Bottom))
    End Function

    Private Function 获取行前间距(rowIndex As Integer) As Integer
        If rowIndex <= _scrollOffset Then Return 0
        Dim spacing = Dpi(项间距)
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
                Dim first = _selectedMin
                _selectedIndices.Clear()
                _selectedIndices.Add(first)
                _selectedMin = first
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

    ' --- D2D 资源 ---
    Private _当前合成器 As WindowCompositor
    Private ReadOnly _iconBitmaps As New Dictionary(Of Image, D2DHelper.D2DBitmapCache)

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
            Return _selectedMin
        End Get
        Set(value As Integer)
            _selectedIndices.Clear()
            _selectedMin = -1
            If value >= 0 AndAlso value < _displayRows.Count Then
                _selectedIndices.Add(value)
                _selectedMin = value
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
        Dim newSet As New HashSet(Of Integer)(indices)
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
            Me.Invalidate()
            RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
        End If
    End Sub

    Private Sub 同步选中最小()
        _selectedMin = -1
        For Each i In _selectedIndices
            If _selectedMin = -1 OrElse i < _selectedMin Then _selectedMin = i
        Next
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
            maxSubH = TextRenderer.MeasureText("Ag", Me.Font).Height
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
        Dim f As Font = If(sub_.Font, Me.Font)
        Dim proposed As New Size(Math.Max(1, availWidth), Integer.MaxValue)
        Dim flags As TextFormatFlags = 获取文本格式标志()
        Dim totalH As Integer = TextRenderer.MeasureText(If(String.IsNullOrEmpty(sub_.Text), "Ag", sub_.Text), f, proposed, flags).Height
        For Each line_ In sub_.ExtraLines
            totalH += Dpi(文本行间距)
            totalH += TextRenderer.MeasureText(If(String.IsNullOrEmpty(line_.Text), "Ag", line_.Text), If(line_.Font, Me.Font), proposed, flags).Height
        Next
        Return totalH
    End Function

    Private Sub 全部项高度缓存失效()
        For Each itm In _items
            itm.InvalidateCache()
        Next
    End Sub

    Friend Sub InvalidateItemFontResources()
        D2DHelperV2.InvalidateTextFormatCache(Me)
        全部项高度缓存失效()
        _columnXDirty = True
        重建显示列表()
        D2DHelperV2.RefreshFontDependentRendering(Me)
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
            h -= (Dpi(滚动条宽度) + ScrollBarRenderer.Margin * 2)
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

    Private Function 获取列X列表() As List(Of Integer)
        ' 兼容性包装：仅供必须返回 List 的极少路径使用，热路径请用 确保列X缓存() + _columnXCache。
        确保列X缓存()
        Dim result As New List(Of Integer)(_columnXCount)
        For i As Integer = 0 To _columnXCount - 1
            result.Add(_columnXCache(i))
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

        预计算滚动条布局()
        预计算横向滚动条布局()

        Dim ssaa As Integer = Math.Max(1, CInt(超采样倍率))
        If Class1.GlobalSSAA <> Class1.SuperSamplingScaleEnum.OFF Then ssaa = Math.Max(ssaa, CInt(Class1.GlobalSSAA))

        ' --- 第一遍：D2D 画形状（背景/边框/选中/悬停/箭头/拖选/指示线/滚动条/更多指示器）---
        Using scope = D2DHelperV2.BeginPaint(e, Me, ssaa)
            If scope Is Nothing Then Return
            _当前合成器 = scope.Compositor
            Try
                ' V2 背景穿透：若指定了 BackgroundSource，先把 source 的像素采样到 BackgroundLayer
                ' 作为底图；后续 GraphicsLayer 用 BackColor 做半透明遮罩 / 行高亮叠加。
                If _backgroundSource IsNot Nothing Then
                    BackgroundPenetrationV2.PaintBackground(Me, scope, _backgroundSource)
                ElseIf 背景颜色.A = 255 Then
                    scope.BackgroundLayer.Clear(D2DHelper.ToColor4(背景颜色))
                End If
                Dim gRT = scope.GraphicsLayer
                绘制背景与边框_D2D(gRT, scope.Compositor.BrushCache)

                ' 内容区域裁剪
                Dim inset As Integer = 获取边框内边距()
                Dim clipRect As New RectangleF(inset, inset, Me.Width - inset * 2 - 1, Me.Height - inset * 2 - 1)
                If clipRect.Width > 0 AndAlso clipRect.Height > 0 Then
                    Dim s As Single = DpiScale()
                    Dim clipGeo As Vortice.Direct2D1.ID2D1Geometry = Nothing
                    If 边框圆角半径 > 0 Then
                        clipGeo = RectangleRenderer.创建圆角矩形几何(clipRect, Math.Max(0, (边框圆角半径 - 边框宽度) * s))
                    End If
                    Dim clipPushed As Boolean = False
                    Try
                        If clipGeo IsNot Nothing Then
                            D2DHelper.PushGeometryClip(gRT, clipGeo, clipRect)
                            clipPushed = True
                        Else
                            gRT.PushAxisAlignedClip(New Vortice.RawRectF(clipRect.Left, clipRect.Top, clipRect.Right, clipRect.Bottom), Vortice.Direct2D1.AntialiasMode.PerPrimitive)
                        End If
                        If 列标题可见 AndAlso _columns.Count > 0 Then 绘制列标题形状_D2D(gRT)
                        绘制全部行形状_D2D(gRT)
                        绘制拖选框_D2D(gRT)
                        绘制拖动排序指示线_D2D(gRT)
                    Finally
                        If clipPushed Then
                            gRT.PopLayer()
                        Else
                            gRT.PopAxisAlignedClip()
                        End If
                        If clipGeo IsNot Nothing Then clipGeo.Dispose()
                    End Try
                End If

                ' 滚动条无须裁剪到内容区
                scope.FlushGraphics()
                绘制滚动条_D2D(scope.DCRenderTarget)
                绘制横向滚动条_D2D(scope.DCRenderTarget)

                ' --- 第二遍：在 TextLayer 上叠加文字 / 图标（DirectWrite，子像素 ClearType）---
                Dim textRT As Vortice.Direct2D1.ID2D1RenderTarget = scope.TextLayer
                Dim textInset As Integer = 获取边框内边距()
                Dim textClipRect As New RectangleF(textInset, textInset, Me.Width - textInset * 2 - 1, Me.Height - textInset * 2 - 1)
                If textClipRect.Width > 0 AndAlso textClipRect.Height > 0 Then
                    Dim sg As Single = DpiScale()
                    Dim textClipGeo As Vortice.Direct2D1.ID2D1Geometry = Nothing
                    If 边框圆角半径 > 0 Then
                        textClipGeo = RectangleRenderer.创建圆角矩形几何(textClipRect, Math.Max(0, (边框圆角半径 - 边框宽度) * sg))
                    End If
                    Dim textClipPushed As Boolean = False
                    Try
                        If textClipGeo IsNot Nothing Then
                            D2DHelper.PushGeometryClip(textRT, textClipGeo, textClipRect)
                            textClipPushed = True
                        Else
                            textRT.PushAxisAlignedClip(New Vortice.RawRectF(textClipRect.Left, textClipRect.Top, textClipRect.Right, textClipRect.Bottom), Vortice.Direct2D1.AntialiasMode.PerPrimitive)
                        End If
                        If 列标题可见 AndAlso _columns.Count > 0 Then 绘制列标题_D2D(textRT)
                        绘制全部行_D2D(textRT)
                    Finally
                        If textClipPushed Then
                            textRT.PopLayer()
                        Else
                            textRT.PopAxisAlignedClip()
                        End If
                        If textClipGeo IsNot Nothing Then textClipGeo.Dispose()
                    End Try
                End If
            Finally
                _当前合成器 = Nothing
            End Try
        End Using
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
            Dim hsbSpace As Integer = If(需要横向滚动条(), Dpi(滚动条宽度) + ScrollBarRenderer.Margin * 2, 0)
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
        Dim vsbReserved As Integer = If(Not _scrollBar.TrackRect.IsEmpty, Me.Width - inset - _scrollBar.VisualLeft, 0)
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
    Private Sub 绘制列标题_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget)
        Dim headerRect As Rectangle = 获取列标题区域()
        确保列X缓存()
        Dim dpiS As Single = DpiScale()
        Dim flags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
        Dim brushCache = _当前合成器.BrushCache
        Dim tfc = _当前合成器.TextFormatCache
        For i As Integer = 0 To _columns.Count - 1
            Dim col = _columns(i)
            Dim x As Integer = _columnXCache(i)
            Dim pad As Padding = Dpi(col.HeaderPadding)
            Dim textRect As New Rectangle(x + pad.Left, headerRect.Y + pad.Top, col.Width - pad.Horizontal, headerRect.Height - pad.Vertical)
            D2DTextRenderer.DrawText(rt, col.Text, Me.Font, textRect, 列标题文字颜色, flags, dpiS, tfc, brushCache)
        Next
    End Sub

    ''' <summary>D2D 文字层：行文字 + 行图标 + 分组箭头/文字 + 更多指示器符号。</summary>
    Private Sub 绘制全部行_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget)
        If _displayRows.Count = 0 Then Return
        Dim contentRect = 获取内容区域()
        If contentRect.Height <= 0 OrElse contentRect.Width <= 0 Then Return

        Dim hasMoreAbove As Boolean = _scrollOffset > 0
        Dim topIndicatorH As Integer = If(hasMoreAbove, Dpi(更多指示器高度), 0)
        Dim currentY As Integer = contentRect.Y + topIndicatorH + 获取有效内容上边距()
        Dim bottomLimit As Integer = contentRect.Bottom - 获取有效内容下边距()
        Dim lastDrawnIndex As Integer = _scrollOffset - 1

        Dim inset As Integer = 获取边框内边距()
        Dim scrollW As Integer = If(Not _scrollBar.TrackRect.IsEmpty, Me.Width - inset - _scrollBar.VisualLeft, 0)
        Dim availW As Integer = contentRect.Width - scrollW
        If _columns.Count > 0 Then availW = Math.Max(availW, 获取总列宽())
        确保列X缓存()
        Dim colXListLocal As List(Of Integer) = 获取列X列表()

        For i As Integer = _scrollOffset To _displayRows.Count - 1
            Dim row = _displayRows(i)
            Dim rowH As Integer = row.Height
            Dim spacing As Integer = If(i = _scrollOffset, 0, row.Spacing)
            If currentY + spacing + rowH > bottomLimit Then Exit For
            currentY += spacing
            Dim rowRect As New Rectangle(contentRect.X, currentY, availW, rowH)

            If row.Type = DisplayRowType.GroupHeader Then
                绘制分组标题行_D2D(rt, row.Group, rowRect)
            Else
                绘制项行_D2D(rt, row.Item, rowRect, colXListLocal, i)
            End If

            currentY += rowH
            lastDrawnIndex = i
        Next

        Dim hasMoreBelow As Boolean = lastDrawnIndex < _displayRows.Count - 1
        If hasMoreBelow AndAlso currentY < bottomLimit Then
            绘制更多指示器符号_D2D(rt, New Rectangle(contentRect.X, currentY, availW, bottomLimit - currentY), False)
        End If
        If hasMoreAbove Then
            绘制更多指示器符号_D2D(rt, New Rectangle(contentRect.X, contentRect.Y, availW, topIndicatorH), True)
        End If
    End Sub

    Private Sub 绘制分组标题行_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget, grp As ListGroup, rect As Rectangle)
        Dim effectiveColor As Color = If(grp.ForeColor <> Color.Empty, grp.ForeColor, 分组文字颜色)
        Dim arrowSize As Integer = Dpi(12)
        Dim arrowMargin As Integer = Dpi(10)
        Dim arrowX As Integer = rect.X + arrowMargin
        Dim textX As Integer = arrowX + arrowSize + Dpi(6)
        Dim textRect As New Rectangle(textX, rect.Y, rect.Right - textX - Dpi(4), rect.Height)
        D2DTextRenderer.DrawText(rt, grp.Text, Me.Font, textRect, effectiveColor,
            TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine,
            DpiScale(), _当前合成器.TextFormatCache, _当前合成器.BrushCache)
    End Sub

    Private Sub 绘制项行_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget, item As ListItem, rowRect As Rectangle,
                              colXList As List(Of Integer), Optional displayRowIndex As Integer = -1)
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
        Dim dpiS As Single = DpiScale()
        Dim brushCache = _当前合成器.BrushCache
        Dim tfc = _当前合成器.TextFormatCache

        Dim colCount As Integer = If(_columns.Count > 0, _columns.Count, 1)
        For colIdx As Integer = 0 To colCount - 1
            If colIdx >= item.SubItems.Count Then Exit For
            Dim sub_ = item.SubItems(colIdx)
            Dim colX As Integer = colXList(Math.Min(colIdx, colXList.Count - 1))
            Dim colW As Integer = If(_columns.Count > 0, _columns(colIdx).Width, rowRect.Width)

            Dim colFixed As Boolean = (_columns.Count > 0 AndAlso colIdx < _columns.Count AndAlso _columns(colIdx).WordWrapHeightFixed)
            Dim cellFlags As TextFormatFlags = If(colFixed AndAlso 自动换行,
                (flags And Not TextFormatFlags.WordBreak) Or TextFormatFlags.EndEllipsis,
                flags)

            Dim cellRect As New Rectangle(
                colX + scaledPadding.Left, rowRect.Y + scaledPadding.Top,
                colW - scaledPadding.Horizontal, upperPartH)

            If colIdx = 0 AndAlso hasIcon AndAlso item.Icon IsNot Nothing Then
                Dim iconX As Integer = cellRect.X
                Dim iconY As Integer = rowRect.Y + scaledPadding.Top + (upperPartH - scaledIconSize.Height) \ 2
                Dim bmp = 获取D2D位图(rt, item.Icon)
                If bmp IsNot Nothing Then
                    rt.DrawBitmap(bmp,
                        New Vortice.Mathematics.Rect(iconX, iconY, scaledIconSize.Width, scaledIconSize.Height),
                        1.0F, Vortice.Direct2D1.BitmapInterpolationMode.Linear,
                        New Vortice.Mathematics.Rect(0, 0, bmp.Size.Width, bmp.Size.Height))
                End If
                cellRect = New Rectangle(cellRect.X + iconAreaW, cellRect.Y, cellRect.Width - iconAreaW, cellRect.Height)
            End If

            If cellRect.Width <= 0 OrElse cellRect.Height <= 0 Then Continue For
            If skipEditingCell AndAlso colIdx = _editColumnIndex Then Continue For

            Dim contentH As Integer = 计算子项内容高度(sub_, cellRect.Width)
            If colFixed Then contentH = Math.Min(contentH, cellRect.Height)
            Dim startY As Integer = cellRect.Y + Math.Max(0, (cellRect.Height - contentH) \ 2)
            Dim lineY As Integer = startY
            Dim proposed As New Size(cellRect.Width, Integer.MaxValue)

            Dim mf As Font = If(sub_.Font, Me.Font)
            Dim mc As Color = If(sub_.ForeColor <> Color.Empty, sub_.ForeColor, 项文本颜色)
            Dim mh As Integer = TextRenderer.MeasureText(If(String.IsNullOrEmpty(sub_.Text), "Ag", sub_.Text), mf, proposed, cellFlags).Height
            If Not String.IsNullOrEmpty(sub_.Text) Then
                Dim remainH As Integer = cellRect.Bottom - lineY
                If remainH > 0 Then
                    D2DTextRenderer.DrawText(rt, sub_.Text, mf, New Rectangle(cellRect.X, lineY, cellRect.Width, Math.Min(mh, remainH)), mc, cellFlags, dpiS, tfc, brushCache)
                End If
            End If
            lineY += mh

            For Each line_ In sub_.ExtraLines
                lineY += Dpi(文本行间距)
                Dim lf As Font = If(line_.Font, Me.Font)
                Dim lc As Color = If(line_.ForeColor <> Color.Empty, line_.ForeColor, 项文本颜色)
                Dim lh As Integer = TextRenderer.MeasureText(If(String.IsNullOrEmpty(line_.Text), "Ag", line_.Text), lf, proposed, cellFlags).Height
                If Not String.IsNullOrEmpty(line_.Text) Then
                    Dim remainH As Integer = cellRect.Bottom - lineY
                    If remainH > 0 Then
                        D2DTextRenderer.DrawText(rt, line_.Text, lf, New Rectangle(cellRect.X, lineY, cellRect.Width, Math.Min(lh, remainH)), lc, cellFlags, dpiS, tfc, brushCache)
                    End If
                End If
                lineY += lh
            Next
        Next

        If item.BottomLines.Count > 0 Then
            Dim blX As Integer = rowRect.X + scaledPadding.Left
            Dim blW As Integer = rowRect.Width - scaledPadding.Horizontal
            Dim blY As Integer = rowRect.Y + scaledPadding.Top + upperPartH + Dpi(底部文本行间距)
            Dim blProposed As New Size(Math.Max(1, blW), Integer.MaxValue)
            For Each bl In item.BottomLines
                blY += Dpi(文本行间距)
                Dim lf As Font = If(bl.Font, Me.Font)
                Dim lc As Color = If(bl.ForeColor <> Color.Empty, bl.ForeColor, 项文本颜色)
                Dim lh As Integer = TextRenderer.MeasureText(If(String.IsNullOrEmpty(bl.Text), "Ag", bl.Text), lf, blProposed, flags).Height
                If Not String.IsNullOrEmpty(bl.Text) Then
                    D2DTextRenderer.DrawText(rt, bl.Text, lf, New Rectangle(blX, blY, blW, lh), lc, flags, dpiS, tfc, brushCache)
                End If
                blY += lh
            Next
        End If
    End Sub

    Private Sub 绘制更多指示器符号_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget, rect As Rectangle, isTop As Boolean)
        If rect.Height < 2 Then Return
        Dim symbol As String = If(isTop, "▲", "▼")
        Dim symbolSize As Single = Math.Max(7, Me.Font.Size - 1)
        If _moreSymbolFont Is Nothing OrElse _moreSymbolFontKey <> symbolSize Then
            _moreSymbolFont?.Dispose()
            _moreSymbolFont = New Font(Me.Font.FontFamily, symbolSize, FontStyle.Regular)
            _moreSymbolFontKey = symbolSize
        End If
        D2DTextRenderer.DrawText(rt, symbol, _moreSymbolFont, rect, 更多指示器颜色,
            TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine,
            DpiScale(), _当前合成器.TextFormatCache, _当前合成器.BrushCache)
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

    Private Function 命中测试矩形顺序(dragRect As Rectangle) As List(Of Integer)
        ' 已被前缀和版本取代，保留占位以避免外部调用，但实际不会被调用。
        Return 命中测试矩形(dragRect)
    End Function

#End Region

#Region "拖动排序"

    Private Function 是否在拖选区域(mouseX As Integer) As Boolean
        If Not 允许多选 Then Return False
        If Not 允许拖动排序 Then Return True
        Dim contentRect = 获取内容区域()
        Dim inset = 获取边框内边距()
        Dim scrollW = If(Not _scrollBar.TrackRect.IsEmpty, Me.Width - inset - _scrollBar.VisualLeft, 0)
        Dim zoneLeft = contentRect.Right - scrollW - Dpi(拖选区域宽度)
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
        _selectedMin = -1
        For i = 0 To _displayRows.Count - 1
            If _displayRows(i).Type = DisplayRowType.Item AndAlso movedItems.Contains(_displayRows(i).Item) Then
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
        Dim scaledPadding As Padding = Dpi(项内边距)
        Dim cellX = colX + scaledPadding.Left + iconAreaW
        Dim cellW = colW - scaledPadding.Horizontal - iconAreaW
        If cellW <= 20 Then Return

        _editRowIndex = displayRowIndex
        _editColumnIndex = columnIndex
        Dim sub_ = item.SubItems(columnIndex)

        ' 让内嵌编辑框通过 V2 背景穿透采样宿主列表视图，使其底图自动匹配行的当前
        ' 选中/悬停/普通状态像素；BackColor1/BackColor 设为透明避免叠色"自照"。
        _editTextBox = New ModernTextBox With {
            .Text = sub_.Text,
            .Font = If(sub_.Font, Me.Font),
            .ForeColor = If(sub_.ForeColor <> Color.Empty, sub_.ForeColor, 项文本颜色),
            .BackColor1 = Color.Transparent,
            .BackColor = Color.Transparent,
            .BackgroundSource = CType(Me, Control),
            .BorderSize = 1,
            .BorderColor = 项高亮边框颜色,
            .BorderColorFocus = 项高亮边框颜色,
            .Padding = New Padding(2, 0, 2, 0),
            .Location = New Point(cellX, rowY),
            .Size = New Size(cellW, row.Height)
        }

        ' 让 list view 立刻重画一次，使穿透缓存中不再包含正在编辑的 cell 文字，
        ' 避免编辑框采样时拍到旧文字 → 与 DirectWrite 文字层叠加成重影。
        BackgroundPenetrationV2.Invalidate(Me)
        Me.Refresh()

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
        Return lo
    End Function

    Private Function 命中测试矩形(dragRect As Rectangle) As List(Of Integer)
        Dim result As New List(Of Integer)
        If _displayRows.Count = 0 OrElse _scrollOffset >= _displayRows.Count Then Return result
        Dim contentRect = 获取内容区域()
        Dim hasMoreAbove As Boolean = _scrollOffset > 0
        Dim baseY As Integer = contentRect.Y + If(hasMoreAbove, Dpi(更多指示器高度), 0) + 获取有效内容上边距()
        Dim bottomLimit As Integer = contentRect.Bottom - 获取有效内容下边距()
        Dim baseTop As Integer = _rowTops(_scrollOffset)
        Dim rowW As Integer = contentRect.Width
        If _columns.Count > 0 Then rowW = Math.Max(rowW, 获取总列宽())
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
        Dim scaledPadding As Padding = Dpi(项内边距)

        Dim bottomLinesH As Integer = 计算底部文本行高度(item, rowRect.Width)
        Dim upperPartH = rowRect.Height - scaledPadding.Vertical - bottomLinesH

        ' 检测子项区域
        Dim colCount = If(_columns.Count > 0, _columns.Count, 1)
        For colIdx = 0 To colCount - 1
            If colIdx >= item.SubItems.Count Then Exit For
            Dim colX = colXList(Math.Min(colIdx, colXList.Count - 1))
            Dim colW As Integer
            If _columns.Count > 0 Then colW = _columns(colIdx).Width Else colW = rowRect.Width
            If pt.X < colX OrElse pt.X >= colX + colW Then Continue For

            Dim sub_ = item.SubItems(colIdx)
            Dim cellW = colW - scaledPadding.Horizontal
            If colIdx = 0 Then cellW -= iconAreaW
            If cellW <= 0 Then Return ""

            Dim contentH = 计算子项内容高度(sub_, cellW)
            Dim cellTop = rowRect.Y + scaledPadding.Top
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
                lineY += Dpi(文本行间距)
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
            Dim blW = rowRect.Width - scaledPadding.Horizontal
            Dim blY = rowRect.Y + scaledPadding.Top + upperPartH + Dpi(底部文本行间距)
            Dim blProp As New Size(Math.Max(1, blW), Integer.MaxValue)
            For Each bl In item.BottomLines
                blY += Dpi(文本行间距)
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
        D2DHelperV2.InvalidateTextFormatCache(Me)
        全部项高度缓存失效()
        _moreSymbolFont?.Dispose()
        _moreSymbolFont = Nothing
        _moreSymbolFontKey = 0F
        _columnXDirty = True
        重建显示列表()
        D2DHelperV2.RefreshFontDependentRendering(Me)
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        全部项高度缓存失效()
        _moreSymbolFont?.Dispose()
        _moreSymbolFont = Nothing
        _moreSymbolFontKey = 0F
        _columnXDirty = True
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
