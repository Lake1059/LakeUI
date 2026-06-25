Imports System.Runtime.InteropServices

' ═══════════════════════════════════════════════════════════════════════════
'  ExFloatingBox — LakeUI 浮动非模态对话框
'
'  ● 非模态弹出卡片，点击卡片外部区域即自动关闭并释放（类似下拉菜单）。
'    不抢占主窗口焦点，按钮无需额外点击即可响应。
'
'  ● 在控件正上方居中显示：
'      ExFloatingBox(Button1, "操作成功！")
'      ExFloatingBox(Button1, "确认删除？", MsgBoxStyle.YesNo Or MsgBoxStyle.Question, "提示")
'
'  ● 在鼠标位置正上方显示：
'      ExFloatingBox("操作成功！")
'      ExFloatingBox("确认删除？", MsgBoxStyle.YesNo Or MsgBoxStyle.Question, "提示")
'
'  ● 不传按钮（或 Buttons = 0）时不渲染任何按钮，点击外部即消失：
'      ExFloatingBox(Button1, "已复制到剪贴板", MsgBoxStyle.Information)
'
'  ● 自定义按钮（返回从左到右的按钮索引，0 起始；ESC/外部点击返回 -1）：
'      Dim idx = ExFloatingBox(Button1, "文件未保存", {"保存", "不保存", "取消"}, "提示")
'      Dim idx = ExFloatingBox("操作", {"继续", "中止"}, "提示", MsgBoxStyle.Question, 0)
'
'  ● 全局主题设置：
'      ExFloatingBoxTheme.Current = ExFloatingBoxTheme.CreateDark()   ' 暗色（默认）
'      ExFloatingBoxTheme.Current = ExFloatingBoxTheme.CreateLight()  ' 亮色
' ═══════════════════════════════════════════════════════════════════════════

#Region "主题"

