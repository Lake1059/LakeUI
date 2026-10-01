Imports System.Drawing
Imports System.Numerics
Imports Vortice.Direct2D1

''' <summary>
''' ProgressRing 与 AgentDialogueList 共用的不确定进度动画绘制核心。
''' 同时提供 Win11 弧线和 Win10 五点追逐两种样式；该模块只接收当前帧上下文
''' 和纯绘制参数，不持有控件状态或动画计时器。
''' </summary>
Friend Module ProgressRingRenderer
    Friend Sub RenderWin11(context As D3D_PaintContext,
                    drawingArea As RectangleF,
                    color As Color,
                    strokeWidth As Single,
                    isAnimating As Boolean,
                    elapsedMilliseconds As Double,
                    designMode As Boolean,
                    animationDuration As Integer)
        If context Is Nothing OrElse drawingArea.Width <= 0.0F OrElse drawingArea.Height <= 0.0F OrElse color.A = 0 Then Return

        Dim centerX = drawingArea.X + drawingArea.Width / 2.0F
        Dim centerY = drawingArea.Y + drawingArea.Height / 2.0F
        Dim radius = Math.Min(drawingArea.Width, drawingArea.Height) / 2.0F - 1.0F
        Dim width = Math.Max(0.5F, strokeWidth)
        If width >= radius Then width = radius - 1.0F
        If width < 0.5F Then Return

        Dim arcRadius = radius - width / 2.0F
        If arcRadius < 1.0F Then Return
        Dim arcRect As New RectangleF(centerX - arcRadius, centerY - arcRadius,
                                      arcRadius * 2.0F, arcRadius * 2.0F)

        Const minArc As Single = 15.0F
        Const maxArc As Single = 260.0F
        Const baseRotation As Single = 535.0F
        Const sweepRange As Single = maxArc - minArc
        Const totalPerCycle As Single = baseRotation + sweepRange

        Dim n As Single
        Dim t As Single
        If designMode Then
            n = 0.0F
            t = 0.15F
        ElseIf Not isAnimating Then
            n = 0.0F
            t = 0.0F
        Else
            Dim duration = CDbl(Math.Max(100, animationDuration))
            Dim cycles = elapsedMilliseconds / duration
            Dim reduced = cycles Mod 6.0
            n = CSng(Math.Floor(reduced))
            t = CSng(reduced - Math.Floor(reduced))
        End If

        Dim sweepAngle As Single
        Dim sweepOffset As Single = 0.0F
        If t < 0.5F Then
            sweepAngle = minArc + sweepRange * EaseInOut(t * 2.0F)
        Else
            sweepAngle = maxArc - sweepRange * EaseInOut((t - 0.5F) * 2.0F)
            sweepOffset = maxArc - sweepAngle
        End If

        Dim startAngle = n * totalPerCycle + baseRotation * t + sweepOffset - 90.0F
        If sweepAngle <= 0.05F Then Return

        Using geometry = CreateArcGeometry(arcRect, startAngle, sweepAngle)
            If geometry Is Nothing Then Return
            Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext,
                                                                     color, context.DeviceGeneration)
            context.DeviceContext.DrawGeometry(geometry, brush, width, D3D_D2DInterop.GetRoundStrokeStyle())
        End Using
    End Sub

    Friend Sub RenderWin10(context As D3D_PaintContext,
                           drawingArea As RectangleF,
                           color As Color,
                           pointDiameter As Single,
                           isAnimating As Boolean,
                           elapsedMilliseconds As Double,
                           designMode As Boolean,
                           animationDuration As Integer)
        If context Is Nothing OrElse drawingArea.Width <= 0.0F OrElse drawingArea.Height <= 0.0F OrElse color.A = 0 Then Return

        Dim centerX = drawingArea.X + drawingArea.Width / 2.0F
        Dim centerY = drawingArea.Y + drawingArea.Height / 2.0F
        Dim radius = Math.Min(drawingArea.Width, drawingArea.Height) / 2.0F - 1.0F
        Dim diameter = Math.Max(1.0F, pointDiameter)
        If diameter >= radius Then diameter = radius - 1.0F
        If diameter < 1.0F Then Return
        Dim orbitRadius = radius - diameter / 2.0F
        If orbitRadius < 1.0F Then Return

        Dim t = GetAnimationProgress(isAnimating, elapsedMilliseconds, designMode, animationDuration)
        Const pointCount As Integer = 5
        Const phaseSpan As Double = 0.25
        Const amplitude As Double = 0.75
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext,
                                                                 color, context.DeviceGeneration)
        Dim pointRadius = diameter / 2.0F
        For i As Integer = 0 To pointCount - 1
            Dim phase = (t + CDbl(i) / (pointCount - 1) * phaseSpan) Mod 1.0
            Dim angle = (720.0 * phase - (180.0 * amplitude / Math.PI) * Math.Sin(4.0 * Math.PI * phase) - 90.0) * Math.PI / 180.0
            Dim pointX = centerX + CSng(Math.Cos(angle)) * orbitRadius
            Dim pointY = centerY + CSng(Math.Sin(angle)) * orbitRadius
            context.DeviceContext.FillEllipse(New Ellipse(New Vector2(pointX, pointY), pointRadius, pointRadius), brush)
        Next
    End Sub

    Private Function CreateArcGeometry(rect As RectangleF, startAngle As Single,
                                       sweepAngle As Single) As ID2D1PathGeometry
        Dim rx = rect.Width / 2.0F
        Dim ry = rect.Height / 2.0F
        Dim cx = rect.X + rx
        Dim cy = rect.Y + ry
        Dim startRadians = startAngle * Math.PI / 180.0
        Dim endRadians = (startAngle + sweepAngle) * Math.PI / 180.0
        Dim startPoint As New Vector2(cx + CSng(Math.Cos(startRadians)) * rx,
                                      cy + CSng(Math.Sin(startRadians)) * ry)
        Dim endPoint As New Vector2(cx + CSng(Math.Cos(endRadians)) * rx,
                                    cy + CSng(Math.Sin(endRadians)) * ry)

        Dim path = D3D_RenderCore.DeviceManager.D2DFactory.CreatePathGeometry()
        Dim sink = path.Open()
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

    Private Function EaseInOut(t As Single) As Single
        If t < 0.5F Then Return 4.0F * t * t * t
        Return 1.0F - CSng(Math.Pow(-2.0 * t + 2.0, 3) / 2.0)
    End Function

    Private Function GetAnimationProgress(isAnimating As Boolean,
                                          elapsedMilliseconds As Double,
                                          designMode As Boolean,
                                          animationDuration As Integer) As Double
        If designMode Then Return 0.15
        If Not isAnimating Then Return 0.0
        Dim duration = CDbl(Math.Max(100, animationDuration))
        Return (elapsedMilliseconds Mod duration) / duration
    End Function
End Module
