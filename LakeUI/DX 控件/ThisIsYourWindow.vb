Imports System.ComponentModel
Imports System.Drawing.Drawing2D
Imports System.Numerics
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports Vortice.Direct2D1
Imports Vortice.DirectWrite

''' <summary>
''' 无界面组件，用于完全自定义窗口的标题栏与边框外观，
''' 同时保留 Windows 原生的拖动、调整大小、最大化/最小化及贴靠行为。
''' 单个实例可同时附加到多个窗体，所有窗体共享同一套外观属性。
''' 在窗体的 Load 事件中调用 <see cref="Attach"/> 即可启用。
''' </summary>
<DesignerCategory("Component")>
<DefaultEvent("CaptionPaint")>
Public Class ThisIsYourWindow
    Implements IMessageFilter

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
    Private Const WM_WINDOWPOSCHANGED As Integer = &H47
    Private Const WM_PAINT As Integer = &HF
    Private Const WM_ERASEBKGND As Integer = &H14
    Private Const WM_KEYDOWN As Integer = &H100
    Private Const WM_SYSKEYDOWN As Integer = &H104
    Private Const WM_NCMOUSEMOVE As Integer = &HA0

    Private Const TPM_LEFTALIGN As Integer = &H0
    Private Const TPM_TOPALIGN As Integer = &H0
    Private Const TPM_RETURNCMD As Integer = &H100

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
    ' Use a standard non-client value without the HTHELP (21) system "Help" tooltip.
    Private Const HTFULLSCREEN As Integer = 18 ' HTBORDER
    Private Const HTNOWHERE As Integer = 0

    Private Const SWP_FRAMECHANGED As Integer = &H20
    Private Const SWP_NOMOVE As Integer = &H2
    Private Const SWP_NOSIZE As Integer = &H1
    Private Const SWP_NOZORDER As Integer = &H4
    Private Const SWP_NOOWNERZORDER As Integer = &H200

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
    Private Const WS_POPUP As Long = &H80000000L
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

    <DllImport("user32.dll")>
    Private Shared Function ValidateRect(hWnd As IntPtr, lpRect As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetSystemMenu(hWnd As IntPtr, bRevert As Boolean) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function TrackPopupMenuEx(hMenu As IntPtr, uFlags As UInteger, X As Integer, Y As Integer,
                                               hWnd As IntPtr, lptpm As IntPtr) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetWindowRect(hWnd As IntPtr, ByRef lpRect As RECT) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetClientRect(hWnd As IntPtr, ByRef lpRect As RECT) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetCursorPos(ByRef lpPoint As NATIVEPOINT) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetForegroundWindow() As IntPtr
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Private Structure OSVERSIONINFOEX
        Public dwOSVersionInfoSize As Integer
        Public dwMajorVersion As Integer
        Public dwMinorVersion As Integer
        Public dwBuildNumber As Integer
        Public dwPlatformId As Integer
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=128)>
        Public szCSDVersion As String
        Public wServicePackMajor As UShort
        Public wServicePackMinor As UShort
        Public wSuiteMask As UShort
        Public wProductType As Byte
        Public wReserved As Byte
    End Structure

    <DllImport("ntdll.dll")>
    Private Shared Function RtlGetVersion(ByRef versionInfo As OSVERSIONINFOEX) As Integer
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

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NATIVEPOINT
        Public X, Y As Integer
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
        Public CloseRect, MaxRect, MinRect, FullScreenRect, IconRect As Rectangle
        Public CaptionControlRect As Rectangle
        Public LastTitleTextDirtyRect As Rectangle = Rectangle.Empty
        Public TitleEllipsisSignature As Integer = Integer.MinValue
        Public TitleDisplayText As String = String.Empty
        Public ShadowForm As ShadowWindow
        Public IsInSizeMove As Boolean = False
        Public DeferredClientBoundsActive As Boolean = False
        Public DeferredBeginBounds As Rectangle = Rectangle.Empty
        Public AnimatingShow As Boolean = False
        Public AnimatingClose As Boolean = False
        Public LastClientSize As Size = Size.Empty
        ' 上一次记录的最小化状态：用于在 WM_SIZE 中检测"从最小化恢复"事件并强制刷新毛玻璃。
        Public WasMinimized As Boolean = False
        Public OriginalOpacity As Double = 1.0
        Public PendingFirstPaintRestore As Boolean = False
        Public IsFullScreen As Boolean = False
        Public FullScreenCaptionVisible As Boolean = False
        Public FullScreenCaptionHideTimer As Timer
        Public FullScreenOriginalStyle As Long
        Public FullScreenOriginalBounds As Rectangle = Rectangle.Empty
        Public FullScreenOriginalWindowState As FormWindowState = FormWindowState.Normal
        ' ── 布局缓存签名：仅当窗口宽度/按钮可见性/相关属性变化时重新计算按钮位置 ──
        Public LayoutSignature As Long = -1
        ' ── 毛玻璃 ──
        Public Renderer As D3D_BackdropSurfaceRenderer
        Public BackdropTimer As PrecisionTimer
        Public ChromeOverlays As List(Of ChromeOverlayControl)
        Public ChromeOverlayActive As Boolean
        Public ChromeOverlayRegions As List(Of Rectangle)
        Public ChromeOverlayRegionsSignature As Long = Long.MinValue
        ' ── D3D 资源 ──
        ' 窗口级 D3D compositor 统一持有图形资源；这里不再持有任何长期 D2D 字段。
        Public Sub New(form As Form)
            HostForm = form
            If form IsNot Nothing Then LastClientSize = ThisIsYourWindow.获取真实客户区尺寸(form)
        End Sub
    End Class

    Private ReadOnly _forms As New Dictionary(Of IntPtr, PerFormState)
    Private ReadOnly _pendingAttachHandlers As New Dictionary(Of Form, EventHandler)
    Private _消息过滤器已注册 As Boolean
    Private _首个附加窗体 As Form
    Private Shared ReadOnly _attachedFormsLock As New Object()
    Private Shared ReadOnly _attachedForms As New Dictionary(Of Form, ThisIsYourWindow)

    ' ── 绘制热路径共享缓存：避免每帧 New SolidBrush/Pen 造成 GC 压力 ──
    Private ReadOnly _共享画刷 As New SolidBrush(Color.Black)
    Private ReadOnly _共享画笔 As New Pen(Color.Black, 1.0F)
    Private _标题栏绑定控件 As Control
    Private _标题栏控件逻辑宽度 As Single
    Private _正在同步标题栏控件 As Boolean
    Private _标题栏控件原始父级 As Control
    Private _标题栏控件原始边界 As Rectangle
    Private _标题栏控件原始停靠 As DockStyle
    Private _标题栏控件原始锚定 As AnchorStyles
    Private _标题栏控件宿主窗体 As Form
    Private ReadOnly _useGpuChromeOverlay As Boolean = True

    Private Function 查找状态(form As Form) As PerFormState
        Dim s As PerFormState = Nothing
        If form IsNot Nothing AndAlso form.IsHandleCreated Then _forms.TryGetValue(form.Handle, s)
        Return s
    End Function

    Public Shared Function TryGetAttached(form As Form, ByRef owner As ThisIsYourWindow) As Boolean
        owner = Nothing
        If form Is Nothing Then Return False
        SyncLock _attachedFormsLock
            Return _attachedForms.TryGetValue(form, owner) AndAlso owner IsNot Nothing
        End SyncLock
    End Function

    Friend Shared Sub NotifyGpuFramePresented(form As Form)
        If form Is Nothing Then Return
        Dim owner As ThisIsYourWindow = Nothing
        If Not TryGetAttached(form, owner) OrElse owner Is Nothing Then Return
        owner.完成首帧还原(owner.查找状态(form))
    End Sub

    Friend Shared Sub NotifyGpuFrameNotPresented(form As Form)
        If form Is Nothing Then Return
        Dim owner As ThisIsYourWindow = Nothing
        If Not TryGetAttached(form, owner) OrElse owner Is Nothing Then Return
        owner.取消首帧等待(owner.查找状态(form))
    End Sub

    Friend Shared Function TryRenderAttachedChrome(context As D3D_PaintContext, targetForm As Form) As Boolean
        If context Is Nothing OrElse targetForm Is Nothing OrElse targetForm.IsDisposed Then Return False
        Dim owner As ThisIsYourWindow = Nothing
        If Not TryGetAttached(targetForm, owner) OrElse owner Is Nothing Then Return False
        Dim state = owner.查找状态(targetForm)
        If state Is Nothing Then Return False

        ' When child overlays are active they own the caption and border
        ' pixels. Do not render the same chrome into the Form surface, or the
        ' two coordinate spaces can blend and produce a shifted/self-sampled
        ' frame. The Form surface remains responsible for client content.
        If Not state.ChromeOverlayActive Then
            owner.RenderGpuWindow(context, targetForm)
        Else
            D3D_RenderDiagnostics.V5ChromeOverlayDuplicateSuppressed()
        End If
        owner.完成首帧还原(state)
        Return True
    End Function

    ''' <summary>
    ''' Renders the attached window visual into another GPU surface, including
    ''' the configured image/blur backdrop. This is used when a native Form is
    ''' selected as a V5 BackgroundSource; no HDC or screen capture is involved.
    ''' </summary>
    Friend Shared Function TryRenderAttachedSurface(context As D3D_PaintContext, targetForm As Form) As Boolean
        If context Is Nothing OrElse targetForm Is Nothing OrElse targetForm.IsDisposed Then Return False
        Dim owner As ThisIsYourWindow = Nothing
        If Not TryGetAttached(targetForm, owner) OrElse owner Is Nothing Then Return False
        Dim state = owner.查找状态(targetForm)
        If state Is Nothing Then Return False
        owner.RenderGpuWindow(context, targetForm)
        Return True
    End Function

    ''' <summary>
    ''' Draws only the attached window backdrop into a client surface.  Chrome
    ''' overlays own the caption and borders, so the Form surface still needs
    ''' an explicit backdrop pass for its client area.
    ''' </summary>
    Friend Shared Function TryRenderAttachedClientBackdrop(context As D3D_PaintContext,
                                                            targetForm As Form) As Boolean
        If context Is Nothing OrElse targetForm Is Nothing OrElse targetForm.IsDisposed Then Return False
        Dim owner As ThisIsYourWindow = Nothing
        If Not TryGetAttached(targetForm, owner) OrElse owner Is Nothing Then Return False
        Dim state = owner.查找状态(targetForm)
        If state Is Nothing OrElse Not state.ChromeOverlayActive Then Return False
        Return owner.RenderGpuClientBackdrop(context, targetForm)
    End Function

    Friend Shared Function HasAttachedSurface(targetForm As Form) As Boolean
        If targetForm Is Nothing OrElse targetForm.IsDisposed Then Return False
        Dim owner As ThisIsYourWindow = Nothing
        Return TryGetAttached(targetForm, owner) AndAlso owner IsNot Nothing
    End Function

    Friend Shared Function AttachedBackdropCoversClient(form As Form) As Boolean
        If form Is Nothing OrElse form.IsDisposed Then Return False
        Dim owner As ThisIsYourWindow = Nothing
        If Not TryGetAttached(form, owner) OrElse owner Is Nothing Then Return False
        Return owner.BackdropCoversClient(form)
    End Function

    Private Function BackdropCoversClient(form As Form) As Boolean
        Dim s = 查找状态(form)
        If Not 毛玻璃允许用于窗体(s) Then Return False
        Select Case _毛玻璃模式
            Case BackdropModeEnum.Image
                Return _毛玻璃图片 IsNot Nothing
            Case BackdropModeEnum.Auto
                Return s IsNot Nothing AndAlso s.Renderer IsNot Nothing AndAlso s.Renderer.HasFrame
            Case Else
                Return False
        End Select
    End Function

    Private Function 是首个附加窗体(form As Form) As Boolean
        Return form IsNot Nothing AndAlso ReferenceEquals(form, _首个附加窗体)
    End Function

    ''' <summary>
    ''' Owned modal dialogs deactivate their owner at the Win32 level, but the
    ''' owner is still the active application surface from a visual perspective.
    ''' Keep the owner's backdrop/chrome colors unchanged while such a dialog is
    ''' active; switching to the inactive tint would darken every mapped child.
    ''' </summary>
    Private Shared Function 由自有对话框保持激活视觉(form As Form) As Boolean
        If form Is Nothing OrElse form.IsDisposed Then Return False

        Dim activeForm As Form = Nothing
        Try
            Dim foregroundHandle = GetForegroundWindow()
            If foregroundHandle <> IntPtr.Zero Then
                Dim activeControl = Control.FromHandle(foregroundHandle)
                If activeControl Is Nothing Then Return False
                activeForm = TryCast(activeControl, Form)
                If activeForm Is Nothing Then activeForm = activeControl.FindForm()
            Else
                activeForm = Form.ActiveForm
            End If
        Catch
            activeForm = Nothing
        End Try
        If activeForm Is Nothing OrElse activeForm Is form OrElse activeForm.IsDisposed Then Return False

        Dim current As Form = activeForm
        For i As Integer = 0 To 16
            Dim owner = current.Owner
            If owner Is Nothing Then Return False
            If ReferenceEquals(owner, form) Then Return True
            current = owner
        Next
        Return False
    End Function

    Private Shared Function 视觉上保持激活(form As Form, activated As Boolean) As Boolean
        Return activated OrElse 由自有对话框保持激活视觉(form)
    End Function

    Private Function 全屏允许用于窗体(s As PerFormState) As Boolean
        Return s IsNot Nothing AndAlso 是首个附加窗体(s.HostForm)
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

    ''' <summary>
    ''' 毛玻璃 / 亚克力背景模式。
    ''' None — 关闭。
    ''' Auto — 抓取窗口背后的桌面区域并模糊后绘制为窗体背景。默认仅在事件驱动时刷新（移动或调整大小结束 / 显示），
    '''        系统截图工具能截到本窗口；如需常态周期刷新，请同时开启 <see cref="BackdropExcludeFromCapture"/>，
    '''        此时启用 WDA_EXCLUDEFROMCAPTURE 防止抓自身（要求 Win10 build 19041+），副作用：系统截图 / 录屏均无法捕获本窗口。
    ''' Image — 使用 <see cref="BackdropImage"/> 作为虚拟背景源（按 cover 撑满窗口）后再做模糊；
    '''         不抓屏、不影响系统截图，可在任意 Windows 版本工作。
    ''' CaptionOnly — 与 Auto 类似但仅对标题栏区域抓屏 / 模糊 / 绘制；
    '''         由于抓屏与模糊数据量大幅减少，性能开销远低于 Auto。
    ''' </summary>
    Public Enum BackdropModeEnum
        None = 0
        Auto = 1
        Image = 2
        CaptionOnly = 3
    End Enum

#End Region

#Region "OS 检测"

    Private Shared _backdropSupportedCached As Integer = -1

    ''' <summary>当前 OS 是否支持真正的"不含自身"抓屏（Win10 build 19041+）。</summary>
    <Browsable(False)>
    Public Shared ReadOnly Property IsBackdropSupported As Boolean
        Get
            Dim v As Integer = _backdropSupportedCached
            If v = -1 Then
                Dim info As New OSVERSIONINFOEX With {
                    .dwOSVersionInfoSize = Marshal.SizeOf(Of OSVERSIONINFOEX)()
                }
                Try
                    If RtlGetVersion(info) = 0 Then
                        v = If(info.dwMajorVersion > 10 OrElse
                               (info.dwMajorVersion = 10 AndAlso info.dwBuildNumber >= 19041), 1, 0)
                    Else
                        v = 0
                    End If
                Catch
                    v = 0
                End Try
                _backdropSupportedCached = v
            End If
            Return v = 1
        End Get
    End Property

#End Region

