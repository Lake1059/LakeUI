Imports System.Numerics
Imports Vortice.Direct2D1
Imports Vortice.DXGI

Friend NotInheritable Class D3D_BitmapColorTransfer
    Implements IDisposable

    Private Shared ReadOnly _decodeTable As Single() = CreateTable(False)
    Private Shared ReadOnly _encodeTable As Single() = CreateTable(True)
    Private ReadOnly _context As ID2D1DeviceContext
    Private _decode As Effects.TableTransfer
    Private _encode As Effects.TableTransfer

    Friend Sub New(context As ID2D1DeviceContext)
        _context = context
    End Sub

    Friend Sub Draw(bitmap As ID2D1Bitmap1, destination As Vortice.RawRectF, source As Vortice.RawRectF,
                    interpolation As InterpolationMode, linearOutput As Boolean)
        Dim linearInput = bitmap.PixelFormat.Format = Format.R16G16B16A16_Float
        If linearInput = linearOutput Then
            _context.DrawBitmap(bitmap, destination, 1.0F, interpolation, source, New Nullable(Of Matrix4x4)())
            Return
        End If
        Dim effect As Effects.TableTransfer
        If linearOutput Then
            If _decode Is Nothing Then _decode = CreateEffect(_decodeTable)
            effect = _decode
        Else
            If _encode Is Nothing Then _encode = CreateEffect(_encodeTable)
            effect = _encode
        End If
        Dim originalTransform = _context.Transform
        effect.SetInput(0UI, bitmap, True)
        Try
            Dim scale = Matrix3x2.CreateScale((destination.Right - destination.Left) / (source.Right - source.Left),
                                              (destination.Bottom - destination.Top) / (source.Bottom - source.Top))
            _context.Transform = scale * Matrix3x2.CreateTranslation(destination.Left, destination.Top) * originalTransform
            Using output = effect.Output
                _context.DrawImage(output, New Nullable(Of Vector2)(Vector2.Zero), New Nullable(Of Vortice.RawRectF)(source),
                                   interpolation, CompositeMode.SourceOver)
            End Using
        Finally
            _context.Transform = originalTransform
            effect.SetInput(0UI, Nothing, True)
        End Try
    End Sub

    Private Function CreateEffect(table As Single()) As Effects.TableTransfer
        Dim effect = New Effects.TableTransfer(_context)
        Try
            effect.RedTable = table
            effect.GreenTable = table
            effect.BlueTable = table
            effect.AlphaDisable = True
            effect.ClampOutput = True
            effect.SetValue(Of BufferPrecision)(CUInt(Vortice.Direct2D1.Property.Precision), BufferPrecision.PerChannel16Float)
            Return effect
        Catch
            effect.Dispose()
            Throw
        End Try
    End Function

    Private Shared Function CreateTable(encode As Boolean) As Single()
        Dim table(1023) As Single
        For index As Integer = 0 To table.Length - 1
            Dim value = CDbl(index) / (table.Length - 1)
            table(index) = CSng(If(encode,
                If(value <= 0.0031308, value * 12.92, 1.055 * Math.Pow(value, 1.0 / 2.4) - 0.055),
                If(value <= 0.04045, value / 12.92, Math.Pow((value + 0.055) / 1.055, 2.4))))
        Next
        Return table
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        _decode?.Dispose()
        _encode?.Dispose()
        _decode = Nothing
        _encode = Nothing
    End Sub
End Class
