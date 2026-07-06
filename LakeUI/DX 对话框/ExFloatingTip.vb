Imports System.Runtime.InteropServices

' ═══════════════════════════════════════════════════════════════════════════
'  ExFloatingTip — LakeUI 浮动纯文本提示
'
'  ● 非模态浮动提示，显示指定时长后自动渐出消失。
'    不抢占主窗口焦点，不阻塞调用线程。
'    点击提示本身 / 点击外部区域 / 按 ESC 均可立即关闭。
'
'  ● 在控件正上方居中显示：
'      ExFloatingTip(Button1, "操作成功！")
'      ExFloatingTip(Button1, "已复制到剪贴板", 3000)
'
'  ● 在鼠标位置正上方显示：
'      ExFloatingTip("操作成功！")
'      ExFloatingTip("已复制到剪贴板", 3000)
'
'  ● 全局主题设置：
'      ExFloatingTipTheme.Current = ExFloatingTipTheme.CreateDark()   ' 暗色（默认）
'      ExFloatingTipTheme.Current = ExFloatingTipTheme.CreateLight()  ' 亮色
' ═══════════════════════════════════════════════════════════════════════════

#Region "主题"

''' <summary>
''' ExFloatingTip 全局主题配置。
''' 通过 <see cref="Current"/> 设置当前主题，使用 <see cref="CreateDark"/>/<see cref="CreateLight"/> 加载预设，或逐项定制任意颜色。
''' </summary>
Public Class ExFloatingTipTheme

    ''' <summary>当前全局主题（默认暗色）。</summary>
    Public Shared Property Current As New ExFloatingTipTheme()

    ' ── 卡片 ──
    ''' <summary>卡片背景色。</summary>
    Public Property CardBackColor As Color = Color.FromArgb(44, 44, 44)
    ''' <summary>卡片边框颜色。</summary>
    Public Property CardBorderColor As Color = Color.FromArgb(70, 70, 70)
    ''' <summary>卡片边框宽度（像素，逻辑值，会自动适配 DPI）。</summary>
    Public Property CardBorderSize As Integer = 1

    ' ── 内容区 ──
    ''' <summary>消息文本颜色。</summary>
    Public Property MessageForeColor As Color = Color.FromArgb(220, 220, 220)

    ' ── 布局 ──
    ''' <summary>卡片内边距（像素，逻辑值，会自动适配 DPI）。</summary>
    Public Property Padding As Integer = 10
    ''' <summary>卡片最大宽度（像素，逻辑值，会自动适配 DPI）。</summary>
    Public Property MaxWidth As Integer = 300
    ''' <summary>卡片与锚点控件（或鼠标位置）之间的垂直间距（像素，逻辑值，会自动适配 DPI）。</summary>
    Public Property AnchorGap As Integer = 8

    ' ── 自动关闭 ──
    ''' <summary>默认显示时长（毫秒）。</summary>
    Public Property DisplayDuration As Integer = 2000

    ' ── 动画 ──
    ''' <summary>展开动画时长（毫秒）。</summary>
    Public Property OpenAnimationDuration As Integer = 180
    ''' <summary>关闭动画时长（毫秒）。</summary>
    Public Property CloseAnimationDuration As Integer = 120
    ''' <summary>展开时向上滑动的距离（像素，逻辑值，会自动适配 DPI）。</summary>
    Public Property SlideDistance As Integer = 12

    ''' <summary>创建暗色主题预设。</summary>
    Public Shared Function CreateDark() As ExFloatingTipTheme
        Return New ExFloatingTipTheme()
    End Function

    ''' <summary>创建亮色主题预设。</summary>
    Public Shared Function CreateLight() As ExFloatingTipTheme
        Return New ExFloatingTipTheme With {
            .CardBackColor = Color.FromArgb(249, 249, 249),
            .CardBorderColor = Color.FromArgb(200, 200, 200),
            .MessageForeColor = Color.FromArgb(30, 30, 30)
        }
    End Function

End Class

#End Region

#Region "公共接口"

