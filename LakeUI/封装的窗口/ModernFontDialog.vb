Imports System.Drawing.Text

Public Class ModernFontDialog

#Region "公共属性"

    ''' <summary>
    ''' 获取或设置用户选择的字体。
    ''' </summary>
    <ComponentModel.DesignerSerializationVisibility(ComponentModel.DesignerSerializationVisibility.Hidden)>
    <ComponentModel.Browsable(False)>
    Public Property SelectedFont As Font = New Font("Microsoft YaHei UI", 10.0F)

#End Region

#Region "私有字段"

    Private _fontFamilies As FontFamily()
    Private _allFontNames As List(Of String)
    Private _suppressTextBoxEvent As Boolean = False
    Private _suppressListBoxEvent As Boolean = False

    ' 标准字号列表
    Private ReadOnly _standardSizes As String() = {
        "8", "9", "10", "11", "12", "14", "16", "18",
        "20", "22", "24", "26", "28", "36", "48", "72"
    }

#End Region

#Region "初始化"

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)

        ' 获取所有已安装字体
        Using ifc As New InstalledFontCollection()
            _fontFamilies = ifc.Families
        End Using
        _allFontNames = _fontFamilies.Select(Function(f) f.Name).ToList()

        ' 填充字体列表
        ModernListBox1.Items.Clear()
        For Each fontName In _allFontNames
            ModernListBox1.Items.Add(fontName)
        Next

        ' 填充字号列表
        ModernListBox3.Items.Clear()
        For Each sz In _standardSizes
            ModernListBox3.Items.Add(sz)
        Next

        ' 根据当前 SelectedFont 初始化选中状态
        ApplyFontToUI(SelectedFont)

        ' 绑定事件
        AddHandler ModernListBox1.SelectedIndexChanged, AddressOf FontList_SelectedIndexChanged
        AddHandler ModernListBox2.SelectedIndexChanged, AddressOf StyleList_SelectedIndexChanged
        AddHandler ModernListBox3.SelectedIndexChanged, AddressOf SizeList_SelectedIndexChanged

        AddHandler ModernTextBox1.TextChanged, AddressOf FontTextBox_TextChanged
        AddHandler ModernTextBox2.TextChanged, AddressOf StyleTextBox_TextChanged
        AddHandler ModernTextBox3.TextChanged, AddressOf SizeTextBox_TextChanged

        AddHandler BooleanSwitch1.CheckedChanged, AddressOf Effect_Changed
        AddHandler BooleanSwitch2.CheckedChanged, AddressOf Effect_Changed

        AddHandler ModernButton2.Click, AddressOf OkButton_Click
        AddHandler ModernButton1.Click, AddressOf CancelButton_Click
    End Sub

    ''' <summary>
    ''' 将一个 Font 对象反映到界面上。
    ''' </summary>
    Private Sub ApplyFontToUI(f As Font)
        _suppressTextBoxEvent = True
        _suppressListBoxEvent = True

        ' 选中字体名
        Dim familyName As String = f.FontFamily.Name
        ModernTextBox1.Text = familyName
        SelectListItem(ModernListBox1, familyName)

        ' 填充并选中字形
        PopulateStyles(familyName)
        Dim styleName As String = GetStyleName(f.Style)
        ModernTextBox2.Text = styleName
        SelectListItem(ModernListBox2, styleName)

        ' 选中字号
        Dim sizeStr As String = f.SizeInPoints.ToString("0.##")
        ModernTextBox3.Text = sizeStr
        SelectListItem(ModernListBox3, sizeStr)

        ' 效果
        BooleanSwitch1.Checked = f.Strikeout
        BooleanSwitch2.Checked = f.Underline

        _suppressListBoxEvent = False
        _suppressTextBoxEvent = False

        UpdatePreview()
    End Sub

#End Region

#Region "字体列表"

    Private Sub FontTextBox_TextChanged(sender As Object, e As EventArgs)
        If _suppressTextBoxEvent Then Return

        Dim filter As String = ModernTextBox1.Text.Trim()

        ' 筛选字体列表
        _suppressListBoxEvent = True
        ModernListBox1.Items.Clear()
        If String.IsNullOrEmpty(filter) Then
            For Each fontName In _allFontNames
                ModernListBox1.Items.Add(fontName)
            Next
        Else
            For Each fontName In _allFontNames
                If fontName.Contains(filter, StringComparison.OrdinalIgnoreCase) Then
                    ModernListBox1.Items.Add(fontName)
                End If
            Next
        End If

        ' 尝试精确匹配并选中
        Dim exactIdx As Integer = -1
        For i = 0 To ModernListBox1.Items.Count - 1
            If String.Equals(ModernListBox1.Items(i), filter, StringComparison.OrdinalIgnoreCase) Then
                exactIdx = i
                Exit For
            End If
        Next
        If exactIdx >= 0 Then
            ModernListBox1.SelectedIndex = exactIdx
            ModernListBox1.EnsureVisible(exactIdx)
        End If
        _suppressListBoxEvent = False

        UpdateFontFromUI()
    End Sub

    Private Sub FontList_SelectedIndexChanged(sender As Object, e As EventArgs)
        If _suppressListBoxEvent Then Return

        Dim sel As String = ModernListBox1.SelectedItem
        If sel Is Nothing Then Return

        _suppressTextBoxEvent = True
        ModernTextBox1.Text = sel
        _suppressTextBoxEvent = False

        ' 重新填充字形列表
        PopulateStyles(sel)

        UpdateFontFromUI()
    End Sub