#Region "通用辅助"

    Private Shared Function 取Dpi缩放(control As Control) As Single
        If control IsNot Nothing AndAlso Not control.IsDisposed Then
            Return D3D_DpiContext.FromControl(control).Scale
        End If
        Return 1.0F
    End Function

    Private Shared Function 缩放逻辑尺寸(control As Control, value As Integer) As Integer
        Return CInt(Math.Round(value * 取Dpi缩放(control), MidpointRounding.AwayFromZero))
    End Function

    Private Shared Function 缩放逻辑尺寸(control As Control, value As Single) As Single
        Return value * 取Dpi缩放(control)
    End Function

    Private Shared Function 缩放逻辑内边距(control As Control, value As Padding) As Padding
        Return New Padding(缩放逻辑尺寸(control, value.Left),
                           缩放逻辑尺寸(control, value.Top),
                           缩放逻辑尺寸(control, value.Right),
                           缩放逻辑尺寸(control, value.Bottom))
    End Function

    Private Shared Function 规范化内边距(value As Padding) As Padding
        Return New Padding(Math.Max(0, value.Left),
                           Math.Max(0, value.Top),
                           Math.Max(0, value.Right),
                           Math.Max(0, value.Bottom))
    End Function

    Private Shared Function 应用内边距(bounds As Rectangle, padding As Padding) As Rectangle
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return Rectangle.Empty

        Dim left As Integer = Math.Min(bounds.Width, Math.Max(0, padding.Left))
        Dim top As Integer = Math.Min(bounds.Height, Math.Max(0, padding.Top))
        Dim width As Integer = Math.Max(0, bounds.Width - left - Math.Max(0, padding.Right))
        Dim height As Integer = Math.Max(0, bounds.Height - top - Math.Max(0, padding.Bottom))
        Return New Rectangle(bounds.X + left, bounds.Y + top, width, height)
    End Function

    Private Function 取缩放边框厚度(control As Control) As Integer
        Return Math.Max(0, 缩放逻辑尺寸(control, _边框厚度))
    End Function

    Private Function 取缩放标题栏高度(control As Control) As Integer
        Return Math.Max(0, 缩放逻辑尺寸(control, _标题栏高度))
    End Function

    Private Function 取缩放标题栏底部横线高度(control As Control) As Integer
        Return Math.Max(0, 缩放逻辑尺寸(control, _标题栏底部横线高度))
    End Function

    Private Function 取缩放标题栏总高度(control As Control) As Integer
        Return 取缩放边框厚度(control) + 取缩放标题栏高度(control)
    End Function

    Private Sub 标题栏绑定控件_Disposed(sender As Object, e As EventArgs)
        If Not ReferenceEquals(sender, _标题栏绑定控件) Then Return
        _标题栏绑定控件 = Nothing
        _标题栏控件宿主窗体 = Nothing
        _标题栏控件原始父级 = Nothing
        _标题栏控件逻辑宽度 = 0.0F
        使布局失效()
        通知标题栏重绘()
    End Sub

    Private Sub 标题栏绑定控件_SizeChanged(sender As Object, e As EventArgs)
        If _正在同步标题栏控件 OrElse Not ReferenceEquals(sender, _标题栏绑定控件) Then Return
        Dim scaleSource As Control = If(_标题栏控件宿主窗体, _标题栏绑定控件)
        _标题栏控件逻辑宽度 = _标题栏绑定控件.Width / Math.Max(0.01F, 取Dpi缩放(scaleSource))
        使布局失效()
        通知标题栏重绘()
    End Sub

    Private Sub 恢复标题栏控件原始布局()
        Dim ctrl = _标题栏绑定控件
        If ctrl Is Nothing OrElse ctrl.IsDisposed Then Return

        _正在同步标题栏控件 = True
        Try
            ctrl.Dock = DockStyle.None
            If ctrl.Parent IsNot _标题栏控件原始父级 Then
                ctrl.Parent?.Controls.Remove(ctrl)
                If _标题栏控件原始父级 IsNot Nothing AndAlso Not _标题栏控件原始父级.IsDisposed Then
                    _标题栏控件原始父级.Controls.Add(ctrl)
                End If
            End If
            ctrl.Dock = _标题栏控件原始停靠
            ctrl.Anchor = _标题栏控件原始锚定
            ctrl.Bounds = _标题栏控件原始边界
        Finally
            _正在同步标题栏控件 = False
        End Try
    End Sub

    Private Sub 解除标题栏控件绑定()
        Dim ctrl = _标题栏绑定控件
        If ctrl IsNot Nothing Then
            Try : RemoveHandler ctrl.Disposed, AddressOf 标题栏绑定控件_Disposed : Catch : End Try
            Try : RemoveHandler ctrl.SizeChanged, AddressOf 标题栏绑定控件_SizeChanged : Catch : End Try
            恢复标题栏控件原始布局()
        End If

        _标题栏绑定控件 = Nothing
        _标题栏控件宿主窗体 = Nothing
        _标题栏控件原始父级 = Nothing
        _标题栏控件原始边界 = Rectangle.Empty
        _标题栏控件逻辑宽度 = 0.0F
        For Each state In _forms.Values
            state.CaptionControlRect = Rectangle.Empty
            state.LayoutSignature = -1
        Next
    End Sub

    Private Sub 同步所有标题栏绑定控件布局()
        If _标题栏绑定控件 Is Nothing OrElse _标题栏绑定控件.IsDisposed Then Return

        Dim hostState = 查找状态(_标题栏控件宿主窗体)
        If hostState Is Nothing Then
            hostState = _forms.Values.FirstOrDefault()
            _标题栏控件宿主窗体 = hostState?.HostForm
        End If

        For Each state In _forms.Values
            state.LayoutSignature = -1
            RecalculateButtonBounds(state)
        Next
    End Sub

    Private Sub 同步标题栏绑定控件布局(s As PerFormState)
        If s Is Nothing OrElse s.HostForm Is Nothing Then Return
        Dim ctrl = _标题栏绑定控件
        If ctrl Is Nothing OrElse ctrl.IsDisposed OrElse
           Not ReferenceEquals(s.HostForm, _标题栏控件宿主窗体) OrElse
           (s.IsFullScreen AndAlso Not s.FullScreenCaptionVisible) Then
            s.CaptionControlRect = Rectangle.Empty
            If ctrl IsNot Nothing AndAlso Not ctrl.IsDisposed AndAlso
               ReferenceEquals(s.HostForm, _标题栏控件宿主窗体) Then
                _正在同步标题栏控件 = True
                Try
                    If ctrl.Bounds <> Rectangle.Empty Then ctrl.SetBounds(0, 0, 0, 0)
                Finally
                    _正在同步标题栏控件 = False
                End Try
            End If
            Return
        End If

        Dim captionRect = 获取标题栏布局矩形(s.HostForm)
        If captionRect.Width <= 0 OrElse captionRect.Height <= 0 Then
            s.CaptionControlRect = Rectangle.Empty
            _正在同步标题栏控件 = True
            Try
                Dim hiddenBounds As New Rectangle(captionRect.X, captionRect.Y, 0, 0)
                If ctrl.Parent IsNot s.HostForm Then s.HostForm.Controls.Add(ctrl)
                If ctrl.Bounds <> hiddenBounds Then ctrl.SetBounds(hiddenBounds.X, hiddenBounds.Y, hiddenBounds.Width, hiddenBounds.Height)
            Finally
                _正在同步标题栏控件 = False
            End Try
            Return
        End If

        Dim leadingEdge As Integer = captionRect.Left
        If Not s.IconRect.IsEmpty Then
            Dim iconPadding As Padding = 缩放逻辑内边距(s.HostForm, _图标内边距)
            leadingEdge = s.IconRect.Right + iconPadding.Right
        ElseIf _按钮位置 = ButtonPositionEnum.Left Then
            leadingEdge = Math.Max(Math.Max(s.CloseRect.Right, s.FullScreenRect.Right),
                                   Math.Max(s.MaxRect.Right, s.MinRect.Right))
        End If

        Dim trailingEdge As Integer = captionRect.Right
        If _按钮位置 = ButtonPositionEnum.Right Then
            trailingEdge = Math.Min(s.CloseRect.Left, Math.Min(
                If(s.FullScreenRect.IsEmpty, s.CloseRect.Left, s.FullScreenRect.Left),
                Math.Min(
                If(s.MaxRect.IsEmpty, s.CloseRect.Left, s.MaxRect.Left),
                If(s.MinRect.IsEmpty, s.CloseRect.Left, s.MinRect.Left))))
        End If

        Dim x As Integer = leadingEdge
        Dim y As Integer = captionRect.Top
        Dim desiredWidth As Integer = Math.Max(0, CInt(Math.Round(_标题栏控件逻辑宽度 * 取Dpi缩放(s.HostForm), MidpointRounding.AwayFromZero)))
        Dim width As Integer = Math.Max(0, Math.Min(desiredWidth, trailingEdge - x))
        Dim height As Integer = captionRect.Height
        s.CaptionControlRect = New Rectangle(x, y, width, height)

        _正在同步标题栏控件 = True
        Try
            If ctrl.Parent IsNot s.HostForm Then s.HostForm.Controls.Add(ctrl)
            If ctrl.Dock <> DockStyle.None Then ctrl.Dock = DockStyle.None
            Dim desiredAnchor = AnchorStyles.Top Or AnchorStyles.Left
            If ctrl.Anchor <> desiredAnchor Then ctrl.Anchor = desiredAnchor
            Dim desiredBounds As New Rectangle(x, y, width, height)
            If ctrl.Bounds <> desiredBounds Then ctrl.SetBounds(desiredBounds.X, desiredBounds.Y, desiredBounds.Width, desiredBounds.Height)
            If ctrl.Parent IsNot Nothing AndAlso ctrl.Parent.Controls.GetChildIndex(ctrl) > 0 Then
                ctrl.BringToFront()
            End If
        Finally
            _正在同步标题栏控件 = False
        End Try
    End Sub

    Private Shared Function 获取真实客户区尺寸(form As Form) As Size
        If form Is Nothing OrElse form.IsDisposed Then Return Size.Empty

        If form.IsHandleCreated Then
            Dim rect As RECT
            If GetClientRect(form.Handle, rect) Then
                Return New Size(Math.Max(0, rect.Right - rect.Left), Math.Max(0, rect.Bottom - rect.Top))
            End If
        End If

        Return form.ClientSize
    End Function

    Private Shared Function 获取真实客户区矩形(form As Form) As Rectangle
        Dim size = 获取真实客户区尺寸(form)
        If size.Width <= 0 OrElse size.Height <= 0 Then Return Rectangle.Empty
        Return New Rectangle(Point.Empty, size)
    End Function

    ''' <summary>
    ''' 返回窗口当前是否仍由 Win32 视为最大化。
    ''' 尺寸移动优化会暂时跳过 WinForms 的 WM_SIZE 默认处理，
    ''' 此时 Form.WindowState 可能晚于原生窗口状态更新。
    ''' </summary>
    Private Shared Function 窗口当前已最大化(form As Form) As Boolean
        If form Is Nothing OrElse form.IsDisposed Then Return False
        If form.IsHandleCreated Then Return IsZoomed(form.Handle)
        Return form.WindowState = FormWindowState.Maximized
    End Function

    Private Sub 通知重绘(Optional immediate As Boolean = True)
        For Each s In _forms.Values
            Dim frm = s.HostForm
            If frm IsNot Nothing AndAlso Not frm.IsDisposed AndAlso frm.IsHandleCreated Then
                ' The attached Form itself is a native host, not a V5 source.
                ' Mark its optional background surface dirty for cross-form
                ' consumers, then present chrome overlays directly.
                D3D_ControlSurfaceRegistry.MarkDirty(frm,
                                                      获取真实客户区矩形(frm),
                                                      requestConsumers:=True)
                请求Chrome渲染(s, includeBorders:=True)
            End If
        Next
    End Sub

    Friend Shared Sub 请求GPU渲染(control As Control, dirtyRect As Rectangle, Optional immediate As Boolean = False)
        If control Is Nothing OrElse control.IsDisposed Then Return

        Dim form = TryCast(control, Form)
        If form IsNot Nothing Then
            Dim owner As ThisIsYourWindow = Nothing
            If TryGetAttached(form, owner) AndAlso owner IsNot Nothing Then
                Dim state = owner.查找状态(form)
                If state Is Nothing Then Return
                D3D_ControlSurfaceRegistry.MarkDirty(form, dirtyRect, requestConsumers:=True)
                Dim captionBottom = owner.取缩放标题栏总高度(form)
                Dim captionOnly = dirtyRect.Width > 0 AndAlso dirtyRect.Height > 0 AndAlso
                                  dirtyRect.Top < captionBottom AndAlso dirtyRect.Bottom <= captionBottom
                owner.请求Chrome渲染(state, includeBorders:=Not captionOnly)
                Return
            End If
        End If

        D3D_InvalidationRouter.RequestRender(control, dirtyRect)
    End Sub

    Private Sub 使布局失效(Optional recalculate As Boolean = True)
        For Each s In _forms.Values
            s.LayoutSignature = -1
            If recalculate Then RecalculateButtonBounds(s)
        Next
    End Sub

    Private Sub 通知标题栏重绘(Optional immediate As Boolean = True)
        For Each s In _forms.Values
            请求Chrome渲染(s, includeBorders:=True)
        Next
    End Sub

    Private Sub 请求Chrome渲染(s As PerFormState, includeBorders As Boolean)
        If s Is Nothing OrElse s.HostForm Is Nothing OrElse s.HostForm.IsDisposed Then Return
        If s.ChromeOverlayActive AndAlso s.ChromeOverlays IsNot Nothing Then
            If includeBorders Then
                For Each overlay In s.ChromeOverlays
                    If overlay Is Nothing OrElse overlay.IsDisposed OrElse Not overlay.Visible Then Continue For
                    D3D_V5Presentation.RequestRender(overlay, New Rectangle(Point.Empty, overlay.ClientSize))
                Next
            Else
                Dim captionOverlay = 获取CaptionOverlay(s)
                If captionOverlay IsNot Nothing AndAlso Not captionOverlay.IsDisposed AndAlso captionOverlay.Visible Then
                    D3D_V5Presentation.RequestRender(captionOverlay, New Rectangle(Point.Empty, captionOverlay.ClientSize))
                End If
            End If
            Return
        End If

        ' A native Form is not itself a V5 presentation source. Keep this
        ' fallback for custom hosts that explicitly implement the contract.
        If TypeOf s.HostForm Is V5_IGpuPresentationSource Then
            D3D_V5Presentation.RequestRender(s.HostForm, 获取真实客户区矩形(s.HostForm))
        End If
    End Sub

    Private Function 当前使用圆角模式(s As PerFormState) As Boolean
        If s Is Nothing OrElse s.IsFullScreen OrElse s.HostForm Is Nothing Then Return False
        ' Windows 11 在最大化/贴靠状态下不会绘制窗口圆角；GPU 自绘边框必须遵循同一规则。
        If 窗口当前已最大化(s.HostForm) Then Return False
        Return DwmWindowStyle.IsCornerModeSupported AndAlso
               DwmWindowStyle.GetCornerRadiusLogical(_窗口圆角模式) > 0.0F
    End Function

    Private Sub 应用Dwm边框颜色(hWnd As IntPtr)
        ' LakeUI 自己绘制窗口边框。DWM 的独立边框与 GPU 自绘边框同时存在时，
        ' 圆角像素会出现双重抗锯齿/颜色泄漏，因此统一禁止系统边框。
        Dim borderValue As Integer = DWMWA_COLOR_NONE
        Dim unused = DwmSetWindowAttribute(hWnd, DWMWA_BORDER_COLOR, borderValue, 4)
    End Sub

    Private Sub 应用Dwm窗口属性(hWnd As IntPtr, Optional disableTransitions As Boolean = False)
        Try
            Dim pref As Integer = CInt(_窗口圆角模式)
            Dim unused1 = DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, pref, 4)
            应用Dwm边框颜色(hWnd)
            Dim margins As MARGINS
            If _阴影模式 = ShadowModeEnum.DWM Then margins.Bottom = 1
            Dim unused3 = DwmExtendFrameIntoClientArea(hWnd, margins)
            If disableTransitions Then
                Dim disable As Integer = 1
                Dim unused4 = DwmSetWindowAttribute(hWnd, DWMWA_TRANSITIONS_FORCEDISABLED, disable, 4)
            End If
        Catch
        End Try
    End Sub

    ''' <summary>
    ''' 计算毛玻璃 Renderer 实际需要抓取 / 渲染的桌面区域。
    ''' Auto / Image — 整个窗口；CaptionOnly — 仅标题栏区域，可显著减小抓屏与模糊计算量。
    ''' </summary>
    Friend Function 获取毛玻璃捕获区域(form As Form) As Rectangle
        If form Is Nothing Then Return Rectangle.Empty
        Dim b As Rectangle = form.Bounds
        If _毛玻璃模式 = BackdropModeEnum.CaptionOnly Then
            Dim ch As Integer = Math.Max(1, 取缩放标题栏总高度(form))
            If ch > b.Height Then ch = b.Height
            Return New Rectangle(b.X, b.Y, b.Width, ch)
        End If
        Return b
    End Function

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

    Private Function 毛玻璃当前启用(s As PerFormState) As Boolean
        Return 毛玻璃允许用于窗体(s) AndAlso s.Renderer IsNot Nothing
    End Function

    Private Sub 请求毛玻璃帧(s As PerFormState,
                         Optional commitAverage As Boolean = True,
                         Optional forceImageMode As Boolean = False)
        If s Is Nothing OrElse s.HostForm Is Nothing OrElse s.HostForm.IsDisposed Then Return
        If s.Renderer Is Nothing Then Return
        If _毛玻璃模式 = BackdropModeEnum.Image AndAlso s.Renderer.HasFrame AndAlso Not forceImageMode Then Return
        s.Renderer.RequestFrame(获取毛玻璃捕获区域(s.HostForm), commitAverage)
    End Sub

    Private Function 可跳过WMSize客户区刷新(s As PerFormState, clientSizeChanged As Boolean) As Boolean
        If clientSizeChanged Then Return False
        If Not 毛玻璃当前启用(s) Then Return False
        If _毛玻璃模式 <> BackdropModeEnum.Image Then Return False
        Return s.Renderer IsNot Nothing AndAlso s.Renderer.IsImageSource AndAlso s.Renderer.HasFrame
    End Function

    Private Function 尺寸移动刷新优化当前启用(s As PerFormState) As Boolean
        Return _尺寸移动刷新优化启用 AndAlso s IsNot Nothing
    End Function

    Private Function 毛玻璃允许用于窗体(s As PerFormState) As Boolean
        Return _毛玻璃模式 <> BackdropModeEnum.None AndAlso
               s IsNot Nothing AndAlso
               (Not _毛玻璃仅首个窗口 OrElse 是首个附加窗体(s.HostForm))
    End Function

    Private Sub 开始延迟客户区坐标上报(s As PerFormState)
        If Not 尺寸移动刷新优化当前启用(s) Then Return
        s.DeferredClientBoundsActive = True
        s.DeferredBeginBounds = 获取窗口屏幕矩形(s.HostForm)
        s.BackdropTimer?.Stop()
    End Sub

    Private Shared Function 获取窗口屏幕矩形(form As Form) As Rectangle
        If form Is Nothing OrElse Not form.IsHandleCreated Then Return Rectangle.Empty
        Dim r As RECT
        If GetWindowRect(form.Handle, r) Then
            Return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom)
        End If
        Return form.Bounds
    End Function

    Private Sub 提交延迟客户区坐标上报(s As PerFormState)
        If s Is Nothing OrElse s.HostForm Is Nothing OrElse s.HostForm.IsDisposed Then Return
        If Not s.DeferredClientBoundsActive Then Return
        s.DeferredClientBoundsActive = False

        Dim boundsChanged As Boolean = False
        Dim sizeChanged As Boolean = False
        If s.HostForm.IsHandleCreated Then
            Dim currentBounds As Rectangle = 获取窗口屏幕矩形(s.HostForm)
            boundsChanged = (currentBounds <> s.DeferredBeginBounds)
            sizeChanged = (currentBounds.Size <> s.DeferredBeginBounds.Size)
            ' WM_SIZE 在尺寸移动期间被延迟处理；无论边界是否变化，都要让
            ' WinForms 重新读取原生窗口状态，否则最大化拖回窗口化后
            ' Form.WindowState 可能仍为 Maximized。
            更新控件边界缓存(s.HostForm)
        End If

        s.DeferredBeginBounds = Rectangle.Empty
        s.LayoutSignature = -1
        If _阴影模式 <> ShadowModeEnum.DWM AndAlso s.HostForm.IsHandleCreated Then
            切换动画样式(s.HostForm.Handle, False)
            ' 切换动画样式只改窗口样式位；这里补一次 frame changed，
            ' 让恢复后的 WS_THICKFRAME / 非客户区命中区域立即生效。
            SetWindowPos(s.HostForm.Handle, IntPtr.Zero, 0, 0, 0, 0,
                         CUInt(SWP_FRAMECHANGED Or SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOZORDER Or SWP_NOOWNERZORDER))
        End If
        RecalculateButtonBounds(s)
        更新阴影(s)
        Dim requestBackdropFrame As Boolean = boundsChanged AndAlso
                                             毛玻璃当前启用(s) AndAlso
                                             (_毛玻璃模式 <> BackdropModeEnum.Image OrElse sizeChanged)
        If requestBackdropFrame Then
            请求毛玻璃帧(s, True, forceImageMode:=sizeChanged)
        ElseIf sizeChanged Then
            请求GPU渲染(s.HostForm, 获取真实客户区矩形(s.HostForm))
        End If
        重置毛玻璃Tick(s)
    End Sub

    Private Sub 同步尺寸移动刷新优化状态()
        For Each s In _forms.Values.ToList()
            If 尺寸移动刷新优化当前启用(s) Then
                If s.IsInSizeMove AndAlso Not s.DeferredClientBoundsActive Then
                    开始延迟客户区坐标上报(s)
                End If
            ElseIf s.DeferredClientBoundsActive Then
                提交延迟客户区坐标上报(s)
            End If
        Next
    End Sub

    Private Shared Sub 更新控件边界缓存(form As Form)
        If form Is Nothing Then Return
        Static updateBoundsMethod As MethodInfo = GetType(Control).GetMethod("UpdateBounds", BindingFlags.Instance Or BindingFlags.NonPublic, Nothing, Type.EmptyTypes, Nothing)
        updateBoundsMethod?.Invoke(form, Nothing)
    End Sub

    Private Sub 宿主窗口_Paint(sender As Object, e As PaintEventArgs)
        Dim frm = TryCast(sender, Form)
        If frm Is Nothing Then Return
        Dim s = 查找状态(frm)
        If s IsNot Nothing AndAlso s.ChromeOverlayActive Then
            完成首帧还原(s)
            Return
        End If
        If TryPaintWindowChrome(e, frm) Then
            完成首帧还原(s)
        ElseIf s IsNot Nothing AndAlso s.PendingFirstPaintRestore Then
            取消首帧等待(s)
        End If
    End Sub

    Friend Sub 完成首帧还原(s As PerFormState)
        If s Is Nothing OrElse Not s.PendingFirstPaintRestore Then Return
        s.PendingFirstPaintRestore = False
        If s.AnimatingShow Then
            开始渐入动画(s)
        ElseIf s.HostForm IsNot Nothing AndAlso Not s.HostForm.IsDisposed AndAlso s.HostForm.IsHandleCreated Then
            Dim alphaByte As Byte = CByte(Math.Min(255, Math.Max(0, CInt(Math.Round(s.OriginalOpacity * 255)))))
            SetLayeredWindowAttributes(s.HostForm.Handle, 0, alphaByte, LWA_ALPHA)
        End If
    End Sub

    Friend Sub 取消首帧等待(s As PerFormState)
        If s Is Nothing OrElse Not s.PendingFirstPaintRestore Then Return
        s.PendingFirstPaintRestore = False
        s.AnimatingShow = False
        If s.HostForm Is Nothing OrElse s.HostForm.IsDisposed OrElse Not s.HostForm.IsHandleCreated Then Return

        Dim alphaByte As Byte = CByte(Math.Min(255, Math.Max(0, CInt(Math.Round(s.OriginalOpacity * 255)))))
        SetLayeredWindowAttributes(s.HostForm.Handle, 0, alphaByte, LWA_ALPHA)
        If _阴影模式 = ShadowModeEnum.Layer AndAlso s.ShadowForm IsNot Nothing Then
            s.ShadowForm.SetGlobalAlpha(255)
        End If
    End Sub

    Private Sub 宿主窗口_FormClosed(sender As Object, e As FormClosedEventArgs)
        Dim frm = TryCast(sender, Form)
        If frm IsNot Nothing Then Detach(frm)
    End Sub

    Private Sub 宿主窗口_HandleDestroyed(sender As Object, e As EventArgs)
        Dim frm = TryCast(sender, Form)
        If frm Is Nothing Then Return

        If frm.RecreatingHandle AndAlso Not frm.IsDisposed Then
            释放当前句柄附加状态(frm, removeAttachedRegistration:=False, removePendingAttach:=False)
            安排句柄创建后附加(frm)
            Return
        End If

        Detach(frm)
    End Sub

    Private Sub 宿主窗口_VisibleChanged(sender As Object, e As EventArgs)
        Dim frm = TryCast(sender, Form)
        If frm Is Nothing Then Return
        Dim s = 查找状态(frm)
        If s Is Nothing Then Return

        If frm.Visible Then
            If Not s.AnimatingClose Then 更新阴影(s)
        Else
            销毁阴影(s)
        End If
        更新ChromeOverlays(s)
    End Sub

    ''' <summary>
    ''' 宿主窗体 Font 改变时：当 <see cref="TitleFont"/> 未单独设置时，标题文字使用窗体 Font，
    ''' 此处需要立即让缓存的 IDWriteTextFormat 失效（不同字号 / 字族对应不同实例）并重绘标题栏。
    ''' </summary>
    Private Sub 宿主窗口_FontChanged(sender As Object, e As EventArgs)
        Dim frm = TryCast(sender, Form)
        If frm Is Nothing Then Return
        Dim s = 查找状态(frm)
        If s Is Nothing Then Return
        D3D_RenderCore.InvalidateExistingTextResources(frm)
        InvalidateTitleText(s, True)
    End Sub

    Private Sub HostForm_TextChanged(sender As Object, e As EventArgs)
        Dim frm = TryCast(sender, Form)
        If frm Is Nothing Then Return
        Dim s = 查找状态(frm)
        If s Is Nothing Then Return
        InvalidateTitleText(s, True)
    End Sub

    Private Sub 宿主窗口_StyleChanged(sender As Object, e As EventArgs)
        Dim frm = TryCast(sender, Form)
        If frm Is Nothing Then Return
        Dim s = 查找状态(frm)
        If s Is Nothing Then Return

        s.LayoutSignature = -1
        RecalculateButtonBounds(s)
        InvalidateCaption(frm, True)
    End Sub

    Private Sub InvalidateTitleText(s As PerFormState, Optional immediate As Boolean = False)
        If s Is Nothing OrElse s.HostForm Is Nothing OrElse s.HostForm.IsDisposed OrElse Not s.HostForm.IsHandleCreated Then Return
        RecalculateButtonBounds(s)
        Dim newDirty As Rectangle = 获取标题文字脏区(s)
        Dim dirty As Rectangle = 合并脏区(s.LastTitleTextDirtyRect, newDirty)
        s.LastTitleTextDirtyRect = newDirty
        If dirty.Width > 0 AndAlso dirty.Height > 0 Then
            D3D_ControlSurfaceRegistry.MarkDirty(s.HostForm, dirty, requestConsumers:=True)
            If s.ChromeOverlayActive AndAlso s.ChromeOverlays IsNot Nothing Then
                Dim captionOverlay = 获取CaptionOverlay(s)
                If captionOverlay IsNot Nothing AndAlso captionOverlay.Visible Then
                    D3D_V5Presentation.RequestRender(captionOverlay,
                                                      New Rectangle(Point.Empty, captionOverlay.ClientSize))
                End If
            ElseIf TypeOf s.HostForm Is V5_IGpuPresentationSource Then
                D3D_V5Presentation.RequestRender(s.HostForm, dirty)
            End If
        End If
    End Sub

    Private Sub 通知标题文字重绘(Optional immediate As Boolean = False)
        For Each s In _forms.Values
            InvalidateTitleText(s, immediate)
        Next
    End Sub

    Private Shared Function 合并脏区(a As Rectangle, b As Rectangle) As Rectangle
        If a.Width <= 0 OrElse a.Height <= 0 Then Return b
        If b.Width <= 0 OrElse b.Height <= 0 Then Return a
        Return Rectangle.Union(a, b)
    End Function

    Private Sub 使标题字体资源失效()
        For Each s In _forms.Values
            If s?.HostForm IsNot Nothing Then D3D_RenderCore.InvalidateExistingTextResources(s.HostForm)
        Next
    End Sub

    Private Sub 更新窗口内边距(s As PerFormState)
        If s Is Nothing Then Return
        If s.IsFullScreen Then
            Dim captionInset As Integer = If(s.FullScreenCaptionVisible,
                                             取缩放标题栏高度(s.HostForm),
                                             0)
            s.HostForm.Padding = New Padding(
                s.OriginalPadding.Left,
                s.OriginalPadding.Top + captionInset,
                s.OriginalPadding.Right,
                s.OriginalPadding.Bottom)
            Return
        End If
        Dim bdr As Integer = 取缩放边框厚度(s.HostForm)
        Dim captionH As Integer = 取缩放标题栏高度(s.HostForm)
        s.HostForm.Padding = New Padding(
            s.OriginalPadding.Left + bdr,
            s.OriginalPadding.Top + bdr + captionH,
            s.OriginalPadding.Right + bdr,
            s.OriginalPadding.Bottom + bdr)
    End Sub

    Private Sub 处理DpiChanged(s As PerFormState)
        If s Is Nothing OrElse s.HostForm Is Nothing OrElse s.HostForm.IsDisposed Then Return
        If s.ChromeOverlayActive Then D3D_RenderDiagnostics.V5ChromeOverlayDpiUpdated()
        s.LayoutSignature = -1
        RecalculateButtonBounds(s)
        更新窗口内边距(s)
        D3D_RenderCore.InvalidateExistingTextResources(s.HostForm)
        If 毛玻璃当前启用(s) Then 请求毛玻璃帧(s, True, forceImageMode:=True)
        更新阴影(s)
        请求GPU渲染(s.HostForm, 获取真实客户区矩形(s.HostForm), True)
    End Sub

    Friend Sub 开始渐入动画(s As PerFormState)
        If s Is Nothing OrElse Not s.AnimatingShow Then Return
        Dim frm = s.HostForm
        Dim targetAlpha As Integer = CInt(Math.Round(s.OriginalOpacity * 255))
        Dim syncShadow As Boolean = (_阴影模式 = ShadowModeEnum.Layer) AndAlso s.ShadowForm IsNot Nothing
        Dim duration As Integer = _动画持续时间
        Dim t As PrecisionTimer = 创建UI精度计时器(frm, FrameIntervalMilliseconds(60))
        Dim startTicks As Long = Stopwatch.GetTimestamp()
        AddHandler t.Tick, Sub(sender, ev)
                               Dim elapsed As Double = (Stopwatch.GetTimestamp() - startTicks) * 1000.0R / Stopwatch.Frequency
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
    ''' <summary>窗口处于激活状态时的边框绘制颜色。</summary>
    <Category("LakeUI"), Description("窗口边框颜色。"), DefaultValue(GetType(Color), "60,60,60")>
    Public Property BorderColor As Color
        Get
            Return _边框颜色
        End Get
        Set(value As Color)
            If _边框颜色 = value Then Return
            _边框颜色 = value
            For Each s In _forms.Values
                If s?.HostForm Is Nothing OrElse s.HostForm.IsDisposed OrElse Not s.HostForm.IsHandleCreated OrElse s.IsFullScreen Then Continue For
                Try : 应用Dwm边框颜色(s.HostForm.Handle) : Catch : End Try
            Next
            通知重绘()
        End Set
    End Property

    Private _边框失焦颜色 As Color = Color.FromArgb(40, 40, 40)
    ''' <summary>窗口失去焦点时的边框绘制颜色。</summary>
    <Category("LakeUI"), Description("窗口失去焦点时的边框颜色。"), DefaultValue(GetType(Color), "40,40,40")>
    Public Property BorderInactiveColor As Color
        Get
            Return _边框失焦颜色
        End Get
        Set(value As Color)
            If _边框失焦颜色 = value Then Return
            _边框失焦颜色 = value
            For Each s In _forms.Values
                If s?.HostForm Is Nothing OrElse s.HostForm.IsDisposed OrElse Not s.HostForm.IsHandleCreated OrElse s.IsFullScreen Then Continue For
                Try : 应用Dwm边框颜色(s.HostForm.Handle) : Catch : End Try
            Next
            通知重绘()
        End Set
    End Property

    Private _边框厚度 As Integer = 1
    ''' <summary>窗口边框的绘制厚度（逻辑像素）。设为 0 表示不绘制边框；该值会同步影响窗体内边距以避免内容被边框遮挡。</summary>
    <Category("LakeUI"), Description("窗口边框的绘制厚度（逻辑像素）。0 = 不绘制边框。"), DefaultValue(1)>
    Public Property BorderSize As Integer
        Get
            Return _边框厚度
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If _边框厚度 = value Then Return
            _边框厚度 = value
            For Each s In _forms.Values
                If s Is Nothing Then Continue For
                s.LayoutSignature = -1
                s.ChromeOverlayRegionsSignature = Long.MinValue
                RecalculateButtonBounds(s)
                更新窗口内边距(s)
                If s.HostForm IsNot Nothing AndAlso Not s.HostForm.IsDisposed AndAlso s.HostForm.IsHandleCreated AndAlso Not s.IsFullScreen Then
                    Try : 应用Dwm边框颜色(s.HostForm.Handle) : Catch : End Try
                    更新ChromeOverlays(s)
                End If
            Next
            通知重绘()
        End Set
    End Property

