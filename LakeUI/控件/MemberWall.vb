Imports System.ComponentModel
Imports System.Numerics
Imports Vortice.Direct2D1
Imports Vortice.DirectWrite

''' <summary>
''' 成员墙控件：用虚拟化的 D2D 卡片流展示项目成员 / 赞助名单，并内置搜索。
''' </summary>
<DefaultEvent("ItemClick")>
<DefaultProperty("Items")>
Partial Public Class MemberWall

#Region "数据模型"

    <TypeConverter(GetType(ExpandableObjectConverter))>
    Public Class MemberItem
        Private _owner As MemberWall
        Private _text As String = ""
        Private _foreColor As Color = Color.Empty
        Private _backColor As Color = Color.Empty
        Private _borderColor As Color = Color.Empty
        Private _borderSize As Integer = -1
        Private _clickAction As Action = Nothing

        Public Sub New()
        End Sub

        Public Sub New(text As String)
            _text = If(text, "")
        End Sub

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Friend Property Owner As MemberWall
            Get
                Return _owner
            End Get
            Set(value As MemberWall)
                _owner = value
            End Set
        End Property

        <Category("LakeUI"), Description("成员卡文字"), DefaultValue(GetType(String), "")>
        Public Property Text As String
            Get
                Return _text
            End Get
            Set(value As String)
                Dim v As String = If(value, "")
                If _text = v Then Return
                _text = v
                _owner?.NotifyItemContentChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("成员卡独立文字颜色。留空时使用 MemberWall.ForeColor。"), DefaultValue(GetType(Color), "")>
        Public Property ForeColor As Color
            Get
                Return _foreColor
            End Get
            Set(value As Color)
                If _foreColor = value Then Return
                _foreColor = value
                _owner?.NotifyItemContentChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("成员卡独立背景颜色。留空时使用 CardBackColor。"), DefaultValue(GetType(Color), "")>
        Public Property BackColor As Color
            Get
                Return _backColor
            End Get
            Set(value As Color)
                If _backColor = value Then Return
                _backColor = value
                _owner?.NotifyItemContentChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("成员卡独立边框颜色。留空时使用 CardBorderColor。"), DefaultValue(GetType(Color), "")>
        Public Property BorderColor As Color
            Get
                Return _borderColor
            End Get
            Set(value As Color)
                If _borderColor = value Then Return
                _borderColor = value
                _owner?.NotifyItemContentChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("成员卡独立边框厚度。小于 0 时使用 CardBorderSize。"), DefaultValue(-1)>
        Public Property BorderSize As Integer
            Get
                Return _borderSize
            End Get
            Set(value As Integer)
                If _borderSize = value Then Return
                _borderSize = value
                _owner?.NotifyItemContentChanged()
            End Set
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property ClickAction As Action
            Get
                Return _clickAction
            End Get
            Set(value As Action)
                _clickAction = value
            End Set
        End Property

        Public Overrides Function ToString() As String
            If String.IsNullOrEmpty(_text) Then Return "(空)"
            Return _text
        End Function
    End Class

    Public Class MemberItemCollection
        Inherits ObjectModel.Collection(Of MemberItem)

        Private ReadOnly _owner As MemberWall
        Private _suspendCount As Integer = 0
        Private _dirty As Boolean = False

        Friend Sub New(owner As MemberWall)
            _owner = owner
        End Sub

        Public Sub BeginUpdate()
            _suspendCount += 1
        End Sub

        Public Sub EndUpdate()
            If _suspendCount > 0 Then _suspendCount -= 1
            If _suspendCount = 0 AndAlso _dirty Then
                _dirty = False
                NotifyChanged()
            End If
        End Sub

        Public Overloads Function Add(text As String) As MemberItem
            Dim it As New MemberItem(text)
            Add(it)
            Return it
        End Function

        Public Overloads Sub AddRange(items As IEnumerable(Of MemberItem))
            If items Is Nothing Then Return
            BeginUpdate()
            Try
                For Each it In items
                    Add(it)
                Next
            Finally
                EndUpdate()
            End Try
        End Sub

        Public Overloads Sub AddRange(texts As IEnumerable(Of String))
            If texts Is Nothing Then Return
            BeginUpdate()
            Try
                For Each memberText As String In texts
                    MyBase.Add(New MemberItem(memberText))
                Next
            Finally
                EndUpdate()
            End Try
        End Sub

        Private Sub NotifyChanged()
            If _suspendCount > 0 Then
                _dirty = True
                Return
            End If
            _owner.NotifyItemContentChanged()
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As MemberItem)
            If item Is Nothing Then item = New MemberItem()
            item.Owner = _owner
            MyBase.InsertItem(index, item)
            NotifyChanged()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            Dim it = Me(index)
            If it IsNot Nothing Then it.Owner = Nothing
            MyBase.RemoveItem(index)
            NotifyChanged()
        End Sub

        Protected Overrides Sub ClearItems()
            For Each it In Me
                If it IsNot Nothing Then it.Owner = Nothing
            Next
            MyBase.ClearItems()
            NotifyChanged()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, item As MemberItem)
            If item Is Nothing Then item = New MemberItem()
            Dim old = Me(index)
            If old IsNot Nothing Then old.Owner = Nothing
            item.Owner = _owner
            MyBase.SetItem(index, item)
            NotifyChanged()
        End Sub
    End Class

#End Region

#Region "事件"

    Public Class MemberItemEventArgs
        Inherits EventArgs

        Public ReadOnly Property Item As MemberItem
        Public ReadOnly Property Index As Integer

        Public Sub New(item As MemberItem, index As Integer)
            Me.Item = item
            Me.Index = index
        End Sub
    End Class

    Public Event ItemClick As EventHandler(Of MemberItemEventArgs)

#End Region

#Region "字段"

    Private ReadOnly _items As New MemberItemCollection(Me)
    Private ReadOnly _scrollBar As New ScrollBarRenderer()
    Private ReadOnly _layoutCache As New List(Of CardLayout)

    Private Structure CardLayout
        Public Index As Integer
        Public Bounds As RectangleF
    End Structure

    Private _layoutDirty As Boolean = True
    Private _contentHeight As Integer = 0
    Private _scrollOffset As Integer = 0
    Private _showVScroll As Boolean = False
    Private _cardsViewportRect As RectangleF = RectangleF.Empty
    Private _searchText As String = ""

    Private _hoverIndex As Integer = -1
    Private _pressedIndex As Integer = -1

#End Region

#Region "构造"

    Public Sub New()
        InitializeComponent()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.Selectable Or
                 ControlStyles.SupportsTransparentBackColor, True)
        DoubleBuffered = True
        TabStop = True
        Size = New Size(420, 260)
        MyBase.Padding = New Padding(10)
        MyBase.BackColor = Color.Transparent
        MyBase.ForeColor = Color.Silver
    End Sub

#End Region

#Region "通用"

    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function

    Private Function IsInDesignMode() As Boolean
        Return LicenseManager.UsageMode = LicenseUsageMode.Designtime OrElse
               Me.DesignMode OrElse
               (Me.Site IsNot Nothing AndAlso Me.Site.DesignMode)
    End Function

    Private Sub SetValue(Of T)(ByRef field As T, value As T, Optional affectsLayout As Boolean = False)
        If EqualityComparer(Of T).Default.Equals(field, value) Then Return
        field = value
        If affectsLayout Then _layoutDirty = True
        Invalidate()
    End Sub

    Friend Sub NotifyItemContentChanged()
        _layoutDirty = True
        _hoverIndex = -1
        _pressedIndex = -1
        If IsInDesignMode() Then Invalidate()
    End Sub

    ''' <summary>
    ''' 手动刷新 Items 变更后的布局与绘制。运行时 Items 变动不会自动触发重绘，请调用此方法。
    ''' </summary>
    Public Sub Redraw()
        _layoutDirty = True
        Invalidate()
    End Sub

    ''' <summary>
    ''' 按关键字更新成员墙的渲染内容。搜索规则与 ModernTabListControl 一致：
    ''' 以空白分词、去重，所有词元都在成员卡文字中出现才视为命中。
    ''' </summary>
    Public Sub Search(keyword As String)
        Dim v As String = If(keyword, "")
        _searchText = v
        _scrollOffset = 0
        _layoutDirty = True
        _hoverIndex = -1
        _pressedIndex = -1
        Invalidate()
    End Sub

    Private Shared Function 搜索词元(text As String) As String()
        If String.IsNullOrWhiteSpace(text) Then Return Array.Empty(Of String)()

        Dim tokens As New List(Of String)()
        Dim current As New System.Text.StringBuilder()
        For Each ch As Char In text
            If Char.IsWhiteSpace(ch) Then
                If current.Length > 0 Then
                    Dim token As String = current.ToString().ToLowerInvariant()
                    If tokens.IndexOf(token) < 0 Then tokens.Add(token)
                    current.Clear()
                End If
            Else
                current.Append(Char.ToLowerInvariant(ch))
            End If
        Next

        If current.Length > 0 Then
            Dim token As String = current.ToString().ToLowerInvariant()
            If tokens.IndexOf(token) < 0 Then tokens.Add(token)
        End If

        Return tokens.ToArray()
    End Function

    Private Shared Function 标题匹配搜索(text As String, searchTokens As String()) As Boolean
        If searchTokens Is Nothing OrElse searchTokens.Length = 0 Then Return True
        If String.IsNullOrWhiteSpace(text) Then Return False

        For Each token As String In searchTokens
            If token.Length = 0 Then Continue For
            If text.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0 Then Return False
        Next
        Return True
    End Function

    Private Shared Function TextRenderGuard(dpiScale As Single) As Single
        Return Math.Max(2.0F, CSng(Math.Ceiling(2.0F * Math.Max(1.0F, dpiScale))))
    End Function

