Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Threading

Public Class ModernColorDialog

#Region "公共属性"

    ''' <summary>
    ''' 获取或设置用户选择的颜色。
    ''' </summary>
    <ComponentModel.DesignerSerializationVisibility(ComponentModel.DesignerSerializationVisibility.Hidden)>
    <ComponentModel.Browsable(False)>
    Public Property SelectedColor As Color = Color.White

    ''' <summary>
    ''' 色域图渲染精度步长。1 = 逐像素（最高质量），2 = 2×2 块，4 = 4×4 块。值越大速度越快。
    ''' </summary>
    <ComponentModel.Category("LakeUI")>
    <ComponentModel.Description("色域图渲染精度步长，1=最高质量，2-4=更快")>
    <ComponentModel.DefaultValue(2)>
    Public Property RenderQuality As Integer
        Get
            Return _renderStep
        End Get
        Set(value As Integer)
            value = Math.Clamp(value, 1, 8)
            If _renderStep <> value Then
                _renderStep = value
                StartBackgroundRender()
            End If
        End Set
    End Property

#End Region

#Region "私有字段"

    Private _suppressSync As Boolean = False
    Private _renderStep As Integer = 2

    ' ── 色域图 ──
    Private _chromaticityBitmap As Bitmap = Nothing
    Private _markerX As Double = 0.3127  ' 当前标记的 CIE xy（默认 D65 白点）
    Private _markerY As Double = 0.329
    Private _renderCts As CancellationTokenSource = Nothing

    ' ── HTML 基本颜色表 ──
    Private ReadOnly _htmlColors As New List(Of KeyValuePair(Of String, Color))
    Private ReadOnly _colorSwatchImages As New List(Of Bitmap)

    ' ── 标签拖动调节 ──
    Private _dragLabel As Label = Nothing
    Private _dragTextBox As ModernTextBox = Nothing
    Private _dragStartY As Integer
    Private _dragStartValue As Integer
    Private _dragMin As Integer
    Private _dragMax As Integer

    ' ── 收藏夹 ──
    Private _favButtons() As ModernButton
    Private ReadOnly _favoriteColors(9) As Color
    Private ReadOnly _favoriteSet(9) As Boolean

    ' ── 全屏取色器 ──
    Private _eyeDropperActive As Boolean = False
    Private _eyeDropperTimer As System.Windows.Forms.Timer = Nothing

    ' ── CIE 1931 光谱轨迹闭合多边形 (380nm – 700nm, 5nm 步进 + 紫线闭合) ──
    Private Shared ReadOnly _polyX As Double()
    Private Shared ReadOnly _polyY As Double()
    Private Shared ReadOnly _polyN As Integer

    Shared Sub New()
        Dim sx() As Double = {
            0.1741, 0.174, 0.1738, 0.1736, 0.1733, 0.173, 0.1726, 0.1721, 0.1714, 0.1703,
            0.1689, 0.1669, 0.1644, 0.1611, 0.1566, 0.151, 0.144, 0.1355, 0.1241, 0.1096,
            0.0913, 0.0687, 0.0454, 0.0235, 0.0082, 0.0039, 0.0139, 0.0389, 0.0743, 0.1142,
            0.1547, 0.1929, 0.2296, 0.2658, 0.3016, 0.3373, 0.3731, 0.4087, 0.4441, 0.4788,
            0.5125, 0.5448, 0.5752, 0.6029, 0.627, 0.6482, 0.6658, 0.6801, 0.6915, 0.7006,
            0.7079, 0.714, 0.719, 0.723, 0.726, 0.7283, 0.73, 0.7311, 0.732, 0.7327,
            0.7334, 0.734, 0.7344, 0.7346, 0.7347
        }
        Dim sy() As Double = {
            0.005, 0.005, 0.0049, 0.0049, 0.0048, 0.0048, 0.0048, 0.0048, 0.0051, 0.0058,
            0.0069, 0.0086, 0.0109, 0.0138, 0.0177, 0.0227, 0.0297, 0.0399, 0.0578, 0.0868,
            0.1327, 0.2007, 0.295, 0.4127, 0.5384, 0.6548, 0.7502, 0.812, 0.8338, 0.8262,
            0.8059, 0.7816, 0.7543, 0.7243, 0.6923, 0.6589, 0.6245, 0.5896, 0.5547, 0.5202,
            0.4866, 0.4544, 0.4242, 0.3965, 0.3725, 0.3514, 0.334, 0.3197, 0.3083, 0.2993,
            0.292, 0.2859, 0.2809, 0.277, 0.274, 0.2717, 0.27, 0.2689, 0.268, 0.2673,
            0.2666, 0.266, 0.2656, 0.2654, 0.2653
        }
        _polyN = sx.Length + 1
        ReDim _polyX(_polyN - 1)
        ReDim _polyY(_polyN - 1)
        Array.Copy(sx, _polyX, sx.Length)
        Array.Copy(sy, _polyY, sy.Length)
        _polyX(sx.Length) = sx(0)
        _polyY(sy.Length) = sy(0)
    End Sub

#End Region

