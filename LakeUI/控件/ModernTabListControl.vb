Imports System.ComponentModel
Imports System.Drawing.Drawing2D
Imports System.Numerics
Imports Vortice.Direct2D1
Imports Vortice.DirectWrite

''' <summary>
''' 现代化选项卡列表控件。
''' 每个 <see cref="ModernTabPage"/> 可绑定一个 <see cref="Control"/> 作为内容，
''' 运行时自动切换其可见性。支持分割线和小字说明项。
''' </summary>
<DefaultEvent("SelectedIndexChanged")>
Public Class ModernTabListControl

    ''' <summary>
    ''' 表示 <see cref="ModernTabListControl"/> 中的一个选项卡项。
    ''' 支持独立设置标题字体、颜色、图标，以及绑定一个 <see cref="Control"/> 作为选项卡内容。
    ''' 也可设为分割线或小字说明项。
    ''' </summary>
    Public Class ModernTabPage

        Friend Property Owner As ModernTabListControl

        Private Sub 通知父级重绘()
            If Owner IsNot Nothing Then Owner.Invalidate()
        End Sub

        Private Sub 通知父级布局变更()
            If Owner Is Nothing Then Return
            Owner.项目布局属性已改变(Me)
        End Sub

        Private _text As String = "ModernTabPage"
        <Category("LakeUI"), Description("标签页标题文本"), DefaultValue("ModernTabPage"), Browsable(True)>
        Public Property Text As String
            Get
                Return _text
            End Get
            Set(value As String)
                value = If(value, "")
                If _text = value Then Return
                _text = value
                通知父级布局变更()
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
                Owner?.RefreshFontDependentRenderingNow()
                通知父级重绘()
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

        Private _isSeparator As Boolean
        <Category("LakeUI"), Description("是否是分割线"), DefaultValue(False), Browsable(True)>
        Public Property IsSeparator As Boolean
            Get
                Return _isSeparator
            End Get
            Set(value As Boolean)
                If _isSeparator = value Then Return
                _isSeparator = value
                通知父级布局变更()
            End Set
        End Property

        Private _isDescription As Boolean
        <Category("LakeUI"), Description("是否是小字说明项（不可选中）"), DefaultValue(False), Browsable(True)>
        Public Property IsDescription As Boolean
            Get
                Return _isDescription
            End Get
            Set(value As Boolean)
                If _isDescription = value Then Return
                _isDescription = value
                通知父级布局变更()
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
            If IsSeparator Then Return "─── Separator ───"
            If IsDescription Then Return $"[说明] {_text}"
            Return If(String.IsNullOrEmpty(_text), "ModernTabPage", _text)
        End Function

    End Class

#Region "内部类型"
    Public Enum TabSideEnum
        Left
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
    ''' 通过调用父级的 Paint 流程取真实像素作为背景。
    ''' </summary>
    Private Class 透明内容面板
        Inherits Panel
        Public Sub New()
            SetStyle(ControlStyles.SupportsTransparentBackColor Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.UserPaint Or
                     ControlStyles.ResizeRedraw, True)
            UpdateStyles()
        End Sub
        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            If BackColor.A < 255 Then
                ' V2 透明背景穿透：source 选择 ModernTabListControl 的 BackgroundSource，
                ' 若未指定再回退到祖父级控件（兼容旧行为）。
                Dim tabListCtrl As ModernTabListControl = TryCast(Me.Parent, ModernTabListControl)
                Dim source As Control = Nothing
                If tabListCtrl IsNot Nothing Then
                    source = If(tabListCtrl.BackgroundSource, tabListCtrl.Parent)
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

        Private Sub 解除背景穿透消费者()
            Try : BackgroundPenetrationV2.UnregisterConsumer(Me) : Catch : End Try
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
    End Class
#End Region

