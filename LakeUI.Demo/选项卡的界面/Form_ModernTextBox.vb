
Public Class Form_ModernTextBox
    Private Sub Form_ModernTextBox_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.ModernTextBox2.SyntaxHighlighter = New VbKeywordHighlighter()
    End Sub

End Class


Public Class VbKeywordHighlighter
    Implements ModernTextBox.ISyntaxHighlighter

    ' ═══════════ 颜色定义（VS 2022 深色主题配色） ═══════════
    Private Shared ReadOnly ColorKeyword As Color = Color.FromArgb(86, 156, 214)       ' 蓝色 - 关键字
    Private Shared ReadOnly ColorControlFlow As Color = Color.FromArgb(216, 160, 223)  ' 紫色 - 流程控制
    Private Shared ReadOnly ColorTypeName As Color = Color.FromArgb(78, 201, 176)      ' 青色 - 类型名
    Private Shared ReadOnly ColorString As Color = Color.FromArgb(214, 157, 133)       ' 橙色 - 字符串
    Private Shared ReadOnly ColorComment As Color = Color.FromArgb(87, 166, 74)        ' 绿色 - 注释
    Private Shared ReadOnly ColorNumber As Color = Color.FromArgb(181, 206, 168)       ' 浅绿 - 数字
    Private Shared ReadOnly ColorOperatorWord As Color = Color.FromArgb(86, 156, 214)  ' 蓝色 - 运算符关键字
    Private Shared ReadOnly ColorPreprocessor As Color = Color.FromArgb(155, 155, 155) ' 灰色 - 预处理指令
    Private Shared ReadOnly ColorXmlDoc As Color = Color.FromArgb(96, 139, 78)         ' 深绿 - XML文档注释

    ' ═══════════ 关键字分类表 ═══════════
    Private Shared ReadOnly Keywords As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "AddHandler", "AddressOf", "Alias", "And", "AndAlso",
        "ByRef", "ByVal", "Call", "Case", "Catch",
        "CBool", "CByte", "CChar", "CDate", "CDbl", "CDec", "CInt", "CLng",
        "CObj", "CSByte", "CShort", "CSng", "CStr", "CType", "CUInt", "CULng", "CUShort",
        "Class", "Const", "Continue", "Declare", "Default",
        "Delegate", "Dim", "DirectCast", "Do",
        "Each", "Else", "ElseIf", "End", "EndIf", "Enum", "Erase", "Error", "Event",
        "Exit", "Finally", "For", "Friend", "Function",
        "Get", "GetType", "GetXmlNamespace", "Global", "GoSub", "GoTo",
        "Handles", "If", "Implements", "Imports", "In", "Inherits",
        "Interface", "Is", "IsNot", "Let", "Lib", "Like", "Loop",
        "Me", "Mod", "Module", "MustInherit", "MustOverride", "MyBase", "MyClass",
        "Namespace", "Narrowing", "New", "Next",
        "Not", "Nothing", "NotInheritable", "NotOverridable",
        "Of", "On", "Operator", "Option", "Optional", "Or", "OrElse",
        "Overloads", "Overridable", "Overrides",
        "ParamArray", "Partial", "Private", "Property", "Protected", "Public",
        "RaiseEvent", "ReadOnly", "ReDim", "REM", "RemoveHandler",
        "Resume", "Return", "Select", "Set", "Shadows", "Shared",
        "Static", "Step", "Stop", "Structure", "Sub", "SyncLock",
        "Then", "Throw", "To", "Try", "TryCast", "TypeOf",
        "Using", "Wend", "When", "While", "Widening", "With", "WithEvents", "WriteOnly",
        "Xor", "As"
    }

    Private Shared ReadOnly ControlFlowKeywords As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "If", "Then", "Else", "ElseIf", "End If",
        "Select", "Case",
        "For", "Each", "Next", "To", "Step",
        "While", "Wend", "Do", "Loop", "Until",
        "Try", "Catch", "Finally", "Throw",
        "GoTo", "GoSub", "Resume", "Continue", "Exit", "Return",
        "With", "Using", "SyncLock",
        "When"
    }

    Private Shared ReadOnly TypeNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "Boolean", "Byte", "Char", "Date", "DateTime", "Decimal", "Double",
        "Integer", "Long", "Object", "SByte", "Short", "Single", "String",
        "UInteger", "ULong", "UShort",
        "Task", "List", "Dictionary", "Array", "Hashtable",
        "EventArgs", "Exception", "Type", "Guid",
        "TimeSpan", "StringBuilder", "Regex", "Match",
        "Stream", "StreamReader", "StreamWriter",
        "Color", "Point", "PointF", "Size", "SizeF", "Rectangle", "RectangleF",
        "Font", "Bitmap", "Image", "Graphics", "Pen", "Brush", "SolidBrush",
        "Form", "Control", "Panel", "Button", "Label", "TextBox", "ComboBox",
        "Timer", "BackgroundWorker", "DefaultEvent"
    }

    Private Shared ReadOnly OperatorWords As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "And", "AndAlso", "Or", "OrElse", "Xor", "Not",
        "Is", "IsNot", "Like", "Mod",
        "TypeOf", "GetType", "DirectCast", "TryCast", "CType",
        "CBool", "CByte", "CChar", "CDate", "CDbl", "CDec", "CInt", "CLng",
        "CObj", "CSByte", "CShort", "CSng", "CStr", "CUInt", "CULng", "CUShort",
        "AddressOf", "NameOf"
    }

    Private Shared ReadOnly PreprocessorKeywords As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "#If", "#ElseIf", "#Else", "#End", "#Const", "#Region", "#End Region",
        "#ExternalSource", "#ExternalChecksum", "#Disable", "#Enable"
    }

    Private Shared ReadOnly LiteralKeywords As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "True", "False", "Nothing", "Me", "MyBase", "MyClass"
    }

    ' ═══════════ 行状态常量 ═══════════
    Private Const STATE_NORMAL As Integer = 0
    Private Const STATE_IN_XML_DOC As Integer = 0  ' XML文档注释不跨行
    Private Const STATE_IN_LINE_CONTINUATION As Integer = 1

    Public Function HighlightLine(lineIndex As Integer, lineText As String,
            previousLineState As Integer) As ModernTextBox.SyntaxHighlightResult _
            Implements ModernTextBox.ISyntaxHighlighter.HighlightLine

        Dim tokens As New List(Of ModernTextBox.SyntaxToken)
        If lineText.Length = 0 Then Return New ModernTextBox.SyntaxHighlightResult(tokens, STATE_NORMAL)

        Dim i As Integer = 0

        ' 跳过行首空白
        While i < lineText.Length AndAlso Char.IsWhiteSpace(lineText(i))
            i += 1
        End While

        ' 预处理指令: #Region, #If 等
        If i < lineText.Length AndAlso lineText(i) = "#"c Then
            tokens.Add(New ModernTextBox.SyntaxToken(i, lineText.Length - i, ColorPreprocessor))
            Return New ModernTextBox.SyntaxHighlightResult(tokens, STATE_NORMAL)
        End If

        ' XML 文档注释: '''
        If i + 2 < lineText.Length AndAlso lineText(i) = "'"c AndAlso lineText(i + 1) = "'"c AndAlso lineText(i + 2) = "'"c Then
            tokens.Add(New ModernTextBox.SyntaxToken(i, lineText.Length - i, ColorXmlDoc))
            Return New ModernTextBox.SyntaxHighlightResult(tokens, STATE_NORMAL)
        End If

        ' 逐字符扫描
        While i < lineText.Length
            Dim ch As Char = lineText(i)

            ' ── 注释: ' 或 REM ──
            If ch = "'"c Then
                tokens.Add(New ModernTextBox.SyntaxToken(i, lineText.Length - i, ColorComment))
                Exit While
            End If
            If i + 2 < lineText.Length AndAlso
               Char.ToUpperInvariant(lineText(i)) = "R"c AndAlso
               Char.ToUpperInvariant(lineText(i + 1)) = "E"c AndAlso
               Char.ToUpperInvariant(lineText(i + 2)) = "M"c AndAlso
               (i + 3 >= lineText.Length OrElse Not Char.IsLetterOrDigit(lineText(i + 3))) AndAlso
               (i = 0 OrElse Not Char.IsLetterOrDigit(lineText(i - 1))) Then
                tokens.Add(New ModernTextBox.SyntaxToken(i, lineText.Length - i, ColorComment))
                Exit While
            End If

            ' ── 字符串: "..." ──
            If ch = """"c Then
                Dim start = i
                i += 1
                While i < lineText.Length
                    If lineText(i) = """"c Then
                        i += 1
                        ' VB 转义: "" → 继续
                        If i < lineText.Length AndAlso lineText(i) = """"c Then
                            i += 1
                            Continue While
                        End If
                        Exit While
                    End If
                    i += 1
                End While
                tokens.Add(New ModernTextBox.SyntaxToken(start, i - start, ColorString))
                Continue While
            End If

            ' ── 数字 ──
            If Char.IsDigit(ch) OrElse
               (ch = "."c AndAlso i + 1 < lineText.Length AndAlso Char.IsDigit(lineText(i + 1))) OrElse
               (ch = "&"c AndAlso i + 1 < lineText.Length AndAlso "HhOoBb".Contains(lineText(i + 1))) Then
                Dim start = i
                If ch = "&"c Then
                    i += 2  ' 跳过 &H / &O / &B
                    While i < lineText.Length AndAlso IsHexDigit(lineText(i))
                        i += 1
                    End While
                Else
                    While i < lineText.Length AndAlso (Char.IsDigit(lineText(i)) OrElse lineText(i) = "."c)
                        i += 1
                    End While
                    ' 科学计数法 E/e
                    If i < lineText.Length AndAlso (lineText(i) = "E"c OrElse lineText(i) = "e"c) Then
                        i += 1
                        If i < lineText.Length AndAlso (lineText(i) = "+"c OrElse lineText(i) = "-"c) Then i += 1
                        While i < lineText.Length AndAlso Char.IsDigit(lineText(i))
                            i += 1
                        End While
                    End If
                End If
                ' 类型后缀: D, F, L, S, US, UI, UL, %, &, !, #, @
                If i < lineText.Length AndAlso "DdFfLlSsUu%&!#@".Contains(lineText(i)) Then
                    i += 1
                    If i < lineText.Length AndAlso (Char.ToUpperInvariant(lineText(i)) = "S"c OrElse
                                                    Char.ToUpperInvariant(lineText(i)) = "I"c OrElse
                                                    Char.ToUpperInvariant(lineText(i)) = "L"c) Then
                        i += 1
                    End If
                End If
                tokens.Add(New ModernTextBox.SyntaxToken(start, i - start, ColorNumber))
                Continue While
            End If

            ' ── 标识符 / 关键字 ──
            If Char.IsLetter(ch) OrElse ch = "_"c Then
                Dim start = i
                While i < lineText.Length AndAlso (Char.IsLetterOrDigit(lineText(i)) OrElse lineText(i) = "_"c)
                    i += 1
                End While
                Dim word = lineText.Substring(start, i - start)
                Dim clr As Color = ResolveWordColor(word)
                If clr <> Color.Empty Then
                    tokens.Add(New ModernTextBox.SyntaxToken(start, word.Length, clr))
                End If
                Continue While
            End If

            ' ── 方括号标识符 [identifier] ──
            If ch = "["c Then
                i += 1
                While i < lineText.Length AndAlso lineText(i) <> "]"c
                    i += 1
                End While
                If i < lineText.Length Then i += 1
                ' 方括号标识符不着色，跳过
                Continue While
            End If

            ' ── 日期字面量 #...# ──
            If ch = "#"c Then
                Dim start = i
                i += 1
                While i < lineText.Length AndAlso lineText(i) <> "#"c
                    i += 1
                End While
                If i < lineText.Length Then i += 1
                tokens.Add(New ModernTextBox.SyntaxToken(start, i - start, ColorString))
                Continue While
            End If

            i += 1
        End While

        ' 行尾续行符检测
        Dim endState As Integer = STATE_NORMAL
        Dim trimmed = lineText.TrimEnd()
        If trimmed.Length > 0 AndAlso trimmed(trimmed.Length - 1) = "_"c Then
            endState = STATE_IN_LINE_CONTINUATION
        End If

        Return New ModernTextBox.SyntaxHighlightResult(tokens, endState)
    End Function

    Private Shared Function ResolveWordColor(word As String) As Color
        If LiteralKeywords.Contains(word) Then Return ColorKeyword
        If TypeNames.Contains(word) Then Return ColorTypeName
        If OperatorWords.Contains(word) Then Return ColorOperatorWord
        If ControlFlowKeywords.Contains(word) Then Return ColorControlFlow
        If Keywords.Contains(word) Then Return ColorKeyword
        Return Color.Empty
    End Function

    Private Shared Function IsHexDigit(c As Char) As Boolean
        Return Char.IsDigit(c) OrElse
               (c >= "A"c AndAlso c <= "F"c) OrElse
               (c >= "a"c AndAlso c <= "f"c)
    End Function
End Class