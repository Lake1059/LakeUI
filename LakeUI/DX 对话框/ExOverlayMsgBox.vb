Imports System.Runtime.InteropServices

' ═══════════════════════════════════════════════════════════════════════════
'  ExOverlayMsgBox — LakeUI 全屏遮罩消息框
'
'  ● 外观类似于 Windows 用户账户控制 (UAC) 的全屏遮罩对话框：
'    半透明暗色遮罩 + 居中卡片式对话框，Win10 风格类 UWP，Win11 圆角。
'
'  ● 用法与 ExMsgBox / MsgBox 完全一致：
'      ExOverlayMsgBox("Hello World")
'      ExOverlayMsgBox("确认删除？", MsgBoxStyle.YesNo Or MsgBoxStyle.Question, "提示")
'
'  ● 自定义按钮（返回从左到右的按钮索引，0 起始；ESC/关闭返回 -1）：
'      Dim idx = ExOverlayMsgBox("文件未保存", {"保存", "不保存", "取消"}, "提示")
'      ' idx = 0 → 保存, 1 → 不保存, 2 → 取消, -1 → ESC/关闭
'      Dim idx2 = ExOverlayMsgBox("操作", {"继续", "中止"}, "提示", MsgBoxStyle.Question, 0)
'
'  ● 全局主题设置：
'      ExOverlayMsgBoxTheme.Current = ExOverlayMsgBoxTheme.CreateDark()   ' 暗色（默认）
'      ExOverlayMsgBoxTheme.Current = ExOverlayMsgBoxTheme.CreateLight()  ' 亮色
'      ExOverlayMsgBoxTheme.Current.OverlayOpacity = 180                  ' 遮罩透明度
'
'  ● 指定父窗口（遮罩仅覆盖目标窗口，卡片居中于目标窗口）：
'      ExOverlayMsgBox(Me, "消息", MsgBoxStyle.OkCancel)
' ═══════════════════════════════════════════════════════════════════════════

#Region "主题"

''' <summary>
''' ExOverlayMsgBox 全局主题配置。
''' 通过 <see cref="Current"/> 设置当前主题，使用 <see cref="CreateDark"/>/<see cref="CreateLight"/> 加载预设，或逐项定制任意颜色。
''' </summary>
Public Class ExOverlayMsgBoxTheme

    ''' <summary>当前全局主题（默认暗色）。</summary>
    Public Shared Property Current As New ExOverlayMsgBoxTheme()

    ' ── 遮罩 ──
    ''' <summary>遮罩背景色。</summary>
    Public Property OverlayBackColor As Color = Color.Black
    ''' <summary>遮罩不透明度 (0-255)。</summary>
    Public Property OverlayOpacity As Integer = 180

    ' ── 卡片 ──
    ''' <summary>卡片背景色。</summary>
    Public Property CardBackColor As Color = Color.FromArgb(44, 44, 44)
    ''' <summary>卡片边框颜色。</summary>
    Public Property CardBorderColor As Color = Color.FromArgb(70, 70, 70)
    ''' <summary>卡片圆角半径。</summary>
    Public Property CardBorderRadius As Integer = 8

    ' ── 标题 ──
    ''' <summary>标题文字颜色。</summary>
    Public Property TitleForeColor As Color = Color.FromArgb(240, 240, 240)

    ' ── 内容区 ──
    ''' <summary>消息文本颜色。</summary>
    Public Property MessageForeColor As Color = Color.FromArgb(220, 220, 220)

    ' ── 按钮区 ──
    ''' <summary>按钮区域背景色。</summary>
    Public Property ButtonAreaBackColor As Color = Color.FromArgb(50, 50, 50)
    ''' <summary>普通按钮背景色。</summary>
    Public Property ButtonBackColor As Color = Color.FromArgb(60, 60, 60)
    ''' <summary>普通按钮文字颜色。</summary>
    Public Property ButtonForeColor As Color = Color.FromArgb(220, 220, 220)
    ''' <summary>普通按钮边框颜色。</summary>
    Public Property ButtonBorderColor As Color = Color.FromArgb(80, 80, 80)
    ''' <summary>普通按钮悬停时背景色。</summary>
    Public Property ButtonHoverBackColor As Color = Color.FromArgb(75, 75, 75)
    ''' <summary>普通按钮按下时背景色。</summary>
    Public Property ButtonPressedBackColor As Color = Color.FromArgb(45, 45, 45)
    ''' <summary>按钮圆角半径。</summary>
    Public Property ButtonBorderRadius As Integer = 4

    ' ── 默认按钮（高亮）──
    ''' <summary>默认（高亮）按钮背景色。</summary>
    Public Property AccentButtonBackColor As Color = Color.FromArgb(0, 95, 184)
    ''' <summary>默认（高亮）按钮文字颜色。</summary>
    Public Property AccentButtonForeColor As Color = Color.White
    ''' <summary>默认（高亮）按钮悬停时背景色。</summary>
    Public Property AccentButtonHoverBackColor As Color = Color.FromArgb(30, 115, 200)
    ''' <summary>默认（高亮）按钮按下时背景色。</summary>
    Public Property AccentButtonPressedBackColor As Color = Color.FromArgb(0, 75, 155)
    ''' <summary>默认（高亮）按钮边框颜色。</summary>
    Public Property AccentButtonBorderColor As Color = Color.FromArgb(0, 95, 184)

    ''' <summary>创建暗色主题预设。</summary>
    Public Shared Function CreateDark() As ExOverlayMsgBoxTheme
        Return New ExOverlayMsgBoxTheme()
    End Function

    ''' <summary>创建亮色主题预设。</summary>
    Public Shared Function CreateLight() As ExOverlayMsgBoxTheme
        Return New ExOverlayMsgBoxTheme With {
            .OverlayBackColor = Color.Black,
            .OverlayOpacity = 140,
            .CardBackColor = Color.FromArgb(249, 249, 249),
            .CardBorderColor = Color.FromArgb(200, 200, 200),
            .CardBorderRadius = 8,
            .TitleForeColor = Color.FromArgb(20, 20, 20),
            .MessageForeColor = Color.FromArgb(30, 30, 30),
            .ButtonAreaBackColor = Color.FromArgb(238, 238, 238),
            .ButtonBackColor = Color.FromArgb(253, 253, 253),
            .ButtonForeColor = Color.FromArgb(30, 30, 30),
            .ButtonBorderColor = Color.FromArgb(200, 200, 200),
            .ButtonHoverBackColor = Color.FromArgb(240, 240, 240),
            .ButtonPressedBackColor = Color.FromArgb(218, 218, 218),
            .AccentButtonBackColor = Color.FromArgb(0, 95, 184),
            .AccentButtonForeColor = Color.White,
            .AccentButtonHoverBackColor = Color.FromArgb(30, 115, 200),
            .AccentButtonPressedBackColor = Color.FromArgb(0, 75, 155),
            .AccentButtonBorderColor = Color.FromArgb(0, 95, 184)
        }
    End Function

End Class

#End Region

#Region "公共接口"

