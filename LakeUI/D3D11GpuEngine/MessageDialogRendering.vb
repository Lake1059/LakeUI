Imports System.Numerics
Imports System.Runtime.InteropServices
Imports Vortice.Direct2D1

''' <summary>
''' LakeUI 内置消息窗口的全局毛玻璃配置。默认关闭。
''' </summary>
''' <remarks>
''' 这些选项只影响 LakeUI 自带消息窗口，不会改变 <see cref="ThisIsYourWindow"/> 或普通 popup 的玻璃设置。
''' Image 模式下的 BackdropImage 由调用方持有；消息窗口只引用它，不负责 Dispose。
''' </remarks>
Public NotInheritable Class MessageDialogOptions
    Private Sub New()
    End Sub

    Public Shared Property BackdropEnabled As Boolean = False
    Public Shared Property BackdropMode As PopupBackdropMode = PopupBackdropMode.Auto
    Public Shared Property BackdropImage As Image = Nothing
    Public Shared Property BackdropTintColor As Color = Color.FromArgb(20, 220, 220, 220)
    Public Shared Property BackdropBlurRadius As Integer = 30
    Public Shared Property BackdropBlurPasses As Integer = 1
    Public Shared Property BackdropDownsampleFactor As Integer = 4
    Public Shared Property BackdropNoiseOpacity As Byte = 0
    Public Shared Property BackdropNoiseScale As Single = 1.0F
End Class

''' <summary>
''' 消息窗口毛玻璃背景控制器。
''' </summary>
''' <remarks>
''' 每个消息窗 Form 一份实例；Prepare 负责抓取/生成当前背景帧，Draw 只消费已生成的帧。
''' Dispose 会释放内部 D3D_PopupBackdropRenderer 与 D3D_BackdropSurfaceRenderer。
''' </remarks>
Friend NotInheritable Class MessageDialogBackdropController
    Implements IDisposable

    Private ReadOnly _host As Form
    Private _backdrop As D3D_PopupBackdropRenderer

    Public Sub New(host As Form)
        _host = host
    End Sub

    Public ReadOnly Property Enabled As Boolean
        Get
            Return MessageDialogRendering.IsGlassEnabled()
        End Get
    End Property

    Public ReadOnly Property HasFrame As Boolean
        Get
            Return _backdrop IsNot Nothing AndAlso _backdrop.HasFrame
        End Get
    End Property

    Public Sub Prepare()
        Prepare(_host.Bounds)
    End Sub

    Public Sub Prepare(captureBounds As Rectangle)
        If _backdrop Is Nothing Then _backdrop = New D3D_PopupBackdropRenderer(_host)
        _backdrop.TransientExcludeOnCapture = True

        If Enabled Then
            _backdrop.Configure(MessageDialogOptions.BackdropMode,
                                MessageDialogOptions.BackdropImage,
                                MessageDialogOptions.BackdropTintColor,
                                MessageDialogOptions.BackdropBlurRadius,
                                MessageDialogOptions.BackdropBlurPasses,
                                MessageDialogOptions.BackdropDownsampleFactor,
                                MessageDialogOptions.BackdropNoiseOpacity,
                                MessageDialogOptions.BackdropNoiseScale)
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
        _backdrop.Prepare(captureBounds, True)
    End Sub

    Public Function WaitForFrame(Optional timeoutMilliseconds As Integer = 500) As Boolean
        If _backdrop Is Nothing Then Return True
        Return _backdrop.WaitForFrame(timeoutMilliseconds)
    End Function

    Public Sub Draw(g As Graphics)
        Draw(g, New Rectangle(Point.Empty, _host.ClientSize))
    End Sub

    Public Sub Draw(g As Graphics, target As Rectangle)
        ' V3-only: pixels are emitted by RenderGpu.
    End Sub

    Public Sub DrawRounded(g As Graphics, target As Rectangle, radius As Single)
        ' V3-only: pixels are emitted by RenderGpu.
    End Sub

    Public Function Draw(context As D3D_PaintContext, target As RectangleF) As Boolean
        If Not Enabled OrElse _backdrop Is Nothing Then Return False
        Return _backdrop.Draw(context, target)
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If _backdrop IsNot Nothing Then
            _backdrop.Dispose()
            _backdrop = Nothing
        End If
    End Sub
