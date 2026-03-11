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
        Dim n As Integer = lineStr.Length
        Dim totalW As Integer = MeasureTextWidth(lineStr, font, lineHeight)
        If x >= totalW Then Return n
        Dim lo As Integer = 0
        Dim hi As Integer = n
        While lo < hi
            Dim mid As Integer = (lo + hi + 1) \ 2
            If MeasureTextWidth(lineStr.Substring(0, mid), font, lineHeight) <= x Then
                lo = mid
            Else
                hi = mid - 1
            End If
        End While
        If lo < n Then
            Dim wLo As Integer = MeasureTextWidth(lineStr.Substring(0, lo), font, lineHeight)
            Dim wNext As Integer = MeasureTextWidth(lineStr.Substring(0, lo + 1), font, lineHeight)
            If x - wLo > wNext - x Then
                Return lo + 1
            End If
        End If
        Return lo
    End Function

End Module
