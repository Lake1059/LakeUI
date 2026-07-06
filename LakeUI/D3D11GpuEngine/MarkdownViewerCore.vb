Imports System.ComponentModel
Imports System.Net.Http
Imports System.Numerics
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Text.RegularExpressions
Imports Vortice.Direct2D1

''' <summary>
''' 轻量级 Markdown 渲染控件，只读显示，支持流式追加（AI 场景）、文本选中和可扩展的格式解析。
''' </summary>
<DefaultEvent("LinkClicked")>
Public Class MarkdownViewerCore
    Inherits UserControl
    Implements D3D_IRenderCacheOwner, V3_IGpuRenderable, V3_IGpuInvalidationSource

    Public Event LinkClicked As EventHandler(Of LinkClickedEventArgs)

    Private Shared ReadOnly _instancesLock As New Object()
    Private Shared ReadOnly _instances As New List(Of WeakReference(Of MarkdownViewerCore))()

#Region "Markdown 默认样式"

    Public Shared ReadOnly DefaultMarkdownHeadingColor As Color = Color.Silver
    Public Shared ReadOnly DefaultMarkdownHeadingSeparatorColor As Color = Color.FromArgb(80, 220, 220, 220)
    Public Const DefaultMarkdownHeadingSeparatorThickness As Integer = 2
    Public Const DefaultMarkdownHeadingSeparatorGap As Integer = 4
    Public Shared ReadOnly DefaultMarkdownBoldColor As Color = Color.Empty
    Public Shared ReadOnly DefaultMarkdownItalicColor As Color = Color.Empty
    Public Shared ReadOnly DefaultMarkdownInlineCodeColor As Color = Color.FromArgb(206, 145, 120)
    Public Shared ReadOnly DefaultMarkdownCodeBackColor As Color = Color.FromArgb(120, 0, 0, 0)
    Public Shared ReadOnly DefaultMarkdownCodeBlockForeColor As Color = Color.Silver
    Public Shared ReadOnly DefaultMarkdownInlineCodePadding As New Padding(5, 3, 5, 3)
    Public Const DefaultMarkdownInlineCodeRadius As Integer = 3
    Public Shared ReadOnly DefaultMarkdownCodeBlockPadding As New Padding(7, 5, 7, 5)
    Public Shared ReadOnly DefaultMarkdownLinkColor As Color = Color.FromArgb(86, 156, 214)
    Public Const DefaultMarkdownLinkUnderlineThickness As Integer = 1
    Public Const DefaultMarkdownLinkUnderlineOffset As Integer = 3
    Public Shared ReadOnly DefaultMarkdownStrikethroughColor As Color = Color.Gray
    Public Const DefaultMarkdownStrikethroughThickness As Integer = 1
    Public Shared ReadOnly DefaultMarkdownBlockQuoteBarColor As Color = Color.FromArgb(80, 220, 220, 220)
    Public Shared ReadOnly DefaultMarkdownBlockQuoteForeColor As Color = Color.FromArgb(100, 255, 255, 255)
    Public Shared ReadOnly DefaultMarkdownAlertNoteColor As Color = Color.FromArgb(83, 155, 245)
    Public Shared ReadOnly DefaultMarkdownAlertTipColor As Color = Color.FromArgb(87, 171, 90)
    Public Shared ReadOnly DefaultMarkdownAlertImportantColor As Color = Color.FromArgb(152, 110, 226)
    Public Shared ReadOnly DefaultMarkdownAlertWarningColor As Color = Color.FromArgb(198, 144, 38)
    Public Shared ReadOnly DefaultMarkdownAlertCautionColor As Color = Color.FromArgb(229, 83, 75)
    Public Shared ReadOnly DefaultMarkdownHorizontalRuleColor As Color = Color.FromArgb(60, 60, 60)
    Public Const DefaultMarkdownHorizontalRuleThickness As Integer = 1
    Public Const DefaultMarkdownHorizontalRulePadding As Integer = 4
    Public Const DefaultMarkdownHorizontalRuleInset As Integer = 4
    Public Const DefaultMarkdownBlockQuoteIndent As Integer = 16
    Public Const DefaultMarkdownBlockQuoteBarOffset As Integer = 4
    Public Const DefaultMarkdownBlockQuoteBarWidth As Integer = 3
    Public Const DefaultMarkdownUnorderedListIndent As Integer = 20
    Public Const DefaultMarkdownOrderedListIndent As Integer = 24
    Public Const DefaultMarkdownBulletRadius As Integer = 3
    Public Const DefaultMarkdownBulletOffsetX As Integer = 6
    Public Const DefaultMarkdownBulletOffsetY As Integer = -2
    Public Const DefaultMarkdownOrderedListMarkerWidth As Integer = 22
    Public Shared ReadOnly DefaultMarkdownTableBorderColor As Color = Color.FromArgb(80, 220, 220, 220)
    Public Shared ReadOnly DefaultMarkdownTableHeaderBackColor As Color = Color.FromArgb(40, 220, 220, 220)
    Public Const DefaultMarkdownTableCellPadding As Integer = 7
    Public Const DefaultMarkdownTableBorderThickness As Integer = 1
    Public Shared ReadOnly DefaultMarkdownImagePlaceholderBorderColor As Color = Color.FromArgb(40, 220, 220, 220)
    Public Shared ReadOnly DefaultMarkdownImagePlaceholderTextColor As Color = Color.FromArgb(80, 220, 220, 220)
    Public Const DefaultMarkdownImagePlaceholderWidth As Integer = 300
    Public Const DefaultMarkdownImagePlaceholderHeightLines As Integer = 2
    Public Const DefaultMarkdownBlockSpacing As Integer = 20
    Public Const DefaultMarkdownInlineLineSpacing As Integer = 4

#End Region

#Region "文档模型"

    ''' <summary>内联元素类型。</summary>
    Public Enum InlineKind
        Text
        Bold
        Italic
        BoldItalic
        Code
        Link
        Strikethrough
        Image
        LineBreak
    End Enum

    ''' <summary>块级元素类型。</summary>
    Public Enum BlockKind
        Paragraph
        Heading1
        Heading2
        Heading3
        Heading4
        Heading5
        Heading6
        CodeBlock
        BlockQuote
        UnorderedListItem
        OrderedListItem
        HorizontalRule
        BlankLine
        Table
    End Enum

    ''' <summary>表格列对齐方式。</summary>
    Public Enum TableAlignment
        Left
        Center
        Right
    End Enum

    ''' <summary>GitHub 风格提示块类型。</summary>
    Public Enum AlertKind
        None
        Note
        Tip
        Important
        Warning
        Caution
    End Enum

    ''' <summary>内联元素。</summary>
    Public Class MarkdownInline
        Public Property Kind As InlineKind
        Public Property Text As String
        Public Property Url As String
        ''' <summary>HTML img 指定的宽度（像素），0 表示自动。</summary>
        Public Property ImageWidth As Integer
        ''' <summary>HTML img 指定的高度（像素），0 表示自动。</summary>
        Public Property ImageHeight As Integer
        Public Sub New(kind As InlineKind, text As String, Optional url As String = Nothing)
            Me.Kind = kind
            Me.Text = text
            Me.Url = url
        End Sub
    End Class

    ''' <summary>表格单元格。</summary>
    Public Class MarkdownTableCell
        Public Property Inlines As List(Of MarkdownInline)
        Public Sub New()
            Me.Inlines = New List(Of MarkdownInline)
        End Sub
    End Class

    ''' <summary>块级元素。</summary>
    Public Class MarkdownBlock
        Public Property Kind As BlockKind
        Public Property Inlines As List(Of MarkdownInline)
        Public Property RawText As String
        Public Property OrderIndex As Integer
        Public Property ListLevel As Integer
        ''' <summary>表格行数据（Kind=Table 时有效）。每个元素是一行的单元格列表。</summary>
        Public Property TableRows As List(Of List(Of MarkdownTableCell))
        ''' <summary>表格列对齐（Kind=Table 时有效）。</summary>
        Public Property ColumnAlignments As List(Of TableAlignment)
        ''' <summary>表格是否有表头行（Kind=Table 时有效）。</summary>
        Public Property HasHeader As Boolean
        ''' <summary>GitHub 风格提示块类型（Kind=BlockQuote 时有效）。</summary>
        Public Property AlertKind As AlertKind
        ''' <summary>是否为提示块的标题行（显示类型标签）。</summary>
        Public Property IsAlertHeader As Boolean
        Public Sub New(kind As BlockKind, Optional rawText As String = Nothing)
            Me.Kind = kind
            Me.Inlines = New List(Of MarkdownInline)
            Me.RawText = rawText
        End Sub
    End Class

    ''' <summary>解析后的文档。</summary>
    Public Class MarkdownDocument
        Public Property Blocks As New List(Of MarkdownBlock)
    End Class

#End Region

#Region "解析器"

    ''' <summary>
    ''' 可扩展的 Markdown 解析器基类。
    ''' 派生类可重写 ParseBlocks / ParseInlines 以支持更多格式或 HTML 标签。
    ''' </summary>
    Public Class MarkdownParser
        Private Shared ReadOnly 标题匹配 As New Regex("^(#{1,6})\s+(.*)", RegexOptions.Compiled)
        Private Shared ReadOnly 代码块开始 As New Regex("^```\S*\s*$", RegexOptions.Compiled)
        Private Shared ReadOnly 无序列表匹配 As New Regex("^(?<indent>[ \t]*)[\-\*\+]\s+(?<text>.*)", RegexOptions.Compiled)
        Private Shared ReadOnly 有序列表匹配 As New Regex("^(?<indent>[ \t]*)(?<order>\d+)\.\s+(?<text>.*)", RegexOptions.Compiled)
        Private Shared ReadOnly 分隔线匹配 As New Regex("^(\*{3,}|-{3,}|_{3,})$", RegexOptions.Compiled)
        Private Shared ReadOnly 引用匹配 As New Regex("^>\s?(.*)", RegexOptions.Compiled)
        Private Shared ReadOnly 表格分隔匹配 As New Regex("^\|?(\s*:?-+:?\s*\|)+\s*:?-+:?\s*\|?\s*$", RegexOptions.Compiled)
        Private Shared ReadOnly 提示块匹配 As New Regex("^\s*\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION)\]\s*$", RegexOptions.Compiled Or RegexOptions.IgnoreCase)

        Private Shared ReadOnly 图片src匹配
        Private Shared ReadOnly 图片width匹配 As New Regex("width\s*=\s*[""']?(\d+)[""']?", RegexOptions.Compiled Or RegexOptions.IgnoreCase)
        Private Shared ReadOnly 图片height匹配 As New Regex("height\s*=\s*[""']?(\d+)[""']?", RegexOptions.Compiled Or RegexOptions.IgnoreCase)
        Private Shared ReadOnly 图片alt匹配 As New Regex("alt\s*=\s*[""']([^""']*)[""']", RegexOptions.Compiled Or RegexOptions.IgnoreCase)

        Private Shared ReadOnly 内联匹配 As New Regex(
            "(?<bolditalic>\*{3}(?=\S)(?<bolditalic_text>.+?)(?<=\S)\*{3})" &
            "|(?<bold>\*{2}(?=\S)(?<bold_text>.+?)(?<=\S)\*{2})" &
            "|(?<italic>\*(?=\S)(?<italic_text>.+?)(?<=\S)\*)" &
            "|(?<strike>~~(?=\S)(?<strike_text>.+?)(?<=\S)~~)" &
            "|(?<code>`(?<code_text>[^`]+?)`)" &
            "|(?<image>!\[(?<image_alt>[^\]]*)\]\((?<image_url>[^\)]+)\))" &
            "|(?<link>\[(?<link_text>[^\]]+)\]\((?<link_url>[^\)]+)\))" &
            "|(?<autolink>(?:https?://|ftp://|www\.)\S+)" &
            "|(?<htmlimg><(?i:img)\s[^>]+?>)" &
            "|(?<br><(?i:br)\s*/?>)",
            RegexOptions.Compiled Or RegexOptions.Singleline)

        Public Overridable Function Parse(markdown As String) As MarkdownDocument
            Dim doc As New MarkdownDocument()
            If String.IsNullOrEmpty(markdown) Then Return doc
            doc.Blocks = ParseBlocks(markdown.Replace(vbCr, "").Split(vbLf))
            Return doc
        End Function

        ''' <summary>将行数组解析为块列表，可重写以支持新块类型。</summary>
        Public Overridable Function ParseBlocks(lines As String()) As List(Of MarkdownBlock)
            Dim blocks As New List(Of MarkdownBlock)
            Dim i As Integer = 0
            Dim currentAlertKind As AlertKind = AlertKind.None
            Dim listIndentStack As New List(Of Integer)
            While i < lines.Length
                Dim line As String = lines(i)

                If String.IsNullOrWhiteSpace(line) Then
                    currentAlertKind = AlertKind.None
                    listIndentStack.Clear()
                    blocks.Add(New MarkdownBlock(BlockKind.BlankLine))
                    i += 1
                    Continue While
                End If

                If 代码块开始.IsMatch(line) Then
                    currentAlertKind = AlertKind.None
                    listIndentStack.Clear()
                    Dim sb As New StringBuilder()
                    i += 1
                    While i < lines.Length AndAlso Not lines(i).StartsWith("```")
                        If sb.Length > 0 Then sb.Append(vbLf)
                        sb.Append(lines(i))
                        i += 1
                    End While
                    If i < lines.Length Then i += 1
                    Dim b As New MarkdownBlock(BlockKind.CodeBlock, sb.ToString())
                    blocks.Add(b)
                    Continue While
                End If

                If 分隔线匹配.IsMatch(line.Trim()) Then
                    currentAlertKind = AlertKind.None
                    listIndentStack.Clear()
                    blocks.Add(New MarkdownBlock(BlockKind.HorizontalRule))
                    i += 1
                    Continue While
                End If

                ' 表格检测：当前行包含 | 且下一行是分隔行
                If line.Contains("|"c) AndAlso i + 1 < lines.Length AndAlso 表格分隔匹配.IsMatch(lines(i + 1)) Then
                    currentAlertKind = AlertKind.None
                    listIndentStack.Clear()
                    Dim tableBlock = ParseTable(lines, i)
                    If tableBlock IsNot Nothing Then
                        blocks.Add(tableBlock.Block)
                        i = tableBlock.NextIndex
                        Continue While
                    End If
                End If

                Dim b2 = TryParseInlineBlock(line, listIndentStack)
                If b2 IsNot Nothing Then
                    If b2.Kind <> BlockKind.UnorderedListItem AndAlso b2.Kind <> BlockKind.OrderedListItem Then
                        listIndentStack.Clear()
                    End If
                    If b2.Kind = BlockKind.BlockQuote Then
                        Dim alertM = 提示块匹配.Match(If(b2.RawText, ""))
                        If alertM.Success Then
                            Dim ak = ParseAlertKindFromText(alertM.Groups(1).Value)
                            If ak <> AlertKind.None Then
                                currentAlertKind = ak
                                b2.AlertKind = ak
                                b2.IsAlertHeader = True
                                Dim label = GetAlertLabel(ak)
                                b2.RawText = label
                                b2.Inlines.Clear()
                                b2.Inlines.Add(New MarkdownInline(InlineKind.Bold, label))
                            End If
                        ElseIf currentAlertKind <> AlertKind.None Then
                            b2.AlertKind = currentAlertKind
                        End If
                    Else
                        currentAlertKind = AlertKind.None
                    End If
                    blocks.Add(b2)
                    i += 1
                    Continue While
                End If

                currentAlertKind = AlertKind.None
                listIndentStack.Clear()
                Dim trimmedLine = line.TrimEnd()
                Dim mergedInlines = ParseInlines(trimmedLine)
                ' 末尾 2+ 空格视为硬换行
                If line.Length >= 2 AndAlso line.EndsWith("  ") Then
                    mergedInlines.Add(New MarkdownInline(InlineKind.LineBreak, ""))
                End If
                ' 只要当前段落以 LineBreak 结尾（<br> 或末尾空格），就与下一行合并为同一段落
                While mergedInlines.Count > 0 AndAlso
                      mergedInlines(mergedInlines.Count - 1).Kind = InlineKind.LineBreak AndAlso
                      i + 1 < lines.Length
                    Dim nextLine = lines(i + 1)
                    If IsSpecialLine(nextLine) Then Exit While
                    If nextLine.Contains("|"c) AndAlso i + 2 < lines.Length AndAlso 表格分隔匹配.IsMatch(lines(i + 2)) Then Exit While
                    i += 1
                    line = lines(i)
                    trimmedLine = line.TrimEnd()
                    mergedInlines.AddRange(ParseInlines(trimmedLine))
                    If line.Length >= 2 AndAlso line.EndsWith("  ") Then
                        mergedInlines.Add(New MarkdownInline(InlineKind.LineBreak, ""))
                    End If
                End While
                blocks.Add(New MarkdownBlock(BlockKind.Paragraph, trimmedLine) With {
                    .Inlines = mergedInlines
                })
                i += 1
            End While
            Return blocks
        End Function

        ''' <summary>尝试将行解析为标题/引用/列表等带内联内容的块，返回 Nothing 表示不匹配。</summary>
        Private Function TryParseInlineBlock(line As String, listIndentStack As List(Of Integer)) As MarkdownBlock
            Dim hm = 标题匹配.Match(line)
            If hm.Success Then
                Dim level As Integer = hm.Groups(1).Length
                Return New MarkdownBlock(CType(BlockKind.Heading1 + level - 1, BlockKind), hm.Groups(2).Value) With {
                    .Inlines = ParseInlines(hm.Groups(2).Value)
                }
            End If

            Dim qm = 引用匹配.Match(line)
            If qm.Success Then
                Return New MarkdownBlock(BlockKind.BlockQuote, qm.Groups(1).Value) With {
                    .Inlines = ParseInlines(qm.Groups(1).Value)
                }
            End If

            Dim ulm = 无序列表匹配.Match(line)
            If ulm.Success Then
                Dim itemText = ulm.Groups("text").Value
                Dim level = ResolveListLevel(CountIndentColumns(ulm.Groups("indent").Value), listIndentStack)
                Return New MarkdownBlock(BlockKind.UnorderedListItem, itemText) With {
                    .ListLevel = level,
                    .Inlines = ParseInlines(itemText)
                }
            End If

            Dim olm = 有序列表匹配.Match(line)
            If olm.Success Then
                Dim itemText = olm.Groups("text").Value
                Dim level = ResolveListLevel(CountIndentColumns(olm.Groups("indent").Value), listIndentStack)
                Return New MarkdownBlock(BlockKind.OrderedListItem, itemText) With {
                    .OrderIndex = Integer.Parse(olm.Groups("order").Value),
                    .ListLevel = level,
                    .Inlines = ParseInlines(itemText)
                }
            End If

            Return Nothing
        End Function

        Private Shared Function CountIndentColumns(indentText As String) As Integer
            If String.IsNullOrEmpty(indentText) Then Return 0
            Dim columns As Integer = 0
            For Each ch As Char In indentText
                If ch = ChrW(9) Then
                    columns += 4 - (columns Mod 4)
                Else
                    columns += 1
                End If
            Next
            Return columns
        End Function

        Private Shared Function ResolveListLevel(indentColumns As Integer, indentStack As List(Of Integer)) As Integer
            If indentStack Is Nothing Then Return 0
            If indentStack.Count = 0 Then
                indentStack.Add(indentColumns)
                Return 0
            End If

            For idx As Integer = 0 To indentStack.Count - 1
                If indentStack(idx) = indentColumns Then
                    If indentStack.Count > idx + 1 Then indentStack.RemoveRange(idx + 1, indentStack.Count - idx - 1)
                    Return idx
                End If
            Next

            While indentStack.Count > 0 AndAlso indentStack(indentStack.Count - 1) > indentColumns
                indentStack.RemoveAt(indentStack.Count - 1)
            End While

            If indentStack.Count = 0 OrElse indentColumns > indentStack(indentStack.Count - 1) Then
                indentStack.Add(indentColumns)
            End If
            Return Math.Max(0, indentStack.Count - 1)
        End Function

        ''' <summary>表格解析结果。</summary>
        Private Class TableParseResult
            Public Property Block As MarkdownBlock
            Public Property NextIndex As Integer
        End Class

        ''' <summary>将行拆分为表格单元格文本。</summary>
        Private Shared Function SplitTableCells(line As String) As String()
            Dim trimmed = line.Trim()
            If trimmed.StartsWith("|"c) Then trimmed = trimmed.Substring(1)
            If trimmed.EndsWith("|"c) Then trimmed = trimmed.Substring(0, trimmed.Length - 1)
            Return trimmed.Split("|"c)
        End Function

        ''' <summary>从分隔行解析列对齐方式。</summary>
        Private Shared Function ParseAlignments(sepLine As String) As List(Of TableAlignment)
            Dim result As New List(Of TableAlignment)
            For Each cell In SplitTableCells(sepLine)
                Dim t = cell.Trim()
                Dim leftColon = t.StartsWith(":"c)
                Dim rightColon = t.EndsWith(":"c)
                If leftColon AndAlso rightColon Then
                    result.Add(TableAlignment.Center)
                ElseIf rightColon Then
                    result.Add(TableAlignment.Right)
                Else
                    result.Add(TableAlignment.Left)
                End If
            Next
            Return result
        End Function

        ''' <summary>解析表格块。</summary>
        Private Function ParseTable(lines As String(), startIndex As Integer) As TableParseResult
            Dim headerCells = SplitTableCells(lines(startIndex))
            Dim colCount As Integer = headerCells.Length
            If colCount = 0 Then Return Nothing

            Dim alignments = ParseAlignments(lines(startIndex + 1))
            ' 确保对齐数量匹配列数
            While alignments.Count < colCount
                alignments.Add(TableAlignment.Left)
            End While

            Dim tableBlock As New MarkdownBlock(BlockKind.Table) With {
                .ColumnAlignments = alignments,
                .TableRows = New List(Of List(Of MarkdownTableCell)),
                .HasHeader = True
            }

            ' 表头行
            Dim headerRow As New List(Of MarkdownTableCell)
            For Each cellText In headerCells
                Dim cell As New MarkdownTableCell With {
                    .Inlines = ParseInlines(cellText.Trim())
                }
                headerRow.Add(cell)
            Next
            tableBlock.TableRows.Add(headerRow)

            ' 数据行
            Dim i As Integer = startIndex + 2
            While i < lines.Length
                Dim rowLine = lines(i)
                If Not rowLine.Contains("|"c) OrElse String.IsNullOrWhiteSpace(rowLine) Then Exit While
                Dim rowCells = SplitTableCells(rowLine)
                Dim row As New List(Of MarkdownTableCell)
                For ci As Integer = 0 To colCount - 1
                    Dim cell As New MarkdownTableCell
                    If ci < rowCells.Length Then
                        cell.Inlines = ParseInlines(rowCells(ci).Trim())
                    End If
                    row.Add(cell)
                Next
                tableBlock.TableRows.Add(row)
                i += 1
            End While

            Return New TableParseResult With {.Block = tableBlock, .NextIndex = i}
        End Function

        ''' <summary>解析内联格式，可重写以支持 HTML 内联标签等。</summary>
        Public Overridable Function ParseInlines(text As String) As List(Of MarkdownInline)
            Dim result As New List(Of MarkdownInline)
            If String.IsNullOrEmpty(text) Then Return result
            Dim pos As Integer = 0
            For Each m As Match In 内联匹配.Matches(text)
                If m.Index > pos Then
                    result.Add(New MarkdownInline(InlineKind.Text, text.Substring(pos, m.Index - pos)))
                End If
                If m.Groups("bolditalic").Success Then
                    result.Add(New MarkdownInline(InlineKind.BoldItalic, m.Groups("bolditalic_text").Value))
                ElseIf m.Groups("bold").Success Then
                    result.Add(New MarkdownInline(InlineKind.Bold, m.Groups("bold_text").Value))
                ElseIf m.Groups("italic").Success Then
                    result.Add(New MarkdownInline(InlineKind.Italic, m.Groups("italic_text").Value))
                ElseIf m.Groups("strike").Success Then
                    result.Add(New MarkdownInline(InlineKind.Strikethrough, m.Groups("strike_text").Value))
                ElseIf m.Groups("code").Success Then
                    result.Add(New MarkdownInline(InlineKind.Code, m.Groups("code_text").Value))
                ElseIf m.Groups("image").Success Then
                    result.Add(New MarkdownInline(InlineKind.Image, m.Groups("image_alt").Value, m.Groups("image_url").Value.Trim()))
                ElseIf m.Groups("htmlimg").Success Then
                    Dim imgTag = m.Groups("htmlimg").Value
                    Dim srcM = 图片src匹配.Match(imgTag)
                    If srcM.Success Then
                        Dim alt As String = ""
                        Dim altM = 图片alt匹配.Match(imgTag)
                        If altM.Success Then alt = altM.Groups(1).Value
                        Dim imgInline As New MarkdownInline(InlineKind.Image, alt, srcM.Groups(1).Value.Trim())
                        Dim wM = 图片width匹配.Match(imgTag)
                        If wM.Success Then imgInline.ImageWidth = Integer.Parse(wM.Groups(1).Value)
                        Dim hM = 图片height匹配.Match(imgTag)
                        If hM.Success Then imgInline.ImageHeight = Integer.Parse(hM.Groups(1).Value)
                        result.Add(imgInline)
                    End If
                ElseIf m.Groups("link").Success Then
                    result.Add(New MarkdownInline(InlineKind.Link, m.Groups("link_text").Value, m.Groups("link_url").Value))
                ElseIf m.Groups("autolink").Success Then
                    Dim rawUrl = m.Groups("autolink").Value
                    Dim url = rawUrl.TrimEnd("."c, ","c, ";"c, ":"c, "!"c, "?"c, ")"c, "]"c, ">"c, "'"c, """"c)
                    If url.Length > 0 Then
                        result.Add(New MarkdownInline(InlineKind.Link, url, url))
                    End If
                    If url.Length < rawUrl.Length Then
                        result.Add(New MarkdownInline(InlineKind.Text, rawUrl.Substring(url.Length)))
                    End If
                ElseIf m.Groups("br").Success Then
                    result.Add(New MarkdownInline(InlineKind.LineBreak, ""))
                End If
                pos = m.Index + m.Length
            Next
            If pos < text.Length Then
                result.Add(New MarkdownInline(InlineKind.Text, text.Substring(pos)))
            End If
            Return result
        End Function

        Private Shared Function ParseAlertKindFromText(text As String) As AlertKind
            Select Case text.ToUpperInvariant()
                Case "NOTE" : Return AlertKind.Note
                Case "TIP" : Return AlertKind.Tip
                Case "IMPORTANT" : Return AlertKind.Important
                Case "WARNING" : Return AlertKind.Warning
                Case "CAUTION" : Return AlertKind.Caution
                Case Else : Return AlertKind.None
            End Select
        End Function

        Private Shared Function GetAlertLabel(kind As AlertKind) As String
            Select Case kind
                Case AlertKind.Note : Return "Note"
                Case AlertKind.Tip : Return "Tip"
                Case AlertKind.Important : Return "Important"
                Case AlertKind.Warning : Return "Warning"
                Case AlertKind.Caution : Return "Caution"
                Case Else : Return ""
            End Select
        End Function

        ''' <summary>判断行是否为特殊块（标题/引用/列表/代码块/分隔线/空行），用于末尾空格合并判定。</summary>
        Private Shared Function IsSpecialLine(line As String) As Boolean
            If String.IsNullOrWhiteSpace(line) Then Return True
            If 代码块开始.IsMatch(line) Then Return True
            If 分隔线匹配.IsMatch(line.Trim()) Then Return True
            If 标题匹配.IsMatch(line) Then Return True
            If 引用匹配.IsMatch(line) Then Return True
            If 无序列表匹配.IsMatch(line) Then Return True
            If 有序列表匹配.IsMatch(line) Then Return True
            Return False
        End Function
    End Class

