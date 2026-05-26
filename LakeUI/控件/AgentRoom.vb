Imports System.ComponentModel
Imports System.Drawing.Drawing2D
Imports System.Text.RegularExpressions

''' <summary>
''' AI 聊天室控件：仅保留消息显示区，支持气泡消息与卡片消息、文字选取/复制、链接点击。
''' 数据通过 Items 集合或 AddXxx/Clear 方法操作；不内置输入框、按钮、线程切换。
''' </summary>
<DefaultEvent("LinkClicked")>
Public Class AgentRoom
    Inherits Control

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
        Friend NeedsRelayout As Boolean = True
        Friend CachedRect As Rectangle
        Friend ReadOnly LineRanges As New List(Of LineRange)
        Friend ReadOnly LinkSpans As New List(Of LinkSpan)
        Friend BubbleRect As Rectangle
        Friend TextOriginX As Integer
        Friend TextOriginY As Integer

        Private _kind As ChatItemKind = ChatItemKind.AssistantMessage
        Private _text As String = ""

        Public Sub New()
        End Sub

        Public Sub New(kind As ChatItemKind, text As String)
            _kind = kind
            _text = If(text, "")
            ChatTextHelper.RescanLinks(_text, LinkSpans)
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
                Return _text
            End Get
            Set(value As String)
                Dim v = If(value, "")
                If _text <> v Then
                    _text = v
                    ChatTextHelper.RescanLinks(_text, LinkSpans)
                    NeedsRelayout = True
                    OwnerRoom?.NotifyItemChanged(Me)
                End If
            End Set
        End Property

        Friend Sub AppendTextInternal(more As String)
            If String.IsNullOrEmpty(more) Then Return
            _text &= more
            ChatTextHelper.RescanLinks(_text, LinkSpans)
            NeedsRelayout = True
        End Sub

        Public Overrides Function ToString() As String
            Dim p = If(_text, "")
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

        Protected Overrides Sub InsertItem(index As Integer, item As ChatItem)
            If item Is Nothing Then Return
            MyBase.InsertItem(index, item)
            item.OwnerRoom = _owner
            item.NeedsRelayout = True
            _owner?.OnItemsChanged(index, True)
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            Dim it = Me(index)
            it.OwnerRoom = Nothing
            MyBase.RemoveItem(index)
            _owner?.OnItemsChanged(index, False)
        End Sub

        Protected Overrides Sub ClearItems()
            For Each it In Me
                it.OwnerRoom = Nothing
            Next
            MyBase.ClearItems()
            _owner?.OnItemsChanged(0, False)
        End Sub

        Protected Overrides Sub SetItem(index As Integer, item As ChatItem)
            If item Is Nothing Then Return
            Dim old = Me(index)
            If old IsNot Nothing Then old.OwnerRoom = Nothing
            MyBase.SetItem(index, item)
            item.OwnerRoom = _owner
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
    Friend ReadOnly _items As New ChatItemCollection(Me)
    Friend ReadOnly _scrollBar As New ScrollBarRenderer()
    Friend _滚动偏移 As Integer = 0
    Friend _contentHeight As Integer = 0
    Friend _contentHeightDirty As Boolean = True
    Friend _pinnedToBottom As Boolean = True

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
            Return ItemIndex * 31 Xor CharIndex
        End Function
    End Structure
    Friend _selAnchor As New TextPos(-1, 0)
    Friend _selCaret As New TextPos(-1, 0)
    Friend _hasSelection As Boolean = False
    Friend _mouseSelecting As Boolean = False

    Friend _copyContextMenu As ContextMenuStrip
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
    End Sub
#End Region
#Region "属性"
    Friend _superSamplingScale As Class1.SuperSamplingScaleEnum = Class1.SuperSamplingScaleEnum.OFF
    <Category("LakeUI"), Description(Class1.超采样抗锯齿描述词), DefaultValue(GetType(Class1.SuperSamplingScaleEnum), "OFF")>
    Public Property SuperSamplingScale As Class1.SuperSamplingScaleEnum
        Get
            Return _superSamplingScale
        End Get
        Set(value As Class1.SuperSamplingScaleEnum)
            SetValue(_superSamplingScale, value)
        End Set
    End Property

    Private _backgroundSource As Control = Nothing
    <Category("LakeUI"),
     Description("背景采样源（V2 透明背景穿透）。设置后将跨越任意层级直接采样此控件的绘制内容作为透明背景；为空时按 BackColor 协议处理。"),
     DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource IsNot value Then
                _backgroundSource = value
                Me.Invalidate()
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
                _contentHeightDirty = True
                Invalidate()
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
                InvalidateAllItemsLayout() : Invalidate()
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
                InvalidateAllItemsLayout() : Invalidate()
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
                InvalidateAllItemsLayout() : Invalidate()
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
                InvalidateAllItemsLayout() : Invalidate()
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

    Friend _linkColor As Color = Color.FromArgb(86, 156, 214)
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
                InvalidateAllItemsLayout() : Invalidate()
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
                InvalidateAllItemsLayout() : Invalidate()
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

    Public Event LinkClicked As EventHandler(Of LinkClickedEventArgs)
