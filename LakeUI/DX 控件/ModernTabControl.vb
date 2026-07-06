Imports System.ComponentModel
Imports Vortice.Direct2D1
Imports Vortice.DirectWrite

''' <summary>
''' 现代化横向选项卡控件。采用自绘标签栏 + 面板的组合方式，
''' 彻底避开原生 TabControl 的底层协议问题。
''' 支持顶部/底部标签栏位置、自适应宽度/均分宽度两种布局模式、
''' 左/中/右/顶部居中/底部居中五种对齐方式、横向滚动条、悬停动画、图标和分割线。
''' 每个 <see cref="ModernTab"/> 可绑定一个 <see cref="Control"/> 作为内容，
''' 运行时自动切换其可见性。
''' </summary>
<DefaultEvent("SelectedIndexChanged")>
Public Class ModernTabControl
    Implements IOuterToInnerRefreshFilter, V3_IGpuRenderable, V3_IGpuInvalidationSource

#Region "内部类型"
    ''' <summary>
    ''' 表示 <see cref="ModernTabControl"/> 中的一个选项卡项。
    ''' 支持独立设置标题字体、颜色、图标，以及绑定一个 <see cref="Control"/> 作为选项卡内容。
    ''' 也可设为分割线。
    ''' </summary>
    Public Class ModernTab

        Friend Property Owner As ModernTabControl

        Private Sub 通知父级重绘()
            Owner?.请求V3渲染()
        End Sub

        Private _text As String = "ModernTab"
        <Category("LakeUI"), Description("标签页标题文本"), DefaultValue("ModernTab"), Browsable(True)>
        Public Property Text As String
            Get
                Return _text
            End Get
            Set(value As String)
                value = If(value, "")
                If String.Equals(_text, value, StringComparison.Ordinal) Then Return
                _text = value
                Owner?.InvalidateLayoutCache()
                通知父级重绘()
            End Set
        End Property

        Private _tabFont As Font = Nothing
        <Category("LakeUI"), Description("该标签页标题使用的字体，为 Nothing 时使用控件默认字体"), Browsable(True)>
        Public Property TabFont As Font
            Get
                Return _tabFont
            End Get
            Set(value As Font)
                _tabFont = value
                If Owner IsNot Nothing Then Owner.InvalidateFontResources()
                通知父级重绘()
                Owner?.RefreshFontDependentRenderingNow()
            End Set
        End Property
        Private Function ShouldSerializeTabFont() As Boolean
            Return _tabFont IsNot Nothing
        End Function

        Private _normalForeColor As Color = Color.Empty
        <Category("LakeUI"), Description("该标签页标题的默认文字颜色，为 Empty 时使用控件默认颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
        Public Property NormalForeColor As Color
            Get
                Return _normalForeColor
            End Get
            Set(value As Color)
                _normalForeColor = value
                通知父级重绘()
            End Set
        End Property

        Private _selectedForeColor As Color = Color.Empty
        <Category("LakeUI"), Description("该标签页标题被选中时的文字颜色，为 Empty 时使用控件默认颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
        Public Property SelectedForeColor As Color
            Get
                Return _selectedForeColor
            End Get
            Set(value As Color)
                _selectedForeColor = value
                通知父级重绘()
            End Set
        End Property

        Private _tabIcon As Image = Nothing
        <Category("LakeUI"), Description("该标签页标题使用的图标"), DefaultValue(GetType(Image), Nothing), Browsable(True)>
        Public Property TabIcon As Image
            Get
                Return _tabIcon
            End Get
            Set(value As Image)
                If Object.ReferenceEquals(_tabIcon, value) Then Return
                _tabIcon = value
                Owner?.InvalidateLayoutCache()
                通知父级重绘()
            End Set
        End Property

        Private _isSeparator As Boolean = False
        <Category("LakeUI"), Description("是否是分割线"), DefaultValue(False), Browsable(True)>
        Public Property IsSeparator As Boolean
            Get
                Return _isSeparator
            End Get
            Set(value As Boolean)
                If _isSeparator = value Then Return
                _isSeparator = value
                Owner?.InvalidateLayoutCache()
                通知父级重绘()
            End Set
        End Property

        Private _boundControl As Control = Nothing
        <Category("LakeUI"), Description("绑定的内容控件，切换到此选项卡时将显示该控件"), DefaultValue(GetType(Control), Nothing), Browsable(True)>
        Public Property BoundControl As Control
            Get
                Return _boundControl
            End Get
            Set(value As Control)
                If Object.ReferenceEquals(_boundControl, value) Then Return
                Dim oldControl = _boundControl
                _boundControl = value
                If Owner IsNot Nothing Then Owner.项目绑定控件已改变(Me, oldControl)
            End Set
        End Property

        Public Sub New()
        End Sub

        Public Sub New(text As String)
            _text = text
        End Sub

        Public Overrides Function ToString() As String
            If IsSeparator Then Return "│ Separator │"
            Return If(String.IsNullOrEmpty(_text), "ModernTab", _text)
        End Function

    End Class

    ''' <summary>标签栏位于控件的顶部还是底部</summary>
    Public Enum TabPositionEnum
        Top
        Bottom
    End Enum

    ''' <summary>选项卡宽度的计算模式</summary>
    Public Enum TabSizingEnum
        ''' <summary>每个选项卡根据文本和图标自动计算宽度</summary>
        AutoWidth
        ''' <summary>所有选项卡均分可用宽度</summary>
        EqualWidth
    End Enum

    ''' <summary>选项卡组在标签栏中的对齐方式（仅在 AutoWidth 且未溢出时生效）</summary>
    Public Enum TabAlignmentEnum
        Left
        Center
        Right
        ''' <summary>水平居中，标签页项紧靠标签栏顶部</summary>
        TopCenter
        ''' <summary>水平居中，标签页项紧靠标签栏底部</summary>
        BottomCenter
    End Enum

    ''' <summary>Ribbon 模式下折叠按钮的对齐方位</summary>
    Public Enum CollapseButtonAlignmentEnum
        ''' <summary>折叠按钮位于标签栏最左侧</summary>
        Left
        ''' <summary>折叠按钮位于标签栏最右侧</summary>
        Right
    End Enum

    Private Class TabAnimState
        Public 当前值 As Single = 0.0F
        Public 目标值 As Single = 0.0F
        Public 起始值 As Single = 0.0F
        Public 起始时刻 As Long = 0
    End Class

    Private Class BoundPageState
        Public HasBeenShown As Boolean
        Public LastShownSize As Size = Size.Empty
        Public LastShownBackgroundVersion As Integer = -1
        Public LastShownPanelBounds As Rectangle = Rectangle.Empty
        Public LastShownPanelParent As Control = Nothing
        Public ForceRefreshDuringSwitch As Boolean
    End Class

    Private Class SwitchRefreshScope
        Implements IDisposable

        Private ReadOnly _owner As ModernTabControl
        Private _disposed As Boolean

        Public Sub New(owner As ModernTabControl)
            _owner = owner
            If _owner Is Nothing Then Return
            _owner._切页刷新过滤深度 += 1
            _owner._切页刷新过滤序号 += 1
            _owner._切页刷新过滤启用 = True
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then Return
            _disposed = True
            If _owner Is Nothing Then Return
            _owner._切页刷新过滤深度 = Math.Max(0, _owner._切页刷新过滤深度 - 1)
            If _owner._切页刷新过滤深度 = 0 Then
                _owner.延迟结束切页刷新过滤(_owner._切页刷新过滤序号)
            End If
        End Sub
    End Class

    ''' <summary>
    ''' 支持透明背景的内容承载面板。当 BackColor 的 Alpha 为 0 或为透明色时，
    ''' 通过调用背景源的 Paint 流程取真实像素作为背景。
    ''' </summary>
    Private Class 透明内容面板
        Inherits Panel
        Implements IOuterToInnerRefreshFilter, V3_IGpuRenderable, V3_IGpuInvalidationSource

        Private _ownerControl As ModernTabControl
        Private _backgroundSource As Control

        Public Sub New()
            SetStyle(ControlStyles.SupportsTransparentBackColor Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.UserPaint Or
                     ControlStyles.ResizeRedraw, True)
            UpdateStyles()
        End Sub

        Friend Sub SetOwnerControl(value As ModernTabControl)
            _ownerControl = value
        End Sub

        <Browsable(False),
         EditorBrowsable(EditorBrowsableState.Never),
         DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property BackgroundSource As Control
            Get
                Return _backgroundSource
            End Get
            Set(value As Control)
                If _backgroundSource Is value Then Return
                _backgroundSource = D3D_BackgroundPenetration.SetBackgroundSource(Me, _backgroundSource, value)
                请求V3渲染()
            End Set
        End Property

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            ' V3-only: pixels are emitted by RenderGpu.
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
        End Sub

        Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
            If Width <= 0 OrElse Height <= 0 Then Return
            Dim source As Control = _backgroundSource
            If source IsNot Nothing Then
                context.DrawBackgroundSource(Me, source, New RectangleF(0, 0, Width, Height))
            ElseIf BackColor.A > 0 Then
                context.FillRectangle(New RectangleF(0, 0, Width, Height), BackColor)
            End If
        End Sub

        Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
            Return New Rectangle(Point.Empty, Me.Size)
        End Function

        Private Sub 请求V3渲染(Optional dirtyRect As Rectangle = Nothing)
            If IsDisposed Then Return
            Dim rect As Rectangle = If(dirtyRect.IsEmpty, New Rectangle(Point.Empty, Me.Size), dirtyRect)
            V3_InvalidationRouter.RequestRender(Me, rect)
        End Sub

        Private Sub 解除背景穿透消费者()
            Try : D3D_BackgroundPenetration.UnregisterConsumer(Me) : Catch : End Try
        End Sub

        Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
            解除背景穿透消费者()
            MyBase.OnHandleDestroyed(e)
        End Sub

        Protected Overrides Sub OnVisibleChanged(e As EventArgs)
            MyBase.OnVisibleChanged(e)
            If Not Me.Visible Then 解除背景穿透消费者()
        End Sub

        Protected Overrides Sub OnParentChanged(e As EventArgs)
            MyBase.OnParentChanged(e)
            If Me.Parent Is Nothing Then 解除背景穿透消费者()
        End Sub

        Public Function ShouldSuppressOuterToInnerRefresh(target As Control,
                                                          rect As Rectangle,
                                                          hasFull As Boolean,
                                                          invalidateChildren As Boolean,
                                                          fromChildrenExpansion As Boolean) As Boolean Implements IOuterToInnerRefreshFilter.ShouldSuppressOuterToInnerRefresh
            If _ownerControl Is Nothing OrElse _ownerControl.IsDisposed Then Return False
            Return _ownerControl.ShouldSuppressOuterToInnerRefresh(target, rect, hasFull, invalidateChildren, fromChildrenExpansion)
        End Function
    End Class
#End Region

#Region "构造"
    Private ReadOnly _标签页动画 As New Dictionary(Of Integer, TabAnimState)
    Private _悬停索引 As Integer = -1
    Private ReadOnly _动画助手 As New V3_AnimationHelper(Me)
    Private _动画中 As Boolean = False
    Private ReadOnly _内容面板 As New 透明内容面板()
    Private ReadOnly 项目列表 As New List(Of ModernTab)
    Private _滚动偏移 As Integer = 0
    Private _缓存宽度 As Single() = Nothing
    Private _缓存可用宽度 As Single = -1

    ' 横向滚动条状态
    Private _滚动条ThumbRect As Rectangle = Rectangle.Empty
    Private _滚动条TrackRect As Rectangle = Rectangle.Empty
    Private _滚动条IsHover As Boolean = False
    Private _滚动条IsDragging As Boolean = False
    Private _滚动条DragStartX As Integer = 0
    Private _滚动条DragStartOffset As Integer = 0

    ' Ribbon 模式 / 折叠按钮状态
    Private _折叠按钮悬停 As Boolean = False
    Private _折叠按钮动画 As New TabAnimState()
    Private _折叠按钮缓存矩形 As RectangleF = RectangleF.Empty
    Private _浮层显示 As Boolean = False
    Private _鼠标过滤器 As RibbonMouseFilter = Nothing

    ' V3 渲染：每次 OnPaint 只请求窗口级 D3D compositor 刷新。
    Private ReadOnly _绑定页状态 As New Dictionary(Of Control, BoundPageState)()
    Private _背景刷新版本 As Integer = 0
    Private _背景源已订阅 As Control = Nothing
    Private _切换页抑制刷新 As Boolean = True
    Private _当前绑定控件 As Control = Nothing
    Private _切页刷新过滤深度 As Integer = 0
    Private _切页刷新过滤序号 As Integer = 0
    Private _切页刷新过滤启用 As Boolean = False

    Private Class RibbonMouseFilter
        Implements IMessageFilter
        Private ReadOnly _owner As ModernTabControl
        Public Sub New(owner As ModernTabControl)
            _owner = owner
        End Sub
        Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
            Const WM_MOUSEMOVE As Integer = &H200
            Const WM_NCMOUSEMOVE As Integer = &HA0
            Const WM_LBUTTONDOWN As Integer = &H201
            Const WM_RBUTTONDOWN As Integer = &H204
            Const WM_MBUTTONDOWN As Integer = &H207
            Const WM_NCLBUTTONDOWN As Integer = &HA1
            Select Case m.Msg
                Case WM_MOUSEMOVE, WM_NCMOUSEMOVE
                    _owner.检查浮层是否需要关闭(False)
                Case WM_LBUTTONDOWN, WM_RBUTTONDOWN, WM_MBUTTONDOWN, WM_NCLBUTTONDOWN
                    _owner.检查浮层是否需要关闭(True)
            End Select
            Return False
        End Function
    End Class

    Public Sub New()
        InitializeComponent()
        SetStyle(ControlStyles.AllPaintingInWmPaint, True)
        SetStyle(ControlStyles.DoubleBuffer, True)
        SetStyle(ControlStyles.UserPaint, True)
        SetStyle(ControlStyles.OptimizedDoubleBuffer, True)
        SetStyle(ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, True)
        SetStyle(ControlStyles.SupportsTransparentBackColor, True)
        DoubleBuffered = True

        _内容面板.Dock = DockStyle.None
        _内容面板.SetOwnerControl(Me)
        _内容面板.BackColor = 获取内容面板有效背景色()
        Me.Controls.Add(_内容面板)

        _动画助手.DirtyProvider = AddressOf 动画驱动脏区
        _动画助手.FPS = 动画帧率值
        同步内容面板布局()
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        同步切页背景源订阅()
    End Sub

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        解除背景穿透消费者()
        Try : D3D_BackgroundPenetration.UnregisterConsumer(_内容面板) : Catch : End Try
        释放所有切页刷新抑制状态()
        停止动画驱动()
        停用鼠标过滤器()
        MyBase.OnHandleDestroyed(e)
    End Sub

    <Category("LakeUI"), Description("选项卡项集合"), Browsable(True)>
    <DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
    Public ReadOnly Property Items As List(Of ModernTab)
        Get
            Return 项目列表
        End Get
    End Property

    ''' <summary>
    ''' 刷新所有项的内部引用并同步绑定控件显示。在运行时增删项后调用。
    ''' </summary>
    Public Sub RefreshItems()
        确保Owner()
        标准化选中索引()
        清理已移除绑定控件状态()
        InvalidateLayoutCache()
        _标签页动画.Clear()
        _悬停索引 = -1
        限制滚动范围()
        切换绑定控件()
        请求V3渲染()
    End Sub

    Private Sub 确保Owner()
        For Each item In 项目列表
            If item IsNot Nothing AndAlso item.Owner IsNot Me Then item.Owner = Me
        Next
    End Sub

    Private Sub 标准化选中索引()
        If 项目列表.Count = 0 Then
            _selectedIndex = -1
            Return
        End If

        If _selectedIndex >= 项目列表.Count Then _selectedIndex = 项目列表.Count - 1
        If _selectedIndex < -1 Then _selectedIndex = -1

        If _selectedIndex >= 0 Then
            Dim item = 项目列表(_selectedIndex)
            If item Is Nothing OrElse item.IsSeparator Then
                _selectedIndex = 第一个可选索引()
            End If
        End If
    End Sub

    Private Function 绑定控件是否仍在项目中(ctrl As Control, Optional ignoreItem As ModernTab = Nothing) As Boolean
        If ctrl Is Nothing Then Return False
        For Each item In 项目列表
            If item Is Nothing OrElse item Is ignoreItem Then Continue For
            If item.BoundControl Is ctrl Then Return True
        Next
        Return False
    End Function

    Private Sub 清理已移除绑定控件状态()
        For Each ctrl In _绑定页状态.Keys.ToArray()
            If Not 绑定控件是否仍在项目中(ctrl) Then
                If ctrl Is _当前绑定控件 OrElse ctrl.Parent Is _内容面板 Then
                    移除绑定控件(ctrl)
                End If
                释放绑定控件状态(ctrl)
            End If
        Next
    End Sub

    Private Sub 项目绑定控件已改变(item As ModernTab, oldControl As Control)
        If oldControl IsNot Nothing Then
            Dim stillBound = 绑定控件是否仍在项目中(oldControl, item)
            If Not stillBound AndAlso (oldControl Is _当前绑定控件 OrElse oldControl.Parent Is _内容面板) Then
                移除绑定控件(oldControl)
                If oldControl Is _当前绑定控件 Then _当前绑定控件 = Nothing
            End If
            If Not stillBound Then
                释放绑定控件状态(oldControl)
            End If
        End If
        If item IsNot Nothing AndAlso 项目列表.IndexOf(item) = _selectedIndex Then
            切换绑定控件()
        End If
        请求V3渲染()
    End Sub

    Private Function 获取索引绑定控件(index As Integer) As Control
        If index < 0 OrElse index >= 项目列表.Count Then Return Nothing
        Dim item = 项目列表(index)
        If item Is Nothing Then Return Nothing
        Return item.BoundControl
    End Function

    Private Function 获取绑定页状态(ctrl As Control) As BoundPageState
        If ctrl Is Nothing Then Return Nothing
        Dim state As BoundPageState = Nothing
        If Not _绑定页状态.TryGetValue(ctrl, state) Then
            state = New BoundPageState()
            _绑定页状态(ctrl) = state
        End If
        Return state
    End Function

    Private Sub 释放绑定控件状态(ctrl As Control)
        If ctrl Is Nothing Then Return
        _绑定页状态.Remove(ctrl)
        If _当前绑定控件 Is ctrl Then _当前绑定控件 = Nothing
    End Sub

    Private Sub 释放所有切页刷新抑制状态()
        _绑定页状态.Clear()
        解除切页背景源订阅()
    End Sub

    Private Sub 清除所有切页刷新抑制()
        For Each state In _绑定页状态.Values
            If state Is Nothing Then Continue For
            state.LastShownBackgroundVersion = -1
        Next
    End Sub

    Private Function 获取切页背景源() As Control
        Return Me.Parent
    End Function

    Private Function 当前切页签名已匹配(ctrl As Control, state As BoundPageState) As Boolean
        If ctrl Is Nothing OrElse state Is Nothing Then Return False
        If Not state.HasBeenShown Then Return False
        If state.LastShownSize <> ctrl.ClientSize Then Return False
        If state.LastShownPanelBounds <> _内容面板.Bounds Then Return False
        If state.LastShownPanelParent IsNot _内容面板.Parent Then Return False
        If state.LastShownBackgroundVersion <> _背景刷新版本 Then Return False
        Return True
    End Function

    Private Function 查找绑定页控件(target As Control) As Control
        If target Is Nothing Then Return Nothing
        If target Is _内容面板 Then Return _当前绑定控件

        Dim current As Control = target
        While current IsNot Nothing AndAlso current IsNot _内容面板
            If current.Parent Is _内容面板 Then Return current
            current = current.Parent
        End While
        Return Nothing
    End Function

    Private Function 正在切页刷新过滤期() As Boolean
        Return _切页刷新过滤启用
    End Function

    Private Function 进入切页刷新过滤() As IDisposable
        Return New SwitchRefreshScope(Me)
    End Function

    Private Sub 延迟结束切页刷新过滤(sequence As Integer)
        If Not Me.IsHandleCreated Then
            _切页刷新过滤深度 = 0
            _切页刷新过滤启用 = False
            Return
        End If
        Try
            Me.BeginInvoke(
                New MethodInvoker(
                    Sub()
                        If sequence <> _切页刷新过滤序号 Then Return
                        If _切页刷新过滤深度 > 0 Then Return
                        _切页刷新过滤启用 = False
                        _切页刷新过滤深度 = 0
                        清除切页强制刷新标记()
                    End Sub))
        Catch
            _切页刷新过滤深度 = 0
            _切页刷新过滤启用 = False
            清除切页强制刷新标记()
        End Try
    End Sub

    Private Sub 清除切页强制刷新标记()
        For Each state In _绑定页状态.Values
            If state IsNot Nothing Then state.ForceRefreshDuringSwitch = False
        Next
    End Sub

    Public Function ShouldSuppressOuterToInnerRefresh(target As Control,
                                                      rect As Rectangle,
                                                      hasFull As Boolean,
                                                      invalidateChildren As Boolean,
                                                      fromChildrenExpansion As Boolean) As Boolean Implements IOuterToInnerRefreshFilter.ShouldSuppressOuterToInnerRefresh
        If Not 正在切页刷新过滤期() Then Return False
        If target Is Nothing OrElse target Is Me Then Return False

        Dim bound = 查找绑定页控件(target)
        If bound Is Nothing Then Return False

        If bound IsNot _当前绑定控件 Then Return True
        If _切页刷新过滤深度 = 0 AndAlso Not fromChildrenExpansion Then Return False
        If Not hasFull AndAlso Not fromChildrenExpansion AndAlso Not invalidateChildren Then Return False

        Dim state As BoundPageState = Nothing
        If Not _绑定页状态.TryGetValue(bound, state) Then Return False
        If state.ForceRefreshDuringSwitch Then Return False
        Return 当前切页签名已匹配(bound, state)
    End Function

    Private Sub 解除切页背景源订阅()
        If _背景源已订阅 Is Nothing Then Return
        Try : RemoveHandler _背景源已订阅.Invalidated, AddressOf 切页背景源已失效 : Catch : End Try
        Try : RemoveHandler _背景源已订阅.Resize, AddressOf 切页背景源已改变 : Catch : End Try
        Try : RemoveHandler _背景源已订阅.Disposed, AddressOf 切页背景源已释放 : Catch : End Try
        _背景源已订阅 = Nothing
        推进切页背景版本()
    End Sub

    Private Sub 同步切页背景源订阅(Optional source As Control = Nothing)
        Dim newSource As Control = If(source, 获取切页背景源())
        If _背景源已订阅 Is newSource Then Return
        解除切页背景源订阅()
        _背景源已订阅 = newSource
        If _背景源已订阅 IsNot Nothing Then
            Try : AddHandler _背景源已订阅.Invalidated, AddressOf 切页背景源已失效 : Catch : End Try
            Try : AddHandler _背景源已订阅.Resize, AddressOf 切页背景源已改变 : Catch : End Try
            Try : AddHandler _背景源已订阅.Disposed, AddressOf 切页背景源已释放 : Catch : End Try
        End If
        推进切页背景版本()
    End Sub

    Private Sub 推进切页背景版本()
        _背景刷新版本 += 1
    End Sub

    Private Function 切页背景脏区影响内容区(source As Control, dirtyRect As Rectangle) As Boolean
        If source Is Nothing Then Return True
        If dirtyRect.Width <= 0 OrElse dirtyRect.Height <= 0 Then Return True
        If _内容面板 Is Nothing OrElse _内容面板.IsDisposed Then Return True
        If _内容面板.Width <= 0 OrElse _内容面板.Height <= 0 Then Return False

        Try
            Dim origin As Point = source.PointToClient(_内容面板.PointToScreen(Point.Empty))
            Dim contentRectInSource As New Rectangle(origin, _内容面板.ClientSize)
            Return contentRectInSource.IntersectsWith(dirtyRect)
        Catch
            Return True
        End Try
    End Function

    Private Sub 切页背景源已失效(sender As Object, e As InvalidateEventArgs)
        Dim source = TryCast(sender, Control)
        Dim dirtyRect As Rectangle = If(e IsNot Nothing, e.InvalidRect, Rectangle.Empty)
        If Not 切页背景脏区影响内容区(source, dirtyRect) Then Return
        推进切页背景版本()
    End Sub

    Private Sub 切页背景源已改变(sender As Object, e As EventArgs)
        推进切页背景版本()
    End Sub

    Private Sub 切页背景源已释放(sender As Object, e As EventArgs)
        同步切页背景源订阅()
    End Sub

    Private Function 内容面板应显示() As Boolean
        If Ribbon模式值 AndAlso 已折叠值 Then Return _浮层显示
        Return _内容面板.Width > 0 AndAlso _内容面板.Height > 0
    End Function

    Private Sub 隐藏绑定控件(ctrl As Control)
        If ctrl Is Nothing Then Return
        解除绑定页背景穿透消费者(ctrl)
        Try
            ctrl.Visible = False
        Catch
        End Try
    End Sub

    Private Sub 移除绑定控件(ctrl As Control)
        If ctrl Is Nothing Then Return
        解除绑定页背景穿透消费者(ctrl)
        Try
            ctrl.Visible = False
            If ctrl.Parent Is _内容面板 Then
                _内容面板.Controls.Remove(ctrl)
            End If
        Catch
        End Try
    End Sub

    Private Sub 准备窗体绑定(frm As Form)
        If frm Is Nothing Then Return
        If frm.TopLevel Then
            frm.TopLevel = False
            frm.FormBorderStyle = FormBorderStyle.None
        End If
    End Sub

    Private Sub 解除绑定页背景穿透消费者(root As Control)
        If root Is Nothing Then Return
        Try : D3D_BackgroundPenetration.UnregisterConsumer(root) : Catch : End Try
        Try
            For Each child As Control In root.Controls
                解除绑定页背景穿透消费者(child)
            Next
        Catch
        End Try
    End Sub

    Private Sub 准备绑定页渲染边界(ctrl As Control)
        If ctrl Is Nothing OrElse ctrl.IsDisposed Then Return
        Try : _内容面板.PerformLayout() : Catch : End Try
        Try : ctrl.PerformLayout() : Catch : End Try
        Try : _内容面板.CreateControl() : Catch : End Try
        Try : ctrl.CreateControl() : Catch : End Try
    End Sub

    Private Sub 提交绑定页切换首帧()
        If Me.IsDisposed Then Return
        请求V3渲染()
        If _内容面板 IsNot Nothing AndAlso Not _内容面板.IsDisposed AndAlso _内容面板.Width > 0 AndAlso _内容面板.Height > 0 Then
            V3_InvalidationRouter.RequestRender(_内容面板, New Rectangle(Point.Empty, _内容面板.Size))
        End If
        If _当前绑定控件 IsNot Nothing AndAlso Not _当前绑定控件.IsDisposed Then
            请求绑定页V3渲染(_当前绑定控件)
        End If
    End Sub

    Private Sub 显示绑定控件(ctrl As Control)
        If ctrl Is Nothing Then Return
        Dim frm = TryCast(ctrl, Form)
        If frm IsNot Nothing Then
            准备窗体绑定(frm)
        End If
        _当前绑定控件 = ctrl

        Dim parentChanged As Boolean = ctrl.Parent IsNot _内容面板
        If parentChanged Then
            _内容面板.Controls.Add(ctrl)
        End If
        Dim dockChanged As Boolean = ctrl.Dock <> DockStyle.Fill
        If dockChanged Then ctrl.Dock = DockStyle.Fill
        Dim state = 获取绑定页状态(ctrl)
        Dim formFirstShow As Boolean = frm IsNot Nothing AndAlso (state Is Nothing OrElse Not state.HasBeenShown)
        If formFirstShow AndAlso ctrl.Visible Then
            Try : ctrl.Visible = False : Catch : End Try
        End If

        Dim panelWasVisible As Boolean = _内容面板.Visible
        Dim desiredPanelVisible As Boolean = 内容面板应显示()
        ctrl.Visible = True
        If _内容面板.Visible <> desiredPanelVisible Then _内容面板.Visible = desiredPanelVisible
        If desiredPanelVisible AndAlso _内容面板.Controls.GetChildIndex(ctrl) <> 0 Then ctrl.BringToFront()
        If desiredPanelVisible Then 准备绑定页渲染边界(ctrl)

        Dim backgroundChanged As Boolean = state Is Nothing OrElse
                                           Not state.HasBeenShown OrElse
                                           state.LastShownBackgroundVersion <> _背景刷新版本
        Dim panelChanged As Boolean = state Is Nothing OrElse
                                      state.LastShownPanelBounds <> _内容面板.Bounds OrElse
                                      state.LastShownPanelParent IsNot _内容面板.Parent
        Dim sizeChanged As Boolean = state Is Nothing OrElse state.LastShownSize <> ctrl.ClientSize
        Dim needsRefresh As Boolean = desiredPanelVisible AndAlso
                                      (Not _切换页抑制刷新 OrElse
                                       parentChanged OrElse
                                       dockChanged OrElse
                                       Not panelWasVisible OrElse
                                       backgroundChanged OrElse
                                       panelChanged OrElse
                                      sizeChanged)
        If state IsNot Nothing Then state.ForceRefreshDuringSwitch = needsRefresh AndAlso 正在切页刷新过滤期()
        If desiredPanelVisible Then 请求绑定页V3渲染(ctrl)
        If desiredPanelVisible Then 提交绑定页切换首帧()
        If state IsNot Nothing AndAlso desiredPanelVisible Then
            state.HasBeenShown = True
            state.LastShownSize = ctrl.ClientSize
            state.LastShownBackgroundVersion = _背景刷新版本
            state.LastShownPanelBounds = _内容面板.Bounds
            state.LastShownPanelParent = _内容面板.Parent
        End If
    End Sub