#End Region

#Region "属性 - 标题栏"

    ''' <summary>
    ''' 标题栏左侧绑定的单个界面控件。控件显示在图标和左侧窗口按钮之后，标题文字之前。
    ''' 布局宽度读取控件自身的 <see cref="Control.Width"/>，并随宿主窗体 DPI 缩放。
    ''' 一个组件同时附加多个窗体时，该控件只显示在第一个附加的窗体中。
    ''' </summary>
    <Category("LakeUI"), Description("标题栏左侧绑定的界面控件。只显示在第一个附加的窗体中。"),
     DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property CaptionControl As Control
        Get
            Return _标题栏绑定控件
        End Get
        Set(value As Control)
            If ReferenceEquals(_标题栏绑定控件, value) Then Return
            If value IsNot Nothing AndAlso TypeOf value Is Form Then
                Throw New ArgumentException("CaptionControl 不能绑定 Form，请绑定 Panel 或其他普通控件。", NameOf(value))
            End If

            解除标题栏控件绑定()
            _标题栏绑定控件 = value
            If _标题栏绑定控件 IsNot Nothing Then
                _标题栏控件原始父级 = _标题栏绑定控件.Parent
                _标题栏控件原始边界 = _标题栏绑定控件.Bounds
                _标题栏控件原始停靠 = _标题栏绑定控件.Dock
                _标题栏控件原始锚定 = _标题栏绑定控件.Anchor
                _标题栏控件逻辑宽度 = _标题栏绑定控件.Width / Math.Max(0.01F, 取Dpi缩放(_标题栏绑定控件))
                AddHandler _标题栏绑定控件.Disposed, AddressOf 标题栏绑定控件_Disposed
                AddHandler _标题栏绑定控件.SizeChanged, AddressOf 标题栏绑定控件_SizeChanged
            End If

            使布局失效()
            通知标题栏重绘()
        End Set
    End Property

    Private _标题栏高度 As Integer = 32
    ''' <summary>标题栏区域的高度（逻辑像素）。改变此值会同步重算按钮布局并调整窗体内边距。</summary>
    <Category("LakeUI"), Description("标题栏区域的高度（逻辑像素）。"), DefaultValue(32)>
    Public Property CaptionHeight As Integer
        Get
            Return _标题栏高度
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If _标题栏高度 = value Then Return
            _标题栏高度 = value
            For Each s In _forms.Values
                s.LayoutSignature = -1
                RecalculateButtonBounds(s)
                更新窗口内边距(s)
            Next
            通知重绘()
        End Set
    End Property

    Private _标题栏底部横线高度 As Integer = 1
    ''' <summary>标题栏底部横线的高度（逻辑像素）。横线占用标题栏高度且不受 <see cref="CaptionPadding"/> 影响；设为 0 表示不绘制。</summary>
    <Category("LakeUI"), Description("标题栏底部横线的高度（逻辑像素）。0 = 不绘制横线。"), DefaultValue(1)>
    Public Property CaptionBottomLineHeight As Integer
        Get
            Return _标题栏底部横线高度
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If _标题栏底部横线高度 = value Then Return
            _标题栏底部横线高度 = value
            使布局失效()
            通知标题栏重绘()
        End Set
    End Property

    Private _标题栏底部横线颜色 As Color = Color.FromArgb(40, 220, 220, 220)
    ''' <summary>标题栏底部横线的颜色。</summary>
    <Category("LakeUI"), Description("标题栏底部横线的颜色。"), DefaultValue(GetType(Color), "40, 220, 220, 220")>
    Public Property CaptionBottomLineColor As Color
        Get
            Return _标题栏底部横线颜色
        End Get
        Set(value As Color)
            If _标题栏底部横线颜色 = value Then Return
            _标题栏底部横线颜色 = value
            通知标题栏重绘()
        End Set
    End Property

    Private _标题栏背景颜色 As Color = Color.FromArgb(32, 32, 32)
    ''' <summary>标题栏在窗口激活时的背景填充颜色。</summary>
    <Category("LakeUI"), Description("标题栏的背景颜色。"), DefaultValue(GetType(Color), "32,32,32")>
    Public Property CaptionBackColor As Color
        Get
            Return _标题栏背景颜色
        End Get
        Set(value As Color)
            If _标题栏背景颜色 = value Then Return
            _标题栏背景颜色 = value : 通知重绘()
        End Set
    End Property

    Private _标题栏失焦背景颜色 As Color = Color.FromArgb(28, 28, 28)
    ''' <summary>窗口失去焦点时标题栏的背景填充颜色。</summary>
    <Category("LakeUI"), Description("窗口失去焦点时标题栏的背景颜色。"), DefaultValue(GetType(Color), "28,28,28")>
    Public Property CaptionInactiveBackColor As Color
        Get
            Return _标题栏失焦背景颜色
        End Get
        Set(value As Color)
            If _标题栏失焦背景颜色 = value Then Return
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
            If _标题栏背景图片 Is value Then Return
            _标题栏背景图片 = value
            通知重绘()
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
            If _标题栏遮罩颜色 = value Then Return
            _标题栏遮罩颜色 = value : 通知重绘()
        End Set
    End Property

#End Region

#Region "属性 - 标题文字"

    Private Const TitleTextPrivateProtocolTitleToken As String = "<Title>"
    Private _标题文字私有协议 As String = String.Empty
    ''' <summary>标题栏文本私有协议。仅对第一个调用 <see cref="Attach"/> 接入的窗体生效；为空时直接使用窗体 Text。</summary>
    <Category("LakeUI"), Description("标题栏文本私有协议。仅对第一个接入的窗体生效；非空时将 <Title> 替换为该窗体真实 Text 后渲染。"), DefaultValue("")>
    Public Property TitleTextPrivateProtocol As String
        Get
            Return _标题文字私有协议
        End Get
        Set(value As String)
            value = If(value, String.Empty)
            If _标题文字私有协议 = value Then Return
            _标题文字私有协议 = value
            通知标题文字重绘(True)
        End Set
    End Property

    Private _标题文字颜色 As Color = Color.FromArgb(230, 230, 230)
    ''' <summary>窗口激活时的标题文字颜色。</summary>
    <Category("LakeUI"), Description("标题文字颜色。"), DefaultValue(GetType(Color), "230,230,230")>
    Public Property TitleForeColor As Color
        Get
            Return _标题文字颜色
        End Get
        Set(value As Color)
            If _标题文字颜色 = value Then Return
            _标题文字颜色 = value
            通知标题文字重绘(True)
        End Set
    End Property

    Private _标题文字失焦颜色 As Color = Color.FromArgb(140, 140, 140)
    ''' <summary>窗口失去焦点时的标题文字颜色。</summary>
    <Category("LakeUI"), Description("窗口失去焦点时标题文字颜色。"), DefaultValue(GetType(Color), "140,140,140")>
    Public Property TitleInactiveForeColor As Color
        Get
            Return _标题文字失焦颜色
        End Get
        Set(value As Color)
            If _标题文字失焦颜色 = value Then Return
            _标题文字失焦颜色 = value
            通知标题文字重绘(True)
        End Set
    End Property

    Private _标题文字对齐 As TitleAlignEnum = TitleAlignEnum.Left
    ''' <summary>标题文字在可用区域内的水平对齐方式（左 / 居中 / 右）。</summary>
    <Category("LakeUI"), Description("标题文字的水平对齐方式。"), DefaultValue(GetType(TitleAlignEnum), "Left")>
    Public Property TitleAlign As TitleAlignEnum
        Get
            Return _标题文字对齐
        End Get
        Set(value As TitleAlignEnum)
            If _标题文字对齐 = value Then Return
            _标题文字对齐 = value : 通知重绘()
        End Set
    End Property

    Private _标题文字字体 As Font = Nothing
    ''' <summary>标题文字使用的字体。设为 Nothing 时使用宿主窗体的 <see cref="Control.Font"/>。</summary>
    <Category("LakeUI"), Description("标题文字的字体。留空则使用宿主窗口的 Font。"), DefaultValue(GetType(Font), "")>
    Public Property TitleFont As Font
        Get
            Return _标题文字字体
        End Get
        Set(value As Font)
            If ReferenceEquals(_标题文字字体, value) Then Return
            _标题文字字体 = value
            使标题字体资源失效()
            通知重绘()
        End Set
    End Property

    Private _标题文字左边距 As Integer = 10
    ''' <summary>标题文字距离其左侧元素（图标右边缘或窗口左边缘）的水平间距（逻辑像素）。</summary>
    <Category("LakeUI"), Description("标题文字距离左侧（或图标右侧）的间距。"), DefaultValue(10)>
    Public Property TitlePaddingLeft As Integer
        Get
            Return _标题文字左边距
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If _标题文字左边距 = value Then Return
            _标题文字左边距 = value
            使布局失效()
            通知标题栏重绘()
        End Set
    End Property

    Private _标题栏内容内边距 As Padding = Padding.Empty
    ''' <summary>标题栏内所有内容距离标题栏四周的内边距（逻辑像素）。</summary>
    <Category("LakeUI"), Description("标题栏整体内容的四周内边距。"), DefaultValue(GetType(Padding), "0, 0, 0, 0")>
    Public Property CaptionPadding As Padding
        Get
            Return _标题栏内容内边距
        End Get
        Set(value As Padding)
            value = 规范化内边距(value)
            If _标题栏内容内边距.Equals(value) Then Return
            _标题栏内容内边距 = value
            使布局失效()
            通知标题栏重绘()
        End Set
    End Property

    Private _标题文字右边距 As Integer = 10
    ''' <summary>标题文字距离其右侧元素（按钮左边缘或窗口右边缘）的水平间距（逻辑像素）。</summary>
    <Category("LakeUI"), Description("标题文字距离右侧（或按钮左侧）的间距。"), DefaultValue(10)>
    Public Property TitlePaddingRight As Integer
        Get
            Return _标题文字右边距
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If _标题文字右边距 = value Then Return
            _标题文字右边距 = value : 通知重绘()
        End Set
    End Property

#End Region

#Region "属性 - 图标"

    Private _图标来源 As IconSourceEnum = IconSourceEnum.FormIcon
    ''' <summary>标题栏图标的来源：None 不显示、FormIcon 使用窗体 <see cref="Form.Icon"/>、Custom 使用 <see cref="CustomIcon"/>。</summary>
    <Category("LakeUI"), Description("标题栏图标来源。"), DefaultValue(GetType(IconSourceEnum), "FormIcon")>
    Public Property IconSource As IconSourceEnum
        Get
            Return _图标来源
        End Get
        Set(value As IconSourceEnum)
            If _图标来源 = value Then Return
            _图标来源 = value
            使布局失效()
            通知标题栏重绘()
        End Set
    End Property

    Private _自定义图标 As Image = Nothing
    ''' <summary>当 <see cref="IconSource"/> 设为 Custom 时使用的自定义图像；其它来源下被忽略。</summary>
    <Category("LakeUI"), Description("IconSource 为 Custom 时使用的图像。"), DefaultValue(GetType(Image), "")>
    Public Property CustomIcon As Image
        Get
            Return _自定义图标
        End Get
        Set(value As Image)
            If _自定义图标 Is value Then Return
            _自定义图标 = value
            通知重绘()
        End Set
    End Property

    Private _图标大小 As Integer = 16
    ''' <summary>图标显示尺寸（正方形，逻辑像素）。</summary>
    <Category("LakeUI"), Description("图标的显示尺寸（逻辑像素，正方形）。"), DefaultValue(16)>
    Public Property IconSize As Integer
        Get
            Return _图标大小
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If _图标大小 = value Then Return
            _图标大小 = value
            使布局失效()
            通知标题栏重绘()
        End Set
    End Property

    Private _图标内边距 As New Padding(8, 0, 0, 0)
    ''' <summary>图标四周的内边距（逻辑像素）。左右值同时决定图标与相邻内容的间距，上下值限定图标的纵向布局区域。</summary>
    <Category("LakeUI"), Description("图标四周的内边距。"), DefaultValue(GetType(Padding), "8, 0, 0, 0")>
    Public Property IconPadding As Padding
        Get
            Return _图标内边距
        End Get
        Set(value As Padding)
            value = 规范化内边距(value)
            If _图标内边距.Equals(value) Then Return
            _图标内边距 = value
            使布局失效()
            通知标题栏重绘()
        End Set
    End Property

#End Region

#Region "属性 - 控制按钮"

    Private _按钮位置 As ButtonPositionEnum = ButtonPositionEnum.Right
    ''' <summary>最小化 / 最大化 / 关闭按钮组在标题栏中的水平位置。</summary>
    <Category("LakeUI"), Description("控制按钮的布局位置。"), DefaultValue(GetType(ButtonPositionEnum), "Right")>
    Public Property ButtonPosition As ButtonPositionEnum
        Get
            Return _按钮位置
        End Get
        Set(value As ButtonPositionEnum)
            If _按钮位置 = value Then Return
            _按钮位置 = value
            使布局失效()
            通知标题栏重绘()
        End Set
    End Property

    Private _按钮宽度 As Integer = 46
    ''' <summary>每个控制按钮的命中与绘制宽度（逻辑像素），最小为 16。</summary>
    <Category("LakeUI"), Description("每个控制按钮的宽度（逻辑像素）。"), DefaultValue(46)>
    Public Property ButtonWidth As Integer
        Get
            Return _按钮宽度
        End Get
        Set(value As Integer)
            value = Math.Max(16, value)
            If _按钮宽度 = value Then Return
            _按钮宽度 = value
            使布局失效()
            通知标题栏重绘()
        End Set
    End Property

    Private _按钮符号大小 As Integer = 10
    ''' <summary>按钮内绘制的符号（×、□、—）的边长（逻辑像素），最小为 4。</summary>
    <Category("LakeUI"), Description("按钮符号的逻辑尺寸。"), DefaultValue(10)>
    Public Property ButtonGlyphSize As Integer
        Get
            Return _按钮符号大小
        End Get
        Set(value As Integer)
            value = Math.Max(4, value)
            If _按钮符号大小 = value Then Return
            _按钮符号大小 = value : 通知重绘()
        End Set
    End Property

    Private _按钮符号线宽 As Single = 1.0F
    ''' <summary>按钮符号线条的画笔宽度（逻辑像素），最小为 0.5。</summary>
    <Category("LakeUI"), Description("按钮符号线条宽度。"), DefaultValue(1.0F)>
    Public Property ButtonGlyphLineWidth As Single
        Get
            Return _按钮符号线宽
        End Get
        Set(value As Single)
            value = Math.Max(0.5F, value)
            If _按钮符号线宽 = value Then Return
            _按钮符号线宽 = value : 通知重绘()
        End Set
    End Property

    Private _按钮内边距 As Padding
    ''' <summary>每个控制按钮内部的留白；可视化背景与符号绘制区域将在按钮命中区基础上向内收缩。</summary>
    <Category("LakeUI"), Description("控制按钮的内边距。"), DefaultValue(GetType(Padding), "0, 0, 0, 0")>
    Public Property ButtonPadding As Padding
        Get
            Return _按钮内边距
        End Get
        Set(value As Padding)
            value = 规范化内边距(value)
            If _按钮内边距.Equals(value) Then Return
            _按钮内边距 = value
            通知重绘()
        End Set
    End Property

    Private _按钮圆角半径 As Integer = 0
    ''' <summary>控制按钮背景填充的圆角半径（逻辑像素）；0 表示矩形填充。</summary>
    <Category("LakeUI"), Description("控制按钮背景圆角半径。"), DefaultValue(0)>
    Public Property ButtonCornerRadius As Integer
        Get
            Return _按钮圆角半径
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If _按钮圆角半径 = value Then Return
            _按钮圆角半径 = value : 通知重绘()
        End Set
    End Property

    Private _按钮间距 As Integer = 0
    ''' <summary>相邻控制按钮之间的水平间隔（逻辑像素）。</summary>
    <Category("LakeUI"), Description("控制按钮之间的间距。"), DefaultValue(0)>
    Public Property ButtonSpacing As Integer
        Get
            Return _按钮间距
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If _按钮间距 = value Then Return
            _按钮间距 = value
            使布局失效()
            通知标题栏重绘()
        End Set
    End Property

    Private _显示全屏按钮 As Boolean = False
    ''' <summary>是否在首个附加窗体的标题栏显示全屏按钮。F11 仅对首个附加窗体生效。</summary>
    <Category("LakeUI"), Description("是否在首个附加窗体显示全屏按钮。显示后可通过按钮或 F11 进入全屏。"), DefaultValue(False)>
    Public Property ShowFullScreenButton As Boolean
        Get
            Return _显示全屏按钮
        End Get
        Set(value As Boolean)
            If _显示全屏按钮 = value Then Return
            _显示全屏按钮 = value
            使布局失效()
            通知标题栏重绘()
        End Set
    End Property

#End Region

#Region "属性 - 关闭按钮颜色"

    Private _关闭按钮背景颜色 As Color = Color.Transparent
    ''' <summary>关闭按钮默认（非悬停 / 非按下）状态下的背景颜色。</summary>
    <Category("LakeUI"), Description("关闭按钮默认状态背景颜色。"), DefaultValue(GetType(Color), "Transparent")>
    Public Property CloseButtonBackColor As Color
        Get
            Return _关闭按钮背景颜色
        End Get
        Set(value As Color)
            If _关闭按钮背景颜色 = value Then Return
            _关闭按钮背景颜色 = value : 通知重绘()
        End Set
    End Property

    Private _关闭按钮悬停背景颜色 As Color = Color.FromArgb(232, 17, 35)
    ''' <summary>关闭按钮鼠标悬停状态下的背景颜色。</summary>
    <Category("LakeUI"), Description("关闭按钮悬停状态背景颜色。"), DefaultValue(GetType(Color), "232,17,35")>
    Public Property CloseButtonHoverBackColor As Color
        Get
            Return _关闭按钮悬停背景颜色
        End Get
        Set(value As Color)
            If _关闭按钮悬停背景颜色 = value Then Return
            _关闭按钮悬停背景颜色 = value : 通知重绘()
        End Set
    End Property

    Private _关闭按钮按下背景颜色 As Color = Color.FromArgb(200, 15, 30)
    ''' <summary>关闭按钮被鼠标按下且仍处于悬停状态时的背景颜色。</summary>
    <Category("LakeUI"), Description("关闭按钮按下状态背景颜色。"), DefaultValue(GetType(Color), "200,15,30")>
    Public Property CloseButtonPressedBackColor As Color
        Get
            Return _关闭按钮按下背景颜色
        End Get
        Set(value As Color)
            If _关闭按钮按下背景颜色 = value Then Return
            _关闭按钮按下背景颜色 = value : 通知重绘()
        End Set
    End Property

    Private _关闭按钮符号颜色 As Color = Color.FromArgb(200, 200, 200)
    ''' <summary>关闭按钮默认状态下的“×”符号线条颜色。</summary>
    <Category("LakeUI"), Description("关闭按钮默认状态符号颜色。"), DefaultValue(GetType(Color), "200,200,200")>
    Public Property CloseButtonGlyphColor As Color
        Get
            Return _关闭按钮符号颜色
        End Get
        Set(value As Color)
            If _关闭按钮符号颜色 = value Then Return
            _关闭按钮符号颜色 = value : 通知重绘()
        End Set
    End Property

    Private _关闭按钮悬停符号颜色 As Color = Color.White
    ''' <summary>关闭按钮悬停 / 按下状态下的符号颜色。</summary>
    <Category("LakeUI"), Description("关闭按钮悬停状态符号颜色。"), DefaultValue(GetType(Color), "White")>
    Public Property CloseButtonHoverGlyphColor As Color
        Get
            Return _关闭按钮悬停符号颜色
        End Get
        Set(value As Color)
            If _关闭按钮悬停符号颜色 = value Then Return
            _关闭按钮悬停符号颜色 = value : 通知重绘()
        End Set
    End Property

#End Region

#Region "属性 - 最大化/最小化按钮颜色"

    Private _功能按钮背景颜色 As Color = Color.Transparent
    ''' <summary>最小化 / 最大化 / 还原按钮默认状态下的背景颜色。</summary>
    <Category("LakeUI"), Description("最小化/最大化按钮默认背景颜色。"), DefaultValue(GetType(Color), "Transparent")>
    Public Property CaptionButtonBackColor As Color
        Get
            Return _功能按钮背景颜色
        End Get
        Set(value As Color)
            If _功能按钮背景颜色 = value Then Return
            _功能按钮背景颜色 = value : 通知重绘()
        End Set
    End Property

    Private _功能按钮悬停背景颜色 As Color = Color.FromArgb(55, 55, 55)
    ''' <summary>最小化/最大化按钮鼠标悬停状态下的背景颜色。</summary>
    <Category("LakeUI"), Description("最小化/最大化按钮悬停状态背景颜色。"), DefaultValue(GetType(Color), "55,55,55")>
    Public Property CaptionButtonHoverBackColor As Color
        Get
            Return _功能按钮悬停背景颜色
        End Get
        Set(value As Color)
            If _功能按钮悬停背景颜色 = value Then Return
            _功能按钮悬停背景颜色 = value : 通知重绘()
        End Set
    End Property

    Private _功能按钮按下背景颜色 As Color = Color.FromArgb(70, 70, 70)
    ''' <summary>最小化/最大化按钮被按下且处于悬停状态时的背景颜色。</summary>
    <Category("LakeUI"), Description("最小化/最大化按钮按下状态背景颜色。"), DefaultValue(GetType(Color), "70,70,70")>
    Public Property CaptionButtonPressedBackColor As Color
        Get
            Return _功能按钮按下背景颜色
        End Get
        Set(value As Color)
            If _功能按钮按下背景颜色 = value Then Return
            _功能按钮按下背景颜色 = value : 通知重绘()
        End Set
    End Property

    Private _功能按钮符号颜色 As Color = Color.FromArgb(200, 200, 200)
    ''' <summary>最小化/最大化按钮默认状态下的符号线条颜色。</summary>
    <Category("LakeUI"), Description("最小化/最大化按钮默认状态符号颜色。"), DefaultValue(GetType(Color), "200,200,200")>
    Public Property CaptionButtonGlyphColor As Color
        Get
            Return _功能按钮符号颜色
        End Get
        Set(value As Color)
            If _功能按钮符号颜色 = value Then Return
            _功能按钮符号颜色 = value : 通知重绘()
        End Set
    End Property

    Private _功能按钮悬停符号颜色 As Color = Color.White
    ''' <summary>最小化/最大化按钮悬停 / 按下状态下的符号颜色。</summary>
    <Category("LakeUI"), Description("最小化/最大化按钮悬停状态符号颜色。"), DefaultValue(GetType(Color), "White")>
    Public Property CaptionButtonHoverGlyphColor As Color
        Get
            Return _功能按钮悬停符号颜色
        End Get
        Set(value As Color)
            If _功能按钮悬停符号颜色 = value Then Return
            _功能按钮悬停符号颜色 = value : 通知重绘()
        End Set
    End Property

