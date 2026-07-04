Imports System.Runtime.InteropServices

''' <summary>
''' V3_DpiContext 是新主版本控件迁移使用的非渲染 DPI 上下文。
''' 它不持有 GPU 对象，不创建 D3D/D2D 资源，也不替代任何 D3D_ 缓存类。
''' </summary>
Friend NotInheritable Class V3_DpiContext
    <DllImport("user32.dll")>
    Private Shared Function GetDpiForWindow(hwnd As IntPtr) As UInteger
    End Function

    Public Sub New(dpi As Integer)
        Me.Dpi = Math.Max(1, dpi)
        Me.Scale = CSng(Me.Dpi) / 96.0F
    End Sub

    Public ReadOnly Property Dpi As Integer
    Public ReadOnly Property Scale As Single

    ''' <summary>
    ''' 从控件所属窗口解析 DPI。该方法只读取 Win32/WinForms 状态，不触碰 GPU 资源。
    ''' </summary>
    Public Shared Function FromControl(control As Control) As V3_DpiContext
        If control IsNot Nothing AndAlso Not control.IsDisposed Then
            Try
                If control.IsHandleCreated Then
                    Dim dpi = CInt(GetDpiForWindow(control.Handle))
                    If dpi > 0 Then Return New V3_DpiContext(dpi)
                End If
            Catch
            End Try

            Try
                If control.DeviceDpi > 0 Then Return New V3_DpiContext(control.DeviceDpi)
            Catch
            End Try
        End If

        Return New V3_DpiContext(96)
    End Function
End Class
