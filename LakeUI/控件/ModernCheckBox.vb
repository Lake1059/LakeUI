Imports System.ComponentModel
Imports System.Drawing.Drawing2D

<DefaultEvent("CheckedChanged")>
Public Class ModernCheckBox

    Public Sub New()
        InitializeComponent()
        SetStyle(ControlStyles.StandardDoubleClick, False)
    End Sub

    Public Event CheckedChanged As EventHandler

#Region "枚举"
    Public Enum CheckModeEnum
        CheckBox
        RadioButton
    End Enum

    Private Enum MouseStateEnum
        Normal
        Hover
        Pressed
    End Enum
#End Region

#Region "绘制"
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
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
        绘制文本内容(e.Graphics)
        If Not Enabled Then
            Using brush As New SolidBrush(Color.FromArgb(120, 0, 0, 0))
                e.Graphics.FillRectangle(brush, 0, 0, Me.Width, Me.Height)
            End Using
        End If
    End Sub

    Private Sub 绘制图形内容(g As Graphics)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        g.InterpolationMode = InterpolationMode.HighQualityBicubic

        Dim s As Single = DpiScale()
        Dim 当前框边框宽度 As Single = 框边框宽度 * s
        Dim 框区域 As RectangleF = 计算框区域(s)

        Dim 当前框背景色 As Color = 获取当前框背景颜色()
        Dim 当前框边框色 As Color = 获取鼠标状态颜色(框边框颜色值, 鼠标移上时框边框颜色, 鼠标按下时框边框颜色)

        If 当前模式 = CheckModeEnum.CheckBox Then
            绘制方框(g, 框区域, 当前框背景色, 当前框边框色, 当前框边框宽度, s)
        Else
            绘制圆框(g, 框区域, 当前框背景色, 当前框边框色, 当前框边框宽度, s)
        End If
    End Sub

    Private Function 计算主文本Y(s As Single) As Single
        Dim 主文本高度 As Integer = 获取主文本行高()
        If Not String.IsNullOrEmpty(次要文本) Then
            Dim 次文本高度 As Integer = 获取次文本行高()
            Dim _主次间距 As Integer = CInt(Math.Round(主次文本间距 * s))
            Dim 文本总高度 As Integer = 主文本高度 + _主次间距 + 次文本高度
            Return Me.Padding.Top + (Me.Height - Me.Padding.Vertical - 文本总高度) / 2.0F
        Else
            Return Me.Padding.Top + (Me.Height - Me.Padding.Vertical - 主文本高度) / 2.0F
        End If
    End Function

    Private Function 计算框区域(s As Single) As RectangleF
        Dim 框尺寸 As Single = 操作框尺寸 * s
        Dim 边框偏移 As Single = 框边框宽度 * s / 2.0F
        Dim 主文本Y As Single = 计算主文本Y(s)
        Dim 主文本高度 As Integer = 获取主文本行高()
        Dim 框X As Single = Me.Padding.Left + 边框偏移
        Dim 框Y As Single = Math.Max(Me.Padding.Top + 边框偏移, 主文本Y + (主文本高度 - 框尺寸) / 2.0F)
        Return New RectangleF(框X, 框Y, 框尺寸, 框尺寸)
    End Function

    Private Sub 绘制方框(g As Graphics, 框区域 As RectangleF, 背景色 As Color, 边框色 As Color, 边框宽 As Single, s As Single)
        Dim 圆角 As Single = 框圆角半径 * s
        If 圆角 > 0 Then
            Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(框区域, 圆角)
                Using brush As New SolidBrush(背景色)
                    g.FillPath(brush, path)
                End Using
                RectangleRenderer.绘制圆角边框(g, path, 边框色, 边框宽)
            End Using
        Else
            Using brush As New SolidBrush(背景色)
                g.FillRectangle(brush, 框区域)
            End Using
            If 边框宽 > 0 Then
                Using pen As New Pen(边框色, 边框宽)
                    g.DrawRectangle(pen, 框区域.X, 框区域.Y, 框区域.Width, 框区域.Height)
                End Using
            End If
        End If
        ' 绘制勾号笔迹动画
        Dim progress As Single = 动画助手.Progress
        If progress > 0.001F Then
            绘制勾号(g, 框区域, progress, s)
        End If
    End Sub

    Private Sub 绘制勾号(g As Graphics, 框区域 As RectangleF, progress As Single, s As Single)
        Dim 当前勾号色 As Color = 获取鼠标状态颜色(勾号颜色值, 鼠标移上时勾号颜色, 鼠标按下时勾号颜色)
        Dim 内边距 As Single = 框内边距 * s + 框边框宽度 * s / 2.0F
        Dim x1 As Single = 框区域.X + 内边距
        Dim y1 As Single = 框区域.Y + 框区域.Height * 0.5F
        Dim x2 As Single = 框区域.X + 框区域.Width * 0.4F
        Dim y2 As Single = 框区域.Bottom - 内边距
        Dim x3 As Single = 框区域.Right - 内边距
        Dim y3 As Single = 框区域.Y + 内边距
        Dim 段1长 As Single = CSng(Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1)))
        Dim 段2长 As Single = CSng(Math.Sqrt((x3 - x2) * (x3 - x2) + (y3 - y2) * (y3 - y2)))
        Dim 总长度 As Single = 段1长 + 段2长
        If 总长度 < 0.01F Then Return
        Dim 笔宽 As Single = 勾号线宽 * s
        Using path As New GraphicsPath()
            path.AddLine(x1, y1, x2, y2)
            path.AddLine(x2, y2, x3, y3)
            Using pen As New Pen(当前勾号色, 笔宽)
                pen.StartCap = LineCap.Round
                pen.EndCap = LineCap.Round
                pen.LineJoin = LineJoin.Round
                Dim 可见长度 As Single = 总长度 * progress
                Dim 不可见长度 As Single = 总长度 - 可见长度
                pen.DashStyle = DashStyle.Dash
                pen.DashPattern = New Single() {可见长度 / 笔宽, 不可见长度 / 笔宽 + 1}
                g.DrawPath(pen, path)
            End Using
        End Using
    End Sub

    Private Sub 绘制圆框(g As Graphics, 框区域 As RectangleF, 背景色 As Color, 边框色 As Color, 边框宽 As Single, s As Single)
        Using brush As New SolidBrush(背景色)
            g.FillEllipse(brush, 框区域)
        End Using
        RectangleRenderer.绘制椭圆边框(g, 框区域, 边框色, 边框宽)
        ' 绘制内圆缩放动画
        Dim progress As Single = 动画助手.Progress
        If progress > 0.001F Then
            Dim 当前勾号色 As Color = 获取鼠标状态颜色(勾号颜色值, 鼠标移上时勾号颜色, 鼠标按下时勾号颜色)
            Dim 最大半径 As Single = (框区域.Width / 2.0F) - 框内边距 * s - 框边框宽度 * s / 2.0F
            If 最大半径 < 1 Then 最大半径 = 1
            Dim 当前半径 As Single = 最大半径 * progress
            Dim cx As Single = 框区域.X + 框区域.Width / 2.0F
            Dim cy As Single = 框区域.Y + 框区域.Height / 2.0F
            Using brush As New SolidBrush(当前勾号色)
                g.FillEllipse(brush, cx - 当前半径, cy - 当前半径, 当前半径 * 2, 当前半径 * 2)
            End Using
        End If
    End Sub

    Private Sub 绘制文本内容(g As Graphics)
        Dim s As Single = DpiScale()
        Dim 框尺寸 As Single = 操作框尺寸 * s
        Dim 间距 As Single = 框文本间距 * s
        Dim 边框偏移 As Single = 框边框宽度 * s / 2.0F
        Dim 文本X As Integer = CInt(Me.Padding.Left + 边框偏移 + 框尺寸 + 间距)
        Dim 文本可用宽度 As Integer = Me.Width - 文本X - Me.Padding.Right
        If 文本可用宽度 <= 0 Then Return
        Dim 文本格式 As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
        Dim 主文本Y As Integer = CInt(计算主文本Y(s))
        Dim 主文本高度 As Integer = 获取主文本行高()
        If Not String.IsNullOrEmpty(次要文本) Then
            Using 次要文本字体 As New Font(Me.Font.FontFamily, 次要文本字号, FontStyle.Regular)
                Dim 主文本尺寸 As Size = TextRenderer.MeasureText(g, MyBase.Text, Me.Font, New Size(文本可用宽度, Integer.MaxValue), 文本格式)
                Dim 次文本尺寸 As Size = TextRenderer.MeasureText(g, 次要文本, 次要文本字体, New Size(文本可用宽度, Integer.MaxValue), 文本格式)
                Dim _主次间距 As Integer = CInt(Math.Round(主次文本间距 * s))
                Dim 主文本区域 As New Rectangle(文本X, 主文本Y, 文本可用宽度, 主文本尺寸.Height)
                TextRenderer.DrawText(g, MyBase.Text, Me.Font, 主文本区域, 文本颜色, 文本格式)
                Dim 次文本区域 As New Rectangle(文本X, 主文本Y + 主文本尺寸.Height + _主次间距, 文本可用宽度, 次文本尺寸.Height)
                TextRenderer.DrawText(g, 次要文本, 次要文本字体, 次文本区域, 次要文本颜色, 文本格式)
            End Using
        Else
            Dim 主文本区域 As New Rectangle(文本X, 主文本Y, 文本可用宽度, 主文本高度)
            TextRenderer.DrawText(g, MyBase.Text, Me.Font, 主文本区域, 文本颜色, 文本格式)
        End If
    End Sub