#End Region

#Region "内部渲染结构"

    ''' <summary>视觉行（自动换行后的单个显示行）。</summary>
    Private Structure VisualLine
        Public BlockIndex As Integer
        Public Fragments As List(Of VisualFragment)
        Public Y As Integer
        Public Height As Integer
        ''' <summary>表格行索引（BlockKind=Table 时有效，-1 表示非表格行）。</summary>
        Public TableRowIndex As Integer
        ''' <summary>表格行内的子行索引（0 = 该逻辑行的第一行，用于换行后的后续行）。</summary>
        Public TableSubLine As Integer
    End Structure

    ''' <summary>视觉片段（一个行内渲染单元）。</summary>
    Private Structure VisualFragment
        Public InlineIndex As Integer
        Public CharStart As Integer
        Public CharLength As Integer
        Public X As Integer
        Public Width As Integer
        Public Text As String
        Public UseFont As Font
        Public ForeColor As Color
        Public BackColor As Color
        Public Kind As InlineKind
        Public Url As String
        ''' <summary>表格列索引（表格单元格时有效，-1 表示非表格片段）。</summary>
        Public TableColIndex As Integer
        ''' <summary>已加载的图片对象（Kind=Image 时有效）。</summary>
        Public ImageObj As Image
        ''' <summary>片段自身高度（Kind=Image 时为图片渲染高度，0 表示使用行高）。</summary>
        Public FragmentHeight As Integer
    End Structure

    ''' <summary>选中位置。</summary>
    Private Structure SelectionPos
        Public VisualLine As Integer
        Public FragmentIndex As Integer
        Public CharOffset As Integer
        Public Sub New(vl As Integer, fi As Integer, co As Integer)
            Me.VisualLine = vl
            Me.FragmentIndex = fi
            Me.CharOffset = co
        End Sub
    End Structure

    ''' <summary>内容区域边距缓存（避免反复计算）。</summary>
    Private Structure ContentInsets
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure

    Private Structure TextWidthKey
        Implements IEquatable(Of TextWidthKey)

        Public Text As String
        Public FontHash As Integer
        Public LineHeight As Integer
        Public Version As Integer

        Public Overloads Function Equals(other As TextWidthKey) As Boolean Implements IEquatable(Of TextWidthKey).Equals
            Return FontHash = other.FontHash AndAlso
                   LineHeight = other.LineHeight AndAlso
                   Version = other.Version AndAlso
                   String.Equals(Text, other.Text, StringComparison.Ordinal)
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            Return TypeOf obj Is TextWidthKey AndAlso Equals(DirectCast(obj, TextWidthKey))
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return System.HashCode.Combine(Text, FontHash, LineHeight, Version)
        End Function
    End Structure

#End Region

#Region "字段"

    Private _document As New MarkdownDocument()
    Private _parser As New MarkdownParser()
    Private _visualLines As New List(Of VisualLine)
    Private _totalContentHeight As Integer = 0

    ' 表格布局缓存（blockIndex -> 列宽数组）
    Private _tableColumnWidths As New Dictionary(Of Integer, Integer())

    ' 选中
    Private _hasSelection As Boolean = False
    Private _selAnchor As New SelectionPos(0, 0, 0)
    Private _selCurrent As New SelectionPos(0, 0, 0)
    Private _mouseDownSelecting As Boolean = False
    Private _lastMousePos As Point = Point.Empty
    Private _autoScrollTimer As New Timer() With {.Interval = 50}

    ' 滚动
    Private _scrollY As Integer = 0
    Private _scrollBarVisible As Boolean = False
    Private _scrollBar As New V3_ScrollBarRenderer()

    ' 流式追加
    Private _streamBuffer As New StringBuilder()
    Private _streamDirty As Boolean = False
    Private _streamTimer As New PrecisionTimer() With {
        .Interval = 30,
        .DispatchMode = PrecisionTimer.DispatchModeEnum.NonBlocking,
        .OverrunPolicy = PrecisionTimer.OverrunPolicyEnum.Drop,
        .WorkerThreadCount = 1,
        .AutoReset = True
    }
    Private _parseVersion As Integer
    Private _lastAppliedParseVersion As Integer
    Private _parseRunning As Boolean
    Private _runningParseVersion As Integer
    Private _applyingParsedDocument As Boolean
    Private _pendingParseVersion As Integer
    Private _pendingParseParser As MarkdownParser
    Private _pendingParseText As String = ""
    Private _pendingParseResetScroll As Boolean
    Private _pendingParseKeepAtBottom As Boolean
    Private _pendingParseClearSelection As Boolean

    ' 字体缓存
    Private _fontCache As Dictionary(Of String, Font) = Nothing
    Private _lastBaseFont As Font = Nothing
    Private _measureVersion As Integer
    Private _layoutDpi As Integer = 0
    Private _layoutAreaWidth As Integer = 0
    Private ReadOnly _blockLayoutTops As New List(Of Integer)
    Private ReadOnly _blockLayoutBottoms As New List(Of Integer)
    Private ReadOnly _textWidthCache As New Dictionary(Of TextWidthKey, Integer)(512)
    Private Const MaxTextWidthCacheEntries As Integer = 4096

    ' 鼠标链接
    Private _mouseDownLinkUrl As String = Nothing

    ' 图片缓存
    Private ReadOnly _imageCache As New Dictionary(Of String, Image)
    Private ReadOnly _imageLastUsed As New Dictionary(Of String, Long)
    Private ReadOnly _imageLoadingUrls As New HashSet(Of String)
    Private _imageCacheClock As Long
    Private _imageLoadVersion As Integer
    Private Shared ReadOnly _httpClient As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(30)}

    ' 兼容渲染资源（共享 DC RT / brush / textformat / SSAA 来自 D3D_SurfaceCompositor，仅图片缓存按 URL 本地持有）
    Private _当前合成器 As D3D_SurfaceCompositor
    Private ReadOnly _d2dImageCaches As New Dictionary(Of String, D3D_D2DInterop.D2DBitmapCache)
    Private _backgroundSource As Control = Nothing
    Private _embeddedContentMode As Boolean = False
    Private _embeddedHostDpi As Integer = 0
    Private _embeddedInvalidationTarget As Control = Nothing

    Friend Shared Sub CleanupAllD2DResources(level As D3DCacheCleanupLevel, Optional owner As Control = Nothing)
        Dim targetForm As Form = ResolveCleanupForm(owner)
        Dim viewers As New List(Of MarkdownViewerCore)()

        SyncLock _instancesLock
            For i As Integer = _instances.Count - 1 To 0 Step -1
                Dim viewer As MarkdownViewerCore = Nothing
                If Not _instances(i).TryGetTarget(viewer) OrElse viewer Is Nothing OrElse viewer.IsDisposed Then
                    _instances.RemoveAt(i)
                    Continue For
                End If
                If targetForm IsNot Nothing Then
                    Dim viewerForm As Form = Nothing
                    Try : viewerForm = viewer.FindForm() : Catch : viewerForm = Nothing : End Try
                    If viewerForm IsNot targetForm Then Continue For
                End If
                viewers.Add(viewer)
            Next
        End SyncLock

        For Each viewer In viewers
            viewer.CleanupRenderCaches(level)
        Next
    End Sub

    Private Shared Function ResolveCleanupForm(owner As Control) As Form
        If owner Is Nothing OrElse owner.IsDisposed Then Return Nothing
        If TypeOf owner Is Form Then Return DirectCast(owner, Form)
        Try
            Return owner.FindForm()
        Catch
            Return Nothing
        End Try
    End Function

    Private Shared Sub RegisterInstance(viewer As MarkdownViewerCore)
        If viewer Is Nothing Then Return
        SyncLock _instancesLock
            For i As Integer = _instances.Count - 1 To 0 Step -1
                Dim existing As MarkdownViewerCore = Nothing
                If Not _instances(i).TryGetTarget(existing) OrElse existing Is Nothing OrElse existing.IsDisposed Then
                    _instances.RemoveAt(i)
                ElseIf existing Is viewer Then
                    Return
                End If
            Next
            _instances.Add(New WeakReference(Of MarkdownViewerCore)(viewer))
        End SyncLock
    End Sub

    ''' <summary>
    ''' 作为其他控件内部内容渲染器使用时开启：不绘制自身背景/边框/滚动条，高度由宿主控制。
    ''' </summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property EmbeddedContentMode As Boolean
        Get
            Return _embeddedContentMode
        End Get
        Set(value As Boolean)
            If _embeddedContentMode <> value Then
                _embeddedContentMode = value
                If value Then
                    _scrollY = 0
                    _scrollBarVisible = False
                End If
                RebuildLayout()
                RequestV3Render()
            End If
        End Set
    End Property

    ''' <summary>当前 Markdown 内容布局后的高度（像素，已包含 DPI 缩放）。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property ContentHeight As Integer
        Get
            Return _totalContentHeight + GetContentInsets().Top + GetContentInsets().Bottom
        End Get
    End Property

    ''' <summary>
    ''' 嵌入宿主绘制前同步尺寸和 DPI。不会创建窗口句柄，也不会把控件加入宿主 Controls。
    ''' </summary>
    Friend Sub PrepareEmbeddedContent(contentWidth As Integer, hostDpi As Integer, Optional invalidationTarget As Control = Nothing)
        If Not _embeddedContentMode Then _embeddedContentMode = True
        _scrollY = 0
        _scrollBarVisible = False
        _embeddedInvalidationTarget = invalidationTarget
        If invalidationTarget IsNot Nothing Then _streamTimer.SynchronizingObject = invalidationTarget

        Dim safeDpi As Integer = Math.Max(1, hostDpi)
        Dim safeWidth As Integer = Math.Max(1, contentWidth)
        Dim needsLayout As Boolean = False

        If _embeddedHostDpi <> safeDpi Then
            _embeddedHostDpi = safeDpi
            InvalidateMeasureCache()
            needsLayout = True
        End If

        If Me.Width <> safeWidth Then
            Me.Width = safeWidth
            needsLayout = True
        End If

        If needsLayout Then RebuildLayout()
    End Sub

    ''' <summary>嵌入模式下把鼠标滚轮交给宿主控件处理。</summary>
    Public Event EmbeddedMouseWheel As EventHandler(Of MouseEventArgs)

    ''' <summary>Markdown 内容重新布局后高度发生变化时触发，供嵌入宿主同步外层布局。</summary>
    Public Event ContentHeightChanged As EventHandler
    Friend Event EmbeddedContentApplied As EventHandler

    <Category("LakeUI"),
     Description("背景采样源（V3 背景图）。设置后将跨越任意层级直接采样此控件的绘制内容作为透明背景。"),
     DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource IsNot value Then
                _backgroundSource = D3D_BackgroundPenetration.SetBackgroundSource(Me, _backgroundSource, value)
                RequestV3Render()
            End If
        End Set
    End Property

#End Region

