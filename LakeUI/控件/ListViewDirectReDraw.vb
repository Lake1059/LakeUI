Public Class ListViewDirectReDraw

    Private Shared ReadOnly _options As New Dictionary(Of ListView, ListViewOption)

    ''' <summary>
    ''' 接管指定 ListView 的自定义绘制
    ''' </summary>
    Public Shared Sub TakeOver(WhichListView As ListView, Optional CustomOption As ListViewOption = Nothing)
        If CustomOption Is Nothing Then CustomOption = New ListViewOption()
        _options(WhichListView) = CustomOption
        WhichListView.OwnerDraw = True
        WhichListView.FullRowSelect = True
        WhichListView.DoubleBuffer

        AddHandler WhichListView.DrawItem, Sub(sender, e) 绘制项(sender, e)
        AddHandler WhichListView.DrawSubItem, Sub(sender, e) 绘制子项(sender, e)
        AddHandler WhichListView.DrawColumnHeader, Sub(sender, e) 绘制列标题(sender, e)
        AddHandler WhichListView.SelectedIndexChanged, Sub(sender, e) sender.Invalidate(sender.ClientRectangle)
        AddHandler WhichListView.ItemMouseHover, Sub(sender, e) sender.Invalidate(sender.ClientRectangle)
    End Sub

    ''' <summary>
    ''' 释放指定 ListView 的自定义绘制
    ''' </summary>
    Public Shared Sub Release(WhichListView As ListView)
        _options.Remove(WhichListView)
        WhichListView.OwnerDraw = False
    End Sub

    Friend Shared Function 获取选项(lv As ListView) As ListViewOption
        Dim opt As ListViewOption = Nothing
        If _options.TryGetValue(lv, opt) Then Return opt
        Return New ListViewOption()
    End Function

#Region "选项"
    ''' <summary>
    ''' ListView 自定义绘制选项
    ''' </summary>
    Public Class ListViewOption
        ''' <summary>DPI 缩放倍率</summary>
        Public Property DPI As Single = 1
        ''' <summary>选中项的背景色</summary>
        Public Property SelectedBackColor As Color = Color.FromArgb(48, 48, 48)
        ''' <summary>列标题背景色</summary>
        Public Property ColumnHeaderBackColor As Color = Color.FromArgb(30, 30, 30)
        ''' <summary>列标题文本色</summary>
        Public Property ColumnHeaderForeColor As Color = Color.Silver
        ''' <summary>列标题分割线颜色</summary>
        Public Property ColumnHeaderBorderColor As Color = Color.FromArgb(60, 60, 60)
        ''' <summary>文本左侧内边距 (会乘以 DPI)</summary>
        Public Property TextPadding As Integer = 5
        ''' <summary>文本溢出省略符号</summary>
        Public Property EllipsisText As String = "..."
    End Class
#End Region

#Region "辅助方法"
    Private Shared Function 截断文本(原始文本 As String, font As Font, 可用宽度 As Integer, opt As ListViewOption) As String
        Dim 文字尺寸 As Size = TextRenderer.MeasureText(原始文本, font)
        If 文字尺寸.Width <= (可用宽度 - 3 * opt.DPI) Then Return 原始文本
        Dim 省略号宽度 As Integer = TextRenderer.MeasureText(opt.EllipsisText, font).Width
        Dim 实际可用宽度 As Integer = 可用宽度 - 省略号宽度
        Dim result As String = 原始文本
        While TextRenderer.MeasureText(result, font).Width > 实际可用宽度 AndAlso result.Length > 0
            result = result.Substring(0, result.Length - 1)
        End While
        Return result & opt.EllipsisText
    End Function

    Private Shared Function 获取图标(imageList As ImageList, item As ListViewItem) As Image
        If imageList Is Nothing Then Return Nothing
        If item.ImageIndex >= 0 AndAlso item.ImageIndex < imageList.Images.Count Then
            Return imageList.Images(item.ImageIndex)
        ElseIf Not String.IsNullOrEmpty(item.ImageKey) AndAlso imageList.Images.ContainsKey(item.ImageKey) Then
            Return imageList.Images(item.ImageKey)
        End If
        Return Nothing
    End Function

    Private Shared Function 获取状态图标(imageList As ImageList, item As ListViewItem) As Image
        If imageList Is Nothing Then Return Nothing
        If item.StateImageIndex >= 0 AndAlso item.StateImageIndex < imageList.Images.Count Then
            Return imageList.Images(item.StateImageIndex)
        End If
        Return Nothing
    End Function
#End Region