#End Region

#Region "颜色计算"
    Private Function 获取当前框背景颜色() As Color
        Dim 选中色 As Color = 获取状态框背景颜色(True)
        Dim 未选中色 As Color = 获取状态框背景颜色(False)
        Return 颜色插值(未选中色, 选中色, 动画助手.Progress)
    End Function

    Private Function 获取状态框背景颜色(isChecked As Boolean) As Color
        If isChecked Then
            Select Case 鼠标状态
                Case MouseStateEnum.Hover
                    If 鼠标移上时选中框背景颜色 <> Color.Empty Then Return 鼠标移上时选中框背景颜色
                Case MouseStateEnum.Pressed
                    If 鼠标按下时选中框背景颜色 <> Color.Empty Then Return 鼠标按下时选中框背景颜色
            End Select
            Return 选中时框背景颜色
        Else
            Select Case 鼠标状态
                Case MouseStateEnum.Hover
                    If 鼠标移上时未选中框背景颜色 <> Color.Empty Then Return 鼠标移上时未选中框背景颜色
                Case MouseStateEnum.Pressed
                    If 鼠标按下时未选中框背景颜色 <> Color.Empty Then Return 鼠标按下时未选中框背景颜色
            End Select
            Return 未选中时框背景颜色
        End If
    End Function

    Private Function 获取鼠标状态颜色(默认颜色 As Color, hover颜色 As Color, pressed颜色 As Color) As Color
        Select Case 鼠标状态
            Case MouseStateEnum.Hover
                If hover颜色 <> Color.Empty Then Return hover颜色
            Case MouseStateEnum.Pressed
                If pressed颜色 <> Color.Empty Then Return pressed颜色
        End Select
        Return 默认颜色
    End Function

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