#Region "外观属性 - 颜色"

    Private 背景颜色 As Color = Color.FromArgb(30, 30, 30)
    <Category("LakeUI"), Description("背景颜色"), DefaultValue(GetType(Color), "30, 30, 30"), Browsable(True)>
    Public Property BackColor1 As Color
        Get
            Return 背景颜色
        End Get
        Set(value As Color)
            SetValue(背景颜色, value)
        End Set
    End Property

    Private 文本颜色 As Color = Color.FromArgb(212, 212, 212)
    <Category("LakeUI"), Description("正文文本颜色"), DefaultValue(GetType(Color), "212, 212, 212"), Browsable(True)>
    Public Overrides Property ForeColor As Color
        Get
            Return 文本颜色
        End Get
        Set(value As Color)
            SetValue(文本颜色, value)
        End Set
    End Property

    Private 标题颜色 As Color = DefaultMarkdownHeadingColor
    <Category("LakeUI - Markdown"), Description("标题颜色 (H1-H6)"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property HeadingColor As Color
        Get
            Return 标题颜色
        End Get
        Set(value As Color)
            SetValue(标题颜色, value)
        End Set
    End Property

    Private 标题分隔线颜色 As Color = DefaultMarkdownHeadingSeparatorColor
    <Category("LakeUI - Markdown"), Description("H1/H2 标题下方分隔线颜色"), DefaultValue(GetType(Color), "80, 220, 220, 220"), Browsable(True)>
    Public Property HeadingSeparatorColor As Color
        Get
            Return 标题分隔线颜色
        End Get
        Set(value As Color)
            SetValue(标题分隔线颜色, value)
        End Set
    End Property

    Private 标题分隔线粗细 As Integer = DefaultMarkdownHeadingSeparatorThickness
    <Category("LakeUI - Markdown"), Description("H1/H2 标题下方分隔线粗细（像素），0 则不显示"), DefaultValue(GetType(Integer), "2"), Browsable(True)>
    Public Property HeadingSeparatorThickness As Integer
        Get
            Return 标题分隔线粗细
        End Get
        Set(value As Integer)
            SetValue(标题分隔线粗细, Math.Max(0, value))
        End Set
    End Property

    Private 标题分隔线间距 As Integer = DefaultMarkdownHeadingSeparatorGap
    <Category("LakeUI - Markdown"), Description("H1/H2 标题文字与分隔线之间的间距（像素）"), DefaultValue(GetType(Integer), "4"), Browsable(True)>
    Public Property HeadingSeparatorGap As Integer
        Get
            Return 标题分隔线间距
        End Get
        Set(value As Integer)
            SetValue(标题分隔线间距, Math.Max(0, value))
        End Set
    End Property

    Private 粗体颜色 As Color = DefaultMarkdownBoldColor
    <Category("LakeUI - Markdown"), Description("粗体颜色，Empty 时跟随 ForeColor"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property BoldColor As Color
        Get
            Return 粗体颜色
        End Get
        Set(value As Color)
            SetValue(粗体颜色, value)
        End Set
    End Property

    Private 斜体颜色 As Color = DefaultMarkdownItalicColor
    <Category("LakeUI - Markdown"), Description("斜体颜色，Empty 时跟随 ForeColor"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property ItalicColor As Color
        Get
            Return 斜体颜色
        End Get
        Set(value As Color)
            SetValue(斜体颜色, value)
        End Set
    End Property

    Private 代码颜色 As Color = DefaultMarkdownInlineCodeColor
    <Category("LakeUI - Markdown"), Description("行内代码文字颜色"), DefaultValue(GetType(Color), "206, 145, 120"), Browsable(True)>
    Public Property InlineCodeColor As Color
        Get
            Return 代码颜色
        End Get
        Set(value As Color)
            SetValue(代码颜色, value)
        End Set
    End Property

    Private 行内代码背景颜色 As Color = DefaultMarkdownCodeBackColor
    Private 代码块背景颜色 As Color = DefaultMarkdownCodeBackColor
    <Category("LakeUI - Markdown"), Description("行内代码 / 代码块背景颜色（兼容属性，设置时同时写入 InlineCodeBackColor 与 CodeBlockBackColor）"), DefaultValue(GetType(Color), "120, 0, 0, 0"), Browsable(True)>
    Public Property CodeBackColor As Color
        Get
            Return 代码块背景颜色
        End Get
        Set(value As Color)
            If 行内代码背景颜色 <> value OrElse 代码块背景颜色 <> value Then
                行内代码背景颜色 = value
                代码块背景颜色 = value
                RebuildLayout()
                RequestV3Render()
            End If
        End Set
    End Property

    <Category("LakeUI - Markdown"), Description("行内代码背景颜色"), DefaultValue(GetType(Color), "120, 0, 0, 0"), Browsable(True)>
    Public Property InlineCodeBackColor As Color
        Get
            Return 行内代码背景颜色
        End Get
        Set(value As Color)
            SetValue(行内代码背景颜色, value)
        End Set
    End Property

    <Category("LakeUI - Markdown"), Description("代码块背景颜色"), DefaultValue(GetType(Color), "120, 0, 0, 0"), Browsable(True)>
    Public Property CodeBlockBackColor As Color
        Get
            Return 代码块背景颜色
        End Get
        Set(value As Color)
            SetValue(代码块背景颜色, value)
        End Set
    End Property

    Private 代码块文字颜色 As Color = DefaultMarkdownCodeBlockForeColor
    <Category("LakeUI - Markdown"), Description("代码块文字颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property CodeBlockForeColor As Color
        Get
            Return 代码块文字颜色
        End Get
        Set(value As Color)
            SetValue(代码块文字颜色, value)
        End Set
    End Property

    Private 行内代码内边距 As Padding = DefaultMarkdownInlineCodePadding
    <Category("LakeUI - Markdown"), Description("行内代码内边距（像素，自动缩放 DPI）"), Browsable(True)>
    Public Property InlineCodePadding As Padding
        Get
            Return 行内代码内边距
        End Get
        Set(value As Padding)
            SetValue(行内代码内边距, value)
        End Set
    End Property

    Private Function ShouldSerializeInlineCodePadding() As Boolean
        Return 行内代码内边距 <> DefaultMarkdownInlineCodePadding
    End Function

    Private Sub ResetInlineCodePadding()
        InlineCodePadding = DefaultMarkdownInlineCodePadding
    End Sub

    Private 行内代码圆角 As Integer = DefaultMarkdownInlineCodeRadius
    <Category("LakeUI - Markdown"), Description("行内代码背景圆角半径（像素，自动缩放 DPI）"), DefaultValue(3), Browsable(True)>
    Public Property InlineCodeRadius As Integer
        Get
            Return 行内代码圆角
        End Get
        Set(value As Integer)
            SetValue(行内代码圆角, Math.Max(0, value))
        End Set
    End Property

    Private 代码块内边距 As Padding = DefaultMarkdownCodeBlockPadding
    <Category("LakeUI - Markdown"), Description("代码块内边距（像素，自动缩放 DPI）"), Browsable(True)>
    Public Property CodeBlockPadding As Padding
        Get
            Return 代码块内边距
        End Get
        Set(value As Padding)
            SetValue(代码块内边距, value)
        End Set
    End Property

    Private Function ShouldSerializeCodeBlockPadding() As Boolean
        Return 代码块内边距 <> DefaultMarkdownCodeBlockPadding
    End Function

    Private Sub ResetCodeBlockPadding()
        CodeBlockPadding = DefaultMarkdownCodeBlockPadding
    End Sub

    Private 链接颜色 As Color = DefaultMarkdownLinkColor
    <Category("LakeUI - Markdown"), Description("链接颜色"), DefaultValue(GetType(Color), "86, 156, 214"), Browsable(True)>
    Public Property LinkColor As Color
        Get
            Return 链接颜色
        End Get
        Set(value As Color)
            SetValue(链接颜色, value)
        End Set
    End Property

    Private 链接下划线粗细 As Integer = DefaultMarkdownLinkUnderlineThickness
    <Category("LakeUI - Markdown"), Description("链接下划线粗细（像素，自动缩放 DPI）"), DefaultValue(1), Browsable(True)>
    Public Property LinkUnderlineThickness As Integer
        Get
            Return 链接下划线粗细
        End Get
        Set(value As Integer)
            SetValue(链接下划线粗细, Math.Max(1, value))
        End Set
    End Property

    Private 链接下划线偏移 As Integer = DefaultMarkdownLinkUnderlineOffset
    <Category("LakeUI - Markdown"), Description("链接下划线距离行底部的偏移（像素，自动缩放 DPI）"), DefaultValue(3), Browsable(True)>
    Public Property LinkUnderlineOffset As Integer
        Get
            Return 链接下划线偏移
        End Get
        Set(value As Integer)
            SetValue(链接下划线偏移, Math.Max(0, value))
        End Set
    End Property

    Private 删除线颜色 As Color = DefaultMarkdownStrikethroughColor
    <Category("LakeUI - Markdown"), Description("删除线颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property StrikethroughColor As Color
        Get
            Return 删除线颜色
        End Get
        Set(value As Color)
            SetValue(删除线颜色, value)
        End Set
    End Property

    Private 删除线粗细 As Integer = DefaultMarkdownStrikethroughThickness
    <Category("LakeUI - Markdown"), Description("删除线粗细（像素，自动缩放 DPI）"), DefaultValue(1), Browsable(True)>
    Public Property StrikethroughThickness As Integer
        Get
            Return 删除线粗细
        End Get
        Set(value As Integer)
            SetValue(删除线粗细, Math.Max(1, value))
        End Set
    End Property

    Private 引用条颜色 As Color = DefaultMarkdownBlockQuoteBarColor
    <Category("LakeUI - Markdown"), Description("引用左侧竖条颜色"), DefaultValue(GetType(Color), "80, 220, 220, 220"), Browsable(True)>
    Public Property BlockQuoteBarColor As Color
        Get
            Return 引用条颜色
        End Get
        Set(value As Color)
            SetValue(引用条颜色, value)
        End Set
    End Property

    Private 引用文字颜色 As Color = DefaultMarkdownBlockQuoteForeColor
    <Category("LakeUI - Markdown"), Description("引用文字颜色"), DefaultValue(GetType(Color), "100, 255, 255, 255"), Browsable(True)>
    Public Property BlockQuoteForeColor As Color
        Get
            Return 引用文字颜色
        End Get
        Set(value As Color)
            SetValue(引用文字颜色, value)
        End Set
    End Property

    Private 提示Note颜色 As Color = DefaultMarkdownAlertNoteColor
    <Category("LakeUI - Markdown"), Description("[!NOTE] 提示块颜色"), DefaultValue(GetType(Color), "83, 155, 245"), Browsable(True)>
    Public Property AlertNoteColor As Color
        Get
            Return 提示Note颜色
        End Get
        Set(value As Color)
            SetValue(提示Note颜色, value)
        End Set
    End Property

    Private 提示Tip颜色 As Color = DefaultMarkdownAlertTipColor
    <Category("LakeUI - Markdown"), Description("[!TIP] 提示块颜色"), DefaultValue(GetType(Color), "87, 171, 90"), Browsable(True)>
    Public Property AlertTipColor As Color
        Get
            Return 提示Tip颜色
        End Get
        Set(value As Color)
            SetValue(提示Tip颜色, value)
        End Set
    End Property

    Private 提示Important颜色 As Color = DefaultMarkdownAlertImportantColor
    <Category("LakeUI - Markdown"), Description("[!IMPORTANT] 提示块颜色"), DefaultValue(GetType(Color), "152, 110, 226"), Browsable(True)>
    Public Property AlertImportantColor As Color
        Get
            Return 提示Important颜色
        End Get
        Set(value As Color)
            SetValue(提示Important颜色, value)
        End Set
    End Property

    Private 提示Warning颜色 As Color = DefaultMarkdownAlertWarningColor
    <Category("LakeUI - Markdown"), Description("[!WARNING] 提示块颜色"), DefaultValue(GetType(Color), "198, 144, 38"), Browsable(True)>
    Public Property AlertWarningColor As Color
        Get
            Return 提示Warning颜色
        End Get
        Set(value As Color)
            SetValue(提示Warning颜色, value)
        End Set
    End Property

    Private 提示Caution颜色 As Color = DefaultMarkdownAlertCautionColor
    <Category("LakeUI - Markdown"), Description("[!CAUTION] 提示块颜色"), DefaultValue(GetType(Color), "229, 83, 75"), Browsable(True)>
    Public Property AlertCautionColor As Color
        Get
            Return 提示Caution颜色
        End Get
        Set(value As Color)
            SetValue(提示Caution颜色, value)
        End Set
    End Property

    Private 分隔线颜色 As Color = DefaultMarkdownHorizontalRuleColor
    <Category("LakeUI - Markdown"), Description("分隔线颜色"), DefaultValue(GetType(Color), "60, 60, 60"), Browsable(True)>
    Public Property HorizontalRuleColor As Color
        Get
            Return 分隔线颜色
        End Get
        Set(value As Color)
            SetValue(分隔线颜色, value)
        End Set
    End Property

    Private 分隔线粗细 As Integer = DefaultMarkdownHorizontalRuleThickness
    <Category("LakeUI - Markdown"), Description("分隔线粗细（像素）"), DefaultValue(GetType(Integer), "1"), Browsable(True)>
    Public Property HorizontalRuleThickness As Integer
        Get
            Return 分隔线粗细
        End Get
        Set(value As Integer)
            SetValue(分隔线粗细, Math.Max(1, value))
        End Set
    End Property

    Private 分隔线上下边距 As Integer = DefaultMarkdownHorizontalRulePadding
    <Category("LakeUI - Markdown"), Description("分隔线上下内边距（像素）"), DefaultValue(GetType(Integer), "4"), Browsable(True)>
    Public Property HorizontalRulePadding As Integer
        Get
            Return 分隔线上下边距
        End Get
        Set(value As Integer)
            SetValue(分隔线上下边距, Math.Max(0, value))
        End Set
    End Property

    Private 分隔线水平缩进 As Integer = DefaultMarkdownHorizontalRuleInset
    <Category("LakeUI - Markdown"), Description("分隔线左右缩进（像素，自动缩放 DPI）"), DefaultValue(4), Browsable(True)>
    Public Property HorizontalRuleInset As Integer
        Get
            Return 分隔线水平缩进
        End Get
        Set(value As Integer)
            SetValue(分隔线水平缩进, Math.Max(0, value))
        End Set
    End Property

    Private 引用缩进 As Integer = DefaultMarkdownBlockQuoteIndent
    <Category("LakeUI - Markdown"), Description("引用块文字缩进（像素，自动缩放 DPI）"), DefaultValue(16), Browsable(True)>
    Public Property BlockQuoteIndent As Integer
        Get
            Return 引用缩进
        End Get
        Set(value As Integer)
            SetValue(引用缩进, Math.Max(0, value))
        End Set
    End Property

    Private 引用条偏移 As Integer = DefaultMarkdownBlockQuoteBarOffset
    <Category("LakeUI - Markdown"), Description("引用块竖条相对内容左侧的偏移（像素，自动缩放 DPI）"), DefaultValue(4), Browsable(True)>
    Public Property BlockQuoteBarOffset As Integer
        Get
            Return 引用条偏移
        End Get
        Set(value As Integer)
            SetValue(引用条偏移, Math.Max(0, value))
        End Set
    End Property

    Private 引用条宽度 As Integer = DefaultMarkdownBlockQuoteBarWidth
    <Category("LakeUI - Markdown"), Description("引用块竖条宽度（像素，自动缩放 DPI）"), DefaultValue(3), Browsable(True)>
    Public Property BlockQuoteBarWidth As Integer
        Get
            Return 引用条宽度
        End Get
        Set(value As Integer)
            SetValue(引用条宽度, Math.Max(1, value))
        End Set
    End Property

    Private 无序列表缩进 As Integer = DefaultMarkdownUnorderedListIndent
    <Category("LakeUI - Markdown"), Description("无序列表文字缩进（像素，自动缩放 DPI）"), DefaultValue(20), Browsable(True)>
    Public Property UnorderedListIndent As Integer
        Get
            Return 无序列表缩进
        End Get
        Set(value As Integer)
            SetValue(无序列表缩进, Math.Max(0, value))
        End Set
    End Property

    Private 有序列表缩进 As Integer = DefaultMarkdownOrderedListIndent
    <Category("LakeUI - Markdown"), Description("有序列表文字缩进（像素，自动缩放 DPI）"), DefaultValue(24), Browsable(True)>
    Public Property OrderedListIndent As Integer
        Get
            Return 有序列表缩进
        End Get
        Set(value As Integer)
            SetValue(有序列表缩进, Math.Max(0, value))
        End Set
    End Property

    Private 列表圆点半径 As Integer = DefaultMarkdownBulletRadius
    <Category("LakeUI - Markdown"), Description("无序列表圆点半径（像素，自动缩放 DPI）"), DefaultValue(3), Browsable(True)>
    Public Property BulletRadius As Integer
        Get
            Return 列表圆点半径
        End Get
        Set(value As Integer)
            SetValue(列表圆点半径, Math.Max(1, value))
        End Set
    End Property

    Private 列表圆点偏移X As Integer = DefaultMarkdownBulletOffsetX
    <Category("LakeUI - Markdown"), Description("无序列表圆点 X 偏移（像素，自动缩放 DPI）"), DefaultValue(6), Browsable(True)>
    Public Property BulletOffsetX As Integer
        Get
            Return 列表圆点偏移X
        End Get
        Set(value As Integer)
            SetValue(列表圆点偏移X, Math.Max(0, value))
        End Set
    End Property

    Private 列表圆点偏移Y As Integer = DefaultMarkdownBulletOffsetY
    <Category("LakeUI - Markdown"), Description("无序列表圆点 Y 偏移（像素，自动缩放 DPI）"), DefaultValue(-2), Browsable(True)>
    Public Property BulletOffsetY As Integer
        Get
            Return 列表圆点偏移Y
        End Get
        Set(value As Integer)
            SetValue(列表圆点偏移Y, value)
        End Set
    End Property

    Private 有序列表标记宽度 As Integer = DefaultMarkdownOrderedListMarkerWidth
    <Category("LakeUI - Markdown"), Description("有序列表序号标记宽度（像素，自动缩放 DPI）"), DefaultValue(22), Browsable(True)>
    Public Property OrderedListMarkerWidth As Integer
        Get
            Return 有序列表标记宽度
        End Get
        Set(value As Integer)
            SetValue(有序列表标记宽度, Math.Max(1, value))
        End Set
    End Property

    Private 表格边框颜色 As Color = DefaultMarkdownTableBorderColor
    <Category("LakeUI - Markdown"), Description("表格边框颜色"), DefaultValue(GetType(Color), "80, 220, 220, 220"), Browsable(True)>
    Public Property TableBorderColor As Color
        Get
            Return 表格边框颜色
        End Get
        Set(value As Color)
            SetValue(表格边框颜色, value)
        End Set
    End Property

    Private 表头背景颜色 As Color = DefaultMarkdownTableHeaderBackColor
    <Category("LakeUI - Markdown"), Description("表头背景颜色"), DefaultValue(GetType(Color), "40, 220, 220, 220"), Browsable(True)>
    Public Property TableHeaderBackColor As Color
        Get
            Return 表头背景颜色
        End Get
        Set(value As Color)
            SetValue(表头背景颜色, value)
        End Set
    End Property

    Private 表格单元格内边距 As Integer = DefaultMarkdownTableCellPadding
    <Category("LakeUI - Markdown"), Description("表格单元格内边距（像素，自动缩放 DPI）"), DefaultValue(7), Browsable(True)>
    Public Property TableCellPadding As Integer
        Get
            Return 表格单元格内边距
        End Get
        Set(value As Integer)
            SetValue(表格单元格内边距, Math.Max(0, value))
        End Set
    End Property

    Private 表格边框粗细 As Integer = DefaultMarkdownTableBorderThickness
    <Category("LakeUI - Markdown"), Description("表格边框粗细（像素，自动缩放 DPI）"), DefaultValue(1), Browsable(True)>
    Public Property TableBorderThickness As Integer
        Get
            Return 表格边框粗细
        End Get
        Set(value As Integer)
            SetValue(表格边框粗细, Math.Max(1, value))
        End Set
    End Property

    Private 图片占位边框颜色 As Color = DefaultMarkdownImagePlaceholderBorderColor
    <Category("LakeUI - Markdown"), Description("图片未加载时的占位边框颜色"), DefaultValue(GetType(Color), "40, 220, 220, 220"), Browsable(True)>
    Public Property ImagePlaceholderBorderColor As Color
        Get
            Return 图片占位边框颜色
        End Get
        Set(value As Color)
            SetValue(图片占位边框颜色, value)
        End Set
    End Property

    Private 图片占位文字颜色 As Color = DefaultMarkdownImagePlaceholderTextColor
    <Category("LakeUI - Markdown"), Description("图片未加载时的占位文字颜色"), DefaultValue(GetType(Color), "80, 220, 220, 220"), Browsable(True)>
    Public Property ImagePlaceholderTextColor As Color
        Get
            Return 图片占位文字颜色
        End Get
        Set(value As Color)
            SetValue(图片占位文字颜色, value)
        End Set
    End Property

    Private 图片占位宽度 As Integer = DefaultMarkdownImagePlaceholderWidth
    <Category("LakeUI - Markdown"), Description("图片未加载时的占位宽度（像素，自动缩放 DPI）"), DefaultValue(300), Browsable(True)>
    Public Property ImagePlaceholderWidth As Integer
        Get
            Return 图片占位宽度
        End Get
        Set(value As Integer)
            SetValue(图片占位宽度, Math.Max(1, value))
        End Set
    End Property

    Private 图片占位高度行数 As Integer = DefaultMarkdownImagePlaceholderHeightLines
    <Category("LakeUI - Markdown"), Description("图片未加载时的占位高度倍数（按当前行高计算）"), DefaultValue(2), Browsable(True)>
    Public Property ImagePlaceholderHeightLines As Integer
        Get
            Return 图片占位高度行数
        End Get
        Set(value As Integer)
            SetValue(图片占位高度行数, Math.Max(1, value))
        End Set
    End Property

    Private 选中背景颜色 As Color = Color.FromArgb(60, 80, 120)
    <Category("LakeUI"), Description("选中背景颜色"), DefaultValue(GetType(Color), "60, 80, 120"), Browsable(True)>
    Public Property SelectionColor As Color
        Get
            Return 选中背景颜色
        End Get
        Set(value As Color)
            SetValue(选中背景颜色, value)
        End Set
    End Property

#End Region

#Region "外观属性 - 字体"

    Private 代码字体 As Font = Nothing
    <Category("LakeUI - Markdown"), Description("代码字体；Nothing 时代码块使用 Consolas，行内代码继承当前文本字体"), Browsable(True),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property CodeFont As Font
        Get
            Return 代码字体
        End Get
        Set(value As Font)
            代码字体 = value
            DisposeFontCache()
            RebuildLayout()
            InvalidateV3TextResources()
            RequestEmbeddedOrSelfRefresh()
        End Set
    End Property

#End Region

#Region "外观属性 - 边框"

    Private 边框颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("边框颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property BorderColor As Color
        Get
            Return 边框颜色
        End Get
        Set(value As Color)
            SetValue(边框颜色, value)
        End Set
    End Property

    Private 边框宽度 As Integer = 0
    <Category("LakeUI"), Description("边框宽度"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property BorderSize As Integer
        Get
            Return 边框宽度
        End Get
        Set(value As Integer)
            SetValue(边框宽度, value)
        End Set
    End Property

    Private 边框圆角半径 As Integer = 0
    <Category("LakeUI"), Description("边框圆角半径"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property BorderRadius As Integer
        Get
            Return 边框圆角半径
        End Get
        Set(value As Integer)
            SetValue(边框圆角半径, value)
        End Set
    End Property

#End Region

#Region "外观属性 - 滚动条"

    Private 滚动条宽度 As Integer = 10
    <Category("LakeUI"), Description("滚动条宽度"), DefaultValue(GetType(Integer), "10"), Browsable(True)>
    Public Property ScrollBarWidth As Integer
        Get
            Return 滚动条宽度
        End Get
        Set(value As Integer)
            滚动条宽度 = Math.Max(2, value)
            RequestV3Render()
        End Set
    End Property

    Private Shared ReadOnly 默认滚动条颜色 As Color = Color.FromArgb(140, 140, 140)
    Private 滚动条颜色 As Color = 默认滚动条颜色
    <Category("LakeUI"), Description("滚动条滑块颜色"), Browsable(True)>
    Public Property ScrollBarColor As Color
        Get
            Return 滚动条颜色
        End Get
        Set(value As Color)
            SetValue(滚动条颜色, value)
        End Set
    End Property

    Private Function ShouldSerializeScrollBarColor() As Boolean
        Return 滚动条颜色 <> 默认滚动条颜色
    End Function

    Private Sub ResetScrollBarColor()
        ScrollBarColor = 默认滚动条颜色
    End Sub

    Private Shared ReadOnly 默认滚动条悬停颜色 As Color = Color.FromArgb(200, 200, 200)
    Private 滚动条悬停颜色 As Color = 默认滚动条悬停颜色
    <Category("LakeUI"), Description("滚动条滑块悬停/拖拽颜色"), Browsable(True)>
    Public Property ScrollBarHoverColor As Color
        Get
            Return 滚动条悬停颜色
        End Get
        Set(value As Color)
            SetValue(滚动条悬停颜色, value)
        End Set
    End Property

    Private Function ShouldSerializeScrollBarHoverColor() As Boolean
        Return 滚动条悬停颜色 <> 默认滚动条悬停颜色
    End Function

    Private Sub ResetScrollBarHoverColor()
        ScrollBarHoverColor = 默认滚动条悬停颜色
    End Sub

    Private Shared ReadOnly 默认滚动条轨道颜色 As Color = Color.FromArgb(20, 255, 255, 255)
    Private 滚动条轨道颜色 As Color = 默认滚动条轨道颜色
    <Category("LakeUI"), Description("滚动条轨道颜色"), Browsable(True)>
    Public Property ScrollBarTrackColor As Color
        Get
            Return 滚动条轨道颜色
        End Get
        Set(value As Color)
            SetValue(滚动条轨道颜色, value)
        End Set
    End Property

    Private Function ShouldSerializeScrollBarTrackColor() As Boolean
        Return 滚动条轨道颜色 <> 默认滚动条轨道颜色
    End Function

    Private Sub ResetScrollBarTrackColor()
        ScrollBarTrackColor = 默认滚动条轨道颜色
    End Sub

#End Region

#Region "外观属性 - 行距"

    Private 段落行距 As Integer = DefaultMarkdownBlockSpacing
    <Category("LakeUI - Markdown"), Description("段落间距（块与块之间的额外间距）"), DefaultValue(GetType(Integer), "20"), Browsable(True)>
    Public Property BlockSpacing As Integer
        Get
            Return 段落行距
        End Get
        Set(value As Integer)
            段落行距 = Math.Max(0, value)
            RebuildLayout()
            RequestV3Render()
        End Set
    End Property

    Private 行内行距 As Integer = DefaultMarkdownInlineLineSpacing
    <Category("LakeUI - Markdown"), Description("自动换行后的行内行距（同一块内折行的行间距）"), DefaultValue(GetType(Integer), "4"), Browsable(True)>
    Public Property InlineLineSpacing As Integer
        Get
            Return 行内行距
        End Get
        Set(value As Integer)
            行内行距 = Math.Max(0, value)
            RebuildLayout()
            RequestV3Render()
        End Set
    End Property

#End Region

#Region "功能属性"

    <Category("LakeUI"), Description("Markdown 文本"),
     DefaultValue(GetType(String), ""), Browsable(True),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
    Public Overrides Property Text As String
        Get
            Return _streamBuffer.ToString()
        End Get
        Set(value As String)
            _streamBuffer.Clear()
            _streamBuffer.Append(If(value, ""))
            ParseAndLayout(resetScroll:=True, clearSelectionOnApply:=True)
        End Set
    End Property

    <Category("LakeUI"), Description("设置自定义解析器实例以扩展格式支持"), Browsable(False),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property Parser As MarkdownParser
        Get
            Return _parser
        End Get
        Set(value As MarkdownParser)
            _parser = If(value, New MarkdownParser())
            ParseAndLayout()
        End Set
    End Property

    Private 自动滚动 As Boolean = True
    <Category("LakeUI"), Description("流式追加时是否自动滚到底部"), DefaultValue(GetType(Boolean), "True"), Browsable(True)>
    Public Property AutoScrollOnAppend As Boolean
        Get
            Return 自动滚动
        End Get
        Set(value As Boolean)
            自动滚动 = value
        End Set
    End Property

    Private 基础路径 As String = Nothing
    ''' <summary>
    ''' 图片相对路径的基准目录。未设置时使用应用程序运行目录。
    ''' </summary>
    <Category("LakeUI - Markdown"), Description("图片相对路径的基准目录，未设置时使用应用程序运行目录"), DefaultValue(GetType(String), ""), Browsable(True)>
    Public Property BasePath As String
        Get
            Return 基础路径
        End Get
        Set(value As String)
            If 基础路径 <> value Then
                基础路径 = value
                DisposeImageCache()
                RebuildLayout()
                RequestV3Render()
            End If
        End Set
    End Property

#End Region

#Region "构造"

    Public Sub New()
        RegisterInstance(Me)
        D3D_CpuCache.Register(Me)
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.Selectable Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.SupportsTransparentBackColor, True)
        UpdateStyles()

        _streamTimer.SynchronizingObject = Me
        AddHandler _autoScrollTimer.Tick, AddressOf AutoScrollTick
        AddHandler _streamTimer.Tick, AddressOf StreamTimerTick
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        _streamTimer.SynchronizingObject = Me
        ParseAndLayout()
    End Sub

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        _autoScrollTimer.Stop()
        _streamTimer.Stop()
        _parseVersion += 1
        _pendingParseVersion = 0
        _pendingParseParser = Nothing
        _pendingParseText = ""
        _parseRunning = False
        _runningParseVersion = 0
        DisposeFontCache()
        DisposeImageCache()
        DisposeD2DResources()
        MyBase.OnHandleDestroyed(e)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _autoScrollTimer.Stop()
            _streamTimer.Stop()
            RemoveHandler _streamTimer.Tick, AddressOf StreamTimerTick
            _streamTimer.Dispose()
            _parseVersion += 1
            _pendingParseVersion = 0
            _pendingParseParser = Nothing
            _pendingParseText = ""
            _parseRunning = False
            _runningParseVersion = 0
            DisposeFontCache()
            DisposeImageCache()
            DisposeD2DResources()
        End If
        MyBase.Dispose(disposing)
    End Sub

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        RebuildLayout()
        ClampScroll()
        RequestV3Render()
    End Sub

#End Region

#Region "公共方法"

    ''' <summary>
    ''' 流式追加 Markdown 文本，适用于 AI 对话场景的逐字/逐段输出。
    ''' 内部自动合并高频调用，按 30ms 节流刷新。
    ''' </summary>
    Public Sub AppendMarkdown(text As String)
        If String.IsNullOrEmpty(text) Then Return
        _streamBuffer.Append(text)
        _streamDirty = True
        InvalidatePendingParse()
        If Not _streamTimer.IsRunning Then _streamTimer.Start()
    End Sub

    ''' <summary>
    ''' 同步设置 Markdown 并立即完成解析/布局。嵌入到其他控件进行尺寸测量时使用。
    ''' </summary>
    Public Sub SetMarkdownImmediate(markdown As String,
                                    Optional resetScroll As Boolean = False,
                                    Optional keepAtBottom As Boolean = False,
                                    Optional clearSelectionOnApply As Boolean = False)
        _streamTimer.Stop()
        _streamDirty = False
        _streamBuffer.Clear()
        _streamBuffer.Append(If(markdown, ""))
        Dim version = System.Threading.Interlocked.Increment(_parseVersion)
        Dim doc As MarkdownDocument = Nothing
        Try
            doc = If(_parser, New MarkdownParser()).Parse(_streamBuffer.ToString())
        Catch
            doc = New MarkdownDocument()
        End Try
        ApplyParsedDocument(version, doc, resetScroll, keepAtBottom, clearSelectionOnApply)
    End Sub

    ''' <summary>清空全部内容。</summary>
    Public Sub Clear()
        Text = String.Empty
    End Sub

    ''' <summary>滚动到顶部。</summary>
    Public Sub ScrollToTop()
        _scrollY = 0
        RequestV3Render()
    End Sub

    ''' <summary>滚动到底部。</summary>
    Public Sub ScrollToBottom()
        _scrollY = MaxScrollY()
        RequestV3Render()
    End Sub

    ''' <summary>获取选中的纯文本。</summary>
    Public Function GetSelectedText() As String
        If Not _hasSelection OrElse _visualLines.Count = 0 Then Return ""
        Dim startPos As SelectionPos
        Dim endPos As SelectionPos
        GetOrderedSelection(startPos, endPos)
        Dim sb As New StringBuilder()
        For vli As Integer = startPos.VisualLine To Math.Min(endPos.VisualLine, _visualLines.Count - 1)
            Dim vl = _visualLines(vli)
            For fi As Integer = 0 To vl.Fragments.Count - 1
                Dim frag = vl.Fragments(fi)
                If vli = startPos.VisualLine AndAlso fi < startPos.FragmentIndex Then Continue For
                If vli = endPos.VisualLine AndAlso fi > endPos.FragmentIndex Then Continue For
                Dim fragStart As Integer = If(vli = startPos.VisualLine AndAlso fi = startPos.FragmentIndex, startPos.CharOffset, 0)
                Dim fragEnd As Integer = If(vli = endPos.VisualLine AndAlso fi = endPos.FragmentIndex, endPos.CharOffset, frag.CharLength)
                If fragEnd > fragStart AndAlso frag.Text IsNot Nothing Then
                    fragStart = Math.Min(fragStart, frag.Text.Length)
                    fragEnd = Math.Min(fragEnd, frag.Text.Length)
                    If fragEnd > fragStart Then sb.Append(frag.Text.AsSpan(fragStart, fragEnd - fragStart))
                End If
            Next
            If vli < endPos.VisualLine Then sb.AppendLine()
        Next
        Return sb.ToString()
    End Function

    Friend ReadOnly Property HasEmbeddedSelection As Boolean
        Get
            Return _hasSelection
        End Get
    End Property

    Friend Sub BeginEmbeddedSelection(localX As Integer, localY As Integer)
        _mouseDownSelecting = True
        _selAnchor = HitTestPos(localX, localY)
        _selCurrent = _selAnchor
        _hasSelection = False
        RequestEmbeddedOrSelfRefresh()
    End Sub

    Friend Sub UpdateEmbeddedSelection(localX As Integer, localY As Integer)
        If Not _mouseDownSelecting Then Return
        Dim newCurrent = HitTestPos(localX, localY)
        Dim newHasSelection As Boolean = CompareSelectionPos(_selAnchor, newCurrent) <> 0
        If CompareSelectionPos(_selCurrent, newCurrent) = 0 AndAlso _hasSelection = newHasSelection Then Return
        _selCurrent = newCurrent
        _hasSelection = newHasSelection
        RequestEmbeddedOrSelfRefresh()
    End Sub

    Friend Sub EndEmbeddedSelection()
        _mouseDownSelecting = False
        _autoScrollTimer.Stop()
    End Sub

    Friend Sub ClearEmbeddedSelection()
        If _hasSelection OrElse _mouseDownSelecting Then
            _hasSelection = False
            _mouseDownSelecting = False
            _autoScrollTimer.Stop()
            RequestEmbeddedOrSelfRefresh()
        End If
    End Sub

    Friend Sub SelectAllEmbeddedText()
        If _visualLines.Count = 0 Then Return
        _selAnchor = New SelectionPos(0, 0, 0)
        Dim lastVl = _visualLines(_visualLines.Count - 1)
        Dim lastFi As Integer = Math.Max(0, lastVl.Fragments.Count - 1)
        _selCurrent = New SelectionPos(_visualLines.Count - 1, lastFi,
            If(lastVl.Fragments.Count > 0, lastVl.Fragments(lastFi).CharLength, 0))
        _hasSelection = True
        RequestEmbeddedOrSelfRefresh()
    End Sub

#End Region

#Region "解析与布局"

    Private Sub ParseAndLayout(Optional resetScroll As Boolean = False,
                               Optional keepAtBottom As Boolean = False,
                               Optional clearSelectionOnApply As Boolean = False)
        Dim parser = If(_parser, New MarkdownParser())
        Dim markdown = _streamBuffer.ToString()
        Dim version = System.Threading.Interlocked.Increment(_parseVersion)

        Dim callbackTarget = GetAsyncCallbackTarget()
        If parser.GetType() IsNot GetType(MarkdownParser) OrElse callbackTarget Is Nothing Then
            Dim doc As MarkdownDocument = Nothing
            Try
                doc = parser.Parse(markdown)
            Catch
                doc = New MarkdownDocument()
            End Try
            ApplyParsedDocument(version, doc, resetScroll, keepAtBottom, clearSelectionOnApply)
            Return
        End If

        _pendingParseVersion = version
        _pendingParseParser = parser
        _pendingParseText = markdown
        _pendingParseResetScroll = resetScroll
        _pendingParseKeepAtBottom = keepAtBottom
        _pendingParseClearSelection = clearSelectionOnApply
        StartPendingParseIfIdle()
    End Sub

    Private Sub InvalidatePendingParse()
        System.Threading.Interlocked.Increment(_parseVersion)
        _pendingParseVersion = 0
        _pendingParseParser = Nothing
        _pendingParseText = ""
        _pendingParseResetScroll = False
        _pendingParseKeepAtBottom = False
        _pendingParseClearSelection = False
    End Sub

    Private Sub StartPendingParseIfIdle()
        If _parseRunning OrElse _pendingParseVersion <= 0 Then Return

        Dim version = _pendingParseVersion
        Dim parser = _pendingParseParser
        Dim markdown = _pendingParseText
        Dim resetScroll = _pendingParseResetScroll
        Dim keepAtBottom = _pendingParseKeepAtBottom
        Dim clearSelectionOnApply = _pendingParseClearSelection

        _pendingParseVersion = 0
        _pendingParseParser = Nothing
        _pendingParseText = ""
        _pendingParseResetScroll = False
        _pendingParseKeepAtBottom = False
        _pendingParseClearSelection = False

        _parseRunning = True
        _runningParseVersion = version
        Dim callbackTarget = GetAsyncCallbackTarget()
        If callbackTarget Is Nothing Then
            _parseRunning = False
            _runningParseVersion = 0
            Return
        End If

        Task.Run(Sub()
                     Dim doc As MarkdownDocument = Nothing
                     Try
                         doc = parser.Parse(markdown)
                     Catch
                         doc = New MarkdownDocument()
                     End Try

                     Try
                         callbackTarget.BeginInvoke(
                             Sub()
                                 If Not IsDisposed Then CompleteAsyncParse(version, doc, resetScroll, keepAtBottom, clearSelectionOnApply)
                             End Sub)
                     Catch
                         _parseRunning = False
                         _runningParseVersion = 0
                     End Try
                 End Sub)
    End Sub

    Private Sub CompleteAsyncParse(version As Integer, doc As MarkdownDocument,
                                   resetScroll As Boolean, keepAtBottom As Boolean,
                                   clearSelectionOnApply As Boolean)
        If version = _runningParseVersion Then
            _parseRunning = False
            _runningParseVersion = 0
        End If

        If version = _parseVersion Then
            ApplyParsedDocument(version, doc, resetScroll, keepAtBottom, clearSelectionOnApply)
        End If

        StartPendingParseIfIdle()
    End Sub

    Private Sub ApplyParsedDocument(version As Integer, doc As MarkdownDocument,
                                    resetScroll As Boolean, keepAtBottom As Boolean,
                                    clearSelectionOnApply As Boolean)
        If version < _lastAppliedParseVersion Then Return
        _lastAppliedParseVersion = version
        Dim oldDoc = _document
        Dim oldContentHeight As Integer = _totalContentHeight
        Dim firstChangedBlock As Integer = FindFirstChangedBlock(oldDoc, doc)
        _document = If(doc, New MarkdownDocument())
        _applyingParsedDocument = True
        Try
            If firstChangedBlock < 0 Then
                RaiseContentHeightChangedIfNeeded(oldContentHeight)
            ElseIf Not TryRebuildLayoutFromChangedBlock(oldDoc, _document, firstChangedBlock, oldContentHeight) Then
                RebuildLayout(oldContentHeight)
            End If
        Finally
            _applyingParsedDocument = False
        End Try
        If resetScroll Then _scrollY = 0
        If keepAtBottom Then _scrollY = MaxScrollY()
        If clearSelectionOnApply Then ClearSelection()
        ClampScroll()
        RaiseEvent EmbeddedContentApplied(Me, EventArgs.Empty)
        If Not _embeddedContentMode Then RequestEmbeddedOrSelfRefresh()
    End Sub

    Private Function TryRebuildLayoutFromChangedBlock(oldDoc As MarkdownDocument,
                                                      newDoc As MarkdownDocument,
                                                      firstChangedBlock As Integer,
                                                      oldContentHeight As Integer) As Boolean
        If Not CanIncrementalLayout(oldDoc, newDoc, firstChangedBlock) Then Return False

        Dim areaW As Integer = TextAreaWidth()
        If areaW <= 0 Then Return False

        Dim startBlock As Integer = Math.Max(0, Math.Min(firstChangedBlock, newDoc.Blocks.Count))
        Dim y As Integer = If(startBlock < _blockLayoutTops.Count, _blockLayoutTops(startBlock), oldContentHeight)

        For i As Integer = _visualLines.Count - 1 To 0 Step -1
            If _visualLines(i).BlockIndex >= startBlock Then _visualLines.RemoveAt(i)
        Next

        For Each key In _tableColumnWidths.Keys.ToArray()
            If key >= startBlock Then _tableColumnWidths.Remove(key)
        Next

        If _blockLayoutTops.Count > startBlock Then _blockLayoutTops.RemoveRange(startBlock, _blockLayoutTops.Count - startBlock)
        If _blockLayoutBottoms.Count > startBlock Then _blockLayoutBottoms.RemoveRange(startBlock, _blockLayoutBottoms.Count - startBlock)

        Dim lastContentKind As BlockKind? = Nothing
        Dim hadBlankLine As Boolean = False
        GetLayoutContextBeforeBlock(newDoc, startBlock, lastContentKind, hadBlankLine)

        Dim s As Single = DpiScale()
        For bi As Integer = startBlock To newDoc.Blocks.Count - 1
            y = LayoutDocumentBlock(bi, newDoc.Blocks(bi), areaW, y, s, lastContentKind, hadBlankLine)
        Next

        _totalContentHeight = y
        Dim prevVisible As Boolean = _scrollBarVisible
        UpdateScrollBarState()
        If _scrollBarVisible <> prevVisible Then Return False

        TrimImageCache(GetActiveImageUrls())
        RaiseContentHeightChangedIfNeeded(oldContentHeight)
        Return True
    End Function

    Private Function CanIncrementalLayout(oldDoc As MarkdownDocument,
                                          newDoc As MarkdownDocument,
                                          firstChangedBlock As Integer) As Boolean
        If oldDoc Is Nothing OrElse newDoc Is Nothing Then Return False
        If firstChangedBlock < 0 Then Return True
        If _layoutDpi <= 0 OrElse _layoutDpi <> EffectiveDeviceDpi() Then Return False
        If _layoutAreaWidth <= 0 OrElse _layoutAreaWidth <> TextAreaWidth() Then Return False
        If _blockLayoutTops.Count <> oldDoc.Blocks.Count OrElse _blockLayoutBottoms.Count <> oldDoc.Blocks.Count Then Return False
        If firstChangedBlock > oldDoc.Blocks.Count Then Return False
        Return True
    End Function

    Private Shared Function FindFirstChangedBlock(oldDoc As MarkdownDocument, newDoc As MarkdownDocument) As Integer
        If oldDoc Is Nothing OrElse oldDoc.Blocks Is Nothing Then
            Return If(newDoc Is Nothing OrElse newDoc.Blocks Is Nothing OrElse newDoc.Blocks.Count = 0, -1, 0)
        End If
        If newDoc Is Nothing OrElse newDoc.Blocks Is Nothing Then
            Return If(oldDoc.Blocks.Count = 0, -1, 0)
        End If

        Dim sharedCount As Integer = Math.Min(oldDoc.Blocks.Count, newDoc.Blocks.Count)
        For i As Integer = 0 To sharedCount - 1
            If Not BlocksEquivalent(oldDoc.Blocks(i), newDoc.Blocks(i)) Then Return i
        Next
        If oldDoc.Blocks.Count <> newDoc.Blocks.Count Then Return sharedCount
        Return -1
    End Function

    Private Shared Function BlocksEquivalent(a As MarkdownBlock, b As MarkdownBlock) As Boolean
        If a Is b Then Return True
        If a Is Nothing OrElse b Is Nothing Then Return False
        If a.Kind <> b.Kind OrElse
           Not String.Equals(a.RawText, b.RawText, StringComparison.Ordinal) OrElse
           a.OrderIndex <> b.OrderIndex OrElse
           a.ListLevel <> b.ListLevel OrElse
           a.HasHeader <> b.HasHeader OrElse
           a.AlertKind <> b.AlertKind OrElse
           a.IsAlertHeader <> b.IsAlertHeader Then Return False
        If Not InlineListsEquivalent(a.Inlines, b.Inlines) Then Return False
        If Not TableAlignmentsEquivalent(a.ColumnAlignments, b.ColumnAlignments) Then Return False
        Return TableRowsEquivalent(a.TableRows, b.TableRows)
    End Function

    Private Shared Function InlineListsEquivalent(a As List(Of MarkdownInline), b As List(Of MarkdownInline)) As Boolean
        If a Is b Then Return True
        If a Is Nothing OrElse b Is Nothing Then Return False
        If a.Count <> b.Count Then Return False
        For i As Integer = 0 To a.Count - 1
            Dim ai = a(i)
            Dim bi = b(i)
            If ai Is bi Then Continue For
            If ai Is Nothing OrElse bi Is Nothing Then Return False
            If ai.Kind <> bi.Kind OrElse
               Not String.Equals(ai.Text, bi.Text, StringComparison.Ordinal) OrElse
               Not String.Equals(ai.Url, bi.Url, StringComparison.Ordinal) OrElse
               ai.ImageWidth <> bi.ImageWidth OrElse
               ai.ImageHeight <> bi.ImageHeight Then Return False
        Next
        Return True
    End Function

    Private Shared Function TableAlignmentsEquivalent(a As List(Of TableAlignment), b As List(Of TableAlignment)) As Boolean
        If a Is b Then Return True
        If a Is Nothing OrElse b Is Nothing Then Return False
        If a.Count <> b.Count Then Return False
        For i As Integer = 0 To a.Count - 1
            If a(i) <> b(i) Then Return False
        Next
        Return True
    End Function

    Private Shared Function TableRowsEquivalent(a As List(Of List(Of MarkdownTableCell)),
                                                b As List(Of List(Of MarkdownTableCell))) As Boolean
        If a Is b Then Return True
        If a Is Nothing OrElse b Is Nothing Then Return False
        If a.Count <> b.Count Then Return False
        For ri As Integer = 0 To a.Count - 1
            Dim ar = a(ri)
            Dim br = b(ri)
            If ar Is br Then Continue For
            If ar Is Nothing OrElse br Is Nothing OrElse ar.Count <> br.Count Then Return False
            For ci As Integer = 0 To ar.Count - 1
                If ar(ci) Is br(ci) Then Continue For
                If ar(ci) Is Nothing OrElse br(ci) Is Nothing Then Return False
                If Not InlineListsEquivalent(ar(ci).Inlines, br(ci).Inlines) Then Return False
            Next
        Next
        Return True
    End Function

    Private Sub RebuildLayout(Optional previousContentHeight As Integer = -1)
        Dim oldContentHeight As Integer = If(previousContentHeight >= 0, previousContentHeight, _totalContentHeight)
        InvalidateMeasureCache()
        _layoutDpi = EffectiveDeviceDpi()
        _visualLines.Clear()
        _tableColumnWidths.Clear()
        _blockLayoutTops.Clear()
        _blockLayoutBottoms.Clear()
        _totalContentHeight = 0
        If Not IsHandleCreated AndAlso Not _embeddedContentMode Then
            RaiseContentHeightChangedIfNeeded(oldContentHeight)
            Return
        End If
        Dim areaW As Integer = TextAreaWidth()
        _layoutAreaWidth = areaW
        If areaW <= 0 Then
            RaiseContentHeightChangedIfNeeded(oldContentHeight)
            Return
        End If
        Dim s As Single = DpiScale()
        Dim y As Integer = 0

        Dim lastContentKind As BlockKind? = Nothing
        Dim hadBlankLine As Boolean = False

        For bi As Integer = 0 To _document.Blocks.Count - 1
            y = LayoutDocumentBlock(bi, _document.Blocks(bi), areaW, y, s, lastContentKind, hadBlankLine)
        Next

        _totalContentHeight = y
        Dim prevVisible As Boolean = _scrollBarVisible
        UpdateScrollBarState()
        If _scrollBarVisible <> prevVisible AndAlso _scrollBarVisible Then
            RebuildLayout(oldContentHeight)
            Return
        End If
        TrimImageCache(GetActiveImageUrls())
        RaiseContentHeightChangedIfNeeded(oldContentHeight)
    End Sub

    Private Function LayoutDocumentBlock(blockIndex As Integer,
                                         block As MarkdownBlock,
                                         areaW As Integer,
                                         y As Integer,
                                         s As Single,
                                         ByRef lastContentKind As BlockKind?,
                                         ByRef hadBlankLine As Boolean) As Integer
        Dim blockTop As Integer = y

        If block Is Nothing Then
            AddBlockLayoutRange(blockTop, y)
            Return y
        End If

        If block.Kind = BlockKind.BlankLine Then
            hadBlankLine = True
            AddBlockLayoutRange(blockTop, y)
            Return y
        End If

        If lastContentKind.HasValue Then
            Dim isHeading = (block.Kind >= BlockKind.Heading1 AndAlso block.Kind <= BlockKind.Heading6)
            Dim prevIsHeading = (lastContentKind.Value >= BlockKind.Heading1 AndAlso lastContentKind.Value <= BlockKind.Heading6)
            If Not hadBlankLine AndAlso Not isHeading AndAlso IsSameGroup(lastContentKind.Value, block.Kind) Then
                y += 行内行距
            ElseIf prevIsHeading AndAlso Not isHeading Then
                y += Math.Max(行内行距, 段落行距 \ 2)
            Else
                y += 段落行距
            End If
        End If
        hadBlankLine = False

        Select Case block.Kind
            Case BlockKind.HorizontalRule
                Dim ruleThick As Integer = Math.Max(1, CInt(分隔线粗细 * s))
                Dim rulePad As Integer = CInt(分隔线上下边距 * s)
                Dim vl As New VisualLine With {
                    .BlockIndex = blockIndex, .Y = y,
                    .Height = ruleThick + rulePad * 2,
                    .Fragments = New List(Of VisualFragment)
                }
                _visualLines.Add(vl)
                y += vl.Height

            Case BlockKind.CodeBlock
                y = LayoutCodeBlock(blockIndex, block, areaW, y, s)

            Case BlockKind.Table
                y = LayoutTable(blockIndex, block, areaW, y, s)

            Case Else
                y = LayoutInlineBlock(blockIndex, block, areaW, y, s)
        End Select

        lastContentKind = block.Kind
        AddBlockLayoutRange(blockTop, y)
        Return y
    End Function

    Private Sub AddBlockLayoutRange(top As Integer, bottom As Integer)
        _blockLayoutTops.Add(top)
        _blockLayoutBottoms.Add(bottom)
    End Sub

    Private Sub GetLayoutContextBeforeBlock(doc As MarkdownDocument,
                                            blockIndex As Integer,
                                            ByRef lastContentKind As BlockKind?,
                                            ByRef hadBlankLine As Boolean)
        lastContentKind = Nothing
        hadBlankLine = False
        If doc Is Nothing OrElse doc.Blocks Is Nothing Then Return

        For i As Integer = 0 To Math.Min(blockIndex, doc.Blocks.Count) - 1
            Dim block = doc.Blocks(i)
            If block Is Nothing Then Continue For
            If block.Kind = BlockKind.BlankLine Then
                hadBlankLine = True
            Else
                lastContentKind = block.Kind
                hadBlankLine = False
            End If
        Next
    End Sub

    Private Sub RaiseContentHeightChangedIfNeeded(oldContentHeight As Integer)
        If _totalContentHeight = oldContentHeight Then Return
        If _embeddedContentMode AndAlso _applyingParsedDocument Then Return
        RaiseEvent ContentHeightChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub EnsureLayoutDpiCurrent()
        Dim currentDpi As Integer = EffectiveDeviceDpi()
        If currentDpi <= 0 OrElse currentDpi = _layoutDpi Then Return
        RebuildLayout()
        ClampScroll()
    End Sub

    ''' <summary>判断两个相邻块是否属于同一元素组（使用行内行距而非段落行距）。</summary>
    Private Shared Function IsSameGroup(prev As BlockKind, curr As BlockKind) As Boolean
        If IsListBlock(prev) AndAlso IsListBlock(curr) Then Return True
        Return prev = curr AndAlso curr = BlockKind.BlockQuote
    End Function

    Private Shared Function IsListBlock(kind As BlockKind) As Boolean
        Return kind = BlockKind.UnorderedListItem OrElse kind = BlockKind.OrderedListItem
    End Function

    Private Function LayoutCodeBlock(bi As Integer, block As MarkdownBlock, areaW As Integer, y As Integer, s As Single) As Integer
        Dim cFont As Font = GetCodeFont()
        Dim codeLines = If(block.RawText, "").Split(vbLf)
        Dim padT As Integer = CInt(代码块内边距.Top * s)
        Dim padB As Integer = CInt(代码块内边距.Bottom * s)
        Dim padL As Integer = CInt(代码块内边距.Left * s)
        Dim padR As Integer = CInt(代码块内边距.Right * s)
        Dim fontH As Integer = GetLayoutLineHeight(cFont)
        y += padT
        For cli As Integer = 0 To codeLines.Length - 1
            Dim cText = codeLines(cli)
            Dim lineH As Integer = If(cli < codeLines.Length - 1, fontH + 行内行距, fontH)
            Dim frag As New VisualFragment With {
                .InlineIndex = 0, .CharStart = 0, .CharLength = cText.Length,
                .X = padL, .Width = areaW - padL - padR,
                .Text = cText, .UseFont = cFont,
                .ForeColor = 代码块文字颜色, .BackColor = 代码块背景颜色,
                .Kind = InlineKind.Code, .TableColIndex = -1
            }
            _visualLines.Add(New VisualLine With {
                .BlockIndex = bi, .Y = y, .Height = lineH,
                .Fragments = New List(Of VisualFragment) From {frag},
                .TableRowIndex = -1
            })
            y += lineH
        Next
        Return y + padB
    End Function

    Private Function LayoutTable(bi As Integer, block As MarkdownBlock, areaW As Integer, y As Integer, s As Single) As Integer
        If block.TableRows Is Nothing OrElse block.TableRows.Count = 0 Then Return y
        Dim cellPadding As Integer = CInt(表格单元格内边距 * s)
        Dim colCount As Integer = If(block.ColumnAlignments?.Count, 0)
        If colCount = 0 Then Return y

        ' 计算每列最小宽度（单行时的理想宽度）
        Dim lineH As Integer = GetLayoutLineHeight(Font)
        Dim colWidths(colCount - 1) As Integer
        Dim minColW As Integer = cellPadding * 2 + MeasureTextWidthCached("W", Font, lineH)
        For Each row In block.TableRows
            For ci As Integer = 0 To Math.Min(row.Count, colCount) - 1
                Dim cellText = GetCellPlainText(row(ci))
                Dim tw = MeasureTextWidthCached(cellText, Font, lineH) + cellPadding * 2
                If tw > colWidths(ci) Then colWidths(ci) = tw
            Next
        Next

        ' 如果总宽度超过可用区域，按比例缩放，但保证最小列宽
        Dim totalW As Integer = colWidths.Sum()
        If totalW > areaW AndAlso totalW > 0 Then
            Dim ratio As Single = CSng(areaW) / totalW
            For ci As Integer = 0 To colCount - 1
                colWidths(ci) = Math.Max(minColW, CInt(colWidths(ci) * ratio))
            Next
            totalW = colWidths.Sum()
        End If

        _tableColumnWidths(bi) = colWidths

        ' 布局每一行（支持自动换行）
        For ri As Integer = 0 To block.TableRows.Count - 1
            Dim row = block.TableRows(ri)
            Dim cellFont As Font = If(ri = 0 AndAlso block.HasHeader, GetBoldFont(), Font)

            ' 对每个单元格执行换行，得到每列的子行文本列表
            Dim cellWrappedLines As New List(Of List(Of String))  ' colIndex -> List(Of lineText)
            Dim maxSubLines As Integer = 1
            For ci As Integer = 0 To colCount - 1
                Dim cellText As String = ""
                If ci < row.Count Then cellText = GetCellPlainText(row(ci))
                Dim contentW As Integer = colWidths(ci) - cellPadding * 2
                Dim wrapped = WrapText(cellText, cellFont, contentW, lineH)
                cellWrappedLines.Add(wrapped)
                If wrapped.Count > maxSubLines Then maxSubLines = wrapped.Count
            Next

            ' 为该逻辑行的每个子行创建一个 VisualLine
            Dim topPad As Integer = cellPadding \ 2
            Dim bottomPad As Integer = cellPadding - topPad
            y += topPad
            For sl As Integer = 0 To maxSubLines - 1
                Dim fragments As New List(Of VisualFragment)
                Dim x As Integer = 0
                For ci As Integer = 0 To colCount - 1
                    Dim subText As String = ""
                    If sl < cellWrappedLines(ci).Count Then subText = cellWrappedLines(ci)(sl)
                    Dim frag As New VisualFragment With {
                        .InlineIndex = ci, .CharStart = 0, .CharLength = subText.Length,
                        .X = x + cellPadding, .Width = colWidths(ci) - cellPadding * 2,
                        .Text = subText, .UseFont = cellFont,
                        .ForeColor = 文本颜色, .BackColor = Color.Empty,
                        .Kind = InlineKind.Text, .TableColIndex = ci
                    }
                    fragments.Add(frag)
                    x += colWidths(ci)
                Next
                _visualLines.Add(New VisualLine With {
                    .BlockIndex = bi, .Y = y, .Height = lineH + 行内行距,
                    .Fragments = fragments, .TableRowIndex = ri, .TableSubLine = sl
                })
                y += lineH + 行内行距
            Next
            ' 行间留出 cellPadding 的上下空白（均匀分布在子行上下）
            y += bottomPad
        Next
        Return y
    End Function

    ''' <summary>将文本按指定宽度换行，返回每行的文本。</summary>
    Private Function WrapText(text As String, font As Font, maxWidth As Integer, lineH As Integer) As List(Of String)
        Dim lines As New List(Of String)
        If String.IsNullOrEmpty(text) Then
            lines.Add("")
            Return lines
        End If
        Dim pos As Integer = 0
        While pos < text.Length
            Dim fitLen = FindFitLength(text, pos, text.Length, font, maxWidth, lineH)
            If fitLen <= 0 Then fitLen = 1
            lines.Add(text.Substring(pos, fitLen))
            pos += fitLen
        End While
        Return lines
    End Function

    Private Function GetCellPlainText(cell As MarkdownTableCell) As String
        If cell Is Nothing OrElse cell.Inlines Is Nothing OrElse cell.Inlines.Count = 0 Then Return ""
        If cell.Inlines.Count = 1 Then Return If(cell.Inlines(0).Text, "")
        Dim sb As New StringBuilder()
        For Each inl In cell.Inlines
            If inl.Text IsNot Nothing Then sb.Append(inl.Text)
        Next
        Return sb.ToString()
    End Function

    Private Function GetBoldFont() As Font
        EnsureFontCache()
        Return _fontCache(FontBold)
    End Function

    Private Function LayoutInlineBlock(bi As Integer, block As MarkdownBlock, areaW As Integer, y As Integer, s As Single) As Integer
        Dim blockFont As Font = GetBlockFont(block.Kind)
        Dim blockFore As Color = GetBlockForeColor(block.Kind)
        If block.AlertKind <> AlertKind.None AndAlso block.IsAlertHeader Then
            blockFore = GetAlertColor(block.AlertKind)
        End If
        Dim indent As Integer = GetBlockIndent(block, s)
        Dim textLineH As Integer = GetLayoutLineHeight(blockFont)
        Dim fragments As New List(Of VisualFragment)
        Dim x As Integer = indent
        Dim firstLine As Boolean = True
        Dim currentLineH As Integer = textLineH

        For ii As Integer = 0 To block.Inlines.Count - 1
            Dim inl = block.Inlines(ii)

            ' <br> 换行处理
            If inl.Kind = InlineKind.LineBreak Then
                _visualLines.Add(New VisualLine With {
                    .BlockIndex = bi, .Y = y, .Height = currentLineH,
                    .Fragments = fragments
                })
                y += currentLineH + 行内行距
                fragments = New List(Of VisualFragment)
                x = indent
                currentLineH = textLineH
                firstLine = False
                Continue For
            End If

            ' 图片内联渲染
            If inl.Kind = InlineKind.Image Then
                Dim resolvedUrl = ResolveImageUrl(inl.Url)
                Dim img = TryGetOrLoadImage(resolvedUrl)
                Dim maxW As Integer = areaW - indent
                Dim renderW As Integer = 0
                Dim renderH As Integer = 0

                If img IsNot Nothing Then
                    renderW = img.Width
                    renderH = img.Height
                Else
                    renderW = Math.Min(CInt(图片占位宽度 * s), maxW)
                    renderH = GetLayoutLineHeight(blockFont) * 图片占位高度行数
                End If

                If inl.ImageWidth > 0 AndAlso inl.ImageHeight > 0 Then
                    renderW = inl.ImageWidth
                    renderH = inl.ImageHeight
                ElseIf inl.ImageWidth > 0 Then
                    If img IsNot Nothing AndAlso img.Width > 0 Then
                        renderH = CInt(img.Height * (inl.ImageWidth / CSng(img.Width)))
                    End If
                    renderW = inl.ImageWidth
                ElseIf inl.ImageHeight > 0 Then
                    If img IsNot Nothing AndAlso img.Height > 0 Then
                        renderW = CInt(img.Width * (inl.ImageHeight / CSng(img.Height)))
                    End If
                    renderH = inl.ImageHeight
                End If

                ' 限制最大宽度为当前行剩余宽度（若放不下则不超过整行宽度）
                Dim availW As Integer = areaW - x
                If renderW > availW AndAlso x > indent Then
                    ' 当前行放不下，先刷出已有内容
                    _visualLines.Add(New VisualLine With {
                        .BlockIndex = bi, .Y = y, .Height = currentLineH,
                        .Fragments = fragments
                    })
                    y += currentLineH + 行内行距
                    fragments = New List(Of VisualFragment)
                    x = indent
                    currentLineH = textLineH
                    firstLine = False
                    availW = areaW - indent
                End If

                If renderW > maxW AndAlso renderW > 0 Then
                    Dim ratio As Single = maxW / CSng(renderW)
                    renderW = maxW
                    renderH = CInt(renderH * ratio)
                End If
                renderW = Math.Max(1, renderW)
                renderH = Math.Max(1, renderH)

                Dim imgFrag As New VisualFragment With {
                    .InlineIndex = ii, .CharStart = 0, .CharLength = If(inl.Text, "").Length,
                    .X = x, .Width = renderW, .Text = If(inl.Text, ""),
                    .UseFont = blockFont, .ForeColor = blockFore,
                    .Kind = InlineKind.Image, .Url = resolvedUrl,
                    .ImageObj = img, .TableColIndex = -1,
                    .FragmentHeight = renderH
                }
                fragments.Add(imgFrag)
                x += renderW
                currentLineH = Math.Max(currentLineH, renderH)
                Continue For
            End If

            Dim inlFont As Font = GetInlineFont(inl.Kind, blockFont)
            Dim inlFore As Color = GetInlineForeColor(inl.Kind, blockFore)
            Dim inlText As String = If(inl.Text, "")
            Dim pos As Integer = 0
            Dim isInlineCode As Boolean = (inl.Kind = InlineKind.Code)
            Dim codePadL As Integer = If(isInlineCode, CInt(行内代码内边距.Left * s), 0)
            Dim codePadR As Integer = If(isInlineCode, CInt(行内代码内边距.Right * s), 0)
            Dim codePadV As Integer = If(isInlineCode, CInt((行内代码内边距.Top + 行内代码内边距.Bottom) * s), 0)
            If isInlineCode Then
                x += codePadL
                currentLineH = Math.Max(currentLineH, textLineH + codePadV)
            End If

            While pos < inlText.Length
                Dim fitLen As Integer = FindFitLength(inlText, pos, inlText.Length, inlFont, areaW - x - codePadR, textLineH)
                Dim lineStartX As Integer = indent + If(isInlineCode, codePadL, 0)
                If fitLen <= 0 AndAlso (fragments.Count > 0 OrElse x > lineStartX) Then
                    If isInlineCode Then x += codePadR
                    _visualLines.Add(New VisualLine With {
                        .BlockIndex = bi, .Y = y, .Height = currentLineH,
                        .Fragments = fragments
                    })
                    y += currentLineH + 行内行距
                    fragments = New List(Of VisualFragment)
                    x = indent
                    If isInlineCode Then x += codePadL
                    currentLineH = textLineH
                    If isInlineCode Then currentLineH = Math.Max(currentLineH, textLineH + codePadV)
                    firstLine = False
                    Continue While
                End If
                fitLen = Math.Max(1, fitLen)
                Dim segText = inlText.Substring(pos, fitLen)
                Dim segW = MeasureTextWidthCached(segText, inlFont, textLineH)
                Dim frag As New VisualFragment With {
                    .InlineIndex = ii, .CharStart = pos, .CharLength = fitLen,
                    .X = x, .Width = segW, .Text = segText,
                    .UseFont = inlFont, .ForeColor = inlFore,
                    .Kind = inl.Kind, .Url = inl.Url,
                    .BackColor = If(isInlineCode, 行内代码背景颜色, Color.Empty)
                }
                fragments.Add(frag)
                x += segW
                pos += fitLen
            End While
            If isInlineCode Then x += codePadR
        Next

        If fragments.Count > 0 OrElse firstLine Then
            _visualLines.Add(New VisualLine With {
                .BlockIndex = bi, .Y = y, .Height = currentLineH,
                .Fragments = fragments
            })
            y += currentLineH
        End If

        ' H1/H2 自带分割线：在文字下方预留间距 + 线粗空间
        If (block.Kind = BlockKind.Heading1 OrElse block.Kind = BlockKind.Heading2) AndAlso 标题分隔线粗细 > 0 Then
            y += CInt(标题分隔线间距 * s) + Math.Max(1, CInt(标题分隔线粗细 * s))
        End If

        Return y
    End Function

    Private Function GetBlockIndent(block As MarkdownBlock, s As Single) As Integer
        If block Is Nothing Then Return 0
        Select Case block.Kind
            Case BlockKind.BlockQuote : Return CInt(引用缩进 * s)
            Case BlockKind.UnorderedListItem : Return GetListLevelIndent(无序列表缩进, block.ListLevel + 1, s)
            Case BlockKind.OrderedListItem : Return GetListLevelIndent(有序列表缩进, block.ListLevel + 1, s)
            Case Else : Return 0
        End Select
    End Function

    Private Function GetListMarkerIndent(block As MarkdownBlock, s As Single) As Integer
        If block Is Nothing Then Return 0
        Select Case block.Kind
            Case BlockKind.UnorderedListItem : Return GetListLevelIndent(无序列表缩进, block.ListLevel, s)
            Case BlockKind.OrderedListItem : Return GetListLevelIndent(有序列表缩进, block.ListLevel, s)
            Case Else : Return 0
        End Select
    End Function

    Private Shared Function GetListLevelIndent(baseIndent As Integer, levelCount As Integer, s As Single) As Integer
        Return CInt(baseIndent * Math.Max(0, levelCount) * s)
    End Function

    Private Function FindFitLength(text As String, font As Font, maxWidth As Integer, lineH As Integer) As Integer
        Return FindFitLength(text, 0, If(text Is Nothing, 0, text.Length), font, maxWidth, lineH)
    End Function

    Private Function FindFitLength(text As String,
                                   start As Integer,
                                   endExclusive As Integer,
                                   font As Font,
                                   maxWidth As Integer,
                                   lineH As Integer) As Integer
        If maxWidth <= 0 Then Return 0
        If String.IsNullOrEmpty(text) Then Return 0
        start = Math.Max(0, Math.Min(start, text.Length))
        endExclusive = Math.Max(start, Math.Min(endExclusive, text.Length))
        Dim total As Integer = endExclusive - start
        If total <= 0 Then Return 0

        Dim hi As Integer = 1
        While hi < total AndAlso MeasureTextRangeWidthCached(text, start, hi, font, lineH) <= maxWidth
            hi = Math.Min(total, hi * 2)
        End While

        If hi = total AndAlso MeasureTextRangeWidthCached(text, start, total, font, lineH) <= maxWidth Then Return total

        Dim lo As Integer = 1
        hi = Math.Min(hi, total)
        Dim best As Integer = 0
        While lo <= hi
            Dim mid As Integer = (lo + hi) \ 2
            If MeasureTextRangeWidthCached(text, start, mid, font, lineH) <= maxWidth Then
                best = mid
                lo = mid + 1
            Else
                hi = mid - 1
            End If
        End While
        Return best
    End Function

    Private Function MeasureTextRangeWidthCached(text As String,
                                                 start As Integer,
                                                 length As Integer,
                                                 font As Font,
                                                 lineH As Integer) As Integer
        If String.IsNullOrEmpty(text) OrElse length <= 0 Then Return 0
        If start <= 0 AndAlso length >= text.Length Then Return MeasureTextWidthCached(text, font, lineH)
        Return MeasureTextWidthCached(text.Substring(start, length), font, lineH)
    End Function

#End Region

#Region "绘制"

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        If _embeddedContentMode OrElse _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        Dim w As Integer = ClientRectangle.Width
        Dim h As Integer = ClientRectangle.Height
        If w <= 0 OrElse h <= 0 Then Return
        EnsureLayoutDpiCurrent()
        Dim s As Single = DpiScale()

        Dim boundsRect As New RectangleF(0, 0, w, h)
        If 边框宽度 > 0 Then boundsRect.Inflate(-边框宽度 * s / 2.0F, -边框宽度 * s / 2.0F)

        If _embeddedContentMode Then
            If 背景颜色.A > 0 Then context.FillRectangle(New RectangleF(0, 0, w, h), 背景颜色)
        Else
            If _backgroundSource IsNot Nothing Then
                context.DrawBackgroundSource(Me, _backgroundSource, New RectangleF(0, 0, w, h))
            End If
            DrawBackground_GPU(context, boundsRect, s)
        End If

        DrawMarkdownContent_GPU(context, Point.Empty, New Size(w, h), _scrollY, drawBackground:=False)
        If Not _embeddedContentMode Then DrawScrollBar_GPU(context, w, h, s)
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Private Sub RequestV3Render(Optional immediate As Boolean = False)
        RequestV3Render(New Rectangle(Point.Empty, Me.Size), immediate)
    End Sub

    Private Sub RequestV3Render(dirtyRect As Rectangle, Optional immediate As Boolean = False)
        Dim target = GetEmbeddedInvalidationTarget()
        If target IsNot Nothing Then
            RequestV3Render(target)
            Return
        End If

        If Me.IsDisposed Then Return
        V3_InvalidationRouter.RequestRender(Me, dirtyRect)
    End Sub

    Private Sub RequestV3Render(target As Control)
        If target Is Nothing OrElse target.IsDisposed Then Return
        V3_InvalidationRouter.RequestRender(target, New Rectangle(Point.Empty, target.Size))
    End Sub

    Friend Sub DrawEmbeddedContent_GPU(context As D3D_PaintContext,
                                       origin As Point,
                                       clipSize As Size,
                                       Optional drawBackground As Boolean = False,
                                       Optional visibleLocalTop As Integer = -1,
                                       Optional visibleLocalBottom As Integer = -1)
        If context Is Nothing OrElse clipSize.Width <= 0 OrElse clipSize.Height <= 0 Then Return
        EnsureLayoutDpiCurrent()
        DrawMarkdownContent_GPU(context, origin, clipSize, _scrollY, drawBackground, visibleLocalTop, visibleLocalBottom)
    End Sub

    Private Sub DrawMarkdownContent_GPU(context As D3D_PaintContext,
                                        origin As Point,
                                        clipSize As Size,
                                        scrollY As Integer,
                                        Optional drawBackground As Boolean = False,
                                        Optional visibleLocalTop As Integer = -1,
                                        Optional visibleLocalBottom As Integer = -1)
        If clipSize.Width <= 0 OrElse clipSize.Height <= 0 Then Return

        If drawBackground AndAlso 背景颜色.A > 0 Then
            context.FillRectangle(New RectangleF(origin.X, origin.Y, clipSize.Width, clipSize.Height), 背景颜色)
        End If

        Dim s As Single = DpiScale()
        Dim ci = GetContentInsets()
        Dim scrollW As Integer = If(_scrollBarVisible AndAlso Not _embeddedContentMode, CInt(Math.Round(滚动条宽度 * s)) + V3_ScrollBarRenderer.Margin * 2, 0)
        Dim clipW As Integer = Math.Max(0, Math.Min(TextAreaWidth(), clipSize.Width - ci.Left - Math.Max(ci.Right, scrollW)))
        Dim clipH As Integer = Math.Max(0, clipSize.Height - ci.Top - ci.Bottom)
        If clipW <= 0 OrElse clipH <= 0 Then Return

        Dim localClipTop As Integer = ci.Top
        Dim localClipBottom As Integer = ci.Top + clipH
        If visibleLocalTop >= 0 Then localClipTop = Math.Max(localClipTop, visibleLocalTop)
        If visibleLocalBottom >= 0 Then localClipBottom = Math.Min(localClipBottom, visibleLocalBottom)
        If localClipBottom <= localClipTop Then Return

        Dim clipLeft As Single = origin.X + ci.Left
        Dim clipTop As Single = origin.Y + localClipTop
        Dim clipBottom As Single = origin.Y + localClipBottom

        Using context.PushClip(New RectangleF(clipLeft, clipTop, clipW, clipBottom - clipTop))
            Dim selStart, selEnd As SelectionPos
            If _hasSelection Then GetOrderedSelection(selStart, selEnd)

            Dim visibleTop As Integer = Math.Max(0, scrollY + localClipTop - ci.Top)
            For vli As Integer = GetFirstVisibleVisualLine(visibleTop) To _visualLines.Count - 1
                Dim vl = _visualLines(vli)
                Dim drawY As Integer = origin.Y + vl.Y - scrollY + ci.Top
                If drawY + vl.Height < clipTop Then Continue For
                If drawY > clipBottom Then Exit For

                Dim block = _document.Blocks(vl.BlockIndex)
                DrawBlockDecoration_GPU(context, block, vl, vli, CInt(clipLeft), drawY, clipW, s)
                DrawFragments_GPU(context, vl, vli, CInt(clipLeft), drawY, block.Kind, s, selStart, selEnd)
            Next
        End Using
    End Sub

    Private Sub DrawBackground_GPU(context As D3D_PaintContext, boundsRect As RectangleF, s As Single)
        If 背景颜色.A > 0 Then FillRoundedRect_GPU(context, boundsRect, 边框圆角半径 * s, 背景颜色)
        If 边框宽度 > 0 AndAlso 边框颜色.A > 0 Then DrawRoundedBorder_GPU(context, boundsRect, 边框圆角半径 * s, 边框颜色, 边框宽度 * s)
    End Sub

    Private Sub DrawBlockDecoration_GPU(context As D3D_PaintContext,
                                        block As MarkdownBlock,
                                        vl As VisualLine,
                                        vli As Integer,
                                        textLeft As Integer,
                                        drawY As Integer,
                                        clipW As Integer,
                                        s As Single)
        Select Case block.Kind
            Case BlockKind.HorizontalRule
                Dim ruleThick As Integer = Math.Max(1, CInt(分隔线粗细 * s))
                Dim ruleY As Integer = drawY + (vl.Height - ruleThick) \ 2
                Dim ruleInset As Integer = CInt(分隔线水平缩进 * s)
                context.FillRectangle(New RectangleF(textLeft + ruleInset, ruleY, Math.Max(0, clipW - ruleInset * 2), ruleThick), 分隔线颜色)

            Case BlockKind.BlockQuote
                Dim barH As Integer = vl.Height
                If vli + 1 < _visualLines.Count Then
                    Dim nextVl = _visualLines(vli + 1)
                    Dim nextBlock = _document.Blocks(nextVl.BlockIndex)
                    If nextBlock.Kind = BlockKind.BlockQuote Then barH = nextVl.Y - vl.Y
                End If
                Dim barColor As Color = If(block.AlertKind <> AlertKind.None, GetAlertColor(block.AlertKind), 引用条颜色)
                context.FillRectangle(New RectangleF(textLeft + CInt(引用条偏移 * s), drawY, Math.Max(1, CInt(引用条宽度 * s)), barH), barColor)

            Case BlockKind.CodeBlock
                Dim padT As Integer = CInt(代码块内边距.Top * s)
                Dim padB As Integer = CInt(代码块内边距.Bottom * s)
                Dim isFirstCodeLine = (vli = 0 OrElse _visualLines(vli - 1).BlockIndex <> vl.BlockIndex)
                Dim isLastCodeLine = (vli = _visualLines.Count - 1 OrElse _visualLines(vli + 1).BlockIndex <> vl.BlockIndex)
                Dim bgY = drawY - If(isFirstCodeLine, padT, 0)
                Dim bgH = vl.Height + If(isFirstCodeLine, padT, 0) + If(isLastCodeLine, padB, 0)
                context.FillRectangle(New RectangleF(textLeft, bgY, clipW, bgH), 代码块背景颜色)

            Case BlockKind.UnorderedListItem
                If vli > 0 AndAlso _visualLines(vli - 1).BlockIndex = vl.BlockIndex Then Return
                Dim unorderedMarkerIndent As Integer = GetListMarkerIndent(block, s)
                Dim bulletR As Integer = Math.Max(1, CInt(列表圆点半径 * s))
                Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, 文本颜色, context.DeviceGeneration)
                context.DeviceContext.FillEllipse(
                    New Ellipse(New Vector2(textLeft + unorderedMarkerIndent + CInt(列表圆点偏移X * s) + bulletR,
                                            drawY + vl.Height \ 2 + CInt(列表圆点偏移Y * s) + bulletR),
                                bulletR, bulletR),
                    brush)

            Case BlockKind.OrderedListItem
                If vli > 0 AndAlso _visualLines(vli - 1).BlockIndex = vl.BlockIndex Then Return
                Dim orderedMarkerIndent As Integer = GetListMarkerIndent(block, s)
                DrawText_GPU(context, block.OrderIndex.ToString() & ".", GetBlockFont(block.Kind), 文本颜色,
                    New RectangleF(textLeft + orderedMarkerIndent, drawY, Math.Max(1, CInt(有序列表标记宽度 * s)), vl.Height), D2DTextAlign.Right, True)

            Case BlockKind.Table
                DrawTableDecoration_GPU(context, block, vl, vli, textLeft, drawY, s)

            Case BlockKind.Heading1, BlockKind.Heading2
                If 标题分隔线粗细 > 0 Then
                    Dim isLastLineOfBlock As Boolean =
                        (vli + 1 >= _visualLines.Count OrElse _visualLines(vli + 1).BlockIndex <> vl.BlockIndex)
                    If isLastLineOfBlock Then
                        Dim sepThick As Integer = Math.Max(1, CInt(标题分隔线粗细 * s))
                        Dim sepGap As Integer = CInt(标题分隔线间距 * s)
                        Dim sepY As Integer = drawY + vl.Height + sepGap
                        context.FillRectangle(New RectangleF(textLeft, sepY, clipW, sepThick), 标题分隔线颜色)
                    End If
                End If
        End Select
    End Sub

    Private Sub DrawTableDecoration_GPU(context As D3D_PaintContext,
                                        block As MarkdownBlock,
                                        vl As VisualLine,
                                        vli As Integer,
                                        textLeft As Integer,
                                        drawY As Integer,
                                        s As Single)
        If vl.TableSubLine <> 0 Then Return
        Dim colWidths As Integer() = Nothing
        If Not _tableColumnWidths.TryGetValue(vl.BlockIndex, colWidths) Then Return
        Dim colCount As Integer = colWidths.Length
        Dim totalW As Integer = colWidths.Sum()

        Dim cellPadding As Integer = CInt(表格单元格内边距 * s)
        Dim logicalRowH As Integer = vl.Height
        Dim look As Integer = vli + 1
        While look < _visualLines.Count
            Dim nextVl = _visualLines(look)
            If nextVl.BlockIndex <> vl.BlockIndex OrElse nextVl.TableRowIndex <> vl.TableRowIndex Then Exit While
            logicalRowH += nextVl.Height
            look += 1
        End While
        logicalRowH += cellPadding

        Dim topPad As Integer = cellPadding \ 2
        Dim rowTop As Integer = drawY - topPad

        If vl.TableRowIndex = 0 AndAlso block.HasHeader Then
            context.FillRectangle(New RectangleF(textLeft, rowTop, totalW, logicalRowH), 表头背景颜色)
        End If

        Dim lineBrush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, 表格边框颜色, context.DeviceGeneration)
        Dim stroke As Single = Math.Max(1.0F, 表格边框粗细 * s)
        context.DeviceContext.DrawLine(New Vector2(textLeft, rowTop), New Vector2(textLeft + totalW, rowTop), lineBrush, stroke)
        context.DeviceContext.DrawLine(New Vector2(textLeft, rowTop + logicalRowH), New Vector2(textLeft + totalW, rowTop + logicalRowH), lineBrush, stroke)
        Dim x As Integer = textLeft
        For ci As Integer = 0 To colCount
            context.DeviceContext.DrawLine(New Vector2(x, rowTop), New Vector2(x, rowTop + logicalRowH), lineBrush, stroke)
            If ci < colCount Then x += colWidths(ci)
        Next
    End Sub

    Private Sub DrawFragments_GPU(context As D3D_PaintContext,
                                  vl As VisualLine,
                                  vli As Integer,
                                  textLeft As Integer,
                                  drawY As Integer,
                                  blockKind As BlockKind,
                                  s As Single,
                                  selStart As SelectionPos,
                                  selEnd As SelectionPos)
        For fi As Integer = 0 To vl.Fragments.Count - 1
            Dim frag = vl.Fragments(fi)
            Dim fragX As Integer = textLeft + frag.X
            Dim fragW As Integer = frag.Width

            If frag.Kind = InlineKind.Image Then
                If _hasSelection Then DrawFragmentSelection_GPU(context, vli, fi, frag, fragX, drawY, vl.Height, selStart, selEnd)
                Dim imgH As Integer = If(frag.FragmentHeight > 0, frag.FragmentHeight, vl.Height)
                Dim imgRect As New RectangleF(fragX, drawY, frag.Width, imgH)
                If frag.ImageObj IsNot Nothing Then
                    context.DrawImage(frag.ImageObj, imgRect)
                Else
                    context.DrawRectangle(imgRect, 图片占位边框颜色, Math.Max(1.0F, s))
                    If Not String.IsNullOrEmpty(frag.Text) Then
                        DrawText_GPU(context, frag.Text, frag.UseFont, 图片占位文字颜色, imgRect, D2DTextAlign.Center, True)
                    End If
                End If
                Continue For
            End If

            If frag.BackColor <> Color.Empty AndAlso blockKind <> BlockKind.CodeBlock Then
                Dim padL As Integer = CInt(行内代码内边距.Left * s)
                Dim padR As Integer = CInt(行内代码内边距.Right * s)
                FillRoundedRect_GPU(context, New RectangleF(fragX - padL, drawY, fragW + padL + padR, vl.Height), CInt(行内代码圆角 * s), frag.BackColor)
            End If

            If _hasSelection Then DrawFragmentSelection_GPU(context, vli, fi, frag, fragX, drawY, vl.Height, selStart, selEnd)

            If Not String.IsNullOrEmpty(frag.Text) Then
                If blockKind = BlockKind.Table AndAlso frag.TableColIndex >= 0 Then
                    Dim block = _document.Blocks(vl.BlockIndex)
                    Dim colWidths As Integer() = Nothing
                    Dim cellW As Integer = frag.Width
                    If _tableColumnWidths.TryGetValue(vl.BlockIndex, colWidths) AndAlso frag.TableColIndex < colWidths.Length Then
                        Dim cellPadding As Integer = CInt(表格单元格内边距 * s)
                        cellW = colWidths(frag.TableColIndex) - cellPadding * 2
                    End If
                    Dim align As D2DTextAlign = D2DTextAlign.Left
                    If block.ColumnAlignments IsNot Nothing AndAlso frag.TableColIndex < block.ColumnAlignments.Count Then
                        Select Case block.ColumnAlignments(frag.TableColIndex)
                            Case TableAlignment.Center : align = D2DTextAlign.Center
                            Case TableAlignment.Right : align = D2DTextAlign.Right
                        End Select
                    End If
                    DrawText_GPU(context, frag.Text, frag.UseFont, frag.ForeColor, New RectangleF(fragX, drawY, cellW, vl.Height), align, True)
                Else
                    DrawText_GPU(context, frag.Text, frag.UseFont, frag.ForeColor, New RectangleF(fragX, drawY, Short.MaxValue, vl.Height), D2DTextAlign.Left, True)
                End If
            End If

            If fragW > 0 Then
                Dim lineBrush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, frag.ForeColor, context.DeviceGeneration)
                If frag.Kind = InlineKind.Strikethrough Then
                    context.DeviceContext.DrawLine(New Vector2(fragX, drawY + vl.Height \ 2), New Vector2(fragX + fragW, drawY + vl.Height \ 2), lineBrush, Math.Max(1.0F, 删除线粗细 * s))
                ElseIf frag.Kind = InlineKind.Link Then
                    Dim y As Integer = drawY + vl.Height - CInt(链接下划线偏移 * s)
                    context.DeviceContext.DrawLine(New Vector2(fragX, y), New Vector2(fragX + fragW, y), lineBrush, Math.Max(1.0F, 链接下划线粗细 * s))
                End If
            End If
        Next
    End Sub

    Private Sub DrawFragmentSelection_GPU(context As D3D_PaintContext,
                                          vli As Integer,
                                          fi As Integer,
                                          frag As VisualFragment,
                                          fragX As Integer,
                                          drawY As Integer,
                                          lineH As Integer,
                                          selStart As SelectionPos,
                                          selEnd As SelectionPos)
        If vli < selStart.VisualLine OrElse vli > selEnd.VisualLine Then Return
        Dim sChar As Integer = 0
        Dim eChar As Integer = frag.CharLength
        If vli = selStart.VisualLine Then
            If fi < selStart.FragmentIndex Then Return
            If fi = selStart.FragmentIndex Then sChar = selStart.CharOffset
        End If
        If vli = selEnd.VisualLine Then
            If fi > selEnd.FragmentIndex Then Return
            If fi = selEnd.FragmentIndex Then eChar = selEnd.CharOffset
        End If
        If eChar <= sChar OrElse frag.Text Is Nothing Then Return
        Dim safeS = Math.Min(sChar, frag.Text.Length)
        Dim safeE = Math.Min(eChar, frag.Text.Length)
        Dim x1 As Integer = fragX + If(safeS > 0, MeasureTextWidthCached(frag.Text.Substring(0, safeS), frag.UseFont, lineH), 0)
        Dim x2 As Integer = fragX + MeasureTextWidthCached(frag.Text.Substring(0, safeE), frag.UseFont, lineH)
        If x2 > x1 Then context.FillRectangle(New RectangleF(x1, drawY, x2 - x1, lineH), 选中背景颜色)
    End Sub

    Private Sub DrawText_GPU(context As D3D_PaintContext,
                             text As String,
                             font As Font,
                             color As Color,
                             rect As RectangleF,
                             align As D2DTextAlign,
                             verticalCenter As Boolean)
        If String.IsNullOrEmpty(text) OrElse font Is Nothing OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return

        Dim textAlign As Vortice.DirectWrite.TextAlignment
        Select Case align
            Case D2DTextAlign.Center : textAlign = Vortice.DirectWrite.TextAlignment.Center
            Case D2DTextAlign.Right : textAlign = Vortice.DirectWrite.TextAlignment.Trailing
            Case Else : textAlign = Vortice.DirectWrite.TextAlignment.Leading
        End Select
        Dim paraAlign As Vortice.DirectWrite.ParagraphAlignment = If(verticalCenter, Vortice.DirectWrite.ParagraphAlignment.Center, Vortice.DirectWrite.ParagraphAlignment.Near)
        context.DrawText(text, font, color, rect, textAlign, paraAlign)
    End Sub

    Private Sub DrawScrollBar_GPU(context As D3D_PaintContext, w As Integer, h As Integer, s As Single)
        If _embeddedContentMode OrElse Not _scrollBarVisible Then Return
        Dim scaledBorder As Integer = CInt(Math.Round(边框宽度 * s))
        Dim scaledRadius As Integer = CInt(Math.Round(边框圆角半径 * s))
        Dim scaledScrollW As Integer = CInt(Math.Round(滚动条宽度 * s))
        Dim viewH As Integer = ViewportHeight()
        If _totalContentHeight <= 0 OrElse viewH <= 0 OrElse scaledScrollW <= 0 Then Return

        _scrollBar.ComputeLayout(w, h, scaledBorder, scaledRadius, 0, 0, scaledScrollW, _totalContentHeight, viewH, _scrollY)
        If _scrollBar.TrackRect.IsEmpty Then Return

        Dim width As Single = Math.Max(1.0F, scaledScrollW)
        Dim trackArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.TrackRect.Y, width, _scrollBar.TrackRect.Height)
        Dim thumbArea As New RectangleF(_scrollBar.VisualLeft, _scrollBar.ThumbRect.Y, width, _scrollBar.ThumbRect.Height)
        FillRoundedRect_GPU(context, trackArea, Math.Min(width / 2.0F, trackArea.Height / 2.0F), 滚动条轨道颜色)
        Dim thumbColor = If(_scrollBar.IsDragging OrElse _scrollBar.IsHover, 滚动条悬停颜色, 滚动条颜色)
        FillRoundedRect_GPU(context, thumbArea, Math.Min(width / 2.0F, thumbArea.Height / 2.0F), thumbColor)
    End Sub

    Private Sub FillRoundedRect_GPU(context As D3D_PaintContext, rect As RectangleF, radius As Single, color As Color)
        If color.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If radius <= 0 Then
            context.DeviceContext.FillRectangle(D3D_PaintContext.ToRawRect(rect), brush)
            Return
        End If
        context.FillRoundedRectangle(rect, radius, brush)
    End Sub

    Private Sub DrawRoundedBorder_GPU(context As D3D_PaintContext, rect As RectangleF, radius As Single, color As Color, strokeWidth As Single)
        If color.A = 0 OrElse strokeWidth <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If radius <= 0 Then
            context.DeviceContext.DrawRectangle(D3D_PaintContext.ToRawRect(rect), brush, strokeWidth)
            Return
        End If
        context.DrawRoundedRectangle(rect, radius, brush, strokeWidth)
    End Sub

    Friend Sub DrawEmbeddedContent_D2D(rt As ID2D1RenderTarget,
                                       compositor As D3D_SurfaceCompositor,
                                       origin As Point,
                                       clipSize As Size,
                                       Optional drawBackground As Boolean = False,
                                       Optional visibleLocalTop As Integer = -1,
                                       Optional visibleLocalBottom As Integer = -1)
        If rt Is Nothing OrElse compositor Is Nothing Then Return
        If clipSize.Width <= 0 OrElse clipSize.Height <= 0 Then Return
        EnsureLayoutDpiCurrent()

        Dim oldCompositor = _当前合成器
        _当前合成器 = compositor
        Dim trimImageCachesAfterPaint As Boolean = False
        Try
            Dim s As Single = DpiScale()
            If drawBackground AndAlso 背景颜色.A > 0 Then
                rt.FillRectangle(
                    D3D_D2DInterop.ToD2DRect(New RectangleF(origin.X, origin.Y, clipSize.Width, clipSize.Height)),
                    compositor.BrushCache.Get(rt, 背景颜色))
            End If

            Dim ci = GetContentInsets()
            Dim clipW As Integer = Math.Max(0, Math.Min(TextAreaWidth(), clipSize.Width - ci.Left - ci.Right))
            Dim clipH As Integer = Math.Max(0, clipSize.Height - ci.Top - ci.Bottom)
            If clipW <= 0 OrElse clipH <= 0 Then Return

            Dim localClipTop As Integer = ci.Top
            Dim localClipBottom As Integer = ci.Top + clipH
            If visibleLocalTop >= 0 Then localClipTop = Math.Max(localClipTop, visibleLocalTop)
            If visibleLocalBottom >= 0 Then localClipBottom = Math.Min(localClipBottom, visibleLocalBottom)
            If localClipBottom <= localClipTop Then Return

            Dim clipLeft As Single = origin.X + ci.Left
            Dim clipTop As Single = origin.Y + localClipTop
            Dim clipBottom As Single = origin.Y + localClipBottom
            rt.PushAxisAlignedClip(New Vortice.RawRectF(clipLeft, clipTop, clipLeft + clipW, clipBottom), AntialiasMode.PerPrimitive)
            Try
                Dim selStart, selEnd As SelectionPos
                If _hasSelection Then GetOrderedSelection(selStart, selEnd)

                Dim visibleTop As Integer = Math.Max(0, _scrollY + localClipTop - ci.Top)
                For vli As Integer = GetFirstVisibleVisualLine(visibleTop) To _visualLines.Count - 1
                    Dim vl = _visualLines(vli)
                    Dim drawY As Integer = origin.Y + vl.Y - _scrollY + ci.Top
                    If drawY + vl.Height < clipTop Then Continue For
                    If drawY > clipBottom Then Exit For

                    Dim block = _document.Blocks(vl.BlockIndex)
                    DrawBlockDecoration_D2D(rt, block, vl, vli, CInt(clipLeft), drawY, clipW, s)
                    trimImageCachesAfterPaint = DrawFragments_D2D(rt, vl, vli, CInt(clipLeft), drawY, block.Kind, s, selStart, selEnd) OrElse trimImageCachesAfterPaint
                Next
            Finally
                rt.PopAxisAlignedClip()
            End Try
        Finally
            _当前合成器 = oldCompositor
        End Try

        If trimImageCachesAfterPaint Then TrimD2DImageCachesToCurrentBudget()
    End Sub

    Private Sub DrawEmbeddedBackground_D2D(rt As ID2D1RenderTarget, w As Integer, h As Integer)
        If rt Is Nothing OrElse 背景颜色.A <= 0 Then Return
        Dim brushCache = _当前合成器?.BrushCache
        Dim br = brushCache?.Get(rt, 背景颜色)
        If br IsNot Nothing Then
            rt.FillRectangle(D3D_D2DInterop.ToD2DRect(New RectangleF(0, 0, w, h)), br)
            Return
        End If
        Using fallback = rt.CreateSolidColorBrush(D3D_D2DInterop.ToColor4(背景颜色))
            rt.FillRectangle(D3D_D2DInterop.ToD2DRect(New RectangleF(0, 0, w, h)), fallback)
        End Using
    End Sub


    Private Sub DrawBackground_D2D(rt As ID2D1RenderTarget, boundsRect As RectangleF, s As Single)
        Dim brushCache = _当前合成器?.BrushCache
        If 边框圆角半径 > 0 Then
            Using geo = D3D_RectangleRenderer.创建圆角矩形几何(boundsRect, 边框圆角半径 * s)
                D3D_RectangleRenderer.绘制圆角背景_D2D(rt, geo, boundsRect, 背景颜色, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
                D3D_RectangleRenderer.绘制圆角边框_D2D(rt, geo, 边框颜色, 边框宽度 * s, brushCache)
            End Using
        Else
            D3D_RectangleRenderer.绘制矩形背景_D2D(rt, boundsRect, 背景颜色, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
            D3D_RectangleRenderer.绘制矩形边框_D2D(rt, boundsRect, 边框颜色, 边框宽度 * s, brushCache)
        End If
    End Sub



    Private Sub DrawBlockDecoration_D2D(rt As ID2D1RenderTarget, block As MarkdownBlock, vl As VisualLine, vli As Integer,
                                        textLeft As Integer, drawY As Integer, clipW As Integer, s As Single)
        Select Case block.Kind
            Case BlockKind.HorizontalRule
                Dim ruleThick As Integer = Math.Max(1, CInt(分隔线粗细 * s))
                Dim ruleY As Integer = drawY + (vl.Height - ruleThick) \ 2
                Dim ruleInset As Integer = CInt(分隔线水平缩进 * s)
                rt.FillRectangle(D3D_D2DInterop.ToD2DRect(New RectangleF(textLeft + ruleInset, ruleY, Math.Max(0, clipW - ruleInset * 2), ruleThick)), _当前合成器.BrushCache.Get(rt, 分隔线颜色))

            Case BlockKind.BlockQuote
                Dim barH As Integer = vl.Height
                If vli + 1 < _visualLines.Count Then
                    Dim nextVl = _visualLines(vli + 1)
                    Dim nextBlock = _document.Blocks(nextVl.BlockIndex)
                    If nextBlock.Kind = BlockKind.BlockQuote Then
                        barH = nextVl.Y - vl.Y
                    End If
                End If
                Dim barColor As Color = If(block.AlertKind <> AlertKind.None, GetAlertColor(block.AlertKind), 引用条颜色)
                rt.FillRectangle(D3D_D2DInterop.ToD2DRect(New RectangleF(textLeft + CInt(引用条偏移 * s), drawY, Math.Max(1, CInt(引用条宽度 * s)), barH)), _当前合成器.BrushCache.Get(rt, barColor))

            Case BlockKind.CodeBlock
                Dim padT As Integer = CInt(代码块内边距.Top * s)
                Dim padB As Integer = CInt(代码块内边距.Bottom * s)
                Dim isFirstCodeLine = (vli = 0 OrElse _visualLines(vli - 1).BlockIndex <> vl.BlockIndex)
                Dim isLastCodeLine = (vli = _visualLines.Count - 1 OrElse _visualLines(vli + 1).BlockIndex <> vl.BlockIndex)
                Dim bgY = drawY - If(isFirstCodeLine, padT, 0)
                Dim bgH = vl.Height + If(isFirstCodeLine, padT, 0) + If(isLastCodeLine, padB, 0)
                rt.FillRectangle(D3D_D2DInterop.ToD2DRect(New RectangleF(textLeft, bgY, clipW, bgH)), _当前合成器.BrushCache.Get(rt, 代码块背景颜色))

            Case BlockKind.UnorderedListItem
                If vli > 0 AndAlso _visualLines(vli - 1).BlockIndex = vl.BlockIndex Then Return
                Dim unorderedMarkerIndent As Integer = GetListMarkerIndent(block, s)
                Dim bulletR As Integer = Math.Max(1, CInt(列表圆点半径 * s))
                Using geo = D3D_D2DInterop.GetD2DFactory().CreateEllipseGeometry(New Ellipse(New System.Numerics.Vector2(textLeft + unorderedMarkerIndent + CInt(列表圆点偏移X * s) + bulletR, drawY + vl.Height \ 2 + CInt(列表圆点偏移Y * s) + bulletR), bulletR, bulletR))
                    rt.FillGeometry(geo, _当前合成器.BrushCache.Get(rt, 文本颜色))
                End Using

            Case BlockKind.OrderedListItem
                If vli > 0 AndAlso _visualLines(vli - 1).BlockIndex = vl.BlockIndex Then Return
                Dim orderedMarkerIndent As Integer = GetListMarkerIndent(block, s)
                DrawText_D2D(rt, block.OrderIndex.ToString() & ".", GetBlockFont(block.Kind), 文本颜色,
                    New RectangleF(textLeft + orderedMarkerIndent, drawY, Math.Max(1, CInt(有序列表标记宽度 * s)), vl.Height), D2DTextAlign.Right, True)

            Case BlockKind.Table
                DrawTableDecoration_D2D(rt, block, vl, vli, textLeft, drawY, s)

            Case BlockKind.Heading1, BlockKind.Heading2
                If 标题分隔线粗细 > 0 Then
                    Dim isLastLineOfBlock As Boolean =
                        (vli + 1 >= _visualLines.Count OrElse _visualLines(vli + 1).BlockIndex <> vl.BlockIndex)
                    If isLastLineOfBlock Then
                        Dim sepThick As Integer = Math.Max(1, CInt(标题分隔线粗细 * s))
                        Dim sepGap As Integer = CInt(标题分隔线间距 * s)
                        Dim sepY As Integer = drawY + vl.Height + sepGap
                        rt.FillRectangle(D3D_D2DInterop.ToD2DRect(New RectangleF(textLeft, sepY, clipW, sepThick)), _当前合成器.BrushCache.Get(rt, 标题分隔线颜色))
                    End If
                End If
        End Select
    End Sub

    Private Sub DrawTableDecoration_D2D(rt As ID2D1RenderTarget, block As MarkdownBlock, vl As VisualLine, vli As Integer,
                                        textLeft As Integer, drawY As Integer, s As Single)
        If vl.TableSubLine <> 0 Then Return
        Dim colWidths As Integer() = Nothing
        If Not _tableColumnWidths.TryGetValue(vl.BlockIndex, colWidths) Then Return
        Dim colCount As Integer = colWidths.Length
        Dim totalW As Integer = colWidths.Sum()

        Dim cellPadding As Integer = CInt(表格单元格内边距 * s)
        Dim logicalRowH As Integer = vl.Height
        Dim look As Integer = vli + 1
        While look < _visualLines.Count
            Dim nextVl = _visualLines(look)
            If nextVl.BlockIndex <> vl.BlockIndex OrElse nextVl.TableRowIndex <> vl.TableRowIndex Then Exit While
            logicalRowH += nextVl.Height
            look += 1
        End While
        logicalRowH += cellPadding

        Dim topPad As Integer = cellPadding \ 2
        Dim rowTop As Integer = drawY - topPad

        If vl.TableRowIndex = 0 AndAlso block.HasHeader Then
            rt.FillRectangle(D3D_D2DInterop.ToD2DRect(New RectangleF(textLeft, rowTop, totalW, logicalRowH)), _当前合成器.BrushCache.Get(rt, 表头背景颜色))
        End If

        Dim lineBrush = _当前合成器.BrushCache.Get(rt, 表格边框颜色)
        Dim stroke As Single = Math.Max(1.0F, 表格边框粗细 * s)
        rt.DrawLine(New System.Numerics.Vector2(textLeft, rowTop), New System.Numerics.Vector2(textLeft + totalW, rowTop), lineBrush, stroke)
        rt.DrawLine(New System.Numerics.Vector2(textLeft, rowTop + logicalRowH), New System.Numerics.Vector2(textLeft + totalW, rowTop + logicalRowH), lineBrush, stroke)
        Dim x As Integer = textLeft
        For ci As Integer = 0 To colCount
            rt.DrawLine(New System.Numerics.Vector2(x, rowTop), New System.Numerics.Vector2(x, rowTop + logicalRowH), lineBrush, stroke)
            If ci < colCount Then x += colWidths(ci)
        Next
    End Sub



    Private Enum D2DTextAlign
        Left
        Center
        Right
    End Enum

    Private Function DrawFragments_D2D(rt As ID2D1RenderTarget, vl As VisualLine, vli As Integer,
                                       textLeft As Integer, drawY As Integer, blockKind As BlockKind,
                                       s As Single, selStart As SelectionPos, selEnd As SelectionPos) As Boolean
        Dim drewImage As Boolean = False
        For fi As Integer = 0 To vl.Fragments.Count - 1
            Dim frag = vl.Fragments(fi)
            Dim fragX As Integer = textLeft + frag.X
            Dim fragW As Integer = frag.Width

            If frag.Kind = InlineKind.Image Then
                If _hasSelection Then DrawFragmentSelection_D2D(rt, vli, fi, frag, fragX, drawY, vl.Height, selStart, selEnd)
                Dim imgH As Integer = If(frag.FragmentHeight > 0, frag.FragmentHeight, vl.Height)
                Dim imgRect As New RectangleF(fragX, drawY, frag.Width, imgH)
                If frag.ImageObj IsNot Nothing Then
                    Dim cache = GetD2DImageCache(frag.Url)
                    Dim bmp = cache?.GetBitmap(rt, frag.ImageObj)
                    If bmp IsNot Nothing Then
                        rt.DrawBitmap(bmp, D3D_D2DInterop.ToD2DRect(imgRect), 1.0F, BitmapInterpolationMode.Linear,
                            New Vortice.Mathematics.Rect(0, 0, bmp.Size.Width, bmp.Size.Height))
                        drewImage = True
                    End If
                Else
                    rt.DrawRectangle(D3D_D2DInterop.ToD2DRect(imgRect), _当前合成器.BrushCache.Get(rt, 图片占位边框颜色), Math.Max(1.0F, s))
                    If Not String.IsNullOrEmpty(frag.Text) Then
                        DrawText_D2D(rt, frag.Text, frag.UseFont, 图片占位文字颜色, imgRect, D2DTextAlign.Center, True)
                    End If
                End If
                Continue For
            End If

            If frag.BackColor <> Color.Empty AndAlso blockKind <> BlockKind.CodeBlock Then
                Dim padL As Integer = CInt(行内代码内边距.Left * s)
                Dim padR As Integer = CInt(行内代码内边距.Right * s)
                Using geo = D3D_RectangleRenderer.创建圆角矩形几何(New RectangleF(fragX - padL, drawY, fragW + padL + padR, vl.Height), CInt(行内代码圆角 * s))
                    rt.FillGeometry(geo, _当前合成器.BrushCache.Get(rt, frag.BackColor))
                End Using
            End If

            If _hasSelection Then DrawFragmentSelection_D2D(rt, vli, fi, frag, fragX, drawY, vl.Height, selStart, selEnd)

            If Not String.IsNullOrEmpty(frag.Text) Then
                If blockKind = BlockKind.Table AndAlso frag.TableColIndex >= 0 Then
                    Dim block = _document.Blocks(vl.BlockIndex)
                    Dim colWidths As Integer() = Nothing
                    Dim cellW As Integer = frag.Width
                    If _tableColumnWidths.TryGetValue(vl.BlockIndex, colWidths) AndAlso frag.TableColIndex < colWidths.Length Then
                        Dim cellPadding As Integer = CInt(表格单元格内边距 * s)
                        cellW = colWidths(frag.TableColIndex) - cellPadding * 2
                    End If
                    Dim align As D2DTextAlign = D2DTextAlign.Left
                    If block.ColumnAlignments IsNot Nothing AndAlso frag.TableColIndex < block.ColumnAlignments.Count Then
                        Select Case block.ColumnAlignments(frag.TableColIndex)
                            Case TableAlignment.Center : align = D2DTextAlign.Center
                            Case TableAlignment.Right : align = D2DTextAlign.Right
                        End Select
                    End If
                    DrawText_D2D(rt, frag.Text, frag.UseFont, frag.ForeColor, New RectangleF(fragX, drawY, cellW, vl.Height), align, True)
                Else
                    DrawText_D2D(rt, frag.Text, frag.UseFont, frag.ForeColor, New RectangleF(fragX, drawY, Short.MaxValue, vl.Height), D2DTextAlign.Left, True)
                End If
            End If

            If fragW > 0 Then
                Dim lineBrush = _当前合成器.BrushCache.Get(rt, frag.ForeColor)
                If frag.Kind = InlineKind.Strikethrough Then
                    rt.DrawLine(New System.Numerics.Vector2(fragX, drawY + vl.Height \ 2), New System.Numerics.Vector2(fragX + fragW, drawY + vl.Height \ 2), lineBrush, Math.Max(1.0F, 删除线粗细 * s))
                ElseIf frag.Kind = InlineKind.Link Then
                    Dim y As Integer = drawY + vl.Height - CInt(链接下划线偏移 * s)
                    rt.DrawLine(New System.Numerics.Vector2(fragX, y), New System.Numerics.Vector2(fragX + fragW, y), lineBrush, Math.Max(1.0F, 链接下划线粗细 * s))
                End If
            End If
        Next
        Return drewImage
    End Function

    Private Sub DrawFragmentSelection_D2D(rt As ID2D1RenderTarget, vli As Integer, fi As Integer, frag As VisualFragment,
                                          fragX As Integer, drawY As Integer, lineH As Integer,
                                          selStart As SelectionPos, selEnd As SelectionPos)
        If vli < selStart.VisualLine OrElse vli > selEnd.VisualLine Then Return
        Dim sChar As Integer = 0
        Dim eChar As Integer = frag.CharLength
        If vli = selStart.VisualLine Then
            If fi < selStart.FragmentIndex Then Return
            If fi = selStart.FragmentIndex Then sChar = selStart.CharOffset
        End If
        If vli = selEnd.VisualLine Then
            If fi > selEnd.FragmentIndex Then Return
            If fi = selEnd.FragmentIndex Then eChar = selEnd.CharOffset
        End If
        If eChar <= sChar OrElse frag.Text Is Nothing Then Return
        Dim safeS = Math.Min(sChar, frag.Text.Length)
        Dim safeE = Math.Min(eChar, frag.Text.Length)
        Dim x1 As Integer = fragX + If(safeS > 0, MeasureTextWidthCached(frag.Text.Substring(0, safeS), frag.UseFont, lineH), 0)
        Dim x2 As Integer = fragX + MeasureTextWidthCached(frag.Text.Substring(0, safeE), frag.UseFont, lineH)
        If x2 > x1 Then
            rt.FillRectangle(D3D_D2DInterop.ToD2DRect(New RectangleF(x1, drawY, x2 - x1, lineH)), _当前合成器.BrushCache.Get(rt, 选中背景颜色))
        End If
    End Sub

    Private Sub DrawText_D2D(rt As ID2D1RenderTarget, text As String, font As Font, color As Color,
                             rect As RectangleF, align As D2DTextAlign, verticalCenter As Boolean)
        If String.IsNullOrEmpty(text) OrElse font Is Nothing OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim weight As Vortice.DirectWrite.FontWeight = If((font.Style And System.Drawing.FontStyle.Bold) <> 0, Vortice.DirectWrite.FontWeight.Bold, Vortice.DirectWrite.FontWeight.Normal)
        Dim style As Vortice.DirectWrite.FontStyle = If((font.Style And System.Drawing.FontStyle.Italic) <> 0, Vortice.DirectWrite.FontStyle.Italic, Vortice.DirectWrite.FontStyle.Normal)
        Dim textAlign As Vortice.DirectWrite.TextAlignment
        Select Case align
            Case D2DTextAlign.Center : textAlign = Vortice.DirectWrite.TextAlignment.Center
            Case D2DTextAlign.Right : textAlign = Vortice.DirectWrite.TextAlignment.Trailing
            Case Else : textAlign = Vortice.DirectWrite.TextAlignment.Leading
        End Select
        Dim paraAlign As Vortice.DirectWrite.ParagraphAlignment = If(verticalCenter, Vortice.DirectWrite.ParagraphAlignment.Center, Vortice.DirectWrite.ParagraphAlignment.Near)
        Dim sizePx As Single = D3D_D2DInterop.GetDWriteFontSizePx(font, DpiScale())
        Dim fmt = _当前合成器.TextFormatCache.Get(font.FontFamily.Name, weight, style, sizePx, textAlign, paraAlign, True)
        rt.DrawText(text, fmt, D3D_D2DInterop.ToD2DRect(rect), _当前合成器.BrushCache.Get(rt, color))
    End Sub


    Private Sub DrawScrollBar_D2D(rt As ID2D1RenderTarget, w As Integer, h As Integer, s As Single)
        If _embeddedContentMode Then Return
        If Not _scrollBarVisible Then Return
        Dim scaledBorder As Integer = CInt(Math.Round(边框宽度 * s))
        Dim scaledRadius As Integer = CInt(Math.Round(边框圆角半径 * s))
        Dim scaledScrollW As Integer = CInt(Math.Round(滚动条宽度 * s))
        Dim viewH As Integer = ViewportHeight()
        If _totalContentHeight <= 0 OrElse viewH <= 0 Then Return
        _scrollBar.ComputeLayout(w, h, scaledBorder, scaledRadius, 0, 0, scaledScrollW,
            _totalContentHeight, viewH, _scrollY)
        _scrollBar.Draw_D2D(rt, w, h, scaledBorder, scaledRadius, scaledScrollW,
            滚动条轨道颜色, 滚动条颜色, 滚动条悬停颜色, _当前合成器?.BrushCache)
    End Sub

#End Region

#Region "鼠标处理"

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        Focus()
        If e.Button <> MouseButtons.Left Then Return

        If _scrollBarVisible Then
            If _scrollBar.BeginDrag(e.Location, _scrollY) Then Return
            Dim newOff = _scrollBar.TrackClick(e.Location, _scrollY, _totalContentHeight, ViewportHeight())
            If newOff <> _scrollY Then
                _scrollY = newOff
                ClampScroll()
                RequestV3Render()
                Return
            End If
        End If

        _mouseDownSelecting = True
        Dim pos = HitTestPos(e.X, e.Y)
        _selAnchor = pos
        _selCurrent = pos
        _hasSelection = False
        _mouseDownLinkUrl = HitTestLink(e.X, e.Y)
        RequestV3Render()
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If _scrollBar.IsDragging Then
            _scrollY = _scrollBar.DragMove(e.Y, _totalContentHeight, ViewportHeight())
            ClampScroll()
            RequestV3Render()
            Return
        End If

        If _scrollBarVisible AndAlso _scrollBar.TrackRect.Contains(e.Location) Then
            If _scrollBar.UpdateHover(e.Location) Then RequestV3Render()
            Cursor = Cursors.Default
        Else
            Cursor = If(HitTestLink(e.X, e.Y) IsNot Nothing, Cursors.Hand, Cursors.IBeam)
        End If

        If _mouseDownSelecting AndAlso e.Button = MouseButtons.Left Then
            _lastMousePos = e.Location
            _selCurrent = HitTestPos(e.X, e.Y)
            _hasSelection = CompareSelectionPos(_selAnchor, _selCurrent) <> 0
            Dim ci = GetContentInsets()
            If e.Y < ci.Top OrElse e.Y > ClientRectangle.Height - ci.Bottom Then
                If Not _autoScrollTimer.Enabled Then _autoScrollTimer.Start()
            Else
                _autoScrollTimer.Stop()
            End If
            RequestV3Render()
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If e.Button = MouseButtons.Left AndAlso Not _hasSelection AndAlso _mouseDownLinkUrl IsNot Nothing Then
            If HitTestLink(e.X, e.Y) = _mouseDownLinkUrl Then
                RaiseEvent LinkClicked(Me, New LinkClickedEventArgs(_mouseDownLinkUrl))
            End If
        End If
        _mouseDownLinkUrl = Nothing
        _mouseDownSelecting = False
        _scrollBar.EndDrag()
        _autoScrollTimer.Stop()
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        If _embeddedContentMode Then
            RaiseEvent EmbeddedMouseWheel(Me, e)
            Return
        End If
        ScrollBy(-CInt(e.Delta / 120.0 * 60))
    End Sub

    Private Sub AutoScrollTick(sender As Object, e As EventArgs)
        If Not _mouseDownSelecting Then
            _autoScrollTimer.Stop()
            Return
        End If
        Dim ci = GetContentInsets()
        Dim scrollDelta As Integer
        If _lastMousePos.Y < ci.Top Then
            scrollDelta = -20
        ElseIf _lastMousePos.Y > ClientRectangle.Height - ci.Bottom Then
            scrollDelta = 20
        Else
            _autoScrollTimer.Stop()
            Return
        End If
        ScrollBy(scrollDelta)
        _selCurrent = HitTestPos(_lastMousePos.X, _lastMousePos.Y)
        _hasSelection = CompareSelectionPos(_selAnchor, _selCurrent) <> 0
        RequestV3Render()
    End Sub

#End Region

#Region "键盘处理"

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If e.Control Then
            Select Case e.KeyCode
                Case Keys.C : CopySelection() : e.Handled = True
                Case Keys.A : SelectAll() : e.Handled = True
            End Select
        End If
        Select Case e.KeyCode
            Case Keys.Up : ScrollBy(-40) : e.Handled = True
            Case Keys.Down : ScrollBy(40) : e.Handled = True
            Case Keys.PageUp : ScrollBy(-ViewportHeight()) : e.Handled = True
            Case Keys.PageDown : ScrollBy(ViewportHeight()) : e.Handled = True
            Case Keys.Home : If e.Control Then ScrollToTop() : e.Handled = True
            Case Keys.End : If e.Control Then ScrollToBottom() : e.Handled = True
        End Select
    End Sub

    Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
        Select Case keyData And Keys.KeyCode
            Case Keys.Up, Keys.Down, Keys.PageUp, Keys.PageDown
                Return True
        End Select
        Return MyBase.IsInputKey(keyData)
    End Function

#End Region

#Region "选中逻辑"

    Private Sub SelectAll()
        If _visualLines.Count = 0 Then Return
        _selAnchor = New SelectionPos(0, 0, 0)
        Dim lastVl = _visualLines(_visualLines.Count - 1)
        Dim lastFi As Integer = Math.Max(0, lastVl.Fragments.Count - 1)
        _selCurrent = New SelectionPos(_visualLines.Count - 1, lastFi,
            If(lastVl.Fragments.Count > 0, lastVl.Fragments(lastFi).CharLength, 0))
        _hasSelection = True
        RequestV3Render()
    End Sub

    Private Sub ClearSelection()
        _hasSelection = False
    End Sub

    Private Sub CopySelection()
        If Not _hasSelection Then Return
        Try
            Dim text = GetSelectedText()
            If Not String.IsNullOrEmpty(text) Then Clipboard.SetText(text)
        Catch ex As ExternalException
        End Try
    End Sub

    Private Sub GetOrderedSelection(ByRef startPos As SelectionPos, ByRef endPos As SelectionPos)
        If CompareSelectionPos(_selAnchor, _selCurrent) <= 0 Then
            startPos = _selAnchor : endPos = _selCurrent
        Else
            startPos = _selCurrent : endPos = _selAnchor
        End If
    End Sub

    Private Shared Function CompareSelectionPos(a As SelectionPos, b As SelectionPos) As Integer
        If a.VisualLine <> b.VisualLine Then Return a.VisualLine.CompareTo(b.VisualLine)
        If a.FragmentIndex <> b.FragmentIndex Then Return a.FragmentIndex.CompareTo(b.FragmentIndex)
        Return a.CharOffset.CompareTo(b.CharOffset)
    End Function

#End Region

#Region "命中测试"

    Private Function HitTestCoords(mx As Integer, my As Integer) As (hitX As Integer, hitY As Integer, vli As Integer)
        Dim ci = GetContentInsets()
        Dim hitY As Integer = my - ci.Top + _scrollY
        Dim hitX As Integer = mx - ci.Left
        Dim vli As Integer = GetFirstVisibleVisualLine(hitY)
        Return (hitX, hitY, vli)
    End Function

    Private Function HitTestPos(mx As Integer, my As Integer) As SelectionPos
        EnsureLayoutDpiCurrent()
        If _visualLines.Count = 0 Then Return New SelectionPos(0, 0, 0)
        Dim hit = HitTestCoords(mx, my)
        Dim vl = _visualLines(hit.vli)
        If vl.Fragments.Count = 0 Then Return New SelectionPos(hit.vli, 0, 0)

        For fi As Integer = 0 To vl.Fragments.Count - 1
            Dim frag = vl.Fragments(fi)
            If hit.hitX < frag.X + frag.Width OrElse fi = vl.Fragments.Count - 1 Then
                Dim charOff As Integer = 0
                If frag.Text IsNot Nothing AndAlso frag.Text.Length > 0 Then
                    charOff = D3D_TextMeasureHelper.FindColFromX_D2D(frag.Text, hit.hitX - frag.X, frag.UseFont, DpiScale(), GetTextFormatCacheForMeasure())
                End If
                Return New SelectionPos(hit.vli, fi, charOff)
            End If
        Next
        Return New SelectionPos(hit.vli, 0, 0)
    End Function

    Private Function HitTestLink(mx As Integer, my As Integer) As String
        EnsureLayoutDpiCurrent()
        If _visualLines.Count = 0 Then Return Nothing
        Dim hit = HitTestCoords(mx, my)
        If hit.vli >= _visualLines.Count Then Return Nothing
        Dim vl = _visualLines(hit.vli)
        If hit.hitY < vl.Y OrElse hit.hitY >= vl.Y + vl.Height Then Return Nothing
        For Each frag In vl.Fragments
            If frag.Kind = InlineKind.Link AndAlso frag.Url IsNot Nothing Then
                If hit.hitX >= frag.X AndAlso hit.hitX < frag.X + frag.Width Then Return frag.Url
            End If
        Next
        Return Nothing
    End Function

    Friend Function HitTestEmbeddedLink(localX As Integer, localY As Integer) As String
        Return HitTestLink(localX, localY)
    End Function

#End Region

#Region "滚动辅助"

    Private Function GetFirstVisibleVisualLine(contentY As Integer) As Integer
        If _visualLines.Count = 0 Then Return 0
        Dim lo As Integer = 0
        Dim hi As Integer = _visualLines.Count - 1
        Dim best As Integer = hi

        While lo <= hi
            Dim mid As Integer = (lo + hi) \ 2
            Dim vl = _visualLines(mid)
            If vl.Y + vl.Height > contentY Then
                best = mid
                hi = mid - 1
            Else
                lo = mid + 1
            End If
        End While

        Return Math.Max(0, Math.Min(best, _visualLines.Count - 1))
    End Function

    Private Sub ScrollBy(delta As Integer)
        _scrollY += delta
        ClampScroll()
        RequestV3Render()
    End Sub

    Private Function ViewportHeight() As Integer
        If _embeddedContentMode Then Return Integer.MaxValue \ 4
        Dim ci = GetContentInsets()
        Return ClientRectangle.Height - ci.Top - ci.Bottom
    End Function

    Private Function MaxScrollY() As Integer
        Return Math.Max(0, _totalContentHeight - ViewportHeight())
    End Function

    Private Sub ClampScroll()
        _scrollY = Math.Max(0, Math.Min(_scrollY, MaxScrollY()))
        UpdateScrollBarState()
    End Sub

    Private Sub UpdateScrollBarState()
        If _embeddedContentMode Then
            _scrollBarVisible = False
            _scrollY = 0
            Return
        End If
        _scrollBarVisible = _totalContentHeight > ViewportHeight()
    End Sub

    Private Function TextAreaWidth() As Integer
        Dim ci = GetContentInsets()
        Dim scrollW As Integer = If(_scrollBarVisible AndAlso Not _embeddedContentMode, CInt(Math.Round(滚动条宽度 * DpiScale())) + V3_ScrollBarRenderer.Margin * 2, 0)
        Return ClientRectangle.Width - ci.Left - Math.Max(ci.Right, scrollW)
    End Function

#End Region

#Region "流式刷新"

    Private Sub StreamTimerTick(sender As Object, e As EventArgs)
        If Not _streamDirty Then
            _streamTimer.Stop()
            Return
        End If
        _streamDirty = False
        Dim wasAtBottom As Boolean = (_scrollY >= MaxScrollY() - 2)
        ParseAndLayout(keepAtBottom:=自动滚动 AndAlso wasAtBottom)
    End Sub

#End Region

#Region "字体管理"

    Private Const FontBold As String = "Bold"
    Private Const FontItalic As String = "Italic"
    Private Const FontBoldItalic As String = "BoldItalic"
    Private Const FontCode As String = "Code"
    Private Const FontH1 As String = "H1"
    Private Const FontH2 As String = "H2"
    Private Const FontH3 As String = "H3"
    Private Const FontH4 As String = "H4"
    Private Const FontH5 As String = "H5"
    Private Const FontH6 As String = "H6"

    Private Function GetBlockFont(kind As BlockKind) As Font
        EnsureFontCache()
        Select Case kind
            Case BlockKind.Heading1 : Return _fontCache(FontH1)
            Case BlockKind.Heading2 : Return _fontCache(FontH2)
            Case BlockKind.Heading3 : Return _fontCache(FontH3)
            Case BlockKind.Heading4 : Return _fontCache(FontH4)
            Case BlockKind.Heading5 : Return _fontCache(FontH5)
            Case BlockKind.Heading6 : Return _fontCache(FontH6)
            Case Else : Return Font
        End Select
    End Function

    Private Function GetBlockForeColor(kind As BlockKind) As Color
        Select Case kind
            Case BlockKind.Heading1, BlockKind.Heading2, BlockKind.Heading3,
                 BlockKind.Heading4, BlockKind.Heading5, BlockKind.Heading6
                Return 标题颜色
            Case BlockKind.BlockQuote
                Return 引用文字颜色
            Case Else
                Return 文本颜色
        End Select
    End Function

    Private Function GetInlineFont(kind As InlineKind, blockFont As Font) As Font
        EnsureFontCache()
        Select Case kind
            Case InlineKind.Bold : Return _fontCache(FontBold)
            Case InlineKind.Italic : Return _fontCache(FontItalic)
            Case InlineKind.BoldItalic : Return _fontCache(FontBoldItalic)
            Case InlineKind.Code : Return GetInlineCodeFont(blockFont)
            Case Else : Return blockFont
        End Select
    End Function

    Private Function GetInlineForeColor(kind As InlineKind, blockFore As Color) As Color
        Select Case kind
            Case InlineKind.Bold, InlineKind.BoldItalic : Return If(粗体颜色 = Color.Empty, blockFore, 粗体颜色)
            Case InlineKind.Italic : Return If(斜体颜色 = Color.Empty, blockFore, 斜体颜色)
            Case InlineKind.Code : Return 代码颜色
            Case InlineKind.Strikethrough : Return 删除线颜色
            Case InlineKind.Link : Return 链接颜色
            Case Else : Return blockFore
        End Select
    End Function

    Private Function GetCodeFont() As Font
        If 代码字体 IsNot Nothing Then Return 代码字体
        EnsureFontCache()
        Return _fontCache(FontCode)
    End Function

    Private Function GetInlineCodeFont(blockFont As Font) As Font
        If 代码字体 IsNot Nothing Then Return 代码字体
        Return blockFont
    End Function

    Private Sub EnsureFontCache()
        If Font Is _lastBaseFont AndAlso _fontCache IsNot Nothing Then Return
        DisposeFontCache()
        _lastBaseFont = Font
        Dim sz As Single = Font.SizeInPoints
        Dim family As String = Font.FontFamily.Name
        _fontCache = New Dictionary(Of String, Font) From {
            {FontBold, New Font(family, sz, FontStyle.Bold)},
            {FontItalic, New Font(family, sz, FontStyle.Italic)},
            {FontBoldItalic, New Font(family, sz, FontStyle.Bold Or FontStyle.Italic)},
            {FontCode, New Font("Consolas", sz)},
            {FontH1, New Font(family, sz * 2.0F, FontStyle.Bold)},
            {FontH2, New Font(family, sz * 1.6F, FontStyle.Bold)},
            {FontH3, New Font(family, sz * 1.3F, FontStyle.Bold)},
            {FontH4, New Font(family, sz * 1.1F, FontStyle.Bold)},
            {FontH5, New Font(family, sz, FontStyle.Bold)},
            {FontH6, New Font(family, sz * 0.9F, FontStyle.Bold)}
        }
    End Sub

    Private Sub DisposeFontCache()
        If _fontCache IsNot Nothing Then
            For Each f In _fontCache.Values
                f.Dispose()
            Next
            _fontCache = Nothing
        End If
        _lastBaseFont = Nothing
    End Sub

    Private Sub InvalidateFontCache()
        DisposeFontCache()
        InvalidateMeasureCache()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        InvalidateFontCache()
        RebuildLayout()
        InvalidateV3TextResources()
        RequestEmbeddedOrSelfRefresh()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        InvalidateFontCache()
        RebuildLayout()
        ClampScroll()
        InvalidateV3TextResources()
        RequestEmbeddedOrSelfRefresh()
    End Sub

#End Region

#Region "图片管理"

    ''' <summary>将图片 URL 解析为可加载的完整路径或绝对 URL。</summary>
    Private Function ResolveImageUrl(url As String) As String
        If String.IsNullOrEmpty(url) Then Return ""
        If url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) OrElse
           url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) Then
            Return url
        End If
        If IO.Path.IsPathRooted(url) Then Return url
        Dim basePath = GetEffectiveBasePath()
        Return IO.Path.GetFullPath(IO.Path.Combine(basePath, url.Replace("/"c, IO.Path.DirectorySeparatorChar)))
    End Function

    Private Function GetEffectiveBasePath() As String
        If Not String.IsNullOrEmpty(基础路径) Then Return 基础路径
        Return AppDomain.CurrentDomain.BaseDirectory
    End Function

    Private Function GetEmbeddedInvalidationTarget() As Control
        If Not _embeddedContentMode Then Return Nothing
        Dim target = _embeddedInvalidationTarget
        If target Is Nothing OrElse target.IsDisposed OrElse Not target.IsHandleCreated Then Return Nothing
        Return target
    End Function

    Private Function GetAsyncCallbackTarget() As Control
        If IsHandleCreated AndAlso Not IsDisposed Then Return Me
        Return GetEmbeddedInvalidationTarget()
    End Function

    ''' <summary>从缓存获取图片，如未加载则启动加载。</summary>
    Private Function TryGetOrLoadImage(resolvedUrl As String) As Image
        If String.IsNullOrEmpty(resolvedUrl) Then Return Nothing
        Dim cached As Image = Nothing
        If _imageCache.TryGetValue(resolvedUrl, cached) Then
            If cached IsNot Nothing Then _imageLastUsed(resolvedUrl) = NextImageCacheClock()
            Return cached
        End If
        If _imageLoadingUrls.Contains(resolvedUrl) Then Return Nothing
        If resolvedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) OrElse
           resolvedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) Then
            LoadImageFromUrlAsync(resolvedUrl)
            Return Nothing
        End If
        LoadImageFromFileAsync(resolvedUrl)
        Return Nothing
    End Function

    Private Function LoadImageFromFile(path As String) As Image
        Try
            If Not IO.File.Exists(path) Then Return Nothing
            Dim data = IO.File.ReadAllBytes(path)
            Using ms As New IO.MemoryStream(data)
                Using original = Image.FromStream(ms)
                    Return New Bitmap(original)
                End Using
            End Using
        Catch
            Return Nothing
        End Try
    End Function

    Private Sub LoadImageFromFileAsync(path As String)
        _imageLoadingUrls.Add(path)
        Dim loadVersion = _imageLoadVersion
        Task.Run(Sub()
                     Dim img = LoadImageFromFile(path)
                     If img Is Nothing Then
                         CacheNothing(path, loadVersion)
                     Else
                         PostImageLoadResult(path, img, loadVersion)
                     End If
                 End Sub)
    End Sub

    Private Sub LoadImageFromUrlAsync(url As String)
        _imageLoadingUrls.Add(url)
        Dim loadVersion = _imageLoadVersion
        Task.Run(Async Function()
                     Try
                         Using response = Await _httpClient.GetAsync(url)
                             response.EnsureSuccessStatusCode()
                             ' GDI+ 仅支持光栅格式；SVG / XML 等矢量格式直接跳过
                             Dim contentType = response.Content.Headers.ContentType?.MediaType
                             If contentType IsNot Nothing AndAlso Not IsRasterContentType(contentType) Then
                                 CacheNothing(url, loadVersion)
                                 Return
                             End If
                             Dim data = Await response.Content.ReadAsByteArrayAsync()
                             ' 二次检查：文件头以 '<' 开头大概率是 SVG/XML
                             If data.Length > 0 AndAlso data(0) = CByte(AscW("<"c)) Then
                                 CacheNothing(url, loadVersion)
                                 Return
                             End If
                             Using ms As New IO.MemoryStream(data)
                                 Using original = Image.FromStream(ms)
                                     Dim bmp As New Bitmap(original)
                                     PostImageLoadResult(url, bmp, loadVersion)
                                 End Using
                             End Using
                         End Using
                     Catch
                         CacheNothing(url, loadVersion)
                     End Try
                 End Function)
    End Sub

    Private Sub PostImageLoadResult(url As String, img As Image, loadVersion As Integer)
        If IsHandleCreated AndAlso Not IsDisposed Then
            Try
                BeginInvoke(Sub()
                                If loadVersion = _imageLoadVersion Then
                                    StoreImageCacheEntry(url, img)
                                    _imageLoadingUrls.Remove(url)
                                    RebuildLayout()
                                    RequestV3Render()
                                Else
                                    img?.Dispose()
                                End If
                            End Sub)
                Return
            Catch
            End Try
        End If
        Dim target = GetEmbeddedInvalidationTarget()
        If target IsNot Nothing Then
            Try
                target.BeginInvoke(Sub()
                                       If loadVersion = _imageLoadVersion AndAlso Not IsDisposed Then
                                           StoreImageCacheEntry(url, img)
                                           _imageLoadingUrls.Remove(url)
                                           RebuildLayout()
                                           If _embeddedInvalidationTarget IsNot Nothing AndAlso Not _embeddedInvalidationTarget.IsDisposed Then
                                               RequestV3Render(_embeddedInvalidationTarget)
                                           End If
                                       Else
                                           img?.Dispose()
                                       End If
                                   End Sub)
                Return
            Catch
            End Try
        End If
        img?.Dispose()
    End Sub

    ''' <summary>判断 Content-Type 是否为 GDI+ 可解码的光栅图片格式。</summary>
    Private Shared Function IsRasterContentType(mediaType As String) As Boolean
        Select Case mediaType.ToLowerInvariant()
            Case "image/png", "image/jpeg", "image/gif", "image/bmp",
                 "image/tiff", "image/x-icon", "image/webp",
                 "image/x-ms-bmp"
                Return True
            Case Else
                ' 未知 image/* 子类型也尝试解码，但排除已知不支持的
                If mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) AndAlso
                   Not mediaType.Contains("svg", StringComparison.OrdinalIgnoreCase) AndAlso
                   Not mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase) Then
                    Return True
                End If
                Return False
        End Select
    End Function

    ''' <summary>将 url 标记为不可用并从加载队列移除。</summary>
    Private Sub CacheNothing(url As String, Optional loadVersion As Integer = -1)
        If IsHandleCreated AndAlso Not IsDisposed Then
            Try
                BeginInvoke(Sub()
                                If loadVersion < 0 OrElse loadVersion = _imageLoadVersion Then
                                    _imageCache(url) = Nothing
                                    _imageLastUsed.Remove(url)
                                    _imageLoadingUrls.Remove(url)
                                End If
                            End Sub)
            Catch
            End Try
        End If
        Dim target = GetEmbeddedInvalidationTarget()
        If target IsNot Nothing Then
            Try
                target.BeginInvoke(Sub()
                                       If loadVersion < 0 OrElse loadVersion = _imageLoadVersion Then
                                           _imageCache(url) = Nothing
                                           _imageLastUsed.Remove(url)
                                           _imageLoadingUrls.Remove(url)
                                       End If
                                   End Sub)
            Catch
            End Try
        End If
    End Sub

    Private Function NextImageCacheClock() As Long
        Return D3D_CpuCache.NextTick()
    End Function

    Private Shared Function EstimateImageBytes(img As Image) As Long
        If img Is Nothing Then Return 0
        Return CLng(Math.Max(1, img.Width)) * CLng(Math.Max(1, img.Height)) * 4L
    End Function

    Private Sub StoreImageCacheEntry(url As String, img As Image)
        Dim old As Image = Nothing
        If _imageCache.TryGetValue(url, old) AndAlso old IsNot Nothing AndAlso Not Object.ReferenceEquals(old, img) Then
            old.Dispose()
        End If
        _imageCache(url) = img
        If img IsNot Nothing Then
            _imageLastUsed(url) = NextImageCacheClock()
        Else
            _imageLastUsed.Remove(url)
        End If
        D3D_CpuCache.TrimToBudget(Me)
    End Sub

    Private Function GetActiveImageUrls() As HashSet(Of String)
        Dim active As New HashSet(Of String)(StringComparer.Ordinal)
        For Each visualLine In _visualLines
            For Each fragment In visualLine.Fragments
                If fragment.Kind = InlineKind.Image AndAlso Not String.IsNullOrEmpty(fragment.Url) Then
                    active.Add(fragment.Url)
                End If
            Next
        Next
        Return active
    End Function

    Private Sub TrimImageCache(protectedUrls As HashSet(Of String))
        While CacheBytes > Math.Max(0L, GlobalOptions.CpuCacheBudgetBytes)
            If Not TrimOldestImage(protectedUrls) Then Exit While
        End While
    End Sub

    Private Function TrimOldestImage(protectedUrls As HashSet(Of String)) As Boolean
        Dim removeUrl As String = Nothing
        Dim oldest As Long = Long.MaxValue
        For Each kvp In _imageCache
            If kvp.Value Is Nothing Then Continue For
            If protectedUrls IsNot Nothing AndAlso protectedUrls.Contains(kvp.Key) Then Continue For
            Dim lastUsed As Long = 0
            If Not _imageLastUsed.TryGetValue(kvp.Key, lastUsed) Then lastUsed = 0
            If lastUsed < oldest Then
                oldest = lastUsed
                removeUrl = kvp.Key
            End If
        Next
        If String.IsNullOrEmpty(removeUrl) Then Return False
        RemoveImageCacheEntry(removeUrl)
        Return True
    End Function

    Private Sub RemoveImageCacheEntry(url As String)
        If String.IsNullOrEmpty(url) Then Return
        Dim img As Image = Nothing
        If _imageCache.TryGetValue(url, img) Then img?.Dispose()
        _imageCache.Remove(url)
        _imageLastUsed.Remove(url)
        Dim d2dCache As D3D_D2DInterop.D2DBitmapCache = Nothing
        If _d2dImageCaches.TryGetValue(url, d2dCache) Then
            d2dCache.Dispose()
            _d2dImageCaches.Remove(url)
        End If
    End Sub

    Private Sub DisposeImageCache()
        _imageLoadVersion += 1
        DisposeD2DImageCache()
        For Each img In _imageCache.Values
            img?.Dispose()
        Next
        _imageCache.Clear()
        _imageLastUsed.Clear()
        _imageLoadingUrls.Clear()
    End Sub

    ''' <summary>清除图片缓存，释放已加载的图片内存。下次布局时将重新加载。</summary>
    Public Sub ClearImageCache()
        DisposeImageCache()
        RebuildLayout()
        RequestV3Render()
    End Sub

#End Region

#Region "D2D 资源管理"

    Private Sub CleanupRenderCaches(level As D3DCacheCleanupLevel)
        Select Case level
            Case D3DCacheCleanupLevel.TrimToBudget
                D3D_CpuCache.TrimToBudget()
                For Each cache In _d2dImageCaches.Values
                    Try : cache.TrimToCurrentBudget() : Catch : End Try
                Next

            Case D3DCacheCleanupLevel.ReleaseVolatileCaches
                DisposeD2DImageCache()

            Case Else
                DisposeImageCache()
        End Select
    End Sub

    Private Sub DisposeD2DResources()
        DisposeD2DImageCache()
    End Sub

    Private Sub DisposeD2DImageCache()
        For Each cache In _d2dImageCaches.Values
            cache.Dispose()
        Next
        _d2dImageCaches.Clear()
    End Sub

    Private Function GetD2DImageCache(url As String) As D3D_D2DInterop.D2DBitmapCache
        If String.IsNullOrEmpty(url) Then Return Nothing
        Dim cache As D3D_D2DInterop.D2DBitmapCache = Nothing
        If Not _d2dImageCaches.TryGetValue(url, cache) Then
            cache = New D3D_D2DInterop.D2DBitmapCache()
            _d2dImageCaches(url) = cache
        End If
        Return cache
    End Function

    Private Sub TrimD2DImageCachesToCurrentBudget()
        For Each cache In _d2dImageCaches.Values
            Try : cache.TrimToCurrentBudget() : Catch : End Try
        Next
    End Sub

    Public ReadOnly Property CacheBytes As Long Implements D3D_IRenderCacheOwner.CacheBytes
        Get
            Dim total As Long = 0
            For Each img In _imageCache.Values
                total += EstimateImageBytes(img)
            Next
            Return total
        End Get
    End Property

    Public ReadOnly Property OldestUseTick As Long Implements D3D_IRenderCacheOwner.OldestUseTick
        Get
            Dim oldest As Long = Long.MaxValue
            For Each kvp In _imageCache
                If kvp.Value Is Nothing Then Continue For
                Dim tick As Long = 0
                If Not _imageLastUsed.TryGetValue(kvp.Key, tick) Then tick = 0
                If tick < oldest Then oldest = tick
            Next
            Return oldest
        End Get
    End Property

    Public Function TrimOldest() As Boolean Implements D3D_IRenderCacheOwner.TrimOldest
        Return TrimOldestImage(Nothing)
    End Function

    Public Sub ReleaseAll() Implements D3D_IRenderCacheOwner.ReleaseAll
        DisposeImageCache()
    End Sub

#End Region

#Region "辅助方法"

    Private Sub InvalidateMeasureCache()
        _measureVersion += 1
        _textWidthCache.Clear()
    End Sub

    Private Function GetFontMeasureHash(font As Font) As Integer
        If font Is Nothing Then Return 0
        Return System.HashCode.Combine(font.FontFamily.Name, font.Style, font.SizeInPoints, font.Unit)
    End Function

    Private Function MeasureTextWidthCached(text As String, font As Font, lineH As Integer) As Integer
        If String.IsNullOrEmpty(text) Then Return 0
        Dim key As New TextWidthKey With {
            .Text = text,
            .FontHash = GetFontMeasureHash(font),
            .LineHeight = lineH,
            .Version = _measureVersion
        }
        Dim cached As Integer = 0
        If _textWidthCache.TryGetValue(key, cached) Then Return cached

        cached = CInt(Math.Ceiling(D3D_TextMeasureHelper.MeasureTextWidth_D2D(text, font, DpiScale(), GetTextFormatCacheForMeasure())))
        If _textWidthCache.Count >= MaxTextWidthCacheEntries Then _textWidthCache.Clear()
        _textWidthCache(key) = cached
        Return cached
    End Function

    Private Function GetLayoutLineHeight(font As Font) As Integer
        Return CInt(Math.Ceiling(D3D_D2DInterop.GetDWriteLineHeightPx(font, DpiScale())))
    End Function

    Private Function GetTextFormatCacheForMeasure() As D3D_D2DInterop.TextFormatCache
        Return Nothing
    End Function

    Private Function GetAlertColor(kind As AlertKind) As Color
        Select Case kind
            Case AlertKind.Note : Return 提示Note颜色
            Case AlertKind.Tip : Return 提示Tip颜色
            Case AlertKind.Important : Return 提示Important颜色
            Case AlertKind.Warning : Return 提示Warning颜色
            Case AlertKind.Caution : Return 提示Caution颜色
            Case Else : Return 引用条颜色
        End Select
    End Function

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            RebuildLayout()
            RequestV3Render()
        End If
    End Sub

    Private Sub RequestEmbeddedOrSelfRefresh()
        Dim target = GetEmbeddedInvalidationTarget()
        If target IsNot Nothing Then
            RequestV3Render(target)
        Else
            RequestV3Render()
        End If
    End Sub

    Private Sub InvalidateV3TextResources()
        Dim target = If(GetEmbeddedInvalidationTarget(), DirectCast(Me, Control))
        D3D_RenderCore.InvalidateExistingTextResources(target)
    End Sub

    Private Function DpiScale() As Single
        Return EffectiveDeviceDpi() / 96.0F
    End Function

    Private Function EffectiveDeviceDpi() As Integer
        If _embeddedContentMode AndAlso _embeddedHostDpi > 0 Then Return _embeddedHostDpi
        Return V3_DpiContext.FromControl(Me).Dpi
    End Function

    Private Function GetContentInsets() As ContentInsets
        Dim bi As Integer = CInt(Math.Round(边框宽度 * DpiScale()))
        Return New ContentInsets With {
            .Left = Math.Max(Padding.Left, bi),
            .Top = Math.Max(Padding.Top, bi),
            .Right = Math.Max(Padding.Right, bi),
            .Bottom = Math.Max(Padding.Bottom, bi)
        }
    End Function

#End Region

End Class
