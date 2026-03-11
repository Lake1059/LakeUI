Public Class Form1
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Form2.Show()
        Me.ModernTabListControl1.Items(1).BoundControl = Form基本信息
        Me.ModernTabListControl1.Items(2).BoundControl = Form许可证
        Me.ModernTabListControl1.Items(5).BoundControl = Form_ListViewDirectReDraw
        Me.ModernTabListControl1.Items(6).BoundControl = Form_ModernContextMenu






    End Sub

    Private Sub Form1_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        Me.ModernTabListControl1.SelectedIndex = 1
        'Dim a As New ModernFontDialog
        'a.Show(Me)
        Dim a As New ModernColorDialog
        a.Show(Me)
    End Sub
End Class