#Region "鼠标状态"
    Private 鼠标状态 As MouseStateEnum = MouseStateEnum.Normal
    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        If Not Enabled Then Return
        鼠标状态 = MouseStateEnum.Hover
        Me.Invalidate()
    End Sub
    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If Not Enabled Then Return
        鼠标状态 = MouseStateEnum.Normal
        Me.Invalidate()
    End Sub
    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not Enabled Then Return
        鼠标状态 = MouseStateEnum.Pressed
        Me.Invalidate()
    End Sub
    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If Not Enabled Then Return
        Dim isInside As Boolean = ClientRectangle.Contains(e.Location)
        鼠标状态 = If(isInside, MouseStateEnum.Hover, MouseStateEnum.Normal)
        If isInside AndAlso 点击命中操作框(e.Location) Then
            If 当前模式 = CheckModeEnum.RadioButton Then
                If Not 已选中 Then
                    Checked = True
                End If
            Else
                Checked = Not Checked
            End If
        End If
        Me.Invalidate()
    End Sub
    Private Function 点击命中操作框(位置 As Point) As Boolean
        If 允许任意区域点击 Then Return True
        Dim s As Single = DpiScale()
        Dim 边框偏移 As Single = 框边框宽度 * s / 2.0F
        Dim 框区域 As RectangleF = 计算框区域(s)
        Dim 命中区域 As New RectangleF(框区域.X - 边框偏移, 框区域.Y - 边框偏移, 框区域.Width + 框边框宽度 * s, 框区域.Height + 框边框宽度 * s)
        Return 命中区域.Contains(位置)
    End Function
    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        If Not Enabled Then
            鼠标状态 = MouseStateEnum.Normal
            动画助手.StopAnimation()
        End If
        Me.Invalidate()
    End Sub
    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        重置文本行高缓存()
        更新自动尺寸()
        Me.Invalidate()
    End Sub