''' <summary>
''' ExFloatingBox 全局主题配置。
''' 通过 <see cref="Current"/> 设置当前主题，使用 <see cref="CreateDark"/>/<see cref="CreateLight"/> 加载预设，或逐项定制任意颜色。
''' </summary>
Public Class ExFloatingBoxTheme

    ''' <summary>当前全局主题（默认暗色）。</summary>
    Public Shared Property Current As New ExFloatingBoxTheme()

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

    ' ── 按钮 ──
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

    ' ── 布局 ──
    ''' <summary>卡片与锚点控件（或鼠标位置）之间的垂直间距（像素，逻辑值，会自动适配 DPI）。</summary>
    Public Property AnchorGap As Integer = 8

    ' ── 动画 ──
    ''' <summary>展开动画时长（毫秒）。</summary>
    Public Property OpenAnimationDuration As Integer = 180
    ''' <summary>关闭动画时长（毫秒）。</summary>
    Public Property CloseAnimationDuration As Integer = 120
    ''' <summary>展开时向上滑动的距离（像素，逻辑值，会自动适配 DPI）。</summary>
    Public Property SlideDistance As Integer = 12

    ''' <summary>创建暗色主题预设。</summary>
    Public Shared Function CreateDark() As ExFloatingBoxTheme
        Return New ExFloatingBoxTheme()
    End Function

    ''' <summary>创建亮色主题预设。</summary>
    Public Shared Function CreateLight() As ExFloatingBoxTheme
        Return New ExFloatingBoxTheme With {
            .CardBackColor = Color.FromArgb(249, 249, 249),
            .CardBorderColor = Color.FromArgb(200, 200, 200),
            .CardBorderRadius = 8,
            .TitleForeColor = Color.FromArgb(20, 20, 20),
            .MessageForeColor = Color.FromArgb(30, 30, 30),
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
''' 提供全局可调用的 ExFloatingBox() 函数，显示浮动非模态对话框。
''' 点击卡片外部区域即自动关闭并释放（类似下拉菜单行为）。
''' 无锚点控件时在鼠标位置正上方显示；指定锚点控件时在控件正上方居中对齐显示。
''' </summary>
Public Module ExFloatingBoxModule

    ' ──────────────────── 标准按钮（鼠标位置） ────────────────────

    ''' <summary>
    ''' 在鼠标位置正上方显示浮动对话框。Buttons 为 0 时不渲染按钮。
    ''' </summary>
    Public Function ExFloatingBox(
        Prompt As Object,
        Optional Buttons As Integer = 0,
        Optional Title As Object = Nothing
    ) As MsgBoxResult
        Return 显示标准(Nothing, Prompt, Buttons, Title)
    End Function

    ' ──────────────────── 标准按钮（锚点控件） ────────────────────

    ''' <summary>
    ''' 在指定控件正上方居中显示浮动对话框。Buttons 为 0 时不渲染按钮。
    ''' </summary>
    Public Function ExFloatingBox(
        Anchor As Control,
        Prompt As Object,
        Optional Buttons As Integer = 0,
        Optional Title As Object = Nothing
    ) As MsgBoxResult
        Return 显示标准(Anchor, Prompt, Buttons, Title)
    End Function

    ' ──────────────────── 自定义按钮（鼠标位置） ────────────────────

    ''' <summary>
    ''' 在鼠标位置正上方显示浮动对话框（自定义按钮）。ButtonTexts 为空时不渲染按钮。
    ''' </summary>
    Public Function ExFloatingBox(
        Prompt As Object,
        ButtonTexts() As String,
        Optional Title As Object = Nothing,
        Optional Icon As MsgBoxStyle = 0,
        Optional DefaultButton As Integer = 0
    ) As Integer
        Return 显示自定义(Nothing, Prompt, ButtonTexts, Title, Icon, DefaultButton)
    End Function

    ' ──────────────────── 自定义按钮（锚点控件） ────────────────────

    ''' <summary>
    ''' 在指定控件正上方居中显示浮动对话框（自定义按钮）。ButtonTexts 为空时不渲染按钮。
    ''' </summary>
    Public Function ExFloatingBox(
        Anchor As Control,
        Prompt As Object,
        ButtonTexts() As String,
        Optional Title As Object = Nothing,
        Optional Icon As MsgBoxStyle = 0,
        Optional DefaultButton As Integer = 0
    ) As Integer
        Return 显示自定义(Anchor, Prompt, ButtonTexts, Title, Icon, DefaultButton)
    End Function

    ' ──────────────────── 内部实现 ────────────────────

    Private Function 显示标准(anchor As Control, prompt As Object, buttons As Integer, title As Object) As MsgBoxResult
        Dim frm As New ExFloatingBoxForm(
            If(prompt?.ToString(), String.Empty), buttons,
            title?.ToString(),
            ExFloatingBoxTheme.Current, anchor)
        frm.ShowFloating()
        Return frm.Result
    End Function

    Private Function 显示自定义(anchor As Control, prompt As Object, buttonTexts() As String, title As Object, icon As MsgBoxStyle, defaultButton As Integer) As Integer
        Dim frm As New ExFloatingBoxForm(
            If(prompt?.ToString(), String.Empty), buttonTexts,
            title?.ToString(),
            ExFloatingBoxTheme.Current, anchor,
            CInt(icon) And &HF0, defaultButton)
        frm.ShowFloating()
        Return frm.CustomButtonResult
    End Function

End Module

#End Region

#Region "浮动卡片窗体"

''' <summary>
''' 浮动卡片式非模态对话框窗体。
''' 点击外部区域自动关闭并释放（类似下拉菜单行为）。
''' </summary>
Friend Class ExFloatingBoxForm
    Inherits Form
    Implements IMessageFilter

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
    Private Shared Function EnableWindow(hWnd As IntPtr, bEnable As Boolean) As Boolean
    End Function

    Private Const DWMWA_WINDOW_CORNER_PREFERENCE As Integer = 33
    Private Const DWMWCP_ROUND As Integer = 2

    Private Const WM_LBUTTONDOWN As Integer = &H201
    Private Const WM_RBUTTONDOWN As Integer = &H204
    Private Const WM_MBUTTONDOWN As Integer = &H207
    Private Const WM_NCLBUTTONDOWN As Integer = &HA1
    Private Const WM_KEYDOWN As Integer = &H100
    Private Const VK_ESCAPE As Integer = &H1B

    ' user32.dll 按钮文本资源 ID
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

#Region "IMessageFilter — 外部点击 / ESC 检测"

    Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
        If 已关闭 Then Return False
        Select Case m.Msg
            Case WM_LBUTTONDOWN, WM_RBUTTONDOWN, WM_MBUTTONDOWN, WM_NCLBUTTONDOWN
                If Not Me.IsDisposed AndAlso Not Me.Bounds.Contains(Cursor.Position) Then
                    If Not 正在关闭动画 Then 执行关闭(关闭标记)
                    Return True ' 吞掉外部点击，防止模态蜂鸣
                End If
            Case WM_KEYDOWN
                If m.WParam.ToInt32() = VK_ESCAPE Then
                    If Not 正在关闭动画 Then 执行关闭(关闭标记)
                    Return True
                End If
        End Select
        Return False
    End Function

#End Region

#Region "布局常量"

    Private Const L_卡片内边距 As Integer = 15
    Private Const L_标题下间距 As Integer = 6
    Private Const L_图标尺寸 As Integer = 32
    Private Const L_图标间距 As Integer = 12
    Private Const L_按钮上间距 As Integer = 15
    Private Const L_按钮宽度 As Integer = 88
    Private Const L_按钮高度 As Integer = 30
    Private Const L_按钮间距 As Integer = 8
    Private Const L_卡片最小宽度 As Integer = 200
    Private Const L_卡片最大宽度 As Integer = 480
    Private Const L_最小内容高 As Integer = 20

#End Region

#Region "字段"

    Private 主题 As ExFloatingBoxTheme
    Private 锚点控件 As Control
    Private 毛玻璃 As MessageDialogBackdropController
    Private 返回值 As MsgBoxResult = MsgBoxResult.Ok
    Private 已关闭 As Boolean = False

    ' 自定义按钮模式
    Private ReadOnly 是自定义按钮模式 As Boolean = False
    Private 自定义返回索引 As Integer = -1

    ' 标题 / 按钮
    Private 有标题 As Boolean = False
    Private 有按钮 As Boolean = False

    ' 图标
    Private 消息图标位图 As Bitmap

    ' 按钮
    Private ReadOnly 操作按钮 As New List(Of ModernButton)
    Private 默认按钮序号 As Integer = 0

    ' 关闭行为
    Private 关闭标记 As Object = MsgBoxResult.Ok

    ' DPI 缩放系数
    Private SC As Single = 1.0F

    ' 缩放后的尺寸
    Private 卡片内边距 As Integer
    Private 标题下间距 As Integer
    Private 图标尺寸 As Integer
    Private 图标间距 As Integer
    Private 按钮上间距 As Integer
    Private 按钮宽度 As Integer
    Private 按钮高度 As Integer
    Private 按钮间距 As Integer
    Private 卡片最小宽度 As Integer
    Private 卡片最大宽度 As Integer
    Private 最小内容高 As Integer

    ' 区域
    Private 图标区域 As Rectangle

    ' 控件
    Private 标题标签 As Label
    Private 消息标签 As Label

    ' 字体
    Private 标题字体 As Font
    Private 消息字体 As Font

    ' 动画
    Private ReadOnly 动画秒表 As New Stopwatch()
    Private 动画计时器 As Timer
    Private 正在展开动画 As Boolean = False
    Private 正在关闭动画 As Boolean = False
    Private 最终位置 As Point
    Private 滑动像素 As Integer

#End Region

#Region "属性"

    Public ReadOnly Property Result As MsgBoxResult
        Get
            Return 返回值
        End Get
    End Property

    Public ReadOnly Property CustomButtonResult As Integer
        Get
            Return 自定义返回索引
        End Get
    End Property

#End Region

#Region "构造"

    ''' <summary>标准按钮模式构造。</summary>
    Public Sub New(prompt As String, buttons As Integer, title As String, theme As ExFloatingBoxTheme, anchor As Control)
        初始化通用(prompt, title, theme, anchor)
        Dim 按钮样式 As Integer = buttons And &HF
        Dim 图标样式 As Integer = buttons And &HF0
        设置图标(图标样式)
        播放声音(图标样式)
        If 按钮样式 <> 0 Then
            构建操作按钮(按钮样式, buttons And &HF00)
            配置关闭行为(按钮样式)
        End If
        完成初始化()
    End Sub

    ''' <summary>自定义按钮模式构造。</summary>
    Public Sub New(prompt As String, customButtonTexts() As String, title As String, theme As ExFloatingBoxTheme, anchor As Control, iconStyle As Integer, defaultButtonIndex As Integer)
        是自定义按钮模式 = True
        初始化通用(prompt, title, theme, anchor)
        设置图标(iconStyle)
        播放声音(iconStyle)
        If customButtonTexts IsNot Nothing AndAlso customButtonTexts.Length > 0 Then
            构建自定义按钮(customButtonTexts, defaultButtonIndex)
        End If
        关闭标记 = CObj(-1)
        完成初始化()
    End Sub

    Private Sub 初始化通用(prompt As String, title As String, theme As ExFloatingBoxTheme, anchor As Control)
        主题 = If(theme, New ExFloatingBoxTheme())
        锚点控件 = anchor
        毛玻璃 = New MessageDialogBackdropController(Me)

        Me.FormBorderStyle = FormBorderStyle.None
        Me.ShowInTaskbar = False
        Me.StartPosition = FormStartPosition.Manual
        Me.TopMost = True
        Me.DoubleBuffered = True
        Me.BackColor = 主题.CardBackColor

        SC = D2DGlobals.GetCurrentDpiScale(Me)
        缩放常量()

        Dim fontName = MessageDialogRendering.ResolveDialogFontName(anchor, Me)
        标题字体 = New Font(fontName, 11.0F, FontStyle.Bold)
        消息字体 = New Font(fontName, 9.5F, FontStyle.Regular)

        有标题 = Not String.IsNullOrEmpty(title)
        If 有标题 Then 构建标题标签(title)
        构建消息标签(prompt)

        滑动像素 = CInt(主题.SlideDistance * SC)
    End Sub

    Private Sub 完成初始化()
        有按钮 = 操作按钮.Count > 0
        计算布局()
        定位卡片()
    End Sub

#End Region

#Region "显示与消息循环"

    ''' <summary>
    ''' 以非模态方式显示浮动卡片（不抢占焦点），并运行本地消息循环直到卡片关闭。
    ''' </summary>
    Public Sub ShowFloating()
        Application.AddMessageFilter(Me)

        ' 初始状态：透明 + 偏移
        Me.Opacity = 0
        Me.Location = New Point(最终位置.X, 最终位置.Y + 滑动像素)

        ' 先启动展开动画，ShowDialog 的模态消息循环会处理定时器事件
        正在展开动画 = True
        动画秒表.Restart()
        动画计时器 = New Timer() With {.Interval = 10}
        AddHandler 动画计时器.Tick, AddressOf 动画帧更新
        动画计时器.Start()

        Dim ownerForm As Form = Nothing
        If 锚点控件 IsNot Nothing Then ownerForm = 锚点控件.FindForm()
        If ownerForm Is Nothing Then ownerForm = Form.ActiveForm
        Me.ShowDialog(ownerForm)

        Application.RemoveMessageFilter(Me)
        If Not Me.IsDisposed Then Me.Dispose()
    End Sub

