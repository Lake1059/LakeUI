Imports System.ComponentModel
Imports System.Drawing.Drawing2D

Public Class ProgressRing

    Public Sub New()
        InitializeComponent()
    End Sub

#Region "绘制"
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim _ssaa As Integer = If(Class1.GlobalSSAA > 1, Class1.GlobalSSAA, 超采样倍率)
        If _ssaa > 1 Then
            Using bmp As New Bitmap(Me.Width * _ssaa, Me.Height * _ssaa)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.ScaleTransform(_ssaa, _ssaa)
                    绘制图形内容(g)
                End Using
                e.Graphics.CompositingQuality = Class1.GlobalCompositingQuality
                e.Graphics.InterpolationMode = Class1.GlobalInterpolationMode
                e.Graphics.DrawImage(bmp, 0, 0, Me.Width, Me.Height)
            End Using
        Else
            绘制图形内容(e.Graphics)
        End If
        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            Using brush As New SolidBrush(禁用时遮罩颜色)
                e.Graphics.FillEllipse(brush, 0, 0, Me.Width - 1, Me.Height - 1)
            End Using
        End If
    End Sub

    Private Sub 绘制图形内容(g As Graphics)
        g.SmoothingMode = Class1.GlobalSmoothingMode
        g.PixelOffsetMode = Class1.GlobalPixelOffsetMode
        g.InterpolationMode = Class1.GlobalInterpolationMode

        Select Case 动画样式
            Case StyleEnum.Win11
                绘制Win11样式(g)
            Case StyleEnum.Win10
                绘制Win10样式(g)
        End Select
    End Sub

    Private Sub 绘制Win11样式(g As Graphics)
        Dim s As Single = DpiScale()
        Dim 中心X As Single = Me.Width / 2.0F
        Dim 中心Y As Single = Me.Height / 2.0F
        Dim 半径 As Single = Math.Min(中心X, 中心Y) - 1
        Dim 画笔宽度 As Single = 圆弧厚度值 * s
        If 画笔宽度 >= 半径 Then 画笔宽度 = 半径 - 1
        If 画笔宽度 < 0.5F Then Return
        Dim 绘制半径 As Single = 半径 - 画笔宽度 / 2.0F
        If 绘制半径 < 1 Then Return

        Dim 绘制矩形 As New RectangleF(中心X - 绘制半径, 中心Y - 绘制半径, 绘制半径 * 2, 绘制半径 * 2)

        Const minArc As Single = 15.0F
        Const maxArc As Single = 260.0F
        Const baseRotation As Single = 535.0F
        Const sweepRange As Single = maxArc - minArc
        ' 每周期尾部总位移 = 535 + 245 = 780°，780 mod 360 = 60°
        ' 弧线最长/最短位置每周期自然漂移 60°
        Const totalPerCycle As Single = baseRotation + sweepRange

        ' 使用连续时间计算，保证周期间无缝衔接
        Dim n As Single
        Dim t As Single
        If DesignMode Then
            n = 0 : t = 0.15F
        ElseIf Not 动画运行中 Then
            n = 0 : t = 0.0F
        Else
            Dim cycles As Double = 秒表.Elapsed.TotalMilliseconds / 动画周期时长
            ' 每 6 周期角度精确回归（6 × 780 = 13 × 360），取模避免精度损失
            Dim reduced As Double = cycles Mod 6.0
            n = CSng(Math.Floor(reduced))
            t = CSng(reduced - Math.Floor(reduced))
        End If

        Dim sweepAngle As Single
        Dim sweepOffset As Single = 0

        If t < 0.5F Then
            ' 前半段：弧线从短变长（头部快速前进，尾部缓慢跟随）
            Dim p As Single = 缓动(t * 2.0F)
            sweepAngle = minArc + sweepRange * p
        Else
            ' 后半段：弧线从长变短（尾部追上头部）
            Dim p As Single = 缓动((t - 0.5F) * 2.0F)
            sweepAngle = maxArc - sweepRange * p
            sweepOffset = maxArc - sweepAngle
        End If

        ' 从12点位置（-90°）开始，连续旋转
        Dim startAngle As Single = n * totalPerCycle + baseRotation * t + sweepOffset - 90.0F

        Using pen As New Pen(圆弧颜色, 画笔宽度)
            pen.StartCap = LineCap.Round
            pen.EndCap = LineCap.Round
            g.DrawArc(pen, 绘制矩形, startAngle, sweepAngle)
        End Using
    End Sub

    Private Sub 绘制Win10样式(g As Graphics)
        Dim s As Single = DpiScale()
        Dim 中心X As Single = Me.Width / 2.0F
        Dim 中心Y As Single = Me.Height / 2.0F
        Dim 半径 As Single = Math.Min(中心X, 中心Y) - 1
        Dim 点直径 As Single = 圆弧厚度值 * s
        If 点直径 >= 半径 Then 点直径 = 半径 - 1
        If 点直径 < 1 Then Return
        Dim 轨道半径 As Single = 半径 - 点直径 / 2.0F
        If 轨道半径 < 1 Then Return

        Dim t As Double = 获取动画进度()
        Const 点数量 As Integer = 5
        Const 相位跨度 As Double = 0.25  ' 5个点的相位覆盖范围（占周期的25%）
        Const A As Double = 0.75         ' 正弦变速幅度，<1 保证速度始终为正
        ' 正弦变速公式：angle(p) = 720p - (180A/π)·sin(4πp) - 90
        ' 12点位置慢（圆点聚拢），6点位置快（圆点散开）
        ' 5个点始终可见，总角度跨度 94°~266°，永远不会互相超越

        Using brush As New SolidBrush(圆弧颜色)
            For i As Integer = 0 To 点数量 - 1
                Dim p As Double = (t + CDbl(i) / (点数量 - 1) * 相位跨度) Mod 1.0
                Dim 角度 As Double = (720.0 * p - (180.0 * A / Math.PI) * Math.Sin(4.0 * Math.PI * p) - 90.0) * Math.PI / 180.0
                Dim 点X As Single = 中心X + CSng(Math.Cos(角度)) * 轨道半径
                Dim 点Y As Single = 中心Y + CSng(Math.Sin(角度)) * 轨道半径
                g.FillEllipse(brush, 点X - 点直径 / 2, 点Y - 点直径 / 2, 点直径, 点直径)
            Next
        End Using
    End Sub

    Private Shared Function 缓动(t As Single) As Single
        ' EaseInOut 三次方缓动
        If t < 0.5F Then
            Return 4.0F * t * t * t
        Else
            Return 1.0F - CSng(Math.Pow(-2.0 * t + 2.0, 3) / 2.0)
        End If
    End Function

    Private Function 获取动画进度() As Single
        If DesignMode Then Return 0.15F
        If Not 动画运行中 Then Return 0.0F
        Dim elapsed As Double = 秒表.Elapsed.TotalMilliseconds
        Dim t As Single = CSng((elapsed Mod 动画周期时长) / 动画周期时长)
        Return t
    End Function
