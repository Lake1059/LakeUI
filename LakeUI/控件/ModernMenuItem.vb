Imports System.ComponentModel

Public Class ModernMenuItem

    <Category("LakeUI"), Description("是否是分割线"), DefaultValue(False), Browsable(True)>
    Public Property IsSeparator As Boolean = False

    <Category("LakeUI"), Description("是否是描述文本"), DefaultValue(False), Browsable(True)>
    Public Property IsDescription As Boolean = False

    <Category("LakeUI"), Description("文本"), DefaultValue(GetType(String), ""), Browsable(True)>
    Public Property Text As String = ""

    <Category("LakeUI"), Description("字体"), Browsable(True)>
    Public Property Font As Font = Nothing

    <Category("LakeUI"), Description("文本颜色"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property ForeColor As Color = Color.Empty

    <Category("LakeUI"), Description("图标"), DefaultValue(GetType(Image), Nothing), Browsable(True)>
    Public Property Icon As Image = Nothing

    <Category("LakeUI"), Description("是否选中"), DefaultValue(False), Browsable(True)>
    Public Property Checked As Boolean = False

    <Category("LakeUI"), Description("点击后自动切换勾选状态"), DefaultValue(False), Browsable(True)>
    Public Property ToggleCheckOnClick As Boolean = False

    <Category("LakeUI"), Description("点击后关闭所在菜单"), DefaultValue(True), Browsable(True)>
    Public Property CloseOnClick As Boolean = True

    <Category("LakeUI"), Description("绑定的子菜单"), DefaultValue(GetType(ModernContextMenu), Nothing), Browsable(True)>
    Public Property SubMenu As ModernContextMenu = Nothing

    Public Event Click As EventHandler

    Friend Sub PerformClick()
        If ToggleCheckOnClick Then Checked = Not Checked
        RaiseEvent Click(Me, EventArgs.Empty)
    End Sub

    Public Sub New()
    End Sub

    Public Sub New(text As String)
        Me.Text = text
    End Sub

    Public Sub New(text As String, icon As Image)
        Me.Text = text
        Me.Icon = icon
    End Sub

    Public Overrides Function ToString() As String
        If IsSeparator Then Return "─── Separator ───"
        If IsDescription Then Return $"[说明] {Text}"
        Return If(String.IsNullOrEmpty(Text), "ModernMenuItem", Text)
    End Function

End Class
