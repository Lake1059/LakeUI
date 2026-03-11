Imports System.ComponentModel
Imports System.Drawing.Drawing2D

''' <summary>
''' 现代化横向选项卡控件。采用自绘标签栏 + 面板的组合方式，
''' 彻底避开原生 TabControl 的底层协议问题。
''' 支持顶部/底部标签栏位置、自适应宽度/均分宽度两种布局模式、
''' 左/中/右/顶部居中/底部居中五种对齐方式、横向滚动条、悬停动画、图标、分割线和说明项。
''' 每个 <see cref="ModernTab"/> 可绑定一个 <see cref="Control"/> 作为内容，
''' 运行时自动切换其可见性。
''' </summary>
<DefaultEvent("SelectedIndexChanged")>
Public Class ModernTabControl

#Region "内部类型"
    ''' <summary>
    ''' 表示 <see cref="ModernTabControl"/> 中的一个选项卡项。
    ''' 支持独立设置标题字体、颜色、图标，以及绑定一个 <see cref="Control"/> 作为选项卡内容。
    ''' 也可设为分割线或小字说明项。
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
            If IsSeparator Then Return "│ Separator │"
            If IsDescription Then Return $"[说明] {_text}"
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

        Dim contentRect = 获取内容区域矩形()
        Dim stripRect = 获取标签栏矩形()

        Using brush As New SolidBrush(内容区域背景颜色)
            g.FillRectangle(brush, contentRect)
        End Using

        Using brush As New SolidBrush(标签栏背景颜色)
            g.FillRectangle(brush, stripRect)
        End Using

        Dim gState = g.Save()
        g.SetClip(stripRect, CombineMode.Intersect)

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
        If Not _滚动条TrackRect.IsEmpty Then
            绘制横向滚动条(g)
        End If

        If 内容区域边框宽度 > 0 Then
            Using pen As New Pen(内容区域边框颜色, 内容区域边框宽度)
                g.DrawRectangle(pen, contentRect.X, contentRect.Y, contentRect.Width - 1, contentRect.Height - 1)
            End Using
        End If
    End Sub

    Private Sub 绘制分割线(g As Graphics, index As Integer)
        Dim bounds = 获取标签页项矩形(index)
        Dim lineX As Single = bounds.X + (bounds.Width - 1) / 2.0F
        Using brush As New SolidBrush(分割线颜色值)
            g.FillRectangle(brush, lineX, bounds.Y, 1, bounds.Height)
        End Using
    End Sub

    Private Sub 绘制标签页项图形(g As Graphics, index As Integer)
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
            Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(bounds, 标签页圆角半径)
                Using brush As New SolidBrush(bgColor)
                    g.FillPath(brush, path)
                End Using
            End Using
        Else
            Using brush As New SolidBrush(bgColor)
                g.FillRectangle(brush, bounds)
            End Using
        End If

        If isSelected AndAlso 选中指示条高度 > 0 Then
            Dim indicatorRect As RectangleF
            If 标签页位置 = TabPositionEnum.Top Then
                indicatorRect = New RectangleF(bounds.X + 选中指示条边距, bounds.Bottom - 选中指示条高度, bounds.Width - 选中指示条边距 * 2, 选中指示条高度)
            Else
                indicatorRect = New RectangleF(bounds.X + 选中指示条边距, bounds.Y, bounds.Width - 选中指示条边距 * 2, 选中指示条高度)
            End If
            If 选中指示条圆角半径 > 0 Then
                Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(indicatorRect, 选中指示条圆角半径)
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
            focusBounds.Inflate(-1, -1)
            If 标签页圆角半径 > 0 Then
                Using focusPath As GraphicsPath = RectangleRenderer.创建圆角矩形路径(focusBounds, Math.Max(1, 标签页圆角半径 - 1))
                    RectangleRenderer.绘制圆角边框(g, focusPath, 焦点边框颜色, 1.0F)
                End Using
            Else
                RectangleRenderer.绘制矩形边框(g, focusBounds, 焦点边框颜色, 1.0F)
            End If
        End If

        绘制标签页图标(g, index, bounds)
    End Sub

    Private Sub 绘制标签页图标(g As Graphics, index As Integer, bounds As RectangleF)
        If index >= 项目列表.Count Then Return
        Dim item = 项目列表(index)
        If item.TabIcon Is Nothing Then Return

        Dim iconX As Single = bounds.X + 标签页文本内边距
        Dim iconY As Single = bounds.Y + (bounds.Height - 图标尺寸) / 2.0F
        g.DrawImage(item.TabIcon, New RectangleF(iconX, iconY, 图标尺寸, 图标尺寸))
    End Sub

    Private Sub 绘制标签页文本(g As Graphics, index As Integer)
        If index >= 项目列表.Count Then Return
        Dim bounds As Rectangle = Rectangle.Round(获取标签页项矩形(index))
        Dim item = 项目列表(index)
        If item.IsSeparator Then Return

        If item.IsDescription Then
            Dim descFont = If(item.TabFont, 说明字体值)
            Dim descColor = If(item.NormalForeColor <> Color.Empty, item.NormalForeColor, 说明文本颜色值)
            TextRenderer.DrawText(g, item.Text, descFont, bounds, descColor,
                TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding)
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
            iconOffset = 图标尺寸 + 图标与文本间距
        End If

        Dim textRect As New Rectangle(
            bounds.X + 标签页文本内边距 + iconOffset,
            bounds.Y,
            bounds.Width - 标签页文本内边距 * 2 - iconOffset,
            bounds.Height)
        TextRenderer.DrawText(g, item.Text, textFont, textRect, textColor,
            TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding)
    End Sub

    Private Sub 绘制横向滚动条(g As Graphics)
        If _滚动条TrackRect.IsEmpty Then Return
        If _滚动条TrackRect.Width < 1 OrElse 滚动条高度 < 1 Then Return

        Dim oldSmooth = g.SmoothingMode
        g.SmoothingMode = SmoothingMode.AntiAlias

        Dim barH As Integer = 滚动条高度
        Dim trackY As Integer = _滚动条TrackRect.Y + (_滚动条TrackRect.Height - barH) \ 2

        If 滚动条轨道颜色.A > 0 Then
            Dim trackRadius As Integer = Math.Min(barH \ 2, _滚动条TrackRect.Width \ 2)
            Using trackPath As GraphicsPath = RectangleRenderer.创建圆角矩形路径(
                New RectangleF(_滚动条TrackRect.X, trackY, _滚动条TrackRect.Width, barH), trackRadius)
                Using br As New SolidBrush(滚动条轨道颜色)
                    g.FillPath(br, trackPath)
                End Using
            End Using
        End If

        Dim activeColor As Color = If(_滚动条IsDragging OrElse _滚动条IsHover, 滚动条悬停颜色, 滚动条滑块颜色)
        Dim thumbY As Integer = _滚动条ThumbRect.Y + (_滚动条ThumbRect.Height - barH) \ 2
        Dim thumbRadius As Integer = Math.Min(barH \ 2, _滚动条ThumbRect.Width \ 2)
        Using thumbPath As GraphicsPath = RectangleRenderer.创建圆角矩形路径(
            New RectangleF(_滚动条ThumbRect.X, thumbY, _滚动条ThumbRect.Width, barH), thumbRadius)
            Using br As New SolidBrush(activeColor)
                g.FillPath(br, thumbPath)
            End Using
        End Using

        g.SmoothingMode = oldSmooth
    End Sub
