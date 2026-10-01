Imports System.ComponentModel
Imports System.Numerics
Imports Vortice.Direct2D1

Public Class ProgressRing
    Implements D3D_IGpuRenderable, D3D_IGpuInvalidationSource, D3D_ISuperSamplingSource, D3D_IBackgroundSourceProvider, D3D_IGpuDirtyRegionCoverage, V5_IGpuPresentationSource

    Public Sub New()
        InitializeComponent()
        动画调度器 = New D3D_AnimationHelper(Me) With {.FPS = 动画帧率值}
        动画调度器.SetDirtyRectProvider(Function() New Rectangle(Point.Empty, Me.Size))
    End Sub

#Region "背景源"
    Private _backgroundSource As Control = Nothing
    ''' <summary>
    ''' 背景采样源。GPU 渲染保留该关系，具体背景图由窗口级合成器统一调度。
    ''' </summary>
    <Category("LakeUI"),
     Description("背景采样源。设置后记录关联源控件；GPU 渲染由窗口合成器统一调度。"),
     DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource IsNot value Then
                _backgroundSource = D3D_BackgroundPenetration.SetBackgroundSource(Me, _backgroundSource, value)
                请求GPU渲染()
            End If
        End Set
    End Property

    Public Function TryGetBackgroundSource(ByRef source As Control) As Boolean Implements D3D_IBackgroundSourceProvider.TryGetBackgroundSource
        source = _backgroundSource
        Return source IsNot Nothing
    End Function

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub
#End Region

#Region "绘制"
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements D3D_IGpuRenderable.RenderGpu
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

    Public Function GetRenderBounds() As Rectangle Implements D3D_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Public Function CoversDirtyRegion(dirtyRegion As Rectangle) As Boolean Implements D3D_IGpuDirtyRegionCoverage.CoversDirtyRegion
        Return dirtyRegion.Width > 0 AndAlso dirtyRegion.Height > 0
    End Function

    Private Sub 绘制Win11样式_GPU(context As D3D_PaintContext)
        Dim s As Single = DpiScale()
        Dim 绘制区域 As RectangleF = 获取内容绘制区域()
        ProgressRingRenderer.RenderWin11(context, 绘制区域, 圆弧颜色, 圆弧厚度值 * s,
                                       动画运行中, 秒表.Elapsed.TotalMilliseconds,
                                       DesignMode, 动画周期时长)
    End Sub

    Private Sub 绘制Win10样式_GPU(context As D3D_PaintContext)
        Dim s As Single = DpiScale()
        Dim 绘制区域 As RectangleF = 获取内容绘制区域()
        ProgressRingRenderer.RenderWin10(context, 绘制区域, 圆弧颜色, 圆弧厚度值 * s,
                                              动画运行中, 秒表.Elapsed.TotalMilliseconds,
                                              DesignMode, 动画周期时长)
    End Sub

    Private Function 获取内容绘制区域() As RectangleF
        Return New RectangleF(
            Padding.Left,
            Padding.Top,
            Math.Max(0.0F, CSng(Me.Width - Padding.Horizontal)),
            Math.Max(0.0F, CSng(Me.Height - Padding.Vertical)))
    End Function

#End Region

#Region "通用"
    Private ReadOnly 秒表 As New Stopwatch()
    Private ReadOnly 动画调度器 As D3D_AnimationHelper
    Private 动画运行中 As Boolean = False

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            请求GPU渲染()
        End If
    End Sub

    Private Function DpiScale() As Single
        Return D3D_DpiContext.FromControl(Me).Scale
    End Function

    Private Sub 请求GPU渲染(Optional immediate As Boolean = False)
        请求GPU渲染(New Rectangle(Point.Empty, Me.Size), immediate)
    End Sub

    Private Sub 请求GPU渲染(dirtyRect As Rectangle, Optional immediate As Boolean = False)
        If Me.IsDisposed Then Return
        D3D_InvalidationRouter.RequestRender(Me, dirtyRect)
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
        请求GPU渲染()
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
        ' 动画助手在回调完成后会根据 DirtyProvider 自动提交当前帧。
        ' 此处不能再次调用 RequestRender，否则会为同一帧创建两个 V5 请求。
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
        请求GPU渲染()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        请求GPU渲染()
    End Sub

    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        请求GPU渲染()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        请求GPU渲染()
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
            ' 与其他动画控件保持一致：0 表示不限制帧率。
            动画帧率值 = Math.Max(0, value)
            If 动画运行中 Then 更新动画计时器状态()
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
    Public Property SuperSamplingScale As GlobalOptions.SuperSamplingScaleEnum Implements D3D_ISuperSamplingSource.SuperSamplingScale
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
