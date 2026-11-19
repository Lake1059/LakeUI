Imports System.Runtime.CompilerServices
Imports Vortice.Mathematics

Public Interface ID3D12Renderable
    Sub RenderGpu12(context As D3D12_PaintContext)
End Interface

Public NotInheritable Class D3D12_PaintContext
    Private presenter As D3D12_CompositionPresenter

    Friend Sub New(target As D3D12_CompositionPresenter)
        presenter = target
    End Sub

    Private origin As Point
    Private opaqueRect As Rectangle

    Friend Sub AtOffset(offset As Point, draw As Action)
        Dim old = origin
        origin.Offset(offset.X, offset.Y)
        Try
            draw()
        Finally
            origin = old
        End Try
    End Sub

    Private Function Map(rect As RectangleF) As RectangleF
        rect.Offset(origin.X, origin.Y)
        Return rect
    End Function

    Public Sub FillRectangle(bounds As Rectangle, color As Drawing.Color)
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        bounds.Offset(origin)
        If color.A > 0 Then presenter.RenderRectangle(bounds, ToColor(color))
        If color.A = 255 Then
            Dim filled = Rectangle.Intersect(bounds, presenter.FrameClip)
            If Not opaqueRect.Contains(filled) Then opaqueRect = filled
        End If
    End Sub

    Public Sub FillRectangle(bounds As RectangleF, color As Drawing.Color)
        FillRectangle(Rectangle.Round(bounds), color)
    End Sub

    Public Sub FillRoundedRectangle(bounds As RectangleF, radius As Single, color As Drawing.Color)
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        If color.A = 0 Then Return
        presenter.RenderRoundedRectangle(Rectangle.Round(Map(bounds)), radius, ToColor(color))
    End Sub

    Friend Sub FillShape(bounds As RectangleF, radius As Single, color As Drawing.Color, Optional last As Drawing.Color = Nothing,
                         Optional direction As Orientation = Orientation.Horizontal, Optional reverse As Boolean = False,
                         Optional gradient As RectangleF = Nothing, Optional maskRadius As Single = 0, Optional mask As RectangleF = Nothing)
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        If last.IsEmpty OrElse last.A = 0 Then last = color
        If last.A = 0 AndAlso color.A = 0 Then Return
        If gradient.IsEmpty Then gradient = bounds
        presenter.DrawShape(Rectangle.Round(Map(bounds)), radius, ToColor(If(reverse, last, color)), ToColor(If(reverse, color, last)),
                            Map(gradient), direction = Orientation.Horizontal, maskRadius, Map(mask))
    End Sub

    Public Sub StrokeRoundedRectangle(bounds As RectangleF, radius As Single, color As Drawing.Color, width As Single)
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        If width <= 0 OrElse color.A = 0 Then Return
        bounds.Inflate(width / 2, width / 2)
        presenter.RenderRoundedRectangleStroke(Rectangle.Round(Map(bounds)), radius + width / 2, width, ToColor(color))
    End Sub

    Public Sub DrawRectangle(bounds As RectangleF, color As Drawing.Color, width As Single)
        If width <= 0 Then Return
        DrawPanelShape(bounds, 0, width, 0, color)
    End Sub

    Public Sub DrawLine(start As System.Numerics.Vector2, finish As System.Numerics.Vector2, color As Drawing.Color, width As Single)
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        Dim translation As New System.Numerics.Vector2(origin.X, origin.Y)
        start += translation
        finish += translation
        Dim delta = finish - start
        Dim length = delta.Length()
        If length <= 0 OrElse width <= 0 OrElse color.A = 0 Then Return
        Dim normal As New System.Numerics.Vector2(-delta.Y, delta.X)
        normal *= width / (2 * length)
        presenter.RenderPolygon({New PointF(start.X + normal.X, start.Y + normal.Y), New PointF(finish.X + normal.X, finish.Y + normal.Y),
                                 New PointF(finish.X - normal.X, finish.Y - normal.Y), New PointF(start.X - normal.X, start.Y - normal.Y)}, ToColor(color))
    End Sub

    Friend Sub DrawArc(area As RectangleF, width As Single, start As Single, sweep As Single, color As Drawing.Color,
                       Optional last As Drawing.Color = Nothing, Optional fraction As Single = 1)
        If last.IsEmpty Then last = color
        presenter.DrawArc(Map(area), width, start, sweep, ToColor(color), ToColor(last), fraction)
    End Sub

    Friend Sub DrawHistory(points As System.Numerics.Vector2(), count As Integer, color As Drawing.Color, bottom As Single, width As Single)
        If count < 2 OrElse color.A = 0 Then Return
        Dim triangles((count - 1) * 6 - 1) As PointF
        For i = 0 To count - 2
            Dim a = points(i)
            Dim b = points(i + 1)
            Dim a2 As New System.Numerics.Vector2(a.X, bottom)
            Dim b2 As New System.Numerics.Vector2(b.X, bottom)
            If width > 0 Then
                Dim delta = b - a
                Dim side As New System.Numerics.Vector2(-delta.Y, delta.X)
                side *= width / (2 * Math.Max(0.001F, delta.Length()))
                a2 = a - side
                b2 = b - side
                a += side
                b += side
            End If
            Dim k = i * 6
            triangles(k) = New PointF(a.X + origin.X, a.Y + origin.Y)
            triangles(k + 1) = New PointF(b.X + origin.X, b.Y + origin.Y)
            triangles(k + 2) = New PointF(a2.X + origin.X, a2.Y + origin.Y)
            triangles(k + 3) = triangles(k + 2)
            triangles(k + 4) = triangles(k + 1)
            triangles(k + 5) = New PointF(b2.X + origin.X, b2.Y + origin.Y)
        Next
        presenter.DrawTriangles(triangles, ToColor(color))
    End Sub

    Friend Sub FillRoundedTriangle(vertices As System.Numerics.Vector2(), radius As Single, color As Drawing.Color)
        If color.A = 0 Then Return
        Dim points As New List(Of PointF)()
        For i = 0 To 2
            Dim vertex = vertices(i)
            Dim before = vertices((i + 2) Mod 3) - vertex
            Dim after = vertices((i + 1) Mod 3) - vertex
            If before.LengthSquared() < 0.0001F OrElse after.LengthSquared() < 0.0001F Then Return
            If radius <= 0 Then
                points.Add(New PointF(vertex.X + origin.X, vertex.Y + origin.Y))
                Continue For
            End If
            Dim incoming = vertex + System.Numerics.Vector2.Normalize(before) * Math.Min(radius, before.Length() / 2)
            Dim outgoing = vertex + System.Numerics.Vector2.Normalize(after) * Math.Min(radius, after.Length() / 2)
            Dim segments = Math.Clamp(CInt(Math.Ceiling(Math.Sqrt(radius * 2))), 2, 32)
            For j = 0 To segments
                Dim t = j / CSng(segments)
                Dim v = incoming * ((1 - t) * (1 - t)) + vertex * (2 * t * (1 - t)) + outgoing * (t * t)
                points.Add(New PointF(v.X + origin.X, v.Y + origin.Y))
            Next
        Next
        presenter.RenderPolygon(points.ToArray(), ToColor(color))
    End Sub

    Friend Sub DrawRoundLine(a As System.Numerics.Vector2, b As System.Numerics.Vector2, color As Drawing.Color, width As Single)
        DrawLine(a, b, color, width)
        Dim half = width / 2
        FillEllipse(New RectangleF(a.X - half, a.Y - half, width, width), color)
        FillEllipse(New RectangleF(b.X - half, b.Y - half, width, width), color)
    End Sub

    Public Sub FillEllipse(bounds As RectangleF, color As Drawing.Color)
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        If color.A > 0 Then presenter.RenderEllipse(Rectangle.Round(Map(bounds)), ToColor(color), 0)
    End Sub

    Public Sub StrokeEllipse(bounds As RectangleF, color As Drawing.Color, width As Single)
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        If color.A = 0 OrElse width <= 0 Then Return
        bounds.Inflate(width / 2, width / 2)
        presenter.RenderEllipse(Rectangle.Round(Map(bounds)), ToColor(color), width)
    End Sub

    Private Function ToColor(color As Drawing.Color) As Color4
        Dim value = D3D_HdrOutput.MapColor4(color)
        If Not presenter.FloatTarget Then Return value
        Return New Color4(DecodeSrgb(value.R), DecodeSrgb(value.G), DecodeSrgb(value.B), value.A)
    End Function

    Private Shared Function DecodeSrgb(value As Single) As Single
        If value <= 0.04045F Then Return value / 12.92F
        Return MathF.Pow((value + 0.055F) / 1.055F, 2.4F)
    End Function

    Public Function PushClip(bounds As RectangleF) As IDisposable
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        Return New ClipScope(presenter, Rectangle.Round(Map(bounds)))
    End Function

    Friend ReadOnly Property DirtyRectangle As Rectangle
        Get
            Dim clip = presenter.FrameClip
            clip.Offset(-origin.X, -origin.Y)
            Return clip
        End Get
    End Property

    Friend Function IntersectsDirty(bounds As RectangleF) As Boolean
        Return bounds.IntersectsWith(DirtyRectangle)
    End Function

    Private NotInheritable Class ClipScope
        Implements IDisposable
        Private target As D3D12_CompositionPresenter
        Private ReadOnly previous As Rectangle
        Friend Sub New(presenter As D3D12_CompositionPresenter, bounds As Rectangle)
            target = presenter
            previous = target.FrameClip
            target.FrameClip = Rectangle.Intersect(previous, bounds)
        End Sub
        Public Sub Dispose() Implements IDisposable.Dispose
            If target Is Nothing Then Return
            target.FrameClip = previous
            target = Nothing
        End Sub
    End Class

    Friend Sub DrawPanelShape(area As RectangleF, radius As Single, stroke As Single, corners As Integer, color As Drawing.Color)
        If color.A = 0 Then Return
        If stroke > 0 Then
            area.Inflate(stroke / 2, stroke / 2)
            radius += stroke / 2
        End If
        presenter.RenderPanelShape(Rectangle.Round(Map(area)), ToColor(color), radius, stroke, corners)
    End Sub

    Public Sub DrawImage(image As Image, destination As RectangleF)
        DrawMaskedImage(image, destination, Nothing, 0, 0)
    End Sub

    Private Shared ReadOnly immutableImageKeys As New ConditionalWeakTable(Of Image, Object)()

    ' For decoder-owned images whose contents do not change during their lifetime.
    Friend Sub DrawImmutableImage(image As Image, destination As RectangleF)
        Dim key = (immutableImageKeys.GetValue(image, Function(source) New Object()), D3D_HdrOutput.ImageRevision)
        presenter.DrawLinearTile(image.Width, image.Height, Function() DecodeImage(image), Map(destination), Nothing, False, key,
                                 Vortice.DXGI.Format.B8G8R8A8_UNorm)
    End Sub

    Friend Sub DrawMaskedImage(image As Image, destination As RectangleF, mask As RectangleF, radius As Single, corners As Integer)
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        If image Is Nothing OrElse destination.Width <= 0 OrElse destination.Height <= 0 Then Return
        Dim mappedDestination = Map(destination)
        If Not mappedDestination.IntersectsWith(presenter.FrameClip) Then Return
        presenter.DrawPremultipliedImage(image.Width, image.Height, DecodeImage(image), mappedDestination, 1, Map(mask), radius, corners)
    End Sub

    Friend Shared Function DecodeImage(image As Image) As Byte()
        Using bitmap As New Bitmap(image.Width, image.Height, Imaging.PixelFormat.Format32bppPArgb)
            Using graphics = Drawing.Graphics.FromImage(bitmap)
                graphics.CompositingMode = Drawing2D.CompositingMode.SourceCopy
                graphics.DrawImage(image, New Rectangle(0, 0, bitmap.Width, bitmap.Height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel)
            End Using
            D3D_HdrOutput.MapBitmapForImageUpload(bitmap)
            Dim data = bitmap.LockBits(New Rectangle(0, 0, bitmap.Width, bitmap.Height), Imaging.ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppPArgb)
            Dim bytes(bitmap.Width * bitmap.Height * 4 - 1) As Byte
            Try
                For y = 0 To bitmap.Height - 1
                    Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, bytes, y * bitmap.Width * 4, bitmap.Width * 4)
                Next
            Finally
                bitmap.UnlockBits(data)
            End Try
            Return bytes
        End Using
    End Function

    Friend Sub DrawBlurredBackdrop(image As Image, bounds As RectangleF, radius As Integer, passes As Integer, downsample As Integer)
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        presenter.DrawBlurredBackdrop(image, Map(bounds), radius, passes, downsample)
    End Sub

    Friend Sub DrawNoise(bounds As RectangleF, scale As Single, opacity As Byte)
        presenter.DrawNoise(Map(bounds), scale, opacity)
    End Sub

    Public Sub DrawPremultipliedImage(width As Integer, height As Integer, pixels As Byte(), dest As RectangleF, Optional opacity As Single = 1.0F)
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        presenter.DrawPremultipliedImage(width, height, pixels, Map(dest), opacity)
    End Sub

    Friend Sub DrawGlyphCoverage(width As Integer, height As Integer, pixels As Byte(), dest As RectangleF, ink As Color4, Optional subpixel As Boolean = False)
        presenter.DrawGlyphCoverage(width, height, pixels, Map(dest), ink, subpixel)
    End Sub

    ''' <summary>Draws tightly packed premultiplied RGBA16F scRGB pixels from the given normalized source rectangle.</summary>
    Public Sub DrawLinearImage(width As Integer, height As Integer, pixels As Byte(), dest As RectangleF,
                               Optional sourceUV As RectangleF = Nothing, Optional nearest As Boolean = False)
        DrawLinearTile(width, height, Function() pixels, dest, sourceUV, nearest, Nothing)
    End Sub

    Friend Sub DrawLinearTile(width As Integer, height As Integer, getPixels As Func(Of Byte()), dest As RectangleF,
                              uv As RectangleF, nearest As Boolean, key As Object)
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        presenter.DrawLinearTile(width, height, getPixels, Map(dest), uv, nearest, key)
    End Sub

    Public Sub DrawText(text As String, font As Font, color As Drawing.Color, bounds As RectangleF, dpiScale As Single,
                        Optional alignment As Vortice.DirectWrite.TextAlignment = Vortice.DirectWrite.TextAlignment.Leading,
                        Optional vertical As Vortice.DirectWrite.ParagraphAlignment = Vortice.DirectWrite.ParagraphAlignment.Near,
                        Optional wordWrap As Boolean = False, Optional trim As Boolean = False)
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        If String.IsNullOrEmpty(text) OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 OrElse color.A = 0 Then Return
        ' Component coverage is valid only where this paint has established opaque pixels.
        Dim subpixel = GlobalOptions.GlobalTextQuality = GlobalOptions.TextQualityMode.ClearType AndAlso
            presenter.SampleScale = 1 AndAlso Not presenter.FloatTarget AndAlso
            opaqueRect.Contains(Rectangle.Intersect(Rectangle.Round(Map(bounds)), presenter.FrameClip))
        ' An integral translation keeps the same raster phase after a layout or scroll change.
        Dim placement As New Point(CInt(Math.Floor(bounds.X)), CInt(Math.Floor(bounds.Y)))
        Dim rasterBounds As New RectangleF(bounds.X - placement.X, bounds.Y - placement.Y, bounds.Width, bounds.Height)
        Dim key = (text, font.Name, font.SizeInPoints, font.Style, rasterBounds, dpiScale, subpixel, alignment, vertical, wordWrap, trim, TextQualityKey())
        Using PushClip(bounds)
            If presenter.FrameClip.Width <= 0 OrElse presenter.FrameClip.Height <= 0 Then Return
            Dim offset As New Point(origin.X + placement.X, origin.Y + placement.Y)
            presenter.DrawGlyphBatch(key, ToColor(color), Sub() RasterizeText(text, font, color, bounds, dpiScale, alignment, vertical, wordWrap, trim:=trim, subpixel:=subpixel), offset)
        End Using
    End Sub

    Friend Sub DrawTransformedText(text As String, font As Font, color As Drawing.Color, bounds As RectangleF, dpiScale As Single,
                                   transform As System.Numerics.Matrix3x2)
        If String.IsNullOrEmpty(text) OrElse font Is Nothing OrElse color.A = 0 OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        Dim key = (text, font.Name, font.SizeInPoints, font.Style, bounds, origin, dpiScale, transform, D3D_HdrOutput.VectorColorRevision, TextQualityKey())
        presenter.DrawGlyphBatch(key, ToColor(color), Sub() RasterizeText(text, font, color, bounds, dpiScale,
                       Vortice.DirectWrite.TextAlignment.Center, Vortice.DirectWrite.ParagraphAlignment.Center, False, transform))
    End Sub

    Friend Sub DrawTextLayout(layout As Vortice.DirectWrite.IDWriteTextLayout, color As Drawing.Color, location As System.Numerics.Vector2)
        If layout Is Nothing OrElse color.A = 0 Then Return
        Using renderer As New D3D12_GlyphRenderer(Me, ToColor(color), Nothing, presenter.SampleScale)
            layout.Draw(renderer, location.X, location.Y)
        End Using
    End Sub

    Friend Sub FillPolygon(points As PointF(), color As Drawing.Color)
        If color.A = 0 Then Return
        Dim translated = points.Select(Function(p) New PointF(p.X + origin.X, p.Y + origin.Y)).ToArray()
        presenter.RenderPolygon(translated, ToColor(color))
    End Sub

    Private Shared Function TextQualityKey() As (GlobalOptions.TextQualityMode, Vortice.DirectWrite.RenderingMode, Vortice.DirectWrite.GridFitMode)
        Return (GlobalOptions.GlobalTextQuality, GlobalOptions.OutlineText.RenderingMode, GlobalOptions.OutlineText.GridFitMode)
    End Function

    Private Sub RasterizeText(text As String, font As Font, color As Drawing.Color, bounds As RectangleF, dpiScale As Single,
                              alignment As Vortice.DirectWrite.TextAlignment, vertical As Vortice.DirectWrite.ParagraphAlignment, wordWrap As Boolean,
                              Optional transform As System.Numerics.Matrix3x2? = Nothing, Optional trim As Boolean = False, Optional subpixel As Boolean = False)
        Dim dw = D3D_D2DInterop.GetDWriteFactory()
        Using format = D3D_TextMeasureHelper.CreateDWriteTextFormat(font, dpiScale),
              layout = dw.CreateTextLayout(text, format, bounds.Width, bounds.Height),
              renderer As New D3D12_GlyphRenderer(Me, ToColor(color), transform, presenter.SampleScale, subpixel)
            layout.WordWrapping = If(wordWrap, Vortice.DirectWrite.WordWrapping.Wrap, Vortice.DirectWrite.WordWrapping.NoWrap)
            layout.TextAlignment = alignment
            layout.ParagraphAlignment = vertical
            If trim Then
                Dim options As New Vortice.DirectWrite.Trimming With {.Granularity = Vortice.DirectWrite.TrimmingGranularity.Character}
                Using sign = dw.CreateEllipsisTrimmingSign(format)
                    layout.SetTrimming(options, sign)
                End Using
            End If
            layout.Draw(renderer, bounds.Left, bounds.Top)
        End Using
    End Sub

    Public Function DrawBackgroundSource(source As Control, clip As Drawing.Rectangle) As Boolean
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        If Not D3D12_BackgroundDependencies.Accept(presenter.OwnerControl, source) Then Return False
        Dim provider = D3D12StartupDispatch.FindPresenter(source)
        D3D12_BackgroundDependencies.Sample(presenter.OwnerControl, source, If(provider?.RenderCount, 0L))
        If Not source.IsHandleCreated Then Return False
        If TypeOf source Is Form AndAlso provider Is Nothing Then
            Dim owner = presenter.OwnerControl
            Dim offset = owner.PointToClient(source.PointToScreen(Point.Empty))
            Dim sourceBounds As New Rectangle(offset, source.ClientSize)
            Dim paintBounds = Rectangle.Intersect(Rectangle.Intersect(Rectangle.Round(Map(clip)), sourceBounds), presenter.FrameClip)
            If paintBounds.Width <= 0 OrElse paintBounds.Height <= 0 Then Return False
            paintBounds.Offset(-origin.X, -origin.Y)
            Using PushClip(paintBounds)
                Return ThisIsYourWindow.DrawFormSurface12(Me, DirectCast(source, Form), New Point(offset.X - origin.X, offset.Y - origin.Y))
            End Using
        End If
        Return presenter.DrawSource(provider, clip)
    End Function

    Friend Function DrawPublishedSource(source As D3D12_CompositionPresenter, clip As Drawing.Rectangle) As Boolean
        Return presenter.DrawSource(source, clip)
    End Function

    Friend Sub EndFrame()
        presenter = Nothing
    End Sub
End Class

Friend Module D3D12StartupDispatch
    Private group As D3D12_DeviceManager
    Private ownerThread As Integer
    Private recovering As Boolean
    Private ReadOnly presenters As New ConditionalWeakTable(Of Control, D3D12_CompositionPresenter)()

    Private ReadOnly atlases As New Dictionary(Of Control, D3D12_WindowAtlas)()
    Private batchDepth As Integer

    Friend Sub BeginBatch()
        batchDepth += 1
    End Sub

    Friend Sub EndBatch()
        batchDepth -= 1
        If batchDepth <> 0 OrElse RenderBackendStartup.Failure IsNot Nothing Then Return
        Try
            For Each atlas In atlases.Values.ToArray()
                atlas.Flush()
            Next
        Catch ex As Exception
            If Not ScheduleDeviceRecovery(ex) Then RenderBackendStartup.Failed(ex)
        End Try
    End Sub

    <Runtime.InteropServices.DllImport("user32.dll")>
    Private Function GetUpdateRect(hwnd As IntPtr, rect As IntPtr, eraseBackground As Boolean) As Boolean
    End Function

    Friend Function ConsumeCoveredPaint(owner As Control) As Boolean
        Dim presenter = FindPresenter(owner)
        If presenter Is Nothing OrElse Not presenter.PaintAlreadyRecorded Then Return False
        presenter.PaintAlreadyRecorded = False
        Return True
    End Function

    Friend Function FindPresenter(owner As Control) As D3D12_CompositionPresenter
        Dim result As D3D12_CompositionPresenter = Nothing
        If owner IsNot Nothing Then presenters.TryGetValue(owner, result)
        Return result
    End Function

    Friend Sub CheckVisibleTree(root As Control)
        If root Is Nothing OrElse root.IsDisposed OrElse Not root.Visible Then Return
        If D3D_V5Presentation.IsV5Control(root) AndAlso Not TypeOf root Is ID3D12Renderable Then
            Throw New NotSupportedException($"Missing native DX12 renderer for {root.GetType().FullName}.")
        End If
        If root.HasChildren Then
            For Each child As Control In root.Controls
                CheckVisibleTree(child)
            Next
        End If
    End Sub

    Friend Sub Render(owner As Control, Optional dirty As Rectangle = Nothing)
        If RenderBackendStartup.Failure IsNot Nothing OrElse recovering Then Return
        Dim designerPreview = RenderBackendStartup.IsDesignerPreview
        Dim source = TryCast(owner, ID3D12Renderable)
        ' A legacy custom control in the designer must not block other controls' previews.
        If designerPreview AndAlso source Is Nothing Then Return
        Try
            Dim current = Environment.CurrentManagedThreadId
            Dim previous = Threading.Interlocked.CompareExchange(ownerThread, current, 0)
            If previous <> 0 AndAlso previous <> current Then Throw New NotSupportedException("The DX12 backend requires a single rendering UI thread.")
            If Not designerPreview AndAlso RenderBackendStartup.Active Is Nothing Then CheckVisibleTree(D3D_RenderCore.ResolveCompositorForm(owner))
            If source Is Nothing Then Throw New NotSupportedException($"Missing native DX12 renderer for {owner.GetType().FullName}.")
            If group Is Nothing Then group = New D3D12_DeviceManager()
            Dim presenter = presenters.GetValue(owner, AddressOf Attach)
            presenter.Resize(owner.ClientSize.Width, owner.ClientSize.Height)
            Dim chromeKey = TryCast(owner, ThisIsYourWindow.ChromeOverlayControl)?.ChromeCacheKey()
            Dim panel = TryCast(owner, ModernPanel)
            If panel IsNot Nothing Then
                Dim forwarded As Control = Nothing
                If panel.TryGetTransparentBackgroundForward(forwarded) AndAlso TypeOf forwarded Is Form AndAlso forwarded.IsHandleCreated Then
                    chromeKey = ThisIsYourWindow.StaticBackdropKey(DirectCast(forwarded, Form), owner)
                End If
            End If
            If Not designerPreview AndAlso chromeKey IsNot Nothing AndAlso Equals(chromeKey, presenter.ChromeKey) AndAlso presenter.RetainedContent IsNot Nothing Then Return
            Dim invalidation = presenter.InvalidationVersion
            ' WM_PAINT can carry a designer selection change after a queued frame.
            Dim coveredPaint = Not designerPreview AndAlso batchDepth > 0 AndAlso GetUpdateRect(owner.Handle, IntPtr.Zero, False)
            Dim coverage = TryCast(owner, D3D_IGpuDirtyRegionCoverage)
            If designerPreview OrElse coverage Is Nothing OrElse Not coverage.CoversDirtyRegion(dirty) Then dirty = Rectangle.Empty
            presenter.RenderFrame(Sub(context)
                Dim ancestor = D3D_ControlSurfaceRegistry.ResolveNearestGpuAncestor(owner)
                Dim opacity = TryCast(owner, D3D_IBackgroundOpacitySource)
                Dim provider = TryCast(owner, D3D_IBackgroundSourceProvider)
                Dim explicitSource As Control = Nothing
                Dim opaque = opacity IsNot Nothing AndAlso opacity.IsBackgroundOpaque
                If ancestor IsNot Nothing AndAlso Not opaque AndAlso Not (provider IsNot Nothing AndAlso provider.TryGetBackgroundSource(explicitSource) AndAlso explicitSource IsNot Nothing) Then
                    Dim parentPresenter = FindPresenter(ancestor)
                    If parentPresenter IsNot Nothing Then
                        context.DrawPublishedSource(parentPresenter, owner.ClientRectangle)
                    ElseIf TypeOf ancestor Is Form AndAlso Not TypeOf owner Is ThisIsYourWindow.ChromeOverlayControl AndAlso
                           Not ThisIsYourWindow.DrawFormBackdrop12(context, DirectCast(ancestor, Form), owner) Then
                        context.FillRectangle(owner.ClientRectangle, ancestor.BackColor)
                    End If
                End If
                source.RenderGpu12(context)
                If designerPreview Then D3D_PaintBridge.绘制设计时选择装饰(context, owner)
            End Sub, dirty)
            presenter.PaintAlreadyRecorded = coveredPaint AndAlso invalidation = presenter.InvalidationVersion
            presenter.ChromeKey = chromeKey
            If batchDepth = 0 Then presenter.Atlas.Flush()
        Catch ex As Exception
            If ScheduleDeviceRecovery(ex) Then Return
            If RenderBackendStartup.Active Is Nothing AndAlso ownerThread = Environment.CurrentManagedThreadId Then
                ReleaseFailedStartup()
                group?.Dispose()
                group = Nothing
            End If
            RenderBackendStartup.Failed(ex)
        End Try
    End Sub

    Friend ReadOnly Property CurrentGroup As D3D12_DeviceManager
        Get
            Return group
        End Get
    End Property

    Private Function ScheduleDeviceRecovery(failure As Exception) As Boolean
        If recovering Then Return True
        If RenderBackendSwitching.InTransition OrElse RenderBackendStartup.Active <> RenderBackend.Direct3D12 Then Return False
        If group Is Nothing OrElse Not (group.IsDeviceRemoved OrElse D3D_DeviceManager.IsDeviceLostException(failure)) Then Return False
        Dim anchor = presenters.Select(Function(pair) pair.Key).FirstOrDefault(Function(c) c.IsHandleCreated AndAlso Not c.IsDisposed)
        If anchor Is Nothing Then Return False
        recovering = True
        Try
            anchor.BeginInvoke(New MethodInvoker(AddressOf RecoverDevice))
            Return True
        Catch ex As InvalidOperationException
            recovering = False
            Return False
        End Try
    End Function

    Private Sub RecoverDevice()
        recovering = False
        If RenderBackendStartup.Requested <> RenderBackend.Direct3D12 Then Return
        RenderBackendSwitching.RecoverDx12()
    End Sub

    Friend Sub Install(candidate As D3D12_DeviceManager)
        If group IsNot Nothing Then Throw New InvalidOperationException("DX12 device group already installed.")
        group = candidate
    End Sub

    Friend Sub DrainForSwitch()
        If group IsNot Nothing AndAlso Not group.WaitForIdle(TimeSpan.FromSeconds(2)) Then Throw New TimeoutException("DX12 queue drain timed out.")
    End Sub

    Friend Sub ReleaseBackend()
        ReleaseFailedStartup()
        group?.Dispose()
        group = Nothing
        ownerThread = 0
    End Sub

    Private Sub ReleaseFailedStartup()
        For Each pair In presenters.ToArray()
            Detach(pair.Key, EventArgs.Empty)
        Next
        For Each atlas In atlases.Values.ToArray()
            atlas.Dispose()
        Next
        atlases.Clear()
    End Sub

    Private Function Attach(owner As Control) As D3D12_CompositionPresenter
        Dim root As Control = owner
        While root.Parent IsNot Nothing
            root = root.Parent
        End While
        Dim atlas As D3D12_WindowAtlas = Nothing
        If Not atlases.TryGetValue(root, atlas) Then
            atlas = New D3D12_WindowAtlas(group)
            atlases.Add(root, atlas)
        End If
        Dim presenter As New D3D12_CompositionPresenter(owner, group, atlas)
        AddHandler owner.Invalidated, AddressOf InvalidatePaint
        AddHandler owner.VisibleChanged, AddressOf VisibilityChanged
        AddHandler owner.HandleDestroyed, AddressOf Detach
        Return presenter
    End Function

    Private Sub InvalidatePaint(sender As Object, e As InvalidateEventArgs)
        Dim p = FindPresenter(DirectCast(sender, Control))
        If p IsNot Nothing Then
            p.InvalidationVersion += 1
            p.PaintAlreadyRecorded = False
        End If
    End Sub

    Private Sub VisibilityChanged(sender As Object, e As EventArgs)
        Dim owner = DirectCast(sender, Control)
        If owner.Visible AndAlso owner.IsHandleCreated AndAlso Not owner.IsDisposed Then D3D_V5Presentation.RequestRenderBatched(owner)
    End Sub

    Private Sub Detach(sender As Object, e As EventArgs)
        Dim owner = DirectCast(sender, Control)
        D3D12_BackgroundDependencies.Remove(owner)
        RemoveHandler owner.Invalidated, AddressOf InvalidatePaint
        RemoveHandler owner.VisibleChanged, AddressOf VisibilityChanged
        RemoveHandler owner.HandleDestroyed, AddressOf Detach
        Dim presenter As D3D12_CompositionPresenter = Nothing
        If presenters.TryGetValue(owner, presenter) Then
            presenters.Remove(owner)
            Dim atlas = presenter.Atlas
            presenter.Dispose()
            If atlas.Count = 0 Then
                Dim root = atlases.First(Function(item) item.Value Is atlas).Key
                atlases.Remove(root)
                atlas.Dispose()
            End If
        End If
    End Sub
End Module
