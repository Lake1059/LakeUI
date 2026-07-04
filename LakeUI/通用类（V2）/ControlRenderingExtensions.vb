Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports System.Windows.Forms

''' <summary>
''' 控件渲染相关扩展。
''' </summary>
Friend Module ControlRenderingExtensions

    ''' <summary>
    ''' 为部分原生控件开启受保护的 WinForms 双缓冲。
    ''' </summary>
    <Extension>
    Public Sub DoubleBuffer(control As Control)
        If control Is Nothing Then Return

        Dim propertyInfo As PropertyInfo = control.GetType().GetProperty("DoubleBuffered", BindingFlags.Instance Or BindingFlags.NonPublic)
        propertyInfo?.SetValue(control, True, Nothing)
    End Sub

End Module
