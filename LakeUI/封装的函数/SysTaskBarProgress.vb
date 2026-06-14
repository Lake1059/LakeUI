Imports System.Runtime.InteropServices

''' <summary>
''' 设置 Windows 任务栏按钮上的进度显示，支持绿色 (Normal)、红色 (Error)、黄色 (Paused) 三种颜色模式。
''' </summary>
Public Class SysTaskBarProgress

#Region "COM 接口定义"

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
        Sub SetProgressState(hwnd As IntPtr, state As TaskBarProgressState)
        Sub RegisterTab(hwndTab As IntPtr, hwndMDI As IntPtr)
        Sub UnregisterTab(hwndTab As IntPtr)
        Sub SetTabOrder(hwndTab As IntPtr, hwndInsertBefore As IntPtr)
        Sub SetTabActive(hwndTab As IntPtr, hwndMDI As IntPtr, tbatFlags As UInteger)
        Sub ThumbBarAddButtons(hwnd As IntPtr, cButtons As UInteger, <MarshalAs(UnmanagedType.LPArray)> pButton As IntPtr())
        Sub ThumbBarUpdateButtons(hwnd As IntPtr, cButtons As UInteger, <MarshalAs(UnmanagedType.LPArray)> pButton As IntPtr())
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

#End Region

    ''' <summary>
    ''' 任务栏进度状态
    ''' </summary>
    Public Enum TaskBarProgressState
        ''' <summary>
        ''' 不显示进度
        ''' </summary>
        NoProgress = 0
        ''' <summary>
        ''' 不确定的进度（循环动画，无具体百分比）
        ''' </summary>
        Indeterminate = 1
        ''' <summary>
        ''' 正常进度（绿色）
        ''' </summary>
        Normal = 2
        ''' <summary>
        ''' 错误状态（红色）
        ''' </summary>
        [Error] = 4
        ''' <summary>
        ''' 暂停状态（黄色）
        ''' </summary>
        Paused = 8
    End Enum

    Private Shared ReadOnly _instance As ITaskbarList3 = CType(New TaskbarInstance(), ITaskbarList3)

    ''' <summary>
    ''' 设置任务栏进度的状态（颜色模式）。
    ''' </summary>
    ''' <param name="windowHandle">窗口句柄，通常传入 Me.Handle</param>
    ''' <param name="state">进度状态</param>
    Public Shared Sub SetState(windowHandle As IntPtr, state As TaskBarProgressState)
        _instance.SetProgressState(windowHandle, state)
    End Sub

    ''' <summary>
    ''' 设置任务栏进度的值。
    ''' </summary>
    ''' <param name="windowHandle">窗口句柄，通常传入 Me.Handle</param>
    ''' <param name="completed">当前完成量</param>
    ''' <param name="total">总量</param>
    Public Shared Sub SetValue(windowHandle As IntPtr, completed As ULong, total As ULong)
        _instance.SetProgressValue(windowHandle, completed, total)
    End Sub

    ''' <summary>
    ''' 设置任务栏进度（同时设置状态和值）。
    ''' </summary>
    ''' <param name="windowHandle">窗口句柄，通常传入 Me.Handle</param>
    ''' <param name="state">进度状态</param>
    ''' <param name="completed">当前完成量</param>
    ''' <param name="total">总量</param>
    Public Shared Sub SetProgress(windowHandle As IntPtr, state As TaskBarProgressState, completed As ULong, total As ULong)
        _instance.SetProgressState(windowHandle, state)
        _instance.SetProgressValue(windowHandle, completed, total)
    End Sub

    ''' <summary>
    ''' 清除任务栏进度显示。
    ''' </summary>
    ''' <param name="windowHandle">窗口句柄，通常传入 Me.Handle</param>
    Public Shared Sub Clear(windowHandle As IntPtr)
        _instance.SetProgressState(windowHandle, TaskBarProgressState.NoProgress)
    End Sub

End Class
