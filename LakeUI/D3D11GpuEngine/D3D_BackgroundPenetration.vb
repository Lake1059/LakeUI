Imports System.Drawing.Imaging
Imports System.Buffers
Imports Vortice.Direct2D1

''' <summary>
''' V3 背景穿透兼容实现。第一代的精简继任者。
'''
''' === 与 V1 (<see cref="TransparentBackgroundCache"/>) 的差异 ===
''' • V3 必须显式指定背景源（child 控件的 <c>BackgroundSource</c> 属性）。未指定 → 不采样、不绘制，
'''   完全不画背景层。不再自动回退到 child.Parent，避免无意识的递归与采样链。
''' • 每个 source 在窗口内共享一份 GDI Bitmap 作权威，按 source 的
'''   <see cref="Control.Invalidated"/> / <see cref="Control.Resize"/> / <see cref="Control.Disposed"/> 失效。
'''   Invalidated 带矩形时只重采 dirty rect，并释放对应 source 的共享 D2D 上传图。
''' • D2D 上传按 source / RenderTarget 共享一张整源位图；各 child 使用 DrawBitmap 的 sourceRect
'''   从共享图中取样，避免大量控件各自持有一份裁剪上传副本。
''' • 不区分 GDI / D2D 消费者：GDI Bitmap 负责重采与 dirty rect 合并，ID2D1Bitmap 只作为当前 RT 的上传缓存。
'''
''' === BackColor 与背景穿透的优先级（V3 强约束） ===
'''   1) 指定了 BackgroundSource → 跳过 BackColor，直接调本类画穿透层。
'''   2) 未指定 BackgroundSource 且 BackColor.A &gt; 0 → 直接以 BackColor 填底，本类不参与。
'''   3) 未指定 BackgroundSource 且 BackColor.A = 0 → 不画背景层（由 WinForms 默认透明逻辑处理）。
'''
''' === 用法 ===
'''   ' 在 OnPaint 内、画图形层之前：
'''   If _backgroundSource IsNot Nothing Then
'''       D3D_BackgroundPenetration.PaintBackground(Me, scope, _backgroundSource)
'''   End If
'''
''' === 注意 ===
''' • source 自身若是纯透明转发面板，会先解析到最终真实 source，再按该 canonical source 共享 CPU backing
'''   与 D2D 整源上传；Demo 中多个透明页面透出的 Form1 因此命中同一份缓存。
''' • D2D 共享上传缓存接入 <see cref="GlobalOptions.GpuCacheBudgetBytes"/>；CPU backing bitmap 接入
'''   <see cref="GlobalOptions.CpuCacheBudgetBytes"/>。source 离开可见控件链后仍会主动释放整份背景采样缓存。
''' • <see cref="Invalidate"/> 用于换主题等需要立即重采的极端场景，常规情况下不需要手动调用。
'''
''' === 调用坑点 ===
''' • RegisterConsumer 会把同一个 child 从其他 source 的消费者列表中移除，保证控件换 BackgroundSource 后
'''   旧 source 不再传播失效。
''' • FlushConsumerInvalidations 走 <see cref="OuterToInnerRefreshScheduler"/>，不要在这里直接 Update；
'''   背景穿透的刷新必须和外层容器 resize/layout 合并。
''' • 只有真实 source 内容变化才应该调用 <see cref="Invalidate(Control)"/>。标题栏文字、按钮 hover 等局部 UI
'''   不应把整个 Form 作为 source 置脏。
'''
''' === 线程要求 ===
''' UI 线程。对 _cache 字典有锁，但 GDI / D2D 调用本身仍假定在 UI 线程。
''' </summary>
Public Module D3D_BackgroundPenetration

    Private Class Entry
        Public Class SharedUploadEntry
            Public Width As Integer
            Public Height As Integer
            Public Bytes As Long
            Public D2DBmp As ID2D1Bitmap
            Public D2DOwnerRT As WeakReference
            Public Version As Integer
            Public LastUsed As Long
        End Class

        Public Class ConsumerEntry
            Public ChildRef As WeakReference
            Public SourceRect As Rectangle
            Public DestRect As Rectangle
            Public TopologyRefs As List(Of Control)
        End Class

        Public Bmp As Bitmap
        Public Width As Integer
        Public Height As Integer
        Public BitmapBytes As Long
        Public LastUsed As Long
        Public Version As Integer
        Public FullDirty As Boolean = True
        Public ReadOnly DirtyRects As New List(Of Rectangle)()
        Public Painting As Boolean
        Public ReadOnly SharedUploads As New List(Of SharedUploadEntry)()
        Public ReadOnly Consumers As New List(Of ConsumerEntry)()
        Public ReadOnly AncestorSubscriptions As New List(Of Control)()
        Public IsSolidColor As Boolean
        Public SolidArgb As Integer
    End Class

    Private Structure ConsumerInvalidation
        Public ReadOnly Child As Control
        Public ReadOnly Rect As Rectangle

        Public Sub New(child As Control, rect As Rectangle)
            Me.Child = child
            Me.Rect = rect
        End Sub
    End Structure

    Private Structure BitmapAcquireResult
        Public Bitmap As ID2D1Bitmap
        Public SourceRect As Rectangle
        Public DrawDestRect As Rectangle
        Public DisposeAfterDraw As Boolean
    End Structure

    Private ReadOnly _cache As New Dictionary(Of Control, Entry)
    Private ReadOnly _ancestorSubscriptionRefs As New Dictionary(Of Control, Integer)
    Private ReadOnly _topologySubscriptionRefs As New Dictionary(Of Control, Integer)
    Private ReadOnly _consumerLifecycleSubscriptions As New HashSet(Of Control)
    Private _sourceBitmapBytes As Long
    Private _sharedUploadBytes As Long
    Private ReadOnly _cpuOwner As New CpuBudgetOwner()
    Private ReadOnly _gpuOwner As New GpuBudgetOwner()

    Private NotInheritable Class CpuBudgetOwner
        Implements D3D_IRenderCacheOwner

        Public Sub New()
            D3D_CpuCache.Register(Me)
        End Sub

        Public ReadOnly Property CacheBytes As Long Implements D3D_IRenderCacheOwner.CacheBytes
            Get
                SyncLock _cache
                    Return _sourceBitmapBytes
                End SyncLock
            End Get
        End Property

        Public ReadOnly Property OldestUseTick As Long Implements D3D_IRenderCacheOwner.OldestUseTick
            Get
                SyncLock _cache
                    Return GetOldestSourceBitmapTick()
                End SyncLock
            End Get
        End Property

        Public Function TrimOldest() As Boolean Implements D3D_IRenderCacheOwner.TrimOldest
            SyncLock _cache
                Return TrimOldestSourceBitmap(Nothing)
            End SyncLock
        End Function

        Public Sub ReleaseAll() Implements D3D_IRenderCacheOwner.ReleaseAll
            SyncLock _cache
                For Each entry In _cache.Values
                    ReleaseSourceBitmap(entry)
                    entry.FullDirty = True
                Next
            End SyncLock
        End Sub
    End Class

    Private NotInheritable Class GpuBudgetOwner
        Implements D3D_IRenderCacheOwner

        Public Sub New()
            D3D_GpuCache.Register(Me)
        End Sub

        Public ReadOnly Property CacheBytes As Long Implements D3D_IRenderCacheOwner.CacheBytes
            Get
                SyncLock _cache
                    Return _sharedUploadBytes
                End SyncLock
            End Get
        End Property

        Public ReadOnly Property OldestUseTick As Long Implements D3D_IRenderCacheOwner.OldestUseTick
            Get
                SyncLock _cache
                    Return GetOldestSharedUploadTick()
                End SyncLock
            End Get
        End Property

        Public Function TrimOldest() As Boolean Implements D3D_IRenderCacheOwner.TrimOldest
            SyncLock _cache
                Return TrimOldestSharedUpload(Nothing)
            End SyncLock
        End Function

        Public Sub ReleaseAll() Implements D3D_IRenderCacheOwner.ReleaseAll
            SyncLock _cache
                For Each entry In _cache.Values
                    InvalidateSharedUploads(entry, Nothing)
                Next
            End SyncLock
        End Sub
    End Class

