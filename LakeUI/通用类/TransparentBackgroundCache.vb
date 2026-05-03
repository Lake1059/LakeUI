Imports System.Drawing.Imaging

''' <summary>
''' 透明控件背景共享缓存与统一接入入口。
'''
''' === 设计动机 ===
''' 当多个透明控件（ModernPanel / ModernButton / HtmlColorLabel /
''' ModernTabListControl 内容面板等）叠在同一个背景源上时，每帧每个控件都把整个
''' 背景源画到一张 Bitmap 是巨大的性能浪费。本类按背景源控件分组，
''' 一帧内（短 TTL）的所有取样请求共享同一张 Bitmap。
'''
''' === 接入新控件的清单 ===
''' 1) 构造里调用 SetStyle(ControlStyles.SupportsTransparentBackColor, True)，
'''    并允许 BackColor 为 Color.Transparent 或 A &lt; 255 的颜色。
''' 2) 重写 OnPaintBackground：当存在圆角 / 半透明背景 / 透明 BackColor 时直接 Return，
'''    防止基类用纯色填底覆盖透出内容。
''' 3) 重写 OnPaint：在绘制自身内容之前调用
'''        TransparentBackgroundCache.PaintBackgroundFor(Me, e.Graphics, _backgroundSource)
'''    即可贴底图。
''' 4) 暴露 BackgroundSource As Control 属性，存到字段里传入步骤 3。Setter 中调用
'''    Me.Invalidate() 即可，不需要手动 Invalidate 缓存（控件 Dispose 时缓存自动清理）。
''' 5) 控件自身的尺寸/视觉变化由 Invalidate() 触发重绘即可，缓存键是“背景源控件”，
'''    与子控件无关；只有“背景源”自身视觉变化时才需要调用
'''    TransparentBackgroundCache.Invalidate(source) 来强制重建。
'''
''' === 已知限制（接入前需了解） ===
''' • 缓存仅复制 source 自身的 OnPaintBackground + OnPaint 输出，不递归绘制 source 的子控件。
'''   这是为避免 N×N 渲染必须做出的取舍：若需要看到 source 的子控件，请把 BackgroundSource
'''   指向更外层的容器（例如顶层 Form）。
''' • 不要把 BackgroundSource 设到自身或祖先链中本身处于"正在被采样"状态的控件上：会触发
'''   递归。模块内置一层 reentrancy 防护，递归时会回退为透明背景而不是栈溢出，但视觉
'''   会缺底图。
''' • TTL 约一帧（CacheTtlMs ms）。需要立即看到背景变化（如背景动画）时，请在背景源
'''   重绘后调用 TransparentBackgroundCache.Invalidate(source)。
''' </summary>
Friend Module TransparentBackgroundCache

    ''' <summary>缓存有效时长（毫秒）。约一帧时间，足以让同一帧内多控件共享，又不至于显示陈旧数据。</summary>
    Private Const CacheTtlMs As Integer = 16

    Private Class Entry
        Public Bmp As Bitmap
        Public Width As Integer
        Public Height As Integer
        Public Stamp As Long
        ''' <summary>正在重建该 source 的位图：用于阻断递归（child 的 BackgroundSource 指向自身祖先时）。</summary>
        Public Painting As Boolean
    End Class

    Private ReadOnly _cache As New Dictionary(Of Control, Entry)
    Private ReadOnly _sw As Stopwatch = Stopwatch.StartNew()

    ''' <summary>
    ''' 高层 API：为透明控件 child 在 g 上贴一层来自 explicitSource（或 child.Parent）的底图。
    ''' 已封装：解析有效背景源、计算偏移、共享缓存与递归防护。
    ''' </summary>
    ''' <param name="child">需要绘制底图的透明控件本身。</param>
    ''' <param name="g">child.OnPaint 的 Graphics（坐标系以 child 自身 (0,0) 为原点）。</param>
    ''' <param name="explicitSource">显式指定的背景采样源；为 Nothing 时使用 child.Parent。</param>
    Public Sub PaintBackgroundFor(child As Control, g As Graphics, explicitSource As Control)
        If child Is Nothing OrElse g Is Nothing Then Return
        Dim source As Control = ResolveSource(child, explicitSource)
        If source Is Nothing Then Return
        Dim sw As Integer = source.Width, sh As Integer = source.Height
        If sw <= 0 OrElse sh <= 0 Then Return
        Dim offset As Point = ComputeOffset(child, source)
        DrawSourceRegion(
            g, source,
            New Rectangle(offset.X, offset.Y, child.Width, child.Height),
            New Rectangle(0, 0, child.Width, child.Height))
    End Sub

    ''' <summary>
    ''' 低层 API：把 source 的指定子区域绘制到 g 的 dest 矩形。内部按需重建 source 的离屏位图缓存。
    ''' 一般控件应使用 <see cref="PaintBackgroundFor"/>；此方法保留给需要自定义偏移/裁切的特殊场景。
    ''' </summary>
    Public Sub DrawSourceRegion(g As Graphics, source As Control, srcRect As Rectangle, destRect As Rectangle)
        If source Is Nothing Then Return
        Dim sw As Integer = source.Width, sh As Integer = source.Height
        If sw <= 0 OrElse sh <= 0 Then Return

        Dim now As Long = _sw.ElapsedMilliseconds
        Dim entry As Entry = Nothing
        Dim hit As Boolean = False
        SyncLock _cache
            If _cache.TryGetValue(source, entry) Then
                If entry.Painting Then
                    ' 递归：child 的 BackgroundSource 指向了正在被采样的 source（或其祖先在被采样）。
                    ' 直接返回，等价于该层贴一张透明底图，避免 StackOverflow。
                    Return
                End If
                If entry.Width = sw AndAlso entry.Height = sh AndAlso
                   entry.Bmp IsNot Nothing AndAlso (now - entry.Stamp) <= CacheTtlMs Then
                    hit = True
                End If
            Else
                entry = New Entry()
                _cache(source) = entry
                ' 首次见到该控件，挂一次 Disposed 钩子做缓存清理（Bitmap/字典都释放）。
                AddHandler source.Disposed, AddressOf OnSourceDisposed
            End If
            If Not hit Then entry.Painting = True
        End SyncLock

        If Not hit Then
            Try
                ' 重建位图（仅当尺寸变化时才重新分配）
                If entry.Bmp Is Nothing OrElse entry.Width <> sw OrElse entry.Height <> sh Then
                    entry.Bmp?.Dispose()
                    entry.Bmp = New Bitmap(sw, sh, PixelFormat.Format32bppPArgb)
                    entry.Width = sw
                    entry.Height = sh
                End If
                ' 把 source 完整绘制到位图：以 source 自身 (0,0) 为原点，
                ' 这样 GDI（TextRenderer）等不遵循 Transform 的绘制 API 也能落在正确位置。
                ' 注意：这里只调用 source 自身的 InvokePaintBackground/InvokePaint，
                ' 不递归其子控件——避免 N 个透明控件相互渲染造成 N×N 爆炸。
                Using bg As Graphics = Graphics.FromImage(entry.Bmp)
                    bg.Clear(Color.Transparent)
                    Using pea As New PaintEventArgs(bg, New Rectangle(0, 0, sw, sh))
                        InvokePaintBackgroundProxy(source, pea)
                        InvokePaintProxy(source, pea)
                    End Using
                End Using
                entry.Stamp = now
            Finally
                SyncLock _cache
                    entry.Painting = False
                End SyncLock
            End Try
        End If

        g.DrawImage(entry.Bmp, destRect, srcRect, GraphicsUnit.Pixel)
    End Sub

    ''' <summary>
    ''' 显式使指定控件的缓存失效。背景源自身的视觉重大变化（如换图/换主题）时调用，
    ''' 普通的子控件位置/状态变化不需要调用本方法，让常规 Invalidate + TTL 处理即可。
    ''' </summary>
    Public Sub Invalidate(source As Control)
        If source Is Nothing Then Return
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then
                entry.Stamp = 0
            End If
        End SyncLock
    End Sub

    Private Sub OnSourceDisposed(sender As Object, e As EventArgs)
        Dim source = TryCast(sender, Control)
        If source Is Nothing Then Return
        RemoveHandler source.Disposed, AddressOf OnSourceDisposed
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then
                entry.Bmp?.Dispose()
                _cache.Remove(source)
            End If
        End SyncLock
    End Sub

    ''' <summary>
    ''' 解析有效的背景采样源：显式指定优先；否则使用 child.Parent。
    ''' 显式 source 已 Disposed 时回退到 Parent，保证设计期/运行期都不致 NRE。
    ''' </summary>
    Private Function ResolveSource(child As Control, explicitSource As Control) As Control
        If explicitSource IsNot Nothing AndAlso Not explicitSource.IsDisposed Then
            Return explicitSource
        End If
        Return child.Parent
    End Function

    ''' <summary>
    ''' 计算 child 在 source 客户区中的偏移。
    ''' 若 source 在 child 的祖先链上，逐级累加 Left/Top（始终是客户区坐标，对修改了
    ''' 标题栏/NC 区的 Form 也完全准确）；否则退化到屏幕坐标转换。
    ''' </summary>
    Private Function ComputeOffset(child As Control, source As Control) As Point
        Dim ox As Integer = 0, oy As Integer = 0
        Dim ctrl As Control = child
        While ctrl IsNot Nothing AndAlso ctrl IsNot source
            ox += ctrl.Left
            oy += ctrl.Top
            ctrl = ctrl.Parent
        End While
        If ctrl Is source Then Return New Point(ox, oy)
        ' source 不在祖先链上，退化使用屏幕坐标转换
        Return source.PointToClient(child.PointToScreen(Point.Empty))
    End Function

    ' ── InvokePaint / InvokePaintBackground 是 Control 的 protected 成员，
    '   通过反射代理调用，绕过模块上下文限制。 ─────────────────────────────
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
        ' 第 1 个参数是 "this"（任意 Control 实例都可，作为调用者上下文），
        ' 第 2 个参数才是真正被绘制的目标。
        _invokePaintBackground(source, source, pea)
    End Sub

    Private Sub InvokePaintProxy(source As Control, pea As PaintEventArgs)
        _invokePaint(source, source, pea)
    End Sub

End Module
