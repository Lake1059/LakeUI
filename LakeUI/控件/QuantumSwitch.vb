Imports System.ComponentModel
Imports System.Drawing.Drawing2D

<DefaultEvent("StateChanged")>
Public Class QuantumSwitch

    Public Event StateChanged As EventHandler

#Region "状态枚举"
    Public Enum QuantumStateEnum
        ''' <summary>关闭</summary>
        Off = 0
        ''' <summary>开启</summary>
        [On] = 1
        ''' <summary>叠加态：即是开又是关</summary>
        Superposition = 2
        ''' <summary>不确定态：未被观测时的状态</summary>
        Indeterminate = 3
    End Enum
#End Region

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

        Dim 是未观测状态 As Boolean = 观测者模式 AndAlso 鼠标状态 = MouseStateEnum.Normal

        If 是未观测状态 Then
            ' 未被观测：绘制不确定态外观
            绘制不确定态(g, 极限矩形区域)
        Else
            ' 被观测：绘制正常状态
            绘制正常态(g, 极限矩形区域)
        End If
    End Sub

    Private Sub 绘制正常态(g As Graphics, 极限矩形区域 As RectangleF)
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
        Dim 滑块X As Single = 滑块最小X + (滑块最大X - 滑块最小X) * 动画进度
        Dim 滑块Y As Single = 极限矩形区域.Y + 滑块边距值
        Using brush As New SolidBrush(滑块颜色)
            g.FillEllipse(brush, 滑块X, 滑块Y, 滑块直径, 滑块直径)
        End Using
    End Sub

    Private Sub 绘制不确定态(g As Graphics, 极限矩形区域 As RectangleF)
        ' 绘制轨道（药丸形状）- 使用不确定态颜色
        Dim 圆角半径 As Integer = CInt(Math.Floor(极限矩形区域.Height / 2))
        Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(极限矩形区域, 圆角半径)
            Using brush As New SolidBrush(不确定态轨道颜色值)
                g.FillPath(brush, path)
            End Using
            RectangleRenderer.绘制圆角边框(g, path, 获取当前边框颜色(), 边框宽度)
        End Using

        ' 绘制滑块固定在中间位置
        Dim 滑块直径 As Single = 极限矩形区域.Height - 滑块边距值 * 2
        Dim 滑块最小X As Single = 极限矩形区域.X + 滑块边距值
        Dim 滑块最大X As Single = 极限矩形区域.Right - 滑块边距值 - 滑块直径
        Dim 滑块X As Single = 滑块最小X + (滑块最大X - 滑块最小X) * 0.5F
        Dim 滑块Y As Single = 极限矩形区域.Y + 滑块边距值
        Using brush As New SolidBrush(不确定态滑块颜色值)
            g.FillEllipse(brush, 滑块X, 滑块Y, 滑块直径, 滑块直径)
        End Using

        ' 绘制问号符号
        Dim 字体大小 As Single = 滑块直径 * 0.55F
        Using f As New Font("Segoe UI", 字体大小, FontStyle.Bold, GraphicsUnit.Pixel)
            Dim 文字 As String = "?"
            Dim 文字尺寸 As SizeF = g.MeasureString(文字, f)
            Dim tx As Single = 滑块X + (滑块直径 - 文字尺寸.Width) / 2.0F
            Dim ty As Single = 滑块Y + (滑块直径 - 文字尺寸.Height) / 2.0F
            g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit
            Using brush As New SolidBrush(不确定态轨道颜色值)
                g.DrawString(文字, f, brush, tx, ty)
            End Using
        End Using
    End Sub

    Private Function 获取当前轨道颜色() As Color
        Dim 目标进度 As Single = 动画进度

        ' 叠加态使用专用颜色
        If 内部状态 = QuantumStateEnum.Superposition Then
            Dim baseColor As Color = 叠加态轨道颜色值
            Select Case 鼠标状态
                Case MouseStateEnum.Hover
                    If 鼠标移上时叠加态轨道颜色值 <> Color.Empty Then baseColor = 鼠标移上时叠加态轨道颜色值
                Case MouseStateEnum.Pressed
                    If 鼠标按下时叠加态轨道颜色值 <> Color.Empty Then baseColor = 鼠标按下时叠加态轨道颜色值
            End Select
            ' 从当前颜色插值到叠加态颜色
            Dim offColor As Color = 获取状态轨道颜色(False)
            Dim onColor As Color = 获取状态轨道颜色(True)
            Dim 当前颜色 As Color = 颜色插值(offColor, onColor, 目标进度)
            ' 当进度接近0.5时就是叠加态颜色
            If Math.Abs(目标进度 - 0.5F) < 0.01F Then Return baseColor
            Return 颜色插值(当前颜色, baseColor, Math.Min(1.0F, Math.Abs(目标进度 - 0.5F) * 2.0F) * -1.0F + 1.0F)
        End If

        Dim offC As Color = 获取状态轨道颜色(False)
        Dim onC As Color = 获取状态轨道颜色(True)
        Return 颜色插值(offC, onC, 目标进度)
    End Function

    Private Function 获取状态轨道颜色(isOn As Boolean) As Color
        If isOn Then
            Select Case 鼠标状态
                Case MouseStateEnum.Hover
                    If 鼠标移上时开启轨道颜色 <> Color.Empty Then Return 鼠标移上时开启轨道颜色
                Case MouseStateEnum.Pressed
                    If 鼠标按下时开启轨道颜色 <> Color.Empty Then Return 鼠标按下时开启轨道颜色
            End Select
            Return 开启时轨道颜色
        Else
            Select Case 鼠标状态
                Case MouseStateEnum.Hover
                    If 鼠标移上时关闭轨道颜色 <> Color.Empty Then Return 鼠标移上时关闭轨道颜色
                Case MouseStateEnum.Pressed
                    If 鼠标按下时关闭轨道颜色 <> Color.Empty Then Return 鼠标按下时关闭轨道颜色
            End Select
            Return 关闭时轨道颜色
        End If
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

