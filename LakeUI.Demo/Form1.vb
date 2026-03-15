Public Class Form1
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        Me.ModernTabListControl1.Items(1).BoundControl = Form基本信息
        Me.ModernTabListControl1.Items(2).BoundControl = Form许可证

        Me.ModernTabListControl1.Items(5).BoundControl = Form_ModernButton
        Me.ModernTabListControl1.Items(6).BoundControl = Form_ModernTextBox
        Me.ModernTabListControl1.Items(7).BoundControl = Form_ModernComboBox
        Me.ModernTabListControl1.Items(8).BoundControl = Form_BooleanSwitch
        Me.ModernTabListControl1.Items(9).BoundControl = Form_QuantumSwitch
        Me.ModernTabListControl1.Items(10).BoundControl = Form_ExcellentTrackBar
        Me.ModernTabListControl1.Items(11).BoundControl = Form_ListViewDirectReDraw
        Me.ModernTabListControl1.Items(12).BoundControl = Form_ReDrawContextMenuStrip
        Me.ModernTabListControl1.Items(13).BoundControl = Form_ModernContextMenu
        Me.ModernTabListControl1.Items(14).BoundControl = Form_UltraDetailListView
        Me.ModernTabListControl1.Items(15).BoundControl = Form_ModernTabListControl
        Me.ModernTabListControl1.Items(16).BoundControl = Form_ModernTabControl
        Me.ModernTabListControl1.Items(17).BoundControl = Form_ModernPanel
        Me.ModernTabListControl1.Items(18).BoundControl = Form_ModernListBox
        Me.ModernTabListControl1.Items(19).BoundControl = Form_HtmlColorLabel
        Me.ModernTabListControl1.Items(20).BoundControl = Form_ModernFontDialog
        Me.ModernTabListControl1.Items(21).BoundControl = Form_ModernColorDialog
        Me.ModernTabListControl1.Items(22).BoundControl = Form_ExcellentProgressBar
        Me.ModernTabListControl1.Items(23).BoundControl = Form_RoundDashBoard
    End Sub

    Private Sub Form1_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        Me.ModernTabListControl1.SelectedIndex = 1
    End Sub
End Class