#Region "构造"
    Private Shared ReadOnly _共享Stopwatch As Stopwatch = Stopwatch.StartNew()
    Private ReadOnly _标签页动画 As New Dictionary(Of Integer, TabAnimState)
    Private _悬停索引 As Integer = -1
    Private ReadOnly _动画助手 As New AnimationHelperV2(Me)
    Private _hover动画激活 As Boolean = False
    Private _帧驱动激活 As Boolean = False
    Private ReadOnly _内容面板 As New 透明内容面板()
    Private ReadOnly 项目列表 As New List(Of ModernTabPage)
    Private _滚动偏移 As Single = 0
    Private _滚动目标 As Single = 0
    Private _滚动速度 As Single = 0
    Private _滚动动画中 As Boolean = False
    Private ReadOnly _标签栏滚动条 As New ScrollBarRenderer()

    ' D2D 资源
    ' V2：DC RT / SSAA / 位图缓存 / brush 缓存均由 WindowCompositor（Form 级共享）托管；
    ' 本控件不再持有任何长生命周期 D2D 字段。OnPaint 期间通过 _当前合成器 共享 compositor。
    Private _当前合成器 As WindowCompositor

    ' --- 渲染层缓存（统一 1 秒 TTL）---
    Private Const 缓存有效期Ms As Integer = 1000

    ' 布局缓存：把 O(n²) 的项位置累加压缩成一帧一次的 O(n) 预计算。
    Private _布局项高度数组 As Single() = Array.Empty(Of Single)()
    Private _布局项AbsY数组 As Single() = Array.Empty(Of Single)()
    Private _布局项可见数组 As Boolean() = Array.Empty(Of Boolean)()
    Private _布局总高度 As Single
    Private _布局视口高度 As Single
    Private _布局搜索区高度 As Single
    Private _布局项X As Single
    Private _布局项W As Single
    Private _布局有滚动条 As Boolean
    Private _布局标签栏矩形 As Rectangle
    Private _布局内容区矩形 As Rectangle
    Private _布局已生成 As Boolean = False

    ' DirectWrite TextFormat 缓存（V2：使用 compositor 共享 TextFormatCache；
    ' 仍保留本地条目记录省略号 trimming sign 以便释放）
    Private Class TextFormatEntry
        Public Format As IDWriteTextFormat
        Public Ellipsis As IDWriteInlineObject
        Public LastUseMs As Long
        Public OwnsFormat As Boolean
    End Class
    Private ReadOnly _textFormatCache As New Dictionary(Of String, TextFormatEntry)
    Private _textFormatLastSweepMs As Long = 0

    Private Function 获取项图标缓存(item As ModernTabPage) As D2DGlobals.D2DBitmapCache
        If item Is Nothing OrElse item.TabIcon Is Nothing Then Return Nothing
        If _当前合成器 Is Nothing Then Return Nothing
        Return _当前合成器.GetBitmapCache(item.TabIcon)
    End Function

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        解除背景穿透消费者()
        Try : BackgroundPenetrationV2.UnregisterConsumer(_内容面板) : Catch : End Try
        Try : 停止帧驱动() : Catch : End Try
        ReleaseLocalTextFormatCache()
        ReleaseMoreIndicatorFont()
        MyBase.OnHandleDestroyed(e)
    End Sub

    Protected Overrides Sub OnVisibleChanged(e As EventArgs)
        MyBase.OnVisibleChanged(e)
        If Not Me.Visible Then
            解除背景穿透消费者()
            Try : BackgroundPenetrationV2.UnregisterConsumer(_内容面板) : Catch : End Try
        End If
    End Sub

    Protected Overrides Sub OnParentChanged(e As EventArgs)
        MyBase.OnParentChanged(e)
        If Me.Parent Is Nothing Then
            解除背景穿透消费者()
            Try : BackgroundPenetrationV2.UnregisterConsumer(_内容面板) : Catch : End Try
        End If
    End Sub

    Private Sub ReleaseMoreIndicatorFont()
        If _moreIndicatorFont IsNot Nothing Then
            Try : _moreIndicatorFont.Dispose() : Catch : End Try
            _moreIndicatorFont = Nothing
        End If
        _moreIndicatorFontKey = Nothing
    End Sub

    Private Sub ReleaseLocalTextFormatCache()
        Try
            For Each kv In _textFormatCache
                Try : kv.Value.Ellipsis?.Dispose() : Catch : End Try
                If kv.Value.OwnsFormat Then
                    Try : kv.Value.Format?.Dispose() : Catch : End Try
                End If
            Next
            _textFormatCache.Clear()
        Catch
        End Try
    End Sub
    Private _搜索框控件 As Control = Nothing
    Private _搜索文本 As String = ""
    Private _搜索词元缓存 As String() = Array.Empty(Of String)()
    Private _搜索框高度 As Integer = 30
    Private _搜索框原始父级 As Control = Nothing
    Private _搜索框原始边界 As Rectangle

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
        _内容面板.BackColor = 获取内容面板有效背景色()
        Me.Controls.Add(_内容面板)

        _动画助手.DirtyProvider = AddressOf 帧驱动脏区
        _动画助手.FPS = 动画帧率值
        同步内容面板布局()
    End Sub

    <Category("LakeUI"), Description("选项卡项集合"), Browsable(True)>
    <DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
    Public ReadOnly Property Items As List(Of ModernTabPage)
        Get
            Return 项目列表
        End Get
    End Property

    ''' <summary>
    ''' 刷新所有项的内部引用并同步绑定控件显示。在运行时增删项后调用。
    ''' </summary>
    Public Sub RefreshItems()
        确保Owner()
        失效布局缓存()
        标准化选中索引()
        _标签页动画.Clear()
        _悬停索引 = -1
        限制滚动范围()
        切换绑定控件()
        Invalidate()
    End Sub

    Private Sub 项目布局属性已改变(item As ModernTabPage)
        失效布局缓存()
        If item IsNot Nothing AndAlso 项目列表.IndexOf(item) = _selectedIndex AndAlso (item.IsSeparator OrElse item.IsDescription) Then
            _selectedIndex = 第一个可选索引()
            切换绑定控件()
            RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
        End If
        限制滚动范围()
        Invalidate()
    End Sub

    Private Sub 项目绑定控件已改变(item As ModernTabPage, oldControl As Control)
        If oldControl IsNot Nothing Then
            oldControl.Visible = False
            If oldControl.Parent Is _内容面板 Then
                _内容面板.Controls.Remove(oldControl)
            End If
        End If
        If item IsNot Nothing AndAlso 项目列表.IndexOf(item) = _selectedIndex Then
            切换绑定控件()
        End If
        Invalidate()
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
            If item Is Nothing OrElse item.IsSeparator OrElse item.IsDescription Then
                _selectedIndex = 第一个可选索引()
            End If
        End If
    End Sub

    Private Sub 同步搜索框布局()
        If _搜索框控件 Is Nothing Then Return
        Dim s As Single = DpiScale()
        Dim tabStripRect = 获取标签栏矩形()
        Dim x As Integer = CInt(tabStripRect.X + 标签栏内边距.Left * s)
        Dim y As Integer = CInt(标签栏内边距.Top * s)
        Dim w As Integer = Math.Max(0, CInt(tabStripRect.Width - (标签栏内边距.Left + 标签栏内边距.Right) * s))
        _搜索框控件.SetBounds(x, y, w, CInt(_搜索框高度 * s))
    End Sub

    Private Shared Function 搜索词元(text As String) As String()
        If String.IsNullOrWhiteSpace(text) Then Return Array.Empty(Of String)()

        Dim tokens As New List(Of String)()
        Dim current As New System.Text.StringBuilder()
        For Each ch As Char In text
            If Char.IsWhiteSpace(ch) Then
                If current.Length > 0 Then
                    Dim token As String = current.ToString().ToLowerInvariant()
                    If tokens.IndexOf(token) < 0 Then tokens.Add(token)
                    current.Clear()
                End If
            Else
                current.Append(Char.ToLowerInvariant(ch))
            End If
        Next

        If current.Length > 0 Then
            Dim token As String = current.ToString().ToLowerInvariant()
            If tokens.IndexOf(token) < 0 Then tokens.Add(token)
        End If

        Return tokens.ToArray()
    End Function

    Private Shared Function 标题匹配搜索(text As String, searchTokens As String()) As Boolean
        If searchTokens Is Nothing OrElse searchTokens.Length = 0 Then Return True
        If String.IsNullOrWhiteSpace(text) Then Return False

        For Each token As String In searchTokens
            If token.Length = 0 Then Continue For
            If text.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0 Then Return False
        Next
        Return True
    End Function

    Private Sub 搜索框文本变更(sender As Object, e As EventArgs)
        Dim ctrl = TryCast(sender, Control)
        If ctrl Is Nothing Then Return
        _搜索文本 = If(ctrl.Text, "")
        _搜索词元缓存 = 搜索词元(_搜索文本)
        _滚动偏移 = 0
        失效布局缓存()
        限制滚动范围()
        Invalidate()
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
                If item.IsSeparator OrElse item.IsDescription Then Return
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

    <Category("LakeUI"), Description("当前选中的选项卡页面，为 Nothing 时表示未选中任何项"), DefaultValue(GetType(ModernTabPage), Nothing), Browsable(False)>
    Public Property SelectedTabPage As ModernTabPage
        Get
            If _selectedIndex >= 0 AndAlso _selectedIndex < 项目列表.Count Then
                Return 项目列表(_selectedIndex)
            End If
            Return Nothing
        End Get
        Set(value As ModernTabPage)
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
            If Not 项目是否可见(i) Then Continue For
            If Not 项目列表(i).IsSeparator AndAlso Not 项目列表(i).IsDescription Then Return i
        Next
        Return fromIndex
    End Function

    Private Function 下一个可选索引(fromIndex As Integer) As Integer
        For i As Integer = fromIndex + 1 To 项目列表.Count - 1
            If Not 项目是否可见(i) Then Continue For
            If Not 项目列表(i).IsSeparator AndAlso Not 项目列表(i).IsDescription Then Return i
        Next
        Return fromIndex
    End Function

    Private Function 第一个可选索引() As Integer
        For i As Integer = 0 To 项目列表.Count - 1
            If Not 项目是否可见(i) Then Continue For
            If Not 项目列表(i).IsSeparator AndAlso Not 项目列表(i).IsDescription Then Return i
        Next
        Return -1
    End Function

    Private Function 最后一个可选索引() As Integer
        For i As Integer = 项目列表.Count - 1 To 0 Step -1
            If Not 项目是否可见(i) Then Continue For
            If Not 项目列表(i).IsSeparator AndAlso Not 项目列表(i).IsDescription Then Return i
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
        ' V2 契约（与 ModernButton 对齐）：
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

        Dim ssaa As Integer = D2DHelperV2.GetEffectiveSsaaScale(超采样倍率)

        Using scope = D2DHelperV2.BeginPaint(e, Me, ssaa)
            If scope Is Nothing Then Return  ' 设计期或无 Form 上下文
            _当前合成器 = scope.Compositor
            Try
                Dim compositor = scope.Compositor
                Dim gRT As ID2D1RenderTarget = scope.GraphicsLayer
                Dim dcRT As ID2D1DCRenderTarget = scope.DCRenderTarget

                ' 1) 背景层（1× 直绘）：
                '    • 显式 BackgroundSource → 绘制穿透底图（跳过 BackColor）；
                '    • 否则若 MyBase.BackColor 半透明 → 基类 OnPaintBackground 已把父级背景合成到 DC，
                '      这里再叠加 BackColor 作为半透明遮罩（"颜色覆盖在上面"）。
                If _backgroundSource IsNot Nothing Then
                    BackgroundPenetrationV2.PaintBackground(Me, scope, _backgroundSource)
                ElseIf MyBase.BackColor.A > 0 AndAlso MyBase.BackColor.A < 255 Then
                    Dim bgLayer = scope.BackgroundLayer
                    Dim brush = compositor.BrushCache.[Get](bgLayer, MyBase.BackColor)
                    If brush IsNot Nothing Then
                        bgLayer.FillRectangle(D2DGlobals.ToD2DRect(New RectangleF(0, 0, Me.Width, Me.Height)), brush)
                    End If
                End If

                ' 2) 图形层（享受 SSAA）
                绘制图形内容_D2D(gRT)

                ' 3) 把图形层回采到 DC，然后在 DC RT 上绘制文字（DirectWrite 子像素质量）
                scope.FlushGraphics()
                绘制文本与指示器符号_D2D(dcRT)

                ' 4) 滚动条直接画在 DC RT 上（无需 SSAA）
                If Not _标签栏滚动条.TrackRect.IsEmpty Then
                    Dim sbContainerW As Integer = If(标签页位置 = TabSideEnum.Left, CInt(标签栏宽度 * DpiScale()), Me.Width)
                    _标签栏滚动条.Draw_D2D(dcRT, sbContainerW, Me.Height, 0, 0, CInt(滚动条宽度 * DpiScale()),
                                           滚动条轨道颜色, 滚动条滑块颜色, 滚动条悬停颜色, compositor.BrushCache)
                End If
            Finally
                _当前合成器 = Nothing
            End Try
        End Using
    End Sub

    Private Sub 绘制图形内容_D2D(rt As ID2D1RenderTarget)
        ' 内容区域背景
        If 内容区域背景颜色.A = 255 Then
            Dim br = _当前合成器.BrushCache.Get(rt, 内容区域背景颜色)
            If br IsNot Nothing Then rt.FillRectangle(D2DGlobals.ToD2DRect(获取内容区域矩形()), br)
        End If

        Dim tabStripRect = 获取标签栏矩形()
        ' 标签栏背景
        If 标签栏背景颜色.A > 0 Then
            Dim br = _当前合成器.BrushCache.Get(rt, 标签栏背景颜色)
            If br IsNot Nothing Then rt.FillRectangle(D2DGlobals.ToD2DRect(tabStripRect), br)
        End If

        ' 标签栏背景图片（居中裁切）
        绘制标签栏背景图片_D2D(rt, tabStripRect)

        ' 标签栏遮罩
        If 标签栏遮罩颜色.A > 0 Then
            Dim br = _当前合成器.BrushCache.Get(rt, 标签栏遮罩颜色)
            If br IsNot Nothing Then rt.FillRectangle(D2DGlobals.ToD2DRect(tabStripRect), br)
        End If

        Dim tabItemClip As Rectangle = tabStripRect
        Dim searchAreaH As Integer = CInt(获取搜索框区域高度())
        If searchAreaH > 0 Then
            tabItemClip = New Rectangle(tabItemClip.X, tabItemClip.Y + searchAreaH, tabItemClip.Width, Math.Max(0, tabItemClip.Height - searchAreaH))
        End If

        Dim totalTabH As Integer = CInt(获取标签页总高度())
        Dim viewportTabH As Integer = CInt(获取可滚动视口高度())
        Dim hasMoreAbove As Boolean = _滚动偏移 > 0.5F
        Dim hasMoreBelow As Boolean = (totalTabH > viewportTabH) AndAlso (totalTabH - _滚动偏移 > viewportTabH + 0.5F)
        Dim scaledIndicatorH As Integer = CInt(更多指示器高度 * DpiScale())
        Dim topIndicatorH As Integer = If(hasMoreAbove, scaledIndicatorH, 0)
        Dim bottomIndicatorH As Integer = If(hasMoreBelow, scaledIndicatorH, 0)

        Dim clippedTabItemClip As Rectangle = tabItemClip
        If topIndicatorH > 0 Then
            clippedTabItemClip = New Rectangle(clippedTabItemClip.X, clippedTabItemClip.Y + topIndicatorH, clippedTabItemClip.Width, Math.Max(0, clippedTabItemClip.Height - topIndicatorH))
        End If
        If bottomIndicatorH > 0 Then
            clippedTabItemClip = New Rectangle(clippedTabItemClip.X, clippedTabItemClip.Y, clippedTabItemClip.Width, Math.Max(0, clippedTabItemClip.Height - bottomIndicatorH))
        End If

        ' 标签项裁剪绘制
        If clippedTabItemClip.Width > 0 AndAlso clippedTabItemClip.Height > 0 Then
            rt.PushAxisAlignedClip(New Vortice.RawRectF(clippedTabItemClip.Left, clippedTabItemClip.Top, clippedTabItemClip.Right, clippedTabItemClip.Bottom), AntialiasMode.Aliased)
            Try
                For i As Integer = 0 To 项目列表.Count - 1
                    If Not 项目是否可见(i) Then Continue For
                    Dim itemRect = 获取标签页项矩形(i)
                    If itemRect.Top >= clippedTabItemClip.Bottom Then Exit For
                    If itemRect.Bottom <= clippedTabItemClip.Top Then Continue For
                    Dim item = 项目列表(i)
                    If item.IsSeparator Then
                        绘制分割线_D2D(rt, i)
                    ElseIf Not item.IsDescription Then
                        绘制标签页项图形_D2D(rt, i)
                    End If
                Next
            Finally
                rt.PopAxisAlignedClip()
            End Try
        End If

        ' 上/下"更多内容"指示器仅绘制箭头符号（在文本层），此处不再绘制背景

        ' 同步滚动条布局（绘制留到 OnPaint 末尾，画在 DC RT 上）
        更新滚动条布局()

        ' 内容区域边框
        If 内容区域边框宽度 > 0 Then
            Dim contentRect = 获取内容区域矩形()
            Dim r As New RectangleF(contentRect.X, contentRect.Y, contentRect.Width - 1, contentRect.Height - 1)
            RectangleRenderer.绘制矩形边框_D2D(rt, r, 内容区域边框颜色, 内容区域边框宽度 * DpiScale(), _当前合成器.BrushCache)
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
        Dim lineH As Single = Math.Max(1, DpiScale())
        Dim lineY As Single = bounds.Y + (bounds.Height - lineH) / 2.0F
        Dim br = _当前合成器.BrushCache.Get(rt, 分割线颜色值)
        If br IsNot Nothing Then rt.FillRectangle(New Vortice.Mathematics.Rect(bounds.X, lineY, bounds.Width, lineH), br)
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

        If (isSelected OrElse hoverProgress > 0.001F) AndAlso bgColor.A > 0 Then
            If 标签页圆角半径 > 0 Then
                Using geo = RectangleRenderer.创建圆角矩形几何(bounds, 标签页圆角半径 * s)
                    Dim br = _当前合成器.BrushCache.Get(rt, bgColor)
                    If br IsNot Nothing Then rt.FillGeometry(geo, br)
                End Using
            Else
                Dim br = _当前合成器.BrushCache.Get(rt, bgColor)
                If br IsNot Nothing Then rt.FillRectangle(D2DGlobals.ToD2DRect(bounds), br)
            End If
        End If

        If isSelected AndAlso 选中指示条宽度 > 0 Then
            Dim indicatorRect As RectangleF
            If 标签页位置 = TabSideEnum.Left Then
                indicatorRect = New RectangleF(bounds.X, bounds.Y + 选中指示条边距 * s, 选中指示条宽度 * s, bounds.Height - 选中指示条边距 * s * 2)
            Else
                indicatorRect = New RectangleF(bounds.Right - 选中指示条宽度 * s, bounds.Y + 选中指示条边距 * s, 选中指示条宽度 * s, bounds.Height - 选中指示条边距 * s * 2)
            End If
            If indicatorRect.Width > 0 AndAlso indicatorRect.Height > 0 AndAlso 选中指示条圆角半径 > 0 Then
                Using geo = RectangleRenderer.创建圆角矩形几何(indicatorRect, 选中指示条圆角半径 * s)
                    Dim br = _当前合成器.BrushCache.Get(rt, 选中指示条颜色)
                    If br IsNot Nothing Then rt.FillGeometry(geo, br)
                End Using
            ElseIf indicatorRect.Width > 0 AndAlso indicatorRect.Height > 0 Then
                Dim br = _当前合成器.BrushCache.Get(rt, 选中指示条颜色)
                If br IsNot Nothing Then rt.FillRectangle(D2DGlobals.ToD2DRect(indicatorRect), br)
            End If
        End If

        If isSelected AndAlso Me.Focused AndAlso 焦点边框颜色 <> Color.Empty Then
            Dim focusBounds = bounds
            focusBounds.Inflate(-s, -s)
            If 标签页圆角半径 > 0 Then
                RectangleRenderer.绘制圆角边框_D2D(rt, focusBounds, Math.Max(1, 标签页圆角半径 * s - s), 焦点边框颜色, s, _当前合成器.BrushCache)
            Else
                RectangleRenderer.绘制矩形边框_D2D(rt, focusBounds, 焦点边框颜色, s, _当前合成器.BrushCache)
            End If
        End If

        绘制标签页图标_D2D(rt, index, bounds)
    End Sub

    Private Sub 绘制标签页图标_D2D(rt As ID2D1RenderTarget, index As Integer, bounds As RectangleF)
        If index >= 项目列表.Count Then Return
        Dim item = 项目列表(index)
        If item.TabIcon Is Nothing Then Return

        Dim s As Single = DpiScale()
        Dim scaledIconSize As Single = 图标尺寸 * s
        If scaledIconSize <= 0 OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim iconX As Single = bounds.X + 标签页文本左边距 * s
        Dim iconY As Single = bounds.Y + (bounds.Height - scaledIconSize) / 2.0F
        Dim cache = 获取项图标缓存(item)
        Dim bmp = cache?.GetBitmap(rt, item.TabIcon)
        If bmp IsNot Nothing Then
            rt.DrawBitmap(bmp, New Vortice.Mathematics.Rect(iconX, iconY, scaledIconSize, scaledIconSize), 1.0F, BitmapInterpolationMode.Linear, Nothing)
        End If
    End Sub

    ''' <summary>在 DC RT 上绘制所有文字（标签项 / 说明项 / "更多" 三角符号）。</summary>
    Private Sub 绘制文本与指示器符号_D2D(rt As ID2D1DCRenderTarget)
        Dim tabStripRect = 获取标签栏矩形()
        Dim tabItemClip As Rectangle = tabStripRect
        Dim searchAreaH As Integer = CInt(获取搜索框区域高度())
        If searchAreaH > 0 Then
            tabItemClip = New Rectangle(tabItemClip.X, tabItemClip.Y + searchAreaH, tabItemClip.Width, Math.Max(0, tabItemClip.Height - searchAreaH))
        End If

        Dim totalTabH As Integer = CInt(获取标签页总高度())
        Dim viewportTabH As Integer = CInt(获取可滚动视口高度())
        Dim hasMoreAbove As Boolean = _滚动偏移 > 0.5F
        Dim hasMoreBelow As Boolean = (totalTabH > viewportTabH) AndAlso (totalTabH - _滚动偏移 > viewportTabH + 0.5F)
        Dim scaledIndicatorH As Integer = CInt(更多指示器高度 * DpiScale())
        Dim topIndicatorH As Integer = If(hasMoreAbove, scaledIndicatorH, 0)
        Dim bottomIndicatorH As Integer = If(hasMoreBelow, scaledIndicatorH, 0)
        Dim clippedTabItemClip As Rectangle = tabItemClip
        If topIndicatorH > 0 Then
            clippedTabItemClip = New Rectangle(clippedTabItemClip.X, clippedTabItemClip.Y + topIndicatorH, clippedTabItemClip.Width, Math.Max(0, clippedTabItemClip.Height - topIndicatorH))
        End If
        If bottomIndicatorH > 0 Then
            clippedTabItemClip = New Rectangle(clippedTabItemClip.X, clippedTabItemClip.Y, clippedTabItemClip.Width, Math.Max(0, clippedTabItemClip.Height - bottomIndicatorH))
        End If

        Dim dw = D2DGlobals.GetDWriteFactory()

        ' 绘制标签项与说明项文本
        If clippedTabItemClip.Width > 0 AndAlso clippedTabItemClip.Height > 0 Then
            rt.PushAxisAlignedClip(New Vortice.RawRectF(clippedTabItemClip.Left, clippedTabItemClip.Top, clippedTabItemClip.Right, clippedTabItemClip.Bottom), AntialiasMode.Aliased)
            Try
                For i As Integer = 0 To 项目列表.Count - 1
                    If Not 项目是否可见(i) Then Continue For
                    Dim itemRect = 获取标签页项矩形(i)
                    If itemRect.Top >= clippedTabItemClip.Bottom Then Exit For
                    If itemRect.Bottom <= clippedTabItemClip.Top Then Continue For
                    绘制标签页文本_D2D(rt, dw, i)
                Next
            Finally
                rt.PopAxisAlignedClip()
            End Try
        End If

        ' "更多内容"指示符号
        If hasMoreAbove Then
            绘制更多指示器符号_D2D(rt, dw, New RectangleF(tabItemClip.X, tabItemClip.Y, tabItemClip.Width, topIndicatorH), True)
        End If
        If hasMoreBelow Then
            绘制更多指示器符号_D2D(rt, dw, New RectangleF(tabItemClip.X, tabItemClip.Bottom - bottomIndicatorH, tabItemClip.Width, bottomIndicatorH), False)
        End If
    End Sub

    Private Sub 绘制标签页文本_D2D(rt As ID2D1RenderTarget, dw As IDWriteFactory, index As Integer)
        If index >= 项目列表.Count Then Return
        Dim item = 项目列表(index)
        If item.IsSeparator Then Return

        Dim s As Single = DpiScale()
        Dim bounds As RectangleF = 获取标签页项矩形(index)
        Dim _textPad As Single = 标签页文本左边距 * s

        If item.IsDescription Then
            Dim descFont As Font = If(item.TabFont, 说明字体值)
            Dim descColor As Color = If(item.NormalForeColor <> Color.Empty, item.NormalForeColor, 说明文本颜色值)
            Dim r As New RectangleF(bounds.X + _textPad, bounds.Y, bounds.Width - _textPad * 2, bounds.Height)
            If r.Width <= 0 OrElse r.Height <= 0 Then Return
            画文本_D2D(rt, dw, item.Text, descFont, r, descColor, Vortice.DirectWrite.TextAlignment.Leading)
            Return
        End If

        Dim isSelected As Boolean = (_selectedIndex = index)
        Dim textColor As Color = If(isSelected,
            If(item.SelectedForeColor <> Color.Empty, item.SelectedForeColor, 选中标签页文本颜色),
            If(item.NormalForeColor <> Color.Empty, item.NormalForeColor, 标签页默认文本颜色))
        Dim textFont As Font = If(item.TabFont, Me.Font)
        Dim iconOffset As Single = 0
        If item.TabIcon IsNot Nothing Then
            iconOffset = (图标尺寸 + 图标与文本间距) * s
        End If
        Dim r2 As New RectangleF(bounds.X + _textPad + iconOffset, bounds.Y, bounds.Width - _textPad * 2 - iconOffset, bounds.Height)
        If r2.Width <= 0 OrElse r2.Height <= 0 Then Return
        画文本_D2D(rt, dw, item.Text, textFont, r2, textColor, Vortice.DirectWrite.TextAlignment.Leading)
    End Sub

    Private _moreIndicatorFont As Font
    Private _moreIndicatorFontKey As String

    Private Function 获取更多指示器字体() As Font
        Dim sz As Single = Math.Max(7, Me.Font.Size - 1)
        Dim key As String = Me.Font.FontFamily.Name & "|" & sz.ToString(Globalization.CultureInfo.InvariantCulture)
        If _moreIndicatorFont IsNot Nothing AndAlso _moreIndicatorFontKey = key Then
            Return _moreIndicatorFont
        End If
        If _moreIndicatorFont IsNot Nothing Then
            Try : _moreIndicatorFont.Dispose() : Catch : End Try
        End If
        _moreIndicatorFont = New Font(Me.Font.FontFamily, sz, System.Drawing.FontStyle.Regular)
        _moreIndicatorFontKey = key
        Return _moreIndicatorFont
    End Function

    Private Sub 绘制更多指示器符号_D2D(rt As ID2D1RenderTarget, dw As IDWriteFactory, rect As RectangleF, isTop As Boolean)
        If rect.Height < 2 Then Return
        Dim symbol As String = If(isTop, "▲", "▼")
        画文本_D2D(rt, dw, symbol, 获取更多指示器字体(), rect, 更多指示器颜色, Vortice.DirectWrite.TextAlignment.Center)
    End Sub

    ''' <summary>DirectWrite 单行文本绘制（垂直居中、末尾省略号）。</summary>
    Private Sub 画文本_D2D(rt As ID2D1RenderTarget, dw As IDWriteFactory,
                          text As String, font As Font, rect As RectangleF, color As Color,
                          hAlign As Vortice.DirectWrite.TextAlignment)
        If String.IsNullOrEmpty(text) OrElse rect.Width <= 0 OrElse rect.Height <= 0 OrElse color.A = 0 Then Return
        Dim s As Single = DpiScale()
        Dim sizePx As Single = font.SizeInPoints * (96.0F / 72.0F) * s
        Dim weight As Vortice.DirectWrite.FontWeight = If(font.Bold, Vortice.DirectWrite.FontWeight.Bold, Vortice.DirectWrite.FontWeight.Normal)
        Dim style As Vortice.DirectWrite.FontStyle = If(font.Italic, Vortice.DirectWrite.FontStyle.Italic, Vortice.DirectWrite.FontStyle.Normal)
        Dim familyName As String = font.FontFamily.Name
        Dim entry = 获取或创建TextFormat(dw, familyName, sizePx, weight, style, hAlign)
        If entry Is Nothing OrElse entry.Format Is Nothing Then Return
        Dim fmt = entry.Format
        Using layout = dw.CreateTextLayout(text, fmt, rect.Width, rect.Height)
            If entry.Ellipsis IsNot Nothing Then
                Try
                    Dim trim As New Trimming With {.Granularity = TrimmingGranularity.Character, .Delimiter = 0, .DelimiterCount = 0}
                    layout.SetTrimming(trim, entry.Ellipsis)
                Catch
                End Try
            End If
            Dim br = _当前合成器?.BrushCache.Get(rt, color)
            If br IsNot Nothing Then rt.DrawTextLayout(New Vector2(rect.X, rect.Y), layout, br)
        End Using
    End Sub

    ''' <summary>
    ''' 缓存按 (family/sizePx/weight/style/align) 命中的 TextFormat 与 ellipsis trimming sign。
    ''' 1 秒未使用即清扫，避免长期持有冷条目。
    ''' </summary>
    Private Function 获取或创建TextFormat(dw As IDWriteFactory,
                                          family As String, sizePx As Single,
                                          weight As Vortice.DirectWrite.FontWeight,
                                          style As Vortice.DirectWrite.FontStyle,
                                          hAlign As Vortice.DirectWrite.TextAlignment) As TextFormatEntry
        Dim now As Long = _共享Stopwatch.ElapsedMilliseconds
        Dim key As String = $"{family}|{sizePx:F2}|{CInt(weight)}|{CInt(style)}|{CInt(hAlign)}"
        Dim entry As TextFormatEntry = Nothing
        If _textFormatCache.TryGetValue(key, entry) Then
            If Not entry.OwnsFormat Then
                Dim sharedFormat = 获取共享TextFormat(family, sizePx, weight, style, hAlign)
                If sharedFormat Is Nothing Then Return Nothing
                If Not Object.ReferenceEquals(entry.Format, sharedFormat) Then
                    Try : entry.Ellipsis?.Dispose() : Catch : End Try
                    entry.Format = sharedFormat
                    entry.Ellipsis = Nothing
                    Try : entry.Ellipsis = dw.CreateEllipsisTrimmingSign(entry.Format) : Catch : End Try
                End If
            End If
            entry.LastUseMs = now
            清扫TextFormat缓存(now)
            Return entry
        End If
        Dim ownsFormat As Boolean = False
        Dim fmt = 获取TextFormat(family, sizePx, weight, style, hAlign, ownsFormat)
        If fmt Is Nothing Then Return Nothing
        Dim ellipsis As IDWriteInlineObject = Nothing
        Try : ellipsis = dw.CreateEllipsisTrimmingSign(fmt) : Catch : End Try
        entry = New TextFormatEntry With {.Format = fmt, .Ellipsis = ellipsis, .LastUseMs = now, .OwnsFormat = ownsFormat}
        _textFormatCache(key) = entry
        清扫TextFormat缓存(now)
        Return entry
    End Function

    Private Function 获取TextFormat(family As String, sizePx As Single,
                                    weight As Vortice.DirectWrite.FontWeight,
                                    style As Vortice.DirectWrite.FontStyle,
                                    hAlign As Vortice.DirectWrite.TextAlignment,
                                    ByRef ownsFormat As Boolean) As IDWriteTextFormat
        Dim sharedFormat = 获取共享TextFormat(family, sizePx, weight, style, hAlign)
        If sharedFormat IsNot Nothing Then
            ownsFormat = False
            Return sharedFormat
        End If

        ownsFormat = True
        Dim fmt = D2DGlobals.CreateTextFormat(family, weight, style, Vortice.DirectWrite.FontStretch.Normal, sizePx)
        fmt.TextAlignment = hAlign
        fmt.WordWrapping = WordWrapping.NoWrap
        fmt.ParagraphAlignment = ParagraphAlignment.Center
        Return fmt
    End Function

    Private Function 获取共享TextFormat(family As String, sizePx As Single,
                                      weight As Vortice.DirectWrite.FontWeight,
                                      style As Vortice.DirectWrite.FontStyle,
                                      hAlign As Vortice.DirectWrite.TextAlignment) As IDWriteTextFormat
        Return _当前合成器?.TextFormatCache.[Get](family, weight, style, sizePx,
                                                 hAlign, ParagraphAlignment.Center,
                                                 False, False)
    End Function

    Private Sub 清扫TextFormat缓存(now As Long)
        If now - _textFormatLastSweepMs < 缓存有效期Ms Then Return
        _textFormatLastSweepMs = now
        Dim toRemove As List(Of String) = Nothing
        For Each kv In _textFormatCache
            If now - kv.Value.LastUseMs > 缓存有效期Ms Then
                If toRemove Is Nothing Then toRemove = New List(Of String)()
                toRemove.Add(kv.Key)
            End If
        Next
        If toRemove IsNot Nothing Then
            For Each k In toRemove
                Dim e = _textFormatCache(k)
                Try : e.Ellipsis?.Dispose() : Catch : End Try
                If e.OwnsFormat Then
                    Try : e.Format?.Dispose() : Catch : End Try
                End If
                _textFormatCache.Remove(k)
            Next
        End If
    End Sub
#End Region

#Region "布局"
    ''' <summary>按显式失效重建布局，后续几何查询直接复用数组。</summary>
    Private Sub 确保布局缓存()
        If _布局已生成 Then Return

        Dim s As Single = DpiScale()
        Dim count As Integer = 项目列表.Count

        ' 标签栏 / 内容区矩形
        Dim w As Integer = CInt(标签栏宽度 * s)
        If 标签页位置 = TabSideEnum.Left Then
            _布局标签栏矩形 = New Rectangle(0, 0, w, Me.Height)
            _布局内容区矩形 = New Rectangle(w, 0, Math.Max(0, Me.Width - w), Me.Height)
        Else
            _布局标签栏矩形 = New Rectangle(Me.Width - w, 0, w, Me.Height)
            _布局内容区矩形 = New Rectangle(0, 0, Math.Max(0, Me.Width - w), Me.Height)
        End If

        _布局搜索区高度 = If(_搜索框控件 Is Nothing, 0, 标签栏内边距.Top * s + _搜索框高度 * s)
        _布局视口高度 = Math.Max(0, Me.Height - _布局搜索区高度)

        ' 项可见 / 高度 / 总高
        Dim requiredCapacity As Integer = Math.Max(count, 4)
        If _布局项高度数组.Length < count OrElse _布局项高度数组.Length > requiredCapacity * 2 Then
            ReDim _布局项高度数组(requiredCapacity - 1)
            ReDim _布局项AbsY数组(requiredCapacity - 1)
            ReDim _布局项可见数组(requiredCapacity - 1)
        End If

        Dim searchTokens As String() = _搜索词元缓存
        Dim hasSearch As Boolean = searchTokens.Length > 0
        Dim total As Single = 标签栏内边距.Top * s
        Dim visibleCount As Integer = 0
        For i = 0 To count - 1
            Dim it = 项目列表(i)
            Dim visible As Boolean
            If Not hasSearch Then
                visible = True
            ElseIf it.IsSeparator OrElse it.IsDescription Then
                visible = True
            Else
                visible = 标题匹配搜索(it.Text, searchTokens)
            End If
            _布局项可见数组(i) = visible

            Dim itemH As Single
            If it.IsSeparator Then
                itemH = 分割线高度值 * s
            ElseIf it.IsDescription Then
                itemH = 说明项高度值 * s
            Else
                itemH = 标签页项高度 * s
            End If
            _布局项高度数组(i) = itemH

            If visible Then
                If visibleCount > 0 Then total += 标签页项间距 * s
                _布局项AbsY数组(i) = total
                total += itemH
                visibleCount += 1
            Else
                _布局项AbsY数组(i) = total
            End If
        Next
        total += 标签栏内边距.Bottom * s
        _布局总高度 = total

        _布局有滚动条 = (count > 0 AndAlso _布局总高度 > _布局视口高度)

        Dim x As Single = _布局标签栏矩形.X + 标签栏内边距.Left * s
        Dim itemW As Single = _布局标签栏矩形.Width - (标签栏内边距.Left + 标签栏内边距.Right) * s
        If _布局有滚动条 Then itemW -= 滚动条宽度 * s
        _布局项X = x
        _布局项W = Math.Max(0, itemW)

        _布局已生成 = True
    End Sub

    ''' <summary>使布局缓存立即失效（增删项、项内 IsSeparator/IsDescription 变更等）。</summary>
    Private Sub 失效布局缓存()
        _布局已生成 = False
    End Sub

    Private Function 项目是否可见(index As Integer) As Boolean
        If index < 0 OrElse index >= 项目列表.Count Then Return False
        确保布局缓存()
        Return _布局项可见数组(index)
    End Function

    Private Function 获取标签栏矩形() As Rectangle
        确保布局缓存()
        Return _布局标签栏矩形
    End Function

    Private Function 获取内容区域矩形() As Rectangle
        确保布局缓存()
        Return _布局内容区矩形
    End Function

    Private Function 获取项高度(index As Integer) As Single
        If index < 0 OrElse index >= 项目列表.Count Then Return 标签页项高度 * DpiScale()
        确保布局缓存()
        Return _布局项高度数组(index)
    End Function

    Private Function 获取标签页项矩形(index As Integer) As RectangleF
        确保布局缓存()
        If index < 0 OrElse index >= 项目列表.Count Then
            Return New RectangleF(_布局项X, _布局搜索区高度 - _滚动偏移, _布局项W, 标签页项高度 * DpiScale())
        End If
        Dim y As Single = _布局搜索区高度 + _布局项AbsY数组(index) - _滚动偏移
        Return New RectangleF(_布局项X, y, _布局项W, _布局项高度数组(index))
    End Function

    Private Function 获取标签页总高度() As Single
        确保布局缓存()
        Return _布局总高度
    End Function

    Private Function 获取可滚动视口高度() As Single
        确保布局缓存()
        Return _布局视口高度
    End Function

    Private Function 获取搜索框区域高度() As Single
        确保布局缓存()
        Return _布局搜索区高度
    End Function



    Private Sub 同步内容面板布局()
        Dim contentRect = 获取内容区域矩形()
        _内容面板.Bounds = contentRect
        _内容面板.BackColor = 获取内容面板有效背景色()
        同步搜索框布局()
    End Sub

    Private Sub 限制滚动范围()
        Dim totalHeight As Single = 获取标签页总高度()
        Dim maxScroll As Single = Math.Max(0.0F, totalHeight - 获取可滚动视口高度())
        If _滚动偏移 < 0 Then _滚动偏移 = 0
        If _滚动偏移 > maxScroll Then _滚动偏移 = maxScroll
        If _滚动目标 < 0 Then _滚动目标 = 0
        If _滚动目标 > maxScroll Then _滚动目标 = maxScroll
    End Sub

    Private Sub 确保选中项可见()
        If _selectedIndex < 0 OrElse _selectedIndex >= 项目列表.Count Then Return
        If Not 项目是否可见(_selectedIndex) Then Return
        确保布局缓存()
        Dim absY As Single = _布局项AbsY数组(_selectedIndex)
        Dim itemH As Single = _布局项高度数组(_selectedIndex)
        Dim viewportH As Single = _布局视口高度
        If absY < _滚动偏移 Then
            _滚动偏移 = absY
            _滚动目标 = absY
        ElseIf absY + itemH > _滚动偏移 + viewportH Then
            _滚动偏移 = absY + itemH - viewportH
            _滚动目标 = _滚动偏移
        End If
        停止滚动动画()
        限制滚动范围()
    End Sub

    Private Sub 更新滚动条布局()
        Dim s As Single = DpiScale()
        Dim totalH As Integer = CInt(获取标签页总高度())
        Dim searchAreaH As Integer = CInt(获取搜索框区域高度())
        Dim visibleH As Integer = CInt(获取可滚动视口高度())
        If totalH <= visibleH OrElse 项目列表.Count = 0 Then
            _标签栏滚动条.ThumbRect = Rectangle.Empty
            _标签栏滚动条.TrackRect = Rectangle.Empty
            Return
        End If
        Dim sbContainerW As Integer = If(标签页位置 = TabSideEnum.Left, CInt(标签栏宽度 * s), Me.Width)
        _标签栏滚动条.ComputeLayout(sbContainerW, Me.Height, 0, 0, CInt(标签栏内边距.Top * s) + searchAreaH, CInt(标签栏内边距.Bottom * s), CInt(滚动条宽度 * s), totalH, visibleH, CInt(_滚动偏移))
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        失效布局缓存()
        同步内容面板布局()
        限制滚动范围()
        Invalidate()
    End Sub

    Protected Overrides Sub OnLayout(e As LayoutEventArgs)
        MyBase.OnLayout(e)
        失效布局缓存()
        同步内容面板布局()
        Invalidate()
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

    ''' <summary>
    ''' 统一动画帧 Tick：聚合"hover/选中插值"和"标签栏滚动"两类动画，由同一个调度源驱动。
    ''' 所有计算都使用 Stopwatch 真实 dt，保证帧率改变时缓动手感一致。
    ''' </summary>
    Private Sub 帧Tick(sender As Object, e As EventArgs)
        If IsDisposed OrElse Disposing OrElse Not IsHandleCreated Then Return

        Dim now As Long = Stopwatch.GetTimestamp()
        Dim freq As Double = Stopwatch.Frequency

        ' ---------- 1) hover / 选中淡入淡出动画 ----------
        Dim hoverActive As Boolean = False
        If _hover动画激活 Then
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
                    hoverActive = True
                Else
                    state.当前值 = state.目标值
                End If
            Next
            If Not hoverActive Then _hover动画激活 = False
        End If

        ' ---------- 2) 标签栏平滑滚动 ----------
        Dim scrollActive As Boolean = False
        If _滚动动画中 Then
            Dim dt As Single = CSng((now - _滚动动画上次时刻Ticks) / freq)
            If dt < 0.001F Then dt = 0.001F
            If dt > 0.05F Then dt = 0.05F
            _滚动动画上次时刻Ticks = now

            Dim totalH As Single = 获取标签页总高度()
            Dim viewportH As Single = 获取可滚动视口高度()
            Dim maxScroll As Single = Math.Max(0.0F, totalH - viewportH)
            Dim coef As Single = 滚动平滑系数
            If _滚动目标 < 0 Then
                _滚动目标 = 0
                coef = 滚动回弹系数
            ElseIf _滚动目标 > maxScroll Then
                _滚动目标 = maxScroll
                coef = 滚动回弹系数
            End If

            Dim diff As Single = _滚动目标 - _滚动偏移
            Dim alpha As Single = 1.0F - CSng(Math.Exp(-coef * dt))
            _滚动偏移 += diff * alpha

            If Math.Abs(diff) < 滚动停止阈值 Then
                _滚动偏移 = _滚动目标
                _滚动动画中 = False
                _滚动速度 = 0
            Else
                scrollActive = True
            End If
            限制滚动范围()
        End If

        ' ---------- 3) 失活时停掉调度源 ----------
        If hoverActive OrElse scrollActive Then
            Me.Invalidate(获取标签栏矩形())
        Else
            停止帧驱动()
        End If
    End Sub

    ''' <summary>启动统一帧驱动。重复调用幂等。</summary>
    Private Sub 启动帧驱动()
        If _帧驱动激活 Then Return
        _帧驱动激活 = True
        _动画助手.StartFrameLoop(AddressOf 帧Tick_Idle)
    End Sub

    Friend Sub 停止帧驱动()
        If Not _帧驱动激活 Then Return
        _帧驱动激活 = False
        Try : _动画助手.StopFrameLoop() : Catch : End Try
    End Sub

    ''' <summary>
    ''' Idle 模式下一次只推进一帧后让出，调用 Application.DoEvents 让消息循环回到 idle 时再继续。
    ''' 不再使用 PeekMessage 自旋——那会导致 CPU 跑满。
    ''' </summary>
    Private Sub 帧Tick_Idle(sender As Object, e As EventArgs)
        帧Tick(sender, e)
        ' Idle 处理后控件 Invalidate 会推送 WM_PAINT，处理完后会再次进入 idle，自然形成 V-Blank 等价节拍。
    End Sub

    Private Sub 帧驱动脏区(helper As AnimationHelperV2, owner As Control, sink As AnimationHelperV2.InvalidateRegionSink)
        sink.SuppressInvalidate()
    End Sub

    ' 兼容旧调用入口：hover 动画的启动/停止改为对统一帧驱动的请求。
    Private Sub 启动动画驱动()
        _hover动画激活 = True
        启动帧驱动()
    End Sub

    Friend Sub 停止动画驱动()
        _hover动画激活 = False
        ' 帧驱动是否真停由 帧Tick 内部决定（可能滚动动画仍在跑）。
    End Sub

    Private Function HitTestTab(clientPoint As Point) As Integer
        For i As Integer = 0 To 项目列表.Count - 1
            If Not 项目是否可见(i) Then Continue For
            If 获取标签页项矩形(i).Contains(clientPoint.X, clientPoint.Y) Then
                Dim item = 项目列表(i)
                If item.IsSeparator OrElse item.IsDescription Then Return -1
                Return i
            End If
        Next
        Return -1
    End Function

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)

        If _标签栏滚动条.IsDragging Then
            Dim totalH = CInt(获取标签页总高度())
            _滚动偏移 = _标签栏滚动条.DragMove(e.Y, totalH, CInt(获取可滚动视口高度()))
            _滚动目标 = _滚动偏移
            _滚动速度 = 0
            停止滚动动画()
            Me.Invalidate(获取标签栏矩形())
            Return
        End If

        Dim needInvalidate As Boolean = _标签栏滚动条.UpdateHover(e.Location)

        Dim newHover As Integer = -1
        For i As Integer = 0 To 项目列表.Count - 1
            If Not 项目是否可见(i) Then Continue For
            If 获取标签页项矩形(i).Contains(e.Location.X, e.Location.Y) Then
                Dim item = 项目列表(i)
                If Not item.IsSeparator AndAlso Not item.IsDescription Then
                    newHover = i
                End If
                Exit For
            End If
        Next
        Me.Cursor = If(newHover >= 0, Cursors.Hand, Cursors.Default)
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
        If needInvalidate Then Me.Invalidate(获取标签栏矩形())
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
        If _标签栏滚动条.ResetHover() Then needInvalidate = True
        If needInvalidate Then Me.Invalidate(获取标签栏矩形())
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button = MouseButtons.Left Then
            Me.Focus()
            If _标签栏滚动条.BeginDrag(e.Location, CInt(_滚动偏移)) Then
                停止滚动动画()
                Return
            End If
            If Not _标签栏滚动条.TrackRect.IsEmpty Then
                Dim totalH = CInt(获取标签页总高度())
                Dim newOff = _标签栏滚动条.TrackClick(e.Location, CInt(_滚动偏移), totalH, CInt(获取可滚动视口高度()))
                If newOff <> CInt(_滚动偏移) Then
                    _滚动目标 = newOff
                    启动滚动动画()
                    Return
                End If
            End If
            Dim hit = HitTestTab(e.Location)
            If hit >= 0 Then
                SelectedIndex = hit
            End If
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If _标签栏滚动条.IsDragging Then
            _标签栏滚动条.EndDrag()
            Me.Invalidate(获取标签栏矩形())
        End If
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        If 项目列表.Count = 0 Then Return
        Dim stripRect = 获取标签栏矩形()
        If Not stripRect.Contains(e.Location) Then Return
        Dim totalHeight As Single = 获取标签页总高度()
        Dim viewportH As Single = 获取可滚动视口高度()
        If totalHeight <= viewportH Then Return
        Dim scrollAmount As Single = Math.Max(1.0F, SystemInformation.MouseWheelScrollLines * 标签页项高度 * DpiScale() / 3.0F)
        Dim delta As Single = -Math.Sign(e.Delta) * scrollAmount
        Dim maxScroll As Single = Math.Max(0.0F, totalHeight - viewportH)
        ' 允许在边界处轻微过冲，由动画 Tick 中的更强阻尼系数实现"终点回弹"的橡皮筋手感。
        Dim overshoot As Single = 标签页项高度 * DpiScale() * 0.6F
        Dim newTarget As Single = _滚动目标 + delta
        If newTarget < -overshoot Then newTarget = -overshoot
        If newTarget > maxScroll + overshoot Then newTarget = maxScroll + overshoot
        _滚动目标 = newTarget
        启动滚动动画()
    End Sub

