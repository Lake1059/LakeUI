Imports System.ComponentModel
Imports System.Numerics
Imports Vortice.Direct2D1

<DefaultEvent("CheckedChanged")>
Public Class ModernCheckBox
    Implements V3_IGpuRenderable, V3_IGpuInvalidationSource, V3_ISuperSamplingSource

    Private _subTextFontCache As Font
    Private _subTextFontCacheKey As String

    Private Function 获取次文本字体() As Font
        Dim size As Single = Math.Max(1.0F, 次要文本字号)
        Dim key As String = Me.Font.FontFamily.Name & "|" & size.ToString(Globalization.CultureInfo.InvariantCulture)
        If _subTextFontCache IsNot Nothing AndAlso _subTextFontCacheKey = key Then Return _subTextFontCache
        释放次文本字体()
        _subTextFontCache = New Font(Me.Font.FontFamily, size, FontStyle.Regular, GraphicsUnit.Point)
        _subTextFontCacheKey = key
        Return _subTextFontCache
    End Function

    Private Sub 释放次文本字体()
        If _subTextFontCache IsNot Nothing Then
            Try : _subTextFontCache.Dispose() : Catch : End Try
            _subTextFontCache = Nothing
        End If
        _subTextFontCacheKey = Nothing
    End Sub

    Public Sub New()
        InitializeComponent()
        SetStyle(ControlStyles.StandardDoubleClick, False)
        动画助手.DirtyProvider = AddressOf 勾选动画脏区
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
    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        If context Is Nothing OrElse Me.Width <= 0 OrElse Me.Height <= 0 Then Return

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(Me, _backgroundSource, New RectangleF(0, 0, Me.Width, Me.Height))
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), MyBase.BackColor)
        End If

        绘制图形内容_GPU(context)
        绘制文本_GPU(context)
        绘制禁用遮罩_GPU(context)
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Private Sub 绘制图形内容_GPU(context As D3D_PaintContext)
        Dim s As Single = DpiScale()
        Dim 当前框边框宽度 As Single = 框边框宽度 * s
        Dim 框区域 As RectangleF = 计算框区域(s)

        Dim 当前框背景色 As Color = 获取当前框背景颜色()
        Dim 当前框边框色 As Color = 获取鼠标状态颜色(框边框颜色值, 鼠标移上时框边框颜色, 鼠标按下时框边框颜色)

        If 当前模式 = CheckModeEnum.CheckBox Then
            绘制方框_GPU(context, 框区域, 当前框背景色, 当前框边框色, 当前框边框宽度, s)
        Else
            绘制圆框_GPU(context, 框区域, 当前框背景色, 当前框边框色, 当前框边框宽度, s)
        End If
    End Sub

    Private Sub 绘制禁用遮罩_GPU(context As D3D_PaintContext)
        If Enabled OrElse 禁用时遮罩颜色.A <= 0 Then Return

        Dim s As Single = DpiScale()
        Dim 边框偏移 As Single = 框边框宽度 * s / 2.0F
        Dim 遮罩区域 As RectangleF = 计算框外缘区域(s)
        If 当前模式 = CheckModeEnum.CheckBox Then
            Dim 圆角 As Single = 框圆角半径 * s + 边框偏移
            填充圆角矩形_GPU(context, 遮罩区域, 圆角, 禁用时遮罩颜色)
        Else
            填充椭圆_GPU(context, 遮罩区域, 禁用时遮罩颜色)
        End If
    End Sub

    Private Sub 绘制方框_GPU(context As D3D_PaintContext, 框区域 As RectangleF, 背景色 As Color, 边框色 As Color, 边框宽 As Single, s As Single)
        Dim 圆角 As Single = 框圆角半径 * s
        填充圆角矩形_GPU(context, 框区域, 圆角, 背景色)
        绘制圆角边框_GPU(context, 框区域, 圆角, 边框色, 边框宽)

        Dim progress As Single = 动画助手.Progress
        If progress > 0.001F Then 绘制勾号_GPU(context, 框区域, progress, s)
    End Sub

    Private Sub 绘制勾号_GPU(context As D3D_PaintContext, 框区域 As RectangleF, progress As Single, s As Single)
        Dim 当前勾号色 As Color = 获取鼠标状态颜色(勾号颜色值, 鼠标移上时勾号颜色, 鼠标按下时勾号颜色)
        If 当前勾号色.A <= 0 Then Return

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

        Dim 可见长度 As Single = 总长度 * progress
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, 当前勾号色, context.DeviceGeneration)
        Dim strokeStyle = D3D_D2DInterop.GetRoundStrokeStyle()
        Dim strokeWidth = 勾号线宽 * s
        If 可见长度 <= 段1长 Then
            Dim t As Single = If(段1长 > 0, 可见长度 / 段1长, 0F)
            context.DeviceContext.DrawLine(
                New Vector2(x1, y1),
                New Vector2(x1 + (x2 - x1) * t, y1 + (y2 - y1) * t),
                brush, strokeWidth, strokeStyle)
        Else
            context.DeviceContext.DrawLine(New Vector2(x1, y1), New Vector2(x2, y2), brush, strokeWidth, strokeStyle)
            Dim 剩余 As Single = 可见长度 - 段1长
            Dim t As Single = If(段2长 > 0, 剩余 / 段2长, 0F)
            context.DeviceContext.DrawLine(
                New Vector2(x2, y2),
                New Vector2(x2 + (x3 - x2) * t, y2 + (y3 - y2) * t),
                brush, strokeWidth, strokeStyle)
        End If
    End Sub

    Private Sub 绘制圆框_GPU(context As D3D_PaintContext, 框区域 As RectangleF, 背景色 As Color, 边框色 As Color, 边框宽 As Single, s As Single)
        填充椭圆_GPU(context, 框区域, 背景色)
        绘制椭圆边框_GPU(context, 框区域, 边框色, 边框宽)

        Dim progress As Single = 动画助手.Progress
        If progress > 0.001F Then
            Dim 当前勾号色 As Color = 获取鼠标状态颜色(勾号颜色值, 鼠标移上时勾号颜色, 鼠标按下时勾号颜色)
            Dim 最大半径 As Single = (框区域.Width / 2.0F) - 框内边距 * s - 框边框宽度 * s / 2.0F
            If 最大半径 < 1 Then 最大半径 = 1
            Dim 当前半径 As Single = 最大半径 * progress
            Dim cx As Single = 框区域.X + 框区域.Width / 2.0F
            Dim cy As Single = 框区域.Y + 框区域.Height / 2.0F
            Dim inner As New RectangleF(cx - 当前半径, cy - 当前半径, 当前半径 * 2, 当前半径 * 2)
            填充椭圆_GPU(context, inner, 当前勾号色)
        End If
    End Sub

    Private Sub 绘制文本_GPU(context As D3D_PaintContext)
        Dim s As Single = DpiScale()
        Dim 框尺寸 As Single = 操作框尺寸 * s
        Dim 间距 As Single = 框文本间距 * s
        Dim 边框偏移 As Single = 框边框宽度 * s / 2.0F
        Dim 文本X As Single = Me.Padding.Left + 边框偏移 + 框尺寸 + 间距
        Dim 文本可用宽度 As Single = Me.Width - 文本X - Me.Padding.Right
        If 文本可用宽度 <= 0 Then Return

        Dim mainText As String = If(MyBase.Text, "")
        If String.IsNullOrEmpty(mainText) AndAlso String.IsNullOrEmpty(次要文本) Then Return

        Dim mainY As Single = 计算主文本Y(s)

        If Not String.IsNullOrEmpty(次要文本) Then
            Dim mainH As Single = 获取主文本行高()
            Dim subH As Single = 获取次文本行高()
            Dim gap As Single = 主次文本间距 * s
            context.DrawText(mainText, Me.Font, 文本颜色, New RectangleF(文本X, mainY, 文本可用宽度, mainH), Vortice.DirectWrite.TextAlignment.Leading, Vortice.DirectWrite.ParagraphAlignment.Near)
            Dim subFont = 获取次文本字体()
            If subFont IsNot Nothing Then
                context.DrawText(次要文本, subFont, 次要文本颜色, New RectangleF(文本X, mainY + mainH + gap, 文本可用宽度, subH), Vortice.DirectWrite.TextAlignment.Leading, Vortice.DirectWrite.ParagraphAlignment.Near)
            End If
        Else
            Dim mainH As Single = 获取主文本行高()
            context.DrawText(mainText, Me.Font, 文本颜色, New RectangleF(文本X, mainY, 文本可用宽度, mainH), Vortice.DirectWrite.TextAlignment.Leading, Vortice.DirectWrite.ParagraphAlignment.Near)
        End If
    End Sub

    Private Sub 填充圆角矩形_GPU(context As D3D_PaintContext, bounds As RectangleF, radius As Single, color As Color)
        If color.A <= 0 OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If radius > 0 Then
            context.FillRoundedRectangle(bounds, radius, brush)
        Else
            context.DeviceContext.FillRectangle(D3D_PaintContext.ToRawRect(bounds), brush)
        End If
    End Sub

    Private Sub 绘制圆角边框_GPU(context As D3D_PaintContext, bounds As RectangleF, radius As Single, color As Color, strokeWidth As Single)
        If color.A <= 0 OrElse strokeWidth <= 0 OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If radius > 0 Then
            context.DrawRoundedRectangle(bounds, radius, brush, strokeWidth)
        Else
            context.DeviceContext.DrawRectangle(D3D_PaintContext.ToRawRect(bounds), brush, strokeWidth)
        End If
    End Sub

    Private Sub 填充椭圆_GPU(context As D3D_PaintContext, bounds As RectangleF, color As Color)
        If color.A <= 0 OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        context.DeviceContext.FillEllipse(New Ellipse(New Vector2(bounds.X + bounds.Width / 2.0F, bounds.Y + bounds.Height / 2.0F), bounds.Width / 2.0F, bounds.Height / 2.0F), brush)
    End Sub

    Private Sub 绘制椭圆边框_GPU(context As D3D_PaintContext, bounds As RectangleF, color As Color, strokeWidth As Single)
        If color.A <= 0 OrElse strokeWidth <= 0 OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        context.DeviceContext.DrawEllipse(New Ellipse(New Vector2(bounds.X + bounds.Width / 2.0F, bounds.Y + bounds.Height / 2.0F), bounds.Width / 2.0F, bounds.Height / 2.0F), brush, strokeWidth)
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

    Private Function 计算框外缘区域(s As Single) As RectangleF
        Dim 边框偏移 As Single = 框边框宽度 * s / 2.0F
        Dim 框区域 As RectangleF = 计算框区域(s)
        Return New RectangleF(
            框区域.X - 边框偏移,
            框区域.Y - 边框偏移,
            框区域.Width + 框边框宽度 * s,
            框区域.Height + 框边框宽度 * s)
    End Function

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
        请求V3渲染()
    End Sub
    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If Not Enabled Then Return
        鼠标状态 = MouseStateEnum.Normal
        请求V3渲染()
    End Sub
    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not Enabled Then Return
        鼠标状态 = MouseStateEnum.Pressed
        请求V3渲染()
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
        请求V3渲染()
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
        请求V3渲染()
    End Sub
    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        重置文本行高缓存()
        更新自动尺寸()
        请求V3渲染()
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
            请求V3渲染()
        End If
    End Sub

    Private ReadOnly 动画助手 As New V3_AnimationHelper(Me)

    Private Sub 勾选动画脏区(helper As V3_AnimationHelper, owner As Control, sink As V3_AnimationHelper.InvalidateRegionSink)
        sink.InvalidateAll()
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
            _缓存次文本行高 = TextRenderer.MeasureText("A", 获取次文本字体()).Height
        End If
        Return _缓存次文本行高
    End Function

    Private Sub 重置文本行高缓存()
        _缓存主文本行高 = -1
        _缓存次文本行高 = -1
    End Sub

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

    <Category("LakeUI"), Description(GlobalOptions.动画时长描述词), DefaultValue(300), Browsable(True)>
    Public Property AnimationDuration As Integer
        Get
            Return 动画助手.Duration
        End Get
        Set(value As Integer)
            动画助手.Duration = Math.Max(0, value)
        End Set
    End Property

    <Category("LakeUI"), Description(GlobalOptions.动画帧率描述词), DefaultValue(60), Browsable(True)>
    Public Property AnimationFPS As Integer
        Get
            Return 动画助手.FPS
        End Get
        Set(value As Integer)
            动画助手.FPS = Math.Max(0, value)
        End Set
    End Property

    Private _backgroundSource As Control = Nothing
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
                请求V3渲染()
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
            value = Math.Max(1, value)
            If 次要文本字号 = value Then Return
            _缓存次文本行高 = -1
            次要文本字号 = value
            释放次文本字体()
            请求V3渲染()
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

    Private 禁用时遮罩颜色 As Color = Color.FromArgb(120, 0, 0, 0)
    <Category("LakeUI"), Description("禁用（Enabled = False）时覆盖在复选框区域上的遮罩颜色。"), DefaultValue(GetType(Color), "120, 0, 0, 0"), Browsable(True)>
    Public Property DisabledOverlayColor As Color
        Get
            Return 禁用时遮罩颜色
        End Get
        Set(value As Color)
            SetValue(禁用时遮罩颜色, value)
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
                请求V3渲染()
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
            Dim 次要文本字体 = 获取次文本字体()
            If 次要文本字体 IsNot Nothing Then
                Dim 次文本尺寸 As Size = TextRenderer.MeasureText(次要文本, 次要文本字体, New Size(Integer.MaxValue, Integer.MaxValue), 文本格式)
                Dim _主次间距 As Integer = CInt(Math.Round(主次文本间距 * s))
                Dim 文本总高度 As Integer = 主文本尺寸.Height + _主次间距 + 次文本尺寸.Height
                新高度 = Math.Max(CInt(框尺寸 + 边框额外), 文本总高度) + Me.Padding.Vertical
                新宽度 = Math.Max(新宽度, CInt(Me.Padding.Left + 边框额外 + 框尺寸 + 间距) + 次文本尺寸.Width + Me.Padding.Right)
            End If
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
        释放次文本字体()
        重置文本行高缓存()
        更新自动尺寸()
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        更新自动尺寸()
        请求V3渲染()
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
