Public Class Form1
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        LakeUI.D2DHelper.GlobalTextQuality = TextQualityMode.ClearType
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
        Me.ModernTabListControl1.Items(23).BoundControl = Form_RoundDashBoard
        Me.ModernTabListControl1.Items(24).BoundControl = Form_JustEmptyControl
        Me.ModernTabListControl1.Items(25).BoundControl = Form_ModernCheckBox
        绑定选项卡窗体背景透明(Form_ModernCheckBox.ModernPanel1)
        Me.ModernTabListControl1.Items(26).BoundControl = Form_ThisIsYourWindow
        绑定选项卡窗体背景透明(Form_ThisIsYourWindow.ModernPanel1)
        Me.ModernTabListControl1.Items(27).BoundControl = Form_MarkDownViewer
        绑定选项卡窗体背景透明(Form_MarkDownViewer.ModernPanel1)
        Me.ModernTabListControl1.Items(28).BoundControl = Form_ProgressRing
        Me.ModernTabListControl1.Items(29).BoundControl = Form_SysTaskBarProgress
        Me.ModernTabListControl1.Items(30).BoundControl = Form_PixelPictureBox
        Me.ModernTabListControl1.Items(31).BoundControl = Form_TaskbarThumbnailToolbar
        Me.ModernTabListControl1.Items(32).BoundControl = Form_MsgBox_InputBox_Tip
        Me.ModernTabListControl1.Items(33).BoundControl = Form_CpuMonitor
        绑定选项卡窗体背景透明(Form_CpuMonitor.ModernPanel1)
        Me.ModernTabListControl1.Items(34).BoundControl = Form_RamMonitor
        绑定选项卡窗体背景透明(Form_RamMonitor.ModernPanel1)
        Me.ModernTabListControl1.Items(35).BoundControl = Form_GpuMonitor
        绑定选项卡窗体背景透明(Form_GpuMonitor.ModernPanel1)

        Me.ModernTabListControl1.Items(37).BoundControl = Form_PrecisionTimer
        Me.ModernTabListControl1.Items(38).BoundControl = Form_AgentRoom
    End Sub

    Private Sub Form1_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        Me.ModernTabListControl1.SelectedIndex = 1

    End Sub

    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing

    End Sub

    Sub 绑定选项卡窗体背景透明(选项卡的根面板容器 As ModernPanel)
        选项卡的根面板容器.BackColor = Color.Transparent
        选项卡的根面板容器.BackColor1 = Color.Transparent
        选项卡的根面板容器.BackgroundSource = Me
    End Sub

End Class