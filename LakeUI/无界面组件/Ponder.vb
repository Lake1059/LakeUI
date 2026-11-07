Imports System.ComponentModel
Imports System.ComponentModel.Design
Imports System.Drawing.Drawing2D
Imports System.Linq
Imports System.Numerics
Imports System.Windows.Forms

''' <summary>
''' Ponder 教学遮罩组件。组件本身不参与布局，调用 Start(target) 后在目标上方创建 V5 GPU 遮罩。
''' </summary>
<DesignerCategory("Component"), ToolboxItem(True), DefaultEvent("ItemChanged")>
Public Class Ponder
    Inherits Component

    Public Enum TextPlacementEnum
        Auto
        Top
        Bottom
        Left
        Right
    End Enum

    Public Enum ButtonCornerEnum
        TopLeft
        TopRight
        BottomLeft
        BottomRight
    End Enum

    Public Enum ConnectorStyleEnum
        Straight
        Elbow
    End Enum

    <TypeConverter(GetType(ExpandableObjectConverter))>
    Public Class PonderItem
        Private _highlightControl As Control
        Private _text As String = String.Empty
        Private _placement As TextPlacementEnum = TextPlacementEnum.Auto
        Private _connectorEnabled As Boolean = True
        Private _clearIndices As New List(Of Integer)()

        <Category("教学"), Description("需要高亮的控件。")>
        Public Property HighlightControl As Control
            Get
                Return _highlightControl
            End Get
            Set(value As Control)
                _highlightControl = value
            End Set
        End Property

        <Category("教学"), Description("教学文本。")>
        Public Property Text As String
            Get
                Return _text
            End Get
            Set(value As String)
                _text = If(value, String.Empty)
            End Set
        End Property

        <Category("教学"), Description("文本相对高亮控件的方位。")>
        Public Property TextPlacement As TextPlacementEnum
            Get
                Return _placement
            End Get
            Set(value As TextPlacementEnum)
                _placement = value
            End Set
        End Property

        <Category("教学"), Description("是否绘制高亮控件到文本的连接线。"), DefaultValue(True)>
        Public Property ConnectorEnabled As Boolean
            Get
                Return _connectorEnabled
            End Get
            Set(value As Boolean)
                _connectorEnabled = value
            End Set
        End Property

        <Category("教学"), Description("显示当前 Item 前要清除的旧 Item 索引。")>
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
        <Editor("System.ComponentModel.Design.CollectionEditor, System.Design", GetType(System.Drawing.Design.UITypeEditor))>
        Public ReadOnly Property ClearItemIndices As List(Of Integer)
            Get
                Return _clearIndices
            End Get
        End Property

        Public Overrides Function ToString() As String
            If String.IsNullOrWhiteSpace(_text) Then Return "PonderItem"
            Return _text.Replace(vbCr, " ").Replace(vbLf, " ")
        End Function
    End Class

    <DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
     Editor("System.ComponentModel.Design.CollectionEditor, System.Design", GetType(System.Drawing.Design.UITypeEditor)),
     Category("教学"), Description("按顺序播放的教学项目。")>
    Public ReadOnly Property Items As List(Of PonderItem)

    Private _target As Control
    Private _overlay As PonderOverlayForm
    Private _currentIndex As Integer = -1
    Private ReadOnly _shownItemIndices As New List(Of Integer)()
    Private _autoPlay As Boolean
    Private _autoPlayInterval As Integer = 4500
    Private _animationEnabled As Boolean = True
    Private _animationDuration As Integer = 300
    Private _overlayColor As Color = Color.Black
    Private _overlayOpacity As Integer = 180
    Private _highlightBorderColor As Color = Color.FromArgb(255, 255, 255, 255)
    Private _highlightBorderWidth As Single = 2.0F
    Private _highlightPadding As Padding = New Padding(6)
    Private _textColor As Color = Color.White
    Private _textBackColor As Color = Color.FromArgb(220, 32, 32, 32)
    Private _textBorderColor As Color = Color.FromArgb(230, 255, 255, 255)
    Private _textBorderWidth As Single = 1.0F
    Private _textPadding As Padding = New Padding(12)
    Private _textMaxWidth As Integer = 360
    Private _connectorColor As Color = Color.FromArgb(235, 255, 255, 255)
    Private _connectorWidth As Single = 1.5F
    Private _connectorStyle As ConnectorStyleEnum = ConnectorStyleEnum.Elbow
    Private _buttonCorner As ButtonCornerEnum = ButtonCornerEnum.TopRight
    Private _buttonPadding As Padding = New Padding(10)
    Private _buttonSpacing As Integer = 4
    Private _buttonSize As Size = New Size(34, 30)
    Private _closeButtonBackColor As Color = Color.Transparent
    Private _closeButtonHoverBackColor As Color = Color.FromArgb(232, 17, 35)
    Private _closeButtonGlyphColor As Color = Color.White
    Private _closeButtonGlyphWidth As Single = 2.0F

    Public Event Started As EventHandler
    Public Event Stopped As EventHandler
    Public Event ItemChanged As EventHandler
    Public Event Closed As EventHandler

    Public Sub New()
        Items = New List(Of PonderItem)()
    End Sub

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property IsRunning As Boolean
        Get
            Return _overlay IsNot Nothing AndAlso Not _overlay.IsDisposed
        End Get
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property CurrentIndex As Integer
        Get
            Return _currentIndex
        End Get
    End Property

    <Category("行为"), Description("是否在项目之间自动播放。"), DefaultValue(False)>
    Public Property AutoPlay As Boolean
        Get
            Return _autoPlay
        End Get
        Set(value As Boolean)
            _autoPlay = value
            If _overlay IsNot Nothing Then _overlay.SetAutoPlay(value)
        End Set
    End Property

    <Category("行为"), Description("自动播放项目间隔（毫秒）。"), DefaultValue(4500)>
    Public Property AutoPlayInterval As Integer
        Get
            Return _autoPlayInterval
        End Get
        Set(value As Integer)
            _autoPlayInterval = Math.Max(250, value)
            If _overlay IsNot Nothing Then _overlay.SetAutoPlayInterval(_autoPlayInterval)
        End Set
    End Property

    <Category("动画"), DefaultValue(True)>
    Public Property AnimationEnabled As Boolean
        Get
            Return _animationEnabled
        End Get
        Set(value As Boolean)
            _animationEnabled = value
        End Set
    End Property

    <Category("动画"), DefaultValue(300)>
    Public Property AnimationDuration As Integer
        Get
            Return _animationDuration
        End Get
        Set(value As Integer)
            _animationDuration = Math.Max(0, value)
        End Set
    End Property

    <Category("外观"), Description("遮罩颜色。"), DefaultValue(GetType(Color), "Black")>
    Public Property OverlayColor As Color
        Get
            Return _overlayColor
        End Get
        Set(value As Color)
            _overlayColor = value
        End Set
    End Property

    <Category("外观"), Description("遮罩不透明度（0-255）。"), DefaultValue(180)>
    Public Property OverlayOpacity As Integer
        Get
            Return _overlayOpacity
        End Get
        Set(value As Integer)
            _overlayOpacity = Math.Max(0, Math.Min(255, value))
        End Set
    End Property

    <Category("外观"), DefaultValue(GetType(Color), "White")>
    Public Property HighlightBorderColor As Color
        Get
            Return _highlightBorderColor
        End Get
        Set(value As Color)
            _highlightBorderColor = value
        End Set
    End Property

    <Category("外观"), DefaultValue(2.0F)>
    Public Property HighlightBorderWidth As Single
        Get
            Return _highlightBorderWidth
        End Get
        Set(value As Single)
            _highlightBorderWidth = Math.Max(0.1F, value)
        End Set
    End Property

    <Category("外观"), DefaultValue(GetType(Padding), "6, 6, 6, 6")>
    Public Property HighlightPadding As Padding
        Get
            Return _highlightPadding
        End Get
        Set(value As Padding)
            _highlightPadding = value
        End Set
    End Property

    <Category("文本"), DefaultValue(GetType(Color), "White")>
    Public Property TextColor As Color
        Get
            Return _textColor
        End Get
        Set(value As Color)
            _textColor = value
        End Set
    End Property

    <Category("文本"), DefaultValue(GetType(Color), "220, 32, 32, 32")>
    Public Property TextBackColor As Color
        Get
            Return _textBackColor
        End Get
        Set(value As Color)
            _textBackColor = value
        End Set
    End Property

    <Category("文本"), DefaultValue(GetType(Color), "230, 255, 255, 255")>
    Public Property TextBorderColor As Color
        Get
            Return _textBorderColor
        End Get
        Set(value As Color)
            _textBorderColor = value
        End Set
    End Property

    <Category("文本"), DefaultValue(1.0F)>
    Public Property TextBorderWidth As Single
        Get
            Return _textBorderWidth
        End Get
        Set(value As Single)
            _textBorderWidth = Math.Max(0.1F, value)
        End Set
    End Property

    <Category("文本"), DefaultValue(GetType(Padding), "12, 12, 12, 12")>
    Public Property TextPadding As Padding
        Get
            Return _textPadding
        End Get
        Set(value As Padding)
            _textPadding = value
        End Set
    End Property

    <Category("文本"), DefaultValue(360)>
    Public Property TextMaxWidth As Integer
        Get
            Return _textMaxWidth
        End Get
        Set(value As Integer)
            _textMaxWidth = Math.Max(80, value)
        End Set
    End Property

    <Category("连接线"), DefaultValue(GetType(Color), "235, 255, 255, 255")>
    Public Property ConnectorColor As Color
        Get
            Return _connectorColor
        End Get
        Set(value As Color)
            _connectorColor = value
        End Set
    End Property

    <Category("连接线"), DefaultValue(1.5F)>
    Public Property ConnectorWidth As Single
        Get
            Return _connectorWidth
        End Get
        Set(value As Single)
            _connectorWidth = Math.Max(0.1F, value)
        End Set
    End Property

    <Category("连接线"), DefaultValue(GetType(ConnectorStyleEnum), "Elbow")>
    Public Property ConnectorStyle As ConnectorStyleEnum
        Get
            Return _connectorStyle
        End Get
        Set(value As ConnectorStyleEnum)
            _connectorStyle = value
        End Set
    End Property

    <Category("按钮"), DefaultValue(GetType(ButtonCornerEnum), "TopRight")>
    Public Property ButtonCorner As ButtonCornerEnum
        Get
            Return _buttonCorner
        End Get
        Set(value As ButtonCornerEnum)
            _buttonCorner = value
            _overlay?.RefreshLayout()
        End Set
    End Property

    <Category("按钮"), DefaultValue(GetType(Padding), "10, 10, 10, 10")>
    Public Property ButtonPadding As Padding
        Get
            Return _buttonPadding
        End Get
        Set(value As Padding)
            _buttonPadding = value
            _overlay?.RefreshLayout()
        End Set
    End Property

    <Category("按钮"), DefaultValue(4)>
    Public Property ButtonSpacing As Integer
        Get
            Return _buttonSpacing
        End Get
        Set(value As Integer)
            _buttonSpacing = Math.Max(0, value)
            _overlay?.RefreshLayout()
        End Set
    End Property

    <Category("按钮"), DefaultValue(GetType(Size), "34, 30")>
    Public Property ButtonSize As Size
        Get
            Return _buttonSize
        End Get
        Set(value As Size)
            _buttonSize = New Size(Math.Max(20, value.Width), Math.Max(20, value.Height))
            _overlay?.RefreshLayout()
        End Set
    End Property

    <Category("按钮"), DefaultValue(GetType(Color), "Transparent")>
    Public Property CloseButtonBackColor As Color
        Get
            Return _closeButtonBackColor
        End Get
        Set(value As Color)
            _closeButtonBackColor = value
        End Set
    End Property

    <Category("按钮"), DefaultValue(GetType(Color), "232, 17, 35")>
    Public Property CloseButtonHoverBackColor As Color
        Get
            Return _closeButtonHoverBackColor
        End Get
        Set(value As Color)
            _closeButtonHoverBackColor = value
        End Set
    End Property

    <Category("按钮"), DefaultValue(GetType(Color), "White")>
    Public Property CloseButtonGlyphColor As Color
        Get
            Return _closeButtonGlyphColor
        End Get
        Set(value As Color)
            _closeButtonGlyphColor = value
        End Set
    End Property

    <Category("按钮"), DefaultValue(2.0F)>
    Public Property CloseButtonGlyphWidth As Single
        Get
            Return _closeButtonGlyphWidth
        End Get
        Set(value As Single)
            _closeButtonGlyphWidth = Math.Max(0.5F, value)
        End Set
    End Property

    Public Sub Start(target As Control)
        If target Is Nothing Then Throw New ArgumentNullException(NameOf(target))
        If target.IsDisposed OrElse Not target.IsHandleCreated Then Return
        [Stop]()
        _target = target
        _currentIndex = -1
        _shownItemIndices.Clear()
        _overlay = New PonderOverlayForm(Me, target)
        AddHandler _overlay.FormClosed, AddressOf OverlayClosed
        _overlay.Show(target.FindForm())
        If Items.Count > 0 Then ShowItem(0)
        RaiseEvent Started(Me, EventArgs.Empty)
    End Sub

    Public Sub [Stop]()
        If _overlay Is Nothing Then Return
        Dim old = _overlay
        _overlay = Nothing
        RemoveHandler old.FormClosed, AddressOf OverlayClosed
        old.Close()
        old.Dispose()
        _target = Nothing
        _currentIndex = -1
        _shownItemIndices.Clear()
        RaiseEvent Stopped(Me, EventArgs.Empty)
    End Sub

    Public Sub NextItem()
        If Items.Count = 0 Then Return
        ShowItem(Math.Min(Items.Count - 1, _currentIndex + 1))
    End Sub

    Public Sub PreviousItem()
        If Items.Count = 0 Then Return
        ShowItem(Math.Max(0, _currentIndex - 1))
    End Sub

    Public Sub Restart()
        If Items.Count = 0 Then Return
        _shownItemIndices.Clear()
        ShowItem(0)
    End Sub

    Private Sub ShowItem(index As Integer)
        If _overlay Is Nothing OrElse index < 0 OrElse index >= Items.Count Then Return
        Dim item = Items(index)
        For Each clearIndex In item.ClearItemIndices.ToArray()
            _shownItemIndices.Remove(clearIndex)
        Next
        If Not _shownItemIndices.Contains(index) Then _shownItemIndices.Add(index)
        _currentIndex = index
        _overlay.SetItems(_shownItemIndices.Select(Function(i) New PonderOverlayItem(i, Items(i))).ToList(), index, _animationEnabled, _animationDuration)
        RaiseEvent ItemChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub OverlayClosed(sender As Object, e As FormClosedEventArgs)
        If _overlay Is sender Then
            _overlay = Nothing
            _target = Nothing
            _currentIndex = -1
            RaiseEvent Closed(Me, EventArgs.Empty)
        End If
    End Sub

    Friend ReadOnly Property CurrentTarget As Control
        Get
            Return _target
        End Get
    End Property

    Friend Function GetOverlayOptions() As PonderOverlayOptions
        Return New PonderOverlayOptions(Me)
    End Function

    Friend Sub RaiseClosed()
        [Stop]()
        RaiseEvent Closed(Me, EventArgs.Empty)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then [Stop]()
        MyBase.Dispose(disposing)
    End Sub
End Class

Friend NotInheritable Class PonderOverlayOptions
    Public ReadOnly Owner As Ponder
    Public Sub New(owner As Ponder)
        Me.Owner = owner
    End Sub
End Class

Friend NotInheritable Class PonderOverlayItem
    Public ReadOnly Index As Integer
    Public ReadOnly Item As Ponder.PonderItem
    Public Sub New(index As Integer, item As Ponder.PonderItem)
        Me.Index = index
        Me.Item = item
    End Sub
End Class

Friend Class PonderOverlayForm
    Inherits Form
    Implements D3D_IGpuRenderable, D3D_IGpuInvalidationSource, D3D_IGpuDirtyRegionCoverage, V5_IGpuPresentationSource

    Private ReadOnly _owner As Ponder
    Private ReadOnly _target As Control
    Private _item As Ponder.PonderItem
    Private _itemIndex As Integer = -1
    Private _visibleItems As New List(Of PonderOverlayItem)()
    Private _visibleHighlights As New List(Of RectangleF)()
    Private _highlight As RectangleF
    Private _textRect As RectangleF
    Private _buttons As New Dictionary(Of String, RectangleF)()
    Private _hoverButton As String
    Private _autoPlay As Boolean
    Private _autoPlayInterval As Integer
    Private _autoTimer As Timer
    Private _animation As D3D_AnimationHelper
    Private _animationProgress As Single = 1.0F
    Private _font As Font

    Public Sub New(owner As Ponder, target As Control)
        _owner = owner
        _target = target
        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        StartPosition = FormStartPosition.Manual
        TopMost = False
        BackColor = owner.OverlayColor
        Opacity = Math.Max(0, Math.Min(255, owner.OverlayOpacity)) / 255.0
        _font = New Font(SystemFonts.MessageBoxFont.FontFamily, 10.0F)
        _autoPlay = owner.AutoPlay
        _autoPlayInterval = owner.AutoPlayInterval
        AddHandler target.LocationChanged, AddressOf TargetChanged
        AddHandler target.SizeChanged, AddressOf TargetChanged
        AddHandler target.VisibleChanged, AddressOf TargetChanged
        If TypeOf target Is Form Then AddHandler DirectCast(target, Form).FormClosed, AddressOf TargetFormClosed
        KeyPreview = True
        SyncBounds()
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        RequestRender()
    End Sub

    Private Sub TargetChanged(sender As Object, e As EventArgs)
        If _target.IsDisposed Then Return
        If TypeOf _target Is Form AndAlso DirectCast(_target, Form).WindowState = FormWindowState.Minimized Then
            Hide()
            Return
        End If
        If Not _target.Visible Then Hide() Else Show()
        SyncBounds()
    End Sub

    Private Sub SyncBounds()
        If _target Is Nothing OrElse _target.IsDisposed OrElse Not _target.IsHandleCreated Then Return
        Dim r = _target.RectangleToScreen(_target.ClientRectangle)
        If Bounds <> r Then Bounds = r
        RefreshLayout()
    End Sub

    Public Sub SetAutoPlay(value As Boolean)
        _autoPlay = value
        ConfigureAutoTimer()
    End Sub

    Public Sub SetAutoPlayInterval(value As Integer)
        _autoPlayInterval = value
        ConfigureAutoTimer()
    End Sub

    Public Sub SetItems(items As List(Of PonderOverlayItem), index As Integer, animate As Boolean, duration As Integer)
        _visibleItems = If(items, New List(Of PonderOverlayItem)())
        _item = _visibleItems.Where(Function(x) x.Index = index).Select(Function(x) x.Item).FirstOrDefault()
        _itemIndex = index
        RefreshLayout()
        If _animation Is Nothing Then
            _animation = New D3D_AnimationHelper(Me) With {.FPS = 60, .Duration = duration}
        Else
            _animation.Duration = duration
        End If
        If animate Then
            _animation.SetImmediate(0)
            _animation.AnimateTo(1)
        Else
            _animation.SetImmediate(1)
        End If
        ConfigureAutoTimer()
        RequestRender()
    End Sub

    Public Sub RefreshLayout()
        If ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then Return
        _visibleHighlights.Clear()
        For Each visualItem In _visibleItems
            _visibleHighlights.Add(GetHighlightRect(visualItem.Item))
        Next
        _highlight = GetHighlightRect(_item)
        Dim placement = If(_item Is Nothing, Ponder.TextPlacementEnum.Auto, _item.TextPlacement)
        Dim text = If(_item?.Text, String.Empty)
        Dim maxW = Math.Min(_owner.TextMaxWidth, Math.Max(80, ClientSize.Width - 32))
        Dim measured = TextRenderer.MeasureText(text, _font, New Size(maxW - _owner.TextPadding.Horizontal, 0), TextFormatFlags.WordBreak)
        Dim textW = Math.Min(maxW, Math.Max(80, measured.Width + _owner.TextPadding.Horizontal))
        Dim textH = Math.Max(30, measured.Height + _owner.TextPadding.Vertical)
        Dim gap As Integer = 18
        Dim candidates As New List(Of RectangleF)()
        Select Case placement
            Case Ponder.TextPlacementEnum.Top
                candidates.Add(New RectangleF(_highlight.X + (_highlight.Width - textW) / 2, _highlight.Y - gap - textH, textW, textH))
            Case Ponder.TextPlacementEnum.Bottom
                candidates.Add(New RectangleF(_highlight.X + (_highlight.Width - textW) / 2, _highlight.Bottom + gap, textW, textH))
            Case Ponder.TextPlacementEnum.Left
                candidates.Add(New RectangleF(_highlight.X - gap - textW, _highlight.Y + (_highlight.Height - textH) / 2, textW, textH))
            Case Ponder.TextPlacementEnum.Right
                candidates.Add(New RectangleF(_highlight.Right + gap, _highlight.Y + (_highlight.Height - textH) / 2, textW, textH))
            Case Else
                candidates.Add(New RectangleF(_highlight.Right + gap, _highlight.Y + (_highlight.Height - textH) / 2, textW, textH))
                candidates.Add(New RectangleF(_highlight.X - gap - textW, _highlight.Y + (_highlight.Height - textH) / 2, textW, textH))
                candidates.Add(New RectangleF(_highlight.X + (_highlight.Width - textW) / 2, _highlight.Bottom + gap, textW, textH))
                candidates.Add(New RectangleF(_highlight.X + (_highlight.Width - textW) / 2, _highlight.Y - gap - textH, textW, textH))
        End Select
        _textRect = candidates.FirstOrDefault(Function(x) New RectangleF(0, 0, ClientSize.Width, ClientSize.Height).Contains(x))
        If _textRect.Width <= 0 Then _textRect = New RectangleF(Math.Max(8, (ClientSize.Width - textW) / 2), Math.Max(8, (ClientSize.Height - textH) / 2), textW, textH)
        LayoutButtons()
        RequestRender()
    End Sub

    Private Function GetHighlightRect(item As Ponder.PonderItem) As RectangleF
        If item Is Nothing OrElse item.HighlightControl Is Nothing OrElse item.HighlightControl.IsDisposed OrElse Not item.HighlightControl.IsHandleCreated Then Return RectangleF.Empty
        Dim r = item.HighlightControl.RectangleToScreen(item.HighlightControl.ClientRectangle)
        Dim local As New RectangleF(r.X - Left, r.Y - Top, r.Width, r.Height)
        local = RectangleF.Intersect(local, New RectangleF(0, 0, ClientSize.Width, ClientSize.Height))
        local = New RectangleF(local.X - _owner.HighlightPadding.Left, local.Y - _owner.HighlightPadding.Top,
                               local.Width + _owner.HighlightPadding.Horizontal, local.Height + _owner.HighlightPadding.Vertical)
        Return RectangleF.Intersect(local, New RectangleF(0, 0, ClientSize.Width, ClientSize.Height))
    End Function

    Private Sub LayoutButtons()
        _buttons.Clear()
        Dim names = New String() {"close", "prev", "next", "auto", "restart"}
        Dim w = _owner.ButtonSize.Width
        Dim h = _owner.ButtonSize.Height
        Dim totalW = names.Length * w + (names.Length - 1) * _owner.ButtonSpacing
        Dim totalH = h
        Dim horizontal As Boolean = _owner.ButtonCorner = Ponder.ButtonCornerEnum.TopLeft OrElse _owner.ButtonCorner = Ponder.ButtonCornerEnum.TopRight OrElse _owner.ButtonCorner = Ponder.ButtonCornerEnum.BottomLeft OrElse _owner.ButtonCorner = Ponder.ButtonCornerEnum.BottomRight
        Dim x As Integer = If(_owner.ButtonCorner = Ponder.ButtonCornerEnum.TopRight OrElse _owner.ButtonCorner = Ponder.ButtonCornerEnum.BottomRight, ClientSize.Width - _owner.ButtonPadding.Right - totalW, _owner.ButtonPadding.Left)
        Dim y As Integer = If(_owner.ButtonCorner = Ponder.ButtonCornerEnum.BottomLeft OrElse _owner.ButtonCorner = Ponder.ButtonCornerEnum.BottomRight, ClientSize.Height - _owner.ButtonPadding.Bottom - h, _owner.ButtonPadding.Top)
        For Each buttonName In names
            _buttons(buttonName) = New RectangleF(x, y, w, h)
            x += w + _owner.ButtonSpacing
        Next
    End Sub

    Private Sub ConfigureAutoTimer()
        If _autoTimer Is Nothing Then
            _autoTimer = New Timer()
            AddHandler _autoTimer.Tick, Sub() If _autoPlay Then _owner.NextItem()
        End If
        _autoTimer.Interval = Math.Max(250, _autoPlayInterval)
        If _autoPlay AndAlso _itemIndex >= 0 AndAlso _itemIndex < _owner.Items.Count - 1 Then _autoTimer.Start() Else _autoTimer.Stop()
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        Dim hit = HitButton(e.Location)
        If hit <> _hoverButton Then
            Dim oldHover = _hoverButton
            _hoverButton = hit
            If oldHover = "close" OrElse hit = "close" Then RequestCloseButtonRender()
        End If
        MyBase.OnMouseMove(e)
    End Sub

    Private Sub RequestCloseButtonRender()
        Dim closeBounds As RectangleF = RectangleF.Empty
        If Not _buttons.TryGetValue("close", closeBounds) Then Return
        Dim dirty = Rectangle.Ceiling(closeBounds)
        Dim antialiasMargin = CInt(Math.Ceiling(Math.Max(1.0F, _owner.CloseButtonGlyphWidth))) + 2
        dirty.Inflate(antialiasMargin, antialiasMargin)
        RequestRender(Rectangle.Intersect(ClientRectangle, dirty))
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then
            Select Case HitButton(e.Location)
                Case "close" : _owner.RaiseClosed()
                Case "prev" : _owner.PreviousItem()
                Case "next" : _owner.NextItem()
                Case "auto" : _autoPlay = Not _autoPlay : _owner.AutoPlay = _autoPlay
                Case "restart" : _owner.Restart()
            End Select
        End If
        MyBase.OnMouseDown(e)
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        If e.KeyCode = Keys.Escape Then
            _owner.RaiseClosed()
            e.Handled = True
            Return
        End If
        MyBase.OnKeyDown(e)
    End Sub

    Private Sub TargetFormClosed(sender As Object, e As FormClosedEventArgs)
        _owner.RaiseClosed()
    End Sub

    Private Function HitButton(point As Point) As String
        For Each pair In _buttons
            If pair.Value.Contains(point) Then Return pair.Key
        Next
        Return Nothing
    End Function

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements D3D_IGpuRenderable.RenderGpu
        If context Is Nothing OrElse ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then Return
        Dim p = If(_animation Is Nothing, 1.0F, _animation.Progress)
        context.FillRectangle(New RectangleF(0, 0, ClientSize.Width, ClientSize.Height), Color.FromArgb(CInt(_owner.OverlayOpacity * p), _owner.OverlayColor))
        For Each retained In _visibleHighlights
            If retained.Width > 0 AndAlso retained.Height > 0 Then
                context.FillRectangle(retained, Color.FromArgb(Math.Max(1, CInt(35 * p)), Color.White))
                context.DrawRectangle(retained, _owner.HighlightBorderColor, _owner.HighlightBorderWidth)
            End If
        Next
        If _highlight.Width > 0 AndAlso _highlight.Height > 0 Then
            context.FillRectangle(_highlight, Color.FromArgb(Math.Max(1, CInt(45 * p)), Color.White))
            context.DrawRectangle(_highlight, _owner.HighlightBorderColor, _owner.HighlightBorderWidth)
        End If
        If _item IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(_item.Text) AndAlso _textRect.Width > 0 Then
            context.FillRectangle(_textRect, _owner.TextBackColor)
            context.DrawRectangle(_textRect, _owner.TextBorderColor, _owner.TextBorderWidth)
            Dim inner = New RectangleF(_textRect.X + _owner.TextPadding.Left, _textRect.Y + _owner.TextPadding.Top,
                                       Math.Max(1, _textRect.Width - _owner.TextPadding.Horizontal), Math.Max(1, _textRect.Height - _owner.TextPadding.Vertical))
            context.DrawText(_item.Text, _font, Color.FromArgb(CInt(255 * p), _owner.TextColor), inner,
                             Vortice.DirectWrite.TextAlignment.Leading, Vortice.DirectWrite.ParagraphAlignment.Near, True)
            If _item.ConnectorEnabled Then DrawConnector(context, p)
        End If
        DrawButtons(context)
    End Sub

    Private Sub DrawConnector(context As D3D_PaintContext, p As Single)
        If _highlight.Width <= 0 OrElse _textRect.Width <= 0 Then Return
        Dim start As New Vector2(_highlight.X + _highlight.Width / 2, _highlight.Y + _highlight.Height / 2)
        Dim finish As New Vector2(_textRect.X + _textRect.Width / 2, _textRect.Y + _textRect.Height / 2)
        Dim pen = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, Color.FromArgb(CInt(255 * p), _owner.ConnectorColor), context.DeviceGeneration)
        If _owner.ConnectorStyle = Ponder.ConnectorStyleEnum.Straight Then
            context.DeviceContext.DrawLine(start, finish, pen, _owner.ConnectorWidth)
        Else
            Dim midX = (start.X + finish.X) / 2.0F
            context.DeviceContext.DrawLine(start, New Vector2(midX, start.Y), pen, _owner.ConnectorWidth)
            context.DeviceContext.DrawLine(New Vector2(midX, start.Y), New Vector2(midX, finish.Y), pen, _owner.ConnectorWidth)
            context.DeviceContext.DrawLine(New Vector2(midX, finish.Y), finish, pen, _owner.ConnectorWidth)
        End If
    End Sub

    Private Sub DrawButtons(context As D3D_PaintContext)
        For Each pair In _buttons
            Dim rect = pair.Value
            Dim isClose = pair.Key = "close"
            Dim bg As Color = If(isClose AndAlso pair.Key = _hoverButton, _owner.CloseButtonHoverBackColor, If(isClose, _owner.CloseButtonBackColor, Color.FromArgb(170, 32, 32, 32)))
            If bg.A > 0 Then context.FillRoundedRectangle(rect, 4, bg)
            Dim glyphColor As Color = If(isClose, _owner.CloseButtonGlyphColor, Color.White)
            Dim pen = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, glyphColor, context.DeviceGeneration)
            Dim cx = rect.X + rect.Width / 2, cy = rect.Y + rect.Height / 2
            Dim s = Math.Min(rect.Width, rect.Height) * 0.32F
            Select Case pair.Key
                Case "close"
                    context.DeviceContext.DrawLine(New Vector2(cx - s, cy - s), New Vector2(cx + s, cy + s), pen, _owner.CloseButtonGlyphWidth)
                    context.DeviceContext.DrawLine(New Vector2(cx + s, cy - s), New Vector2(cx - s, cy + s), pen, _owner.CloseButtonGlyphWidth)
                Case "prev"
                    context.DeviceContext.DrawLine(New Vector2(cx + s, cy - s), New Vector2(cx - s, cy), pen, 2)
                    context.DeviceContext.DrawLine(New Vector2(cx - s, cy), New Vector2(cx + s, cy + s), pen, 2)
                Case "next"
                    context.DeviceContext.DrawLine(New Vector2(cx - s, cy - s), New Vector2(cx + s, cy), pen, 2)
                    context.DeviceContext.DrawLine(New Vector2(cx + s, cy), New Vector2(cx - s, cy + s), pen, 2)
                Case "auto"
                    context.DrawText(If(_autoPlay, "||", ">"), _font, glyphColor, rect, Vortice.DirectWrite.TextAlignment.Center, Vortice.DirectWrite.ParagraphAlignment.Center, False)
                Case "restart"
                    context.DrawText("R", _font, glyphColor, rect, Vortice.DirectWrite.TextAlignment.Center, Vortice.DirectWrite.ParagraphAlignment.Center, False)
            End Select
        Next
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements D3D_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Size)
    End Function

    Public Function CoversDirtyRegion(dirtyRegion As Rectangle) As Boolean Implements D3D_IGpuDirtyRegionCoverage.CoversDirtyRegion
        Return dirtyRegion.Width > 0 AndAlso dirtyRegion.Height > 0
    End Function

    Private Sub RequestRender(Optional dirtyRect As Rectangle? = Nothing)
        If IsDisposed Then Return
        D3D_InvalidationRouter.RequestRender(Me, If(dirtyRect.HasValue, dirtyRect.Value, New Rectangle(Point.Empty, Size)))
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            If _autoTimer IsNot Nothing Then _autoTimer.Dispose()
            _animation?.Dispose()
            _font?.Dispose()
            RemoveHandler _target.LocationChanged, AddressOf TargetChanged
            RemoveHandler _target.SizeChanged, AddressOf TargetChanged
            RemoveHandler _target.VisibleChanged, AddressOf TargetChanged
            If TypeOf _target Is Form Then RemoveHandler DirectCast(_target, Form).FormClosed, AddressOf TargetFormClosed
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