#Region "公开 API"

    ''' <summary>
    ''' 把 <paramref name="source"/> 在 <paramref name="child"/> 所在区域的内容采样后画到
    ''' <c>scope.BackgroundLayer</c>。
    ''' </summary>
    ''' <param name="child">透明控件本人。决定采样目标矩形（child.Width × child.Height）。</param>
    ''' <param name="scope">当前兼容绘制作用域，背景层来自 <see cref="D3D_PaintScope.BackgroundLayer"/>。</param>
    ''' <param name="source">
    ''' 显式背景源。<c>Nothing</c> 或已 Dispose 时本方法直接返回，背景层将保持上一次状态（通常是清空）。
    ''' V3 不再隐式回退到 <c>child.Parent</c>。
    ''' </param>
    Public Sub PaintBackground(child As Control, scope As D3D_PaintScope, source As Control)
        If scope Is Nothing Then Return
        PaintBackgroundCore(
            child,
            source,
            scope.ClipRectangle,
            scope.BackgroundLayer,
            Sub(rt, destRect, solidColor)
                Dim brushCache = scope.Compositor?.BrushCache
                If brushCache IsNot Nothing Then
                    rt.FillRectangle(D3D_D2DInterop.ToD2DRect(destRect), brushCache.Get(rt, solidColor))
                Else
                    Using b = rt.CreateSolidColorBrush(D3D_D2DInterop.ToColor4(solidColor))
                        rt.FillRectangle(D3D_D2DInterop.ToD2DRect(destRect), b)
                    End Using
                End If
            End Sub)
    End Sub

    Public Sub PaintBackground(child As Control,
                               context As D3D_PaintContext,
                               source As Control,
                               destination As RectangleF)
        If context Is Nothing Then Return
        Dim clipRect = Rectangle.Round(destination)
        If clipRect.Width <= 0 OrElse clipRect.Height <= 0 Then
            clipRect = New Rectangle(Point.Empty, If(child IsNot Nothing, child.Size, Size.Empty))
        End If

        PaintBackgroundCore(
            child,
            source,
            clipRect,
            context.DeviceContext,
            Sub(rt, destRect, solidColor)
                Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, solidColor, context.DeviceGeneration)
                context.DeviceContext.FillRectangle(D3D_PaintContext.ToRawRect(destRect), brush)
            End Sub)
    End Sub

    Private Delegate Sub SolidBackgroundPainter(rt As ID2D1RenderTarget, destRect As Rectangle, solidColor As Color)

    Private Sub PaintBackgroundCore(child As Control,
                                    source As Control,
                                    clipRect As Rectangle,
                                    rt As ID2D1RenderTarget,
                                    paintSolid As SolidBackgroundPainter)
        If child Is Nothing OrElse rt Is Nothing Then Return
        If source Is Nothing OrElse source.IsDisposed Then Return
        Dim sw As Integer = source.Width, sh As Integer = source.Height
        If sw <= 0 OrElse sh <= 0 Then Return
        If Not IsRenderableControl(source) Then
            ReleaseSource(source)
            Return
        End If

        Dim childBounds As New Rectangle(0, 0, child.Width, child.Height)
        Dim destRect As Rectangle = Rectangle.Intersect(childBounds, clipRect)
        If destRect.Width <= 0 OrElse destRect.Height <= 0 Then Return

        Dim sourceForSampling As Control = source
        Dim offset As Point = ComputeOffset(child, sourceForSampling)
        Dim sourceMappings As New List(Of KeyValuePair(Of Control, Rectangle))()
        Dim topologyNodes As New List(Of Control)()
        If Not ResolveTransparentSourceChain(childBounds, sourceForSampling, offset, sourceMappings, topologyNodes) Then Return
        sw = sourceForSampling.Width
        sh = sourceForSampling.Height
        Dim srcRect As New Rectangle(offset.X + destRect.X, offset.Y + destRect.Y, destRect.Width, destRect.Height)
        If srcRect.Width <= 0 OrElse srcRect.Height <= 0 Then Return

        Dim isSolid As Boolean
        Dim solidColor As Color = Color.Empty
        Dim acquired As New BitmapAcquireResult()
        If Not TryAcquireContainedStockPanelBackgroundBitmap(sourceForSampling, child, rt, srcRect, destRect, acquired) Then
            acquired = AcquireD2DBitmap(sourceForSampling, rt, srcRect, isSolid, solidColor)
        End If
        Try
            RegisterConsumerMappings(child, sourceMappings, childBounds, topologyNodes)
            If isSolid Then
                paintSolid(rt, destRect, solidColor)
                Return
            End If
            If acquired.Bitmap Is Nothing Then Return
            Dim drawDestRect As Rectangle = acquired.DrawDestRect
            If drawDestRect.Width <= 0 OrElse drawDestRect.Height <= 0 Then
                drawDestRect = New Rectangle(
                    destRect.X + acquired.SourceRect.X - srcRect.X,
                    destRect.Y + acquired.SourceRect.Y - srcRect.Y,
                    acquired.SourceRect.Width,
                    acquired.SourceRect.Height)
            End If
            If drawDestRect.Width <= 0 OrElse drawDestRect.Height <= 0 Then Return
            rt.DrawBitmap(acquired.Bitmap,
                D3D_D2DInterop.ToD2DRect(drawDestRect),
                1.0F,
                BitmapInterpolationMode.Linear,
                D3D_D2DInterop.ToD2DRect(acquired.SourceRect))
        Finally
            If acquired.DisposeAfterDraw AndAlso acquired.Bitmap IsNot Nothing Then
                Try : acquired.Bitmap.Dispose() : Catch : End Try
            End If
        End Try
    End Sub

    ''' <summary>
    ''' 显式让指定背景源的缓存立刻置脏，下一次 <see cref="PaintBackground"/> 会重采。
    ''' </summary>
    ''' <remarks>
    ''' 常规情况下 source 的 <see cref="Control.Invalidated"/> 已经自动置脏，本方法只用于
    ''' 切换主题 / 强制刷新等不会触发 Invalidated 的场景。
    ''' </remarks>
    Public Sub Invalidate(source As Control)
        If source Is Nothing Then Return
        Dim invalidations As New List(Of ConsumerInvalidation)()
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then
                entry.FullDirty = True
                InvalidateSharedUploads(entry, Nothing)
                CollectConsumerInvalidations(source, entry, Nothing, invalidations)
            End If
        End SyncLock
        FlushConsumerInvalidations(invalidations)
    End Sub

    ''' <summary>
    ''' 仅让指定源区域置脏，并只刷新映射到该区域的消费者矩形。
    ''' </summary>
    Public Sub Invalidate(source As Control, dirtyRect As Rectangle)
        If source Is Nothing Then Return
        If dirtyRect.Width <= 0 OrElse dirtyRect.Height <= 0 Then
            Invalidate(source)
            Return
        End If

        Dim invalidations As New List(Of ConsumerInvalidation)()
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then
                AddDirtyRect(entry, dirtyRect, New Rectangle(0, 0, source.Width, source.Height))
                InvalidateSharedUploads(entry, dirtyRect)
                If entry.FullDirty Then
                    InvalidateSharedUploads(entry, Nothing)
                    CollectConsumerInvalidations(source, entry, Nothing, invalidations)
                Else
                    CollectConsumerInvalidations(source, entry, dirtyRect, invalidations)
                End If
            End If
        End SyncLock
        FlushConsumerInvalidations(invalidations)
    End Sub

    Public Sub InvalidateForwarderTopology(forwarder As Control)
        If forwarder Is Nothing Then Return
        OnForwarderTopologyChanged(forwarder, EventArgs.Empty)
    End Sub

    ''' <summary>
    ''' 为控件切换背景源时移除旧 source 订阅并返回新值，避免已切换控件继续响应旧 source 的失效传播。
    ''' </summary>
    Public Function SetBackgroundSource(owner As Control, oldSource As Control, newSource As Control) As Control
        If owner IsNot Nothing Then
            DetachConsumerLifecycle(owner)
            If oldSource IsNot newSource Then
                UnregisterSingleConsumer(owner, Nothing)
                OuterToInnerRefreshScheduler.RequestFull(owner)
            End If
            If newSource IsNot Nothing Then AttachConsumerLifecycle(owner)
        End If
        Return newSource
    End Function

    Public Function SetConsumerSource(child As Control, oldSource As Control, newSource As Control) As Control
        Return SetBackgroundSource(child, oldSource, newSource)
    End Function

    ''' <summary>
    ''' 主动移除透明背景消费者。用于控件隐藏、换父级、换 source 或销毁时，
    ''' 避免已下线页面继续留在 source 的失效传播列表中。
    ''' </summary>
    Public Sub UnregisterConsumer(child As Control, Optional source As Control = Nothing)
        UnregisterBackgroundConsumer(child)
    End Sub

    Public Sub UnregisterBackgroundConsumer(owner As Control)
        If owner Is Nothing Then Return
        UnregisterSingleConsumer(owner, Nothing)
        For Each child As Control In owner.Controls
            UnregisterBackgroundConsumer(child)
        Next
    End Sub

    Private Sub UnregisterSingleConsumer(child As Control, Optional source As Control = Nothing)
        If child Is Nothing Then Return

        SyncLock _cache
            For Each kv In _cache.ToArray()
                If source IsNot Nothing AndAlso kv.Key IsNot source Then Continue For
                RemoveConsumerNoLock(kv.Key, kv.Value, child)
            Next
        End SyncLock
    End Sub

    Private Sub AttachConsumerLifecycle(owner As Control)
        If owner Is Nothing Then Return
        SyncLock _cache
            If _consumerLifecycleSubscriptions.Contains(owner) Then Return
            _consumerLifecycleSubscriptions.Add(owner)
        End SyncLock
        Try : AddHandler owner.HandleDestroyed, AddressOf OnConsumerHandleDestroyed : Catch : End Try
        Try : AddHandler owner.Disposed, AddressOf OnConsumerDisposed : Catch : End Try
        Try : AddHandler owner.VisibleChanged, AddressOf OnConsumerVisibleChanged : Catch : End Try
        Try : AddHandler owner.ParentChanged, AddressOf OnConsumerParentChanged : Catch : End Try
    End Sub

    Private Sub DetachConsumerLifecycle(owner As Control)
        If owner Is Nothing Then Return
        SyncLock _cache
            If Not _consumerLifecycleSubscriptions.Remove(owner) Then Return
        End SyncLock
        Try : RemoveHandler owner.HandleDestroyed, AddressOf OnConsumerHandleDestroyed : Catch : End Try
        Try : RemoveHandler owner.Disposed, AddressOf OnConsumerDisposed : Catch : End Try
        Try : RemoveHandler owner.VisibleChanged, AddressOf OnConsumerVisibleChanged : Catch : End Try
        Try : RemoveHandler owner.ParentChanged, AddressOf OnConsumerParentChanged : Catch : End Try
    End Sub

    Private Sub OnConsumerHandleDestroyed(sender As Object, e As EventArgs)
        Dim owner = TryCast(sender, Control)
        If owner Is Nothing Then Return
        UnregisterBackgroundConsumer(owner)
    End Sub

    Private Sub OnConsumerDisposed(sender As Object, e As EventArgs)
        Dim owner = TryCast(sender, Control)
        If owner Is Nothing Then Return
        DetachConsumerLifecycle(owner)
        UnregisterBackgroundConsumer(owner)
    End Sub

    Private Sub OnConsumerVisibleChanged(sender As Object, e As EventArgs)
        Dim owner = TryCast(sender, Control)
        If owner Is Nothing OrElse owner.Visible Then Return
        UnregisterBackgroundConsumer(owner)
    End Sub

    Private Sub OnConsumerParentChanged(sender As Object, e As EventArgs)
        Dim owner = TryCast(sender, Control)
        If owner Is Nothing Then Return
        UnregisterBackgroundConsumer(owner)
    End Sub

    Friend Sub CleanupD2DResources(level As D3DCacheCleanupLevel, Optional owner As Control = Nothing)
        Dim targetForm As Form = ResolveCleanupForm(owner)
        SyncLock _cache
            Select Case level
                Case D3DCacheCleanupLevel.TrimToBudget
                    D3D_CpuCache.TrimToBudget()
                    D3D_GpuCache.TrimToBudget()

                Case D3DCacheCleanupLevel.ReleaseVolatileCaches
                    For Each kv In _cache.ToArray()
                        If ShouldCleanupSource(kv.Key, targetForm) Then
                            InvalidateSharedUploads(kv.Value, Nothing)
                        End If
                    Next

                Case D3DCacheCleanupLevel.ReleaseAllCaches,
                     D3DCacheCleanupLevel.ReleaseRenderTargets,
                     D3DCacheCleanupLevel.RecreateDevice
                    For Each kv In _cache.ToArray()
                        If ShouldCleanupSource(kv.Key, targetForm) Then
                            ReleaseEntryCache(kv.Value)
                        End If
                    Next

                Case Else
                    For Each kv In _cache.ToArray()
                        If ShouldCleanupSource(kv.Key, targetForm) Then
                            RemoveSourceEntryNoLock(kv.Key, kv.Value)
                        End If
                    Next
            End Select
        End SyncLock
    End Sub

    Private Function ResolveCleanupForm(owner As Control) As Form
        If owner Is Nothing OrElse owner.IsDisposed Then Return Nothing
        If TypeOf owner Is Form Then Return DirectCast(owner, Form)
        Try
            Return owner.FindForm()
        Catch
            Return Nothing
        End Try
    End Function

    Private Function ShouldCleanupSource(source As Control, targetForm As Form) As Boolean
        If targetForm Is Nothing Then Return True
        If source Is Nothing OrElse source.IsDisposed Then Return False
        Try
            Return source.FindForm() Is targetForm
        Catch
            Return False
        End Try
    End Function

    Private Sub RegisterConsumer(source As Control, child As Control, sourceRect As Rectangle, destRect As Rectangle)
        If source Is Nothing OrElse child Is Nothing Then Return
        Dim mappings As New List(Of KeyValuePair(Of Control, Rectangle)) From {
            New KeyValuePair(Of Control, Rectangle)(source, sourceRect)
        }
        RegisterConsumers(child, mappings, destRect, Nothing)
    End Sub

    Private Sub RegisterConsumerMappings(child As Control, mappings As List(Of KeyValuePair(Of Control, Rectangle)),
                                         destRect As Rectangle,
                                         topologyNodes As List(Of Control))
        RegisterConsumers(child, mappings, destRect, topologyNodes)
    End Sub

    Private Sub RegisterConsumers(child As Control, mappings As List(Of KeyValuePair(Of Control, Rectangle)),
                                  destRect As Rectangle,
                                  topologyNodes As List(Of Control))
        If child Is Nothing OrElse mappings Is Nothing OrElse mappings.Count = 0 Then Return
        If Not IsRenderableControl(child) Then Return
        AttachConsumerLifecycle(child)

        SyncLock _cache
            ' 一个 child 同一时刻只注册到 canonical source。透明转发面板只作为 topology 节点，
            ' 用于坐标/层级变化时触发重算，不参与像素缓存，也不监听其 Invalidated。
            For Each kv In _cache.ToArray()
                If MappingContainsSource(mappings, kv.Key) Then Continue For
                RemoveConsumerNoLock(kv.Key, kv.Value, child)
            Next

            For Each mapping In mappings
                Dim source = mapping.Key
                If source Is Nothing OrElse source.IsDisposed Then Continue For
                Dim entry = EnsureEntryNoLock(source)
                If entry Is Nothing Then Continue For
                AddOrUpdateConsumerNoLock(entry, child, mapping.Value, destRect, topologyNodes)
            Next
        End SyncLock
    End Sub

    Private Function MappingContainsSource(mappings As List(Of KeyValuePair(Of Control, Rectangle)), source As Control) As Boolean
        If mappings Is Nothing OrElse source Is Nothing Then Return False
        For Each mapping In mappings
            If mapping.Key Is source Then Return True
        Next
        Return False
    End Function

    Private Sub AddOrUpdateConsumerNoLock(entry As Entry, child As Control, sourceRect As Rectangle,
                                          destRect As Rectangle,
                                          topologyNodes As List(Of Control))
        If entry Is Nothing OrElse child Is Nothing Then Return

        For i As Integer = entry.Consumers.Count - 1 To 0 Step -1
            Dim consumer = entry.Consumers(i)
            Dim existingChild = TryCast(consumer.ChildRef.Target, Control)
            If existingChild Is Nothing OrElse existingChild.IsDisposed Then
                ReleaseConsumerTopology(consumer)
                entry.Consumers.RemoveAt(i)
            ElseIf existingChild Is child Then
                ReleaseConsumerTopology(consumer)
                consumer.SourceRect = sourceRect
                consumer.DestRect = destRect
                consumer.TopologyRefs = CloneTopologyNodes(topologyNodes)
                AcquireConsumerTopology(consumer)
                Return
            End If
        Next

        Dim newConsumer As New Entry.ConsumerEntry With {
            .ChildRef = New WeakReference(child),
            .SourceRect = sourceRect,
            .DestRect = destRect,
            .TopologyRefs = CloneTopologyNodes(topologyNodes)
        }
        AcquireConsumerTopology(newConsumer)
        entry.Consumers.Add(newConsumer)
    End Sub

    Private Sub RemoveConsumerNoLock(source As Control, entry As Entry, child As Control)
        If source Is Nothing OrElse entry Is Nothing OrElse child Is Nothing Then Return

        For i As Integer = entry.Consumers.Count - 1 To 0 Step -1
            Dim consumer = entry.Consumers(i)
            Dim existingChild = TryCast(consumer.ChildRef.Target, Control)
            If existingChild Is Nothing OrElse existingChild.IsDisposed OrElse existingChild Is child Then
                ReleaseConsumerTopology(consumer)
                entry.Consumers.RemoveAt(i)
            End If
        Next

        If entry.Consumers.Count = 0 Then
            RemoveSourceEntryNoLock(source, entry)
        End If
    End Sub

#End Region

#Region "缓存内部"

    Private Function AcquireD2DBitmap(source As Control, rt As ID2D1RenderTarget, sourceRect As Rectangle,
                                      ByRef isSolid As Boolean, ByRef solidColor As Color) As BitmapAcquireResult
        isSolid = False
        solidColor = Color.Empty
        Dim sw As Integer = source.Width, sh As Integer = source.Height
        Dim entry As Entry = Nothing
        Dim needRebuild As Boolean
        Dim reuseExistingBitmapWhilePainting As Boolean
        SyncLock _cache
            entry = EnsureEntryNoLock(source)
            entry.LastUsed = D3D_CpuCache.NextTick()
            ' Pure-color entries intentionally drop Bmp to save RAM; the cached SolidArgb is still valid
            ' while the source size matches and no invalidation has marked it dirty.
            Dim sizeOk As Boolean = ((entry.Bmp IsNot Nothing OrElse entry.IsSolidColor) AndAlso entry.Width = sw AndAlso entry.Height = sh)
            If Not sizeOk Then
                entry.FullDirty = True
                InvalidateSharedUploads(entry, Nothing)
            End If
            If entry.Painting Then
                If entry.IsSolidColor AndAlso sizeOk Then
                    isSolid = True
                    solidColor = Color.FromArgb(entry.SolidArgb)
                    Return Nothing
                End If
                If entry.Bmp IsNot Nothing AndAlso sizeOk Then
                    reuseExistingBitmapWhilePainting = True
                Else
                    Return Nothing
                End If
            Else
                needRebuild = entry.FullDirty OrElse DirtyRectsIntersect(entry, sourceRect)
                If needRebuild Then entry.Painting = True
            End If
        End SyncLock

        If reuseExistingBitmapWhilePainting Then Return AcquireSharedSourceBitmap(entry, rt, sourceRect)

        If needRebuild Then
            Try
                RebuildGdiBitmap(source, entry, sw, sh)
            Finally
                SyncLock _cache
                    entry.Painting = False
                    entry.LastUsed = D3D_CpuCache.NextTick()
                    TrimSourceBitmaps(entry)
                End SyncLock
            End Try
        End If

        If entry.IsSolidColor Then
            isSolid = True
            solidColor = Color.FromArgb(entry.SolidArgb)
            Return Nothing
        End If
        If entry.Bmp Is Nothing Then
            SyncLock _cache
                entry.FullDirty = True
            End SyncLock
            Return Nothing
        End If

        Return AcquireSharedSourceBitmap(entry, rt, sourceRect)
    End Function

    Private Function EnsureEntry(source As Control) As Entry
        If source Is Nothing Then Return Nothing
        SyncLock _cache
            Return EnsureEntryNoLock(source)
        End SyncLock
    End Function

    Private Function EnsureEntryNoLock(source As Control) As Entry
        If source Is Nothing Then Return Nothing
        Dim entry As Entry = Nothing
        If _cache.TryGetValue(source, entry) Then Return entry

        entry = New Entry()
        _cache(source) = entry
        AddHandler source.Disposed, AddressOf OnSourceDisposed
        AddHandler source.HandleDestroyed, AddressOf OnSourceHandleDestroyed
        AddHandler source.Invalidated, AddressOf OnSourceInvalidated
        AddHandler source.LocationChanged, AddressOf OnSourceParentOrVisibleChanged
        AddHandler source.ParentChanged, AddressOf OnSourceParentOrVisibleChanged
        AddHandler source.Resize, AddressOf OnSourceResized
        AddHandler source.VisibleChanged, AddressOf OnSourceParentOrVisibleChanged
        RefreshAncestorSubscriptions(source, entry)
        Return entry
    End Function

    Private Function TryForwardTransparentSource(source As Control, ByRef forwardedSource As Control) As Boolean
        forwardedSource = Nothing
        Dim panel = TryCast(source, ModernPanel)
        If panel Is Nothing OrElse panel.IsDisposed Then Return False
        If Not panel.TryGetTransparentBackgroundForward(forwardedSource) Then Return False
        If forwardedSource Is Nothing OrElse forwardedSource.IsDisposed OrElse forwardedSource Is source Then
            forwardedSource = Nothing
            Return False
        End If
        Return True
    End Function

    Private Function ResolveTransparentSourceChain(childBounds As Rectangle,
                                                   ByRef sourceForSampling As Control,
                                                   ByRef offset As Point,
                                                   mappings As List(Of KeyValuePair(Of Control, Rectangle)),
                                                   topologyNodes As List(Of Control)) As Boolean
        If sourceForSampling Is Nothing OrElse mappings Is Nothing Then Return False
        Dim visited As New HashSet(Of Control)()
        Do
            If sourceForSampling Is Nothing OrElse sourceForSampling.IsDisposed Then Return False
            If sourceForSampling.Width <= 0 OrElse sourceForSampling.Height <= 0 Then Return False
            If Not IsRenderableControl(sourceForSampling) Then
                ReleaseSource(sourceForSampling)
                Return False
            End If
            If visited.Contains(sourceForSampling) Then Return False
            visited.Add(sourceForSampling)

            ' V2 compatibility: transparent ModernPanel sources are forwarding nodes.
            ' The actual self-light guard is in PaintSourceRect, which repaints only the source itself.
            Dim forwardedSource As Control = Nothing
            If Not TryForwardTransparentSource(sourceForSampling, forwardedSource) Then
                mappings.Add(New KeyValuePair(Of Control, Rectangle)(sourceForSampling, New Rectangle(offset, childBounds.Size)))
                Return True
            End If
            If topologyNodes IsNot Nothing AndAlso Not topologyNodes.Contains(sourceForSampling) Then topologyNodes.Add(sourceForSampling)
            Dim sourceOffset = ComputeOffset(sourceForSampling, forwardedSource)
            offset = New Point(offset.X + sourceOffset.X, offset.Y + sourceOffset.Y)
            sourceForSampling = forwardedSource
        Loop
    End Function

    Private Sub RebuildGdiBitmap(source As Control, entry As Entry, sw As Integer, sh As Integer)
        Dim fullRebuild As Boolean = entry.FullDirty OrElse entry.Bmp Is Nothing OrElse entry.Width <> sw OrElse entry.Height <> sh

        Dim repaintRects As List(Of Rectangle)
        If fullRebuild OrElse entry.DirtyRects.Count = 0 Then
            repaintRects = New List(Of Rectangle) From {New Rectangle(0, 0, sw, sh)}
        Else
            repaintRects = New List(Of Rectangle)(entry.DirtyRects)
        End If

        Dim workingBmp As Bitmap = Nothing
        Dim oldBmp As Bitmap = Nothing
        Dim replacedEntryBitmap As Boolean
        Try
            Using D3D_PaintBridge.EnterBackgroundSamplingPaint()
                If fullRebuild OrElse entry.Bmp Is Nothing Then
                    workingBmp = New Bitmap(sw, sh, PixelFormat.Format32bppPArgb)
                    PaintSourceRectsToBitmap(source, workingBmp, repaintRects)
                Else
                    workingBmp = entry.Bmp
                    PaintSourceDirtyRectsInPlace(source, workingBmp, repaintRects)
                End If
            End Using

            If fullRebuild OrElse Not Object.ReferenceEquals(workingBmp, entry.Bmp) Then
                SyncLock _cache
                    oldBmp = entry.Bmp
                    entry.Bmp = workingBmp
                    workingBmp = Nothing
                    replacedEntryBitmap = True
                    InvalidateSharedUploads(entry, Nothing)
                    _sourceBitmapBytes -= entry.BitmapBytes
                    If _sourceBitmapBytes < 0 Then _sourceBitmapBytes = 0
                    entry.Width = sw
                    entry.Height = sh
                    entry.BitmapBytes = EstimateBitmapBytes(sw, sh)
                    _sourceBitmapBytes += entry.BitmapBytes
                End SyncLock
                If oldBmp IsNot Nothing Then
                    Try : oldBmp.Dispose() : Catch : End Try
                    oldBmp = Nothing
                End If
            End If

            If fullRebuild Then
                ' 同 V1：修复 D2D + GDI BitBlt 引发的 alpha=0 写穿问题，并顺手识别纯色采样结果。
                Dim solidArgb As Integer
                entry.IsSolidColor = ForceOpaqueAlphaAndDetectSolid(entry.Bmp, solidArgb)
                entry.SolidArgb = solidArgb
                If entry.IsSolidColor Then
                    ' Pure color sources can be redrawn from SolidArgb; keeping the full CPU bitmap only wastes RAM.
                    SyncLock _cache
                        InvalidateSharedUploads(entry, Nothing)
                        ReleaseSourceBitmap(entry)
                    End SyncLock
                End If
            Else
                ForceOpaqueAlpha(entry.Bmp, repaintRects)
                entry.IsSolidColor = False
            End If

            SyncLock _cache
                entry.Version += 1
                entry.FullDirty = False
                entry.DirtyRects.Clear()
            End SyncLock
        Finally
            If workingBmp IsNot Nothing AndAlso Not Object.ReferenceEquals(workingBmp, entry.Bmp) Then
                Try : workingBmp.Dispose() : Catch : End Try
            End If
            If replacedEntryBitmap AndAlso oldBmp IsNot Nothing Then
                Try : oldBmp.Dispose() : Catch : End Try
            End If
        End Try
    End Sub

    Private Sub PaintSourceRectsToBitmap(source As Control, target As Bitmap, repaintRects As IEnumerable(Of Rectangle))
        If source Is Nothing OrElse target Is Nothing OrElse repaintRects Is Nothing Then Return
        Dim targetBounds As New Rectangle(0, 0, target.Width, target.Height)
        Using bg As Graphics = Graphics.FromImage(target)
            For Each repaintRect In repaintRects
                repaintRect = Rectangle.Intersect(targetBounds, repaintRect)
                If repaintRect.Width <= 0 OrElse repaintRect.Height <= 0 Then Continue For
                PaintSourceRect(source, bg, repaintRect)
            Next
        End Using
    End Sub

    Private Sub PaintSourceDirtyRectsInPlace(source As Control, target As Bitmap, repaintRects As IEnumerable(Of Rectangle))
        If source Is Nothing OrElse target Is Nothing OrElse repaintRects Is Nothing Then Return
        Dim targetBounds As New Rectangle(0, 0, target.Width, target.Height)

        ' 局部重采必须保持 source 的原始坐标系。部分窗口级绘制会走 HDC/BitBlt，
        ' 不可靠继承 GDI+ TranslateTransform；若画到小 patch 再贴回，会把窗口顶部像素误贴到脏区。
        Using targetGraphics As Graphics = Graphics.FromImage(target)
            For Each repaintRect In repaintRects
                repaintRect = Rectangle.Intersect(targetBounds, repaintRect)
                If repaintRect.Width <= 0 OrElse repaintRect.Height <= 0 Then Continue For

                PaintSourceRect(source, targetGraphics, repaintRect)
            Next
        End Using
    End Sub

    Private Sub PaintSourceRect(source As Control, bg As Graphics, repaintRect As Rectangle)
        If source Is Nothing OrElse bg Is Nothing OrElse repaintRect.Width <= 0 OrElse repaintRect.Height <= 0 Then Return

        Dim oldMode = bg.CompositingMode
        bg.CompositingMode = Drawing2D.CompositingMode.SourceCopy
        Using transparentBrush As New SolidBrush(Color.Transparent)
            bg.FillRectangle(transparentBrush, repaintRect)
        End Using
        bg.CompositingMode = oldMode

        Dim state = bg.Save()
        Try
            bg.SetClip(repaintRect)
            Using pea As New PaintEventArgs(bg, repaintRect)
                InvokePaintBackgroundProxy(source, pea)
                InvokePaintProxy(source, pea)
            End Using
        Finally
            bg.Restore(state)
        End Try
    End Sub

    Private Function AcquireSharedSourceBitmap(entry As Entry, rt As ID2D1RenderTarget, sourceBounds As Rectangle) As BitmapAcquireResult
        If entry Is Nothing OrElse entry.Bmp Is Nothing OrElse rt Is Nothing Then Return Nothing

        Dim sourceRect = Rectangle.Intersect(New Rectangle(0, 0, entry.Bmp.Width, entry.Bmp.Height), sourceBounds)
        If sourceRect.Width <= 0 OrElse sourceRect.Height <= 0 Then Return Nothing

        Dim uploadWidth As Integer = entry.Bmp.Width
        Dim uploadHeight As Integer = entry.Bmp.Height
        Dim uploadBytes As Long = EstimateBitmapBytes(uploadWidth, uploadHeight)
        Dim cacheEnabled As Boolean = GlobalOptions.GpuCacheBudgetBytes > 0L AndAlso uploadBytes <= Math.Max(0L, GlobalOptions.GpuCacheBudgetBytes)

        If Not cacheEnabled AndAlso entry.SharedUploads.Count > 0 Then
            InvalidateSharedUploads(entry, Nothing)
        End If

        If cacheEnabled Then
            For Each upload In entry.SharedUploads
                Dim ownerAlive As Boolean = upload.D2DOwnerRT IsNot Nothing AndAlso ReferenceEquals(upload.D2DOwnerRT.Target, rt)
                If ownerAlive AndAlso upload.Width = uploadWidth AndAlso upload.Height = uploadHeight AndAlso upload.Version = entry.Version Then
                    upload.LastUsed = D3D_GpuCache.NextTick()
                    D3D_GpuCache.TrimToBudget(_gpuOwner)
                    Return New BitmapAcquireResult With {.Bitmap = upload.D2DBmp, .SourceRect = sourceRect}
                End If
            Next
        End If

        Dim newUpload As New Entry.SharedUploadEntry With {
            .Width = uploadWidth,
            .Height = uploadHeight,
            .Bytes = uploadBytes,
            .D2DBmp = D3D_D2DInterop.CreateBitmapFromGdi(rt, entry.Bmp),
            .D2DOwnerRT = New WeakReference(rt),
            .Version = entry.Version,
            .LastUsed = D3D_GpuCache.NextTick()
        }
        If newUpload.D2DBmp Is Nothing Then Return Nothing
        If Not cacheEnabled Then
            Return New BitmapAcquireResult With {.Bitmap = newUpload.D2DBmp, .SourceRect = sourceRect, .DisposeAfterDraw = True}
        End If
        _sharedUploadBytes += newUpload.Bytes
        entry.SharedUploads.Add(newUpload)
        D3D_GpuCache.TrimToBudget(_gpuOwner)
        Return New BitmapAcquireResult With {.Bitmap = newUpload.D2DBmp, .SourceRect = sourceRect}
    End Function

    Private Function TryAcquireContainedStockPanelBackgroundBitmap(source As Control, consumer As Control,
                                                                   rt As ID2D1RenderTarget, sourceRect As Rectangle,
                                                                   destRect As Rectangle,
                                                                   ByRef acquired As BitmapAcquireResult) As Boolean
        acquired = Nothing
        If source Is Nothing OrElse consumer Is Nothing OrElse rt Is Nothing Then Return False
        If Not IsPlainWinFormsPanel(source) Then Return False
        If Not ContainsControl(source, consumer) Then Return False
        If sourceRect.Width <= 0 OrElse sourceRect.Height <= 0 Then Return False

        Dim sourceBounds As New Rectangle(0, 0, source.Width, source.Height)
        Dim srcIntersection = Rectangle.Intersect(sourceBounds, sourceRect)
        If srcIntersection.Width <= 0 OrElse srcIntersection.Height <= 0 Then Return False

        Using bmp As New Bitmap(sourceRect.Width, sourceRect.Height, PixelFormat.Format32bppPArgb)
            Using bg = Graphics.FromImage(bmp)
                bg.CompositingMode = Drawing2D.CompositingMode.SourceCopy
                bg.Clear(Color.Transparent)
                bg.CompositingMode = Drawing2D.CompositingMode.SourceOver
                bg.TranslateTransform(-sourceRect.X, -sourceRect.Y)
                bg.SetClip(srcIntersection)
                Using pea As New PaintEventArgs(bg, srcIntersection)
                    InvokePaintBackgroundProxy(source, pea)
                End Using
            End Using
            Dim validRect As New Rectangle(srcIntersection.X - sourceRect.X, srcIntersection.Y - sourceRect.Y,
                                           srcIntersection.Width, srcIntersection.Height)
            ForceOpaqueAlpha(bmp, {validRect})
            acquired = New BitmapAcquireResult With {
                .Bitmap = D3D_D2DInterop.CreateBitmapFromGdi(rt, bmp),
                .SourceRect = New Rectangle(0, 0, sourceRect.Width, sourceRect.Height),
                .DrawDestRect = destRect,
                .DisposeAfterDraw = True
            }
        End Using

        Return acquired.Bitmap IsNot Nothing
    End Function

    Private Function GetOldestSharedUploadTick() As Long
        Dim oldest As Long = Long.MaxValue
        For Each entry In _cache.Values
            If entry Is Nothing Then Continue For
            For Each upload In entry.SharedUploads
                If upload IsNot Nothing AndAlso upload.LastUsed < oldest Then oldest = upload.LastUsed
            Next
        Next
        Return oldest
    End Function

    Private Function TrimOldestSharedUpload(protectedUpload As Entry.SharedUploadEntry) As Boolean
        Dim oldestEntry As Entry = Nothing
        Dim oldestIndex As Integer = -1
        Dim oldestUsed As Long = Long.MaxValue

        For Each entry In _cache.Values
            If entry Is Nothing OrElse entry.SharedUploads.Count = 0 Then Continue For
            For i As Integer = 0 To entry.SharedUploads.Count - 1
                Dim candidate = entry.SharedUploads(i)
                If protectedUpload IsNot Nothing AndAlso ReferenceEquals(candidate, protectedUpload) Then Continue For
                If candidate.LastUsed < oldestUsed Then
                    oldestEntry = entry
                    oldestIndex = i
                    oldestUsed = candidate.LastUsed
                End If
            Next
        Next

        If oldestEntry Is Nothing OrElse oldestIndex < 0 Then Return False
        ReleaseSharedUpload(oldestEntry, oldestIndex)
        Return True
    End Function

    Private Sub ReleaseSharedUpload(entry As Entry, index As Integer)
        If entry Is Nothing OrElse index < 0 OrElse index >= entry.SharedUploads.Count Then Return
        Dim removedUpload = entry.SharedUploads(index)
        Try : removedUpload.D2DBmp?.Dispose() : Catch : End Try
        _sharedUploadBytes -= removedUpload.Bytes
        If _sharedUploadBytes < 0 Then _sharedUploadBytes = 0
        entry.SharedUploads.RemoveAt(index)
    End Sub

    Private Function EstimateBitmapBytes(w As Integer, h As Integer) As Long
        Return CLng(Math.Max(1, w)) * CLng(Math.Max(1, h)) * 4L
    End Function

    Private Sub ReleaseSourceBitmap(entry As Entry)
        If entry Is Nothing OrElse entry.Bmp Is Nothing Then Return
        Try : entry.Bmp.Dispose() : Catch : End Try
        entry.Bmp = Nothing
        _sourceBitmapBytes -= entry.BitmapBytes
        If _sourceBitmapBytes < 0 Then _sourceBitmapBytes = 0
        entry.BitmapBytes = 0
    End Sub

    Private Sub ReleaseEntryCache(entry As Entry)
        If entry Is Nothing Then Return
        ReleaseSourceBitmap(entry)
        InvalidateSharedUploads(entry, Nothing)
        entry.FullDirty = True
        entry.DirtyRects.Clear()
        entry.IsSolidColor = False
        entry.SolidArgb = 0
    End Sub

    Private Sub RemoveSourceEntryNoLock(source As Control, entry As Entry)
        If source Is Nothing OrElse entry Is Nothing Then Return
        ClearAncestorSubscriptions(entry)
        ClearConsumerTopologies(entry)
        Try : RemoveHandler source.Disposed, AddressOf OnSourceDisposed : Catch : End Try
        Try : RemoveHandler source.HandleDestroyed, AddressOf OnSourceHandleDestroyed : Catch : End Try
        Try : RemoveHandler source.Invalidated, AddressOf OnSourceInvalidated : Catch : End Try
        Try : RemoveHandler source.LocationChanged, AddressOf OnSourceParentOrVisibleChanged : Catch : End Try
        Try : RemoveHandler source.ParentChanged, AddressOf OnSourceParentOrVisibleChanged : Catch : End Try
        Try : RemoveHandler source.Resize, AddressOf OnSourceResized : Catch : End Try
        Try : RemoveHandler source.VisibleChanged, AddressOf OnSourceParentOrVisibleChanged : Catch : End Try

        ReleaseEntryCache(entry)
        _cache.Remove(source)
    End Sub

    Private Sub ReleaseSource(source As Control)
        If source Is Nothing Then Return
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then
                RemoveSourceEntryNoLock(source, entry)
            End If
        End SyncLock
    End Sub

    Private Sub TrimSourceBitmaps(protectedEntry As Entry)
        While _sourceBitmapBytes > Math.Max(0L, GlobalOptions.CpuCacheBudgetBytes)
            If Not TrimOldestSourceBitmap(protectedEntry) Then Exit While
        End While
    End Sub

    Private Function GetOldestSourceBitmapTick() As Long
        Dim oldest As Long = Long.MaxValue
        For Each entry In _cache.Values
            If entry Is Nothing OrElse entry.Bmp Is Nothing OrElse entry.Painting Then Continue For
            If entry.LastUsed < oldest Then oldest = entry.LastUsed
        Next
        Return oldest
    End Function

    Private Function TrimOldestSourceBitmap(protectedEntry As Entry) As Boolean
        Dim oldest As Entry = Nothing
        For Each entry In _cache.Values
            If entry Is Nothing OrElse entry Is protectedEntry Then Continue For
            If entry.Bmp Is Nothing OrElse entry.Painting Then Continue For
            If oldest Is Nothing OrElse entry.LastUsed < oldest.LastUsed Then oldest = entry
        Next
        If oldest Is Nothing Then Return False
        InvalidateSharedUploads(oldest, Nothing)
        ReleaseSourceBitmap(oldest)
        oldest.FullDirty = True
        Return True
    End Function

    Private Function DirtyRectsIntersect(entry As Entry, rect As Rectangle) As Boolean
        If entry.FullDirty Then Return True
        For Each dirty In entry.DirtyRects
            If dirty.IntersectsWith(rect) Then Return True
        Next
        Return False
    End Function

    Private Sub AddDirtyRect(entry As Entry, dirtyRect As Rectangle, sourceBounds As Rectangle)
        If entry Is Nothing OrElse dirtyRect.Width <= 0 OrElse dirtyRect.Height <= 0 Then Return
        dirtyRect = Rectangle.Intersect(sourceBounds, dirtyRect)
        If dirtyRect.Width <= 0 OrElse dirtyRect.Height <= 0 Then Return

        If entry.FullDirty Then Return

        Dim mergedRect As Rectangle = dirtyRect
        Dim mergedAny As Boolean
        Do
            mergedAny = False
            For i As Integer = entry.DirtyRects.Count - 1 To 0 Step -1
                Dim existing = entry.DirtyRects(i)
                If existing.IntersectsWith(mergedRect) OrElse existing.Contains(mergedRect) OrElse mergedRect.Contains(existing) Then
                    mergedRect = Rectangle.Union(existing, mergedRect)
                    entry.DirtyRects.RemoveAt(i)
                    mergedAny = True
                End If
            Next
        Loop While mergedAny

        entry.DirtyRects.Add(mergedRect)

        Dim maxRects As Integer = Math.Max(1, GlobalOptions.BackgroundDirtyRectLimit)
        Dim ratio As Single = Math.Max(0.05F, Math.Min(1.0F, GlobalOptions.BackgroundFullDirtyRatio))
        Dim unionRect As Rectangle = Rectangle.Empty
        Dim totalArea As Long = 0
        For Each rect In entry.DirtyRects
            unionRect = If(unionRect.IsEmpty, rect, Rectangle.Union(unionRect, rect))
            totalArea += CLng(rect.Width) * CLng(rect.Height)
        Next
        Dim sourceArea As Long = CLng(Math.Max(1, sourceBounds.Width)) * CLng(Math.Max(1, sourceBounds.Height))
        Dim unionArea As Long = CLng(Math.Max(0, unionRect.Width)) * CLng(Math.Max(0, unionRect.Height))
        If entry.DirtyRects.Count > maxRects OrElse
           unionArea >= CLng(sourceArea * ratio) OrElse
           totalArea >= CLng(sourceArea * ratio) Then
            entry.FullDirty = True
            entry.DirtyRects.Clear()
        End If
    End Sub

    Private Sub InvalidateSharedUploads(entry As Entry, dirtyRect As Rectangle?)
        If entry Is Nothing Then Return
        ' Shared uploads cover the whole source, so any source pixel change invalidates every RT upload.
        For i As Integer = entry.SharedUploads.Count - 1 To 0 Step -1
            ReleaseSharedUpload(entry, i)
        Next
    End Sub

    Private Sub CollectConsumerInvalidations(source As Control, entry As Entry, dirtyRect As Rectangle?,
                                             invalidations As List(Of ConsumerInvalidation),
                                             Optional removeWhenEmpty As Boolean = True)
        If entry Is Nothing OrElse invalidations Is Nothing Then Return

        For i As Integer = entry.Consumers.Count - 1 To 0 Step -1
            Dim consumer = entry.Consumers(i)
            Dim child = TryCast(consumer.ChildRef.Target, Control)
            If child Is Nothing OrElse child.IsDisposed OrElse Not IsRenderableControl(child) Then
                ReleaseConsumerTopology(consumer)
                entry.Consumers.RemoveAt(i)
                Continue For
            End If

            If Not dirtyRect.HasValue Then
                invalidations.Add(New ConsumerInvalidation(child, consumer.DestRect))
                Continue For
            End If

            Dim sourceIntersection = Rectangle.Intersect(consumer.SourceRect, dirtyRect.Value)
            If sourceIntersection.Width <= 0 OrElse sourceIntersection.Height <= 0 Then Continue For

            Dim destRect As New Rectangle(
                consumer.DestRect.X + sourceIntersection.X - consumer.SourceRect.X,
                consumer.DestRect.Y + sourceIntersection.Y - consumer.SourceRect.Y,
                sourceIntersection.Width,
                sourceIntersection.Height)
            invalidations.Add(New ConsumerInvalidation(child, destRect))
        Next

        If removeWhenEmpty AndAlso source IsNot Nothing AndAlso entry.Consumers.Count = 0 Then
            RemoveSourceEntryNoLock(source, entry)
        End If
    End Sub

    Private Sub FlushConsumerInvalidations(invalidations As List(Of ConsumerInvalidation))
        If invalidations Is Nothing OrElse invalidations.Count = 0 Then Return

        For Each item In invalidations
            Dim child = item.Child
            If child Is Nothing OrElse child.IsDisposed OrElse Not child.IsHandleCreated OrElse Not IsRenderableControl(child) Then Continue For
            Dim rect As Rectangle = Rectangle.Intersect(New Rectangle(Point.Empty, child.ClientSize), item.Rect)
            If rect.Width <= 0 OrElse rect.Height <= 0 Then Continue For
            OuterToInnerRefreshScheduler.Request(child, rect)
        Next
    End Sub

#End Region

#Region "source 事件"

    Private Sub OnSourceInvalidated(sender As Object, e As InvalidateEventArgs)
        Dim source = TryCast(sender, Control)
        If source Is Nothing Then Return
        Dim invalidations As New List(Of ConsumerInvalidation)()
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then
                Dim dirtyRect As Rectangle = If(e IsNot Nothing, e.InvalidRect, Rectangle.Empty)
                If dirtyRect.Width <= 0 OrElse dirtyRect.Height <= 0 Then
                    entry.FullDirty = True
                    entry.DirtyRects.Clear()
                    InvalidateSharedUploads(entry, Nothing)
                    CollectConsumerInvalidations(source, entry, Nothing, invalidations)
                Else
                    AddDirtyRect(entry, dirtyRect, New Rectangle(0, 0, source.Width, source.Height))
                    InvalidateSharedUploads(entry, dirtyRect)
                    If entry.FullDirty Then
                        InvalidateSharedUploads(entry, Nothing)
                        CollectConsumerInvalidations(source, entry, Nothing, invalidations)
                    Else
                        CollectConsumerInvalidations(source, entry, dirtyRect, invalidations)
                    End If
                End If
            End If
        End SyncLock
        FlushConsumerInvalidations(invalidations)
    End Sub

    Private Sub OnSourceResized(sender As Object, e As EventArgs)
        Dim source = TryCast(sender, Control)
        If source Is Nothing Then Return
        Dim invalidations As New List(Of ConsumerInvalidation)()
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then
                entry.FullDirty = True
                entry.DirtyRects.Clear()
                InvalidateSharedUploads(entry, Nothing)
                CollectConsumerInvalidations(source, entry, Nothing, invalidations)
            End If
        End SyncLock
        OuterToInnerRefreshScheduler.RequestFull(source)
        FlushConsumerInvalidations(invalidations)
    End Sub

    Private Sub OnSourceDisposed(sender As Object, e As EventArgs)
        Dim source = TryCast(sender, Control)
        If source Is Nothing Then Return
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then
                RemoveSourceEntryNoLock(source, entry)
            End If
        End SyncLock
    End Sub

    Private Sub OnSourceHandleDestroyed(sender As Object, e As EventArgs)
        Dim source = TryCast(sender, Control)
        If source Is Nothing Then Return
        Dim invalidations As New List(Of ConsumerInvalidation)()
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then
                CollectConsumerInvalidations(source, entry, Nothing, invalidations, removeWhenEmpty:=False)
                RemoveSourceEntryNoLock(source, entry)
            End If
        End SyncLock
        FlushConsumerInvalidations(invalidations)
    End Sub

    Private Sub OnSourceParentOrVisibleChanged(sender As Object, e As EventArgs)
        Dim changed = TryCast(sender, Control)
        If changed Is Nothing Then Return
        Dim invalidations As New List(Of ConsumerInvalidation)()

        SyncLock _cache
            Dim affectedSources As New List(Of Control)()
            Dim entry As Entry = Nothing

            If _cache.TryGetValue(changed, entry) Then
                affectedSources.Add(changed)
            End If

            For Each kv In _cache
                If kv.Key Is changed Then Continue For
                If kv.Value IsNot Nothing AndAlso kv.Value.AncestorSubscriptions.Contains(changed) Then
                    affectedSources.Add(kv.Key)
                End If
            Next

            For Each source In affectedSources
                If source Is Nothing Then Continue For
                If Not _cache.TryGetValue(source, entry) Then Continue For
                If IsRenderableControl(source) Then
                    RefreshAncestorSubscriptions(source, entry)
                    entry.FullDirty = True
                    entry.DirtyRects.Clear()
                    InvalidateSharedUploads(entry, Nothing)
                    CollectConsumerInvalidations(source, entry, Nothing, invalidations, removeWhenEmpty:=False)
                    If entry.Consumers.Count = 0 Then RemoveSourceEntryNoLock(source, entry)
                    Continue For
                End If

                CollectConsumerInvalidations(source, entry, Nothing, invalidations, removeWhenEmpty:=False)
                RemoveSourceEntryNoLock(source, entry)
            Next
        End SyncLock

        FlushConsumerInvalidations(invalidations)
    End Sub

    Private Sub OnSourceAncestorInvalidated(sender As Object, e As InvalidateEventArgs)
        Dim ancestor = TryCast(sender, Control)
        If ancestor Is Nothing Then Return
        Dim dirtyRect As Rectangle = If(e IsNot Nothing, e.InvalidRect, Rectangle.Empty)
        InvalidateSourcesForAncestorChange(ancestor, dirtyRect)
    End Sub

    Private Sub OnSourceAncestorResized(sender As Object, e As EventArgs)
        Dim ancestor = TryCast(sender, Control)
        If ancestor Is Nothing Then Return
        InvalidateSourcesForAncestorChange(ancestor, Rectangle.Empty)
    End Sub

    Private Sub OnForwarderTopologyChanged(sender As Object, e As EventArgs)
        Dim changed = TryCast(sender, Control)
        If changed Is Nothing Then Return

        Dim invalidations As New List(Of ConsumerInvalidation)()
        SyncLock _cache
            For Each kv In _cache.ToArray()
                Dim source = kv.Key
                Dim entry = kv.Value
                If entry Is Nothing Then Continue For
                For i As Integer = entry.Consumers.Count - 1 To 0 Step -1
                    Dim consumer = entry.Consumers(i)
                    If Not ConsumerUsesTopology(consumer, changed) Then Continue For

                    Dim child = TryCast(consumer.ChildRef.Target, Control)
                    If child Is Nothing OrElse child.IsDisposed OrElse changed.IsDisposed OrElse Not IsRenderableControl(changed) Then
                        ReleaseConsumerTopology(consumer)
                        entry.Consumers.RemoveAt(i)
                    Else
                        invalidations.Add(New ConsumerInvalidation(child, consumer.DestRect))
                    End If
                Next
                If entry.Consumers.Count = 0 Then RemoveSourceEntryNoLock(source, entry)
            Next
        End SyncLock

        FlushConsumerInvalidations(invalidations)
    End Sub

    Private Sub InvalidateSourcesForAncestorChange(ancestor As Control, ancestorDirtyRect As Rectangle)
        If ancestor Is Nothing Then Return
        Dim invalidations As New List(Of ConsumerInvalidation)()

        SyncLock _cache
            For Each kv In _cache.ToArray()
                Dim source = kv.Key
                Dim entry = kv.Value
                If source Is Nothing OrElse entry Is Nothing Then Continue For
                If source Is ancestor Then Continue For
                If Not entry.AncestorSubscriptions.Contains(ancestor) Then Continue For

                Dim dirtyRect As Rectangle = Rectangle.Empty
                If ancestorDirtyRect.Width > 0 AndAlso ancestorDirtyRect.Height > 0 Then
                    dirtyRect = MapRectangleBetweenControls(ancestor, source, ancestorDirtyRect)
                    dirtyRect = Rectangle.Intersect(New Rectangle(0, 0, source.Width, source.Height), dirtyRect)
                    If dirtyRect.Width <= 0 OrElse dirtyRect.Height <= 0 Then Continue For
                End If

                If dirtyRect.Width <= 0 OrElse dirtyRect.Height <= 0 Then
                    entry.FullDirty = True
                    entry.DirtyRects.Clear()
                    InvalidateSharedUploads(entry, Nothing)
                    CollectConsumerInvalidations(source, entry, Nothing, invalidations, removeWhenEmpty:=False)
                    If entry.Consumers.Count = 0 Then RemoveSourceEntryNoLock(source, entry)
                Else
                    AddDirtyRect(entry, dirtyRect, New Rectangle(0, 0, source.Width, source.Height))
                    InvalidateSharedUploads(entry, dirtyRect)
                    If entry.FullDirty Then
                        InvalidateSharedUploads(entry, Nothing)
                        CollectConsumerInvalidations(source, entry, Nothing, invalidations, removeWhenEmpty:=False)
                    Else
                        CollectConsumerInvalidations(source, entry, dirtyRect, invalidations, removeWhenEmpty:=False)
                    End If
                    If entry.Consumers.Count = 0 Then RemoveSourceEntryNoLock(source, entry)
                End If
            Next
        End SyncLock

        FlushConsumerInvalidations(invalidations)
    End Sub

#End Region

#Region "辅助"

    Private Sub RefreshAncestorSubscriptions(source As Control, entry As Entry)
        If entry Is Nothing Then Return
        ClearAncestorSubscriptions(entry)
        If source Is Nothing OrElse source.IsDisposed Then Return

        Dim current = source.Parent
        While current IsNot Nothing
            AddAncestorSubscription(entry, current)
            current = current.Parent
        End While
    End Sub

    Private Sub ClearAncestorSubscriptions(entry As Entry)
        If entry Is Nothing OrElse entry.AncestorSubscriptions.Count = 0 Then Return

        For Each ancestor In entry.AncestorSubscriptions
            If ancestor Is Nothing Then Continue For
            ReleaseAncestorSubscription(ancestor)
        Next
        entry.AncestorSubscriptions.Clear()
    End Sub

    Private Function CloneTopologyNodes(topologyNodes As List(Of Control)) As List(Of Control)
        If topologyNodes Is Nothing OrElse topologyNodes.Count = 0 Then Return Nothing
        Dim result As New List(Of Control)()
        For Each node In topologyNodes
            If node Is Nothing OrElse node.IsDisposed OrElse result.Contains(node) Then Continue For
            result.Add(node)
        Next
        If result.Count = 0 Then Return Nothing
        Return result
    End Function

    Private Sub AcquireConsumerTopology(consumer As Entry.ConsumerEntry)
        If consumer Is Nothing OrElse consumer.TopologyRefs Is Nothing Then Return
        For Each node In consumer.TopologyRefs
            AddTopologySubscription(node)
        Next
    End Sub

    Private Sub ReleaseConsumerTopology(consumer As Entry.ConsumerEntry)
        If consumer Is Nothing OrElse consumer.TopologyRefs Is Nothing Then Return
        For Each node In consumer.TopologyRefs
            ReleaseTopologySubscription(node)
        Next
        consumer.TopologyRefs = Nothing
    End Sub

    Private Sub ClearConsumerTopologies(entry As Entry)
        If entry Is Nothing Then Return
        For Each consumer In entry.Consumers
            ReleaseConsumerTopology(consumer)
        Next
        entry.Consumers.Clear()
    End Sub

    Private Function ConsumerUsesTopology(consumer As Entry.ConsumerEntry, node As Control) As Boolean
        If consumer Is Nothing OrElse node Is Nothing OrElse consumer.TopologyRefs Is Nothing Then Return False
        For Each existing In consumer.TopologyRefs
            If existing Is node Then Return True
        Next
        Return False
    End Function

    Private Sub AddTopologySubscription(node As Control)
        If node Is Nothing Then Return

        Dim refCount As Integer = 0
        If _topologySubscriptionRefs.TryGetValue(node, refCount) Then
            _topologySubscriptionRefs(node) = refCount + 1
        Else
            _topologySubscriptionRefs(node) = 1
            AddHandler node.Disposed, AddressOf OnForwarderTopologyChanged
            AddHandler node.HandleDestroyed, AddressOf OnForwarderTopologyChanged
            AddHandler node.LocationChanged, AddressOf OnForwarderTopologyChanged
            AddHandler node.ParentChanged, AddressOf OnForwarderTopologyChanged
            AddHandler node.Resize, AddressOf OnForwarderTopologyChanged
            AddHandler node.VisibleChanged, AddressOf OnForwarderTopologyChanged
        End If
    End Sub

    Private Sub ReleaseTopologySubscription(node As Control)
        If node Is Nothing Then Return

        Dim refCount As Integer = 0
        If Not _topologySubscriptionRefs.TryGetValue(node, refCount) Then Return

        If refCount > 1 Then
            _topologySubscriptionRefs(node) = refCount - 1
            Return
        End If

        _topologySubscriptionRefs.Remove(node)
        Try : RemoveHandler node.Disposed, AddressOf OnForwarderTopologyChanged : Catch : End Try
        Try : RemoveHandler node.HandleDestroyed, AddressOf OnForwarderTopologyChanged : Catch : End Try
        Try : RemoveHandler node.LocationChanged, AddressOf OnForwarderTopologyChanged : Catch : End Try
        Try : RemoveHandler node.ParentChanged, AddressOf OnForwarderTopologyChanged : Catch : End Try
        Try : RemoveHandler node.Resize, AddressOf OnForwarderTopologyChanged : Catch : End Try
        Try : RemoveHandler node.VisibleChanged, AddressOf OnForwarderTopologyChanged : Catch : End Try
    End Sub

    Private Sub AddAncestorSubscription(entry As Entry, ancestor As Control)
        If entry Is Nothing OrElse ancestor Is Nothing Then Return

        Dim refCount As Integer = 0
        If _ancestorSubscriptionRefs.TryGetValue(ancestor, refCount) Then
            _ancestorSubscriptionRefs(ancestor) = refCount + 1
        Else
            _ancestorSubscriptionRefs(ancestor) = 1
            AddHandler ancestor.Disposed, AddressOf OnSourceParentOrVisibleChanged
            AddHandler ancestor.HandleDestroyed, AddressOf OnSourceParentOrVisibleChanged
            AddHandler ancestor.Invalidated, AddressOf OnSourceAncestorInvalidated
            AddHandler ancestor.ParentChanged, AddressOf OnSourceParentOrVisibleChanged
            AddHandler ancestor.Resize, AddressOf OnSourceAncestorResized
            AddHandler ancestor.VisibleChanged, AddressOf OnSourceParentOrVisibleChanged
        End If

        entry.AncestorSubscriptions.Add(ancestor)
    End Sub

    Private Sub ReleaseAncestorSubscription(ancestor As Control)
        If ancestor Is Nothing Then Return

        Dim refCount As Integer = 0
        If Not _ancestorSubscriptionRefs.TryGetValue(ancestor, refCount) Then Return

        If refCount > 1 Then
            _ancestorSubscriptionRefs(ancestor) = refCount - 1
            Return
        End If

        _ancestorSubscriptionRefs.Remove(ancestor)
        Try : RemoveHandler ancestor.Disposed, AddressOf OnSourceParentOrVisibleChanged : Catch : End Try
        Try : RemoveHandler ancestor.HandleDestroyed, AddressOf OnSourceParentOrVisibleChanged : Catch : End Try
        Try : RemoveHandler ancestor.Invalidated, AddressOf OnSourceAncestorInvalidated : Catch : End Try
        Try : RemoveHandler ancestor.ParentChanged, AddressOf OnSourceParentOrVisibleChanged : Catch : End Try
        Try : RemoveHandler ancestor.Resize, AddressOf OnSourceAncestorResized : Catch : End Try
        Try : RemoveHandler ancestor.VisibleChanged, AddressOf OnSourceParentOrVisibleChanged : Catch : End Try
    End Sub

    Private Function IsRenderableControl(ctrl As Control) As Boolean
        If ctrl Is Nothing OrElse ctrl.IsDisposed Then Return False
        If ctrl.Width <= 0 OrElse ctrl.Height <= 0 Then Return False
        If Not ctrl.IsHandleCreated Then Return False

        Dim current As Control = ctrl
        While current IsNot Nothing
            If current.IsDisposed OrElse Not current.Visible Then Return False
            current = current.Parent
        End While

        Dim form As Form = ctrl.FindForm()
        If form IsNot Nothing Then
            If form.IsDisposed OrElse Not form.Visible OrElse form.WindowState = FormWindowState.Minimized Then Return False
        End If

        Return True
    End Function

    Private Function ComputeOffset(child As Control, source As Control) As Point
        Dim ox As Integer = 0, oy As Integer = 0
        Dim ctrl As Control = child
        While ctrl IsNot Nothing AndAlso ctrl IsNot source
            ox += ctrl.Left
            oy += ctrl.Top
            ctrl = ctrl.Parent
        End While
        If ctrl Is source Then Return New Point(ox, oy)
        Return source.PointToClient(child.PointToScreen(Point.Empty))
    End Function

    Private Function MapRectangleBetweenControls(fromControl As Control, toControl As Control, rect As Rectangle) As Rectangle
        If fromControl Is Nothing OrElse toControl Is Nothing Then Return Rectangle.Empty
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return Rectangle.Empty
        Try
            Dim screenTopLeft = fromControl.PointToScreen(rect.Location)
            Dim screenBottomRight = fromControl.PointToScreen(New Point(rect.Right, rect.Bottom))
            Dim targetTopLeft = toControl.PointToClient(screenTopLeft)
            Dim targetBottomRight = toControl.PointToClient(screenBottomRight)
            Return Rectangle.FromLTRB(targetTopLeft.X, targetTopLeft.Y, targetBottomRight.X, targetBottomRight.Y)
        Catch
            Return Rectangle.Empty
        End Try
    End Function

    Private Function ContainsControl(ancestor As Control, descendant As Control) As Boolean
        If ancestor Is Nothing OrElse descendant Is Nothing Then Return False
        Dim current As Control = descendant
        While current IsNot Nothing
            If current Is ancestor Then Return True
            current = current.Parent
        End While
        Return False
    End Function

    Private Function IsPlainWinFormsPanel(ctrl As Control) As Boolean
        Return ctrl IsNot Nothing AndAlso Object.ReferenceEquals(ctrl.GetType(), GetType(Panel))
    End Function

    Private ReadOnly _invokePaintBackground As Action(Of Control, Control, PaintEventArgs) =
        CreateInvoker("InvokePaintBackground")
    Private ReadOnly _invokePaint As Action(Of Control, Control, PaintEventArgs) =
        CreateInvoker("InvokePaint")

    Private Function CreateInvoker(name As String) As Action(Of Control, Control, PaintEventArgs)
        Dim mi = GetType(Control).GetMethod(name,
            Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic,
            Nothing, New Type() {GetType(Control), GetType(PaintEventArgs)}, Nothing)
        Return CType(mi.CreateDelegate(GetType(Action(Of Control, Control, PaintEventArgs))),
                     Action(Of Control, Control, PaintEventArgs))
    End Function

    Private Sub InvokePaintBackgroundProxy(source As Control, pea As PaintEventArgs)
        _invokePaintBackground(source, source, pea)
    End Sub

    Private Sub InvokePaintProxy(source As Control, pea As PaintEventArgs)
        _invokePaint(source, source, pea)
    End Sub

    ''' <summary>把 32bpp 位图的 alpha 全部置为 255，并检测结果是否为纯色。</summary>
    Private Function ForceOpaqueAlphaAndDetectSolid(bmp As Bitmap, ByRef solidArgb As Integer) As Boolean
        solidArgb = 0
        If bmp Is Nothing Then Return False
        Dim rect As New Rectangle(0, 0, bmp.Width, bmp.Height)
        Dim data As BitmapData = Nothing
        Try
            data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb)
            Dim stride As Integer = data.Stride
            Dim rowBytes As Integer = Math.Abs(stride)
            Dim h As Integer = data.Height
            Dim w As Integer = data.Width
            Dim scan0 As IntPtr = data.Scan0
            Dim row() As Byte = ArrayPool(Of Byte).Shared.Rent(rowBytes)
            Dim haveFirst As Boolean = False
            Dim firstB As Byte = 0, firstG As Byte = 0, firstR As Byte = 0
            Dim solid As Boolean = True
            Try
                For y As Integer = 0 To h - 1
                    Dim rowPtr As IntPtr = IntPtr.Add(scan0, y * stride)
                    Runtime.InteropServices.Marshal.Copy(rowPtr, row, 0, rowBytes)
                    Dim x As Integer = 3
                    For i As Integer = 0 To w - 1
                        Dim b As Byte = row(x - 3)
                        Dim g As Byte = row(x - 2)
                        Dim r As Byte = row(x - 1)
                        If Not haveFirst Then
                            firstB = b : firstG = g : firstR = r
                            haveFirst = True
                        ElseIf solid AndAlso (b <> firstB OrElse g <> firstG OrElse r <> firstR) Then
                            solid = False
                        End If
                        row(x) = 255
                        x += 4
                    Next
                    Runtime.InteropServices.Marshal.Copy(row, 0, rowPtr, rowBytes)
                Next
            Finally
                ArrayPool(Of Byte).Shared.Return(row)
            End Try
            If haveFirst Then solidArgb = Color.FromArgb(255, firstR, firstG, firstB).ToArgb()
            Return solid AndAlso haveFirst
        Catch
            Return False
        Finally
            If data IsNot Nothing Then
                Try : bmp.UnlockBits(data) : Catch : End Try
            End If
        End Try
    End Function

    Private Sub ForceOpaqueAlpha(bmp As Bitmap, rects As IEnumerable(Of Rectangle))
        If bmp Is Nothing OrElse rects Is Nothing Then Return
        Dim bounds As New Rectangle(0, 0, bmp.Width, bmp.Height)
        For Each rect In rects
            rect = Rectangle.Intersect(bounds, rect)
            If rect.Width <= 0 OrElse rect.Height <= 0 Then Continue For
            Dim data As BitmapData = Nothing
            Try
                data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb)
                Dim stride As Integer = data.Stride
                Dim rowBytes As Integer = Math.Abs(stride)
                Dim row() As Byte = ArrayPool(Of Byte).Shared.Rent(rowBytes)
                Try
                    For y As Integer = 0 To data.Height - 1
                        Dim rowPtr As IntPtr = IntPtr.Add(data.Scan0, y * stride)
                        Runtime.InteropServices.Marshal.Copy(rowPtr, row, 0, rowBytes)
                        Dim x As Integer = 3
                        For i As Integer = 0 To data.Width - 1
                            row(x) = 255
                            x += 4
                        Next
                        Runtime.InteropServices.Marshal.Copy(row, 0, rowPtr, rowBytes)
                    Next
                Finally
                    ArrayPool(Of Byte).Shared.Return(row)
                End Try
            Catch
            Finally
                If data IsNot Nothing Then
                    Try : bmp.UnlockBits(data) : Catch : End Try
                End If
            End Try
        Next
    End Sub

#End Region

End Module