#End Region

#Region "动画"

    Private Sub 动画帧更新(sender As Object, e As EventArgs)
        If 正在展开动画 Then
            Dim duration As Integer = 主题.OpenAnimationDuration
            If duration <= 0 Then duration = 1
            Dim t As Single = CSng(Math.Min(动画秒表.Elapsed.TotalMilliseconds / duration, 1.0))
            ' EaseOutCubic
            Dim eased As Single = 1.0F - CSng(Math.Pow(1.0 - t, 3))
            Me.Opacity = eased
            Me.Location = New Point(最终位置.X, 最终位置.Y + CInt(滑动像素 * (1.0F - eased)))
            If t >= 1.0F Then
                正在展开动画 = False
                Me.Opacity = 1.0
                Me.Location = 最终位置
                停止动画()
            End If
        ElseIf 正在关闭动画 Then
            Dim duration As Integer = 主题.CloseAnimationDuration
            If duration <= 0 Then duration = 1
            Dim t As Single = CSng(Math.Min(动画秒表.Elapsed.TotalMilliseconds / duration, 1.0))
            Dim eased As Single = 1.0F - CSng(Math.Pow(1.0 - t, 3))
            Me.Opacity = Math.Max(0, 1.0 - eased)
            If t >= 1.0F Then
                正在关闭动画 = False
                停止动画()
                已关闭 = True
                Me.Close()
            End If
        End If
    End Sub

    Private Sub 停止动画()
        If 动画计时器 IsNot Nothing Then
            动画计时器.Stop()
        End If
        动画秒表.Stop()
    End Sub

