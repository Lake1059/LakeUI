Imports System.Diagnostics
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
Imports System.Threading.Tasks

''' <summary>Keep a top-level HWND off screen while its first visible control tree is painted.</summary>
Friend NotInheritable Class WindowFirstFrame
    Private Const DWMWA_CLOAK As Integer = 13
    Private Const RDW_INVALIDATE As UInteger = &H1UI
    Private Const RDW_ALLCHILDREN As UInteger = &H80UI
    Private Const RDW_UPDATENOW As UInteger = &H100UI
    Private Shared ReadOnly Pending As New ConditionalWeakTable(Of Form, WindowFirstFrame)()
    Private Shared ReadOnly Timeout As TimeSpan = TimeSpan.FromSeconds(5)

    <DllImport("dwmapi.dll")>
    Private Shared Function DwmSetWindowAttribute(hwnd As IntPtr, attribute As Integer, ByRef value As Integer, size As Integer) As Integer
    End Function

    <DllImport("dwmapi.dll")>
    Private Shared Function DwmFlush() As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function IsWindowVisible(hwnd As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function RedrawWindow(hwnd As IntPtr, rect As IntPtr, region As IntPtr, flags As UInteger) As Boolean
    End Function

    Private ReadOnly _form As Form
    Private ReadOnly _ready As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
    Private ReadOnly _timer As New Timer With {.Interval = 15}
    Private _beforeReveal As Action(Of Boolean)
    Private _fallbackToDx11 As Boolean
    Private _hwnd As IntPtr
    Private _preparedEpoch As Integer = -1
    Private _start As Long

    Private Sub New(form As Form)
        _form = form
    End Sub

    Friend Shared Function Defer(form As Form, Optional beforeReveal As Action(Of Boolean) = Nothing,
                                 Optional fallbackToDx11 As Boolean = False) As Task(Of Boolean)
        ArgumentNullException.ThrowIfNull(form)
        If form.IsDisposed OrElse form.InvokeRequired OrElse Not form.TopLevel OrElse
           D3D_PaintBridge.IsDesignTimeControl(form) Then Return Task.FromResult(False)

        Dim state As WindowFirstFrame = Nothing
        If Pending.TryGetValue(form, state) Then
            If beforeReveal IsNot Nothing Then state._beforeReveal = beforeReveal
            state._fallbackToDx11 = state._fallbackToDx11 OrElse fallbackToDx11
            Return state._ready.Task
        End If
        If form.IsHandleCreated AndAlso IsWindowVisible(form.Handle) Then Return Task.FromResult(False)

        state = New WindowFirstFrame(form) With {._beforeReveal = beforeReveal, ._fallbackToDx11 = fallbackToDx11}
        Pending.Add(form, state)
        state.Start()
        Return state._ready.Task
    End Function

    Friend Shared Function IsPending(form As Form) As Boolean
        Dim state As WindowFirstFrame = Nothing
        Return Pending.TryGetValue(form, state)
    End Function

    Private Sub Start()
        AddHandler _form.HandleCreated, AddressOf HandleCreated
        AddHandler _form.HandleDestroyed, AddressOf HandleDestroyed
        AddHandler _form.VisibleChanged, AddressOf VisibilityChanged
        AddHandler _form.Disposed, AddressOf FormDisposed
        AddHandler _timer.Tick, AddressOf CheckFirstFrame
        If _form.IsHandleCreated Then HandleCreated(_form, EventArgs.Empty)
    End Sub

    Private Sub HandleCreated(sender As Object, e As EventArgs)
        Dim hwnd = _form.Handle
        Dim cloak As Integer = 1
        If DwmSetWindowAttribute(hwnd, DWMWA_CLOAK, cloak, 4) < 0 Then
            Finish(False)
            Return
        End If
        _hwnd = hwnd
        If _form.Visible Then _timer.Start()
    End Sub

    Private Sub HandleDestroyed(sender As Object, e As EventArgs)
        _hwnd = IntPtr.Zero
        _preparedEpoch = -1
        _timer.Stop()
    End Sub

    Private Sub VisibilityChanged(sender As Object, e As EventArgs)
        If _hwnd = IntPtr.Zero Then Return
        If _form.Visible Then
            _timer.Start()
        ElseIf _timer.Enabled OrElse _start <> 0 Then
            Finish(False)
        End If
    End Sub

    Private Sub FormDisposed(sender As Object, e As EventArgs)
        Finish(False)
    End Sub

    Private Async Sub CheckFirstFrame(sender As Object, e As EventArgs)
        If Not IsWindowVisible(_hwnd) OrElse D3D_RenderUpdate.IsActive OrElse D3D_RenderUpdate.IsCommitting OrElse
           D3D_V5Presentation.IsRendering OrElse RenderBackendSwitching.InTransition Then Return
        If _start = 0 Then _start = Stopwatch.GetTimestamp()
        Try
            If RenderBackendStartup.Failure IsNot Nothing Then
                Await RecoverOrRevealAsync()
                Return
            End If
            If Stopwatch.GetElapsedTime(_start) >= Timeout Then
                Finish(False)
                Return
            End If

            If Not RenderBackendStartup.Requested.HasValue Then RenderBackendStartup.SelectTarget(RenderBackend.Direct3D11)
            Dim backend = RenderBackendStartup.Requested.Value
            If _preparedEpoch <> RenderBackendStartup.RouteEpoch Then
                _preparedEpoch = RenderBackendStartup.RouteEpoch
                ' Paint native children and batch the LakeUI control tree after Load and layout.
                Using D3D_RenderUpdate.Begin(_form)
                    RedrawWindow(_hwnd, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE Or RDW_ALLCHILDREN Or RDW_UPDATENOW)
                End Using
            End If

            If _ready.Task.IsCompleted Then Return
            If RenderBackendStartup.Failure IsNot Nothing Then
                Await RecoverOrRevealAsync()
                Return
            End If
            If RenderBackendStartup.Requested = backend AndAlso AllFramesPublished(_form, backend) Then
                Finish(DwmFlush() >= 0)
            End If
        Catch ex As Exception
            Debug.WriteLine(ex)
            Finish(False)
        End Try
    End Sub

    Private Async Function RecoverOrRevealAsync() As Task
        If _fallbackToDx11 AndAlso RenderBackendStartup.Requested = RenderBackend.Direct3D12 Then
            _fallbackToDx11 = False
            _timer.Stop()
            If Not RenderBackendStartup.Active.HasValue Then
                ' A partially initialized DX12 backend can still own HWND composition targets.
                D3D12StartupDispatch.ReleaseBackend()
            End If
            Dim result = Await GlobalOptions.SetRenderBackendAsync(RenderBackend.Direct3D11)
            If _ready.Task.IsCompleted Then Return
            If result = BackendSelectionResult.SelectedForStartup OrElse result = BackendSelectionResult.Switched OrElse
               result = BackendSelectionResult.AlreadyActive Then
                _start = Stopwatch.GetTimestamp()
                If _hwnd <> IntPtr.Zero AndAlso _form.Visible Then _timer.Start()
                Return
            End If
        End If
        Finish(False)
    End Function

    Private Shared Function AllFramesPublished(control As Control, backend As RenderBackend) As Boolean
        If control.IsDisposed OrElse Not control.Visible Then Return True
        If D3D_V5Presentation.IsV5Control(control) AndAlso control.ClientSize.Width > 0 AndAlso control.ClientSize.Height > 0 Then
            If Not control.IsHandleCreated OrElse Not D3D_V5Presentation.HasPublishedFrame(control, backend) Then Return False
        End If
        For Each child In control.Controls
            If Not AllFramesPublished(child, backend) Then Return False
        Next
        Return True
    End Function

    Private Sub Finish(ready As Boolean)
        If _ready.Task.IsCompleted Then Return
        _timer.Dispose()
        RemoveHandler _form.HandleCreated, AddressOf HandleCreated
        RemoveHandler _form.HandleDestroyed, AddressOf HandleDestroyed
        RemoveHandler _form.VisibleChanged, AddressOf VisibilityChanged
        RemoveHandler _form.Disposed, AddressOf FormDisposed
        Pending.Remove(_form)
        Try
            _beforeReveal?.Invoke(ready)
        Catch ex As Exception
            Debug.WriteLine(ex)
        Finally
            If _hwnd <> IntPtr.Zero Then
                Dim cloak As Integer = 0
                DwmSetWindowAttribute(_hwnd, DWMWA_CLOAK, cloak, 4)
                _hwnd = IntPtr.Zero
            End If
            _ready.TrySetResult(ready)
        End Try
    End Sub
End Class
