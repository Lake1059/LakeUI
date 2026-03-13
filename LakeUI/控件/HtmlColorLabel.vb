Imports System.ComponentModel
Imports System.Drawing.Drawing2D
Imports System.Text.RegularExpressions

''' <summary>
''' 支持 HTML 颜色标记的 Label 控件。
''' 仅解析颜色相关标记（font color / span style="color:..."），其余标记被剥离但保留内容文本。
''' 支持 HTML 命名颜色、十六进制 RGB、rgb()、rgba()、hsl() 颜色格式。
''' </summary>
Public Class HtmlColorLabel

#Region "构造"

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

#Region "HTML解析"

    Private Structure 文本片段
        Public 文本 As String
        Public 颜色 As Color
    End Structure

    Private Shared ReadOnly 标签正则 As New Regex("<(/?)\s*(\w+)([^>]*)>", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly fontColor正则 As New Regex("color\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly style属性正则 As New Regex("style\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly cssColor正则 As New Regex("(?:^|;\s*)color\s*:\s*([^;]+)", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly rgb正则 As New Regex("^rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)$", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly rgba正则 As New Regex("^rgba\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*([\d.]+)\s*\)$", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly hsl正则 As New Regex("^hsl\(\s*([\d.]+)\s*,\s*([\d.]+)%?\s*,\s*([\d.]+)%?\s*\)$", RegexOptions.IgnoreCase Or RegexOptions.Compiled)

    Private Function 解析为片段列表(html As String) As List(Of 文本片段)
        Dim 结果 As New List(Of 文本片段)
        If String.IsNullOrEmpty(html) Then Return 结果
        Dim 颜色栈 As New Stack(Of Color)
        颜色栈.Push(文本颜色)
        Dim 推色记录 As New Stack(Of Boolean)
        Dim 上次位置 As Integer = 0
        For Each m As Match In 标签正则.Matches(html)
            If m.Index > 上次位置 Then
                Dim t = 解码HTML实体(html.Substring(上次位置, m.Index - 上次位置))
                If t.Length > 0 Then 结果.Add(New 文本片段 With {.文本 = t, .颜色 = 颜色栈.Peek()})
            End If
            上次位置 = m.Index + m.Length
            Dim 是否闭合 = m.Groups(1).Value = "/"
            Dim 标签名 = m.Groups(2).Value.ToLowerInvariant()
            Dim 属性 = m.Groups(3).Value
            If 标签名 = "br" AndAlso Not 是否闭合 Then
                结果.Add(New 文本片段 With {.文本 = vbLf, .颜色 = 颜色栈.Peek()})
                Continue For
            End If
            If 是否闭合 Then
                If 推色记录.Count > 0 AndAlso 推色记录.Pop() Then
                    If 颜色栈.Count > 1 Then 颜色栈.Pop()
                End If
            Else
                Dim c = 提取标签颜色(标签名, 属性)
                If c.HasValue Then
                    颜色栈.Push(c.Value)
                    推色记录.Push(True)
                Else
                    推色记录.Push(False)
                End If
            End If
        Next
        If 上次位置 < html.Length Then
            Dim t = 解码HTML实体(html.Substring(上次位置))
            If t.Length > 0 Then 结果.Add(New 文本片段 With {.文本 = t, .颜色 = 颜色栈.Peek()})
        End If
        Return 结果
    End Function

    Private Function 提取标签颜色(标签名 As String, 属性 As String) As Color?
        If String.IsNullOrWhiteSpace(属性) Then Return Nothing
        Select Case 标签名
            Case "font"
                Dim m = fontColor正则.Match(属性)
                If m.Success Then
                    Dim 值 = If(m.Groups(1).Success AndAlso m.Groups(1).Length > 0, m.Groups(1).Value,
                              If(m.Groups(2).Success AndAlso m.Groups(2).Length > 0, m.Groups(2).Value,
                              m.Groups(3).Value))
                    Return 解析颜色值(值.Trim())
                End If
            Case "span", "div", "p"
                Dim sm = style属性正则.Match(属性)
                If sm.Success Then
                    Dim styleVal = If(sm.Groups(1).Success AndAlso sm.Groups(1).Length > 0, sm.Groups(1).Value, sm.Groups(2).Value)
                    Dim cm = cssColor正则.Match(styleVal)
                    If cm.Success Then Return 解析颜色值(cm.Groups(1).Value.Trim())
                End If
        End Select
        Return Nothing
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
        Return text.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", """").Replace("&apos;", "'")
    End Function

#End Region

#Region "绘制"

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim 是否有圆角 As Boolean = 边框圆角半径 > 0
        Dim 极限矩形区域 As New RectangleF(0, 0, Me.Width - 1, Me.Height - 1)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * DpiScale() / 2.0F
            极限矩形区域.Inflate(-half, -half)
        End If
        Dim 内容矩形区域 As New RectangleF(
            极限矩形区域.X + Me.Padding.Left,
            极限矩形区域.Y + Me.Padding.Top,
            极限矩形区域.Width - Me.Padding.Horizontal,
            极限矩形区域.Height - Me.Padding.Vertical)
        Dim _ssaa As Integer = If(Class1.GlobalSSAA > 1, Class1.GlobalSSAA, 超采样倍率)
        If _ssaa > 1 Then
            Using bmp As New Bitmap(Me.Width * _ssaa, Me.Height * _ssaa)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.ScaleTransform(_ssaa, _ssaa)
                    绘制图形内容(g, 是否有圆角, 极限矩形区域)
                End Using
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                e.Graphics.DrawImage(bmp, 0, 0, Me.Width, Me.Height)
            End Using
        Else
            绘制图形内容(e.Graphics, 是否有圆角, 极限矩形区域)
        End If
        绘制文本内容(e.Graphics, 内容矩形区域)
        If Not Enabled Then
            Using brush As New SolidBrush(Color.FromArgb(120, 0, 0, 0))
                e.Graphics.FillRectangle(brush, 0, 0, Me.Width, Me.Height)
            End Using
        End If
    End Sub

    Private Sub 绘制图形内容(g As Graphics, 是否有圆角 As Boolean, 极限矩形区域 As RectangleF)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        g.InterpolationMode = InterpolationMode.HighQualityBicubic
        Dim s As Single = DpiScale()
        If 是否有圆角 Then
            Using path As GraphicsPath = RectangleRenderer.创建圆角矩形路径(极限矩形区域, 边框圆角半径 * s)
                RectangleRenderer.绘制圆角背景(g, path, 极限矩形区域, MyBase.BackColor, Color.Empty, Orientation.Vertical)
                RectangleRenderer.绘制圆角边框(g, path, 边框颜色, 边框宽度 * s)
            End Using
        Else
            RectangleRenderer.绘制矩形背景(g, 极限矩形区域, MyBase.BackColor, Color.Empty, Orientation.Vertical)
            RectangleRenderer.绘制矩形边框(g, 极限矩形区域, 边框颜色, 边框宽度 * s)
        End If
    End Sub

    Private Structure 渲染单元
        Public 文本 As String
        Public 颜色 As Color
        Public X偏移 As Integer
        Public 宽度 As Integer
    End Structure

    Private Structure 渲染行
        Public 单元列表 As List(Of 渲染单元)
        Public 行宽 As Integer
    End Structure

    Private Shared ReadOnly 文本测量格式 As TextFormatFlags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine

    Private Function 测量文本宽度(text As String) As Integer
        If String.IsNullOrEmpty(text) Then Return 0
        Return TextRenderer.MeasureText(text, Me.Font, New Size(Integer.MaxValue, Integer.MaxValue), 文本测量格式).Width
    End Function

    Private Function 计算文本布局(最大宽度 As Integer) As List(Of 渲染行)
        Dim 片段列表 = 解析为片段列表(MyBase.Text)
        Dim 行列表 As New List(Of 渲染行)
        If 片段列表.Count = 0 Then Return 行列表
        Dim 当前行 As New 渲染行 With {.单元列表 = New List(Of 渲染单元), .行宽 = 0}
        For Each 片段 In 片段列表
            Dim 行数组 = 片段.文本.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
            For 行索引 As Integer = 0 To 行数组.Length - 1
                If 行索引 > 0 Then
                    行列表.Add(当前行)
                    当前行 = New 渲染行 With {.单元列表 = New List(Of 渲染单元), .行宽 = 0}
                End If
                Dim 行文本 = 行数组(行索引)
                If 行文本.Length = 0 Then Continue For
                Dim 单元列表 = 拆分为可绘制单元(行文本)
                For Each 单元 In 单元列表
                    Dim 单元宽度 = 测量文本宽度(单元)
                    If 当前行.行宽 > 0 AndAlso 当前行.行宽 + 单元宽度 > 最大宽度 Then
                        行列表.Add(当前行)
                        当前行 = New 渲染行 With {.单元列表 = New List(Of 渲染单元), .行宽 = 0}
                    End If
                    If 单元宽度 > 最大宽度 AndAlso 单元.Length > 1 Then
                        For Each ch In 单元
                            Dim 字符文本 = ch.ToString()
                            Dim 字符宽度 = 测量文本宽度(字符文本)
                            If 当前行.行宽 > 0 AndAlso 当前行.行宽 + 字符宽度 > 最大宽度 Then
                                行列表.Add(当前行)
                                当前行 = New 渲染行 With {.单元列表 = New List(Of 渲染单元), .行宽 = 0}
                            End If
                            当前行.单元列表.Add(New 渲染单元 With {.文本 = 字符文本, .颜色 = 片段.颜色, .X偏移 = 当前行.行宽, .宽度 = 字符宽度})
                            当前行.行宽 += 字符宽度
                        Next
                    Else
                        当前行.单元列表.Add(New 渲染单元 With {.文本 = 单元, .颜色 = 片段.颜色, .X偏移 = 当前行.行宽, .宽度 = 单元宽度})
                        当前行.行宽 += 单元宽度
                    End If
                Next
            Next
        Next
        行列表.Add(当前行)
        Return 行列表
    End Function

    Private Sub 绘制文本内容(g As Graphics, 内容矩形区域 As RectangleF)
        Dim 内容区域 As Rectangle = Rectangle.Round(内容矩形区域)
        Dim 行列表 = 计算文本布局(内容区域.Width)
        If 行列表.Count = 0 Then Return
        Dim 行高 As Integer = TextRenderer.MeasureText("A", Me.Font, New Size(Integer.MaxValue, Integer.MaxValue), 文本测量格式).Height
        For 行索引 As Integer = 0 To 行列表.Count - 1
            Dim 行 = 行列表(行索引)
            Dim Y As Integer = 内容区域.Y + 行索引 * 行高
            Dim 对齐偏移 As Integer
            Select Case 文字对齐方位
                Case TextAlignEnum.Center
                    对齐偏移 = (内容区域.Width - 行.行宽) \ 2
                Case TextAlignEnum.Right
                    对齐偏移 = 内容区域.Width - 行.行宽
                Case Else
                    对齐偏移 = 0
            End Select
            For Each 单元 In 行.单元列表
                Dim 绘制位置 As New Point(内容区域.X + 对齐偏移 + 单元.X偏移, Y)
                TextRenderer.DrawText(g, 单元.文本, Me.Font, 绘制位置, 单元.颜色, 文本测量格式)
            Next
        Next
    End Sub

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
        Dim code = AscW(ch)
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
            更新自动尺寸()
            Me.Invalidate()
        End If
    End Sub

    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function

    Private 超采样倍率 As Integer = 1
    <Category("LakeUI"), Description(Class1.超采样抗锯齿描述词), DefaultValue(GetType(Class1.SuperSamplingScaleEnum), "OFF"), Browsable(True)>
    Public Property SuperSamplingScale As Class1.SuperSamplingScaleEnum
        Get
            Return 超采样倍率
        End Get
        Set(value As Class1.SuperSamplingScaleEnum)
            SetValue(超采样倍率, value)
        End Set
    End Property

    Private 启用自动尺寸 As Boolean = False
    Private 自动尺寸前的大小 As Size = Size.Empty
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
        Dim 行高 = TextRenderer.MeasureText("A", Me.Font, New Size(Integer.MaxValue, Integer.MaxValue), 文本测量格式).Height
        Dim 内容最大宽度 As Integer = 0
        For Each 行 In 行列表
            If 行.行宽 > 内容最大宽度 Then 内容最大宽度 = 行.行宽
        Next
        Dim 新宽度 = 内容最大宽度 + Me.Padding.Horizontal + 边框额外
        Dim 新高度 = Math.Max(行列表.Count, 1) * 行高 + Me.Padding.Vertical + 边框额外
        If Me.MaximumSize.Width > 0 Then 新宽度 = Math.Min(新宽度, Me.MaximumSize.Width)
        If Me.MaximumSize.Height > 0 Then 新高度 = Math.Min(新高度, Me.MaximumSize.Height)
        If Me.MinimumSize.Width > 0 Then 新宽度 = Math.Max(新宽度, Me.MinimumSize.Width)
        If Me.MinimumSize.Height > 0 Then 新高度 = Math.Max(新高度, Me.MinimumSize.Height)
        Return New Size(新宽度, 新高度)
    End Function

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        If Not 启用自动尺寸 Then
            自动尺寸前的大小 = Me.Size
        End If
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        更新自动尺寸()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
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

    Private Sub 更新自动尺寸()
        If Not 启用自动尺寸 OrElse Not IsHandleCreated Then Return
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
    End Sub

#End Region

#Region "边框属性"

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

#Region "文本属性"

    <Category("LakeUI"), Description("HTML 颜色标记文本"), DefaultValue(GetType(String), ""), Browsable(True), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
    Public Overrides Property Text As String
        Get
            Return MyBase.Text
        End Get
        Set(value As String)
            SetValue(MyBase.Text, value)
        End Set
    End Property
    Private Function ShouldSerializeText() As Boolean
        Return Not String.IsNullOrEmpty(Text)
    End Function
    Public Overrides Sub ResetText()
        Text = String.Empty
    End Sub

    Private 文本颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("默认文本颜色"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Overrides Property ForeColor As Color
        Get
            Return 文本颜色
        End Get
        Set(value As Color)
            SetValue(文本颜色, value)
        End Set
    End Property

    Private 文字对齐方位 As TextAlignEnum = TextAlignEnum.Left
    Public Enum TextAlignEnum
        Left
        Center
        Right
    End Enum
    <Category("LakeUI"), Description("文字对齐方位"), DefaultValue(GetType(TextAlignEnum), "Left"), Browsable(True)>
    Public Property TextAlign As TextAlignEnum
        Get
            Return 文字对齐方位
        End Get
        Set(value As TextAlignEnum)
            SetValue(文字对齐方位, value)
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