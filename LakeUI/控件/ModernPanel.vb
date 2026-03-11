Imports System.ComponentModel
Imports System.Drawing.Drawing2D

''' <summary>
''' 滚动策略枚举。
''' </summary>
Public Enum ScrollMode
    ''' <summary>不显示滚动条。</summary>
    None = 0
    ''' <summary>仅垂直滚动。</summary>
    Vertical = 1
    ''' <summary>仅水平滚动。</summary>
    Horizontal = 2
    ''' <summary>同时支持垂直和水平滚动。</summary>
    Both = 3
End Enum

''' <summary>
''' 纯自绘的现代化面板控件，支持自定义滚动条、边框圆角和 AutoSize。
''' 可在设计器中像原版 Panel 一样直接添加子控件。
''' </summary>
<Designer("System.Windows.Forms.Design.ParentControlDesigner, System.Design", GetType(Design.IDesigner))>
<Docking(DockingBehavior.Ask)>
<DefaultEvent("Scroll")>
Public Class ModernPanel

#Region "构造"

    Public Sub New()
        InitializeComponent()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.ContainerControl Or
                 ControlStyles.SupportsTransparentBackColor, True)
        UpdateStyles()
    End Sub

#End Region

#Region "辅助方法"

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            更新滚动区域()
            Me.PerformLayout()
            Me.Invalidate()
        End If
    End Sub

    Private Function 获取边框内边距() As Integer
        Return Math.Max(边框宽度, If(边框圆角半径 > 0, 边框圆角半径 \ 2, 0))
    End Function

    ''' <summary>获取有效内边距：边框内缩 + 用户 Padding，Padding 表示边框内侧到内容的间距。
    ''' 右/下额外 +1 px 补偿 Rectangle 坐标不对称（起点含、终点不含）。</summary>
    Private Function 获取有效内边距() As Padding
        Dim inset As Integer = 获取边框内边距()
        Dim endFix As Integer = If(边框宽度 > 0, 1, 0)
        Return New Padding(
            inset + Me.Padding.Left,
            inset + Me.Padding.Top,
            inset + Me.Padding.Right + endFix,
            inset + Me.Padding.Bottom + endFix)
    End Function

    ''' <summary>重写 DisplayRectangle，使 Dock 子控件自动避开边框区域。</summary>
    Public Overrides ReadOnly Property DisplayRectangle As Rectangle
        Get
            Dim ep As Padding = 获取有效内边距()
            Return New Rectangle(
                ep.Left,
                ep.Top,
                Math.Max(0, Me.ClientSize.Width - ep.Horizontal),
                Math.Max(0, Me.ClientSize.Height - ep.Vertical))
        End Get
    End Property

    ''' <summary>计算所有子控件的包围矩形（设计坐标系下的内容总大小）。</summary>
    Private Function 计算内容总大小() As Size
        Dim maxRight As Integer = 0
        Dim maxBottom As Integer = 0
        Dim ep As Padding = 获取有效内边距()
        For Each ctrl As Control In Me.Controls
            If Not ctrl.Visible Then Continue For
            If ctrl.Dock <> DockStyle.None Then Continue For
            Dim tag As ChildLayoutInfo = Nothing
            Dim dl, dt As Integer
            If _childLayouts.TryGetValue(ctrl, tag) Then
                dl = tag.DesignLeft
                dt = tag.DesignTop
            Else
                dl = ctrl.Left - ep.Left + _hScrollOffset
                dt = ctrl.Top - ep.Top + _vScrollOffset
            End If
            Dim r As Integer = dl + ctrl.Width + ctrl.Margin.Right
            Dim b As Integer = dt + ctrl.Height + ctrl.Margin.Bottom
            If r > maxRight Then maxRight = r
            If b > maxBottom Then maxBottom = b
        Next
        Return New Size(maxRight, maxBottom)
    End Function

    ''' <summary>获取扣除滚动条保留区域后的有效视口大小。</summary>
    Private Function 获取有效视口大小() As Size
        Dim ep As Padding = 获取有效内边距()
        Dim sbReserve As Integer = 滚动条宽度 + ScrollBarRenderer.Margin
        Dim w As Integer = Me.Width - ep.Horizontal - If(_showVScroll, sbReserve, 0)
        Dim h As Integer = Me.Height - ep.Vertical - If(_showHScroll, sbReserve, 0)
        Return New Size(Math.Max(0, w), Math.Max(0, h))
    End Function

#End Region

#Region "外观属性 - 边框"

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

#End Region

