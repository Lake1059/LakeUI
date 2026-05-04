Imports System.ComponentModel
Imports System.Numerics
Imports Vortice.Direct2D1
Imports Vortice.DirectWrite

<DefaultEvent("Click")>
Public Class ModernButton
#Region "D2D 资源"
    ''' <summary>控件级 DC RenderTarget。与窗口 DC 强相关，无法跨控件共享，由控件持有。</summary>
    Private _dcRT As ID2D1DCRenderTarget
    ''' <summary>跨帧复用的 SSAA 离屏 BitmapRT，按 (Width, Height, ssaa) 命中。</summary>
    Private ReadOnly _ssaaCache As New D2DHelper.BitmapRTCache()
    ''' <summary>背景图缓存。</summary>
    Private ReadOnly _backImageCache As New D2DHelper.D2DBitmapCache()
    ''' <summary>图标缓存。</summary>
    Private ReadOnly _iconCache As New D2DHelper.D2DBitmapCache()

    Private Function GetOrCreateDCRenderTarget() As ID2D1DCRenderTarget
        If _dcRT Is Nothing Then _dcRT = D2DHelper.CreateDCRenderTarget()
        Return _dcRT
    End Function

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        Try : _ssaaCache.Dispose() : Catch : End Try
        Try : _backImageCache.Dispose() : Catch : End Try
        Try : _iconCache.Dispose() : Catch : End Try
        If _dcRT IsNot Nothing Then
            Try : _dcRT.Dispose() : Catch : End Try
            _dcRT = Nothing
        End If
        MyBase.OnHandleDestroyed(e)
    End Sub
#End Region

