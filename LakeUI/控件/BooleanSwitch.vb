Imports System.ComponentModel
Imports System.Drawing.Drawing2D

<DefaultEvent("CheckedChanged")>
Public Class BooleanSwitch

    Public Event CheckedChanged As EventHandler

#Region "绘制"
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim 极限矩形区域 As New RectangleF(0, 0, Me.Width - 1, Me.Height - 1)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 / 2.0F
            极限矩形区域.Inflate(-half, -half)
        End If
        Dim _ssaa As Integer = If(Class1.GlobalSSAA > 1, Class1.GlobalSSAA, 超采样倍率)
        If _ssaa > 1 Then
            Using bmp As New Bitmap(Me.Width * _ssaa, Me.Height * _ssaa)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.ScaleTransform(_ssaa, _ssaa)
                    绘制图形内容(g, 极限矩形区域)
                End Using
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                e.Graphics.DrawImage(bmp, 0, 0, Me.Width, Me.Height)
            End Using
        Else
            绘制图形内容(e.Graphics, 极限矩形区域)
        End If
    End Sub

    Private Sub 绘制图形内容(g As Graphics, 极限矩形区域 As RectangleF)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        g.InterpolationMode = InterpolationMode.HighQualityBicubic

        Dim 轨道颜色 As Color = 获取当前轨道颜色()
        Dim 滑块颜色 As Color = 获取当前滑块颜色()
        Dim 当前边框颜色 As Color = 获取当前边框颜色()

        ' 绘制轨道（药丸形状）
        Dim 圆角半径 As Integer = CInt(Math.Floor(极限矩形区域.Height / 2))
        Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(极限矩形区域, 圆角半径)
            Using brush As New SolidBrush(轨道颜色)
                g.FillPath(brush, path)
            End Using
            RectangleRenderer.绘制圆角边框(g, path, 当前边框颜色, 边框宽度)
        End Using

        ' 绘制滑块（圆形）
        Dim 滑块直径 As Single = 极限矩形区域.Height - 滑块边距值 * 2
        Dim 滑块最小X As Single = 极限矩形区域.X + 滑块边距值
        Dim 滑块最大X As Single = 极限矩形区域.Right - 滑块边距值 - 滑块直径
        Dim 滑块X As Single = 滑块最小X + (滑块最大X - 滑块最小X) * 动画助手.Progress
        Dim 滑块Y As Single = 极限矩形区域.Y + 滑块边距值
        Using brush As New SolidBrush(滑块颜色)
            g.FillEllipse(brush, 滑块X, 滑块Y, 滑块直径, 滑块直径)
        End Using
    End Sub

    Private Function 获取当前轨道颜色() As Color
        Dim offColor As Color
        Dim onColor As Color
        Select Case 鼠标状态
            Case MouseStateEnum.Hover
                offColor = If(鼠标移上时关闭轨道颜色 <> Color.Empty, 鼠标移上时关闭轨道颜色, 关闭时轨道颜色)
                onColor = If(鼠标移上时开启轨道颜色 <> Color.Empty, 鼠标移上时开启轨道颜色, 开启时轨道颜色)
            Case MouseStateEnum.Pressed
                offColor = If(鼠标按下时关闭轨道颜色 <> Color.Empty, 鼠标按下时关闭轨道颜色, 关闭时轨道颜色)
                onColor = If(鼠标按下时开启轨道颜色 <> Color.Empty, 鼠标按下时开启轨道颜色, 开启时轨道颜色)
            Case Else
                offColor = 关闭时轨道颜色
                onColor = 开启时轨道颜色
        End Select
        Return 颜色插值(offColor, onColor, 动画助手.Progress)
    End Function

    Private Function 获取当前滑块颜色() As Color
        Select Case 鼠标状态
            Case MouseStateEnum.Hover
                If 鼠标移上时滑块颜色 <> Color.Empty Then Return 鼠标移上时滑块颜色
            Case MouseStateEnum.Pressed
                If 鼠标按下时滑块颜色 <> Color.Empty Then Return 鼠标按下时滑块颜色
        End Select
        Return 滑块基础颜色
    End Function

    Private Function 获取当前边框颜色() As Color
        Select Case 鼠标状态
            Case MouseStateEnum.Hover
                If 鼠标移上时边框颜色值 <> Color.Empty Then Return 鼠标移上时边框颜色值
            Case MouseStateEnum.Pressed
                If 鼠标按下时边框颜色值 <> Color.Empty Then Return 鼠标按下时边框颜色值
        End Select
        Return 边框颜色
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
    Private Enum MouseStateEnum
        Normal
        Hover
        Pressed
    End Enum
    Private 鼠标状态 As MouseStateEnum = MouseStateEnum.Normal
    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        鼠标状态 = MouseStateEnum.Hover
        Me.Invalidate()
    End Sub
    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        鼠标状态 = MouseStateEnum.Normal
        Me.Invalidate()
    End Sub
    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        鼠标状态 = MouseStateEnum.Pressed
        Me.Invalidate()
    End Sub
    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        鼠标状态 = If(ClientRectangle.Contains(e.Location), MouseStateEnum.Hover, MouseStateEnum.Normal)
        Me.Invalidate()
    End Sub
    Protected Overrides Sub OnClick(e As EventArgs)
        MyBase.OnClick(e)
        Checked = Not Checked
    End Sub
    Protected Overrides Sub OnDoubleClick(e As EventArgs)
        MyBase.OnDoubleClick(e)
        Checked = Not Checked
    End Sub