''' <summary>
''' 提供全局可调用的 ExOverlayMsgBox() 函数，显示遮罩消息框（类似 UAC 风格）。
''' 无 Owner 时遮罩覆盖全部屏幕；指定 Owner 时遮罩仅覆盖目标窗口。
''' </summary>
Public Module ExOverlayMsgBoxModule

    ''' <summary>
    ''' 显示全屏遮罩消息框。
    ''' </summary>
    ''' <param name="Prompt">消息内容。</param>
    ''' <param name="Buttons">按钮组合与图标样式（MsgBoxStyle 位标志组合）。</param>
    ''' <param name="Title">标题栏文字；为 Nothing 时使用应用名称。</param>
    ''' <returns>用户点击的按钮对应的 MsgBoxResult。</returns>
    Public Function ExOverlayMsgBox(
        Prompt As Object,
        Optional Buttons As Integer = 0,
        Optional Title As Object = Nothing
    ) As MsgBoxResult
        Return 显示(Nothing, Prompt, Buttons, Title)
    End Function

    ''' <summary>
    ''' 显示遮罩消息框并指定父窗口（遮罩仅覆盖目标窗口）。
    ''' </summary>
    Public Function ExOverlayMsgBox(
        Owner As IWin32Window,
        Prompt As Object,
        Optional Buttons As Integer = 0,
        Optional Title As Object = Nothing
    ) As MsgBoxResult
        Return 显示(Owner, Prompt, Buttons, Title)
    End Function

    ''' <summary>
    ''' 显示全屏遮罩消息框（自定义按钮）。
    ''' </summary>
    ''' <param name="Prompt">消息内容。</param>
    ''' <param name="ButtonTexts">自定义按钮文本数组，按从左到右顺序排列。</param>
    ''' <param name="Title">标题栏文字；为 Nothing 时使用应用名称。</param>
    ''' <param name="Icon">图标样式（仅使用 MsgBoxStyle 的图标部分，如 MsgBoxStyle.Question）。</param>
    ''' <param name="DefaultButton">默认（高亮）按钮的索引（从左到右，0 起始）。</param>
    ''' <returns>用户点击的按钮索引（从左到右，0 起始）。ESC/关闭返回 -1。</returns>
    Public Function ExOverlayMsgBox(
        Prompt As Object,
        ButtonTexts() As String,
        Optional Title As Object = Nothing,
        Optional Icon As MsgBoxStyle = 0,
        Optional DefaultButton As Integer = 0
    ) As Integer
        Return 显示自定义(Nothing, Prompt, ButtonTexts, Title, Icon, DefaultButton)
    End Function

    ''' <summary>
    ''' 显示遮罩消息框（自定义按钮）并指定父窗口。
    ''' </summary>
    Public Function ExOverlayMsgBox(
        Owner As IWin32Window,
        Prompt As Object,
        ButtonTexts() As String,
        Optional Title As Object = Nothing,
        Optional Icon As MsgBoxStyle = 0,
        Optional DefaultButton As Integer = 0
    ) As Integer
        Return 显示自定义(Owner, Prompt, ButtonTexts, Title, Icon, DefaultButton)
    End Function

    Private Sub 创建遮罩(owner As IWin32Window, ByRef ownerCtrl As Control, ByRef overlay As ExOverlayBackdropForm)
        Dim theme = ExOverlayMsgBoxTheme.Current
        Dim 有拥有者 = owner IsNot Nothing AndAlso TypeOf owner Is Control
        Dim 遮罩范围 As Rectangle

        If 有拥有者 Then
            ownerCtrl = DirectCast(owner, Control)
            遮罩范围 = ownerCtrl.RectangleToScreen(ownerCtrl.ClientRectangle)
        Else
            ownerCtrl = Nothing
            遮罩范围 = SystemInformation.VirtualScreen
        End If

        overlay = New ExOverlayBackdropForm(遮罩范围, theme, Not 有拥有者, ownerCtrl)
    End Sub

    Private Function 显示(owner As IWin32Window, prompt As Object, buttons As Integer, title As Object) As MsgBoxResult
        Dim ownerCtrl As Control = Nothing
        Dim overlay As ExOverlayBackdropForm = Nothing
        创建遮罩(owner, ownerCtrl, overlay)

        Using overlay
            Using card As New ExOverlayMsgBoxForm(
                If(prompt?.ToString(), String.Empty), buttons,
                If(title?.ToString(), Application.ProductName),
                ExOverlayMsgBoxTheme.Current, overlay, ownerCtrl)
                card.准备毛玻璃()
                绑定遮罩与卡片(overlay, card)
                If owner IsNot Nothing Then
                    overlay.ShowDialog(owner)
                Else
                    overlay.ShowDialog()
                End If
                Return card.Result
            End Using
        End Using
    End Function

    Private Function 显示自定义(owner As IWin32Window, prompt As Object, buttonTexts() As String, title As Object, icon As MsgBoxStyle, defaultButton As Integer) As Integer
        Dim ownerCtrl As Control = Nothing
        Dim overlay As ExOverlayBackdropForm = Nothing
        创建遮罩(owner, ownerCtrl, overlay)

        Using overlay
            Using card As New ExOverlayMsgBoxForm(
                If(prompt?.ToString(), String.Empty), buttonTexts,
                If(title?.ToString(), Application.ProductName),
                ExOverlayMsgBoxTheme.Current, overlay, ownerCtrl,
                CInt(icon) And &HF0, defaultButton)
                card.准备毛玻璃()
                绑定遮罩与卡片(overlay, card)
                If owner IsNot Nothing Then
                    overlay.ShowDialog(owner)
                Else
                    overlay.ShowDialog()
                End If
                Return card.CustomButtonResult
            End Using
        End Using
    End Function

    Private Sub 绑定遮罩与卡片(overlay As ExOverlayBackdropForm, card As ExOverlayMsgBoxForm)
        Dim cardClosed As Boolean = False
        AddHandler overlay.Shown,
            Sub()
                If card.IsDisposed Then Return
                card.Show(overlay)
                card.Activate()
            End Sub
        AddHandler card.FormClosed,
            Sub()
                cardClosed = True
                If overlay IsNot Nothing AndAlso Not overlay.IsDisposed Then overlay.Close()
            End Sub
        AddHandler overlay.FormClosed,
            Sub()
                If Not cardClosed AndAlso card IsNot Nothing AndAlso Not card.IsDisposed Then card.Close()
            End Sub
    End Sub

End Module

#End Region

#Region "D2D 文本度量"

Friend Module ExOverlayMsgBoxTextMetrics

    Private Const 按钮水平留白 As Single = 24.0F
    Private ReadOnly 对话框文本标志 As TextFormatFlags =
        TextFormatFlags.WordBreak Or TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding
    Private ReadOnly 按钮文本标志 As TextFormatFlags =
        TextFormatFlags.SingleLine Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding

    Friend Function MeasureDialogText(owner As Control, text As String, font As Font, proposedSize As Size, dpiScale As Single) As Size
        Dim safeSize As New Size(Math.Max(1, proposedSize.Width), Math.Max(1, proposedSize.Height))
        Return TextRenderer.MeasureText(If(text, ""), font, safeSize, 对话框文本标志)
    End Function

    Friend Function MeasureButtonWidth(owner As Control, text As String, font As Font, minimumWidth As Integer, dpiScale As Single) As Integer
        Dim displayText = GetModernButtonDisplayText(If(text, ""))
        Dim textSize = TextRenderer.MeasureText(displayText, font, New Size(Integer.MaxValue, Integer.MaxValue), 按钮文本标志)
        Dim measuredWidth = CInt(Math.Ceiling(textSize.Width + 按钮水平留白 * dpiScale))
        Return Math.Max(minimumWidth, measuredWidth)
    End Function

    Private Function GetModernButtonDisplayText(text As String) As String
        If String.IsNullOrEmpty(text) OrElse text.IndexOf("&"c) < 0 Then Return If(text, "")

        Dim sb As New System.Text.StringBuilder(text.Length)
        Dim i As Integer = 0
        While i < text.Length
            Dim ch As Char = text(i)
            If ch = "&"c Then
                If i + 1 < text.Length AndAlso text(i + 1) = "&"c Then
                    sb.Append("&"c)
                    i += 2
                    Continue While
                End If
                i += 1
                Continue While
            End If
            sb.Append(ch)
            i += 1
        End While
        Return sb.ToString()
    End Function

End Module

#End Region

#Region "遮罩窗体"

