Imports System.Runtime.InteropServices

' ═══════════════════════════════════════════════════════════════════════════
'  ExMsgBox — LakeUI 自定义消息框
'
'  ● 用法与原版 MsgBox 完全一致：
'      ExMsgBox("Hello World")
'      ExMsgBox("确认删除？", MsgBoxStyle.YesNo Or MsgBoxStyle.Question, "提示")
'
'  ● 全局主题设置：
'      ExMsgBoxTheme.Current = ExMsgBoxTheme.CreateDark()   ' 暗色（默认）
'      ExMsgBoxTheme.Current = ExMsgBoxTheme.CreateLight()  ' 亮色
'      ExMsgBoxTheme.Current.MessageForeColor = Color.Red   ' 逐项定制
'
'  ● 指定父窗口居中：
'      ExMsgBox(Me, "消息", MsgBoxStyle.OkCancel)
' ═══════════════════════════════════════════════════════════════════════════

#Region "主题"

''' <summary>
''' ExMsgBox 全局主题配置。
''' 通过 <see cref="Current"/> 设置当前主题，使用 <see cref="CreateDark"/>/<see cref="CreateLight"/> 加载预设，或逐项定制任意颜色。
''' </summary>
Public Class ExMsgBoxTheme

    ''' <summary>当前全局主题（默认暗色）。</summary>
    Public Shared Property Current As New ExMsgBoxTheme()

    ' ── 窗体 ──
    ''' <summary>窗体背景色。</summary>
    Public Property FormBackColor As Color = Color.FromArgb(32, 32, 32)
    ''' <summary>窗体边框颜色。</summary>
    Public Property FormBorderColor As Color = Color.FromArgb(70, 70, 70)

    ' ── 标题栏 ──
    ''' <summary>标题栏背景色。</summary>
    Public Property TitleBarBackColor As Color = Color.FromArgb(32, 32, 32)
    ''' <summary>标题文字颜色。</summary>
    Public Property TitleForeColor As Color = Color.FromArgb(230, 230, 230)

    ' ── 关闭按钮 ──
    ''' <summary>关闭按钮前景色 (×)。</summary>
    Public Property CloseButtonForeColor As Color = Color.FromArgb(160, 160, 160)
    ''' <summary>关闭按钮悬停时背景色。</summary>
    Public Property CloseButtonHoverBackColor As Color = Color.FromArgb(196, 43, 28)
    ''' <summary>关闭按钮悬停时前景色。</summary>
    Public Property CloseButtonHoverForeColor As Color = Color.White

    ' ── 内容区 ──
    ''' <summary>内容区域背景色。</summary>
    Public Property ContentBackColor As Color = Color.FromArgb(32, 32, 32)
    ''' <summary>消息文本颜色。</summary>
    Public Property MessageForeColor As Color = Color.FromArgb(230, 230, 230)

    ' ── 按钮区 ──
    ''' <summary>按钮区域背景色。</summary>
    Public Property ButtonAreaBackColor As Color = Color.FromArgb(40, 40, 40)
    ''' <summary>普通按钮背景色。</summary>
    Public Property ButtonBackColor As Color = Color.FromArgb(51, 51, 51)
    ''' <summary>普通按钮文字颜色。</summary>
    Public Property ButtonForeColor As Color = Color.FromArgb(220, 220, 220)
    ''' <summary>普通按钮边框颜色。</summary>
    Public Property ButtonBorderColor As Color = Color.FromArgb(76, 76, 76)
    ''' <summary>普通按钮悬停时背景色。</summary>
    Public Property ButtonHoverBackColor As Color = Color.FromArgb(65, 65, 65)
    ''' <summary>普通按钮按下时背景色。</summary>
    Public Property ButtonPressedBackColor As Color = Color.FromArgb(40, 40, 40)
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
    Public Shared Function CreateDark() As ExMsgBoxTheme
        Return New ExMsgBoxTheme()
    End Function

    ''' <summary>创建亮色主题预设。</summary>
    Public Shared Function CreateLight() As ExMsgBoxTheme
        Return New ExMsgBoxTheme With {
            .FormBackColor = Color.FromArgb(243, 243, 243),
            .FormBorderColor = Color.FromArgb(200, 200, 200),
            .TitleBarBackColor = Color.FromArgb(243, 243, 243),
            .TitleForeColor = Color.FromArgb(30, 30, 30),
            .CloseButtonForeColor = Color.FromArgb(100, 100, 100),
            .CloseButtonHoverBackColor = Color.FromArgb(196, 43, 28),
            .CloseButtonHoverForeColor = Color.White,
            .ContentBackColor = Color.FromArgb(243, 243, 243),
            .MessageForeColor = Color.FromArgb(30, 30, 30),
            .ButtonAreaBackColor = Color.FromArgb(230, 230, 230),
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
''' 提供全局可调用的 ExMsgBox() 函数，用法与原版 MsgBox 完全一致。
''' </summary>
Public Module ExMsgBoxModule

    ''' <summary>
    ''' 显示自定义消息框。
    ''' </summary>
    ''' <param name="Prompt">消息内容。</param>
    ''' <param name="Buttons">按钮组合与图标样式（MsgBoxStyle 位标志组合）。</param>
    ''' <param name="Title">标题栏文字；为 Nothing 时使用应用名称。</param>
    ''' <returns>用户点击的按钮对应的 MsgBoxResult。</returns>
    Public Function ExMsgBox(
        Prompt As Object,
        Optional Buttons As Integer = 0,
        Optional Title As Object = Nothing
    ) As MsgBoxResult
        Return 显示(Nothing, Prompt, Buttons, Title)
    End Function

    ''' <summary>
    ''' 显示自定义消息框并指定父窗口以居中。
    ''' </summary>
    Public Function ExMsgBox(
        Owner As IWin32Window,
        Prompt As Object,
        Optional Buttons As Integer = 0,
        Optional Title As Object = Nothing
    ) As MsgBoxResult
        Return 显示(Owner, Prompt, Buttons, Title)
    End Function

    ''' <summary>
    ''' 自定义按钮定义。每个按钮可指定文本与点击回调。
    ''' 回调返回 True 表示关闭对话框；返回 False 则保留对话框（适合实现"复制内容到剪贴板"等不关闭的辅助按钮）。
    ''' 若 <see cref="OnClick"/> 为 Nothing，则点击后直接关闭，并返回该按钮的 <see cref="Index"/> 作为对话框结果。
    ''' </summary>
    Public Class ExMsgBoxButton
        ''' <summary>按钮显示文本。</summary>
        Public Property Text As String
        ''' <summary>是否为高亮（默认）按钮。</summary>
        Public Property IsAccent As Boolean
        ''' <summary>按钮在序列中的序号；由 ExMsgBox 内部赋值。</summary>
        Public Property Index As Integer
        ''' <summary>点击回调。返回 True = 关闭对话框；False = 保持打开。</summary>
        Public Property OnClick As Func(Of ExMsgBoxClickArgs, Boolean)

        Public Sub New()
        End Sub
        Public Sub New(text As String, Optional isAccent As Boolean = False, Optional onClick As Func(Of ExMsgBoxClickArgs, Boolean) = Nothing)
            Me.Text = text
            Me.IsAccent = isAccent
            Me.OnClick = onClick
        End Sub
    End Class

    ''' <summary>自定义按钮点击事件参数。</summary>
    Public Class ExMsgBoxClickArgs
        ''' <summary>触发的按钮。</summary>
        Public Property Button As ExMsgBoxButton
        ''' <summary>对话框承载窗体；可用作其他子窗口的 Owner。</summary>
        Public Property Owner As IWin32Window
        ''' <summary>对话框的全部消息文本，便于按钮回调读取（例如"复制全部"）。</summary>
        Public Property Prompt As String
    End Class

    ''' <summary>
    ''' 显示带有自定义按钮的消息框。
    ''' </summary>
    ''' <param name="Prompt">消息内容。</param>
    ''' <param name="CustomButtons">按钮定义集合（至少一项）。</param>
    ''' <param name="Title">标题；为 Nothing 时使用应用名称。</param>
    ''' <param name="Icon">可选图标样式（MsgBoxStyle 中的 Icon 部分）。</param>
    ''' <param name="Owner">可选父窗口。</param>
    ''' <returns>用户最终关闭对话框时所点击按钮的 <see cref="ExMsgBoxButton.Index"/>；若窗口被关闭按钮关闭则返回 -1。</returns>
    Public Function ExMsgBox(
        Prompt As Object,
        CustomButtons As IEnumerable(Of ExMsgBoxButton),
        Optional Title As Object = Nothing,
        Optional Icon As Integer = 0,
        Optional Owner As IWin32Window = Nothing
    ) As Integer
        Dim 消息文本 As String = If(Prompt?.ToString(), String.Empty)
        Dim 标题文本 As String = If(Title?.ToString(), Application.ProductName)
        Dim 列表 = CustomButtons?.ToList()
        If 列表 Is Nothing OrElse 列表.Count = 0 Then 列表 = New List(Of ExMsgBoxButton) From {New ExMsgBoxButton("OK", True)}
        For i = 0 To 列表.Count - 1 : 列表(i).Index = i : Next
        Using frm As New ExMsgBoxForm(消息文本, Icon, 标题文本, ExMsgBoxTheme.Current, Owner, 列表)
            If Owner IsNot Nothing Then
                frm.ShowDialog(Owner)
            Else
                frm.ShowDialog()
            End If
            Return frm.CustomResultIndex
        End Using
    End Function

    Private Function 显示(owner As IWin32Window, prompt As Object, buttons As Integer, title As Object) As MsgBoxResult
        Dim 消息文本 As String = If(prompt?.ToString(), String.Empty)
        Dim 标题文本 As String = If(title?.ToString(), Application.ProductName)
        Using frm As New ExMsgBoxForm(消息文本, buttons, 标题文本, ExMsgBoxTheme.Current, owner)
            If owner IsNot Nothing Then
                frm.ShowDialog(owner)
            Else
                frm.ShowDialog()
            End If
            Return frm.Result
        End Using
    End Function

End Module

#End Region

#Region "内部窗体"

Friend Class ExMsgBoxForm
    Inherits Form

#Region "Win32"

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(hWnd As IntPtr, Msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function ReleaseCapture() As Boolean
    End Function

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

    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Private Shared Function LoadString(hInstance As IntPtr, uID As UInteger, lpBuffer As System.Text.StringBuilder, nBufferMax As Integer) As Integer
    End Function

    <DllImport("kernel32.dll", CharSet:=CharSet.Auto)>
    Private Shared Function GetModuleHandle(lpModuleName As String) As IntPtr
    End Function

    Private Const WM_NCLBUTTONDOWN As Integer = &HA1
    Private Const WM_EXITSIZEMOVE As Integer = &H232
    Private Const HT_CAPTION As Integer = &H2
    Private Const DWMWA_WINDOW_CORNER_PREFERENCE As Integer = 33
    Private Const DWMWCP_ROUND As Integer = 2

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

    Private Const L_标题栏高度 As Integer = 38
    Private Const L_按钮区高度 As Integer = 60
    Private Const L_内边距 As Integer = 24
    Private Const L_图标尺寸 As Integer = 32
    Private Const L_图标间距 As Integer = 16
    Private Const L_按钮宽度 As Integer = 88
    Private Const L_按钮高度 As Integer = 32
    Private Const L_按钮间距 As Integer = 8
    Private Const L_最小宽度 As Integer = 340
    Private Const L_最大宽度 As Integer = 560
    Private Const L_关闭按钮宽 As Integer = 46
    Private Const L_关闭按钮高 As Integer = 32
    Private Const L_最小内容高 As Integer = 40
    Private Const L_标题左边距 As Integer = 14

#End Region

#Region "字段"

    Private ReadOnly 主题 As ExMsgBoxTheme
    Private ReadOnly 拥有者 As IWin32Window
    Private ReadOnly 毛玻璃 As MessageDialogBackdropController
    Private 返回值 As MsgBoxResult = MsgBoxResult.Cancel
    Private 正在主动关闭 As Boolean = False

    ' 图标
    Private 消息图标位图 As Bitmap

    ' 按钮
    Private ReadOnly 操作按钮 As New List(Of ModernButton)
    Private 默认按钮序号 As Integer = 0
    Private ReadOnly 自定义按钮列表 As List(Of ExMsgBoxButton)
    Private 自定义结果序号 As Integer = -1
    Private ReadOnly 消息原文 As String

    ' 关闭按钮
    Private 允许关闭 As Boolean = True
    Private 关闭返回值 As MsgBoxResult = MsgBoxResult.Cancel
    Private 关闭悬停 As Boolean = False
    Private 关闭按下 As Boolean = False

    ' DPI 缩放系数
    Private Property SC As Single = 1.0F

    ' 缩放后的尺寸
    Private 标题栏高度 As Integer
    Private 按钮区高度 As Integer
    Private 内边距 As Integer
    Private 图标尺寸 As Integer
    Private 图标间距 As Integer
    Private 按钮宽度 As Integer
    Private 按钮高度 As Integer
    Private 按钮间距 As Integer
    Private 最小宽度 As Integer
    Private 最大宽度 As Integer
    Private 关闭按钮宽 As Integer
    Private 关闭按钮高 As Integer
    Private 最小内容高 As Integer
    Private 标题左边距 As Integer

    ' 区域
    Private 关闭按钮区域 As Rectangle
    Private 标题栏区域 As Rectangle
    Private 内容区域 As Rectangle
    Private 按钮区域 As Rectangle
    Private 图标区域 As Rectangle

    ' 控件
    Private 消息标签 As Label

    ' 字体
    Private Property 标题字体 As Font
    Private Property 消息字体 As Font

#End Region

#Region "属性"

    Public ReadOnly Property Result As MsgBoxResult
        Get
            Return 返回值
        End Get
    End Property

    ''' <summary>自定义按钮模式下用户点击的按钮序号；窗口被关闭按钮关闭时为 -1。</summary>
    Public ReadOnly Property CustomResultIndex As Integer
        Get
            Return 自定义结果序号
        End Get
    End Property

