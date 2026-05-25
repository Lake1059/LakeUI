Imports Vortice.Direct2D1

''' <summary>
''' 可复用的自定义滚动条渲染器，统一管理布局计算、D2D 绘制以及鼠标交互状态。
''' 一个实例同时支持竖向或横向使用（取决于调用的 ComputeLayout / Draw 方法对）。
''' </summary>
''' <remarks>
''' 渲染路径已全面切换到 Direct2D；不再保留 GDI+ 版本。
''' D2D 画刷会按颜色 + RenderTarget 缓存复用，避免每帧重建。
''' </remarks>
Public Class ScrollBarRenderer
    ''' <summary>当前帧滑块（thumb）在容器坐标系下的矩形。</summary>
    Public ThumbRect As Rectangle = Rectangle.Empty
    ''' <summary>当前帧轨道（track）在容器坐标系下的矩形，覆盖了 Margin 命中区。</summary>
    Public TrackRect As Rectangle = Rectangle.Empty
    ''' <summary>竖向滚动条的可视左侧 X 坐标（不含 Margin 命中区）。</summary>
    Public VisualLeft As Integer = 0
    ''' <summary>横向滚动条的可视顶部 Y 坐标（不含 Margin 命中区）。</summary>
    Public VisualTop As Integer = 0

    ''' <summary>鼠标当前是否悬停在滑块上。</summary>
    Public IsHover As Boolean = False
    ''' <summary>用户是否正在拖动滑块。</summary>
    Public IsDragging As Boolean = False

    ' 拖拽时记录的起点鼠标坐标和起始滚动偏移，用于计算相对位移
    Private _dragStartY As Integer = 0
    Private _dragStartX As Integer = 0
    Private _dragStartOffset As Integer = 0

    ''' <summary>轨道两侧/上下额外的命中区像素数（同时也是滑块外扩的命中区）。</summary>
    Public Const Margin As Integer = 2

    ' --- D2D 画刷缓存 ---
    ' 画刷与 RenderTarget 生命周期绑定，因此缓存键包含 RT 引用。
    ' 一旦 RT 变化或颜色变化，旧画刷会被释放并按新参数重建。
    Private _cachedRT As ID2D1RenderTarget = Nothing
    Private _trackBrush As ID2D1SolidColorBrush = Nothing
    Private _thumbBrush As ID2D1SolidColorBrush = Nothing
    Private _thumbHoverBrush As ID2D1SolidColorBrush = Nothing
    Private _trackBrushColor As Color = Color.Empty
    Private _thumbBrushColor As Color = Color.Empty
    Private _thumbHoverBrushColor As Color = Color.Empty

    ''' <summary>
    ''' 计算竖向滚动条的轨道与滑块矩形。
    ''' 调用方在每次重绘前调用，无需在尺寸不变时缓存结果。
    ''' </summary>
    ''' <param name="containerW">容器宽度。</param>
    ''' <param name="containerH">容器高度。</param>
    ''' <param name="borderWidth">外边框宽度。</param>
    ''' <param name="borderRadius">外边框圆角半径。</param>
    ''' <param name="paddingTop">顶部留白。</param>
    ''' <param name="paddingBottom">底部留白。</param>
    ''' <param name="scrollBarWidth">滚动条可视宽度（不含 Margin 命中区）。</param>
    ''' <param name="totalCount">总项数（或总像素，由调用方语义决定）。</param>
    ''' <param name="visibleCount">可视项数（或可视像素）。</param>
    ''' <param name="scrollOffset">当前滚动偏移。</param>
    Public Sub ComputeLayout(containerW As Integer, containerH As Integer,
                             borderWidth As Integer, borderRadius As Integer,
                             paddingTop As Integer, paddingBottom As Integer,
                             scrollBarWidth As Integer,
                             totalCount As Integer, visibleCount As Integer, scrollOffset As Integer)
        Dim inset As Integer = Math.Max(borderWidth, If(borderRadius > 0, borderRadius \ 2, 0))
        Dim sbX As Integer = containerW - scrollBarWidth - inset - Margin
        Dim sbY As Integer = inset + Margin + paddingTop
        Dim sbH As Integer = containerH - (inset + Margin) * 2 - paddingTop - paddingBottom
        If sbH <= 0 OrElse scrollBarWidth <= 0 Then
            ThumbRect = Rectangle.Empty
            TrackRect = Rectangle.Empty
            VisualLeft = containerW
            Return
        End If

        VisualLeft = sbX
        TrackRect = New Rectangle(sbX - Margin, sbY, scrollBarWidth + Margin * 2, sbH)

        Dim maxOff As Integer = Math.Max(0, totalCount - visibleCount)
        Dim thumbH As Integer = Math.Max(20, CInt(sbH * visibleCount / Math.Max(1, totalCount)))
        Dim thumbY As Integer = sbY
        If maxOff > 0 Then
            thumbY = sbY + CInt((sbH - thumbH) * scrollOffset / maxOff)
        End If
        ThumbRect = New Rectangle(sbX - Margin, thumbY, scrollBarWidth + Margin * 2, thumbH)
    End Sub

    ''' <summary>更新滑块悬停状态。返回 True 表示状态发生变化，调用方应触发重绘。</summary>
    Public Function UpdateHover(mouseLocation As Point) As Boolean
        Dim wasHover As Boolean = IsHover
        IsHover = ThumbRect.Contains(mouseLocation)
        Return IsHover <> wasHover
    End Function

    ''' <summary>在滑块上按下鼠标时开始拖拽；返回 True 表示已开始拖拽。</summary>
    Public Function BeginDrag(mouseLocation As Point, scrollOffset As Integer) As Boolean
        If ThumbRect.Contains(mouseLocation) Then
            IsDragging = True
            _dragStartY = mouseLocation.Y
            _dragStartOffset = scrollOffset
            Return True
        End If
        Return False
    End Function

    ''' <summary>拖拽过程中根据鼠标 Y 增量换算出新的滚动偏移。未拖拽时返回起始偏移。</summary>
    Public Function DragMove(mouseY As Integer, totalCount As Integer, visibleCount As Integer) As Integer
        If Not IsDragging Then Return _dragStartOffset
        Dim trackH As Integer = TrackRect.Height
        Dim maxOff As Integer = Math.Max(0, totalCount - visibleCount)
        Dim thumbH As Integer = Math.Max(20, CInt(trackH * visibleCount / Math.Max(1, totalCount)))
        Dim usableH As Integer = trackH - thumbH
        If usableH <= 0 Then Return _dragStartOffset
        Dim dy As Integer = mouseY - _dragStartY
        Dim newOff As Integer = _dragStartOffset + CInt(dy * maxOff / usableH)
        Return Math.Max(0, Math.Min(maxOff, newOff))
    End Function

    ''' <summary>点击轨道空白处（非滑块）时翻一页，返回新的滚动偏移。</summary>
    Public Function TrackClick(mouseLocation As Point, scrollOffset As Integer,
                               totalCount As Integer, visibleCount As Integer) As Integer
        If Not TrackRect.Contains(mouseLocation) Then Return scrollOffset
        Dim maxOff As Integer = Math.Max(0, totalCount - visibleCount)
        If mouseLocation.Y < ThumbRect.Y Then
            Return Math.Max(0, scrollOffset - visibleCount)
        Else
            Return Math.Min(maxOff, scrollOffset + visibleCount)
        End If
    End Function

    ''' <summary>结束拖拽状态（鼠标释放时调用）。</summary>
    Public Sub EndDrag()
        IsDragging = False
    End Sub

    ''' <summary>清除悬停标记（如鼠标离开控件时）；返回 True 表示状态发生变化。</summary>
    Public Function ResetHover() As Boolean
        If IsHover Then
            IsHover = False
            Return True
        End If
        Return False
    End Function

    ''' <summary>
    ''' 处理竖向鼠标滚轮，按 <paramref name="scrollStep"/> 调整偏移并裁剪到合法区间。
    ''' </summary>
    Public Shared Function HandleWheel(delta As Integer, scrollOffset As Integer,
                                       totalCount As Integer, visibleCount As Integer,
                                       Optional scrollStep As Integer = 3) As Integer
        Dim d As Integer = -Math.Sign(delta) * scrollStep
        Dim maxOff As Integer = Math.Max(0, totalCount - visibleCount)
        Return Math.Max(0, Math.Min(maxOff, scrollOffset + d))
    End Function

    ''' <summary>
    ''' 返回内容区域需要为滚动条预留的右侧宽度（含 Margin）。布局未计算时返回 0。
    ''' </summary>
    Public Function GetReservedWidth(containerW As Integer, inset As Integer) As Integer
        If TrackRect.IsEmpty Then Return 0
        Return containerW - inset - VisualLeft
    End Function

#Region "横向滚动条"

    ''' <summary>计算横向滚动条的轨道与滑块矩形。调用方在每次重绘前调用。</summary>
    Public Sub ComputeHorizontalLayout(containerW As Integer, containerH As Integer,
                                       borderWidth As Integer, borderRadius As Integer,
                                       paddingLeft As Integer, paddingRight As Integer,
                                       scrollBarHeight As Integer,
                                       totalContentWidth As Integer, visibleWidth As Integer,
                                       scrollPixelOffset As Integer)
        Dim inset As Integer = Math.Max(borderWidth, If(borderRadius > 0, borderRadius \ 2, 0))
        Dim sbX As Integer = inset + Margin + paddingLeft
        Dim sbY As Integer = containerH - scrollBarHeight - inset - Margin
        Dim sbW As Integer = containerW - (inset + Margin) * 2 - paddingLeft - paddingRight
        If sbW <= 0 OrElse scrollBarHeight <= 0 Then
            ThumbRect = Rectangle.Empty
            TrackRect = Rectangle.Empty
            VisualTop = containerH
            Return
        End If

        VisualTop = sbY
        TrackRect = New Rectangle(sbX, sbY - Margin, sbW, scrollBarHeight + Margin * 2)

        Dim maxOff As Integer = Math.Max(0, totalContentWidth - visibleWidth)
        Dim thumbW As Integer = Math.Max(20, CInt(sbW * visibleWidth / Math.Max(1, totalContentWidth)))
        Dim thumbX As Integer = sbX
        If maxOff > 0 Then
            thumbX = sbX + CInt((sbW - thumbW) * scrollPixelOffset / maxOff)
        End If
        ThumbRect = New Rectangle(thumbX, sbY - Margin, thumbW, scrollBarHeight + Margin * 2)
    End Sub

    ''' <summary>横向版拖拽开始。</summary>
    Public Function BeginDragHorizontal(mouseLocation As Point, scrollOffset As Integer) As Boolean
        If ThumbRect.Contains(mouseLocation) Then
            IsDragging = True
            _dragStartX = mouseLocation.X
            _dragStartOffset = scrollOffset
            Return True
        End If
        Return False
    End Function

    ''' <summary>横向版拖拽过程中换算新的像素偏移。</summary>
    Public Function DragMoveHorizontal(mouseX As Integer, totalContentWidth As Integer, visibleWidth As Integer) As Integer
        If Not IsDragging Then Return _dragStartOffset
        Dim trackW As Integer = TrackRect.Width
        Dim maxOff As Integer = Math.Max(0, totalContentWidth - visibleWidth)
        Dim thumbW As Integer = Math.Max(20, CInt(trackW * visibleWidth / Math.Max(1, totalContentWidth)))
        Dim usableW As Integer = trackW - thumbW
        If usableW <= 0 Then Return _dragStartOffset
        Dim dx As Integer = mouseX - _dragStartX
        Dim newOff As Integer = _dragStartOffset + CInt(dx * maxOff / usableW)
        Return Math.Max(0, Math.Min(maxOff, newOff))
    End Function

    ''' <summary>横向版轨道点击：点击滑块两侧空白区时按一个可视宽度翻页。</summary>
    Public Function TrackClickHorizontal(mouseLocation As Point, scrollOffset As Integer,
                                         totalContentWidth As Integer, visibleWidth As Integer) As Integer
        If Not TrackRect.Contains(mouseLocation) Then Return scrollOffset
        Dim maxOff As Integer = Math.Max(0, totalContentWidth - visibleWidth)
        Dim pageSize As Integer = Math.Max(1, visibleWidth)
        If mouseLocation.X < ThumbRect.X Then
            Return Math.Max(0, scrollOffset - pageSize)
        Else
            Return Math.Min(maxOff, scrollOffset + pageSize)
        End If
    End Function

    ''' <summary>处理横向鼠标滚轮（以像素为单位调整偏移）。</summary>
    Public Shared Function HandleHorizontalWheel(delta As Integer, scrollOffset As Integer,
                                                  totalContentWidth As Integer, visibleWidth As Integer,
                                                  Optional scrollStep As Integer = 40) As Integer
        Dim d As Integer = -Math.Sign(delta) * scrollStep
        Dim maxOff As Integer = Math.Max(0, totalContentWidth - visibleWidth)
        Return Math.Max(0, Math.Min(maxOff, scrollOffset + d))
    End Function

