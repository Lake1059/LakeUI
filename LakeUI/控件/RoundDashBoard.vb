Imports System.ComponentModel
Imports System.Drawing.Drawing2D

<DefaultEvent("ValueChanged")>
Public Class RoundDashBoard

    Public Event ValueChanged As EventHandler

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
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                e.Graphics.DrawImage(bmp, 0, 0, Me.Width, Me.Height)
            End Using
        Else
            绘制图形内容(e.Graphics)
        End If
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
        Dim 中心X As Single = Me.Width / 2.0F
        Dim 中心Y As Single = Me.Height / 2.0F
        Dim 外径 As Single = 外圈半径
        Dim 厚度值 As Single = 圆弧厚度 * s

        ' 确保半径不超出控件范围
        Dim 最大半径 As Single = Math.Min(中心X, 中心Y) - 1
        If 外径 > 最大半径 Then 外径 = 最大半径
        If 外径 < 1 Then Return

        Dim 画笔宽度 As Single = Math.Min(厚度值, 外径)
        Dim 绘制半径 As Single = 外径 - 画笔宽度 / 2.0F

        If 绘制半径 < 1 Then Return

        Dim 绘制矩形 As New RectangleF(中心X - 绘制半径, 中心Y - 绘制半径, 绘制半径 * 2, 绘制半径 * 2)
        Dim 实际起始角 As Single = 起始角度 - 90 ' GDI+ 0度在3点钟方向，减90使0度在12点钟方向
        Dim 实际扫过角 As Single = 表盘角度

        ' 绘制轨道背景
        绘制圆弧(g, 绘制矩形, 轨道背景颜色, 画笔宽度, 实际起始角, 实际扫过角)

        ' 绘制填充弧
        Dim progress As Single = 动画助手.Progress
        If progress > 0.001F Then
            Dim 填充扫过角 As Single = 实际扫过角 * progress
            If 填充渐变颜色 <> Color.Empty Then
                绘制渐变弧(g, 绘制矩形, 画笔宽度, 实际起始角, 填充扫过角, 实际扫过角)
            Else
                绘制圆弧(g, 绘制矩形, 填充基础颜色, 画笔宽度, 实际起始角, 填充扫过角)
            End If

            If 显示指针 Then 绘制指针(g, 中心X, 中心Y, 外径, 画笔宽度, 实际起始角 + 填充扫过角, s)
        End If

        绘制中心文字(g, 中心X, 中心Y, progress)
    End Sub

    Private Shared Sub 绘制圆弧(g As Graphics, rect As RectangleF, color As Color, penWidth As Single, startAngle As Single, sweepAngle As Single)
        Using pen As New Pen(color, penWidth)
            pen.StartCap = LineCap.Round
            pen.EndCap = LineCap.Round
            If sweepAngle >= 360 Then
                g.DrawEllipse(pen, rect)
            Else
                g.DrawArc(pen, rect, startAngle, sweepAngle)
            End If
        End Using
    End Sub

    Private Sub 绘制指针(g As Graphics, 中心X As Single, 中心Y As Single, 外径 As Single, 画笔宽度 As Single, 角度 As Single, s As Single)
        Dim 指针角度弧度 As Double = 角度 * Math.PI / 180.0
        Dim cosVal As Single = CSng(Math.Cos(指针角度弧度))
        Dim sinVal As Single = CSng(Math.Sin(指针角度弧度))
        Dim 指针内半径 As Single = Math.Max(0, 外径 - 画笔宽度 - 指针长度值 * s)
        Dim 指针外半径 As Single = 外径 + 2

        Using pen As New Pen(指针颜色值, 指针宽度值 * s)
            pen.StartCap = LineCap.Round
            pen.EndCap = LineCap.Round
            g.DrawLine(pen,
                       中心X + cosVal * 指针内半径, 中心Y + sinVal * 指针内半径,
                       中心X + cosVal * 指针外半径, 中心Y + sinVal * 指针外半径)
        End Using
    End Sub

    Private Sub 绘制中心文字(g As Graphics, 中心X As Single, 中心Y As Single, progress As Single)
        If 中心文字模式 = CenterTextModeEnum.None Then Return

        Dim 文字内容 As String = 获取中心文字内容(progress)
        If String.IsNullOrEmpty(文字内容) Then Return

        Using sf As New StringFormat()
            sf.Alignment = StringAlignment.Center
            sf.LineAlignment = StringAlignment.Center
            Using brush As New SolidBrush(中心文字颜色值)
                g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit
                g.DrawString(文字内容, 中心文字字体值, brush, 中心X, 中心Y, sf)
            End Using
        End Using
    End Sub

    Private Function 获取中心文字内容(progress As Single) As String
        Select Case 中心文字模式
            Case CenterTextModeEnum.Percentage
                Return CInt(Math.Round(progress * 100)).ToString() & "%"
            Case CenterTextModeEnum.Value
                Return CInt(Math.Round(最小值 + (最大值 - 最小值) * progress)).ToString()
            Case CenterTextModeEnum.Custom
                Return 自定义文字
            Case Else
                Return ""
        End Select
    End Function
