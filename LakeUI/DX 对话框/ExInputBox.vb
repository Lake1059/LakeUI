Imports System.Runtime.InteropServices

' ═══════════════════════════════════════════════════════════════════════════
'  ExInputBox — LakeUI 自定义输入框
'
'  ● 用法与原版 InputBox 完全一致：
'      Dim s = ExInputBox("请输入姓名：")
'      Dim s = ExInputBox("请输入姓名：", "标题", "默认值")
'
'  ● 全局主题设置：
'      ExInputBoxTheme.Current = ExInputBoxTheme.CreateDark()   ' 暗色（默认）
'      ExInputBoxTheme.Current = ExInputBoxTheme.CreateLight()  ' 亮色
'      ExInputBoxTheme.Current.MessageForeColor = Color.Red     ' 逐项定制
'
'  ● 指定父窗口居中：
'      Dim s = ExInputBox(Me, "请输入姓名：")
' ═══════════════════════════════════════════════════════════════════════════

#Region "主题"

''' <summary>
''' ExInputBox 全局主题配置。
''' 通过 <see cref="Current"/> 设置当前主题，使用 <see cref="CreateDark"/>/<see cref="CreateLight"/> 加载预设，或逐项定制任意颜色。
''' </summary>
Public Class ExInputBoxTheme

    ''' <summary>当前全局主题（默认暗色）。</summary>
    Public Shared Property Current As New ExInputBoxTheme()

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
    ''' <summary>提示文本颜色。</summary>
    Public Property MessageForeColor As Color = Color.FromArgb(230, 230, 230)

    ' ── 输入框 ──
    ''' <summary>输入框背景色。</summary>
    Public Property InputBackColor As Color = Color.FromArgb(36, 36, 36)
    ''' <summary>输入框文字颜色。</summary>
    Public Property InputForeColor As Color = Color.Silver
    ''' <summary>输入框边框颜色。</summary>
    Public Property InputBorderColor As Color = Color.Gray
    ''' <summary>输入框获得焦点时边框颜色。</summary>
    Public Property InputBorderFocusColor As Color = Color.FromArgb(0, 95, 184)
    ''' <summary>输入框圆角半径。</summary>
    Public Property InputBorderRadius As Integer = 4
    ''' <summary>输入框内边距。</summary>
    Public Property InputPadding As Padding = New Padding(5, 0, 5, 0)

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
    Public Shared Function CreateDark() As ExInputBoxTheme
        Return New ExInputBoxTheme()
    End Function

    ''' <summary>创建亮色主题预设。</summary>
    Public Shared Function CreateLight() As ExInputBoxTheme
        Return New ExInputBoxTheme With {
            .FormBackColor = Color.FromArgb(243, 243, 243),
            .FormBorderColor = Color.FromArgb(200, 200, 200),
            .TitleBarBackColor = Color.FromArgb(243, 243, 243),
            .TitleForeColor = Color.FromArgb(30, 30, 30),
            .CloseButtonForeColor = Color.FromArgb(100, 100, 100),
            .CloseButtonHoverBackColor = Color.FromArgb(196, 43, 28),
            .CloseButtonHoverForeColor = Color.White,
            .ContentBackColor = Color.FromArgb(243, 243, 243),
            .MessageForeColor = Color.FromArgb(30, 30, 30),
            .InputBackColor = Color.FromArgb(255, 255, 255),
            .InputForeColor = Color.FromArgb(30, 30, 30),
            .InputBorderColor = Color.FromArgb(200, 200, 200),
            .InputBorderFocusColor = Color.FromArgb(0, 95, 184),
            .InputBorderRadius = 4,
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
''' 提供全局可调用的 ExInputBox() 函数，用法与原版 InputBox 完全一致。
''' </summary>
Public Module ExInputBoxModule

    ''' <summary>
    ''' 显示自定义输入框。
    ''' </summary>
    ''' <param name="Prompt">提示内容。</param>
    ''' <param name="Title">标题栏文字；为 Nothing 时使用应用名称。</param>
    ''' <param name="DefaultResponse">输入框的默认文本。</param>
    ''' <param name="XPos">窗口左边缘 X 坐标；为 -1 时居中。</param>
    ''' <param name="YPos">窗口上边缘 Y 坐标；为 -1 时居中。</param>
    ''' <returns>用户输入的文本，取消时返回空字符串。</returns>
    Public Function ExInputBox(
        Prompt As String,
        Optional Title As String = "",
        Optional DefaultResponse As String = "",
        Optional XPos As Integer = -1,
        Optional YPos As Integer = -1
    ) As String
        Return 显示(Nothing, Prompt, Title, DefaultResponse, XPos, YPos)
    End Function

    ''' <summary>
    ''' 显示自定义输入框并指定父窗口以居中。
    ''' </summary>
    Public Function ExInputBox(
        Owner As IWin32Window,
        Prompt As String,
        Optional Title As String = "",
        Optional DefaultResponse As String = "",
        Optional XPos As Integer = -1,
        Optional YPos As Integer = -1
    ) As String
        Return 显示(Owner, Prompt, Title, DefaultResponse, XPos, YPos)
    End Function

    Private Function 显示(owner As IWin32Window, prompt As String, title As String, defaultResponse As String, xPos As Integer, yPos As Integer) As String
        Dim 提示文本 As String = If(prompt, String.Empty)
        Dim 标题文本 As String = If(String.IsNullOrEmpty(title), Application.ProductName, title)
        Dim 默认值 As String = If(defaultResponse, String.Empty)
        Using frm As New ExInputBoxForm(提示文本, 标题文本, 默认值, xPos, yPos, ExInputBoxTheme.Current, owner)
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

Friend Class ExInputBoxForm
    Inherits Form
    Implements V3_IGpuRenderable, V3_IGpuInvalidationSource

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

    ' 缓存已加载的本地化按钮文本
    Private Shared 本地化按钮文本 As Dictionary(Of UInteger, String)

    ''' <summary>从 user32.dll 加载系统本地化的按钮文本，去除助记符 (&amp;)，加载失败则回退到英文。</summary>
    Private Shared Function 获取按钮文本(resId As UInteger, fallback As String) As String
        If 本地化按钮文本 Is Nothing Then
            本地化按钮文本 = New Dictionary(Of UInteger, String)()
            Try
                Dim hUser32 As IntPtr = GetModuleHandle("user32.dll")
                If hUser32 <> IntPtr.Zero Then
                    For Each id As UInteger In {RES_OK, RES_CANCEL}
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
    Private Const L_按钮宽度 As Integer = 88
    Private Const L_按钮高度 As Integer = 32
    Private Const L_按钮间距 As Integer = 8
    Private Const L_窗体宽度 As Integer = 430
    Private Const L_关闭按钮宽 As Integer = 46
    Private Const L_关闭按钮高 As Integer = 32
    Private Const L_标题左边距 As Integer = 14
    Private Const L_输入框高度 As Integer = 30
    Private Const L_提示与输入间距 As Integer = 12

