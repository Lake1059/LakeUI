Imports System.ComponentModel
Imports System.Runtime.InteropServices

''' <summary>
''' 任务栏缩略图工具栏组件（无界面），可在窗口的任务栏缩略图预览下方添加最多 7 个工具栏按钮。
''' 完全基于 Win32 ITaskbarList3 COM 接口实现，无需任何第三方依赖。
''' 在窗体的 Load 或 Shown 事件中调用 <see cref="Attach"/> 即可启用。
''' </summary>
<DesignerCategory("Component")>
<DefaultEvent("ButtonClick")>
Public Class TaskbarThumbnailToolbar

#Region "Win32 定义"

    Private Const WM_COMMAND As Integer = &H111
    Private Const THBN_CLICKED As Integer = &H1800
    Private Const 最大按钮数量 As Integer = 7

    Private Const THB_BITMAP As UInteger = &H1UI
    Private Const THB_ICON As UInteger = &H2UI
    Private Const THB_TOOLTIP As UInteger = &H4UI
    Private Const THB_FLAGS As UInteger = &H8UI

    Private Const THBF_ENABLED As UInteger = &H0UI
    Private Const THBF_DISABLED As UInteger = &H1UI
    Private Const THBF_DISMISSONCLICK As UInteger = &H2UI
    Private Const THBF_NOBACKGROUND As UInteger = &H4UI
    Private Const THBF_HIDDEN As UInteger = &H8UI
    Private Const THBF_NONINTERACTIVE As UInteger = &H10UI

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function RegisterWindowMessage(lpString As String) As UInteger
    End Function

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
    Private Structure THUMBBUTTON
        Public dwMask As UInteger
        Public iId As UInteger
        Public iBitmap As UInteger
        Public hIcon As IntPtr
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=260)>
        Public szTip As String
        Public dwFlags As UInteger
    End Structure

    <ComImport>
    <Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")>
    <InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
    Private Interface ITaskbarList3
        ' ITaskbarList
        Sub HrInit()
        Sub AddTab(hwnd As IntPtr)
        Sub DeleteTab(hwnd As IntPtr)
        Sub ActivateTab(hwnd As IntPtr)
        Sub SetActiveAlt(hwnd As IntPtr)
        ' ITaskbarList2
        Sub MarkFullscreenWindow(hwnd As IntPtr, <MarshalAs(UnmanagedType.Bool)> fFullscreen As Boolean)
        ' ITaskbarList3
        Sub SetProgressValue(hwnd As IntPtr, ullCompleted As ULong, ullTotal As ULong)
        Sub SetProgressState(hwnd As IntPtr, state As Integer)
        Sub RegisterTab(hwndTab As IntPtr, hwndMDI As IntPtr)
        Sub UnregisterTab(hwndTab As IntPtr)
        Sub SetTabOrder(hwndTab As IntPtr, hwndInsertBefore As IntPtr)
        Sub SetTabActive(hwndTab As IntPtr, hwndMDI As IntPtr, tbatFlags As UInteger)
        Sub ThumbBarAddButtons(hwnd As IntPtr, cButtons As UInteger, <MarshalAs(UnmanagedType.LPArray, SizeParamIndex:=1)> pButton As THUMBBUTTON())
        Sub ThumbBarUpdateButtons(hwnd As IntPtr, cButtons As UInteger, <MarshalAs(UnmanagedType.LPArray, SizeParamIndex:=1)> pButton As THUMBBUTTON())
        Sub ThumbBarSetImageList(hwnd As IntPtr, himl As IntPtr)
        Sub SetOverlayIcon(hwnd As IntPtr, hIcon As IntPtr, <MarshalAs(UnmanagedType.LPWStr)> pszDescription As String)
        Sub SetThumbnailTooltip(hwnd As IntPtr, <MarshalAs(UnmanagedType.LPWStr)> pszTip As String)
        Sub SetThumbnailClip(hwnd As IntPtr, prcClip As IntPtr)
    End Interface

    <ComImport>
    <Guid("56fdf344-fd6d-11d0-958a-006097c9a090")>
    <ClassInterface(ClassInterfaceType.None)>
    Private Class TaskbarInstance
    End Class

    <DllImport("user32.dll")>
    Private Shared Function DestroyIcon(hIcon As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

#End Region

#Region "NativeWindow 消息拦截器"

    Private Class 消息拦截窗口
        Inherits NativeWindow

        Private ReadOnly _所有者 As TaskbarThumbnailToolbar
        Private ReadOnly _任务栏按钮创建消息 As UInteger

        Public Sub New(owner As TaskbarThumbnailToolbar, hWnd As IntPtr)
            _所有者 = owner
            _任务栏按钮创建消息 = RegisterWindowMessage("TaskbarButtonCreated")
            AssignHandle(hWnd)
        End Sub

        Protected Overrides Sub WndProc(ByRef m As Message)
            If m.Msg = WM_COMMAND Then
                Dim hiWord As Integer = CInt((m.WParam.ToInt64() >> 16) And &HFFFF)
                Dim loWord As Integer = CInt(m.WParam.ToInt64() And &HFFFF)
                If hiWord = THBN_CLICKED Then
                    _所有者.处理按钮点击(loWord)
                End If
            ElseIf CUInt(m.Msg) = _任务栏按钮创建消息 Then
                _所有者.处理任务栏按钮创建()
            End If
            MyBase.WndProc(m)
        End Sub
    End Class

#End Region

#Region "事件"

    ''' <summary>
    ''' 当缩略图工具栏上的按钮被点击时发生。
    ''' </summary>
    Public Event ButtonClick As EventHandler(Of ThumbnailButtonClickEventArgs)