#Region "标签栏平滑滚动"
    ''' <summary>滚动平滑系数（每秒回归到目标的比率，越大越快）。</summary>
    Private Const 滚动平滑系数 As Single = 14.0F
    ''' <summary>越界回弹阻尼（每秒回归比率）。</summary>
    Private Const 滚动回弹系数 As Single = 18.0F
    ''' <summary>视为停止的阈值（像素）。</summary>
    Private Const 滚动停止阈值 As Single = 0.25F

    Private Sub 启动滚动动画()
        _滚动动画上次时刻Ticks = Stopwatch.GetTimestamp()
        _滚动动画中 = True
        启动帧驱动()
        Me.Invalidate(获取标签栏矩形())
    End Sub

    Private Sub 停止滚动动画()
        _滚动动画中 = False
        _滚动速度 = 0
        ' 帧驱动是否要彻底停由 帧Tick 内部决定（hover 动画可能仍在跑）。
    End Sub

    Private _滚动动画上次时刻Ticks As Long = 0
#End Region

    Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
        Select Case keyData And Keys.KeyCode
            Case Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Home, Keys.End
                Return True
        End Select
        Return MyBase.IsInputKey(keyData)
    End Function

    Protected Overrides Sub OnPreviewKeyDown(e As PreviewKeyDownEventArgs)
        Select Case e.KeyCode
            Case Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Home, Keys.End
                e.IsInputKey = True
        End Select
        MyBase.OnPreviewKeyDown(e)
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If 项目列表.Count = 0 Then Return
        Select Case e.KeyCode
            Case Keys.Up, Keys.Left
                SelectedIndex = 上一个可选索引(_selectedIndex)
                e.Handled = True
            Case Keys.Down, Keys.Right
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
            If Not item.IsSeparator AndAlso Not item.IsDescription Then selectableCount += 1
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
        Me.Invalidate(获取标签栏矩形())
    End Sub

    Protected Overrides Sub OnLostFocus(e As EventArgs)
        MyBase.OnLostFocus(e)
        Me.Invalidate(获取标签栏矩形())
    End Sub
#End Region

#Region "通用属性"
    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            Me.Invalidate()
        End If
    End Sub

    Private Sub SetLayoutValue(Of T)(ByRef field As T, value As T)
        If EqualityComparer(Of T).Default.Equals(field, value) Then Return
        field = value
        失效布局缓存()
        限制滚动范围()
        Me.Invalidate()
    End Sub

    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function

    Friend Sub InvalidateFontResources()
        D2DHelperV2.InvalidateTextFormatCache(Me)
        ReleaseLocalTextFormatCache()
        ReleaseMoreIndicatorFont()
        失效布局缓存()
    End Sub

    Friend Sub RefreshFontDependentRenderingNow()
        D2DHelperV2.RefreshFontDependentRendering(Me)
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        InvalidateFontResources()
        MyBase.OnFontChanged(e)
        同步内容面板布局()
        D2DHelperV2.RefreshFontDependentRendering(Me)
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        InvalidateFontResources()
        同步内容面板布局()
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
                解除背景穿透消费者()
                Try : BackgroundPenetrationV2.UnregisterConsumer(_内容面板) : Catch : End Try
                _backgroundSource = value
                ' 内容面板上的透明背景来自同一 source。
                If _内容面板 IsNot Nothing Then _内容面板.Invalidate(True)
                Me.Invalidate()
            End If
        End Set
    End Property

    Private Sub 解除背景穿透消费者()
        If _backgroundSource Is Nothing Then Return
        Try : BackgroundPenetrationV2.UnregisterConsumer(Me, _backgroundSource) : Catch : End Try
    End Sub

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
            Dim wasRunning = _帧驱动激活
            If wasRunning Then 停止帧驱动()
            动画帧率值 = value
            _动画助手.FPS = 动画帧率值
            If wasRunning Then 启动帧驱动()
        End Set
    End Property
#End Region

#Region "标签栏属性"
    Private 标签页位置 As TabSideEnum = TabSideEnum.Left
    <Category("LakeUI"), Description("标签栏位于控件的哪一侧"), DefaultValue(GetType(TabSideEnum), "Left"), Browsable(True)>
    Public Property TabSide As TabSideEnum
        Get
            Return 标签页位置
        End Get
        Set(value As TabSideEnum)
            If 标签页位置 <> value Then
                标签页位置 = value
                失效布局缓存()
                同步内容面板布局()
                限制滚动范围()
                Me.Invalidate()
            End If
        End Set
    End Property

    Private 标签栏宽度 As Integer = 150
    <Category("LakeUI"), Description("标签栏的宽度"), DefaultValue(150), Browsable(True)>
    Public Property TabStripWidth As Integer
        Get
            Return 标签栏宽度
        End Get
        Set(value As Integer)
            value = Math.Max(20, value)
            If 标签栏宽度 <> value Then
                标签栏宽度 = value
                失效布局缓存()
                同步内容面板布局()
                限制滚动范围()
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
            If _搜索框控件 IsNot Nothing Then _搜索框控件.BackColor = value
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

    ''' <summary>
    ''' 设置标签栏顶部的搜索框控件。该控件需具有 Text 属性和 TextChanged 事件。
    ''' 控件将被自动放入标签栏顶部区域，与顶部和选项卡列表的间距均为 <see cref="TabStripPadding"/> 的 Top 值，
    ''' 宽度撑满标签栏（减去左右内边距）。设为 Nothing 可移除搜索框。
    ''' </summary>
    <Category("LakeUI"), Description("标签栏顶部的搜索框控件，需具有 Text 属性和 TextChanged 事件"), DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property SearchBoxControl As Control
        Get
            Return _搜索框控件
        End Get
        Set(value As Control)
            If _搜索框控件 Is value Then Return
            If _搜索框控件 IsNot Nothing Then
                RemoveHandler _搜索框控件.TextChanged, AddressOf 搜索框文本变更
                If _搜索框控件.Parent Is Me Then Me.Controls.Remove(_搜索框控件)
                If _搜索框原始父级 IsNot Nothing Then
                    _搜索框控件.Bounds = _搜索框原始边界
                    If _搜索框控件.Parent IsNot _搜索框原始父级 Then
                        _搜索框原始父级.Controls.Add(_搜索框控件)
                    End If
                End If
                _搜索框原始父级 = Nothing
            End If
            _搜索框控件 = value
            _搜索文本 = ""
            _搜索词元缓存 = Array.Empty(Of String)()
            If _搜索框控件 IsNot Nothing Then
                _搜索文本 = If(_搜索框控件.Text, "")
                _搜索词元缓存 = 搜索词元(_搜索文本)
                _搜索框原始父级 = _搜索框控件.Parent
                _搜索框原始边界 = _搜索框控件.Bounds
                _搜索框控件.BackColor = 标签栏背景颜色
                If _搜索框控件.Parent IsNot Me Then Me.Controls.Add(_搜索框控件)
                _搜索框控件.BringToFront()
                AddHandler _搜索框控件.TextChanged, AddressOf 搜索框文本变更
            End If
            _滚动偏移 = 0
            失效布局缓存()
            同步搜索框布局()
            限制滚动范围()
            Invalidate()
        End Set
    End Property

    <Category("LakeUI"), Description("搜索框区域的高度"), DefaultValue(30), Browsable(True)>
    Public Property SearchBoxHeight As Integer
        Get
            Return _搜索框高度
        End Get
        Set(value As Integer)
            value = Math.Max(1, value)
            If _搜索框高度 <> value Then
                _搜索框高度 = value
                失效布局缓存()
                同步搜索框布局()
                限制滚动范围()
                Invalidate()
            End If
        End Set
    End Property