''' <summary>
''' 半透明遮罩背景窗体，通过 <see cref="Form.Opacity"/> 实现真半透明效果。
''' </summary>
Friend Class ExOverlayBackdropForm
    Inherits Form
    Implements V3_IGpuRenderable, V3_IGpuInvalidationSource

    <DllImport("dwmapi.dll")>
    Private Shared Function DwmSetWindowAttribute(hwnd As IntPtr, attr As Integer, ByRef attrValue As Integer, attrSize As Integer) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(hWnd As IntPtr, Msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function ReleaseCapture() As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function EnableWindow(hWnd As IntPtr, bEnable As Boolean) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SetWindowDisplayAffinity(hWnd As IntPtr, dwAffinity As Integer) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    Private Const DWMWA_WINDOW_CORNER_PREFERENCE As Integer = 33
    Private Const DWMWCP_DONOTROUND As Integer = 1
    Private Const WM_NCLBUTTONDOWN As Integer = &HA1
    Private Const HT_CAPTION As Integer = &H2
    Private Const WDA_NONE As Integer = &H0
    Private Const WDA_EXCLUDEFROMCAPTURE As Integer = &H11

    Private 淡入计时器 As PrecisionTimer
    Private ReadOnly 目标不透明度 As Double
    Private ReadOnly 跟踪目标 As Control

    ''' <summary>遮罩范围发生变化时触发（Owner 窗口移动或调整大小）。</summary>
    Public Event BoundsUpdated As EventHandler

    Public Sub New(bounds As Rectangle, theme As ExOverlayMsgBoxTheme, topMost As Boolean, owner As Control)
        Me.FormBorderStyle = FormBorderStyle.None
        Me.ShowInTaskbar = False
        Me.StartPosition = FormStartPosition.Manual
        Me.Bounds = bounds
        Me.BackColor = theme.OverlayBackColor
        Me.Opacity = 0
        Me.TopMost = topMost
        目标不透明度 = Math.Max(0, Math.Min(255, theme.OverlayOpacity)) / 255.0

        ' 跟踪 Owner 窗口的移动与调整大小
        跟踪目标 = owner
        If 跟踪目标 IsNot Nothing Then
            AddHandler 跟踪目标.LocationChanged, AddressOf 目标位置变化
            AddHandler 跟踪目标.SizeChanged, AddressOf 目标位置变化
        End If
    End Sub

    Private Sub 目标位置变化(sender As Object, e As EventArgs)
        If 跟踪目标 Is Nothing Then Return

        ' Owner 最小化 → 遮罩也最小化（卡片作为被拥有窗口会自动隐藏）
        If TypeOf 跟踪目标 Is Form Then
            Dim ownerForm = DirectCast(跟踪目标, Form)
            If ownerForm.WindowState = FormWindowState.Minimized Then
                If Me.WindowState <> FormWindowState.Minimized Then
                    Me.WindowState = FormWindowState.Minimized
                End If
                Return
            End If
            If Me.WindowState = FormWindowState.Minimized Then
                Me.WindowState = FormWindowState.Normal
            End If
        End If

        Me.Bounds = 跟踪目标.RectangleToScreen(跟踪目标.ClientRectangle)
        RaiseEvent BoundsUpdated(Me, EventArgs.Empty)
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Try
            Dim pref As Integer = DWMWCP_DONOTROUND
            Dim unused = DwmSetWindowAttribute(Me.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, pref, 4)
        Catch
        End Try
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        RequestV3Render()
        淡入计时器 = 创建淡入计时器()
        淡入计时器.Start()
    End Sub

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' V3-only: overlay pixels are emitted by RenderGpu.
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        If context Is Nothing OrElse ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then Return
        context.FillRectangle(New RectangleF(0, 0, ClientSize.Width, ClientSize.Height), Color.FromArgb(255, Me.BackColor))
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Private Sub RequestV3Render()
        RequestV3Render(New Rectangle(Point.Empty, Me.Size))
    End Sub

    Private Sub RequestV3Render(dirtyRect As Rectangle)
        If IsDisposed Then Return
        V3_InvalidationRouter.RequestRender(Me, dirtyRect)
    End Sub

    Private Sub 淡入动画帧(sender As Object, e As EventArgs)
        Me.Opacity += 0.06
        If Me.Opacity >= 目标不透明度 Then
            Me.Opacity = 目标不透明度
            淡入计时器.Stop()
            淡入计时器.Dispose()
            淡入计时器 = Nothing
        End If
    End Sub

    Private Shared Function FrameIntervalMilliseconds(fps As Integer) As Integer
        fps = Math.Max(1, fps)
        Return Math.Max(1, CInt(Math.Ceiling(1000.0R / fps)))
    End Function

    Private Function 创建淡入计时器() As PrecisionTimer
        Dim timer As New PrecisionTimer() With {
            .Interval = FrameIntervalMilliseconds(60),
            .DispatchMode = PrecisionTimer.DispatchModeEnum.NonBlocking,
            .OverrunPolicy = PrecisionTimer.OverrunPolicyEnum.Drop,
            .WorkerThreadCount = 1,
            .SynchronizingObject = Me
        }
        AddHandler timer.Tick, AddressOf 淡入动画帧
        Return timer
    End Function

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button = MouseButtons.Left Then 开始拖拽Owner()
    End Sub

    Private Sub 开始拖拽Owner()
        If 跟踪目标 Is Nothing OrElse Not TypeOf 跟踪目标 Is Form Then Return
        Dim hWnd = 跟踪目标.Handle
        EnableWindow(hWnd, True)
        ReleaseCapture()
        SendMessage(hWnd, WM_NCLBUTTONDOWN, New IntPtr(HT_CAPTION), IntPtr.Zero)
        EnableWindow(hWnd, False)
        目标位置变化(Me, EventArgs.Empty)
        Activate()
    End Sub

    Friend Sub 执行排除捕获(action As Action)
        If action Is Nothing Then Return
        Dim applied As Boolean = False
        If IsHandleCreated Then
            Try
                applied = SetWindowDisplayAffinity(Handle, WDA_EXCLUDEFROMCAPTURE)
            Catch
            End Try
        End If
        Try
            action()
        Finally
            If applied Then
                Try
                    SetWindowDisplayAffinity(Handle, WDA_NONE)
                Catch
                End Try
            End If
        End Try
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            淡入计时器?.Dispose()
            If 跟踪目标 IsNot Nothing Then
                RemoveHandler 跟踪目标.LocationChanged, AddressOf 目标位置变化
                RemoveHandler 跟踪目标.SizeChanged, AddressOf 目标位置变化
            End If
        End If
        MyBase.Dispose(disposing)
    End Sub

End Class

#End Region

#Region "单窗体遮罩对话框"

Friend Class ExOverlayMsgBoxHostForm
    Inherits Form
    Implements V3_IGpuRenderable, V3_IGpuInvalidationSource

