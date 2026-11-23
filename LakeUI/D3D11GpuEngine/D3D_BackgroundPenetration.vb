''' <summary>
''' V5 背景来源注册适配层。实际像素采样统一由 <see cref="D3D_ControlSurfaceRegistry"/> 完成，
''' 本模块不创建 CPU backing bitmap、GDI DC 或独立的 D2D 上传缓存。
''' </summary>
Public Module D3D_BackgroundPenetration
    ''' <summary>记录控件的显式背景来源并使控件表面失效。</summary>
    Public Function SetBackgroundSource(owner As Control, oldSource As Control, newSource As Control) As Control
        If owner IsNot Nothing Then D3D_ControlSurfaceRegistry.BackgroundSourceChanged(owner)
        Return newSource
    End Function

    ''' <summary>记录转发控件的背景来源；实际坐标依赖在绘制时建立。</summary>
    Public Function SetConsumerSource(child As Control, oldSource As Control, newSource As Control) As Control
        If child IsNot Nothing Then D3D_ControlSurfaceRegistry.BackgroundSourceChanged(child)
        Return newSource
    End Function

    ''' <summary>使背景来源的 GPU 表面失效，并传播到依赖消费者。</summary>
    Public Sub Invalidate(source As Control)
        D3D_ControlSurfaceRegistry.MarkDirty(source)
    End Sub

    ''' <summary>使背景来源的指定区域失效。</summary>
    Public Sub Invalidate(source As Control, dirtyRect As Rectangle)
        D3D_ControlSurfaceRegistry.MarkDirty(source, dirtyRect)
    End Sub

    ''' <summary>背景转发拓扑变化时使控件重新解析来源。</summary>
    Public Sub InvalidateForwarderTopology(forwarder As Control)
        If forwarder Is Nothing Then Return
        If RenderBackendStartup.Requested = RenderBackend.Direct3D12 Then
            D3D12_BackgroundDependencies.SourceChanged(forwarder)
        Else
            D3D_ControlSurfaceRegistry.PresentationModeChanged(forwarder)
        End If
    End Sub

    ''' <summary>移除控件的背景依赖；V5 注册表会在下次绘制时按现状重建依赖。</summary>
    Public Sub UnregisterConsumer(child As Control, Optional source As Control = Nothing)
        If child IsNot Nothing Then D3D_ControlSurfaceRegistry.UnregisterConsumer(child, source)
    End Sub

    ''' <summary>移除宿主及其子树的背景依赖。</summary>
    Public Sub UnregisterBackgroundConsumer(owner As Control)
        If owner IsNot Nothing Then D3D_ControlSurfaceRegistry.UnregisterConsumer(owner)
    End Sub

    Friend Sub CleanupD2DResources(level As D3DCacheCleanupLevel, Optional owner As Control = Nothing)
        If level >= D3DCacheCleanupLevel.ReleaseEverything Then
            If owner Is Nothing Then
                D3D_ControlSurfaceRegistry.HandleDeviceLost()
            Else
                D3D_ControlSurfaceRegistry.MarkDirty(owner, requestConsumers:=False)
            End If
        End If
    End Sub
End Module