#End Region

#Region "布局"
    Private Function 获取标签栏矩形() As Rectangle
        If 标签页位置 = TabPositionEnum.Top Then
            Return New Rectangle(0, 0, Me.Width, 标签栏高度)
        Else
            Return New Rectangle(0, Me.Height - 标签栏高度, Me.Width, 标签栏高度)
        End If
    End Function

    Private Function 获取内容区域矩形() As Rectangle
        If 标签页位置 = TabPositionEnum.Top Then
            Return New Rectangle(0, 标签栏高度, Me.Width, Math.Max(0, Me.Height - 标签栏高度))
        Else
            Return New Rectangle(0, 0, Me.Width, Math.Max(0, Me.Height - 标签栏高度))
        End If
    End Function

    Private Function 获取标签页项矩形(index As Integer) As RectangleF
        Dim stripRect = 获取标签栏矩形()
        Dim fullH As Single = stripRect.Height - 标签栏内边距.Top - 标签栏内边距.Bottom
        Dim y As Single = stripRect.Y + 标签栏内边距.Top
        Dim h As Single = fullH
        Dim availableWidth As Single = stripRect.Width - 标签栏内边距.Left - 标签栏内边距.Right

        Dim widths = 计算所有标签页宽度(availableWidth)
        If widths.Length = 0 Then Return RectangleF.Empty

        Dim totalWidth As Single = 0
        For Each w In widths
            totalWidth += w
        Next
        If widths.Length > 1 Then totalWidth += 标签页项间距 * (widths.Length - 1)

        Dim alignOffset As Single = 0
        If 标签页尺寸模式 = TabSizingEnum.AutoWidth AndAlso totalWidth < availableWidth Then
            Select Case 标签页对齐方式
                Case TabAlignmentEnum.Center
                    alignOffset = (availableWidth - totalWidth) / 2.0F
                Case TabAlignmentEnum.Right
                    alignOffset = availableWidth - totalWidth
                Case TabAlignmentEnum.TopCenter
                    alignOffset = (availableWidth - totalWidth) / 2.0F
                    Dim compactH As Single = Math.Min(fullH, Math.Max(Me.Font.Height + 6, fullH * 0.7F))
                    h = compactH
                Case TabAlignmentEnum.BottomCenter
                    alignOffset = (availableWidth - totalWidth) / 2.0F
                    Dim compactH As Single = Math.Min(fullH, Math.Max(Me.Font.Height + 6, fullH * 0.7F))
                    h = compactH
                    y = y + fullH - compactH
            End Select
        End If

        Dim x As Single = 标签栏内边距.Left + alignOffset - _滚动偏移
        For i As Integer = 0 To index - 1
            x += widths(i) + 标签页项间距
        Next
        Return New RectangleF(x, y, widths(index), h)
    End Function

    Private Function 计算所有标签页宽度(availableWidth As Single) As Single()
        If _缓存宽度 IsNot Nothing AndAlso _缓存可用宽度 = availableWidth Then Return _缓存宽度
        If 项目列表.Count = 0 Then Return Array.Empty(Of Single)()
        Dim widths(项目列表.Count - 1) As Single

        If 标签页尺寸模式 = TabSizingEnum.EqualWidth Then
            Dim separatorCount As Integer = 0
            Dim separatorTotalW As Single = 0
            For i As Integer = 0 To 项目列表.Count - 1
                If 项目列表(i).IsSeparator Then
                    separatorCount += 1
                    separatorTotalW += 分割线宽度值
                End If
            Next
            Dim normalCount As Integer = 项目列表.Count - separatorCount
            Dim spacingTotal As Single = 标签页项间距 * Math.Max(0, 项目列表.Count - 1)
            Dim perItem As Single = If(normalCount > 0, (availableWidth - spacingTotal - separatorTotalW) / normalCount, 标签页最小宽度)
            Dim effectiveWidth As Single = Math.Max(标签页最小宽度, perItem)
            For i As Integer = 0 To 项目列表.Count - 1
                If 项目列表(i).IsSeparator Then
                    widths(i) = 分割线宽度值
                Else
                    widths(i) = effectiveWidth
                End If
            Next
        Else
            For i As Integer = 0 To 项目列表.Count - 1
                Dim item = 项目列表(i)
                If item.IsSeparator Then
                    widths(i) = 分割线宽度值
                ElseIf item.IsDescription Then
                    Dim font = If(item.TabFont, 说明字体值)
                    Dim textW = TextRenderer.MeasureText(item.Text, font, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.NoPadding).Width
                    widths(i) = Math.Max(标签页最小宽度, textW + 标签页文本内边距 * 2)
                Else
                    Dim font = If(item.TabFont, Me.Font)
                    Dim textW = TextRenderer.MeasureText(item.Text, font, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.NoPadding).Width
                    Dim iconW As Integer = If(item.TabIcon IsNot Nothing, 图标尺寸 + 图标与文本间距, 0)
                    widths(i) = Math.Max(标签页最小宽度, textW + iconW + 标签页文本内边距 * 2)
                End If
            Next
        End If

        _缓存宽度 = widths
        _缓存可用宽度 = availableWidth
        Return widths
    End Function

    Private Function 获取标签页总宽度() As Single
        If 项目列表.Count = 0 Then Return 0
        Dim stripRect = 获取标签栏矩形()
        Dim availableWidth As Single = stripRect.Width - 标签栏内边距.Left - 标签栏内边距.Right
        Dim widths = 计算所有标签页宽度(availableWidth)
        Dim total As Single = 标签栏内边距.Left
        For i As Integer = 0 To widths.Length - 1
            total += widths(i)
            If i < widths.Length - 1 Then total += 标签页项间距
        Next
        total += 标签栏内边距.Right
        Return total
    End Function

    Private Sub 同步内容面板布局()
        Dim contentRect = 获取内容区域矩形()
        _内容面板.Bounds = contentRect
        _内容面板.BackColor = 内容区域背景颜色
    End Sub

    Private Sub 限制滚动范围()
        Dim totalWidth = CInt(获取标签页总宽度())
        Dim maxScroll = Math.Max(0, totalWidth - Me.Width)
        _滚动偏移 = Math.Clamp(_滚动偏移, 0, maxScroll)
    End Sub

    Private Sub 确保选中项可见()
        If _selectedIndex < 0 OrElse _selectedIndex >= 项目列表.Count Then Return
        Dim stripRect = 获取标签栏矩形()
        Dim availableWidth As Single = stripRect.Width - 标签栏内边距.Left - 标签栏内边距.Right
        Dim widths = 计算所有标签页宽度(availableWidth)

        Dim absX As Single = 标签栏内边距.Left
        For i As Integer = 0 To _selectedIndex - 1
            absX += widths(i) + 标签页项间距
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

        Dim stripRect = 获取标签栏矩形()
        Dim margin As Integer = 2
        Dim sbY As Integer
        If 标签页位置 = TabPositionEnum.Top Then
            sbY = stripRect.Bottom - 滚动条高度 - margin
        Else
            sbY = stripRect.Y + margin
        End If
        Dim sbX As Integer = 标签栏内边距.Left + margin
        Dim sbW As Integer = Me.Width - 标签栏内边距.Left - 标签栏内边距.Right - margin * 2
        If sbW <= 0 Then
            _滚动条ThumbRect = Rectangle.Empty
            _滚动条TrackRect = Rectangle.Empty
            Return
        End If

        _滚动条TrackRect = New Rectangle(sbX, sbY, sbW, 滚动条高度)

        Dim maxOff As Integer = Math.Max(0, totalW - visibleW)
        Dim thumbW As Integer = Math.Max(20, CInt(sbW * visibleW / Math.Max(1, totalW)))
        Dim thumbX As Integer = sbX
        If maxOff > 0 Then
            thumbX = sbX + CInt((sbW - thumbW) * _滚动偏移 / maxOff)
        End If
        _滚动条ThumbRect = New Rectangle(thumbX, sbY, thumbW, 滚动条高度)
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

        If _滚动条IsDragging Then
            Dim totalW = CInt(获取标签页总宽度())
            _滚动偏移 = 滚动条DragMove(e.X, totalW, Me.Width)
            限制滚动范围()
            Me.Invalidate()
            Return
        End If

        Dim needInvalidate As Boolean = 滚动条UpdateHover(e.Location)

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
                SelectedIndex = hit
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