#End Region

#Region "字形列表"

    Private Sub PopulateStyles(familyName As String)
        Dim family As FontFamily = Nothing
        Try
            family = _fontFamilies.FirstOrDefault(Function(f) String.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase))
        Catch
        End Try

        Dim prevSuppressList = _suppressListBoxEvent
        Dim prevSuppressText = _suppressTextBoxEvent
        _suppressListBoxEvent = True
        _suppressTextBoxEvent = True

        Dim prevStyle As String = ModernTextBox2.Text

        ModernListBox2.Items.Clear()

        If family IsNot Nothing Then
            If family.IsStyleAvailable(Drawing.FontStyle.Regular) Then ModernListBox2.Items.Add("常规")
            If family.IsStyleAvailable(Drawing.FontStyle.Bold) Then ModernListBox2.Items.Add("粗体")
            If family.IsStyleAvailable(Drawing.FontStyle.Italic) Then ModernListBox2.Items.Add("斜体")
            If family.IsStyleAvailable(Drawing.FontStyle.Bold Or Drawing.FontStyle.Italic) Then ModernListBox2.Items.Add("粗斜体")
        End If

        ' 尝试重新选中之前的字形
        Dim foundIdx As Integer = -1
        For i = 0 To ModernListBox2.Items.Count - 1
            If String.Equals(ModernListBox2.Items(i), prevStyle, StringComparison.OrdinalIgnoreCase) Then
                foundIdx = i
                Exit For
            End If
        Next
        If foundIdx < 0 AndAlso ModernListBox2.Items.Count > 0 Then foundIdx = 0

        If foundIdx >= 0 Then
            ModernListBox2.SelectedIndex = foundIdx
            ModernTextBox2.Text = ModernListBox2.Items(foundIdx)
        End If

        _suppressListBoxEvent = prevSuppressList
        _suppressTextBoxEvent = prevSuppressText
    End Sub

    Private Sub StyleTextBox_TextChanged(sender As Object, e As EventArgs)
        If _suppressTextBoxEvent Then Return

        Dim filter As String = ModernTextBox2.Text.Trim()
        _suppressListBoxEvent = True
        Dim exactIdx As Integer = -1
        For i = 0 To ModernListBox2.Items.Count - 1
            If String.Equals(ModernListBox2.Items(i), filter, StringComparison.OrdinalIgnoreCase) Then
                exactIdx = i
                Exit For
            End If
        Next
        If exactIdx >= 0 Then
            ModernListBox2.SelectedIndex = exactIdx
            ModernListBox2.EnsureVisible(exactIdx)
        End If
        _suppressListBoxEvent = False

        UpdateFontFromUI()
    End Sub

    Private Sub StyleList_SelectedIndexChanged(sender As Object, e As EventArgs)
        If _suppressListBoxEvent Then Return

        Dim sel As String = ModernListBox2.SelectedItem
        If sel Is Nothing Then Return

        _suppressTextBoxEvent = True
        ModernTextBox2.Text = sel
        _suppressTextBoxEvent = False

        UpdateFontFromUI()
    End Sub

#End Region

#Region "字号列表"

    Private Sub SizeTextBox_TextChanged(sender As Object, e As EventArgs)
        If _suppressTextBoxEvent Then Return

        Dim filter As String = ModernTextBox3.Text.Trim()
        _suppressListBoxEvent = True
        Dim exactIdx As Integer = -1
        For i = 0 To ModernListBox3.Items.Count - 1
            If String.Equals(ModernListBox3.Items(i), filter, StringComparison.OrdinalIgnoreCase) Then
                exactIdx = i
                Exit For
            End If
        Next
        If exactIdx >= 0 Then
            ModernListBox3.SelectedIndex = exactIdx
            ModernListBox3.EnsureVisible(exactIdx)
        End If
        _suppressListBoxEvent = False

        UpdateFontFromUI()
    End Sub

    Private Sub SizeList_SelectedIndexChanged(sender As Object, e As EventArgs)
        If _suppressListBoxEvent Then Return

        Dim sel As String = ModernListBox3.SelectedItem
        If sel Is Nothing Then Return

        _suppressTextBoxEvent = True
        ModernTextBox3.Text = sel
        _suppressTextBoxEvent = False

        UpdateFontFromUI()
    End Sub

