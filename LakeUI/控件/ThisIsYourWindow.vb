Imports System.ComponentModel
Imports System.Drawing.Drawing2D
Imports System.Reflection
Imports System.Runtime.InteropServices

''' <summary>
''' 无界面组件，用于完全自定义窗口的标题栏与边框外观，
''' 同时保留 Windows 原生的拖动、调整大小、最大化/最小化及贴靠行为。
''' 单个实例可同时附加到多个窗体，所有窗体共享同一套外观属性。
''' 在窗体的 Load 事件中调用 <see cref="Attach"/> 即可启用。
''' </summary>
<DesignerCategory("Component")>
<DefaultEvent("CaptionPaint")>
Public Class ThisIsYourWindow

#Region "Win32 常量与结构"

    Private Const WM_NCHITTEST As Integer = &H84
    Private Const WM_NCCALCSIZE As Integer = &H83
    Private Const WM_GETMINMAXINFO As Integer = &H24
    Private Const WM_SYSCOMMAND As Integer = &H112
    Private Const WM_SIZE As Integer = &H5
    Private Const WM_ACTIVATE As Integer = &H6
    Private Const WM_NCACTIVATE As Integer = &H86
    Private Const WM_NCPAINT As Integer = &H85
    Private Const WM_MOVE As Integer = &H3

    Private Const SC_MINIMIZE As Integer = &HF020
    Private Const SC_MAXIMIZE As Integer = &HF030
    Private Const SC_RESTORE As Integer = &HF120

    Private Const HTCLIENT As Integer = 1
    Private Const HTCAPTION As Integer = 2
    Private Const HTSYSMENU As Integer = 3
    Private Const HTMINBUTTON As Integer = 8
    Private Const HTMAXBUTTON As Integer = 9
    Private Const HTLEFT As Integer = 10
    Private Const HTRIGHT As Integer = 11
    Private Const HTTOP As Integer = 12
    Private Const HTTOPLEFT As Integer = 13
    Private Const HTTOPRIGHT As Integer = 14
    Private Const HTBOTTOM As Integer = 15
    Private Const HTBOTTOMLEFT As Integer = 16
    Private Const HTBOTTOMRIGHT As Integer = 17
    Private Const HTCLOSE As Integer = 20
    Private Const HTNOWHERE As Integer = 0

    Private Const SWP_FRAMECHANGED As Integer = &H20
    Private Const SWP_NOMOVE As Integer = &H2
    Private Const SWP_NOSIZE As Integer = &H1
    Private Const SWP_NOZORDER As Integer = &H4

    Private Const DWMWA_TRANSITIONS_FORCEDISABLED As Integer = 3
    Private Const DWMWA_WINDOW_CORNER_PREFERENCE As Integer = 33
    Private Const DWMWA_BORDER_COLOR As Integer = 34
    Private Const DWMWA_COLOR_NONE As Integer = &HFFFFFFFE

    Private Enum DWM_WINDOW_CORNER_PREFERENCE
        DWMWCP_DEFAULT = 0
        DWMWCP_DONOTROUND = 1
        DWMWCP_ROUND = 2
        DWMWCP_ROUNDSMALL = 3
    End Enum

    Private Const GWL_STYLE As Integer = -16
    Private Const GWL_EXSTYLE As Integer = -20
    Private Const WS_CAPTION As Integer = &HC00000
    Private Const WS_THICKFRAME As Integer = &H40000
    Private Const WS_MINIMIZEBOX As Integer = &H20000
    Private Const WS_MAXIMIZEBOX As Integer = &H10000
    Private Const WS_SYSMENU As Integer = &H80000
    Private Const WS_EX_LAYERED As Integer = &H80000
    Private Const LWA_ALPHA As Integer = &H2

    <DllImport("user32.dll", EntryPoint:="SetWindowLongPtrW")>
    Private Shared Function SetWindowLongPtr(hWnd As IntPtr, nIndex As Integer, dwNewLong As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll", EntryPoint:="GetWindowLongPtrW")>
    Private Shared Function GetWindowLongPtr(hWnd As IntPtr, nIndex As Integer) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SetWindowPos(hWnd As IntPtr, hWndInsertAfter As IntPtr,
                                         X As Integer, Y As Integer, cx As Integer, cy As Integer,
                                         uFlags As UInteger) As Boolean
    End Function

    <DllImport("dwmapi.dll")>
    Private Shared Function DwmSetWindowAttribute(hwnd As IntPtr, dwAttribute As Integer,
                                                   ByRef pvAttribute As Integer, cbAttribute As Integer) As Integer
    End Function

    <DllImport("dwmapi.dll")>
    Private Shared Function DwmExtendFrameIntoClientArea(hWnd As IntPtr, ByRef pMarInset As MARGINS) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function IsZoomed(hWnd As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SetCapture(hWnd As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function ReleaseCapture() As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SetLayeredWindowAttributes(hWnd As IntPtr, crKey As Integer,
                                                       bAlpha As Byte, dwFlags As Integer) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Private Structure RECT
        Public Left, Top, Right, Bottom As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure MINMAXINFO
        Public ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize As Point
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure MARGINS
        Public Left, Right, Top, Bottom As Integer
    End Structure

#End Region

#Region "每窗体状态"

    ''' <summary>每个被附加窗体的独立运行时状态。</summary>
    Friend Class PerFormState
        Public ReadOnly HostForm As Form
        Public Interceptor As WindowMessageInterceptor
        Public Activated As Boolean = True
        Public HoverHit As Integer = HTNOWHERE
        Public PressedHit As Integer = HTNOWHERE
        Public OriginalPadding As Padding
        Public CachedIconBitmap As Bitmap
        Public CachedIconSource As Icon
        Public CloseRect, MaxRect, MinRect, IconRect As Rectangle
        Public ShadowForm As ShadowWindow
        Public IsInSizeMove As Boolean = False
        Public AnimatingShow As Boolean = False
        Public AnimatingClose As Boolean = False
        Public OriginalOpacity As Double = 1.0
        Public PendingFirstPaintRestore As Boolean = False
        Public Sub New(form As Form)
            HostForm = form
        End Sub
    End Class

    Private ReadOnly _forms As New Dictionary(Of IntPtr, PerFormState)

    Private Function 查找状态(form As Form) As PerFormState
        Dim s As PerFormState = Nothing
        If form IsNot Nothing AndAlso form.IsHandleCreated Then _forms.TryGetValue(form.Handle, s)
        Return s
    End Function

#End Region

#Region "枚举"

    Public Enum ButtonPositionEnum
        Right = 0
        Left = 1
    End Enum

    Public Enum TitleAlignEnum
        Left = 0
        Center = 1
        Right = 2
    End Enum

    Public Enum IconSourceEnum
        None = 0
        FormIcon = 1
        Custom = 2
    End Enum

    Public Enum WindowShowAnimationMode
        None = 0
        DWM = 1
        Win32 = 2
    End Enum

    Public Enum WindowCloseAnimationMode
        None = 0
        DWM = 1
        Win32 = 2
    End Enum

    Public Enum ShadowModeEnum
        None = 0
        DWM = 1
        Layer = 2
    End Enum

#End Region

#Region "通用辅助"

    Private Sub 通知重绘()
        For Each s In _forms.Values
            s.HostForm?.Invalidate()
        Next
    End Sub

    Friend Sub 切换动画样式(hWnd As IntPtr, enable As Boolean)
        Dim style As Long = GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64()
        Dim has As Boolean = (style And WS_CAPTION) = WS_CAPTION
        If enable = has Then Return
        If enable Then style = style Or WS_CAPTION Else style = style And Not CLng(WS_CAPTION)
        SetWindowLongPtr(hWnd, GWL_STYLE, New IntPtr(style))
    End Sub

    Friend Sub 触发激活状态改变(activated As Boolean, form As Form)
        RaiseEvent ActiveChanged(Me, New ActiveChangedEventArgs(activated, form))
    End Sub

    Private Sub 宿主窗口_Paint(sender As Object, e As PaintEventArgs)
        Dim frm = TryCast(sender, Form)
        If frm Is Nothing Then Return
        PaintWindow(e.Graphics, frm)
        Dim s = 查找状态(frm)
        If s IsNot Nothing AndAlso s.PendingFirstPaintRestore Then
            s.PendingFirstPaintRestore = False
            If s.AnimatingShow Then
                开始渐入动画(s)
            Else
                Dim alphaByte As Byte = CByte(Math.Min(255, Math.Max(0, CInt(Math.Round(s.OriginalOpacity * 255)))))
                SetLayeredWindowAttributes(frm.Handle, 0, alphaByte, LWA_ALPHA)
            End If
        End If
    End Sub

    Private Sub 宿主窗口_FormClosed(sender As Object, e As FormClosedEventArgs)
        Dim frm = TryCast(sender, Form)
        If frm IsNot Nothing Then Detach(frm)
    End Sub

    Private Sub 更新窗口内边距(s As PerFormState)
        If s Is Nothing Then Return
        s.HostForm.Padding = New Padding(
            s.OriginalPadding.Left + _边框厚度,
            s.OriginalPadding.Top + _标题栏高度,
            s.OriginalPadding.Right + _边框厚度,
            s.OriginalPadding.Bottom + _边框厚度)
    End Sub

    Friend Sub 开始渐入动画(s As PerFormState)
        If s Is Nothing OrElse Not s.AnimatingShow Then Return
        Dim frm = s.HostForm
        Dim targetAlpha As Integer = CInt(Math.Round(s.OriginalOpacity * 255))
        Dim syncShadow As Boolean = (_阴影模式 = ShadowModeEnum.Layer) AndAlso s.ShadowForm IsNot Nothing
        Dim duration As Integer = _动画持续时间
        Dim t As New Timer() With {.Interval = 15}
        Dim elapsed As Integer = 0
        AddHandler t.Tick, Sub(sender, ev)
                               elapsed += 15
                               Dim ratio As Double = Math.Min(1.0, elapsed / CDbl(duration))
                               If Not s.AnimatingShow OrElse elapsed >= duration OrElse frm.IsDisposed Then
                                   t.Stop() : t.Dispose()
                                   s.AnimatingShow = False
                                   If Not frm.IsDisposed Then
                                       SetLayeredWindowAttributes(frm.Handle, 0, CByte(targetAlpha), LWA_ALPHA)
                                       If syncShadow AndAlso s.ShadowForm IsNot Nothing Then
                                           s.ShadowForm.SetGlobalAlpha(255)
                                       End If
                                       更新阴影(s)
                                   End If
                               Else
                                   Dim alpha As Byte = CByte(CInt(Math.Round(targetAlpha * ratio)))
                                   SetLayeredWindowAttributes(frm.Handle, 0, alpha, LWA_ALPHA)
                                   If syncShadow AndAlso s.ShadowForm IsNot Nothing Then
                                       s.ShadowForm.SetGlobalAlpha(CByte(CInt(Math.Round(255 * ratio))))
                                   End If
                               End If
                           End Sub
        t.Start()
    End Sub

#End Region

#Region "属性 - 边框"

    Private _边框颜色 As Color = Color.FromArgb(60, 60, 60)
    <Category("LakeUI"), Description("窗口边框颜色。"), DefaultValue(GetType(Color), "60,60,60")>
    Public Property BorderColor As Color
        Get
            Return _边框颜色
        End Get
        Set(value As Color)
            _边框颜色 = value : 通知重绘()
        End Set
    End Property

    Private _边框失焦颜色 As Color = Color.FromArgb(40, 40, 40)
    <Category("LakeUI"), Description("窗口失去焦点时的边框颜色。"), DefaultValue(GetType(Color), "40,40,40")>
    Public Property BorderInactiveColor As Color
        Get
            Return _边框失焦颜色
        End Get
        Set(value As Color)
            _边框失焦颜色 = value : 通知重绘()
        End Set
    End Property

    Private _边框厚度 As Integer = 1
    <Category("LakeUI"), Description("窗口边框的绘制厚度（逻辑像素）。0 = 不绘制边框。"), DefaultValue(1)>
    Public Property BorderSize As Integer
        Get
            Return _边框厚度
        End Get
        Set(value As Integer)
            _边框厚度 = Math.Max(0, value)
            For Each s In _forms.Values : 更新窗口内边距(s) : Next
            通知重绘()
        End Set
    End Property

#End Region

#Region "属性 - 标题栏"

    Private _标题栏高度 As Integer = 32
    <Category("LakeUI"), Description("标题栏区域的高度（逻辑像素）。"), DefaultValue(32)>
    Public Property CaptionHeight As Integer
        Get
            Return _标题栏高度
        End Get
        Set(value As Integer)
            _标题栏高度 = Math.Max(0, value)
            For Each s In _forms.Values : RecalculateButtonBounds(s) : 更新窗口内边距(s) : Next
            通知重绘()
        End Set
    End Property

    Private _标题栏背景颜色 As Color = Color.FromArgb(32, 32, 32)
    <Category("LakeUI"), Description("标题栏的背景颜色。"), DefaultValue(GetType(Color), "32,32,32")>
    Public Property CaptionBackColor As Color
        Get
            Return _标题栏背景颜色
        End Get
        Set(value As Color)
            _标题栏背景颜色 = value : 通知重绘()
        End Set
    End Property

    Private _标题栏失焦背景颜色 As Color = Color.FromArgb(28, 28, 28)
    <Category("LakeUI"), Description("窗口失去焦点时标题栏的背景颜色。"), DefaultValue(GetType(Color), "28,28,28")>
    Public Property CaptionInactiveBackColor As Color
        Get
            Return _标题栏失焦背景颜色
        End Get
        Set(value As Color)
            _标题栏失焦背景颜色 = value : 通知重绘()
        End Set
    End Property

    Private _标题栏背景图片 As Image = Nothing
    ''' <summary>
    ''' 标题栏背景图片。图片以居中裁切模式（CenterImage）绘制：
    ''' 保持比例缩放至撑满标题栏区域，超出部分从中心裁切。
    ''' 设为 Nothing 则不绘制背景图片。
    ''' </summary>
    <Category("LakeUI"), Description("标题栏背景图片（居中裁切模式）。"), DefaultValue(GetType(Image), Nothing)>
    Public Property CaptionBackgroundImage As Image
        Get
            Return _标题栏背景图片
        End Get
        Set(value As Image)
            _标题栏背景图片 = value : 通知重绘()
        End Set
    End Property

    Private _标题栏遮罩颜色 As Color = Color.Transparent
    ''' <summary>
    ''' 标题栏遮罩颜色，绘制在背景图片之上、图标与文字之下。
    ''' 可使用半透明颜色为背景图片添加色调或降低对比度，使标题文字更易读。
    ''' 设为 Transparent 则不绘制遮罩。
    ''' </summary>
    <Category("LakeUI"), Description("标题栏半透明遮罩颜色，绘制在背景图片之上、图标与文字之下。"), DefaultValue(GetType(Color), "Transparent")>
    Public Property CaptionOverlayColor As Color
        Get
            Return _标题栏遮罩颜色
        End Get
        Set(value As Color)
            _标题栏遮罩颜色 = value : 通知重绘()
        End Set
    End Property

#End Region

#Region "属性 - 标题文字"

    Private _标题文字颜色 As Color = Color.FromArgb(230, 230, 230)
    <Category("LakeUI"), Description("标题文字颜色。"), DefaultValue(GetType(Color), "230,230,230")>
    Public Property TitleForeColor As Color
        Get
            Return _标题文字颜色
        End Get
        Set(value As Color)
            _标题文字颜色 = value : 通知重绘()
        End Set
    End Property

    Private _标题文字失焦颜色 As Color = Color.FromArgb(140, 140, 140)
    <Category("LakeUI"), Description("窗口失去焦点时标题文字颜色。"), DefaultValue(GetType(Color), "140,140,140")>
    Public Property TitleInactiveForeColor As Color
        Get
            Return _标题文字失焦颜色
        End Get
        Set(value As Color)
            _标题文字失焦颜色 = value : 通知重绘()
        End Set
    End Property

    Private _标题文字对齐 As TitleAlignEnum = TitleAlignEnum.Left
    <Category("LakeUI"), Description("标题文字的水平对齐方式。"), DefaultValue(GetType(TitleAlignEnum), "Left")>
    Public Property TitleAlign As TitleAlignEnum
        Get
            Return _标题文字对齐
        End Get
        Set(value As TitleAlignEnum)
            _标题文字对齐 = value : 通知重绘()
        End Set
    End Property

    Private _标题文字字体 As Font = Nothing
    <Category("LakeUI"), Description("标题文字的字体。留空则使用宿主窗口的 Font。"), DefaultValue(GetType(Font), "")>
    Public Property TitleFont As Font
        Get
            Return _标题文字字体
        End Get
        Set(value As Font)
            _标题文字字体 = value : 通知重绘()
        End Set
    End Property

    Private _标题文字左边距 As Integer = 10
    <Category("LakeUI"), Description("标题文字距离左侧（或图标右侧）的间距。"), DefaultValue(10)>
    Public Property TitlePaddingLeft As Integer
        Get
            Return _标题文字左边距
        End Get
        Set(value As Integer)
            _标题文字左边距 = Math.Max(0, value) : 通知重绘()
        End Set
    End Property

    Private _标题文字右边距 As Integer = 10
    <Category("LakeUI"), Description("标题文字距离右侧（或按钮左侧）的间距。"), DefaultValue(10)>
    Public Property TitlePaddingRight As Integer
        Get
            Return _标题文字右边距
        End Get
        Set(value As Integer)
            _标题文字右边距 = Math.Max(0, value) : 通知重绘()
        End Set
    End Property

#End Region

#Region "属性 - 图标"

    Private _图标来源 As IconSourceEnum = IconSourceEnum.FormIcon
    <Category("LakeUI"), Description("标题栏图标来源。"), DefaultValue(GetType(IconSourceEnum), "FormIcon")>
    Public Property IconSource As IconSourceEnum
        Get
            Return _图标来源
        End Get
        Set(value As IconSourceEnum)
            _图标来源 = value : 通知重绘()
        End Set
    End Property

    Private _自定义图标 As Image = Nothing
    <Category("LakeUI"), Description("IconSource 为 Custom 时使用的图像。"), DefaultValue(GetType(Image), "")>
    Public Property CustomIcon As Image
        Get
            Return _自定义图标
        End Get
        Set(value As Image)
            _自定义图标 = value : 通知重绘()
        End Set
    End Property

    Private _图标大小 As Integer = 16
    <Category("LakeUI"), Description("图标的显示尺寸（逻辑像素，正方形）。"), DefaultValue(16)>
    Public Property IconSize As Integer
        Get
            Return _图标大小
        End Get
        Set(value As Integer)
            _图标大小 = Math.Max(0, value) : 通知重绘()
        End Set
    End Property

    Private _图标左边距 As Integer = 8
    <Category("LakeUI"), Description("图标距离窗口左边缘的间距。"), DefaultValue(8)>
    Public Property IconPaddingLeft As Integer
        Get
            Return _图标左边距
        End Get
        Set(value As Integer)
            _图标左边距 = Math.Max(0, value) : 通知重绘()
        End Set
    End Property

#End Region

#Region "属性 - 控制按钮"

    Private _按钮位置 As ButtonPositionEnum = ButtonPositionEnum.Right
    <Category("LakeUI"), Description("控制按钮的布局位置。"), DefaultValue(GetType(ButtonPositionEnum), "Right")>
    Public Property ButtonPosition As ButtonPositionEnum
        Get
            Return _按钮位置
        End Get
        Set(value As ButtonPositionEnum)
            _按钮位置 = value : 通知重绘()
        End Set
    End Property

    Private _按钮宽度 As Integer = 46
    <Category("LakeUI"), Description("每个控制按钮的宽度（逻辑像素）。"), DefaultValue(46)>
    Public Property ButtonWidth As Integer
        Get
            Return _按钮宽度
        End Get
        Set(value As Integer)
            _按钮宽度 = Math.Max(16, value) : 通知重绘()
        End Set
    End Property

    Private _按钮符号大小 As Integer = 10
    <Category("LakeUI"), Description("按钮符号的逻辑尺寸。"), DefaultValue(10)>
    Public Property ButtonGlyphSize As Integer
        Get
            Return _按钮符号大小
        End Get
        Set(value As Integer)
            _按钮符号大小 = Math.Max(4, value) : 通知重绘()
        End Set
    End Property

    Private _按钮符号线宽 As Single = 1.0F
    <Category("LakeUI"), Description("按钮符号线条宽度。"), DefaultValue(1.0F)>
    Public Property ButtonGlyphLineWidth As Single
        Get
            Return _按钮符号线宽
        End Get
        Set(value As Single)
            _按钮符号线宽 = Math.Max(0.5F, value) : 通知重绘()
        End Set
    End Property

    Private _按钮内边距
    <Category("LakeUI"), Description("控制按钮的内边距。"), DefaultValue(GetType(Padding), "0, 0, 0, 0")>
    Public Property ButtonPadding As Padding
        Get
            Return _按钮内边距
        End Get
        Set(value As Padding)
            _按钮内边距 = value : 通知重绘()
        End Set
    End Property

    Private _按钮圆角半径 As Integer = 0
    <Category("LakeUI"), Description("控制按钮背景圆角半径。"), DefaultValue(0)>
    Public Property ButtonCornerRadius As Integer
        Get
            Return _按钮圆角半径
        End Get
        Set(value As Integer)
            _按钮圆角半径 = Math.Max(0, value) : 通知重绘()
        End Set
    End Property

    Private _按钮间距 As Integer = 0
    <Category("LakeUI"), Description("控制按钮之间的间距。"), DefaultValue(0)>
    Public Property ButtonSpacing As Integer
        Get
            Return _按钮间距
        End Get
        Set(value As Integer)
            _按钮间距 = Math.Max(0, value) : 通知重绘()
        End Set
    End Property

#End Region

#Region "属性 - 关闭按钮颜色"

    Private _关闭按钮背景颜色 As Color = Color.Transparent
    <Category("LakeUI"), DefaultValue(GetType(Color), "Transparent")>
    Public Property CloseButtonBackColor As Color
        Get
            Return _关闭按钮背景颜色
        End Get
        Set(value As Color)
            _关闭按钮背景颜色 = value : 通知重绘()
        End Set
    End Property

    Private _关闭按钮悬停背景颜色 As Color = Color.FromArgb(232, 17, 35)
    <Category("LakeUI"), DefaultValue(GetType(Color), "232,17,35")>
    Public Property CloseButtonHoverBackColor As Color
        Get
            Return _关闭按钮悬停背景颜色
        End Get
        Set(value As Color)
            _关闭按钮悬停背景颜色 = value : 通知重绘()
        End Set
    End Property

    Private _关闭按钮按下背景颜色 As Color = Color.FromArgb(200, 15, 30)
    <Category("LakeUI"), DefaultValue(GetType(Color), "200,15,30")>
    Public Property CloseButtonPressedBackColor As Color
        Get
            Return _关闭按钮按下背景颜色
        End Get
        Set(value As Color)
            _关闭按钮按下背景颜色 = value : 通知重绘()
        End Set
    End Property

    Private _关闭按钮符号颜色 As Color = Color.FromArgb(200, 200, 200)
    <Category("LakeUI"), DefaultValue(GetType(Color), "200,200,200")>
    Public Property CloseButtonGlyphColor As Color
        Get
            Return _关闭按钮符号颜色
        End Get
        Set(value As Color)
            _关闭按钮符号颜色 = value : 通知重绘()
        End Set
    End Property

    Private _关闭按钮悬停符号颜色 As Color = Color.White
    <Category("LakeUI"), DefaultValue(GetType(Color), "White")>
    Public Property CloseButtonHoverGlyphColor As Color
        Get
            Return _关闭按钮悬停符号颜色
        End Get
        Set(value As Color)
            _关闭按钮悬停符号颜色 = value : 通知重绘()
        End Set
    End Property

#End Region

#Region "属性 - 最大化/最小化按钮颜色"

    Private _功能按钮背景颜色 As Color = Color.Transparent
    <Category("LakeUI"), DefaultValue(GetType(Color), "Transparent")>
    Public Property CaptionButtonBackColor As Color
        Get
            Return _功能按钮背景颜色
        End Get
        Set(value As Color)
            _功能按钮背景颜色 = value : 通知重绘()
        End Set
    End Property

    Private _功能按钮悬停背景颜色 As Color = Color.FromArgb(55, 55, 55)
    <Category("LakeUI"), DefaultValue(GetType(Color), "55,55,55")>
    Public Property CaptionButtonHoverBackColor As Color
        Get
            Return _功能按钮悬停背景颜色
        End Get
        Set(value As Color)
            _功能按钮悬停背景颜色 = value : 通知重绘()
        End Set
    End Property

    Private _功能按钮按下背景颜色 As Color = Color.FromArgb(70, 70, 70)
    <Category("LakeUI"), DefaultValue(GetType(Color), "70,70,70")>
    Public Property CaptionButtonPressedBackColor As Color
        Get
            Return _功能按钮按下背景颜色
        End Get
        Set(value As Color)
            _功能按钮按下背景颜色 = value : 通知重绘()
        End Set
    End Property

    Private _功能按钮符号颜色 As Color = Color.FromArgb(200, 200, 200)
    <Category("LakeUI"), DefaultValue(GetType(Color), "200,200,200")>
    Public Property CaptionButtonGlyphColor As Color
        Get
            Return _功能按钮符号颜色
        End Get
        Set(value As Color)
            _功能按钮符号颜色 = value : 通知重绘()
        End Set
    End Property

    Private _功能按钮悬停符号颜色 As Color = Color.White
    <Category("LakeUI"), DefaultValue(GetType(Color), "White")>
    Public Property CaptionButtonHoverGlyphColor As Color
        Get
            Return _功能按钮悬停符号颜色
        End Get
        Set(value As Color)
            _功能按钮悬停符号颜色 = value : 通知重绘()
        End Set
    End Property

#End Region

#Region "属性 - 调整大小"

    Private _调整边框宽度 As Integer = 6
    <Category("LakeUI"), Description("窗口边缘的调整大小热区宽度。"), DefaultValue(6)>
    Public Property ResizeBorderWidth As Integer
        Get
            Return _调整边框宽度
        End Get
        Set(value As Integer)
            _调整边框宽度 = Math.Max(1, value)
        End Set
    End Property

    Private _允许调整大小 As Boolean = True
    <Category("LakeUI"), DefaultValue(True)>
    Public Property AllowResize As Boolean
        Get
            Return _允许调整大小
        End Get
        Set(value As Boolean)
            _允许调整大小 = value
        End Set
    End Property

    Private _最大化时隐藏调整边框 As Boolean = True
    <Category("LakeUI"), DefaultValue(True)>
    Public Property HideResizeBorderWhenMaximized As Boolean
        Get
            Return _最大化时隐藏调整边框
        End Get
        Set(value As Boolean)
            _最大化时隐藏调整边框 = value
        End Set
    End Property

#End Region

#Region "属性 - 高级 (排除区域)"

    Private _标题栏排除区域 As New List(Of Rectangle)
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property CaptionExcludeBounds As List(Of Rectangle)
        Get
            Return _标题栏排除区域
        End Get
        Set(value As List(Of Rectangle))
            If value Is Nothing Then value = New List(Of Rectangle)
            _标题栏排除区域 = value
        End Set
    End Property

#End Region

#Region "属性 - 阴影"

    Private _阴影模式 As ShadowModeEnum = ShadowModeEnum.None
    ''' <summary>
    ''' 窗口阴影模式。
    ''' None — 无阴影，移除 WS_CAPTION 以避免透明圆角伪影。
    ''' DWM — 保留 WS_CAPTION 以获取 DWM 原生窗口阴影（可能在角落产生透明圆角伪影）。
    ''' Layer — 移除 WS_CAPTION，使用自定义分层窗口阴影。
    ''' </summary>
    <Category("LakeUI"), Description("窗口阴影模式：None 无阴影、DWM 原生阴影、Layer 自定义分层窗口阴影。"), DefaultValue(GetType(ShadowModeEnum), "None")>
    Public Property ShadowMode As ShadowModeEnum
        Get
            Return _阴影模式
        End Get
        Set(value As ShadowModeEnum)
            If _阴影模式 = value Then Return
            _阴影模式 = value
            For Each s In _forms.Values
                Dim hWnd = s.HostForm.Handle
                Dim style As Long = GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64()
                If value = ShadowModeEnum.DWM Then
                    style = style Or WS_CAPTION
                Else
                    style = style And Not WS_CAPTION
                End If
                SetWindowLongPtr(hWnd, GWL_STYLE, New IntPtr(style))
                SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                             SWP_FRAMECHANGED Or SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOZORDER)
                更新阴影(s)
                s.HostForm.Invalidate()
            Next
        End Set
    End Property

    Private _分层阴影深度 As Integer = 15
    <Category("LakeUI"), Description("分层阴影的扩展范围（逻辑像素）。值越大阴影越宽越深。仅 ShadowMode = Layer 时生效。"), DefaultValue(15)>
    Public Property LayerShadowDepth As Integer
        Get
            Return _分层阴影深度
        End Get
        Set(value As Integer)
            _分层阴影深度 = Math.Max(1, value)
            For Each s In _forms.Values
                If s.ShadowForm IsNot Nothing Then s.ShadowForm.ForceReset()
                更新阴影(s)
            Next
        End Set
    End Property

    Private _分层阴影颜色 As Color = Color.Black
    <Category("LakeUI"), Description("分层阴影颜色。仅 ShadowMode = Layer 时生效。"), DefaultValue(GetType(Color), "Black")>
    Public Property LayerShadowColor As Color
        Get
            Return _分层阴影颜色
        End Get
        Set(value As Color)
            _分层阴影颜色 = value
            For Each s In _forms.Values
                If s.ShadowForm IsNot Nothing Then s.ShadowForm.ForceReset()
                更新阴影(s)
            Next
        End Set
    End Property

    Private _分层阴影不透明度 As Byte = 80
    <Category("LakeUI"), Description("分层阴影的最大不透明度 (0-255)。仅 ShadowMode = Layer 时生效。"), DefaultValue(CByte(80))>
    Public Property LayerShadowOpacity As Byte
        Get
            Return _分层阴影不透明度
        End Get
        Set(value As Byte)
            _分层阴影不透明度 = value
            For Each s In _forms.Values
                If s.ShadowForm IsNot Nothing Then s.ShadowForm.ForceReset()
                更新阴影(s)
            Next
        End Set
    End Property

    Private _分层阴影调整宽度 As Integer = 0
    ''' <summary>
    ''' 分层阴影区域中可触发窗口大小调整的热区宽度（逻辑像素）。
    ''' 表示从窗口本体边缘向外延伸多少像素的阴影区域可以拖动调整大小。
    ''' 0 = 阴影区域不可调整大小（鼠标穿透）。仅 ShadowMode = Layer 时生效。
    ''' </summary>
    <Category("LakeUI"), Description("分层阴影中可触发大小调整的热区宽度（逻辑像素）。0 = 阴影不可调整大小。仅 ShadowMode = Layer 时生效。"), DefaultValue(0)>
    Public Property LayerShadowResizeWidth As Integer
        Get
            Return _分层阴影调整宽度
        End Get
        Set(value As Integer)
            _分层阴影调整宽度 = Math.Max(0, Math.Min(value, _分层阴影深度))
            For Each s In _forms.Values
                If s.ShadowForm IsNot Nothing Then
                    s.ShadowForm.ResizeWidth = _分层阴影调整宽度
                    s.ShadowForm.UpdateHitTestTransparency()
                End If
            Next
        End Set
    End Property

    Private Sub 更新阴影(s As PerFormState)
        If s Is Nothing OrElse s.HostForm Is Nothing Then Return
        Dim zoomed As Boolean = (s.HostForm.WindowState = FormWindowState.Maximized)
        Dim minimized As Boolean = (s.HostForm.WindowState = FormWindowState.Minimized)

        If _阴影模式 <> ShadowModeEnum.Layer OrElse zoomed OrElse minimized OrElse Not s.HostForm.Visible Then
            If s.ShadowForm IsNot Nothing Then
                s.ShadowForm.Visible = False
            End If
            Return
        End If

        If s.ShadowForm Is Nothing Then
            s.ShadowForm = New ShadowWindow With {
                .HostHandle = s.HostForm.Handle,
                .ShadowDepth = _分层阴影深度,
                .ResizeWidth = _分层阴影调整宽度
            }
            s.ShadowForm.UpdateHitTestTransparency()
            s.ShadowForm.Show()
        End If

        Dim bounds = s.HostForm.Bounds
        s.ShadowForm.HostHandle = s.HostForm.Handle
        s.ShadowForm.ShadowDepth = _分层阴影深度
        s.ShadowForm.ResizeWidth = _分层阴影调整宽度
        s.ShadowForm.UpdateShadow(bounds, _分层阴影深度, _分层阴影颜色, _分层阴影不透明度, s.IsInSizeMove)
        s.ShadowForm.PlaceBehind(s.HostForm.Handle)
        If Not s.ShadowForm.Visible Then s.ShadowForm.Visible = True

        If s.AnimatingShow AndAlso _显示动画模式 = WindowShowAnimationMode.Win32 Then
            s.ShadowForm.SetGlobalAlpha(0)
        End If
    End Sub

    Private Sub 销毁阴影(s As PerFormState)
        If s.ShadowForm IsNot Nothing Then
            s.ShadowForm.Close()
            s.ShadowForm.Dispose()
            s.ShadowForm = Nothing
        End If
    End Sub

#End Region

#Region "属性 - 动画"

    Private _显示动画模式 As WindowShowAnimationMode = WindowShowAnimationMode.DWM
    ''' <summary>
    ''' 窗口出现时的动画方式。
    ''' DWM（默认）— 使用 DWM 原生窗口出现过渡动画。
    ''' Win32 — 禁止 DWM 过渡，使用自定义分层窗口透明度渐入动画。
    ''' None — 无动画，禁止 DWM 过渡以避免白屏闪烁。
    ''' </summary>
    <Category("LakeUI"), Description("窗口出现时的动画方式：DWM 原生动画、Win32 自定义渐入或无动画。"), DefaultValue(GetType(WindowShowAnimationMode), "DWM")>
    Public Property ShowAnimation As WindowShowAnimationMode
        Get
            Return _显示动画模式
        End Get
        Set(value As WindowShowAnimationMode)
            _显示动画模式 = value
        End Set
    End Property

    Private _关闭动画模式 As WindowCloseAnimationMode = WindowCloseAnimationMode.DWM
    ''' <summary>
    ''' 窗口关闭时的动画方式。
    ''' DWM（默认）— 使用 DWM 原生窗口关闭过渡动画。
    ''' Win32 — 禁止 DWM 过渡，使用自定义透明度渐出动画。
    ''' None — 无动画，禁止 DWM 过渡以避免白屏闪烁。
    ''' </summary>
    <Category("LakeUI"), Description("窗口关闭时的动画方式：DWM 原生动画、Win32 自定义渐出或无动画。"), DefaultValue(GetType(WindowCloseAnimationMode), "DWM")>
    Public Property CloseAnimation As WindowCloseAnimationMode
        Get
            Return _关闭动画模式
        End Get
        Set(value As WindowCloseAnimationMode)
            _关闭动画模式 = value
        End Set
    End Property

    Private _动画持续时间 As Integer = 200
    <Category("LakeUI"), Description("渐入/渐出动画的持续时间（毫秒）。"), DefaultValue(200)>
    Public Property AnimationDuration As Integer
        Get
            Return _动画持续时间
        End Get
        Set(value As Integer)
            _动画持续时间 = Math.Max(50, value)
        End Set
    End Property

#End Region

#Region "事件"

    Public Event CaptionPaint(sender As Object, e As CaptionPaintEventArgs)
    Public Event ActiveChanged(sender As Object, e As ActiveChangedEventArgs)
    Public Event CustomHitTest(sender As Object, e As CustomHitTestEventArgs)

    Public Class CaptionPaintEventArgs : Inherits EventArgs
        Public ReadOnly Property Graphics As Graphics
        Public ReadOnly Property CaptionBounds As Rectangle
        Public ReadOnly Property IsActive As Boolean
        Public ReadOnly Property HostForm As Form
        Public Sub New(g As Graphics, rect As Rectangle, active As Boolean, form As Form)
            Graphics = g : CaptionBounds = rect : IsActive = active : HostForm = form
        End Sub
    End Class

    Public Class ActiveChangedEventArgs : Inherits EventArgs
        Public ReadOnly Property IsActive As Boolean
        Public ReadOnly Property HostForm As Form
        Public Sub New(activated As Boolean, form As Form)
            IsActive = activated : HostForm = form
        End Sub
    End Class

    Public Class CustomHitTestEventArgs : Inherits EventArgs
        Public ReadOnly Property ClientPoint As Point
        Public ReadOnly Property DefaultResult As Integer
        Public ReadOnly Property HostForm As Form
        Public Property OverrideResult As Integer?
        Public Sub New(pt As Point, defaultHit As Integer, form As Form)
            ClientPoint = pt : DefaultResult = defaultHit : HostForm = form : OverrideResult = Nothing
        End Sub
    End Class

#End Region

#Region "只读属性"

    <Browsable(False)>
    Public ReadOnly Property AttachedForms As IReadOnlyList(Of Form)
        Get
            Return _forms.Values.Select(Function(s) s.HostForm).ToList()
        End Get
    End Property

#End Region

#Region "按钮区域计算"

    Friend Sub RecalculateButtonBounds(s As PerFormState)
        If s Is Nothing Then Return
        Dim w As Integer = s.HostForm.ClientSize.Width
        Dim bw As Integer = _按钮宽度
        Dim bh As Integer = _标题栏高度
        Dim sp As Integer = _按钮间距

        Dim 列表 As New List(Of Integer)
        If _按钮位置 = ButtonPositionEnum.Right Then
            If s.HostForm.MinimizeBox Then 列表.Add(HTMINBUTTON)
            If s.HostForm.MaximizeBox Then 列表.Add(HTMAXBUTTON)
            列表.Add(HTCLOSE)
            Dim totalW As Integer = 列表.Count * bw + Math.Max(0, 列表.Count - 1) * sp
            Dim startX As Integer = w - totalW
            For i = 0 To 列表.Count - 1
                Dim r As New Rectangle(startX + i * (bw + sp), 0, bw, bh)
                Select Case 列表(i) : Case HTCLOSE : s.CloseRect = r : Case HTMAXBUTTON : s.MaxRect = r : Case HTMINBUTTON : s.MinRect = r : End Select
            Next
        Else
            列表.Add(HTCLOSE)
            If s.HostForm.MaximizeBox Then 列表.Add(HTMAXBUTTON)
            If s.HostForm.MinimizeBox Then 列表.Add(HTMINBUTTON)
            For i = 0 To 列表.Count - 1
                Dim r As New Rectangle(i * (bw + sp), 0, bw, bh)
                Select Case 列表(i) : Case HTCLOSE : s.CloseRect = r : Case HTMAXBUTTON : s.MaxRect = r : Case HTMINBUTTON : s.MinRect = r : End Select
            Next
        End If
        If Not s.HostForm.MaximizeBox Then s.MaxRect = Rectangle.Empty
        If Not s.HostForm.MinimizeBox Then s.MinRect = Rectangle.Empty

        If _图标来源 <> IconSourceEnum.None Then
            Dim iconY As Integer = (_标题栏高度 - _图标大小) \ 2
            If _按钮位置 = ButtonPositionEnum.Left Then
                Dim totalBtnW As Integer = 列表.Count * bw + Math.Max(0, 列表.Count - 1) * sp
                s.IconRect = New Rectangle(totalBtnW + _图标左边距, iconY, _图标大小, _图标大小)
            Else
                s.IconRect = New Rectangle(_图标左边距, iconY, _图标大小, _图标大小)
            End If
        Else
            s.IconRect = Rectangle.Empty
        End If
    End Sub

#End Region

#Region "绘制"

    ''' <summary>为指定窗体执行完整绘制。通常由内部 Paint 事件自动调用。</summary>
    Public Sub PaintWindow(g As Graphics, targetForm As Form)
        Dim s = 查找状态(targetForm)
        If s Is Nothing Then Return
        RecalculateButtonBounds(s)

        Dim w As Integer = s.HostForm.ClientSize.Width
        Dim h As Integer = s.HostForm.ClientSize.Height
        Dim active As Boolean = s.Activated

        g.SmoothingMode = SmoothingMode.Default
        g.PixelOffsetMode = PixelOffsetMode.Default

        Dim captionRect As New Rectangle(0, 0, w, _标题栏高度)
        Using brush As New SolidBrush(If(active, _标题栏背景颜色, _标题栏失焦背景颜色))
            g.FillRectangle(brush, captionRect)
        End Using

        绘制标题栏背景图片(g, captionRect)

        If _标题栏遮罩颜色.A > 0 Then
            Using brush As New SolidBrush(_标题栏遮罩颜色)
                g.FillRectangle(brush, captionRect)
            End Using
        End If

        绘制图标(g, s)
        绘制标题文字(g, s)

        绘制控制按钮(g, s, s.CloseRect, HTCLOSE)
        If s.HostForm.MaximizeBox Then 绘制控制按钮(g, s, s.MaxRect, HTMAXBUTTON)
        If s.HostForm.MinimizeBox Then 绘制控制按钮(g, s, s.MinRect, HTMINBUTTON)

        If _边框厚度 > 0 Then
            Dim bdr As Integer = _边框厚度
            Using brush As New SolidBrush(If(active, _边框颜色, _边框失焦颜色))
                g.FillRectangle(brush, 0, 0, w, bdr)
                g.FillRectangle(brush, 0, h - bdr, w, bdr)
                g.FillRectangle(brush, 0, bdr, bdr, h - bdr * 2)
                g.FillRectangle(brush, w - bdr, bdr, bdr, h - bdr * 2)
            End Using
        End If

        RaiseEvent CaptionPaint(Me, New CaptionPaintEventArgs(g, captionRect, active, s.HostForm))
    End Sub

    Private Sub 绘制标题栏背景图片(g As Graphics, captionRect As Rectangle)
        If _标题栏背景图片 Is Nothing Then Return
        Dim img As Image = _标题栏背景图片
        Dim cw As Integer = captionRect.Width
        Dim ch As Integer = captionRect.Height
        If cw < 1 OrElse ch < 1 Then Return

        Dim ratioW As Single = CSng(cw) / img.Width
        Dim ratioH As Single = CSng(ch) / img.Height
        Dim ratio As Single = Math.Max(ratioW, ratioH)
        Dim drawW As Single = img.Width * ratio
        Dim drawH As Single = img.Height * ratio
        Dim dx As Single = captionRect.X + (cw - drawW) / 2.0F
        Dim dy As Single = captionRect.Y + (ch - drawH) / 2.0F

        Dim oldClip = g.Clip
        g.SetClip(captionRect)
        g.InterpolationMode = InterpolationMode.HighQualityBicubic
        g.DrawImage(img, dx, dy, drawW, drawH)
        g.Clip = oldClip
    End Sub

    Private Sub 绘制图标(g As Graphics, s As PerFormState)
        If _图标来源 = IconSourceEnum.None OrElse s.IconRect.IsEmpty Then Return
        Dim img As Image = Nothing
        If _图标来源 = IconSourceEnum.Custom Then
            img = _自定义图标
        ElseIf _图标来源 = IconSourceEnum.FormIcon AndAlso s.HostForm?.Icon IsNot Nothing Then
            If s.CachedIconSource IsNot s.HostForm.Icon Then
                s.CachedIconBitmap?.Dispose()
                s.CachedIconBitmap = s.HostForm.Icon.ToBitmap()
                s.CachedIconSource = s.HostForm.Icon
            End If
            img = s.CachedIconBitmap
        End If
        If img IsNot Nothing Then
            g.InterpolationMode = InterpolationMode.HighQualityBicubic
            g.DrawImage(img, s.IconRect)
        End If
    End Sub

    Private Sub 绘制标题文字(g As Graphics, s As PerFormState)
        Dim text As String = s.HostForm.Text
        If String.IsNullOrEmpty(text) Then Return
        Dim font As Font = If(_标题文字字体, s.HostForm.Font)
        Dim fgColor As Color = If(s.Activated, _标题文字颜色, _标题文字失焦颜色)
        Dim leftEdge, rightEdge As Integer

        If _按钮位置 = ButtonPositionEnum.Right Then
            leftEdge = If(Not s.IconRect.IsEmpty, s.IconRect.Right + _标题文字左边距, _标题文字左边距)
            Dim btnLeft As Integer = s.CloseRect.Left
            If s.HostForm.MaximizeBox AndAlso Not s.MaxRect.IsEmpty Then btnLeft = Math.Min(btnLeft, s.MaxRect.Left)
            If s.HostForm.MinimizeBox AndAlso Not s.MinRect.IsEmpty Then btnLeft = Math.Min(btnLeft, s.MinRect.Left)
            rightEdge = btnLeft - _标题文字右边距
        Else
            If Not s.IconRect.IsEmpty Then
                leftEdge = s.IconRect.Right + _标题文字左边距
            Else
                Dim btnRight As Integer = s.CloseRect.Right
                If s.HostForm.MaximizeBox AndAlso Not s.MaxRect.IsEmpty Then btnRight = Math.Max(btnRight, s.MaxRect.Right)
                If s.HostForm.MinimizeBox AndAlso Not s.MinRect.IsEmpty Then btnRight = Math.Max(btnRight, s.MinRect.Right)
                leftEdge = btnRight + _标题文字左边距
            End If
            rightEdge = s.HostForm.ClientSize.Width - _标题文字右边距
        End If

        Dim textRect As New Rectangle(leftEdge, 0, Math.Max(0, rightEdge - leftEdge), _标题栏高度)
        Dim flags As TextFormatFlags = TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine Or TextFormatFlags.NoPadding
        Select Case _标题文字对齐
            Case TitleAlignEnum.Left : flags = flags Or TextFormatFlags.Left
            Case TitleAlignEnum.Center : flags = flags Or TextFormatFlags.HorizontalCenter
            Case TitleAlignEnum.Right : flags = flags Or TextFormatFlags.Right
        End Select
        TextRenderer.DrawText(g, text, font, textRect, fgColor, flags)
    End Sub

    Private Sub 绘制控制按钮(g As Graphics, s As PerFormState, rect As Rectangle, htValue As Integer)
        If rect.IsEmpty Then Return
        Dim isClose As Boolean = (htValue = HTCLOSE)
        Dim isHover As Boolean = (s.HoverHit = htValue)
        Dim isPressed As Boolean = (s.PressedHit = htValue)
        Dim bgColor, symColor As Color

        If isClose Then
            If isPressed AndAlso isHover Then
                bgColor = _关闭按钮按下背景颜色 : symColor = _关闭按钮悬停符号颜色
            ElseIf isHover Then
                bgColor = _关闭按钮悬停背景颜色 : symColor = _关闭按钮悬停符号颜色
            Else
                bgColor = _关闭按钮背景颜色 : symColor = _关闭按钮符号颜色
            End If
        Else
            If isPressed AndAlso isHover Then
                bgColor = _功能按钮按下背景颜色 : symColor = _功能按钮悬停符号颜色
            ElseIf isHover Then
                bgColor = _功能按钮悬停背景颜色 : symColor = _功能按钮悬停符号颜色
            Else
                bgColor = _功能按钮背景颜色 : symColor = _功能按钮符号颜色
            End If
        End If

        Dim vis As New Rectangle(rect.X + _按钮内边距.Left, rect.Y + _按钮内边距.Top,
                                  rect.Width - _按钮内边距.Horizontal, rect.Height - _按钮内边距.Vertical)
        If vis.Width <= 0 OrElse vis.Height <= 0 Then Return

        If bgColor <> Color.Transparent AndAlso bgColor.A > 0 Then
            Dim r As Integer = Math.Min(_按钮圆角半径, Math.Min(vis.Width, vis.Height) \ 2)
            If r > 0 Then
                g.SmoothingMode = SmoothingMode.AntiAlias
                Using brush As New SolidBrush(bgColor), path As New GraphicsPath()
                    Dim d As Integer = r * 2
                    path.AddArc(vis.X, vis.Y, d, d, 180, 90)
                    path.AddArc(vis.Right - d, vis.Y, d, d, 270, 90)
                    path.AddArc(vis.Right - d, vis.Bottom - d, d, d, 0, 90)
                    path.AddArc(vis.X, vis.Bottom - d, d, d, 90, 90)
                    path.CloseFigure()
                    g.FillPath(brush, path)
                End Using
                g.SmoothingMode = SmoothingMode.Default
            Else
                Using brush As New SolidBrush(bgColor) : g.FillRectangle(brush, vis) : End Using
            End If
        End If

        Dim sz As Integer = _按钮符号大小
        Dim cx As Integer = vis.X + (vis.Width - sz) \ 2
        Dim cy As Integer = vis.Y + (vis.Height - sz) \ 2
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using pen As New Pen(symColor, _按钮符号线宽)
            Select Case htValue
                Case HTCLOSE
                    g.DrawLine(pen, cx, cy, cx + sz, cy + sz)
                    g.DrawLine(pen, cx + sz, cy, cx, cy + sz)
                Case HTMAXBUTTON
                    If s.HostForm.WindowState = FormWindowState.Maximized Then
                        Dim off As Integer = CInt(sz * 0.25)
                        g.DrawRectangle(pen, cx + off, cy, sz - off, sz - off)
                        g.DrawRectangle(pen, cx, cy + off, sz - off, sz - off)
                    Else
                        g.DrawRectangle(pen, cx, cy, sz, sz)
                    End If
                Case HTMINBUTTON
                    g.DrawLine(pen, cx, cy + sz \ 2, cx + sz, cy + sz \ 2)
            End Select
        End Using
        g.SmoothingMode = SmoothingMode.Default
    End Sub

    ''' <summary>请求指定窗体重绘标题栏区域。</summary>
    Public Sub InvalidateCaption(form As Form)
        If form IsNot Nothing AndAlso form.IsHandleCreated Then
            form.Invalidate(New Rectangle(0, 0, form.ClientSize.Width, _标题栏高度))
        End If
    End Sub

#End Region

#Region "附加 / 分离"

    ''' <summary>
    ''' 将当前样式附加到目标窗体。可多次调用以附加到不同窗体，所有窗体共享同一套外观属性。
    ''' 建议在 Form.Load 中调用。
    ''' </summary>
    Public Sub Attach(targetForm As Form)
#If NET5_0 Then
        If targetForm Is Nothing Then Throw New ArgumentNullException(NameOf(targetForm))
#Else
        ArgumentNullException.ThrowIfNull(targetForm)
#End If
        If Not targetForm.IsHandleCreated Then
            AddHandler targetForm.HandleCreated, Sub(sender2, ev) Attach(targetForm)
            Return
        End If
        If _forms.ContainsKey(targetForm.Handle) Then Return

        Dim s As New PerFormState(targetForm) With {.OriginalPadding = targetForm.Padding}

        Dim hWnd As IntPtr = targetForm.Handle

        ' ── 第一步：标记与隐藏（通过 P/Invoke 直接操作分层窗口，绝不触碰 Form.Opacity） ──
        ' Form.Opacity 会触发 AllowTransparency → UpdateStyles() →
        ' SetWindowLong(GWL_STYLE, CreateParams.Style) 把 WS_CAPTION 写回，
        ' 导致窗口以默认标题栏出现（白屏）。
        ' 因此改用 SetLayeredWindowAttributes 直接设置 alpha=0。
        s.OriginalOpacity = targetForm.Opacity
        If _显示动画模式 = WindowShowAnimationMode.Win32 Then s.AnimatingShow = True
        If _显示动画模式 <> WindowShowAnimationMode.DWM Then
            Dim exStyle As Long = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64()
            SetWindowLongPtr(hWnd, GWL_EXSTYLE, New IntPtr(exStyle Or WS_EX_LAYERED))
            SetLayeredWindowAttributes(hWnd, 0, 0, LWA_ALPHA)
            s.PendingFirstPaintRestore = True
        End If

        ' ── 第二步：修改窗口样式 ──
        Dim style As Long = GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64()
        If _阴影模式 = ShadowModeEnum.DWM Then
            style = style Or WS_CAPTION
        Else
            style = style And Not CLng(WS_CAPTION)
        End If
        style = style Or WS_THICKFRAME Or WS_MINIMIZEBOX Or WS_MAXIMIZEBOX Or WS_SYSMENU
        SetWindowLongPtr(hWnd, GWL_STYLE, New IntPtr(style))

        ' ── 第三步：DWM 属性 ──
        Try
            Dim pref As Integer = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND
            Dim u1 = DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, pref, 4)
            Dim colorNone As Integer = DWMWA_COLOR_NONE
            Dim u2 = DwmSetWindowAttribute(hWnd, DWMWA_BORDER_COLOR, colorNone, 4)
            Dim margins As MARGINS : margins.Bottom = 1
            Dim u3 = DwmExtendFrameIntoClientArea(hWnd, margins)
            If _显示动画模式 <> WindowShowAnimationMode.DWM Then
                Dim disable As Integer = 1
                Dim unused = DwmSetWindowAttribute(hWnd, DWMWA_TRANSITIONS_FORCEDISABLED, disable, 4)
            End If
        Catch
        End Try

        ' ── 第四步：注册拦截器 ──
        s.Interceptor = New WindowMessageInterceptor(Me, s)
        _forms(hWnd) = s

        ' ── 第五步：使样式变更生效 ──
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                     CUInt(SWP_FRAMECHANGED Or SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOZORDER))

        Dim setStyleMethod = GetType(Control).GetMethod("SetStyle", BindingFlags.Instance Or BindingFlags.NonPublic)
        setStyleMethod?.Invoke(targetForm, New Object() {
            ControlStyles.OptimizedDoubleBuffer Or ControlStyles.AllPaintingInWmPaint, True})

        AddHandler targetForm.Paint, AddressOf 宿主窗口_Paint
        AddHandler targetForm.FormClosed, AddressOf 宿主窗口_FormClosed
        RecalculateButtonBounds(s)
        更新窗口内边距(s)
        targetForm.Invalidate()
        更新阴影(s)
    End Sub

    ''' <summary>从指定窗体分离。</summary>
    Public Sub Detach(targetForm As Form)
        If targetForm Is Nothing OrElse Not targetForm.IsHandleCreated Then Return
        Dim s As PerFormState = Nothing
        If Not _forms.TryGetValue(targetForm.Handle, s) Then Return
        _forms.Remove(targetForm.Handle)

        s.CachedIconBitmap?.Dispose()
        s.Interceptor?.ReleaseHandle()
        销毁阴影(s)
        RemoveHandler targetForm.Paint, AddressOf 宿主窗口_Paint
        RemoveHandler targetForm.FormClosed, AddressOf 宿主窗口_FormClosed
        targetForm.Padding = s.OriginalPadding
    End Sub

    ''' <summary>分离所有已附加的窗体。</summary>
    Public Sub DetachAll()
        For Each s In _forms.Values.ToList()
            Detach(s.HostForm)
        Next
    End Sub

    ''' <summary>
    ''' 强制以当前最新属性重新接管目标窗体。
    ''' 重新应用窗口样式、DWM 属性、内边距、按钮布局及阴影，并触发重绘。
    ''' 如果窗体尚未附加，则等同于调用 <see cref="Attach"/>。
    ''' </summary>
    Public Sub Refresh(targetForm As Form)
#If NET5_0 Then
        If targetForm Is Nothing Then Throw New ArgumentNullException(NameOf(targetForm))
#Else
        ArgumentNullException.ThrowIfNull(targetForm)
#End If
        If Not targetForm.IsHandleCreated Then Return

        Dim s = 查找状态(targetForm)
        If s Is Nothing Then
            Attach(targetForm)
            Return
        End If

        Dim hWnd As IntPtr = targetForm.Handle

        ' ── 重新应用窗口样式 ──
        Dim style As Long = GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64()
        If _阴影模式 = ShadowModeEnum.DWM Then
            style = style Or WS_CAPTION
        Else
            style = style And Not CLng(WS_CAPTION)
        End If
        style = style Or WS_THICKFRAME Or WS_MINIMIZEBOX Or WS_MAXIMIZEBOX Or WS_SYSMENU
        SetWindowLongPtr(hWnd, GWL_STYLE, New IntPtr(style))

        ' ── 重新应用 DWM 属性 ──
        Try
            Dim pref As Integer = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND
            Dim unused1 = DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, pref, 4)
            Dim colorNone As Integer = DWMWA_COLOR_NONE
            Dim unused2 = DwmSetWindowAttribute(hWnd, DWMWA_BORDER_COLOR, colorNone, 4)
            Dim margins As MARGINS : margins.Bottom = 1
            Dim unused3 = DwmExtendFrameIntoClientArea(hWnd, margins)
        Catch
        End Try

        ' ── 使样式变更生效 ──
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                     CUInt(SWP_FRAMECHANGED Or SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOZORDER))

        ' ── 重新计算布局 ──
        RecalculateButtonBounds(s)
        更新窗口内边距(s)

        ' ── 重建阴影 ──
        If s.ShadowForm IsNot Nothing Then s.ShadowForm.ForceReset()
        更新阴影(s)

        ' ── 强制重绘 ──
        targetForm.Invalidate()
    End Sub

    ''' <summary>
    ''' 强制以当前最新属性重新接管所有已附加的窗体。
    ''' </summary>
    Public Sub RefreshAll()
        For Each s In _forms.Values.ToList()
            Refresh(s.HostForm)
        Next
    End Sub

#End Region

#Region "命中测试"

    Friend Function 执行命中测试(s As PerFormState, clientPoint As Point) As Integer
        If s Is Nothing Then Return HTCLIENT
        Dim w As Integer = s.HostForm.ClientSize.Width
        Dim h As Integer = s.HostForm.ClientSize.Height
        Dim bw As Integer = _调整边框宽度
        Dim zoomed As Boolean = (s.HostForm.WindowState = FormWindowState.Maximized)

        If _允许调整大小 AndAlso Not (zoomed AndAlso _最大化时隐藏调整边框) Then
            If clientPoint.X < bw AndAlso clientPoint.Y < bw Then Return HTTOPLEFT
            If clientPoint.X >= w - bw AndAlso clientPoint.Y < bw Then Return HTTOPRIGHT
            If clientPoint.X < bw AndAlso clientPoint.Y >= h - bw Then Return HTBOTTOMLEFT
            If clientPoint.X >= w - bw AndAlso clientPoint.Y >= h - bw Then Return HTBOTTOMRIGHT
            If clientPoint.X < bw Then Return HTLEFT
            If clientPoint.X >= w - bw Then Return HTRIGHT
            If clientPoint.Y < bw Then Return HTTOP
            If clientPoint.Y >= h - bw Then Return HTBOTTOM
        End If

        If Not s.CloseRect.IsEmpty AndAlso s.CloseRect.Contains(clientPoint) Then Return HTCLOSE
        If Not s.MaxRect.IsEmpty AndAlso s.MaxRect.Contains(clientPoint) Then Return HTMAXBUTTON
        If Not s.MinRect.IsEmpty AndAlso s.MinRect.Contains(clientPoint) Then Return HTMINBUTTON
        If Not s.IconRect.IsEmpty AndAlso s.IconRect.Contains(clientPoint) Then Return HTSYSMENU

        If clientPoint.Y < _标题栏高度 Then
            For Each rect In _标题栏排除区域
                If rect.Contains(clientPoint) Then Return HTCLIENT
            Next
            Return HTCAPTION
        End If

        Dim result As Integer = HTCLIENT
        Dim args As New CustomHitTestEventArgs(clientPoint, result, s.HostForm)
        RaiseEvent CustomHitTest(Me, args)
        If args.OverrideResult.HasValue Then Return args.OverrideResult.Value
        Return result
    End Function

#End Region

#Region "NativeWindow 消息拦截器"

    Private Const WM_NCLBUTTONDOWN As Integer = &HA1
    Private Const WM_NCLBUTTONUP As Integer = &HA2
    Private Const WM_NCMOUSELEAVE As Integer = &H2A2
    Private Const WM_MOUSEMOVE As Integer = &H200
    Private Const WM_LBUTTONUP As Integer = &H202
    Private Const WM_MOUSELEAVE As Integer = &H2A3
    Private Const WM_CAPTURECHANGED As Integer = &H215
    Private Const WM_ENTERSIZEMOVE As Integer = &H231
    Private Const WM_EXITSIZEMOVE As Integer = &H232
    Private Const WM_SHOWWINDOW As Integer = &H18
    Private Const WM_CLOSE As Integer = &H10

    Friend Class WindowMessageInterceptor
        Inherits NativeWindow

        Private ReadOnly _owner As ThisIsYourWindow
        Private ReadOnly _state As PerFormState

        Public Sub New(owner As ThisIsYourWindow, state As PerFormState)
            _owner = owner
            _state = state
            Me.AssignHandle(state.HostForm.Handle)
        End Sub

        Private Shared Function 解析LParam坐标(lParam As IntPtr) As Point
            Dim v As Long = lParam.ToInt64()
            Dim x As Integer = CInt(v And &HFFFF)
            Dim y As Integer = CInt((v >> 16) And &HFFFF)
            If x > 32767 Then x -= 65536
            If y > 32767 Then y -= 65536
            Return New Point(x, y)
        End Function

        Protected Overrides Sub WndProc(ByRef m As Message)
            Select Case m.Msg

                Case WM_NCHITTEST
                    MyBase.WndProc(m)
                    Dim sysResult As Integer = m.Result.ToInt32()
                    Dim clientPt As Point = _state.HostForm.PointToClient(解析LParam坐标(m.LParam))
                    Dim hit As Integer = _owner.执行命中测试(_state, clientPt)

                    If hit = HTCLIENT AndAlso
                       sysResult >= HTLEFT AndAlso sysResult <= HTBOTTOMRIGHT AndAlso
                       _owner._允许调整大小 AndAlso
                       Not (IsZoomed(_state.HostForm.Handle) AndAlso _owner._最大化时隐藏调整边框) Then
                        hit = sysResult
                    End If

                    Dim oldHover As Integer = _state.HoverHit
                    _state.HoverHit = If(hit = HTCLOSE OrElse hit = HTMAXBUTTON OrElse hit = HTMINBUTTON, hit, HTNOWHERE)
                    If oldHover <> _state.HoverHit Then _owner.InvalidateCaption(_state.HostForm)
                    m.Result = New IntPtr(hit)
                    Return

                Case WM_NCCALCSIZE
                    If m.WParam <> IntPtr.Zero AndAlso IsZoomed(_state.HostForm.Handle) Then
                        Dim scr = Screen.FromHandle(_state.HostForm.Handle)
                        Dim wa = scr.WorkingArea
                        Dim r As RECT : r.Left = wa.Left : r.Top = wa.Top : r.Right = wa.Right : r.Bottom = wa.Bottom
                        Marshal.StructureToPtr(r, m.LParam, True)
                    End If
                    m.Result = IntPtr.Zero
                    Return

                Case WM_GETMINMAXINFO
                    MyBase.WndProc(m)
                    Dim scr = Screen.FromHandle(_state.HostForm.Handle)
                    Dim wa = scr.WorkingArea, sb = scr.Bounds
                    Dim info = Marshal.PtrToStructure(Of MINMAXINFO)(m.LParam)
                    info.ptMaxPosition = New Point(wa.X - sb.X, wa.Y - sb.Y)
                    info.ptMaxSize = New Point(wa.Width, wa.Height)
                    Marshal.StructureToPtr(info, m.LParam, True)
                    Return

                Case WM_SIZE
                    MyBase.WndProc(m)
                    If _owner._阴影模式 <> ShadowModeEnum.DWM Then _owner.切换动画样式(_state.HostForm.Handle, False)
                    _owner.RecalculateButtonBounds(_state)
                    _state.HostForm?.Invalidate()
                    _owner.更新阴影(_state)
                    Return

                Case WM_ACTIVATE
                    MyBase.WndProc(m)
                    Dim activated As Boolean = (CInt(m.WParam.ToInt64() And &HFFFF) <> 0)
                    _state.Activated = activated
                    _owner.触发激活状态改变(activated, _state.HostForm)
                    _state.HostForm?.Invalidate()
                    If activated Then _owner.更新阴影(_state)
                    Return

                Case WM_MOVE
                    MyBase.WndProc(m)
                    _owner.更新阴影(_state)
                    Return

                Case WM_ENTERSIZEMOVE
                    _state.IsInSizeMove = True
                    MyBase.WndProc(m)
                    Return

                Case WM_EXITSIZEMOVE
                    _state.IsInSizeMove = False
                    _owner.更新阴影(_state)
                    MyBase.WndProc(m)
                    Return

                Case WM_NCACTIVATE
                    m.Result = New IntPtr(1)
                    Return

                Case WM_NCPAINT
                    m.Result = IntPtr.Zero
                    Return

                Case WM_NCLBUTTONDOWN
                    Dim htDown As Integer = CInt(m.WParam.ToInt64())
                    If htDown = HTCLOSE OrElse htDown = HTMAXBUTTON OrElse htDown = HTMINBUTTON Then
                        _state.PressedHit = htDown
                        _state.HoverHit = htDown
                        _owner.InvalidateCaption(_state.HostForm)
                        SetCapture(_state.HostForm.Handle)
                        Return
                    End If
                    MyBase.WndProc(m)
                    Return

                Case WM_NCLBUTTONUP
                    _state.PressedHit = HTNOWHERE
                    _owner.InvalidateCaption(_state.HostForm)
                    MyBase.WndProc(m)
                    Return

                Case WM_NCMOUSELEAVE, WM_MOUSELEAVE
                    If _state.HoverHit <> HTNOWHERE Then
                        _state.HoverHit = HTNOWHERE
                        _owner.InvalidateCaption(_state.HostForm)
                    End If
                    MyBase.WndProc(m)
                    Return

                Case WM_MOUSEMOVE
                    If _state.PressedHit <> HTNOWHERE Then
                        Dim hit As Integer = _owner.执行命中测试(_state, 解析LParam坐标(m.LParam))
                        Dim newHover As Integer = If(hit = _state.PressedHit, hit, HTNOWHERE)
                        If newHover <> _state.HoverHit Then
                            _state.HoverHit = newHover
                            _owner.InvalidateCaption(_state.HostForm)
                        End If
                        Return
                    End If
                    MyBase.WndProc(m)
                    Return

                Case WM_LBUTTONUP
                    If _state.PressedHit <> HTNOWHERE Then
                        Dim released As Integer = _state.PressedHit
                        _state.PressedHit = HTNOWHERE
                        _state.HoverHit = HTNOWHERE
                        ReleaseCapture()
                        _owner.InvalidateCaption(_state.HostForm)
                        Dim hit As Integer = _owner.执行命中测试(_state, 解析LParam坐标(m.LParam))
                        If hit = released Then
                            Select Case released
                                Case HTCLOSE : _state.HostForm?.Close()
                                Case HTMAXBUTTON
                                    If _state.HostForm IsNot Nothing Then
                                        _owner.切换动画样式(_state.HostForm.Handle, True)
                                        _state.HostForm.WindowState = If(_state.HostForm.WindowState = FormWindowState.Maximized,
                                                                         FormWindowState.Normal, FormWindowState.Maximized)
                                    End If
                                Case HTMINBUTTON
                                    If _state.HostForm IsNot Nothing Then
                                        _owner.切换动画样式(_state.HostForm.Handle, True)
                                        _state.HostForm.WindowState = FormWindowState.Minimized
                                    End If
                            End Select
                        End If
                        Return
                    End If
                    MyBase.WndProc(m)
                    Return

                Case WM_CAPTURECHANGED
                    If _state.PressedHit <> HTNOWHERE Then
                        _state.PressedHit = HTNOWHERE
                        _state.HoverHit = HTNOWHERE
                        _owner.InvalidateCaption(_state.HostForm)
                    End If
                    MyBase.WndProc(m)
                    Return

                Case WM_SHOWWINDOW
                    If m.WParam <> IntPtr.Zero AndAlso _state.PendingFirstPaintRestore Then
                        ' 最终安全网：显示前确保窗口仍然处于完全透明状态且样式正确
                        Dim hWnd = _state.HostForm.Handle
                        Dim exStyle As Long = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64()
                        If (exStyle And WS_EX_LAYERED) = 0 Then
                            SetWindowLongPtr(hWnd, GWL_EXSTYLE, New IntPtr(exStyle Or WS_EX_LAYERED))
                        End If
                        SetLayeredWindowAttributes(hWnd, 0, 0, LWA_ALPHA)
                        If _owner._阴影模式 <> ShadowModeEnum.DWM Then
                            Dim st As Long = GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64()
                            If (st And WS_CAPTION) = WS_CAPTION Then
                                SetWindowLongPtr(hWnd, GWL_STYLE, New IntPtr(st And Not CLng(WS_CAPTION)))
                            End If
                        End If
                    End If
                    MyBase.WndProc(m)
                    If m.WParam <> IntPtr.Zero AndAlso _owner._显示动画模式 <> WindowShowAnimationMode.DWM Then
                        Try
                            Dim enable As Integer = 0
                            Dim unused = DwmSetWindowAttribute(_state.HostForm.Handle, DWMWA_TRANSITIONS_FORCEDISABLED, enable, 4)
                        Catch
                        End Try
                    End If
                    Return

                Case WM_CLOSE
                    If _owner._关闭动画模式 <> WindowCloseAnimationMode.DWM Then
                        Try
                            Dim disable As Integer = 1
                            Dim unused = DwmSetWindowAttribute(_state.HostForm.Handle, DWMWA_TRANSITIONS_FORCEDISABLED, disable, 4)
                        Catch
                        End Try
                    End If
                    If _owner._关闭动画模式 = WindowCloseAnimationMode.Win32 AndAlso Not _state.AnimatingClose Then
                        _state.AnimatingClose = True
                        _state.AnimatingShow = False
                        Dim frm = _state.HostForm
                        Dim origOpacity = frm.Opacity
                        Dim t As New Timer() With {.Interval = 15}
                        Dim elapsed As Integer = 0
                        Dim duration As Integer = _owner._动画持续时间
                        AddHandler t.Tick, Sub(s, ev)
                                               elapsed += 15
                                               If elapsed >= duration OrElse frm.IsDisposed Then
                                                   frm.Opacity = 0
                                                   t.Stop() : t.Dispose()
                                                   frm.Close()
                                                   If Not frm.IsDisposed Then
                                                       frm.Opacity = origOpacity
                                                       _state.AnimatingClose = False
                                                   End If
                                               Else
                                                   frm.Opacity = origOpacity * (1.0 - elapsed / CDbl(duration))
                                               End If
                                           End Sub
                        t.Start()
                        Return
                    End If
                    MyBase.WndProc(m)
                    Return

                Case WM_SYSCOMMAND
                    Dim cmd As Integer = CInt(m.WParam.ToInt64() And &HFFF0)
                    If cmd = SC_MINIMIZE OrElse cmd = SC_MAXIMIZE OrElse cmd = SC_RESTORE Then
                        _owner.切换动画样式(_state.HostForm.Handle, True)
                    End If
                    MyBase.WndProc(m)
                    Return

            End Select
            MyBase.WndProc(m)
        End Sub
    End Class

#End Region

End Class