#Region "外观属性 - 背景"

    Private 背景颜色 As Color = Color.FromArgb(36, 36, 36)
    <Category("LakeUI"), Description("背景颜色"), DefaultValue(GetType(Color), "36, 36, 36"), Browsable(True)>
    Public Property BackColor1 As Color
        Get
            Return 背景颜色
        End Get
        Set(value As Color)
            SetValue(背景颜色, value)
        End Set
    End Property

#End Region

#Region "外观属性 - 滚动条"

    Private 滚动条宽度 As Integer = 10
    <Category("LakeUI"), Description("滚动条宽度"), DefaultValue(GetType(Integer), "10"), Browsable(True)>
    Public Property ScrollBarWidth As Integer
        Get
            Return 滚动条宽度
        End Get
        Set(value As Integer)
            SetValue(滚动条宽度, value)
        End Set
    End Property

    Private 滚动条轨道颜色 As Color = Color.FromArgb(20, 20, 20)
    <Category("LakeUI"), Description("滚动条轨道颜色"), DefaultValue(GetType(Color), "20, 20, 20"), Browsable(True)>
    Public Property ScrollBarTrackColor As Color
        Get
            Return 滚动条轨道颜色
        End Get
        Set(value As Color)
            SetValue(滚动条轨道颜色, value)
        End Set
    End Property

    Private 滚动条滑块颜色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("滚动条滑块颜色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
    Public Property ScrollBarThumbColor As Color
        Get
            Return 滚动条滑块颜色
        End Get
        Set(value As Color)
            SetValue(滚动条滑块颜色, value)
        End Set
    End Property

    Private 滚动条悬停颜色 As Color = Color.FromArgb(120, 120, 120)
    <Category("LakeUI"), Description("滚动条滑块悬停颜色"), DefaultValue(GetType(Color), "120, 120, 120"), Browsable(True)>
    Public Property ScrollBarThumbHoverColor As Color
        Get
            Return 滚动条悬停颜色
        End Get
        Set(value As Color)
            SetValue(滚动条悬停颜色, value)
        End Set
    End Property

#End Region

#Region "外观属性 - SSAA"

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

#End Region

#Region "行为属性"

    Private 滚动模式 As ScrollMode = ScrollMode.Both
    <Category("LakeUI"), Description("滚动条策略：None/Vertical/Horizontal/Both"), DefaultValue(GetType(ScrollMode), "Both"), Browsable(True)>
    Public Property ScrollBarMode As ScrollMode
        Get
            Return 滚动模式
        End Get
        Set(value As ScrollMode)
            SetValue(滚动模式, value)
        End Set
    End Property

    Private 垂直滚动步长 As Integer = 40
    <Category("LakeUI"), Description("垂直滚动每次像素步长"), DefaultValue(GetType(Integer), "40"), Browsable(True)>
    Public Property VerticalScrollStep As Integer
        Get
            Return 垂直滚动步长
        End Get
        Set(value As Integer)
            垂直滚动步长 = Math.Max(1, value)
        End Set
    End Property

    Private 水平滚动步长 As Integer = 40
    <Category("LakeUI"), Description("水平滚动每次像素步长"), DefaultValue(GetType(Integer), "40"), Browsable(True)>
    Public Property HorizontalScrollStep As Integer
        Get
            Return 水平滚动步长
        End Get
        Set(value As Integer)
            水平滚动步长 = Math.Max(1, value)
        End Set
    End Property

#End Region

#Region "内部状态"

    Private _vScrollOffset As Integer = 0
    Private ReadOnly _vScrollBar As New ScrollBarRenderer()

    Private _hScrollOffset As Integer = 0
    Private ReadOnly _hScrollBar As New ScrollBarRenderer()

    Private _contentSize As Size = Size.Empty

    Private _showVScroll As Boolean = False
    Private _showHScroll As Boolean = False

    Private ReadOnly _childLayouts As New Dictionary(Of Control, ChildLayoutInfo)()

#End Region

#Region "滚动区域计算"

    Private Function 需要垂直滚动条() As Boolean
        Return _showVScroll
    End Function

    Private Function 需要水平滚动条() As Boolean
        Return _showHScroll
    End Function

    Private Sub 更新滚动区域()
        _contentSize = 计算内容总大小()

        Dim inset As Integer = 获取边框内边距()
        Dim ep As Padding = 获取有效内边距()
        Dim sbReserve As Integer = 滚动条宽度 + ScrollBarRenderer.Margin

        ' 不含滚动条的完整视口
        Dim fullW As Integer = Me.Width - ep.Horizontal
        Dim fullH As Integer = Me.Height - ep.Vertical

        ' 两轮检测：先各自判断，再考虑对方占用空间后重新判断
        Dim needV As Boolean = (滚动模式 And ScrollMode.Vertical) <> 0 AndAlso _contentSize.Height > fullH
        Dim needH As Boolean = (滚动模式 And ScrollMode.Horizontal) <> 0 AndAlso _contentSize.Width > fullW

        If needV AndAlso Not needH Then
            If (滚动模式 And ScrollMode.Horizontal) <> 0 AndAlso _contentSize.Width > (fullW - sbReserve) Then
                needH = True
            End If
        End If
        If needH AndAlso Not needV Then
            If (滚动模式 And ScrollMode.Vertical) <> 0 AndAlso _contentSize.Height > (fullH - sbReserve) Then
                needV = True
            End If
        End If

        _showVScroll = needV
        _showHScroll = needH

        ' 有效视口（扣除滚动条保留区域：scrollBarWidth + Margin，内容边缘对齐滚动条视觉边缘）
        Dim viewW As Integer = Math.Max(0, fullW - If(needV, sbReserve, 0))
        Dim viewH As Integer = Math.Max(0, fullH - If(needH, sbReserve, 0))

        ' 垂直
        If needV Then
            _vScrollBar.ComputeLayout(Me.Width, Me.Height, 边框宽度, 边框圆角半径,
                ep.Top - inset, ep.Bottom - inset + If(needH, sbReserve, 0), 滚动条宽度,
                _contentSize.Height, viewH, _vScrollOffset)
            Dim maxOff As Integer = Math.Max(0, _contentSize.Height - viewH)
            _vScrollOffset = Math.Max(0, Math.Min(_vScrollOffset, maxOff))
        Else
            _vScrollOffset = 0
            _vScrollBar.ThumbRect = Rectangle.Empty
            _vScrollBar.TrackRect = Rectangle.Empty
            _vScrollBar.VisualLeft = Me.Width
        End If

        ' 水平
        If needH Then
            Dim vsbReserved As Integer = If(needV, Me.Width - inset - _vScrollBar.VisualLeft, 0)
            _hScrollBar.ComputeHorizontalLayout(Me.Width, Me.Height, 边框宽度, 边框圆角半径,
                ep.Left - inset, ep.Right - inset + vsbReserved, 滚动条宽度,
                _contentSize.Width, viewW, _hScrollOffset)
            Dim maxOff As Integer = Math.Max(0, _contentSize.Width - viewW)
            _hScrollOffset = Math.Max(0, Math.Min(_hScrollOffset, maxOff))
        Else
            _hScrollOffset = 0
            _hScrollBar.ThumbRect = Rectangle.Empty
            _hScrollBar.TrackRect = Rectangle.Empty
            _hScrollBar.VisualTop = Me.Height
        End If

        应用子控件偏移()
    End Sub

    Private Sub 应用子控件偏移()
        _suppressLocationSync = True
        Try
            Dim ep As Padding = 获取有效内边距()
            Dim ox As Integer = ep.Left - _hScrollOffset
            Dim oy As Integer = ep.Top - _vScrollOffset

            For Each ctrl As Control In Me.Controls
                If ctrl.Dock <> DockStyle.None Then Continue For
                Dim tag As ChildLayoutInfo = Nothing
                If Not _childLayouts.TryGetValue(ctrl, tag) Then
                    tag = New ChildLayoutInfo With {
                        .DesignLeft = ctrl.Left - ep.Left + _hScrollOffset,
                        .DesignTop = ctrl.Top - ep.Top + _vScrollOffset
                    }
                    _childLayouts(ctrl) = tag
                End If
                ctrl.Left = ox + tag.DesignLeft
                ctrl.Top = oy + tag.DesignTop
            Next
        Finally
            _suppressLocationSync = False
        End Try
    End Sub

    Private Class ChildLayoutInfo
        Public DesignLeft As Integer
        Public DesignTop As Integer
    End Class

#End Region

#Region "绘制"

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Me.Width < 1 OrElse Me.Height < 1 Then Return
        Dim g As Graphics = e.Graphics

        更新滚动区域()

        Dim _ssaa As Integer = If(Class1.GlobalSSAA > 1, Class1.GlobalSSAA, 超采样倍率)
        If _ssaa > 1 Then
            Using bmp As New Bitmap(Me.Width * _ssaa, Me.Height * _ssaa)
                Using sg As Graphics = Graphics.FromImage(bmp)
                    sg.ScaleTransform(_ssaa, _ssaa)
                    sg.SmoothingMode = SmoothingMode.AntiAlias
                    sg.PixelOffsetMode = PixelOffsetMode.HighQuality
                    绘制背景与边框(sg)
                    绘制垂直滚动条(sg)
                    绘制水平滚动条(sg)
                End Using
                g.CompositingQuality = CompositingQuality.HighQuality
                g.InterpolationMode = InterpolationMode.HighQualityBicubic
                g.DrawImage(bmp, 0, 0, Me.Width, Me.Height)
            End Using
        Else
            绘制背景与边框(g)
            绘制垂直滚动条(g)
            绘制水平滚动条(g)
        End If
    End Sub

    Private Sub 绘制背景与边框(g As Graphics)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        Dim boundsRect As New RectangleF(0, 0, Me.Width - 1, Me.Height - 1)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 / 2.0F
            boundsRect.Inflate(-half, -half)
        End If
        If 边框圆角半径 > 0 Then
            Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(boundsRect, 边框圆角半径)
                Using br As New SolidBrush(背景颜色)
                    g.FillPath(br, path)
                End Using
                RectangleRenderer.绘制圆角边框(g, path, 边框颜色, 边框宽度)
            End Using
        Else
            Using br As New SolidBrush(背景颜色)
                g.FillRectangle(br, boundsRect)
            End Using
            RectangleRenderer.绘制矩形边框(g, boundsRect, 边框颜色, 边框宽度)
        End If
    End Sub

    Private Sub 绘制垂直滚动条(g As Graphics)
        If _vScrollBar.TrackRect.IsEmpty Then Return
        _vScrollBar.Draw(g, Me.Width, Me.Height, 边框宽度, 边框圆角半径,
            滚动条宽度, 滚动条轨道颜色, 滚动条滑块颜色, 滚动条悬停颜色)
    End Sub

    Private Sub 绘制水平滚动条(g As Graphics)
        If _hScrollBar.TrackRect.IsEmpty Then Return
        _hScrollBar.DrawHorizontal(g, Me.Width, Me.Height, 边框宽度, 边框圆角半径,
            滚动条宽度, 滚动条轨道颜色, 滚动条滑块颜色, 滚动条悬停颜色)
    End Sub

#End Region

#Region "鼠标交互"

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)

        If _vScrollBar.IsDragging Then
            Dim viewport As Size = 获取有效视口大小()
            _vScrollOffset = _vScrollBar.DragMove(e.Y, _contentSize.Height, viewport.Height)
            更新滚动区域()
            Me.Invalidate()
            Return
        End If

        If _hScrollBar.IsDragging Then
            Dim viewport As Size = 获取有效视口大小()
            _hScrollOffset = _hScrollBar.DragMoveHorizontal(e.X, _contentSize.Width, viewport.Width)
            更新滚动区域()
            Me.Invalidate()
            Return
        End If

        Dim needInvalidate As Boolean = False
        If _vScrollBar.UpdateHover(e.Location) Then needInvalidate = True
        If _hScrollBar.UpdateHover(e.Location) Then needInvalidate = True
        If needInvalidate Then Me.Invalidate()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)

        Dim viewport As Size = 获取有效视口大小()

        If _vScrollBar.BeginDrag(e.Location, _vScrollOffset) Then Return
        If Not _vScrollBar.TrackRect.IsEmpty Then
            Dim newOff = _vScrollBar.TrackClick(e.Location, _vScrollOffset, _contentSize.Height, viewport.Height)
            If newOff <> _vScrollOffset Then
                _vScrollOffset = newOff
                更新滚动区域()
                Me.Invalidate()
                Return
            End If
        End If

        If _hScrollBar.BeginDragHorizontal(e.Location, _hScrollOffset) Then Return
        If Not _hScrollBar.TrackRect.IsEmpty Then
            Dim newHOff = _hScrollBar.TrackClickHorizontal(e.Location, _hScrollOffset, _contentSize.Width, viewport.Width)
            If newHOff <> _hScrollOffset Then
                _hScrollOffset = newHOff
                更新滚动区域()
                Me.Invalidate()
                Return
            End If
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        _vScrollBar.EndDrag()
        _hScrollBar.EndDrag()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        Dim needInvalidate As Boolean = False
        If _vScrollBar.ResetHover() Then needInvalidate = True
        If _hScrollBar.ResetHover() Then needInvalidate = True
        If needInvalidate Then Me.Invalidate()
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)

        Dim viewport As Size = 获取有效视口大小()

        If (Control.ModifierKeys And Keys.Shift) = Keys.Shift Then
            If _showHScroll Then
                Dim newHOff = ScrollBarRenderer.HandleHorizontalWheel(e.Delta, _hScrollOffset, _contentSize.Width, viewport.Width, 水平滚动步长)
                If newHOff <> _hScrollOffset Then
                    _hScrollOffset = newHOff
                    更新滚动区域()
                    Me.Invalidate()
                End If
            End If
            Return
        End If

        If _showVScroll Then
            Dim newOff = ScrollBarRenderer.HandleHorizontalWheel(e.Delta, _vScrollOffset, _contentSize.Height, viewport.Height, 垂直滚动步长)
            If newOff <> _vScrollOffset Then
                _vScrollOffset = newOff
                更新滚动区域()
                Me.Invalidate()
            End If
        End If
    End Sub

