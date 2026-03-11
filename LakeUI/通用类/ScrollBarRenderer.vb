Imports System.Drawing.Drawing2D

''' <summary>
''' 可复用的自定义滚动条渲染器，管理布局、绘制和交互状态。
''' </summary>
Public Class ScrollBarRenderer
    Public ThumbRect As Rectangle = Rectangle.Empty
    Public TrackRect As Rectangle = Rectangle.Empty
    Public VisualLeft As Integer = 0
    Public VisualTop As Integer = 0

    Public IsHover As Boolean = False
    Public IsDragging As Boolean = False
    Private _dragStartY As Integer = 0
    Private _dragStartX As Integer = 0
    Private _dragStartOffset As Integer = 0

    Public Const Margin As Integer = 2

    Public Sub ComputeLayout(containerW As Integer, containerH As Integer,
                             borderWidth As Integer, borderRadius As Integer,
                             paddingTop As Integer, paddingBottom As Integer,
                             scrollBarWidth As Integer,
                             totalCount As Integer, visibleCount As Integer, scrollOffset As Integer)
        Dim inset As Integer = Math.Max(borderWidth, If(borderRadius > 0, borderRadius \ 2, 0))
        Dim sbX As Integer = containerW - scrollBarWidth - inset - Margin
        Dim sbY As Integer = inset + Margin + paddingTop
        Dim sbH As Integer = containerH - (inset + Margin) * 2 - paddingTop - paddingBottom
        If sbH <= 0 OrElse scrollBarWidth <= 0 Then
            ThumbRect = Rectangle.Empty
            TrackRect = Rectangle.Empty
            VisualLeft = containerW
            Return
        End If

        VisualLeft = sbX
        TrackRect = New Rectangle(sbX - Margin, sbY, scrollBarWidth + Margin * 2, sbH)

        Dim maxOff As Integer = Math.Max(0, totalCount - visibleCount)
        Dim thumbH As Integer = Math.Max(20, CInt(sbH * visibleCount / Math.Max(1, totalCount)))
        Dim thumbY As Integer = sbY
        If maxOff > 0 Then
            thumbY = sbY + CInt((sbH - thumbH) * scrollOffset / maxOff)
        End If
        ThumbRect = New Rectangle(sbX - Margin, thumbY, scrollBarWidth + Margin * 2, thumbH)
    End Sub

    Public Sub Draw(g As Graphics, containerW As Integer, containerH As Integer,
                     borderWidth As Integer, borderRadius As Integer,
                     scrollBarWidth As Integer,
                     trackColor As Color, thumbColor As Color, thumbHoverColor As Color)
        If TrackRect.IsEmpty Then Return
        If TrackRect.Width < 1 OrElse TrackRect.Height < 1 OrElse scrollBarWidth < 1 Then Return

        Dim oldSmooth = g.SmoothingMode
        g.SmoothingMode = SmoothingMode.AntiAlias

        Dim oldClip As Region = g.Clip.Clone()
        If borderRadius > 0 Then
            Dim clipRect As New RectangleF(0, 0, containerW - 1, containerH - 1)
            If borderWidth > 0 Then
                Dim half As Single = borderWidth / 2.0F
                clipRect.Inflate(-half, -half)
            End If
            Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(clipRect, borderRadius)
                g.SetClip(path, Drawing2D.CombineMode.Replace)
            End Using
        End If

        Dim sbH As Integer = TrackRect.Height
        If trackColor.A > 0 Then
            Dim trackRadius As Integer = Math.Min(scrollBarWidth \ 2, sbH \ 2)
            Using trackPath As GraphicsPath = RectangleRenderer.创建圆角矩形路径(New RectangleF(VisualLeft, TrackRect.Y, scrollBarWidth, sbH), trackRadius)
                Using br As New SolidBrush(trackColor)
                    g.FillPath(br, trackPath)
                End Using
            End Using
        End If

        Dim activeColor As Color = If(IsDragging OrElse IsHover, thumbHoverColor, thumbColor)
        Dim thumbH As Integer = ThumbRect.Height
        Dim thumbRadius As Integer = Math.Min(scrollBarWidth \ 2, thumbH \ 2)
        Using thumbPath As GraphicsPath = RectangleRenderer.创建圆角矩形路径(New RectangleF(VisualLeft, ThumbRect.Y, scrollBarWidth, thumbH), thumbRadius)
            Using br As New SolidBrush(activeColor)
                g.FillPath(br, thumbPath)
            End Using
        End Using

        g.Clip = oldClip
        g.SmoothingMode = oldSmooth
    End Sub

    Public Function UpdateHover(mouseLocation As Point) As Boolean
        Dim wasHover As Boolean = IsHover
        IsHover = ThumbRect.Contains(mouseLocation)
        Return IsHover <> wasHover
    End Function

    Public Function BeginDrag(mouseLocation As Point, scrollOffset As Integer) As Boolean
        If ThumbRect.Contains(mouseLocation) Then
            IsDragging = True
            _dragStartY = mouseLocation.Y
            _dragStartOffset = scrollOffset
            Return True
        End If
        Return False
    End Function

    Public Function DragMove(mouseY As Integer, totalCount As Integer, visibleCount As Integer) As Integer
        If Not IsDragging Then Return _dragStartOffset
        Dim trackH As Integer = TrackRect.Height
        Dim maxOff As Integer = Math.Max(0, totalCount - visibleCount)
        Dim thumbH As Integer = Math.Max(20, CInt(trackH * visibleCount / Math.Max(1, totalCount)))
        Dim usableH As Integer = trackH - thumbH
        If usableH <= 0 Then Return _dragStartOffset
        Dim dy As Integer = mouseY - _dragStartY
        Dim newOff As Integer = _dragStartOffset + CInt(dy * maxOff / usableH)
        Return Math.Max(0, Math.Min(maxOff, newOff))
    End Function

    Public Function TrackClick(mouseLocation As Point, scrollOffset As Integer,
                               totalCount As Integer, visibleCount As Integer) As Integer
        If Not TrackRect.Contains(mouseLocation) Then Return scrollOffset
        Dim maxOff As Integer = Math.Max(0, totalCount - visibleCount)
        If mouseLocation.Y < ThumbRect.Y Then
            Return Math.Max(0, scrollOffset - visibleCount)
        Else
            Return Math.Min(maxOff, scrollOffset + visibleCount)
        End If
    End Function

    Public Sub EndDrag()
        IsDragging = False
    End Sub

    Public Function ResetHover() As Boolean
        If IsHover Then
            IsHover = False
            Return True
        End If
        Return False
    End Function

    Public Shared Function HandleWheel(delta As Integer, scrollOffset As Integer,
                                       totalCount As Integer, visibleCount As Integer,
                                       Optional scrollStep As Integer = 3) As Integer
        Dim d As Integer = -Math.Sign(delta) * scrollStep
        Dim maxOff As Integer = Math.Max(0, totalCount - visibleCount)
        Return Math.Max(0, Math.Min(maxOff, scrollOffset + d))
    End Function

    Public Function GetReservedWidth(containerW As Integer, inset As Integer) As Integer
        If TrackRect.IsEmpty Then Return 0
        Return containerW - inset - VisualLeft
    End Function

#Region "横向滚动条"

    Public Sub ComputeHorizontalLayout(containerW As Integer, containerH As Integer,
                                       borderWidth As Integer, borderRadius As Integer,
                                       paddingLeft As Integer, paddingRight As Integer,
                                       scrollBarHeight As Integer,
                                       totalContentWidth As Integer, visibleWidth As Integer,
                                       scrollPixelOffset As Integer)
        Dim inset As Integer = Math.Max(borderWidth, If(borderRadius > 0, borderRadius \ 2, 0))
        Dim sbX As Integer = inset + Margin + paddingLeft
        Dim sbY As Integer = containerH - scrollBarHeight - inset - Margin
        Dim sbW As Integer = containerW - (inset + Margin) * 2 - paddingLeft - paddingRight
        If sbW <= 0 OrElse scrollBarHeight <= 0 Then
            ThumbRect = Rectangle.Empty
            TrackRect = Rectangle.Empty
            VisualTop = containerH
            Return
        End If

        VisualTop = sbY
        TrackRect = New Rectangle(sbX, sbY - Margin, sbW, scrollBarHeight + Margin * 2)

        Dim maxOff As Integer = Math.Max(0, totalContentWidth - visibleWidth)
        Dim thumbW As Integer = Math.Max(20, CInt(sbW * visibleWidth / Math.Max(1, totalContentWidth)))
        Dim thumbX As Integer = sbX
        If maxOff > 0 Then
            thumbX = sbX + CInt((sbW - thumbW) * scrollPixelOffset / maxOff)
        End If
        ThumbRect = New Rectangle(thumbX, sbY - Margin, thumbW, scrollBarHeight + Margin * 2)
    End Sub

    Public Sub DrawHorizontal(g As Graphics, containerW As Integer, containerH As Integer,
                               borderWidth As Integer, borderRadius As Integer,
                               scrollBarHeight As Integer,
                               trackColor As Color, thumbColor As Color, thumbHoverColor As Color)
        If TrackRect.IsEmpty Then Return
        If TrackRect.Width < 1 OrElse TrackRect.Height < 1 OrElse scrollBarHeight < 1 Then Return

        Dim oldSmooth = g.SmoothingMode
        g.SmoothingMode = SmoothingMode.AntiAlias

        Dim oldClip As Region = g.Clip.Clone()
        If borderRadius > 0 Then
            Dim clipRect As New RectangleF(0, 0, containerW - 1, containerH - 1)
            If borderWidth > 0 Then
                Dim half As Single = borderWidth / 2.0F
                clipRect.Inflate(-half, -half)
            End If
            Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(clipRect, borderRadius)
                g.SetClip(path, Drawing2D.CombineMode.Replace)
            End Using
        End If

        Dim sbW As Integer = TrackRect.Width
        If trackColor.A > 0 Then
            Dim trackRadius As Integer = Math.Min(scrollBarHeight \ 2, sbW \ 2)
            Using trackPath As GraphicsPath = RectangleRenderer.创建圆角矩形路径(
                New RectangleF(TrackRect.X, VisualTop, sbW, scrollBarHeight), trackRadius)
                Using br As New SolidBrush(trackColor)
                    g.FillPath(br, trackPath)
                End Using
            End Using
        End If

        Dim activeColor As Color = If(IsDragging OrElse IsHover, thumbHoverColor, thumbColor)
        Dim thumbW As Integer = ThumbRect.Width
        Dim thumbRadius As Integer = Math.Min(scrollBarHeight \ 2, thumbW \ 2)
        Using thumbPath As GraphicsPath = RectangleRenderer.创建圆角矩形路径(
            New RectangleF(ThumbRect.X, VisualTop, thumbW, scrollBarHeight), thumbRadius)
            Using br As New SolidBrush(activeColor)
                g.FillPath(br, thumbPath)
            End Using
        End Using

        g.Clip = oldClip
        g.SmoothingMode = oldSmooth
    End Sub

    Public Function BeginDragHorizontal(mouseLocation As Point, scrollOffset As Integer) As Boolean
        If ThumbRect.Contains(mouseLocation) Then
            IsDragging = True
            _dragStartX = mouseLocation.X
            _dragStartOffset = scrollOffset
            Return True
        End If
        Return False
    End Function

    Public Function DragMoveHorizontal(mouseX As Integer, totalContentWidth As Integer, visibleWidth As Integer) As Integer
        If Not IsDragging Then Return _dragStartOffset
        Dim trackW As Integer = TrackRect.Width
        Dim maxOff As Integer = Math.Max(0, totalContentWidth - visibleWidth)
        Dim thumbW As Integer = Math.Max(20, CInt(trackW * visibleWidth / Math.Max(1, totalContentWidth)))
        Dim usableW As Integer = trackW - thumbW
        If usableW <= 0 Then Return _dragStartOffset
        Dim dx As Integer = mouseX - _dragStartX
        Dim newOff As Integer = _dragStartOffset + CInt(dx * maxOff / usableW)
        Return Math.Max(0, Math.Min(maxOff, newOff))
    End Function

    Public Function TrackClickHorizontal(mouseLocation As Point, scrollOffset As Integer,
                                         totalContentWidth As Integer, visibleWidth As Integer) As Integer
        If Not TrackRect.Contains(mouseLocation) Then Return scrollOffset
        Dim maxOff As Integer = Math.Max(0, totalContentWidth - visibleWidth)
        Dim pageSize As Integer = Math.Max(1, visibleWidth)
        If mouseLocation.X < ThumbRect.X Then
            Return Math.Max(0, scrollOffset - pageSize)
        Else
            Return Math.Min(maxOff, scrollOffset + pageSize)
        End If
    End Function

    Public Shared Function HandleHorizontalWheel(delta As Integer, scrollOffset As Integer,
                                                  totalContentWidth As Integer, visibleWidth As Integer,
                                                  Optional scrollStep As Integer = 40) As Integer
        Dim d As Integer = -Math.Sign(delta) * scrollStep
        Dim maxOff As Integer = Math.Max(0, totalContentWidth - visibleWidth)
        Return Math.Max(0, Math.Min(maxOff, scrollOffset + d))
    End Function

#End Region

End Class