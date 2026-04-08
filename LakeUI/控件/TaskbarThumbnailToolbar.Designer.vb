<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class TaskbarThumbnailToolbar
    Inherits System.ComponentModel.Component

    Public Sub New()
        InitializeComponent()
    End Sub

    Public Sub New(container As System.ComponentModel.IContainer)
        container.Add(Me)
        InitializeComponent()
    End Sub

    '重写释放以清理组件列表。
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing Then
                释放资源()
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

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        components = New System.ComponentModel.Container()
    End Sub

End Class
