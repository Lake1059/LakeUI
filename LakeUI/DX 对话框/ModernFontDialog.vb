Imports System.ComponentModel
Imports System.Drawing.Text
Imports System.Globalization
Imports System.Numerics
Imports System.Runtime.InteropServices
Imports D2D = Vortice.Direct2D1
Imports DW = Vortice.DirectWrite

Public Class ModernFontDialog
    Implements V3_IGpuRenderable, V3_IGpuInvalidationSource

    Public Sub New()
        MyBase.New()
        InitializeComponent()
    End Sub

#Region "公共属性"

    Private _selectedFont As Font = New Font("Microsoft YaHei UI", 10.0F)

    ''' <summary>
    ''' 获取或设置用户选择的字体。
    ''' </summary>
    <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    <Browsable(False)>
    Public Property SelectedFont As Font
        Get
            Return _selectedFont
        End Get
        Set(value As Font)
            设置选中字体核心(If(value, New Font("Microsoft YaHei UI", 10.0F)), True)
        End Set
    End Property

#End Region

#Region "私有字段"

    Private _fontFamilies As FontFamily()
    Private _allFontNames As List(Of String)
    Private _suppressTextBoxEvent As Boolean
    Private _suppressListBoxEvent As Boolean
    Private _isClosing As Boolean

    Private Shared ReadOnly _numericFontSizeValues As Single() = {
        5.0F, 5.5F, 6.5F, 7.5F, 8.0F, 9.0F, 10.0F, 10.5F,
        11.0F, 12.0F, 14.0F, 15.0F, 16.0F, 18.0F, 20.0F,
        22.0F, 24.0F, 26.0F, 28.0F, 36.0F, 42.0F, 48.0F, 72.0F
    }

#End Region

#Region "D2D 公共接口"

    Private _d2dElementBackColor As Color = Color.FromArgb(40, 220, 220, 220)
    <Category("LakeUI"), Description("按钮、输入框和列表的基础背景色。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property D2DElementBackColor As Color
        Get
            Return _d2dElementBackColor
        End Get
        Set(value As Color)
            If _d2dElementBackColor = value Then Return
            _d2dElementBackColor = value
            Invalidate()
        End Set
    End Property

    Private _d2dElementHoverBackColor As Color = Color.FromArgb(60, 220, 220, 220)
    <Category("LakeUI"), Description("按钮和列表项等元素的悬停背景色。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property D2DElementHoverBackColor As Color
        Get
            Return _d2dElementHoverBackColor
        End Get
        Set(value As Color)
            If _d2dElementHoverBackColor = value Then Return
            _d2dElementHoverBackColor = value
            Invalidate()
        End Set
    End Property

    Private _d2dElementPressedBackColor As Color = Color.FromArgb(80, 220, 220, 220)
    <Category("LakeUI"), Description("按钮按下和列表项选中时的背景色。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property D2DElementPressedBackColor As Color
        Get
            Return _d2dElementPressedBackColor
        End Get
        Set(value As Color)
            If _d2dElementPressedBackColor = value Then Return
            _d2dElementPressedBackColor = value
            Invalidate()
        End Set
    End Property

    Private _d2dElementBorderColor As Color = Color.FromArgb(120, 220, 220, 220)
    <Category("LakeUI"), Description("输入框获得焦点时的边框色。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property D2DElementBorderColor As Color
        Get
            Return _d2dElementBorderColor
        End Get
        Set(value As Color)
            If _d2dElementBorderColor = value Then Return
            _d2dElementBorderColor = value
            Invalidate()
        End Set
    End Property

    Private _d2dTextProvider As Func(Of String, String)
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property D2DTextProvider As Func(Of String, String)
        Get
            Return _d2dTextProvider
        End Get
        Set(value As Func(Of String, String))
            _d2dTextProvider = value
            翻译文本已变化()
        End Set
    End Property

    Private ReadOnly _d2dTextOverrides As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    Public Sub SetD2DText(key As String, text As String)
        If String.IsNullOrWhiteSpace(key) Then Return
        _d2dTextOverrides(key) = If(text, String.Empty)
        翻译文本已变化()
    End Sub

    Public Sub ClearD2DTextOverrides()
        _d2dTextOverrides.Clear()
        翻译文本已变化()
    End Sub

    Private _d2dKeepWindowBackdropTransparent As Boolean = True
    <Category("LakeUI"), Description("挂接 ThisIsYourWindow 等窗口级背景时，客户区底色保持透明，只绘制 D2D 交互元素。"),
     DefaultValue(True)>
    Public Property D2DKeepWindowBackdropTransparent As Boolean
        Get
            Return _d2dKeepWindowBackdropTransparent
        End Get
        Set(value As Boolean)
            If _d2dKeepWindowBackdropTransparent = value Then Return
            _d2dKeepWindowBackdropTransparent = value
            Invalidate()
        End Set
    End Property

    Private Shared ReadOnly _d2dTextKeys As String() = {
        "WindowTitle",
        "FontTitle",
        "StyleTitle",
        "SizeTitle",
        "EffectsTitle",
        "PreviewTitle",
        "FontWatermark",
        "StyleWatermark",
        "SizeWatermark",
        "Strikeout",
        "Underline",
        "StyleRegular",
        "StyleBold",
        "StyleItalic",
        "StyleBoldItalic",
        "SizeInitial",
        "SizeSmallInitial",
        "SizeOne",
        "SizeSmallOne",
        "SizeTwo",
        "SizeSmallTwo",
        "SizeThree",
        "SizeSmallThree",
        "SizeFour",
        "SizeSmallFour",
        "SizeFive",
        "SizeSmallFive",
        "SizeSix",
        "SizeSmallSix",
        "SizeSeven",
        "SizeEight",
        "PreviewSampleChinese",
        "PreviewSampleEnglish",
        "PreviewSampleGlyphs",
        "PreviewSampleSymbols",
        "OK",
        "Cancel"
    }

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shared ReadOnly Property D2DTextKeys As IReadOnlyList(Of String)
        Get
            Return _d2dTextKeys
        End Get
    End Property

#End Region

#Region "D2D 状态"

    Private Const WM_GETDLGCODE_D2D As Integer = &H87
    Private Const WM_CHAR_D2D As Integer = &H102
    Private Const WM_IME_STARTCOMPOSITION_D2D As Integer = &H10D
    Private Const WM_IME_ENDCOMPOSITION_D2D As Integer = &H10E
    Private Const WM_IME_COMPOSITION_D2D As Integer = &H10F
    Private Const WM_ENTERSIZEMOVE_D2D As Integer = &H231
    Private Const WM_EXITSIZEMOVE_D2D As Integer = &H232
    Private Const GCS_RESULTSTR_D2D As Integer = &H800
    Private Const DLGC_WANTCHARS_D2D As Integer = &H80
    Private Const DLGC_WANTALLKEYS_D2D As Integer = &H4

    Private _d2dInitialized As Boolean
    Private _d2dLayoutDirty As Boolean = True
    Private _d2dLayout As FontDialogD2DLayout
    Private ReadOnly _d2dTextBoxes As New Dictionary(Of FontDialogTextBoxKind, VirtualTextBox)()
    Private ReadOnly _d2dTextBoxOrder As New List(Of VirtualTextBox)()
    Private _d2dActiveTextBox As VirtualTextBox
    Private _d2dMouseTextBox As VirtualTextBox
    Private _d2dCapturePart As FontDialogHitPart = FontDialogHitPart.None
    Private _d2dPressedButton As FontDialogButtonKind = FontDialogButtonKind.None
    Private _d2dHoverButton As FontDialogButtonKind = FontDialogButtonKind.None
    Private _d2dPressedSwitch As FontDialogSwitchKind = FontDialogSwitchKind.None
    Private _d2dHoverSwitch As FontDialogSwitchKind = FontDialogSwitchKind.None
    Private _d2dDragList As VirtualList
    Private _d2dListScrollDragOffset As Single
    Private _d2dImeComposing As Boolean
    Private _d2dInSizeMove As Boolean
    Private _strikeoutChecked As Boolean
    Private _underlineChecked As Boolean

    Private ReadOnly _fontList As New VirtualList(FontDialogListKind.FontName)
    Private ReadOnly _styleList As New VirtualList(FontDialogListKind.Style)
    Private ReadOnly _sizeList As New VirtualList(FontDialogListKind.Size)
    Private ReadOnly _styleEntries As New List(Of FontStyleEntry)()
    Private ReadOnly _sizeEntries As New List(Of FontSizeEntry)()

    Private Enum FontDialogHitPart
        None
        TextBox
        ListScrollBar
        Button
        Switch
    End Enum

    Private Enum FontDialogTextBoxKind
        FontName
        Style
        Size
    End Enum

    Private Enum FontDialogListKind
        FontName
        Style
        Size
    End Enum

    Private Enum FontDialogButtonKind
        None
        OK
        Cancel
    End Enum

    Private Enum FontDialogSwitchKind
        None
        Strikeout
        Underline
    End Enum

    Private NotInheritable Class VirtualTextBox
        Public Kind As FontDialogTextBoxKind
        Public Renderer As SingleLineTextBoxRenderer
        Public Bounds As RectangleF
        Public TextArea As RectangleF
    End Class

    Private NotInheritable Class VirtualList
        Public Sub New(kind As FontDialogListKind)
            Me.Kind = kind
        End Sub

        Public Kind As FontDialogListKind
        Public ReadOnly Items As New List(Of String)()
        Public Bounds As RectangleF
        Public Viewport As RectangleF
        Public ScrollBarTrack As RectangleF
        Public SelectedIndex As Integer = -1
        Public HoverIndex As Integer = -1
        Public ScrollIndex As Integer
    End Class

    Private NotInheritable Class FontStyleEntry
        Public Key As String
        Public Fallback As String
        Public DisplayName As String
        Public BaseStyle As Drawing.FontStyle
        Public DWriteWeight As DW.FontWeight = DW.FontWeight.Normal
        Public DWriteStyle As DW.FontStyle = DW.FontStyle.Normal
        Public DWriteStretch As DW.FontStretch = DW.FontStretch.Normal
        Public IsDWriteFace As Boolean
    End Class

    Private NotInheritable Class FontStyleTarget
        Public BaseStyle As Drawing.FontStyle
        Public DWriteWeight As DW.FontWeight = DW.FontWeight.Normal
        Public DWriteStyle As DW.FontStyle = DW.FontStyle.Normal
        Public DWriteStretch As DW.FontStretch = DW.FontStretch.Normal
    End Class

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Auto)>
    Private NotInheritable Class GdiLogFont
        Public lfHeight As Integer
        Public lfWidth As Integer
        Public lfEscapement As Integer
        Public lfOrientation As Integer
        Public lfWeight As Integer
        Public lfItalic As Byte
        Public lfUnderline As Byte
        Public lfStrikeOut As Byte
        Public lfCharSet As Byte
        Public lfOutPrecision As Byte
        Public lfClipPrecision As Byte
        Public lfQuality As Byte
        Public lfPitchAndFamily As Byte
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=32)>
        Public lfFaceName As String
    End Class

    Private NotInheritable Class FontSizeEntry
        Public Key As String
        Public Fallback As String
        Public Points As Single
        Public IsNamedSize As Boolean
    End Class

    Private NotInheritable Class FontDialogD2DLayout
        Public Client As RectangleF
        Public FontTitle As RectangleF
        Public FontTextBox As RectangleF
        Public FontList As RectangleF
        Public StyleTitle As RectangleF
        Public StyleTextBox As RectangleF
        Public StyleList As RectangleF
        Public SizeTitle As RectangleF
        Public SizeTextBox As RectangleF
        Public SizeList As RectangleF
        Public EffectsTitle As RectangleF
        Public StrikeoutSwitch As RectangleF
        Public StrikeoutText As RectangleF
        Public UnderlineSwitch As RectangleF
        Public UnderlineText As RectangleF
        Public PreviewTitle As RectangleF
        Public PreviewLines(3) As RectangleF
        Public ButtonOK As RectangleF
        Public ButtonCancel As RectangleF
        Public ItemHeight As Single
        Public TextBoxPaddingX As Single
        Public TextBoxPaddingY As Single
        Public ListPadding As Single
        Public ListItemPaddingLeft As Single
        Public Radius As Single
        Public ScrollBarWidth As Single
    End Class

#End Region

#Region "生命周期"

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        初始化D2D字体对话框()
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        显示后刷新D2D()
    End Sub

    Protected Overrides Sub OnVisibleChanged(e As EventArgs)
        MyBase.OnVisibleChanged(e)
        If Visible Then 显示后刷新D2D()
    End Sub

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        If 处理D2D快捷键(msg, keyData) Then Return True
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        _isClosing = True
        清理D2D字体对话框()
        MyBase.OnFormClosed(e)
    End Sub

    Private Sub 初始化D2D字体对话框()
        If _d2dInitialized Then Return
        _d2dInitialized = True

        SetStyle(ControlStyles.UserPaint Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.Selectable, True)
        UpdateStyles()
        KeyPreview = True
        Text = 取界面文本("WindowTitle", "FontDialog")

        创建D2D文本框()

        Using ifc As New InstalledFontCollection()
            _fontFamilies = ifc.Families
        End Using

        _allFontNames = New List(Of String)(_fontFamilies.Length)
        For Each family In _fontFamilies
            _allFontNames.Add(family.Name)
        Next

        重建字体列表(String.Empty)
        重建字号列表()
        应用字体到界面(SelectedFont)
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Try : ImeHelper.AssociateDefault(Handle) : Catch : End Try
    End Sub

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' V3-only: dialog pixels are emitted by RenderGpu.
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If _isClosing OrElse IsDisposed OrElse Disposing Then
            Return
        End If
        If _d2dInSizeMove Then
            If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
            Return
        End If

        初始化D2D字体对话框()
        确保D2D布局()
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        If context Is Nothing OrElse _isClosing OrElse IsDisposed OrElse Disposing Then Return

        初始化D2D字体对话框()
        确保D2D布局()

        Dim keepWindowBackdropTransparent As Boolean = 应保持窗口级背景透明()
        ThisIsYourWindow.TryRenderAttachedChrome(context, Me)

        If BackColor.A > 0 AndAlso Not keepWindowBackdropTransparent Then
            context.FillRectangle(DisplayRectangle, BackColor)
        End If

        绘制D2D图形层_GPU(context)
        绘制D2D文字层_GPU(context)
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        _d2dLayoutDirty = True
        If Not _d2dInSizeMove AndAlso WindowState <> FormWindowState.Minimized Then Invalidate()
    End Sub

    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        _d2dLayoutDirty = True
        If IsHandleCreated Then Invalidate()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        D3D_RenderCore.InvalidateExistingTextResources(Me)
        For Each box In _d2dTextBoxOrder
            box.Renderer.LineHeight = 缩放值(24)
        Next
        _d2dLayoutDirty = True
        RequestV3Render()
    End Sub

    Private Sub 清理D2D字体对话框()
        For Each box In _d2dTextBoxOrder
            Try : box.Renderer.StopCaretBlink() : Catch : End Try
        Next
    End Sub

    Private Sub 显示后刷新D2D()
        If Not _d2dInitialized Then Return
        _d2dLayoutDirty = True
        确保D2D布局()
        Invalidate()
    End Sub

    Private Function 应保持窗口级背景透明() As Boolean
        If Not D2DKeepWindowBackdropTransparent Then Return False
        If Not (Padding.Left > 0 OrElse Padding.Top > 0 OrElse Padding.Right > 0 OrElse Padding.Bottom > 0) Then Return False
        Return ThisIsYourWindow.AttachedBackdropCoversClient(Me)
    End Function

#End Region

#Region "布局"

    Private Sub 确保D2D布局()
        If _d2dTextBoxes.Count = 0 Then 创建D2D文本框()
        If _d2dLayout Is Nothing Then _d2dLayoutDirty = True
        If Not _d2dLayoutDirty Then Return
        _d2dLayoutDirty = False

        Dim s = 取D2D缩放()
        Dim textFormatCache As D3D_D2DInterop.TextFormatCache = Nothing
        Dim display = DisplayRectangle
        Dim leftW As Single = 300.0F * s
        Dim topH As Single = 300.0F * s
        Dim bottomH As Single = 75.0F * s
        Dim pad As Single = 20.0F * s
        Dim zeroRightPad As Single = 0.0F
        Dim titleH As Single = 30.0F * s
        Dim textBoxH As Single = 36.0F * s
        Dim listGap As Single = 10.0F * s
        Dim buttonH As Single = 35.0F * s
        Dim buttonW As Single = 120.0F * s
        Dim buttonGap As Single = 10.0F * s
        Dim rowH As Single = 30.0F * s
        Dim checkBoxSize As Single = 24.0F * s
        Dim radius As Single = 10.0F * s
        Dim compactListH As Single = 5.0F * s * 2.0F + 26.0F * s * 7.0F

        If display.Width < leftW + 260.0F * s Then leftW = Math.Max(220.0F * s, display.Width * 0.35F)
        If display.Height < topH + bottomH + 80.0F * s Then topH = Math.Max(220.0F * s, display.Height - bottomH - 120.0F * s)

        Dim rightX As Single = display.X + leftW
        Dim rightW As Single = Math.Max(1.0F, display.Right - rightX)
        Dim bottomTop As Single = display.Bottom - bottomH
        Dim previewTop As Single = display.Y + topH

        Dim layout As New FontDialogD2DLayout With {
            .Client = New RectangleF(display.X, display.Y, display.Width, display.Height),
            .ItemHeight = 26.0F * s,
            .TextBoxPaddingX = 10.0F * s,
            .TextBoxPaddingY = 2.0F * s,
            .ListPadding = 5.0F * s,
            .ListItemPaddingLeft = 5.0F * s,
            .Radius = radius,
            .ScrollBarWidth = 9.0F * s
        }

        Dim fontColX As Single = display.X + pad
        Dim fontColW As Single = Math.Max(1.0F, leftW - pad - zeroRightPad)
        layout.FontTitle = New RectangleF(fontColX, display.Y + pad, fontColW, titleH)
        layout.FontTextBox = New RectangleF(fontColX, display.Y + pad + titleH, fontColW, textBoxH)
        layout.FontList = New RectangleF(fontColX,
                                         layout.FontTextBox.Bottom + listGap,
                                         fontColW,
                                         Math.Max(1.0F, display.Bottom - pad - (layout.FontTextBox.Bottom + listGap)))

        Dim stylePanelW As Single = 200.0F * s
        Dim sizePanelW As Single = 175.0F * s
        If rightW < stylePanelW + sizePanelW + 120.0F * s Then
            stylePanelW = Math.Max(150.0F * s, rightW * 0.34F)
            sizePanelW = Math.Max(120.0F * s, rightW * 0.3F)
        End If

        Dim styleX As Single = rightX
        Dim sizeX As Single = rightX + stylePanelW
        Dim effectsX As Single = rightX + stylePanelW + sizePanelW
        Dim effectsW As Single = Math.Max(1.0F, display.Right - effectsX)

        layout.StyleTitle = New RectangleF(styleX + pad, display.Y + pad, Math.Max(1.0F, stylePanelW - pad), titleH)
        layout.StyleTextBox = New RectangleF(styleX + pad, display.Y + pad + titleH, Math.Max(1.0F, stylePanelW - pad), textBoxH)
        layout.StyleList = New RectangleF(styleX + pad,
                                          layout.StyleTextBox.Bottom + listGap,
                                          Math.Max(1.0F, stylePanelW - pad),
                                          compactListH)

        layout.SizeTitle = New RectangleF(sizeX + pad, display.Y + pad, Math.Max(1.0F, sizePanelW - pad), titleH)
        layout.SizeTextBox = New RectangleF(sizeX + pad, display.Y + pad + titleH, Math.Max(1.0F, sizePanelW - pad), textBoxH)
        layout.SizeList = New RectangleF(sizeX + pad,
                                         layout.SizeTextBox.Bottom + listGap,
                                         Math.Max(1.0F, sizePanelW - pad),
                                         compactListH)

        Dim effectsContentX As Single = effectsX + pad
        Dim effectsContentW As Single = Math.Max(1.0F, effectsW - pad * 2.0F)
        layout.EffectsTitle = New RectangleF(effectsContentX, display.Y + pad, effectsContentW, titleH)
        layout.StrikeoutSwitch = New RectangleF(effectsContentX,
                                                display.Y + pad + titleH + (rowH - checkBoxSize) / 2.0F,
                                                checkBoxSize,
                                                checkBoxSize)
        layout.StrikeoutText = New RectangleF(layout.StrikeoutSwitch.Right + 10.0F * s,
                                              display.Y + pad + titleH,
                                              Math.Max(1.0F, effectsContentX + effectsContentW - layout.StrikeoutSwitch.Right - 10.0F * s),
                                              rowH)
        Dim checkBoxGap As Single = 10.0F * s
        Dim underlineBoxY As Single = layout.StrikeoutSwitch.Bottom + checkBoxGap
        Dim underlineRowY As Single = underlineBoxY - (rowH - checkBoxSize) / 2.0F
        layout.UnderlineSwitch = New RectangleF(effectsContentX,
                                                underlineBoxY,
                                                checkBoxSize,
                                                checkBoxSize)
        layout.UnderlineText = New RectangleF(layout.UnderlineSwitch.Right + 10.0F * s,
                                              underlineRowY,
                                              Math.Max(1.0F, effectsContentX + effectsContentW - layout.UnderlineSwitch.Right - 10.0F * s),
                                              rowH)

        Dim previewX As Single = rightX + pad
        Dim previewW As Single = Math.Max(1.0F, rightW - pad * 2.0F)
        Dim previewTitleH As Single = Math.Max(40.0F * s, CSng(Math.Max(1, D3D_TextInterop.MeasureLineHeight(Font, s, textFormatCache))) + 20.0F * s)
        Dim previewLineH As Single = Math.Max(24.0F * s, CSng(Math.Max(1, D3D_TextInterop.MeasureLineHeight(SelectedFont, s, textFormatCache))) + 5.0F * s)
        layout.PreviewTitle = New RectangleF(previewX, previewTop, previewW, previewTitleH)
        Dim lineY As Single = layout.PreviewTitle.Bottom
        For i = 0 To 3
            layout.PreviewLines(i) = New RectangleF(previewX, lineY, previewW, previewLineH)
            lineY += previewLineH
        Next

        Dim buttonTop As Single = bottomTop + pad
        Dim buttonRight As Single = display.Right - pad
        layout.ButtonCancel = New RectangleF(buttonRight - buttonW, buttonTop, buttonW, buttonH)
        layout.ButtonOK = New RectangleF(layout.ButtonCancel.Left - buttonGap - buttonW, buttonTop, buttonW, buttonH)

        _d2dLayout = layout
        更新文本框渲染区域()
        更新列表布局()
    End Sub

    Private Sub 更新文本框渲染区域()
        If _d2dLayout Is Nothing Then Return
        _d2dTextBoxes(FontDialogTextBoxKind.FontName).Bounds = _d2dLayout.FontTextBox
        _d2dTextBoxes(FontDialogTextBoxKind.Style).Bounds = _d2dLayout.StyleTextBox
        _d2dTextBoxes(FontDialogTextBoxKind.Size).Bounds = _d2dLayout.SizeTextBox

        For Each box In _d2dTextBoxOrder
            box.TextArea = 调整矩形(box.Bounds, -_d2dLayout.TextBoxPaddingX, -_d2dLayout.TextBoxPaddingY)
            box.Renderer.LineHeight = 缩放值(24)
        Next
    End Sub

    Private Sub 更新列表布局()
        If _d2dLayout Is Nothing Then Return
        _fontList.Bounds = _d2dLayout.FontList
        _styleList.Bounds = _d2dLayout.StyleList
        _sizeList.Bounds = _d2dLayout.SizeList

        For Each list In 所有列表()
            更新单个列表布局(list)
            限制列表滚动(list)
        Next
        确保所有列表选中项可见()
    End Sub

    Private Sub 更新单个列表布局(list As VirtualList)
        Dim pad As Single = _d2dLayout.ListPadding
        Dim s As Single = 取D2D缩放()
        Dim rightEdge As Single = list.Bounds.Right - pad
        Dim scrollX As Single = list.Bounds.Right - _d2dLayout.ScrollBarWidth - 6.0F * s
        Dim visible As Integer = Math.Max(0, CInt(Math.Floor((list.Bounds.Height - pad * 2.0F) / Math.Max(1.0F, _d2dLayout.ItemHeight))))
        If list.Items.Count > visible Then rightEdge = scrollX - pad
        list.Viewport = New RectangleF(list.Bounds.X + pad,
                                       list.Bounds.Y + pad,
                                       Math.Max(1.0F, rightEdge - (list.Bounds.X + pad)),
                                       Math.Max(1.0F, list.Bounds.Height - pad * 2.0F))
        list.ScrollBarTrack = New RectangleF(scrollX,
                                             list.Bounds.Y + 8.0F * s,
                                             _d2dLayout.ScrollBarWidth,
                                             Math.Max(1.0F, list.Bounds.Height - 16.0F * s))
    End Sub

    Private Shared Function 调整矩形(rect As RectangleF, dx As Single, dy As Single) As RectangleF
        rect.Inflate(dx, dy)
        Return rect
    End Function

#End Region

#Region "数据同步"

    Private Sub 应用字体到界面(f As Font)
        If f Is Nothing Then Return
        If _fontFamilies Is Nothing OrElse _allFontNames Is Nothing Then Return

        _suppressTextBoxEvent = True
        _suppressListBoxEvent = True
        Try
            Dim familyName As String = f.FontFamily.Name
            重建字体列表(String.Empty)
            设置文本框文本(FontDialogTextBoxKind.FontName, familyName)
            选择列表文本(_fontList, familyName)

            填充字形列表(familyName, 创建字形目标(f))

            选择字号点数(f.SizeInPoints)

            _strikeoutChecked = f.Strikeout
            _underlineChecked = f.Underline
        Finally
            _suppressListBoxEvent = False
            _suppressTextBoxEvent = False
        End Try

        UpdatePreview()
    End Sub

    Private Sub 重建字体列表(filter As String)
        If _allFontNames Is Nothing Then Return
        Dim items As New List(Of String)()
        Dim f = If(filter, String.Empty).Trim()
        For Each fontName In _allFontNames
            If String.IsNullOrEmpty(f) OrElse fontName.Contains(f, StringComparison.OrdinalIgnoreCase) Then
                items.Add(fontName)
            End If
        Next
        设置列表项目(_fontList, items)
    End Sub

    Private Sub 重建字号列表()
        _sizeEntries.Clear()
        添加字号项("SizeInitial", "初号", 42.0F, True)
        添加字号项("SizeSmallInitial", "小初", 36.0F, True)
        添加字号项("SizeOne", "一号", 26.0F, True)
        添加字号项("SizeSmallOne", "小一", 24.0F, True)
        添加字号项("SizeTwo", "二号", 22.0F, True)
        添加字号项("SizeSmallTwo", "小二", 18.0F, True)
        添加字号项("SizeThree", "三号", 16.0F, True)
        添加字号项("SizeSmallThree", "小三", 15.0F, True)
        添加字号项("SizeFour", "四号", 14.0F, True)
        添加字号项("SizeSmallFour", "小四", 12.0F, True)
        添加字号项("SizeFive", "五号", 10.5F, True)
        添加字号项("SizeSmallFive", "小五", 9.0F, True)
        添加字号项("SizeSix", "六号", 7.5F, True)
        添加字号项("SizeSmallSix", "小六", 6.5F, True)
        添加字号项("SizeSeven", "七号", 5.5F, True)
        添加字号项("SizeEight", "八号", 5.0F, True)
        For Each value In _numericFontSizeValues
            添加字号项(Nothing, 格式化字号点数(value), value, False)
        Next

        Dim items As New List(Of String)(_sizeEntries.Count)
        For Each entry In _sizeEntries
            items.Add(取字号显示文本(entry))
        Next
        设置列表项目(_sizeList, items)
    End Sub

    Private Sub 添加字号项(key As String, fallback As String, points As Single, namedSize As Boolean)
        _sizeEntries.Add(New FontSizeEntry With {
            .Key = key,
            .Fallback = fallback,
            .Points = points,
            .IsNamedSize = namedSize
        })
    End Sub

    Private Sub 填充字形列表(familyName As String, Optional preferredTarget As FontStyleTarget = Nothing)
        Dim family As FontFamily = Nothing
        Try
            If _fontFamilies IsNot Nothing Then
                family = _fontFamilies.FirstOrDefault(Function(f) String.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase))
            End If
        Catch
        End Try

        _styleEntries.Clear()
        添加DWrite字形项(familyName)
        If family IsNot Nothing Then
            If _styleEntries.Count = 0 Then
                If family.IsStyleAvailable(Drawing.FontStyle.Regular) Then 添加字形项("StyleRegular", "常规", Drawing.FontStyle.Regular)
                If family.IsStyleAvailable(Drawing.FontStyle.Bold) Then 添加字形项("StyleBold", "粗体", Drawing.FontStyle.Bold)
                If family.IsStyleAvailable(Drawing.FontStyle.Italic) Then 添加字形项("StyleItalic", "斜体", Drawing.FontStyle.Italic)
                If family.IsStyleAvailable(Drawing.FontStyle.Bold Or Drawing.FontStyle.Italic) Then 添加字形项("StyleBoldItalic", "粗斜体", Drawing.FontStyle.Bold Or Drawing.FontStyle.Italic)
            End If
        End If

        Dim items As New List(Of String)(_styleEntries.Count)
        For Each entry In _styleEntries
            entry.DisplayName = 取字形项显示文本(entry)
            items.Add(entry.DisplayName)
        Next
        设置列表项目(_styleList, items)

        If preferredTarget Is Nothing Then preferredTarget = 创建字形目标(Drawing.FontStyle.Regular)
        Dim foundIdx As Integer = 查找字形目标索引(familyName, preferredTarget)
        If foundIdx < 0 AndAlso _styleEntries.Count > 0 Then foundIdx = 0
        If foundIdx >= 0 Then
            设置列表选中(_styleList, foundIdx, False)
            设置文本框文本(FontDialogTextBoxKind.Style, _styleList.Items(foundIdx))
        End If
        限制列表滚动(_styleList)
    End Sub

    Private Sub 添加字形项(key As String, fallback As String, baseStyle As Drawing.FontStyle)
        _styleEntries.Add(New FontStyleEntry With {
            .Key = key,
            .Fallback = fallback,
            .BaseStyle = baseStyle,
            .DWriteWeight = If((baseStyle And Drawing.FontStyle.Bold) = Drawing.FontStyle.Bold, DW.FontWeight.Bold, DW.FontWeight.Normal),
            .DWriteStyle = If((baseStyle And Drawing.FontStyle.Italic) = Drawing.FontStyle.Italic, DW.FontStyle.Italic, DW.FontStyle.Normal),
            .DWriteStretch = DW.FontStretch.Normal,
            .IsDWriteFace = False
        })
    End Sub

    Private Sub FontTextBox_TextChanged()
        If _suppressTextBoxEvent Then Return

        Dim filter As String = 取文本框文本(FontDialogTextBoxKind.FontName).Trim()
        _suppressListBoxEvent = True
        Try
            重建字体列表(filter)
            Dim exactIdx As Integer = 查找列表文本(_fontList, filter)
            If exactIdx >= 0 Then
                设置列表选中(_fontList, exactIdx, False)
                填充字形列表(filter, 创建当前字形目标())
            End If
        Finally
            _suppressListBoxEvent = False
        End Try

        UpdateFontFromUI()
    End Sub

    Private Sub FontList_SelectedIndexChanged()
        If _suppressListBoxEvent Then Return
        Dim sel As String = 取列表选中项(_fontList)
        If sel Is Nothing Then Return

        _suppressTextBoxEvent = True
        Try
            设置文本框文本(FontDialogTextBoxKind.FontName, sel)
        Finally
            _suppressTextBoxEvent = False
        End Try

        填充字形列表(sel, 创建当前字形目标())
        UpdateFontFromUI()
    End Sub

    Private Sub StyleTextBox_TextChanged()
        If _suppressTextBoxEvent Then Return
        Dim filter As String = 取文本框文本(FontDialogTextBoxKind.Style).Trim()
        _suppressListBoxEvent = True
        Try
            Dim exactIdx As Integer = 查找列表文本(_styleList, filter)
            If exactIdx >= 0 Then 设置列表选中(_styleList, exactIdx, False)
        Finally
            _suppressListBoxEvent = False
        End Try
        UpdateFontFromUI()
    End Sub

    Private Sub StyleList_SelectedIndexChanged()
        If _suppressListBoxEvent Then Return
        Dim sel As String = 取列表选中项(_styleList)
        If sel Is Nothing Then Return

        _suppressTextBoxEvent = True
        Try
            设置文本框文本(FontDialogTextBoxKind.Style, sel)
        Finally
            _suppressTextBoxEvent = False
        End Try
        UpdateFontFromUI()
    End Sub

    Private Sub SizeTextBox_TextChanged()
        If _suppressTextBoxEvent Then Return
        Dim filter As String = 取文本框文本(FontDialogTextBoxKind.Size).Trim()
        _suppressListBoxEvent = True
        Try
            Dim exactIdx As Integer = 查找列表文本(_sizeList, filter)
            If exactIdx >= 0 Then 设置列表选中(_sizeList, exactIdx, False)
        Finally
            _suppressListBoxEvent = False
        End Try
        UpdateFontFromUI()
    End Sub

    Private Sub SizeList_SelectedIndexChanged()
        If _suppressListBoxEvent Then Return
        Dim sel As String = 取列表选中项(_sizeList)
        If sel Is Nothing Then Return

        _suppressTextBoxEvent = True
        Try
            设置文本框文本(FontDialogTextBoxKind.Size, sel)
        Finally
            _suppressTextBoxEvent = False
        End Try
        UpdateFontFromUI()
    End Sub

    Private Sub UpdateFontFromUI()
        If _fontFamilies Is Nothing Then Return

        Dim familyName As String = 取文本框文本(FontDialogTextBoxKind.FontName).Trim()
        Dim styleName As String = 取文本框文本(FontDialogTextBoxKind.Style).Trim()
        Dim sizeText As String = 取文本框文本(FontDialogTextBoxKind.Size).Trim()

        Dim fontSize As Single = 10.0F
        If Not 解析字号文本(sizeText, fontSize) Then Return
        If fontSize < 1 Then fontSize = 1
        If fontSize > 999 Then fontSize = 999

        Dim styleEntry = 查找字形项(styleName)
        Dim styleTarget = If(styleEntry IsNot Nothing, 创建字形目标(styleEntry), 创建字形目标(解析字形名称(styleName)))

        Try
            Dim family As FontFamily = Nothing
            Try
                family = _fontFamilies.FirstOrDefault(Function(f) String.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase))
            Catch
            End Try

            If family IsNot Nothing Then
                Dim newFont = 创建Gdi字体(family, familyName, fontSize, styleTarget, _underlineChecked, _strikeoutChecked)
                If newFont IsNot Nothing Then 设置选中字体核心(newFont, False)
            End If
        Catch
        End Try

        UpdatePreview()
    End Sub

    Private Sub UpdatePreview()
        _d2dLayoutDirty = True
        Invalidate()
    End Sub

    Private Sub 设置选中字体核心(value As Font, applyToUi As Boolean)
        _selectedFont = If(value, New Font("Microsoft YaHei UI", 10.0F))
        If applyToUi AndAlso _d2dInitialized Then
            应用字体到界面(_selectedFont)
        Else
            _d2dLayoutDirty = True
            If IsHandleCreated Then Invalidate()
        End If
    End Sub

#End Region

#Region "D2D 文本框"

    Private Sub 创建D2D文本框()
        If _d2dTextBoxes.Count > 0 Then Return
        添加虚拟文本框(FontDialogTextBoxKind.FontName)
        添加虚拟文本框(FontDialogTextBoxKind.Style)
        添加虚拟文本框(FontDialogTextBoxKind.Size)
    End Sub

    Private Sub 添加虚拟文本框(kind As FontDialogTextBoxKind)
        Dim box As New VirtualTextBox With {.Kind = kind}
        box.Renderer = New SingleLineTextBoxRenderer(Me) With {
            .BorderSize = 0,
            .ForeColor = ForeColor,
            .WaterTextForeColor = Color.Gray,
            .SelectionColor = D2DElementBackColor,
            .CaretColor = Color.FromArgb(220, 220, 220),
            .TextAreaProvider = Function() box.TextArea,
            .DpiScaleProvider = Function() 取D2D缩放(),
            .InvalidateAction = Sub() 刷新虚拟文本框(box),
            .FocusProvider = Function() ReferenceEquals(_d2dActiveTextBox, box) AndAlso Focused
        }
        If kind = FontDialogTextBoxKind.Size Then
            box.Renderer.TextFilter = AddressOf 过滤字号文本
            box.Renderer.CandidateValidator = AddressOf 是否可能为字号文本
        End If
        AddHandler box.Renderer.TextChanged, Sub(sender, e) 文本框文本变化D2D(box)
        _d2dTextBoxes(kind) = box
        _d2dTextBoxOrder.Add(box)
    End Sub

    Private Sub 文本框文本变化D2D(box As VirtualTextBox)
        If box Is Nothing Then Return
        Select Case box.Kind
            Case FontDialogTextBoxKind.FontName
                FontTextBox_TextChanged()
            Case FontDialogTextBoxKind.Style
                StyleTextBox_TextChanged()
            Case FontDialogTextBoxKind.Size
                SizeTextBox_TextChanged()
        End Select
    End Sub

    Private Sub 刷新虚拟文本框(box As VirtualTextBox)
        If box Is Nothing Then
            Invalidate()
            Return
        End If
        Dim r = Rectangle.Ceiling(box.Bounds)
        r.Inflate(3, 3)
        Invalidate(Rectangle.Intersect(ClientRectangle, r), False)
    End Sub

    Private Sub 设置文本框文本(kind As FontDialogTextBoxKind, text As String)
        If Not _d2dTextBoxes.ContainsKey(kind) Then Return
        _d2dTextBoxes(kind).Renderer.SetText(text, -1, False, False)
    End Sub

    Private Function 取文本框文本(kind As FontDialogTextBoxKind) As String
        If Not _d2dTextBoxes.ContainsKey(kind) Then Return String.Empty
        Return _d2dTextBoxes(kind).Renderer.Text
    End Function

#End Region

#Region "D2D 绘制"



    Private Sub 绘制D2D图形层_GPU(context As D3D_PaintContext)
        Dim l = _d2dLayout
        If l Is Nothing Then Return

        For Each box In _d2dTextBoxOrder
            MessageDialogRendering.FillRoundedRectangle(context, box.Bounds, D2DElementBackColor, l.Radius)
            Dim border = If(ReferenceEquals(_d2dActiveTextBox, box), D2DElementBorderColor, Color.Transparent)
            MessageDialogRendering.DrawRoundedRectangle(context, box.Bounds, border, 1.0F * 取D2D缩放(), l.Radius)
        Next

        For Each list In 所有列表()
            绘制列表图形_GPU(context, list)
        Next

        绘制复选框_GPU(context, l.StrikeoutSwitch, _strikeoutChecked)
        绘制复选框_GPU(context, l.UnderlineSwitch, _underlineChecked)
        绘制按钮_GPU(context, l.ButtonOK, FontDialogButtonKind.OK)
        绘制按钮_GPU(context, l.ButtonCancel, FontDialogButtonKind.Cancel)
    End Sub

    Private Sub 绘制D2D文字层_GPU(context As D3D_PaintContext)
        Dim l = _d2dLayout
        If l Is Nothing Then Return
        Dim s = 取D2D缩放()
        Dim flagsTitle As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        Dim flagsMiddleLeft As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        Dim flagsCenter As TextFormatFlags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine

        MessageDialogRendering.DrawText(context, 取界面文本("FontTitle", "字体"), Font, l.FontTitle, ForeColor, flagsTitle, s)
        MessageDialogRendering.DrawText(context, 取界面文本("StyleTitle", "字形"), Font, l.StyleTitle, ForeColor, flagsTitle, s)
        MessageDialogRendering.DrawText(context, 取界面文本("SizeTitle", "字号"), Font, l.SizeTitle, ForeColor, flagsTitle, s)
        MessageDialogRendering.DrawText(context, 取界面文本("EffectsTitle", "效果"), Font, l.EffectsTitle, ForeColor, flagsTitle, s)
        MessageDialogRendering.DrawText(context, 取界面文本("PreviewTitle", "预览"), Font, l.PreviewTitle, ForeColor, flagsTitle, s)

        _d2dTextBoxes(FontDialogTextBoxKind.FontName).Renderer.WaterText = 取界面文本("FontWatermark", "选择字体")
        _d2dTextBoxes(FontDialogTextBoxKind.Style).Renderer.WaterText = 取界面文本("StyleWatermark", "选择字形")
        _d2dTextBoxes(FontDialogTextBoxKind.Size).Renderer.WaterText = 取界面文本("SizeWatermark", "选择字号")
        For Each box In _d2dTextBoxOrder
            box.Renderer.ForeColor = ForeColor
            box.Renderer.SelectionColor = D2DElementBackColor
            box.Renderer.DrawGpu(context)
        Next

        For Each list In 所有列表()
            绘制列表文字_GPU(context, list)
        Next

        MessageDialogRendering.DrawText(context, 取界面文本("Strikeout", "删除线"), Font, l.StrikeoutText, ForeColor, flagsMiddleLeft, s)
        MessageDialogRendering.DrawText(context, 取界面文本("Underline", "下划线"), Font, l.UnderlineText, ForeColor, flagsMiddleLeft, s)

        绘制预览文本_GPU(context)
        MessageDialogRendering.DrawText(context, 取界面文本("OK", "确定"), Font, l.ButtonOK, ForeColor, flagsCenter, s)
        MessageDialogRendering.DrawText(context, 取界面文本("Cancel", "取消"), Font, l.ButtonCancel, ForeColor, flagsCenter, s)
    End Sub

    Private Sub 绘制列表图形_GPU(context As D3D_PaintContext, list As VirtualList)
        MessageDialogRendering.FillRoundedRectangle(context, list.Bounds, D2DElementBackColor, _d2dLayout.Radius)
        Dim clip = list.Viewport
        If clip.Width > 0 AndAlso clip.Height > 0 Then
            Using context.PushClip(clip)
                Dim firstIndex = list.ScrollIndex
                Dim drawCount = 可绘制列表项数量(list)
                For row = 0 To drawCount - 1
                    Dim idx = firstIndex + row
                    If idx < 0 OrElse idx >= list.Items.Count Then Exit For
                    Dim itemRect = 取列表项矩形(list, row)
                    If idx = list.SelectedIndex Then
                        MessageDialogRendering.FillRoundedRectangle(context, itemRect, D2DElementPressedBackColor, 6.0F * 取D2D缩放())
                    ElseIf idx = list.HoverIndex Then
                        MessageDialogRendering.FillRoundedRectangle(context, itemRect, 取列表项悬停颜色(), 6.0F * 取D2D缩放())
                    End If
                Next
            End Using
        End If
        绘制列表滚动条_GPU(context, list)
    End Sub

    Private Sub 绘制列表文字_GPU(context As D3D_PaintContext, list As VirtualList)
        Dim clip = list.Viewport
        If clip.Width <= 0 OrElse clip.Height <= 0 Then Return
        Dim s = 取D2D缩放()
        Dim flags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        Using context.PushClip(clip)
            Dim firstIndex = list.ScrollIndex
            Dim drawCount = 可绘制列表项数量(list)
            For row = 0 To drawCount - 1
                Dim idx = firstIndex + row
                If idx < 0 OrElse idx >= list.Items.Count Then Exit For
                Dim itemRect = 取列表项矩形(list, row)
                Dim textX As Single = itemRect.X + _d2dLayout.ListItemPaddingLeft
                Dim rightEdge As Single = itemRect.Right
                If 需要列表滚动条(list) Then rightEdge = Math.Min(rightEdge, list.ScrollBarTrack.Left)
                Dim textRect As New RectangleF(textX,
                                                itemRect.Y,
                                                Math.Max(1.0F, rightEdge - textX - _d2dLayout.ListItemPaddingLeft),
                                                itemRect.Height)
                Dim itemFont As Font = Nothing
                Try
                    Select Case list.Kind
                        Case FontDialogListKind.FontName
                            itemFont = 创建字体名称列表项字体(list.Items(idx))
                            MessageDialogRendering.DrawText(context, list.Items(idx), If(itemFont, Font), textRect, ForeColor, flags, s)
                        Case FontDialogListKind.Style
                            Dim familyName = 取文本框文本(FontDialogTextBoxKind.FontName).Trim()
                            Dim entry = If(idx >= 0 AndAlso idx < _styleEntries.Count, _styleEntries(idx), Nothing)
                            itemFont = 创建字形列表项字体(familyName, entry)
                            MessageDialogRendering.DrawText(context, list.Items(idx), If(itemFont, Font), textRect, ForeColor, flags, s)
                        Case Else
                            MessageDialogRendering.DrawText(context, list.Items(idx), Font, textRect, ForeColor, flags, s)
                    End Select
                Finally
                    itemFont?.Dispose()
                End Try
            Next
        End Using
    End Sub

    Private Sub 绘制列表滚动条_GPU(context As D3D_PaintContext, list As VirtualList)
        If Not 需要列表滚动条(list) Then Return
        Dim track = list.ScrollBarTrack
        Dim thumb = 计算列表滚动滑块(list)
        If track.Width <= 0 OrElse track.Height <= 0 OrElse thumb.Width <= 0 OrElse thumb.Height <= 0 Then Return
        Dim radius As Single = Math.Max(1.0F, track.Width / 2.0F)
        MessageDialogRendering.FillRoundedRectangle(context, track, Color.FromArgb(30, 220, 220, 220), radius)
        MessageDialogRendering.FillRoundedRectangle(context, thumb, Color.FromArgb(120, 220, 220, 220), radius)
    End Sub

    Private Sub 绘制复选框_GPU(context As D3D_PaintContext, rect As RectangleF, checked As Boolean)
        Dim s As Single = 取D2D缩放()
        Dim fill As Color = If(checked, Color.FromArgb(0, 120, 215), D2DElementBackColor)
        MessageDialogRendering.FillRoundedRectangle(context, rect, fill, 3.0F * s)

        If Not checked Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, Color.White, context.DeviceGeneration)
        Dim points = {
            New Vector2(rect.X + 5.8F * s, rect.Y + 12.6F * s),
            New Vector2(rect.X + 10.0F * s, rect.Y + 16.8F * s),
            New Vector2(rect.X + 18.4F * s, rect.Y + 8.2F * s)
        }
        Dim width = Math.Max(1.5F, 2.1F * s)
        context.DeviceContext.DrawLine(points(0), points(1), brush, width)
        context.DeviceContext.DrawLine(points(1), points(2), brush, width)
    End Sub

    Private Sub 绘制预览文本_GPU(context As D3D_PaintContext)
        Dim texts = {
            取界面文本("PreviewSampleChinese", "敏捷的棕色狐狸跳过了懒惰的狗。"),
            取界面文本("PreviewSampleEnglish", "The quick brown fox jumps over the lazy dog."),
            取界面文本("PreviewSampleGlyphs", "永字八法；横竖撇捺折钩点。"),
            取界面文本("PreviewSampleSymbols", "+-*/~！@#￥%……&&*（）-= —— · <>?:;""'[]{}|\")
        }
        For i = 0 To 3
            绘制预览行_GPU(context, texts(i), _d2dLayout.PreviewLines(i))
        Next
    End Sub

    Private Sub 绘制预览行_GPU(context As D3D_PaintContext, text As String, rect As RectangleF)
        If String.IsNullOrEmpty(text) OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim f = SelectedFont
        Dim s = 取D2D缩放()
        Dim sizePx As Single = Math.Max(1.0F, CSng(f.SizeInPoints * 96.0F / 72.0F * Math.Max(0.01F, s)))
        Dim selectedStyle = 查找字形项(取文本框文本(FontDialogTextBoxKind.Style))
        Dim weight As DW.FontWeight = If(selectedStyle IsNot Nothing, selectedStyle.DWriteWeight, If(f.Bold, DW.FontWeight.Bold, DW.FontWeight.Normal))
        Dim style As DW.FontStyle = If(selectedStyle IsNot Nothing, selectedStyle.DWriteStyle, If(f.Italic, DW.FontStyle.Italic, DW.FontStyle.Normal))
        Dim stretch As DW.FontStretch = If(selectedStyle IsNot Nothing, selectedStyle.DWriteStretch, DW.FontStretch.Normal)
        Using fmt = D3D_RenderCore.DeviceManager.DWriteFactory.CreateTextFormat(f.FontFamily.Name, Nothing, weight, style, stretch, sizePx)
            fmt.TextAlignment = DW.TextAlignment.Leading
            fmt.ParagraphAlignment = DW.ParagraphAlignment.Near
            fmt.WordWrapping = DW.WordWrapping.Wrap
            Using layout = D3D_RenderCore.DeviceManager.DWriteFactory.CreateTextLayout(text, fmt, Math.Max(1.0F, rect.Width), Math.Max(1.0F, rect.Height))
                Dim range As New DW.TextRange(0, text.Length)
                If f.Underline Then layout.SetUnderline(True, range)
                If f.Strikeout Then layout.SetStrikethrough(True, range)
                Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, Color.White, context.DeviceGeneration)
                context.DeviceContext.DrawTextLayout(New Vector2(rect.X, rect.Y), layout, brush, D2D.DrawTextOptions.Clip)
            End Using
        End Using
    End Sub

    Private Sub 绘制按钮_GPU(context As D3D_PaintContext, rect As RectangleF, kind As FontDialogButtonKind)
        Dim fill = D2DElementBackColor
        If _d2dPressedButton = kind Then
            fill = D2DElementPressedBackColor
        ElseIf _d2dHoverButton = kind Then
            fill = D2DElementHoverBackColor
        End If
        MessageDialogRendering.FillRoundedRectangle(context, rect, fill, _d2dLayout.Radius)
    End Sub

    Private Function 取列表项悬停颜色() As Color
        Return Color.FromArgb(Math.Max(0, D2DElementHoverBackColor.A \ 2),
                              D2DElementHoverBackColor.R,
                              D2DElementHoverBackColor.G,
                              D2DElementHoverBackColor.B)
    End Function

#End Region

#Region "鼠标和键盘"

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        初始化D2D字体对话框()
        确保D2D布局()
        Focus()

        Dim tb = 命中文本框(e.Location)
        If tb IsNot Nothing Then
            设置活动文本框(tb)
            tb.Renderer.BeginMouseSelection(e.X)
            _d2dMouseTextBox = tb
            _d2dCapturePart = FontDialogHitPart.TextBox
            Capture = True
            Return
        End If

        If e.Button = MouseButtons.Left Then
            Dim scrollList = 命中列表滚动条(e.Location)
            If scrollList IsNot Nothing Then
                设置活动文本框(Nothing)
                Dim thumb = 计算列表滚动滑块(scrollList)
                If thumb.Contains(e.Location) Then
                    _d2dDragList = scrollList
                    _d2dListScrollDragOffset = e.Y - thumb.Y
                    _d2dCapturePart = FontDialogHitPart.ListScrollBar
                    Capture = True
                Else
                    列表轨道点击(scrollList, e.Location)
                End If
                Invalidate()
                Return
            End If
        End If

        Dim hitList = 命中列表项所属列表(e.Location)
        If hitList IsNot Nothing Then
            Dim idx = 命中列表项(hitList, e.Location)
            If idx >= 0 Then
                设置活动文本框(Nothing)
                设置列表选中(hitList, idx, True)
                Return
            End If
        End If

        Dim sw = 命中开关(e.Location)
        If sw <> FontDialogSwitchKind.None AndAlso e.Button = MouseButtons.Left Then
            设置活动文本框(Nothing)
            _d2dPressedSwitch = sw
            _d2dCapturePart = FontDialogHitPart.Switch
            Capture = True
            Invalidate()
            Return
        End If

        Dim btn = 命中按钮(e.Location)
        If btn <> FontDialogButtonKind.None AndAlso e.Button = MouseButtons.Left Then
            设置活动文本框(Nothing)
            _d2dPressedButton = btn
            _d2dCapturePart = FontDialogHitPart.Button
            Capture = True
            Invalidate()
            Return
        End If

        设置活动文本框(Nothing)
        MyBase.OnMouseDown(e)
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        确保D2D布局()
        Select Case _d2dCapturePart
            Case FontDialogHitPart.TextBox
                If _d2dMouseTextBox IsNot Nothing Then _d2dMouseTextBox.Renderer.UpdateMouseSelection(e.X)
            Case FontDialogHitPart.ListScrollBar
                If _d2dDragList IsNot Nothing AndAlso e.Button = MouseButtons.Left Then
                    滚动列表到滑块位置(_d2dDragList, e.Y - _d2dListScrollDragOffset)
                End If
            Case Else
                更新D2D悬停状态(e.Location)
        End Select
        MyBase.OnMouseMove(e)
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        Dim pressedButton = _d2dPressedButton
        Dim releaseButton = 命中按钮(e.Location)
        Dim pressedSwitch = _d2dPressedSwitch
        Dim releaseSwitch = 命中开关(e.Location)

        _d2dCapturePart = FontDialogHitPart.None
        _d2dMouseTextBox = Nothing
        _d2dDragList = Nothing
        _d2dPressedButton = FontDialogButtonKind.None
        _d2dPressedSwitch = FontDialogSwitchKind.None
        Capture = False

        If pressedButton <> FontDialogButtonKind.None AndAlso pressedButton = releaseButton Then 触发D2D按钮(pressedButton)
        If pressedSwitch <> FontDialogSwitchKind.None AndAlso pressedSwitch = releaseSwitch Then 切换D2D开关(pressedSwitch)
        更新D2D悬停状态(e.Location)
        Invalidate()
        MyBase.OnMouseUp(e)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        _d2dHoverButton = FontDialogButtonKind.None
        _d2dHoverSwitch = FontDialogSwitchKind.None
        For Each list In 所有列表()
            list.HoverIndex = -1
        Next
        Invalidate()
        MyBase.OnMouseLeave(e)
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        确保D2D布局()
        Dim list = 命中列表(e.Location)
        If list IsNot Nothing Then
            list.ScrollIndex += -Math.Sign(e.Delta) * 3
            限制列表滚动(list)
            Invalidate()
            Return
        End If
        MyBase.OnMouseWheel(e)
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
    End Sub

    Protected Overrides Sub WndProc(ByRef m As Message)
        If 处理D2D窗口消息(m) Then Return
        MyBase.WndProc(m)
    End Sub

    Private Function 处理D2D快捷键(ByRef msg As Message, keyData As Keys) As Boolean
        If _d2dActiveTextBox Is Nothing Then
            If keyData = Keys.Enter Then
                DialogResult = DialogResult.OK
                Close()
                Return True
            End If
            If keyData = Keys.Escape Then
                DialogResult = DialogResult.Cancel
                Close()
                Return True
            End If
            Return False
        End If

        Dim r = _d2dActiveTextBox.Renderer
        Select Case keyData
            Case Keys.Left
                r.MoveCaret(-1, False) : Return True
            Case Keys.Right
                r.MoveCaret(1, False) : Return True
            Case Keys.Shift Or Keys.Left
                r.MoveCaret(-1, True) : Return True
            Case Keys.Shift Or Keys.Right
                r.MoveCaret(1, True) : Return True
            Case Keys.Control Or Keys.Left
                r.MoveCaretWordLeft(False) : Return True
            Case Keys.Control Or Keys.Right
                r.MoveCaretWordRight(False) : Return True
            Case Keys.Control Or Keys.Shift Or Keys.Left
                r.MoveCaretWordLeft(True) : Return True
            Case Keys.Control Or Keys.Shift Or Keys.Right
                r.MoveCaretWordRight(True) : Return True
            Case Keys.Home
                r.MoveCaretHome(False) : Return True
            Case Keys.End
                r.MoveCaretEnd(False) : Return True
            Case Keys.Shift Or Keys.Home
                r.MoveCaretHome(True) : Return True
            Case Keys.Shift Or Keys.End
                r.MoveCaretEnd(True) : Return True
            Case Keys.Delete
                r.HandleDelete() : Return True
            Case Keys.Back
                r.HandleBackspace() : Return True
            Case Keys.Control Or Keys.A
                r.SelectAll() : Return True
            Case Keys.Control Or Keys.C
                r.CopySelection() : Return True
            Case Keys.Control Or Keys.X
                r.CutSelection() : Return True
            Case Keys.Control Or Keys.V
                r.PasteText() : Return True
            Case Keys.Tab
                聚焦下一个文本框(False) : Return True
            Case Keys.Shift Or Keys.Tab
                聚焦下一个文本框(True) : Return True
            Case Keys.Enter
                DialogResult = DialogResult.OK : Close() : Return True
            Case Keys.Escape
                设置活动文本框(Nothing) : Return True
        End Select
        Return False
    End Function

    Private Function 处理D2D窗口消息(ByRef m As Message) As Boolean
        Select Case m.Msg
            Case WM_ENTERSIZEMOVE_D2D
                _d2dInSizeMove = True
                Return False
            Case WM_EXITSIZEMOVE_D2D
                _d2dInSizeMove = False
                _d2dLayoutDirty = True
                确保D2D布局()
                Invalidate()
                Return False
            Case WM_GETDLGCODE_D2D
                m.Result = New IntPtr(DLGC_WANTCHARS_D2D Or DLGC_WANTALLKEYS_D2D)
                Return True
            Case WM_IME_STARTCOMPOSITION_D2D
                _d2dImeComposing = True
                更新D2D输入法窗口()
                Return False
            Case WM_IME_ENDCOMPOSITION_D2D
                _d2dImeComposing = False
                Return False
            Case WM_IME_COMPOSITION_D2D
                Dim lp As Integer = m.LParam.ToInt32()
                更新D2D输入法窗口()
                If (lp And GCS_RESULTSTR_D2D) <> 0 AndAlso _d2dActiveTextBox IsNot Nothing Then
                    Dim result As String = ImeHelper.GetResultString(Handle)
                    If result IsNot Nothing Then _d2dActiveTextBox.Renderer.InsertText(result)
                    Return True
                End If
            Case WM_CHAR_D2D
                If Not _d2dImeComposing Then
                    处理D2D字符输入(m.WParam.ToInt32())
                    Return True
                End If
        End Select
        Return False
    End Function

    Private Sub 处理D2D字符输入(charCode As Integer)
        If _d2dActiveTextBox Is Nothing Then Return
        Dim r = _d2dActiveTextBox.Renderer
        Select Case charCode
            Case 1
                r.SelectAll()
            Case 3
                r.CopySelection()
            Case 22
                r.PasteText()
            Case 24
                r.CutSelection()
            Case 8
                r.HandleBackspace()
            Case Else
                Dim ch As Char = ChrW(charCode)
                If Not Char.IsControl(ch) Then r.InsertText(ch.ToString())
        End Select
        r.ResetCaretBlink()
    End Sub

#End Region

#Region "交互辅助"

    Private Sub 更新D2D悬停状态(p As Point)
        Dim oldButton = _d2dHoverButton
        Dim oldSwitch = _d2dHoverSwitch
        Dim oldFontHover = _fontList.HoverIndex
        Dim oldStyleHover = _styleList.HoverIndex
        Dim oldSizeHover = _sizeList.HoverIndex

        _d2dHoverButton = 命中按钮(p)
        _d2dHoverSwitch = 命中开关(p)
        For Each list In 所有列表()
            list.HoverIndex = 命中列表项(list, p)
        Next

        If 命中文本框(p) IsNot Nothing Then
            Cursor = Cursors.IBeam
        ElseIf _d2dHoverButton <> FontDialogButtonKind.None OrElse
               _d2dHoverSwitch <> FontDialogSwitchKind.None OrElse
               命中列表项所属列表(p) IsNot Nothing OrElse
               命中列表滚动条(p) IsNot Nothing Then
            Cursor = Cursors.Hand
        Else
            Cursor = Cursors.Default
        End If

        If oldButton <> _d2dHoverButton OrElse oldSwitch <> _d2dHoverSwitch OrElse
           oldFontHover <> _fontList.HoverIndex OrElse oldStyleHover <> _styleList.HoverIndex OrElse oldSizeHover <> _sizeList.HoverIndex Then
            Invalidate()
        End If
    End Sub

    Private Function 命中文本框(p As Point) As VirtualTextBox
        For Each box In _d2dTextBoxOrder
            If box.Bounds.Contains(p) Then Return box
        Next
        Return Nothing
    End Function

    Private Function 命中按钮(p As Point) As FontDialogButtonKind
        If _d2dLayout Is Nothing Then Return FontDialogButtonKind.None
        If _d2dLayout.ButtonOK.Contains(p) Then Return FontDialogButtonKind.OK
        If _d2dLayout.ButtonCancel.Contains(p) Then Return FontDialogButtonKind.Cancel
        Return FontDialogButtonKind.None
    End Function

    Private Function 命中开关(p As Point) As FontDialogSwitchKind
        If _d2dLayout Is Nothing Then Return FontDialogSwitchKind.None
        If _d2dLayout.StrikeoutSwitch.Contains(p) Then Return FontDialogSwitchKind.Strikeout
        If _d2dLayout.UnderlineSwitch.Contains(p) Then Return FontDialogSwitchKind.Underline
        Return FontDialogSwitchKind.None
    End Function

    Private Function 命中列表(p As Point) As VirtualList
        For Each list In 所有列表()
            If list.Bounds.Contains(p) Then Return list
        Next
        Return Nothing
    End Function

    Private Function 命中列表项所属列表(p As Point) As VirtualList
        For Each list In 所有列表()
            If 命中列表项(list, p) >= 0 Then Return list
        Next
        Return Nothing
    End Function

    Private Function 命中列表项(list As VirtualList, p As Point) As Integer
        If list Is Nothing OrElse Not list.Viewport.Contains(p) Then Return -1
        If 命中列表滚动条(list, p) Then Return -1
        Dim row = CInt(Math.Floor((p.Y - list.Viewport.Y) / _d2dLayout.ItemHeight))
        If row < 0 Then Return -1
        Dim idx = list.ScrollIndex + row
        If idx < 0 OrElse idx >= list.Items.Count Then Return -1
        Return idx
    End Function

    Private Function 命中列表滚动条(p As Point) As VirtualList
        For Each list In 所有列表()
            If 命中列表滚动条(list, p) Then Return list
        Next
        Return Nothing
    End Function

    Private Function 命中列表滚动条(list As VirtualList, p As Point) As Boolean
        If list Is Nothing OrElse Not 需要列表滚动条(list) Then Return False
        Dim hit = list.ScrollBarTrack
        hit.Inflate(2.0F * 取D2D缩放(), 0)
        Return hit.Contains(p)
    End Function

    Private Sub 设置活动文本框(box As VirtualTextBox)
        If ReferenceEquals(_d2dActiveTextBox, box) Then Return
        If _d2dActiveTextBox IsNot Nothing Then _d2dActiveTextBox.Renderer.StopCaretBlink()
        _d2dActiveTextBox = box
        If _d2dActiveTextBox IsNot Nothing Then
            _d2dActiveTextBox.Renderer.StartCaretBlink()
            更新D2D输入法窗口()
        End If
        Invalidate()
    End Sub

    Private Sub 聚焦下一个文本框(reverse As Boolean)
        If _d2dTextBoxOrder.Count = 0 Then Return
        Dim idx = If(_d2dActiveTextBox Is Nothing, -1, _d2dTextBoxOrder.IndexOf(_d2dActiveTextBox))
        If reverse Then
            idx -= 1
            If idx < 0 Then idx = _d2dTextBoxOrder.Count - 1
        Else
            idx += 1
            If idx >= _d2dTextBoxOrder.Count Then idx = 0
        End If
        设置活动文本框(_d2dTextBoxOrder(idx))
    End Sub

    Private Sub 触发D2D按钮(kind As FontDialogButtonKind)
        Select Case kind
            Case FontDialogButtonKind.OK
                DialogResult = DialogResult.OK
                Close()
            Case FontDialogButtonKind.Cancel
                DialogResult = DialogResult.Cancel
                Close()
        End Select
    End Sub

    Private Sub 切换D2D开关(kind As FontDialogSwitchKind)
        Select Case kind
            Case FontDialogSwitchKind.Strikeout
                _strikeoutChecked = Not _strikeoutChecked
            Case FontDialogSwitchKind.Underline
                _underlineChecked = Not _underlineChecked
        End Select
        UpdateFontFromUI()
        Invalidate()
    End Sub

    Private Sub 更新D2D输入法窗口()
        If Not IsHandleCreated OrElse _d2dActiveTextBox Is Nothing Then Return
        Dim p = _d2dActiveTextBox.Renderer.GetCaretImeLocation()
        ImeHelper.SetCompositionPosition(Handle, p.X, p.Y)
    End Sub

#End Region

#Region "列表辅助"

    Private Iterator Function 所有列表() As IEnumerable(Of VirtualList)
        Yield _fontList
        Yield _styleList
        Yield _sizeList
    End Function

    Private Sub 设置列表项目(list As VirtualList, items As IEnumerable(Of String))
        list.Items.Clear()
        If items IsNot Nothing Then
            For Each item In items
                list.Items.Add(If(item, String.Empty))
            Next
        End If
        list.SelectedIndex = -1
        list.HoverIndex = -1
        list.ScrollIndex = 0
        限制列表滚动(list)
    End Sub

    Private Sub 设置列表选中(list As VirtualList, index As Integer, raiseChanged As Boolean)
        If list Is Nothing Then Return
        If index < -1 OrElse index >= list.Items.Count Then index = -1
        If list.SelectedIndex = index Then
            确保列表项可见(list, index)
            Invalidate()
            Return
        End If
        list.SelectedIndex = index
        确保列表项可见(list, index)
        Invalidate()
        If raiseChanged Then
            Select Case list.Kind
                Case FontDialogListKind.FontName
                    FontList_SelectedIndexChanged()
                Case FontDialogListKind.Style
                    StyleList_SelectedIndexChanged()
                Case FontDialogListKind.Size
                    SizeList_SelectedIndexChanged()
            End Select
        End If
    End Sub

    Private Sub 选择列表文本(list As VirtualList, text As String)
        Dim idx = 查找列表文本(list, text)
        If idx >= 0 Then 设置列表选中(list, idx, False)
    End Sub

    Private Function 查找列表文本(list As VirtualList, text As String) As Integer
        If list Is Nothing Then Return -1
        For i = 0 To list.Items.Count - 1
            If String.Equals(list.Items(i), text, StringComparison.OrdinalIgnoreCase) Then Return i
        Next
        Return -1
    End Function

    Private Function 取列表选中项(list As VirtualList) As String
        If list Is Nothing Then Return Nothing
        If list.SelectedIndex >= 0 AndAlso list.SelectedIndex < list.Items.Count Then Return list.Items(list.SelectedIndex)
        Return Nothing
    End Function

    Private Function 查找字形目标索引(familyName As String, target As FontStyleTarget) As Integer
        If target Is Nothing Then Return -1

        Dim matchedTarget = 取DWrite匹配字形目标(familyName, target)
        If matchedTarget IsNot Nothing Then
            Dim matchedIdx = 查找DWrite字形索引(matchedTarget)
            If matchedIdx >= 0 Then Return matchedIdx
        End If

        Dim bestIndex As Integer = -1
        Dim bestScore As Integer = Integer.MaxValue
        For i = 0 To _styleEntries.Count - 1
            Dim entry = _styleEntries(i)
            Dim score As Integer = If(提取基础字形(entry.BaseStyle) = target.BaseStyle, 0, 10000)
            If entry.IsDWriteFace Then
                score += Math.Abs(CInt(entry.DWriteWeight) - CInt(target.DWriteWeight))
                score += Math.Abs(CInt(entry.DWriteStretch) - CInt(target.DWriteStretch)) * 10

                If entry.DWriteStyle <> target.DWriteStyle Then
                    If target.DWriteStyle = DW.FontStyle.Italic AndAlso entry.DWriteStyle = DW.FontStyle.Oblique Then
                        score += 25
                    Else
                        score += 1000
                    End If
                End If
            End If

            If score < bestScore Then
                bestScore = score
                bestIndex = i
            End If
        Next
        Return bestIndex
    End Function

    Private Function 查找DWrite字形索引(target As FontStyleTarget) As Integer
        If target Is Nothing Then Return -1
        For i = 0 To _styleEntries.Count - 1
            Dim entry = _styleEntries(i)
            If entry.IsDWriteFace AndAlso
               CInt(entry.DWriteWeight) = CInt(target.DWriteWeight) AndAlso
               entry.DWriteStyle = target.DWriteStyle AndAlso
               entry.DWriteStretch = target.DWriteStretch Then
                Return i
            End If
        Next
        Return -1
    End Function

    Private Function 可见列表项数量(list As VirtualList) As Integer
        If list Is Nothing OrElse _d2dLayout Is Nothing Then Return 0
        Return Math.Max(1, CInt(Math.Floor(list.Viewport.Height / Math.Max(1.0F, _d2dLayout.ItemHeight))))
    End Function

    Private Function 可绘制列表项数量(list As VirtualList) As Integer
        If list Is Nothing OrElse _d2dLayout Is Nothing Then Return 0
        Return Math.Max(1, CInt(Math.Ceiling(list.Viewport.Height / Math.Max(1.0F, _d2dLayout.ItemHeight))) + 1)
    End Function

    Private Sub 限制列表滚动(list As VirtualList)
        If list Is Nothing Then Return
        Dim maxScroll = Math.Max(0, list.Items.Count - 可见列表项数量(list))
        list.ScrollIndex = Math.Clamp(list.ScrollIndex, 0, maxScroll)
    End Sub

    Private Sub 确保列表项可见(list As VirtualList, index As Integer)
        If list Is Nothing OrElse index < 0 Then Return
        Dim visible = 可见列表项数量(list)
        If index < list.ScrollIndex Then list.ScrollIndex = index
        If index >= list.ScrollIndex + visible Then list.ScrollIndex = index - visible + 1
        限制列表滚动(list)
    End Sub

    Private Sub 确保所有列表选中项可见()
        For Each list In 所有列表()
            If list.SelectedIndex >= 0 Then 确保列表项可见(list, list.SelectedIndex)
        Next
    End Sub

    Private Function 需要列表滚动条(list As VirtualList) As Boolean
        If list Is Nothing Then Return False
        Return list.Items.Count > 可见列表项数量(list)
    End Function

    Private Function 取列表项矩形(list As VirtualList, row As Integer) As RectangleF
        Dim rightEdge As Single = list.Viewport.Right
        If 需要列表滚动条(list) Then rightEdge = Math.Min(rightEdge, list.ScrollBarTrack.Left)
        Return New RectangleF(list.Viewport.X,
                              list.Viewport.Y + row * _d2dLayout.ItemHeight,
                              Math.Max(1.0F, rightEdge - list.Viewport.X),
                              _d2dLayout.ItemHeight)
    End Function

    Private Function 计算列表滚动滑块(list As VirtualList) As RectangleF
        If list Is Nothing OrElse Not 需要列表滚动条(list) Then Return RectangleF.Empty
        Dim track = list.ScrollBarTrack
        Dim visible = Math.Max(1, 可见列表项数量(list))
        Dim total = Math.Max(visible, list.Items.Count)
        If track.Width <= 0 OrElse track.Height <= 0 OrElse total <= visible Then Return RectangleF.Empty
        Dim thumbH As Single = Math.Max(20.0F * 取D2D缩放(), track.Height * visible / total)
        thumbH = Math.Min(track.Height, thumbH)
        Dim maxScroll As Single = Math.Max(1.0F, CSng(total - visible))
        Dim usableH As Single = Math.Max(0.0F, track.Height - thumbH)
        Dim y As Single = track.Y + usableH * Math.Clamp(list.ScrollIndex, 0, maxScroll) / maxScroll
        Return New RectangleF(track.X, y, track.Width, thumbH)
    End Function

    Private Sub 列表轨道点击(list As VirtualList, p As Point)
        Dim thumb = 计算列表滚动滑块(list)
        Dim visible = 可见列表项数量(list)
        If p.Y < thumb.Y Then
            list.ScrollIndex -= visible
        Else
            list.ScrollIndex += visible
        End If
        限制列表滚动(list)
    End Sub

    Private Sub 滚动列表到滑块位置(list As VirtualList, thumbTop As Single)
        If list Is Nothing OrElse Not 需要列表滚动条(list) Then Return
        Dim track = list.ScrollBarTrack
        Dim thumb = 计算列表滚动滑块(list)
        Dim visible = Math.Max(1, 可见列表项数量(list))
        Dim maxScroll As Single = Math.Max(0.0F, CSng(list.Items.Count - visible))
        Dim usableH As Single = Math.Max(1.0F, track.Height - thumb.Height)
        Dim ratio As Single = Math.Clamp((thumbTop - track.Y) / usableH, 0.0F, 1.0F)
        list.ScrollIndex = CInt(Math.Round(maxScroll * ratio))
        限制列表滚动(list)
        Invalidate()
    End Sub

#End Region

#Region "文本与字形辅助"

    Private Function 取界面文本(key As String, fallback As String) As String
        If Not String.IsNullOrEmpty(key) Then
            Dim overridden As String = Nothing
            If _d2dTextOverrides.TryGetValue(key, overridden) Then Return overridden
            If D2DTextProvider IsNot Nothing Then
                Dim provided = D2DTextProvider.Invoke(key)
                If provided IsNot Nothing Then Return provided
            End If
        End If
        Return fallback
    End Function

    Private Sub 翻译文本已变化()
        If _d2dInitialized Then
            Text = 取界面文本("WindowTitle", "FontDialog")
            Dim currentTarget = 创建当前字形目标()
            Dim currentSize As Single
            Dim hasCurrentSize = 解析字号文本(取文本框文本(FontDialogTextBoxKind.Size), currentSize)
            填充字形列表(取文本框文本(FontDialogTextBoxKind.FontName), currentTarget)
            重建字号列表()
            If hasCurrentSize Then 选择字号点数(currentSize)
        End If
        _d2dLayoutDirty = True
        Invalidate()
    End Sub

    Private Function 取字形显示文本(baseStyle As Drawing.FontStyle) As String
        Select Case 提取基础字形(baseStyle)
            Case Drawing.FontStyle.Bold Or Drawing.FontStyle.Italic
                Return 取界面文本("StyleBoldItalic", "粗斜体")
            Case Drawing.FontStyle.Bold
                Return 取界面文本("StyleBold", "粗体")
            Case Drawing.FontStyle.Italic
                Return 取界面文本("StyleItalic", "斜体")
            Case Else
                Return 取界面文本("StyleRegular", "常规")
        End Select
    End Function

    Private Function 解析字形名称(name As String) As Drawing.FontStyle
        Dim entry = 查找字形项(name)
        If entry IsNot Nothing Then Return entry.BaseStyle

        Dim n = If(name, String.Empty).Trim()
        If 文本等于字形(n, "StyleBoldItalic", "粗斜体") Then Return Drawing.FontStyle.Bold Or Drawing.FontStyle.Italic
        If 文本等于字形(n, "StyleBold", "粗体") Then Return Drawing.FontStyle.Bold
        If 文本等于字形(n, "StyleItalic", "斜体") Then Return Drawing.FontStyle.Italic
        Return Drawing.FontStyle.Regular
    End Function

    Private Function 文本等于字形(text As String, key As String, fallback As String) As Boolean
        Return String.Equals(text, fallback, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(text, 取界面文本(key, fallback), StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function 取字形项显示文本(entry As FontStyleEntry) As String
        If entry Is Nothing Then Return String.Empty
        If Not String.IsNullOrEmpty(entry.Key) Then Return 取界面文本(entry.Key, entry.Fallback)
        Return If(entry.Fallback, String.Empty)
    End Function

    Private Function 查找字形项(text As String) As FontStyleEntry
        Dim n = If(text, String.Empty).Trim()
        If n.Length = 0 Then Return Nothing
        For Each entry In _styleEntries
            If String.Equals(n, entry.DisplayName, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(n, entry.Fallback, StringComparison.OrdinalIgnoreCase) OrElse
               (Not String.IsNullOrEmpty(entry.Key) AndAlso String.Equals(n, 取界面文本(entry.Key, entry.Fallback), StringComparison.OrdinalIgnoreCase)) Then
                Return entry
            End If
        Next
        Return Nothing
    End Function

    Private Function 创建当前字形目标() As FontStyleTarget
        Dim selected = If(_styleList.SelectedIndex >= 0 AndAlso _styleList.SelectedIndex < _styleEntries.Count,
                          _styleEntries(_styleList.SelectedIndex),
                          查找字形项(取文本框文本(FontDialogTextBoxKind.Style)))
        If selected IsNot Nothing Then Return 创建字形目标(selected)
        Return 创建字形目标(解析字形名称(取文本框文本(FontDialogTextBoxKind.Style)))
    End Function

    Private Function 创建字形目标(entry As FontStyleEntry) As FontStyleTarget
        If entry Is Nothing Then Return 创建字形目标(Drawing.FontStyle.Regular)
        Return New FontStyleTarget With {
            .BaseStyle = 提取基础字形(entry.BaseStyle),
            .DWriteWeight = entry.DWriteWeight,
            .DWriteStyle = entry.DWriteStyle,
            .DWriteStretch = entry.DWriteStretch
        }
    End Function

    Private Function 创建字形目标(style As Drawing.FontStyle) As FontStyleTarget
        Dim baseStyle = 提取基础字形(style)
        Return New FontStyleTarget With {
            .BaseStyle = baseStyle,
            .DWriteWeight = If((baseStyle And Drawing.FontStyle.Bold) = Drawing.FontStyle.Bold, DW.FontWeight.Bold, DW.FontWeight.Normal),
            .DWriteStyle = If((baseStyle And Drawing.FontStyle.Italic) = Drawing.FontStyle.Italic, DW.FontStyle.Italic, DW.FontStyle.Normal),
            .DWriteStretch = DW.FontStretch.Normal
        }
    End Function

    Private Function 创建字形目标(font As Font) As FontStyleTarget
        If font Is Nothing Then Return 创建字形目标(Drawing.FontStyle.Regular)
        Try
            ' GDI+ Font 可能携带比 Bold/Italic 更细的字重；LOGFONT 是这里与系统字体对话框保持一致的依据。
            Dim lf = 创建LogFont(font)
            If lf IsNot Nothing Then Return 创建字形目标(lf, font.Style)
        Catch
        End Try
        Return 创建字形目标(font.Style)
    End Function

    Private Function 创建字形目标(logFont As GdiLogFont, fallbackStyle As Drawing.FontStyle) As FontStyleTarget
        If logFont Is Nothing Then Return 创建字形目标(fallbackStyle)

        Dim weightValue As Integer = logFont.lfWeight
        If weightValue <= 0 Then
            weightValue = If((fallbackStyle And Drawing.FontStyle.Bold) = Drawing.FontStyle.Bold,
                             CInt(DW.FontWeight.Bold),
                             CInt(DW.FontWeight.Normal))
        End If
        weightValue = Math.Clamp(weightValue, 1, 999)

        Dim dwriteStyle = If(logFont.lfItalic <> 0, DW.FontStyle.Italic, DW.FontStyle.Normal)
        Dim weight = CType(weightValue, DW.FontWeight)

        Return New FontStyleTarget With {
            .BaseStyle = DWrite字形转GDI基础字形(weight, dwriteStyle),
            .DWriteWeight = weight,
            .DWriteStyle = dwriteStyle,
            .DWriteStretch = DW.FontStretch.Normal
        }
    End Function

    Private Shared Function 创建LogFont(font As Font) As GdiLogFont
        If font Is Nothing Then Return Nothing
        Dim lf As New GdiLogFont()
        font.ToLogFont(lf)
        Return lf
    End Function

    Private Function 取DWrite匹配字形目标(familyName As String, target As FontStyleTarget) As FontStyleTarget
        If String.IsNullOrWhiteSpace(familyName) OrElse target Is Nothing Then Return Nothing

        Dim collection As DW.IDWriteFontCollection = Nothing
        Dim dwriteFamily As DW.IDWriteFontFamily = Nothing
        Try
            collection = D3D_D2DInterop.GetDWriteFactory().GetSystemFontCollection(False)
            Dim familyIndex As UInteger = 0
            If Not CBool(collection.FindFamilyName(familyName, familyIndex)) Then Return Nothing
            dwriteFamily = collection.GetFontFamily(familyIndex)
            If dwriteFamily Is Nothing Then Return Nothing

            ' 让 DirectWrite 自己按 weight/stretch/style 排序并给出第一匹配，避免按 face 名称或枚举顺序猜测。
            Using matched = dwriteFamily.GetFirstMatchingFont(target.DWriteWeight, target.DWriteStretch, target.DWriteStyle)
                If matched Is Nothing Then Return Nothing
                Return New FontStyleTarget With {
                    .BaseStyle = DWrite字形转GDI基础字形(matched.Weight, matched.Style),
                    .DWriteWeight = matched.Weight,
                    .DWriteStyle = matched.Style,
                    .DWriteStretch = matched.Stretch
                }
            End Using
        Catch
            Return Nothing
        Finally
            If dwriteFamily IsNot Nothing Then Try : dwriteFamily.Dispose() : Catch : End Try
            If collection IsNot Nothing Then Try : collection.Dispose() : Catch : End Try
        End Try
    End Function

    Private Function 创建Gdi字体(family As FontFamily, familyName As String, points As Single,
                                target As FontStyleTarget, underline As Boolean, strikeout As Boolean) As Font
        If family Is Nothing Then Return Nothing
        If target Is Nothing Then target = 创建字形目标(Drawing.FontStyle.Regular)

        Dim gdiBaseStyle = 选择可用Gdi基础字形(family, target.BaseStyle)
        Try
            Using baseFont As New Font(family, points, gdiBaseStyle, GraphicsUnit.Point)
                Dim lf = 创建LogFont(baseFont)
                If lf IsNot Nothing Then
                    ' FromLogFont 会把真实字重、斜体和效果带回 GDI+ Font，SelectedFont 才能对应当前 DirectWrite face。
                    If Not String.IsNullOrWhiteSpace(familyName) Then lf.lfFaceName = familyName
                    lf.lfWeight = Math.Clamp(CInt(target.DWriteWeight), 1, 999)
                    lf.lfItalic = If(target.DWriteStyle = DW.FontStyle.Italic OrElse target.DWriteStyle = DW.FontStyle.Oblique, CByte(1), CByte(0))
                    lf.lfUnderline = If(underline, CByte(1), CByte(0))
                    lf.lfStrikeOut = If(strikeout, CByte(1), CByte(0))
                    Return Font.FromLogFont(lf)
                End If
            End Using
        Catch
        End Try

        Dim fallbackStyle = gdiBaseStyle
        If underline Then fallbackStyle = fallbackStyle Or Drawing.FontStyle.Underline
        If strikeout Then fallbackStyle = fallbackStyle Or Drawing.FontStyle.Strikeout
        Try
            Return New Font(family, points, fallbackStyle, GraphicsUnit.Point)
        Catch
            Return Nothing
        End Try
    End Function

    Private Shared Function 选择可用Gdi基础字形(family As FontFamily, preferred As Drawing.FontStyle) As Drawing.FontStyle
        Dim baseStyle = 提取基础字形(preferred)
        Try
            If family IsNot Nothing AndAlso family.IsStyleAvailable(baseStyle) Then Return baseStyle
            If family IsNot Nothing AndAlso family.IsStyleAvailable(Drawing.FontStyle.Regular) Then Return Drawing.FontStyle.Regular
            If family IsNot Nothing AndAlso family.IsStyleAvailable(Drawing.FontStyle.Bold) Then Return Drawing.FontStyle.Bold
            If family IsNot Nothing AndAlso family.IsStyleAvailable(Drawing.FontStyle.Italic) Then Return Drawing.FontStyle.Italic
            If family IsNot Nothing AndAlso family.IsStyleAvailable(Drawing.FontStyle.Bold Or Drawing.FontStyle.Italic) Then Return Drawing.FontStyle.Bold Or Drawing.FontStyle.Italic
        Catch
        End Try
        Return Drawing.FontStyle.Regular
    End Function

    Private Sub 添加DWrite字形项(familyName As String)
        If String.IsNullOrWhiteSpace(familyName) Then Return

        Dim collection As DW.IDWriteFontCollection = Nothing
        Dim dwriteFamily As DW.IDWriteFontFamily = Nothing
        Try
            collection = D3D_D2DInterop.GetDWriteFactory().GetSystemFontCollection(False)
            Dim familyIndex As UInteger = 0
            If Not CBool(collection.FindFamilyName(familyName, familyIndex)) Then Return
            dwriteFamily = collection.GetFontFamily(familyIndex)
            If dwriteFamily Is Nothing Then Return

            Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim count = CInt(dwriteFamily.FontCount)
            For i = 0 To count - 1
                Using dwriteFont = dwriteFamily.GetFont(CUInt(i))
                    If dwriteFont Is Nothing Then Continue For
                    Dim faceName = 取本地化字符串(dwriteFont.FaceNames)
                    If String.IsNullOrWhiteSpace(faceName) Then faceName = 推断字形名称(dwriteFont.Weight, dwriteFont.Style)
                    If String.IsNullOrWhiteSpace(faceName) OrElse Not seen.Add(faceName) Then Continue For

                    _styleEntries.Add(New FontStyleEntry With {
                        .Fallback = faceName,
                        .DisplayName = faceName,
                        .BaseStyle = DWrite字形转GDI基础字形(dwriteFont.Weight, dwriteFont.Style),
                        .DWriteWeight = dwriteFont.Weight,
                        .DWriteStyle = dwriteFont.Style,
                        .DWriteStretch = dwriteFont.Stretch,
                        .IsDWriteFace = True
                    })
                End Using
            Next
        Catch
        Finally
            If dwriteFamily IsNot Nothing Then Try : dwriteFamily.Dispose() : Catch : End Try
            If collection IsNot Nothing Then Try : collection.Dispose() : Catch : End Try
        End Try
    End Sub

    Private Function 取本地化字符串(strings As DW.IDWriteLocalizedStrings) As String
        If strings Is Nothing Then Return String.Empty
        Try
            Dim index As UInteger = 0
            If Not CBool(strings.FindLocaleName(CultureInfo.CurrentUICulture.Name, index)) AndAlso
               Not CBool(strings.FindLocaleName(CultureInfo.CurrentCulture.Name, index)) AndAlso
               Not CBool(strings.FindLocaleName("zh-cn", index)) AndAlso
               Not CBool(strings.FindLocaleName("en-us", index)) Then
                index = 0
            End If
            Return If(strings.GetString(index), String.Empty)
        Catch
            Return String.Empty
        Finally
            Try : strings.Dispose() : Catch : End Try
        End Try
    End Function

    Private Shared Function DWrite字形转GDI基础字形(weight As DW.FontWeight, style As DW.FontStyle) As Drawing.FontStyle
        Dim result As Drawing.FontStyle = Drawing.FontStyle.Regular
        If CInt(weight) >= CInt(DW.FontWeight.DemiBold) Then result = result Or Drawing.FontStyle.Bold
        If style = DW.FontStyle.Italic OrElse style = DW.FontStyle.Oblique Then result = result Or Drawing.FontStyle.Italic
        Return result
    End Function

    Private Function 推断字形名称(weight As DW.FontWeight, style As DW.FontStyle) As String
        Dim baseStyle = DWrite字形转GDI基础字形(weight, style)
        Return 取字形显示文本(baseStyle)
    End Function

    Private Function 创建字体名称列表项字体(familyName As String) As Font
        If String.IsNullOrWhiteSpace(familyName) OrElse _fontFamilies Is Nothing Then Return Nothing

        Try
            Dim family = _fontFamilies.FirstOrDefault(Function(f) String.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase))
            If family Is Nothing Then Return Nothing

            Dim style As Drawing.FontStyle = Drawing.FontStyle.Regular
            If Not family.IsStyleAvailable(style) Then
                If family.IsStyleAvailable(Drawing.FontStyle.Bold) Then
                    style = Drawing.FontStyle.Bold
                ElseIf family.IsStyleAvailable(Drawing.FontStyle.Italic) Then
                    style = Drawing.FontStyle.Italic
                ElseIf family.IsStyleAvailable(Drawing.FontStyle.Bold Or Drawing.FontStyle.Italic) Then
                    style = Drawing.FontStyle.Bold Or Drawing.FontStyle.Italic
                Else
                    Return Nothing
                End If
            End If

            Return New Font(family, Font.SizeInPoints, style, GraphicsUnit.Point)
        Catch
            Return Nothing
        End Try
    End Function

    Private Function 创建字形列表项字体(familyName As String, entry As FontStyleEntry) As Font
        If String.IsNullOrWhiteSpace(familyName) OrElse _fontFamilies Is Nothing Then Return Nothing

        Try
            Dim family = _fontFamilies.FirstOrDefault(Function(f) String.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase))
            If family Is Nothing Then Return Nothing

            Dim style As Drawing.FontStyle = If(entry IsNot Nothing, entry.BaseStyle, Drawing.FontStyle.Regular)
            If Not family.IsStyleAvailable(style) Then
                If family.IsStyleAvailable(Drawing.FontStyle.Regular) Then
                    style = Drawing.FontStyle.Regular
                ElseIf family.IsStyleAvailable(Drawing.FontStyle.Bold) Then
                    style = Drawing.FontStyle.Bold
                ElseIf family.IsStyleAvailable(Drawing.FontStyle.Italic) Then
                    style = Drawing.FontStyle.Italic
                ElseIf family.IsStyleAvailable(Drawing.FontStyle.Bold Or Drawing.FontStyle.Italic) Then
                    style = Drawing.FontStyle.Bold Or Drawing.FontStyle.Italic
                Else
                    Return Nothing
                End If
            End If

            Return New Font(family, Font.SizeInPoints, style, GraphicsUnit.Point)
        Catch
            Return Nothing
        End Try
    End Function


    Private Function 取字号显示文本(entry As FontSizeEntry) As String
        If entry Is Nothing Then Return String.Empty
        If entry.IsNamedSize Then
            Return 取界面文本(entry.Key, entry.Fallback)
        End If
        Return entry.Fallback
    End Function

    Private Shared Function 格式化字号点数(points As Single) As String
        Return points.ToString("0.##", CultureInfo.InvariantCulture)
    End Function

    Private Sub 选择字号点数(points As Single)
        Dim numericText = 格式化字号点数(points)
        Dim bestIdx As Integer = -1
        For i = 0 To _sizeEntries.Count - 1
            If Math.Abs(_sizeEntries(i).Points - points) < 0.01F AndAlso Not _sizeEntries(i).IsNamedSize Then
                bestIdx = i
                Exit For
            End If
        Next
        If bestIdx >= 0 Then
            设置文本框文本(FontDialogTextBoxKind.Size, _sizeList.Items(bestIdx))
            设置列表选中(_sizeList, bestIdx, False)
        Else
            设置文本框文本(FontDialogTextBoxKind.Size, numericText)
            选择列表文本(_sizeList, numericText)
        End If
    End Sub

    Private Function 解析字号文本(text As String, ByRef points As Single) As Boolean
        Dim n = If(text, String.Empty).Trim()
        If n.Length = 0 Then Return False

        For Each entry In _sizeEntries
            If String.Equals(n, entry.Fallback, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(n, 取字号显示文本(entry), StringComparison.OrdinalIgnoreCase) Then
                points = entry.Points
                Return True
            End If
        Next

        If Single.TryParse(n, NumberStyles.Float, CultureInfo.CurrentCulture, points) OrElse
           Single.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, points) Then
            Return points >= 1.0F AndAlso points <= 999.0F
        End If

        Return False
    End Function

    Private Function 是否可能为字号名称前缀(candidate As String) As Boolean
        Dim n = If(candidate, String.Empty).Trim()
        If n.Length = 0 Then Return True
        For Each entry In _sizeEntries
            Dim fallback = If(entry.Fallback, String.Empty)
            Dim display = 取字号显示文本(entry)
            If fallback.StartsWith(n, StringComparison.OrdinalIgnoreCase) OrElse
               display.StartsWith(n, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
        Next
        Return False
    End Function

    Private Shared Function 提取基础字形(style As Drawing.FontStyle) As Drawing.FontStyle
        Return style And (Drawing.FontStyle.Bold Or Drawing.FontStyle.Italic)
    End Function

    Private Shared Function 过滤字号文本(text As String) As String
        Return If(text, String.Empty)
    End Function

    Private Function 是否可能为字号文本(candidate As String) As Boolean
        If String.IsNullOrEmpty(candidate) Then Return True
        Dim v As Single
        If Single.TryParse(candidate, NumberStyles.Float, CultureInfo.CurrentCulture, v) OrElse
           Single.TryParse(candidate, NumberStyles.Float, CultureInfo.InvariantCulture, v) Then
            Return v >= 1.0F AndAlso v <= 999.0F
        End If
        Return 是否可能为字号名称前缀(candidate)
    End Function

#End Region

#Region "辅助方法"

    Private Function 取D2D缩放() As Single
        Return V3_DpiContext.FromControl(Me).Scale
    End Function

    Private Function 缩放值(value As Integer) As Integer
        Return CInt(Math.Round(value * 取D2D缩放()))
    End Function

    Private Sub RequestV3Render()
        RequestV3Render(New Rectangle(Point.Empty, Me.Size))
    End Sub

    Private Sub RequestV3Render(dirtyRect As Rectangle)
        If IsDisposed Then Return
        V3_InvalidationRouter.RequestRender(Me, dirtyRect)
    End Sub

#End Region

End Class
