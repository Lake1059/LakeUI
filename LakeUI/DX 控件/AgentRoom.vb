Imports System.ComponentModel
Imports System.Drawing.Drawing2D
Imports System.Numerics
Imports System.Text.RegularExpressions
Imports Vortice.Direct2D1

''' <summary>
''' AI 聊天室控件：仅保留消息显示区，支持气泡消息与卡片消息、文字选取/复制、链接点击。
''' 数据通过 Items 集合或 AddXxx/Clear 方法操作；不内置输入框、按钮、线程切换。
''' </summary>
<DefaultEvent("LinkClicked")>
Public Class AgentRoom
    Inherits Control
    Implements V3_IGpuRenderable, V3_IGpuInvalidationSource

#Region "枚举与项类型"
    Public Enum ChatItemKind
        UserMessage = 0
        AssistantMessage = 1
        Card = 2
    End Enum

    Public Enum BubbleWidthMode
        Auto = 0
        FillAvailable = 1
        Fixed = 2
    End Enum

    ''' <summary>聊天条目（用简单字段类型，便于在属性窗口直接编辑）。</summary>
    <TypeConverter(GetType(ExpandableObjectConverter))>
    Public Class ChatItem
        Friend OwnerRoom As AgentRoom
        Friend CachedIndex As Integer = -1
        Friend NeedsRelayout As Boolean = True
        Friend CachedRect As Rectangle
        Friend ReadOnly LineRanges As New List(Of LineRange)
        Friend ReadOnly LinkSpans As New List(Of LinkSpan)
        Friend BubbleRect As Rectangle
        Friend TextOriginX As Integer
        Friend TextOriginY As Integer
        Friend MarkdownRenderer As MarkdownViewerCore
        Friend MarkdownRendererText As String = Nothing
        Friend MarkdownRendererWidth As Integer = -1
        Friend MarkdownRendererSubmittedTextVersion As Integer = -1
        Friend MarkdownRendererAppliedTextVersion As Integer = -1
        Friend TextVersion As Integer = 0
        Friend LinkSpansVersion As Integer = -1

        Private _kind As ChatItemKind = ChatItemKind.AssistantMessage
        Private _text As String = ""
        Private _textBuilder As System.Text.StringBuilder = Nothing
        Private _textSnapshotDirty As Boolean = False
        Private _pendingMarkdownRendererAppend As System.Text.StringBuilder = Nothing

        Public Sub New()
        End Sub

        Public Sub New(kind As ChatItemKind, text As String, Optional scanLinks As Boolean = True)
            _kind = kind
            _text = If(text, "")
            BumpTextVersion()
            If scanLinks Then
                RescanLinksInternal()
            Else
                LinkSpans.Clear()
                LinkSpansVersion = -1
            End If
        End Sub

        <Category("LakeUI"), Description("条目类型"), DefaultValue(ChatItemKind.AssistantMessage)>
        Public Property Kind As ChatItemKind
            Get
                Return _kind
            End Get
            Set(value As ChatItemKind)
                If _kind <> value Then
                    _kind = value
                    NeedsRelayout = True
                    OwnerRoom?.NotifyItemChanged(Me)
                End If
            End Set
        End Property

        <Category("LakeUI"), Description("文本内容"), DefaultValue("")>
        <Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design", GetType(System.Drawing.Design.UITypeEditor))>
        Public Property Text As String
            Get
                If _textSnapshotDirty AndAlso _textBuilder IsNot Nothing Then
                    _text = _textBuilder.ToString()
                    _textSnapshotDirty = False
                End If
                Return _text
            End Get
            Set(value As String)
                Dim v = If(value, "")
                If Text <> v Then
                    _text = v
                    _textBuilder = Nothing
                    _textSnapshotDirty = False
                    BumpTextVersion()
                    RescanLinksInternal()
                    NeedsRelayout = True
                    OwnerRoom?.NotifyItemChanged(Me)
                End If
            End Set
        End Property

        Friend Sub AppendTextInternal(more As String,
                                      Optional rescanLinks As Boolean = False,
                                      Optional markRelayout As Boolean = True)
            If String.IsNullOrEmpty(more) Then Return
            If _textBuilder Is Nothing Then _textBuilder = New System.Text.StringBuilder(_text)
            _textBuilder.Append(more)
            _textSnapshotDirty = True
            BumpTextVersion()
            If rescanLinks Then
                RescanLinksInternal()
            Else
                LinkSpansVersion = -1
            End If
            If markRelayout Then NeedsRelayout = True
        End Sub

        Friend Sub QueueMarkdownRendererAppend(more As String)
            If String.IsNullOrEmpty(more) Then Return
            If _pendingMarkdownRendererAppend Is Nothing Then _pendingMarkdownRendererAppend = New System.Text.StringBuilder()
            _pendingMarkdownRendererAppend.Append(more)
        End Sub

        Friend Function TakeMarkdownRendererAppend() As String
            If _pendingMarkdownRendererAppend Is Nothing OrElse _pendingMarkdownRendererAppend.Length = 0 Then Return ""
            Dim value = _pendingMarkdownRendererAppend.ToString()
            _pendingMarkdownRendererAppend.Clear()
            Return value
        End Function

        Friend Sub ClearMarkdownRendererAppend()
            _pendingMarkdownRendererAppend?.Clear()
        End Sub

        Friend ReadOnly Property HasMarkdownRendererAppend As Boolean
            Get
                Return _pendingMarkdownRendererAppend IsNot Nothing AndAlso _pendingMarkdownRendererAppend.Length > 0
            End Get
        End Property

        Friend Sub EnsureLinksCurrent()
            If LinkSpansVersion <> TextVersion Then RescanLinksInternal()
        End Sub

        Private Sub RescanLinksInternal()
            ChatTextHelper.RescanLinks(Text, LinkSpans)
            LinkSpansVersion = TextVersion
        End Sub

        Private Sub BumpTextVersion()
            If TextVersion = Integer.MaxValue Then
                TextVersion = 0
            Else
                TextVersion += 1
            End If
        End Sub

        Public Overrides Function ToString() As String
            Dim p = If(Text, "")
            If p.Length > 24 Then p = String.Concat(p.AsSpan(0, 24), "…")
            Return $"[{_kind}] {p}"
        End Function
    End Class

    Public Class ChatItemCollection
        Inherits ObjectModel.Collection(Of ChatItem)

        Private ReadOnly _owner As AgentRoom

        Friend Sub New(owner As AgentRoom)
            _owner = owner
        End Sub

        Private Sub RefreshCachedIndexes(startIndex As Integer)
            Dim first As Integer = Math.Max(0, startIndex)
            For i As Integer = first To Count - 1
                Dim it = Me(i)
                If it Is Nothing Then Continue For
                it.OwnerRoom = _owner
                it.CachedIndex = i
            Next
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As ChatItem)
            If item Is Nothing Then Return
            MyBase.InsertItem(index, item)
            item.OwnerRoom = _owner
            RefreshCachedIndexes(index)
            item.NeedsRelayout = True
            _owner?.OnItemsChanged(index, True)
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            Dim it = Me(index)
            _owner?.ReleaseMarkdownRenderer(it)
            it.OwnerRoom = Nothing
            it.CachedIndex = -1
            MyBase.RemoveItem(index)
            RefreshCachedIndexes(index)
            _owner?.OnItemsChanged(index, False)
        End Sub

        Protected Overrides Sub ClearItems()
            For Each it In Me
                _owner?.ReleaseMarkdownRenderer(it)
                it.OwnerRoom = Nothing
                it.CachedIndex = -1
            Next
            MyBase.ClearItems()
            _owner?.OnItemsChanged(0, False)
        End Sub

        Protected Overrides Sub SetItem(index As Integer, item As ChatItem)
            If item Is Nothing Then Return
            Dim old = Me(index)
            If old IsNot Nothing Then
                _owner?.ReleaseMarkdownRenderer(old)
                old.OwnerRoom = Nothing
                old.CachedIndex = -1
            End If
            MyBase.SetItem(index, item)
            item.OwnerRoom = _owner
            item.CachedIndex = index
            item.NeedsRelayout = True
            _owner?.OnItemsChanged(index, False)
        End Sub
    End Class

    Public Class LinkClickedEventArgs
        Inherits EventArgs
        Public Property Url As String
        Public Property ItemIndex As Integer
    End Class
#End Region
#Region "字段"
    Private Structure TextWidthKey
        Implements IEquatable(Of TextWidthKey)

        Public Text As String
        Public FontHash As Integer
        Public LineHeight As Integer
        Public Version As Integer

        Public Overloads Function Equals(other As TextWidthKey) As Boolean Implements IEquatable(Of TextWidthKey).Equals
            Return FontHash = other.FontHash AndAlso
                   LineHeight = other.LineHeight AndAlso
                   Version = other.Version AndAlso
                   String.Equals(Text, other.Text, StringComparison.Ordinal)
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            Return TypeOf obj Is TextWidthKey AndAlso Equals(DirectCast(obj, TextWidthKey))
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return System.HashCode.Combine(Text, FontHash, LineHeight, Version)
        End Function
    End Structure

    Friend ReadOnly _items As New ChatItemCollection(Me)
    Friend ReadOnly _scrollBar As New V3_ScrollBarRenderer()
    Friend _滚动偏移 As Integer = 0
    Friend _contentHeight As Integer = 0
    Friend _contentHeightDirty As Boolean = True
    Friend _pinnedToBottom As Boolean = True
    Private _layoutDpi As Integer = 0
    Private _layoutAreaWidth As Integer = -1
    Private _layoutDirtyFromIndex As Integer = 0
    Private ReadOnly _itemLayoutTops As New List(Of Integer)()
    Private _measureVersion As Integer
    Private ReadOnly _textWidthCache As New Dictionary(Of TextWidthKey, Integer)(512)
    Private Const MaxTextWidthCacheEntries As Integer = 4096
    Private ReadOnly _streamLayoutTimer As New PrecisionTimer() With {
        .Interval = 50,
        .DispatchMode = PrecisionTimer.DispatchModeEnum.NonBlocking,
        .OverrunPolicy = PrecisionTimer.OverrunPolicyEnum.Drop,
        .WorkerThreadCount = 1,
        .AutoReset = True
    }
    Private ReadOnly _dirtyStreamItems As New HashSet(Of ChatItem)()
    Private _suppressMarkdownRendererAppliedEvents As Integer

    Friend _hoverLinkUrl As String = Nothing

    ' 选区
    Friend Structure TextPos
        Public ItemIndex As Integer
        Public CharIndex As Integer
        Public Sub New(ei As Integer, ci As Integer)
            ItemIndex = ei : CharIndex = ci
        End Sub
        Public Shared Operator =(a As TextPos, b As TextPos) As Boolean
            Return a.ItemIndex = b.ItemIndex AndAlso a.CharIndex = b.CharIndex
        End Operator
        Public Shared Operator <>(a As TextPos, b As TextPos) As Boolean
            Return Not (a = b)
        End Operator
        Public Overrides Function Equals(obj As Object) As Boolean
            If TypeOf obj Is TextPos Then Return Me = DirectCast(obj, TextPos)
            Return False
        End Function
        Public Overrides Function GetHashCode() As Integer
            Return System.HashCode.Combine(ItemIndex, CharIndex)
        End Function
    End Structure

    Private Structure MarkdownHitInfo
        Public Renderer As MarkdownViewerCore
        Public LocalPoint As Point
        Public ItemIndex As Integer

        Public ReadOnly Property HasValue As Boolean
            Get
                Return Renderer IsNot Nothing
            End Get
        End Property
    End Structure

    Private Structure LinkHitInfo
        Public Url As String
        Public ItemIndex As Integer

        Public ReadOnly Property HasValue As Boolean
            Get
                Return Not String.IsNullOrEmpty(Url)
            End Get
        End Property
    End Structure

    Friend _selAnchor As New TextPos(-1, 0)
    Friend _selCaret As New TextPos(-1, 0)
    Friend _hasSelection As Boolean = False
    Friend _mouseSelecting As Boolean = False
    Friend _mouseSelectingMarkdown As Boolean = False
    Friend _activeMarkdownRenderer As MarkdownViewerCore = Nothing
    Friend _activeMarkdownItemIndex As Integer = -1
    Friend _mouseDownLinkUrl As String = Nothing
    Friend _mouseDownLinkItemIndex As Integer = -1

    Friend _copyContextMenu As ContextMenuStrip

    Friend _enableMarkdownForAssistant As Boolean = True
    Private _markdownParser As MarkdownViewerCore.MarkdownParser = Nothing
    Private _markdownBasePath As String = Nothing
#End Region

#Region "构造"
    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.UserPaint Or
                     ControlStyles.ResizeRedraw Or
                     ControlStyles.Selectable Or
                     ControlStyles.SupportsTransparentBackColor, True)
        DoubleBuffered = True
        Me.Size = New Size(420, 560)
        Me.BackColor = Color.Transparent

        _copyContextMenu = New ContextMenuStrip()
        Dim miCopy As New ToolStripMenuItem("复制(&C)")
        AddHandler miCopy.Click, Sub(s, e) CopySelectionToClipboard()
        Dim miSelAll As New ToolStripMenuItem("全选(&A)")
        AddHandler miSelAll.Click, Sub(s, e) SelectAllText()
        _copyContextMenu.Items.Add(miCopy)
        _copyContextMenu.Items.Add(miSelAll)
        _streamLayoutTimer.SynchronizingObject = Me
        AddHandler _streamLayoutTimer.Tick, AddressOf StreamLayoutTimerTick
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            Try
                RemoveHandler _streamLayoutTimer.Tick, AddressOf StreamLayoutTimerTick
                _streamLayoutTimer.Stop()
                _streamLayoutTimer.Dispose()
            Catch
            End Try

            For Each it In _items
                ReleaseMarkdownRenderer(it)
            Next

            Try
                _copyContextMenu?.Dispose()
            Catch
            End Try
        End If
        MyBase.Dispose(disposing)
    End Sub
#End Region
#Region "属性"
    Friend _superSamplingScale As GlobalOptions.SuperSamplingScaleEnum = GlobalOptions.SuperSamplingScaleEnum.OFF
    <Category("LakeUI"), Description(GlobalOptions.超采样抗锯齿描述词), DefaultValue(GetType(GlobalOptions.SuperSamplingScaleEnum), "OFF")>
    Public Property SuperSamplingScale As GlobalOptions.SuperSamplingScaleEnum
        Get
            Return _superSamplingScale
        End Get
        Set(value As GlobalOptions.SuperSamplingScaleEnum)
            SetValue(_superSamplingScale, value)
        End Set
    End Property

    Private _backgroundSource As Control = Nothing
    <Category("LakeUI"),
     Description("背景采样源（V3 背景图）。设置后将跨越任意层级直接采样此控件的绘制内容作为透明背景；为空时按 BackColor 协议处理。"),
     DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource IsNot value Then
                _backgroundSource = D3D_BackgroundPenetration.SetBackgroundSource(Me, _backgroundSource, value)
                RequestV3Render()
            End If
        End Set
    End Property

    Friend _backColor1 As Color = Color.FromArgb(30, 30, 30)
    <Category("LakeUI"), Description("主体背景颜色"), DefaultValue(GetType(Color), "30, 30, 30"), Browsable(True)>
    Public Property BackColor1 As Color
        Get
            Return _backColor1
        End Get
        Set(value As Color)
            SetValue(_backColor1, value)
        End Set
    End Property

    Friend _borderRadius As Integer = 0
    <Category("LakeUI"), Description("控件外框圆角半径"), DefaultValue(0)>
    Public Property BorderRadius As Integer
        Get
            Return _borderRadius
        End Get
        Set(value As Integer)
            SetValue(_borderRadius, Math.Max(0, value))
        End Set
    End Property

    Friend _borderColor As Color = Color.FromArgb(63, 63, 70)
    <Category("LakeUI"), Description("控件外框边框颜色"), DefaultValue(GetType(Color), "63, 63, 70")>
    Public Property BorderColor As Color
        Get
            Return _borderColor
        End Get
        Set(value As Color)
            SetValue(_borderColor, value)
        End Set
    End Property

    Friend _borderSize As Integer = 0
    <Category("LakeUI"), Description("控件外框边框宽度"), DefaultValue(0)>
    Public Property BorderSize As Integer
        Get
            Return _borderSize
        End Get
        Set(value As Integer)
            SetValue(_borderSize, Math.Max(0, value))
        End Set
    End Property

    <Category("LakeUI"), Description("聊天条目集合"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
    Public ReadOnly Property Items As ChatItemCollection
        Get
            Return _items
        End Get
    End Property

    Friend _itemSpacing As Integer = 10
    <Category("LakeUI"), Description("两条消息之间的垂直间距"), DefaultValue(10)>
    Public Property ItemSpacing As Integer
        Get
            Return _itemSpacing
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If _itemSpacing <> value Then
                _itemSpacing = value
                MarkContentLayoutDirty(0)
                RequestV3Render()
            End If
        End Set
    End Property

    Friend _bubblePadding As New Padding(10, 6, 10, 6)
    <Category("LakeUI"), Description("气泡内边距"), DefaultValue(GetType(Padding), "10, 6, 10, 6")>
    Public Property BubblePadding As Padding
        Get
            Return _bubblePadding
        End Get
        Set(value As Padding)
            If _bubblePadding <> value Then
                _bubblePadding = value
                InvalidateAllItemsLayout() : RequestV3Render()
            End If
        End Set
    End Property

    Friend _bubbleRadius As Integer = 8
    <Category("LakeUI"), Description("气泡圆角半径"), DefaultValue(8)>
    Public Property BubbleRadius As Integer
        Get
            Return _bubbleRadius
        End Get
        Set(value As Integer)
            SetValue(_bubbleRadius, Math.Max(0, value))
        End Set
    End Property

    Friend _bubbleWidthMode As BubbleWidthMode = BubbleWidthMode.Auto
    <Category("LakeUI"), Description("气泡宽度模式"), DefaultValue(BubbleWidthMode.Auto)>
    Public Property BubbleWidth As BubbleWidthMode
        Get
            Return _bubbleWidthMode
        End Get
        Set(value As BubbleWidthMode)
            If _bubbleWidthMode <> value Then
                _bubbleWidthMode = value
                InvalidateAllItemsLayout() : RequestV3Render()
            End If
        End Set
    End Property

    Friend _bubbleMaxWidthRatio As Single = 0.7F
    <Category("LakeUI"), Description("气泡最大宽度占可用宽度的比例（Auto 模式生效）"), DefaultValue(0.7F)>
    Public Property BubbleMaxWidthRatio As Single
        Get
            Return _bubbleMaxWidthRatio
        End Get
        Set(value As Single)
            value = Math.Max(0.1F, Math.Min(1.0F, value))
            If _bubbleMaxWidthRatio <> value Then
                _bubbleMaxWidthRatio = value
                InvalidateAllItemsLayout() : RequestV3Render()
            End If
        End Set
    End Property

    Friend _bubbleFixedWidth As Integer = 240
    <Category("LakeUI"), Description("Fixed 模式下的固定气泡宽度"), DefaultValue(240)>
    Public Property BubbleFixedWidth As Integer
        Get
            Return _bubbleFixedWidth
        End Get
        Set(value As Integer)
            value = Math.Max(40, value)
            If _bubbleFixedWidth <> value Then
                _bubbleFixedWidth = value
                InvalidateAllItemsLayout() : RequestV3Render()
            End If
        End Set
    End Property

    Friend _assistantBubbleBackColor As Color = Color.FromArgb(51, 51, 55)
    <Category("LakeUI"), Description("助手气泡背景色"), DefaultValue(GetType(Color), "51, 51, 55")>
    Public Property AssistantBubbleBackColor As Color
        Get
            Return _assistantBubbleBackColor
        End Get
        Set(value As Color)
            SetValue(_assistantBubbleBackColor, value)
        End Set
    End Property

    Friend _assistantBubbleForeColor As Color = Color.FromArgb(220, 220, 220)
    <Category("LakeUI"), Description("助手气泡文字颜色"), DefaultValue(GetType(Color), "220, 220, 220")>
    Public Property AssistantBubbleForeColor As Color
        Get
            Return _assistantBubbleForeColor
        End Get
        Set(value As Color)
            SetValue(_assistantBubbleForeColor, value)
        End Set
    End Property

    Friend _userBubbleBackColor As Color = Color.FromArgb(14, 99, 156)
    <Category("LakeUI"), Description("用户气泡背景色"), DefaultValue(GetType(Color), "14, 99, 156")>
    Public Property UserBubbleBackColor As Color
        Get
            Return _userBubbleBackColor
        End Get
        Set(value As Color)
            SetValue(_userBubbleBackColor, value)
        End Set
    End Property

    Friend _userBubbleForeColor As Color = Color.White
    <Category("LakeUI"), Description("用户气泡文字颜色"), DefaultValue(GetType(Color), "White")>
    Public Property UserBubbleForeColor As Color
        Get
            Return _userBubbleForeColor
        End Get
        Set(value As Color)
            SetValue(_userBubbleForeColor, value)
        End Set
    End Property

    Friend _linkColor As Color = MarkdownViewerCore.DefaultMarkdownLinkColor
    <Category("LakeUI"), Description("链接颜色"), DefaultValue(GetType(Color), "86, 156, 214")>
    Public Property LinkColor As Color
        Get
            Return _linkColor
        End Get
        Set(value As Color)
            SetValue(_linkColor, value)
        End Set
    End Property

    Friend _linkHoverColor As Color = Color.FromArgb(120, 180, 230)
    <Category("LakeUI"), Description("链接悬停颜色"), DefaultValue(GetType(Color), "120, 180, 230")>
    Public Property LinkHoverColor As Color
        Get
            Return _linkHoverColor
        End Get
        Set(value As Color)
            SetValue(_linkHoverColor, value)
        End Set
    End Property

    Friend _linkUnderline As Boolean = True
    <Category("LakeUI"), Description("链接是否绘制下划线"), DefaultValue(True)>
    Public Property LinkUnderline As Boolean
        Get
            Return _linkUnderline
        End Get
        Set(value As Boolean)
            SetValue(_linkUnderline, value)
        End Set
    End Property

    Friend _linkUnderlineThickness As Integer = MarkdownViewerCore.DefaultMarkdownLinkUnderlineThickness
    <Category("LakeUI"), Description("链接下划线粗细"), DefaultValue(1)>
    Public Property LinkUnderlineThickness As Integer
        Get
            Return _linkUnderlineThickness
        End Get
        Set(value As Integer)
            SetValue(_linkUnderlineThickness, Math.Max(1, value))
        End Set
    End Property

    Friend _linkUnderlineOffset As Integer = MarkdownViewerCore.DefaultMarkdownLinkUnderlineOffset
    <Category("LakeUI"), Description("链接下划线距离文本行底部的偏移"), DefaultValue(3)>
    Public Property LinkUnderlineOffset As Integer
        Get
            Return _linkUnderlineOffset
        End Get
        Set(value As Integer)
            SetValue(_linkUnderlineOffset, Math.Max(0, value))
        End Set
    End Property

    Friend _cardBackColor As Color = Color.FromArgb(40, 40, 43)
    <Category("LakeUI"), Description("卡片背景色"), DefaultValue(GetType(Color), "40, 40, 43")>
    Public Property CardBackColor As Color
        Get
            Return _cardBackColor
        End Get
        Set(value As Color)
            SetValue(_cardBackColor, value)
        End Set
    End Property

    Friend _cardForeColor As Color = Color.FromArgb(220, 220, 220)
    <Category("LakeUI"), Description("卡片文字颜色"), DefaultValue(GetType(Color), "220, 220, 220")>
    Public Property CardForeColor As Color
        Get
            Return _cardForeColor
        End Get
        Set(value As Color)
            SetValue(_cardForeColor, value)
        End Set
    End Property

    Friend _cardBorderColor As Color = Color.FromArgb(63, 63, 70)
    <Category("LakeUI"), Description("卡片边框颜色"), DefaultValue(GetType(Color), "63, 63, 70")>
    Public Property CardBorderColor As Color
        Get
            Return _cardBorderColor
        End Get
        Set(value As Color)
            SetValue(_cardBorderColor, value)
        End Set
    End Property

    Friend _cardBorderSize As Integer = 1
    <Category("LakeUI"), Description("卡片边框宽度"), DefaultValue(1)>
    Public Property CardBorderSize As Integer
        Get
            Return _cardBorderSize
        End Get
        Set(value As Integer)
            SetValue(_cardBorderSize, Math.Max(0, value))
        End Set
    End Property

    Friend _cardRadius As Integer = 6
    <Category("LakeUI"), Description("卡片圆角半径"), DefaultValue(6)>
    Public Property CardRadius As Integer
        Get
            Return _cardRadius
        End Get
        Set(value As Integer)
            SetValue(_cardRadius, Math.Max(0, value))
        End Set
    End Property

    Friend _cardPadding As New Padding(8, 6, 8, 6)
    <Category("LakeUI"), Description("卡片内边距"), DefaultValue(GetType(Padding), "8, 6, 8, 6")>
    Public Property CardPadding As Padding
        Get
            Return _cardPadding
        End Get
        Set(value As Padding)
            If _cardPadding <> value Then
                _cardPadding = value
                InvalidateAllItemsLayout() : RequestV3Render()
            End If
        End Set
    End Property

    Friend _cardMaxWidthRatio As Single = 0.92F
    <Category("LakeUI"), Description("卡片最大宽度占可用宽度的比例"), DefaultValue(0.92F)>
    Public Property CardMaxWidthRatio As Single
        Get
            Return _cardMaxWidthRatio
        End Get
        Set(value As Single)
            value = Math.Max(0.2F, Math.Min(1.0F, value))
            If _cardMaxWidthRatio <> value Then
                _cardMaxWidthRatio = value
                InvalidateAllItemsLayout() : RequestV3Render()
            End If
        End Set
    End Property

    Friend _scrollBarWidth As Integer = 6
    <Category("LakeUI"), Description("滚动条宽度"), DefaultValue(6)>
    Public Property ScrollBarWidth As Integer
        Get
            Return _scrollBarWidth
        End Get
        Set(value As Integer)
            SetValue(_scrollBarWidth, Math.Max(0, value))
        End Set
    End Property

    Friend Shared ReadOnly 默认ScrollBarTrackColor As Color = Color.Transparent
    Friend _scrollBarTrackColor As Color = 默认ScrollBarTrackColor
    <Category("LakeUI"), Description("滚动条轨道颜色")>
    Public Property ScrollBarTrackColor As Color
        Get
            Return _scrollBarTrackColor
        End Get
        Set(value As Color)
            SetValue(_scrollBarTrackColor, value)
        End Set
    End Property

    Private Function ShouldSerializeScrollBarTrackColor() As Boolean
        Return _scrollBarTrackColor <> 默认ScrollBarTrackColor
    End Function

    Private Sub ResetScrollBarTrackColor()
        ScrollBarTrackColor = 默认ScrollBarTrackColor
    End Sub

    Friend Shared ReadOnly 默认ScrollBarThumbColor As Color = Color.FromArgb(80, 80, 84)
    Friend _scrollBarThumbColor As Color = 默认ScrollBarThumbColor
    <Category("LakeUI"), Description("滚动条滑块颜色")>
    Public Property ScrollBarThumbColor As Color
        Get
            Return _scrollBarThumbColor
        End Get
        Set(value As Color)
            SetValue(_scrollBarThumbColor, value)
        End Set
    End Property

    Private Function ShouldSerializeScrollBarThumbColor() As Boolean
        Return _scrollBarThumbColor <> 默认ScrollBarThumbColor
    End Function

    Private Sub ResetScrollBarThumbColor()
        ScrollBarThumbColor = 默认ScrollBarThumbColor
    End Sub

    Friend Shared ReadOnly 默认ScrollBarThumbHoverColor As Color = Color.FromArgb(120, 120, 124)
    Friend _scrollBarThumbHoverColor As Color = 默认ScrollBarThumbHoverColor
    <Category("LakeUI"), Description("滚动条滑块悬停颜色")>
    Public Property ScrollBarThumbHoverColor As Color
        Get
            Return _scrollBarThumbHoverColor
        End Get
        Set(value As Color)
            SetValue(_scrollBarThumbHoverColor, value)
        End Set
    End Property

    Private Function ShouldSerializeScrollBarThumbHoverColor() As Boolean
        Return _scrollBarThumbHoverColor <> 默认ScrollBarThumbHoverColor
    End Function

    Private Sub ResetScrollBarThumbHoverColor()
        ScrollBarThumbHoverColor = 默认ScrollBarThumbHoverColor
    End Sub

    Friend _autoScrollToBottom As Boolean = True
    <Category("LakeUI"), Description("新消息时是否自动贴底"), DefaultValue(True)>
    Public Property AutoScrollToBottom As Boolean
        Get
            Return _autoScrollToBottom
        End Get
        Set(value As Boolean)
            _autoScrollToBottom = value
        End Set
    End Property

    Friend _wheelStep As Integer = 40
    <Category("LakeUI"), Description("鼠标滚轮单步像素"), DefaultValue(40)>
    Public Property WheelStep As Integer
        Get
            Return _wheelStep
        End Get
        Set(value As Integer)
            _wheelStep = Math.Max(1, value)
        End Set
    End Property

    Friend _selectionBackColor As Color = Color.FromArgb(120, 38, 79, 120)
    <Category("LakeUI"), Description("选中背景色"), DefaultValue(GetType(Color), "120, 38, 79, 120")>
    Public Property SelectionBackColor As Color
        Get
            Return _selectionBackColor
        End Get
        Set(value As Color)
            SetValue(_selectionBackColor, value)
        End Set
    End Property

    Friend _selectableText As Boolean = True
    <Category("LakeUI"), Description("是否允许选中消息文字"), DefaultValue(True)>
    Public Property SelectableText As Boolean
        Get
            Return _selectableText
        End Get
        Set(value As Boolean)
            _selectableText = value
            If Not value Then ClearSelection()
        End Set
    End Property

    Friend _showCopyContextMenu As Boolean = True
    <Category("LakeUI"), Description("是否显示右键复制菜单"), DefaultValue(True)>
    Public Property ShowCopyContextMenu As Boolean
        Get
            Return _showCopyContextMenu
        End Get
        Set(value As Boolean)
            _showCopyContextMenu = value
        End Set
    End Property

    <Category("LakeUI - Markdown"), Description("助手消息是否按 Markdown 渲染"), DefaultValue(True)>
    Public Property EnableMarkdownForAssistant As Boolean
        Get
            Return _enableMarkdownForAssistant
        End Get
        Set(value As Boolean)
            If _enableMarkdownForAssistant <> value Then
                _enableMarkdownForAssistant = value
                InvalidateMarkdownRenderers()
            End If
        End Set
    End Property

    <Category("LakeUI - Markdown"), Description("卡片消息固定按普通文本渲染"), DefaultValue(False)>
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never)>
    Public Property EnableMarkdownForCard As Boolean
        Get
            Return False
        End Get
        Set(value As Boolean)
        End Set
    End Property

    <Category("LakeUI - Markdown"), Description("用户消息固定按普通文本渲染"), DefaultValue(False)>
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never)>
    Public Property EnableMarkdownForUser As Boolean
        Get
            Return False
        End Get
        Set(value As Boolean)
        End Set
    End Property

    <Category("LakeUI - Markdown"), Description("AgentRoom 内部 Markdown 解析器"), Browsable(False),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property MarkdownParser As MarkdownViewerCore.MarkdownParser
        Get
            Return _markdownParser
        End Get
        Set(value As MarkdownViewerCore.MarkdownParser)
            If Not Object.ReferenceEquals(_markdownParser, value) Then
                _markdownParser = value
                InvalidateMarkdownRenderers()
            End If
        End Set
    End Property

    <Category("LakeUI - Markdown"), Description("Markdown 图片相对路径的基准目录"), DefaultValue(GetType(String), "")>
    Public Property MarkdownBasePath As String
        Get
            Return _markdownBasePath
        End Get
        Set(value As String)
            If _markdownBasePath <> value Then
                _markdownBasePath = value
                InvalidateMarkdownRenderers()
            End If
        End Set
    End Property

    Friend _markdownHeadingColor As Color = MarkdownViewerCore.DefaultMarkdownHeadingColor
    <Category("LakeUI - Markdown"), Description("Markdown 标题颜色"), DefaultValue(GetType(Color), "Silver")>
    Public Property HeadingColor As Color
        Get
            Return _markdownHeadingColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownHeadingColor, value)
        End Set
    End Property

    Friend _markdownHeadingSeparatorColor As Color = MarkdownViewerCore.DefaultMarkdownHeadingSeparatorColor
    <Category("LakeUI - Markdown"), Description("Markdown H1/H2 标题下方分隔线颜色"), DefaultValue(GetType(Color), "80, 220, 220, 220")>
    Public Property HeadingSeparatorColor As Color
        Get
            Return _markdownHeadingSeparatorColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownHeadingSeparatorColor, value)
        End Set
    End Property

    Friend _markdownHeadingSeparatorThickness As Integer = MarkdownViewerCore.DefaultMarkdownHeadingSeparatorThickness
    <Category("LakeUI - Markdown"), Description("Markdown H1/H2 标题下方分隔线粗细"), DefaultValue(2)>
    Public Property HeadingSeparatorThickness As Integer
        Get
            Return _markdownHeadingSeparatorThickness
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownHeadingSeparatorThickness, Math.Max(0, value))
        End Set
    End Property

    Friend _markdownHeadingSeparatorGap As Integer = MarkdownViewerCore.DefaultMarkdownHeadingSeparatorGap
    <Category("LakeUI - Markdown"), Description("Markdown H1/H2 标题文字与分隔线之间的间距"), DefaultValue(4)>
    Public Property HeadingSeparatorGap As Integer
        Get
            Return _markdownHeadingSeparatorGap
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownHeadingSeparatorGap, Math.Max(0, value))
        End Set
    End Property

    Friend _markdownBoldColor As Color = MarkdownViewerCore.DefaultMarkdownBoldColor
    <Category("LakeUI - Markdown"), Description("Markdown 粗体颜色。Empty 时跟随当前文本颜色"), DefaultValue(GetType(Color), "")>
    Public Property BoldColor As Color
        Get
            Return _markdownBoldColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownBoldColor, value)
        End Set
    End Property

    Friend _markdownItalicColor As Color = MarkdownViewerCore.DefaultMarkdownItalicColor
    <Category("LakeUI - Markdown"), Description("Markdown 斜体颜色。Empty 时跟随当前文本颜色"), DefaultValue(GetType(Color), "")>
    Public Property ItalicColor As Color
        Get
            Return _markdownItalicColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownItalicColor, value)
        End Set
    End Property

    Friend _markdownInlineCodeColor As Color = MarkdownViewerCore.DefaultMarkdownInlineCodeColor
    <Category("LakeUI - Markdown"), Description("Markdown 行内代码文字颜色"), DefaultValue(GetType(Color), "206, 145, 120")>
    Public Property InlineCodeColor As Color
        Get
            Return _markdownInlineCodeColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownInlineCodeColor, value)
        End Set
    End Property

    Friend _markdownInlineCodeBackColor As Color = MarkdownViewerCore.DefaultMarkdownCodeBackColor
    Friend _markdownCodeBlockBackColor As Color = MarkdownViewerCore.DefaultMarkdownCodeBackColor
    <Category("LakeUI - Markdown"), Description("Markdown 行内代码 / 代码块背景颜色。Empty 时自动跟随当前气泡/卡片背景"), DefaultValue(GetType(Color), "120, 0, 0, 0")>
    Public Property CodeBackColor As Color
        Get
            Return _markdownCodeBlockBackColor
        End Get
        Set(value As Color)
            If _markdownInlineCodeBackColor <> value OrElse _markdownCodeBlockBackColor <> value Then
                _markdownInlineCodeBackColor = value
                _markdownCodeBlockBackColor = value
                InvalidateMarkdownRenderers()
            End If
        End Set
    End Property

    <Category("LakeUI - Markdown"), Description("Markdown 行内代码背景颜色。Empty 时自动跟随当前气泡/卡片背景"), DefaultValue(GetType(Color), "120, 0, 0, 0")>
    Public Property InlineCodeBackColor As Color
        Get
            Return _markdownInlineCodeBackColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownInlineCodeBackColor, value)
        End Set
    End Property

    <Category("LakeUI - Markdown"), Description("Markdown 代码块背景颜色。Empty 时自动跟随当前气泡/卡片背景"), DefaultValue(GetType(Color), "120, 0, 0, 0")>
    Public Property CodeBlockBackColor As Color
        Get
            Return _markdownCodeBlockBackColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownCodeBlockBackColor, value)
        End Set
    End Property

    Friend _markdownCodeBlockForeColor As Color = MarkdownViewerCore.DefaultMarkdownCodeBlockForeColor
    <Category("LakeUI - Markdown"), Description("Markdown 代码块文字颜色"), DefaultValue(GetType(Color), "Silver")>
    Public Property CodeBlockForeColor As Color
        Get
            Return _markdownCodeBlockForeColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownCodeBlockForeColor, value)
        End Set
    End Property

    Friend _markdownInlineCodePadding As Padding = MarkdownViewerCore.DefaultMarkdownInlineCodePadding
    <Category("LakeUI - Markdown"), Description("Markdown 行内代码内边距"), DefaultValue(GetType(Padding), "5, 3, 5, 3")>
    Public Property InlineCodePadding As Padding
        Get
            Return _markdownInlineCodePadding
        End Get
        Set(value As Padding)
            SetMarkdownStyleValue(_markdownInlineCodePadding, value)
        End Set
    End Property

    Friend _markdownInlineCodeRadius As Integer = MarkdownViewerCore.DefaultMarkdownInlineCodeRadius
    <Category("LakeUI - Markdown"), Description("Markdown 行内代码圆角半径"), DefaultValue(3)>
    Public Property InlineCodeRadius As Integer
        Get
            Return _markdownInlineCodeRadius
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownInlineCodeRadius, Math.Max(0, value))
        End Set
    End Property

    Friend _markdownCodeBlockPadding As Padding = MarkdownViewerCore.DefaultMarkdownCodeBlockPadding
    <Category("LakeUI - Markdown"), Description("Markdown 代码块内边距"), DefaultValue(GetType(Padding), "7, 5, 7, 5")>
    Public Property CodeBlockPadding As Padding
        Get
            Return _markdownCodeBlockPadding
        End Get
        Set(value As Padding)
            SetMarkdownStyleValue(_markdownCodeBlockPadding, value)
        End Set
    End Property

    Friend _markdownCodeFont As Font = Nothing
    <Category("LakeUI - Markdown"), Description("Markdown 代码字体；Nothing 时代码块使用 Consolas，行内代码继承当前文本字体"), Browsable(True),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property CodeFont As Font
        Get
            Return _markdownCodeFont
        End Get
        Set(value As Font)
            SetMarkdownStyleValue(_markdownCodeFont, value)
        End Set
    End Property

    Friend _markdownBlockSpacing As Integer = MarkdownViewerCore.DefaultMarkdownBlockSpacing
    <Category("LakeUI - Markdown"), Description("Markdown 段落间距"), DefaultValue(20)>
    Public Property BlockSpacing As Integer
        Get
            Return _markdownBlockSpacing
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownBlockSpacing, Math.Max(0, value))
        End Set
    End Property

    Friend _markdownInlineLineSpacing As Integer = MarkdownViewerCore.DefaultMarkdownInlineLineSpacing
    <Category("LakeUI - Markdown"), Description("Markdown 自动换行后的行内行距"), DefaultValue(4)>
    Public Property InlineLineSpacing As Integer
        Get
            Return _markdownInlineLineSpacing
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownInlineLineSpacing, Math.Max(0, value))
        End Set
    End Property

    Friend _markdownTableCellPadding As Integer = MarkdownViewerCore.DefaultMarkdownTableCellPadding
    <Category("LakeUI - Markdown"), Description("Markdown 表格单元格内边距"), DefaultValue(7)>
    Public Property TableCellPadding As Integer
        Get
            Return _markdownTableCellPadding
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownTableCellPadding, Math.Max(0, value))
        End Set
    End Property

    Friend _markdownTableBorderThickness As Integer = MarkdownViewerCore.DefaultMarkdownTableBorderThickness
    <Category("LakeUI - Markdown"), Description("Markdown 表格边框粗细"), DefaultValue(1)>
    Public Property TableBorderThickness As Integer
        Get
            Return _markdownTableBorderThickness
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownTableBorderThickness, Math.Max(1, value))
        End Set
    End Property

    Friend _markdownTableBorderColor As Color = MarkdownViewerCore.DefaultMarkdownTableBorderColor
    <Category("LakeUI - Markdown"), Description("Markdown 表格边框颜色。Empty 时自动跟随当前气泡/卡片背景"), DefaultValue(GetType(Color), "80, 220, 220, 220")>
    Public Property TableBorderColor As Color
        Get
            Return _markdownTableBorderColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownTableBorderColor, value)
        End Set
    End Property

    Friend _markdownTableHeaderBackColor As Color = MarkdownViewerCore.DefaultMarkdownTableHeaderBackColor
    <Category("LakeUI - Markdown"), Description("Markdown 表头背景颜色。Empty 时自动跟随当前气泡/卡片背景"), DefaultValue(GetType(Color), "40, 220, 220, 220")>
    Public Property TableHeaderBackColor As Color
        Get
            Return _markdownTableHeaderBackColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownTableHeaderBackColor, value)
        End Set
    End Property

    Friend _markdownBlockQuoteBarColor As Color = MarkdownViewerCore.DefaultMarkdownBlockQuoteBarColor
    <Category("LakeUI - Markdown"), Description("Markdown 引用块竖条颜色"), DefaultValue(GetType(Color), "80, 220, 220, 220")>
    Public Property BlockQuoteBarColor As Color
        Get
            Return _markdownBlockQuoteBarColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownBlockQuoteBarColor, value)
        End Set
    End Property

    Friend _markdownBlockQuoteForeColor As Color = MarkdownViewerCore.DefaultMarkdownBlockQuoteForeColor
    <Category("LakeUI - Markdown"), Description("Markdown 引用块文字颜色"), DefaultValue(GetType(Color), "100, 255, 255, 255")>
    Public Property BlockQuoteForeColor As Color
        Get
            Return _markdownBlockQuoteForeColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownBlockQuoteForeColor, value)
        End Set
    End Property

    Friend _markdownBlockQuoteIndent As Integer = MarkdownViewerCore.DefaultMarkdownBlockQuoteIndent
    <Category("LakeUI - Markdown"), Description("Markdown 引用块文字缩进"), DefaultValue(16)>
    Public Property BlockQuoteIndent As Integer
        Get
            Return _markdownBlockQuoteIndent
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownBlockQuoteIndent, Math.Max(0, value))
        End Set
    End Property

    Friend _markdownBlockQuoteBarOffset As Integer = MarkdownViewerCore.DefaultMarkdownBlockQuoteBarOffset
    <Category("LakeUI - Markdown"), Description("Markdown 引用块竖条偏移"), DefaultValue(4)>
    Public Property BlockQuoteBarOffset As Integer
        Get
            Return _markdownBlockQuoteBarOffset
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownBlockQuoteBarOffset, Math.Max(0, value))
        End Set
    End Property

    Friend _markdownBlockQuoteBarWidth As Integer = MarkdownViewerCore.DefaultMarkdownBlockQuoteBarWidth
    <Category("LakeUI - Markdown"), Description("Markdown 引用块竖条宽度"), DefaultValue(3)>
    Public Property BlockQuoteBarWidth As Integer
        Get
            Return _markdownBlockQuoteBarWidth
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownBlockQuoteBarWidth, Math.Max(1, value))
        End Set
    End Property

    Friend _markdownUnorderedListIndent As Integer = MarkdownViewerCore.DefaultMarkdownUnorderedListIndent
    <Category("LakeUI - Markdown"), Description("Markdown 无序列表文字缩进"), DefaultValue(20)>
    Public Property UnorderedListIndent As Integer
        Get
            Return _markdownUnorderedListIndent
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownUnorderedListIndent, Math.Max(0, value))
        End Set
    End Property

    Friend _markdownOrderedListIndent As Integer = MarkdownViewerCore.DefaultMarkdownOrderedListIndent
    <Category("LakeUI - Markdown"), Description("Markdown 有序列表文字缩进"), DefaultValue(24)>
    Public Property OrderedListIndent As Integer
        Get
            Return _markdownOrderedListIndent
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownOrderedListIndent, Math.Max(0, value))
        End Set
    End Property

    Friend _markdownBulletRadius As Integer = MarkdownViewerCore.DefaultMarkdownBulletRadius
    <Category("LakeUI - Markdown"), Description("Markdown 无序列表圆点半径"), DefaultValue(3)>
    Public Property BulletRadius As Integer
        Get
            Return _markdownBulletRadius
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownBulletRadius, Math.Max(1, value))
        End Set
    End Property

    Friend _markdownBulletOffsetX As Integer = MarkdownViewerCore.DefaultMarkdownBulletOffsetX
    <Category("LakeUI - Markdown"), Description("Markdown 无序列表圆点 X 偏移"), DefaultValue(6)>
    Public Property BulletOffsetX As Integer
        Get
            Return _markdownBulletOffsetX
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownBulletOffsetX, Math.Max(0, value))
        End Set
    End Property

    Friend _markdownBulletOffsetY As Integer = MarkdownViewerCore.DefaultMarkdownBulletOffsetY
    <Category("LakeUI - Markdown"), Description("Markdown 无序列表圆点 Y 偏移"), DefaultValue(-2)>
    Public Property BulletOffsetY As Integer
        Get
            Return _markdownBulletOffsetY
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownBulletOffsetY, value)
        End Set
    End Property

    Friend _markdownOrderedListMarkerWidth As Integer = MarkdownViewerCore.DefaultMarkdownOrderedListMarkerWidth
    <Category("LakeUI - Markdown"), Description("Markdown 有序列表序号标记宽度"), DefaultValue(22)>
    Public Property OrderedListMarkerWidth As Integer
        Get
            Return _markdownOrderedListMarkerWidth
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownOrderedListMarkerWidth, Math.Max(1, value))
        End Set
    End Property

    Friend _markdownAlertNoteColor As Color = MarkdownViewerCore.DefaultMarkdownAlertNoteColor
    <Category("LakeUI - Markdown"), Description("Markdown [!NOTE] 提示块颜色"), DefaultValue(GetType(Color), "83, 155, 245")>
    Public Property AlertNoteColor As Color
        Get
            Return _markdownAlertNoteColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownAlertNoteColor, value)
        End Set
    End Property

    Friend _markdownAlertTipColor As Color = MarkdownViewerCore.DefaultMarkdownAlertTipColor
    <Category("LakeUI - Markdown"), Description("Markdown [!TIP] 提示块颜色"), DefaultValue(GetType(Color), "87, 171, 90")>
    Public Property AlertTipColor As Color
        Get
            Return _markdownAlertTipColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownAlertTipColor, value)
        End Set
    End Property

    Friend _markdownAlertImportantColor As Color = MarkdownViewerCore.DefaultMarkdownAlertImportantColor
    <Category("LakeUI - Markdown"), Description("Markdown [!IMPORTANT] 提示块颜色"), DefaultValue(GetType(Color), "152, 110, 226")>
    Public Property AlertImportantColor As Color
        Get
            Return _markdownAlertImportantColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownAlertImportantColor, value)
        End Set
    End Property

    Friend _markdownAlertWarningColor As Color = MarkdownViewerCore.DefaultMarkdownAlertWarningColor
    <Category("LakeUI - Markdown"), Description("Markdown [!WARNING] 提示块颜色"), DefaultValue(GetType(Color), "198, 144, 38")>
    Public Property AlertWarningColor As Color
        Get
            Return _markdownAlertWarningColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownAlertWarningColor, value)
        End Set
    End Property

    Friend _markdownAlertCautionColor As Color = MarkdownViewerCore.DefaultMarkdownAlertCautionColor
    <Category("LakeUI - Markdown"), Description("Markdown [!CAUTION] 提示块颜色"), DefaultValue(GetType(Color), "229, 83, 75")>
    Public Property AlertCautionColor As Color
        Get
            Return _markdownAlertCautionColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownAlertCautionColor, value)
        End Set
    End Property

    Friend _markdownHorizontalRuleColor As Color = MarkdownViewerCore.DefaultMarkdownHorizontalRuleColor
    <Category("LakeUI - Markdown"), Description("Markdown 分隔线颜色"), DefaultValue(GetType(Color), "60, 60, 60")>
    Public Property HorizontalRuleColor As Color
        Get
            Return _markdownHorizontalRuleColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownHorizontalRuleColor, value)
        End Set
    End Property

    Friend _markdownHorizontalRuleThickness As Integer = MarkdownViewerCore.DefaultMarkdownHorizontalRuleThickness
    <Category("LakeUI - Markdown"), Description("Markdown 分隔线粗细"), DefaultValue(1)>
    Public Property HorizontalRuleThickness As Integer
        Get
            Return _markdownHorizontalRuleThickness
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownHorizontalRuleThickness, Math.Max(1, value))
        End Set
    End Property

    Friend _markdownHorizontalRulePadding As Integer = MarkdownViewerCore.DefaultMarkdownHorizontalRulePadding
    <Category("LakeUI - Markdown"), Description("Markdown 分隔线上下内边距"), DefaultValue(4)>
    Public Property HorizontalRulePadding As Integer
        Get
            Return _markdownHorizontalRulePadding
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownHorizontalRulePadding, Math.Max(0, value))
        End Set
    End Property

    Friend _markdownHorizontalRuleInset As Integer = MarkdownViewerCore.DefaultMarkdownHorizontalRuleInset
    <Category("LakeUI - Markdown"), Description("Markdown 分隔线水平缩进"), DefaultValue(4)>
    Public Property HorizontalRuleInset As Integer
        Get
            Return _markdownHorizontalRuleInset
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownHorizontalRuleInset, Math.Max(0, value))
        End Set
    End Property

    Friend _markdownImagePlaceholderBorderColor As Color = MarkdownViewerCore.DefaultMarkdownImagePlaceholderBorderColor
    <Category("LakeUI - Markdown"), Description("Markdown 图片占位边框颜色"), DefaultValue(GetType(Color), "40, 220, 220, 220")>
    Public Property ImagePlaceholderBorderColor As Color
        Get
            Return _markdownImagePlaceholderBorderColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownImagePlaceholderBorderColor, value)
        End Set
    End Property

    Friend _markdownImagePlaceholderTextColor As Color = MarkdownViewerCore.DefaultMarkdownImagePlaceholderTextColor
    <Category("LakeUI - Markdown"), Description("Markdown 图片占位文字颜色"), DefaultValue(GetType(Color), "80, 220, 220, 220")>
    Public Property ImagePlaceholderTextColor As Color
        Get
            Return _markdownImagePlaceholderTextColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownImagePlaceholderTextColor, value)
        End Set
    End Property

    Friend _markdownImagePlaceholderWidth As Integer = MarkdownViewerCore.DefaultMarkdownImagePlaceholderWidth
    <Category("LakeUI - Markdown"), Description("Markdown 图片占位宽度"), DefaultValue(300)>
    Public Property ImagePlaceholderWidth As Integer
        Get
            Return _markdownImagePlaceholderWidth
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownImagePlaceholderWidth, Math.Max(1, value))
        End Set
    End Property

    Friend _markdownImagePlaceholderHeightLines As Integer = MarkdownViewerCore.DefaultMarkdownImagePlaceholderHeightLines
    <Category("LakeUI - Markdown"), Description("Markdown 图片占位高度行数"), DefaultValue(2)>
    Public Property ImagePlaceholderHeightLines As Integer
        Get
            Return _markdownImagePlaceholderHeightLines
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownImagePlaceholderHeightLines, Math.Max(1, value))
        End Set
    End Property

    Friend _markdownStrikethroughColor As Color = MarkdownViewerCore.DefaultMarkdownStrikethroughColor
    <Category("LakeUI - Markdown"), Description("Markdown 删除线颜色"), DefaultValue(GetType(Color), "Gray")>
    Public Property StrikethroughColor As Color
        Get
            Return _markdownStrikethroughColor
        End Get
        Set(value As Color)
            SetMarkdownStyleValue(_markdownStrikethroughColor, value)
        End Set
    End Property

    Friend _markdownStrikethroughThickness As Integer = MarkdownViewerCore.DefaultMarkdownStrikethroughThickness
    <Category("LakeUI - Markdown"), Description("Markdown 删除线粗细"), DefaultValue(1)>
    Public Property StrikethroughThickness As Integer
        Get
            Return _markdownStrikethroughThickness
        End Get
        Set(value As Integer)
            SetMarkdownStyleValue(_markdownStrikethroughThickness, Math.Max(1, value))
        End Set
    End Property

    Public Event LinkClicked As EventHandler(Of LinkClickedEventArgs)
#End Region
#Region "公共 API"
    ''' <summary>清空所有消息并重置滚动/选区。</summary>
    Public Sub Clear()
        ClearSelection()
        _streamLayoutTimer.Stop()
        _dirtyStreamItems.Clear()
        _items.Clear()
        _滚动偏移 = 0
        _pinnedToBottom = True
        _itemLayoutTops.Clear()
        _layoutAreaWidth = -1
        MarkContentLayoutDirty(0)
        RequestV3Render()
    End Sub

    ''' <summary>添加一条用户消息。</summary>
    Public Function AddUserMessage(text As String) As ChatItem
        Return AddItem(ChatItemKind.UserMessage, text)
    End Function

    ''' <summary>添加一条助手消息。</summary>
    Public Function AddAssistantMessage(text As String) As ChatItem
        Return AddItem(ChatItemKind.AssistantMessage, text)
    End Function

    ''' <summary>添加一张卡片消息（占满气泡区，无气泡尾）。</summary>
    Public Function AddCard(text As String) As ChatItem
        Return AddItem(ChatItemKind.Card, text)
    End Function

    Private Function AddItem(kind As ChatItemKind, text As String) As ChatItem
        Dim it As New ChatItem(kind, text, scanLinks:=Not (kind = ChatItemKind.AssistantMessage AndAlso _enableMarkdownForAssistant))
        _items.Add(it)
        Return it
    End Function

    ''' <summary>向最后一条消息追加文本（用于流式输出）。若没有任何消息，则新建一条助手消息。</summary>
    Public Sub AppendToLast(more As String)
        If String.IsNullOrEmpty(more) Then Return
        Dim last As ChatItem
        If _items.Count = 0 Then
            Dim unused As ChatItem = AddAssistantMessage(more)
        Else
            last = _items(_items.Count - 1)
            If ShouldUseMarkdown(last) Then
                last.AppendTextInternal(more, rescanLinks:=False, markRelayout:=False)
                last.QueueMarkdownRendererAppend(more)
                If Not SubmitPendingMarkdownRendererAppend(last) Then QueueStreamItemLayout(last)
            Else
                last.AppendTextInternal(more, rescanLinks:=True)
                NotifyItemChanged(last)
            End If
        End If
    End Sub

    ''' <summary>滚动到底部。</summary>
    Public Sub ScrollToBottom()
        _pinnedToBottom = True
        EnsureLayout()
        Dim viewH As Integer = ContentViewportHeight()
        _滚动偏移 = Math.Max(0, _contentHeight - viewH)
        RequestViewportRefresh()
    End Sub
#End Region

#Region "内部状态变更通知"
    Friend Sub NotifyItemChanged(item As ChatItem)
        If item Is Nothing Then Return
        item.NeedsRelayout = True
        MarkContentLayoutDirty(GetItemIndex(item))
        RequestViewportRefresh()
    End Sub

    Private Sub QueueStreamItemLayout(item As ChatItem)
        If item Is Nothing OrElse item.OwnerRoom IsNot Me Then Return
        _dirtyStreamItems.Add(item)
        If Not _streamLayoutTimer.IsRunning Then
            Try
                _streamLayoutTimer.Start()
            Catch
                StreamLayoutTimerTick(_streamLayoutTimer, EventArgs.Empty)
            End Try
        End If
    End Sub

    Private Sub StreamLayoutTimerTick(sender As Object, e As EventArgs)
        _streamLayoutTimer.Stop()
        If _dirtyStreamItems.Count = 0 Then Return

        Dim snapshot = _dirtyStreamItems.ToArray()
        _dirtyStreamItems.Clear()
        Dim needOuterRefresh As Boolean = False
        For Each it In snapshot
            If it Is Nothing OrElse it.OwnerRoom IsNot Me Then Continue For
            If ShouldUseMarkdown(it) AndAlso CanSubmitMarkdownRendererAppend(it) Then
                SubmitPendingMarkdownRendererAppend(it)
            Else
                it.NeedsRelayout = True
                MarkContentLayoutDirty(GetItemIndex(it))
                needOuterRefresh = True
            End If
        Next
        If needOuterRefresh Then RequestViewportRefresh()
    End Sub

    Private Function CanSubmitMarkdownRendererAppend(it As ChatItem) As Boolean
        Return it IsNot Nothing AndAlso
               it.MarkdownRenderer IsNot Nothing AndAlso
               Not it.MarkdownRenderer.IsDisposed AndAlso
               it.MarkdownRendererWidth > 0
    End Function

    Private Function SubmitPendingMarkdownRendererAppend(it As ChatItem) As Boolean
        If Not CanSubmitMarkdownRendererAppend(it) Then Return False
        Dim appended = it.TakeMarkdownRendererAppend()
        If String.IsNullOrEmpty(appended) Then Return True
        it.MarkdownRenderer.AppendMarkdown(appended)
        it.MarkdownRendererSubmittedTextVersion = it.TextVersion
        Return True
    End Function

    Friend Sub OnItemsChanged(index As Integer, isInsert As Boolean)
        MarkContentLayoutDirty(index)
        If isInsert AndAlso _autoScrollToBottom AndAlso _pinnedToBottom Then
            ' 在贴底状态下插入，应保持贴底
        End If
        RequestViewportRefresh()
    End Sub

    Private Sub InvalidateAllItemsLayout()
        InvalidateMeasureCache()
        For Each it In _items
            it.NeedsRelayout = True
        Next
        MarkContentLayoutDirty(0)
    End Sub

    Private Sub MarkContentLayoutDirty(firstIndex As Integer)
        Dim normalized As Integer = Math.Max(0, firstIndex)
        If Not _contentHeightDirty Then
            _layoutDirtyFromIndex = normalized
        Else
            _layoutDirtyFromIndex = Math.Min(_layoutDirtyFromIndex, normalized)
        End If
        _contentHeightDirty = True
    End Sub

    Private Function GetItemIndex(item As ChatItem) As Integer
        If item Is Nothing Then Return -1
        If item.CachedIndex >= 0 AndAlso item.CachedIndex < _items.Count AndAlso Object.ReferenceEquals(_items(item.CachedIndex), item) Then
            Return item.CachedIndex
        End If
        Return _items.IndexOf(item)
    End Function

    Private Sub RequestViewportRefresh()
        Dim dirty As Rectangle = GetContentArea()
        If Not _scrollBar.TrackRect.IsEmpty Then dirty = Rectangle.Union(dirty, _scrollBar.TrackRect)
        If _scrollBarWidth > 0 Then
            Dim borderSize As Integer = Dpi(_borderSize)
            Dim borderRadius As Integer = Dpi(_borderRadius)
            Dim inset As Integer = Math.Max(borderSize, If(borderRadius > 0, borderRadius \ 2, 0))
            Dim bandWidth As Integer = Dpi(_scrollBarWidth) + V3_ScrollBarRenderer.Margin * 4 + inset
            Dim bandLeft As Integer = Math.Max(0, Width - bandWidth)
            dirty = Rectangle.Union(dirty, New Rectangle(bandLeft, 0, Width - bandLeft, Height))
        End If
        dirty = Rectangle.Intersect(ClientRectangle, dirty)
        If dirty.Width > 0 AndAlso dirty.Height > 0 Then
            RequestV3Render(dirty)
        Else
            RequestV3Render()
        End If
    End Sub

    Private Sub InvalidateMeasureCache()
        _measureVersion += 1
        _textWidthCache.Clear()
    End Sub

    Private Function GetFontMeasureHash(font As Font) As Integer
        If font Is Nothing Then Return 0
        Return System.HashCode.Combine(font.FontFamily.Name, font.Style, font.SizeInPoints, font.Unit)
    End Function

    Private Function MeasureTextWidthCached(text As String, font As Font, lineHeight As Integer) As Integer
        If String.IsNullOrEmpty(text) Then Return 0
        Dim key As New TextWidthKey With {
            .Text = text,
            .FontHash = GetFontMeasureHash(font),
            .LineHeight = lineHeight,
            .Version = _measureVersion
        }
        Dim cached As Integer = 0
        If _textWidthCache.TryGetValue(key, cached) Then Return cached
        cached = CInt(Math.Ceiling(D3D_TextMeasureHelper.MeasureTextWidth_D2D(text, font, DpiScale(), GetTextFormatCacheForMeasure())))
        If _textWidthCache.Count >= MaxTextWidthCacheEntries Then _textWidthCache.Clear()
        _textWidthCache(key) = cached
        Return cached
    End Function

    Private Function GetTextFormatCacheForMeasure() As D3D_D2DInterop.TextFormatCache
        Return Nothing
    End Function

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            InvalidateAllItemsLayout()
            RequestV3Render()
        End If
    End Sub

    Private Sub SetMarkdownStyleValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            InvalidateMarkdownRenderers()
        End If
    End Sub

    Private Function DpiScale() As Single
        Return V3_DpiContext.FromControl(Me).Scale
    End Function

    Private Function Dpi(value As Integer) As Integer
        Return CInt(Math.Round(value * DpiScale()))
    End Function

    Private Function Dpi(value As Single) As Single
        Return value * DpiScale()
    End Function

    Private Function Dpi(value As Padding) As Padding
        Dim s As Single = DpiScale()
        Return New Padding(
            CInt(Math.Round(value.Left * s)),
            CInt(Math.Round(value.Top * s)),
            CInt(Math.Round(value.Right * s)),
            CInt(Math.Round(value.Bottom * s)))
    End Function

    Private Function GetLineHeight(font As Font) As Integer
        If font Is Nothing Then Return Dpi(18)
        Return CInt(Math.Ceiling(font.GetHeight(V3_DpiContext.FromControl(Me).Dpi)))
    End Function

    Private Sub InvalidateMarkdownRenderers()
        For Each it In _items
            ReleaseMarkdownRenderer(it)
            it.NeedsRelayout = True
        Next
        MarkContentLayoutDirty(0)
        RequestV3Render()
    End Sub

    Friend Sub ReleaseMarkdownRenderer(it As ChatItem)
        If it Is Nothing OrElse it.MarkdownRenderer Is Nothing Then Return
        Dim renderer = it.MarkdownRenderer
        it.MarkdownRenderer = Nothing
        it.MarkdownRendererText = Nothing
        it.MarkdownRendererWidth = -1
        it.MarkdownRendererSubmittedTextVersion = -1
        it.MarkdownRendererAppliedTextVersion = -1
        it.ClearMarkdownRendererAppend()
        Try
            Controls.Remove(renderer)
            renderer.BackgroundSource = Nothing
            renderer.Dispose()
        Catch
        End Try
    End Sub

    Private Function ShouldUseMarkdown(it As ChatItem) As Boolean
        If it Is Nothing Then Return False
        Return it.Kind = ChatItemKind.AssistantMessage AndAlso _enableMarkdownForAssistant
    End Function

    Private Function GetItemForeColor(it As ChatItem) As Color
        If it.Kind = ChatItemKind.Card Then Return _cardForeColor
        If it.Kind = ChatItemKind.UserMessage Then Return _userBubbleForeColor
        Return _assistantBubbleForeColor
    End Function

    Private Function GetItemBackColor(it As ChatItem) As Color
        If it.Kind = ChatItemKind.Card Then Return _cardBackColor
        If it.Kind = ChatItemKind.UserMessage Then Return _userBubbleBackColor
        Return _assistantBubbleBackColor
    End Function

    Private Function EnsureMarkdownRenderer(it As ChatItem, contentWidth As Integer) As MarkdownViewerCore
        If it.MarkdownRenderer Is Nothing OrElse it.MarkdownRenderer.IsDisposed Then
            Dim renderer As New MarkdownViewerCore With {
                .EmbeddedContentMode = True,
                .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent,
                .BorderSize = 0,
                .BorderRadius = 0,
                .Padding = Padding.Empty,
                .TabStop = False
            }
            AddHandler renderer.EmbeddedMouseWheel,
                Sub(sender, e)
                    ScrollByPixels(-Math.Sign(e.Delta) * Dpi(_wheelStep))
                End Sub
            AddHandler renderer.LinkClicked,
                Sub(sender, e)
                    ActivateLink(e.LinkText, GetItemIndex(it))
                End Sub
            AddHandler renderer.ContentHeightChanged,
                Sub(sender, e)
                    If Not Object.ReferenceEquals(it.MarkdownRenderer, sender) Then Return
                    CommitMarkdownRendererLayout(it, requestRefresh:=True)
                End Sub
            AddHandler renderer.EmbeddedContentApplied,
                Sub(sender, e)
                    If Not Object.ReferenceEquals(it.MarkdownRenderer, sender) Then Return
                    If _suppressMarkdownRendererAppliedEvents > 0 Then Return
                    it.MarkdownRendererAppliedTextVersion = it.MarkdownRendererSubmittedTextVersion
                    CommitMarkdownRendererLayout(it, requestRefresh:=True)
                End Sub
            it.MarkdownRenderer = renderer
        End If

        ConfigureMarkdownRenderer(it.MarkdownRenderer, it)
        Dim safeWidth As Integer = Math.Max(1, contentWidth)
        it.MarkdownRenderer.PrepareEmbeddedContent(safeWidth, V3_DpiContext.FromControl(Me).Dpi, Me)
        If it.MarkdownRendererSubmittedTextVersion <> it.TextVersion OrElse it.MarkdownRendererWidth <> safeWidth Then
            If it.MarkdownRendererWidth = safeWidth AndAlso it.MarkdownRendererSubmittedTextVersion >= 0 Then
                If Not it.HasMarkdownRendererAppend Then
                    SetMarkdownRendererImmediate(it)
                End If
            Else
                it.ClearMarkdownRendererAppend()
                SetMarkdownRendererImmediate(it)
            End If
            it.MarkdownRendererText = Nothing
            it.MarkdownRendererWidth = safeWidth
        End If
        Return it.MarkdownRenderer
    End Function

    Private Sub SetMarkdownRendererImmediate(it As ChatItem)
        If it Is Nothing OrElse it.MarkdownRenderer Is Nothing OrElse it.MarkdownRenderer.IsDisposed Then Return
        _suppressMarkdownRendererAppliedEvents += 1
        Try
            it.MarkdownRenderer.SetMarkdownImmediate(it.Text, resetScroll:=True, clearSelectionOnApply:=True)
        Finally
            _suppressMarkdownRendererAppliedEvents -= 1
        End Try
        it.MarkdownRendererSubmittedTextVersion = it.TextVersion
        it.MarkdownRendererAppliedTextVersion = it.TextVersion
    End Sub

    Private Sub CommitMarkdownRendererLayout(it As ChatItem, Optional requestRefresh As Boolean = True)
        If it Is Nothing OrElse it.OwnerRoom IsNot Me Then Return
        it.NeedsRelayout = True
        MarkContentLayoutDirty(GetItemIndex(it))
        If requestRefresh Then RequestViewportRefresh()
    End Sub

    Private Sub ConfigureMarkdownRenderer(renderer As MarkdownViewerCore, it As ChatItem)
        Dim fore As Color = GetItemForeColor(it)
        If renderer.BackgroundSource IsNot Nothing Then renderer.BackgroundSource = Nothing
        renderer.Font = Me.Font
        renderer.ForeColor = fore
        renderer.HeadingColor = _markdownHeadingColor
        renderer.HeadingSeparatorColor = _markdownHeadingSeparatorColor
        renderer.HeadingSeparatorThickness = _markdownHeadingSeparatorThickness
        renderer.HeadingSeparatorGap = _markdownHeadingSeparatorGap
        renderer.BoldColor = _markdownBoldColor
        renderer.ItalicColor = _markdownItalicColor
        renderer.LinkColor = _linkColor
        renderer.LinkUnderlineThickness = _linkUnderlineThickness
        renderer.LinkUnderlineOffset = _linkUnderlineOffset
        renderer.SelectionColor = _selectionBackColor
        renderer.BackColor1 = Color.Transparent
        renderer.InlineCodeColor = _markdownInlineCodeColor
        renderer.CodeBlockForeColor = _markdownCodeBlockForeColor
        renderer.InlineCodeBackColor = If(_markdownInlineCodeBackColor = Color.Empty, Color.FromArgb(If(it.Kind = ChatItemKind.UserMessage, 52, 56), GetItemBackColor(it)), _markdownInlineCodeBackColor)
        renderer.CodeBlockBackColor = If(_markdownCodeBlockBackColor = Color.Empty, Color.FromArgb(If(it.Kind = ChatItemKind.UserMessage, 44, 48), GetItemBackColor(it)), _markdownCodeBlockBackColor)
        renderer.InlineCodePadding = _markdownInlineCodePadding
        renderer.InlineCodeRadius = _markdownInlineCodeRadius
        renderer.CodeBlockPadding = _markdownCodeBlockPadding
        renderer.CodeFont = _markdownCodeFont
        renderer.BlockSpacing = _markdownBlockSpacing
        renderer.InlineLineSpacing = _markdownInlineLineSpacing
        renderer.TableCellPadding = _markdownTableCellPadding
        renderer.TableBorderThickness = _markdownTableBorderThickness
        renderer.TableBorderColor = If(_markdownTableBorderColor = Color.Empty, ControlPaint.Light(GetItemBackColor(it), 0.25F), _markdownTableBorderColor)
        renderer.TableHeaderBackColor = If(_markdownTableHeaderBackColor = Color.Empty, ControlPaint.Light(GetItemBackColor(it), 0.12F), _markdownTableHeaderBackColor)
        renderer.BlockQuoteBarColor = If(_markdownBlockQuoteBarColor = Color.Empty, ControlPaint.Light(fore, 0.2F), _markdownBlockQuoteBarColor)
        renderer.BlockQuoteForeColor = If(_markdownBlockQuoteForeColor = Color.Empty, ControlPaint.Light(fore, 0.15F), _markdownBlockQuoteForeColor)
        renderer.BlockQuoteIndent = _markdownBlockQuoteIndent
        renderer.BlockQuoteBarOffset = _markdownBlockQuoteBarOffset
        renderer.BlockQuoteBarWidth = _markdownBlockQuoteBarWidth
        renderer.UnorderedListIndent = _markdownUnorderedListIndent
        renderer.OrderedListIndent = _markdownOrderedListIndent
        renderer.BulletRadius = _markdownBulletRadius
        renderer.BulletOffsetX = _markdownBulletOffsetX
        renderer.BulletOffsetY = _markdownBulletOffsetY
        renderer.OrderedListMarkerWidth = _markdownOrderedListMarkerWidth
        renderer.AlertNoteColor = _markdownAlertNoteColor
        renderer.AlertTipColor = _markdownAlertTipColor
        renderer.AlertImportantColor = _markdownAlertImportantColor
        renderer.AlertWarningColor = _markdownAlertWarningColor
        renderer.AlertCautionColor = _markdownAlertCautionColor
        renderer.HorizontalRuleColor = _markdownHorizontalRuleColor
        renderer.HorizontalRuleThickness = _markdownHorizontalRuleThickness
        renderer.HorizontalRulePadding = _markdownHorizontalRulePadding
        renderer.HorizontalRuleInset = _markdownHorizontalRuleInset
        renderer.ImagePlaceholderBorderColor = _markdownImagePlaceholderBorderColor
        renderer.ImagePlaceholderTextColor = _markdownImagePlaceholderTextColor
        renderer.ImagePlaceholderWidth = _markdownImagePlaceholderWidth
        renderer.ImagePlaceholderHeightLines = _markdownImagePlaceholderHeightLines
        renderer.StrikethroughColor = _markdownStrikethroughColor
        renderer.StrikethroughThickness = _markdownStrikethroughThickness
        renderer.BasePath = _markdownBasePath
        If _markdownParser IsNot Nothing AndAlso Not Object.ReferenceEquals(renderer.Parser, _markdownParser) Then
            renderer.Parser = _markdownParser
        End If
    End Sub

    Private Sub ScrollByPixels(delta As Integer)
        SetScrollOffset(_滚动偏移 + delta)
    End Sub

    Private Function GetMaxScrollOffset() As Integer
        Return Math.Max(0, _contentHeight - ContentViewportHeight())
    End Function

    Private Sub SetScrollOffset(value As Integer)
        EnsureLayout()
        Dim maxOff = GetMaxScrollOffset()
        Dim clamped = Math.Max(0, Math.Min(maxOff, value))
        If _滚动偏移 = clamped AndAlso _pinnedToBottom = (_滚动偏移 >= maxOff) Then Return

        _滚动偏移 = clamped
        _pinnedToBottom = (_滚动偏移 >= maxOff)
        RequestViewportRefresh()
    End Sub

    Private Sub AutoScrollSelectionAtEdge(mouseY As Integer)
        Dim area = GetContentArea()
        If mouseY < area.Top Then
            ScrollByPixels(-Dpi(8))
        ElseIf mouseY > area.Bottom Then
            ScrollByPixels(Dpi(8))
        End If
    End Sub
#End Region
#Region "布局与测量"
    Private Function GetContentArea() As Rectangle
        Dim borderSize As Integer = Dpi(_borderSize)
        Dim borderRadius As Integer = Dpi(_borderRadius)
        Dim inset As Integer = Math.Max(borderSize, If(borderRadius > 0, borderRadius \ 2, 0))
        Dim pad As Padding = Dpi(Me.Padding)
        Dim x As Integer = inset + pad.Left
        Dim y As Integer = inset + pad.Top
        Dim sbReserved As Integer = If(_scrollBarWidth > 0, Dpi(_scrollBarWidth) + V3_ScrollBarRenderer.Margin * 2, 0)
        Dim w As Integer = Math.Max(0, Width - inset * 2 - pad.Horizontal - sbReserved)
        Dim h As Integer = Math.Max(0, Height - inset * 2 - pad.Vertical)
        Return New Rectangle(x, y, w, h)
    End Function

    Private Function ContentViewportHeight() As Integer
        Return GetContentArea().Height
    End Function

    Private Sub EnsureLayout()
        Dim currentDpi As Integer = V3_DpiContext.FromControl(Me).Dpi
        If currentDpi > 0 AndAlso currentDpi <> _layoutDpi Then
            _layoutDpi = currentDpi
            InvalidateAllItemsLayout()
        End If

        Dim area = GetContentArea()
        If area.Width <= 0 Then
            _contentHeight = 0
            _contentHeightDirty = False
            _layoutAreaWidth = -1
            _layoutDirtyFromIndex = Integer.MaxValue
            _itemLayoutTops.Clear()
            Return
        End If

        If _contentHeightDirty OrElse
           _layoutAreaWidth <> area.Width OrElse
           _itemLayoutTops.Count <> _items.Count Then
            RebuildItemLayout(area.Width)
        End If

        ClampScrollOffset(area.Height)
    End Sub

    Private Sub RebuildItemLayout(areaWidth As Integer)
        Dim itemSpacing As Integer = Dpi(_itemSpacing)
        Dim count As Integer = _items.Count
        Dim fullRebuild As Boolean = _layoutAreaWidth <> areaWidth OrElse
                                     _itemLayoutTops.Count <> count OrElse
                                     _layoutDirtyFromIndex <= 0

        If fullRebuild Then
            _itemLayoutTops.Clear()
            If _itemLayoutTops.Capacity < count Then _itemLayoutTops.Capacity = count
            Dim totalHeight As Integer = 0
            For i = 0 To count - 1
                Dim it = _items(i)
                If it.NeedsRelayout OrElse it.CachedRect.Width <> areaWidth Then
                    LayoutItem(it, areaWidth)
                End If
                _itemLayoutTops.Add(totalHeight)
                totalHeight += it.CachedRect.Height
                If i < count - 1 Then totalHeight += itemSpacing
            Next
            _contentHeight = totalHeight
        ElseIf count = 0 Then
            _contentHeight = 0
        Else
            Dim startIndex As Integer = Math.Max(0, Math.Min(_layoutDirtyFromIndex, count - 1))
            Dim y As Integer = _itemLayoutTops(startIndex)
            For i = startIndex To count - 1
                Dim it = _items(i)
                If it.NeedsRelayout OrElse it.CachedRect.Width <> areaWidth Then
                    LayoutItem(it, areaWidth)
                End If
                _itemLayoutTops(i) = y
                y += it.CachedRect.Height
                If i < count - 1 Then y += itemSpacing
            Next
            _contentHeight = y
        End If

        _layoutAreaWidth = areaWidth
        _contentHeightDirty = False
        _layoutDirtyFromIndex = Integer.MaxValue
    End Sub

    Private Sub ClampScrollOffset(viewHeight As Integer)
        Dim maxOff As Integer = Math.Max(0, _contentHeight - viewHeight)
        If _滚动偏移 > maxOff Then _滚动偏移 = maxOff
        If _滚动偏移 < 0 Then _滚动偏移 = 0
        If _autoScrollToBottom AndAlso _pinnedToBottom Then
            _滚动偏移 = maxOff
        End If
    End Sub

    Private Function GetItemViewportRect(index As Integer, contentArea As Rectangle) As Rectangle
        If index < 0 OrElse index >= _items.Count Then Return Rectangle.Empty
        Dim top As Integer = If(index < _itemLayoutTops.Count, _itemLayoutTops(index), 0)
        Dim it = _items(index)
        Return New Rectangle(contentArea.X, contentArea.Y + top - _滚动偏移, contentArea.Width, it.CachedRect.Height)
    End Function

    Private Function FindFirstItemWithBottomAtOrAfter(contentY As Integer) As Integer
        If _items.Count = 0 OrElse _itemLayoutTops.Count <> _items.Count Then Return _items.Count

        Dim lo As Integer = 0
        Dim hi As Integer = _items.Count - 1
        Dim best As Integer = _items.Count

        While lo <= hi
            Dim mid As Integer = (lo + hi) \ 2
            Dim bottom As Integer = _itemLayoutTops(mid) + _items(mid).CachedRect.Height
            If bottom >= contentY Then
                best = mid
                hi = mid - 1
            Else
                lo = mid + 1
            End If
        End While

        Return best
    End Function

    Private Function FindLastItemWithTopAtOrBefore(contentY As Integer) As Integer
        If _items.Count = 0 OrElse _itemLayoutTops.Count <> _items.Count Then Return -1

        Dim lo As Integer = 0
        Dim hi As Integer = _itemLayoutTops.Count - 1
        Dim best As Integer = -1

        While lo <= hi
            Dim mid As Integer = (lo + hi) \ 2
            If _itemLayoutTops(mid) <= contentY Then
                best = mid
                lo = mid + 1
            Else
                hi = mid - 1
            End If
        End While

        Return best
    End Function

    Private Sub GetVisibleItemRange(contentArea As Rectangle, ByRef first As Integer, ByRef last As Integer)
        first = 0
        last = -1
        If _items.Count = 0 OrElse _itemLayoutTops.Count <> _items.Count OrElse contentArea.Height <= 0 Then Return

        Dim contentTop As Integer = _滚动偏移
        Dim contentBottom As Integer = _滚动偏移 + contentArea.Height
        first = FindFirstItemWithBottomAtOrAfter(contentTop)
        last = FindLastItemWithTopAtOrBefore(contentBottom)
        If first >= _items.Count OrElse last < first Then
            first = 0
            last = -1
        End If
    End Sub

    Private Sub LayoutItem(it As ChatItem, areaWidth As Integer)
        it.LineRanges.Clear()
        Dim font As Font = Me.Font
        Dim lineHeight As Integer = GetLineHeight(font)

        Dim isCard As Boolean = (it.Kind = ChatItemKind.Card)
        Dim pad As Padding = If(isCard, Dpi(_cardPadding), Dpi(_bubblePadding))
        Dim maxBubbleW As Integer
        If isCard Then
            maxBubbleW = Math.Max(40, CInt(areaWidth * _cardMaxWidthRatio))
        Else
            Select Case _bubbleWidthMode
                Case BubbleWidthMode.FillAvailable
                    maxBubbleW = areaWidth
                Case BubbleWidthMode.Fixed
                    maxBubbleW = Math.Min(areaWidth, Dpi(_bubbleFixedWidth))
                Case Else
                    maxBubbleW = Math.Max(40, CInt(areaWidth * _bubbleMaxWidthRatio))
            End Select
        End If

        Dim borderInset As Integer = If(isCard, Dpi(_cardBorderSize), 0)
        Dim borderExtra As Integer = borderInset * 2
        Dim contentMaxW As Integer = Math.Max(10, maxBubbleW - pad.Horizontal - borderExtra)

        If ShouldUseMarkdown(it) Then
            Dim renderer = EnsureMarkdownRenderer(it, contentMaxW)
            Dim markdownBubbleW As Integer = maxBubbleW
            Dim markdownBubbleH As Integer = Math.Max(lineHeight, renderer.ContentHeight) + pad.Vertical + borderExtra

            Dim markdownBubbleX As Integer
            If isCard Then
                markdownBubbleX = 0
            ElseIf it.Kind = ChatItemKind.UserMessage Then
                markdownBubbleX = areaWidth - markdownBubbleW
            Else
                markdownBubbleX = 0
            End If

            it.BubbleRect = New Rectangle(markdownBubbleX, 0, markdownBubbleW, markdownBubbleH)
            it.TextOriginX = markdownBubbleX + pad.Left + borderInset
            it.TextOriginY = pad.Top + borderInset
            it.CachedRect = New Rectangle(0, 0, areaWidth, markdownBubbleH)
            it.NeedsRelayout = False
            Return
        End If

        ReleaseMarkdownRenderer(it)
        it.EnsureLinksCurrent()

        ' 折行
        ChatTextHelper.WrapLines(it.Text, font, lineHeight, contentMaxW, it.LineRanges, AddressOf MeasureTextWidthCached)

        ' 计算实际气泡宽度（取最长行）
        Dim usedTextW As Integer = 0
        For Each lr In it.LineRanges
            Dim s As String = it.Text.Substring(lr.Start, lr.Length)
            Dim w As Integer = MeasureTextWidthCached(s, font, lineHeight)
            If w > usedTextW Then usedTextW = w
        Next

        Dim textBlockH As Integer = Math.Max(lineHeight, it.LineRanges.Count * lineHeight)

        Dim bubbleW, bubbleH As Integer
        If _bubbleWidthMode = BubbleWidthMode.FillAvailable OrElse (Not isCard AndAlso _bubbleWidthMode = BubbleWidthMode.Fixed) Then
            bubbleW = maxBubbleW
        Else
            bubbleW = Math.Min(maxBubbleW, usedTextW + pad.Horizontal + borderExtra)
        End If
        bubbleH = textBlockH + pad.Vertical + borderExtra

        ' 定位
        Dim bubbleX As Integer
        If isCard Then
            bubbleX = 0
        ElseIf it.Kind = ChatItemKind.UserMessage Then
            bubbleX = areaWidth - bubbleW
        Else
            bubbleX = 0
        End If

        it.BubbleRect = New Rectangle(bubbleX, 0, bubbleW, bubbleH)
        it.TextOriginX = bubbleX + pad.Left + borderInset
        it.TextOriginY = pad.Top + borderInset
        it.CachedRect = New Rectangle(0, 0, areaWidth, bubbleH)
        it.NeedsRelayout = False
    End Sub
#End Region
#Region "绘制"
    Private Shared Function GetCenteredStrokeRect(bounds As RectangleF, strokeWidth As Single) As RectangleF
        If strokeWidth <= 0 Then Return bounds

        Dim half As Single = strokeWidth / 2.0F
        Return New RectangleF(
            bounds.X + half,
            bounds.Y + half,
            Math.Max(0.0F, bounds.Width - strokeWidth),
            Math.Max(0.0F, bounds.Height - strokeWidth))
    End Function

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        EnsureLayout()
        If Me.Width < 1 OrElse Me.Height < 1 Then Return

        Dim borderSize As Integer = Dpi(_borderSize)
        Dim borderRadius As Integer = Dpi(_borderRadius)
        Dim scrollBarWidth As Integer = Dpi(_scrollBarWidth)
        Dim scaledPadding As Padding = Dpi(Me.Padding)
        Dim bgRect As RectangleF = GetCenteredStrokeRect(New RectangleF(0, 0, Width, Height), borderSize)

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(Me, _backgroundSource, New RectangleF(0, 0, Width, Height))
        End If

        Dim backColorMask As Color = MyBase.BackColor
        If backColorMask.A > 0 AndAlso backColorMask.A < 255 Then FillRoundedRect_GPU(context, bgRect, borderRadius, backColorMask)
        If _backColor1.A > 0 Then FillRoundedRect_GPU(context, bgRect, borderRadius, _backColor1)

        Dim area As Rectangle = GetContentArea()
        Dim font As Font = Me.Font
        Dim lineHeight As Integer = GetLineHeight(font)
        Dim firstVisible As Integer
        Dim lastVisible As Integer
        GetVisibleItemRange(area, firstVisible, lastVisible)

        Using context.PushClip(New RectangleF(area.Left, area.Top, area.Width, area.Height))
            For i As Integer = firstVisible To lastVisible
                Dim it = _items(i)
                Dim r As Rectangle = GetItemViewportRect(i, area)
                DrawItemShapes_GPU(context, it, r, font, lineHeight, i)
                If ShouldUseMarkdown(it) Then DrawMarkdownItem_GPU(context, it, r, area)
                DrawItemText_GPU(context, it, r, font, lineHeight)
            Next
        End Using

        Dim viewH As Integer = area.Height
        Dim totalH As Integer = Math.Max(viewH, _contentHeight)
        If _contentHeight > viewH AndAlso scrollBarWidth > 0 Then
            _scrollBar.ComputeLayout(Width, Height, borderSize, borderRadius,
                                     scaledPadding.Top, scaledPadding.Bottom,
                                     scrollBarWidth, totalH, viewH, _滚动偏移)
            DrawScrollBar_GPU(context, scrollBarWidth)
        End If

        If borderSize > 0 Then DrawRoundedBorder_GPU(context, bgRect, borderRadius, _borderColor, borderSize)
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Private Sub RequestV3Render(Optional immediate As Boolean = False)
        RequestV3Render(New Rectangle(Point.Empty, Me.Size), immediate)
    End Sub

    Private Sub RequestV3Render(dirtyRect As Rectangle, Optional immediate As Boolean = False)
        If Me.IsDisposed Then Return
        V3_InvalidationRouter.RequestRender(Me, dirtyRect)
    End Sub

    Private Sub InvalidateV3TextResources()
        D3D_RenderCore.InvalidateExistingTextResources(Me)
    End Sub

    Private Sub DrawItemShapes_GPU(context As D3D_PaintContext,
                                   it As ChatItem,
                                   areaItemRect As Rectangle,
                                   font As Font,
                                   lineHeight As Integer,
                                   itemIndex As Integer)
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

        FillRoundedRect_GPU(context, rectF, radius, backColor)
        If isCard AndAlso borderSize > 0 Then DrawRoundedBorder_GPU(context, rectF, radius, _cardBorderColor, borderSize)
        DrawSelectionForItem_GPU(context, it, itemIndex, areaItemRect, font, lineHeight)
    End Sub

    Private Sub DrawSelectionForItem_GPU(context As D3D_PaintContext,
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
        Dim textY As Integer = areaItemRect.Y + it.TextOriginY
        For li = 0 To it.LineRanges.Count - 1
            Dim lr = it.LineRanges(li)
            Dim lineS As Integer = lr.Start, lineE As Integer = lr.Start + lr.Length
            Dim a As Integer = Math.Max(selStart, lineS)
            Dim b As Integer = Math.Min(selEnd, lineE)
            If b <= a Then Continue For
            Dim line As String = it.Text.Substring(lr.Start, lr.Length)
            Dim preW As Integer = MeasureTextWidthCached(line.Substring(0, a - lineS), font, lineHeight)
            Dim segW As Integer = MeasureTextWidthCached(line.Substring(a - lineS, b - a), font, lineHeight)
            context.FillRectangle(New RectangleF(textX + preW, textY + li * lineHeight, segW, lineHeight), _selectionBackColor)
        Next
    End Sub

    Private Sub DrawItemText_GPU(context As D3D_PaintContext,
                                 it As ChatItem,
                                 areaItemRect As Rectangle,
                                 font As Font,
                                 lineHeight As Integer)
        If ShouldUseMarkdown(it) Then Return
        Dim isCard As Boolean = (it.Kind = ChatItemKind.Card)
        Dim foreColor As Color
        If isCard Then
            foreColor = _cardForeColor
        ElseIf it.Kind = ChatItemKind.UserMessage Then
            foreColor = _userBubbleForeColor
        Else
            foreColor = _assistantBubbleForeColor
        End If
        Dim textX As Integer = areaItemRect.X + it.TextOriginX
        Dim textY As Integer = areaItemRect.Y + it.TextOriginY
        For li = 0 To it.LineRanges.Count - 1
            Dim lr = it.LineRanges(li)
            Dim line As String = it.Text.Substring(lr.Start, lr.Length)
            DrawLineWithLinks_GPU(context, it, line, lr.Start, textX, textY + li * lineHeight, font, lineHeight, foreColor)
        Next
    End Sub

    Private Sub DrawMarkdownItem_GPU(context As D3D_PaintContext,
                                     it As ChatItem,
                                     areaItemRect As Rectangle,
                                     viewportRect As Rectangle)
        Dim renderer = it.MarkdownRenderer
        If renderer Is Nothing OrElse renderer.IsDisposed Then Return

        Dim origin As New Point(areaItemRect.X + it.TextOriginX, areaItemRect.Y + it.TextOriginY)
        Dim visibleLocalTop As Integer = Math.Max(0, viewportRect.Top - origin.Y)
        Dim visibleLocalBottom As Integer = Math.Min(renderer.ContentHeight, viewportRect.Bottom - origin.Y)
        If visibleLocalBottom <= visibleLocalTop Then Return

        Dim clipSize As New Size(Math.Max(1, renderer.Width), Math.Max(1, renderer.ContentHeight))
        renderer.DrawEmbeddedContent_GPU(context, origin, clipSize, drawBackground:=False,
                                         visibleLocalTop:=visibleLocalTop,
                                         visibleLocalBottom:=visibleLocalBottom)
    End Sub

    Private Sub DrawLineWithLinks_GPU(context As D3D_PaintContext,
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
                    DrawTextSegment_GPU(context, line, p - lineStartCharIndex, link.Value.Start - p,
                                        Nothing, curX, y, font, lineHeight, normalColor)
                End If
                Dim segE As Integer = Math.Min(lineEnd, link.Value.Start + link.Value.Length)
                Dim segStart As Integer = Math.Max(p, link.Value.Start)
                DrawTextSegment_GPU(context, line, segStart - lineStartCharIndex, segE - segStart,
                                    link.Value.Url, curX, y, font, lineHeight, normalColor)
                p = segE
            Else
                DrawTextSegment_GPU(context, line, p - lineStartCharIndex, lineEnd - p,
                                    Nothing, curX, y, font, lineHeight, normalColor)
                p = lineEnd
            End If
        End While
    End Sub

    Private Sub DrawTextSegment_GPU(context As D3D_PaintContext,
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
            context.DrawText(part, font, col, New RectangleF(curX, y, w, lineHeight), Vortice.DirectWrite.TextAlignment.Leading, Vortice.DirectWrite.ParagraphAlignment.Center)
            If _linkUnderline Then
                Dim br = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, col, context.DeviceGeneration)
                Dim underlineY As Integer = y + lineHeight - Dpi(_linkUnderlineOffset)
                context.DeviceContext.DrawLine(New Vector2(curX, underlineY),
                                               New Vector2(curX + w, underlineY),
                                               br,
                                               Math.Max(1.0F, Dpi(_linkUnderlineThickness)))
            End If
        Else
            context.DrawText(part, font, normalColor, New RectangleF(curX, y, w, lineHeight), Vortice.DirectWrite.TextAlignment.Leading, Vortice.DirectWrite.ParagraphAlignment.Center)
        End If
        curX += w
    End Sub

    Private Sub DrawScrollBar_GPU(context As D3D_PaintContext, scrollBarWidth As Integer)
        If _scrollBar.TrackRect.IsEmpty Then Return
        Dim width As Single = Math.Max(1.0F, scrollBarWidth)
        Dim trackArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.TrackRect.Y, width, _scrollBar.TrackRect.Height)
        Dim thumbArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.ThumbRect.Y, width, _scrollBar.ThumbRect.Height)
        FillRoundedRect_GPU(context, trackArea, Math.Min(width / 2.0F, trackArea.Height / 2.0F), _scrollBarTrackColor)
        Dim thumbColor = If(_scrollBar.IsDragging OrElse _scrollBar.IsHover, _scrollBarThumbHoverColor, _scrollBarThumbColor)
        FillRoundedRect_GPU(context, thumbArea, Math.Min(width / 2.0F, thumbArea.Height / 2.0F), thumbColor)
    End Sub

    Private Sub FillRoundedRect_GPU(context As D3D_PaintContext, rect As RectangleF, radius As Single, color As Color)
        If color.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If radius <= 0 Then
            context.DeviceContext.FillRectangle(D3D_PaintContext.ToRawRect(rect), brush)
            Return
        End If
        context.FillRoundedRectangle(rect, radius, brush)
    End Sub

    Private Sub DrawRoundedBorder_GPU(context As D3D_PaintContext, rect As RectangleF, radius As Single, color As Color, strokeWidth As Single)
        If color.A = 0 OrElse strokeWidth <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If radius <= 0 Then
            context.DeviceContext.DrawRectangle(D3D_PaintContext.ToRawRect(rect), brush, strokeWidth)
            Return
        End If
        context.DrawRoundedRectangle(rect, radius, brush, strokeWidth)
    End Sub

    Private Sub DrawItemShapes_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget,
                                    brushCache As D3D_D2DInterop.SolidColorBrushCache,
                                    it As ChatItem, areaItemRect As Rectangle,
                                    font As Font, lineHeight As Integer, itemIndex As Integer)
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
        If radius > 0 Then
            Using geo = D3D_RectangleRenderer.创建圆角矩形几何(rectF, radius)
                D3D_RectangleRenderer.绘制圆角背景_D2D(rt, geo, rectF, backColor, Color.Empty, 0, brushCache)
                If isCard AndAlso borderSize > 0 Then
                    D3D_RectangleRenderer.绘制圆角边框_D2D(rt, geo, _cardBorderColor, borderSize, brushCache)
                End If
            End Using
        Else
            D3D_RectangleRenderer.绘制矩形背景_D2D(rt, rectF, backColor, Color.Empty, 0, brushCache)
            If isCard AndAlso borderSize > 0 Then
                D3D_RectangleRenderer.绘制矩形边框_D2D(rt, rectF, _cardBorderColor, borderSize, brushCache)
            End If
        End If

        ' 选区背景
        DrawSelectionForItem_D2D(rt, brushCache, it, itemIndex, areaItemRect, font, lineHeight)
    End Sub

    Private Sub DrawSelectionForItem_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget,
                                          brushCache As D3D_D2DInterop.SolidColorBrushCache,
                                          it As ChatItem, itemIndex As Integer,
                                          areaItemRect As Rectangle, font As Font, lineHeight As Integer)
        If ShouldUseMarkdown(it) Then Return
        If Not _hasSelection Then Return
        Dim rangeStart, rangeEnd As TextPos
        GetNormalizedSelection(rangeStart, rangeEnd)
        If itemIndex < rangeStart.ItemIndex OrElse itemIndex > rangeEnd.ItemIndex Then Return
        Dim selStart As Integer = If(itemIndex = rangeStart.ItemIndex, rangeStart.CharIndex, 0)
        Dim selEnd As Integer = If(itemIndex = rangeEnd.ItemIndex, rangeEnd.CharIndex, it.Text.Length)
        If selEnd <= selStart Then Return
        Dim textX As Integer = areaItemRect.X + it.TextOriginX
        Dim textY As Integer = areaItemRect.Y + it.TextOriginY
        Dim selBr = brushCache.Get(rt, _selectionBackColor)
        For li = 0 To it.LineRanges.Count - 1
            Dim lr = it.LineRanges(li)
            Dim lineS As Integer = lr.Start, lineE As Integer = lr.Start + lr.Length
            Dim a As Integer = Math.Max(selStart, lineS)
            Dim b As Integer = Math.Min(selEnd, lineE)
            If b <= a Then Continue For
            Dim line As String = it.Text.Substring(lr.Start, lr.Length)
            Dim preW As Integer = MeasureTextWidthCached(line.Substring(0, a - lineS), font, lineHeight)
            Dim segW As Integer = MeasureTextWidthCached(line.Substring(a - lineS, b - a), font, lineHeight)
            rt.FillRectangle(New Vortice.RawRectF(textX + preW, textY + li * lineHeight, textX + preW + segW, textY + li * lineHeight + lineHeight), selBr)
        Next
    End Sub

    Private Sub DrawItemText_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget,
                                  brushCache As D3D_D2DInterop.SolidColorBrushCache,
                                  tfc As D3D_D2DInterop.TextFormatCache, dpiS As Single,
                                  it As ChatItem, areaItemRect As Rectangle, font As Font, lineHeight As Integer)
        If ShouldUseMarkdown(it) Then Return
        Dim isCard As Boolean = (it.Kind = ChatItemKind.Card)
        Dim foreColor As Color
        If isCard Then
            foreColor = _cardForeColor
        ElseIf it.Kind = ChatItemKind.UserMessage Then
            foreColor = _userBubbleForeColor
        Else
            foreColor = _assistantBubbleForeColor
        End If
        Dim textX As Integer = areaItemRect.X + it.TextOriginX
        Dim textY As Integer = areaItemRect.Y + it.TextOriginY
        For li = 0 To it.LineRanges.Count - 1
            Dim lr = it.LineRanges(li)
            Dim line As String = it.Text.Substring(lr.Start, lr.Length)
            DrawLineWithLinks_D2D(rt, brushCache, tfc, dpiS, it, line, lr.Start,
                                   textX, textY + li * lineHeight, font, lineHeight, foreColor)
        Next
    End Sub


    Private Sub DrawLineWithLinks_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget,
                                       brushCache As D3D_D2DInterop.SolidColorBrushCache,
                                       tfc As D3D_D2DInterop.TextFormatCache, dpiS As Single,
                                       it As ChatItem, line As String, lineStartCharIndex As Integer,
                                       x As Integer, y As Integer, font As Font, lineHeight As Integer,
                                       normalColor As Color)
        If String.IsNullOrEmpty(line) Then Return
        Dim curX As Integer = x
        Dim lineEnd As Integer = lineStartCharIndex + line.Length
        Dim flags As TextFormatFlags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
        Dim p As Integer = lineStartCharIndex
        While p < lineEnd
            Dim link = FindLinkAt(it, p)
            If link.HasValue AndAlso link.Value.Start < lineEnd Then
                If link.Value.Start > p Then
                    DrawTextSegment_D2D(rt, brushCache, tfc, dpiS, line,
                                        p - lineStartCharIndex, link.Value.Start - p,
                                        Nothing, curX, y, font, lineHeight, normalColor, flags)
                End If
                Dim segE As Integer = Math.Min(lineEnd, link.Value.Start + link.Value.Length)
                Dim segStart As Integer = Math.Max(p, link.Value.Start)
                DrawTextSegment_D2D(rt, brushCache, tfc, dpiS, line,
                                    segStart - lineStartCharIndex, segE - segStart,
                                    link.Value.Url, curX, y, font, lineHeight, normalColor, flags)
                p = segE
            Else
                DrawTextSegment_D2D(rt, brushCache, tfc, dpiS, line,
                                    p - lineStartCharIndex, lineEnd - p,
                                    Nothing, curX, y, font, lineHeight, normalColor, flags)
                p = lineEnd
            End If
        End While
    End Sub

    Private Sub DrawTextSegment_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget,
                                    brushCache As D3D_D2DInterop.SolidColorBrushCache,
                                    tfc As D3D_D2DInterop.TextFormatCache, dpiS As Single,
                                    line As String, startIndex As Integer, length As Integer, url As String,
                                    ByRef curX As Integer, y As Integer, font As Font, lineHeight As Integer,
                                    normalColor As Color, flags As TextFormatFlags)
        If length <= 0 Then Return
        Dim part As String = line.Substring(startIndex, length)
        Dim w As Integer = MeasureTextWidthCached(part, font, lineHeight)
        If url IsNot Nothing Then
            Dim col As Color = If(url = _hoverLinkUrl, _linkHoverColor, _linkColor)
            D3D_TextInterop.DrawText(rt, part, font, New Rectangle(curX, y, w, lineHeight), col, flags, dpiS, tfc, brushCache)
            If _linkUnderline Then
                Dim br = brushCache.Get(rt, col)
                Dim underlineY As Integer = y + lineHeight - Dpi(_linkUnderlineOffset)
                rt.DrawLine(New System.Numerics.Vector2(curX, underlineY),
                                New System.Numerics.Vector2(curX + w, underlineY), br, Math.Max(1.0F, Dpi(_linkUnderlineThickness)))
            End If
        Else
            D3D_TextInterop.DrawText(rt, part, font, New Rectangle(curX, y, w, lineHeight), normalColor, flags, dpiS, tfc, brushCache)
        End If
        curX += w
    End Sub

    Private Function FindLinkAt(it As ChatItem, charIndex As Integer) As LinkSpan?
        it?.EnsureLinksCurrent()
        Dim nearest As LinkSpan? = Nothing
        For Each ls In it.LinkSpans
            If charIndex >= ls.Start AndAlso charIndex < ls.Start + ls.Length Then
                Return ls
            End If
            If ls.Start > charIndex Then
                If Not nearest.HasValue OrElse ls.Start < nearest.Value.Start Then
                    nearest = ls
                End If
            End If
        Next
        Return nearest
    End Function
#End Region
#Region "选区与点击"
    Private Sub GetNormalizedSelection(ByRef a As TextPos, ByRef b As TextPos)
        a = _selAnchor : b = _selCaret
        If a.ItemIndex > b.ItemIndex OrElse (a.ItemIndex = b.ItemIndex AndAlso a.CharIndex > b.CharIndex) Then
            Dim t = a : a = b : b = t
        End If
    End Sub

    Public Sub ClearSelection()
        Dim hadSelection As Boolean = _hasSelection OrElse HasAnyMarkdownSelection()
        _hasSelection = False
        _mouseSelecting = False
        _mouseSelectingMarkdown = False
        _selAnchor = New TextPos(-1, 0)
        _selCaret = New TextPos(-1, 0)
        ClearAllMarkdownSelections()
        If hadSelection Then RequestViewportRefresh()
    End Sub

    Public Sub SelectAllText()
        If _activeMarkdownRenderer IsNot Nothing AndAlso Not _activeMarkdownRenderer.IsDisposed Then
            _hasSelection = False
            _mouseSelecting = False
            ClearOtherMarkdownSelections(_activeMarkdownRenderer)
            _activeMarkdownRenderer.SelectAllEmbeddedText()
            Return
        End If

        If _items.Count = 0 Then Return
        ClearAllMarkdownSelections()
        _selAnchor = New TextPos(0, 0)
        Dim last = _items(_items.Count - 1)
        _selCaret = New TextPos(_items.Count - 1, last.Text.Length)
        _hasSelection = True
        RequestViewportRefresh()
    End Sub

    Public Function GetSelectedText() As String
        Dim markdownSelection = GetSelectedMarkdownRenderer()
        If markdownSelection IsNot Nothing Then Return markdownSelection.GetSelectedText()

        If Not _hasSelection Then Return ""
        Dim a, b As TextPos
        GetNormalizedSelection(a, b)
        If a.ItemIndex = b.ItemIndex Then
            Dim it = _items(a.ItemIndex)
            Return it.Text.Substring(a.CharIndex, Math.Max(0, b.CharIndex - a.CharIndex))
        End If
        Dim sb As New System.Text.StringBuilder()
        For i = a.ItemIndex To b.ItemIndex
            Dim it = _items(i)
            Dim s As Integer = If(i = a.ItemIndex, a.CharIndex, 0)
            Dim e As Integer = If(i = b.ItemIndex, b.CharIndex, it.Text.Length)
            sb.Append(it.Text.AsSpan(s, Math.Max(0, e - s)))
            If i < b.ItemIndex Then sb.AppendLine()
        Next
        Return sb.ToString()
    End Function

    Public Sub CopySelectionToClipboard()
        Dim s As String = GetSelectedText()
        If Not String.IsNullOrEmpty(s) Then
            Try
                Clipboard.SetText(s)
            Catch
            End Try
        End If
    End Sub

    Private Function HitTestMarkdown(pt As Point) As MarkdownHitInfo
        EnsureLayout()
        Dim area As Rectangle = GetContentArea()
        Dim firstVisible As Integer
        Dim lastVisible As Integer
        GetVisibleItemRange(area, firstVisible, lastVisible)

        For i = firstVisible To lastVisible
            Dim it = _items(i)
            If Not ShouldUseMarkdown(it) Then Continue For
            Dim md = it.MarkdownRenderer
            If md Is Nothing OrElse md.IsDisposed Then Continue For

            Dim r = GetItemViewportRect(i, area)
            If pt.Y < r.Top OrElse pt.Y > r.Bottom Then Continue For

            Dim textX As Integer = r.X + it.TextOriginX
            Dim textY As Integer = r.Y + it.TextOriginY
            Dim localX As Integer = pt.X - textX
            Dim localY As Integer = pt.Y - textY
            If localX < 0 OrElse localY < 0 OrElse localX >= md.Width OrElse localY >= md.ContentHeight Then Continue For

            Return New MarkdownHitInfo With {
                .Renderer = md,
                .LocalPoint = New Point(localX, localY),
                .ItemIndex = i
            }
        Next

        Return New MarkdownHitInfo()
    End Function

    Private Function TryGetMarkdownLocalPoint(renderer As MarkdownViewerCore,
                                             pt As Point,
                                             ByRef localPoint As Point) As Boolean
        localPoint = Point.Empty
        If renderer Is Nothing OrElse renderer.IsDisposed Then Return False

        EnsureLayout()
        Dim area As Rectangle = GetContentArea()
        If _activeMarkdownItemIndex >= 0 AndAlso _activeMarkdownItemIndex < _items.Count Then
            Dim activeItem = _items(_activeMarkdownItemIndex)
            If Object.ReferenceEquals(activeItem.MarkdownRenderer, renderer) Then
                Dim activeRect = GetItemViewportRect(_activeMarkdownItemIndex, area)
                localPoint = New Point(pt.X - (activeRect.X + activeItem.TextOriginX), pt.Y - (activeRect.Y + activeItem.TextOriginY))
                Return True
            End If
        End If

        For Each it In _items
            If Not Object.ReferenceEquals(it.MarkdownRenderer, renderer) Then Continue For
            Dim itemIndex As Integer = GetItemIndex(it)
            If itemIndex < 0 Then Return False
            Dim r = GetItemViewportRect(itemIndex, area)
            localPoint = New Point(pt.X - (r.X + it.TextOriginX), pt.Y - (r.Y + it.TextOriginY))
            Return True
        Next

        Return False
    End Function

    Private Sub ClearAllMarkdownSelections()
        For Each it In _items
            Dim renderer = it.MarkdownRenderer
            If renderer IsNot Nothing AndAlso Not renderer.IsDisposed Then renderer.ClearEmbeddedSelection()
        Next
        _activeMarkdownRenderer = Nothing
        _activeMarkdownItemIndex = -1
        _mouseSelectingMarkdown = False
    End Sub

    Private Sub ClearOtherMarkdownSelections(activeRenderer As MarkdownViewerCore)
        For Each it In _items
            Dim renderer = it.MarkdownRenderer
            If renderer Is Nothing OrElse renderer.IsDisposed Then Continue For
            If Not Object.ReferenceEquals(renderer, activeRenderer) Then renderer.ClearEmbeddedSelection()
        Next
    End Sub

    Private Function HasAnyMarkdownSelection() As Boolean
        Return GetSelectedMarkdownRenderer() IsNot Nothing
    End Function

    Private Function GetSelectedMarkdownRenderer() As MarkdownViewerCore
        If _activeMarkdownRenderer IsNot Nothing AndAlso
           Not _activeMarkdownRenderer.IsDisposed AndAlso
           _activeMarkdownRenderer.HasEmbeddedSelection Then
            Return _activeMarkdownRenderer
        End If

        For i As Integer = 0 To _items.Count - 1
            Dim it = _items(i)
            Dim renderer = it.MarkdownRenderer
            If renderer IsNot Nothing AndAlso
               Not renderer.IsDisposed AndAlso
               renderer.HasEmbeddedSelection Then
                _activeMarkdownRenderer = renderer
                _activeMarkdownItemIndex = i
                Return renderer
            End If
        Next

        _activeMarkdownItemIndex = -1
        Return Nothing
    End Function

    Private Sub ActivateLink(url As String, itemIndex As Integer)
        If String.IsNullOrEmpty(url) Then Return
        RaiseEvent LinkClicked(Me, New LinkClickedEventArgs With {.Url = url, .ItemIndex = itemIndex})
    End Sub

    Private Sub StoreMouseDownLink(link As LinkHitInfo)
        _mouseDownLinkUrl = link.Url
        _mouseDownLinkItemIndex = If(link.HasValue, link.ItemIndex, -1)
    End Sub

    Private Sub ClearMouseDownLink()
        _mouseDownLinkUrl = Nothing
        _mouseDownLinkItemIndex = -1
    End Sub

    Private Sub BeginMarkdownSelection(hit As MarkdownHitInfo)
        _hasSelection = False
        _mouseSelecting = False
        _selAnchor = New TextPos(-1, 0)
        _selCaret = New TextPos(-1, 0)
        ClearOtherMarkdownSelections(hit.Renderer)
        _activeMarkdownRenderer = hit.Renderer
        _activeMarkdownItemIndex = hit.ItemIndex
        _mouseSelectingMarkdown = True
        hit.Renderer.BeginEmbeddedSelection(hit.LocalPoint.X, hit.LocalPoint.Y)
        Capture = True
        RequestViewportRefresh()
    End Sub

    Private Sub BeginPlainTextSelection(pos As TextPos)
        ClearAllMarkdownSelections()
        _selAnchor = pos
        _selCaret = pos
        _hasSelection = False
        _mouseSelecting = True
        Capture = True
        RequestViewportRefresh()
    End Sub

    Private Sub UpdateMarkdownSelection(pt As Point)
        Dim local As Point = Point.Empty
        If TryGetMarkdownLocalPoint(_activeMarkdownRenderer, pt, local) Then
            _activeMarkdownRenderer.UpdateEmbeddedSelection(local.X, local.Y)
        End If
    End Sub

    Private Function EndMarkdownSelection() As Boolean
        Dim selectedMarkdown = _activeMarkdownRenderer
        Dim hadSelection As Boolean = selectedMarkdown IsNot Nothing AndAlso
                                      Not selectedMarkdown.IsDisposed AndAlso
                                      selectedMarkdown.HasEmbeddedSelection
        If selectedMarkdown IsNot Nothing AndAlso Not selectedMarkdown.IsDisposed Then selectedMarkdown.EndEmbeddedSelection()
        _mouseSelectingMarkdown = False
        Capture = False
        Return hadSelection
    End Function

    Private Sub ActivateMouseDownLinkIfClick(pt As Point, hadSelection As Boolean)
        If hadSelection OrElse String.IsNullOrEmpty(_mouseDownLinkUrl) Then
            ClearMouseDownLink()
            Return
        End If

        Dim link = HitTestLink(pt)
        If link.HasValue AndAlso
           link.ItemIndex = _mouseDownLinkItemIndex AndAlso
           String.Equals(link.Url, _mouseDownLinkUrl, StringComparison.Ordinal) Then
            ActivateLink(_mouseDownLinkUrl, _mouseDownLinkItemIndex)
        End If
        ClearMouseDownLink()
    End Sub

    Private Function HitTestText(pt As Point, ByRef result As TextPos, Optional snap As Boolean = True) As Boolean
        result = New TextPos(-1, 0)
        EnsureLayout()
        If _items.Count = 0 Then Return False

        Dim area As Rectangle = GetContentArea()
        Dim font As Font = Me.Font
        Dim lineHeight As Integer = GetLineHeight(font)
        Dim firstIndex As Integer
        Dim lastIndex As Integer

        If snap Then
            Dim contentY As Integer = pt.Y - area.Y + _滚动偏移
            firstIndex = FindFirstItemWithBottomAtOrAfter(contentY)
            If firstIndex >= _items.Count Then
                Dim last = _items(_items.Count - 1)
                result = New TextPos(_items.Count - 1, last.Text.Length)
                Return True
            End If
            lastIndex = _items.Count - 1
        Else
            GetVisibleItemRange(area, firstIndex, lastIndex)
        End If

        For i = firstIndex To lastIndex
            Dim it = _items(i)
            If ShouldUseMarkdown(it) Then Continue For
            Dim r = GetItemViewportRect(i, area)
            If pt.Y < r.Top Then
                If snap Then
                    result = New TextPos(i, 0) : Return True
                End If
                Continue For
            End If
            If pt.Y > r.Bottom Then Continue For
            ' 在该项垂直范围内
            Dim textX As Integer = r.X + it.TextOriginX
            Dim textY As Integer = r.Y + it.TextOriginY
            Dim relY As Integer = pt.Y - textY
            If it.LineRanges.Count = 0 Then
                result = New TextPos(i, 0) : Return True
            End If
            Dim li As Integer = Math.Max(0, Math.Min(it.LineRanges.Count - 1, relY \ Math.Max(1, lineHeight)))
            Dim lr = it.LineRanges(li)
            Dim line As String = it.Text.Substring(lr.Start, lr.Length)
            Dim col As Integer = D3D_TextMeasureHelper.FindColFromX_D2D(line, pt.X - textX, font, DpiScale(), GetTextFormatCacheForMeasure())
            result = New TextPos(i, lr.Start + col)
            Return True
        Next
        If snap AndAlso _items.Count > 0 Then
            Dim last = _items(_items.Count - 1)
            result = New TextPos(_items.Count - 1, last.Text.Length)
            Return True
        End If
        Return False
    End Function

    Private Function HitTestLink(pt As Point) As LinkHitInfo
        EnsureLayout()
        Dim area As Rectangle = GetContentArea()
        Dim firstVisible As Integer
        Dim lastVisible As Integer
        GetVisibleItemRange(area, firstVisible, lastVisible)

        Dim font As Font = Me.Font
        Dim lineHeight As Integer = GetLineHeight(font)
        For i = firstVisible To lastVisible
            Dim it = _items(i)
            Dim r = GetItemViewportRect(i, area)
            If pt.Y < r.Top OrElse pt.Y > r.Bottom Then Continue For
            Dim textX As Integer = r.X + it.TextOriginX
            Dim textY As Integer = r.Y + it.TextOriginY
            If ShouldUseMarkdown(it) Then
                Dim renderer = it.MarkdownRenderer
                If renderer Is Nothing OrElse renderer.IsDisposed Then Continue For
                Dim localX As Integer = pt.X - textX
                Dim localY As Integer = pt.Y - textY
                If localX < 0 OrElse localY < 0 OrElse localX >= renderer.Width OrElse localY >= renderer.ContentHeight Then Continue For
                Dim markdownUrl = renderer.HitTestEmbeddedLink(localX, localY)
                If markdownUrl IsNot Nothing Then Return New LinkHitInfo With {.Url = markdownUrl, .ItemIndex = i}
                Continue For
            End If
            Dim relY As Integer = pt.Y - textY
            If relY < 0 Then Continue For
            Dim li As Integer = relY \ Math.Max(1, lineHeight)
            If li < 0 OrElse li >= it.LineRanges.Count Then Continue For
            Dim lr = it.LineRanges(li)
            Dim line As String = it.Text.Substring(lr.Start, lr.Length)
            Dim col As Integer = D3D_TextMeasureHelper.FindColFromX_D2D(line, pt.X - textX, font, DpiScale(), GetTextFormatCacheForMeasure())
            Dim absCharIndex As Integer = lr.Start + col
            it.EnsureLinksCurrent()
            For Each ls In it.LinkSpans
                If absCharIndex >= ls.Start AndAlso absCharIndex < ls.Start + ls.Length Then
                    Return New LinkHitInfo With {.Url = ls.Url, .ItemIndex = i}
                End If
            Next
        Next
        Return New LinkHitInfo()
    End Function
#End Region
#Region "鼠标与键盘"
    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        Me.Focus()
        EnsureLayout()

        If e.Button = MouseButtons.Left Then
            ' 滚动条优先
            If _scrollBar.BeginDrag(e.Location, _滚动偏移) Then
                Capture = True
                RequestViewportRefresh()
                Return
            End If
            If _scrollBar.TrackRect.Contains(e.Location) Then
                Dim viewH = ContentViewportHeight()
                Dim totalH = Math.Max(viewH, _contentHeight)
                SetScrollOffset(_scrollBar.TrackClick(e.Location, _滚动偏移, totalH, viewH))
                Return
            End If

            ' 链接点击
            StoreMouseDownLink(HitTestLink(e.Location))

            If _selectableText Then
                Dim mdHit = HitTestMarkdown(e.Location)
                If mdHit.HasValue Then
                    BeginMarkdownSelection(mdHit)
                    Return
                End If

                Dim pos As TextPos
                If HitTestText(e.Location, pos, snap:=True) Then
                    BeginPlainTextSelection(pos)
                    Return
                End If
            End If

            If Not String.IsNullOrEmpty(_mouseDownLinkUrl) Then
                ActivateLink(_mouseDownLinkUrl, _mouseDownLinkItemIndex)
                ClearMouseDownLink()
                Return
            End If
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If _scrollBar.IsDragging Then
            Dim viewH = ContentViewportHeight()
            Dim totalH = Math.Max(viewH, _contentHeight)
            SetScrollOffset(_scrollBar.DragMove(e.Y, totalH, viewH))
            Return
        End If

        If _mouseSelectingMarkdown Then
            UpdateMarkdownSelection(e.Location)
            AutoScrollSelectionAtEdge(e.Y)
            Return
        End If

        If _mouseSelecting Then
            Dim pos As TextPos
            If HitTestText(e.Location, pos, snap:=True) Then
                If pos <> _selCaret Then
                    _selCaret = pos
                    _hasSelection = (_selAnchor <> _selCaret)
                    RequestViewportRefresh()
                End If
            End If
            ' 边缘自动滚动
            AutoScrollSelectionAtEdge(e.Y)
            Return
        End If

        ' 悬停光标
        Dim link = HitTestLink(e.Location)
        If Not String.Equals(link.Url, _hoverLinkUrl, StringComparison.Ordinal) Then
            _hoverLinkUrl = link.Url
            RequestViewportRefresh()
        End If
        Dim desiredCursor As Cursor
        If link.HasValue Then
            desiredCursor = Cursors.Hand
        ElseIf _selectableText AndAlso GetContentArea().Contains(e.Location) Then
            desiredCursor = Cursors.IBeam
        Else
            desiredCursor = Cursors.Default
        End If
        If Cursor IsNot desiredCursor Then Cursor = desiredCursor

        If _scrollBar.UpdateHover(e.Location) Then RequestViewportRefresh()
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If _scrollBar.IsDragging Then
            _scrollBar.EndDrag()
            Capture = False
            RequestViewportRefresh()
        End If
        If _mouseSelectingMarkdown Then
            Dim hadSelection As Boolean = EndMarkdownSelection()
            If e.Button = MouseButtons.Left Then ActivateMouseDownLinkIfClick(e.Location, hadSelection)
            Return
        End If

        If _mouseSelecting Then
            Dim hadSelection As Boolean = _hasSelection
            _mouseSelecting = False
            Capture = False

            If e.Button = MouseButtons.Left Then ActivateMouseDownLinkIfClick(e.Location, hadSelection)
        End If

        If e.Button = MouseButtons.Right AndAlso _showCopyContextMenu Then
            Dim mdHit = HitTestMarkdown(e.Location)
            If mdHit.HasValue Then
                _activeMarkdownRenderer = mdHit.Renderer
                _activeMarkdownItemIndex = mdHit.ItemIndex
            End If
            _copyContextMenu.Show(Me, e.Location)
        End If
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If _hoverLinkUrl IsNot Nothing Then
            _hoverLinkUrl = Nothing
            RequestViewportRefresh()
        End If
        If _scrollBar.ResetHover() Then RequestViewportRefresh()
        Cursor = Cursors.Default
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        ScrollByPixels(-Math.Sign(e.Delta) * Dpi(_wheelStep))
    End Sub

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        RequestV3Render()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        InvalidateAllItemsLayout()
        InvalidateMarkdownRenderers()
        InvalidateV3TextResources()
        RequestV3Render()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        InvalidateMeasureCache()
        InvalidateMarkdownRenderers()
        InvalidateAllItemsLayout()
        InvalidateV3TextResources()
        RequestV3Render()
    End Sub

    Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
        Select Case keyData
            Case Keys.Up, Keys.Down, Keys.PageUp, Keys.PageDown, Keys.Home, Keys.End
                Return True
        End Select
        Return MyBase.IsInputKey(keyData)
    End Function

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If e.Control AndAlso e.KeyCode = Keys.C Then
            CopySelectionToClipboard() : e.Handled = True : Return
        End If
        If e.Control AndAlso e.KeyCode = Keys.A Then
            SelectAllText() : e.Handled = True : Return
        End If
        Dim wheelStep As Integer = Dpi(_wheelStep)
        Select Case e.KeyCode
            Case Keys.Up
                SetScrollOffset(_滚动偏移 - wheelStep)
            Case Keys.Down
                SetScrollOffset(_滚动偏移 + wheelStep)
            Case Keys.PageUp
                SetScrollOffset(_滚动偏移 - ContentViewportHeight())
            Case Keys.PageDown
                SetScrollOffset(_滚动偏移 + ContentViewportHeight())
            Case Keys.Home
                SetScrollOffset(0)
            Case Keys.End
                EnsureLayout()
                SetScrollOffset(GetMaxScrollOffset())
        End Select
    End Sub
#End Region
    '__APPEND_POINT__
End Class

Friend Structure LineRange
    Public Start As Integer
    Public Length As Integer
    Public Sub New(s As Integer, l As Integer)
        Start = s : Length = l
    End Sub
End Structure

Friend Structure LinkSpan
    Public Start As Integer
    Public Length As Integer
    Public Url As String
    Public Sub New(s As Integer, l As Integer, u As String)
        Start = s : Length = l : Url = u
    End Sub
End Structure

Friend Module ChatTextHelper
    Private ReadOnly _linkRegex As New Regex("https?://[^\s\u4e00-\u9fff]+|www\.[^\s\u4e00-\u9fff]+",
                                              RegexOptions.IgnoreCase Or RegexOptions.Compiled)

    Public Sub RescanLinks(text As String, target As List(Of LinkSpan))
        target.Clear()
        If String.IsNullOrEmpty(text) Then Return
        For Each m As Match In _linkRegex.Matches(text)
            Dim url As String = m.Value
            If url.StartsWith("www.", StringComparison.OrdinalIgnoreCase) Then url = "http://" & url
            target.Add(New LinkSpan(m.Index, m.Length, url))
        Next
    End Sub

    ''' <summary>把文本按 maxWidth 折行（按字符），保留换行符。</summary>
    Public Sub WrapLines(text As String, font As Font, lineHeight As Integer, maxWidth As Integer,
                         output As List(Of LineRange),
                         Optional measureWidth As Func(Of String, Font, Integer, Integer) = Nothing)
        output.Clear()
        If text Is Nothing Then text = ""
        Dim n As Integer = text.Length
        If n = 0 Then
            output.Add(New LineRange(0, 0))
            Return
        End If

        Dim i As Integer = 0
        While i < n
            ' 找到下一个换行
            Dim eol As Integer = text.IndexOf(ControlChars.Lf, i)
            Dim segEnd As Integer = If(eol < 0, n, eol)
            ' 在 [i, segEnd) 内按宽度分段
            Dim p As Integer = i
            While p < segEnd
                Dim fitLen As Integer = FitLength(text, p, segEnd, font, lineHeight, maxWidth, measureWidth)
                If fitLen <= 0 Then fitLen = 1
                ' 优先在空白处断行
                If p + fitLen < segEnd Then
                    Dim breakAt As Integer = -1
                    For k = p + fitLen - 1 To p + 1 Step -1
                        Dim ch = text(k)
                        If ch = " "c OrElse ch = ControlChars.Tab OrElse ch = "/"c OrElse ch = "-"c Then
                            breakAt = k + 1
                            Exit For
                        End If
                    Next
                    If breakAt > p AndAlso breakAt - p <= fitLen Then
                        fitLen = breakAt - p
                    End If
                End If
                output.Add(New LineRange(p, fitLen))
                p += fitLen
            End While
            If segEnd = i Then
                output.Add(New LineRange(i, 0))
            End If
            If eol < 0 Then
                i = n
            Else
                i = eol + 1
                If i = n Then output.Add(New LineRange(n, 0))
            End If
        End While

        If output.Count = 0 Then output.Add(New LineRange(0, 0))
    End Sub

    Private Function FitLength(text As String, start As Integer, [end] As Integer,
                               font As Font, lineHeight As Integer, maxWidth As Integer,
                               measureWidth As Func(Of String, Font, Integer, Integer)) As Integer
        Dim total As Integer = [end] - start
        If total <= 0 Then Return 0
        Dim full As String = text.Substring(start, total)
        If InvokeMeasureWidth(full, font, lineHeight, measureWidth) <= maxWidth Then Return total
        Dim lo As Integer = 1, hi As Integer = total
        While lo < hi
            Dim mid As Integer = (lo + hi + 1) \ 2
            If InvokeMeasureWidth(text.Substring(start, mid), font, lineHeight, measureWidth) <= maxWidth Then
                lo = mid
            Else
                hi = mid - 1
            End If
        End While
        Return lo
    End Function

    Private Function InvokeMeasureWidth(text As String, font As Font, lineHeight As Integer,
                                        measureWidth As Func(Of String, Font, Integer, Integer)) As Integer
        If measureWidth IsNot Nothing Then Return measureWidth(text, font, lineHeight)
        Return D3D_TextMeasureHelper.MeasureTextWidth(text, font, lineHeight)
    End Function
End Module
