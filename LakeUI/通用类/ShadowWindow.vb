Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices

''' <summary>
''' 分层窗口，用于在宿主窗口后方渲染自定义深度的阴影。
''' 使用 UpdateLayeredWindow + 预乘 Alpha 位图绘制柔和阴影。
''' </summary>
Friend Class ShadowWindow
    Inherits Form

#Region "Win32"

    <DllImport("user32.dll")>
    Private Shared Function UpdateLayeredWindow(
        hwnd As IntPtr, hdcDst As IntPtr,
        ByRef pptDst As W32Point, ByRef psize As W32Size,
        hdcSrc As IntPtr, ByRef pptSrc As W32Point,
        crKey As Integer, ByRef pblend As BLENDFUNCTION,
        dwFlags As Integer) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetDC(hWnd As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function ReleaseDC(hWnd As IntPtr, hDC As IntPtr) As Integer
    End Function

    <DllImport("gdi32.dll")>
    Private Shared Function CreateCompatibleDC(hdc As IntPtr) As IntPtr
    End Function

    <DllImport("gdi32.dll")>
    Private Shared Function DeleteDC(hdc As IntPtr) As Boolean
    End Function

    <DllImport("gdi32.dll")>
    Private Shared Function SelectObject(hdc As IntPtr, hgdiobj As IntPtr) As IntPtr
    End Function

    <DllImport("gdi32.dll")>
    Private Shared Function DeleteObject(hObject As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SetWindowPos(hWnd As IntPtr, hWndInsertAfter As IntPtr,
                                         X As Integer, Y As Integer, cx As Integer, cy As Integer,
                                         uFlags As UInteger) As Boolean
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Private Structure W32Point
        Public X, Y As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure W32Size
        Public Width, Height As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure BLENDFUNCTION
        Public BlendOp As Byte
        Public BlendFlags As Byte
        Public SourceConstantAlpha As Byte
        Public AlphaFormat As Byte
    End Structure

    Private Const WS_EX_LAYERED As Integer = &H80000
    Private Const WS_EX_TOOLWINDOW As Integer = &H80
    Private Const WS_EX_NOACTIVATE As Integer = &H8000000
    Private Const WS_EX_TRANSPARENT As Integer = &H20

    Private Const AC_SRC_OVER As Byte = 0
    Private Const AC_SRC_ALPHA As Byte = 1
    Private Const ULW_ALPHA As Integer = 2

    Private Const SWP_NOACTIVATE As UInteger = &H10
    Private Const SWP_NOMOVE As UInteger = &H2
    Private Const SWP_NOSIZE As UInteger = &H1

#End Region

    Private _lastHostSize As Size

    Public Sub New()
        Me.FormBorderStyle = FormBorderStyle.None
        Me.ShowInTaskbar = False
        Me.StartPosition = FormStartPosition.Manual
    End Sub

    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim cp = MyBase.CreateParams
            cp.ExStyle = cp.ExStyle Or WS_EX_LAYERED Or WS_EX_TOOLWINDOW Or WS_EX_NOACTIVATE Or WS_EX_TRANSPARENT
            Return cp
        End Get
    End Property

    Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
        Get
            Return True
        End Get
    End Property

    ''' <summary>
    ''' 更新阴影的位置与大小。如果宿主窗口尺寸未变则仅移动，否则重新渲染。
    ''' </summary>
    Public Sub UpdateShadow(hostBounds As Rectangle, depth As Integer, color As Color, opacity As Byte, Optional moveOnly As Boolean = False)
        If depth <= 0 Then
            Me.Visible = False
            Return
        End If

        Dim totalW As Integer = hostBounds.Width + depth * 2
        Dim totalH As Integer = hostBounds.Height + depth * 2
        If totalW <= 0 OrElse totalH <= 0 Then Return

        Dim shadowX As Integer = hostBounds.X - depth
        Dim shadowY As Integer = hostBounds.Y - depth

        If moveOnly Then
            MoveToPosition(shadowX, shadowY)
            Return
        End If

        Dim hostSize As New Size(hostBounds.Width, hostBounds.Height)
        Dim needsRender As Boolean = (hostSize <> _lastHostSize)

        If needsRender Then
            _lastHostSize = hostSize
            Using bmp As New Bitmap(totalW, totalH, PixelFormat.Format32bppPArgb)
                RenderShadowBitmap(bmp, depth, hostSize.Width, hostSize.Height, color, opacity)
                ApplyLayeredBitmap(bmp, New Point(shadowX, shadowY), New Size(totalW, totalH))
            End Using
        Else
            MoveToPosition(shadowX, shadowY)
        End If
    End Sub

    ''' <summary>将阴影窗口放置在宿主窗口的 Z 序后方。</summary>
    Public Sub PlaceBehind(hostHandle As IntPtr)
        SetWindowPos(Me.Handle, hostHandle, 0, 0, 0, 0,
                     SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOACTIVATE)
    End Sub

    ''' <summary>仅移动阴影窗口位置，不重新渲染。</summary>
    Public Sub MoveToPosition(x As Integer, y As Integer)
        SetWindowPos(Me.Handle, IntPtr.Zero, x, y, 0, 0,
                     SWP_NOSIZE Or SWP_NOACTIVATE Or &H4UI)
    End Sub

    ''' <summary>强制下次 UpdateShadow 时重新渲染。</summary>
    Public Sub ForceReset()
        _lastHostSize = Size.Empty
    End Sub

    ''' <summary>渲染阴影位图（预乘 Alpha）。</summary>
    Private Shared Sub RenderShadowBitmap(bmp As Bitmap, depth As Integer,
                                    hostW As Integer, hostH As Integer,
                                    color As Color, maxAlpha As Byte)
        Dim rect As New Rectangle(0, 0, bmp.Width, bmp.Height)
        Dim data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb)

        Dim stride As Integer = data.Stride
        Dim bufLen As Integer = Math.Abs(stride) * bmp.Height
        Dim pixels(bufLen - 1) As Byte

        Dim w As Integer = bmp.Width
        Dim h As Integer = bmp.Height
        Dim cr As Byte = color.R
        Dim cg As Byte = color.G
        Dim cb As Byte = color.B
        Dim invDepth As Double = 1.0 / depth

        For y As Integer = 0 To h - 1
            Dim rowOff As Integer = y * stride
            For x As Integer = 0 To w - 1
                Dim dx As Integer = 0
                Dim dy As Integer = 0

                If x < depth Then
                    dx = depth - x
                ElseIf x >= depth + hostW Then
                    dx = x - (depth + hostW) + 1
                End If

                If y < depth Then
                    dy = depth - y
                ElseIf y >= depth + hostH Then
                    dy = y - (depth + hostH) + 1
                End If

                ' 窗口内部区域保持透明
                If dx = 0 AndAlso dy = 0 Then Continue For

                Dim dist As Double = Math.Sqrt(dx * dx + dy * dy)
                If dist >= depth Then Continue For

                Dim t As Double = (depth - dist) * invDepth
                Dim alpha As Integer = CInt(maxAlpha * t * t)
                If alpha <= 0 Then Continue For
                If alpha > 255 Then alpha = 255

                ' 预乘 Alpha (BGRA)
                Dim offset As Integer = rowOff + x * 4
                pixels(offset) = CByte(cb * alpha \ 255)
                pixels(offset + 1) = CByte(cg * alpha \ 255)
                pixels(offset + 2) = CByte(cr * alpha \ 255)
                pixels(offset + 3) = CByte(alpha)
            Next
        Next

        Marshal.Copy(pixels, 0, data.Scan0, pixels.Length)
        bmp.UnlockBits(data)
    End Sub

    ''' <summary>通过 UpdateLayeredWindow 将位图应用到分层窗口。</summary>
    Private Sub ApplyLayeredBitmap(bmp As Bitmap, position As Point, bmpSize As Size)
        Dim screenDC As IntPtr = GetDC(IntPtr.Zero)
        Dim memDC As IntPtr = CreateCompatibleDC(screenDC)
        Dim hBmp As IntPtr = bmp.GetHbitmap(Color.FromArgb(0, 0, 0, 0))
        Dim prev As IntPtr = SelectObject(memDC, hBmp)

        Try
            Dim ptDst As New W32Point With {.X = position.X, .Y = position.Y}
            Dim sz As New W32Size With {.Width = bmpSize.Width, .Height = bmpSize.Height}
            Dim ptSrc As New W32Point()
            Dim blend As New BLENDFUNCTION() With {
                .BlendOp = AC_SRC_OVER,
                .BlendFlags = 0,
                .SourceConstantAlpha = 255,
                .AlphaFormat = AC_SRC_ALPHA
            }
            UpdateLayeredWindow(Me.Handle, screenDC, ptDst, sz, memDC, ptSrc, 0, blend, ULW_ALPHA)
        Finally
            SelectObject(memDC, prev)
            DeleteObject(hBmp)
            DeleteDC(memDC)
            Dim unused = ReleaseDC(IntPtr.Zero, screenDC)
        End Try
    End Sub

End Class
