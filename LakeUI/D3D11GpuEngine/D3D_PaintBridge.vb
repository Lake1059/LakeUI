Public Enum D3DCacheCleanupLevel
    TrimToBudget = 0
    ReleaseVolatileCaches = 1
    ReleaseAllCaches = 2
    ReleaseRenderTargets = 3
    RecreateDevice = 4
    ReleaseEverything = 5
End Enum

Public Module D3D_PaintBridge
    ''' <summary>
    ''' 合并 UI 线程上的初始化和布局变更，须在 Await 前释放。
    ''' 嵌套作用域由最外层统一交给现有从外到内调度器提交。
    ''' </summary>
    Public Function BeginRenderUpdate(root As Control) As IDisposable
        Return D3D_RenderUpdate.Begin(root)
    End Function

    ''' <summary>
    ''' 识别设计器及其子树（包括尚未分配 Site 的嵌套控件）。
    ''' </summary>
    Friend Function IsDesignTimeControl(control As Control) As Boolean
        If System.ComponentModel.LicenseManager.UsageMode = System.ComponentModel.LicenseUsageMode.Designtime Then Return True
        Dim current = control
        While current IsNot Nothing
            Try
                If current.Site IsNot Nothing AndAlso current.Site.DesignMode Then Return True
            Catch
            End Try
            current = current.Parent
        End While
        Return False
    End Function

    Public Sub InvalidateTextFormatCache(control As Control)
        D3D_RenderCore.InvalidateExistingTextResources(control)
    End Sub

    ''' <summary>
    ''' 开关 V5 运行时探针。启用后会记录背景映射和顶层 chrome 的真实状态，
    ''' 供自动化验收读取；不依赖截图或 WM_PAINT 时序。
    ''' </summary>
    Public Property V5ProbeEnabled As Boolean
        Get
            Return D3D_RenderDiagnostics.Enabled
        End Get
        Set(value As Boolean)
            D3D_RenderDiagnostics.Enabled = value
        End Set
    End Property

    Public Function GetV5ProbeSnapshot() As D3D_V5ProbeSnapshot
        Return D3D_RenderDiagnostics.GetV5ProbeSnapshot()
    End Function

    Public Sub ResetV5Probe()
        D3D_RenderDiagnostics.Reset()
    End Sub

    Public Function GetV5RefreshTimings() As D3D_RefreshTiming()
        Return D3D_RefreshDiagnostics.Snapshot()
    End Function

    Public Sub SetV5CrossFormProbePair(consumer As Control, source As Control)
        D3D_RenderDiagnostics.SetV5CrossFormProbePair(consumer, source)
    End Sub

    Public Sub RefreshFontDependentRendering(control As Control,
                                              Optional invalidateChildren As Boolean = True,
                                              Optional immediate As Boolean = True)
        InvalidateTextFormatCache(control)
        If control Is Nothing OrElse control.IsDisposed Then Return
        OuterToInnerRefreshScheduler.RequestFull(control, invalidateChildren, immediate)
    End Sub

    Friend Function IsPainting(control As Control) As Boolean
        Return D3D_RenderCore.HasActivePaint(control)
    End Function

    Public Function CleanupD2DResources(level As D3DCacheCleanupLevel,
                                        Optional owner As Control = Nothing,
                                        Optional invalidateAfterCleanup As Boolean = False) As Integer
        ' 设备由进程内所有窗口共享；重建设备及以上级别不能只恢复 owner 窗体。
        Dim targetForm = If(level >= D3DCacheCleanupLevel.RecreateDevice, Nothing, D3D_RenderCore.ResolveCompositorForm(owner))
        Dim shouldRecover = invalidateAfterCleanup OrElse level >= D3DCacheCleanupLevel.RecreateDevice
        Dim recoveryForms = If(shouldRecover,
                               D3D_RenderCore.GetCleanupRecoveryForms(targetForm),
                               Array.Empty(Of Form)())
        Dim hasActivePaint = D3D_RenderCore.HasActivePaint(targetForm)
        If hasActivePaint Then
            Dim 恢复窗体 = D3D_RenderCore.GetCleanupRecoveryForms(targetForm).FirstOrDefault()
            If 恢复窗体 IsNot Nothing AndAlso 恢复窗体.IsHandleCreated Then
                Try
                    恢复窗体.BeginInvoke(CType(Sub() CleanupD2DResources(level, owner, invalidateAfterCleanup), MethodInvoker))
                Catch 异常 As InvalidOperationException
                    System.Diagnostics.Debug.WriteLine(异常)
                End Try
            End If
            Return 0
        End If
        Dim cleaned = D3D_RenderCore.CleanupD2DResources(level, targetForm, invalidateAfterCleanup:=False)

        If Not hasActivePaint Then
            D3D_BackgroundPenetration.CleanupD2DResources(level, targetForm)
            D3D_BackdropSurfaceRenderer.CleanupAllD2DResources(level, targetForm)
            MarkdownViewerCore.CleanupAllD2DResources(level, targetForm)

            If level = D3DCacheCleanupLevel.TrimToBudget Then
                D3D_CpuCache.TrimToBudget(immediate:=True)
                D3D_GpuCache.TrimToBudget(immediate:=True)
            ElseIf level = D3DCacheCleanupLevel.ReleaseEverything Then
                D3D_GpuCache.ReleaseAll()
            End If

            If level >= D3DCacheCleanupLevel.RecreateDevice Then
                D3D_RenderCore.InvalidateDeviceForCleanup()
            End If
            If targetForm Is Nothing Then
                D3D_D2DInterop.CleanupD2DResources(level)
            End If
        End If

        If shouldRecover Then D3D_RenderCore.QueueCleanupRecovery(recoveryForms)

        Return cleaned
    End Function

    Public Function ResetRenderCore(Optional owner As Control = Nothing,
                                    Optional invalidateAfterCleanup As Boolean = False) As Integer
        Return CleanupD2DResources(D3DCacheCleanupLevel.ReleaseEverything, owner, invalidateAfterCleanup)
    End Function

    Public Function ReleaseImageD2DCache(image As Image,
                                         Optional owner As Control = Nothing,
                                         Optional invalidateAfterCleanup As Boolean = False) As Integer
        Return D3D_RenderCore.ReleaseImageCache(image, owner, invalidateAfterCleanup)
    End Function

    ''' <summary>
    ''' 将 WinForms 的绘制通知转发到 V5 控件表面。
    ''' </summary>
    ''' <param name="e">当前 WM_PAINT 参数；仅用于保持 OnPaint 签名，不读取其 Graphics。</param>
    ''' <param name="control">需要呈现的控件，必须已创建句柄且尺寸大于零。</param>
    ''' <param name="renderable">实现 <see cref="D3D_IGpuRenderable"/> 的控件绘制契约。</param>
    ''' <returns>控件由 V5 提交时返回 <c>True</c>；非 V5 控件返回 <c>False</c>。</returns>
    Public Function PaintRenderable(e As PaintEventArgs,
                                    control As Control,
                                    renderable As D3D_IGpuRenderable) As Boolean
        If e Is Nothing OrElse control Is Nothing OrElse renderable Is Nothing Then Return False
        If control.IsDisposed OrElse control.Width <= 0 OrElse control.Height <= 0 Then Return False
        ' 设计器必须保留 WinForms 的默认绘制和设计时装饰层（选中边框、调整手柄）。
        ' 控件外观由项目配置的后端预览，同时让调用方继续执行 MyBase.OnPaint。
        If IsDesignTimeControl(control) Then
            Try
                If D3D_V5Presentation.IsV5Control(control) AndAlso control.IsHandleCreated AndAlso
                   D3D_ControlTreeWalker.IsEffectivelyVisible(control) AndAlso RenderBackendStartup.TryStartDesignerPreview() Then
                    D3D_V5Presentation.Paint(control, renderable, control.ClientRectangle)
                End If
            Catch ex As Exception
                System.Diagnostics.Debug.WriteLine(ex)
            End Try
            Return False
        End If
        If Not D3D_V5Presentation.IsV5Control(control) Then Return False
        Return D3D_V5Presentation.Paint(control, renderable, dirtyRect:=e.ClipRectangle)
    End Function

    ''' <summary>
    ''' 在 V5 表面最上层绘制设计器选中线框和调整手柄。
    ''' 交换链覆盖 WinForms GDI，所以装饰必须属于同一 GPU 帧。
    ''' </summary>
    Friend Sub 绘制设计时选择装饰(绘制上下文 As D3D_PaintContext, 控件 As Control)
        ' SSAA 下反算线宽，保持呈现后的线框为 1 像素。
        Dim 采样倍率 = Math.Max(1.0F, Math.Abs(绘制上下文.LocalToWindowTransform.M11))
        绘制设计时选择装饰(控件, 1.0F / 采样倍率, AddressOf 绘制上下文.FillRectangle, AddressOf 绘制上下文.DrawRectangle)
    End Sub

    Friend Sub 绘制设计时选择装饰(绘制上下文 As D3D12_PaintContext, 控件 As Control)
        绘制设计时选择装饰(控件, 1.0F, AddressOf 绘制上下文.FillRectangle, AddressOf 绘制上下文.DrawRectangle)
    End Sub

    Private Sub 绘制设计时选择装饰(控件 As Control, 物理一像素 As Single,
                                  填充 As Action(Of RectangleF, Color), 描边 As Action(Of RectangleF, Color, Single))
        If 控件.Site Is Nothing Then Return
        Dim 选择服务 As System.ComponentModel.Design.ISelectionService = Nothing
        Try
            选择服务 = TryCast(控件.Site.GetService(GetType(System.ComponentModel.Design.ISelectionService)),
                              System.ComponentModel.Design.ISelectionService)
        Catch
            Return
        End Try
        If 选择服务 Is Nothing OrElse
           (Not 选择服务.GetComponentSelected(控件) AndAlso
            Not Object.ReferenceEquals(选择服务.PrimarySelection, 控件)) Then Return

        Dim 宽度 = 控件.ClientSize.Width
        Dim 高度 = 控件.ClientSize.Height
        If 宽度 <= 1 OrElse 高度 <= 1 Then Return

        Dim 边界 As New RectangleF(0.5F * 物理一像素,
                                  0.5F * 物理一像素,
                                  Math.Max(物理一像素, 宽度 - 物理一像素),
                                  Math.Max(物理一像素, 高度 - 物理一像素))
        Dim 线框颜色 = Color.FromArgb(235, 30, 110, 220)
        描边(边界, 线框颜色, 物理一像素)

        ' Dock=Fill 没有可调整的尺寸，仍显示选中线框但不绘制手柄。
        If 控件.Dock = DockStyle.Fill Then Return

        Dim 手柄尺寸 = Math.Min(6.0F * 物理一像素,
                                Math.Min(Math.Max(物理一像素, 宽度 - 2.0F * 物理一像素),
                                         Math.Max(物理一像素, 高度 - 2.0F * 物理一像素)))
        If 手柄尺寸 <= 物理一像素 Then Return
        Dim 半尺寸 = 手柄尺寸 / 2.0F
        ' 手柄完全位于线框内壁，不再像设计器原生 glyph 一样向外溢出。
        Dim 内左 = 边界.Left + 0.5F * 物理一像素
        Dim 内上 = 边界.Top + 0.5F * 物理一像素
        Dim 内右 = 边界.Right - 0.5F * 物理一像素
        Dim 内下 = 边界.Bottom - 0.5F * 物理一像素
        Dim 中心X = (内左 + 内右) / 2.0F
        Dim 中心Y = (内上 + 内下) / 2.0F

        Dim 显示位置 As New HashSet(Of String)(StringComparer.Ordinal)
        Select Case 控件.Dock
            Case DockStyle.Top
                显示位置.UnionWith({"下中"})
            Case DockStyle.Bottom
                显示位置.UnionWith({"上中"})
            Case DockStyle.Left
                显示位置.UnionWith({"右中"})
            Case DockStyle.Right
                显示位置.UnionWith({"左中"})
            Case Else
                显示位置.UnionWith({"左上", "上中", "右上", "左中", "右中", "左下", "下中", "右下"})
        End Select

        Dim 点列 = {内左 + 半尺寸, 中心X, 内右 - 半尺寸}
        Dim 点行 = {内上 + 半尺寸, 中心Y, 内下 - 半尺寸}
        Dim 位置名称 = New String(,) {{"左上", "上中", "右上"}, {"左中", "中", "右中"}, {"左下", "下中", "右下"}}
        For 行 As Integer = 0 To 2
            For 列 As Integer = 0 To 2
                Dim 名称 = 位置名称(行, 列)
                If 名称 = "中" OrElse Not 显示位置.Contains(名称) Then Continue For
                Dim 手柄 As New RectangleF(点列(列) - 半尺寸, 点行(行) - 半尺寸, 手柄尺寸, 手柄尺寸)
                填充(手柄, Color.White)
                描边(手柄, 线框颜色, 物理一像素)
            Next
        Next
    End Sub

    Friend Sub 绘制设计时选择装饰(图形 As Graphics, 控件 As Control)
        If 图形 Is Nothing OrElse 控件 Is Nothing OrElse 控件.Site Is Nothing Then Return
        Dim 选择服务 As System.ComponentModel.Design.ISelectionService = Nothing
        Try
            选择服务 = TryCast(控件.Site.GetService(GetType(System.ComponentModel.Design.ISelectionService)),
                              System.ComponentModel.Design.ISelectionService)
        Catch
            Return
        End Try
        If 选择服务 Is Nothing Then Return
        If Not 选择服务.GetComponentSelected(控件) AndAlso
           Not Object.ReferenceEquals(选择服务.PrimarySelection, 控件) Then Return

        Dim 边界 = New Rectangle(0, 0, Math.Max(0, 控件.ClientSize.Width - 1), Math.Max(0, 控件.ClientSize.Height - 1))
        If 边界.Width <= 0 OrElse 边界.Height <= 0 Then Return
        Using 虚线笔 As New Pen(Color.FromArgb(30, 110, 220), 1.0F) With {
            .DashStyle = System.Drawing.Drawing2D.DashStyle.Dash}
            图形.DrawRectangle(虚线笔, 边界)
        End Using

        Const 手柄尺寸 As Integer = 6
        Dim 半尺寸 = 手柄尺寸 \ 2
        Dim 中心X = 边界.Left + 边界.Width \ 2
        Dim 中心Y = 边界.Top + 边界.Height \ 2
        Dim 点列 = {边界.Left, 中心X, 边界.Right}
        Dim 点行 = {边界.Top, 中心Y, 边界.Bottom}
        Using 填充笔 As New SolidBrush(Color.White), 边框笔 As New Pen(Color.FromArgb(30, 110, 220), 1.0F)
            For 行 As Integer = 0 To 2
                For 列 As Integer = 0 To 2
                    If 行 = 1 AndAlso 列 = 1 Then Continue For
                    Dim 手柄 = New Rectangle(点列(列) - 半尺寸, 点行(行) - 半尺寸, 手柄尺寸, 手柄尺寸)
                    图形.FillRectangle(填充笔, 手柄)
                    图形.DrawRectangle(边框笔, 手柄)
                Next
            Next
        End Using
    End Sub

End Module
