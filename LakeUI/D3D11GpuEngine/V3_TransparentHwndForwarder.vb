Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices

''' <summary>
''' V3_TransparentHwndForwarder handles the WinForms child-HWND boundary for window-level GPU rendering.
''' V3 controls still need WinForms HWNDs for layout and input, but their pixels are emitted into the top-level
''' D3D_WindowCompositor surface. Without transparent forwarding, empty child HWNDs cover the swap chain and the
''' client area appears black.
''' </summary>
Friend NotInheritable Class V3_TransparentHwndForwarder
    Private Const GWL_EXSTYLE As Integer = -20
    Private Const WS_EX_LAYERED As Integer = &H80000
    Private Const WS_EX_TRANSPARENT As Integer = &H20
    Private Const LWA_ALPHA As UInteger = &H2UI
    Private Const TransparentAlpha As Byte = 1
    Private Const WM_PAINT As Integer = &HF
    Private Const WM_NCPAINT As Integer = &H85
    Private Const WM_ERASEBKGND As Integer = &H14
    Private Const WM_PRINT As Integer = &H317
    Private Const WM_PRINTCLIENT As Integer = &H318
    Private Const SWP_NOSIZE As UInteger = &H1UI
    Private Const SWP_NOMOVE As UInteger = &H2UI
    Private Const SWP_NOZORDER As UInteger = &H4UI
    Private Const SWP_NOACTIVATE As UInteger = &H10UI
    Private Const SWP_FRAMECHANGED As UInteger = &H20UI

    Private Shared ReadOnly _registrations As New ConditionalWeakTable(Of Control, Registration)()

    Private Sub New()
    End Sub

    Public Shared Sub Enable(control As Control)
        If control Is Nothing OrElse control.IsDisposed Then Return
        If Not ShouldEnableTransparentHwnd(control) Then Return

        Dim suppressPaint = ShouldSuppressNativePaint(control)
        SyncLock _registrations
            Dim registration As Registration = Nothing
            If Not _registrations.TryGetValue(control, registration) Then
                _registrations.Add(control, New Registration(control, suppressPaint))
            Else
                registration.SuppressPaint = suppressPaint
            End If
        End SyncLock

        ApplyStyle(control)
    End Sub

    Public Shared Sub EnableTree(root As Control)
        If root Is Nothing OrElse root.IsDisposed Then Return

        Enable(root)

        Dim children As New System.Collections.Generic.List(Of Control)()
        Try
            For Each child As Control In root.Controls
                children.Add(child)
            Next
        Catch
        End Try

        For Each child In children
            EnableTree(child)
        Next
    End Sub

    Private Shared Function ShouldEnableTransparentHwnd(control As Control) As Boolean
        If control Is Nothing OrElse control.IsDisposed Then Return False
        Dim form = TryCast(control, Form)
        If form IsNot Nothing AndAlso form.TopLevel Then Return False
        If TypeOf control Is V3_IGpuRenderable Then Return True
        If ContainsGpuRenderableDescendant(control) Then Return True
        Return False
    End Function

    Private Shared Function ContainsGpuRenderableDescendant(control As Control) As Boolean
        If control Is Nothing OrElse control.IsDisposed Then Return False
        Try
            For Each child As Control In control.Controls
                If child Is Nothing OrElse child.IsDisposed Then Continue For
                If TypeOf child Is V3_IGpuRenderable Then Return True
                If ContainsGpuRenderableDescendant(child) Then Return True
            Next
        Catch
            Return False
        End Try
        Return False
    End Function

    Private Shared Function ShouldSuppressNativePaint(control As Control) As Boolean
        If control Is Nothing OrElse control.IsDisposed Then Return False
        Return TypeOf control Is V3_IGpuRenderable
    End Function

    Private Shared Sub ApplyStyle(control As Control)
        If control Is Nothing OrElse control.IsDisposed OrElse Not control.IsHandleCreated Then Return

        Dim exStyle = GetWindowLongPtr(control.Handle, GWL_EXSTYLE).ToInt64()
        Dim requiredStyle = CLng(WS_EX_LAYERED)
        Dim newStyle = (exStyle Or requiredStyle) And Not CLng(WS_EX_TRANSPARENT)

        If exStyle <> newStyle Then
            SetWindowLongPtr(control.Handle, GWL_EXSTYLE, New IntPtr(newStyle))
            SetWindowPos(control.Handle,
                         IntPtr.Zero,
                         0,
                         0,
                         0,
                         0,
                         SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOZORDER Or SWP_NOACTIVATE Or SWP_FRAMECHANGED)
        End If

        SetLayeredWindowAttributes(control.Handle, 0UI, TransparentAlpha, LWA_ALPHA)
    End Sub

    Private NotInheritable Class Registration
        Inherits NativeWindow

        Private ReadOnly _control As Control
        Private _suppressPaint As Boolean

        Public Sub New(control As Control, suppressPaint As Boolean)
            _control = control
            _suppressPaint = suppressPaint
            AddHandler _control.HandleCreated, AddressOf OnHandleCreated
            AddHandler _control.HandleDestroyed, AddressOf OnHandleDestroyed
            AddHandler _control.Disposed, AddressOf OnDisposed
            If _control.IsHandleCreated Then AssignHandle(_control.Handle)
        End Sub

        Public Property SuppressPaint As Boolean
            Get
                Return _suppressPaint
            End Get
            Set(value As Boolean)
                _suppressPaint = value
            End Set
        End Property

        Private Sub OnHandleCreated(sender As Object, e As EventArgs)
            Dim ctrl = TryCast(sender, Control)
            If ctrl Is Nothing Then Return
            AssignHandle(ctrl.Handle)
            ApplyStyle(ctrl)
        End Sub

        Private Sub OnHandleDestroyed(sender As Object, e As EventArgs)
            ReleaseHandle()
        End Sub

        Private Sub OnDisposed(sender As Object, e As EventArgs)
            If _control Is Nothing Then Return
            RemoveHandler _control.HandleCreated, AddressOf OnHandleCreated
            RemoveHandler _control.HandleDestroyed, AddressOf OnHandleDestroyed
            RemoveHandler _control.Disposed, AddressOf OnDisposed
            ReleaseHandle()
        End Sub

        Protected Overrides Sub WndProc(ByRef m As Message)
            If _suppressPaint AndAlso m.Msg = WM_ERASEBKGND Then
                m.Result = New IntPtr(1)
                Return
            End If
            If _suppressPaint AndAlso (m.Msg = WM_PAINT OrElse m.Msg = WM_NCPAINT) Then
                If m.Msg = WM_PAINT AndAlso m.HWnd <> IntPtr.Zero Then ValidateRect(m.HWnd, IntPtr.Zero)
                m.Result = IntPtr.Zero
                Return
            End If
            MyBase.WndProc(m)
        End Sub
    End Class

    <DllImport("user32.dll", EntryPoint:="GetWindowLongPtrW", SetLastError:=True)>
    Private Shared Function GetWindowLongPtr64(hWnd As IntPtr, nIndex As Integer) As IntPtr
    End Function

    <DllImport("user32.dll", EntryPoint:="GetWindowLongW", SetLastError:=True)>
    Private Shared Function GetWindowLongPtr32(hWnd As IntPtr, nIndex As Integer) As IntPtr
    End Function

    Private Shared Function GetWindowLongPtr(hWnd As IntPtr, nIndex As Integer) As IntPtr
        If IntPtr.Size = 8 Then Return GetWindowLongPtr64(hWnd, nIndex)
        Return GetWindowLongPtr32(hWnd, nIndex)
    End Function

    <DllImport("user32.dll", EntryPoint:="SetWindowLongPtrW", SetLastError:=True)>
    Private Shared Function SetWindowLongPtr64(hWnd As IntPtr, nIndex As Integer, dwNewLong As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll", EntryPoint:="SetWindowLongW", SetLastError:=True)>
    Private Shared Function SetWindowLongPtr32(hWnd As IntPtr, nIndex As Integer, dwNewLong As IntPtr) As IntPtr
    End Function

    Private Shared Function SetWindowLongPtr(hWnd As IntPtr, nIndex As Integer, dwNewLong As IntPtr) As IntPtr
        If IntPtr.Size = 8 Then Return SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
        Return SetWindowLongPtr32(hWnd, nIndex, dwNewLong)
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function SetWindowPos(hWnd As IntPtr,
                                         hWndInsertAfter As IntPtr,
                                         x As Integer,
                                         y As Integer,
                                         cx As Integer,
                                         cy As Integer,
                                         uFlags As UInteger) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function ValidateRect(hWnd As IntPtr, lpRect As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function SetLayeredWindowAttributes(hwnd As IntPtr,
                                                       crKey As UInteger,
                                                       bAlpha As Byte,
                                                       dwFlags As UInteger) As Boolean
    End Function

End Class
