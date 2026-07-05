Imports System.Numerics
Imports Vortice.Direct2D1

''' <summary>
''' 供组合控件复用的单行轻量文本框内核：负责文本绘制、光标、选区、滚动、命中测试与基础编辑。
''' </summary>
''' <remarks>
''' 这是“无窗口文本框”内核，不继承 TextBox，也不创建子 HWND。外层控件需要把键盘、鼠标、
''' IME 消息和焦点状态转交给本类，然后在兼容绘制路径中调用绘制方法。
'''
''' 调用契约：
''' • 所有成员都假定在 UI 线程使用；内部的光标闪烁 Timer 也是 WinForms UI Timer。
''' • 外层控件必须在 Dispose 时调用本类 Dispose，否则 Timer 与 owner 事件订阅会继续持有控件。
''' • <see cref="TextAreaProvider"/> 应返回文本可绘制区域，坐标为 owner 客户区逻辑像素。
''' • 文本测量走 DirectWrite，并使用 owner 所属 Form 的 <see cref="D3D_D2DInterop.TextFormatCache"/>；
'''   字体、DPI 或文本变化时会清理前缀宽度缓存。
'''
''' 坑点：
''' • 光标/选区列索引按 .NET 字符索引处理，不做 grapheme cluster 分割；组合 emoji、复杂脚本
'''   的光标移动可能不是专业文本编辑器语义。
''' • IME 合成窗口位置由外层控件配合 <see cref="ImeHelper"/> 设置，本类只处理已经提交的文本。
''' • 文字层应画在 <see cref="D3D_PaintScope.TextLayer"/> 上，避免在 SSAA 图形层里画 ClearType 文字。
''' </remarks>
Public Class SingleLineTextBoxRenderer
    Implements IDisposable

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
    Private _caretBlinkRequested As Boolean
    Private _disposed As Boolean
    Private ReadOnly _prefixWidthCache As New Dictionary(Of Integer, Integer)(4)
    Private _measureFontKey As String = String.Empty
    Private _measureDpiScale As Single = -1.0F

    Public Sub New(owner As Control)
        ArgumentNullException.ThrowIfNull(owner)
        _owner = owner
        AddHandler _caretBlinkTimer.Tick,
            Sub()
                _caretVisible = Not _caretVisible
                InvalidateOwner()
            End Sub
        AddHandler _owner.VisibleChanged, AddressOf OwnerStateChanged
        AddHandler _owner.HandleCreated, AddressOf OwnerStateChanged
        AddHandler _owner.HandleDestroyed, AddressOf OwnerStateChanged
        AddHandler _owner.FontChanged, AddressOf OwnerMetricsChanged
        AddHandler _owner.DpiChangedAfterParent, AddressOf OwnerMetricsChanged
        AddHandler _owner.Disposed, AddressOf OwnerDisposed
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
    Public Property TextAreaProvider As Func(Of RectangleF)
    Public Property DpiScaleProvider As Func(Of Single)
    Public Property InvalidateAction As Action
    Public Property FocusProvider As Func(Of Boolean)

    Public Property CaretColumn As Integer
        Get
            Return _caretCol
        End Get
        Set(value As Integer)
            _caretCol = Math.Max(0, Math.Min(_text.Length, value))
            _selAnchorCol = _caretCol
            _hasSelection = False
            EnsureCaretVisible()
            InvalidateOwner()
        End Set
    End Property

    Public Property SelectionAnchorColumn As Integer
        Get
            Return _selAnchorCol
        End Get
        Set(value As Integer)
            _selAnchorCol = Math.Max(0, Math.Min(_text.Length, value))
            _hasSelection = (_caretCol <> _selAnchorCol)
            InvalidateOwner()
        End Set
    End Property

    Public Property HasSelection As Boolean
        Get
            Return _hasSelection
        End Get
        Set(value As Boolean)
            _hasSelection = value AndAlso _caretCol <> _selAnchorCol
            InvalidateOwner()
        End Set
    End Property

    Public Property CaretVisible As Boolean
        Get
            Return _caretVisible
        End Get
        Set(value As Boolean)
            If _caretVisible = value Then Return
            _caretVisible = value
            InvalidateOwner()
        End Set
    End Property

    Public Property ScrollXOffset As Integer
        Get
            Return _scrollXOffset
        End Get
        Set(value As Integer)
            _scrollXOffset = Math.Max(0, value)
            InvalidateOwner()
        End Set
    End Property

    Public Sub SetText(value As String, Optional caretColumn As Integer = -1,
                       Optional resetScroll As Boolean = False, Optional raiseTextChanged As Boolean = True)
        Dim v As String = If(value, String.Empty)
        Dim changed As Boolean = _text <> v
        If Not changed AndAlso caretColumn < 0 AndAlso Not resetScroll Then Return

        If changed Then AssignTextValue(v)
        If caretColumn >= 0 Then
            _caretCol = Math.Max(0, Math.Min(_text.Length, caretColumn))
        ElseIf changed Then
            _caretCol = _text.Length
        End If
        If resetScroll Then _scrollXOffset = 0
        ClearSelection(False)
        EnsureCaretVisible()
        InvalidateOwner()
        If changed AndAlso raiseTextChanged Then RaiseEvent TextChanged(_owner, EventArgs.Empty)
    End Sub

    Public Sub Draw(rt As ID2D1RenderTarget,
                    Optional textFormatCache As D3D_D2DInterop.TextFormatCache = Nothing,
                    Optional brushCache As D3D_D2DInterop.SolidColorBrushCache = Nothing)
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
                    New RectangleF(area.X + waterAlignOff, area.Y, area.Width, area.Height), DpiScale(), False, textFormatCache, brushCache)
            End If

            If Not isEmpty Then
                If _hasSelection Then DrawSelection_D2D(rt, singleLineY, CInt(area.X), CInt(area.Width), brushCache)
                Dim alignOff As Integer = GetAlignOffsetX(_text, CInt(area.Width))
                DrawSingleLineText_D2D(rt, _text, _owner.Font, ForeColor,
                    New RectangleF(area.X + alignOff - _scrollXOffset, area.Y, Short.MaxValue, area.Height), DpiScale(), False, textFormatCache, brushCache)
            End If

            If IsFocused() AndAlso _caretVisible AndAlso Editable Then
                DrawCaret_D2D(rt, CInt(area.X), CInt(area.Y), brushCache)
            End If
        Finally
            rt.PopAxisAlignedClip()
        End Try
    End Sub

    Public Sub DrawGpu(context As D3D_PaintContext)
        If context Is Nothing Then Return
        Dim area As RectangleF = GetTextArea()
        If area.Width <= 0 OrElse area.Height <= 0 Then Return

        Using context.PushClip(area)
            Dim singleLineY As Integer = CInt(area.Y + (area.Height - LineHeight) \ 2)
            Dim isEmpty As Boolean = String.IsNullOrEmpty(_text)

            If isEmpty AndAlso Not String.IsNullOrEmpty(WaterText) Then
                Dim waterAlignOff As Integer = GetAlignOffsetX(WaterText, CInt(area.Width))
                context.DrawText(WaterText, _owner.Font, WaterTextForeColor,
                                 New RectangleF(area.X + waterAlignOff, area.Y, area.Width, area.Height),
                                 Vortice.DirectWrite.TextAlignment.Leading,
                                 Vortice.DirectWrite.ParagraphAlignment.Center)
            End If

            If Not isEmpty Then
                If _hasSelection Then DrawSelection_GPU(context, singleLineY, CInt(area.X), CInt(area.Width))
                Dim alignOff As Integer = GetAlignOffsetX(_text, CInt(area.Width))
                context.DrawText(_text, _owner.Font, ForeColor,
                                 New RectangleF(area.X + alignOff - _scrollXOffset, area.Y, Short.MaxValue, area.Height),
                                 Vortice.DirectWrite.TextAlignment.Leading,
                                 Vortice.DirectWrite.ParagraphAlignment.Center)
            End If

            If IsFocused() AndAlso _caretVisible AndAlso Editable Then
                DrawCaret_GPU(context, CInt(area.X), CInt(area.Y))
            End If
        End Using
    End Sub

    Public Function HitTestColumn(x As Integer) As Integer
        Dim area As RectangleF = GetTextArea()
        Dim alignOff As Integer = GetAlignOffsetX(_text, CInt(area.Width))
        Return FindColumnFromX(CInt(x - area.X - alignOff + _scrollXOffset))
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
        InvalidateOwner()
    End Sub

    Public Sub MoveCaret(deltaCol As Integer, extend As Boolean)
        If Not extend AndAlso _hasSelection AndAlso deltaCol <> 0 Then
            Dim minC As Integer = Math.Min(_selAnchorCol, _caretCol)
            Dim maxC As Integer = Math.Max(_selAnchorCol, _caretCol)
            _caretCol = If(deltaCol < 0, minC, maxC)
            ClearSelection(False)
            EnsureCaretVisible()
            InvalidateOwner()
            Return
        End If
        If Not extend Then _selAnchorCol = _caretCol
        _caretCol = Math.Max(0, Math.Min(_text.Length, _caretCol + deltaCol))
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        InvalidateOwner()
    End Sub

    Public Sub MoveCaretHome(extend As Boolean)
        If Not extend Then _selAnchorCol = _caretCol
        _caretCol = 0
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        InvalidateOwner()
    End Sub

    Public Sub MoveCaretEnd(extend As Boolean)
        If Not extend Then _selAnchorCol = _caretCol
        _caretCol = _text.Length
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        InvalidateOwner()
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
        InvalidateOwner()
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
        InvalidateOwner()
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
        AssignTextValue(candidate)
        _caretCol = startCol + clean.Length
        ClearSelection(False)
        RaiseTextChangedFromEdit()
    End Sub

    Public Sub HandleBackspace()
        Dim changed As Boolean = False
        If _hasSelection Then
            changed = DeleteSelectionCore(False)
        ElseIf _caretCol > 0 Then
            AssignTextValue(String.Concat(_text.AsSpan(0, _caretCol - 1), _text.AsSpan(_caretCol)))
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
            AssignTextValue(String.Concat(_text.AsSpan(0, _caretCol), _text.AsSpan(_caretCol + 1)))
            changed = True
        End If
        If changed Then RaiseTextChangedFromEdit()
    End Sub

    Public Sub SelectAll()
        _selAnchorCol = 0
        _caretCol = _text.Length
        _hasSelection = _text.Length > 0
        InvalidateOwner()
    End Sub

    Public Sub ClearSelection(Optional invalidateOwnerFlag As Boolean = True)
        _hasSelection = False
        _selAnchorCol = _caretCol
        If invalidateOwnerFlag Then InvalidateOwner()
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
        Dim caretX As Integer = MeasurePrefixWidth(_caretCol)
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
        Dim cx As Integer = CInt(area.X + alignOff + MeasurePrefixWidth(_caretCol) - _scrollXOffset)
        Dim cy As Integer = CInt(area.Y + (area.Height - LineHeight) \ 2 + LineHeight)
        Return New Point(cx, cy)
    End Function

    Public Sub StartCaretBlink()
        If _disposed Then Return
        _caretBlinkRequested = True
        _caretVisible = True
        UpdateCaretTimerState()
        InvalidateOwner()
    End Sub

    Public Sub StopCaretBlink()
        _caretBlinkRequested = False
        _caretBlinkTimer.Stop()
        _caretVisible = False
        InvalidateOwner()
    End Sub

    Public Sub ResetCaretBlink()
        If _disposed Then Return
        _caretBlinkRequested = True
        _caretVisible = True
        _caretBlinkTimer.Stop()
        UpdateCaretTimerState()
        InvalidateOwner()
    End Sub

    Private Sub OwnerStateChanged(sender As Object, e As EventArgs)
        UpdateCaretTimerState()
    End Sub

    Private Sub OwnerMetricsChanged(sender As Object, e As EventArgs)
        ClearMeasureCache()
    End Sub

    Private Sub OwnerDisposed(sender As Object, e As EventArgs)
        Dispose()
    End Sub

    Private Sub UpdateCaretTimerState()
        If _disposed Then Return
        Dim shouldRun As Boolean = _caretBlinkRequested AndAlso
                                   Editable AndAlso
                                   _owner.IsHandleCreated AndAlso
                                   Not _owner.IsDisposed AndAlso
                                   _owner.Visible AndAlso
                                   _owner.Enabled AndAlso
                                   IsFocused()
        If shouldRun Then
            If Not _caretBlinkTimer.Enabled Then _caretBlinkTimer.Start()
        ElseIf _caretBlinkTimer.Enabled Then
            _caretBlinkTimer.Stop()
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        Try : _caretBlinkTimer.Stop() : Catch : End Try
        Try : _caretBlinkTimer.Dispose() : Catch : End Try
        Try : RemoveHandler _owner.VisibleChanged, AddressOf OwnerStateChanged : Catch : End Try
        Try : RemoveHandler _owner.HandleCreated, AddressOf OwnerStateChanged : Catch : End Try
        Try : RemoveHandler _owner.HandleDestroyed, AddressOf OwnerStateChanged : Catch : End Try
        Try : RemoveHandler _owner.FontChanged, AddressOf OwnerMetricsChanged : Catch : End Try
        Try : RemoveHandler _owner.DpiChangedAfterParent, AddressOf OwnerMetricsChanged : Catch : End Try
        Try : RemoveHandler _owner.Disposed, AddressOf OwnerDisposed : Catch : End Try
        _prefixWidthCache.Clear()
    End Sub

    Private Function DeleteSelectionCore(invalidateOwnerFlag As Boolean) As Boolean
        If Not _hasSelection Then Return False
        Dim minC As Integer = Math.Min(_selAnchorCol, _caretCol)
        Dim maxC As Integer = Math.Max(_selAnchorCol, _caretCol)
        AssignTextValue(String.Concat(_text.AsSpan(0, minC), _text.AsSpan(maxC)))
        _caretCol = minC
        ClearSelection(invalidateOwnerFlag)
        Return True
    End Function

    Private Sub RaiseTextChangedFromEdit()
        EnsureCaretVisible()
        InvalidateOwner()
        RaiseEvent TextChanged(_owner, EventArgs.Empty)
    End Sub

    Private Sub AssignTextValue(value As String)
        _text = If(value, String.Empty)
        ClearMeasureCache()
    End Sub

    Private Sub UpdateSelectionFromAnchor(extend As Boolean)
        If extend Then
            _hasSelection = (_caretCol <> _selAnchorCol)
        Else
            ClearSelection(False)
        End If
    End Sub

    Private Function GetTextArea() As RectangleF
        If TextAreaProvider IsNot Nothing Then
            Dim provided = TextAreaProvider.Invoke()
            If provided.Width > 0 AndAlso provided.Height > 0 Then Return provided
        End If

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
        If DpiScaleProvider IsNot Nothing Then Return Math.Max(0.01F, DpiScaleProvider.Invoke())
        Return V3_DpiContext.FromControl(_owner).Scale
    End Function

    Private Function IsFocused() As Boolean
        If FocusProvider IsNot Nothing Then Return FocusProvider.Invoke()
        Return _owner.Focused
    End Function

    Private Sub InvalidateOwner()
        If InvalidateAction IsNot Nothing Then
            InvalidateAction.Invoke()
        Else
            V3_InvalidationRouter.RequestRender(_owner, New Rectangle(Point.Empty, _owner.Size))
        End If
    End Sub

    Private Function MeasureWidth(text As String) As Integer
        If String.IsNullOrEmpty(text) Then Return 0
        Return System.Windows.Forms.TextRenderer.MeasureText(text, _owner.Font, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine).Width
    End Function

    Private Function MeasurePrefixWidth(length As Integer) As Integer
        length = Math.Max(0, Math.Min(_text.Length, length))
        EnsureMeasureCacheKey()
        Dim width As Integer
        If _prefixWidthCache.TryGetValue(length, width) Then Return width
        width = If(length = 0, 0, MeasureWidth(_text.Substring(0, length)))
        _prefixWidthCache(length) = width
        Return width
    End Function

    Private Function FindColumnFromX(x As Integer) As Integer
        If String.IsNullOrEmpty(_text) OrElse x <= 0 Then Return 0
        Dim total As Integer = MeasureWidth(_text)
        If x >= total Then Return _text.Length

        Dim lo As Integer = 0
        Dim hi As Integer = _text.Length
        While lo < hi
            Dim mid As Integer = (lo + hi) \ 2
            Dim midX As Integer = MeasurePrefixWidth(mid)
            If midX < x Then
                lo = mid + 1
            Else
                hi = mid
            End If
        End While

        If lo > 0 Then
            Dim prevX As Integer = MeasurePrefixWidth(lo - 1)
            Dim curX As Integer = MeasurePrefixWidth(lo)
            If Math.Abs(x - prevX) <= Math.Abs(curX - x) Then Return lo - 1
        End If
        Return Math.Max(0, Math.Min(_text.Length, lo))
    End Function

    Private Sub EnsureMeasureCacheKey()
        Dim font = _owner.Font
        Dim fontKey As String = font.Name & "|" &
                                font.SizeInPoints.ToString(Globalization.CultureInfo.InvariantCulture) & "|" &
                                CInt(font.Style).ToString(Globalization.CultureInfo.InvariantCulture)
        Dim dpi As Single = DpiScale()
        If _measureFontKey = fontKey AndAlso Math.Abs(_measureDpiScale - dpi) < 0.0001F Then Return
        _measureFontKey = fontKey
        _measureDpiScale = dpi
        _prefixWidthCache.Clear()
    End Sub

    Private Sub ClearMeasureCache()
        _prefixWidthCache.Clear()
        _measureFontKey = String.Empty
        _measureDpiScale = -1.0F
    End Sub

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

    Private Sub DrawSelection_D2D(rt As ID2D1RenderTarget, lineY As Integer, textLeft As Integer, textWidth As Integer,
                                  brushCache As D3D_D2DInterop.SolidColorBrushCache)
        Dim minC As Integer = Math.Min(_selAnchorCol, _caretCol)
        Dim maxC As Integer = Math.Max(_selAnchorCol, _caretCol)
        Dim alignOff As Integer = GetAlignOffsetX(_text, textWidth)
        Dim x1 As Integer = textLeft + alignOff + MeasurePrefixWidth(minC) - _scrollXOffset
        Dim x2 As Integer = textLeft + alignOff + MeasurePrefixWidth(maxC) - _scrollXOffset
        If x2 <= x1 Then Return
        If brushCache IsNot Nothing Then
            rt.FillRectangle(New Vortice.Mathematics.Rect(x1, lineY, x2 - x1, LineHeight), brushCache.Get(rt, SelectionColor))
        Else
            Using br = rt.CreateSolidColorBrush(D3D_D2DInterop.ToColor4(SelectionColor))
                rt.FillRectangle(New Vortice.Mathematics.Rect(x1, lineY, x2 - x1, LineHeight), br)
            End Using
        End If
    End Sub

    Private Sub DrawCaret_D2D(rt As ID2D1RenderTarget, textLeft As Integer, textTop As Integer,
                              brushCache As D3D_D2DInterop.SolidColorBrushCache)
        Dim area As RectangleF = GetTextArea()
        Dim alignOff As Integer = GetAlignOffsetX(_text, CInt(area.Width))
        Dim cx As Integer = textLeft + alignOff + MeasurePrefixWidth(_caretCol) - _scrollXOffset
        Dim lineY As Integer = CInt(textTop + (area.Height - LineHeight) \ 2)
        Dim caretH As Integer = LineHeight - 2
        Dim caretY As Integer = lineY + (LineHeight - caretH) \ 2
        If brushCache IsNot Nothing Then
            rt.FillRectangle(New Vortice.Mathematics.Rect(cx, caretY, CInt(CaretWidth * DpiScale()), caretH), brushCache.Get(rt, CaretColor))
        Else
            Using br = rt.CreateSolidColorBrush(D3D_D2DInterop.ToColor4(CaretColor))
                rt.FillRectangle(New Vortice.Mathematics.Rect(cx, caretY, CInt(CaretWidth * DpiScale()), caretH), br)
            End Using
        End If
    End Sub

    Private Sub DrawSelection_GPU(context As D3D_PaintContext, lineY As Integer, textLeft As Integer, textWidth As Integer)
        Dim minC As Integer = Math.Min(_selAnchorCol, _caretCol)
        Dim maxC As Integer = Math.Max(_selAnchorCol, _caretCol)
        Dim alignOff As Integer = GetAlignOffsetX(_text, textWidth)
        Dim x1 As Integer = textLeft + alignOff + MeasurePrefixWidth(minC) - _scrollXOffset
        Dim x2 As Integer = textLeft + alignOff + MeasurePrefixWidth(maxC) - _scrollXOffset
        If x2 <= x1 Then Return
        context.FillRectangle(New RectangleF(x1, lineY, x2 - x1, LineHeight), SelectionColor)
    End Sub

    Private Sub DrawCaret_GPU(context As D3D_PaintContext, textLeft As Integer, textTop As Integer)
        Dim area As RectangleF = GetTextArea()
        Dim alignOff As Integer = GetAlignOffsetX(_text, CInt(area.Width))
        Dim cx As Integer = textLeft + alignOff + MeasurePrefixWidth(_caretCol) - _scrollXOffset
        Dim lineY As Integer = CInt(textTop + (area.Height - LineHeight) \ 2)
        Dim caretH As Integer = LineHeight - 2
        Dim caretY As Integer = lineY + (LineHeight - caretH) \ 2
        context.FillRectangle(New RectangleF(cx, caretY, CInt(CaretWidth * DpiScale()), caretH), CaretColor)
    End Sub

    Private Shared Sub PushClip_D2D(rt As ID2D1RenderTarget, rect As RectangleF)
        rt.PushAxisAlignedClip(New Vortice.RawRectF(rect.Left, rect.Top, rect.Right, rect.Bottom), AntialiasMode.Aliased)
    End Sub

    Public Shared Sub DrawSingleLineText_D2D(rt As ID2D1RenderTarget, text As String, font As Font, foreColor As Color,
                                             rect As RectangleF, dpiScale As Single, endEllipsis As Boolean,
                                             Optional textFormatCache As D3D_D2DInterop.TextFormatCache = Nothing,
                                             Optional brushCache As D3D_D2DInterop.SolidColorBrushCache = Nothing)
        If String.IsNullOrEmpty(text) OrElse foreColor.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim ownsFormat As Boolean = (textFormatCache Is Nothing)
        Dim fmt As Vortice.DirectWrite.IDWriteTextFormat = Nothing
        Dim ownsBrush As Boolean = (brushCache Is Nothing)
        Dim br As ID2D1Brush = Nothing
        Try
            Dim sizePx As Single = D3D_D2DInterop.GetDWriteFontSizePx(font, dpiScale)
            If textFormatCache IsNot Nothing Then
                fmt = textFormatCache.Get(font, sizePx,
                                          Vortice.DirectWrite.TextAlignment.Leading,
                                          Vortice.DirectWrite.ParagraphAlignment.Center,
                                          endEllipsis,
                                          False)
            Else
                fmt = D3D_TextMeasureHelper.CreateDWriteTextFormat(font, dpiScale)
                fmt.WordWrapping = Vortice.DirectWrite.WordWrapping.NoWrap
                fmt.ParagraphAlignment = Vortice.DirectWrite.ParagraphAlignment.Center
                If endEllipsis Then
                    Try
                        fmt.SetTrimming(New Vortice.DirectWrite.Trimming With {.Granularity = Vortice.DirectWrite.TrimmingGranularity.Character}, Nothing)
                    Catch
                    End Try
                End If
            End If
            If fmt Is Nothing Then Return
            br = If(brushCache IsNot Nothing,
                    DirectCast(brushCache.Get(rt, foreColor), ID2D1Brush),
                    DirectCast(rt.CreateSolidColorBrush(D3D_D2DInterop.ToColor4(foreColor)), ID2D1Brush))
            If br Is Nothing Then Return
            Using layout = D3D_D2DInterop.GetDWriteFactory().CreateTextLayout(text, fmt, rect.Width, rect.Height)
                rt.DrawTextLayout(New Vector2(rect.X, rect.Y), layout, br)
            End Using
        Finally
            If ownsBrush AndAlso br IsNot Nothing Then
                Try : br.Dispose() : Catch : End Try
            End If
            If ownsFormat AndAlso fmt IsNot Nothing Then
                Try : fmt.Dispose() : Catch : End Try
            End If
        End Try
    End Sub

    Private Shared Function DefaultFilterText(text As String) As String
        Return If(text, String.Empty).Replace(vbCr, String.Empty).Replace(vbLf, String.Empty)
    End Function
End Class

