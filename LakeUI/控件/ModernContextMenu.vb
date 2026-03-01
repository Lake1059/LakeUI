Imports System.ComponentModel
Imports System.ComponentModel.Design
Imports System.Drawing.Drawing2D

<ToolboxItem(True)>
<Designer("System.Windows.Forms.Design.ComponentDesigner, System.Design", GetType(IDesigner))>
<ProvideProperty("EnableMenu", GetType(Control))>
Public Class ModernContextMenu
    Inherits Component
    Implements IExtenderProvider

    Private ReadOnly 项目列表 As New List(Of ModernMenuItem)

    <Category("LakeUI"), Description("菜单项集合"), Browsable(True)>
    <DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
    Public ReadOnly Property Items As List(Of ModernMenuItem)
        Get
            Return 项目列表
        End Get
    End Property

    Private Shared Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
        End If
    End Sub

#Region "属性"

    Private 背景颜色 As Color = Color.FromArgb(36, 36, 36)
    <Category("LakeUI"), Description("背景颜色"), DefaultValue(GetType(Color), "36, 36, 36"), Browsable(True)>
    Public Property BackColor1 As Color
        Get
            Return 背景颜色
        End Get
        Set(value As Color)
            SetValue(背景颜色, value)
        End Set
    End Property

    Private 边框颜色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("边框颜色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
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
            If value < 0 Then value = 0
            SetValue(边框宽度, value)
        End Set
    End Property

    Private 项目高度 As Integer = 30
    <Category("LakeUI"), Description("项目高度"), DefaultValue(GetType(Integer), "30"), Browsable(True)>
    Public Property ItemHeight As Integer
        Get
            Return 项目高度
        End Get
        Set(value As Integer)
            If value < 1 Then value = 1
            SetValue(项目高度, value)
        End Set
    End Property

    Private 说明项高度 As Integer = 22
    <Category("LakeUI"), Description("小字说明项高度"), DefaultValue(GetType(Integer), "22"), Browsable(True)>
    Public Property DescriptionItemHeight As Integer
        Get
            Return 说明项高度
        End Get
        Set(value As Integer)
            If value < 1 Then value = 1
            SetValue(说明项高度, value)
        End Set
    End Property

    Private Shared ReadOnly 默认字体 As New Font("Microsoft YaHei UI", 9)
    Private 菜单字体 As New Font("Microsoft YaHei UI", 9)
    <Category("LakeUI"), Description("菜单字体"), Browsable(True)>
    Public Property MenuFont As Font
        Get
            Return 菜单字体
        End Get
        Set(value As Font)
            If value Is Nothing Then Return
            SetValue(菜单字体, value)
        End Set
    End Property
    Private Function ShouldSerializeMenuFont() As Boolean
        Return Not 菜单字体.Equals(默认字体)
    End Function
    Public Sub ResetMenuFont()
        MenuFont = New Font("Microsoft YaHei UI", 9)
    End Sub

    Private 文本颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("文本颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property MenuForeColor As Color
        Get
            Return 文本颜色
        End Get
        Set(value As Color)
            SetValue(文本颜色, value)
        End Set
    End Property

    Private Shared ReadOnly 默认说明字体 As New Font("Microsoft YaHei UI", 8)
    Private 说明字体 As New Font("Microsoft YaHei UI", 8)
    <Category("LakeUI"), Description("小字说明项的字体"), Browsable(True)>
    Public Property DescriptionFont As Font
        Get
            Return 说明字体
        End Get
        Set(value As Font)
            If value Is Nothing Then Return
            SetValue(说明字体, value)
        End Set
    End Property
    Private Function ShouldSerializeDescriptionFont() As Boolean
        Return Not 说明字体.Equals(默认说明字体)
    End Function
    Public Sub ResetDescriptionFont()
        DescriptionFont = New Font("Microsoft YaHei UI", 8)
    End Sub

    Private 说明文本颜色 As Color = Color.CornflowerBlue
    <Category("LakeUI"), Description("小字说明项的文本颜色"), DefaultValue(GetType(Color), "CornflowerBlue"), Browsable(True)>
    Public Property DescriptionForeColor As Color
        Get
            Return 说明文本颜色
        End Get
        Set(value As Color)
            SetValue(说明文本颜色, value)
        End Set
    End Property

    Private 悬停背景颜色 As Color = Color.FromArgb(64, 64, 64)
    <Category("LakeUI"), Description("悬停背景颜色"), DefaultValue(GetType(Color), "64, 64, 64"), Browsable(True)>
    Public Property HoverBackColor As Color
        Get
            Return 悬停背景颜色
        End Get
        Set(value As Color)
            SetValue(悬停背景颜色, value)
        End Set
    End Property

    Private 按下背景颜色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("鼠标按下背景颜色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
    Public Property PressedBackColor As Color
        Get
            Return 按下背景颜色
        End Get
        Set(value As Color)
            SetValue(按下背景颜色, value)
        End Set
    End Property

    Private 分割线颜色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("分割线颜色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
    Public Property SeparatorColor As Color
        Get
            Return 分割线颜色
        End Get
        Set(value As Color)
            SetValue(分割线颜色, value)
        End Set
    End Property

    Private 分割线高度 As Integer = 2
    <Category("LakeUI"), Description("分割线高度"), DefaultValue(GetType(Integer), "2"), Browsable(True)>
    Public Property SeparatorHeight As Integer
        Get
            Return 分割线高度
        End Get
        Set(value As Integer)
            If value < 1 Then value = 1
            SetValue(分割线高度, value)
        End Set
    End Property

    Private 勾选颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("勾选标记颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property CheckMarkColor As Color
        Get
            Return 勾选颜色
        End Get
        Set(value As Color)
            SetValue(勾选颜色, value)
        End Set
    End Property

    Private 箭头颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("子菜单箭头颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property ArrowColor As Color
        Get
            Return 箭头颜色
        End Get
        Set(value As Color)
            SetValue(箭头颜色, value)
        End Set
    End Property

    Private 箭头大小 As Integer = 10
    <Category("LakeUI"), Description("子菜单箭头大小"), DefaultValue(GetType(Integer), "10"), Browsable(True)>
    Public Property ArrowSize As Integer
        Get
            Return 箭头大小
        End Get
        Set(value As Integer)
            If value < 2 Then value = 2
            SetValue(箭头大小, value)
        End Set
    End Property

    Private 图标区域宽度 As Integer = 24
    <Category("LakeUI"), Description("图标区域宽度"), DefaultValue(GetType(Integer), "24"), Browsable(True)>
    Public Property IconAreaWidth As Integer
        Get
            Return 图标区域宽度
        End Get
        Set(value As Integer)
            If value < 0 Then value = 0
            SetValue(图标区域宽度, value)
        End Set
    End Property

    Private 显示图标区域 As Boolean = True
    <Category("LakeUI"), Description("是否显示图标和复选标记区域"), DefaultValue(True), Browsable(True)>
    Public Property ShowIconArea As Boolean
        Get
            Return 显示图标区域
        End Get
        Set(value As Boolean)
            SetValue(显示图标区域, value)
        End Set
    End Property

    Friend ReadOnly Property 有效图标区域宽度 As Integer
        Get
            Return If(显示图标区域, 图标区域宽度, 0)
        End Get
    End Property

    Private Const 勾选最小宽度 As Integer = 20

    Friend ReadOnly Property 有效左侧保留宽度 As Integer
        Get
            Dim w = 有效图标区域宽度
            If w > 0 Then Return w
            For Each item In 项目列表
                If Not item.IsSeparator AndAlso Not item.IsDescription AndAlso item.Checked Then Return 勾选最小宽度
            Next
            Return 0
        End Get
    End Property

    Private 内边距 As New Padding(1)
    <Category("LakeUI"), Description("菜单内边距"), DefaultValue(GetType(Padding), "1, 1, 1, 1"), Browsable(True)>
    Public Property MenuPadding As Padding
        Get
            Return 内边距
        End Get
        Set(value As Padding)
            SetValue(内边距, value)
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

    Private 悬停圆角半径 As Integer = 0
    <Category("LakeUI"), Description("悬停高亮圆角半径，0 = 直角矩形"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property HoverRadius As Integer
        Get
            Return 悬停圆角半径
        End Get
        Set(value As Integer)
            If value < 0 Then value = 0
            SetValue(悬停圆角半径, value)
        End Set
    End Property

    Private 文本内边距 As New Padding(5, 0, 0, 0)
    <Category("LakeUI"), Description("菜单项文本内边距"), DefaultValue(GetType(Padding), "5, 0, 0, 0"), Browsable(True)>
    Public Property TextPadding As Padding
        Get
            Return 文本内边距
        End Get
        Set(value As Padding)
            SetValue(文本内边距, value)
        End Set
    End Property

    Private 悬停动画时长 As Integer = 200
    <Category("LakeUI"), Description("悬停高亮移动动画时长（毫秒），0 = 无动画"), DefaultValue(GetType(Integer), "200"), Browsable(True)>
    Public Property HoverAnimationDuration As Integer
        Get
            Return 悬停动画时长
        End Get
        Set(value As Integer)
            If value < 0 Then value = 0
            SetValue(悬停动画时长, value)
        End Set
    End Property

#End Region

#Region "绑定控件（IExtenderProvider）"

    Private ReadOnly 绑定控件集合 As New Dictionary(Of Control, Boolean)

    Public Function CanExtend(extendee As Object) As Boolean Implements IExtenderProvider.CanExtend
        Return TypeOf extendee Is Control
    End Function

    <Category("LakeUI"), Description("启用后右键该控件将自动弹出此 ModernContextMenu")>
    <DefaultValue(False)>
    Public Function GetEnableMenu(control As Control) As Boolean
        Dim result As Boolean = False
        绑定控件集合.TryGetValue(control, result)
        Return result
    End Function

    Public Sub SetEnableMenu(control As Control, value As Boolean)
        If value Then
            If Not 绑定控件集合.ContainsKey(control) Then
                AddHandler control.MouseUp, AddressOf 绑定控件_MouseUp
                AddHandler control.Disposed, AddressOf 绑定控件_Disposed
            End If
            绑定控件集合(control) = True
        Else
            If 绑定控件集合.ContainsKey(control) Then
                RemoveHandler control.MouseUp, AddressOf 绑定控件_MouseUp
                RemoveHandler control.Disposed, AddressOf 绑定控件_Disposed
                绑定控件集合.Remove(control)
            End If
        End If
    End Sub

    Private Sub 绑定控件_MouseUp(sender As Object, e As MouseEventArgs)
        If e.Button = MouseButtons.Right Then
            Dim ctrl = DirectCast(sender, Control)
            Show(ctrl, e.Location)
        End If
    End Sub

    Private Sub 绑定控件_Disposed(sender As Object, e As EventArgs)
        Dim ctrl = DirectCast(sender, Control)
        SetEnableMenu(ctrl, False)
    End Sub

#End Region

#Region "事件"

    Public Event MenuClosed As EventHandler

    Friend Sub 通知菜单关闭()
        当前弹出窗口 = Nothing
        RaiseEvent MenuClosed(Me, EventArgs.Empty)
    End Sub

#End Region

#Region "显示与关闭"

    Private 当前弹出窗口 As MenuPopupForm = Nothing

    Public Sub Show(x As Integer, y As Integer)
        Close()
        If 项目列表.Count = 0 Then Return
        当前弹出窗口 = New MenuPopupForm(Me, Nothing)
        当前弹出窗口.ShowAt(x, y)
    End Sub

    Public Sub Show(control As Control, location As Point)
        Dim screenPoint = control.PointToScreen(location)
        Show(screenPoint.X, screenPoint.Y)
    End Sub

    Public Sub Show(control As Control, x As Integer, y As Integer)
        Show(control, New Point(x, y))
    End Sub

    Public Sub Close()
        If 当前弹出窗口 IsNot Nothing AndAlso Not 当前弹出窗口.IsDisposed Then
            当前弹出窗口.关闭全部()
        End If
        当前弹出窗口 = Nothing
    End Sub

#End Region

#Region "弹出窗口"

    Friend Class MenuPopupForm
        Inherits PopupForm
        Implements IMessageFilter

        Private ReadOnly 菜单 As ModernContextMenu
        Private ReadOnly 父弹窗 As MenuPopupForm
        Private 悬停索引 As Integer = -1
        Private 子菜单弹窗 As MenuPopupForm = Nothing
        Private ReadOnly 项目区域列表 As New List(Of Rectangle)
        Private 正在关闭 As Boolean = False
        Private 鼠标按下 As Boolean = False

        ' 悬停动画相关
        Private ReadOnly 动画秒表 As New Stopwatch()
        Private ReadOnly 动画计时器 As New System.Windows.Forms.Timer() With {.Interval = 15}
        Private 动画起始Y As Single = -1
        Private 动画目标Y As Single = -1
        Private 动画当前Y As Single = -1
        Private 动画起始高度 As Single = 0
        Private 动画目标高度 As Single = 0
        Private 动画当前高度 As Single = 0
        Private 动画中 As Boolean = False
        Private 动画显示高亮 As Boolean = False

        Private Const WM_LBUTTONDOWN As Integer = &H201
        Private Const WM_RBUTTONDOWN As Integer = &H204
        Private Const WM_MBUTTONDOWN As Integer = &H207
        Private Const WM_NCLBUTTONDOWN As Integer = &HA1
        Private Const WM_KEYDOWN As Integer = &H100
        Private Const WM_ACTIVATEAPP As Integer = &H1C

        Protected Overrides ReadOnly Property CreateParams As CreateParams
            Get
                Dim cp = MyBase.CreateParams
                cp.ClassStyle = cp.ClassStyle Or &H20000
                Return cp
            End Get
        End Property

        Protected Overrides Sub WndProc(ByRef m As Message)
            MyBase.WndProc(m)
            If m.Msg = WM_ACTIVATEAPP AndAlso m.WParam = IntPtr.Zero Then
                If Not 正在关闭 Then BeginInvoke(Sub() 关闭全部())
            End If
        End Sub

        Friend Sub New(menu As ModernContextMenu, parent As MenuPopupForm)
            菜单 = menu
            父弹窗 = parent
            BackColor = menu.BackColor1
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer, True)
            AddHandler 动画计时器.Tick, AddressOf 动画更新帧
        End Sub

        Friend Sub ShowAt(x As Integer, y As Integer)
            计算布局()
            Me.Location = New Point(x, y)
            Dim scr = Screen.FromPoint(New Point(x, y)).WorkingArea
            If Me.Right > scr.Right Then Me.Left = scr.Right - Me.Width
            If Me.Bottom > scr.Bottom Then Me.Top = y - Me.Height
            If Me.Left < scr.Left Then Me.Left = scr.Left
            If Me.Top < scr.Top Then Me.Top = scr.Top
            If 父弹窗 Is Nothing Then Application.AddMessageFilter(Me)
            Me.Show()
        End Sub

        Private Sub 计算布局()
            Dim pad = 菜单.内边距
            Dim border = 菜单.边框宽度
            Dim currentY As Integer = pad.Top + border
            Dim maxContentWidth As Integer = 80
            项目区域列表.Clear()

            Dim leftArea = 菜单.有效左侧保留宽度
            Dim tp = 菜单.文本内边距

            For Each item In 菜单.项目列表
                If item.IsSeparator Then
                    项目区域列表.Add(New Rectangle(0, currentY, 0, 菜单.分割线高度))
                    currentY += 菜单.分割线高度
                Else
                    Dim font = If(item.IsDescription, If(item.Font, 菜单.说明字体), If(item.Font, 菜单.菜单字体))
                    Dim textWidth = TextRenderer.MeasureText(item.Text, font).Width
                    Dim w = leftArea + tp.Left + textWidth + tp.Right + 20
                    If Not item.IsDescription AndAlso item.SubMenu IsNot Nothing Then w += 20
                    maxContentWidth = Math.Max(maxContentWidth, w)
                    Dim h As Integer = If(item.IsDescription, 菜单.说明项高度, 菜单.项目高度)
                    项目区域列表.Add(New Rectangle(0, currentY, 0, h))
                    currentY += h
                End If
            Next

            Dim contentWidth = maxContentWidth + pad.Left + pad.Right
            Dim totalWidth = contentWidth + border * 2
            Dim totalHeight = currentY + pad.Bottom + border
            Dim itemX = border + pad.Left
            Dim itemWidth = totalWidth - border * 2 - pad.Left - pad.Right

            For i = 0 To 项目区域列表.Count - 1
                Dim r = 项目区域列表(i)
                项目区域列表(i) = New Rectangle(itemX, r.Y, itemWidth, r.Height)
            Next

            Me.ClientSize = New Size(totalWidth + 1, totalHeight + 1)
        End Sub

#Region "绘制"

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim _ssaa As Integer = If(Class1.GlobalSSAA > 1, Class1.GlobalSSAA, 菜单.超采样倍率)
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
            绘制全部文本(e.Graphics)
        End Sub

        Private Sub 绘制图形内容(g As Graphics)
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.InterpolationMode = InterpolationMode.HighQualityBicubic

            Using brush As New SolidBrush(菜单.背景颜色)
                g.FillRectangle(brush, ClientRectangle)
            End Using

            If 菜单.边框宽度 > 0 Then
                Dim bw = 菜单.边框宽度
                Dim cw = ClientSize.Width - 1
                Dim ch = ClientSize.Height - 1
                Using brush As New SolidBrush(菜单.边框颜色)
                    g.FillRectangle(brush, 0, 0, cw, bw)
                    g.FillRectangle(brush, 0, ch - bw, cw, bw)
                    g.FillRectangle(brush, 0, bw, bw, ch - bw * 2)
                    g.FillRectangle(brush, cw - bw, bw, bw, ch - bw * 2)
                End Using
            End If

            绘制悬停高亮(g)

            For i = 0 To 菜单.项目列表.Count - 1
                If i >= 项目区域列表.Count Then Exit For
                Dim item = 菜单.项目列表(i)
                Dim rect = 项目区域列表(i)
                If item.IsSeparator Then
                    绘制分割线(g, rect)
                ElseIf Not item.IsDescription Then
                    绘制项目图形(g, item, rect)
                End If
            Next
        End Sub

        Private Sub 绘制分割线(g As Graphics, rect As Rectangle)
            Dim lineY As Integer = rect.Y + (rect.Height - 1) \ 2
            Using brush As New SolidBrush(菜单.分割线颜色)
                g.FillRectangle(brush, rect.X, lineY, rect.Width, 1)
            End Using
        End Sub

        Private Sub 绘制悬停高亮(g As Graphics)
            If Not 动画显示高亮 Then Return
            Dim highlightRect As New RectangleF(
                项目区域列表(0).X, 动画当前Y,
                项目区域列表(0).Width, 动画当前高度)
            Dim highlightColor As Color = If(鼠标按下, 菜单.按下背景颜色, 菜单.悬停背景颜色)
            If 菜单.悬停圆角半径 > 0 Then
                Dim radius As Integer = Math.Min(菜单.悬停圆角半径, CInt(highlightRect.Height) \ 2)
                Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(highlightRect, radius)
                    Using brush As New SolidBrush(highlightColor)
                        g.FillPath(brush, path)
                    End Using
                End Using
            Else
                Using brush As New SolidBrush(highlightColor)
                    g.FillRectangle(brush, highlightRect.X, highlightRect.Y, highlightRect.Width, highlightRect.Height)
                End Using
            End If
        End Sub

        Private Sub 绘制项目图形(g As Graphics, item As ModernMenuItem, rect As Rectangle)
            Dim x As Integer = rect.X
            Dim leftArea = 菜单.有效左侧保留宽度

            If leftArea > 0 Then
                If item.Checked Then
                    绘制勾选标记(g, New Rectangle(x, rect.Y, leftArea, rect.Height))
                End If

                If item.Icon IsNot Nothing Then
                    Dim iconSize As Integer = Math.Min(leftArea - 4, rect.Height - 4)
                    Dim iconX As Integer = x + (leftArea - iconSize) \ 2
                    Dim iconY As Integer = rect.Y + (rect.Height - iconSize) \ 2
                    g.DrawImage(item.Icon, New Rectangle(iconX, iconY, iconSize, iconSize))
                End If
            End If

            If item.SubMenu IsNot Nothing Then
                绘制箭头(g, New Rectangle(rect.Right - 16, rect.Y, 16, rect.Height))
            End If
        End Sub

        Private Sub 绘制全部文本(g As Graphics)
            Dim leftArea = 菜单.有效左侧保留宽度
            Dim tp = 菜单.文本内边距

            For i = 0 To 菜单.项目列表.Count - 1
                If i >= 项目区域列表.Count Then Exit For
                Dim item = 菜单.项目列表(i)
                If item.IsSeparator Then Continue For
                Dim rect = 项目区域列表(i)
                Dim x As Integer = rect.X + leftArea + tp.Left
                Dim font As Font
                Dim foreColor As Color
                If item.IsDescription Then
                    font = If(item.Font, 菜单.说明字体)
                    foreColor = If(item.ForeColor <> Color.Empty, item.ForeColor, 菜单.说明文本颜色)
                Else
                    font = If(item.Font, 菜单.菜单字体)
                    foreColor = If(item.ForeColor <> Color.Empty, item.ForeColor, 菜单.文本颜色)
                End If
                Dim arrowSpace As Integer = If(Not item.IsDescription AndAlso item.SubMenu IsNot Nothing, 20, 0)
                Dim textRect As New Rectangle(x, rect.Y + tp.Top, rect.Width - leftArea - tp.Left - tp.Right - arrowSpace, rect.Height - tp.Top - tp.Bottom)
                TextRenderer.DrawText(g, item.Text, font, textRect, foreColor,
                    TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding)
            Next
        End Sub

        Private Sub 绘制勾选标记(g As Graphics, rect As Rectangle)
            Dim cx As Single = rect.X + rect.Width / 2.0F
            Dim cy As Single = rect.Y + rect.Height / 2.0F
            Dim s As Single = rect.Height * 0.18F
            Dim pw As Single = Math.Max(1.6F, rect.Height * 0.08F)

            Dim oldSmooth = g.SmoothingMode
            g.SmoothingMode = SmoothingMode.AntiAlias

            Using path As New GraphicsPath()
                path.AddLines({
                    New PointF(cx - s, cy),
                    New PointF(cx - s * 0.35F, cy + s * 0.85F),
                    New PointF(cx + s, cy - s)
                })
                Using wp As New Pen(Color.Black, pw)
                    wp.StartCap = LineCap.Round
                    wp.EndCap = LineCap.Round
                    wp.LineJoin = LineJoin.Round
                    path.Widen(wp)
                End Using
                Using brush As New SolidBrush(菜单.勾选颜色)
                    g.FillPath(brush, path)
                End Using
            End Using

            g.SmoothingMode = oldSmooth
        End Sub

        Private Sub 绘制箭头(g As Graphics, rect As Rectangle)
            Dim cx As Single = rect.X + rect.Width / 2.0F
            Dim cy As Single = rect.Y + rect.Height / 2.0F
            Dim arrSize As Single = 菜单.箭头大小
            Dim arrH As Single = arrSize
            Dim arrW As Single = CSng(arrSize * Math.Sqrt(3.0) / 2.0)

            Dim verts() As PointF = {
                New PointF(cx - arrW / 2.0F, cy - arrH / 2.0F),
                New PointF(cx - arrW / 2.0F, cy + arrH / 2.0F),
                New PointF(cx + arrW / 2.0F, cy)
            }
            Dim cr As Single = Math.Max(arrSize * 0.2F, 1.0F)

            Dim oldSmooth = g.SmoothingMode
            g.SmoothingMode = SmoothingMode.AntiAlias

            Using path As New GraphicsPath()
                For i As Integer = 0 To 2
                    Dim curr As PointF = verts(i)
                    Dim prv As PointF = verts((i + 2) Mod 3)
                    Dim nxt As PointF = verts((i + 1) Mod 3)
                    Dim d1x As Single = prv.X - curr.X, d1y As Single = prv.Y - curr.Y
                    Dim d2x As Single = nxt.X - curr.X, d2y As Single = nxt.Y - curr.Y
                    Dim l1 As Single = CSng(Math.Sqrt(d1x * d1x + d1y * d1y))
                    Dim l2 As Single = CSng(Math.Sqrt(d2x * d2x + d2y * d2y))
                    Dim a As New PointF(curr.X + cr * d1x / l1, curr.Y + cr * d1y / l1)
                    Dim b As New PointF(curr.X + cr * d2x / l2, curr.Y + cr * d2y / l2)
                    Dim cp1 As New PointF(a.X + 2.0F / 3.0F * (curr.X - a.X), a.Y + 2.0F / 3.0F * (curr.Y - a.Y))
                    Dim cp2 As New PointF(b.X + 2.0F / 3.0F * (curr.X - b.X), b.Y + 2.0F / 3.0F * (curr.Y - b.Y))
                    If i > 0 Then path.AddLine(path.GetLastPoint(), a)
                    path.AddBezier(a, cp1, cp2, b)
                Next
                path.CloseFigure()
                Using brush As New SolidBrush(菜单.箭头颜色)
                    g.FillPath(brush, path)
                End Using
            End Using

            g.SmoothingMode = oldSmooth
        End Sub

#End Region

#Region "鼠标交互"

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim newIndex = 获取项目索引(e.Location)
            If newIndex <> 悬停索引 Then
                悬停索引 = newIndex
                更新悬停动画()
                Invalidate()
                处理子菜单悬停()
            End If
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button = MouseButtons.Left OrElse e.Button = MouseButtons.Right Then
                鼠标按下 = True
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            鼠标按下 = False
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            If e.Button <> MouseButtons.Left AndAlso e.Button <> MouseButtons.Right Then Return
            Dim index = 获取项目索引(e.Location)
            If index < 0 OrElse index >= 菜单.项目列表.Count Then Return
            Dim item = 菜单.项目列表(index)
            If item.IsSeparator Then Return
            If item.SubMenu IsNot Nothing Then Return
            item.PerformClick()
            If item.CloseOnClick Then
                关闭全部()
            Else
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            If 子菜单弹窗 Is Nothing OrElse 子菜单弹窗.IsDisposed Then
                悬停索引 = -1
                更新悬停动画()
                Invalidate()
            End If
        End Sub

        Private Function 获取项目索引(location As Point) As Integer
            For i = 0 To 项目区域列表.Count - 1
                If 项目区域列表(i).Contains(location) Then
                    If i < 菜单.项目列表.Count AndAlso (菜单.项目列表(i).IsSeparator OrElse 菜单.项目列表(i).IsDescription) Then Return -1
                    Return i
                End If
            Next
            Return -1
        End Function

#End Region

#Region "子菜单管理"

        Private Sub 处理子菜单悬停()
            If 子菜单弹窗 IsNot Nothing AndAlso Not 子菜单弹窗.IsDisposed Then
                子菜单弹窗.关闭自身及子菜单()
                子菜单弹窗 = Nothing
            End If
            If 悬停索引 < 0 OrElse 悬停索引 >= 菜单.项目列表.Count Then Return
            Dim item = 菜单.项目列表(悬停索引)
            If item.IsSeparator Then Return
            If item.SubMenu Is Nothing OrElse item.SubMenu.Items.Count = 0 Then Return
            Dim rect = 项目区域列表(悬停索引)
            Dim screenPt = Me.PointToScreen(New Point(rect.Right, rect.Top))
            子菜单弹窗 = New MenuPopupForm(item.SubMenu, Me)
            子菜单弹窗.ShowAt(screenPt.X, screenPt.Y)
        End Sub

#End Region

#Region "悬停动画"

        Private Sub 更新悬停动画()
            If 悬停索引 >= 0 AndAlso 悬停索引 < 项目区域列表.Count Then
                Dim rect = 项目区域列表(悬停索引)
                Dim targetY As Single = rect.Y
                Dim targetH As Single = rect.Height

                If 菜单.悬停动画时长 <= 0 OrElse Not 动画显示高亮 Then
                    ' 无动画或首次出现，直接跳到目标
                    动画起始Y = targetY
                    动画目标Y = targetY
                    动画当前Y = targetY
                    动画起始高度 = targetH
                    动画目标高度 = targetH
                    动画当前高度 = targetH
                    动画显示高亮 = True
                    停止动画()
                    Return
                End If

                动画起始Y = 动画当前Y
                动画目标Y = targetY
                动画起始高度 = 动画当前高度
                动画目标高度 = targetH
                动画显示高亮 = True
                动画秒表.Restart()
                If Not 动画中 Then
                    动画中 = True
                    动画计时器.Start()
                End If
            Else
                ' 悬停离开
                动画显示高亮 = False
                停止动画()
            End If
        End Sub

        Private Sub 动画更新帧(sender As Object, e As EventArgs)
            Dim duration = 菜单.悬停动画时长
            If duration <= 0 Then
                动画当前Y = 动画目标Y
                动画当前高度 = 动画目标高度
                停止动画()
                Invalidate()
                Return
            End If

            Dim elapsed As Double = 动画秒表.Elapsed.TotalMilliseconds
            Dim t As Single = CSng(Math.Min(elapsed / duration, 1.0))
            ' ease-out cubic
            Dim eased As Single = 1.0F - CSng(Math.Pow(1.0 - t, 3))
            动画当前Y = 动画起始Y + (动画目标Y - 动画起始Y) * eased
            动画当前高度 = 动画起始高度 + (动画目标高度 - 动画起始高度) * eased

            If t >= 1.0F Then
                动画当前Y = 动画目标Y
                动画当前高度 = 动画目标高度
                停止动画()
            End If
            Invalidate()
        End Sub

        Private Sub 停止动画()
            If 动画中 Then
                动画中 = False
                动画计时器.Stop()
                动画秒表.Stop()
            End If
        End Sub

#End Region

#Region "关闭逻辑"

        Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
            Select Case m.Msg
                Case WM_LBUTTONDOWN, WM_RBUTTONDOWN, WM_MBUTTONDOWN, WM_NCLBUTTONDOWN
                    If Not 点击在菜单链内(Control.MousePosition) Then
                        BeginInvoke(Sub() 关闭全部())
                    End If
                Case WM_KEYDOWN
                    If CInt(m.WParam) = Keys.Escape Then
                        BeginInvoke(Sub() 关闭全部())
                        Return True
                    End If
            End Select
            Return False
        End Function

        Private Function 点击在菜单链内(screenPos As Point) As Boolean
            If Not IsDisposed AndAlso Bounds.Contains(screenPos) Then Return True
            If 子菜单弹窗 IsNot Nothing AndAlso Not 子菜单弹窗.IsDisposed Then
                Return 子菜单弹窗.点击在菜单链内(screenPos)
            End If
            Return False
        End Function

        Friend Sub 关闭全部()
            获取根弹窗().关闭自身及子菜单()
        End Sub

        Friend Sub 关闭自身及子菜单()
            If 正在关闭 Then Return
            正在关闭 = True
            停止动画()
            动画计时器.Dispose()
            If 子菜单弹窗 IsNot Nothing AndAlso Not 子菜单弹窗.IsDisposed Then
                子菜单弹窗.关闭自身及子菜单()
                子菜单弹窗 = Nothing
            End If
            If 父弹窗 Is Nothing Then
                Application.RemoveMessageFilter(Me)
                菜单.通知菜单关闭()
            End If
            If Not IsDisposed Then Close()
        End Sub

        Private Function 获取根弹窗() As MenuPopupForm
            If 父弹窗 IsNot Nothing Then Return 父弹窗.获取根弹窗()
            Return Me
        End Function

        Protected Overrides Sub OnDeactivate(e As EventArgs)
            MyBase.OnDeactivate(e)
            If 正在关闭 OrElse IsDisposed OrElse Not IsHandleCreated Then Return
            Try
                BeginInvoke(Sub()
                                If 正在关闭 Then Return
                                Dim root = 获取根弹窗()
                                If Not root.链中有活动窗口() Then
                                    root.关闭自身及子菜单()
                                End If
                            End Sub)
            Catch ex As InvalidOperationException
            End Try
        End Sub

        Private Function 链中有活动窗口() As Boolean
            If Not IsDisposed AndAlso Me Is Form.ActiveForm Then Return True
            If 子菜单弹窗 IsNot Nothing AndAlso Not 子菜单弹窗.IsDisposed Then
                Return 子菜单弹窗.链中有活动窗口()
            End If
            Return False
        End Function

        Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
            MyBase.OnFormClosed(e)
            If 父弹窗 Is Nothing AndAlso Not 正在关闭 Then
                Application.RemoveMessageFilter(Me)
            End If
        End Sub

#End Region

    End Class

#End Region

#Region "释放资源"

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            Close()
            For Each ctrl In 绑定控件集合.Keys.ToList()
                RemoveHandler ctrl.MouseUp, AddressOf 绑定控件_MouseUp
                RemoveHandler ctrl.Disposed, AddressOf 绑定控件_Disposed
            Next
            绑定控件集合.Clear()
            If 菜单字体 IsNot Nothing Then
                菜单字体.Dispose()
                菜单字体 = Nothing
            End If
            If 说明字体 IsNot Nothing Then
                说明字体.Dispose()
                说明字体 = Nothing
            End If
        End If
        MyBase.Dispose(disposing)
    End Sub

#End Region

    Public Class ModernMenuItem

        <Category("LakeUI"), Description("是否是分割线"), DefaultValue(False), Browsable(True)>
        Public Property IsSeparator As Boolean = False

        <Category("LakeUI"), Description("是否是描述文本"), DefaultValue(False), Browsable(True)>
        Public Property IsDescription As Boolean = False

        <Category("LakeUI"), Description("文本"), DefaultValue(GetType(String), ""), Browsable(True)>
        Public Property Text As String = ""

        <Category("LakeUI"), Description("字体"), Browsable(True)>
        Public Property Font As Font = Nothing

        <Category("LakeUI"), Description("文本颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
        Public Property ForeColor As Color = Color.Empty

        <Category("LakeUI"), Description("图标"), DefaultValue(GetType(Image), Nothing), Browsable(True)>
        Public Property Icon As Image = Nothing

        <Category("LakeUI"), Description("是否选中"), DefaultValue(False), Browsable(True)>
        Public Property Checked As Boolean = False

        <Category("LakeUI"), Description("点击后自动切换勾选状态"), DefaultValue(False), Browsable(True)>
        Public Property ToggleCheckOnClick As Boolean = False

        <Category("LakeUI"), Description("点击后关闭所在菜单"), DefaultValue(True), Browsable(True)>
        Public Property CloseOnClick As Boolean = True

        <Category("LakeUI"), Description("绑定的子菜单"), DefaultValue(GetType(ModernContextMenu), Nothing), Browsable(True)>
        Public Property SubMenu As ModernContextMenu = Nothing

        Public Event Click As EventHandler

        Friend Sub PerformClick()
            If ToggleCheckOnClick Then Checked = Not Checked
            RaiseEvent Click(Me, EventArgs.Empty)
        End Sub

        Public Sub New()
        End Sub

        Public Sub New(text As String)
            Me.Text = text
        End Sub

        Public Sub New(text As String, icon As Image)
            Me.Text = text
            Me.Icon = icon
        End Sub

        Public Overrides Function ToString() As String
            If IsSeparator Then Return "─── Separator ───"
            If IsDescription Then Return $"[说明] {Text}"
            Return If(String.IsNullOrEmpty(Text), "ModernMenuItem", Text)
        End Function

    End Class

End Class