#End Region

    Private Sub 绘制渐变弧(g As Graphics, rect As RectangleF, penWidth As Single, startAngle As Single, fillSweep As Single, totalSweep As Single)
        Const 步进角度 As Single = 2.0F
        Dim 段数 As Integer = Math.Max(1, CInt(Math.Ceiling(fillSweep / 步进角度)))
        Dim 每段角度 As Single = fillSweep / 段数

        For i As Integer = 0 To 段数 - 1
            Dim 段起始角 As Single = startAngle + i * 每段角度
            ' 轻微重叠 0.5° 消除接缝，最后一段不超出
            Dim 段跨度 As Single = If(i < 段数 - 1, 每段角度 + 0.5F, 每段角度)

            Dim 段中点比例 As Single = (i + 0.5F) / 段数
            Dim 渐变比例 As Single
            If 渐变模式 = FillGradientModeEnum.FixedPosition Then
                ' 颜色位置按整条表盘计算
                Dim 弧上绝对位置 As Single = (i + 0.5F) * 每段角度
                渐变比例 = If(totalSweep > 0, 弧上绝对位置 / totalSweep, 0)
            Else
                ' 颜色完全铺满当前进度区间
                渐变比例 = 段中点比例
            End If
            渐变比例 = Math.Max(0, Math.Min(1, 渐变比例))

            Dim c As Color = 颜色插值(填充基础颜色, 填充渐变颜色, 渐变比例)
            Using pen As New Pen(c, penWidth)
                pen.StartCap = If(i = 0, LineCap.Round, LineCap.Flat)
                pen.EndCap = If(i = 段数 - 1, LineCap.Round, LineCap.Flat)
                g.DrawArc(pen, rect, 段起始角, 段跨度)
            End Using
        Next
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

#Region "通用"
    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            Me.Invalidate()
        End If
    End Sub

    Private ReadOnly 动画助手 As New AnimationHelper(Me) With {.EasingMode = AnimationHelper.EasingModeEnum.EaseInOut}

    Private Function 计算值比例(val As Integer) As Single
        Dim range As Integer = 最大值 - 最小值
        If range = 0 Then Return 0.0F
        Return (val - 最小值) / CSng(range)
    End Function

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        If Not Enabled Then 动画助手.StopAnimation()
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        Me.Invalidate()
    End Sub

    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function
#End Region