#End Region

#Region "生命周期"

    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        更新滚动区域()
    End Sub

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        更新滚动区域()
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnControlAdded(e As ControlEventArgs)
        MyBase.OnControlAdded(e)
        If e.Control.Dock = DockStyle.None Then
            Dim ep As Padding = 获取有效内边距()
            Dim tag As New ChildLayoutInfo With {
                .DesignLeft = e.Control.Left - ep.Left + _hScrollOffset,
                .DesignTop = e.Control.Top - ep.Top + _vScrollOffset
            }
            _childLayouts(e.Control) = tag
        End If
        AddHandler e.Control.SizeChanged, AddressOf 子控件布局变更
        AddHandler e.Control.LocationChanged, AddressOf 子控件位置变更
        更新滚动区域()
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnControlRemoved(e As ControlEventArgs)
        MyBase.OnControlRemoved(e)
        _childLayouts.Remove(e.Control)
        RemoveHandler e.Control.SizeChanged, AddressOf 子控件布局变更
        RemoveHandler e.Control.LocationChanged, AddressOf 子控件位置变更
        更新滚动区域()
        Me.Invalidate()
    End Sub

    Private _suppressLocationSync As Boolean = False

    Private Sub 子控件布局变更(sender As Object, e As EventArgs)
        更新滚动区域()
        Me.Invalidate()
    End Sub

    Private Sub 子控件位置变更(sender As Object, e As EventArgs)
        If _suppressLocationSync Then Return
        Dim ctrl = DirectCast(sender, Control)
        If ctrl.Dock <> DockStyle.None Then Return
        Dim tag As ChildLayoutInfo = Nothing
        If Not _childLayouts.TryGetValue(ctrl, tag) Then Return
        Dim ep As Padding = 获取有效内边距()
        tag.DesignLeft = ctrl.Left - ep.Left + _hScrollOffset
        tag.DesignTop = ctrl.Top - ep.Top + _vScrollOffset
    End Sub

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        MyBase.OnLayout(levent)
        If AutoSize Then
            PerformAutoSize()
        End If
    End Sub