#Region "初始化"

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)

        ' 初始化 HTML 基本颜色表（按 VS Web 色彩顺序排列，带颜色色块图标）
        InitHtmlColors()
        PopulateColorList()

        ' 初始化收藏夹
        For i = 0 To 9 : _favoriteColors(i) = Color.Black : Next

        ' 从 SelectedColor 初始化界面
        ApplyColorToUI(SelectedColor, True)

        ' 绑定事件
        AddHandler PictureBox1.Paint, AddressOf PictureBox1_Paint
        AddHandler PictureBox1.MouseDown, AddressOf PictureBox1_MouseInteract
        AddHandler PictureBox1.MouseMove, AddressOf PictureBox1_MouseInteract
        AddHandler PictureBox1.Resize, AddressOf PictureBox1_Resize

        AddHandler ModernTextBox2.TextChanged, AddressOf RGB_TextChanged
        AddHandler ModernTextBox3.TextChanged, AddressOf RGB_TextChanged
        AddHandler ModernTextBox4.TextChanged, AddressOf RGB_TextChanged
        AddHandler ModernTextBox5.TextChanged, AddressOf HSL_TextChanged
        AddHandler ModernTextBox6.TextChanged, AddressOf HSL_TextChanged
        AddHandler ModernTextBox7.TextChanged, AddressOf HSL_TextChanged
        AddHandler ModernTextBox8.TextChanged, AddressOf Alpha_TextChanged
        AddHandler ModernTextBox9.TextChanged, AddressOf Hex_TextChanged
        AddHandler ModernTextBox1.TextChanged, AddressOf HtmlSearch_TextChanged
        AddHandler ModernListBox1.SelectedIndexChanged, AddressOf HtmlList_SelectedIndexChanged
        AddHandler ModernButton2.Click, AddressOf OkButton_Click
        AddHandler ModernButton1.Click, AddressOf CancelButton_Click
        AddHandler ModernButton13.Click, AddressOf EyeDropperButton_Click

        ' 鼠标滚轮调节数字（R, G, B, H, S, L, A）
        AddHandler ModernTextBox2.MouseWheel, Sub(s, ev) AdjustTextBoxByDelta(ModernTextBox2, ev.Delta, 0, 255)
        AddHandler ModernTextBox3.MouseWheel, Sub(s, ev) AdjustTextBoxByDelta(ModernTextBox3, ev.Delta, 0, 255)
        AddHandler ModernTextBox4.MouseWheel, Sub(s, ev) AdjustTextBoxByDelta(ModernTextBox4, ev.Delta, 0, 255)
        AddHandler ModernTextBox5.MouseWheel, Sub(s, ev) AdjustTextBoxByDelta(ModernTextBox5, ev.Delta, 0, 359)
        AddHandler ModernTextBox6.MouseWheel, Sub(s, ev) AdjustTextBoxByDelta(ModernTextBox6, ev.Delta, 0, 100)
        AddHandler ModernTextBox7.MouseWheel, Sub(s, ev) AdjustTextBoxByDelta(ModernTextBox7, ev.Delta, 0, 100)
        AddHandler ModernTextBox8.MouseWheel, Sub(s, ev) AdjustTextBoxByDelta(ModernTextBox8, ev.Delta, 0, 255)

        ' 标签拖动调节（按住拖动，往上增加）
        SetupLabelDrag(Label9, ModernTextBox2, 0, 255)
        SetupLabelDrag(Label10, ModernTextBox3, 0, 255)
        SetupLabelDrag(Label13, ModernTextBox4, 0, 255)
        SetupLabelDrag(Label15, ModernTextBox5, 0, 359)
        SetupLabelDrag(Label17, ModernTextBox6, 0, 100)
        SetupLabelDrag(Label19, ModernTextBox7, 0, 100)
        SetupLabelDrag(Label21, ModernTextBox8, 0, 255)

        _favButtons = {
            ModernButton3, ModernButton4, ModernButton5,
            ModernButton6, ModernButton7, ModernButton8,
            ModernButton9, ModernButton10, ModernButton11,
            ModernButton12
        }
        For i = 0 To _favButtons.Length - 1
            Dim idx As Integer = i
            AddHandler _favButtons(i).MouseDown, Sub(s As Object, ev As MouseEventArgs)
                                                     FavoriteButton_Click(idx, ev.Button)
                                                 End Sub
        Next

        ' 后台生成色域图
        StartBackgroundRender()
    End Sub

    Private Sub InitHtmlColors()
        _htmlColors.Clear()
        Dim names() As String = {
            "AliceBlue", "AntiqueWhite", "Aqua", "Aquamarine", "Azure",
            "Beige", "Bisque", "Black", "BlanchedAlmond", "Blue",
            "BlueViolet", "Brown", "BurlyWood", "CadetBlue", "Chartreuse",
            "Chocolate", "Coral", "CornflowerBlue", "Cornsilk", "Crimson",
            "Cyan", "DarkBlue", "DarkCyan", "DarkGoldenrod", "DarkGray",
            "DarkGreen", "DarkKhaki", "DarkMagenta", "DarkOliveGreen", "DarkOrange",
            "DarkOrchid", "DarkRed", "DarkSalmon", "DarkSeaGreen", "DarkSlateBlue",
            "DarkSlateGray", "DarkTurquoise", "DarkViolet", "DeepPink", "DeepSkyBlue",
            "DimGray", "DodgerBlue", "Firebrick", "FloralWhite", "ForestGreen",
            "Fuchsia", "Gainsboro", "GhostWhite", "Gold", "Goldenrod",
            "Gray", "Green", "GreenYellow", "Honeydew", "HotPink",
            "IndianRed", "Indigo", "Ivory", "Khaki", "Lavender",
            "LavenderBlush", "LawnGreen", "LemonChiffon", "LightBlue", "LightCoral",
            "LightCyan", "LightGoldenrodYellow", "LightGray", "LightGreen", "LightPink",
            "LightSalmon", "LightSeaGreen", "LightSkyBlue", "LightSlateGray", "LightSteelBlue",
            "LightYellow", "Lime", "LimeGreen", "Linen", "Magenta",
            "Maroon", "MediumAquamarine", "MediumBlue", "MediumOrchid", "MediumPurple",
            "MediumSeaGreen", "MediumSlateBlue", "MediumSpringGreen", "MediumTurquoise", "MediumVioletRed",
            "MidnightBlue", "MintCream", "MistyRose", "Moccasin", "NavajoWhite",
            "Navy", "OldLace", "Olive", "OliveDrab", "Orange",
            "OrangeRed", "Orchid", "PaleGoldenrod", "PaleGreen", "PaleTurquoise",
            "PaleVioletRed", "PapayaWhip", "PeachPuff", "Peru", "Pink",
            "Plum", "PowderBlue", "Purple", "Red", "RosyBrown",
            "RoyalBlue", "SaddleBrown", "Salmon", "SandyBrown", "SeaGreen",
            "SeaShell", "Sienna", "Silver", "SkyBlue", "SlateBlue",
            "SlateGray", "Snow", "SpringGreen", "SteelBlue", "Tan",
            "Teal", "Thistle", "Tomato", "Turquoise", "Violet",
            "Wheat", "White", "WhiteSmoke", "Yellow", "YellowGreen"
        }
        For Each n In names
            Dim c = Color.FromName(n)
            If c.IsKnownColor Then
                _htmlColors.Add(New KeyValuePair(Of String, Color)(n, c))
            End If
        Next

        ' 按 Visual Studio Web 颜色顺序排序：色相 → 饱和度 → 亮度
        _htmlColors.Sort(Function(a, b)
                             Dim ca = a.Value, cb = b.Value
                             Dim cmp = ca.GetHue().CompareTo(cb.GetHue())
                             If cmp <> 0 Then Return cmp
                             cmp = ca.GetSaturation().CompareTo(cb.GetSaturation())
                             If cmp <> 0 Then Return cmp
                             Return ca.GetBrightness().CompareTo(cb.GetBrightness())
                         End Function)
    End Sub

    ''' <summary>
    ''' 将 _htmlColors 填充到 ListBox 并为每项生成颜色色块图标。
    ''' </summary>
    Private Sub PopulateColorList()
        ' 清除旧图标
        For Each img In _colorSwatchImages
            img.Dispose()
        Next
        _colorSwatchImages.Clear()
        ModernListBox1.ItemIcons.Clear()
        ModernListBox1.Items.Clear()

        Dim sz = ModernListBox1.IconSize.Height
        If sz < 4 Then sz = 16

        For Each kv In _htmlColors
            ModernListBox1.Items.Add(kv.Key)
            Dim swatch = CreateColorSwatch(kv.Value, sz)
            _colorSwatchImages.Add(swatch)
            ModernListBox1.ItemIcons.Add(New ModernListBox.IconEntry(kv.Key, swatch))
        Next
    End Sub

    ''' <summary>
    ''' 创建一个正方形纯色色块位图（带 1px 边框）。
    ''' </summary>
    Private Shared Function CreateColorSwatch(c As Color, size As Integer) As Bitmap
        Dim bmp As New Bitmap(size, size, Imaging.PixelFormat.Format32bppArgb)
        Using g = Graphics.FromImage(bmp)
            Using brush As New SolidBrush(c)
                g.FillRectangle(brush, 0, 0, size, size)
            End Using
            Dim borderColor = If(c.GetBrightness() > 0.85F,
                Color.FromArgb(100, 140, 140, 140),
                Color.FromArgb(60, 200, 200, 200))
            Using pen As New Pen(borderColor, 1)
                g.DrawRectangle(pen, 0, 0, size - 1, size - 1)
            End Using
        End Using
        Return bmp
    End Function