#End Region

#Region "DWM 圆角"

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Try
            Dim m As New MARGINS With {.left = 1, .right = 1, .top = 1, .bottom = 1}
            Dim unused0 = DwmExtendFrameIntoClientArea(Me.Handle, m)
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
        按钮上间距 = CInt(L_按钮上间距 * SC)
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
                Return
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
            Case 1, 2, 3, 4, 5
                关闭标记 = MsgBoxResult.Cancel
        End Select
    End Sub

    Private Sub 构建自定义按钮(buttonTexts() As String, defaultIndex As Integer)
        默认按钮序号 = Math.Max(0, Math.Min(defaultIndex, buttonTexts.Length - 1))
        For i = 0 To buttonTexts.Length - 1
            添加按钮(buttonTexts(i), i, i, i = 默认按钮序号)
        Next
    End Sub

    Private Sub 添加按钮(text As String, tag As Object, index As Integer, isDefault As Boolean)
        Dim btn As New ModernButton() With {
            .Text = text,
            .Size = New Size(按钮宽度, 按钮高度),
            .Font = New Font(MessageDialogRendering.ResolveDialogFontName(锚点控件, Me), 9.0F),
            .Tag = tag,
            .BorderRadius = 主题.ButtonBorderRadius,
            .BorderSize = 1,
            .AnimationDuration = 150,
            .TabStop = False,
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
        Dim 标题高度 As Integer = 0
        Dim 标题宽度 As Integer = 0
        If 有标题 Then
            Dim 标题尺寸 = TextRenderer.MeasureText(
                标题标签.Text, 标题字体,
                New Size(最大文本宽, Integer.MaxValue),
                TextFormatFlags.WordBreak Or TextFormatFlags.TextBoxControl)
            标题高度 = 标题尺寸.Height
            标题宽度 = 标题尺寸.Width
        End If

        ' 测量消息
        Dim 文本尺寸 = TextRenderer.MeasureText(
            消息标签.Text, 消息字体,
            New Size(最大文本宽, Integer.MaxValue),
            TextFormatFlags.WordBreak Or TextFormatFlags.TextBoxControl)

        ' 计算卡片宽度
        Dim 按钮组总宽 As Integer = If(有按钮, 操作按钮.Count * 按钮宽度 + (操作按钮.Count - 1) * 按钮间距 + 卡片内边距 * 2, 0)
        Dim 内容需要宽 As Integer = Math.Max(标题宽度, 文本尺寸.Width + 图标占宽) + 卡片内边距 * 2
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

        ' 卡片总高度
        Dim 标题区高度 As Integer = If(有标题, 标题高度 + 标题下间距, 0)
        Dim 按钮区高度 As Integer = If(有按钮, 按钮上间距 + 按钮高度, 0)
        Dim 卡片高度 As Integer = 卡片内边距 + 标题区高度 + 消息高度 + 按钮区高度 + 卡片内边距

        ' 限制最大高度
        Dim 屏幕区域 As Rectangle = Screen.PrimaryScreen.WorkingArea
        Dim 最大高度 As Integer = CInt(屏幕区域.Height * 0.6)
        卡片高度 = Math.Min(卡片高度, 最大高度)

        Me.ClientSize = New Size(卡片宽度, 卡片高度)

        Dim curY As Integer = 卡片内边距

        ' 标题标签
        If 有标题 Then
            标题标签.Location = New Point(卡片内边距, curY)
            标题标签.Size = New Size(卡片宽度 - 卡片内边距 * 2, 标题高度)
            curY += 标题高度 + 标题下间距
        End If

        ' 图标区域
        If 有图标 Then
            图标区域 = New Rectangle(
                卡片内边距,
                curY + (消息高度 - 图标尺寸) \ 2,
                图标尺寸, 图标尺寸)
        End If

        ' 消息标签
        Dim 消息X As Integer = 卡片内边距 + 图标占宽
        Dim 可用消息高 As Integer = 消息高度
        Dim 文本Y偏移 As Integer = If(有图标, Math.Max(0, (可用消息高 - 文本尺寸.Height) \ 2), 0)
        消息标签.Location = New Point(消息X, curY + 文本Y偏移)
        消息标签.Size = New Size(实际文本宽, Math.Min(文本尺寸.Height, 可用消息高))

        ' 按钮位置（右对齐）
        If 有按钮 Then
            Dim btnY As Integer = 卡片高度 - 卡片内边距 - 按钮高度
            Dim 按钮组宽度 As Integer = 操作按钮.Count * 按钮宽度 + (操作按钮.Count - 1) * 按钮间距
            Dim btnX As Integer = 卡片宽度 - 卡片内边距 - 按钮组宽度
            For Each btn In 操作按钮
                btn.Location = New Point(btnX, btnY)
                btnX += 按钮宽度 + 按钮间距
            Next
        End If
    End Sub

    Private Sub 定位卡片()
        Dim 屏幕区域 As Rectangle = Screen.FromPoint(Cursor.Position).WorkingArea
        Dim gap As Integer = CInt(主题.AnchorGap * SC)
        Dim 锚点中心X As Integer
        Dim 锚点顶部Y As Integer

        If 锚点控件 IsNot Nothing Then
            Dim 锚点屏幕位置 As Point = 锚点控件.PointToScreen(Point.Empty)
            锚点中心X = 锚点屏幕位置.X + 锚点控件.Width \ 2
            锚点顶部Y = 锚点屏幕位置.Y
        Else
            锚点中心X = Cursor.Position.X
            锚点顶部Y = Cursor.Position.Y
        End If

        Dim x As Integer = 锚点中心X - Me.Width \ 2
        Dim y As Integer = 锚点顶部Y - Me.Height - gap

        If x < 屏幕区域.Left Then x = 屏幕区域.Left
        If x + Me.Width > 屏幕区域.Right Then x = 屏幕区域.Right - Me.Width

        If y < 屏幕区域.Top Then
            If 锚点控件 IsNot Nothing Then
                Dim 锚点屏幕位置 As Point = 锚点控件.PointToScreen(Point.Empty)
                y = 锚点屏幕位置.Y + 锚点控件.Height + gap
            Else
                y = Cursor.Position.Y + gap
            End If
        End If
        If y + Me.Height > 屏幕区域.Bottom Then y = 屏幕区域.Bottom - Me.Height

        最终位置 = New Point(x, y)
    End Sub

#End Region

#Region "绘制"

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        If Not 毛玻璃.Enabled Then MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        ' ShowDialog 会禁用 Owner，重新启用以便 IMessageFilter 检测外部点击
        If Me.Owner IsNot Nothing AndAlso Me.Owner.IsHandleCreated Then
            EnableWindow(Me.Owner.Handle, True)
        End If
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
            If Not 毛玻璃.HasFrame Then
                MessageDialogRendering.FillRectangle(rt, brushCache, New RectangleF(0, 0, ClientSize.Width, ClientSize.Height), 主题.CardBackColor)
            End If

            If 有标题 Then
                MessageDialogRendering.DrawText(rt, compositor, 标题标签.Text, 标题字体, 标题标签.Bounds,
                    主题.TitleForeColor, TextFormatFlags.WordBreak Or TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding, SC)
            End If
            MessageDialogRendering.DrawText(rt, compositor, 消息标签.Text, 消息字体, 消息标签.Bounds,
                主题.MessageForeColor, TextFormatFlags.WordBreak Or TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding, SC)
            MessageDialogRendering.DrawImage(rt, compositor, 消息图标位图, 图标区域)
            MessageDialogRendering.DrawRectangle(rt, brushCache,
                New RectangleF(0.5F, 0.5F, Math.Max(0, ClientSize.Width - 1), Math.Max(0, ClientSize.Height - 1)),
                主题.CardBorderColor, 1.0F)
        End Using
    End Sub

#End Region

#Region "关闭"

    Private Sub 存储结果(tag As Object)
        If 是自定义按钮模式 Then
            自定义返回索引 = CInt(tag)
        Else
            返回值 = CType(tag, MsgBoxResult)
        End If
    End Sub

    Friend Sub 执行关闭(tag As Object)
        If 已关闭 OrElse 正在关闭动画 Then Return
        存储结果(tag)
        ' 启动渐出动画
        正在展开动画 = False
        正在关闭动画 = True
        动画秒表.Restart()
        If 动画计时器 Is Nothing Then
            动画计时器 = New Timer() With {.Interval = 10}
            AddHandler 动画计时器.Tick, AddressOf 动画帧更新
        End If
        动画计时器.Start()
    End Sub

    Protected Overrides Sub OnDeactivate(e As EventArgs)
        MyBase.OnDeactivate(e)
        ' 窗口失去焦点时关闭（点击其他应用程序、Alt+Tab 等）
        If Not 已关闭 AndAlso Not 正在关闭动画 Then
            执行关闭(关闭标记)
        End If
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If Not 已关闭 Then
            已关闭 = True
        End If
        MyBase.OnFormClosing(e)
    End Sub

    Private Sub 操作按钮点击(sender As Object, e As EventArgs)
        执行关闭(DirectCast(sender, ModernButton).Tag)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            Application.RemoveMessageFilter(Me)
            动画计时器?.Stop()
            动画计时器?.Dispose()
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
