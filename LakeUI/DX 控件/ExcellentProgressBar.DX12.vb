Partial Public Class ExcellentProgressBar
    Implements ID3D12Renderable
    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse Me.Width < 1 OrElse Me.Height < 1 Then Return

        Dim scale As Single = DpiScale()
        Dim p As Padding = Me.Padding
        Dim sourceRect As New RectangleF(0, 0, Me.Width, Me.Height)
        Dim bounds As New RectangleF(0, 0, Me.Width, Me.Height)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * scale / 2.0F
            bounds.Inflate(-half, -half)
        End If
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return

        Dim content As New RectangleF(
            bounds.X + p.Left,
            bounds.Y + p.Top,
            bounds.Width - p.Horizontal,
            bounds.Height - p.Vertical)

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, Rectangle.Round(sourceRect))
        ElseIf BackColor1.A > 0 Then
            context.FillShape(bounds, If(边框圆角半径 > 0, 边框圆角半径 * scale, 0.0F), BackColor1)
        End If
        绘制图形内容_DX12(context, bounds, content)

        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            context.FillShape(bounds, If(边框圆角半径 > 0, 边框圆角半径 * scale, 0.0F), 禁用时遮罩颜色)
        End If

        If Not String.IsNullOrEmpty(Me.Text) Then
            绘制文字_DX12(context)
        End If
    End Sub

    Private Sub 绘制文字_DX12(context As D3D12_PaintContext)
        Dim p As Padding = 文字边距
        Dim textRect As New RectangleF(p.Left, p.Top, Math.Max(0, Me.Width - p.Horizontal), Math.Max(0, Me.Height - p.Vertical))
        If textRect.Width < 1 OrElse textRect.Height < 1 Then Return
        Dim flags As TextFormatFlags = TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis
        Select Case TextAlign
            Case ContentAlignment.TopLeft
                flags = flags Or TextFormatFlags.Left
            Case ContentAlignment.TopCenter
                flags = flags Or TextFormatFlags.HorizontalCenter
            Case ContentAlignment.TopRight
                flags = flags Or TextFormatFlags.Right
            Case ContentAlignment.MiddleLeft
                flags = flags Or TextFormatFlags.Left Or TextFormatFlags.VerticalCenter
            Case ContentAlignment.MiddleCenter
                flags = flags Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter
            Case ContentAlignment.MiddleRight
                flags = flags Or TextFormatFlags.Right Or TextFormatFlags.VerticalCenter
            Case ContentAlignment.BottomLeft
                flags = flags Or TextFormatFlags.Left Or TextFormatFlags.Bottom
            Case ContentAlignment.BottomCenter
                flags = flags Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.Bottom
            Case ContentAlignment.BottomRight
                flags = flags Or TextFormatFlags.Right Or TextFormatFlags.Bottom
            Case Else
                flags = flags Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter
        End Select
        Dim horizontal = If(flags.HasFlag(TextFormatFlags.HorizontalCenter), Vortice.DirectWrite.TextAlignment.Center,
                            If(flags.HasFlag(TextFormatFlags.Right), Vortice.DirectWrite.TextAlignment.Trailing, Vortice.DirectWrite.TextAlignment.Leading))
        Dim vertical = If(flags.HasFlag(TextFormatFlags.VerticalCenter), Vortice.DirectWrite.ParagraphAlignment.Center,
                          If(flags.HasFlag(TextFormatFlags.Bottom), Vortice.DirectWrite.ParagraphAlignment.Far, Vortice.DirectWrite.ParagraphAlignment.Near))
        context.DrawText(Me.Text, Me.Font, Me.ForeColor, textRect, DpiScale(), horizontal, vertical)
    End Sub

    Private Sub 绘制图形内容_DX12(context As D3D12_PaintContext, bounds As RectangleF, content As RectangleF)
        Dim scale As Single = DpiScale()
        Dim radius As Single = 边框圆角半径 * scale
        Dim borderWidth As Single = 边框宽度 * scale

        Dim fillRadius = 计算内容区圆角半径(bounds, content, radius)
        context.FillShape(bounds, radius, 轨道背景颜色, 轨道渐变颜色, 轨道渐变方向, reverse:=轨道渐变方向 = System.Windows.Forms.Orientation.Vertical)
        绘制双填充区域_DX12(context, content, fillRadius)
        绘制刻度线_DX12(context, content, fillRadius)
        If 边框颜色.A > 0 AndAlso borderWidth > 0 Then
            context.StrokeRoundedRectangle(bounds, radius, 边框颜色, borderWidth)
        End If
    End Sub

    Private Sub 绘制刻度线_DX12(context As D3D12_PaintContext, content As RectangleF, clipRadius As Single)
        If MarkerColor.A = 0 OrElse MarkerWidth <= 0 OrElse MarkerValue < Minimum OrElse MarkerValue > Maximum Then Return
        If content.Width <= 0 OrElse content.Height <= 0 Then Return

        Dim thickness As Single = MarkerWidth * DpiScale()
        If thickness <= 0 Then Return

        Dim ratio As Single = 计算值比例(MarkerValue)
        Dim markerRect As RectangleF
        If 方向 = BarOrientationEnum.Horizontal Then
            Dim centerX As Single = content.X + content.Width * ratio
            Dim left As Single = Math.Max(content.Left, centerX - thickness / 2.0F)
            Dim right As Single = Math.Min(content.Right, centerX + thickness / 2.0F)
            markerRect = New RectangleF(left, content.Top, Math.Max(0, right - left), content.Height)
        Else
            Dim centerY As Single = content.Bottom - content.Height * ratio
            Dim top As Single = Math.Max(content.Top, centerY - thickness / 2.0F)
            Dim bottom As Single = Math.Min(content.Bottom, centerY + thickness / 2.0F)
            markerRect = New RectangleF(content.Left, top, content.Width, Math.Max(0, bottom - top))
        End If
        If markerRect.Width <= 0 OrElse markerRect.Height <= 0 Then Return

        context.FillShape(markerRect, 0, MarkerColor, maskRadius:=clipRadius, mask:=content)
    End Sub

    Private Sub 绘制双填充区域_DX12(context As D3D12_PaintContext, content As RectangleF, clipRadius As Single)
        Dim progress1 As Single = 动画助手.Progress
        Dim progress2 As Single = 动画助手2.Progress

        If progress1 >= progress2 Then
            绘制单个填充区域_DX12(context, content, clipRadius, progress1, 填充基础颜色, 填充渐变颜色, 填充渐变方向, 渐变模式)
            绘制单个填充区域_DX12(context, content, clipRadius, progress2, 填充基础颜色2, 填充渐变颜色2, 填充渐变方向2, 渐变模式2)
        Else
            绘制单个填充区域_DX12(context, content, clipRadius, progress2, 填充基础颜色2, 填充渐变颜色2, 填充渐变方向2, 渐变模式2)
            绘制单个填充区域_DX12(context, content, clipRadius, progress1, 填充基础颜色, 填充渐变颜色, 填充渐变方向, 渐变模式)
        End If
    End Sub

    Private Sub 绘制单个填充区域_DX12(context As D3D12_PaintContext,
                                  content As RectangleF,
                                  clipRadius As Single,
                                  progress As Single,
                                  baseColor As Color,
                                  gradColor As Color,
                                  gradDir As Orientation,
                                  gradMode As FillGradientModeEnum)
        If progress < 0.001F Then Return

        Dim fillRect As RectangleF
        If 方向 = BarOrientationEnum.Horizontal Then
            fillRect = New RectangleF(content.X, content.Y, content.Width * progress, content.Height)
        Else
            Dim fillHeight As Single = content.Height * progress
            fillRect = New RectangleF(content.X, content.Bottom - fillHeight, content.Width, fillHeight)
        End If
        If fillRect.Width < 1 OrElse fillRect.Height < 1 Then Return

        Dim gradientBounds As RectangleF = If(gradMode = FillGradientModeEnum.WithinProgress, fillRect, content)
        context.FillShape(fillRect, 0, baseColor, gradColor, gradDir, gradDir = System.Windows.Forms.Orientation.Vertical, gradientBounds, clipRadius, content)
    End Sub
End Class