#Region "动画"
    Private ReadOnly 动画秒表 As New Stopwatch()
    Private ReadOnly 动画计时器 As New System.Windows.Forms.Timer()
    Private 动画进度 As Single = 0.0F
    Private 动画起始进度 As Single = 0.0F
    Private 动画目标 As Single = 0.0F
    Private 动画中 As Boolean = False
    Private 使用空闲驱动 As Boolean = False

    Private Sub 更新动画帧(sender As Object, e As EventArgs)
        Dim elapsed As Double = 动画秒表.Elapsed.TotalMilliseconds
        Dim t As Single = CSng(Math.Min(elapsed / 动画时长, 1.0))
        Dim eased As Single = 1.0F - CSng(Math.Pow(1.0 - t, 3))
        动画进度 = 动画起始进度 + (动画目标 - 动画起始进度) * eased
        If t >= 1.0F Then
            动画进度 = 动画目标
            停止动画()
        End If
        Me.Invalidate()
    End Sub

    Private Sub 开始动画()
        Select Case 内部状态
            Case QuantumStateEnum.On
                动画目标 = 1.0F
            Case QuantumStateEnum.Superposition
                动画目标 = 0.5F
            Case Else
                动画目标 = 0.0F
        End Select
        If Not IsHandleCreated OrElse 动画时长 <= 0 Then
            动画进度 = 动画目标
            Me.Invalidate()
        Else
            动画起始进度 = 动画进度
            动画秒表.Restart()
            If Not 动画中 Then
                动画中 = True
                使用空闲驱动 = (动画帧率 <= 0)
                If 使用空闲驱动 Then
                    AddHandler Application.Idle, AddressOf 更新动画帧
                Else
                    动画计时器.Interval = Math.Max(1, CInt(1000.0 / 动画帧率))
                    AddHandler 动画计时器.Tick, AddressOf 更新动画帧
                    动画计时器.Start()
                End If
            End If
        End If
    End Sub

    Private Sub 停止动画()
        If 动画中 Then
            动画中 = False
            If 使用空闲驱动 Then
                RemoveHandler Application.Idle, AddressOf 更新动画帧
            Else
                动画计时器.Stop()
                RemoveHandler 动画计时器.Tick, AddressOf 更新动画帧
            End If
            动画秒表.Stop()
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

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        鼠标状态 = MouseStateEnum.Hover
        Me.Invalidate()
        If 观测者模式 Then RaiseEvent StateChanged(Me, EventArgs.Empty)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        鼠标状态 = MouseStateEnum.Normal
        Me.Invalidate()
        If 观测者模式 Then RaiseEvent StateChanged(Me, EventArgs.Empty)
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

    Private Shared ReadOnly 随机数生成器 As New Random()

    Protected Overrides Sub OnClick(e As EventArgs)
        MyBase.OnClick(e)
        ' 左键：在 Off 和 On 之间切换
        If 内部状态 = QuantumStateEnum.Superposition Then
            ' 叠加态坍缩：随机选择开或关，模拟量子不确定性
            If 随机数生成器.Next(2) = 0 Then
                State = QuantumStateEnum.Off
            Else
                State = QuantumStateEnum.On
            End If
        ElseIf 内部状态 = QuantumStateEnum.Off Then
            State = QuantumStateEnum.On
        Else
            State = QuantumStateEnum.Off
        End If
    End Sub

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        ' 右键：切换到叠加态（如果已经是叠加态则回到关闭）
        If e.Button = MouseButtons.Right Then
            If 内部状态 = QuantumStateEnum.Superposition Then
                State = QuantumStateEnum.Off
            Else
                State = QuantumStateEnum.Superposition
            End If
        End If
    End Sub
