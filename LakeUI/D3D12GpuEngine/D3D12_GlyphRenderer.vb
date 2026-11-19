Imports Vortice.Mathematics
Imports Vortice.DirectWrite
Imports Vortice.DCommon
Imports SharpGen.Runtime

' DirectWrite selects and rasterizes glyphs. The resulting coverage is blended by the DX12 image PSO.
Friend NotInheritable Class D3D12_GlyphRenderer
    Inherits TextRendererBase

    Private ReadOnly target As D3D12_PaintContext
    Private ReadOnly foreground As Color4
    Private Shared ReadOnly analysisFactory As New Lazy(Of IDWriteFactory2)(
        Function() D3D_D2DInterop.GetDWriteFactory().QueryInterface(Of IDWriteFactory2)())
    Private ReadOnly transform As System.Numerics.Matrix3x2?
    Private ReadOnly scale As Integer
    Private ReadOnly subpixel As Boolean

    Friend Sub New(target As D3D12_PaintContext, color As Color4, transform As System.Numerics.Matrix3x2?, scale As Integer, Optional subpixel As Boolean = False)
        Me.target = target
        foreground = color
        Me.scale = scale
        Me.subpixel = subpixel
        If scale > 1 Then
            Me.transform = If(transform, System.Numerics.Matrix3x2.Identity) * System.Numerics.Matrix3x2.CreateScale(scale)
        Else
            Me.transform = transform
        End If
    End Sub

    Public Overrides Sub DrawGlyphRun(context As IntPtr, baselineX As Single, baselineY As Single, measuring As MeasuringMode,
                                      run As GlyphRun, description As GlyphRunDescription, effect As IUnknown)
        Dim mode = GlobalOptions.GlobalTextQuality
        If mode = GlobalOptions.TextQualityMode.Outline AndAlso GlobalOptions.OutlineText.RenderingMode = RenderingMode.Outline Then
            Dim bounds As Rectangle
            Dim coverage = D3D12_OutlineCoverage.Rasterize(run, baselineX, baselineY, transform, bounds)
            If coverage IsNot Nothing Then DrawCoverage(coverage, bounds)
            Return
        End If
        Dim rasterMode = If(mode = GlobalOptions.TextQualityMode.Aliased, RenderingMode.Aliased, RenderingMode.NaturalSymmetric)
        Dim gridFit = GridFitMode.Default
        If mode = GlobalOptions.TextQualityMode.Outline Then
            rasterMode = GlobalOptions.OutlineText.RenderingMode
            gridFit = GlobalOptions.OutlineText.GridFitMode
        End If
        Using analysis = analysisFactory.Value.CreateGlyphRunAnalysis(run, transform, rasterMode, measuring,
                                                        gridFit, If(subpixel, TextAntialiasMode.ClearType, TextAntialiasMode.Grayscale), baselineX, baselineY)
            Dim texture = If(subpixel, TextureType.Cleartype3x1, TextureType.Aliased1x1)
            Dim box = analysis.GetAlphaTextureBounds(texture)
            Dim width = box.Right - box.Left
            Dim height = box.Bottom - box.Top
            If width <= 0 OrElse height <= 0 Then Return
            Dim coverage(width * height * If(subpixel, 3, 1) - 1) As Byte
            analysis.CreateAlphaTexture(texture, box, coverage, CUInt(coverage.Length))
            If subpixel Then
                ' DirectWrite delivers one RGB coverage triplet per destination pixel.
                Dim bgra(width * height * 4 - 1) As Byte
                For pixel = 0 To width * height - 1
                    Dim src = pixel * 3
                    Dim dst = pixel * 4
                    bgra(dst) = coverage(src + 2)
                    bgra(dst + 1) = coverage(src + 1)
                    bgra(dst + 2) = coverage(src)
                Next
                target.DrawGlyphCoverage(width, height, bgra, New RectangleF(box.Left, box.Top, width, height), foreground, subpixel:=True)
            Else
                DrawCoverage(coverage, New Rectangle(box.Left, box.Top, width, height))
            End If
        End Using
    End Sub

    Private Sub DrawCoverage(coverage As Byte(), bounds As Rectangle)
        target.DrawGlyphCoverage(bounds.Width, bounds.Height, coverage,
                                 New RectangleF(bounds.Left / CSng(scale), bounds.Top / CSng(scale), bounds.Width / CSng(scale), bounds.Height / CSng(scale)),
                                 foreground)
    End Sub
End Class