#Region "属性"
    Public Enum CenterTextModeEnum
        ''' <summary>不显示中心文字</summary>
        None
        ''' <summary>显示百分比</summary>
        Percentage
        ''' <summary>显示当前数值</summary>
        Value
        ''' <summary>显示自定义文字</summary>
        Custom
    End Enum

    Private 最小值 As Integer = 0
    <Category("LakeUI"), Description("最小值"), DefaultValue(0), Browsable(True)>
    Public Property Minimum As Integer
        Get
            Return 最小值
        End Get
        Set(value As Integer)
            If value = 最小值 Then Return
            If value > 最大值 Then value = 最大值
            最小值 = value
            If 当前值 < 最小值 Then 当前值 = 最小值
            动画助手.SetImmediate(计算值比例(当前值))
            Me.Invalidate()
        End Set
    End Property

    Private 最大值 As Integer = 100
    <Category("LakeUI"), Description("最大值"), DefaultValue(100), Browsable(True)>
    Public Property Maximum As Integer
        Get
            Return 最大值
        End Get
        Set(value As Integer)
            If value = 最大值 Then Return
            If value < 最小值 Then value = 最小值
            最大值 = value
            If 当前值 > 最大值 Then 当前值 = 最大值
            动画助手.SetImmediate(计算值比例(当前值))
            Me.Invalidate()
        End Set
    End Property

    Private 当前值 As Integer = 0
    <Category("LakeUI"), Description("当前值"), DefaultValue(0), Browsable(True)>
    Public Property Value As Integer
        Get
            Return 当前值
        End Get
        Set(value As Integer)
            Dim newVal As Integer = Math.Max(最小值, Math.Min(最大值, value))
            If newVal = 当前值 Then Return
            当前值 = newVal
            Dim ratio As Single = 计算值比例(newVal)
            动画助手.AnimateTo(ratio)
            RaiseEvent ValueChanged(Me, EventArgs.Empty)
        End Set
    End Property

    Private 外圈半径 As Single = 90
    <Category("LakeUI"), Description("外圈半径"), DefaultValue(90.0F), Browsable(True)>
    Public Property Radius As Single
        Get
            Return 外圈半径
        End Get
        Set(value As Single)
            SetValue(外圈半径, Math.Max(1, value))
        End Set
    End Property

    Private 圆弧厚度 As Single = 12
    <Category("LakeUI"), Description("圆弧向内的厚度"), DefaultValue(12.0F), Browsable(True)>
    Public Property Thickness As Single
        Get
            Return 圆弧厚度
        End Get
        Set(value As Single)
            SetValue(圆弧厚度, Math.Max(1, value))
        End Set
    End Property

    Private 起始角度 As Single = 225
    <Category("LakeUI"), Description("表盘起始角度（0 = 12 点钟方向，顺时针增加）"), DefaultValue(225.0F), Browsable(True)>
    Public Property StartAngle As Single
        Get
            Return 起始角度
        End Get
        Set(value As Single)
            SetValue(起始角度, value)
        End Set
    End Property

    Private 表盘角度 As Single = 270
    <Category("LakeUI"), Description("表盘扫过的角度范围"), DefaultValue(270.0F), Browsable(True)>
    Public Property SweepAngle As Single
        Get
            Return 表盘角度
        End Get
        Set(value As Single)
            SetValue(表盘角度, Math.Max(0, Math.Min(360, value)))
        End Set
    End Property

    Private 轨道背景颜色 As Color = Color.FromArgb(50, 50, 50)
    <Category("LakeUI"), Description("轨道背景颜色"), DefaultValue(GetType(Color), "50, 50, 50"), Browsable(True)>
    Public Property TrackColor As Color
        Get
            Return 轨道背景颜色
        End Get
        Set(value As Color)
            SetValue(轨道背景颜色, value)
        End Set
    End Property

    Private 填充基础颜色 As Color = Color.FromArgb(0, 120, 215)
    <Category("LakeUI"), Description("填充颜色"), DefaultValue(GetType(Color), "0, 120, 215"), Browsable(True)>
    Public Property FillColor As Color
        Get
            Return 填充基础颜色
        End Get
        Set(value As Color)
            SetValue(填充基础颜色, value)
        End Set
    End Property

    Private 填充渐变颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("填充渐变颜色，Empty 为纯色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property FillGradientColor As Color
        Get
            Return 填充渐变颜色
        End Get
        Set(value As Color)
            SetValue(填充渐变颜色, value)
        End Set
    End Property

    Public Enum FillGradientModeEnum
        ''' <summary>渐变按表盘百分比位置分布，进度只显示到当前位置为止的颜色</summary>
        FixedPosition
        ''' <summary>渐变完全铺满在当前进度范围内</summary>
        WithinProgress
    End Enum

    Private 渐变模式 As FillGradientModeEnum = FillGradientModeEnum.FixedPosition
    <Category("LakeUI"), Description("渐变模式：FixedPosition = 颜色按表盘绝对位置分布；WithinProgress = 颜色始终铺满已填充区域"), DefaultValue(GetType(FillGradientModeEnum), "FixedPosition"), Browsable(True)>
    Public Property FillGradientMode As FillGradientModeEnum
        Get
            Return 渐变模式
        End Get
        Set(value As FillGradientModeEnum)
            SetValue(渐变模式, value)
        End Set
    End Property

    Private 中心文字模式 As CenterTextModeEnum = CenterTextModeEnum.Percentage
    <Category("LakeUI"), Description("中心文字显示模式"), DefaultValue(GetType(CenterTextModeEnum), "Percentage"), Browsable(True)>
    Public Property CenterTextMode As CenterTextModeEnum
        Get
            Return 中心文字模式
        End Get
        Set(value As CenterTextModeEnum)
            SetValue(中心文字模式, value)
        End Set
    End Property

    Private 自定义文字 As String = ""
    <Category("LakeUI"), Description("CenterTextMode 为 Custom 时显示的文字"), DefaultValue(""), Browsable(True)>
    Public Property CenterText As String
        Get
            Return 自定义文字
        End Get
        Set(value As String)
            SetValue(自定义文字, value)
        End Set
    End Property

    Private 中心文字颜色值 As Color = Color.White
    <Category("LakeUI"), Description("中心文字颜色"), DefaultValue(GetType(Color), "White"), Browsable(True)>
    Public Property CenterTextColor As Color
        Get
            Return 中心文字颜色值
        End Get
        Set(value As Color)
            SetValue(中心文字颜色值, value)
        End Set
    End Property

    Private Shared ReadOnly 默认中心文字字体 As New Font("Segoe UI", 14, FontStyle.Bold)
    Private 中心文字字体值 As Font = 默认中心文字字体
    <Category("LakeUI"), Description("中心文字字体"), Browsable(True)>
    Public Property CenterTextFont As Font
        Get
            Return 中心文字字体值
        End Get
        Set(value As Font)
            If value IsNot Nothing Then
                中心文字字体值 = value
                Me.Invalidate()
            End If
        End Set
    End Property

    Private Function ShouldSerializeCenterTextFont() As Boolean
        Return Not 中心文字字体值.Equals(默认中心文字字体)
    End Function

    Private Sub ResetCenterTextFont()
        CenterTextFont = 默认中心文字字体
    End Sub

    Private 显示指针 As Boolean = False
    <Category("LakeUI"), Description("是否绘制指针"), DefaultValue(False), Browsable(True)>
    Public Property ShowPointer As Boolean
        Get
            Return 显示指针
        End Get
        Set(value As Boolean)
            SetValue(显示指针, value)
        End Set
    End Property

    Private 指针颜色值 As Color = Color.White
    <Category("LakeUI"), Description("指针颜色"), DefaultValue(GetType(Color), "White"), Browsable(True)>
    Public Property PointerColor As Color
        Get
            Return 指针颜色值
        End Get
        Set(value As Color)
            SetValue(指针颜色值, value)
        End Set
    End Property

    Private 指针宽度值 As Single = 2
    <Category("LakeUI"), Description("指针宽度"), DefaultValue(2.0F), Browsable(True)>
    Public Property PointerWidth As Single
        Get
            Return 指针宽度值
        End Get
        Set(value As Single)
            SetValue(指针宽度值, Math.Max(1, value))
        End Set
    End Property

    Private 指针长度值 As Single = 20
    <Category("LakeUI"), Description("指针长度"), DefaultValue(20.0F), Browsable(True)>
    Public Property PointerLength As Single
        Get
            Return 指针长度值
        End Get
        Set(value As Single)
            SetValue(指针长度值, Math.Max(1, value))
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