#Region "Win32"

    <DllImport("dwmapi.dll")>
    Private Shared Function DwmSetWindowAttribute(hwnd As IntPtr, attr As Integer, ByRef attrValue As Integer, attrSize As Integer) As Integer
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Shared Function LoadString(hInstance As IntPtr, uID As UInteger, lpBuffer As System.Text.StringBuilder, nBufferMax As Integer) As Integer
    End Function

    <DllImport("kernel32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Shared Function GetModuleHandle(lpModuleName As String) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(hWnd As IntPtr, Msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function ReleaseCapture() As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function EnableWindow(hWnd As IntPtr, bEnable As Boolean) As Boolean
    End Function

    Private Const DWMWA_WINDOW_CORNER_PREFERENCE As Integer = 33
    Private Const DWMWCP_DONOTROUND As Integer = 1
    Private Const WM_NCLBUTTONDOWN As Integer = &HA1
    Private Const HT_CAPTION As Integer = &H2

    Private Const RES_OK As UInteger = 800
    Private Const RES_CANCEL As UInteger = 801
    Private Const RES_ABORT As UInteger = 802
    Private Const RES_RETRY As UInteger = 803
    Private Const RES_IGNORE As UInteger = 804
    Private Const RES_YES As UInteger = 805
    Private Const RES_NO As UInteger = 806

    Private Shared 本地化按钮文本 As Dictionary(Of UInteger, String)

    Private Shared Function 获取按钮文本(resId As UInteger, fallback As String) As String
        If 本地化按钮文本 Is Nothing Then
            本地化按钮文本 = New Dictionary(Of UInteger, String)()
            Try
                Dim hUser32 As IntPtr = GetModuleHandle("user32.dll")
                If hUser32 <> IntPtr.Zero Then
                    For Each id As UInteger In {RES_OK, RES_CANCEL, RES_ABORT, RES_RETRY, RES_IGNORE, RES_YES, RES_NO}
                        Dim sb As New System.Text.StringBuilder(256)
                        Dim len As Integer = LoadString(hUser32, id, sb, sb.Capacity)
                        If len > 0 Then 本地化按钮文本(id) = sb.ToString().Replace("&", "")
                    Next
                End If
            Catch
            End Try
        End If
        Dim result As String = Nothing
        If 本地化按钮文本.TryGetValue(resId, result) Then Return result
        Return fallback
    End Function

#End Region

#Region "布局常量"

    Private Const L_卡片内边距 As Integer = 28
    Private Const L_标题下间距 As Integer = 8
    Private Const L_图标尺寸 As Integer = 32
    Private Const L_图标间距 As Integer = 16
    Private Const L_按钮区高度 As Integer = 64
    Private Const L_按钮宽度 As Integer = 100
    Private Const L_按钮高度 As Integer = 32
    Private Const L_按钮间距 As Integer = 8
    Private Const L_卡片最小宽度 As Integer = 400
    Private Const L_卡片最大宽度 As Integer = 560
    Private Const L_最小内容高 As Integer = 40

#End Region

#Region "字段"

    Private 主题 As ExOverlayMsgBoxTheme
    Private 拥有者 As IWin32Window
    Private 拥有者控件 As Control
    Private 毛玻璃 As MessageDialogBackdropController
    Private 淡入计时器 As PrecisionTimer
    Private 目标不透明度 As Double
    Private 返回值字段 As MsgBoxResult = MsgBoxResult.Cancel
    Private 自定义返回索引 As Integer = -1
    Private 正在主动关闭 As Boolean
    Private 是自定义按钮模式 As Boolean
    Private 消息图标位图 As Bitmap
    Private ReadOnly 操作按钮 As New List(Of ModernButton)
    Private 默认按钮序号 As Integer
    Private 允许关闭 As Boolean = True
    Private 关闭标记 As Object = MsgBoxResult.Cancel
    Private 遮罩范围 As Rectangle
    Private 卡片区域 As Rectangle
    Private 按钮区域 As Rectangle
    Private 图标区域 As Rectangle
    Private 标题区域 As Rectangle
    Private 消息区域 As Rectangle
    Private 标题文本 As String
    Private 消息文本 As String
    Private 标题字体 As Font
    Private 消息字体 As Font
    Private SC As Single = 1.0F
    Private 卡片内边距 As Integer
    Private 标题下间距 As Integer
    Private 图标尺寸 As Integer
    Private 图标间距 As Integer
    Private 按钮区高度 As Integer
    Private 按钮宽度 As Integer
    Private 按钮高度 As Integer
    Private 按钮间距 As Integer
    Private 卡片最小宽度 As Integer
    Private 卡片最大宽度 As Integer
    Private 最小内容高 As Integer

#End Region

#Region "属性"

    Public ReadOnly Property Result As MsgBoxResult
        Get
            Return 返回值字段
        End Get
    End Property

    Public ReadOnly Property CustomButtonResult As Integer
        Get
            Return 自定义返回索引
        End Get
    End Property

#End Region

#Region "构造"

    Public Sub New(prompt As String, buttons As Integer, title As String, theme As ExOverlayMsgBoxTheme, owner As IWin32Window)
        初始化通用(prompt, title, theme, owner)
        Dim iconStyle As Integer = buttons And &HF0
        设置图标(iconStyle)
        播放声音(iconStyle)
        构建操作按钮(buttons And &HF, buttons And &HF00)
        配置关闭行为(buttons And &HF)
        完成初始化()
    End Sub

    Public Sub New(prompt As String, customButtonTexts() As String, title As String, theme As ExOverlayMsgBoxTheme, owner As IWin32Window, iconStyle As Integer, defaultButtonIndex As Integer)
        是自定义按钮模式 = True
        初始化通用(prompt, title, theme, owner)
        设置图标(iconStyle)
        播放声音(iconStyle)
        构建自定义按钮(customButtonTexts, defaultButtonIndex)
        允许关闭 = True
        关闭标记 = CObj(-1)
        完成初始化()
    End Sub

    Private Sub 初始化通用(prompt As String, title As String, theme As ExOverlayMsgBoxTheme, owner As IWin32Window)
        主题 = If(theme, New ExOverlayMsgBoxTheme())
        拥有者 = owner
        拥有者控件 = TryCast(owner, Control)
        标题文本 = title
        消息文本 = prompt
        毛玻璃 = New MessageDialogBackdropController(Me)

        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        StartPosition = FormStartPosition.Manual
        KeyPreview = True
        DoubleBuffered = True
        BackColor = 主题.OverlayBackColor
        Text = title
        TopMost = owner Is Nothing
        目标不透明度 = Math.Max(0, Math.Min(255, 主题.OverlayOpacity)) / 255.0
        Opacity = 0

        SC = V3_DpiContext.FromControl(Me).Scale
        缩放常量()
        Dim fontName = MessageDialogRendering.ResolveDialogFontName(owner, Me)
        标题字体 = New Font(fontName, 13.0F, FontStyle.Bold)
        消息字体 = New Font(fontName, 10.0F, FontStyle.Regular)

        If 拥有者控件 IsNot Nothing Then
            AddHandler 拥有者控件.LocationChanged, AddressOf 拥有者位置变化
            AddHandler 拥有者控件.SizeChanged, AddressOf 拥有者位置变化
        End If
    End Sub

    Private Sub 完成初始化()
        更新遮罩范围()
        计算布局()
        If 默认按钮序号 >= 0 AndAlso 默认按钮序号 < 操作按钮.Count Then ActiveControl = 操作按钮(默认按钮序号)
    End Sub

#End Region

#Region "初始化"

    Private Sub 缩放常量()
        卡片内边距 = CInt(L_卡片内边距 * SC)
        标题下间距 = CInt(L_标题下间距 * SC)
        图标尺寸 = CInt(L_图标尺寸 * SC)
        图标间距 = CInt(L_图标间距 * SC)
        按钮区高度 = CInt(L_按钮区高度 * SC)
        按钮宽度 = CInt(L_按钮宽度 * SC)
        按钮高度 = CInt(L_按钮高度 * SC)
        按钮间距 = CInt(L_按钮间距 * SC)
        卡片最小宽度 = CInt(L_卡片最小宽度 * SC)
        卡片最大宽度 = CInt(L_卡片最大宽度 * SC)
        最小内容高 = CInt(L_最小内容高 * SC)
    End Sub

    Private Sub 更新遮罩范围()
        Dim ownerBounds As Rectangle
        If MessageDialogRendering.TryGetWindowBounds(拥有者, ownerBounds) Then
            遮罩范围 = ownerBounds
        Else
            遮罩范围 = SystemInformation.VirtualScreen
        End If
        Bounds = 遮罩范围
    End Sub

    Private Sub 设置图标(iconStyle As Integer)
        消息图标位图?.Dispose()
        消息图标位图 = MessageDialogRendering.CreateMessageIconBitmap(iconStyle, 图标尺寸)
    End Sub

    Private Shared Sub 播放声音(iconStyle As Integer)
        Select Case iconStyle
            Case MsgBoxStyle.Critical : Media.SystemSounds.Hand.Play()
            Case MsgBoxStyle.Question : Media.SystemSounds.Question.Play()
            Case MsgBoxStyle.Exclamation : Media.SystemSounds.Exclamation.Play()
            Case MsgBoxStyle.Information : Media.SystemSounds.Asterisk.Play()
        End Select
    End Sub

    Private Sub 构建操作按钮(buttonStyle As Integer, defaultStyle As Integer)
        Dim defs As New List(Of (Text As String, Result As MsgBoxResult))
        Select Case buttonStyle
            Case 0
                defs.Add((获取按钮文本(RES_OK, "OK"), MsgBoxResult.Ok))
            Case 1
                defs.Add((获取按钮文本(RES_OK, "OK"), MsgBoxResult.Ok))
                defs.Add((获取按钮文本(RES_CANCEL, "Cancel"), MsgBoxResult.Cancel))
            Case 2
                defs.Add((获取按钮文本(RES_ABORT, "Abort"), MsgBoxResult.Abort))
                defs.Add((获取按钮文本(RES_RETRY, "Retry"), MsgBoxResult.Retry))
                defs.Add((获取按钮文本(RES_IGNORE, "Ignore"), MsgBoxResult.Ignore))
            Case 3
                defs.Add((获取按钮文本(RES_YES, "Yes"), MsgBoxResult.Yes))
                defs.Add((获取按钮文本(RES_NO, "No"), MsgBoxResult.No))
                defs.Add((获取按钮文本(RES_CANCEL, "Cancel"), MsgBoxResult.Cancel))
            Case 4
                defs.Add((获取按钮文本(RES_YES, "Yes"), MsgBoxResult.Yes))
                defs.Add((获取按钮文本(RES_NO, "No"), MsgBoxResult.No))
            Case 5
                defs.Add((获取按钮文本(RES_RETRY, "Retry"), MsgBoxResult.Retry))
                defs.Add((获取按钮文本(RES_CANCEL, "Cancel"), MsgBoxResult.Cancel))
            Case Else
                defs.Add((获取按钮文本(RES_OK, "OK"), MsgBoxResult.Ok))
        End Select

        Select Case defaultStyle
            Case MsgBoxStyle.DefaultButton2 : 默认按钮序号 = Math.Min(1, defs.Count - 1)
            Case MsgBoxStyle.DefaultButton3 : 默认按钮序号 = Math.Min(2, defs.Count - 1)
            Case Else : 默认按钮序号 = 0
        End Select

        For i = 0 To defs.Count - 1
            添加按钮(defs(i).Text, defs(i).Result, i, i = 默认按钮序号)
        Next
    End Sub

    Private Sub 配置关闭行为(buttonStyle As Integer)
        Select Case buttonStyle
            Case 2, 4
                允许关闭 = False
            Case 1, 3, 5
                关闭标记 = MsgBoxResult.Cancel
            Case Else
                关闭标记 = MsgBoxResult.Ok
        End Select
    End Sub

    Private Sub 构建自定义按钮(buttonTexts() As String, defaultIndex As Integer)
        If buttonTexts Is Nothing OrElse buttonTexts.Length = 0 Then buttonTexts = {"OK"}
        默认按钮序号 = Math.Max(0, Math.Min(defaultIndex, buttonTexts.Length - 1))
        For i = 0 To buttonTexts.Length - 1
            添加按钮(buttonTexts(i), i, i, i = 默认按钮序号)
        Next
    End Sub

    Private Sub 添加按钮(text As String, tag As Object, index As Integer, isDefault As Boolean)
        Dim buttonFont As New Font(MessageDialogRendering.ResolveDialogFontName(拥有者, Me), 9.0F)
        Dim actualButtonWidth = ExOverlayMsgBoxTextMetrics.MeasureButtonWidth(Me, text, buttonFont, 按钮宽度, SC)
        Dim btn As New ModernButton() With {
            .Text = If(text, ""),
            .Size = New Size(actualButtonWidth, 按钮高度),
            .Font = buttonFont,
            .Tag = tag,
            .BorderRadius = 0,
            .AnimationDuration = 150,
            .TabStop = True,
            .TabIndex = index
        }
        MessageDialogRendering.ApplyButtonStyle(
            btn, Me, isDefault,
            主题.ButtonBackColor, 主题.ButtonForeColor, 主题.ButtonBorderColor,
            主题.ButtonHoverBackColor, 主题.ButtonPressedBackColor,
            主题.AccentButtonBackColor, 主题.AccentButtonForeColor, 主题.AccentButtonBorderColor,
            主题.AccentButtonHoverBackColor, 主题.AccentButtonPressedBackColor)
        AddHandler btn.Click, AddressOf 操作按钮点击
        Controls.Add(btn)
        操作按钮.Add(btn)
    End Sub

    Private Sub 计算布局()
        Dim hasIcon = 消息图标位图 IsNot Nothing
        Dim iconSpan = If(hasIcon, 图标尺寸 + 图标间距, 0)
        Dim maxTextWidth = 卡片最大宽度 - 卡片内边距 * 2 - iconSpan
        Dim titleSize = ExOverlayMsgBoxTextMetrics.MeasureDialogText(Me, 标题文本, 标题字体, New Size(maxTextWidth, Integer.MaxValue), SC)
        Dim messageSize = ExOverlayMsgBoxTextMetrics.MeasureDialogText(Me, 消息文本, 消息字体, New Size(maxTextWidth, Integer.MaxValue), SC)

        Dim buttonWidthSum = 操作按钮.Sum(Function(btn) btn.Width)
        Dim buttonGroupWidth = buttonWidthSum + Math.Max(0, 操作按钮.Count - 1) * 按钮间距 + 卡片内边距 * 2
        Dim contentWidth = Math.Max(titleSize.Width, messageSize.Width + iconSpan) + 卡片内边距 * 2
        Dim availableCardWidth = Math.Max(卡片最小宽度, ClientSize.Width - 卡片内边距 * 2)
        Dim cardWidth = Math.Min(availableCardWidth, Math.Max(卡片最小宽度, Math.Max(buttonGroupWidth, Math.Min(contentWidth, 卡片最大宽度))))
        Dim actualTextWidth = cardWidth - 卡片内边距 * 2 - iconSpan
        messageSize = ExOverlayMsgBoxTextMetrics.MeasureDialogText(Me, 消息文本, 消息字体, New Size(actualTextWidth, Integer.MaxValue), SC)
        Dim messageHeight = Math.Max(最小内容高, Math.Max(messageSize.Height, If(hasIcon, 图标尺寸, 0)))
        Dim cardHeight = 卡片内边距 + titleSize.Height + 标题下间距 + messageHeight + 卡片内边距 + 按钮区高度
        cardHeight = Math.Min(cardHeight, CInt(ClientSize.Height * 0.8))

        卡片区域 = New Rectangle((ClientSize.Width - cardWidth) \ 2, (ClientSize.Height - cardHeight) \ 2, cardWidth, cardHeight)
        标题区域 = New Rectangle(卡片区域.X + 卡片内边距, 卡片区域.Y + 卡片内边距, cardWidth - 卡片内边距 * 2, titleSize.Height)
        Dim messageY = 标题区域.Bottom + 标题下间距
        If hasIcon Then
            图标区域 = New Rectangle(卡片区域.X + 卡片内边距, messageY + (messageHeight - 图标尺寸) \ 2, 图标尺寸, 图标尺寸)
        End If
        消息区域 = New Rectangle(卡片区域.X + 卡片内边距 + iconSpan, messageY + Math.Max(0, (messageHeight - messageSize.Height) \ 2),
                              actualTextWidth, Math.Min(messageSize.Height, messageHeight))
        按钮区域 = New Rectangle(卡片区域.X, 卡片区域.Bottom - 按钮区高度, 卡片区域.Width, 按钮区高度)

        Dim groupWidth = buttonWidthSum + Math.Max(0, 操作按钮.Count - 1) * 按钮间距
        Dim btnX = 卡片区域.Right - 卡片内边距 - groupWidth
        Dim btnY = 按钮区域.Y + (按钮区高度 - 按钮高度) \ 2
        For Each btn In 操作按钮
            btn.Location = New Point(btnX, btnY)
            btn.Size = New Size(btn.Width, 按钮高度)
            btnX += btn.Width + 按钮间距
        Next
    End Sub

#End Region

#Region "绘制"

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Try
            Dim pref As Integer = DWMWCP_DONOTROUND
            Dim unused = DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, pref, 4)
        Catch
        End Try
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        准备卡片毛玻璃()
        淡入计时器 = 创建淡入计时器()
        淡入计时器.Start()
    End Sub

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' V3-only: overlay/card pixels are emitted by RenderGpu.
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
    End Sub

    Private Sub 准备卡片毛玻璃()
        If IsDisposed Then Return
        RequestV3Render(卡片区域)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        If context Is Nothing OrElse ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then Return

        context.FillRectangle(New RectangleF(0, 0, ClientSize.Width, ClientSize.Height), Color.FromArgb(255, 主题.OverlayBackColor))

        Dim cardRect As RectangleF = 卡片区域
        Dim glass = MessageDialogRendering.DrawBackdrop(context, cardRect)
        If Not glass Then
            MessageDialogRendering.FillRectangle(context, cardRect, 主题.CardBackColor)
            MessageDialogRendering.FillRectangle(context, 按钮区域, 主题.ButtonAreaBackColor)
        End If

        MessageDialogRendering.DrawText(context, 标题文本, 标题字体, 标题区域, 主题.TitleForeColor,
            TextFormatFlags.WordBreak Or TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding, SC)
        MessageDialogRendering.DrawText(context, 消息文本, 消息字体, 消息区域, 主题.MessageForeColor,
            TextFormatFlags.WordBreak Or TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding, SC)
        MessageDialogRendering.DrawImage(context, 消息图标位图, 图标区域)
        MessageDialogRendering.DrawRectangle(context, cardRect, 主题.CardBorderColor, 1.0F)
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Private Sub RequestV3Render()
        RequestV3Render(New Rectangle(Point.Empty, Me.Size))
    End Sub

    Private Sub RequestV3Render(dirtyRect As Rectangle)
        If IsDisposed Then Return
        V3_InvalidationRouter.RequestRender(Me, dirtyRect)
    End Sub

    Private Sub 淡入动画帧(sender As Object, e As EventArgs)
        Opacity += 0.06
        If Opacity >= 目标不透明度 Then
            Opacity = 目标不透明度
            淡入计时器?.Stop()
            淡入计时器?.Dispose()
            淡入计时器 = Nothing
        End If
    End Sub

    Private Shared Function FrameIntervalMilliseconds(fps As Integer) As Integer
        fps = Math.Max(1, fps)
        Return Math.Max(1, CInt(Math.Ceiling(1000.0R / fps)))
    End Function

    Private Function 创建淡入计时器() As PrecisionTimer
        Dim timer As New PrecisionTimer() With {
            .Interval = FrameIntervalMilliseconds(60),
            .DispatchMode = PrecisionTimer.DispatchModeEnum.NonBlocking,
            .OverrunPolicy = PrecisionTimer.OverrunPolicyEnum.Drop,
            .WorkerThreadCount = 1,
            .SynchronizingObject = Me
        }
        AddHandler timer.Tick, AddressOf 淡入动画帧
        Return timer
    End Function

