Imports System.ComponentModel
Imports System.Numerics
Imports Vortice.Direct2D1
Imports Vortice.DirectWrite

''' <summary>
''' 轻量状态卡片面板：以 Direct2D 独立渲染两行状态小卡片，并提供平滑竖向滚动。
''' </summary>
<DefaultProperty("Items")>
Partial Public Class EasyStatesPanel
    Implements V3_IGpuRenderable, V3_IGpuInvalidationSource

#Region "数据模型"

    <TypeConverter(GetType(ExpandableObjectConverter))>
    Public Class StateItem
        Private _owner As EasyStatesPanel
        Private _text As String = ""
        Private _subText As String = ""
        Private _foreColor As Color = Color.Empty

        Public Sub New()
        End Sub

        Public Sub New(text As String, subText As String)
            _text = If(text, "")
            _subText = If(subText, "")
        End Sub

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Friend Property Owner As EasyStatesPanel
            Get
                Return _owner
            End Get
            Set(value As EasyStatesPanel)
                _owner = value
            End Set
        End Property

        <Category("LakeUI"), Description("状态卡主文本。"), DefaultValue(GetType(String), "")>
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

        <Category("LakeUI"), Description("状态卡副文本。"), DefaultValue(GetType(String), "")>
        Public Property SubText As String
            Get
                Return _subText
            End Get
            Set(value As String)
                Dim v As String = If(value, "")
                If _subText = v Then Return
                _subText = v
                _owner?.NotifyItemContentChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("状态卡主文本独立颜色。留空时使用 EasyStatesPanel.ForeColor。"), DefaultValue(GetType(Color), "")>
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

        Public Overrides Function ToString() As String
            If String.IsNullOrEmpty(_text) Then Return "(空)"
            Return _text
        End Function
    End Class

    Public Class StateItemCollection
        Inherits ObjectModel.Collection(Of StateItem)

        Private ReadOnly _owner As EasyStatesPanel
        Private _suspendCount As Integer = 0
        Private _dirty As Boolean = False

        Friend Sub New(owner As EasyStatesPanel)
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

        Public Overloads Function Add(text As String, subText As String) As StateItem
            Dim it As New StateItem(text, subText)
            Add(it)
            Return it
        End Function

        Public Overloads Sub AddRange(items As IEnumerable(Of StateItem))
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

        Private Sub NotifyChanged()
            If _suspendCount > 0 Then
                _dirty = True
                Return
            End If
            _owner.NotifyItemContentChanged()
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As StateItem)
            If item Is Nothing Then item = New StateItem()
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

        Protected Overrides Sub SetItem(index As Integer, item As StateItem)
            If item Is Nothing Then item = New StateItem()
            Dim old = Me(index)
            If old IsNot Nothing Then old.Owner = Nothing
            item.Owner = _owner
            MyBase.SetItem(index, item)
            NotifyChanged()
        End Sub
    End Class

#End Region

#Region "字段"

    Private ReadOnly _items As New StateItemCollection(Me)
    Private ReadOnly _scrollBar As New V3_ScrollBarRenderer()
    Private ReadOnly _layoutCache As New List(Of CardLayout)
    Private ReadOnly _scrollAnimationHelper As New V3_AnimationHelper(Me) With {.Duration = 220, .FPS = 60}

    Private Structure CardLayout
        Public Index As Integer
        Public Bounds As RectangleF
    End Structure

    Private _layoutDirty As Boolean = True
    Private _contentHeight As Integer = 0
    Private _scrollOffset As Single = 0.0F
    Private _scrollTargetOffset As Single = 0.0F
    Private _scrollAnimationRunning As Boolean = False
    Private _scrollAnimationLastTicks As Long = 0
    Private _showVScroll As Boolean = False
    Private _cardsViewportRect As RectangleF = RectangleF.Empty

    Private Const ScrollStopThreshold As Single = 0.45F

#End Region

#Region "构造"

    Public Sub New()
        InitializeComponent()
        _scrollAnimationHelper.DirtyProvider = AddressOf 滚动动画脏区
        SetStyle(ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.Selectable Or
                 ControlStyles.SupportsTransparentBackColor, True)
        DoubleBuffered = True
        TabStop = True
        Size = New Size(420, 180)
        MyBase.Padding = New Padding(0)
        MyBase.BackColor = Color.Transparent
        MyBase.ForeColor = Color.MediumPurple
    End Sub

#End Region

#Region "通用"

    Private Function DpiScale() As Single
        Return V3_DpiContext.FromControl(Me).Scale
    End Function

    Private Sub 请求V3渲染(Optional immediate As Boolean = False)
        请求V3渲染(New Rectangle(Point.Empty, Me.Size), immediate)
    End Sub

    Private Sub 请求V3渲染(dirtyRect As Rectangle, Optional immediate As Boolean = False)
        If Me.IsDisposed Then Return
        V3_InvalidationRouter.RequestRender(Me, dirtyRect)
    End Sub

    Private Sub SetValue(Of T)(ByRef field As T, value As T, Optional affectsLayout As Boolean = False)
        If EqualityComparer(Of T).Default.Equals(field, value) Then Return
        field = value
        If affectsLayout Then _layoutDirty = True
        请求V3渲染()
    End Sub

    Friend Sub NotifyItemContentChanged()
        _layoutDirty = True
        ClampScrollOffsets()
        请求V3渲染()
    End Sub

    Public Sub Redraw()
        _layoutDirty = True
        ClampScrollOffsets()
        请求V3渲染()
    End Sub

    Private Shared Function TextRenderGuard(dpiScale As Single) As Single
        Return Math.Max(2.0F, CSng(Math.Ceiling(2.0F * Math.Max(1.0F, dpiScale))))
    End Function

    Private _subTextFontCache As Font
    Private _subTextFontCacheKey As String

    Private Function GetSubTextFont() As Font
        Dim size As Single = Math.Max(1.0F, CSng(_subTextSize))
        Dim key As String = Me.Font.FontFamily.Name & "|" & size.ToString(Globalization.CultureInfo.InvariantCulture)
        If _subTextFontCache IsNot Nothing AndAlso _subTextFontCacheKey = key Then Return _subTextFontCache
        ReleaseSubTextFont()
        _subTextFontCache = New Font(Me.Font.FontFamily, size, System.Drawing.FontStyle.Regular, GraphicsUnit.Point)
        _subTextFontCacheKey = key
        Return _subTextFontCache
    End Function

    Private Sub ReleaseSubTextFont()
        If _subTextFontCache IsNot Nothing Then
            Try : _subTextFontCache.Dispose() : Catch : End Try
            _subTextFontCache = Nothing
        End If
        _subTextFontCacheKey = Nothing
    End Sub

#End Region

#Region "公开属性 - 数据"

    <DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
     Editor("System.ComponentModel.Design.CollectionEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing"),
     Category("LakeUI"), Description("状态卡集合。"), Browsable(True)>
    Public ReadOnly Property Items As StateItemCollection
        Get
            Return _items
        End Get
    End Property

#End Region

#Region "公开属性 - 外观"

    Private _backgroundSource As Control = Nothing
    <Category("LakeUI"),
     Description("背景采样源。设置后记录关联源控件；V3 渲染由窗口合成器统一调度。"),
     DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource Is value Then Return
            _backgroundSource = D3D_BackgroundPenetration.SetBackgroundSource(Me, _backgroundSource, value)
            请求V3渲染()
        End Set
    End Property

    Private _superSamplingScale As GlobalOptions.SuperSamplingScaleEnum = GlobalOptions.SuperSamplingScaleEnum.OFF
    <Category("LakeUI"), Description(GlobalOptions.超采样抗锯齿描述词), DefaultValue(GetType(GlobalOptions.SuperSamplingScaleEnum), "OFF"), Browsable(True)>
    Public Property SuperSamplingScale As GlobalOptions.SuperSamplingScaleEnum
        Get
            Return _superSamplingScale
        End Get
        Set(value As GlobalOptions.SuperSamplingScaleEnum)
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

    <Category("LakeUI"), Description("默认主文本颜色"), DefaultValue(GetType(Color), "MediumPurple"), Browsable(True)>
    Public Overrides Property ForeColor As Color
        Get
            Return MyBase.ForeColor
        End Get
        Set(value As Color)
            If MyBase.ForeColor = value Then Return
            MyBase.ForeColor = value
            请求V3渲染()
        End Set
    End Property

    Private _subTextForeColor As Color = Color.Gray
    <Category("LakeUI"), Description("副文本颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property SubTextForeColor As Color
        Get
            Return _subTextForeColor
        End Get
        Set(value As Color)
            SetValue(_subTextForeColor, value)
        End Set
    End Property

    Private _subTextSize As Integer = 9
    <Category("LakeUI"), Description("副文本字号"), DefaultValue(9), Browsable(True)>
    Public Property SubTextSize As Integer
        Get
            Return _subTextSize
        End Get
        Set(value As Integer)
            value = Math.Max(1, value)
            If _subTextSize = value Then Return
            _subTextSize = value
            ReleaseSubTextFont()
            _layoutDirty = True
            请求V3渲染()
        End Set
    End Property

    Private _mainSubTextSpacing As Integer = 3
    <Category("LakeUI"), Description("主副文本间距"), DefaultValue(3), Browsable(True)>
    Public Property MainSubTextSpacing As Integer
        Get
            Return _mainSubTextSpacing
        End Get
        Set(value As Integer)
            SetValue(_mainSubTextSpacing, Math.Max(0, value), True)
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

#Region "公开属性 - 状态卡"

    Private _cardBackColor As Color = Color.FromArgb(120, 0, 0, 0)
    <Category("LakeUI"), Description("状态卡背景颜色"), DefaultValue(GetType(Color), "120, 0, 0, 0"), Browsable(True)>
    Public Property CardBackColor As Color
        Get
            Return _cardBackColor
        End Get
        Set(value As Color)
            SetValue(_cardBackColor, value)
        End Set
    End Property

    Private _cardBorderColor As Color = Color.Transparent
    <Category("LakeUI"), Description("状态卡边框颜色"), DefaultValue(GetType(Color), "Transparent"), Browsable(True)>
    Public Property CardBorderColor As Color
        Get
            Return _cardBorderColor
        End Get
        Set(value As Color)
            SetValue(_cardBorderColor, value)
        End Set
    End Property

    Private _cardBorderSize As Integer = 0
    <Category("LakeUI"), Description("状态卡边框宽度"), DefaultValue(0), Browsable(True)>
    Public Property CardBorderSize As Integer
        Get
            Return _cardBorderSize
        End Get
        Set(value As Integer)
            SetValue(_cardBorderSize, Math.Max(0, value), True)
        End Set
    End Property

    Private _cardRadius As Integer = 10
    <Category("LakeUI"), Description("状态卡圆角半径"), DefaultValue(10), Browsable(True)>
    Public Property CardRadius As Integer
        Get
            Return _cardRadius
        End Get
        Set(value As Integer)
            SetValue(_cardRadius, Math.Max(0, value))
        End Set
    End Property

    Private _cardSpacing As Integer = 10
    <Category("LakeUI"), Description("状态卡之间的水平和垂直间距"), DefaultValue(10), Browsable(True)>
    Public Property CardSpacing As Integer
        Get
            Return _cardSpacing
        End Get
        Set(value As Integer)
            SetValue(_cardSpacing, Math.Max(0, value), True)
        End Set
    End Property

    Private _cardHorizontalPadding As Integer = 10
    <Category("LakeUI"), Description("状态卡文字左右留白"), DefaultValue(10), Browsable(True)>
    Public Property CardHorizontalPadding As Integer
        Get
            Return _cardHorizontalPadding
        End Get
        Set(value As Integer)
            SetValue(_cardHorizontalPadding, Math.Max(0, value), True)
        End Set
    End Property

    Private _cardVerticalPadding As Integer = 6
    <Category("LakeUI"), Description("状态卡文字上下留白"), DefaultValue(6), Browsable(True)>
    Public Property CardVerticalPadding As Integer
        Get
            Return _cardVerticalPadding
        End Get
        Set(value As Integer)
            SetValue(_cardVerticalPadding, Math.Max(0, value), True)
        End Set
    End Property

    Private _cardPreferredWidth As Integer = 176
    <Category("LakeUI"), Description("状态卡首选宽度。实际宽度会根据可用区域和列数自动填充调整。"), DefaultValue(176), Browsable(True)>
    Public Property CardPreferredWidth As Integer
        Get
            Return _cardPreferredWidth
        End Get
        Set(value As Integer)
            SetValue(_cardPreferredWidth, Math.Max(1, value), True)
        End Set
    End Property

    Private _cardMinWidth As Integer = 120
    <Category("LakeUI"), Description("状态卡最小宽度"), DefaultValue(120), Browsable(True)>
    Public Property CardMinWidth As Integer
        Get
            Return _cardMinWidth
        End Get
        Set(value As Integer)
            SetValue(_cardMinWidth, Math.Max(1, value), True)
        End Set
    End Property

    Private _cardMinHeight As Integer = 55
    <Category("LakeUI"), Description("状态卡最小高度"), DefaultValue(55), Browsable(True)>
    Public Property CardMinHeight As Integer
        Get
            Return _cardMinHeight
        End Get
        Set(value As Integer)
            SetValue(_cardMinHeight, Math.Max(1, value), True)
        End Set
    End Property

#End Region

#Region "公开属性 - 滚动"

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

    Private _smoothScroll As Boolean = True
    <Category("LakeUI"), Description("是否启用平滑滚动"), DefaultValue(True), Browsable(True)>
    Public Property SmoothScroll As Boolean
        Get
            Return _smoothScroll
        End Get
        Set(value As Boolean)
            If _smoothScroll = value Then Return
            _smoothScroll = value
            If Not _smoothScroll Then SetScrollOffset(_scrollTargetOffset, False)
        End Set
    End Property

    <Category("LakeUI"), Description("平滑滚动动画时长"), DefaultValue(220), Browsable(True)>
    Public Property SmoothScrollDuration As Integer
        Get
            Return _scrollAnimationHelper.Duration
        End Get
        Set(value As Integer)
            _scrollAnimationHelper.Duration = Math.Max(0, value)
        End Set
    End Property

    <Category("LakeUI"), Description("平滑滚动动画帧率。0 表示不限制帧率上限，仍使用高精度计时器驱动。"), DefaultValue(60), Browsable(True)>
    Public Property SmoothScrollFPS As Integer
        Get
            Return _scrollAnimationHelper.FPS
        End Get
        Set(value As Integer)
            _scrollAnimationHelper.FPS = Math.Max(0, value)
        End Set
    End Property

    Private _scrollBarTrackColor As Color = Color.FromArgb(20, 220, 220, 220)
    <Category("LakeUI"), Description("滚动条轨道颜色"), DefaultValue(GetType(Color), "20, 220, 220, 220"), Browsable(True)>
    Public Property ScrollBarTrackColor As Color
        Get
            Return _scrollBarTrackColor
        End Get
        Set(value As Color)
            SetValue(_scrollBarTrackColor, value)
        End Set
    End Property

    Private _scrollBarThumbColor As Color = Color.FromArgb(80, 220, 220, 220)
    <Category("LakeUI"), Description("滚动条滑块颜色"), DefaultValue(GetType(Color), "80, 220, 220, 220"), Browsable(True)>
    Public Property ScrollBarThumbColor As Color
        Get
            Return _scrollBarThumbColor
        End Get
        Set(value As Color)
            SetValue(_scrollBarThumbColor, value)
        End Set
    End Property

    Private _scrollBarThumbHoverColor As Color = Color.FromArgb(120, 220, 220, 220)
    <Category("LakeUI"), Description("滚动条滑块悬停颜色"), DefaultValue(GetType(Color), "120, 220, 220, 220"), Browsable(True)>
    Public Property ScrollBarThumbHoverColor As Color
        Get
            Return _scrollBarThumbHoverColor
        End Get
        Set(value As Color)
            SetValue(_scrollBarThumbHoverColor, value)
        End Set
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property VerticalScrollOffset As Single
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
            StopScrollAnimation()
            _scrollOffset = 0.0F
            _scrollTargetOffset = 0.0F
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
            StopScrollAnimation()
            _scrollOffset = 0.0F
            _scrollTargetOffset = 0.0F
            _layoutDirty = False
            Return
        End If

        Dim tempLayout As New List(Of CardLayout)
        Dim contentH As Integer = BuildCardLayout(_cardsViewportRect.Width, tempLayout)
        Dim needScroll As Boolean = contentH > _cardsViewportRect.Height

        If needScroll Then
            Dim reserve As Single = CInt(Math.Round(_scrollBarWidth * s)) + V3_ScrollBarRenderer.Margin * 2 + _cardSpacing * s
            _cardsViewportRect.Width = Math.Max(0.0F, _cardsViewportRect.Width - reserve)
            tempLayout.Clear()
            contentH = BuildCardLayout(_cardsViewportRect.Width, tempLayout)
            needScroll = contentH > _cardsViewportRect.Height
        End If

        _layoutCache.AddRange(tempLayout)
        _contentHeight = contentH
        _showVScroll = needScroll
        ClampScrollOffsets()

        If _showVScroll Then
            ComputeScrollBarLayout(borderPx, radiusPx, inset)
        Else
            StopScrollAnimation()
            _scrollOffset = 0.0F
            _scrollTargetOffset = 0.0F
            _scrollBar.TrackRect = Rectangle.Empty
            _scrollBar.ThumbRect = Rectangle.Empty
        End If

        _layoutDirty = False
    End Sub

    Private Function BuildCardLayout(layoutWidth As Single, target As List(Of CardLayout)) As Integer
        If target Is Nothing Then Throw New ArgumentNullException(NameOf(target))
        If layoutWidth <= 0 OrElse _items.Count = 0 Then Return 0

        Dim s As Single = DpiScale()
        Dim spacing As Single = _cardSpacing * s
        Dim minW As Single = Math.Max(1.0F, _cardMinWidth * s)
        Dim preferredW As Single = Math.Max(minW, _cardPreferredWidth * s)
        Dim cardH As Single = CalculateCardHeight(s)
        Dim columns As Integer = Math.Max(1, CInt(Math.Floor((layoutWidth + spacing) / (preferredW + spacing))))

        Do While columns > 1
            Dim testW As Single = (layoutWidth - spacing * (columns - 1)) / columns
            If testW >= minW Then Exit Do
            columns -= 1
        Loop

        Dim cardW As Single = Math.Max(1.0F, (layoutWidth - spacing * (columns - 1)) / columns)
        If columns = 1 Then cardW = Math.Min(layoutWidth, Math.Max(minW, cardW))

        For i As Integer = 0 To _items.Count - 1
            Dim col As Integer = i Mod columns
            Dim row As Integer = i \ columns
            Dim x As Single = col * (cardW + spacing)
            Dim y As Single = row * (cardH + spacing)
            target.Add(New CardLayout With {
                .Index = i,
                .Bounds = New RectangleF(x, y, cardW, cardH)
            })
        Next

        Dim rows As Integer = CInt(Math.Ceiling(_items.Count / CDbl(columns)))
        Return CInt(Math.Ceiling(rows * cardH + Math.Max(0, rows - 1) * spacing))
    End Function

    Private Function CalculateCardHeight(s As Single) As Single
        Dim flags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
        Dim mainH As Single = Math.Max(1, TextRenderer.MeasureText("Ag", Font, New Size(Integer.MaxValue, Integer.MaxValue), flags).Height)
        Dim subH As Single
        Dim subFont = GetSubTextFont()
        subH = Math.Max(1, TextRenderer.MeasureText("Ag", subFont, New Size(Integer.MaxValue, Integer.MaxValue), flags).Height)
        Dim totalTextH As Single = mainH + subH + _mainSubTextSpacing * s
        Return Math.Max(_cardMinHeight * s, totalTextH + _cardVerticalPadding * s * 2.0F + TextRenderGuard(s))
    End Function

    Private Sub ComputeScrollBarLayout(borderPx As Integer, radiusPx As Integer, inset As Single)
        Dim s As Single = DpiScale()
        Dim scrollW As Integer = CInt(Math.Round(_scrollBarWidth * s))
        Dim padTop As Integer = Math.Max(0, CInt(Math.Round(_cardsViewportRect.Top - inset - V3_ScrollBarRenderer.Margin)))
        Dim padBottom As Integer = Math.Max(0, CInt(Math.Round(Height - _cardsViewportRect.Bottom - inset - V3_ScrollBarRenderer.Margin)))
        _scrollBar.ComputeLayout(Width, Height, borderPx, radiusPx, padTop, padBottom, scrollW,
                                 Math.Max(1, _contentHeight),
                                 Math.Max(1, CInt(Math.Floor(_cardsViewportRect.Height))),
                                 CInt(Math.Round(_scrollOffset)))
    End Sub

    Private Sub UpdateScrollBarForCurrentOffset()
        If Not _showVScroll Then Return
        Dim s As Single = DpiScale()
        Dim borderPx As Integer = CInt(Math.Round(_borderSize * s))
        Dim radiusPx As Integer = CInt(Math.Round(_borderRadius * s))
        Dim inset As Single = Math.Max(borderPx, If(_borderRadius > 0, radiusPx / 2.0F, 0.0F))
        ComputeScrollBarLayout(borderPx, radiusPx, inset)
    End Sub

    Private Function MaxScrollOffset() As Single
        Return Math.Max(0.0F, _contentHeight - CSng(Math.Floor(_cardsViewportRect.Height)))
    End Function

    Private Function ClampScrollOffsetValue(value As Single) As Single
        Return Math.Max(0.0F, Math.Min(MaxScrollOffset(), value))
    End Function

    Private Sub ClampScrollOffsets()
        Dim maxOff As Single = MaxScrollOffset()
        _scrollOffset = Math.Max(0.0F, Math.Min(maxOff, _scrollOffset))
        _scrollTargetOffset = Math.Max(0.0F, Math.Min(maxOff, _scrollTargetOffset))
    End Sub

#End Region

#Region "绘制"

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me, 1) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        If context Is Nothing OrElse Width <= 0 OrElse Height <= 0 Then Return
        EnsureLayout()

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(Me, _backgroundSource, New RectangleF(0, 0, Width, Height))
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Width, Height), MyBase.BackColor)
        End If
        DrawBackgroundAndFrame_GPU(context)
        DrawCards_GPU(context)
        If _showVScroll Then DrawScrollBar_GPU(context)
        DrawCardTexts_GPU(context)
        If Not Enabled AndAlso _disabledOverlayColor.A > 0 Then DrawDisabledOverlay_GPU(context)
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Private Sub DrawBackgroundAndFrame_GPU(context As D3D_PaintContext)
        Dim s As Single = DpiScale()
        Dim rect As New RectangleF(0, 0, Width, Height)
        If _borderSize > 0 Then
            Dim half As Single = _borderSize * s / 2.0F
            rect.Inflate(-half, -half)
        End If
        Dim radius As Single = _borderRadius * s
        If _backColor1.A > 0 Then FillRoundedRect_GPU(context, rect, radius, _backColor1)
        If _borderSize > 0 AndAlso _borderColor.A > 0 Then DrawRoundedBorder_GPU(context, rect, radius, _borderColor, _borderSize * s)
    End Sub

    Private Sub DrawCards_GPU(context As D3D_PaintContext)
        If _cardsViewportRect.Width <= 0 OrElse _cardsViewportRect.Height <= 0 Then Return
        Using context.PushClip(_cardsViewportRect)
            Dim s As Single = DpiScale()
            For Each l In _layoutCache
                Dim drawRect As New RectangleF(
                    _cardsViewportRect.X + l.Bounds.X,
                    _cardsViewportRect.Y + l.Bounds.Y - _scrollOffset,
                    l.Bounds.Width,
                    l.Bounds.Height)
                If drawRect.Bottom < _cardsViewportRect.Top OrElse drawRect.Top > _cardsViewportRect.Bottom Then Continue For

                If _cardBorderSize > 0 Then
                    Dim half As Single = _cardBorderSize * s / 2.0F
                    drawRect.Inflate(-half, -half)
                End If

                Dim radius As Single = _cardRadius * s
                If _cardBackColor.A > 0 Then FillRoundedRect_GPU(context, drawRect, radius, _cardBackColor)
                If _cardBorderSize > 0 AndAlso _cardBorderColor.A > 0 Then DrawRoundedBorder_GPU(context, drawRect, radius, _cardBorderColor, _cardBorderSize * s)
            Next
        End Using
    End Sub

    Private Sub DrawCardTexts_GPU(context As D3D_PaintContext)
        If _cardsViewportRect.Width <= 0 OrElse _cardsViewportRect.Height <= 0 Then Return
        Using context.PushClip(_cardsViewportRect)
            Dim s As Single = DpiScale()
            Dim padX As Single = _cardHorizontalPadding * s
            Dim padY As Single = _cardVerticalPadding * s
            Dim lineGap As Single = _mainSubTextSpacing * s
            Dim subFont = GetSubTextFont()

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

                Dim textRect As New RectangleF(drawRect.X + padX,
                                               drawRect.Y + padY,
                                               Math.Max(0.0F, drawRect.Width - padX * 2.0F),
                                               Math.Max(0.0F, drawRect.Height - padY * 2.0F))
                If textRect.Width <= 0 OrElse textRect.Height <= 0 Then Continue For

                Dim mainText As String = If(it.Text, "")
                Dim subText As String = If(it.SubText, "")
                If String.IsNullOrEmpty(mainText) AndAlso String.IsNullOrEmpty(subText) Then Continue For

                Dim mainH As Single = If(String.IsNullOrEmpty(mainText), 0.0F, TextRenderer.MeasureText(mainText, Font).Height)
                Dim subH As Single = If(String.IsNullOrEmpty(subText), 0.0F, TextRenderer.MeasureText(subText, subFont).Height)
                Dim totalH As Single = mainH + If(subH > 0, lineGap + subH, 0.0F)
                Dim startY As Single = textRect.Y + Math.Max(0.0F, (textRect.Height - totalH) / 2.0F)

                If Not String.IsNullOrEmpty(mainText) Then
                    context.DrawText(mainText, Font, If(it.ForeColor.IsEmpty, MyBase.ForeColor, it.ForeColor), New RectangleF(textRect.X, startY, textRect.Width, Math.Max(1.0F, mainH)), TextAlignment.Leading, ParagraphAlignment.Near)
                End If
                If Not String.IsNullOrEmpty(subText) Then
                    context.DrawText(subText, subFont, _subTextForeColor, New RectangleF(textRect.X, startY + mainH + lineGap, textRect.Width, Math.Max(1.0F, subH)), TextAlignment.Leading, ParagraphAlignment.Near)
                End If
            Next
        End Using
    End Sub

    Private Sub DrawScrollBar_GPU(context As D3D_PaintContext)
        If _scrollBar.TrackRect.IsEmpty Then Return
        Dim s As Single = DpiScale()
        Dim width As Single = Math.Max(1.0F, _scrollBarWidth * s)
        Dim trackArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.TrackRect.Y, width, _scrollBar.TrackRect.Height)
        Dim thumbArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.ThumbRect.Y, width, _scrollBar.ThumbRect.Height)
        FillRoundedRect_GPU(context, trackArea, Math.Min(width / 2.0F, trackArea.Height / 2.0F), _scrollBarTrackColor)
        Dim thumbColor = If(_scrollBar.IsDragging OrElse _scrollBar.IsHover, _scrollBarThumbHoverColor, _scrollBarThumbColor)
        FillRoundedRect_GPU(context, thumbArea, Math.Min(width / 2.0F, thumbArea.Height / 2.0F), thumbColor)
    End Sub

    Private Sub DrawDisabledOverlay_GPU(context As D3D_PaintContext)
        Dim s As Single = DpiScale()
        Dim rect As New RectangleF(0, 0, Width, Height)
        If _borderSize > 0 Then
            Dim half As Single = _borderSize * s / 2.0F
            rect.Inflate(-half, -half)
        End If
        FillRoundedRect_GPU(context, rect, _borderRadius * s, _disabledOverlayColor)
    End Sub

    Private Sub FillRoundedRect_GPU(context As D3D_PaintContext, rect As RectangleF, radius As Single, color As Color)
        If color.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If radius <= 0 Then
            context.DeviceContext.FillRectangle(D3D_PaintContext.ToRawRect(rect), brush)
            Return
        End If
        Using geo = D3D_RenderCore.DeviceManager.D2DFactory.CreateRoundedRectangleGeometry(New RoundedRectangle(rect, radius, radius))
            context.DeviceContext.FillGeometry(geo, brush)
        End Using
    End Sub

    Private Sub DrawRoundedBorder_GPU(context As D3D_PaintContext, rect As RectangleF, radius As Single, color As Color, strokeWidth As Single)
        If color.A = 0 OrElse strokeWidth <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If radius <= 0 Then
            context.DeviceContext.DrawRectangle(D3D_PaintContext.ToRawRect(rect), brush, strokeWidth)
            Return
        End If
        Using geo = D3D_RenderCore.DeviceManager.D2DFactory.CreateRoundedRectangleGeometry(New RoundedRectangle(rect, radius, radius))
            context.DeviceContext.DrawGeometry(geo, brush, strokeWidth)
        End Using
    End Sub


    Private Sub DrawBackgroundAndFrame(rt As ID2D1RenderTarget, brushCache As D3D_D2DInterop.SolidColorBrushCache)
        Dim s As Single = DpiScale()
        Dim rect As New RectangleF(0, 0, Width, Height)
        If _borderSize > 0 Then
            Dim half As Single = _borderSize * s / 2.0F
            rect.Inflate(-half, -half)
        End If
        Dim radius As Single = _borderRadius * s
        Dim backColorMask As Color = MyBase.BackColor

        If radius > 0 Then
            Using geo = D3D_RectangleRenderer.创建圆角矩形几何(rect, radius)
                If backColorMask.A > 0 AndAlso backColorMask.A < 255 Then
                    D3D_RectangleRenderer.绘制圆角背景_D2D(rt, geo, rect, backColorMask, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
                End If
                If _backColor1.A > 0 Then
                    D3D_RectangleRenderer.绘制圆角背景_D2D(rt, geo, rect, _backColor1, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
                End If
                If _borderSize > 0 AndAlso _borderColor.A > 0 Then
                    D3D_RectangleRenderer.绘制圆角边框_D2D(rt, geo, _borderColor, _borderSize * s, brushCache)
                End If
            End Using
        Else
            If backColorMask.A > 0 AndAlso backColorMask.A < 255 Then
                D3D_RectangleRenderer.绘制矩形背景_D2D(rt, rect, backColorMask, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
            End If
            If _backColor1.A > 0 Then
                D3D_RectangleRenderer.绘制矩形背景_D2D(rt, rect, _backColor1, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
            End If
            If _borderSize > 0 AndAlso _borderColor.A > 0 Then
                D3D_RectangleRenderer.绘制矩形边框_D2D(rt, rect, _borderColor, _borderSize * s, brushCache)
            End If
        End If
    End Sub

    Private Sub DrawCards(rt As ID2D1RenderTarget, brushCache As D3D_D2DInterop.SolidColorBrushCache)
        If _cardsViewportRect.Width <= 0 OrElse _cardsViewportRect.Height <= 0 Then Return
        rt.PushAxisAlignedClip(New Vortice.RawRectF(_cardsViewportRect.Left, _cardsViewportRect.Top, _cardsViewportRect.Right, _cardsViewportRect.Bottom),
                               AntialiasMode.PerPrimitive)
        Try
            Dim s As Single = DpiScale()
            For Each l In _layoutCache
                Dim drawRect As New RectangleF(
                    _cardsViewportRect.X + l.Bounds.X,
                    _cardsViewportRect.Y + l.Bounds.Y - _scrollOffset,
                    l.Bounds.Width,
                    l.Bounds.Height)
                If drawRect.Bottom < _cardsViewportRect.Top OrElse drawRect.Top > _cardsViewportRect.Bottom Then Continue For

                If _cardBorderSize > 0 Then
                    Dim half As Single = _cardBorderSize * s / 2.0F
                    drawRect.Inflate(-half, -half)
                End If

                Dim radius As Single = _cardRadius * s
                If radius > 0 Then
                    Using geo = D3D_RectangleRenderer.创建圆角矩形几何(drawRect, radius)
                        If _cardBackColor.A > 0 Then D3D_RectangleRenderer.绘制圆角背景_D2D(rt, geo, drawRect, _cardBackColor, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
                        If _cardBorderSize > 0 AndAlso _cardBorderColor.A > 0 Then D3D_RectangleRenderer.绘制圆角边框_D2D(rt, geo, _cardBorderColor, _cardBorderSize * s, brushCache)
                    End Using
                Else
                    If _cardBackColor.A > 0 Then D3D_RectangleRenderer.绘制矩形背景_D2D(rt, drawRect, _cardBackColor, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
                    If _cardBorderSize > 0 AndAlso _cardBorderColor.A > 0 Then D3D_RectangleRenderer.绘制矩形边框_D2D(rt, drawRect, _cardBorderColor, _cardBorderSize * s, brushCache)
                End If
            Next
        Finally
            rt.PopAxisAlignedClip()
        End Try
    End Sub

    Private Sub DrawCardTexts(rt As ID2D1RenderTarget, textFormatCache As D3D_D2DInterop.TextFormatCache,
                              brushCache As D3D_D2DInterop.SolidColorBrushCache)
        If _cardsViewportRect.Width <= 0 OrElse _cardsViewportRect.Height <= 0 Then Return
        rt.PushAxisAlignedClip(New Vortice.RawRectF(_cardsViewportRect.Left, _cardsViewportRect.Top, _cardsViewportRect.Right, _cardsViewportRect.Bottom),
                               AntialiasMode.PerPrimitive)
        Try
            Dim s As Single = DpiScale()
            Dim dw = D3D_D2DInterop.GetDWriteFactory()
            Dim mainSizePx As Single = D3D_D2DInterop.GetDWriteFontSizePx(Font, s)
            Dim padX As Single = _cardHorizontalPadding * s
            Dim padY As Single = _cardVerticalPadding * s
            Dim lineGap As Single = _mainSubTextSpacing * s
            Dim subFont = GetSubTextFont()
            Dim subSizePx As Single = D3D_D2DInterop.GetDWriteFontSizePx(subFont, s)
            Dim mainFmt = textFormatCache.Get(Font, mainSizePx, TextAlignment.Leading, ParagraphAlignment.Near, True)
            Dim subFmt = textFormatCache.Get(subFont.FontFamily.Name, Vortice.DirectWrite.FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal,
                                             subSizePx, TextAlignment.Leading, ParagraphAlignment.Near, True)

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

                    Dim textRect As New RectangleF(drawRect.X + padX,
                                                   drawRect.Y + padY,
                                                   Math.Max(0.0F, drawRect.Width - padX * 2.0F),
                                                   Math.Max(0.0F, drawRect.Height - padY * 2.0F))
                    If textRect.Width <= 0 OrElse textRect.Height <= 0 Then Continue For

                    Dim mainText As String = If(it.Text, "")
                    Dim subText As String = If(it.SubText, "")
                    If String.IsNullOrEmpty(mainText) AndAlso String.IsNullOrEmpty(subText) Then Continue For

                    Using mainLayout = dw.CreateTextLayout(mainText, mainFmt, textRect.Width, textRect.Height)
                        Using subLayout = dw.CreateTextLayout(subText, subFmt, textRect.Width, textRect.Height)
                            Dim mm = mainLayout.Metrics
                            Dim sm = subLayout.Metrics
                            Dim hasSub As Boolean = Not String.IsNullOrEmpty(subText)
                            Dim totalH As Single = Math.Max(0.0F, mm.Height) + If(hasSub, lineGap + Math.Max(0.0F, sm.Height), 0.0F)
                            Dim startY As Single = textRect.Y + Math.Max(0.0F, (textRect.Height - totalH) / 2.0F)
                            Dim mainBrush = brushCache.Get(rt, If(it.ForeColor.IsEmpty, MyBase.ForeColor, it.ForeColor))
                            If mainBrush IsNot Nothing AndAlso Not String.IsNullOrEmpty(mainText) Then
                                rt.DrawTextLayout(New Vector2(textRect.X, startY), mainLayout, mainBrush)
                            End If
                            If hasSub Then
                                Dim subBrush = brushCache.Get(rt, _subTextForeColor)
                                If subBrush IsNot Nothing Then rt.DrawTextLayout(New Vector2(textRect.X, startY + mm.Height + lineGap), subLayout, subBrush)
                            End If
                        End Using
                    End Using
            Next
        Finally
            rt.PopAxisAlignedClip()
        End Try
    End Sub

    Private Sub DrawDisabledOverlay(rt As ID2D1RenderTarget, brushCache As D3D_D2DInterop.SolidColorBrushCache)
        Dim s As Single = DpiScale()
        Dim rect As New RectangleF(0, 0, Width, Height)
        If _borderSize > 0 Then
            Dim half As Single = _borderSize * s / 2.0F
            rect.Inflate(-half, -half)
        End If
        Dim radius As Single = _borderRadius * s
        If radius > 0 Then
            Using geo = D3D_RectangleRenderer.创建圆角矩形几何(rect, radius)
                D3D_RectangleRenderer.绘制圆角背景_D2D(rt, geo, rect, _disabledOverlayColor, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
            End Using
        Else
            D3D_RectangleRenderer.绘制矩形背景_D2D(rt, rect, _disabledOverlayColor, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
        End If
    End Sub

#End Region

#Region "滚动"

    Private Function ScrollSmoothCoefficient() As Single
        If _scrollAnimationHelper.Duration <= 0 Then Return Single.MaxValue
        Return Math.Max(1.0F, 4200.0F / _scrollAnimationHelper.Duration)
    End Function

    Private Sub SetScrollOffset(value As Single, Optional animate As Boolean = False)
        EnsureLayout()
        Dim target As Single = ClampScrollOffsetValue(value)
        If animate AndAlso _smoothScroll AndAlso IsHandleCreated AndAlso _scrollAnimationHelper.Duration > 0 Then
            _scrollTargetOffset = target
            If Not _scrollAnimationRunning Then
                _scrollAnimationRunning = True
                _scrollAnimationLastTicks = Stopwatch.GetTimestamp()
                _scrollAnimationHelper.StartFrameLoop(AddressOf ScrollAnimationTick)
            End If
        Else
            StopScrollAnimation()
            _scrollOffset = target
            _scrollTargetOffset = target
        End If
        UpdateScrollBarForCurrentOffset()
        请求V3渲染()
    End Sub

    Private Sub StopScrollAnimation()
        If Not _scrollAnimationRunning Then Return
        _scrollAnimationRunning = False
        _scrollAnimationLastTicks = 0
        _scrollAnimationHelper.StopFrameLoop()
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

        _scrollTargetOffset = ClampScrollOffsetValue(_scrollTargetOffset)
        Dim diff As Single = _scrollTargetOffset - _scrollOffset
        Dim alpha As Single = 1.0F - CSng(Math.Exp(-ScrollSmoothCoefficient() * dt))
        _scrollOffset += diff * alpha
        If Math.Abs(diff) < ScrollStopThreshold Then
            _scrollOffset = _scrollTargetOffset
            StopScrollAnimation()
        End If
        ClampScrollOffsets()
        UpdateScrollBarForCurrentOffset()
        请求V3渲染()
    End Sub

    Private Sub 滚动动画脏区(helper As V3_AnimationHelper, owner As Control, sink As V3_AnimationHelper.InvalidateRegionSink)
        sink.SuppressInvalidate()
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        EnsureLayout()

        If _showVScroll AndAlso _scrollBar.IsDragging Then
            Dim newOff = _scrollBar.DragMove(e.Y, Math.Max(1, _contentHeight), Math.Max(1, CInt(Math.Floor(_cardsViewportRect.Height))))
            SetScrollOffset(newOff, False)
            Return
        End If

        If _showVScroll AndAlso _scrollBar.UpdateHover(e.Location) Then 请求V3渲染()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button <> MouseButtons.Left Then Return
        Focus()
        EnsureLayout()

        If _showVScroll AndAlso _scrollBar.BeginDrag(e.Location, CInt(Math.Round(_scrollOffset))) Then
            StopScrollAnimation()
            请求V3渲染()
            Return
        End If

        If _showVScroll AndAlso Not _scrollBar.TrackRect.IsEmpty Then
            Dim newOff = _scrollBar.TrackClick(e.Location, CInt(Math.Round(_scrollOffset)), Math.Max(1, _contentHeight), Math.Max(1, CInt(Math.Floor(_cardsViewportRect.Height))))
            If newOff <> CInt(Math.Round(_scrollOffset)) Then
                SetScrollOffset(newOff, _smoothScroll)
                Return
            End If
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If _scrollBar.IsDragging Then
            _scrollBar.EndDrag()
            请求V3渲染()
        End If
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If _scrollBar.ResetHover() Then 请求V3渲染()
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        EnsureLayout()
        If Not _showVScroll Then Return
        Dim baseOffset As Single = If(_scrollAnimationRunning, _scrollTargetOffset, _scrollOffset)
        Dim newOff As Single = baseOffset - Math.Sign(e.Delta) * _scrollStep
        SetScrollOffset(newOff, _smoothScroll)
    End Sub

#End Region

#Region "生命周期"

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        _layoutDirty = True
        ClampScrollOffsets()
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        _layoutDirty = True
        ClampScrollOffsets()
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        ReleaseSubTextFont()
        _layoutDirty = True
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        ReleaseSubTextFont()
        _layoutDirty = True
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        If Not Enabled Then StopScrollAnimation()
        请求V3渲染()
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