#End Region
#Region "公共 API"
    ''' <summary>清空所有消息并重置滚动/选区。</summary>
    Public Sub Clear()
        ClearSelection()
        _items.Clear()
        _滚动偏移 = 0
        _pinnedToBottom = True
        _contentHeightDirty = True
        Invalidate()
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
        Dim it As New ChatItem(kind, text)
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
            last.AppendTextInternal(more)
            NotifyItemChanged(last)
        End If
    End Sub

    ''' <summary>滚动到底部。</summary>
    Public Sub ScrollToBottom()
        _pinnedToBottom = True
        EnsureLayout()
        Dim viewH As Integer = ContentViewportHeight()
        _滚动偏移 = Math.Max(0, _contentHeight - viewH)
        Invalidate()
    End Sub
#End Region

#Region "内部状态变更通知"
    Friend Sub NotifyItemChanged(item As ChatItem)
        If item Is Nothing Then Return
        item.NeedsRelayout = True
        _contentHeightDirty = True
        Invalidate()
    End Sub

    Friend Sub OnItemsChanged(index As Integer, isInsert As Boolean)
        _contentHeightDirty = True
        If isInsert AndAlso _autoScrollToBottom AndAlso _pinnedToBottom Then
            ' 在贴底状态下插入，应保持贴底
        End If
        Invalidate()
    End Sub

    Private Sub InvalidateAllItemsLayout()
        For Each it In _items
            it.NeedsRelayout = True
        Next
        _contentHeightDirty = True
    End Sub

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            Invalidate()
        End If
    End Sub
