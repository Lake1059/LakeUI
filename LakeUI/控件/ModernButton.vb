Imports System.ComponentModel
Imports System.Drawing.Drawing2D

<DefaultEvent("Click")>
Public Class ModernButton
#Region "绘制"
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim 是否有圆角 As Boolean = 边框圆角半径 > 0
        Dim 极限矩形区域 As New RectangleF(0, 0, Me.Width - 1, Me.Height - 1)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 / 2.0F
            极限矩形区域.Inflate(-half, -half)
        End If
        Dim 内容矩形区域 As New RectangleF(
            极限矩形区域.X + Me.Padding.Left,
            极限矩形区域.Y + Me.Padding.Top,
            极限矩形区域.Width - Me.Padding.Horizontal,
            极限矩形区域.Height - Me.Padding.Vertical)
        Dim _ssaa As Integer = If(Class1.GlobalSSAA > 1, Class1.GlobalSSAA, 超采样倍率)
        If _ssaa > 1 Then
            Using bmp As New Bitmap(Me.Width * _ssaa, Me.Height * _ssaa)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.ScaleTransform(_ssaa, _ssaa)
                    绘制图形内容(g, 是否有圆角, 极限矩形区域, 内容矩形区域)
                End Using
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                e.Graphics.DrawImage(bmp, 0, 0, Me.Width, Me.Height)
            End Using
        Else
            绘制图形内容(e.Graphics, 是否有圆角, 极限矩形区域, 内容矩形区域)
        End If
        绘制文本(e.Graphics, 内容矩形区域, 计算图标占用的水平宽度(内容矩形区域))
        If Not Enabled Then
            Using brush As New SolidBrush(Color.FromArgb(120, 0, 0, 0))
                e.Graphics.FillRectangle(brush, 0, 0, Me.Width, Me.Height)
            End Using
        End If
    End Sub
    Private Sub 绘制图形内容(g As Graphics, 是否有圆角 As Boolean, 极限矩形区域 As RectangleF, 内容矩形区域 As RectangleF)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        g.InterpolationMode = InterpolationMode.HighQualityBicubic
        Dim 背景颜色缓存值 As Color
        Dim 渐变颜色缓存值 As Color
        Dim 边框颜色缓存值 As Color
        If 颜色动画已启用 Then
            Dim 目标背景 As Color = Nothing, 目标渐变 As Color = Nothing, 目标边框 As Color = Nothing
            根据鼠标状态分配颜色(目标背景, 目标渐变, 目标边框)
            Dim t As Single = 动画助手.Progress
            背景颜色缓存值 = 颜色插值(动画前背景颜色, 目标背景, t)
            渐变颜色缓存值 = 颜色插值(动画前渐变颜色, 目标渐变, t)
            边框颜色缓存值 = 颜色插值(动画前边框颜色, 目标边框, t)
        Else
            根据鼠标状态分配颜色(背景颜色缓存值, 渐变颜色缓存值, 边框颜色缓存值)
        End If
        If 是否有圆角 Then
            Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(极限矩形区域, 边框圆角半径)
                RectangleRenderer.绘制圆角背景(g, path, 极限矩形区域, 背景颜色缓存值, 渐变颜色缓存值, 渐变方向)
                RectangleRenderer.绘制圆角边框(g, path, 边框颜色缓存值, 边框宽度)
            End Using
        Else
            RectangleRenderer.绘制矩形背景(g, 极限矩形区域, 背景颜色缓存值, 渐变颜色缓存值, 渐变方向)
            RectangleRenderer.绘制矩形边框(g, 极限矩形区域, 边框颜色缓存值, 边框宽度)
        End If
        绘制图标(g, 内容矩形区域)
    End Sub
    Private Function 计算图标占用的水平宽度(内容矩形区域 As RectangleF) As Single
        If 图标 Is Nothing Then Return 0
        Return Math.Min(内容矩形区域.Height - 图标边距 * 2, 内容矩形区域.Width * 0.3F)
    End Function
    Private Sub 根据鼠标状态分配颜色(ByRef _背景颜色 As Color, ByRef _渐变颜色 As Color, ByRef _边框颜色 As Color)
        Select Case 鼠标状态
            Case MouseStateEnum.Hover
                _背景颜色 = If(鼠标移上时背景颜色 <> Color.Empty, 鼠标移上时背景颜色, 背景基础颜色)
                _渐变颜色 = If(鼠标移上时渐变颜色 <> Color.Empty, 鼠标移上时渐变颜色, 背景渐变颜色)
                _边框颜色 = If(鼠标移上时边框颜色 <> Color.Empty, 鼠标移上时边框颜色, 边框颜色)
            Case MouseStateEnum.Pressed
                _背景颜色 = If(鼠标按下时背景颜色 <> Color.Empty, 鼠标按下时背景颜色, 背景基础颜色)
                _渐变颜色 = If(鼠标按下时渐变颜色 <> Color.Empty, 鼠标按下时渐变颜色, 背景渐变颜色)
                _边框颜色 = If(鼠标按下时边框颜色 <> Color.Empty, 鼠标按下时边框颜色, 边框颜色)
            Case Else
                _背景颜色 = 背景基础颜色
                _渐变颜色 = 背景渐变颜色
                _边框颜色 = 边框颜色
        End Select
    End Sub
    Private Sub 切换鼠标颜色状态(新状态 As MouseStateEnum)
        Dim 当前背景 As Color = Nothing, 当前渐变 As Color = Nothing, 当前边框 As Color = Nothing
        If 颜色动画已启用 Then
            Dim 旧目标背景 As Color = Nothing, 旧目标渐变 As Color = Nothing, 旧目标边框 As Color = Nothing
            根据鼠标状态分配颜色(旧目标背景, 旧目标渐变, 旧目标边框)
            Dim t As Single = 动画助手.Progress
            当前背景 = 颜色插值(动画前背景颜色, 旧目标背景, t)
            当前渐变 = 颜色插值(动画前渐变颜色, 旧目标渐变, t)
            当前边框 = 颜色插值(动画前边框颜色, 旧目标边框, t)
        Else
            根据鼠标状态分配颜色(当前背景, 当前渐变, 当前边框)
            颜色动画已启用 = True
        End If
        动画前背景颜色 = 当前背景
        动画前渐变颜色 = 当前渐变
        动画前边框颜色 = 当前边框
        鼠标状态 = 新状态
        动画助手.SetImmediate(0)
        动画助手.AnimateTo(1)
    End Sub
    Private Shared Function 颜色插值(c1 As Color, c2 As Color, t As Single) As Color
        If c1.IsEmpty AndAlso c2.IsEmpty Then Return Color.Empty
        Return Color.FromArgb(
            字节插值(c1.A, c2.A, t),
            字节插值(c1.R, c2.R, t),
            字节插值(c1.G, c2.G, t),
            字节插值(c1.B, c2.B, t))
    End Function
    Private Shared Function 字节插值(a As Integer, b As Integer, t As Single) As Integer
        Return Math.Clamp(CInt(a + (b - a) * t), 0, 255)
    End Function

    Private Sub 绘制图标(g As Graphics, 内容矩形区域 As RectangleF)
        If 图标 Is Nothing Then Return
        Dim iconSize As Single = 计算图标占用的水平宽度(内容矩形区域)
        Dim iconX As Single = 内容矩形区域.X + 图标边距
        Dim iconY As Single = 内容矩形区域.Y + (内容矩形区域.Height - iconSize) / 2.0F
        g.DrawImage(图标, New RectangleF(iconX, iconY, iconSize, iconSize))
    End Sub
    Private Sub 绘制文本(g As Graphics, 内容矩形区域 As RectangleF, 图标宽度 As Single)
        Dim 图标占用总宽度 As Single = If(图标宽度 > 0, 图标宽度 + 图标边距, 0)
        Dim 文本绘制区域 As Rectangle = Rectangle.Round(New RectangleF(
            内容矩形区域.X + 图标占用总宽度 + 边框圆角半径,
            内容矩形区域.Y,
            内容矩形区域.Width - 图标占用总宽度 - 边框圆角半径 * 2,
            内容矩形区域.Height))
        Dim 文本格式1 As TextFormatFlags
        Select Case 文字对齐方位
            Case TextAlignEnum.Left
                文本格式1 = TextFormatFlags.Left
            Case TextAlignEnum.Right
                文本格式1 = TextFormatFlags.Right
            Case Else
                文本格式1 = TextFormatFlags.HorizontalCenter
        End Select
        Dim 文本格式2 As TextFormatFlags = 文本格式1 Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding
        If Not String.IsNullOrEmpty(次要文本) Then
            Using 次要文本字体 As New Font(Me.Font.FontFamily, 次要文本字号, FontStyle.Regular)
                Dim 主要文本尺寸 As Size = TextRenderer.MeasureText(g, MyBase.Text, Me.Font, 文本绘制区域.Size, 文本格式2)
                Dim 次要文本尺寸 As Size = TextRenderer.MeasureText(g, 次要文本, 次要文本字体, 文本绘制区域.Size, 文本格式2)
                Dim 文本极限高度 As Integer = 主要文本尺寸.Height + 主次文本间距 + 次要文本尺寸.Height
                Dim 高度起始 As Integer = 文本绘制区域.Y + (文本绘制区域.Height - 文本极限高度) \ 2
                Dim 主要文本区域 As New Rectangle(文本绘制区域.X, 高度起始, 文本绘制区域.Width, 主要文本尺寸.Height)
                TextRenderer.DrawText(g, MyBase.Text, Me.Font, 主要文本区域, 文本颜色, 文本格式2)
                Dim 次要文本区域 As New Rectangle(文本绘制区域.X, 高度起始 + 主要文本尺寸.Height + 主次文本间距, 文本绘制区域.Width, 次要文本尺寸.Height)
                TextRenderer.DrawText(g, 次要文本, 次要文本字体, 次要文本区域, 次要文本颜色, 文本格式2)
            End Using
        Else
            Dim 文本格式3 As TextFormatFlags = 文本格式2 Or TextFormatFlags.VerticalCenter
            TextRenderer.DrawText(g, MyBase.Text, Me.Font, 文本绘制区域, 文本颜色, 文本格式3)
        End If
    End Sub