#End Region

#Region "标签页项属性"
    Private 标签页项高度 As Integer = 36
    <Category("LakeUI"), Description("每个标签页项的高度"), DefaultValue(36), Browsable(True)>
    Public Property TabItemHeight As Integer
        Get
            Return 标签页项高度
        End Get
        Set(value As Integer)
            SetLayoutValue(标签页项高度, Math.Max(16, value))
        End Set
    End Property

    Private 标签页项间距 As Integer = 1
    <Category("LakeUI"), Description("标签页项之间的间距"), DefaultValue(1), Browsable(True)>
    Public Property TabItemSpacing As Integer
        Get
            Return 标签页项间距
        End Get
        Set(value As Integer)
            SetLayoutValue(标签页项间距, Math.Max(0, value))
        End Set
    End Property

    Private 标签栏内边距 As New Padding(10)
    <Category("LakeUI"), Description("标签栏内边距，控制标签页项在标签栏容器内的缩进"), DefaultValue(GetType(Padding), "10, 10, 10, 10"), Browsable(True)>
    Public Property TabStripPadding As Padding
        Get
            Return 标签栏内边距
        End Get
        Set(value As Padding)
            If 标签栏内边距 = value Then Return
            标签栏内边距 = value
            失效布局缓存()
            同步内容面板布局()
            限制滚动范围()
            Me.Invalidate()
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
            SetValue(标签页圆角半径, Math.Max(0, value))
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

    Private 标签页文本左边距 As Integer = 12
    <Category("LakeUI"), Description("标签页文本的左边距"), DefaultValue(12), Browsable(True)>
    Public Property TabItemTextPadding As Integer
        Get
            Return 标签页文本左边距
        End Get
        Set(value As Integer)
            SetValue(标签页文本左边距, Math.Max(0, value))
        End Set
    End Property