#End Region

#Region "公开属性 - 数据"

    <DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
     Editor("System.ComponentModel.Design.CollectionEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing"),
     Category("LakeUI"), Description("成员卡集合。运行时修改后不会自动重绘，请调用 Redraw。"), Browsable(True)>
    Public ReadOnly Property Items As MemberItemCollection
        Get
            Return _items
        End Get
    End Property

#End Region

#Region "公开属性 - 外观"

    Private _backgroundSource As Control = Nothing
    <Category("LakeUI"),
     Description("背景采样源（V2 透明背景穿透）。设置后将跨越任意层级直接采样此控件的绘制内容作为透明背景。"),
     DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource Is value Then Return
            _backgroundSource = value
            Invalidate()
        End Set
    End Property

    Private _superSamplingScale As Class1.SuperSamplingScaleEnum = Class1.SuperSamplingScaleEnum.OFF
    <Category("LakeUI"), Description(Class1.超采样抗锯齿描述词), DefaultValue(GetType(Class1.SuperSamplingScaleEnum), "OFF"), Browsable(True)>
    Public Property SuperSamplingScale As Class1.SuperSamplingScaleEnum
        Get
            Return _superSamplingScale
        End Get
        Set(value As Class1.SuperSamplingScaleEnum)
            SetValue(_superSamplingScale, value)
        End Set
    End Property

    Private _backColor1 As Color = Color.Transparent
    <Category("LakeUI"), Description("主体背景颜色"), DefaultValue(GetType(Color), "Transparent"), Browsable(True)>
    Public Property BackColor1 As Color
        Get
            Return _backColor1
        End Get
        Set(value As Color)
            SetValue(_backColor1, value)
        End Set
    End Property

    Private _borderColor As Color = Color.Transparent
    <Category("LakeUI"), Description("主体边框颜色"), DefaultValue(GetType(Color), "Transparent"), Browsable(True)>
    Public Property BorderColor As Color
        Get
            Return _borderColor
        End Get
        Set(value As Color)
            SetValue(_borderColor, value)
        End Set
    End Property

    Private _borderSize As Integer = 0
    <Category("LakeUI"), Description("主体边框宽度"), DefaultValue(0), Browsable(True)>
    Public Property BorderSize As Integer
        Get
            Return _borderSize
        End Get
        Set(value As Integer)
            SetValue(_borderSize, Math.Max(0, value), True)
        End Set
    End Property

    Private _borderRadius As Integer = 0
    <Category("LakeUI"), Description("主体边框圆角半径"), DefaultValue(0), Browsable(True)>
    Public Property BorderRadius As Integer
        Get
            Return _borderRadius
        End Get
        Set(value As Integer)
            SetValue(_borderRadius, Math.Max(0, value), True)
        End Set
    End Property

    <Category("LakeUI"), Description("默认文字颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Overrides Property ForeColor As Color
        Get
            Return MyBase.ForeColor
        End Get
        Set(value As Color)
            If MyBase.ForeColor = value Then Return
            MyBase.ForeColor = value
            Invalidate()
        End Set
    End Property

    Private _disabledOverlayColor As Color = Color.FromArgb(120, 0, 0, 0)
    <Category("LakeUI"), Description("禁用（Enabled = False）时覆盖在主体区域上的遮罩颜色。"), DefaultValue(GetType(Color), "120, 0, 0, 0"), Browsable(True)>
    Public Property DisabledOverlayColor As Color
        Get
            Return _disabledOverlayColor
        End Get
        Set(value As Color)
            SetValue(_disabledOverlayColor, value)
        End Set
    End Property

#End Region

#Region "公开属性 - 成员卡"

    Private _cardBackColor As Color = Color.FromArgb(42, 42, 42)
    <Category("LakeUI"), Description("成员卡默认背景颜色"), DefaultValue(GetType(Color), "42, 42, 42"), Browsable(True)>
    Public Property CardBackColor As Color
        Get
            Return _cardBackColor
        End Get
        Set(value As Color)
            SetValue(_cardBackColor, value)
        End Set
    End Property

    Private _cardBorderColor As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("成员卡默认边框颜色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
    Public Property CardBorderColor As Color
        Get
            Return _cardBorderColor
        End Get
        Set(value As Color)
            SetValue(_cardBorderColor, value)
        End Set
    End Property

    Private _cardBorderSize As Integer = 1
    <Category("LakeUI"), Description("成员卡默认边框宽度"), DefaultValue(1), Browsable(True)>
    Public Property CardBorderSize As Integer
        Get
            Return _cardBorderSize
        End Get
        Set(value As Integer)
            SetValue(_cardBorderSize, Math.Max(0, value), True)
        End Set
    End Property

    Private _cardRadius As Integer = 6
    <Category("LakeUI"), Description("成员卡圆角半径"), DefaultValue(6), Browsable(True)>
    Public Property CardRadius As Integer
        Get
            Return _cardRadius
        End Get
        Set(value As Integer)
            SetValue(_cardRadius, Math.Max(0, value))
        End Set
    End Property

    Private _cardSpacing As Integer = 8
    <Category("LakeUI"), Description("成员卡之间的水平和垂直间距"), DefaultValue(8), Browsable(True)>
    Public Property CardSpacing As Integer
        Get
            Return _cardSpacing
        End Get
        Set(value As Integer)
            SetValue(_cardSpacing, Math.Max(0, value), True)
        End Set
    End Property

    Private _cardHorizontalPadding As Integer = 14
    <Category("LakeUI"), Description("成员卡文字左右留白，卡片宽度会按文字宽度加上该值自动计算。"), DefaultValue(14), Browsable(True)>
    Public Property CardHorizontalPadding As Integer
        Get
            Return _cardHorizontalPadding
        End Get
        Set(value As Integer)
            SetValue(_cardHorizontalPadding, Math.Max(0, value), True)
        End Set
    End Property

    Private _cardVerticalPadding As Integer = 6
    <Category("LakeUI"), Description("成员卡文字上下留白，卡片高度会按文字高度加上该值自动计算。"), DefaultValue(6), Browsable(True)>
    Public Property CardVerticalPadding As Integer
        Get
            Return _cardVerticalPadding
        End Get
        Set(value As Integer)
            SetValue(_cardVerticalPadding, Math.Max(0, value), True)
        End Set
    End Property

    Private _cardMinWidth As Integer = 48
    <Category("LakeUI"), Description("成员卡最小宽度"), DefaultValue(48), Browsable(True)>
    Public Property CardMinWidth As Integer
        Get
            Return _cardMinWidth
        End Get
        Set(value As Integer)
            SetValue(_cardMinWidth, Math.Max(1, value), True)
        End Set
    End Property

    Private _cardMinHeight As Integer = 28
    <Category("LakeUI"), Description("成员卡最小高度"), DefaultValue(28), Browsable(True)>
    Public Property CardMinHeight As Integer
        Get
            Return _cardMinHeight
        End Get
        Set(value As Integer)
            SetValue(_cardMinHeight, Math.Max(1, value), True)
        End Set
    End Property

    Private _hoverOverlayColor As Color = Color.FromArgb(28, 255, 255, 255)
    <Category("LakeUI"), Description("成员卡鼠标悬停时叠加的遮罩颜色"), DefaultValue(GetType(Color), "28, 255, 255, 255"), Browsable(True)>
    Public Property CardHoverOverlayColor As Color
        Get
            Return _hoverOverlayColor
        End Get
        Set(value As Color)
            SetValue(_hoverOverlayColor, value)
        End Set
    End Property

    Private _pressedOverlayColor As Color = Color.FromArgb(40, 0, 0, 0)
    <Category("LakeUI"), Description("成员卡鼠标按下时叠加的遮罩颜色"), DefaultValue(GetType(Color), "40, 0, 0, 0"), Browsable(True)>
    Public Property CardPressedOverlayColor As Color
        Get
            Return _pressedOverlayColor
        End Get
        Set(value As Color)
            SetValue(_pressedOverlayColor, value)
        End Set
    End Property

#End Region

#Region "公开属性 - 滚动条"

    Private _scrollBarWidth As Integer = 8
    <Category("LakeUI"), Description("滚动条宽度"), DefaultValue(8), Browsable(True)>
    Public Property ScrollBarWidth As Integer
        Get
            Return _scrollBarWidth
        End Get
        Set(value As Integer)
            SetValue(_scrollBarWidth, Math.Max(1, value), True)
        End Set
    End Property

    Private _scrollStep As Integer = 48
    <Category("LakeUI"), Description("鼠标滚轮每次滚动的像素步长"), DefaultValue(48), Browsable(True)>
    Public Property ScrollStep As Integer
        Get
            Return _scrollStep
        End Get
        Set(value As Integer)
            _scrollStep = Math.Max(1, value)
        End Set
    End Property

    Private _scrollBarTrackColor As Color = Color.FromArgb(18, 18, 18)
    <Category("LakeUI"), Description("滚动条轨道颜色"), DefaultValue(GetType(Color), "18, 18, 18"), Browsable(True)>
    Public Property ScrollBarTrackColor As Color
        Get
            Return _scrollBarTrackColor
        End Get
        Set(value As Color)
            SetValue(_scrollBarTrackColor, value)
        End Set
    End Property

    Private _scrollBarThumbColor As Color = Color.FromArgb(92, 92, 92)
    <Category("LakeUI"), Description("滚动条滑块颜色"), DefaultValue(GetType(Color), "92, 92, 92"), Browsable(True)>
    Public Property ScrollBarThumbColor As Color
        Get
            Return _scrollBarThumbColor
        End Get
        Set(value As Color)
            SetValue(_scrollBarThumbColor, value)
        End Set
    End Property

    Private _scrollBarThumbHoverColor As Color = Color.FromArgb(132, 132, 132)
    <Category("LakeUI"), Description("滚动条滑块悬停颜色"), DefaultValue(GetType(Color), "132, 132, 132"), Browsable(True)>
    Public Property ScrollBarThumbHoverColor As Color
        Get
            Return _scrollBarThumbHoverColor
        End Get
        Set(value As Color)
            SetValue(_scrollBarThumbHoverColor, value)
        End Set
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property VerticalScrollOffset As Integer
        Get
            Return _scrollOffset
        End Get
    End Property

#End Region

#Region "布局"

    Private Sub EnsureLayout()
        If Not _layoutDirty Then Return
        RebuildLayout()
    End Sub

    Private Sub RebuildLayout()
        _layoutCache.Clear()
        _showVScroll = False
        _cardsViewportRect = RectangleF.Empty
        _contentHeight = 0

        If Width <= 0 OrElse Height <= 0 Then
            _layoutDirty = False
            Return
        End If

        Dim s As Single = DpiScale()
        Dim borderPx As Integer = CInt(Math.Round(_borderSize * s))
        Dim radiusPx As Integer = CInt(Math.Round(_borderRadius * s))
        Dim inset As Single = Math.Max(borderPx, If(_borderRadius > 0, radiusPx / 2.0F, 0.0F))

        Dim contentPadding As Padding = MyBase.Padding
        Dim inner As New RectangleF(
            inset + contentPadding.Left,
            inset + contentPadding.Top,
            Math.Max(0.0F, Width - inset * 2.0F - contentPadding.Horizontal),
            Math.Max(0.0F, Height - inset * 2.0F - contentPadding.Vertical))

        _cardsViewportRect = inner
        If _cardsViewportRect.Width <= 0 OrElse _cardsViewportRect.Height <= 0 Then
            _scrollOffset = 0
            _layoutDirty = False
            Return
        End If

        Dim searchTokens As String() = 搜索词元(_searchText)
        Dim tempLayout As New List(Of CardLayout)
        Dim contentH As Integer = BuildCardLayout(_cardsViewportRect.Width, searchTokens, tempLayout)
        Dim needScroll As Boolean = contentH > _cardsViewportRect.Height

        If needScroll Then
            Dim reserve As Single = CInt(Math.Round(_scrollBarWidth * s)) + ScrollBarRenderer.Margin * 2
            _cardsViewportRect.Width = Math.Max(0.0F, _cardsViewportRect.Width - reserve)
            tempLayout.Clear()
            contentH = BuildCardLayout(_cardsViewportRect.Width, searchTokens, tempLayout)
            needScroll = contentH > _cardsViewportRect.Height
        End If

        _layoutCache.AddRange(tempLayout)
        _contentHeight = contentH
        _showVScroll = needScroll
        ClampScrollOffset()

        If _showVScroll Then
            Dim scrollW As Integer = CInt(Math.Round(_scrollBarWidth * s))
            Dim padTop As Integer = Math.Max(0, CInt(Math.Round(_cardsViewportRect.Top - inset - ScrollBarRenderer.Margin)))
            Dim padBottom As Integer = Math.Max(0, CInt(Math.Round(Height - _cardsViewportRect.Bottom - inset - ScrollBarRenderer.Margin)))
            _scrollBar.ComputeLayout(Width, Height, borderPx, radiusPx, padTop, padBottom, scrollW,
                                     Math.Max(1, _contentHeight),
                                     Math.Max(1, CInt(Math.Floor(_cardsViewportRect.Height))),
                                     _scrollOffset)
        Else
            _scrollBar.TrackRect = Rectangle.Empty
            _scrollBar.ThumbRect = Rectangle.Empty
        End If

        _layoutDirty = False
    End Sub

    Private Function BuildCardLayout(layoutWidth As Single, searchTokens As String(), target As List(Of CardLayout)) As Integer
        If target Is Nothing Then Throw New ArgumentNullException(NameOf(target))
        If layoutWidth <= 0 Then Return 0

        Dim s As Single = DpiScale()
        Dim spacing As Single = _cardSpacing * s
        Dim padX As Single = _cardHorizontalPadding * s
        Dim padY As Single = _cardVerticalPadding * s
        Dim textGuard As Single = TextRenderGuard(s)
        Dim minW As Single = _cardMinWidth * s
        Dim minH As Single = _cardMinHeight * s

        Dim x As Single = 0.0F
        Dim y As Single = 0.0F
        Dim rowH As Single = 0.0F
        Dim hasAny As Boolean = False
        Dim dw = D2DHelper.GetDWriteFactory()
        Dim sizePx As Single = Font.SizeInPoints * (96.0F / 72.0F) * s

        Using fmt = D2DHelper.CreateTextFormat(Font, sizePx)
            fmt.WordWrapping = WordWrapping.NoWrap
            For i As Integer = 0 To _items.Count - 1
                Dim it = _items(i)
                If it Is Nothing OrElse Not 标题匹配搜索(it.Text, searchTokens) Then Continue For

                Dim text As String = If(it.Text, "")
                Dim textW As Single = 0.0F
                Dim textH As Single = Math.Max(1.0F, Font.Height * s)
                If text.Length > 0 Then
                    Using layout = dw.CreateTextLayout(text, fmt, 100000.0F, 100000.0F)
                        Dim m = layout.Metrics
                        textW = m.WidthIncludingTrailingWhitespace
                        textH = Math.Max(1.0F, m.Height)
                    End Using
                End If

                Dim cardW As Single = Math.Max(minW, textW + (padX + textGuard) * 2.0F)
                Dim cardH As Single = Math.Max(minH, textH + (padY + textGuard) * 2.0F)
                If cardW > layoutWidth Then cardW = layoutWidth

                If x > 0.0F AndAlso x + cardW > layoutWidth Then
                    x = 0.0F
                    y += rowH + spacing
                    rowH = 0.0F
                End If

                target.Add(New CardLayout With {
                    .Index = i,
                    .Bounds = New RectangleF(x, y, cardW, cardH)
                })
                hasAny = True
                x += cardW + spacing
                If cardH > rowH Then rowH = cardH
            Next
        End Using

        If Not hasAny Then Return 0
        Return CInt(Math.Ceiling(y + rowH))
    End Function

    Private Sub ClampScrollOffset()
        Dim maxOff As Integer = Math.Max(0, _contentHeight - CInt(Math.Floor(_cardsViewportRect.Height)))
        If _scrollOffset < 0 Then _scrollOffset = 0
        If _scrollOffset > maxOff Then _scrollOffset = maxOff
    End Sub

    Private Function HitTestCard(p As Point) As Integer
        EnsureLayout()
        If Not _cardsViewportRect.Contains(p) Then Return -1
        For i As Integer = 0 To _layoutCache.Count - 1
            Dim l = _layoutCache(i)
            Dim r As New RectangleF(
                _cardsViewportRect.X + l.Bounds.X,
                _cardsViewportRect.Y + l.Bounds.Y - _scrollOffset,
                l.Bounds.Width,
                l.Bounds.Height)
            If r.Contains(p) Then Return l.Index
        Next
        Return -1
    End Function

#End Region

#Region "绘制"

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        EnsureLayout()

        Dim ssaa As Integer = Math.Max(1, CInt(_superSamplingScale))
        If Class1.GlobalSSAA <> Class1.SuperSamplingScaleEnum.OFF Then ssaa = Math.Max(ssaa, CInt(Class1.GlobalSSAA))

        Using scope = D2DHelperV2.BeginPaint(e, Me, ssaa)
            If scope Is Nothing Then Return
            Dim compositor = scope.Compositor

            If _backgroundSource IsNot Nothing Then
                BackgroundPenetrationV2.PaintBackground(Me, scope, _backgroundSource)
            ElseIf MyBase.BackColor.A > 0 AndAlso MyBase.BackColor.A < 255 Then
                Dim brush = compositor.BrushCache.[Get](scope.BackgroundLayer, MyBase.BackColor)
                If brush IsNot Nothing Then
                    scope.BackgroundLayer.FillRectangle(D2DHelper.ToD2DRect(New RectangleF(0, 0, Width, Height)), brush)
                End If
            End If

            Dim gRT As ID2D1RenderTarget = scope.GraphicsLayer
            DrawBackgroundAndFrame(gRT, compositor.BrushCache)
            DrawCards(gRT, compositor.BrushCache)
            If _showVScroll Then
                Dim s As Single = DpiScale()
                _scrollBar.Draw_D2D(gRT, Width, Height,
                                    CInt(Math.Round(_borderSize * s)),
                                    CInt(Math.Round(_borderRadius * s)),
                                    CInt(Math.Round(_scrollBarWidth * s)),
                                    _scrollBarTrackColor, _scrollBarThumbColor, _scrollBarThumbHoverColor)
            End If

            scope.FlushGraphics()

            Dim textRT As ID2D1RenderTarget = scope.TextLayer
            DrawCardTexts(textRT, compositor.TextFormatCache, compositor.BrushCache)

            If Not Enabled AndAlso _disabledOverlayColor.A > 0 Then
                DrawDisabledOverlay(scope.DCRenderTarget, compositor.BrushCache)
            End If
        End Using
    End Sub

    Private Sub DrawBackgroundAndFrame(rt As ID2D1RenderTarget, brushCache As D2DHelper.SolidColorBrushCache)
        Dim s As Single = DpiScale()
        Dim rect As New RectangleF(0, 0, Width, Height)
        If _borderSize > 0 Then
            Dim half As Single = _borderSize * s / 2.0F
            rect.Inflate(-half, -half)
        End If
        Dim radius As Single = _borderRadius * s
        Dim backColorMask As Color = MyBase.BackColor

        If radius > 0 Then
            Using geo = RectangleRenderer.创建圆角矩形几何(rect, radius)
                If backColorMask.A > 0 AndAlso backColorMask.A < 255 Then
                    RectangleRenderer.绘制圆角背景_D2D(rt, geo, rect, backColorMask, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
                End If
                If _backColor1.A > 0 Then
                    RectangleRenderer.绘制圆角背景_D2D(rt, geo, rect, _backColor1, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
                End If
                If _borderSize > 0 AndAlso _borderColor.A > 0 Then
                    RectangleRenderer.绘制圆角边框_D2D(rt, geo, _borderColor, _borderSize * s, brushCache)
                End If
            End Using
        Else
            If backColorMask.A > 0 AndAlso backColorMask.A < 255 Then
                RectangleRenderer.绘制矩形背景_D2D(rt, rect, backColorMask, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
            End If
            If _backColor1.A > 0 Then
                RectangleRenderer.绘制矩形背景_D2D(rt, rect, _backColor1, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
            End If
            If _borderSize > 0 AndAlso _borderColor.A > 0 Then
                RectangleRenderer.绘制矩形边框_D2D(rt, rect, _borderColor, _borderSize * s, brushCache)
            End If
        End If
    End Sub

    Private Sub DrawCards(rt As ID2D1RenderTarget, brushCache As D2DHelper.SolidColorBrushCache)
        If _cardsViewportRect.Width <= 0 OrElse _cardsViewportRect.Height <= 0 Then Return
        rt.PushAxisAlignedClip(New Vortice.RawRectF(_cardsViewportRect.Left, _cardsViewportRect.Top, _cardsViewportRect.Right, _cardsViewportRect.Bottom),
                               AntialiasMode.PerPrimitive)
        Try
            Dim s As Single = DpiScale()
            For Each l In _layoutCache
                If l.Index < 0 OrElse l.Index >= _items.Count Then Continue For
                Dim it = _items(l.Index)
                If it Is Nothing Then Continue For
                Dim drawRect As New RectangleF(
                    _cardsViewportRect.X + l.Bounds.X,
                    _cardsViewportRect.Y + l.Bounds.Y - _scrollOffset,
                    l.Bounds.Width,
                    l.Bounds.Height)
                If drawRect.Bottom < _cardsViewportRect.Top OrElse drawRect.Top > _cardsViewportRect.Bottom Then Continue For

                Dim borderSize As Integer = If(it.BorderSize >= 0, it.BorderSize, _cardBorderSize)
                If borderSize > 0 Then
                    Dim half As Single = borderSize * s / 2.0F
                    drawRect.Inflate(-half, -half)
                End If

                Dim bg As Color = If(it.BackColor.IsEmpty, _cardBackColor, it.BackColor)
                Dim bc As Color = If(it.BorderColor.IsEmpty, _cardBorderColor, it.BorderColor)
                Dim radius As Single = _cardRadius * s

                If radius > 0 Then
                    Using geo = RectangleRenderer.创建圆角矩形几何(drawRect, radius)
                        If bg.A > 0 Then RectangleRenderer.绘制圆角背景_D2D(rt, geo, drawRect, bg, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
                        DrawCardOverlay(rt, geo, drawRect, l.Index, brushCache)
                        If borderSize > 0 AndAlso bc.A > 0 Then RectangleRenderer.绘制圆角边框_D2D(rt, geo, bc, borderSize * s, brushCache)
                    End Using
                Else
                    If bg.A > 0 Then RectangleRenderer.绘制矩形背景_D2D(rt, drawRect, bg, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
                    DrawCardOverlay(rt, Nothing, drawRect, l.Index, brushCache)
                    If borderSize > 0 AndAlso bc.A > 0 Then RectangleRenderer.绘制矩形边框_D2D(rt, drawRect, bc, borderSize * s, brushCache)
                End If
            Next
        Finally
            rt.PopAxisAlignedClip()
        End Try
    End Sub

    Private Sub DrawCardOverlay(rt As ID2D1RenderTarget, geo As ID2D1Geometry, rect As RectangleF, index As Integer,
                                brushCache As D2DHelper.SolidColorBrushCache)
        Dim overlay As Color = Color.Empty
        If _pressedIndex = index Then
            overlay = _pressedOverlayColor
        ElseIf _hoverIndex = index Then
            overlay = _hoverOverlayColor
        End If
        If overlay.A <= 0 Then Return
        If geo IsNot Nothing Then
            RectangleRenderer.绘制圆角背景_D2D(rt, geo, rect, overlay, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
        Else
            RectangleRenderer.绘制矩形背景_D2D(rt, rect, overlay, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
        End If
    End Sub

    Private Sub DrawCardTexts(rt As ID2D1RenderTarget, textFormatCache As D2DHelper.TextFormatCache,
                              brushCache As D2DHelper.SolidColorBrushCache)
        If _cardsViewportRect.Width <= 0 OrElse _cardsViewportRect.Height <= 0 Then Return
        rt.PushAxisAlignedClip(New Vortice.RawRectF(_cardsViewportRect.Left, _cardsViewportRect.Top, _cardsViewportRect.Right, _cardsViewportRect.Bottom),
                               AntialiasMode.PerPrimitive)
        Try
            Dim s As Single = DpiScale()
            Dim padX As Single = _cardHorizontalPadding * s
            For Each l In _layoutCache
                If l.Index < 0 OrElse l.Index >= _items.Count Then Continue For
                Dim it = _items(l.Index)
                If it Is Nothing Then Continue For
                Dim drawRect As New RectangleF(
                    _cardsViewportRect.X + l.Bounds.X,
                    _cardsViewportRect.Y + l.Bounds.Y - _scrollOffset,
                    l.Bounds.Width,
                    l.Bounds.Height)
                If drawRect.Bottom < _cardsViewportRect.Top OrElse drawRect.Top > _cardsViewportRect.Bottom Then Continue For
                Dim textRect As New RectangleF(drawRect.X + padX, drawRect.Y, Math.Max(0.0F, drawRect.Width - padX * 2.0F), drawRect.Height)
                Dim fc As Color = If(it.ForeColor.IsEmpty, MyBase.ForeColor, it.ForeColor)
                D2DTextRenderer.DrawText(rt, If(it.Text, ""), Font, textRect, fc,
                                         TextFormatFlags.NoPadding Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine,
                                         s, textFormatCache, brushCache)
            Next
        Finally
            rt.PopAxisAlignedClip()
        End Try
    End Sub

    Private Sub DrawDisabledOverlay(rt As ID2D1RenderTarget, brushCache As D2DHelper.SolidColorBrushCache)
        Dim s As Single = DpiScale()
        Dim rect As New RectangleF(0, 0, Width, Height)
        If _borderSize > 0 Then
            Dim half As Single = _borderSize * s / 2.0F
            rect.Inflate(-half, -half)
        End If
        Dim radius As Single = _borderRadius * s
        If radius > 0 Then
            Using geo = RectangleRenderer.创建圆角矩形几何(rect, radius)
                RectangleRenderer.绘制圆角背景_D2D(rt, geo, rect, _disabledOverlayColor, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
            End Using
        Else
            RectangleRenderer.绘制矩形背景_D2D(rt, rect, _disabledOverlayColor, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
        End If
    End Sub

#End Region

#Region "鼠标键盘"

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        EnsureLayout()

        If _showVScroll AndAlso _scrollBar.IsDragging Then
            Dim newOff = _scrollBar.DragMove(e.Y, Math.Max(1, _contentHeight), Math.Max(1, CInt(Math.Floor(_cardsViewportRect.Height))))
            If newOff <> _scrollOffset Then
                _scrollOffset = newOff
                _layoutDirty = True
                Invalidate()
            End If
            Return
        End If

        Dim oldHover As Integer = _hoverIndex
        _hoverIndex = HitTestCard(e.Location)
        Dim needInvalidate As Boolean = oldHover <> _hoverIndex
        If _showVScroll AndAlso _scrollBar.UpdateHover(e.Location) Then needInvalidate = True

        If _hoverIndex >= 0 Then
            Cursor = Cursors.Hand
        Else
            Cursor = Cursors.Default
        End If

        If needInvalidate Then Invalidate()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button <> MouseButtons.Left Then Return
        Focus()
        EnsureLayout()

        If _showVScroll AndAlso _scrollBar.BeginDrag(e.Location, _scrollOffset) Then
            Invalidate()
            Return
        End If

        If _showVScroll AndAlso Not _scrollBar.TrackRect.IsEmpty Then
            Dim newOff = _scrollBar.TrackClick(e.Location, _scrollOffset, Math.Max(1, _contentHeight), Math.Max(1, CInt(Math.Floor(_cardsViewportRect.Height))))
            If newOff <> _scrollOffset Then
                _scrollOffset = newOff
                _layoutDirty = True
                Invalidate()
                Return
            End If
        End If

        _pressedIndex = HitTestCard(e.Location)
        If _pressedIndex >= 0 Then Invalidate()
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If _scrollBar.IsDragging Then
            _scrollBar.EndDrag()
            Invalidate()
            Return
        End If

        If e.Button <> MouseButtons.Left Then Return
        Dim pressed As Integer = _pressedIndex
        _pressedIndex = -1
        Dim hit As Integer = HitTestCard(e.Location)
        If pressed >= 0 Then Invalidate()
        If pressed >= 0 AndAlso pressed = hit AndAlso pressed < _items.Count Then
            Dim it = _items(pressed)
            RaiseEvent ItemClick(Me, New MemberItemEventArgs(it, pressed))
            it.ClickAction?.Invoke()
        End If
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        Dim needInvalidate As Boolean = _hoverIndex >= 0
        _hoverIndex = -1
        If _scrollBar.ResetHover() Then needInvalidate = True
        Cursor = Cursors.Default
        If needInvalidate Then Invalidate()
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        EnsureLayout()
        If Not _showVScroll Then Return
        Dim newOff = ScrollBarRenderer.HandleWheel(e.Delta, _scrollOffset, Math.Max(1, _contentHeight), Math.Max(1, CInt(Math.Floor(_cardsViewportRect.Height))), _scrollStep)
        If newOff <> _scrollOffset Then
            _scrollOffset = newOff
            _layoutDirty = True
            Invalidate()
        End If
    End Sub

#End Region

#Region "生命周期"

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        _layoutDirty = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        _layoutDirty = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        _layoutDirty = True
        D2DHelperV2.RefreshFontDependentRendering(Me)
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        _layoutDirty = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        _pressedIndex = -1
        _hoverIndex = -1
        Invalidate()
    End Sub

#End Region

#Region "禁用属性"

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
            Return ImageLayout.None
        End Get
        Set(value As ImageLayout)
        End Set
    End Property

#End Region

End Class
