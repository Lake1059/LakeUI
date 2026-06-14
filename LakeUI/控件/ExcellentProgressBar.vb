Imports System.ComponentModel
Imports System.Drawing.Drawing2D
Imports Vortice.Direct2D1

<DefaultEvent("ValueChanged")>
Public Class ExcellentProgressBar

    Public Event ValueChanged As EventHandler

    Public Sub New()
        InitializeComponent()
        动画助手.DirtyProvider = AddressOf 进度动画脏区
        动画助手2.DirtyProvider = AddressOf 进度动画脏区
    End Sub

#Region "V2 背景穿透"
    Private _backgroundSource As Control = Nothing
    <Category("LakeUI"),
     Description("背景采样源（V2 透明背景穿透）。设置后将跨越任意层级直接采样此控件的绘制内容作为透明背景；为空时按 BackColor 协议处理。"),
     DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource IsNot value Then
                _backgroundSource = BackgroundPenetrationV2.SetConsumerSource(Me, _backgroundSource, value)
                OuterToInnerRefreshScheduler.RequestFull(Me)
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
        If Me.Width < 1 OrElse Me.Height < 1 Then Return

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

        Dim ssaa As Integer = D2DHelperV2.GetEffectiveSsaaScale(超采样倍率)

        ' --- 第一遍：D2D 画形状（背景/填充/边框/禁用遮罩）---
        Using scope = D2DHelperV2.BeginPaint(e, Me, ssaa)
            If scope Is Nothing Then
                MyBase.OnPaint(e)
                Return
            End If

            If _backgroundSource IsNot Nothing Then
                BackgroundPenetrationV2.PaintBackground(Me, scope, _backgroundSource)
            ElseIf MyBase.BackColor.A > 0 AndAlso MyBase.BackColor.A < 255 Then
                Dim bgLayer = scope.BackgroundLayer
                Dim bb = scope.Compositor.BrushCache.[Get](bgLayer, MyBase.BackColor)
                If bb IsNot Nothing Then
                    bgLayer.FillRectangle(D2DGlobals.ToD2DRect(New RectangleF(0, 0, Me.Width, Me.Height)), bb)
                End If
            End If

            Dim gRT As ID2D1RenderTarget = scope.GraphicsLayer
            Dim brushCache = scope.Compositor.BrushCache
            绘制图形内容_D2D(gRT, brushCache, 极限矩形区域, 内容区域)

            scope.FlushGraphics()

            If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
                Dim dcRT = scope.DCRenderTarget
                If 边框圆角半径 > 0 Then
                    Using maskGeo = RectangleRenderer.创建圆角矩形几何(极限矩形区域, 边框圆角半径 * s)
                        Dim mb = brushCache.[Get](dcRT, 禁用时遮罩颜色)
                        If mb IsNot Nothing Then dcRT.FillGeometry(maskGeo, mb)
                    End Using
                Else
                    Dim mb = brushCache.[Get](dcRT, 禁用时遮罩颜色)
                    If mb IsNot Nothing Then dcRT.FillRectangle(D2DGlobals.ToD2DRect(极限矩形区域), mb)
                End If
            End If

            ' 文字层（DirectWrite）
            If Not String.IsNullOrEmpty(Me.Text) Then
                绘制文字_D2D(scope.TextLayer, scope.Compositor.TextFormatCache, brushCache)
            End If
        End Using
    End Sub

    Private Sub 绘制文字_D2D(rt As ID2D1RenderTarget,
                              textFormatCache As D2DGlobals.TextFormatCache,
                              brushCache As D2DGlobals.SolidColorBrushCache)
        Dim p As Padding = 文字边距
        Dim textRect As New Rectangle(p.Left, p.Top, Me.Width - p.Horizontal - 1, Me.Height - p.Vertical - 1)
        If textRect.Width < 1 OrElse textRect.Height < 1 Then Return
        Dim flags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Bottom Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis
        D2DTextRenderer.DrawText(rt, Me.Text, Me.Font, textRect, Me.ForeColor, flags, DpiScale(), textFormatCache, brushCache)
    End Sub

    Private Sub 绘制图形内容_D2D(rt As ID2D1RenderTarget, brushCache As D2DGlobals.SolidColorBrushCache,
                              极限矩形区域 As RectangleF, 内容区域 As RectangleF)
        Dim s As Single = DpiScale()
        Dim _边框圆角半径 As Single = 边框圆角半径 * s
        Dim _边框宽度 As Single = 边框宽度 * s
        Dim 是否有圆角 As Boolean = 边框圆角半径 > 0

        If 是否有圆角 Then
            Using geo = RectangleRenderer.创建圆角矩形几何(极限矩形区域, _边框圆角半径)
                RectangleRenderer.绘制圆角背景_D2D(rt, geo, 极限矩形区域, 轨道背景颜色, 轨道渐变颜色, 轨道渐变方向, brushCache)
                绘制双填充区域_D2D(rt, brushCache, 内容区域, geo)
                RectangleRenderer.绘制圆角边框_D2D(rt, geo, 边框颜色, _边框宽度, brushCache)
            End Using
        Else
            RectangleRenderer.绘制矩形背景_D2D(rt, 极限矩形区域, 轨道背景颜色, 轨道渐变颜色, 轨道渐变方向, brushCache)
            绘制双填充区域_D2D(rt, brushCache, 内容区域, Nothing)
            RectangleRenderer.绘制矩形边框_D2D(rt, 极限矩形区域, 边框颜色, _边框宽度, brushCache)
        End If
    End Sub

    Private Sub 绘制双填充区域_D2D(rt As ID2D1RenderTarget, brushCache As D2DGlobals.SolidColorBrushCache,
                                极限矩形区域 As RectangleF, clipGeo As ID2D1Geometry)
        Dim progress1 As Single = 动画助手.Progress
        Dim progress2 As Single = 动画助手2.Progress

        ' 先绘制较大的进度（在底层），再绘制较小的进度（在上层）
        If progress1 >= progress2 Then
            绘制单个填充区域_D2D(rt, brushCache, 极限矩形区域, clipGeo, progress1, 填充基础颜色, 填充渐变颜色, 填充渐变方向, 渐变模式)
            绘制单个填充区域_D2D(rt, brushCache, 极限矩形区域, clipGeo, progress2, 填充基础颜色2, 填充渐变颜色2, 填充渐变方向2, 渐变模式2)
        Else
            绘制单个填充区域_D2D(rt, brushCache, 极限矩形区域, clipGeo, progress2, 填充基础颜色2, 填充渐变颜色2, 填充渐变方向2, 渐变模式2)
            绘制单个填充区域_D2D(rt, brushCache, 极限矩形区域, clipGeo, progress1, 填充基础颜色, 填充渐变颜色, 填充渐变方向, 渐变模式)
        End If
    End Sub

    Private Sub 绘制单个填充区域_D2D(rt As ID2D1RenderTarget, brushCache As D2DGlobals.SolidColorBrushCache,
                                  极限矩形区域 As RectangleF, clipGeo As ID2D1Geometry,
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

        Dim clipPushed As Boolean = False
        If clipGeo IsNot Nothing Then
            ' 用 D2D 几何裁剪到圆角轨道范围内，再在填充矩形里绘制（避免渐变与圆角不一致导致的锯齿）
            D2DGlobals.PushGeometryClip(rt, clipGeo, 极限矩形区域)
            clipPushed = True
        End If
        Try
            If gradColor <> Color.Empty AndAlso 渐变参考区域.Width > 0 AndAlso 渐变参考区域.Height > 0 Then
                Using brush = 创建填充渐变画刷_D2D(rt, 渐变参考区域, baseColor, gradColor, gradDir)
                    rt.FillRectangle(D2DGlobals.ToD2DRect(填充区域), brush)
                End Using
            Else
                Dim solid = brushCache.[Get](rt, baseColor)
                If solid IsNot Nothing Then rt.FillRectangle(D2DGlobals.ToD2DRect(填充区域), solid)
            End If
        Finally
            If clipPushed Then rt.PopLayer()
        End Try
    End Sub

    Private Shared Function 创建填充渐变画刷_D2D(rt As ID2D1RenderTarget, 区域 As RectangleF,
                                          baseColor As Color, gradColor As Color, gradDir As Orientation) As ID2D1LinearGradientBrush
        Dim startPt As System.Numerics.Vector2
        Dim endPt As System.Numerics.Vector2
        If gradDir = System.Windows.Forms.Orientation.Vertical Then
            ' 竖向：底部为主色、顶部为渐变色（GDI 中 270° 的效果）
            startPt = New System.Numerics.Vector2(区域.X, 区域.Bottom)
            endPt = New System.Numerics.Vector2(区域.X, 区域.Y)
        Else
            startPt = New System.Numerics.Vector2(区域.X, 区域.Y)
            endPt = New System.Numerics.Vector2(区域.Right, 区域.Y)
        End If
        Dim stops() As Vortice.Direct2D1.GradientStop = {
            New Vortice.Direct2D1.GradientStop With {.Position = 0.0F, .Color = D2DGlobals.ToColor4(baseColor)},
            New Vortice.Direct2D1.GradientStop With {.Position = 1.0F, .Color = D2DGlobals.ToColor4(gradColor)}}
        Dim gsc = rt.CreateGradientStopCollection(stops)
        Try
            Return rt.CreateLinearGradientBrush(New LinearGradientBrushProperties(startPt, endPt), gsc)
        Finally
            gsc.Dispose()
        End Try
    End Function
#End Region

#Region "通用"
    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            OuterToInnerRefreshScheduler.RequestFull(Me)
        End If
    End Sub

    Private ReadOnly 动画助手 As New AnimationHelperV2(Me) With {.EasingMode = AnimationHelperV2.EasingModeEnum.EaseInOut}
    Private ReadOnly 动画助手2 As New AnimationHelperV2(Me) With {.EasingMode = AnimationHelperV2.EasingModeEnum.EaseInOut}

    Private Sub 进度动画脏区(helper As AnimationHelperV2, owner As Control, sink As AnimationHelperV2.InvalidateRegionSink)
        sink.InvalidateAll()
    End Sub

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
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        OuterToInnerRefreshScheduler.RequestFull(Me)
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
            OuterToInnerRefreshScheduler.RequestFull(Me)
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
            OuterToInnerRefreshScheduler.RequestFull(Me)
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

    <Category("LakeUI"), Description(GlobalOptions.动画时长描述词), DefaultValue(300), Browsable(True)>
    Public Property AnimationDuration As Integer
        Get
            Return 动画助手.Duration
        End Get
        Set(value As Integer)
            动画助手.Duration = Math.Max(0, value)
            动画助手2.Duration = Math.Max(0, value)
        End Set
    End Property

    <Category("LakeUI"), Description(GlobalOptions.动画帧率描述词), DefaultValue(60), Browsable(True)>
    Public Property AnimationFPS As Integer
        Get
            Return 动画助手.FPS
        End Get
        Set(value As Integer)
            动画助手.FPS = Math.Max(0, value)
            动画助手2.FPS = Math.Max(0, value)
        End Set
    End Property

    Private 文字边距 As New Padding(0)
    <Category("LakeUI"), Description("文字的内边距"), Browsable(True), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
    Public Property TextPadding As Padding
        Get
            Return 文字边距
        End Get
        Set(value As Padding)
            SetValue(文字边距, value)
        End Set
    End Property

    Private 超采样倍率 As Integer = 1
    <Category("LakeUI"), Description(GlobalOptions.超采样抗锯齿描述词), DefaultValue(GetType(GlobalOptions.SuperSamplingScaleEnum), "OFF"), Browsable(True)>
    Public Property SuperSamplingScale As GlobalOptions.SuperSamplingScaleEnum
        Get
            Return 超采样倍率
        End Get
        Set(value As GlobalOptions.SuperSamplingScaleEnum)
            SetValue(超采样倍率, value)
        End Set
    End Property

    <Category("LakeUI"), Description("显示在进度条上的文字"), DefaultValue(""), Browsable(True)>
    Public Overrides Property Text As String
        Get
            Return MyBase.Text
        End Get
        Set(value As String)
            If MyBase.Text = value Then Return
            MyBase.Text = value
            OuterToInnerRefreshScheduler.RequestFull(Me)
        End Set
    End Property
#End Region

#Region "生命周期"
    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        D2DHelperV2.RefreshFontDependentRendering(Me)
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