#End Region

#Region "RadioButton 容器逻辑"
    Private Sub 取消同组其他选中()
        If Me.Parent Is Nothing Then Return
        For Each ctrl As Control In Me.Parent.Controls
            If ctrl Is Me Then Continue For
            Dim other = TryCast(ctrl, ModernCheckBox)
            If other IsNot Nothing AndAlso other.CheckMode = CheckModeEnum.RadioButton AndAlso other.Checked Then
                other.Checked = False
            End If
        Next
    End Sub
#End Region

#Region "通用"
    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            更新自动尺寸()
            Me.Invalidate()
        End If
    End Sub

    Private ReadOnly 动画助手 As New AnimationHelper(Me)

    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function

    Private _缓存主文本行高 As Integer = -1
    Private _缓存次文本行高 As Integer = -1

    Private Function 获取主文本行高() As Integer
        If _缓存主文本行高 < 0 Then
            _缓存主文本行高 = TextRenderer.MeasureText("A", Me.Font).Height
        End If
        Return _缓存主文本行高
    End Function

    Private Function 获取次文本行高() As Integer
        If _缓存次文本行高 < 0 AndAlso Not String.IsNullOrEmpty(次要文本) Then
            Using f As New Font(Me.Font.FontFamily, 次要文本字号, FontStyle.Regular)
                _缓存次文本行高 = TextRenderer.MeasureText("A", f).Height
            End Using
        End If
        Return _缓存次文本行高
    End Function

    Private Sub 重置文本行高缓存()
        _缓存主文本行高 = -1
        _缓存次文本行高 = -1
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

    <Category("LakeUI"), Description(Class1.动画时长描述词), DefaultValue(300), Browsable(True)>
    Public Property AnimationDuration As Integer
        Get
            Return 动画助手.Duration
        End Get
        Set(value As Integer)
            动画助手.Duration = Math.Max(0, value)
        End Set
    End Property

    <Category("LakeUI"), Description(Class1.动画帧率描述词), DefaultValue(60), Browsable(True)>
    Public Property AnimationFPS As Integer
        Get
            Return 动画助手.FPS
        End Get
        Set(value As Integer)
            动画助手.FPS = Math.Max(0, value)
        End Set
    End Property
#End Region

#Region "模式属性"
    Private 当前模式 As CheckModeEnum = CheckModeEnum.CheckBox
    <Category("LakeUI"), Description("操作模式：CheckBox 多选模式，RadioButton 单选模式（同容器互斥）"), DefaultValue(GetType(CheckModeEnum), "CheckBox"), Browsable(True)>
    Public Property CheckMode As CheckModeEnum
        Get
            Return 当前模式
        End Get
        Set(value As CheckModeEnum)
            SetValue(当前模式, value)
        End Set
    End Property

    Private 允许任意区域点击 As Boolean = False
    <Category("LakeUI"), Description("是否允许点击控件任意区域触发选中状态变更，默认仅操作框区域触发"), DefaultValue(False), Browsable(True)>
    Public Property ClickAnywhere As Boolean
        Get
            Return 允许任意区域点击
        End Get
        Set(value As Boolean)
            允许任意区域点击 = value
        End Set
    End Property
#End Region

