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

    Private Const WDA_NONE As Integer = &H0
    Private Const WDA_EXCLUDEFROMCAPTURE As Integer = &H11

    <DllImport("user32.dll")>
    Private Shared Function SetWindowDisplayAffinity(hWnd As IntPtr, dwAffinity As Integer) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function ValidateRect(hWnd As IntPtr, lpRect As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetWindowRect(hWnd As IntPtr, ByRef lpRect As RECT) As <MarshalAs(UnmanagedType.Bool)> Boolean
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
        Public LastTitleTextDirtyRect As Rectangle = Rectangle.Empty
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
        ' ── 布局缓存签名：仅当窗口宽度/按钮可见性/相关属性变化时重新计算按钮位置 ──
        Public LayoutSignature As Long = -1
        ' ── 毛玻璃 ──
        Public Renderer As BackdropRenderer
        Public BackdropTimer As Timer
        ' ── D2D 资源（V2 占位）──
        ' V2：DC RT / SSAA 池 / 笔刷缓存 / TextFormat 缓存 / 位图缓存 全部迁到 WindowCompositor（Form 级共享）。
        ' 这里不再持有任何 D2D 字段；通过 D2DHelperV2.BeginPaint(...) → PaintScopeV2 拿到。
        Public Sub New(form As Form)
            HostForm = form
            If form IsNot Nothing Then LastClientSize = form.ClientSize
        End Sub
    End Class

    Private ReadOnly _forms As New Dictionary(Of IntPtr, PerFormState)
    Private ReadOnly _pendingAttachHandlers As New Dictionary(Of Form, EventHandler)
    Private _首个附加窗体 As Form
    Private Shared ReadOnly _attachedFormsLock As New Object()
    Private Shared ReadOnly _attachedForms As New Dictionary(Of Form, ThisIsYourWindow)

    ' ── 绘制热路径共享缓存：避免每帧 New SolidBrush/Pen 造成 GC 压力 ──
    Private ReadOnly _共享画刷 As New SolidBrush(Color.Black)
    Private ReadOnly _共享画笔 As New Pen(Color.Black, 1.0F)

    Private Function 查找状态(form As Form) As PerFormState
        Dim s As PerFormState = Nothing
        If form IsNot Nothing AndAlso form.IsHandleCreated Then _forms.TryGetValue(form.Handle, s)
        Return s
    End Function

    Private Function 是首个附加窗体(form As Form) As Boolean
        Return form IsNot Nothing AndAlso ReferenceEquals(form, _首个附加窗体)
    End Function

    Friend Shared Function TryPaintImageBackdropForBackgroundMapping(source As Control,
                                                                    rt As ID2D1RenderTarget,
                                                                    sourceClientRect As Rectangle,
                                                                    destRect As Rectangle) As Boolean
        Dim form = TryCast(source, Form)
        If form Is Nothing OrElse rt Is Nothing Then Return False

        Dim owner As ThisIsYourWindow = Nothing
        SyncLock _attachedFormsLock
            _attachedForms.TryGetValue(form, owner)
        End SyncLock
        If owner Is Nothing Then Return False
        Return owner.TryPaintImageBackdropSlice(form, rt, sourceClientRect, destRect)
    End Function

    Friend Shared Function TryGetImageBackdropFrameVersion(source As Control,
                                                          ByRef frameVersion As Integer) As Boolean
        frameVersion = -1
        Dim form = TryCast(source, Form)
        If form Is Nothing Then Return False

        Dim owner As ThisIsYourWindow = Nothing
        SyncLock _attachedFormsLock
            _attachedForms.TryGetValue(form, owner)
        End SyncLock
        If owner Is Nothing Then Return False

        Dim state = owner.查找状态(form)
        If state Is Nothing OrElse state.Renderer Is Nothing Then Return False
        If owner._毛玻璃模式 <> BackdropModeEnum.Image OrElse Not state.Renderer.IsImageSource Then Return False
        frameVersion = state.Renderer.FrameVersion
        Return frameVersion >= 0
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
            Return Math.Max(0.01F, control.DeviceDpi / 96.0F)
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

    Private Function 取缩放边框厚度(control As Control) As Integer
        Return Math.Max(0, 缩放逻辑尺寸(control, _边框厚度))
    End Function

    Private Function 取缩放标题栏高度(control As Control) As Integer
        Return Math.Max(0, 缩放逻辑尺寸(control, _标题栏高度))
    End Function

    Private Function 取缩放标题栏总高度(control As Control) As Integer
        Return 取缩放边框厚度(control) + 取缩放标题栏高度(control)
    End Function

    Private Sub 通知重绘(Optional immediate As Boolean = True)
        For Each s In _forms.Values
            Dim frm = s.HostForm
            If frm IsNot Nothing AndAlso Not frm.IsDisposed AndAlso frm.IsHandleCreated Then
                OuterToInnerRefreshScheduler.RequestFull(frm, invalidateChildren:=True, immediate:=immediate)
            End If
        Next
    End Sub

    Private Sub 使布局失效(Optional recalculate As Boolean = True)
        For Each s In _forms.Values
            s.LayoutSignature = -1
            If recalculate Then RecalculateButtonBounds(s)
        Next
    End Sub

    Private Sub 通知标题栏重绘(Optional immediate As Boolean = True)
        For Each s In _forms.Values
            InvalidateCaption(s.HostForm, immediate)
        Next
    End Sub

    Private Sub 应用Dwm窗口属性(hWnd As IntPtr, Optional disableTransitions As Boolean = False)
        Try
            Dim pref As Integer = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND
            Dim unused1 = DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, pref, 4)
            Dim colorNone As Integer = DWMWA_COLOR_NONE
            Dim unused2 = DwmSetWindowAttribute(hWnd, DWMWA_BORDER_COLOR, colorNone, 4)
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

    Private Function TryPaintImageBackdropSlice(form As Form,
                                                rt As ID2D1RenderTarget,
                                                sourceClientRect As Rectangle,
                                                destRect As Rectangle) As Boolean
        Dim s = 查找状态(form)
        If s Is Nothing OrElse s.Renderer Is Nothing Then Return False
        If _毛玻璃模式 <> BackdropModeEnum.Image Then Return False
        If Not 毛玻璃当前启用(s) OrElse Not s.Renderer.HasFrame OrElse Not s.Renderer.IsImageSource Then Return False
        If sourceClientRect.Width <= 0 OrElse sourceClientRect.Height <= 0 OrElse
           destRect.Width <= 0 OrElse destRect.Height <= 0 Then Return False

        Dim clientBounds As New Rectangle(Point.Empty, form.ClientSize)
        Dim visibleSource = Rectangle.Intersect(clientBounds, sourceClientRect)
        If visibleSource.Width <= 0 OrElse visibleSource.Height <= 0 Then Return False

        Dim visibleDest As New Rectangle(
            destRect.X + visibleSource.X - sourceClientRect.X,
            destRect.Y + visibleSource.Y - sourceClientRect.Y,
            visibleSource.Width,
            visibleSource.Height)

        If Not s.Renderer.DrawImageBackdropSlice(rt, visibleSource, visibleDest) Then Return False

        Dim tint As Color = If(s.Activated, _毛玻璃Tint颜色, _毛玻璃Tint失焦颜色)
        If tint.A > 0 Then
            Using b = rt.CreateSolidColorBrush(D2DGlobals.ToColor4(tint))
                rt.FillRectangle(D2DGlobals.ToD2DRect(visibleDest), b)
            End Using
        End If

        If _毛玻璃模糊次数 > 0 AndAlso _毛玻璃噪点不透明度 > 0 Then
            s.Renderer.DrawNoise(rt, visibleDest, _毛玻璃噪点不透明度)
        End If
        Return True
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
            If boundsChanged Then
                更新控件边界缓存(s.HostForm)
            End If
        End If

        s.DeferredBeginBounds = Rectangle.Empty
        s.LayoutSignature = -1
        If _阴影模式 <> ShadowModeEnum.DWM AndAlso s.HostForm.IsHandleCreated Then
            切换动画样式(s.HostForm.Handle, False)
        End If
        RecalculateButtonBounds(s)
        更新阴影(s)
        Dim requestBackdropFrame As Boolean = boundsChanged AndAlso
                                             毛玻璃当前启用(s) AndAlso
                                             (_毛玻璃模式 <> BackdropModeEnum.Image OrElse sizeChanged)
        If requestBackdropFrame Then
            请求毛玻璃帧(s, True, forceImageMode:=sizeChanged)
        ElseIf sizeChanged Then
            OuterToInnerRefreshScheduler.RequestFull(s.HostForm)
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
        PaintWindow(e, frm)
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

    Private Sub 宿主窗口_HandleDestroyed(sender As Object, e As EventArgs)
        Dim frm = TryCast(sender, Form)
        If frm IsNot Nothing Then Detach(frm)
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
        D2DHelperV2.InvalidateTextFormatCache(frm)
        InvalidateTitleText(s, True)
    End Sub

    Private Sub HostForm_TextChanged(sender As Object, e As EventArgs)
        Dim frm = TryCast(sender, Form)
        If frm Is Nothing Then Return
        Dim s = 查找状态(frm)
        If s Is Nothing Then Return
        InvalidateTitleText(s, True)
    End Sub

    Private Sub InvalidateTitleText(s As PerFormState, Optional immediate As Boolean = False)
        If s Is Nothing OrElse s.HostForm Is Nothing OrElse s.HostForm.IsDisposed OrElse Not s.HostForm.IsHandleCreated Then Return
        RecalculateButtonBounds(s)
        Dim newDirty As Rectangle = 获取标题文字脏区(s)
        Dim dirty As Rectangle = 合并脏区(s.LastTitleTextDirtyRect, newDirty)
        s.LastTitleTextDirtyRect = newDirty
        If dirty.Width > 0 AndAlso dirty.Height > 0 Then
            s.HostForm.Invalidate(dirty, False)
            If immediate Then s.HostForm.Update()
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
            If s?.HostForm IsNot Nothing Then D2DHelperV2.InvalidateTextFormatCache(s.HostForm)
        Next
    End Sub

    Private Sub 更新窗口内边距(s As PerFormState)
        If s Is Nothing Then Return
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
        s.LayoutSignature = -1
        RecalculateButtonBounds(s)
        更新窗口内边距(s)
        D2DHelperV2.InvalidateTextFormatCache(s.HostForm)
        If 毛玻璃当前启用(s) Then 请求毛玻璃帧(s, True, forceImageMode:=True)
        更新阴影(s)
        OuterToInnerRefreshScheduler.RequestFull(s.HostForm, invalidateChildren:=True)
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
    ''' <summary>窗口处于激活状态时的边框绘制颜色。</summary>
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
    ''' <summary>窗口失去焦点时的边框绘制颜色。</summary>
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
                s.LayoutSignature = -1
                RecalculateButtonBounds(s)
                更新窗口内边距(s)
            Next
            通知重绘()
        End Set
    End Property

