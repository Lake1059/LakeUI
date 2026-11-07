using System.Drawing.Imaging;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using LakeUI;

internal static class LowCostOptimizationTests
{
    internal static void Run()
    {
        CheckImageSnapshotLoading();
        CheckImageTextureDoesNotRootSource();
        CheckRamMonitorHistoryBufferReuse();
        CheckFullInvalidationControlsUseDefaultPath();
        CheckAnimationInvalidationSemantics();
        CheckFrameLoopInvalidationCanBeSuppressed();
    }

    private static void CheckImageSnapshotLoading()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lakeui-image-{Guid.NewGuid():N}.png");
        using (var source = new Bitmap(32, 18, PixelFormat.Format32bppArgb))
        {
            source.SetPixel(8, 7, Color.FromArgb(255, 210, 40, 90));
            source.Save(path, ImageFormat.Png);
        }

        try
        {
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            try
            {
                D3D_ImagePreparation.LoadBitmapAsync(path, cancelled.Token).GetAwaiter().GetResult();
                throw new InvalidOperationException("预取消的图片加载必须取消。");
            }
            catch (OperationCanceledException) { }

            using var loaded = D3D_ImagePreparation.LoadBitmapAsync(path).GetAwaiter().GetResult();
            RenderingRegression.Require(loaded.Width == 32 && loaded.Height == 18,
                "图片快照必须保留源图尺寸。");
            RenderingRegression.Require(loaded.PixelFormat == PixelFormat.Format32bppPArgb,
                "图片快照必须使用 PArgb 格式。");
            RenderingRegression.Require(loaded.GetPixel(8, 7).ToArgb() == Color.FromArgb(255, 210, 40, 90).ToArgb(),
                "图片快照必须保留源像素。");
            File.Delete(path);
            RenderingRegression.Require(!File.Exists(path), "图片加载完成后必须关闭源文件流。");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }

        Console.WriteLine("image preparation: streaming snapshot, cancellation, and source release passed");
    }

    private static void CheckImageTextureDoesNotRootSource()
    {
        using var form = RenderingRegression.Host();
        using var control = new CachedImageControl { Size = new Size(48, 36) };
        form.Controls.Add(control);
        form.Show();
        var compositor = D3D_RenderCore.GetWindowCompositor(form);
        var bytesBefore = compositor.TextureCache.TotalGpuBytes;
        var source = CacheImageSource(control);
        try
        {
            var bytesAfterUpload = compositor.TextureCache.TotalGpuBytes;
            RenderingRegression.Require(bytesAfterUpload > bytesBefore,
                "弱引用验证必须先实际创建并保留 GPU 图片纹理。");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            RenderingRegression.Require(!source.TryGetTarget(out _),
                "GPU 图片纹理缓存不得强引用调用方的 CPU Image。");
            RenderingRegression.Require(compositor.TextureCache.TotalGpuBytes == bytesAfterUpload,
                "回收 CPU Image 时 GPU 纹理仍应留在预算缓存中。");
        }
        finally
        {
            compositor.ImageCache.Invalidate();
        }

        Console.WriteLine("image cache: source Image collectible while GPU texture remains cached passed");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Image> CacheImageSource(CachedImageControl control)
    {
        var image = new Bitmap(24, 20);
        image.SetPixel(6, 8, Color.FromArgb(255, 170, 45, 120));
        var weak = new WeakReference<Image>(image);
        control.ImageSource = image;
        D3D_V5Presentation.RequestRender(control);
        control.ImageSource = null;
        return weak;
    }

    private static void CheckAnimationInvalidationSemantics()
    {
        using var form = RenderingRegression.Host();
        using var owner = new Control { Size = new Size(80, 50) };
        form.Controls.Add(owner);
        form.Show();

        using var helper = new D3D_AnimationHelper(owner);
        var before = D3D_AnimationHelper.GetThreadSchedulerSnapshot();
        var scheduler = typeof(D3D_AnimationHelper)
            .GetField("_threadScheduler", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        var syncOwnerField = scheduler.GetType().GetField("_syncOwner", BindingFlags.NonPublic | BindingFlags.Instance)!;
        helper.SetImmediate(0.25f);
        var full = D3D_AnimationHelper.GetThreadSchedulerSnapshot();
        RenderingRegression.Require(full.RequestedInvalidates == before.RequestedInvalidates + 1 &&
            full.FlushedInvalidates == before.FlushedInvalidates + 1,
            "默认动画失效必须发布一次完整更新。");
        RenderingRegression.Require(!ReferenceEquals(syncOwnerField.GetValue(scheduler), owner),
            "一次性失效不能让线程调度器长期持有窗体同步对象。");

        helper.DirtyProvider = (_, _, sink) => sink.SuppressInvalidate();
        helper.SetImmediate(0.5f);
        var suppressed = D3D_AnimationHelper.GetThreadSchedulerSnapshot();
        RenderingRegression.Require(suppressed.RequestedInvalidates == full.RequestedInvalidates &&
            suppressed.FlushedInvalidates == full.FlushedInvalidates,
            "显式抑制必须跳过失效发布。");

        helper.DirtyProvider = (_, _, sink) => sink.Add(new Rectangle(4, 5, 12, 9));
        helper.SetImmediate(0.75f);
        var partial = D3D_AnimationHelper.GetThreadSchedulerSnapshot();
        RenderingRegression.Require(partial.RequestedInvalidates == suppressed.RequestedInvalidates + 1 &&
            partial.FlushedInvalidates == suppressed.FlushedInvalidates + 1,
            "局部动画失效必须发布一次更新。");

        helper.DirtyProvider = null;
        for (var i = 0; i < 24; i++) helper.SetImmediate(i / 24f);
        var defaultStart = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 128; i++) helper.SetImmediate(i / 128f);
        var defaultBytes = GC.GetAllocatedBytesForCurrentThread() - defaultStart;
        RenderingRegression.Require(defaultBytes <= 1024,
            $"预热后 128 次默认失效不应恢复逐请求分配，实测 {defaultBytes} B。");

        helper.DirtyProvider = AddSmallDirtyRegion;
        for (var i = 0; i < 24; i++) helper.SetImmediate(i / 24f);
        var customStart = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 128; i++) helper.SetImmediate(i / 128f);
        var customBytes = GC.GetAllocatedBytesForCurrentThread() - customStart;
        RenderingRegression.Require(defaultBytes < customBytes,
            "默认全控件动画失效应比自定义脏区路径少分配。");

        Console.WriteLine($"animation invalidation: full/partial/suppressed passed; 128 requests allocated {defaultBytes} B default vs {customBytes} B custom");
    }

    private static void CheckFullInvalidationControlsUseDefaultPath()
    {
        using var progress = new ExcellentProgressBar();
        using var track = new ExcellentTrackBar();
        using var checkBox = new ModernCheckBox();
        using var dashboard = new RoundDashBoard();
        using var ring = new ProgressRing();
        using var dialogueList = new AgentDialogueList();

        RequireDefaultProvider(progress, "动画助手");
        RequireDefaultProvider(progress, "动画助手2");
        RequireDefaultProvider(track, "动画助手");
        RequireDefaultProvider(checkBox, "动画助手");
        RequireDefaultProvider(dashboard, "动画助手");
        RequireDefaultProvider(ring, "动画调度器");
        RequireDefaultProvider(dialogueList, "_progressAnimation");
        Console.WriteLine("full-invalidation controls: all six use the allocation-free default path");
    }

    private static void CheckRamMonitorHistoryBufferReuse()
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        using var form = RenderingRegression.Host();
        using var monitor = new RamMonitor { Size = new Size(240, 120), HistoryLength = 64 };
        form.Controls.Add(monitor);
        form.Show();
        ((System.Windows.Forms.Timer)typeof(RamMonitor).GetField("采样定时器", Private)!.GetValue(monitor)!).Stop();
        var history = (System.Collections.IList)typeof(RamMonitor).GetField("历史数据", Private)!.GetValue(monitor)!;
        var historyPointType = typeof(RamMonitor).GetNestedType("HistoryPoint", BindingFlags.NonPublic)!;
        for (var i = 0; i < 64; i++)
        {
            var point = Activator.CreateInstance(historyPointType)!;
            historyPointType.GetField("TotalBytes")!.SetValue(point, 1000UL);
            historyPointType.GetField("InUseBytes")!.SetValue(point, (ulong)(300 + i));
            historyPointType.GetField("ModifiedBytes")!.SetValue(point, 100UL);
            historyPointType.GetField("StandbyBytes")!.SetValue(point, 200UL);
            historyPointType.GetField("FreeBytes")!.SetValue(point, 400UL - (ulong)i);
            history.Add(point);
        }

        D3D_V5Presentation.RequestRender(monitor, monitor.ClientRectangle);
        var presenter = D3D_V5Presentation.GetPresenterForDiagnostics(monitor);
        RenderingRegression.Require(presenter is not null, "RAM monitor 必须创建 GPU 呈现器。");
        byte[] pixels = [];
        presenter!.SurfaceProbe = (context, bitmap, region) => pixels = RenderingRegression.Read(context, bitmap, region);
        void Render()
        {
            D3D_V5Presentation.RequestRender(monitor, monitor.ClientRectangle);
        }

        Render();
        var snapshotBuffer = Field<object>(monitor, "_historySnapshotBuffer");
        var inUseBuffer = Field<object>(monitor, "_inUseTopBuffer");
        var modifiedBuffer = Field<object>(monitor, "_modifiedTopBuffer");
        var standbyBuffer = Field<object>(monitor, "_standbyTopBuffer");
        RenderingRegression.Require(((Array)snapshotBuffer).Length >= 64 && ((Array)standbyBuffer).Length >= 64 && pixels.Length > 0,
            "RAM 缓冲测试必须实际绘制 64 个历史样本并读回 GPU 像素。");
        var firstPixels = pixels;
        Render();
        RenderingRegression.Require(ReferenceEquals(snapshotBuffer, Field<object>(monitor, "_historySnapshotBuffer")) &&
            ReferenceEquals(inUseBuffer, Field<object>(monitor, "_inUseTopBuffer")) &&
            ReferenceEquals(modifiedBuffer, Field<object>(monitor, "_modifiedTopBuffer")) &&
            ReferenceEquals(standbyBuffer, Field<object>(monitor, "_standbyTopBuffer")),
            "稳定 RAM 历史帧必须复用快照和曲线工作数组。");
        RenderingRegression.Require(firstPixels.SequenceEqual(pixels), "复用历史数据缓冲不能改变完整帧像素。");

        var snapshotMethod = typeof(RamMonitor).GetMethod("获取历史快照", Private)!;
        var snapshotProbe = Expression.Lambda<Action>(Expression.Block(
            Expression.Call(Expression.Constant(monitor), snapshotMethod),
            Expression.Empty())).Compile();
        for (var i = 0; i < 16; i++) snapshotProbe();
        var snapshotStart = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 128; i++) snapshotProbe();
        var snapshotBytes = GC.GetAllocatedBytesForCurrentThread() - snapshotStart;
        RenderingRegression.Require(snapshotBytes <= 1024,
            $"预热后 RAM 历史快照不应逐帧分配，128 次实测 {snapshotBytes} B。");

        monitor.EnableHoverReadout = true;
        var graphRect = (RectangleF)Field<object>(monitor, "_图表矩形");
        typeof(RamMonitor).GetField("_悬停有效", Private)!.SetValue(monitor, true);
        typeof(RamMonitor).GetField("_悬停X", Private)!.SetValue(monitor, graphRect.Left + graphRect.Width / 2);
        Render();
        RenderingRegression.Require(ReferenceEquals(snapshotBuffer, Field<object>(monitor, "_historySnapshotBuffer")),
            "图表与悬停读数必须共享同一帧的历史快照缓冲。");

        monitor.HistoryLength = 16;
        Render();
        RenderingRegression.Require(((Array)Field<object>(monitor, "_historySnapshotBuffer")).Length <= 16 &&
            ((Array)Field<object>(monitor, "_inUseTopBuffer")).Length <= 16 &&
            ((Array)Field<object>(monitor, "_modifiedTopBuffer")).Length <= 16 &&
            ((Array)Field<object>(monitor, "_standbyTopBuffer")).Length <= 16,
            "缩短历史长度必须及时收缩 CPU 快照与曲线工作区。");
        var reducedPixels = pixels;
        foreach (var name in new[] { "_historySnapshotBuffer", "_inUseTopBuffer", "_modifiedTopBuffer", "_standbyTopBuffer" })
        {
            var field = typeof(RamMonitor).GetField(name, Private)!;
            field.SetValue(monitor, Array.CreateInstance(field.FieldType.GetElementType()!, 0));
        }
        typeof(RamMonitor).GetField("_historySnapshotVersion", Private)!.SetValue(monitor, long.MinValue);
        Render();
        RenderingRegression.Require(reducedPixels.SequenceEqual(pixels),
            "缩短历史后的复用缓冲输出必须与重新创建工作区的完整帧一致。");
        monitor.Reset();
        RenderingRegression.Require(((Array)Field<object>(monitor, "_historySnapshotBuffer")).Length == 0 &&
            ((Array)Field<object>(monitor, "_inUseTopBuffer")).Length == 0 &&
            ((Array)Field<object>(monitor, "_modifiedTopBuffer")).Length == 0 &&
            ((Array)Field<object>(monitor, "_standbyTopBuffer")).Length == 0 &&
            (int)history.GetType().GetProperty("Capacity")!.GetValue(history)! == 0,
            "清空 RAM 历史必须释放 CPU 快照、曲线工作区和列表容量。");
        presenter.SurfaceProbe = null;
        Console.WriteLine($"RAM monitor: 128 history snapshots allocated {snapshotBytes} B; curve buffers reused, pixel parity and shrink cleanup passed");
    }

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void RequireDefaultProvider(Control control, string helperField)
    {
        var helper = control.GetType().GetField(helperField, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(control)!;
        var provider = helper.GetType().GetProperty("DirtyProvider")!.GetValue(helper);
        RenderingRegression.Require(provider is null,
            $"{control.GetType().Name} 的全控件失效动画应使用默认路径。");
    }

    private static void AddSmallDirtyRegion(D3D_AnimationHelper helper, Control owner, D3D_AnimationHelper.InvalidateRegionSink sink) =>
        sink.Add(new Rectangle(4, 5, 12, 9));

    private static void CheckFrameLoopInvalidationCanBeSuppressed()
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        using var form = RenderingRegression.Host();
        using var owner = new Control { Size = new Size(60, 40) };
        form.Controls.Add(owner);
        form.Show();
        using var helper = new D3D_AnimationHelper(owner) { FrameLoopRequestsInvalidate = false };
        var invoked = false;
        typeof(D3D_AnimationHelper).GetField("_frameLoopHandler", Private)!
            .SetValue(helper, new EventHandler((_, _) => invoked = true));
        typeof(D3D_AnimationHelper).GetField("_frameLoopRunning", Private)!.SetValue(helper, true);
        var before = D3D_AnimationHelper.GetThreadSchedulerSnapshot();
        helper.Tick(System.Diagnostics.Stopwatch.GetTimestamp());
        var after = D3D_AnimationHelper.GetThreadSchedulerSnapshot();
        RenderingRegression.Require(invoked && after.RequestedInvalidates == before.RequestedInvalidates,
            "只更新窗口属性的帧回调必须可以跳过控件失效。");
        Console.WriteLine("animation frame loop: property-only callback skipped redundant invalidation");
    }

    private sealed class CachedImageControl : Control, D3D_IGpuRenderable, V5_IGpuPresentationSource
    {
        internal Image? ImageSource;

        public void RenderGpu(D3D_PaintContext context)
        {
            if (ImageSource != null) context.DrawImage(ImageSource, ClientRectangle);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }
    }
}