#End Region

#Region "构造"

    Public Sub New(prompt As String, buttons As Integer, title As String, theme As ExMsgBoxTheme, owner As IWin32Window)
        Me.New(prompt, buttons, title, theme, owner, Nothing)
    End Sub

    Public Sub New(prompt As String, buttons As Integer, title As String, theme As ExMsgBoxTheme, owner As IWin32Window, customButtons As List(Of ExMsgBoxButton))
        主题 = If(theme, New ExMsgBoxTheme())
        拥有者 = owner
        毛玻璃 = New MessageDialogBackdropController(Me)
        自定义按钮列表 = customButtons
        消息原文 = prompt

        Me.FormBorderStyle = FormBorderStyle.None
        Me.ShowInTaskbar = False
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.DoubleBuffered = True
        Me.KeyPreview = True
        Me.BackColor = 主题.FormBackColor
        Me.Text = title
        Me.StartPosition = If(owner IsNot Nothing, FormStartPosition.CenterParent, FormStartPosition.CenterScreen)

        ' 解析样式位标志
        Dim 按钮样式 As Integer = buttons And &HF
        Dim 图标样式 As Integer = buttons And &HF0
        Dim 默认样式 As Integer = buttons And &HF00

        ' DPI 缩放
        SC = Me.DeviceDpi / 96.0F
        缩放常量()
        ' 自定义按钮模式：允许更宽窗体以承载多行信息
        If 自定义按钮列表 IsNot Nothing Then 最大宽度 = CInt(900 * SC)

        ' 字体
        Dim fontName = MessageDialogRendering.ResolveDialogFontName(owner, Me)
        标题字体 = New Font(fontName, 10.0F, FontStyle.Regular)
        消息字体 = New Font(fontName, 9.5F, FontStyle.Regular)

        ' 图标与声音
        设置图标(图标样式)
        播放声音(图标样式)

        ' 构建界面
        构建消息标签(prompt)
        If 自定义按钮列表 IsNot Nothing Then
            构建自定义按钮()
        Else
            构建操作按钮(按钮样式, 默认样式)
        End If
        配置关闭行为(按钮样式)
        计算布局()

        ' 聚焦默认按钮
        If 默认按钮序号 >= 0 AndAlso 默认按钮序号 < 操作按钮.Count Then
            Me.ActiveControl = 操作按钮(默认按钮序号)
        End If
    End Sub

