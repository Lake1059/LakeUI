Imports System.ComponentModel
Imports System.Drawing.Drawing2D

''' <summary>
''' Win11 文件资源管理器风格的面包屑导航控件。
''' </summary>
<DefaultEvent("ItemClicked")>
Public Class BreadcrumbNavigationBar

#Region "节点定义与集合"
    ''' <summary>
    ''' 单个面包屑节点。
    ''' </summary>
    <TypeConverter(GetType(ExpandableObjectConverter))>
    Public Class BreadcrumbItem
        Friend Owner As BreadcrumbNavigationBar
        Private _text As String = ""
        Private _toolTip As String = ""
        Private _hasChildren As Boolean = True
        Private _tag As Object

        Public Sub New()
        End Sub

        Public Sub New(text As String)
            _text = If(text, "")
        End Sub

        Public Sub New(text As String, tag As Object)
            _text = If(text, "")
            _tag = tag
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
                Owner?.Invalidate()
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

        <Category("LakeUI"), Description("是否含有子项（决定是否显示展开箭头）"), DefaultValue(True)>
        Public Property HasChildren As Boolean
            Get
                Return _hasChildren
            End Get
            Set(value As Boolean)
                If _hasChildren = value Then Return
                _hasChildren = value
                Owner?.Invalidate()
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

        Private ReadOnly _owner As BreadcrumbNavigationBar

        Friend Sub New(owner As BreadcrumbNavigationBar)
            _owner = owner
        End Sub

        Public Overloads Sub AddRange(items As IEnumerable(Of BreadcrumbItem))
            For Each it In items
                Add(it)
            Next
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

        Protected Overrides Sub InsertItem(index As Integer, item As BreadcrumbItem)
            If item IsNot Nothing Then item.Owner = _owner
            MyBase.InsertItem(index, item)
            _owner.OnItemsChangedInternal()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            Dim it As BreadcrumbItem = Me(index)
            If it IsNot Nothing Then it.Owner = Nothing
            MyBase.RemoveItem(index)
            _owner.OnItemsChangedInternal()
        End Sub

        Protected Overrides Sub ClearItems()
            For Each it In Me
                If it IsNot Nothing Then it.Owner = Nothing
            Next
            MyBase.ClearItems()
            _owner.OnItemsChangedInternal()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, item As BreadcrumbItem)
            If item IsNot Nothing Then item.Owner = _owner
            MyBase.SetItem(index, item)
            _owner.OnItemsChangedInternal()
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
    ''' 子项请求事件参数：处理程序通过 <see cref="Items"/> 填充子项。
    ''' </summary>
    Public Class BreadcrumbDropDownOpeningEventArgs
        Inherits EventArgs

        Public ReadOnly Property ParentItem As BreadcrumbItem
        Public ReadOnly Property ParentIndex As Integer
        ''' <summary>由处理程序填充的子项集合（运行时绑定）。</summary>
        Public ReadOnly Property Items As New List(Of BreadcrumbItem)
        Public Property Cancel As Boolean

        Public Sub New(parent As BreadcrumbItem, parentIndex As Integer)
            ParentItem = parent
            Me.ParentIndex = parentIndex
        End Sub
    End Class

    Public Class BreadcrumbDropDownItemClickedEventArgs
        Inherits EventArgs

        Public ReadOnly Property ParentItem As BreadcrumbItem
        Public ReadOnly Property ParentIndex As Integer
        Public ReadOnly Property ChildItem As BreadcrumbItem
        Public ReadOnly Property ChildIndex As Integer

        Public Sub New(parent As BreadcrumbItem, parentIndex As Integer, child As BreadcrumbItem, childIndex As Integer)
            Me.ParentItem = parent
            Me.ParentIndex = parentIndex
            Me.ChildItem = child
            Me.ChildIndex = childIndex
        End Sub
    End Class
#End Region

#Region "事件"
    ''' <summary>用户点击了某个面包屑节点文本区域。</summary>
    Public Event ItemClicked As EventHandler(Of BreadcrumbItemEventArgs)
    ''' <summary>下拉箭头展开前触发；处理程序应在此填充 e.Items。</summary>
    Public Event DropDownOpening As EventHandler(Of BreadcrumbDropDownOpeningEventArgs)
    ''' <summary>下拉已展开。</summary>
    Public Event DropDownOpened As EventHandler(Of BreadcrumbItemEventArgs)
    ''' <summary>下拉已关闭。</summary>
    Public Event DropDownClosed As EventHandler(Of BreadcrumbItemEventArgs)
    ''' <summary>用户在下拉中点击了某个子项。</summary>
    Public Event DropDownItemClicked As EventHandler(Of BreadcrumbDropDownItemClickedEventArgs)
