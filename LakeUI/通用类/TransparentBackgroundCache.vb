Imports System.Drawing.Imaging

''' <summary>
''' 透明控件背景共享缓存与统一接入入口（LakeUI 全库唯一的透明渲染机制）。
'''
''' === 设计动机 ===
''' 当多个透明控件（ModernPanel / ModernButton / HtmlColorLabel /
''' ModernTabListControl 内容面板等）叠在同一个背景源上时，每帧每个控件都把整个
''' 背景源画到一张 Bitmap 是巨大的性能浪费。本类按背景源控件分组，
''' 一帧内（短 TTL）的所有取样请求共享同一张 Bitmap。
'''
''' === 统一图层契约（重要） ===
''' LakeUI 的所有"透明感知控件"都不依赖 .NET WinForms 的默认透明合成，而是
''' 一律走本机制采样父级 / 指定背景源，再在采样底图之上以 D2D 叠加自身图层。
''' 这条契约的关键点：
'''
''' 1) BackColor 的语义重定义：
'''    • BackColor.A = 255 → 完全不透明纯色，控件按普通实色背景绘制，
'''      无需采样背景源（除非启用了圆角，圆角外的空白仍由本机制透出）。
'''    • BackColor.A &lt; 255（含 0/Transparent）→ 把 BackColor 当作"带颜色的
'''      半透明遮罩"。绘制顺序固定为：① 先用本机制采样背景源 → ② 再把 BackColor
'''      作为一层半透明遮罩盖到底图上。A=0 时遮罩层退化为不绘制，与"完全透明"等价。
'''
''' 2) 标准图层叠加顺序（自下而上）：
'''        采样底图（仅 A&lt;255 或圆角时）
'''     → BackColor 半透明遮罩（仅 0 &lt; A &lt; 255 时绘制）
'''     → 控件自身的实色填充（背景图 / BackColor1 / 渐变 / 多状态色 等）
'''     → ModernPanel 专属的 OverlayColor（仅 ModernPanel）
'''     → 边框
'''    其中 BackColor1 是控件实际填充色（位于 BackColor 遮罩之上），
'''    BackColor2 是 ModernPanel 特有的渐变副色，OverlayColor 是 ModernPanel 特有
'''    的容器叠加层（用于在透明模式下区分容器边界），其他控件不应增加这两个属性。
'''
''' 3) 圆角空白也由本机制透出：当 BorderRadius &gt; 0 时，圆角之外、控件矩形之内
'''    的"空缺区"必须显示父级背景，因此触发条件是
'''        BorderRadius &gt; 0 OrElse BackColor.A &lt; 255
'''    满足任一即走本机制采样底图，再按上面的标准顺序叠加。
'''
''' === 屏蔽 .NET 默认透明渲染的三件套 ===
''' 1) 构造里调用 SetStyle(ControlStyles.SupportsTransparentBackColor, True)，
'''    并允许 BackColor 为 Color.Transparent 或 A &lt; 255 的 ARGB 颜色。
''' 2) 重写 OnPaintBackground：当 BorderRadius &gt; 0 OrElse BackColor.A &lt; 255 时
'''    直接 Return，防止基类用纯色填底或默认透明合成覆盖本机制的输出。
''' 3) 重写 OnPaint：在绘制自身内容之前先调用
'''        TransparentBackgroundCache.PaintBackgroundFor(Me, e.Graphics, _backgroundSource)
'''    贴底图，再按"BackColor 遮罩 → 自身填充 → (OverlayColor) → 边框"顺序绘制。
'''
''' === 接入新控件的清单 ===
''' 1) 完成上述"三件套"。
''' 2) 暴露 BackgroundSource As Control 属性，Setter 中调用 Me.Invalidate() 即可，
'''    不需要手动 Invalidate 缓存（控件 Dispose 时缓存自动清理）。
''' 3) 控件自身的尺寸/视觉变化由 Invalidate() 触发重绘即可，缓存键是"背景源控件"，
'''    与子控件无关；只有"背景源"自身视觉变化时才需要调用
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

    ''' <summary>
    ''' 缓存最长有效时长（毫秒）：达到此时间后无论是否 Dirty 都强制刷新一次，作为兜底。
    ''' 把它放大到 1 秒级是为了让 hover/动画这类频繁但视觉上无关的 Invalidated 不会拖累透明子控件。
    ''' </summary>
    Private Const CacheTtlMs As Integer = 1000

    ''' <summary>
    ''' Dirty 节流：source 触发 Invalidated 后，距上次实际重建至少要等这么久才允许真正重建底图。
    ''' 用于过滤"鼠标进入兄弟控件"这种与底图无关的高频脏标记。设得偏大可显著降低 CPU；
    ''' 同时由于切换选项卡 / Resize 都会显式 Invalidate，视觉滞后基本无感。
    ''' </summary>
    Private Const DirtyMinIntervalMs As Integer = 120

    Private Class Entry
        Public Bmp As Bitmap
        Public Width As Integer
        Public Height As Integer
        Public Stamp As Long
        ''' <summary>事件驱动的"脏"标记：source.Invalidated / Resize 触发后置 True，下次取样重建。</summary>
        Public Dirty As Boolean = True
        ''' <summary>高优先级脏标记：Resize 或显式 Invalidate(source) 时置位，绕过 DirtyMinIntervalMs 节流。</summary>
        Public ForceDirty As Boolean = True
        ''' <summary>正在重建该 source 的位图：用于阻断递归（child 的 BackgroundSource 指向自身祖先时）。</summary>
        Public Painting As Boolean
        ''' <summary>跨帧缓存的 D2D 位图（与 Bmp 同步重建），避免每帧 LockBits + CreateBitmap 上传。</summary>
        Public D2DBmp As Vortice.Direct2D1.ID2D1Bitmap
        Public D2DStamp As Long
        ''' <summary>D2DBmp 来源的 RT，用于校验是否兼容（不同控件 RT 不能复用）。</summary>
        Public D2DOwnerRT As WeakReference
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
        Dim bmp As Bitmap = AcquireSourceBitmap(source)
        If bmp Is Nothing Then Return
        g.DrawImage(bmp, destRect, srcRect, GraphicsUnit.Pixel)
    End Sub

    ''' <summary>
    ''' D2D 版高层 API：把背景源采样位图绘制到 D2D RenderTarget 上指定子控件位置。
    ''' 内部复用与 GDI 路径相同的位图缓存。
    ''' </summary>
    Public Sub PaintBackgroundFor_D2D(child As Control, rt As Vortice.Direct2D1.ID2D1RenderTarget, explicitSource As Control)
        If child Is Nothing OrElse rt Is Nothing Then Return
        Dim source As Control = ResolveSource(child, explicitSource)
        If source Is Nothing Then Return
        Dim sw As Integer = source.Width, sh As Integer = source.Height
        If sw <= 0 OrElse sh <= 0 Then Return
        Dim offset As Point = ComputeOffset(child, source)
        DrawSourceRegion_D2D(rt, source,
            New Rectangle(offset.X, offset.Y, child.Width, child.Height),
            New Rectangle(0, 0, child.Width, child.Height))
    End Sub

    ''' <summary>D2D 低层 API：跨帧缓存 ID2D1Bitmap，仅在 GDI 位图刷新时才重新上传。</summary>
    Public Sub DrawSourceRegion_D2D(rt As Vortice.Direct2D1.ID2D1RenderTarget, source As Control,
                                     srcRect As Rectangle, destRect As Rectangle)
        If rt Is Nothing OrElse source Is Nothing Then Return
        Dim bmp As Bitmap = AcquireSourceBitmap(source)
        If bmp Is Nothing Then Return

        Dim entry As Entry = Nothing
        SyncLock _cache
            _cache.TryGetValue(source, entry)
        End SyncLock
        If entry Is Nothing Then Return

        Dim d2dBmp As Vortice.Direct2D1.ID2D1Bitmap = Nothing
        Dim ownerRtAlive As Boolean =
            entry.D2DOwnerRT IsNot Nothing AndAlso ReferenceEquals(entry.D2DOwnerRT.Target, rt)
        If entry.D2DBmp IsNot Nothing AndAlso ownerRtAlive AndAlso entry.D2DStamp = entry.Stamp Then
            d2dBmp = entry.D2DBmp
        Else
            ' GDI 位图已刷新或 RT 改变 → 重新上传一次。
            If entry.D2DBmp IsNot Nothing Then
                Try : entry.D2DBmp.Dispose() : Catch : End Try
                entry.D2DBmp = Nothing
            End If
            d2dBmp = D2DHelper.CreateBitmapFromGdi(rt, bmp)
            If d2dBmp Is Nothing Then Return
            entry.D2DBmp = d2dBmp
            entry.D2DStamp = entry.Stamp
            entry.D2DOwnerRT = New WeakReference(rt)
        End If

        rt.DrawBitmap(d2dBmp,
            D2DHelper.ToD2DRect(destRect),
            1.0F,
            Vortice.Direct2D1.BitmapInterpolationMode.Linear,
            D2DHelper.ToD2DRect(srcRect))
    End Sub

    ''' <summary>取得 source 的最新缓存位图（按 TTL 重建）。返回 Nothing 表示当前不可采样。</summary>
    Private Function AcquireSourceBitmap(source As Control) As Bitmap
        If source Is Nothing Then Return Nothing
        Dim sw As Integer = source.Width, sh As Integer = source.Height
        If sw <= 0 OrElse sh <= 0 Then Return Nothing

        Dim now As Long = _sw.ElapsedMilliseconds
        Dim entry As Entry = Nothing
        Dim hit As Boolean = False
        SyncLock _cache
            If _cache.TryGetValue(source, entry) Then
                If entry.Painting Then Return Nothing
                Dim sizeOk As Boolean = (entry.Width = sw AndAlso entry.Height = sh AndAlso entry.Bmp IsNot Nothing)
                If Not sizeOk Then entry.Dirty = True
                ' 节流：Dirty 但距上次重建不到 DirtyMinIntervalMs 时仍视为命中（继续用旧底图）。
                ' 这样即便 source 因兄弟控件 hover 而频繁 Invalidated，也不会每帧都重采样。
                ' 尺寸变化 / 显式 Invalidate(source) 是高优先级，绕开节流。
                Dim canSkipDirty As Boolean = entry.Dirty AndAlso sizeOk AndAlso Not entry.ForceDirty AndAlso
                    (now - entry.Stamp) < DirtyMinIntervalMs
                If sizeOk AndAlso (Not entry.Dirty OrElse canSkipDirty) AndAlso (now - entry.Stamp) <= CacheTtlMs Then
                    hit = True
                End If
            Else
                entry = New Entry()
                _cache(source) = entry
                AddHandler source.Disposed, AddressOf OnSourceDisposed
                AddHandler source.Invalidated, AddressOf OnSourceInvalidated
                AddHandler source.Resize, AddressOf OnSourceResized
            End If
            If Not hit Then entry.Painting = True
        End SyncLock

        If Not hit Then
            Try
                If entry.Bmp Is Nothing OrElse entry.Width <> sw OrElse entry.Height <> sh Then
                    entry.Bmp?.Dispose()
                    entry.Bmp = New Bitmap(sw, sh, PixelFormat.Format32bppPArgb)
                    entry.Width = sw
                    entry.Height = sh
                End If
                Using bg As Graphics = Graphics.FromImage(entry.Bmp)
                    bg.Clear(Color.Transparent)
                    Using pea As New PaintEventArgs(bg, New Rectangle(0, 0, sw, sh))
                        InvokePaintBackgroundProxy(source, pea)
                        InvokePaintProxy(source, pea)
                    End Using
                End Using
                ' ── 关键修复：强制把缓存位图的 alpha 通道恢复为 255 ──
                ' 背景采样位图按定义代表"源控件的不透明视图"。但当 source 控件（例如 ModernPanel）
                ' 内部使用 ID2D1DCRenderTarget 在该位图的 HDC 上绘制时，D2D EndDraw 通过 GDI BitBlt
                ' 把内部表面写回 32bpp DIB Section，会把覆盖到的像素 alpha 清成 0（GDI 经典行为）。
                ' 任何整面覆盖的 D2D 操作（OverlayColor.A>0、纯色背景等）都会引发该问题，导致后续
                ' 子控件 DrawImage 时把整层视为完全透明，视觉表现为"跳过本层、采样到更外层"。
                ForceOpaqueAlpha(entry.Bmp)
                entry.Stamp = now
                entry.Dirty = False
                entry.ForceDirty = False
            Finally
                SyncLock _cache
                    entry.Painting = False
                End SyncLock
            End Try
        End If

        Return entry.Bmp
    End Function

    ''' <summary>
    ''' 显式使指定控件的缓存失效。背景源自身的视觉重大变化（如换图/换主题）时调用，
    ''' 普通的子控件位置/状态变化不需要调用本方法，让常规 Invalidate + TTL 处理即可。
    ''' </summary>
    ''' <summary>
    ''' 显式使指定控件的缓存立即失效。背景源自身的视觉重大变化（如选项卡切换、换图、换主题）时调用，
    ''' 会绕过 DirtyMinIntervalMs 节流，下次绘制立即重采样。
    ''' 普通的子控件 hover/状态变化不需要调用本方法。
    ''' </summary>
    Public Sub Invalidate(source As Control)
        If source Is Nothing Then Return
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then
                entry.Dirty = True
                entry.ForceDirty = True
            End If
        End SyncLock
    End Sub

    ''' <summary>
    ''' 来自 source.Invalidated 的低优先级脏标记：仅打 Dirty，由 DirtyMinIntervalMs 节流决定是否真正重建。
    ''' 这样兄弟控件 hover 引起的 Invalidated 风暴不会拖累透明子控件。
    ''' </summary>
    Private Sub OnSourceInvalidated(sender As Object, e As EventArgs)
        Dim source = TryCast(sender, Control)
        If source Is Nothing Then Return
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then entry.Dirty = True
        End SyncLock
    End Sub

    ''' <summary>Resize 是高优先级失效，必须立即重建（尺寸变化下旧底图直接错位）。</summary>
    Private Sub OnSourceResized(sender As Object, e As EventArgs)
        Dim source = TryCast(sender, Control)
        If source Is Nothing Then Return
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then
                entry.Dirty = True
                entry.ForceDirty = True
            End If
        End SyncLock
    End Sub

    Private Sub OnSourceDisposed(sender As Object, e As EventArgs)
        Dim source = TryCast(sender, Control)
        If source Is Nothing Then Return
        RemoveHandler source.Disposed, AddressOf OnSourceDisposed
        RemoveHandler source.Invalidated, AddressOf OnSourceInvalidated
        RemoveHandler source.Resize, AddressOf OnSourceResized
        SyncLock _cache
            Dim entry As Entry = Nothing
            If _cache.TryGetValue(source, entry) Then
                entry.Bmp?.Dispose()
                If entry.D2DBmp IsNot Nothing Then
                    Try : entry.D2DBmp.Dispose() : Catch : End Try
                End If
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

    ''' <summary>
    ''' 把 32bpp 位图的 alpha 通道全部置为 255。
    ''' 见 AcquireSourceBitmap 中的注释——用于消除 D2D + GDI BitBlt 在内存 DIB 上对 alpha 的破坏。
    ''' </summary>
    Private Sub ForceOpaqueAlpha(bmp As Bitmap)
        If bmp Is Nothing Then Return
        Dim rect As New Rectangle(0, 0, bmp.Width, bmp.Height)
        Dim data As BitmapData = Nothing
        Try
            data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb)
            Dim stride As Integer = data.Stride
            Dim h As Integer = data.Height
            Dim w As Integer = data.Width
            Dim scan0 As IntPtr = data.Scan0
            ' BGRA 顺序，alpha 在每 4 字节里的最后一个字节。
            ' 当 alpha 被 BitBlt 写成 0 时，BGR 通常仍是有效的颜色字节（GDI 不预乘），
            ' 直接把 A 设为 255 即可恢复成不透明的正确颜色。
            Dim row(stride - 1) As Byte
            For y As Integer = 0 To h - 1
                Dim rowPtr As IntPtr = IntPtr.Add(scan0, y * stride)
                Runtime.InteropServices.Marshal.Copy(rowPtr, row, 0, stride)
                Dim x As Integer = 3
                For i As Integer = 0 To w - 1
                    row(x) = 255
                    x += 4
                Next
                Runtime.InteropServices.Marshal.Copy(row, 0, rowPtr, stride)
            Next
        Catch
            ' LockBits 失败时静默忽略，下一帧会重试
        Finally
            If data IsNot Nothing Then
                Try : bmp.UnlockBits(data) : Catch : End Try
            End If
        End Try
    End Sub

End Module