#End Region

#Region "选中管理"
    Private _selectedIndex As Integer = -1

    Public Event SelectedIndexChanged As EventHandler

    <Category("LakeUI"), Description("当前选中项的索引"), DefaultValue(-1), Browsable(True)>
    Public Property SelectedIndex As Integer
        Get
            Return _selectedIndex
        End Get
        Set(value As Integer)
            If value < -1 Then value = -1
            If value >= 项目列表.Count Then value = 项目列表.Count - 1
            If value >= 0 Then
                Dim item = 项目列表(value)
                If item.IsSeparator Then Return
            End If
            If _selectedIndex <> value Then
                Dim oldIndex As Integer = _selectedIndex
                Dim oldScroll As Integer = _滚动偏移
                _selectedIndex = value
                Using 进入切页刷新过滤()
                    确保选中项可见()
                    切换绑定控件()
                End Using
                If _滚动偏移 <> oldScroll Then
                    请求V3渲染(获取标签栏矩形())
                Else
                    请求选中项切换重绘(oldIndex, _selectedIndex)
                End If
                RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
            End If
        End Set
    End Property

    <Category("LakeUI"), Description("当前选中的选项卡，为 Nothing 时表示未选中任何项"), DefaultValue(GetType(ModernTab), Nothing), Browsable(False)>
    Public Property SelectedTab As ModernTab
        Get
            If _selectedIndex >= 0 AndAlso _selectedIndex < 项目列表.Count Then
                Return 项目列表(_selectedIndex)
            End If
            Return Nothing
        End Get
        Set(value As ModernTab)
            If value Is Nothing Then
                SelectedIndex = -1
            Else
                Dim idx = 项目列表.IndexOf(value)
                If idx >= 0 Then
                    SelectedIndex = idx
                End If
            End If
        End Set
    End Property

    Private Function 上一个可选索引(fromIndex As Integer) As Integer
        For i As Integer = fromIndex - 1 To 0 Step -1
            If Not 项目列表(i).IsSeparator Then Return i
        Next
        Return fromIndex
    End Function

    Private Function 下一个可选索引(fromIndex As Integer) As Integer
        For i As Integer = fromIndex + 1 To 项目列表.Count - 1
            If Not 项目列表(i).IsSeparator Then Return i
        Next
        Return fromIndex
    End Function

    Private Function 第一个可选索引() As Integer
        For i As Integer = 0 To 项目列表.Count - 1
            If Not 项目列表(i).IsSeparator Then Return i
        Next
        Return -1
    End Function

    Private Function 最后一个可选索引() As Integer
        For i As Integer = 项目列表.Count - 1 To 0 Step -1
            If Not 项目列表(i).IsSeparator Then Return i
        Next
        Return -1
    End Function

    Private Sub 切换绑定控件()
        Dim nextControl = 获取索引绑定控件(_selectedIndex)
        If _当前绑定控件 Is nextControl Then
            If nextControl IsNot Nothing Then
                Dim desiredPanelVisible As Boolean = 内容面板应显示()
                Dim state As BoundPageState = Nothing
                _绑定页状态.TryGetValue(nextControl, state)
                If nextControl.Parent IsNot _内容面板 OrElse
                   Not nextControl.Visible OrElse
                   _内容面板.Visible <> desiredPanelVisible OrElse
                   (desiredPanelVisible AndAlso Not 当前切页签名已匹配(nextControl, state)) Then
                    显示绑定控件(nextControl)
                Else
                    If desiredPanelVisible AndAlso _内容面板.Controls.GetChildIndex(nextControl) <> 0 Then nextControl.BringToFront()
                End If
            Else
                _内容面板.Visible = False
            End If
            Return
        End If

        If nextControl Is Nothing Then
            _内容面板.Visible = False
            _当前绑定控件 = Nothing
            Return
        End If

        If _当前绑定控件 IsNot Nothing Then 隐藏绑定控件(_当前绑定控件)
        For Each item In 项目列表
            Dim bound = If(item IsNot Nothing, item.BoundControl, Nothing)
            If bound IsNot Nothing AndAlso bound IsNot nextControl AndAlso bound.Parent Is _内容面板 AndAlso bound.Visible Then
                隐藏绑定控件(bound)
            End If
        Next
        ' 已访问页面驻留在内容面板中；非当前页隐藏，避免切页唤醒历史页面和嵌套选项卡整棵子树。
        显示绑定控件(nextControl)
    End Sub

    Private Function 获取标签页项脏区(index As Integer) As Rectangle
        If index < 0 OrElse index >= 项目列表.Count Then Return Rectangle.Empty
        Dim itemRect = 获取标签页项矩形(index)
        If itemRect.Width <= 0 OrElse itemRect.Height <= 0 Then Return Rectangle.Empty

        Dim stripRect = 获取标签栏矩形()
        Dim s As Single = DpiScale()
        Dim dirty = Rectangle.Ceiling(itemRect)
        dirty.Inflate(CInt(Math.Ceiling(2.0F * s)), CInt(Math.Ceiling(2.0F * s)))
        Return Rectangle.Intersect(stripRect, dirty)
    End Function

    Private Sub 请求选中项切换重绘(oldIndex As Integer, newIndex As Integer)
        Dim stripRect = 获取标签栏矩形()
        If stripRect.Width <= 0 OrElse stripRect.Height <= 0 Then Return

        Dim dirty As Rectangle = Rectangle.Empty
        Dim oldDirty = 获取标签页项脏区(oldIndex)
        If oldDirty.Width > 0 AndAlso oldDirty.Height > 0 Then dirty = oldDirty
        Dim newDirty = 获取标签页项脏区(newIndex)
        If newDirty.Width > 0 AndAlso newDirty.Height > 0 Then
            dirty = If(dirty.IsEmpty, newDirty, Rectangle.Union(dirty, newDirty))
        End If

        Dim collapseDirty = Rectangle.Ceiling(获取折叠按钮矩形())
        If collapseDirty.Width > 0 AndAlso collapseDirty.Height > 0 Then
            dirty = If(dirty.IsEmpty, collapseDirty, Rectangle.Union(dirty, collapseDirty))
        End If
        If Not _滚动条TrackRect.IsEmpty Then
            dirty = If(dirty.IsEmpty, _滚动条TrackRect, Rectangle.Union(dirty, _滚动条TrackRect))
        End If

        If dirty.IsEmpty Then dirty = stripRect
        dirty = Rectangle.Intersect(stripRect, dirty)
        If dirty.Width > 0 AndAlso dirty.Height > 0 Then
            请求V3渲染(dirty)
        End If
    End Sub
