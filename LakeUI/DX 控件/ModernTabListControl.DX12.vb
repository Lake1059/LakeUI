Imports Vortice.DirectWrite

Partial Public Class ModernTabListControl
    Implements ID3D12Renderable

    Partial Private Class 透明内容面板
        Implements ID3D12Renderable

        Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
            If _backgroundSource IsNot Nothing Then
                context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
            ElseIf BackColor.A > 0 Then
                context.FillRectangle(ClientRectangle, BackColor)
            End If
        End Sub
    End Class

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        确保Owner()
        If Me.Width <= 0 OrElse Me.Height <= 0 Then Return

        Dim effectiveStripColor = 获取标签栏有效背景颜色()


        If _backgroundSource IsNot Nothing Then
            If Not context.DrawBackgroundSource(_backgroundSource, ClientRectangle) AndAlso
               MyBase.BackColor.A > 0 Then
                context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), MyBase.BackColor)
            End If
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), MyBase.BackColor)
        End If

        绘制图形内容_DX12(context)
        绘制文本与指示器符号_DX12(context)
        绘制标签栏滚动条_DX12(context)
    End Sub

    Private Sub 绘制图形内容_DX12(context As D3D12_PaintContext)
        If 内容区域背景颜色.A = 255 Then
            context.FillRectangle(获取内容区域矩形(), 内容区域背景颜色)
        End If

        Dim tabStripRect = 获取标签栏矩形()
        Dim effectiveStripColor = 获取标签栏有效背景颜色()
        If effectiveStripColor.A > 0 Then
            context.FillRectangle(tabStripRect, effectiveStripColor)
        End If

        绘制标签栏背景图片_DX12(context, tabStripRect)

        If 标签栏遮罩颜色.A > 0 Then
            context.FillRectangle(tabStripRect, 标签栏遮罩颜色)
        End If

        Dim tabItemClip As Rectangle = tabStripRect
        Dim searchAreaH As Integer = CInt(获取搜索框区域高度())
        If searchAreaH > 0 Then
            tabItemClip = New Rectangle(tabItemClip.X, tabItemClip.Y + searchAreaH, tabItemClip.Width, Math.Max(0, tabItemClip.Height - searchAreaH))
        End If

        Dim totalTabH As Integer = CInt(获取标签页总高度())
        Dim viewportTabH As Integer = CInt(获取可滚动视口高度())
        Dim hasMoreAbove As Boolean = _滚动偏移 > 0.5F
        Dim hasMoreBelow As Boolean = (totalTabH > viewportTabH) AndAlso (totalTabH - _滚动偏移 > viewportTabH + 0.5F)
        Dim scaledIndicatorH As Integer = CInt(更多指示器高度 * DpiScale())
        Dim topIndicatorH As Integer = If(hasMoreAbove, scaledIndicatorH, 0)
        Dim bottomIndicatorH As Integer = If(hasMoreBelow, scaledIndicatorH, 0)

        Dim clippedTabItemClip As Rectangle = tabItemClip
        If topIndicatorH > 0 Then
            clippedTabItemClip = New Rectangle(clippedTabItemClip.X, clippedTabItemClip.Y + topIndicatorH, clippedTabItemClip.Width, Math.Max(0, clippedTabItemClip.Height - topIndicatorH))
        End If
        If bottomIndicatorH > 0 Then
            clippedTabItemClip = New Rectangle(clippedTabItemClip.X, clippedTabItemClip.Y, clippedTabItemClip.Width, Math.Max(0, clippedTabItemClip.Height - bottomIndicatorH))
        End If

        If clippedTabItemClip.Width > 0 AndAlso clippedTabItemClip.Height > 0 Then
            Using context.PushClip(clippedTabItemClip)
                For i As Integer = 0 To 项目列表.Count - 1
                    If Not 项目是否可见(i) Then Continue For
                    Dim itemRect = 获取标签页项矩形(i)
                    If itemRect.Top >= clippedTabItemClip.Bottom Then Exit For
                    If itemRect.Bottom <= clippedTabItemClip.Top Then Continue For
                    Dim item = 项目列表(i)
                    If item.IsSeparator Then
                        绘制分割线_DX12(context, i)
                    ElseIf Not item.IsDescription Then
                        绘制标签页项图形_DX12(context, i)
                    End If
                Next
            End Using
        End If

        更新滚动条布局()

        If 内容区域边框宽度 > 0 Then
            Dim contentRect = 获取内容区域矩形()
            Dim r As New RectangleF(contentRect.X, contentRect.Y, contentRect.Width - 1, contentRect.Height - 1)
            绘制圆角边框_DX12(context, r, 0.0F, 内容区域边框颜色, 内容区域边框宽度 * DpiScale())
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
        ' Keep the DPI-scaled thickness, but align its origin to the physical pixel
        ' grid so fractional layout coordinates cannot antialias separators differently.
        Dim lineH As Single = Math.Max(1.0F, DpiScale())
        Dim lineY As Single = CSng(Math.Round(bounds.Y + (bounds.Height - lineH) / 2.0F))
        context.FillRectangle(New RectangleF(bounds.X, lineY, bounds.Width, lineH), 分割线颜色值)
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

        If (isSelected OrElse hoverProgress > 0.001F) AndAlso bgColor.A > 0 Then
            填充圆角或矩形_DX12(context, bounds, 标签页圆角半径 * s, bgColor)
        End If

        If isSelected AndAlso 选中指示条宽度 > 0 Then
            Dim indicatorRect As RectangleF
            If 标签页位置 = TabSideEnum.Left Then
                indicatorRect = New RectangleF(bounds.X, bounds.Y + 选中指示条边距 * s, 选中指示条宽度 * s, bounds.Height - 选中指示条边距 * s * 2)
            Else
                indicatorRect = New RectangleF(bounds.Right - 选中指示条宽度 * s, bounds.Y + 选中指示条边距 * s, 选中指示条宽度 * s, bounds.Height - 选中指示条边距 * s * 2)
            End If
            If indicatorRect.Width > 0 AndAlso indicatorRect.Height > 0 Then
                填充圆角或矩形_DX12(context, indicatorRect, 选中指示条圆角半径 * s, 选中指示条颜色)
            End If
        End If

        If isSelected AndAlso Me.Focused AndAlso 焦点边框颜色 <> Color.Empty Then
            Dim focusBounds = bounds
            focusBounds.Inflate(-s, -s)
            绘制圆角边框_DX12(context, focusBounds, If(标签页圆角半径 > 0, Math.Max(1, 标签页圆角半径 * s - s), 0.0F), 焦点边框颜色, s)
        End If

        绘制标签页图标_DX12(context, index, bounds)
    End Sub

    Private Sub 填充圆角或矩形_DX12(context As D3D12_PaintContext, rect As RectangleF, radius As Single, color As Color)
        context.FillRoundedRectangle(rect, radius, color)
    End Sub

    Private Sub 绘制圆角边框_DX12(context As D3D12_PaintContext, rect As RectangleF, radius As Single, color As Color, strokeWidth As Single)
        context.StrokeRoundedRectangle(rect, radius, color, strokeWidth)
    End Sub

    Private Sub 绘制标签页图标_DX12(context As D3D12_PaintContext, index As Integer, bounds As RectangleF)
        If index >= 项目列表.Count Then Return
        Dim item = 项目列表(index)
        If item.TabIcon Is Nothing Then Return

        Dim s As Single = DpiScale()
        Dim scaledIconSize As Single = 图标尺寸 * s
        If scaledIconSize <= 0 OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim iconX As Single = bounds.X + 标签页文本左边距 * s
        Dim iconY As Single = bounds.Y + (bounds.Height - scaledIconSize) / 2.0F
        context.DrawImage(item.TabIcon, New RectangleF(iconX, iconY, scaledIconSize, scaledIconSize))
    End Sub

    Private Sub 绘制文本与指示器符号_DX12(context As D3D12_PaintContext)
        Dim tabStripRect = 获取标签栏矩形()
        Dim tabItemClip As Rectangle = tabStripRect
        Dim searchAreaH As Integer = CInt(获取搜索框区域高度())
        If searchAreaH > 0 Then
            tabItemClip = New Rectangle(tabItemClip.X, tabItemClip.Y + searchAreaH, tabItemClip.Width, Math.Max(0, tabItemClip.Height - searchAreaH))
        End If

        Dim totalTabH As Integer = CInt(获取标签页总高度())
        Dim viewportTabH As Integer = CInt(获取可滚动视口高度())
        Dim hasMoreAbove As Boolean = _滚动偏移 > 0.5F
        Dim hasMoreBelow As Boolean = (totalTabH > viewportTabH) AndAlso (totalTabH - _滚动偏移 > viewportTabH + 0.5F)
        Dim scaledIndicatorH As Integer = CInt(更多指示器高度 * DpiScale())
        Dim topIndicatorH As Integer = If(hasMoreAbove, scaledIndicatorH, 0)
        Dim bottomIndicatorH As Integer = If(hasMoreBelow, scaledIndicatorH, 0)
        Dim clippedTabItemClip As Rectangle = tabItemClip
        If topIndicatorH > 0 Then
            clippedTabItemClip = New Rectangle(clippedTabItemClip.X, clippedTabItemClip.Y + topIndicatorH, clippedTabItemClip.Width, Math.Max(0, clippedTabItemClip.Height - topIndicatorH))
        End If
        If bottomIndicatorH > 0 Then
            clippedTabItemClip = New Rectangle(clippedTabItemClip.X, clippedTabItemClip.Y, clippedTabItemClip.Width, Math.Max(0, clippedTabItemClip.Height - bottomIndicatorH))
        End If

        If clippedTabItemClip.Width > 0 AndAlso clippedTabItemClip.Height > 0 Then
            Using context.PushClip(clippedTabItemClip)
                For i As Integer = 0 To 项目列表.Count - 1
                    If Not 项目是否可见(i) Then Continue For
                    Dim itemRect = 获取标签页项矩形(i)
                    If itemRect.Top >= clippedTabItemClip.Bottom Then Exit For
                    If itemRect.Bottom <= clippedTabItemClip.Top Then Continue For
                    绘制标签页文本_DX12(context, i)
                Next
            End Using
        End If

        If hasMoreAbove Then
            绘制更多指示器符号_DX12(context, New RectangleF(tabItemClip.X, tabItemClip.Y, tabItemClip.Width, topIndicatorH), True)
        End If
        If hasMoreBelow Then
            绘制更多指示器符号_DX12(context, New RectangleF(tabItemClip.X, tabItemClip.Bottom - bottomIndicatorH, tabItemClip.Width, bottomIndicatorH), False)
        End If
    End Sub

    Private Sub 绘制标签页文本_DX12(context As D3D12_PaintContext, index As Integer)
        If index >= 项目列表.Count Then Return
        Dim item = 项目列表(index)
        If item.IsSeparator Then Return

        Dim s As Single = DpiScale()
        Dim bounds As RectangleF = 获取标签页项矩形(index)
        Dim textPad As Single = 标签页文本左边距 * s

        If item.IsDescription Then
            Dim descFont As Font = If(item.TabFont, 说明字体值)
            Dim descColor As Color = If(item.NormalForeColor <> Color.Empty, item.NormalForeColor, 说明文本颜色值)
            Dim r As New RectangleF(bounds.X + textPad, bounds.Y, bounds.Width - textPad * 2, bounds.Height)
            画文本_DX12(context, item.Text, descFont, r, descColor, TextAlignment.Leading)
            Return
        End If

        Dim isSelected As Boolean = (_selectedIndex = index)
        Dim textColor As Color = If(isSelected,
            If(item.SelectedForeColor <> Color.Empty, item.SelectedForeColor, 选中标签页文本颜色),
            If(item.NormalForeColor <> Color.Empty, item.NormalForeColor, 标签页默认文本颜色))
        Dim textFont As Font = If(item.TabFont, Me.Font)
        Dim iconOffset As Single = If(item.TabIcon IsNot Nothing, (图标尺寸 + 图标与文本间距) * s, 0.0F)
        Dim r2 As New RectangleF(bounds.X + textPad + iconOffset, bounds.Y, bounds.Width - textPad * 2 - iconOffset, bounds.Height)
        画文本_DX12(context, item.Text, textFont, r2, textColor, TextAlignment.Leading)
    End Sub

    Private Sub 绘制更多指示器符号_DX12(context As D3D12_PaintContext, rect As RectangleF, isTop As Boolean)
        If rect.Height < 2 Then Return
        Dim symbol As String = If(isTop, "▲", "▼")
        画文本_DX12(context, symbol, 获取更多指示器字体(), rect, 更多指示器颜色, TextAlignment.Center)
    End Sub

    Private Sub 画文本_DX12(context As D3D12_PaintContext, text As String, font As Font, rect As RectangleF, color As Color, hAlign As TextAlignment)
        If String.IsNullOrEmpty(text) OrElse font Is Nothing OrElse rect.Width <= 0 OrElse rect.Height <= 0 OrElse color.A = 0 Then Return
        context.DrawText(text, font, color, rect, DpiScale(), hAlign, ParagraphAlignment.Center)
    End Sub

    Private Sub 绘制标签栏滚动条_DX12(context As D3D12_PaintContext)
        If _标签栏滚动条.TrackRect.IsEmpty Then Return
        Dim width As Single = Math.Max(1.0F, CInt(滚动条宽度 * DpiScale()))
        Dim trackArea As New RectangleF(_标签栏滚动条.VisualLeft, _标签栏滚动条.TrackRect.Y, width, _标签栏滚动条.TrackRect.Height)
        Dim thumbArea As New RectangleF(_标签栏滚动条.VisualLeft, _标签栏滚动条.ThumbRect.Y, width, _标签栏滚动条.ThumbRect.Height)
        填充圆角或矩形_DX12(context, trackArea, Math.Min(width / 2.0F, trackArea.Height / 2.0F), 滚动条轨道颜色)
        Dim activeColor As Color = If(_标签栏滚动条.IsDragging OrElse _标签栏滚动条.IsHover, 滚动条悬停颜色, 滚动条滑块颜色)
        填充圆角或矩形_DX12(context, thumbArea, Math.Min(width / 2.0F, thumbArea.Height / 2.0F), activeColor)
    End Sub
End Class