#Region "Details 视图 - 子项绘制"
    ''' <summary>
    ''' Details 视图的子项绘制 (同时负责主项列)
    ''' </summary>
    Public Shared Sub 绘制子项(哪个列表视图控件 As ListView, e As DrawListViewSubItemEventArgs)
        Try
            If 哪个列表视图控件.View <> View.Details Then Exit Sub
            If Not e.Bounds.IntersectsWith(哪个列表视图控件.ClientRectangle) OrElse e.Bounds.Width = 0 Then Exit Sub

            Dim opt As ListViewOption = 获取选项(哪个列表视图控件)
            Dim 选中 As Boolean = 哪个列表视图控件.SelectedIndices.Contains(e.ItemIndex)
            Dim 项背景色 As Color = If(选中, opt.SelectedBackColor, 哪个列表视图控件.BackColor)

            Using brush As New SolidBrush(项背景色)
                e.Graphics.FillRectangle(brush, e.Bounds)
            End Using

            Dim padding As Integer = opt.TextPadding * opt.DPI
            Dim 图标间距 As Integer = CInt(Math.Ceiling(2.0 * opt.DPI))
            Dim 当前X As Integer = e.Bounds.X + padding

            If e.ColumnIndex = 0 Then
                Dim stateImg As Image = 获取状态图标(哪个列表视图控件.StateImageList, e.Item)
                If stateImg IsNot Nothing Then
                    Dim imgY As Integer = e.Bounds.Y + (e.Bounds.Height - stateImg.Height) \ 2
                    e.Graphics.DrawImage(stateImg, 当前X, imgY)
                    当前X += stateImg.Width + 图标间距
                End If

                Dim smallImg As Image = 获取图标(哪个列表视图控件.SmallImageList, e.Item)
                If smallImg IsNot Nothing Then
                    Dim imgY As Integer = e.Bounds.Y + (e.Bounds.Height - smallImg.Height) \ 2
                    e.Graphics.DrawImage(smallImg, 当前X, imgY)
                    当前X += smallImg.Width + 图标间距
                End If
            End If

            Dim 可用文本宽度 As Integer = e.Bounds.Right - 当前X
            Dim 文本高度修正 As Integer = (e.Bounds.Height - TextRenderer.MeasureText(e.SubItem.Text, e.SubItem.Font).Height) \ 2
            Dim 文本绘制区 As New Rectangle(当前X, e.Bounds.Y + 文本高度修正, 可用文本宽度, e.Bounds.Height)
            Dim 实际要绘制的文本 As String = 截断文本(e.SubItem.Text, e.SubItem.Font, 可用文本宽度, opt)

            Dim 文本色 As Color = If(e.SubItem.ForeColor = 哪个列表视图控件.ForeColor, e.Item.ForeColor, e.SubItem.ForeColor)
            TextRenderer.DrawText(e.Graphics, 实际要绘制的文本.Replace("&", "&&"), e.SubItem.Font, 文本绘制区, 文本色, Color.Transparent, TextFormatFlags.Default)
        Catch ex As Exception
        End Try
    End Sub
#End Region

#Region "Details 视图 - 列标题绘制"
    ''' <summary>
    ''' Details 视图列标题绘制
    ''' </summary>
    Public Shared Sub 绘制列标题(哪个列表视图控件 As ListView, e As DrawListViewColumnHeaderEventArgs)
        Try
            Dim opt As ListViewOption = 获取选项(哪个列表视图控件)

            Using brush As New SolidBrush(opt.ColumnHeaderBackColor)
                e.Graphics.FillRectangle(brush, e.Bounds)
            End Using

            Using pen As New Pen(opt.ColumnHeaderBorderColor)
                e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom)
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1)
            End Using

            Dim flags As TextFormatFlags = TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
            Select Case 哪个列表视图控件.Columns(e.ColumnIndex).TextAlign
                Case HorizontalAlignment.Left : flags = flags Or TextFormatFlags.Left
                Case HorizontalAlignment.Center : flags = flags Or TextFormatFlags.HorizontalCenter
                Case HorizontalAlignment.Right : flags = flags Or TextFormatFlags.Right
            End Select

            Dim padding As Integer = opt.TextPadding * opt.DPI
            Dim 文本区 As New Rectangle(e.Bounds.X + padding, e.Bounds.Y, e.Bounds.Width - padding * 2, e.Bounds.Height)
            TextRenderer.DrawText(e.Graphics, 哪个列表视图控件.Columns(e.ColumnIndex).Text, 哪个列表视图控件.Font, 文本区, opt.ColumnHeaderForeColor, Color.Transparent, flags)
        Catch ex As Exception
        End Try
    End Sub
#End Region

