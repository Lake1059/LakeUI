''' <summary>
''' Provides decimal-based arithmetic for user-facing numeric steps.
''' Double remains the public value type, while interaction updates avoid
''' accumulating binary floating-point rounding errors.
''' </summary>
Friend Module NumericValuePrecision
    Friend Function AddStep(value As Double, delta As Double) As Double
        Try
            Return CDbl(CDec(value) + CDec(delta))
        Catch ex As OverflowException
            Return value + delta
        End Try
    End Function

    Friend Function SnapToIncrement(value As Double, minimum As Double, increment As Double) As Double
        If increment <= 0 OrElse Double.IsNaN(value) OrElse Double.IsInfinity(value) Then Return value

        Try
            Dim decimalMinimum As Decimal = CDec(minimum)
            Dim decimalIncrement As Decimal = CDec(increment)
            Dim steps As Decimal = Decimal.Round((CDec(value) - decimalMinimum) / decimalIncrement,
                                                 0,
                                                 MidpointRounding.AwayFromZero)
            Return CDbl(decimalMinimum + steps * decimalIncrement)
        Catch ex As OverflowException
            Return Math.Round((value - minimum) / increment, MidpointRounding.AwayFromZero) * increment + minimum
        End Try
    End Function

    Friend Function RemoveStepNoise(value As Double, minimum As Double, increment As Double) As Double
        If increment <= 0 OrElse Double.IsNaN(value) OrElse Double.IsInfinity(value) Then Return value

        Dim snapped As Double = SnapToIncrement(value, minimum, increment)
        Dim incrementTolerance As Double = Math.Abs(increment) * 0.000000000001R
        Dim ulpTolerance As Double = Math.Abs(Math.BitIncrement(value) - value) * 8.0R
        If Math.Abs(value - snapped) <= Math.Max(incrementTolerance, ulpTolerance) Then Return snapped
        Return value
    End Function
End Module
