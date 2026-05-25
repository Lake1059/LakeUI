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

#Region "DirectWrite 度量"

    ''' <summary>使用 DirectWrite 创建一个 TextFormat（调用方负责 Dispose）。</summary>
    Friend Function CreateDWriteTextFormat(font As Font, dpiScale As Single) As Vortice.DirectWrite.IDWriteTextFormat
        Dim sizePx As Single = font.SizeInPoints * (96.0F / 72.0F) * dpiScale
        Return D2DHelper.CreateTextFormat(font, sizePx)
    End Function

    ''' <summary>使用 DirectWrite 测量单行文本宽度（像素）。</summary>
    Friend Function MeasureTextWidth_D2D(text As String, font As Font, dpiScale As Single) As Single
        If String.IsNullOrEmpty(text) Then Return 0
        Using fmt = CreateDWriteTextFormat(font, dpiScale)
            fmt.WordWrapping = Vortice.DirectWrite.WordWrapping.NoWrap
            Using layout = D2DHelper.GetDWriteFactory().CreateTextLayout(text, fmt, Single.MaxValue, Single.MaxValue)
                Return layout.Metrics.WidthIncludingTrailingWhitespace
            End Using
        End Using
    End Function

    ''' <summary>使用 DirectWrite 命中测试，根据 X 坐标返回最近字符列索引（基于 TextLayout 二分测量）。</summary>
    Friend Function FindColFromX_D2D(lineStr As String, x As Single, font As Font, dpiScale As Single) As Integer
        If String.IsNullOrEmpty(lineStr) OrElse x <= 0 Then Return 0
        Dim n As Integer = lineStr.Length
        Try
            Using fmt = CreateDWriteTextFormat(font, dpiScale)
                fmt.WordWrapping = Vortice.DirectWrite.WordWrapping.NoWrap
                Using layout = D2DHelper.GetDWriteFactory().CreateTextLayout(lineStr, fmt, Single.MaxValue, Single.MaxValue)
                    If x >= layout.Metrics.WidthIncludingTrailingWhitespace Then Return n

                    Dim trailing As SharpGen.Runtime.RawBool = False
                    Dim inside As SharpGen.Runtime.RawBool = False
                    Dim hit As Vortice.DirectWrite.HitTestMetrics
                    layout.HitTestPoint(x, 0.0F, trailing, inside, hit)

                    Dim col As Integer = CInt(hit.TextPosition)
                    If CBool(trailing) Then col += Math.Max(1, CInt(hit.Length))
                    Return Math.Max(0, Math.Min(n, col))
                End Using
            End Using
        Catch
            Return FindColFromX_D2DByMeasure(lineStr, x, font, dpiScale)
        End Try
    End Function

    Private Function FindColFromX_D2DByMeasure(lineStr As String, x As Single, font As Font, dpiScale As Single) As Integer
        Dim n As Integer = lineStr.Length
        Dim totalW As Single = MeasureTextWidth_D2D(lineStr, font, dpiScale)
        If x >= totalW Then Return n
        Dim lo As Integer = 0
        Dim hi As Integer = n
        While lo < hi
            Dim mid As Integer = (lo + hi + 1) \ 2
            If MeasureTextWidth_D2D(lineStr.Substring(0, mid), font, dpiScale) <= x Then
                lo = mid
            Else
                hi = mid - 1
            End If
        End While
        If lo < n Then
            Dim wLo As Single = MeasureTextWidth_D2D(lineStr.Substring(0, lo), font, dpiScale)
            Dim wNext As Single = MeasureTextWidth_D2D(lineStr.Substring(0, lo + 1), font, dpiScale)
            If x - wLo > wNext - x Then Return lo + 1
        End If
        Return lo
    End Function

#End Region

End Module
