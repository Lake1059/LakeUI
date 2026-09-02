Imports System.Drawing
Imports System.Text.RegularExpressions

''' <summary>代码块单行语法着色标记。</summary>
Public Structure CodeSyntaxToken
    Public StartCol As Integer
    Public Length As Integer
    Public ForeColor As Color
    Public Sub New(startCol As Integer, length As Integer, foreColor As Color)
        Me.StartCol = startCol
        Me.Length = length
        Me.ForeColor = foreColor
    End Sub
End Structure

''' <summary>代码块逐行语法着色结果。EndState 支持跨行注释。</summary>
Public Structure CodeSyntaxHighlightResult
    Public Tokens As List(Of CodeSyntaxToken)
    Public EndState As Integer
    Public Sub New(tokens As List(Of CodeSyntaxToken), endState As Integer)
        Me.Tokens = tokens
        Me.EndState = endState
    End Sub
End Structure

''' <summary>自定义代码块高亮器接口，遵循 ModernTextBox 的逐行状态模型。</summary>
Public Interface ICodeSyntaxHighlighter
    Function HighlightLine(lineIndex As Integer, lineText As String, previousLineState As Integer) As CodeSyntaxHighlightResult
End Interface

''' <summary>语法缩进分析结果。IndentLevel 是当前行，NextIndentLevel 用于下一行。</summary>
Public Structure CodeIndentationResult
    Public IndentLevel As Integer
    Public NextIndentLevel As Integer
    Public Text As String
    Public Sub New(indentLevel As Integer, nextIndentLevel As Integer, text As String)
        Me.IndentLevel = indentLevel
        Me.NextIndentLevel = nextIndentLevel
        Me.Text = text
    End Sub
End Structure

