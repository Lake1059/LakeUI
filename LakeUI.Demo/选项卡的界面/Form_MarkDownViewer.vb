Public Class Form_MarkDownViewer
    Private Sub Form_MarkDownViewer_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.ModernTextBox1.Text = CreateSampleMarkdown()
        Me.MarkDownViewer1.Text = Me.ModernTextBox1.Text
    End Sub

    Private Sub Form_MarkDownViewer_SizeChanged(sender As Object, e As EventArgs) Handles Me.SizeChanged
        Me.ModernTextBox1.Width = (Panel1.Width - Me.JustEmptyControl2.Width) * 0.5
    End Sub

    Private Sub ModernTextBox1_TextChanged(sender As Object, e As EventArgs) Handles ModernTextBox1.TextChanged
        Me.MarkDownViewer1.Text = Me.ModernTextBox1.Text
    End Sub

    Private Shared Function CreateSampleMarkdown() As String
        Return String.Join(vbCrLf, {
            "# LakeUI MarkdownViewer",
            "",
            "MarkdownViewer 使用 V3 GPU 路线完成文本、块级结构、链接和代码块绘制。",
            "",
            "## 支持内容",
            "",
            "- 标题、段落、粗体、斜体和删除线",
            "- 引用块、分隔线和有序/无序列表",
            "- 表格、链接和行内代码",
            "- 代码块与滚动视图",
            "",
            "> 这段内容由左侧 ModernTextBox 触发 TextChanged 后同步到右侧预览。",
            "",
            "| 项目 | 状态 |",
            "| --- | --- |",
            "| TextChanged 同步 | 正常 |",
            "| V3 文本绘制 | 正常 |",
            "| 背景穿透 | 正常 |",
            "",
            "```vb",
            "Private Sub ModernTextBox1_TextChanged(sender As Object, e As EventArgs)",
            "    MarkDownViewer1.Text = ModernTextBox1.Text",
            "End Sub",
            "```",
            "",
            "链接示例: https://lakeui.top"
        })
    End Function

End Class
