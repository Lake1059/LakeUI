Imports System.Numerics
Imports Vortice.DirectWrite
Imports DW = Vortice.DirectWrite
Partial Public Class ExcellentTrackBar
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If context Is Nothing OrElse Me.Width <= 0 OrElse Me.Height <= 0 Then Return
        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), MyBase.BackColor)
        End If

        Dim thumbRect As RectangleF = 计算滑块矩形()
        绘制轨道_DX12(context)
        绘制标签连线_DX12(context)
        绘制章节标记_DX12(context)
        绘制滑块_DX12(context, thumbRect)
        绘制标签文字_DX12(context)
        绘制滑块文字_DX12(context, thumbRect)

        If Not Enabled Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), Color.FromArgb(128, BackColor))
        End If
    End Sub

    Private Sub 绘制轨道_DX12(context As D3D12_PaintContext)
        Dim s As Single = DpiScale()
        Dim radius As Single = 轨道圆角半径 * s
        Dim borderWidth As Single = 轨道边框宽度 * s
        Dim trackRect As RectangleF = 计算轨道区域()
        If trackRect.Width <= 0 OrElse trackRect.Height <= 0 Then Return

        context.FillShape(trackRect, radius, 轨道颜色)
        context.StrokeRoundedRectangle(trackRect, radius, 轨道边框颜色, borderWidth)

        Dim center As Single = 计算滑块中心坐标()
        Dim fillRect As RectangleF
        If 方向 = TrackOrientationEnum.Horizontal Then
            Dim fillW As Single = center - trackRect.X
            If fillW <= 0 Then Return
            fillRect = New RectangleF(trackRect.X, trackRect.Y, fillW, trackRect.Height)
        Else
            Dim fillH As Single = trackRect.Bottom - center
            If fillH <= 0 Then Return
            fillRect = New RectangleF(trackRect.X, center, trackRect.Width, fillH)
        End If

        context.FillShape(fillRect, 0, 轨道填充颜色, maskRadius:=radius, mask:=trackRect)
    End Sub

    Private Sub 绘制滑块_DX12(context As D3D12_PaintContext, thumbRect As RectangleF)
        Dim s As Single = DpiScale()
        Dim radius As Single = 滑块圆角半径 * s
        Dim borderWidth As Single = 滑块边框宽度 * s
        context.FillShape(thumbRect, radius, 获取当前滑块颜色(), 滑块渐变颜色, System.Windows.Forms.Orientation.Vertical)
        context.StrokeRoundedRectangle(thumbRect, radius, 获取当前滑块边框颜色(), borderWidth)
    End Sub

    Private Sub 绘制章节标记_DX12(context As D3D12_PaintContext)
        If 章节标记颜色.A = 0 OrElse 章节标记尺寸.Width = 0 OrElse 章节标记尺寸.Height = 0 Then Return
        For Each marker In 章节标记列表
            If Double.IsNaN(marker.Position) OrElse marker.Position < 最小值 OrElse marker.Position > 最大值 Then Continue For
            Dim rect As RectangleF = 计算章节标记区域(marker)
            Dim vertices(2) As Vector2
            If 方向 = TrackOrientationEnum.Horizontal Then
                Dim tipY As Single = If(marker.Side = LabelSideEnum.TopOrLeft, rect.Bottom, rect.Top)
                Dim baseY As Single = If(marker.Side = LabelSideEnum.TopOrLeft, rect.Top, rect.Bottom)
                vertices(0) = New Vector2(rect.Left, baseY)
                vertices(1) = New Vector2(rect.Right, baseY)
                vertices(2) = New Vector2(rect.Left + rect.Width / 2.0F, tipY)
            Else
                Dim tipX As Single = If(marker.Side = LabelSideEnum.TopOrLeft, rect.Right, rect.Left)
                Dim baseX As Single = If(marker.Side = LabelSideEnum.TopOrLeft, rect.Left, rect.Right)
                vertices(0) = New Vector2(baseX, rect.Top)
                vertices(1) = New Vector2(baseX, rect.Bottom)
                vertices(2) = New Vector2(tipX, rect.Top + rect.Height / 2.0F)
            End If

            context.FillRoundedTriangle(vertices, Math.Min(rect.Width, rect.Height) * 章节标记圆角强度 / 2, 章节标记颜色)
        Next
    End Sub

    Private Sub 绘制标签连线_DX12(context As D3D12_PaintContext)
        If 标签列表.Count = 0 OrElse 标签连线颜色.A = 0 Then Return
        Dim lineLength As Single = 标签连线长度 * DpiScale()
        Dim trackRect As RectangleF = 计算轨道区域()
        For Each lbl In 标签列表
            If lbl.Position < 最小值 OrElse lbl.Position > 最大值 Then Continue For
            Dim coord As Single = 计算值对应轨道坐标(lbl.Position)
            If 方向 = TrackOrientationEnum.Horizontal Then
                If lbl.Side = LabelSideEnum.TopOrLeft Then
                    context.DrawLine(New System.Numerics.Vector2(coord, trackRect.Y - 2), New System.Numerics.Vector2(coord, trackRect.Y - 2 - lineLength), 标签连线颜色, 1.0F)
                Else
                    context.DrawLine(New System.Numerics.Vector2(coord, trackRect.Bottom + 2), New System.Numerics.Vector2(coord, trackRect.Bottom + 2 + lineLength), 标签连线颜色, 1.0F)
                End If
            Else
                If lbl.Side = LabelSideEnum.TopOrLeft Then
                    context.DrawLine(New System.Numerics.Vector2(trackRect.X - 2, coord), New System.Numerics.Vector2(trackRect.X - 2 - lineLength, coord), 标签连线颜色, 1.0F)
                Else
                    context.DrawLine(New System.Numerics.Vector2(trackRect.Right + 2, coord), New System.Numerics.Vector2(trackRect.Right + 2 + lineLength, coord), 标签连线颜色, 1.0F)
                End If
            End If
        Next
    End Sub

    Private Sub 绘制标签文字_DX12(context As D3D12_PaintContext)
        If 标签列表.Count = 0 Then Return
        Dim lineLength As Single = 标签连线长度 * DpiScale()
        Dim trackRect As RectangleF = 计算轨道区域()
        Dim labelFont As Font = 获取标签字体()
        For Each lbl In 标签列表
            If lbl.Position < 最小值 OrElse lbl.Position > 最大值 Then Continue For
            Dim displayText As String = 获取标签显示文字(lbl)
            If String.IsNullOrEmpty(displayText) Then Continue For
            Dim textSize As Size = TextRenderer.MeasureText(displayText, labelFont)
            Dim coord As Single = 计算值对应轨道坐标(lbl.Position)
            Dim textRect As RectangleF
            If 方向 = TrackOrientationEnum.Horizontal Then
                Dim textX As Single = coord - textSize.Width / 2.0F
                If lbl.Side = LabelSideEnum.TopOrLeft Then
                    textRect = New RectangleF(textX, trackRect.Y - 2 - lineLength - textSize.Height, textSize.Width, textSize.Height)
                Else
                    textRect = New RectangleF(textX, trackRect.Bottom + 2 + lineLength, textSize.Width, textSize.Height)
                End If
            Else
                Dim textY As Single = coord - textSize.Height / 2.0F
                If lbl.Side = LabelSideEnum.TopOrLeft Then
                    textRect = New RectangleF(trackRect.X - 2 - lineLength - textSize.Width, textY, textSize.Width, textSize.Height)
                Else
                    textRect = New RectangleF(trackRect.Right + 2 + lineLength, textY, textSize.Width, textSize.Height)
                End If
            End If
            If textRect.Width > 0 AndAlso textRect.Height > 0 Then
                context.DrawText(displayText, labelFont, 标签颜色, textRect, DpiScale(), DW.TextAlignment.Center, DW.ParagraphAlignment.Center)
            End If
        Next
    End Sub

    Private Sub 绘制滑块文字_DX12(context As D3D12_PaintContext, thumbRect As RectangleF)
        If 滑块文字模式 = ThumbTextModeEnum.None Then Return
        Dim displayText As String = 获取滑块显示文字()
        If String.IsNullOrEmpty(displayText) Then Return
        context.DrawText(displayText, Me.Font, 滑块文字颜色, thumbRect, DpiScale(), DW.TextAlignment.Center, DW.ParagraphAlignment.Center)
    End Sub
End Class