#End Region

#Region "字段"
    Private _items As BreadcrumbItemCollection
    Private _hoverNodeIndex As Integer = -1
    Private _hoverIsArrow As Boolean = False
    Private _pressedNodeIndex As Integer = -1
    Private _pressedIsArrow As Boolean = False
    Private _dropDownForm As DropDownForm = Nothing
    Private _dropDownArrowIndex As Integer = -1

    Private Class NodeLayout
        Public Index As Integer
        Public TextRect As Rectangle
        Public ArrowRect As Rectangle
        Public HasArrow As Boolean
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

    Private 背景颜色 As Color = Color.FromArgb(36, 36, 36)
    <Category("LakeUI"), Description("背景颜色"), DefaultValue(GetType(Color), "36,36,36"), Browsable(True)>
    Public Overrides Property BackColor As Color
        Get
            Return 背景颜色
        End Get
        Set(value As Color)
            SetValue(背景颜色, value)
        End Set
    End Property

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
    <Category("LakeUI"), Description("下拉项悬停颜色"), DefaultValue(GetType(Color), "60,60,60"), Browsable(True)>
    Public Property DropDownHoverColor As Color
        Get
            Return 下拉悬停颜色
        End Get
        Set(value As Color)
            SetValue(下拉悬停颜色, value)
        End Set
    End Property

    Private 下拉文本颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("下拉项文本颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property DropDownForeColor As Color
        Get
            Return 下拉文本颜色
        End Get
        Set(value As Color)
            SetValue(下拉文本颜色, value)
        End Set
    End Property

    Private 下拉边框颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("下拉边框颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property DropDownBorderColor As Color
        Get
            Return 下拉边框颜色
        End Get
        Set(value As Color)
            SetValue(下拉边框颜色, value)
        End Set
    End Property

    Private 下拉边框宽度 As Integer = 1
    <Category("LakeUI"), Description("下拉边框宽度"), DefaultValue(1), Browsable(True)>
    Public Property DropDownBorderSize As Integer
        Get
            Return 下拉边框宽度
        End Get
        Set(value As Integer)
            SetValue(下拉边框宽度, Math.Max(0, value))
        End Set
    End Property

    Private 下拉圆角半径 As Integer = 6
    <Category("LakeUI"), Description("下拉圆角半径"), DefaultValue(6), Browsable(True)>
    Public Property DropDownCornerRadius As Integer
        Get
            Return 下拉圆角半径
        End Get
        Set(value As Integer)
            SetValue(下拉圆角半径, Math.Max(0, value))
        End Set
    End Property

    Private 下拉项高度 As Integer = 30
    <Category("LakeUI"), Description("下拉项高度"), DefaultValue(30), Browsable(True)>
    Public Property DropDownItemHeight As Integer
        Get
            Return 下拉项高度
        End Get
        Set(value As Integer)
            下拉项高度 = Math.Max(10, value)
        End Set
    End Property

    Private 下拉间距 As Integer = 2
    <Category("LakeUI"), Description("下拉与控件的垂直间距"), DefaultValue(2), Browsable(True)>
    Public Property DropDownGap As Integer
        Get
            Return 下拉间距
        End Get
        Set(value As Integer)
            下拉间距 = value
        End Set
    End Property

    Private 下拉最小宽度 As Integer = 160
    <Category("LakeUI"), Description("下拉最小宽度"), DefaultValue(160), Browsable(True)>
    Public Property DropDownMinWidth As Integer
        Get
            Return 下拉最小宽度
        End Get
        Set(value As Integer)
            下拉最小宽度 = Math.Max(20, value)
        End Set
    End Property

    Private 下拉内边距 As New Padding(4)
    <Category("LakeUI"), Description("下拉内边距"), Browsable(True)>
    Public Property DropDownPadding As Padding
        Get
            Return 下拉内边距
        End Get
        Set(value As Padding)
            下拉内边距 = value
        End Set
    End Property
    Private Function ShouldSerializeDropDownPadding() As Boolean
        Return 下拉内边距 <> New Padding(4)
    End Function
    Private Sub ResetDropDownPadding()
        下拉内边距 = New Padding(4)
    End Sub

    Private 下拉项内边距 As Integer = 10
    <Category("LakeUI"), Description("下拉项左右内边距"), DefaultValue(10), Browsable(True)>
    Public Property DropDownItemPadding As Integer
        Get
            Return 下拉项内边距
        End Get
        Set(value As Integer)
            下拉项内边距 = Math.Max(0, value)
        End Set
    End Property

    Private 下拉展开关闭动画时长 As Integer = 150
    <Category("LakeUI"), Description("下拉展开/关闭动画时长（毫秒），0 = 无动画"), DefaultValue(150), Browsable(True)>
    Public Property DropDownAnimationDuration As Integer
        Get
            Return 下拉展开关闭动画时长
        End Get
        Set(value As Integer)
            SetValue(下拉展开关闭动画时长, Math.Max(0, value))
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
    End Sub

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        CloseDropDown()
        MyBase.OnHandleDestroyed(e)
    End Sub

    Friend Sub OnItemsChangedInternal()
        _hoverNodeIndex = -1
        _pressedNodeIndex = -1
        If _dropDownForm IsNot Nothing Then CloseDropDown()
        Invalidate()
    End Sub
#End Region

#Region "布局"
    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function

    Private Sub RebuildLayout()
        _layoutCache.Clear()
        Dim s As Single = DpiScale()
        Dim padX As Integer = CInt(节点文本左右内边距 * s)
        Dim padY As Integer = CInt(节点垂直内边距 * s)
        Dim aaw As Integer = CInt(箭头区域宽度 * s)
        Dim h As Integer = ClientRectangle.Height
        Dim x As Integer = 0
        For i As Integer = 0 To _items.Count - 1
            Dim item As BreadcrumbItem = _items(i)
            If item Is Nothing Then Continue For
            Dim textW As Integer = TextRenderHelper.MeasureTextWidth(item.Text, Font, h)
            Dim nodeTextW As Integer = textW + padX * 2
            Dim layout As New NodeLayout With {
                .Index = i,
                .TextRect = New Rectangle(x, padY, nodeTextW, h - padY * 2),
                .HasArrow = item.HasChildren
            }
            x += nodeTextW
            If item.HasChildren Then
                layout.ArrowRect = New Rectangle(x, padY, aaw, h - padY * 2)
                x += aaw
            Else
                layout.ArrowRect = Rectangle.Empty
            End If
            _layoutCache.Add(layout)
        Next
    End Sub

    Private Function HitTest(p As Point, ByRef nodeIndex As Integer, ByRef isArrow As Boolean) As Boolean
        nodeIndex = -1
        isArrow = False
        For Each l In _layoutCache
            If l.TextRect.Contains(p) Then
                nodeIndex = l.Index
                isArrow = False
                Return True
            End If
            If l.HasArrow AndAlso l.ArrowRect.Contains(p) Then
                nodeIndex = l.Index
                isArrow = True
                Return True
            End If
        Next
        Return False
    End Function
#End Region

#Region "绘制"
    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        If 背景颜色.A < 255 Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        RebuildLayout()
        Dim w As Integer = ClientRectangle.Width
        Dim h As Integer = ClientRectangle.Height
        If 背景颜色.A < 255 Then
            绘制父容器背景(e.Graphics)
        End If
        If 背景颜色.A > 0 Then
            Using br As New SolidBrush(背景颜色)
                e.Graphics.FillRectangle(br, ClientRectangle)
            End Using
        End If

        Dim ssaa As Integer = If(Class1.GlobalSSAA > 1, Class1.GlobalSSAA, 超采样倍率)
        If ssaa > 1 Then
            Using bmp As New Bitmap(w * ssaa, h * ssaa)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.ScaleTransform(ssaa, ssaa)
                    DrawNodes(g)
                End Using
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                e.Graphics.DrawImage(bmp, 0, 0, w, h)
            End Using
        Else
            DrawNodes(e.Graphics)
        End If

        If Not Enabled Then
            Using br As New SolidBrush(Color.FromArgb(120, 0, 0, 0))
                e.Graphics.FillRectangle(br, ClientRectangle)
            End Using
        End If
    End Sub

    Private Sub 绘制父容器背景(g As Graphics)
        If Parent Is Nothing Then Return
        Dim state = g.Save()
        g.TranslateTransform(-Me.Left, -Me.Top)
        Using pea As New PaintEventArgs(g, New Rectangle(Me.Left, Me.Top, Me.Width, Me.Height))
            InvokePaintBackground(Parent, pea)
            InvokePaint(Parent, pea)
        End Using
        g.Restore(state)
    End Sub

    Private Sub DrawNodes(g As Graphics)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit

        Dim s As Single = DpiScale()
        Dim radius As Single = 节点圆角半径 * s

        For Each l In _layoutCache
            Dim item As BreadcrumbItem = _items(l.Index)
            If item Is Nothing Then Continue For

            Dim textHover As Boolean = (_hoverNodeIndex = l.Index AndAlso Not _hoverIsArrow)
            Dim textPressed As Boolean = (_pressedNodeIndex = l.Index AndAlso Not _pressedIsArrow)
            Dim textHl As Color = Color.Empty
            If textPressed Then
                textHl = 节点按下背景
            ElseIf textHover Then
                textHl = 节点悬停背景
            End If
            If textHl <> Color.Empty AndAlso textHl.A > 0 Then
                FillRoundedRect(g, l.TextRect, radius, textHl)
            End If

            If l.HasArrow Then
                Dim arrowHover As Boolean = (_hoverNodeIndex = l.Index AndAlso _hoverIsArrow)
                Dim arrowPressed As Boolean = (_pressedNodeIndex = l.Index AndAlso _pressedIsArrow) OrElse (_dropDownArrowIndex = l.Index)
                Dim arrHl As Color = Color.Empty
                If arrowPressed Then
                    arrHl = 节点按下背景
                ElseIf arrowHover Then
                    arrHl = 节点悬停背景
                End If
                If arrHl <> Color.Empty AndAlso arrHl.A > 0 Then
                    FillRoundedRect(g, l.ArrowRect, radius, arrHl)
                End If
            End If

            TextRenderer.DrawText(g, item.Text, Font, l.TextRect, ForeColor,
                TextFormatFlags.NoPadding Or TextFormatFlags.HorizontalCenter Or
                TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis)

            If l.HasArrow Then
                Dim arrowClr As Color = 箭头颜色
                If (_hoverNodeIndex = l.Index AndAlso _hoverIsArrow) OrElse (_dropDownArrowIndex = l.Index) Then
                    arrowClr = 箭头悬停颜色
                End If
                Dim chevronDown As Boolean = (_dropDownArrowIndex = l.Index)
                DrawChevron(g, l.ArrowRect, arrowClr, chevronDown)
            End If
        Next
    End Sub

    Private Sub FillRoundedRect(g As Graphics, r As Rectangle, radius As Single, c As Color)
        If r.Width <= 0 OrElse r.Height <= 0 Then Return
        If radius <= 0.5F Then
            Using br As New SolidBrush(c)
                g.FillRectangle(br, r)
            End Using
            Return
        End If
        Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(New RectangleF(r.X, r.Y, r.Width, r.Height), radius)
            Using br As New SolidBrush(c)
                g.FillPath(br, path)
            End Using
        End Using
    End Sub

    ''' <summary>
    ''' 绘制 Win11 风格的 chevron 箭头（两段折线，非实心三角）。
    ''' </summary>
    Private Sub DrawChevron(g As Graphics, area As Rectangle, c As Color, pointDown As Boolean)
        Dim s As Single = DpiScale()
        Dim sz As Single = 箭头大小 * s
        Dim cx As Single = area.X + area.Width / 2.0F
        Dim cy As Single = area.Y + area.Height / 2.0F
        Dim half As Single = sz / 2.0F
        Dim hh As Single = half * 0.55F
        Dim p1, p2, p3 As PointF
        If pointDown Then
            p1 = New PointF(cx - half, cy - hh)
            p2 = New PointF(cx, cy + hh)
            p3 = New PointF(cx + half, cy - hh)
        Else
            p1 = New PointF(cx - hh, cy - half)
            p2 = New PointF(cx + hh, cy)
            p3 = New PointF(cx - hh, cy + half)
        End If
        Using pen As New Pen(c, 箭头线宽 * s)
            pen.StartCap = LineCap.Round
            pen.EndCap = LineCap.Round
            pen.LineJoin = LineJoin.Round
            g.DrawLines(pen, New PointF() {p1, p2, p3})
        End Using
    End Sub
#End Region

#Region "鼠标处理"
    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim idx As Integer
        Dim isArrow As Boolean
        Dim hit As Boolean = HitTest(e.Location, idx, isArrow)
        If hit <> (_hoverNodeIndex >= 0) OrElse idx <> _hoverNodeIndex OrElse isArrow <> _hoverIsArrow Then
            _hoverNodeIndex = If(hit, idx, -1)
            _hoverIsArrow = isArrow
            Cursor = If(hit, Cursors.Hand, Cursors.Default)
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If _hoverNodeIndex <> -1 Then
            _hoverNodeIndex = -1
            _hoverIsArrow = False
            Cursor = Cursors.Default
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not Enabled OrElse e.Button <> MouseButtons.Left Then Return
        Dim idx As Integer
        Dim isArrow As Boolean
        If HitTest(e.Location, idx, isArrow) Then
            _pressedNodeIndex = idx
            _pressedIsArrow = isArrow
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If e.Button <> MouseButtons.Left Then Return
        Dim idx As Integer
        Dim isArrow As Boolean
        Dim hit As Boolean = HitTest(e.Location, idx, isArrow)
        Dim wasPressedIdx As Integer = _pressedNodeIndex
        Dim wasPressedArrow As Boolean = _pressedIsArrow
        _pressedNodeIndex = -1
        _pressedIsArrow = False
        Invalidate()
        If hit AndAlso idx = wasPressedIdx AndAlso isArrow = wasPressedArrow Then
            If isArrow Then
                ToggleDropDown(idx)
            Else
                Dim it = _items(idx)
                RaiseEvent ItemClicked(Me, New BreadcrumbItemEventArgs(it, idx))
            End If
        End If
    End Sub
#End Region

#Region "下拉控制"
    Private Sub ToggleDropDown(arrowIndex As Integer)
        If _dropDownForm IsNot Nothing AndAlso _dropDownArrowIndex = arrowIndex Then
            CloseDropDown()
            Return
        End If
        OpenDropDown(arrowIndex)
    End Sub

    Private Sub OpenDropDown(arrowIndex As Integer)
        If arrowIndex < 0 OrElse arrowIndex >= _items.Count Then Return
        If _dropDownForm IsNot Nothing Then
            ImmediatelyDisposeDropDown()
        End If
        Dim parentItem As BreadcrumbItem = _items(arrowIndex)
        Dim args As New BreadcrumbDropDownOpeningEventArgs(parentItem, arrowIndex)
        RaiseEvent DropDownOpening(Me, args)
        If args.Cancel OrElse args.Items.Count = 0 Then Return

        _dropDownArrowIndex = arrowIndex
        _dropDownForm = New DropDownForm(Me, arrowIndex, parentItem, args.Items)
        _dropDownForm.ShowDropDown()
        Invalidate()
        RaiseEvent DropDownOpened(Me, New BreadcrumbItemEventArgs(parentItem, arrowIndex))
    End Sub

    Friend Sub CloseDropDown()
        If _dropDownForm Is Nothing Then Return
        If _dropDownForm.正在关闭动画 Then Return
        If 下拉展开关闭动画时长 > 0 AndAlso _dropDownForm.IsHandleCreated AndAlso Not _dropDownForm.IsDisposed Then
            _dropDownForm.开始关闭动画()
            Return
        End If
        ImmediatelyDisposeDropDown()
    End Sub

    Private Sub ImmediatelyDisposeDropDown()
        If _dropDownForm Is Nothing Then Return
        Dim f = _dropDownForm
        Dim parentIdx As Integer = _dropDownArrowIndex
        Dim parentItem As BreadcrumbItem = If(parentIdx >= 0 AndAlso parentIdx < _items.Count, _items(parentIdx), Nothing)
        _dropDownForm = Nothing
        _dropDownArrowIndex = -1
        Try
            f.关闭并释放()
        Catch
        End Try
        Invalidate()
        RaiseEvent DropDownClosed(Me, New BreadcrumbItemEventArgs(parentItem, parentIdx))
    End Sub

    Friend Sub OnDropDownClosed_FromForm()
        Dim parentIdx As Integer = _dropDownArrowIndex
        Dim parentItem As BreadcrumbItem = If(parentIdx >= 0 AndAlso parentIdx < _items.Count, _items(parentIdx), Nothing)
        _dropDownForm = Nothing
        _dropDownArrowIndex = -1
        Invalidate()
        RaiseEvent DropDownClosed(Me, New BreadcrumbItemEventArgs(parentItem, parentIdx))
    End Sub

    Friend Sub OnDropDownItemClicked_FromForm(parentIndex As Integer, parentItem As BreadcrumbItem, childIndex As Integer, child As BreadcrumbItem)
        RaiseEvent DropDownItemClicked(Me, New BreadcrumbDropDownItemClickedEventArgs(parentItem, parentIndex, child, childIndex))
        CloseDropDown()
    End Sub

    ''' <summary>主动收起当前下拉。</summary>
    Public Sub CloseAnyDropDown()
        CloseDropDown()
    End Sub

    ''' <summary>主动展开指定节点的下拉（会触发 DropDownOpening 让用户填充）。</summary>
    Public Sub ShowDropDownFor(nodeIndex As Integer)
        OpenDropDown(nodeIndex)
    End Sub
#End Region

#Region "辅助"
    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        Invalidate()
    End Sub

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        If _dropDownForm IsNot Nothing Then CloseDropDown()
        Invalidate()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        Invalidate()
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        If Not Enabled AndAlso _dropDownForm IsNot Nothing Then CloseDropDown()
        Invalidate()
    End Sub
#End Region

#Region "下拉弹窗类"
    Private Class DropDownForm
        Inherits PopupForm
        Implements IMessageFilter

        Private ReadOnly _owner As BreadcrumbNavigationBar
        Private ReadOnly _parentIndex As Integer
        Private ReadOnly _parentItem As BreadcrumbItem
        Private ReadOnly _items As List(Of BreadcrumbItem)
        Private _hoverIndex As Integer = -1
        Private _pressedIndex As Integer = -1

#Disable Warning IDE0044
        Private _finalHeight As Integer
        Private _finalWidth As Integer
        Private _originPt As Point
        Private _useIdle As Boolean = False

        Private ReadOnly 展开关闭秒表 As New Stopwatch()
        Private 展开关闭计时器 As Timer
        Private 展开关闭动画中 As Boolean = False
        Friend 正在关闭动画 As Boolean = False
#Enable Warning IDE0044

        Private Const WM_LBUTTONDOWN As Integer = &H201
        Private Const WM_RBUTTONDOWN As Integer = &H204
        Private Const WM_MBUTTONDOWN As Integer = &H207
        Private Const WM_NCLBUTTONDOWN As Integer = &HA1
        Private Const WM_ACTIVATEAPP As Integer = &H1C

        Public Sub New(owner As BreadcrumbNavigationBar, parentIndex As Integer, parentItem As BreadcrumbItem, items As List(Of BreadcrumbItem))
            _owner = owner
            _parentIndex = parentIndex
            _parentItem = parentItem
            _items = items
            Me.DoubleBuffered = True
            Me.AutoScaleMode = AutoScaleMode.Dpi
            Me.BackColor = owner.下拉背景颜色

            _useIdle = (owner.下拉动画帧率 <= 0)
            If Not _useIdle Then
                Dim interval As Integer = Math.Max(1, 1000 \ owner.下拉动画帧率)
                展开关闭计时器 = New Timer() With {.Interval = interval}
            End If

            ComputeSize()
            ComputeLocation()
            Me.Location = _originPt
            Me.Size = New Size(_finalWidth, _finalHeight)
        End Sub

        Private Sub ComputeSize()
            Dim s As Single = _owner.DpiScale()
            Dim itemH As Integer = CInt(_owner.下拉项高度 * s)
            Dim bw As Integer = CInt(_owner.下拉边框宽度 * s)
            Dim pad As Padding = _owner.下拉内边距
            Dim itemPad As Integer = CInt(_owner.下拉项内边距 * s)
            _finalHeight = _items.Count * itemH + bw * 2 + pad.Top + pad.Bottom

            Dim maxTextW As Integer = 0
            For Each it In _items
                Dim w = TextRenderHelper.MeasureTextWidth(If(it Is Nothing, "", it.Text), _owner.Font, itemH)
                If w > maxTextW Then maxTextW = w
            Next
            Dim w0 As Integer = maxTextW + itemPad * 2 + bw * 2 + pad.Left + pad.Right
            Dim minW As Integer = CInt(_owner.下拉最小宽度 * s)
            _finalWidth = Math.Max(minW, w0)
        End Sub

        Private Sub ComputeLocation()
            Dim s As Single = _owner.DpiScale()
            Dim gap As Integer = CInt(_owner.下拉间距 * s)
            Dim layout = GetParentArrowRect()
            Dim anchorScreen As Point = _owner.PointToScreen(New Point(layout.X + layout.Width \ 2, _owner.Height + gap))
            Dim x As Integer = anchorScreen.X - _finalWidth \ 2
            Dim y As Integer = anchorScreen.Y
            Dim scr As Screen = Screen.FromControl(_owner)
            If x < scr.WorkingArea.Left Then x = scr.WorkingArea.Left
            If x + _finalWidth > scr.WorkingArea.Right Then x = scr.WorkingArea.Right - _finalWidth
            If y + _finalHeight > scr.WorkingArea.Bottom Then
                Dim aboveAnchor As Point = _owner.PointToScreen(New Point(layout.X + layout.Width \ 2, -gap))
                y = aboveAnchor.Y - _finalHeight
                If y < scr.WorkingArea.Top Then y = scr.WorkingArea.Top
            End If
            _originPt = New Point(x, y)
        End Sub

        Private Function GetParentArrowRect() As Rectangle
            For Each l In _owner._layoutCache
                If l.Index = _parentIndex AndAlso l.HasArrow Then Return l.ArrowRect
            Next
            Return New Rectangle(0, 0, _owner.Width, _owner.Height)
        End Function

        Friend Sub ShowDropDown()
            Application.AddMessageFilter(Me)
            If _owner.下拉展开关闭动画时长 > 0 Then
                Me.Size = New Size(_finalWidth, 1)
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
            停止展开关闭驱动()
            If 展开关闭计时器 IsNot Nothing Then 展开关闭计时器.Dispose()
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
                    Me.Location = _originPt
                    Me.Size = New Size(_finalWidth, _finalHeight)
                End If
                Return
            End If
            Dim elapsed As Double = 展开关闭秒表.Elapsed.TotalMilliseconds
            Dim t As Single = CSng(Math.Min(elapsed / duration, 1.0))
            Dim eased As Single = 1.0F - CSng(Math.Pow(1.0 - t, 3))
            If 正在关闭动画 Then
                Dim newH As Integer = Math.Max(1, CInt(_finalHeight * (1.0F - eased)))
                Me.Size = New Size(_finalWidth, newH)
            Else
                Dim newH As Integer = Math.Max(1, CInt(_finalHeight * eased))
                Me.Size = New Size(_finalWidth, newH)
            End If
            Invalidate()
            If t >= 1.0F Then
                停止展开关闭驱动()
                展开关闭动画中 = False
                If 正在关闭动画 Then
                    完成关闭()
                Else
                    Me.Location = _originPt
                    Me.Size = New Size(_finalWidth, _finalHeight)
                    Invalidate()
                End If
            End If
        End Sub

        Private Sub 完成关闭()
            正在关闭动画 = False
            展开关闭动画中 = False
            关闭并释放()
            _owner.OnDropDownClosed_FromForm()
        End Sub

        Protected Overrides Sub WndProc(ByRef m As Message)
            MyBase.WndProc(m)
            If m.Msg = WM_ACTIVATEAPP AndAlso m.WParam = IntPtr.Zero Then
                If Not 正在关闭动画 Then BeginInvoke(Sub() _owner.CloseDropDown())
            End If
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
            Dim g As Graphics = e.Graphics
            Dim w As Integer = ClientRectangle.Width
            Dim h As Integer = ClientRectangle.Height
            Dim s As Single = _owner.DpiScale()
            Dim bw As Integer = CInt(_owner.下拉边框宽度 * s)
            Dim radius As Single = _owner.下拉圆角半径 * s
            Dim pad As Padding = _owner.下拉内边距
            Dim itemH As Integer = CInt(_owner.下拉项高度 * s)
            Dim itemPad As Integer = CInt(_owner.下拉项内边距 * s)

            g.SmoothingMode = SmoothingMode.AntiAlias
            g.PixelOffsetMode = PixelOffsetMode.HighQuality
            Using bgBr As New SolidBrush(_owner.下拉背景颜色)
                g.FillRectangle(bgBr, 0, 0, w, h)
            End Using

            Dim boundsRect As New RectangleF(0, 0, w - 1, h - 1)
            If bw > 0 Then
                Dim half As Single = bw / 2.0F
                boundsRect.Inflate(-half, -half)
            End If

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
                If bw > 0 Then
                    Using pen As New Pen(_owner.下拉边框颜色, bw)
                        g.DrawRectangle(pen, boundsRect.X, boundsRect.Y, boundsRect.Width, boundsRect.Height)
                    End Using
                End If
            End If

            g.SetClip(New Rectangle(bw, bw, w - bw * 2, h - bw * 2))
            g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit
            For i As Integer = 0 To _items.Count - 1
                Dim it As BreadcrumbItem = _items(i)
                If it Is Nothing Then Continue For
                Dim y As Integer = bw + pad.Top + i * itemH
                Dim itemRect As New Rectangle(bw + pad.Left, y, w - bw * 2 - pad.Left - pad.Right, itemH)
                If i = _hoverIndex Then
                    Dim hr As Single = Math.Max(0, radius - 2)
                    FillRoundedRectF(g, itemRect, hr, _owner.下拉悬停颜色)
                End If
                Dim textRect As New Rectangle(itemRect.X + itemPad, itemRect.Y, itemRect.Width - itemPad * 2, itemRect.Height)
                TextRenderer.DrawText(g, it.Text, _owner.Font, textRect, _owner.下拉文本颜色,
                    TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding)
            Next
            g.ResetClip()
        End Sub

        Private Sub FillRoundedRectF(g As Graphics, r As Rectangle, radius As Single, c As Color)
            If r.Width <= 0 OrElse r.Height <= 0 Then Return
            If radius <= 0.5F Then
                Using br As New SolidBrush(c)
                    g.FillRectangle(br, r)
                End Using
                Return
            End If
            Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(New RectangleF(r.X, r.Y, r.Width, r.Height), radius)
                Using br As New SolidBrush(c)
                    g.FillPath(br, path)
                End Using
            End Using
        End Sub

        Private Function GetItemIndexAtY(y As Integer) As Integer
            Dim s As Single = _owner.DpiScale()
            Dim bw As Integer = CInt(_owner.下拉边框宽度 * s)
            Dim itemH As Integer = CInt(_owner.下拉项高度 * s)
            Dim pad As Padding = _owner.下拉内边距
            Dim idx As Integer = (y - bw - pad.Top) \ itemH
            If idx < 0 OrElse idx >= _items.Count Then Return -1
            Return idx
        End Function

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim idx As Integer = GetItemIndexAtY(e.Y)
            If idx <> _hoverIndex Then
                _hoverIndex = idx
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            If _hoverIndex <> -1 Then
                _hoverIndex = -1
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            _pressedIndex = GetItemIndexAtY(e.Y)
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            Dim idx As Integer = GetItemIndexAtY(e.Y)
            If _pressedIndex >= 0 AndAlso idx = _pressedIndex Then
                Dim child As BreadcrumbItem = _items(idx)
                _owner.OnDropDownItemClicked_FromForm(_parentIndex, _parentItem, idx, child)
            End If
            _pressedIndex = -1
        End Sub
    End Class
#End Region

End Class