#Region "绘制"
    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' 有圆角或半透明背景时由 OnPaint 负责绘制父容器背景，此处不做默认填充
        If 边框圆角半径 > 0 OrElse 背景基础颜色.A < 255 Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim 是否有圆角 As Boolean = 边框圆角半径 > 0
        ' 透明背景采样：保留对共享缓存的调用（GDI 路径），随后再切换到 D2D 绘制
        If 是否有圆角 OrElse 背景基础颜色.A < 255 Then
            TransparentBackgroundCache.PaintBackgroundFor(Me, e.Graphics, _backgroundSource)
        End If

        Dim 极限矩形区域 As New RectangleF(0, 0, Me.Width, Me.Height)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * DpiScale() / 2.0F
            极限矩形区域.Inflate(-half, -half)
        End If
        Dim 内容矩形区域 As New RectangleF(
            极限矩形区域.X + Me.Padding.Left,
            极限矩形区域.Y + Me.Padding.Top,
            极限矩形区域.Width - Me.Padding.Horizontal,
            极限矩形区域.Height - Me.Padding.Vertical)

        Dim ssaa As Integer = Math.Max(1, CInt(超采样倍率))
        If Class1.GlobalSSAA <> Class1.SuperSamplingScaleEnum.OFF Then ssaa = Math.Max(ssaa, CInt(Class1.GlobalSSAA))

        Using scope = D2DHelper.BeginPaint(e, Me, GetOrCreateDCRenderTarget(), ssaa, _ssaaCache)
            Dim gRT As ID2D1RenderTarget = scope.GraphicsRenderTarget
            Dim dcRT As ID2D1DCRenderTarget = scope.DCRenderTarget

            ' 1) 图形层（享受 SSAA）
            绘制图形内容_D2D(gRT, 是否有圆角, 极限矩形区域, 内容矩形区域)

            ' 2) 把图形层（如果是 BitmapRT）回采到 DC，然后在 DC 上画文字（保留 ClearType 子像素）
            scope.FlushGraphics()
            绘制文本_D2D(dcRT, 内容矩形区域, 计算图标占用的水平宽度(内容矩形区域))

            ' 3) 禁用遮罩（直接覆盖整个 DC，不需要 SSAA）
            If Not Enabled Then
                Using mb = dcRT.CreateSolidColorBrush(D2DHelper.ToColor4(Color.FromArgb(120, 0, 0, 0)))
                    dcRT.FillRectangle(New Vortice.Mathematics.Rect(0, 0, Me.Width, Me.Height), mb)
                End Using
            End If
        End Using

        If 长按正在进行 AndAlso 长按动画助手.Progress >= 1.0F Then
            长按正在进行 = False
            BeginInvoke(Sub() MyBase.OnClick(EventArgs.Empty))
        End If
    End Sub

    Private Sub 绘制图形内容_D2D(rt As ID2D1RenderTarget, 是否有圆角 As Boolean, 极限矩形区域 As RectangleF, 内容矩形区域 As RectangleF)
        Dim 背景颜色缓存值 As Color
        Dim 渐变颜色缓存值 As Color
        Dim 边框颜色缓存值 As Color
        If 颜色动画已启用 Then
            Dim 目标背景 As Color = Nothing, 目标渐变 As Color = Nothing, 目标边框 As Color = Nothing
            根据鼠标状态分配颜色(目标背景, 目标渐变, 目标边框)
            Dim t As Single = 动画助手.Progress
            背景颜色缓存值 = 颜色插值(动画前背景颜色, 目标背景, t)
            渐变颜色缓存值 = 颜色插值(动画前渐变颜色, 目标渐变, t)
            边框颜色缓存值 = 颜色插值(动画前边框颜色, 目标边框, t)
        Else
            根据鼠标状态分配颜色(背景颜色缓存值, 渐变颜色缓存值, 边框颜色缓存值)
        End If
        Dim s As Single = DpiScale()
        Dim r As Single = 边框圆角半径 * s

        If 是否有圆角 Then
            Using geo = RectangleRenderer.创建圆角矩形几何(极限矩形区域, r)
                If 背景颜色缓存值.A > 0 OrElse 渐变颜色缓存值.A > 0 Then
                    RectangleRenderer.绘制圆角背景_D2D(rt, geo, 极限矩形区域, 背景颜色缓存值, 渐变颜色缓存值, 渐变方向)
                End If
                绘制背景图片_D2D(rt, 极限矩形区域, geo)
                绘制长按遮罩_D2D(rt, 极限矩形区域, geo)
                If 边框颜色缓存值.A > 0 AndAlso 边框宽度 > 0 Then
                    RectangleRenderer.绘制圆角边框_D2D(rt, geo, 边框颜色缓存值, 边框宽度 * s)
                End If
            End Using
        Else
            If 背景颜色缓存值.A > 0 OrElse 渐变颜色缓存值.A > 0 Then
                RectangleRenderer.绘制矩形背景_D2D(rt, 极限矩形区域, 背景颜色缓存值, 渐变颜色缓存值, 渐变方向)
            End If
            绘制背景图片_D2D(rt, 极限矩形区域, Nothing)
            绘制长按遮罩_D2D(rt, 极限矩形区域, Nothing)
            If 边框颜色缓存值.A > 0 AndAlso 边框宽度 > 0 Then
                RectangleRenderer.绘制矩形边框_D2D(rt, 极限矩形区域, 边框颜色缓存值, 边框宽度 * s)
            End If
        End If

        绘制图标_D2D(rt, 内容矩形区域)
    End Sub

    Private Sub 绘制背景图片_D2D(rt As ID2D1RenderTarget, area As RectangleF, geo As ID2D1Geometry)
        If 背景图片 Is Nothing Then Return
        Dim hasMask As Boolean = geo IsNot Nothing
        If hasMask Then D2DHelper.PushGeometryClip(rt, geo, area)
        Try
            Dim bmp = _backImageCache.GetBitmap(rt, 背景图片)
            If bmp IsNot Nothing Then
                rt.DrawBitmap(bmp, D2DHelper.ToD2DRect(area), 1.0F, BitmapInterpolationMode.Linear, Nothing)
            End If
        Finally
            If hasMask Then rt.PopLayer()
        End Try
    End Sub

    Private Sub 绘制长按遮罩_D2D(rt As ID2D1RenderTarget, area As RectangleF, geo As ID2D1Geometry)
        If Not 长按确认已启用 Then Return
        Dim progress As Single = 长按动画助手.Progress
        If progress < 0.001F Then Return
        Dim maskRect As RectangleF
        If 长按遮罩方向 = HoldClickDirectionEnum.LeftToRight Then
            maskRect = New RectangleF(area.X, area.Y, area.Width * progress, area.Height)
        Else
            Dim w As Single = area.Width * progress
            maskRect = New RectangleF(area.Right - w, area.Y, w, area.Height)
        End If
        Dim hasMask As Boolean = geo IsNot Nothing
        If hasMask Then D2DHelper.PushGeometryClip(rt, geo, area)
        Try
            Using brush = rt.CreateSolidColorBrush(D2DHelper.ToColor4(长按遮罩颜色))
                rt.FillRectangle(D2DHelper.ToD2DRect(maskRect), brush)
            End Using
        Finally
            If hasMask Then rt.PopLayer()
        End Try
    End Sub

    Private Sub 绘制图标_D2D(rt As ID2D1RenderTarget, 内容矩形区域 As RectangleF)
        If 图标 Is Nothing Then Return
        Dim iconSize As Single = 计算图标占用的水平宽度(内容矩形区域)
        Dim iconX As Single = 内容矩形区域.X + 图标边距 * DpiScale()
        Dim iconY As Single = 内容矩形区域.Y + (内容矩形区域.Height - iconSize) / 2.0F
        Dim bmp = _iconCache.GetBitmap(rt, 图标)
        If bmp IsNot Nothing Then
            rt.DrawBitmap(bmp, New Vortice.Mathematics.Rect(iconX, iconY, iconSize, iconSize), 1.0F, BitmapInterpolationMode.Linear, Nothing)
        End If
    End Sub

    Private Sub 绘制文本_D2D(rt As ID2D1DCRenderTarget, 内容矩形区域 As RectangleF, 图标宽度 As Single)
        Dim s As Single = DpiScale()
        Dim _图标边距 As Single = 图标边距 * s
        Dim _边框圆角半径 As Single = 边框圆角半径 * s
        Dim 图标占用总宽度 As Single = If(图标宽度 > 0, 图标宽度 + _图标边距, 0)
        Dim 文本绘制区域 As New RectangleF(
            内容矩形区域.X + 图标占用总宽度 + _边框圆角半径,
            内容矩形区域.Y,
            内容矩形区域.Width - 图标占用总宽度 - _边框圆角半径 * 2,
            内容矩形区域.Height)
        If 文本绘制区域.Width <= 0 OrElse 文本绘制区域.Height <= 0 Then Return

        Dim align As Vortice.DirectWrite.TextAlignment
        Select Case 文字对齐方位
            Case TextAlignEnum.Left : align = Vortice.DirectWrite.TextAlignment.Leading
            Case TextAlignEnum.Right : align = Vortice.DirectWrite.TextAlignment.Trailing
            Case Else : align = Vortice.DirectWrite.TextAlignment.Center
        End Select

        Dim mainText As String = If(MyBase.Text, "")
        Dim mainSizePx As Single = Me.Font.SizeInPoints * (96.0F / 72.0F) * s
        Dim mainWeight As Vortice.DirectWrite.FontWeight = If(Me.Font.Bold, Vortice.DirectWrite.FontWeight.Bold, Vortice.DirectWrite.FontWeight.Normal)
        Dim mainStyle As Vortice.DirectWrite.FontStyle = If(Me.Font.Italic, Vortice.DirectWrite.FontStyle.Italic, Vortice.DirectWrite.FontStyle.Normal)
        Dim familyName As String = Me.Font.FontFamily.Name

        Dim dw = D2DHelper.GetDWriteFactory()

        If Not String.IsNullOrEmpty(次要文本) Then
            Dim subSizePx As Single = 次要文本字号 * (96.0F / 72.0F) * s
            Using mainFmt = dw.CreateTextFormat(familyName, Nothing, mainWeight, mainStyle, Vortice.DirectWrite.FontStretch.Normal, mainSizePx)
                Using subFmt = dw.CreateTextFormat(familyName, Nothing, Vortice.DirectWrite.FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, Vortice.DirectWrite.FontStretch.Normal, subSizePx)
                    mainFmt.TextAlignment = align
                    mainFmt.WordWrapping = WordWrapping.NoWrap
                    mainFmt.ParagraphAlignment = ParagraphAlignment.Near
                    subFmt.TextAlignment = align
                    subFmt.WordWrapping = WordWrapping.NoWrap
                    subFmt.ParagraphAlignment = ParagraphAlignment.Near

                    Using mainLayout = dw.CreateTextLayout(mainText, mainFmt, 文本绘制区域.Width, 文本绘制区域.Height)
                        Using subLayout = dw.CreateTextLayout(次要文本, subFmt, 文本绘制区域.Width, 文本绘制区域.Height)
                            Dim mm = mainLayout.Metrics
                            Dim sm = subLayout.Metrics
                            Dim _主次文本间距 As Single = 主次文本间距 * s
                            Dim totalH As Single = mm.Height + _主次文本间距 + sm.Height
                            Dim startY As Single = 文本绘制区域.Y + (文本绘制区域.Height - totalH) / 2.0F

                            Using fb1 = rt.CreateSolidColorBrush(D2DHelper.ToColor4(文本颜色))
                                rt.DrawTextLayout(New Vector2(文本绘制区域.X, startY), mainLayout, fb1)
                            End Using
                            Using fb2 = rt.CreateSolidColorBrush(D2DHelper.ToColor4(次要文本颜色))
                                rt.DrawTextLayout(New Vector2(文本绘制区域.X, startY + mm.Height + _主次文本间距), subLayout, fb2)
                            End Using
                        End Using
                    End Using
                End Using
            End Using
        Else
            Using mainFmt = dw.CreateTextFormat(familyName, Nothing, mainWeight, mainStyle, Vortice.DirectWrite.FontStretch.Normal, mainSizePx)
                mainFmt.TextAlignment = align
                mainFmt.ParagraphAlignment = ParagraphAlignment.Center
                mainFmt.WordWrapping = WordWrapping.NoWrap
                Using fb = rt.CreateSolidColorBrush(D2DHelper.ToColor4(文本颜色))
                    rt.DrawText(mainText, mainFmt, D2DHelper.ToD2DRect(文本绘制区域), fb)
                End Using
            End Using
        End If
    End Sub

    Private Function 计算图标占用的水平宽度(内容矩形区域 As RectangleF) As Single
        If 图标 Is Nothing Then Return 0
        Return Math.Min(内容矩形区域.Height - 图标边距 * DpiScale() * 2, 内容矩形区域.Width * 0.3F)
    End Function
    Private Sub 根据鼠标状态分配颜色(ByRef _背景颜色 As Color, ByRef _渐变颜色 As Color, ByRef _边框颜色 As Color)
        Select Case 鼠标状态
            Case MouseStateEnum.Hover
                _背景颜色 = If(鼠标移上时背景颜色 <> Color.Empty, 鼠标移上时背景颜色, 背景基础颜色)
                _渐变颜色 = If(鼠标移上时渐变颜色 <> Color.Empty, 鼠标移上时渐变颜色, 背景渐变颜色)
                _边框颜色 = If(鼠标移上时边框颜色 <> Color.Empty, 鼠标移上时边框颜色, 边框颜色)
            Case MouseStateEnum.Pressed
                _背景颜色 = If(鼠标按下时背景颜色 <> Color.Empty, 鼠标按下时背景颜色, 背景基础颜色)
                _渐变颜色 = If(鼠标按下时渐变颜色 <> Color.Empty, 鼠标按下时渐变颜色, 背景渐变颜色)
                _边框颜色 = If(鼠标按下时边框颜色 <> Color.Empty, 鼠标按下时边框颜色, 边框颜色)
            Case Else
                _背景颜色 = 背景基础颜色
                _渐变颜色 = 背景渐变颜色
                _边框颜色 = 边框颜色
        End Select
    End Sub
    Private Sub 切换鼠标颜色状态(新状态 As MouseStateEnum)
        Dim 当前背景 As Color = Nothing, 当前渐变 As Color = Nothing, 当前边框 As Color = Nothing
        If 颜色动画已启用 Then
            Dim 旧目标背景 As Color = Nothing, 旧目标渐变 As Color = Nothing, 旧目标边框 As Color = Nothing
            根据鼠标状态分配颜色(旧目标背景, 旧目标渐变, 旧目标边框)
            Dim t As Single = 动画助手.Progress
            当前背景 = 颜色插值(动画前背景颜色, 旧目标背景, t)
            当前渐变 = 颜色插值(动画前渐变颜色, 旧目标渐变, t)
            当前边框 = 颜色插值(动画前边框颜色, 旧目标边框, t)
        Else
            根据鼠标状态分配颜色(当前背景, 当前渐变, 当前边框)
            颜色动画已启用 = True
        End If
        动画前背景颜色 = 当前背景
        动画前渐变颜色 = 当前渐变
        动画前边框颜色 = 当前边框
        鼠标状态 = 新状态
        动画助手.SetImmediate(0)
        动画助手.AnimateTo(1)
    End Sub
    Private Shared Function 颜色插值(c1 As Color, c2 As Color, t As Single) As Color
        If c1.IsEmpty AndAlso c2.IsEmpty Then Return Color.Empty
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
    Private ReadOnly 动画助手 As New AnimationHelper(Me)
    Private ReadOnly 长按动画助手 As New AnimationHelper(Me) With {.EasingMode = AnimationHelper.EasingModeEnum.EaseInOut, .Duration = 800}
    Private 长按正在进行 As Boolean = False
    Private 颜色动画已启用 As Boolean = False
    Private 动画前背景颜色 As Color
    Private 动画前渐变颜色 As Color
    Private 动画前边框颜色 As Color
    Protected Overrides Sub OnClick(e As EventArgs)
        If Not 长按确认已启用 Then
            MyBase.OnClick(e)
        End If
    End Sub
    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        If Not Enabled Then Return
        切换鼠标颜色状态(MouseStateEnum.Hover)
    End Sub
    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If Not Enabled Then Return
        切换鼠标颜色状态(MouseStateEnum.Normal)
        If 长按确认已启用 Then
            长按正在进行 = False
            长按动画助手.StopAnimation()
            长按动画助手.SetImmediate(0)
        End If
    End Sub
    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not Enabled Then Return
        切换鼠标颜色状态(MouseStateEnum.Pressed)
        If 长按确认已启用 Then
            长按正在进行 = True
            长按动画助手.SetImmediate(0)
            长按动画助手.AnimateTo(1)
        End If
    End Sub
    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If Not Enabled Then Return
        切换鼠标颜色状态(If(ClientRectangle.Contains(e.Location), MouseStateEnum.Hover, MouseStateEnum.Normal))
        If 长按确认已启用 Then
            长按正在进行 = False
            长按动画助手.StopAnimation()
            长按动画助手.SetImmediate(0)
        End If
    End Sub
    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        If Not Enabled Then
            鼠标状态 = MouseStateEnum.Normal
            颜色动画已启用 = False
            动画助手.StopAnimation()
            长按正在进行 = False
            长按动画助手.StopAnimation()
            长按动画助手.SetImmediate(0)
        End If
        Me.Invalidate()
    End Sub
    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        Me.Invalidate()
    End Sub
