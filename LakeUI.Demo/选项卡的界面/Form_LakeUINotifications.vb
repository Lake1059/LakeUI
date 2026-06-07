Imports System.Text
Imports LakeUI.Notifications

Public Class Form_LakeUINotifications
    Private Const 通知标签 As String = "lakeui-notifications-demo"
    Private Const 通知分组 As String = "lakeui-demo"

    Private _最后通知Id As UInteger
    Private _进度序号 As UInteger = 1
    Private _已绑定激活事件 As Boolean

    Private Sub Form_LakeUINotifications_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        If Not _已绑定激活事件 Then
            AddHandler LakeNotificationManager.NotificationActivated, AddressOf 通知被激活
            _已绑定激活事件 = True
        End If

        刷新状态("页面已加载")
    End Sub

    Private Sub Form_LakeUINotifications_FormClosed(sender As Object, e As FormClosedEventArgs) Handles MyBase.FormClosed
        If _已绑定激活事件 Then
            RemoveHandler LakeNotificationManager.NotificationActivated, AddressOf 通知被激活
            _已绑定激活事件 = False
        End If
    End Sub

    Private Sub ModernButton5_Click(sender As Object, e As EventArgs) Handles ModernButton5.Click
        Try
            _最后通知Id = LakeNotificationManager.Show(
                "LakeUI.Notifications",
                "这是一条基础 Windows App Notification。",
                通知标签 & "-basic",
                通知分组)

            记录("已发送基础通知，Id=" & _最后通知Id.ToString())
            刷新状态("基础通知已发送")
            ExFloatingTip("已发送基础通知")
        Catch ex As Exception
            操作失败("基础通知发送失败", ex)
        End Try
    End Sub

    Private Sub ModernButton6_Click(sender As Object, e As EventArgs) Handles ModernButton6.Click
        Try
            Dim 请求 = 创建完整通知请求()
            _最后通知Id = LakeNotificationManager.Show(请求)
            _进度序号 = 1

            记录("已发送完整交互通知，Id=" & _最后通知Id.ToString())
            刷新状态("完整通知已发送")
            ExFloatingTip("已发送完整交互通知")
        Catch ex As Exception
            操作失败("完整通知发送失败", ex)
        End Try
    End Sub

    Private Async Sub ModernButton7_Click(sender As Object, e As EventArgs) Handles ModernButton7.Click
        Try
            _进度序号 += 1UI
            Dim 阶段 As Integer = CInt(((_进度序号 - 1UI) Mod 5UI) + 1UI)
            Dim 百分比 As Double = 阶段 / 5.0
            Dim 结果 = Await LakeNotificationManager.UpdateProgressAsync(
                通知标签,
                通知分组,
                New LakeNotificationProgressUpdate With {
                    .SequenceNumber = _进度序号,
                    .Title = "LakeUI.Notifications",
                    .Status = "进度已更新",
                    .Value = 百分比,
                    .ValueStringOverride = CInt(百分比 * 100).ToString() & "%"
                })

            记录("更新进度结果：" & 结果.ToString() & "，当前值 " & CInt(百分比 * 100).ToString() & "%")
            刷新状态("进度更新：" & 结果.ToString())
            ExFloatingTip("进度更新：" & 结果.ToString())
        Catch ex As Exception
            操作失败("进度更新失败", ex)
        End Try
    End Sub

    Private Async Sub ModernButton8_Click(sender As Object, e As EventArgs) Handles ModernButton8.Click
        Try
            Dim 通知列表 = Await LakeNotificationManager.GetAllAsync()
            Dim 本组数量 As Integer = 0
            Dim 列表文本 As New StringBuilder()

            For Each 通知 In 通知列表
                If String.Equals(通知.Group, 通知分组, StringComparison.OrdinalIgnoreCase) Then
                    本组数量 += 1
                    If 列表文本.Length < 600 Then
                        列表文本.Append("#"c).Append(通知.Id).Append(" Tag=").Append(通知.Tag).AppendLine()
                    End If
                End If
            Next

            记录("通知中心共有 " & 通知列表.Count.ToString() & " 条，本组 " & 本组数量.ToString() & " 条。" & If(列表文本.Length > 0, vbCrLf & 列表文本.ToString().TrimEnd(), ""))
            刷新状态("已查询通知中心")
            ExFloatingTip("通知中心：" & 通知列表.Count.ToString() & " 条")
        Catch ex As Exception
            操作失败("查询通知中心失败", ex)
        End Try
    End Sub

    Private Async Sub ModernButton9_Click(sender As Object, e As EventArgs) Handles ModernButton9.Click
        Try
            Await LakeNotificationManager.RemoveByGroupAsync(通知分组)
            _最后通知Id = 0
            记录("已清除分组：" & 通知分组)
            刷新状态("已清除本组通知")
            ExFloatingTip("已清除本组通知")
        Catch ex As Exception
            操作失败("清除本组通知失败", ex)
        End Try
    End Sub

    Private Async Sub ModernButton10_Click(sender As Object, e As EventArgs) Handles ModernButton10.Click
        Try
            Await LakeNotificationManager.RemoveAllAsync()
            _最后通知Id = 0
            记录("已清除当前应用的全部通知")
            刷新状态("已清除全部通知")
            ExFloatingTip("已清除全部通知")
        Catch ex As Exception
            操作失败("清除全部通知失败", ex)
        End Try
    End Sub

    Private Sub ModernButton11_Click(sender As Object, e As EventArgs) Handles ModernButton11.Click
        Try
            Dim Payload = LakeNotificationManager.BuildPayload(创建完整通知请求())
            记录("完整通知 XML：" & vbCrLf & Payload)
            刷新状态("已生成 XML Payload")
            ExFloatingTip("已生成 XML Payload")
        Catch ex As Exception
            操作失败("生成 XML 失败", ex)
        End Try
    End Sub

    Private Async Sub ModernButton12_Click(sender As Object, e As EventArgs) Handles ModernButton12.Click
        Try
            If _最后通知Id = 0UI Then
                ExFloatingTip("没有可清除的最后通知")
                Return
            End If

            Await LakeNotificationManager.RemoveByIdAsync(_最后通知Id)
            记录("已清除最后通知，Id=" & _最后通知Id.ToString())
            _最后通知Id = 0
            刷新状态("已清除最后通知")
            ExFloatingTip("已清除最后通知")
        Catch ex As Exception
            操作失败("清除最后通知失败", ex)
        End Try
    End Sub

    Private Sub ModernButton13_Click(sender As Object, e As EventArgs) Handles ModernButton13.Click
        Try
            LakeNotificationManager.Unregister()
            记录("已注销当前进程的通知注册")
            刷新状态("已注销注册")
            ExFloatingTip("已注销通知注册")
        Catch ex As Exception
            操作失败("注销通知注册失败", ex)
        End Try
    End Sub

    Private Sub ModernButton14_Click(sender As Object, e As EventArgs) Handles ModernButton14.Click
        刷新状态("手动刷新")
        ExFloatingTip("状态已刷新")
    End Sub

    Private Function 创建完整通知请求() As LakeNotificationRequest
        Dim 请求 As New LakeNotificationRequest With {
            .Tag = 通知标签,
            .Group = 通知分组,
            .AttributionText = "LakeUI Demo",
            .Duration = LakeNotificationDuration.Long,
            .Priority = LakeNotificationPriority.High,
            .InitialProgress = New LakeNotificationProgressUpdate With {
                .SequenceNumber = 1UI,
                .Title = "LakeUI.Notifications",
                .Status = "等待更新",
                .Value = 0.2,
                .ValueStringOverride = "20%"
            }
        }

        请求.Arguments("source") = "LakeUI.Demo"
        请求.Arguments("feature") = "LakeUI.Notifications"
        请求.Texts.Add(New LakeNotificationText("LakeUI.Notifications"))
        请求.Texts.Add(New LakeNotificationText("带输入框、下拉选择、按钮和可更新进度条的 Windows 通知。"))
        请求.TextBoxes.Add(New LakeNotificationTextBox("reply") With {
            .Title = "快速回复",
            .Placeholder = "输入一段文字后点回复"
        })

        Dim 级别 As New LakeNotificationComboBox("level") With {
            .Title = "处理级别",
            .SelectedItem = "normal"
        }
        级别.Items("normal") = "普通"
        级别.Items("important") = "重要"
        请求.ComboBoxes.Add(级别)

        请求.ProgressBars.Add(New LakeNotificationProgressBar With {
            .BindTitle = True,
            .BindStatus = True,
            .BindValue = True,
            .BindValueStringOverride = True
        })

        Dim 回复按钮 As New LakeNotificationButton("回复") With {
            .InputId = "reply"
        }
        回复按钮.Arguments("action") = "reply"
        If LakeNotificationManager.IsButtonToolTipSupported() Then
            回复按钮.ToolTip = "读取输入框和下拉框内容"
        End If
        If LakeNotificationManager.IsButtonStyleSupported() Then
            回复按钮.ButtonStyle = LakeNotificationButtonStyle.Success
        End If
        请求.Buttons.Add(回复按钮)

        Dim 完成按钮 As New LakeNotificationButton("完成")
        完成按钮.Arguments("action") = "done"
        If LakeNotificationManager.IsButtonToolTipSupported() Then
            完成按钮.ToolTip = "触发 NotificationActivated 回调"
        End If
        请求.Buttons.Add(完成按钮)

        Dim 静默按钮 As New LakeNotificationButton("稍后处理") With {
            .ContextMenuPlacement = True
        }
        静默按钮.Arguments("action") = "later"
        请求.Buttons.Add(静默按钮)

        Return 请求
    End Function

    Private Sub 刷新状态(来源 As String)
        Try
            Dim 支持 = LakeNotificationManager.IsSupported
            Dim 设置 = LakeNotificationManager.Setting
            Dim 已注册 = LakeNotificationManager.IsRegistered
            Dim 按钮样式 = LakeNotificationManager.IsButtonStyleSupported()
            Dim 按钮提示 = LakeNotificationManager.IsButtonToolTipSupported()
            Dim 紧急场景 = LakeNotificationManager.IsUrgentScenarioSupported()

            ModernTextBoxStatus.Text =
                "来源：" & 来源 & vbCrLf &
                "Supported=" & 支持.ToString() &
                "    Setting=" & 设置.ToString() &
                "    Registered=" & 已注册.ToString() &
                "    LastId=" & _最后通知Id.ToString() & vbCrLf &
                "ButtonStyle=" & 按钮样式.ToString() &
                "    ButtonToolTip=" & 按钮提示.ToString() &
                "    UrgentScenario=" & 紧急场景.ToString()
        Catch ex As Exception
            ModernTextBoxStatus.Text = "来源：" & 来源 & vbCrLf & "状态读取失败：" & ex.Message
        End Try
    End Sub

    Private Sub 通知被激活(sender As Object, e As LakeNotificationActivatedEventArgs)
        If IsDisposed Then Return

        If InvokeRequired Then
            BeginInvoke(New MethodInvoker(Sub() 显示通知激活(e)))
        Else
            显示通知激活(e)
        End If
    End Sub

    Private Sub 显示通知激活(e As LakeNotificationActivatedEventArgs)
        Dim 文本 As New StringBuilder()
        文本.Append("通知回调：").Append(If(String.IsNullOrWhiteSpace(e.Argument), "(无 launch 参数)", e.Argument))

        If e.Arguments.Count > 0 Then
            文本.Append(" | 参数：").Append(字典转文本(e.Arguments))
        End If

        If e.UserInput.Count > 0 Then
            文本.Append(" | 用户输入：").Append(字典转文本(e.UserInput))
        End If

        记录(文本.ToString())
        刷新状态("收到通知回调")
    End Sub

    Private Function 字典转文本(values As IReadOnlyDictionary(Of String, String)) As String
        Dim 文本 As New StringBuilder()
        For Each pair In values
            If 文本.Length > 0 Then 文本.Append("; ")
            文本.Append(pair.Key).Append("=").Append(pair.Value)
        Next
        Return 文本.ToString()
    End Function

    Private Sub 记录(message As String)
        Dim 行 = Date.Now.ToString("HH:mm:ss") & "  " & message
        If String.IsNullOrWhiteSpace(ModernTextBoxLog.Text) OrElse ModernTextBoxLog.Text = "等待通知回调..." Then
            ModernTextBoxLog.Text = 行
        Else
            ModernTextBoxLog.Text = ModernTextBoxLog.Text & vbCrLf & 行
        End If
    End Sub

    Private Sub 操作失败(动作 As String, ex As Exception)
        记录(动作 & "：" & ex.Message)
        刷新状态(动作)
        ExFloatingTip(动作 & "：" & ex.Message)
    End Sub
End Class
