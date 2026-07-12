Imports System.ComponentModel
Imports System.Numerics
Imports Vortice.Direct2D1

Public Class ProgressRing
    Implements V3_IGpuRenderable, V3_IGpuInvalidationSource, V3_ISuperSamplingSource

    Public Sub New()
        InitializeComponent()
        动画调度器 = New V3_AnimationHelper(Me) With {.FPS = 动画帧率值}
        动画调度器.SetDirtyRectProvider(Function() New Rectangle(Point.Empty, Me.Size))
    End Sub

#Region "背景源"
    Private _backgroundSource As Control = Nothing
    ''' <summary>
    ''' 背景采样源。V3 渲染保留该关系，具体背景图由窗口级合成器统一调度。
    ''' </summary>
    <Category("LakeUI"),
     Description("背景采样源。设置后记录关联源控件；V3 渲染由窗口合成器统一调度。"),
     DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource IsNot value Then
                _backgroundSource = D3D_BackgroundPenetration.SetBackgroundSource(Me, _backgroundSource, value)
                请求V3渲染()
            End If
        End Set
    End Property

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub
#End Region

#Region "绘制"
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        If context Is Nothing OrElse Me.Width < 1 OrElse Me.Height < 1 Then Return

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(Me, _backgroundSource, New RectangleF(0, 0, Me.Width, Me.Height))
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), MyBase.BackColor)
        End If

        Select Case 动画样式
            Case StyleEnum.Win11
                绘制Win11样式_GPU(context)
            Case StyleEnum.Win10
                绘制Win10样式_GPU(context)
        End Select

        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, 禁用时遮罩颜色, context.DeviceGeneration)
            context.DeviceContext.FillEllipse(New Ellipse(New Vector2(Me.Width / 2.0F, Me.Height / 2.0F),
                                                          Math.Max(0, (Me.Width - 1) / 2.0F),
                                                          Math.Max(0, (Me.Height - 1) / 2.0F)),
                                             brush)
        End If
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Private Sub 绘制Win11样式_GPU(context As D3D_PaintContext)
        Dim s As Single = DpiScale()
        Dim 绘制区域 As RectangleF = 获取内容绘制区域()
        If 绘制区域.Width <= 0.0F OrElse 绘制区域.Height <= 0.0F Then Return

        Dim 中心X As Single = 绘制区域.X + 绘制区域.Width / 2.0F
        Dim 中心Y As Single = 绘制区域.Y + 绘制区域.Height / 2.0F
        Dim 半径 As Single = Math.Min(绘制区域.Width, 绘制区域.Height) / 2.0F - 1
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
        Const totalPerCycle As Single = baseRotation + sweepRange

        Dim n As Single
        Dim t As Single
        If DesignMode Then
            n = 0 : t = 0.15F
        ElseIf Not 动画运行中 Then
            n = 0 : t = 0.0F
        Else
            Dim cycles As Double = 秒表.Elapsed.TotalMilliseconds / 动画周期时长
            Dim reduced As Double = cycles Mod 6.0
            n = CSng(Math.Floor(reduced))
            t = CSng(reduced - Math.Floor(reduced))
        End If

        Dim sweepAngle As Single
        Dim sweepOffset As Single = 0
        If t < 0.5F Then
            Dim p As Single = 缓动(t * 2.0F)
            sweepAngle = minArc + sweepRange * p
        Else
            Dim p As Single = 缓动((t - 0.5F) * 2.0F)
            sweepAngle = maxArc - sweepRange * p
            sweepOffset = maxArc - sweepAngle
        End If

        Dim startAngle As Single = n * totalPerCycle + baseRotation * t + sweepOffset - 90.0F
        If sweepAngle <= 0.05F Then Return

        Using geo = 创建圆弧几何(绘制矩形, startAngle, sweepAngle)
            Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, 圆弧颜色, context.DeviceGeneration)
            Using strokeStyle = 创建圆头描边样式_GPU()
                context.DeviceContext.DrawGeometry(geo, brush, 画笔宽度, strokeStyle)
            End Using
        End Using
    End Sub

    Private Sub 绘制Win10样式_GPU(context As D3D_PaintContext)
        Dim s As Single = DpiScale()
        Dim 绘制区域 As RectangleF = 获取内容绘制区域()
        If 绘制区域.Width <= 0.0F OrElse 绘制区域.Height <= 0.0F Then Return

        Dim 中心X As Single = 绘制区域.X + 绘制区域.Width / 2.0F
        Dim 中心Y As Single = 绘制区域.Y + 绘制区域.Height / 2.0F
        Dim 半径 As Single = Math.Min(绘制区域.Width, 绘制区域.Height) / 2.0F - 1
        Dim 点直径 As Single = 圆弧厚度值 * s
        If 点直径 >= 半径 Then 点直径 = 半径 - 1
        If 点直径 < 1 Then Return
        Dim 轨道半径 As Single = 半径 - 点直径 / 2.0F
        If 轨道半径 < 1 Then Return

        Dim t As Double = 获取动画进度()
        Const 点数量 As Integer = 5
        Const 相位跨度 As Double = 0.25
        Const A As Double = 0.75

        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, 圆弧颜色, context.DeviceGeneration)
        Dim 点半径 As Single = 点直径 / 2.0F
        For i As Integer = 0 To 点数量 - 1
            Dim p As Double = (t + CDbl(i) / (点数量 - 1) * 相位跨度) Mod 1.0
            Dim 角度 As Double = (720.0 * p - (180.0 * A / Math.PI) * Math.Sin(4.0 * Math.PI * p) - 90.0) * Math.PI / 180.0
            Dim 点X As Single = 中心X + CSng(Math.Cos(角度)) * 轨道半径
            Dim 点Y As Single = 中心Y + CSng(Math.Sin(角度)) * 轨道半径
            context.DeviceContext.FillEllipse(New Ellipse(New Vector2(点X, 点Y), 点半径, 点半径), brush)
        Next
    End Sub

    Private Function 获取内容绘制区域() As RectangleF
        Return New RectangleF(
            Padding.Left,
            Padding.Top,
            Math.Max(0.0F, CSng(Me.Width - Padding.Horizontal)),
            Math.Max(0.0F, CSng(Me.Height - Padding.Vertical)))
    End Function

    Private Shared Function 创建圆弧几何(rect As RectangleF, startAngle As Single, sweepAngle As Single) As ID2D1PathGeometry
        Dim rx As Single = rect.Width / 2.0F
        Dim ry As Single = rect.Height / 2.0F
        Dim cx As Single = rect.X + rx
        Dim cy As Single = rect.Y + ry
        Dim startRad As Double = startAngle * Math.PI / 180.0
        Dim endRad As Double = (startAngle + sweepAngle) * Math.PI / 180.0
        Dim startPoint As New Vector2(cx + CSng(Math.Cos(startRad)) * rx, cy + CSng(Math.Sin(startRad)) * ry)
        Dim endPoint As New Vector2(cx + CSng(Math.Cos(endRad)) * rx, cy + CSng(Math.Sin(endRad)) * ry)

        Dim path As ID2D1PathGeometry = D3D_RenderCore.DeviceManager.D2DFactory.CreatePathGeometry()
        Dim sink As ID2D1GeometrySink = path.Open()
        Try
            sink.BeginFigure(startPoint, FigureBegin.Hollow)
            sink.AddArc(New ArcSegment With {
                .Point = endPoint,
                .Size = New Vortice.Mathematics.Size(rx, ry),
                .RotationAngle = 0,
                .SweepDirection = SweepDirection.Clockwise,
                .ArcSize = If(sweepAngle > 180.0F, ArcSize.Large, ArcSize.Small)})
            sink.EndFigure(FigureEnd.Open)
            sink.Close()
        Finally
            sink.Dispose()
        End Try
        Return path
    End Function

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
    Private ReadOnly 动画调度器 As V3_AnimationHelper
    Private 动画运行中 As Boolean = False

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            请求V3渲染()
        End If
    End Sub

    Private Function DpiScale() As Single
        Return V3_DpiContext.FromControl(Me).Scale
    End Function

    Private Sub 请求V3渲染(Optional immediate As Boolean = False)
        请求V3渲染(New Rectangle(Point.Empty, Me.Size), immediate)
    End Sub

    Private Sub 请求V3渲染(dirtyRect As Rectangle, Optional immediate As Boolean = False)
        If Me.IsDisposed Then Return
        V3_InvalidationRouter.RequestRender(Me, dirtyRect)
    End Sub

    ''' <summary>开始播放动画</summary>
    Public Sub StartAnimation()
        If Not 动画运行中 Then
            动画运行中 = True
            秒表.Restart()
        End If
        更新动画计时器状态()
    End Sub

    ''' <summary>停止播放动画</summary>
    Public Sub StopAnimation()
        If Not 动画运行中 Then Return
        动画运行中 = False
        动画调度器.StopFrameLoop()
        秒表.Stop()
        请求V3渲染()
    End Sub

    ''' <summary>当前动画是否正在播放</summary>
    <Browsable(False)>
    Public ReadOnly Property IsAnimating As Boolean
        Get
            Return 动画运行中
        End Get
    End Property

    Private Sub 动画帧回调(sender As Object, e As EventArgs)
        If Not 应运行动画计时器() Then
            更新动画计时器状态()
            Return
        End If
        请求V3渲染()
    End Sub

    Private Function 应运行动画计时器() As Boolean
        Return 动画运行中 AndAlso
               Me.IsHandleCreated AndAlso
               Not Me.IsDisposed AndAlso
               Me.Visible AndAlso
               Me.Enabled
    End Function

    Private Sub 更新动画计时器状态()
        If 应运行动画计时器() Then
            动画调度器.FPS = 动画帧率值
            动画调度器.StartFrameLoop(AddressOf 动画帧回调)
        Else
            动画调度器.StopFrameLoop()
        End If
    End Sub

    Private Shared Function 创建圆头描边样式_GPU() As ID2D1StrokeStyle
        Return D3D_RenderCore.DeviceManager.D2DFactory.CreateStrokeStyle(
            New StrokeStyleProperties With {
                .StartCap = CapStyle.Round,
                .EndCap = CapStyle.Round,
                .DashCap = CapStyle.Flat,
                .LineJoin = LineJoin.Round,
                .DashStyle = DashStyle.Solid,
                .MiterLimit = 10.0F
            })
    End Function

    Private Sub 释放描边样式()
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        If 自动启动 AndAlso Not DesignMode AndAlso Not 动画运行中 Then
            StartAnimation()
        Else
            更新动画计时器状态()
        End If
    End Sub

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        动画调度器.StopFrameLoop()
        MyBase.OnHandleDestroyed(e)
    End Sub

    Protected Overrides Sub OnVisibleChanged(e As EventArgs)
        MyBase.OnVisibleChanged(e)
        更新动画计时器状态()
    End Sub

    Protected Overrides Sub OnParentChanged(e As EventArgs)
        MyBase.OnParentChanged(e)
        更新动画计时器状态()
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        If Not Enabled Then
            StopAnimation()
        Else
            更新动画计时器状态()
        End If
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        请求V3渲染()
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
    <Category("LakeUI"), Description(GlobalOptions.动画帧率描述词), DefaultValue(60), Browsable(True)>
    Public Property AnimationFPS As Integer
        Get
            Return 动画帧率值
        End Get
        Set(value As Integer)
            动画帧率值 = Math.Max(1, value)
            If 动画运行中 Then
                动画调度器.FPS = 动画帧率值
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
    <Category("LakeUI"), Description(GlobalOptions.超采样抗锯齿描述词), DefaultValue(GetType(GlobalOptions.SuperSamplingScaleEnum), "OFF"), Browsable(True)>
    Public Property SuperSamplingScale As GlobalOptions.SuperSamplingScaleEnum Implements V3_ISuperSamplingSource.SuperSamplingScale
        Get
            Return 超采样倍率
        End Get
        Set(value As GlobalOptions.SuperSamplingScaleEnum)
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