#End Region

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            Me.Invalidate()
        End If
    End Sub

    Private ReadOnly 动画助手 As New AnimationHelper(Me)

#Region "属性"
    Private 已选中 As Boolean = False
    <Category("LakeUI"), Description("开关状态"), DefaultValue(False), Browsable(True)>
    Public Property Checked As Boolean
        Get
            Return 已选中
        End Get
        Set(value As Boolean)
            If 已选中 <> value Then
                已选中 = value
                动画助手.AnimateTo(If(value, 1.0F, 0.0F))
                RaiseEvent CheckedChanged(Me, EventArgs.Empty)
            End If
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

    Private 开启时轨道颜色 As Color = Color.FromArgb(0, 120, 215)
    <Category("LakeUI"), Description("开启时轨道颜色"), DefaultValue(GetType(Color), "0, 120, 215"), Browsable(True)>
    Public Property TrackColorOn As Color
        Get
            Return 开启时轨道颜色
        End Get
        Set(value As Color)
            SetValue(开启时轨道颜色, value)
        End Set
    End Property

    Private 关闭时轨道颜色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("关闭时轨道颜色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
    Public Property TrackColorOff As Color
        Get
            Return 关闭时轨道颜色
        End Get
        Set(value As Color)
            SetValue(关闭时轨道颜色, value)
        End Set
    End Property

    Private 滑块基础颜色 As Color = Color.FromArgb(220, 220, 220)
    <Category("LakeUI"), Description("滑块颜色"), DefaultValue(GetType(Color), "220, 220, 220"), Browsable(True)>
    Public Property KnobColor As Color
        Get
            Return 滑块基础颜色
        End Get
        Set(value As Color)
            SetValue(滑块基础颜色, value)
        End Set
    End Property

    Private 滑块边距值 As Integer = 3
    <Category("LakeUI"), Description("滑块与轨道的边距"), DefaultValue(3), Browsable(True)>
    Public Property KnobPadding As Integer
        Get
            Return 滑块边距值
        End Get
        Set(value As Integer)
            SetValue(滑块边距值, value)
        End Set
    End Property

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

    Private 边框宽度 As Integer = 0
    <Category("LakeUI"), Description("边框宽度"), DefaultValue(0), Browsable(True)>
    Public Property BorderSize As Integer
        Get
            Return 边框宽度
        End Get
        Set(value As Integer)
            SetValue(边框宽度, value)
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

    <Category("LakeUI"), Description("动画帧率上限，设为0则不限制"), DefaultValue(60), Browsable(True)>
    Public Property AnimationFPS As Integer
        Get
            Return 动画助手.FPS
        End Get
        Set(value As Integer)
            动画助手.FPS = Math.Max(0, value)
        End Set
    End Property

    Private 鼠标移上时开启轨道颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时开启轨道颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverTrackColorOn As Color
        Get
            Return 鼠标移上时开启轨道颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时开启轨道颜色, value)
        End Set
    End Property

    Private 鼠标移上时关闭轨道颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时关闭轨道颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverTrackColorOff As Color
        Get
            Return 鼠标移上时关闭轨道颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时关闭轨道颜色, value)
        End Set
    End Property

    Private 鼠标移上时滑块颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时滑块颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverKnobColor As Color
        Get
            Return 鼠标移上时滑块颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时滑块颜色, value)
        End Set
    End Property

    Private 鼠标移上时边框颜色值 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时边框颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverBorderColor As Color
        Get
            Return 鼠标移上时边框颜色值
        End Get
        Set(value As Color)
            SetValue(鼠标移上时边框颜色值, value)
        End Set
    End Property

    Private 鼠标按下时开启轨道颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时开启轨道颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedTrackColorOn As Color
        Get
            Return 鼠标按下时开启轨道颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时开启轨道颜色, value)
        End Set
    End Property

    Private 鼠标按下时关闭轨道颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时关闭轨道颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedTrackColorOff As Color
        Get
            Return 鼠标按下时关闭轨道颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时关闭轨道颜色, value)
        End Set
    End Property

    Private 鼠标按下时滑块颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时滑块颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedKnobColor As Color
        Get
            Return 鼠标按下时滑块颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时滑块颜色, value)
        End Set
    End Property

    Private 鼠标按下时边框颜色值 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时边框颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedBorderColor As Color
        Get
            Return 鼠标按下时边框颜色值
        End Get
        Set(value As Color)
            SetValue(鼠标按下时边框颜色值, value)
        End Set
    End Property
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