#End Region

#Region "色域图绘制（后台线程）"

    ''' <summary>
    ''' 取消正在进行的渲染并启动新的后台渲染。
    ''' </summary>
    Private Sub StartBackgroundRender()
        Dim w As Integer = PictureBox1.ClientSize.Width
        Dim h As Integer = PictureBox1.ClientSize.Height
        If w < 10 OrElse h < 10 Then Return

        _renderCts?.Cancel()
        _renderCts = New CancellationTokenSource()
        Dim token = _renderCts.Token
        Dim renderStep = _renderStep

        Task.Run(
            Sub()
                Dim bmp = RenderChromaticityBitmap(w, h, renderStep, token)
                If token.IsCancellationRequested Then
                    bmp?.Dispose()
                    Return
                End If
                If bmp Is Nothing Then Return
                Try
                    Me.BeginInvoke(
                        Sub()
                            Dim old = _chromaticityBitmap
                            _chromaticityBitmap = bmp
                            old?.Dispose()
                            PictureBox1.Invalidate()
                        End Sub)
                Catch
                    bmp.Dispose()
                End Try
            End Sub)
    End Sub

    ''' <summary>
    ''' 纯计算：在后台线程中构建 CIE 1931 色度图位图。
    ''' 使用「亮度归一化」算法：先按 max(R,G,B) 归一化以保持色度比例，再钳位负值。
    ''' 这与标准参考实现一致，确保蓝/紫区域的色彩过渡正确。
    ''' </summary>
    Private Shared Function RenderChromaticityBitmap(w As Integer, h As Integer,
                                                      renderStep As Integer,
                                                      token As CancellationToken) As Bitmap
        Dim bmp As New Bitmap(w, h, PixelFormat.Format32bppArgb)
        Dim bmpData = bmp.LockBits(New Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
        Dim stride = bmpData.Stride
        Dim scan0 = bmpData.Scan0
        Dim bgArgb As Integer = Color.FromArgb(255, 36, 36, 36).ToArgb()

        ' 批量填充背景
        Dim bgLine(w - 1) As Integer
        Array.Fill(bgLine, bgArgb)
        For py = 0 To h - 1
            Runtime.InteropServices.Marshal.Copy(bgLine, 0, IntPtr.Add(scan0, py * stride), w)
        Next

        ' 按 step 填充色域内像素
        For py = 0 To h - 1 Step renderStep
            If token.IsCancellationRequested Then
                bmp.UnlockBits(bmpData)
                Return Nothing
            End If
            For px = 0 To w - 1 Step renderStep
                Dim cieXY = PixelToCIExy(px, py, w, h)
                Dim cx = cieXY.Item1
                Dim cy = cieXY.Item2
                If cy < 0.001 Then Continue For

                If Not PointInPolygon(cx, cy, _polyX, _polyY, _polyN) Then Continue For

                ' CIE xy → XYZ，固定 Y = 1
                Dim cX2 = cx / cy
                Dim cZ = (1.0 - cx - cy) / cy

                ' XYZ → 线性 sRGB（D65 矩阵）
                Dim rl = 3.2404542 * cX2 - 1.5371385 - 0.4985314 * cZ
                Dim gl = -0.969266 * cX2 + 1.8760108 + 0.041556 * cZ
                Dim bl = 0.0556434 * cX2 - 0.2040259 + 1.0572252 * cZ

                ' ★ 关键：亮度归一化 ── 先除以 max 使最亮通道 = 1，保持色度比例
                Dim maxC = Math.Max(rl, Math.Max(gl, bl))
                If maxC > 0 Then
                    rl /= maxC
                    gl /= maxC
                    bl /= maxC
                End If

                ' 钳位色域外的负值
                If rl < 0 Then rl = 0
                If gl < 0 Then gl = 0
                If bl < 0 Then bl = 0

                ' sRGB gamma 校正
                rl = SRGBGamma(rl)
                gl = SRGBGamma(gl)
                bl = SRGBGamma(bl)

                Dim ri = CInt(Math.Clamp(rl * 255, 0, 255))
                Dim gi = CInt(Math.Clamp(gl * 255, 0, 255))
                Dim bi = CInt(Math.Clamp(bl * 255, 0, 255))
                Dim argb = Color.FromArgb(255, ri, gi, bi).ToArgb()

                ' 填充 step×step 块
                Dim endY = Math.Min(py + renderStep - 1, h - 1)
                Dim endX = Math.Min(px + renderStep - 1, w - 1)
                For fy = py To endY
                    For fx = px To endX
                        Runtime.InteropServices.Marshal.WriteInt32(scan0, fy * stride + fx * 4, argb)
                    Next
                Next
            Next
        Next

        bmp.UnlockBits(bmpData)
        If token.IsCancellationRequested Then Return Nothing

        ' 在位图上描绘光谱轨迹边界线（抗锯齿）
        Dim polyPoints(_polyN - 2) As PointF
        For i = 0 To _polyN - 2
            polyPoints(i) = CIExyToPixel(_polyX(i), _polyY(i), w, h)
        Next
        Using g = Graphics.FromImage(bmp)
            g.SmoothingMode = SmoothingMode.AntiAlias
            Using pen As New Pen(Color.FromArgb(140, 200, 200, 200), 1.0F)
                g.DrawPolygon(pen, polyPoints)
            End Using
        End Using

        Return bmp
    End Function

    Private Sub PictureBox1_Paint(sender As Object, e As PaintEventArgs)
        If _chromaticityBitmap IsNot Nothing Then
            e.Graphics.DrawImage(_chromaticityBitmap, 0, 0)
        End If
        DrawCrosshairMarker(e.Graphics)
    End Sub

    ''' <summary>
    ''' 在色域图上绘制自动反色十字线标记。
    ''' </summary>
    Private Sub DrawCrosshairMarker(g As Graphics)
        Dim w = PictureBox1.ClientSize.Width
        Dim h = PictureBox1.ClientSize.Height
        Dim pt = CIExyToPixel(_markerX, _markerY, w, h)
        Dim px = CInt(Math.Round(pt.X))
        Dim py = CInt(Math.Round(pt.Y))

        ' 获取标记点处的颜色以计算反色
        Dim markerColor As Color = Color.White
        If _chromaticityBitmap IsNot Nothing AndAlso
           px >= 0 AndAlso px < _chromaticityBitmap.Width AndAlso
           py >= 0 AndAlso py < _chromaticityBitmap.Height Then
            markerColor = _chromaticityBitmap.GetPixel(px, py)
        End If

        Dim lum = 0.299 * markerColor.R + 0.587 * markerColor.G + 0.114 * markerColor.B
        Dim invColor As Color = If(lum > 128, Color.Black, Color.White)

        Dim crossSize As Integer = 12
        Using pen As New Pen(invColor, 1.5F)
            g.DrawLine(pen, px - crossSize, py, px - 4, py)
            g.DrawLine(pen, px + 4, py, px + crossSize, py)
            g.DrawLine(pen, px, py - crossSize, px, py - 4)
            g.DrawLine(pen, px, py + 4, px, py + crossSize)
        End Using

        Using pen As New Pen(Color.FromArgb(120, invColor), 1.0F)
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.DrawEllipse(pen, px - crossSize, py - crossSize, crossSize * 2, crossSize * 2)
        End Using
    End Sub

    Private Sub PictureBox1_Resize(sender As Object, e As EventArgs)
        StartBackgroundRender()
    End Sub

#End Region

#Region "色域图交互"

    Private Sub PictureBox1_MouseInteract(sender As Object, e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then PickColorFromChromaticity(e.X, e.Y)
    End Sub

    Private Sub PickColorFromChromaticity(px As Integer, py As Integer)
        Dim w = PictureBox1.ClientSize.Width
        Dim h = PictureBox1.ClientSize.Height
        If w < 10 OrElse h < 10 Then Return

        Dim xy = PixelToCIExy(px, py, w, h)
        _markerX = xy.Item1
        _markerY = xy.Item2

        ClampToGamut()

        ' CIE xy → XYZ (Y=1) → 归一化 sRGB
        Dim c As Color
        If _markerY > 0.001 Then
            Dim cX2 = _markerX / _markerY
            Dim cZ = (1.0 - _markerX - _markerY) / _markerY
            c = XYZtoSRGB(cX2, 1.0, cZ)
        Else
            c = Color.Black
        End If

        SelectedColor = Color.FromArgb(ParseCurrentAlpha(), c.R, c.G, c.B)
        ApplyColorToUI(SelectedColor, False) ' 不做 sRGB 往返以避免精度偏移
    End Sub

    Private Sub ClampToGamut()
        If PointInPolygon(_markerX, _markerY, _polyX, _polyY, _polyN) Then Return

        Dim bestDist As Double = Double.MaxValue
        Dim bestX As Double = _markerX
        Dim bestY As Double = _markerY

        For i = 0 To _polyN - 2
            Dim cp = ClosestPointOnSegment(
                _markerX, _markerY,
                _polyX(i), _polyY(i),
                _polyX(i + 1), _polyY(i + 1))
            Dim dx = _markerX - cp.Item1
            Dim dy = _markerY - cp.Item2
            Dim d = dx * dx + dy * dy
            If d < bestDist Then
                bestDist = d
                bestX = cp.Item1
                bestY = cp.Item2
            End If
        Next

        _markerX = bestX
        _markerY = bestY
    End Sub

#End Region

#Region "颜色同步"

    ''' <summary>
    ''' 将颜色应用到所有 UI 控件。
    ''' </summary>
    ''' <param name="updateMarker">是否从 RGB 反算 CIE xy 来更新标记位置。
    ''' 当由色域图点击触发时应为 False，避免 sRGB 往返精度损失导致准心偏移。</param>
    Private Sub ApplyColorToUI(c As Color, updateMarker As Boolean)
        If _suppressSync Then Return
        _suppressSync = True

        ' RGB
        ModernTextBox2.Text = c.R.ToString()
        ModernTextBox3.Text = c.G.ToString()
        ModernTextBox4.Text = c.B.ToString()

        ' Alpha
        ModernTextBox8.Text = c.A.ToString()

        ' HEX
        If c.A = 255 Then
            ModernTextBox9.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}"
        Else
            ModernTextBox9.Text = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}"
        End If

        ' HSL
        Dim hsl = RGBtoHSL(c.R, c.G, c.B)
        ModernTextBox5.Text = Math.Round(hsl.Item1).ToString()
        ModernTextBox6.Text = Math.Round(hsl.Item2).ToString()
        ModernTextBox7.Text = Math.Round(hsl.Item3).ToString()

        ' 更新色域图标记位置（仅当非色域图点击时）
        If updateMarker Then
            UpdateMarkerFromColor(c)
        End If

        ' 更新预览
        UpdatePreview(c)

        ' 匹配 HTML 颜色列表
        HighlightHtmlColor(c)

        PictureBox1.Invalidate()

        _suppressSync = False
    End Sub

    Private Sub UpdateMarkerFromColor(c As Color)
        Dim xyz = SRGBtoXYZ(c.R, c.G, c.B)
        Dim sum = xyz.Item1 + xyz.Item2 + xyz.Item3
        If sum > 0.0001 Then
            _markerX = xyz.Item1 / sum
            _markerY = xyz.Item2 / sum
        Else
            _markerX = 0.3127
            _markerY = 0.329
        End If
    End Sub

    Private Sub UpdatePreview(c As Color)
        ModernPanel2.BackColor1 = Color.FromArgb(c.R, c.G, c.B)
    End Sub

    Private Sub HighlightHtmlColor(c As Color)
        ' 必须搜索当前可见的 ListBox 项（可能已被搜索框过滤），而非 _htmlColors 全量索引
        For i = 0 To ModernListBox1.Items.Count - 1
            Dim hc = Color.FromName(ModernListBox1.Items(i))
            If hc.R = c.R AndAlso hc.G = c.G AndAlso hc.B = c.B Then
                ModernListBox1.SelectedIndex = i
                ModernListBox1.EnsureVisible(i)
                Return
            End If
        Next
        ModernListBox1.SelectedIndex = -1
    End Sub

    ' ── RGB 输入 ──
    Private Sub RGB_TextChanged(sender As Object, e As EventArgs)
        If _suppressSync Then Return
        Dim r, g, b As Integer
        If Not Integer.TryParse(ModernTextBox2.Text, r) Then Return
        If Not Integer.TryParse(ModernTextBox3.Text, g) Then Return
        If Not Integer.TryParse(ModernTextBox4.Text, b) Then Return
        SelectedColor = Color.FromArgb(ParseCurrentAlpha(),
            Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255))
        ApplyColorToUI(SelectedColor, True)
    End Sub

    ' ── HSL 输入 ──
    Private Sub HSL_TextChanged(sender As Object, e As EventArgs)
        If _suppressSync Then Return
        Dim h2, s, l As Double
        If Not Double.TryParse(ModernTextBox5.Text, h2) Then Return
        If Not Double.TryParse(ModernTextBox6.Text, s) Then Return
        If Not Double.TryParse(ModernTextBox7.Text, l) Then Return
        Dim rgb = HSLtoRGB(((h2 Mod 360) + 360) Mod 360, Math.Clamp(s, 0, 100), Math.Clamp(l, 0, 100))
        SelectedColor = Color.FromArgb(ParseCurrentAlpha(), rgb.Item1, rgb.Item2, rgb.Item3)
        ApplyColorToUI(SelectedColor, True)
    End Sub

    ' ── Alpha 输入 ──
    Private Sub Alpha_TextChanged(sender As Object, e As EventArgs)
        If _suppressSync Then Return
        Dim a As Integer
        If Not Integer.TryParse(ModernTextBox8.Text, a) Then Return
        a = Math.Clamp(a, 0, 255)
        SelectedColor = Color.FromArgb(a, SelectedColor.R, SelectedColor.G, SelectedColor.B)
        ApplyColorToUI(SelectedColor, True)
    End Sub

    ' ── HEX 输入 ──
    Private Sub Hex_TextChanged(sender As Object, e As EventArgs)
        If _suppressSync Then Return
        Dim hex = ModernTextBox9.Text.Trim().TrimStart("#"c)
        Try
            Dim c As Color
            If hex.Length = 6 Then
                Dim r = Convert.ToInt32(hex.Substring(0, 2), 16)
                Dim g = Convert.ToInt32(hex.Substring(2, 2), 16)
                Dim b = Convert.ToInt32(hex.Substring(4, 2), 16)
                c = Color.FromArgb(255, r, g, b)
            ElseIf hex.Length = 8 Then
                Dim a = Convert.ToInt32(hex.Substring(0, 2), 16)
                Dim r = Convert.ToInt32(hex.Substring(2, 2), 16)
                Dim g = Convert.ToInt32(hex.Substring(4, 2), 16)
                Dim b = Convert.ToInt32(hex.Substring(6, 2), 16)
                c = Color.FromArgb(a, r, g, b)
            ElseIf hex.Length = 3 Then
                Dim r = Convert.ToInt32(String.Concat(hex.AsSpan(0, 1), hex.AsSpan(0, 1)), 16)
                Dim g = Convert.ToInt32(String.Concat(hex.AsSpan(1, 1), hex.AsSpan(1, 1)), 16)
                Dim b = Convert.ToInt32(String.Concat(hex.AsSpan(2, 1), hex.AsSpan(2, 1)), 16)
                c = Color.FromArgb(255, r, g, b)
            Else
                Return
            End If
            SelectedColor = c
            ApplyColorToUI(c, True)
        Catch
        End Try
    End Sub

    ' ── HTML 颜色搜索 ──
    Private Sub HtmlSearch_TextChanged(sender As Object, e As EventArgs)
        If _suppressSync Then Return
        Dim filter = ModernTextBox1.Text.Trim()
        _suppressSync = True
        ModernListBox1.Items.Clear()
        If String.IsNullOrEmpty(filter) Then
            For Each kv In _htmlColors
                ModernListBox1.Items.Add(kv.Key)
            Next
        Else
            For Each kv In _htmlColors
                If kv.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) Then
                    ModernListBox1.Items.Add(kv.Key)
                End If
            Next
        End If
        _suppressSync = False
    End Sub

    ' ── HTML 颜色列表选择 ──
    Private Sub HtmlList_SelectedIndexChanged(sender As Object, e As EventArgs)
        If _suppressSync Then Return
        Dim selText As String = ModernListBox1.SelectedItem
        If selText Is Nothing Then Return
        Dim kv = _htmlColors.FirstOrDefault(Function(x) x.Key = selText)
        If kv.Key Is Nothing Then Return
        SelectedColor = Color.FromArgb(ParseCurrentAlpha(), kv.Value.R, kv.Value.G, kv.Value.B)
        ApplyColorToUI(SelectedColor, True)
    End Sub

    Private Function ParseCurrentAlpha() As Integer
        Dim a As Integer = 255
        Dim unused = Integer.TryParse(ModernTextBox8.Text, a)
        Return Math.Clamp(a, 0, 255)
    End Function

