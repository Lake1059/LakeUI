<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class ModernColorDialog
    Inherits System.Windows.Forms.Form

    'Form 重写 Dispose，以清理组件列表。
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Windows 窗体设计器所必需的
    Private components As System.ComponentModel.IContainer

    '注意: 以下过程是 Windows 窗体设计器所必需的
    '可以使用 Windows 窗体设计器修改它。
    '不要使用代码编辑器修改它。
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Dim ModernMenuItem1 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        Dim ModernMenuItem2 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        Dim ModernMenuItem3 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        Dim ModernMenuItem4 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        Dim ModernMenuItem5 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        Dim ModernMenuItem6 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        Dim ModernMenuItem7 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        Dim ModernMenuItem8 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        Dim ModernMenuItem9 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        Dim ModernMenuItem10 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        Dim ModernMenuItem11 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        Dim ModernMenuItem12 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        Dim ModernMenuItem13 As LakeUI.ModernContextMenu.ModernMenuItem = New ModernContextMenu.ModernMenuItem()
        ModernContextMenu1 = New ModernContextMenu()
        SuspendLayout()
        ' 
        ' ModernContextMenu1
        ' 
        ModernContextMenu1.BackdropBlurPasses = 1
        ModernContextMenu1.BackdropBlurRadius = 10
        ModernContextMenu1.BackdropMode = ModernContextMenu.BackdropModeEnum.Auto
        ModernContextMenu1.BackdropNoiseOpacity = CByte(0)
        ModernContextMenu1.BorderSize = 2
        ModernContextMenu1.HoverAnimationFPS = 0
        ModernContextMenu1.HoverRadius = 5
        ModernContextMenu1.IconSize = 0
        ModernContextMenu1.ItemPadding = New Padding(7, 0, 0, 0)
        ModernMenuItem1.Font = Nothing
        ModernMenuItem1.IsDescription = True
        ModernMenuItem1.Text = "最推荐的做法"
        ModernMenuItem2.Font = Nothing
        ModernMenuItem2.Text = "先选择 HTML 颜色来定位到大致颜色，然后使用数值面板进行微调"
        ModernMenuItem3.Font = Nothing
        ModernMenuItem3.IsSeparator = True
        ModernMenuItem4.Font = Nothing
        ModernMenuItem4.IsDescription = True
        ModernMenuItem4.Text = "数值面板操作提示"
        ModernMenuItem5.Font = Nothing
        ModernMenuItem5.Text = "除了 HEX，所有文本框可以使用鼠标滚轮进行微调"
        ModernMenuItem6.Font = Nothing
        ModernMenuItem6.Text = "除了 HEX，所有标签区域可以像实体推子一样快速增加或减少"
        ModernMenuItem7.Font = Nothing
        ModernMenuItem7.IsSeparator = True
        ModernMenuItem8.Font = Nothing
        ModernMenuItem8.IsDescription = True
        ModernMenuItem8.Text = "色域图操作提示"
        ModernMenuItem9.Font = Nothing
        ModernMenuItem9.Text = "左键选择时，会尝试保留当前亮度来计算新的颜色"
        ModernMenuItem10.Font = Nothing
        ModernMenuItem10.Text = "右键选择时，不会保留亮度，而是直接映射颜色"
        ModernMenuItem11.Font = Nothing
        ModernMenuItem11.IsSeparator = True
        ModernMenuItem12.Font = Nothing
        ModernMenuItem12.IsDescription = True
        ModernMenuItem12.Text = "并非节约性能"
        ModernMenuItem13.Font = Nothing
        ModernMenuItem13.Text = "这个菜单默认是开无限帧率动画的，就看开发者有没有调了"
        ModernContextMenu1.Items.Add(ModernMenuItem1)
        ModernContextMenu1.Items.Add(ModernMenuItem2)
        ModernContextMenu1.Items.Add(ModernMenuItem3)
        ModernContextMenu1.Items.Add(ModernMenuItem4)
        ModernContextMenu1.Items.Add(ModernMenuItem5)
        ModernContextMenu1.Items.Add(ModernMenuItem6)
        ModernContextMenu1.Items.Add(ModernMenuItem7)
        ModernContextMenu1.Items.Add(ModernMenuItem8)
        ModernContextMenu1.Items.Add(ModernMenuItem9)
        ModernContextMenu1.Items.Add(ModernMenuItem10)
        ModernContextMenu1.Items.Add(ModernMenuItem11)
        ModernContextMenu1.Items.Add(ModernMenuItem12)
        ModernContextMenu1.Items.Add(ModernMenuItem13)
        ModernContextMenu1.MenuPadding = New Padding(10)
        ModernContextMenu1.PopupAnimationDuration = 300
        ModernContextMenu1.PopupAnimationFPS = 0
        ModernContextMenu1.SeparatorHeight = 20
        ' 
        ' ModernColorDialog
        ' 
        AutoScaleDimensions = New SizeF(96F, 96F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(24), CByte(24), CByte(24))
        ClientSize = New Size(834, 561)
        Font = New Font("Microsoft YaHei UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ForeColor = Color.Silver
        MaximizeBox = False
        MinimizeBox = False
        MinimumSize = New Size(850, 600)
        Name = "ModernColorDialog"
        ShowIcon = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        Text = "sRGB ColorDialog CIE 1931 380nm~700nm Step 5nm"
        ResumeLayout(False)
    End Sub

    Friend WithEvents ModernContextMenu1 As ModernContextMenu
End Class
