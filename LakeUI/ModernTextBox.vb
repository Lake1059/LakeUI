Imports System.ComponentModel
Imports System.Drawing.Drawing2D
Imports System.Runtime.InteropServices
Imports System.Text

<DefaultEvent("TextChanged")>
Public Class ModernTextBox
    Public Shadows Event TextChanged As EventHandler
#Region "─── Win32 / IME P/Invoke ───"
    Private Const WM_CHAR As Integer = &H102
    Private Const WM_IME_COMPOSITION As Integer = &H10F
    Private Const WM_IME_STARTCOMPOSITION As Integer = &H10D
    Private Const WM_IME_ENDCOMPOSITION As Integer = &H10E
    Private Const WM_GETDLGCODE As Integer = &H87
    Private Const GCS_RESULTSTR As Integer = &H800
    Private Const CFS_POINT As Integer = &H2
    Private Const DLGC_WANTCHARS As Integer = &H80
    Private Const DLGC_WANTALLKEYS As Integer = &H4
    Private Const IACE_DEFAULT As Integer = &H10
    <DllImport("imm32.dll")>
    Private Shared Function ImmGetContext(hWnd As IntPtr) As IntPtr
    End Function
    <DllImport("imm32.dll")>
    Private Shared Function ImmReleaseContext(hWnd As IntPtr, hIMC As IntPtr) As Boolean
    End Function
    <DllImport("imm32.dll", EntryPoint:="ImmGetCompositionStringW")>
    Private Shared Function ImmGetCompositionBytes(hIMC As IntPtr, dwIndex As Integer, lpBuf As Byte(), dwBufLen As Integer) As Integer
    End Function
    <DllImport("imm32.dll")>
    Private Shared Function ImmSetCompositionWindow(hIMC As IntPtr, ByRef lpCompForm As COMPOSITIONFORM) As Boolean
    End Function
    <DllImport("imm32.dll")>
    Private Shared Function ImmAssociateContextEx(hWnd As IntPtr, hIMC As IntPtr, dwFlags As Integer) As Boolean
    End Function
    <StructLayout(LayoutKind.Sequential)>
    Private Structure COMPOSITIONFORM
        Public dwStyle As Integer
        Public ptCurrentPos As Point
        Public rcArea As Rectangle
    End Structure
#End Region
#Region "─── 内部数据结构 ───"
    Private Structure TextSnapshot
        Public Lines As String()
        Public CaretLine As Integer
        Public CaretCol As Integer
        Public Sub New(lines As IEnumerable(Of String), caretLine As Integer, caretCol As Integer)
            Me.Lines = lines.ToArray()
            Me.CaretLine = caretLine
            Me.CaretCol = caretCol
        End Sub
    End Structure
    Private Structure VisualLineInfo
        Public LogicalLine As Integer
        Public StartCol As Integer
        Public Length As Integer
        Public Sub New(logicalLine As Integer, startCol As Integer, length As Integer)
            Me.LogicalLine = logicalLine
            Me.StartCol = startCol
            Me.Length = length
        End Sub
    End Structure
#End Region
#Region "─── 字段 ───"
    Private _lines As New List(Of String) From {String.Empty}
    Private _caretLine As Integer = 0
    Private _caretCol As Integer = 0
    Private _selAnchorLine As Integer = 0
    Private _selAnchorCol As Integer = 0
    Private _hasSelection As Boolean = False
    Private Const MAX_UNDO As Integer = 10
    Private _undoStack As New List(Of TextSnapshot)
    Private _caretVisible As Boolean = True
    Private _caretBlinkTimer As New Timer() With {.Interval = 530}
    Private _scrollLineOffset As Integer = 0
    Private _scrollXOffset As Integer = 0
    Private _scrollBarVisible As Boolean = False
    Private _scrollThumbRect As Rectangle = Rectangle.Empty
    Private _scrollTrackRect As Rectangle = Rectangle.Empty
    Private _scrollThumbDragging As Boolean = False
    Private _scrollThumbDragStartY As Integer = 0
    Private _scrollThumbDragStartOffset As Integer = 0
    Private _scrollThumbHover As Boolean = False
    Private _mouseDownSelecting As Boolean = False
    Private _imeComposing As Boolean = False
    Private _visualLines As New List(Of VisualLineInfo)
    Private _autoScrollTimer As New Timer() With {.Interval = 50}
    Private _lastMousePos As Point = Point.Empty
#End Region