#End Region

#Region "绘制"
    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' V3-only: pixels are emitted by RenderGpu.
    End Sub

    Private Function 获取控件背景源() As Control
        If MyBase.BackColor.A < 255 Then Return Me.Parent
        Return Nothing
    End Function

    Private Function 获取内容面板有效背景色() As Color
        ' Panel 不支持完全透明的 BackColor 显示，需要使用 Transparent 触发透明背景路径。
        If 内容区域背景颜色.A < 255 Then Return Color.Transparent
        Return 内容区域背景颜色
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        确保Owner()
        If Me.Width <= 0 OrElse Me.Height <= 0 Then Return

        Dim backgroundSource = If(_backgroundSource, 获取控件背景源())
        If backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(Me, backgroundSource, New RectangleF(0, 0, Me.Width, Me.Height))
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), MyBase.BackColor)
        End If

        绘制图形内容_GPU(context)
        绘制文本内容_GPU(context)
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Friend Sub 请求V3渲染(Optional immediate As Boolean = False)
        请求V3渲染(New Rectangle(Point.Empty, Me.Size), immediate)
    End Sub

    Friend Sub 请求V3渲染(dirtyRect As Rectangle, Optional immediate As Boolean = False)
        If Me.IsDisposed Then Return
        V3_InvalidationRouter.RequestRender(Me, dirtyRect)
    End Sub

    Private Sub 请求绑定页V3渲染(ctrl As Control)
        If ctrl Is Nothing OrElse ctrl.IsDisposed Then Return
        V3_InvalidationRouter.RequestRender(ctrl, New Rectangle(Point.Empty, ctrl.Size))
    End Sub

    Private Sub 绘制图形内容_GPU(context As D3D_PaintContext)
        Dim contentRect = 获取内容区域矩形()
        Dim stripRect = 获取标签栏矩形()

        If Not contentRect.IsEmpty AndAlso 内容区域背景颜色.A = 255 Then
            context.FillRectangle(contentRect, 内容区域背景颜色)
        End If
        If 标签栏背景颜色.A > 0 Then
            context.FillRectangle(stripRect, 标签栏背景颜色)
        End If

        绘制标签栏背景图片_GPU(context, stripRect)

        If 标签栏遮罩颜色.A > 0 Then
            context.FillRectangle(stripRect, 标签栏遮罩颜色)
        End If

        Using context.PushClip(stripRect)
            For i As Integer = 0 To 项目列表.Count - 1
                Dim item = 项目列表(i)
                If item.IsSeparator Then
                    绘制分割线_GPU(context, i)
                Else
                    绘制标签页项图形_GPU(context, i)
                End If
            Next

            If Ribbon模式值 Then
                绘制折叠按钮图形_GPU(context)
            End If
        End Using

        更新滚动条布局()
        If Not _滚动条TrackRect.IsEmpty Then
            绘制横向滚动条_GPU(context)
        End If

        If 内容区域边框宽度 > 0 AndAlso Not contentRect.IsEmpty Then
            Dim s As Single = DpiScale()
            Dim borderRect As RectangleF = contentRect
            borderRect.Width -= 1
            borderRect.Height -= 1
            绘制圆角边框_GPU(context, borderRect, 0.0F, 内容区域边框颜色, 内容区域边框宽度 * s)
        End If
    End Sub

    Private Sub 绘制标签栏背景图片_GPU(context As D3D_PaintContext, tabStripRect As Rectangle)
        If 标签栏背景图片 Is Nothing Then Return
        Dim img As Image = 标签栏背景图片
        Dim cw As Integer = tabStripRect.Width
        Dim ch As Integer = tabStripRect.Height
        If cw < 1 OrElse ch < 1 Then Return

        Dim ratioW As Single = CSng(cw) / img.Width
        Dim ratioH As Single = CSng(ch) / img.Height
        Dim ratio As Single = Math.Max(ratioW, ratioH)
        Dim drawW As Single = img.Width * ratio
        Dim drawH As Single = img.Height * ratio
        Dim dx As Single = tabStripRect.X + (cw - drawW) / 2.0F
        Dim dy As Single = tabStripRect.Y + (ch - drawH) / 2.0F

        Using context.PushClip(tabStripRect)
            context.DrawImage(img, New RectangleF(dx, dy, drawW, drawH))
        End Using
    End Sub

    Private Sub 绘制分割线_GPU(context As D3D_PaintContext, index As Integer)
        Dim bounds = 获取标签页项矩形(index)
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim lineX As Single = bounds.X + (bounds.Width - 1) / 2.0F
        context.FillRectangle(New RectangleF(lineX, bounds.Y, 1, bounds.Height), 分割线颜色值)
    End Sub

    Private Sub 绘制标签页项图形_GPU(context As D3D_PaintContext, index As Integer)
        Dim s As Single = DpiScale()
        Dim bounds As RectangleF = 获取标签页项矩形(index)
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim isSelected As Boolean = (_selectedIndex = index)
        Dim hoverProgress As Single = 获取动画进度(index)

        Dim bgColor As Color
        If isSelected Then
            bgColor = 选中标签页背景颜色
        Else
            bgColor = 颜色插值(Color.FromArgb(0, 悬停标签页背景颜色), 悬停标签页背景颜色, hoverProgress)
        End If
        Dim radius As Single = 标签页圆角半径 * s
        If isSelected OrElse hoverProgress > 0.001F Then
            填充圆角或矩形_GPU(context, bounds, radius, bgColor)
        End If

        If isSelected AndAlso 选中指示条高度 > 0 Then
            Dim indicatorH As Single = 选中指示条高度 * s
            Dim indicatorPad As Single = 选中指示条边距 * s
            Dim indicatorRadius As Single = 选中指示条圆角半径 * s
            Dim indicatorRect As RectangleF
            If 标签页位置 = TabPositionEnum.Top Then
                indicatorRect = New RectangleF(bounds.X + indicatorPad, bounds.Bottom - indicatorH, bounds.Width - indicatorPad * 2, indicatorH)
            Else
                indicatorRect = New RectangleF(bounds.X + indicatorPad, bounds.Y, bounds.Width - indicatorPad * 2, indicatorH)
            End If
            If indicatorRect.Width > 0 AndAlso indicatorRect.Height > 0 Then
                填充圆角或矩形_GPU(context, indicatorRect, indicatorRadius, 选中指示条颜色)
            End If
        End If

        If isSelected AndAlso Me.Focused AndAlso 焦点边框颜色 <> Color.Empty Then
            Dim focusBounds = bounds
            focusBounds.Inflate(-1 * s, -1 * s)
            If focusBounds.Width > 0 AndAlso focusBounds.Height > 0 Then
                绘制圆角边框_GPU(context, focusBounds, If(radius > 0, Math.Max(1, radius - 1 * s), 0.0F), 焦点边框颜色, 1.0F * s)
            End If
        End If

        绘制标签页图标_GPU(context, index, bounds)
    End Sub

    Private Sub 填充圆角或矩形_GPU(context As D3D_PaintContext, rect As RectangleF, radius As Single, color As Color)
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

    Private Sub 绘制标签页图标_GPU(context As D3D_PaintContext, index As Integer, bounds As RectangleF)
        If index >= 项目列表.Count Then Return
        Dim item = 项目列表(index)
        绘制图标_GPU(context, item.TabIcon, bounds)
    End Sub

    Private Sub 绘制图标_GPU(context As D3D_PaintContext, img As Image, bounds As RectangleF)
        If img Is Nothing Then Return
        Dim s As Single = DpiScale()
        Dim iconSize As Single = 图标尺寸 * s
        Dim pad As Single = 标签页文本内边距 * s
        Dim iconRect As New RectangleF(bounds.X + pad, bounds.Y + (bounds.Height - iconSize) / 2.0F, iconSize, iconSize)
        context.DrawImage(img, iconRect, New RectangleF(0, 0, img.Width, img.Height))
    End Sub

    Private Sub 绘制折叠按钮图形_GPU(context As D3D_PaintContext)
        Dim bounds = 获取折叠按钮矩形()
        If bounds.IsEmpty Then Return
        _折叠按钮缓存矩形 = bounds
        Dim s As Single = DpiScale()
        Dim bgColor As Color = 颜色插值(Color.FromArgb(0, 悬停标签页背景颜色), 悬停标签页背景颜色, _折叠按钮动画.当前值)
        If _折叠按钮动画.当前值 > 0.001F Then
            填充圆角或矩形_GPU(context, bounds, 标签页圆角半径 * s, bgColor)
        End If
        绘制图标_GPU(context, 折叠按钮图标值, bounds)
    End Sub

    Private Sub 绘制横向滚动条_GPU(context As D3D_PaintContext)
        If _滚动条TrackRect.IsEmpty Then Return
        Dim s As Single = DpiScale()
        Dim barH As Integer = CInt(滚动条高度 * s)
        If _滚动条TrackRect.Width < 1 OrElse barH < 1 Then Return

        Dim trackY As Integer = _滚动条TrackRect.Y + (_滚动条TrackRect.Height - barH) \ 2
        If 滚动条轨道颜色.A > 0 Then
            Dim trackRadius As Integer = Math.Min(barH \ 2, _滚动条TrackRect.Width \ 2)
            填充圆角或矩形_GPU(context, New RectangleF(_滚动条TrackRect.X, trackY, _滚动条TrackRect.Width, barH), trackRadius, 滚动条轨道颜色)
        End If

        Dim activeColor As Color = If(_滚动条IsDragging OrElse _滚动条IsHover, 滚动条悬停颜色, 滚动条滑块颜色)
        Dim thumbY As Integer = _滚动条ThumbRect.Y + (_滚动条ThumbRect.Height - barH) \ 2
        Dim thumbRadius As Integer = Math.Min(barH \ 2, _滚动条ThumbRect.Width \ 2)
        填充圆角或矩形_GPU(context, New RectangleF(_滚动条ThumbRect.X, thumbY, _滚动条ThumbRect.Width, barH), thumbRadius, activeColor)
    End Sub

    Private Sub 绘制文本内容_GPU(context As D3D_PaintContext)
        Dim stripRect = 获取标签栏矩形()
        Using context.PushClip(stripRect)
            For i As Integer = 0 To 项目列表.Count - 1
                绘制标签页文本_GPU(context, i)
            Next
            If Ribbon模式值 Then
                绘制折叠按钮文本_GPU(context)
            End If
        End Using
    End Sub

    Private Sub 绘制标签页文本_GPU(context As D3D_PaintContext, index As Integer)
        If index >= 项目列表.Count Then Return
        Dim bounds As RectangleF = 获取标签页项矩形(index)
        Dim item = 项目列表(index)
        If item.IsSeparator OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim isSelected As Boolean = (_selectedIndex = index)
        Dim textColor As Color = If(isSelected,
            If(item.SelectedForeColor <> Color.Empty, item.SelectedForeColor, 选中标签页文本颜色),
            If(item.NormalForeColor <> Color.Empty, item.NormalForeColor, 标签页默认文本颜色))
        Dim textFont As Font = If(item.TabFont, Me.Font)

        Dim s As Single = DpiScale()
        Dim pad As Single = 标签页文本内边距 * s
        Dim iconOffset As Single = If(item.TabIcon IsNot Nothing, 图标尺寸 * s + 图标与文本间距 * s, 0)
        Dim textRect As New RectangleF(bounds.X + pad + iconOffset, bounds.Y, bounds.Width - pad * 2 - iconOffset, bounds.Height)
        绘制单行居中文本_GPU(context, item.Text, textFont, textRect, textColor)
    End Sub

    Private Sub 绘制折叠按钮文本_GPU(context As D3D_PaintContext)
        Dim bounds As RectangleF = 获取折叠按钮矩形()
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim caption As String = If(已折叠值, 折叠按钮展开文本值, 折叠按钮折叠文本值)
        If String.IsNullOrEmpty(caption) AndAlso 折叠按钮图标值 Is Nothing Then Return
        Dim textColor As Color = If(已折叠值,
            If(折叠按钮选中文本颜色值 <> Color.Empty, 折叠按钮选中文本颜色值, 选中标签页文本颜色),
            If(折叠按钮文本颜色值 <> Color.Empty, 折叠按钮文本颜色值, 标签页默认文本颜色))
        Dim textFont As Font = If(折叠按钮字体值, Me.Font)

        Dim s As Single = DpiScale()
        Dim pad As Single = 标签页文本内边距 * s
        Dim iconOffset As Single = If(折叠按钮图标值 IsNot Nothing, 图标尺寸 * s + 图标与文本间距 * s, 0)
        Dim textRect As New RectangleF(bounds.X + pad + iconOffset, bounds.Y, bounds.Width - pad * 2 - iconOffset, bounds.Height)
        绘制单行居中文本_GPU(context, caption, textFont, textRect, textColor)
    End Sub

    Private Sub 绘制单行居中文本_GPU(context As D3D_PaintContext, text As String, font As Font, rect As RectangleF, color As Color)
        If String.IsNullOrEmpty(text) OrElse rect.Width <= 0 OrElse rect.Height <= 0 OrElse color.A = 0 Then Return
        context.DrawText(text, font, color, rect, TextAlignment.Center, ParagraphAlignment.Center)
    End Sub

