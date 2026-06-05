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

#Region "内部类型"
    ''' <summary>
    ''' 表示 <see cref="ModernTabControl"/> 中的一个选项卡项。
    ''' 支持独立设置标题字体、颜色、图标，以及绑定一个 <see cref="Control"/> 作为选项卡内容。
    ''' 也可设为分割线。
    ''' </summary>
    Public Class ModernTab

        Friend Property Owner As ModernTabControl

        Private Sub 通知父级重绘()
            If Owner IsNot Nothing Then Owner.Invalidate()
        End Sub

        Private _text As String = "ModernTab"
        <Category("LakeUI"), Description("标签页标题文本"), DefaultValue("ModernTab"), Browsable(True)>
        Public Property Text As String
            Get
                Return _text
            End Get
            Set(value As String)
                _text = If(value, "")
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
                _tabIcon = value
                通知父级重绘()
            End Set
        End Property

        <Category("LakeUI"), Description("是否是分割线"), DefaultValue(False), Browsable(True)>
        Public Property IsSeparator As Boolean = False

        <Category("LakeUI"), Description("绑定的内容控件，切换到此选项卡时将显示该控件"), DefaultValue(GetType(Control), Nothing), Browsable(True)>
        Public Property BoundControl As Control = Nothing

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

    ''' <summary>
    ''' 支持透明背景的内容承载面板。当 BackColor 的 Alpha 为 0 或为透明色时，
    ''' 通过调用背景源的 Paint 流程取真实像素作为背景。
    ''' </summary>
    Private Class 透明内容面板
        Inherits Panel

        Private _ownerControl As ModernTabControl

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

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            If BackColor.A < 255 Then
                ' V2 透明背景穿透：source 优先选择 ModernTabControl 的 BackgroundSource，
                ' 若未指定再回退到控件父级，兼容旧行为。
                Dim tabCtrl As ModernTabControl = If(_ownerControl, TryCast(Me.Parent, ModernTabControl))
                Dim source As Control = Nothing
                If tabCtrl IsNot Nothing Then
                    source = If(tabCtrl.BackgroundSource, tabCtrl.Parent)
                End If
                If source IsNot Nothing Then
                    Using scope = D2DHelperV2.BeginPaint(e, Me, 1)
                        If scope IsNot Nothing Then
                            BackgroundPenetrationV2.PaintBackground(Me, scope, source)
                        End If
                    End Using
                End If
                If BackColor.A > 0 Then
                    Using brush As New SolidBrush(BackColor)
                        e.Graphics.FillRectangle(brush, Me.ClientRectangle)
                    End Using
                End If
            Else
                MyBase.OnPaintBackground(e)
            End If
        End Sub
    End Class
#End Region

#Region "构造"
    Private ReadOnly _标签页动画 As New Dictionary(Of Integer, TabAnimState)
    Private _悬停索引 As Integer = -1
    Private _动画计时器 As Timer
    Private _动画用Idle As Boolean = False
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

    ' V2 渲染：每次 OnPaint 内由 D2DHelperV2 提供共享 WindowCompositor。
    Private _当前合成器 As WindowCompositor

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

        _动画用Idle = (动画帧率值 <= 0)
        If Not _动画用Idle Then
            _动画计时器 = New Timer() With {.Interval = Math.Max(1, CInt(1000.0 / 动画帧率值))}
        End If
        同步内容面板布局()
    End Sub

    Private Function 获取图标缓存(img As Image) As D2DGlobals.D2DBitmapCache
        If img Is Nothing OrElse _当前合成器 Is Nothing Then Return Nothing
        Return _当前合成器.GetBitmapCache(img)
    End Function

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
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
        If _selectedIndex >= 项目列表.Count Then
            _selectedIndex = 项目列表.Count - 1
        End If
        _标签页动画.Clear()
        _悬停索引 = -1
        限制滚动范围()
        切换绑定控件()
        Invalidate()
    End Sub

    Private Sub 确保Owner()
        For Each item In 项目列表
            If item.Owner IsNot Me Then item.Owner = Me
        Next
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
                _selectedIndex = value
                确保选中项可见()
                切换绑定控件()
                Invalidate()
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
        For Each item In 项目列表
            If item.BoundControl IsNot Nothing Then
                item.BoundControl.Visible = False
                If item.BoundControl.Parent Is _内容面板 Then
                    _内容面板.Controls.Remove(item.BoundControl)
                End If
            End If
        Next
        If _selectedIndex >= 0 AndAlso _selectedIndex < 项目列表.Count Then
            Dim sel = 项目列表(_selectedIndex)
            If sel.BoundControl IsNot Nothing Then
                Dim frm = TryCast(sel.BoundControl, Form)
                If frm IsNot Nothing Then
                    If frm.TopLevel Then
                        frm.TopLevel = False
                        frm.FormBorderStyle = FormBorderStyle.None
                    End If
                End If
                If sel.BoundControl.Parent IsNot _内容面板 Then
                    _内容面板.Controls.Add(sel.BoundControl)
                End If
                sel.BoundControl.Dock = DockStyle.Fill
                sel.BoundControl.Visible = True
            End If
        End If
    End Sub
#End Region

#Region "绘制"
    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' V2 契约（与 ModernTabListControl 对齐）：
        '   • BackgroundSource 已设置 → 跳过 BackColor 整个逻辑，背景由 OnPaint 内 BackgroundPenetrationV2 绘制；
        '   • 否则一律走 .NET 自身透明逻辑（半透明 BackColor 由基类合成父级背景，不透明色由基类填底）。
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Private Function 获取内容面板有效背景色() As Color
        ' Panel 不支持完全透明的 BackColor 显示，需要使用 Transparent 触发透明背景路径。
        If 内容区域背景颜色.A < 255 Then Return Color.Transparent
        Return 内容区域背景颜色
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        确保Owner()
        If Me.Width <= 0 OrElse Me.Height <= 0 Then Return

        Dim ssaa As Integer = Math.Max(1, CInt(超采样倍率))
        If GlobalOptions.GlobalSSAA <> GlobalOptions.SuperSamplingScaleEnum.OFF Then ssaa = Math.Max(ssaa, CInt(GlobalOptions.GlobalSSAA))

        Using scope = D2DHelperV2.BeginPaint(e, Me, ssaa)
            If scope Is Nothing Then Return  ' 设计期 / 无 Form
            _当前合成器 = scope.Compositor
            Try
                Dim compositor = scope.Compositor
                Dim gRT As ID2D1RenderTarget = scope.GraphicsLayer

                ' 1) 背景层（1× 直绘）：
                '    • 显式 BackgroundSource → 绘制穿透底图（跳过 BackColor）；
                '    • 否则若 MyBase.BackColor 半透明 → 基类 OnPaintBackground 已把父级背景合成到 DC，
                '      这里再叠加 BackColor 作为半透明遮罩。
                If _backgroundSource IsNot Nothing Then
                    BackgroundPenetrationV2.PaintBackground(Me, scope, _backgroundSource)
                ElseIf MyBase.BackColor.A > 0 AndAlso MyBase.BackColor.A < 255 Then
                    Dim bgLayer = scope.BackgroundLayer
                    Dim brush = compositor.BrushCache.[Get](bgLayer, MyBase.BackColor)
                    If brush IsNot Nothing Then
                        bgLayer.FillRectangle(D2DGlobals.ToD2DRect(New RectangleF(0, 0, Me.Width, Me.Height)), brush)
                    End If
                End If

                绘制图形内容_D2D(gRT)
                scope.FlushGraphics()

                Dim dcRT As ID2D1RenderTarget = scope.DCRenderTarget
                绘制文本内容_D2D(dcRT)
            Finally
                _当前合成器 = Nothing
            End Try
        End Using
    End Sub

    Private Sub 绘制图形内容_D2D(rt As ID2D1RenderTarget)
        Dim contentRect = 获取内容区域矩形()
        Dim stripRect = 获取标签栏矩形()

        If Not contentRect.IsEmpty AndAlso 内容区域背景颜色.A = 255 Then
            rt.FillRectangle(D2DGlobals.ToD2DRect(contentRect), _当前合成器.BrushCache.Get(rt, 内容区域背景颜色))
        End If
        If 标签栏背景颜色.A > 0 Then
            rt.FillRectangle(D2DGlobals.ToD2DRect(stripRect), _当前合成器.BrushCache.Get(rt, 标签栏背景颜色))
        End If

        绘制标签栏背景图片_D2D(rt, stripRect)

        If 标签栏遮罩颜色.A > 0 Then
            rt.FillRectangle(D2DGlobals.ToD2DRect(stripRect), _当前合成器.BrushCache.Get(rt, 标签栏遮罩颜色))
        End If

        rt.PushAxisAlignedClip(New Vortice.RawRectF(stripRect.Left, stripRect.Top, stripRect.Right, stripRect.Bottom), AntialiasMode.PerPrimitive)
        Try
            For i As Integer = 0 To 项目列表.Count - 1
                Dim item = 项目列表(i)
                If item.IsSeparator Then
                    绘制分割线_D2D(rt, i)
                Else
                    绘制标签页项图形_D2D(rt, i)
                End If
            Next

            If Ribbon模式值 Then
                绘制折叠按钮图形_D2D(rt)
            End If
        Finally
            rt.PopAxisAlignedClip()
        End Try

        更新滚动条布局()
        If Not _滚动条TrackRect.IsEmpty Then
            绘制横向滚动条_D2D(rt)
        End If

        If 内容区域边框宽度 > 0 AndAlso Not contentRect.IsEmpty Then
            Dim s As Single = DpiScale()
            Dim borderRect As RectangleF = contentRect
            borderRect.Width -= 1
            borderRect.Height -= 1
            RectangleRenderer.绘制矩形边框_D2D(rt, borderRect, 内容区域边框颜色, 内容区域边框宽度 * s)
        End If
    End Sub

    Private Sub 绘制标签栏背景图片_D2D(rt As ID2D1RenderTarget, tabStripRect As Rectangle)
        If 标签栏背景图片 Is Nothing Then Return
        Dim img As Image = 标签栏背景图片
        Dim cw As Integer = tabStripRect.Width
        Dim ch As Integer = tabStripRect.Height
        If cw < 1 OrElse ch < 1 Then Return

        Dim bmpCache = _当前合成器?.GetBitmapCache(img)
        Dim bmp = bmpCache?.GetBitmap(rt, img)
        If bmp Is Nothing Then Return

        Dim ratioW As Single = CSng(cw) / img.Width
        Dim ratioH As Single = CSng(ch) / img.Height
        Dim ratio As Single = Math.Max(ratioW, ratioH)
        Dim drawW As Single = img.Width * ratio
        Dim drawH As Single = img.Height * ratio
        Dim dx As Single = tabStripRect.X + (cw - drawW) / 2.0F
        Dim dy As Single = tabStripRect.Y + (ch - drawH) / 2.0F

        rt.PushAxisAlignedClip(New Vortice.RawRectF(tabStripRect.Left, tabStripRect.Top, tabStripRect.Right, tabStripRect.Bottom), AntialiasMode.Aliased)
        Try
            rt.DrawBitmap(bmp, New Vortice.Mathematics.Rect(dx, dy, drawW, drawH), 1.0F, BitmapInterpolationMode.Linear, Nothing)
        Finally
            rt.PopAxisAlignedClip()
        End Try
    End Sub

    Private Sub 绘制分割线_D2D(rt As ID2D1RenderTarget, index As Integer)
        Dim bounds = 获取标签页项矩形(index)
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim lineX As Single = bounds.X + (bounds.Width - 1) / 2.0F
        rt.FillRectangle(D2DGlobals.ToD2DRect(New RectangleF(lineX, bounds.Y, 1, bounds.Height)), _当前合成器.BrushCache.Get(rt, 分割线颜色值))
    End Sub

    Private Sub 绘制标签页项图形_D2D(rt As ID2D1RenderTarget, index As Integer)
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
            填充圆角或矩形_D2D(rt, bounds, radius, bgColor)
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
                填充圆角或矩形_D2D(rt, indicatorRect, indicatorRadius, 选中指示条颜色)
            End If
        End If

        If isSelected AndAlso Me.Focused AndAlso 焦点边框颜色 <> Color.Empty Then
            Dim focusBounds = bounds
            focusBounds.Inflate(-1 * s, -1 * s)
            If focusBounds.Width > 0 AndAlso focusBounds.Height > 0 Then
                If radius > 0 Then
                    Using focusGeo = RectangleRenderer.创建圆角矩形几何(focusBounds, Math.Max(1, radius - 1 * s))
                        RectangleRenderer.绘制圆角边框_D2D(rt, focusGeo, 焦点边框颜色, 1.0F * s)
                    End Using
                Else
                    RectangleRenderer.绘制矩形边框_D2D(rt, focusBounds, 焦点边框颜色, 1.0F * s)
                End If
            End If
        End If

        绘制标签页图标_D2D(rt, index, bounds)
    End Sub

    Private Sub 填充圆角或矩形_D2D(rt As ID2D1RenderTarget, rect As RectangleF, radius As Single, color As Color)
        If color.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        If radius > 0 Then
            Using geo = RectangleRenderer.创建圆角矩形几何(rect, radius)
                rt.FillGeometry(geo, _当前合成器.BrushCache.Get(rt, color))
            End Using
        Else
            rt.FillRectangle(D2DGlobals.ToD2DRect(rect), _当前合成器.BrushCache.Get(rt, color))
        End If
    End Sub

    Private Sub 绘制标签页图标_D2D(rt As ID2D1RenderTarget, index As Integer, bounds As RectangleF)
        If index >= 项目列表.Count Then Return
        Dim item = 项目列表(index)
        绘制图标_D2D(rt, item.TabIcon, bounds)
    End Sub

    Private Sub 绘制图标_D2D(rt As ID2D1RenderTarget, img As Image, bounds As RectangleF)
        If img Is Nothing Then Return
        Dim cache = 获取图标缓存(img)
        Dim bmp = cache?.GetBitmap(rt, img)
        If bmp Is Nothing Then Return
        Dim s As Single = DpiScale()
        Dim iconSize As Single = 图标尺寸 * s
        Dim pad As Single = 标签页文本内边距 * s
        Dim iconRect As New RectangleF(bounds.X + pad, bounds.Y + (bounds.Height - iconSize) / 2.0F, iconSize, iconSize)
        rt.DrawBitmap(bmp, D2DGlobals.ToD2DRect(iconRect), 1.0F, BitmapInterpolationMode.Linear,
            New Vortice.Mathematics.Rect(0, 0, img.Width, img.Height))
    End Sub

    Private Sub 绘制折叠按钮图形_D2D(rt As ID2D1RenderTarget)
        Dim bounds = 获取折叠按钮矩形()
        If bounds.IsEmpty Then Return
        _折叠按钮缓存矩形 = bounds
        Dim s As Single = DpiScale()
        Dim bgColor As Color = 颜色插值(Color.FromArgb(0, 悬停标签页背景颜色), 悬停标签页背景颜色, _折叠按钮动画.当前值)
        If _折叠按钮动画.当前值 > 0.001F Then
            填充圆角或矩形_D2D(rt, bounds, 标签页圆角半径 * s, bgColor)
        End If
        绘制图标_D2D(rt, 折叠按钮图标值, bounds)
    End Sub

    Private Sub 绘制横向滚动条_D2D(rt As ID2D1RenderTarget)
        If _滚动条TrackRect.IsEmpty Then Return
        Dim s As Single = DpiScale()
        Dim barH As Integer = CInt(滚动条高度 * s)
        If _滚动条TrackRect.Width < 1 OrElse barH < 1 Then Return

        Dim trackY As Integer = _滚动条TrackRect.Y + (_滚动条TrackRect.Height - barH) \ 2
        If 滚动条轨道颜色.A > 0 Then
            Dim trackRadius As Integer = Math.Min(barH \ 2, _滚动条TrackRect.Width \ 2)
            填充圆角或矩形_D2D(rt, New RectangleF(_滚动条TrackRect.X, trackY, _滚动条TrackRect.Width, barH), trackRadius, 滚动条轨道颜色)
        End If

        Dim activeColor As Color = If(_滚动条IsDragging OrElse _滚动条IsHover, 滚动条悬停颜色, 滚动条滑块颜色)
        Dim thumbY As Integer = _滚动条ThumbRect.Y + (_滚动条ThumbRect.Height - barH) \ 2
        Dim thumbRadius As Integer = Math.Min(barH \ 2, _滚动条ThumbRect.Width \ 2)
        填充圆角或矩形_D2D(rt, New RectangleF(_滚动条ThumbRect.X, thumbY, _滚动条ThumbRect.Width, barH), thumbRadius, activeColor)
    End Sub

    Private Sub 绘制文本内容_D2D(rt As ID2D1RenderTarget)
        Dim stripRect = 获取标签栏矩形()
        rt.PushAxisAlignedClip(New Vortice.RawRectF(stripRect.Left, stripRect.Top, stripRect.Right, stripRect.Bottom), AntialiasMode.PerPrimitive)
        Try
            For i As Integer = 0 To 项目列表.Count - 1
                绘制标签页文本_D2D(rt, i)
            Next
            If Ribbon模式值 Then
                绘制折叠按钮文本_D2D(rt)
            End If
        Finally
            rt.PopAxisAlignedClip()
        End Try
    End Sub

    Private Sub 绘制标签页文本_D2D(rt As ID2D1RenderTarget, index As Integer)
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
        绘制单行居中文本_D2D(rt, item.Text, textFont, textRect, textColor)
    End Sub

    Private Sub 绘制折叠按钮文本_D2D(rt As ID2D1RenderTarget)
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
        绘制单行居中文本_D2D(rt, caption, textFont, textRect, textColor)
    End Sub

    Private Sub 绘制单行居中文本_D2D(rt As ID2D1RenderTarget, text As String, font As Font, rect As RectangleF, color As Color)
        If String.IsNullOrEmpty(text) OrElse rect.Width <= 0 OrElse rect.Height <= 0 OrElse color.A = 0 Then Return
        Dim weight As Vortice.DirectWrite.FontWeight = If(font.Bold, Vortice.DirectWrite.FontWeight.Bold, Vortice.DirectWrite.FontWeight.Normal)
        Dim style As Vortice.DirectWrite.FontStyle = If(font.Italic, Vortice.DirectWrite.FontStyle.Italic, Vortice.DirectWrite.FontStyle.Normal)
        Dim sizePx As Single = font.SizeInPoints * (96.0F / 72.0F) * DpiScale()
        Dim fmt = _当前合成器.TextFormatCache.Get(font.FontFamily.Name, weight, style, sizePx, TextAlignment.Center, ParagraphAlignment.Center, True)
        rt.DrawText(text, fmt, D2DGlobals.ToD2DRect(rect), _当前合成器.BrushCache.Get(rt, color), DrawTextOptions.Clip)
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
        同步内容面板布局()
    End Sub

    Private Sub 关闭浮层()
        If Not _浮层显示 Then Return
        _浮层显示 = False
        同步内容面板布局()
        Me.Invalidate()
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
        Invalidate()
    End Sub

    Protected Overrides Sub OnLayout(e As LayoutEventArgs)
        MyBase.OnLayout(e)
        同步内容面板布局()
        Invalidate()
    End Sub

    Protected Overrides Sub OnInvalidated(e As InvalidateEventArgs)
        _缓存宽度 = Nothing
        MyBase.OnInvalidated(e)
    End Sub

    Private _上一个父级 As Control = Nothing
    Protected Overrides Sub OnParentChanged(e As EventArgs)
        MyBase.OnParentChanged(e)
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
        If Ribbon模式值 Then
            If Not Me.Visible Then
                _内容面板.Visible = False
            Else
                同步内容面板布局()
            End If
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
        Me.Invalidate()
    End Sub

    Private Sub 启动动画驱动()
        If _动画中 Then Return
        _动画中 = True
        If _动画用Idle Then
            AddHandler Application.Idle, AddressOf 动画帧更新
        Else
            AddHandler _动画计时器.Tick, AddressOf 动画帧更新
            _动画计时器.Start()
        End If
    End Sub

    Friend Sub 停止动画驱动()
        If Not _动画中 Then Return
        _动画中 = False
        If _动画用Idle Then
            RemoveHandler Application.Idle, AddressOf 动画帧更新
        Else
            _动画计时器?.Stop()
            RemoveHandler _动画计时器.Tick, AddressOf 动画帧更新
        End If
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
            Me.Invalidate()
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
        If needInvalidate Then Me.Invalidate()
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
        If needInvalidate Then Me.Invalidate()
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
                Me.Invalidate()
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
            Me.Invalidate()
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
        Me.Invalidate()
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
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnLostFocus(e As EventArgs)
        MyBase.OnLostFocus(e)
        Me.Invalidate()
    End Sub
#End Region

#Region "通用属性"
    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            Me.Invalidate()
        End If
    End Sub

    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function

    Friend Sub InvalidateFontResources()
        D2DHelperV2.InvalidateTextFormatCache(Me)
        _缓存宽度 = Nothing
        _缓存可用宽度 = -1
    End Sub

    Friend Sub RefreshFontDependentRenderingNow()
        D2DHelperV2.RefreshFontDependentRendering(Me)
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        InvalidateFontResources()
        MyBase.OnFontChanged(e)
        D2DHelperV2.RefreshFontDependentRendering(Me)
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        Me.Invalidate()
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
    <Category("LakeUI"),
     Description("背景采样源（V2 透明背景穿透）。设置后将跨越任意层级直接采样此控件的绘制内容作为透明背景；为空时按 BackColor 协议处理。"),
     DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource IsNot value Then
                _backgroundSource = value
                ' 内容面板上的透明背景来自同一 source。
                If _内容面板 IsNot Nothing Then _内容面板.Invalidate(True)
                Me.Invalidate()
            End If
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
            Dim wasRunning = _动画中
            If wasRunning Then 停止动画驱动()
            动画帧率值 = value
            _动画用Idle = (动画帧率值 <= 0)
            If _动画用Idle Then
                _动画计时器?.Dispose()
                _动画计时器 = Nothing
            Else
                If _动画计时器 Is Nothing Then
                    _动画计时器 = New Timer()
                End If
                _动画计时器.Interval = Math.Max(1, CInt(1000.0 / 动画帧率值))
            End If
            If wasRunning Then 启动动画驱动()
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
                Me.Invalidate()
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
                _滚动偏移 = 0
                限制滚动范围()
                Me.Invalidate()
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
                Me.Invalidate()
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
                Me.Invalidate()
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
            Me.Invalidate()
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
            标签栏内边距 = value
            Me.Invalidate()
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
                Me.Invalidate()
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
            SetValue(标签页项间距, value)
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
            SetValue(标签页文本内边距, value)
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
            SetValue(图标尺寸, value)
        End Set
    End Property

    Private 图标与文本间距 As Integer = 6
    <Category("LakeUI"), Description("图标与文本之间的间距"), DefaultValue(6), Browsable(True)>
    Public Property TabIconTextSpacing As Integer
        Get
            Return 图标与文本间距
        End Get
        Set(value As Integer)
            SetValue(图标与文本间距, value)
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
            SetValue(分割线宽度值, value)
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
                _浮层显示 = False
                同步内容面板布局()
                限制滚动范围()
                Me.Invalidate()
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
                Me.Invalidate()
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
                Me.Invalidate()
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
                Me.Invalidate()
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
            折叠按钮折叠文本值 = If(value, "")
            Me.Invalidate()
        End Set
    End Property

    Private 折叠按钮展开文本值 As String = "▼"
    <Category("LakeUI"), Description("Ribbon 模式下折叠按钮在【折叠状态】时显示的文本（点击后会展开）"), DefaultValue("▼"), Browsable(True)>
    Public Property CollapseButtonExpandText As String
        Get
            Return 折叠按钮展开文本值
        End Get
        Set(value As String)
            折叠按钮展开文本值 = If(value, "")
            Me.Invalidate()
        End Set
    End Property

    Private 折叠按钮字体值 As Font = Nothing
    <Category("LakeUI"), Description("Ribbon 模式下折叠按钮使用的字体，为 Nothing 时使用控件默认字体"), Browsable(True)>
    Public Property CollapseButtonFont As Font
        Get
            Return 折叠按钮字体值
        End Get
        Set(value As Font)
            折叠按钮字体值 = value
            InvalidateFontResources()
            D2DHelperV2.RefreshFontDependentRendering(Me)
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
            折叠按钮图标值 = value
            Me.Invalidate()
        End Set
    End Property
#End Region

End Class
