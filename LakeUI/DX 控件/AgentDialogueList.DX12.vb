Imports System.Numerics
Imports Vortice.DirectWrite

Partial Public Class AgentDialogueList
    Implements ID3D12Renderable

    Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
        If Width <= 0 OrElse Height <= 0 Then Return
        EnsureLayout()
        If _backgroundSource IsNot Nothing Then
            If Not context.DrawBackgroundSource(_backgroundSource, ClientRectangle) AndAlso BackColor.A > 0 Then
                context.FillRectangle(New RectangleF(0, 0, Width, Height), BackColor)
            End If
        ElseIf BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Width, Height), BackColor)
        End If
        context.FillRectangle(New RectangleF(0, 0, Width, Height), _backColor1)

        Dim viewport As New RectangleF(Padding.Left, Padding.Top,
                                       Math.Max(0, Width - Padding.Horizontal),
                                       Math.Max(0, Height - Padding.Vertical))
        Using context.PushClip(viewport)
            For Each row In _rows
                If row.Bounds.Bottom < viewport.Top OrElse row.Bounds.Top > viewport.Bottom Then Continue For
                DrawRow(context, row)
            Next
            If _isDragging AndAlso ((_dragFolder IsNot Nothing AndAlso _dropFolderIndex >= 0) OrElse
                                    (_dragDialogue IsNot Nothing AndAlso _dropFolder IsNot Nothing AndAlso _dropIndex >= 0)) Then
                DrawDropIndicator(context)
            End If
        End Using
        DrawScrollBar(context)
        If _borderWidth > 0 AndAlso _borderColor.A > 0 Then
            Dim borderRect As New RectangleF(_borderWidth / 2.0F, _borderWidth / 2.0F,
                                             Math.Max(0, Width - _borderWidth), Math.Max(0, Height - _borderWidth))
            context.StrokeRoundedRectangle(borderRect, S(_cornerRadius), _borderColor, S(_borderWidth))
        End If
    End Sub

    Private Sub DrawRow(context As D3D12_PaintContext, row As LayoutRow)
        Dim isHover = IsRowHovered(row)
        Dim isSelected = row.Kind = RowKind.Dialogue AndAlso Object.ReferenceEquals(row.Dialogue, _selectedDialogue)
        Dim bg = If(isSelected, _selectedDialogueBackColor,
                     If(isHover, _hoverBackColor,
                        If(row.Kind = RowKind.Folder, _folderBackColor, Color.Transparent)))
        If bg.A > 0 Then FillRounded(context, row.Bounds, S(_cornerRadius), bg)

        Dim textLeft = row.Bounds.Left + S(_itemTextPadding)
        If row.Kind = RowKind.Dialogue AndAlso row.Dialogue.IsInProgress Then
            Dim ringSize = Math.Min(S(_progressRingSize), Math.Max(0.0F, row.Bounds.Height - S(8)))
            If ringSize > 0.0F Then
                Dim ringRect As New RectangleF(textLeft,
                                               row.Bounds.Top + (row.Bounds.Height - ringSize) / 2.0F,
                                               ringSize, ringSize)
                ProgressRingRenderer12.RenderWin11(context, ringRect, _progressRingColor,
                                               _progressRingThickness * DpiScale(),
                                               _progressAnimationRunning,
                                               _progressStopwatch.Elapsed.TotalMilliseconds,
                                               DesignMode, _progressRingAnimationDuration)
                textLeft += ringSize + S(_iconTextSpacing)
            End If
        End If
        If row.Kind = RowKind.Folder Then
            ' 用两段线绘制直角折叠箭头：展开时向下，折叠时向右，
            ' 并固定显示在文件夹文本左侧。
            Dim glyphSize = S(_folderCollapseIconSize)
            Dim glyphCenterX = CInt(textLeft + glyphSize / 2.0F)
            Dim glyphCenterY = CInt(row.Bounds.Top + row.Bounds.Height / 2.0F)
            DrawChevronLines_DX12(context, glyphCenterX, glyphCenterY,
                                 glyphSize, row.Folder.IsExpanded,
                                 If(row.Folder.ForeColor = Color.Empty, _folderForeColor, row.Folder.ForeColor),
                                 Math.Max(1.0F, S(2)))
            textLeft += glyphSize + S(_iconTextSpacing)
        End If

        Dim icon As Image = If(row.Kind = RowKind.Folder, row.Folder.Icon, row.Dialogue.Icon)
        If icon IsNot Nothing Then
            Dim iconSize = Math.Min(S(_iconSize), Math.Max(0, row.Bounds.Height - S(8)))
            context.DrawImage(icon, New RectangleF(textLeft, row.Bounds.Top + (row.Bounds.Height - iconSize) / 2.0F,
                                                  iconSize, iconSize))
            textLeft += iconSize + S(_iconTextSpacing)
        End If

        Dim showActionButtons = IsRowHovered(row)
        Dim buttonWidth = If(Not showActionButtons OrElse row.ButtonBounds.Count = 0, 0.0F,
                             (S(_actionButtonSize) + S(_actionButtonSpacing)) * row.ButtonBounds.Count)
        Dim textRight = row.Bounds.Right - S(_itemTextPadding) - buttonWidth
        If row.Kind = RowKind.Folder Then
            context.DrawText(row.Folder.Text, If(row.Folder.FolderFont, If(_folderFont, Font)),
                             If(row.Folder.ForeColor = Color.Empty, _folderForeColor, row.Folder.ForeColor),
                             New RectangleF(textLeft, row.Bounds.Top, Math.Max(0, textRight - textLeft), row.Bounds.Height),
                             DpiScale(), TextAlignment.Leading, ParagraphAlignment.Center)
        Else
            Dim titleHeight = If(String.IsNullOrEmpty(row.Dialogue.Subtitle), row.Bounds.Height, row.Bounds.Height * 0.58F)
            context.DrawText(row.Dialogue.Text, If(row.Dialogue.DialogueFont, If(_dialogueFont, Font)),
                             If(isSelected AndAlso row.Dialogue.SelectedForeColor <> Color.Empty,
                                row.Dialogue.SelectedForeColor,
                                If(isSelected, _selectedDialogueForeColor,
                                   If(row.Dialogue.ForeColor = Color.Empty, _dialogueForeColor, row.Dialogue.ForeColor))),
                             New RectangleF(textLeft, row.Bounds.Top, Math.Max(0, textRight - textLeft), titleHeight),
                             DpiScale(), TextAlignment.Leading, ParagraphAlignment.Center)
            If Not String.IsNullOrEmpty(row.Dialogue.Subtitle) Then
                context.DrawText(row.Dialogue.Subtitle, If(_subtitleFont, Font), _subtitleForeColor,
                                 New RectangleF(textLeft, row.Bounds.Top + titleHeight - S(2), Math.Max(0, textRight - textLeft), row.Bounds.Height - titleHeight),
                                 DpiScale(), TextAlignment.Leading, ParagraphAlignment.Center)
            End If
        End If

        If Not showActionButtons Then Return
        For i = 0 To row.ButtonBounds.Count - 1
            Dim button = row.ButtonItems(i)
            Dim b = row.ButtonBounds(i)
            If Object.ReferenceEquals(button, _hoverButton) AndAlso _buttonHoverBackColor.A > 0 Then
                FillRounded(context, b, S(4), _buttonHoverBackColor)
            ElseIf _buttonBackColor.A > 0 Then
                FillRounded(context, b, S(4), _buttonBackColor)
            End If
            If button.Image IsNot Nothing Then
                Dim inset = S(4)
                context.DrawImage(button.Image, New RectangleF(b.X + inset, b.Y + inset,
                                                               Math.Max(0, b.Width - inset * 2), Math.Max(0, b.Height - inset * 2)))
            End If
        Next
    End Sub

    Private Shared Sub DrawChevronLines_DX12(context As D3D12_PaintContext,
                                            cx As Integer, cy As Integer, size As Single,
                                            expanded As Boolean, color As Color,
                                            strokeWidth As Single)
        If color.A = 0 Then Return
        Dim brush = color
        Dim halfSize = size / 2.0F
        Dim firstPoint As Vector2
        Dim vertex As Vector2
        Dim lastPoint As Vector2
        If expanded Then
            firstPoint = New Vector2(cx - halfSize, cy - halfSize / 2.0F)
            vertex = New Vector2(cx, cy + halfSize / 2.0F)
            lastPoint = New Vector2(cx + halfSize, cy - halfSize / 2.0F)
        Else
            firstPoint = New Vector2(cx - halfSize / 2.0F, cy - halfSize)
            vertex = New Vector2(cx + halfSize / 2.0F, cy)
            lastPoint = New Vector2(cx - halfSize / 2.0F, cy + halfSize)
        End If
        context.DrawLine(firstPoint, vertex, brush, strokeWidth)
        context.DrawLine(vertex, lastPoint, brush, strokeWidth)
        Dim jointSize = Math.Max(1.0F, strokeWidth)
        context.FillRectangle(New RectangleF(vertex.X - jointSize / 2.0F,
                                             vertex.Y - jointSize / 2.0F,
                                             jointSize, jointSize), color)
    End Sub

    Private Sub DrawDropIndicator(context As D3D12_PaintContext)
        If _dragFolder IsNot Nothing Then
            DrawFolderDropIndicator(context)
            Return
        End If
        Dim target As LayoutRow = Nothing
        Dim insertionBefore As Boolean = False
        For Each row In _rows
            If row.Kind = RowKind.Folder AndAlso row.Folder Is _dropFolder Then
                If _dropIndex = row.Folder.Dialogues.Count AndAlso row.Folder.Dialogues.Count = 0 Then target = row
            ElseIf row.Kind = RowKind.Dialogue AndAlso row.Folder Is _dropFolder Then
                Dim index = row.Folder.Dialogues.IndexOf(row.Dialogue)
                If index = _dropIndex OrElse index = _dropIndex - 1 Then
                    If index = _dropIndex Then
                        target = row
                        insertionBefore = True
                    End If
                End If
            End If
        Next
        If target Is Nothing AndAlso _dropFolder IsNot Nothing AndAlso
           _dropIndex = _dropFolder.Dialogues.Count Then
            For i As Integer = _rows.Count - 1 To 0 Step -1
                Dim row = _rows(i)
                If row.Kind = RowKind.Dialogue AndAlso row.Folder Is _dropFolder Then
                    target = row
                    Exit For
                End If
            Next
        End If
        If target Is Nothing Then Return
        Dim y = If(insertionBefore, target.Bounds.Top, target.Bounds.Bottom)
        context.FillRectangle(New RectangleF(target.Bounds.Left, y - S(_dropIndicatorHeight) / 2.0F,
                                             target.Bounds.Width, S(_dropIndicatorHeight)), _dropIndicatorColor)
    End Sub

    Private Sub DrawFolderDropIndicator(context As D3D12_PaintContext)
        If _dropFolderIndex < 0 Then Return
        Dim target As LayoutRow = Nothing
        Dim y As Single
        If _dropFolderIndex < _folders.Count Then
            Dim targetFolder = _folders(_dropFolderIndex)
            For Each row In _rows
                If row.Kind = RowKind.Folder AndAlso row.Folder Is targetFolder Then
                    target = row
                    Exit For
                End If
            Next
            If target Is Nothing Then Return
            y = target.Bounds.Top
        Else
            Dim lastFolder = If(_folders.Count > 0, _folders(_folders.Count - 1), Nothing)
            If lastFolder Is Nothing Then Return
            For i As Integer = _rows.Count - 1 To 0 Step -1
                Dim row = _rows(i)
                If row.Folder Is lastFolder Then
                    target = row
                    Exit For
                End If
            Next
            If target Is Nothing Then Return
            y = target.Bounds.Bottom
        End If

        context.FillRectangle(New RectangleF(target.Bounds.Left,
                                             y - S(_dropIndicatorHeight) / 2.0F,
                                             target.Bounds.Width,
                                             S(_dropIndicatorHeight)),
                              _dropIndicatorColor)
    End Sub

    Private Sub DrawScrollBar(context As D3D12_PaintContext)
        If _scrollBar.TrackRect.IsEmpty Then Return
        Dim width = Math.Max(1.0F, S(_scrollBarWidth))
        Dim track = New RectangleF(_scrollBar.VisualLeft, _scrollBar.TrackRect.Y, width, _scrollBar.TrackRect.Height)
        Dim thumb = New RectangleF(_scrollBar.VisualLeft, _scrollBar.ThumbRect.Y, width, _scrollBar.ThumbRect.Height)
        FillRounded(context, track, width / 2.0F, _scrollBarTrackColor)
        FillRounded(context, thumb, width / 2.0F,
                    If(_scrollBar.IsHover OrElse _scrollBar.IsDragging, _scrollBarThumbHoverColor, _scrollBarThumbColor))
    End Sub

    Private Shared Sub FillRounded(context As D3D12_PaintContext, rect As RectangleF, radius As Single, color As Color)
        If color.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        If radius <= 0 Then
            context.FillRectangle(rect, color)
        Else
            context.FillRoundedRectangle(rect, radius, color)
        End If
    End Sub
End Class
