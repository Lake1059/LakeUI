Imports System.ComponentModel
Imports System.ComponentModel.Design
Imports System.Drawing.Drawing2D

<ToolboxItem(True)>
<Designer("System.Windows.Forms.Design.ComponentDesigner, System.Design", GetType(IDesigner))>
Public Class ReDrawContextMenuStrip
    Inherits ContextMenuStrip

    Private ReadOnly _renderer As ReDrawContextMenuStripRenderer

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            Invalidate()
        End If
    End Sub

#Region "DPI"
    Private _dpi As Integer = 1
    <Category("LakeUI"), Description("DPI 缩放倍率"), DefaultValue(1), Browsable(True)>
    Public Property DPI As Integer
        Get
            Return _dpi
        End Get
        Set(value As Integer)
            If value < 1 Then value = 1
            SetValue(_dpi, value)
        End Set
    End Property
#End Region

#Region "颜色"
    Private 背景色 As Color = Color.FromArgb(36, 36, 36)
    <Category("LakeUI"), Description("菜单背景颜色"), DefaultValue(GetType(Color), "36, 36, 36"), Browsable(True)>
    Public Property MenuBackColor As Color
        Get
            Return 背景色
        End Get
        Set(value As Color)
            SetValue(背景色, value)
        End Set
    End Property

    Private 边框色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("菜单边框颜色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
    Public Property MenuBorderColor As Color
        Get
            Return 边框色
        End Get
        Set(value As Color)
            SetValue(边框色, value)
        End Set
    End Property

    Private 选中背景色 As Color = Color.FromArgb(64, 64, 64)
    <Category("LakeUI"), Description("菜单项选中时的背景颜色"), DefaultValue(GetType(Color), "64, 64, 64"), Browsable(True)>
    Public Property MenuSelectedColor As Color
        Get
            Return 选中背景色
        End Get
        Set(value As Color)
            SetValue(选中背景色, value)
        End Set
    End Property

    Private 文本颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("菜单项文本颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property MenuForeColor As Color
        Get
            Return 文本颜色
        End Get
        Set(value As Color)
            SetValue(文本颜色, value)
        End Set
    End Property

    Private 分割线颜色 As Color = Color.FromArgb(80, 80, 80)
    <Category("LakeUI"), Description("分割线颜色"), DefaultValue(GetType(Color), "80, 80, 80"), Browsable(True)>
    Public Property SeparatorColor As Color
        Get
            Return 分割线颜色
        End Get
        Set(value As Color)
            SetValue(分割线颜色, value)
        End Set
    End Property

    Private 箭头颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("子菜单箭头颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property ArrowColor As Color
        Get
            Return 箭头颜色
        End Get
        Set(value As Color)
            SetValue(箭头颜色, value)
        End Set
    End Property

    Private 勾选颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("复选标记颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property CheckMarkColor As Color
        Get
            Return 勾选颜色
        End Get
        Set(value As Color)
            SetValue(勾选颜色, value)
        End Set
    End Property

    Private 勾选背景色 As Color = Color.FromArgb(64, 64, 64)
    <Category("LakeUI"), Description("复选标记背景颜色"), DefaultValue(GetType(Color), "64, 64, 64"), Browsable(True)>
    Public Property CheckMarkBackColor As Color
        Get
            Return 勾选背景色
        End Get
        Set(value As Color)
            SetValue(勾选背景色, value)
        End Set
    End Property
#End Region

#Region "尺寸"
    Private 边框宽度 As Integer = 1
    <Category("LakeUI"), Description("边框宽度"), DefaultValue(1), Browsable(True)>
    Public Property BorderSize As Integer
        Get
            Return 边框宽度
        End Get
        Set(value As Integer)
            If value < 0 Then value = 0
            SetValue(边框宽度, value)
            Padding = New Padding(边框宽度)
        End Set
    End Property

    Private 项目内边距 As New Padding(0, 5, 0, 5)
    <Category("LakeUI"), Description("菜单项内边距（会乘以 DPI）"), DefaultValue(GetType(Padding), "0, 5, 0, 5"), Browsable(True)>
    Public Property ItemPaddingSize As Padding
        Get
            Return 项目内边距
        End Get
        Set(value As Padding)
            SetValue(项目内边距, value)
        End Set
    End Property

    Private 选中高亮边距 As New Padding(2, 0, 1, 0)
    <Category("LakeUI"), Description("选中高亮的内缩边距"), DefaultValue(GetType(Padding), "2, 0, 1, 0"), Browsable(True)>
    Public Property SelectedHighlightMargin As Padding
        Get
            Return 选中高亮边距
        End Get
        Set(value As Padding)
            SetValue(选中高亮边距, value)
        End Set
    End Property

    Private 分割线高度 As Integer = 1
    <Category("LakeUI"), Description("分割线高度"), DefaultValue(1), Browsable(True)>
    Public Property SeparatorHeight As Integer
        Get
            Return 分割线高度
        End Get
        Set(value As Integer)
            If value < 1 Then value = 1
            SetValue(分割线高度, value)
        End Set
    End Property

    Private 分割线外边距 As Integer = 0
    <Category("LakeUI"), Description("分割线上下外边距（会乘以 DPI）"), DefaultValue(0), Browsable(True)>
    Public Property SeparatorMarginSize As Integer
        Get
            Return 分割线外边距
        End Get
        Set(value As Integer)
            If value < 0 Then value = 0
            SetValue(分割线外边距, value)
        End Set
    End Property

    Private 空白分隔高度 As Integer = 0
    <Category("LakeUI"), Description("空白分隔高度（会乘以 DPI，Tag 为 nothing/null 时使用）"), DefaultValue(0), Browsable(True)>
    Public Property SpacerHeight As Integer
        Get
            Return 空白分隔高度
        End Get
        Set(value As Integer)
            If value < 0 Then value = 0
            SetValue(空白分隔高度, value)
        End Set
    End Property

    Private 禁用时文本字号偏移 As Single = -2
    <Category("LakeUI"), Description("禁用项目文本字号偏移量"), DefaultValue(-2.0F), Browsable(True)>
    Public Property DisabledFontSizeOffset As Single
        Get
            Return 禁用时文本字号偏移
        End Get
        Set(value As Single)
            SetValue(禁用时文本字号偏移, value)
        End Set
    End Property
#End Region

    Public Sub New()
        _renderer = New ReDrawContextMenuStripRenderer(Me)
        Renderer = _renderer
        Padding = New Padding(边框宽度)
    End Sub

    Private Class ReDrawContextMenuStripRenderer
        Inherits ToolStripRenderer

        Private ReadOnly _owner As ReDrawContextMenuStrip

        Friend Sub New(owner As ReDrawContextMenuStrip)
            _owner = owner
        End Sub

#Region "Tag 常量"
        Private Const TagLabel As String = "label"
        Private Const TagNothing As String = "nothing"
        Private Const TagNull As String = "null"
#End Region

#Region "辅助方法"
        ''' <summary>按 DPI 缩放项目内边距</summary>
        Private Function GetScaledItemPadding() As Padding
            Dim d As Integer = _owner._dpi
            Dim p = _owner.项目内边距
            Return New Padding(p.Left * d, p.Top * d, p.Right * d, p.Bottom * d)
        End Function

        ''' <summary>统一同步菜单项的 Padding / Margin</summary>
        Private Sub ApplyItemLayout(item As ToolStripItem)
            Dim tag As String = TryCast(item.Tag, String)
            If tag = TagLabel Then
                If item.Padding <> Padding.Empty Then item.Padding = Padding.Empty
            Else
                Dim expected = GetScaledItemPadding()
                If item.Padding <> expected Then item.Padding = expected
            End If
            If item.Margin <> Padding.Empty Then item.Margin = Padding.Empty
        End Sub

        ''' <summary>绘制矢量勾选标记（无自定义图像时使用）</summary>
        Private Sub DrawCheckMark(g As Graphics, imageRect As Rectangle, itemHeight As Integer)
            Dim cx As Single = imageRect.X + imageRect.Width / 2.0F
            Dim cy As Single = itemHeight / 2.0F
            Dim s As Single = itemHeight * 0.18F
            Dim pw As Single = Math.Max(1.6F, itemHeight * 0.08F)

            Dim oldSmooth = g.SmoothingMode
            g.SmoothingMode = SmoothingMode.AntiAlias

            Using path As New GraphicsPath()
                path.AddLines({
                    New PointF(cx - s, cy),
                    New PointF(cx - s * 0.35F, cy + s * 0.85F),
                    New PointF(cx + s, cy - s)
                })
                Using wp As New Pen(Color.Black, pw)
                    wp.StartCap = LineCap.Round
                    wp.EndCap = LineCap.Round
                    wp.LineJoin = LineJoin.Round
                    path.Widen(wp)
                End Using
                Using brush As New SolidBrush(_owner.勾选颜色)
                    g.FillPath(brush, path)
                End Using
            End Using

            g.SmoothingMode = oldSmooth
        End Sub
#End Region

        Protected Overrides Sub Initialize(toolStrip As ToolStrip)
            MyBase.Initialize(toolStrip)
            toolStrip.BackColor = _owner.背景色
            toolStrip.ForeColor = _owner.文本颜色
            toolStrip.Padding = New Padding(_owner.边框宽度)
        End Sub

        Protected Overrides Sub InitializeItem(item As ToolStripItem)
            MyBase.InitializeItem(item)
            ApplyItemLayout(item)
        End Sub

        Protected Overrides Sub OnRenderToolStripBackground(e As ToolStripRenderEventArgs)
            Dim ts = e.ToolStrip
            Dim expectedPad As New Padding(_owner.边框宽度)
            If ts.Padding <> expectedPad Then ts.Padding = expectedPad
            If ts.BackColor <> _owner.背景色 Then ts.BackColor = _owner.背景色
            If ts.ForeColor <> _owner.文本颜色 Then ts.ForeColor = _owner.文本颜色
            ' 同步子菜单的图标列 / 复选列显示状态
            If TypeOf ts Is ToolStripDropDownMenu AndAlso ts IsNot _owner Then
                Dim ddm = DirectCast(ts, ToolStripDropDownMenu)
                If ddm.ShowImageMargin <> _owner.ShowImageMargin Then ddm.ShowImageMargin = _owner.ShowImageMargin
                If ddm.ShowCheckMargin <> _owner.ShowCheckMargin Then ddm.ShowCheckMargin = _owner.ShowCheckMargin
            End If
            Using b As New SolidBrush(_owner.背景色)
                e.Graphics.FillRectangle(b, e.AffectedBounds)
            End Using
        End Sub

        Protected Overrides Sub OnRenderToolStripBorder(e As ToolStripRenderEventArgs)
            If _owner.边框宽度 > 0 Then
                Dim half As Single = _owner.边框宽度 / 2.0F
                Using p As New Pen(_owner.边框色, _owner.边框宽度)
                    e.Graphics.DrawRectangle(p, half, half, e.AffectedBounds.Width - _owner.边框宽度, e.AffectedBounds.Height - _owner.边框宽度)
                End Using
            End If
        End Sub

        Protected Overrides Sub OnRenderImageMargin(e As ToolStripRenderEventArgs)
            Using b As New SolidBrush(_owner.背景色)
                e.Graphics.FillRectangle(b, e.AffectedBounds)
            End Using
        End Sub

        Protected Overrides Sub OnRenderSeparator(e As ToolStripSeparatorRenderEventArgs)
            Dim d As Integer = _owner._dpi
            Dim tag As String = TryCast(e.Item.Tag, String)

            e.Item.AutoSize = False
            e.Item.Padding = Padding.Empty

            If tag = TagNothing OrElse tag = TagNull Then
                e.Item.Margin = Padding.Empty
                e.Item.Height = _owner.空白分隔高度 * d
                Return
            End If

            e.Item.Height = _owner.分割线高度
            e.Item.Margin = New Padding(0, _owner.分割线外边距 * d, 0, _owner.分割线外边距 * d)
            Using b As New SolidBrush(_owner.分割线颜色)
                e.Graphics.FillRectangle(b, 0, 0, e.Item.Width, e.Item.Height)
            End Using
        End Sub

        Protected Overrides Sub OnRenderArrow(e As ToolStripArrowRenderEventArgs)
            e.ArrowColor = _owner.箭头颜色
            e.ArrowRectangle = New Rectangle(New Point(e.ArrowRectangle.Left, e.ArrowRectangle.Top - 1), e.ArrowRectangle.Size)
            MyBase.OnRenderArrow(e)
        End Sub

        Protected Overrides Sub OnRenderMenuItemBackground(e As ToolStripItemRenderEventArgs)
            ApplyItemLayout(e.Item)

            Dim tag As String = TryCast(e.Item.Tag, String)
            If Not e.Item.Enabled OrElse tag = TagLabel Then Return

            If e.Item.Selected Then
                Dim m = _owner.选中高亮边距
                Dim rect As New Rectangle(m.Left, m.Top, e.Item.Width - m.Horizontal, e.Item.Height - m.Vertical)
                Using b As New SolidBrush(_owner.选中背景色)
                    e.Graphics.FillRectangle(b, rect)
                End Using
            End If
        End Sub

        Protected Overrides Sub OnRenderItemImage(e As ToolStripItemImageRenderEventArgs)
            If e.Image Is Nothing Then Return
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
            e.Graphics.DrawImage(e.Image, e.ImageRectangle)
        End Sub

        Protected Overrides Sub OnRenderItemCheck(e As ToolStripItemImageRenderEventArgs)
            Dim g = e.Graphics

            If e.Image IsNot Nothing Then
                g.InterpolationMode = InterpolationMode.HighQualityBicubic
                g.DrawImage(e.Image, e.ImageRectangle)
                Return
            End If

            ' 不绘制背景：让 OnRenderMenuItemBackground 的高亮自然透出，
            ' 勾选区域的背景色始终与菜单项焦点态保持一致。
            DrawCheckMark(g, e.ImageRectangle, e.Item.Height)
        End Sub

        Protected Overrides Sub OnRenderItemText(e As ToolStripItemTextRenderEventArgs)
            Dim textRect As New Rectangle(e.TextRectangle.Left, e.Item.ContentRectangle.Top, e.TextRectangle.Width, e.Item.ContentRectangle.Height)
            Dim escapedText As String = e.Text.Replace("&", "&&")

            If e.Item.Enabled Then
                TextRenderer.DrawText(e.Graphics, escapedText, e.TextFont, textRect, _owner.文本颜色, Nothing, TextFormatFlags.VerticalCenter)
            Else
                e.Item.Margin = Padding.Empty
                e.Item.Padding = Padding.Empty
                Dim tag As String = TryCast(e.Item.Tag, String)
                Dim fontSize As Single = If(tag = TagLabel, e.TextFont.Size, Math.Max(1, e.TextFont.Size + _owner.禁用时文本字号偏移))
                Using f As New Font(e.TextFont.Name, fontSize, FontStyle.Regular)
                    TextRenderer.DrawText(e.Graphics, escapedText, f, textRect, e.TextColor, Nothing, TextFormatFlags.VerticalCenter)
                End Using
            End If
        End Sub

    End Class

End Class
