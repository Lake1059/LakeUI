Imports System.ComponentModel
Imports System.Drawing.Drawing2D

''' <summary>
''' 现代化选项卡列表控件。采用自绘列表 + 面板的组合方式，
''' 彻底避开原生 TabControl 的底层协议问题。
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

        Private _text As String = "ModernTabPage"
        <Category("LakeUI"), Description("标签页标题文本"), DefaultValue("ModernTabPage"), Browsable(True)>
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

        <Category("LakeUI"), Description("是否是分割线"), DefaultValue(False), Browsable(True)>
        Public Property IsSeparator As Boolean = False

        <Category("LakeUI"), Description("是否是小字说明项（不可选中）"), DefaultValue(False), Browsable(True)>
        Public Property IsDescription As Boolean = False

        <Category("LakeUI"), Description("绑定的内容控件，切换到此选项卡时将显示该控件"), DefaultValue(GetType(Control), Nothing), Browsable(True)>
        Public Property BoundControl As Control = Nothing

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
#End Region

#Region "构造"
    Private ReadOnly _标签页动画 As New Dictionary(Of Integer, TabAnimState)
    Private _悬停索引 As Integer = -1
    Private _动画计时器 As Timer
    Private _动画用Idle As Boolean = False
    Private _动画中 As Boolean = False
    Private ReadOnly _内容面板 As New Panel()
    Private ReadOnly 项目列表 As New List(Of ModernTabPage)
    Private _滚动偏移 As Integer = 0
    Private ReadOnly _标签栏滚动条 As New ScrollBarRenderer()

    Public Sub New()
        InitializeComponent()
        SetStyle(ControlStyles.AllPaintingInWmPaint, True)
        SetStyle(ControlStyles.DoubleBuffer, True)
        SetStyle(ControlStyles.UserPaint, True)
        SetStyle(ControlStyles.OptimizedDoubleBuffer, True)
        SetStyle(ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, True)
        DoubleBuffered = True

        _内容面板.Dock = DockStyle.None
        _内容面板.BackColor = 内容区域背景颜色
        Me.Controls.Add(_内容面板)

        _动画用Idle = (动画帧率值 <= 0)
        If Not _动画用Idle Then
            _动画计时器 = New Timer() With {.Interval = Math.Max(1, CInt(1000.0 / 动画帧率值))}
        End If
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
            If Not 项目列表(i).IsSeparator AndAlso Not 项目列表(i).IsDescription Then Return i
        Next
        Return fromIndex
    End Function

    Private Function 下一个可选索引(fromIndex As Integer) As Integer
        For i As Integer = fromIndex + 1 To 项目列表.Count - 1
            If Not 项目列表(i).IsSeparator AndAlso Not 项目列表(i).IsDescription Then Return i
        Next
        Return fromIndex
    End Function

    Private Function 第一个可选索引() As Integer
        For i As Integer = 0 To 项目列表.Count - 1
            If Not 项目列表(i).IsSeparator AndAlso Not 项目列表(i).IsDescription Then Return i
        Next
        Return -1
    End Function

    Private Function 最后一个可选索引() As Integer
        For i As Integer = 项目列表.Count - 1 To 0 Step -1
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
        ' 所有绘制在 OnPaint 中完成
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        确保Owner()
        If Me.Width <= 0 OrElse Me.Height <= 0 Then Return
        e.Graphics.SetClip(Me.ClientRectangle)
        Dim _ssaa As Integer = If(Class1.GlobalSSAA > 1, Class1.GlobalSSAA, 超采样倍率)
        If _ssaa > 1 Then
            Using bmp As New Bitmap(Me.Width * _ssaa, Me.Height * _ssaa)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.ScaleTransform(_ssaa, _ssaa)
                    绘制图形内容(g)
                End Using
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                e.Graphics.DrawImage(bmp, 0, 0, Me.Width, Me.Height)
            End Using
        Else
            绘制图形内容(e.Graphics)
        End If
        Dim tabClipState = e.Graphics.Save()
        e.Graphics.SetClip(获取标签栏矩形(), CombineMode.Intersect)
        For i As Integer = 0 To 项目列表.Count - 1
            绘制标签页文本(e.Graphics, i)
        Next
        e.Graphics.Restore(tabClipState)
    End Sub

    Private Sub 绘制图形内容(g As Graphics)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        g.InterpolationMode = InterpolationMode.HighQualityBicubic

        Using brush As New SolidBrush(内容区域背景颜色)
            g.FillRectangle(brush, 获取内容区域矩形())
        End Using

        Using brush As New SolidBrush(标签栏背景颜色)
            g.FillRectangle(brush, 获取标签栏矩形())
        End Using

        Dim gState = g.Save()
        g.SetClip(获取标签栏矩形(), CombineMode.Intersect)

        For i As Integer = 0 To 项目列表.Count - 1
            Dim item = 项目列表(i)
            If item.IsSeparator Then
                绘制分割线(g, i)
            ElseIf Not item.IsDescription Then
                绘制标签页项图形(g, i)
            End If
        Next

        g.Restore(gState)

        更新滚动条布局()
        If Not _标签栏滚动条.TrackRect.IsEmpty Then
            Dim sbContainerW As Integer = If(标签页位置 = TabSideEnum.Left, CInt(标签栏宽度 * DpiScale()), Me.Width)
            _标签栏滚动条.Draw(g, sbContainerW, Me.Height, 0, 0, CInt(滚动条宽度 * DpiScale()), 滚动条轨道颜色, 滚动条滑块颜色, 滚动条悬停颜色)
        End If

        If 内容区域边框宽度 > 0 Then
            Dim contentRect = 获取内容区域矩形()
            Using pen As New Pen(内容区域边框颜色, 内容区域边框宽度 * DpiScale())
                g.DrawRectangle(pen, contentRect.X, contentRect.Y, contentRect.Width - 1, contentRect.Height - 1)
            End Using
        End If
    End Sub

    Private Sub 绘制分割线(g As Graphics, index As Integer)
        Dim bounds = 获取标签页项矩形(index)
        Dim lineH As Single = Math.Max(1, DpiScale())
        Dim lineY As Single = bounds.Y + (bounds.Height - lineH) / 2.0F
        Using brush As New SolidBrush(分割线颜色值)
            g.FillRectangle(brush, bounds.X, lineY, bounds.Width, lineH)
        End Using
    End Sub

    Private Sub 绘制标签页项图形(g As Graphics, index As Integer)
        Dim s As Single = DpiScale()
        Dim bounds As RectangleF = 获取标签页项矩形(index)
        Dim isSelected As Boolean = (_selectedIndex = index)
        Dim hoverProgress As Single = 获取动画进度(index)

        Dim bgColor As Color
        If isSelected Then
            bgColor = 选中标签页背景颜色
        Else
            bgColor = 颜色插值(标签栏背景颜色, 悬停标签页背景颜色, hoverProgress)
        End If

        If 标签页圆角半径 > 0 Then
            Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(bounds, 标签页圆角半径 * s)
                Using brush As New SolidBrush(bgColor)
                    g.FillPath(brush, path)
                End Using
            End Using
        Else
            Using brush As New SolidBrush(bgColor)
                g.FillRectangle(brush, bounds)
            End Using
        End If

        If isSelected AndAlso 选中指示条宽度 > 0 Then
            Dim indicatorRect As RectangleF
            If 标签页位置 = TabSideEnum.Left Then
                indicatorRect = New RectangleF(bounds.X, bounds.Y + 选中指示条边距 * s, 选中指示条宽度 * s, bounds.Height - 选中指示条边距 * s * 2)
            Else
                indicatorRect = New RectangleF(bounds.Right - 选中指示条宽度 * s, bounds.Y + 选中指示条边距 * s, 选中指示条宽度 * s, bounds.Height - 选中指示条边距 * s * 2)
            End If
            If 选中指示条圆角半径 > 0 Then
                Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(indicatorRect, 选中指示条圆角半径 * s)
                    Using brush As New SolidBrush(选中指示条颜色)
                        g.FillPath(brush, path)
                    End Using
                End Using
            Else
                Using brush As New SolidBrush(选中指示条颜色)
                    g.FillRectangle(brush, indicatorRect)
                End Using
            End If
        End If

        If isSelected AndAlso Me.Focused AndAlso 焦点边框颜色 <> Color.Empty Then
            Dim focusBounds = bounds
            focusBounds.Inflate(-s, -s)
            If 标签页圆角半径 > 0 Then
                Using focusPath As GraphicsPath = RectangleRenderer.创建圆角矩形路径(focusBounds, Math.Max(1, 标签页圆角半径 * s - s))
                    RectangleRenderer.绘制圆角边框(g, focusPath, 焦点边框颜色, s)
                End Using
            Else
                RectangleRenderer.绘制矩形边框(g, focusBounds, 焦点边框颜色, s)
            End If
        End If

        绘制标签页图标(g, index, bounds)
    End Sub

    Private Sub 绘制标签页图标(g As Graphics, index As Integer, bounds As RectangleF)
        If index >= 项目列表.Count Then Return
        Dim item = 项目列表(index)
        If item.TabIcon Is Nothing Then Return

        Dim s As Single = DpiScale()
        Dim scaledIconSize As Single = 图标尺寸 * s
        Dim iconX As Single = bounds.X + 标签页文本左边距 * s
        Dim iconY As Single = bounds.Y + (bounds.Height - scaledIconSize) / 2.0F
        g.DrawImage(item.TabIcon, New RectangleF(iconX, iconY, scaledIconSize, scaledIconSize))
    End Sub

    Private Sub 绘制标签页文本(g As Graphics, index As Integer)
        If index >= 项目列表.Count Then Return
        Dim bounds As Rectangle = Rectangle.Round(获取标签页项矩形(index))
        Dim item = 项目列表(index)
        If item.IsSeparator Then Return

        Dim s As Single = DpiScale()
        Dim _textPad As Integer = CInt(标签页文本左边距 * s)

        If item.IsDescription Then
            Dim descFont = If(item.TabFont, 说明字体值)
            Dim descColor = If(item.NormalForeColor <> Color.Empty, item.NormalForeColor, 说明文本颜色值)
            Dim textRect As New Rectangle(
                bounds.X + _textPad,
                bounds.Y,
                bounds.Width - _textPad * 2,
                bounds.Height)
            TextRenderer.DrawText(g, item.Text, descFont, textRect, descColor,
                TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding)
            Return
        End If

        Dim isSelected As Boolean = (_selectedIndex = index)
        Dim textColor As Color
        Dim textFont As Font
        textColor = If(isSelected,
            If(item.SelectedForeColor <> Color.Empty, item.SelectedForeColor, 选中标签页文本颜色),
            If(item.NormalForeColor <> Color.Empty, item.NormalForeColor, 标签页默认文本颜色))
        textFont = If(item.TabFont, Me.Font)
        Dim iconOffset As Integer = 0
        If item.TabIcon IsNot Nothing Then
            iconOffset = CInt((图标尺寸 + 图标与文本间距) * s)
        End If

        Dim textRect2 As New Rectangle(
            bounds.X + _textPad + iconOffset,
            bounds.Y,
            bounds.Width - _textPad * 2 - iconOffset,
            bounds.Height)
        TextRenderer.DrawText(g, item.Text, textFont, textRect2, textColor,
            TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding)
    End Sub
#End Region

#Region "布局"
    Private Function 获取标签栏矩形() As Rectangle
        Dim w As Integer = CInt(标签栏宽度 * DpiScale())
        If 标签页位置 = TabSideEnum.Left Then
            Return New Rectangle(0, 0, w, Me.Height)
        Else
            Return New Rectangle(Me.Width - w, 0, w, Me.Height)
        End If
    End Function

    Private Function 获取内容区域矩形() As Rectangle
        Dim w As Integer = CInt(标签栏宽度 * DpiScale())
        If 标签页位置 = TabSideEnum.Left Then
            Return New Rectangle(w, 0, Math.Max(0, Me.Width - w), Me.Height)
        Else
            Return New Rectangle(0, 0, Math.Max(0, Me.Width - w), Me.Height)
        End If
    End Function

    Private Function 获取标签页项矩形(index As Integer) As RectangleF
        Dim s As Single = DpiScale()
        Dim tabStripRect = 获取标签栏矩形()
        Dim x As Single = tabStripRect.X + 标签栏内边距.Left * s
        Dim w As Single = tabStripRect.Width - (标签栏内边距.Left + 标签栏内边距.Right) * s
        Dim y As Single = 标签栏内边距.Top * s - _滚动偏移
        For i As Integer = 0 To index - 1
            y += 获取项高度(i) + 标签页项间距 * s
        Next
        Return New RectangleF(x, y, w, 获取项高度(index))
    End Function

    Private Function 获取项高度(index As Integer) As Single
        Dim s As Single = DpiScale()
        If index < 0 OrElse index >= 项目列表.Count Then Return 标签页项高度 * s
        Dim item = 项目列表(index)
        If item.IsSeparator Then Return 分割线高度值 * s
        If item.IsDescription Then Return 说明项高度值 * s
        Return 标签页项高度 * s
    End Function

    Private Sub 同步内容面板布局()
        Dim contentRect = 获取内容区域矩形()
        _内容面板.Bounds = contentRect
        _内容面板.BackColor = 内容区域背景颜色
    End Sub

    Private Function 获取标签页总高度() As Single
        Dim s As Single = DpiScale()
        If 项目列表.Count = 0 Then Return 0
        Dim h As Single = 标签栏内边距.Top * s
        For i As Integer = 0 To 项目列表.Count - 1
            h += 获取项高度(i)
            If i < 项目列表.Count - 1 Then h += 标签页项间距 * s
        Next
        h += 标签栏内边距.Bottom * s
        Return h
    End Function

    Private Sub 限制滚动范围()
        Dim totalHeight = CInt(获取标签页总高度())
        Dim maxScroll = Math.Max(0, totalHeight - Me.Height)
        _滚动偏移 = Math.Clamp(_滚动偏移, 0, maxScroll)
    End Sub

    Private Sub 确保选中项可见()
        If _selectedIndex < 0 OrElse _selectedIndex >= 项目列表.Count Then Return
        Dim s As Single = DpiScale()
        Dim absY As Single = 标签栏内边距.Top * s
        For i As Integer = 0 To _selectedIndex - 1
            absY += 获取项高度(i) + 标签页项间距 * s
        Next
        Dim itemH As Single = 获取项高度(_selectedIndex)
        If absY < _滚动偏移 Then
            _滚动偏移 = CInt(absY)
        ElseIf absY + itemH > _滚动偏移 + Me.Height Then
            _滚动偏移 = CInt(absY + itemH - Me.Height)
        End If
        限制滚动范围()
    End Sub

    Private Sub 更新滚动条布局()
        Dim s As Single = DpiScale()
        Dim totalH As Integer = CInt(获取标签页总高度())
        Dim visibleH As Integer = Me.Height
        If totalH <= visibleH OrElse 项目列表.Count = 0 Then
            _标签栏滚动条.ThumbRect = Rectangle.Empty
            _标签栏滚动条.TrackRect = Rectangle.Empty
            Return
        End If
        Dim sbContainerW As Integer = If(标签页位置 = TabSideEnum.Left, CInt(标签栏宽度 * s), Me.Width)
        _标签栏滚动条.ComputeLayout(sbContainerW, Me.Height, 0, 0, CInt(标签栏内边距.Top * s), CInt(标签栏内边距.Bottom * s), CInt(滚动条宽度 * s), totalH, visibleH, _滚动偏移)
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
            _滚动偏移 = _标签栏滚动条.DragMove(e.Y, totalH, Me.Height)
            Me.Invalidate()
            Return
        End If

        Dim needInvalidate As Boolean = _标签栏滚动条.UpdateHover(e.Location)

        Dim newHover As Integer = -1
        For i As Integer = 0 To 项目列表.Count - 1
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
        If _标签栏滚动条.ResetHover() Then needInvalidate = True
        If needInvalidate Then Me.Invalidate()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button = MouseButtons.Left Then
            Me.Focus()
            If _标签栏滚动条.BeginDrag(e.Location, _滚动偏移) Then Return
            If Not _标签栏滚动条.TrackRect.IsEmpty Then
                Dim totalH = CInt(获取标签页总高度())
                Dim newOff = _标签栏滚动条.TrackClick(e.Location, _滚动偏移, totalH, Me.Height)
                If newOff <> _滚动偏移 Then
                    _滚动偏移 = newOff
                    限制滚动范围()
                    Me.Invalidate()
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
            Me.Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        If 项目列表.Count = 0 Then Return
        Dim stripRect = 获取标签栏矩形()
        If Not stripRect.Contains(e.Location) Then Return
        Dim totalHeight = CInt(获取标签页总高度())
        If totalHeight <= Me.Height Then Return
        Dim scrollAmount As Integer = Math.Max(1, CInt(SystemInformation.MouseWheelScrollLines * 标签页项高度 * DpiScale() / 3))
        _滚动偏移 -= Math.Sign(e.Delta) * scrollAmount
        限制滚动范围()
        Me.Invalidate()
    End Sub

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

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        同步内容面板布局()
        Me.Invalidate()
    End Sub

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

    Private 动画时长值 As Integer = 300
    <Category("LakeUI"), Description(Class1.动画时长描述词), DefaultValue(300), Browsable(True)>
    Public Property AnimationDuration As Integer
        Get
            Return 动画时长值
        End Get
        Set(value As Integer)
            动画时长值 = Math.Max(0, value)
        End Set
    End Property

    Private 动画帧率值 As Integer = 60
    <Category("LakeUI"), Description(Class1.动画帧率描述词), DefaultValue(60), Browsable(True)>
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
    Private 标签页位置 As TabSideEnum = TabSideEnum.Left
    <Category("LakeUI"), Description("标签栏位于控件的哪一侧"), DefaultValue(GetType(TabSideEnum), "Left"), Browsable(True)>
    Public Property TabSide As TabSideEnum
        Get
            Return 标签页位置
        End Get
        Set(value As TabSideEnum)
            If 标签页位置 <> value Then
                标签页位置 = value
                同步内容面板布局()
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
            If 标签栏宽度 <> value Then
                标签栏宽度 = Math.Max(20, value)
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
#End Region

#Region "标签页项属性"
    Private 标签页项高度 As Integer = 36
    <Category("LakeUI"), Description("每个标签页项的高度"), DefaultValue(36), Browsable(True)>
    Public Property TabItemHeight As Integer
        Get
            Return 标签页项高度
        End Get
        Set(value As Integer)
            If 标签页项高度 <> value Then
                标签页项高度 = Math.Max(16, value)
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

    Private 标签栏内边距 As New Padding(10)
    <Category("LakeUI"), Description("标签栏内边距，控制标签页项在标签栏容器内的缩进"), DefaultValue(GetType(Padding), "10, 10, 10, 10"), Browsable(True)>
    Public Property TabStripPadding As Padding
        Get
            Return 标签栏内边距
        End Get
        Set(value As Padding)
            标签栏内边距 = value
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

    Private 标签页文本左边距 As Integer = 12
    <Category("LakeUI"), Description("标签页文本的左边距"), DefaultValue(12), Browsable(True)>
    Public Property TabItemTextPadding As Integer
        Get
            Return 标签页文本左边距
        End Get
        Set(value As Integer)
            SetValue(标签页文本左边距, value)
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

    Private 选中指示条宽度 As Integer = 3
    <Category("LakeUI"), Description("选中指示条的宽度"), DefaultValue(3), Browsable(True)>
    Public Property IndicatorWidth As Integer
        Get
            Return 选中指示条宽度
        End Get
        Set(value As Integer)
            SetValue(选中指示条宽度, value)
        End Set
    End Property

    Private 选中指示条边距 As Integer = 6
    <Category("LakeUI"), Description("选中指示条的上下边距"), DefaultValue(6), Browsable(True)>
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
            _内容面板.BackColor = value
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
    Private 滚动条宽度 As Integer = 6
    <Category("LakeUI"), Description("标签栏滚动条宽度"), DefaultValue(6), Browsable(True)>
    Public Property ScrollBarWidth As Integer
        Get
            Return 滚动条宽度
        End Get
        Set(value As Integer)
            SetValue(滚动条宽度, value)
        End Set
    End Property

    Private 滚动条轨道颜色 As Color = Color.FromArgb(20, 20, 20)
    <Category("LakeUI"), Description("标签栏滚动条轨道颜色"), DefaultValue(GetType(Color), "20, 20, 20"), Browsable(True)>
    Public Property ScrollBarTrackColor As Color
        Get
            Return 滚动条轨道颜色
        End Get
        Set(value As Color)
            SetValue(滚动条轨道颜色, value)
        End Set
    End Property

    Private 滚动条滑块颜色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("标签栏滚动条滑块颜色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
    Public Property ScrollBarThumbColor As Color
        Get
            Return 滚动条滑块颜色
        End Get
        Set(value As Color)
            SetValue(滚动条滑块颜色, value)
        End Set
    End Property

    Private 滚动条悬停颜色 As Color = Color.FromArgb(120, 120, 120)
    <Category("LakeUI"), Description("标签栏滚动条滑块悬停颜色"), DefaultValue(GetType(Color), "120, 120, 120"), Browsable(True)>
    Public Property ScrollBarThumbHoverColor As Color
        Get
            Return 滚动条悬停颜色
        End Get
        Set(value As Color)
            SetValue(滚动条悬停颜色, value)
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
            SetValue(分割线高度值, value)
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
            SetValue(说明项高度值, value)
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
            Me.Invalidate()
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