#Region "选中状态"
    Private 已选中 As Boolean = False
    <Category("LakeUI"), Description("选中状态"), DefaultValue(False), Browsable(True)>
    Public Property Checked As Boolean
        Get
            Return 已选中
        End Get
        Set(value As Boolean)
            If 已选中 <> value Then
                已选中 = value
                动画助手.AnimateTo(If(value, 1.0F, 0.0F))
                If value AndAlso 当前模式 = CheckModeEnum.RadioButton Then
                    取消同组其他选中()
                End If
                RaiseEvent CheckedChanged(Me, EventArgs.Empty)
            End If
        End Set
    End Property
#End Region

#Region "操作框属性"
    Private 操作框尺寸 As Integer = 16
    <Category("LakeUI"), Description("操作框尺寸（逻辑像素）"), DefaultValue(16), Browsable(True)>
    Public Property BoxSize As Integer
        Get
            Return 操作框尺寸
        End Get
        Set(value As Integer)
            SetValue(操作框尺寸, Math.Max(8, value))
        End Set
    End Property

    Private 框圆角半径 As Integer = 2
    <Category("LakeUI"), Description("CheckBox 模式下操作框圆角半径"), DefaultValue(2), Browsable(True)>
    Public Property BoxBorderRadius As Integer
        Get
            Return 框圆角半径
        End Get
        Set(value As Integer)
            SetValue(框圆角半径, value)
        End Set
    End Property

    Private 框边框宽度 As Integer = 1
    <Category("LakeUI"), Description("操作框边框宽度"), DefaultValue(1), Browsable(True)>
    Public Property BoxBorderSize As Integer
        Get
            Return 框边框宽度
        End Get
        Set(value As Integer)
            SetValue(框边框宽度, value)
        End Set
    End Property

    Private 框边框颜色值 As Color = Color.Gray
    <Category("LakeUI"), Description("操作框边框颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property BoxBorderColor As Color
        Get
            Return 框边框颜色值
        End Get
        Set(value As Color)
            SetValue(框边框颜色值, value)
        End Set
    End Property

    Private 框文本间距 As Integer = 6
    <Category("LakeUI"), Description("操作框与文本之间的间距"), DefaultValue(6), Browsable(True)>
    Public Property BoxTextSpacing As Integer
        Get
            Return 框文本间距
        End Get
        Set(value As Integer)
            SetValue(框文本间距, value)
        End Set
    End Property

    Private 框内边距 As Integer = 3
    <Category("LakeUI"), Description("操作框内部边距，控制勾号四周的间距和实心圆到边框圆的距离"), DefaultValue(3), Browsable(True)>
    Public Property BoxInnerPadding As Integer
        Get
            Return 框内边距
        End Get
        Set(value As Integer)
            SetValue(框内边距, Math.Max(0, value))
        End Set
    End Property
#End Region

#Region "框背景颜色属性"
    Private 选中时框背景颜色 As Color = Color.FromArgb(0, 120, 215)
    <Category("LakeUI"), Description("选中时操作框背景颜色"), DefaultValue(GetType(Color), "0, 120, 215"), Browsable(True)>
    Public Property BoxCheckedBackColor As Color
        Get
            Return 选中时框背景颜色
        End Get
        Set(value As Color)
            SetValue(选中时框背景颜色, value)
        End Set
    End Property

    Private 未选中时框背景颜色 As Color = Color.FromArgb(50, 50, 50)
    <Category("LakeUI"), Description("未选中时操作框背景颜色"), DefaultValue(GetType(Color), "50, 50, 50"), Browsable(True)>
    Public Property BoxUncheckedBackColor As Color
        Get
            Return 未选中时框背景颜色
        End Get
        Set(value As Color)
            SetValue(未选中时框背景颜色, value)
        End Set
    End Property
#End Region

#Region "勾号/圆点属性"
    Private 勾号颜色值 As Color = Color.White
    <Category("LakeUI"), Description("勾号或圆点颜色"), DefaultValue(GetType(Color), "White"), Browsable(True)>
    Public Property CheckMarkColor As Color
        Get
            Return 勾号颜色值
        End Get
        Set(value As Color)
            SetValue(勾号颜色值, value)
        End Set
    End Property

    Private 勾号线宽 As Single = 2.0F
    <Category("LakeUI"), Description("勾号线条宽度（逻辑像素）"), DefaultValue(2.0F), Browsable(True)>
    Public Property CheckMarkWidth As Single
        Get
            Return 勾号线宽
        End Get
        Set(value As Single)
            SetValue(勾号线宽, Math.Max(0.5F, value))
        End Set
    End Property
#End Region

#Region "文本属性"
    <Category("LakeUI"), Description("主要文本"), DefaultValue(GetType(String), "ModernCheckBox"), Browsable(True), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
    Public Overrides Property Text As String
        Get
            Return MyBase.Text
        End Get
        Set(value As String)
            If MyBase.Text <> value Then
                MyBase.Text = value
                更新自动尺寸()
                Me.Invalidate()
            End If
        End Set
    End Property
    Private Function ShouldSerializeText() As Boolean
        Return Not String.IsNullOrEmpty(Text)
    End Function
    Public Overrides Sub ResetText()
        Text = String.Empty
    End Sub

    Private 文本颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("文本颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Overrides Property ForeColor As Color
        Get
            Return 文本颜色
        End Get
        Set(value As Color)
            SetValue(文本颜色, value)
        End Set
    End Property

    Private 次要文本 As String = ""
    <Category("LakeUI"), Description("次要文本，绘制在主文本下方"), DefaultValue(GetType(String), ""), Browsable(True)>
    Public Property SubText As String
        Get
            Return 次要文本
        End Get
        Set(value As String)
            SetValue(次要文本, value)
        End Set
    End Property

    Private 次要文本颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("次要文本颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property SubTextForeColor As Color
        Get
            Return 次要文本颜色
        End Get
        Set(value As Color)
            SetValue(次要文本颜色, value)
        End Set
    End Property

    Private 次要文本字号 As Integer = 9
    <Category("LakeUI"), Description("次要文本字号"), DefaultValue(9), Browsable(True)>
    Public Property SubTextSize As Integer
        Get
            Return 次要文本字号
        End Get
        Set(value As Integer)
            _缓存次文本行高 = -1
            SetValue(次要文本字号, value)
        End Set
    End Property

    Private 主次文本间距 As Integer = 1
    <Category("LakeUI"), Description("主次文本间距"), DefaultValue(1), Browsable(True)>
    Public Property MainSubTextSpacing As Integer
        Get
            Return 主次文本间距
        End Get
        Set(value As Integer)
            SetValue(主次文本间距, value)
        End Set
    End Property
#End Region

#Region "交互状态颜色属性"
    Private 鼠标移上时选中框背景颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时选中框背景颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverCheckedBackColor As Color
        Get
            Return 鼠标移上时选中框背景颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时选中框背景颜色, value)
        End Set
    End Property

    Private 鼠标移上时未选中框背景颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时未选中框背景颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverUncheckedBackColor As Color
        Get
            Return 鼠标移上时未选中框背景颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时未选中框背景颜色, value)
        End Set
    End Property

    Private 鼠标移上时框边框颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时框边框颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverBoxBorderColor As Color
        Get
            Return 鼠标移上时框边框颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时框边框颜色, value)
        End Set
    End Property

    Private 鼠标移上时勾号颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时勾号/圆点颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverCheckMarkColor As Color
        Get
            Return 鼠标移上时勾号颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时勾号颜色, value)
        End Set
    End Property

    Private 鼠标按下时选中框背景颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时选中框背景颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedCheckedBackColor As Color
        Get
            Return 鼠标按下时选中框背景颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时选中框背景颜色, value)
        End Set
    End Property

    Private 鼠标按下时未选中框背景颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时未选中框背景颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedUncheckedBackColor As Color
        Get
            Return 鼠标按下时未选中框背景颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时未选中框背景颜色, value)
        End Set
    End Property

    Private 鼠标按下时框边框颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时框边框颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedBoxBorderColor As Color
        Get
            Return 鼠标按下时框边框颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时框边框颜色, value)
        End Set
    End Property

    Private 鼠标按下时勾号颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时勾号/圆点颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedCheckMarkColor As Color
        Get
            Return 鼠标按下时勾号颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时勾号颜色, value)
        End Set
    End Property