#End Region

#Region "D2D 渲染"

    ''' <summary>
    ''' 当目标 RenderTarget 或任一颜色变更时，重建并缓存三只 SolidColorBrush；
    ''' 否则保持复用。可显著降低高频重绘时的画刷创建/销毁开销。
    ''' </summary>
    Private Sub EnsureBrushes(rt As ID2D1RenderTarget,
                              trackColor As Color, thumbColor As Color, thumbHoverColor As Color)
        ' RenderTarget 切换时必须丢弃旧画刷（它们与原 RT 绑定）
        If Not Object.ReferenceEquals(_cachedRT, rt) Then
            DisposeBrushes()
            _cachedRT = rt
        End If

        If _trackBrush Is Nothing OrElse Not _trackBrushColor.Equals(trackColor) Then
            _trackBrush?.Dispose()
            _trackBrush = rt.CreateSolidColorBrush(D2DHelper.ToColor4(trackColor))
            _trackBrushColor = trackColor
        End If
        If _thumbBrush Is Nothing OrElse Not _thumbBrushColor.Equals(thumbColor) Then
            _thumbBrush?.Dispose()
            _thumbBrush = rt.CreateSolidColorBrush(D2DHelper.ToColor4(thumbColor))
            _thumbBrushColor = thumbColor
        End If
        If _thumbHoverBrush Is Nothing OrElse Not _thumbHoverBrushColor.Equals(thumbHoverColor) Then
            _thumbHoverBrush?.Dispose()
            _thumbHoverBrush = rt.CreateSolidColorBrush(D2DHelper.ToColor4(thumbHoverColor))
            _thumbHoverBrushColor = thumbHoverColor
        End If
    End Sub

    ''' <summary>释放缓存的 D2D 画刷。RenderTarget 重建/控件释放时调用。</summary>
    Public Sub DisposeBrushes()
        _trackBrush?.Dispose() : _trackBrush = Nothing
        _thumbBrush?.Dispose() : _thumbBrush = Nothing
        _thumbHoverBrush?.Dispose() : _thumbHoverBrush = Nothing
        _trackBrushColor = Color.Empty
        _thumbBrushColor = Color.Empty
        _thumbHoverBrushColor = Color.Empty
        _cachedRT = Nothing
    End Sub

    ''' <summary>
    ''' 当存在圆角边框时，PushLayer 一个对应几何掩膜，避免滚动条溢出圆角外。
    ''' 返回的 layer/clipGeo 由调用方在 Finally 中释放。
    ''' </summary>
    Private Shared Sub PushClipLayerIfNeeded(rt As ID2D1RenderTarget,
                                              containerW As Integer, containerH As Integer,
                                              borderWidth As Integer, borderRadius As Integer,
                                              ByRef clipPushed As Boolean,
                                              ByRef clipGeo As Vortice.Direct2D1.ID2D1Geometry)
        clipPushed = False
        If borderRadius <= 0 Then Return
        Dim clipRect As New RectangleF(0, 0, containerW - 1, containerH - 1)
        If borderWidth > 0 Then
            Dim half As Single = borderWidth / 2.0F
            clipRect.Inflate(-half, -half)
        End If
        clipGeo = RectangleRenderer.创建圆角矩形几何(clipRect, borderRadius)
        D2DHelper.PushGeometryClip(rt, clipGeo, New RectangleF(0, 0, containerW, containerH))
        clipPushed = True
    End Sub

    ''' <summary>D2D 版竖向滚动条绘制。调用前需先调用 <see cref="ComputeLayout"/> 计算布局。</summary>
    Public Sub Draw_D2D(rt As ID2D1RenderTarget,
                         containerW As Integer, containerH As Integer,
                         borderWidth As Integer, borderRadius As Integer,
                         scrollBarWidth As Integer,
                         trackColor As Color, thumbColor As Color, thumbHoverColor As Color)
        If TrackRect.IsEmpty Then Return
        If TrackRect.Width < 1 OrElse TrackRect.Height < 1 OrElse scrollBarWidth < 1 Then Return

        EnsureBrushes(rt, trackColor, thumbColor, thumbHoverColor)

        Dim clipPushed As Boolean = False
        Dim clipGeo As ID2D1Geometry = Nothing
        PushClipLayerIfNeeded(rt, containerW, containerH, borderWidth, borderRadius, clipPushed, clipGeo)
        Try
            Dim sbH As Integer = TrackRect.Height
            ' 轨道（A=0 表示完全透明，直接跳过以节省一次填充）
            If trackColor.A > 0 Then
                Dim trackRadius As Integer = Math.Min(scrollBarWidth \ 2, sbH \ 2)
                Dim trackArea As New RectangleF(VisualLeft, TrackRect.Y, scrollBarWidth, sbH)
                Using geo = RectangleRenderer.创建圆角矩形几何(trackArea, trackRadius)
                    rt.FillGeometry(geo, _trackBrush)
                End Using
            End If

            ' 滑块；悬停或拖拽时使用 hover 色
            Dim thumbBr As ID2D1SolidColorBrush = If(IsDragging OrElse IsHover, _thumbHoverBrush, _thumbBrush)
            Dim thumbH As Integer = ThumbRect.Height
            Dim thumbRadius As Integer = Math.Min(scrollBarWidth \ 2, thumbH \ 2)
            Dim thumbArea As New RectangleF(VisualLeft, ThumbRect.Y, scrollBarWidth, thumbH)
            Using geo = RectangleRenderer.创建圆角矩形几何(thumbArea, thumbRadius)
                rt.FillGeometry(geo, thumbBr)
            End Using
        Finally
            If clipPushed Then
                rt.PopLayer()
            End If
            clipGeo?.Dispose()
        End Try
    End Sub

    ''' <summary>D2D 版横向滚动条绘制。调用前需先调用 <see cref="ComputeHorizontalLayout"/> 计算布局。</summary>
    Public Sub DrawHorizontal_D2D(rt As ID2D1RenderTarget,
                                   containerW As Integer, containerH As Integer,
                                   borderWidth As Integer, borderRadius As Integer,
                                   scrollBarHeight As Integer,
                                   trackColor As Color, thumbColor As Color, thumbHoverColor As Color)
        If TrackRect.IsEmpty Then Return
        If TrackRect.Width < 1 OrElse TrackRect.Height < 1 OrElse scrollBarHeight < 1 Then Return

        EnsureBrushes(rt, trackColor, thumbColor, thumbHoverColor)

        Dim clipPushed As Boolean = False
        Dim clipGeo As ID2D1Geometry = Nothing
        PushClipLayerIfNeeded(rt, containerW, containerH, borderWidth, borderRadius, clipPushed, clipGeo)
        Try
            Dim sbW As Integer = TrackRect.Width
            If trackColor.A > 0 Then
                Dim trackRadius As Integer = Math.Min(scrollBarHeight \ 2, sbW \ 2)
                Dim trackArea As New RectangleF(TrackRect.X, VisualTop, sbW, scrollBarHeight)
                Using geo = RectangleRenderer.创建圆角矩形几何(trackArea, trackRadius)
                    rt.FillGeometry(geo, _trackBrush)
                End Using
            End If

            Dim thumbBr As ID2D1SolidColorBrush = If(IsDragging OrElse IsHover, _thumbHoverBrush, _thumbBrush)
            Dim thumbW As Integer = ThumbRect.Width
            Dim thumbRadius As Integer = Math.Min(scrollBarHeight \ 2, thumbW \ 2)
            Dim thumbArea As New RectangleF(ThumbRect.X, VisualTop, thumbW, scrollBarHeight)
            Using geo = RectangleRenderer.创建圆角矩形几何(thumbArea, thumbRadius)
                rt.FillGeometry(geo, thumbBr)
            End Using
        Finally
            If clipPushed Then
                rt.PopLayer()
            End If
            clipGeo?.Dispose()
        End Try
    End Sub

#End Region

End Class