#End Region

#Region "字段"

    Private ReadOnly 主题 As ExInputBoxTheme
    Private ReadOnly 拥有者 As IWin32Window
    Private ReadOnly 毛玻璃 As MessageDialogBackdropController
    Private 返回文本 As String = String.Empty
    Private 正在主动关闭 As Boolean = False
    Private 已确认 As Boolean = False

    ' 按钮
    Private 确定按钮 As ModernButton
    Private 取消按钮 As ModernButton

    ' 关闭按钮
    Private 关闭悬停 As Boolean = False
    Private 关闭按下 As Boolean = False

    ' DPI 缩放系数
    Private Property SC As Single = 1.0F

    ' 缩放后的尺寸
    Private 标题栏高度 As Integer
    Private 按钮区高度 As Integer
    Private 内边距 As Integer
    Private 按钮宽度 As Integer
    Private 按钮高度 As Integer
    Private 按钮间距 As Integer
    Private 窗体宽度 As Integer
    Private 关闭按钮宽 As Integer
    Private 关闭按钮高 As Integer
    Private 标题左边距 As Integer
    Private 输入框高度 As Integer
    Private 提示与输入间距 As Integer

    ' 区域
    Private 关闭按钮区域 As Rectangle
    Private 标题栏区域 As Rectangle
    Private 内容区域 As Rectangle
    Private 按钮区域 As Rectangle

    ' 控件
    Private 提示标签 As Label
    Private 输入框 As ModernTextBox

    ' 字体
    Private Property 标题字体 As Font
    Private Property 提示字体 As Font

#End Region