#End Region

#Region "鼠标状态"
    Private Enum MouseStateEnum
        Normal
        Hover
        Pressed
    End Enum
    Private 鼠标状态 As MouseStateEnum = MouseStateEnum.Normal
    Private ReadOnly 动画助手 As New AnimationHelper(Me)
    Private 颜色动画已启用 As Boolean = False
    Private 动画前背景颜色 As Color
    Private 动画前渐变颜色 As Color
    Private 动画前边框颜色 As Color
    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        If Not Enabled Then Return
        切换鼠标颜色状态(MouseStateEnum.Hover)
    End Sub
    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If Not Enabled Then Return
        切换鼠标颜色状态(MouseStateEnum.Normal)
    End Sub
    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not Enabled Then Return
        切换鼠标颜色状态(MouseStateEnum.Pressed)
    End Sub
    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If Not Enabled Then Return
        切换鼠标颜色状态(If(ClientRectangle.Contains(e.Location), MouseStateEnum.Hover, MouseStateEnum.Normal))
    End Sub
    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        If Not Enabled Then
            鼠标状态 = MouseStateEnum.Normal
            颜色动画已启用 = False
            动画助手.StopAnimation()
        End If
        Me.Invalidate()
    End Sub
#End Region

#Region "通用"
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

#Region "边框属性"
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

