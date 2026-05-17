Imports System.Drawing.Drawing2D
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
        If 有拥有者 Then overlay.Show(owner) Else overlay.Show()
    End Sub

    Private Function 显示(owner As IWin32Window, prompt As Object, buttons As Integer, title As Object) As MsgBoxResult
        Dim ownerCtrl As Control = Nothing
        Dim overlay As ExOverlayBackdropForm = Nothing
        创建遮罩(owner, ownerCtrl, overlay)

        Using card As New ExOverlayMsgBoxForm(
                If(prompt?.ToString(), String.Empty), buttons,
                If(title?.ToString(), Application.ProductName),
                ExOverlayMsgBoxTheme.Current, overlay, ownerCtrl)
            card.ShowDialog(overlay)
            overlay.Close()
            overlay.Dispose()
            Return card.Result
        End Using
    End Function

    Private Function 显示自定义(owner As IWin32Window, prompt As Object, buttonTexts() As String, title As Object, icon As MsgBoxStyle, defaultButton As Integer) As Integer
        Dim ownerCtrl As Control = Nothing
        Dim overlay As ExOverlayBackdropForm = Nothing
        创建遮罩(owner, ownerCtrl, overlay)

        Using card As New ExOverlayMsgBoxForm(
                If(prompt?.ToString(), String.Empty), buttonTexts,
                If(title?.ToString(), Application.ProductName),
                ExOverlayMsgBoxTheme.Current, overlay, ownerCtrl,
                CInt(icon) And &HF0, defaultButton)
            card.ShowDialog(overlay)
            overlay.Close()
            overlay.Dispose()
            Return card.CustomButtonResult
        End Using
    End Function

End Module

#End Region

#Region "遮罩窗体"

