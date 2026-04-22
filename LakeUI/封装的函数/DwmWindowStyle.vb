Imports System.Runtime.InteropServices

''' <summary>
''' 使用 DWM (Desktop Window Manager) 设置目标窗口的主题外观，包括亮色/暗色模式以及窗口圆角样式。
''' 需要 Windows 11 Build 22000 及以上版本才能生效。
''' </summary>
Public Class DwmWindowStyle

#Region "Win32"

    Private Const DWMWA_USE_IMMERSIVE_DARK_MODE As Integer = 20
    Private Const DWMWA_WINDOW_CORNER_PREFERENCE As Integer = 33

    <DllImport("dwmapi.dll", EntryPoint:="DwmSetWindowAttribute")>
    Private Shared Function DwmSetWindowAttributeInt(hwnd As IntPtr, dwAttribute As Integer,
                                                      ByRef pvAttribute As Integer, cbAttribute As Integer) As Integer
    End Function

    <DllImport("dwmapi.dll", EntryPoint:="DwmSetWindowAttribute")>
    Private Shared Function DwmSetWindowAttributeBool(hwnd As IntPtr, dwAttribute As Integer,
                                                       <MarshalAs(UnmanagedType.Bool)> ByRef pvAttribute As Boolean, cbAttribute As Integer) As Integer
    End Function

#End Region

    ''' <summary>
    ''' 窗口圆角模式
    ''' </summary>
    Public Enum CornerMode
        ''' <summary>
        ''' 跟随系统默认行为
        ''' </summary>
        [Default] = 0
        ''' <summary>
        ''' 直角（不圆角）
        ''' </summary>
        Square = 1
        ''' <summary>
        ''' 圆角
        ''' </summary>
        Round = 2
        ''' <summary>
        ''' 小圆角
        ''' </summary>
        RoundSmall = 3
    End Enum

    ''' <summary>
    ''' 设置目标窗口的亮色/暗色模式。设置为 True 时窗口标题栏和边框使用暗色主题，False 时使用亮色主题。
    ''' </summary>
    ''' <param name="windowHandle">窗口句柄，通常传入 Me.Handle</param>
    ''' <param name="isDarkMode">True 为暗色模式，False 为亮色模式</param>
    ''' <returns>返回 HRESULT 值，0 表示成功。</returns>
    Public Shared Function SetDarkMode(windowHandle As IntPtr, isDarkMode As Boolean) As Integer
        Dim value As Boolean = isDarkMode
        Return DwmSetWindowAttributeBool(windowHandle, DWMWA_USE_IMMERSIVE_DARK_MODE, value, Marshal.SizeOf(Of Boolean)())
    End Function

    ''' <summary>
    ''' 设置目标窗口的圆角样式。
    ''' </summary>
    ''' <param name="windowHandle">窗口句柄，通常传入 Me.Handle</param>
    ''' <param name="mode">圆角模式</param>
    ''' <returns>返回 HRESULT 值，0 表示成功。</returns>
    Public Shared Function SetCornerMode(windowHandle As IntPtr, mode As CornerMode) As Integer
        Dim value As Integer = CInt(mode)
        Return DwmSetWindowAttributeInt(windowHandle, DWMWA_WINDOW_CORNER_PREFERENCE, value, 4)
    End Function

End Class