#End Region

#Region "滚轮与标签拖动调节"

    ''' <summary>
    ''' 鼠标滚轮调整文本框数值，每次固定 ±1。
    ''' </summary>
    Private Sub AdjustTextBoxByDelta(tb As ModernTextBox, wheelDelta As Integer, minVal As Integer, maxVal As Integer)
        Dim v As Integer
        If Not Integer.TryParse(tb.Text, v) Then Return
        Dim step1 = Math.Sign(wheelDelta)  ' +1 或 -1
        v = Math.Clamp(v + step1, minVal, maxVal)
        tb.Text = v.ToString()
    End Sub

    ''' <summary>
    ''' 为指定 Label 设置拖动调节：按住鼠标向上拖动增大数值，向下减小。
    ''' </summary>
    Private Sub SetupLabelDrag(lbl As Label, tb As ModernTextBox, minVal As Integer, maxVal As Integer)
        lbl.Cursor = Cursors.SizeNS
        AddHandler lbl.MouseDown, Sub(s, ev)
                                      If ev.Button <> MouseButtons.Left Then Return
                                      _dragLabel = lbl
                                      _dragTextBox = tb
                                      _dragStartY = ev.Y
                                      _dragMin = minVal
                                      _dragMax = maxVal
                                      Dim v As Integer
                                      If Integer.TryParse(tb.Text, v) Then _dragStartValue = v Else _dragStartValue = 0
                                      lbl.Capture = True
                                  End Sub
        AddHandler lbl.MouseMove, Sub(s, ev)
                                      If _dragLabel IsNot lbl Then Return
                                      Dim dy = _dragStartY - ev.Y  ' 往上为正
                                      Dim newVal = Math.Clamp(_dragStartValue + dy, _dragMin, _dragMax)
                                      _dragTextBox.Text = newVal.ToString()
                                  End Sub
        AddHandler lbl.MouseUp, Sub(s, ev)
                                    If _dragLabel IsNot lbl Then Return
                                    lbl.Capture = False
                                    _dragLabel = Nothing
                                    _dragTextBox = Nothing
                                End Sub
    End Sub

