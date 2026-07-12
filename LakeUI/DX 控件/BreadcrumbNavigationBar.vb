Imports System.ComponentModel
Imports Vortice.Direct2D1

''' <summary>
''' Win11 文件资源管理器风格的面包屑导航控件。
''' </summary>
<DefaultEvent("ItemClicked")>
Public Class BreadcrumbNavigationBar
    Implements V3_IGpuRenderable, V3_IGpuInvalidationSource, V3_ISuperSamplingSource

#Region "节点定义与集合"
    ''' <summary>
    ''' 单个面包屑节点。
    ''' </summary>
    <TypeConverter(GetType(ExpandableObjectConverter))>
    Public Class BreadcrumbItem
        Private _owner As BreadcrumbNavigationBar
        Private _text As String = ""
        Private _toolTip As String = ""
        Private _hasDropDownExplicit As Boolean? = Nothing
        Private _tag As Object
        Private _image As Image
        Private _dropDownMenu As ModernContextMenu

        Public Sub New()
        End Sub

        Public Sub New(text As String)
            _text = If(text, "")
        End Sub

        Public Sub New(text As String, tag As Object)
            _text = If(text, "")
            _tag = tag
        End Sub

        <Browsable(False)>
        Friend Property Owner As BreadcrumbNavigationBar
            Get
                Return _owner
            End Get
            Set(value As BreadcrumbNavigationBar)
                _owner = value
            End Set
        End Property

        Private Sub InvalidateOwner()
            _owner?.请求V3渲染()
        End Sub

        <Category("LakeUI"), Description("节点文本"), DefaultValue(GetType(String), "")>
        Public Property Text As String
            Get
                Return _text
            End Get
            Set(value As String)
                Dim v As String = If(value, "")
                If _text = v Then Return
                _text = v
                InvalidateOwner()
            End Set
        End Property

        <Category("LakeUI"), Description("节点工具提示"), DefaultValue(GetType(String), "")>
        Public Property ToolTip As String
            Get
                Return _toolTip
            End Get
            Set(value As String)
                _toolTip = If(value, "")
            End Set
        End Property

        <Category("LakeUI"), Description("是否显示下拉箭头。未显式设置时根据 DropDownMenu 是否为空自动推断。")>
        Public Property HasDropDown As Boolean
            Get
                If _hasDropDownExplicit.HasValue Then Return _hasDropDownExplicit.Value
                Return _dropDownMenu IsNot Nothing
            End Get
            Set(value As Boolean)
                If _hasDropDownExplicit.HasValue AndAlso _hasDropDownExplicit.Value = value Then Return
                _hasDropDownExplicit = value
                InvalidateOwner()
            End Set
        End Property
        Private Function ShouldSerializeHasDropDown() As Boolean
            Return _hasDropDownExplicit.HasValue
        End Function
        Private Sub ResetHasDropDown()
            _hasDropDownExplicit = Nothing
            InvalidateOwner()
        End Sub

        <Category("LakeUI"), Description("节点显示在文本前的图标（可选）。"), DefaultValue(GetType(Image), Nothing)>
        Public Property Image As Image
            Get
                Return _image
            End Get
            Set(value As Image)
                If _image Is value Then Return
                _image = value
                InvalidateOwner()
            End Set
        End Property

        <Category("LakeUI"), Description("点击该节点箭头区域时显示的上下文菜单。"), DefaultValue(GetType(ModernContextMenu), Nothing)>
        Public Property DropDownMenu As ModernContextMenu
            Get
                Return _dropDownMenu
            End Get
            Set(value As ModernContextMenu)
                If _dropDownMenu Is value Then Return
                _dropDownMenu = value
                InvalidateOwner()
            End Set
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Tag As Object
            Get
                Return _tag
            End Get
            Set(value As Object)
                _tag = value
            End Set
        End Property

        Public Overrides Function ToString() As String
            If String.IsNullOrEmpty(_text) Then Return "(空)"
            Return _text
        End Function
    End Class

    Public Class BreadcrumbItemCollection
        Inherits ObjectModel.Collection(Of BreadcrumbItem)

        Private _owner As BreadcrumbNavigationBar
        Private _suspendCount As Integer = 0
        Private _dirty As Boolean = False

        Friend Sub New(owner As BreadcrumbNavigationBar)
            _owner = owner
        End Sub

        Friend Sub SetOwnerControl(owner As BreadcrumbNavigationBar)
            _owner = owner
            For Each it In Me
                If it IsNot Nothing Then it.Owner = owner
            Next
        End Sub

        ''' <summary>
        ''' 暂停 Items 变更引发的 Invalidate / 内部刷新。需配 EndUpdate 配对调用。
        ''' </summary>
        Public Sub BeginUpdate()
            _suspendCount += 1
        End Sub

        ''' <summary>
        ''' 恢复 Items 变更刷新。若 BeginUpdate 期间发生过变更，则触发一次刷新。
        ''' </summary>
        Public Sub EndUpdate()
            If _suspendCount > 0 Then _suspendCount -= 1
            If _suspendCount = 0 AndAlso _dirty Then
                _dirty = False
                _owner?.OnItemsChangedInternal()
            End If
        End Sub

        Private Sub NotifyChanged()
            If _suspendCount > 0 Then
                _dirty = True
                Return
            End If
            _owner?.OnItemsChangedInternal()
        End Sub

        Public Function IndexOfTag(tag As Object) As Integer
            For i As Integer = 0 To Count - 1
                Dim it As BreadcrumbItem = Me(i)
                If it Is Nothing Then Continue For
                If Object.Equals(it.Tag, tag) Then Return i
            Next
            Return -1
        End Function

        Public Function IndexOfText(text As String) As Integer
            Dim t As String = If(text, "")
            For i As Integer = 0 To Count - 1
                Dim it As BreadcrumbItem = Me(i)
                If it Is Nothing Then Continue For
                If String.Equals(it.Text, t, StringComparison.Ordinal) Then Return i
            Next
            Return -1
        End Function

        Public Overloads Sub AddRange(items As IEnumerable(Of BreadcrumbItem))
            BeginUpdate()
            Try
                For Each it In items
                    Add(it)
                Next
            Finally
                EndUpdate()
            End Try
        End Sub

        Public Overloads Function Add(text As String) As BreadcrumbItem
            Dim it As New BreadcrumbItem(text)
            Add(it)
            Return it
        End Function

        Public Overloads Function Add(text As String, tag As Object) As BreadcrumbItem
            Dim it As New BreadcrumbItem(text, tag)
            Add(it)
            Return it
        End Function

        Public Overloads Function Add(text As String, image As Image, tag As Object) As BreadcrumbItem
            Dim it As New BreadcrumbItem(text, tag) With {.Image = image}
            Add(it)
            Return it
        End Function

        Protected Overrides Sub InsertItem(index As Integer, item As BreadcrumbItem)
            If item IsNot Nothing Then item.Owner = _owner
            MyBase.InsertItem(index, item)
            NotifyChanged()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            Dim it As BreadcrumbItem = Me(index)
            If it IsNot Nothing Then it.Owner = Nothing
            MyBase.RemoveItem(index)
            NotifyChanged()
        End Sub

        Protected Overrides Sub ClearItems()
            For Each it In Me
                If it IsNot Nothing Then it.Owner = Nothing
            Next
            MyBase.ClearItems()
            NotifyChanged()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, item As BreadcrumbItem)
            If item IsNot Nothing Then item.Owner = _owner
            MyBase.SetItem(index, item)
            NotifyChanged()
        End Sub
    End Class