#End Region

#Region "布局"
    Private Function 获取标签栏矩形() As Rectangle
        Dim s As Single = DpiScale()
        Dim h As Integer = CInt(标签栏高度 * s)
        If 标签页位置 = TabPositionEnum.Top Then
            Return New Rectangle(0, 0, Me.Width, h)
        Else
            Return New Rectangle(0, Me.Height - h, Me.Width, h)
        End If
    End Function

    Private Function 获取内容区域矩形() As Rectangle
        Dim s As Single = DpiScale()
        Dim h As Integer = CInt(标签栏高度 * s)
        If Ribbon模式值 AndAlso 已折叠值 Then
            ' 折叠时控件本体只占标签栏，无内部内容区
            Return Rectangle.Empty
        End If
        Dim contentH As Integer = Math.Max(0, Me.Height - h)
        If 标签页位置 = TabPositionEnum.Top Then
            Return New Rectangle(0, h, Me.Width, contentH)
        Else
            Return New Rectangle(0, Me.Height - h - contentH, Me.Width, contentH)
        End If
    End Function

    Private Function 获取标签页项矩形(index As Integer) As RectangleF
        Dim s As Single = DpiScale()
        Dim _标签栏内边距 As New Padding(
            CInt(标签栏内边距.Left * s), CInt(标签栏内边距.Top * s),
            CInt(标签栏内边距.Right * s), CInt(标签栏内边距.Bottom * s))
        Dim _标签页项间距 As Single = 标签页项间距 * s
        Dim stripRect = 获取标签栏矩形()
        Dim fullH As Single = stripRect.Height - _标签栏内边距.Top - _标签栏内边距.Bottom
        Dim y As Single = stripRect.Y + _标签栏内边距.Top
        Dim h As Single = fullH
        Dim 折叠按钮宽 As Single = 计算折叠按钮宽度()
        Dim 左侧让出 As Single = 0
        Dim 右侧让出 As Single = 0
        If Ribbon模式值 AndAlso 折叠按钮宽 > 0 Then
            If 折叠按钮对齐值 = CollapseButtonAlignmentEnum.Left Then
                左侧让出 = 折叠按钮宽 + _标签页项间距
            Else
                右侧让出 = 折叠按钮宽 + _标签页项间距
            End If
        End If
        Dim availableWidth As Single = stripRect.Width - _标签栏内边距.Left - _标签栏内边距.Right - 左侧让出 - 右侧让出

        Dim widths = 计算所有标签页宽度(availableWidth)
        If widths.Length = 0 Then Return RectangleF.Empty

        Dim totalWidth As Single = 0
        For Each w In widths
            totalWidth += w
        Next
        If widths.Length > 1 Then totalWidth += _标签页项间距 * (widths.Length - 1)

        Dim alignOffset As Single = 0
        If 标签页尺寸模式 = TabSizingEnum.AutoWidth AndAlso totalWidth < availableWidth Then
            Select Case 标签页对齐方式
                Case TabAlignmentEnum.Center
                    alignOffset = (availableWidth - totalWidth) / 2.0F
                Case TabAlignmentEnum.Right
                    alignOffset = availableWidth - totalWidth
                Case TabAlignmentEnum.TopCenter
                    alignOffset = (availableWidth - totalWidth) / 2.0F
                    Dim compactH As Single = Math.Min(fullH, Math.Max(Me.Font.Height + 6 * s, fullH * 0.7F))
                    h = compactH
                Case TabAlignmentEnum.BottomCenter
                    alignOffset = (availableWidth - totalWidth) / 2.0F
                    Dim compactH As Single = Math.Min(fullH, Math.Max(Me.Font.Height + 6 * s, fullH * 0.7F))
                    h = compactH
                    y = y + fullH - compactH
            End Select
        End If

        Dim x As Single = _标签栏内边距.Left + 左侧让出 + alignOffset - _滚动偏移
        For i As Integer = 0 To index - 1
            x += widths(i) + _标签页项间距
        Next
        Return New RectangleF(x, y, widths(index), h)
    End Function

    Private Function 计算所有标签页宽度(availableWidth As Single) As Single()
        If _缓存宽度 IsNot Nothing AndAlso _缓存可用宽度 = availableWidth Then Return _缓存宽度
        If 项目列表.Count = 0 Then Return Array.Empty(Of Single)()
        Dim s As Single = DpiScale()
        Dim _标签页项间距 As Single = 标签页项间距 * s
        Dim _分割线宽度值 As Single = 分割线宽度值 * s
        Dim _标签页最小宽度 As Single = 标签页最小宽度 * s
        Dim _标签页文本内边距 As Single = 标签页文本内边距 * s
        Dim _图标尺寸 As Single = 图标尺寸 * s
        Dim _图标与文本间距 As Single = 图标与文本间距 * s
        Dim widths(项目列表.Count - 1) As Single

        If 标签页尺寸模式 = TabSizingEnum.EqualWidth Then
            Dim separatorCount As Integer = 0
            Dim separatorTotalW As Single = 0
            For i As Integer = 0 To 项目列表.Count - 1
                If 项目列表(i).IsSeparator Then
                    separatorCount += 1
                    separatorTotalW += _分割线宽度值
                End If
            Next
            Dim normalCount As Integer = 项目列表.Count - separatorCount
            Dim spacingTotal As Single = _标签页项间距 * Math.Max(0, 项目列表.Count - 1)
            Dim perItem As Single = If(normalCount > 0, (availableWidth - spacingTotal - separatorTotalW) / normalCount, _标签页最小宽度)
            Dim effectiveWidth As Single = Math.Max(_标签页最小宽度, perItem)
            For i As Integer = 0 To 项目列表.Count - 1
                If 项目列表(i).IsSeparator Then
                    widths(i) = _分割线宽度值
                Else
                    widths(i) = effectiveWidth
                End If
            Next
        Else
            For i As Integer = 0 To 项目列表.Count - 1
                Dim item = 项目列表(i)
                If item.IsSeparator Then
                    widths(i) = _分割线宽度值
                Else
                    Dim font = If(item.TabFont, Me.Font)
                    Dim textW = TextRenderer.MeasureText(item.Text, font, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.NoPadding).Width
                    Dim iconW As Single = If(item.TabIcon IsNot Nothing, _图标尺寸 + _图标与文本间距, 0)
                    widths(i) = Math.Max(_标签页最小宽度, textW + iconW + _标签页文本内边距 * 2)
                End If
            Next
        End If

        _缓存宽度 = widths
        _缓存可用宽度 = availableWidth
        Return widths
    End Function

    Private Function 获取标签页总宽度() As Single
        If 项目列表.Count = 0 Then Return 0
        Dim s As Single = DpiScale()
        Dim _标签页项间距 As Single = 标签页项间距 * s
        Dim stripRect = 获取标签栏矩形()
        Dim _标签栏内边距L As Single = 标签栏内边距.Left * s
        Dim _标签栏内边距R As Single = 标签栏内边距.Right * s
        Dim 折叠按钮宽 As Single = 计算折叠按钮宽度()
        Dim 折叠按钮空间 As Single = If(Ribbon模式值 AndAlso 折叠按钮宽 > 0, 折叠按钮宽 + _标签页项间距, 0)
        Dim availableWidth As Single = stripRect.Width - _标签栏内边距L - _标签栏内边距R - 折叠按钮空间
        Dim widths = 计算所有标签页宽度(availableWidth)
        Dim total As Single = _标签栏内边距L + 折叠按钮空间
        For i As Integer = 0 To widths.Length - 1
            total += widths(i)
            If i < widths.Length - 1 Then total += _标签页项间距
        Next
        total += _标签栏内边距R
        Return total
    End Function

    Private Sub 同步内容面板布局()
        Dim oldParent As Control = _内容面板.Parent
        Dim oldBounds As Rectangle = _内容面板.Bounds
        Dim oldVisible As Boolean = _内容面板.Visible

        If Ribbon模式值 Then
            Dim s As Single = DpiScale()
            Dim 标签栏H As Integer = CInt(标签栏高度 * s)
            Dim 内容H As Integer = CInt(Ribbon内容高度值 * s)

            If 已折叠值 Then
                ' 折叠：控件本体只占标签栏；内容面板浮层化于 Parent 上
                If Me.Height <> 标签栏H Then Me.Height = 标签栏H
                Dim hostParent As Control = Me.Parent
                If hostParent Is Nothing Then
                    _内容面板.Visible = False
                    If oldParent IsNot _内容面板.Parent OrElse oldBounds <> _内容面板.Bounds OrElse oldVisible <> _内容面板.Visible Then
                        推进切页背景版本()
                    End If
                    Return
                End If
                If _内容面板.Parent IsNot hostParent Then
                    If _内容面板.Parent IsNot Nothing Then _内容面板.Parent.Controls.Remove(_内容面板)
                    hostParent.Controls.Add(_内容面板)
                End If
                Dim x As Integer = Me.Left
                Dim w As Integer = Me.Width
                Dim y As Integer = If(标签页位置 = TabPositionEnum.Top, Me.Bottom, Me.Top - 内容H)
                _内容面板.Bounds = New Rectangle(x, y, w, 内容H)
                _内容面板.Visible = _浮层显示
                If _浮层显示 Then _内容面板.BringToFront()
            Else
                ' 固定（Pinned）：内容区常驻于本控件内部，控件占完整高度
                If _内容面板.Parent IsNot Me Then
                    If _内容面板.Parent IsNot Nothing Then _内容面板.Parent.Controls.Remove(_内容面板)
                    Me.Controls.Add(_内容面板)
                End If
                Dim 期望H As Integer = 标签栏H + 内容H
                If Me.Height <> 期望H Then Me.Height = 期望H
                Dim y As Integer = If(标签页位置 = TabPositionEnum.Top, 标签栏H, 0)
                _内容面板.Bounds = New Rectangle(0, y, Me.Width, 内容H)
                _内容面板.Visible = True
            End If
            _内容面板.BackColor = 获取内容面板有效背景色()
            更新鼠标过滤器()
        Else
            停用鼠标过滤器()
            If _内容面板.Parent IsNot Me Then
                If _内容面板.Parent IsNot Nothing Then _内容面板.Parent.Controls.Remove(_内容面板)
                Me.Controls.Add(_内容面板)
            End If
            Dim contentRect = 获取内容区域矩形()
            _内容面板.Bounds = contentRect
            _内容面板.BackColor = 获取内容面板有效背景色()
            _内容面板.Visible = (contentRect.Height > 0)
        End If

        If oldParent IsNot _内容面板.Parent OrElse oldBounds <> _内容面板.Bounds OrElse oldVisible <> _内容面板.Visible Then
            推进切页背景版本()
            If _当前绑定控件 IsNot Nothing AndAlso _当前绑定控件.Parent Is _内容面板 Then
                显示绑定控件(_当前绑定控件)
            End If
        End If
    End Sub

    Private Sub 更新鼠标过滤器()
        Dim 应当启用 As Boolean = Ribbon模式值 AndAlso 已折叠值 AndAlso _浮层显示
        If 应当启用 Then
            If _鼠标过滤器 Is Nothing Then
                _鼠标过滤器 = New RibbonMouseFilter(Me)
                Application.AddMessageFilter(_鼠标过滤器)
            End If
        Else
            停用鼠标过滤器()
        End If
    End Sub

    Friend Sub 停用鼠标过滤器()
        If _鼠标过滤器 IsNot Nothing Then
            Application.RemoveMessageFilter(_鼠标过滤器)
            _鼠标过滤器 = Nothing
        End If
    End Sub

    Friend Sub 检查浮层是否需要关闭(isClick As Boolean)
        If Not (Ribbon模式值 AndAlso 已折叠值 AndAlso _浮层显示) Then Return
        Dim mp As Point = Cursor.Position
        Dim inSelf As Boolean = Me.IsHandleCreated AndAlso Me.RectangleToScreen(Me.ClientRectangle).Contains(mp)
        Dim inPanel As Boolean = _内容面板.Visible AndAlso _内容面板.IsHandleCreated AndAlso _内容面板.RectangleToScreen(_内容面板.ClientRectangle).Contains(mp)
        If inSelf OrElse inPanel Then Return
        ' 鼠标在控件与浮层之外
        关闭浮层()
    End Sub

    Private Sub 显示浮层()
        If Not (Ribbon模式值 AndAlso 已折叠值) Then Return
        If _浮层显示 Then Return
        _浮层显示 = True
        Using 进入切页刷新过滤()
            同步内容面板布局()
            切换绑定控件()
        End Using
    End Sub

    Private Sub 关闭浮层()
        If Not _浮层显示 Then Return
        _浮层显示 = False
        Using 进入切页刷新过滤()
            同步内容面板布局()
            切换绑定控件()
        End Using
        请求V3渲染()
    End Sub

    Private Sub 限制滚动范围()
        Dim totalWidth = CInt(获取标签页总宽度())
        Dim maxScroll = Math.Max(0, totalWidth - Me.Width)
        _滚动偏移 = Math.Clamp(_滚动偏移, 0, maxScroll)
    End Sub

    Private Sub 确保选中项可见()
        If _selectedIndex < 0 OrElse _selectedIndex >= 项目列表.Count Then Return
        Dim s As Single = DpiScale()
        Dim _标签页项间距 As Single = 标签页项间距 * s
        Dim stripRect = 获取标签栏矩形()
        Dim 折叠按钮宽 As Single = 计算折叠按钮宽度()
        Dim 折叠按钮空间 As Single = If(Ribbon模式值 AndAlso 折叠按钮宽 > 0, 折叠按钮宽 + _标签页项间距, 0)
        Dim availableWidth As Single = stripRect.Width - 标签栏内边距.Left * s - 标签栏内边距.Right * s - 折叠按钮空间
        Dim widths = 计算所有标签页宽度(availableWidth)

        Dim absX As Single = 标签栏内边距.Left * s + If(折叠按钮对齐值 = CollapseButtonAlignmentEnum.Left, 折叠按钮空间, 0)
        For i As Integer = 0 To _selectedIndex - 1
            absX += widths(i) + _标签页项间距
        Next
        Dim itemW As Single = widths(_selectedIndex)

        If absX < _滚动偏移 Then
            _滚动偏移 = CInt(absX)
        ElseIf absX + itemW > _滚动偏移 + Me.Width Then
            _滚动偏移 = CInt(absX + itemW - Me.Width)
        End If
        限制滚动范围()
    End Sub

    Private Sub 更新滚动条布局()
        Dim totalW As Integer = CInt(获取标签页总宽度())
        Dim visibleW As Integer = Me.Width
        If totalW <= visibleW OrElse 项目列表.Count = 0 Then
            _滚动条ThumbRect = Rectangle.Empty
            _滚动条TrackRect = Rectangle.Empty
            Return
        End If

        Dim s As Single = DpiScale()
        Dim _滚动条高度 As Integer = CInt(滚动条高度 * s)
        Dim stripRect = 获取标签栏矩形()
        Dim margin As Integer = CInt(2 * s)
        Dim sbY As Integer
        If 标签页位置 = TabPositionEnum.Top Then
            sbY = stripRect.Bottom - _滚动条高度 - margin
        Else
            sbY = stripRect.Y + margin
        End If
        Dim sbX As Integer = CInt(标签栏内边距.Left * s) + margin
        Dim sbW As Integer = Me.Width - CInt(标签栏内边距.Left * s) - CInt(标签栏内边距.Right * s) - margin * 2
        If sbW <= 0 Then
            _滚动条ThumbRect = Rectangle.Empty
            _滚动条TrackRect = Rectangle.Empty
            Return
        End If

        _滚动条TrackRect = New Rectangle(sbX, sbY, sbW, _滚动条高度)

        Dim maxOff As Integer = Math.Max(0, totalW - visibleW)
        Dim thumbW As Integer = Math.Max(CInt(20 * s), CInt(sbW * visibleW / Math.Max(1, totalW)))
        Dim thumbX As Integer = sbX
        If maxOff > 0 Then
            thumbX = sbX + CInt((sbW - thumbW) * _滚动偏移 / maxOff)
        End If
        _滚动条ThumbRect = New Rectangle(thumbX, sbY, thumbW, _滚动条高度)
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        同步内容面板布局()
        限制滚动范围()
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnLayout(e As LayoutEventArgs)
        MyBase.OnLayout(e)
        同步内容面板布局()
        请求V3渲染()
    End Sub

    Friend Sub InvalidateLayoutCache()
        _缓存宽度 = Nothing
        _缓存可用宽度 = -1
    End Sub

    Private _上一个父级 As Control = Nothing
    Protected Overrides Sub OnParentChanged(e As EventArgs)
        MyBase.OnParentChanged(e)
        同步切页背景源订阅()
        解除背景穿透消费者()
        Try : D3D_BackgroundPenetration.UnregisterConsumer(_内容面板) : Catch : End Try
        If _上一个父级 IsNot Nothing Then
            RemoveHandler _上一个父级.Resize, AddressOf 父级几何变更
            If _内容面板.Parent Is _上一个父级 Then
                _上一个父级.Controls.Remove(_内容面板)
            End If
        End If
        _上一个父级 = Me.Parent
        If _上一个父级 IsNot Nothing Then
            AddHandler _上一个父级.Resize, AddressOf 父级几何变更
        End If
        同步内容面板布局()
        If Me.Parent Is Nothing Then 清除所有切页刷新抑制()
    End Sub

    Private Sub 父级几何变更(sender As Object, e As EventArgs)
        If Ribbon模式值 Then 同步内容面板布局()
    End Sub

    Protected Overrides Sub OnLocationChanged(e As EventArgs)
        MyBase.OnLocationChanged(e)
        If Ribbon模式值 Then 同步内容面板布局()
    End Sub

    Protected Overrides Sub OnVisibleChanged(e As EventArgs)
        MyBase.OnVisibleChanged(e)
        If Not Me.Visible Then
            _内容面板.Visible = False
            解除背景穿透消费者()
            Try : D3D_BackgroundPenetration.UnregisterConsumer(_内容面板) : Catch : End Try
        Else
            Using 进入切页刷新过滤()
                同步内容面板布局()
                切换绑定控件()
            End Using
        End If
    End Sub

    Private Shared Function 颜色插值(c1 As Color, c2 As Color, t As Single) As Color
        Return Color.FromArgb(
            字节插值(c1.A, c2.A, t),
            字节插值(c1.R, c2.R, t),
            字节插值(c1.G, c2.G, t),
            字节插值(c1.B, c2.B, t))
    End Function

    Private Shared Function 字节插值(a As Integer, b As Integer, t As Single) As Integer
        Return Math.Clamp(CInt(a + (b - a) * t), 0, 255)
    End Function

    Private Function 计算折叠按钮宽度() As Single
        If Not Ribbon模式值 Then Return 0
        Dim s As Single = DpiScale()
        Dim _标签页文本内边距 As Single = 标签页文本内边距 * s
        Dim _图标尺寸 As Single = 图标尺寸 * s
        Dim _图标与文本间距 As Single = 图标与文本间距 * s
        Dim _标签页最小宽度 As Single = 标签页最小宽度 * s
        Dim caption As String = If(已折叠值, 折叠按钮展开文本值, 折叠按钮折叠文本值)
        Dim font = If(折叠按钮字体值, Me.Font)
        Dim textW As Single = 0
        If Not String.IsNullOrEmpty(caption) Then
            textW = TextRenderer.MeasureText(caption, font, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.NoPadding).Width
        End If
        Dim iconW As Single = If(折叠按钮图标值 IsNot Nothing, _图标尺寸 + If(textW > 0, _图标与文本间距, 0.0F), 0)
        Dim w As Single = textW + iconW + _标签页文本内边距 * 2
        Return Math.Max(_标签页最小宽度, w)
    End Function

    Private Function 获取折叠按钮矩形() As RectangleF
        If Not Ribbon模式值 Then Return RectangleF.Empty
        Dim s As Single = DpiScale()
        Dim _标签栏内边距 As New Padding(
            CInt(标签栏内边距.Left * s), CInt(标签栏内边距.Top * s),
            CInt(标签栏内边距.Right * s), CInt(标签栏内边距.Bottom * s))
        Dim stripRect = 获取标签栏矩形()
        Dim fullH As Single = stripRect.Height - _标签栏内边距.Top - _标签栏内边距.Bottom
        Dim y As Single = stripRect.Y + _标签栏内边距.Top
        Dim w As Single = 计算折叠按钮宽度()
        If w <= 0 Then Return RectangleF.Empty
        Dim x As Single
        If 折叠按钮对齐值 = CollapseButtonAlignmentEnum.Left Then
            x = stripRect.X + _标签栏内边距.Left
        Else
            x = stripRect.Right - _标签栏内边距.Right - w
        End If
        Return New RectangleF(x, y, w, fullH)
    End Function