#End Region

#Region "交互"

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button = MouseButtons.Left AndAlso Not 卡片区域.Contains(e.Location) Then 开始拖拽Owner()
    End Sub

    Private Sub 开始拖拽Owner()
        If 拥有者控件 Is Nothing OrElse Not TypeOf 拥有者控件 Is Form Then Return
        Dim hWnd = 拥有者控件.Handle
        EnableWindow(hWnd, True)
        ReleaseCapture()
        SendMessage(hWnd, WM_NCLBUTTONDOWN, New IntPtr(HT_CAPTION), IntPtr.Zero)
        EnableWindow(hWnd, False)
        更新遮罩范围()
        计算布局()
        准备卡片毛玻璃()
        RequestV3Render()
        Activate()
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        Select Case e.KeyCode
            Case Keys.Enter
                Dim focusedButton = 操作按钮.FirstOrDefault(Function(b) b.Focused)
                If focusedButton IsNot Nothing Then
                    执行关闭(focusedButton.Tag)
                ElseIf 默认按钮序号 >= 0 AndAlso 默认按钮序号 < 操作按钮.Count Then
                    执行关闭(操作按钮(默认按钮序号).Tag)
                End If
                e.Handled = True
            Case Keys.Escape
                If 允许关闭 Then 执行关闭(关闭标记)
                e.Handled = True
        End Select
    End Sub

    Protected Overrides Function ProcessTabKey(forward As Boolean) As Boolean
        If 操作按钮.Count <= 1 Then Return True
        Dim current = 操作按钮.FindIndex(Function(b) b.Focused)
        If current < 0 Then current = 默认按钮序号
        Dim nextIndex = If(forward, (current + 1) Mod 操作按钮.Count, (current - 1 + 操作按钮.Count) Mod 操作按钮.Count)
        操作按钮(nextIndex).Focus()
        Return True
    End Function

    Private Sub 拥有者位置变化(sender As Object, e As EventArgs)
        If 拥有者控件 Is Nothing Then Return
        If TypeOf 拥有者控件 Is Form AndAlso DirectCast(拥有者控件, Form).WindowState = FormWindowState.Minimized Then
            WindowState = FormWindowState.Minimized
            Return
        End If
        If WindowState = FormWindowState.Minimized Then WindowState = FormWindowState.Normal
        更新遮罩范围()
        计算布局()
        准备卡片毛玻璃()
        RequestV3Render()
    End Sub

#End Region

#Region "关闭"

    Private Sub 存储结果(tag As Object)
        If 是自定义按钮模式 Then
            自定义返回索引 = CInt(tag)
        Else
            返回值字段 = CType(tag, MsgBoxResult)
        End If
    End Sub

    Private Sub 执行关闭(tag As Object)
        存储结果(tag)
        正在主动关闭 = True
        Close()
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If Not 正在主动关闭 Then
            Select Case e.CloseReason
                Case CloseReason.FormOwnerClosing, CloseReason.ApplicationExitCall, CloseReason.WindowsShutDown, CloseReason.TaskManagerClosing
                Case Else
                    If Not 允许关闭 Then
                        e.Cancel = True
                        Return
                    End If
                    存储结果(关闭标记)
            End Select
        End If
        MyBase.OnFormClosing(e)
    End Sub

    Private Sub 操作按钮点击(sender As Object, e As EventArgs)
        执行关闭(DirectCast(sender, ModernButton).Tag)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            If 拥有者控件 IsNot Nothing Then
                RemoveHandler 拥有者控件.LocationChanged, AddressOf 拥有者位置变化
                RemoveHandler 拥有者控件.SizeChanged, AddressOf 拥有者位置变化
            End If
            淡入计时器?.Dispose()
            标题字体?.Dispose()
            消息字体?.Dispose()
            消息图标位图?.Dispose()
            毛玻璃?.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