#End Region

#Region "内部字段"

    Private _任务栏实例 As ITaskbarList3
    Private _消息拦截窗口实例 As 消息拦截窗口
    Private _目标窗体 As Form
    Private _按钮已注册 As Boolean = False
    Private ReadOnly _按钮列表 As New List(Of ThumbnailToolbarButton)

#End Region

#Region "属性"

    ''' <summary>
    ''' 获取缩略图工具栏的按钮集合（最多 7 个按钮）。
    ''' 修改按钮属性后需调用 <see cref="UpdateButtons"/> 以应用更改。
    ''' </summary>
    <Category("LakeUI"), Description("缩略图工具栏按钮集合，最多支持 7 个按钮。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Content), Browsable(True)>
    Public ReadOnly Property Buttons As List(Of ThumbnailToolbarButton)
        Get
            Return _按钮列表
        End Get
    End Property

#End Region

#Region "公共方法"

    ''' <summary>
    ''' 将缩略图工具栏附加到目标窗体。建议在 Form.Load 中调用。
    ''' 如果窗体句柄尚未创建，将自动延迟到句柄创建后初始化。
    ''' </summary>
    ''' <param name="targetForm">要附加缩略图工具栏的目标窗体</param>
    Public Sub Attach(targetForm As Form)
#If NET5_0 Then
        If targetForm Is Nothing Then Throw New ArgumentNullException(NameOf(targetForm))
#Else
        ArgumentNullException.ThrowIfNull(targetForm)
#End If
        If _目标窗体 IsNot Nothing Then Detach()
        _目标窗体 = targetForm
        初始化()
    End Sub

    ''' <summary>
    ''' 从当前附加的窗体分离，释放消息拦截和 COM 资源。
    ''' </summary>
    Public Sub Detach()
        卸载()
    End Sub

    ''' <summary>
    ''' 更新所有按钮的状态到任务栏。在修改按钮属性后调用此方法以应用更改。
    ''' </summary>
    Public Sub UpdateButtons()
        If Not _按钮已注册 OrElse _目标窗体 Is Nothing OrElse _目标窗体.IsDisposed Then Return
        If Not _目标窗体.IsHandleCreated Then Return
        Try
            Dim buttons = 构建THUMBBUTTON数组()
            _任务栏实例.ThumbBarUpdateButtons(_目标窗体.Handle, CUInt(buttons.Length), buttons)
        Catch
        End Try
    End Sub

    ''' <summary>
    ''' 设置指定按钮的启用状态并立即更新到任务栏。
    ''' </summary>
    ''' <param name="index">按钮在集合中的索引</param>
    ''' <param name="enabled">是否启用</param>
    Public Sub SetButtonEnabled(index As Integer, enabled As Boolean)
        If index < 0 OrElse index >= _按钮列表.Count Then Return
        _按钮列表(index).Enabled = enabled
        UpdateButtons()
    End Sub

    ''' <summary>
    ''' 设置指定按钮的可见性并立即更新到任务栏。
    ''' </summary>
    ''' <param name="index">按钮在集合中的索引</param>
    ''' <param name="visible">是否可见</param>
    Public Sub SetButtonVisible(index As Integer, visible As Boolean)
        If index < 0 OrElse index >= _按钮列表.Count Then Return
        _按钮列表(index).Visible = visible
        UpdateButtons()
    End Sub

    ''' <summary>
    ''' 设置指定按钮的图标并立即更新到任务栏。
    ''' </summary>
    ''' <param name="index">按钮在集合中的索引</param>
    ''' <param name="icon">新图标</param>
    Public Sub SetButtonIcon(index As Integer, icon As Icon)
        If index < 0 OrElse index >= _按钮列表.Count Then Return
        _按钮列表(index).ButtonIcon = icon
        UpdateButtons()
    End Sub

    ''' <summary>
    ''' 设置指定按钮的提示文本并立即更新到任务栏。
    ''' </summary>
    ''' <param name="index">按钮在集合中的索引</param>
    ''' <param name="tooltip">新的提示文本</param>
    Public Sub SetButtonTooltip(index As Integer, tooltip As String)
        If index < 0 OrElse index >= _按钮列表.Count Then Return
        _按钮列表(index).Tooltip = tooltip
        UpdateButtons()
    End Sub

#End Region

#Region "图标绘制"

    ''' <summary>
    ''' 使用 GDI+ 矢量自绘创建缩略图工具栏图标。返回的 Icon 由调用方负责生命周期管理。
    ''' 所有图标均为纯几何图形绘制，不依赖任何字体或系统图标，保证在所有 Windows 版本上显示一致。
    ''' </summary>
    ''' <param name="icon">要创建的预置图标</param>
    ''' <param name="size">图标像素尺寸，默认 16（适合缩略图工具栏按钮）</param>
    ''' <param name="color">图标颜色，默认白色</param>
    ''' <returns>绘制完成的图标</returns>
    Public Shared Function CreateIcon(icon As ThumbnailToolbarIcon, Optional size As Integer = 16, Optional color As Color = Nothing) As Icon
        If color.IsEmpty Then color = Color.White
        Using bmp As New Bitmap(size, size, Imaging.PixelFormat.Format32bppArgb)
            Using g = Graphics.FromImage(bmp)
                g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
                绘制预置图标(g, icon, CSng(size), color)
            End Using
            Dim hIcon = bmp.GetHicon()
            Dim managedIcon = DirectCast(Drawing.Icon.FromHandle(hIcon).Clone(), Drawing.Icon)
            DestroyIcon(hIcon)
            Return managedIcon
        End Using
    End Function

    Private Shared Sub 绘制预置图标(g As Graphics, icon As ThumbnailToolbarIcon, s As Single, c As Color)
        Using b As New SolidBrush(c)
            Select Case icon

                Case ThumbnailToolbarIcon.Play
                    g.FillPolygon(b, New PointF() {
                        New PointF(s * 0.22F, s * 0.1F),
                        New PointF(s * 0.22F, s * 0.9F),
                        New PointF(s * 0.88F, s * 0.5F)})

                Case ThumbnailToolbarIcon.Pause
                    g.FillRectangle(b, s * 0.18F, s * 0.1F, s * 0.2F, s * 0.8F)
                    g.FillRectangle(b, s * 0.62F, s * 0.1F, s * 0.2F, s * 0.8F)

                Case ThumbnailToolbarIcon.[Stop]
                    g.FillRectangle(b, s * 0.14F, s * 0.14F, s * 0.72F, s * 0.72F)

                Case ThumbnailToolbarIcon.Previous
                    g.FillRectangle(b, s * 0.1F, s * 0.12F, s * 0.2F, s * 0.76F)
                    g.FillPolygon(b, New PointF() {
                        New PointF(s * 0.9F, s * 0.12F),
                        New PointF(s * 0.9F, s * 0.88F),
                        New PointF(s * 0.38F, s * 0.5F)})

                Case ThumbnailToolbarIcon.[Next]
                    g.FillPolygon(b, New PointF() {
                        New PointF(s * 0.1F, s * 0.12F),
                        New PointF(s * 0.1F, s * 0.88F),
                        New PointF(s * 0.62F, s * 0.5F)})
                    g.FillRectangle(b, s * 0.7F, s * 0.12F, s * 0.2F, s * 0.76F)

                Case ThumbnailToolbarIcon.Rewind
                    g.FillPolygon(b, New PointF() {
                        New PointF(s * 0.5F, s * 0.1F),
                        New PointF(s * 0.5F, s * 0.9F),
                        New PointF(s * 0.09F, s * 0.5F)})
                    g.FillPolygon(b, New PointF() {
                        New PointF(s * 0.91F, s * 0.1F),
                        New PointF(s * 0.91F, s * 0.9F),
                        New PointF(s * 0.5F, s * 0.5F)})

                Case ThumbnailToolbarIcon.FastForward
                    g.FillPolygon(b, New PointF() {
                        New PointF(s * 0.09F, s * 0.1F),
                        New PointF(s * 0.09F, s * 0.9F),
                        New PointF(s * 0.5F, s * 0.5F)})
                    g.FillPolygon(b, New PointF() {
                        New PointF(s * 0.5F, s * 0.1F),
                        New PointF(s * 0.5F, s * 0.9F),
                        New PointF(s * 0.91F, s * 0.5F)})

            End Select
        End Using
    End Sub

#End Region

#Region "内部方法"

    Private Sub 初始化()
        Try
            _任务栏实例 = CType(New TaskbarInstance(), ITaskbarList3)
            _任务栏实例.HrInit()
        Catch
            Return
        End Try

        If _目标窗体.IsHandleCreated Then
            _消息拦截窗口实例 = New 消息拦截窗口(Me, _目标窗体.Handle)
            注册按钮()
        Else
            AddHandler _目标窗体.HandleCreated, AddressOf 窗体句柄已创建
        End If
        AddHandler _目标窗体.HandleDestroyed, AddressOf 窗体句柄已销毁
    End Sub

    Private Sub 卸载()
        If _按钮已注册 AndAlso _目标窗体 IsNot Nothing AndAlso _目标窗体.IsHandleCreated AndAlso _任务栏实例 IsNot Nothing Then
            Try
                Dim count As Integer = Math.Min(_按钮列表.Count, 最大按钮数量)
                Dim buttons(count - 1) As THUMBBUTTON
                For i As Integer = 0 To count - 1
                    buttons(i) = New THUMBBUTTON With {
                        .iId = CUInt(i),
                        .dwMask = THB_FLAGS,
                        .dwFlags = THBF_HIDDEN Or THBF_DISABLED Or THBF_NONINTERACTIVE
                    }
                Next
                _任务栏实例.ThumbBarUpdateButtons(_目标窗体.Handle, CUInt(count), buttons)
            Catch
            End Try
        End If
        If _目标窗体 IsNot Nothing Then
            RemoveHandler _目标窗体.HandleCreated, AddressOf 窗体句柄已创建
            RemoveHandler _目标窗体.HandleDestroyed, AddressOf 窗体句柄已销毁
        End If
        _消息拦截窗口实例?.ReleaseHandle()
        _消息拦截窗口实例 = Nothing
        _按钮已注册 = False
        _目标窗体 = Nothing
        If _任务栏实例 IsNot Nothing Then
            Try
                Marshal.ReleaseComObject(_任务栏实例)
            Catch
            End Try
            _任务栏实例 = Nothing
        End If
    End Sub

    Private Sub 窗体句柄已创建(sender As Object, e As EventArgs)
        If _目标窗体 Is Nothing OrElse Not _目标窗体.IsHandleCreated Then Return
        _消息拦截窗口实例 = New 消息拦截窗口(Me, _目标窗体.Handle)
        注册按钮()
    End Sub

    Private Sub 窗体句柄已销毁(sender As Object, e As EventArgs)
        _消息拦截窗口实例?.ReleaseHandle()
        _消息拦截窗口实例 = Nothing
        _按钮已注册 = False
    End Sub

    Private Sub 注册按钮()
        If _按钮已注册 OrElse _按钮列表.Count = 0 Then Return
        If _目标窗体 Is Nothing OrElse Not _目标窗体.IsHandleCreated Then Return
        If _任务栏实例 Is Nothing Then Return
        Dim buttons = 构建THUMBBUTTON数组()
        Dim hwnd = _目标窗体.Handle
        Dim count = CUInt(buttons.Length)
        Try
            _任务栏实例.ThumbBarAddButtons(hwnd, count, buttons)
        Catch
        End Try
        Try
            _任务栏实例.ThumbBarUpdateButtons(hwnd, count, buttons)
            _按钮已注册 = True
        Catch
        End Try
    End Sub

    Private Sub 处理任务栏按钮创建()
        _按钮已注册 = False
        注册按钮()
    End Sub

    Private Sub 处理按钮点击(buttonId As Integer)
        If buttonId >= 0 AndAlso buttonId < _按钮列表.Count Then
            Dim btn = _按钮列表(buttonId)
            btn.触发点击事件()
            RaiseEvent ButtonClick(Me, New ThumbnailButtonClickEventArgs(btn, buttonId))
        End If
    End Sub

    Private Function 构建THUMBBUTTON数组() As THUMBBUTTON()
        Dim count As Integer = Math.Min(_按钮列表.Count, 最大按钮数量)
        Dim result(count - 1) As THUMBBUTTON
        For i As Integer = 0 To count - 1
            result(i) = 转换按钮(_按钮列表(i), CUInt(i))
        Next
        Return result
    End Function

    Private Shared Function 转换按钮(btn As ThumbnailToolbarButton, id As UInteger) As THUMBBUTTON
        Dim tb As New THUMBBUTTON With {
            .iId = id,
            .dwMask = THB_ICON Or THB_TOOLTIP Or THB_FLAGS,
            .szTip = If(btn.Tooltip, "")
        }
        If btn.ButtonIcon IsNot Nothing Then
            tb.hIcon = btn.ButtonIcon.Handle
        End If
        Dim flags As UInteger = THBF_ENABLED
        If Not btn.Enabled Then flags = THBF_DISABLED
        If Not btn.Visible Then flags = flags Or THBF_HIDDEN
        If btn.DismissOnClick Then flags = flags Or THBF_DISMISSONCLICK
        If btn.NoBackground Then flags = flags Or THBF_NOBACKGROUND
        If btn.NonInteractive Then flags = flags Or THBF_NONINTERACTIVE
        tb.dwFlags = flags
        Return tb
    End Function

    Private Sub 释放资源()
        卸载()
    End Sub

#End Region

End Class

''' <summary>
''' 表示任务栏缩略图工具栏上的单个按钮。
''' </summary>
Public Class ThumbnailToolbarButton

    ''' <summary>当此按钮被点击时发生。</summary>
    Public Event Click As EventHandler

    ''' <summary>按钮图标，将直接映射为 Win32 HICON。</summary>
    Public Property ButtonIcon As Icon

    ''' <summary>鼠标悬停时显示的提示文本（最长 259 个字符）。</summary>
    Public Property Tooltip As String = ""

    ''' <summary>按钮是否启用。禁用的按钮显示为灰色且不可点击。</summary>
    Public Property Enabled As Boolean = True

    ''' <summary>按钮是否可见。不可见的按钮将被隐藏。</summary>
    Public Property Visible As Boolean = True

    ''' <summary>点击按钮后是否自动关闭缩略图预览。</summary>
    Public Property DismissOnClick As Boolean = False

    ''' <summary>是否不绘制按钮边框背景，仅显示图标。</summary>
    Public Property NoBackground As Boolean = False

    ''' <summary>按钮是否为非交互状态（显示但不响应用户操作，无悬停/按下视觉效果）。</summary>
    Public Property NonInteractive As Boolean = False

    ''' <summary>
    ''' 创建一个空的缩略图工具栏按钮。
    ''' </summary>
    Public Sub New()
    End Sub

    ''' <summary>
    ''' 使用指定的图标和提示文本创建缩略图工具栏按钮。
    ''' </summary>
    ''' <param name="icon">按钮图标</param>
    ''' <param name="tooltip">提示文本</param>
    Public Sub New(icon As Icon, tooltip As String)
        Me.ButtonIcon = icon
        Me.Tooltip = tooltip
    End Sub

    ''' <summary>
    ''' 使用指定的图标、提示文本和点击处理程序创建缩略图工具栏按钮。
    ''' </summary>
    ''' <param name="icon">按钮图标</param>
    ''' <param name="tooltip">提示文本</param>
    ''' <param name="clickHandler">点击事件处理程序</param>
    Public Sub New(icon As Icon, tooltip As String, clickHandler As EventHandler)
        Me.ButtonIcon = icon
        Me.Tooltip = tooltip
        If clickHandler IsNot Nothing Then AddHandler Click, clickHandler
    End Sub

    ''' <summary>
    ''' 使用预置图标和提示文本创建缩略图工具栏按钮。图标由控件内置矢量绘制生成。
    ''' </summary>
    ''' <param name="icon">预置图标</param>
    ''' <param name="tooltip">提示文本</param>
    Public Sub New(icon As ThumbnailToolbarIcon, tooltip As String)
        Me.ButtonIcon = TaskbarThumbnailToolbar.CreateIcon(icon)
        Me.Tooltip = tooltip
    End Sub

    ''' <summary>
    ''' 使用预置图标、提示文本和点击处理程序创建缩略图工具栏按钮。
    ''' </summary>
    ''' <param name="icon">预置图标</param>
    ''' <param name="tooltip">提示文本</param>
    ''' <param name="clickHandler">点击事件处理程序</param>
    Public Sub New(icon As ThumbnailToolbarIcon, tooltip As String, clickHandler As EventHandler)
        Me.ButtonIcon = TaskbarThumbnailToolbar.CreateIcon(icon)
        Me.Tooltip = tooltip
        If clickHandler IsNot Nothing Then AddHandler Click, clickHandler
    End Sub

    ''' <summary>
    ''' 使用预置图标、自定义颜色和提示文本创建缩略图工具栏按钮。
    ''' </summary>
    ''' <param name="icon">预置图标</param>
    ''' <param name="color">图标颜色</param>
    ''' <param name="tooltip">提示文本</param>
    ''' <param name="clickHandler">点击事件处理程序</param>
    Public Sub New(icon As ThumbnailToolbarIcon, color As Color, tooltip As String, Optional clickHandler As EventHandler = Nothing)
        Me.ButtonIcon = TaskbarThumbnailToolbar.CreateIcon(icon, 16, color)
        Me.Tooltip = tooltip
        If clickHandler IsNot Nothing Then AddHandler Click, clickHandler
    End Sub

    Friend Sub 触发点击事件()
        RaiseEvent Click(Me, EventArgs.Empty)
    End Sub

End Class

''' <summary>
''' 为 <see cref="TaskbarThumbnailToolbar.ButtonClick"/> 事件提供数据。
''' </summary>
Public Class ThumbnailButtonClickEventArgs
    Inherits EventArgs

    ''' <summary>被点击的按钮。</summary>
    Public ReadOnly Property Button As ThumbnailToolbarButton

    ''' <summary>被点击按钮在集合中的索引。</summary>
    Public ReadOnly Property ButtonIndex As Integer

    Public Sub New(button As ThumbnailToolbarButton, index As Integer)
        Me.Button = button
        Me.ButtonIndex = index
    End Sub

End Class

''' <summary>
''' 缩略图工具栏预置图标，所有图标均由 GDI+ 矢量自绘生成，不依赖任何字体或系统图标。
''' 可通过 <see cref="TaskbarThumbnailToolbar.CreateIcon"/> 渲染为 <see cref="Icon"/> 实例。
''' </summary>
Public Enum ThumbnailToolbarIcon
    ''' <summary>▶ 播放</summary>
    Play
    ''' <summary>⏸ 暂停</summary>
    Pause
    ''' <summary>⏹ 停止</summary>
    [Stop]
    ''' <summary>⏮ 上一曲</summary>
    Previous
    ''' <summary>⏭ 下一曲</summary>
    [Next]
    ''' <summary>⏪ 快退</summary>
    Rewind
    ''' <summary>⏩ 快进</summary>
    FastForward
End Enum
