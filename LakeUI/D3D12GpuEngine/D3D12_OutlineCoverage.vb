Imports System.Buffers
Imports System.Numerics
Imports SharpGen.Runtime
Imports Vortice.Direct2D1
Imports Vortice.DirectWrite

' Only CPU geometry services are used here. No Direct2D render target or device is created.
Friend NotInheritable Class D3D12_OutlineCoverage
    Inherits CallbackBase
    Implements ID2D1SimplifiedGeometrySink

    Private Shared ReadOnly geometryFactory As New Threading.ThreadLocal(Of ID2D1Factory)(Function() D2D1.D2D1CreateFactory(Of ID2D1Factory)())
    Private ReadOnly bounds As Rectangle
    Private ReadOnly crossings As List(Of (X As Single, Delta As Integer))()
    Private ReadOnly crossingDeltas As Integer()
    Private ReadOnly occupiedBins As ULong()
    Private ReadOnly stride As Integer
    Private ReadOnly wordsPerRow As Integer
    Private winding As Boolean
    Private filled As Boolean
    Private first, previous As Vector2

    Private Sub New(bounds As Rectangle)
        Me.bounds = bounds
        stride = bounds.Width * 4 + 1
        Dim sampleCount = CLng(stride) * bounds.Height * 4
        If sampleCount <= 512 * 1024 Then
            wordsPerRow = (stride + 63) >> 6
            occupiedBins = New ULong(wordsPerRow * bounds.Height * 4 - 1) {}
            crossingDeltas = ArrayPool(Of Integer).Shared.Rent(CInt(sampleCount))
        Else
            crossings = New List(Of (Single, Integer))(bounds.Height * 4 - 1) {}
        End If
    End Sub

    Friend Shared Function Rasterize(run As GlyphRun, x As Single, y As Single, transform As Matrix3x2?,
                                     ByRef box As Rectangle) As Byte()
        Using path = geometryFactory.Value.CreatePathGeometry(), sink = path.Open()
            run.FontFace.GetGlyphRunOutline(run.FontEmSize, run.Indices, run.Advances, run.Offsets, run.IsSideways, (run.BidiLevel And 1) <> 0, sink)
            sink.Close()
            Dim matrix = Matrix3x2.CreateTranslation(x, y) * If(transform, Matrix3x2.Identity)
            Dim b = path.GetBounds(matrix)
            box = Rectangle.FromLTRB(CInt(Math.Floor(b.Left)), CInt(Math.Floor(b.Top)), CInt(Math.Ceiling(b.Right)), CInt(Math.Ceiling(b.Bottom)))
            If box.Width <= 0 OrElse box.Height <= 0 Then Return Nothing
            ' Keep the same D3D12 texture dimension limits as the grayscale path.
            If box.Width > 16384 OrElse box.Height > 16384 OrElse CLng(box.Width) * box.Height > 16 * 1024 * 1024 Then Return Nothing
            Using raster As New D3D12_OutlineCoverage(box)
                Try
                    path.Simplify(GeometrySimplificationOption.Lines, matrix, 0.125F, raster)
                    Return raster.MakeCoverage()
                Finally
                    If raster.crossingDeltas IsNot Nothing Then ArrayPool(Of Integer).Shared.Return(raster.crossingDeltas)
                End Try
            End Using
        End Using
    End Function

    Private Sub SetFillMode(mode As FillMode) Implements ID2D1SimplifiedGeometrySink.SetFillMode
        winding = mode = FillMode.Winding
    End Sub

    Private Sub SetSegmentFlags(flags As PathSegment) Implements ID2D1SimplifiedGeometrySink.SetSegmentFlags
    End Sub

    Private Sub BeginFigure(start As Vector2, kind As FigureBegin) Implements ID2D1SimplifiedGeometrySink.BeginFigure
        filled = kind = FigureBegin.Filled
        first = start
        previous = start
    End Sub

    Private Sub AddLines(points As Vector2()) Implements ID2D1SimplifiedGeometrySink.AddLines
        If Not filled Then Return
        For Each point In points
            AddEdge(previous, point)
            previous = point
        Next
    End Sub

    Private Sub AddBeziers(segments As BezierSegment()) Implements ID2D1SimplifiedGeometrySink.AddBeziers
        Throw New InvalidOperationException("Expected a flattened outline.")
    End Sub

    Private Sub EndFigure(kind As FigureEnd) Implements ID2D1SimplifiedGeometrySink.EndFigure
        If filled Then AddEdge(previous, first)
    End Sub

    Private Sub Close() Implements ID2D1SimplifiedGeometrySink.Close
    End Sub

    Private Sub AddEdge(a As Vector2, b As Vector2)
        Dim dy = b.Y - a.Y
        If dy = 0 Then Return
        Dim direction = If(dy > 0, 1, -1)
        Dim low = Math.Max(0, CInt(Math.Ceiling((Math.Min(a.Y, b.Y) - bounds.Top) * 4.0F - 0.5F)))
        Dim high = Math.Min(bounds.Height * 4, CInt(Math.Ceiling((Math.Max(a.Y, b.Y) - bounds.Top) * 4.0F - 0.5F)))
        Dim slope = (b.X - a.X) / dy
        For row = low To high - 1
            Dim y = bounds.Top + (row + 0.5F) / 4.0F
            Dim crossingX = a.X + (y - a.Y) * slope
            If crossingDeltas IsNot Nothing Then
                Dim x = Math.Clamp(CInt(Math.Ceiling((crossingX - bounds.Left) * 4.0F - 0.5F)), 0, stride - 1)
                Dim bin = row * stride + x
                Dim wordIndex = row * wordsPerRow + (x >> 6)
                Dim bit = 1UL << (x And 63)
                Dim occupied = occupiedBins(wordIndex)
                If (occupied And bit) = 0 Then
                    ' Only initialize touched bins in the rented array.
                    crossingDeltas(bin) = direction
                    occupiedBins(wordIndex) = occupied Or bit
                Else
                    crossingDeltas(bin) += direction
                End If
            Else
                Dim values = crossings(row)
                If values Is Nothing Then
                    values = New List(Of (Single, Integer))()
                    crossings(row) = values
                End If
                values.Add((crossingX, direction))
            End If
        Next
    End Sub

    Private Function MakeCoverage() As Byte()
        Dim alpha(bounds.Width * bounds.Height - 1) As Byte
        If crossingDeltas IsNot Nothing Then
            FillBinnedCoverage(alpha)
        Else
            FillSortedCoverage(alpha)
        End If
        For index = 0 To alpha.Length - 1
            alpha(index) = CByte((CInt(alpha(index)) * 255 + 8) \ 16)
        Next
        Return alpha
    End Function

    Private Sub FillBinnedCoverage(alpha As Byte())
        Dim fillMask = If(winding, -1, 1)
        For row = 0 To bounds.Height * 4 - 1
            Dim source = row * stride
            Dim dest = (row >> 2) * bounds.Width
            Dim wordOffset = row * wordsPerRow
            Dim inside = 0
            Dim start = 0
            ' Only visit quantized crossings. The fill state is constant between them.
            For word = 0 To wordsPerRow - 1
                Dim pending = occupiedBins(wordOffset + word)
                While pending <> 0UL
                    Dim x = (word << 6) + BitOperations.TrailingZeroCount(pending)
                    pending = pending And (pending - 1UL)
                    Dim delta = crossingDeltas(source + x)
                    If delta = 0 Then Continue While
                    Dim wasInside = (inside And fillMask) <> 0
                    inside += delta
                    If wasInside = ((inside And fillMask) <> 0) Then Continue While
                    If wasInside Then
                        AddSpan(alpha, dest, start, x)
                    Else
                        start = x
                    End If
                End While
            Next
            If (inside And fillMask) <> 0 Then AddSpan(alpha, dest, start, stride - 1)
        Next
    End Sub

    Private Sub FillSortedCoverage(alpha As Byte())
        For row = 0 To crossings.Length - 1
            Dim values = crossings(row)
            If values Is Nothing Then Continue For
            values.Sort(Function(left, right) left.X.CompareTo(right.X))
            Dim inside As Integer = 0
            Dim start As Single = 0
            Dim offset = (row >> 2) * bounds.Width
            For Each crossing In values
                If inside = 0 Then start = crossing.X
                inside = If(winding, inside + crossing.Delta, inside Xor 1)
                If inside <> 0 Then Continue For
                Dim left = Math.Max(0, CInt(Math.Ceiling((start - bounds.Left) * 4.0F - 0.5F)))
                Dim right = Math.Min(bounds.Width * 4, CInt(Math.Ceiling((crossing.X - bounds.Left) * 4.0F - 0.5F)))
                AddSpan(alpha, offset, left, right)
            Next
        Next
    End Sub

    Private Shared Sub AddSpan(alpha As Byte(), offset As Integer, left As Integer, right As Integer)
        If left >= right Then Return
        Dim firstPixel = left >> 2
        Dim lastPixel = (right - 1) >> 2
        If firstPixel = lastPixel Then
            alpha(offset + firstPixel) += CByte(right - left)
        Else
            alpha(offset + firstPixel) += CByte(4 - (left And 3))
            For pixel = firstPixel + 1 To lastPixel - 1
                alpha(offset + pixel) += 4
            Next
            alpha(offset + lastPixel) += CByte(((right - 1) And 3) + 1)
        End If
    End Sub
End Class