#End Region

End Class

#End Region

#Region "卡片窗体"

''' <summary>
''' 卡片式对话框窗体，仅包含卡片本身（标题、图标、消息、按钮）。
''' 配合 <see cref="ExOverlayBackdropForm"/> 作为遮罩背景使用。
''' </summary>
Friend Class ExOverlayMsgBoxForm
    Inherits Form
    Implements V3_IGpuRenderable, V3_IGpuInvalidationSource

#Region "Win32"

    <DllImport("dwmapi.dll")>
    Private Shared Function DwmSetWindowAttribute(hwnd As IntPtr, attr As Integer, ByRef attrValue As Integer, attrSize As Integer) As Integer
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Shared Function LoadString(hInstance As IntPtr, uID As UInteger, lpBuffer As System.Text.StringBuilder, nBufferMax As Integer) As Integer
    End Function

    <DllImport("kernel32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Shared Function GetModuleHandle(lpModuleName As String) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(hWnd As IntPtr, Msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function ReleaseCapture() As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function EnableWindow(hWnd As IntPtr, bEnable As Boolean) As Boolean
    End Function

    Private Const DWMWA_WINDOW_CORNER_PREFERENCE As Integer = 33
    Private Const DWMWCP_DONOTROUND As Integer = 1
    Private Const WM_NCLBUTTONDOWN As Integer = &HA1
    Private Const HT_CAPTION As Integer = &H2

    ' user32.dll 按钮文本资源 ID
    Private Const RES_OK As UInteger = 800
    Private Const RES_CANCEL As UInteger = 801
    Private Const RES_ABORT As UInteger = 802
    Private Const RES_RETRY As UInteger = 803
    Private Const RES_IGNORE As UInteger = 804
    Private Const RES_YES As UInteger = 805
    Private Const RES_NO As UInteger = 806

    ' 缓存已加载的本地化按钮文本
    Private Shared 本地化按钮文本 As Dictionary(Of UInteger, String)

    ''' <summary>从 user32.dll 加载系统本地化的按钮文本，去除助记符 (&amp;)，加载失败则回退到英文。</summary>
    Private Shared Function 获取按钮文本(resId As UInteger, fallback As String) As String
        If 本地化按钮文本 Is Nothing Then
            本地化按钮文本 = New Dictionary(Of UInteger, String)()
            Try
                Dim hUser32 As IntPtr = GetModuleHandle("user32.dll")
                If hUser32 <> IntPtr.Zero Then
                    For Each id As UInteger In {RES_OK, RES_CANCEL, RES_ABORT, RES_RETRY, RES_IGNORE, RES_YES, RES_NO}
                        Dim sb As New System.Text.StringBuilder(256)
                        Dim len As Integer = LoadString(hUser32, id, sb, sb.Capacity)
                        If len > 0 Then
                            本地化按钮文本(id) = sb.ToString().Replace("&", "")
                        End If
                    Next
                End If
            Catch
            End Try
        End If
        Dim result As String = Nothing
        If 本地化按钮文本.TryGetValue(resId, result) Then Return result
        Return fallback
    End Function

#End Region

#Region "布局常量"

    Private Const L_卡片内边距 As Integer = 28
    Private Const L_标题下间距 As Integer = 8
    Private Const L_图标尺寸 As Integer = 32
    Private Const L_图标间距 As Integer = 16
    Private Const L_按钮区高度 As Integer = 64
    Private Const L_按钮宽度 As Integer = 100
    Private Const L_按钮高度 As Integer = 32
    Private Const L_按钮间距 As Integer = 8
    Private Const L_卡片最小宽度 As Integer = 400
    Private Const L_卡片最大宽度 As Integer = 560
    Private Const L_最小内容高 As Integer = 40

#End Region

#Region "字段"

    Private 主题 As ExOverlayMsgBoxTheme
    Private 返回值 As MsgBoxResult = MsgBoxResult.Cancel
    Private 正在主动关闭 As Boolean = False

    ' 自定义按钮模式
    Private ReadOnly 是自定义按钮模式 As Boolean = False
    Private 自定义返回索引 As Integer = -1

    ' 跟随遮罩移动
    Private 遮罩窗体 As ExOverlayBackdropForm
    Private 毛玻璃 As MessageDialogBackdropController

    ' 拖拽转发
    Private 拥有者控件 As Control

    ' 图标
    Private 消息图标位图 As Bitmap

    ' 按钮
    Private ReadOnly 操作按钮 As New List(Of ModernButton)
    Private 默认按钮序号 As Integer = 0

    ' 关闭行为
    Private 允许关闭 As Boolean = True
    Private 关闭标记 As Object = MsgBoxResult.Cancel

    ' 居中区域（多显示器时卡片居中于此区域而非遮罩范围）
    Private 居中区域 As Rectangle

    ' DPI 缩放系数
    Private SC As Single = 1.0F

    ' 缩放后的尺寸
    Private 卡片内边距 As Integer
    Private 标题下间距 As Integer
    Private 图标尺寸 As Integer
    Private 图标间距 As Integer
    Private 按钮区高度 As Integer
    Private 按钮宽度 As Integer
    Private 按钮高度 As Integer
    Private 按钮间距 As Integer
    Private 卡片最小宽度 As Integer
    Private 卡片最大宽度 As Integer
    Private 最小内容高 As Integer

    ' 区域（窗体坐标，窗体即卡片）
    Private 按钮区域 As Rectangle
    Private 图标区域 As Rectangle
    Private 标题区域 As Rectangle
    Private 消息区域 As Rectangle

    ' 控件
    Private 标题标签 As Label
    Private 消息标签 As Label

    ' 字体
    Private 标题字体 As Font
    Private 消息字体 As Font

#End Region

#Region "属性"

    Public ReadOnly Property Result As MsgBoxResult
        Get
            Return 返回值
        End Get
    End Property

    ''' <summary>自定义按钮模式下，用户点击的按钮索引（从左往右，0 起始）。未点击或标准模式时为 -1。</summary>
    Public ReadOnly Property CustomButtonResult As Integer
        Get
            Return 自定义返回索引
        End Get
    End Property

#End Region

#Region "构造"

    ''' <summary>标准按钮模式构造。</summary>
    Public Sub New(prompt As String, buttons As Integer, title As String, theme As ExOverlayMsgBoxTheme, overlay As ExOverlayBackdropForm, ownerCtrl As Control)
        初始化通用(prompt, title, theme, overlay, ownerCtrl)
        Dim 图标样式 As Integer = buttons And &HF0
        设置图标(图标样式)
        播放声音(图标样式)
        构建操作按钮(buttons And &HF, buttons And &HF00)
        配置关闭行为(buttons And &HF)
        完成初始化()
    End Sub

    ''' <summary>
    ''' 自定义按钮模式构造。按钮按 <paramref name="customButtonTexts"/> 顺序从左到右排列，返回索引。
    ''' </summary>
    Public Sub New(prompt As String, customButtonTexts() As String, title As String, theme As ExOverlayMsgBoxTheme, overlay As ExOverlayBackdropForm, ownerCtrl As Control, iconStyle As Integer, defaultButtonIndex As Integer)
        是自定义按钮模式 = True
        初始化通用(prompt, title, theme, overlay, ownerCtrl)
        设置图标(iconStyle)
        播放声音(iconStyle)
        构建自定义按钮(customButtonTexts, defaultButtonIndex)
        允许关闭 = True
        关闭标记 = CObj(-1)
        完成初始化()
    End Sub

    Private Sub 初始化通用(prompt As String, title As String, theme As ExOverlayMsgBoxTheme, overlay As ExOverlayBackdropForm, ownerCtrl As Control)
        主题 = If(theme, New ExOverlayMsgBoxTheme())
        遮罩窗体 = overlay
        拥有者控件 = ownerCtrl
        毛玻璃 = New MessageDialogBackdropController(Me)

        If 遮罩窗体 IsNot Nothing Then
            AddHandler 遮罩窗体.BoundsUpdated, AddressOf 遮罩范围变化
        End If

        Me.FormBorderStyle = FormBorderStyle.None
        Me.ShowInTaskbar = False
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.DoubleBuffered = True
        Me.KeyPreview = True
        Me.BackColor = 主题.CardBackColor
        Me.Text = title
        If overlay IsNot Nothing Then Me.TopMost = overlay.TopMost

        ' 居中区域：有 Owner 时跟随遮罩，否则使用主屏幕工作区
        If ownerCtrl IsNot Nothing Then
            居中区域 = overlay.Bounds
        Else
            居中区域 = Screen.PrimaryScreen.WorkingArea
        End If

        SC = V3_DpiContext.FromControl(Me).Scale
        缩放常量()

        Dim fontName = MessageDialogRendering.ResolveDialogFontName(ownerCtrl, Me)
        标题字体 = New Font(fontName, 13.0F, FontStyle.Bold)
        消息字体 = New Font(fontName, 10.0F, FontStyle.Regular)

        构建标题标签(title)
        构建消息标签(prompt)
    End Sub

    Private Sub 完成初始化()
        计算布局()
        If 默认按钮序号 >= 0 AndAlso 默认按钮序号 < 操作按钮.Count Then
            Me.ActiveControl = 操作按钮(默认按钮序号)
        End If
    End Sub

#End Region

#Region "DWM 初始化"

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Try
            Dim pref As Integer = DWMWCP_DONOTROUND
            Dim unused = DwmSetWindowAttribute(Me.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, pref, 4)
        Catch
        End Try
    End Sub

#End Region

#Region "界面构建"

    Private Sub 缩放常量()
        卡片内边距 = CInt(L_卡片内边距 * SC)
        标题下间距 = CInt(L_标题下间距 * SC)
        图标尺寸 = CInt(L_图标尺寸 * SC)
        图标间距 = CInt(L_图标间距 * SC)
        按钮区高度 = CInt(L_按钮区高度 * SC)
        按钮宽度 = CInt(L_按钮宽度 * SC)
        按钮高度 = CInt(L_按钮高度 * SC)
        按钮间距 = CInt(L_按钮间距 * SC)
        卡片最小宽度 = CInt(L_卡片最小宽度 * SC)
        卡片最大宽度 = CInt(L_卡片最大宽度 * SC)
        最小内容高 = CInt(L_最小内容高 * SC)
    End Sub

    Private Sub 设置图标(图标样式 As Integer)
        消息图标位图?.Dispose()
        消息图标位图 = MessageDialogRendering.CreateMessageIconBitmap(图标样式, 图标尺寸)
    End Sub

    Private Shared Sub 播放声音(图标样式 As Integer)
        Select Case 图标样式
            Case MsgBoxStyle.Critical : Media.SystemSounds.Hand.Play()
            Case MsgBoxStyle.Question : Media.SystemSounds.Question.Play()
            Case MsgBoxStyle.Exclamation : Media.SystemSounds.Exclamation.Play()
            Case MsgBoxStyle.Information : Media.SystemSounds.Asterisk.Play()
        End Select
    End Sub

    Private Sub 构建标题标签(title As String)
        标题标签 = New Label() With {
            .AutoSize = False,
            .Text = title,
            .ForeColor = 主题.TitleForeColor,
            .BackColor = Color.Transparent,
            .Font = 标题字体,
            .UseMnemonic = False,
            .Visible = False
        }
        Me.Controls.Add(标题标签)
    End Sub

    Private Sub 构建消息标签(prompt As String)
        消息标签 = New Label() With {
            .AutoSize = False,
            .Text = prompt,
            .ForeColor = 主题.MessageForeColor,
            .BackColor = Color.Transparent,
            .Font = 消息字体,
            .UseMnemonic = False,
            .Visible = False
        }
        Me.Controls.Add(消息标签)
    End Sub

    Private Sub 构建操作按钮(按钮样式 As Integer, 默认样式 As Integer)
        Dim 定义 As New List(Of (文本 As String, 返回值 As MsgBoxResult))

        Dim sOK As String = 获取按钮文本(RES_OK, "OK")
        Dim sCancel As String = 获取按钮文本(RES_CANCEL, "Cancel")
        Dim sAbort As String = 获取按钮文本(RES_ABORT, "Abort")
        Dim sRetry As String = 获取按钮文本(RES_RETRY, "Retry")
        Dim sIgnore As String = 获取按钮文本(RES_IGNORE, "Ignore")
        Dim sYes As String = 获取按钮文本(RES_YES, "Yes")
        Dim sNo As String = 获取按钮文本(RES_NO, "No")

        Select Case 按钮样式
            Case 0 ' OkOnly
                定义.Add((sOK, MsgBoxResult.Ok))
            Case 1 ' OkCancel
                定义.Add((sOK, MsgBoxResult.Ok))
                定义.Add((sCancel, MsgBoxResult.Cancel))
            Case 2 ' AbortRetryIgnore
                定义.Add((sAbort, MsgBoxResult.Abort))
                定义.Add((sRetry, MsgBoxResult.Retry))
                定义.Add((sIgnore, MsgBoxResult.Ignore))
            Case 3 ' YesNoCancel
                定义.Add((sYes, MsgBoxResult.Yes))
                定义.Add((sNo, MsgBoxResult.No))
                定义.Add((sCancel, MsgBoxResult.Cancel))
            Case 4 ' YesNo
                定义.Add((sYes, MsgBoxResult.Yes))
                定义.Add((sNo, MsgBoxResult.No))
            Case 5 ' RetryCancel
                定义.Add((sRetry, MsgBoxResult.Retry))
                定义.Add((sCancel, MsgBoxResult.Cancel))
            Case Else
                定义.Add((sOK, MsgBoxResult.Ok))
        End Select

        Select Case 默认样式
            Case MsgBoxStyle.DefaultButton2
                默认按钮序号 = Math.Min(1, 定义.Count - 1)
            Case MsgBoxStyle.DefaultButton3
                默认按钮序号 = Math.Min(2, 定义.Count - 1)
            Case Else
                默认按钮序号 = 0
        End Select

        For i = 0 To 定义.Count - 1
            Dim d = 定义(i)
            添加按钮(d.文本, d.返回值, i, i = 默认按钮序号)
        Next
    End Sub

    Private Sub 配置关闭行为(按钮样式 As Integer)
        Select Case 按钮样式
            Case 2, 4 ' AbortRetryIgnore, YesNo
                允许关闭 = False
            Case 1, 3, 5 ' OkCancel, YesNoCancel, RetryCancel
                关闭标记 = MsgBoxResult.Cancel
            Case Else ' OkOnly
                关闭标记 = MsgBoxResult.Ok
        End Select
    End Sub

    Private Sub 构建自定义按钮(buttonTexts() As String, defaultIndex As Integer)
        If buttonTexts Is Nothing OrElse buttonTexts.Length = 0 Then
            buttonTexts = {"OK"}
        End If
        默认按钮序号 = Math.Max(0, Math.Min(defaultIndex, buttonTexts.Length - 1))
        For i = 0 To buttonTexts.Length - 1
            添加按钮(buttonTexts(i), i, i, i = 默认按钮序号)
        Next
    End Sub

    Private Sub 添加按钮(text As String, tag As Object, index As Integer, isDefault As Boolean)
        Dim 按钮字体 As New Font(MessageDialogRendering.ResolveDialogFontName(拥有者控件, Me), 9.0F)
        Dim 实际按钮宽度 = ExOverlayMsgBoxTextMetrics.MeasureButtonWidth(Me, text, 按钮字体, 按钮宽度, SC)
        Dim btn As New ModernButton() With {
            .Text = text,
            .Size = New Size(实际按钮宽度, 按钮高度),
            .Font = 按钮字体,
            .Tag = tag,
            .BorderRadius = 0,
            .BorderSize = 1,
            .AnimationDuration = 150,
            .TabStop = True,
            .TabIndex = index
        }
        MessageDialogRendering.ApplyButtonStyle(
            btn, Me, isDefault,
            主题.ButtonBackColor, 主题.ButtonForeColor, 主题.ButtonBorderColor,
            主题.ButtonHoverBackColor, 主题.ButtonPressedBackColor,
            主题.AccentButtonBackColor, 主题.AccentButtonForeColor, 主题.AccentButtonBorderColor,
            主题.AccentButtonHoverBackColor, 主题.AccentButtonPressedBackColor)
        AddHandler btn.Click, AddressOf 操作按钮点击
        Me.Controls.Add(btn)
        操作按钮.Add(btn)
    End Sub

    Private Sub 计算布局()
        Dim 有图标 As Boolean = 消息图标位图 IsNot Nothing
        Dim 图标占宽 As Integer = If(有图标, 图标尺寸 + 图标间距, 0)
        Dim 最大文本宽 As Integer = 卡片最大宽度 - 卡片内边距 * 2 - 图标占宽

        ' 测量标题
        Dim 标题尺寸 = ExOverlayMsgBoxTextMetrics.MeasureDialogText(
            Me, 标题标签.Text, 标题字体,
            New Size(最大文本宽, Integer.MaxValue), SC)

        ' 测量消息
        Dim 文本尺寸 = ExOverlayMsgBoxTextMetrics.MeasureDialogText(
            Me, 消息标签.Text, 消息字体,
            New Size(最大文本宽, Integer.MaxValue), SC)

        ' 计算卡片宽度
        Dim 按钮宽合计 As Integer = 操作按钮.Sum(Function(btn) btn.Width)
        Dim 按钮组总宽 As Integer = 按钮宽合计 + Math.Max(0, 操作按钮.Count - 1) * 按钮间距 + 卡片内边距 * 2
        Dim 内容需要宽 As Integer = Math.Max(标题尺寸.Width, 文本尺寸.Width + 图标占宽) + 卡片内边距 * 2
        Dim 卡片宽度 As Integer = Math.Max(卡片最小宽度, Math.Max(按钮组总宽, 内容需要宽))
        卡片宽度 = Math.Min(卡片宽度, Math.Max(卡片最小宽度, 居中区域.Width - 卡片内边距 * 2))

        ' 用实际宽度重新测量
        Dim 实际文本宽 As Integer = 卡片宽度 - 卡片内边距 * 2 - 图标占宽
        文本尺寸 = ExOverlayMsgBoxTextMetrics.MeasureDialogText(
            Me, 消息标签.Text, 消息字体,
            New Size(实际文本宽, Integer.MaxValue), SC)

        ' 内容高度
        Dim 消息高度 As Integer = Math.Max(最小内容高, Math.Max(文本尺寸.Height, If(有图标, 图标尺寸, 0)))

        ' 卡片总高度 = 内边距 + 标题 + 标题下间距 + 消息区 + 内边距 + 按钮区
        Dim 卡片高度 As Integer = 卡片内边距 + 标题尺寸.Height + 标题下间距 + 消息高度 + 卡片内边距 + 按钮区高度

        ' 限制最大高度
        Dim 最大高度 As Integer = CInt(居中区域.Height * 0.8)
        卡片高度 = Math.Min(卡片高度, 最大高度)

        ' 窗体大小 = 卡片大小
        Me.ClientSize = New Size(卡片宽度, 卡片高度)

        ' 窗体居中到遮罩范围
        Me.StartPosition = FormStartPosition.Manual
        Me.Location = New Point(
            居中区域.Left + (居中区域.Width - 卡片宽度) \ 2,
            居中区域.Top + (居中区域.Height - 卡片高度) \ 2)

        ' 按钮区域（窗体坐标）
        按钮区域 = New Rectangle(0, 卡片高度 - 按钮区高度, 卡片宽度, 按钮区高度)

        ' 图标区域
        If 有图标 Then
            Dim 消息区Y As Integer = 卡片内边距 + 标题尺寸.Height + 标题下间距
            图标区域 = New Rectangle(
                卡片内边距,
                消息区Y + (消息高度 - 图标尺寸) \ 2,
                图标尺寸, 图标尺寸)
        End If

        ' 标题标签位置
        标题标签.Location = New Point(卡片内边距, 卡片内边距)
        标题标签.Size = New Size(卡片宽度 - 卡片内边距 * 2, 标题尺寸.Height)
        标题区域 = 标题标签.Bounds

        ' 消息标签位置
        Dim 消息X As Integer = 卡片内边距 + 图标占宽
        Dim 消息Y As Integer = 卡片内边距 + 标题尺寸.Height + 标题下间距
        Dim 可用消息高 As Integer = 卡片高度 - 卡片内边距 - 标题尺寸.Height - 标题下间距 - 卡片内边距 - 按钮区高度
        Dim 文本Y偏移 As Integer = If(有图标, Math.Max(0, (可用消息高 - 文本尺寸.Height) \ 2), 0)
        消息标签.Location = New Point(消息X, 消息Y + 文本Y偏移)
        消息标签.Size = New Size(实际文本宽, Math.Min(文本尺寸.Height, 可用消息高))
        消息区域 = 消息标签.Bounds

        ' 按钮位置（在按钮区内右对齐）
        Dim 按钮组宽度 As Integer = 按钮宽合计 + Math.Max(0, 操作按钮.Count - 1) * 按钮间距
        Dim btnX As Integer = 卡片宽度 - 卡片内边距 - 按钮组宽度
        Dim btnY As Integer = 按钮区域.Y + (按钮区高度 - 按钮高度) \ 2
        For Each btn In 操作按钮
            btn.Location = New Point(btnX, btnY)
            btn.Size = New Size(btn.Width, 按钮高度)
            btnX += btn.Width + 按钮间距
        Next
    End Sub

#End Region

#Region "绘制"

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' V3-only: card pixels are emitted by RenderGpu.
    End Sub

    Public Sub 准备毛玻璃()
        RequestV3Render()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        If context Is Nothing OrElse ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then Return

        Dim cardRect As New RectangleF(0, 0, ClientSize.Width, ClientSize.Height)
        Dim glass = MessageDialogRendering.DrawBackdrop(context, cardRect)
        If Not glass Then
            MessageDialogRendering.FillRectangle(context, cardRect, 主题.CardBackColor)
            MessageDialogRendering.FillRectangle(context, 按钮区域, 主题.ButtonAreaBackColor)
        End If

        MessageDialogRendering.DrawText(context, 标题标签.Text, 标题字体, 标题区域, 主题.TitleForeColor,
            TextFormatFlags.WordBreak Or TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding, SC)
        MessageDialogRendering.DrawText(context, 消息标签.Text, 消息字体, 消息区域, 主题.MessageForeColor,
            TextFormatFlags.WordBreak Or TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding, SC)
        MessageDialogRendering.DrawImage(context, 消息图标位图, 图标区域)
        MessageDialogRendering.DrawRectangle(context,
            New RectangleF(0.5F, 0.5F, Math.Max(0, ClientSize.Width - 1), Math.Max(0, ClientSize.Height - 1)),
            主题.CardBorderColor, 1.0F)
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Private Sub RequestV3Render()
        RequestV3Render(New Rectangle(Point.Empty, Me.Size))
    End Sub

    Private Sub RequestV3Render(dirtyRect As Rectangle)
        If IsDisposed Then Return
        V3_InvalidationRouter.RequestRender(Me, dirtyRect)
    End Sub

#End Region

#Region "鼠标"

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
    End Sub

    ''' <summary>
    ''' 临时启用 Owner 窗口并发送 WM_NCLBUTTONDOWN，
    ''' 进入系统原生拖拽循环（支持屏幕边缘吸附等特性）。
    ''' </summary>
    Private Sub 开始拖拽Owner()
        If 拥有者控件 Is Nothing OrElse Not (TypeOf 拥有者控件 Is Form) Then Return
        Dim hWnd = 拥有者控件.Handle
        EnableWindow(hWnd, True)
        ReleaseCapture()
        SendMessage(hWnd, WM_NCLBUTTONDOWN, New IntPtr(HT_CAPTION), IntPtr.Zero)
        ' SendMessage 在拖拽结束后才返回
        EnableWindow(hWnd, False)
        准备毛玻璃()
        RequestV3Render()
        Me.Activate()
    End Sub

#End Region

#Region "键盘"

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        Select Case e.KeyCode
            Case Keys.Enter
                Dim 焦点按钮 = 操作按钮.FirstOrDefault(Function(b) b.Focused)
                If 焦点按钮 IsNot Nothing Then
                    执行关闭(焦点按钮.Tag)
                ElseIf 默认按钮序号 >= 0 AndAlso 默认按钮序号 < 操作按钮.Count Then
                    执行关闭(操作按钮(默认按钮序号).Tag)
                End If
                e.Handled = True
            Case Keys.Escape
                If 允许关闭 Then 执行关闭(关闭标记)
                e.Handled = True
        End Select
    End Sub

    Protected Overrides Function ProcessTabKey(forward As Boolean) As Boolean
        If 操作按钮.Count <= 1 Then Return True
        Dim 当前序号 As Integer = 操作按钮.FindIndex(Function(b) b.Focused)
        If 当前序号 < 0 Then 当前序号 = 默认按钮序号
        Dim 下一序号 As Integer
        If forward Then
            下一序号 = (当前序号 + 1) Mod 操作按钮.Count
        Else
            下一序号 = (当前序号 - 1 + 操作按钮.Count) Mod 操作按钮.Count
        End If
        操作按钮(下一序号).Focus()
        Return True
    End Function

#End Region

#Region "关闭"

    Private Sub 存储结果(tag As Object)
        If 是自定义按钮模式 Then
            自定义返回索引 = CInt(tag)
        Else
            返回值 = CType(tag, MsgBoxResult)
        End If
    End Sub

    Private Sub 执行关闭(tag As Object)
        存储结果(tag)
        正在主动关闭 = True
        Me.Close()
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If Not 正在主动关闭 Then
            Select Case e.CloseReason
                Case CloseReason.FormOwnerClosing, CloseReason.ApplicationExitCall, CloseReason.WindowsShutDown, CloseReason.TaskManagerClosing
                    ' 外部强制关闭，允许通过
                Case Else
                    If Not 允许关闭 Then
                        e.Cancel = True
                        Return
                    Else
                        存储结果(关闭标记)
                    End If
            End Select
        End If
        MyBase.OnFormClosing(e)
    End Sub

    Private Sub 操作按钮点击(sender As Object, e As EventArgs)
        执行关闭(DirectCast(sender, ModernButton).Tag)
    End Sub

    ''' <summary>遮罩窗体范围变化时，卡片重新居中。</summary>
    Private Sub 遮罩范围变化(sender As Object, e As EventArgs)
        If 遮罩窗体 Is Nothing Then Return
        居中区域 = 遮罩窗体.Bounds
        Me.Location = New Point(
            居中区域.Left + (居中区域.Width - Me.Width) \ 2,
            居中区域.Top + (居中区域.Height - Me.Height) \ 2)
        准备毛玻璃()
        RequestV3Render()
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            If 遮罩窗体 IsNot Nothing Then
                RemoveHandler 遮罩窗体.BoundsUpdated, AddressOf 遮罩范围变化
            End If
            标题字体?.Dispose()
            消息字体?.Dispose()
            消息图标位图?.Dispose()
            毛玻璃?.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

#End Region

End Class

#End Region