#End Region

#Region "AutoSize 和 Dock"
    Private 启用自动尺寸 As Boolean = False
    Private 自动尺寸前的大小 As Size = Size.Empty
    <Category("LakeUI"), Description("启用自动尺寸，控件将根据文本内容自动调整大小"), DefaultValue(False), Browsable(True)>
    Public Overrides Property AutoSize As Boolean
        Get
            Return 启用自动尺寸
        End Get
        Set(value As Boolean)
            If 启用自动尺寸 <> value Then
                启用自动尺寸 = value
                MyBase.AutoSize = value
                If value Then
                    更新自动尺寸()
                Else
                    If 自动尺寸前的大小 <> Size.Empty Then
                        Me.Size = 自动尺寸前的大小
                    End If
                End If
                Me.Invalidate()
            End If
        End Set
    End Property

    Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
        If Not 启用自动尺寸 Then Return Me.Size
        Dim s As Single = DpiScale()
        Dim 框尺寸 As Single = 操作框尺寸 * s
        Dim 间距 As Single = 框文本间距 * s
        Dim 边框额外 As Single = 框边框宽度 * s
        Dim 文本格式 As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
        Dim 主文本尺寸 As Size = TextRenderer.MeasureText(If(String.IsNullOrEmpty(MyBase.Text), "A", MyBase.Text), Me.Font, New Size(Integer.MaxValue, Integer.MaxValue), 文本格式)
        Dim 新宽度 As Integer = CInt(Me.Padding.Left + 边框额外 + 框尺寸 + 间距) + 主文本尺寸.Width + Me.Padding.Right
        Dim 新高度 As Integer
        If Not String.IsNullOrEmpty(次要文本) Then
            Using 次要文本字体 As New Font(Me.Font.FontFamily, 次要文本字号, FontStyle.Regular)
                Dim 次文本尺寸 As Size = TextRenderer.MeasureText(次要文本, 次要文本字体, New Size(Integer.MaxValue, Integer.MaxValue), 文本格式)
                Dim _主次间距 As Integer = CInt(Math.Round(主次文本间距 * s))
                Dim 文本总高度 As Integer = 主文本尺寸.Height + _主次间距 + 次文本尺寸.Height
                新高度 = Math.Max(CInt(框尺寸 + 边框额外), 文本总高度) + Me.Padding.Vertical
                新宽度 = Math.Max(新宽度, CInt(Me.Padding.Left + 边框额外 + 框尺寸 + 间距) + 次文本尺寸.Width + Me.Padding.Right)
            End Using
        Else
            新高度 = Math.Max(CInt(框尺寸 + 边框额外), 主文本尺寸.Height) + Me.Padding.Vertical
        End If
        If Me.MaximumSize.Width > 0 Then 新宽度 = Math.Min(新宽度, Me.MaximumSize.Width)
        If Me.MaximumSize.Height > 0 Then 新高度 = Math.Min(新高度, Me.MaximumSize.Height)
        If Me.MinimumSize.Width > 0 Then 新宽度 = Math.Max(新宽度, Me.MinimumSize.Width)
        If Me.MinimumSize.Height > 0 Then 新高度 = Math.Max(新高度, Me.MinimumSize.Height)
        Return New Size(新宽度, 新高度)
    End Function

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        If 启用自动尺寸 Then
            更新自动尺寸()
        Else
            自动尺寸前的大小 = Me.Size
        End If
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        更新自动尺寸()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        重置文本行高缓存()
        更新自动尺寸()
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        更新自动尺寸()
        Me.Invalidate()
    End Sub

    Private 正在更新尺寸 As Boolean = False

    Private Sub 更新自动尺寸()
        If Not 启用自动尺寸 OrElse Not IsHandleCreated OrElse 正在更新尺寸 Then Return
        正在更新尺寸 = True
        Try
            Dim preferred = GetPreferredSize(New Size(Me.Width, Me.Height))
            Select Case Dock
                Case DockStyle.Top, DockStyle.Bottom
                    If Me.Height <> preferred.Height Then Me.Height = preferred.Height
                Case DockStyle.Left, DockStyle.Right
                    If Me.Width <> preferred.Width Then Me.Width = preferred.Width
                Case DockStyle.Fill
                Case Else
                    Me.Size = preferred
            End Select
        Finally
            正在更新尺寸 = False
        End Try
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
    Public Shadows Property AutoSizeMode As AutoSizeMode
        Get
            Return Nothing
        End Get
        Set(value As AutoSizeMode)
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