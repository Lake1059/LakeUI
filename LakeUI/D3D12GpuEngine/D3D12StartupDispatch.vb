Imports System.Numerics
Imports System.Runtime.CompilerServices
Imports Vortice.Mathematics

Public Interface ID3D12Renderable
    Sub RenderGpu12(context As D3D12_PaintContext)
End Interface

Public NotInheritable Class D3D12_PaintContext
    Private presenter As D3D12_CompositionPresenter

    Private ReadOnly backgroundFrame As Long
    Private ReadOnly completeFrame As Boolean
    Private backdropClip As Rectangle

    Friend Sub New(target As D3D12_CompositionPresenter)
        presenter = target
        backgroundFrame = D3D12_BackgroundDependencies.NextFrame()
        backdropClip = target.OwnerControl.ClientRectangle
        completeFrame = target.FrameClip = backdropClip
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
        presenter.RenderRoundedRectangle(Map(bounds), radius, ToColor(color))
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
        presenter.RenderRoundedRectangle(Map(bounds), radius, ToColor(color), width)
    End Sub

    Public Sub DrawRectangle(bounds As RectangleF, color As Drawing.Color, width As Single)
        StrokeRoundedRectangle(bounds, 0, color, width)
    End Sub

    Public Sub DrawLine(start As System.Numerics.Vector2, finish As System.Numerics.Vector2, color As Drawing.Color, width As Single)
        DrawLineCore(start, finish, color, width, roundCaps:=False)
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
        DrawLineCore(a, b, color, width, roundCaps:=True)
    End Sub

    Friend Sub DrawJoinedRoundLines(start As Vector2, joint As Vector2, finish As Vector2, color As Drawing.Color, width As Single)
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        If color.A = 0 OrElse width <= 0 Then Return
        Dim offset As New Vector2(origin.X, origin.Y)
        presenter.RenderJoinedRoundLines(start + offset, joint + offset, finish + offset, width, ToColor(color))
    End Sub

    Friend Sub DrawAnimatedCheckMark(bounds As RectangleF, inset As Single, progress As Single, color As Drawing.Color, width As Single)
        If color.A = 0 OrElse width <= 0 Then Return
        Dim start As New Vector2(bounds.Left + inset, bounds.Top + bounds.Height * 0.5F)
        Dim joint As New Vector2(bounds.Left + bounds.Width * 0.4F, bounds.Bottom - inset)
        Dim finish As New Vector2(bounds.Right - inset, bounds.Top + inset)
        Dim firstLength = Vector2.Distance(start, joint)
        Dim lastLength = Vector2.Distance(joint, finish)
        Dim length = firstLength + lastLength
        If length < 0.01F Then Return

        Dim visibleLength = length * progress
        If visibleLength <= firstLength Then
            DrawRoundLine(start, Vector2.Lerp(start, joint, If(firstLength > 0, visibleLength / firstLength, 0.0F)), color, width)
        Else
            Dim fraction = If(lastLength > 0, (visibleLength - firstLength) / lastLength, 0.0F)
            DrawJoinedRoundLines(start, joint, Vector2.Lerp(joint, finish, fraction), color, width)
        End If
    End Sub

    Private Sub DrawLineCore(start As Vector2, finish As Vector2, color As Drawing.Color, width As Single, roundCaps As Boolean)
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        If width <= 0 OrElse color.A = 0 Then Return
        Dim offset As New System.Numerics.Vector2(origin.X, origin.Y)
        presenter.RenderLine(start + offset, finish + offset, width, ToColor(color), roundCaps)
    End Sub

    Public Sub FillEllipse(bounds As RectangleF, color As Drawing.Color)
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        If color.A > 0 Then presenter.RenderEllipse(Map(bounds), ToColor(color), 0)
    End Sub

    Public Sub StrokeEllipse(bounds As RectangleF, color As Drawing.Color, width As Single)
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        If color.A = 0 OrElse width <= 0 Then Return
        presenter.RenderEllipse(Map(bounds), ToColor(color), width)
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
        Return New ClipScope(Me, Rectangle.Round(Map(bounds)))
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
        Private target As D3D12_PaintContext
        Private ReadOnly previous As Rectangle
        Private ReadOnly previousBackdrop As Rectangle
        Friend Sub New(context As D3D12_PaintContext, bounds As Rectangle)
            target = context
            previous = target.presenter.FrameClip
            previousBackdrop = target.backdropClip
            target.presenter.FrameClip = Rectangle.Intersect(previous, bounds)
            target.backdropClip = Rectangle.Intersect(previousBackdrop, bounds)
        End Sub
        Public Sub Dispose() Implements IDisposable.Dispose
            If target Is Nothing Then Return
            target.presenter.FrameClip = previous
            target.backdropClip = previousBackdrop
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

    Friend Sub DrawCubicImage(image As Image, destination As RectangleF)
        If presenter Is Nothing Then Throw New InvalidOperationException("Frame is closed.")
        If image Is Nothing Then Return
        Dim bounds = Map(destination)
        If bounds.Width <= 0 OrElse bounds.Height <= 0 OrElse Not bounds.IntersectsWith(presenter.FrameClip) Then Return
        Dim width = image.Width
        Dim height = image.Height
        Dim resize = width <> bounds.Width * presenter.SampleScale OrElse height <> bounds.Height * presenter.SampleScale
        presenter.DrawLinearTile(width, height, Function() DecodeImage(image), bounds, Nothing, False, Nothing,
                                 Vortice.DXGI.Format.B8G8R8A8_UNorm, cubicSampling:=resize)
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

    Friend Sub FillCoverageMask(bounds As Rectangle, color As Drawing.Color, key As Object, rasterize As Func(Of Integer, Byte()))
        If color.A = 0 OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        bounds.Offset(origin)
        If Not bounds.IntersectsWith(presenter.FrameClip) Then Return
        Dim scale = presenter.SampleScale
        Dim ink = ToColor(color)
        presenter.DrawGlyphBatch(key, ink,
                                 Sub() presenter.DrawGlyphCoverage(bounds.Width * scale, bounds.Height * scale, rasterize(scale), bounds, ink),
                                 bounds.Location)
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
        Dim owner = presenter.OwnerControl
        If clip.Width <= 0 OrElse clip.Height <= 0 Then clip = owner.ClientRectangle
        clip.Offset(origin)
        clip = Rectangle.Intersect(clip, backdropClip)
        If clip.Width <= 0 OrElse clip.Height <= 0 Then Return False

        ' A native-transparent panel contributes no pixels. Walk out to the first surface
        ' just as the automatic path does, but keep watching the original source's topology.
        Dim resolved = source
        Dim forwarders As HashSet(Of Control) = Nothing
        While resolved IsNot Nothing AndAlso Not resolved.IsDisposed AndAlso resolved IsNot owner AndAlso
              TryCast(resolved, D3D_INativeTransparentPresentationSource)?.UseNativeTransparentPresentation = True
            If forwarders Is Nothing Then forwarders = New HashSet(Of Control)()
            If Not forwarders.Add(resolved) Then
                resolved = Nothing
                Exit While
            End If
            WatchSource(resolved)
            Dim nextSource As Control = Nothing
            Dim provider = TryCast(resolved, D3D_IBackgroundSourceProvider)
            If provider IsNot Nothing AndAlso provider.TryGetBackgroundSource(nextSource) AndAlso nextSource IsNot Nothing AndAlso Not nextSource.IsDisposed Then
                resolved = nextSource
            Else
                resolved = resolved.Parent
                While resolved IsNot Nothing AndAlso Not D3D12_BackgroundDependencies.IsSurfaceSource(resolved)
                    Dim transparent = TryCast(resolved, D3D_INativeTransparentPresentationSource)
                    If transparent IsNot Nothing AndAlso Not resolved.IsDisposed Then Exit While
                    resolved = resolved.Parent
                End While
            End If
        End While
        Dim covered = DrawBackdrop(resolved, clip, explicitSource:=True)
        If Not covered.IsEmpty Then Return True
        Return DrawInherited(clip, owner.Parent, resolved, fillOwnerColor:=True)
    End Function

    Friend Sub DrawInheritedBackdrop()
        Dim owner = presenter.OwnerControl
        DrawInherited(backdropClip, owner.Parent, Nothing, fillOwnerColor:=False)
    End Sub

    Private Function DrawInherited(clip As Rectangle, ancestor As Control, skip As Control, fillOwnerColor As Boolean) As Boolean
        While ancestor IsNot Nothing
            If ancestor IsNot skip Then
                Dim covered = DrawBackdrop(ancestor, clip, explicitSource:=False)
                If Not covered.IsEmpty Then
                    DrawUncovered(clip, covered, ancestor.Parent, skip, fillOwnerColor)
                    Return True
                End If
            End If
            ancestor = ancestor.Parent
        End While
        If Not fillOwnerColor OrElse presenter.OwnerControl.BackColor.A = 0 Then Return False
        clip.Offset(-origin.X, -origin.Y)
        FillRectangle(clip, presenter.OwnerControl.BackColor)
        Return True
    End Function

    Private Function DrawUncovered(clip As Rectangle, covered As Rectangle, ancestor As Control, skip As Control, fillOwnerColor As Boolean) As Boolean
        If covered.IsEmpty Then Return DrawInherited(clip, ancestor, skip, fillOwnerColor)
        If clip = covered Then Return False
        ' Fill only the remainder, not the area already composited with the explicit surface.
        DrawInheritedPart(Rectangle.FromLTRB(clip.Left, clip.Top, clip.Right, covered.Top), ancestor, skip, fillOwnerColor)
        DrawInheritedPart(Rectangle.FromLTRB(clip.Left, covered.Bottom, clip.Right, clip.Bottom), ancestor, skip, fillOwnerColor)
        DrawInheritedPart(Rectangle.FromLTRB(clip.Left, covered.Top, covered.Left, covered.Bottom), ancestor, skip, fillOwnerColor)
        DrawInheritedPart(Rectangle.FromLTRB(covered.Right, covered.Top, clip.Right, covered.Bottom), ancestor, skip, fillOwnerColor)
        Return True
    End Function

    Private Sub DrawInheritedPart(clip As Rectangle, ancestor As Control, skip As Control, fillOwnerColor As Boolean)
        If clip.Width > 0 AndAlso clip.Height > 0 Then DrawInherited(clip, ancestor, skip, fillOwnerColor)
    End Sub

    Private Sub WatchSource(source As Control)
        D3D12_BackgroundDependencies.Sample(presenter.OwnerControl, source, backgroundFrame, Rectangle.Empty, Nothing, completeFrame)
    End Sub

    Private Function DrawBackdrop(source As Control, clip As Rectangle, explicitSource As Boolean) As Rectangle
        Dim owner = presenter.OwnerControl
        If source Is Nothing OrElse source Is owner OrElse source.IsDisposed Then Return Rectangle.Empty
        If Not D3D12_BackgroundDependencies.IsSurfaceSource(source) Then
            If TypeOf source Is D3D_INativeTransparentPresentationSource Then WatchSource(source)
            Return Rectangle.Empty
        End If
        If Not D3D12_BackgroundDependencies.Accept(owner, source) Then Return Rectangle.Empty
        If Not source.IsHandleCreated Then
            WatchSource(source)
            Return Rectangle.Empty
        End If
        Dim offset = owner.PointToClient(source.PointToScreen(Point.Empty))
        Dim mapped = Rectangle.Intersect(clip, New Rectangle(offset, source.ClientSize))
        Dim form = TryCast(source, Form)
        If form IsNot Nothing AndAlso Not explicitSource AndAlso TypeOf owner Is ThisIsYourWindow.ChromeOverlayControl Then Return Rectangle.Empty
        If mapped.Width <= 0 OrElse mapped.Height <= 0 Then
            WatchSource(source)
            Return Rectangle.Empty
        End If
        Dim region = mapped
        region.Offset(-offset.X, -offset.Y)
        D3D12_BackgroundDependencies.Sample(owner, source, backgroundFrame, region, offset, completeFrame)

        Dim sourcePresenter = D3D12StartupDispatch.FindPresenter(source)
        Dim caption = form IsNot Nothing AndAlso ThisIsYourWindow.UsesCaptionBackdrop12(form, owner)
        If form IsNot Nothing AndAlso (caption OrElse Not D3D_V5Presentation.IsV5Control(form) OrElse Not TypeOf form Is ID3D12Renderable) Then
            DrawExplicitFallback(source, clip, mapped, explicitSource)
            If mapped.IntersectsWith(presenter.FrameClip) Then
                Dim local = mapped
                local.Offset(-origin.X, -origin.Y)
                Using PushClip(local)
                    If explicitSource OrElse caption Then
                        ThisIsYourWindow.DrawFormSurface12(Me, form, New Point(offset.X - origin.X, offset.Y - origin.Y))
                    ElseIf Not ThisIsYourWindow.DrawFormBackdrop12(Me, form, owner, New Point(offset.X - origin.X, offset.Y - origin.Y)) Then
                        FillRectangle(local, form.BackColor)
                    End If
                End Using
            End If
            Return mapped
        End If
        If sourcePresenter?.RetainedContent Is Nothing OrElse Not sourcePresenter.PendingDamage.IsEmpty OrElse Not sourcePresenter.MatchesOwner() Then
            ' The dependency is recorded before requesting this producer. A producer
            ' in another branch can finish later in the current or next batch.
            D3D_V5Presentation.RequestRenderBatched(source, markSurfaceDirty:=False)
        End If
        If sourcePresenter?.RetainedContent Is Nothing OrElse sourcePresenter.BufferSize.Width <> source.ClientSize.Width OrElse
           sourcePresenter.BufferSize.Height <> source.ClientSize.Height Then
            Return Rectangle.Empty
        End If
        DrawExplicitFallback(source, clip, mapped, explicitSource)
        If mapped.IntersectsWith(presenter.FrameClip) Then
            presenter.DrawSource(sourcePresenter, New Rectangle(offset, source.ClientSize), mapped)
        End If
        Return mapped
    End Function

    Private Sub DrawExplicitFallback(source As Control, clip As Rectangle, mapped As Rectangle, explicitSource As Boolean)
        If Not explicitSource OrElse mapped = clip Then Return
        ' Lay down the fallback first so premultiplied pixels on the explicit surface
        ' also blend correctly where a partially overlapping source is translucent.
        DrawInherited(clip, presenter.OwnerControl.Parent, source, fillOwnerColor:=True)
    End Sub

    Friend Sub EndFrame(success As Boolean)
        If success Then D3D12_BackgroundDependencies.EndFrame(presenter.OwnerControl, backgroundFrame, completeFrame)
        presenter = Nothing
    End Sub
