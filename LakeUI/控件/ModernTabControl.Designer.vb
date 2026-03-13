<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class ModernTabControl
    Inherits System.Windows.Forms.UserControl

    '重写释放以清理组件列表。
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing Then
                停止动画驱动()
                _动画计时器?.Dispose()
                If components IsNot Nothing Then
                    components.Dispose()
                End If
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Windows 窗体设计器所必需的
    Private components As System.ComponentModel.IContainer

    '注意: 以下过程是 Windows 窗体设计器所必需的
    '可以使用 Windows 窗体设计器修改它。  
    '不要使用代码编辑器修改它。
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        SuspendLayout()
        components = New System.ComponentModel.Container()
        AutoScaleMode = AutoScaleMode.Dpi
        DoubleBuffered = True
        Name = "ModernTabControl"
        Size = New Size(300, 300)
        ResumeLayout(False)
    End Sub

End Class