#End Region

#Region "鼠标状态与动画"
    Private Function 获取动画进度(index As Integer) As Single
        Dim value As TabAnimState = Nothing
        If _标签页动画.TryGetValue(index, value) Then
            Return value.当前值
        End If
        Return 0.0F
    End Function

    Private Sub 设置动画目标(index As Integer, target As Single)
        Dim state As TabAnimState = Nothing
        If Not _标签页动画.TryGetValue(index, state) Then
            state = New TabAnimState()
            _标签页动画(index) = state
        End If
        If Math.Abs(state.目标值 - target) < 0.001F Then Return
        state.起始值 = state.当前值
        state.目标值 = target
        state.起始时刻 = Stopwatch.GetTimestamp()
        启动动画驱动()
    End Sub

    Private Sub 设置折叠按钮动画目标(target As Single)
        If Math.Abs(_折叠按钮动画.目标值 - target) < 0.001F Then Return
        _折叠按钮动画.起始值 = _折叠按钮动画.当前值
        _折叠按钮动画.目标值 = target
        _折叠按钮动画.起始时刻 = Stopwatch.GetTimestamp()
        启动动画驱动()
    End Sub

    Private Sub 动画帧更新(sender As Object, e As EventArgs)
        Dim 有活跃动画 As Boolean = False
        Dim now As Long = Stopwatch.GetTimestamp()
        Dim freq As Double = Stopwatch.Frequency

        For Each kvp In _标签页动画
            Dim state = kvp.Value
            If Math.Abs(state.当前值 - state.目标值) > 0.001F Then
                Dim elapsed As Double = (now - state.起始时刻) / freq * 1000.0
                Dim totalDuration As Double = 动画时长值 * CDbl(Math.Abs(state.目标值 - state.起始值))
                If totalDuration <= 0 Then
                    state.当前值 = state.目标值
                Else
                    Dim t As Single = CSng(Math.Min(elapsed / totalDuration, 1.0))
                    Dim eased As Single = 1.0F - CSng(Math.Pow(1.0 - t, 3))
                    state.当前值 = state.起始值 + (state.目标值 - state.起始值) * eased
                    If t >= 1.0F Then state.当前值 = state.目标值
                End If
                有活跃动画 = True
            Else
                state.当前值 = state.目标值
            End If
        Next

        ' 折叠按钮动画
        Dim btn = _折叠按钮动画
        If Math.Abs(btn.当前值 - btn.目标值) > 0.001F Then
            Dim elapsed As Double = (now - btn.起始时刻) / freq * 1000.0
            Dim totalDuration As Double = 动画时长值 * CDbl(Math.Abs(btn.目标值 - btn.起始值))
            If totalDuration <= 0 Then
                btn.当前值 = btn.目标值
            Else
                Dim t As Single = CSng(Math.Min(elapsed / totalDuration, 1.0))
                Dim eased As Single = 1.0F - CSng(Math.Pow(1.0 - t, 3))
                btn.当前值 = btn.起始值 + (btn.目标值 - btn.起始值) * eased
                If t >= 1.0F Then btn.当前值 = btn.目标值
            End If
            有活跃动画 = True
        Else
            btn.当前值 = btn.目标值
        End If

        If Not 有活跃动画 Then
            停止动画驱动()
        End If
        请求V3渲染()
    End Sub

    Private Sub 动画驱动脏区(helper As V3_AnimationHelper, owner As Control, sink As V3_AnimationHelper.InvalidateRegionSink)
        sink.SuppressInvalidate()
    End Sub

    Private Sub 启动动画驱动()
        If _动画中 Then Return
        _动画中 = True
        _动画助手.StartFrameLoop(AddressOf 动画帧更新)
    End Sub

    Friend Sub 停止动画驱动()
        If Not _动画中 Then Return
        _动画中 = False
        _动画助手.StopFrameLoop()
    End Sub

    Private Function HitTestTab(clientPoint As Point) As Integer
        For i As Integer = 0 To 项目列表.Count - 1
            If 获取标签页项矩形(i).Contains(clientPoint.X, clientPoint.Y) Then
                Dim item = 项目列表(i)
                If item.IsSeparator Then Return -1
                Return i
            End If
        Next
        Return -1
    End Function

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)

        If _滚动条IsDragging Then
            Dim totalW = CInt(获取标签页总宽度())
            _滚动偏移 = 滚动条DragMove(e.X, totalW, Me.Width)
            限制滚动范围()
            请求V3渲染()
            Return
        End If

        Dim needInvalidate As Boolean = 滚动条UpdateHover(e.Location)

        ' 折叠按钮悬停
        Dim btnRect = 获取折叠按钮矩形()
        Dim newBtnHover As Boolean = (Not btnRect.IsEmpty) AndAlso btnRect.Contains(e.Location.X, e.Location.Y)
        If newBtnHover <> _折叠按钮悬停 Then
            _折叠按钮悬停 = newBtnHover
            设置折叠按钮动画目标(If(newBtnHover, 1.0F, 0.0F))
            needInvalidate = True
        End If

        Dim newHover As Integer = -1
        For i As Integer = 0 To 项目列表.Count - 1
            If 获取标签页项矩形(i).Contains(e.Location.X, e.Location.Y) Then
                Dim item = 项目列表(i)
                If Not item.IsSeparator Then
                    newHover = i
                End If
                Exit For
            End If
        Next
        Me.Cursor = If(newHover >= 0 OrElse newBtnHover, Cursors.Hand, Cursors.Default)
        If newHover <> _悬停索引 Then
            If _悬停索引 >= 0 Then
                设置动画目标(_悬停索引, 0.0F)
            End If
            _悬停索引 = newHover
            If _悬停索引 >= 0 Then
                设置动画目标(_悬停索引, 1.0F)
            End If
            needInvalidate = True
        End If
        If needInvalidate Then 请求V3渲染()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        Me.Cursor = Cursors.Default
        Dim needInvalidate As Boolean = False
        If _悬停索引 >= 0 Then
            设置动画目标(_悬停索引, 0.0F)
            _悬停索引 = -1
            needInvalidate = True
        End If
        If _折叠按钮悬停 Then
            _折叠按钮悬停 = False
            设置折叠按钮动画目标(0.0F)
            needInvalidate = True
        End If
        If _滚动条IsHover Then
            _滚动条IsHover = False
            needInvalidate = True
        End If
        If needInvalidate Then 请求V3渲染()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button = MouseButtons.Left Then
            Me.Focus()
            ' 折叠按钮命中（优先于滚动条与标签页）
            If Ribbon模式值 Then
                Dim btnRect = 获取折叠按钮矩形()
                If Not btnRect.IsEmpty AndAlso btnRect.Contains(e.Location.X, e.Location.Y) Then
                    IsCollapsed = Not IsCollapsed
                    Return
                End If
            End If
            If Not _滚动条ThumbRect.IsEmpty AndAlso _滚动条ThumbRect.Contains(e.Location) Then
                _滚动条IsDragging = True
                _滚动条DragStartX = e.X
                _滚动条DragStartOffset = _滚动偏移
                Return
            End If
            If Not _滚动条TrackRect.IsEmpty AndAlso _滚动条TrackRect.Contains(e.Location) Then
                Dim totalW = CInt(获取标签页总宽度())
                Dim maxOff = Math.Max(0, totalW - Me.Width)
                If e.X < _滚动条ThumbRect.X Then
                    _滚动偏移 = Math.Max(0, _滚动偏移 - Me.Width)
                Else
                    _滚动偏移 = Math.Min(maxOff, _滚动偏移 + Me.Width)
                End If
                限制滚动范围()
                请求V3渲染()
                Return
            End If
            Dim hit = HitTestTab(e.Location)
            If hit >= 0 Then
                If Ribbon模式值 Then
                    If 已折叠值 Then
                        ' 折叠态：点击标签显示浮层；若点击的是当前选中标签且浮层已显示，则关闭浮层
                        If hit = _selectedIndex AndAlso _浮层显示 Then
                            关闭浮层()
                        Else
                            SelectedIndex = hit
                            显示浮层()
                        End If
                    Else
                        ' 固定态：点击当前选中标签 => 切换为折叠
                        If hit = _selectedIndex Then
                            IsCollapsed = True
                        Else
                            SelectedIndex = hit
                        End If
                    End If
                Else
                    SelectedIndex = hit
                End If
            End If
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If _滚动条IsDragging Then
            _滚动条IsDragging = False
            请求V3渲染()
        End If
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        If 项目列表.Count = 0 Then Return
        Dim stripRect = 获取标签栏矩形()
        If Not stripRect.Contains(e.Location) Then Return
        Dim totalWidth = CInt(获取标签页总宽度())
        If totalWidth <= Me.Width Then Return
        Dim scrollAmount As Integer = Math.Max(1, SystemInformation.MouseWheelScrollLines * 30)
        _滚动偏移 -= Math.Sign(e.Delta) * scrollAmount
        限制滚动范围()
        请求V3渲染()
    End Sub

    Private Function 滚动条UpdateHover(mouseLocation As Point) As Boolean
        Dim wasHover As Boolean = _滚动条IsHover
        _滚动条IsHover = Not _滚动条ThumbRect.IsEmpty AndAlso _滚动条ThumbRect.Contains(mouseLocation)
        Return _滚动条IsHover <> wasHover
    End Function

    Private Function 滚动条DragMove(mouseX As Integer, totalWidth As Integer, visibleWidth As Integer) As Integer
        If Not _滚动条IsDragging Then Return _滚动条DragStartOffset
        Dim trackW As Integer = _滚动条TrackRect.Width
        Dim maxOff As Integer = Math.Max(0, totalWidth - visibleWidth)
        Dim thumbW As Integer = Math.Max(20, CInt(trackW * visibleWidth / Math.Max(1, totalWidth)))
        Dim usableW As Integer = trackW - thumbW
        If usableW <= 0 Then Return _滚动条DragStartOffset
        Dim dx As Integer = mouseX - _滚动条DragStartX
        Dim newOff As Integer = _滚动条DragStartOffset + CInt(dx * maxOff / usableW)
        Return Math.Max(0, Math.Min(maxOff, newOff))
    End Function

    Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
        Select Case keyData And Keys.KeyCode
            Case Keys.Left, Keys.Right, Keys.Home, Keys.End
                Return True
        End Select
        Return MyBase.IsInputKey(keyData)
    End Function

    Protected Overrides Sub OnPreviewKeyDown(e As PreviewKeyDownEventArgs)
        Select Case e.KeyCode
            Case Keys.Left, Keys.Right, Keys.Home, Keys.End
                e.IsInputKey = True
        End Select
        MyBase.OnPreviewKeyDown(e)
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If 项目列表.Count = 0 Then Return
        Select Case e.KeyCode
            Case Keys.Left
                SelectedIndex = 上一个可选索引(_selectedIndex)
                e.Handled = True
            Case Keys.Right
                SelectedIndex = 下一个可选索引(_selectedIndex)
                e.Handled = True
            Case Keys.Home
                SelectedIndex = 第一个可选索引()
                e.Handled = True
            Case Keys.End
                SelectedIndex = 最后一个可选索引()
                e.Handled = True
        End Select
    End Sub

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        Dim selectableCount = 0
        For Each item In 项目列表
            If Not item.IsSeparator Then selectableCount += 1
        Next
        If selectableCount > 1 Then
            If keyData = (Keys.Tab Or Keys.Control) Then
                Dim nextIdx = 下一个可选索引(_selectedIndex)
                If nextIdx = _selectedIndex Then
                    SelectedIndex = 第一个可选索引()
                Else
                    SelectedIndex = nextIdx
                End If
                Return True
            ElseIf keyData = (Keys.Tab Or Keys.Control Or Keys.Shift) Then
                Dim prevIdx = 上一个可选索引(_selectedIndex)
                If prevIdx = _selectedIndex Then
                    SelectedIndex = 最后一个可选索引()
                Else
                    SelectedIndex = prevIdx
                End If
                Return True
            End If
        End If
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    Protected Overrides Sub OnGotFocus(e As EventArgs)
        MyBase.OnGotFocus(e)
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnLostFocus(e As EventArgs)
        MyBase.OnLostFocus(e)
        请求V3渲染()
    End Sub
