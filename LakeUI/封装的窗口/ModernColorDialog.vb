Imports System.ComponentModel
Imports System.Drawing.Imaging
Imports System.Threading
Imports System.Globalization
Imports System.Numerics
Imports D2D = Vortice.Direct2D1
Public Class ModernColorDialog

#Region "公共属性"

    ''' <summary>
    ''' 获取或设置用户选择的颜色。
    ''' </summary>
    <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    <Browsable(False)>
    Public Property SelectedColor As Color = Color.White

    ''' <summary>
    ''' 色域图渲染精度步长。1 = 逐像素，2 = 2x2 块，4 = 4x4 块。值越大速度越快。
    ''' </summary>
    <Category("LakeUI")>
    <Description("色域图渲染精度步长，1=最高质量，2-4=更快")>
    <DefaultValue(1)>
    Public Property RenderQuality As Integer
        Get
            Return _renderStep
        End Get
        Set(value As Integer)
            value = Math.Clamp(value, 1, 8)
            If _renderStep = value Then Return
            _renderStep = value
            启动后台色域图渲染()
        End Set
    End Property

#End Region

#Region "共享状态"

    Private _suppressSync As Boolean
    Private _lastAppliedArgb As Integer = Integer.MinValue
    Private _renderStep As Integer = 1

    Private ReadOnly _chromaticityBitmapLock As New Object()
    Private _chromaticityBitmap As Bitmap
    Private ReadOnly _chromaticityBitmapCache As New D2DGlobals.D2DBitmapCache()
    Private _markerX As Double = 0.3127
    Private _markerY As Double = 0.329
    Private _renderCts As CancellationTokenSource
    Private _isClosing As Boolean

    Private ReadOnly _htmlColors As New List(Of KeyValuePair(Of String, Color))
    Private ReadOnly _htmlColorByName As New Dictionary(Of String, Color)(StringComparer.OrdinalIgnoreCase)

    Private Shared ReadOnly _favoriteColors(9) As Color
    Private Shared ReadOnly _favoriteSet(9) As Boolean

    Private _eyeDropperActive As Boolean
    Private _eyeDropperTimer As System.Windows.Forms.Timer

    Private Shared ReadOnly _polyX As Double()
    Private Shared ReadOnly _polyY As Double()
    Private Shared ReadOnly _polyN As Integer

    Private Const OpaqueAlphaMask As Integer = -16777216

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

#Region "生命周期"

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        初始化D2D颜色对话框()
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        显示后刷新D2D()
    End Sub

    Protected Overrides Sub OnVisibleChanged(e As EventArgs)
        MyBase.OnVisibleChanged(e)
        If Visible Then 显示后刷新D2D()
    End Sub

    Protected Overrides Sub OnResizeEnd(e As EventArgs)
        MyBase.OnResizeEnd(e)
        调整大小结束D2D()
    End Sub

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        If 处理D2D快捷键(msg, keyData) Then Return True
        If _eyeDropperActive AndAlso keyData = Keys.Escape Then
            停止取色器()
            Return True
        End If
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        _isClosing = True
        清理D2D颜色对话框()
        停止取色器(False)
        _eyeDropperTimer?.Dispose()

        Dim cts = _renderCts
        _renderCts = Nothing
        Try
            cts?.Cancel()
        Catch ex As ObjectDisposedException
        End Try
        cts?.Dispose()

        Dim oldBitmap As Bitmap = Nothing
        SyncLock _chromaticityBitmapLock
            oldBitmap = _chromaticityBitmap
            _chromaticityBitmap = Nothing
            _chromaticityBitmapCache.Dispose()
        End SyncLock
        oldBitmap?.Dispose()

        MyBase.OnFormClosed(e)
    End Sub

#End Region

#Region "HTML 颜色表"

    Private Sub 初始化Html颜色表()
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
            If c.IsKnownColor Then _htmlColors.Add(New KeyValuePair(Of String, Color)(n, c))
        Next

        _htmlColors.Sort(
            Function(a, b)
                Dim ca = a.Value
                Dim cb = b.Value
                Dim cmp = ca.GetHue().CompareTo(cb.GetHue())
                If cmp <> 0 Then Return cmp
                cmp = ca.GetSaturation().CompareTo(cb.GetSaturation())
                If cmp <> 0 Then Return cmp
                Return ca.GetBrightness().CompareTo(cb.GetBrightness())
            End Function)

        _htmlColorByName.Clear()
        For Each kv In _htmlColors
            _htmlColorByName(kv.Key) = kv.Value
        Next
    End Sub

#End Region