#End Region

#Region "AutoSize"

    <Category("LakeUI"), Description("是否根据子控件自动调整面板大小"), DefaultValue(False), Browsable(True)>
    Public Overrides Property AutoSize As Boolean
        Get
            Return MyBase.AutoSize
        End Get
        Set(value As Boolean)
            MyBase.AutoSize = value
            If value Then PerformAutoSize()
        End Set
    End Property

    Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
        Dim content As Size = 计算内容总大小()
        Dim ep As Padding = 获取有效内边距()
        Return New Size(
            content.Width + ep.Horizontal,
            content.Height + ep.Vertical)
    End Function

    Private Sub PerformAutoSize()
        Dim preferred As Size = GetPreferredSize(Size.Empty)
        If Me.Size <> preferred Then
            Me.Size = preferred
        End If
    End Sub

#End Region

#Region "公开方法"

    ''' <summary>滚动到指定的像素偏移位置。</summary>
    Public Sub ScrollTo(horizontalOffset As Integer, verticalOffset As Integer)
        _hScrollOffset = Math.Max(0, horizontalOffset)
        _vScrollOffset = Math.Max(0, verticalOffset)
        更新滚动区域()
        Me.Invalidate()
    End Sub

    Private Sub ModernPanel_Load(sender As Object, e As EventArgs) Handles MyBase.Load

    End Sub

    ''' <summary>获取当前垂直滚动偏移。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property VerticalScrollOffset As Integer
        Get
            Return _vScrollOffset
        End Get
    End Property

    ''' <summary>获取当前水平滚动偏移。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property HorizontalScrollOffset As Integer
        Get
            Return _hScrollOffset
        End Get
    End Property

#End Region

End Class