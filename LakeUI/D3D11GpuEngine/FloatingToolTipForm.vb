Imports SharpGen.Runtime
Imports Vortice.Direct2D1
Imports Vortice.DirectWrite

''' <summary>
''' 控件共用的自绘浮动提示窗，支持 D2D 文本、边框、圆角以及弹出层毛玻璃背景。
''' </summary>
''' <remarks>
''' 这是一个 no-activate 顶层 popup，继承 <see cref="PopupForm"/>，用于控件 hover 提示。
''' 它不参与宿主控件的 PaintScope，也不应作为子控件添加到容器中。
'''
''' 调用契约：
''' • 每次显示前调用 <see cref="ShowTip"/>，该方法会重新测量文本、调整屏幕位置、准备毛玻璃帧并触发重绘。
''' • 毛玻璃背景通过 <see cref="D3D_PopupBackdropRenderer"/> 独立抓屏，不复用主窗口 ThisIsYourWindow 的帧，
'''   因为 tooltip 是独立顶层窗口，捕获区域和排除截屏策略不同。
''' • <see cref="OnPaintBackground"/> 故意留空，底色由 OnPaint 内的 backdrop / D2D 背景接管。
'''
''' 坑点：
''' • 文本测量缓存只按文本、宽度、字体 hash 和 DPI 命中；样式中 padding、border 改变会在 ShowTip
'''   外层重新算窗口尺寸，不需要进测量 key。
''' • ShowWithoutActivation + WM_MOUSEACTIVATE 是必要组合，避免提示窗抢走原控件焦点。
''' </remarks>
Public Enum FloatingToolTipSide
    Left = 0
    Right = 1
End Enum

