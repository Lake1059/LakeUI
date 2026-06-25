Imports System.ComponentModel
Imports System.Numerics
Imports Vortice.Direct2D1

<DefaultEvent("CheckedChanged")>
Public Class ModernCheckBox

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
        ' V2 契约（与 ModernButton 一致）：
        '   • BackgroundSource 已设置 → 跳过基类填底，背景由 OnPaint 内显式穿透绘制；
        '   • 否则一律走 .NET 自身透明逻辑——半透明 BackColor 由基类把父级背景合成到 HDC，
        '     不透明色由基类填底。BindDC 之后 DC RT 初始像素即正确底图，
        '     避免"HDC 残留 → 乱照父窗体其它区域"的故障。
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim ssaa As Integer = D2DHelperV2.GetEffectiveSsaaScale(超采样倍率)

        Using scope = D2DHelperV2.BeginPaint(e, Me, ssaa)
            If scope Is Nothing Then Return  ' 设计期 / 无 Form
            Dim compositor = scope.Compositor
            Dim gRT As ID2D1RenderTarget = scope.GraphicsLayer
            Dim dcRT As ID2D1DCRenderTarget = scope.DCRenderTarget

            ' 1) 背景层（V2 显式透明穿透）
            If _backgroundSource IsNot Nothing Then
                BackgroundPenetrationV2.PaintBackground(Me, scope, _backgroundSource)
            End If

            ' 2) 图形层（享受 SSAA）
            绘制图形内容_D2D(gRT, compositor.BrushCache)

            ' 3) 文字层（DC RT 子像素抗锯齿）
            scope.FlushGraphics()
            绘制文本_D2D(dcRT, compositor)

            ' 4) 禁用遮罩
            绘制禁用遮罩_D2D(dcRT, compositor.BrushCache)
        End Using
    End Sub

    Private Sub 绘制图形内容_D2D(rt As ID2D1RenderTarget, brushCache As D2DGlobals.SolidColorBrushCache)
        Dim s As Single = DpiScale()
        Dim 当前框边框宽度 As Single = 框边框宽度 * s
        Dim 框区域 As RectangleF = 计算框区域(s)

        Dim 当前框背景色 As Color = 获取当前框背景颜色()
        Dim 当前框边框色 As Color = 获取鼠标状态颜色(框边框颜色值, 鼠标移上时框边框颜色, 鼠标按下时框边框颜色)

        If 当前模式 = CheckModeEnum.CheckBox Then
            绘制方框_D2D(rt, brushCache, 框区域, 当前框背景色, 当前框边框色, 当前框边框宽度, s)
        Else
            绘制圆框_D2D(rt, brushCache, 框区域, 当前框背景色, 当前框边框色, 当前框边框宽度, s)
        End If
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

    Private Sub 绘制禁用遮罩_D2D(rt As ID2D1RenderTarget, brushCache As D2DGlobals.SolidColorBrushCache)
        If Enabled OrElse 禁用时遮罩颜色.A <= 0 Then Return

        Dim s As Single = DpiScale()
        Dim 边框偏移 As Single = 框边框宽度 * s / 2.0F
        Dim 遮罩区域 As RectangleF = 计算框外缘区域(s)
        If 当前模式 = CheckModeEnum.CheckBox Then
            Dim 圆角 As Single = 框圆角半径 * s + 边框偏移
            If 圆角 > 0 Then
                Using geo = RectangleRenderer.创建圆角矩形几何(遮罩区域, 圆角)
                    RectangleRenderer.绘制圆角背景_D2D(rt, geo, 遮罩区域, 禁用时遮罩颜色, Color.Empty, System.Windows.Forms.Orientation.Vertical, brushCache)
                End Using
            Else
                RectangleRenderer.绘制矩形背景_D2D(rt, 遮罩区域, 禁用时遮罩颜色, Color.Empty, System.Windows.Forms.Orientation.Vertical, brushCache)
            End If
        Else
            Dim brush = brushCache.Get(rt, 禁用时遮罩颜色)
            If brush Is Nothing Then Return

            Dim ellipse As New Ellipse(
                New Vector2(遮罩区域.X + 遮罩区域.Width / 2.0F, 遮罩区域.Y + 遮罩区域.Height / 2.0F),
                遮罩区域.Width / 2.0F,
                遮罩区域.Height / 2.0F)
            rt.FillEllipse(ellipse, brush)
        End If
    End Sub

    Private Sub 绘制方框_D2D(rt As ID2D1RenderTarget, brushCache As D2DGlobals.SolidColorBrushCache, 框区域 As RectangleF, 背景色 As Color, 边框色 As Color, 边框宽 As Single, s As Single)
        Dim 圆角 As Single = 框圆角半径 * s
        If 圆角 > 0 Then
            Using geo = RectangleRenderer.创建圆角矩形几何(框区域, 圆角)
                If 背景色.A > 0 Then
                    Dim brush = brushCache.Get(rt, 背景色)
                    If brush IsNot Nothing Then rt.FillGeometry(geo, brush)
                End If
                If 边框宽 > 0 AndAlso 边框色.A > 0 Then
                    Dim brush = brushCache.Get(rt, 边框色)
                    If brush IsNot Nothing Then rt.DrawGeometry(geo, brush, 边框宽)
                End If
            End Using
        Else
            If 背景色.A > 0 Then
                Dim brush = brushCache.Get(rt, 背景色)
                If brush IsNot Nothing Then rt.FillRectangle(D2DGlobals.ToD2DRect(框区域), brush)
            End If
            If 边框宽 > 0 AndAlso 边框色.A > 0 Then
                Dim brush = brushCache.Get(rt, 边框色)
                If brush IsNot Nothing Then rt.DrawRectangle(D2DGlobals.ToD2DRect(框区域), brush, 边框宽)
            End If
        End If
        ' 绘制勾号笔迹动画
        Dim progress As Single = 动画助手.Progress
        If progress > 0.001F Then
            绘制勾号_D2D(rt, brushCache, 框区域, progress, s)
        End If
    End Sub

    Private Sub 绘制勾号_D2D(rt As ID2D1RenderTarget, brushCache As D2DGlobals.SolidColorBrushCache, 框区域 As RectangleF, progress As Single, s As Single)
        Dim 当前勾号色 As Color = 获取鼠标状态颜色(勾号颜色值, 鼠标移上时勾号颜色, 鼠标按下时勾号颜色)
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
        Dim 笔宽 As Single = 勾号线宽 * s
        Dim 可见长度 As Single = 总长度 * progress

        ' 用进度截断的两段折线（动画效果同 GDI 版本）
        Dim path As ID2D1PathGeometry = D2DGlobals.GetD2DFactory().CreatePathGeometry()
        Dim sink As ID2D1GeometrySink = path.Open()
        Try
            sink.BeginFigure(New Vector2(x1, y1), FigureBegin.Hollow)
            If 可见长度 <= 段1长 Then
                Dim t As Single = If(段1长 > 0, 可见长度 / 段1长, 0F)
                Dim ex As Single = x1 + (x2 - x1) * t
                Dim ey As Single = y1 + (y2 - y1) * t
                sink.AddLine(New Vector2(ex, ey))
            Else
                sink.AddLine(New Vector2(x2, y2))
                Dim 剩余 As Single = 可见长度 - 段1长
                Dim t As Single = If(段2长 > 0, 剩余 / 段2长, 0F)
                Dim ex As Single = x2 + (x3 - x2) * t
                Dim ey As Single = y2 + (y3 - y2) * t
                sink.AddLine(New Vector2(ex, ey))
            End If
            sink.EndFigure(FigureEnd.Open)
            sink.Close()
        Finally
            sink.Dispose()
        End Try
        Try
            Dim brush = brushCache.Get(rt, 当前勾号色)
            If brush Is Nothing Then Return
            rt.DrawGeometry(path, brush, 笔宽, D2DGlobals.GetRoundStrokeStyle())
        Finally
            path.Dispose()
        End Try
    End Sub

    Private Sub 绘制圆框_D2D(rt As ID2D1RenderTarget, brushCache As D2DGlobals.SolidColorBrushCache, 框区域 As RectangleF, 背景色 As Color, 边框色 As Color, 边框宽 As Single, s As Single)
        Dim cx As Single = 框区域.X + 框区域.Width / 2.0F
        Dim cy As Single = 框区域.Y + 框区域.Height / 2.0F
        Dim rx As Single = 框区域.Width / 2.0F
        Dim ry As Single = 框区域.Height / 2.0F
        Dim e As New Ellipse(New Vector2(cx, cy), rx, ry)

        If 背景色.A > 0 Then
            Dim brush = brushCache.Get(rt, 背景色)
            If brush IsNot Nothing Then rt.FillEllipse(e, brush)
        End If
        If 边框宽 > 0 AndAlso 边框色.A > 0 Then
            Dim brush = brushCache.Get(rt, 边框色)
            If brush IsNot Nothing Then rt.DrawEllipse(e, brush, 边框宽)
        End If

        ' 绘制内圆缩放动画
        Dim progress As Single = 动画助手.Progress
        If progress > 0.001F Then
            Dim 当前勾号色 As Color = 获取鼠标状态颜色(勾号颜色值, 鼠标移上时勾号颜色, 鼠标按下时勾号颜色)
            Dim 最大半径 As Single = (框区域.Width / 2.0F) - 框内边距 * s - 框边框宽度 * s / 2.0F
            If 最大半径 < 1 Then 最大半径 = 1
            Dim 当前半径 As Single = 最大半径 * progress
            Dim inner As New Ellipse(New Vector2(cx, cy), 当前半径, 当前半径)
            Dim brush = brushCache.Get(rt, 当前勾号色)
            If brush IsNot Nothing Then rt.FillEllipse(inner, brush)
        End If
    End Sub

    Private Sub 绘制文本_D2D(rt As ID2D1DCRenderTarget, compositor As WindowCompositor)
        Dim s As Single = DpiScale()
        Dim 框尺寸 As Single = 操作框尺寸 * s
        Dim 间距 As Single = 框文本间距 * s
        Dim 边框偏移 As Single = 框边框宽度 * s / 2.0F
        Dim 文本X As Single = Me.Padding.Left + 边框偏移 + 框尺寸 + 间距
        Dim 文本可用宽度 As Single = Me.Width - 文本X - Me.Padding.Right
        If 文本可用宽度 <= 0 Then Return
        Dim mainText As String = If(MyBase.Text, "")

        Dim familyName As String = Me.Font.FontFamily.Name
        ' 控件 Font 可能已被 WinForms AutoScale 修改，DirectWrite 字号统一交给 D2DGlobals 推断基准 DPI。
        Dim mainSizePx As Single = D2DGlobals.GetDWriteFontSizePx(Me.Font, s)

        Dim dw = D2DGlobals.GetDWriteFactory()
        Dim textFormatCache = compositor.TextFormatCache
        Dim brushCache = compositor.BrushCache
        Dim 框区域 As RectangleF = 计算框区域(s)
        Dim 框中心Y As Single = 框区域.Y + 框区域.Height / 2.0F

        Dim mainWeight As Vortice.DirectWrite.FontWeight = If(Me.Font.Bold, Vortice.DirectWrite.FontWeight.Bold, Vortice.DirectWrite.FontWeight.Normal)
        Dim mainStyle As Vortice.DirectWrite.FontStyle = If(Me.Font.Italic, Vortice.DirectWrite.FontStyle.Italic, Vortice.DirectWrite.FontStyle.Normal)
        Dim mainFmt = textFormatCache.Get(familyName, mainWeight, mainStyle, mainSizePx,
                                          Vortice.DirectWrite.TextAlignment.Leading,
                                          Vortice.DirectWrite.ParagraphAlignment.Near,
                                          True)

        If Not String.IsNullOrEmpty(次要文本) Then
            Dim subSizePx As Single = 次要文本字号 * (96.0F / 72.0F) * s
            Dim subFmt = textFormatCache.Get(familyName, Vortice.DirectWrite.FontWeight.Normal,
                                             Vortice.DirectWrite.FontStyle.Normal, subSizePx,
                                             Vortice.DirectWrite.TextAlignment.Leading,
                                             Vortice.DirectWrite.ParagraphAlignment.Near,
                                             True)
            Using mainLayout = dw.CreateTextLayout(mainText, mainFmt, 文本可用宽度, Me.Height)
                Using subLayout = dw.CreateTextLayout(次要文本, subFmt, 文本可用宽度, Me.Height)
                    Dim mainMetrics = mainLayout.Metrics
                    Dim _主次间距 As Single = 主次文本间距 * s
                    Dim 主文本Y As Single = 框中心Y - mainMetrics.Height / 2.0F
                    Dim fb1 = brushCache.Get(rt, 文本颜色)
                    If fb1 IsNot Nothing Then rt.DrawTextLayout(New Vector2(文本X, 主文本Y), mainLayout, fb1)
                    Dim fb2 = brushCache.Get(rt, 次要文本颜色)
                    If fb2 IsNot Nothing Then rt.DrawTextLayout(New Vector2(文本X, 主文本Y + mainMetrics.Height + _主次间距), subLayout, fb2)
                End Using
            End Using
        Else
            Using mainLayout = dw.CreateTextLayout(mainText, mainFmt, 文本可用宽度, Me.Height)
                Dim mainMetrics = mainLayout.Metrics
                Dim 主文本Y As Single = 框中心Y - mainMetrics.Height / 2.0F
                Dim fb = brushCache.Get(rt, 文本颜色)
                If fb IsNot Nothing Then rt.DrawTextLayout(New Vector2(文本X, 主文本Y), mainLayout, fb)
            End Using
        End If
    End Sub
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
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If Not Enabled Then Return
        鼠标状态 = MouseStateEnum.Normal
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not Enabled Then Return
        鼠标状态 = MouseStateEnum.Pressed
        OuterToInnerRefreshScheduler.RequestFull(Me)
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
        OuterToInnerRefreshScheduler.RequestFull(Me)
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
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        重置文本行高缓存()
        更新自动尺寸()
        OuterToInnerRefreshScheduler.RequestFull(Me)
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
            OuterToInnerRefreshScheduler.RequestFull(Me)
        End If
    End Sub

    Private ReadOnly 动画助手 As New AnimationHelperV2(Me)

    Private Sub 勾选动画脏区(helper As AnimationHelperV2, owner As Control, sink As AnimationHelperV2.InvalidateRegionSink)
        sink.InvalidateAll()
    End Sub

    Private Function DpiScale() As Single
        Return D2DGlobals.GetCurrentDpiScale(Me)
    End Function

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
            Using f As New Font(Me.Font.FontFamily, 次要文本字号, FontStyle.Regular)
                _缓存次文本行高 = TextRenderer.MeasureText("A", f).Height
            End Using
        End If
        Return _缓存次文本行高
    End Function

    Private Sub 重置文本行高缓存()
        _缓存主文本行高 = -1
        _缓存次文本行高 = -1
    End Sub

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
     Description("背景采样源（超容器背景映射）。设置后将跨越任意层级直接采样此控件的绘制内容作为透明背景；为空时自动选择首个不透明祖先。"),
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
                OuterToInnerRefreshScheduler.RequestFull(Me)
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
            _缓存次文本行高 = -1
            SetValue(次要文本字号, value)
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
                OuterToInnerRefreshScheduler.RequestFull(Me)
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
            Using 次要文本字体 As New Font(Me.Font.FontFamily, 次要文本字号, FontStyle.Regular)
                Dim 次文本尺寸 As Size = TextRenderer.MeasureText(次要文本, 次要文本字体, New Size(Integer.MaxValue, Integer.MaxValue), 文本格式)
                Dim _主次间距 As Integer = CInt(Math.Round(主次文本间距 * s))
                Dim 文本总高度 As Integer = 主文本尺寸.Height + _主次间距 + 次文本尺寸.Height
                新高度 = Math.Max(CInt(框尺寸 + 边框额外), 文本总高度) + Me.Padding.Vertical
                新宽度 = Math.Max(新宽度, CInt(Me.Padding.Left + 边框额外 + 框尺寸 + 间距) + 次文本尺寸.Width + Me.Padding.Right)
            End Using
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
        重置文本行高缓存()
        更新自动尺寸()
        D2DHelperV2.RefreshFontDependentRendering(Me)
    End Sub

    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        更新自动尺寸()
        OuterToInnerRefreshScheduler.RequestFull(Me)
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