#End Region

#Region "通用"
    Private ReadOnly 秒表 As New Stopwatch()
    Private ReadOnly 计时器 As New System.Windows.Forms.Timer()
    Private 动画运行中 As Boolean = False

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            Me.Invalidate()
        End If
    End Sub

    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function

    ''' <summary>开始播放动画</summary>
    Public Sub StartAnimation()
        If 动画运行中 Then Return
        If Not Me.IsHandleCreated Then Return
        动画运行中 = True
        秒表.Restart()
        计时器.Interval = Math.Max(1, CInt(1000.0 / 动画帧率值))
        AddHandler 计时器.Tick, AddressOf 动画帧回调
        计时器.Start()
    End Sub

    ''' <summary>停止播放动画</summary>
    Public Sub StopAnimation()
        If Not 动画运行中 Then Return
        动画运行中 = False
        计时器.Stop()
        RemoveHandler 计时器.Tick, AddressOf 动画帧回调
        秒表.Stop()
        Me.Invalidate()
    End Sub

    ''' <summary>当前动画是否正在播放</summary>
    <Browsable(False)>
    Public ReadOnly Property IsAnimating As Boolean
        Get
            Return 动画运行中
        End Get
    End Property

    Private Sub 动画帧回调(sender As Object, e As EventArgs)
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        If 自动启动 AndAlso Not DesignMode Then
            StartAnimation()
        End If
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        If Not Enabled Then
            StopAnimation()
        End If
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        Me.Invalidate()
    End Sub
#End Region

#Region "属性"
    Public Enum StyleEnum
        ''' <summary>Win11 样式：单段弧线旋转伸缩</summary>
        Win11
        ''' <summary>Win10 样式：多个圆点追逐旋转</summary>
        Win10
    End Enum

    Private 动画样式 As StyleEnum = StyleEnum.Win11
    <Category("LakeUI"), Description("加载动画样式"), DefaultValue(GetType(StyleEnum), "Win11"), Browsable(True)>
    Public Property AnimationStyle As StyleEnum
        Get
            Return 动画样式
        End Get
        Set(value As StyleEnum)
            SetValue(动画样式, value)
        End Set
    End Property

    Private 圆弧颜色 As Color = Color.FromArgb(0, 120, 215)
    <Category("LakeUI"), Description("圆弧/圆点颜色"), DefaultValue(GetType(Color), "0, 120, 215"), Browsable(True)>
    Public Property RingColor As Color
        Get
            Return 圆弧颜色
        End Get
        Set(value As Color)
            SetValue(圆弧颜色, value)
        End Set
    End Property

    Private 禁用时遮罩颜色 As Color = Color.FromArgb(120, 0, 0, 0)
    <Category("LakeUI"), Description("禁用（Enabled = False）时覆盖在主体区域上的遮罩颜色（受圆角裁剪，不影响圆角外的透明区域）。"), DefaultValue(GetType(Color), "120, 0, 0, 0"), Browsable(True)>
    Public Property DisabledOverlayColor As Color
        Get
            Return 禁用时遮罩颜色
        End Get
        Set(value As Color)
            SetValue(禁用时遮罩颜色, value)
        End Set
    End Property

    Private 圆弧厚度值 As Single = 3.0F
    <Category("LakeUI"), Description("圆弧厚度（Win11 样式为弧线宽度，Win10 样式为圆点直径）"), DefaultValue(3.0F), Browsable(True)>
    Public Property RingThickness As Single
        Get
            Return 圆弧厚度值
        End Get
        Set(value As Single)
            SetValue(圆弧厚度值, Math.Max(1.0F, value))
        End Set
    End Property

    Private 动画周期时长 As Integer = 1500
    <Category("LakeUI"), Description("动画正好转一圈的时长 (毫秒)"), DefaultValue(1500), Browsable(True)>
    Public Property AnimationDuration As Integer
        Get
            Return 动画周期时长
        End Get
        Set(value As Integer)
            动画周期时长 = Math.Max(100, value)
        End Set
    End Property

    Private 动画帧率值 As Integer = 60
    <Category("LakeUI"), Description(Class1.动画帧率描述词), DefaultValue(60), Browsable(True)>
    Public Property AnimationFPS As Integer
        Get
            Return 动画帧率值
        End Get
        Set(value As Integer)
            动画帧率值 = Math.Max(1, value)
            If 动画运行中 Then
                计时器.Interval = Math.Max(1, CInt(1000.0 / 动画帧率值))
            End If
        End Set
    End Property

    Private 自动启动 As Boolean = True
    <Category("LakeUI"), Description("控件创建后是否自动开始动画"), DefaultValue(True), Browsable(True)>
    Public Property AutoStart As Boolean
        Get
            Return 自动启动
        End Get
        Set(value As Boolean)
            自动启动 = value
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
