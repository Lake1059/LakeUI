Imports System.ComponentModel
Imports System.Numerics
Imports Vortice.Direct2D1
Imports Vortice.DirectWrite

<DefaultEvent("Click")>
Public Class ModernButton
    Implements V3_IGpuRenderable, V3_IGpuInvalidationSource

    Public Sub New()
        InitializeComponent()
        动画助手.DirtyProvider = AddressOf 按钮动画脏区
        长按动画助手.DirtyProvider = AddressOf 按钮动画脏区
    End Sub

#Region "绘制"
    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me, 1) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        If context Is Nothing OrElse Me.Width <= 0 OrElse Me.Height <= 0 Then Return

        Dim 是否有圆角 As Boolean = 边框圆角半径 > 0
        Dim s As Single = DpiScale()
        Dim sourceRect As New RectangleF(0, 0, Me.Width, Me.Height)
        Dim 极限矩形区域 As New RectangleF(0, 0, Me.Width, Me.Height)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * s / 2.0F
            极限矩形区域.Inflate(-half, -half)
        End If
        Dim 内容矩形区域 As New RectangleF(
            极限矩形区域.X + Me.Padding.Left,
            极限矩形区域.Y + Me.Padding.Top,
            极限矩形区域.Width - Me.Padding.Horizontal,
            极限矩形区域.Height - Me.Padding.Vertical)
        Dim 图标宽度 As Single = 计算图标占用的水平宽度(内容矩形区域, s)

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(Me, _backgroundSource, sourceRect)
        ElseIf MyBase.BackColor.A > 0 Then
            填充形状_GPU(context, 极限矩形区域, If(是否有圆角, 边框圆角半径 * s, 0.0F), MyBase.BackColor)
        End If

        绘制图形内容_GPU(context, 是否有圆角, 极限矩形区域, 内容矩形区域, 图标宽度, s)
        绘制文本_GPU(context, 内容矩形区域, 图标宽度, s)

        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            填充形状_GPU(context, 极限矩形区域, If(是否有圆角, 边框圆角半径 * s, 0.0F), 禁用时遮罩颜色)
        End If

        If 长按正在进行 AndAlso 长按动画助手.Progress >= 1.0F Then
            长按正在进行 = False
            BeginInvoke(Sub() 触发点击事件(EventArgs.Empty))
        End If
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Private Sub 绘制图形内容_GPU(context As D3D_PaintContext, 是否有圆角 As Boolean, 极限矩形区域 As RectangleF, 内容矩形区域 As RectangleF, 图标宽度 As Single, s As Single)
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

        Dim radius As Single = If(是否有圆角, 边框圆角半径 * s, 0.0F)
        If 背景颜色缓存值.A > 0 OrElse 渐变颜色缓存值.A > 0 Then
            填充形状_GPU(context, 极限矩形区域, radius, 背景颜色缓存值, 渐变颜色缓存值, 渐变方向)
        End If

        Dim geo As ID2D1Geometry = Nothing
        If 是否有圆角 Then geo = D3D_RenderCore.DeviceManager.D2DFactory.CreateRoundedRectangleGeometry(New RoundedRectangle(极限矩形区域, radius, radius))
        Try
            绘制背景图片_GPU(context, 极限矩形区域, geo)
            绘制长按遮罩_GPU(context, 极限矩形区域, geo)
        Finally
            geo?.Dispose()
        End Try

        If 边框颜色缓存值.A > 0 AndAlso 边框宽度 > 0 Then
            绘制形状边框_GPU(context, 极限矩形区域, radius, 边框颜色缓存值, 边框宽度 * s)
        End If

        绘制图标_GPU(context, 内容矩形区域, 图标宽度, s)
    End Sub

    Private Sub 绘制背景图片_GPU(context As D3D_PaintContext, area As RectangleF, geo As ID2D1Geometry)
        If 背景图片 Is Nothing Then Return
        Dim clipped As Boolean
        If geo IsNot Nothing Then
            PushGeometryClip_GPU(context, geo, area)
            clipped = True
        End If
        Try
            context.DrawImage(背景图片, area)
        Finally
            If clipped Then context.DeviceContext.PopLayer()
        End Try
    End Sub

    Private Sub 绘制长按遮罩_GPU(context As D3D_PaintContext, area As RectangleF, geo As ID2D1Geometry)
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

        Dim clipped As Boolean
        If geo IsNot Nothing Then
            PushGeometryClip_GPU(context, geo, area)
            clipped = True
        End If
        Try
            If 长按遮罩颜色.A > 0 Then context.FillRectangle(maskRect, 长按遮罩颜色)
        Finally
            If clipped Then context.DeviceContext.PopLayer()
        End Try
    End Sub

    Private Sub 绘制图标_GPU(context As D3D_PaintContext, 内容矩形区域 As RectangleF, iconSize As Single, s As Single)
        If 图标 Is Nothing OrElse iconSize <= 0 Then Return
        Dim iconX As Single = 内容矩形区域.X + 图标边距 * s
        Dim iconY As Single = 内容矩形区域.Y + (内容矩形区域.Height - iconSize) / 2.0F
        context.DrawImage(图标, New RectangleF(iconX, iconY, iconSize, iconSize))
    End Sub

    Private Sub 绘制文本_GPU(context As D3D_PaintContext, 内容矩形区域 As RectangleF, 图标宽度 As Single, s As Single)
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

        Dim mainText As String = 解析助记键文本(If(MyBase.Text, "")).DisplayText
        If String.IsNullOrEmpty(mainText) AndAlso String.IsNullOrEmpty(次要文本) Then Return

        If Not String.IsNullOrEmpty(次要文本) Then
            Dim mainH As Single = Math.Max(1, TextRenderer.MeasureText(If(String.IsNullOrEmpty(mainText), "A", mainText), Me.Font).Height)
            Using subFont As New Font(Me.Font.FontFamily, 次要文本字号, System.Drawing.FontStyle.Regular)
                Dim subH As Single = Math.Max(1, TextRenderer.MeasureText(次要文本, subFont).Height)
                Dim gap As Single = 主次文本间距 * s
                Dim totalH As Single = mainH + gap + subH
                Dim startY As Single = 文本绘制区域.Y + (文本绘制区域.Height - totalH) / 2.0F
                context.DrawText(mainText, Me.Font, 文本颜色, New RectangleF(文本绘制区域.X, startY, 文本绘制区域.Width, mainH), align, ParagraphAlignment.Near)
                context.DrawText(次要文本, subFont, 次要文本颜色, New RectangleF(文本绘制区域.X, startY + mainH + gap, 文本绘制区域.Width, subH), align, ParagraphAlignment.Near)
            End Using
        Else
            context.DrawText(mainText, Me.Font, 文本颜色, 文本绘制区域, align, ParagraphAlignment.Center)
        End If
    End Sub

    Private Sub 填充形状_GPU(context As D3D_PaintContext,
                         bounds As RectangleF,
                         radius As Single,
                         color As Color,
                         Optional gradientColor As Color = Nothing,
                         Optional gradientDirection As System.Windows.Forms.Orientation = System.Windows.Forms.Orientation.Horizontal)
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        If color.A = 0 AndAlso (gradientColor = Color.Empty OrElse gradientColor.A = 0) Then Return

        Dim brush As ID2D1Brush = Nothing
        Dim ownsBrush As Boolean
        If gradientColor <> Color.Empty AndAlso gradientColor.A > 0 Then
            brush = 创建线性渐变画刷_GPU(context, bounds, color, gradientColor, gradientDirection)
            ownsBrush = True
        Else
            brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        End If

        Try
            If radius > 0 Then
                Using geo = D3D_RenderCore.DeviceManager.D2DFactory.CreateRoundedRectangleGeometry(New RoundedRectangle(bounds, radius, radius))
                    context.DeviceContext.FillGeometry(geo, brush)
                End Using
            Else
                context.DeviceContext.FillRectangle(D3D_PaintContext.ToRawRect(bounds), brush)
            End If
        Finally
            If ownsBrush Then brush.Dispose()
        End Try
    End Sub

    Private Sub 绘制形状边框_GPU(context As D3D_PaintContext, bounds As RectangleF, radius As Single, color As Color, strokeWidth As Single)
        If color.A = 0 OrElse strokeWidth <= 0 OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If radius > 0 Then
            Using geo = D3D_RenderCore.DeviceManager.D2DFactory.CreateRoundedRectangleGeometry(New RoundedRectangle(bounds, radius, radius))
                context.DeviceContext.DrawGeometry(geo, brush, strokeWidth)
            End Using
        Else
            context.DeviceContext.DrawRectangle(D3D_PaintContext.ToRawRect(bounds), brush, strokeWidth)
        End If
    End Sub

    Private Shared Function 创建线性渐变画刷_GPU(context As D3D_PaintContext,
                                             bounds As RectangleF,
                                             baseColor As Color,
                                             gradColor As Color,
                                             gradDir As System.Windows.Forms.Orientation) As ID2D1LinearGradientBrush
        Dim startPt As Vector2
        Dim endPt As Vector2
        If gradDir = System.Windows.Forms.Orientation.Vertical Then
            startPt = New Vector2(bounds.X, bounds.Y)
            endPt = New Vector2(bounds.X, bounds.Bottom)
        Else
            startPt = New Vector2(bounds.X, bounds.Y)
            endPt = New Vector2(bounds.Right, bounds.Y)
        End If

        Dim stops() As GradientStop = {
            New GradientStop With {.Position = 0.0F, .Color = D3D_PaintContext.ToColor4(baseColor)},
            New GradientStop With {.Position = 1.0F, .Color = D3D_PaintContext.ToColor4(gradColor)}}
        Dim stopCollection = context.DeviceContext.CreateGradientStopCollection(stops)
        Try
            Return context.DeviceContext.CreateLinearGradientBrush(New LinearGradientBrushProperties(startPt, endPt), stopCollection)
        Finally
            stopCollection.Dispose()
        End Try
    End Function

    Private Shared Sub PushGeometryClip_GPU(context As D3D_PaintContext, geo As ID2D1Geometry, bounds As RectangleF)
        Dim parameters As New LayerParameters With {
            .ContentBounds = New Vortice.RawRectF(bounds.X, bounds.Y, bounds.Right, bounds.Bottom),
            .GeometricMask = geo,
            .MaskAntialiasMode = AntialiasMode.PerPrimitive,
            .MaskTransform = Matrix3x2.Identity,
            .Opacity = 1.0F,
            .OpacityBrush = Nothing,
            .LayerOptions = LayerOptions.None
        }
        context.DeviceContext.PushLayer(parameters, Nothing)
    End Sub






    Private Sub 绘制文本_D2D(rt As ID2D1DCRenderTarget, 内容矩形区域 As RectangleF, 图标宽度 As Single, s As Single, textFormatCache As D3D_D2DInterop.TextFormatCache, brushCache As D3D_D2DInterop.SolidColorBrushCache)
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

        Dim mainTextInfo = 解析助记键文本(If(MyBase.Text, ""))
        Dim mainText As String = mainTextInfo.DisplayText
        If String.IsNullOrEmpty(mainText) AndAlso String.IsNullOrEmpty(次要文本) Then Return
        ' 控件 Font 可能已被 WinForms AutoScale 修改，DirectWrite 字号统一交给 D3D_D2DInterop 推断基准 DPI。
        Dim mainSizePx As Single = D3D_D2DInterop.GetDWriteFontSizePx(Me.Font, s)
        Dim dw = D3D_D2DInterop.GetDWriteFactory()

        If Not String.IsNullOrEmpty(次要文本) Then
            ' 次要文本字号是独立数值属性，不会被 WinForms 自动缩放，所以按当前 DPI 显式放大。
            Dim subSizePx As Single = 次要文本字号 * (96.0F / 72.0F) * s
            Dim mainFmt = textFormatCache.Get(Me.Font, mainSizePx, align, ParagraphAlignment.Near, False)
            Dim subFmt = textFormatCache.Get(Me.Font.FontFamily.Name, Vortice.DirectWrite.FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, subSizePx, align, ParagraphAlignment.Near, False)
            Using mainLayout = dw.CreateTextLayout(mainText, mainFmt, 文本绘制区域.Width, 文本绘制区域.Height)
                Using subLayout = dw.CreateTextLayout(次要文本, subFmt, 文本绘制区域.Width, 文本绘制区域.Height)
                    应用助记键下划线(mainLayout, mainTextInfo.MnemonicIndex)
                    Dim mm = mainLayout.Metrics
                    Dim sm = subLayout.Metrics
                    Dim _主次文本间距 As Single = 主次文本间距 * s
                    Dim totalH As Single = mm.Height + _主次文本间距 + sm.Height
                    Dim startY As Single = 文本绘制区域.Y + (文本绘制区域.Height - totalH) / 2.0F
                    Dim fb1 = brushCache.Get(rt, 文本颜色)
                    Dim fb2 = brushCache.Get(rt, 次要文本颜色)
                    If fb1 IsNot Nothing Then rt.DrawTextLayout(New Vector2(文本绘制区域.X, startY), mainLayout, fb1)
                    If fb2 IsNot Nothing Then rt.DrawTextLayout(New Vector2(文本绘制区域.X, startY + mm.Height + _主次文本间距), subLayout, fb2)
                End Using
            End Using
        Else
            Dim mainFmt = textFormatCache.Get(Me.Font, mainSizePx, align, ParagraphAlignment.Center, False)
            Using mainLayout = dw.CreateTextLayout(mainText, mainFmt, 文本绘制区域.Width, 文本绘制区域.Height)
                应用助记键下划线(mainLayout, mainTextInfo.MnemonicIndex)
                Dim fb = brushCache.Get(rt, 文本颜色)
                If fb IsNot Nothing Then rt.DrawTextLayout(New Vector2(文本绘制区域.X, 文本绘制区域.Y), mainLayout, fb)
            End Using
        End If
    End Sub

    Private Structure 助记键文本信息
        Public DisplayText As String
        Public MnemonicIndex As Integer
    End Structure

    Private Function 解析助记键文本(text As String) As 助记键文本信息
        Dim result As New 助记键文本信息 With {.DisplayText = "", .MnemonicIndex = -1}
        If String.IsNullOrEmpty(text) Then Return result
        If text.IndexOf("&"c) < 0 Then
            result.DisplayText = text
            Return result
        End If

        Dim sb As New System.Text.StringBuilder(text.Length)
        Dim i As Integer = 0
        While i < text.Length
            Dim ch As Char = text(i)
            If ch = "&"c Then
                If i + 1 < text.Length AndAlso text(i + 1) = "&"c Then
                    sb.Append("&"c)
                    i += 2
                    Continue While
                End If
                If i + 1 < text.Length AndAlso result.MnemonicIndex < 0 Then
                    result.MnemonicIndex = sb.Length
                End If
                i += 1
                Continue While
            End If
            sb.Append(ch)
            i += 1
        End While

        result.DisplayText = sb.ToString()
        Return result
    End Function

    Private Sub 应用助记键下划线(layout As IDWriteTextLayout, mnemonicIndex As Integer)
        If mnemonicIndex < 0 OrElse Not ShowKeyboardCues Then Return
        layout.SetUnderline(True, New TextRange(mnemonicIndex, 1))
    End Sub

    Private Function 计算图标占用的水平宽度(内容矩形区域 As RectangleF, s As Single) As Single
        If 图标 Is Nothing Then Return 0
        Return Math.Max(0, Math.Min(内容矩形区域.Height - 图标边距 * s * 2, 内容矩形区域.Width * 0.3F))
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
    Private Sub 按钮动画脏区(helper As V3_AnimationHelper, owner As Control, sink As V3_AnimationHelper.InvalidateRegionSink)
        If helper IsNot 长按动画助手 OrElse Not 长按确认已启用 Then
            sink.InvalidateAll()
            Return
        End If

        Dim currentProgress As Single = Math.Clamp(长按动画助手.Progress, 0.0F, 1.0F)
        Dim dirty As Rectangle = 长按遮罩脏区(长按上次失效进度, currentProgress)
        长按上次失效进度 = currentProgress
        If dirty.Width > 0 AndAlso dirty.Height > 0 Then
            sink.Add(dirty)
        Else
            sink.SuppressInvalidate()
        End If
    End Sub

    Private Function 长按遮罩脏区(oldProgress As Single, newProgress As Single) As Rectangle
        Dim oldRect = 长按遮罩客户区矩形(oldProgress)
        Dim newRect = 长按遮罩客户区矩形(newProgress)
        If oldRect.IsEmpty Then Return newRect
        If newRect.IsEmpty Then Return oldRect
        Return Rectangle.Union(oldRect, newRect)
    End Function

    Private Function 长按遮罩客户区矩形(progress As Single) As Rectangle
        If progress <= 0.0F OrElse ClientRectangle.Width <= 0 OrElse ClientRectangle.Height <= 0 Then Return Rectangle.Empty
        progress = Math.Clamp(progress, 0.0F, 1.0F)
        Dim w As Integer = Math.Max(1, CInt(Math.Ceiling(ClientRectangle.Width * progress)))
        Dim rect As Rectangle
        If 长按遮罩方向 = HoldClickDirectionEnum.LeftToRight Then
            rect = New Rectangle(0, 0, w, ClientRectangle.Height)
        Else
            rect = New Rectangle(ClientRectangle.Width - w, 0, w, ClientRectangle.Height)
        End If
        rect.Inflate(2, 2)
        Return Rectangle.Intersect(ClientRectangle, rect)
    End Function
    Private Sub 切换鼠标颜色状态(新状态 As MouseStateEnum)
        If 新状态 = 鼠标状态 Then Return
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
    Private ReadOnly 动画助手 As New V3_AnimationHelper(Me)
    Private ReadOnly 长按动画助手 As New V3_AnimationHelper(Me) With {.EasingMode = V3_AnimationHelper.EasingModeEnum.EaseInOut, .Duration = 800}
    Private 长按正在进行 As Boolean = False
    Private 长按上次失效进度 As Single = -1.0F
    Private 颜色动画已启用 As Boolean = False
    Private 动画前背景颜色 As Color
    Private 动画前渐变颜色 As Color
    Private 动画前边框颜色 As Color
    Private 助记键触发计时器 As Timer
    Private 点击后等待鼠标移动 As Boolean = False
    Private 点击后鼠标屏幕位置 As Point = Point.Empty

    Private Sub 触发点击事件(e As EventArgs)
        If Not Enabled OrElse IsDisposed Then Return

        Dim 点击期间进入嵌套消息循环 As Boolean = False
        Dim 嵌套消息循环探测计时器 As New Timer() With {.Interval = 1}
        AddHandler 嵌套消息循环探测计时器.Tick,
            Sub()
                点击期间进入嵌套消息循环 = True
                直接恢复常规鼠标状态(True)
                嵌套消息循环探测计时器.Stop()
            End Sub
        嵌套消息循环探测计时器.Start()

        Try
            MyBase.OnClick(e)
        Finally
            嵌套消息循环探测计时器.Stop()
            嵌套消息循环探测计时器.Dispose()

            If Not IsDisposed AndAlso Enabled Then
                If 点击期间进入嵌套消息循环 Then
                    直接恢复常规鼠标状态(True)
                    延后恢复点击后的常规状态()
                Else
                    按当前鼠标位置刷新状态()
                End If
            End If
        End Try
    End Sub

    Private Sub 延后恢复点击后的常规状态()
        If Not IsHandleCreated Then Return
        Try
            BeginInvoke(Sub()
                            If IsDisposed OrElse Not Enabled Then Exit Sub
                            直接恢复常规鼠标状态(True)
                        End Sub)
        Catch ex As InvalidOperationException
        End Try
    End Sub

    Private Sub 直接恢复常规鼠标状态(Optional 等待鼠标实际移动 As Boolean = False)
        If IsDisposed Then Return

        If Capture Then Capture = False
        鼠标状态 = MouseStateEnum.Normal
        颜色动画已启用 = False
        动画助手.StopAnimation()
        停止长按确认()

        If 等待鼠标实际移动 Then
            点击后等待鼠标移动 = True
            点击后鼠标屏幕位置 = Cursor.Position
        Else
            点击后等待鼠标移动 = False
        End If

        请求V3渲染()
    End Sub

    Private Sub 按当前鼠标位置刷新状态()
        点击后等待鼠标移动 = False
        If IsDisposed OrElse Not Enabled Then Return

        Dim 鼠标在控件内 As Boolean = ClientRectangle.Contains(PointToClient(Cursor.Position))
        切换鼠标颜色状态(If(鼠标在控件内, MouseStateEnum.Hover, MouseStateEnum.Normal))
    End Sub

    Private Function 点击后仍在等待同一鼠标位置() As Boolean
        If Not 点击后等待鼠标移动 Then Return False
        If Cursor.Position.Equals(点击后鼠标屏幕位置) Then Return True

        点击后等待鼠标移动 = False
        Return False
    End Function

    Private Sub 停止长按确认()
        If Not 长按正在进行 AndAlso 长按动画助手.Progress <= 0 Then Return
        长按正在进行 = False
        长按动画助手.StopAnimation()
        长按动画助手.SetImmediate(0)
    End Sub

    Protected Overrides Sub OnClick(e As EventArgs)
        If Not 长按确认已启用 Then
            触发点击事件(e)
        End If
    End Sub
    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        If Not Enabled Then Return
        If 点击后仍在等待同一鼠标位置() Then Return
        切换鼠标颜色状态(MouseStateEnum.Hover)
    End Sub
    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If Not Enabled Then Return
        If 点击后仍在等待同一鼠标位置() Then Return
        If 鼠标状态 = MouseStateEnum.Normal AndAlso ClientRectangle.Contains(e.Location) Then
            切换鼠标颜色状态(MouseStateEnum.Hover)
        End If
    End Sub
    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If Not Enabled Then Return
        点击后等待鼠标移动 = False
        切换鼠标颜色状态(MouseStateEnum.Normal)
        停止长按确认()
    End Sub
    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not Enabled Then Return
        点击后等待鼠标移动 = False
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
        If 点击后仍在等待同一鼠标位置() Then
            直接恢复常规鼠标状态(True)
        Else
            切换鼠标颜色状态(If(ClientRectangle.Contains(e.Location), MouseStateEnum.Hover, MouseStateEnum.Normal))
        End If
        停止长按确认()
    End Sub
    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        If Not Enabled Then
            鼠标状态 = MouseStateEnum.Normal
            颜色动画已启用 = False
            点击后等待鼠标移动 = False
            动画助手.StopAnimation()
            助记键触发计时器?.Stop()
            助记键触发计时器?.Dispose()
            助记键触发计时器 = Nothing
            停止长按确认()
        End If
        请求V3渲染()
    End Sub
    Protected Overrides Sub OnChangeUICues(e As UICuesEventArgs)
        MyBase.OnChangeUICues(e)
        If (e.Changed And UICues.ChangeKeyboard) <> 0 Then 请求V3渲染()
    End Sub
    Protected Overrides Function ProcessMnemonic(charCode As Char) As Boolean
        If CanSelect AndAlso IsMnemonic(charCode, MyBase.Text) Then
            If 长按确认已启用 Then
                Return True
            End If
            通过助记键触发点击()
            Return True
        End If
        Return MyBase.ProcessMnemonic(charCode)
    End Function

    Private Sub 通过助记键触发点击()
        If Not Enabled Then Return
        助记键触发计时器?.Stop()
        助记键触发计时器?.Dispose()

        切换鼠标颜色状态(MouseStateEnum.Pressed)

        助记键触发计时器 = New Timer() With {.Interval = Math.Max(1, 动画助手.Duration)}
        AddHandler 助记键触发计时器.Tick,
            Sub()
                助记键触发计时器.Stop()
                助记键触发计时器.Dispose()
                助记键触发计时器 = Nothing

                触发点击事件(EventArgs.Empty)
            End Sub
        助记键触发计时器.Start()
    End Sub
    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        请求V3渲染()
    End Sub
#End Region

#Region "通用"
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
            Dim fps As Integer = Math.Max(0, value)
            动画助手.FPS = fps
            长按动画助手.FPS = fps
        End Set
    End Property

    Private _backgroundSource As Control = Nothing
    ''' <summary>
    ''' 背景采样源（超容器背景映射）。透明背景模式下，控件会调用此控件的绘制流程取像素作为底图，
    ''' 从而实现跨越任意层级的"穿透显示"效果。
    ''' 为 Nothing 时自动沿祖先链查找首个不透明祖先（默认行为）。
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

#Region "生命周期"
    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        请求V3渲染()
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
