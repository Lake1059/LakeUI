Public Class Form1

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Using D3D_PaintBridge.BeginRenderUpdate(Me)
            LakeUI.GlobalOptions.GlobalTextQuality = LakeUI.GlobalOptions.TextQualityMode.Outline
            Me.ThisIsYourWindow1.Attach(Me)

            注册页面(1, Function() Form基本信息, Function(page) page.ModernPanel1)
            注册页面(2, Function() Form许可证, Function(page) page.ModernPanel1)

            注册页面(5, Function() Form_ModernButton, Function(page) page.ModernPanel1)
            注册页面(6, Function() Form_ModernTextBox, Function(page) page.ModernPanel1)
            注册页面(7, Function() Form_ModernComboBox, Function(page) page.ModernPanel1)
            注册页面(8, Function() Form_BooleanSwitch, Function(page) page.ModernPanel1)
            注册页面(9, Function() Form_QuantumSwitch, Function(page) page.ModernPanel1)
            注册页面(10, Function() Form_ExcellentTrackBar, Function(page) page.ModernPanel1)
            注册页面(11, Function() Form_ListViewDirectReDraw, Function(page) page.ModernPanel1)
            注册页面(12, Function() Form_ReDrawContextMenuStrip, Function(page) page.ModernPanel1)
            注册页面(13, Function() Form_ModernContextMenu, Function(page) page.ModernPanel1)
            注册页面(14, Function() Form_UltraDetailListView, Function(page) page.ModernPanel1)
            注册页面(15, Function() Form_ModernTabListControl, Function(page) page.ModernPanel1)
            注册页面(16, Function() Form_ModernTabControl, Function(page) page.ModernPanel1)
            注册页面(17, Function() Form_ModernPanel, Function(page) page.ModernPanel1)
            注册页面(18, Function() Form_ModernListBox, Function(page) page.ModernPanel1)
            注册页面(19, Function() Form_HtmlColorLabel, Function(page) page.ModernPanel1)
            注册页面(20, Function() Form_ModernFontDialog, Function(page) page.ModernPanel1)
            注册页面(21, Function() Form_ModernColorDialog, Function(page) page.ModernPanel1)
            注册页面(22, Function() Form_ExcellentProgressBar, Function(page) page.ModernPanel1)
            注册页面(23, Function() Form_RoundDashBoard, Function(page) page.ModernPanel1)
            注册页面(24, Function() Form_JustEmptyControl, Function(page) page.ModernPanel1)
            注册页面(25, Function() Form_ModernCheckBox, Function(page) page.ModernPanel1)
            注册页面(26, Function() Form_ThisIsYourWindow, Function(page) page.ModernPanel1)
            注册页面(27, Function() Form_MarkDownViewer, Function(page) page.ModernPanel1)
            注册页面(28, Function() Form_ProgressRing, Function(page) page.ModernPanel1)
            注册页面(29, Function() Form_SysTaskBarProgress, Function(page) page.ModernPanel1)
            注册页面(30, Function() Form_PixelPictureBox, Function(page) page.ModernPanel1)
            注册页面(31, Function() Form_TaskbarThumbnailToolbar, Function(page) page.ModernPanel1)
            注册页面(32, Function() Form_MsgBox_InputBox_Tip, Function(page) page.ModernPanel1)
            注册页面(33, Function() Form_CpuMonitor, Function(page) page.ModernPanel1)
            注册页面(34, Function() Form_RamMonitor, Function(page) page.ModernPanel1)
            注册页面(35, Function() Form_GpuMonitor, Function(page) page.ModernPanel1)
            注册页面(36, Function() Form_BreadcrumbNavigationBar, Function(page) page.ModernPanel1)
            注册页面(37, Function() Form_PrecisionTimer, Function(page) page.ModernPanel1)
            注册页面(38, Function() Form_AgentRoom, Function(page) page.ModernPanel1)
            注册页面(39, Function() Form_ModernNumericUpDown, Function(page) page.ModernPanel1)
            注册页面(40, Function() Form_MemberWall, Function(page) page.ModernPanel1)
            注册页面(41, Function() Form_EasyStatesPanel, Function(page) page.ModernPanel1)
            注册页面(42, Function() Form_LakeUINotifications, Function(page) page.ModernPanel1)
            注册页面(43, Function() Form_Ultra2DChart, Function(page) page.ModernPanel1)
            Me.ModernTabListControl1.SelectedIndex = 1
        End Using
    End Sub

    Private Sub Form1_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        Me.ModernTabListControl1.SelectedIndex = 1

    End Sub

    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing

    End Sub

    Private Sub 注册页面(Of T As Form)(index As Integer, factory As Func(Of T), panel As Func(Of T, ModernPanel))
        Me.ModernTabListControl1.Items(index).BoundControlFactory =
            Function()
                Dim page = factory()
                绑定选项卡窗体背景透明(panel(page))
                Return page
            End Function
    End Sub

    Sub 绑定选项卡窗体背景透明(选项卡的根面板容器 As ModernPanel)
        If 选项卡的根面板容器 Is Nothing Then Return
        选项卡的根面板容器.BackColor = Color.Transparent
        选项卡的根面板容器.BackColor1 = Color.Transparent
        选项卡的根面板容器.BackgroundSource = Me
    End Sub

End Class
