Public Class Form_AgentRoom
    Private Sub Form_AgentRoom_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        AgentRoom1.AddUserMessage("这是一段示例文本")
        AgentRoom1.AddAssistantMessage("这是一段示例回复")
        AgentRoom1.AddAssistantMessage("访问 https://github.com 了解更多")
        AgentRoom1.AddCard("正在编辑文件")
        AgentRoom1.AddAssistantMessage("哈哈哈")
    End Sub
End Class