Imports System.Runtime.InteropServices
Imports System.Text

''' <summary>
''' 输入法 IME 相关的 Win32 P/Invoke 声明与辅助方法。
''' </summary>
Friend Module ImeHelper

#Region "Win32 常量"
    Friend Const WM_CHAR As Integer = &H102
    Friend Const WM_IME_COMPOSITION As Integer = &H10F
    Friend Const WM_IME_STARTCOMPOSITION As Integer = &H10D
    Friend Const WM_IME_ENDCOMPOSITION As Integer = &H10E
    Friend Const WM_GETDLGCODE As Integer = &H87
    Friend Const GCS_RESULTSTR As Integer = &H800
    Friend Const CFS_POINT As Integer = &H2
    Friend Const DLGC_WANTCHARS As Integer = &H80
    Friend Const DLGC_WANTALLKEYS As Integer = &H4
    Friend Const IACE_DEFAULT As Integer = &H10
#End Region

#Region "P/Invoke"
    <DllImport("imm32.dll")>
    Private Function ImmGetContext(hWnd As IntPtr) As IntPtr
    End Function
    <DllImport("imm32.dll")>
    Private Function ImmReleaseContext(hWnd As IntPtr, hIMC As IntPtr) As Boolean
    End Function
    <DllImport("imm32.dll", EntryPoint:="ImmGetCompositionStringW")>
    Private Function ImmGetCompositionBytes(hIMC As IntPtr, dwIndex As Integer, lpBuf As Byte(), dwBufLen As Integer) As Integer
    End Function
    <DllImport("imm32.dll")>
    Private Function ImmSetCompositionWindow(hIMC As IntPtr, ByRef lpCompForm As COMPOSITIONFORM) As Boolean
    End Function
    <DllImport("imm32.dll")>
    Private Function ImmAssociateContextEx(hWnd As IntPtr, hIMC As IntPtr, dwFlags As Integer) As Boolean
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Private Structure COMPOSITIONFORM
        Public dwStyle As Integer
        Public ptCurrentPos As Point
        Public rcArea As Rectangle
    End Structure
#End Region

#Region "辅助方法"
    ''' <summary>
    ''' 从 IME 获取已确认的合成字符串，无结果时返回 Nothing。
    ''' </summary>
    Friend Function GetResultString(hWnd As IntPtr) As String
        Dim hIMC As IntPtr = ImmGetContext(hWnd)
        If hIMC = IntPtr.Zero Then Return Nothing
        Try
            Dim byteLen As Integer = ImmGetCompositionBytes(hIMC, GCS_RESULTSTR, Nothing, 0)
            If byteLen > 0 Then
                Dim buf(byteLen - 1) As Byte
                Dim unused = ImmGetCompositionBytes(hIMC, GCS_RESULTSTR, buf, byteLen)
                Return Encoding.Unicode.GetString(buf, 0, byteLen)
            End If
            Return Nothing
        Finally
            ImmReleaseContext(hWnd, hIMC)
        End Try
    End Function

    ''' <summary>
    ''' 设置 IME 合成窗口位置。
    ''' </summary>
    Friend Sub SetCompositionPosition(hWnd As IntPtr, x As Integer, y As Integer)
        Dim hIMC As IntPtr = ImmGetContext(hWnd)
        If hIMC = IntPtr.Zero Then Return
        Try
            Dim cf As New COMPOSITIONFORM With {
                .dwStyle = CFS_POINT,
                .ptCurrentPos = New Point(x, y)
            }
            ImmSetCompositionWindow(hIMC, cf)
        Finally
            ImmReleaseContext(hWnd, hIMC)
        End Try
    End Sub

    ''' <summary>
    ''' 激活默认 IME 关联。
    ''' </summary>
    Friend Sub AssociateDefault(hWnd As IntPtr)
        ImmAssociateContextEx(hWnd, IntPtr.Zero, IACE_DEFAULT)
    End Sub
#End Region

End Module