#End Region
#Region "布局与测量"
    Private Function GetContentArea() As Rectangle
        Dim inset As Integer = Math.Max(_borderSize, If(_borderRadius > 0, _borderRadius \ 2, 0))
        Dim pad As Padding = Me.Padding
        Dim x As Integer = inset + pad.Left
        Dim y As Integer = inset + pad.Top
        Dim sbReserved As Integer = If(_scrollBarWidth > 0, _scrollBarWidth + ScrollBarRenderer.Margin * 2, 0)
        Dim w As Integer = Math.Max(0, Width - inset * 2 - pad.Horizontal - sbReserved)
        Dim h As Integer = Math.Max(0, Height - inset * 2 - pad.Vertical)
        Return New Rectangle(x, y, w, h)
    End Function

    Private Function ContentViewportHeight() As Integer
        Return GetContentArea().Height
    End Function

    Private Sub EnsureLayout()
        If Not _contentHeightDirty Then
            ' 仍可能有局部脏；统一遍历会处理
        End If
        Dim area = GetContentArea()
        If area.Width <= 0 Then
            _contentHeight = 0
            _contentHeightDirty = False
            Return
        End If
        Dim y As Integer = 0
        For i = 0 To _items.Count - 1
            Dim it = _items(i)
            If it.NeedsRelayout OrElse it.CachedRect.Width <> area.Width Then
                LayoutItem(it, area.Width)
            End If
            it.CachedRect = New Rectangle(area.X, area.Y + y - _滚动偏移, area.Width, it.CachedRect.Height)
            y += it.CachedRect.Height
            If i < _items.Count - 1 Then y += _itemSpacing
        Next
        _contentHeight = y
        _contentHeightDirty = False

        ' clamp
        Dim maxOff As Integer = Math.Max(0, _contentHeight - area.Height)
        If _滚动偏移 > maxOff Then _滚动偏移 = maxOff
        If _滚动偏移 < 0 Then _滚动偏移 = 0
        If _autoScrollToBottom AndAlso _pinnedToBottom Then
            _滚动偏移 = maxOff
        End If

        ' 更新 CachedRect 的 Y（基于最终 _滚动偏移）
        Dim y2 As Integer = 0
        For i = 0 To _items.Count - 1
            Dim it = _items(i)
            it.CachedRect = New Rectangle(area.X, area.Y + y2 - _滚动偏移, area.Width, it.CachedRect.Height)
            y2 += it.CachedRect.Height
            If i < _items.Count - 1 Then y2 += _itemSpacing
        Next
    End Sub

    Private Sub LayoutItem(it As ChatItem, areaWidth As Integer)
        it.LineRanges.Clear()
        Dim font As Font = Me.Font
        Dim lineHeight As Integer = TextRenderer.MeasureText("Ag", font).Height

        Dim isCard As Boolean = (it.Kind = ChatItemKind.Card)
        Dim pad As Padding = If(isCard, _cardPadding, _bubblePadding)
        Dim maxBubbleW As Integer
        If isCard Then
            maxBubbleW = Math.Max(40, CInt(areaWidth * _cardMaxWidthRatio))
        Else
            Select Case _bubbleWidthMode
                Case BubbleWidthMode.FillAvailable
                    maxBubbleW = areaWidth
                Case BubbleWidthMode.Fixed
                    maxBubbleW = Math.Min(areaWidth, _bubbleFixedWidth)
                Case Else
                    maxBubbleW = Math.Max(40, CInt(areaWidth * _bubbleMaxWidthRatio))
            End Select
        End If

        Dim borderExtra As Integer = If(isCard, _cardBorderSize * 2, 0)
        Dim contentMaxW As Integer = Math.Max(10, maxBubbleW - pad.Horizontal - borderExtra)

        ' 折行
        ChatTextHelper.WrapLines(it.Text, font, lineHeight, contentMaxW, it.LineRanges)

        ' 计算实际气泡宽度（取最长行）
        Dim usedTextW As Integer = 0
        For Each lr In it.LineRanges
            Dim s As String = it.Text.Substring(lr.Start, lr.Length)
            Dim w As Integer = TextRenderHelper.MeasureTextWidth(s, font, lineHeight)
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
        Dim borderInset As Integer = If(isCard, _cardBorderSize, 0)
        it.TextOriginX = bubbleX + pad.Left + borderInset
        it.TextOriginY = pad.Top + borderInset
        it.CachedRect = New Rectangle(0, 0, areaWidth, bubbleH)
        it.NeedsRelayout = False
    End Sub
