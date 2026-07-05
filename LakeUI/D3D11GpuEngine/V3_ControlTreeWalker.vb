Imports System.Runtime.InteropServices

''' <summary>
''' V3_ControlTreeWalker 是后续控件迁移使用的非渲染树遍历辅助。
''' 它不创建 GPU 资源，不绘制控件，只枚举实现 V3_IGpuRenderable 的控件并提供窗口坐标映射。
''' </summary>
Friend NotInheritable Class V3_ControlTreeWalker
    Private Sub New()
    End Sub

    Public Shared Iterator Function EnumerateGpuRenderables(root As Control) As IEnumerable(Of Control)
        If root Is Nothing OrElse root.IsDisposed Then Return

        For Each child As Control In root.Controls
            If child Is Nothing OrElse child.IsDisposed Then Continue For
            If TypeOf child Is V3_IGpuRenderable Then Yield child

            For Each nested In EnumerateGpuRenderables(child)
                Yield nested
            Next
        Next
    End Function

    Public Shared Function GetWindowBounds(control As Control, form As Form) As Rectangle
        If control Is Nothing OrElse form Is Nothing OrElse control.IsDisposed OrElse form.IsDisposed Then Return Rectangle.Empty
        Try
            Dim topLeft = form.PointToClient(control.PointToScreen(Point.Empty))
            Return New Rectangle(topLeft, control.Size)
        Catch
        End Try

        If control.IsHandleCreated Then
            Dim rect As NativeRect
            If GetWindowRect(control.Handle, rect) Then
                Return ScreenRectToFormClient(rect, form)
            End If
        End If

        If control.Parent IsNot Nothing AndAlso Not control.Parent.IsDisposed Then
            Try
                Dim topLeft = form.PointToClient(control.Parent.PointToScreen(control.Location))
                Return New Rectangle(topLeft, control.Bounds.Size)
            Catch
            End Try
        End If

        Dim layoutTopLeft As Point = Point.Empty
        If TryGetControlLocationInAncestor(control, form, layoutTopLeft) Then
            Return New Rectangle(layoutTopLeft, control.Bounds.Size)
        End If

        Return Rectangle.Empty
    End Function

    Public Shared Function GetWindowClientBounds(control As Control, form As Form) As Rectangle
        If control Is Nothing OrElse form Is Nothing OrElse control.IsDisposed OrElse form.IsDisposed Then Return Rectangle.Empty
        Try
            Dim topLeft = form.PointToClient(control.PointToScreen(Point.Empty))
            Return New Rectangle(topLeft, control.ClientSize)
        Catch
        End Try

        If control.IsHandleCreated Then
            Dim rect As NativeRect
            If GetClientRect(control.Handle, rect) Then
                Dim clientTopLeft As New NativePoint(rect.Left, rect.Top)
                If ClientToScreen(control.Handle, clientTopLeft) Then
                    Dim topLeft = form.PointToClient(New Point(clientTopLeft.X, clientTopLeft.Y))
                    Return New Rectangle(topLeft, New Size(rect.Right - rect.Left, rect.Bottom - rect.Top))
                End If
            End If
        End If

        If control.Parent IsNot Nothing AndAlso Not control.Parent.IsDisposed Then
            Try
                Dim topLeft = form.PointToClient(control.Parent.PointToScreen(control.Location))
                Return New Rectangle(topLeft, control.ClientSize)
            Catch
            End Try
        End If

        Dim layoutTopLeft As Point = Point.Empty
        If TryGetControlLocationInAncestor(control, form, layoutTopLeft) Then
            Return New Rectangle(layoutTopLeft, control.ClientSize)
        End If

        Return Rectangle.Empty
    End Function

    Private Shared Function TryGetControlLocationInAncestor(control As Control, ancestor As Control, ByRef topLeft As Point) As Boolean
        topLeft = Point.Empty
        If control Is Nothing OrElse ancestor Is Nothing Then Return False
        If control.IsDisposed OrElse ancestor.IsDisposed Then Return False

        Dim x As Integer = 0
        Dim y As Integer = 0
        Dim current As Control = control
        While current IsNot Nothing AndAlso current IsNot ancestor
            x += current.Left
            y += current.Top
            current = current.Parent
        End While

        If current IsNot ancestor Then Return False
        topLeft = New Point(x, y)
        Return True
    End Function

    Private Shared Function ScreenRectToFormClient(rect As NativeRect, form As Form) As Rectangle
        Dim topLeft = form.PointToClient(New Point(rect.Left, rect.Top))
        Dim bottomRight = form.PointToClient(New Point(rect.Right, rect.Bottom))
        Return Rectangle.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y)
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativePoint
        Public X As Integer
        Public Y As Integer

        Public Sub New(x As Integer, y As Integer)
            Me.X = x
            Me.Y = y
        End Sub
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeRect
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function GetWindowRect(hWnd As IntPtr, ByRef lpRect As NativeRect) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function GetClientRect(hWnd As IntPtr, ByRef lpRect As NativeRect) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function ClientToScreen(hWnd As IntPtr, ByRef lpPoint As NativePoint) As Boolean
    End Function
End Class