#End Region

#Region "DWM 初始化"

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Try
            Dim m As New MARGINS With {.left = 1, .right = 1, .top = 1, .bottom = 1}
            Dim unused = DwmExtendFrameIntoClientArea(Me.Handle, m)
            Dim pref As Integer = DWMWCP_ROUND
            Dim unused1 = DwmSetWindowAttribute(Me.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, pref, 4)
        Catch
        End Try
    End Sub

#End Region

#Region "界面构建"

    Private Sub 缩放常量()
        标题栏高度 = CInt(L_标题栏高度 * SC)
        按钮区高度 = CInt(L_按钮区高度 * SC)
        内边距 = CInt(L_内边距 * SC)
        图标尺寸 = CInt(L_图标尺寸 * SC)
        图标间距 = CInt(L_图标间距 * SC)
        按钮宽度 = CInt(L_按钮宽度 * SC)
        按钮高度 = CInt(L_按钮高度 * SC)
        按钮间距 = CInt(L_按钮间距 * SC)
        最小宽度 = CInt(L_最小宽度 * SC)
        最大宽度 = CInt(L_最大宽度 * SC)
        关闭按钮宽 = CInt(L_关闭按钮宽 * SC)
        关闭按钮高 = CInt(L_关闭按钮高 * SC)
        最小内容高 = CInt(L_最小内容高 * SC)
        标题左边距 = CInt(L_标题左边距 * SC)
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
            Dim 是默认 As Boolean = i = 默认按钮序号
            Dim btn As New ModernButton() With {
                .Text = d.文本,
                .Size = New Size(按钮宽度, 按钮高度),
                .Font = New Font(MessageDialogRendering.ResolveDialogFontName(拥有者, Me), 9.0F),
                .Tag = d.返回值,
                .BorderRadius = 主题.ButtonBorderRadius,
                .BorderSize = 1,
                .AnimationDuration = 150,
                .TabStop = True,
                .TabIndex = i
            }
            MessageDialogRendering.ApplyButtonStyle(
                btn, Me, 是默认,
                主题.ButtonBackColor, 主题.ButtonForeColor, 主题.ButtonBorderColor,
                主题.ButtonHoverBackColor, 主题.ButtonPressedBackColor,
                主题.AccentButtonBackColor, 主题.AccentButtonForeColor, 主题.AccentButtonBorderColor,
                主题.AccentButtonHoverBackColor, 主题.AccentButtonPressedBackColor)
            AddHandler btn.Click, AddressOf 操作按钮点击
            Me.Controls.Add(btn)
            操作按钮.Add(btn)
        Next
    End Sub

    Private Sub 配置关闭行为(按钮样式 As Integer)
        Select Case 按钮样式
            Case 2, 4 ' AbortRetryIgnore, YesNo
                允许关闭 = False
            Case 1, 5 ' OkCancel, RetryCancel
                允许关闭 = True
                关闭返回值 = MsgBoxResult.Cancel
            Case 3 ' YesNoCancel
                允许关闭 = True
                关闭返回值 = MsgBoxResult.Cancel
            Case Else ' OkOnly
                允许关闭 = True
                关闭返回值 = MsgBoxResult.Ok
        End Select
    End Sub

    Private Sub 计算布局()
        Dim 有图标 As Boolean = 消息图标位图 IsNot Nothing
        Dim 图标占宽 As Integer = If(有图标, 图标尺寸 + 图标间距, 0)
        Dim 最大文本宽 As Integer = 最大宽度 - 内边距 * 2 - 图标占宽

        ' 测量文本
        Dim 文本尺寸 = TextRenderer.MeasureText(
            消息标签.Text, 消息字体,
            New Size(最大文本宽, Integer.MaxValue),
            TextFormatFlags.WordBreak Or TextFormatFlags.TextBoxControl)

        ' 计算宽度
        Dim 按钮宽合计 As Integer = 0
        For Each btn In 操作按钮 : 按钮宽合计 += btn.Width : Next
        Dim 按钮组总宽 As Integer = 按钮宽合计 + Math.Max(0, 操作按钮.Count - 1) * 按钮间距 + 内边距 * 2
        Dim 内容需要宽 As Integer = 文本尺寸.Width + 图标占宽 + 内边距 * 2
        Dim 标题需要宽 As Integer = TextRenderer.MeasureText(Me.Text, 标题字体).Width + 标题左边距 + 关闭按钮宽 + 标题左边距

        Dim 窗体宽度 As Integer = Math.Max(最小宽度, Math.Max(按钮组总宽, Math.Max(内容需要宽, 标题需要宽)))
        窗体宽度 = Math.Min(窗体宽度, 最大宽度)

        ' 用实际宽度重新测量
        Dim 实际文本宽 As Integer = 窗体宽度 - 内边距 * 2 - 图标占宽
        文本尺寸 = TextRenderer.MeasureText(
            消息标签.Text, 消息字体,
            New Size(实际文本宽, Integer.MaxValue),
            TextFormatFlags.WordBreak Or TextFormatFlags.TextBoxControl)

        ' 高度
        Dim 内容高度 As Integer = Math.Max(最小内容高, Math.Max(文本尺寸.Height, If(有图标, 图标尺寸, 0)))

        ' 限制最大高度
        Dim 屏幕区域 As Rectangle = Screen.PrimaryScreen.WorkingArea
        Dim 最大高度 As Integer = CInt(屏幕区域.Height * 0.8)
        Dim 窗体高度 As Integer = Math.Min(标题栏高度 + 内边距 + 内容高度 + 内边距 + 按钮区高度, 最大高度)
        内容高度 = 窗体高度 - 标题栏高度 - 内边距 * 2 - 按钮区高度

        Me.ClientSize = New Size(窗体宽度, 窗体高度)

        ' 各区域
        标题栏区域 = New Rectangle(0, 0, 窗体宽度, 标题栏高度)
        关闭按钮区域 = New Rectangle(窗体宽度 - 关闭按钮宽, 0, 关闭按钮宽, 关闭按钮高)
        内容区域 = New Rectangle(0, 标题栏高度, 窗体宽度, 内边距 + 内容高度 + 内边距)
        按钮区域 = New Rectangle(0, 窗体高度 - 按钮区高度, 窗体宽度, 按钮区高度)

        ' 图标位置
        If 有图标 Then
            图标区域 = New Rectangle(内边距, 标题栏高度 + 内边距 + (内容高度 - 图标尺寸) \ 2, 图标尺寸, 图标尺寸)
        End If

        ' 消息标签
        Dim 文本Y偏移 As Integer = If(有图标, Math.Max(0, (内容高度 - 文本尺寸.Height) \ 2), 0)
        消息标签.Location = New Point(内边距 + 图标占宽, 标题栏高度 + 内边距 + 文本Y偏移)
        消息标签.Size = New Size(实际文本宽, Math.Min(文本尺寸.Height, 内容高度))

        ' 按钮位置（右对齐）
        Dim 按钮组宽度 As Integer = 按钮宽合计 + Math.Max(0, 操作按钮.Count - 1) * 按钮间距
        Dim btnX As Integer = 窗体宽度 - 内边距 - 按钮组宽度
        Dim btnY As Integer = 按钮区域.Y + (按钮区高度 - 按钮高度) \ 2
        For Each btn In 操作按钮
            btn.Location = New Point(btnX, btnY)
            btnX += btn.Width + 按钮间距
        Next

        MessageDialogRendering.CenterOnOwner(Me, 拥有者)
    End Sub