#End Region

#Region "图标属性"
    Private 图标尺寸 As Integer = 28
    <Category("LakeUI"), Description("标签页图标尺寸"), DefaultValue(28), Browsable(True)>
    Public Property TabIconSize As Integer
        Get
            Return 图标尺寸
        End Get
        Set(value As Integer)
            SetValue(图标尺寸, Math.Max(0, value))
        End Set
    End Property

    Private 图标与文本间距 As Integer = 6
    <Category("LakeUI"), Description("图标与文本之间的间距"), DefaultValue(6), Browsable(True)>
    Public Property TabIconTextSpacing As Integer
        Get
            Return 图标与文本间距
        End Get
        Set(value As Integer)
            SetValue(图标与文本间距, Math.Max(0, value))
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

    Private 选中指示条宽度 As Integer = 3
    <Category("LakeUI"), Description("选中指示条的宽度"), DefaultValue(3), Browsable(True)>
    Public Property IndicatorWidth As Integer
        Get
            Return 选中指示条宽度
        End Get
        Set(value As Integer)
            SetValue(选中指示条宽度, Math.Max(0, value))
        End Set
    End Property

    Private 选中指示条边距 As Integer = 6
    <Category("LakeUI"), Description("选中指示条的上下边距"), DefaultValue(6), Browsable(True)>
    Public Property IndicatorPadding As Integer
        Get
            Return 选中指示条边距
        End Get
        Set(value As Integer)
            SetValue(选中指示条边距, Math.Max(0, value))
        End Set
    End Property

    Private 选中指示条圆角半径 As Integer = 2
    <Category("LakeUI"), Description("选中指示条的圆角半径"), DefaultValue(2), Browsable(True)>
    Public Property IndicatorBorderRadius As Integer
        Get
            Return 选中指示条圆角半径
        End Get
        Set(value As Integer)
            SetValue(选中指示条圆角半径, Math.Max(0, value))
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
            SetValue(内容区域边框宽度, Math.Max(0, value))
        End Set
    End Property
