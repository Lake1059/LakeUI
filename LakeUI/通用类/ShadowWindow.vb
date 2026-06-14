Imports System.ComponentModel
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices

''' <summary>
''' 分层窗口，用于在宿主窗口后方渲染自定义深度的阴影。
''' 使用 UpdateLayeredWindow + 预乘 Alpha 位图绘制柔和阴影。
''' </summary>
''' <remarks>
''' ShadowWindow 是 <see cref="ThisIsYourWindow"/> 的辅助顶层窗口，不参与 D2D V2 compositor。
''' 它通过一张 32bppPArgb 位图生成阴影，再用 UpdateLayeredWindow 一次性提交给 DWM。
'''
''' 调用契约：
''' • 宿主窗口移动、大小变化、激活状态变化或自动阴影颜色变化时，由 ThisIsYourWindow 调用
'''   UpdateShadow / PlaceBehind 更新。
''' • 最大化、最小化、DWM 原生阴影模式或宿主不可见时应隐藏/销毁本窗口。
''' • ResizeWidth / ResizeFullArea 只决定阴影区域是否转发 hit-test 到宿主窗口，不改变绘制位图大小。
'''
''' 坑点：
''' • 分层窗口透明像素默认不会接收鼠标；需要可调整大小时必须在 WndProc 中显式返回对应 HT*。
''' • UpdateLayeredWindow 使用预乘 alpha。写入像素时若按非预乘 alpha 计算，边缘会发灰或出现黑边。
''' • 该窗口必须放在宿主后方，但又不能抢焦点；PlaceBehind 和 WS_EX_NOACTIVATE 都不能随意删。
''' </remarks>
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

    Private Const WM_NCHITTEST As Integer = &H84
    Private Const WM_LBUTTONDOWN As Integer = &H201
    Private Const WM_NCLBUTTONDOWN As Integer = &HA1
    Private Const HTTRANSPARENT As Integer = -1
    Private Const HTCLIENT As Integer = 1
    Private Const HTCAPTION As Integer = 2
    Private Const HTLEFT As Integer = 10
    Private Const HTRIGHT As Integer = 11
    Private Const HTTOP As Integer = 12
    Private Const HTTOPLEFT As Integer = 13
    Private Const HTTOPRIGHT As Integer = 14
    Private Const HTBOTTOM As Integer = 15
    Private Const HTBOTTOMLEFT As Integer = 16
    Private Const HTBOTTOMRIGHT As Integer = 17

    <DllImport("user32.dll")>
    Private Shared Function SendMessageW(hWnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll", EntryPoint:="GetWindowLongPtrW")>
    Private Shared Function GetWindowLongPtr(hWnd As IntPtr, nIndex As Integer) As IntPtr
    End Function

    <DllImport("user32.dll", EntryPoint:="SetWindowLongPtrW")>
    Private Shared Function SetWindowLongPtr(hWnd As IntPtr, nIndex As Integer, dwNewLong As IntPtr) As IntPtr
    End Function

    Private Const GWL_EXSTYLE As Integer = -20
    Private Const SWP_FRAMECHANGED As UInteger = &H20
    Private Const SWP_NOZORDER As UInteger = &H4

#End Region

    Private _lastHostSize As Size
    Private _globalAlpha As Byte = 255

    ''' <summary>宿主窗口句柄，用于转发调整大小消息。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property HostHandle As IntPtr = IntPtr.Zero

    ''' <summary>阴影扩展深度（逻辑像素）。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property ShadowDepth As Integer = 0

    ''' <summary>阴影区域中可触发大小调整的热区宽度（逻辑像素）。0 = 鼠标穿透。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property ResizeWidth As Integer = 0

    ''' <summary>是否将整个阴影绘制区域作为窗口大小调整热区（忽略 <see cref="ResizeWidth"/> 上限）。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property ResizeFullArea As Boolean = False

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

    ''' <summary>根据 ResizeWidth / ResizeFullArea 更新是否允许鼠标命中测试。</summary>
    Public Sub UpdateHitTestTransparency()
        If Not Me.IsHandleCreated Then Return
        Dim exStyle As Long = GetWindowLongPtr(Me.Handle, GWL_EXSTYLE).ToInt64()
        If ResizeWidth > 0 OrElse ResizeFullArea Then
            exStyle = exStyle And Not CLng(WS_EX_TRANSPARENT)
        Else
            exStyle = exStyle Or WS_EX_TRANSPARENT
        End If
        SetWindowLongPtr(Me.Handle, GWL_EXSTYLE, New IntPtr(exStyle))
        SetWindowPos(Me.Handle, IntPtr.Zero, 0, 0, 0, 0,
                     SWP_FRAMECHANGED Or SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOZORDER Or SWP_NOACTIVATE)
    End Sub

    Protected Overrides Sub WndProc(ByRef m As Message)
        If m.Msg = WM_NCHITTEST AndAlso ShadowDepth > 0 AndAlso HostHandle <> IntPtr.Zero AndAlso (ResizeWidth > 0 OrElse ResizeFullArea) Then
            ' 使用屏幕坐标解析
            Dim lp As Long = m.LParam.ToInt64()
            Dim sx As Integer = CInt(lp And &HFFFF) : If sx > 32767 Then sx -= 65536
            Dim sy As Integer = CInt((lp >> 16) And &HFFFF) : If sy > 32767 Then sy -= 65536

            ' 阴影窗口的客户端坐标
            Dim clientPt As Point = Me.PointToClient(New Point(sx, sy))
            Dim totalW As Integer = Me.Width
            Dim totalH As Integer = Me.Height
            Dim d As Integer = ShadowDepth
            ' 启用 ResizeFullArea 时整个阴影绘制区域都参与调整大小
            Dim rw As Integer = If(ResizeFullArea, d, ResizeWidth)

            ' 鼠标在阴影区域（窗口本体之外的部分）
            Dim inLeft As Boolean = (clientPt.X < d)
            Dim inRight As Boolean = (clientPt.X >= totalW - d)
            Dim inTop As Boolean = (clientPt.Y < d)
            Dim inBottom As Boolean = (clientPt.Y >= totalH - d)
            Dim inShadow As Boolean = inLeft OrElse inRight OrElse inTop OrElse inBottom

            If Not inShadow Then
                ' 在窗口本体范围内，让点击穿透到宿主窗口
                m.Result = New IntPtr(HTTRANSPARENT)
                Return
            End If

            Dim hit As Integer = HTTRANSPARENT

            If rw > 0 Then
                ' 只有在距离窗口本体 rw 像素以内的阴影区域才触发调整
                Dim nearLeft As Boolean = inLeft AndAlso (d - clientPt.X) <= rw
                Dim nearRight As Boolean = inRight AndAlso (clientPt.X - (totalW - d - 1)) <= rw
                Dim nearTop As Boolean = inTop AndAlso (d - clientPt.Y) <= rw
                Dim nearBottom As Boolean = inBottom AndAlso (clientPt.Y - (totalH - d - 1)) <= rw

                If nearTop AndAlso nearLeft Then
                    hit = HTTOPLEFT
                ElseIf nearTop AndAlso nearRight Then
                    hit = HTTOPRIGHT
                ElseIf nearBottom AndAlso nearLeft Then
                    hit = HTBOTTOMLEFT
                ElseIf nearBottom AndAlso nearRight Then
                    hit = HTBOTTOMRIGHT
                ElseIf nearLeft Then
                    hit = HTLEFT
                ElseIf nearRight Then
                    hit = HTRIGHT
                ElseIf nearTop Then
                    hit = HTTOP
                ElseIf nearBottom Then
                    hit = HTBOTTOM
                End If
            End If

            m.Result = New IntPtr(hit)
            Return
        End If

        If m.Msg = WM_NCLBUTTONDOWN AndAlso HostHandle <> IntPtr.Zero AndAlso (ResizeWidth > 0 OrElse ResizeFullArea) Then
            Dim hit As Integer = m.WParam.ToInt32()
            ' 阴影窗口承担的调整热区一律转交给宿主，避免拖动阴影窗口自身
            If hit >= HTLEFT AndAlso hit <= HTBOTTOMRIGHT Then
                ReleaseCapture()
                SendMessageW(HostHandle, WM_NCLBUTTONDOWN, New IntPtr(hit), m.LParam)
                m.Result = IntPtr.Zero
                Return
            End If
        End If

        MyBase.WndProc(m)
    End Sub

    <DllImport("user32.dll")>
    Private Shared Function ReleaseCapture() As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

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

    <DllImport("user32.dll", EntryPoint:="UpdateLayeredWindow")>
    Private Shared Function UpdateLayeredWindowBlend(
        hwnd As IntPtr, hdcDst As IntPtr,
        pptDst As IntPtr, psize As IntPtr,
        hdcSrc As IntPtr, pptSrc As IntPtr,
        crKey As Integer, ByRef pblend As BLENDFUNCTION,
        dwFlags As Integer) As Boolean
    End Function

    ''' <summary>设置阴影窗口的全局透明度乘数（0=完全透明，255=完全不透明）。</summary>
    Public Sub SetGlobalAlpha(alpha As Byte)
        _globalAlpha = alpha
        Dim blend As New BLENDFUNCTION() With {
            .BlendOp = AC_SRC_OVER,
            .BlendFlags = 0,
            .SourceConstantAlpha = alpha,
            .AlphaFormat = AC_SRC_ALPHA
        }
        UpdateLayeredWindowBlend(Me.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                                 IntPtr.Zero, IntPtr.Zero, 0, blend, ULW_ALPHA)
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
                .SourceConstantAlpha = _globalAlpha,
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
