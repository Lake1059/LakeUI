Imports System.Numerics
Imports Vortice.DirectWrite

Partial Public Class AgentRoom
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        Dim renderStarted = Diagnostics.Stopwatch.GetTimestamp()
        Dim layoutStarted = Diagnostics.Stopwatch.GetTimestamp()
        EnsureLayout()
        _maxLayoutMilliseconds = Math.Max(_maxLayoutMilliseconds, ElapsedMilliseconds(layoutStarted))
        If Me.Width < 1 OrElse Me.Height < 1 Then Return

        Dim borderSize As Integer = Dpi(_borderSize)
        Dim borderRadius As Integer = Dpi(_borderRadius)
        Dim scrollBarWidth As Integer = Dpi(_scrollBarWidth)
        Dim scaledPadding As Padding = Dpi(Me.Padding)
        Dim bgRect As RectangleF = GetCenteredStrokeRect(New RectangleF(0, 0, Width, Height), borderSize)

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
        End If

        Dim backColorMask As Color = MyBase.BackColor
        If _backgroundSource Is Nothing AndAlso backColorMask.A > 0 AndAlso backColorMask.A < 255 Then
            context.FillRoundedRectangle(bgRect, borderRadius, backColorMask)
        End If
        If _backColor1.A > 0 Then context.FillRoundedRectangle(bgRect, borderRadius, _backColor1)

        Dim area As Rectangle = GetContentArea()
        Dim font As Font = Me.Font
        Dim lineHeight As Integer = GetLineHeight(font)
        Dim firstVisible As Integer
        Dim lastVisible As Integer
        GetVisibleItemRange(area, firstVisible, lastVisible)

        Using context.PushClip(New RectangleF(area.Left, area.Top, area.Width, area.Height))
            For i As Integer = firstVisible To lastVisible
                Dim it = _items(i)
                If Not it.IsVisible OrElse it.CachedRect.Height <= 0 Then Continue For
                Dim r As Rectangle = GetItemViewportRect(i, area)
                If Not context.IntersectsDirty(r) Then Continue For
                Dim shapeStarted = Diagnostics.Stopwatch.GetTimestamp()
                DrawItemShapes_DX12(context, it, r, font, lineHeight, i)
                _maxShapeDrawMilliseconds = Math.Max(_maxShapeDrawMilliseconds, ElapsedMilliseconds(shapeStarted))
                If ShouldUseMarkdown(it) Then
                    Dim markdownStarted = Diagnostics.Stopwatch.GetTimestamp()
                    DrawMarkdownItem_DX12(context, it, r, area)
                    _maxMarkdownDrawMilliseconds = Math.Max(_maxMarkdownDrawMilliseconds, ElapsedMilliseconds(markdownStarted))
                End If
                DrawItemText_DX12(context, it, r, font, lineHeight)
            Next
        End Using

        Dim viewH As Integer = area.Height
        Dim totalH As Integer = Math.Max(viewH, _contentHeight)
        If _scrollBarVisible AndAlso scrollBarWidth > 0 Then
            _scrollBar.ComputeLayout(Width, Height, borderSize, borderRadius,
                                     scaledPadding.Top, scaledPadding.Bottom,
                                     scrollBarWidth, totalH, viewH, _滚动偏移)
            DrawScrollBar_DX12(context, scrollBarWidth)
        Else
            _scrollBar.TrackRect = Rectangle.Empty
            _scrollBar.ThumbRect = Rectangle.Empty
        End If

        If borderSize > 0 Then context.StrokeRoundedRectangle(bgRect, borderRadius, _borderColor, borderSize)
        _lastRenderMilliseconds = ElapsedMilliseconds(renderStarted)
        _maxRenderMilliseconds = Math.Max(_maxRenderMilliseconds, _lastRenderMilliseconds)
    End Sub

    Private Sub DrawItemShapes_DX12(context As D3D12_PaintContext,
                                   it As ChatItem,
                                   areaItemRect As Rectangle,
                                   font As Font,
                                   lineHeight As Integer,
                                   itemIndex As Integer)
        If Not it.IsVisible Then Return
        If it.Kind = ChatItemKind.TurnHeader Then
            DrawDisclosureGlyph_DX12(context, areaItemRect, it.HeaderRect, it.IsExpanded, If(it.IsError, _agentErrorColor, _turnHeaderForeColor))
            Return
        End If

        If it.Kind = ChatItemKind.ToolCall Then
            Dim blockAbs As New RectangleF(areaItemRect.X + it.BubbleRect.X,
                                           areaItemRect.Y + it.BubbleRect.Y,
                                           it.BubbleRect.Width,
                                           it.BubbleRect.Height)
            context.FillRoundedRectangle(blockAbs, Dpi(6), _toolCallBackColor)
            context.StrokeRoundedRectangle(GetCenteredStrokeRect(blockAbs, 1.0F), Dpi(6), If(it.IsError, _agentErrorColor, _toolCallBorderColor), 1.0F)
            If it.IsExpanded AndAlso Not String.IsNullOrWhiteSpace(it.Text) Then
                Dim separatorY = areaItemRect.Y + it.HeaderRect.Bottom
                Dim brush = _toolCallBorderColor
                context.DrawLine(New Vector2(areaItemRect.X + it.BubbleRect.Left + Dpi(10), separatorY),
                                               New Vector2(areaItemRect.X + it.BubbleRect.Right - Dpi(10), separatorY),
                                               brush, 1.0F)
                If it.FooterRect.Height > 0 Then
                    Dim footerSeparatorY = areaItemRect.Y + it.FooterRect.Top
                    context.DrawLine(New Vector2(areaItemRect.X + it.BubbleRect.Left + Dpi(10), footerSeparatorY),
                                                   New Vector2(areaItemRect.X + it.BubbleRect.Right - Dpi(10), footerSeparatorY),
                                                   brush, 1.0F)
                End If
            End If
            DrawDisclosureGlyph_DX12(context, areaItemRect, it.HeaderRect, it.IsExpanded, If(it.IsError, _agentErrorColor, _toolCallForeColor))
            DrawSelectionForItem_DX12(context, it, itemIndex, areaItemRect, font, lineHeight)
            DrawToolActionButtons_DX12(context, it, areaItemRect, font)
            If it.DetailScrollBarVisible Then DrawItemScrollBar_DX12(context, it, areaItemRect)
            Return
        End If

        Dim isCard As Boolean = (it.Kind = ChatItemKind.Card)
        Dim bubbleAbs As New Rectangle(areaItemRect.X + it.BubbleRect.X,
                                        areaItemRect.Y + it.BubbleRect.Y,
                                        it.BubbleRect.Width, it.BubbleRect.Height)
        Dim backColor As Color
        If isCard Then
            backColor = _cardBackColor
        ElseIf it.Kind = ChatItemKind.UserMessage Then
            backColor = _userBubbleBackColor
        Else
            backColor = _assistantBubbleBackColor
        End If
        Dim radius As Integer = If(isCard, Dpi(_cardRadius), Dpi(_bubbleRadius))
        Dim borderSize As Integer = If(isCard, Dpi(_cardBorderSize), 0)
        Dim rectF As RectangleF = GetCenteredStrokeRect(
            New RectangleF(bubbleAbs.X, bubbleAbs.Y, bubbleAbs.Width, bubbleAbs.Height),
            If(isCard, CSng(borderSize), 0.0F))

        If it.Kind = ChatItemKind.UserMessage OrElse isCard Then context.FillRoundedRectangle(rectF, radius, backColor)
        If isCard AndAlso borderSize > 0 Then context.StrokeRoundedRectangle(rectF, radius, _cardBorderColor, borderSize)
        DrawSelectionForItem_DX12(context, it, itemIndex, areaItemRect, font, lineHeight)
    End Sub

    Private Sub DrawDisclosureGlyph_DX12(context As D3D12_PaintContext,
                                        areaItemRect As Rectangle,
                                        localHeaderRect As Rectangle,
                                        expanded As Boolean,
                                        color As Color)
        Dim cx = areaItemRect.X + localHeaderRect.Left + localHeaderRect.Height \ 2
        Dim cy = areaItemRect.Y + localHeaderRect.Top + localHeaderRect.Height \ 2
        Dim size = Math.Max(3, Dpi(5))
        Dim brush = color
        Dim strokeWidth = Math.Max(1.0F, Dpi(2.0F))
        DrawChevronLines_DX12(context, cx, cy, size, expanded, brush, color, strokeWidth)
    End Sub

    Private Sub DrawToolActionButtons_DX12(context As D3D12_PaintContext,
                                          it As ChatItem,
                                          areaItemRect As Rectangle,
                                          font As Font)
        Dim color = If(it.IsError, _agentErrorColor, _toolCallForeColor)
        DrawCopyTextButton_DX12(context,
                               OffsetRectangle(it.HeaderCopyButtonRect, areaItemRect.Location),
                               color,
                               Object.ReferenceEquals(_hoverToolCopyItem, it) AndAlso Not _hoverToolCopyIsFooter,
                               font)
        DrawToggleGlyph_DX12(context, OffsetRectangle(it.HeaderToggleButtonRect, areaItemRect.Location), it.IsExpanded, color)
        If it.IsExpanded Then
            DrawCopyTextButton_DX12(context,
                                   OffsetRectangle(it.FooterCopyButtonRect, areaItemRect.Location),
                                   color,
                                   Object.ReferenceEquals(_hoverToolCopyItem, it) AndAlso _hoverToolCopyIsFooter,
                                   font)
            DrawToggleGlyph_DX12(context, OffsetRectangle(it.FooterToggleButtonRect, areaItemRect.Location), True, color)
        End If
    End Sub

    Private Sub DrawCopyTextButton_DX12(context As D3D12_PaintContext,
                                       bounds As Rectangle,
                                       color As Color,
                                       isHovered As Boolean,
                                       font As Font)
        If bounds.IsEmpty Then Return
        If isHovered AndAlso _toolCallCopyHoverBackColor.A > 0 Then
            Dim radius = Math.Min(Dpi(_toolCallCopyCornerRadius), Math.Min(bounds.Width, bounds.Height) \ 2)
            context.FillRoundedRectangle(New RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), radius, _toolCallCopyHoverBackColor)
        End If

        Dim padding = Dpi(_toolCallCopyPadding)
        Dim textRect As New RectangleF(bounds.X + padding.Left,
                                       bounds.Y + padding.Top,
                                       Math.Max(0, bounds.Width - padding.Horizontal),
                                       Math.Max(0, bounds.Height - padding.Vertical))
        context.DrawText(If(_toolCallCopyText, ""), font, color, textRect, DpiScale(),
                         Vortice.DirectWrite.TextAlignment.Trailing,
                         Vortice.DirectWrite.ParagraphAlignment.Center)
    End Sub

    Private Sub DrawToggleGlyph_DX12(context As D3D12_PaintContext, bounds As Rectangle, expanded As Boolean, color As Color)
        If bounds.IsEmpty Then Return
        Dim cx = bounds.Left + bounds.Width \ 2
        Dim cy = bounds.Top + bounds.Height \ 2
        Dim size = Math.Max(3, Dpi(5))
        Dim stroke = Math.Max(1.0F, Dpi(2.0F))
        Dim brush = color
        DrawChevronLines_DX12(context, cx, cy, size, expanded, brush, color, stroke)
    End Sub

    Private Shared Sub DrawChevronLines_DX12(context As D3D12_PaintContext,
                                            cx As Integer,
                                            cy As Integer,
                                            size As Integer,
                                            expanded As Boolean,
                                            brush As Color,
                                            color As Color,
                                            strokeWidth As Single)
        Dim firstPoint As Vector2
        Dim vertex As Vector2
        Dim lastPoint As Vector2
        If expanded Then
            firstPoint = New Vector2(cx - size, cy - size \ 2)
            vertex = New Vector2(cx, cy + size \ 2)
            lastPoint = New Vector2(cx + size, cy - size \ 2)
        Else
            firstPoint = New Vector2(cx - size \ 2, cy - size)
            vertex = New Vector2(cx + size \ 2, cy)
            lastPoint = New Vector2(cx - size \ 2, cy + size)
        End If

        context.DrawLine(firstPoint, vertex, brush, strokeWidth)
        context.DrawLine(vertex, lastPoint, brush, strokeWidth)

        ' Direct2D 分别栅格化两条线时，公共端点可能留下一个缺口；用方形连接点补齐直角。
        Dim jointSize = Math.Max(1.0F, strokeWidth)
        context.FillRectangle(New RectangleF(vertex.X - jointSize / 2.0F,
                                             vertex.Y - jointSize / 2.0F,
                                             jointSize,
                                             jointSize), color)
    End Sub

    Private Sub DrawSelectionForItem_DX12(context As D3D12_PaintContext,
                                         it As ChatItem,
                                         itemIndex As Integer,
                                         areaItemRect As Rectangle,
                                         font As Font,
                                         lineHeight As Integer)
        If ShouldUseMarkdown(it) OrElse Not _hasSelection Then Return
        Dim rangeStart, rangeEnd As TextPos
        GetNormalizedSelection(rangeStart, rangeEnd)
        If itemIndex < rangeStart.ItemIndex OrElse itemIndex > rangeEnd.ItemIndex Then Return
        Dim selStart As Integer = If(itemIndex = rangeStart.ItemIndex, rangeStart.CharIndex, 0)
        Dim selEnd As Integer = If(itemIndex = rangeEnd.ItemIndex, rangeEnd.CharIndex, it.Text.Length)
        If selEnd <= selStart Then Return

        Dim textX As Integer = areaItemRect.X + it.TextOriginX
        Dim textY As Integer = areaItemRect.Y + it.TextOriginY - If(it.Kind = ChatItemKind.ToolCall, it.DetailScrollOffset, 0)
        If context.DirtyRectangle.Width > 0 AndAlso context.DirtyRectangle.Height > 0 Then
            Dim textWidth As Integer = Math.Max(1, it.BubbleRect.Right - (it.TextOriginX - it.BubbleRect.Left))
            Dim textHeight As Integer = Math.Max(lineHeight, it.LineRanges.Count * lineHeight)
            If Not context.IntersectsDirty(New Rectangle(textX, textY, textWidth, textHeight)) Then Return
        End If
        If it.Kind = ChatItemKind.ToolCall Then
            Dim clipRect = OffsetRectangle(it.DetailViewportRect, areaItemRect.Location)
            If clipRect.IsEmpty Then Return
            Using context.PushClip(New RectangleF(clipRect.X, clipRect.Y, clipRect.Width, clipRect.Height))
                DrawSelectionLines_DX12(context, it, font, lineHeight, textX, textY, selStart, selEnd, clipRect)
            End Using
            Return
        End If
        DrawSelectionLines_DX12(context, it, font, lineHeight, textX, textY, selStart, selEnd, Rectangle.Empty)
    End Sub

    Private Sub DrawSelectionLines_DX12(context As D3D12_PaintContext,
                                       it As ChatItem,
                                       font As Font,
                                       lineHeight As Integer,
                                       textX As Integer,
                                       textY As Integer,
                                       selStart As Integer,
                                       selEnd As Integer,
                                       clipRect As Rectangle)
        For li = 0 To it.LineRanges.Count - 1
            Dim lineY = textY + li * lineHeight
            If Not clipRect.IsEmpty AndAlso (lineY + lineHeight < clipRect.Top OrElse lineY > clipRect.Bottom) Then Continue For
            If context.DirtyRectangle.Width > 0 AndAlso context.DirtyRectangle.Height > 0 AndAlso
               (lineY + lineHeight < context.DirtyRectangle.Top OrElse lineY > context.DirtyRectangle.Bottom) Then Continue For
            Dim lr = it.LineRanges(li)
            Dim lineS As Integer = lr.Start, lineE As Integer = lr.Start + lr.Length
            Dim a As Integer = Math.Max(selStart, lineS)
            Dim b As Integer = Math.Min(selEnd, lineE)
            If b <= a Then Continue For
            Dim line As String = it.Text.Substring(lr.Start, lr.Length)
            Dim preW As Integer = MeasureTextWidthCached(line.Substring(0, a - lineS), font, lineHeight)
            Dim segW As Integer = MeasureTextWidthCached(line.Substring(a - lineS, b - a), font, lineHeight)
            context.FillRectangle(New RectangleF(textX + preW, lineY, segW, lineHeight), _selectionBackColor)
        Next
    End Sub

    Private Sub DrawItemText_DX12(context As D3D12_PaintContext,
                                 it As ChatItem,
                                 areaItemRect As Rectangle,
                                 font As Font,
                                 lineHeight As Integer)
        If Not it.IsVisible Then Return
        If it.Kind = ChatItemKind.TurnHeader OrElse it.Kind = ChatItemKind.ToolCall Then
            Dim header = it.HeaderRect
            Dim titleColor = If(it.IsError, _agentErrorColor, If(it.Kind = ChatItemKind.TurnHeader, _turnHeaderForeColor, _toolCallForeColor))
            Dim titleLeft = header.Left + header.Height
            Dim toolActionLeft = If(it.HeaderCopyButtonRect.IsEmpty,
                                    it.HeaderToggleButtonRect.Left,
                                    it.HeaderCopyButtonRect.Left)
            Dim rightReserve = If(it.Kind = ChatItemKind.ToolCall,
                                  header.Right - toolActionLeft + Dpi(4),
                                  Dpi(8))
            Dim titleRect As New RectangleF(areaItemRect.X + titleLeft,
                                            areaItemRect.Y + header.Top,
                                            Math.Max(0, header.Right - titleLeft - rightReserve),
                                            header.Height)
            context.DrawText(If(it.Title, ""), font, titleColor, titleRect, DpiScale(),
                             Vortice.DirectWrite.TextAlignment.Leading,
                             Vortice.DirectWrite.ParagraphAlignment.Center)
            If it.Kind = ChatItemKind.TurnHeader OrElse Not it.IsExpanded Then Return
        End If
        If ShouldUseMarkdown(it) Then Return
        Dim isCard As Boolean = (it.Kind = ChatItemKind.Card)
        Dim foreColor As Color
        If isCard Then
            foreColor = _cardForeColor
        ElseIf it.Kind = ChatItemKind.UserMessage Then
            foreColor = _userBubbleForeColor
        ElseIf it.Kind = ChatItemKind.AssistantActivity Then
            foreColor = _activityForeColor
        ElseIf it.Kind = ChatItemKind.ToolCall Then
            foreColor = _toolCallDetailForeColor
        Else
            foreColor = _assistantBubbleForeColor
        End If
        Dim textX As Integer = areaItemRect.X + it.TextOriginX
        Dim textY As Integer = areaItemRect.Y + it.TextOriginY - If(it.Kind = ChatItemKind.ToolCall, it.DetailScrollOffset, 0)
        If it.Kind = ChatItemKind.ToolCall Then
            Dim clipRect = OffsetRectangle(it.DetailViewportRect, areaItemRect.Location)
            If clipRect.IsEmpty Then Return
            Using context.PushClip(New RectangleF(clipRect.X, clipRect.Y, clipRect.Width, clipRect.Height))
                DrawPlainTextLines_DX12(context, it, textX, textY, font, lineHeight, foreColor, clipRect)
            End Using
            Return
        End If
        DrawPlainTextLines_DX12(context, it, textX, textY, font, lineHeight, foreColor, Rectangle.Empty)
    End Sub

    Private Sub DrawPlainTextLines_DX12(context As D3D12_PaintContext,
                                       it As ChatItem,
                                       textX As Integer,
                                       textY As Integer,
                                       font As Font,
                                       lineHeight As Integer,
                                       foreColor As Color,
                                       clipRect As Rectangle)
        Dim dirty = context.DirtyRectangle
        Dim textWidth As Integer = Math.Max(1, it.BubbleRect.Right - (it.TextOriginX - it.BubbleRect.Left))
        Dim textHeight As Integer = Math.Max(lineHeight, it.LineRanges.Count * lineHeight)
        If dirty.Width > 0 AndAlso dirty.Height > 0 AndAlso
           Not context.IntersectsDirty(New Rectangle(textX, textY, textWidth, textHeight)) Then Return

        For li = 0 To it.LineRanges.Count - 1
            Dim lineY = textY + li * lineHeight
            If Not clipRect.IsEmpty AndAlso (lineY + lineHeight < clipRect.Top OrElse lineY > clipRect.Bottom) Then Continue For
            If dirty.Width > 0 AndAlso dirty.Height > 0 AndAlso
               (lineY + lineHeight < dirty.Top OrElse lineY > dirty.Bottom) Then Continue For
            Dim lr = it.LineRanges(li)
            Dim line As String = it.Text.Substring(lr.Start, lr.Length)
            DrawLineWithLinks_DX12(context, it, line, lr.Start, textX, lineY, font, lineHeight, foreColor)
        Next
    End Sub

    Private Sub DrawMarkdownItem_DX12(context As D3D12_PaintContext,
                                     it As ChatItem,
                                     areaItemRect As Rectangle,
                                     viewportRect As Rectangle)
        Dim renderer = it.MarkdownRenderer
        If renderer Is Nothing OrElse renderer.IsDisposed Then Return

        Dim origin As New Point(areaItemRect.X + it.TextOriginX, areaItemRect.Y + it.TextOriginY)
        Dim visibleLocalTop As Integer = Math.Max(0, viewportRect.Top - origin.Y)
        Dim visibleLocalBottom As Integer = Math.Min(renderer.ContentHeight, viewportRect.Bottom - origin.Y)
        If visibleLocalBottom <= visibleLocalTop Then Return

        Dim contentRect As New Rectangle(origin.X, origin.Y, Math.Max(1, renderer.Width), Math.Max(1, renderer.ContentHeight))
        If Not context.IntersectsDirty(contentRect) Then Return

        If context.DirtyRectangle.Width > 0 AndAlso context.DirtyRectangle.Height > 0 Then
            visibleLocalTop = Math.Max(visibleLocalTop, context.DirtyRectangle.Top - origin.Y)
            visibleLocalBottom = Math.Min(visibleLocalBottom, context.DirtyRectangle.Bottom - origin.Y)
            If visibleLocalBottom <= visibleLocalTop Then Return
        End If

        Dim clipSize As New Size(Math.Max(1, renderer.Width), Math.Max(1, renderer.ContentHeight))
        renderer.DrawEmbeddedContent_DX12(context, origin, clipSize, drawBackground:=False,
                                         visibleLocalTop:=visibleLocalTop,
                                         visibleLocalBottom:=visibleLocalBottom)
    End Sub

    Private Sub DrawLineWithLinks_DX12(context As D3D12_PaintContext,
                                      it As ChatItem,
                                      line As String,
                                      lineStartCharIndex As Integer,
                                      x As Integer,
                                      y As Integer,
                                      font As Font,
                                      lineHeight As Integer,
                                      normalColor As Color)
        If String.IsNullOrEmpty(line) Then Return
        Dim curX As Integer = x
        Dim lineEnd As Integer = lineStartCharIndex + line.Length
        Dim p As Integer = lineStartCharIndex
        While p < lineEnd
            Dim link = FindLinkAt(it, p)
            If link.HasValue AndAlso link.Value.Start < lineEnd Then
                If link.Value.Start > p Then
                    DrawTextSegment_DX12(context, line, p - lineStartCharIndex, link.Value.Start - p,
                                        Nothing, curX, y, font, lineHeight, normalColor)
                End If
                Dim segE As Integer = Math.Min(lineEnd, link.Value.Start + link.Value.Length)
                Dim segStart As Integer = Math.Max(p, link.Value.Start)
                DrawTextSegment_DX12(context, line, segStart - lineStartCharIndex, segE - segStart,
                                    link.Value.Url, curX, y, font, lineHeight, normalColor)
                p = segE
            Else
                DrawTextSegment_DX12(context, line, p - lineStartCharIndex, lineEnd - p,
                                    Nothing, curX, y, font, lineHeight, normalColor)
                p = lineEnd
            End If
        End While
    End Sub

    Private Sub DrawTextSegment_DX12(context As D3D12_PaintContext,
                                    line As String,
                                    startIndex As Integer,
                                    length As Integer,
                                    url As String,
                                    ByRef curX As Integer,
                                    y As Integer,
                                    font As Font,
                                    lineHeight As Integer,
                                    normalColor As Color)
        If length <= 0 Then Return
        Dim part As String = line.Substring(startIndex, length)
        Dim w As Integer = MeasureTextWidthCached(part, font, lineHeight)
        If url IsNot Nothing Then
            Dim col As Color = If(url = _hoverLinkUrl, _linkHoverColor, _linkColor)
            context.DrawText(part, font, col, New RectangleF(curX, y, w, lineHeight), DpiScale(), Vortice.DirectWrite.TextAlignment.Leading, Vortice.DirectWrite.ParagraphAlignment.Center)
            If _linkUnderline Then
                Dim br = col
                Dim underlineY As Integer = y + lineHeight - Dpi(_linkUnderlineOffset)
                context.DrawLine(New Vector2(curX, underlineY),
                                               New Vector2(curX + w, underlineY),
                                               br,
                                               Math.Max(1.0F, Dpi(_linkUnderlineThickness)))
            End If
        Else
            context.DrawText(part, font, normalColor, New RectangleF(curX, y, w, lineHeight), DpiScale(), Vortice.DirectWrite.TextAlignment.Leading, Vortice.DirectWrite.ParagraphAlignment.Center)
        End If
        curX += w
    End Sub

    Private Sub DrawScrollBar_DX12(context As D3D12_PaintContext, scrollBarWidth As Integer)
        DrawScrollBarRenderer_DX12(context, _scrollBar, scrollBarWidth, Point.Empty)
    End Sub

    Private Sub DrawItemScrollBar_DX12(context As D3D12_PaintContext, it As ChatItem, areaItemRect As Rectangle)
        DrawScrollBarRenderer_DX12(context, it.DetailScrollBar, Dpi(_scrollBarWidth), areaItemRect.Location)
    End Sub

    Private Sub DrawScrollBarRenderer_DX12(context As D3D12_PaintContext,
                                          renderer As D3D_ScrollBarRenderer,
                                          scrollBarWidth As Integer,
                                          offset As Point)
        If renderer Is Nothing OrElse renderer.TrackRect.IsEmpty Then Return
        Dim width As Single = Math.Max(1.0F, scrollBarWidth)
        Dim trackArea As New RectangleF(offset.X + renderer.VisualLeft, offset.Y + renderer.TrackRect.Y, width, renderer.TrackRect.Height)
        Dim thumbArea As New RectangleF(offset.X + renderer.VisualLeft, offset.Y + renderer.ThumbRect.Y, width, renderer.ThumbRect.Height)
        context.FillRoundedRectangle(trackArea, Math.Min(width / 2.0F, trackArea.Height / 2.0F), _scrollBarTrackColor)
        Dim thumbColor = If(renderer.IsDragging OrElse renderer.IsHover, _scrollBarThumbHoverColor, _scrollBarThumbColor)
        context.FillRoundedRectangle(thumbArea, Math.Min(width / 2.0F, thumbArea.Height / 2.0F), thumbColor)
    End Sub
End Class
