Imports System.Numerics

Partial Public Class ThisIsYourWindow
    Partial Friend NotInheritable Class ChromeOverlayControl
        Implements ID3D12Renderable

        Friend Function ChromeCacheKey() As Object
            Dim state = _owner.查找状态(_form)
            If state Is Nothing OrElse (_owner.BackdropMode <> BackdropModeEnum.None AndAlso _owner.BackdropMode <> BackdropModeEnum.Image) Then Return Nothing
            Return (state.Dx12ChromeRevision, _viewportOrigin, _viewportSize, Size, _form.DeviceDpi, _form.WindowState,
                    _form.Text, _form.Icon, _form.ShowIcon, _form.MaximizeBox, _form.MinimizeBox, _form.Font,
                    state.IsFullScreen, state.Activated, state.HoverHit, state.PressedHit, state.LayoutSignature,
                    GlobalOptions.GlobalTextQuality, D3D_HdrOutput.ImageRevision)
        End Function

        Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
            context.AtOffset(New Point(-_viewportOrigin.X, -_viewportOrigin.Y), Sub() _owner.RenderWindowDX12(context, _form, _viewportSize))
        End Sub
    End Class

    Private Function GetCaptionBackdropRoot(state As PerFormState) As Control
        Dim control = _标题栏绑定控件
        If state Is Nothing OrElse control Is Nothing OrElse control.IsDisposed OrElse
           state.HostForm IsNot _标题栏控件宿主窗体 OrElse control.Parent IsNot state.HostForm OrElse
           state.CaptionControlRect.Width <= 0 OrElse state.CaptionControlRect.Height <= 0 Then Return Nothing
        Return control
    End Function

    Private Function UsesCaptionBackdrop(state As PerFormState, consumer As Control) As Boolean
        Dim root = GetCaptionBackdropRoot(state)
        Return root IsNot Nothing AndAlso D3D_ControlTreeWalker.IsDescendantOrSelf(consumer, root)
    End Function

    Friend Shared Function UsesCaptionBackdrop12(form As Form, consumer As Control) As Boolean
        Dim owner As ThisIsYourWindow = Nothing
        Return TryGetAttached(form, owner) AndAlso owner.UsesCaptionBackdrop(owner.查找状态(form), consumer)
    End Function

    Private Shared Sub InvalidateCaptionBackdrop12(control As Control)
        If RenderBackendStartup.Requested = RenderBackend.Direct3D12 AndAlso control IsNot Nothing AndAlso control.Visible Then
            OuterToInnerRefreshScheduler.RequestFull(control, invalidateChildren:=True)
        End If
    End Sub

    Friend Shared Function StaticBackdropKey(form As Form, consumer As Control) As Object
        Dim owner As ThisIsYourWindow = Nothing
        If Not TryGetAttached(form, owner) Then Return Nothing
        Dim state = owner.查找状态(form)
        If state Is Nothing OrElse (owner.BackdropMode <> BackdropModeEnum.None AndAlso owner.BackdropMode <> BackdropModeEnum.Image) Then Return Nothing
        Dim offset = consumer.PointToClient(form.PointToScreen(Point.Empty))
        Dim captionRevision As Long? = Nothing
        If owner.UsesCaptionBackdrop(state, consumer) Then captionRevision = state.Dx12ChromeRevision
        Return (form, consumer.ClientSize, offset, 获取真实客户区尺寸(form), form.BackColor.ToArgb(), captionRevision,
                owner.毛玻璃允许用于窗体(state), owner.BackdropMode, owner.BackdropImage, owner.BackdropBlurRadius,
                owner.BackdropBlurPasses, owner.BackdropDownsampleFactor, owner.BackdropNoiseScale, owner.BackdropNoiseOpacity,
                owner.BackdropTintColor, owner.BackdropTintInactiveColor, 视觉上保持激活(form, state.Activated), D3D_HdrOutput.ImageRevision)
    End Function

    Friend Shared Function DrawFormSurface12(context As D3D12_PaintContext, form As Form, offset As Point) As Boolean
        Dim owner As ThisIsYourWindow = Nothing
        Dim state = If(TryGetAttached(form, owner), owner.查找状态(form), Nothing)
        ' Explicitly sampled forms include their configured chrome, unlike inherited backdrop fills.
        context.AtOffset(offset,
            Sub()
                context.FillRectangle(form.ClientRectangle, form.BackColor)
                If state IsNot Nothing Then owner.RenderWindowDX12(context, form, form.ClientSize)
            End Sub)
        Return True
    End Function

    Friend Shared Function DrawFormBackdrop12(context As D3D12_PaintContext, form As Form, consumer As Control) As Boolean
        Dim owner As ThisIsYourWindow = Nothing
        If Not TryGetAttached(form, owner) Then Return False
        Dim state = owner.查找状态(form)
        If state Is Nothing Then Return False
        Dim size = 获取真实客户区尺寸(form)
        If size.Width <= 0 OrElse size.Height <= 0 Then Return False
        Dim offset = consumer.PointToClient(form.PointToScreen(Point.Empty))
        Dim full As New RectangleF(0, 0, size.Width, size.Height)
        Dim caption = owner.获取标题栏内容矩形(form, size.Width, size.Height)
        context.AtOffset(offset, Sub()
            If Not owner.绘制毛玻璃背景_DX12(context, state, full, caption, 视觉上保持激活(form, state.Activated)) Then context.FillRectangle(full, form.BackColor)
        End Sub)
        Return True
    End Function

    Private Sub RenderWindowDX12(context As D3D12_PaintContext,
                                    targetForm As Form,
                                    viewportSize As Size)
        Dim s = 查找状态(targetForm)
        If context Is Nothing OrElse s Is Nothing Then Return
        ' 标题栏布局必须使用整窗的真实客户区尺寸，不能使用 overlay 自身的裁剪尺寸。
        ' 最小化还原期间 WinForms 可能短暂报告 0 或过渡尺寸；此时跳过一帧，
        ' 防止按钮矩形按 overlay 尺寸重算后瞬间拉伸到整个标题栏。
        Dim 真实尺寸 = 获取真实客户区尺寸(targetForm)
        If targetForm.WindowState = FormWindowState.Minimized Then Return
        If 真实尺寸.Width <= 0 OrElse 真实尺寸.Height <= 0 Then
            If viewportSize.Width <= 0 OrElse viewportSize.Height <= 0 Then Return
            真实尺寸 = viewportSize
        End If
        Dim w As Integer = 真实尺寸.Width
        Dim h As Integer = 真实尺寸.Height

        Dim active As Boolean = 视觉上保持激活(targetForm, s.Activated)
        Dim fullRect As New RectangleF(0, 0, w, h)
        Dim captionRect As Rectangle = 获取标题栏内容矩形(s.HostForm, w, h)
        Dim captionRectF As New RectangleF(captionRect.X, captionRect.Y, captionRect.Width, captionRect.Height)
        Dim drewBackdrop As Boolean = 绘制毛玻璃背景_DX12(context, s, fullRect, captionRectF, active)

        Dim captionCoverage = Rectangle.FromLTRB(0, 0, w, Math.Max(captionRect.Bottom, s.HostForm.Padding.Top))
        captionCoverage.Inflate(2, 2)
        Dim drawCaption = context.IntersectsDirty(captionCoverage)
        If drawCaption Then
            RecalculateButtonBounds(s)
            If Not drewBackdrop AndAlso captionRect.Width > 0 AndAlso captionRect.Height > 0 Then
                Dim capColor As Color = If(active, _标题栏背景颜色, _标题栏失焦背景颜色)
                If capColor.A > 0 Then context.FillRectangle(captionRectF, capColor)
            End If

            If _标题栏背景图片 IsNot Nothing AndAlso captionRect.Width > 0 AndAlso captionRect.Height > 0 Then
                绘制Cover图片_DX12(context, _标题栏背景图片, captionRectF)
            End If

            If _标题栏遮罩颜色.A > 0 AndAlso captionRect.Width > 0 AndAlso captionRect.Height > 0 Then
                context.FillRectangle(captionRectF, _标题栏遮罩颜色)
            End If

            绘制标题栏底部横线_DX12(context, s, captionRect)
            绘制图标_DX12(context, s)
            绘制控制按钮_DX12(context, s, s.CloseRect, HTCLOSE)
            If _显示全屏按钮 AndAlso 全屏允许用于窗体(s) Then
                绘制控制按钮_DX12(context, s, s.FullScreenRect, HTFULLSCREEN)
            End If
            If s.HostForm.MaximizeBox Then 绘制控制按钮_DX12(context, s, s.MaxRect, HTMAXBUTTON)
            If s.HostForm.MinimizeBox Then 绘制控制按钮_DX12(context, s, s.MinRect, HTMINBUTTON)
        End If
        绘制窗口边框_DX12(context, s, w, h, active)
        If drawCaption Then 绘制标题文字_DX12(context, s)
    End Sub

    Private Function 绘制毛玻璃背景_DX12(context As D3D12_PaintContext,
                                 s As PerFormState,
                                 fullRect As RectangleF,
                                 captionRect As RectangleF,
                                 active As Boolean) As Boolean
        If context Is Nothing OrElse s Is Nothing Then Return False
        If _毛玻璃模式 = BackdropModeEnum.None Then Return False
        If Not 毛玻璃允许用于窗体(s) Then Return False

        ' An empty inactive tint keeps the active composition, so focus changes do
        ' not trigger a different backdrop overlay when the surface is repainted.
        Dim tint As Color = If(active OrElse _毛玻璃Tint失焦颜色.IsEmpty,
                               _毛玻璃Tint颜色,
                               _毛玻璃Tint失焦颜色)

        Select Case _毛玻璃模式
            Case BackdropModeEnum.Image
                If _毛玻璃图片 Is Nothing Then Return False
                context.DrawBlurredBackdrop(_毛玻璃图片, fullRect, _毛玻璃模糊半径, _毛玻璃模糊次数, _毛玻璃下采样)
                If tint.A > 0 Then context.FillRectangle(fullRect, tint)
                context.DrawNoise(fullRect, _毛玻璃噪点缩放, _毛玻璃噪点不透明度)
                Return True
            Case BackdropModeEnum.Auto, BackdropModeEnum.CaptionOnly
                If s.Renderer Is Nothing Then 应用毛玻璃状态(s)
                Dim area = If(_毛玻璃模式 = BackdropModeEnum.CaptionOnly, captionRect, fullRect)
                Return s.Renderer IsNot Nothing AndAlso s.Renderer.DrawTo12(context, area, tint, _毛玻璃噪点不透明度)
        End Select

        Return False
    End Function

    Private Sub 绘制Cover图片_DX12(context As D3D12_PaintContext, image As Image, bounds As RectangleF)
        If context Is Nothing OrElse image Is Nothing Then Return
        If image.Width <= 0 OrElse image.Height <= 0 OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return

        Dim ratio As Single = Math.Max(bounds.Width / image.Width, bounds.Height / image.Height)
        If ratio <= 0 Then Return
        Dim drawW As Single = image.Width * ratio
        Dim drawH As Single = image.Height * ratio
        Dim dest As New RectangleF(
            bounds.X + (bounds.Width - drawW) / 2.0F,
            bounds.Y + (bounds.Height - drawH) / 2.0F,
            drawW,
            drawH)

        Using context.PushClip(bounds)
            context.DrawImage(image, dest)
        End Using
    End Sub

    Private Sub 绘制图标_DX12(context As D3D12_PaintContext, s As PerFormState)
        Dim img = 获取标题栏图标(s)
        If img Is Nothing Then Return
        context.DrawCubicImage(img, s.IconRect)
    End Sub

    Private Sub 绘制控制按钮_DX12(context As D3D12_PaintContext, s As PerFormState, rect As Rectangle, htValue As Integer)
        If rect.IsEmpty Then Return

        Dim isClose As Boolean = (htValue = HTCLOSE)
        Dim isHover As Boolean = (s.HoverHit = htValue)
        Dim isPressed As Boolean = (s.PressedHit = htValue)
        Dim bgColor, symColor As Color

        If isClose Then
            If isPressed AndAlso isHover Then
                bgColor = _关闭按钮按下背景颜色 : symColor = _关闭按钮悬停符号颜色
            ElseIf isHover Then
                bgColor = _关闭按钮悬停背景颜色 : symColor = _关闭按钮悬停符号颜色
            Else
                bgColor = _关闭按钮背景颜色 : symColor = _关闭按钮符号颜色
            End If
        Else
            If isPressed AndAlso isHover Then
                bgColor = _功能按钮按下背景颜色 : symColor = _功能按钮悬停符号颜色
            ElseIf isHover Then
                bgColor = _功能按钮悬停背景颜色 : symColor = _功能按钮悬停符号颜色
            Else
                bgColor = _功能按钮背景颜色 : symColor = _功能按钮符号颜色
            End If
        End If

        Dim buttonPadding As Padding = 缩放逻辑内边距(s.HostForm, _按钮内边距)
        Dim visualRect As Rectangle = 应用内边距(rect, buttonPadding)
        Dim vis As New RectangleF(visualRect.X, visualRect.Y, visualRect.Width, visualRect.Height)
        If vis.Width <= 0 OrElse vis.Height <= 0 Then Return

        If bgColor.A > 0 Then
            Dim r As Integer = Math.Min(Math.Max(0, 缩放逻辑尺寸(s.HostForm, _按钮圆角半径)), CInt(Math.Min(vis.Width, vis.Height)) \ 2)
            context.FillRoundedRectangle(vis, r, bgColor)
        End If

        If symColor.A = 0 Then Return
        Dim sz As Integer = Math.Max(缩放逻辑尺寸(s.HostForm, 4), 缩放逻辑尺寸(s.HostForm, _按钮符号大小))
        Dim cx As Single = vis.X + (vis.Width - sz) / 2.0F
        Dim cy As Single = vis.Y + (vis.Height - sz) / 2.0F
        Dim lw As Single = Math.Max(0.5F * 取Dpi缩放(s.HostForm), 缩放逻辑尺寸(s.HostForm, _按钮符号线宽))
        Dim pen = symColor

        Select Case htValue
            Case HTCLOSE
                context.DrawLine(New Vector2(cx, cy), New Vector2(cx + sz, cy + sz), pen, lw)
                context.DrawLine(New Vector2(cx + sz, cy), New Vector2(cx, cy + sz), pen, lw)
            Case HTMAXBUTTON
                If 窗口当前已最大化(s.HostForm) Then
                    Dim off As Single = sz * 0.25F
                    context.DrawRectangle(New RectangleF(cx + off, cy, sz - off, sz - off), pen, lw)
                    context.DrawRectangle(New RectangleF(cx, cy + off, sz - off, sz - off), pen, lw)
                Else
                    context.DrawRectangle(New RectangleF(cx, cy, sz, sz), pen, lw)
                End If
            Case HTMINBUTTON
                Dim mid As Single = cy + sz / 2.0F
                context.DrawLine(New Vector2(cx, mid), New Vector2(cx + sz, mid), pen, lw)
            Case HTFULLSCREEN
                Dim fullScreenLogicalLineWidth As Single = Math.Max(1.0F, _按钮符号线宽 - 1.0F)
                Dim fullScreenLineWidth As Single = 缩放逻辑尺寸(s.HostForm, fullScreenLogicalLineWidth)
                绘制全屏按钮符号_DX12(context, pen, fullScreenLineWidth, cx, cy, sz, s.IsFullScreen)
        End Select
    End Sub

    Private Shared Sub 绘制全屏按钮符号_DX12(context As D3D12_PaintContext,
                                      pen As Color,
                                      lineWidth As Single,
                                      x As Single,
                                      y As Single,
                                      size As Single,
                                      restore As Boolean)
        ' The stroke center stays half a line inside the requested glyph box, so its outer pixels
        ' exactly reach the ButtonGlyphSize boundary without being clipped.
        Dim edge As Single = Math.Max(0.5F, lineWidth / 2.0F)
        Dim head As Single = Math.Max(lineWidth * 1.5F, size * 0.28F)
        Dim center As Single = size / 2.0F
        Dim gap As Single = Math.Max(0.5F, size * 0.035F)
        Dim corners() As Vector2 = {
            New Vector2(x + edge, y + edge),
            New Vector2(x + size - edge, y + edge),
            New Vector2(x + edge, y + size - edge),
            New Vector2(x + size - edge, y + size - edge)}
        Dim inner() As Vector2 = {
            New Vector2(x + center - gap, y + center - gap),
            New Vector2(x + center + gap, y + center - gap),
            New Vector2(x + center - gap, y + center + gap),
            New Vector2(x + center + gap, y + center + gap)}

        For i As Integer = 0 To 3
            Dim arrowStart As Vector2 = If(restore, corners(i), inner(i))
            Dim arrowEnd As Vector2 = If(restore, inner(i), corners(i))
            context.DrawLine(arrowStart, arrowEnd, pen, lineWidth)

            Dim sx As Single = If((i And 1) = 0, 1.0F, -1.0F)
            Dim sy As Single = If((i And 2) = 0, 1.0F, -1.0F)
            If restore Then
                sx = -sx
                sy = -sy
            End If
            context.DrawLine(arrowEnd, New Vector2(arrowEnd.X + sx * head, arrowEnd.Y), pen, lineWidth)
            context.DrawLine(arrowEnd, New Vector2(arrowEnd.X, arrowEnd.Y + sy * head), pen, lineWidth)
        Next
    End Sub

    Private Sub 绘制窗口边框_DX12(context As D3D12_PaintContext, s As PerFormState, w As Integer, h As Integer, active As Boolean)
        If s.IsFullScreen Then Return
        Dim scaledBorderSize As Integer = 取缩放边框厚度(s.HostForm)
        If scaledBorderSize <= 0 Then Return

        Dim bdrColor As Color = If(active, _边框颜色, _边框失焦颜色)
        If bdrColor.A = 0 Then Return

        Dim bdr As Integer = Math.Min(scaledBorderSize, Math.Max(0, Math.Min(w, h)))
        If bdr <= 0 Then Return

        ' DWM 只负责最终窗口裁切，边框始终由 LakeUI 自绘；这样边框与内容使用同一套 GPU 几何，
        ' 不会在圆角处叠加系统边框的第二层抗锯齿。
        If 当前使用圆角模式(s) Then
            Dim logicalRadius As Single = DwmWindowStyle.GetCornerRadiusLogical(_窗口圆角模式)
            Dim outerRadius As Single = Math.Max(1.0F, CSng(缩放逻辑尺寸(s.HostForm, logicalRadius)))
            outerRadius = Math.Min(outerRadius, Math.Min(w, h) / 2.0F)
            Dim stroke As Single = CSng(bdr)
            If stroke * 2.0F >= Math.Min(w, h) Then
                context.FillRoundedRectangle(New RectangleF(0, 0, w, h), outerRadius, bdrColor)
                Return
            End If
            Dim inset As Single = stroke / 2.0F
            Dim strokeRadius As Single = Math.Max(0.0F, outerRadius - inset)
            ' 细描边圆弧避开 DWM 裁切的抗锯齿带，直边位置和线宽保持不变。
            If stroke <= 1.0F AndAlso strokeRadius > 0.0F Then strokeRadius += 0.5F
            Dim rect As New RectangleF(inset, inset, w - stroke, h - stroke)
            context.StrokeRoundedRectangle(rect, strokeRadius, bdrColor, stroke)
            Return
        End If

        context.FillRectangle(New RectangleF(0, 0, w, Math.Min(bdr, h)), bdrColor)
        If h > bdr Then context.FillRectangle(New RectangleF(0, h - bdr, w, bdr), bdrColor)

        Dim sideH As Integer = h - bdr * 2
        If sideH <= 0 Then Return
        context.FillRectangle(New RectangleF(0, bdr, Math.Min(bdr, w), sideH), bdrColor)
        If w > bdr Then context.FillRectangle(New RectangleF(w - bdr, bdr, bdr, sideH), bdrColor)
    End Sub

    Private Sub 绘制标题栏底部横线_DX12(context As D3D12_PaintContext, s As PerFormState, captionRect As Rectangle)
        If context Is Nothing OrElse s Is Nothing OrElse captionRect.Width <= 0 OrElse captionRect.Height <= 0 Then Return

        Dim lineHeight As Integer = Math.Min(captionRect.Height, 取缩放标题栏底部横线高度(s.HostForm))
        If lineHeight <= 0 OrElse _标题栏底部横线颜色.A = 0 Then Return

        context.FillRectangle(New RectangleF(captionRect.Left,
                                             captionRect.Bottom - lineHeight,
                                             captionRect.Width,
                                             lineHeight),
                              _标题栏底部横线颜色)
    End Sub

    Private Sub 绘制标题文字_DX12(context As D3D12_PaintContext, s As PerFormState)
        Dim text As String = 获取标题栏渲染文本(s.HostForm)
        If String.IsNullOrEmpty(text) Then Return

        Dim font As Font = If(_标题文字字体, s.HostForm.Font)
        If font Is Nothing Then Return

        Dim fgColor As Color = If(视觉上保持激活(s.HostForm, s.Activated), _标题文字颜色, _标题文字失焦颜色)
        If fgColor.A = 0 Then Return

        Dim textRect As RectangleF = 获取标题文字布局矩形(s)
        If textRect.Width <= 0 OrElse textRect.Height <= 0 Then Return
        text = 获取省略标题文字(s, text, font, textRect.Width)
        If String.IsNullOrEmpty(text) Then Return

        Dim align = Vortice.DirectWrite.TextAlignment.Leading
        Select Case _标题文字对齐
            Case TitleAlignEnum.Center : align = Vortice.DirectWrite.TextAlignment.Center
            Case TitleAlignEnum.Right : align = Vortice.DirectWrite.TextAlignment.Trailing
        End Select
        context.DrawText(text, font, fgColor, textRect, 取Dpi缩放(s.HostForm), align, Vortice.DirectWrite.ParagraphAlignment.Center)
    End Sub

End Class