#End Region
#Region "绘制"
    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' V2 契约：显式 BackgroundSource 由 OnPaint 内绘制穿透底图；否则交给基类处理透明 BackColor。
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        EnsureLayout()
        If Me.Width < 1 OrElse Me.Height < 1 Then Return

        Dim ssaa As Integer = 1
        If Class1.GlobalSSAA <> Class1.SuperSamplingScaleEnum.OFF Then ssaa = CInt(Class1.GlobalSSAA)

        Using scope = D2DHelperV2.BeginPaint(e, Me, ssaa)
            If scope Is Nothing Then Return

            ' 背景层：显式 BackgroundSource 绘制穿透底图；半透明 BackColor 作为底层遮罩。
            If _backgroundSource IsNot Nothing Then
                BackgroundPenetrationV2.PaintBackground(Me, scope, _backgroundSource)
            ElseIf MyBase.BackColor.A > 0 AndAlso MyBase.BackColor.A < 255 Then
                Dim bgLayer = scope.BackgroundLayer
                Dim bgBrush = scope.Compositor.BrushCache.[Get](bgLayer, MyBase.BackColor)
                If bgBrush IsNot Nothing Then
                    bgLayer.FillRectangle(D2DHelper.ToD2DRect(New RectangleF(0, 0, Me.Width, Me.Height)), bgBrush)
                End If
            End If

            Dim gRT As Vortice.Direct2D1.ID2D1RenderTarget = scope.GraphicsLayer
            Dim brushCache = scope.Compositor.BrushCache
            Dim bgRect As New RectangleF(0, 0, Width, Height)

            ' 主体背景：BackColor 仅作透明/遮罩协议，BackColor1 负责控件主体填充。
            Dim backColorMask As Color = MyBase.BackColor
            If _borderRadius > 0 Then
                Using geo = RectangleRenderer.创建圆角矩形几何(bgRect, _borderRadius)
                    If backColorMask.A > 0 AndAlso backColorMask.A < 255 Then
                        RectangleRenderer.绘制圆角背景_D2D(gRT, geo, bgRect, backColorMask, Color.Empty, 0, brushCache)
                    End If
                    If _backColor1.A > 0 Then
                        RectangleRenderer.绘制圆角背景_D2D(gRT, geo, bgRect, _backColor1, Color.Empty, 0, brushCache)
                    End If
                End Using
            Else
                If backColorMask.A > 0 AndAlso backColorMask.A < 255 Then
                    RectangleRenderer.绘制矩形背景_D2D(gRT, bgRect, backColorMask, Color.Empty, 0, brushCache)
                End If
                If _backColor1.A > 0 Then
                    RectangleRenderer.绘制矩形背景_D2D(gRT, bgRect, _backColor1, Color.Empty, 0, brushCache)
                End If
            End If

            ' 气泡形状 + 选区填充（GraphicsLayer 上、文字之下）
            Dim area As Rectangle = GetContentArea()
            Dim font As Font = Me.Font
            Dim lineHeight As Integer = TextRenderer.MeasureText("Ag", font).Height
            gRT.PushAxisAlignedClip(New Vortice.RawRectF(area.Left, area.Top, area.Right, area.Bottom),
                                     Vortice.Direct2D1.AntialiasMode.PerPrimitive)
            Try
                For i As Integer = 0 To _items.Count - 1
                    Dim it = _items(i)
                    Dim r As Rectangle = it.CachedRect
                    If r.Bottom < area.Top OrElse r.Top > area.Bottom Then Continue For
                    DrawItemShapes_D2D(gRT, brushCache, it, r, font, lineHeight, i)
                Next
            Finally
                gRT.PopAxisAlignedClip()
            End Try

            ' 滚动条
            Dim viewH As Integer = area.Height
            Dim totalH As Integer = Math.Max(viewH, _contentHeight)
            If _contentHeight > viewH AndAlso _scrollBarWidth > 0 Then
                _scrollBar.ComputeLayout(Width, Height, _borderSize, _borderRadius,
                                         Me.Padding.Top, Me.Padding.Bottom,
                                         _scrollBarWidth, totalH, viewH, _滚动偏移)
                _scrollBar.Draw_D2D(gRT, Width, Height, _borderSize, _borderRadius, _scrollBarWidth,
                                    _scrollBarTrackColor, _scrollBarThumbColor, _scrollBarThumbHoverColor)
            End If

            ' 边框
            If _borderSize > 0 Then
                Dim half As Single = _borderSize / 2.0F
                Dim brRect As New RectangleF(half, half, Width - _borderSize, Height - _borderSize)
                If _borderRadius > 0 Then
                    Using geo = RectangleRenderer.创建圆角矩形几何(brRect, _borderRadius)
                        RectangleRenderer.绘制圆角边框_D2D(gRT, geo, _borderColor, _borderSize, brushCache)
                    End Using
                Else
                    RectangleRenderer.绘制矩形边框_D2D(gRT, brRect, _borderColor, _borderSize, brushCache)
                End If
            End If

            scope.FlushGraphics()

            ' 文字层：所有气泡文本 + 链接
            Dim textRT As Vortice.Direct2D1.ID2D1RenderTarget = scope.TextLayer
            Dim tfc = scope.Compositor.TextFormatCache
            Dim dpiS As Single = CSng(Me.DeviceDpi) / 96.0F
            textRT.PushAxisAlignedClip(New Vortice.RawRectF(area.Left, area.Top, area.Right, area.Bottom),
                                       Vortice.Direct2D1.AntialiasMode.PerPrimitive)
            Try
                For i As Integer = 0 To _items.Count - 1
                    Dim it = _items(i)
                    Dim r As Rectangle = it.CachedRect
                    If r.Bottom < area.Top OrElse r.Top > area.Bottom Then Continue For
                    DrawItemText_D2D(textRT, brushCache, tfc, dpiS, it, r, font, lineHeight)
                Next
            Finally
                textRT.PopAxisAlignedClip()
            End Try
        End Using
    End Sub

    Private Sub DrawItemShapes_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget,
                                    brushCache As D2DHelper.SolidColorBrushCache,
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
        Dim radius As Integer = If(isCard, _cardRadius, _bubbleRadius)
        Dim rectF As New RectangleF(bubbleAbs.X, bubbleAbs.Y, bubbleAbs.Width, bubbleAbs.Height)
        If radius > 0 Then
            Using geo = RectangleRenderer.创建圆角矩形几何(rectF, radius)
                RectangleRenderer.绘制圆角背景_D2D(rt, geo, rectF, backColor, Color.Empty, 0, brushCache)
                If isCard AndAlso _cardBorderSize > 0 Then
                    RectangleRenderer.绘制圆角边框_D2D(rt, geo, _cardBorderColor, _cardBorderSize, brushCache)
                End If
            End Using
        Else
            RectangleRenderer.绘制矩形背景_D2D(rt, rectF, backColor, Color.Empty, 0, brushCache)
            If isCard AndAlso _cardBorderSize > 0 Then
                RectangleRenderer.绘制矩形边框_D2D(rt, rectF, _cardBorderColor, _cardBorderSize, brushCache)
            End If
        End If

        ' 选区背景
        DrawSelectionForItem_D2D(rt, brushCache, it, itemIndex, areaItemRect, font, lineHeight)
    End Sub

    Private Sub DrawSelectionForItem_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget,
                                          brushCache As D2DHelper.SolidColorBrushCache,
                                          it As ChatItem, itemIndex As Integer,
                                          areaItemRect As Rectangle, font As Font, lineHeight As Integer)
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
            Dim preW As Integer = TextRenderHelper.MeasureTextWidth(line.Substring(0, a - lineS), font, lineHeight)
            Dim segW As Integer = TextRenderHelper.MeasureTextWidth(line.Substring(a - lineS, b - a), font, lineHeight)
            rt.FillRectangle(New Vortice.RawRectF(textX + preW, textY + li * lineHeight, textX + preW + segW, textY + li * lineHeight + lineHeight), selBr)
        Next
    End Sub

    Private Sub DrawItemText_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget,
                                  brushCache As D2DHelper.SolidColorBrushCache,
                                  tfc As D2DHelper.TextFormatCache, dpiS As Single,
                                  it As ChatItem, areaItemRect As Rectangle, font As Font, lineHeight As Integer)
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
                                       brushCache As D2DHelper.SolidColorBrushCache,
                                       tfc As D2DHelper.TextFormatCache, dpiS As Single,
                                       it As ChatItem, line As String, lineStartCharIndex As Integer,
                                       x As Integer, y As Integer, font As Font, lineHeight As Integer,
                                       normalColor As Color)
        If String.IsNullOrEmpty(line) Then Return
        Dim curX As Integer = x
        Dim lineEnd As Integer = lineStartCharIndex + line.Length
        Dim segments As New List(Of (s As Integer, e As Integer, isLink As Boolean, url As String))
        Dim p As Integer = lineStartCharIndex
        While p < lineEnd
            Dim link = FindLinkAt(it, p)
            If link.HasValue AndAlso link.Value.Start < lineEnd Then
                If link.Value.Start > p Then
                    segments.Add((p - lineStartCharIndex, link.Value.Start - lineStartCharIndex, False, Nothing))
                End If
                Dim segE As Integer = Math.Min(lineEnd, link.Value.Start + link.Value.Length)
                segments.Add((Math.Max(p, link.Value.Start) - lineStartCharIndex, segE - lineStartCharIndex, True, link.Value.Url))
                p = segE
            Else
                segments.Add((p - lineStartCharIndex, lineEnd - lineStartCharIndex, False, Nothing))
                p = lineEnd
            End If
        End While
        Dim flags As TextFormatFlags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
        For Each seg In segments
            If seg.e <= seg.s Then Continue For
            Dim part As String = line.Substring(seg.s, seg.e - seg.s)
            Dim w As Integer = TextRenderHelper.MeasureTextWidth(part, font, lineHeight)
            If seg.isLink Then
                Dim col As Color = If(seg.url IsNot Nothing AndAlso seg.url = _hoverLinkUrl, _linkHoverColor, _linkColor)
                D2DTextRenderer.DrawText(rt, part, font, New Rectangle(curX, y, w, lineHeight), col, flags, dpiS, tfc, brushCache)
                If _linkUnderline Then
                    Dim br = brushCache.Get(rt, col)
                    rt.DrawLine(New System.Numerics.Vector2(curX, y + lineHeight - 2),
                                New System.Numerics.Vector2(curX + w, y + lineHeight - 2), br, 1.0F)
                End If
            Else
                D2DTextRenderer.DrawText(rt, part, font, New Rectangle(curX, y, w, lineHeight), normalColor, flags, dpiS, tfc, brushCache)
            End If
            curX += w
        Next
    End Sub

    Private Function FindLinkAt(it As ChatItem, charIndex As Integer) As LinkSpan?
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
        If _hasSelection Then
            _hasSelection = False
            _selAnchor = New TextPos(-1, 0)
            _selCaret = New TextPos(-1, 0)
            Invalidate()
        End If
    End Sub

    Public Sub SelectAllText()
        If _items.Count = 0 Then Return
        _selAnchor = New TextPos(0, 0)
        Dim last = _items(_items.Count - 1)
        _selCaret = New TextPos(_items.Count - 1, last.Text.Length)
        _hasSelection = True
        Invalidate()
    End Sub

    Public Function GetSelectedText() As String
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

    Private Function HitTestText(pt As Point, ByRef result As TextPos, Optional snap As Boolean = True) As Boolean
        result = New TextPos(-1, 0)
        Dim font As Font = Me.Font
        Dim lineHeight As Integer = TextRenderer.MeasureText("Ag", font).Height
        For i = 0 To _items.Count - 1
            Dim it = _items(i)
            Dim r = it.CachedRect
            Dim bubbleAbs As New Rectangle(r.X + it.BubbleRect.X, r.Y + it.BubbleRect.Y, it.BubbleRect.Width, it.BubbleRect.Height)
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
            Dim li As Integer = Math.Max(0, Math.Min(it.LineRanges.Count - 1, relY \ Math.Max(1, lineHeight)))
            If it.LineRanges.Count = 0 Then
                result = New TextPos(i, 0) : Return True
            End If
            Dim lr = it.LineRanges(li)
            Dim line As String = it.Text.Substring(lr.Start, lr.Length)
            Dim col As Integer = TextRenderHelper.FindColFromX(line, pt.X - textX, font, lineHeight)
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

    Private Function HitTestLink(pt As Point) As String
        Dim font As Font = Me.Font
        Dim lineHeight As Integer = TextRenderer.MeasureText("Ag", font).Height
        For i = 0 To _items.Count - 1
            Dim it = _items(i)
            Dim r = it.CachedRect
            If pt.Y < r.Top OrElse pt.Y > r.Bottom Then Continue For
            Dim textX As Integer = r.X + it.TextOriginX
            Dim textY As Integer = r.Y + it.TextOriginY
            Dim relY As Integer = pt.Y - textY
            If relY < 0 Then Continue For
            Dim li As Integer = relY \ Math.Max(1, lineHeight)
            If li < 0 OrElse li >= it.LineRanges.Count Then Continue For
            Dim lr = it.LineRanges(li)
            Dim line As String = it.Text.Substring(lr.Start, lr.Length)
            Dim col As Integer = TextRenderHelper.FindColFromX(line, pt.X - textX, font, lineHeight)
            Dim absCharIndex As Integer = lr.Start + col
            For Each ls In it.LinkSpans
                If absCharIndex >= ls.Start AndAlso absCharIndex < ls.Start + ls.Length Then
                    Return ls.Url
                End If
            Next
        Next
        Return Nothing
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
                Invalidate()
                Return
            End If
            If _scrollBar.TrackRect.Contains(e.Location) Then
                Dim viewH = ContentViewportHeight()
                Dim totalH = Math.Max(viewH, _contentHeight)
                _滚动偏移 = _scrollBar.TrackClick(e.Location, _滚动偏移, totalH, viewH)
                _pinnedToBottom = (_滚动偏移 >= totalH - viewH)
                Invalidate()
                Return
            End If

            ' 链接点击
            Dim url = HitTestLink(e.Location)
            If url IsNot Nothing Then
                RaiseEvent LinkClicked(Me, New LinkClickedEventArgs With {.Url = url, .ItemIndex = -1})
                Try
                    Process.Start(New ProcessStartInfo(url) With {.UseShellExecute = True})
                Catch
                End Try
                Return
            End If

            If _selectableText Then
                Dim pos As TextPos
                If HitTestText(e.Location, pos, snap:=True) Then
                    _selAnchor = pos : _selCaret = pos
                    _hasSelection = False
                    _mouseSelecting = True
                    Capture = True
                    Invalidate()
                End If
            End If
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If _scrollBar.IsDragging Then
            Dim viewH = ContentViewportHeight()
            Dim totalH = Math.Max(viewH, _contentHeight)
            _滚动偏移 = _scrollBar.DragMove(e.Y, totalH, viewH)
            _pinnedToBottom = (_滚动偏移 >= totalH - viewH)
            Invalidate()
            Return
        End If

        If _mouseSelecting Then
            Dim pos As TextPos
            If HitTestText(e.Location, pos, snap:=True) Then
                _selCaret = pos
                _hasSelection = (_selAnchor <> _selCaret)
                Invalidate()
            End If
            ' 边缘自动滚动
            Dim area = GetContentArea()
            If e.Y < area.Top Then
                _滚动偏移 = Math.Max(0, _滚动偏移 - 8) : Invalidate()
            ElseIf e.Y > area.Bottom Then
                Dim viewH = ContentViewportHeight()
                Dim maxOff = Math.Max(0, _contentHeight - viewH)
                _滚动偏移 = Math.Min(maxOff, _滚动偏移 + 8) : Invalidate()
            End If
            Return
        End If

        ' 悬停光标
        Dim url = HitTestLink(e.Location)
        If url IsNot _hoverLinkUrl Then
            _hoverLinkUrl = url
            Invalidate()
        End If
        If url IsNot Nothing Then
            Cursor = Cursors.Hand
        ElseIf _selectableText AndAlso GetContentArea().Contains(e.Location) Then
            Cursor = Cursors.IBeam
        Else
            Cursor = Cursors.Default
        End If

        If _scrollBar.UpdateHover(e.Location) Then Invalidate()
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If _scrollBar.IsDragging Then
            _scrollBar.EndDrag()
            Capture = False
            Invalidate()
        End If
        If _mouseSelecting Then
            _mouseSelecting = False
            Capture = False
        End If

        If e.Button = MouseButtons.Right AndAlso _showCopyContextMenu Then
            _copyContextMenu.Show(Me, e.Location)
        End If
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If _hoverLinkUrl IsNot Nothing Then
            _hoverLinkUrl = Nothing
            Invalidate()
        End If
        If _scrollBar.ResetHover() Then Invalidate()
        Cursor = Cursors.Default
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        Dim viewH = ContentViewportHeight()
        Dim maxOff = Math.Max(0, _contentHeight - viewH)
        Dim delta As Integer = -Math.Sign(e.Delta) * _wheelStep
        _滚动偏移 = Math.Max(0, Math.Min(maxOff, _滚动偏移 + delta))
        _pinnedToBottom = (_滚动偏移 >= maxOff)
        Invalidate()
    End Sub

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        InvalidateAllItemsLayout()
        Invalidate()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        InvalidateAllItemsLayout()
        D2DHelperV2.RefreshFontDependentRendering(Me)
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
        Dim viewH = ContentViewportHeight()
        Dim maxOff = Math.Max(0, _contentHeight - viewH)
        Select Case e.KeyCode
            Case Keys.Up
                _滚动偏移 = Math.Max(0, _滚动偏移 - _wheelStep) : Invalidate()
            Case Keys.Down
                _滚动偏移 = Math.Min(maxOff, _滚动偏移 + _wheelStep) : Invalidate()
            Case Keys.PageUp
                _滚动偏移 = Math.Max(0, _滚动偏移 - viewH) : Invalidate()
            Case Keys.PageDown
                _滚动偏移 = Math.Min(maxOff, _滚动偏移 + viewH) : Invalidate()
            Case Keys.Home
                _滚动偏移 = 0 : _pinnedToBottom = (maxOff = 0) : Invalidate()
            Case Keys.End
                _滚动偏移 = maxOff : _pinnedToBottom = True : Invalidate()
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
                         output As List(Of LineRange))
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
                Dim fitLen As Integer = FitLength(text, p, segEnd, font, lineHeight, maxWidth)
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
                               font As Font, lineHeight As Integer, maxWidth As Integer) As Integer
        Dim total As Integer = [end] - start
        If total <= 0 Then Return 0
        Dim full As String = text.Substring(start, total)
        If TextRenderHelper.MeasureTextWidth(full, font, lineHeight) <= maxWidth Then Return total
        Dim lo As Integer = 1, hi As Integer = total
        While lo < hi
            Dim mid As Integer = (lo + hi + 1) \ 2
            If TextRenderHelper.MeasureTextWidth(text.Substring(start, mid), font, lineHeight) <= maxWidth Then
                lo = mid
            Else
                hi = mid - 1
            End If
        End While
        Return lo
    End Function
End Module
