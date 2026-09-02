Imports Vortice.Direct2D1

''' <summary>
''' V5 GPU 表面注册表及 BackgroundSource 解析器。实现 V5 标记的控件来源，
''' 以及可由渲染器直接表达自身背景的宿主 Form，都会生成可采样 GPU 表面；
''' 其他原生或 CPU 来源和未迁移的 GPU 来源不会被隐式截屏或回退。
''' </summary>
Friend NotInheritable Class D3D_ControlSurfaceRegistry
    Private NotInheritable Class Entry
        Public ReadOnly Surface As D3D_ControlSurface
        Public ReadOnly Owner As Control
        Public Dirty As Boolean = True
        Public Rendering As Boolean
        Public PendingDirty As Rectangle = Rectangle.Empty
        Public Revision As Long
        Public LastUsed As Long

        Public Sub New(owner As Control)
            Me.Owner = owner
            Surface = New D3D_ControlSurface(owner, D3D_RenderCore.DeviceManager)
        End Sub
    End Class

    Private Shared ReadOnly _entries As New Dictionary(Of Control, Entry)()
    Private Shared ReadOnly _consumers As New Dictionary(Of Control, HashSet(Of Control))()
    Private Shared ReadOnly _consumerSources As New Dictionary(Of Control, HashSet(Of Control))()
    Private Shared ReadOnly _dependencySourceRects As New Dictionary(Of Control, Dictionary(Of Control, RectangleF))()
    Private Shared ReadOnly _coordinateConsumers As New Dictionary(Of Control, HashSet(Of Control))()
    Private Shared ReadOnly _consumerCoordinateControls As New Dictionary(Of Control, HashSet(Of Control))()
    Private Shared _frameGeneration As Integer
    <ThreadStatic>
    Private Shared _当前渲染控件 As HashSet(Of Control)

    Private Sub New()
    End Sub

    Friend Shared Function NextFrameGeneration() As Integer
        Return Threading.Interlocked.Increment(_frameGeneration)
    End Function

    Friend Shared Function GetAllocatedSurfaceBytes() As Long
        Dim 总字节数 As Long
        For Each 项目 In _entries.Values
            If 项目 IsNot Nothing AndAlso 项目.Surface IsNot Nothing Then
                总字节数 += Math.Max(0L, 项目.Surface.AllocatedBytes)
            End If
        Next
        Return Math.Max(0L, 总字节数)
    End Function

    Friend Shared Function RenderControl(control As Control, renderable As D3D_IGpuRenderable,
                                         Optional requestedDirty As Rectangle = Nothing,
                                         Optional 绘制后处理 As Action(Of D3D_PaintContext) = Nothing) As D3D_ControlSurface
        Dim 项目 = 获取或创建项目(control)
        If 项目 Is Nothing Then Return Nothing
        If Not 项目.Dirty AndAlso 项目.Surface.Bitmap IsNot Nothing Then Return 项目.Surface
        If requestedDirty.Width > 0 AndAlso requestedDirty.Height > 0 Then
            项目.PendingDirty = 合并脏区(项目.PendingDirty, requestedDirty, control.Size)
        End If
        ' 失效事件提交 V5 帧之后仍可能收到 WM_PAINT。
        ' 保留持久表面，避免在绘制回调中重复构建同一帧。
        If Not 渲染项目(control, 项目, renderable, 绘制后处理) Then Return Nothing
        ' 表面提交属于动画热路径。预算维护由资源新增和显式清理入口负责，
        ' 这里不能同步扫描所有 GPU owner，否则多个控件动画会周期性停顿。
        Return 项目.Surface
    End Function

    Friend Shared Function TryDrawBackground(consumer As Control,
                                             source As Control,
                                             context As D3D_PaintContext,
                                             destination As RectangleF,
                                             Optional renderSourceIfDirty As Boolean = True,
                                             Optional registerDependency As Boolean = True) As Boolean
        If consumer Is Nothing OrElse source Is Nothing OrElse context Is Nothing Then Return False
        If consumer.IsDisposed OrElse source.IsDisposed Then Return False
        ' 强制约束：背景来源只能是外层容器已经完成或正在按外到内顺序准备的表面。
        ' 禁止在子控件绘制阶段反向遍历兄弟树或重新提交当前容器。
        D3D_RenderDiagnostics.V5BackdropAttempt()
        D3D_RenderDiagnostics.V5CrossFormBackdropAttempt(consumer, source)
        ' 控件不能采样自身，也不能采样显式来源链最终指回消费者的来源。
        ' 这种循环会留下过期或空白表面，并使背景显示在无关的偏移位置。
        If 形成背景循环(consumer, source) Then
            D3D_RenderDiagnostics.V5BackdropCycleReject()
            Return False
        End If
        Dim 可渲染对象 = TryCast(source, D3D_IGpuRenderable)
        If 可渲染对象 Is Nothing AndAlso TypeOf source Is Form Then
            可渲染对象 = New NativeFormSurfaceRenderable(DirectCast(source, Form))
        End If
        If 可渲染对象 Is Nothing Then
            D3D_RenderDiagnostics.V5BackdropNonV5Reject()
            D3D_RenderDiagnostics.V5BackdropSurfaceReject()
            D3D_RenderDiagnostics.V5CrossFormBackdropSurfaceReject(consumer, source)
            Return False
        End If

        Dim 项目 = 获取或创建项目(source)
        If 项目 Is Nothing Then
            D3D_RenderDiagnostics.V5BackdropSurfaceReject()
            D3D_RenderDiagnostics.V5CrossFormBackdropSurfaceReject(consumer, source)
            Return False
        End If
        ' 即使来源暂时没有句柄或位图，或者位于消费者范围外，也要保留逻辑依赖。
        ' 后续创建句柄、可见性和几何变化必须能够唤醒消费者并重试显式映射。
        If registerDependency Then 注册依赖(consumer, source, RectangleF.Empty)
        ' 来源正在当前渲染链中时只能使用已完成的旧表面；禁止再次进入其 RenderGpu，
        ' 否则父子控件的自动背景映射会形成递归。
        If 项目.Rendering Then Return False
        D3D_RenderDiagnostics.V5CrossFormSourceState(consumer, source,
                                                     项目.Dirty,
                                                     项目.Rendering,
                                                     项目.Surface.Bitmap IsNot Nothing)
        If renderSourceIfDirty AndAlso (项目.Dirty OrElse 项目.Surface.Bitmap Is Nothing) AndAlso Not 项目.Rendering Then
            ' 显式来源允许先准备外层表面；自动父级映射通过 registerDependency:=False
            ' 进入同一入口时不得建立反向布局依赖，避免重绘顺序被事件打乱。
            渲染项目(source, 项目, 可渲染对象)
        End If
        Dim 位图 = 项目.Surface.Bitmap
        D3D_RenderDiagnostics.V5CrossFormSourceState(consumer, source,
                                                     项目.Dirty,
                                                     项目.Rendering,
                                                     位图 IsNot Nothing)
        If 位图 Is Nothing Then
            D3D_RenderDiagnostics.V5BackdropSurfaceReject()
            D3D_RenderDiagnostics.V5CrossFormBackdropSurfaceReject(consumer, source)
            Return False
        End If

        Dim 偏移 As Point
        Try
            偏移 = source.PointToClient(consumer.PointToScreen(Point.Empty))
        Catch
            Return False
        End Try

        Dim 来源边界 As New RectangleF(0, 0, 项目.Surface.LogicalSize.Width, 项目.Surface.LogicalSize.Height)
        Dim 请求来源区域 As New RectangleF(偏移.X + destination.X, 偏移.Y + destination.Y, destination.Width, destination.Height)
        Dim 裁剪来源区域 = RectangleF.Intersect(来源边界, 请求来源区域)
        If 裁剪来源区域.Width <= 0 OrElse 裁剪来源区域.Height <= 0 Then
            D3D_RenderDiagnostics.V5BackdropSurfaceReject()
            D3D_RenderDiagnostics.V5CrossFormBackdropCoordinateReject(consumer, source)
            Return False
        End If

        If registerDependency Then 注册依赖(consumer, source, 裁剪来源区域)

        If Not 来源边界.Contains(请求来源区域) Then
            Dim 后备来源 = 查找最近GPU祖先(consumer)
            If 后备来源 IsNot Nothing AndAlso Not ReferenceEquals(后备来源, source) Then
                TryDrawBackground(consumer, 后备来源, context, destination)
            ElseIf consumer.BackColor.A > 0 Then
                context.FillRectangle(destination, consumer.BackColor)
            End If
        End If

        Dim 绘制目标 As New RectangleF(
            destination.X + 裁剪来源区域.X - 请求来源区域.X,
            destination.Y + 裁剪来源区域.Y - 请求来源区域.Y,
            裁剪来源区域.Width,
            裁剪来源区域.Height)
        Dim 采样倍率 = 项目.Surface.SampleScale
        Dim 物理来源区域 As New Vortice.RawRectF(
            裁剪来源区域.Left * 采样倍率,
            裁剪来源区域.Top * 采样倍率,
            裁剪来源区域.Right * 采样倍率,
            裁剪来源区域.Bottom * 采样倍率)
        项目.Surface.BeginResourceUse()
        Try
            context.DeviceContext.DrawBitmap(
                位图,
                D3D_PaintContext.ToRawRect(绘制目标),
                1.0F,
                InterpolationMode.Linear,
                物理来源区域,
                Nothing)
        Finally
            项目.Surface.EndResourceUse()
        End Try
        D3D_RenderDiagnostics.V5BackdropSuccess(consumer, source, 偏移, 裁剪来源区域, 绘制目标)
        Return True
    End Function

    Friend Shared Function TryDrawAutomaticGpuBackdrop(owner As Control,
                                                       context As D3D_PaintContext,
                                                       Optional ignoreExplicitSource As Boolean = False) As Boolean
        If owner Is Nothing OrElse context Is Nothing Then Return False
        ' 强制约束：自动背景只允许沿 Parent 向外查找，父级完成后再由子级采样；
        ' 这里绝不能主动绘制兄弟或子树，否则会破坏外到内顺序并造成递归。
        ' 只有透明或半透明控件需要向父级查找背景。完全不透明的控件
        ' 自身已经覆盖整个客户区，跳过父级和同级遍历可避免启动阶段重复合成。
        If Not ignoreExplicitSource AndAlso owner.BackColor.A >= 255 Then Return False
        If Not ignoreExplicitSource Then
            Dim 来源提供者 = TryCast(owner, D3D_IBackgroundSourceProvider)
            Dim 显式来源 As Control = Nothing
            If 来源提供者 IsNot Nothing AndAlso 来源提供者.TryGetBackgroundSource(显式来源) AndAlso 显式来源 IsNot Nothing Then Return False
        End If

        Dim 来源 = 查找最近GPU祖先(owner)
        If 来源 Is Nothing Then Return False
        Dim 目标区域 As New RectangleF(0, 0, owner.Width, owner.Height)
        ' V3 的 RenderGpu 只负责当前控件自身；背景来源只采样已完成的父级表面。
        ' 不在这里主动渲染兄弟或子树，避免启动阶段递归和重复重绘。
        Return TryDrawBackground(owner, 来源, context, 目标区域,
                                 renderSourceIfDirty:=True,
                                 registerDependency:=False)
    End Function

    Friend Shared Sub DrawAutomaticGpuBackdrop(owner As Control, context As D3D_PaintContext)
        ' 自动背景不应只依赖直接父级：WinForms 中经常会在 GPU 容器与控件之间
        ' 插入普通 Panel/UserControl，ModernTabListControl 也有自己的透明内容面板。
        ' 沿祖先链寻找最近的 V5 来源，既保持视觉层级，又避免回退到 CPU 截屏路径。
        TryDrawAutomaticGpuBackdrop(owner, context)
    End Sub

    Friend Shared Function ResolveNearestGpuAncestor(owner As Control) As Control
        Return 查找最近GPU祖先(owner)
    End Function

    Private Shared Function 查找最近GPU祖先(所有者 As Control) As Control
        If 所有者 Is Nothing Then Return Nothing
        Dim 当前控件 = 所有者.Parent
        While 当前控件 IsNot Nothing
            If Not 当前控件.IsDisposed AndAlso
               ((TypeOf 当前控件 Is V5_IGpuPresentationSource AndAlso
                 TryCast(当前控件, D3D_IGpuRenderable) IsNot Nothing) OrElse
                TypeOf 当前控件 Is Form) AndAlso
               Not 正在渲染(当前控件) AndAlso
               Not 形成背景循环(所有者, 当前控件) Then
                Return 当前控件
            End If
            当前控件 = 当前控件.Parent
        End While
        Return Nothing
    End Function

    Private Shared Function 形成背景循环(消费者 As Control, 来源 As Control) As Boolean
        If 消费者 Is Nothing OrElse 来源 Is Nothing Then Return False
        If ReferenceEquals(消费者, 来源) Then Return True

        ' 只沿显式提供者链接检查。自动祖先链接按结构不会形成循环，
        ' 显式链接则可能在同级或嵌套控件之间形成循环。
        Dim 已访问控件 As New HashSet(Of Control)()
        Dim 当前控件 As Control = 来源
        For 检查次数 As Integer = 0 To 64
            If 当前控件 Is Nothing Then Return False
            If Not 已访问控件.Add(当前控件) Then Return True
            If ReferenceEquals(当前控件, 消费者) Then Return True
            Dim 来源提供者 = TryCast(当前控件, D3D_IBackgroundSourceProvider)
            If 来源提供者 Is Nothing Then Return False
            Dim 下一来源 As Control = Nothing
            If Not 来源提供者.TryGetBackgroundSource(下一来源) OrElse 下一来源 Is Nothing Then Return False
            当前控件 = 下一来源
        Next
        Return True
    End Function

    Friend Shared Sub MarkDirty(control As Control,
                                Optional dirtyRect As Rectangle = Nothing,
                                Optional requestConsumers As Boolean = True)
        If control Is Nothing Then Return
        Dim 项目 As Entry = Nothing
        If _entries.TryGetValue(control, 项目) Then
            项目.Dirty = True
            Dim 边界 = dirtyRect
            If 边界.Width <= 0 OrElse 边界.Height <= 0 Then 边界 = New Rectangle(Point.Empty, control.Size)
            项目.PendingDirty = 合并脏区(项目.PendingDirty, 边界, control.Size)
        End If
        If Not requestConsumers Then Return

        Dim 规范脏区 = dirtyRect
        If 规范脏区.Width <= 0 OrElse 规范脏区.Height <= 0 Then 规范脏区 = New Rectangle(Point.Empty, control.Size)
        请求依赖消费者(control, 规范脏区)
    End Sub

    Private Shared Sub 请求依赖消费者(来源 As Control, 脏区 As Rectangle)
        Dim 目标集合 As HashSet(Of Control) = Nothing
        If 来源 Is Nothing OrElse Not _consumers.TryGetValue(来源, 目标集合) Then Return
        For Each 消费者 In 目标集合.ToArray()
            If 消费者 Is Nothing OrElse 消费者.IsDisposed Then Continue For
            Dim 来源区域 As RectangleF = RectangleF.Empty
            Dim 按来源区域 As Dictionary(Of Control, RectangleF) = Nothing
            If _dependencySourceRects.TryGetValue(消费者, 按来源区域) Then 按来源区域.TryGetValue(来源, 来源区域)
            If 来源区域.Width > 0 AndAlso 来源区域.Height > 0 AndAlso
               Not 来源区域.IntersectsWith(New RectangleF(脏区.X, 脏区.Y, 脏区.Width, 脏区.Height)) Then Continue For
            D3D_RenderDiagnostics.V5DependencyInvalidation()
            D3D_V5Presentation.RequestRender(消费者)
        Next
    End Sub

    Friend Shared Sub BackgroundSourceChanged(consumer As Control)
        If consumer Is Nothing Then Return
        分离消费者依赖(consumer)
        MarkDirty(consumer, requestConsumers:=False)
        ' SetBackgroundSource 返回时后备字段尚未接收新值。
        ' 等新来源完成赋值并允许所有者先渲染新修订后，在下一次 UI 调度中刷新依赖项。
        If consumer.IsHandleCreated AndAlso Not consumer.IsDisposed Then
            Try
                consumer.BeginInvoke(CType(
                    Sub()
                        If consumer.IsDisposed Then Return
                        D3D_V5Presentation.RequestRender(consumer)
                        请求依赖消费者(consumer, New Rectangle(Point.Empty, consumer.Size))
                    End Sub, Action))
            Catch
            End Try
        End If
    End Sub

    Friend Shared Sub UnregisterConsumer(consumer As Control, Optional source As Control = Nothing)
        If consumer Is Nothing Then Return
        If source Is Nothing Then
            分离消费者依赖(consumer)
        Else
            分离消费者来源(consumer, source)
        End If
        MarkDirty(consumer, requestConsumers:=False)
    End Sub

    Private Shared Function 渲染项目(控件 As Control, 项目 As Entry, 可渲染对象 As D3D_IGpuRenderable,
                                      Optional 绘制后处理 As Action(Of D3D_PaintContext) = Nothing) As Boolean
        If 项目.Rendering Then Return 项目.Surface.Bitmap IsNot Nothing
        项目.Rendering = True
        If _当前渲染控件 Is Nothing Then _当前渲染控件 = New HashSet(Of Control)()
        _当前渲染控件.Add(控件)
        Try
            ' 依赖关系描述当前帧采样的像素。每次从 RenderGpu 重新构建，
            ' 防止来源切换、裁剪变化和控件移动留下长期存活的过期失效关系。
            分离消费者依赖(控件)
            Dim 请求脏区 = 项目.PendingDirty
            ' 进入 RenderGpu 前清空请求。若控件或其依赖来源在渲染期间自行失效，
            ' MarkDirty 写入的新待处理区域必须保留到下一帧。
            项目.PendingDirty = Rectangle.Empty
            Dim 渲染成功 = 项目.Surface.Render(可渲染对象, 请求脏区, 绘制后处理)
            If 渲染成功 Then
                项目.Revision = 项目.Surface.Revision
                项目.LastUsed = D3D_GpuCache.NextTick()
                项目.Dirty = 项目.PendingDirty.Width > 0 AndAlso 项目.PendingDirty.Height > 0
            Else
                项目.PendingDirty = 合并脏区(项目.PendingDirty, 请求脏区, 控件.Size)
                D3D_RenderDiagnostics.V5SurfaceRenderFailure(控件)
            End If
            Return 渲染成功
        Finally
            _当前渲染控件.Remove(控件)
            项目.Rendering = False
        End Try
    End Function

    Private Shared Function 正在渲染(控件 As Control) As Boolean
        Return 控件 IsNot Nothing AndAlso _当前渲染控件 IsNot Nothing AndAlso _当前渲染控件.Contains(控件)
    End Function

    Private Shared Function 获取或创建项目(控件 As Control) As Entry
        If 控件 Is Nothing OrElse 控件.IsDisposed Then Return Nothing
        Dim 项目 As Entry = Nothing
        If _entries.TryGetValue(控件, 项目) Then Return 项目
        项目 = New Entry(控件)
        _entries(控件) = 项目
        AddHandler 控件.Invalidated, AddressOf 控件已失效
        AddHandler 控件.SizeChanged, AddressOf 控件几何已变化
        AddHandler 控件.LocationChanged, AddressOf 控件几何已变化
        AddHandler 控件.ParentChanged, AddressOf 控件几何已变化
        AddHandler 控件.VisibleChanged, AddressOf 控件几何已变化
        AddHandler 控件.HandleCreated, AddressOf 控件几何已变化
        AddHandler 控件.HandleDestroyed, AddressOf 控件句柄已销毁
        AddHandler 控件.Disposed, AddressOf 控件已释放
        Return 项目
    End Function

    Private Shared Sub 注册依赖(消费者 As Control, 来源 As Control, 来源区域 As RectangleF)
        Dim 来源消费者 As HashSet(Of Control) = Nothing
        If Not _consumers.TryGetValue(来源, 来源消费者) Then
            来源消费者 = New HashSet(Of Control)()
            _consumers(来源) = 来源消费者
        End If
        来源消费者.Add(消费者)

        Dim 来源集合 As HashSet(Of Control) = Nothing
        If Not _consumerSources.TryGetValue(消费者, 来源集合) Then
            来源集合 = New HashSet(Of Control)()
            _consumerSources(消费者) = 来源集合
        End If
        来源集合.Add(来源)

        Dim 按来源区域 As Dictionary(Of Control, RectangleF) = Nothing
        If Not _dependencySourceRects.TryGetValue(消费者, 按来源区域) Then
            按来源区域 = New Dictionary(Of Control, RectangleF)()
            _dependencySourceRects(消费者) = 按来源区域
        End If
        Dim 现有区域 As RectangleF = RectangleF.Empty
        If 按来源区域.TryGetValue(来源, 现有区域) AndAlso 现有区域.Width > 0 AndAlso 现有区域.Height > 0 Then
            按来源区域(来源) = RectangleF.Union(现有区域, 来源区域)
        Else
            按来源区域(来源) = 来源区域
        End If

        注册坐标依赖(消费者, 来源)
    End Sub

    Private Shared Sub 注册坐标依赖(消费者 As Control, 来源 As Control)
        注册坐标链(消费者, 消费者.Parent)
        注册坐标链(消费者, 来源.Parent)
    End Sub

    Private Shared Sub 注册坐标链(消费者 As Control, 当前控件 As Control)
        While 当前控件 IsNot Nothing
            Dim 监视控件集合 As HashSet(Of Control) = Nothing
            If Not _consumerCoordinateControls.TryGetValue(消费者, 监视控件集合) Then
                监视控件集合 = New HashSet(Of Control)()
                _consumerCoordinateControls(消费者) = 监视控件集合
            End If
            If 监视控件集合.Add(当前控件) Then
                Dim 目标集合 As HashSet(Of Control) = Nothing
                If Not _coordinateConsumers.TryGetValue(当前控件, 目标集合) Then
                    目标集合 = New HashSet(Of Control)()
                    _coordinateConsumers(当前控件) = 目标集合
                    AddHandler 当前控件.LocationChanged, AddressOf 坐标空间已变化
                    AddHandler 当前控件.ParentChanged, AddressOf 坐标空间已变化
                    AddHandler 当前控件.VisibleChanged, AddressOf 坐标空间已变化
                    AddHandler 当前控件.Layout, AddressOf 坐标空间已变化
                    AddHandler 当前控件.Disposed, AddressOf 坐标控件已释放
                End If
                目标集合.Add(消费者)
            End If
            当前控件 = 当前控件.Parent
        End While
    End Sub

    Private Shared Sub 分离消费者依赖(消费者 As Control)
        Dim 来源集合 As HashSet(Of Control) = Nothing
        If _consumerSources.TryGetValue(消费者, 来源集合) Then
            For Each 来源 In 来源集合.ToArray()
                Dim 目标集合 As HashSet(Of Control) = Nothing
                If _consumers.TryGetValue(来源, 目标集合) Then
                    目标集合.Remove(消费者)
                    If 目标集合.Count = 0 Then _consumers.Remove(来源)
                End If
            Next
            _consumerSources.Remove(消费者)
        End If
        _dependencySourceRects.Remove(消费者)
        分离坐标依赖(消费者)
    End Sub

    Private Shared Sub 分离消费者来源(消费者 As Control, 来源 As Control)
        Dim 来源集合 As HashSet(Of Control) = Nothing
        If _consumerSources.TryGetValue(消费者, 来源集合) Then
            来源集合.Remove(来源)
            If 来源集合.Count = 0 Then _consumerSources.Remove(消费者)
        End If
        Dim 目标集合 As HashSet(Of Control) = Nothing
        If _consumers.TryGetValue(来源, 目标集合) Then
            目标集合.Remove(消费者)
            If 目标集合.Count = 0 Then _consumers.Remove(来源)
        End If
        Dim 来源区域集合 As Dictionary(Of Control, RectangleF) = Nothing
        If _dependencySourceRects.TryGetValue(消费者, 来源区域集合) Then
            来源区域集合.Remove(来源)
            If 来源区域集合.Count = 0 Then _dependencySourceRects.Remove(消费者)
        End If
        ' 坐标订阅可在下一帧低成本重建，并可能由多个来源共享，因此分离完整监视集合。
        分离坐标依赖(消费者)
    End Sub

    Private Shared Sub 分离坐标依赖(消费者 As Control)
        Dim 监视控件集合 As HashSet(Of Control) = Nothing
        If Not _consumerCoordinateControls.TryGetValue(消费者, 监视控件集合) Then Return
        For Each 坐标控件 In 监视控件集合.ToArray()
            Dim 目标集合 As HashSet(Of Control) = Nothing
            If Not _coordinateConsumers.TryGetValue(坐标控件, 目标集合) Then Continue For
            目标集合.Remove(消费者)
            If 目标集合.Count = 0 Then
                _coordinateConsumers.Remove(坐标控件)
                RemoveHandler 坐标控件.LocationChanged, AddressOf 坐标空间已变化
                RemoveHandler 坐标控件.ParentChanged, AddressOf 坐标空间已变化
                RemoveHandler 坐标控件.VisibleChanged, AddressOf 坐标空间已变化
                RemoveHandler 坐标控件.Layout, AddressOf 坐标空间已变化
                RemoveHandler 坐标控件.Disposed, AddressOf 坐标控件已释放
            End If
        Next
        _consumerCoordinateControls.Remove(消费者)
    End Sub

    Private Shared Sub 坐标空间已变化(发送者 As Object, 事件参数 As EventArgs)
        Dim 坐标控件 = TryCast(发送者, Control)
        Dim 目标集合 As HashSet(Of Control) = Nothing
        If 坐标控件 Is Nothing OrElse Not _coordinateConsumers.TryGetValue(坐标控件, 目标集合) Then Return
        For Each 消费者 In 目标集合.ToArray()
            If 消费者 Is Nothing OrElse 消费者.IsDisposed Then Continue For
            D3D_V5Presentation.RequestRender(消费者)
        Next
    End Sub

    Private Shared Sub 坐标控件已释放(发送者 As Object, 事件参数 As EventArgs)
        Dim 坐标控件 = TryCast(发送者, Control)
        Dim 目标集合 As HashSet(Of Control) = Nothing
        If 坐标控件 Is Nothing OrElse Not _coordinateConsumers.TryGetValue(坐标控件, 目标集合) Then Return
        _coordinateConsumers.Remove(坐标控件)
        For Each 消费者 In 目标集合.ToArray()
            Dim 监视控件集合 As HashSet(Of Control) = Nothing
            If _consumerCoordinateControls.TryGetValue(消费者, 监视控件集合) Then
                监视控件集合.Remove(坐标控件)
                If 监视控件集合.Count = 0 Then _consumerCoordinateControls.Remove(消费者)
            End If
            If 消费者 IsNot Nothing AndAlso Not 消费者.IsDisposed Then D3D_V5Presentation.RequestRender(消费者)
        Next
    End Sub

    Private Shared Sub 控件已失效(发送者 As Object, 事件参数 As InvalidateEventArgs)
        Dim 控件 = TryCast(发送者, Control)
        Dim 失效事件 = TryCast(事件参数, InvalidateEventArgs)
        Dim 脏区 = If(失效事件 Is Nothing, Rectangle.Empty, 失效事件.InvalidRect)
        MarkDirty(控件, 脏区)

        ' 大多数 V5 控件在此同步渲染，以保持父子表面由外到内的提交顺序。
        ' 单 HWND 对话框由 D3D_V5Presentation 合并请求，因此改为在其中排队。
        If D3D_V5Presentation.IsV5Control(控件) AndAlso Not D3D_V5Presentation.IsRendering Then
            D3D_V5Presentation.RequestRender(控件, 脏区)
        End If
    End Sub

    Private Shared Sub 控件几何已变化(发送者 As Object, 事件参数 As EventArgs)
        ' 几何变化同时影响当前表面及所有采样消费者。依赖矩形位于来源的旧坐标系，
        ' 因此必须无条件使消费者失效。
        MarkDirty(TryCast(发送者, Control), requestConsumers:=True)
    End Sub

    Friend Shared Function GetRecoveryTargets(form As Form) As Control()
        If form Is Nothing OrElse form.IsDisposed Then Return Array.Empty(Of Control)()

        Return _entries.Keys.
            Where(Function(control)
                      If control Is Nothing OrElse control.IsDisposed OrElse
                         Not control.IsHandleCreated OrElse Not control.Visible Then Return False
                      If Not D3D_V5Presentation.IsV5Control(control) Then Return False
                      Return Object.ReferenceEquals(D3D_RenderCore.ResolveCompositorForm(control), form)
                  End Function).
            OrderBy(Function(control) 获取控件树深度(control)).
            ToArray()
    End Function

    Private Shared Function 获取控件树深度(control As Control) As Integer
        Dim depth As Integer = 0
        Dim current = control
        Dim visited As New HashSet(Of Control)()
        While current IsNot Nothing AndAlso visited.Add(current)
            depth += 1
            current = current.Parent
        End While
        Return depth
    End Function

    Private Shared Sub 控件句柄已销毁(发送者 As Object, 事件参数 As EventArgs)
        Dim 控件 = TryCast(发送者, Control)
        If 控件 Is Nothing Then Return
        If 控件.IsDisposed Then
            移除控件(控件)
            Return
        End If
        ' 句柄销毁可能因换父级、显式重建、DPI 或样式变化而恢复。
        ' 保留逻辑依赖，使 HandleCreated 能够重建来源并唤醒所有映射消费者。
        ReleaseSurfaceResources(控件)
        ' 不要在 WmDestroy 事件中同步请求消费者重绘：控件 Dispose 通常会
        ' 先释放字体/图片等渲染资源，再进入基类句柄销毁流程，此时重绘会
        ' 读取已经释放的 Image。HandleCreated/Disposed 会负责后续唤醒或移除。
        MarkDirty(控件, requestConsumers:=False)
    End Sub

    Private Shared Sub 控件已释放(发送者 As Object, 事件参数 As EventArgs)
        移除控件(TryCast(发送者, Control))
    End Sub

    Private Shared Sub 移除控件(控件 As Control)
        If 控件 Is Nothing Then Return
        Dim 依赖消费者 As Control() = Array.Empty(Of Control)()
        Dim 目标集合 As HashSet(Of Control) = Nothing
        If _consumers.TryGetValue(控件, 目标集合) Then 依赖消费者 = 目标集合.ToArray()
        分离消费者依赖(控件)
        For Each 消费者 In 依赖消费者
            分离消费者来源(消费者, 控件)
        Next
        Dim 项目 As Entry = Nothing
        If _entries.TryGetValue(控件, 项目) Then
            _entries.Remove(控件)
            项目.Surface.Dispose()
        End If
        _consumers.Remove(控件)
        For Each 消费者 In 依赖消费者
            If 消费者 IsNot Nothing AndAlso Not 消费者.IsDisposed Then D3D_V5Presentation.RequestRender(消费者)
        Next
    End Sub

    Friend Shared Sub HandleDeviceLost()
        For Each 项目 In _entries.Values
            项目.Dirty = True
            项目.Surface.HandleDeviceLost()
        Next
    End Sub

    Friend Shared Sub ReleaseSurfaceResources(control As Control)
        If control Is Nothing Then Return
        Dim 项目 As Entry = Nothing
        If Not _entries.TryGetValue(control, 项目) OrElse 项目 Is Nothing Then Return
        项目.Dirty = True
        项目.PendingDirty = Rectangle.Empty
        项目.Surface.ReleaseSurfaceResources()
    End Sub

    Friend Shared Sub SurfaceResourcesReleased(control As Control)
        Dim 项目 As Entry = Nothing
        If control Is Nothing OrElse Not _entries.TryGetValue(control, 项目) OrElse 项目 Is Nothing Then Return
        项目.Dirty = True
        项目.PendingDirty = New Rectangle(Point.Empty, control.Size)
    End Sub

    Friend Shared Sub ReleaseUnreferencedSurface(control As Control)
        Dim 项目 As Entry = Nothing
        If control Is Nothing OrElse Not _entries.TryGetValue(control, 项目) OrElse 项目 Is Nothing Then Return
        ' 可见控件的表面是当前 HWND 的显示工作集。V3 保留这类表面，
        ' 否则下一次窗口装饰或背景映射重绘只能看到被清空的纯色表面。
        If Not control.IsDisposed AndAlso control.Visible Then Return
        If 项目.Rendering OrElse _consumers.ContainsKey(control) Then Return
        If 项目.Surface Is Nothing OrElse 项目.Surface.Bitmap Is Nothing Then Return
        项目.Surface.ReleaseSurfaceResources(markRegistryDirty:=False)
    End Sub

    Friend Shared Function GetRevision(control As Control) As Long
        Dim 项目 As Entry = Nothing
        If control Is Nothing OrElse Not _entries.TryGetValue(control, 项目) Then Return 0L
        Return 项目.Revision
    End Function

    Friend Shared Function IsDirty(control As Control) As Boolean
        Dim 项目 As Entry = Nothing
        Return control IsNot Nothing AndAlso _entries.TryGetValue(control, 项目) AndAlso 项目.Dirty
    End Function

    ''' <summary>完整 GPU 表面已可用于呈现时返回 True。</summary>
    Friend Shared Function HasCurrentSurface(control As Control) As Boolean
        Dim 项目 As Entry = Nothing
        If control Is Nothing OrElse Not _entries.TryGetValue(control, 项目) OrElse 项目 Is Nothing Then Return False
        Return Not 项目.Dirty AndAlso Not 项目.Rendering AndAlso
               项目.Surface IsNot Nothing AndAlso 项目.Surface.Bitmap IsNot Nothing
    End Function

    Private Shared Function 合并脏区(现有区域 As Rectangle, 请求区域 As Rectangle, 边界尺寸 As Size) As Rectangle
        Dim 裁剪区域 = New Rectangle(Point.Empty, New Size(Math.Max(0, 边界尺寸.Width), Math.Max(0, 边界尺寸.Height)))
        Dim 下一脏区 = Rectangle.Intersect(裁剪区域, 请求区域)
        If 下一脏区.Width <= 0 OrElse 下一脏区.Height <= 0 Then Return 现有区域
        If 现有区域.Width <= 0 OrElse 现有区域.Height <= 0 Then Return 下一脏区
        Return Rectangle.Union(现有区域, 下一脏区)
    End Function

    ''' <summary>
    ''' 原生宿主 Form 用作 BackgroundSource 时的 GPU 表示。
    ''' 它只渲染宿主自身的视觉背景和窗口装饰；子控件仍使用独立 HWND 表面，
    ''' 并且不会通过 GDI 捕获。
    ''' </summary>
    Private NotInheritable Class NativeFormSurfaceRenderable
        Implements D3D_IGpuRenderable

        Private ReadOnly _form As Form

        Friend Sub New(form As Form)
            _form = form
        End Sub

        Public Sub RenderGpu(context As D3D_PaintContext) Implements D3D_IGpuRenderable.RenderGpu
            If context Is Nothing OrElse _form Is Nothing OrElse _form.IsDisposed Then Return
            Dim 边界 As New RectangleF(0, 0, Math.Max(1, _form.ClientSize.Width), Math.Max(1, _form.ClientSize.Height))
            If _form.BackColor.A > 0 Then context.FillRectangle(边界, _form.BackColor)
            ThisIsYourWindow.TryRenderAttachedSurface(context, _form)
        End Sub
    End Class
End Class
