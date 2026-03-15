Imports System.ComponentModel
Imports System.Drawing.Drawing2D

<DefaultEvent("ValueChanged")>
Public Class ExcellentProgressBar

    Public Event ValueChanged As EventHandler

    Public Sub New()
        InitializeComponent()
    End Sub

#Region "绘制"
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim s As Single = DpiScale()
        Dim p As Padding = Me.Padding
        Dim 极限矩形区域 As New RectangleF(0, 0, Me.Width - 1, Me.Height - 1)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * s / 2.0F
            极限矩形区域.Inflate(-half, -half)
        End If
        Dim 内容区域 As New RectangleF(
            极限矩形区域.X + p.Left,
            极限矩形区域.Y + p.Top,
            极限矩形区域.Width - p.Horizontal,
            极限矩形区域.Height - p.Vertical)
        Dim _ssaa As Integer = If(Class1.GlobalSSAA > 1, Class1.GlobalSSAA, 超采样倍率)
        If _ssaa > 1 Then
            Using bmp As New Bitmap(Me.Width * _ssaa, Me.Height * _ssaa)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.ScaleTransform(_ssaa, _ssaa)
                    绘制图形内容(g, 极限矩形区域, 内容区域)
                End Using
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                e.Graphics.DrawImage(bmp, 0, 0, Me.Width, Me.Height)
            End Using
        Else
            绘制图形内容(e.Graphics, 极限矩形区域, 内容区域)
        End If
        If Not Enabled Then
            Using brush As New SolidBrush(Color.FromArgb(120, 0, 0, 0))
                e.Graphics.FillRectangle(brush, 0, 0, Me.Width, Me.Height)
            End Using
        End If
    End Sub

    Private Sub 绘制图形内容(g As Graphics, 极限矩形区域 As RectangleF, 内容区域 As RectangleF)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        g.InterpolationMode = InterpolationMode.HighQualityBicubic

        Dim s As Single = DpiScale()
        Dim _边框圆角半径 As Single = 边框圆角半径 * s
        Dim _边框宽度 As Single = 边框宽度 * s
        Dim 是否有圆角 As Boolean = 边框圆角半径 > 0

        If 是否有圆角 Then
            Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(极限矩形区域, _边框圆角半径)
                RectangleRenderer.绘制圆角背景(g, path, 极限矩形区域, 轨道背景颜色, 轨道渐变颜色, 轨道渐变方向)
                绘制双填充区域(g, 内容区域, path)
                RectangleRenderer.绘制圆角边框(g, path, 边框颜色, _边框宽度)
            End Using
        Else
            RectangleRenderer.绘制矩形背景(g, 极限矩形区域, 轨道背景颜色, 轨道渐变颜色, 轨道渐变方向)
            绘制双填充区域(g, 内容区域, Nothing)
            RectangleRenderer.绘制矩形边框(g, 极限矩形区域, 边框颜色, _边框宽度)
        End If
    End Sub

    Private Sub 绘制双填充区域(g As Graphics, 极限矩形区域 As RectangleF, clipPath As GraphicsPath)
        Dim progress1 As Single = 动画助手.Progress
        Dim progress2 As Single = 动画助手2.Progress

        ' 先绘制较大的进度（在底层），再绘制较小的进度（在上层）
        If progress1 >= progress2 Then
            绘制单个填充区域(g, 极限矩形区域, clipPath, progress1, 填充基础颜色, 填充渐变颜色, 填充渐变方向, 渐变模式)
            绘制单个填充区域(g, 极限矩形区域, clipPath, progress2, 填充基础颜色2, 填充渐变颜色2, 填充渐变方向2, 渐变模式2)
        Else
            绘制单个填充区域(g, 极限矩形区域, clipPath, progress2, 填充基础颜色2, 填充渐变颜色2, 填充渐变方向2, 渐变模式2)
            绘制单个填充区域(g, 极限矩形区域, clipPath, progress1, 填充基础颜色, 填充渐变颜色, 填充渐变方向, 渐变模式)
        End If
    End Sub

    Private Sub 绘制单个填充区域(g As Graphics, 极限矩形区域 As RectangleF, clipPath As GraphicsPath,
                               progress As Single, baseColor As Color, gradColor As Color, gradDir As Orientation,
                               gradMode As FillGradientModeEnum)
        If progress < 0.001F Then Return

        Dim 填充区域 As RectangleF
        If 方向 = BarOrientationEnum.Horizontal Then
            Dim fillWidth As Single = 极限矩形区域.Width * progress
            填充区域 = New RectangleF(极限矩形区域.X, 极限矩形区域.Y, fillWidth, 极限矩形区域.Height)
        Else
            Dim fillHeight As Single = 极限矩形区域.Height * progress
            填充区域 = New RectangleF(极限矩形区域.X, 极限矩形区域.Bottom - fillHeight, 极限矩形区域.Width, fillHeight)
        End If

        If 填充区域.Width < 1 OrElse 填充区域.Height < 1 Then Return

        Dim 渐变参考区域 As RectangleF = If(gradMode = FillGradientModeEnum.WithinProgress, 填充区域, 极限矩形区域)

        If clipPath IsNot Nothing Then
            ' 使用矩形裁剪 + FillPath 代替圆角路径裁剪 + FillRectangle，避免圆角锯齿
            Dim state = g.Save()
            g.SetClip(填充区域)
            绘制填充内容(g, 填充区域, 渐变参考区域, baseColor, gradColor, gradDir, clipPath)
            g.Restore(state)
        Else
            绘制填充内容(g, 填充区域, 渐变参考区域, baseColor, gradColor, gradDir)
        End If
    End Sub

    Private Sub 绘制填充内容(g As Graphics, 填充区域 As RectangleF, 渐变参考区域 As RectangleF,
                           baseColor As Color, gradColor As Color, gradDir As Orientation,
                           Optional fillPath As GraphicsPath = Nothing)
        If gradColor <> Color.Empty AndAlso 渐变参考区域.Width > 0 AndAlso 渐变参考区域.Height > 0 Then
            Dim angle As Single = If(gradDir = BarOrientationEnum.Vertical, 90.0F, 0.0F)
            Using brush As New LinearGradientBrush(渐变参考区域, baseColor, gradColor, angle)
                If fillPath IsNot Nothing Then
                    g.FillPath(brush, fillPath)
                Else
                    g.FillRectangle(brush, 填充区域)
                End If
            End Using
        Else
            Using brush As New SolidBrush(baseColor)
                If fillPath IsNot Nothing Then
                    g.FillPath(brush, fillPath)
                Else
                    g.FillRectangle(brush, 填充区域)
                End If
            End Using
        End If
    End Sub