#End Region

#Region "通用属性"
    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            请求V3渲染()
        End If
    End Sub

    Private Function DpiScale() As Single
        Return V3_DpiContext.FromControl(Me).Scale
    End Function

    Friend Sub InvalidateFontResources()
        D3D_RenderCore.InvalidateExistingTextResources(Me)
        InvalidateLayoutCache()
    End Sub

    Friend Sub RefreshFontDependentRenderingNow()
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        InvalidateFontResources()
        MyBase.OnFontChanged(e)
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        InvalidateLayoutCache()
        请求V3渲染()
    End Sub

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

    Private _backgroundSource As Control = Nothing
    <Browsable(False),
     EditorBrowsable(EditorBrowsableState.Never),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
     DefaultValue(GetType(Control), Nothing)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource Is value Then Return
            _backgroundSource = D3D_BackgroundPenetration.SetBackgroundSource(Me, _backgroundSource, value)
            _内容面板.BackgroundSource = value
            请求V3渲染()
        End Set
    End Property

    Private Sub 解除背景穿透消费者()
        Try : D3D_BackgroundPenetration.UnregisterConsumer(Me) : Catch : End Try
    End Sub

    <Category("LakeUI"), Description("切换绑定页面时保留已访问页面；仅在首次显示、尺寸、内容面板位置或背景穿透源变化时主动递归刷新。控件自身的正常刷新不受影响。"), DefaultValue(True), Browsable(True)>
    Public Property SuppressBoundPageRefreshOnSwitch As Boolean
        Get
            Return _切换页抑制刷新
        End Get
        Set(value As Boolean)
            If _切换页抑制刷新 = value Then Return
            _切换页抑制刷新 = value
            If Not value Then 清除所有切页刷新抑制()
        End Set
    End Property

    Private 动画时长值 As Integer = 300
    <Category("LakeUI"), Description(GlobalOptions.动画时长描述词), DefaultValue(300), Browsable(True)>
    Public Property AnimationDuration As Integer
        Get
            Return 动画时长值
        End Get
        Set(value As Integer)
            动画时长值 = Math.Max(0, value)
        End Set
    End Property

    Private 动画帧率值 As Integer = 60
    <Category("LakeUI"), Description(GlobalOptions.动画帧率描述词), DefaultValue(60), Browsable(True)>
    Public Property AnimationFPS As Integer
        Get
            Return 动画帧率值
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If 动画帧率值 = value Then Return
            动画帧率值 = value
            _动画助手.FPS = 动画帧率值
        End Set
    End Property