#Region "─── 属性 ───"
    <Category("LakeUI"), Description("主要文本"), DefaultValue(GetType(String), ""), Browsable(True),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
    Public Overrides Property Text As String
        Get
            Return String.Join(vbCrLf, _lines)
        End Get
        Set(value As String)
            Dim normalized As String = If(value, "").Replace(vbCr, "")
            If String.Join(vbLf, _lines) = normalized Then Return
            PushUndo()
            SetLinesFromString(normalized)
            _caretLine = 0
            _caretCol = 0
            _scrollXOffset = 0
            _scrollLineOffset = 0
            ClearSelection()
            NotifyTextChanged()
        End Set
    End Property
    Private Function ShouldSerializeText() As Boolean
        Return Not String.IsNullOrEmpty(Text)
    End Function
    Public Overrides Sub ResetText()
        Text = String.Empty
    End Sub

    Private 背景颜色 As Color = Color.FromArgb(36, 36, 36)
    <Category("LakeUI"), Description("背景颜色"), DefaultValue(GetType(Color), "36,36,36"), Browsable(True)>
    Public Property BackColor1 As Color
        Get
            Return 背景颜色
        End Get
        Set(value As Color)
            SetValue(背景颜色, value)
        End Set
    End Property

    Private 文本颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("文本颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Overrides Property ForeColor As Color
        Get
            Return 文本颜色
        End Get
        Set(value As Color)
            SetValue(文本颜色, value)
        End Set
    End Property

    Private 行高 As Integer = 25
    <Category("LakeUI"), Description("行高"), DefaultValue(GetType(Integer), "25"), Browsable(True)>
    Public Property LineHeight As Integer
        Get
            Return 行高
        End Get
        Set(value As Integer)
            行高 = Math.Max(10, value)
            UpdateScrollBar()
            Invalidate()
        End Set
    End Property

    Private 光标线宽 As Integer = 2
    <Category("LakeUI"), Description("光标线宽"), DefaultValue(GetType(Integer), "2"), Browsable(True)>
    Public Property CaretWidth As Integer
        Get
            Return 光标线宽
        End Get
        Set(value As Integer)
            光标线宽 = Math.Max(1, value)
            Invalidate()
        End Set
    End Property

    Private 光标颜色 As Color = Color.FromArgb(220, 220, 220)
    <Category("LakeUI"), Description("光标颜色"), DefaultValue(GetType(Color), "220,220,220"), Browsable(True)>
    Public Property CaretColor As Color
        Get
            Return 光标颜色
        End Get
        Set(value As Color)
            SetValue(光标颜色, value)
        End Set
    End Property

    Private 选区背景色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("选区背景色"), DefaultValue(GetType(Color), "80,80,80"), Browsable(True)>
    Public Property SelectionColor As Color
        Get
            Return 选区背景色
        End Get
        Set(value As Color)
            SetValue(选区背景色, value)
        End Set
    End Property

    Private 边框颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("边框颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property BorderColor As Color
        Get
            Return 边框颜色
        End Get
        Set(value As Color)
            SetValue(边框颜色, value)
        End Set
    End Property

    Private 有焦点时边框颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("有焦点时边框颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property BorderColorFocus As Color
        Get
            Return 有焦点时边框颜色
        End Get
        Set(v As Color)
            SetValue(有焦点时边框颜色, v)
        End Set
    End Property

    Private 边框宽度 As Integer = 1
    <Category("LakeUI"), Description("边框宽度"), DefaultValue(GetType(Integer), "1"), Browsable(True)>
    Public Property BorderSize As Integer
        Get
            Return 边框宽度
        End Get
        Set(value As Integer)
            SetValue(边框宽度, value)
        End Set
    End Property

    Private 边框圆角半径 As Integer = 0
    <Category("LakeUI"), Description("边框圆角半径"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property BorderRadius As Integer
        Get
            Return 边框圆角半径
        End Get
        Set(value As Integer)
            SetValue(边框圆角半径, value)
        End Set
    End Property

    Private 启用多行 As Boolean = False
    <Category("LakeUI"), Description("启用多行"), DefaultValue(GetType(Boolean), "False"), Browsable(True)>
    Public Property MultiLine As Boolean
        Get
            Return 启用多行
        End Get
        Set(value As Boolean)
            启用多行 = value
            RebuildVisualLines()
            UpdateScrollBar()
            Invalidate()
        End Set
    End Property

    Private 启用只读模式
    <Category("LakeUI"), Description("启用后阻止用户更改文本"), DefaultValue(GetType(Boolean), "False"), Browsable(True)>
    Public Property [ReadOnly] As Boolean
        Get
            Return 启用只读模式
        End Get
        Set(value As Boolean)
            启用只读模式 = value
        End Set
    End Property

    <Description("获取总行数"), Browsable(False)>
    Public ReadOnly Property LineCount As Integer
        Get
            Return _lines.Count
        End Get
    End Property

    Public Enum TextAlignMode
        Left = 0
        Center = 1
        Right = 2
    End Enum
    Private 文本对齐 As TextAlignMode = TextAlignMode.Left
    <Category("LakeUI"), Description("文本对齐方式，仅单行模式生效"), DefaultValue(TextAlignMode.Left), Browsable(True)>
    Public Property TextAlign As TextAlignMode
        Get
            Return 文本对齐
        End Get
        Set(value As TextAlignMode)
            SetValue(文本对齐, value)
        End Set
    End Property

    Private 水印文本 As String = ""
    <Category("LakeUI"), Description("水印文本"), DefaultValue(GetType(String), ""), Browsable(True)>
    Public Property WaterText As String
        Get
            Return 水印文本
        End Get
        Set(value As String)
            SetValue(水印文本, value)
        End Set
    End Property

    Private 水印颜色 As Color = Color.DarkGray
    <Category("LakeUI"), Description("水印颜色"), DefaultValue(GetType(Color), "DarkGray"), Browsable(True)>
    Public Property WaterTextForeColor As Color
        Get
            Return 水印颜色
        End Get
        Set(value As Color)
            SetValue(水印颜色, value)
        End Set
    End Property

    Private 滚动条宽度 As Integer = 10
    <Category("LakeUI"), Description("滚动条宽度"), DefaultValue(GetType(Integer), "10"), Browsable(True)>
    Public Property ScrollBarWidth As Integer
        Get
            Return 滚动条宽度
        End Get
        Set(value As Integer)
            滚动条宽度 = Math.Max(2, value)
            Invalidate()
        End Set
    End Property

    Private 滚动条颜色 As Color = Color.FromArgb(140, 140, 140)
    <Category("LakeUI"), Description("滚动条滑块颜色"), DefaultValue(GetType(Color), "140,140,140"), Browsable(True)>
    Public Property ScrollBarColor As Color
        Get
            Return 滚动条颜色
        End Get
        Set(value As Color)
            SetValue(滚动条颜色, value)
        End Set
    End Property

    Private 滚动条悬停颜色 As Color = Color.FromArgb(200, 200, 200)
    <Category("LakeUI"), Description("滚动条滑块悬停/拖拽颜色"), DefaultValue(GetType(Color), "200,200,200"), Browsable(True)>
    Public Property ScrollBarHoverColor As Color
        Get
            Return 滚动条悬停颜色
        End Get
        Set(value As Color)
            SetValue(滚动条悬停颜色, value)
        End Set
    End Property

    Private 滚动条轨道颜色 As Color = Color.FromArgb(20, 255, 255, 255)
    <Category("LakeUI"), Description("滚动条轨道颜色"), DefaultValue(GetType(Color), "20, 255, 255, 255"), Browsable(True)>
    Public Property ScrollBarTrackColor As Color
        Get
            Return 滚动条轨道颜色
        End Get
        Set(value As Color)
            SetValue(滚动条轨道颜色, value)
        End Set
    End Property

    Private 超采样倍率 As Integer = 1
    <Category("LakeUI"), Description(Class1.超采样抗锯齿描述词), DefaultValue(GetType(Class1.SuperSamplingScaleEnum), "OFF"), Browsable(True)>
    Public Property SuperSamplingScale As Class1.SuperSamplingScaleEnum
        Get
            Return 超采样倍率
        End Get
        Set(value As Class1.SuperSamplingScaleEnum)
            SetValue(超采样倍率, value)
        End Set
    End Property

    Private _wordWrap As Boolean = True
    <Category("LakeUI"), Description("自动换行（多行模式）"), DefaultValue(GetType(Boolean), "True"), Browsable(True)>
    Public Property WordWrap As Boolean
        Get
            Return _wordWrap
        End Get
        Set(value As Boolean)
            If _wordWrap <> value Then
                _wordWrap = value
                RebuildVisualLines()
                UpdateScrollBar()
                EnsureCaretVisible()
                Invalidate()
            End If
        End Set
    End Property
#End Region

#Region "─── 初始化 ───"
    Public Sub New()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.Selectable Or
                 ControlStyles.StandardClick, True)
        UpdateStyles()
        AddHandler _caretBlinkTimer.Tick, Sub()
                                              _caretVisible = Not _caretVisible
                                              Invalidate()
                                          End Sub
        AddHandler _autoScrollTimer.Tick, AddressOf AutoScrollTick
        RebuildVisualLines()
    End Sub
    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        ImmAssociateContextEx(Handle, IntPtr.Zero, IACE_DEFAULT)
        RebuildVisualLines()
        _caretBlinkTimer.Start()
    End Sub
    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        _caretBlinkTimer.Stop()
        _autoScrollTimer.Stop()
        MyBase.OnHandleDestroyed(e)
    End Sub
#End Region

#Region "─── 绘制 ───"
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim w As Integer = ClientRectangle.Width
        Dim h As Integer = ClientRectangle.Height
        Dim hasRadius As Boolean = 边框圆角半径 > 0
        Dim boundsRect As New RectangleF(0, 0, w - 1, h - 1)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 / 2.0F
            boundsRect.Inflate(-half, -half)
        End If
        Dim bc As Color = If(Focused, 有焦点时边框颜色, 边框颜色)
        If 超采样倍率 > 1 Then
            Using bmp As New Bitmap(w * 超采样倍率, h * 超采样倍率)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.ScaleTransform(超采样倍率, 超采样倍率)
                    DrawBackground(g, hasRadius, boundsRect, bc)
                End Using
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                e.Graphics.DrawImage(bmp, 0, 0, w, h)
            End Using
        Else
            DrawBackground(e.Graphics, hasRadius, boundsRect, bc)
        End If
        DrawTextContent(e.Graphics, w, h)
        DrawScrollBar(e.Graphics, w, h)
    End Sub

    Private Sub DrawBackground(g As Graphics, hasRadius As Boolean, boundsRect As RectangleF, borderClr As Color)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        g.InterpolationMode = InterpolationMode.HighQualityBicubic
        If hasRadius Then
            Using path As GraphicsPath = Class1.创建圆角矩形路径(boundsRect, 边框圆角半径)
                Class1.绘制圆角背景(g, path, boundsRect, 背景颜色, Color.Empty, Orientation.Horizontal)
                Class1.绘制圆角边框(g, path, borderClr, 边框宽度)
            End Using
        Else
            Class1.绘制矩形背景(g, boundsRect, 背景颜色, Color.Empty, Orientation.Horizontal)
            Class1.绘制矩形边框(g, boundsRect, borderClr, 边框宽度)
        End If
    End Sub

    Private Sub DrawTextContent(g As Graphics, w As Integer, h As Integer)
        g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit
        Dim scrollW As Integer = If(_scrollBarVisible, 滚动条宽度 + 6, 0)
        Dim bi As Integer = 边框宽度
        Dim textLeft As Integer = Math.Max(Padding.Left, bi)
        Dim textTop As Integer = Math.Max(Padding.Top, bi)
        Dim textRight As Integer = Math.Max(Padding.Right, bi)
        Dim textBottom As Integer = Math.Max(Padding.Bottom, bi)
        Dim textWidth As Integer = w - textLeft - textRight - scrollW
        Dim textHeight As Integer = h - textTop - textBottom
        g.SetClip(New Rectangle(textLeft, textTop, textWidth, textHeight))
        Dim isSingleLine As Boolean = Not 启用多行
        Dim singleLineY As Integer = textTop + (textHeight - 行高) \ 2
        Dim isEmpty As Boolean = (_lines.Count = 1 AndAlso _lines(0).Length = 0)
        If isEmpty AndAlso Not String.IsNullOrEmpty(水印文本) Then
            Dim waterLineY As Integer = If(isSingleLine, singleLineY, textTop)
            Dim waterAlignOff As Integer = GetAlignOffsetX(水印文本, textWidth)
            TextRenderer.DrawText(g, 水印文本, Font,
                New Point(textLeft + waterAlignOff, waterLineY + (行高 - FontHeight) \ 2),
                水印颜色, TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
        End If
        Dim visibleLines As Integer = VisibleLineCount()
        Dim startVi As Integer = _scrollLineOffset
        Dim endVi As Integer = Math.Min(_visualLines.Count - 1, startVi + visibleLines + 1)
        For vi As Integer = startVi To endVi
            Dim vl = _visualLines(vi)
            Dim lineY As Integer = If(isSingleLine, singleLineY,
                textTop + (vi - _scrollLineOffset) * 行高)
            Dim fullLineStr As String = _lines(vl.LogicalLine)
            Dim lineStr As String = fullLineStr.Substring(vl.StartCol, vl.Length)
            If _hasSelection Then
                DrawVisualLineSelection(g, vl, lineY, textLeft, textWidth)
            End If
            If lineStr.Length > 0 Then
                Dim wrapActive As Boolean = IsWordWrapActive()
                Dim alignOff As Integer = If(wrapActive, 0, GetAlignOffsetX(fullLineStr, textWidth))
                Dim scrollX As Integer = If(wrapActive, 0, _scrollXOffset)
                Dim textY As Integer = lineY + (行高 - FontHeight) \ 2
                TextRenderer.DrawText(g, lineStr, Font,
                    New Point(textLeft + alignOff - scrollX, textY),
                    ForeColor,
                    TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
            End If
        Next
        If Focused AndAlso _caretVisible Then
            DrawCaret(g, textLeft, textTop)
        End If
        g.ResetClip()
    End Sub

    Private Sub DrawVisualLineSelection(g As Graphics, vl As VisualLineInfo, lineY As Integer, textLeft As Integer, textWidth As Integer)
        Dim minL, minC, maxL, maxC As Integer
        GetOrderedSelection(minL, minC, maxL, maxC)
        Dim li As Integer = vl.LogicalLine
        If li < minL OrElse li > maxL Then Return
        Dim selStart As Integer = If(li = minL, minC, 0)
        Dim selEnd As Integer = If(li = maxL, maxC, _lines(li).Length)
        Dim vlEnd As Integer = vl.StartCol + vl.Length
        Dim drawStart As Integer = Math.Max(selStart, vl.StartCol)
        Dim drawEnd As Integer = Math.Min(selEnd, vlEnd)
        If drawStart > drawEnd Then Return
        If drawStart = drawEnd AndAlso Not (selEnd > vlEnd OrElse li < maxL) Then Return
        Dim wrapActive As Boolean = IsWordWrapActive()
        Dim fullLineStr As String = _lines(li)
        Dim alignOff As Integer = If(wrapActive, 0, GetAlignOffsetX(fullLineStr, textWidth))
        Dim scrollX As Integer = If(wrapActive, 0, _scrollXOffset)
        Dim x1 As Integer = textLeft + alignOff + MeasureWidth(fullLineStr.Substring(vl.StartCol, drawStart - vl.StartCol)) - scrollX
        Dim x2 As Integer
        If selEnd > vlEnd OrElse li < maxL Then
            x2 = textLeft + alignOff + MeasureWidth(fullLineStr.Substring(vl.StartCol, vl.Length)) + 6 - scrollX
        Else
            x2 = textLeft + alignOff + MeasureWidth(fullLineStr.Substring(vl.StartCol, drawEnd - vl.StartCol)) - scrollX
        End If
        If x2 <= x1 Then Return
        Using br As New SolidBrush(选区背景色)
            g.FillRectangle(br, x1, lineY, x2 - x1, 行高)
        End Using
    End Sub

    Private Sub DrawCaret(g As Graphics, textLeft As Integer, textTop As Integer)
        Dim vi As Integer = GetVisualLineIndex(_caretLine, _caretCol)
        If vi < _scrollLineOffset OrElse vi >= _scrollLineOffset + VisibleLineCount() + 2 Then
            Return
        End If
        Dim vl = _visualLines(vi)
        Dim lineStr As String = _lines(_caretLine)
        Dim wrapActive As Boolean = IsWordWrapActive()
        Dim alignOff As Integer = If(wrapActive, 0, GetAlignOffsetX(lineStr, TextAreaWidth()))
        Dim scrollX As Integer = If(wrapActive, 0, _scrollXOffset)
        Dim cx As Integer = textLeft + alignOff + MeasureWidth(lineStr.Substring(vl.StartCol, _caretCol - vl.StartCol)) - scrollX
        Dim lineY As Integer
        If Not 启用多行 Then
            Dim bi2 As Integer = 边框宽度
            Dim textHeight As Integer = ClientRectangle.Height - Math.Max(Padding.Top, bi2) - Math.Max(Padding.Bottom, bi2)
            lineY = textTop + (textHeight - 行高) \ 2
        Else
            lineY = textTop + (vi - _scrollLineOffset) * 行高
        End If
        Dim caretH As Integer = 行高 - 2
        Dim caretY As Integer = lineY + (行高 - caretH) \ 2
        Using br As New SolidBrush(光标颜色)
            g.FillRectangle(br, cx, caretY, 光标线宽, caretH)
        End Using
    End Sub

    Private Sub DrawScrollBar(g As Graphics, w As Integer, h As Integer)
        If Not _scrollBarVisible Then Return
        Dim oldSmooth = g.SmoothingMode
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim inset As Integer = Math.Max(边框宽度, If(边框圆角半径 > 0, 边框圆角半径 \ 2, 0))
        Dim margin As Integer = 2
        Dim sbW As Integer = 滚动条宽度
        Dim sbX As Integer = w - sbW - inset - margin
        Dim sbY As Integer = inset + margin
        Dim sbH As Integer = h - (inset + margin) * 2
        If sbH <= 0 OrElse sbW <= 0 Then Return
        _scrollTrackRect = New Rectangle(sbX - margin, sbY, sbW + margin * 2, sbH)
        Dim total As Integer = _visualLines.Count
        Dim vis As Integer = VisibleLineCount()
        Dim maxOffset As Integer = Math.Max(0, total - vis)
        Dim thumbH As Integer = Math.Max(20, CInt(sbH * vis / Math.Max(1, total)))
        Dim thumbY As Integer = sbY
        If maxOffset > 0 Then
            thumbY = sbY + CInt((sbH - thumbH) * _scrollLineOffset / maxOffset)
        End If
        _scrollThumbRect = New Rectangle(sbX - margin, thumbY, sbW + margin * 2, thumbH)
        Dim oldClip As Region = g.Clip.Clone()
        If 边框圆角半径 > 0 Then
            Dim clipRect As New RectangleF(0, 0, w - 1, h - 1)
            If 边框宽度 > 0 Then
                Dim half As Single = 边框宽度 / 2.0F
                clipRect.Inflate(-half, -half)
            End If
            Using path As GraphicsPath = Class1.创建圆角矩形路径(clipRect, 边框圆角半径)
                g.SetClip(path, CombineMode.Replace)
            End Using
        End If
        If 滚动条轨道颜色.A > 0 Then
            Dim trackRadius As Integer = Math.Min(sbW \ 2, sbH \ 2)
            Using trackPath As GraphicsPath = Class1.创建圆角矩形路径(New RectangleF(sbX, sbY, sbW, sbH), trackRadius)
                Using br As New SolidBrush(滚动条轨道颜色)
                    g.FillPath(br, trackPath)
                End Using
            End Using
        End If
        Dim thumbColor As Color = If(_scrollThumbDragging OrElse _scrollThumbHover, 滚动条悬停颜色, 滚动条颜色)
        Dim thumbRadius As Integer = Math.Min(sbW \ 2, thumbH \ 2)
        Using thumbPath As GraphicsPath = Class1.创建圆角矩形路径(New RectangleF(sbX, thumbY, sbW, thumbH), thumbRadius)
            Using br As New SolidBrush(thumbColor)
                g.FillPath(br, thumbPath)
            End Using
        End Using
        g.Clip = oldClip
        g.SmoothingMode = oldSmooth
    End Sub

#End Region

#Region "─── 消息处理 (WndProc) ───"
    Protected Overrides Sub WndProc(ByRef m As Message)
        Select Case m.Msg
            Case WM_GETDLGCODE
                m.Result = New IntPtr(DLGC_WANTCHARS Or DLGC_WANTALLKEYS)
                Return
            Case WM_IME_STARTCOMPOSITION
                _imeComposing = True
                UpdateImeWindow()
                MyBase.WndProc(m)
            Case WM_IME_ENDCOMPOSITION
                _imeComposing = False
                MyBase.WndProc(m)
            Case WM_IME_COMPOSITION
                Dim lp As Integer = m.LParam.ToInt32()
                UpdateImeWindow()
                If (lp And GCS_RESULTSTR) <> 0 Then
                    Dim hIMC As IntPtr = ImmGetContext(Handle)
                    If hIMC <> IntPtr.Zero Then
                        Try
                            Dim byteLen As Integer = ImmGetCompositionBytes(hIMC, GCS_RESULTSTR, Nothing, 0)
                            If byteLen > 0 Then
                                Dim buf(byteLen - 1) As Byte
                                Dim unused = ImmGetCompositionBytes(hIMC, GCS_RESULTSTR, buf, byteLen)
                                Dim result As String = Encoding.Unicode.GetString(buf, 0, byteLen)
                                PushUndo()
                                InsertTextCore(result)
                            End If
                        Finally
                            ImmReleaseContext(Handle, hIMC)
                        End Try
                    End If
                Else
                    MyBase.WndProc(m)
                End If
            Case WM_CHAR
                If Not _imeComposing Then
                    HandleWmChar(m.WParam.ToInt32())
                End If
            Case Else
                MyBase.WndProc(m)
        End Select
    End Sub

#End Region

#Region "─── 字符输入 (WM_CHAR) ───"
    Private Sub HandleWmChar(charCode As Integer)
        Select Case charCode
            Case 1  ' Ctrl+A
                SelectAll()
            Case 3  ' Ctrl+C
                CopySelection()
            Case 22 ' Ctrl+V
                PasteText()
            Case 24 ' Ctrl+X
                CutSelection()
            Case 26 ' Ctrl+Z
                Undo()
            Case 8  ' Backspace
                If Not 启用只读模式 Then HandleBackspace()
            Case 13 ' Enter
                If Not 启用只读模式 AndAlso 启用多行 Then
                    PushUndo()
                    DeleteSelection()
                    InsertNewLine()
                End If
            Case Else
                If Not 启用只读模式 Then
                    Dim ch As Char = ChrW(charCode)
                    If Not Char.IsControl(ch) Then
                        PushUndo()
                        DeleteSelection()
                        InsertTextCore(ch.ToString())
                    End If
                End If
        End Select
        ResetCaretBlink()
    End Sub
#End Region

#Region "─── 键盘导航 (OnKeyDown) ───"
    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        Dim shift As Boolean = e.Shift
        Dim ctrl As Boolean = e.Control
        Select Case e.KeyCode
            Case Keys.Left
                If ctrl Then MoveCaretWordLeft(shift) Else MoveCaret(-1, 0, shift)
                e.Handled = True
            Case Keys.Right
                If ctrl Then MoveCaretWordRight(shift) Else MoveCaret(1, 0, shift)
                e.Handled = True
            Case Keys.Up
                If 启用多行 Then MoveCaret(0, -1, shift) : e.Handled = True
            Case Keys.Down
                If 启用多行 Then MoveCaret(0, 1, shift) : e.Handled = True
            Case Keys.Home
                MoveCaretHome(shift, ctrl)
                e.Handled = True
            Case Keys.End
                MoveCaretEnd(shift, ctrl)
                e.Handled = True
            Case Keys.Delete
                If Not 启用只读模式 Then HandleDelete()
                e.Handled = True
            Case Keys.PageUp
                If 启用多行 Then MoveCaret(0, -VisibleLineCount(), shift) : e.Handled = True
            Case Keys.PageDown
                If 启用多行 Then MoveCaret(0, VisibleLineCount(), shift) : e.Handled = True
        End Select
        If e.Handled Then ResetCaretBlink()
    End Sub
    Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
        Select Case keyData And Keys.KeyCode
            Case Keys.Left, Keys.Right, Keys.Up, Keys.Down,
                 Keys.Home, Keys.End, Keys.PageUp, Keys.PageDown,
                 Keys.Delete, Keys.Enter
                Return True
        End Select
        Return MyBase.IsInputKey(keyData)
    End Function
    Protected Overrides Function IsInputChar(charCode As Char) As Boolean
        Return True
    End Function
#End Region

#Region "─── 鼠标处理 ───"
    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        Focus()
        If e.Button = MouseButtons.Left Then
            If _scrollBarVisible Then
                If _scrollThumbRect.Contains(e.Location) Then
                    _scrollThumbDragging = True
                    _scrollThumbDragStartY = e.Y
                    _scrollThumbDragStartOffset = _scrollLineOffset
                    Return
                ElseIf _scrollTrackRect.Contains(e.Location) Then
                    Dim maxOffset As Integer = Math.Max(0, _visualLines.Count - VisibleLineCount())
                    If e.Y < _scrollThumbRect.Y Then
                        _scrollLineOffset = Math.Max(0, _scrollLineOffset - VisibleLineCount())
                    Else
                        _scrollLineOffset = Math.Min(maxOffset, _scrollLineOffset + VisibleLineCount())
                    End If
                    UpdateScrollBar()
                    Invalidate()
                    Return
                End If
            End If
            _mouseDownSelecting = True
            Dim pos As Point = HitTest(e.X, e.Y)
            _caretLine = pos.Y
            _caretCol = pos.X
            _selAnchorLine = _caretLine
            _selAnchorCol = _caretCol
            _hasSelection = False
            ResetCaretBlink()
            Invalidate()
        End If
    End Sub
    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If _scrollThumbDragging Then
            Dim trackH As Integer = _scrollTrackRect.Height
            Dim total As Integer = _visualLines.Count
            Dim vis As Integer = VisibleLineCount()
            Dim maxOffset As Integer = Math.Max(0, total - vis)
            Dim thumbH As Integer = Math.Max(20, CInt(trackH * vis / Math.Max(1, total)))
            Dim usableH As Integer = trackH - thumbH
            If usableH > 0 Then
                Dim dy As Integer = e.Y - _scrollThumbDragStartY
                Dim newOffset As Integer = _scrollThumbDragStartOffset + CInt(dy * maxOffset / usableH)
                _scrollLineOffset = Math.Max(0, Math.Min(maxOffset, newOffset))
                Invalidate()
            End If
            Return
        End If
        If _scrollBarVisible Then
            Dim wasHover As Boolean = _scrollThumbHover
            _scrollThumbHover = _scrollThumbRect.Contains(e.Location)
            If _scrollThumbHover <> wasHover Then Invalidate()
            Cursor = If(_scrollTrackRect.Contains(e.Location), Cursors.Default, Cursors.IBeam)
        Else
            Cursor = Cursors.IBeam
        End If
        If _mouseDownSelecting AndAlso e.Button = MouseButtons.Left Then
            _lastMousePos = e.Location
            Dim pos As Point = HitTest(e.X, e.Y)
            _caretLine = pos.Y
            _caretCol = pos.X
            _hasSelection = (_caretLine <> _selAnchorLine OrElse _caretCol <> _selAnchorCol)
            EnsureCaretVisible()
            If 启用多行 AndAlso (e.Y < Math.Max(Padding.Top, 边框宽度) OrElse e.Y > ClientRectangle.Height - Math.Max(Padding.Bottom, 边框宽度)) Then
                If Not _autoScrollTimer.Enabled Then _autoScrollTimer.Start()
            Else
                _autoScrollTimer.Stop()
            End If
            Invalidate()
        End If
    End Sub
    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        _mouseDownSelecting = False
        _scrollThumbDragging = False
        _autoScrollTimer.Stop()
    End Sub
    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        If Not 启用多行 Then Return
        Dim delta As Integer = -Math.Sign(e.Delta) * 3
        Dim maxOffset As Integer = Math.Max(0, _visualLines.Count - VisibleLineCount())
        _scrollLineOffset = Math.Max(0, Math.Min(maxOffset, _scrollLineOffset + delta))
        Invalidate()
    End Sub
    Private Function HitTest(x As Integer, y As Integer) As Point
        Dim bi As Integer = 边框宽度
        Dim textLeft As Integer = Math.Max(Padding.Left, bi)
        Dim vi As Integer
        If 启用多行 Then
            Dim textTop As Integer = Math.Max(Padding.Top, bi)
            vi = (y - textTop) \ 行高 + _scrollLineOffset
        Else
            vi = 0
        End If
        vi = Math.Max(0, Math.Min(_visualLines.Count - 1, vi))
        Dim vl = _visualLines(vi)
        Dim fullLineStr As String = _lines(vl.LogicalLine)
        Dim vlText As String = fullLineStr.Substring(vl.StartCol, vl.Length)
        Dim wrapActive As Boolean = IsWordWrapActive()
        Dim alignOff As Integer = If(wrapActive, 0, GetAlignOffsetX(fullLineStr, TextAreaWidth()))
        Dim scrollX As Integer = If(wrapActive, 0, _scrollXOffset)
        Dim colInVl As Integer = FindColFromX(vlText, x - textLeft - alignOff + scrollX)
        Return New Point(vl.StartCol + colInVl, vl.LogicalLine)
    End Function
    Private Function FindColFromX(lineStr As String, x As Integer) As Integer
        If lineStr.Length = 0 OrElse x <= 0 Then Return 0
        Dim best As Integer = 0
        Dim bestDist As Integer = Integer.MaxValue
        For i As Integer = 0 To lineStr.Length
            Dim cx As Integer = MeasureWidth(lineStr.Substring(0, i))
            Dim dist As Integer = Math.Abs(x - cx)
            If dist < bestDist Then
                bestDist = dist
                best = i
            ElseIf dist > bestDist Then
                Exit For
            End If
        Next
        Return best
    End Function
#End Region

#Region "─── 光标移动 ───"
    Private Sub MoveCaret(deltaCol As Integer, deltaLine As Integer, extend As Boolean)
        If Not extend AndAlso _hasSelection AndAlso deltaCol <> 0 AndAlso deltaLine = 0 Then
            Dim minL, minC, maxL, maxC As Integer
            GetOrderedSelection(minL, minC, maxL, maxC)
            If deltaCol < 0 Then
                _caretLine = minL : _caretCol = minC
            Else
                _caretLine = maxL : _caretCol = maxC
            End If
            ClearSelection()
            EnsureCaretVisible()
            Invalidate()
            Return
        End If
        If Not extend Then
            _selAnchorLine = _caretLine
            _selAnchorCol = _caretCol
        End If
        If deltaCol <> 0 Then
            _caretCol += deltaCol
            If _caretCol < 0 Then
                If _caretLine > 0 Then
                    _caretLine -= 1
                    _caretCol = _lines(_caretLine).Length
                Else
                    _caretCol = 0
                End If
            ElseIf _caretCol > _lines(_caretLine).Length Then
                If _caretLine < _lines.Count - 1 Then
                    _caretLine += 1
                    _caretCol = 0
                Else
                    _caretCol = _lines(_caretLine).Length
                End If
            End If
        End If
        If deltaLine <> 0 Then
            If IsWordWrapActive() Then
                Dim curVi As Integer = GetVisualLineIndex(_caretLine, _caretCol)
                Dim curVl = _visualLines(curVi)
                Dim currentX As Integer = MeasureWidth(_lines(_caretLine).Substring(curVl.StartCol, _caretCol - curVl.StartCol))
                Dim newVi As Integer = Math.Max(0, Math.Min(_visualLines.Count - 1, curVi + deltaLine))
                Dim newVl = _visualLines(newVi)
                _caretLine = newVl.LogicalLine
                Dim vlText As String = _lines(newVl.LogicalLine).Substring(newVl.StartCol, newVl.Length)
                _caretCol = newVl.StartCol + FindColFromX(vlText, currentX)
            Else
                Dim currentX As Integer = MeasureWidth(_lines(_caretLine).Substring(0, _caretCol))
                _caretLine = Math.Max(0, Math.Min(_lines.Count - 1, _caretLine + deltaLine))
                _caretCol = FindColFromX(_lines(_caretLine), currentX)
            End If
        End If
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        Invalidate()
    End Sub
    Private Sub MoveCaretHome(extend As Boolean, ctrl As Boolean)
        If Not extend Then
            _selAnchorLine = _caretLine
            _selAnchorCol = _caretCol
        End If
        If ctrl Then _caretLine = 0
        _caretCol = 0
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        Invalidate()
    End Sub
    Private Sub MoveCaretEnd(extend As Boolean, ctrl As Boolean)
        If Not extend Then
            _selAnchorLine = _caretLine
            _selAnchorCol = _caretCol
        End If
        If ctrl Then _caretLine = _lines.Count - 1
        _caretCol = _lines(_caretLine).Length
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        Invalidate()
    End Sub
    Private Sub MoveCaretWordLeft(extend As Boolean)
        If Not extend Then
            _selAnchorLine = _caretLine
            _selAnchorCol = _caretCol
        End If
        If _caretCol = 0 Then
            If _caretLine > 0 Then
                _caretLine -= 1
                _caretCol = _lines(_caretLine).Length
            End If
        Else
            Dim line As String = _lines(_caretLine)
            Dim c As Integer = _caretCol - 1
            While c > 0 AndAlso Char.IsWhiteSpace(line(c - 1))
                c -= 1
            End While
            While c > 0 AndAlso Not Char.IsWhiteSpace(line(c - 1))
                c -= 1
            End While
            _caretCol = c
        End If
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        Invalidate()
    End Sub
    Private Sub MoveCaretWordRight(extend As Boolean)
        If Not extend Then
            _selAnchorLine = _caretLine
            _selAnchorCol = _caretCol
        End If
        Dim line As String = _lines(_caretLine)
        If _caretCol >= line.Length Then
            If _caretLine < _lines.Count - 1 Then
                _caretLine += 1
                _caretCol = 0
            End If
        Else
            Dim c As Integer = _caretCol
            While c < line.Length AndAlso Not Char.IsWhiteSpace(line(c))
                c += 1
            End While
            While c < line.Length AndAlso Char.IsWhiteSpace(line(c))
                c += 1
            End While
            _caretCol = c
        End If
        UpdateSelectionFromAnchor(extend)
        EnsureCaretVisible()
        Invalidate()
    End Sub
    Private Sub UpdateSelectionFromAnchor(extend As Boolean)
        If extend Then
            _hasSelection = (_caretLine <> _selAnchorLine OrElse _caretCol <> _selAnchorCol)
        Else
            ClearSelection()
        End If
    End Sub
    Private Sub EnsureCaretVisible()
        ' 垂直方向
        If 启用多行 Then
            Dim vi As Integer = GetVisualLineIndex(_caretLine, _caretCol)
            Dim vis As Integer = VisibleLineCount()
            If vi < _scrollLineOffset Then
                _scrollLineOffset = vi
            ElseIf vi >= _scrollLineOffset + vis Then
                _scrollLineOffset = vi - vis + 1
            End If
            _scrollLineOffset = Math.Max(0, _scrollLineOffset)
        End If
        UpdateScrollBar()
        ' 水平方向
        If Not IsWordWrapActive() Then
            Dim areaW As Integer = TextAreaWidth()
            If areaW > 0 Then
                Dim caretX As Integer = MeasureWidth(_lines(_caretLine).Substring(0, _caretCol))
                If Not 启用多行 AndAlso 文本对齐 <> TextAlignMode.Left Then
                    Dim lineW As Integer = MeasureWidth(_lines(_caretLine))
                    If lineW < areaW Then
                        _scrollXOffset = 0
                        Return
                    End If
                End If

                Dim margin As Integer = 光标线宽 + 2
                If caretX - _scrollXOffset < 0 Then
                    _scrollXOffset = Math.Max(0, caretX - margin)
                ElseIf caretX - _scrollXOffset >= areaW - margin Then
                    _scrollXOffset = caretX - areaW + margin
                End If
            End If
        Else
            _scrollXOffset = 0
        End If
    End Sub
#End Region

#Region "─── 文本编辑核心 ───"
    Private Sub InsertTextCore(text As String)
        DeleteSelection()
        Dim normalized As String = text.Replace(vbCr, "")
        If Not normalized.Contains(vbLf) Then
            Dim line As String = _lines(_caretLine)
            _lines(_caretLine) = String.Concat(line.AsSpan(0, _caretCol), normalized, line.AsSpan(_caretCol))
            _caretCol += normalized.Length
        Else
            Dim parts() As String = normalized.Split(vbLf)
            Dim tail As String = _lines(_caretLine).Substring(_caretCol)
            _lines(_caretLine) = String.Concat(_lines(_caretLine).AsSpan(0, _caretCol), parts(0))
            For i As Integer = 1 To parts.Length - 1
                _caretLine += 1
                _lines.Insert(_caretLine, If(i = parts.Length - 1, parts(i) & tail, parts(i)))
            Next
            _caretCol = parts(parts.Length - 1).Length
        End If
        NotifyTextChanged()
    End Sub
    Private Sub InsertNewLine()
        Dim line As String = _lines(_caretLine)
        _lines(_caretLine) = line.Substring(0, _caretCol)
        _caretLine += 1
        _lines.Insert(_caretLine, line.Substring(_caretCol))
        _caretCol = 0
        NotifyTextChanged()
    End Sub
    Private Sub HandleBackspace()
        If _hasSelection Then
            PushUndo()
            DeleteSelection()
        ElseIf _caretCol > 0 Then
            PushUndo()
            Dim line As String = _lines(_caretLine)
            _lines(_caretLine) = String.Concat(line.AsSpan(0, _caretCol - 1), line.AsSpan(_caretCol))
            _caretCol -= 1
        ElseIf _caretLine > 0 Then
            PushUndo()
            Dim prev As String = _lines(_caretLine - 1)
            _caretCol = prev.Length
            _lines(_caretLine - 1) = prev & _lines(_caretLine)
            _lines.RemoveAt(_caretLine)
            _caretLine -= 1
        End If
        NotifyTextChanged()
    End Sub
    Private Sub HandleDelete()
        If _hasSelection Then
            PushUndo()
            DeleteSelection()
        ElseIf _caretCol < _lines(_caretLine).Length Then
            PushUndo()
            Dim line As String = _lines(_caretLine)
            _lines(_caretLine) = String.Concat(line.AsSpan(0, _caretCol), line.AsSpan(_caretCol + 1))
        ElseIf _caretLine < _lines.Count - 1 Then
            PushUndo()
            _lines(_caretLine) = _lines(_caretLine) & _lines(_caretLine + 1)
            _lines.RemoveAt(_caretLine + 1)
        End If
        NotifyTextChanged()
    End Sub
    Private Sub DeleteSelection()
        If Not _hasSelection Then Return
        Dim minL, minC, maxL, maxC As Integer
        GetOrderedSelection(minL, minC, maxL, maxC)

        If minL = maxL Then
            _lines(minL) = String.Concat(_lines(minL).AsSpan(0, minC), _lines(minL).AsSpan(maxC))
        Else
            _lines(minL) = String.Concat(_lines(minL).AsSpan(0, minC), _lines(maxL).AsSpan(maxC))
            For i As Integer = maxL To minL + 1 Step -1
                _lines.RemoveAt(i)
            Next
        End If
        _caretLine = minL
        _caretCol = minC
        ClearSelection()
    End Sub
#End Region

#Region "─── 选区 ───"
    Private Sub SelectAll()
        _selAnchorLine = 0
        _selAnchorCol = 0
        _caretLine = _lines.Count - 1
        _caretCol = _lines(_caretLine).Length
        _hasSelection = True
        Invalidate()
    End Sub
    Private Sub ClearSelection()
        _hasSelection = False
        _selAnchorLine = _caretLine
        _selAnchorCol = _caretCol
    End Sub
    Private Sub GetOrderedSelection(ByRef minL As Integer, ByRef minC As Integer,
                                     ByRef maxL As Integer, ByRef maxC As Integer)
        Dim anchorBefore As Boolean =
            _selAnchorLine < _caretLine OrElse
            (_selAnchorLine = _caretLine AndAlso _selAnchorCol <= _caretCol)

        If anchorBefore Then
            minL = _selAnchorLine : minC = _selAnchorCol
            maxL = _caretLine : maxC = _caretCol
        Else
            minL = _caretLine : minC = _caretCol
            maxL = _selAnchorLine : maxC = _selAnchorCol
        End If
    End Sub
    Private Function GetSelectedText() As String
        If Not _hasSelection Then Return ""
        Dim minL, minC, maxL, maxC As Integer
        GetOrderedSelection(minL, minC, maxL, maxC)
        If minL = maxL Then Return _lines(minL).Substring(minC, maxC - minC)
        Dim sb As New StringBuilder()
        sb.AppendLine(_lines(minL).Substring(minC))
        For i As Integer = minL + 1 To maxL - 1
            sb.AppendLine(_lines(i))
        Next
        sb.Append(_lines(maxL).AsSpan(0, maxC))
        Return sb.ToString()
    End Function
#End Region

#Region "─── 剪贴板 ───"
    Private Sub CopySelection()
        If _hasSelection Then
            Try
                Clipboard.SetText(GetSelectedText())
            Catch
            End Try
        End If
    End Sub
    Private Sub CutSelection()
        If _hasSelection AndAlso Not 启用只读模式 Then
            PushUndo()
            CopySelection()
            DeleteSelection()
            NotifyTextChanged()
        End If
    End Sub
    Private Sub PasteText()
        If 启用只读模式 Then Return
        Try
            If Clipboard.ContainsText() Then
                PushUndo()
                InsertTextCore(Clipboard.GetText())
            End If
        Catch
        End Try
    End Sub
#End Region

#Region "─── 撤回 ───"
    Private Sub PushUndo()
        _undoStack.Add(New TextSnapshot(_lines, _caretLine, _caretCol))
        If _undoStack.Count > MAX_UNDO Then
            _undoStack.RemoveAt(0)
        End If
    End Sub
    Private Sub Undo()
        If _undoStack.Count = 0 Then Return
        Dim snap As TextSnapshot = _undoStack(_undoStack.Count - 1)
        _undoStack.RemoveAt(_undoStack.Count - 1)
        _lines = New List(Of String)(snap.Lines)
        _caretLine = Math.Min(snap.CaretLine, _lines.Count - 1)
        _caretCol = Math.Min(snap.CaretCol, _lines(_caretLine).Length)
        ClearSelection()
        NotifyTextChanged()
    End Sub
#End Region

#Region "─── 滚动条 ───"
    Private Sub UpdateScrollBar()
        If Not 启用多行 OrElse Not IsHandleCreated Then
            _scrollBarVisible = False
            Return
        End If
        _scrollBarVisible = _visualLines.Count > VisibleLineCount()
        Invalidate()
    End Sub
#End Region

#Region "─── 输入法 IME ───"
    Private Sub UpdateImeWindow()
        If Not IsHandleCreated Then Return
        Dim hIMC As IntPtr = ImmGetContext(Handle)
        If hIMC = IntPtr.Zero Then Return
        Try
            Dim lineStr As String = _lines(_caretLine)
            Dim vi As Integer = GetVisualLineIndex(_caretLine, _caretCol)
            Dim vl = _visualLines(vi)
            Dim wrapActive As Boolean = IsWordWrapActive()
            Dim bi As Integer = 边框宽度
            Dim imeLeft As Integer = Math.Max(Padding.Left, bi)
            Dim imeTop As Integer = Math.Max(Padding.Top, bi)
            Dim alignOff As Integer = If(wrapActive, 0, GetAlignOffsetX(lineStr, TextAreaWidth()))
            Dim scrollX As Integer = If(wrapActive, 0, _scrollXOffset)
            Dim cx As Integer = imeLeft + alignOff + MeasureWidth(lineStr.Substring(vl.StartCol, _caretCol - vl.StartCol)) - scrollX
            Dim cy As Integer
            If 启用多行 Then
                cy = imeTop + (vi - _scrollLineOffset) * 行高 + 行高
            Else
                Dim textHeight As Integer = ClientRectangle.Height - imeTop - Math.Max(Padding.Bottom, bi)
                cy = imeTop + (textHeight - 行高) \ 2 + 行高
            End If
            Dim cf As New COMPOSITIONFORM With {
                .dwStyle = CFS_POINT,
                .ptCurrentPos = New Point(cx, cy)
            }
            ImmSetCompositionWindow(hIMC, cf)
        Finally
            ImmReleaseContext(Handle, hIMC)
        End Try
    End Sub
#End Region

#Region "─── 辅助 ───"
    Private Function MeasureWidth(text As String) As Integer
        If String.IsNullOrEmpty(text) Then Return 0
        Return TextRenderer.MeasureText(text, Font, New Size(32767, 行高),
            TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine).Width
    End Function
    Private Function VisibleLineCount() As Integer
        Dim bi As Integer = 边框宽度
        Dim h As Integer = ClientRectangle.Height - Math.Max(Padding.Top, bi) - Math.Max(Padding.Bottom, bi)
        Return Math.Max(1, h \ 行高)
    End Function
    Private Function TextAreaWidth() As Integer
        Dim scrollW As Integer = If(_scrollBarVisible, 滚动条宽度 + 6, 0)
        Dim bi As Integer = 边框宽度
        Return ClientRectangle.Width - Math.Max(Padding.Left, bi) - Math.Max(Padding.Right, bi) - scrollW
    End Function
    Private Function GetAlignOffsetX(lineStr As String, areaWidth As Integer) As Integer
        If 启用多行 OrElse 文本对齐 = TextAlignMode.Left Then Return 0
        Dim textW As Integer = MeasureWidth(lineStr)
        If textW >= areaWidth Then Return 0
        Select Case 文本对齐
            Case TextAlignMode.Center
                Return (areaWidth - textW) \ 2
            Case TextAlignMode.Right
                Return areaWidth - textW
            Case Else
                Return 0
        End Select
    End Function
    Private Sub SetLinesFromString(s As String)
        Dim normalized As String = If(s, "").Replace(vbCr, "")
        _lines = New List(Of String)(normalized.Split(vbLf))
        If _lines.Count = 0 Then _lines.Add("")
    End Sub
    Private Sub NotifyTextChanged()
        RebuildVisualLines()
        Dim oldVisible As Boolean = _scrollBarVisible
        UpdateScrollBar()
        If _scrollBarVisible <> oldVisible Then
            RebuildVisualLines()
            UpdateScrollBar()
        End If
        EnsureCaretVisible()
        Invalidate()
        RaiseEvent TextChanged(Me, EventArgs.Empty)
    End Sub
    Private Sub ResetCaretBlink()
        _caretVisible = True
        _caretBlinkTimer.Stop()
        _caretBlinkTimer.Start()
        Invalidate()
    End Sub
    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            Invalidate()
        End If
    End Sub
    Private Function IsWordWrapActive() As Boolean
        Return 启用多行 AndAlso _wordWrap
    End Function
    Private Sub RebuildVisualLines()
        _visualLines.Clear()
        Dim areaW As Integer = If(IsHandleCreated, TextAreaWidth(), 0)
        For li As Integer = 0 To _lines.Count - 1
            Dim line As String = _lines(li)
            If Not IsWordWrapActive() OrElse areaW <= 0 OrElse line.Length = 0 Then
                _visualLines.Add(New VisualLineInfo(li, 0, line.Length))
            Else
                Dim startCol As Integer = 0
                While startCol < line.Length
                    Dim fitLen As Integer = FindFitLength(line, startCol, areaW)
                    _visualLines.Add(New VisualLineInfo(li, startCol, fitLen))
                    startCol += fitLen
                End While
            End If
        Next
        If _visualLines.Count = 0 Then
            _visualLines.Add(New VisualLineInfo(0, 0, 0))
        End If
    End Sub
    Private Function FindFitLength(line As String, startCol As Integer, maxWidth As Integer) As Integer
        Dim remaining As Integer = line.Length - startCol
        If remaining <= 0 Then Return 0
        If MeasureWidth(line.Substring(startCol)) <= maxWidth Then Return remaining
        Dim lo As Integer = 1
        Dim hi As Integer = remaining
        Dim best As Integer = 1
        While lo <= hi
            Dim mid As Integer = (lo + hi) \ 2
            If MeasureWidth(line.Substring(startCol, mid)) <= maxWidth Then
                best = mid
                lo = mid + 1
            Else
                hi = mid - 1
            End If
        End While
        Return Math.Max(1, best)
    End Function
    Private Function GetVisualLineIndex(logicalLine As Integer, col As Integer) As Integer
        For i As Integer = _visualLines.Count - 1 To 0 Step -1
            Dim vl = _visualLines(i)
            If vl.LogicalLine = logicalLine AndAlso col >= vl.StartCol Then
                Return i
            End If
        Next
        Return 0
    End Function
    Private Sub AutoScrollTick(sender As Object, e As EventArgs)
        If Not _mouseDownSelecting Then
            _autoScrollTimer.Stop()
            Return
        End If
        Dim bi As Integer = 边框宽度
        Dim textTop As Integer = Math.Max(Padding.Top, bi)
        Dim textBottom As Integer = ClientRectangle.Height - Math.Max(Padding.Bottom, bi)
        Dim scrollDelta As Integer
        If _lastMousePos.Y < textTop Then
            scrollDelta = -1
        ElseIf _lastMousePos.Y > textBottom Then
            scrollDelta = 1
        Else
            _autoScrollTimer.Stop()
            Return
        End If
        Dim maxOffset As Integer = Math.Max(0, _visualLines.Count - VisibleLineCount())
        _scrollLineOffset = Math.Max(0, Math.Min(maxOffset, _scrollLineOffset + scrollDelta))
        Dim pos As Point = HitTest(_lastMousePos.X, _lastMousePos.Y)
        _caretLine = pos.Y
        _caretCol = pos.X
        _hasSelection = (_caretLine <> _selAnchorLine OrElse _caretCol <> _selAnchorCol)
        Invalidate()
    End Sub
#End Region

#Region "─── 事件 ───"
    Protected Overrides Sub OnGotFocus(e As EventArgs)
        MyBase.OnGotFocus(e)
        If IsHandleCreated Then
            ImmAssociateContextEx(Handle, IntPtr.Zero, IACE_DEFAULT)
            UpdateImeWindow()
        End If
        _caretVisible = True
        _caretBlinkTimer.Start()
        Invalidate()
    End Sub
    Protected Overrides Sub OnLostFocus(e As EventArgs)
        MyBase.OnLostFocus(e)
        _caretBlinkTimer.Stop()
        _caretVisible = False
        Invalidate()
    End Sub
    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        RebuildVisualLines()
        EnsureCaretVisible()
        Invalidate()
    End Sub
    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        RebuildVisualLines()
        Invalidate()
    End Sub
    Protected Overrides Sub OnBackColorChanged(e As EventArgs)
        MyBase.OnBackColorChanged(e)
        Invalidate()
    End Sub
    Protected Overrides Sub OnForeColorChanged(e As EventArgs)
        MyBase.OnForeColorChanged(e)
        Invalidate()
    End Sub
#End Region

#Region "禁用属性"
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property AutoScroll As Boolean
        Get
            Return Nothing
        End Get
        Set(value As Boolean)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property AutoScrollMargin As Size
        Get
            Return Nothing
        End Get
        Set(value As Size)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property AutoScrollMinSize As Size
        Get
            Return Nothing
        End Get
        Set(value As Size)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property AutoSize As Boolean
        Get
            Return Nothing
        End Get
        Set(value As Boolean)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property AutoSizeMode As AutoSizeMode
        Get
            Return Nothing
        End Get
        Set(value As AutoSizeMode)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property BorderStyle As BorderStyle
        Get
            Return Nothing
        End Get
        Set(value As BorderStyle)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property BackgroundImage As Image
        Get
            Return Nothing
        End Get
        Set(value As Image)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property BackgroundImageLayout As ImageLayout
        Get
            Return Nothing
        End Get
        Set(value As ImageLayout)
        End Set
    End Property
#End Region
End Class