#End Region

#Region "收藏夹"

    Private Sub FavoriteButton_Click(index As Integer, button As MouseButtons)
        If index < 0 OrElse index > 9 Then Return
        Select Case button
            Case MouseButtons.Left
                If _favoriteSet(index) Then
                    SelectedColor = _favoriteColors(index)
                    ApplyColorToUI(SelectedColor, True)
                End If
            Case MouseButtons.Right
                _favoriteColors(index) = SelectedColor
                _favoriteSet(index) = True
                _favButtons(index).BackColor1 = Color.FromArgb(SelectedColor.R, SelectedColor.G, SelectedColor.B)
                Dim lum = 0.299 * SelectedColor.R + 0.587 * SelectedColor.G + 0.114 * SelectedColor.B
                _favButtons(index).ForeColor = If(lum > 128, Color.Black, Color.White)
            Case MouseButtons.Middle
                _favoriteColors(index) = Color.Black
                _favoriteSet(index) = False
                _favButtons(index).BackColor1 = Color.Black
                _favButtons(index).ForeColor = Color.Gray
        End Select
    End Sub

#End Region

#Region "全屏取色器"

    Private Sub EyeDropperButton_Click(sender As Object, e As EventArgs)
        ToggleEyeDropper()
    End Sub

    Private Sub ToggleEyeDropper()
        If _eyeDropperActive Then
            StopEyeDropper()
        Else
            StartEyeDropper()
        End If
    End Sub

    Private Sub StartEyeDropper()
        _eyeDropperActive = True
        ModernButton13.Text = "按 Esc 取消"

        If _eyeDropperTimer Is Nothing Then
            _eyeDropperTimer = New System.Windows.Forms.Timer With {.Interval = 100}
            AddHandler _eyeDropperTimer.Tick, AddressOf EyeDropperTimer_Tick
        End If
        _eyeDropperTimer.Start()
    End Sub

    Private Sub StopEyeDropper()
        _eyeDropperActive = False
        _eyeDropperTimer?.Stop()
        ModernButton13.Text = "取色器"
    End Sub

    Private Sub EyeDropperTimer_Tick(sender As Object, e As EventArgs)
        Dim pos = Cursor.Position
        Dim c = GetScreenPixelColor(pos.X, pos.Y)
        c = Color.FromArgb(ParseCurrentAlpha(), c.R, c.G, c.B)
        SelectedColor = c
        ApplyColorToUI(c, True)
    End Sub

    ''' <summary>
    ''' 截取屏幕上指定坐标处 1×1 像素的颜色。
    ''' </summary>
    Private Shared Function GetScreenPixelColor(x As Integer, y As Integer) As Color
        Using bmp As New Bitmap(1, 1, PixelFormat.Format32bppArgb)
            Using g = Graphics.FromImage(bmp)
                g.CopyFromScreen(x, y, 0, 0, New Size(1, 1))
            End Using
            Return bmp.GetPixel(0, 0)
        End Using
    End Function

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        If _eyeDropperActive AndAlso keyData = Keys.Escape Then
            StopEyeDropper()
            Return True
        End If
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

#End Region

#Region "确定 / 取消 / 清理"

    Private Sub OkButton_Click(sender As Object, e As EventArgs)
        DialogResult = DialogResult.OK : Close()
    End Sub

    Private Sub CancelButton_Click(sender As Object, e As EventArgs)
        DialogResult = DialogResult.Cancel : Close()
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        StopEyeDropper()
        _eyeDropperTimer?.Dispose()
        _renderCts?.Cancel()
        _renderCts?.Dispose()
        _chromaticityBitmap?.Dispose()
        For Each img In _colorSwatchImages : img.Dispose() : Next
        MyBase.OnFormClosed(e)
    End Sub

#End Region

#Region "坐标变换"

    ''' <summary>
    ''' CIE xy → 画布像素。x ∈ [0, 0.8]，y ∈ [0, 0.9]。
    ''' </summary>
    Private Shared Function CIExyToPixel(cx As Double, cy As Double, w As Integer, h As Integer) As PointF
        Const margin As Double = 0.08
        Dim px = CSng((cx / 0.8) * (1 - 2 * margin) * w + margin * w)
        Dim py = CSng((1.0 - cy / 0.9) * (1 - 2 * margin) * h + margin * h)
        Return New PointF(px, py)
    End Function

    ''' <summary>
    ''' 画布像素 → CIE xy。
    ''' </summary>
    Private Shared Function PixelToCIExy(px As Integer, py As Integer, w As Integer, h As Integer) As Tuple(Of Double, Double)
        Const margin As Double = 0.08
        Dim cx = ((px - margin * w) / ((1 - 2 * margin) * w)) * 0.8
        Dim cy = (1.0 - (py - margin * h) / ((1 - 2 * margin) * h)) * 0.9
        Return Tuple.Create(cx, cy)
    End Function

#End Region

#Region "颜色空间转换"

    ''' <summary>
    ''' CIE XYZ → sRGB (D65)。使用亮度归一化：先除以 max(R,G,B) 再钳位负值，
    ''' 确保输出颜色保持正确的色度比例。
    ''' </summary>
    Private Shared Function XYZtoSRGB(X As Double, Y As Double, Z As Double) As Color
        ' sRGB D65 矩阵
        Dim rl = 3.2404542 * X - 1.5371385 * Y - 0.4985314 * Z
        Dim gl = -0.969266 * X + 1.8760108 * Y + 0.041556 * Z
        Dim bl = 0.0556434 * X - 0.2040259 * Y + 1.0572252 * Z

        ' ★ 亮度归一化：按最大通道缩放，保持色度比例
        Dim maxC = Math.Max(rl, Math.Max(gl, bl))
        If maxC > 0 Then
            rl /= maxC
            gl /= maxC
            bl /= maxC
        End If

        ' 钳位色域外的负值
        If rl < 0 Then rl = 0
        If gl < 0 Then gl = 0
        If bl < 0 Then bl = 0

        ' Gamma 校正
        rl = SRGBGamma(rl)
        gl = SRGBGamma(gl)
        bl = SRGBGamma(bl)

        Dim r = CInt(Math.Clamp(rl * 255, 0, 255))
        Dim g = CInt(Math.Clamp(gl * 255, 0, 255))
        Dim b = CInt(Math.Clamp(bl * 255, 0, 255))
        Return Color.FromArgb(255, r, g, b)
    End Function

    Private Shared Function SRGBtoXYZ(r As Integer, g As Integer, b As Integer) As Tuple(Of Double, Double, Double)
        Dim rl = SRGBInverseGamma(r / 255.0)
        Dim gl = SRGBInverseGamma(g / 255.0)
        Dim bl = SRGBInverseGamma(b / 255.0)

        Dim X = 0.4124564 * rl + 0.3575761 * gl + 0.1804375 * bl
        Dim Y = 0.2126729 * rl + 0.7151522 * gl + 0.072175 * bl
        Dim Z = 0.0193339 * rl + 0.119192 * gl + 0.9503041 * bl
        Return Tuple.Create(X, Y, Z)
    End Function

    Private Shared Function SRGBGamma(c As Double) As Double
        If c <= 0.0031308 Then Return 12.92 * c
        Return 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055
    End Function

    Private Shared Function SRGBInverseGamma(c As Double) As Double
        If c <= 0.04045 Then Return c / 12.92
        Return Math.Pow((c + 0.055) / 1.055, 2.4)
    End Function

    Private Shared Function RGBtoHSL(r As Integer, g As Integer, b As Integer) As Tuple(Of Double, Double, Double)
        Dim rd = r / 255.0, gd = g / 255.0, bd = b / 255.0
        Dim max = Math.Max(rd, Math.Max(gd, bd))
        Dim min = Math.Min(rd, Math.Min(gd, bd))
        Dim l2 = (max + min) / 2.0
        Dim h2 As Double = 0, s2 As Double = 0

        If max <> min Then
            Dim d = max - min
            s2 = If(l2 > 0.5, d / (2.0 - max - min), d / (max + min))
            If max = rd Then
                h2 = (gd - bd) / d + If(gd < bd, 6, 0)
            ElseIf max = gd Then
                h2 = (bd - rd) / d + 2
            Else
                h2 = (rd - gd) / d + 4
            End If
            h2 *= 60
        End If
        Return Tuple.Create(h2, s2 * 100, l2 * 100)
    End Function

    Private Shared Function HSLtoRGB(h As Double, s As Double, l As Double) As Tuple(Of Integer, Integer, Integer)
        s /= 100.0
        l /= 100.0
        If s = 0 Then
            Dim v = CInt(Math.Clamp(l * 255, 0, 255))
            Return Tuple.Create(v, v, v)
        End If

        Dim q = If(l < 0.5, l * (1 + s), l + s - l * s)
        Dim p = 2 * l - q
        Dim hNorm = h / 360.0

        Dim r = HueToRGB(p, q, hNorm + 1.0 / 3.0)
        Dim g = HueToRGB(p, q, hNorm)
        Dim b = HueToRGB(p, q, hNorm - 1.0 / 3.0)
        Return Tuple.Create(
            CInt(Math.Clamp(r * 255, 0, 255)),
            CInt(Math.Clamp(g * 255, 0, 255)),
            CInt(Math.Clamp(b * 255, 0, 255)))
    End Function

    Private Shared Function HueToRGB(p As Double, q As Double, t As Double) As Double
        If t < 0 Then t += 1
        If t > 1 Then t -= 1
        If t < 1 / 6.0 Then Return p + (q - p) * 6 * t
        If t < 1 / 2.0 Then Return q
        If t < 2 / 3.0 Then Return p + (q - p) * (2.0 / 3.0 - t) * 6
        Return p
    End Function

#End Region

#Region "几何辅助"

    ''' <summary>
    ''' 射线法判断点是否在多边形内（使用预计算数组）。
    ''' </summary>
    Private Shared Function PointInPolygon(px As Double, py As Double,
                                           polyX() As Double, polyY() As Double,
                                           n As Integer) As Boolean
        Dim inside = False
        Dim j = n - 1
        For i = 0 To n - 1
            If ((polyY(i) > py) <> (polyY(j) > py)) AndAlso
               (px < (polyX(j) - polyX(i)) * (py - polyY(i)) / (polyY(j) - polyY(i)) + polyX(i)) Then
                inside = Not inside
            End If
            j = i
        Next
        Return inside
    End Function

    Private Shared Function ClosestPointOnSegment(px As Double, py As Double,
                                                   ax As Double, ay As Double,
                                                   bx As Double, by As Double) As Tuple(Of Double, Double)
        Dim dx = bx - ax, dy = by - ay
        Dim lenSq = dx * dx + dy * dy
        If lenSq < 0.000001 Then Return Tuple.Create(ax, ay)
        Dim t = ((px - ax) * dx + (py - ay) * dy) / lenSq
        t = Math.Clamp(t, 0.0, 1.0)
        Return Tuple.Create(ax + t * dx, ay + t * dy)
    End Function

#End Region

    Private Sub ModernColorDialog_SizeChanged(sender As Object, e As EventArgs) Handles Me.SizeChanged
        If Me.WindowState = FormWindowState.Minimized Then Exit Sub
        Panel4.Width = Panel4.Parent.Width * 0.4
    End Sub

End Class