#End Region

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            Me.Invalidate()
        End If
    End Sub

#Region "属性"
    Private 内部状态 As QuantumStateEnum = QuantumStateEnum.Off

    ''' <summary>
    ''' 获取或设置开关的内部状态（Off / On / Superposition）。
    ''' 当启用观测者模式且鼠标不在控件上时，读取到的 State 为 Indeterminate。
    ''' </summary>
    <Category("LakeUI"), Description("开关状态"), DefaultValue(GetType(QuantumStateEnum), "Off"), Browsable(True)>
    Public Property State As QuantumStateEnum
        Get
            If 观测者模式 AndAlso 鼠标状态 = MouseStateEnum.Normal Then
                Return QuantumStateEnum.Indeterminate
            End If
            Return 内部状态
        End Get
        Set(value As QuantumStateEnum)
            If value = QuantumStateEnum.Indeterminate Then value = QuantumStateEnum.Off
            If 内部状态 <> value Then
                内部状态 = value
                开始动画()
                RaiseEvent StateChanged(Me, EventArgs.Empty)
            End If
        End Set
    End Property

    Private 观测者模式 As Boolean = False
    ''' <summary>
    ''' 启用后，只有鼠标移上去时才渲染真实状态；鼠标离开后显示不确定态，State 返回 Indeterminate。
    ''' </summary>
    <Category("LakeUI"), Description("观测者模式：启用后仅在鼠标悬停时才渲染真实状态，否则显示不确定态"), DefaultValue(False), Browsable(True)>
    Public Property ObserverMode As Boolean
        Get
            Return 观测者模式
        End Get
        Set(value As Boolean)
            SetValue(观测者模式, value)
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

    Private 叠加态轨道颜色值 As Color = Color.FromArgb(160, 80, 200)
    <Category("LakeUI"), Description("叠加态轨道颜色"), DefaultValue(GetType(Color), "160, 80, 200"), Browsable(True)>
    Public Property TrackColorSuperposition As Color
        Get
            Return 叠加态轨道颜色值
        End Get
        Set(value As Color)
            SetValue(叠加态轨道颜色值, value)
        End Set
    End Property

    Private 不确定态轨道颜色值 As Color = Color.FromArgb(120, 120, 120)
    <Category("LakeUI"), Description("不确定态（未被观测）轨道颜色"), DefaultValue(GetType(Color), "120, 120, 120"), Browsable(True)>
    Public Property TrackColorIndeterminate As Color
        Get
            Return 不确定态轨道颜色值
        End Get
        Set(value As Color)
            SetValue(不确定态轨道颜色值, value)
        End Set
    End Property

    Private 不确定态滑块颜色值 As Color = Color.FromArgb(180, 180, 180)
    <Category("LakeUI"), Description("不确定态（未被观测）滑块颜色"), DefaultValue(GetType(Color), "180, 180, 180"), Browsable(True)>
    Public Property KnobColorIndeterminate As Color
        Get
            Return 不确定态滑块颜色值
        End Get
        Set(value As Color)
            SetValue(不确定态滑块颜色值, value)
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

    Private 动画时长 As Integer = 300
    <Category("LakeUI"), Description("动画时长，设为0则无动画，单位是毫秒"), DefaultValue(300), Browsable(True)>
    Public Property AnimationDuration As Integer
        Get
            Return 动画时长
        End Get
        Set(value As Integer)
            动画时长 = Math.Max(0, value)
        End Set
    End Property

    Private 动画帧率 As Integer = 60
    <Category("LakeUI"), Description("动画帧率上限，设为0则不限制"), DefaultValue(60), Browsable(True)>
    Public Property AnimationFPS As Integer
        Get
            Return 动画帧率
        End Get
        Set(value As Integer)
            动画帧率 = Math.Max(0, value)
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

    Private 鼠标移上时叠加态轨道颜色值 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时叠加态轨道颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverTrackColorSuperposition As Color
        Get
            Return 鼠标移上时叠加态轨道颜色值
        End Get
        Set(value As Color)
            SetValue(鼠标移上时叠加态轨道颜色值, value)
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

    Private 鼠标按下时叠加态轨道颜色值 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时叠加态轨道颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedTrackColorSuperposition As Color
        Get
            Return 鼠标按下时叠加态轨道颜色值
        End Get
        Set(value As Color)
            SetValue(鼠标按下时叠加态轨道颜色值, value)
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
