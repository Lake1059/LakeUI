''' <summary>
''' D3D_DirectCompositionHost 保留新核心 DirectComposition 呈现路线的宿主边界。
''' 本阶段可验证路线优先使用 D3D_SwapChainHost 的 HWND swap chain；该类不创建 HDC 回退、不接入 Demo，也不混入旧 D3D_SurfaceCompositor。
''' 后续若引入 Vortice.DirectComposition 或原生 COM wrapper，资源生命周期必须绑定 D3D_DeviceManager generation，并由 D3D_WindowCompositor 调用 Commit。
''' </summary>
Public NotInheritable Class D3D_DirectCompositionHost
    Implements IDisposable

    Private _disposed As Boolean

    Public ReadOnly Property IsAvailable As Boolean
        Get
            Return False
        End Get
    End Property

    ''' <summary>
    ''' DirectComposition Commit 入口。当前 swap chain 路线不需要调用；控件 RenderGpu 永远不能直接 Commit。
    ''' </summary>
    Public Sub Commit()
        If _disposed Then Throw New ObjectDisposedException(NameOf(D3D_DirectCompositionHost))
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        GC.SuppressFinalize(Me)
    End Sub
End Class
