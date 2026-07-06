Public Class Form1

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        LakeUI.GlobalOptions.GlobalTextQuality = LakeUI.GlobalOptions.TextQualityMode.Outline
        LakeUI.GlobalOptions.HDR.Enabled = True
        LakeUI.GlobalOptions.HDR.Profile = LakeUI.GlobalOptions.HdrOutputProfile.HDR400
        LakeUI.GlobalOptions.HDR.MapVectorColors = True
        LakeUI.GlobalOptions.HDR.MapImages = True
        Me.ThisIsYourWindow1.Attach(Me)

        Me.ModernTabListControl1.Items(1).BoundControl = Form基本信息
        绑定选项卡窗体背景透明(Form基本信息.ModernPanel1)
        Me.ModernTabListControl1.Items(2).BoundControl = Form许可证
        绑定选项卡窗体背景透明(Form许可证.ModernPanel1)

        Me.ModernTabListControl1.Items(5).BoundControl = Form_ModernButton
        绑定选项卡窗体背景透明(Form_ModernButton.ModernPanel1)
        Me.ModernTabListControl1.Items(6).BoundControl = Form_ModernTextBox
        绑定选项卡窗体背景透明(Form_ModernTextBox.ModernPanel1)
        Me.ModernTabListControl1.Items(7).BoundControl = Form_ModernComboBox
        绑定选项卡窗体背景透明(Form_ModernComboBox.ModernPanel1)
        Me.ModernTabListControl1.Items(8).BoundControl = Form_BooleanSwitch
        绑定选项卡窗体背景透明(Form_BooleanSwitch.ModernPanel1)
        Me.ModernTabListControl1.Items(9).BoundControl = Form_QuantumSwitch
        绑定选项卡窗体背景透明(Form_QuantumSwitch.ModernPanel1)
        Me.ModernTabListControl1.Items(10).BoundControl = Form_ExcellentTrackBar
        绑定选项卡窗体背景透明(Form_ExcellentTrackBar.ModernPanel1)
        Me.ModernTabListControl1.Items(11).BoundControl = Form_ListViewDirectReDraw
        绑定选项卡窗体背景透明(Form_ListViewDirectReDraw.ModernPanel1)
        Me.ModernTabListControl1.Items(12).BoundControl = Form_ReDrawContextMenuStrip
        绑定选项卡窗体背景透明(Form_ReDrawContextMenuStrip.ModernPanel1)
        Me.ModernTabListControl1.Items(13).BoundControl = Form_ModernContextMenu
        绑定选项卡窗体背景透明(Form_ModernContextMenu.ModernPanel1)
        Me.ModernTabListControl1.Items(14).BoundControl = Form_UltraDetailListView
        绑定选项卡窗体背景透明(Form_UltraDetailListView.ModernPanel1)
        Me.ModernTabListControl1.Items(15).BoundControl = Form_ModernTabListControl
        绑定选项卡窗体背景透明(Form_ModernTabListControl.ModernPanel1)
        Me.ModernTabListControl1.Items(16).BoundControl = Form_ModernTabControl
        绑定选项卡窗体背景透明(Form_ModernTabControl.ModernPanel1)
        Me.ModernTabListControl1.Items(17).BoundControl = Form_ModernPanel
        绑定选项卡窗体背景透明(Form_ModernPanel.ModernPanel1)
        Me.ModernTabListControl1.Items(18).BoundControl = Form_ModernListBox
        绑定选项卡窗体背景透明(Form_ModernListBox.ModernPanel1)
        Me.ModernTabListControl1.Items(19).BoundControl = Form_HtmlColorLabel
        绑定选项卡窗体背景透明(Form_HtmlColorLabel.ModernPanel1)
        Me.ModernTabListControl1.Items(20).BoundControl = Form_ModernFontDialog
        绑定选项卡窗体背景透明(Form_ModernFontDialog.ModernPanel1)
        Me.ModernTabListControl1.Items(21).BoundControl = Form_ModernColorDialog
        绑定选项卡窗体背景透明(Form_ModernColorDialog.ModernPanel1)
        Me.ModernTabListControl1.Items(22).BoundControl = Form_ExcellentProgressBar
        绑定选项卡窗体背景透明(Form_ExcellentProgressBar.ModernPanel1)
        Me.ModernTabListControl1.Items(23).BoundControl = Form_RoundDashBoard
        绑定选项卡窗体背景透明(Form_RoundDashBoard.ModernPanel1)
        Me.ModernTabListControl1.Items(24).BoundControl = Form_JustEmptyControl
        绑定选项卡窗体背景透明(Form_JustEmptyControl.ModernPanel1)
        Me.ModernTabListControl1.Items(25).BoundControl = Form_ModernCheckBox
        绑定选项卡窗体背景透明(Form_ModernCheckBox.ModernPanel1)
        Me.ModernTabListControl1.Items(26).BoundControl = Form_ThisIsYourWindow
        绑定选项卡窗体背景透明(Form_ThisIsYourWindow.ModernPanel1)
        Me.ModernTabListControl1.Items(27).BoundControl = Form_MarkDownViewer
        绑定选项卡窗体背景透明(Form_MarkDownViewer.ModernPanel1)
        Me.ModernTabListControl1.Items(28).BoundControl = Form_ProgressRing
        绑定选项卡窗体背景透明(Form_ProgressRing.ModernPanel1)
        Me.ModernTabListControl1.Items(29).BoundControl = Form_SysTaskBarProgress
        绑定选项卡窗体背景透明(Form_SysTaskBarProgress.ModernPanel1)
        Me.ModernTabListControl1.Items(30).BoundControl = Form_PixelPictureBox
        绑定选项卡窗体背景透明(Form_PixelPictureBox.ModernPanel1)
        Me.ModernTabListControl1.Items(31).BoundControl = Form_TaskbarThumbnailToolbar
        绑定选项卡窗体背景透明(Form_TaskbarThumbnailToolbar.ModernPanel1)
        Me.ModernTabListControl1.Items(32).BoundControl = Form_MsgBox_InputBox_Tip
        绑定选项卡窗体背景透明(Form_MsgBox_InputBox_Tip.ModernPanel1)
        Me.ModernTabListControl1.Items(33).BoundControl = Form_CpuMonitor
        绑定选项卡窗体背景透明(Form_CpuMonitor.ModernPanel1)
        Me.ModernTabListControl1.Items(34).BoundControl = Form_RamMonitor
        绑定选项卡窗体背景透明(Form_RamMonitor.ModernPanel1)
        Me.ModernTabListControl1.Items(35).BoundControl = Form_GpuMonitor
        绑定选项卡窗体背景透明(Form_GpuMonitor.ModernPanel1)
        Me.ModernTabListControl1.Items(36).BoundControl = Form_BreadcrumbNavigationBar
        绑定选项卡窗体背景透明(Form_BreadcrumbNavigationBar.ModernPanel1)
        Me.ModernTabListControl1.Items(37).BoundControl = Form_PrecisionTimer
        绑定选项卡窗体背景透明(Form_PrecisionTimer.ModernPanel1)
        Me.ModernTabListControl1.Items(38).BoundControl = Form_AgentRoom
        绑定选项卡窗体背景透明(Form_AgentRoom.ModernPanel1)
        Me.ModernTabListControl1.Items(39).BoundControl = Form_ModernNumericUpDown
        绑定选项卡窗体背景透明(Form_ModernNumericUpDown.ModernPanel1)
        Me.ModernTabListControl1.Items(40).BoundControl = Form_MemberWall
        绑定选项卡窗体背景透明(Form_MemberWall.ModernPanel1)
        Me.ModernTabListControl1.Items(41).BoundControl = Form_EasyStatesPanel
        绑定选项卡窗体背景透明(Form_EasyStatesPanel.ModernPanel1)
        Me.ModernTabListControl1.Items(42).BoundControl = Form_LakeUINotifications
        绑定选项卡窗体背景透明(Form_LakeUINotifications.ModernPanel1)
        Me.ModernTabListControl1.Items(43).BoundControl = Form_Ultra2DChart
        绑定选项卡窗体背景透明(Form_Ultra2DChart.ModernPanel1)
    End Sub

    Private Sub Form1_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        Me.ModernTabListControl1.SelectedIndex = 1

    End Sub

    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing

    End Sub

    Sub 绑定选项卡窗体背景透明(选项卡的根面板容器 As ModernPanel)
        If 选项卡的根面板容器 Is Nothing Then Return
        选项卡的根面板容器.BackColor = Color.Transparent
        选项卡的根面板容器.BackColor1 = Color.Transparent
        选项卡的根面板容器.BackgroundSource = Me
    End Sub

End Class
