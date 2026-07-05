Imports System.Numerics
Imports Vortice.Direct2D1
Imports Vortice.Mathematics

''' <summary>
''' D3D_BackgroundGraph 维护 V3 背景 source/consumer 关系，并在窗口级 GPU 路线中生成可采样的 source snapshot。
''' 它不走 InvokePaint/HDC，也不让控件在主 swap chain 上重画外层 source。
''' </summary>
Public NotInheritable Class D3D_BackgroundGraph
    Implements IDisposable

    Private ReadOnly _textureCache As D3D_TextureCache
    Private ReadOnly _deviceManager As D3D_DeviceManager
    Private ReadOnly _compositor As D3D_WindowCompositor
    Private ReadOnly _relations As New Dictionary(Of Control, BackgroundRelation)()
    Private ReadOnly _consumerTopologies As New Dictionary(Of Control, List(Of Control))()
    Private ReadOnly _consumerSubscriptions As New HashSet(Of Control)()
    Private ReadOnly _sourceSubscriptionRefs As New Dictionary(Of Control, Integer)()
    Private ReadOnly _sourceAncestorNodes As New Dictionary(Of Control, List(Of Control))()
    Private ReadOnly _ancestorSubscriptionRefs As New Dictionary(Of Control, Integer)()
    Private ReadOnly _topologySubscriptionRefs As New Dictionary(Of Control, Integer)()
    Private ReadOnly _renderedSnapshotKeys As New HashSet(Of String)(StringComparer.Ordinal)
    Private ReadOnly _activeSnapshotKeys As New HashSet(Of String)(StringComparer.Ordinal)
    Private ReadOnly _snapshotStack As New HashSet(Of Control)()
    Private ReadOnly _pendingSnapshotReleases As New HashSet(Of Control)()
    Private ReadOnly _invalidatingSources As New HashSet(Of Control)()
    Private ReadOnly _offscreenContexts As New List(Of ID2D1DeviceContext)()
    Private _offscreenDeviceGeneration As Integer = -1
    Private _renderedSnapshotFrame As Integer = -1
    Private _renderedSnapshotDeviceGeneration As Integer = -1
    Private _disposed As Boolean

    Public Sub New(textureCache As D3D_TextureCache, deviceManager As D3D_DeviceManager, compositor As D3D_WindowCompositor)
        If textureCache Is Nothing Then Throw New ArgumentNullException(NameOf(textureCache))
        If deviceManager Is Nothing Then Throw New ArgumentNullException(NameOf(deviceManager))
        If compositor Is Nothing Then Throw New ArgumentNullException(NameOf(compositor))
        _textureCache = textureCache
        _deviceManager = deviceManager
        _compositor = compositor
    End Sub

    Public Sub RegisterConsumer(consumer As Control,
                                source As Control,
                                Optional topologyNodes As IEnumerable(Of Control) = Nothing,
                                Optional sourceRect As RectangleF? = Nothing,
                                Optional destinationRect As RectangleF? = Nothing)
        If consumer Is Nothing Then Return
        If source Is Nothing Then
            RemoveConsumerRelation(consumer)
            Return
        End If

        Dim oldRelation As BackgroundRelation = Nothing
        If _relations.TryGetValue(consumer, oldRelation) AndAlso oldRelation IsNot Nothing AndAlso oldRelation.Source Is source Then
            oldRelation.SourceRect = If(sourceRect.HasValue, sourceRect.Value, RectangleF.Empty)
            oldRelation.DestinationRect = If(destinationRect.HasValue, destinationRect.Value, RectangleF.Empty)
            AttachConsumerLifecycle(consumer)
            SetConsumerTopology(consumer, topologyNodes)
            Return
        End If

        If oldRelation IsNot Nothing AndAlso oldRelation.Source IsNot Nothing Then ReleaseSourceSubscription(oldRelation.Source)
        _relations(consumer) = New BackgroundRelation With {
            .Source = source,
            .SourceRect = If(sourceRect.HasValue, sourceRect.Value, RectangleF.Empty),
            .DestinationRect = If(destinationRect.HasValue, destinationRect.Value, RectangleF.Empty)
        }
        AttachConsumerLifecycle(consumer)
        SetConsumerTopology(consumer, topologyNodes)
        AddSourceSubscription(source)
    End Sub

    Public Function TryGetSource(consumer As Control, ByRef source As Control) As Boolean
        source = Nothing
        If consumer Is Nothing Then Return False
        Dim relation As BackgroundRelation = Nothing
        If Not _relations.TryGetValue(consumer, relation) OrElse relation Is Nothing Then Return False
        source = relation.Source
        Return source IsNot Nothing
    End Function

    Public Sub UnregisterConsumer(consumer As Control, Optional recursive As Boolean = False)
        If consumer Is Nothing Then Return

        RemoveConsumerRelation(consumer)
        If Not recursive Then Return

        Dim children As Control() = Nothing
        Try
            children = consumer.Controls.Cast(Of Control)().ToArray()
        Catch
            children = Array.Empty(Of Control)()
        End Try

        For Each child In children
            UnregisterConsumer(child, recursive:=True)
        Next
    End Sub

    Public Sub InvalidateSource(source As Control)
        InvalidateSource(source, Rectangle.Empty)
    End Sub

    Public Sub InvalidateSource(source As Control, dirtyRect As Rectangle)
        If _disposed OrElse source Is Nothing Then Return
        If _invalidatingSources.Contains(source) Then Return

        _invalidatingSources.Add(source)
        Try
            ReleaseSnapshot(source)

            Dim invalidations = CollectConsumerInvalidations(source, dirtyRect)
            For Each invalidation In invalidations
                RequestConsumerRender(invalidation.Consumer, invalidation.DirtyRect)
            Next
            RequestSourceRender(source, dirtyRect)
        Finally
            _invalidatingSources.Remove(source)
        End Try
    End Sub

    Public Sub InvalidateSnapshotsForRenderedControl(changedControl As Control, dirtyRect As Rectangle)
        If _disposed OrElse changedControl Is Nothing OrElse changedControl.IsDisposed Then Return

        Dim relations = _relations.ToArray()
        For Each kv In relations
            Dim consumer = kv.Key
            Dim relation = kv.Value
            If consumer Is Nothing OrElse consumer.IsDisposed OrElse relation Is Nothing Then Continue For
            Dim source = relation.Source
            If source Is Nothing OrElse source.IsDisposed OrElse source Is changedControl Then Continue For
            If Not IsDescendantOrSelf(changedControl, source) Then Continue For
            If IsDescendantOrSelf(changedControl, consumer) Then Continue For

            Dim sourceDirty As Rectangle = Rectangle.Empty
            If dirtyRect.Width > 0 AndAlso dirtyRect.Height > 0 Then
                sourceDirty = MapRectangleBetweenControls(changedControl, source, dirtyRect)
                Dim sourceSize = GetSourceControlSize(source)
                sourceDirty = Rectangle.Intersect(New Rectangle(Point.Empty, sourceSize), sourceDirty)
                If sourceDirty.Width <= 0 OrElse sourceDirty.Height <= 0 Then Continue For
            End If

            Dim consumerDirty = ResolveConsumerDirtyFromSourceDirty(relation, sourceDirty, consumer)
            If consumerDirty.Width <= 0 OrElse consumerDirty.Height <= 0 Then Continue For

            ReleaseSnapshot(source, consumer)
            RequestConsumerRender(consumer, consumerDirty)
        Next
    End Sub

    Friend Function DrawMappedBackground(context As D3D_PaintContext,
                                         consumer As Control,
                                         explicitSource As Control,
                                         destination As RectangleF) As Boolean
        If _disposed OrElse context Is Nothing Then Return False
        If consumer Is Nothing OrElse consumer.IsDisposed Then Return False
        If explicitSource Is Nothing OrElse explicitSource.IsDisposed Then Return False
        If destination.Width <= 0 OrElse destination.Height <= 0 Then Return False

        Try
            Dim mapping As BackgroundMapping = Nothing
            If Not TryResolveMapping(context, consumer, explicitSource, destination, mapping) Then
                RegisterConsumer(consumer, Nothing)
                Return False
            End If

            RegisterConsumer(consumer, mapping.Source, mapping.TopologyNodes, mapping.SourceRect, mapping.DestinationRect)
            Dim snapshot = GetOrRenderSnapshot(context, mapping.Source, mapping.SourceSize, consumer)
            If snapshot Is Nothing Then Return False

            Dim dst As Vortice.RawRectF? = D3D_PaintContext.ToRawRect(mapping.DestinationRect)
            Dim src As Vortice.RawRectF? = D3D_PaintContext.ToRawRect(mapping.SourceRect)
            context.DeviceContext.DrawBitmap(snapshot, dst, 1.0F, InterpolationMode.Linear, src, Nothing)
            Return True
        Catch ex As Exception
            If _deviceManager.HandleDeviceLost(ex) Then
                context.Compositor.HandleDeviceLost()
                Return False
            End If
            Throw
        End Try
    End Function

    Private Function TryResolveMapping(context As D3D_PaintContext,
                                       consumer As Control,
                                       explicitSource As Control,
                                       destination As RectangleF,
                                       ByRef mapping As BackgroundMapping) As Boolean
        mapping = Nothing

        Dim source = explicitSource
        Dim topologyNodes As New List(Of Control)()
        If Not ResolveTransparentSourceChain(source, consumer, topologyNodes) Then Return False
        If source Is Nothing OrElse source.IsDisposed Then Return False
        Dim sourceSize = GetSourceRenderSize(context, source)
        If sourceSize.Width <= 0 OrElse sourceSize.Height <= 0 Then Return False
        If Not IsRenderableSource(context, source) Then Return False

        Dim sourceOrigin As Point = Point.Empty
        If Not TryMapPointBetweenControls(consumer, source, Point.Empty, sourceOrigin) Then Return False
        Dim sourceRect As New RectangleF(
            sourceOrigin.X + destination.X,
            sourceOrigin.Y + destination.Y,
            destination.Width,
            destination.Height)

        Dim sourceBounds As New RectangleF(0, 0, sourceSize.Width, sourceSize.Height)
        Dim visibleSource = RectangleF.Intersect(sourceRect, sourceBounds)
        If visibleSource.Width <= 0 OrElse visibleSource.Height <= 0 Then Return False

        Dim dx = visibleSource.X - sourceRect.X
        Dim dy = visibleSource.Y - sourceRect.Y
        Dim visibleDestination As New RectangleF(
            destination.X + dx,
            destination.Y + dy,
            visibleSource.Width,
            visibleSource.Height)

        mapping = New BackgroundMapping With {
            .Source = source,
            .SourceSize = sourceSize,
            .SourceRect = visibleSource,
            .DestinationRect = visibleDestination,
            .TopologyNodes = topologyNodes
        }
        Return True
    End Function

    Private Shared Function GetSourceRenderSize(context As D3D_PaintContext, source As Control) As System.Drawing.Size
        If source Is Nothing OrElse source.IsDisposed Then Return System.Drawing.Size.Empty

        Dim form = TryCast(source, Form)
        If form IsNot Nothing AndAlso context IsNot Nothing AndAlso source Is context.Compositor.Form Then
            Return context.Compositor.RenderTargetSize
        End If
        If form IsNot Nothing Then Return form.ClientSize

        Return source.Size
    End Function

    Private Function ResolveTransparentSourceChain(ByRef source As Control,
                                                   consumer As Control,
                                                   topologyNodes As List(Of Control)) As Boolean
        Dim visited As New HashSet(Of Control)()
        Do
            If source Is Nothing OrElse source.IsDisposed Then Return False
            If visited.Contains(source) Then Return False
            visited.Add(source)

            If IsDescendantOrSelf(consumer, source) Then Return True

            Dim forwardedSource As Control = Nothing
            If Not TryForwardTransparentSource(source, forwardedSource) Then Return True
            If topologyNodes IsNot Nothing AndAlso Not topologyNodes.Contains(source) Then topologyNodes.Add(source)
            source = forwardedSource
        Loop
    End Function

    Private Shared Function TryForwardTransparentSource(source As Control, ByRef forwardedSource As Control) As Boolean
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

    Private Shared Function IsRenderableSource(context As D3D_PaintContext, source As Control) As Boolean
        If context Is Nothing OrElse source Is Nothing OrElse source.IsDisposed Then Return False
        If Not IsRenderableControl(source, context.Compositor.Form) Then Return False
        If TypeOf source Is Form Then
            If source Is context.Compositor.Form Then Return True
            If D3D_RenderCore.ResolveCompositorForm(source) IsNot context.Compositor.Form Then Return False
            Return ContainsGpuRenderableDescendant(source)
        End If
        If D3D_RenderCore.ResolveCompositorForm(source) IsNot context.Compositor.Form Then Return False
        Return TypeOf source Is V3_IGpuRenderable OrElse ContainsGpuRenderableDescendant(source)
    End Function

    Private Function GetOrRenderSnapshot(context As D3D_PaintContext,
                                         source As Control,
                                         snapshotSize As System.Drawing.Size,
                                         excludedConsumer As Control) As ID2D1Bitmap1
        If snapshotSize.Width <= 0 OrElse snapshotSize.Height <= 0 Then Return Nothing
        PrepareFrame(context)

        If _pendingSnapshotReleases.Remove(source) Then ReleaseSnapshot(source)

        Dim key = BuildSnapshotKey(context, source, snapshotSize, excludedConsumer)
        If _activeSnapshotKeys.Contains(key) Then Return Nothing
        Dim hasRenderedSnapshot = _renderedSnapshotKeys.Contains(key) AndAlso
                                  _textureCache.ContainsTexture(Of ID2D1Bitmap1)(key, context.DeviceGeneration)

        Dim bytes = CLng(snapshotSize.Width) * CLng(snapshotSize.Height) * 4L
        Dim snapshot = _textureCache.AcquireTexture(Of ID2D1Bitmap1)(
            key,
            context.DeviceGeneration,
            bytes,
            Function()
                Dim props As New BitmapProperties1(
                    New Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                    96.0F,
                    96.0F,
                    BitmapOptions.Target)
                Return context.DeviceContext.CreateBitmap(New SizeI(snapshotSize.Width, snapshotSize.Height), IntPtr.Zero, 0UI, props)
            End Function)

        If snapshot Is Nothing Then Return Nothing
        If hasRenderedSnapshot Then Return snapshot
        _activeSnapshotKeys.Add(key)
        Try
            If Not RenderSnapshot(context, source, snapshot, snapshotSize, excludedConsumer) Then
                _textureCache.Release(key)
                Return Nothing
            End If
            If _pendingSnapshotReleases.Remove(source) Then
                ReleaseSnapshot(source)
                Return Nothing
            End If
            _renderedSnapshotKeys.Add(key)
        Finally
            _activeSnapshotKeys.Remove(key)
        End Try
        Return snapshot
    End Function

    Private Sub PrepareFrame(context As D3D_PaintContext)
        Dim generationChanged = _renderedSnapshotDeviceGeneration <> context.DeviceGeneration
        Dim frameChanged = _renderedSnapshotFrame <> context.FrameGeneration OrElse generationChanged

        If generationChanged Then
            _renderedSnapshotKeys.Clear()
        End If
        If frameChanged Then
            _activeSnapshotKeys.Clear()
            _snapshotStack.Clear()
        End If

        _renderedSnapshotFrame = context.FrameGeneration
        _renderedSnapshotDeviceGeneration = context.DeviceGeneration
    End Sub

    Private Function RenderSnapshot(context As D3D_PaintContext,
                                    source As Control,
                                    snapshot As ID2D1Bitmap1,
                                    snapshotSize As System.Drawing.Size,
                                    excludedConsumer As Control) As Boolean
        If source Is Nothing OrElse snapshot Is Nothing Then Return False
        If _snapshotStack.Contains(source) Then Return False

        Dim depth = _snapshotStack.Count
        Dim renderContext = GetOrCreateOffscreenContext(context, depth)
        If renderContext Is Nothing Then Return False

        _snapshotStack.Add(source)
        Dim previousTarget As ID2D1Image = Nothing
        Dim drawing As Boolean = False

        Try
            previousTarget = renderContext.Target
            renderContext.Target = snapshot
            renderContext.Transform = Matrix3x2.Identity
            renderContext.AntialiasMode = AntialiasMode.PerPrimitive
            context.Compositor.TextRenderer.ConfigureDeviceContext(renderContext, context.TextQuality, targetHasAlpha:=True)
            renderContext.BeginDraw()
            drawing = True
            renderContext.Clear(New Color4(0, 0, 0, 0))

            Using snapshotContext As New D3D_PaintContext(
                context.Compositor,
                renderContext,
                Matrix3x2.Identity,
                New RectangleF(0, 0, snapshotSize.Width, snapshotSize.Height),
                V3_DpiContext.FromControl(source).Scale,
                context.TextQuality,
                targetHasAlpha:=True,
                context.FrameGeneration,
                context.DeviceGeneration,
                context.DirtyRegion)

                If Not context.Compositor.RenderBackgroundSourceSnapshot(snapshotContext, source, excludedConsumer) Then Return False
            End Using

            renderContext.EndDraw()
            drawing = False
            Return True
        Finally
            If drawing Then
                Try : renderContext.EndDraw() : Catch : End Try
            End If
            If renderContext IsNot Nothing Then
                Try : renderContext.Target = previousTarget : Catch : End Try
                Try : renderContext.Transform = Matrix3x2.Identity : Catch : End Try
            End If
            If previousTarget IsNot Nothing Then
                Try : previousTarget.Dispose() : Catch : End Try
            End If
            _snapshotStack.Remove(source)
        End Try
    End Function

    Private Shared Function ContainsGpuRenderableDescendant(control As Control) As Boolean
        If control Is Nothing OrElse control.IsDisposed Then Return False
        For Each child As Control In control.Controls
            If child Is Nothing OrElse child.IsDisposed Then Continue For
            If TypeOf child Is V3_IGpuRenderable Then Return True
            If ContainsGpuRenderableDescendant(child) Then Return True
        Next
        Return False
    End Function

    Private Function GetOrCreateOffscreenContext(context As D3D_PaintContext, depth As Integer) As ID2D1DeviceContext
        If context Is Nothing Then Return Nothing
        If _offscreenDeviceGeneration <> context.DeviceGeneration Then
            ReleaseOffscreenContexts()
            _offscreenDeviceGeneration = context.DeviceGeneration
        End If

        depth = Math.Max(0, depth)
        While _offscreenContexts.Count <= depth
            _offscreenContexts.Add(Nothing)
        End While

        If _offscreenContexts(depth) Is Nothing Then
            _offscreenContexts(depth) = _deviceManager.CreateDeviceContext()
        End If
        Return _offscreenContexts(depth)
    End Function

    Private Shared Function BuildSnapshotKey(context As D3D_PaintContext,
                                             source As Control,
                                             snapshotSize As System.Drawing.Size,
                                             excludedConsumer As Control) As String
        Return BuildSnapshotKeyPrefix(source, excludedConsumer) &
                  snapshotSize.Width.ToString(Globalization.CultureInfo.InvariantCulture) & "x" &
                  snapshotSize.Height.ToString(Globalization.CultureInfo.InvariantCulture) & ":" &
                  context.DeviceGeneration.ToString(Globalization.CultureInfo.InvariantCulture)
    End Function

    Private Shared Function BuildSnapshotKeyPrefix(source As Control) As String
        Return "background:" &
               Runtime.CompilerServices.RuntimeHelpers.GetHashCode(source).ToString(Globalization.CultureInfo.InvariantCulture) & ":"
    End Function

    Private Shared Function BuildSnapshotKeyPrefix(source As Control, excludedConsumer As Control) As String
        Dim prefix = BuildSnapshotKeyPrefix(source)
        If excludedConsumer Is Nothing Then Return prefix & "exclude:none:"
        Return prefix & "exclude:" &
               Runtime.CompilerServices.RuntimeHelpers.GetHashCode(excludedConsumer).ToString(Globalization.CultureInfo.InvariantCulture) & ":"
    End Function

    Private Sub ReleaseSnapshot(source As Control)
        If source Is Nothing Then Return

        Dim prefix = BuildSnapshotKeyPrefix(source)
        RemoveRenderedSnapshotKeys(prefix)
        If _snapshotStack.Contains(source) Then
            _pendingSnapshotReleases.Add(source)
            Return
        End If

        _textureCache.ReleaseByPrefix(prefix)
    End Sub

    Private Sub ReleaseSnapshot(source As Control, excludedConsumer As Control)
        If source Is Nothing Then Return

        Dim prefix = BuildSnapshotKeyPrefix(source, excludedConsumer)
        RemoveRenderedSnapshotKeys(prefix)
        If _snapshotStack.Contains(source) Then
            _pendingSnapshotReleases.Add(source)
            Return
        End If

        _textureCache.ReleaseByPrefix(prefix)
    End Sub

    Private Sub RemoveRenderedSnapshotKeys(prefix As String)
        If String.IsNullOrEmpty(prefix) Then Return

        Dim stale As New List(Of String)()
        For Each key In _renderedSnapshotKeys
            If key.StartsWith(prefix, StringComparison.Ordinal) Then stale.Add(key)
        Next
        For Each key In stale
            _renderedSnapshotKeys.Remove(key)
        Next
    End Sub

    Private Sub RemoveConsumerRelation(consumer As Control)
        If consumer Is Nothing Then Return

        Dim oldRelation As BackgroundRelation = Nothing
        If _relations.TryGetValue(consumer, oldRelation) Then
            _relations.Remove(consumer)
            If oldRelation IsNot Nothing AndAlso oldRelation.Source IsNot Nothing Then
                ReleaseSnapshot(oldRelation.Source, consumer)
                ReleaseSourceSubscription(oldRelation.Source)
            End If
        End If
        ClearConsumerTopology(consumer)
        DetachConsumerLifecycle(consumer)
    End Sub

    Private Function CollectConsumersForSource(source As Control) As List(Of Control)
        Dim result As New List(Of Control)()
        If source Is Nothing Then Return result

        Dim stale As New List(Of Control)()
        For Each kv In _relations
            Dim consumer = kv.Key
            Dim relation = kv.Value
            If consumer Is Nothing OrElse consumer.IsDisposed Then
                stale.Add(consumer)
                Continue For
            End If
            If relation Is Nothing OrElse relation.Source Is Nothing OrElse relation.Source.IsDisposed Then
                stale.Add(consumer)
                Continue For
            End If
            If relation.Source Is source Then result.Add(consumer)
        Next

        For Each consumer In stale
            RemoveConsumerRelation(consumer)
        Next

        Return result
    End Function

    Private Function CollectConsumerInvalidations(source As Control, dirtyRect As Rectangle) As List(Of ConsumerInvalidation)
        Dim result As New List(Of ConsumerInvalidation)()
        If source Is Nothing Then Return result

        Dim hasDirtyRect = dirtyRect.Width > 0 AndAlso dirtyRect.Height > 0
        Dim sourceDirty As RectangleF = If(hasDirtyRect,
                                           New RectangleF(dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height),
                                           RectangleF.Empty)
        Dim stale As New List(Of Control)()

        For Each kv In _relations.ToArray()
            Dim consumer = kv.Key
            Dim relation = kv.Value
            If consumer Is Nothing OrElse consumer.IsDisposed Then
                stale.Add(consumer)
                Continue For
            End If
            If relation Is Nothing OrElse relation.Source Is Nothing OrElse relation.Source.IsDisposed Then
                stale.Add(consumer)
                Continue For
            End If
            If relation.Source IsNot source Then Continue For
            If Not IsRenderableControl(consumer) Then
                stale.Add(consumer)
                Continue For
            End If

            Dim consumerDirty As Rectangle
            If Not hasDirtyRect OrElse relation.SourceRect.Width <= 0 OrElse relation.SourceRect.Height <= 0 OrElse
               relation.DestinationRect.Width <= 0 OrElse relation.DestinationRect.Height <= 0 Then
                consumerDirty = ToEnclosingRectangle(relation.DestinationRect)
                If consumerDirty.Width <= 0 OrElse consumerDirty.Height <= 0 Then
                    consumerDirty = New Rectangle(Point.Empty, consumer.Size)
                End If
            Else
                Dim sourceIntersection = RectangleF.Intersect(relation.SourceRect, sourceDirty)
                If sourceIntersection.Width <= 0 OrElse sourceIntersection.Height <= 0 Then Continue For

                Dim destination As New RectangleF(
                    relation.DestinationRect.X + sourceIntersection.X - relation.SourceRect.X,
                    relation.DestinationRect.Y + sourceIntersection.Y - relation.SourceRect.Y,
                    sourceIntersection.Width,
                    sourceIntersection.Height)
                consumerDirty = ToEnclosingRectangle(destination)
            End If

            If consumerDirty.Width > 0 AndAlso consumerDirty.Height > 0 Then
                result.Add(New ConsumerInvalidation(consumer, consumerDirty))
            End If
        Next

        For Each consumer In stale
            RemoveConsumerRelation(consumer)
        Next

        Return result
    End Function

    Private Shared Function ResolveConsumerDirtyFromSourceDirty(relation As BackgroundRelation,
                                                               sourceDirtyRect As Rectangle,
                                                               consumer As Control) As Rectangle
        If relation Is Nothing Then Return Rectangle.Empty

        Dim hasDirtyRect = sourceDirtyRect.Width > 0 AndAlso sourceDirtyRect.Height > 0
        If Not hasDirtyRect OrElse relation.SourceRect.Width <= 0 OrElse relation.SourceRect.Height <= 0 OrElse
           relation.DestinationRect.Width <= 0 OrElse relation.DestinationRect.Height <= 0 Then
            Dim consumerDirty = ToEnclosingRectangle(relation.DestinationRect)
            If consumerDirty.Width <= 0 OrElse consumerDirty.Height <= 0 Then
                If consumer Is Nothing OrElse consumer.IsDisposed Then Return Rectangle.Empty
                consumerDirty = New Rectangle(Point.Empty, consumer.Size)
            End If
            Return consumerDirty
        End If

        Dim sourceDirty As New RectangleF(sourceDirtyRect.X, sourceDirtyRect.Y, sourceDirtyRect.Width, sourceDirtyRect.Height)
        Dim sourceIntersection = RectangleF.Intersect(relation.SourceRect, sourceDirty)
        If sourceIntersection.Width <= 0 OrElse sourceIntersection.Height <= 0 Then Return Rectangle.Empty

        Dim destination As New RectangleF(
            relation.DestinationRect.X + sourceIntersection.X - relation.SourceRect.X,
            relation.DestinationRect.Y + sourceIntersection.Y - relation.SourceRect.Y,
            sourceIntersection.Width,
            sourceIntersection.Height)
        Return ToEnclosingRectangle(destination)
    End Function

    Private Shared Function IsDescendantOrSelf(control As Control, ancestor As Control) As Boolean
        If control Is Nothing OrElse ancestor Is Nothing Then Return False
        If control.IsDisposed OrElse ancestor.IsDisposed Then Return False

        Dim current As Control = control
        While current IsNot Nothing
            If current Is ancestor Then Return True
            current = current.Parent
        End While

        Return False
    End Function

    Private Shared Function ToEnclosingRectangle(rect As RectangleF) As Rectangle
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return Rectangle.Empty

        Dim left = CInt(Math.Floor(rect.Left))
        Dim top = CInt(Math.Floor(rect.Top))
        Dim right = CInt(Math.Ceiling(rect.Right))
        Dim bottom = CInt(Math.Ceiling(rect.Bottom))
        Return Rectangle.FromLTRB(left, top, right, bottom)
    End Function

    Private Shared Function MapRectangleBetweenControls(fromControl As Control, toControl As Control, rect As Rectangle) As Rectangle
        If fromControl Is Nothing OrElse toControl Is Nothing Then Return Rectangle.Empty
        If fromControl.IsDisposed OrElse toControl.IsDisposed Then Return Rectangle.Empty
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return Rectangle.Empty

        Try
            Dim screenTopLeft = fromControl.PointToScreen(rect.Location)
            Dim screenBottomRight = fromControl.PointToScreen(New Point(rect.Right, rect.Bottom))
            Dim targetTopLeft = toControl.PointToClient(screenTopLeft)
            Dim targetBottomRight = toControl.PointToClient(screenBottomRight)
            Return Rectangle.FromLTRB(targetTopLeft.X, targetTopLeft.Y, targetBottomRight.X, targetBottomRight.Y)
        Catch
        End Try

        Dim fallbackTopLeft As Point = Point.Empty
        Dim fallbackBottomRight As Point = Point.Empty
        If TryMapPointBetweenControls(fromControl, toControl, rect.Location, fallbackTopLeft) AndAlso
           TryMapPointBetweenControls(fromControl, toControl, New Point(rect.Right, rect.Bottom), fallbackBottomRight) Then
            Return Rectangle.FromLTRB(fallbackTopLeft.X, fallbackTopLeft.Y, fallbackBottomRight.X, fallbackBottomRight.Y)
        End If

        Return Rectangle.Empty
    End Function

    Private Shared Function TryMapPointBetweenControls(fromControl As Control,
                                                       toControl As Control,
                                                       point As Point,
                                                       ByRef mappedPoint As Point) As Boolean
        mappedPoint = Point.Empty
        If fromControl Is Nothing OrElse toControl Is Nothing Then Return False
        If fromControl.IsDisposed OrElse toControl.IsDisposed Then Return False

        Try
            mappedPoint = toControl.PointToClient(fromControl.PointToScreen(point))
            Return True
        Catch
        End Try

        Dim common = FindCommonAncestor(fromControl, toControl)
        If common Is Nothing Then Return False

        Dim fromLocation As Point = Point.Empty
        Dim toLocation As Point = Point.Empty
        If Not TryGetControlLocationInAncestor(fromControl, common, fromLocation) Then Return False
        If Not TryGetControlLocationInAncestor(toControl, common, toLocation) Then Return False

        mappedPoint = New Point(fromLocation.X + point.X - toLocation.X,
                                fromLocation.Y + point.Y - toLocation.Y)
        Return True
    End Function

    Private Shared Function FindCommonAncestor(first As Control, second As Control) As Control
        If first Is Nothing OrElse second Is Nothing Then Return Nothing

        Dim ancestors As New HashSet(Of Control)()
        Dim current As Control = first
        While current IsNot Nothing
            If current.IsDisposed Then Return Nothing
            ancestors.Add(current)
            current = current.Parent
        End While

        current = second
        While current IsNot Nothing
            If current.IsDisposed Then Return Nothing
            If ancestors.Contains(current) Then Return current
            current = current.Parent
        End While

        Return Nothing
    End Function

    Private Shared Function TryGetControlLocationInAncestor(control As Control,
                                                           ancestor As Control,
                                                           ByRef location As Point) As Boolean
        location = Point.Empty
        If control Is Nothing OrElse ancestor Is Nothing Then Return False
        If control.IsDisposed OrElse ancestor.IsDisposed Then Return False

        Dim x As Integer = 0
        Dim y As Integer = 0
        Dim current As Control = control
        While current IsNot Nothing AndAlso current IsNot ancestor
            If current.IsDisposed Then Return False
            x += current.Left
            y += current.Top
            current = current.Parent
        End While

        If current IsNot ancestor Then Return False
        location = New Point(x, y)
        Return True
    End Function

    Private Shared Function IsRenderableControl(control As Control, Optional expectedForm As Form = Nothing) As Boolean
        If control Is Nothing OrElse control.IsDisposed Then Return False
        If control.Width <= 0 OrElse control.Height <= 0 Then Return False

        Dim current As Control = control
        While current IsNot Nothing
            If current.IsDisposed OrElse Not current.Visible Then Return False
            current = current.Parent
        End While

        Dim form = D3D_RenderCore.ResolveCompositorForm(control)
        If form Is Nothing Then Return expectedForm Is Nothing
        If expectedForm IsNot Nothing AndAlso form IsNot expectedForm Then Return False
        If form.IsDisposed OrElse Not form.Visible OrElse Not form.IsHandleCreated Then Return False
        If form.WindowState = FormWindowState.Minimized Then Return False
        Return True
    End Function

    Private Shared Sub RequestConsumerRender(consumer As Control)
        RequestConsumerRender(consumer, Rectangle.Empty)
    End Sub

    Private Shared Sub RequestConsumerRender(consumer As Control, dirtyRect As Rectangle)
        If consumer Is Nothing OrElse consumer.IsDisposed Then Return
        If consumer.Width <= 0 OrElse consumer.Height <= 0 Then Return
        If Not consumer.IsHandleCreated Then Return
        Dim bounds = If(dirtyRect.Width > 0 AndAlso dirtyRect.Height > 0,
                        Rectangle.Intersect(New Rectangle(Point.Empty, consumer.Size), dirtyRect),
                        New Rectangle(Point.Empty, consumer.Size))
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        D3D_RenderCore.RequestRender(consumer, bounds)
    End Sub

    Private Sub RequestSourceRender(source As Control, dirtyRect As Rectangle)
        If _compositor Is Nothing OrElse _compositor.IsDisposed Then Return
        If source Is Nothing OrElse source.IsDisposed Then Return
        If source.Width <= 0 OrElse source.Height <= 0 Then Return
        If Not source.IsHandleCreated Then Return
        If Not IsRenderableControl(source, _compositor.Form) Then Return

        Dim sourceSize = GetSourceControlSize(source)
        If sourceSize.Width <= 0 OrElse sourceSize.Height <= 0 Then Return

        Dim sourceBounds As New Rectangle(Point.Empty, sourceSize)
        Dim sourceDirty = If(dirtyRect.Width > 0 AndAlso dirtyRect.Height > 0,
                             Rectangle.Intersect(sourceBounds, dirtyRect),
                             sourceBounds)
        If sourceDirty.Width <= 0 OrElse sourceDirty.Height <= 0 Then Return

        Dim form = _compositor.Form
        If form Is Nothing OrElse form.IsDisposed OrElse Not form.IsHandleCreated Then Return

        Dim windowDirty As Rectangle
        If TypeOf source Is Form AndAlso source Is form Then
            windowDirty = sourceDirty
        Else
            Try
                If D3D_RenderCore.ResolveCompositorForm(source) IsNot form Then Return
                Dim screenPoint = source.PointToScreen(sourceDirty.Location)
                Dim formPoint = form.PointToClient(screenPoint)
                windowDirty = New Rectangle(formPoint, sourceDirty.Size)
            Catch
                Return
            End Try
        End If

        _compositor.RequestRender(windowDirty)
    End Sub

    Private Function GetSourceControlSize(source As Control) As System.Drawing.Size
        If source Is Nothing OrElse source.IsDisposed Then Return System.Drawing.Size.Empty
        If TypeOf source Is Form AndAlso source Is _compositor.Form Then Return _compositor.RenderTargetSize
        Dim form = TryCast(source, Form)
        If form IsNot Nothing Then Return form.ClientSize
        Return source.Size
    End Function

    Private Shared Function IsTransientHandleDestroy(control As Control) As Boolean
        Return control IsNot Nothing AndAlso Not control.IsDisposed AndAlso control.RecreatingHandle
    End Function

    Private Sub AttachConsumerLifecycle(consumer As Control)
        If consumer Is Nothing OrElse _consumerSubscriptions.Contains(consumer) Then Return

        _consumerSubscriptions.Add(consumer)
        Try : AddHandler consumer.Disposed, AddressOf OnConsumerDisposed : Catch : End Try
        Try : AddHandler consumer.HandleCreated, AddressOf OnConsumerHandleCreated : Catch : End Try
        Try : AddHandler consumer.HandleDestroyed, AddressOf OnConsumerHandleDestroyed : Catch : End Try
        Try : AddHandler consumer.VisibleChanged, AddressOf OnConsumerVisibleChanged : Catch : End Try
        Try : AddHandler consumer.ParentChanged, AddressOf OnConsumerParentChanged : Catch : End Try
        Try : AddHandler consumer.LocationChanged, AddressOf OnConsumerLayoutChanged : Catch : End Try
        Try : AddHandler consumer.Resize, AddressOf OnConsumerLayoutChanged : Catch : End Try
    End Sub

    Private Sub DetachConsumerLifecycle(consumer As Control)
        If consumer Is Nothing OrElse Not _consumerSubscriptions.Remove(consumer) Then Return

        Try : RemoveHandler consumer.Disposed, AddressOf OnConsumerDisposed : Catch : End Try
        Try : RemoveHandler consumer.HandleCreated, AddressOf OnConsumerHandleCreated : Catch : End Try
        Try : RemoveHandler consumer.HandleDestroyed, AddressOf OnConsumerHandleDestroyed : Catch : End Try
        Try : RemoveHandler consumer.VisibleChanged, AddressOf OnConsumerVisibleChanged : Catch : End Try
        Try : RemoveHandler consumer.ParentChanged, AddressOf OnConsumerParentChanged : Catch : End Try
        Try : RemoveHandler consumer.LocationChanged, AddressOf OnConsumerLayoutChanged : Catch : End Try
        Try : RemoveHandler consumer.Resize, AddressOf OnConsumerLayoutChanged : Catch : End Try
    End Sub

    Private Sub OnConsumerDisposed(sender As Object, e As EventArgs)
        UnregisterConsumer(TryCast(sender, Control), recursive:=True)
    End Sub

    Private Sub OnConsumerHandleCreated(sender As Object, e As EventArgs)
        RequestConsumerRender(TryCast(sender, Control))
    End Sub

    Private Sub OnConsumerHandleDestroyed(sender As Object, e As EventArgs)
        Dim consumer = TryCast(sender, Control)
        If IsTransientHandleDestroy(consumer) Then Return
        UnregisterConsumer(consumer, recursive:=True)
    End Sub

    Private Sub OnConsumerVisibleChanged(sender As Object, e As EventArgs)
        Dim consumer = TryCast(sender, Control)
        If consumer Is Nothing Then Return
        If consumer.Visible Then
            RequestConsumerRender(consumer)
        Else
            UnregisterConsumer(consumer, recursive:=True)
        End If
    End Sub

    Private Sub OnConsumerParentChanged(sender As Object, e As EventArgs)
        Dim consumer = TryCast(sender, Control)
        If consumer Is Nothing Then Return
        UnregisterConsumer(consumer, recursive:=True)
        If consumer.Parent IsNot Nothing Then RequestConsumerRender(consumer)
    End Sub

    Private Sub OnConsumerLayoutChanged(sender As Object, e As EventArgs)
        RequestConsumerRender(TryCast(sender, Control))
    End Sub

    Private Sub AddSourceSubscription(source As Control)
        If source Is Nothing Then Return

        Dim refCount As Integer = 0
        If _sourceSubscriptionRefs.TryGetValue(source, refCount) Then
            _sourceSubscriptionRefs(source) = refCount + 1
            Return
        End If

        _sourceSubscriptionRefs(source) = 1
        Try : AddHandler source.Invalidated, AddressOf OnSourceInvalidated : Catch : End Try
        Try : AddHandler source.Resize, AddressOf OnSourceChanged : Catch : End Try
        Try : AddHandler source.LocationChanged, AddressOf OnSourceChanged : Catch : End Try
        Try : AddHandler source.Disposed, AddressOf OnSourceDisposed : Catch : End Try
        Try : AddHandler source.HandleCreated, AddressOf OnSourceHandleCreated : Catch : End Try
        Try : AddHandler source.HandleDestroyed, AddressOf OnSourceHandleDestroyed : Catch : End Try
        Try : AddHandler source.ParentChanged, AddressOf OnSourceChanged : Catch : End Try
        Try : AddHandler source.VisibleChanged, AddressOf OnSourceChanged : Catch : End Try
        RefreshSourceAncestorSubscriptions(source)
    End Sub

    Private Sub ReleaseSourceSubscription(source As Control)
        If source Is Nothing Then Return

        Dim refCount As Integer = 0
        If Not _sourceSubscriptionRefs.TryGetValue(source, refCount) Then Return
        If refCount > 1 Then
            _sourceSubscriptionRefs(source) = refCount - 1
            Return
        End If

        _sourceSubscriptionRefs.Remove(source)
        Try : RemoveHandler source.Invalidated, AddressOf OnSourceInvalidated : Catch : End Try
        Try : RemoveHandler source.Resize, AddressOf OnSourceChanged : Catch : End Try
        Try : RemoveHandler source.LocationChanged, AddressOf OnSourceChanged : Catch : End Try
        Try : RemoveHandler source.Disposed, AddressOf OnSourceDisposed : Catch : End Try
        Try : RemoveHandler source.HandleCreated, AddressOf OnSourceHandleCreated : Catch : End Try
        Try : RemoveHandler source.HandleDestroyed, AddressOf OnSourceHandleDestroyed : Catch : End Try
        Try : RemoveHandler source.ParentChanged, AddressOf OnSourceChanged : Catch : End Try
        Try : RemoveHandler source.VisibleChanged, AddressOf OnSourceChanged : Catch : End Try
        ClearSourceAncestorSubscriptions(source)
    End Sub

    Private Sub OnSourceInvalidated(sender As Object, e As InvalidateEventArgs)
        Dim source = TryCast(sender, Control)
        Dim dirtyRect As Rectangle = If(e IsNot Nothing, e.InvalidRect, Rectangle.Empty)
        InvalidateSource(source, dirtyRect)
    End Sub

    Private Sub OnSourceChanged(sender As Object, e As EventArgs)
        Dim source = TryCast(sender, Control)
        RefreshSourceAncestorSubscriptions(source)
        InvalidateSource(source)
    End Sub

    Private Sub OnSourceDisposed(sender As Object, e As EventArgs)
        Dim source = TryCast(sender, Control)
        ClearSourceAncestorSubscriptions(source)
        RemoveSourceRelations(source, requestConsumers:=True)
    End Sub

    Private Sub OnSourceHandleCreated(sender As Object, e As EventArgs)
        Dim source = TryCast(sender, Control)
        If source Is Nothing OrElse source.IsDisposed Then Return
        RefreshSourceAncestorSubscriptions(source)
        InvalidateSource(source)
    End Sub

    Private Sub OnSourceHandleDestroyed(sender As Object, e As EventArgs)
        Dim source = TryCast(sender, Control)
        If IsTransientHandleDestroy(source) Then
            ReleaseSnapshot(source)
            Return
        End If
        ClearSourceAncestorSubscriptions(source)
        RemoveSourceRelations(source, requestConsumers:=True)
    End Sub

    Private Sub OnSourceAncestorInvalidated(sender As Object, e As InvalidateEventArgs)
        Dim dirtyRect As Rectangle = If(e IsNot Nothing, e.InvalidRect, Rectangle.Empty)
        InvalidateSourcesForAncestorChange(TryCast(sender, Control), dirtyRect)
    End Sub

    Private Sub OnSourceAncestorChanged(sender As Object, e As EventArgs)
        InvalidateSourcesForAncestorChange(TryCast(sender, Control), Rectangle.Empty)
    End Sub

    Private Sub OnForwarderTopologyChanged(sender As Object, e As EventArgs)
        Dim node = TryCast(sender, Control)
        If node Is Nothing Then Return

        Dim affected As New List(Of Control)()
        For Each kv In _consumerTopologies.ToArray()
            Dim consumer = kv.Key
            Dim topology = kv.Value
            If topology Is Nothing OrElse Not topology.Contains(node) Then Continue For

            If consumer Is Nothing OrElse consumer.IsDisposed OrElse node.IsDisposed Then
                RemoveConsumerRelation(consumer)
            Else
                affected.Add(consumer)
            End If
        Next

        For Each consumer In affected
            RequestConsumerRender(consumer)
        Next
    End Sub

    Private Sub InvalidateSourcesForAncestorChange(ancestor As Control, ancestorDirtyRect As Rectangle)
        If ancestor Is Nothing Then Return

        Dim affected As New List(Of KeyValuePair(Of Control, Rectangle))()
        For Each kv In _sourceAncestorNodes.ToArray()
            Dim source = kv.Key
            Dim ancestors = kv.Value
            If source Is Nothing OrElse source.IsDisposed Then
                RemoveSourceRelations(source, requestConsumers:=True)
                Continue For
            End If
            If ancestors Is Nothing OrElse Not ancestors.Contains(ancestor) Then Continue For

            Dim dirtyRect As Rectangle = Rectangle.Empty
            If ancestorDirtyRect.Width > 0 AndAlso ancestorDirtyRect.Height > 0 Then
                dirtyRect = MapRectangleBetweenControls(ancestor, source, ancestorDirtyRect)
                dirtyRect = Rectangle.Intersect(New Rectangle(0, 0, source.Width, source.Height), dirtyRect)
                If dirtyRect.Width <= 0 OrElse dirtyRect.Height <= 0 Then Continue For
            End If

            affected.Add(New KeyValuePair(Of Control, Rectangle)(source, dirtyRect))
        Next

        For Each item In affected
            InvalidateSource(item.Key, item.Value)
        Next
    End Sub

    Private Sub RemoveSourceRelations(source As Control, requestConsumers As Boolean)
        If source Is Nothing Then Return

        ReleaseSnapshot(source)

        Dim affected = CollectConsumersForSource(source)
        For Each consumer In affected
            RemoveConsumerRelation(consumer)
            If requestConsumers Then RequestConsumerRender(consumer)
        Next
    End Sub

    Private Sub ReleaseOffscreenContexts()
        For Each context In _offscreenContexts
            If context Is Nothing Then Continue For
            Try : context.Target = Nothing : Catch : End Try
            Try : context.Dispose() : Catch : End Try
        Next
        _offscreenContexts.Clear()
        _offscreenDeviceGeneration = -1
    End Sub

    ''' <summary>
    ''' 释放 background snapshot GPU 资源但保留 source/consumer 关系。
    ''' </summary>
    Public Sub Invalidate()
        _textureCache.ReleaseByPrefix("background:")
        _renderedSnapshotKeys.Clear()
        _activeSnapshotKeys.Clear()
        _snapshotStack.Clear()
        _pendingSnapshotReleases.Clear()
        ReleaseOffscreenContexts()
    End Sub

    Public Sub ClearRelations()
        For Each consumer In _consumerSubscriptions.ToArray()
            DetachConsumerLifecycle(consumer)
        Next
        For Each consumer In _consumerTopologies.Keys.ToArray()
            ClearConsumerTopology(consumer)
        Next
        For Each source In _sourceSubscriptionRefs.Keys.ToArray()
            ReleaseSourceSubscription(source)
        Next
        For Each source In _sourceAncestorNodes.Keys.ToArray()
            ClearSourceAncestorSubscriptions(source)
        Next

        _relations.Clear()
        _consumerTopologies.Clear()
        _sourceSubscriptionRefs.Clear()
        _sourceAncestorNodes.Clear()
        _ancestorSubscriptionRefs.Clear()
        _topologySubscriptionRefs.Clear()
        _consumerSubscriptions.Clear()
        _invalidatingSources.Clear()
        _pendingSnapshotReleases.Clear()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        Invalidate()
        ClearRelations()
        GC.SuppressFinalize(Me)
    End Sub

    Private Structure BackgroundMapping
        Public Source As Control
        Public SourceSize As System.Drawing.Size
        Public SourceRect As RectangleF
        Public DestinationRect As RectangleF
        Public TopologyNodes As List(Of Control)
    End Structure

    Private NotInheritable Class BackgroundRelation
        Public Source As Control
        Public SourceRect As RectangleF
        Public DestinationRect As RectangleF
    End Class

    Private Structure ConsumerInvalidation
        Public ReadOnly Consumer As Control
        Public ReadOnly DirtyRect As Rectangle

        Public Sub New(consumer As Control, dirtyRect As Rectangle)
            Me.Consumer = consumer
            Me.DirtyRect = dirtyRect
        End Sub
    End Structure

    Private Sub RefreshSourceAncestorSubscriptions(source As Control)
        If source Is Nothing Then Return

        ClearSourceAncestorSubscriptions(source)
        If source.IsDisposed Then Return

        Dim ancestors As New List(Of Control)()
        Dim current = source.Parent
        While current IsNot Nothing
            If current.IsDisposed Then Exit While
            If Not ancestors.Contains(current) Then
                ancestors.Add(current)
                AddAncestorSubscription(current)
            End If
            current = current.Parent
        End While

        If ancestors.Count > 0 Then _sourceAncestorNodes(source) = ancestors
    End Sub

    Private Sub ClearSourceAncestorSubscriptions(source As Control)
        If source Is Nothing Then Return

        Dim ancestors As List(Of Control) = Nothing
        If Not _sourceAncestorNodes.TryGetValue(source, ancestors) Then Return
        _sourceAncestorNodes.Remove(source)

        If ancestors Is Nothing Then Return
        For Each ancestor In ancestors
            ReleaseAncestorSubscription(ancestor)
        Next
    End Sub

    Private Sub AddAncestorSubscription(ancestor As Control)
        If ancestor Is Nothing Then Return

        Dim refCount As Integer = 0
        If _ancestorSubscriptionRefs.TryGetValue(ancestor, refCount) Then
            _ancestorSubscriptionRefs(ancestor) = refCount + 1
            Return
        End If

        _ancestorSubscriptionRefs(ancestor) = 1
        Try : AddHandler ancestor.Disposed, AddressOf OnSourceAncestorChanged : Catch : End Try
        Try : AddHandler ancestor.HandleCreated, AddressOf OnSourceAncestorChanged : Catch : End Try
        Try : AddHandler ancestor.HandleDestroyed, AddressOf OnSourceAncestorChanged : Catch : End Try
        Try : AddHandler ancestor.Invalidated, AddressOf OnSourceAncestorInvalidated : Catch : End Try
        Try : AddHandler ancestor.LocationChanged, AddressOf OnSourceAncestorChanged : Catch : End Try
        Try : AddHandler ancestor.ParentChanged, AddressOf OnSourceAncestorChanged : Catch : End Try
        Try : AddHandler ancestor.Resize, AddressOf OnSourceAncestorChanged : Catch : End Try
        Try : AddHandler ancestor.VisibleChanged, AddressOf OnSourceAncestorChanged : Catch : End Try
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
        Try : RemoveHandler ancestor.Disposed, AddressOf OnSourceAncestorChanged : Catch : End Try
        Try : RemoveHandler ancestor.HandleCreated, AddressOf OnSourceAncestorChanged : Catch : End Try
        Try : RemoveHandler ancestor.HandleDestroyed, AddressOf OnSourceAncestorChanged : Catch : End Try
        Try : RemoveHandler ancestor.Invalidated, AddressOf OnSourceAncestorInvalidated : Catch : End Try
        Try : RemoveHandler ancestor.LocationChanged, AddressOf OnSourceAncestorChanged : Catch : End Try
        Try : RemoveHandler ancestor.ParentChanged, AddressOf OnSourceAncestorChanged : Catch : End Try
        Try : RemoveHandler ancestor.Resize, AddressOf OnSourceAncestorChanged : Catch : End Try
        Try : RemoveHandler ancestor.VisibleChanged, AddressOf OnSourceAncestorChanged : Catch : End Try
    End Sub

    Private Sub SetConsumerTopology(consumer As Control, topologyNodes As IEnumerable(Of Control))
        If consumer Is Nothing Then Return

        ClearConsumerTopology(consumer)
        Dim normalized = NormalizeTopologyNodes(topologyNodes)
        If normalized Is Nothing OrElse normalized.Count = 0 Then Return

        _consumerTopologies(consumer) = normalized
        For Each node In normalized
            AddTopologySubscription(node)
        Next
    End Sub

    Private Sub ClearConsumerTopology(consumer As Control)
        If consumer Is Nothing Then Return

        Dim topology As List(Of Control) = Nothing
        If Not _consumerTopologies.TryGetValue(consumer, topology) Then Return
        _consumerTopologies.Remove(consumer)

        If topology Is Nothing Then Return
        For Each node In topology
            ReleaseTopologySubscription(node)
        Next
    End Sub

    Private Shared Function NormalizeTopologyNodes(nodes As IEnumerable(Of Control)) As List(Of Control)
        If nodes Is Nothing Then Return Nothing

        Dim result As New List(Of Control)()
        For Each node In nodes
            If node Is Nothing OrElse node.IsDisposed Then Continue For
            If result.Contains(node) Then Continue For
            result.Add(node)
        Next
        If result.Count = 0 Then Return Nothing
        Return result
    End Function

    Private Sub AddTopologySubscription(node As Control)
        If node Is Nothing Then Return

        Dim refCount As Integer = 0
        If _topologySubscriptionRefs.TryGetValue(node, refCount) Then
            _topologySubscriptionRefs(node) = refCount + 1
            Return
        End If

        _topologySubscriptionRefs(node) = 1
        Try : AddHandler node.Disposed, AddressOf OnForwarderTopologyChanged : Catch : End Try
        Try : AddHandler node.HandleCreated, AddressOf OnForwarderTopologyChanged : Catch : End Try
        Try : AddHandler node.HandleDestroyed, AddressOf OnForwarderTopologyChanged : Catch : End Try
        Try : AddHandler node.LocationChanged, AddressOf OnForwarderTopologyChanged : Catch : End Try
        Try : AddHandler node.ParentChanged, AddressOf OnForwarderTopologyChanged : Catch : End Try
        Try : AddHandler node.Resize, AddressOf OnForwarderTopologyChanged : Catch : End Try
        Try : AddHandler node.VisibleChanged, AddressOf OnForwarderTopologyChanged : Catch : End Try
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
        Try : RemoveHandler node.HandleCreated, AddressOf OnForwarderTopologyChanged : Catch : End Try
        Try : RemoveHandler node.HandleDestroyed, AddressOf OnForwarderTopologyChanged : Catch : End Try
        Try : RemoveHandler node.LocationChanged, AddressOf OnForwarderTopologyChanged : Catch : End Try
        Try : RemoveHandler node.ParentChanged, AddressOf OnForwarderTopologyChanged : Catch : End Try
        Try : RemoveHandler node.Resize, AddressOf OnForwarderTopologyChanged : Catch : End Try
        Try : RemoveHandler node.VisibleChanged, AddressOf OnForwarderTopologyChanged : Catch : End Try
    End Sub
End Class
