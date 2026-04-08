Imports System.ComponentModel
Imports System.Drawing.Drawing2D
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Text.RegularExpressions

<DefaultEvent("TextChanged")>
Public Class ModernTextBox
    Public Shadows Event TextChanged As EventHandler
    Public Event LinkClicked As EventHandler(Of LinkClickedEventArgs)

#Region "内部数据结构"
    Private Structure TextRun
        Public StartCol As Integer
        Public Length As Integer
        Public ForeColor As Color
        Public RunFont As Font
        Public Sub New(startCol As Integer, length As Integer,
                       Optional foreColor As Color = Nothing,
                       Optional runFont As Font = Nothing)
            Me.StartCol = startCol
            Me.Length = length
            Me.ForeColor = foreColor
            Me.RunFont = runFont
        End Sub
    End Structure
    Private Structure TextSnapshot
        Public Lines As String()
        Public LineRuns As List(Of List(Of TextRun))
        Public CaretLine As Integer
        Public CaretCol As Integer
        Public Sub New(lines As IEnumerable(Of String), lineRuns As List(Of List(Of TextRun)), caretLine As Integer, caretCol As Integer)
            Me.Lines = lines.ToArray()
            Me.LineRuns = lineRuns
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
    Private Structure LinkRange
        Public StartCol As Integer
        Public Length As Integer
        Public Url As String
        Public Sub New(startCol As Integer, length As Integer, url As String)
            Me.StartCol = startCol
            Me.Length = length
            Me.Url = url
        End Sub
    End Structure
    Public Structure SyntaxToken
        Public StartCol As Integer
        Public Length As Integer
        Public ForeColor As Color
        Public Sub New(startCol As Integer, length As Integer, foreColor As Color)
            Me.StartCol = startCol
            Me.Length = length
            Me.ForeColor = foreColor
        End Sub
    End Structure
    Public Structure SyntaxHighlightResult
        Public Tokens As List(Of SyntaxToken)
        Public EndState As Integer
        Public Sub New(tokens As List(Of SyntaxToken), endState As Integer)
            Me.Tokens = tokens
            Me.EndState = endState
        End Sub
    End Structure
    Public Interface ISyntaxHighlighter
        Function HighlightLine(lineIndex As Integer, lineText As String, previousLineState As Integer) As SyntaxHighlightResult
    End Interface
#End Region