End Class

''' <summary>
''' 消息窗口渲染与 Win32 小工具集合：系统图标、owner 居中、玻璃背景和按钮样式。
''' </summary>
''' <remarks>
''' 这里不保存 D2D 资源；真正的毛玻璃帧由 <see cref="MessageDialogBackdropController"/> /
''' <see cref="D3D_PopupBackdropRenderer"/> 管理。
'''
''' 坑点：
''' • SHGetStockIconInfo 返回的 hIcon 必须 DestroyIcon；TryGetStockIcon 会 clone 一份托管 Icon 后释放原句柄。
''' • CreateMessageIconBitmap 返回的 Bitmap 由调用方 Dispose，通常作为 PictureBox / D2D 上传源使用。
''' • CenterOnOwner 使用 Win32 GetWindowRect，适合 owner 不是 Control 的场景。
''' </remarks>
Friend Module MessageDialogRendering
    <DllImport("user32.dll")>
    Private Function GetWindowRect(hWnd As IntPtr, ByRef rect As NativeRect) As Boolean
    End Function

    <DllImport("shell32.dll", SetLastError:=False)>
    Private Function SHGetStockIconInfo(siid As Integer, uFlags As UInteger, ByRef psii As SHSTOCKICONINFO) As Integer
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Function DestroyIcon(hIcon As IntPtr) As Boolean
    End Function

    Private Const SHGSI_ICON As UInteger = &H100UI
    Private Const SHGSI_LARGEICON As UInteger = &H0UI
    Private Const SIID_HELP As Integer = 23
    Private Const SIID_WARNING As Integer = 78
    Private Const SIID_INFO As Integer = 79
    Private Const SIID_ERROR As Integer = 80

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeRect
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
    Private Structure SHSTOCKICONINFO
        Public cbSize As UInteger
        Public hIcon As IntPtr
        Public iSysImageIndex As Integer
        Public iIcon As Integer
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=260)>
        Public szPath As String
    End Structure

    Public Function IsGlassEnabled() As Boolean
        Return MessageDialogOptions.BackdropEnabled AndAlso
               MessageDialogOptions.BackdropMode <> PopupBackdropMode.None AndAlso
               (MessageDialogOptions.BackdropMode <> PopupBackdropMode.Image OrElse
                MessageDialogOptions.BackdropImage IsNot Nothing)
    End Function

    Public Function ResolveDialogFontName(owner As IWin32Window, Optional fallbackControl As Control = Nothing) As String
        Dim ownerControl = TryCast(owner, Control)
        If ownerControl IsNot Nothing AndAlso ownerControl.Font IsNot Nothing Then Return ownerControl.Font.FontFamily.Name

        If Application.OpenForms IsNot Nothing AndAlso Application.OpenForms.Count > 0 Then
            Dim mainForm = Application.OpenForms(0)
            If mainForm IsNot Nothing AndAlso mainForm.Font IsNot Nothing Then Return mainForm.Font.FontFamily.Name
        End If

        If fallbackControl IsNot Nothing AndAlso fallbackControl.Font IsNot Nothing Then Return fallbackControl.Font.FontFamily.Name
        Return SystemFonts.MessageBoxFont.FontFamily.Name
    End Function

    Public Function CreateMessageIconBitmap(iconStyle As Integer, size As Integer) As Bitmap
        Dim stockId = ResolveStockIconId(iconStyle)
        If stockId < 0 OrElse size <= 0 Then Return Nothing

        Dim stockIcon = TryGetStockIcon(stockId)
        If stockIcon IsNot Nothing Then
            Try
                Return RenderIconBitmap(stockIcon, size)
            Finally
                stockIcon.Dispose()
            End Try
        End If

        Dim fallbackIcon = ResolveFallbackIcon(iconStyle)
        If fallbackIcon Is Nothing Then Return Nothing
        Return RenderIconBitmap(fallbackIcon, size)
    End Function

    Private Function ResolveStockIconId(iconStyle As Integer) As Integer
        Select Case iconStyle
            Case MsgBoxStyle.Critical : Return SIID_ERROR
            Case MsgBoxStyle.Question : Return SIID_HELP
            Case MsgBoxStyle.Exclamation : Return SIID_WARNING
            Case MsgBoxStyle.Information : Return SIID_INFO
            Case Else : Return -1
        End Select
    End Function

    Private Function ResolveFallbackIcon(iconStyle As Integer) As Icon
        Select Case iconStyle
            Case MsgBoxStyle.Critical : Return SystemIcons.Error
            Case MsgBoxStyle.Question : Return SystemIcons.Question
            Case MsgBoxStyle.Exclamation : Return SystemIcons.Warning
            Case MsgBoxStyle.Information : Return SystemIcons.Information
            Case Else : Return Nothing
        End Select
    End Function

    Private Function TryGetStockIcon(stockId As Integer) As Icon
        Dim info As New SHSTOCKICONINFO With {
            .cbSize = CUInt(Marshal.SizeOf(GetType(SHSTOCKICONINFO)))
        }
        If SHGetStockIconInfo(stockId, SHGSI_ICON Or SHGSI_LARGEICON, info) <> 0 OrElse info.hIcon = IntPtr.Zero Then Return Nothing

        Try
            Using handleIcon = Icon.FromHandle(info.hIcon)
                Return DirectCast(handleIcon.Clone(), Icon)
            End Using
        Finally
            DestroyIcon(info.hIcon)
        End Try
    End Function

    Private Function RenderIconBitmap(icon As Icon, size As Integer) As Bitmap
        If icon Is Nothing OrElse size <= 0 Then Return Nothing
        Dim bmp As New Bitmap(size, size, Imaging.PixelFormat.Format32bppPArgb)
        Using g = Graphics.FromImage(bmp)
            g.Clear(Color.Transparent)
            g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
            g.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
            Using sized As New Icon(icon, size, size)
                g.DrawIcon(sized, New Rectangle(0, 0, size, size))
            End Using
        End Using
        Return bmp
    End Function

    Public Sub CenterOnOwner(dialog As Form, owner As IWin32Window)
        If dialog Is Nothing OrElse owner Is Nothing OrElse owner.Handle = IntPtr.Zero Then Return

        Dim ownerBounds As Rectangle
        If Not TryGetWindowBounds(owner, ownerBounds) Then Return

        Dim x = ownerBounds.Left + (ownerBounds.Width - dialog.Width) \ 2
        Dim y = ownerBounds.Top + (ownerBounds.Height - dialog.Height) \ 2
        Dim workArea = Screen.FromRectangle(ownerBounds).WorkingArea
        x = Math.Max(workArea.Left, Math.Min(x, workArea.Right - dialog.Width))
        y = Math.Max(workArea.Top, Math.Min(y, workArea.Bottom - dialog.Height))

        dialog.StartPosition = FormStartPosition.Manual
        dialog.Location = New Point(x, y)
    End Sub

    Public Function TryGetWindowBounds(owner As IWin32Window, ByRef bounds As Rectangle) As Boolean
        bounds = Rectangle.Empty
        If owner Is Nothing OrElse owner.Handle = IntPtr.Zero Then Return False

        Dim nativeBounds As NativeRect
        If Not GetWindowRect(owner.Handle, nativeBounds) Then Return False

        bounds = Rectangle.FromLTRB(nativeBounds.Left, nativeBounds.Top, nativeBounds.Right, nativeBounds.Bottom)
        Return bounds.Width > 0 AndAlso bounds.Height > 0
    End Function

    Public Sub ApplyButtonStyle(button As ModernButton,
                                surface As Control,
                                isDefault As Boolean,
                                normalBackColor As Color,
                                normalForeColor As Color,
                                normalBorderColor As Color,
                                hoverBackColor As Color,
                                pressedBackColor As Color,
                                accentBackColor As Color,
                                accentForeColor As Color,
                                accentBorderColor As Color,
                                accentHoverBackColor As Color,
                                accentPressedBackColor As Color)
        If IsGlassEnabled() Then
            DirectCast(button, Control).BackColor = Color.Transparent
            button.BackgroundSource = surface
            button.BackColor1 = Color.FromArgb(40, 220, 220, 220)
            button.HoverBackColor1 = Color.FromArgb(60, 220, 220, 220)
            button.PressedBackColor1 = Color.FromArgb(80, 220, 220, 220)
            button.ForeColor = normalForeColor
            button.BorderColor = If(isDefault, Color.Silver, Color.Transparent)
            button.HoverBorderColor = button.BorderColor
            button.PressedBorderColor = button.BorderColor
            button.BorderSize = If(isDefault, 1, 0)
            Return
        End If

        DirectCast(button, Control).BackColor = If(surface Is Nothing, Color.Transparent, surface.BackColor)
        button.BackgroundSource = Nothing
        button.BorderSize = 1
        If isDefault Then
            button.BackColor1 = accentBackColor
            button.ForeColor = accentForeColor
            button.BorderColor = accentBorderColor
            button.HoverBackColor1 = accentHoverBackColor
            button.PressedBackColor1 = accentPressedBackColor
        Else
            button.BackColor1 = normalBackColor
            button.ForeColor = normalForeColor
            button.BorderColor = normalBorderColor
            button.HoverBackColor1 = hoverBackColor
            button.PressedBackColor1 = pressedBackColor
        End If
        button.HoverBorderColor = Color.Empty
        button.PressedBorderColor = Color.Empty
    End Sub

    Public Sub FillRectangle(rt As ID2D1RenderTarget,
                             brushCache As D3D_D2DInterop.SolidColorBrushCache,
                             rect As RectangleF,
                             color As Color)
        If rt Is Nothing OrElse brushCache Is Nothing OrElse color.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        rt.FillRectangle(D3D_D2DInterop.ToD2DRect(rect), brushCache.Get(rt, color))
    End Sub

    Public Sub DrawRectangle(rt As ID2D1RenderTarget,
                             brushCache As D3D_D2DInterop.SolidColorBrushCache,
                             rect As RectangleF,
                             color As Color,
                             width As Single)
        If rt Is Nothing OrElse brushCache Is Nothing OrElse color.A = 0 OrElse width <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        rt.DrawRectangle(D3D_D2DInterop.ToD2DRect(rect), brushCache.Get(rt, color), width)
    End Sub

    Public Sub DrawText(rt As ID2D1RenderTarget,
                        compositor As D3D_SurfaceCompositor,
                        text As String,
                        font As Font,
                        rect As RectangleF,
                        color As Color,
                        flags As TextFormatFlags,
                        dpiScale As Single)
        If String.IsNullOrEmpty(text) OrElse font Is Nothing OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        D3D_TextInterop.DrawText(rt, text, font, rect, color, flags, dpiScale,
                                 compositor.TextFormatCache, compositor.BrushCache)
    End Sub

    Public Sub DrawImage(rt As ID2D1RenderTarget,
                         compositor As D3D_SurfaceCompositor,
                         image As Image,
                         rect As RectangleF)
        If rt Is Nothing OrElse compositor Is Nothing OrElse image Is Nothing OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim sourceRect As New Vortice.Mathematics.Rect(0, 0, image.Width, image.Height)
        Dim cache = compositor.GetBitmapCache(image)
        Dim bitmap = cache?.GetBitmap(rt, image)
        If bitmap Is Nothing Then
            Using directBitmap = D3D_D2DInterop.CreateBitmapFromImage(rt, image)
                If directBitmap Is Nothing Then Return
                rt.DrawBitmap(directBitmap, D3D_D2DInterop.ToD2DRect(rect), 1.0F, BitmapInterpolationMode.Linear, sourceRect)
            End Using
            Return
        End If
        rt.DrawBitmap(bitmap, D3D_D2DInterop.ToD2DRect(rect), 1.0F, BitmapInterpolationMode.Linear, sourceRect)
    End Sub

    Public Sub DrawCloseButton(rt As ID2D1RenderTarget,
                               brushCache As D3D_D2DInterop.SolidColorBrushCache,
                               rect As RectangleF,
                               hovered As Boolean,
                               pressed As Boolean,
                               normalForeColor As Color,
                               hoverForeColor As Color,
                               hoverBackColor As Color,
                               dpiScale As Single)
        If pressed Then
            FillRectangle(rt, brushCache, rect, Color.FromArgb(180, hoverBackColor))
        ElseIf hovered Then
            FillRectangle(rt, brushCache, rect, hoverBackColor)
        End If

        Dim glyphColor = If(hovered OrElse pressed, hoverForeColor, normalForeColor)
        Dim cx = rect.X + rect.Width / 2.0F
        Dim cy = rect.Y + rect.Height / 2.0F
        Dim half = 5.0F * dpiScale
        Dim width = Math.Max(1.0F, 1.2F * dpiScale)
        Dim brush = brushCache.Get(rt, glyphColor)
        rt.DrawLine(New Vector2(cx - half, cy - half), New Vector2(cx + half, cy + half), brush, width)
        rt.DrawLine(New Vector2(cx + half, cy - half), New Vector2(cx - half, cy + half), brush, width)
    End Sub

    Public Function DrawBackdrop(context As D3D_PaintContext,
                                 bounds As RectangleF,
                                 Optional controller As MessageDialogBackdropController = Nothing) As Boolean
        If context Is Nothing OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return False
        If Not IsGlassEnabled() Then Return False

        If controller IsNot Nothing Then
            Return controller.Draw(context, bounds)
        End If

        If MessageDialogOptions.BackdropMode <> PopupBackdropMode.Image OrElse
           MessageDialogOptions.BackdropImage Is Nothing Then Return False

        context.Compositor.D3D_BackdropSurfaceRenderer.SetImage(MessageDialogOptions.BackdropImage)
        context.Compositor.D3D_BackdropSurfaceRenderer.ApplyParameters(MessageDialogOptions.BackdropBlurRadius,
                                                                       MessageDialogOptions.BackdropBlurPasses,
                                                                       MessageDialogOptions.BackdropDownsampleFactor,
                                                                       MessageDialogOptions.BackdropNoiseScale)
        context.Compositor.D3D_BackdropSurfaceRenderer.TintColor = MessageDialogOptions.BackdropTintColor
        context.Compositor.D3D_BackdropSurfaceRenderer.NoiseOpacity = MessageDialogOptions.BackdropNoiseOpacity
        context.Compositor.D3D_BackdropSurfaceRenderer.DrawImageBackdrop(context, bounds)
        Return True
    End Function

    Public Sub FillRectangle(context As D3D_PaintContext,
                             rect As RectangleF,
                             color As Color)
        If context Is Nothing OrElse color.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        context.FillRectangle(rect, color)
    End Sub

    Public Sub DrawRectangle(context As D3D_PaintContext,
                             rect As RectangleF,
                             color As Color,
                             width As Single)
        If context Is Nothing OrElse color.A = 0 OrElse width <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        context.DrawRectangle(rect, color, width)
    End Sub

    Public Sub DrawInsetRectangle(context As D3D_PaintContext,
                                  bounds As RectangleF,
                                  color As Color,
                                  width As Single)
        If context Is Nothing OrElse color.A = 0 OrElse width <= 0 OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        ' D2D DrawRectangle 的 stroke 以路径为中心线；外框绘制要先 inset，避免高 DPI 下半个 stroke 落到窗口外。
        Dim half As Single = width / 2.0F
        Dim rect As New RectangleF(bounds.X + half,
                                   bounds.Y + half,
                                   Math.Max(0.0F, bounds.Width - width),
                                   Math.Max(0.0F, bounds.Height - width))
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        context.DrawRectangle(rect, color, width)
    End Sub

    Public Sub FillRoundedRectangle(context As D3D_PaintContext,
                                    rect As RectangleF,
                                    color As Color,
                                    radius As Single)
        If context Is Nothing OrElse color.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        If radius <= 0.0F Then
            context.FillRectangle(rect, color)
            Return
        End If

        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        context.FillRoundedRectangle(rect, radius, brush)
    End Sub

    Public Sub DrawRoundedRectangle(context As D3D_PaintContext,
                                    rect As RectangleF,
                                    color As Color,
                                    width As Single,
                                    radius As Single)
        If context Is Nothing OrElse color.A = 0 OrElse width <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim half = width / 2.0F
        rect.Inflate(-half, -half)
        If radius <= 0.0F Then
            context.DrawRectangle(rect, color, width)
            Return
        End If

        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        context.DrawRoundedRectangle(rect, radius, brush, width)
    End Sub

    Public Sub DrawText(context As D3D_PaintContext,
                        text As String,
                        font As Font,
                        rect As RectangleF,
                        color As Color,
                        flags As TextFormatFlags,
                        dpiScale As Single)
        If context Is Nothing OrElse String.IsNullOrEmpty(text) OrElse font Is Nothing OrElse color.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return

        Dim hAlign As Vortice.DirectWrite.TextAlignment = Vortice.DirectWrite.TextAlignment.Leading
        If (flags And TextFormatFlags.HorizontalCenter) = TextFormatFlags.HorizontalCenter Then
            hAlign = Vortice.DirectWrite.TextAlignment.Center
        ElseIf (flags And TextFormatFlags.Right) = TextFormatFlags.Right Then
            hAlign = Vortice.DirectWrite.TextAlignment.Trailing
        End If

        Dim vAlign As Vortice.DirectWrite.ParagraphAlignment = Vortice.DirectWrite.ParagraphAlignment.Near
        If (flags And TextFormatFlags.VerticalCenter) = TextFormatFlags.VerticalCenter Then
            vAlign = Vortice.DirectWrite.ParagraphAlignment.Center
        ElseIf (flags And TextFormatFlags.Bottom) = TextFormatFlags.Bottom Then
            vAlign = Vortice.DirectWrite.ParagraphAlignment.Far
        End If

        Dim wrap = (flags And TextFormatFlags.WordBreak) = TextFormatFlags.WordBreak
        context.DrawText(text, font, color, rect, hAlign, vAlign, wrap)
    End Sub

    Public Sub DrawImage(context As D3D_PaintContext,
                         image As Image,
                         rect As RectangleF)
        If context Is Nothing OrElse image Is Nothing OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        context.DrawImage(image, rect)
    End Sub

    Public Sub DrawCloseButton(context As D3D_PaintContext,
                               rect As RectangleF,
                               hovered As Boolean,
                               pressed As Boolean,
                               normalForeColor As Color,
                               hoverForeColor As Color,
                               hoverBackColor As Color,
                               dpiScale As Single)
        If context Is Nothing OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return

        If pressed Then
            FillRectangle(context, rect, Color.FromArgb(180, hoverBackColor))
        ElseIf hovered Then
            FillRectangle(context, rect, hoverBackColor)
        End If

        Dim glyphColor = If(hovered OrElse pressed, hoverForeColor, normalForeColor)
        If glyphColor.A = 0 Then Return

        Dim cx = rect.X + rect.Width / 2.0F
        Dim cy = rect.Y + rect.Height / 2.0F
        Dim half = 5.0F * dpiScale
        Dim width = Math.Max(1.0F, 1.2F * dpiScale)
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, glyphColor, context.DeviceGeneration)
        context.DeviceContext.DrawLine(New Vector2(cx - half, cy - half), New Vector2(cx + half, cy + half), brush, width)
        context.DeviceContext.DrawLine(New Vector2(cx + half, cy - half), New Vector2(cx - half, cy + half), brush, width)
    End Sub
End Module
