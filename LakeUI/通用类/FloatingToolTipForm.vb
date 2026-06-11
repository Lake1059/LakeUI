Imports Vortice.Direct2D1

''' <summary>
''' 控件共用的自绘浮动提示窗，支持 D2D 文本、边框、圆角以及弹出层毛玻璃背景。
''' </summary>
Friend NotInheritable Class FloatingToolTipForm
    Inherits PopupForm

    Private ReadOnly _owner As Control
    Private _tipText As String = ""
    Private _style As New FloatingToolTipStyle()
    Private _backdrop As PopupBackdropRenderer
    Private _lastMeasureKey As String = Nothing
    Private _lastMeasuredSize As Size = Size.Empty

    Public Sub New(owner As Control)
        _owner = owner
        DoubleBuffered = True
        AutoScaleMode = AutoScaleMode.Dpi
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer, True)
        ApplyPopupWindowState()
    End Sub

    Public Sub ShowTip(text As String, screenLocation As Point, style As FloatingToolTipStyle,
                       Optional overflowFlipDistance As Integer = 0)
        _tipText = If(text, "")
        _style = If(style, New FloatingToolTipStyle()).Clone()

        Dim pad As Padding = NormalizePadding(_style.Padding)
        Dim bw As Integer = BorderWidth()
        Dim maxW As Integer = Math.Max(50, _style.MaxWidth)
        Dim contentW As Integer = maxW - pad.Left - pad.Right - bw * 2
        If contentW < 10 Then contentW = 10

        Dim displayFont = TipFont()
        Dim measureKey As String = String.Concat(_tipText, ChrW(0), contentW, ChrW(0), displayFont.GetHashCode(), ChrW(0), OwnerDpiScale())
        Dim measured As Size
        If String.Equals(_lastMeasureKey, measureKey, StringComparison.Ordinal) Then
            measured = _lastMeasuredSize
        Else
            measured = MeasureWrappedText(_tipText, displayFont, contentW)
            _lastMeasureKey = measureKey
            _lastMeasuredSize = measured
        End If

        Dim w As Integer = Math.Min(maxW, Math.Max(1, measured.Width + pad.Left + pad.Right + bw * 2))
        Dim h As Integer = Math.Max(1, measured.Height + pad.Top + pad.Bottom + bw * 2)
        Size = New Size(w, h)
        ApplyRoundedRegion()

        Dim scr As Screen = Screen.FromPoint(screenLocation)
        Dim loc As Point = screenLocation
        If loc.X + w > scr.WorkingArea.Right Then
            If overflowFlipDistance > 0 Then
                loc.X = screenLocation.X - w - overflowFlipDistance
            Else
                loc.X = scr.WorkingArea.Right - w
            End If
            If loc.X < scr.WorkingArea.Left Then loc.X = scr.WorkingArea.Left
        End If
        If loc.Y + h > scr.WorkingArea.Bottom Then loc.Y = scr.WorkingArea.Bottom - h
        If loc.Y < scr.WorkingArea.Top Then loc.Y = scr.WorkingArea.Top
        Location = loc

        ApplyPopupWindowState()
        准备毛玻璃背景()
        If Not Visible Then Show()
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' 顶层提示窗体由 OnPaint 中的毛玻璃与 D2D 绘制接管底色。
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        绘制毛玻璃背景(e.Graphics)

        Dim w As Integer = ClientSize.Width
        Dim h As Integer = ClientSize.Height
        If w <= 0 OrElse h <= 0 Then Return

        Dim bw As Integer = BorderWidth()
        Dim pad As Padding = NormalizePadding(_style.Padding)

        Using scope = D2DHelperV2.BeginPaint(e, Me, 1)
            If scope Is Nothing Then Return
            Dim rt As ID2D1RenderTarget = scope.GraphicsLayer
            Dim brushCache = scope.Compositor.BrushCache

            DrawBackground_D2D(rt, brushCache, bw, w, h, Not HasBackdropFrame())
            scope.FlushGraphics()

            Dim textRect As New RectangleF(bw + pad.Left, bw + pad.Top,
                                           w - bw * 2 - pad.Left - pad.Right,
                                           h - bw * 2 - pad.Top - pad.Bottom)
            D2DTextRenderer.DrawText(scope.DCRenderTarget, _tipText, TipFont(), textRect, _style.ForeColor,
                                     TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding Or TextFormatFlags.Left Or TextFormatFlags.Top,
                                     OwnerDpiScale(), scope.Compositor.TextFormatCache, brushCache)
        End Using
    End Sub

    Private Function MeasureWrappedText(text As String, font As Font, contentW As Integer) As Size
        If String.IsNullOrEmpty(text) Then Return Size.Empty
        Dim textFormatCache = D2DHelperV2.GetCompositor(_owner)?.TextFormatCache
        Return D2DTextRenderer.MeasureText(text, font, New Size(contentW, Integer.MaxValue),
                                           TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding Or TextFormatFlags.Left Or TextFormatFlags.Top,
                                           OwnerDpiScale(), textFormatCache)
    End Function

    Private Sub DrawBackground_D2D(rt As ID2D1RenderTarget, brushCache As D2DGlobals.SolidColorBrushCache,
                                   bw As Integer, w As Integer, h As Integer, fillBackground As Boolean)
        Dim radius As Single = Math.Max(0.0F, _style.BorderRadius * OwnerDpiScale())
        Dim fillColor As Color = ToolTipFillColor()

        If radius > 0 Then
            Dim boundsRect As New RectangleF(0, 0, Math.Max(1, w), Math.Max(1, h))
            If bw > 0 Then
                Dim half As Single = bw / 2.0F
                boundsRect.Inflate(-half, -half)
            End If
            Using geo = RectangleRenderer.创建圆角矩形几何(boundsRect, radius)
                If fillBackground Then rt.FillGeometry(geo, brushCache.Get(rt, fillColor))
                If bw > 0 AndAlso _style.BorderColor.A > 0 Then rt.DrawGeometry(geo, brushCache.Get(rt, _style.BorderColor), bw)
            End Using
            Return
        End If

        If fillBackground Then
            rt.FillRectangle(New Vortice.Mathematics.Rect(0, 0, w, h), brushCache.Get(rt, fillColor))
        End If
        If bw > 0 AndAlso _style.BorderColor.A > 0 Then DrawSquareBorder_D2D(rt, brushCache, w, h, bw)
    End Sub

    Private Sub DrawSquareBorder_D2D(rt As ID2D1RenderTarget, brushCache As D2DGlobals.SolidColorBrushCache,
                                     w As Integer, h As Integer, bw As Integer)
        Dim border As Integer = Math.Min(bw, Math.Min(w, h))
        If border <= 0 Then Return

        Dim br = brushCache.Get(rt, _style.BorderColor)
        rt.FillRectangle(New Vortice.Mathematics.Rect(0, 0, w, border), br)
        If h > border Then rt.FillRectangle(New Vortice.Mathematics.Rect(0, h - border, w, border), br)

        Dim middleHeight As Integer = h - border * 2
        If middleHeight > 0 Then
            rt.FillRectangle(New Vortice.Mathematics.Rect(0, border, border, middleHeight), br)
            If w > border Then rt.FillRectangle(New Vortice.Mathematics.Rect(w - border, border, border, middleHeight), br)
        End If
    End Sub

    Private Sub ApplyPopupWindowState()
        TransparencyKey = Color.Empty
        Opacity = 1.0R
        BackColor = ToolTipFillColor()
    End Sub

    Private Sub ApplyRoundedRegion()
        Dim oldRegion As Region = Region
        Dim radius As Single = Math.Max(0.0F, _style.BorderRadius * OwnerDpiScale())
        If radius <= 0 OrElse Width <= 0 OrElse Height <= 0 Then
            Region = Nothing
        Else
            Using path = RectangleRenderer.创建圆角矩形路径(New RectangleF(0, 0, Width, Height), radius)
                Region = New Region(path)
            End Using
        End If
        If oldRegion IsNot Nothing Then oldRegion.Dispose()
    End Sub

    Private Sub 准备毛玻璃背景()
        If _backdrop Is Nothing Then _backdrop = New PopupBackdropRenderer(Me)
        _backdrop.TransientExcludeOnCapture = True

        If _style.BackdropMode <> PopupBackdropMode.None Then
            _backdrop.Configure(_style.BackdropMode,
                                _style.BackdropImage,
                                _style.BackdropTintColor,
                                _style.BackdropBlurRadius,
                                _style.BackdropBlurPasses,
                                _style.BackdropDownsampleFactor,
                                _style.BackdropNoiseOpacity,
                                _style.BackdropNoiseScale)
        ElseIf ShouldCaptureTransparentBackground() Then
            _backdrop.Configure(PopupBackdropMode.Auto,
                                Nothing,
                                _style.BackColor,
                                1,
                                0,
                                1,
                                0,
                                1.0F)
        Else
            _backdrop.Configure(PopupBackdropMode.None,
                                Nothing,
                                Color.Transparent,
                                1,
                                0,
                                1,
                                0,
                                1.0F)
        End If
        _backdrop.Prepare(Bounds, True)
    End Sub

    Private Sub 绘制毛玻璃背景(g As Graphics)
        If Not HasBackdropFrame() Then Return
        _backdrop.Draw(g, New Rectangle(0, 0, ClientSize.Width, ClientSize.Height))
    End Sub

    Private Function HasBackdropFrame() As Boolean
        Return _backdrop IsNot Nothing AndAlso _backdrop.HasFrame
    End Function

    Private Function ShouldCaptureTransparentBackground() As Boolean
        Return _style.BackdropMode = PopupBackdropMode.None AndAlso _style.BackColor.A < 255
    End Function

    Private Function ToolTipFillColor() As Color
        Return Color.FromArgb(255, _style.BackColor.R, _style.BackColor.G, _style.BackColor.B)
    End Function

    Private Function BorderWidth() As Integer
        Return Math.Max(0, CInt(Math.Round(_style.BorderSize * OwnerDpiScale())))
    End Function

    Private Function TipFont() As Font
        If _style.Font IsNot Nothing Then Return _style.Font
        If _owner IsNot Nothing AndAlso _owner.Font IsNot Nothing Then Return _owner.Font
        Return SystemFonts.DefaultFont
    End Function

    Private Function OwnerDpiScale() As Single
        If _owner IsNot Nothing AndAlso Not _owner.IsDisposed Then Return _owner.DeviceDpi / 96.0F
        Return DeviceDpi / 96.0F
    End Function

    Private Shared Function NormalizePadding(value As Padding) As Padding
        Return New Padding(Math.Max(0, value.Left),
                           Math.Max(0, value.Top),
                           Math.Max(0, value.Right),
                           Math.Max(0, value.Bottom))
    End Function

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            If _backdrop IsNot Nothing Then
                _backdrop.Dispose()
                _backdrop = Nothing
            End If
        End If
        MyBase.Dispose(disposing)
    End Sub

End Class

Friend Class FloatingToolTipStyle
    Public Property Font As Font = Nothing
    Public Property BackColor As Color = Color.FromArgb(50, 50, 50)
    Public Property ForeColor As Color = Color.Silver
    Public Property BorderColor As Color = Color.Gray
    Public Property BorderSize As Integer = 1
    Public Property BorderRadius As Integer = 0
    Public Property Padding As Padding = New Padding(10, 10, 10, 10)
    Public Property MaxWidth As Integer = 300
    Public Property BackdropMode As PopupBackdropMode = PopupBackdropMode.None
    Public Property BackdropImage As Image = Nothing
    Public Property BackdropTintColor As Color = Color.FromArgb(20, 220, 220, 220)
    Public Property BackdropBlurRadius As Integer = 10
    Public Property BackdropBlurPasses As Integer = 1
    Public Property BackdropDownsampleFactor As Integer = 4
    Public Property BackdropNoiseOpacity As Byte = 0
    Public Property BackdropNoiseScale As Single = 1.0F

    Public Function Clone() As FloatingToolTipStyle
        Return DirectCast(MemberwiseClone(), FloatingToolTipStyle)
    End Function
End Class
