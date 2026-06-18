Imports System.ComponentModel
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Text.RegularExpressions
Imports Vortice.Direct2D1

<DefaultEvent("TextChanged")>
Public Class ModernTextBox
#Region "D2D 资源（V2 占位）"
    ' V2：D2D 资源由 WindowCompositor 按顶层 Form 共享管理，本控件不再持有 _dcRT / _ssaaCache。
#End Region
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
    Private Structure MeasureKey
        Public LineIndex As Integer
        Public StartCol As Integer
        Public Length As Integer
        Public FontKey As Integer
        Public TextVersion As Integer
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
    Private _textLength As Integer = 0
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
    Private _scrollPixelOffset As Single = 0.0F
    Private _scrollTargetPixelOffset As Single = 0.0F
    Private ReadOnly _scrollAnimationHelper As New AnimationHelperV2(Me)
    Private _scrollAnimationRunning As Boolean = False
    Private _scrollAnimationLastTicks As Long = 0
    Private _scrollAnimationLastPaintOffset As Single = Single.NaN
    Private _scrollAnimationFrameNeedsInvalidate As Boolean = True
    Private _allowSmoothScroll As Boolean = False
    Private _scrollXOffset As Integer = 0
    Private _scrollBarVisible As Boolean = False
    Private _scrollBar As New ScrollBarRenderer()
    Private _mouseDownSelecting As Boolean = False
    Private _imeComposing As Boolean = False
    Private _visualLines As New List(Of VisualLineInfo)
    Private _visualLineStarts As New List(Of Integer) From {0}
    Private _autoScrollTimer As New Timer() With {.Interval = 50}
    Private _lastMousePos As Point = Point.Empty
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
    Private _lineNumberGutterWidthCache As Integer = -1
    Private _lineNumberGutterDigitCount As Integer = -1
    Private _measureVersion As Integer
    Private ReadOnly _lineWidthCache As New Dictionary(Of MeasureKey, Integer)(128)
    Private ReadOnly _textWidthCache As New Dictionary(Of String, Integer)(32)
    Private Const ScrollStopThreshold As Single = 0.25F
    Private Const MaxLineWidthCacheEntries As Integer = 4096
    Private Const MaxTextWidthCacheEntries As Integer = 512
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
            If _textLength = normalized.Length AndAlso String.Join(vbLf, _lines) = normalized Then Return
            PushUndo()
            Dim savedScrollY As Single = _scrollPixelOffset
            Dim savedScrollX As Integer = _scrollXOffset
            SetLinesFromString(normalized)
            _caretLine = 0
            _caretCol = 0
            If Not _preserveScrollPosition Then
                _scrollXOffset = 0
                _scrollLineOffset = 0
                _scrollPixelOffset = 0
                _scrollTargetPixelOffset = 0
            End If
            ClearSelection()
            NotifyTextChanged()
            If _preserveScrollPosition Then
                SetScrollPixelOffset(savedScrollY)
                _scrollXOffset = savedScrollX
                UpdateScrollBar()
                OuterToInnerRefreshScheduler.RequestFull(Me)
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
            RefreshVisualLayout(True)
            OuterToInnerRefreshScheduler.RequestFull(Me)
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
            OuterToInnerRefreshScheduler.RequestFull(Me)
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
                RefreshVisualLayout(True)
                OuterToInnerRefreshScheduler.RequestFull(Me)
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
            If 启用多行 <> value Then
                启用多行 = value
                RefreshVisualLayout(True)
                OuterToInnerRefreshScheduler.RequestFull(Me)
            End If
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
            OuterToInnerRefreshScheduler.RequestFull(Me)
        End Set
    End Property

    Private Shared ReadOnly 默认滚动条颜色 As Color = Color.FromArgb(140, 140, 140)
    Private 滚动条颜色 As Color = 默认滚动条颜色
    <Category("LakeUI"), Description("滚动条滑块颜色"), Browsable(True)>
    Public Property ScrollBarColor As Color
        Get
            Return 滚动条颜色
        End Get
        Set(value As Color)
            SetValue(滚动条颜色, value)
        End Set
    End Property

    Private Function ShouldSerializeScrollBarColor() As Boolean
        Return 滚动条颜色 <> 默认滚动条颜色
    End Function

    Private Sub ResetScrollBarColor()
        ScrollBarColor = 默认滚动条颜色
    End Sub

    Private Shared ReadOnly 默认滚动条悬停颜色 As Color = Color.FromArgb(200, 200, 200)
    Private 滚动条悬停颜色 As Color = 默认滚动条悬停颜色
    <Category("LakeUI"), Description("滚动条滑块悬停/拖拽颜色"), Browsable(True)>
    Public Property ScrollBarHoverColor As Color
        Get
            Return 滚动条悬停颜色
        End Get
        Set(value As Color)
            SetValue(滚动条悬停颜色, value)
        End Set
    End Property

    Private Function ShouldSerializeScrollBarHoverColor() As Boolean
        Return 滚动条悬停颜色 <> 默认滚动条悬停颜色
    End Function

    Private Sub ResetScrollBarHoverColor()
        ScrollBarHoverColor = 默认滚动条悬停颜色
    End Sub

    Private Shared ReadOnly 默认滚动条轨道颜色 As Color = Color.FromArgb(20, 255, 255, 255)
    Private 滚动条轨道颜色 As Color = 默认滚动条轨道颜色
    <Category("LakeUI"), Description("滚动条轨道颜色"), Browsable(True)>
    Public Property ScrollBarTrackColor As Color
        Get
            Return 滚动条轨道颜色
        End Get
        Set(value As Color)
            SetValue(滚动条轨道颜色, value)
        End Set
    End Property

    Private Function ShouldSerializeScrollBarTrackColor() As Boolean
        Return 滚动条轨道颜色 <> 默认滚动条轨道颜色
    End Function

    Private Sub ResetScrollBarTrackColor()
        ScrollBarTrackColor = 默认滚动条轨道颜色
    End Sub

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
                OuterToInnerRefreshScheduler.RequestFull(Me)
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
                If _syntaxHighlighter Is Nothing Then
                    ClearAllFormats()
                Else
                    ApplySyntaxHighlighting()
                    RefreshVisualLayout(True)
                End If
            End If
            OuterToInnerRefreshScheduler.RequestFull(Me)
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
                    RefreshVisualLayout(True)
                Else
                    ClearAllFormats()
                End If
                OuterToInnerRefreshScheduler.RequestFull(Me)
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
                RefreshVisualLayout(True)
                OuterToInnerRefreshScheduler.RequestFull(Me)
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
            InvalidateLineNumberGutterCache()
            If _showLineNumbers Then
                RefreshVisualLayout(True)
            End If
            D2DHelperV2.RefreshFontDependentRendering(Me)
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
                RefreshVisualLayout(True)
                OuterToInnerRefreshScheduler.RequestFull(Me)
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
                RefreshVisualLayout(True)
                OuterToInnerRefreshScheduler.RequestFull(Me)
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
    <Category("LakeUI"), Description(GlobalOptions.超采样抗锯齿描述词), DefaultValue(GetType(GlobalOptions.SuperSamplingScaleEnum), "OFF"), Browsable(True)>
    Public Property SuperSamplingScale As GlobalOptions.SuperSamplingScaleEnum
        Get
            Return 超采样倍率
        End Get
        Set(value As GlobalOptions.SuperSamplingScaleEnum)
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

    <Category("LakeUI"), Description(GlobalOptions.动画时长描述词), DefaultValue(300), Browsable(True)>
    Public Property AnimationDuration As Integer
        Get
            Return _scrollAnimationHelper.Duration
        End Get
        Set(value As Integer)
            _scrollAnimationHelper.Duration = Math.Max(0, value)
            If _scrollAnimationHelper.Duration <= 0 Then
                SetScrollPixelOffset(_scrollTargetPixelOffset)
            End If
        End Set
    End Property

    <Category("LakeUI"), Description(GlobalOptions.动画帧率描述词), DefaultValue(60), Browsable(True)>
    Public Property AnimationFPS As Integer
        Get
            Return _scrollAnimationHelper.FPS
        End Get
        Set(value As Integer)
            _scrollAnimationHelper.FPS = Math.Max(0, value)
            If _scrollAnimationRunning Then
                _scrollAnimationHelper.StopFrameLoop()
                _scrollAnimationLastTicks = Stopwatch.GetTimestamp()
                _scrollAnimationHelper.StartFrameLoop(AddressOf ScrollAnimationTick)
            End If
        End Set
    End Property

    <Category("LakeUI"), Description("是否允许滚动操作使用平滑滚动。默认关闭，开启后滚动动画帧率与 AnimationFPS 同步。"), DefaultValue(False), Browsable(True)>
    Public Property AllowSmoothScroll As Boolean
        Get
            Return _allowSmoothScroll
        End Get
        Set(value As Boolean)
            If _allowSmoothScroll = value Then Return
            _allowSmoothScroll = value
            If Not _allowSmoothScroll Then
                SetScrollPixelOffset(_scrollTargetPixelOffset)
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
                RefreshVisualLayout(True)
                OuterToInnerRefreshScheduler.RequestFull(Me)
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
            If _passwordChar = value Then Return
            _passwordChar = value
            InvalidateMeasureCache()
            RefreshVisualLayout(True)
            OuterToInnerRefreshScheduler.RequestFull(Me)
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
            OuterToInnerRefreshScheduler.RequestFull(Me)
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
            OuterToInnerRefreshScheduler.RequestFull(Me)
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

    Private _backgroundSource As Control = Nothing
    ''' <summary>
    ''' 背景采样源（超容器背景映射）。透明背景模式下，控件会调用此控件的绘制流程取像素作为底图，
    ''' 从而实现跨越任意层级的"穿透显示"效果。
    ''' 为 Nothing 时不进行背景采样。
    ''' </summary>
    <Category("LakeUI"),
     Description("背景采样源（超容器背景映射）。设置后将跨越任意层级直接采样此控件的绘制内容作为透明背景；为空时不进行背景采样。"),
     DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource IsNot value Then
                _backgroundSource = BackgroundPenetrationV2.SetConsumerSource(Me, _backgroundSource, value)
                OuterToInnerRefreshScheduler.RequestFull(Me)
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
        Dim oldGutterW As Integer = If(IsHandleCreated, LineNumberGutterWidth(), 0)

        If _maxLength > 0 Then
            Dim currentLen As Integer = _textLength
            Dim overhead As Integer = If(_lines.Count = 1 AndAlso _lines(0).Length = 0, 0, 1)
            Dim remaining As Integer = _maxLength - currentLen - overhead
            If remaining <= 0 Then Return
            If lineText.Length > remaining Then lineText = lineText.Substring(0, remaining)
        End If

        Dim isEmpty As Boolean = (_lines.Count = 1 AndAlso _lines(0).Length = 0)
        Dim newLineIndex As Integer

        If isEmpty Then
            _lines(0) = lineText
            _textLength = lineText.Length
            newLineIndex = 0
            _visualLines.Clear()
            _visualLineStarts.Clear()
            _visualLineStarts.Add(0)
            InvalidateLineMeasureCache(0)
        Else
            newLineIndex = _lines.Count
            _lines.Add(lineText)
            _textLength += 1 + lineText.Length
            _lineRuns.Add(Nothing)
            _lineLinks.Add(Nothing)
            _lineStates.Add(0)
            _visualLineStarts.Add(_visualLines.Count)
        End If

        Dim hasExplicitFormat As Boolean = foreColor <> Color.Empty OrElse lineFont IsNot Nothing
        If _enableSyntaxHighlight AndAlso _syntaxHighlighter IsNot Nothing Then
            ApplySyntaxHighlightingToLine(newLineIndex, Not hasExplicitFormat)
        End If

        ' 直接设置格式 Run，避免二次遍历
        If hasExplicitFormat Then
            If lineText.Length > 0 Then
                _lineRuns(newLineIndex) = New List(Of TextRun) From {
                    New TextRun(0, lineText.Length, foreColor, lineFont)
                }
            End If
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
        Dim gutterChanged As Boolean = IsHandleCreated AndAlso LineNumberGutterWidth() <> oldGutterW
        If _scrollBarVisible <> oldVisible OrElse gutterChanged Then
            RefreshVisualLayout()
        End If

        ' 未启用保留位置时自动滚到底部
        If Not _preserveScrollPosition Then
            SetScrollPixelOffset(MaxScrollPixelOffset())
        End If

        OuterToInnerRefreshScheduler.RequestFull(Me)
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
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Public Sub DeselectAll()
        ClearSelection()
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Public Sub ScrollToCaret()
        EnsureCaretVisible()
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Public Sub ScrollToBottom()
        If Not 启用多行 Then Return
        SetScrollPixelOffset(MaxScrollPixelOffset())
        UpdateScrollBar()
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Public Sub ScrollToTop()
        SetScrollPixelOffset(0)
        _scrollXOffset = 0
        UpdateScrollBar()
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Public Sub ScrollToLine(lineIndex As Integer)
        If Not 启用多行 Then Return
        lineIndex = Math.Max(0, Math.Min(_lines.Count - 1, lineIndex))
        Dim targetVi As Integer = If(lineIndex < _visualLineStarts.Count,
            _visualLineStarts(lineIndex),
            Math.Max(0, _visualLines.Count - 1))
        SetScrollPixelOffset(targetVi * _scaledLineHeight)
        UpdateScrollBar()
        OuterToInnerRefreshScheduler.RequestFull(Me)
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
        InvalidateMeasureCache()
        RefreshVisualLayout(True)
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Public Sub SetLineFormat(lineIndex As Integer, startCol As Integer, length As Integer,
                              Optional foreColor As Color = Nothing, Optional runFont As Font = Nothing)
        If lineIndex < 0 OrElse lineIndex >= _lines.Count Then Return
        If length <= 0 Then Return
        startCol = Math.Max(0, startCol)
        length = Math.Min(length, _lines(lineIndex).Length - startCol)
        If length <= 0 Then Return
        ApplyFormatToLine(lineIndex, startCol, length, foreColor, runFont)
        InvalidateMeasureCache()
        RefreshVisualLayout(True)
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Public Sub ClearFormat(startPos As Integer, length As Integer)
        SetFormat(startPos, length, Color.Empty, Nothing)
    End Sub
    Public Sub ClearAllFormats()
        For i = 0 To _lineRuns.Count - 1
            _lineRuns(i) = Nothing
        Next
        InvalidateMeasureCache()
        RefreshVisualLayout(True)
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
#End Region

#Region "初始化"
    Public Sub New()
        InitializeComponent()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.Selectable Or
                 ControlStyles.StandardClick Or
                 ControlStyles.SupportsTransparentBackColor, True)
        UpdateStyles()
        AddHandler _caretBlinkTimer.Tick, Sub()
                                              ' 守卫：失焦 / 不可见 / 未创建句柄 时立即停止，避免空闲状态下持续 Invalidate。
                                              If Not Me.Focused OrElse Not Me.Visible OrElse Not Me.IsHandleCreated Then
                                                  _caretBlinkTimer.Stop()
                                                  If _caretVisible Then
                                                      _caretVisible = False
                                                      OuterToInnerRefreshScheduler.RequestFull(Me)
                                                  End If
                                                  Return
                                              End If
                                              _caretVisible = Not _caretVisible
                                              OuterToInnerRefreshScheduler.RequestFull(Me)
                                          End Sub
        AddHandler _autoScrollTimer.Tick, AddressOf AutoScrollTick
        _scrollAnimationHelper.DirtyProvider = AddressOf 滚动动画脏区
        RebuildVisualLines()
    End Sub
    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        UpdateDpiCache()
        ImeHelper.AssociateDefault(Handle)
        RefreshVisualLayout(True)
        ' 仅在已聚焦时才启动光标闪烁；未聚焦时启动会让控件即使无操作也每 530ms 触发一次重绘。
        If Me.Focused Then
            _caretVisible = True
            _caretBlinkTimer.Start()
        End If
    End Sub
    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        _caretBlinkTimer.Stop()
        StopScrollAnimation()
        _autoScrollTimer.Stop()
        _underlineFontCache?.Dispose()
        _underlineFontCache = Nothing
        MyBase.OnHandleDestroyed(e)
    End Sub
#End Region

#Region "绘制"
    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' V2 契约（与 ModernButton 一致）：
        '   • BackgroundSource 已设置 → 跳过基类填底，背景由 OnPaint 内 BackgroundPenetrationV2 绘制；
        '   • 否则一律走 .NET 自身透明逻辑（半透明 BackColor 由基类合成父级背景到 HDC，
        '     不透明色由基类填底）。BindDC 之后 DC RT 初始像素即为正确底图，
        '     不再依赖手工 Clear，因此不会出现"HDC 残留导致乱照父窗体其它区域"的问题。
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim w As Integer = ClientRectangle.Width
        Dim h As Integer = ClientRectangle.Height
        If w <= 0 OrElse h <= 0 Then Return
        Dim hasRadius As Boolean = 边框圆角半径 > 0
        Dim fillRect As New RectangleF(0, 0, w, h)
        Dim boundsRect As New RectangleF(0, 0, w, h)
        Dim s As Single = DpiScale()
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * s / 2.0F
            boundsRect.Inflate(-half, -half)
        End If
        Dim bc As Color = If(Focused, 有焦点时边框颜色, 边框颜色)
        Dim ssaa As Integer = 计算当前绘制超采样倍率()

        Using scope = D2DHelperV2.BeginPaint(e, Me, ssaa)
            If scope Is Nothing Then Return
            ' 背景层：
            '   • BackgroundSource 已设置 → 显式穿透到指定源；
            '   • 否则 OnPaintBackground 中 MyBase 已把基类 BackColor / 父级透明背景合成进 HDC，
            '     DC RT 初始像素即正确底图，这里不再手工 Clear。
            '     旧版的"ClearType 累积"是把 Parent.BackColor 当不透明底强 Clear 的副作用，
            '     现在每帧都由基类重画底图，不会累积。
            If _backgroundSource IsNot Nothing Then
                BackgroundPenetrationV2.PaintBackground(Me, scope, _backgroundSource)
            End If

            Dim gRT As ID2D1RenderTarget = scope.GraphicsLayer
            Dim brushCache = scope.Compositor.BrushCache
            绘制背景_D2D(gRT, hasRadius, fillRect, brushCache)
            scope.FlushGraphics()

            Dim dcRT As ID2D1DCRenderTarget = scope.DCRenderTarget
            DrawTextContent_D2D(dcRT, w, h, scope.Compositor)
            绘制边框_D2D(dcRT, hasRadius, boundsRect, bc, brushCache)
            DrawScrollBar_D2D(dcRT, brushCache, w, h)
        End Using
    End Sub

    Private Function 计算当前绘制超采样倍率() As Integer
        Return D2DHelperV2.GetEffectiveSsaaScale(超采样倍率)
    End Function

    Private Sub 绘制背景_D2D(rt As ID2D1RenderTarget, hasRadius As Boolean, fillRect As RectangleF, brushCache As D2DGlobals.SolidColorBrushCache)
        ' BackColor 半透明遮罩层：位于采样底图之上、BackColor1（=背景颜色）之下。A=255 不走本路径。
        Dim backColorMask As Color = MyBase.BackColor
        Dim s As Single = DpiScale()
        If hasRadius Then
            Using geo = RectangleRenderer.创建圆角矩形几何(fillRect, 边框圆角半径 * s)
                If backColorMask.A > 0 AndAlso backColorMask.A < 255 Then
                    RectangleRenderer.绘制圆角背景_D2D(rt, geo, fillRect, backColorMask, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
                End If
                If 背景颜色.A > 0 Then
                    RectangleRenderer.绘制圆角背景_D2D(rt, geo, fillRect, 背景颜色, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
                End If
            End Using
        Else
            If backColorMask.A > 0 AndAlso backColorMask.A < 255 Then
                RectangleRenderer.绘制矩形背景_D2D(rt, fillRect, backColorMask, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
            End If
            If 背景颜色.A > 0 Then
                RectangleRenderer.绘制矩形背景_D2D(rt, fillRect, 背景颜色, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
            End If
        End If
    End Sub

    Private Sub 绘制边框_D2D(rt As ID2D1RenderTarget, hasRadius As Boolean, boundsRect As RectangleF, borderClr As Color, brushCache As D2DGlobals.SolidColorBrushCache)
        If 边框宽度 <= 0 OrElse borderClr.A = 0 Then Return
        Dim s As Single = DpiScale()
        If hasRadius Then
            RectangleRenderer.绘制圆角边框_D2D(rt, boundsRect, 边框圆角半径 * s, borderClr, 边框宽度 * s, brushCache)
        Else
            RectangleRenderer.绘制矩形边框_D2D(rt, boundsRect, borderClr, 边框宽度 * s, brushCache)
        End If
    End Sub

    Private Sub DrawScrollBar_D2D(rt As ID2D1RenderTarget,
                                  brushCache As D2DGlobals.SolidColorBrushCache,
                                  w As Integer, h As Integer)
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
        _scrollBar.Draw_D2D(rt, w, h, scaledBorder, scaledRadius, scaledScrollW,
            滚动条轨道颜色, 滚动条颜色, 滚动条悬停颜色, brushCache)
    End Sub

    Private Sub DrawTextContent_D2D(rt As ID2D1DCRenderTarget, w As Integer, h As Integer, compositor As WindowCompositor)
        Dim textFormatCache = compositor?.TextFormatCache
        Dim brushCache = compositor?.BrushCache
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
        Dim textWidth As Integer = Math.Max(0, w - textLeft - textRight - scrollW)
        Dim textHeight As Integer = Math.Max(0, h - textTop - textBottom)
        Dim isSingleLine As Boolean = Not 启用多行
        Dim singleLineY As Single = textTop + (textHeight - _scaledLineHeight) / 2.0F
        Dim scrollY As Single = If(isSingleLine, 0.0F, _scrollPixelOffset)
        Dim startVi As Integer = If(isSingleLine OrElse _scaledLineHeight <= 0, 0, CInt(Math.Floor(scrollY / _scaledLineHeight)))
        Dim scrollRemainder As Single = If(isSingleLine, 0.0F, scrollY - startVi * _scaledLineHeight)
        Dim visibleLines As Integer = If(isSingleLine, 1, CInt(Math.Ceiling((textHeight + scrollRemainder) / _scaledLineHeight)) + 1)
        Dim endVi As Integer = Math.Min(_visualLines.Count - 1, startVi + visibleLines - 1)

        ' 绘制行号区域（紧贴上下边框内侧）
        If gutterW > 0 AndAlso textHeight > 0 Then
            ' 绘制行号背景，兼容圆角边框
            If _lineNumBackColor <> Color.Empty Then
                Dim s As Single = DpiScale()
                Dim gutterRect As New RectangleF(0, 0, bi + gutterW, h)
                If 边框圆角半径 > 0 Then
                    Dim boundsRect As New RectangleF(0, 0, w, h)
                    If 边框宽度 > 0 Then
                        Dim half As Single = 边框宽度 * s / 2.0F
                        boundsRect.Inflate(-half, -half)
                    End If
                    Using geo = RectangleRenderer.创建圆角矩形几何(boundsRect, 边框圆角半径 * s)
                        D2DGlobals.PushGeometryClip(rt, geo, boundsRect)
                        FillRectangle_D2D(rt, gutterRect, _lineNumBackColor, brushCache)
                        rt.PopLayer()
                    End Using
                Else
                    FillRectangle_D2D(rt, gutterRect, _lineNumBackColor, brushCache)
                End If
            End If
            PushClip(rt, New RectangleF(gutterLeft, textTop, gutterW, textHeight))
            Dim useNumFont As Font = If(_lineNumFont, Font)
            Dim contentW As Integer = gutterW - _scaledLineNumPadL - _scaledLineNumPadR
            Dim lastDrawnLogical As Integer = -1
            For vi As Integer = startVi To endVi
                Dim vl = _visualLines(vi)
                Dim lineY As Single = textTop + (vi - startVi) * _scaledLineHeight - scrollRemainder
                If vl.LogicalLine <> lastDrawnLogical Then
                    lastDrawnLogical = vl.LogicalLine
                    Dim numStr As String = (vl.LogicalLine + 1).ToString()
                    Dim numW As Integer = CInt(Math.Ceiling(MeasureTextWidth_D2D(numStr, useNumFont, textFormatCache)))
                    Dim numX As Integer
                    Select Case _lineNumAlign
                        Case TextAlignMode.Left
                            numX = gutterLeft + _scaledLineNumPadL
                        Case TextAlignMode.Center
                            numX = gutterLeft + _scaledLineNumPadL + (contentW - numW) \ 2
                        Case Else ' Right
                            numX = gutterLeft + _scaledLineNumPadL + contentW - numW
                    End Select
                    DrawTextSegment_D2D(rt, numStr, useNumFont, _lineNumForeColor, numX, lineY, numW, _scaledLineHeight, False, textFormatCache, brushCache)
                End If
            Next
            rt.PopAxisAlignedClip()
        End If

        ' 绘制文本内容
        If textWidth <= 0 OrElse textHeight <= 0 Then Return
        PushClip(rt, New RectangleF(textLeft, textTop, textWidth, textHeight))
        Dim isEmpty As Boolean = (_lines.Count = 1 AndAlso _lines(0).Length = 0)
        If isEmpty AndAlso Not String.IsNullOrEmpty(水印文本) Then
            DrawWaterText_D2D(rt, textLeft, textTop, textWidth, textHeight, singleLineY, textFormatCache, brushCache)
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
                DrawVisualLineSelection_D2D(rt, vl, lineY, textLeft, alignOff, scrollX, minL, minC, maxL, maxC, brushCache)
            End If
            If vl.Length > 0 Then
                DrawLineRuns_D2D(rt, vl.LogicalLine, vl.StartCol, vl.Length,
                    textLeft + alignOff - scrollX, lineY, textFormatCache, brushCache)
            End If
        Next
        If Focused AndAlso _caretVisible Then
            DrawCaret_D2D(rt, textLeft, textTop, brushCache)
        End If
        rt.PopAxisAlignedClip()
    End Sub

    Private Sub DrawWaterText_D2D(rt As ID2D1RenderTarget,
                                  textLeft As Integer,
                                  textTop As Integer,
                                  textWidth As Integer,
                                  textHeight As Integer,
                                  singleLineY As Single,
                                  textFormatCache As D2DGlobals.TextFormatCache,
                                  brushCache As D2DGlobals.SolidColorBrushCache)
        If rt Is Nothing OrElse String.IsNullOrEmpty(水印文本) OrElse 水印颜色.A = 0 Then Return
        If textWidth <= 0 OrElse textHeight <= 0 Then Return

        If Not IsWordWrapActive() Then
            Dim waterLineY As Single = If(启用多行, CSng(textTop), singleLineY)
            Dim waterAlignOff As Integer = If(启用多行 OrElse 文本对齐 = TextAlignMode.Left, 0,
                ComputeAlignOffset(MeasureWidth(水印文本), textWidth))
            DrawTextSegment_D2D(rt, 水印文本, Font, 水印颜色, textLeft + waterAlignOff, waterLineY,
                textWidth, _scaledLineHeight, False, textFormatCache, brushCache)
            Return
        End If

        Dim waterLines = BuildWaterTextVisualLines(水印文本, textWidth)
        Dim visibleLines As Integer = Math.Min(waterLines.Count, Math.Max(1, CInt(Math.Ceiling(textHeight / CDbl(_scaledLineHeight)))))
        For i As Integer = 0 To visibleLines - 1
            Dim lineText As String = waterLines(i)
            If lineText.Length = 0 Then Continue For
            DrawTextSegment_D2D(rt, lineText, Font, 水印颜色, textLeft,
                textTop + i * _scaledLineHeight, textWidth, _scaledLineHeight,
                False, textFormatCache, brushCache)
        Next
    End Sub

    Private Sub PushClip(rt As ID2D1RenderTarget, rect As RectangleF)
        rt.PushAxisAlignedClip(New Vortice.RawRectF(rect.Left, rect.Top, rect.Right, rect.Bottom), AntialiasMode.Aliased)
    End Sub

    Private Sub DrawVisualLineSelection_D2D(rt As ID2D1RenderTarget, vl As VisualLineInfo, lineY As Single, textLeft As Integer,
                                         alignOff As Integer, scrollX As Integer,
                                         minL As Integer, minC As Integer, maxL As Integer, maxC As Integer,
                                         brushCache As D2DGlobals.SolidColorBrushCache)
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
        FillRectangle_D2D(rt, New RectangleF(x1, lineY, x2 - x1, _scaledLineHeight), 选区背景色, brushCache)
    End Sub

    Private Sub DrawCaret_D2D(rt As ID2D1RenderTarget, textLeft As Integer, textTop As Integer,
                              brushCache As D2DGlobals.SolidColorBrushCache)
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
            Dim textHeight As Integer = ClientRectangle.Height - Math.Max(Padding.Top, bi2) - Math.Max(Padding.Bottom, bi2)
            lineY = textTop + (textHeight - _scaledLineHeight) / 2.0F
        Else
            lineY = textTop + vi * _scaledLineHeight - _scrollPixelOffset
        End If
        Dim caretH As Integer = _scaledLineHeight - 2
        Dim caretY As Single = lineY + (_scaledLineHeight - caretH) / 2.0F
        FillRectangle_D2D(rt, New RectangleF(cx, caretY, _scaledCaretWidth, caretH), 光标颜色, brushCache)
    End Sub

    Private Sub DrawLineRuns_D2D(rt As ID2D1RenderTarget, lineIndex As Integer, vlStartCol As Integer,
                                  vlLength As Integer, x As Single, lineY As Single,
                                  textFormatCache As D2DGlobals.TextFormatCache,
                                  brushCache As D2DGlobals.SolidColorBrushCache)
        Dim runs = _lineRuns(lineIndex)
        Dim lineStr = _lines(lineIndex)
        Dim vlEnd = vlStartCol + vlLength
        Dim links = If(lineIndex < _lineLinks.Count, _lineLinks(lineIndex), Nothing)
        Dim hasLinks As Boolean = links IsNot Nothing AndAlso links.Count > 0 AndAlso (_passwordChar = vbNullChar OrElse 启用多行)
        If runs Is Nothing OrElse runs.Count = 0 Then
            If Not hasLinks Then
                Dim text = GetDisplayText(lineStr.Substring(vlStartCol, vlLength))
                DrawTextSegment_D2D(rt, text, Font, ForeColor, x, lineY, Short.MaxValue, _scaledLineHeight, False, textFormatCache, brushCache)
            Else
                DrawSegmentsWithLinks_D2D(rt, lineStr, vlStartCol, vlEnd, x, lineY, ForeColor, Font, links, textFormatCache, brushCache)
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
                drawX += DrawTextSegment_D2D(rt, segText, useFont, useFore, drawX, lineY, Short.MaxValue, _scaledLineHeight, False, textFormatCache, brushCache)
            Else
                drawX = DrawSegmentsWithLinks_D2D(rt, lineStr, segStart, segEnd, drawX, lineY, useFore, useFont, links, textFormatCache, brushCache)
            End If
        Next
    End Sub

    Private Function DrawSegmentsWithLinks_D2D(rt As ID2D1RenderTarget, lineStr As String, startCol As Integer, endCol As Integer,
                                                x As Single, lineY As Single, baseFore As Color, baseFont As Font,
                                                links As List(Of LinkRange),
                                                textFormatCache As D2DGlobals.TextFormatCache,
                                                brushCache As D2DGlobals.SolidColorBrushCache) As Single
        Dim pos = startCol
        Dim drawX = x
        For Each link In links
            Dim linkEnd = link.StartCol + link.Length
            If link.StartCol >= endCol OrElse linkEnd <= pos Then Continue For
            If pos < link.StartCol AndAlso pos < endCol Then
                Dim nonLinkEnd = Math.Min(link.StartCol, endCol)
                Dim segText = GetDisplayText(lineStr.Substring(pos, nonLinkEnd - pos))
                drawX += DrawTextSegment_D2D(rt, segText, baseFont, baseFore, drawX, lineY, Short.MaxValue, _scaledLineHeight, False, textFormatCache, brushCache)
                pos = nonLinkEnd
            End If
            Dim overlapStart = Math.Max(pos, link.StartCol)
            Dim overlapEnd = Math.Min(endCol, linkEnd)
            If overlapStart < overlapEnd Then
                Dim linkText = GetDisplayText(lineStr.Substring(overlapStart, overlapEnd - overlapStart))
                drawX += DrawTextSegment_D2D(rt, linkText, baseFont, 链接颜色, drawX, lineY, Short.MaxValue, _scaledLineHeight, 链接下划线, textFormatCache, brushCache)
                pos = overlapEnd
            End If
        Next
        If pos < endCol Then
            Dim segText = GetDisplayText(lineStr.Substring(pos, endCol - pos))
            drawX += DrawTextSegment_D2D(rt, segText, baseFont, baseFore, drawX, lineY, Short.MaxValue, _scaledLineHeight, False, textFormatCache, brushCache)
        End If
        Return drawX
    End Function

    Private Function DrawTextSegment_D2D(rt As ID2D1RenderTarget, text As String, font As Font, foreColor As Color,
                                         x As Single, y As Single, width As Single, height As Single, underline As Boolean,
                                         textFormatCache As D2DGlobals.TextFormatCache,
                                         brushCache As D2DGlobals.SolidColorBrushCache) As Single
        If rt Is Nothing OrElse String.IsNullOrEmpty(text) OrElse foreColor.A = 0 Then Return 0.0F
        Dim ownsFormat As Boolean = False
        Dim fmt = AcquireTextFormat_D2D(font, textFormatCache, ownsFormat)
        If fmt Is Nothing Then Return 0.0F
        Try
            Dim layoutWidth As Single = Math.Max(1.0F, width)
            Dim layoutHeight As Single = Math.Max(1.0F, height)
            Using layout = D2DGlobals.GetDWriteFactory().CreateTextLayout(text, fmt, layoutWidth, layoutHeight)
                Dim measuredWidth As Single = CSng(Math.Ceiling(layout.Metrics.WidthIncludingTrailingWhitespace))
                If underline Then
                    layout.SetUnderline(True, New Vortice.DirectWrite.TextRange(0, CUInt(text.Length)))
                End If
                Dim brush As ID2D1Brush = Nothing
                Dim ownsBrush As Boolean = False
                Try
                    brush = GetSolidBrush_D2D(rt, foreColor, brushCache, ownsBrush)
                    If brush IsNot Nothing Then rt.DrawTextLayout(New System.Numerics.Vector2(x, y), layout, brush)
                Finally
                    If ownsBrush AndAlso brush IsNot Nothing Then
                        Try : brush.Dispose() : Catch : End Try
                    End If
                End Try
                Return measuredWidth
            End Using
        Finally
            If ownsFormat AndAlso fmt IsNot Nothing Then
                Try : fmt.Dispose() : Catch : End Try
            End If
        End Try
    End Function

    Private Function AcquireTextFormat_D2D(font As Font, textFormatCache As D2DGlobals.TextFormatCache,
                                           ByRef ownsFormat As Boolean) As Vortice.DirectWrite.IDWriteTextFormat
        Dim useFont As Font = If(font, Me.Font)
        If useFont Is Nothing Then Return Nothing
        Dim s As Single = DpiScale()
        Dim sizePx As Single = useFont.SizeInPoints * (96.0F / 72.0F) * s
        ownsFormat = (textFormatCache Is Nothing)
        If textFormatCache IsNot Nothing Then
            Return textFormatCache.[Get](
                useFont,
                sizePx,
                Vortice.DirectWrite.TextAlignment.Leading,
                Vortice.DirectWrite.ParagraphAlignment.Center,
                False,
                False)
        End If

        Dim fmt = TextRenderHelper.CreateDWriteTextFormat(useFont, s)
        fmt.WordWrapping = Vortice.DirectWrite.WordWrapping.NoWrap
        fmt.ParagraphAlignment = Vortice.DirectWrite.ParagraphAlignment.Center
        Return fmt
    End Function

    Private Function MeasureTextWidth_D2D(text As String, font As Font,
                                          textFormatCache As D2DGlobals.TextFormatCache) As Single
        If String.IsNullOrEmpty(text) Then Return 0.0F
        Dim ownsFormat As Boolean = False
        Dim fmt = AcquireTextFormat_D2D(font, textFormatCache, ownsFormat)
        If fmt Is Nothing Then Return 0.0F
        Try
            Using layout = D2DGlobals.GetDWriteFactory().CreateTextLayout(text, fmt, Single.MaxValue, Single.MaxValue)
                Return layout.Metrics.WidthIncludingTrailingWhitespace
            End Using
        Finally
            If ownsFormat AndAlso fmt IsNot Nothing Then
                Try : fmt.Dispose() : Catch : End Try
            End If
        End Try
    End Function

    Private Function GetSolidBrush_D2D(rt As ID2D1RenderTarget, color As Color,
                                       brushCache As D2DGlobals.SolidColorBrushCache,
                                       ByRef ownsBrush As Boolean) As ID2D1Brush
        If rt Is Nothing OrElse color.A = 0 Then Return Nothing
        ownsBrush = (brushCache Is Nothing)
        If brushCache IsNot Nothing Then Return brushCache.[Get](rt, color)
        Return rt.CreateSolidColorBrush(D2DGlobals.ToColor4(color))
    End Function

    Private Sub FillRectangle_D2D(rt As ID2D1RenderTarget, rect As RectangleF, color As Color,
                                  brushCache As D2DGlobals.SolidColorBrushCache)
        If rect.Width <= 0 OrElse rect.Height <= 0 OrElse color.A = 0 Then Return
        Dim brush As ID2D1Brush = Nothing
        Dim ownsBrush As Boolean = False
        Try
            brush = GetSolidBrush_D2D(rt, color, brushCache, ownsBrush)
            If brush IsNot Nothing Then rt.FillRectangle(D2DGlobals.ToD2DRect(rect), brush)
        Finally
            If ownsBrush AndAlso brush IsNot Nothing Then
                Try : brush.Dispose() : Catch : End Try
            End If
        End Try
    End Sub

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
                StopScrollAnimation()
                _scrollTargetPixelOffset = _scrollPixelOffset
                If _scrollBar.BeginDrag(e.Location, CInt(Math.Round(_scrollPixelOffset))) Then Return
                Dim newOff As Integer = _scrollBar.TrackClick(e.Location, CInt(Math.Round(_scrollPixelOffset)),
                    TotalContentPixelHeight(), TextViewportHeight())
                If newOff <> CInt(Math.Round(_scrollPixelOffset)) Then
                    SetScrollPixelOffset(newOff)
                    UpdateScrollBar()
                    OuterToInnerRefreshScheduler.RequestFull(Me)
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
            OuterToInnerRefreshScheduler.RequestFull(Me)
        End If
    End Sub
    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If _scrollBar.IsDragging Then
            SetScrollPixelOffset(_scrollBar.DragMove(e.Y, TotalContentPixelHeight(), TextViewportHeight()))
            OuterToInnerRefreshScheduler.RequestFull(Me)
            Return
        End If
        If _scrollBarVisible Then
            If _scrollBar.UpdateHover(e.Location) Then OuterToInnerRefreshScheduler.RequestFull(Me)
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
            OuterToInnerRefreshScheduler.RequestFull(Me)
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
        Dim maxOffset As Integer = MaxScrollPixelOffset()
        If maxOffset <= 0 Then Return
        Dim wheelLines As Integer = Math.Max(1, SystemInformation.MouseWheelScrollLines)
        Dim scrollAmount As Single = Math.Max(1.0F, wheelLines * _scaledLineHeight / 3.0F)
        Dim deltaPixels As Single = -CSng(e.Delta) / 120.0F * scrollAmount
        SetScrollPixelOffset(_scrollTargetPixelOffset + deltaPixels, _allowSmoothScroll)
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
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Private Function HitTest(x As Integer, y As Integer) As Point
        Dim bi As Integer = ScaledBorderWidth()
        Dim gutterW As Integer = LineNumberGutterWidth()
        Dim textLeft As Integer = If(gutterW > 0, bi + gutterW + Padding.Left, Math.Max(Padding.Left, bi))
        Dim vi As Integer
        If 启用多行 Then
            Dim textTop As Integer = Math.Max(Padding.Top, bi)
            vi = CInt(Math.Floor((y - textTop + _scrollPixelOffset) / _scaledLineHeight))
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
        Return TextRenderHelper.FindColFromX_D2D(GetDisplayText(lineStr), x, Font, DpiScale(), GetTextFormatCacheForMeasure())
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
            OuterToInnerRefreshScheduler.RequestFull(Me)
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
        OuterToInnerRefreshScheduler.RequestFull(Me)
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
        OuterToInnerRefreshScheduler.RequestFull(Me)
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
        OuterToInnerRefreshScheduler.RequestFull(Me)
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
        OuterToInnerRefreshScheduler.RequestFull(Me)
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
        OuterToInnerRefreshScheduler.RequestFull(Me)
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
            Dim viewH As Integer = TextViewportHeight()
            Dim caretTop As Single = vi * _scaledLineHeight
            Dim caretBottom As Single = caretTop + _scaledLineHeight
            If caretTop < _scrollPixelOffset Then
                SetScrollPixelOffset(caretTop)
            ElseIf caretBottom > _scrollPixelOffset + viewH Then
                SetScrollPixelOffset(caretBottom - viewH)
            End If
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
            Dim currentLen As Integer = _textLength
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
        _textLength += normalized.Length
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
        _textLength += 1
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
            _textLength = Math.Max(0, _textLength - 1)
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
            _textLength = Math.Max(0, _textLength - 1)
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
            _textLength = Math.Max(0, _textLength - 1)
            NotifyTextChanged(_caretLine, _caretLine)
        ElseIf _caretLine < _lines.Count - 1 Then
            PushUndo()
            MergeLineRuns(_caretLine, _caretLine + 1, _lines(_caretLine).Length)
            _lines(_caretLine) = _lines(_caretLine) & _lines(_caretLine + 1)
            _lines.RemoveAt(_caretLine + 1)
            _lineRuns.RemoveAt(_caretLine + 1)
            _lineLinks.RemoveAt(_caretLine + 1)
            _lineStates.RemoveAt(_caretLine + 1)
            _textLength = Math.Max(0, _textLength - 1)
            NotifyTextChanged(_caretLine, _caretLine)
        End If
    End Sub
    Private Sub DeleteSelection()
        If Not _hasSelection Then Return
        Dim minL, minC, maxL, maxC As Integer
        GetOrderedSelection(minL, minC, maxL, maxC)
        _textLength = Math.Max(0, _textLength - GetSelectionNormalizedLength(minL, minC, maxL, maxC))

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
        OuterToInnerRefreshScheduler.RequestFull(Me)
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
    Private Function GetSelectionNormalizedLength(minL As Integer, minC As Integer,
                                                  maxL As Integer, maxC As Integer) As Integer
        If minL = maxL Then Return Math.Max(0, maxC - minC)
        Dim total As Integer = Math.Max(0, _lines(minL).Length - minC) + Math.Max(0, maxC)
        total += Math.Max(0, maxL - minL)
        For i As Integer = minL + 1 To maxL - 1
            total += _lines(i).Length
        Next
        Return total
    End Function
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
        _textLength = ComputeNormalizedTextLength()
        _lineRuns = If(snap.LineRuns, New List(Of List(Of TextRun)))
        While _lineRuns.Count < _lines.Count
            _lineRuns.Add(Nothing)
        End While
        InvalidateMeasureCache()
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
        _scrollBarVisible = TotalContentPixelHeight() > TextViewportHeight()
    End Sub
    Private Sub RefreshVisualLayout(Optional keepCaretVisible As Boolean = False)
        For pass As Integer = 0 To 2
            Dim oldVisible As Boolean = _scrollBarVisible
            RebuildVisualLines()
            UpdateScrollBar()
            If _scrollBarVisible = oldVisible Then Exit For
        Next

        If 启用多行 Then
            ClampScrollPixelOffsets()
        Else
            StopScrollAnimation()
            _scrollLineOffset = 0
            _scrollPixelOffset = 0
            _scrollTargetPixelOffset = 0
        End If

        If IsWordWrapActive() Then _scrollXOffset = 0
        If keepCaretVisible Then EnsureCaretVisible()
    End Sub
    Private Function TotalContentPixelHeight() As Integer
        Return Math.Max(0, _visualLines.Count * _scaledLineHeight)
    End Function
    Private Function MaxScrollPixelOffset() As Integer
        If Not 启用多行 Then Return 0
        Return Math.Max(0, TotalContentPixelHeight() - TextViewportHeight())
    End Function
    Private Function ClampScrollOffset(value As Single) As Single
        Dim maxOffset As Single = MaxScrollPixelOffset()
        Return Math.Max(0.0F, Math.Min(maxOffset, value))
    End Function
    Private Function ClampScrollTargetOffset(value As Single) As Single
        Dim maxOffset As Single = MaxScrollPixelOffset()
        Dim overshoot As Single = _scaledLineHeight * 0.6F
        Return Math.Max(-overshoot, Math.Min(maxOffset + overshoot, value))
    End Function
    Private Function ScrollSmoothCoefficient() As Single
        If _scrollAnimationHelper.Duration <= 0 Then Return Single.MaxValue
        Return Math.Max(1.0F, 4200.0F / _scrollAnimationHelper.Duration)
    End Function
    Private Function ScrollReboundCoefficient() As Single
        Dim smooth As Single = ScrollSmoothCoefficient()
        Return Math.Max(smooth + 4.0F, smooth * 1.3F)
    End Function
    Private Sub SetScrollPixelOffset(value As Single, Optional animate As Boolean = False)
        animate = animate AndAlso _allowSmoothScroll
        If animate Then
            _scrollTargetPixelOffset = ClampScrollTargetOffset(value)
            If Not IsHandleCreated OrElse _scrollAnimationHelper.Duration <= 0 Then
                StopScrollAnimation()
                _scrollPixelOffset = ClampScrollOffset(_scrollTargetPixelOffset)
                _scrollTargetPixelOffset = _scrollPixelOffset
            Else
                If Not _scrollAnimationRunning Then
                    _scrollAnimationRunning = True
                    _scrollAnimationLastTicks = Stopwatch.GetTimestamp()
                    _scrollAnimationLastPaintOffset = _scrollPixelOffset
                    _scrollAnimationHelper.StartFrameLoop(AddressOf ScrollAnimationTick)
                End If
            End If
        Else
            StopScrollAnimation()
            _scrollPixelOffset = ClampScrollOffset(value)
            _scrollTargetPixelOffset = _scrollPixelOffset
        End If

        SyncScrollLineOffset()
        UpdateScrollBar()
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Private Sub ClampScrollPixelOffsets()
        Dim maxOffset As Single = MaxScrollPixelOffset()
        _scrollPixelOffset = Math.Max(0.0F, Math.Min(maxOffset, _scrollPixelOffset))
        _scrollTargetPixelOffset = ClampScrollTargetOffset(_scrollTargetPixelOffset)
        SyncScrollLineOffset()
    End Sub
    Private Sub StopScrollAnimation()
        If _scrollAnimationRunning Then
            _scrollAnimationRunning = False
            _scrollAnimationHelper.StopFrameLoop()
        End If
        _scrollAnimationLastTicks = 0
        _scrollAnimationLastPaintOffset = Single.NaN
    End Sub
    Private Sub SyncScrollLineOffset()
        If _scaledLineHeight <= 0 Then
            _scrollLineOffset = 0
        Else
            _scrollLineOffset = Math.Max(0, Math.Min(_visualLines.Count - 1, CInt(Math.Floor(_scrollPixelOffset / _scaledLineHeight))))
        End If
    End Sub
    Private Sub ScrollAnimationTick(sender As Object, e As EventArgs)
        Dim nowTicks As Long = Stopwatch.GetTimestamp()
        Dim dt As Single
        If _scrollAnimationLastTicks = 0 Then
            dt = 1.0F / Math.Max(1, If(_scrollAnimationHelper.FPS > 0, _scrollAnimationHelper.FPS, 60))
        Else
            dt = CSng((nowTicks - _scrollAnimationLastTicks) / Stopwatch.Frequency)
        End If
        If dt < 0.001F Then dt = 0.001F
        If dt > 0.05F Then dt = 0.05F
        _scrollAnimationLastTicks = nowTicks

        Dim maxOffset As Single = MaxScrollPixelOffset()
        Dim coef As Single = ScrollSmoothCoefficient()
        If _scrollTargetPixelOffset < 0 Then
            _scrollTargetPixelOffset = 0
            coef = ScrollReboundCoefficient()
        ElseIf _scrollTargetPixelOffset > maxOffset Then
            _scrollTargetPixelOffset = maxOffset
            coef = ScrollReboundCoefficient()
        End If

        Dim diff As Single = _scrollTargetPixelOffset - _scrollPixelOffset
        Dim alpha As Single = 1.0F - CSng(Math.Exp(-coef * dt))
        _scrollPixelOffset += diff * alpha

        Dim stopAfterThisTick As Boolean = Math.Abs(diff) < ScrollStopThreshold
        If stopAfterThisTick Then
            _scrollPixelOffset = _scrollTargetPixelOffset
        End If
        ClampScrollPixelOffsets()
        SyncScrollLineOffset()
        _scrollAnimationFrameNeedsInvalidate = 应重绘滚动动画帧(stopAfterThisTick)
        If stopAfterThisTick Then StopScrollAnimation()
    End Sub
    Private Sub 滚动动画脏区(helper As AnimationHelperV2, owner As Control, sink As AnimationHelperV2.InvalidateRegionSink)
        If _scrollAnimationFrameNeedsInvalidate Then
            Dim dirty = 滚动动画失效区域()
            If dirty.Width > 0 AndAlso dirty.Height > 0 Then
                sink.Add(dirty)
            Else
                sink.InvalidateAll()
            End If
        Else
            sink.SuppressInvalidate()
        End If
    End Sub

    Private Function 滚动动画失效区域() As Rectangle
        Dim bi As Integer = ScaledBorderWidth()
        Dim top As Integer = Math.Max(Padding.Top, bi)
        Dim bottom As Integer = ClientRectangle.Height - Math.Max(Padding.Bottom, bi)
        If bottom <= top Then Return ClientRectangle

        Dim gutterW As Integer = LineNumberGutterWidth()
        Dim left As Integer = If(gutterW > 0, bi, Math.Max(Padding.Left, bi))
        Dim right As Integer = ClientRectangle.Width - Math.Max(Padding.Right, bi)
        If _scrollBarVisible Then
            right = ClientRectangle.Width
        End If
        Dim dirty As New Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top))
        dirty.Inflate(2, 2)
        Return Rectangle.Intersect(ClientRectangle, dirty)
    End Function
    Private Function 应重绘滚动动画帧(force As Boolean) As Boolean
        If force OrElse Single.IsNaN(_scrollAnimationLastPaintOffset) OrElse
           Math.Abs(_scrollPixelOffset - _scrollAnimationLastPaintOffset) >= ScrollStopThreshold Then
            _scrollAnimationLastPaintOffset = _scrollPixelOffset
            Return True
        End If
        Return False
    End Function
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
            cy = CInt(Math.Round(imeTop + vi * _scaledLineHeight - _scrollPixelOffset + _scaledLineHeight))
        Else
            Dim textHeight As Integer = ClientRectangle.Height - imeTop - Math.Max(Padding.Bottom, bi)
            cy = imeTop + (textHeight - _scaledLineHeight) \ 2 + _scaledLineHeight
        End If
        ImeHelper.SetCompositionPosition(Handle, cx, cy)
    End Sub
#End Region

#Region "度量与布局"
    Private Function MeasureWidth(text As String) As Integer
        If String.IsNullOrEmpty(text) Then Return 0
        Dim displayText = GetDisplayText(text)
        Dim cached As Integer = 0
        If _textWidthCache.TryGetValue(displayText, cached) Then Return cached
        cached = CInt(Math.Ceiling(TextRenderHelper.MeasureTextWidth_D2D(displayText, Font, DpiScale(), GetTextFormatCacheForMeasure())))
        If _textWidthCache.Count >= MaxTextWidthCacheEntries Then _textWidthCache.Clear()
        _textWidthCache(displayText) = cached
        Return cached
    End Function
    Private Function MeasureLineWidth(lineIndex As Integer, startCol As Integer, length As Integer) As Integer
        If length <= 0 Then Return 0
        If lineIndex < 0 OrElse lineIndex >= _lines.Count Then Return 0
        startCol = Math.Max(0, Math.Min(startCol, _lines(lineIndex).Length))
        length = Math.Max(0, Math.Min(length, _lines(lineIndex).Length - startCol))
        If length <= 0 Then Return 0

        Dim key As New MeasureKey With {
            .LineIndex = lineIndex,
            .StartCol = startCol,
            .Length = length,
            .FontKey = GetLineMeasureFontKey(lineIndex, startCol, length),
            .TextVersion = _measureVersion
        }
        Dim cachedWidth As Integer = 0
        If _lineWidthCache.TryGetValue(key, cachedWidth) Then Return cachedWidth

        Dim runs = _lineRuns(lineIndex)
        Dim totalWidth As Integer
        If runs Is Nothing OrElse runs.Count = 0 Then
            totalWidth = MeasureWidth(_lines(lineIndex).Substring(startCol, length))
        Else
            Dim endCol = startCol + length
            Dim lineStr = _lines(lineIndex)
            For Each r In runs
                Dim rEnd = r.StartCol + r.Length
                Dim segStart = Math.Max(r.StartCol, startCol)
                Dim segEnd = Math.Min(rEnd, endCol)
                If segStart >= segEnd Then Continue For
                Dim useFont = If(r.RunFont, Font)
                Dim segText = GetDisplayText(lineStr.Substring(segStart, segEnd - segStart))
                totalWidth += CInt(Math.Ceiling(TextRenderHelper.MeasureTextWidth_D2D(segText, useFont, DpiScale(), GetTextFormatCacheForMeasure())))
            Next
        End If
        If _lineWidthCache.Count >= MaxLineWidthCacheEntries Then _lineWidthCache.Clear()
        _lineWidthCache(key) = totalWidth
        Return totalWidth
    End Function

    Private Function GetLineMeasureFontKey(lineIndex As Integer, startCol As Integer, length As Integer) As Integer
        Dim hash As New System.HashCode()
        hash.Add(Font)
        hash.Add(_passwordChar)
        hash.Add(启用多行)
        Dim runs = _lineRuns(lineIndex)
        If runs Is Nothing OrElse runs.Count = 0 Then Return hash.ToHashCode()
        Dim endCol As Integer = startCol + length
        For Each r In runs
            Dim segStart = Math.Max(r.StartCol, startCol)
            Dim segEnd = Math.Min(r.StartCol + r.Length, endCol)
            If segStart >= segEnd Then Continue For
            Dim useFont = If(r.RunFont, Font)
            hash.Add(useFont)
            hash.Add(r.ForeColor.ToArgb())
        Next
        Return hash.ToHashCode()
    End Function

    Private Sub InvalidateMeasureCache()
        _measureVersion += 1
        _lineWidthCache.Clear()
        _textWidthCache.Clear()
    End Sub
    Private Sub InvalidateLineMeasureCache(lineIndex As Integer)
        If _lineWidthCache.Count = 0 Then Return
        Dim removeKeys As New List(Of MeasureKey)
        For Each key In _lineWidthCache.Keys
            If key.LineIndex = lineIndex Then removeKeys.Add(key)
        Next
        For Each key In removeKeys
            _lineWidthCache.Remove(key)
        Next
    End Sub
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
            Dim segWidth = MeasureLineWidth(lineIndex, segStart, segEnd - segStart)
            If accWidth + segWidth > x Then
                Dim localCol = TextRenderHelper.FindColFromX_D2D(segText, x - accWidth, useFont, DpiScale(), GetTextFormatCacheForMeasure())
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
        Return Math.Max(1, TextViewportHeight() \ _scaledLineHeight)
    End Function
    Private Function TextViewportHeight() As Integer
        Dim bi As Integer = ScaledBorderWidth()
        Return Math.Max(0, ClientRectangle.Height - Math.Max(Padding.Top, bi) - Math.Max(Padding.Bottom, bi))
    End Function
    Private Function TextAreaWidth() As Integer
        Dim bi As Integer = ScaledBorderWidth()
        Dim scrollW As Integer = If(_scrollBarVisible, CInt(Math.Round(滚动条宽度 * DpiScale())) + ScrollBarRenderer.Margin * 2, 0)
        Dim gutterW As Integer = LineNumberGutterWidth()
        Dim leftUsed As Integer = If(gutterW > 0, bi + gutterW + Padding.Left, Math.Max(Padding.Left, bi))
        Return Math.Max(0, ClientRectangle.Width - leftUsed - Math.Max(Padding.Right, bi) - scrollW)
    End Function
    Private Function LineNumberGutterWidth() As Integer
        If Not _showLineNumbers OrElse Not 启用多行 Then Return 0
        Dim digitCount As Integer = Math.Max(1, _lines.Count).ToString().Length
        If _lineNumberGutterWidthCache >= 0 AndAlso _lineNumberGutterDigitCount = digitCount Then
            Return _lineNumberGutterWidthCache
        End If
        Dim useFont As Font = If(_lineNumFont, Font)
        Dim numW As Integer = 0
        For Each digit As Char In "089"
            Dim probe As String = New String(digit, digitCount)
            Dim probeW As Integer = CInt(Math.Ceiling(TextRenderHelper.MeasureTextWidth_D2D(probe, useFont, DpiScale(), GetTextFormatCacheForMeasure())))
            If probeW > numW Then numW = probeW
        Next
        _lineNumberGutterDigitCount = digitCount
        _lineNumberGutterWidthCache = _scaledLineNumPadL + numW + _scaledLineNumPadR
        Return _lineNumberGutterWidthCache
    End Function

    Private Function GetTextFormatCacheForMeasure() As D2DGlobals.TextFormatCache
        Return D2DHelperV2.GetCompositor(Me)?.TextFormatCache
    End Function
    Private Sub InvalidateLineNumberGutterCache()
        _lineNumberGutterWidthCache = -1
        _lineNumberGutterDigitCount = -1
    End Sub
#End Region

#Region "视觉行"
    Private Sub RebuildVisualLines()
        _visualLines.Clear()
        _visualLineStarts.Clear()
        Dim areaW As Integer = If(IsHandleCreated, TextAreaWidth(), 0)
        For li As Integer = 0 To _lines.Count - 1
            _visualLineStarts.Add(_visualLines.Count)
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
        If _visualLineStarts.Count = 0 Then
            _visualLineStarts.Add(0)
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
        Dim bestLen As Integer = Math.Max(1, best)
        If bestLen < remaining Then
            bestLen = PreferWhitespaceWrap(line, startCol, bestLen)
        End If
        Return bestLen
    End Function
    Private Function PreferWhitespaceWrap(line As String, startCol As Integer, fitLength As Integer) As Integer
        If fitLength <= 1 Then Return fitLength
        Dim limit As Integer = Math.Min(line.Length, startCol + fitLength)
        For i As Integer = limit - 1 To startCol + 1 Step -1
            If Char.IsWhiteSpace(line(i)) Then
                Return i - startCol + 1
            End If
        Next
        Return fitLength
    End Function
    Private Function BuildWaterTextVisualLines(text As String, maxWidth As Integer) As List(Of String)
        Dim result As New List(Of String)
        If String.IsNullOrEmpty(text) Then
            result.Add(String.Empty)
            Return result
        End If

        Dim logicalLines = text.Replace(vbCr, String.Empty).Split(ControlChars.Lf)
        For Each line In logicalLines
            If line.Length = 0 OrElse maxWidth <= 0 Then
                result.Add(String.Empty)
                Continue For
            End If

            Dim startCol As Integer = 0
            While startCol < line.Length
                Dim fitLen As Integer = FindWaterTextFitLength(line, startCol, maxWidth)
                result.Add(line.Substring(startCol, fitLen))
                startCol += fitLen
            End While
        Next
        If result.Count = 0 Then result.Add(String.Empty)
        Return result
    End Function
    Private Function FindWaterTextFitLength(line As String, startCol As Integer, maxWidth As Integer) As Integer
        Dim remaining As Integer = line.Length - startCol
        If remaining <= 0 Then Return 0
        If MeasureWidth(line.Substring(startCol, remaining)) <= maxWidth Then Return remaining

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

        Dim bestLen As Integer = Math.Max(1, best)
        If bestLen < remaining Then
            bestLen = PreferWhitespaceWrap(line, startCol, bestLen)
        End If
        Return bestLen
    End Function
    Private Function GetVisualLineIndex(logicalLine As Integer, col As Integer) As Integer
        If _visualLines.Count = 0 Then Return 0
        logicalLine = Math.Max(0, Math.Min(logicalLine, _lines.Count - 1))
        If logicalLine >= _visualLineStarts.Count Then Return Math.Max(0, _visualLines.Count - 1)
        Dim startIndex As Integer = Math.Max(0, Math.Min(_visualLineStarts(logicalLine), _visualLines.Count - 1))
        Dim endIndex As Integer = If(logicalLine + 1 < _visualLineStarts.Count,
            Math.Min(_visualLineStarts(logicalLine + 1) - 1, _visualLines.Count - 1),
            _visualLines.Count - 1)
        For i As Integer = endIndex To startIndex Step -1
            Dim vl = _visualLines(i)
            If vl.LogicalLine = logicalLine AndAlso col >= vl.StartCol Then
                Return i
            End If
        Next
        Return startIndex
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
        _underlineFontCache = New Font(baseFont, baseFont.Style Or System.Drawing.FontStyle.Underline)
        Return _underlineFontCache
    End Function
#End Region

#Region "语法高亮"
    Private Sub ApplySyntaxHighlighting()
        InvalidateMeasureCache()
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
    Private Sub ApplySyntaxHighlightingToLine(lineIndex As Integer, Optional applyRuns As Boolean = True)
        If Not _enableSyntaxHighlight OrElse _syntaxHighlighter Is Nothing Then Return
        InvalidateLineMeasureCache(lineIndex)
        Dim prevState As Integer = If(lineIndex > 0 AndAlso lineIndex - 1 < _lineStates.Count, _lineStates(lineIndex - 1), 0)
        Dim result = _syntaxHighlighter.HighlightLine(lineIndex, _lines(lineIndex), prevState)
        While _lineStates.Count <= lineIndex
            _lineStates.Add(0)
        End While
        _lineStates(lineIndex) = result.EndState
        If applyRuns Then
            _lineRuns(lineIndex) = TokensToRuns(result.Tokens, _lines(lineIndex).Length)
        End If
    End Sub
    Private Sub UpdateSyntaxHighlightingFrom(fromLine As Integer, toLine As Integer)
        If Not _enableSyntaxHighlight OrElse _syntaxHighlighter Is Nothing Then Return
        InvalidateMeasureCache()
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
        If tokens Is Nothing OrElse tokens.Count = 0 OrElse lineLength <= 0 Then Return Nothing
        Dim ordered = tokens.
            Where(Function(t) t.Length > 0 AndAlso t.StartCol < lineLength).
            OrderBy(Function(t) t.StartCol).
            ToList()
        If ordered.Count = 0 Then Return Nothing
        Dim runs As New List(Of TextRun)
        Dim pos As Integer = 0
        For Each tk In ordered
            Dim startCol As Integer = Math.Max(0, tk.StartCol)
            Dim endCol As Integer = Math.Min(lineLength, tk.StartCol + tk.Length)
            If endCol <= pos Then Continue For
            If startCol > pos Then runs.Add(New TextRun(pos, startCol - pos))
            Dim runStart As Integer = Math.Max(startCol, pos)
            runs.Add(New TextRun(runStart, endCol - runStart, tk.ForeColor, Nothing))
            pos = endCol
        Next
        If pos < lineLength Then runs.Add(New TextRun(pos, lineLength - pos))
        Return MergeAdjacentRuns(runs)
    End Function
#End Region

#Region "通用辅助"
    Private Sub SetLinesFromString(s As String)
        Dim normalized As String = If(s, "").Replace(vbCr, "")
        InvalidateMeasureCache()
        _lines = New List(Of String)(normalized.Split(vbLf))
        If _lines.Count = 0 Then _lines.Add("")
        _textLength = ComputeNormalizedTextLength()
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
        InvalidateMeasureCache()
        RefreshVisualLayout(True)
        OuterToInnerRefreshScheduler.RequestFull(Me)
        OnTextChanged(EventArgs.Empty)
        RaiseEvent TextChanged(Me, EventArgs.Empty)
    End Sub
    Private Sub ResetCaretBlink()
        _caretVisible = True
        _caretBlinkTimer.Stop()
        ' 只在聚焦时重启闪烁，否则保持停止状态。
        If Me.Focused Then
            _caretBlinkTimer.Start()
        End If
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            OuterToInnerRefreshScheduler.RequestFull(Me)
        End If
    End Sub
    Private Function DpiScale() As Single
        Return _cachedDpiScale
    End Function
    Private Function ScaledBorderWidth() As Integer
        Return _cachedBorderInset
    End Function
    Private Function ComputeNormalizedTextLength() As Integer
        Dim total As Integer = Math.Max(0, _lines.Count - 1)
        For Each line In _lines
            total += If(line, String.Empty).Length
        Next
        Return total
    End Function
    Private Sub UpdateDpiCache()
        _cachedDpiScale = Me.DeviceDpi / 96.0F
        _cachedBorderInset = CInt(Math.Round(边框宽度 * _cachedDpiScale))
        _scaledLineHeight = CInt(Math.Round(行高 * _cachedDpiScale))
        _scaledCaretWidth = CInt(Math.Round(光标线宽 * _cachedDpiScale))
        _scaledLineNumPadL = CInt(Math.Round(_lineNumPadLeft * _cachedDpiScale))
        _scaledLineNumPadR = CInt(Math.Round(_lineNumPadRight * _cachedDpiScale))
        InvalidateLineNumberGutterCache()
        InvalidateMeasureCache()
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
        SetScrollPixelOffset(_scrollPixelOffset + scrollDelta * _scaledLineHeight)
        Dim pos As Point = HitTest(_lastMousePos.X, _lastMousePos.Y)
        _caretLine = pos.Y
        _caretCol = pos.X
        _hasSelection = (_caretLine <> _selAnchorLine OrElse _caretCol <> _selAnchorCol)
        OuterToInnerRefreshScheduler.RequestFull(Me)
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
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Protected Overrides Sub OnLostFocus(e As EventArgs)
        MyBase.OnLostFocus(e)
        _caretBlinkTimer.Stop()
        _caretVisible = False
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        RefreshVisualLayout(True)
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        RefreshVisualLayout(True)
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Protected Overrides Sub OnFontChanged(e As EventArgs)
        _underlineFontCache?.Dispose()
        _underlineFontCache = Nothing
        _underlineFontBase = Nothing
        InvalidateLineNumberGutterCache()
        InvalidateMeasureCache()
        MyBase.OnFontChanged(e)
        RefreshVisualLayout(True)
        D2DHelperV2.RefreshFontDependentRendering(Me)
    End Sub
    Protected Overrides Sub OnBackColorChanged(e As EventArgs)
        MyBase.OnBackColorChanged(e)
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Protected Overrides Sub OnForeColorChanged(e As EventArgs)
        MyBase.OnForeColorChanged(e)
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub
    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        UpdateDpiCache()
        RefreshVisualLayout(True)
        OuterToInnerRefreshScheduler.RequestFull(Me)
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
