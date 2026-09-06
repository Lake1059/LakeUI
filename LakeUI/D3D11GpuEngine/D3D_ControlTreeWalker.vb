Imports System.Runtime.InteropServices

''' <summary>
''' D3D_ControlTreeWalker 是后续控件迁移使用的非渲染树遍历辅助。
''' 它不创建 GPU 资源，不绘制控件，只枚举实现 D3D_IGpuRenderable 的控件并提供窗口坐标映射。
''' </summary>
Friend NotInheritable Class D3D_ControlTreeWalker
    Private NotInheritable Class DepthRecord
        Public Parent As Control
        Public ParentVersion As Long
        Public Depth As Integer
        Public Version As Long
    End Class

    Private Shared ReadOnly _depthCache As New System.Runtime.CompilerServices.ConditionalWeakTable(Of Control, DepthRecord)()
    Private Shared _depthSequence As Long
    <ThreadStatic>
    Private Shared _depthVisited As HashSet(Of Control)

    Private Sub New()
    End Sub

    Friend Shared Function GetTreeDepth(control As Control) As Integer
        If control Is Nothing Then Return 0
        If _depthVisited Is Nothing Then _depthVisited = New HashSet(Of Control)()
        _depthVisited.Clear()
        Try
            Return GetTreeDepthCore(control, _depthVisited)
        Finally
            _depthVisited.Clear()
        End Try
    End Function

    ''' <summary>
    ''' 判断控件及其 WinForms 祖先是否全部可见。
    ''' Control.Visible 是本地状态，隐藏页的后代仍可能返回 True；调度必须使用有效可见性，
    ''' 避免为隐藏控件创建或保留 GPU 表面。
    ''' </summary>
    Friend Shared Function IsEffectivelyVisible(control As Control) As Boolean
        If control Is Nothing OrElse control.IsDisposed OrElse Not control.Visible Then Return False

        Dim visited As HashSet(Of Control) = Nothing
        While control IsNot Nothing
            If visited Is Nothing Then visited = New HashSet(Of Control)()
            If Not visited.Add(control) Then Return False
            If control.IsDisposed OrElse Not control.Visible Then Return False
            control = control.Parent
        End While
        Return True
    End Function

    Friend Shared Function IsDescendantOrSelf(control As Control, ancestor As Control) As Boolean
        If control Is Nothing OrElse ancestor Is Nothing Then Return False
        Dim visited As New HashSet(Of Control)()
        Dim current = control
        While current IsNot Nothing AndAlso visited.Add(current)
            If Object.ReferenceEquals(current, ancestor) Then Return True
            current = current.Parent
        End While
        Return False
    End Function

    Private Shared Function GetTreeDepthCore(control As Control, visited As HashSet(Of Control)) As Integer
        If control Is Nothing OrElse Not visited.Add(control) Then Return 0

        Dim parent = control.Parent
        Dim parentDepth As Integer
        Dim parentVersion As Long
        If parent Is Nothing Then
            parentDepth = 0
            parentVersion = 0
        Else
            parentDepth = GetTreeDepthCore(parent, visited)
            Dim parentRecord As DepthRecord = Nothing
            If _depthCache.TryGetValue(parent, parentRecord) Then parentVersion = parentRecord.Version
        End If

        Dim record As DepthRecord = Nothing
        If Not _depthCache.TryGetValue(control, record) Then
            record = New DepthRecord()
            _depthCache.Add(control, record)
        End If

        If Object.ReferenceEquals(record.Parent, parent) AndAlso record.ParentVersion = parentVersion Then
            Return record.Depth
        End If

        record.Parent = parent
        record.ParentVersion = parentVersion
        record.Depth = parentDepth + 1
        record.Version = Threading.Interlocked.Increment(_depthSequence)
        Return record.Depth
    End Function

    Public Shared Iterator Function EnumerateGpuRenderables(root As Control) As IEnumerable(Of Control)
        If root Is Nothing OrElse root.IsDisposed Then Return

        For Each child As Control In root.Controls
            If child Is Nothing OrElse child.IsDisposed Then Continue For
            If TypeOf child Is D3D_IGpuRenderable Then Yield child

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
