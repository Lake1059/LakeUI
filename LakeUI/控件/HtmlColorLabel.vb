Imports System.ComponentModel
Imports System.Drawing.Drawing2D
Imports System.Text.RegularExpressions
Imports System.Numerics
Imports Vortice.Direct2D1

''' <summary>
''' 支持 HTML 颜色/字体标记的 Label 控件。
''' 解析 font / span / b / i / u / s 等常用标记，未识别标记会被剥离但保留其文本内容。
''' 颜色支持：HTML 命名颜色、#rgb / #rrggbb / #rrggbbaa、rgb()、rgba()、hsl()。
''' 性能说明：当 Text 不包含 '&lt;' 与 '&amp;' 字符时自动走纯文本快速渲染路径，与原生 Label 等同；含标记时再走完整 HTML 解析与排版流程。解析、布局、字体均带缓存，仅在依赖项变化时失效。
''' </summary>
Public Class HtmlColorLabel

#Region "构造"

    ''' <summary>初始化 <see cref="HtmlColorLabel"/> 的新实例并启用双缓冲、用户绘制等样式。</summary>
    Public Sub New()
        InitializeComponent()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.SupportsTransparentBackColor, True)
        UpdateStyles()
        SetAutoSizeMode(AutoSizeMode.GrowAndShrink)
    End Sub

#End Region

#Region "D2D 资源"
    ''' <summary>控件级 DC RenderTarget。与窗口 DC 强相关，无法跨控件共享，由控件持有。</summary>
    Private _dcRT As ID2D1DCRenderTarget
    ''' <summary>跨帧复用的 SSAA 离屏 BitmapRT，按 (Width, Height, ssaa) 命中。</summary>
    Private ReadOnly _ssaaCache As New D2DHelper.BitmapRTCache()
    ''' <summary>SolidColorBrush 缓存（RT 切换时自动失效）。</summary>
    Private ReadOnly _brushCache As New D2DHelper.SolidColorBrushCache()
    ''' <summary>DirectWrite TextFormat 缓存。</summary>
    Private ReadOnly _textFormatCache As New D2DHelper.TextFormatCache()

    Private Function GetOrCreateDCRenderTarget() As ID2D1DCRenderTarget
        If _dcRT Is Nothing Then _dcRT = D2DHelper.CreateDCRenderTarget()
        Return _dcRT
    End Function

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        Try : _ssaaCache.Dispose() : Catch : End Try
        Try : _brushCache.Dispose() : Catch : End Try
        Try : _textFormatCache.Dispose() : Catch : End Try
        If _dcRT IsNot Nothing Then
            Try : _dcRT.Dispose() : Catch : End Try
            _dcRT = Nothing
        End If
        MyBase.OnHandleDestroyed(e)
    End Sub
#End Region

#Region "HTML解析"

    Private Structure 文本片段
        Public 文本 As String
        Public 颜色 As Color
        Public 字号 As Single
        Public 字体名称 As String
        Public 字体样式 As FontStyle
    End Structure

    Private Structure 标签样式
        Public 颜色 As Color?
        Public 字号 As Single
        Public 字体名称 As String
        Public 字体样式 As FontStyle
    End Structure

    Private Shared ReadOnly 标签正则 As New Regex("<(/?)\s*(\w+)([^>]*)>", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly fontColor正则 As New Regex("color\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly fontSize属性正则 As New Regex("size\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly fontFace正则 As New Regex("face\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly style属性正则 As New Regex("style\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly cssColor正则 As New Regex("(?:^|;\s*)color\s*:\s*([^;]+)", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly cssFontSize正则 As New Regex("(?:^|;\s*)font-size\s*:\s*([^;]+)", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly cssFontFamily正则 As New Regex("(?:^|;\s*)font-family\s*:\s*([^;]+)", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly cssFontWeight正则 As New Regex("(?:^|;\s*)font-weight\s*:\s*([^;]+)", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly cssFontStyle正则 As New Regex("(?:^|;\s*)font-style\s*:\s*([^;]+)", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly cssTextDecoration正则 As New Regex("(?:^|;\s*)text-decoration\s*:\s*([^;]+)", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly rgb正则 As New Regex("^rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)$", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly rgba正则 As New Regex("^rgba\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*([\d.]+)\s*\)$", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly hsl正则 As New Regex("^hsl\(\s*([\d.]+)\s*,\s*([\d.]+)%?\s*,\s*([\d.]+)%?\s*\)$", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly 字号数值正则 As New Regex("^([\d.]+)\s*(px|pt|em)?$", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly 已知标签集合 As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "font", "span", "div", "p", "br",
        "b", "i", "u", "s", "em", "strong", "del", "ins",
        "sub", "sup", "small", "big", "mark",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "a", "pre", "code"}

    ''' <summary>当文本不含 '&lt;' 与 '&amp;' 时视为纯文本，可绕过 HTML 解析与排版的完整流程。</summary>
    Private Shared Function 是否纯文本(html As String) As Boolean
        If String.IsNullOrEmpty(html) Then Return True
        For i As Integer = 0 To html.Length - 1
            Dim c = html(i)
            If c = "<"c OrElse c = "&"c Then Return False
        Next
        Return True
    End Function

    Private Function 解析为片段列表(html As String) As List(Of 文本片段)
        Dim 结果 As New List(Of 文本片段)
        If String.IsNullOrEmpty(html) Then Return 结果
        ' 纯文本快速路径：避免任何正则匹配、栈分配，仅产生单一片段。
        If 是否纯文本(html) Then
            结果.Add(New 文本片段 With {.文本 = html, .颜色 = 文本颜色, .字号 = 0, .字体名称 = Nothing, .字体样式 = Me.Font.Style})
            Return 结果
        End If
        Dim 颜色栈 As New Stack(Of Color)
        颜色栈.Push(文本颜色)
        Dim 字号栈 As New Stack(Of Single)
        字号栈.Push(0)
        Dim 字体名称栈 As New Stack(Of String)
        字体名称栈.Push(Nothing)
        Dim 推色记录 As New Stack(Of Boolean)
        Dim 推号记录 As New Stack(Of Boolean)
        Dim 推名记录 As New Stack(Of Boolean)
        Dim 字体样式栈 As New Stack(Of FontStyle)
        字体样式栈.Push(Me.Font.Style)
        Dim 推样式记录 As New Stack(Of Boolean)
        Dim 上次位置 As Integer = 0
        For Each m As Match In 标签正则.Matches(html)
            Dim 标签名 = m.Groups(2).Value.ToLowerInvariant()
            If Not 已知标签集合.Contains(标签名) Then Continue For
            If m.Index > 上次位置 Then
                Dim t = 解码HTML实体(html.Substring(上次位置, m.Index - 上次位置))
                If t.Length > 0 Then 结果.Add(New 文本片段 With {.文本 = t, .颜色 = 颜色栈.Peek(), .字号 = 字号栈.Peek(), .字体名称 = 字体名称栈.Peek(), .字体样式 = 字体样式栈.Peek()})
            End If
            上次位置 = m.Index + m.Length
            Dim 是否闭合 = m.Groups(1).Value = "/"
            Dim 属性 = m.Groups(3).Value
            If 标签名 = "br" AndAlso Not 是否闭合 Then
                结果.Add(New 文本片段 With {.文本 = vbLf, .颜色 = 颜色栈.Peek(), .字号 = 字号栈.Peek(), .字体名称 = 字体名称栈.Peek(), .字体样式 = 字体样式栈.Peek()})
                Continue For
            End If
            If 是否闭合 Then
                If 推色记录.Count > 0 AndAlso 推色记录.Pop() Then
                    If 颜色栈.Count > 1 Then 颜色栈.Pop()
                End If
                If 推号记录.Count > 0 AndAlso 推号记录.Pop() Then
                    If 字号栈.Count > 1 Then 字号栈.Pop()
                End If
                If 推名记录.Count > 0 AndAlso 推名记录.Pop() Then
                    If 字体名称栈.Count > 1 Then 字体名称栈.Pop()
                End If
                If 推样式记录.Count > 0 AndAlso 推样式记录.Pop() Then
                    If 字体样式栈.Count > 1 Then 字体样式栈.Pop()
                End If
            Else
                Dim 样式 = 提取标签样式(标签名, 属性)
                If 样式.颜色.HasValue Then
                    颜色栈.Push(样式.颜色.Value)
                    推色记录.Push(True)
                Else
                    推色记录.Push(False)
                End If
                If 样式.字号 > 0 Then
                    字号栈.Push(样式.字号)
                    推号记录.Push(True)
                Else
                    推号记录.Push(False)
                End If
                If 样式.字体名称 IsNot Nothing Then
                    字体名称栈.Push(样式.字体名称)
                    推名记录.Push(True)
                Else
                    推名记录.Push(False)
                End If
                Dim 隐含样式 = 获取标签隐含样式(标签名)
                Dim 新增样式 = 隐含样式 Or 样式.字体样式
                If 新增样式 <> FontStyle.Regular Then
                    字体样式栈.Push(字体样式栈.Peek() Or 新增样式)
                    推样式记录.Push(True)
                Else
                    推样式记录.Push(False)
                End If
            End If
        Next
        If 上次位置 < html.Length Then
            Dim t = 解码HTML实体(html.Substring(上次位置))
            If t.Length > 0 Then 结果.Add(New 文本片段 With {.文本 = t, .颜色 = 颜色栈.Peek(), .字号 = 字号栈.Peek(), .字体名称 = 字体名称栈.Peek(), .字体样式 = 字体样式栈.Peek()})
        End If
        Return 结果
    End Function

    Private Function 提取标签样式(标签名 As String, 属性 As String) As 标签样式
        Dim 结果 As New 标签样式
        If String.IsNullOrWhiteSpace(属性) Then Return 结果
        Select Case 标签名
            Case "font"
                Dim m = fontColor正则.Match(属性)
                If m.Success Then
                    Dim 值 = If(m.Groups(1).Success AndAlso m.Groups(1).Length > 0, m.Groups(1).Value,
                              If(m.Groups(2).Success AndAlso m.Groups(2).Length > 0, m.Groups(2).Value,
                              m.Groups(3).Value))
                    结果.颜色 = 解析颜色值(值.Trim())
                End If
                Dim sm = fontSize属性正则.Match(属性)
                If sm.Success Then
                    Dim 值 = If(sm.Groups(1).Success AndAlso sm.Groups(1).Length > 0, sm.Groups(1).Value,
                              If(sm.Groups(2).Success AndAlso sm.Groups(2).Length > 0, sm.Groups(2).Value,
                              sm.Groups(3).Value))
                    结果.字号 = 解析字号值(值.Trim())
                End If
                Dim fm = fontFace正则.Match(属性)
                If fm.Success Then
                    Dim 值 = If(fm.Groups(1).Success AndAlso fm.Groups(1).Length > 0, fm.Groups(1).Value,
                              If(fm.Groups(2).Success AndAlso fm.Groups(2).Length > 0, fm.Groups(2).Value,
                              fm.Groups(3).Value))
                    结果.字体名称 = 解析字体名称(值.Trim())
                End If
            Case "span", "div", "p"
                Dim sm = style属性正则.Match(属性)
                If sm.Success Then
                    Dim styleVal = If(sm.Groups(1).Success AndAlso sm.Groups(1).Length > 0, sm.Groups(1).Value, sm.Groups(2).Value)
                    Dim cm = cssColor正则.Match(styleVal)
                    If cm.Success Then 结果.颜色 = 解析颜色值(cm.Groups(1).Value.Trim())
                    Dim fm = cssFontSize正则.Match(styleVal)
                    If fm.Success Then 结果.字号 = 解析字号值(fm.Groups(1).Value.Trim())
                    Dim ffm = cssFontFamily正则.Match(styleVal)
                    If ffm.Success Then 结果.字体名称 = 解析字体名称(ffm.Groups(1).Value.Trim())
                    Dim fwm = cssFontWeight正则.Match(styleVal)
                    If fwm.Success Then
                        Dim v = fwm.Groups(1).Value.Trim().ToLowerInvariant()
                        Dim wt As Integer
                        If v = "bold" OrElse v = "bolder" OrElse (Integer.TryParse(v, wt) AndAlso wt >= 700) Then 结果.字体样式 = 结果.字体样式 Or FontStyle.Bold
                    End If
                    Dim fsm = cssFontStyle正则.Match(styleVal)
                    If fsm.Success Then
                        Dim v = fsm.Groups(1).Value.Trim().ToLowerInvariant()
                        If v = "italic" OrElse v = "oblique" Then 结果.字体样式 = 结果.字体样式 Or FontStyle.Italic
                    End If
                    Dim tdm = cssTextDecoration正则.Match(styleVal)
                    If tdm.Success Then
                        Dim v = tdm.Groups(1).Value.Trim().ToLowerInvariant()
                        If v.Contains("underline") Then 结果.字体样式 = 结果.字体样式 Or FontStyle.Underline
                        If v.Contains("line-through") Then 结果.字体样式 = 结果.字体样式 Or FontStyle.Strikeout
                    End If
                End If
        End Select
        Return 结果
    End Function

    Private Shared Function 获取标签隐含样式(标签名 As String) As FontStyle
        Select Case 标签名
            Case "b", "strong" : Return FontStyle.Bold
            Case "i", "em" : Return FontStyle.Italic
            Case "u", "ins" : Return FontStyle.Underline
            Case "s", "del" : Return FontStyle.Strikeout
            Case Else : Return FontStyle.Regular
        End Select
    End Function

    Private Function 解析字号值(值 As String) As Single
        If String.IsNullOrWhiteSpace(值) Then Return 0
        Dim m = 字号数值正则.Match(值.Trim())
        If Not m.Success Then Return 0
        Dim 数值 As Single
        If Not Single.TryParse(m.Groups(1).Value, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, 数值) Then Return 0
        If 数值 <= 0 Then Return 0
        Dim 单位 = If(m.Groups(2).Success, m.Groups(2).Value.ToLowerInvariant(), "")
        Select Case 单位
            Case "px"
                Return 数值 * 72.0F / 96.0F
            Case "pt", ""
                Return 数值
            Case "em"
                Return Me.Font.SizeInPoints * 数值
            Case Else
                Return 0
        End Select
    End Function

    Private Shared Function 解析字体名称(值 As String) As String
        If String.IsNullOrWhiteSpace(值) Then Return Nothing
        Dim 第一项 = 值.Split(","c)(0).Trim().Trim(""""c, "'"c).Trim()
        If 第一项.Length = 0 Then Return Nothing
        Return 第一项
    End Function

    Private Shared Function 解析颜色值(值 As String) As Color?
        If String.IsNullOrWhiteSpace(值) Then Return Nothing
        值 = 值.Trim()
        Dim rm = rgb正则.Match(值)
        If rm.Success Then
            Return Color.FromArgb(
                Math.Clamp(Integer.Parse(rm.Groups(1).Value), 0, 255),
                Math.Clamp(Integer.Parse(rm.Groups(2).Value), 0, 255),
                Math.Clamp(Integer.Parse(rm.Groups(3).Value), 0, 255))
        End If
        Dim ram = rgba正则.Match(值)
        If ram.Success Then
            Dim a As Double = Double.Parse(ram.Groups(4).Value, Globalization.CultureInfo.InvariantCulture)
            If a <= 1.0 Then a *= 255
            Return Color.FromArgb(
                Math.Clamp(CInt(a), 0, 255),
                Math.Clamp(Integer.Parse(ram.Groups(1).Value), 0, 255),
                Math.Clamp(Integer.Parse(ram.Groups(2).Value), 0, 255),
                Math.Clamp(Integer.Parse(ram.Groups(3).Value), 0, 255))
        End If
        Dim hm = hsl正则.Match(值)
        If hm.Success Then
            Return HSL转RGB(
                Double.Parse(hm.Groups(1).Value, Globalization.CultureInfo.InvariantCulture),
                Double.Parse(hm.Groups(2).Value, Globalization.CultureInfo.InvariantCulture) / 100.0,
                Double.Parse(hm.Groups(3).Value, Globalization.CultureInfo.InvariantCulture) / 100.0)
        End If
        If 值.StartsWith("#"c) Then
            Try
                Dim hex = 值.Substring(1)
                Select Case hex.Length
                    Case 3
                        Return Color.FromArgb(
                            Convert.ToInt32(hex(0).ToString() & hex(0), 16),
                            Convert.ToInt32(hex(1).ToString() & hex(1), 16),
                            Convert.ToInt32(hex(2).ToString() & hex(2), 16))
                    Case 6
                        Return ColorTranslator.FromHtml(值)
                    Case 8
                        Return Color.FromArgb(
                            Convert.ToInt32(hex.Substring(6, 2), 16),
                            Convert.ToInt32(hex.Substring(0, 2), 16),
                            Convert.ToInt32(hex.Substring(2, 2), 16),
                            Convert.ToInt32(hex.Substring(4, 2), 16))
                End Select
            Catch
            End Try
        End If
        Try
            Dim c = Color.FromName(值)
            If c.IsKnownColor Then Return c
            Return ColorTranslator.FromHtml(值)
        Catch
        End Try
        Return Nothing
    End Function

    Private Shared Function HSL转RGB(h As Double, s As Double, l As Double) As Color
        h = ((h Mod 360) + 360) Mod 360
        Dim c As Double = (1 - Math.Abs(2 * l - 1)) * s
        Dim x As Double = c * (1 - Math.Abs((h / 60.0) Mod 2 - 1))
        Dim m As Double = l - c / 2
        Dim r1, g1, b1 As Double
        Select Case CInt(Math.Floor(h / 60.0)) Mod 6
            Case 0 : r1 = c : g1 = x : b1 = 0
            Case 1 : r1 = x : g1 = c : b1 = 0
            Case 2 : r1 = 0 : g1 = c : b1 = x
            Case 3 : r1 = 0 : g1 = x : b1 = c
            Case 4 : r1 = x : g1 = 0 : b1 = c
            Case Else : r1 = c : g1 = 0 : b1 = x
        End Select
        Return Color.FromArgb(
            Math.Clamp(CInt((r1 + m) * 255), 0, 255),
            Math.Clamp(CInt((g1 + m) * 255), 0, 255),
            Math.Clamp(CInt((b1 + m) * 255), 0, 255))
    End Function

    Private Shared Function 解码HTML实体(text As String) As String
        ' 绝大多数情况无 '&'，提前返回避免 5 次 String.Replace 分配。
        If String.IsNullOrEmpty(text) OrElse text.IndexOf("&"c) < 0 Then Return text
        Return text.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", """").Replace("&apos;", "'")
    End Function

#End Region

#Region "绘制"

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        If 边框圆角半径 > 0 OrElse MyBase.BackColor.A < 255 Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim 是否有圆角 As Boolean = 边框圆角半径 > 0
        Dim 极限矩形区域 As New RectangleF(0, 0, Me.Width, Me.Height)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * DpiScale() / 2.0F
            极限矩形区域.Inflate(-half, -half)
        End If
        Dim 内容矩形区域 As New RectangleF(
            极限矩形区域.X + Me.Padding.Left,
            极限矩形区域.Y + Me.Padding.Top,
            极限矩形区域.Width - Me.Padding.Horizontal,
            极限矩形区域.Height - Me.Padding.Vertical)
        Dim _ssaa As Integer = If(Class1.GlobalSSAA > 1, CInt(Class1.GlobalSSAA), 超采样倍率)

        Using scope = D2DHelper.BeginPaint(e, Me, GetOrCreateDCRenderTarget(), _ssaa, _ssaaCache)
            Dim gRT As ID2D1RenderTarget = scope.GraphicsRenderTarget
            Dim dcRT As ID2D1DCRenderTarget = scope.DCRenderTarget

            ' 0) 透明背景贴底图：直接画到图形 RT，避免 dcRT.EndDraw 把圆角外像素覆盖为黑。
            If 是否有圆角 OrElse MyBase.BackColor.A < 255 Then
                TransparentBackgroundCache.PaintBackgroundFor_D2D(Me, gRT, _backgroundSource)
            End If

            ' 1) 图形层（享受 SSAA）
            绘制图形内容_D2D(gRT, 是否有圆角, 极限矩形区域)

            ' 2) 把图形层回采到 DC，然后在 DC 上画文字（DirectWrite ClearType 子像素）
            scope.FlushGraphics()
            绘制文本内容_D2D(dcRT, 内容矩形区域)

            ' 3) 禁用遮罩
            If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
                If 是否有圆角 Then
                    Using geo = RectangleRenderer.创建圆角矩形几何(极限矩形区域, 边框圆角半径 * DpiScale())
                        RectangleRenderer.绘制圆角背景_D2D(dcRT, geo, 极限矩形区域, 禁用时遮罩颜色, Color.Empty, System.Windows.Forms.Orientation.Vertical)
                    End Using
                Else
                    RectangleRenderer.绘制矩形背景_D2D(dcRT, 极限矩形区域, 禁用时遮罩颜色, Color.Empty, System.Windows.Forms.Orientation.Vertical)
                End If
            End If
        End Using
    End Sub

    ''' <summary>
    ''' 透明背景贴底图：圆角或 BackColor 含 alpha 时，由共享缓存把 BackgroundSource（或 Parent）
    ''' 的内容采样到本控件区域。本控件直接以 MyBase.BackColor 作为"BackColor 半透明遮罩 +
    ''' 实色填充"的合一层（A=255 时为实色 BackColor1 等价层，0&lt;A&lt;255 时为半透明遮罩，
    ''' A=0 时退化为不绘制）。详见 TransparentBackgroundCache 顶部的统一图层契约。
    ''' </summary>
    Private Sub 绘制父容器背景(g As Graphics)
        TransparentBackgroundCache.PaintBackgroundFor(Me, g, _backgroundSource)
    End Sub

    Private Sub 绘制图形内容_D2D(rt As ID2D1RenderTarget, 是否有圆角 As Boolean, 极限矩形区域 As RectangleF)
        Dim s As Single = DpiScale()
        Dim r As Single = 边框圆角半径 * s
        If MyBase.BackColor.A = 0 Then
            If 是否有圆角 Then
                Using geo = RectangleRenderer.创建圆角矩形几何(极限矩形区域, r)
                    RectangleRenderer.绘制圆角边框_D2D(rt, geo, 边框颜色, 边框宽度 * s)
                End Using
            Else
                RectangleRenderer.绘制矩形边框_D2D(rt, 极限矩形区域, 边框颜色, 边框宽度 * s)
            End If
            Return
        End If
        If 是否有圆角 Then
            Using geo = RectangleRenderer.创建圆角矩形几何(极限矩形区域, r)
                If MyBase.BackColor.A > 0 Then
                    RectangleRenderer.绘制圆角背景_D2D(rt, geo, 极限矩形区域, MyBase.BackColor, Color.Empty, System.Windows.Forms.Orientation.Vertical)
                End If
                If 边框颜色.A > 0 AndAlso 边框宽度 > 0 Then
                    RectangleRenderer.绘制圆角边框_D2D(rt, geo, 边框颜色, 边框宽度 * s)
                End If
            End Using
        Else
            If MyBase.BackColor.A > 0 Then
                RectangleRenderer.绘制矩形背景_D2D(rt, 极限矩形区域, MyBase.BackColor, Color.Empty, System.Windows.Forms.Orientation.Vertical)
            End If
            If 边框颜色.A > 0 AndAlso 边框宽度 > 0 Then
                RectangleRenderer.绘制矩形边框_D2D(rt, 极限矩形区域, 边框颜色, 边框宽度 * s)
            End If
        End If
    End Sub

    Private Structure 渲染单元
        Public 文本 As String
        Public 颜色 As Color
        Public 字号 As Single
        Public 字体名称 As String
        Public 字体样式 As FontStyle
        Public X偏移 As Integer
        Public 宽度 As Integer
        Public 高度 As Integer
    End Structure

    Private Structure 渲染行
        Public 单元列表 As List(Of 渲染单元)
        Public 行宽 As Integer
        Public 行高 As Integer
    End Structure

    Private Shared ReadOnly 文本测量格式 As TextFormatFlags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine

    ' 布局与字体缓存：仅在字体/文本/宽度变更时失效，避免重复解析与布局。
    Private 缓存布局行表 As List(Of 渲染行)
    Private 缓存布局宽度 As Integer = -1
    Private 缓存布局文本 As String
    Private 缓存布局字体版本 As Integer
    Private 缓存字体 As Dictionary(Of String, Font)
    Private 字体版本 As Integer

    ''' <summary>释放布局与字体缓存，本控件在字体/文本/颜色变更时调用。</summary>
    Private Sub 使缓存失效(是否含字体 As Boolean)
        缓存布局行表 = Nothing
        缓存布局宽度 = -1
        缓存布局文本 = Nothing
        If 是否含字体 AndAlso 缓存字体 IsNot Nothing Then
            For Each f In 缓存字体.Values
                If f IsNot Me.Font Then f.Dispose()
            Next
            缓存字体.Clear()
            字体版本 += 1
        End If
    End Sub

    Private Shared Function 测量文本尺寸(text As String, font As Font) As Size
        If String.IsNullOrEmpty(text) Then Return Size.Empty
        Return TextRenderer.MeasureText(text, font, New Size(Integer.MaxValue, Integer.MaxValue), 文本测量格式)
    End Function

    Private Function 获取缓存字体(字号 As Single, 字体名称 As String, 字体样式 As FontStyle) As Font
        If 字号 <= 0 AndAlso String.IsNullOrEmpty(字体名称) AndAlso 字体样式 = Me.Font.Style Then Return Me.Font
        If 缓存字体 Is Nothing Then 缓存字体 = New Dictionary(Of String, Font)(StringComparer.Ordinal)
        Dim 实际字号 = If(字号 > 0, 字号, Me.Font.SizeInPoints)
        Dim 实际字体 = If(String.IsNullOrEmpty(字体名称), Me.Font.FontFamily.Name, 字体名称)
        Dim 缓存键 = 实际字体 & "|" & 实际字号.ToString(Globalization.CultureInfo.InvariantCulture) & "|" & CInt(字体样式).ToString()
        Dim value As Font = Nothing
        If Not 缓存字体.TryGetValue(缓存键, value) Then
            value = New Font(实际字体, 实际字号, 字体样式, GraphicsUnit.Point)
            缓存字体(缓存键) = value
        End If
        Return value
    End Function

    Private Function 计算文本布局(最大宽度 As Integer) As List(Of 渲染行)
        ' 命中缓存：相同文本/宽度/字体版本下直接复用上次布局结果。
        If 缓存布局行表 IsNot Nothing AndAlso
           最大宽度 = 缓存布局宽度 AndAlso
           Object.ReferenceEquals(缓存布局文本, MyBase.Text) AndAlso
           缓存布局字体版本 = 字体版本 Then
            Return 缓存布局行表
        End If
        Dim 片段列表 = 解析为片段列表(MyBase.Text)
        Dim 行列表 As New List(Of 渲染行)
        If 片段列表.Count = 0 Then
            缓存布局行表 = 行列表
            缓存布局宽度 = 最大宽度
            缓存布局文本 = MyBase.Text
            缓存布局字体版本 = 字体版本
            Return 行列表
        End If
        Dim 默认行高 As Integer = 测量文本尺寸("A", Me.Font).Height
        Dim 当前行 As New 渲染行 With {.单元列表 = New List(Of 渲染单元), .行宽 = 0, .行高 = 0}
        For Each 片段 In 片段列表
            Dim 片段字体 = 获取缓存字体(片段.字号, 片段.字体名称, 片段.字体样式)
            Dim 行数组 = 片段.文本.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
            For 行索引 As Integer = 0 To 行数组.Length - 1
                If 行索引 > 0 Then
                    If 当前行.行高 = 0 Then 当前行.行高 = 默认行高
                    行列表.Add(当前行)
                    当前行 = New 渲染行 With {.单元列表 = New List(Of 渲染单元), .行宽 = 0, .行高 = 0}
                End If
                Dim 行文本 = 行数组(行索引)
                If 行文本.Length = 0 Then Continue For
                Dim 单元列表 = 拆分为可绘制单元(行文本)
                For Each 单元 In 单元列表
                    Dim 单元尺寸 = 测量文本尺寸(单元, 片段字体)
                    Dim 单元宽度 = 单元尺寸.Width
                    Dim 单元高度 = 单元尺寸.Height
                    If 当前行.行宽 > 0 AndAlso 当前行.行宽 + 单元宽度 > 最大宽度 Then
                        If 当前行.行高 = 0 Then 当前行.行高 = 单元高度
                        行列表.Add(当前行)
                        当前行 = New 渲染行 With {.单元列表 = New List(Of 渲染单元), .行宽 = 0, .行高 = 0}
                    End If
                    If 单元宽度 > 最大宽度 AndAlso 单元.Length > 1 Then
                        For Each ch In 单元
                            Dim 字符文本 = ch.ToString()
                            Dim 字符尺寸 = 测量文本尺寸(字符文本, 片段字体)
                            Dim 字符宽度 = 字符尺寸.Width
                            Dim 字符高度 = 字符尺寸.Height
                            If 当前行.行宽 > 0 AndAlso 当前行.行宽 + 字符宽度 > 最大宽度 Then
                                If 当前行.行高 = 0 Then 当前行.行高 = 字符高度
                                行列表.Add(当前行)
                                当前行 = New 渲染行 With {.单元列表 = New List(Of 渲染单元), .行宽 = 0, .行高 = 0}
                            End If
                            当前行.单元列表.Add(New 渲染单元 With {
                                .文本 = 字符文本, .颜色 = 片段.颜色, .字号 = 片段.字号, .字体名称 = 片段.字体名称, .字体样式 = 片段.字体样式,
                                .X偏移 = 当前行.行宽, .宽度 = 字符宽度, .高度 = 字符高度})
                            当前行.行宽 += 字符宽度
                            当前行.行高 = Math.Max(当前行.行高, 字符高度)
                        Next
                    Else
                        当前行.单元列表.Add(New 渲染单元 With {
                            .文本 = 单元, .颜色 = 片段.颜色, .字号 = 片段.字号, .字体名称 = 片段.字体名称, .字体样式 = 片段.字体样式,
                            .X偏移 = 当前行.行宽, .宽度 = 单元宽度, .高度 = 单元高度})
                        当前行.行宽 += 单元宽度
                        当前行.行高 = Math.Max(当前行.行高, 单元高度)
                    End If
                Next
            Next
        Next
        If 当前行.行高 = 0 Then 当前行.行高 = 默认行高
        行列表.Add(当前行)
        缓存布局行表 = 行列表
        缓存布局宽度 = 最大宽度
        缓存布局文本 = MyBase.Text
        缓存布局字体版本 = 字体版本
        Return 行列表
    End Function

    Private Sub 绘制文本内容_D2D(rt As ID2D1DCRenderTarget, 内容矩形区域 As RectangleF)
        Dim 内容区域 As Rectangle = Rectangle.Round(内容矩形区域)
        Dim 原始文本 = MyBase.Text
        ' 纯文本快速路径：无 HTML 标记时直接 DirectWrite 绘制。
        If Not String.IsNullOrEmpty(原始文本) AndAlso 是否纯文本(原始文本) Then
            绘制纯文本_D2D(rt, 原始文本, 内容区域)
            Return
        End If
        Dim 行列表 = 计算文本布局(内容区域.Width)
        If 行列表.Count = 0 Then Return
        Dim 总高度 As Integer = 0
        For Each 行 In 行列表
            总高度 += 行.行高
        Next
        If 行列表.Count > 1 Then 总高度 += 行距 * (行列表.Count - 1)
        Dim 起始Y As Integer
        Select Case 文字对齐方位
            Case TextAlignEnum.BottomLeft, TextAlignEnum.BottomRight
                起始Y = 内容区域.Y + 内容区域.Height - 总高度
            Case TextAlignEnum.Center, TextAlignEnum.MiddleLeft, TextAlignEnum.MiddleRight
                起始Y = 内容区域.Y + (内容区域.Height - 总高度) \ 2
            Case Else
                起始Y = 内容区域.Y
        End Select
        Dim 当前Y As Integer = 起始Y
        For Each 行 In 行列表
            Dim 对齐偏移 As Integer
            Select Case 文字对齐方位
                Case TextAlignEnum.Center
                    对齐偏移 = (内容区域.Width - 行.行宽) \ 2
                Case TextAlignEnum.TopRight, TextAlignEnum.BottomRight, TextAlignEnum.MiddleRight
                    对齐偏移 = 内容区域.Width - 行.行宽
                Case Else
                    对齐偏移 = 0
            End Select
            For Each 单元 In 行.单元列表
                Dim 单元字体 = 获取缓存字体(单元.字号, 单元.字体名称, 单元.字体样式)
                Dim Y偏移 As Integer
                Select Case 行内垂直对齐方式
                    Case InlineAlignEnum.Center
                        Y偏移 = (行.行高 - 单元.高度) \ 2
                    Case Else
                        Y偏移 = 行.行高 - 单元.高度
                End Select
                Dim 绘制位置 As New PointF(内容区域.X + 对齐偏移 + 单元.X偏移, 当前Y + Y偏移)
                绘制单元_D2D(rt, 单元.文本, 单元字体, 单元.颜色, 绘制位置, 单元.宽度, 单元.高度)
            Next
            当前Y += 行.行高 + 行距
        Next
    End Sub

    Private Sub 绘制纯文本_D2D(rt As ID2D1DCRenderTarget, text As String, 内容区域 As Rectangle)
        Dim sizePx As Single = Me.Font.SizeInPoints * (96.0F / 72.0F) * DpiScale()
        Dim weight As Vortice.DirectWrite.FontWeight = If(Me.Font.Bold, Vortice.DirectWrite.FontWeight.Bold, Vortice.DirectWrite.FontWeight.Normal)
        Dim style As Vortice.DirectWrite.FontStyle = If(Me.Font.Italic, Vortice.DirectWrite.FontStyle.Italic, Vortice.DirectWrite.FontStyle.Normal)

        Dim ta As Vortice.DirectWrite.TextAlignment
        Dim pa As Vortice.DirectWrite.ParagraphAlignment
        Select Case 文字对齐方位
            Case TextAlignEnum.TopLeft : ta = Vortice.DirectWrite.TextAlignment.Leading : pa = Vortice.DirectWrite.ParagraphAlignment.Near
            Case TextAlignEnum.TopRight : ta = Vortice.DirectWrite.TextAlignment.Trailing : pa = Vortice.DirectWrite.ParagraphAlignment.Near
            Case TextAlignEnum.Center : ta = Vortice.DirectWrite.TextAlignment.Center : pa = Vortice.DirectWrite.ParagraphAlignment.Center
            Case TextAlignEnum.BottomLeft : ta = Vortice.DirectWrite.TextAlignment.Leading : pa = Vortice.DirectWrite.ParagraphAlignment.Far
            Case TextAlignEnum.BottomRight : ta = Vortice.DirectWrite.TextAlignment.Trailing : pa = Vortice.DirectWrite.ParagraphAlignment.Far
            Case TextAlignEnum.MiddleLeft : ta = Vortice.DirectWrite.TextAlignment.Leading : pa = Vortice.DirectWrite.ParagraphAlignment.Center
            Case TextAlignEnum.MiddleRight : ta = Vortice.DirectWrite.TextAlignment.Trailing : pa = Vortice.DirectWrite.ParagraphAlignment.Center
            Case Else : ta = Vortice.DirectWrite.TextAlignment.Leading : pa = Vortice.DirectWrite.ParagraphAlignment.Near
        End Select

        Dim fmt = _textFormatCache.Get(Me.Font.FontFamily.Name, weight, style, sizePx, ta, pa, False)
        Try
            fmt.WordWrapping = Vortice.DirectWrite.WordWrapping.Wrap
        Catch
        End Try
        Dim brush = _brushCache.Get(rt, 文本颜色)
        rt.DrawText(text, fmt,
                    New Vortice.Mathematics.Rect(内容区域.X, 内容区域.Y, 内容区域.Width, 内容区域.Height),
                    brush, DrawTextOptions.None, Vortice.DCommon.MeasuringMode.GdiClassic)
    End Sub

    Private Sub 绘制单元_D2D(rt As ID2D1DCRenderTarget, text As String, font As Font, color As Color,
                           pt As PointF, w As Integer, h As Integer)
        If String.IsNullOrEmpty(text) Then Return
        Dim sizePx As Single = font.SizeInPoints * (96.0F / 72.0F) * DpiScale()
        Dim weight As Vortice.DirectWrite.FontWeight = If((font.Style And FontStyle.Bold) <> 0, Vortice.DirectWrite.FontWeight.Bold, Vortice.DirectWrite.FontWeight.Normal)
        Dim style As Vortice.DirectWrite.FontStyle = If((font.Style And FontStyle.Italic) <> 0, Vortice.DirectWrite.FontStyle.Italic, Vortice.DirectWrite.FontStyle.Normal)
        Dim fmt = _textFormatCache.Get(font.FontFamily.Name, weight, style, sizePx,
                                       Vortice.DirectWrite.TextAlignment.Leading,
                                       Vortice.DirectWrite.ParagraphAlignment.Near, False)
        Dim brush = _brushCache.Get(rt, color)
        ' 使用 GDI 经典度量以贴近 TextRenderer.MeasureText 的布局结果
        Dim layoutRect As New Vortice.Mathematics.Rect(pt.X, pt.Y, Math.Max(1, w + 2), Math.Max(1, h + 2))

        ' 下划线 / 删除线：DirectWrite 通过 TextLayout SetUnderline/SetStrikethrough 实现
        Dim needsLayout As Boolean = (font.Style And (FontStyle.Underline Or FontStyle.Strikeout)) <> 0
        If needsLayout Then
            Using layout = D2DHelper.GetDWriteFactory().CreateTextLayout(text, fmt, layoutRect.Right - layoutRect.Left, layoutRect.Bottom - layoutRect.Top)
                Dim range As New Vortice.DirectWrite.TextRange(0, text.Length)
                If (font.Style And FontStyle.Underline) <> 0 Then layout.SetUnderline(True, range)
                If (font.Style And FontStyle.Strikeout) <> 0 Then layout.SetStrikethrough(True, range)
                rt.DrawTextLayout(New Vector2(pt.X, pt.Y), layout, brush, DrawTextOptions.None)
            End Using
        Else
            rt.DrawText(text, fmt, layoutRect, brush, DrawTextOptions.None, Vortice.DCommon.MeasuringMode.GdiClassic)
        End If
    End Sub

    ''' <summary>根据当前文字与行内对齐设置生成纯文本路径所需的 <see cref="TextFormatFlags"/>。</summary>
    Private Function 计算纯文本绘制标志() As TextFormatFlags
        Dim flags As TextFormatFlags = TextFormatFlags.NoPadding Or TextFormatFlags.WordBreak
        Select Case 文字对齐方位
            Case TextAlignEnum.TopLeft
                flags = flags Or TextFormatFlags.Top Or TextFormatFlags.Left
            Case TextAlignEnum.TopRight
                flags = flags Or TextFormatFlags.Top Or TextFormatFlags.Right
            Case TextAlignEnum.Center
                flags = flags Or TextFormatFlags.VerticalCenter Or TextFormatFlags.HorizontalCenter
            Case TextAlignEnum.BottomLeft
                flags = flags Or TextFormatFlags.Bottom Or TextFormatFlags.Left
            Case TextAlignEnum.BottomRight
                flags = flags Or TextFormatFlags.Bottom Or TextFormatFlags.Right
            Case TextAlignEnum.MiddleLeft
                flags = flags Or TextFormatFlags.VerticalCenter Or TextFormatFlags.Left
            Case TextAlignEnum.MiddleRight
                flags = flags Or TextFormatFlags.VerticalCenter Or TextFormatFlags.Right
        End Select
        Return flags
    End Function

    Private Shared Function 拆分为可绘制单元(text As String) As List(Of String)
        Dim 结果 As New List(Of String)
        Dim i As Integer = 0
        While i < text.Length
            If Char.IsWhiteSpace(text(i)) Then
                Dim start = i
                While i < text.Length AndAlso Char.IsWhiteSpace(text(i))
                    i += 1
                End While
                结果.Add(text.Substring(start, i - start))
            ElseIf 是否CJK字符(text(i)) Then
                结果.Add(text(i).ToString())
                i += 1
            Else
                Dim start = i
                While i < text.Length AndAlso Not Char.IsWhiteSpace(text(i)) AndAlso Not 是否CJK字符(text(i))
                    i += 1
                End While
                结果.Add(text.Substring(start, i - start))
            End If
        End While
        Return 结果
    End Function

    Private Shared Function 是否CJK字符(ch As Char) As Boolean
        ' 在常见 ASCII 范围快速否决，避免不必要的范围比较。
        Dim code As Integer = AscW(ch)
        If code < &H3000 Then Return False
        Return (code >= &H4E00 AndAlso code <= &H9FFF) OrElse
               (code >= &H3400 AndAlso code <= &H4DBF) OrElse
               (code >= &HF900 AndAlso code <= &HFAFF) OrElse
               (code >= &H3000 AndAlso code <= &H303F) OrElse
               (code >= &HFF00 AndAlso code <= &HFFEF)
    End Function

#End Region

#Region "通用"

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            使缓存失效(False)
            更新自动尺寸()
            Me.Invalidate()
        End If
    End Sub

    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function

    Private 超采样倍率 As Integer = 1
    ''' <summary>超采样抗锯齿倍率；仅影响控件背景与边框绘制。</summary>
    <Category("LakeUI"), Description(Class1.超采样抗锯齿描述词), DefaultValue(GetType(Class1.SuperSamplingScaleEnum), "OFF"), Browsable(True)>
    Public Property SuperSamplingScale As Class1.SuperSamplingScaleEnum
        Get
            Return 超采样倍率
        End Get
        Set(value As Class1.SuperSamplingScaleEnum)
            SetValue(超采样倍率, value)
        End Set
    End Property

    Private _backgroundSource As Control = Nothing
    ''' <summary>
    ''' 背景采样源（超容器背景映射）。透明背景模式下，控件会调用此控件的绘制流程取像素作为底图，
    ''' 从而实现跨越任意层级的"穿透显示"效果。
    ''' 为 Nothing 时自动沿祖先链查找首个不透明祖先（默认行为）。
    ''' </summary>
    <Category("LakeUI"),
     Description("背景采样源（超容器背景映射）。设置后将跨越任意层级直接采样此控件的绘制内容作为透明背景；为空时自动选择首个不透明祖先。"),
     DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource IsNot value Then
                _backgroundSource = value
                Me.Invalidate()
            End If
        End Set
    End Property

    Private 启用自动尺寸 As Boolean = False
    Private 自动尺寸前的大小 As Size = Size.Empty
    ''' <summary>是否启用自动尺寸；为 True 时控件会根据文本与 <see cref="Control.MaximumSize"/> 调整大小。</summary>
    <Category("LakeUI"), Description("启用自动尺寸，控件将根据文本内容自动调整大小；配合 MaximumSize.Width 可实现自动换行+自动高度"), DefaultValue(False), Browsable(True)>
    Public Overrides Property AutoSize As Boolean
        Get
            Return 启用自动尺寸
        End Get
        Set(value As Boolean)
            If 启用自动尺寸 <> value Then
                启用自动尺寸 = value
                MyBase.AutoSize = value
                If value Then
                    更新自动尺寸()
                Else
                    If 自动尺寸前的大小 <> Size.Empty Then
                        Me.Size = 自动尺寸前的大小
                    End If
                End If
                Me.Invalidate()
            End If
        End Set
    End Property

    ''' <summary>依据文本、内边距与边框计算控件的最佳尺寸；仅在 <see cref="AutoSize"/> 开启时计算文本占用。</summary>
    Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
        If Not 启用自动尺寸 Then Return Me.Size
        Dim 边框额外 As Integer = CInt(Math.Ceiling(CSng(边框宽度))) + 1
        Dim 最大约束宽度 As Integer
        If Me.MaximumSize.Width > 0 Then
            最大约束宽度 = Me.MaximumSize.Width - Me.Padding.Horizontal - 边框额外
        ElseIf proposedSize.Width > 0 Then
            最大约束宽度 = proposedSize.Width - Me.Padding.Horizontal - 边框额外
        ElseIf Dock = DockStyle.Top OrElse Dock = DockStyle.Bottom OrElse Dock = DockStyle.Fill Then
            最大约束宽度 = Me.Width - Me.Padding.Horizontal - 边框额外
        Else
            最大约束宽度 = Integer.MaxValue
        End If
        最大约束宽度 = Math.Max(1, 最大约束宽度)
        Dim 行列表 = 计算文本布局(最大约束宽度)
        Dim 总高度 As Integer = 0
        Dim 内容最大宽度 As Integer = 0
        For Each 行 In 行列表
            总高度 += 行.行高
            If 行.行宽 > 内容最大宽度 Then 内容最大宽度 = 行.行宽
        Next
        If 行列表.Count > 1 Then 总高度 += 行距 * (行列表.Count - 1)
        If 总高度 = 0 Then 总高度 = 测量文本尺寸("A", Me.Font).Height
        Dim 新宽度 = 内容最大宽度 + Me.Padding.Horizontal + 边框额外
        Dim 新高度 = 总高度 + Me.Padding.Vertical + 边框额外
        If Me.MaximumSize.Width > 0 Then 新宽度 = Math.Min(新宽度, Me.MaximumSize.Width)
        If Me.MaximumSize.Height > 0 Then 新高度 = Math.Min(新高度, Me.MaximumSize.Height)
        If Me.MinimumSize.Width > 0 Then 新宽度 = Math.Max(新宽度, Me.MinimumSize.Width)
        If Me.MinimumSize.Height > 0 Then 新高度 = Math.Max(新高度, Me.MinimumSize.Height)
        Return New Size(新宽度, 新高度)
    End Function

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        If 启用自动尺寸 Then
            更新自动尺寸()
        Else
            自动尺寸前的大小 = Me.Size
        End If
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        更新自动尺寸()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        使缓存失效(True)
        更新自动尺寸()
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        更新自动尺寸()
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        Me.Invalidate()
    End Sub

    Private 正在更新尺寸 As Boolean = False

    Private Sub 更新自动尺寸()
        If Not 启用自动尺寸 OrElse Not IsHandleCreated OrElse 正在更新尺寸 Then Return
        正在更新尺寸 = True
        Try
            Dim preferred = GetPreferredSize(New Size(Me.Width, Me.Height))
            Select Case Dock
                Case DockStyle.Top, DockStyle.Bottom
                    If Me.Height <> preferred.Height Then Me.Height = preferred.Height
                Case DockStyle.Left, DockStyle.Right
                    If Me.Width <> preferred.Width Then Me.Width = preferred.Width
                Case DockStyle.Fill
                    ' Dock=Fill 时尺寸完全由父容器决定
                Case Else
                    Me.Size = preferred
            End Select
        Finally
            正在更新尺寸 = False
        End Try
    End Sub

#End Region

#Region "边框属性"

    Private 边框颜色 As Color = Color.Gray
    ''' <summary>边框颜色。</summary>
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
    ''' <summary>边框宽度（逻辑像素，会随 DPI 缩放）。</summary>
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
    ''' <summary>边框圆角半径（逻辑像素，会随 DPI 缩放）；0 表示直角。</summary>
    <Category("LakeUI"), Description("边框圆角半径"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property BorderRadius As Integer
        Get
            Return 边框圆角半径
        End Get
        Set(value As Integer)
            SetValue(边框圆角半径, value)
        End Set
    End Property

    Private 禁用时遮罩颜色 As Color = Color.FromArgb(120, 0, 0, 0)
    <Category("LakeUI"), Description("禁用（Enabled = False）时覆盖在主体区域上的遮罩颜色（受圆角裁剪，不影响圆角外的透明区域）。"), DefaultValue(GetType(Color), "120, 0, 0, 0"), Browsable(True)>
    Public Property DisabledOverlayColor As Color
        Get
            Return 禁用时遮罩颜色
        End Get
        Set(value As Color)
            SetValue(禁用时遮罩颜色, value)
        End Set
    End Property

#End Region

#Region "文本属性"

    ''' <summary>控件显示的文本，可包含 HTML 颜色/字体标记。当文本不含 '&lt;' 与 '&amp;' 时会走纯文本快速绘制路径。</summary>
    <Category("LakeUI"), Description("HTML 颜色标记文本"), DefaultValue(GetType(String), ""), Browsable(True), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
    Public Overrides Property Text As String
        Get
            Return MyBase.Text
        End Get
        Set(value As String)
            If MyBase.Text <> value Then
                MyBase.Text = value
                使缓存失效(False)
                更新自动尺寸()
                Me.Invalidate()
            End If
        End Set
    End Property
    Private Function ShouldSerializeText() As Boolean
        Return Not String.IsNullOrEmpty(Text)
    End Function
    ''' <summary>将 <see cref="Text"/> 置为空字符串。</summary>
    Public Overrides Sub ResetText()
        Text = String.Empty
    End Sub

    Private 文本颜色 As Color = Color.Silver
    ''' <summary>默认文本颜色；标记中未显式指定颜色的片段使用此颜色。</summary>
    <Category("LakeUI"), Description("默认文本颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Overrides Property ForeColor As Color
        Get
            Return 文本颜色
        End Get
        Set(value As Color)
            SetValue(文本颜色, value)
        End Set
    End Property

    Private 文字对齐方位 As TextAlignEnum = TextAlignEnum.TopLeft

    Private Sub HtmlColorLabel_Load(sender As Object, e As EventArgs) Handles MyBase.Load

    End Sub

    ''' <summary>文字在控件内的整体对齐方位。</summary>
    Public Enum TextAlignEnum
        ''' <summary>左上。</summary>
        TopLeft
        ''' <summary>右上。</summary>
        TopRight
        ''' <summary>完全居中。</summary>
        Center
        ''' <summary>左下。</summary>
        BottomLeft
        ''' <summary>右下。</summary>
        BottomRight
        ''' <summary>左侧垂直居中。</summary>
        MiddleLeft
        ''' <summary>右侧垂直居中。</summary>
        MiddleRight
    End Enum
    ''' <summary>文字整体对齐方位。</summary>
    <Category("LakeUI"), Description("文字对齐方位：TopLeft 左上, TopRight 右上, Center 完全居中, BottomLeft 左下, BottomRight 右下, MiddleLeft 左侧居中, MiddleRight 右侧居中"), DefaultValue(GetType(TextAlignEnum), "TopLeft"), Browsable(True)>
    Public Property TextAlign As TextAlignEnum
        Get
            Return 文字对齐方位
        End Get
        Set(value As TextAlignEnum)
            SetValue(文字对齐方位, value)
        End Set
    End Property

    ''' <summary>同一行内不同字号文字的垂直对齐方式。</summary>
    Public Enum InlineAlignEnum
        ''' <summary>底部对齐。</summary>
        Bottom
        ''' <summary>垂直居中。</summary>
        Center
    End Enum

    Private 行内垂直对齐方式 As InlineAlignEnum = InlineAlignEnum.Bottom
    ''' <summary>同一行内不同字号文字的垂直对齐方式。</summary>
    <Category("LakeUI"), Description("行内不同字号文字的垂直对齐方式：Bottom 底部对齐，Center 垂直居中"), DefaultValue(GetType(InlineAlignEnum), "Bottom"), Browsable(True)>
    Public Property InlineVerticalAlign As InlineAlignEnum
        Get
            Return 行内垂直对齐方式
        End Get
        Set(value As InlineAlignEnum)
            SetValue(行内垂直对齐方式, value)
        End Set
    End Property

    Private 行距 As Integer = 0
    ''' <summary>多行文本间的额外行距（像素）；赋值会被修正为非负数。</summary>
    <Category("LakeUI"), Description("行与行之间的额外间距（像素），仅在多行时生效"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property LineSpacing As Integer
        Get
            Return 行距
        End Get
        Set(value As Integer)
            SetValue(行距, Math.Max(value, 0))
        End Set
    End Property

#End Region

#Region "禁用属性"

    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property AutoScroll As Boolean
        Get
            Return Nothing
        End Get
        Set(value As Boolean)
        End Set
    End Property

    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property AutoScrollMargin As Size
        Get
            Return Nothing
        End Get
        Set(value As Size)
        End Set
    End Property

    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property AutoScrollMinSize As Size
        Get
            Return Nothing
        End Get
        Set(value As Size)
        End Set
    End Property

    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property BorderStyle As BorderStyle
        Get
            Return Nothing
        End Get
        Set(value As BorderStyle)
        End Set
    End Property

    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property BackgroundImage As Image
        Get
            Return Nothing
        End Get
        Set(value As Image)
        End Set
    End Property

    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property BackgroundImageLayout As ImageLayout
        Get
            Return Nothing
        End Get
        Set(value As ImageLayout)
        End Set
    End Property

#End Region

End Class
