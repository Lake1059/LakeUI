Imports LakeUI.Notifications
Imports Microsoft.VisualBasic.ApplicationServices

Namespace My
    Partial Friend Class MyApplication
        Private Sub MyApplication_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
            Try
                Dim 通知选项 As New LakeNotificationRegistrationOptions With {
                    .DisplayName = "LakeUI Demo",
                    .ShowRuntimeInstallerUi = False
                }

                Dim 图标路径 = IO.Path.Combine(AppContext.BaseDirectory, "LakeUI.png")
                If IO.File.Exists(图标路径) Then
                    通知选项.IconUri = New Uri(图标路径)
                End If

                LakeNotificationManager.Initialize(通知选项)
                AddHandler LakeNotificationManager.NotificationActivated, AddressOf MyApplication_NotificationActivated
            Catch ex As Exception
                Diagnostics.Debug.WriteLine("LakeUI notification initialization failed: " & ex.Message)
            End Try
        End Sub

        Private Sub MyApplication_NotificationActivated(sender As Object, e As LakeNotificationActivatedEventArgs)
            Diagnostics.Debug.WriteLine("LakeUI notification activated: " & e.Argument)
        End Sub
    End Class
End Namespace