#End Region

#Region "绘制"

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        If Not 毛玻璃.Enabled Then MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        MessageDialogRendering.CenterOnOwner(Me, 拥有者)
        毛玻璃.Prepare()
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        毛玻璃.Draw(e.Graphics)

        Using scope = D2DHelperV2.BeginPaint(e, Me, 1)
            If scope Is Nothing Then Return
            Dim rt = scope.GraphicsLayer
            Dim compositor = scope.Compositor
            Dim brushCache = compositor.BrushCache
            Dim glass = 毛玻璃.HasFrame

            If Not glass Then
                MessageDialogRendering.FillRectangle(rt, brushCache, 标题栏区域, 主题.TitleBarBackColor)
                MessageDialogRendering.FillRectangle(rt, brushCache, 内容区域, 主题.ContentBackColor)
                MessageDialogRendering.FillRectangle(rt, brushCache, 按钮区域, 主题.ButtonAreaBackColor)
            End If

            Dim 标题文字区域 As New RectangleF(标题左边距, 0, 标题栏区域.Width - 关闭按钮宽 - 标题左边距 * 2, 标题栏高度)
            MessageDialogRendering.DrawText(rt, compositor, Me.Text, 标题字体, 标题文字区域, 主题.TitleForeColor,
                TextFormatFlags.VerticalCenter Or TextFormatFlags.Left Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding, SC)

            If 允许关闭 Then
                MessageDialogRendering.DrawCloseButton(rt, brushCache, 关闭按钮区域, 关闭悬停, 关闭按下,
                                                       主题.CloseButtonForeColor, 主题.CloseButtonHoverForeColor,
                                                       主题.CloseButtonHoverBackColor, SC)
            End If

            MessageDialogRendering.DrawImage(rt, compositor, 消息图标位图, 图标区域)
            MessageDialogRendering.DrawText(rt, compositor, 消息标签.Text, 消息字体, 消息标签.Bounds,
                主题.MessageForeColor, TextFormatFlags.WordBreak Or TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding, SC)

            MessageDialogRendering.DrawRectangle(rt, brushCache,
                New RectangleF(0.5F, 0.5F, Math.Max(0, ClientSize.Width - 1), Math.Max(0, ClientSize.Height - 1)),
                主题.FormBorderColor, 1.0F)
        End Using
    End Sub