#End Region

#Region "标签栏属性"
    Private 标签页位置 As TabPositionEnum = TabPositionEnum.Top
    <Category("LakeUI"), Description("标签栏位于控件的顶部还是底部"), DefaultValue(GetType(TabPositionEnum), "Top"), Browsable(True)>
    Public Property TabPosition As TabPositionEnum
        Get
            Return 标签页位置
        End Get
        Set(value As TabPositionEnum)
            If 标签页位置 <> value Then
                标签页位置 = value
                同步内容面板布局()
                请求V3渲染()
            End If
        End Set
    End Property

    Private 标签页尺寸模式 As TabSizingEnum = TabSizingEnum.AutoWidth
    <Category("LakeUI"), Description("选项卡宽度的计算模式：AutoWidth 根据文本自适应，EqualWidth 均分可用宽度"), DefaultValue(GetType(TabSizingEnum), "AutoWidth"), Browsable(True)>
    Public Property TabSizingMode As TabSizingEnum
        Get
            Return 标签页尺寸模式
        End Get
        Set(value As TabSizingEnum)
            If 标签页尺寸模式 <> value Then
                标签页尺寸模式 = value
                InvalidateLayoutCache()
                _滚动偏移 = 0
                限制滚动范围()
                请求V3渲染()
            End If
        End Set
    End Property

    Private 标签页对齐方式 As TabAlignmentEnum = TabAlignmentEnum.Left
    <Category("LakeUI"), Description("选项卡组在标签栏中的对齐方式（仅在 AutoWidth 且未溢出时生效）"), DefaultValue(GetType(TabAlignmentEnum), "Left"), Browsable(True)>
    Public Property TabAlignment As TabAlignmentEnum
        Get
            Return 标签页对齐方式
        End Get
        Set(value As TabAlignmentEnum)
            If 标签页对齐方式 <> value Then
                标签页对齐方式 = value
                请求V3渲染()
            End If
        End Set
    End Property

    Private 标签栏高度 As Integer = 40
    <Category("LakeUI"), Description("标签栏的高度"), DefaultValue(40), Browsable(True)>
    Public Property TabStripHeight As Integer
        Get
            Return 标签栏高度
        End Get
        Set(value As Integer)
            If 标签栏高度 <> value Then
                标签栏高度 = Math.Max(16, value)
                同步内容面板布局()
                请求V3渲染()
            End If
        End Set
    End Property

    Private 标签栏背景颜色 As Color = Color.FromArgb(48, 48, 48)
    <Category("LakeUI"), Description("标签栏背景颜色"), DefaultValue(GetType(Color), "48, 48, 48"), Browsable(True)>
    Public Property TabStripBackColor As Color
        Get
            Return 标签栏背景颜色
        End Get
        Set(value As Color)
            SetValue(标签栏背景颜色, value)
        End Set
    End Property

    Private 标签栏背景图片 As Image = Nothing
    ''' <summary>
    ''' 标签栏背景图片。图片以居中裁切模式（CenterImage）绘制：
    ''' 保持比例缩放至撑满标签栏区域，超出部分从中心裁切。
    ''' 设为 Nothing 则不绘制背景图片。
    ''' </summary>
    <Category("LakeUI"), Description("标签栏背景图片（居中裁切模式）。"), DefaultValue(GetType(Image), Nothing), Browsable(True)>
    Public Property TabStripBackgroundImage As Image
        Get
            Return 标签栏背景图片
        End Get
        Set(value As Image)
            标签栏背景图片 = value
            请求V3渲染()
        End Set
    End Property

    Private 标签栏遮罩颜色 As Color = Color.Transparent
    ''' <summary>
    ''' 标签栏半透明遮罩颜色，绘制在背景图片之上、标签页项之下。
    ''' 可使用半透明颜色为背景图片添加色调或降低对比度，使标签页文字更易读。
    ''' 设为 Transparent 则不绘制遮罩。
    ''' </summary>
    <Category("LakeUI"), Description("标签栏半透明遮罩颜色，绘制在背景图片之上、标签页项之下。"), DefaultValue(GetType(Color), "Transparent"), Browsable(True)>
    Public Property TabStripOverlayColor As Color
        Get
            Return 标签栏遮罩颜色
        End Get
        Set(value As Color)
            SetValue(标签栏遮罩颜色, value)
        End Set
    End Property

    Private 标签栏内边距 As New Padding(6)
    <Category("LakeUI"), Description("标签栏内边距，控制标签页项在标签栏容器内的缩进"), DefaultValue(GetType(Padding), "6, 6, 6, 6"), Browsable(True)>
    Public Property TabStripPadding As Padding
        Get
            Return 标签栏内边距
        End Get
        Set(value As Padding)
            If 标签栏内边距.Equals(value) Then Return
            标签栏内边距 = value
            InvalidateLayoutCache()
            请求V3渲染()
        End Set
    End Property
#End Region

