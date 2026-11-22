Imports System.ComponentModel
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Threading.Tasks

Public Enum RenderBackend
    Direct3D11
    Direct3D12
End Enum

Public Enum BackendSelectionResult
    SelectedForStartup
    AlreadyActive
    SwitchUnavailable
    Switched
    CandidateRejected
    RolledBack
    Unavailable
    NoVisibleParticipants
End Enum

Partial Public Class GlobalOptions
    Public Shared Function SetRenderBackendAsync(target As RenderBackend) As Task(Of BackendSelectionResult)
        If RenderBackendStartup.IsDesignerPreview Then Return Task.FromResult(BackendSelectionResult.SwitchUnavailable)
        If RenderBackendStartup.Active.HasValue OrElse RenderBackendSwitching.IsPending Then Return RenderBackendSwitching.Request(target)
        Return Task.FromResult(RenderBackendStartup.SelectTarget(target))
    End Function

    Public Shared ReadOnly Property LastBackendSwitch As BackendSwitchDiagnostics
        Get
            Return RenderBackendSwitching.LastResult
        End Get
    End Property

    ''' <summary>Defer the first display of a top-level form until its visible controls have published a frame. Call before showing the form.</summary>
    ''' <returns>True when the frame is ready, or False if the form was already shown, closed, or presentation could not be completed.</returns>
    Public Shared Function DeferWindowDisplayUntilFirstFrameAsync(form As Form, Optional fallbackToDx11 As Boolean = False) As Task(Of Boolean)
        Return WindowFirstFrame.Defer(form, fallbackToDx11:=fallbackToDx11)
    End Function

    Public Shared Function WaitForFirstFrameAsync() As Task(Of RenderBackend)
        Return RenderBackendStartup.FirstFrame
    End Function

    Public Shared ReadOnly Property RequestedBackend As RenderBackend?
        Get
            Return RenderBackendStartup.Requested
        End Get
    End Property

    Public Shared ReadOnly Property ActiveBackend As RenderBackend?
        Get
            Return RenderBackendStartup.Active
        End Get
    End Property

    Public Shared ReadOnly Property BackendStartupError As Exception
        Get
            Return RenderBackendStartup.Failure
        End Get
    End Property
End Class

