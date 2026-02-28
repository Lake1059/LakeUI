Imports System.ComponentModel

''' <summary>
''' 不会抢占焦点的顶层弹出窗口基类。
''' </summary>
<ToolboxItem(False)>
Friend Class PopupForm
    Inherits Form

    Private Const WM_MOUSEACTIVATE As Integer = &H21
    Private Const MA_NOACTIVATE As Integer = &H3

    Protected Sub New()
        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        StartPosition = FormStartPosition.Manual
    End Sub

    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim cp = MyBase.CreateParams
            ' WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE
            cp.ExStyle = cp.ExStyle Or &H80 Or &H8 Or &H8000000
            Return cp
        End Get
    End Property

    Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
        Get
            Return True
        End Get
    End Property

    Protected Overrides Sub WndProc(ByRef m As Message)
        If m.Msg = WM_MOUSEACTIVATE Then
            m.Result = New IntPtr(MA_NOACTIVATE)
            Return
        End If
        MyBase.WndProc(m)
    End Sub

End Class