#Region "色域图渲染"

    ''' <summary>
    ''' 取消旧任务并启动新的后台色域图渲染；窗口只在新位图准备好后刷新色域脏区。
    ''' </summary>
    Private Sub 启动后台色域图渲染()
        If _isClosing OrElse IsDisposed OrElse Disposing Then Return

        Dim renderSize As Size = 获取D2D色域图渲染尺寸()
        Dim w As Integer = renderSize.Width
        Dim h As Integer = renderSize.Height
        If w < 10 OrElse h < 10 Then Return

        Dim oldCts = _renderCts
        _renderCts = New CancellationTokenSource()
        Try
            oldCts?.Cancel()
        Catch ex As ObjectDisposedException
        End Try
        oldCts?.Dispose()

        Dim token = _renderCts.Token
        Dim renderStep = _renderStep
        Task.Run(
            Sub()
                Dim bmp = 渲染色域图位图(w, h, renderStep, token)
                If token.IsCancellationRequested Then
                    bmp?.Dispose()
                    Return
                End If
                If bmp Is Nothing Then Return
                If _isClosing OrElse IsDisposed OrElse Disposing OrElse Not IsHandleCreated Then
                    bmp.Dispose()
                    Return
                End If

                Try
                    BeginInvoke(
                        Sub()
                            If _isClosing OrElse IsDisposed OrElse Disposing Then
                                bmp.Dispose()
                                Return
                            End If
                            Dim old As Bitmap = Nothing
                            SyncLock _chromaticityBitmapLock
                                old = _chromaticityBitmap
                                _chromaticityBitmapCache.Invalidate()
                                _chromaticityBitmap = bmp
                            End SyncLock
                            old?.Dispose()
                            刷新D2D色域图区域()
                        End Sub)
                Catch
                    bmp.Dispose()
                End Try
            End Sub)
    End Sub

    ''' <summary>
    ''' 纯计算方法：在后台线程中构建 CIE 1931 色度图位图，不触碰任何 UI/D2D 对象。
    ''' </summary>
    Private Shared Function 渲染色域图位图(w As Integer, h As Integer,
                                          renderStep As Integer,
                                          token As CancellationToken) As Bitmap
        Dim pixels(w * h - 1) As Integer
        Array.Fill(pixels, Color.Transparent.ToArgb())

        Dim rowMin(h - 1) As Integer
        Dim rowMax(h - 1) As Integer
        计算色域扫描跨度(w, h, rowMin, rowMax)

        For py = 0 To h - 1 Step renderStep
            If token.IsCancellationRequested Then Return Nothing
            Dim xLo = rowMin(py)
            Dim xHi = rowMax(py)
            If xLo > xHi Then Continue For

            For px = xLo To xHi Step renderStep
                If px < xLo OrElse px > xHi Then Continue For

                Dim cieXY = 像素中心转色度坐标(px, py, w, h)
                Dim cx = cieXY.Item1
                Dim cy = cieXY.Item2
                If cy < 0.001 Then Continue For

                Dim cX2 = cx / cy
                Dim cZ = (1.0 - cx - cy) / cy
                Dim rl = 3.2404542 * cX2 - 1.5371385 - 0.4985314 * cZ
                Dim gl = -0.969266 * cX2 + 1.8760108 + 0.041556 * cZ
                Dim bl = 0.0556434 * cX2 - 0.2040259 + 1.0572252 * cZ

                Dim maxC = Math.Max(rl, Math.Max(gl, bl))
                If maxC > 0 Then
                    rl /= maxC
                    gl /= maxC
                    bl /= maxC
                End If

                If rl < 0 Then rl = 0
                If gl < 0 Then gl = 0
                If bl < 0 Then bl = 0

                rl = 标准RGB伽马校正(rl)
                gl = 标准RGB伽马校正(gl)
                bl = 标准RGB伽马校正(bl)

                Dim ri = CInt(Math.Clamp(rl * 255, 0, 255))
                Dim gi = CInt(Math.Clamp(gl * 255, 0, 255))
                Dim bi = CInt(Math.Clamp(bl * 255, 0, 255))
                Dim argb As Integer = OpaqueAlphaMask Or (ri << 16) Or (gi << 8) Or bi

                Dim endY = Math.Min(py + renderStep - 1, h - 1)
                Dim endX = Math.Min(px + renderStep - 1, w - 1)
                For fy = py To endY
                    Dim rowBase = fy * w
                    For fx = px To endX
                        pixels(rowBase + fx) = argb
                    Next
                Next
            Next
        Next

        If token.IsCancellationRequested Then Return Nothing

        Dim bmp As New Bitmap(w, h, PixelFormat.Format32bppArgb)
        Dim bmpData = bmp.LockBits(New Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
        Try
            Dim stride = bmpData.Stride
            Dim scan0 = bmpData.Scan0
            If stride = w * 4 Then
                Runtime.InteropServices.Marshal.Copy(pixels, 0, scan0, w * h)
            Else
                For py = 0 To h - 1
                    Runtime.InteropServices.Marshal.Copy(pixels, py * w, IntPtr.Add(scan0, py * stride), w)
                Next
            End If
        Finally
            bmp.UnlockBits(bmpData)
        End Try
        Return bmp
    End Function

#End Region

#Region "颜色同步"

    ''' <summary>
    ''' 将当前颜色同步到所有 D2D 虚拟输入框、预览区和 HTML 选择状态。
    ''' </summary>
    Private Sub 应用颜色到界面(c As Color, updateMarker As Boolean)
        应用颜色到D2D界面(c, updateMarker)
    End Sub

    ''' <summary>
    ''' 从 sRGB 反算 CIE xy 标记位置；从色域图点击进入时不调用，避免往返误差移动准心。
    ''' </summary>
    Private Sub 从颜色更新标记(c As Color)
        Dim xyz = 标准RGB转XYZ(c.R, c.G, c.B)
        Dim sum = xyz.Item1 + xyz.Item2 + xyz.Item3
        If sum > 0.0001 Then
            _markerX = xyz.Item1 / sum
            _markerY = xyz.Item2 / sum
        Else
            _markerX = 0.3127
            _markerY = 0.329
        End If
        限制标记到色域内()
    End Sub

    Private Function 解析当前Alpha() As Integer
        Return 解析D2D当前Alpha()
    End Function

    ''' <summary>
    ''' 将当前 CIE 标记限制到光谱轨迹闭合区域内，保证后续 XYZ/sRGB 计算可控。
    ''' </summary>
    Private Sub 限制标记到色域内()
        If 点在多边形内(_markerX, _markerY, _polyX, _polyY, _polyN) Then Return

        Dim bestDist As Double = Double.MaxValue
        Dim bestX As Double = _markerX
        Dim bestY As Double = _markerY

        For i = 0 To _polyN - 2
            Dim cp = 取线段最近点(
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

#Region "取色器"

    Private Sub 切换取色器()
        If _eyeDropperActive Then
            停止取色器()
        Else
            开始取色器()
        End If
    End Sub

    Private Sub 开始取色器()
        _eyeDropperActive = True
        If IsHandleCreated Then Invalidate()

        If _eyeDropperTimer Is Nothing Then
            _eyeDropperTimer = New System.Windows.Forms.Timer With {.Interval = 100}
            AddHandler _eyeDropperTimer.Tick, AddressOf 取色器计时器触发
        End If
        _eyeDropperTimer.Start()
    End Sub

    Private Sub 停止取色器(Optional invalidateView As Boolean = True)
        _eyeDropperActive = False
        _eyeDropperTimer?.Stop()
        If invalidateView AndAlso Not _isClosing AndAlso IsHandleCreated Then Invalidate()
    End Sub

    Private Sub 取色器计时器触发(sender As Object, e As EventArgs)
        Dim pos = Cursor.Position
        Dim c = 获取屏幕像素颜色(pos.X, pos.Y)
        c = Color.FromArgb(解析当前Alpha(), c.R, c.G, c.B)
        SelectedColor = c
        应用颜色到界面(c, True)
    End Sub

    ''' <summary>
    ''' 截取屏幕上指定坐标处 1x1 像素颜色，供全屏取色器轮询使用。
    ''' </summary>
    Private Shared Function 获取屏幕像素颜色(x As Integer, y As Integer) As Color
        Using bmp As New Bitmap(1, 1, PixelFormat.Format32bppArgb)
            Using g = Graphics.FromImage(bmp)
                g.CopyFromScreen(x, y, 0, 0, New Size(1, 1))
            End Using
            Return bmp.GetPixel(0, 0)
        End Using
    End Function

#End Region

#Region "坐标变换"

    ''' <summary>
    ''' 将 CIE xy 坐标映射到色域图像素坐标；显示范围固定为 x[0,0.8]、y[0,0.9]。
    ''' </summary>
    Private Shared Function 色度坐标转像素(cx As Double, cy As Double, w As Integer, h As Integer) As PointF
        Const margin As Double = 0.08
        Dim px = CSng((cx / 0.8) * (1 - 2 * margin) * w + margin * w)
        Dim py = CSng((1.0 - cy / 0.9) * (1 - 2 * margin) * h + margin * h)
        Return New PointF(px, py)
    End Function

    ''' <summary>
    ''' 将色域图像素坐标反算为 CIE xy 坐标，用于鼠标点击取色。
    ''' </summary>
    Private Shared Function 像素转色度坐标(px As Integer, py As Integer, w As Integer, h As Integer) As (Double, Double)
        Const margin As Double = 0.08
        Dim cx = ((px - margin * w) / ((1 - 2 * margin) * w)) * 0.8
        Dim cy = (1.0 - (py - margin * h) / ((1 - 2 * margin) * h)) * 0.9
        Return (cx, cy)
    End Function

    ''' <summary>
    ''' 使用像素中心点反算 CIE xy，避免后台栅格化时出现半像素偏移。
    ''' </summary>
    Private Shared Function 像素中心转色度坐标(px As Integer, py As Integer, w As Integer, h As Integer) As (Double, Double)
        Const margin As Double = 0.08
        Dim cx = (((px + 0.5) - margin * w) / ((1 - 2 * margin) * w)) * 0.8
        Dim cy = (1.0 - ((py + 0.5) - margin * h) / ((1 - 2 * margin) * h)) * 0.9
        Return (cx, cy)
    End Function

#End Region

#Region "颜色空间转换"

    ''' <summary>
    ''' 将 CIE XYZ(D65) 转为 sRGB，并按最大通道归一化以保留色度比例。
    ''' </summary>
    Private Shared Function 色彩XYZ转标准RGB(X As Double, Y As Double, Z As Double) As Color
        Dim rl = 3.2404542 * X - 1.5371385 * Y - 0.4985314 * Z
        Dim gl = -0.969266 * X + 1.8760108 * Y + 0.041556 * Z
        Dim bl = 0.0556434 * X - 0.2040259 * Y + 1.0572252 * Z

        Dim maxC = Math.Max(rl, Math.Max(gl, bl))
        If maxC > 0 Then
            rl /= maxC
            gl /= maxC
            bl /= maxC
        End If

        If rl < 0 Then rl = 0
        If gl < 0 Then gl = 0
        If bl < 0 Then bl = 0

        rl = 标准RGB伽马校正(rl)
        gl = 标准RGB伽马校正(gl)
        bl = 标准RGB伽马校正(bl)

        Return Color.FromArgb(
            255,
            CInt(Math.Clamp(rl * 255, 0, 255)),
            CInt(Math.Clamp(gl * 255, 0, 255)),
            CInt(Math.Clamp(bl * 255, 0, 255)))
    End Function

    ''' <summary>
    ''' 将 sRGB 转为线性 CIE XYZ(D65)，用于从当前颜色反推 CIE xy 标记。
    ''' </summary>
    Private Shared Function 标准RGB转XYZ(r As Integer, g As Integer, b As Integer) As (Double, Double, Double)
        Dim rl = 标准RGB反伽马校正(r / 255.0)
        Dim gl = 标准RGB反伽马校正(g / 255.0)
        Dim bl = 标准RGB反伽马校正(b / 255.0)

        Dim X = 0.4124564 * rl + 0.3575761 * gl + 0.1804375 * bl
        Dim Y = 0.2126729 * rl + 0.7151522 * gl + 0.072175 * bl
        Dim Z = 0.0193339 * rl + 0.119192 * gl + 0.9503041 * bl
        Return (X, Y, Z)
    End Function

    Private Shared Function 标准RGB伽马校正(c As Double) As Double
        If c <= 0.0031308 Then Return 12.92 * c
        Return 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055
    End Function

    Private Shared Function 标准RGB反伽马校正(c As Double) As Double
        If c <= 0.04045 Then Return c / 12.92
        Return Math.Pow((c + 0.055) / 1.055, 2.4)
    End Function

    ''' <summary>
    ''' 将 RGB 分量转换为 HSL，H 取 0-360，S/L 取 0-100。
    ''' </summary>
    Private Shared Function 颜色转HSL(r As Integer, g As Integer, b As Integer) As (Double, Double, Double)
        Dim rd = r / 255.0
        Dim gd = g / 255.0
        Dim bd = b / 255.0
        Dim max = Math.Max(rd, Math.Max(gd, bd))
        Dim min = Math.Min(rd, Math.Min(gd, bd))
        Dim l2 = (max + min) / 2.0
        Dim h2 As Double = 0
        Dim s2 As Double = 0

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
        Return (h2, s2 * 100, l2 * 100)
    End Function

    ''' <summary>
    ''' 将 HSL 转为 RGB，输入与界面数值一致：H 0-360，S/L 0-100。
    ''' </summary>
    Private Shared Function 色相饱和亮度转RGB(h As Double, s As Double, l As Double) As (Integer, Integer, Integer)
        s /= 100.0
        l /= 100.0
        If s = 0 Then
            Dim v = CInt(Math.Clamp(l * 255, 0, 255))
            Return (v, v, v)
        End If

        Dim q = If(l < 0.5, l * (1 + s), l + s - l * s)
        Dim p = 2 * l - q
        Dim hNorm = h / 360.0

        Dim r = 色相转RGB分量(p, q, hNorm + 1.0 / 3.0)
        Dim g = 色相转RGB分量(p, q, hNorm)
        Dim b = 色相转RGB分量(p, q, hNorm - 1.0 / 3.0)
        Return (
            CInt(Math.Clamp(r * 255, 0, 255)),
            CInt(Math.Clamp(g * 255, 0, 255)),
            CInt(Math.Clamp(b * 255, 0, 255)))
    End Function

    Private Shared Function 色相转RGB分量(p As Double, q As Double, t As Double) As Double
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
    ''' 射线法判断 CIE xy 点是否在光谱轨迹闭合多边形内。
    ''' </summary>
    Private Shared Function 点在多边形内(px As Double, py As Double,
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

    ''' <summary>
    ''' 扫描线栅格化：为每一行预先计算色域多边形覆盖的像素范围。
    ''' </summary>
    Private Shared Sub 计算色域扫描跨度(w As Integer, h As Integer,
                                      rowMin() As Integer, rowMax() As Integer)
        Dim vx(_polyN - 1) As Double
        Dim vy(_polyN - 1) As Double
        For i = 0 To _polyN - 1
            Dim p = 色度坐标转像素(_polyX(i), _polyY(i), w, h)
            vx(i) = p.X
            vy(i) = p.Y
        Next

        For i = 0 To h - 1
            rowMin(i) = Integer.MaxValue
            rowMax(i) = Integer.MinValue
        Next

        For py = 0 To h - 1
            Dim yScan As Double = py + 0.5
            Dim xLo As Double = Double.PositiveInfinity
            Dim xHi As Double = Double.NegativeInfinity
            Dim j = _polyN - 1
            For i = 0 To _polyN - 1
                Dim yi = vy(i)
                Dim yj = vy(j)
                If (yi <= yScan AndAlso yj > yScan) OrElse (yj <= yScan AndAlso yi > yScan) Then
                    Dim t = (yScan - yi) / (yj - yi)
                    Dim xInt = vx(i) + t * (vx(j) - vx(i))
                    If xInt < xLo Then xLo = xInt
                    If xInt > xHi Then xHi = xInt
                End If
                j = i
            Next
            If Double.IsInfinity(xLo) Then Continue For

            Dim xLoI = CInt(Math.Floor(xLo)) - 1
            Dim xHiI = CInt(Math.Ceiling(xHi)) + 1
            If xLoI < 0 Then xLoI = 0
            If xHiI > w - 1 Then xHiI = w - 1
            rowMin(py) = xLoI
            rowMax(py) = xHiI
        Next
    End Sub

    ''' <summary>
    ''' 返回点到指定线段的最近点，用于把越界 CIE 标记吸附回色域边界。
    ''' </summary>
    Private Shared Function 取线段最近点(px As Double, py As Double,
                                      ax As Double, ay As Double,
                                      bx As Double, by As Double) As (Double, Double)
        Dim dx = bx - ax
        Dim dy = by - ay
        Dim lenSq = dx * dx + dy * dy
        If lenSq < 0.000001 Then Return (ax, ay)
        Dim t = ((px - ax) * dx + (py - ay) * dy) / lenSq
        t = Math.Clamp(t, 0.0, 1.0)
        Return (ax + t * dx, ay + t * dy)
    End Function

#End Region

#Region "D2D 公共接口"

    Private _d2dElementBackColor As Color = Color.FromArgb(40, 220, 220, 220)
    <Category("LakeUI"), Description("按钮、输入框、列表和色域图容器的基础背景色。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property D2DElementBackColor As Color
        Get
            Return _d2dElementBackColor
        End Get
        Set(value As Color)
            If _d2dElementBackColor = value Then Return
            _d2dElementBackColor = value
            Invalidate()
        End Set
    End Property

    Private _d2dElementHoverBackColor As Color = Color.FromArgb(60, 220, 220, 220)
    <Category("LakeUI"), Description("按钮、列表项等元素的悬停背景色。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property D2DElementHoverBackColor As Color
        Get
            Return _d2dElementHoverBackColor
        End Get
        Set(value As Color)
            If _d2dElementHoverBackColor = value Then Return
            _d2dElementHoverBackColor = value
            Invalidate()
        End Set
    End Property

    Private _d2dElementPressedBackColor As Color = Color.FromArgb(80, 220, 220, 220)
    <Category("LakeUI"), Description("按钮按下时的背景色。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property D2DElementPressedBackColor As Color
        Get
            Return _d2dElementPressedBackColor
        End Get
        Set(value As Color)
            If _d2dElementPressedBackColor = value Then Return
            _d2dElementPressedBackColor = value
            Invalidate()
        End Set
    End Property

    Private _d2dElementBorderColor As Color = Color.FromArgb(120, 220, 220, 220)
    <Category("LakeUI"), Description("输入框、列表和色域图容器的边框色。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property D2DElementBorderColor As Color
        Get
            Return _d2dElementBorderColor
        End Get
        Set(value As Color)
            If _d2dElementBorderColor = value Then Return
            _d2dElementBorderColor = value
            Invalidate()
        End Set
    End Property

    Private _d2dFavoriteSlotBackColor As Color = Color.Black
    <Category("LakeUI"), Description("收藏夹空槽的底色。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property D2DFavoriteSlotBackColor As Color
        Get
            Return _d2dFavoriteSlotBackColor
        End Get
        Set(value As Color)
            If _d2dFavoriteSlotBackColor = value Then Return
            _d2dFavoriteSlotBackColor = value
            Invalidate()
        End Set
    End Property

    Private _d2dTextProvider As Func(Of String, String)
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property D2DTextProvider As Func(Of String, String)
        Get
            Return _d2dTextProvider
        End Get
        Set(value As Func(Of String, String))
            _d2dTextProvider = value
            Invalidate()
        End Set
    End Property

    Private ReadOnly _d2dTextOverrides As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    Public Sub SetD2DText(key As String, text As String)
        If String.IsNullOrWhiteSpace(key) Then Return
        _d2dTextOverrides(key) = If(text, String.Empty)
        Invalidate()
    End Sub

    Public Sub ClearD2DTextOverrides()
        _d2dTextOverrides.Clear()
        Invalidate()
    End Sub

    Private _d2dKeepWindowBackdropTransparent As Boolean = True
    <Category("LakeUI"), Description("挂接 ThisIsYourWindow 等窗口级背景时，客户区底色保持透明，只绘制 D2D 交互元素。"),
     DefaultValue(True)>
    Public Property D2DKeepWindowBackdropTransparent As Boolean
        Get
            Return _d2dKeepWindowBackdropTransparent
        End Get
        Set(value As Boolean)
            If _d2dKeepWindowBackdropTransparent = value Then Return
            _d2dKeepWindowBackdropTransparent = value
            Invalidate()
        End Set
    End Property

#End Region

#Region "D2D 状态"

    Private Const WM_GETDLGCODE_D2D As Integer = &H87
    Private Const WM_CHAR_D2D As Integer = &H102
    Private Const WM_IME_STARTCOMPOSITION_D2D As Integer = &H10D
    Private Const WM_IME_ENDCOMPOSITION_D2D As Integer = &H10E
    Private Const WM_IME_COMPOSITION_D2D As Integer = &H10F
    Private Const WM_ENTERSIZEMOVE_D2D As Integer = &H231
    Private Const WM_EXITSIZEMOVE_D2D As Integer = &H232
    Private Const GCS_RESULTSTR_D2D As Integer = &H800
    Private Const DLGC_WANTCHARS_D2D As Integer = &H80
    Private Const DLGC_WANTALLKEYS_D2D As Integer = &H4

    Private _d2dInitialized As Boolean
    Private _d2dLayoutDirty As Boolean = True
    Private _d2dLayout As ColorDialogD2DLayout
    Private ReadOnly _d2dTextBoxes As New Dictionary(Of ColorDialogTextBoxKind, VirtualTextBox)()
    Private ReadOnly _d2dTextBoxOrder As New List(Of VirtualTextBox)()
    Private _d2dActiveTextBox As VirtualTextBox
    Private _d2dMouseTextBox As VirtualTextBox
    Private _d2dDragTextBox As VirtualTextBox
    Private _d2dDragStartY As Integer
    Private _d2dDragStartValue As Integer
    Private _d2dCapturePart As ColorDialogHitPart = ColorDialogHitPart.None
    Private _d2dPressedButton As ColorDialogButtonKind = ColorDialogButtonKind.None
    Private _d2dHoverButton As ColorDialogButtonKind = ColorDialogButtonKind.None
    Private _d2dHoverFavorite As Integer = -1
    Private _d2dPressedFavorite As Integer = -1
    Private _d2dHoverHtmlIndex As Integer = -1
    Private _d2dHtmlScrollDragOffset As Single
    Private _d2dSelectedHtmlName As String = Nothing
    Private _d2dSelectedHtmlIndex As Integer = -1
    Private _d2dHtmlScrollIndex As Single = 0.0F
    Private ReadOnly _d2dFilteredHtmlColors As New List(Of KeyValuePair(Of String, Color))()
    Private _d2dImeComposing As Boolean
    Private _d2dInSizeMove As Boolean

    Private Enum ColorDialogHitPart
        None
        Chromaticity
        TextBox
        NumericLabel
        HtmlScrollBar
        Favorite
        Button
    End Enum

    Private Enum ColorDialogButtonKind
        None
        EyeDropper
        Tips
        CopyArgb
        CopyHex
        OK
        Cancel
    End Enum

    Private Enum ColorDialogTextBoxKind
        Search
        R
        G
        B
        H
        S
        L
        A
        Hex
    End Enum

    Private NotInheritable Class VirtualTextBox
        Public Kind As ColorDialogTextBoxKind
        Public Renderer As SingleLineTextBoxRenderer
        Public Bounds As RectangleF
        Public TextArea As RectangleF
        Public LabelRect As RectangleF
        Public LabelHitRect As RectangleF
        Public Minimum As Integer
        Public Maximum As Integer
        Public IsNumeric As Boolean
    End Class

    Private NotInheritable Class ColorDialogD2DLayout
        Public Client As RectangleF
        Public ChromaticityTitle As RectangleF
        Public ChromaticityFrame As RectangleF
        Public ChromaticityRect As RectangleF
        Public HtmlTitle As RectangleF
        Public SearchBox As RectangleF
        Public HtmlList As RectangleF
        Public HtmlScrollBar As RectangleF
        Public ValuesTitle As RectangleF
        Public Preview As RectangleF
        Public FavoriteTitleMain As RectangleF
        Public FavoriteTitleHint As RectangleF
        Public FavoriteRects(9) As RectangleF
        Public ButtonEyeDropper As RectangleF
        Public ButtonTips As RectangleF
        Public ButtonCopyArgb As RectangleF
        Public ButtonCopyHex As RectangleF
        Public ButtonOK As RectangleF
        Public ButtonCancel As RectangleF
        Public ItemHeight As Single
        Public Radius As Single
        Public Padding As Single
        Public Gap As Single
    End Class

#End Region

#Region "D2D 生命周期"

    Private Sub 初始化D2D颜色对话框()
        If _d2dInitialized Then Return

        _d2dInitialized = True
        SetStyle(ControlStyles.UserPaint Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.Selectable, True)
        UpdateStyles()
        KeyPreview = True

        创建D2D文本框()
        初始化Html颜色表()
        重建D2DHTML筛选()
        应用颜色到界面(SelectedColor, True)
        启动后台色域图渲染()
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Try : ImeHelper.AssociateDefault(Handle) : Catch : End Try
    End Sub

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        Return
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If _isClosing OrElse IsDisposed OrElse Disposing Then
            MyBase.OnPaint(e)
            Return
        End If
        If _d2dInSizeMove Then
            MyBase.OnPaint(e)
            Return
        End If

        初始化D2D颜色对话框()
        确保D2D布局()
        MyBase.OnPaint(e)

        Dim ssaa As Integer = D2DHelperV2.GetEffectiveSsaaScale(2)

        Using scope = D2DHelperV2.BeginPaint(e, Me, ssaa)
            If scope Is Nothing Then Return
            Dim compositor = scope.Compositor
            Dim gRT As D2D.ID2D1RenderTarget = scope.GraphicsLayer
            Dim brushCache = compositor.BrushCache

            If BackColor.A > 0 AndAlso Not 应保持窗口级背景透明() Then
                Dim b = brushCache.[Get](scope.BackgroundLayer, BackColor)
                If b IsNot Nothing Then
                    scope.BackgroundLayer.FillRectangle(D2DGlobals.ToD2DRect(DisplayRectangle), b)
                End If
            End If

            绘制D2D图形层(gRT, compositor)
            scope.FlushGraphics()
            绘制D2D文字层(scope.TextLayer, compositor)
        End Using
        _chromaticityBitmapCache.TrimToCurrentBudget()
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        _d2dLayoutDirty = True
        If Not _d2dInSizeMove AndAlso WindowState <> FormWindowState.Minimized Then Invalidate()
    End Sub

    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        _d2dLayoutDirty = True
        If IsHandleCreated Then Invalidate()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        D2DHelperV2.InvalidateTextFormatCache(Me)
        For Each box In _d2dTextBoxOrder
            box.Renderer.LineHeight = 缩放值(24)
        Next
        _d2dLayoutDirty = True
        Invalidate()
    End Sub

    Private Sub 清理D2D颜色对话框()
        For Each box In _d2dTextBoxOrder
            Try : box.Renderer.StopCaretBlink() : Catch : End Try
        Next
    End Sub

    Private Sub 调整大小结束D2D()
        _d2dLayoutDirty = True
        确保D2D布局()
        启动后台色域图渲染()
        Invalidate()
    End Sub

    Private Sub 显示后刷新D2D()
        _d2dLayoutDirty = True
        确保D2D布局()
        启动后台色域图渲染()
        Invalidate()
    End Sub

    Private Function 获取D2D色域图渲染尺寸() As Size
        确保D2D布局()
        If _d2dLayout Is Nothing Then Return Size.Empty
        Dim r = _d2dLayout.ChromaticityRect
        Dim w = Math.Max(10, CInt(Math.Round(r.Width)))
        Dim h = Math.Max(10, CInt(Math.Round(r.Height)))
        Return New Size(w, h)
    End Function

    Private Sub 刷新D2D色域图区域()
        确保D2D布局()
        Dim r = Rectangle.Ceiling(_d2dLayout.ChromaticityFrame)
        r.Inflate(3, 3)
        Invalidate(Rectangle.Intersect(ClientRectangle, r), False)
    End Sub

    Private Function 应保持窗口级背景透明() As Boolean
        If Not D2DKeepWindowBackdropTransparent Then Return False
        Return Padding.Left > 0 OrElse Padding.Top > 0 OrElse Padding.Right > 0 OrElse Padding.Bottom > 0
    End Function

#End Region

#Region "D2D 布局"

    Private Sub 确保D2D布局()
        If _d2dTextBoxes.Count = 0 Then 创建D2D文本框()
        If _d2dLayout Is Nothing Then _d2dLayoutDirty = True
        If Not _d2dLayoutDirty Then Return
        _d2dLayoutDirty = False

        Dim s = 取D2D缩放()
        Dim textFormatCache = D2DHelperV2.GetCompositor(Me)?.TextFormatCache
        Dim display = DisplayRectangle
        Dim pad As Single = 20.0F * s
        Dim gap As Single = 14.0F * s
        Dim smallGap As Single = 10.0F * s
        Dim headerH As Single = 28.0F * s
        Dim fieldH As Single = 32.0F * s
        Dim buttonH As Single = 36.0F * s
        Dim bottomH As Single = 90.0F * s
        Dim radius As Single = 10.0F * s
        Dim buttonW As Single = 120.0F * s
        Dim contentLeft As Single = display.X + pad
        Dim contentRight As Single = display.Right - pad
        Dim contentW As Single = Math.Max(1.0F, contentRight - contentLeft)
        Dim topY As Single = display.Y + pad
        Dim searchTop As Single = topY + headerH + smallGap
        Dim buttonsTop As Single = display.Bottom - pad - buttonH
        Dim valueRowsH As Single = fieldH * 8.0F + smallGap * 9.0F
        Dim requiredMainBottom As Single = searchTop + valueRowsH
        Dim bottomTop As Single = display.Bottom - (buttonH + pad * 2.0F) - bottomH
        bottomTop = Math.Max(bottomTop, requiredMainBottom)
        If bottomTop + bottomH > buttonsTop - smallGap Then bottomH = Math.Max(0.0F, buttonsTop - smallGap - bottomTop)
        Dim mainBottom As Single = bottomTop

        Dim layout As New ColorDialogD2DLayout With {
            .Client = New RectangleF(display.X, display.Y, display.Width, display.Height),
            .ItemHeight = 26.0F * s,
            .Radius = radius,
            .Padding = pad,
            .Gap = gap
        }

        Dim valueLabelW As Single = 计算数值标签宽度(textFormatCache)
        Dim valueTextMinW As Single = Math.Max(64.0F * s, 测量界面文本宽度("#AARRGGBB", Font, textFormatCache) + 26.0F * s)
        Dim rightMinW As Single = valueLabelW + smallGap + valueTextMinW
        Dim rightPreferredW As Single = Math.Min(190.0F * s, Math.Max(rightMinW, contentW * 0.24F))
        Dim htmlSideBlank As Single = 10.0F * s
        Dim htmlBaseW As Single = 220.0F * s + htmlSideBlank * 2.0F

        Dim desiredChromSize As Single = Math.Max(120.0F * s, mainBottom - searchTop)
        Dim htmlAbsoluteMinW As Single = 120.0F * s + htmlSideBlank * 2.0F
        Dim maxChromByWidth As Single = contentW - rightMinW - htmlAbsoluteMinW - gap * 2.0F
        Dim chromSize As Single = Math.Min(desiredChromSize, Math.Max(120.0F * s, maxChromByWidth))
        Dim rightW As Single = rightPreferredW
        Dim roomForRightWithBaseHtml As Single = contentW - chromSize - htmlBaseW - gap * 2.0F
        If roomForRightWithBaseHtml < rightW Then rightW = Math.Max(rightMinW, roomForRightWithBaseHtml)
        If rightW < rightMinW Then rightW = rightMinW

        Dim chromX As Single = contentLeft
        Dim valuesX As Single = contentRight - rightW
        Dim htmlX As Single = chromX + chromSize + gap
        Dim htmlW As Single = Math.Max(80.0F * s, valuesX - gap - htmlX)
        If htmlW < htmlBaseW AndAlso rightW > rightMinW Then
            Dim shrinkRight As Single = Math.Min(rightW - rightMinW, htmlBaseW - htmlW)
            rightW -= shrinkRight
            valuesX = contentRight - rightW
            htmlW = Math.Max(80.0F * s, valuesX - gap - htmlX)
        End If

        layout.ChromaticityTitle = New RectangleF(chromX, topY, chromSize, headerH)
        layout.ChromaticityFrame = New RectangleF(chromX, searchTop, chromSize, chromSize)
        Dim frameInset As Single = 5.0F * s
        layout.ChromaticityRect = 调整矩形(layout.ChromaticityFrame, -frameInset, -frameInset)

        layout.HtmlTitle = New RectangleF(htmlX, topY, htmlW, headerH)
        layout.SearchBox = New RectangleF(htmlX, searchTop, htmlW, fieldH)
        layout.HtmlList = New RectangleF(htmlX, layout.SearchBox.Bottom + smallGap, htmlW, Math.Max(fieldH, mainBottom - layout.SearchBox.Bottom - smallGap))
        Dim scrollBarW As Single = 9.0F * s
        layout.HtmlScrollBar = New RectangleF(layout.HtmlList.Right - scrollBarW - 6.0F * s,
                                              layout.HtmlList.Top + 8.0F * s,
                                              scrollBarW,
                                              Math.Max(1.0F, layout.HtmlList.Height - 16.0F * s))

        layout.ValuesTitle = New RectangleF(valuesX, topY, rightW, headerH)
        布局数值输入框(valuesX, searchTop, rightW, valueLabelW, fieldH, smallGap)

        Dim previewOffsetY As Single = Math.Min(20.0F * s, Math.Max(0.0F, bottomH - 36.0F * s))
        Dim previewH As Single = Math.Max(1.0F, bottomH - previewOffsetY)
        layout.Preview = New RectangleF(contentLeft, bottomTop + previewOffsetY, buttonW, previewH)
        Dim favStartX As Single = layout.Preview.Right + gap
        Dim favGap As Single = 10.0F * s
        Dim favSlotByHeight As Single = Math.Max(1.0F, layout.Preview.Height)
        Dim favSlot As Single = Math.Min(Math.Min(40.0F * s, favSlotByHeight), Math.Max(18.0F * s, (contentRight - favStartX - favGap * 9.0F) / 10.0F))
        If favStartX + favSlot * 10.0F + favGap * 9.0F > contentRight AndAlso favSlot > 0 Then
            favGap = Math.Max(4.0F * s, (contentRight - favStartX - favSlot * 10.0F) / 9.0F)
        End If
        Dim favY As Single = layout.Preview.Bottom - favSlot
        For i = 0 To 9
            layout.FavoriteRects(i) = New RectangleF(favStartX + i * (favSlot + favGap), favY, favSlot, favSlot)
        Next

        Dim favoriteMainText As String = 取界面文本("FavoritesTitle", "收藏夹")
        Dim favoriteMainW As Single = Math.Max(1.0F, 测量界面文本宽度(favoriteMainText, Font, textFormatCache) + 6.0F * s)
        Dim favoriteMainH As Single = Math.Max(1.0F, CSng(D2DTextRenderer.MeasureLineHeight(Font, s, textFormatCache)))
        layout.FavoriteTitleMain = New RectangleF(favStartX, layout.Preview.Top, favoriteMainW, favoriteMainH)
        Using hintFont As New Font(Font.Name, Math.Max(1.0F, Font.Size - 1.2F), FontStyle.Regular, GraphicsUnit.Point)
            Dim favoriteHintH As Single = Math.Max(1.0F, CSng(D2DTextRenderer.MeasureLineHeight(hintFont, s, textFormatCache)))
            layout.FavoriteTitleHint = New RectangleF(layout.FavoriteTitleMain.Right + 2.0F * s,
                                                      layout.FavoriteTitleMain.Bottom - favoriteHintH,
                                                      Math.Max(1.0F, contentRight - layout.FavoriteTitleMain.Right - 2.0F * s),
                                                      favoriteHintH)
        End Using

        layout.ButtonEyeDropper = New RectangleF(contentLeft, buttonsTop, buttonW, buttonH)
        layout.ButtonTips = New RectangleF(layout.ButtonEyeDropper.Right + smallGap, buttonsTop, buttonW, buttonH)
        Dim copyButtonW As Single = Math.Min(buttonW, Math.Max(96.0F * s, buttonW - 20.0F * s))
        layout.ButtonCopyArgb = New RectangleF(layout.ButtonTips.Right + smallGap, buttonsTop, copyButtonW, buttonH)
        layout.ButtonCopyHex = New RectangleF(layout.ButtonCopyArgb.Right + smallGap, buttonsTop, copyButtonW, buttonH)
        layout.ButtonCancel = New RectangleF(contentRight - buttonW, buttonsTop, buttonW, buttonH)
        layout.ButtonOK = New RectangleF(layout.ButtonCancel.Left - smallGap - buttonW, buttonsTop, buttonW, buttonH)
        Dim maxLeftButtonsRight As Single = layout.ButtonOK.Left - smallGap
        If layout.ButtonCopyHex.Right > maxLeftButtonsRight Then
            Dim availableCopyW As Single = (maxLeftButtonsRight - layout.ButtonTips.Right - smallGap * 3.0F) / 2.0F
            copyButtonW = Math.Max(70.0F * s, availableCopyW)
            layout.ButtonCopyArgb = New RectangleF(layout.ButtonTips.Right + smallGap, buttonsTop, copyButtonW, buttonH)
            layout.ButtonCopyHex = New RectangleF(layout.ButtonCopyArgb.Right + smallGap, buttonsTop, copyButtonW, buttonH)
        End If

        _d2dLayout = layout
        更新文本框渲染区域()
        限制HTML滚动()
    End Sub

    Private Sub 布局数值输入框(x As Single, y As Single, w As Single, labelW As Single, fieldH As Single, gap As Single)
        Dim rowY As Single = y
        Dim rows = {
            ColorDialogTextBoxKind.R,
            ColorDialogTextBoxKind.G,
            ColorDialogTextBoxKind.B,
            ColorDialogTextBoxKind.H,
            ColorDialogTextBoxKind.S,
            ColorDialogTextBoxKind.L,
            ColorDialogTextBoxKind.A,
            ColorDialogTextBoxKind.Hex
        }
        For Each kind In rows
            Dim box = _d2dTextBoxes(kind)
            box.LabelRect = New RectangleF(x, rowY, labelW, fieldH)
            box.LabelHitRect = New RectangleF(x, rowY, labelW + gap, fieldH)
            box.Bounds = New RectangleF(x + labelW + gap, rowY, Math.Max(1.0F, w - labelW - gap), fieldH)
            rowY += fieldH + gap
            If kind = ColorDialogTextBoxKind.B OrElse kind = ColorDialogTextBoxKind.L Then rowY += gap
        Next
    End Sub

    Private Sub 更新文本框渲染区域()
        If _d2dLayout Is Nothing Then Return
        Dim insetX As Single = 10.0F * 取D2D缩放()
        Dim insetY As Single = 2.0F * 取D2D缩放()
        For Each box In _d2dTextBoxOrder
            If box.Kind = ColorDialogTextBoxKind.Search Then box.Bounds = _d2dLayout.SearchBox
            box.TextArea = 调整矩形(box.Bounds, -insetX, -insetY)
            box.Renderer.LineHeight = 缩放值(24)
        Next
    End Sub

    Private Function 计算数值标签宽度(textFormatCache As D2DGlobals.TextFormatCache) As Single
        Dim s = 取D2D缩放()
        Dim w As Single = 0.0F
        For Each kind In {ColorDialogTextBoxKind.R, ColorDialogTextBoxKind.G, ColorDialogTextBoxKind.B,
                          ColorDialogTextBoxKind.H, ColorDialogTextBoxKind.S, ColorDialogTextBoxKind.L,
                          ColorDialogTextBoxKind.A, ColorDialogTextBoxKind.Hex}
            w = Math.Max(w, 测量界面文本宽度(取数值标签文本(kind), Font, textFormatCache))
        Next
        Return Math.Max(48.0F * s, w + 4.0F * s)
    End Function

    Private Function 取数值标签文本(kind As ColorDialogTextBoxKind) As String
        Select Case kind
            Case ColorDialogTextBoxKind.R
                Return 取界面文本("LabelRed", "红色 R")
            Case ColorDialogTextBoxKind.G
                Return 取界面文本("LabelGreen", "绿色 G")
            Case ColorDialogTextBoxKind.B
                Return 取界面文本("LabelBlue", "蓝色 B")
            Case ColorDialogTextBoxKind.H
                Return 取界面文本("LabelHue", "色相 H")
            Case ColorDialogTextBoxKind.S
                Return 取界面文本("LabelSaturation", "饱和 S")
            Case ColorDialogTextBoxKind.L
                Return 取界面文本("LabelLightness", "亮度 L")
            Case ColorDialogTextBoxKind.A
                Return 取界面文本("LabelAlpha", "透明 A")
            Case ColorDialogTextBoxKind.Hex
                Return 取界面文本("LabelHex", "HEX")
            Case Else
                Return String.Empty
        End Select
    End Function

    Private Shared Function 调整矩形(rect As RectangleF, dx As Single, dy As Single) As RectangleF
        rect.Inflate(dx, dy)
        Return rect
    End Function

#End Region

#Region "D2D 文本框"

    Private Sub 创建D2D文本框()
        If _d2dTextBoxes.Count > 0 Then Return
        添加虚拟文本框(ColorDialogTextBoxKind.Search, 0, 0, False)
        添加虚拟文本框(ColorDialogTextBoxKind.R, 0, 255, True)
        添加虚拟文本框(ColorDialogTextBoxKind.G, 0, 255, True)
        添加虚拟文本框(ColorDialogTextBoxKind.B, 0, 255, True)
        添加虚拟文本框(ColorDialogTextBoxKind.H, 0, 359, True)
        添加虚拟文本框(ColorDialogTextBoxKind.S, 0, 100, True)
        添加虚拟文本框(ColorDialogTextBoxKind.L, 0, 100, True)
        添加虚拟文本框(ColorDialogTextBoxKind.A, 0, 255, True)
        添加虚拟文本框(ColorDialogTextBoxKind.Hex, 0, 0, False)
    End Sub

    Private Sub 添加虚拟文本框(kind As ColorDialogTextBoxKind, minValue As Integer, maxValue As Integer, numeric As Boolean)
        Dim box As New VirtualTextBox With {
            .Kind = kind,
            .Minimum = minValue,
            .Maximum = maxValue,
            .IsNumeric = numeric
        }
        box.Renderer = New SingleLineTextBoxRenderer(Me) With {
            .BorderSize = 0,
            .ForeColor = ForeColor,
            .WaterTextForeColor = Color.Gray,
            .SelectionColor = D2DElementBackColor,
            .CaretColor = Color.FromArgb(220, 220, 220),
            .TextAreaProvider = Function() box.TextArea,
            .DpiScaleProvider = Function() 取D2D缩放(),
            .InvalidateAction = Sub() 刷新虚拟文本框(box),
            .FocusProvider = Function() ReferenceEquals(_d2dActiveTextBox, box) AndAlso Focused
        }
        Select Case kind
            Case ColorDialogTextBoxKind.Search
                box.Renderer.WaterText = 取界面文本("SearchWatermark", "搜索")
            Case ColorDialogTextBoxKind.Hex
                box.Renderer.TextFilter = AddressOf 过滤HEX文本
                box.Renderer.CandidateValidator = AddressOf 是否可能为HEX文本
            Case Else
                box.Renderer.TextFilter = AddressOf 过滤整数文本
                box.Renderer.CandidateValidator = Function(candidate) 是否可能为整数文本(candidate, minValue, maxValue)
        End Select
        AddHandler box.Renderer.TextChanged, Sub(sender, e) 文本框文本变化D2D(box)
        _d2dTextBoxes(kind) = box
        _d2dTextBoxOrder.Add(box)
    End Sub

    Private Sub 刷新虚拟文本框(box As VirtualTextBox)
        If box Is Nothing Then
            Invalidate()
            Return
        End If
        Dim r = Rectangle.Ceiling(box.Bounds)
        r.Inflate(3, 3)
        Invalidate(Rectangle.Intersect(ClientRectangle, r), False)
    End Sub

    Private Sub 应用颜色到D2D界面(c As Color, updateMarker As Boolean)
        If _suppressSync Then Return
        Dim newArgb = c.ToArgb()
        If newArgb = _lastAppliedArgb Then
            Invalidate()
            Return
        End If
        _lastAppliedArgb = newArgb
        _suppressSync = True
        Try
            设置文本框文本(ColorDialogTextBoxKind.R, c.R.ToString(CultureInfo.InvariantCulture))
            设置文本框文本(ColorDialogTextBoxKind.G, c.G.ToString(CultureInfo.InvariantCulture))
            设置文本框文本(ColorDialogTextBoxKind.B, c.B.ToString(CultureInfo.InvariantCulture))
            设置文本框文本(ColorDialogTextBoxKind.A, c.A.ToString(CultureInfo.InvariantCulture))
            设置文本框文本(ColorDialogTextBoxKind.Hex, If(c.A = 255,
                $"#{c.R:X2}{c.G:X2}{c.B:X2}",
                $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}"))

            Dim hsl = 颜色转HSL(c.R, c.G, c.B)
            设置文本框文本(ColorDialogTextBoxKind.H, Math.Round(hsl.Item1).ToString(CultureInfo.InvariantCulture))
            设置文本框文本(ColorDialogTextBoxKind.S, Math.Round(hsl.Item2).ToString(CultureInfo.InvariantCulture))
            设置文本框文本(ColorDialogTextBoxKind.L, Math.Round(hsl.Item3).ToString(CultureInfo.InvariantCulture))

            If updateMarker Then 从颜色更新标记(c)
            高亮D2DHTML颜色(c)
        Finally
            _suppressSync = False
        End Try
        Invalidate()
    End Sub

    Private Sub 设置文本框文本(kind As ColorDialogTextBoxKind, text As String)
        If Not _d2dTextBoxes.ContainsKey(kind) Then Return
        _d2dTextBoxes(kind).Renderer.SetText(text, -1, False, False)
    End Sub

    Private Function 取文本框文本(kind As ColorDialogTextBoxKind) As String
        If Not _d2dTextBoxes.ContainsKey(kind) Then Return String.Empty
        Return _d2dTextBoxes(kind).Renderer.Text
    End Function

    Private Sub 文本框文本变化D2D(box As VirtualTextBox)
        If _suppressSync OrElse box Is Nothing Then Return
        Select Case box.Kind
            Case ColorDialogTextBoxKind.Search
                重建D2DHTML筛选()
                Invalidate()
            Case ColorDialogTextBoxKind.R, ColorDialogTextBoxKind.G, ColorDialogTextBoxKind.B
                Dim r, g, b As Integer
                If Not Integer.TryParse(取文本框文本(ColorDialogTextBoxKind.R), r) Then Return
                If Not Integer.TryParse(取文本框文本(ColorDialogTextBoxKind.G), g) Then Return
                If Not Integer.TryParse(取文本框文本(ColorDialogTextBoxKind.B), b) Then Return
                SelectedColor = Color.FromArgb(解析当前Alpha(), Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255))
                应用颜色到界面(SelectedColor, True)
            Case ColorDialogTextBoxKind.H, ColorDialogTextBoxKind.S, ColorDialogTextBoxKind.L
                Dim h, s, l As Double
                If Not Double.TryParse(取文本框文本(ColorDialogTextBoxKind.H), h) Then Return
                If Not Double.TryParse(取文本框文本(ColorDialogTextBoxKind.S), s) Then Return
                If Not Double.TryParse(取文本框文本(ColorDialogTextBoxKind.L), l) Then Return
                Dim rgb = 色相饱和亮度转RGB(((h Mod 360) + 360) Mod 360, Math.Clamp(s, 0, 100), Math.Clamp(l, 0, 100))
                SelectedColor = Color.FromArgb(解析当前Alpha(), rgb.Item1, rgb.Item2, rgb.Item3)
                应用颜色到界面(SelectedColor, True)
            Case ColorDialogTextBoxKind.A
                Dim a As Integer
                If Not Integer.TryParse(取文本框文本(ColorDialogTextBoxKind.A), a) Then Return
                a = Math.Clamp(a, 0, 255)
                SelectedColor = Color.FromArgb(a, SelectedColor.R, SelectedColor.G, SelectedColor.B)
                应用颜色到界面(SelectedColor, True)
            Case ColorDialogTextBoxKind.Hex
                Dim c As Color
                If 尝试解析HEX颜色(取文本框文本(ColorDialogTextBoxKind.Hex), c) Then
                    SelectedColor = c
                    应用颜色到界面(c, True)
                End If
        End Select
    End Sub

    Private Function 解析D2D当前Alpha() As Integer
        Dim a As Integer = 255
        Integer.TryParse(取文本框文本(ColorDialogTextBoxKind.A), a)
        Return Math.Clamp(a, 0, 255)
    End Function

#End Region

#Region "D2D 绘制"

    Private Sub 绘制D2D图形层(rt As D2D.ID2D1RenderTarget, compositor As WindowCompositor)
        Dim brushCache = compositor.BrushCache
        Dim l = _d2dLayout
        If l Is Nothing Then Return

        填充圆角矩形(rt, l.ChromaticityFrame, D2DElementBackColor, l.Radius, brushCache)
        绘制D2D色域位图(rt, compositor)
        绘制D2D准心(rt, l.ChromaticityRect, brushCache)

        填充圆角矩形(rt, l.SearchBox, D2DElementBackColor, l.Radius, brushCache)
        描绘圆角边框(rt, l.SearchBox, If(ReferenceEquals(_d2dActiveTextBox, _d2dTextBoxes(ColorDialogTextBoxKind.Search)), D2DElementBorderColor, Color.Transparent), 1.0F * 取D2D缩放(), l.Radius, brushCache)
        填充圆角矩形(rt, l.HtmlList, D2DElementBackColor, l.Radius, brushCache)
        绘制HTML列表图形(rt, brushCache)

        For Each box In _d2dTextBoxOrder
            If box.Kind = ColorDialogTextBoxKind.Search Then Continue For
            填充圆角矩形(rt, box.Bounds, D2DElementBackColor, l.Radius, brushCache)
            Dim border = If(ReferenceEquals(_d2dActiveTextBox, box), D2DElementBorderColor, Color.Transparent)
            描绘圆角边框(rt, box.Bounds, border, 1.0F * 取D2D缩放(), l.Radius, brushCache)
        Next

        绘制当前颜色预览(rt, brushCache)
        绘制收藏夹(rt, brushCache)
        绘制按钮(rt, l.ButtonEyeDropper, ColorDialogButtonKind.EyeDropper, brushCache)
        绘制按钮(rt, l.ButtonTips, ColorDialogButtonKind.Tips, brushCache)
        绘制按钮(rt, l.ButtonCopyArgb, ColorDialogButtonKind.CopyArgb, brushCache)
        绘制按钮(rt, l.ButtonCopyHex, ColorDialogButtonKind.CopyHex, brushCache)
        绘制按钮(rt, l.ButtonOK, ColorDialogButtonKind.OK, brushCache)
        绘制按钮(rt, l.ButtonCancel, ColorDialogButtonKind.Cancel, brushCache)
    End Sub

    Private Sub 绘制D2D文字层(rt As D2D.ID2D1RenderTarget, compositor As WindowCompositor)
        Dim l = _d2dLayout
        If l Is Nothing Then Return
        Dim flagsLeft As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        Dim flagsRight As TextFormatFlags = TextFormatFlags.Right Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        Dim flagsCenter As TextFormatFlags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        Dim s = 取D2D缩放()
        Dim brushCache = compositor.BrushCache
        Dim tfc = compositor.TextFormatCache

        D2DTextRenderer.DrawText(rt, 取界面文本("Chromaticity", "色域图"), Font, l.ChromaticityTitle, ForeColor, flagsLeft, s, tfc, brushCache)
        D2DTextRenderer.DrawText(rt, 取界面文本("HtmlColors", "HTML 颜色"), Font, l.HtmlTitle, ForeColor, flagsLeft, s, tfc, brushCache)
        D2DTextRenderer.DrawText(rt, 取界面文本("Values", "数值"), Font, l.ValuesTitle, ForeColor, flagsRight, s, tfc, brushCache)

        _d2dTextBoxes(ColorDialogTextBoxKind.Search).Renderer.WaterText = 取界面文本("SearchWatermark", "搜索")
        For Each box In _d2dTextBoxOrder
            box.Renderer.ForeColor = ForeColor
            box.Renderer.SelectionColor = D2DElementBackColor
            box.Renderer.Draw(rt, tfc, brushCache)
        Next

        绘制HTML列表文字(rt, compositor)
        绘制数值标签(rt, compositor, flagsRight)
        绘制收藏夹标题文字(rt, compositor)

        绘制按钮文字(rt, compositor, l.ButtonEyeDropper, If(_eyeDropperActive, 取界面文本("EyeDropperCancel", "按 Esc 取消"), 取界面文本("EyeDropper", "取色器")))
        绘制按钮文字(rt, compositor, l.ButtonTips, 取界面文本("Tips", "使用技巧"))
        绘制按钮文字(rt, compositor, l.ButtonCopyArgb, 取界面文本("CopyArgb", "复制 ARGB"))
        绘制按钮文字(rt, compositor, l.ButtonCopyHex, 取界面文本("CopyHex", "复制 HEX"))
        绘制按钮文字(rt, compositor, l.ButtonOK, 取界面文本("OK", "确定"))
        绘制按钮文字(rt, compositor, l.ButtonCancel, 取界面文本("Cancel", "取消"))

        For i = 0 To 9
            Dim textColor = If(_favoriteSet(i), 收藏夹文字颜色(_favoriteColors(i)), Color.Gray)
            D2DTextRenderer.DrawText(rt, (i + 1).ToString(CultureInfo.InvariantCulture), Font, l.FavoriteRects(i), textColor, flagsCenter, s, tfc, brushCache)
        Next
    End Sub

    Private Sub 绘制D2D色域位图(rt As D2D.ID2D1RenderTarget, compositor As WindowCompositor)
        Dim l = _d2dLayout
        If l Is Nothing Then Return
        Dim clipRect = l.ChromaticityRect
        If clipRect.Width <= 0 OrElse clipRect.Height <= 0 Then Return

        SyncLock _chromaticityBitmapLock
            Dim bmp = _chromaticityBitmap
            If bmp Is Nothing Then Return

            Dim bmpW As Integer
            Dim bmpH As Integer
            Try
                bmpW = bmp.Width
                bmpH = bmp.Height
            Catch ex As ArgumentException
                _chromaticityBitmapCache.Invalidate()
                Return
            End Try
            If bmpW <= 0 OrElse bmpH <= 0 Then Return

            Dim d2dBitmap As D2D.ID2D1Bitmap = Nothing
            Try
                d2dBitmap = _chromaticityBitmapCache.GetBitmap(rt, bmp)
            Catch ex As ArgumentException
                _chromaticityBitmapCache.Invalidate()
                Return
            End Try
            If d2dBitmap IsNot Nothing Then
                rt.DrawBitmap(
                    d2dBitmap,
                    D2DGlobals.ToD2DRect(clipRect),
                    1.0F,
                    D2D.BitmapInterpolationMode.Linear,
                    D2DGlobals.ToD2DRect(New RectangleF(0, 0, bmpW, bmpH)))
            End If
        End SyncLock
    End Sub

    Private Sub 绘制D2D准心(rt As D2D.ID2D1RenderTarget, rect As RectangleF, brushCache As D2DGlobals.SolidColorBrushCache)
        If rect.Width < 10 OrElse rect.Height < 10 Then Return
        Dim pt = 色度坐标转矩形像素(_markerX, _markerY, rect)
        Dim lum = 0.299 * SelectedColor.R + 0.587 * SelectedColor.G + 0.114 * SelectedColor.B
        Dim invColor As Color = If(lum > 128, Color.Black, Color.White)
        Dim crossSize As Single = 12.0F * 取D2D缩放()
        Dim gap As Single = 4.0F * 取D2D缩放()
        Dim brush = brushCache.Get(rt, invColor)
        If brush Is Nothing Then Return
        rt.DrawLine(New Vector2(pt.X - crossSize, pt.Y), New Vector2(pt.X - gap, pt.Y), brush, 1.5F * 取D2D缩放())
        rt.DrawLine(New Vector2(pt.X + gap, pt.Y), New Vector2(pt.X + crossSize, pt.Y), brush, 1.5F * 取D2D缩放())
        rt.DrawLine(New Vector2(pt.X, pt.Y - crossSize), New Vector2(pt.X, pt.Y - gap), brush, 1.5F * 取D2D缩放())
        rt.DrawLine(New Vector2(pt.X, pt.Y + gap), New Vector2(pt.X, pt.Y + crossSize), brush, 1.5F * 取D2D缩放())

        brush = brushCache.Get(rt, Color.FromArgb(120, invColor))
        If brush Is Nothing Then Return
        rt.DrawEllipse(New D2D.Ellipse(New Vector2(pt.X, pt.Y), crossSize, crossSize), brush, 1.0F * 取D2D缩放())
    End Sub

    Private Sub 绘制HTML列表图形(rt As D2D.ID2D1RenderTarget, brushCache As D2DGlobals.SolidColorBrushCache)
        Dim clip = 取HTML列表视口矩形()
        If clip.Width > 0 AndAlso clip.Height > 0 Then
            rt.PushAxisAlignedClip(New Vortice.RawRectF(clip.Left, clip.Top, clip.Right, clip.Bottom), D2D.AntialiasMode.PerPrimitive)
            Try
                Dim firstIndex = HTML首项索引()
                For row = 0 To 可绘制HTML项数量() - 1
                    Dim idx = firstIndex + row
                    If idx < 0 OrElse idx >= _d2dFilteredHtmlColors.Count Then Exit For
                    Dim itemRect = 取HTML列表项矩形(row)
                    If idx = _d2dSelectedHtmlIndex Then
                        填充圆角矩形(rt, itemRect, D2DElementPressedBackColor, 6.0F * 取D2D缩放(), brushCache)
                    ElseIf idx = _d2dHoverHtmlIndex Then
                        填充圆角矩形(rt, itemRect, D2DElementHoverBackColor, 6.0F * 取D2D缩放(), brushCache)
                    End If
                    Dim swatchSize As Single = 16.0F * 取D2D缩放()
                    Dim swatchPadding As Single = Math.Max(0.0F, (itemRect.Height - swatchSize) / 2.0F)
                    Dim swatchRect As New RectangleF(itemRect.X + swatchPadding,
                                                     itemRect.Y + swatchPadding,
                                                     swatchSize,
                                                     swatchSize)
                    Dim c = _d2dFilteredHtmlColors(idx).Value
                    填充圆角矩形(rt, swatchRect, c, 3.0F * 取D2D缩放(), brushCache)
                Next
            Finally
                rt.PopAxisAlignedClip()
            End Try
        End If
        绘制HTML滚动条(rt, brushCache)
    End Sub

    Private Sub 绘制HTML列表文字(rt As D2D.ID2D1RenderTarget, compositor As WindowCompositor)
        Dim flags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        Dim s = 取D2D缩放()
        Dim clip = 取HTML列表视口矩形()
        If clip.Width <= 0 OrElse clip.Height <= 0 Then Return
        rt.PushAxisAlignedClip(New Vortice.RawRectF(clip.Left, clip.Top, clip.Right, clip.Bottom), D2D.AntialiasMode.PerPrimitive)
        Try
            Dim firstIndex = HTML首项索引()
            For row = 0 To 可绘制HTML项数量() - 1
                Dim idx = firstIndex + row
                If idx < 0 OrElse idx >= _d2dFilteredHtmlColors.Count Then Exit For
                Dim itemRect = 取HTML列表项矩形(row)
                Dim swatchSize As Single = 16.0F * s
                Dim swatchPadding As Single = Math.Max(0.0F, (itemRect.Height - swatchSize) / 2.0F)
                Dim textX As Single = itemRect.X + swatchPadding + swatchSize + swatchPadding
                Dim textRect As New RectangleF(textX, itemRect.Y, Math.Max(1.0F, itemRect.Right - textX - swatchPadding), itemRect.Height)
                D2DTextRenderer.DrawText(rt, _d2dFilteredHtmlColors(idx).Key, Font, textRect, ForeColor, flags, s, compositor.TextFormatCache, compositor.BrushCache)
            Next
        Finally
            rt.PopAxisAlignedClip()
        End Try
    End Sub

    Private Sub 绘制数值标签(rt As D2D.ID2D1RenderTarget, compositor As WindowCompositor, flagsRight As TextFormatFlags)
        For Each kind In {ColorDialogTextBoxKind.R, ColorDialogTextBoxKind.G, ColorDialogTextBoxKind.B,
                          ColorDialogTextBoxKind.H, ColorDialogTextBoxKind.S, ColorDialogTextBoxKind.L,
                          ColorDialogTextBoxKind.A, ColorDialogTextBoxKind.Hex}
            Dim box = _d2dTextBoxes(kind)
            D2DTextRenderer.DrawText(rt, 取数值标签文本(kind), Font, box.LabelRect, ForeColor, flagsRight, 取D2D缩放(), compositor.TextFormatCache, compositor.BrushCache)
        Next
    End Sub

    Private Sub 绘制当前颜色预览(rt As D2D.ID2D1RenderTarget, brushCache As D2DGlobals.SolidColorBrushCache)
        Dim r = _d2dLayout.Preview
        填充圆角矩形(rt, r, SelectedColor, _d2dLayout.Radius, brushCache)
    End Sub

    Private Sub 绘制收藏夹(rt As D2D.ID2D1RenderTarget, brushCache As D2DGlobals.SolidColorBrushCache)
        For i = 0 To 9
            Dim r = _d2dLayout.FavoriteRects(i)
            Dim fill = If(_favoriteSet(i), Color.FromArgb(_favoriteColors(i).R, _favoriteColors(i).G, _favoriteColors(i).B), D2DFavoriteSlotBackColor)
            If i = _d2dPressedFavorite Then
                fill = 混合颜色(fill, Color.White, 0.18F)
            ElseIf i = _d2dHoverFavorite Then
                fill = 混合颜色(fill, Color.White, 0.1F)
            End If
            填充圆角矩形(rt, r, fill, _d2dLayout.Radius, brushCache)
        Next
    End Sub

    Private Sub 绘制收藏夹标题文字(rt As D2D.ID2D1RenderTarget, compositor As WindowCompositor)
        Dim l = _d2dLayout
        If l Is Nothing Then Return
        Dim s = 取D2D缩放()
        Dim flagsMain As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        Dim flagsHint As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Bottom Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        D2DTextRenderer.DrawText(rt, 取界面文本("FavoritesTitle", "收藏夹"), Font, l.FavoriteTitleMain, ForeColor, flagsMain, s, compositor.TextFormatCache, compositor.BrushCache)
        Using hintFont As New Font(Font.Name, Math.Max(1.0F, Font.Size - 1.2F), FontStyle.Regular, GraphicsUnit.Point)
            D2DTextRenderer.DrawText(rt,
                                     取界面文本("FavoritesHint", "左键读取，右键写入，中键清除，仅保留在当前应用程序运行周期内"),
                                     hintFont,
                                     l.FavoriteTitleHint,
                                     Color.Gray,
                                     flagsHint,
                                     s,
                                     compositor.TextFormatCache,
                                     compositor.BrushCache)
        End Using
    End Sub

    Private Sub 绘制HTML滚动条(rt As D2D.ID2D1RenderTarget, brushCache As D2DGlobals.SolidColorBrushCache)
        If Not 需要HTML滚动条() Then Return
        Dim track = _d2dLayout.HtmlScrollBar
        Dim thumb = 计算HTML滚动滑块()
        If track.Width <= 0 OrElse track.Height <= 0 OrElse thumb.Width <= 0 OrElse thumb.Height <= 0 Then Return
        Dim radius As Single = Math.Max(1.0F, track.Width / 2.0F)
        填充圆角矩形(rt, track, Color.FromArgb(30, 220, 220, 220), radius, brushCache)
        填充圆角矩形(rt, thumb, Color.FromArgb(120, 220, 220, 220), radius, brushCache)
    End Sub

    Private Sub 绘制按钮(rt As D2D.ID2D1RenderTarget, rect As RectangleF, kind As ColorDialogButtonKind, brushCache As D2DGlobals.SolidColorBrushCache)
        Dim fill = D2DElementBackColor
        If _d2dPressedButton = kind Then
            fill = D2DElementPressedBackColor
        ElseIf _d2dHoverButton = kind Then
            fill = D2DElementHoverBackColor
        End If
        填充圆角矩形(rt, rect, fill, _d2dLayout.Radius, brushCache)
    End Sub

    Private Sub 绘制按钮文字(rt As D2D.ID2D1RenderTarget, compositor As WindowCompositor, rect As RectangleF, text As String)
        Dim flags As TextFormatFlags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine
        D2DTextRenderer.DrawText(rt, text, Font, rect, ForeColor, flags, 取D2D缩放(), compositor.TextFormatCache, compositor.BrushCache)
    End Sub

    Private Sub 填充圆角矩形(rt As D2D.ID2D1RenderTarget, rect As RectangleF, color As Color, radius As Single, brushCache As D2DGlobals.SolidColorBrushCache)
        If color.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Using geo = RectangleRenderer.创建圆角矩形几何(rect, radius)
            RectangleRenderer.绘制圆角背景_D2D(rt, geo, rect, color, Color.Empty, Orientation.Horizontal, brushCache)
        End Using
    End Sub

    Private Sub 描绘圆角边框(rt As D2D.ID2D1RenderTarget, rect As RectangleF, color As Color, width As Single, radius As Single, brushCache As D2DGlobals.SolidColorBrushCache)
        If color.A = 0 OrElse width <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim half = width / 2.0F
        rect.Inflate(-half, -half)
        RectangleRenderer.绘制圆角边框_D2D(rt, rect, radius, color, width, brushCache)
    End Sub

#End Region

#Region "鼠标和键盘"

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        初始化D2D颜色对话框()
        确保D2D布局()
        Focus()

        Dim tb = 命中文本框(e.Location)
        If tb IsNot Nothing Then
            设置活动文本框(tb)
            tb.Renderer.BeginMouseSelection(e.X)
            _d2dMouseTextBox = tb
            _d2dCapturePart = ColorDialogHitPart.TextBox
            Capture = True
            Return
        End If

        Dim labelBox = 命中数值标签(e.Location)
        If labelBox IsNot Nothing AndAlso e.Button = MouseButtons.Left Then
            设置活动文本框(labelBox)
            _d2dDragTextBox = labelBox
            _d2dDragStartY = e.Y
            Dim v As Integer
            Integer.TryParse(labelBox.Renderer.Text, v)
            _d2dDragStartValue = v
            _d2dCapturePart = ColorDialogHitPart.NumericLabel
            Capture = True
            Return
        End If

        If _d2dLayout.ChromaticityRect.Contains(e.Location) AndAlso (e.Button = MouseButtons.Left OrElse e.Button = MouseButtons.Right) Then
            设置活动文本框(Nothing)
            _d2dCapturePart = ColorDialogHitPart.Chromaticity
            Capture = True
            从D2D色域图取色(e.X, e.Y, e.Button <> MouseButtons.Right)
            Return
        End If

        Dim fav = 命中收藏夹(e.Location)
        If fav >= 0 Then
            _d2dPressedFavorite = fav
            处理收藏夹鼠标按下(fav, e.Button)
            Invalidate()
            Return
        End If

        If e.Button = MouseButtons.Left AndAlso 命中HTML滚动条(e.Location) Then
            设置活动文本框(Nothing)
            Dim thumb = 计算HTML滚动滑块()
            _d2dHtmlScrollDragOffset = If(thumb.Contains(e.Location), e.Y - thumb.Y, thumb.Height / 2.0F)
            滚动HTML到滑块位置(e.Y - _d2dHtmlScrollDragOffset)
            _d2dCapturePart = ColorDialogHitPart.HtmlScrollBar
            Capture = True
            Invalidate()
            Return
        End If

        Dim htmlIdx = 命中HTML列表项(e.Location)
        If htmlIdx >= 0 Then
            选择D2DHTML索引(htmlIdx)
            Return
        End If

        Dim btn = 命中按钮(e.Location)
        If btn <> ColorDialogButtonKind.None AndAlso e.Button = MouseButtons.Left Then
            设置活动文本框(Nothing)
            _d2dPressedButton = btn
            _d2dCapturePart = ColorDialogHitPart.Button
            Capture = True
            Invalidate()
            Return
        End If

        设置活动文本框(Nothing)
        MyBase.OnMouseDown(e)
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        确保D2D布局()
        Select Case _d2dCapturePart
            Case ColorDialogHitPart.Chromaticity
                If e.Button = MouseButtons.Left OrElse e.Button = MouseButtons.Right Then
                    从D2D色域图取色(e.X, e.Y, e.Button <> MouseButtons.Right)
                End If
            Case ColorDialogHitPart.TextBox
                If _d2dMouseTextBox IsNot Nothing Then _d2dMouseTextBox.Renderer.UpdateMouseSelection(e.X)
            Case ColorDialogHitPart.NumericLabel
                If _d2dDragTextBox IsNot Nothing Then
                    Dim dy = _d2dDragStartY - e.Y
                    Dim newValue = Math.Clamp(_d2dDragStartValue + dy, _d2dDragTextBox.Minimum, _d2dDragTextBox.Maximum)
                    _d2dDragTextBox.Renderer.Text = newValue.ToString(CultureInfo.InvariantCulture)
                End If
            Case ColorDialogHitPart.HtmlScrollBar
                If e.Button = MouseButtons.Left Then 滚动HTML到滑块位置(e.Y - _d2dHtmlScrollDragOffset)
            Case Else
                更新D2D悬停状态(e.Location)
        End Select
        MyBase.OnMouseMove(e)
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        Dim pressed = _d2dPressedButton
        Dim releaseBtn = 命中按钮(e.Location)
        _d2dCapturePart = ColorDialogHitPart.None
        _d2dMouseTextBox = Nothing
        _d2dDragTextBox = Nothing
        _d2dPressedButton = ColorDialogButtonKind.None
        _d2dPressedFavorite = -1
        Capture = False
        If pressed <> ColorDialogButtonKind.None AndAlso pressed = releaseBtn Then 触发D2D按钮(pressed)
        更新D2D悬停状态(e.Location)
        Invalidate()
        MyBase.OnMouseUp(e)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        _d2dHoverButton = ColorDialogButtonKind.None
        _d2dHoverFavorite = -1
        _d2dHoverHtmlIndex = -1
        Invalidate()
        MyBase.OnMouseLeave(e)
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        确保D2D布局()
        If _d2dLayout.HtmlList.Contains(e.Location) Then
            _d2dHtmlScrollIndex += CSng(-e.Delta) / 120.0F * 2.5F
            限制HTML滚动()
            刷新HTML列表区域()
            Return
        End If
        Dim tb = 命中文本框(e.Location)
        If tb Is Nothing Then tb = 命中数值标签(e.Location)
        If tb IsNot Nothing AndAlso tb.IsNumeric Then
            按增量调整D2D文本框(tb, e.Delta)
            Return
        End If
        If _d2dActiveTextBox IsNot Nothing AndAlso _d2dActiveTextBox.IsNumeric Then
            按增量调整D2D文本框(_d2dActiveTextBox, e.Delta)
            Return
        End If
        MyBase.OnMouseWheel(e)
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        If 处理D2D按键(e) Then Return
        MyBase.OnKeyDown(e)
    End Sub

    Protected Overrides Sub WndProc(ByRef m As Message)
        If 处理D2D窗口消息(m) Then Return
        MyBase.WndProc(m)
    End Sub

    Private Function 处理D2D快捷键(ByRef msg As Message, keyData As Keys) As Boolean
        If _eyeDropperActive AndAlso keyData = Keys.Escape Then
            停止取色器()
            Return True
        End If
        If _d2dActiveTextBox Is Nothing Then
            If keyData = Keys.Enter Then
                DialogResult = DialogResult.OK
                Close()
                Return True
            End If
            If keyData = Keys.Escape Then
                DialogResult = DialogResult.Cancel
                Close()
                Return True
            End If
            Return False
        End If

        Dim r = _d2dActiveTextBox.Renderer
        Select Case keyData
            Case Keys.Left
                r.MoveCaret(-1, False) : Return True
            Case Keys.Right
                r.MoveCaret(1, False) : Return True
            Case Keys.Shift Or Keys.Left
                r.MoveCaret(-1, True) : Return True
            Case Keys.Shift Or Keys.Right
                r.MoveCaret(1, True) : Return True
            Case Keys.Control Or Keys.Left
                r.MoveCaretWordLeft(False) : Return True
            Case Keys.Control Or Keys.Right
                r.MoveCaretWordRight(False) : Return True
            Case Keys.Control Or Keys.Shift Or Keys.Left
                r.MoveCaretWordLeft(True) : Return True
            Case Keys.Control Or Keys.Shift Or Keys.Right
                r.MoveCaretWordRight(True) : Return True
            Case Keys.Home
                r.MoveCaretHome(False) : Return True
            Case Keys.End
                r.MoveCaretEnd(False) : Return True
            Case Keys.Shift Or Keys.Home
                r.MoveCaretHome(True) : Return True
            Case Keys.Shift Or Keys.End
                r.MoveCaretEnd(True) : Return True
            Case Keys.Delete
                r.HandleDelete() : Return True
            Case Keys.Back
                r.HandleBackspace() : Return True
            Case Keys.Control Or Keys.A
                r.SelectAll() : Return True
            Case Keys.Control Or Keys.C
                r.CopySelection() : Return True
            Case Keys.Control Or Keys.X
                r.CutSelection() : Return True
            Case Keys.Control Or Keys.V
                r.PasteText() : Return True
            Case Keys.Tab
                聚焦下一个文本框(False) : Return True
            Case Keys.Shift Or Keys.Tab
                聚焦下一个文本框(True) : Return True
            Case Keys.Enter
                If _d2dActiveTextBox.Kind = ColorDialogTextBoxKind.Search Then Return True
                DialogResult = DialogResult.OK : Close() : Return True
            Case Keys.Escape
                设置活动文本框(Nothing) : Return True
        End Select
        Return False
    End Function

    Private Function 处理D2D按键(e As KeyEventArgs) As Boolean
        If _d2dActiveTextBox IsNot Nothing AndAlso _d2dActiveTextBox.IsNumeric Then
            If e.KeyCode = Keys.Up Then
                按增量调整D2D文本框(_d2dActiveTextBox, 120)
                Return True
            ElseIf e.KeyCode = Keys.Down Then
                按增量调整D2D文本框(_d2dActiveTextBox, -120)
                Return True
            End If
        End If
        Return False
    End Function

    Private Function 处理D2D窗口消息(ByRef m As Message) As Boolean
        Select Case m.Msg
            Case WM_ENTERSIZEMOVE_D2D
                _d2dInSizeMove = True
                Return False
            Case WM_EXITSIZEMOVE_D2D
                _d2dInSizeMove = False
                _d2dLayoutDirty = True
                调整大小结束D2D()
                Return False
            Case WM_GETDLGCODE_D2D
                m.Result = New IntPtr(DLGC_WANTCHARS_D2D Or DLGC_WANTALLKEYS_D2D)
                Return True
            Case WM_IME_STARTCOMPOSITION_D2D
                _d2dImeComposing = True
                更新D2D输入法窗口()
                Return False
            Case WM_IME_ENDCOMPOSITION_D2D
                _d2dImeComposing = False
                Return False
            Case WM_IME_COMPOSITION_D2D
                Dim lp As Integer = m.LParam.ToInt32()
                更新D2D输入法窗口()
                If (lp And GCS_RESULTSTR_D2D) <> 0 AndAlso _d2dActiveTextBox IsNot Nothing Then
                    Dim result As String = ImeHelper.GetResultString(Handle)
                    If result IsNot Nothing Then _d2dActiveTextBox.Renderer.InsertText(result)
                    Return True
                End If
            Case WM_CHAR_D2D
                If Not _d2dImeComposing Then
                    处理D2D字符输入(m.WParam.ToInt32())
                    Return True
                End If
        End Select
        Return False
    End Function

    Private Sub 处理D2D字符输入(charCode As Integer)
        If _d2dActiveTextBox Is Nothing Then Return
        Dim r = _d2dActiveTextBox.Renderer
        Select Case charCode
            Case 1
                r.SelectAll()
            Case 3
                r.CopySelection()
            Case 22
                r.PasteText()
            Case 24
                r.CutSelection()
            Case 8
                r.HandleBackspace()
            Case Else
                Dim ch As Char = ChrW(charCode)
                If Not Char.IsControl(ch) Then r.InsertText(ch.ToString())
        End Select
        r.ResetCaretBlink()
    End Sub

#End Region

#Region "交互辅助"

    Private Sub 更新D2D悬停状态(p As Point)
        Dim oldButton = _d2dHoverButton
        Dim oldFav = _d2dHoverFavorite
        Dim oldHtml = _d2dHoverHtmlIndex
        _d2dHoverButton = 命中按钮(p)
        _d2dHoverFavorite = 命中收藏夹(p)
        _d2dHoverHtmlIndex = 命中HTML列表项(p)
        Dim tb = 命中文本框(p)
        Dim labelTb = 命中数值标签(p)
        Dim htmlScrollHit = 命中HTML滚动条(p)
        If tb IsNot Nothing Then
            Cursor = Cursors.IBeam
        ElseIf labelTb IsNot Nothing Then
            Cursor = Cursors.SizeNS
        ElseIf _d2dHoverButton <> ColorDialogButtonKind.None OrElse _d2dHoverFavorite >= 0 OrElse _d2dHoverHtmlIndex >= 0 OrElse htmlScrollHit OrElse _d2dLayout.ChromaticityRect.Contains(p) Then
            Cursor = Cursors.Hand
        Else
            Cursor = Cursors.Default
        End If
        If oldButton <> _d2dHoverButton OrElse oldFav <> _d2dHoverFavorite OrElse oldHtml <> _d2dHoverHtmlIndex Then Invalidate()
    End Sub

    Private Function 命中文本框(p As Point) As VirtualTextBox
        For Each box In _d2dTextBoxOrder
            If box.Bounds.Contains(p) Then Return box
        Next
        Return Nothing
    End Function

    Private Function 命中数值标签(p As Point) As VirtualTextBox
        For Each box In _d2dTextBoxOrder
            Dim hitRect = box.LabelHitRect
            If hitRect.IsEmpty Then hitRect = box.LabelRect
            If box.IsNumeric AndAlso hitRect.Contains(p) Then Return box
        Next
        Return Nothing
    End Function

    Private Function 命中收藏夹(p As Point) As Integer
        If _d2dLayout Is Nothing Then Return -1
        For i = 0 To 9
            If _d2dLayout.FavoriteRects(i).Contains(p) Then Return i
        Next
        Return -1
    End Function

    Private Function 命中按钮(p As Point) As ColorDialogButtonKind
        If _d2dLayout Is Nothing Then Return ColorDialogButtonKind.None
        If _d2dLayout.ButtonEyeDropper.Contains(p) Then Return ColorDialogButtonKind.EyeDropper
        If _d2dLayout.ButtonTips.Contains(p) Then Return ColorDialogButtonKind.Tips
        If _d2dLayout.ButtonCopyArgb.Contains(p) Then Return ColorDialogButtonKind.CopyArgb
        If _d2dLayout.ButtonCopyHex.Contains(p) Then Return ColorDialogButtonKind.CopyHex
        If _d2dLayout.ButtonOK.Contains(p) Then Return ColorDialogButtonKind.OK
        If _d2dLayout.ButtonCancel.Contains(p) Then Return ColorDialogButtonKind.Cancel
        Return ColorDialogButtonKind.None
    End Function

    Private Function 命中HTML列表项(p As Point) As Integer
        If _d2dLayout Is Nothing OrElse Not 取HTML列表视口矩形().Contains(p) Then Return -1
        If 命中HTML滚动条(p) Then Return -1
        Dim inset As Single = 5.0F * 取D2D缩放()
        Dim row = CInt(Math.Floor((p.Y - _d2dLayout.HtmlList.Y - inset + HTML滚动小数偏移() * _d2dLayout.ItemHeight) / _d2dLayout.ItemHeight))
        If row < 0 Then Return -1
        Dim idx = HTML首项索引() + row
        If idx < 0 OrElse idx >= _d2dFilteredHtmlColors.Count Then Return -1
        Return idx
    End Function

    Private Function 命中HTML滚动条(p As Point) As Boolean
        If Not 需要HTML滚动条() Then Return False
        Return _d2dLayout.HtmlScrollBar.Contains(p)
    End Function

    Private Function 取HTML列表项矩形(row As Integer) As RectangleF
        Dim viewport = 取HTML列表视口矩形()
        Return New RectangleF(viewport.X,
                              _d2dLayout.HtmlList.Y + 5.0F * 取D2D缩放() + (row - HTML滚动小数偏移()) * _d2dLayout.ItemHeight,
                              viewport.Width,
                              _d2dLayout.ItemHeight)
    End Function

    Private Function 取HTML列表视口矩形() As RectangleF
        If _d2dLayout Is Nothing Then Return RectangleF.Empty
        Dim inset As Single = 5.0F * 取D2D缩放()
        Dim rightEdge As Single = _d2dLayout.HtmlList.Right - inset
        If 需要HTML滚动条() Then rightEdge = _d2dLayout.HtmlScrollBar.Left - inset
        Return New RectangleF(_d2dLayout.HtmlList.X + inset,
                              _d2dLayout.HtmlList.Y + inset,
                              Math.Max(1.0F, rightEdge - (_d2dLayout.HtmlList.X + inset)),
                              Math.Max(1.0F, _d2dLayout.HtmlList.Height - inset * 2.0F))
    End Function

    Private Function 可见HTML项数量() As Integer
        If _d2dLayout Is Nothing Then Return 0
        Return Math.Max(0, CInt(Math.Floor((_d2dLayout.HtmlList.Height - 10.0F * 取D2D缩放()) / _d2dLayout.ItemHeight)))
    End Function

    Private Function 可绘制HTML项数量() As Integer
        If _d2dLayout Is Nothing Then Return 0
        Dim viewport = 取HTML列表视口矩形()
        Dim count = CInt(Math.Ceiling(viewport.Height / Math.Max(1.0F, _d2dLayout.ItemHeight))) + 1
        Return Math.Max(0, count)
    End Function

    Private Function HTML首项索引() As Integer
        Return CInt(Math.Floor(Math.Max(0.0F, _d2dHtmlScrollIndex)))
    End Function

    Private Function HTML滚动小数偏移() As Single
        Return Math.Max(0.0F, _d2dHtmlScrollIndex - HTML首项索引())
    End Function

    Private Function 需要HTML滚动条() As Boolean
        If _d2dLayout Is Nothing Then Return False
        Return _d2dFilteredHtmlColors.Count > Math.Max(0, 可见HTML项数量())
    End Function

    Private Function 计算HTML滚动滑块() As RectangleF
        If _d2dLayout Is Nothing Then Return RectangleF.Empty
        Dim track = _d2dLayout.HtmlScrollBar
        Dim visible = Math.Max(1, 可见HTML项数量())
        Dim total = Math.Max(visible, _d2dFilteredHtmlColors.Count)
        If track.Width <= 0 OrElse track.Height <= 0 OrElse total <= visible Then Return RectangleF.Empty

        Dim s = 取D2D缩放()
        Dim thumbH As Single = Math.Max(18.0F * s, track.Height * visible / total)
        thumbH = Math.Min(track.Height, thumbH)
        Dim maxScroll As Single = Math.Max(1.0F, CSng(total - visible))
        Dim usableH As Single = Math.Max(0.0F, track.Height - thumbH)
        Dim y As Single = track.Y + usableH * Math.Clamp(_d2dHtmlScrollIndex, 0, maxScroll) / maxScroll
        Return New RectangleF(track.X, y, track.Width, thumbH)
    End Function

    Private Sub 滚动HTML到滑块位置(thumbTop As Single)
        If Not 需要HTML滚动条() Then Return
        Dim track = _d2dLayout.HtmlScrollBar
        Dim thumb = 计算HTML滚动滑块()
        Dim visible = Math.Max(1, 可见HTML项数量())
        Dim maxScroll As Single = Math.Max(0.0F, CSng(_d2dFilteredHtmlColors.Count - visible))
        Dim usableH As Single = Math.Max(1.0F, track.Height - thumb.Height)
        Dim ratio As Single = Math.Clamp((thumbTop - track.Y) / usableH, 0.0F, 1.0F)
        _d2dHtmlScrollIndex = maxScroll * ratio
        限制HTML滚动()
        刷新HTML列表区域()
    End Sub

    Private Sub 刷新HTML列表区域()
        If _d2dLayout Is Nothing Then
            Invalidate()
            Return
        End If
        Dim r = Rectangle.Ceiling(_d2dLayout.HtmlList)
        r.Inflate(2, 2)
        Invalidate(Rectangle.Intersect(ClientRectangle, r), False)
    End Sub

    Private Sub 设置活动文本框(box As VirtualTextBox)
        If ReferenceEquals(_d2dActiveTextBox, box) Then Return
        If _d2dActiveTextBox IsNot Nothing Then _d2dActiveTextBox.Renderer.StopCaretBlink()
        _d2dActiveTextBox = box
        If _d2dActiveTextBox IsNot Nothing Then
            _d2dActiveTextBox.Renderer.StartCaretBlink()
            更新D2D输入法窗口()
        End If
        Invalidate()
    End Sub

    Private Sub 聚焦下一个文本框(reverse As Boolean)
        If _d2dTextBoxOrder.Count = 0 Then Return
        Dim idx = If(_d2dActiveTextBox Is Nothing, -1, _d2dTextBoxOrder.IndexOf(_d2dActiveTextBox))
        If reverse Then
            idx -= 1
            If idx < 0 Then idx = _d2dTextBoxOrder.Count - 1
        Else
            idx += 1
            If idx >= _d2dTextBoxOrder.Count Then idx = 0
        End If
        设置活动文本框(_d2dTextBoxOrder(idx))
    End Sub

    Private Sub 按增量调整D2D文本框(box As VirtualTextBox, wheelDelta As Integer)
        If box Is Nothing OrElse Not box.IsNumeric Then Return
        Dim v As Integer
        If Not Integer.TryParse(box.Renderer.Text, v) Then Return
        v = Math.Clamp(v + Math.Sign(wheelDelta), box.Minimum, box.Maximum)
        box.Renderer.Text = v.ToString(CultureInfo.InvariantCulture)
    End Sub

    Private Sub 触发D2D按钮(kind As ColorDialogButtonKind)
        Select Case kind
            Case ColorDialogButtonKind.EyeDropper
                切换取色器()
            Case ColorDialogButtonKind.Tips
                Me.ModernContextMenu1.MenuFont = Me.Font
                Me.ModernContextMenu1.DescriptionFont = New Font(Me.Font.Name, 9)
                Dim menuPoint = If(_d2dLayout Is Nothing,
                                   New Point(Me.ModernContextMenu1.MenuPadding.Left, Me.ModernContextMenu1.MenuPadding.Top),
                                   Point.Round(_d2dLayout.ChromaticityTitle.Location))
                Me.ModernContextMenu1.Show(Me, menuPoint.X, menuPoint.Y)
            Case ColorDialogButtonKind.CopyArgb
                复制D2D文本到剪贴板(取当前ARGB文本())
            Case ColorDialogButtonKind.CopyHex
                复制D2D文本到剪贴板(取当前HEX文本())
            Case ColorDialogButtonKind.OK
                DialogResult = DialogResult.OK
                Close()
            Case ColorDialogButtonKind.Cancel
                DialogResult = DialogResult.Cancel
                Close()
        End Select
    End Sub

    Private Sub 复制D2D文本到剪贴板(text As String)
        If String.IsNullOrEmpty(text) Then Return
        Try
            Clipboard.SetText(text)
        Catch
        End Try
    End Sub

    Private Function 取当前ARGB文本() As String
        Dim c = SelectedColor
        If c.A < 255 Then
            Return String.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}, {3}", c.A, c.R, c.G, c.B)
        End If
        Return String.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}", c.R, c.G, c.B)
    End Function

    Private Function 取当前HEX文本() As String
        Dim c = SelectedColor
        If c.A < 255 Then Return String.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B)
        Return String.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B)
    End Function

    Private Sub 处理收藏夹鼠标按下(index As Integer, button As MouseButtons)
        If index < 0 OrElse index > 9 Then Return
        Select Case button
            Case MouseButtons.Left
                If _favoriteSet(index) Then
                    SelectedColor = _favoriteColors(index)
                    应用颜色到界面(SelectedColor, True)
                End If
            Case MouseButtons.Right
                _favoriteColors(index) = SelectedColor
                _favoriteSet(index) = True
            Case MouseButtons.Middle
                _favoriteColors(index) = Color.Black
                _favoriteSet(index) = False
        End Select
        Invalidate()
    End Sub

#End Region

#Region "颜色操作"

    Private Sub 从D2D色域图取色(px As Integer, py As Integer, keepLightness As Boolean)
        确保D2D布局()
        Dim rect = _d2dLayout.ChromaticityRect
        If rect.Width < 10 OrElse rect.Height < 10 Then Return
        Dim localX = CInt(Math.Round(Math.Clamp(px - rect.X, 0, rect.Width - 1)))
        Dim localY = CInt(Math.Round(Math.Clamp(py - rect.Y, 0, rect.Height - 1)))
        Dim xy = 像素转色度坐标(localX, localY, CInt(Math.Round(rect.Width)), CInt(Math.Round(rect.Height)))
        _markerX = xy.Item1
        _markerY = xy.Item2
        限制标记到色域内()

        Dim c As Color
        If _markerY > 0.001 Then
            Dim cX2 = _markerX / _markerY
            Dim cZ = (1.0 - _markerX - _markerY) / _markerY
            c = 色彩XYZ转标准RGB(cX2, 1.0, cZ)
        Else
            c = Color.Black
        End If

        If keepLightness Then
            Dim hsl = 颜色转HSL(c.R, c.G, c.B)
            Dim currentL As Double = 50.0
            Double.TryParse(取文本框文本(ColorDialogTextBoxKind.L), currentL)
            currentL = Math.Clamp(currentL, 0, 100)
            Dim currentS As Double = 0.0
            Double.TryParse(取文本框文本(ColorDialogTextBoxKind.S), currentS)
            If currentS <= 0.001 OrElse currentL <= 0.001 OrElse currentL >= 99.999 Then currentL = 50.0
            Dim rgb = 色相饱和亮度转RGB(hsl.Item1, hsl.Item2, currentL)
            c = Color.FromArgb(255, rgb.Item1, rgb.Item2, rgb.Item3)
        End If

        SelectedColor = Color.FromArgb(解析当前Alpha(), c.R, c.G, c.B)
        应用颜色到界面(SelectedColor, False)
    End Sub

    Private Sub 重建D2DHTML筛选()
        _d2dFilteredHtmlColors.Clear()
        Dim filter = If(取文本框文本(ColorDialogTextBoxKind.Search), String.Empty).Trim()
        For Each kv In _htmlColors
            If String.IsNullOrEmpty(filter) OrElse kv.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) Then
                _d2dFilteredHtmlColors.Add(kv)
            End If
        Next
        高亮D2DHTML颜色(SelectedColor)
        限制HTML滚动()
    End Sub

    Private Sub 高亮D2DHTML颜色(c As Color)
        If Not String.IsNullOrEmpty(_d2dSelectedHtmlName) Then
            Dim selectedColor As Color = Nothing
            If _htmlColorByName.TryGetValue(_d2dSelectedHtmlName, selectedColor) AndAlso
               selectedColor.R = c.R AndAlso selectedColor.G = c.G AndAlso selectedColor.B = c.B Then
                解析选中HTML索引()
                确保HTML选择可见()
                Return
            End If
        End If

        _d2dSelectedHtmlName = Nothing
        _d2dSelectedHtmlIndex = -1
        For i = 0 To _d2dFilteredHtmlColors.Count - 1
            Dim hc = _d2dFilteredHtmlColors(i).Value
            If hc.R = c.R AndAlso hc.G = c.G AndAlso hc.B = c.B Then
                _d2dSelectedHtmlName = _d2dFilteredHtmlColors(i).Key
                _d2dSelectedHtmlIndex = i
                确保HTML选择可见()
                Return
            End If
        Next
    End Sub

    Private Sub 解析选中HTML索引()
        _d2dSelectedHtmlIndex = -1
        If String.IsNullOrEmpty(_d2dSelectedHtmlName) Then Return
        For i = 0 To _d2dFilteredHtmlColors.Count - 1
            If String.Equals(_d2dFilteredHtmlColors(i).Key, _d2dSelectedHtmlName, StringComparison.OrdinalIgnoreCase) Then
                _d2dSelectedHtmlIndex = i
                Exit For
            End If
        Next
    End Sub

    Private Sub 选择D2DHTML索引(index As Integer)
        If index < 0 OrElse index >= _d2dFilteredHtmlColors.Count Then Return
        _d2dSelectedHtmlName = _d2dFilteredHtmlColors(index).Key
        _d2dSelectedHtmlIndex = index
        Dim col = _d2dFilteredHtmlColors(index).Value
        SelectedColor = Color.FromArgb(解析当前Alpha(), col.R, col.G, col.B)
        应用颜色到界面(SelectedColor, True)
    End Sub

    Private Sub 确保HTML选择可见()
        If _d2dSelectedHtmlIndex < 0 Then Return
        Dim visible = 可见HTML项数量()
        If visible <= 0 Then Return
        If _d2dSelectedHtmlIndex < _d2dHtmlScrollIndex Then _d2dHtmlScrollIndex = _d2dSelectedHtmlIndex
        If _d2dSelectedHtmlIndex >= _d2dHtmlScrollIndex + visible Then _d2dHtmlScrollIndex = _d2dSelectedHtmlIndex - visible + 1
        限制HTML滚动()
    End Sub

    Private Sub 限制HTML滚动()
        Dim maxScroll = Math.Max(0, _d2dFilteredHtmlColors.Count - Math.Max(1, 可见HTML项数量()))
        _d2dHtmlScrollIndex = Math.Clamp(_d2dHtmlScrollIndex, 0, maxScroll)
    End Sub

#End Region

#Region "输入过滤"

    Private Shared Function 过滤整数文本(text As String) As String
        If String.IsNullOrEmpty(text) Then Return String.Empty
        Dim chars As New List(Of Char)(text.Length)
        For Each ch In text
            If Char.IsDigit(ch) Then chars.Add(ch)
        Next
        Return New String(chars.ToArray())
    End Function

    Private Shared Function 过滤HEX文本(text As String) As String
        If String.IsNullOrEmpty(text) Then Return String.Empty
        Dim chars As New List(Of Char)(text.Length)
        For Each ch In text.Trim()
            If ch = "#"c OrElse Uri.IsHexDigit(ch) Then chars.Add(Char.ToUpperInvariant(ch))
        Next
        Return New String(chars.ToArray())
    End Function

    Private Shared Function 是否可能为整数文本(candidate As String, minValue As Integer, maxValue As Integer) As Boolean
        If String.IsNullOrEmpty(candidate) Then Return True
        Dim v As Integer
        If Not Integer.TryParse(candidate, v) Then Return False
        Return v >= minValue AndAlso v <= maxValue
    End Function

    Private Shared Function 是否可能为HEX文本(candidate As String) As Boolean
        If String.IsNullOrEmpty(candidate) Then Return True
        Dim s = candidate.Trim()
        If s.StartsWith("#", StringComparison.Ordinal) Then s = s.Substring(1)
        If s.Length > 8 Then Return False
        For Each ch In s
            If Not Uri.IsHexDigit(ch) Then Return False
        Next
        Return True
    End Function

    Private Shared Function 尝试解析HEX颜色(text As String, ByRef color As Color) As Boolean
        Dim hex = If(text, String.Empty).Trim().TrimStart("#"c)
        Try
            If hex.Length = 6 Then
                color = Color.FromArgb(255,
                    Convert.ToInt32(hex.Substring(0, 2), 16),
                    Convert.ToInt32(hex.Substring(2, 2), 16),
                    Convert.ToInt32(hex.Substring(4, 2), 16))
                Return True
            End If
            If hex.Length = 8 Then
                color = Color.FromArgb(
                    Convert.ToInt32(hex.Substring(0, 2), 16),
                    Convert.ToInt32(hex.Substring(2, 2), 16),
                    Convert.ToInt32(hex.Substring(4, 2), 16),
                    Convert.ToInt32(hex.Substring(6, 2), 16))
                Return True
            End If
            If hex.Length = 3 Then
                Dim r = Convert.ToInt32(New String(hex(0), 2), 16)
                Dim g = Convert.ToInt32(New String(hex(1), 2), 16)
                Dim b = Convert.ToInt32(New String(hex(2), 2), 16)
                color = Color.FromArgb(255, r, g, b)
                Return True
            End If
        Catch
        End Try
        Return False
    End Function

#End Region

#Region "辅助方法"

    Private Function 取界面文本(key As String, fallback As String) As String
        If Not String.IsNullOrEmpty(key) Then
            Dim overridden As String = Nothing
            If _d2dTextOverrides.TryGetValue(key, overridden) Then Return overridden
            If D2DTextProvider IsNot Nothing Then
                Dim provided = D2DTextProvider.Invoke(key)
                If provided IsNot Nothing Then Return provided
            End If
        End If
        Return fallback
    End Function

    Private Function 取D2D缩放() As Single
        Return Math.Max(0.01F, DeviceDpi / 96.0F)
    End Function

    Private Function 缩放值(value As Integer) As Integer
        Return CInt(Math.Round(value * 取D2D缩放()))
    End Function

    Private Function 测量界面文本宽度(text As String, font As Font,
                               Optional textFormatCache As D2DGlobals.TextFormatCache = Nothing) As Single
        If String.IsNullOrEmpty(text) OrElse font Is Nothing Then Return 0.0F
        Return CSng(D2DTextRenderer.MeasureWidth(text, font, 取D2D缩放(), textFormatCache))
    End Function

    Private Shared Function 混合颜色(a As Color, b As Color, t As Single) As Color
        t = Math.Clamp(t, 0.0F, 1.0F)
        Return Color.FromArgb(
            CInt(a.A + (b.A - a.A) * t),
            CInt(a.R + (b.R - a.R) * t),
            CInt(a.G + (b.G - a.G) * t),
            CInt(a.B + (b.B - a.B) * t))
    End Function

    Private Shared Function 收藏夹文字颜色(c As Color) As Color
        Dim lum = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B
        Return If(lum > 128, Color.Black, Color.White)
    End Function

    Private Shared Function 色度坐标转矩形像素(cx As Double, cy As Double, rect As RectangleF) As PointF
        Dim p = 色度坐标转像素(cx, cy, Math.Max(1, CInt(Math.Round(rect.Width))), Math.Max(1, CInt(Math.Round(rect.Height))))
        Return New PointF(rect.X + p.X, rect.Y + p.Y)
    End Function

    Private Sub 更新D2D输入法窗口()
        If Not IsHandleCreated OrElse _d2dActiveTextBox Is Nothing Then Return
        Dim p = _d2dActiveTextBox.Renderer.GetCaretImeLocation()
        ImeHelper.SetCompositionPosition(Handle, p.X, p.Y)
    End Sub

#End Region


End Class

