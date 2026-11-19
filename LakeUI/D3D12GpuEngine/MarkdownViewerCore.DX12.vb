Imports System.Numerics

Partial Public Class MarkdownViewerCore
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        Dim w As Integer = ClientRectangle.Width
        Dim h As Integer = ClientRectangle.Height
        If w <= 0 OrElse h <= 0 Then Return
        EnsureLayoutDpiCurrent()
        Dim s As Single = DpiScale()

        Dim boundsRect As New RectangleF(0, 0, w, h)
        If 边框宽度 > 0 Then boundsRect.Inflate(-边框宽度 * s / 2.0F, -边框宽度 * s / 2.0F)

        If _embeddedContentMode Then
            If 背景颜色.A > 0 Then context.FillRectangle(New RectangleF(0, 0, w, h), 背景颜色)
        Else
            If _backgroundSource IsNot Nothing Then
                context.DrawBackgroundSource(_backgroundSource, ClientRectangle)
            End If
            DrawBackground_DX12(context, boundsRect, s)
        End If

        DrawMarkdownContent_DX12(context, Point.Empty, New Size(w, h), _scrollY, drawBackground:=False)
        If Not _embeddedContentMode Then DrawScrollBar_DX12(context, w, h, s)
    End Sub

    Friend Sub DrawEmbeddedContent_DX12(context As D3D12_PaintContext,
                                       origin As Point,
                                       clipSize As Size,
                                       Optional drawBackground As Boolean = False,
                                       Optional visibleLocalTop As Integer = -1,
                                       Optional visibleLocalBottom As Integer = -1)
        If context Is Nothing OrElse clipSize.Width <= 0 OrElse clipSize.Height <= 0 Then Return
        EnsureLayoutDpiCurrent()
        DrawMarkdownContent_DX12(context, origin, clipSize, _scrollY, drawBackground, visibleLocalTop, visibleLocalBottom)
    End Sub

    Private Sub DrawMarkdownContent_DX12(context As D3D12_PaintContext,
                                        origin As Point,
                                        clipSize As Size,
                                        scrollY As Integer,
                                        Optional drawBackground As Boolean = False,
                                        Optional visibleLocalTop As Integer = -1,
                                        Optional visibleLocalBottom As Integer = -1)
        If clipSize.Width <= 0 OrElse clipSize.Height <= 0 Then Return

        If drawBackground AndAlso 背景颜色.A > 0 Then
            context.FillRectangle(New RectangleF(origin.X, origin.Y, clipSize.Width, clipSize.Height), 背景颜色)
        End If

        Dim s As Single = DpiScale()
        Dim ci = GetContentInsets()
        Dim clipW As Integer = Math.Max(0, Math.Min(TextAreaWidth(), clipSize.Width - ci.Left - ci.Right))
        Dim clipH As Integer = Math.Max(0, clipSize.Height - ci.Top - ci.Bottom)
        If clipW <= 0 OrElse clipH <= 0 Then Return

        Dim localClipTop As Integer = ci.Top
        Dim localClipBottom As Integer = ci.Top + clipH
        If visibleLocalTop >= 0 Then localClipTop = Math.Max(localClipTop, visibleLocalTop)
        If visibleLocalBottom >= 0 Then localClipBottom = Math.Min(localClipBottom, visibleLocalBottom)
        If localClipBottom <= localClipTop Then Return

        Dim clipLeft As Single = origin.X + ci.Left
        Dim clipTop As Single = origin.Y + localClipTop
        Dim clipBottom As Single = origin.Y + localClipBottom

        Dim drawWatch = Diagnostics.Stopwatch.StartNew()
        Dim drawnLines As Integer = 0
        Dim drawnFragments As Integer = 0
        Using context.PushClip(New RectangleF(clipLeft, clipTop, clipW, clipBottom - clipTop))
            Dim selStart, selEnd As SelectionPos
            If _hasSelection Then GetOrderedSelection(selStart, selEnd)
            Dim lastMermaidBlockIndex As Integer = -1

            Dim visibleTop As Integer = Math.Max(0, scrollY + localClipTop - ci.Top)
            For vli As Integer = GetFirstVisibleVisualLine(visibleTop) To _visualLines.Count - 1
                Dim vl = _visualLines(vli)
                Dim drawY As Integer = origin.Y + vl.Y - scrollY + ci.Top
                If drawY + vl.Height < clipTop Then Continue For
                If drawY > clipBottom Then Exit For
                Dim block = _document.Blocks(vl.BlockIndex)
                If Not block.IsMermaidSequenceDiagram OrElse lastMermaidBlockIndex <> vl.BlockIndex Then
                    DrawBlockDecoration_DX12(context, block, vl, vli, CInt(clipLeft), drawY, clipW, s)
                    If block.IsMermaidSequenceDiagram Then lastMermaidBlockIndex = vl.BlockIndex
                End If
                DrawFragments_DX12(context, vl, vli, CInt(clipLeft), drawY, block.Kind, s, selStart, selEnd)
                drawnLines += 1
                drawnFragments += vl.Fragments.Count
            Next
        End Using
        drawWatch.Stop()
        _lastDrawnVisualLineCount = drawnLines
        _lastDrawnFragmentCount = drawnFragments
        _lastDrawMilliseconds = drawWatch.Elapsed.TotalMilliseconds
        _maxDrawMilliseconds = Math.Max(_maxDrawMilliseconds, _lastDrawMilliseconds)
    End Sub

    Private Sub DrawBackground_DX12(context As D3D12_PaintContext, boundsRect As RectangleF, s As Single)
        If 背景颜色.A > 0 Then context.FillRoundedRectangle(boundsRect, 边框圆角半径 * s, 背景颜色)
        If 边框宽度 > 0 AndAlso 边框颜色.A > 0 Then context.StrokeRoundedRectangle(boundsRect, 边框圆角半径 * s, 边框颜色, 边框宽度 * s)
    End Sub

    Private Sub DrawBlockDecoration_DX12(context As D3D12_PaintContext,
                                        block As MarkdownBlock,
                                        vl As VisualLine,
                                        vli As Integer,
                                        textLeft As Integer,
                                        drawY As Integer,
                                        clipW As Integer,
                                        s As Single)
        Select Case block.Kind
            Case BlockKind.HorizontalRule
                Dim ruleThick As Integer = Math.Max(1, CInt(分隔线粗细 * s))
                Dim ruleY As Integer = drawY + (vl.Height - ruleThick) \ 2
                Dim ruleInset As Integer = CInt(分隔线水平缩进 * s)
                context.FillRectangle(New RectangleF(textLeft + ruleInset, ruleY, Math.Max(0, clipW - ruleInset * 2), ruleThick), 分隔线颜色)

            Case BlockKind.BlockQuote
                Dim barH As Integer = vl.Height
                If vli + 1 < _visualLines.Count Then
                    Dim nextVl = _visualLines(vli + 1)
                    Dim nextBlock = _document.Blocks(nextVl.BlockIndex)
                    If nextBlock.Kind = BlockKind.BlockQuote Then barH = nextVl.Y - vl.Y
                End If
                Dim barColor As Color = If(block.AlertKind <> AlertKind.None, GetAlertColor(block.AlertKind), 引用条颜色)
                context.FillRectangle(New RectangleF(textLeft + CInt(引用条偏移 * s), drawY, Math.Max(1, CInt(引用条宽度 * s)), barH), barColor)

            Case BlockKind.CodeBlock
                If block.IsMermaidSequenceDiagram Then
                    DrawMermaidSequenceDiagram_DX12(context, block, vl, vli, textLeft, drawY, clipW, s)
                    Return
                End If
                Dim padT As Integer = CInt(代码块内边距.Top * s)
                Dim padB As Integer = CInt(代码块内边距.Bottom * s)
                Dim isFirstCodeLine = (vli = 0 OrElse _visualLines(vli - 1).BlockIndex <> vl.BlockIndex)
                Dim isLastCodeLine = (vli = _visualLines.Count - 1 OrElse _visualLines(vli + 1).BlockIndex <> vl.BlockIndex)
                Dim bgY = drawY - If(isFirstCodeLine, padT, 0)
                Dim bgH = vl.Height + If(isFirstCodeLine, padT, 0) + If(isLastCodeLine, padB, 0)
                context.FillRectangle(New RectangleF(textLeft, bgY, clipW, bgH), 代码块背景颜色)

            Case BlockKind.UnorderedListItem
                If vli > 0 AndAlso _visualLines(vli - 1).BlockIndex = vl.BlockIndex Then Return
                Dim unorderedMarkerIndent As Integer = GetListMarkerIndent(block, s)
                Dim bulletR As Integer = Math.Max(1, CInt(列表圆点半径 * s))
                context.FillEllipse(New RectangleF(textLeft + unorderedMarkerIndent + CInt(列表圆点偏移X * s),
                    drawY + vl.Height \ 2 + CInt(列表圆点偏移Y * s), bulletR * 2, bulletR * 2), 文本颜色)

            Case BlockKind.OrderedListItem
                If vli > 0 AndAlso _visualLines(vli - 1).BlockIndex = vl.BlockIndex Then Return
                Dim orderedMarkerIndent As Integer = GetListMarkerIndent(block, s)
                DrawText_DX12(context, block.OrderIndex.ToString() & ".", GetBlockFont(block.Kind), 文本颜色,
                    New RectangleF(textLeft + orderedMarkerIndent, drawY, Math.Max(1, CInt(有序列表标记宽度 * s)), vl.Height), D2DTextAlign.Right, True)

            Case BlockKind.Table
                DrawTableDecoration_DX12(context, block, vl, vli, textLeft, drawY, s)

            Case BlockKind.Heading1, BlockKind.Heading2
                If 标题分隔线粗细 > 0 Then
                    Dim isLastLineOfBlock As Boolean =
                        (vli + 1 >= _visualLines.Count OrElse _visualLines(vli + 1).BlockIndex <> vl.BlockIndex)
                    If isLastLineOfBlock Then
                        Dim sepThick As Integer = Math.Max(1, CInt(标题分隔线粗细 * s))
                        Dim sepGap As Integer = CInt(标题分隔线间距 * s)
                        Dim sepY As Integer = drawY + vl.Height + sepGap
                        context.FillRectangle(New RectangleF(textLeft, sepY, clipW, sepThick), 标题分隔线颜色)
                    End If
                End If
        End Select
    End Sub

    Private Sub DrawTableDecoration_DX12(context As D3D12_PaintContext,
                                        block As MarkdownBlock,
                                        vl As VisualLine,
                                        vli As Integer,
                                        textLeft As Integer,
                                        drawY As Integer,
                                        s As Single)
        If vl.TableSubLine <> 0 Then Return
        Dim colWidths As Integer() = Nothing
        If Not _tableColumnWidths.TryGetValue(vl.BlockIndex, colWidths) Then Return
        Dim colCount As Integer = colWidths.Length
        Dim totalW As Integer = colWidths.Sum()

        Dim cellPadding As Integer = CInt(表格单元格内边距 * s)
        Dim logicalRowH As Integer = vl.Height
        Dim look As Integer = vli + 1
        While look < _visualLines.Count
            Dim nextVl = _visualLines(look)
            If nextVl.BlockIndex <> vl.BlockIndex OrElse nextVl.TableRowIndex <> vl.TableRowIndex Then Exit While
            logicalRowH += nextVl.Height
            look += 1
        End While
        logicalRowH += cellPadding

        Dim topPad As Integer = cellPadding \ 2
        Dim rowTop As Integer = drawY - topPad

        If vl.TableRowIndex = 0 AndAlso block.HasHeader Then
            context.FillRectangle(New RectangleF(textLeft, rowTop, totalW, logicalRowH), 表头背景颜色)
        End If

        Dim lineBrush = 表格边框颜色
        Dim stroke As Single = Math.Max(1.0F, 表格边框粗细 * s)
        context.DrawLine(New Vector2(textLeft, rowTop), New Vector2(textLeft + totalW, rowTop), lineBrush, stroke)
        context.DrawLine(New Vector2(textLeft, rowTop + logicalRowH), New Vector2(textLeft + totalW, rowTop + logicalRowH), lineBrush, stroke)
        Dim x As Integer = textLeft
        For ci As Integer = 0 To colCount
            context.DrawLine(New Vector2(x, rowTop), New Vector2(x, rowTop + logicalRowH), lineBrush, stroke)
            If ci < colCount Then x += colWidths(ci)
        Next
    End Sub

    Private Sub DrawFragments_DX12(context As D3D12_PaintContext,
                                  vl As VisualLine,
                                  vli As Integer,
                                  textLeft As Integer,
                                  drawY As Integer,
                                  blockKind As BlockKind,
                                  s As Single,
                                  selStart As SelectionPos,
                                  selEnd As SelectionPos)
        For fi As Integer = 0 To vl.Fragments.Count - 1
            Dim frag = vl.Fragments(fi)
            Dim fragX As Integer = textLeft + frag.X
            Dim fragW As Integer = frag.Width

            If frag.SelectionOnly Then Continue For

            If frag.Kind = InlineKind.Image Then
                If _hasSelection Then DrawFragmentSelection_DX12(context, vli, fi, frag, fragX, drawY, vl.Height, selStart, selEnd)
                Dim imgH As Integer = If(frag.FragmentHeight > 0, frag.FragmentHeight, vl.Height)
                Dim imgRect As New RectangleF(fragX, drawY, frag.Width, imgH)
                If frag.ImageObj IsNot Nothing Then
                    context.DrawImmutableImage(frag.ImageObj, imgRect)
                Else
                    context.DrawRectangle(imgRect, 图片占位边框颜色, Math.Max(1.0F, s))
                    If Not String.IsNullOrEmpty(frag.Text) Then
                        DrawText_DX12(context, frag.Text, frag.UseFont, 图片占位文字颜色, imgRect, D2DTextAlign.Center, True)
                    End If
                End If
                Continue For
            End If

            If frag.BackColor <> Color.Empty AndAlso blockKind <> BlockKind.CodeBlock Then
                Dim padL As Integer = CInt(行内代码内边距.Left * s)
                Dim padR As Integer = CInt(行内代码内边距.Right * s)
                context.FillRoundedRectangle(New RectangleF(fragX - padL, drawY, fragW + padL + padR, vl.Height), CInt(行内代码圆角 * s), frag.BackColor)
            End If

            If _hasSelection Then DrawFragmentSelection_DX12(context, vli, fi, frag, fragX, drawY, vl.Height, selStart, selEnd)

            If Not String.IsNullOrEmpty(frag.Text) Then
                If blockKind = BlockKind.Table AndAlso frag.TableColIndex >= 0 Then
                    Dim block = _document.Blocks(vl.BlockIndex)
                    Dim colWidths As Integer() = Nothing
                    Dim cellW As Integer = frag.Width
                    If _tableColumnWidths.TryGetValue(vl.BlockIndex, colWidths) AndAlso frag.TableColIndex < colWidths.Length Then
                        Dim cellPadding As Integer = CInt(表格单元格内边距 * s)
                        cellW = colWidths(frag.TableColIndex) - cellPadding * 2
                    End If
                    Dim align As D2DTextAlign = D2DTextAlign.Left
                    If block.ColumnAlignments IsNot Nothing AndAlso frag.TableColIndex < block.ColumnAlignments.Count Then
                        Select Case block.ColumnAlignments(frag.TableColIndex)
                            Case TableAlignment.Center : align = D2DTextAlign.Center
                            Case TableAlignment.Right : align = D2DTextAlign.Right
                        End Select
                    End If
                    DrawText_DX12(context, frag.Text, frag.UseFont, frag.ForeColor, New RectangleF(fragX, drawY, cellW, vl.Height), align, True)
                Else
                    Dim textWidth = If(blockKind = BlockKind.CodeBlock, Math.Max(0, TextAreaWidth() - frag.X), CInt(Short.MaxValue))
                    DrawText_DX12(context, frag.Text, frag.UseFont, frag.ForeColor, New RectangleF(fragX, drawY, textWidth, vl.Height), D2DTextAlign.Left, True)
                End If
            End If

            If fragW > 0 Then
                Dim lineBrush = frag.ForeColor
                If frag.Kind = InlineKind.Strikethrough Then
                    context.DrawLine(New Vector2(fragX, drawY + vl.Height \ 2), New Vector2(fragX + fragW, drawY + vl.Height \ 2), lineBrush, Math.Max(1.0F, 删除线粗细 * s))
                ElseIf frag.Kind = InlineKind.Link Then
                    Dim y As Integer = drawY + vl.Height - CInt(链接下划线偏移 * s)
                    context.DrawLine(New Vector2(fragX, y), New Vector2(fragX + fragW, y), lineBrush, Math.Max(1.0F, 链接下划线粗细 * s))
                End If
            End If
        Next
    End Sub

    Private Sub DrawFragmentSelection_DX12(context As D3D12_PaintContext,
                                          vli As Integer,
                                          fi As Integer,
                                          frag As VisualFragment,
                                          fragX As Integer,
                                          drawY As Integer,
                                          lineH As Integer,
                                          selStart As SelectionPos,
                                          selEnd As SelectionPos)
        If vli < selStart.VisualLine OrElse vli > selEnd.VisualLine Then Return
        Dim sChar As Integer = 0
        Dim eChar As Integer = frag.CharLength
        If vli = selStart.VisualLine Then
            If fi < selStart.FragmentIndex Then Return
            If fi = selStart.FragmentIndex Then sChar = selStart.CharOffset
        End If
        If vli = selEnd.VisualLine Then
            If fi > selEnd.FragmentIndex Then Return
            If fi = selEnd.FragmentIndex Then eChar = selEnd.CharOffset
        End If
        If eChar <= sChar OrElse frag.Text Is Nothing Then Return
        Dim safeS = Math.Min(sChar, frag.Text.Length)
        Dim safeE = Math.Min(eChar, frag.Text.Length)
        Dim x1 As Integer = fragX + If(safeS > 0, MeasureTextWidthCached(frag.Text.Substring(0, safeS), frag.UseFont, lineH), 0)
        Dim x2 As Integer = fragX + MeasureTextWidthCached(frag.Text.Substring(0, safeE), frag.UseFont, lineH)
        If x2 > x1 Then context.FillRectangle(New RectangleF(x1, drawY, x2 - x1, lineH), 选中背景颜色)
    End Sub

    Private Sub DrawText_DX12(context As D3D12_PaintContext,
                             text As String,
                             font As Font,
                             color As Color,
                             rect As RectangleF,
                             align As D2DTextAlign,
                             verticalCenter As Boolean,
                             Optional wordWrap As Boolean = False)
        If String.IsNullOrEmpty(text) OrElse font Is Nothing OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return

        Dim textAlign As Vortice.DirectWrite.TextAlignment
        Select Case align
            Case D2DTextAlign.Center : textAlign = Vortice.DirectWrite.TextAlignment.Center
            Case D2DTextAlign.Right : textAlign = Vortice.DirectWrite.TextAlignment.Trailing
            Case Else : textAlign = Vortice.DirectWrite.TextAlignment.Leading
        End Select
        Dim paraAlign As Vortice.DirectWrite.ParagraphAlignment = If(verticalCenter, Vortice.DirectWrite.ParagraphAlignment.Center, Vortice.DirectWrite.ParagraphAlignment.Near)
        context.DrawText(text, font, color, rect, DpiScale(), textAlign, paraAlign, wordWrap)
    End Sub

    Private Sub DrawScrollBar_DX12(context As D3D12_PaintContext, w As Integer, h As Integer, s As Single)
        If _embeddedContentMode OrElse Not _scrollBarVisible Then Return
        Dim scaledBorder As Integer = CInt(Math.Round(边框宽度 * s))
        Dim scaledRadius As Integer = CInt(Math.Round(边框圆角半径 * s))
        Dim scaledScrollW As Integer = CInt(Math.Round(滚动条宽度 * s))
        Dim viewH As Integer = ViewportHeight()
        If _totalContentHeight <= 0 OrElse viewH <= 0 OrElse scaledScrollW <= 0 Then Return

        _scrollBar.ComputeLayout(w, h, scaledBorder, scaledRadius, 0, 0, scaledScrollW, _totalContentHeight, viewH, _scrollY)
        If _scrollBar.TrackRect.IsEmpty Then Return

        Dim width As Single = Math.Max(1.0F, scaledScrollW)
        Dim trackArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.TrackRect.Y, width, _scrollBar.TrackRect.Height)
        Dim thumbArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.ThumbRect.Y, width, _scrollBar.ThumbRect.Height)
        context.FillRoundedRectangle(trackArea, Math.Min(width / 2.0F, trackArea.Height / 2.0F), 滚动条轨道颜色)
        Dim thumbColor = If(_scrollBar.IsDragging OrElse _scrollBar.IsHover, 滚动条悬停颜色, 滚动条颜色)
        context.FillRoundedRectangle(thumbArea, Math.Min(width / 2.0F, thumbArea.Height / 2.0F), thumbColor)
    End Sub

    Private Sub DrawMermaidSequenceDiagram_DX12(context As D3D12_PaintContext,
                                                block As MarkdownBlock,
                                                vl As VisualLine,
                                                vli As Integer,
                                                left As Integer,
                                                top As Integer,
                                                width As Integer,
                                                scale As Single)
        Dim participants As List(Of String) = Nothing
        Dim messages = ParseMermaidSequence(block.RawText, participants)
        If participants Is Nothing OrElse participants.Count = 0 Then Return
        Dim baseFont = GetCodeFont()
        Dim participantWidth As Integer = 0
        Dim stepX As Single = 0
        Dim participantFont = CreateMermaidFont(participants, width, scale, baseFont, participantWidth, stepX)
        Dim messageFont = New Font(baseFont.FontFamily, baseFont.SizeInPoints, baseFont.Style)
        Using participantFont
            Using messageFont
                Dim lineHeight = Math.Max(GetLayoutLineHeight(participantFont), GetLayoutLineHeight(messageFont))
            Dim diagramTop = top
            Dim diagramHeight = Math.Max(vl.Height, _blockLayoutBottoms(vl.BlockIndex) - vl.Y)
            Dim headerHeight = Math.Max(lineHeight + CInt(10 * scale), CInt(30 * scale))
            Dim bottomTop = diagramTop + diagramHeight - headerHeight
            Dim participantMessageGap = Math.Max(6, CInt(8 * scale))
            Dim messageRows = vl.MermaidMessageRows
            If messageRows Is Nothing OrElse messageRows.Count <> messages.Count Then
                messageRows = MeasureMermaidMessageRows(messages, participants, width, participantWidth, stepX, scale, messageFont, lineHeight)
            End If
            Dim textGap = Math.Max(3, CInt(4 * scale))
            Dim arrowBand = Math.Max(6, CInt(8 * scale))
            Dim centers As New Dictionary(Of String, Single)(StringComparer.OrdinalIgnoreCase)
            Dim labelColor = If(代码块文字颜色 = Color.Empty, 文本颜色, 代码块文字颜色)
            Dim lineColor = Color.FromArgb(145, labelColor)
            Dim headerBack = Color.FromArgb(190, 38, 42, 48)
            For i As Integer = 0 To participants.Count - 1
                Dim cx = left + If(participants.Count = 1, width / 2.0F, participantWidth / 2.0F + i * stepX)
                centers(participants(i)) = cx
                DrawMermaidParticipant_DX12(context, participants(i), cx, diagramTop, participantWidth, headerHeight, participantFont, labelColor, lineColor, headerBack, scale)
                DrawMermaidParticipant_DX12(context, participants(i), cx, bottomTop, participantWidth, headerHeight, participantFont, labelColor, lineColor, headerBack, scale)
                DrawDashedLine_DX12(context, New Vector2(cx, diagramTop + headerHeight), New Vector2(cx, bottomTop), lineColor, Math.Max(1.0F, scale), CInt(4 * scale))
            Next
            Dim messageTop = diagramTop + headerHeight + participantMessageGap
            For mi As Integer = 0 To messages.Count - 1
                Dim msg = messages(mi)
                Dim rowHeight = messageRows(mi)
                Dim fromX As Single = centers(msg.FromName)
                Dim toX As Single = centers(msg.ToName)
                If msg.IsNote Then
                    Dim noteWidth = Math.Min(CInt(width * 0.6F), Math.Max(CInt(100 * scale), MeasureTextWidthCached(msg.Text, messageFont, lineHeight) + CInt(16 * scale)))
                    Dim noteHeight = Math.Max(lineHeight, rowHeight - CInt(8 * scale))
                    Dim noteRect As New RectangleF(fromX - noteWidth / 2.0F, messageTop + (rowHeight - noteHeight) / 2.0F, noteWidth, noteHeight)
                    context.FillRectangle(noteRect, Color.FromArgb(110, 107, 99, 36))
                    context.DrawRectangle(noteRect, lineColor, Math.Max(1.0F, scale))
                    DrawText_DX12(context, msg.Text, messageFont, labelColor, noteRect, D2DTextAlign.Center, True, True)
                    messageTop += rowHeight
                    Continue For
                End If
                Dim messageWidth = Math.Max(CInt(20 * scale), CInt(Math.Abs(toX - fromX) - 8 * scale))
                Dim labelLeft = (fromX + toX) / 2.0F - messageWidth / 2.0F
                Dim messageHeight = Math.Max(lineHeight, rowHeight - textGap - arrowBand)
                Dim messageY = messageTop + messageHeight + textGap
                Dim direction = If(toX >= fromX, 1.0F, -1.0F)
                If msg.IsDashed Then
                    DrawDashedLine_DX12(context, New Vector2(fromX, messageY), New Vector2(toX, messageY), lineColor, Math.Max(1.0F, scale), CInt(4 * scale))
                Else
                    Dim brush = lineColor
                    context.DrawLine(New Vector2(fromX, messageY), New Vector2(toX, messageY), brush, Math.Max(1.0F, scale))
                End If
                Dim arrow = Math.Max(3.0F, 5.0F * scale)
                Dim arrowBrush = lineColor
                context.DrawLine(New Vector2(toX, messageY), New Vector2(toX - direction * arrow, messageY - arrow), arrowBrush, Math.Max(1.0F, scale))
                context.DrawLine(New Vector2(toX, messageY), New Vector2(toX - direction * arrow, messageY + arrow), arrowBrush, Math.Max(1.0F, scale))
                DrawText_DX12(context, msg.Text, messageFont, labelColor, New RectangleF(labelLeft, messageTop, messageWidth, messageHeight), D2DTextAlign.Center, True, True)
                messageTop += rowHeight
            Next
            End Using
        End Using
    End Sub

    Private Sub DrawMermaidParticipant_DX12(context As D3D12_PaintContext,
                                           label As String,
                                           centerX As Single,
                                           top As Integer,
                                           width As Integer,
                                           height As Integer,
                                           font As Font,
                                           labelColor As Color,
                                           lineColor As Color,
                                           backColor As Color,
        scale As Single)
        Dim rect As New RectangleF(centerX - width / 2.0F, top, width, height)
        context.FillRectangle(rect, backColor)
        context.DrawRectangle(rect, lineColor, Math.Max(1.0F, scale))
        DrawText_DX12(context, label, font, labelColor, rect, D2DTextAlign.Center, True)
    End Sub

    Private Sub DrawDashedLine_DX12(context As D3D12_PaintContext, fromPoint As Vector2, toPoint As Vector2, color As Color, strokeWidth As Single, dashLength As Integer)
        Dim brush = color
        Dim total = Vector2.Distance(fromPoint, toPoint)
        If total <= 0 Then Return
        Dim direction = Vector2.Normalize(toPoint - fromPoint)
        Dim cursor As Single = 0
        Dim dash = Math.Max(2, dashLength)
        While cursor < total
            Dim segmentEnd = Math.Min(total, cursor + dash)
            context.DrawLine(fromPoint + direction * cursor, fromPoint + direction * segmentEnd, brush, strokeWidth)
            cursor += dash * 2
        End While
    End Sub
End Class