Friend Module RenderBackendStartup
    Private ReadOnly gate As New Object()
    Private ReadOnly hosts As New ConditionalWeakTable(Of Form, Object)()
    Private selected As Integer = -1
    Private published As Integer = -1
    Private completion As New TaskCompletionSource(Of RenderBackend)(TaskCreationOptions.RunContinuationsAsynchronously)
    Private factoryStarted As Boolean
    Private designerPreview As Boolean
    Private renderThread As Integer
    Private multipleRenderThreads As Integer
    Private generationSequence As Integer
    Private routeRevision As Integer

    Friend ReadOnly Property RouteEpoch As Integer
        Get
            Return Volatile.Read(routeRevision)
        End Get
    End Property
    <ThreadStatic>
    Private candidate As RenderBackend?

    Friend Function NextGeneration() As Integer
        Return Interlocked.Increment(generationSequence)
    End Function

    Friend Function IsSingleRenderThread() As Boolean
        Return renderThread = Environment.CurrentManagedThreadId AndAlso Volatile.Read(multipleRenderThreads) = 0
    End Function

    Friend Sub TrackThread()
        Dim id = Environment.CurrentManagedThreadId
        Dim previous = Interlocked.CompareExchange(renderThread, id, 0)
        If previous <> 0 AndAlso previous <> id Then Volatile.Write(multipleRenderThreads, 1)
    End Sub

    Friend Sub Preflight(target As RenderBackend, action As Action)
        candidate = target
        Try
            action()
        Finally
            candidate = Nothing
        End Try
    End Sub

    Friend Sub BeginTransition(target As RenderBackend)
        SyncLock gate
            Interlocked.Increment(routeRevision)
            Volatile.Write(published, -1)
            Volatile.Write(selected, CInt(target))
            Volatile.Write(startupException, Nothing)
            completion = New TaskCompletionSource(Of RenderBackend)(TaskCreationOptions.RunContinuationsAsynchronously)
        End SyncLock
    End Sub
    Private startupException As Exception

    <DllImport("user32.dll")>
    Private Function IsWindowVisible(hwnd As IntPtr) As Boolean
    End Function

    Friend ReadOnly Property Requested As RenderBackend?
        Get
            Dim value = Volatile.Read(selected)
            If value < 0 Then Return Nothing
            Return CType(value, RenderBackend)
        End Get
    End Property

    Friend ReadOnly Property Active As RenderBackend?
        Get
            Dim value = Volatile.Read(published)
            If value < 0 Then Return Nothing
            Return CType(value, RenderBackend)
        End Get
    End Property

    Friend ReadOnly Property Failure As Exception
        Get
            Return Volatile.Read(startupException)
        End Get
    End Property

    Friend ReadOnly Property FirstFrame As Task(Of RenderBackend)
        Get
            SyncLock gate
                Return completion.Task
            End SyncLock
        End Get
    End Property

    Friend ReadOnly Property CanScheduleDx11 As Boolean
        Get
            Return Volatile.Read(selected) = CInt(RenderBackend.Direct3D11) AndAlso Volatile.Read(startupException) Is Nothing AndAlso Not RenderBackendSwitching.BackgroundPaused
        End Get
    End Property

    Friend ReadOnly Property IsDesignerPreview As Boolean
        Get
            Return Volatile.Read(designerPreview)
        End Get
    End Property

    Friend Function TryStartDesignerPreview() As Boolean
        If IsDesignerPreview Then Return True
        SyncLock gate
            If designerPreview Then Return True
            ' VS 的设计器进程不经过应用的 Shown。已有运行时设备的进程不改选后端。
            If factoryStarted OrElse published >= 0 Then Return False
            Dim setting = TryCast(AppContext.GetData("LakeUI.DesignerBackend"), String)
            Dim target = If(String.Equals(setting, "DX12", StringComparison.OrdinalIgnoreCase),
                            RenderBackend.Direct3D12, RenderBackend.Direct3D11)
            SelectStartupTargetNoLock(target)
            Volatile.Write(designerPreview, True)
            Return True
        End SyncLock
    End Function

    Friend Function SelectTarget(target As RenderBackend) As BackendSelectionResult
        If Not [Enum].IsDefined(target) Then Throw New ArgumentOutOfRangeException(NameOf(target))
        SyncLock gate
            If designerPreview Then Return BackendSelectionResult.SwitchUnavailable
            If published = CInt(target) Then Return BackendSelectionResult.AlreadyActive
            If published >= 0 OrElse (factoryStarted AndAlso selected <> CInt(target) AndAlso startupException Is Nothing) Then Return BackendSelectionResult.SwitchUnavailable

            SelectStartupTargetNoLock(target)
        End SyncLock
        If target = RenderBackend.Direct3D11 Then D3D_BackdropSurfaceRenderer.ReplayStartupRequests()
        Return BackendSelectionResult.SelectedForStartup
    End Function

    Private Sub SelectStartupTargetNoLock(target As RenderBackend)
        If selected <> CInt(target) OrElse startupException IsNot Nothing Then
            Interlocked.Increment(routeRevision)
            If selected >= 0 Then
                completion.TrySetCanceled()
                completion = New TaskCompletionSource(Of RenderBackend)(TaskCreationOptions.RunContinuationsAsynchronously)
            End If
        End If
        Volatile.Write(selected, CInt(target))
        Volatile.Write(startupException, Nothing)
    End Sub

    Friend Sub RequireFactory(target As RenderBackend, Optional background As Boolean = False)
        SyncLock gate
            Dim licensingDesignTime = LicenseManager.UsageMode = LicenseUsageMode.Designtime
            If candidate = target AndAlso Not background AndAlso Not licensingDesignTime AndAlso Not designerPreview Then Return
            If selected <> CInt(target) OrElse startupException IsNot Nothing OrElse (licensingDesignTime AndAlso Not designerPreview) Then
                Throw New InvalidOperationException($"The {target} device factory is closed.")
            End If
            factoryStarted = True
        End SyncLock
    End Sub

    Friend Sub PublishedFrame(target As RenderBackend, Optional transitionComplete As Boolean = False)
        If Not transitionComplete AndAlso RenderBackendSwitching.InTransition Then Return
        If Volatile.Read(selected) = CInt(target) AndAlso Interlocked.CompareExchange(published, CInt(target), -1) = -1 Then
            completion.TrySetResult(target)
        End If
    End Sub

    Friend Sub Failed(ex As Exception)
        SyncLock gate
            If startupException IsNot Nothing Then Return
            Volatile.Write(startupException, ex)
            completion.TrySetException(ex)
        End SyncLock
    End Sub

    Friend Sub ObserveHost(control As Control)
        If Volatile.Read(selected) >= 0 OrElse control Is Nothing OrElse control.IsDisposed OrElse control.InvokeRequired Then Return
        Dim host = D3D_RenderCore.ResolveCompositorForm(control)
        If host Is Nothing OrElse Not host.TopLevel OrElse host.Owner IsNot Nothing OrElse host.Modal OrElse D3D_PaintBridge.IsDesignTimeControl(control) Then Return
        If TypeOf host Is PopupForm OrElse TypeOf host Is ShadowWindow OrElse TypeOf host Is FloatingToolTipForm Then Return
        Dim observed As Object = Nothing
        If hosts.TryGetValue(host, observed) Then Return
        hosts.GetValue(host, Function(f) New Object())
        AddHandler host.Shown, AddressOf HostShown
        ' Late attachment: the native top-level window has already been shown.
        If host.IsHandleCreated AndAlso IsWindowVisible(host.Handle) Then HostShown(host, EventArgs.Empty)
    End Sub

    Private Sub HostShown(sender As Object, e As EventArgs)
        Dim host = DirectCast(sender, Form)
        RemoveHandler host.Shown, AddressOf HostShown
        If D3D_PaintBridge.IsDesignTimeControl(host) Then Return
        If Application.OpenForms.Count = 0 OrElse Application.OpenForms(0) IsNot host Then Return
        SyncLock gate
            If selected < 0 Then SelectTarget(RenderBackend.Direct3D11)
        End SyncLock
        host.Invalidate(True)
    End Sub
End Module