#End Region

#Region "鼠标"

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If 允许关闭 AndAlso 关闭按钮区域.Contains(e.Location) Then
            关闭按下 = True
            Me.Invalidate(关闭按钮区域)
        ElseIf 标题栏区域.Contains(e.Location) AndAlso Not 关闭按钮区域.Contains(e.Location) Then
            ReleaseCapture()
            SendMessage(Me.Handle, WM_NCLBUTTONDOWN, New IntPtr(HT_CAPTION), IntPtr.Zero)
            刷新毛玻璃背景()
        End If
    End Sub

    Protected Overrides Sub WndProc(ByRef m As Message)
        MyBase.WndProc(m)
        If m.Msg = WM_EXITSIZEMOVE Then 刷新毛玻璃背景()
    End Sub

    Private Sub 刷新毛玻璃背景()
        If Not 毛玻璃.Enabled OrElse IsDisposed OrElse Not IsHandleCreated Then Return
        毛玻璃.Prepare()
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If 关闭按下 AndAlso 关闭按钮区域.Contains(e.Location) Then
            执行关闭(关闭返回值)
        End If
        关闭按下 = False
        Me.Invalidate(关闭按钮区域)
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim hovered As Boolean = 允许关闭 AndAlso 关闭按钮区域.Contains(e.Location)
        If hovered <> 关闭悬停 Then
            关闭悬停 = hovered
            Me.Invalidate(关闭按钮区域)
        End If
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If 关闭悬停 OrElse 关闭按下 Then
            关闭悬停 = False
            关闭按下 = False
            Me.Invalidate(关闭按钮区域)
        End If
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
                If 允许关闭 Then
                    执行关闭(关闭返回值)
                End If
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

    Private Sub 执行关闭(result As MsgBoxResult)
        返回值 = result
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
                        返回值 = 关闭返回值
                    End If
            End Select
        End If
        MyBase.OnFormClosing(e)
    End Sub

    Private Sub 操作按钮点击(sender As Object, e As EventArgs)
        执行关闭(DirectCast(sender, ModernButton).Tag)
    End Sub

    Private Sub 构建自定义按钮()
        默认按钮序号 = -1
        For i = 0 To 自定义按钮列表.Count - 1
            Dim def = 自定义按钮列表(i)
            If def.IsAccent AndAlso 默认按钮序号 < 0 Then 默认按钮序号 = i
        Next
        If 默认按钮序号 < 0 Then 默认按钮序号 = 自定义按钮列表.Count - 1

        Dim 按钮字体 As New Font(MessageDialogRendering.ResolveDialogFontName(拥有者, Me), 9.0F)
        For i = 0 To 自定义按钮列表.Count - 1
            Dim def = 自定义按钮列表(i)
            Dim 是默认 As Boolean = (i = 默认按钮序号) OrElse def.IsAccent
            ' 按文本宽度自适应（最小=按钮宽度）
            Dim ts = TextRenderer.MeasureText(If(def.Text, ""), 按钮字体)
            Dim w = Math.Max(按钮宽度, ts.Width + CInt(24 * SC))
            Dim btn As New ModernButton() With {
                .Text = If(def.Text, ""),
                .Size = New Size(w, 按钮高度),
                .Font = 按钮字体,
                .Tag = def,
                .BorderRadius = 主题.ButtonBorderRadius,
                .BorderSize = 1,
                .AnimationDuration = 150,
                .TabStop = True,
                .TabIndex = i
            }
            MessageDialogRendering.ApplyButtonStyle(
                btn, Me, 是默认,
                主题.ButtonBackColor, 主题.ButtonForeColor, 主题.ButtonBorderColor,
                主题.ButtonHoverBackColor, 主题.ButtonPressedBackColor,
                主题.AccentButtonBackColor, 主题.AccentButtonForeColor, 主题.AccentButtonBorderColor,
                主题.AccentButtonHoverBackColor, 主题.AccentButtonPressedBackColor)
            AddHandler btn.Click, AddressOf 自定义按钮点击
            Me.Controls.Add(btn)
            操作按钮.Add(btn)
        Next
    End Sub

    Private Sub 自定义按钮点击(sender As Object, e As EventArgs)
        Dim btn = DirectCast(sender, ModernButton)
        Dim def = DirectCast(btn.Tag, ExMsgBoxButton)
        Dim 关闭 As Boolean = True
        If def.OnClick IsNot Nothing Then
            Try
                关闭 = def.OnClick(New ExMsgBoxClickArgs With {.Button = def, .Owner = Me, .Prompt = 消息原文})
            Catch
                关闭 = True
            End Try
        End If
        If 关闭 Then
            自定义结果序号 = def.Index
            正在主动关闭 = True
            Me.DialogResult = DialogResult.OK
            Me.Close()
        End If
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
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