#End Region

#Region "属性 - 调整大小"

    Private _调整边框宽度 As Integer = 6
    ''' <summary>窗口边缘可触发拖拽改变大小的热区宽度（逻辑像素）。</summary>
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
    ''' <summary>是否允许通过拖拽窗口边缘调整大小。设为 False 时禁用所有 Resize 命中测试。</summary>
    <Category("LakeUI"), Description("是否允许通过拖拽窗口边缘调整大小。"), DefaultValue(True)>
    Public Property AllowResize As Boolean
        Get
            Return _允许调整大小
        End Get
        Set(value As Boolean)
            _允许调整大小 = value
        End Set
    End Property

    Private _最大化时隐藏调整边框 As Boolean = True
    ''' <summary>窗口最大化时是否禁用边缘调整大小热区（推荐 True，避免最大化下边缘穿透到次屏）。</summary>
    <Category("LakeUI"), Description("窗口最大化时是否禁用调整大小边框。"), DefaultValue(True)>
    Public Property HideResizeBorderWhenMaximized As Boolean
        Get
            Return _最大化时隐藏调整边框
        End Get
        Set(value As Boolean)
            _最大化时隐藏调整边框 = value
        End Set
    End Property

#End Region

#Region "属性 - 窗口外观"

    Private _窗口圆角模式 As DwmWindowStyle.CornerMode = DwmWindowStyle.CornerMode.Square
    ''' <summary>
    ''' 窗口圆角首选项。默认 Square 以保持既有行为；Windows 11 Build 22000+ 支持 Default、Round 和 RoundSmall。
    ''' 全屏期间始终使用直角，退出全屏后恢复当前设置。
    ''' </summary>
    <Category("LakeUI"), Description("窗口圆角首选项。Windows 11 Build 22000+ 生效；全屏期间始终为直角。"), DefaultValue(GetType(DwmWindowStyle.CornerMode), "Square")>
    Public Property WindowCornerMode As DwmWindowStyle.CornerMode
        Get
            Return _窗口圆角模式
        End Get
        Set(value As DwmWindowStyle.CornerMode)
            If _窗口圆角模式 = value Then Return
            _窗口圆角模式 = value
            For Each s In _forms.Values
                If s Is Nothing Then Continue For
                s.LayoutSignature = -1
                s.ChromeOverlayRegionsSignature = Long.MinValue
                If s.IsFullScreen OrElse s.HostForm Is Nothing OrElse s.HostForm.IsDisposed OrElse Not s.HostForm.IsHandleCreated Then Continue For
                应用Dwm窗口属性(s.HostForm.Handle)
                If s.ShadowForm IsNot Nothing Then s.ShadowForm.ForceReset()
                更新阴影(s)
                更新ChromeOverlays(s)
            Next
            通知重绘()
        End Set
    End Property

#End Region

#Region "属性 - 高级 (排除区域)"

    Private _标题栏排除区域 As New List(Of Rectangle)
    ''' <summary>
    ''' 标题栏内的排除区域列表（客户端坐标）。位于这些矩形内的鼠标命中将返回 HTCLIENT 而非 HTCAPTION，
    ''' 以便放置可交互控件（如菜单、搜索框）而不被窗口拖动逻辑拦截。
    ''' </summary>
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
                If s.IsFullScreen Then
                    If value = ShadowModeEnum.DWM Then
                        s.FullScreenOriginalStyle = s.FullScreenOriginalStyle Or WS_CAPTION
                    Else
                        s.FullScreenOriginalStyle = s.FullScreenOriginalStyle And Not CLng(WS_CAPTION)
                    End If
                    Dim fullScreenStyle As Long = GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64()
                    fullScreenStyle = (fullScreenStyle Or WS_POPUP) And Not CLng(WS_CAPTION) And Not CLng(WS_THICKFRAME)
                    SetWindowLongPtr(hWnd, GWL_STYLE, New IntPtr(fullScreenStyle))
                    应用全屏Dwm窗口属性(hWnd)
                    SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                                 CUInt(SWP_FRAMECHANGED Or SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOZORDER))
                    更新阴影(s)
                    请求GPU渲染(s.HostForm, 获取真实客户区矩形(s.HostForm), True)
                    Continue For
                End If
                Dim style As Long = GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64()
                If value = ShadowModeEnum.DWM Then
                    style = style Or WS_CAPTION
                Else
                    style = style And Not WS_CAPTION
                End If
                SetWindowLongPtr(hWnd, GWL_STYLE, New IntPtr(style))
                应用Dwm窗口属性(hWnd)
                SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                             SWP_FRAMECHANGED Or SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOZORDER)
                更新阴影(s)
                请求GPU渲染(s.HostForm, 获取真实客户区矩形(s.HostForm), True)
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
            value = Math.Max(1, value)
            If _分层阴影深度 = value Then Return
            _分层阴影深度 = value
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
            If _分层阴影颜色 = value Then Return
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
            If _分层阴影不透明度 = value Then Return
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
            value = Math.Max(0, Math.Min(value, _分层阴影深度))
            If _分层阴影调整宽度 = value Then Return
            _分层阴影调整宽度 = value
            For Each s In _forms.Values
                If s.ShadowForm IsNot Nothing Then
                    s.ShadowForm.ResizeWidth = _分层阴影调整宽度
                    s.ShadowForm.UpdateHitTestTransparency()
                End If
            Next
        End Set
    End Property

    Private _分层阴影整区可调 As Boolean = False
    ''' <summary>
    ''' 是否将整个分层阴影绘制区域作为窗口大小调整热区。
    ''' 启用后阴影绘制范围内的任意位置都可触发尺寸调整，<see cref="LayerShadowResizeWidth"/> 上限被忽略。
    ''' 仅 ShadowMode = Layer 时生效。
    ''' </summary>
    <Category("LakeUI"), Description("是否将整个分层阴影绘制区域作为窗口大小调整热区。仅 ShadowMode = Layer 时生效。"), DefaultValue(False)>
    Public Property LayerShadowResizeFullArea As Boolean
        Get
            Return _分层阴影整区可调
        End Get
        Set(value As Boolean)
            If _分层阴影整区可调 = value Then Return
            _分层阴影整区可调 = value
            For Each s In _forms.Values
                If s.ShadowForm IsNot Nothing Then
                    s.ShadowForm.ResizeFullArea = value
                    s.ShadowForm.UpdateHitTestTransparency()
                End If
            Next
        End Set
    End Property

    Private Sub 更新阴影(s As PerFormState)
        更新阴影(s, Rectangle.Empty, False)
    End Sub

    Private Sub 更新阴影实时跟随(s As PerFormState)
        If s Is Nothing OrElse s.HostForm Is Nothing OrElse Not s.HostForm.IsHandleCreated Then Return
        更新阴影(s, 获取窗口屏幕矩形(s.HostForm), False)
    End Sub

    Private Sub 更新阴影(s As PerFormState, boundsOverride As Rectangle, forceFullRender As Boolean)
        If s Is Nothing OrElse s.HostForm Is Nothing Then Return
        ' 阴影显隐也以原生窗口状态为准，避免还原过渡期间误判为最大化。
        Dim zoomed As Boolean = 窗口当前已最大化(s.HostForm)
        Dim minimized As Boolean = (s.HostForm.WindowState = FormWindowState.Minimized)

        If _阴影模式 <> ShadowModeEnum.Layer OrElse s.IsFullScreen OrElse zoomed OrElse minimized OrElse Not s.HostForm.Visible Then
            If s.ShadowForm IsNot Nothing Then
                If Not s.HostForm.Visible Then
                    销毁阴影(s)
                Else
                    s.ShadowForm.SetDesktopAwareVisible(False)
                End If
            End If
            Return
        End If

        If s.ShadowForm Is Nothing Then
            s.ShadowForm = New ShadowWindow With {
                .HostHandle = s.HostForm.Handle,
                .ShadowDepth = _分层阴影深度,
                .ResizeWidth = _分层阴影调整宽度,
                .ResizeFullArea = _分层阴影整区可调
            }
            s.ShadowForm.UpdateHitTestTransparency()
        End If

        Dim bounds = If(boundsOverride.IsEmpty, s.HostForm.Bounds, boundsOverride)
        s.ShadowForm.HostHandle = s.HostForm.Handle
        s.ShadowForm.ShadowDepth = _分层阴影深度
        s.ShadowForm.ResizeWidth = _分层阴影调整宽度
        s.ShadowForm.ResizeFullArea = _分层阴影整区可调
        s.ShadowForm.UpdateHitTestTransparency()
        Dim shadowColor As Color = _分层阴影颜色
        If _分层阴影自动颜色 AndAlso 毛玻璃当前启用(s) Then
            shadowColor = s.Renderer.DeriveShadowColor(_分层阴影颜色)
        End If
        Dim logicalCornerRadius As Single = If(DwmWindowStyle.IsCornerModeSupported,
                                                DwmWindowStyle.GetCornerRadiusLogical(_窗口圆角模式),
                                                0.0F)
        Dim shadowCornerRadius As Integer = Math.Max(0, CInt(Math.Round(缩放逻辑尺寸(s.HostForm, logicalCornerRadius))))
        s.ShadowForm.UpdateShadow(bounds, _分层阴影深度, shadowColor, _分层阴影不透明度,
                                  shadowCornerRadius, If(forceFullRender, False, s.IsInSizeMove))
        s.ShadowForm.SyncVirtualDesktopWithHost()
        s.ShadowForm.PlaceBehind(s.HostForm.Handle)
        s.ShadowForm.SetDesktopAwareVisible(True)

        If s.AnimatingShow AndAlso _显示动画模式 = WindowShowAnimationMode.Win32 Then
            s.ShadowForm.SetGlobalAlpha(0)
        End If
    End Sub

    Private Sub 销毁阴影(s As PerFormState)
        If s.ShadowForm IsNot Nothing Then
            Dim shadow = s.ShadowForm
            s.ShadowForm = Nothing
            Try
                If Not shadow.IsDisposed Then
                    shadow.SetDesktopAwareVisible(False)
                    shadow.Hide()
                    shadow.Close()
                End If
            Finally
                shadow.Dispose()
            End Try
        End If
    End Sub

#End Region

#Region "属性 - 毛玻璃"

    Private _毛玻璃模式 As BackdropModeEnum = BackdropModeEnum.None
    ''' <summary>
    ''' 毛玻璃 / 亚克力背景模式。启用后窗体背景将由"源 + 模糊 + tint + 噪点"组成。
    ''' 该模式（非 None）下 <see cref="CaptionBackColor"/> / <see cref="CaptionInactiveBackColor"/> 不再生效。
    ''' </summary>
    <Category("LakeUI - Backdrop"), Description("毛玻璃 / 亚克力背景模式。"), DefaultValue(GetType(BackdropModeEnum), "None")>
    Public Property BackdropMode As BackdropModeEnum
        Get
            Return _毛玻璃模式
        End Get
        Set(value As BackdropModeEnum)
            If _毛玻璃模式 = value Then Return
            _毛玻璃模式 = value
            For Each s In _forms.Values
                应用毛玻璃状态(s)
            Next
            通知重绘()
        End Set
    End Property

    Private _毛玻璃仅首个窗口 As Boolean = False
    ''' <summary>
    ''' 多个窗体共享同一个 ThisIsYourWindow 实例时，是否仅允许第一个成功 <see cref="Attach"/> 的窗体启用毛玻璃背景。
    ''' 该开关只限制 BackdropMode 非 None 时的 Renderer / WDA / 定时刷新，不影响标题栏、按钮、边框和阴影等窗口样式。
    ''' </summary>
    <Category("LakeUI - Backdrop"), Description("是否仅允许第一个接入的窗体启用毛玻璃背景。"), DefaultValue(False)>
    Public Property BackdropFirstWindowOnly As Boolean
        Get
            Return _毛玻璃仅首个窗口
        End Get
        Set(value As Boolean)
            If _毛玻璃仅首个窗口 = value Then Return
            _毛玻璃仅首个窗口 = value
            For Each s In _forms.Values
                应用毛玻璃状态(s)
                If s.ShadowForm IsNot Nothing Then s.ShadowForm.ForceReset()
                更新阴影(s)
            Next
            通知重绘()
        End Set
    End Property

    Private _尺寸移动刷新优化启用 As Boolean = True
    ''' <summary>
    ''' 是否启用尺寸移动期间的客户区刷新优化。
    ''' 启用后窗口移动 / 调整大小期间会延迟客户区坐标上报并抑制大多数客户区刷新，
    ''' 仅在尺寸移动结束或鼠标抬起后提交一次重绘。关闭后恢复常规 WinForms 刷新节奏。
    ''' </summary>
    <Category("LakeUI"), Description("启用尺寸移动期间的客户区刷新优化：移动/调整大小期间延迟客户区坐标上报并抑制大多数客户区刷新，结束或鼠标抬起后再重绘。"), DefaultValue(True)>
    Public Property SizeMoveRefreshOptimization As Boolean
        Get
            Return _尺寸移动刷新优化启用
        End Get
        Set(value As Boolean)
            If _尺寸移动刷新优化启用 = value Then Return
            _尺寸移动刷新优化启用 = value
            同步尺寸移动刷新优化状态()
        End Set
    End Property

    Private _毛玻璃图片 As Image = Nothing
    ''' <summary>
    ''' 当 <see cref="BackdropMode"/> = <see cref="BackdropModeEnum.Image"/> 时使用的虚拟背景图。
    ''' 图片以 cover 模式（保持比例放大撑满后居中裁切）适配窗口尺寸，再做模糊。
    ''' </summary>
    <Category("LakeUI - Backdrop"), Description("Image 模式下作为模糊源的图片（cover 撑满窗口）。"), DefaultValue(GetType(Image), Nothing)>
    Public Property BackdropImage As Image
        Get
            Return _毛玻璃图片
        End Get
        Set(value As Image)
            If _毛玻璃图片 Is value Then Return
            _毛玻璃图片 = value
            For Each s In _forms.Values
                If s.Renderer IsNot Nothing AndAlso _毛玻璃模式 = BackdropModeEnum.Image Then
                    s.Renderer.CleanupD2DResources(D3DCacheCleanupLevel.ReleaseAllCaches)
                    s.Renderer.SetSource(True, value)
                    s.Renderer.RequestFrame(获取毛玻璃捕获区域(s.HostForm), True)
                End If
            Next
            通知重绘()
        End Set
    End Property

    Private _毛玻璃Tint颜色 As Color = Color.FromArgb(120, 32, 32, 32)
    <Category("LakeUI - Backdrop"), Description("毛玻璃模式下激活窗口的 tint 叠加颜色（含 Alpha）。"), DefaultValue(GetType(Color), "120, 32, 32, 32")>
    Public Property BackdropTintColor As Color
        Get
            Return _毛玻璃Tint颜色
        End Get
        Set(value As Color)
            If _毛玻璃Tint颜色 = value Then Return
            _毛玻璃Tint颜色 = value : 通知重绘()
        End Set
    End Property

    Private _毛玻璃Tint失焦颜色 As Color = Color.Empty
    <Category("LakeUI - Backdrop"), Description("毛玻璃模式下失活窗口的 tint 叠加颜色。"), DefaultValue(GetType(Color), "")>
    Public Property BackdropTintInactiveColor As Color
        Get
            Return _毛玻璃Tint失焦颜色
        End Get
        Set(value As Color)
            If _毛玻璃Tint失焦颜色 = value Then Return
            _毛玻璃Tint失焦颜色 = value : 通知重绘()
        End Set
    End Property

    Private _毛玻璃模糊半径 As Integer = 24
    <Category("LakeUI - Backdrop"), Description("毛玻璃模糊半径（逻辑像素）。1 - 96。"), DefaultValue(24)>
    Public Property BackdropBlurRadius As Integer
        Get
            Return _毛玻璃模糊半径
        End Get
        Set(value As Integer)
            value = Math.Max(1, Math.Min(96, value))
            If _毛玻璃模糊半径 = value Then Return
            _毛玻璃模糊半径 = value
            应用毛玻璃参数()
        End Set
    End Property

    Private _毛玻璃模糊次数 As Integer = 3
    <Category("LakeUI - Backdrop"), Description("box blur 通过次数（0=不模糊，直出源图后仅叠加 Tint；1=方框，3≈高斯）。"), DefaultValue(3)>
    Public Property BackdropBlurPasses As Integer
        Get
            Return _毛玻璃模糊次数
        End Get
        Set(value As Integer)
            value = Math.Max(0, Math.Min(5, value))
            If _毛玻璃模糊次数 = value Then Return
            _毛玻璃模糊次数 = value
            应用毛玻璃参数()
        End Set
    End Property

    Private _毛玻璃下采样 As Integer = 4
    <Category("LakeUI - Backdrop"), Description("下采样倍率（建议 1/2/4/6/8，越大越快越糊；BackdropBlurPasses=0 时忽略）。"), DefaultValue(4)>
    Public Property BackdropDownsampleFactor As Integer
        Get
            Return _毛玻璃下采样
        End Get
        Set(value As Integer)
            value = Math.Max(1, value)
            If _毛玻璃下采样 = value Then Return
            _毛玻璃下采样 = value
            应用毛玻璃参数()
        End Set
    End Property

    Private _毛玻璃噪点不透明度 As Byte = 18
    <Category("LakeUI - Backdrop"), Description("噪点叠加层不透明度 (0-255)。0 = 关闭噪点。"), DefaultValue(CByte(18))>
    Public Property BackdropNoiseOpacity As Byte
        Get
            Return _毛玻璃噪点不透明度
        End Get
        Set(value As Byte)
            If _毛玻璃噪点不透明度 = value Then Return
            _毛玻璃噪点不透明度 = value : 通知重绘()
        End Set
    End Property

    Private _毛玻璃噪点缩放 As Single = 1.0F
    <Category("LakeUI - Backdrop"), Description("噪点 tile 缩放（>1 颗粒变粗）。"), DefaultValue(1.0F)>
    Public Property BackdropNoiseScale As Single
        Get
            Return _毛玻璃噪点缩放
        End Get
        Set(value As Single)
            value = Math.Max(0.1F, value)
            If _毛玻璃噪点缩放 = value Then Return
            _毛玻璃噪点缩放 = value
            应用毛玻璃参数()
        End Set
    End Property

    Private _毛玻璃帧率 As Integer = 15
    <Category("LakeUI - Backdrop"), Description("Auto 模式常态刷新帧率 (0-60)。0 = 仅事件驱动（移动或调整大小结束 / 显示）。仅在 BackdropExcludeFromCapture=True 时生效；关闭该开关时强制纯事件驱动。"), DefaultValue(15)>
    Public Property BackdropFrameRate As Integer
        Get
            Return _毛玻璃帧率
        End Get
        Set(value As Integer)
            value = Math.Max(0, Math.Min(60, value))
            If _毛玻璃帧率 = value Then Return
            _毛玻璃帧率 = value
            For Each s In _forms.Values : 重置毛玻璃Tick(s) : Next
        End Set
    End Property

    Private _毛玻璃排除截屏 As Boolean = False
    ''' <summary>
    ''' Auto 模式下是否启用 <c>WDA_EXCLUDEFROMCAPTURE</c> 把本窗口排除在抓屏之外。
    ''' True — 安全防自照，可启用常态周期刷新；副作用：系统截图、屏幕共享、录屏均无法捕获本窗口。
    ''' False（默认） — 不启用 WDA，截图工具可以正常截到窗口；为防止"自己抓自己"产生递归反馈纹路，
    ''' 强制使用纯事件驱动刷新（移动或调整大小结束 / 显示），<see cref="BackdropFrameRate"/> 被忽略。
    ''' Image 模式与本属性无关：永远不抓屏、永远不启用 WDA。
    ''' </summary>
    <Category("LakeUI - Backdrop"), Description("Auto 模式下启用 WDA_EXCLUDEFROMCAPTURE 防自照（True 才允许周期刷新；副作用：系统截图截不到本窗口）。"), DefaultValue(False)>
    Public Property BackdropExcludeFromCapture As Boolean
        Get
            Return _毛玻璃排除截屏
        End Get
        Set(value As Boolean)
            If _毛玻璃排除截屏 = value Then Return
            _毛玻璃排除截屏 = value
            For Each s In _forms.Values
                应用毛玻璃状态(s)
            Next
        End Set
    End Property

    Private _边框自动颜色 As Boolean = False
    <Category("LakeUI - Backdrop"), Description("是否在毛玻璃模式下从背景平均色自动派生边框颜色（覆盖 BorderColor / BorderInactiveColor）。"), DefaultValue(False)>
    Public Property BorderAutoColor As Boolean
        Get
            Return _边框自动颜色
        End Get
        Set(value As Boolean)
            If _边框自动颜色 = value Then Return
            _边框自动颜色 = value : 通知重绘()
        End Set
    End Property

    Private _分层阴影自动颜色 As Boolean = False
    <Category("LakeUI - Backdrop"), Description("是否在毛玻璃模式下从背景平均色自动派生分层阴影颜色（覆盖 LayerShadowColor）。"), DefaultValue(False)>
    Public Property LayerShadowAutoColor As Boolean
        Get
            Return _分层阴影自动颜色
        End Get
        Set(value As Boolean)
            If _分层阴影自动颜色 = value Then Return
            _分层阴影自动颜色 = value
            For Each s In _forms.Values
                If s.ShadowForm IsNot Nothing Then s.ShadowForm.ForceReset()
                更新阴影(s)
            Next
        End Set
    End Property

    Private Sub 应用毛玻璃参数()
        For Each s In _forms.Values
            s.Renderer?.ApplyParameters(_毛玻璃模糊半径, _毛玻璃模糊次数, _毛玻璃下采样,
                                         _毛玻璃噪点缩放)
        Next
        通知重绘()
    End Sub

    Private Sub 应用毛玻璃状态(s As PerFormState)
        If s Is Nothing OrElse s.HostForm Is Nothing OrElse Not s.HostForm.IsHandleCreated Then Return
        Dim mode As BackdropModeEnum = _毛玻璃模式
        ' V5 backdrop 只接受显式图片源；桌面截图模式已移除。
        Dim shouldEnable As Boolean = 毛玻璃允许用于窗体(s) AndAlso mode = BackdropModeEnum.Image

        If shouldEnable Then
            If s.Renderer Is Nothing Then
                s.Renderer = New D3D_BackdropSurfaceRenderer(s.HostForm)
                s.Renderer.ApplyParameters(_毛玻璃模糊半径, _毛玻璃模糊次数, _毛玻璃下采样,
                                            _毛玻璃噪点缩放)
                AddHandler s.Renderer.AverageCommitted, Sub(sender2, ev2)
                                                            If _分层阴影自动颜色 Then
                                                                If s.ShadowForm IsNot Nothing Then s.ShadowForm.ForceReset()
                                                                更新阴影(s)
                                                            End If
                                                        End Sub
            End If
            ' 配置源。非 Image 模式清掉静态源图引用。
            s.Renderer.CleanupD2DResources(D3DCacheCleanupLevel.ReleaseAllCaches)
            s.Renderer.SetSource(mode = BackdropModeEnum.Image, If(mode = BackdropModeEnum.Image, _毛玻璃图片, Nothing))
            ' V5 不执行桌面截图；该兼容配置入口保持为空操作。
            s.Renderer.SetTransientExcludeOnCapture(
                (mode = BackdropModeEnum.Auto OrElse mode = BackdropModeEnum.CaptionOnly) AndAlso Not _毛玻璃排除截屏)
            ' 首帧
            s.Renderer.RequestFrame(获取毛玻璃捕获区域(s.HostForm), True)
            重置毛玻璃Tick(s)
        Else
            If s.BackdropTimer IsNot Nothing Then
                s.BackdropTimer.Stop()
                s.BackdropTimer.Dispose()
                s.BackdropTimer = Nothing
            End If
            If s.Renderer IsNot Nothing Then
                s.Renderer.Dispose()
                s.Renderer = Nothing
            End If
        End If
    End Sub

    Private Sub 重置毛玻璃Tick(s As PerFormState)
        If s Is Nothing Then Return

        ' 周期 Tick 仅在 Auto 模式 + 启用 BackdropExcludeFromCapture + 帧率 > 0 时启用：
        '   - None：未启用毛玻璃。
        '   - Image：源是静态图片，输出帧只取决于窗口尺寸（事件驱动即可：尺寸变化、显示）。
        '   - Auto 但未启用 BackdropExcludeFromCapture：抓屏依赖瞬时 WDA 切换防自照，
        '     而 SetWindowDisplayAffinity 的状态恢复需要数个 DWM 合成帧才能完成；高频翻转会
        '     让 DWM 长时间处于 EXCLUDE 状态，导致系统截图整体失效，违背开关初衷 ⇒ 强制纯事件驱动。
        '   - Auto + 长期 WDA + 帧率=0：用户显式选择纯事件驱动。
        Dim needTick As Boolean = False

        If Not needTick Then
            If s.BackdropTimer IsNot Nothing Then
                s.BackdropTimer.Stop()
                s.BackdropTimer.Dispose()
                s.BackdropTimer = Nothing
            End If
            Return
        End If

        Dim interval As Integer = Math.Max(16, FrameIntervalMilliseconds(_毛玻璃帧率))
        If s.BackdropTimer Is Nothing Then
            s.BackdropTimer = 创建UI精度计时器(s.HostForm, interval)
            AddHandler s.BackdropTimer.Tick, Sub(sender, ev) 毛玻璃Tick(s)
            s.BackdropTimer.Start()
        Else
            s.BackdropTimer.Interval = interval
            If Not s.BackdropTimer.IsRunning Then s.BackdropTimer.Start()
        End If
    End Sub

    Private Shared Function 创建UI精度计时器(owner As Control, interval As Integer) As PrecisionTimer
        Return New PrecisionTimer() With {
            .Interval = Math.Max(1, interval),
            .DispatchMode = PrecisionTimer.DispatchModeEnum.NonBlocking,
            .OverrunPolicy = PrecisionTimer.OverrunPolicyEnum.Drop,
            .WorkerThreadCount = 1,
            .SynchronizingObject = owner
        }
    End Function

    Private Shared Function FrameIntervalMilliseconds(fps As Integer) As Integer
        fps = Math.Max(1, fps)
        Return Math.Max(1, CInt(Math.Ceiling(1000.0R / fps)))
    End Function

    Private Sub 毛玻璃Tick(s As PerFormState)
        If s Is Nothing OrElse s.Renderer Is Nothing OrElse s.HostForm Is Nothing Then Return
        ' 防御性早返：仅 Auto / CaptionOnly 模式才需要常态周期重抓屏 + 重模糊。
        ' Image 模式源不变，理论上不会到达此处（重置毛玻璃Tick 已停 Timer），
        ' 但保留这层保护以防止模式切换时残留的 Timer Tick 触发无意义的重模糊。
        If _毛玻璃模式 <> BackdropModeEnum.Auto AndAlso _毛玻璃模式 <> BackdropModeEnum.CaptionOnly Then Return
        Dim frm = s.HostForm
        If frm.IsDisposed OrElse Not frm.Visible Then Return
        If frm.WindowState = FormWindowState.Minimized Then Return
        If s.IsInSizeMove Then Return
        s.Renderer.RequestFrame(获取毛玻璃捕获区域(frm), False)
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
    ''' <summary>Win32 自定义渐入 / 渐出动画的持续时间（毫秒），最小 50 毫秒。</summary>
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

    ''' <summary>当标题栏完成默认绘制后触发，便于宿主在标题栏上叠加自定义内容（例如徽章、标签）。</summary>
    Public Event CaptionPaint(sender As Object, e As CaptionPaintEventArgs)
    ''' <summary>当窗口的激活状态发生变化时触发，可用于联动外部 UI 的高亮 / 低亮显示。</summary>
    Public Event ActiveChanged(sender As Object, e As ActiveChangedEventArgs)
    ''' <summary>当指定窗体进入或退出全屏时触发。</summary>
    Public Event FullScreenChanged(sender As Object, e As FullScreenChangedEventArgs)
    ''' <summary>当默认命中测试结果为 HTCLIENT 时触发，允许将客户区某些区域识别为标题、按钮或调整边框。</summary>
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

    ''' <summary>当前已附加（通过 <see cref="Attach"/>）的所有窗体的只读快照集合。</summary>
    <Browsable(False)>
    Public ReadOnly Property AttachedForms As IReadOnlyList(Of Form)
        Get
            Dim list As New List(Of Form)(_forms.Count)
            For Each s In _forms.Values
                list.Add(s.HostForm)
            Next
            Return list
        End Get
    End Property

