Imports Vortice.DirectWrite
Imports System.Numerics

Partial Public Class ModernTextBox
    Implements ID3D12Renderable

    Public Overridable Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        EnsureDpiCacheCurrent()
        Dim w As Integer = ClientRectangle.Width
        Dim h As Integer = ClientRectangle.Height
        If w <= 0 OrElse h <= 0 Then Return
        Dim hasRadius As Boolean = 边框圆角半径 > 0
        Dim sourceRect As New RectangleF(0, 0, w, h)
        Dim boundsRect As New RectangleF(0, 0, w, h)
        Dim s As Single = DpiScale()
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * s / 2.0F
            boundsRect.Inflate(-half, -half)
        End If
        Dim bc As Color = If(Focused, 有焦点时边框颜色, 边框颜色)

        绘制背景_DX12(context, hasRadius, sourceRect, boundsRect)
        DrawTextContent_DX12(context, w, h)
        绘制边框_DX12(context, hasRadius, boundsRect, bc)
        DrawScrollBar_DX12(context, w, h)
    End Sub

    Private Sub 绘制背景_DX12(context As D3D12_PaintContext, hasRadius As Boolean, sourceRect As RectangleF, fillRect As RectangleF)
        Dim backColorMask As Color = MyBase.BackColor
        Dim radius As Single = If(hasRadius, 边框圆角半径 * DpiScale(), 0.0F)
        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(_backgroundSource, Rectangle.Round(sourceRect))
        End If
        If _backgroundSource Is Nothing AndAlso backColorMask.A > 0 AndAlso backColorMask.A < 255 Then
            填充圆角矩形_DX12(context, fillRect, radius, backColorMask)
        End If
        If 背景颜色.A > 0 Then
            填充圆角矩形_DX12(context, fillRect, radius, 背景颜色)
        End If
    End Sub

    Private Sub 绘制边框_DX12(context As D3D12_PaintContext, hasRadius As Boolean, boundsRect As RectangleF, borderClr As Color)
        If 边框宽度 <= 0 OrElse borderClr.A = 0 Then Return
        Dim s As Single = DpiScale()
        绘制圆角边框_DX12(context, boundsRect, If(hasRadius, 边框圆角半径 * s, 0.0F), borderClr, 边框宽度 * s)
    End Sub

    Private Sub DrawScrollBar_DX12(context As D3D12_PaintContext, w As Integer, h As Integer)
        If Not _scrollBarVisible Then Return
        Dim s As Single = DpiScale()
        Dim scaledBorder As Integer = CInt(Math.Round(边框宽度 * s))
        Dim scaledRadius As Integer = CInt(Math.Round(边框圆角半径 * s))
        Dim scaledScrollW As Integer = CInt(Math.Round(滚动条宽度 * s))
        Dim totalH As Integer = TotalContentPixelHeight()
        Dim viewH As Integer = TextViewportHeight()
        If totalH <= 0 OrElse viewH <= 0 Then Return

        _scrollBar.ComputeLayout(w, h, scaledBorder, scaledRadius, 0, 0, scaledScrollW,
            totalH, viewH, CInt(Math.Round(_scrollPixelOffset)))
        If _scrollBar.TrackRect.IsEmpty Then Return

        Dim width As Single = Math.Max(1.0F, scaledScrollW)
        Dim trackArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.TrackRect.Y, width, _scrollBar.TrackRect.Height)
        Dim thumbArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.ThumbRect.Y, width, _scrollBar.ThumbRect.Height)
        填充圆角矩形_DX12(context, trackArea, Math.Min(width / 2.0F, trackArea.Height / 2.0F), 滚动条轨道颜色)
        Dim thumbColor As Color = If(_scrollBar.IsDragging OrElse _scrollBar.IsHover, 滚动条悬停颜色, 滚动条颜色)
        填充圆角矩形_DX12(context, thumbArea, Math.Min(width / 2.0F, thumbArea.Height / 2.0F), thumbColor)
    End Sub

    Private Sub 填充圆角矩形_DX12(context As D3D12_PaintContext, rect As RectangleF, radius As Single, color As Color)
        context.FillRoundedRectangle(rect, radius, color)
    End Sub

    Private Sub 绘制圆角边框_DX12(context As D3D12_PaintContext, rect As RectangleF, radius As Single, color As Color, strokeWidth As Single)
        context.StrokeRoundedRectangle(rect, radius, color, strokeWidth)
    End Sub

    Private Sub DrawTextContent_DX12(context As D3D12_PaintContext, w As Integer, h As Integer)
        Dim bi As Integer = ScaledBorderWidth()
        Dim pad As Padding = EffectivePadding()
        Dim textTop As Integer = Math.Max(pad.Top, bi)
        Dim textBottom As Integer = Math.Max(pad.Bottom, bi)
        Dim gutterW As Integer = LineNumberGutterWidth()
        Dim gutterLeft As Integer = bi
        Dim textLeft As Integer
        If gutterW > 0 Then
            textLeft = bi + gutterW + pad.Left
        Else
            textLeft = Math.Max(pad.Left, bi)
        End If

        Dim textWidth As Integer = Math.Max(0, TextAreaRight() - textLeft)
        Dim textHeight As Integer = Math.Max(0, h - textTop - textBottom)
        Dim isSingleLine As Boolean = Not 启用多行
        Dim singleLineY As Single = textTop + (textHeight - _scaledLineHeight) / 2.0F
        Dim scrollY As Single = If(isSingleLine, 0.0F, _scrollPixelOffset)
        Dim startVi As Integer = If(isSingleLine OrElse _scaledLineHeight <= 0, 0, CInt(Math.Floor(scrollY / _scaledLineHeight)))
        Dim scrollRemainder As Single = If(isSingleLine, 0.0F, scrollY - startVi * _scaledLineHeight)
        Dim visibleLines As Integer = If(isSingleLine, 1, CInt(Math.Ceiling((textHeight + scrollRemainder) / _scaledLineHeight)) + 1)
        Dim endVi As Integer = Math.Min(_visualLines.Count - 1, startVi + visibleLines - 1)

        If gutterW > 0 AndAlso textHeight > 0 Then
            Dim gutterRect As New RectangleF(0, 0, bi + gutterW, h)
            If _lineNumBackColor <> Color.Empty Then
                If 边框圆角半径 > 0 Then
                    Dim clipRect As New RectangleF(0, 0, w, h)
                    If 边框宽度 > 0 Then
                        Dim half As Single = 边框宽度 * DpiScale() / 2.0F
                        clipRect.Inflate(-half, -half)
                    End If

                    If clipRect.Width > 0 AndAlso clipRect.Height > 0 Then
                        Using context.PushClip(gutterRect)
                            context.FillRoundedRectangle(clipRect, 边框圆角半径 * DpiScale(), _lineNumBackColor)
                        End Using
                    Else
                        context.FillRectangle(gutterRect, _lineNumBackColor)
                    End If
                Else
                    context.FillRectangle(gutterRect, _lineNumBackColor)
                End If
            End If

            Using context.PushClip(New RectangleF(gutterLeft, textTop, gutterW, textHeight))
                Dim useNumFont As Font = If(_lineNumFont, Font)
                Dim contentW As Integer = gutterW - _scaledLineNumPadL - _scaledLineNumPadR
                Dim lastDrawnLogical As Integer = -1
                For vi As Integer = startVi To endVi
                    Dim vl = _visualLines(vi)
                    Dim lineY As Single = textTop + (vi - startVi) * _scaledLineHeight - scrollRemainder
                    If vl.LogicalLine <> lastDrawnLogical Then
                        lastDrawnLogical = vl.LogicalLine
                        Dim numStr As String = (vl.LogicalLine + 1).ToString()
                        Dim numW As Integer = CInt(Math.Ceiling(MeasureTextWidthDirectWrite(numStr, useNumFont, Nothing)))
                        Dim numX As Integer
                        Select Case _lineNumAlign
                            Case TextAlignMode.Left
                                numX = gutterLeft + _scaledLineNumPadL
                            Case TextAlignMode.Center
                                numX = gutterLeft + _scaledLineNumPadL + (contentW - numW) \ 2
                            Case Else
                                numX = gutterLeft + _scaledLineNumPadL + contentW - numW
                        End Select
                        DrawTextSegment_DX12(context, numStr, useNumFont, _lineNumForeColor, numX, lineY, numW, _scaledLineHeight, False)
                    End If
                Next
            End Using
        End If

        If textWidth <= 0 OrElse textHeight <= 0 Then Return
        Using context.PushClip(New RectangleF(textLeft, textTop, textWidth, textHeight))
            Dim isEmpty As Boolean = (_lines.Count = 1 AndAlso _lines(0).Length = 0)
            If isEmpty AndAlso Not String.IsNullOrEmpty(水印文本) Then
                DrawWaterText_DX12(context, textLeft, textTop, textWidth, textHeight, singleLineY)
            End If

            Dim wrapActive As Boolean = IsWordWrapActive()
            Dim minL As Integer = 0, minC As Integer = 0, maxL As Integer = 0, maxC As Integer = 0
            If _hasSelection Then
                GetOrderedSelection(minL, minC, maxL, maxC)
            End If

            For vi As Integer = startVi To endVi
                Dim vl = _visualLines(vi)
                Dim lineY As Single = If(isSingleLine, singleLineY,
                    textTop + (vi - startVi) * _scaledLineHeight - scrollRemainder)
                Dim alignOff As Integer = If(wrapActive, 0, GetAlignOffsetXForLine(vl.LogicalLine, textWidth))
                Dim scrollX As Integer = If(wrapActive, 0, _scrollXOffset)
                If _hasSelection Then
                    DrawVisualLineSelection_DX12(context, vl, lineY, textLeft, alignOff, scrollX, minL, minC, maxL, maxC)
                End If
                If vl.Length > 0 Then
                    DrawLineRuns_DX12(context, vl.LogicalLine, vl.StartCol, vl.Length,
                        textLeft + alignOff - scrollX, lineY)
                End If
            Next

            If Focused AndAlso _caretVisible Then
                DrawCaret_DX12(context, textLeft, textTop)
            End If
        End Using
    End Sub

    Private Sub DrawWaterText_DX12(context As D3D12_PaintContext,
                                  textLeft As Integer,
                                  textTop As Integer,
                                  textWidth As Integer,
                                  textHeight As Integer,
                                  singleLineY As Single)
        If context Is Nothing OrElse String.IsNullOrEmpty(水印文本) OrElse 水印颜色.A = 0 Then Return
        If textWidth <= 0 OrElse textHeight <= 0 Then Return

        If Not IsWordWrapActive() Then
            Dim waterLineY As Single = If(启用多行, CSng(textTop), singleLineY)
            Dim waterAlignOff As Integer = If(启用多行 OrElse 文本对齐 = TextAlignMode.Left, 0,
                ComputeAlignOffset(MeasureWidth(水印文本), textWidth))
            DrawTextSegment_DX12(context, 水印文本, Font, 水印颜色, textLeft + waterAlignOff, waterLineY,
                textWidth, _scaledLineHeight, False)
            Return
        End If

        Dim waterLines = BuildWaterTextVisualLines(水印文本, textWidth)
        Dim visibleLines As Integer = Math.Min(waterLines.Count, Math.Max(1, CInt(Math.Ceiling(textHeight / CDbl(_scaledLineHeight)))))
        For i As Integer = 0 To visibleLines - 1
            Dim lineText As String = waterLines(i)
            If lineText.Length = 0 Then Continue For
            DrawTextSegment_DX12(context, lineText, Font, 水印颜色, textLeft,
                textTop + i * _scaledLineHeight, textWidth, _scaledLineHeight, False)
        Next
    End Sub

    Private Sub DrawVisualLineSelection_DX12(context As D3D12_PaintContext, vl As VisualLineInfo, lineY As Single, textLeft As Integer,
                                            alignOff As Integer, scrollX As Integer,
                                            minL As Integer, minC As Integer, maxL As Integer, maxC As Integer)
        Dim li As Integer = vl.LogicalLine
        If li < minL OrElse li > maxL Then Return
        Dim selStart As Integer = If(li = minL, minC, 0)
        Dim selEnd As Integer = If(li = maxL, maxC, _lines(li).Length)
        Dim vlEnd As Integer = vl.StartCol + vl.Length
        Dim drawStart As Integer = Math.Max(selStart, vl.StartCol)
        Dim drawEnd As Integer = Math.Min(selEnd, vlEnd)
        If drawStart > drawEnd Then Return
        If drawStart = drawEnd AndAlso Not (selEnd > vlEnd OrElse li < maxL) Then Return

        Dim x1 As Single = textLeft + alignOff + MeasureLineWidth(li, vl.StartCol, drawStart - vl.StartCol) - scrollX
        Dim x2 As Single
        If selEnd > vlEnd OrElse li < maxL Then
            x2 = textLeft + alignOff + MeasureLineWidth(li, vl.StartCol, vl.Length) + 6 - scrollX
        Else
            x2 = textLeft + alignOff + MeasureLineWidth(li, vl.StartCol, drawEnd - vl.StartCol) - scrollX
        End If
        If x2 <= x1 Then Return
        context.FillRectangle(New RectangleF(x1, lineY, x2 - x1, _scaledLineHeight), 选区背景色)
    End Sub

    Private Sub DrawCaret_DX12(context As D3D12_PaintContext, textLeft As Integer, textTop As Integer)
        Dim vi As Integer = GetVisualLineIndex(_caretLine, _caretCol)
        Dim viewH As Integer = TextViewportHeight()
        Dim caretTop As Integer = vi * _scaledLineHeight
        Dim caretBottom As Integer = caretTop + _scaledLineHeight
        If 启用多行 AndAlso (caretBottom < _scrollPixelOffset OrElse caretTop > _scrollPixelOffset + viewH) Then
            Return
        End If

        Dim vl = _visualLines(vi)
        Dim wrapActive As Boolean = IsWordWrapActive()
        Dim alignOff As Integer = If(wrapActive, 0, GetAlignOffsetXForLine(_caretLine, TextAreaWidth()))
        Dim scrollX As Integer = If(wrapActive, 0, _scrollXOffset)
        Dim cx As Integer = textLeft + alignOff + MeasureLineWidth(_caretLine, vl.StartCol, _caretCol - vl.StartCol) - scrollX
        Dim lineY As Single
        If Not 启用多行 Then
            Dim bi2 As Integer = ScaledBorderWidth()
            Dim pad As Padding = EffectivePadding()
            Dim textHeight As Integer = ClientRectangle.Height - Math.Max(pad.Top, bi2) - Math.Max(pad.Bottom, bi2)
            lineY = textTop + (textHeight - _scaledLineHeight) / 2.0F
        Else
            lineY = textTop + vi * _scaledLineHeight - _scrollPixelOffset
        End If
        Dim caretH As Integer = _scaledLineHeight - 2
        Dim caretY As Single = lineY + (_scaledLineHeight - caretH) / 2.0F
        context.FillRectangle(New RectangleF(cx, caretY, _scaledCaretWidth, caretH), 光标颜色)
    End Sub

    Private Sub DrawLineRuns_DX12(context As D3D12_PaintContext, lineIndex As Integer, vlStartCol As Integer,
                                 vlLength As Integer, x As Single, lineY As Single)
        Dim runs = _lineRuns(lineIndex)
        Dim lineStr = _lines(lineIndex)
        Dim vlEnd = vlStartCol + vlLength
        Dim links = If(lineIndex < _lineLinks.Count, _lineLinks(lineIndex), Nothing)
        Dim hasLinks As Boolean = links IsNot Nothing AndAlso links.Count > 0 AndAlso (_passwordChar = vbNullChar OrElse 启用多行)
        If runs Is Nothing OrElse runs.Count = 0 Then
            If Not hasLinks Then
                Dim text = GetDisplayText(lineStr.Substring(vlStartCol, vlLength))
                DrawTextSegment_DX12(context, text, Font, ForeColor, x, lineY, Short.MaxValue, _scaledLineHeight, False)
            Else
                DrawSegmentsWithLinks_DX12(context, lineStr, vlStartCol, vlEnd, x, lineY, ForeColor, Font, links)
            End If
            Return
        End If

        Dim drawX = x
        For Each r In runs
            Dim rEnd = r.StartCol + r.Length
            Dim segStart = Math.Max(r.StartCol, vlStartCol)
            Dim segEnd = Math.Min(rEnd, vlEnd)
            If segStart >= segEnd Then Continue For
            Dim useFore = If(r.ForeColor = Color.Empty, ForeColor, r.ForeColor)
            Dim useFont = If(r.RunFont, Font)
            If Not hasLinks Then
                Dim segText = GetDisplayText(lineStr.Substring(segStart, segEnd - segStart))
                drawX += DrawTextSegment_DX12(context, segText, useFont, useFore, drawX, lineY, Short.MaxValue, _scaledLineHeight, False)
            Else
                drawX = DrawSegmentsWithLinks_DX12(context, lineStr, segStart, segEnd, drawX, lineY, useFore, useFont, links)
            End If
        Next
    End Sub

    Private Function DrawSegmentsWithLinks_DX12(context As D3D12_PaintContext, lineStr As String, startCol As Integer, endCol As Integer,
                                               x As Single, lineY As Single, baseFore As Color, baseFont As Font,
                                               links As List(Of LinkRange)) As Single
        Dim pos = startCol
        Dim drawX = x
        For Each link In links
            Dim linkEnd = link.StartCol + link.Length
            If link.StartCol >= endCol OrElse linkEnd <= pos Then Continue For
            If pos < link.StartCol AndAlso pos < endCol Then
                Dim nonLinkEnd = Math.Min(link.StartCol, endCol)
                Dim segText = GetDisplayText(lineStr.Substring(pos, nonLinkEnd - pos))
                drawX += DrawTextSegment_DX12(context, segText, baseFont, baseFore, drawX, lineY, Short.MaxValue, _scaledLineHeight, False)
                pos = nonLinkEnd
            End If

            Dim overlapStart = Math.Max(pos, link.StartCol)
            Dim overlapEnd = Math.Min(endCol, linkEnd)
            If overlapStart < overlapEnd Then
                Dim linkText = GetDisplayText(lineStr.Substring(overlapStart, overlapEnd - overlapStart))
                drawX += DrawTextSegment_DX12(context, linkText, baseFont, 链接颜色, drawX, lineY, Short.MaxValue, _scaledLineHeight, 链接下划线)
                pos = overlapEnd
            End If
        Next

        If pos < endCol Then
            Dim segText = GetDisplayText(lineStr.Substring(pos, endCol - pos))
            drawX += DrawTextSegment_DX12(context, segText, baseFont, baseFore, drawX, lineY, Short.MaxValue, _scaledLineHeight, False)
        End If
        Return drawX
    End Function

    Private Function DrawTextSegment_DX12(context As D3D12_PaintContext, text As String, font As Font, foreColor As Color,
                                         x As Single, y As Single, width As Single, height As Single, underline As Boolean) As Single
        If String.IsNullOrEmpty(text) OrElse foreColor.A = 0 OrElse font Is Nothing Then Return 0.0F
        Dim measuredWidth As Single = MeasureTextWidthDirectWrite(text, font, Nothing)
        Dim layoutWidth As Single = Math.Max(1.0F, width)
        Dim layoutHeight As Single = Math.Max(1.0F, height)
        context.DrawText(text, font, foreColor, New RectangleF(x, y, layoutWidth, layoutHeight), DpiScale(),
                         Vortice.DirectWrite.TextAlignment.Leading,
                         Vortice.DirectWrite.ParagraphAlignment.Center)

        If underline AndAlso measuredWidth > 0.0F Then
            Dim drawWidth = If(width >= Short.MaxValue, measuredWidth, Math.Min(measuredWidth, width))
            Dim thickness = Math.Max(1.0F, DpiScale())
            Dim underlineY = y + Math.Max(1.0F, height - 3.0F)
            context.FillRectangle(New RectangleF(x, underlineY - thickness / 2, drawWidth, thickness), foreColor)
        End If
        Return measuredWidth
    End Function
End Class
