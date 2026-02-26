''' <summary>
''' 文本渲染相关的共享辅助方法。
''' </summary>
Friend Module TextRenderHelper

    ''' <summary>
    ''' 测量文本渲染宽度。
    ''' </summary>
    Friend Function MeasureTextWidth(text As String, font As Font, lineHeight As Integer) As Integer
        If String.IsNullOrEmpty(text) Then Return 0
        Return TextRenderer.MeasureText(text, font, New Size(32767, lineHeight),
            TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine).Width
    End Function

    ''' <summary>
    ''' 根据 X 坐标查找最近的字符列索引。
    ''' </summary>
    Friend Function FindColFromX(lineStr As String, x As Integer, font As Font, lineHeight As Integer) As Integer
        If String.IsNullOrEmpty(lineStr) OrElse x <= 0 Then Return 0
        Dim best As Integer = 0
        Dim bestDist As Integer = Integer.MaxValue
        For i As Integer = 0 To lineStr.Length
            Dim cx As Integer = MeasureTextWidth(lineStr.Substring(0, i), font, lineHeight)
            Dim dist As Integer = Math.Abs(x - cx)
            If dist < bestDist Then
                bestDist = dist
                best = i
            ElseIf dist > bestDist Then
                Exit For
            End If
        Next
        Return best
    End Function

End Module