#End Region

#Region "通用"
    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            Me.Invalidate()
        End If
    End Sub

    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function

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

    Private _backgroundSource As Control = Nothing
    ''' <summary>
    ''' 背景采样源（超容器背景映射）。透明背景模式下，控件会调用此控件的绘制流程取像素作为底图，
    ''' 从而实现跨越任意层级的"穿透显示"效果。
    ''' 为 Nothing 时自动沿祖先链查找首个不透明祖先（默认行为）。
    ''' </summary>
    <Category("LakeUI"),
     Description("背景采样源（超容器背景映射）。设置后将跨越任意层级直接采样此控件的绘制内容作为透明背景；为空时自动选择首个不透明祖先。"),
     DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource IsNot value Then
                _backgroundSource = value
                Me.Invalidate()
            End If
        End Set
    End Property
#End Region

#Region "边框属性"
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
    Private 边框宽度 As Integer = 1
    <Category("LakeUI"), Description("边框宽度"), DefaultValue(GetType(Integer), "1"), Browsable(True)>
    Public Property BorderSize As Integer
        Get
            Return 边框宽度
        End Get
        Set(value As Integer)
            SetValue(边框宽度, value)
        End Set
    End Property
    Private 边框圆角半径 As Integer = 0
    <Category("LakeUI"), Description("边框圆角半径"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property BorderRadius As Integer
        Get
            Return 边框圆角半径
        End Get
        Set(value As Integer)
            SetValue(边框圆角半径, value)
        End Set
    End Property
#End Region

#Region "背景属性"
    Private 背景基础颜色 As Color = Color.FromArgb(36, 36, 36)
    <Category("LakeUI"), Description("背景基础颜色"), DefaultValue(GetType(Color), "36,36,36"), Browsable(True)>
    Public Property BackColor1 As Color
        Get
            Return 背景基础颜色
        End Get
        Set(value As Color)
            SetValue(背景基础颜色, value)
        End Set
    End Property
    Private 背景渐变颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("背景渐变颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property BackColor2 As Color
        Get
            Return 背景渐变颜色
        End Get
        Set(value As Color)
            SetValue(背景渐变颜色, value)
        End Set
    End Property
    Private 渐变方向 As System.Windows.Forms.Orientation = System.Windows.Forms.Orientation.Vertical
    <Category("LakeUI"), Description("渐变方向"), DefaultValue(GetType(System.Windows.Forms.Orientation), "Vertical"), Browsable(True)>
    Public Property BackColorOrientation As System.Windows.Forms.Orientation
        Get
            Return 渐变方向
        End Get
        Set(value As System.Windows.Forms.Orientation)
            SetValue(渐变方向, value)
        End Set
    End Property
    Private 背景图片 As Image = Nothing
    <Category("LakeUI"), Description("背景图片"), DefaultValue(GetType(Image), ""), Browsable(True)>
    Public Property BackImage As Image
        Get
            Return 背景图片
        End Get
        Set(value As Image)
            SetValue(背景图片, value)
        End Set
    End Property
#End Region

#Region "文本属性"
    <Category("LakeUI"), Description("主要文本"), DefaultValue(GetType(String), "ExButton"), Browsable(True), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
    Public Overrides Property Text As String
        Get
            Return MyBase.Text
        End Get
        Set(value As String)
            SetValue(MyBase.Text, value)
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
    <Category("LakeUI"), Description("次要文本"), DefaultValue(GetType(String), ""), Browsable(True)>
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
    <Category("LakeUI"), Description("次要文本字号"), DefaultValue(GetType(Integer), "9"), Browsable(True)>
    Public Property SubTextSize As Integer
        Get
            Return 次要文本字号
        End Get
        Set(value As Integer)
            SetValue(次要文本字号, value)
        End Set
    End Property
    Private 主次文本间距 As Integer = 1
    <Category("LakeUI"), Description("主次文本间距"), DefaultValue(GetType(Integer), "1"), Browsable(True)>
    Public Property MainSubTextSpacing As Integer
        Get
            Return 主次文本间距
        End Get
        Set(value As Integer)
            SetValue(主次文本间距, value)
        End Set
    End Property
    Private 文字对齐方位 As TextAlignEnum = TextAlignEnum.Center
    Public Enum TextAlignEnum
        Center
        Left
        Right
    End Enum
    <Category("LakeUI"), Description("文字对齐方位"), DefaultValue(GetType(TextAlignEnum), "Center"), Browsable(True)>
    Public Property TextAlign As TextAlignEnum
        Get
            Return 文字对齐方位
        End Get
        Set(value As TextAlignEnum)
            SetValue(文字对齐方位, value)
        End Set
    End Property
#End Region

#Region "图标属性"
    Private 图标 As Image = Nothing
    <Category("LakeUI"), Description("图标"), DefaultValue(GetType(Image), ""), Browsable(True)>
    Public Property Icon As Image
        Get
            Return 图标
        End Get
        Set(value As Image)
            SetValue(图标, value)
        End Set
    End Property

    Private 图标边距 As Integer = 5
    <Category("LakeUI"), Description("图标边距"), DefaultValue(GetType(Integer), "5"), Browsable(True)>
    Public Property IconPadding As Integer
        Get
            Return 图标边距
        End Get
        Set(value As Integer)
            SetValue(图标边距, value)
        End Set
    End Property
#End Region

#Region "交互状态属性"
    Private 鼠标移上时背景颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时背景颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverBackColor1 As Color
        Get
            Return 鼠标移上时背景颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时背景颜色, value)
        End Set
    End Property
    Private 鼠标移上时渐变颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时渐变颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverBackColor2 As Color
        Get
            Return 鼠标移上时渐变颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时渐变颜色, value)
        End Set
    End Property
    Private 鼠标移上时边框颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标移上时边框颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property HoverBorderColor As Color
        Get
            Return 鼠标移上时边框颜色
        End Get
        Set(value As Color)
            SetValue(鼠标移上时边框颜色, value)
        End Set
    End Property
    Private 鼠标按下时背景颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时背景颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedBackColor1 As Color
        Get
            Return 鼠标按下时背景颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时背景颜色, value)
        End Set
    End Property
    Private 鼠标按下时渐变颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时渐变颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedBackColor2 As Color
        Get
            Return 鼠标按下时渐变颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时渐变颜色, value)
        End Set
    End Property
    Private 鼠标按下时边框颜色 As Color = Color.Empty
    <Category("LakeUI"), Description("鼠标按下时边框颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property PressedBorderColor As Color
        Get
            Return 鼠标按下时边框颜色
        End Get
        Set(value As Color)
            SetValue(鼠标按下时边框颜色, value)
        End Set
    End Property
#End Region

#Region "长按确认属性"
    Public Enum HoldClickDirectionEnum
        LeftToRight
        RightToLeft
    End Enum

    Private 长按确认已启用 As Boolean = False
    <Category("LakeUI"), Description("启用长按确认模式，按住一定时间后才触发 Click"), DefaultValue(False), Browsable(True)>
    Public Property HoldClickEnabled As Boolean
        Get
            Return 长按确认已启用
        End Get
        Set(value As Boolean)
            SetValue(长按确认已启用, value)
        End Set
    End Property

    <Category("LakeUI"), Description("长按确认动画时长（毫秒）"), DefaultValue(800), Browsable(True)>
    Public Property HoldClickDuration As Integer
        Get
            Return 长按动画助手.Duration
        End Get
        Set(value As Integer)
            长按动画助手.Duration = Math.Max(0, value)
        End Set
    End Property

    <Category("LakeUI"), Description("长按确认动画帧率"), DefaultValue(60), Browsable(True)>
    Public Property HoldClickFPS As Integer
        Get
            Return 长按动画助手.FPS
        End Get
        Set(value As Integer)
            长按动画助手.FPS = Math.Max(0, value)
        End Set
    End Property

    Private 长按遮罩颜色 As Color = Color.FromArgb(80, 255, 255, 255)
    <Category("LakeUI"), Description("长按确认遮罩颜色"), DefaultValue(GetType(Color), "80, 255, 255, 255"), Browsable(True)>
    Public Property HoldClickMaskColor As Color
        Get
            Return 长按遮罩颜色
        End Get
        Set(value As Color)
            SetValue(长按遮罩颜色, value)
        End Set
    End Property

    Private 长按遮罩方向 As HoldClickDirectionEnum = HoldClickDirectionEnum.LeftToRight
    <Category("LakeUI"), Description("长按确认遮罩扫过方向"), DefaultValue(GetType(HoldClickDirectionEnum), "LeftToRight"), Browsable(True)>
    Public Property HoldClickDirection As HoldClickDirectionEnum
        Get
            Return 长按遮罩方向
        End Get
        Set(value As HoldClickDirectionEnum)
            SetValue(长按遮罩方向, value)
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