#End Region

#Region "事件参数"
    Public Class BreadcrumbItemEventArgs
        Inherits EventArgs

        Public ReadOnly Property Item As BreadcrumbItem
        Public ReadOnly Property Index As Integer

        Public Sub New(item As BreadcrumbItem, index As Integer)
            Me.Item = item
            Me.Index = index
        End Sub
    End Class

    ''' <summary>
    ''' 节点选中变更事件参数。
    ''' </summary>
    Public Class BreadcrumbSelectionChangedEventArgs
        Inherits EventArgs

        Public ReadOnly Property OldIndex As Integer
        Public ReadOnly Property NewIndex As Integer
        Public ReadOnly Property NewItem As BreadcrumbItem

        Public Sub New(oldIndex As Integer, newIndex As Integer, newItem As BreadcrumbItem)
            Me.OldIndex = oldIndex
            Me.NewIndex = newIndex
            Me.NewItem = newItem
        End Sub
    End Class
#End Region

#Region "事件"
    ''' <summary>用户点击了某个面包屑节点文本区域。</summary>
    Public Event ItemClicked As EventHandler(Of BreadcrumbItemEventArgs)
    ''' <summary>SelectedIndex 变更后触发。</summary>
    Public Event SelectedIndexChanged As EventHandler(Of BreadcrumbSelectionChangedEventArgs)
#End Region

#Region "字段"
    Private _items As BreadcrumbItemCollection
    Private _hoverNodeIndex As Integer = -2
    Private _hoverIsArrow As Boolean = False
    Private _pressedNodeIndex As Integer = -2
    Private _pressedIsArrow As Boolean = False
    Private _activeDropDownMenu As ModernContextMenu = Nothing
    Private _dropDownArrowIndex As Integer = -2

    ' 选中节点索引
    Private _selectedIndex As Integer = -1

    ' ToolTip 组件
    Private _toolTip As ToolTip
    Private _lastToolTipIndex As Integer = -2  ' -2 表示尚未设置过

    ' 溢出折叠：当布局总宽超过控件宽度时，把左侧若干节点折叠到一个 "..." 根。
    ' _overflowStartIndex < 0 表示没有折叠；否则 [0, _overflowStartIndex) 的节点被折叠。
    Private _overflowStartIndex As Integer = -1
    Private ReadOnly _overflowItem As New BreadcrumbItem("…")

    Private Class NodeLayout
        Public Index As Integer            ' -1 表示溢出折叠根
        Public TextRect As Rectangle
        Public ArrowRect As Rectangle
        Public IconRect As Rectangle
        Public HasArrow As Boolean
        Public HasIcon As Boolean
        Public IsOverflow As Boolean
    End Class
    Private _layoutCache As New List(Of NodeLayout)
#End Region

#Region "属性"
    <DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
     Editor("System.ComponentModel.Design.CollectionEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing"),
     Category("LakeUI"), Description("面包屑节点集合"), Browsable(True)>
    Public ReadOnly Property Items As BreadcrumbItemCollection
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
            Dim v As Integer = value
            If v < -1 Then v = -1
            If v >= _items.Count Then v = _items.Count - 1
            If v = _selectedIndex Then Return
            Dim oldIdx As Integer = _selectedIndex
            _selectedIndex = v
            Dim newItem As BreadcrumbItem = If(v >= 0 AndAlso v < _items.Count, _items(v), Nothing)
            请求V3渲染()
            RaiseEvent SelectedIndexChanged(Me, New BreadcrumbSelectionChangedEventArgs(oldIdx, v, newItem))
        End Set
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property SelectedItem As BreadcrumbItem
        Get
            If _selectedIndex < 0 OrElse _selectedIndex >= _items.Count Then Return Nothing
            Return _items(_selectedIndex)
        End Get
    End Property

    Private 选中节点背景 As Color = Color.FromArgb(70, 70, 70)
    <Category("LakeUI"), Description("当前选中节点的高亮背景颜色"), DefaultValue(GetType(Color), "70,70,70"), Browsable(True)>
    Public Property SelectedNodeBackColor As Color
        Get
            Return 选中节点背景
        End Get
        Set(value As Color)
            SetValue(选中节点背景, value)
        End Set
    End Property

    Private 选中节点文本颜色 As Color = Color.White
    <Category("LakeUI"), Description("当前选中节点的文本颜色"), DefaultValue(GetType(Color), "White"), Browsable(True)>
    Public Property SelectedNodeForeColor As Color
        Get
            Return 选中节点文本颜色
        End Get
        Set(value As Color)
            SetValue(选中节点文本颜色, value)
        End Set
    End Property

    Private 启用溢出折叠 As Boolean = True
    <Category("LakeUI"), Description("总宽度超出控件时是否将左侧节点折叠为省略号根（点击展开为下拉）。"), DefaultValue(True), Browsable(True)>
    Public Property AutoCollapseOverflow As Boolean
        Get
            Return 启用溢出折叠
        End Get
        Set(value As Boolean)
            SetValue(启用溢出折叠, value)
        End Set
    End Property

    Private 溢出根文本 As String = "…"
    <Category("LakeUI"), Description("溢出折叠根显示的文本"), DefaultValue("…"), Browsable(True)>
    Public Property OverflowRootText As String
        Get
            Return 溢出根文本
        End Get
        Set(value As String)
            Dim v As String = If(value, "")
            If 溢出根文本 = v Then Return
            溢出根文本 = v
            _overflowItem.Text = v
            请求V3渲染()
        End Set
    End Property

    Private 图标大小 As Integer = 16
    <Category("LakeUI"), Description("节点图标尺寸（DIP，会乘 DPI）"), DefaultValue(16), Browsable(True)>
    Public Property NodeIconSize As Integer
        Get
            Return 图标大小
        End Get
        Set(value As Integer)
            SetValue(图标大小, Math.Max(4, value))
        End Set
    End Property

    Private 图标与文本间距 As Integer = 4
    <Category("LakeUI"), Description("节点图标与文本之间的间距"), DefaultValue(4), Browsable(True)>
    Public Property NodeIconTextSpacing As Integer
        Get
            Return 图标与文本间距
        End Get
        Set(value As Integer)
            SetValue(图标与文本间距, Math.Max(0, value))
        End Set
    End Property

    Private 焦点框颜色 As Color = Color.FromArgb(120, 170, 255)
    <Category("LakeUI"), Description("焦点框颜色"), DefaultValue(GetType(Color), "120,170,255"), Browsable(True)>
    Public Property FocusCueColor As Color
        Get
            Return 焦点框颜色
        End Get
        Set(value As Color)
            SetValue(焦点框颜色, value)
        End Set
    End Property

    ' 背景颜色：与 ModernButton 一致，直接使用 MyBase.BackColor。
    ' • 不透明（A=255） → 由基类 OnPaintBackground 填底；
    ' • 半透明（0<A<255） → 基类先合成父级背景，再在 D2D 背景层叠加 BackColor 作为遮罩；
    ' • A=0 → 完全透明，仅依赖 BackgroundSource 或父级。

    Private 文本颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("节点文本颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Overrides Property ForeColor As Color
        Get
            Return 文本颜色
        End Get
        Set(value As Color)
            SetValue(文本颜色, value)
        End Set
    End Property

    Private 节点悬停背景 As Color = Color.FromArgb(60, 60, 60)
    <Category("LakeUI"), Description("节点鼠标悬停背景颜色"), DefaultValue(GetType(Color), "60,60,60"), Browsable(True)>
    Public Property NodeHoverBackColor As Color
        Get
            Return 节点悬停背景
        End Get
        Set(value As Color)
            SetValue(节点悬停背景, value)
        End Set
    End Property

    Private 节点按下背景 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("节点鼠标按下背景颜色"), DefaultValue(GetType(Color), "80,80,80"), Browsable(True)>
    Public Property NodePressedBackColor As Color
        Get
            Return 节点按下背景
        End Get
        Set(value As Color)
            SetValue(节点按下背景, value)
        End Set
    End Property

    Private 节点圆角半径 As Integer = 4
    <Category("LakeUI"), Description("节点高亮区域圆角半径"), DefaultValue(4), Browsable(True)>
    Public Property NodeCornerRadius As Integer
        Get
            Return 节点圆角半径
        End Get
        Set(value As Integer)
            SetValue(节点圆角半径, Math.Max(0, value))
        End Set
    End Property

    Private 节点文本左右内边距 As Integer = 8
    <Category("LakeUI"), Description("节点文本左右内边距"), DefaultValue(8), Browsable(True)>
    Public Property NodeTextPadding As Integer
        Get
            Return 节点文本左右内边距
        End Get
        Set(value As Integer)
            SetValue(节点文本左右内边距, Math.Max(0, value))
        End Set
    End Property

    Private 节点垂直内边距 As Integer = 4
    <Category("LakeUI"), Description("节点上下内边距"), DefaultValue(4), Browsable(True)>
    Public Property NodeVerticalPadding As Integer
        Get
            Return 节点垂直内边距
        End Get
        Set(value As Integer)
            SetValue(节点垂直内边距, Math.Max(0, value))
        End Set
    End Property

    Private 箭头区域宽度 As Integer = 22
    <Category("LakeUI"), Description("分隔/展开箭头区域宽度"), DefaultValue(22), Browsable(True)>
    Public Property ArrowAreaWidth As Integer
        Get
            Return 箭头区域宽度
        End Get
        Set(value As Integer)
            SetValue(箭头区域宽度, Math.Max(8, value))
        End Set
    End Property

    Private 箭头大小 As Integer = 8
    <Category("LakeUI"), Description("箭头图形大小"), DefaultValue(8), Browsable(True)>
    Public Property ArrowSize As Integer
        Get
            Return 箭头大小
        End Get
        Set(value As Integer)
            SetValue(箭头大小, Math.Max(4, value))
        End Set
    End Property

    Private 箭头线宽 As Single = 1.4F
    <Category("LakeUI"), Description("箭头线条宽度"), DefaultValue(1.4F), Browsable(True)>
    Public Property ArrowStrokeWidth As Single
        Get
            Return 箭头线宽
        End Get
        Set(value As Single)
            SetValue(箭头线宽, Math.Max(0.5F, value))
        End Set
    End Property

    Private 箭头颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("箭头颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property ArrowColor As Color
        Get
            Return 箭头颜色
        End Get
        Set(value As Color)
            SetValue(箭头颜色, value)
        End Set
    End Property

    Private 箭头悬停颜色 As Color = Color.White
    <Category("LakeUI"), Description("箭头鼠标悬停颜色"), DefaultValue(GetType(Color), "White"), Browsable(True)>
    Public Property ArrowHoverColor As Color
        Get
            Return 箭头悬停颜色
        End Get
        Set(value As Color)
            SetValue(箭头悬停颜色, value)
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

    Private 超采样倍率 As GlobalOptions.SuperSamplingScaleEnum = GlobalOptions.SuperSamplingScaleEnum.OFF
    <Category("LakeUI"), Description(GlobalOptions.超采样抗锯齿描述词), DefaultValue(GetType(GlobalOptions.SuperSamplingScaleEnum), "OFF"), Browsable(True)>
    Public Property SuperSamplingScale As GlobalOptions.SuperSamplingScaleEnum Implements V3_ISuperSamplingSource.SuperSamplingScale
        Get
            Return 超采样倍率
        End Get
        Set(value As GlobalOptions.SuperSamplingScaleEnum)
            SetValue(超采样倍率, value)
        End Set
    End Property

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

#Region "初始化"
    Public Sub New()
        InitializeComponent()
        _items = New BreadcrumbItemCollection(Me)
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.SupportsTransparentBackColor Or
                 ControlStyles.ResizeRedraw, True)
        UpdateStyles()
        TabStop = False
        MyBase.BackColor = Color.FromArgb(36, 36, 36)
    End Sub

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        ' 句柄销毁时不能再走动画路径，避免 ObjectDisposed
        ImmediatelyDisposeDropDown()
        MyBase.OnHandleDestroyed(e)
    End Sub

    Friend Sub DisposeManagedResources()
        ImmediatelyDisposeDropDown()
        If _toolTip IsNot Nothing Then
            Try : _toolTip.Dispose() : Catch : End Try
            _toolTip = Nothing
        End If
    End Sub

    Friend Sub OnItemsChangedInternal()
        _hoverNodeIndex = -2
        _pressedNodeIndex = -2
        If _selectedIndex >= _items.Count Then _selectedIndex = -1
        If _activeDropDownMenu IsNot Nothing Then CloseDropDown()
        请求V3渲染()
    End Sub
#End Region

#Region "布局"
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

    Private Sub RebuildLayout()
        _layoutCache.Clear()
        _overflowStartIndex = -1
        Dim s As Single = DpiScale()
        Dim padX As Integer = CInt(节点文本左右内边距 * s)
        Dim padY As Integer = CInt(节点垂直内边距 * s)
        Dim aaw As Integer = CInt(箭头区域宽度 * s)
        Dim iconSize As Integer = CInt(图标大小 * s)
        Dim iconSpacing As Integer = CInt(图标与文本间距 * s)
        Dim contentLeft As Integer = Me.Padding.Left
        Dim contentTop As Integer = Me.Padding.Top
        Dim contentHeight As Integer = Math.Max(0, ClientRectangle.Height - Me.Padding.Vertical)
        Dim itemY As Integer = contentTop + padY
        Dim itemH As Integer = Math.Max(0, contentHeight - padY * 2)
        Dim availableRight As Integer = ClientRectangle.Width - Me.Padding.Right

        ' 第一遍：先正常排，得到每个节点的宽度。
        Dim widths As New List(Of Integer)(_items.Count)
        Dim hasIcons As New List(Of Boolean)(_items.Count)
        For i As Integer = 0 To _items.Count - 1
            Dim item As BreadcrumbItem = _items(i)
            If item Is Nothing Then
                widths.Add(0)
                hasIcons.Add(False)
                Continue For
            End If
            Dim showIcon As Boolean = item.Image IsNot Nothing
            Dim textW As Integer = D3D_TextMeasureHelper.MeasureTextWidth(item.Text, Font, contentHeight)
            Dim nodeTextW As Integer = textW + padX * 2
            If showIcon Then nodeTextW += iconSize + iconSpacing
            Dim total As Integer = nodeTextW
            If item.HasDropDown Then total += aaw
            widths.Add(nodeTextW)
            hasIcons.Add(showIcon)
        Next

        Dim totalWidth As Integer = contentLeft
        For i As Integer = 0 To _items.Count - 1
            totalWidth += widths(i)
            Dim it As BreadcrumbItem = _items(i)
            If it IsNot Nothing AndAlso it.HasDropDown Then totalWidth += aaw
        Next

        Dim overflowStart As Integer = -1
        If 启用溢出折叠 AndAlso totalWidth > availableRight AndAlso _items.Count > 1 Then
            ' 始终保留最后一个节点（叶子）；从左侧开始折叠，直到能放下。
            ' 折叠根本身需要的宽度 = padX*2 + textW("…") + aaw（折叠根总有箭头）。
            Dim overflowTextW As Integer = D3D_TextMeasureHelper.MeasureTextWidth(溢出根文本, Font, contentHeight)
            Dim overflowNodeW As Integer = overflowTextW + padX * 2 + aaw

            For cut As Integer = 1 To _items.Count - 1
                Dim used As Integer = contentLeft + overflowNodeW
                For j As Integer = cut To _items.Count - 1
                    used += widths(j)
                    Dim it As BreadcrumbItem = _items(j)
                    If it IsNot Nothing AndAlso it.HasDropDown Then used += aaw
                Next
                If used <= availableRight Then
                    overflowStart = cut
                    Exit For
                End If
            Next
            ' 即使全都折叠也放不下，至少保留最后一个节点。
            If overflowStart < 0 Then overflowStart = _items.Count - 1
        End If
        _overflowStartIndex = overflowStart

        Dim x As Integer = contentLeft

        ' 添加溢出折叠根
        If overflowStart > 0 Then
            Dim overflowTextW As Integer = D3D_TextMeasureHelper.MeasureTextWidth(溢出根文本, Font, contentHeight)
            Dim oNodeW As Integer = overflowTextW + padX * 2
            Dim oLayout As New NodeLayout With {
                .Index = -1,
                .IsOverflow = True,
                .HasArrow = True,
                .HasIcon = False,
                .TextRect = New Rectangle(x, itemY, oNodeW, itemH)
            }
            x += oNodeW
            oLayout.ArrowRect = New Rectangle(x, itemY, aaw, itemH)
            x += aaw
            _layoutCache.Add(oLayout)
        End If

        Dim startIdx As Integer = If(overflowStart > 0, overflowStart, 0)
        For i As Integer = startIdx To _items.Count - 1
            Dim item As BreadcrumbItem = _items(i)
            If item Is Nothing Then Continue For
            Dim showIcon As Boolean = hasIcons(i)
            Dim layout As New NodeLayout With {
                .Index = i,
                .IsOverflow = False,
                .HasArrow = item.HasDropDown,
                .HasIcon = showIcon
            }
            Dim nodeW As Integer = widths(i)
            layout.TextRect = New Rectangle(x, itemY, nodeW, itemH)
            If showIcon Then
                Dim iconY As Integer = itemY + Math.Max(0, (itemH - iconSize) \ 2)
                layout.IconRect = New Rectangle(x + padX, iconY, iconSize, iconSize)
            End If
            x += nodeW
            If item.HasDropDown Then
                layout.ArrowRect = New Rectangle(x, itemY, aaw, itemH)
                x += aaw
            Else
                layout.ArrowRect = Rectangle.Empty
            End If
            _layoutCache.Add(layout)
        Next
    End Sub

    Private Function HitTest(p As Point, ByRef nodeIndex As Integer, ByRef isArrow As Boolean, ByRef isOverflow As Boolean) As Boolean
        nodeIndex = -1
        isArrow = False
        isOverflow = False
        For Each l In _layoutCache
            If l.TextRect.Contains(p) Then
                nodeIndex = l.Index
                isArrow = False
                isOverflow = l.IsOverflow
                Return True
            End If
            If l.HasArrow AndAlso l.ArrowRect.Contains(p) Then
                nodeIndex = l.Index
                isArrow = True
                isOverflow = l.IsOverflow
                Return True
            End If
        Next
        Return False
    End Function

    ' 兼容旧签名（不暴露 IsOverflow）
    Private Function HitTest(p As Point, ByRef nodeIndex As Integer, ByRef isArrow As Boolean) As Boolean
        Dim ov As Boolean
        Return HitTest(p, nodeIndex, isArrow, ov)
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
        RebuildLayout()
        Dim w As Integer = ClientRectangle.Width
        Dim h As Integer = ClientRectangle.Height
        If w < 1 OrElse h < 1 Then Return

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(Me, _backgroundSource, New RectangleF(0, 0, w, h))
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, w, h), MyBase.BackColor)
        End If

        DrawNodesShapes_GPU(context)
        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, w, h), 禁用时遮罩颜色)
        End If
        DrawNodesText_GPU(context)
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Private Sub DrawNodesShapes_GPU(context As D3D_PaintContext)
        Dim s As Single = DpiScale()
        Dim radius As Single = 节点圆角半径 * s

        For Each l In _layoutCache
            Dim isSelected As Boolean = (Not l.IsOverflow) AndAlso (l.Index = _selectedIndex) AndAlso l.Index >= 0

            Dim textHover As Boolean = (_hoverNodeIndex = l.Index AndAlso Not _hoverIsArrow)
            Dim textPressed As Boolean = (_pressedNodeIndex = l.Index AndAlso Not _pressedIsArrow)
            Dim textHl As Color = Color.Empty
            If textPressed Then
                textHl = 节点按下背景
            ElseIf textHover Then
                textHl = 节点悬停背景
            ElseIf isSelected Then
                textHl = 选中节点背景
            End If
            If textHl <> Color.Empty AndAlso textHl.A > 0 Then FillRoundedRect_GPU(context, l.TextRect, radius, textHl)

            If l.HasArrow Then
                Dim arrowHover As Boolean = (_hoverNodeIndex = l.Index AndAlso _hoverIsArrow)
                Dim arrowPressed As Boolean = (_pressedNodeIndex = l.Index AndAlso _pressedIsArrow) OrElse (_dropDownArrowIndex = l.Index)
                Dim arrHl As Color = Color.Empty
                If arrowPressed Then
                    arrHl = 节点按下背景
                ElseIf arrowHover Then
                    arrHl = 节点悬停背景
                End If
                If arrHl <> Color.Empty AndAlso arrHl.A > 0 Then FillRoundedRect_GPU(context, l.ArrowRect, radius, arrHl)

                Dim arrowClr As Color = 箭头颜色
                If (_hoverNodeIndex = l.Index AndAlso _hoverIsArrow) OrElse (_dropDownArrowIndex = l.Index) Then arrowClr = 箭头悬停颜色
                DrawChevron_GPU(context, l.ArrowRect, arrowClr, _dropDownArrowIndex = l.Index)
            End If
        Next
    End Sub

    Private Sub DrawNodesText_GPU(context As D3D_PaintContext)
        Dim s As Single = DpiScale()
        Dim padX As Integer = CInt(节点文本左右内边距 * s)
        Dim iconSize As Integer = CInt(图标大小 * s)
        Dim iconSpacing As Integer = CInt(图标与文本间距 * s)
        For Each l In _layoutCache
            Dim item As BreadcrumbItem
            If l.IsOverflow Then
                item = _overflowItem
            Else
                If l.Index < 0 OrElse l.Index >= _items.Count Then Continue For
                item = _items(l.Index)
            End If
            If item Is Nothing Then Continue For

            Dim isSelected As Boolean = (Not l.IsOverflow) AndAlso (l.Index = _selectedIndex)
            Dim textColor As Color = If(isSelected, 选中节点文本颜色, ForeColor)

            If l.HasIcon AndAlso item.Image IsNot Nothing Then
                context.DrawImage(item.Image, New RectangleF(l.IconRect.X, l.IconRect.Y, l.IconRect.Width, l.IconRect.Height))
            End If

            Dim textRect As Rectangle = l.TextRect
            Dim align As Vortice.DirectWrite.TextAlignment
            If l.HasIcon Then
                Dim shift As Integer = iconSize + iconSpacing
                textRect = New Rectangle(textRect.X + padX + shift, textRect.Y, textRect.Width - padX - shift, textRect.Height)
                align = Vortice.DirectWrite.TextAlignment.Leading
            Else
                align = Vortice.DirectWrite.TextAlignment.Center
            End If
            If textRect.Width > 0 AndAlso textRect.Height > 0 Then
                context.DrawText(item.Text, Font, textColor, New RectangleF(textRect.X, textRect.Y, textRect.Width, textRect.Height), align, Vortice.DirectWrite.ParagraphAlignment.Center)
            End If
        Next
    End Sub

    Private Sub FillRoundedRect_GPU(context As D3D_PaintContext, r As Rectangle, radius As Single, c As Color)
        If r.Width <= 0 OrElse r.Height <= 0 OrElse c.A = 0 Then Return
        Dim rect As New RectangleF(r.X, r.Y, r.Width, r.Height)
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, c, context.DeviceGeneration)
        If radius <= 0.5F Then
            context.DeviceContext.FillRectangle(D3D_PaintContext.ToRawRect(rect), brush)
            Return
        End If
        context.FillRoundedRectangle(rect, radius, brush)
    End Sub

    Private Sub DrawChevron_GPU(context As D3D_PaintContext, area As Rectangle, c As Color, pointDown As Boolean)
        If c.A = 0 Then Return
        Dim s As Single = DpiScale()
        Dim sz As Single = 箭头大小 * s
        Dim cx As Single = area.X + area.Width / 2.0F
        Dim cy As Single = area.Y + area.Height / 2.0F
        Dim half As Single = sz / 2.0F
        Dim hh As Single = half * 0.55F
        Dim p1, p2, p3 As System.Numerics.Vector2
        If pointDown Then
            p1 = New System.Numerics.Vector2(cx - half, cy - hh)
            p2 = New System.Numerics.Vector2(cx, cy + hh)
            p3 = New System.Numerics.Vector2(cx + half, cy - hh)
        Else
            p1 = New System.Numerics.Vector2(cx - hh, cy - half)
            p2 = New System.Numerics.Vector2(cx + hh, cy)
            p3 = New System.Numerics.Vector2(cx - hh, cy + half)
        End If
        Dim path = D3D_RenderCore.DeviceManager.D2DFactory.CreatePathGeometry()
        Try
            Using sink = path.Open()
                sink.BeginFigure(p1, FigureBegin.Hollow)
                sink.AddLine(p2)
                sink.AddLine(p3)
                sink.EndFigure(FigureEnd.Open)
                sink.Close()
            End Using
            Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, c, context.DeviceGeneration)
            Using strokeStyle = 创建圆头描边样式_GPU()
                context.DeviceContext.DrawGeometry(path, brush, 箭头线宽 * s, strokeStyle)
            End Using
        Finally
            path.Dispose()
        End Try
    End Sub

    Private Shared Function 创建圆头描边样式_GPU() As ID2D1StrokeStyle
        Return D3D_RenderCore.DeviceManager.D2DFactory.CreateStrokeStyle(
            New StrokeStyleProperties With {
                .StartCap = CapStyle.Round,
                .EndCap = CapStyle.Round,
                .DashCap = CapStyle.Round,
                .LineJoin = LineJoin.Round,
                .DashStyle = DashStyle.Solid,
                .MiterLimit = 10.0F
            })
    End Function

#End Region

#Region "鼠标处理"
    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim idx As Integer
        Dim isArrow As Boolean
        Dim isOverflow As Boolean
        Dim hit As Boolean = HitTest(e.Location, idx, isArrow, isOverflow)
        Dim newHoverIdx As Integer = If(hit, idx, -2)
        If newHoverIdx <> _hoverNodeIndex OrElse isArrow <> _hoverIsArrow Then
            _hoverNodeIndex = newHoverIdx
            _hoverIsArrow = isArrow
            Cursor = If(hit, Cursors.Hand, Cursors.Default)
            请求V3渲染()
        End If

        ' 更新 ToolTip
        UpdateToolTipForHit(hit, idx, isOverflow)
    End Sub

    Private Sub UpdateToolTipForHit(hit As Boolean, idx As Integer, isOverflow As Boolean)
        If _toolTip Is Nothing Then
            _toolTip = New ToolTip()
        End If
        Dim newTipIndex As Integer = If(hit AndAlso Not isOverflow, idx, -1)
        If newTipIndex = _lastToolTipIndex Then Return
        _lastToolTipIndex = newTipIndex
        If newTipIndex >= 0 AndAlso newTipIndex < _items.Count Then
            Dim it = _items(newTipIndex)
            If it IsNot Nothing AndAlso Not String.IsNullOrEmpty(it.ToolTip) Then
                _toolTip.SetToolTip(Me, it.ToolTip)
                Return
            End If
        End If
        _toolTip.SetToolTip(Me, String.Empty)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If _hoverNodeIndex <> -2 Then
            _hoverNodeIndex = -2
            _hoverIsArrow = False
            Cursor = Cursors.Default
            请求V3渲染()
        End If
        If _toolTip IsNot Nothing Then
            _toolTip.SetToolTip(Me, String.Empty)
            _lastToolTipIndex = -2
        End If
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not Enabled OrElse e.Button <> MouseButtons.Left Then Return
        Dim idx As Integer
        Dim isArrow As Boolean
        Dim isOverflow As Boolean
        If HitTest(e.Location, idx, isArrow, isOverflow) Then
            _pressedNodeIndex = idx
            _pressedIsArrow = isArrow
            请求V3渲染()
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If e.Button <> MouseButtons.Left Then Return
        Dim idx As Integer
        Dim isArrow As Boolean
        Dim isOverflow As Boolean
        Dim hit As Boolean = HitTest(e.Location, idx, isArrow, isOverflow)
        Dim wasPressedIdx As Integer = _pressedNodeIndex
        Dim wasPressedArrow As Boolean = _pressedIsArrow
        _pressedNodeIndex = -2
        _pressedIsArrow = False
        请求V3渲染()
        If hit AndAlso idx = wasPressedIdx AndAlso isArrow = wasPressedArrow Then
            If isOverflow Then
                ' 溢出根：无论点的是文本还是箭头，都展开折叠列表
                ToggleDropDown(-1)
            ElseIf isArrow Then
                ToggleDropDown(idx)
            Else
                Dim it = _items(idx)
                SelectedIndex = idx
                RaiseEvent ItemClicked(Me, New BreadcrumbItemEventArgs(it, idx))
            End If
        End If
    End Sub
#End Region

#Region "尺寸"
    Protected Overrides ReadOnly Property DefaultSize As Size
        Get
            Return New Size(300, 28)
        End Get
    End Property

    Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
        Dim s As Single = DpiScale()
        Dim padY As Integer = CInt(节点垂直内边距 * s)
        Dim h As Integer = Font.Height + padY * 2 + Me.Padding.Vertical
        Return New Size(If(proposedSize.Width > 0, proposedSize.Width, MyBase.Width), h)
    End Function
#End Region

#Region "下拉控制"
    Private Sub ToggleDropDown(arrowIndex As Integer)
        If _dropDownArrowIndex = arrowIndex Then
            CloseDropDown()
            Return
        End If
        OpenDropDown(arrowIndex)
    End Sub

    Private Sub OpenDropDown(arrowIndex As Integer)
        ' arrowIndex == -1 表示溢出折叠根
        Dim isOverflow As Boolean = (arrowIndex = -1)
        If Not isOverflow Then
            If arrowIndex < 0 OrElse arrowIndex >= _items.Count Then Return
        End If
        Dim menu As ModernContextMenu = GetDropDownMenu(arrowIndex)
        If menu Is Nothing Then Return

        ImmediatelyDisposeDropDown()

        _dropDownArrowIndex = arrowIndex
        _activeDropDownMenu = menu
        RemoveHandler _activeDropDownMenu.MenuClosed, AddressOf DropDownMenu_MenuClosed
        AddHandler _activeDropDownMenu.MenuClosed, AddressOf DropDownMenu_MenuClosed

        Dim anchorRect As Rectangle = GetDropDownAnchorRect(arrowIndex)
        Dim pt As Point = PointToScreen(New Point(anchorRect.Left, Height))
        _activeDropDownMenu.Show(pt.X, pt.Y)
        请求V3渲染()
    End Sub

    Private Function GetDropDownMenu(arrowIndex As Integer) As ModernContextMenu
        If arrowIndex >= 0 AndAlso arrowIndex < _items.Count Then
            Dim item = _items(arrowIndex)
            If item IsNot Nothing Then Return item.DropDownMenu
        End If
        Return Nothing
    End Function

    Private Function GetDropDownAnchorRect(parentIndex As Integer) As Rectangle
        For Each l In _layoutCache
            If parentIndex = -1 Then
                If l.IsOverflow AndAlso l.HasArrow Then Return l.ArrowRect
            ElseIf l.Index = parentIndex AndAlso l.HasArrow Then
                Return l.ArrowRect
            End If
        Next
        Return New Rectangle(0, 0, Width, Height)
    End Function

    Friend Sub CloseDropDown()
        If _activeDropDownMenu Is Nothing OrElse _dropDownArrowIndex = -2 Then Return
        _activeDropDownMenu.Close()
    End Sub

    Private Sub ImmediatelyDisposeDropDown()
        If _activeDropDownMenu Is Nothing OrElse _dropDownArrowIndex = -2 Then Return
        Dim menu = _activeDropDownMenu
        RemoveHandler menu.MenuClosed, AddressOf DropDownMenu_MenuClosed
        _activeDropDownMenu = Nothing
        _dropDownArrowIndex = -2
        Try
            menu.Close()
        Catch
        End Try
        请求V3渲染()
    End Sub

    Private Sub DropDownMenu_MenuClosed(sender As Object, e As EventArgs)
        Dim menu = TryCast(sender, ModernContextMenu)
        If menu IsNot Nothing Then RemoveHandler menu.MenuClosed, AddressOf DropDownMenu_MenuClosed
        _activeDropDownMenu = Nothing
        _dropDownArrowIndex = -2
        请求V3渲染()
    End Sub
#End Region

#Region "辅助"
    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            请求V3渲染()
        End If
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        If _activeDropDownMenu IsNot Nothing Then CloseDropDown()
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        If Not Enabled AndAlso _activeDropDownMenu IsNot Nothing Then CloseDropDown()
        请求V3渲染()
    End Sub
#End Region

End Class
