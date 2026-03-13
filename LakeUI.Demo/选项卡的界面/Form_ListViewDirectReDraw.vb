Public Class Form_ListViewDirectReDraw
    Private Sub Form_ListViewDirectReDraw_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.ModernTextBox1.Text = $"无需变动已有项目，仍旧使用原版的 ListView，直接调用 LakeUI.ListViewDirectReDraw.TakeOver() 并填好定制参数即可，支持列表视图的全部模式。原理：通过使用原版的 OwnerDraw 功能，不仅可以解决原版的大多数绘制限制，还能解锁 SubItem 自身的字体和颜色展现，同时还能提升性能，最重要的是那个令强迫症反复去世的键盘焦点终于可以走开了，如果不想改动现有项目就用上那么这是最佳选择，虽然这样做限制比较多但已足够满足极大多数场景需求。"
        Me.ModernTextBox2.Text = $"无法重绘滚动条，如果有深度定制需求，请查看相关全新绘制的控件。{vbCrLf}无法从渲染上调整项的高度，请用 ImageList 把项撑起来，这是原版的特性。 {vbCrLf}无法重绘详细信息模式下列标头的右侧区域，建议自己做一个外部列。{vbCrLf}无法重绘分组的样式。"
        ListViewDirectReDraw.TakeOver(Me.ListView1, New ListViewDirectReDraw.ListViewOption With {.SelectedBackColor = Color.FromArgb(64, 64, 64)})
    End Sub

    Private Sub ListView1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ListView1.SelectedIndexChanged

    End Sub
End Class