#Region "字段"
    Private _lines As New List(Of String) From {String.Empty}
    Private _lineRuns As New List(Of List(Of TextRun)) From {Nothing}
    Private _caretLine As Integer = 0
    Private _caretCol As Integer = 0
    Private _selAnchorLine As Integer = 0
    Private _selAnchorCol As Integer = 0
    Private _hasSelection As Boolean = False
    Private _maxUndo As Integer = 10
    Private _undoStack As New List(Of TextSnapshot)
    Private _caretVisible As Boolean = True
    Private _caretBlinkTimer As New Timer() With {.Interval = 530}
    Private _scrollLineOffset As Integer = 0
    Private _scrollXOffset As Integer = 0
    Private _scrollBarVisible As Boolean = False
    Private _scrollBar As New ScrollBarRenderer()
    Private _mouseDownSelecting As Boolean = False
    Private _imeComposing As Boolean = False
    Private _visualLines As New List(Of VisualLineInfo)
    Private _autoScrollTimer As New Timer() With {.Interval = 50}
    Private _lastMousePos As Point = Point.Empty
    Private _ssaaBitmap As Bitmap = Nothing
    Private _ssaaBitmapW As Integer = 0
    Private _ssaaBitmapH As Integer = 0
    Private _preserveScrollPosition As Boolean = False
    Private _lineLinks As New List(Of List(Of LinkRange)) From {Nothing}
    Private _underlineFontCache As Font = Nothing
    Private _underlineFontBase As Font = Nothing
    Private _mouseDownLinkText As String = Nothing
    Private _syntaxHighlighter As ISyntaxHighlighter = Nothing
    Private _lineStates As New List(Of Integer) From {0}
    Private _enableSyntaxHighlight As Boolean = False
    Private _showLineNumbers As Boolean = False
    Private _lineNumForeColor As Color = Color.FromArgb(140, 140, 140)
    Private _lineNumBackColor As Color = Color.FromArgb(30, 30, 30)
    Private _lineNumFont As Font = Nothing
    Private _lineNumPadLeft As Integer = 10
    Private _lineNumPadRight As Integer = 10
    Private _lineNumAlign As TextAlignMode = TextAlignMode.Right
    Private Shared ReadOnly LinkRegex As New Regex("(https?://|ftp://|www\.)\S+", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Const TF As TextFormatFlags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.VerticalCenter
    Private _cachedDpiScale As Single = 1.0F
    Private _cachedBorderInset As Integer = 1
    Private _scaledLineHeight As Integer = 25
    Private _scaledCaretWidth As Integer = 2
    Private _scaledLineNumPadL As Integer = 6
    Private _scaledLineNumPadR As Integer = 8
#End Region

#Region "属性"
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
            Dim savedScrollLine As Integer = _scrollLineOffset
            Dim savedScrollX As Integer = _scrollXOffset
            SetLinesFromString(normalized)
            _caretLine = 0
            _caretCol = 0
            If Not _preserveScrollPosition Then
                _scrollXOffset = 0
                _scrollLineOffset = 0
            End If
            ClearSelection()
            NotifyTextChanged()
            If _preserveScrollPosition Then
                Dim maxOffset As Integer = Math.Max(0, _visualLines.Count - VisibleLineCount())
                _scrollLineOffset = Math.Min(savedScrollLine, maxOffset)
                _scrollXOffset = savedScrollX
                UpdateScrollBar()
                Invalidate()
            End If
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
            UpdateDpiCache()
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
            UpdateDpiCache()
            Invalidate()
        End Set
    End Property

    Private 光标颜色 As Color = Color.Gainsboro
    <Category("LakeUI"), Description("光标颜色"), DefaultValue(GetType(Color), "Gainsboro"), Browsable(True)>
    Public Property CaretColor As Color
        Get
            Return 光标颜色
        End Get
        Set(value As Color)
            SetValue(光标颜色, value)
        End Set
    End Property

    Private 选区背景色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("选区背景色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
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
            If 边框宽度 <> value Then
                边框宽度 = value
                UpdateDpiCache()
                Invalidate()
            End If
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

    Private 启用只读模式 As Boolean
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

    Private 水印颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("水印颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
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
    <Category("LakeUI"), Description("滚动条滑块颜色"), DefaultValue(GetType(Color), "140, 140, 140"), Browsable(True)>
    Public Property ScrollBarColor As Color
        Get
            Return 滚动条颜色
        End Get
        Set(value As Color)
            SetValue(滚动条颜色, value)
        End Set
    End Property

    Private 滚动条悬停颜色 As Color = SystemColors.ScrollBar
    <Category("LakeUI"), Description("滚动条滑块悬停/拖拽颜色"), DefaultValue(GetType(Color), "ScrollBar"), Browsable(True)>
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

    Private 启用链接识别 As Boolean = False
    <Category("LakeUI"), Description("是否启用超链接自动识别"), DefaultValue(GetType(Boolean), "False"), Browsable(True)>
    Public Property LinkDetection As Boolean
        Get
            Return 启用链接识别
        End Get
        Set(value As Boolean)
            If 启用链接识别 <> value Then
                启用链接识别 = value
                RebuildAllLinks()
                Invalidate()
            End If
        End Set
    End Property

    Private 链接颜色 As Color = Color.FromArgb(86, 156, 214)
    <Category("LakeUI"), Description("链接渲染颜色"), DefaultValue(GetType(Color), "86, 156, 214"), Browsable(True)>
    Public Property LinkColor As Color
        Get
            Return 链接颜色
        End Get
        Set(value As Color)
            SetValue(链接颜色, value)
        End Set
    End Property

    Private 链接下划线 As Boolean = True
    <Category("LakeUI"), Description("是否为链接渲染下划线"), DefaultValue(GetType(Boolean), "True"), Browsable(True)>
    Public Property LinkUnderline As Boolean
        Get
            Return 链接下划线
        End Get
        Set(value As Boolean)
            SetValue(链接下划线, value)
        End Set
    End Property

    <Category("LakeUI"), Description("语法高亮器，设置后自动对文本进行语法着色"), DefaultValue(GetType(ISyntaxHighlighter), Nothing), Browsable(False)>
    Public Property SyntaxHighlighter As ISyntaxHighlighter
        Get
            Return _syntaxHighlighter
        End Get
        Set(value As ISyntaxHighlighter)
            _syntaxHighlighter = value
            If _enableSyntaxHighlight Then
                ApplySyntaxHighlighting()
            End If
            Invalidate()
        End Set
    End Property

    <Category("LakeUI"), Description("是否启用语法高亮"), DefaultValue(GetType(Boolean), "False"), Browsable(True)>
    Public Property EnableSyntaxHighlight As Boolean
        Get
            Return _enableSyntaxHighlight
        End Get
        Set(value As Boolean)
            If _enableSyntaxHighlight <> value Then
                _enableSyntaxHighlight = value
                If value Then
                    ApplySyntaxHighlighting()
                Else
                    ClearAllFormats()
                End If
                Invalidate()
            End If
        End Set
    End Property

    <Category("LakeUI"), Description("是否显示行号（仅多行模式）"), DefaultValue(GetType(Boolean), "False"), Browsable(True)>
    Public Property ShowLineNumbers As Boolean
        Get
            Return _showLineNumbers
        End Get
        Set(value As Boolean)
            If _showLineNumbers <> value Then
                _showLineNumbers = value
                RebuildVisualLines()
                UpdateScrollBar()
                Invalidate()
            End If
        End Set
    End Property

    <Category("LakeUI"), Description("行号文本颜色"), DefaultValue(GetType(Color), "140, 140, 140"), Browsable(True)>
    Public Property LineNumberForeColor As Color
        Get
            Return _lineNumForeColor
        End Get
        Set(value As Color)
            SetValue(_lineNumForeColor, value)
        End Set
    End Property

    <Category("LakeUI"), Description("行号区域背景颜色"), DefaultValue(GetType(Color), "30, 30, 30"), Browsable(True)>
    Public Property LineNumberBackColor As Color
        Get
            Return _lineNumBackColor
        End Get
        Set(value As Color)
            SetValue(_lineNumBackColor, value)
        End Set
    End Property

    <Category("LakeUI"), Description("行号字体，为 Nothing 时使用控件 Font"), Browsable(True),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
    Public Property LineNumberFont As Font
        Get
            Return _lineNumFont
        End Get
        Set(value As Font)
            _lineNumFont = value
            If _showLineNumbers Then
                RebuildVisualLines()
                UpdateScrollBar()
                Invalidate()
            End If
        End Set
    End Property
    Private Function ShouldSerializeLineNumberFont() As Boolean
        Return _lineNumFont IsNot Nothing
    End Function
    Public Sub ResetLineNumberFont()
        LineNumberFont = Nothing
    End Sub

    <Category("LakeUI"), Description("行号区域左侧内距"), DefaultValue(GetType(Integer), "10"), Browsable(True)>
    Public Property LineNumberPaddingLeft As Integer
        Get
            Return _lineNumPadLeft
        End Get
        Set(value As Integer)
            _lineNumPadLeft = Math.Max(0, value)
            UpdateDpiCache()
            If _showLineNumbers Then
                RebuildVisualLines()
                UpdateScrollBar()
                Invalidate()
            End If
        End Set
    End Property

    <Category("LakeUI"), Description("行号区域右侧内距"), DefaultValue(GetType(Integer), "10"), Browsable(True)>
    Public Property LineNumberPaddingRight As Integer
        Get
            Return _lineNumPadRight
        End Get
        Set(value As Integer)
            _lineNumPadRight = Math.Max(0, value)
            UpdateDpiCache()
            If _showLineNumbers Then
                RebuildVisualLines()
                UpdateScrollBar()
                Invalidate()
            End If
        End Set
    End Property

    <Category("LakeUI"), Description("行号对齐方式"), DefaultValue(TextAlignMode.Right), Browsable(True)>
    Public Property LineNumberAlign As TextAlignMode
        Get
            Return _lineNumAlign
        End Get
        Set(value As TextAlignMode)
            SetValue(_lineNumAlign, value)
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

    <Category("LakeUI"), Description("重设 Text 时保留滚动条位置，适用于日志输出等场景"), DefaultValue(GetType(Boolean), "False"), Browsable(True)>
    Public Property PreserveScrollPosition As Boolean
        Get
            Return _preserveScrollPosition
        End Get
        Set(value As Boolean)
            _preserveScrollPosition = value
        End Set
    End Property

    <Category("LakeUI"), Description("最大撤回次数，设为0则关闭撤回功能以节约性能"), DefaultValue(GetType(Integer), "10"), Browsable(True)>
    Public Property MaxUndoCount As Integer
        Get
            Return _maxUndo
        End Get
        Set(value As Integer)
            _maxUndo = Math.Max(0, value)
            If _maxUndo = 0 Then
                _undoStack.Clear()
            ElseIf _undoStack.Count > _maxUndo Then
                _undoStack.RemoveRange(0, _undoStack.Count - _maxUndo)
            End If
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

    Private _maxLength As Integer = 0
    <Category("LakeUI"), Description("最大允许字符数，0 = 无限制"), DefaultValue(0), Browsable(True)>
    Public Property MaxLength As Integer
        Get
            Return _maxLength
        End Get
        Set(value As Integer)
            _maxLength = Math.Max(0, value)
        End Set
    End Property

    Private _passwordChar As Char = ChrW(0)
    <Category("LakeUI"), Description("密码掩码字符，为空则不启用（仅单行模式）"), DefaultValue(GetType(Char), ""), Browsable(True)>
    Public Property PasswordChar As Char
        Get
            Return _passwordChar
        End Get
        Set(value As Char)
            _passwordChar = value
            Invalidate()
        End Set
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SelectionStart As Integer
        Get
            Return GetAbsoluteOffset(_caretLine, _caretCol)
        End Get
        Set(value As Integer)
            Dim pos = GetLineColFromAbsolute(value)
            _caretLine = pos.Y
            _caretCol = pos.X
            ClearSelection()
            EnsureCaretVisible()
            Invalidate()
        End Set
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SelectionLength As Integer
        Get
            If Not _hasSelection Then Return 0
            Dim s = GetAbsoluteOffset(_selAnchorLine, _selAnchorCol)
            Dim e2 = GetAbsoluteOffset(_caretLine, _caretCol)
            Return Math.Abs(e2 - s)
        End Get
        Set(value As Integer)
            _selAnchorLine = _caretLine
            _selAnchorCol = _caretCol
            Dim target = GetAbsoluteOffset(_caretLine, _caretCol) + value
            Dim pos = GetLineColFromAbsolute(target)
            _caretLine = pos.Y
            _caretCol = pos.X
            _hasSelection = value <> 0
            Invalidate()
        End Set
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SelectedText As String
        Get
            Return GetSelectedText()
        End Get
        Set(value As String)
            If _hasSelection Then
                PushUndo()
                DeleteSelection()
            End If
            If Not String.IsNullOrEmpty(value) Then
                InsertTextCore(value)
            End If
        End Set
    End Property
#End Region

#Region "公共方法"
    Public Sub Clear()
        Text = String.Empty
    End Sub
    Public Sub AppendText(text As String)
        If String.IsNullOrEmpty(text) Then Return
        PushUndo()
        _caretLine = _lines.Count - 1
        _caretCol = _lines(_caretLine).Length
        ClearSelection()
        InsertTextCore(text)
    End Sub
    ''' <summary>
    ''' 追加一行文本，可指定颜色和字体。专为日志输出等高频场景优化：
    ''' 跳过撤回记录、跳过全量视觉行重建、直接设置格式，配合 PreserveScrollPosition 使用效果最佳。
    ''' </summary>
    Public Sub AppendLine(text As String, Optional foreColor As Color = Nothing, Optional lineFont As Font = Nothing)
        Dim lineText As String = If(text, "").Replace(vbCr, "").Replace(vbLf, "")

        If _maxLength > 0 Then
            Dim currentLen As Integer = _lines.Sum(Function(l) l.Length) + _lines.Count - 1
            Dim overhead As Integer = If(_lines.Count = 1 AndAlso _lines(0).Length = 0, 0, 1)
            Dim remaining As Integer = _maxLength - currentLen - overhead
            If remaining <= 0 Then Return
            If lineText.Length > remaining Then lineText = lineText.Substring(0, remaining)
        End If

        Dim isEmpty As Boolean = (_lines.Count = 1 AndAlso _lines(0).Length = 0)
        Dim newLineIndex As Integer

        If isEmpty Then
            _lines(0) = lineText
            newLineIndex = 0
            _visualLines.Clear()
        Else
            newLineIndex = _lines.Count
            _lines.Add(lineText)
            _lineRuns.Add(Nothing)
            _lineLinks.Add(Nothing)
            _lineStates.Add(0)
        End If

        ' 直接设置格式 Run，避免二次遍历
        If foreColor <> Color.Empty OrElse lineFont IsNot Nothing Then
            If lineText.Length > 0 Then
                _lineRuns(newLineIndex) = New List(Of TextRun) From {
                    New TextRun(0, lineText.Length, foreColor, lineFont)
                }
            End If
        ElseIf _syntaxHighlighter IsNot Nothing Then
            ApplySyntaxHighlightingToLine(newLineIndex)
        End If

        ' 检测链接
        DetectLinksInLine(newLineIndex)

        ' 增量追加视觉行，不做全量 RebuildVisualLines
        Dim areaW As Integer = If(IsHandleCreated, TextAreaWidth(), 0)
        If Not IsWordWrapActive() OrElse areaW <= 0 OrElse lineText.Length = 0 Then
            _visualLines.Add(New VisualLineInfo(newLineIndex, 0, lineText.Length))
        Else
            Dim startCol As Integer = 0
            While startCol < lineText.Length
                Dim fitLen As Integer = FindFitLength(newLineIndex, startCol, areaW)
                _visualLines.Add(New VisualLineInfo(newLineIndex, startCol, fitLen))
                startCol += fitLen
            End While
        End If

        ' 滚动条可见性变化时才回退到全量重建
        Dim oldVisible As Boolean = _scrollBarVisible
        UpdateScrollBar()
        If _scrollBarVisible <> oldVisible Then
            RebuildVisualLines()
            UpdateScrollBar()
        End If

        ' 未启用保留位置时自动滚到底部
        If Not _preserveScrollPosition Then
            _scrollLineOffset = Math.Max(0, _visualLines.Count - VisibleLineCount())
        End If

        Invalidate()
        RaiseEvent TextChanged(Me, EventArgs.Empty)
    End Sub
    Public Shadows Sub [Select](start As Integer, length As Integer)
        Dim pos = GetLineColFromAbsolute(start)
        _selAnchorLine = pos.Y
        _selAnchorCol = pos.X
        Dim endPos = GetLineColFromAbsolute(start + length)
        _caretLine = endPos.Y
        _caretCol = endPos.X
        _hasSelection = length <> 0
        EnsureCaretVisible()
        Invalidate()
    End Sub
    Public Sub DeselectAll()
        ClearSelection()
        Invalidate()
    End Sub
    Public Sub ScrollToCaret()
        EnsureCaretVisible()
        Invalidate()
    End Sub
    Public Sub ScrollToBottom()
        If Not 启用多行 Then Return
        Dim maxOffset As Integer = Math.Max(0, _visualLines.Count - VisibleLineCount())
        _scrollLineOffset = maxOffset
        UpdateScrollBar()
        Invalidate()
    End Sub
    Public Sub ScrollToTop()
        _scrollLineOffset = 0
        _scrollXOffset = 0
        UpdateScrollBar()
        Invalidate()
    End Sub
    Public Sub ScrollToLine(lineIndex As Integer)
        If Not 启用多行 Then Return
        lineIndex = Math.Max(0, Math.Min(_lines.Count - 1, lineIndex))
        Dim targetVi As Integer = _visualLines.Count - 1
        For i As Integer = 0 To _visualLines.Count - 1
            If _visualLines(i).LogicalLine >= lineIndex Then
                targetVi = i
                Exit For
            End If
        Next
        Dim maxOffset As Integer = Math.Max(0, _visualLines.Count - VisibleLineCount())
        _scrollLineOffset = Math.Min(targetVi, maxOffset)
        UpdateScrollBar()
        Invalidate()
    End Sub
    Public Sub SetFormat(startPos As Integer, length As Integer,
                         Optional foreColor As Color = Nothing, Optional runFont As Font = Nothing)
        If length <= 0 Then Return
        Dim pos = GetLineColFromAbsolute(startPos)
        Dim remaining = length
        Dim line = pos.Y
        Dim col = pos.X
        While remaining > 0 AndAlso line < _lines.Count
            Dim lineLen = _lines(line).Length
            Dim colEnd = Math.Min(col + remaining, lineLen)
            Dim segLen = colEnd - col
            If segLen > 0 Then
                ApplyFormatToLine(line, col, segLen, foreColor, runFont)
            End If
            remaining -= segLen + 2
            line += 1
            col = 0
        End While
        Invalidate()
    End Sub
    Public Sub SetLineFormat(lineIndex As Integer, startCol As Integer, length As Integer,
                              Optional foreColor As Color = Nothing, Optional runFont As Font = Nothing)
        If lineIndex < 0 OrElse lineIndex >= _lines.Count Then Return
        If length <= 0 Then Return
        startCol = Math.Max(0, startCol)
        length = Math.Min(length, _lines(lineIndex).Length - startCol)
        If length <= 0 Then Return
        ApplyFormatToLine(lineIndex, startCol, length, foreColor, runFont)
        Invalidate()
    End Sub
    Public Sub ClearFormat(startPos As Integer, length As Integer)
        SetFormat(startPos, length, Color.Empty, Nothing)
    End Sub
    Public Sub ClearAllFormats()
        For i = 0 To _lineRuns.Count - 1
            _lineRuns(i) = Nothing
        Next
        Invalidate()
    End Sub
#End Region

#Region "初始化"
    Public Sub New()
        InitializeComponent()
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
        UpdateDpiCache()
        ImeHelper.AssociateDefault(Handle)
        RebuildVisualLines()
        _caretBlinkTimer.Start()
    End Sub
    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        _caretBlinkTimer.Stop()
        _autoScrollTimer.Stop()
        _ssaaBitmap?.Dispose()
        _ssaaBitmap = Nothing
        _underlineFontCache?.Dispose()
        _underlineFontCache = Nothing
        MyBase.OnHandleDestroyed(e)
    End Sub
#End Region

#Region "绘制"
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim w As Integer = ClientRectangle.Width
        Dim h As Integer = ClientRectangle.Height
        Dim hasRadius As Boolean = 边框圆角半径 > 0
        Dim boundsRect As New RectangleF(0, 0, w - 1, h - 1)
        Dim s As Single = DpiScale()
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * s / 2.0F
            boundsRect.Inflate(-half, -half)
        End If
        Dim bc As Color = If(Focused, 有焦点时边框颜色, 边框颜色)
        Dim _ssaa As Integer = If(Class1.GlobalSSAA > 1, Class1.GlobalSSAA, 超采样倍率)
        If _ssaa > 1 Then
            Dim bmpW As Integer = w * _ssaa
            Dim bmpH As Integer = h * _ssaa
            If _ssaaBitmap Is Nothing OrElse _ssaaBitmapW <> bmpW OrElse _ssaaBitmapH <> bmpH Then
                _ssaaBitmap?.Dispose()
                _ssaaBitmap = New Bitmap(bmpW, bmpH)
                _ssaaBitmapW = bmpW
                _ssaaBitmapH = bmpH
            End If
            Using g As Graphics = Graphics.FromImage(_ssaaBitmap)
                g.Clear(Color.Transparent)
                g.ScaleTransform(_ssaa, _ssaa)
                DrawBackgroundFill(g, hasRadius, boundsRect)
            End Using
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
            e.Graphics.DrawImage(_ssaaBitmap, 0, 0, w, h)
        Else
            DrawBackgroundFill(e.Graphics, hasRadius, boundsRect)
        End If
        DrawTextContent(e.Graphics, w, h)
        ' 边框绘制在行号背景之上
        If _ssaa > 1 Then
            Using g As Graphics = Graphics.FromImage(_ssaaBitmap)
                g.Clear(Color.Transparent)
                g.ScaleTransform(_ssaa, _ssaa)
                DrawBorderOnly(g, hasRadius, boundsRect, bc)
            End Using
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
            e.Graphics.DrawImage(_ssaaBitmap, 0, 0, w, h)
        Else
            DrawBorderOnly(e.Graphics, hasRadius, boundsRect, bc)
        End If
        DrawScrollBar(e.Graphics, w, h)
    End Sub

    Private Sub DrawBackgroundFill(g As Graphics, hasRadius As Boolean, boundsRect As RectangleF)
        SetHighQualityGraphics(g)
        Dim s As Single = DpiScale()
        If hasRadius Then
            Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(boundsRect, 边框圆角半径 * s)
                RectangleRenderer.绘制圆角背景(g, path, boundsRect, 背景颜色, Color.Empty, Orientation.Horizontal)
            End Using
        Else
            RectangleRenderer.绘制矩形背景(g, boundsRect, 背景颜色, Color.Empty, Orientation.Horizontal)
        End If
    End Sub

    Private Sub DrawBorderOnly(g As Graphics, hasRadius As Boolean, boundsRect As RectangleF, borderClr As Color)
        SetHighQualityGraphics(g)
        Dim s As Single = DpiScale()
        If hasRadius Then
            Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(boundsRect, 边框圆角半径 * s)
                RectangleRenderer.绘制圆角边框(g, path, borderClr, 边框宽度 * s)
            End Using
        Else
            RectangleRenderer.绘制矩形边框(g, boundsRect, borderClr, 边框宽度 * s)
        End If
    End Sub

    Private Shared Sub SetHighQualityGraphics(g As Graphics)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        g.InterpolationMode = InterpolationMode.HighQualityBicubic
    End Sub

    Private Sub DrawTextContent(g As Graphics, w As Integer, h As Integer)
        g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit
        Dim bi As Integer = ScaledBorderWidth()
        Dim textTop As Integer = Math.Max(Padding.Top, bi)
        Dim textRight As Integer = Math.Max(Padding.Right, bi)
        Dim textBottom As Integer = Math.Max(Padding.Bottom, bi)
        Dim gutterW As Integer = LineNumberGutterWidth()
        Dim gutterLeft As Integer = bi
        Dim textLeft As Integer
        If gutterW > 0 Then
            textLeft = bi + gutterW + Padding.Left
        Else
            textLeft = Math.Max(Padding.Left, bi)
        End If
        Dim scrollW As Integer = If(_scrollBarVisible, CInt(Math.Round(滚动条宽度 * DpiScale())) + ScrollBarRenderer.Margin * 2, 0)
        Dim textWidth As Integer = w - textLeft - textRight - scrollW
        Dim textHeight As Integer = h - textTop - textBottom
        Dim isSingleLine As Boolean = Not 启用多行
        Dim singleLineY As Integer = textTop + (textHeight - _scaledLineHeight) \ 2
        Dim visibleLines As Integer = VisibleLineCount()
        Dim startVi As Integer = _scrollLineOffset
        Dim endVi As Integer = Math.Min(_visualLines.Count - 1, startVi + visibleLines - 1)

        ' 绘制行号区域（紧贴上下边框内侧）
        If gutterW > 0 Then
            ' 绘制行号背景，兼容圆角边框
            If _lineNumBackColor <> Color.Empty Then
                Dim s As Single = DpiScale()
                Dim gutterRect As New Rectangle(0, 0, bi + gutterW, h)
                If 边框圆角半径 > 0 Then
                    Dim boundsRect As New RectangleF(0, 0, w - 1, h - 1)
                    If 边框宽度 > 0 Then
                        Dim half As Single = 边框宽度 * s / 2.0F
                        boundsRect.Inflate(-half, -half)
                    End If
                    Using clipPath As GraphicsPath = RectangleRenderer.创建圆角矩形路径(boundsRect, 边框圆角半径 * s)
                        Dim oldSmooth = g.SmoothingMode
                        g.SmoothingMode = SmoothingMode.AntiAlias
                        Using rgn As New Region(clipPath)
                            rgn.Intersect(gutterRect)
                            g.SetClip(rgn, Drawing2D.CombineMode.Replace)
                        End Using
                        Using br As New SolidBrush(_lineNumBackColor)
                            g.FillRectangle(br, gutterRect)
                        End Using
                        g.SmoothingMode = oldSmooth
                        g.ResetClip()
                    End Using
                Else
                    Using br As New SolidBrush(_lineNumBackColor)
                        g.FillRectangle(br, gutterRect)
                    End Using
                End If
            End If
            g.SetClip(New Rectangle(gutterLeft, textTop, gutterW, textHeight))
            Dim useNumFont As Font = If(_lineNumFont, Font)
            Dim contentW As Integer = gutterW - _scaledLineNumPadL - _scaledLineNumPadR
            Dim lastDrawnLogical As Integer = -1
            For vi As Integer = startVi To endVi
                Dim vl = _visualLines(vi)
                Dim lineY As Integer = textTop + (vi - _scrollLineOffset) * _scaledLineHeight
                If vl.LogicalLine <> lastDrawnLogical Then
                    lastDrawnLogical = vl.LogicalLine
                    Dim numStr As String = (vl.LogicalLine + 1).ToString()
                    Dim numW As Integer = TextRenderHelper.MeasureTextWidth(numStr, useNumFont, _scaledLineHeight)
                    Dim numX As Integer
                    Select Case _lineNumAlign
                        Case TextAlignMode.Left
                            numX = gutterLeft + _scaledLineNumPadL
                        Case TextAlignMode.Center
                            numX = gutterLeft + _scaledLineNumPadL + (contentW - numW) \ 2
                        Case Else ' Right
                            numX = gutterLeft + _scaledLineNumPadL + contentW - numW
                    End Select
                    TextRenderer.DrawText(g, numStr, useNumFont,
                        New Rectangle(numX, lineY, numW, _scaledLineHeight),
                        _lineNumForeColor, TF)
                End If
            Next
            g.ResetClip()
        End If

        ' 绘制文本内容
        g.SetClip(New Rectangle(textLeft, textTop, textWidth, textHeight))
        Dim isEmpty As Boolean = (_lines.Count = 1 AndAlso _lines(0).Length = 0)
        If isEmpty AndAlso Not String.IsNullOrEmpty(水印文本) Then
            Dim waterLineY As Integer = If(isSingleLine, singleLineY, textTop)
            Dim waterAlignOff As Integer = If(启用多行 OrElse 文本对齐 = TextAlignMode.Left, 0,
                ComputeAlignOffset(MeasureWidth(水印文本), textWidth))
            TextRenderer.DrawText(g, 水印文本, Font,
                New Rectangle(textLeft + waterAlignOff, waterLineY, textWidth, _scaledLineHeight),
                水印颜色, TF)
        End If
        Dim wrapActive As Boolean = IsWordWrapActive()
        Dim minL As Integer = 0, minC As Integer = 0, maxL As Integer = 0, maxC As Integer = 0
        If _hasSelection Then
            GetOrderedSelection(minL, minC, maxL, maxC)
        End If
        For vi As Integer = startVi To endVi
            Dim vl = _visualLines(vi)
            Dim lineY As Integer = If(isSingleLine, singleLineY,
                textTop + (vi - _scrollLineOffset) * _scaledLineHeight)
            Dim alignOff As Integer = If(wrapActive, 0, GetAlignOffsetXForLine(vl.LogicalLine, textWidth))
            Dim scrollX As Integer = If(wrapActive, 0, _scrollXOffset)
            If _hasSelection Then
                DrawVisualLineSelection(g, vl, lineY, textLeft, alignOff, scrollX, minL, minC, maxL, maxC)
            End If
            If vl.Length > 0 Then
                DrawLineRuns(g, vl.LogicalLine, vl.StartCol, vl.Length,
                    textLeft + alignOff - scrollX, lineY)
            End If
        Next
        If Focused AndAlso _caretVisible Then
            DrawCaret(g, textLeft, textTop)
        End If
        g.ResetClip()
    End Sub

    Private Sub DrawVisualLineSelection(g As Graphics, vl As VisualLineInfo, lineY As Integer, textLeft As Integer,
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
        Dim x1 As Integer = textLeft + alignOff + MeasureLineWidth(li, vl.StartCol, drawStart - vl.StartCol) - scrollX
        Dim x2 As Integer
        If selEnd > vlEnd OrElse li < maxL Then
            x2 = textLeft + alignOff + MeasureLineWidth(li, vl.StartCol, vl.Length) + 6 - scrollX
        Else
            x2 = textLeft + alignOff + MeasureLineWidth(li, vl.StartCol, drawEnd - vl.StartCol) - scrollX
        End If
        If x2 <= x1 Then Return
        Using br As New SolidBrush(选区背景色)
            g.FillRectangle(br, x1, lineY, x2 - x1, _scaledLineHeight)
        End Using
    End Sub

    Private Sub DrawCaret(g As Graphics, textLeft As Integer, textTop As Integer)
        Dim vi As Integer = GetVisualLineIndex(_caretLine, _caretCol)
        If vi < _scrollLineOffset OrElse vi >= _scrollLineOffset + VisibleLineCount() + 2 Then
            Return
        End If
        Dim vl = _visualLines(vi)
        Dim wrapActive As Boolean = IsWordWrapActive()
        Dim alignOff As Integer = If(wrapActive, 0, GetAlignOffsetXForLine(_caretLine, TextAreaWidth()))
        Dim scrollX As Integer = If(wrapActive, 0, _scrollXOffset)
        Dim cx As Integer = textLeft + alignOff + MeasureLineWidth(_caretLine, vl.StartCol, _caretCol - vl.StartCol) - scrollX
        Dim lineY As Integer
        If Not 启用多行 Then
            Dim bi2 As Integer = ScaledBorderWidth()
            Dim textHeight As Integer = ClientRectangle.Height - Math.Max(Padding.Top, bi2) - Math.Max(Padding.Bottom, bi2)
            lineY = textTop + (textHeight - _scaledLineHeight) \ 2
        Else
            lineY = textTop + (vi - _scrollLineOffset) * _scaledLineHeight
        End If
        Dim caretH As Integer = _scaledLineHeight - 2
        Dim caretY As Integer = lineY + (_scaledLineHeight - caretH) \ 2
        Using br As New SolidBrush(光标颜色)
            g.FillRectangle(br, cx, caretY, _scaledCaretWidth, caretH)
        End Using
    End Sub

    Private Sub DrawScrollBar(g As Graphics, w As Integer, h As Integer)
        If Not _scrollBarVisible Then Return
        Dim s As Single = DpiScale()
        Dim scaledBorder As Integer = CInt(Math.Round(边框宽度 * s))
        Dim scaledRadius As Integer = CInt(Math.Round(边框圆角半径 * s))
        Dim scaledScrollW As Integer = CInt(Math.Round(滚动条宽度 * s))
        _scrollBar.ComputeLayout(w, h, scaledBorder, scaledRadius, 0, 0, scaledScrollW,
            _visualLines.Count, VisibleLineCount(), _scrollLineOffset)
        _scrollBar.Draw(g, w, h, scaledBorder, scaledRadius, scaledScrollW,
            滚动条轨道颜色, 滚动条颜色, 滚动条悬停颜色)
    End Sub

    Private Sub DrawLineRuns(g As Graphics, lineIndex As Integer, vlStartCol As Integer,
                              vlLength As Integer, x As Integer, lineY As Integer)
        Dim runs = _lineRuns(lineIndex)
        Dim lineStr = _lines(lineIndex)
        Dim vlEnd = vlStartCol + vlLength
        Dim links = If(lineIndex < _lineLinks.Count, _lineLinks(lineIndex), Nothing)
        Dim hasLinks As Boolean = links IsNot Nothing AndAlso links.Count > 0 AndAlso (_passwordChar = vbNullChar OrElse 启用多行)
        If runs Is Nothing OrElse runs.Count = 0 Then
            If Not hasLinks Then
                Dim text = GetDisplayText(lineStr.Substring(vlStartCol, vlLength))
                TextRenderer.DrawText(g, text, Font, New Rectangle(x, lineY, Short.MaxValue, _scaledLineHeight),
                    ForeColor, TF)
            Else
                DrawSegmentsWithLinks(g, lineStr, vlStartCol, vlEnd, x, lineY, ForeColor, Font, links)
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
                TextRenderer.DrawText(g, segText, useFont, New Rectangle(drawX, lineY, Short.MaxValue, _scaledLineHeight),
                    useFore, TF)
                drawX += TextRenderHelper.MeasureTextWidth(segText, useFont, _scaledLineHeight)
            Else
                drawX = DrawSegmentsWithLinks(g, lineStr, segStart, segEnd, drawX, lineY, useFore, useFont, links)
            End If
        Next
    End Sub

    Private Function DrawSegmentsWithLinks(g As Graphics, lineStr As String, startCol As Integer, endCol As Integer,
                                            x As Integer, lineY As Integer, baseFore As Color, baseFont As Font,
                                            links As List(Of LinkRange)) As Integer
        Dim pos = startCol
        Dim drawX = x
        For Each link In links
            Dim linkEnd = link.StartCol + link.Length
            If link.StartCol >= endCol OrElse linkEnd <= pos Then Continue For
            If pos < link.StartCol AndAlso pos < endCol Then
                Dim nonLinkEnd = Math.Min(link.StartCol, endCol)
                Dim segText = GetDisplayText(lineStr.Substring(pos, nonLinkEnd - pos))
                TextRenderer.DrawText(g, segText, baseFont, New Rectangle(drawX, lineY, Short.MaxValue, _scaledLineHeight),
                    baseFore, TF)
                drawX += TextRenderHelper.MeasureTextWidth(segText, baseFont, _scaledLineHeight)
                pos = nonLinkEnd
            End If
            Dim overlapStart = Math.Max(pos, link.StartCol)
            Dim overlapEnd = Math.Min(endCol, linkEnd)
            If overlapStart < overlapEnd Then
                Dim linkText = GetDisplayText(lineStr.Substring(overlapStart, overlapEnd - overlapStart))
                Dim linkFont = If(链接下划线, GetUnderlineFont(baseFont), baseFont)
                TextRenderer.DrawText(g, linkText, linkFont, New Rectangle(drawX, lineY, Short.MaxValue, _scaledLineHeight),
                    链接颜色, TF)
                drawX += TextRenderHelper.MeasureTextWidth(linkText, baseFont, _scaledLineHeight)
                pos = overlapEnd
            End If
        Next
        If pos < endCol Then
            Dim segText = GetDisplayText(lineStr.Substring(pos, endCol - pos))
            TextRenderer.DrawText(g, segText, baseFont, New Rectangle(drawX, lineY, Short.MaxValue, _scaledLineHeight),
                baseFore, TF)
            drawX += TextRenderHelper.MeasureTextWidth(segText, baseFont, _scaledLineHeight)
        End If
        Return drawX
    End Function

#End Region

#Region "消息与输入"
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
                    Dim result As String = ImeHelper.GetResultString(Handle)
                    If result IsNot Nothing Then
                        PushUndo()
                        InsertTextCore(result)
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

    Private Sub HandleWmChar(charCode As Integer)
        Select Case charCode
            Case 1  ' Ctrl+A
                SelectAll()
            Case 3  ' Ctrl+C
                If _passwordChar = vbNullChar OrElse 启用多行 Then CopySelection()
            Case 22 ' Ctrl+V
                PasteText()
            Case 24 ' Ctrl+X
                If _passwordChar = vbNullChar OrElse 启用多行 Then CutSelection()
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

#Region "键盘导航 (OnKeyDown)"
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

#Region "鼠标处理"
    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        Focus()
        If e.Button = MouseButtons.Left Then
            If _scrollBarVisible Then
                If _scrollBar.BeginDrag(e.Location, _scrollLineOffset) Then Return
                Dim newOff As Integer = _scrollBar.TrackClick(e.Location, _scrollLineOffset, _visualLines.Count, VisibleLineCount())
                If newOff <> _scrollLineOffset Then
                    _scrollLineOffset = newOff
                    UpdateScrollBar()
                    Invalidate()
                    Return
                End If
            End If
            _mouseDownSelecting = True
            Dim pos As Point = HitTest(e.X, e.Y)
            _caretLine = pos.Y
            _caretCol = pos.X
            _mouseDownLinkText = FindLinkAtPosition(pos.Y, pos.X)
            _selAnchorLine = _caretLine
            _selAnchorCol = _caretCol
            _hasSelection = False
            ResetCaretBlink()
            Invalidate()
        End If
    End Sub
    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If _scrollBar.IsDragging Then
            Dim total As Integer = _visualLines.Count
            Dim vis As Integer = VisibleLineCount()
            _scrollLineOffset = _scrollBar.DragMove(e.Y, total, vis)
            Invalidate()
            Return
        End If
        If _scrollBarVisible Then
            If _scrollBar.UpdateHover(e.Location) Then Invalidate()
            If _scrollBar.TrackRect.Contains(e.Location) Then
                Cursor = Cursors.Default
            Else
                UpdateCursorForLink(e.X, e.Y)
            End If
        Else
            UpdateCursorForLink(e.X, e.Y)
        End If
        If _mouseDownSelecting AndAlso e.Button = MouseButtons.Left Then
            _lastMousePos = e.Location
            Dim pos As Point = HitTest(e.X, e.Y)
            _caretLine = pos.Y
            _caretCol = pos.X
            _hasSelection = (_caretLine <> _selAnchorLine OrElse _caretCol <> _selAnchorCol)
            EnsureCaretVisible()
            If 启用多行 AndAlso (e.Y < Math.Max(Padding.Top, ScaledBorderWidth()) OrElse e.Y > ClientRectangle.Height - Math.Max(Padding.Bottom, ScaledBorderWidth())) Then
                If Not _autoScrollTimer.Enabled Then _autoScrollTimer.Start()
            Else
                _autoScrollTimer.Stop()
            End If
            Invalidate()
        End If
    End Sub
    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If e.Button = MouseButtons.Left AndAlso Not _hasSelection AndAlso _mouseDownLinkText IsNot Nothing Then
            Dim upPos As Point = HitTest(e.X, e.Y)
            Dim upLink As String = FindLinkAtPosition(upPos.Y, upPos.X)
            If upLink = _mouseDownLinkText Then
                RaiseEvent LinkClicked(Me, New LinkClickedEventArgs(_mouseDownLinkText))
            End If
        End If
        _mouseDownLinkText = Nothing
        _mouseDownSelecting = False
        _scrollBar.EndDrag()
        _autoScrollTimer.Stop()
    End Sub
    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        If Not 启用多行 Then Return
        _scrollLineOffset = ScrollBarRenderer.HandleWheel(e.Delta, _scrollLineOffset, _visualLines.Count, VisibleLineCount())
        Invalidate()
    End Sub
    Protected Overrides Sub OnMouseDoubleClick(e As MouseEventArgs)
        MyBase.OnMouseDoubleClick(e)
        If e.Button <> MouseButtons.Left Then Return
        Dim line As String = _lines(_caretLine)
        If line.Length = 0 Then Return
        Dim col As Integer = Math.Min(_caretCol, line.Length - 1)
        Dim left As Integer = col
        While left > 0 AndAlso Not Char.IsWhiteSpace(line(left - 1))
            left -= 1
        End While
        Dim right As Integer = col
        While right < line.Length AndAlso Not Char.IsWhiteSpace(line(right))
            right += 1
        End While
        _selAnchorLine = _caretLine
        _selAnchorCol = left
        _caretCol = right
        _hasSelection = left <> right
        ResetCaretBlink()
        Invalidate()
    End Sub
    Private Function HitTest(x As Integer, y As Integer) As Point
        Dim bi As Integer = ScaledBorderWidth()
        Dim gutterW As Integer = LineNumberGutterWidth()
        Dim textLeft As Integer = If(gutterW > 0, bi + gutterW + Padding.Left, Math.Max(Padding.Left, bi))
        Dim vi As Integer
        If 启用多行 Then
            Dim textTop As Integer = Math.Max(Padding.Top, bi)
            vi = (y - textTop) \ _scaledLineHeight + _scrollLineOffset
        Else
            vi = 0
        End If
        vi = Math.Max(0, Math.Min(_visualLines.Count - 1, vi))
        Dim vl = _visualLines(vi)
        Dim wrapActive As Boolean = IsWordWrapActive()
        Dim alignOff As Integer = If(wrapActive, 0, GetAlignOffsetXForLine(vl.LogicalLine, TextAreaWidth()))
        Dim scrollX As Integer = If(wrapActive, 0, _scrollXOffset)
        Dim col As Integer = FindColFromXForLine(vl.LogicalLine, vl.StartCol, vl.Length, x - textLeft - alignOff + scrollX)
        Return New Point(col, vl.LogicalLine)
    End Function
    Private Function FindColFromX(lineStr As String, x As Integer) As Integer
        Return TextRenderHelper.FindColFromX(GetDisplayText(lineStr), x, Font, _scaledLineHeight)
    End Function
    Private Sub UpdateCursorForLink(x As Integer, y As Integer)
        Dim hitPos As Point = HitTest(x, y)
        Cursor = If(FindLinkAtPosition(hitPos.Y, hitPos.X) IsNot Nothing, Cursors.Hand, Cursors.IBeam)
    End Sub
#End Region

#Region "光标移动"
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
                Dim currentX As Integer = MeasureLineWidth(_caretLine, curVl.StartCol, _caretCol - curVl.StartCol)
                Dim newVi As Integer = Math.Max(0, Math.Min(_visualLines.Count - 1, curVi + deltaLine))
                Dim newVl = _visualLines(newVi)
                _caretLine = newVl.LogicalLine
                _caretCol = FindColFromXForLine(newVl.LogicalLine, newVl.StartCol, newVl.Length, currentX)
            Else
                Dim currentX As Integer = MeasureLineWidth(_caretLine, 0, _caretCol)
                _caretLine = Math.Max(0, Math.Min(_lines.Count - 1, _caretLine + deltaLine))
                _caretCol = FindColFromXForLine(_caretLine, 0, _lines(_caretLine).Length, currentX)
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
                Dim caretX As Integer = MeasureLineWidth(_caretLine, 0, _caretCol)
                If Not 启用多行 AndAlso 文本对齐 <> TextAlignMode.Left Then
                    Dim lineW As Integer = MeasureLineWidth(_caretLine, 0, _lines(_caretLine).Length)
                    If lineW < areaW Then
                        _scrollXOffset = 0
                        Return
                    End If
                End If

                Dim margin As Integer = _scaledCaretWidth + 2
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

#Region "文本编辑核心"
    Private Sub InsertTextCore(text As String)
        DeleteSelection()
        Dim fromLine As Integer = _caretLine
        Dim normalized As String = text.Replace(vbCr, "")
        If _maxLength > 0 Then
            Dim currentLen As Integer = _lines.Sum(Function(l) l.Length) + _lines.Count - 1
            Dim remaining As Integer = _maxLength - currentLen
            If remaining <= 0 Then Return
            If normalized.Length > remaining Then
                normalized = normalized.Substring(0, remaining)
            End If
        End If
        If normalized.Length = 0 Then Return
        If Not normalized.Contains(vbLf) Then
            Dim line As String = _lines(_caretLine)
            Dim insertCol As Integer = _caretCol
            _lines(_caretLine) = String.Concat(line.AsSpan(0, _caretCol), normalized, line.AsSpan(_caretCol))
            InsertIntoRuns(_caretLine, insertCol, normalized.Length)
            _caretCol += normalized.Length
        Else
            Dim parts() As String = normalized.Split(vbLf)
            Dim tail As String = _lines(_caretLine).Substring(_caretCol)
            Dim insertLine As Integer = _caretLine
            Dim insertCol As Integer = _caretCol
            Dim tailRuns = SplitLineRunsAt(insertLine, insertCol)
            _lines(_caretLine) = String.Concat(_lines(_caretLine).AsSpan(0, _caretCol), parts(0))
            If parts(0).Length > 0 Then
                InsertIntoRuns(insertLine, insertCol, parts(0).Length)
            End If
            For i As Integer = 1 To parts.Length - 1
                _caretLine += 1
                _lines.Insert(_caretLine, If(i = parts.Length - 1, parts(i) & tail, parts(i)))
                If i = parts.Length - 1 AndAlso tailRuns IsNot Nothing Then
                    Dim newLineRuns As New List(Of TextRun)
                    If parts(i).Length > 0 Then
                        newLineRuns.Add(New TextRun(0, parts(i).Length))
                    End If
                    For Each r In tailRuns
                        newLineRuns.Add(New TextRun(r.StartCol + parts(i).Length, r.Length, r.ForeColor, r.RunFont))
                    Next
                    _lineRuns.Insert(_caretLine, MergeAdjacentRuns(newLineRuns))
                Else
                    _lineRuns.Insert(_caretLine, Nothing)
                End If
                _lineLinks.Insert(_caretLine, Nothing)
                _lineStates.Insert(_caretLine, 0)
            Next
            _caretCol = parts(parts.Length - 1).Length
        End If
        NotifyTextChanged(fromLine, _caretLine)
    End Sub
    Private Sub InsertNewLine()
        Dim fromLine As Integer = _caretLine
        Dim line As String = _lines(_caretLine)
        Dim newLineRuns = SplitLineRunsAt(_caretLine, _caretCol)
        _lines(_caretLine) = line.Substring(0, _caretCol)
        _caretLine += 1
        _lines.Insert(_caretLine, line.Substring(_caretCol))
        _lineRuns.Insert(_caretLine, newLineRuns)
        _lineLinks.Insert(_caretLine, Nothing)
        _lineStates.Insert(_caretLine, 0)
        _caretCol = 0
        NotifyTextChanged(fromLine, _caretLine)
    End Sub
    Private Sub HandleBackspace()
        If _hasSelection Then
            PushUndo()
            DeleteSelection()
            NotifyTextChanged(_caretLine, _caretLine)
        ElseIf _caretCol > 0 Then
            PushUndo()
            Dim line As String = _lines(_caretLine)
            _lines(_caretLine) = String.Concat(line.AsSpan(0, _caretCol - 1), line.AsSpan(_caretCol))
            RemoveFromRuns(_caretLine, _caretCol - 1, 1)
            _caretCol -= 1
            NotifyTextChanged(_caretLine, _caretLine)
        ElseIf _caretLine > 0 Then
            PushUndo()
            Dim prev As String = _lines(_caretLine - 1)
            _caretCol = prev.Length
            MergeLineRuns(_caretLine - 1, _caretLine, prev.Length)
            _lines(_caretLine - 1) = prev & _lines(_caretLine)
            _lines.RemoveAt(_caretLine)
            _lineRuns.RemoveAt(_caretLine)
            _lineLinks.RemoveAt(_caretLine)
            _lineStates.RemoveAt(_caretLine)
            _caretLine -= 1
            NotifyTextChanged(_caretLine, _caretLine)
        End If
    End Sub
    Private Sub HandleDelete()
        If _hasSelection Then
            PushUndo()
            DeleteSelection()
            NotifyTextChanged(_caretLine, _caretLine)
        ElseIf _caretCol < _lines(_caretLine).Length Then
            PushUndo()
            Dim line As String = _lines(_caretLine)
            _lines(_caretLine) = String.Concat(line.AsSpan(0, _caretCol), line.AsSpan(_caretCol + 1))
            RemoveFromRuns(_caretLine, _caretCol, 1)
            NotifyTextChanged(_caretLine, _caretLine)
        ElseIf _caretLine < _lines.Count - 1 Then
            PushUndo()
            MergeLineRuns(_caretLine, _caretLine + 1, _lines(_caretLine).Length)
            _lines(_caretLine) = _lines(_caretLine) & _lines(_caretLine + 1)
            _lines.RemoveAt(_caretLine + 1)
            _lineRuns.RemoveAt(_caretLine + 1)
            _lineLinks.RemoveAt(_caretLine + 1)
            _lineStates.RemoveAt(_caretLine + 1)
            NotifyTextChanged(_caretLine, _caretLine)
        End If
    End Sub
    Private Sub DeleteSelection()
        If Not _hasSelection Then Return
        Dim minL, minC, maxL, maxC As Integer
        GetOrderedSelection(minL, minC, maxL, maxC)

        If minL = maxL Then
            RemoveFromRuns(minL, minC, maxC - minC)
            _lines(minL) = String.Concat(_lines(minL).AsSpan(0, minC), _lines(minL).AsSpan(maxC))
        Else
            Dim tailRuns As List(Of TextRun) = Nothing
            If _lineRuns(maxL) IsNot Nothing Then
                tailRuns = New List(Of TextRun)
                For Each r In _lineRuns(maxL)
                    Dim rEnd = r.StartCol + r.Length
                    If r.StartCol >= maxC Then
                        tailRuns.Add(New TextRun(r.StartCol - maxC + minC, r.Length, r.ForeColor, r.RunFont))
                    ElseIf rEnd > maxC Then
                        tailRuns.Add(New TextRun(minC, rEnd - maxC, r.ForeColor, r.RunFont))
                    End If
                Next
            End If
            If _lineRuns(minL) IsNot Nothing Then
                Dim leftRuns As New List(Of TextRun)
                For Each r In _lineRuns(minL)
                    If r.StartCol + r.Length <= minC Then
                        leftRuns.Add(r)
                    ElseIf r.StartCol < minC Then
                        leftRuns.Add(New TextRun(r.StartCol, minC - r.StartCol, r.ForeColor, r.RunFont))
                    End If
                Next
                If tailRuns IsNot Nothing Then leftRuns.AddRange(tailRuns)
                _lineRuns(minL) = If(leftRuns.Count > 0, MergeAdjacentRuns(leftRuns), Nothing)
            ElseIf tailRuns IsNot Nothing Then
                Dim combined As New List(Of TextRun)
                If minC > 0 Then combined.Add(New TextRun(0, minC))
                combined.AddRange(tailRuns)
                _lineRuns(minL) = MergeAdjacentRuns(combined)
            End If
            _lines(minL) = String.Concat(_lines(minL).AsSpan(0, minC), _lines(maxL).AsSpan(maxC))
            For i As Integer = maxL To minL + 1 Step -1
                _lines.RemoveAt(i)
                _lineRuns.RemoveAt(i)
                _lineLinks.RemoveAt(i)
                _lineStates.RemoveAt(i)
            Next
        End If
        _caretLine = minL
        _caretCol = minC
        ClearSelection()
    End Sub
#End Region

#Region "选区与剪贴板"
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
    Private Sub CopySelection()
        If _hasSelection Then
            Try
                Clipboard.SetText(GetSelectedText())
            Catch ex As ExternalException
            End Try
        End If
    End Sub
    Private Sub CutSelection()
        If _hasSelection AndAlso Not 启用只读模式 Then
            PushUndo()
            CopySelection()
            DeleteSelection()
            NotifyTextChanged(_caretLine, _caretLine)
        End If
    End Sub
    Private Sub PasteText()
        If 启用只读模式 Then Return
        Try
            If Clipboard.ContainsText() Then
                PushUndo()
                InsertTextCore(Clipboard.GetText())
            End If
        Catch ex As ExternalException
        End Try
    End Sub
#End Region

#Region "撤回"
    Private Sub PushUndo()
        If _maxUndo = 0 Then Return
        _undoStack.Add(New TextSnapshot(_lines, CloneLineRuns(), _caretLine, _caretCol))
        If _undoStack.Count > _maxUndo Then
            _undoStack.RemoveAt(0)
        End If
    End Sub
    Private Sub Undo()
        If _maxUndo = 0 OrElse _undoStack.Count = 0 Then Return
        Dim snap As TextSnapshot = _undoStack(_undoStack.Count - 1)
        _undoStack.RemoveAt(_undoStack.Count - 1)
        _lines = New List(Of String)(snap.Lines)
        _lineRuns = If(snap.LineRuns, New List(Of List(Of TextRun)))
        While _lineRuns.Count < _lines.Count
            _lineRuns.Add(Nothing)
        End While
        _caretLine = Math.Min(snap.CaretLine, _lines.Count - 1)
        _caretCol = Math.Min(snap.CaretCol, _lines(_caretLine).Length)
        ClearSelection()
        NotifyTextChanged()
    End Sub
#End Region

#Region "滚动条与输入法"
    Private Sub UpdateScrollBar()
        If Not 启用多行 OrElse Not IsHandleCreated Then
            _scrollBarVisible = False
            Return
        End If
        _scrollBarVisible = _visualLines.Count > VisibleLineCount()
    End Sub
    Private Sub UpdateImeWindow()
        If Not IsHandleCreated Then Return
        Dim vi As Integer = GetVisualLineIndex(_caretLine, _caretCol)
        Dim vl = _visualLines(vi)
        Dim wrapActive As Boolean = IsWordWrapActive()
        Dim bi As Integer = ScaledBorderWidth()
        Dim gutterW As Integer = LineNumberGutterWidth()
        Dim imeLeft As Integer = If(gutterW > 0, bi + gutterW + Padding.Left, Math.Max(Padding.Left, bi))
        Dim imeTop As Integer = Math.Max(Padding.Top, bi)
        Dim alignOff As Integer = If(wrapActive, 0, GetAlignOffsetXForLine(_caretLine, TextAreaWidth()))
        Dim scrollX As Integer = If(wrapActive, 0, _scrollXOffset)
        Dim cx As Integer = imeLeft + alignOff + MeasureLineWidth(_caretLine, vl.StartCol, _caretCol - vl.StartCol) - scrollX
        Dim cy As Integer
        If 启用多行 Then
            cy = imeTop + (vi - _scrollLineOffset) * _scaledLineHeight + _scaledLineHeight
        Else
            Dim textHeight As Integer = ClientRectangle.Height - imeTop - Math.Max(Padding.Bottom, bi)
            cy = imeTop + (textHeight - _scaledLineHeight) \ 2 + _scaledLineHeight
        End If
        ImeHelper.SetCompositionPosition(Handle, cx, cy)
    End Sub
#End Region

#Region "度量与布局"
    Private Function MeasureWidth(text As String) As Integer
        Return TextRenderHelper.MeasureTextWidth(GetDisplayText(text), Font, _scaledLineHeight)
    End Function
    Private Function MeasureLineWidth(lineIndex As Integer, startCol As Integer, length As Integer) As Integer
        If length <= 0 Then Return 0
        Dim runs = _lineRuns(lineIndex)
        If runs Is Nothing OrElse runs.Count = 0 Then
            Return MeasureWidth(_lines(lineIndex).Substring(startCol, length))
        End If
        Dim endCol = startCol + length
        Dim totalWidth = 0
        Dim lineStr = _lines(lineIndex)
        For Each r In runs
            Dim rEnd = r.StartCol + r.Length
            Dim segStart = Math.Max(r.StartCol, startCol)
            Dim segEnd = Math.Min(rEnd, endCol)
            If segStart >= segEnd Then Continue For
            Dim useFont = If(r.RunFont, Font)
            Dim segText = GetDisplayText(lineStr.Substring(segStart, segEnd - segStart))
            totalWidth += TextRenderHelper.MeasureTextWidth(segText, useFont, _scaledLineHeight)
        Next
        Return totalWidth
    End Function
    Private Function FindColFromXForLine(lineIndex As Integer, vlStartCol As Integer, vlLength As Integer, x As Integer) As Integer
        If x <= 0 Then Return vlStartCol
        Dim runs = _lineRuns(lineIndex)
        If runs Is Nothing OrElse runs.Count = 0 Then
            Return vlStartCol + FindColFromX(_lines(lineIndex).Substring(vlStartCol, vlLength), x)
        End If
        Dim vlEnd = vlStartCol + vlLength
        Dim accWidth = 0
        Dim lineStr = _lines(lineIndex)
        For Each r In runs
            Dim rEnd = r.StartCol + r.Length
            Dim segStart = Math.Max(r.StartCol, vlStartCol)
            Dim segEnd = Math.Min(rEnd, vlEnd)
            If segStart >= segEnd Then Continue For
            Dim useFont = If(r.RunFont, Font)
            Dim segText = GetDisplayText(lineStr.Substring(segStart, segEnd - segStart))
            Dim segWidth = TextRenderHelper.MeasureTextWidth(segText, useFont, _scaledLineHeight)
            If accWidth + segWidth > x Then
                Dim localCol = TextRenderHelper.FindColFromX(segText, x - accWidth, useFont, _scaledLineHeight)
                Return segStart + localCol
            End If
            accWidth += segWidth
        Next
        Return vlStartCol + vlLength
    End Function
    Private Function GetAlignOffsetXForLine(lineIndex As Integer, areaWidth As Integer) As Integer
        If 启用多行 OrElse 文本对齐 = TextAlignMode.Left Then Return 0
        Return ComputeAlignOffset(MeasureLineWidth(lineIndex, 0, _lines(lineIndex).Length), areaWidth)
    End Function
    Private Function ComputeAlignOffset(textW As Integer, areaWidth As Integer) As Integer
        If textW >= areaWidth Then Return 0
        Select Case 文本对齐
            Case TextAlignMode.Center : Return (areaWidth - textW) \ 2
            Case TextAlignMode.Right : Return areaWidth - textW
            Case Else : Return 0
        End Select
    End Function
    Private Function VisibleLineCount() As Integer
        Dim bi As Integer = ScaledBorderWidth()
        Dim h As Integer = ClientRectangle.Height - Math.Max(Padding.Top, bi) - Math.Max(Padding.Bottom, bi)
        Return Math.Max(1, h \ _scaledLineHeight)
    End Function
    Private Function TextAreaWidth()
        Dim bi As Integer = ScaledBorderWidth()
        Dim scrollW As Integer = If(_scrollBarVisible, CInt(Math.Round(滚动条宽度 * DpiScale())) + ScrollBarRenderer.Margin * 2, 0)
        Dim gutterW As Integer = LineNumberGutterWidth()
        Dim leftUsed As Integer = If(gutterW > 0, bi + gutterW + Padding.Left, Math.Max(Padding.Left, bi))
        Return ClientRectangle.Width - leftUsed - Math.Max(Padding.Right, bi) - scrollW
    End Function
    Private Function LineNumberGutterWidth() As Integer
        If Not _showLineNumbers OrElse Not 启用多行 Then Return 0
        Dim useFont As Font = If(_lineNumFont, Font)
        Dim maxNum As String = _lines.Count.ToString()
        Dim numW As Integer = TextRenderHelper.MeasureTextWidth(maxNum, useFont, _scaledLineHeight)
        Return _scaledLineNumPadL + numW + _scaledLineNumPadR
    End Function
#End Region

#Region "视觉行"
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
                    Dim fitLen As Integer = FindFitLength(li, startCol, areaW)
                    _visualLines.Add(New VisualLineInfo(li, startCol, fitLen))
                    startCol += fitLen
                End While
            End If
        Next
        If _visualLines.Count = 0 Then
            _visualLines.Add(New VisualLineInfo(0, 0, 0))
        End If
    End Sub
    Private Function FindFitLength(lineIndex As Integer, startCol As Integer, maxWidth As Integer) As Integer
        Dim line As String = _lines(lineIndex)
        Dim remaining As Integer = line.Length - startCol
        If remaining <= 0 Then Return 0
        If MeasureLineWidth(lineIndex, startCol, remaining) <= maxWidth Then Return remaining
        Dim lo As Integer = 1
        Dim hi As Integer = remaining
        Dim best As Integer = 1
        While lo <= hi
            Dim mid As Integer = (lo + hi) \ 2
            If MeasureLineWidth(lineIndex, startCol, mid) <= maxWidth Then
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
#End Region

#Region "文本格式 (Runs)"
    Private Shared Function MergeAdjacentRuns(runs As List(Of TextRun)) As List(Of TextRun)
        If runs.Count <= 1 Then Return runs
        Dim merged As New List(Of TextRun) From {runs(0)}
        For i = 1 To runs.Count - 1
            Dim last = merged(merged.Count - 1)
            If last.ForeColor = runs(i).ForeColor AndAlso
               Object.Equals(last.RunFont, runs(i).RunFont) AndAlso
               last.StartCol + last.Length = runs(i).StartCol Then
                merged(merged.Count - 1) = New TextRun(last.StartCol, last.Length + runs(i).Length, last.ForeColor, last.RunFont)
            Else
                merged.Add(runs(i))
            End If
        Next
        Return merged
    End Function
    Private Sub InsertIntoRuns(lineIndex As Integer, col As Integer, length As Integer)
        Dim runs = _lineRuns(lineIndex)
        If runs Is Nothing Then Return
        Dim newRuns As New List(Of TextRun)
        Dim defaultInserted As Boolean = False
        For Each r In runs
            Dim rEnd = r.StartCol + r.Length
            If rEnd <= col Then
                newRuns.Add(r)
            ElseIf r.StartCol >= col Then
                If Not defaultInserted Then
                    newRuns.Add(New TextRun(col, length))
                    defaultInserted = True
                End If
                newRuns.Add(New TextRun(r.StartCol + length, r.Length, r.ForeColor, r.RunFont))
            Else
                newRuns.Add(New TextRun(r.StartCol, col - r.StartCol, r.ForeColor, r.RunFont))
                newRuns.Add(New TextRun(col, length))
                defaultInserted = True
                newRuns.Add(New TextRun(col + length, rEnd - col, r.ForeColor, r.RunFont))
            End If
        Next
        If Not defaultInserted Then
            newRuns.Add(New TextRun(col, length))
        End If
        _lineRuns(lineIndex) = MergeAdjacentRuns(newRuns)
    End Sub
    Private Sub RemoveFromRuns(lineIndex As Integer, col As Integer, length As Integer)
        Dim runs = _lineRuns(lineIndex)
        If runs Is Nothing Then Return
        Dim removeEnd = col + length
        Dim newRuns As New List(Of TextRun)
        For Each r In runs
            Dim rEnd = r.StartCol + r.Length
            If rEnd <= col Then
                newRuns.Add(r)
            ElseIf r.StartCol >= removeEnd Then
                newRuns.Add(New TextRun(r.StartCol - length, r.Length, r.ForeColor, r.RunFont))
            ElseIf r.StartCol >= col AndAlso rEnd <= removeEnd Then
                ' Entirely within removal range
            Else
                If r.StartCol < col Then
                    If rEnd > removeEnd Then
                        newRuns.Add(New TextRun(r.StartCol, r.Length - length, r.ForeColor, r.RunFont))
                    Else
                        newRuns.Add(New TextRun(r.StartCol, col - r.StartCol, r.ForeColor, r.RunFont))
                    End If
                Else
                    Dim rightLen = rEnd - removeEnd
                    newRuns.Add(New TextRun(col, rightLen, r.ForeColor, r.RunFont))
                End If
            End If
        Next
        _lineRuns(lineIndex) = If(newRuns.Count > 0, MergeAdjacentRuns(newRuns), Nothing)
    End Sub
    Private Function SplitLineRunsAt(lineIndex As Integer, col As Integer) As List(Of TextRun)
        Dim runs = _lineRuns(lineIndex)
        If runs Is Nothing Then Return Nothing
        Dim leftRuns As New List(Of TextRun)
        Dim rightRuns As New List(Of TextRun)
        For Each r In runs
            Dim rEnd = r.StartCol + r.Length
            If rEnd <= col Then
                leftRuns.Add(r)
            ElseIf r.StartCol >= col Then
                rightRuns.Add(New TextRun(r.StartCol - col, r.Length, r.ForeColor, r.RunFont))
            Else
                leftRuns.Add(New TextRun(r.StartCol, col - r.StartCol, r.ForeColor, r.RunFont))
                rightRuns.Add(New TextRun(0, rEnd - col, r.ForeColor, r.RunFont))
            End If
        Next
        _lineRuns(lineIndex) = If(leftRuns.Count > 0, leftRuns, Nothing)
        Return If(rightRuns.Count > 0, rightRuns, Nothing)
    End Function
    Private Sub MergeLineRuns(targetLine As Integer, sourceLine As Integer, joinCol As Integer)
        Dim tRuns = _lineRuns(targetLine)
        Dim sRuns = _lineRuns(sourceLine)
        If tRuns Is Nothing AndAlso sRuns Is Nothing Then Return
        Dim newRuns As New List(Of TextRun)
        If tRuns IsNot Nothing Then
            newRuns.AddRange(tRuns)
        Else
            If joinCol > 0 Then newRuns.Add(New TextRun(0, joinCol))
        End If
        If sRuns IsNot Nothing Then
            For Each r In sRuns
                newRuns.Add(New TextRun(r.StartCol + joinCol, r.Length, r.ForeColor, r.RunFont))
            Next
        Else
            Dim srcLen = _lines(sourceLine).Length
            If srcLen > 0 Then newRuns.Add(New TextRun(joinCol, srcLen))
        End If
        _lineRuns(targetLine) = MergeAdjacentRuns(newRuns)
    End Sub
    Private Sub ApplyFormatToLine(lineIndex As Integer, col As Integer,
                                   length As Integer, foreColor As Color, runFont As Font)
        If _lineRuns(lineIndex) Is Nothing Then
            _lineRuns(lineIndex) = New List(Of TextRun) From {
                New TextRun(0, _lines(lineIndex).Length)
            }
        End If
        Dim runs = _lineRuns(lineIndex)
        Dim newRuns As New List(Of TextRun)
        Dim applyStart = col
        Dim applyEnd = col + length
        For Each r In runs
            Dim rEnd = r.StartCol + r.Length
            If rEnd <= applyStart OrElse r.StartCol >= applyEnd Then
                newRuns.Add(r)
            Else
                If r.StartCol < applyStart Then
                    newRuns.Add(New TextRun(r.StartCol, applyStart - r.StartCol, r.ForeColor, r.RunFont))
                End If
                Dim overlapStart = Math.Max(r.StartCol, applyStart)
                Dim overlapEnd = Math.Min(rEnd, applyEnd)
                newRuns.Add(New TextRun(overlapStart, overlapEnd - overlapStart, foreColor, runFont))
                If rEnd > applyEnd Then
                    newRuns.Add(New TextRun(applyEnd, rEnd - applyEnd, r.ForeColor, r.RunFont))
                End If
            End If
        Next
        _lineRuns(lineIndex) = MergeAdjacentRuns(newRuns)
        Dim allDefault = True
        For Each r In _lineRuns(lineIndex)
            If r.ForeColor <> Color.Empty OrElse r.RunFont IsNot Nothing Then
                allDefault = False
                Exit For
            End If
        Next
        If allDefault Then _lineRuns(lineIndex) = Nothing
    End Sub
    Private Function CloneLineRuns() As List(Of List(Of TextRun))
        Dim clone As New List(Of List(Of TextRun))(_lineRuns.Count)
        For Each runs In _lineRuns
            If runs Is Nothing Then
                clone.Add(Nothing)
            Else
                clone.Add(New List(Of TextRun)(runs))
            End If
        Next
        Return clone
    End Function
#End Region

#Region "链接检测"
    Private Sub RebuildAllLinks()
        _lineLinks.Clear()
        For i = 0 To _lines.Count - 1
            _lineLinks.Add(Nothing)
            DetectLinksInLine(i)
        Next
    End Sub
    Private Sub UpdateLinksInRange(fromLine As Integer, toLine As Integer)
        If Not 启用链接识别 Then Return
        For i As Integer = fromLine To Math.Min(toLine, _lines.Count - 1)
            DetectLinksInLine(i)
        Next
    End Sub
    Private Sub DetectLinksInLine(lineIndex As Integer)
        If Not 启用链接识别 Then
            _lineLinks(lineIndex) = Nothing
            Return
        End If
        Dim line = _lines(lineIndex)
        If line.Length = 0 OrElse (_passwordChar <> vbNullChar AndAlso Not 启用多行) Then
            _lineLinks(lineIndex) = Nothing
            Return
        End If
        Dim matches = LinkRegex.Matches(line)
        If matches.Count = 0 Then
            _lineLinks(lineIndex) = Nothing
            Return
        End If
        Dim links As New List(Of LinkRange)
        For Each m As Match In matches
            Dim url = m.Value.TrimEnd("."c, ","c, ";"c, ":"c, "!"c, "?"c, ")"c, "]"c, ">"c, "'"c, """"c)
            If url.Length > 0 Then
                links.Add(New LinkRange(m.Index, url.Length, url))
            End If
        Next
        _lineLinks(lineIndex) = If(links.Count > 0, links, Nothing)
    End Sub
    Private Function FindLinkAtPosition(line As Integer, col As Integer) As String
        If Not 启用链接识别 Then Return Nothing
        If line < 0 OrElse line >= _lineLinks.Count Then Return Nothing
        Dim links = _lineLinks(line)
        If links Is Nothing Then Return Nothing
        For Each link In links
            If col >= link.StartCol AndAlso col < link.StartCol + link.Length Then
                Return link.Url
            End If
        Next
        Return Nothing
    End Function
    Private Function GetUnderlineFont(baseFont As Font) As Font
        If _underlineFontCache IsNot Nothing AndAlso Object.ReferenceEquals(_underlineFontBase, baseFont) Then
            Return _underlineFontCache
        End If
        _underlineFontCache?.Dispose()
        _underlineFontBase = baseFont
        _underlineFontCache = New Font(baseFont, baseFont.Style Or FontStyle.Underline)
        Return _underlineFontCache
    End Function
#End Region

#Region "语法高亮"
    Private Sub ApplySyntaxHighlighting()
        _lineStates.Clear()
        For i = 0 To _lines.Count - 1
            _lineStates.Add(0)
        Next
        If Not _enableSyntaxHighlight OrElse _syntaxHighlighter Is Nothing Then Return
        Dim prevState As Integer = 0
        For i = 0 To _lines.Count - 1
            Dim result = _syntaxHighlighter.HighlightLine(i, _lines(i), prevState)
            _lineStates(i) = result.EndState
            _lineRuns(i) = TokensToRuns(result.Tokens, _lines(i).Length)
            prevState = result.EndState
        Next
    End Sub
    Private Sub ApplySyntaxHighlightingToLine(lineIndex As Integer)
        If Not _enableSyntaxHighlight OrElse _syntaxHighlighter Is Nothing Then Return
        Dim prevState As Integer = If(lineIndex > 0 AndAlso lineIndex - 1 < _lineStates.Count, _lineStates(lineIndex - 1), 0)
        Dim result = _syntaxHighlighter.HighlightLine(lineIndex, _lines(lineIndex), prevState)
        While _lineStates.Count <= lineIndex
            _lineStates.Add(0)
        End While
        _lineStates(lineIndex) = result.EndState
        _lineRuns(lineIndex) = TokensToRuns(result.Tokens, _lines(lineIndex).Length)
    End Sub
    Private Sub UpdateSyntaxHighlightingFrom(fromLine As Integer, toLine As Integer)
        If Not _enableSyntaxHighlight OrElse _syntaxHighlighter Is Nothing Then Return
        Dim prevState As Integer = If(fromLine > 0, _lineStates(fromLine - 1), 0)
        For i As Integer = fromLine To _lines.Count - 1
            Dim oldEndState As Integer = _lineStates(i)
            Dim result = _syntaxHighlighter.HighlightLine(i, _lines(i), prevState)
            _lineStates(i) = result.EndState
            _lineRuns(i) = TokensToRuns(result.Tokens, _lines(i).Length)
            prevState = result.EndState
            If i > toLine AndAlso result.EndState = oldEndState Then Exit For
        Next
    End Sub
    Private Function TokensToRuns(tokens As List(Of SyntaxToken), lineLength As Integer) As List(Of TextRun)
        If tokens Is Nothing OrElse tokens.Count = 0 Then Return Nothing
        Dim runs As New List(Of TextRun)
        Dim pos As Integer = 0
        For Each tk In tokens
            If tk.StartCol > pos Then runs.Add(New TextRun(pos, tk.StartCol - pos))
            runs.Add(New TextRun(tk.StartCol, tk.Length, tk.ForeColor, Nothing))
            pos = tk.StartCol + tk.Length
        Next
        If pos < lineLength Then runs.Add(New TextRun(pos, lineLength - pos))
        Return MergeAdjacentRuns(runs)
    End Function
#End Region

#Region "通用辅助"
    Private Sub SetLinesFromString(s As String)
        Dim normalized As String = If(s, "").Replace(vbCr, "")
        _lines = New List(Of String)(normalized.Split(vbLf))
        If _lines.Count = 0 Then _lines.Add("")
        _lineRuns = New List(Of List(Of TextRun))(_lines.Count)
        _lineLinks = New List(Of List(Of LinkRange))(_lines.Count)
        _lineStates = New List(Of Integer)(_lines.Count)
        For i = 0 To _lines.Count - 1
            _lineRuns.Add(Nothing)
            _lineLinks.Add(Nothing)
            _lineStates.Add(0)
        Next
    End Sub
    Private Sub NotifyTextChanged()
        ApplySyntaxHighlighting()
        RebuildAllLinks()
        FinalizeTextChanged()
    End Sub
    Private Sub NotifyTextChanged(fromLine As Integer, toLine As Integer)
        UpdateSyntaxHighlightingFrom(fromLine, toLine)
        UpdateLinksInRange(fromLine, toLine)
        FinalizeTextChanged()
    End Sub
    Private Sub FinalizeTextChanged()
        RebuildVisualLines()
        Dim oldVisible As Boolean = _scrollBarVisible
        UpdateScrollBar()
        If _scrollBarVisible <> oldVisible Then
            RebuildVisualLines()
            UpdateScrollBar()
        End If
        EnsureCaretVisible()
        Invalidate()
        OnTextChanged(EventArgs.Empty)
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
    Private Function DpiScale() As Single
        Return _cachedDpiScale
    End Function
    Private Function ScaledBorderWidth() As Integer
        Return _cachedBorderInset
    End Function
    Private Sub UpdateDpiCache()
        _cachedDpiScale = Me.DeviceDpi / 96.0F
        _cachedBorderInset = CInt(Math.Round(边框宽度 * _cachedDpiScale))
        _scaledLineHeight = CInt(Math.Round(行高 * _cachedDpiScale))
        _scaledCaretWidth = CInt(Math.Round(光标线宽 * _cachedDpiScale))
        _scaledLineNumPadL = CInt(Math.Round(_lineNumPadLeft * _cachedDpiScale))
        _scaledLineNumPadR = CInt(Math.Round(_lineNumPadRight * _cachedDpiScale))
    End Sub
    Private Function IsWordWrapActive() As Boolean
        Return 启用多行 AndAlso _wordWrap
    End Function
    Private Sub AutoScrollTick(sender As Object, e As EventArgs)
        If Not _mouseDownSelecting Then
            _autoScrollTimer.Stop()
            Return
        End If
        Dim bi As Integer = ScaledBorderWidth()
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
    Private Function GetDisplayText(text As String) As String
        If _passwordChar = vbNullChar OrElse 启用多行 Then Return text
        Return New String(_passwordChar, text.Length)
    End Function
    Private Function GetAbsoluteOffset(line As Integer, col As Integer) As Integer
        Dim offset As Integer = 0
        For i As Integer = 0 To line - 1
            offset += _lines(i).Length + 2
        Next
        Return offset + col
    End Function
    Private Function GetLineColFromAbsolute(absOffset As Integer) As Point
        Dim remaining As Integer = Math.Max(0, absOffset)
        For i As Integer = 0 To _lines.Count - 1
            If remaining <= _lines(i).Length Then
                Return New Point(remaining, i)
            End If
            remaining -= _lines(i).Length + 2
        Next
        Return New Point(_lines(_lines.Count - 1).Length, _lines.Count - 1)
    End Function
#End Region

#Region "事件"
    Protected Overrides Sub OnGotFocus(e As EventArgs)
        MyBase.OnGotFocus(e)
        If IsHandleCreated Then
            ImeHelper.AssociateDefault(Handle)
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
    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        UpdateDpiCache()
        RebuildVisualLines()
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

Public Class LinkClickedEventArgs
    Inherits EventArgs
    Public ReadOnly Property LinkText As String
    Public Sub New(linkText As String)
        Me.LinkText = linkText
    End Sub
End Class