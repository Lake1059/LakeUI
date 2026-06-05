Imports System.Numerics
Imports Vortice.Direct2D1

''' <summary>
''' 供组合控件复用的单行轻量文本框内核：负责文本绘制、光标、选区、滚动、命中测试与基础编辑。
''' </summary>
Public Class SingleLineTextBoxRenderer
    Public Event TextChanged As EventHandler

    Public Enum TextAlignMode
        Left = 0
        Center = 1
        Right = 2
    End Enum

    Private ReadOnly _owner As Control
    Private ReadOnly _caretBlinkTimer As New Timer() With {.Interval = 530}
    Private _text As String = String.Empty
    Private _caretCol As Integer = 0
    Private _selAnchorCol As Integer = 0
    Private _hasSelection As Boolean = False
    Private _caretVisible As Boolean = True
    Private _scrollXOffset As Integer = 0

    Public Sub New(owner As Control)
        ArgumentNullException.ThrowIfNull(owner)
        _owner = owner
        AddHandler _caretBlinkTimer.Tick,
            Sub()
                _caretVisible = Not _caretVisible
                _owner.Invalidate()
            End Sub
    End Sub

    Public Property Text As String
        Get
            Return _text
        End Get
        Set(value As String)
            SetText(value, -1, False, True)
        End Set
    End Property

    Public Property Editable As Boolean = True
    Public Property ForeColor As Color = Color.Silver
    Public Property WaterText As String = String.Empty
    Public Property WaterTextForeColor As Color = Color.Gray
    Public Property SelectionColor As Color = Color.FromArgb(80, 80, 80)
    Public Property CaretColor As Color = Color.FromArgb(220, 220, 220)
    Public Property CaretWidth As Integer = 2
    Public Property LineHeight As Integer = 25
    Public Property TextAlign As TextAlignMode = TextAlignMode.Left
    Public Property BorderSize As Integer = 1
    Public Property RightReservedWidth As Integer = 0
    Public Property TextFilter As Func(Of String, String)
    Public Property CandidateValidator As Func(Of String, Boolean)

    Public Property CaretColumn As Integer
        Get
            Return _caretCol
        End Get
        Set(value As Integer)
            _caretCol = Math.Max(0, Math.Min(_text.Length, value))
            _selAnchorCol = _caretCol
            _hasSelection = False
            EnsureCaretVisible()
            _owner.Invalidate()
        End Set
    End Property

    Public Property SelectionAnchorColumn As Integer
        Get
            Return _selAnchorCol
        End Get
        Set(value As Integer)
            _selAnchorCol = Math.Max(0, Math.Min(_text.Length, value))
            _hasSelection = (_caretCol <> _selAnchorCol)
            _owner.Invalidate()
        End Set
    End Property

    Public Property HasSelection As Boolean
        Get
            Return _hasSelection
        End Get
        Set(value As Boolean)
            _hasSelection = value AndAlso _caretCol <> _selAnchorCol
            _owner.Invalidate()
        End Set
    End Property

    Public Property CaretVisible As Boolean
        Get
            Return _caretVisible
        End Get
        Set(value As Boolean)
            If _caretVisible = value Then Return
            _caretVisible = value
            _owner.Invalidate()
        End Set
    End Property

    Public Property ScrollXOffset As Integer
        Get
            Return _scrollXOffset
        End Get
        Set(value As Integer)
            _scrollXOffset = Math.Max(0, value)
            _owner.Invalidate()
        End Set
    End Property

    Public Sub SetText(value As String, Optional caretColumn As Integer = -1,
                       Optional resetScroll As Boolean = False, Optional raiseTextChanged As Boolean = True)
        Dim v As String = If(value, String.Empty)
        Dim changed As Boolean = _text <> v
        If Not changed AndAlso caretColumn < 0 AndAlso Not resetScroll Then Return

        _text = v
        If caretColumn >= 0 Then
            _caretCol = Math.Max(0, Math.Min(_text.Length, caretColumn))
        ElseIf changed Then
            _caretCol = _text.Length
        End If
        If resetScroll Then _scrollXOffset = 0
        ClearSelection(False)
        EnsureCaretVisible()
        _owner.Invalidate()
        If changed AndAlso raiseTextChanged Then RaiseEvent TextChanged(_owner, EventArgs.Empty)
    End Sub

    Public Sub Draw(rt As ID2D1RenderTarget)
        If rt Is Nothing Then Return
        Dim area As RectangleF = GetTextArea()
        If area.Width <= 0 OrElse area.Height <= 0 Then Return

        PushClip_D2D(rt, area)
        Try
            Dim singleLineY As Integer = CInt(area.Y + (area.Height - LineHeight) \ 2)
            Dim isEmpty As Boolean = String.IsNullOrEmpty(_text)

            If isEmpty AndAlso Not String.IsNullOrEmpty(WaterText) Then
                Dim waterAlignOff As Integer = GetAlignOffsetX(WaterText, CInt(area.Width))
                DrawSingleLineText_D2D(rt, WaterText, _owner.Font, WaterTextForeColor,
                    New RectangleF(area.X + waterAlignOff, area.Y, area.Width, area.Height), DpiScale(), False)
            End If

            If Not isEmpty Then
                If _hasSelection Then DrawSelection_D2D(rt, singleLineY, CInt(area.X), CInt(area.Width))
                Dim alignOff As Integer = GetAlignOffsetX(_text, CInt(area.Width))
                DrawSingleLineText_D2D(rt, _text, _owner.Font, ForeColor,
                    New RectangleF(area.X + alignOff - _scrollXOffset, area.Y, Short.MaxValue, area.Height), DpiScale(), False)
            End If

            If _owner.Focused AndAlso _caretVisible AndAlso Editable Then
                DrawCaret_D2D(rt, CInt(area.X), CInt(area.Y))
            End If
        Finally
            rt.PopAxisAlignedClip()
        End Try
    End Sub

    Public Function HitTestColumn(x As Integer) As Integer
        Dim area As RectangleF = GetTextArea()
        Dim alignOff As Integer = GetAlignOffsetX(_text, CInt(area.Width))
        Return TextRenderHelper.FindColFromX_D2D(_text, CInt(x - area.X - alignOff + _scrollXOffset), _owner.Font, DpiScale())
    End Function

    Public Sub BeginMouseSelection(x As Integer)
        _caretCol = HitTestColumn(x)
        _selAnchorCol = _caretCol
        _hasSelection = False
        ResetCaretBlink()
    End Sub

    Public Sub UpdateMouseSelection(x As Integer)
        _caretCol = HitTestColumn(x)
        _hasSelection = (_caretCol <> _selAnchorCol)
        EnsureCaretVisible()
        _owner.Invalidate()
    End Sub

    Public Sub MoveCaret(deltaCol As Integer, extend As Boolean)
        If Not extend AndAlso _hasSelection AndAlso deltaCol <> 0 Then
            Dim minC As Integer = Math.Min(_selAnchorCol, _caretCol)
            Dim maxC As Integer = Math.Max(_selAnchorCol, _caretCol)
            _caretCol = If(deltaCol < 0, minC, maxC)
            ClearSelection(False)
            EnsureCaretVisible()
            _owner.Invalidate()
            Return
        End If
        If Not extend Then _selAnchorCol = _caretCol
        _caretCol = Math.Max(0, Math.Min(_text.Length, _caretCol + deltaCol))
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        _owner.Invalidate()
    End Sub

    Public Sub MoveCaretHome(extend As Boolean)
        If Not extend Then _selAnchorCol = _caretCol
        _caretCol = 0
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        _owner.Invalidate()
    End Sub

    Public Sub MoveCaretEnd(extend As Boolean)
        If Not extend Then _selAnchorCol = _caretCol
        _caretCol = _text.Length
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        _owner.Invalidate()
    End Sub

    Public Sub MoveCaretWordLeft(extend As Boolean)
        If Not extend Then _selAnchorCol = _caretCol
        If _caretCol > 0 Then
            Dim c As Integer = _caretCol - 1
            While c > 0 AndAlso Char.IsWhiteSpace(_text(c - 1))
                c -= 1
            End While
            While c > 0 AndAlso Not Char.IsWhiteSpace(_text(c - 1))
                c -= 1
            End While
            _caretCol = c
        End If
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        _owner.Invalidate()
    End Sub

    Public Sub MoveCaretWordRight(extend As Boolean)
        If Not extend Then _selAnchorCol = _caretCol
        If _caretCol < _text.Length Then
            Dim c As Integer = _caretCol
            While c < _text.Length AndAlso Not Char.IsWhiteSpace(_text(c))
                c += 1
            End While
            While c < _text.Length AndAlso Char.IsWhiteSpace(_text(c))
                c += 1
            End While
            _caretCol = c
        End If
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        _owner.Invalidate()
    End Sub

    Public Sub InsertText(text As String)
        If String.IsNullOrEmpty(text) Then Return
        Dim clean As String = If(TextFilter Is Nothing, DefaultFilterText(text), TextFilter.Invoke(text))
        If String.IsNullOrEmpty(clean) Then Return

        Dim startCol As Integer = _caretCol
        Dim endCol As Integer = _caretCol
        If _hasSelection Then
            startCol = Math.Min(_selAnchorCol, _caretCol)
            endCol = Math.Max(_selAnchorCol, _caretCol)
        End If

        Dim candidate As String = String.Concat(_text.AsSpan(0, startCol), clean, _text.AsSpan(endCol))
        If CandidateValidator IsNot Nothing AndAlso Not CandidateValidator.Invoke(candidate) Then Return
        _text = candidate
        _caretCol = startCol + clean.Length
        ClearSelection(False)
        RaiseTextChangedFromEdit()
    End Sub

    Public Sub HandleBackspace()
        Dim changed As Boolean = False
        If _hasSelection Then
            changed = DeleteSelectionCore(False)
        ElseIf _caretCol > 0 Then
            _text = String.Concat(_text.AsSpan(0, _caretCol - 1), _text.AsSpan(_caretCol))
            _caretCol -= 1
            changed = True
        End If
        If changed Then RaiseTextChangedFromEdit()
    End Sub

    Public Sub HandleDelete()
        Dim changed As Boolean = False
        If _hasSelection Then
            changed = DeleteSelectionCore(False)
        ElseIf _caretCol < _text.Length Then
            _text = String.Concat(_text.AsSpan(0, _caretCol), _text.AsSpan(_caretCol + 1))
            changed = True
        End If
        If changed Then RaiseTextChangedFromEdit()
    End Sub

    Public Sub SelectAll()
        _selAnchorCol = 0
        _caretCol = _text.Length
        _hasSelection = _text.Length > 0
        _owner.Invalidate()
    End Sub

    Public Sub ClearSelection(Optional invalidateOwner As Boolean = True)
        _hasSelection = False
        _selAnchorCol = _caretCol
        If invalidateOwner Then _owner.Invalidate()
    End Sub

    Public Sub DeleteSelection()
        If DeleteSelectionCore(False) Then RaiseTextChangedFromEdit()
    End Sub

    Public Function GetSelectedText() As String
        If Not _hasSelection Then Return String.Empty
        Dim minC As Integer = Math.Min(_selAnchorCol, _caretCol)
        Dim maxC As Integer = Math.Max(_selAnchorCol, _caretCol)
        Return _text.Substring(minC, maxC - minC)
    End Function

    Public Sub CopySelection()
        If _hasSelection Then
            Try
                Clipboard.SetText(GetSelectedText())
            Catch
            End Try
        End If
    End Sub

    Public Sub CutSelection()
        If _hasSelection AndAlso Editable Then
            CopySelection()
            If DeleteSelectionCore(False) Then RaiseTextChangedFromEdit()
        End If
    End Sub

    Public Sub PasteText()
        If Not Editable Then Return
        Try
            If Clipboard.ContainsText() Then InsertText(Clipboard.GetText())
        Catch
        End Try
    End Sub

    Public Sub EnsureCaretVisible()
        Dim area As RectangleF = GetTextArea()
        Dim areaW As Integer = CInt(area.Width)
        If areaW <= 0 Then Return
        _caretCol = Math.Max(0, Math.Min(_text.Length, _caretCol))
        Dim caretX As Integer = MeasureWidth(_text.Substring(0, _caretCol))
        If TextAlign <> TextAlignMode.Left Then
            Dim lineW As Integer = MeasureWidth(_text)
            If lineW < areaW Then
                _scrollXOffset = 0
                Return
            End If
        End If
        Dim margin As Integer = CInt(CaretWidth * DpiScale()) + 2
        If caretX - _scrollXOffset < 0 Then
            _scrollXOffset = Math.Max(0, caretX - margin)
        ElseIf caretX - _scrollXOffset >= areaW - margin Then
            _scrollXOffset = caretX - areaW + margin
        End If
    End Sub

    Public Function GetCaretImeLocation() As Point
        Dim area As RectangleF = GetTextArea()
        Dim alignOff As Integer = GetAlignOffsetX(_text, CInt(area.Width))
        Dim cx As Integer = CInt(area.X + alignOff + MeasureWidth(_text.Substring(0, _caretCol)) - _scrollXOffset)
        Dim cy As Integer = CInt(area.Y + (area.Height - LineHeight) \ 2 + LineHeight)
        Return New Point(cx, cy)
    End Function

    Public Sub StartCaretBlink()
        _caretVisible = True
        _caretBlinkTimer.Start()
        _owner.Invalidate()
    End Sub

    Public Sub StopCaretBlink()
        _caretBlinkTimer.Stop()
        _caretVisible = False
        _owner.Invalidate()
    End Sub

    Public Sub ResetCaretBlink()
        _caretVisible = True
        _caretBlinkTimer.Stop()
        _caretBlinkTimer.Start()
        _owner.Invalidate()
    End Sub

    Private Function DeleteSelectionCore(invalidateOwner As Boolean) As Boolean
        If Not _hasSelection Then Return False
        Dim minC As Integer = Math.Min(_selAnchorCol, _caretCol)
        Dim maxC As Integer = Math.Max(_selAnchorCol, _caretCol)
        _text = String.Concat(_text.AsSpan(0, minC), _text.AsSpan(maxC))
        _caretCol = minC
        ClearSelection(invalidateOwner)
        Return True
    End Function

    Private Sub RaiseTextChangedFromEdit()
        EnsureCaretVisible()
        _owner.Invalidate()
        RaiseEvent TextChanged(_owner, EventArgs.Empty)
    End Sub

    Private Sub UpdateSelectionFromAnchor(extend As Boolean)
        If extend Then
            _hasSelection = (_caretCol <> _selAnchorCol)
        Else
            ClearSelection(False)
        End If
    End Sub

    Private Function GetTextArea() As RectangleF
        Dim bi As Integer = CInt(BorderSize * DpiScale())
        Dim textLeft As Integer = Math.Max(_owner.Padding.Left, bi)
        Dim textTop As Integer = Math.Max(_owner.Padding.Top, bi)
        Dim textRight As Integer = Math.Max(_owner.Padding.Right, bi)
        Dim textBottom As Integer = Math.Max(_owner.Padding.Bottom, bi)
        Return New RectangleF(
            textLeft,
            textTop,
            _owner.ClientRectangle.Width - textLeft - textRight - RightReservedWidth,
            _owner.ClientRectangle.Height - textTop - textBottom)
    End Function

    Private Function DpiScale() As Single
        Return _owner.DeviceDpi / 96.0F
    End Function

    Private Function MeasureWidth(text As String) As Integer
        Return CInt(Math.Ceiling(TextRenderHelper.MeasureTextWidth_D2D(text, _owner.Font, DpiScale())))
    End Function

    Private Function GetAlignOffsetX(lineStr As String, areaWidth As Integer) As Integer
        If TextAlign = TextAlignMode.Left Then Return 0
        Dim textW As Integer = MeasureWidth(lineStr)
        If textW >= areaWidth Then Return 0
        Select Case TextAlign
            Case TextAlignMode.Center
                Return (areaWidth - textW) \ 2
            Case TextAlignMode.Right
                Return areaWidth - textW
            Case Else
                Return 0
        End Select
    End Function

    Private Sub DrawSelection_D2D(rt As ID2D1RenderTarget, lineY As Integer, textLeft As Integer, textWidth As Integer)
        Dim minC As Integer = Math.Min(_selAnchorCol, _caretCol)
        Dim maxC As Integer = Math.Max(_selAnchorCol, _caretCol)
        Dim alignOff As Integer = GetAlignOffsetX(_text, textWidth)
        Dim x1 As Integer = textLeft + alignOff + MeasureWidth(_text.Substring(0, minC)) - _scrollXOffset
        Dim x2 As Integer = textLeft + alignOff + MeasureWidth(_text.Substring(0, maxC)) - _scrollXOffset
        If x2 <= x1 Then Return
        Using br = rt.CreateSolidColorBrush(D2DGlobals.ToColor4(SelectionColor))
            rt.FillRectangle(New Vortice.Mathematics.Rect(x1, lineY, x2 - x1, LineHeight), br)
        End Using
    End Sub

    Private Sub DrawCaret_D2D(rt As ID2D1RenderTarget, textLeft As Integer, textTop As Integer)
        Dim area As RectangleF = GetTextArea()
        Dim alignOff As Integer = GetAlignOffsetX(_text, CInt(area.Width))
        Dim cx As Integer = textLeft + alignOff + MeasureWidth(_text.Substring(0, _caretCol)) - _scrollXOffset
        Dim lineY As Integer = CInt(textTop + (area.Height - LineHeight) \ 2)
        Dim caretH As Integer = LineHeight - 2
        Dim caretY As Integer = lineY + (LineHeight - caretH) \ 2
        Using br = rt.CreateSolidColorBrush(D2DGlobals.ToColor4(CaretColor))
            rt.FillRectangle(New Vortice.Mathematics.Rect(cx, caretY, CInt(CaretWidth * DpiScale()), caretH), br)
        End Using
    End Sub

    Private Shared Sub PushClip_D2D(rt As ID2D1RenderTarget, rect As RectangleF)
        rt.PushAxisAlignedClip(New Vortice.RawRectF(rect.Left, rect.Top, rect.Right, rect.Bottom), AntialiasMode.Aliased)
    End Sub

    Public Shared Sub DrawSingleLineText_D2D(rt As ID2D1RenderTarget, text As String, font As Font, foreColor As Color,
                                             rect As RectangleF, dpiScale As Single, endEllipsis As Boolean)
        If String.IsNullOrEmpty(text) OrElse foreColor.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Using fmt = TextRenderHelper.CreateDWriteTextFormat(font, dpiScale)
            fmt.WordWrapping = Vortice.DirectWrite.WordWrapping.NoWrap
            fmt.ParagraphAlignment = Vortice.DirectWrite.ParagraphAlignment.Center
            If endEllipsis Then
                Try
                    fmt.SetTrimming(New Vortice.DirectWrite.Trimming With {.Granularity = Vortice.DirectWrite.TrimmingGranularity.Character}, Nothing)
                Catch
                End Try
            End If
            Using layout = D2DGlobals.GetDWriteFactory().CreateTextLayout(text, fmt, rect.Width, rect.Height)
                Using br = rt.CreateSolidColorBrush(D2DGlobals.ToColor4(foreColor))
                    rt.DrawTextLayout(New Vector2(rect.X, rect.Y), layout, br)
                End Using
            End Using
        End Using
    End Sub

    Private Shared Function DefaultFilterText(text As String) As String
        Return If(text, String.Empty).Replace(vbCr, String.Empty).Replace(vbLf, String.Empty)
    End Function
End Class