#End Region

#Region "全屏"

    ''' <summary>返回指定附加窗体当前是否处于全屏模式。</summary>
    Public Function IsFullScreen(targetForm As Form) As Boolean
        Dim s = 查找状态(targetForm)
        Return s IsNot Nothing AndAlso s.IsFullScreen
    End Function

    ''' <summary>让首个附加窗体进入覆盖当前显示器的无边框全屏模式。</summary>
    Public Sub EnterFullScreen(targetForm As Form)
        SetFullScreen(targetForm, True)
    End Sub

    ''' <summary>让指定附加窗体退出全屏并恢复进入前的窗口状态与边界。</summary>
    Public Sub ExitFullScreen(targetForm As Form)
        SetFullScreen(targetForm, False)
    End Sub

    ''' <summary>切换首个附加窗体的全屏状态。</summary>
    Public Sub ToggleFullScreen(targetForm As Form)
        Dim s = 查找状态(targetForm)
        If s Is Nothing Then Throw New InvalidOperationException("目标窗体尚未附加到 ThisIsYourWindow。")
        SetFullScreen(targetForm, Not s.IsFullScreen)
    End Sub

    ''' <summary>设置首个附加窗体的全屏状态。其他附加窗体不能进入全屏。</summary>
    Public Sub SetFullScreen(targetForm As Form, fullScreen As Boolean)
        ArgumentNullException.ThrowIfNull(targetForm)
        If targetForm.IsDisposed OrElse Not targetForm.IsHandleCreated Then Return
        If targetForm.InvokeRequired Then
            targetForm.Invoke(Sub() SetFullScreen(targetForm, fullScreen))
            Return
        End If

        Dim s = 查找状态(targetForm)
        If s Is Nothing Then Throw New InvalidOperationException("目标窗体尚未附加到 ThisIsYourWindow。")
        If fullScreen AndAlso Not 全屏允许用于窗体(s) Then
            Throw New InvalidOperationException("全屏功能仅对首个附加到 ThisIsYourWindow 的窗体开放。")
        End If
        If s.IsFullScreen = fullScreen Then Return

        If fullScreen Then
            进入全屏(s)
        Else
            退出全屏(s)
        End If
    End Sub

    Private Sub 进入全屏(s As PerFormState)
        If Not 全屏允许用于窗体(s) Then Return
        Dim frm = s.HostForm
        If frm Is Nothing OrElse frm.IsDisposed OrElse Not frm.IsHandleCreated Then Return

        Dim hWnd As IntPtr = frm.Handle
        Dim monitorBounds As Rectangle = Screen.FromHandle(hWnd).Bounds
        s.FullScreenOriginalWindowState = frm.WindowState
        If frm.WindowState = FormWindowState.Normal Then
            s.FullScreenOriginalBounds = 获取窗口屏幕矩形(frm)
        Else
            s.FullScreenOriginalBounds = frm.RestoreBounds
        End If
        If s.FullScreenOriginalBounds.Width <= 0 OrElse s.FullScreenOriginalBounds.Height <= 0 Then
            s.FullScreenOriginalBounds = 获取窗口屏幕矩形(frm)
        End If

        ' 全屏期间保持 Form.WindowState = Normal，避免最大化状态位与 WS_POPUP 混用，
        ' 否则退出全屏时 WinForms 可能不会重新执行最大化状态转换。
        If frm.WindowState <> FormWindowState.Normal Then frm.WindowState = FormWindowState.Normal
        s.FullScreenOriginalStyle = GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64()

        s.IsFullScreen = True
        s.FullScreenCaptionVisible = False
        停止全屏标题栏隐藏计时器(s)
        s.HoverHit = HTNOWHERE
        s.PressedHit = HTNOWHERE
        s.LayoutSignature = -1
        应用全屏窗口外观(s, monitorBounds)
        Dim cursorPoint As NATIVEPOINT
        If GetCursorPos(cursorPoint) Then
            处理全屏鼠标移动(s, frm.PointToClient(New Point(cursorPoint.X, cursorPoint.Y)))
        End If
        RaiseEvent FullScreenChanged(Me, New FullScreenChangedEventArgs(True, frm))
    End Sub

    Private Sub 应用全屏窗口外观(s As PerFormState, monitorBounds As Rectangle)
        Dim frm = s.HostForm
        If frm Is Nothing OrElse frm.IsDisposed OrElse Not frm.IsHandleCreated Then Return

        Dim hWnd As IntPtr = frm.Handle
        Dim style As Long = GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64()
        style = (style Or WS_POPUP) And Not CLng(WS_CAPTION) And Not CLng(WS_THICKFRAME)
        SetWindowLongPtr(hWnd, GWL_STYLE, New IntPtr(style))
        应用全屏Dwm窗口属性(hWnd)
        更新窗口内边距(s)
        SetWindowPos(hWnd, IntPtr.Zero,
                     monitorBounds.X, monitorBounds.Y, monitorBounds.Width, monitorBounds.Height,
                     CUInt(SWP_FRAMECHANGED Or SWP_NOOWNERZORDER))

        s.LayoutSignature = -1
        RecalculateButtonBounds(s)
        更新阴影(s)
        请求GPU渲染(frm, 获取真实客户区矩形(frm), True)
        If 毛玻璃当前启用(s) Then 请求毛玻璃帧(s, True, forceImageMode:=True)
    End Sub

    Private Shared Sub 应用全屏Dwm窗口属性(hWnd As IntPtr)
        Try
            Dim pref As Integer = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND
            Dim unused1 = DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, pref, 4)
            Dim colorNone As Integer = DWMWA_COLOR_NONE
            Dim unused2 = DwmSetWindowAttribute(hWnd, DWMWA_BORDER_COLOR, colorNone, 4)
            Dim margins As New MARGINS()
            Dim unused3 = DwmExtendFrameIntoClientArea(hWnd, margins)
        Catch
        End Try
    End Sub

    Private Sub 退出全屏(s As PerFormState)
        Dim frm = s.HostForm
        If frm Is Nothing OrElse frm.IsDisposed OrElse Not frm.IsHandleCreated Then Return

        Dim hWnd As IntPtr = frm.Handle
        Dim restoreBounds As Rectangle = s.FullScreenOriginalBounds
        Dim restoreState As FormWindowState = s.FullScreenOriginalWindowState
        s.IsFullScreen = False
        s.FullScreenCaptionVisible = False
        停止全屏标题栏隐藏计时器(s)
        s.HoverHit = HTNOWHERE
        s.PressedHit = HTNOWHERE
        s.LayoutSignature = -1

        If frm.WindowState <> FormWindowState.Normal Then frm.WindowState = FormWindowState.Normal
        SetWindowLongPtr(hWnd, GWL_STYLE, New IntPtr(s.FullScreenOriginalStyle))
        应用Dwm窗口属性(hWnd)
        更新窗口内边距(s)
        frm.Bounds = restoreBounds
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                     CUInt(SWP_FRAMECHANGED Or SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOZORDER Or SWP_NOOWNERZORDER))
        If restoreState <> FormWindowState.Normal Then frm.WindowState = restoreState

        s.FullScreenOriginalBounds = Rectangle.Empty
        ' 最大化/还原会经过 WM_SIZE 与 WM_SYSCOMMAND，并可能临时加入原生标题栏样式。
        ' 最后按控件当前属性统一收敛窗口样式、客户区内边距与自定义标题栏布局。
        Refresh(frm)
        If 毛玻璃当前启用(s) Then 请求毛玻璃帧(s, True, forceImageMode:=True)
        RaiseEvent FullScreenChanged(Me, New FullScreenChangedEventArgs(False, frm))
    End Sub

    Private Sub 处理全屏鼠标移动(s As PerFormState, clientPoint As Point)
        If s Is Nothing OrElse Not s.IsFullScreen Then Return

        Dim captionHeight As Integer = Math.Max(1, 取缩放标题栏高度(s.HostForm))
        Dim topEdgeTrigger As Boolean = (clientPoint.Y <= 1)
        Dim captionAreaTrigger As Boolean = (clientPoint.Y >= 0 AndAlso clientPoint.Y <= captionHeight)
        If topEdgeTrigger OrElse captionAreaTrigger Then
            显示全屏标题栏(s)
        ElseIf s.FullScreenCaptionVisible AndAlso clientPoint.Y > captionHeight Then
            启动全屏标题栏隐藏计时器(s)
        ElseIf s.FullScreenCaptionVisible Then
            停止全屏标题栏隐藏计时器(s)
        End If
    End Sub

    Private Sub 显示全屏标题栏(s As PerFormState)
        If s Is Nothing OrElse Not s.IsFullScreen Then Return
        停止全屏标题栏隐藏计时器(s)
        If s.FullScreenCaptionVisible Then Return
        s.FullScreenCaptionVisible = True
        s.LayoutSignature = -1
        更新窗口内边距(s)
        s.HostForm.PerformLayout()
        RecalculateButtonBounds(s)
        请求GPU渲染(s.HostForm, 获取真实客户区矩形(s.HostForm), True)
    End Sub

    Private Sub 启动全屏标题栏隐藏计时器(s As PerFormState)
        If s Is Nothing OrElse Not s.IsFullScreen OrElse Not s.FullScreenCaptionVisible Then Return
        If s.FullScreenCaptionHideTimer IsNot Nothing Then Return
        s.FullScreenCaptionHideTimer = New Timer With {.Interval = 900}
        AddHandler s.FullScreenCaptionHideTimer.Tick,
            Sub(sender, e)
                If s.HostForm Is Nothing OrElse s.HostForm.IsDisposed OrElse Not s.IsFullScreen Then
                    停止全屏标题栏隐藏计时器(s)
                    Return
                End If
                Dim p As NATIVEPOINT
                If Not GetCursorPos(p) Then Return
                Dim clientPoint As Point = s.HostForm.PointToClient(New Point(p.X, p.Y))
                If clientPoint.Y > Math.Max(1, 取缩放标题栏高度(s.HostForm)) Then
                    s.FullScreenCaptionVisible = False
                    s.LayoutSignature = -1
                    停止全屏标题栏隐藏计时器(s)
                    更新窗口内边距(s)
                    s.HostForm.PerformLayout()
                    RecalculateButtonBounds(s)
                    请求GPU渲染(s.HostForm, 获取真实客户区矩形(s.HostForm), True)
                Else
                    停止全屏标题栏隐藏计时器(s)
                End If
            End Sub
        s.FullScreenCaptionHideTimer.Start()
    End Sub

    Private Shared Sub 停止全屏标题栏隐藏计时器(s As PerFormState)
        If s Is Nothing OrElse s.FullScreenCaptionHideTimer Is Nothing Then Return
        s.FullScreenCaptionHideTimer.Stop()
        s.FullScreenCaptionHideTimer.Dispose()
        s.FullScreenCaptionHideTimer = Nothing
    End Sub

    Private Sub 注册键盘过滤器()
        If _消息过滤器已注册 Then Return
        Application.AddMessageFilter(Me)
        _消息过滤器已注册 = True
    End Sub

    Private Sub 注销键盘过滤器()
        If Not _消息过滤器已注册 Then Return
        Application.RemoveMessageFilter(Me)
        _消息过滤器已注册 = False
    End Sub

    Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
        If m.Msg = WM_MOUSEMOVE OrElse m.Msg = WM_NCMOUSEMOVE Then
            Dim mouseState = 查找消息所属状态(m.HWnd)
            Dim cursorPoint As NATIVEPOINT
            Dim hasCursorPoint As Boolean = GetCursorPos(cursorPoint)
            ' 某些原生/特殊子窗口会吞掉句柄到 Form 的托管映射；全屏窗体覆盖
            ' 显示器时，改用光标屏幕坐标兜底识别，确保标题栏热区仍可唤出。
            If (mouseState Is Nothing OrElse Not mouseState.IsFullScreen) AndAlso hasCursorPoint Then
                Dim screenPoint As New Point(cursorPoint.X, cursorPoint.Y)
                For Each candidate In _forms.Values
                    If candidate Is Nothing OrElse Not candidate.IsFullScreen OrElse
                       candidate.HostForm Is Nothing OrElse candidate.HostForm.IsDisposed Then Continue For
                    If 获取窗口屏幕矩形(candidate.HostForm).Contains(screenPoint) Then
                        mouseState = candidate
                        Exit For
                    End If
                Next
            End If
            If mouseState IsNot Nothing AndAlso mouseState.IsFullScreen Then
                If hasCursorPoint Then
                    处理全屏鼠标移动(mouseState,
                                     mouseState.HostForm.PointToClient(New Point(cursorPoint.X, cursorPoint.Y)))
                End If
            End If
            Return False
        End If
        If m.Msg <> WM_KEYDOWN AndAlso m.Msg <> WM_SYSKEYDOWN Then Return False
        If (m.LParam.ToInt64() And &H40000000L) <> 0 Then Return False

        Dim keyCode As Keys = CType(m.WParam.ToInt32() And &HFFFF, Keys)
        If keyCode <> Keys.Escape AndAlso keyCode <> Keys.F11 Then Return False

        Dim activeState As PerFormState = Nothing
        For Each state In _forms.Values
            Dim frm = state.HostForm
            If frm IsNot Nothing AndAlso Not frm.IsDisposed AndAlso
               (frm.ContainsFocus OrElse ReferenceEquals(Form.ActiveForm, frm)) Then
                activeState = state
                Exit For
            End If
        Next
        If activeState Is Nothing Then Return False

        If keyCode = Keys.Escape Then
            If Not activeState.IsFullScreen Then Return False
            退出全屏(activeState)
            Return True
        End If

        If activeState.IsFullScreen Then
            退出全屏(activeState)
            Return True
        End If
        If Not _显示全屏按钮 OrElse Not 全屏允许用于窗体(activeState) Then Return False
        进入全屏(activeState)
        Return True
    End Function

    Private Function 查找消息所属状态(hWnd As IntPtr) As PerFormState
        Dim direct As PerFormState = Nothing
        If _forms.TryGetValue(hWnd, direct) Then Return direct

        Dim sourceControl As Control = Control.FromHandle(hWnd)
        Dim form = sourceControl?.FindForm()
        If form Is Nothing Then Return Nothing
        Return 查找状态(form)
    End Function

#End Region

#Region "按钮区域计算"

    Friend Sub RecalculateButtonBounds(s As PerFormState)
        If s Is Nothing Then Return
        Dim form = s.HostForm
        If _useGpuChromeOverlay AndAlso Not s.ChromeOverlayActive AndAlso
           form IsNot Nothing AndAlso form.IsHandleCreated Then
            If CreateChromeOverlays(s) Then
                RemoveHandler form.Paint, AddressOf 宿主窗口_Paint
            End If
        End If
        If _标题栏绑定控件 IsNot Nothing AndAlso _标题栏控件宿主窗体 Is Nothing Then
            _标题栏控件宿主窗体 = form
        End If
        Dim w As Integer = 获取真实客户区尺寸(form).Width
        Dim bdr As Integer = 取缩放边框厚度(form)
        Dim bw As Integer = Math.Max(缩放逻辑尺寸(form, 16), 缩放逻辑尺寸(form, _按钮宽度))
        Dim captionLayoutRect As Rectangle = 获取标题栏布局矩形(form)
        Dim bh As Integer = captionLayoutRect.Height
        Dim sp As Integer = Math.Max(0, 缩放逻辑尺寸(form, _按钮间距))
        Dim iconSize As Integer = Math.Max(0, 缩放逻辑尺寸(form, _图标大小))
        Dim iconPadding As Padding = 缩放逻辑内边距(form, _图标内边距)
        Dim captionPadding As Padding = 缩放逻辑内边距(form, _标题栏内容内边距)
        Dim hasMin As Boolean = s.HostForm.MinimizeBox
        Dim hasMax As Boolean = s.HostForm.MaximizeBox
        Dim hasFullScreen As Boolean = _显示全屏按钮 AndAlso 全屏允许用于窗体(s)
        Dim posRight As Boolean = (_按钮位置 = ButtonPositionEnum.Right)
        Dim iconNone As Boolean = (_图标来源 = IconSourceEnum.None OrElse Not s.HostForm.ShowIcon)

        ' 布局签名：所有影响按钮/图标位置的输入生成哈希，避免手工 bit-pack 截断导致缓存误命中。
        Dim sig As Long = HashCode.Combine(w, D3D_DpiContext.FromControl(form).Dpi, bdr, bw, bh, sp, iconSize, iconPadding)
        sig = HashCode.Combine(sig, captionPadding, hasMin)
        sig = HashCode.Combine(sig, hasMax, hasFullScreen, posRight, iconNone,
                               s.IsFullScreen, s.FullScreenCaptionVisible)
        If s.LayoutSignature = sig Then
            同步标题栏绑定控件布局(s)
            更新ChromeOverlays(s)
            Return
        End If
        s.LayoutSignature = sig

        s.CloseRect = Rectangle.Empty
        s.MaxRect = Rectangle.Empty
        s.MinRect = Rectangle.Empty
        s.FullScreenRect = Rectangle.Empty
        s.IconRect = Rectangle.Empty
        If (s.IsFullScreen AndAlso Not s.FullScreenCaptionVisible) OrElse
           captionLayoutRect.Width <= 0 OrElse captionLayoutRect.Height <= 0 Then
            同步标题栏绑定控件布局(s)
            更新ChromeOverlays(s)
            Return
        End If

        ' 用栈数组替代 List(Of Integer)，避免装箱 + 集合分配。
        Dim 列表(3) As Integer
        Dim 数量 As Integer = 0
        If posRight Then
            If hasFullScreen Then 列表(数量) = HTFULLSCREEN : 数量 += 1
            If hasMin Then 列表(数量) = HTMINBUTTON : 数量 += 1
            If hasMax Then 列表(数量) = HTMAXBUTTON : 数量 += 1
            列表(数量) = HTCLOSE : 数量 += 1
            Dim totalW As Integer = 数量 * bw + Math.Max(0, 数量 - 1) * sp
            Dim startX As Integer = captionLayoutRect.Right - totalW
            For i = 0 To 数量 - 1
                Dim r As New Rectangle(startX + i * (bw + sp), captionLayoutRect.Top, bw, bh)
                Select Case 列表(i) : Case HTCLOSE : s.CloseRect = r : Case HTMAXBUTTON : s.MaxRect = r : Case HTMINBUTTON : s.MinRect = r : Case HTFULLSCREEN : s.FullScreenRect = r : End Select
            Next
        Else
            列表(数量) = HTCLOSE : 数量 += 1
            If hasMax Then 列表(数量) = HTMAXBUTTON : 数量 += 1
            If hasMin Then 列表(数量) = HTMINBUTTON : 数量 += 1
            If hasFullScreen Then 列表(数量) = HTFULLSCREEN : 数量 += 1
            For i = 0 To 数量 - 1
                Dim r As New Rectangle(captionLayoutRect.Left + i * (bw + sp), captionLayoutRect.Top, bw, bh)
                Select Case 列表(i) : Case HTCLOSE : s.CloseRect = r : Case HTMAXBUTTON : s.MaxRect = r : Case HTMINBUTTON : s.MinRect = r : Case HTFULLSCREEN : s.FullScreenRect = r : End Select
            Next
        End If
        If Not hasMax Then s.MaxRect = Rectangle.Empty
        If Not hasMin Then s.MinRect = Rectangle.Empty
        If Not hasFullScreen Then s.FullScreenRect = Rectangle.Empty

        If Not iconNone AndAlso iconSize > 0 Then
            Dim totalBtnW As Integer = 数量 * bw + Math.Max(0, 数量 - 1) * sp
            Dim iconAreaLeft As Integer = If(posRight, captionLayoutRect.Left, captionLayoutRect.Left + totalBtnW)
            Dim iconAreaRight As Integer = If(posRight, captionLayoutRect.Right - totalBtnW, captionLayoutRect.Right)
            Dim availableWidth As Integer = Math.Max(0, iconAreaRight - iconAreaLeft - iconPadding.Horizontal)
            Dim availableHeight As Integer = Math.Max(0, captionLayoutRect.Height - iconPadding.Vertical)
            Dim drawSize As Integer = Math.Min(iconSize, Math.Min(availableWidth, availableHeight))
            If drawSize > 0 Then
                Dim iconX As Integer = iconAreaLeft + iconPadding.Left
                Dim iconY As Integer = captionLayoutRect.Top + iconPadding.Top + (availableHeight - drawSize) \ 2
                s.IconRect = New Rectangle(iconX, iconY, drawSize, drawSize)
            End If
        End If
        同步标题栏绑定控件布局(s)
        更新ChromeOverlays(s)
    End Sub

#End Region

#Region "绘制"

    Private Function 获取标题栏内容矩形(form As Form) As Rectangle
        If form Is Nothing Then Return Rectangle.Empty
        Dim size = 获取真实客户区尺寸(form)
        Return 获取标题栏内容矩形(form, size.Width, size.Height)
    End Function

    Private Function 获取标题栏内容矩形(form As Form, w As Integer, h As Integer) As Rectangle
        Dim state = 查找状态(form)
        If state IsNot Nothing AndAlso state.IsFullScreen Then
            If Not state.FullScreenCaptionVisible Then Return Rectangle.Empty
            Return New Rectangle(0, 0, Math.Max(0, w), Math.Min(取缩放标题栏高度(form), Math.Max(0, h)))
        End If
        Dim bdr As Integer = 取缩放边框厚度(form)
        Dim x As Integer = Math.Min(bdr, Math.Max(0, w))
        Dim y As Integer = Math.Min(bdr, Math.Max(0, h))
        Dim rw As Integer = Math.Max(0, w - bdr * 2)
        Dim rh As Integer = Math.Min(取缩放标题栏高度(form), Math.Max(0, h - bdr * 2))
        Return New Rectangle(x, y, rw, rh)
    End Function

    Private Function 获取标题栏布局矩形(form As Form) As Rectangle
        If form Is Nothing Then Return Rectangle.Empty
        Dim size = 获取真实客户区尺寸(form)
        Dim captionRect As Rectangle = 获取标题栏内容矩形(form, size.Width, size.Height)
        Dim bottomLineHeight As Integer = Math.Min(captionRect.Height, 取缩放标题栏底部横线高度(form))
        captionRect.Height = Math.Max(0, captionRect.Height - bottomLineHeight)
        Return 应用内边距(captionRect, 缩放逻辑内边距(form, _标题栏内容内边距))
    End Function

    Friend Sub RenderGpuWindow(context As D3D_PaintContext, targetForm As Form)
        RenderGpuWindowCore(context, targetForm, Size.Empty)
    End Sub

    Private Function RenderGpuClientBackdrop(context As D3D_PaintContext, targetForm As Form) As Boolean
        Dim s = 查找状态(targetForm)
        If context Is Nothing OrElse s Is Nothing Then Return False
        Dim realSize = 获取真实客户区尺寸(targetForm)
        Dim w = Math.Max(1, realSize.Width)
        Dim h = Math.Max(1, realSize.Height)
        Dim fullRect As New RectangleF(0, 0, w, h)
        Dim captionRect = 获取标题栏内容矩形(targetForm, w, h)
        Dim captionRectF As New RectangleF(captionRect.X, captionRect.Y, captionRect.Width, captionRect.Height)
        Dim drew = 绘制毛玻璃背景_GPU(context, s, fullRect, captionRectF,
                                             视觉上保持激活(targetForm, s.Activated))
        If Not drew AndAlso targetForm.BackColor.A > 0 Then
            context.FillRectangle(fullRect, targetForm.BackColor)
            drew = True
        End If
        Return drew
    End Function

    Friend Sub RenderGpuWindowViewport(context As D3D_PaintContext,
                                       targetForm As Form,
                                       viewportOrigin As Point,
                                       viewportSize As Size)
        If context Is Nothing OrElse targetForm Is Nothing Then Return
        Dim oldTransform = context.DeviceContext.Transform
        Try
            context.DeviceContext.Transform = Matrix3x2.CreateTranslation(-viewportOrigin.X, -viewportOrigin.Y) * oldTransform
            RenderGpuWindowCore(context, targetForm, viewportSize)
        Finally
            context.DeviceContext.Transform = oldTransform
        End Try
    End Sub

    Private Sub RenderGpuWindowCore(context As D3D_PaintContext,
                                    targetForm As Form,
                                    viewportSize As Size)
        Dim s = 查找状态(targetForm)
        If context Is Nothing OrElse s Is Nothing Then Return
        ' 标题栏布局必须使用整窗的真实客户区尺寸，不能使用 overlay 自身的裁剪尺寸。
        ' 最小化还原期间 WinForms 可能短暂报告 0 或过渡尺寸；此时跳过一帧，
        ' 防止按钮矩形按 overlay 尺寸重算后瞬间拉伸到整个标题栏。
        Dim 真实尺寸 = 获取真实客户区尺寸(targetForm)
        If targetForm.WindowState = FormWindowState.Minimized Then Return
        If 真实尺寸.Width <= 0 OrElse 真实尺寸.Height <= 0 Then
            If viewportSize.Width <= 0 OrElse viewportSize.Height <= 0 Then Return
            真实尺寸 = viewportSize
        End If
        RecalculateButtonBounds(s)

        Dim w As Integer = 真实尺寸.Width
        Dim h As Integer = 真实尺寸.Height

        Dim active As Boolean = 视觉上保持激活(targetForm, s.Activated)
        Dim fullRect As New RectangleF(0, 0, w, h)
        Dim captionRect As Rectangle = 获取标题栏内容矩形(s.HostForm, w, h)
        Dim captionRectF As New RectangleF(captionRect.X, captionRect.Y, captionRect.Width, captionRect.Height)
        Dim drewBackdrop As Boolean = 绘制毛玻璃背景_GPU(context, s, fullRect, captionRectF, active)

        If Not drewBackdrop AndAlso captionRect.Width > 0 AndAlso captionRect.Height > 0 Then
            Dim capColor As Color = If(active, _标题栏背景颜色, _标题栏失焦背景颜色)
            If capColor.A > 0 Then context.FillRectangle(captionRectF, capColor)
        End If

        If _标题栏背景图片 IsNot Nothing AndAlso captionRect.Width > 0 AndAlso captionRect.Height > 0 Then
            绘制Cover图片_GPU(context, _标题栏背景图片, captionRectF)
        End If

        If _标题栏遮罩颜色.A > 0 AndAlso captionRect.Width > 0 AndAlso captionRect.Height > 0 Then
            context.FillRectangle(captionRectF, _标题栏遮罩颜色)
        End If

        绘制标题栏底部横线_GPU(context, s, captionRect)
        绘制图标_GPU(context, s)
        绘制控制按钮_GPU(context, s, s.CloseRect, HTCLOSE)
        If _显示全屏按钮 AndAlso 全屏允许用于窗体(s) Then
            绘制控制按钮_GPU(context, s, s.FullScreenRect, HTFULLSCREEN)
        End If
        If s.HostForm.MaximizeBox Then 绘制控制按钮_GPU(context, s, s.MaxRect, HTMAXBUTTON)
        If s.HostForm.MinimizeBox Then 绘制控制按钮_GPU(context, s, s.MinRect, HTMINBUTTON)
        绘制窗口边框_GPU(context, s, w, h, active)
        绘制标题文字_GPU(context, s)
    End Sub

    Private Function 绘制毛玻璃背景_GPU(context As D3D_PaintContext,
                                 s As PerFormState,
                                 fullRect As RectangleF,
                                 captionRect As RectangleF,
                                 active As Boolean) As Boolean
        If context Is Nothing OrElse s Is Nothing Then Return False
        If _毛玻璃模式 = BackdropModeEnum.None Then Return False
        If Not 毛玻璃允许用于窗体(s) Then Return False

        ' An empty inactive tint keeps the active composition, so focus changes do
        ' not trigger a different backdrop overlay when the surface is repainted.
        Dim tint As Color = If(active OrElse _毛玻璃Tint失焦颜色.IsEmpty,
                               _毛玻璃Tint颜色,
                               _毛玻璃Tint失焦颜色)

        Select Case _毛玻璃模式
            Case BackdropModeEnum.Image
                If _毛玻璃图片 Is Nothing Then Return False
                Dim renderer = context.Compositor.BackdropRenderer
                renderer.SetImage(_毛玻璃图片)
                renderer.ApplyParameters(_毛玻璃模糊半径, _毛玻璃模糊次数, _毛玻璃下采样, _毛玻璃噪点缩放)
                renderer.TintColor = tint
                renderer.NoiseOpacity = _毛玻璃噪点不透明度
                renderer.DrawImageBackdrop(context, fullRect)
                Return True

            Case BackdropModeEnum.Auto
                If s.Renderer Is Nothing OrElse Not s.Renderer.HasFrame Then Return False
                Return s.Renderer.DrawTo(context, fullRect, tint, _毛玻璃噪点不透明度)

            Case BackdropModeEnum.CaptionOnly
                If s.Renderer Is Nothing OrElse Not s.Renderer.HasFrame Then Return False
                If captionRect.Width <= 0 OrElse captionRect.Height <= 0 Then Return False
                Return s.Renderer.DrawTo(context, captionRect, tint, _毛玻璃噪点不透明度)
        End Select

        Return False
    End Function

    Private Sub 绘制Cover图片_GPU(context As D3D_PaintContext, image As Image, bounds As RectangleF)
        If context Is Nothing OrElse image Is Nothing Then Return
        If image.Width <= 0 OrElse image.Height <= 0 OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return

        Dim ratio As Single = Math.Max(bounds.Width / image.Width, bounds.Height / image.Height)
        If ratio <= 0 Then Return
        Dim drawW As Single = image.Width * ratio
        Dim drawH As Single = image.Height * ratio
        Dim dest As New RectangleF(
            bounds.X + (bounds.Width - drawW) / 2.0F,
            bounds.Y + (bounds.Height - drawH) / 2.0F,
            drawW,
            drawH)

        Using context.PushClip(bounds)
            context.DrawImage(image, dest)
        End Using
    End Sub

    Private Sub 绘制图标_GPU(context As D3D_PaintContext, s As PerFormState)
        If s Is Nothing OrElse s.HostForm Is Nothing OrElse Not s.HostForm.ShowIcon OrElse
           _图标来源 = IconSourceEnum.None OrElse s.IconRect.IsEmpty Then Return

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
        If img Is Nothing Then Return

        Dim r = s.IconRect
        context.DrawImage(img, New RectangleF(r.X, r.Y, r.Width, r.Height), Nothing, 1.0F, 0, Vortice.Direct2D1.InterpolationMode.HighQualityCubic)
    End Sub

    Private Sub 绘制控制按钮_GPU(context As D3D_PaintContext, s As PerFormState, rect As Rectangle, htValue As Integer)
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

        Dim buttonPadding As Padding = 缩放逻辑内边距(s.HostForm, _按钮内边距)
        Dim visualRect As Rectangle = 应用内边距(rect, buttonPadding)
        Dim vis As New RectangleF(visualRect.X, visualRect.Y, visualRect.Width, visualRect.Height)
        If vis.Width <= 0 OrElse vis.Height <= 0 Then Return

        If bgColor.A > 0 Then
            Dim r As Integer = Math.Min(Math.Max(0, 缩放逻辑尺寸(s.HostForm, _按钮圆角半径)), CInt(Math.Min(vis.Width, vis.Height)) \ 2)
            Dim bgBrush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, bgColor, context.DeviceGeneration)
            If r > 0 Then
                context.FillRoundedRectangle(vis, r, bgBrush)
            Else
                context.DeviceContext.FillRectangle(D3D_PaintContext.ToRawRect(vis), bgBrush)
            End If
        End If

        If symColor.A = 0 Then Return
        Dim sz As Integer = Math.Max(缩放逻辑尺寸(s.HostForm, 4), 缩放逻辑尺寸(s.HostForm, _按钮符号大小))
        Dim cx As Single = vis.X + (vis.Width - sz) / 2.0F
        Dim cy As Single = vis.Y + (vis.Height - sz) / 2.0F
        Dim lw As Single = Math.Max(0.5F * 取Dpi缩放(s.HostForm), 缩放逻辑尺寸(s.HostForm, _按钮符号线宽))
        Dim pen = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, symColor, context.DeviceGeneration)

        Select Case htValue
            Case HTCLOSE
                context.DeviceContext.DrawLine(New Vector2(cx, cy), New Vector2(cx + sz, cy + sz), pen, lw)
                context.DeviceContext.DrawLine(New Vector2(cx + sz, cy), New Vector2(cx, cy + sz), pen, lw)
            Case HTMAXBUTTON
                If 窗口当前已最大化(s.HostForm) Then
                    Dim off As Single = sz * 0.25F
                    context.DeviceContext.DrawRectangle(New Vortice.Mathematics.Rect(cx + off, cy, sz - off, sz - off), pen, lw)
                    context.DeviceContext.DrawRectangle(New Vortice.Mathematics.Rect(cx, cy + off, sz - off, sz - off), pen, lw)
                Else
                    context.DeviceContext.DrawRectangle(New Vortice.Mathematics.Rect(cx, cy, sz, sz), pen, lw)
                End If
            Case HTMINBUTTON
                Dim mid As Single = cy + sz / 2.0F
                context.DeviceContext.DrawLine(New Vector2(cx, mid), New Vector2(cx + sz, mid), pen, lw)
            Case HTFULLSCREEN
                Dim fullScreenLogicalLineWidth As Single = Math.Max(1.0F, _按钮符号线宽 - 1.0F)
                Dim fullScreenLineWidth As Single = 缩放逻辑尺寸(s.HostForm, fullScreenLogicalLineWidth)
                绘制全屏按钮符号_GPU(context, pen, fullScreenLineWidth, cx, cy, sz, s.IsFullScreen)
        End Select
    End Sub

    Private Shared Sub 绘制全屏按钮符号_GPU(context As D3D_PaintContext,
                                      pen As ID2D1SolidColorBrush,
                                      lineWidth As Single,
                                      x As Single,
                                      y As Single,
                                      size As Single,
                                      restore As Boolean)
        ' The stroke center stays half a line inside the requested glyph box, so its outer pixels
        ' exactly reach the ButtonGlyphSize boundary without being clipped.
        Dim edge As Single = Math.Max(0.5F, lineWidth / 2.0F)
        Dim head As Single = Math.Max(lineWidth * 1.5F, size * 0.28F)
        Dim center As Single = size / 2.0F
        Dim gap As Single = Math.Max(0.5F, size * 0.035F)
        Dim corners() As Vector2 = {
            New Vector2(x + edge, y + edge),
            New Vector2(x + size - edge, y + edge),
            New Vector2(x + edge, y + size - edge),
            New Vector2(x + size - edge, y + size - edge)}
        Dim inner() As Vector2 = {
            New Vector2(x + center - gap, y + center - gap),
            New Vector2(x + center + gap, y + center - gap),
            New Vector2(x + center - gap, y + center + gap),
            New Vector2(x + center + gap, y + center + gap)}

        For i As Integer = 0 To 3
            Dim arrowStart As Vector2 = If(restore, corners(i), inner(i))
            Dim arrowEnd As Vector2 = If(restore, inner(i), corners(i))
            context.DeviceContext.DrawLine(arrowStart, arrowEnd, pen, lineWidth)

            Dim sx As Single = If((i And 1) = 0, 1.0F, -1.0F)
            Dim sy As Single = If((i And 2) = 0, 1.0F, -1.0F)
            If restore Then
                sx = -sx
                sy = -sy
            End If
            context.DeviceContext.DrawLine(arrowEnd, New Vector2(arrowEnd.X + sx * head, arrowEnd.Y), pen, lineWidth)
            context.DeviceContext.DrawLine(arrowEnd, New Vector2(arrowEnd.X, arrowEnd.Y + sy * head), pen, lineWidth)
        Next
    End Sub

    Private Sub 绘制窗口边框_GPU(context As D3D_PaintContext, s As PerFormState, w As Integer, h As Integer, active As Boolean)
        If s.IsFullScreen Then Return
        Dim scaledBorderSize As Integer = 取缩放边框厚度(s.HostForm)
        If scaledBorderSize <= 0 Then Return

        Dim bdrColor As Color = If(active, _边框颜色, _边框失焦颜色)
        If bdrColor.A = 0 Then Return

        Dim bdr As Integer = Math.Min(scaledBorderSize, Math.Max(0, Math.Min(w, h)))
        If bdr <= 0 Then Return

        ' DWM 只负责最终窗口裁切，边框始终由 LakeUI 自绘；这样边框与内容使用同一套 GPU 几何，
        ' 不会在圆角处叠加系统边框的第二层抗锯齿。
        If 当前使用圆角模式(s) Then
            Dim logicalRadius As Single = DwmWindowStyle.GetCornerRadiusLogical(_窗口圆角模式)
            Dim outerRadius As Single = Math.Max(1.0F, CSng(缩放逻辑尺寸(s.HostForm, logicalRadius)))
            Dim stroke As Single = CSng(bdr)
            Dim inset As Single = stroke / 2.0F
            Dim strokeRadius As Single = Math.Max(0.0F, outerRadius - inset)
            Dim rect As New RectangleF(inset, inset, Math.Max(0.0F, w - stroke), Math.Max(0.0F, h - stroke))
            If rect.Width > 0 AndAlso rect.Height > 0 Then context.DrawRoundedRectangle(rect, strokeRadius, bdrColor, stroke)
            Return
        End If

        context.FillRectangle(New RectangleF(0, 0, w, Math.Min(bdr, h)), bdrColor)
        If h > bdr Then context.FillRectangle(New RectangleF(0, h - bdr, w, bdr), bdrColor)

        Dim sideH As Integer = h - bdr * 2
        If sideH <= 0 Then Return
        context.FillRectangle(New RectangleF(0, bdr, Math.Min(bdr, w), sideH), bdrColor)
        If w > bdr Then context.FillRectangle(New RectangleF(w - bdr, bdr, bdr, sideH), bdrColor)
    End Sub

    Private Sub 绘制标题栏底部横线_GPU(context As D3D_PaintContext, s As PerFormState, captionRect As Rectangle)
        If context Is Nothing OrElse s Is Nothing OrElse captionRect.Width <= 0 OrElse captionRect.Height <= 0 Then Return

        Dim lineHeight As Integer = Math.Min(captionRect.Height, 取缩放标题栏底部横线高度(s.HostForm))
        If lineHeight <= 0 OrElse _标题栏底部横线颜色.A = 0 Then Return

        context.FillRectangle(New RectangleF(captionRect.Left,
                                             captionRect.Bottom - lineHeight,
                                             captionRect.Width,
                                             lineHeight),
                              _标题栏底部横线颜色)
    End Sub

    Private Sub 绘制标题文字_GPU(context As D3D_PaintContext, s As PerFormState)
        Dim text As String = 获取标题栏渲染文本(s.HostForm)
        If String.IsNullOrEmpty(text) Then Return

        Dim font As Font = If(_标题文字字体, s.HostForm.Font)
        If font Is Nothing Then Return

        Dim fgColor As Color = If(视觉上保持激活(s.HostForm, s.Activated), _标题文字颜色, _标题文字失焦颜色)
        If fgColor.A = 0 Then Return

        Dim textRect As RectangleF = 获取标题文字布局矩形(s)
        If textRect.Width <= 0 OrElse textRect.Height <= 0 Then Return
        text = 获取省略标题文字(s, text, font, textRect.Width)
        If String.IsNullOrEmpty(text) Then Return

        Dim flags As TextFormatFlags = TextFormatFlags.VerticalCenter Or
                                       TextFormatFlags.SingleLine Or
                                       TextFormatFlags.EndEllipsis Or
                                       TextFormatFlags.NoPadding
        Select Case _标题文字对齐
            Case TitleAlignEnum.Center : flags = flags Or TextFormatFlags.HorizontalCenter
            Case TitleAlignEnum.Right : flags = flags Or TextFormatFlags.Right
            Case Else : flags = flags Or TextFormatFlags.Left
        End Select

        context.DrawText(text, font, fgColor, textRect, flags)
    End Sub

    Private Function 获取省略标题文字(s As PerFormState, text As String, font As Font, maxWidth As Single) As String
        Dim width As Integer = Math.Max(0, CInt(Math.Floor(maxWidth)))
        Dim signature As Integer = HashCode.Combine(text, font.FontFamily.Name, font.SizeInPoints,
                                                    font.Style, width, D3D_DpiContext.FromControl(s.HostForm).Dpi)
        If s.TitleEllipsisSignature = signature Then Return s.TitleDisplayText

        Dim result As String = text
        Dim scale As Single = 取Dpi缩放(s.HostForm)
        If width <= 0 Then
            result = String.Empty
        ElseIf D3D_TextMeasureHelper.MeasureTextWidth_D2D(text, font, scale) > width Then
            Const ellipsis As String = "…"
            If D3D_TextMeasureHelper.MeasureTextWidth_D2D(ellipsis, font, scale) > width Then
                result = String.Empty
            Else
                Dim elementStarts As Integer() = Globalization.StringInfo.ParseCombiningCharacters(text)
                Dim low As Integer = 0
                Dim high As Integer = elementStarts.Length
                While low < high
                    Dim middle As Integer = (low + high + 1) \ 2
                    Dim charLength As Integer = If(middle >= elementStarts.Length, text.Length, elementStarts(middle))
                    Dim candidate As String = text.Substring(0, charLength) & ellipsis
                    If D3D_TextMeasureHelper.MeasureTextWidth_D2D(candidate, font, scale) <= width Then
                        low = middle
                    Else
                        high = middle - 1
                    End If
                End While
                Dim fittedLength As Integer = If(low >= elementStarts.Length, text.Length, elementStarts(low))
                result = text.Substring(0, fittedLength) & ellipsis
            End If
        End If

        s.TitleEllipsisSignature = signature
        s.TitleDisplayText = result
        Return result
    End Function

    ''' <summary>
    ''' 请求指定窗体的 V5 chrome overlay 呈现。
    ''' </summary>
    Public Sub PaintWindow(e As PaintEventArgs, targetForm As Form)
        If targetForm Is Nothing OrElse targetForm.IsDisposed Then Return
        Dim state = 查找状态(targetForm)
        If state IsNot Nothing AndAlso state.ChromeOverlayActive Then
            更新ChromeOverlays(state)
        End If
    End Sub

    Private Function TryPaintWindowChrome(e As PaintEventArgs, targetForm As Form) As Boolean
        ' V5 chrome 仅通过子 HWND overlay 呈现；Form Paint 不再承载 GPU 或 HDC 路线。
        Return False
    End Function

    ''' <summary>
    ''' Caption/border child HWND used by the V5 chrome path. It never receives input:
    ''' WM_NCHITTEST is returned as HTTRANSPARENT so the existing Form interceptor keeps
    ''' ownership of drag, resize and system-button hit testing.
    ''' </summary>
    Friend NotInheritable Class ChromeOverlayControl
        Inherits Control
        Implements D3D_IGpuRenderable, V5_IGpuPresentationSource,
                   V5_IGeometryUpdateSource, V5_ICoalescedPresentationSource

        Private Const WM_NCHITTEST As Integer = &H84
        Private Const WM_MOUSEACTIVATE As Integer = &H21
        Private Const HTTRANSPARENT As Integer = -1
        Private Const MA_NOACTIVATE As Integer = 3
        Private ReadOnly _owner As ThisIsYourWindow
        Private ReadOnly _form As Form
        Private _viewportOrigin As Point
        Private _viewportSize As Size
        Private _geometryUpdateInProgress As Boolean

        Friend Sub New(owner As ThisIsYourWindow,
                       form As Form,
                       viewportOrigin As Point,
                       regionSize As Size,
                       viewportSize As Size)
            _owner = owner
            _form = form
            _viewportOrigin = viewportOrigin
            _viewportSize = viewportSize
            SetBounds(viewportOrigin.X, viewportOrigin.Y, Math.Max(0, regionSize.Width), Math.Max(0, regionSize.Height))
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.Opaque Or ControlStyles.ResizeRedraw Or
                     ControlStyles.SupportsTransparentBackColor, True)
            TabStop = False
            BackColor = Color.Transparent
        End Sub

        Friend Function UpdateViewport(viewportOrigin As Point,
                                       regionSize As Size,
                                       viewportSize As Size,
                                       Optional requestRender As Boolean = True) As Boolean
            Dim changed = Left <> viewportOrigin.X OrElse Top <> viewportOrigin.Y OrElse
                          Width <> Math.Max(0, regionSize.Width) OrElse Height <> Math.Max(0, regionSize.Height)
            _viewportOrigin = viewportOrigin
            _viewportSize = viewportSize
            If changed Then
                Dim ownsGeometryGate = Not _geometryUpdateInProgress
                If ownsGeometryGate Then _geometryUpdateInProgress = True
                Try
                    SetBounds(viewportOrigin.X, viewportOrigin.Y, Math.Max(0, regionSize.Width), Math.Max(0, regionSize.Height))
                Finally
                    If ownsGeometryGate Then _geometryUpdateInProgress = False
                End Try
            End If
            If changed AndAlso requestRender AndAlso IsHandleCreated Then
                D3D_V5Presentation.RequestRender(Me, New Rectangle(Point.Empty, ClientSize))
            End If
            Return changed
        End Function

        Friend Sub BeginGeometryUpdate()
            _geometryUpdateInProgress = True
        End Sub

        Friend Sub EndGeometryUpdate()
            _geometryUpdateInProgress = False
        End Sub

        Friend ReadOnly Property IsGeometryUpdateInProgress As Boolean _
            Implements V5_IGeometryUpdateSource.IsGeometryUpdateInProgress
            Get
                Return _geometryUpdateInProgress
            End Get
        End Property

        Protected Overrides ReadOnly Property CreateParams As CreateParams
            Get
                Dim cp = MyBase.CreateParams
                ' HTTRANSPARENT is handled in WndProc.  WS_EX_TRANSPARENT would
                ' force sibling paint ordering and visibly flash during dragging.
                cp.ExStyle = cp.ExStyle Or &H8000000 ' WS_EX_NOACTIVATE
                Return cp
            End Get
        End Property

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            ' The swap-chain owns every pixel in this region.
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then
                ' V5 never falls back to a CPU paint for this overlay.
            End If
        End Sub

        Public Sub RenderGpu(context As D3D_PaintContext) Implements D3D_IGpuRenderable.RenderGpu
            If _owner Is Nothing OrElse _form Is Nothing OrElse _form.IsDisposed Then Return
            _owner.RenderGpuWindowViewport(context, _form, _viewportOrigin, _viewportSize)
        End Sub

        Protected Overrides Sub WndProc(ByRef m As Message)
            If m.Msg = WM_NCHITTEST Then
                m.Result = New IntPtr(HTTRANSPARENT)
                Return
            End If
            If m.Msg = WM_MOUSEACTIVATE Then
                m.Result = New IntPtr(MA_NOACTIVATE)
                Return
            End If
            MyBase.WndProc(m)
        End Sub
    End Class

    Public Class FullScreenChangedEventArgs : Inherits EventArgs
        Public ReadOnly Property IsFullScreen As Boolean
        Public ReadOnly Property HostForm As Form
        Public Sub New(isFullScreen As Boolean, form As Form)
            Me.IsFullScreen = isFullScreen : HostForm = form
        End Sub
    End Class




    Private Function 获取标题文字布局矩形(s As PerFormState) As RectangleF
        If s Is Nothing OrElse s.HostForm Is Nothing Then Return RectangleF.Empty
        Dim captionRect As Rectangle = 获取标题栏布局矩形(s.HostForm)
        If captionRect.Width <= 0 OrElse captionRect.Height <= 0 Then Return RectangleF.Empty

        Dim leftEdge, rightEdge As Integer
        Dim titlePadLeft As Integer = Math.Max(0, 缩放逻辑尺寸(s.HostForm, _标题文字左边距))
        Dim titlePadRight As Integer = Math.Max(0, 缩放逻辑尺寸(s.HostForm, _标题文字右边距))
        Dim iconPadRight As Integer = 缩放逻辑内边距(s.HostForm, _图标内边距).Right
        If _按钮位置 = ButtonPositionEnum.Right Then
            If Not s.CaptionControlRect.IsEmpty Then
                leftEdge = s.CaptionControlRect.Right + titlePadLeft
            Else
                leftEdge = If(Not s.IconRect.IsEmpty, s.IconRect.Right + iconPadRight + titlePadLeft, captionRect.Left + titlePadLeft)
            End If
            Dim btnLeft As Integer = s.CloseRect.Left
            If Not s.FullScreenRect.IsEmpty Then btnLeft = Math.Min(btnLeft, s.FullScreenRect.Left)
            If s.HostForm.MaximizeBox AndAlso Not s.MaxRect.IsEmpty Then btnLeft = Math.Min(btnLeft, s.MaxRect.Left)
            If s.HostForm.MinimizeBox AndAlso Not s.MinRect.IsEmpty Then btnLeft = Math.Min(btnLeft, s.MinRect.Left)
            rightEdge = btnLeft - titlePadRight
        Else
            If Not s.CaptionControlRect.IsEmpty Then
                leftEdge = s.CaptionControlRect.Right + titlePadLeft
            ElseIf Not s.IconRect.IsEmpty Then
                leftEdge = s.IconRect.Right + iconPadRight + titlePadLeft
            Else
                Dim btnRight As Integer = s.CloseRect.Right
                If Not s.FullScreenRect.IsEmpty Then btnRight = Math.Max(btnRight, s.FullScreenRect.Right)
                If s.HostForm.MaximizeBox AndAlso Not s.MaxRect.IsEmpty Then btnRight = Math.Max(btnRight, s.MaxRect.Right)
                If s.HostForm.MinimizeBox AndAlso Not s.MinRect.IsEmpty Then btnRight = Math.Max(btnRight, s.MinRect.Right)
                leftEdge = btnRight + titlePadLeft
            End If
            rightEdge = captionRect.Right - titlePadRight
        End If

        Return New RectangleF(leftEdge, captionRect.Top, Math.Max(0, rightEdge - leftEdge), captionRect.Height)
    End Function

    Private Function 获取标题文字脏区(s As PerFormState) As Rectangle
        Dim textRect As RectangleF = 获取标题文字布局矩形(s)
        If textRect.Width <= 0 OrElse textRect.Height <= 0 Then Return Rectangle.Empty
        Dim dirty As Rectangle = Rectangle.Ceiling(textRect)
        Dim inflate As Integer = Math.Max(1, 缩放逻辑尺寸(s.HostForm, 2))
        dirty.Inflate(inflate, inflate)
        Return Rectangle.Intersect(获取真实客户区矩形(s.HostForm), dirty)
    End Function

    Private Function 获取标题栏渲染文本(form As Form) As String
        Dim realTitle As String = If(form?.Text, String.Empty)
        If String.IsNullOrEmpty(_标题文字私有协议) OrElse
           Not 是首个附加窗体(form) Then Return realTitle
        Return _标题文字私有协议.Replace(TitleTextPrivateProtocolTitleToken, realTitle)
    End Function

    Private Shared Function 获取CaptionOverlay(s As PerFormState) As ChromeOverlayControl
        If s Is Nothing OrElse Not s.ChromeOverlayActive OrElse
           s.ChromeOverlays Is Nothing OrElse s.ChromeOverlays.Count = 0 Then Return Nothing
        ' 计算ChromeOverlay区域 always emits the caption region first.
        Return s.ChromeOverlays(0)
    End Function


    ''' <summary>请求指定窗体重绘标题栏区域。</summary>
    Public Sub InvalidateCaption(form As Form, Optional immediate As Boolean = False)
        If form Is Nothing OrElse form.IsDisposed OrElse Not form.IsHandleCreated Then Return
        Dim s = 查找状态(form)
        If s Is Nothing Then Return
        Dim size = 获取真实客户区尺寸(form)
        Dim dirty As New Rectangle(0, 0, size.Width,
                                   Math.Min(size.Height, 取缩放标题栏总高度(form)))
        D3D_ControlSurfaceRegistry.MarkDirty(form, dirty, requestConsumers:=True)
        If s.ChromeOverlayActive Then
            Dim captionOverlay = 获取CaptionOverlay(s)
            If captionOverlay IsNot Nothing AndAlso captionOverlay.Visible Then
                D3D_V5Presentation.RequestRender(captionOverlay,
                                                  New Rectangle(Point.Empty, captionOverlay.ClientSize))
            End If
        ElseIf TypeOf form Is V5_IGpuPresentationSource Then
            D3D_V5Presentation.RequestRender(form,
                                              dirty)
        End If
    End Sub

