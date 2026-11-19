Imports System.Numerics
Imports Vortice.DirectWrite
Imports DW = Vortice.DirectWrite
Partial Public Class ModernTabControl
    Implements ID3D12Renderable

    Partial Private Class 透明内容面板
        Implements ID3D12Renderable
        Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
            If _backgroundSource IsNot Nothing Then
                context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
            Else
                context.FillRectangle(ClientRectangle, BackColor)
            End If
        End Sub
    End Class

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        确保Owner()
        If Me.Width <= 0 OrElse Me.Height <= 0 Then Return

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), MyBase.BackColor)
        End If

        绘制图形内容_DX12(context)
        绘制文本内容_DX12(context)
    End Sub

    Private Sub 绘制图形内容_DX12(context As D3D12_PaintContext)
        Dim contentRect = 获取内容区域矩形()
        Dim stripRect = 获取标签栏矩形()

        If Not contentRect.IsEmpty AndAlso 内容区域背景颜色.A = 255 Then
            context.FillRectangle(contentRect, 内容区域背景颜色)
        End If
        If 标签栏背景颜色.A > 0 Then
            context.FillRectangle(stripRect, 标签栏背景颜色)
        End If

        绘制标签栏背景图片_DX12(context, stripRect)

        If 标签栏遮罩颜色.A > 0 Then
            context.FillRectangle(stripRect, 标签栏遮罩颜色)
        End If

        Using context.PushClip(stripRect)
            For i As Integer = 0 To 项目列表.Count - 1
                Dim item = 项目列表(i)
                If item.IsSeparator Then
                    绘制分割线_DX12(context, i)
                Else
                    绘制标签页项图形_DX12(context, i)
                End If
            Next

            If Ribbon模式值 Then
                绘制折叠按钮图形_DX12(context)
            End If
        End Using

        更新滚动条布局()
        If Not _滚动条TrackRect.IsEmpty Then
            绘制横向滚动条_DX12(context)
        End If

        If 内容区域边框宽度 > 0 AndAlso Not contentRect.IsEmpty Then
            Dim s As Single = DpiScale()
            Dim borderRect As RectangleF = contentRect
            borderRect.Width -= 1
            borderRect.Height -= 1
            context.StrokeRoundedRectangle(borderRect, 0.0F, 内容区域边框颜色, 内容区域边框宽度 * s)
        End If
    End Sub

    Private Sub 绘制标签栏背景图片_DX12(context As D3D12_PaintContext, tabStripRect As Rectangle)
        If 标签栏背景图片 Is Nothing Then Return
        Dim img As Image = 标签栏背景图片
        Dim cw As Integer = tabStripRect.Width
        Dim ch As Integer = tabStripRect.Height
        If cw < 1 OrElse ch < 1 Then Return

        Dim ratioW As Single = CSng(cw) / img.Width
        Dim ratioH As Single = CSng(ch) / img.Height
        Dim ratio As Single = Math.Max(ratioW, ratioH)
        Dim drawW As Single = img.Width * ratio
        Dim drawH As Single = img.Height * ratio
        Dim dx As Single = tabStripRect.X + (cw - drawW) / 2.0F
        Dim dy As Single = tabStripRect.Y + (ch - drawH) / 2.0F

        Using context.PushClip(tabStripRect)
            context.DrawImage(img, New RectangleF(dx, dy, drawW, drawH))
        End Using
    End Sub

    Private Sub 绘制分割线_DX12(context As D3D12_PaintContext, index As Integer)
        Dim bounds = 获取标签页项矩形(index)
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim lineX As Single = bounds.X + (bounds.Width - 1) / 2.0F
        context.FillRectangle(New RectangleF(lineX, bounds.Y, 1, bounds.Height), 分割线颜色值)
    End Sub

    Private Sub 绘制标签页项图形_DX12(context As D3D12_PaintContext, index As Integer)
        Dim s As Single = DpiScale()
        Dim bounds As RectangleF = 获取标签页项矩形(index)
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim isSelected As Boolean = (_selectedIndex = index)
        Dim hoverProgress As Single = 获取动画进度(index)

        Dim bgColor As Color
        If isSelected Then
            bgColor = 选中标签页背景颜色
        Else
            bgColor = 颜色插值(Color.FromArgb(0, 悬停标签页背景颜色), 悬停标签页背景颜色, hoverProgress)
        End If
        Dim radius As Single = 标签页圆角半径 * s
        If isSelected OrElse hoverProgress > 0.001F Then
            context.FillRoundedRectangle(bounds, radius, bgColor)
        End If

        If isSelected AndAlso 选中指示条高度 > 0 Then
            Dim indicatorH As Single = 选中指示条高度 * s
            Dim indicatorPad As Single = 选中指示条边距 * s
            Dim indicatorRadius As Single = 选中指示条圆角半径 * s
            Dim indicatorRect As RectangleF
            If 标签页位置 = TabPositionEnum.Top Then
                indicatorRect = New RectangleF(bounds.X + indicatorPad, bounds.Bottom - indicatorH, bounds.Width - indicatorPad * 2, indicatorH)
            Else
                indicatorRect = New RectangleF(bounds.X + indicatorPad, bounds.Y, bounds.Width - indicatorPad * 2, indicatorH)
            End If
            If indicatorRect.Width > 0 AndAlso indicatorRect.Height > 0 Then
                context.FillRoundedRectangle(indicatorRect, indicatorRadius, 选中指示条颜色)
            End If
        End If

        If isSelected AndAlso Me.Focused AndAlso 焦点边框颜色 <> Color.Empty Then
            Dim focusBounds = bounds
            focusBounds.Inflate(-1 * s, -1 * s)
            If focusBounds.Width > 0 AndAlso focusBounds.Height > 0 Then
                context.StrokeRoundedRectangle(focusBounds, If(radius > 0, Math.Max(1, radius - 1 * s), 0.0F), 焦点边框颜色, 1.0F * s)
            End If
        End If

        绘制标签页图标_DX12(context, index, bounds)
    End Sub

    Private Sub 绘制标签页图标_DX12(context As D3D12_PaintContext, index As Integer, bounds As RectangleF)
        If index >= 项目列表.Count Then Return
        Dim item = 项目列表(index)
        绘制图标_DX12(context, item.TabIcon, bounds)
    End Sub

    Private Sub 绘制图标_DX12(context As D3D12_PaintContext, img As Image, bounds As RectangleF)
        If img Is Nothing Then Return
        Dim s As Single = DpiScale()
        Dim iconSize As Single = 图标尺寸 * s
        Dim pad As Single = 标签页文本内边距 * s
        Dim iconRect As New RectangleF(bounds.X + pad, bounds.Y + (bounds.Height - iconSize) / 2.0F, iconSize, iconSize)
        context.DrawImage(img, iconRect)
    End Sub

    Private Sub 绘制折叠按钮图形_DX12(context As D3D12_PaintContext)
        Dim bounds = 获取折叠按钮矩形()
        If bounds.IsEmpty Then Return
        _折叠按钮缓存矩形 = bounds
        Dim s As Single = DpiScale()
        Dim bgColor As Color = 颜色插值(Color.FromArgb(0, 悬停标签页背景颜色), 悬停标签页背景颜色, _折叠按钮动画.当前值)
        If _折叠按钮动画.当前值 > 0.001F Then
            context.FillRoundedRectangle(bounds, 标签页圆角半径 * s, bgColor)
        End If
        绘制图标_DX12(context, 折叠按钮图标值, bounds)
    End Sub

    Private Sub 绘制横向滚动条_DX12(context As D3D12_PaintContext)
        If _滚动条TrackRect.IsEmpty Then Return
        Dim s As Single = DpiScale()
        Dim barH As Integer = CInt(滚动条高度 * s)
        If _滚动条TrackRect.Width < 1 OrElse barH < 1 Then Return

        Dim trackY As Integer = _滚动条TrackRect.Y + (_滚动条TrackRect.Height - barH) \ 2
        If 滚动条轨道颜色.A > 0 Then
            Dim trackRadius As Integer = Math.Min(barH \ 2, _滚动条TrackRect.Width \ 2)
            context.FillRoundedRectangle(New RectangleF(_滚动条TrackRect.X, trackY, _滚动条TrackRect.Width, barH), trackRadius, 滚动条轨道颜色)
        End If

        Dim activeColor As Color = If(_滚动条IsDragging OrElse _滚动条IsHover, 滚动条悬停颜色, 滚动条滑块颜色)
        Dim thumbY As Integer = _滚动条ThumbRect.Y + (_滚动条ThumbRect.Height - barH) \ 2
        Dim thumbRadius As Integer = Math.Min(barH \ 2, _滚动条ThumbRect.Width \ 2)
        context.FillRoundedRectangle(New RectangleF(_滚动条ThumbRect.X, thumbY, _滚动条ThumbRect.Width, barH), thumbRadius, activeColor)
    End Sub

    Private Sub 绘制文本内容_DX12(context As D3D12_PaintContext)
        Dim stripRect = 获取标签栏矩形()
        Using context.PushClip(stripRect)
            For i As Integer = 0 To 项目列表.Count - 1
                绘制标签页文本_DX12(context, i)
            Next
            If Ribbon模式值 Then
                绘制折叠按钮文本_DX12(context)
            End If
        End Using
    End Sub

    Private Sub 绘制标签页文本_DX12(context As D3D12_PaintContext, index As Integer)
        If index >= 项目列表.Count Then Return
        Dim bounds As RectangleF = 获取标签页项矩形(index)
        Dim item = 项目列表(index)
        If item.IsSeparator OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim isSelected As Boolean = (_selectedIndex = index)
        Dim textColor As Color = If(isSelected,
            If(item.SelectedForeColor <> Color.Empty, item.SelectedForeColor, 选中标签页文本颜色),
            If(item.NormalForeColor <> Color.Empty, item.NormalForeColor, 标签页默认文本颜色))
        Dim textFont As Font = If(item.TabFont, Me.Font)

        Dim s As Single = DpiScale()
        Dim pad As Single = 标签页文本内边距 * s
        Dim iconOffset As Single = If(item.TabIcon IsNot Nothing, 图标尺寸 * s + 图标与文本间距 * s, 0)
        Dim textRect As New RectangleF(bounds.X + pad + iconOffset, bounds.Y, bounds.Width - pad * 2 - iconOffset, bounds.Height)
        绘制单行居中文本_DX12(context, item.Text, textFont, textRect, textColor)
    End Sub

    Private Sub 绘制折叠按钮文本_DX12(context As D3D12_PaintContext)
        Dim bounds As RectangleF = 获取折叠按钮矩形()
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim caption As String = If(已折叠值, 折叠按钮展开文本值, 折叠按钮折叠文本值)
        If String.IsNullOrEmpty(caption) AndAlso 折叠按钮图标值 Is Nothing Then Return
        Dim textColor As Color = If(已折叠值,
            If(折叠按钮选中文本颜色值 <> Color.Empty, 折叠按钮选中文本颜色值, 选中标签页文本颜色),
            If(折叠按钮文本颜色值 <> Color.Empty, 折叠按钮文本颜色值, 标签页默认文本颜色))
        Dim textFont As Font = If(折叠按钮字体值, Me.Font)

        Dim s As Single = DpiScale()
        Dim pad As Single = 标签页文本内边距 * s
        Dim iconOffset As Single = If(折叠按钮图标值 IsNot Nothing, 图标尺寸 * s + 图标与文本间距 * s, 0)
        Dim textRect As New RectangleF(bounds.X + pad + iconOffset, bounds.Y, bounds.Width - pad * 2 - iconOffset, bounds.Height)
        绘制单行居中文本_DX12(context, caption, textFont, textRect, textColor)
    End Sub

    Private Sub 绘制单行居中文本_DX12(context As D3D12_PaintContext, text As String, font As Font, rect As RectangleF, color As Color)
        If String.IsNullOrEmpty(text) OrElse rect.Width <= 0 OrElse rect.Height <= 0 OrElse color.A = 0 Then Return
        context.DrawText(text, font, color, rect, DpiScale(), TextAlignment.Center, ParagraphAlignment.Center)
    End Sub
End Class