#End Region

#Region "效果"

    Private Sub Effect_Changed(sender As Object, e As EventArgs)
        UpdateFontFromUI()
    End Sub

#End Region

#Region "构建字体 / 更新预览"

    Private Sub UpdateFontFromUI()
        Dim familyName As String = ModernTextBox1.Text.Trim()
        Dim styleName As String = ModernTextBox2.Text.Trim()
        Dim sizeText As String = ModernTextBox3.Text.Trim()

        ' 解析字号
        Dim fontSize As Single = 10.0F
        Dim unused = Single.TryParse(sizeText, fontSize)
        If fontSize < 1 Then fontSize = 1
        If fontSize > 999 Then fontSize = 999

        ' 解析字形
        Dim fontStyle As Drawing.FontStyle = ParseStyleName(styleName)

        ' 删除线 / 下划线
        If BooleanSwitch1.Checked Then fontStyle = fontStyle Or Drawing.FontStyle.Strikeout
        If BooleanSwitch2.Checked Then fontStyle = fontStyle Or Drawing.FontStyle.Underline

        ' 尝试创建字体
        Try
            Dim family As FontFamily = Nothing
            Try
                family = _fontFamilies.FirstOrDefault(Function(f) String.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase))
            Catch
            End Try

            If family IsNot Nothing Then
                ' 确保字形可用，否则回退
                Dim baseStyle As Drawing.FontStyle = fontStyle And (Drawing.FontStyle.Bold Or Drawing.FontStyle.Italic Or Drawing.FontStyle.Regular)
                If Not family.IsStyleAvailable(baseStyle) Then
                    If family.IsStyleAvailable(Drawing.FontStyle.Regular) Then
                        baseStyle = Drawing.FontStyle.Regular
                    ElseIf family.IsStyleAvailable(Drawing.FontStyle.Bold) Then
                        baseStyle = Drawing.FontStyle.Bold
                    ElseIf family.IsStyleAvailable(Drawing.FontStyle.Italic) Then
                        baseStyle = Drawing.FontStyle.Italic
                    End If
                    fontStyle = baseStyle
                    If BooleanSwitch1.Checked Then fontStyle = fontStyle Or Drawing.FontStyle.Strikeout
                    If BooleanSwitch2.Checked Then fontStyle = fontStyle Or Drawing.FontStyle.Underline
                End If

                Dim newFont As New Font(family, fontSize, fontStyle, GraphicsUnit.Point)
                SelectedFont = newFont
            End If
        Catch
            ' 字体创建失败时保留上次有效字体
        End Try

        UpdatePreview()
    End Sub

    Private Sub UpdatePreview()
        Dim f As Font = SelectedFont
        If f Is Nothing Then Return

        Label13.Font = f
        Label14.Font = f
        Label15.Font = f
        Label16.Font = f
    End Sub

#End Region

#Region "确定 / 取消"

    Private Sub OkButton_Click(sender As Object, e As EventArgs)
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    Private Sub CancelButton_Click(sender As Object, e As EventArgs)
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

#End Region

#Region "辅助方法"

    Private Shared Function GetStyleName(style As Drawing.FontStyle) As String
        Dim baseStyle As Drawing.FontStyle = style And (Drawing.FontStyle.Bold Or Drawing.FontStyle.Italic)
        Select Case baseStyle
            Case Drawing.FontStyle.Bold Or Drawing.FontStyle.Italic
                Return "粗斜体"
            Case Drawing.FontStyle.Bold
                Return "粗体"
            Case Drawing.FontStyle.Italic
                Return "斜体"
            Case Else
                Return "常规"
        End Select
    End Function

    Private Shared Function ParseStyleName(name As String) As Drawing.FontStyle
        Select Case name
            Case "粗体"
                Return Drawing.FontStyle.Bold
            Case "斜体"
                Return Drawing.FontStyle.Italic
            Case "粗斜体"
                Return Drawing.FontStyle.Bold Or Drawing.FontStyle.Italic
            Case Else
                Return Drawing.FontStyle.Regular
        End Select
    End Function

    Private Shared Sub SelectListItem(lb As ModernListBox, text As String)
        For i = 0 To lb.Items.Count - 1
            If String.Equals(lb.Items(i), text, StringComparison.OrdinalIgnoreCase) Then
                lb.SelectedIndex = i
                lb.EnsureVisible(i)
                Return
            End If
        Next
    End Sub

#End Region

End Class