#End Region

#Region "滚动条属性"
    Private 滚动条宽度 As Integer = 6
    <Category("LakeUI"), Description("标签栏滚动条宽度"), DefaultValue(6), Browsable(True)>
    Public Property ScrollBarWidth As Integer
        Get
            Return 滚动条宽度
        End Get
        Set(value As Integer)
            SetLayoutValue(滚动条宽度, Math.Max(0, value))
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

#Region "更多指示器属性"
    Private 更多指示器高度 As Integer = 20
    <Category("LakeUI"), Description("上下还有更多内容时的指示器高度"), DefaultValue(20), Browsable(True)>
    Public Property MoreIndicatorHeight As Integer
        Get
            Return 更多指示器高度
        End Get
        Set(value As Integer)
            SetValue(更多指示器高度, Math.Max(0, value))
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

    Private 分割线高度值 As Integer = 20
    <Category("LakeUI"), Description("分割线高度"), DefaultValue(20), Browsable(True)>
    Public Property SeparatorHeight As Integer
        Get
            Return 分割线高度值
        End Get
        Set(value As Integer)
            If value < 1 Then value = 1
            SetLayoutValue(分割线高度值, value)
        End Set
    End Property

    Private 说明项高度值 As Integer = 30
    <Category("LakeUI"), Description("小字说明项高度"), DefaultValue(30), Browsable(True)>
    Public Property DescriptionItemHeight As Integer
        Get
            Return 说明项高度值
        End Get
        Set(value As Integer)
            If value < 1 Then value = 1
            SetLayoutValue(说明项高度值, value)
        End Set
    End Property

    Private Shared ReadOnly 默认说明字体 As New Font("Microsoft YaHei UI", 9)
    Private 说明字体值 As New Font("Microsoft YaHei UI", 9)
    <Category("LakeUI"), Description("小字说明项的字体"), Browsable(True)>
    Public Property DescriptionFont As Font
        Get
            Return 说明字体值
        End Get
        Set(value As Font)
            If value Is Nothing Then Return
            说明字体值 = value
            InvalidateFontResources()
            D2DHelperV2.RefreshFontDependentRendering(Me)
        End Set
    End Property
    Private Function ShouldSerializeDescriptionFont() As Boolean
        Return Not 说明字体值.Equals(默认说明字体)
    End Function
    Public Sub ResetDescriptionFont()
        DescriptionFont = New Font("Microsoft YaHei UI", 9)
    End Sub

    Private 说明文本颜色值 As Color = Color.CornflowerBlue
    <Category("LakeUI"), Description("小字说明项的文本颜色"), DefaultValue(GetType(Color), "CornflowerBlue"), Browsable(True)>
    Public Property DescriptionForeColor As Color
        Get
            Return 说明文本颜色值
        End Get
        Set(value As Color)
            SetValue(说明文本颜色值, value)
        End Set
    End Property
#End Region

End Class