#Region "背景属性"
    Private 背景基础颜色 As Color = Color.FromArgb(36, 36, 36)
    <Category("LakeUI"), Description("背景基础颜色"), DefaultValue(GetType(Color), "36,36,36"), Browsable(True)>
    Public Property BackColor1 As Color
        Get
            Return 背景基础颜色
        End Get
        Set(value As Color)
            SetValue(背景基础颜色, value)
        End Set
    End Property
    Private 背景渐变颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("背景渐变颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property BackColor2 As Color
        Get
            Return 背景渐变颜色
        End Get
        Set(value As Color)
            SetValue(背景渐变颜色, value)
        End Set
    End Property
    Private 渐变方向 As Orientation = Orientation.Vertical
    <Category("LakeUI"), Description("渐变方向"), DefaultValue(GetType(Orientation), "Vertical"), Browsable(True)>
    Public Property BackColorOrientation As Orientation
        Get
            Return 渐变方向
        End Get
        Set(value As Orientation)
            SetValue(渐变方向, value)
        End Set
    End Property
#End Region

#Region "文本属性"
    <Category("LakeUI"), Description("主要文本"), DefaultValue(GetType(String), "ExButton"), Browsable(True), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
    Public Overrides Property Text As String
        Get
            Return MyBase.Text
        End Get
        Set(value As String)
            SetValue(MyBase.Text, value)
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
    <Category("LakeUI"), Description("次要文本"), DefaultValue(GetType(String), ""), Browsable(True)>
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
    <Category("LakeUI"), Description("次要文本字号"), DefaultValue(GetType(Integer), "9"), Browsable(True)>
    Public Property SubTextSize As Integer
        Get
            Return 次要文本字号
        End Get
        Set(value As Integer)
            SetValue(次要文本字号, value)
        End Set
    End Property
    Private 主次文本间距 As Integer = 1
    <Category("LakeUI"), Description("主次文本间距"), DefaultValue(GetType(Integer), "1"), Browsable(True)>
    Public Property MainSubTextSpacing As Integer
        Get
            Return 主次文本间距
        End Get
        Set(value As Integer)
            SetValue(主次文本间距, value)
        End Set
    End Property
    Private 文字对齐方位 As TextAlignEnum = TextAlignEnum.Center
    Public Enum TextAlignEnum
        Center
        Left
        Right
    End Enum
    <Category("LakeUI"), Description("文字对齐方位"), DefaultValue(GetType(TextAlignEnum), "Center"), Browsable(True)>
    Public Property TextAlign As TextAlignEnum
        Get
            Return 文字对齐方位
        End Get
        Set(value As TextAlignEnum)
            SetValue(文字对齐方位, value)
        End Set
    End Property
#End Region

#Region "图标属性"
    Private 图标 As Image = Nothing
    <Category("LakeUI"), Description("图标"), DefaultValue(GetType(Image), ""), Browsable(True)>
    Public Property Icon As Image
        Get
            Return 图标
        End Get
        Set(value As Image)
            SetValue(图标, value)
        End Set
    End Property

    Private 图标边距 As Integer = 5
    <Category("LakeUI"), Description("图标边距"), DefaultValue(GetType(Integer), "5"), Browsable(True)>
    Public Property IconPadding As Integer
        Get
            Return 图标边距
        End Get
        Set(value As Integer)
            SetValue(图标边距, value)
        End Set
    End Property
#End Region

#Region "交互状态属性"
    Private 鼠标移上时背景颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时背景颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverBackColor1 As Color
        Get
            Return 鼠标移上时背景颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时背景颜色, value)
        End Set
    End Property
    Private 鼠标移上时渐变颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时渐变颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverBackColor2 As Color
        Get
            Return 鼠标移上时渐变颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时渐变颜色, value)
        End Set
    End Property
    Private 鼠标移上时边框颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时边框颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverBorderColor As Color
        Get
            Return 鼠标移上时边框颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时边框颜色, value)
        End Set
    End Property
    Private 鼠标按下时背景颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时背景颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedBackColor1 As Color
        Get
            Return 鼠标按下时背景颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时背景颜色, value)
        End Set
    End Property
    Private 鼠标按下时渐变颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时渐变颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedBackColor2 As Color
        Get
            Return 鼠标按下时渐变颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时渐变颜色, value)
        End Set
    End Property
    Private 鼠标按下时边框颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时边框颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedBorderColor As Color
        Get
            Return 鼠标按下时边框颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时边框颜色, value)
        End Set
    End Property
#End Region

#Region "禁用属性"
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property BackColor As Color
        Get
            Return Nothing
        End Get
        Set(value As Color)
        End Set
    End Property
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
    Public Shadows Property AutoSize As Boolean
        Get
            Return Nothing
        End Get
        Set(value As Boolean)
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