#Region "标签页项属性"
    Private 标签页最小宽度 As Integer = 50
    <Category("LakeUI"), Description("每个标签页项的最小宽度"), DefaultValue(50), Browsable(True)>
    Public Property TabItemMinWidth As Integer
        Get
            Return 标签页最小宽度
        End Get
        Set(value As Integer)
            If 标签页最小宽度 <> value Then
                标签页最小宽度 = Math.Max(16, value)
                InvalidateLayoutCache()
                请求V3渲染()
            End If
        End Set
    End Property

    Private 标签页项间距 As Integer = 1
    <Category("LakeUI"), Description("标签页项之间的间距"), DefaultValue(1), Browsable(True)>
    Public Property TabItemSpacing As Integer
        Get
            Return 标签页项间距
        End Get
        Set(value As Integer)
            If 标签页项间距 = value Then Return
            标签页项间距 = value
            InvalidateLayoutCache()
            请求V3渲染()
        End Set
    End Property

    Private 选中标签页背景颜色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("选中标签页的背景颜色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
    Public Property TabItemSelectedBackColor As Color
        Get
            Return 选中标签页背景颜色
        End Get
        Set(value As Color)
            SetValue(选中标签页背景颜色, value)
        End Set
    End Property

    Private 悬停标签页背景颜色 As Color = Color.FromArgb(64, 64, 64)
    <Category("LakeUI"), Description("鼠标悬停时标签页的背景颜色"), DefaultValue(GetType(Color), "64, 64, 64"), Browsable(True)>
    Public Property TabItemHoverBackColor As Color
        Get
            Return 悬停标签页背景颜色
        End Get
        Set(value As Color)
            SetValue(悬停标签页背景颜色, value)
        End Set
    End Property

    Private 标签页默认文本颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("标签页项的默认文本颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property TabItemForeColor As Color
        Get
            Return 标签页默认文本颜色
        End Get
        Set(value As Color)
            SetValue(标签页默认文本颜色, value)
        End Set
    End Property

    Private 选中标签页文本颜色 As Color = Color.White
    <Category("LakeUI"), Description("选中标签页的文本颜色"), DefaultValue(GetType(Color), "White"), Browsable(True)>
    Public Property TabItemSelectedForeColor As Color
        Get
            Return 选中标签页文本颜色
        End Get
        Set(value As Color)
            SetValue(选中标签页文本颜色, value)
        End Set
    End Property

    Private 标签页圆角半径 As Integer = 6
    <Category("LakeUI"), Description("标签页项的圆角半径"), DefaultValue(6), Browsable(True)>
    Public Property TabItemBorderRadius As Integer
        Get
            Return 标签页圆角半径
        End Get
        Set(value As Integer)
            SetValue(标签页圆角半径, value)
        End Set
    End Property

    Private 焦点边框颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("键盘焦点时选中标签页的边框颜色，为 Empty 时不显示"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property FocusBorderColor As Color
        Get
            Return 焦点边框颜色
        End Get
        Set(value As Color)
            SetValue(焦点边框颜色, value)
        End Set
    End Property

    Private 标签页文本内边距 As Integer = 12
    <Category("LakeUI"), Description("标签页文本的水平内边距"), DefaultValue(12), Browsable(True)>
    Public Property TabItemTextPadding As Integer
        Get
            Return 标签页文本内边距
        End Get
        Set(value As Integer)
            If 标签页文本内边距 = value Then Return
            标签页文本内边距 = value
            InvalidateLayoutCache()
            请求V3渲染()
        End Set
    End Property
#End Region

#Region "图标属性"
    Private 图标尺寸 As Integer = 20
    <Category("LakeUI"), Description("标签页图标尺寸"), DefaultValue(20), Browsable(True)>
    Public Property TabIconSize As Integer
        Get
            Return 图标尺寸
        End Get
        Set(value As Integer)
            If 图标尺寸 = value Then Return
            图标尺寸 = value
            InvalidateLayoutCache()
            请求V3渲染()
        End Set
    End Property

    Private 图标与文本间距 As Integer = 6
    <Category("LakeUI"), Description("图标与文本之间的间距"), DefaultValue(6), Browsable(True)>
    Public Property TabIconTextSpacing As Integer
        Get
            Return 图标与文本间距
        End Get
        Set(value As Integer)
            If 图标与文本间距 = value Then Return
            图标与文本间距 = value
            InvalidateLayoutCache()
            请求V3渲染()
        End Set
    End Property
#End Region

#Region "选中指示条属性"
    Private 选中指示条颜色 As Color = Color.DodgerBlue
    <Category("LakeUI"), Description("选中指示条的颜色"), DefaultValue(GetType(Color), "DodgerBlue"), Browsable(True)>
    Public Property IndicatorColor As Color
        Get
            Return 选中指示条颜色
        End Get
        Set(value As Color)
            SetValue(选中指示条颜色, value)
        End Set
    End Property

    Private 选中指示条高度 As Integer = 3
    <Category("LakeUI"), Description("选中指示条的高度"), DefaultValue(3), Browsable(True)>
    Public Property IndicatorHeight As Integer
        Get
            Return 选中指示条高度
        End Get
        Set(value As Integer)
            SetValue(选中指示条高度, value)
        End Set
    End Property

    Private 选中指示条边距 As Integer = 6
    <Category("LakeUI"), Description("选中指示条的左右边距"), DefaultValue(6), Browsable(True)>
    Public Property IndicatorPadding As Integer
        Get
            Return 选中指示条边距
        End Get
        Set(value As Integer)
            SetValue(选中指示条边距, value)
        End Set
    End Property

    Private 选中指示条圆角半径 As Integer = 2
    <Category("LakeUI"), Description("选中指示条的圆角半径"), DefaultValue(2), Browsable(True)>
    Public Property IndicatorBorderRadius As Integer
        Get
            Return 选中指示条圆角半径
        End Get
        Set(value As Integer)
            SetValue(选中指示条圆角半径, value)
        End Set
    End Property
#End Region

#Region "内容区域属性"
    Private 内容区域背景颜色 As Color = Color.FromArgb(36, 36, 36)
    <Category("LakeUI"), Description("内容区域背景颜色"), DefaultValue(GetType(Color), "36, 36, 36"), Browsable(True)>
    Public Property ContentBackColor As Color
        Get
            Return 内容区域背景颜色
        End Get
        Set(value As Color)
            SetValue(内容区域背景颜色, value)
            _内容面板.BackColor = 获取内容面板有效背景色()
        End Set
    End Property

    Private 内容区域边框颜色 As Color = Color.DarkGray
    <Category("LakeUI"), Description("内容区域边框颜色"), DefaultValue(GetType(Color), "DarkGray"), Browsable(True)>
    Public Property ContentBorderColor As Color
        Get
            Return 内容区域边框颜色
        End Get
        Set(value As Color)
            SetValue(内容区域边框颜色, value)
        End Set
    End Property

    Private 内容区域边框宽度 As Integer = 0
    <Category("LakeUI"), Description("内容区域边框宽度"), DefaultValue(0), Browsable(True)>
    Public Property ContentBorderWidth As Integer
        Get
            Return 内容区域边框宽度
        End Get
        Set(value As Integer)
            SetValue(内容区域边框宽度, value)
        End Set
    End Property
#End Region

#Region "滚动条属性"
    Private 滚动条高度 As Integer = 6
    <Category("LakeUI"), Description("标签栏横向滚动条高度"), DefaultValue(6), Browsable(True)>
    Public Property ScrollBarHeight As Integer
        Get
            Return 滚动条高度
        End Get
        Set(value As Integer)
            SetValue(滚动条高度, value)
        End Set
    End Property

    Private Shared ReadOnly 默认滚动条轨道颜色 As Color = Color.FromArgb(20, 20, 20)
    Private 滚动条轨道颜色 As Color = 默认滚动条轨道颜色
    <Category("LakeUI"), Description("标签栏滚动条轨道颜色"), Browsable(True)>
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
    <Category("LakeUI"), Description("标签栏滚动条滑块颜色"), Browsable(True)>
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
    <Category("LakeUI"), Description("标签栏滚动条滑块悬停颜色"), Browsable(True)>
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

#Region "分割线与说明项属性"
    Private 分割线颜色值 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("分割线颜色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
    Public Property SeparatorColor As Color
        Get
            Return 分割线颜色值
        End Get
        Set(value As Color)
            SetValue(分割线颜色值, value)
        End Set
    End Property

    Private 分割线宽度值 As Integer = 20
    <Category("LakeUI"), Description("分割线宽度（分割线项所占的水平空间）"), DefaultValue(20), Browsable(True)>
    Public Property SeparatorWidth As Integer
        Get
            Return 分割线宽度值
        End Get
        Set(value As Integer)
            If value < 1 Then value = 1
            If 分割线宽度值 = value Then Return
            分割线宽度值 = value
            InvalidateLayoutCache()
            请求V3渲染()
        End Set
    End Property
#End Region

#Region "Ribbon 模式属性"
    Private Ribbon模式值 As Boolean = False
    <Category("LakeUI"), Description("启用 Ribbon 模式：内容区域使用固定高度，且支持折叠/展开切换"), DefaultValue(False), Browsable(True)>
    Public Property RibbonMode As Boolean
        Get
            Return Ribbon模式值
        End Get
        Set(value As Boolean)
            If Ribbon模式值 <> value Then
                Ribbon模式值 = value
                InvalidateLayoutCache()
                _浮层显示 = False
                同步内容面板布局()
                限制滚动范围()
                请求V3渲染()
            End If
        End Set
    End Property

    Private Ribbon内容高度值 As Integer = 120
    <Category("LakeUI"), Description("Ribbon 模式下展开时内容区域的固定高度"), DefaultValue(120), Browsable(True)>
    Public Property RibbonContentHeight As Integer
        Get
            Return Ribbon内容高度值
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If Ribbon内容高度值 <> value Then
                Ribbon内容高度值 = value
                If Ribbon模式值 Then 同步内容面板布局()
                请求V3渲染()
            End If
        End Set
    End Property

    Private 已折叠值 As Boolean = False
    <Category("LakeUI"), Description("Ribbon 模式下当前是否处于折叠状态"), DefaultValue(False), Browsable(True)>
    Public Property IsCollapsed As Boolean
        Get
            Return 已折叠值
        End Get
        Set(value As Boolean)
            If 已折叠值 <> value Then
                已折叠值 = value
                ' 状态切换时清空浮层显示标记
                _浮层显示 = False
                If Ribbon模式值 Then 同步内容面板布局()
                请求V3渲染()
                RaiseEvent CollapsedChanged(Me, EventArgs.Empty)
            End If
        End Set
    End Property

    Public Event CollapsedChanged As EventHandler

    Private 折叠按钮对齐值 As CollapseButtonAlignmentEnum = CollapseButtonAlignmentEnum.Right
    <Category("LakeUI"), Description("Ribbon 模式下折叠按钮的对齐方位（最左 / 最右）"), DefaultValue(GetType(CollapseButtonAlignmentEnum), "Right"), Browsable(True)>
    Public Property CollapseButtonAlignment As CollapseButtonAlignmentEnum
        Get
            Return 折叠按钮对齐值
        End Get
        Set(value As CollapseButtonAlignmentEnum)
            If 折叠按钮对齐值 <> value Then
                折叠按钮对齐值 = value
                请求V3渲染()
            End If
        End Set
    End Property

    Private 折叠按钮折叠文本值 As String = "▲"
    <Category("LakeUI"), Description("Ribbon 模式下折叠按钮在【展开状态】时显示的文本（点击后会折叠）"), DefaultValue("▲"), Browsable(True)>
    Public Property CollapseButtonCollapseText As String
        Get
            Return 折叠按钮折叠文本值
        End Get
        Set(value As String)
            value = If(value, "")
            If String.Equals(折叠按钮折叠文本值, value, StringComparison.Ordinal) Then Return
            折叠按钮折叠文本值 = value
            InvalidateLayoutCache()
            请求V3渲染()
        End Set
    End Property

    Private 折叠按钮展开文本值 As String = "▼"
    <Category("LakeUI"), Description("Ribbon 模式下折叠按钮在【折叠状态】时显示的文本（点击后会展开）"), DefaultValue("▼"), Browsable(True)>
    Public Property CollapseButtonExpandText As String
        Get
            Return 折叠按钮展开文本值
        End Get
        Set(value As String)
            value = If(value, "")
            If String.Equals(折叠按钮展开文本值, value, StringComparison.Ordinal) Then Return
            折叠按钮展开文本值 = value
            InvalidateLayoutCache()
            请求V3渲染()
        End Set
    End Property

    Private 折叠按钮字体值 As Font = Nothing
    <Category("LakeUI"), Description("Ribbon 模式下折叠按钮使用的字体，为 Nothing 时使用控件默认字体"), Browsable(True)>
    Public Property CollapseButtonFont As Font
        Get
            Return 折叠按钮字体值
        End Get
        Set(value As Font)
            If Object.ReferenceEquals(折叠按钮字体值, value) Then Return
            折叠按钮字体值 = value
            InvalidateFontResources()
            请求V3渲染()
            请求V3渲染()
        End Set
    End Property
    Private Function ShouldSerializeCollapseButtonFont() As Boolean
        Return 折叠按钮字体值 IsNot Nothing
    End Function

    Private 折叠按钮文本颜色值 As Color = Color.Empty
    <Category("LakeUI"), Description("Ribbon 模式下折叠按钮在【展开状态】时的文本颜色，为 Empty 时跟随标签页默认文本颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property CollapseButtonForeColor As Color
        Get
            Return 折叠按钮文本颜色值
        End Get
        Set(value As Color)
            SetValue(折叠按钮文本颜色值, value)
        End Set
    End Property

    Private 折叠按钮选中文本颜色值 As Color = Color.Empty
    <Category("LakeUI"), Description("Ribbon 模式下折叠按钮在【折叠状态】时的文本颜色，为 Empty 时跟随选中标签页文本颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property CollapseButtonActiveForeColor As Color
        Get
            Return 折叠按钮选中文本颜色值
        End Get
        Set(value As Color)
            SetValue(折叠按钮选中文本颜色值, value)
        End Set
    End Property

    Private 折叠按钮图标值 As Image = Nothing
    <Category("LakeUI"), Description("Ribbon 模式下折叠按钮使用的图标"), DefaultValue(GetType(Image), Nothing), Browsable(True)>
    Public Property CollapseButtonIcon As Image
        Get
            Return 折叠按钮图标值
        End Get
        Set(value As Image)
            If Object.ReferenceEquals(折叠按钮图标值, value) Then Return
            折叠按钮图标值 = value
            InvalidateLayoutCache()
            请求V3渲染()
        End Set
    End Property
#End Region

End Class
