Public Class Form_ReDrawContextMenuStrip
    Private Sub Form_ModernContextMenu_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.ModernTextBox1.Text = $"哪个 WinForms 新手没有被原版上下文菜单的离谱机制折磨过呢，现在不需要考虑那么多了，所有颜色重新渲染，还可调整项高度，更有专为强迫症适配的 Separator 暗号（Tag 设置为 nothing 或 null 即可渲染成空白段），以及新增了 10 多个属性。注意下拉框和文本框没有重绘，还是原来的样子，这俩的使用场景太稀有了，不建议把那俩塞菜单里。"
        Me.ReDrawContextMenuStrip1.DPI = Me.ParentForm.DeviceDpi / 96
    End Sub

End Class