''' <summary>
''' 提供全局可调用的 ExFloatingTip() 函数，显示浮动纯文本提示。
''' 提示在指定时长后自动渐出消失，不阻塞调用线程。
''' 无锚点控件时在鼠标位置正上方显示；指定锚点控件时在控件正上方居中对齐显示。
''' </summary>
Public Module ExFloatingTipModule

    ' ──────────────────── 鼠标位置 ────────────────────

    ''' <summary>
    ''' 在鼠标位置正上方显示浮动提示。Duration 为 0 时使用主题默认时长。
    ''' </summary>
    Public Sub ExFloatingTip(
        Prompt As Object,
        Optional Duration As Integer = 0
    )
        显示提示(Nothing, Prompt, Duration)
    End Sub

    ' ──────────────────── 锚点控件 ────────────────────

    ''' <summary>
    ''' 在指定控件正上方居中显示浮动提示。Duration 为 0 时使用主题默认时长。
    ''' </summary>
    Public Sub ExFloatingTip(
        Anchor As Control,
        Prompt As Object,
        Optional Duration As Integer = 0
    )
        显示提示(Anchor, Prompt, Duration)
    End Sub

    ' ──────────────────── 内部实现 ────────────────────

    Private Sub 显示提示(anchor As Control, prompt As Object, duration As Integer)
        Dim theme = ExFloatingTipTheme.Current
        Dim ms = If(duration > 0, duration, theme.DisplayDuration)
        Dim frm As New ExFloatingTipForm(
            If(prompt?.ToString(), String.Empty), theme, anchor, ms)
        frm.ShowFloating()
    End Sub

End Module

#End Region

#Region "浮动提示窗体"

''' <summary>
''' 浮动纯文本提示窗体。
''' 显示指定时长后自动渐出消失；点击提示 / 外部区域 / ESC 可立即关闭。
''' </summary>
Friend Class ExFloatingTipForm
    Inherits PopupForm
    Implements IMessageFilter, V3_IGpuRenderable, V3_IGpuInvalidationSource

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

    Private Const DWMWA_WINDOW_CORNER_PREFERENCE As Integer = 33
    Private Const DWMWCP_ROUND As Integer = 2

    Private Const WM_LBUTTONDOWN As Integer = &H201
    Private Const WM_RBUTTONDOWN As Integer = &H204
    Private Const WM_MBUTTONDOWN As Integer = &H207
    Private Const WM_NCLBUTTONDOWN As Integer = &HA1
    Private Const WM_KEYDOWN As Integer = &H100
    Private Const VK_ESCAPE As Integer = &H1B

#End Region

#Region "IMessageFilter — 外部点击 / ESC 检测"

    Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
        If 已关闭 Then Return False
        Select Case m.Msg
            Case WM_LBUTTONDOWN, WM_RBUTTONDOWN, WM_MBUTTONDOWN, WM_NCLBUTTONDOWN
                If Not Me.IsDisposed AndAlso Not Me.Bounds.Contains(Cursor.Position) Then
                    执行关闭()
                End If
            Case WM_KEYDOWN
                If m.WParam.ToInt32() = VK_ESCAPE Then
                    执行关闭()
                End If
        End Select
        Return False ' 不吞掉消息，保持底层控件正常响应
    End Function

#End Region

#Region "字段"

    Private 主题 As ExFloatingTipTheme
    Private 锚点控件 As Control
    Private 已关闭 As Boolean = False

    ' DPI 缩放系数
    Private SC As Single = 1.0F

    ' 缩放后的尺寸
    Private 卡片内边距 As Integer
    Private 卡片边框宽度 As Integer
    Private 卡片最大宽度 As Integer

    ' 文本
    Private 消息文本 As String

    ' 字体
    Private 消息字体 As Font

    ' 动画
    Private ReadOnly 动画秒表 As New Stopwatch()
    Private 动画计时器 As PrecisionTimer
    Private 正在展开动画 As Boolean = False
    Private 正在关闭动画 As Boolean = False
    Private 最终位置 As Point
    Private 滑动像素 As Integer

    ' 自动关闭
    Private 自动关闭计时器 As Timer
    Private 显示时长 As Integer

#End Region

#Region "构造"

    Public Sub New(prompt As String, theme As ExFloatingTipTheme, anchor As Control, duration As Integer)
        主题 = If(theme, New ExFloatingTipTheme())
        锚点控件 = anchor
        显示时长 = duration

        Me.DoubleBuffered = True
        Me.BackColor = 主题.CardBackColor

        SC = ResolveDpiScale(anchor)
        卡片内边距 = 缩放逻辑尺寸(主题.Padding)
        卡片边框宽度 = 缩放边框宽度(主题.CardBorderSize)
        卡片最大宽度 = Math.Max(1, 缩放逻辑尺寸(主题.MaxWidth))

        消息字体 = New Font(MessageDialogRendering.ResolveDialogFontName(anchor, Me), 9.5F, FontStyle.Regular)
        消息文本 = prompt

        滑动像素 = 缩放逻辑尺寸(主题.SlideDistance)

        计算布局()
        定位卡片()
    End Sub

#End Region

#Region "显示"

    ''' <summary>
    ''' 以非模态方式显示浮动提示（不抢占焦点、不阻塞调用线程）。
    ''' </summary>
    Public Sub ShowFloating()
        Application.AddMessageFilter(Me)

        ' 初始状态：透明 + 偏移
        Me.Opacity = 0
        Me.Location = New Point(最终位置.X, 最终位置.Y + 滑动像素)

        ' 启动展开动画
        正在展开动画 = True
        动画秒表.Restart()
        动画计时器 = 创建动画计时器()
        动画计时器.Start()

        Dim ownerForm As Form = Nothing
        If 锚点控件 IsNot Nothing Then ownerForm = 锚点控件.FindForm()
        If ownerForm Is Nothing Then ownerForm = Form.ActiveForm
        Me.Show(ownerForm)
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
                启动自动关闭()
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

    Private Shared Function FrameIntervalMilliseconds(fps As Integer) As Integer
        fps = Math.Max(1, fps)
        Return Math.Max(1, CInt(Math.Ceiling(1000.0R / fps)))
    End Function

    Private Function 创建动画计时器() As PrecisionTimer
        Dim timer As New PrecisionTimer() With {
            .Interval = FrameIntervalMilliseconds(60),
            .DispatchMode = PrecisionTimer.DispatchModeEnum.NonBlocking,
            .OverrunPolicy = PrecisionTimer.OverrunPolicyEnum.Drop,
            .WorkerThreadCount = 1,
            .SynchronizingObject = Me
        }
        AddHandler timer.Tick, AddressOf 动画帧更新
        Return timer
    End Function

    Private Sub 启动自动关闭()
        自动关闭计时器 = New Timer() With {.Interval = 显示时长}
        AddHandler 自动关闭计时器.Tick, AddressOf 自动关闭回调
        自动关闭计时器.Start()
    End Sub

    Private Sub 自动关闭回调(sender As Object, e As EventArgs)
        自动关闭计时器.Stop()
        If Not 正在关闭动画 AndAlso Not 已关闭 Then 执行关闭()
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

    Private Const 文本标志 As TextFormatFlags =
        TextFormatFlags.WordBreak Or TextFormatFlags.TextBoxControl Or
        TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter

    Private Sub 计算布局()
        ' 测量消息（约束宽度为实际可用文本宽度，即卡片最大宽度减去左右内边距）
        Dim 最大文本宽 As Integer = Math.Max(1, 卡片最大宽度 - 卡片内边距 * 2 - 卡片边框宽度 * 2)
        Dim 文本尺寸 = TextRenderer.MeasureText(
            消息文本, 消息字体,
            New Size(最大文本宽, Integer.MaxValue),
            文本标志)

        ' 卡片宽度 = 左右内边距 + 文本宽度（紧贴文本，无多余空白）
        Dim 卡片宽度 As Integer = Math.Min(文本尺寸.Width + 卡片内边距 * 2 + 卡片边框宽度 * 2, 卡片最大宽度)

        ' 用实际宽度重新测量
        Dim 实际文本宽 As Integer = Math.Max(1, 卡片宽度 - 卡片内边距 * 2 - 卡片边框宽度 * 2)
        文本尺寸 = TextRenderer.MeasureText(
            消息文本, 消息字体,
            New Size(实际文本宽, Integer.MaxValue),
            文本标志)

        ' 高度 = 上下内边距 + 文本高度
        Dim 卡片高度 As Integer = 卡片内边距 * 2 + 卡片边框宽度 * 2 + 文本尺寸.Height

        Me.ClientSize = New Size(Math.Max(1, 卡片宽度), Math.Max(1, 卡片高度))
    End Sub

    Private Sub 定位卡片()
        Dim 屏幕区域 As Rectangle = Screen.FromPoint(Cursor.Position).WorkingArea
        Dim gap As Integer = 缩放逻辑尺寸(主题.AnchorGap)
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
                y = 锚点顶部Y + 锚点控件.Height + gap
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
        ' V3-only: tip pixels are emitted by RenderGpu.
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        RequestV3Render()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me, 1) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        If context Is Nothing OrElse ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then Return

        Dim bounds As New RectangleF(0, 0, ClientSize.Width, ClientSize.Height)
        Dim glass = MessageDialogRendering.DrawBackdrop(context, bounds)
        If Not glass Then
            MessageDialogRendering.FillRectangle(context, bounds, 主题.CardBackColor)
        End If

        Dim textRect As New RectangleF(
            卡片边框宽度 + 卡片内边距,
            卡片边框宽度 + 卡片内边距,
            Me.ClientSize.Width - (卡片边框宽度 + 卡片内边距) * 2,
            Me.ClientSize.Height - (卡片边框宽度 + 卡片内边距) * 2)
        MessageDialogRendering.DrawText(context, 消息文本, 消息字体, textRect,
            主题.MessageForeColor, 文本标志, SC)

        If 卡片边框宽度 > 0 Then
            MessageDialogRendering.DrawInsetRectangle(context,
                New RectangleF(0, 0, ClientSize.Width, ClientSize.Height),
                主题.CardBorderColor, 卡片边框宽度)
        End If
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

    Private Shared Function ResolveDpiScale(anchor As Control) As Single
        ' Popup 构造时自身句柄通常还没创建；优先继承锚点或活动窗口 DPI。
        If anchor IsNot Nothing AndAlso Not anchor.IsDisposed Then Return V3_DpiContext.FromControl(anchor).Scale
        Dim active = Form.ActiveForm
        If active IsNot Nothing AndAlso Not active.IsDisposed Then Return V3_DpiContext.FromControl(active).Scale
        Return 1.0F
    End Function

    Private Function 缩放逻辑尺寸(value As Integer) As Integer
        Return Math.Max(0, CInt(Math.Round(value * SC, MidpointRounding.AwayFromZero)))
    End Function

    Private Function 缩放边框宽度(value As Integer) As Integer
        Return Math.Max(0, CInt(Math.Round(value * SC)))
    End Function

#End Region

#Region "关闭"

    Private Sub 执行关闭()
        If 已关闭 OrElse 正在关闭动画 Then Return
        自动关闭计时器?.Stop()
        ' 启动渐出动画
        正在展开动画 = False
        正在关闭动画 = True
        动画秒表.Restart()
        If 动画计时器 Is Nothing Then
            动画计时器 = 创建动画计时器()
        End If
        动画计时器.Start()
    End Sub

    Protected Overrides Sub OnClick(e As EventArgs)
        MyBase.OnClick(e)
        执行关闭()
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        已关闭 = True
        MyBase.OnFormClosing(e)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            Application.RemoveMessageFilter(Me)
            动画计时器?.Stop()
            动画计时器?.Dispose()
            自动关闭计时器?.Stop()
            自动关闭计时器?.Dispose()
            消息字体?.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

#End Region

End Class

#End Region