#Region "属性"

    ''' <summary>用户输入的文本；取消时为空字符串。</summary>
    Public ReadOnly Property Result As String
        Get
            Return 返回文本
        End Get
    End Property

#End Region

#Region "构造"

    Public Sub New(prompt As String, title As String, defaultResponse As String, xPos As Integer, yPos As Integer, theme As ExInputBoxTheme, owner As IWin32Window)
        主题 = If(theme, New ExInputBoxTheme())
        拥有者 = owner
        毛玻璃 = New MessageDialogBackdropController(Me)

        Me.FormBorderStyle = FormBorderStyle.None
        Me.ShowInTaskbar = False
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.DoubleBuffered = True
        Me.KeyPreview = True
        Me.BackColor = 主题.FormBackColor
        Me.Text = title

        If xPos >= 0 AndAlso yPos >= 0 Then
            Me.StartPosition = FormStartPosition.Manual
        ElseIf owner IsNot Nothing Then
            Me.StartPosition = FormStartPosition.CenterParent
        Else
            Me.StartPosition = FormStartPosition.CenterScreen
        End If

        ' DPI 缩放
        SC = V3_DpiContext.FromControl(Me).Scale
        缩放常量()

        ' 字体
        Dim fontName = MessageDialogRendering.ResolveDialogFontName(owner, Me)
        标题字体 = New Font(fontName, 10.0F, FontStyle.Regular)
        提示字体 = New Font(fontName, 9.5F, FontStyle.Regular)

        ' 构建界面
        构建提示标签(prompt)
        构建输入框(defaultResponse)
        构建操作按钮()
        计算布局(xPos, yPos)

        ' 聚焦输入框并全选
        Me.ActiveControl = 输入框
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
        按钮宽度 = CInt(L_按钮宽度 * SC)
        按钮高度 = CInt(L_按钮高度 * SC)
        按钮间距 = CInt(L_按钮间距 * SC)
        窗体宽度 = CInt(L_窗体宽度 * SC)
        关闭按钮宽 = CInt(L_关闭按钮宽 * SC)
        关闭按钮高 = CInt(L_关闭按钮高 * SC)
        标题左边距 = CInt(L_标题左边距 * SC)
        输入框高度 = CInt(L_输入框高度 * SC)
        提示与输入间距 = CInt(L_提示与输入间距 * SC)
    End Sub

    Private Sub 构建提示标签(prompt As String)
        提示标签 = New Label() With {
            .AutoSize = False,
            .Text = prompt,
            .ForeColor = 主题.MessageForeColor,
            .BackColor = Color.Transparent,
            .Font = 提示字体,
            .UseMnemonic = False,
            .Visible = False
        }
        Me.Controls.Add(提示标签)
    End Sub

    Private Sub 构建输入框(defaultResponse As String)
        输入框 = New ModernTextBox() With {
            .Text = defaultResponse,
            .Font = New Font(MessageDialogRendering.ResolveDialogFontName(拥有者, Me), 9.5F, FontStyle.Regular),
            .BackColor1 = 主题.InputBackColor,
            .ForeColor = 主题.InputForeColor,
            .BorderColor = 主题.InputBorderColor,
            .BorderColorFocus = 主题.InputBorderFocusColor,
            .BorderRadius = 主题.InputBorderRadius,
            .Padding = 主题.InputPadding,
            .BorderSize = 1,
            .MultiLine = False,
            .TabStop = True,
            .TabIndex = 0
        }
        If MessageDialogRendering.IsGlassEnabled() Then
            Dim inputOverlay = Color.FromArgb(40, 220, 220, 220)
            输入框.BackColor = Color.Transparent
            输入框.BackColor1 = inputOverlay
            输入框.BorderColor = inputOverlay
            输入框.BorderColorFocus = inputOverlay
            输入框.BackgroundSource = Me
        End If
        Me.Controls.Add(输入框)
    End Sub

    Private Sub 构建操作按钮()
        Dim sOK As String = 获取按钮文本(RES_OK, "OK")
        Dim sCancel As String = 获取按钮文本(RES_CANCEL, "Cancel")

        ' 确定按钮（高亮）
        确定按钮 = New ModernButton() With {
            .Text = sOK,
            .Size = New Size(按钮宽度, 按钮高度),
            .Font = New Font(MessageDialogRendering.ResolveDialogFontName(拥有者, Me), 9.0F),
            .BorderRadius = 主题.ButtonBorderRadius,
            .BorderSize = 1,
            .AnimationDuration = 150,
            .TabStop = True,
            .TabIndex = 1,
            .BackColor1 = 主题.AccentButtonBackColor,
            .ForeColor = 主题.AccentButtonForeColor,
            .BorderColor = 主题.AccentButtonBorderColor,
            .HoverBackColor1 = 主题.AccentButtonHoverBackColor,
            .PressedBackColor1 = 主题.AccentButtonPressedBackColor
        }
        MessageDialogRendering.ApplyButtonStyle(
            确定按钮, Me, True,
            主题.ButtonBackColor, 主题.ButtonForeColor, 主题.ButtonBorderColor,
            主题.ButtonHoverBackColor, 主题.ButtonPressedBackColor,
            主题.AccentButtonBackColor, 主题.AccentButtonForeColor, 主题.AccentButtonBorderColor,
            主题.AccentButtonHoverBackColor, 主题.AccentButtonPressedBackColor)
        AddHandler 确定按钮.Click, AddressOf 确定按钮点击
        Me.Controls.Add(确定按钮)

        ' 取消按钮
        取消按钮 = New ModernButton() With {
            .Text = sCancel,
            .Size = New Size(按钮宽度, 按钮高度),
            .Font = New Font(MessageDialogRendering.ResolveDialogFontName(拥有者, Me), 9.0F),
            .BorderRadius = 主题.ButtonBorderRadius,
            .BorderSize = 1,
            .AnimationDuration = 150,
            .TabStop = True,
            .TabIndex = 2,
            .BackColor1 = 主题.ButtonBackColor,
            .ForeColor = 主题.ButtonForeColor,
            .BorderColor = 主题.ButtonBorderColor,
            .HoverBackColor1 = 主题.ButtonHoverBackColor,
            .PressedBackColor1 = 主题.ButtonPressedBackColor
        }
        MessageDialogRendering.ApplyButtonStyle(
            取消按钮, Me, False,
            主题.ButtonBackColor, 主题.ButtonForeColor, 主题.ButtonBorderColor,
            主题.ButtonHoverBackColor, 主题.ButtonPressedBackColor,
            主题.AccentButtonBackColor, 主题.AccentButtonForeColor, 主题.AccentButtonBorderColor,
            主题.AccentButtonHoverBackColor, 主题.AccentButtonPressedBackColor)
        AddHandler 取消按钮.Click, AddressOf 取消按钮点击
        Me.Controls.Add(取消按钮)
    End Sub

    Private Sub 计算布局(xPos As Integer, yPos As Integer)
        Dim 最大文本宽 As Integer = 窗体宽度 - 内边距 * 2

        ' 测量提示文本
        Dim 文本尺寸 = TextRenderer.MeasureText(
            提示标签.Text, 提示字体,
            New Size(最大文本宽, Integer.MaxValue),
            TextFormatFlags.WordBreak Or TextFormatFlags.TextBoxControl)

        Dim 提示高度 As Integer = Math.Max(文本尺寸.Height, CInt(20 * SC))

        ' 内容高度 = 提示 + 间距 + 输入框
        Dim 内容高度 As Integer = 提示高度 + 提示与输入间距 + 输入框高度

        ' 窗体高度
        Dim 窗体高度 As Integer = 标题栏高度 + 内边距 + 内容高度 + 内边距 + 按钮区高度

        Me.ClientSize = New Size(窗体宽度, 窗体高度)

        ' 各区域
        标题栏区域 = New Rectangle(0, 0, 窗体宽度, 标题栏高度)
        关闭按钮区域 = New Rectangle(窗体宽度 - 关闭按钮宽, 0, 关闭按钮宽, 关闭按钮高)
        内容区域 = New Rectangle(0, 标题栏高度, 窗体宽度, 内边距 + 内容高度 + 内边距)
        按钮区域 = New Rectangle(0, 窗体高度 - 按钮区高度, 窗体宽度, 按钮区高度)

        ' 提示标签
        提示标签.Location = New Point(内边距, 标题栏高度 + 内边距)
        提示标签.Size = New Size(最大文本宽, 提示高度)

        ' 输入框
        输入框.Location = New Point(内边距, 标题栏高度 + 内边距 + 提示高度 + 提示与输入间距)
        输入框.Size = New Size(最大文本宽, 输入框高度)

        ' 按钮位置（右对齐）
        Dim 按钮组宽度 As Integer = 按钮宽度 * 2 + 按钮间距
        Dim btnX As Integer = 窗体宽度 - 内边距 - 按钮组宽度
        Dim btnY As Integer = 按钮区域.Y + (按钮区高度 - 按钮高度) \ 2
        确定按钮.Location = New Point(btnX, btnY)
        取消按钮.Location = New Point(btnX + 按钮宽度 + 按钮间距, btnY)

        ' 定位窗口
        If xPos >= 0 AndAlso yPos >= 0 Then
            Me.Location = New Point(xPos, yPos)
        ElseIf 拥有者 IsNot Nothing Then
            MessageDialogRendering.CenterOnOwner(Me, 拥有者)
        End If
    End Sub

#End Region

#Region "绘制"

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' V3-only: dialog pixels are emitted by RenderGpu.
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        If 拥有者 IsNot Nothing Then MessageDialogRendering.CenterOnOwner(Me, 拥有者)
        RequestV3Render()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        If context Is Nothing OrElse ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then Return

        Dim bounds As New RectangleF(0, 0, ClientSize.Width, ClientSize.Height)
        Dim glass = MessageDialogRendering.DrawBackdrop(context, bounds)

        If Not glass Then
            MessageDialogRendering.FillRectangle(context, 标题栏区域, 主题.TitleBarBackColor)
            MessageDialogRendering.FillRectangle(context, 内容区域, 主题.ContentBackColor)
            MessageDialogRendering.FillRectangle(context, 按钮区域, 主题.ButtonAreaBackColor)
        End If

        Dim 标题文字区域 As New RectangleF(标题左边距, 0, 标题栏区域.Width - 关闭按钮宽 - 标题左边距 * 2, 标题栏高度)
        MessageDialogRendering.DrawText(context, Me.Text, 标题字体, 标题文字区域, 主题.TitleForeColor,
            TextFormatFlags.VerticalCenter Or TextFormatFlags.Left Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding, SC)

        MessageDialogRendering.DrawCloseButton(context, 关闭按钮区域, 关闭悬停, 关闭按下,
                                               主题.CloseButtonForeColor, 主题.CloseButtonHoverForeColor,
                                               主题.CloseButtonHoverBackColor, SC)

        MessageDialogRendering.DrawText(context, 提示标签.Text, 提示字体, 提示标签.Bounds,
            主题.MessageForeColor, TextFormatFlags.WordBreak Or TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPadding, SC)

        MessageDialogRendering.DrawRectangle(context,
            New RectangleF(0.5F, 0.5F, Math.Max(0, ClientSize.Width - 1), Math.Max(0, ClientSize.Height - 1)),
            主题.FormBorderColor, 1.0F)
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
        If 关闭按钮区域.Contains(e.Location) Then
            关闭按下 = True
            RequestV3Render(关闭按钮区域)
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
        If IsDisposed OrElse Not IsHandleCreated Then Return
        RequestV3Render()
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If 关闭按下 AndAlso 关闭按钮区域.Contains(e.Location) Then
            执行取消()
        End If
        关闭按下 = False
        RequestV3Render(关闭按钮区域)
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim hovered As Boolean = 关闭按钮区域.Contains(e.Location)
        If hovered <> 关闭悬停 Then
            关闭悬停 = hovered
            RequestV3Render(关闭按钮区域)
        End If
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If 关闭悬停 OrElse 关闭按下 Then
            关闭悬停 = False
            关闭按下 = False
            RequestV3Render(关闭按钮区域)
        End If
    End Sub

#End Region

#Region "键盘"

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        Select Case e.KeyCode
            Case Keys.Enter
                执行确定()
                e.Handled = True
            Case Keys.Escape
                执行取消()
                e.Handled = True
        End Select
    End Sub

    Protected Overrides Function ProcessTabKey(forward As Boolean) As Boolean
        Dim 控件列表 As Control() = {输入框, 确定按钮, 取消按钮}
        Dim 当前序号 As Integer = -1
        For i = 0 To 控件列表.Length - 1
            If 控件列表(i).Focused Then
                当前序号 = i
                Exit For
            End If
        Next
        If 当前序号 < 0 Then 当前序号 = 0
        Dim 下一序号 As Integer
        If forward Then
            下一序号 = (当前序号 + 1) Mod 控件列表.Length
        Else
            下一序号 = (当前序号 - 1 + 控件列表.Length) Mod 控件列表.Length
        End If
        控件列表(下一序号).Focus()
        Return True
    End Function

#End Region

#Region "关闭"

    Private Sub 执行确定()
        已确认 = True
        返回文本 = 输入框.Text
        正在主动关闭 = True
        Me.Close()
    End Sub

    Private Sub 执行取消()
        已确认 = False
        返回文本 = String.Empty
        正在主动关闭 = True
        Me.Close()
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If Not 正在主动关闭 Then
            Select Case e.CloseReason
                Case CloseReason.FormOwnerClosing, CloseReason.ApplicationExitCall, CloseReason.WindowsShutDown, CloseReason.TaskManagerClosing
                    ' 外部强制关闭，允许通过
                Case Else
                    返回文本 = String.Empty
            End Select
        End If
        MyBase.OnFormClosing(e)
    End Sub

    Private Sub 确定按钮点击(sender As Object, e As EventArgs)
        执行确定()
    End Sub

    Private Sub 取消按钮点击(sender As Object, e As EventArgs)
        执行取消()
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            标题字体?.Dispose()
            提示字体?.Dispose()
            毛玻璃?.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

#End Region

End Class

#End Region