#End Region

#Region "附加 / 分离"

    Private Function CreateChromeOverlays(s As PerFormState) As Boolean
        If Not _useGpuChromeOverlay OrElse s Is Nothing OrElse s.HostForm Is Nothing Then Return False
        If s.ChromeOverlays IsNot Nothing AndAlso s.ChromeOverlays.Count > 0 Then Return True

        Try
            s.ChromeOverlays = New List(Of ChromeOverlayControl)()
            For Each region In 计算ChromeOverlay区域(s)
                Dim overlay As New ChromeOverlayControl(Me, s.HostForm, region.Location, region.Size, 获取真实客户区尺寸(s.HostForm))
                s.HostForm.Controls.Add(overlay)
                确保ChromeOverlay位于内容之后(overlay)
                s.ChromeOverlays.Add(overlay)
            Next
            s.ChromeOverlayActive = s.ChromeOverlays.Count > 0
            D3D_RenderDiagnostics.V5ChromeOverlayCreated(s.ChromeOverlays.Count)
            更新ChromeOverlays(s)
            Return s.ChromeOverlayActive
        Catch ex As Exception
            D3D_RenderDiagnostics.V5ChromeOverlayCreateFailure(ex)
            销毁ChromeOverlays(s)
            Return False
        End Try
    End Function

    Private Sub 更新ChromeOverlays(s As PerFormState)
        If s Is Nothing OrElse Not s.ChromeOverlayActive OrElse s.ChromeOverlays Is Nothing Then Return
        D3D_RenderDiagnostics.V5ChromeOverlayLayoutUpdated(s.IsFullScreen)
        Dim fullSize = 获取真实客户区尺寸(s.HostForm)
        Dim scaledBorder = Math.Max(0, 取缩放边框厚度(s.HostForm))
        Dim regionSignature As Long = HashCode.Combine(fullSize.Width, fullSize.Height, s.LayoutSignature, s.IsFullScreen)
        regionSignature = HashCode.Combine(regionSignature, s.FullScreenCaptionVisible, scaledBorder, CInt(_窗口圆角模式))
        Dim regions As List(Of Rectangle)
        If s.ChromeOverlayRegions IsNot Nothing AndAlso s.ChromeOverlayRegionsSignature = regionSignature Then
            regions = s.ChromeOverlayRegions
        Else
            regions = 计算ChromeOverlay区域(s)
            s.ChromeOverlayRegions = regions
            s.ChromeOverlayRegionsSignature = regionSignature
        End If
        Dim changedOverlays As List(Of ChromeOverlayControl) = Nothing
        While s.ChromeOverlays.Count < regions.Count
            Dim region = regions(s.ChromeOverlays.Count)
            Dim overlay As New ChromeOverlayControl(Me, s.HostForm, region.Location, region.Size, fullSize)
            s.HostForm.Controls.Add(overlay)
            确保ChromeOverlay位于内容之后(overlay)
            s.ChromeOverlays.Add(overlay)
            If changedOverlays Is Nothing Then changedOverlays = New List(Of ChromeOverlayControl)()
            changedOverlays.Add(overlay)
            D3D_RenderDiagnostics.V5ChromeOverlayCreated(1)
        End While

        Dim count = Math.Min(regions.Count, s.ChromeOverlays.Count)
        For i As Integer = 0 To count - 1
            Dim region = regions(i)
            Dim nextVisible = s.HostForm.Visible AndAlso region.Width > 0 AndAlso region.Height > 0
            Dim overlay = s.ChromeOverlays(i)
            Dim visibilityChanged = (overlay.Visible <> nextVisible)
            overlay.BeginGeometryUpdate()
            Dim geometryChanged As Boolean
            Try
                If visibilityChanged Then
                    D3D_RenderDiagnostics.V5ChromeOverlayVisibilityChanged()
                    overlay.Visible = nextVisible
                End If
                ' 先切换可见状态，再请求新尺寸的 GPU 帧；否则还原阶段的请求会被
                ' 不可见控件短路，overlay 可能先显示上一帧或清屏结果。
                geometryChanged = overlay.UpdateViewport(region.Location, region.Size, fullSize, requestRender:=False)
            Finally
                overlay.EndGeometryUpdate()
            End Try
            确保ChromeOverlay位于内容之后(overlay)
            If (visibilityChanged OrElse geometryChanged) AndAlso nextVisible Then
                If changedOverlays Is Nothing Then changedOverlays = New List(Of ChromeOverlayControl)()
                changedOverlays.Add(overlay)
            End If
        Next
        For i As Integer = count To s.ChromeOverlays.Count - 1
            Dim overlay = s.ChromeOverlays(i)
            If overlay.Visible Then
                overlay.BeginGeometryUpdate()
                Try
                    D3D_RenderDiagnostics.V5ChromeOverlayVisibilityChanged()
                    overlay.Visible = False
                Finally
                    overlay.EndGeometryUpdate()
                End Try
            End If
        Next
        If changedOverlays IsNot Nothing Then
            For Each overlay In changedOverlays
                If overlay.IsHandleCreated AndAlso overlay.Visible Then
                    D3D_V5Presentation.RequestRender(overlay, New Rectangle(Point.Empty, overlay.ClientSize))
                End If
            Next
        End If
    End Sub

    Private Shared Sub 确保ChromeOverlay位于内容之后(overlay As ChromeOverlayControl)
        If overlay Is Nothing OrElse overlay.Parent Is Nothing OrElse overlay.IsDisposed Then Return
        Dim parent = overlay.Parent
        Dim overlayIndex As Integer = parent.Controls.GetChildIndex(overlay)
        If overlayIndex < 0 Then Return

        ' Controls index 0 is front-most. Move the overlay only when a normal
        ' child has ended up behind it; repeated SendToBack calls themselves
        ' generate z-order messages and can flash transparent HWNDs.
        For i As Integer = overlayIndex + 1 To parent.Controls.Count - 1
            If Not TypeOf parent.Controls(i) Is ChromeOverlayControl Then
                overlay.SendToBack()
                Exit For
            End If
        Next
    End Sub

    Private Function 计算ChromeOverlay区域(s As PerFormState) As List(Of Rectangle)
        Dim regions As New List(Of Rectangle)()
        If s Is Nothing OrElse s.HostForm Is Nothing OrElse s.HostForm.IsDisposed Then Return regions
        Dim full = 获取真实客户区尺寸(s.HostForm)
        Dim w = Math.Max(0, full.Width)
        Dim h = Math.Max(0, full.Height)
        If w <= 0 OrElse h <= 0 Then Return regions

        Dim caption = 获取标题栏内容矩形(s.HostForm, w, h)
        If caption.Width > 0 AndAlso caption.Height > 0 Then regions.Add(caption)
        If s.IsFullScreen Then Return regions

        Dim bdr = Math.Min(Math.Max(0, 取缩放边框厚度(s.HostForm)), Math.Min(w, h) \ 2)
        If bdr <= 0 Then Return regions

        Dim edgeBand As Integer = bdr
        If 当前使用圆角模式(s) Then
            Dim logicalRadius = DwmWindowStyle.GetCornerRadiusLogical(_窗口圆角模式)
            Dim outerRadius = Math.Max(1.0F, CSng(缩放逻辑尺寸(s.HostForm, logicalRadius)))
            ' 顶/底圆角必须完整落在同一个 opaque swap-chain 带内；额外保留 half-stroke 与 1px AA 余量。
            edgeBand = Math.Max(edgeBand, CInt(Math.Ceiling(outerRadius + bdr / 2.0F + 1.0F)))
        End If
        edgeBand = Math.Min(edgeBand, Math.Min(w, h))

        ' 标题栏之外，圆角模式只使用“整条顶部带 + 整条底部带 + 两侧中段”。
        ' 四个圆弧分别完整位于顶部或底部同一个 surface 中，不跨 HWND 拼接；侧边不与角带重叠。
        regions.Add(New Rectangle(0, 0, w, edgeBand))
        regions.Add(New Rectangle(0, Math.Max(0, h - edgeBand), w, edgeBand))
        Dim sideHeight = Math.Max(0, h - edgeBand * 2)
        If sideHeight > 0 Then
            regions.Add(New Rectangle(0, edgeBand, bdr, sideHeight))
            regions.Add(New Rectangle(Math.Max(0, w - bdr), edgeBand, bdr, sideHeight))
        End If
        Return regions
    End Function

    Private Sub 销毁ChromeOverlays(s As PerFormState)
        If s Is Nothing Then Return
        If s.ChromeOverlays IsNot Nothing Then
            D3D_RenderDiagnostics.V5ChromeOverlayDestroyed(s.ChromeOverlays.Count)
            For Each overlay In s.ChromeOverlays.ToArray()
                Try
                    If overlay.Parent IsNot Nothing Then overlay.Parent.Controls.Remove(overlay)
                    overlay.Dispose()
                Catch
                End Try
            Next
        End If
        s.ChromeOverlays = Nothing
        s.ChromeOverlayActive = False
        s.ChromeOverlayRegions = Nothing
        s.ChromeOverlayRegionsSignature = Long.MinValue
    End Sub

    ''' <summary>
    ''' 将当前样式附加到目标窗体。可多次调用以附加到不同窗体，所有窗体共享同一套外观属性。
    ''' 建议在 Form.Load 中调用。
    ''' </summary>
    Public Sub Attach(targetForm As Form)
        ArgumentNullException.ThrowIfNull(targetForm)
        If _首个附加窗体 Is Nothing Then _首个附加窗体 = targetForm
        If Not targetForm.IsHandleCreated Then
            安排句柄创建后附加(targetForm)
            Return
        End If
        Dim pendingHandler As EventHandler = Nothing
        If _pendingAttachHandlers.TryGetValue(targetForm, pendingHandler) Then
            RemoveHandler targetForm.HandleCreated, pendingHandler
            _pendingAttachHandlers.Remove(targetForm)
        End If
        If _forms.ContainsKey(targetForm.Handle) Then Return

        Dim s As New PerFormState(targetForm) With {.OriginalPadding = targetForm.Padding}

        Dim hWnd As IntPtr = targetForm.Handle

        ' ── 第一步：记录透明度。仅 Win32 渐入需要先隐藏窗口；None 不再把窗口压到 0 alpha。 ──
        ' Form.Opacity 会触发 AllowTransparency → UpdateStyles() →
        ' SetWindowLong(GWL_STYLE, CreateParams.Style) 把 WS_CAPTION 写回，
        ' 导致窗口以默认标题栏出现（白屏）。
        ' 因此 Win32 渐入改用 SetLayeredWindowAttributes 直接设置 alpha=0。
        s.OriginalOpacity = targetForm.Opacity
        s.AnimatingShow = False
        s.PendingFirstPaintRestore = False
        If _显示动画模式 = WindowShowAnimationMode.Win32 Then
            s.AnimatingShow = True
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
        style = style Or WS_THICKFRAME Or WS_SYSMENU
        If targetForm.MinimizeBox Then
            style = style Or WS_MINIMIZEBOX
        Else
            style = style And Not CLng(WS_MINIMIZEBOX)
        End If
        If targetForm.MaximizeBox Then
            style = style Or WS_MAXIMIZEBOX
        Else
            style = style And Not CLng(WS_MAXIMIZEBOX)
        End If
        SetWindowLongPtr(hWnd, GWL_STYLE, New IntPtr(style))

        ' ── 第三步：DWM 属性 ──
        应用Dwm窗口属性(hWnd, _显示动画模式 <> WindowShowAnimationMode.DWM)

        ' ── 第四步：注册拦截器 ──
        s.Interceptor = New WindowMessageInterceptor(Me, s)
        _forms(hWnd) = s
        注册键盘过滤器()
        SyncLock _attachedFormsLock
            _attachedForms(targetForm) = Me
        End SyncLock

        ' ── 第五步：使样式变更生效 ──
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                     CUInt(SWP_FRAMECHANGED Or SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOZORDER))

        Dim setStyleMethod = GetType(Control).GetMethod("SetStyle", BindingFlags.Instance Or BindingFlags.NonPublic)
        setStyleMethod?.Invoke(targetForm, New Object() {
            ControlStyles.OptimizedDoubleBuffer Or ControlStyles.AllPaintingInWmPaint, True})

        Dim chromeOverlayActive = CreateChromeOverlays(s)
        If Not chromeOverlayActive Then AddHandler targetForm.Paint, AddressOf 宿主窗口_Paint
        AddHandler targetForm.FormClosed, AddressOf 宿主窗口_FormClosed
        AddHandler targetForm.HandleDestroyed, AddressOf 宿主窗口_HandleDestroyed
        AddHandler targetForm.VisibleChanged, AddressOf 宿主窗口_VisibleChanged
        AddHandler targetForm.FontChanged, AddressOf 宿主窗口_FontChanged
        AddHandler targetForm.TextChanged, AddressOf HostForm_TextChanged
        AddHandler targetForm.StyleChanged, AddressOf 宿主窗口_StyleChanged
        RecalculateButtonBounds(s)
        更新窗口内边距(s)
        更新ChromeOverlays(s)
        请求GPU渲染(targetForm, 获取真实客户区矩形(targetForm), True)
        更新阴影(s)
        应用毛玻璃状态(s)
    End Sub

    Private Sub 安排句柄创建后附加(targetForm As Form)
        If targetForm Is Nothing OrElse targetForm.IsDisposed Then Return
        If _pendingAttachHandlers.ContainsKey(targetForm) Then Return

        Dim handler As EventHandler = Nothing
        handler = Sub(sender2, ev)
                      RemoveHandler targetForm.HandleCreated, handler
                      _pendingAttachHandlers.Remove(targetForm)
                      If targetForm.IsDisposed Then Return
                      Attach(targetForm)
                  End Sub
        _pendingAttachHandlers(targetForm) = handler
        AddHandler targetForm.HandleCreated, handler
    End Sub

    ''' <summary>从指定窗体分离。</summary>
    Public Sub Detach(targetForm As Form)
        Dim s = 查找状态(targetForm)
        If s IsNot Nothing AndAlso s.IsFullScreen AndAlso targetForm IsNot Nothing AndAlso
           Not targetForm.IsDisposed AndAlso targetForm.IsHandleCreated AndAlso targetForm.Visible Then
            退出全屏(s)
        End If
        释放当前句柄附加状态(targetForm, removeAttachedRegistration:=True, removePendingAttach:=True)
    End Sub

    Private Sub 释放当前句柄附加状态(targetForm As Form,
                              removeAttachedRegistration As Boolean,
                              removePendingAttach As Boolean)
        If targetForm Is Nothing Then Return

        Dim pendingHandler As EventHandler = Nothing
        If removePendingAttach AndAlso _pendingAttachHandlers.TryGetValue(targetForm, pendingHandler) Then
            RemoveHandler targetForm.HandleCreated, pendingHandler
            _pendingAttachHandlers.Remove(targetForm)
        End If

        Dim s As PerFormState = Nothing
        Dim key As IntPtr = IntPtr.Zero
        If targetForm.IsHandleCreated Then
            key = targetForm.Handle
            _forms.TryGetValue(key, s)
        End If
        If s Is Nothing Then
            For Each kv In _forms
                If kv.Value.HostForm Is targetForm Then
                    key = kv.Key
                    s = kv.Value
                    Exit For
                End If
            Next
        End If
        If s Is Nothing Then
            If removeAttachedRegistration Then 移除附加注册(targetForm)
            Return
        End If

        Dim wasCaptionControlHost As Boolean = ReferenceEquals(_标题栏控件宿主窗体, targetForm)
        If wasCaptionControlHost Then 恢复标题栏控件原始布局()
        _forms.Remove(key)
        If _forms.Count = 0 Then 注销键盘过滤器()
        If removeAttachedRegistration Then 移除附加注册(targetForm)

        s.CachedIconBitmap?.Dispose()
        停止全屏标题栏隐藏计时器(s)
        ' 窗口级 D3D compositor 会在 Form.HandleDestroyed 时释放图形资源，这里无需重复清理。
        s.Interceptor?.ReleaseHandle()
        销毁阴影(s)
        If s.BackdropTimer IsNot Nothing Then
            s.BackdropTimer.Stop()
            s.BackdropTimer.Dispose()
            s.BackdropTimer = Nothing
        End If
        If s.Renderer IsNot Nothing Then
            Try
            Catch
            End Try
            s.Renderer.Dispose()
            s.Renderer = Nothing
        End If
        销毁ChromeOverlays(s)
        RemoveHandler targetForm.Paint, AddressOf 宿主窗口_Paint
        RemoveHandler targetForm.FormClosed, AddressOf 宿主窗口_FormClosed
        RemoveHandler targetForm.HandleDestroyed, AddressOf 宿主窗口_HandleDestroyed
        RemoveHandler targetForm.VisibleChanged, AddressOf 宿主窗口_VisibleChanged
        RemoveHandler targetForm.FontChanged, AddressOf 宿主窗口_FontChanged
        RemoveHandler targetForm.TextChanged, AddressOf HostForm_TextChanged
        RemoveHandler targetForm.StyleChanged, AddressOf 宿主窗口_StyleChanged
        targetForm.Padding = s.OriginalPadding

        If wasCaptionControlHost AndAlso removeAttachedRegistration Then
            _标题栏控件宿主窗体 = Nothing
            同步所有标题栏绑定控件布局()
        End If
    End Sub

    Private Sub 移除附加注册(targetForm As Form)
        SyncLock _attachedFormsLock
            Dim owner As ThisIsYourWindow = Nothing
            If _attachedForms.TryGetValue(targetForm, owner) AndAlso owner Is Me Then
                _attachedForms.Remove(targetForm)
            End If
        End SyncLock
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

        If s.IsFullScreen Then
            应用全屏窗口外观(s, Screen.FromHandle(targetForm.Handle).Bounds)
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
        style = style Or WS_THICKFRAME Or WS_SYSMENU
        If targetForm.MinimizeBox Then
            style = style Or WS_MINIMIZEBOX
        Else
            style = style And Not CLng(WS_MINIMIZEBOX)
        End If
        If targetForm.MaximizeBox Then
            style = style Or WS_MAXIMIZEBOX
        Else
            style = style And Not CLng(WS_MAXIMIZEBOX)
        End If
        SetWindowLongPtr(hWnd, GWL_STYLE, New IntPtr(style))

        ' ── 重新应用 DWM 属性 ──
        应用Dwm窗口属性(hWnd)

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
        请求GPU渲染(targetForm, 获取真实客户区矩形(targetForm), True)
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
        If s.IsFullScreen AndAlso Not s.FullScreenCaptionVisible Then Return HTCLIENT
        Dim clientSize = 获取真实客户区尺寸(s.HostForm)
        Dim w As Integer = clientSize.Width
        Dim h As Integer = clientSize.Height
        Dim bw As Integer = Math.Max(1, 缩放逻辑尺寸(s.HostForm, _调整边框宽度))
        ' 以原生窗口状态为准。标题栏拖动还原时，WinForms WindowState
        ' 可能在 WM_SIZE 之后才更新，不能据此永久关闭边缘命中。
        Dim zoomed As Boolean = 窗口当前已最大化(s.HostForm)

        If Not s.IsFullScreen AndAlso _允许调整大小 AndAlso Not (zoomed AndAlso _最大化时隐藏调整边框) Then
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
        If Not s.FullScreenRect.IsEmpty AndAlso s.FullScreenRect.Contains(clientPoint) Then Return HTFULLSCREEN
        If Not s.MaxRect.IsEmpty AndAlso s.MaxRect.Contains(clientPoint) Then Return HTMAXBUTTON
        If Not s.MinRect.IsEmpty AndAlso s.MinRect.Contains(clientPoint) Then Return HTMINBUTTON
        If Not s.IconRect.IsEmpty AndAlso s.IconRect.Contains(clientPoint) Then Return HTSYSMENU
        If Not s.CaptionControlRect.IsEmpty AndAlso s.CaptionControlRect.Contains(clientPoint) Then Return HTCLIENT

        Dim captionRect As Rectangle = 获取标题栏内容矩形(s.HostForm, w, h)
        If captionRect.Contains(clientPoint) Then
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

    Private Sub ShowSystemMenuAtIcon(s As PerFormState)
        If s Is Nothing OrElse s.HostForm Is Nothing OrElse s.HostForm.IsDisposed OrElse s.IconRect.IsEmpty Then Return

        Dim hWnd As IntPtr = s.HostForm.Handle
        Dim systemMenu As IntPtr = GetSystemMenu(hWnd, False)
        If systemMenu = IntPtr.Zero Then Return

        Dim anchor As Point = s.HostForm.PointToScreen(New Point(s.IconRect.Left, s.IconRect.Bottom))
        Dim command As Integer = TrackPopupMenuEx(systemMenu,
                                                   CUInt(TPM_LEFTALIGN Or TPM_TOPALIGN Or TPM_RETURNCMD),
                                                   anchor.X, anchor.Y, hWnd, IntPtr.Zero)
        If command <> 0 Then SendMessage(hWnd, WM_SYSCOMMAND, New IntPtr(command), IntPtr.Zero)
    End Sub

#End Region

#Region "NativeWindow 消息拦截器"

    Private Const WM_NCLBUTTONDOWN As Integer = &HA1
    Private Const WM_NCLBUTTONUP As Integer = &HA2
    Private Const WM_NCLBUTTONDBLCLK As Integer = &HA3
    Private Const WM_NCMOUSELEAVE As Integer = &H2A2
    Private Const WM_MOUSEMOVE As Integer = &H200
    Private Const WM_LBUTTONUP As Integer = &H202
    Private Const WM_MOUSELEAVE As Integer = &H2A3
    Private Const WM_CAPTURECHANGED As Integer = &H215
    Private Const WM_ENTERSIZEMOVE As Integer = &H231
    Private Const WM_EXITSIZEMOVE As Integer = &H232
    Private Const WM_SHOWWINDOW As Integer = &H18
    Private Const WM_CLOSE As Integer = &H10
    Private Const WM_DPICHANGED As Integer = &H2E0

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
                       Not _state.IsFullScreen AndAlso
                       _owner._允许调整大小 AndAlso
                       Not (IsZoomed(_state.HostForm.Handle) AndAlso _owner._最大化时隐藏调整边框) Then
                        hit = sysResult
                    End If

                    Dim oldHover As Integer = _state.HoverHit
                    _state.HoverHit = If(hit = HTCLOSE OrElse hit = HTFULLSCREEN OrElse hit = HTMAXBUTTON OrElse hit = HTMINBUTTON, hit, HTNOWHERE)
                    If oldHover <> _state.HoverHit Then _owner.InvalidateCaption(_state.HostForm)
                    m.Result = New IntPtr(hit)
                    Return

                Case WM_NCCALCSIZE
                    If m.WParam <> IntPtr.Zero AndAlso Not _state.IsFullScreen AndAlso IsZoomed(_state.HostForm.Handle) Then
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

                Case WM_WINDOWPOSCHANGED
                    If _state.DeferredClientBoundsActive Then
                        _owner.更新阴影实时跟随(_state)
                        m.Result = IntPtr.Zero
                        Return
                    End If
                    MyBase.WndProc(m)
                    If Not _state.AnimatingClose Then _owner.更新阴影(_state)
                    Return

                Case WM_SIZE
                    If _state.DeferredClientBoundsActive Then
                        Dim minimizedDuringDeferred As Boolean = (_state.HostForm IsNot Nothing AndAlso
                                                                  _state.HostForm.WindowState = FormWindowState.Minimized)
                        _state.WasMinimized = minimizedDuringDeferred
                        m.Result = IntPtr.Zero
                        Return
                    End If
                    MyBase.WndProc(m)
                    If _owner._阴影模式 <> ShadowModeEnum.DWM Then _owner.切换动画样式(_state.HostForm.Handle, False)
                    Dim currentClientSize As Size = ThisIsYourWindow.获取真实客户区尺寸(_state.HostForm)
                    Dim clientSizeChanged As Boolean = (currentClientSize <> _state.LastClientSize)
                    Dim minimizedNow As Boolean = (_state.HostForm IsNot Nothing AndAlso
                                                   _state.HostForm.WindowState = FormWindowState.Minimized)
                    _owner.RecalculateButtonBounds(_state)
                    If minimizedNow Then
                        ' 最小化阶段没有可见客户区，避免把一次隐藏态 WM_SIZE 扩散成全量重绘。
                    ElseIf Not _owner.可跳过WMSize客户区刷新(_state, clientSizeChanged) Then
                        请求GPU渲染(_state.HostForm, ThisIsYourWindow.获取真实客户区矩形(_state.HostForm))
                    Else
                        _owner.InvalidateCaption(_state.HostForm)
                    End If
                    _owner.更新阴影(_state)
                    ' 检测"从最小化恢复"：此时桌面 DC 与上一次抓屏所在的位置可能已完全不同，
                    ' 必须强制刷新一次毛玻璃帧（同时 commit 平均色，刷新阴影自动颜色）。
                    If _state.WasMinimized AndAlso Not minimizedNow Then
                        _owner.请求毛玻璃帧(_state, True)
                    End If
                    If Not minimizedNow Then _state.LastClientSize = currentClientSize
                    _state.WasMinimized = minimizedNow
                    Return

                Case WM_ACTIVATE
                    MyBase.WndProc(m)
                    Dim activated As Boolean = (CInt(m.WParam.ToInt64() And &HFFFF) <> 0)
                    _state.Activated = activated
                    _owner.触发激活状态改变(activated, _state.HostForm)
                    If Not _state.IsFullScreen AndAlso _state.HostForm IsNot Nothing AndAlso _state.HostForm.IsHandleCreated Then
                        Try : _owner.应用Dwm边框颜色(_state.HostForm.Handle) : Catch : End Try
                    End If
                    If _owner._标题文字颜色 <> _owner._标题文字失焦颜色 Then
                        _owner.InvalidateTitleText(_state, True)
                    End If
                    If Not _owner.毛玻璃当前启用(_state) Then
                        请求GPU渲染(_state.HostForm, ThisIsYourWindow.获取真实客户区矩形(_state.HostForm))
                    End If
                    If activated AndAlso Not _state.AnimatingClose Then _owner.更新阴影(_state)
                    Return

                Case WM_MOVE
                    If _state.DeferredClientBoundsActive Then
                        m.Result = IntPtr.Zero
                        Return
                    End If
                    MyBase.WndProc(m)
                    _owner.更新阴影(_state)
                    Return

                Case WM_ERASEBKGND
                    If _state.DeferredClientBoundsActive Then
                        m.Result = New IntPtr(1)
                        Return
                    End If
                    m.Result = New IntPtr(1)
                    Return

                Case WM_PAINT
                    If _state.DeferredClientBoundsActive Then
                        ValidateRect(_state.HostForm.Handle, IntPtr.Zero)
                        m.Result = IntPtr.Zero
                        Return
                    End If
                    MyBase.WndProc(m)
                    If _state.PendingFirstPaintRestore Then _owner.取消首帧等待(_state)
                    Return

                Case WM_ENTERSIZEMOVE
                    _state.IsInSizeMove = True
                    _owner.开始延迟客户区坐标上报(_state)
                    MyBase.WndProc(m)
                    Return

                Case WM_EXITSIZEMOVE
                    _state.IsInSizeMove = False
                    MyBase.WndProc(m)
                    If _state.DeferredClientBoundsActive Then
                        _owner.提交延迟客户区坐标上报(_state)
                    Else
                        _owner.更新阴影(_state)
                        _owner.请求毛玻璃帧(_state, True)
                        _owner.重置毛玻璃Tick(_state)
                    End If
                    Return

                Case WM_NCACTIVATE
                    m.Result = New IntPtr(1)
                    Return

                Case WM_NCPAINT
                    m.Result = IntPtr.Zero
                    Return

                Case WM_NCLBUTTONDOWN
                    Dim htDown As Integer = CInt(m.WParam.ToInt64())
                    If _state.IsFullScreen AndAlso htDown = HTCAPTION Then Return
                    If htDown = HTSYSMENU Then
                        _owner.ShowSystemMenuAtIcon(_state)
                        Return
                    End If
                    If htDown = HTCLOSE OrElse htDown = HTFULLSCREEN OrElse htDown = HTMAXBUTTON OrElse htDown = HTMINBUTTON Then
                        _state.PressedHit = htDown
                        _state.HoverHit = htDown
                        _owner.InvalidateCaption(_state.HostForm)
                        SetCapture(_state.HostForm.Handle)
                        Return
                    End If
                    MyBase.WndProc(m)
                    Return

                Case WM_NCLBUTTONDBLCLK
                    If _state.IsFullScreen Then Return
                    If CInt(m.WParam.ToInt64()) = HTCAPTION AndAlso Not _state.HostForm.MaximizeBox Then
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
                    If _state.IsFullScreen Then
                        _owner.处理全屏鼠标移动(_state, 解析LParam坐标(m.LParam))
                    End If
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
                                Case HTFULLSCREEN
                                    If _state.HostForm IsNot Nothing Then _owner.ToggleFullScreen(_state.HostForm)
                                Case HTMAXBUTTON
                                    If _state.HostForm IsNot Nothing Then
                                        ' 全屏时窗口已经覆盖显示器，切换到 Maximized 会让无边框
                                        ' WS_POPUP 被系统按普通最大化重新定位，可能移到屏幕顶部之外。
                                        ' 最大化在该状态下没有语义，因此保持全屏边界不变。
                                        If _state.IsFullScreen Then Return
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
                    If m.WParam = IntPtr.Zero Then
                        _owner.销毁阴影(_state)
                        MyBase.WndProc(m)
                        Return
                    End If

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
                    If m.WParam <> IntPtr.Zero AndAlso _state.PendingFirstPaintRestore Then
                        请求GPU渲染(_state.HostForm, ThisIsYourWindow.获取真实客户区矩形(_state.HostForm), True)
                    End If
                    If m.WParam <> IntPtr.Zero AndAlso _owner._显示动画模式 <> WindowShowAnimationMode.DWM Then
                        Try
                            Dim enable As Integer = 0
                            Dim unused = DwmSetWindowAttribute(_state.HostForm.Handle, DWMWA_TRANSITIONS_FORCEDISABLED, enable, 4)
                        Catch
                        End Try
                    End If
                    If m.WParam <> IntPtr.Zero AndAlso _state.Renderer IsNot Nothing AndAlso Not _state.Renderer.HasFrame Then
                        _owner.请求毛玻璃帧(_state, True, forceImageMode:=True)
                    End If
                    If m.WParam <> IntPtr.Zero AndAlso Not _state.AnimatingClose Then
                        _owner.更新阴影(_state)
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
                        Dim targetAlpha As Integer = CInt(Math.Round(Math.Max(0.0, Math.Min(1.0, frm.Opacity)) * 255))
                        Dim hWnd = frm.Handle
                        Dim exStyle As Long = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64()
                        If (exStyle And WS_EX_LAYERED) = 0 Then
                            SetWindowLongPtr(hWnd, GWL_EXSTYLE, New IntPtr(exStyle Or WS_EX_LAYERED))
                        End If
                        Dim syncShadow As Boolean = (_owner._阴影模式 = ShadowModeEnum.Layer) AndAlso _state.ShadowForm IsNot Nothing
                        Dim t As PrecisionTimer = 创建UI精度计时器(frm, FrameIntervalMilliseconds(60))
                        Dim startTicks As Long = Stopwatch.GetTimestamp()
                        Dim duration As Integer = _owner._动画持续时间
                        AddHandler t.Tick, Sub(s, ev)
                                               Dim elapsed As Double = (Stopwatch.GetTimestamp() - startTicks) * 1000.0R / Stopwatch.Frequency
                                               If elapsed >= duration OrElse frm.IsDisposed Then
                                                   If Not frm.IsDisposed Then
                                                       SetLayeredWindowAttributes(hWnd, 0, 0, LWA_ALPHA)
                                                       If syncShadow AndAlso _state.ShadowForm IsNot Nothing Then
                                                           _state.ShadowForm.SetGlobalAlpha(0)
                                                       End If
                                                   End If
                                                   t.Stop() : t.Dispose()
                                                   If Not frm.IsDisposed Then frm.Close()
                                                   Dim closeCancelled As Boolean = Not frm.IsDisposed AndAlso frm.Visible
                                                   If closeCancelled Then
                                                       SetLayeredWindowAttributes(hWnd, 0, CByte(Math.Min(255, Math.Max(0, targetAlpha))), LWA_ALPHA)
                                                       If syncShadow AndAlso _state.ShadowForm IsNot Nothing Then
                                                           _state.ShadowForm.SetGlobalAlpha(255)
                                                       End If
                                                   Else
                                                       If Not frm.IsDisposed Then
                                                           SetLayeredWindowAttributes(hWnd, 0, CByte(Math.Min(255, Math.Max(0, targetAlpha))), LWA_ALPHA)
                                                       End If
                                                       _owner.销毁阴影(_state)
                                                   End If
                                                   _state.AnimatingClose = False
                                               Else
                                                   Dim ratio As Double = Math.Max(0.0, 1.0 - elapsed / CDbl(duration))
                                                   Dim alpha As Byte = CByte(Math.Min(255, Math.Max(0, CInt(Math.Round(targetAlpha * ratio)))))
                                                   SetLayeredWindowAttributes(hWnd, 0, alpha, LWA_ALPHA)
                                                   If syncShadow AndAlso _state.ShadowForm IsNot Nothing Then
                                                       _state.ShadowForm.SetGlobalAlpha(CByte(CInt(Math.Round(255 * ratio))))
                                                   End If
                                               End If
                                           End Sub
                        t.Start()
                        Return
                    End If
                    MyBase.WndProc(m)
                    If Not _state.HostForm.IsDisposed AndAlso Not _state.HostForm.Visible Then
                        _owner.销毁阴影(_state)
                    End If
                    Return

                Case WM_DPICHANGED
                    Dim newDpi As Integer = D3D_D2DInterop.ExtractDpiFromWParam(m.WParam)
                    If newDpi > 0 AndAlso _state.HostForm IsNot Nothing AndAlso
                       Not _state.HostForm.IsDisposed AndAlso _state.HostForm.IsHandleCreated Then
                        D3D_D2DInterop.SetWindowDpi(_state.HostForm.Handle, newDpi)
                    End If
                    MyBase.WndProc(m)
                    _owner.处理DpiChanged(_state)
                    Return

                Case WM_SYSCOMMAND
                    Dim cmd As Integer = CInt(m.WParam.ToInt64() And &HFFF0)
                    ' 不允许系统命令在全屏标题栏可见期间改变 WindowState。
                    ' 这与自绘最大化按钮的无操作行为保持一致，并防止外部/键盘
                    ' 触发 SC_MAXIMIZE 后把无边框全屏窗口移出屏幕顶部。
                    If _state.IsFullScreen AndAlso (cmd = SC_MAXIMIZE OrElse cmd = SC_RESTORE) Then
                        m.Result = IntPtr.Zero
                        Return
                    End If
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