#End Region

#Region "属性 - 标题栏"

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

    Private _标题栏背景颜色 As Color = Color.FromArgb(32, 32, 32)
    ''' <summary>标题栏在窗口激活时的背景填充颜色。</summary>
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
    ''' <summary>窗口失去焦点时标题栏的背景填充颜色。</summary>
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
            _标题文字左边距 = Math.Max(0, value) : 通知重绘()
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
            _标题文字右边距 = Math.Max(0, value) : 通知重绘()
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
            _自定义图标 = value : 通知重绘()
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

    Private _图标左边距 As Integer = 8
    ''' <summary>图标距离其外侧（按钮组左侧或窗口左边缘）的水平间距（逻辑像素）。</summary>
    <Category("LakeUI"), Description("图标距离窗口左边缘的间距。"), DefaultValue(8)>
    Public Property IconPaddingLeft As Integer
        Get
            Return _图标左边距
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If _图标左边距 = value Then Return
            _图标左边距 = value
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
            _按钮符号大小 = Math.Max(4, value) : 通知重绘()
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
            _按钮符号线宽 = Math.Max(0.5F, value) : 通知重绘()
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
            _按钮内边距 = value : 通知重绘()
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
            _按钮圆角半径 = Math.Max(0, value) : 通知重绘()
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
                OuterToInnerRefreshScheduler.RequestFull(s.HostForm, invalidateChildren:=True, immediate:=True)
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
        更新阴影(s, 获取窗口屏幕矩形(s.HostForm), True)
    End Sub

    Private Sub 更新阴影(s As PerFormState, boundsOverride As Rectangle, forceFullRender As Boolean)
        If s Is Nothing OrElse s.HostForm Is Nothing Then Return
        Dim zoomed As Boolean = (s.HostForm.WindowState = FormWindowState.Maximized)
        Dim minimized As Boolean = (s.HostForm.WindowState = FormWindowState.Minimized)

        If _阴影模式 <> ShadowModeEnum.Layer OrElse zoomed OrElse minimized OrElse Not s.HostForm.Visible Then
            If s.ShadowForm IsNot Nothing Then
                If Not s.HostForm.Visible Then
                    销毁阴影(s)
                Else
                    s.ShadowForm.Visible = False
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
            s.ShadowForm.Show()
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
        s.ShadowForm.UpdateShadow(bounds, _分层阴影深度, shadowColor, _分层阴影不透明度, If(forceFullRender, False, s.IsInSizeMove))
        s.ShadowForm.PlaceBehind(s.HostForm.Handle)
        If Not s.ShadowForm.Visible Then s.ShadowForm.Visible = True

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
            _毛玻璃图片 = value
            For Each s In _forms.Values
                If s.Renderer IsNot Nothing AndAlso _毛玻璃模式 = BackdropModeEnum.Image Then
                    s.Renderer.SetSource(True, value)
                    s.Renderer.RequestFrame(s.HostForm.Bounds, True)
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
            _毛玻璃Tint颜色 = value : 通知重绘()
        End Set
    End Property

    Private _毛玻璃Tint失焦颜色 As Color = Color.FromArgb(140, 24, 24, 24)
    <Category("LakeUI - Backdrop"), Description("毛玻璃模式下失活窗口的 tint 叠加颜色。"), DefaultValue(GetType(Color), "140, 24, 24, 24")>
    Public Property BackdropTintInactiveColor As Color
        Get
            Return _毛玻璃Tint失焦颜色
        End Get
        Set(value As Color)
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
            _毛玻璃模糊半径 = Math.Max(1, Math.Min(96, value))
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
            _毛玻璃模糊次数 = Math.Max(0, Math.Min(5, value))
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
            _毛玻璃下采样 = Math.Max(1, value)
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
            _毛玻璃噪点缩放 = Math.Max(0.1F, value)
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
            _毛玻璃帧率 = Math.Max(0, Math.Min(60, value))
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
        ' Auto / CaptionOnly 模式需要 OS 支持 WDA_EXCLUDEFROMCAPTURE，否则降级为 None
        Dim shouldEnable As Boolean = 毛玻璃允许用于窗体(s) AndAlso
                                      ((mode = BackdropModeEnum.Image) OrElse
                                       ((mode = BackdropModeEnum.Auto OrElse mode = BackdropModeEnum.CaptionOnly) AndAlso IsBackdropSupported)
                                      )

        If shouldEnable Then
            ' WDA_EXCLUDEFROMCAPTURE 仅在 Auto / CaptionOnly 模式且用户显式开启 BackdropExcludeFromCapture 时启用：
            '   - Image 模式不抓屏，永远不需要 WDA。
            '   - Auto / CaptionOnly + 关闭 WDA：截图工具能截到本窗口，但若开启周期刷新会出现"递归自照"纹路 ⇒ 强制纯事件驱动。
            '   - Auto / CaptionOnly + 开启 WDA：可安全周期刷新，但系统截图 / 录屏均无法捕获本窗口。
            If (mode = BackdropModeEnum.Auto OrElse mode = BackdropModeEnum.CaptionOnly) AndAlso _毛玻璃排除截屏 Then
                SetWindowDisplayAffinity(s.HostForm.Handle, WDA_EXCLUDEFROMCAPTURE)
            Else
                SetWindowDisplayAffinity(s.HostForm.Handle, WDA_NONE)
            End If
            If s.Renderer Is Nothing Then
                s.Renderer = New BackdropRenderer(s.HostForm)
                s.Renderer.ApplyParameters(_毛玻璃模糊半径, _毛玻璃模糊次数, _毛玻璃下采样,
                                            _毛玻璃噪点缩放)
                AddHandler s.Renderer.AverageCommitted, Sub(sender2, ev2)
                                                            If _分层阴影自动颜色 Then
                                                                If s.ShadowForm IsNot Nothing Then s.ShadowForm.ForceReset()
                                                                更新阴影(s)
                                                            End If
                                                        End Sub
            End If
            ' 配置源
            s.Renderer.SetSource(mode = BackdropModeEnum.Image, _毛玻璃图片)
            ' Auto / CaptionOnly 模式且未长期启用 WDA 时，让 Renderer 在每次 BitBlt 瞬间临时排除自身，
            ' 避免事件驱动抓屏抓到自己产生镜像反馈。
            s.Renderer.SetTransientExcludeOnCapture(
                (mode = BackdropModeEnum.Auto OrElse mode = BackdropModeEnum.CaptionOnly) AndAlso Not _毛玻璃排除截屏)
            ' 首帧
            s.Renderer.RequestFrame(获取毛玻璃捕获区域(s.HostForm), True)
            重置毛玻璃Tick(s)
        Else
            SetWindowDisplayAffinity(s.HostForm.Handle, WDA_NONE)
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
        Dim needTick As Boolean = (_毛玻璃模式 = BackdropModeEnum.Auto OrElse _毛玻璃模式 = BackdropModeEnum.CaptionOnly) AndAlso
                                  IsBackdropSupported AndAlso
                                  _毛玻璃排除截屏 AndAlso
                                  s.Renderer IsNot Nothing AndAlso
                                  _毛玻璃帧率 > 0

        If Not needTick Then
            If s.BackdropTimer IsNot Nothing Then
                s.BackdropTimer.Stop()
                s.BackdropTimer.Dispose()
                s.BackdropTimer = Nothing
            End If
            Return
        End If

        Dim interval As Integer = Math.Max(16, 1000 \ _毛玻璃帧率)
        If s.BackdropTimer Is Nothing Then
            s.BackdropTimer = New Timer() With {.Interval = interval}
            AddHandler s.BackdropTimer.Tick, Sub(sender, ev) 毛玻璃Tick(s)
            s.BackdropTimer.Start()
        Else
            s.BackdropTimer.Interval = interval
            If Not s.BackdropTimer.Enabled Then s.BackdropTimer.Start()
        End If
    End Sub

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

#Region "按钮区域计算"

    Friend Sub RecalculateButtonBounds(s As PerFormState)
        If s Is Nothing Then Return
        Dim form = s.HostForm
        Dim w As Integer = form.ClientSize.Width
        Dim bdr As Integer = 取缩放边框厚度(form)
        Dim bw As Integer = Math.Max(缩放逻辑尺寸(form, 16), 缩放逻辑尺寸(form, _按钮宽度))
        Dim bh As Integer = 取缩放标题栏高度(form)
        Dim sp As Integer = Math.Max(0, 缩放逻辑尺寸(form, _按钮间距))
        Dim iconSize As Integer = Math.Max(0, 缩放逻辑尺寸(form, _图标大小))
        Dim iconPadLeft As Integer = Math.Max(0, 缩放逻辑尺寸(form, _图标左边距))
        Dim hasMin As Boolean = s.HostForm.MinimizeBox
        Dim hasMax As Boolean = s.HostForm.MaximizeBox
        Dim posRight As Boolean = (_按钮位置 = ButtonPositionEnum.Right)
        Dim iconNone As Boolean = (_图标来源 = IconSourceEnum.None)

        ' 布局签名：所有影响按钮/图标位置的输入生成哈希，避免手工 bit-pack 截断导致缓存误命中。
        Dim sig As Long = HashCode.Combine(w, form.DeviceDpi, bdr, bw, bh, sp, iconSize, iconPadLeft)
        sig = HashCode.Combine(sig, hasMin)
        sig = HashCode.Combine(sig, hasMax, posRight, iconNone)
        If s.LayoutSignature = sig Then Return
        s.LayoutSignature = sig

        ' 用栈数组替代 List(Of Integer)，避免装箱 + 集合分配。
        Dim 列表(2) As Integer
        Dim 数量 As Integer = 0
        If posRight Then
            If hasMin Then 列表(数量) = HTMINBUTTON : 数量 += 1
            If hasMax Then 列表(数量) = HTMAXBUTTON : 数量 += 1
            列表(数量) = HTCLOSE : 数量 += 1
            Dim totalW As Integer = 数量 * bw + Math.Max(0, 数量 - 1) * sp
            Dim startX As Integer = w - bdr - totalW
            For i = 0 To 数量 - 1
                Dim r As New Rectangle(startX + i * (bw + sp), bdr, bw, bh)
                Select Case 列表(i) : Case HTCLOSE : s.CloseRect = r : Case HTMAXBUTTON : s.MaxRect = r : Case HTMINBUTTON : s.MinRect = r : End Select
            Next
        Else
            列表(数量) = HTCLOSE : 数量 += 1
            If hasMax Then 列表(数量) = HTMAXBUTTON : 数量 += 1
            If hasMin Then 列表(数量) = HTMINBUTTON : 数量 += 1
            For i = 0 To 数量 - 1
                Dim r As New Rectangle(bdr + i * (bw + sp), bdr, bw, bh)
                Select Case 列表(i) : Case HTCLOSE : s.CloseRect = r : Case HTMAXBUTTON : s.MaxRect = r : Case HTMINBUTTON : s.MinRect = r : End Select
            Next
        End If
        If Not hasMax Then s.MaxRect = Rectangle.Empty
        If Not hasMin Then s.MinRect = Rectangle.Empty

        If Not iconNone AndAlso iconSize > 0 Then
            Dim iconY As Integer = bdr + (bh - iconSize) \ 2
            If Not posRight Then
                Dim totalBtnW As Integer = 数量 * bw + Math.Max(0, 数量 - 1) * sp
                s.IconRect = New Rectangle(bdr + totalBtnW + iconPadLeft, iconY, iconSize, iconSize)
            Else
                s.IconRect = New Rectangle(bdr + iconPadLeft, iconY, iconSize, iconSize)
            End If
        Else
            s.IconRect = Rectangle.Empty
        End If
    End Sub

#End Region

#Region "绘制"

    Private Function 获取标题栏内容矩形(form As Form) As Rectangle
        If form Is Nothing Then Return Rectangle.Empty
        Return 获取标题栏内容矩形(form, form.ClientSize.Width, form.ClientSize.Height)
    End Function

    Private Function 获取标题栏内容矩形(form As Form, w As Integer, h As Integer) As Rectangle
        Dim bdr As Integer = 取缩放边框厚度(form)
        Dim x As Integer = Math.Min(bdr, Math.Max(0, w))
        Dim y As Integer = Math.Min(bdr, Math.Max(0, h))
        Dim rw As Integer = Math.Max(0, w - bdr * 2)
        Dim rh As Integer = Math.Min(取缩放标题栏高度(form), Math.Max(0, h - bdr * 2))
        Return New Rectangle(x, y, rw, rh)
    End Function

    ''' <summary>
    ''' 为指定窗体执行完整绘制。通常由内部 Paint 事件自动调用。
    ''' <para>
    ''' 绘制流程分三段：① GDI 毛玻璃层（必须留在 GDI 上，原因见函数体内注释）；② D2D V2 scope（DC RT + DirectWrite）；
    ''' ③ 自定义 <c>CaptionPaint</c> 事件（仍以 GDI <see cref="Graphics"/> 暴露）。
    ''' </para>
    ''' <para>
    ''' <b>D3D11 接入</b>：进入 V2 scope 后会显式 touch <see cref="PaintScopeV2.DeviceContext"/>，
    ''' 让当前 Form 的 D3D11 / D2D 1.1 设备真正实例化并完成 <see cref="D3D11Globals.DeviceLost"/> 事件订阅。
    ''' 本控件的实际绘制仍走 DC RT 以保留 DirectWrite ClearType 子像素抗锯齿；
    ''' DeviceContext 在这里只起到"激活设备 + 监听设备丢失"的角色。一旦 D3D 设备进入丢失态
    ''' （TDR / 远程桌面切换 / 驱动崩溃），会被本函数捕获并触发 <see cref="WindowCompositor.NotifyDeviceContextException"/>，
    ''' 下一帧自动用新设备恢复，且只影响本帧像素，不会让进程崩溃。
    ''' </para>
    ''' </summary>
    Public Sub PaintWindow(e As PaintEventArgs, targetForm As Form)
        Dim s = 查找状态(targetForm)
        If s Is Nothing Then Return
        RecalculateButtonBounds(s)

        Dim g As Graphics = e.Graphics
        Dim w As Integer = s.HostForm.ClientSize.Width
        Dim h As Integer = s.HostForm.ClientSize.Height
        If w <= 0 OrElse h <= 0 Then Return
        Dim active As Boolean = s.Activated

        Dim useBackdrop As Boolean = 毛玻璃当前启用(s) AndAlso
                                      s.Renderer.HasFrame
        Dim captionOnly As Boolean = (_毛玻璃模式 = BackdropModeEnum.CaptionOnly)
        Dim fullRect As New Rectangle(0, 0, w, h)
        Dim captionRect As Rectangle = 获取标题栏内容矩形(s.HostForm, w, h)
        Dim backdropRect As Rectangle = If(captionOnly,
                                           New Rectangle(0, 0, w, Math.Min(h, 取缩放标题栏总高度(s.HostForm))),
                                           fullRect)

        ' ── 1) 毛玻璃层（GDI 路径）──
        ' 必须在进入 D2D scope（=BeginPaint → e.Graphics.GetHdc + DCRT.BindDC + BeginDraw）之前完成：
        ' • BeginPaint 一旦发生，e.Graphics 的 HDC 已被释放给 DC RT 独占，再在 e.Graphics 上画会跨越
        '   D2D 的 BeginDraw/EndDraw，造成像素丢失或闪烁；
        ' • BackdropRenderer 的 _currentFrame 是 CPU GDI Bitmap (PArgb)，在 D2D scope 内上传 + 绘制
        '   实测会出现毛玻璃帧不可见的情况（DC RT 与 HDC 的合成时序边界问题）。
        ' 因此“必须 GDI+ 的部分”就是这一层 —— 把毛玻璃 / tint / 噪点画在 e.Graphics 上，
        ' 再进入 D2D scope 继续画标题栏等。
        If useBackdrop Then
            g.SmoothingMode = SmoothingMode.Default
            g.PixelOffsetMode = PixelOffsetMode.Default
            If D2DHelperV2.IsBackgroundSamplingPaint Then
                s.Renderer.DrawToCpu(g, backdropRect)
            Else
                s.Renderer.DrawTo(g, backdropRect)
            End If
            Dim tint = If(active, _毛玻璃Tint颜色, _毛玻璃Tint失焦颜色)
            If tint.A > 0 Then
                _共享画刷.Color = tint
                g.FillRectangle(_共享画刷, backdropRect)
            End If
            If _毛玻璃模糊次数 > 0 AndAlso _毛玻璃噪点不透明度 > 0 Then
                s.Renderer.DrawNoise(g, backdropRect, _毛玻璃噪点不透明度)
            End If
        End If

        ' ── 2) D2D V2 scope：标题栏背景 / 图片 / 遮罩 / 图标 / 按钮 / 边框 / 标题文字 ──
        ' ThisIsYourWindow 只画矩形 / 位图 / DirectWrite 文字（文字在 DC RT 走 ClearType），
        ' 没有任何需要超采的几何（无圆角、无旋转、无抗锯齿曲线），
        ' 因此强制 ssaa=1，省下每帧一次离屏 BitmapRT 申请 + Flush 回采的开销。
        ' GlobalOptions.GlobalSSAA 仍然对其他控件（ModernButton 等）生效，这里只是本控件不参与。
        Const ssaa As Integer = 1
        Dim deviceLost As Boolean = False
        Try
            Using scope = D2DHelperV2.BeginPaint(e, s.HostForm, ssaa)
                If scope Is Nothing Then
                    ' 设计期 / 无 compositor：仅触发外部自定义绘制事件即可。
                    RaiseEvent CaptionPaint(Me, New CaptionPaintEventArgs(g, captionRect, active, s.HostForm))
                    Return
                End If
                Dim compositor = scope.Compositor

                ' ── 2.0 D3D11 设备激活 + 设备丢失订阅触发 ──
                ' 主动 touch DeviceContext 让本 Form 的 D3D11 / D2D 1.1 设备完成实例化与
                ' D3D11Globals.DeviceLost 事件订阅。返回 Nothing（设计器 / RDP 早期）也无关紧要：
                ' 本控件实际绘制走 DC RT 以保留 ClearType；DeviceContext 仅作为"设备状态心跳"存在。
                Dim ctx As ID2D1DeviceContext = compositor.DeviceContext

                Dim gRT As ID2D1RenderTarget = scope.GraphicsLayer
                Dim dcRT As ID2D1DCRenderTarget = scope.DCRenderTarget

                ' 2.1 标题栏底色（仅在没有毛玻璃时绘制）
                If Not useBackdrop AndAlso captionRect.Width > 0 AndAlso captionRect.Height > 0 Then
                    Dim capColor As Color = If(active, _标题栏背景颜色, _标题栏失焦背景颜色)
                    If capColor.A > 0 Then
                        Dim b = compositor.BrushCache.[Get](gRT, capColor)
                        gRT.FillRectangle(D2DGlobals.ToD2DRect(captionRect), b)
                    End If
                End If

                ' 2.2 标题栏背景图片（cover 居中裁切）
                If _标题栏背景图片 IsNot Nothing AndAlso captionRect.Width > 0 AndAlso captionRect.Height > 0 Then
                    绘制标题栏背景图片_D2D(gRT, compositor, captionRect)
                End If

                ' 2.3 标题栏遮罩
                If _标题栏遮罩颜色.A > 0 AndAlso captionRect.Width > 0 AndAlso captionRect.Height > 0 Then
                    Dim b = compositor.BrushCache.[Get](gRT, _标题栏遮罩颜色)
                    gRT.FillRectangle(D2DGlobals.ToD2DRect(captionRect), b)
                End If

                ' 2.4 图标
                绘制图标_D2D(gRT, compositor, s)

                ' 2.5 控制按钮（背景与符号）
                绘制控制按钮_D2D(gRT, compositor, s, s.CloseRect, HTCLOSE)
                If s.HostForm.MaximizeBox Then 绘制控制按钮_D2D(gRT, compositor, s, s.MaxRect, HTMAXBUTTON)
                If s.HostForm.MinimizeBox Then 绘制控制按钮_D2D(gRT, compositor, s, s.MinRect, HTMINBUTTON)

                ' 2.6 外边框
                Dim scaledBorderSize As Integer = 取缩放边框厚度(s.HostForm)
                If scaledBorderSize > 0 Then
                    Dim bdrColor As Color
                    If useBackdrop AndAlso _边框自动颜色 Then
                        bdrColor = s.Renderer.DeriveBorderColor(active, If(active, _边框颜色, _边框失焦颜色))
                    Else
                        bdrColor = If(active, _边框颜色, _边框失焦颜色)
                    End If
                    If bdrColor.A > 0 Then
                        Dim bdr As Integer = Math.Min(scaledBorderSize, Math.Max(0, Math.Min(w, h)))
                        Dim b = compositor.BrushCache.[Get](gRT, bdrColor)
                        If bdr > 0 Then
                            gRT.FillRectangle(D2DGlobals.ToD2DRect(New RectangleF(0, 0, w, Math.Min(bdr, h))), b)
                            If h > bdr Then gRT.FillRectangle(D2DGlobals.ToD2DRect(New RectangleF(0, h - bdr, w, bdr)), b)
                            Dim sideH As Integer = h - bdr * 2
                            If sideH > 0 Then
                                gRT.FillRectangle(D2DGlobals.ToD2DRect(New RectangleF(0, bdr, Math.Min(bdr, w), sideH)), b)
                                If w > bdr Then gRT.FillRectangle(D2DGlobals.ToD2DRect(New RectangleF(w - bdr, bdr, bdr, sideH)), b)
                            End If
                        End If
                    End If
                End If

                ' 2.7 SSAA=1 时 FlushGraphics 是 no-op；保留调用以维持 scope 状态机一致性。
                scope.FlushGraphics()

                ' 2.8 标题文字（D2D / DirectWrite 路径，绘制在 DC RT 上以保留 ClearType 子像素抗锯齿）
                绘制标题文字_D2D(dcRT, compositor, s)
            End Using
        Catch ex As Exception
            ' D2D / D3D11 设备级错误：通知 compositor 失效相关资源 → 吞掉本帧异常 → 请求下一帧重绘。
            ' 非设备级错误：原样抛出，让 WinForms 默认的 Paint 异常处理生效（调试时能看到堆栈）。
            Dim comp = D2DHelperV2.GetCompositor(s.HostForm)
            If comp IsNot Nothing AndAlso comp.NotifyDeviceContextException(ex) Then
                deviceLost = True
            ElseIf D3D11Globals.IsDeviceLostException(ex) Then
                D3D11Globals.HandleDeviceLost(ex)
                deviceLost = True
            Else
                Throw
            End If
        End Try

        If deviceLost Then
            ' 让外部 CaptionPaint 仍能在本帧拿到 GDI Graphics（事件契约一致），
            ' 然后请求下一帧重绘 —— compositor / D3D11 设备会在下一次访问时自动重建。
            Try
                RaiseEvent CaptionPaint(Me, New CaptionPaintEventArgs(g, captionRect, active, s.HostForm))
            Catch
            End Try
            Try : OuterToInnerRefreshScheduler.RequestFull(s.HostForm) : Catch : End Try
            Return
        End If

        ' ── 3) 触发外部自定义绘制事件（仍以 GDI Graphics 暴露，保持兼容）──
        RaiseEvent CaptionPaint(Me, New CaptionPaintEventArgs(g, captionRect, active, s.HostForm))
    End Sub

    Private Sub 绘制标题栏背景图片_D2D(rt As ID2D1RenderTarget, compositor As WindowCompositor, captionRect As Rectangle)
        Dim img As Image = _标题栏背景图片
        If img Is Nothing Then Return
        Dim cw As Integer = captionRect.Width
        Dim ch As Integer = captionRect.Height
        If cw < 1 OrElse ch < 1 Then Return

        Dim cache = compositor.GetBitmapCache(img)
        Dim bmp = cache?.GetBitmap(rt, img)
        If bmp Is Nothing Then Return

        Dim ratioW As Single = CSng(cw) / img.Width
        Dim ratioH As Single = CSng(ch) / img.Height
        Dim ratio As Single = Math.Max(ratioW, ratioH)
        Dim drawW As Single = img.Width * ratio
        Dim drawH As Single = img.Height * ratio
        Dim dx As Single = captionRect.X + (cw - drawW) / 2.0F
        Dim dy As Single = captionRect.Y + (ch - drawH) / 2.0F

        ' 用 captionRect 做矩形几何裁剪，避免图像溢出标题栏。
        Using clipGeo = D2DGlobals.GetD2DFactory().CreateRectangleGeometry(New RectangleF(captionRect.X, captionRect.Y, cw, ch))
            D2DGlobals.PushGeometryClip(rt, clipGeo, New RectangleF(captionRect.X, captionRect.Y, cw, ch))
            Try
                rt.DrawBitmap(bmp,
                              New Vortice.Mathematics.Rect(dx, dy, drawW, drawH),
                              1.0F,
                              BitmapInterpolationMode.Linear,
                              Nothing)
            Finally
                rt.PopLayer()
            End Try
        End Using
    End Sub

    Private Sub 绘制图标_D2D(rt As ID2D1RenderTarget, compositor As WindowCompositor, s As PerFormState)
        If _图标来源 = IconSourceEnum.None OrElse s.IconRect.IsEmpty Then Return
        Dim img As Image = Nothing
        If _图标来源 = IconSourceEnum.Custom Then
            img = _自定义图标
        ElseIf _图标来源 = IconSourceEnum.FormIcon AndAlso s.HostForm?.Icon IsNot Nothing Then
            If s.CachedIconSource IsNot s.HostForm.Icon Then
                s.CachedIconBitmap?.Dispose()
                s.CachedIconBitmap = s.HostForm.Icon.ToBitmap()
                s.CachedIconSource = s.HostForm.Icon
                ' V2：新 Bitmap 引用即新缓存键，旧条目随 Form Dispose 时一同清理；无需手动 Invalidate。
            End If
            img = s.CachedIconBitmap
        End If
        If img Is Nothing Then Return
        Dim cache = compositor.GetBitmapCache(img)
        Dim bmp = cache?.GetBitmap(rt, img)
        If bmp Is Nothing Then Return
        Dim r = s.IconRect
        Dim srcRect As New RectangleF(0, 0, img.Width, img.Height)
        rt.DrawBitmap(bmp,
                      New Vortice.Mathematics.Rect(r.X, r.Y, r.Width, r.Height),
                      1.0F,
                      BitmapInterpolationMode.Linear,
                       D2DGlobals.ToD2DRect(srcRect))
    End Sub

    Private Sub 绘制控制按钮_D2D(rt As ID2D1RenderTarget, compositor As WindowCompositor, s As PerFormState, rect As Rectangle, htValue As Integer)
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
        Dim vis As New RectangleF(rect.X + buttonPadding.Left, rect.Y + buttonPadding.Top,
                                  rect.Width - buttonPadding.Horizontal, rect.Height - buttonPadding.Vertical)
        If vis.Width <= 0 OrElse vis.Height <= 0 Then Return

        ' 背景
        If bgColor.A > 0 Then
            Dim r As Integer = Math.Min(Math.Max(0, 缩放逻辑尺寸(s.HostForm, _按钮圆角半径)), CInt(Math.Min(vis.Width, vis.Height)) \ 2)
            Dim bgBrush = compositor.BrushCache.[Get](rt, bgColor)
            If r > 0 Then
                Using geo = RectangleRenderer.创建圆角矩形几何(vis, r)
                    rt.FillGeometry(geo, bgBrush)
                End Using
            Else
                rt.FillRectangle(D2DGlobals.ToD2DRect(vis), bgBrush)
            End If
        End If

        ' 符号
        If symColor.A = 0 Then Return
        Dim sz As Integer = Math.Max(缩放逻辑尺寸(s.HostForm, 4), 缩放逻辑尺寸(s.HostForm, _按钮符号大小))
        Dim cx As Single = vis.X + (vis.Width - sz) / 2.0F
        Dim cy As Single = vis.Y + (vis.Height - sz) / 2.0F
        Dim lw As Single = Math.Max(0.5F * 取Dpi缩放(s.HostForm), 缩放逻辑尺寸(s.HostForm, _按钮符号线宽))
        Dim pen = compositor.BrushCache.[Get](rt, symColor)
        If True Then
            Select Case htValue
                Case HTCLOSE
                    rt.DrawLine(New Vector2(cx, cy), New Vector2(cx + sz, cy + sz), pen, lw)
                    rt.DrawLine(New Vector2(cx + sz, cy), New Vector2(cx, cy + sz), pen, lw)
                Case HTMAXBUTTON
                    If s.HostForm.WindowState = FormWindowState.Maximized Then
                        Dim off As Single = sz * 0.25F
                        rt.DrawRectangle(New Vortice.Mathematics.Rect(cx + off, cy, sz - off, sz - off), pen, lw)
                        rt.DrawRectangle(New Vortice.Mathematics.Rect(cx, cy + off, sz - off, sz - off), pen, lw)
                    Else
                        rt.DrawRectangle(New Vortice.Mathematics.Rect(cx, cy, sz, sz), pen, lw)
                    End If
                Case HTMINBUTTON
                    Dim mid As Single = cy + sz / 2.0F
                    rt.DrawLine(New Vector2(cx, mid), New Vector2(cx + sz, mid), pen, lw)
            End Select
        End If
    End Sub

    Private Function 获取标题文字布局矩形(s As PerFormState) As RectangleF
        If s Is Nothing OrElse s.HostForm Is Nothing Then Return RectangleF.Empty
        Dim captionRect As Rectangle = 获取标题栏内容矩形(s.HostForm)
        If captionRect.Width <= 0 OrElse captionRect.Height <= 0 Then Return RectangleF.Empty

        Dim leftEdge, rightEdge As Integer
        Dim titlePadLeft As Integer = Math.Max(0, 缩放逻辑尺寸(s.HostForm, _标题文字左边距))
        Dim titlePadRight As Integer = Math.Max(0, 缩放逻辑尺寸(s.HostForm, _标题文字右边距))
        If _按钮位置 = ButtonPositionEnum.Right Then
            leftEdge = If(Not s.IconRect.IsEmpty, s.IconRect.Right + titlePadLeft, captionRect.Left + titlePadLeft)
            Dim btnLeft As Integer = s.CloseRect.Left
            If s.HostForm.MaximizeBox AndAlso Not s.MaxRect.IsEmpty Then btnLeft = Math.Min(btnLeft, s.MaxRect.Left)
            If s.HostForm.MinimizeBox AndAlso Not s.MinRect.IsEmpty Then btnLeft = Math.Min(btnLeft, s.MinRect.Left)
            rightEdge = btnLeft - titlePadRight
        Else
            If Not s.IconRect.IsEmpty Then
                leftEdge = s.IconRect.Right + titlePadLeft
            Else
                Dim btnRight As Integer = s.CloseRect.Right
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
        Return Rectangle.Intersect(New Rectangle(Point.Empty, s.HostForm.ClientSize), dirty)
    End Function

    Private Function 获取标题栏渲染文本(form As Form) As String
        Dim realTitle As String = If(form?.Text, String.Empty)
        If String.IsNullOrEmpty(_标题文字私有协议) OrElse
           Not 是首个附加窗体(form) Then Return realTitle
        Return _标题文字私有协议.Replace(TitleTextPrivateProtocolTitleToken, realTitle)
    End Function

    Private Sub 绘制标题文字_D2D(rt As ID2D1DCRenderTarget, compositor As WindowCompositor, s As PerFormState)
        Dim text As String = 获取标题栏渲染文本(s.HostForm)
        If String.IsNullOrEmpty(text) Then Return
        Dim font As Font = If(_标题文字字体, s.HostForm.Font)
        If font Is Nothing Then Return
        Dim fgColor As Color = If(s.Activated, _标题文字颜色, _标题文字失焦颜色)
        If fgColor.A = 0 Then Return

        Dim textRect As RectangleF = 获取标题文字布局矩形(s)
        If textRect.Width <= 0 OrElse textRect.Height <= 0 Then Return

        Dim align As Vortice.DirectWrite.TextAlignment
        Select Case _标题文字对齐
            Case TitleAlignEnum.Center : align = Vortice.DirectWrite.TextAlignment.Center
            Case TitleAlignEnum.Right : align = Vortice.DirectWrite.TextAlignment.Trailing
            Case Else : align = Vortice.DirectWrite.TextAlignment.Leading
        End Select

        ' DirectWrite 字号必须叠加 DPI 缩放（参考 ModernButton.vb 中的注释说明）。
        Dim dpiScale As Single = s.HostForm.DeviceDpi / 96.0F
        Dim sizePx As Single = font.SizeInPoints * (96.0F / 72.0F) * dpiScale
        Dim weight As Vortice.DirectWrite.FontWeight = If(font.Bold, Vortice.DirectWrite.FontWeight.Bold, Vortice.DirectWrite.FontWeight.Normal)
        Dim style As Vortice.DirectWrite.FontStyle = If(font.Italic, Vortice.DirectWrite.FontStyle.Italic, Vortice.DirectWrite.FontStyle.Normal)

        Dim fmt = compositor.TextFormatCache.[Get](font.FontFamily.Name, weight, style, sizePx, align, ParagraphAlignment.Center, True)
        Dim brush = compositor.BrushCache.[Get](rt, fgColor)
        rt.DrawText(text, fmt, D2DGlobals.ToD2DRect(textRect), brush,
                    DrawTextOptions.Clip, Vortice.DCommon.MeasuringMode.Natural)
    End Sub

    ''' <summary>请求指定窗体重绘标题栏区域。</summary>
    Public Sub InvalidateCaption(form As Form, Optional immediate As Boolean = False)
        If form IsNot Nothing AndAlso form.IsHandleCreated Then
            form.Invalidate(New Rectangle(0, 0, form.ClientSize.Width, Math.Min(form.ClientSize.Height, 取缩放标题栏总高度(form))), False)
            If immediate Then form.Update()
        End If
    End Sub

#End Region

#Region "附加 / 分离"

    ''' <summary>
    ''' 将当前样式附加到目标窗体。可多次调用以附加到不同窗体，所有窗体共享同一套外观属性。
    ''' 建议在 Form.Load 中调用。
    ''' </summary>
    Public Sub Attach(targetForm As Form)
        ArgumentNullException.ThrowIfNull(targetForm)
        If _首个附加窗体 Is Nothing Then _首个附加窗体 = targetForm
        If Not targetForm.IsHandleCreated Then
            If _pendingAttachHandlers.ContainsKey(targetForm) Then Return
            Dim handler As EventHandler = Nothing
            handler = Sub(sender2, ev)
                          RemoveHandler targetForm.HandleCreated, handler
                          _pendingAttachHandlers.Remove(targetForm)
                          Attach(targetForm)
                      End Sub
            _pendingAttachHandlers(targetForm) = handler
            AddHandler targetForm.HandleCreated, handler
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
        应用Dwm窗口属性(hWnd, _显示动画模式 <> WindowShowAnimationMode.DWM)

        ' ── 第四步：注册拦截器 ──
        s.Interceptor = New WindowMessageInterceptor(Me, s)
        _forms(hWnd) = s
        SyncLock _attachedFormsLock
            _attachedForms(targetForm) = Me
        End SyncLock

        ' ── 第五步：使样式变更生效 ──
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                     CUInt(SWP_FRAMECHANGED Or SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOZORDER))

        Dim setStyleMethod = GetType(Control).GetMethod("SetStyle", BindingFlags.Instance Or BindingFlags.NonPublic)
        setStyleMethod?.Invoke(targetForm, New Object() {
            ControlStyles.OptimizedDoubleBuffer Or ControlStyles.AllPaintingInWmPaint, True})

        AddHandler targetForm.Paint, AddressOf 宿主窗口_Paint
        AddHandler targetForm.FormClosed, AddressOf 宿主窗口_FormClosed
        AddHandler targetForm.HandleDestroyed, AddressOf 宿主窗口_HandleDestroyed
        AddHandler targetForm.VisibleChanged, AddressOf 宿主窗口_VisibleChanged
        AddHandler targetForm.FontChanged, AddressOf 宿主窗口_FontChanged
        AddHandler targetForm.TextChanged, AddressOf HostForm_TextChanged
        RecalculateButtonBounds(s)
        更新窗口内边距(s)
        OuterToInnerRefreshScheduler.RequestFull(targetForm)
        更新阴影(s)
        应用毛玻璃状态(s)
    End Sub

    ''' <summary>从指定窗体分离。</summary>
    Public Sub Detach(targetForm As Form)
        If targetForm Is Nothing Then Return

        Dim pendingHandler As EventHandler = Nothing
        If _pendingAttachHandlers.TryGetValue(targetForm, pendingHandler) Then
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
        If s Is Nothing Then Return
        _forms.Remove(key)
        SyncLock _attachedFormsLock
            Dim owner As ThisIsYourWindow = Nothing
            If _attachedForms.TryGetValue(targetForm, owner) AndAlso owner Is Me Then
                _attachedForms.Remove(targetForm)
            End If
        End SyncLock

        s.CachedIconBitmap?.Dispose()
        ' V2：D2D 资源（DC RT / SSAA / 笔刷 / TextFormat / 位图缓存）全部由 WindowCompositor 管理，
        '       会在 Form.HandleDestroyed 时自动释放，这里无需重复清理。
        s.Interceptor?.ReleaseHandle()
        销毁阴影(s)
        If s.BackdropTimer IsNot Nothing Then
            s.BackdropTimer.Stop()
            s.BackdropTimer.Dispose()
            s.BackdropTimer = Nothing
        End If
        If s.Renderer IsNot Nothing Then
            Try
                If targetForm.IsHandleCreated Then SetWindowDisplayAffinity(targetForm.Handle, WDA_NONE)
            Catch
            End Try
            s.Renderer.Dispose()
            s.Renderer = Nothing
        End If
        RemoveHandler targetForm.Paint, AddressOf 宿主窗口_Paint
        RemoveHandler targetForm.FormClosed, AddressOf 宿主窗口_FormClosed
        RemoveHandler targetForm.HandleDestroyed, AddressOf 宿主窗口_HandleDestroyed
        RemoveHandler targetForm.VisibleChanged, AddressOf 宿主窗口_VisibleChanged
        RemoveHandler targetForm.FontChanged, AddressOf 宿主窗口_FontChanged
        RemoveHandler targetForm.TextChanged, AddressOf HostForm_TextChanged
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
        OuterToInnerRefreshScheduler.RequestFull(targetForm)
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
        Dim bw As Integer = Math.Max(1, 缩放逻辑尺寸(s.HostForm, _调整边框宽度))
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
                    Dim currentClientSize As Size = _state.HostForm.ClientSize
                    Dim clientSizeChanged As Boolean = (currentClientSize <> _state.LastClientSize)
                    Dim minimizedNow As Boolean = (_state.HostForm IsNot Nothing AndAlso
                                                   _state.HostForm.WindowState = FormWindowState.Minimized)
                    _owner.RecalculateButtonBounds(_state)
                    If minimizedNow Then
                        ' 最小化阶段没有可见客户区，避免把一次隐藏态 WM_SIZE 扩散成全量重绘。
                    ElseIf Not _owner.可跳过WMSize客户区刷新(_state, clientSizeChanged) Then
                        OuterToInnerRefreshScheduler.RequestFull(_state.HostForm)
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
                    If _owner._标题文字颜色 <> _owner._标题文字失焦颜色 Then
                        _owner.InvalidateTitleText(_state, True)
                    End If
                    If Not _owner.毛玻璃当前启用(_state) Then
                        OuterToInnerRefreshScheduler.RequestFull(_state.HostForm)
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

                Case WM_PAINT, WM_ERASEBKGND
                    If _state.DeferredClientBoundsActive Then
                        If m.Msg = WM_PAINT Then ValidateRect(_state.HostForm.Handle, IntPtr.Zero)
                        m.Result = If(m.Msg = WM_ERASEBKGND, New IntPtr(1), IntPtr.Zero)
                        Return
                    End If
                    MyBase.WndProc(m)
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
                        Dim t As New Timer() With {.Interval = 15}
                        Dim elapsed As Integer = 0
                        Dim duration As Integer = _owner._动画持续时间
                        AddHandler t.Tick, Sub(s, ev)
                                               elapsed += 15
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
                    MyBase.WndProc(m)
                    _owner.处理DpiChanged(_state)
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