End Class

Friend Module D3D12StartupDispatch
    Private group As D3D12_DeviceManager
    Private ownerThread As Integer
    Private recovering As Boolean
    Private ReadOnly presenters As New ConditionalWeakTable(Of Control, D3D12_CompositionPresenter)()

    Private ReadOnly atlases As New Dictionary(Of Control, D3D12_WindowAtlas)()
    Private ReadOnly parked As New LinkedList(Of D3D12_CompositionPresenter)()
    Private Const ParkedBudget As Long = 48L * 1024 * 1024
    Private parkedBytes As Long
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

    Friend Function NeedsPresentation(owner As Control) As Boolean
        If RenderBackendStartup.IsDesignerPreview Then Return True
        Dim presenter = FindPresenter(owner)
        Return presenter Is Nothing OrElse Not presenter.PendingDamage.IsEmpty OrElse
            Not presenter.HasPublishedFrame OrElse Not presenter.MatchesOwner()
    End Function

    Friend Sub RequestPresentation(owner As Control)
        If NeedsPresentation(owner) Then D3D_V5Presentation.RequestRenderBatched(owner, markSurfaceDirty:=False)
    End Sub

    Friend Sub MarkDirty(owner As Control, dirty As Rectangle)
        Dim presenter = FindPresenter(owner)
        If presenter Is Nothing Then Return
        If dirty.Width <= 0 OrElse dirty.Height <= 0 Then dirty = owner.ClientRectangle
        presenter.PendingDamage = If(presenter.PendingDamage.IsEmpty, dirty, Rectangle.Union(presenter.PendingDamage, dirty))
    End Sub

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

    Friend Sub Render(owner As Control)
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
            Unpark(presenter)
            presenter.Resize()
            Dim visible = D3D_ControlTreeWalker.IsEffectivelyVisible(owner)
            If visible AndAlso presenter.Atlas Is Nothing Then AttachAtlas(presenter)
            Dim chromeKey = TryCast(owner, ThisIsYourWindow.ChromeOverlayControl)?.ChromeCacheKey()
            Dim damage = presenter.PendingDamage
            presenter.PendingDamage = Rectangle.Empty
            If Not designerPreview AndAlso presenter.RetainedContent IsNot Nothing AndAlso
               (damage.IsEmpty OrElse (chromeKey IsNot Nothing AndAlso Equals(chromeKey, presenter.ChromeKey))) Then
                If visible AndAlso Not presenter.HasPublishedFrame Then presenter.StageRetainedContent()
            Else
                Dim coverage = TryCast(owner, D3D_IGpuDirtyRegionCoverage)
                If designerPreview OrElse coverage Is Nothing OrElse Not coverage.CoversDirtyRegion(damage) Then damage = Rectangle.Empty
                Try
                    presenter.RenderFrame(Sub(context)
                        Dim opacity = TryCast(owner, D3D_IBackgroundOpacitySource)
                        Dim provider = TryCast(owner, D3D_IBackgroundSourceProvider)
                        Dim explicitSource As Control = Nothing
                        If Not If(opacity?.IsBackgroundOpaque, False) AndAlso
                           Not (provider IsNot Nothing AndAlso provider.TryGetBackgroundSource(explicitSource) AndAlso explicitSource IsNot Nothing) Then
                            context.DrawInheritedBackdrop()
                        End If
                        source.RenderGpu12(context)
                        If designerPreview Then D3D_PaintBridge.绘制设计时选择装饰(context, owner)
                    End Sub, damage)
                Catch
                    MarkDirty(owner, Rectangle.Empty)
                    Throw
                End Try
                presenter.ChromeKey = chromeKey
            End If
            If Not visible Then
                Dim retirements As New List(Of Action)()
                ParkPresenter(presenter, retirements)
                RetireFrameResources(retirements)
                TrimParkedSurfaces()
            ElseIf batchDepth = 0 Then
                presenter.Atlas.Flush()
            End If
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
            ReleasePresenter(pair.Key, notifyConsumers:=False)
        Next
        For Each atlas In atlases.Values.ToArray()
            atlas.Dispose()
        Next
        atlases.Clear()
    End Sub

    Private Function Attach(owner As Control) As D3D12_CompositionPresenter
        Dim presenter As New D3D12_CompositionPresenter(owner, group)
        AddHandler owner.Invalidated, AddressOf InvalidatePaint
        AddHandler owner.VisibleChanged, AddressOf VisibilityChanged
        AddHandler owner.HandleDestroyed, AddressOf HandleDestroyed
        Return presenter
    End Function

    Private Sub AttachAtlas(presenter As D3D12_CompositionPresenter)
        Dim root = presenter.OwnerControl
        While root.Parent IsNot Nothing
            root = root.Parent
        End While
        Dim atlas As D3D12_WindowAtlas = Nothing
        If Not atlases.TryGetValue(root, atlas) Then
            atlas = New D3D12_WindowAtlas(group)
            atlases.Add(root, atlas)
        End If
        atlas.Attach(presenter)
        presenter.Atlas = atlas
    End Sub

    Private Sub DetachAtlas(presenter As D3D12_CompositionPresenter)
        Dim atlas = presenter.Atlas
        If atlas Is Nothing Then Return
        presenter.Atlas = Nothing
        atlas.Detach(presenter)
        If atlas.Count = 0 Then
            Dim root = atlases.First(Function(item) item.Value Is atlas).Key
            atlases.Remove(root)
            atlas.Dispose()
        End If
    End Sub

    Private Sub InvalidatePaint(sender As Object, e As InvalidateEventArgs)
        Dim owner = DirectCast(sender, Control)
        MarkDirty(owner, e.InvalidRect)
        If Not owner.Visible AndAlso D3D12_BackgroundDependencies.HasVisibleConsumers(owner) Then RequestPresentation(owner)
    End Sub

    Private Sub VisibilityChanged(sender As Object, e As EventArgs)
        Dim owner = DirectCast(sender, Control)
        If owner.Visible AndAlso owner.IsHandleCreated AndAlso Not owner.IsDisposed Then RequestPresentation(owner)
    End Sub

    Friend Sub ReleaseHiddenSubtreeResources(root As Control, keepPage As Boolean)
        If root Is Nothing OrElse group Is Nothing OrElse D3D_V5Presentation.IsRendering Then Return
        Dim retirements As New List(Of Action)()
        For Each pair In presenters.ToArray()
            Dim owner = pair.Key
            If owner.Visible OrElse owner.IsDisposed OrElse Not D3D_ControlTreeWalker.IsDescendantOrSelf(owner, root) Then Continue For
            Dim presenter = pair.Value
            If presenter.RetainedContent IsNot Nothing AndAlso (keepPage OrElse D3D12_BackgroundDependencies.HasVisibleConsumers(owner)) Then
                ParkPresenter(presenter, retirements)
            Else
                ReleasePresenter(owner)
            End If
        Next
        RetireFrameResources(retirements)
        TrimParkedSurfaces()
    End Sub

    Private Sub ParkPresenter(presenter As D3D12_CompositionPresenter, retirements As List(Of Action))
        DetachAtlas(presenter)
        presenter.Park(retirements)
        If presenter.ParkedNode IsNot Nothing OrElse presenter.RetainedContent Is Nothing Then Return
        Dim allocation = group.Device.GetResourceAllocationInfo(0UI, presenter.RetainedContent.Description)
        presenter.ParkedBytes = CLng(allocation.SizeInBytes)
        presenter.ParkedNode = parked.AddLast(presenter)
        parkedBytes += presenter.ParkedBytes
    End Sub

    Private Sub RetireFrameResources(retirements As List(Of Action))
        If retirements.Count = 0 Then Return
        group.RetireAfterGpu(Sub()
                                 For Each release In retirements
                                     release()
                                 Next
                             End Sub)
    End Sub

    Friend Sub TrimParkedSurfaces()
        ' A hidden surface with a live consumer is still part of the display working set.
        Dim limit = Math.Min(ParkedBudget, GlobalOptions.GpuCacheBudgetBytes \ 4)
        Dim node = parked.First
        While node IsNot Nothing AndAlso parkedBytes > limit
            Dim following = node.Next
            Dim owner = node.Value.OwnerControl
            If Not D3D12_BackgroundDependencies.HasVisibleConsumers(owner) Then ReleasePresenter(owner)
            node = following
        End While
    End Sub

    Private Sub Unpark(presenter As D3D12_CompositionPresenter)
        If presenter.ParkedNode Is Nothing Then Return
        parked.Remove(presenter.ParkedNode)
        parkedBytes -= presenter.ParkedBytes
        presenter.ParkedNode = Nothing
        presenter.ParkedBytes = 0
    End Sub

    Friend Sub SuspendPresentation(owner As Control)
        ReleasePresenter(owner)
    End Sub

    Private Sub HandleDestroyed(sender As Object, e As EventArgs)
        ReleasePresenter(DirectCast(sender, Control))
    End Sub

    Private Sub ReleasePresenter(owner As Control, Optional notifyConsumers As Boolean = True)
        Dim presenter = FindPresenter(owner)
        If presenter Is Nothing Then Return
        D3D12_BackgroundDependencies.Remove(owner)
        presenters.Remove(owner)
        RemoveHandler owner.Invalidated, AddressOf InvalidatePaint
        RemoveHandler owner.VisibleChanged, AddressOf VisibilityChanged
        RemoveHandler owner.HandleDestroyed, AddressOf HandleDestroyed
        Unpark(presenter)
        DetachAtlas(presenter)
        presenter.Dispose()
        If notifyConsumers Then D3D12_BackgroundDependencies.SourceChanged(owner)
    End Sub
End Module