''' <summary>
''' 半透明遮罩背景窗体，通过 <see cref="Form.Opacity"/> 实现真半透明效果。
''' </summary>
Friend Class ExOverlayBackdropForm
    Inherits Form

    <DllImport("dwmapi.dll")>
    Private Shared Function DwmSetWindowAttribute(hwnd As IntPtr, attr As Integer, ByRef attrValue As Integer, attrSize As Integer) As Integer
    End Function

    Private Const DWMWA_WINDOW_CORNER_PREFERENCE As Integer = 33
    Private Const DWMWCP_DONOTROUND As Integer = 1

    Private 淡入计时器 As Timer
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
        淡入计时器 = New Timer() With {.Interval = 10}
        AddHandler 淡入计时器.Tick, AddressOf 淡入动画帧
        淡入计时器.Start()
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

#Region "卡片窗体"

''' <summary>
''' 卡片式对话框窗体，仅包含卡片本身（标题、图标、消息、按钮）。
''' 配合 <see cref="ExOverlayBackdropForm"/> 作为遮罩背景使用。
''' </summary>
Friend Class ExOverlayMsgBoxForm
    Inherits Form

#Region "Win32"

    <DllImport("dwmapi.dll")>
    Private Shared Function DwmExtendFrameIntoClientArea(hwnd As IntPtr, ByRef margins As MARGINS) As Integer
    End Function

    <DllImport("dwmapi.dll")>
    Private Shared Function DwmSetWindowAttribute(hwnd As IntPtr, attr As Integer, ByRef attrValue As Integer, attrSize As Integer) As Integer
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Private Structure MARGINS
        Public left As Integer
        Public right As Integer
        Public top As Integer
        Public bottom As Integer
    End Structure

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
    Private Const DWMWCP_ROUND As Integer = 2
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

    ' 拖拽转发
    Private 拥有者控件 As Control
    Private 标题拖拽区域 As Rectangle

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

        SC = Me.DeviceDpi / 96.0F
        缩放常量()

        标题字体 = New Font("Microsoft YaHei UI", 13.0F, FontStyle.Bold)
        消息字体 = New Font("Microsoft YaHei UI", 10.0F, FontStyle.Regular)

        构建标题标签(title)
        AddHandler 标题标签.MouseDown, Sub(s, ev)
                                       If ev.Button = MouseButtons.Left Then 开始拖拽Owner()
                                   End Sub
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
            ' DWM 1px 边框，让系统为无边框窗体绘制圆角
            Dim m As New MARGINS With {.left = 1, .right = 1, .top = 1, .bottom = 1}
            Dim unused0 = DwmExtendFrameIntoClientArea(Me.Handle, m)
            ' Win11 圆角
            Dim pref As Integer = DWMWCP_ROUND
            Dim unused1 = DwmSetWindowAttribute(Me.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, pref, 4)
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
        Dim 消息图标 As Icon
        Select Case 图标样式
            Case MsgBoxStyle.Critical : 消息图标 = SystemIcons.Error
            Case MsgBoxStyle.Question : 消息图标 = SystemIcons.Question
            Case MsgBoxStyle.Exclamation : 消息图标 = SystemIcons.Warning
            Case MsgBoxStyle.Information : 消息图标 = SystemIcons.Information
            Case Else : 消息图标 = Nothing
        End Select
        If 消息图标 IsNot Nothing Then
            Using sized As New Icon(消息图标, 图标尺寸, 图标尺寸)
                消息图标位图 = sized.ToBitmap()
            End Using
        End If
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
            .UseMnemonic = False
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
            .UseMnemonic = False
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
        Dim btn As New ModernButton() With {
            .Text = text,
            .Size = New Size(按钮宽度, 按钮高度),
            .Font = New Font("Microsoft YaHei UI", 9.0F),
            .Tag = tag,
            .BorderRadius = 主题.ButtonBorderRadius,
            .BorderSize = 1,
            .AnimationDuration = 150,
            .TabStop = True,
            .TabIndex = index
        }
        DirectCast(btn, Control).BackColor = 主题.ButtonAreaBackColor
        If isDefault Then
            btn.BackColor1 = 主题.AccentButtonBackColor
            btn.ForeColor = 主题.AccentButtonForeColor
            btn.BorderColor = 主题.AccentButtonBorderColor
            btn.HoverBackColor1 = 主题.AccentButtonHoverBackColor
            btn.PressedBackColor1 = 主题.AccentButtonPressedBackColor
        Else
            btn.BackColor1 = 主题.ButtonBackColor
            btn.ForeColor = 主题.ButtonForeColor
            btn.BorderColor = 主题.ButtonBorderColor
            btn.HoverBackColor1 = 主题.ButtonHoverBackColor
            btn.PressedBackColor1 = 主题.ButtonPressedBackColor
        End If
        AddHandler btn.Click, AddressOf 操作按钮点击
        Me.Controls.Add(btn)
        操作按钮.Add(btn)
    End Sub

    Private Sub 计算布局()
        Dim 有图标 As Boolean = 消息图标位图 IsNot Nothing
        Dim 图标占宽 As Integer = If(有图标, 图标尺寸 + 图标间距, 0)
        Dim 最大文本宽 As Integer = 卡片最大宽度 - 卡片内边距 * 2 - 图标占宽

        ' 测量标题
        Dim 标题尺寸 = TextRenderer.MeasureText(
            标题标签.Text, 标题字体,
            New Size(最大文本宽, Integer.MaxValue),
            TextFormatFlags.WordBreak Or TextFormatFlags.TextBoxControl)

        ' 测量消息
        Dim 文本尺寸 = TextRenderer.MeasureText(
            消息标签.Text, 消息字体,
            New Size(最大文本宽, Integer.MaxValue),
            TextFormatFlags.WordBreak Or TextFormatFlags.TextBoxControl)

        ' 计算卡片宽度
        Dim 按钮组总宽 As Integer = 操作按钮.Count * 按钮宽度 + (操作按钮.Count - 1) * 按钮间距 + 卡片内边距 * 2
        Dim 内容需要宽 As Integer = Math.Max(标题尺寸.Width, 文本尺寸.Width + 图标占宽) + 卡片内边距 * 2
        Dim 卡片宽度 As Integer = Math.Max(卡片最小宽度, Math.Max(按钮组总宽, 内容需要宽))
        卡片宽度 = Math.Min(卡片宽度, 卡片最大宽度)

        ' 用实际宽度重新测量
        Dim 实际文本宽 As Integer = 卡片宽度 - 卡片内边距 * 2 - 图标占宽
        文本尺寸 = TextRenderer.MeasureText(
            消息标签.Text, 消息字体,
            New Size(实际文本宽, Integer.MaxValue),
            TextFormatFlags.WordBreak Or TextFormatFlags.TextBoxControl)

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

        ' 标题拖拽区域（标题标签上方的内边距 + 标题标签 + 标题下间距）
        标题拖拽区域 = New Rectangle(0, 0, 卡片宽度, 卡片内边距 + 标题尺寸.Height + 标题下间距)

        ' 消息标签位置
        Dim 消息X As Integer = 卡片内边距 + 图标占宽
        Dim 消息Y As Integer = 卡片内边距 + 标题尺寸.Height + 标题下间距
        Dim 可用消息高 As Integer = 卡片高度 - 卡片内边距 - 标题尺寸.Height - 标题下间距 - 卡片内边距 - 按钮区高度
        Dim 文本Y偏移 As Integer = If(有图标, Math.Max(0, (可用消息高 - 文本尺寸.Height) \ 2), 0)
        消息标签.Location = New Point(消息X, 消息Y + 文本Y偏移)
        消息标签.Size = New Size(实际文本宽, Math.Min(文本尺寸.Height, 可用消息高))

        ' 按钮位置（在按钮区内右对齐）
        Dim 按钮组宽度 As Integer = 操作按钮.Count * 按钮宽度 + (操作按钮.Count - 1) * 按钮间距
        Dim btnX As Integer = 卡片宽度 - 卡片内边距 - 按钮组宽度
        Dim btnY As Integer = 按钮区域.Y + (按钮区高度 - 按钮高度) \ 2
        For Each btn In 操作按钮
            btn.Location = New Point(btnX, btnY)
            btnX += 按钮宽度 + 按钮间距
        Next
    End Sub

#End Region

#Region "绘制"

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit

        ' 卡片背景（由 BackColor 自动绘制）

        ' 按钮区域背景
        Using brush As New SolidBrush(主题.ButtonAreaBackColor)
            g.FillRectangle(brush, 按钮区域)
        End Using

        ' 图标
        If 消息图标位图 IsNot Nothing Then
            g.InterpolationMode = InterpolationMode.HighQualityBicubic
            g.DrawImage(消息图标位图, 图标区域)
        End If

        ' 卡片边框
        Using pen As New Pen(主题.CardBorderColor, 1)
            g.DrawRectangle(pen, 0, 0, Me.Width - 1, Me.Height - 1)
        End Using
    End Sub

#End Region

#Region "鼠标"

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button = MouseButtons.Left AndAlso 标题拖拽区域.Contains(e.Location) Then
            开始拖拽Owner()
        End If
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
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            If 遮罩窗体 IsNot Nothing Then
                RemoveHandler 遮罩窗体.BoundsUpdated, AddressOf 遮罩范围变化
            End If
            标题字体?.Dispose()
            消息字体?.Dispose()
            消息图标位图?.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

#End Region

End Class

#End Region