Public NotInheritable Class FloatingToolTipForm
    Inherits PopupForm
    Implements IMessageFilter, V3_IGpuRenderable, V3_IGpuInvalidationSource

    Private ReadOnly _owner As Control
    Private _tipText As String = ""
    Private _style As New FloatingToolTipStyle()
    Private _backdrop As D3D_PopupBackdropRenderer
    Private _lastMeasureKey As String = Nothing
    Private _lastMeasuredSize As Size = Size.Empty
    Private _selectionAnchor As Integer = 0
    Private _selectionCaret As Integer = 0
    Private _isMouseSelecting As Boolean = False
    Private _messageFilterInstalled As Boolean = False
    Private _closeTimer As Timer = Nothing
    Private _closeRelatedBounds As Rectangle() = Array.Empty(Of Rectangle)()
    Private _ownerForm As Form = Nothing
    Private _ownerStateHandlersInstalled As Boolean = False

    Private Const WM_KEYDOWN As Integer = &H100
    Private Const WM_SYSKEYDOWN As Integer = &H104

    Private Shared _keyboardSelectionOwner As FloatingToolTipForm = Nothing
    Public Shared Property SelectableCopyEnabled As Boolean = True
    Public Shared Property SelectionFocusColor As Color = Color.FromArgb(40, 220, 220, 220)
    Public Shared Property BackdropEnabled As Boolean = False
    Public Shared Property BackdropMode As PopupBackdropMode = PopupBackdropMode.Auto
    Public Shared Property BackdropImage As Image = Nothing
    Public Shared Property BackdropTintColor As Color = Color.FromArgb(20, 0, 0, 0)
    Public Shared Property BackdropBlurRadius As Integer = 30
    Public Shared Property BackdropBlurPasses As Integer = 1
    Public Shared Property BackdropDownsampleFactor As Integer = 4
    Public Shared Property BackdropNoiseOpacity As Byte = 0
    Public Shared Property BackdropNoiseScale As Single = 1.0F


    Public Sub New(owner As Control)
        _owner = owner
        DoubleBuffered = True
        AutoScaleMode = AutoScaleMode.Dpi
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer, True)
        ApplyPopupWindowState()
    End Sub

    Public Sub ShowTip(text As String, screenLocation As Point, style As FloatingToolTipStyle,
                       Optional overflowFlipDistance As Integer = 0,
                       Optional preferredSide As FloatingToolTipSide = FloatingToolTipSide.Right)
        Dim newText As String = If(text, "")
        If Not IsSelectableCopyEnabled() OrElse Not String.Equals(_tipText, newText, StringComparison.Ordinal) Then
            ClearSelection(False)
        End If
        CancelScheduledClose()
        If Not CanShowForOwner() Then
            CloseImmediately()
            Return
        End If

        _tipText = newText
        _style = If(style, New FloatingToolTipStyle()).Clone()
        ClampSelection()

        Dim pad As Padding = ScaledPadding(_style.Padding)
        Dim bw As Integer = BorderWidth()
        Dim maxW As Integer = ScaledMaxWidth()
        Dim contentW As Integer = maxW - pad.Left - pad.Right - bw * 2
        Dim minContentW As Integer = ScaledLogicalSize(10)
        If contentW < minContentW Then contentW = minContentW

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
        Dim hasFlipTarget As Boolean = overflowFlipDistance > 0
        If preferredSide = FloatingToolTipSide.Left AndAlso hasFlipTarget Then
            loc.X = screenLocation.X - w - overflowFlipDistance
            If loc.X < scr.WorkingArea.Left Then
                loc.X = screenLocation.X
            End If
        ElseIf loc.X + w > scr.WorkingArea.Right Then
            If hasFlipTarget Then
                loc.X = screenLocation.X - w - overflowFlipDistance
            Else
                loc.X = scr.WorkingArea.Right - w
            End If
        End If
        If loc.X + w > scr.WorkingArea.Right Then loc.X = scr.WorkingArea.Right - w
        If loc.X < scr.WorkingArea.Left Then loc.X = scr.WorkingArea.Left
        If loc.Y + h > scr.WorkingArea.Bottom Then loc.Y = scr.WorkingArea.Bottom - h
        If loc.Y < scr.WorkingArea.Top Then loc.Y = scr.WorkingArea.Top
        Location = loc

        ApplyPopupWindowState()
        准备毛玻璃背景()
        EnsureOwnerStateHandlers()
        EnsureMessageFilter()
        If Not Visible Then Show()
        RequestV3Render()
    End Sub

    Friend ReadOnly Property HasSelectedText As Boolean
        Get
            Return IsSelectableCopyEnabled() AndAlso SelectionLength() > 0
        End Get
    End Property

    Friend Function ContainsScreenPoint(screenPoint As Point) As Boolean
        Return Not IsDisposed AndAlso Visible AndAlso Bounds.Contains(screenPoint)
    End Function

    Friend Sub ScheduleCloseIfPointerOutside(delayMs As Integer, ParamArray relatedScreenBounds As Rectangle())
        If IsDisposed OrElse HasSelectedText OrElse _isMouseSelecting Then Return
        _closeRelatedBounds = If(relatedScreenBounds, Array.Empty(Of Rectangle)())
        If _closeTimer Is Nothing Then
            _closeTimer = New Timer()
            AddHandler _closeTimer.Tick, AddressOf CloseTimerTick
        End If
        _closeTimer.Stop()
        _closeTimer.Interval = Math.Max(1, delayMs)
        _closeTimer.Start()
    End Sub

    Friend Sub CopySelectedText()
        If Not IsSelectableCopyEnabled() Then Return
        Dim text As String = GetSelectedText()
        If String.IsNullOrEmpty(text) Then Return
        Try
            Clipboard.SetText(text)
        Catch
        End Try
    End Sub

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' 顶层提示窗体由 OnPaint 中的毛玻璃与 D2D 绘制接管底色。
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If D3D_PaintBridge.PaintRenderable(e, Me, Me) Then Return
        PaintFallback(e)
    End Sub

    Private Sub PaintFallback(e As PaintEventArgs)
        绘制毛玻璃背景(e.Graphics)

        Dim w As Integer = ClientSize.Width
        Dim h As Integer = ClientSize.Height
        If w <= 0 OrElse h <= 0 Then Return

        Dim bw As Integer = BorderWidth()

        Using scope = D3D_PaintBridge.BeginPaint(e, Me, 1)
            If scope Is Nothing Then Return
            Dim rt As ID2D1RenderTarget = scope.GraphicsLayer
            Dim brushCache = scope.Compositor.BrushCache

            DrawBackground_D2D(rt, brushCache, bw, w, h, Not HasBackdropFrame())
            scope.FlushGraphics()

            Dim textRect As RectangleF = GetTextRectangle()
            DrawSelection_D2D(scope.DCRenderTarget, brushCache, textRect)
            D3D_TextInterop.DrawText(scope.DCRenderTarget, _tipText, TipFont(), textRect, _style.ForeColor,
                                     TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding Or TextFormatFlags.Left Or TextFormatFlags.Top,
                                     OwnerDpiScale(), scope.Compositor.TextFormatCache, brushCache)
        End Using
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        If context Is Nothing OrElse ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then Return

        Dim w As Integer = ClientSize.Width
        Dim h As Integer = ClientSize.Height
        Dim bw As Integer = BorderWidth()
        Dim bounds As New RectangleF(0, 0, w, h)
        Dim hasBackdrop As Boolean = 绘制毛玻璃背景(context, bounds)

        DrawBackground_GPU(context, bw, w, h, Not hasBackdrop)

        Dim textRect As RectangleF = GetTextRectangle()
        DrawSelection_GPU(context, textRect)
        context.DrawText(_tipText, TipFont(), _style.ForeColor, textRect,
                         TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding Or TextFormatFlags.Left Or TextFormatFlags.Top)
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not IsSelectableCopyEnabled() OrElse e.Button <> MouseButtons.Left OrElse String.IsNullOrEmpty(_tipText) Then Return

        _keyboardSelectionOwner = Me
        _selectionAnchor = TextPositionFromPoint(e.Location)
        _selectionCaret = _selectionAnchor
        _isMouseSelecting = True
        Capture = True
        Cursor = Cursors.IBeam
        RequestV3Render()
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        CancelScheduledClose()
        Cursor = If(IsSelectableCopyEnabled() AndAlso GetTextRectangle().Contains(e.Location), Cursors.IBeam, Cursors.Default)

        If Not _isMouseSelecting Then Return
        _selectionCaret = TextPositionFromPoint(e.Location)
        ClampSelection()
        RequestV3Render()
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If e.Button <> MouseButtons.Left Then Return
        _isMouseSelecting = False
        Capture = False
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If Not _isMouseSelecting Then Cursor = Cursors.Default
        ScheduleCloseIfPointerOutside(180)
    End Sub

    Protected Overrides Sub OnVisibleChanged(e As EventArgs)
        MyBase.OnVisibleChanged(e)
        If Not Visible Then
            CancelScheduledClose()
            RemoveOwnerStateHandlers()
            If ReferenceEquals(_keyboardSelectionOwner, Me) Then
                _keyboardSelectionOwner = Nothing
            End If
        End If
    End Sub

    Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
        If Not IsSelectableCopyEnabled() Then Return False
        If IsDisposed OrElse Not Visible OrElse Not ReferenceEquals(_keyboardSelectionOwner, Me) Then Return False
        If m.Msg <> WM_KEYDOWN AndAlso m.Msg <> WM_SYSKEYDOWN Then Return False
        If (Control.ModifierKeys And Keys.Control) <> Keys.Control Then Return False

        Dim keyCode As Keys = CType(m.WParam.ToInt32() And &HFFFF, Keys)
        Select Case keyCode
            Case Keys.A
                SelectAllText()
                Return True
            Case Keys.C, Keys.Insert
                CopySelectedText()
                Return HasSelectedText
        End Select
        Return False
    End Function

    Private Function MeasureWrappedText(text As String, font As Font, contentW As Integer) As Size
        If String.IsNullOrEmpty(text) Then Return Size.Empty
        Dim textFormatCache = D3D_PaintBridge.GetCompositor(_owner)?.TextFormatCache
        Return D3D_TextMeasureHelper.MeasureWrappedText_D2D(text, font, contentW, OwnerDpiScale(), textFormatCache)
    End Function

    Private Sub DrawSelection_D2D(rt As ID2D1RenderTarget, brushCache As D3D_D2DInterop.SolidColorBrushCache, textRect As RectangleF)
        If Not IsSelectableCopyEnabled() Then Return
        Dim length As Integer = SelectionLength()
        If rt Is Nothing OrElse length <= 0 OrElse textRect.Width <= 0 OrElse textRect.Height <= 0 Then Return

        Dim start As Integer = SelectionStart()
        Using fmt = CreateTipTextFormat()
            If fmt Is Nothing Then Return
            Using layout = D3D_D2DInterop.GetDWriteFactory().CreateTextLayout(_tipText, fmt,
                                                                          Math.Max(1.0F, textRect.Width),
                                                                          Math.Max(1.0F, textRect.Height))
                Dim metrics(Math.Max(1, length + 1) - 1) As HitTestMetrics
                Dim actual As UInteger = 0
                Try
                    layout.HitTestTextRange(CUInt(start), CUInt(length), textRect.X, textRect.Y, metrics, actual)
                Catch
                    Return
                End Try
                If actual <= 0 Then Return

                Dim selectionBrush = brushCache.Get(rt, EffectiveSelectionFocusColor())
                Dim count As Integer = Math.Min(metrics.Length, CInt(actual))
                For i As Integer = 0 To count - 1
                    Dim m = metrics(i)
                    If m.Width <= 0.0F OrElse m.Height <= 0.0F Then Continue For
                    rt.FillRectangle(New Vortice.Mathematics.Rect(m.Left, m.Top, m.Width, m.Height), selectionBrush)
                Next
            End Using
        End Using
    End Sub

    Private Sub DrawSelection_GPU(context As D3D_PaintContext, textRect As RectangleF)
        If Not IsSelectableCopyEnabled() Then Return
        Dim length As Integer = SelectionLength()
        If context Is Nothing OrElse length <= 0 OrElse textRect.Width <= 0 OrElse textRect.Height <= 0 Then Return

        Dim start As Integer = SelectionStart()
        Using fmt = CreateTipTextFormat()
            If fmt Is Nothing Then Return
            Using layout = D3D_D2DInterop.GetDWriteFactory().CreateTextLayout(_tipText, fmt,
                                                                          Math.Max(1.0F, textRect.Width),
                                                                          Math.Max(1.0F, textRect.Height))
                Dim metrics(Math.Max(1, length + 1) - 1) As HitTestMetrics
                Dim actual As UInteger = 0
                Try
                    layout.HitTestTextRange(CUInt(start), CUInt(length), textRect.X, textRect.Y, metrics, actual)
                Catch
                    Return
                End Try
                If actual <= 0 Then Return

                Dim selectionBrush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext,
                                                                                 EffectiveSelectionFocusColor(),
                                                                                 context.DeviceGeneration)
                Dim count As Integer = Math.Min(metrics.Length, CInt(actual))
                For i As Integer = 0 To count - 1
                    Dim m = metrics(i)
                    If m.Width <= 0.0F OrElse m.Height <= 0.0F Then Continue For
                    context.DeviceContext.FillRectangle(New Vortice.Mathematics.Rect(m.Left, m.Top, m.Width, m.Height), selectionBrush)
                Next
            End Using
        End Using
    End Sub

    Private Function TextPositionFromPoint(point As Point) As Integer
        If Not IsSelectableCopyEnabled() Then Return 0
        If String.IsNullOrEmpty(_tipText) Then Return 0

        Dim textRect As RectangleF = GetTextRectangle()
        If textRect.Width <= 0 OrElse textRect.Height <= 0 Then Return 0

        Using fmt = CreateTipTextFormat()
            If fmt Is Nothing Then Return 0
            Using layout = D3D_D2DInterop.GetDWriteFactory().CreateTextLayout(_tipText, fmt,
                                                                          Math.Max(1.0F, textRect.Width),
                                                                          Math.Max(1.0F, textRect.Height))
                Dim trailing As RawBool = False
                Dim inside As RawBool = False
                Dim metrics As HitTestMetrics
                layout.HitTestPoint(point.X - textRect.X, point.Y - textRect.Y, trailing, inside, metrics)

                Dim pos As Integer = CInt(metrics.TextPosition)
                If CBool(trailing) Then pos += Math.Max(1, CInt(metrics.Length))
                Return Math.Max(0, Math.Min(_tipText.Length, pos))
            End Using
        End Using
    End Function

    Private Function CreateTipTextFormat() As IDWriteTextFormat
        Dim font As Font = TipFont()
        If font Is Nothing Then Return Nothing

        Dim sizePx As Single = D3D_D2DInterop.GetDWriteFontSizePx(font, OwnerDpiScale())
        Dim fmt = D3D_D2DInterop.CreateTextFormat(font, sizePx)
        fmt.TextAlignment = TextAlignment.Leading
        fmt.ParagraphAlignment = ParagraphAlignment.Near
        fmt.WordWrapping = WordWrapping.Wrap
        D3D_TextMeasureHelper.ApplyUniformLineSpacing(fmt, font, OwnerDpiScale())
        Return fmt
    End Function

    Private Function GetTextRectangle() As RectangleF
        Dim bw As Integer = BorderWidth()
        Dim pad As Padding = ScaledPadding(_style.Padding)
        Return New RectangleF(bw + pad.Left,
                              bw + pad.Top,
                              Math.Max(0, ClientSize.Width - bw * 2 - pad.Left - pad.Right),
                              Math.Max(0, ClientSize.Height - bw * 2 - pad.Top - pad.Bottom))
    End Function

    Private Function SelectionStart() As Integer
        Return Math.Min(_selectionAnchor, _selectionCaret)
    End Function

    Private Function SelectionLength() As Integer
        Return Math.Abs(_selectionCaret - _selectionAnchor)
    End Function

    Private Function GetSelectedText() As String
        Dim start As Integer = SelectionStart()
        Dim length As Integer = SelectionLength()
        If length <= 0 OrElse String.IsNullOrEmpty(_tipText) Then Return String.Empty
        Return _tipText.Substring(start, Math.Min(length, _tipText.Length - start))
    End Function

    Private Sub SelectAllText()
        If Not IsSelectableCopyEnabled() Then Return
        If String.IsNullOrEmpty(_tipText) Then Return
        _selectionAnchor = 0
        _selectionCaret = _tipText.Length
        _keyboardSelectionOwner = Me
        RequestV3Render()
    End Sub

    Private Sub ClearSelection(Optional invalidateForm As Boolean = True)
        _selectionAnchor = 0
        _selectionCaret = 0
        _isMouseSelecting = False
        If ReferenceEquals(_keyboardSelectionOwner, Me) Then _keyboardSelectionOwner = Nothing
        If invalidateForm Then RequestV3Render()
    End Sub

    Private Sub ClampSelection()
        Dim maxLen As Integer = If(_tipText Is Nothing, 0, _tipText.Length)
        _selectionAnchor = Math.Max(0, Math.Min(maxLen, _selectionAnchor))
        _selectionCaret = Math.Max(0, Math.Min(maxLen, _selectionCaret))
    End Sub

    Private Sub EnsureMessageFilter()
        If _messageFilterInstalled Then Return
        Application.AddMessageFilter(Me)
        _messageFilterInstalled = True
    End Sub

    Private Sub CancelScheduledClose()
        If _closeTimer IsNot Nothing Then _closeTimer.Stop()
        _closeRelatedBounds = Array.Empty(Of Rectangle)()
    End Sub

    Private Function CanShowForOwner() As Boolean
        If _owner Is Nothing OrElse _owner.IsDisposed Then Return False
        If Not _owner.Visible OrElse Not _owner.Enabled Then Return False

        Dim ownerForm As Form = _owner.FindForm()
        If ownerForm IsNot Nothing AndAlso (ownerForm.IsDisposed OrElse Not ownerForm.Visible OrElse Not ownerForm.Enabled) Then Return False
        Return True
    End Function

    Private Sub CloseTimerTick(sender As Object, e As EventArgs)
        If _closeTimer IsNot Nothing Then _closeTimer.Stop()
        If IsDisposed OrElse HasSelectedText OrElse _isMouseSelecting Then Return

        Dim screenPos As Point = Control.MousePosition
        If ContainsScreenPoint(screenPos) Then Return
        For Each rect In _closeRelatedBounds
            If rect.Contains(screenPos) Then Return
        Next

        Close()
    End Sub

    Private Sub EnsureOwnerStateHandlers()
        If _owner Is Nothing OrElse _owner.IsDisposed Then Return

        Dim currentOwnerForm As Form = _owner.FindForm()
        If _ownerStateHandlersInstalled AndAlso Not ReferenceEquals(_ownerForm, currentOwnerForm) Then
            RemoveOwnerStateHandlers()
        End If

        If _ownerStateHandlersInstalled Then Return

        AddHandler _owner.HandleDestroyed, AddressOf OwnerStateChanged
        AddHandler _owner.VisibleChanged, AddressOf OwnerStateChanged
        AddHandler _owner.EnabledChanged, AddressOf OwnerStateChanged

        _ownerForm = currentOwnerForm
        If _ownerForm IsNot Nothing Then
            AddHandler _ownerForm.Deactivate, AddressOf OwnerStateChanged
            AddHandler _ownerForm.FormClosed, AddressOf OwnerFormClosed
            AddHandler _ownerForm.HandleDestroyed, AddressOf OwnerStateChanged
            AddHandler _ownerForm.VisibleChanged, AddressOf OwnerStateChanged
            AddHandler _ownerForm.EnabledChanged, AddressOf OwnerStateChanged
        End If

        _ownerStateHandlersInstalled = True
    End Sub

    Private Sub OwnerStateChanged(sender As Object, e As EventArgs)
        CloseImmediately()
    End Sub

    Private Sub OwnerFormClosed(sender As Object, e As FormClosedEventArgs)
        CloseImmediately()
    End Sub

    Private Sub CloseImmediately()
        If IsDisposed Then Return
        CancelScheduledClose()
        ClearSelection(False)
        Close()
    End Sub

    Private Sub RemoveOwnerStateHandlers()
        If Not _ownerStateHandlersInstalled Then
            _ownerForm = Nothing
            Return
        End If

        If _owner IsNot Nothing Then
            RemoveHandler _owner.HandleDestroyed, AddressOf OwnerStateChanged
            RemoveHandler _owner.VisibleChanged, AddressOf OwnerStateChanged
            RemoveHandler _owner.EnabledChanged, AddressOf OwnerStateChanged
        End If

        If _ownerForm IsNot Nothing Then
            RemoveHandler _ownerForm.Deactivate, AddressOf OwnerStateChanged
            RemoveHandler _ownerForm.FormClosed, AddressOf OwnerFormClosed
            RemoveHandler _ownerForm.HandleDestroyed, AddressOf OwnerStateChanged
            RemoveHandler _ownerForm.VisibleChanged, AddressOf OwnerStateChanged
            RemoveHandler _ownerForm.EnabledChanged, AddressOf OwnerStateChanged
        End If

        _ownerForm = Nothing
        _ownerStateHandlersInstalled = False
    End Sub

    Private Sub RemoveMessageFilter()
        If Not _messageFilterInstalled Then Return
        Application.RemoveMessageFilter(Me)
        _messageFilterInstalled = False
    End Sub

    Private Sub DrawBackground_D2D(rt As ID2D1RenderTarget, brushCache As D3D_D2DInterop.SolidColorBrushCache,
                                   bw As Integer, w As Integer, h As Integer, fillBackground As Boolean)
        Dim radius As Single = BorderRadius()
        Dim fillColor As Color = ToolTipFillColor()

        If radius > 0 Then
            Dim boundsRect As New RectangleF(0, 0, Math.Max(1, w), Math.Max(1, h))
            If bw > 0 Then
                Dim half As Single = bw / 2.0F
                boundsRect.Inflate(-half, -half)
            End If
            Using geo = D3D_RectangleRenderer.创建圆角矩形几何(boundsRect, radius)
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

    Private Sub DrawSquareBorder_D2D(rt As ID2D1RenderTarget, brushCache As D3D_D2DInterop.SolidColorBrushCache,
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

    Private Sub DrawBackground_GPU(context As D3D_PaintContext, bw As Integer, w As Integer, h As Integer, fillBackground As Boolean)
        Dim radius As Single = BorderRadius()
        Dim fillColor As Color = ToolTipFillColor()

        If radius > 0 Then
            Dim boundsRect As New RectangleF(0, 0, Math.Max(1, w), Math.Max(1, h))
            If bw > 0 Then
                Dim half As Single = bw / 2.0F
                boundsRect.Inflate(-half, -half)
            End If
            If fillBackground Then context.FillRoundedRectangle(boundsRect, radius, fillColor)
            If bw > 0 AndAlso _style.BorderColor.A > 0 Then context.DrawRoundedRectangle(boundsRect, radius, _style.BorderColor, bw)
            Return
        End If

        If fillBackground Then context.FillRectangle(New RectangleF(0, 0, w, h), fillColor)
        If bw > 0 AndAlso _style.BorderColor.A > 0 Then DrawSquareBorder_GPU(context, w, h, bw)
    End Sub

    Private Sub DrawSquareBorder_GPU(context As D3D_PaintContext, w As Integer, h As Integer, bw As Integer)
        Dim border As Integer = Math.Min(bw, Math.Min(w, h))
        If border <= 0 Then Return

        context.FillRectangle(New RectangleF(0, 0, w, border), _style.BorderColor)
        If h > border Then context.FillRectangle(New RectangleF(0, h - border, w, border), _style.BorderColor)

        Dim middleHeight As Integer = h - border * 2
        If middleHeight > 0 Then
            context.FillRectangle(New RectangleF(0, border, border, middleHeight), _style.BorderColor)
            If w > border Then context.FillRectangle(New RectangleF(w - border, border, border, middleHeight), _style.BorderColor)
        End If
    End Sub

    Private Sub ApplyPopupWindowState()
        TransparencyKey = Color.Empty
        Opacity = 1.0R
        BackColor = ToolTipFillColor()
    End Sub

    Private Sub ApplyRoundedRegion()
        Dim oldRegion As Region = Region
        Dim radius As Single = BorderRadius()
        If radius <= 0 OrElse Width <= 0 OrElse Height <= 0 Then
            Region = Nothing
        Else
            Using path = D3D_RectangleRenderer.创建圆角矩形路径(New RectangleF(0, 0, Width, Height), radius)
                Region = New Region(path)
            End Using
        End If
        If oldRegion IsNot Nothing Then oldRegion.Dispose()
    End Sub

    Private Sub 准备毛玻璃背景()
        If _backdrop Is Nothing Then _backdrop = New D3D_PopupBackdropRenderer(Me)
        _backdrop.TransientExcludeOnCapture = True

        If BackdropEnabled AndAlso BackdropMode <> PopupBackdropMode.None Then
            _backdrop.Configure(BackdropMode,
                                BackdropImage,
                                BackdropTintColor,
                                BackdropBlurRadius,
                                BackdropBlurPasses,
                                BackdropDownsampleFactor,
                                BackdropNoiseOpacity,
                                BackdropNoiseScale)
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

    Private Function 绘制毛玻璃背景(context As D3D_PaintContext, target As RectangleF) As Boolean
        If Not HasBackdropFrame() Then Return False
        Return _backdrop.Draw(context, target)
    End Function

    Private Function HasBackdropFrame() As Boolean
        Return _backdrop IsNot Nothing AndAlso _backdrop.HasFrame
    End Function

    Private Function ShouldCaptureTransparentBackground() As Boolean
        Return _style.BackColor.A < 255
    End Function

    Private Function ToolTipFillColor() As Color
        Return Color.FromArgb(255, _style.BackColor.R, _style.BackColor.G, _style.BackColor.B)
    End Function

    Private Shared Function IsSelectableCopyEnabled() As Boolean
        Return SelectableCopyEnabled
    End Function

    Private Sub RequestV3Render()
        RequestV3Render(New Rectangle(Point.Empty, Me.Size))
    End Sub

    Private Sub RequestV3Render(dirtyRect As Rectangle)
        If IsDisposed OrElse Disposing Then Return
        If dirtyRect.Width <= 0 OrElse dirtyRect.Height <= 0 Then Return
        V3_InvalidationRouter.RequestRender(Me, dirtyRect)
    End Sub

    Private Function EffectiveSelectionFocusColor() As Color
        If _style.SelectionBackColor <> Color.Empty Then Return _style.SelectionBackColor
        Return SelectionFocusColor
    End Function

    Private Function BorderWidth() As Integer
        Return Math.Max(0, ScaledBorderWidth(_style.BorderSize))
    End Function

    Private Function BorderRadius() As Single
        Return Math.Max(0.0F, _style.BorderRadius * OwnerDpiScale())
    End Function

    Private Function ScaledMaxWidth() As Integer
        Return Math.Max(ScaledLogicalSize(50), ScaledLogicalSize(_style.MaxWidth))
    End Function

    Private Function ScaledPadding(value As Padding) As Padding
        Dim pad As Padding = NormalizePadding(value)
        Return New Padding(ScaledLogicalSize(pad.Left),
                           ScaledLogicalSize(pad.Top),
                           ScaledLogicalSize(pad.Right),
                           ScaledLogicalSize(pad.Bottom))
    End Function

    Private Function ScaledLogicalSize(value As Integer) As Integer
        Return CInt(Math.Round(value * OwnerDpiScale(), MidpointRounding.AwayFromZero))
    End Function

    Private Function ScaledBorderWidth(value As Integer) As Integer
        Return CInt(Math.Round(value * OwnerDpiScale()))
    End Function

    Private Function TipFont() As Font
        If _style.Font IsNot Nothing Then Return _style.Font
        If _owner IsNot Nothing AndAlso _owner.Font IsNot Nothing Then Return _owner.Font
        Return SystemFonts.DefaultFont
    End Function

    Private Function OwnerDpiScale() As Single
        If _owner IsNot Nothing AndAlso Not _owner.IsDisposed Then Return D3D_D2DInterop.GetCurrentDpiScale(_owner)
        Return D3D_D2DInterop.GetCurrentDpiScale(Me)
    End Function

    Private Shared Function NormalizePadding(value As Padding) As Padding
        Return New Padding(Math.Max(0, value.Left),
                           Math.Max(0, value.Top),
                           Math.Max(0, value.Right),
                           Math.Max(0, value.Bottom))
    End Function

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            RemoveOwnerStateHandlers()
            RemoveMessageFilter()
            If _closeTimer IsNot Nothing Then
                RemoveHandler _closeTimer.Tick, AddressOf CloseTimerTick
                _closeTimer.Dispose()
                _closeTimer = Nothing
            End If
            If ReferenceEquals(_keyboardSelectionOwner, Me) Then _keyboardSelectionOwner = Nothing
            If _backdrop IsNot Nothing Then
                _backdrop.Dispose()
                _backdrop = Nothing
            End If
        End If
        MyBase.Dispose(disposing)
    End Sub

End Class

''' <summary>
''' 浮动提示窗的视觉配置快照。
''' </summary>
''' <remarks>
''' ShowTip 会 clone 一份样式，之后外部继续修改原对象不会影响已经显示的提示窗。
''' Font 由调用方持有，本类不负责 Dispose。
''' </remarks>
Public Class FloatingToolTipStyle
    Public Property Font As Font = Nothing
    Public Property BackColor As Color = Color.FromArgb(50, 50, 50)
    Public Property ForeColor As Color = Color.Silver
    Public Property BorderColor As Color = Color.Gray
    Public Property BorderSize As Integer = 1
    Public Property BorderRadius As Integer = 0
    Public Property Padding As Padding = New Padding(10, 10, 10, 10)
    Public Property MaxWidth As Integer = 300
    Public Property SelectionBackColor As Color = Color.Empty

    Public Function Clone() As FloatingToolTipStyle
        Return DirectCast(MemberwiseClone(), FloatingToolTipStyle)
    End Function
End Class
