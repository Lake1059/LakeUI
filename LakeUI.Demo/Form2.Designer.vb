<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form2
    Inherits System.Windows.Forms.Form

    'Form 重写 Dispose，以清理组件列表。
    <System.Diagnostics.DebuggerNonUserCode()> _
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
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Dim ListColumn1 As LakeUI.UltraDetailListView.ListColumn = New UltraDetailListView.ListColumn()
        Dim ListColumn2 As LakeUI.UltraDetailListView.ListColumn = New UltraDetailListView.ListColumn()
        Dim ListColumn3 As LakeUI.UltraDetailListView.ListColumn = New UltraDetailListView.ListColumn()
        Dim ListGroup1 As LakeUI.UltraDetailListView.ListGroup = New UltraDetailListView.ListGroup()
        Dim ListItem1 As LakeUI.UltraDetailListView.ListItem = New UltraDetailListView.ListItem()
        Dim ListSubItem1 As LakeUI.UltraDetailListView.ListSubItem = New UltraDetailListView.ListSubItem()
        Dim ListSubItem2 As LakeUI.UltraDetailListView.ListSubItem = New UltraDetailListView.ListSubItem()
        Dim ListSubItem3 As LakeUI.UltraDetailListView.ListSubItem = New UltraDetailListView.ListSubItem()
        Dim ListItem2 As LakeUI.UltraDetailListView.ListItem = New UltraDetailListView.ListItem()
        Dim ListSubItem4 As LakeUI.UltraDetailListView.ListSubItem = New UltraDetailListView.ListSubItem()
        Dim ListSubItem5 As LakeUI.UltraDetailListView.ListSubItem = New UltraDetailListView.ListSubItem()
        Dim ListSubItem6 As LakeUI.UltraDetailListView.ListSubItem = New UltraDetailListView.ListSubItem()
        Dim ListItem3 As LakeUI.UltraDetailListView.ListItem = New UltraDetailListView.ListItem()
        Dim ListSubItem7 As LakeUI.UltraDetailListView.ListSubItem = New UltraDetailListView.ListSubItem()
        Dim ListSubItem8 As LakeUI.UltraDetailListView.ListSubItem = New UltraDetailListView.ListSubItem()
        Dim ListSubItem9 As LakeUI.UltraDetailListView.ListSubItem = New UltraDetailListView.ListSubItem()
        Dim ListItem4 As LakeUI.UltraDetailListView.ListItem = New UltraDetailListView.ListItem()
        Dim TextLine1 As LakeUI.UltraDetailListView.TextLine = New UltraDetailListView.TextLine()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(Form2))
        Dim ListSubItem10 As LakeUI.UltraDetailListView.ListSubItem = New UltraDetailListView.ListSubItem()
        Dim TextLine2 As LakeUI.UltraDetailListView.TextLine = New UltraDetailListView.TextLine()
        Dim ListSubItem11 As LakeUI.UltraDetailListView.ListSubItem = New UltraDetailListView.ListSubItem()
        Dim TextLine3 As LakeUI.UltraDetailListView.TextLine = New UltraDetailListView.TextLine()
        Dim ListSubItem12 As LakeUI.UltraDetailListView.ListSubItem = New UltraDetailListView.ListSubItem()
        Dim TextLine4 As LakeUI.UltraDetailListView.TextLine = New UltraDetailListView.TextLine()
        Dim ListItem5 As LakeUI.UltraDetailListView.ListItem = New UltraDetailListView.ListItem()
        Dim ListSubItem13 As LakeUI.UltraDetailListView.ListSubItem = New UltraDetailListView.ListSubItem()
        Dim ListSubItem14 As LakeUI.UltraDetailListView.ListSubItem = New UltraDetailListView.ListSubItem()
        Dim ListSubItem15 As LakeUI.UltraDetailListView.ListSubItem = New UltraDetailListView.ListSubItem()
        Dim TrackLabel1 As LakeUI.ExcellentTrackBar.TrackLabel = New ExcellentTrackBar.TrackLabel()
        Dim TrackLabel2 As LakeUI.ExcellentTrackBar.TrackLabel = New ExcellentTrackBar.TrackLabel()
        Dim TrackLabel3 As LakeUI.ExcellentTrackBar.TrackLabel = New ExcellentTrackBar.TrackLabel()
        Dim TrackLabel4 As LakeUI.ExcellentTrackBar.TrackLabel = New ExcellentTrackBar.TrackLabel()
        Dim TrackLabel5 As LakeUI.ExcellentTrackBar.TrackLabel = New ExcellentTrackBar.TrackLabel()
        Dim TrackLabel6 As LakeUI.ExcellentTrackBar.TrackLabel = New ExcellentTrackBar.TrackLabel()
        Dim TrackLabel7 As LakeUI.ExcellentTrackBar.TrackLabel = New ExcellentTrackBar.TrackLabel()
        Dim ToolTipEntry1 As LakeUI.ModernComboBox.ToolTipEntry = New ModernComboBox.ToolTipEntry()
        Dim ModernTab1 As LakeUI.ModernTabControl.ModernTab = New ModernTabControl.ModernTab()
        Dim ModernTab2 As LakeUI.ModernTabControl.ModernTab = New ModernTabControl.ModernTab()
        Dim ModernTab3 As LakeUI.ModernTabControl.ModernTab = New ModernTabControl.ModernTab()
        Dim ModernTab4 As LakeUI.ModernTabControl.ModernTab = New ModernTabControl.ModernTab()
        Dim ModernTab5 As LakeUI.ModernTabControl.ModernTab = New ModernTabControl.ModernTab()
        UltraDetailListView1 = New UltraDetailListView()
        ExcellentTrackBar1 = New ExcellentTrackBar()
        QuantumSwitch1 = New QuantumSwitch()
        BooleanSwitch1 = New BooleanSwitch()
        ModernComboBox1 = New ModernComboBox()
        ModernTextBox1 = New ModernTextBox()
        ModernButton1 = New ModernButton()
        ModernTabControl1 = New ModernTabControl()
        HtmlColorLabel1 = New HtmlColorLabel()
        ModernPanel1 = New ModernPanel()
        ExcellentProgressBar1 = New ExcellentProgressBar()
        RoundDashBoard1 = New RoundDashBoard()
        ModernPanel1.SuspendLayout()
        SuspendLayout()
        ' 
        ' UltraDetailListView1
        ' 
        UltraDetailListView1.BottomLinesSpacing = 10
        ListColumn1.Text = "列宽会限制子项区域"
        ListColumn1.Width = 300
        ListColumn2.Text = "但不影响项的焦点宽度"
        ListColumn2.Width = 300
        ListColumn3.Text = "列宽的文字也可调整"
        ListColumn3.Width = 300
        UltraDetailListView1.Columns.Add(ListColumn1)
        UltraDetailListView1.Columns.Add(ListColumn2)
        UltraDetailListView1.Columns.Add(ListColumn3)
        UltraDetailListView1.GroupHeight = 35
        ListGroup1.Name = "G1"
        ListGroup1.Text = "分组点击切换折叠"
        UltraDetailListView1.Groups.Add(ListGroup1)
        UltraDetailListView1.HeaderBackColor = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        UltraDetailListView1.HeaderBorderColor = SystemColors.ButtonShadow
        UltraDetailListView1.HeaderHeight = 40
        UltraDetailListView1.IconSize = New Size(48, 48)
        UltraDetailListView1.IconSpacing = 10
        UltraDetailListView1.ItemPadding = New Padding(10, 5, 5, 5)
        ListSubItem1.Text = "所有文字都是子项数据"
        ListSubItem2.Text = "可以在设计器里尝试效果"
        ListSubItem3.Text = "不过没有原版那样的实时预览"
        ListItem1.SubItems.Add(ListSubItem1)
        ListItem1.SubItems.Add(ListSubItem2)
        ListItem1.SubItems.Add(ListSubItem3)
        ListSubItem4.Text = "全区域可进行拖拽选择"
        ListSubItem5.Text = "拖拽的框也可以自定义颜色"
        ListSubItem6.Text = "颜色可以是带透明度的"
        ListItem2.SubItems.Add(ListSubItem4)
        ListItem2.SubItems.Add(ListSubItem5)
        ListItem2.SubItems.Add(ListSubItem6)
        ListSubItem7.Text = "可响应键盘常用快捷键"
        ListSubItem8.Text = "事件和属性也基本和原版一致"
        ListSubItem9.Text = "更易于过渡上手"
        ListItem3.SubItems.Add(ListSubItem7)
        ListItem3.SubItems.Add(ListSubItem8)
        ListItem3.SubItems.Add(ListSubItem9)
        TextLine1.ForeColor = Color.LightSteelBlue
        TextLine1.Text = "这里是底部专用行，特别适合用来展示任务处理进度详情，这里也可以单独设置字体和颜色，并独享极限宽度"
        ListItem4.BottomLines.Add(TextLine1)
        ListItem4.GroupName = "G1"
        ListItem4.Icon = CType(resources.GetObject("ListItem4.Icon"), Image)
        TextLine2.Text = "无需依赖其他组件"
        ListSubItem10.ExtraLines.Add(TextLine2)
        ListSubItem10.Text = "图标直接设置 Icon 即可"
        TextLine3.ForeColor = Color.FromArgb(CByte(255), CByte(128), CByte(128))
        TextLine3.Text = "每行文本都可以单独设置颜色"
        ListSubItem11.ExtraLines.Add(TextLine3)
        ListSubItem11.Font = New Font("华文新魏", 12F)
        ListSubItem11.ForeColor = Color.FromArgb(CByte(128), CByte(128), CByte(255))
        ListSubItem11.Text = "每行文本都可以单独设置字体"
        TextLine4.Text = "项的高度取决于最高的子项"
        ListSubItem12.ExtraLines.Add(TextLine4)
        ListSubItem12.ForeColor = Color.FromArgb(CByte(128), CByte(255), CByte(128))
        ListSubItem12.Text = "ExtraLines 负责管理多余的行"
        ListItem4.SubItems.Add(ListSubItem10)
        ListItem4.SubItems.Add(ListSubItem11)
        ListItem4.SubItems.Add(ListSubItem12)
        ListItem5.GroupName = "G1"
        ListSubItem13.Text = "跨显示区域多选请按住 Ctrl"
        ListSubItem14.Text = "直接框选会自动放弃绘制区域外的"
        ListSubItem15.Text = "需要一些时间来熟悉机制"
        ListItem5.SubItems.Add(ListSubItem13)
        ListItem5.SubItems.Add(ListSubItem14)
        ListItem5.SubItems.Add(ListSubItem15)
        UltraDetailListView1.Items.Add(ListItem1)
        UltraDetailListView1.Items.Add(ListItem2)
        UltraDetailListView1.Items.Add(ListItem3)
        UltraDetailListView1.Items.Add(ListItem4)
        UltraDetailListView1.Items.Add(ListItem5)
        UltraDetailListView1.Location = New Point(436, 30)
        UltraDetailListView1.Name = "UltraDetailListView1"
        UltraDetailListView1.Size = New Size(936, 404)
        UltraDetailListView1.TabIndex = 24
        UltraDetailListView1.WordWrap = False
        ' 
        ' ExcellentTrackBar1
        ' 
        ExcellentTrackBar1.AnimationDuration = 500
        ExcellentTrackBar1.AnimationFPS = 0
        ExcellentTrackBar1.BackColor = Color.FromArgb(CByte(36), CByte(36), CByte(36))
        ExcellentTrackBar1.LabelFont = New Font("MiSans Medium", 9F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        ExcellentTrackBar1.LabelLineLength = 20
        TrackLabel2.Position = 1
        TrackLabel2.Side = ExcellentTrackBar.LabelSideEnum.TopOrLeft
        TrackLabel3.Position = 2
        TrackLabel4.Position = 3
        TrackLabel4.Side = ExcellentTrackBar.LabelSideEnum.TopOrLeft
        TrackLabel5.Position = 4
        TrackLabel6.Position = 5
        TrackLabel6.Side = ExcellentTrackBar.LabelSideEnum.TopOrLeft
        TrackLabel7.Position = 6
        ExcellentTrackBar1.Labels.Add(TrackLabel1)
        ExcellentTrackBar1.Labels.Add(TrackLabel2)
        ExcellentTrackBar1.Labels.Add(TrackLabel3)
        ExcellentTrackBar1.Labels.Add(TrackLabel4)
        ExcellentTrackBar1.Labels.Add(TrackLabel5)
        ExcellentTrackBar1.Labels.Add(TrackLabel6)
        ExcellentTrackBar1.Labels.Add(TrackLabel7)
        ExcellentTrackBar1.Location = New Point(66, 501)
        ExcellentTrackBar1.Maximum = 6
        ExcellentTrackBar1.Name = "ExcellentTrackBar1"
        ExcellentTrackBar1.Size = New Size(500, 100)
        ExcellentTrackBar1.StringItems.Add("veryslow")
        ExcellentTrackBar1.StringItems.Add("slower")
        ExcellentTrackBar1.StringItems.Add("slow")
        ExcellentTrackBar1.StringItems.Add("medium")
        ExcellentTrackBar1.StringItems.Add("fast")
        ExcellentTrackBar1.StringItems.Add("faster")
        ExcellentTrackBar1.StringItems.Add("veryfast")
        ExcellentTrackBar1.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        ExcellentTrackBar1.TabIndex = 23
        ExcellentTrackBar1.ThumbBorderWidth = 0
        ExcellentTrackBar1.ThumbColor = Color.IndianRed
        ExcellentTrackBar1.ThumbHeight = 35
        ExcellentTrackBar1.ThumbRadius = 5
        ExcellentTrackBar1.ThumbTextMode = ExcellentTrackBar.ThumbTextModeEnum.StringItem
        ExcellentTrackBar1.ThumbWidth = 100
        ExcellentTrackBar1.UseStringItems = True
        ExcellentTrackBar1.Value = 3
        ' 
        ' QuantumSwitch1
        ' 
        QuantumSwitch1.AnimationFPS = 0
        QuantumSwitch1.KnobColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        QuantumSwitch1.KnobColorIndeterminate = Color.FromArgb(CByte(180), CByte(180), CByte(180))
        QuantumSwitch1.Location = New Point(139, 435)
        QuantumSwitch1.Name = "QuantumSwitch1"
        QuantumSwitch1.Size = New Size(100, 40)
        QuantumSwitch1.State = QuantumSwitch.QuantumStateEnum.Superposition
        QuantumSwitch1.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        QuantumSwitch1.TabIndex = 22
        ' 
        ' BooleanSwitch1
        ' 
        BooleanSwitch1.AnimationFPS = 0
        BooleanSwitch1.KnobColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        BooleanSwitch1.Location = New Point(46, 435)
        BooleanSwitch1.Name = "BooleanSwitch1"
        BooleanSwitch1.Size = New Size(75, 40)
        BooleanSwitch1.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        BooleanSwitch1.TabIndex = 21
        ' 
        ' ModernComboBox1
        ' 
        ModernComboBox1.BackColor2 = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        ModernComboBox1.BorderRadius = 10
        ModernComboBox1.BorderSize = 2
        ModernComboBox1.CaretColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        ModernComboBox1.DropDownBorderSize = 2
        ModernComboBox1.DropDownGap = 10
        ModernComboBox1.DropDownPadding = New Padding(10)
        ModernComboBox1.DropDownScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernComboBox1.HoverBackColor2 = Color.FromArgb(CByte(80), CByte(80), CByte(80))
        ModernComboBox1.Items.Add("现代化下拉框")
        ModernComboBox1.Items.Add("支持定制超多部件和颜色")
        ModernComboBox1.Items.Add("灵活适应各种交互场景")
        ModernComboBox1.Items.Add("av1_nvenc")
        ModernComboBox1.Items.Add("av2_nvenc")
        ToolTipEntry1.ItemText = "av1_nvenc"
        ToolTipEntry1.ToolTipText = "强烈推荐 RTX50 全系使用，强烈推荐使用 UHQ 模式"
        ModernComboBox1.ItemToolTips.Add(ToolTipEntry1)
        ModernComboBox1.Location = New Point(19, 324)
        ModernComboBox1.Name = "ModernComboBox1"
        ModernComboBox1.Padding = New Padding(15, 0, 10, 0)
        ModernComboBox1.PressedBackColor2 = SystemColors.WindowFrame
        ModernComboBox1.Size = New Size(271, 40)
        ModernComboBox1.TabIndex = 20
        ModernComboBox1.Text = "ModernComboBox1"
        ModernComboBox1.ToolTipBorderSize = 2
        ModernComboBox1.ToolTipGap = 10
        ModernComboBox1.WaterText = "水印文字"
        ' 
        ' ModernTextBox1
        ' 
        ModernTextBox1.BorderRadius = 10
        ModernTextBox1.BorderSize = 2
        ModernTextBox1.CaretColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
        ModernTextBox1.Location = New Point(19, 156)
        ModernTextBox1.MultiLine = True
        ModernTextBox1.Name = "ModernTextBox1"
        ModernTextBox1.Padding = New Padding(10)
        ModernTextBox1.ScrollBarHoverColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
        ModernTextBox1.Size = New Size(356, 150)
        ModernTextBox1.SuperSamplingScale = Class1.SuperSamplingScaleEnum.x2
        ModernTextBox1.TabIndex = 19
        ModernTextBox1.Text = "ModernTextBox1"
        ' 
        ' ModernButton1
        ' 
        ModernButton1.BackColor2 = Color.FromArgb(CByte(64), CByte(64), CByte(64))
        ModernButton1.BorderRadius = 10
        ModernButton1.BorderSize = 2
        ModernButton1.HoverBackColor1 = Color.FromArgb(CByte(48), CByte(48), CByte(48))
        ModernButton1.HoverBackColor2 = Color.FromArgb(CByte(80), CByte(80), CByte(80))
        ModernButton1.Icon = CType(resources.GetObject("ModernButton1.Icon"), Image)
        ModernButton1.IconPadding = 10
        ModernButton1.Location = New Point(19, 74)
        ModernButton1.Margin = New Padding(3, 9, 3, 9)
        ModernButton1.Name = "ModernButton1"
        ModernButton1.Size = New Size(207, 70)
        ModernButton1.SubText = "Subtitle"
        ModernButton1.TabIndex = 18
        ModernButton1.Text = "ExButton1"
        ' 
        ' ModernTabControl1
        ' 
        ModernTabControl1.Items.Add(ModernTab1)
        ModernTabControl1.Items.Add(ModernTab2)
        ModernTabControl1.Items.Add(ModernTab3)
        ModernTabControl1.Items.Add(ModernTab4)
        ModernTabControl1.Items.Add(ModernTab5)
        ModernTabControl1.Location = New Point(759, 450)
        ModernTabControl1.Name = "ModernTabControl1"
        ModernTabControl1.Size = New Size(613, 92)
        ModernTabControl1.TabAlignment = ModernTabControl.TabAlignmentEnum.Center
        ModernTabControl1.TabIndex = 25
        ModernTabControl1.TabStripHeight = 50
        ' 
        ' HtmlColorLabel1
        ' 
        HtmlColorLabel1.AutoSize = True
        HtmlColorLabel1.AutoSizeMode = AutoSizeMode.GrowAndShrink
        HtmlColorLabel1.Dock = DockStyle.Top
        HtmlColorLabel1.Font = New Font("微软雅黑", 10.2F, FontStyle.Regular, GraphicsUnit.Point, CByte(134))
        HtmlColorLabel1.Location = New Point(1, 1)
        HtmlColorLabel1.Name = "HtmlColorLabel1"
        HtmlColorLabel1.Padding = New Padding(10)
        HtmlColorLabel1.Size = New Size(302, 136)
        HtmlColorLabel1.TabIndex = 26
        HtmlColorLabel1.Text = "HtmlColorLabel1 <span style=""color:Green"">这是专用于显示高亮文字的标签控件</span> <span style=""color:CornflowerBlue"">直接写 HTML 的文字颜色标记即可</span> <span style=""color:IndianRed"">支持 HTML 自身颜色、十六进制、RGB、RGBA、HSL</span>"
        ' 
        ' ModernPanel1
        ' 
        ModernPanel1.Controls.Add(HtmlColorLabel1)
        ModernPanel1.Location = New Point(1081, 567)
        ModernPanel1.Name = "ModernPanel1"
        ModernPanel1.Size = New Size(305, 209)
        ModernPanel1.TabIndex = 27
        ' 
        ' ExcellentProgressBar1
        ' 
        ExcellentProgressBar1.AnimationFPS = 0
        ExcellentProgressBar1.FillGradientColor = Color.Fuchsia
        ExcellentProgressBar1.Location = New Point(46, 30)
        ExcellentProgressBar1.Name = "ExcellentProgressBar1"
        ExcellentProgressBar1.Size = New Size(250, 23)
        ExcellentProgressBar1.TabIndex = 28
        ExcellentProgressBar1.Value = 100
        ' 
        ' RoundDashBoard1
        ' 
        RoundDashBoard1.AnimationDuration = 1000
        RoundDashBoard1.AnimationFPS = 0
        RoundDashBoard1.CenterTextFont = New Font("Segoe UI", 14F, FontStyle.Bold)
        RoundDashBoard1.FillGradientColor = Color.FromArgb(CByte(255), CByte(128), CByte(128))
        RoundDashBoard1.Location = New Point(414, 623)
        RoundDashBoard1.Name = "RoundDashBoard1"
        RoundDashBoard1.Radius = 60F
        RoundDashBoard1.Size = New Size(182, 179)
        RoundDashBoard1.TabIndex = 29
        RoundDashBoard1.Value = 50
        ' 
        ' Form2
        ' 
        AutoScaleDimensions = New SizeF(120F, 120F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.FromArgb(CByte(24), CByte(24), CByte(24))
        ClientSize = New Size(1419, 961)
        Controls.Add(RoundDashBoard1)
        Controls.Add(ExcellentProgressBar1)
        Controls.Add(ModernPanel1)
        Controls.Add(BooleanSwitch1)
        Controls.Add(ModernTabControl1)
        Controls.Add(UltraDetailListView1)
        Controls.Add(ExcellentTrackBar1)
        Controls.Add(QuantumSwitch1)
        Controls.Add(ModernComboBox1)
        Controls.Add(ModernTextBox1)
        Controls.Add(ModernButton1)
        ForeColor = Color.Silver
        Name = "Form2"
        Text = "Form2"
        ModernPanel1.ResumeLayout(False)
        ModernPanel1.PerformLayout()
        ResumeLayout(False)
    End Sub

    Friend WithEvents UltraDetailListView1 As UltraDetailListView
    Friend WithEvents ExcellentTrackBar1 As ExcellentTrackBar
    Friend WithEvents QuantumSwitch1 As QuantumSwitch
    Friend WithEvents BooleanSwitch1 As BooleanSwitch
    Friend WithEvents ModernComboBox1 As ModernComboBox
    Friend WithEvents ModernTextBox1 As ModernTextBox
    Friend WithEvents ModernButton1 As ModernButton
    Friend WithEvents ModernTabControl1 As ModernTabControl
    Friend WithEvents HtmlColorLabel1 As HtmlColorLabel
    Friend WithEvents ModernPanel1 As ModernPanel
    Friend WithEvents ExcellentProgressBar1 As ExcellentProgressBar
    Friend WithEvents RoundDashBoard1 As RoundDashBoard
End Class
