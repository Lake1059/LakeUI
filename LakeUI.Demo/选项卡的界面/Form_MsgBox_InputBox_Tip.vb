Public Class Form_MsgBox_InputBox_Tip
    Private Sub ModernButton5_Click(sender As Object, e As EventArgs) Handles ModernButton5.Click
        ExMsgBox(Form1, "这里是内容", MsgBoxStyle.Information + MsgBoxStyle.YesNoCancel)
    End Sub

    Private Sub ModernButton6_Click(sender As Object, e As EventArgs) Handles ModernButton6.Click
        Dim 消息 As String = "这是一条带有自定义按钮的消息。" & vbCrLf & "你可以点击「复制」将内容复制到剪贴板，或选择「确认」/「取消」关闭对话框。"

        Dim 按钮列表 = New List(Of ExMsgBoxButton) From {
            New ExMsgBoxButton("复制内容", False, Function(args)
                                                  Clipboard.SetText(args.Prompt)
                                                  Return False  ' 保持对话框打开
                                              End Function),
            New ExMsgBoxButton("取消"),
            New ExMsgBoxButton("确认", True)
        }

        Dim 结果 As Integer = ExMsgBox(消息, 按钮列表, "自定义按钮示例", MsgBoxStyle.Information, Form1)

        Select Case 结果
            Case 2 : ExFloatingTip("你点击了【确认】")
            Case 1 : ExFloatingTip("你点击了【取消】")
            Case -1 : ExFloatingTip("你关闭了对话框")
        End Select
    End Sub

    Private Sub ModernButton8_Click(sender As Object, e As EventArgs) Handles ModernButton8.Click
        ExOverlayMsgBox("这里是内容", MsgBoxStyle.Information + MsgBoxStyle.OkOnly)
    End Sub

    Private Sub ModernButton7_Click(sender As Object, e As EventArgs) Handles ModernButton7.Click
        ExOverlayMsgBox(Form1, "这里是内容", MsgBoxStyle.Information + MsgBoxStyle.OkOnly)
    End Sub

    Private Sub ModernButton10_Click(sender As Object, e As EventArgs) Handles ModernButton10.Click
        ExFloatingBox("这里是内容", MsgBoxStyle.YesNo)
    End Sub

    Private Sub ModernButton9_Click(sender As Object, e As EventArgs) Handles ModernButton9.Click
        ExInputBox(Form1, "这里是内容")
    End Sub

    Private Sub ModernButton12_Click(sender As Object, e As EventArgs) Handles ModernButton12.Click
        ExFloatingTip("这里是内容")
    End Sub

    Private Sub ModernButton11_Click(sender As Object, e As EventArgs) Handles ModernButton11.Click
        ExFloatingTip(sender, "这里是内容")
    End Sub
End Class