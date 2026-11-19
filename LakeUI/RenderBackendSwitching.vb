Imports System.Threading
Imports System.Threading.Tasks

Public NotInheritable Class BackendSwitchDiagnostics
    Public ReadOnly Property Requested As RenderBackend
    Public ReadOnly Property Active As RenderBackend?
    Public ReadOnly Property Result As BackendSelectionResult
    Public ReadOnly Property FailureStage As String
    Public ReadOnly Property Cause As Exception
    Public ReadOnly Property RollbackCause As Exception
    Public ReadOnly Property Adapter As String
    Public ReadOnly Property Elapsed As TimeSpan

    Friend Sub New(requested As RenderBackend, result As BackendSelectionResult, stage As String, cause As Exception,
                   rollbackCause As Exception, adapter As String, elapsed As TimeSpan)
        Me.Requested = requested
        Me.Active = RenderBackendStartup.Active
        Me.Result = result
        FailureStage = stage
        Me.Cause = cause
        Me.RollbackCause = rollbackCause
        Me.Adapter = adapter
        Me.Elapsed = elapsed
    End Sub
End Class

Friend Module RenderBackendSwitching
    Private ReadOnly gate As New Object()
    Private pending As TaskCompletionSource(Of BackendSelectionResult)
    Private pendingTarget As RenderBackend
    Private pendingHost As Form
    Private pendingHostDestroyed As EventHandler
    Private committed As HashSet(Of Control)
    Private _lastResult As BackendSwitchDiagnostics
    Private _backgroundPaused As Integer
    Private _inTransition As Boolean

    Friend ReadOnly Property IsPending As Boolean
        Get
            SyncLock gate
                Return pending IsNot Nothing
            End SyncLock
        End Get
    End Property

    Friend ReadOnly Property LastResult As BackendSwitchDiagnostics
        Get
            Return Volatile.Read(_lastResult)
        End Get
    End Property

    Friend ReadOnly Property BackgroundPaused As Boolean
        Get
            Return Volatile.Read(_backgroundPaused) <> 0
        End Get
    End Property

    Friend ReadOnly Property InTransition As Boolean
        Get
            Return _inTransition
        End Get
    End Property

    Friend Sub ObservePublication(owner As Control)
        committed?.Add(owner)
    End Sub

    Friend Function Request(target As RenderBackend) As Task(Of BackendSelectionResult)
        If Not [Enum].IsDefined(target) Then Throw New ArgumentOutOfRangeException(NameOf(target))
        SyncLock gate
            If pending IsNot Nothing Then
                If pendingTarget = target Then Return pending.Task
                Return Task.FromResult(BackendSelectionResult.SwitchUnavailable)
            End If
            If RenderBackendStartup.Active = target Then Return Task.FromResult(BackendSelectionResult.AlreadyActive)

            If Not RenderBackendStartup.IsSingleRenderThread() Then Return Task.FromResult(BackendSelectionResult.SwitchUnavailable)
            If Application.OpenForms.Cast(Of Form)().Any(Function(f) f.IsHandleCreated AndAlso f.InvokeRequired) Then Return Task.FromResult(BackendSelectionResult.SwitchUnavailable)
            Dim host = Application.OpenForms.Cast(Of Form)().FirstOrDefault(Function(f) f.IsHandleCreated AndAlso f.Visible)
            If host Is Nothing Then Return Task.FromResult(BackendSelectionResult.NoVisibleParticipants)
            Dim operation As New TaskCompletionSource(Of BackendSelectionResult)(TaskCreationOptions.RunContinuationsAsynchronously)
            pendingTarget = target
            pending = operation
            pendingHost = host
            pendingHostDestroyed = Sub(sender, args)
                                       If Not _inTransition Then FinishRequest(operation, BackendSelectionResult.NoVisibleParticipants)
                                   End Sub
            AddHandler host.HandleDestroyed, pendingHostDestroyed
            Try
                host.BeginInvoke(New MethodInvoker(Sub() ExecuteWhenIdle(host, target, operation)))
            Catch ex As InvalidOperationException
                FinishRequest(operation, BackendSelectionResult.NoVisibleParticipants)
            End Try
            Return operation.Task
        End SyncLock
    End Function

    Private Sub ExecuteWhenIdle(host As Form, target As RenderBackend, operation As TaskCompletionSource(Of BackendSelectionResult))
        If operation.Task.IsCompleted Then Return
        If host.IsDisposed OrElse Not host.IsHandleCreated Then
            FinishRequest(operation, BackendSelectionResult.NoVisibleParticipants)
            Return
        End If
        If D3D_RenderUpdate.IsActive OrElse D3D_RenderUpdate.IsCommitting OrElse D3D_RenderCore.HasActivePaint() Then
            Dim timer As New System.Windows.Forms.Timer With {.Interval = 15}
            AddHandler timer.Tick, Sub()
                                       timer.Dispose()
                                       ExecuteWhenIdle(host, target, operation)
                                   End Sub
            timer.Start()
            Return
        End If
        FinishRequest(operation, ExecuteSwitch(target))
    End Sub

    Private Sub FinishRequest(operation As TaskCompletionSource(Of BackendSelectionResult), result As BackendSelectionResult)
        SyncLock gate
            If pending IsNot operation Then Return
            RemoveHandler pendingHost.HandleDestroyed, pendingHostDestroyed
            pendingHost = Nothing
            pendingHostDestroyed = Nothing
            pending = Nothing
        End SyncLock
        operation.TrySetResult(result)
    End Sub

    Private Iterator Function Participants(root As Control) As IEnumerable(Of Control)
        If root.IsDisposed OrElse Not root.Visible Then Return
        If root.IsHandleCreated AndAlso root.ClientSize.Width > 0 AndAlso root.ClientSize.Height > 0 AndAlso D3D_V5Presentation.IsV5Control(root) Then Yield root
        For Each child As Control In root.Controls
            For Each participant In Participants(child)
                Yield participant
            Next
        Next
    End Function

    Private Function ExecuteSwitch(target As RenderBackend) As BackendSelectionResult
        Dim old = RenderBackendStartup.Active
        If Not old.HasValue Then Return BackendSelectionResult.Unavailable
        Dim hosts = Application.OpenForms.Cast(Of Form)().Where(Function(f) f.IsHandleCreated AndAlso f.Visible).ToArray()
        If hosts.Any(Function(f) f.InvokeRequired) OrElse Not RenderBackendStartup.IsSingleRenderThread() Then Return BackendSelectionResult.SwitchUnavailable
        Dim controls = hosts.SelectMany(Function(f) Participants(f)).Distinct().ToArray()
        If controls.Length = 0 Then Return BackendSelectionResult.NoVisibleParticipants
        Dim start = Stopwatch.GetTimestamp()
        Dim stage = "Candidate"
        Dim adapter As String = Nothing
        Dim failure As Exception = Nothing
        Dim rollbackFailure As Exception = Nothing
        Dim candidate12 As D3D12_DeviceManager = Nothing
        Dim target11Prepared As Boolean
        Dim retired As Boolean
        Dim installed As Boolean
        Dim result As BackendSelectionResult
        Try
            RenderBackendStartup.Preflight(target,
                Sub()
                    If target = RenderBackend.Direct3D12 Then
                        For Each host In hosts
                            D3D12StartupDispatch.CheckVisibleTree(host)
                        Next
                        candidate12 = New D3D12_DeviceManager()
                        candidate12.EnsureCreated()
                        candidate12.GetShapes()
                        candidate12.GetImages()
                        adapter = candidate12.AdapterDescription
                    Else
                        Dim manager = D3D_RenderCore.DeviceManager
                        manager.EnsureCreated()
                        target11Prepared = True
                        Using context = manager.CreateDeviceContext(), nativeAdapter = manager.DXGIDevice.GetAdapter()
                            adapter = nativeAdapter.Description.Description
                        End Using
                    End If
                End Sub)
            stage = "Drain"
            Volatile.Write(_backgroundPaused, 1)
            Drain(old.Value)
            stage = "Detach"
            retired = True
            Retire(old.Value)
            stage = "FirstFrame"
            _inTransition = True
            RenderBackendStartup.BeginTransition(target)
            For Each host In hosts
                ThisIsYourWindow.RefreshBackdropBackend(host)
            Next
            If candidate12 IsNot Nothing Then
                D3D12StartupDispatch.Install(candidate12)
                candidate12 = Nothing
            End If
            installed = True
            PublishAll(controls, target)
            result = BackendSelectionResult.Switched
        Catch ex As Exception
            failure = ex
            result = BackendSelectionResult.CandidateRejected
            If retired Then
                Try
                    _inTransition = True
                    If installed Then
                        If target = RenderBackend.Direct3D11 OrElse Not If(D3D12StartupDispatch.CurrentGroup?.IsDeviceRemoved, False) Then Drain(target)
                        Retire(target)
                    End If
                    RenderBackendStartup.BeginTransition(old.Value)
                    For Each host In hosts
                        ThisIsYourWindow.RefreshBackdropBackend(host)
                    Next
                    PublishAll(controls, old.Value)
                    result = BackendSelectionResult.RolledBack
                Catch rollback As Exception
                    rollbackFailure = rollback
                    RenderBackendStartup.Failed(rollback)
                    result = BackendSelectionResult.Unavailable
                End Try
            ElseIf target11Prepared Then
                D3D_RenderCore.InvalidateDeviceForCleanup()
            End If
        Finally
            candidate12?.Dispose()
            committed = Nothing
            _inTransition = False
            Volatile.Write(_backgroundPaused, 0)
            D3D_BackdropSurfaceRenderer.ReplayStartupRequests()
        End Try
        Volatile.Write(_lastResult, New BackendSwitchDiagnostics(target, result, If(failure Is Nothing, Nothing, stage), failure,
                                                                 rollbackFailure, adapter, Stopwatch.GetElapsedTime(start)))
        Return result
    End Function

    Friend Sub RecoverDx12()
        Dim controls = Application.OpenForms.Cast(Of Form)().Where(Function(f) Not f.InvokeRequired).SelectMany(Function(f) Participants(f)).Distinct().ToArray()
        _inTransition = True
        Try
            RenderBackendStartup.BeginTransition(RenderBackend.Direct3D12)
            D3D12StartupDispatch.ReleaseBackend()
            If controls.Length > 0 Then PublishAll(controls, RenderBackend.Direct3D12)
        Catch ex As Exception
            RenderBackendStartup.Failed(ex)
        Finally
            committed = Nothing
            _inTransition = False
        End Try
    End Sub

    Private Sub PublishAll(controls As Control(), target As RenderBackend)
        committed = New HashSet(Of Control)()
        For Each control In controls
            D3D_V5Presentation.RequestRenderBatched(control)
        Next
        D3D_V5Presentation.FlushPendingFrame(prepareSurfaces:=True)
        If RenderBackendStartup.Failure IsNot Nothing Then Throw RenderBackendStartup.Failure
        Dim missing = controls.FirstOrDefault(Function(c) c.Visible AndAlso Not c.IsDisposed AndAlso Not committed.Contains(c))
        If missing IsNot Nothing OrElse committed.Count = 0 Then Throw New InvalidOperationException($"Missing switched frame from {missing?.GetType().Name}.")
        RenderBackendStartup.PublishedFrame(target, transitionComplete:=True)
    End Sub

    Private Sub Drain(backend As RenderBackend)
        D3D_BackdropSurfaceRenderer.DrainWorkersForSwitch()
        If backend = RenderBackend.Direct3D12 Then
            D3D12StartupDispatch.DrainForSwitch()
        Else
            D3D_DeviceGlobals.DrainForSwitch()
            D3D_RenderCore.DeviceManager.DrainForSwitch()
        End If
    End Sub

    Private Sub Retire(backend As RenderBackend)
        If backend = RenderBackend.Direct3D12 Then
            D3D12StartupDispatch.ReleaseBackend()
        Else
            D3D_V5Presentation.DetachDx11ForSwitch()
            D3D_PaintBridge.CleanupD2DResources(D3DCacheCleanupLevel.ReleaseEverything)
        End If
    End Sub
End Module