#Region "非 Details 视图 - 整项绘制"
    ''' <summary>
    ''' DrawItem 事件处理；Details 视图由 DrawSubItem 接管，此处处理其它视图模式
    ''' </summary>
    Public Shared Sub 绘制项(哪个列表视图控件 As ListView, e As DrawListViewItemEventArgs)
        Try
            If 哪个列表视图控件.View = View.Details Then Exit Sub

            Dim opt As ListViewOption = 获取选项(哪个列表视图控件)
            Dim 选中 As Boolean = e.Item.Selected
            Dim 项背景色 As Color = If(选中, opt.SelectedBackColor, 哪个列表视图控件.BackColor)

            Using brush As New SolidBrush(项背景色)
                e.Graphics.FillRectangle(brush, e.Bounds)
            End Using

            Select Case 哪个列表视图控件.View
                Case View.LargeIcon
                    绘制大图标模式(哪个列表视图控件, e, opt)
                Case View.SmallIcon, View.List
                    绘制小图标模式(哪个列表视图控件, e, opt)
                Case View.Tile
                    绘制平铺模式(哪个列表视图控件, e, opt)
            End Select
        Catch ex As Exception
        End Try
    End Sub

    Private Shared Sub 绘制大图标模式(lv As ListView, e As DrawListViewItemEventArgs, opt As ListViewOption)
        Dim padding As Integer = opt.TextPadding * opt.DPI
        Dim img As Image = 获取图标(lv.LargeImageList, e.Item)
        If img IsNot Nothing Then
            Dim imgX As Integer = e.Bounds.X + (e.Bounds.Width - img.Width) \ 2
            Dim imgY As Integer = e.Bounds.Y + padding
            e.Graphics.DrawImage(img, imgX, imgY)
            Dim 文本区 As New Rectangle(e.Bounds.X, imgY + img.Height + 2 * opt.DPI, e.Bounds.Width, e.Bounds.Height - img.Height - padding - 2 * opt.DPI)
            TextRenderer.DrawText(e.Graphics, e.Item.Text, e.Item.Font, 文本区, e.Item.ForeColor, Color.Transparent,
                TextFormatFlags.HorizontalCenter Or TextFormatFlags.Top Or TextFormatFlags.EndEllipsis Or TextFormatFlags.WordBreak)
        Else
            TextRenderer.DrawText(e.Graphics, e.Item.Text, e.Item.Font, e.Bounds, e.Item.ForeColor, Color.Transparent,
                TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
        End If
    End Sub

    Private Shared Sub 绘制小图标模式(lv As ListView, e As DrawListViewItemEventArgs, opt As ListViewOption)
        Dim padding As Integer = opt.TextPadding * opt.DPI
        Dim img As Image = 获取图标(lv.SmallImageList, e.Item)
        Dim textX As Integer = e.Bounds.X + padding
        If img IsNot Nothing Then
            Dim imgY As Integer = e.Bounds.Y + (e.Bounds.Height - img.Height) \ 2
            e.Graphics.DrawImage(img, e.Bounds.X + padding, imgY)
            textX = e.Bounds.X + padding + img.Width + padding
        End If
        Dim 文本区 As New Rectangle(textX, e.Bounds.Y, e.Bounds.Width - (textX - e.Bounds.X), e.Bounds.Height)
        TextRenderer.DrawText(e.Graphics, e.Item.Text, e.Item.Font, 文本区, e.Item.ForeColor, Color.Transparent,
            TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine)
    End Sub

    Private Shared Sub 绘制平铺模式(lv As ListView, e As DrawListViewItemEventArgs, opt As ListViewOption)
        Dim padding As Integer = opt.TextPadding * opt.DPI
        Dim img As Image = 获取图标(lv.LargeImageList, e.Item)
        Dim textX As Integer = e.Bounds.X + padding
        If img IsNot Nothing Then
            Dim imgY As Integer = e.Bounds.Y + (e.Bounds.Height - img.Height) \ 2
            e.Graphics.DrawImage(img, e.Bounds.X + padding, imgY)
            textX = e.Bounds.X + padding + img.Width + padding
        End If

        Dim 文本宽度 As Integer = e.Bounds.Width - (textX - e.Bounds.X) - padding
        Dim titleHeight As Integer = TextRenderer.MeasureText(e.Item.Text, e.Item.Font).Height
        Dim titleRect As New Rectangle(textX, e.Bounds.Y + padding, 文本宽度, titleHeight)
        TextRenderer.DrawText(e.Graphics, e.Item.Text, e.Item.Font, titleRect, e.Item.ForeColor, Color.Transparent,
            TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine)

        Dim currentY As Integer = titleRect.Bottom + opt.DPI
        For i As Integer = 1 To e.Item.SubItems.Count - 1
            If currentY >= e.Bounds.Bottom - padding Then Exit For
            Dim subText As String = e.Item.SubItems(i).Text
            If String.IsNullOrEmpty(subText) Then Continue For
            Dim subHeight As Integer = TextRenderer.MeasureText(subText, lv.Font).Height
            Dim subRect As New Rectangle(textX, currentY, 文本宽度, subHeight)
            Dim subColor As Color = If(e.Item.SubItems(i).ForeColor = lv.ForeColor, Color.Gray, e.Item.SubItems(i).ForeColor)
            TextRenderer.DrawText(e.Graphics, subText, lv.Font, subRect, subColor, Color.Transparent,
                TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine)
            currentY = subRect.Bottom + opt.DPI
        Next
    End Sub
#End Region

End Class
