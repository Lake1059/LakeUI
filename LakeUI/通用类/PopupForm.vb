Imports System.ComponentModel

''' <summary>
''' 不会抢占焦点的顶层弹出窗口基类。
''' </summary>
''' <remarks>
''' 用于 tooltip、下拉层、临时浮层等“看起来像控件但实际是顶层窗口”的 UI。
'''
''' 调用原则：
''' • 派生类应自行处理绘制和大小，不要依赖父控件布局系统。
''' • 显示前设置 Location/Size；本类只保证不激活、不进任务栏、不抢焦点。
''' • 如果弹出层需要毛玻璃，使用 <see cref="PopupBackdropRenderer"/>，不要复用宿主窗口的 PaintScope。
'''
''' 坑点：
''' • WS_EX_TOPMOST 会让 popup 压在普通窗口之上；派生类关闭/隐藏时必须及时释放或 Hide。
''' • WM_MOUSEACTIVATE 返回 MA_NOACTIVATE 是为了保持宿主控件焦点，别在派生类里轻易改掉。
''' </remarks>
<ToolboxItem(False)>
Public Class PopupForm
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