#End Region

#Region "通用"
    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            Me.Invalidate()
        End If
    End Sub

    Private ReadOnly 动画助手 As New AnimationHelper(Me) With {.EasingMode = AnimationHelper.EasingModeEnum.EaseInOut}
    Private ReadOnly 动画助手2 As New AnimationHelper(Me) With {.EasingMode = AnimationHelper.EasingModeEnum.EaseInOut}

    Private Function 计算值比例(val As Integer) As Single
        Dim range As Integer = 最大值 - 最小值
        If range = 0 Then Return 0.0F
        Return (val - 最小值) / CSng(range)
    End Function

    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        If Not Enabled Then
            动画助手.StopAnimation()
            动画助手2.StopAnimation()
        End If
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        Me.Invalidate()
    End Sub
#End Region

#Region "属性"
    Public Enum BarOrientationEnum
        Horizontal
        Vertical
    End Enum

    Public Enum FillGradientModeEnum
        ''' <summary>渐变按轨道百分比位置分布，进度只显示到当前位置为止的颜色</summary>
        FixedPosition
        ''' <summary>渐变完全铺满在当前进度范围内</summary>
        WithinProgress
    End Enum

    Private 方向 As BarOrientationEnum = BarOrientationEnum.Horizontal
    <Category("LakeUI"), Description("进度条方向"), DefaultValue(GetType(BarOrientationEnum), "Horizontal"), Browsable(True)>
    Public Property Orientation As BarOrientationEnum
        Get
            Return 方向
        End Get
        Set(value As BarOrientationEnum)
            SetValue(方向, value)
        End Set
    End Property

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
            If 当前值2 < 最小值 Then 当前值2 = 最小值
            动画助手.SetImmediate(计算值比例(当前值))
            动画助手2.SetImmediate(计算值比例(当前值2))
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
            If 当前值2 > 最大值 Then 当前值2 = 最大值
            动画助手.SetImmediate(计算值比例(当前值))
            动画助手2.SetImmediate(计算值比例(当前值2))
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

    Private 当前值2 As Integer = 0
    <Category("LakeUI"), Description("第二进度当前值，用于对比两个值"), DefaultValue(0), Browsable(True)>
    Public Property Value2 As Integer
        Get
            Return 当前值2
        End Get
        Set(value As Integer)
            Dim newVal As Integer = Math.Max(最小值, Math.Min(最大值, value))
            If newVal = 当前值2 Then Return
            当前值2 = newVal
            Dim ratio As Single = 计算值比例(newVal)
            动画助手2.AnimateTo(ratio)
            RaiseEvent ValueChanged(Me, EventArgs.Empty)
        End Set
    End Property

    Private 轨道背景颜色 As Color
    <Category("LakeUI"), Description("轨道背景颜色"), DefaultValue(GetType(Color), "50, 50, 50"), Browsable(True)>
    Public Property TrackColor As Color
        Get
            Return 轨道背景颜色
        End Get
        Set(value As Color)
            SetValue(轨道背景颜色, value)
        End Set
    End Property

    Private 轨道渐变颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("轨道渐变颜色，Empty 为纯色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property TrackGradientColor As Color
        Get
            Return 轨道渐变颜色
        End Get
        Set(value As Color)
            SetValue(轨道渐变颜色, value)
        End Set
    End Property

    Private 轨道渐变方向 As Orientation = BarOrientationEnum.Horizontal
    <Category("LakeUI"), Description("轨道渐变方向"), DefaultValue(GetType(Orientation), "Horizontal"), Browsable(True)>
    Public Property TrackGradientOrientation As Orientation
        Get
            Return 轨道渐变方向
        End Get
        Set(value As Orientation)
            SetValue(轨道渐变方向, value)
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

    Private 填充渐变方向 As Orientation = BarOrientationEnum.Horizontal
    <Category("LakeUI"), Description("填充渐变方向"), DefaultValue(GetType(Orientation), "Horizontal"), Browsable(True)>
    Public Property FillGradientOrientation As Orientation
        Get
            Return 填充渐变方向
        End Get
        Set(value As Orientation)
            SetValue(填充渐变方向, value)
        End Set
    End Property

    Private 渐变模式 As FillGradientModeEnum = FillGradientModeEnum.FixedPosition
    <Category("LakeUI"), Description("渐变模式：FixedPosition = 颜色按轨道绝对位置分布；WithinProgress = 颜色始终铺满已填充区域"), DefaultValue(GetType(FillGradientModeEnum), "FixedPosition"), Browsable(True)>
    Public Property FillGradientMode As FillGradientModeEnum
        Get
            Return 渐变模式
        End Get
        Set(value As FillGradientModeEnum)
            SetValue(渐变模式, value)
        End Set
    End Property

    Private 填充基础颜色2 As Color = Color.FromArgb(0, 200, 83)
    <Category("LakeUI"), Description("第二进度填充颜色"), DefaultValue(GetType(Color), "0, 200, 83"), Browsable(True)>
    Public Property FillColor2 As Color
        Get
            Return 填充基础颜色2
        End Get
        Set(value As Color)
            SetValue(填充基础颜色2, value)
        End Set
    End Property

    Private 填充渐变颜色2 As Color = Color.Empty
    <Category("LakeUI"), Description("第二进度填充渐变颜色，Empty 为纯色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property FillGradientColor2 As Color
        Get
            Return 填充渐变颜色2
        End Get
        Set(value As Color)
            SetValue(填充渐变颜色2, value)
        End Set
    End Property

    Private 填充渐变方向2 As Orientation = BarOrientationEnum.Horizontal
    <Category("LakeUI"), Description("第二进度填充渐变方向"), DefaultValue(GetType(Orientation), "Horizontal"), Browsable(True)>
    Public Property FillGradientOrientation2 As Orientation
        Get
            Return 填充渐变方向2
        End Get
        Set(value As Orientation)
            SetValue(填充渐变方向2, value)
        End Set
    End Property

    Private 渐变模式2 As FillGradientModeEnum = FillGradientModeEnum.FixedPosition
    <Category("LakeUI"), Description("第二进度渐变模式：FixedPosition = 颜色按轨道绝对位置分布；WithinProgress = 颜色始终铺满已填充区域"), DefaultValue(GetType(FillGradientModeEnum), "FixedPosition"), Browsable(True)>
    Public Property FillGradientMode2 As FillGradientModeEnum
        Get
            Return 渐变模式2
        End Get
        Set(value As FillGradientModeEnum)
            SetValue(渐变模式2, value)
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

    Private 边框圆角半径 As Integer = 0
    <Category("LakeUI"), Description("边框圆角半径"), DefaultValue(0), Browsable(True)>
    Public Property BorderRadius As Integer
        Get
            Return 边框圆角半径
        End Get
        Set(value As Integer)
            SetValue(边框圆角半径, value)
        End Set
    End Property

    <Category("LakeUI"), Description(Class1.动画时长描述词), DefaultValue(300), Browsable(True)>
    Public Property AnimationDuration As Integer
        Get
            Return 动画助手.Duration
        End Get
        Set(value As Integer)
            动画助手.Duration = Math.Max(0, value)
            动画助手2.Duration = Math.Max(0, value)
        End Set
    End Property

    <Category("LakeUI"), Description(Class1.动画帧率描述词), DefaultValue(60), Browsable(True)>
    Public Property AnimationFPS As Integer
        Get
            Return 动画助手.FPS
        End Get
        Set(value As Integer)
            动画助手.FPS = Math.Max(0, value)
            动画助手2.FPS = Math.Max(0, value)
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