''' <summary>代码块语法缩进分析器。只在启用语法高亮时调用。</summary>
Public NotInheritable Class CodeIndentationAnalyzer
    Private Sub New()
    End Sub

    Public Shared Function Analyze(language As String, lineText As String, previousIndentLevel As Integer) As CodeIndentationResult
        Dim text = If(lineText, "").TrimStart(" "c, ChrW(9))
        If text.Length = 0 Then Return New CodeIndentationResult(0, previousIndentLevel, text)
        Dim key = CodeSyntaxHighlighterRegistry.NormalizeLanguage(language)
        Dim level = Math.Max(0, previousIndentLevel)
        Dim nextLevel = level
        If key = "python" OrElse key = "py" OrElse key = "py3" Then
            If IsPythonDedent(text) Then level = Math.Max(0, level - 1)
            If text.EndsWith(":"c) AndAlso Not text.StartsWith("#"c) Then nextLevel = level + 1
        ElseIf key = "vb" OrElse key = "vbnet" OrElse key = "vb.net" OrElse key = "visualbasic.net" OrElse key = "vb6" OrElse key = "visualbasic6" Then
            If IsVisualBasicDedent(text) Then
                level = Math.Max(0, level - 1)
                nextLevel = level
            End If
            If IsVisualBasicContinuation(text) Then nextLevel = level + 1
            If IsVisualBasicMidBlock(text) Then nextLevel = level + 1
        ElseIf key = "asm" OrElse key = "assembly" OrElse key = "x86asm" OrElse key = "masm" OrElse key = "nasm" Then
            If IsAssemblyLabel(text) Then
                level = 0
                nextLevel = 1
            ElseIf IsAssemblyDirective(text) Then
                level = 0
                nextLevel = 0
            End If
        ElseIf key = "xml" OrElse key = "xsd" OrElse key = "xsl" OrElse key = "xslt" OrElse key = "html" OrElse key = "htm" OrElse key = "xhtml" OrElse key = "svg" Then
            Return AnalyzeMarkupIndentation(text, level, key = "html" OrElse key = "htm")
        Else
            If StartsWithClosingBrace(text) Then level = Math.Max(0, level - 1)
            Dim clean = StripCStyleStringsAndComments(text)
            nextLevel = Math.Max(0, level + CountChar(clean, "{"c) - CountChar(clean, "}"c))
        End If
        Return New CodeIndentationResult(level, Math.Max(0, nextLevel), text)
    End Function

    Private Shared Function IsPythonDedent(text As String) As Boolean
        Return Regex.IsMatch(text, "^(elif|else|except|finally|case)\b", RegexOptions.IgnoreCase)
    End Function

    Private Shared Function IsVisualBasicDedent(text As String) As Boolean
        Dim normalized = StripVisualBasicModifiers(text)
        Return Regex.IsMatch(normalized, "^(end\b|else\b|elseif\b|case\b|catch\b|finally\b|loop\b|next\b|wend\b)", RegexOptions.IgnoreCase)
    End Function

    Private Shared Function IsVisualBasicContinuation(text As String) As Boolean
        Dim normalized = StripVisualBasicModifiers(text)
        Return Regex.IsMatch(normalized, "^(else|elseif|case|catch|finally)\b", RegexOptions.IgnoreCase)
    End Function

    Private Shared Function IsVisualBasicMidBlock(text As String) As Boolean
        Dim normalized = StripVisualBasicModifiers(text)
        If Regex.IsMatch(normalized, "^(end\b|else\b|elseif\b|case\b|catch\b|finally\b|loop\b|next\b|wend\b)", RegexOptions.IgnoreCase) Then Return False
        If Regex.IsMatch(normalized, "^(class|module|namespace|sub|function|property|structure|enum|interface|for|foreach|while|do|select|try|with|using)\b", RegexOptions.IgnoreCase) Then Return True
        Return Regex.IsMatch(normalized, "^if\b.*\bthen\s*$", RegexOptions.IgnoreCase)
    End Function

    Private Shared Function StripVisualBasicModifiers(text As String) As String
        Return Regex.Replace(If(text, ""), "^(?:(?:public|private|protected|friend|shared|partial|default|overloads|overridable|overrides|mustinherit|notoverridable|notinheritable|shadows|static|async|iterator)\s+)+", "", RegexOptions.IgnoreCase)
    End Function

    Private Shared Function IsAssemblyLabel(text As String) As Boolean
        Return Regex.IsMatch(text, "^[A-Za-z_.$?][A-Za-z0-9_.$?]*:")
    End Function

    Private Shared Function IsAssemblyDirective(text As String) As Boolean
        Return Regex.IsMatch(text, "^(section|segment|global|extern|bits|org|align|db|dw|dd|dq)\b", RegexOptions.IgnoreCase)
    End Function

    Private Shared Function AnalyzeMarkupIndentation(text As String, previousIndentLevel As Integer, html As Boolean) As CodeIndentationResult
        If text.StartsWith("</", StringComparison.Ordinal) Then previousIndentLevel = Math.Max(0, previousIndentLevel - 1)
        Dim openings = 0
        Dim closings = 0
        For Each match As Match In Regex.Matches(text, "<\s*(?<closing>/)?\s*(?<name>[\p{L}_:][\p{L}\p{Nd}_:.-]*)(?=\s|/?>)[^>]*?(?<self>/)?\s*>")
            If match.Groups("closing").Success Then
                closings += 1
            ElseIf Not match.Groups("self").Success AndAlso Not (html AndAlso IsHtmlVoidElement(match.Groups("name").Value)) Then
                openings += 1
            End If
        Next
        Dim nextLevel = Math.Max(0, previousIndentLevel + openings - Math.Max(0, closings - If(text.StartsWith("</", StringComparison.Ordinal), 1, 0)))
        Return New CodeIndentationResult(previousIndentLevel, nextLevel, text)
    End Function

    Private Shared Function IsHtmlVoidElement(name As String) As Boolean
        Return Regex.IsMatch(name, "^(area|base|br|col|embed|hr|img|input|link|meta|source|track|wbr)$", RegexOptions.IgnoreCase)
    End Function

    Private Shared Function StartsWithClosingBrace(text As String) As Boolean
        Return text.StartsWith("}"c) OrElse text.StartsWith(")"c) OrElse text.StartsWith("]"c)
    End Function

    Private Shared Function CountChar(text As String, value As Char) As Integer
        Dim count = 0
        For Each ch In text
            If ch = value Then count += 1
        Next
        Return count
    End Function

    Private Shared Function StripCStyleStringsAndComments(text As String) As String
        Dim result As New System.Text.StringBuilder()
        Dim inString As Boolean = False
        Dim quote As Char = ChrW(0)
        Dim i As Integer = 0
        While i < text.Length
            If Not inString AndAlso i + 1 < text.Length AndAlso text(i) = "/"c AndAlso text(i + 1) = "/"c Then Exit While
            Dim ch = text(i)
            If ch = """"c OrElse ch = "'"c Then
                If inString Then
                    If ch = quote Then inString = False
                Else
                    quote = ch
                    inString = True
                End If
                result.Append(" "c)
            ElseIf inString Then
                result.Append(" "c)
            Else
                result.Append(ch)
            End If
            i += 1
        End While
        Return result.ToString()
    End Function
End Class

''' <summary>内置代码块高亮器注册表。Register 会覆盖现有语言映射。</summary>
Public NotInheritable Class CodeSyntaxHighlighterRegistry
    Private Shared ReadOnly _highlighters As New Dictionary(Of String, ICodeSyntaxHighlighter)(StringComparer.OrdinalIgnoreCase)

    Shared Sub New()
        Register(New CFamilyHighlighter("csharp"), "csharp", "cs", "c#")
        Register(New CFamilyHighlighter("cpp"), "cpp", "c++", "cxx", "cc", "hpp", "hxx")
        Register(New CFamilyHighlighter("c"), "c", "h")
        Register(New VisualBasicHighlighter(False), "vb", "vbnet", "vb.net", "visualbasic.net")
        Register(New VisualBasicHighlighter(True), "vb6", "visualbasic6")
        Register(New PythonHighlighter(), "python", "py", "py3")
        Register(New JavaHighlighter(), "java", "jav")
        Register(New MarkupHighlighter(), "xml", "xsd", "xsl", "xslt", "html", "htm", "xhtml", "svg")
        Register(New JsonHighlighter(), "json")
        Register(New AssemblyHighlighter(), "asm", "assembly", "x86asm", "masm", "nasm")
    End Sub

    Public Shared Sub Register(highlighter As ICodeSyntaxHighlighter, ParamArray languages As String())
        If highlighter Is Nothing OrElse languages Is Nothing Then Return
        SyncLock _highlighters
            For Each language In languages
                Dim key = NormalizeLanguage(language)
                If key.Length > 0 Then _highlighters(key) = highlighter
            Next
        End SyncLock
    End Sub

    Public Shared Function Unregister(language As String) As Boolean
        Dim key = NormalizeLanguage(language)
        If key.Length = 0 Then Return False
        SyncLock _highlighters
            Return _highlighters.Remove(key)
        End SyncLock
    End Function

    Public Shared Function GetHighlighter(language As String) As ICodeSyntaxHighlighter
        Dim result As ICodeSyntaxHighlighter = Nothing
        SyncLock _highlighters
            _highlighters.TryGetValue(NormalizeLanguage(language), result)
        End SyncLock
        Return result
    End Function

    Public Shared Function NormalizeLanguage(language As String) As String
        If String.IsNullOrWhiteSpace(language) Then Return ""
        Return language.Trim().ToLowerInvariant()
    End Function

    Private MustInherit Class BasicHighlighter
        Implements ICodeSyntaxHighlighter
        Protected Shared ReadOnly KeywordColor As Color = Color.FromArgb(86, 156, 214)
        Protected Shared ReadOnly ControlColor As Color = Color.FromArgb(216, 160, 223)
        Protected Shared ReadOnly TypeColor As Color = Color.FromArgb(78, 201, 176)
        Protected Shared ReadOnly StringColor As Color = Color.FromArgb(214, 157, 133)
        Protected Shared ReadOnly CommentColor As Color = Color.FromArgb(87, 166, 74)
        Protected Shared ReadOnly NumberColor As Color = Color.FromArgb(181, 206, 168)
        Protected Shared ReadOnly DirectiveColor As Color = Color.FromArgb(155, 155, 155)

        Public MustOverride Function HighlightLine(lineIndex As Integer,
                                                   lineText As String,
                                                   previousLineState As Integer) As CodeSyntaxHighlightResult _
                                                   Implements ICodeSyntaxHighlighter.HighlightLine

        Protected Shared Function Scan(lineText As String, previousLineState As Integer, keywords As HashSet(Of String), controls As HashSet(Of String), types As HashSet(Of String), lineComment As String, Optional blockStart As String = Nothing, Optional blockEnd As String = Nothing, Optional apostropheString As Boolean = True) As CodeSyntaxHighlightResult
            Dim tokens As New List(Of CodeSyntaxToken)
            Dim i As Integer = 0
            Dim inBlock = previousLineState = 1
            While i < lineText.Length
                If inBlock Then
                    Dim ending = lineText.IndexOf(blockEnd, i, StringComparison.Ordinal)
                    If ending < 0 Then
                        Add(tokens, i, lineText.Length - i, CommentColor)
                        Return New CodeSyntaxHighlightResult(tokens, 1)
                    End If
                    Add(tokens, i, ending + blockEnd.Length - i, CommentColor)
                    i = ending + blockEnd.Length
                    inBlock = False
                    Continue While
                End If
                If Not String.IsNullOrEmpty(blockStart) AndAlso lineText.IndexOf(blockStart, i, StringComparison.Ordinal) = i Then
                    Dim ending = lineText.IndexOf(blockEnd, i + blockStart.Length, StringComparison.Ordinal)
                    If ending < 0 Then
                        Add(tokens, i, lineText.Length - i, CommentColor)
                        Return New CodeSyntaxHighlightResult(tokens, 1)
                    End If
                    Add(tokens, i, ending + blockEnd.Length - i, CommentColor)
                    i = ending + blockEnd.Length
                    Continue While
                End If
                If Not String.IsNullOrEmpty(lineComment) AndAlso lineText.IndexOf(lineComment, i, StringComparison.Ordinal) = i Then
                    Add(tokens, i, lineText.Length - i, CommentColor)
                    Exit While
                End If
                Dim ch = lineText(i)
                If ch = """"c OrElse (apostropheString AndAlso ch = "'"c) Then
                    Dim start = i
                    Dim quote = ch
                    i += 1
                    While i < lineText.Length
                        If lineText(i) = "\"c AndAlso i + 1 < lineText.Length Then
                            i += 2
                        ElseIf lineText(i) = quote Then
                            i += 1
                            Exit While
                        Else
                            i += 1
                        End If
                    End While
                    Add(tokens, start, i - start, StringColor)
                    Continue While
                End If
                If Char.IsDigit(ch) AndAlso (i = 0 OrElse Not Char.IsLetterOrDigit(lineText(i - 1))) Then
                    Dim start = i
                    i += 1
                    While i < lineText.Length AndAlso (Char.IsLetterOrDigit(lineText(i)) OrElse "._xX+-".Contains(lineText(i)))
                        i += 1
                    End While
                    Add(tokens, start, i - start, NumberColor)
                    Continue While
                End If
                If Char.IsLetter(ch) OrElse ch = "_"c Then
                    Dim start = i
                    i += 1
                    While i < lineText.Length AndAlso (Char.IsLetterOrDigit(lineText(i)) OrElse lineText(i) = "_"c)
                        i += 1
                    End While
                    Dim word = lineText.Substring(start, i - start)
                    If controls.Contains(word) Then
                        Add(tokens, start, word.Length, ControlColor)
                    ElseIf types.Contains(word) Then
                        Add(tokens, start, word.Length, TypeColor)
                    ElseIf keywords.Contains(word) Then
                        Add(tokens, start, word.Length, KeywordColor)
                    End If
                    Continue While
                End If
                i += 1
            End While
            Return New CodeSyntaxHighlightResult(tokens, 0)
        End Function

        Protected Shared Sub Add(tokens As List(Of CodeSyntaxToken), startCol As Integer, length As Integer, color As Color)
            If length > 0 Then tokens.Add(New CodeSyntaxToken(startCol, length, color))
        End Sub
    End Class

    Private NotInheritable Class CFamilyHighlighter
        Inherits BasicHighlighter
        Private ReadOnly _language As String
        Private Shared ReadOnly Controls As New HashSet(Of String)(StringComparer.Ordinal) From {"if", "else", "switch", "case", "for", "while", "do", "break", "continue", "try", "catch", "throw", "return"}
        Private Shared ReadOnly Types As New HashSet(Of String)(StringComparer.Ordinal) From {"void", "bool", "char", "short", "int", "long", "float", "double", "decimal", "string", "object", "size_t", "wchar_t", "char8_t", "char16_t", "char32_t", "nullptr_t"}
        Private Shared ReadOnly CSharpKeywords As New HashSet(Of String)(StringComparer.Ordinal) From {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
            "add", "allows", "alias", "and", "args", "ascending", "async", "await", "by", "closed", "descending", "dynamic", "equals", "extension", "field", "file", "from", "get", "global", "group", "init", "into", "join", "let", "managed", "nameof", "nint", "not", "notnull", "nuint", "on", "or", "orderby", "partial", "record", "remove", "required", "safe", "scoped", "select", "set", "unmanaged", "value", "var", "when", "where", "with", "yield"
        }
        Private Shared ReadOnly CppKeywords As New HashSet(Of String)(StringComparer.Ordinal) From {
            "alignas", "alignof", "and", "and_eq", "asm", "auto", "bitand", "bitor", "bool", "break", "case", "catch", "char", "char8_t", "char16_t", "char32_t", "class", "compl", "concept", "const", "const_cast", "consteval", "constexpr", "constinit", "continue", "co_await", "co_return", "co_yield", "decltype", "default", "delete", "do", "double", "dynamic_cast", "else", "enum", "explicit", "export", "extern", "false", "final", "float", "for", "friend", "goto", "if", "import", "inline", "int", "long", "module", "mutable", "namespace", "new", "noexcept", "not", "not_eq", "nullptr", "operator", "or", "or_eq", "override", "private", "protected", "public", "register", "reinterpret_cast", "requires", "return", "short", "signed", "sizeof", "static", "static_assert", "static_cast", "struct", "switch", "template", "this", "thread_local", "throw", "true", "try", "typedef", "typeid", "typename", "union", "unsigned", "using", "virtual", "void", "volatile", "wchar_t", "while", "xor", "xor_eq"
        }
        Private Shared ReadOnly CKeywords As New HashSet(Of String)(StringComparer.Ordinal) From {
            "auto", "break", "case", "char", "const", "continue", "default", "do", "double", "else", "enum", "extern", "float", "for", "goto", "if", "inline", "int", "long", "register", "restrict", "return", "short", "signed", "sizeof", "static", "struct", "switch", "typedef", "union", "unsigned", "void", "volatile", "while",
            "_Alignas", "_Alignof", "_Atomic", "_BitInt", "_Bool", "_Complex", "_Decimal32", "_Decimal64", "_Decimal128", "_Generic", "_Imaginary", "_Noreturn", "_Static_assert", "_Thread_local", "alignas", "alignof", "bool", "constexpr", "false", "nullptr", "static_assert", "thread_local", "true", "typeof", "typeof_unqual"
        }
        Public Sub New(language As String)
            _language = language
        End Sub
        Public Overrides Function HighlightLine(lineIndex As Integer, lineText As String, previousLineState As Integer) As CodeSyntaxHighlightResult
            Dim trimmed = lineText.TrimStart()
            If trimmed.StartsWith("#"c) Then Return New CodeSyntaxHighlightResult(New List(Of CodeSyntaxToken) From {New CodeSyntaxToken(lineText.Length - trimmed.Length, trimmed.Length, DirectiveColor)}, 0)
            Dim keywords = If(_language = "csharp", CSharpKeywords, If(_language = "cpp", CppKeywords, CKeywords))
            Return Scan(lineText, previousLineState, keywords, Controls, Types, "//", "/*", "*/")
        End Function
    End Class

    Private NotInheritable Class VisualBasicHighlighter
        Inherits BasicHighlighter
        Private ReadOnly _vb6 As Boolean
        Private Shared ReadOnly NetKeywords As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            "AddHandler", "AddressOf", "Alias", "And", "AndAlso", "As", "Async", "Await", "ByRef", "ByVal", "Call", "Class", "Const", "Custom", "Declare", "Default", "Delegate", "Dim", "DirectCast", "Each", "EndIf", "Erase", "Error", "Event", "False", "Friend", "Function", "Get", "GetType", "GetXMLNamespace", "Global", "GoSub", "GoTo", "Handles", "Implements", "Imports", "In", "Inherits", "Interface", "Is", "IsNot", "Iterator", "Lib", "Like", "Me", "Mod", "Module", "MustInherit", "MustOverride", "MyBase", "MyClass", "NameOf", "Namespace", "Narrowing", "New", "Not", "Nothing", "NotInheritable", "NotOverridable", "Of", "On", "Operator", "Option", "Optional", "Or", "OrElse", "Out", "Overloads", "Overridable", "Overrides", "ParamArray", "Partial", "Private", "Property", "Protected", "Public", "RaiseEvent", "ReadOnly", "ReDim", "REM", "RemoveHandler", "Resume", "Shadows", "Shared", "Static", "Step", "Stop", "SyncLock", "Then", "To", "True", "TryCast", "TypeOf", "Using", "When", "Widening", "With", "WithEvents", "WriteOnly", "Xor", "Yield"
        }
        Private Shared ReadOnly Vb6Keywords As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {"Option", "Explicit", "Private", "Public", "Dim", "Static", "Const", "Sub", "Function", "Property", "Set", "Let", "New", "Nothing", "True", "False", "ByVal", "ByRef"}
        Private Shared ReadOnly Controls As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {"If", "Then", "Else", "ElseIf", "Select", "Case", "For", "Each", "While", "Wend", "Do", "Loop", "Try", "Catch", "Finally", "Throw", "Exit", "Continue", "Next", "End", "Return"}
        Private Shared ReadOnly Types As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {"String", "Integer", "Long", "Boolean", "Double", "Decimal", "Object", "Date", "Byte", "SByte", "Short", "UShort", "UInteger", "ULong", "Single", "Char", "Variant", "Currency"}
        Public Sub New(vb6 As Boolean)
            _vb6 = vb6
        End Sub
        Public Overrides Function HighlightLine(lineIndex As Integer, lineText As String, previousLineState As Integer) As CodeSyntaxHighlightResult
            Dim trimmed = lineText.TrimStart()
            If Not _vb6 AndAlso trimmed.StartsWith("#"c) Then Return New CodeSyntaxHighlightResult(New List(Of CodeSyntaxToken) From {New CodeSyntaxToken(lineText.Length - trimmed.Length, trimmed.Length, DirectiveColor)}, 0)
            Return Scan(lineText, previousLineState, If(_vb6, Vb6Keywords, NetKeywords), Controls, Types, "'", apostropheString:=False)
        End Function
    End Class

    Private NotInheritable Class PythonHighlighter
        Inherits BasicHighlighter
        Private Shared ReadOnly Keywords As New HashSet(Of String)(StringComparer.Ordinal) From {"and", "as", "assert", "async", "await", "class", "def", "del", "from", "global", "import", "in", "is", "lambda", "nonlocal", "not", "or", "pass", "raise", "return", "True", "False", "None", "with", "yield", "type"}
        Private Shared ReadOnly Controls As New HashSet(Of String)(StringComparer.Ordinal) From {"if", "elif", "else", "for", "while", "try", "except", "finally", "break", "continue", "match", "case"}
        Private Shared ReadOnly Types As New HashSet(Of String)(StringComparer.Ordinal) From {"str", "int", "float", "bool", "list", "dict", "set", "tuple", "bytes"}
        Public Overrides Function HighlightLine(lineIndex As Integer, lineText As String, previousLineState As Integer) As CodeSyntaxHighlightResult
            Return Scan(lineText, previousLineState, Keywords, Controls, Types, "#")
        End Function
    End Class

    ''' <summary>Java SE 25 词法高亮器。状态 1 为块注释，状态 2 为文本块。</summary>
    Private NotInheritable Class JavaHighlighter
        Inherits BasicHighlighter

        ' 关键字集合依据 JLS 3.9（Java SE 25）定义。
        Private Shared ReadOnly Keywords As New HashSet(Of String)(StringComparer.Ordinal) From {
            "abstract", "assert", "boolean", "break", "byte", "case", "catch", "char", "class", "const",
            "continue", "default", "do", "double", "else", "enum", "extends", "final", "finally", "float",
            "for", "goto", "if", "implements", "import", "instanceof", "int", "interface", "long", "native",
            "new", "package", "private", "protected", "public", "return", "short", "static", "strictfp", "super",
            "switch", "synchronized", "this", "throw", "throws", "transient", "try", "void", "volatile", "while",
            "_", "exports", "opens", "requires", "uses", "yield", "module", "permits", "sealed", "var",
            "non-sealed", "provides", "to", "when", "open", "record", "transitive", "with"
        }

        Private Shared ReadOnly Controls As New HashSet(Of String)(StringComparer.Ordinal) From {
            "assert", "break", "case", "catch", "continue", "default", "do", "else", "finally", "for", "if",
            "return", "switch", "throw", "try", "while", "yield"
        }

        Private Shared ReadOnly Types As New HashSet(Of String)(StringComparer.Ordinal) From {
            "boolean", "byte", "char", "double", "float", "int", "long", "short", "void",
            "String", "Object", "Class", "Void", "Integer", "Long", "Short", "Byte", "Boolean",
            "Character", "Double", "Float", "Number", "Exception", "RuntimeException", "Throwable",
            "System", "Math", "BigDecimal", "BigInteger", "List", "ArrayList", "Map", "HashMap",
            "Set", "HashSet", "Collection", "Iterable", "Iterator", "Stream", "Optional"
        }

        Private Shared ReadOnly NumberPattern As New Regex(
            "(?:0[xX](?:[0-9a-fA-F](?:_?[0-9a-fA-F])*)(?:\.(?:[0-9a-fA-F](?:_?[0-9a-fA-F])*)?)?(?:[pP][+-]?[0-9](?:_?[0-9])*)?[fFdDlL]?|0[bB][01](?:_?[01])*[lL]?|(?:[0-9](?:_?[0-9])*)(?:\.(?:[0-9](?:_?[0-9])*)?)?(?:[eE][+-]?[0-9](?:_?[0-9])*)?[fFdD]?|\.(?:[0-9](?:_?[0-9])*)(?:[eE][+-]?[0-9](?:_?[0-9])*)?[fFdD]?)(?=$|[^A-Za-z0-9_$])",
            RegexOptions.Compiled)
        Private Shared ReadOnly TextBlockDelimiter As New String(ChrW(34), 3)

        Public Overrides Function HighlightLine(lineIndex As Integer, lineText As String, previousLineState As Integer) As CodeSyntaxHighlightResult
            Dim text = If(lineText, "")
            Dim tokens As New List(Of CodeSyntaxToken)
            Dim i = 0
            Dim state = If(previousLineState = 2, 2, If(previousLineState = 1, 1, 0))

            While i < text.Length
                If state = 1 Then
                    Dim ending = text.IndexOf("*/", i, StringComparison.Ordinal)
                    If ending < 0 Then
                        Add(tokens, i, text.Length - i, CommentColor)
                        Return New CodeSyntaxHighlightResult(tokens, 1)
                    End If
                    Add(tokens, i, ending + 2 - i, CommentColor)
                    i = ending + 2
                    state = 0
                    Continue While
                End If

                If state = 2 Then
                    Dim ending = text.IndexOf(TextBlockDelimiter, i, StringComparison.Ordinal)
                    If ending < 0 Then
                        Add(tokens, i, text.Length - i, StringColor)
                        Return New CodeSyntaxHighlightResult(tokens, 2)
                    End If
                    Add(tokens, i, ending + 3 - i, StringColor)
                    i = ending + 3
                    state = 0
                    Continue While
                End If

                If i + 1 < text.Length AndAlso text(i) = "/"c AndAlso text(i + 1) = "/"c Then
                    Add(tokens, i, text.Length - i, CommentColor)
                    Exit While
                End If
                If i + 1 < text.Length AndAlso text(i) = "/"c AndAlso text(i + 1) = "*"c Then
                    Dim ending = text.IndexOf("*/", i + 2, StringComparison.Ordinal)
                    If ending < 0 Then
                        Add(tokens, i, text.Length - i, CommentColor)
                        Return New CodeSyntaxHighlightResult(tokens, 1)
                    End If
                    Add(tokens, i, ending + 2 - i, CommentColor)
                    i = ending + 2
                    Continue While
                End If

                If i + 2 < text.Length AndAlso text(i) = ChrW(34) AndAlso text(i + 1) = ChrW(34) AndAlso text(i + 2) = ChrW(34) Then
                    Dim ending = text.IndexOf(TextBlockDelimiter, i + 3, StringComparison.Ordinal)
                    If ending < 0 Then
                        Add(tokens, i, text.Length - i, StringColor)
                        Return New CodeSyntaxHighlightResult(tokens, 2)
                    End If
                    Add(tokens, i, ending + 3 - i, StringColor)
                    i = ending + 3
                    Continue While
                End If

                If i + 9 <= text.Length AndAlso text.Substring(i, 9) = "non-sealed" AndAlso
                   (i = 0 OrElse Not IsJavaIdentifierPart(text(i - 1))) AndAlso
                   (i + 9 = text.Length OrElse Not IsJavaIdentifierPart(text(i + 9))) Then
                    Add(tokens, i, 9, KeywordColor)
                    i += 9
                    Continue While
                End If

                If text(i) = """"c OrElse text(i) = "'"c Then
                    Dim start = i
                    Dim quote = text(i)
                    i += 1
                    While i < text.Length
                        If text(i) = "\"c AndAlso i + 1 < text.Length Then
                            i += 2
                        ElseIf text(i) = quote Then
                            i += 1
                            Exit While
                        Else
                            i += 1
                        End If
                    End While
                    Add(tokens, start, i - start, StringColor)
                    Continue While
                End If

                If (Char.IsDigit(text(i)) OrElse (text(i) = "."c AndAlso i + 1 < text.Length AndAlso Char.IsDigit(text(i + 1)))) AndAlso
                   (i = 0 OrElse Not IsJavaIdentifierPart(text(i - 1))) Then
                    Dim number = NumberPattern.Match(text, i)
                    If number.Success AndAlso number.Index = i Then
                        Add(tokens, i, number.Length, NumberColor)
                        i += number.Length
                        Continue While
                    End If
                End If

                If IsJavaIdentifierStart(text(i)) Then
                    Dim start = i
                    i += 1
                    While i < text.Length AndAlso IsJavaIdentifierPart(text(i))
                        i += 1
                    End While
                    Dim word = text.Substring(start, i - start)
                    If Controls.Contains(word) Then
                        Add(tokens, start, word.Length, ControlColor)
                    ElseIf Types.Contains(word) Then
                        Add(tokens, start, word.Length, TypeColor)
                    ElseIf Keywords.Contains(word) OrElse word = "true" OrElse word = "false" OrElse word = "null" Then
                        Add(tokens, start, word.Length, KeywordColor)
                    End If
                    Continue While
                End If

                i += 1
            End While
            Return New CodeSyntaxHighlightResult(tokens, state)
        End Function

        Private Shared Function IsJavaIdentifierStart(value As Char) As Boolean
            Return Char.IsLetter(value) OrElse value = "_"c OrElse value = "$"c
        End Function

        Private Shared Function IsJavaIdentifierPart(value As Char) As Boolean
            Return Char.IsLetterOrDigit(value) OrElse value = "_"c OrElse value = "$"c
        End Function
    End Class

    ''' <summary>XML/HTML 基本标记高亮器。状态 1=注释，2=标签，3/4=标签属性引号，5=声明，6=CDATA，7=无引号属性值。</summary>
    Private NotInheritable Class MarkupHighlighter
        Inherits BasicHighlighter

        Public Overrides Function HighlightLine(lineIndex As Integer, lineText As String, previousLineState As Integer) As CodeSyntaxHighlightResult
            Dim text = If(lineText, "")
            Dim tokens As New List(Of CodeSyntaxToken)
            Dim i = 0
            Dim state = Math.Max(0, Math.Min(7, previousLineState))

            While i < text.Length
                If state = 1 Then
                    Dim ending = text.IndexOf("-->", i, StringComparison.Ordinal)
                    If ending < 0 Then
                        Add(tokens, i, text.Length - i, CommentColor)
                        Return New CodeSyntaxHighlightResult(tokens, 1)
                    End If
                    Add(tokens, i, ending + 3 - i, CommentColor)
                    i = ending + 3
                    state = 0
                    Continue While
                End If
                If state = 6 Then
                    Dim ending = text.IndexOf("]]>", i, StringComparison.Ordinal)
                    If ending < 0 Then
                        Add(tokens, i, text.Length - i, CommentColor)
                        Return New CodeSyntaxHighlightResult(tokens, 6)
                    End If
                    Add(tokens, i, ending + 3 - i, CommentColor)
                    i = ending + 3
                    state = 0
                    Continue While
                End If
                If state = 5 Then
                    Dim ending = text.IndexOf(">"c, i)
                    If ending < 0 Then
                        Add(tokens, i, text.Length - i, DirectiveColor)
                        Return New CodeSyntaxHighlightResult(tokens, 5)
                    End If
                    Add(tokens, i, ending + 1 - i, DirectiveColor)
                    i = ending + 1
                    state = 0
                    Continue While
                End If
                If state = 3 OrElse state = 4 Then
                    Dim quote = If(state = 3, """"c, "'"c)
                    Dim ending = text.IndexOf(quote, i)
                    If ending < 0 Then
                        Add(tokens, i, text.Length - i, StringColor)
                        Return New CodeSyntaxHighlightResult(tokens, state)
                    End If
                    Add(tokens, i, ending + 1 - i, StringColor)
                    i = ending + 1
                    state = 2
                    Continue While
                End If
                If state = 7 Then
                    While i < text.Length AndAlso Char.IsWhiteSpace(text(i))
                        i += 1
                    End While
                    If i >= text.Length Then Return New CodeSyntaxHighlightResult(tokens, 7)
                    If text(i) = """"c OrElse text(i) = "'"c Then
                        Add(tokens, i, 1, StringColor)
                        state = If(text(i) = """"c, 3, 4)
                        i += 1
                        Continue While
                    End If
                    Dim start = i
                    While i < text.Length AndAlso Not Char.IsWhiteSpace(text(i)) AndAlso text(i) <> ">"c
                        i += 1
                    End While
                    Add(tokens, start, i - start, StringColor)
                    state = 2
                    Continue While
                End If
                If state = 2 Then
                    If Char.IsWhiteSpace(text(i)) Then
                        i += 1
                        Continue While
                    End If
                    If text(i) = ">"c Then
                        Add(tokens, i, 1, KeywordColor)
                        i += 1
                        state = 0
                        Continue While
                    End If
                    If i + 1 < text.Length AndAlso text(i) = "/"c AndAlso text(i + 1) = ">"c Then
                        Add(tokens, i, 2, KeywordColor)
                        i += 2
                        state = 0
                        Continue While
                    End If
                    If text(i) = "="c Then
                        Add(tokens, i, 1, KeywordColor)
                        i += 1
                        state = 7
                        Continue While
                    End If
                    If text(i) = """"c Then
                        Add(tokens, i, 1, StringColor)
                        i += 1
                        state = 3
                        Continue While
                    End If
                    If text(i) = "'"c Then
                        Add(tokens, i, 1, StringColor)
                        i += 1
                        state = 4
                        Continue While
                    End If
                    If IsMarkupNameChar(text(i)) Then
                        Dim start = i
                        i += 1
                        While i < text.Length AndAlso IsMarkupNameChar(text(i))
                            i += 1
                        End While
                        Add(tokens, start, i - start, KeywordColor)
                    Else
                        i += 1
                    End If
                    Continue While
                End If

                If text(i) <> "<"c Then
                    If text(i) = "&"c Then
                        Dim ending = text.IndexOf(";"c, i + 1)
                        If ending >= 0 Then
                            Add(tokens, i, ending + 1 - i, NumberColor)
                            i = ending + 1
                            Continue While
                        End If
                    End If
                    i += 1
                    Continue While
                End If

                If i + 1 >= text.Length OrElse Not (IsMarkupNameStart(text(i + 1)) OrElse text(i + 1) = "/"c OrElse text(i + 1) = "!"c OrElse text(i + 1) = "?"c) Then
                    i += 1
                    Continue While
                End If

                If text.IndexOf("<!--", i, StringComparison.Ordinal) = i Then
                    Dim ending = text.IndexOf("-->", i + 4, StringComparison.Ordinal)
                    If ending < 0 Then
                        Add(tokens, i, text.Length - i, CommentColor)
                        Return New CodeSyntaxHighlightResult(tokens, 1)
                    End If
                    Add(tokens, i, ending + 3 - i, CommentColor)
                    i = ending + 3
                    Continue While
                End If
                If text.IndexOf("<![CDATA[", i, StringComparison.Ordinal) = i Then
                    Dim ending = text.IndexOf("]]>", i + 9, StringComparison.Ordinal)
                    If ending < 0 Then
                        Add(tokens, i, text.Length - i, CommentColor)
                        Return New CodeSyntaxHighlightResult(tokens, 6)
                    End If
                    Add(tokens, i, ending + 3 - i, CommentColor)
                    i = ending + 3
                    Continue While
                End If
                If i + 1 < text.Length AndAlso (text(i + 1) = "!"c OrElse text(i + 1) = "?"c) Then
                    Dim ending = text.IndexOf(">"c, i + 2)
                    If ending < 0 Then
                        Add(tokens, i, text.Length - i, DirectiveColor)
                        Return New CodeSyntaxHighlightResult(tokens, 5)
                    End If
                    Add(tokens, i, ending + 1 - i, DirectiveColor)
                    i = ending + 1
                    Continue While
                End If

                Dim openerLength = If(i + 1 < text.Length AndAlso text(i + 1) = "/"c, 2, 1)
                Add(tokens, i, openerLength, KeywordColor)
                i += openerLength
                Dim nameStart = i
                While i < text.Length AndAlso IsMarkupNameChar(text(i))
                    i += 1
                End While
                If i > nameStart Then Add(tokens, nameStart, i - nameStart, TypeColor)
                state = 2
            End While
            Return New CodeSyntaxHighlightResult(tokens, state)
        End Function

        Private Shared Function IsMarkupNameStart(value As Char) As Boolean
            Return Char.IsLetter(value) OrElse value = "_"c OrElse value = ":"c
        End Function

        Private Shared Function IsMarkupNameChar(value As Char) As Boolean
            Return IsMarkupNameStart(value) OrElse Char.IsDigit(value) OrElse value = "-"c OrElse value = "."c
        End Function
    End Class

    Private NotInheritable Class JsonHighlighter
        Implements ICodeSyntaxHighlighter
        Private Shared ReadOnly Pattern As New Regex("(?<key>""(?:\\.|[^""])*"")(?=\s*:)|(?<string>""(?:\\.|[^""])*"")|(?<number>-?\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b)|(?<literal>\b(?:true|false|null)\b)", RegexOptions.Compiled)
        Public Function HighlightLine(lineIndex As Integer, lineText As String, previousLineState As Integer) As CodeSyntaxHighlightResult Implements ICodeSyntaxHighlighter.HighlightLine
            Dim tokens As New List(Of CodeSyntaxToken)
            For Each match As Match In Pattern.Matches(lineText)
                Dim tokenColor As Color = If(match.Groups("key").Success, System.Drawing.Color.FromArgb(78, 201, 176), If(match.Groups("string").Success, System.Drawing.Color.FromArgb(214, 157, 133), If(match.Groups("number").Success, System.Drawing.Color.FromArgb(181, 206, 168), System.Drawing.Color.FromArgb(86, 156, 214))))
                tokens.Add(New CodeSyntaxToken(match.Index, match.Length, tokenColor))
            Next
            Return New CodeSyntaxHighlightResult(tokens, 0)
        End Function
    End Class

    Private NotInheritable Class AssemblyHighlighter
        Implements ICodeSyntaxHighlighter
        Private Shared ReadOnly Instructions As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {"mov", "lea", "push", "pop", "add", "sub", "imul", "idiv", "inc", "dec", "and", "or", "xor", "not", "cmp", "test", "jmp", "je", "jne", "jg", "jge", "jl", "jle", "call", "ret", "nop", "int", "syscall"}
        Private Shared ReadOnly Registers As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {"rax", "rbx", "rcx", "rdx", "rsi", "rdi", "rbp", "rsp", "eax", "ebx", "ecx", "edx", "esi", "edi", "ebp", "esp", "ax", "bx", "cx", "dx", "al", "bl", "cl", "dl", "rip", "eip"}
        Private Shared ReadOnly Words As New Regex("\b[A-Za-z_.$?][A-Za-z0-9_.$?]*\b|-?(?:0x[0-9a-fA-F]+|[0-9A-Fa-f]+h|\d+)\b", RegexOptions.Compiled)
        Public Function HighlightLine(lineIndex As Integer, lineText As String, previousLineState As Integer) As CodeSyntaxHighlightResult Implements ICodeSyntaxHighlighter.HighlightLine
            Dim tokens As New List(Of CodeSyntaxToken)
            Dim commentStart = lineText.IndexOf(";"c)
            For Each match As Match In Words.Matches(lineText)
                If commentStart >= 0 AndAlso match.Index >= commentStart Then Exit For
                Dim tokenColor As Color = If(Instructions.Contains(match.Value), System.Drawing.Color.FromArgb(86, 156, 214), If(Registers.Contains(match.Value), System.Drawing.Color.FromArgb(78, 201, 176), If(Char.IsDigit(match.Value(0)) OrElse match.Value.StartsWith("0x", StringComparison.OrdinalIgnoreCase), System.Drawing.Color.FromArgb(181, 206, 168), System.Drawing.Color.Empty)))
                If tokenColor <> System.Drawing.Color.Empty Then tokens.Add(New CodeSyntaxToken(match.Index, match.Length, tokenColor))
            Next
            If commentStart >= 0 Then tokens.Add(New CodeSyntaxToken(commentStart, lineText.Length - commentStart, Color.FromArgb(87, 166, 74)))
            Return New CodeSyntaxHighlightResult(tokens, 0)
        End Function
    End Class
End Class
