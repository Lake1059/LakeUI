using System.Diagnostics;
using System.Drawing;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Forms;
using LakeUI;
using Vortice.Direct2D1;
using Vortice.DXGI;
using SizeI = Vortice.Mathematics.SizeI;

internal static class RenderingRegression
{
    internal static void Run()
    {
        foreach (var (scale, fp16, output) in new[] { (1, false, false), (2, false, false), (4, false, false),
            (1, true, false), (2, true, false), (1, true, true), (2, true, true) })
            CheckPresentation(scale, fp16, output);
        CheckBackgroundDependencies();
        CheckPanelLifecycle();
        CheckPonderCloseButtonHoverPartialUpdate();
        WindowInitializationTests.Run();
    }

    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static Form Host() => new()
    {
        ClientSize = new Size(320, 220), FormBorderStyle = FormBorderStyle.None,
        ShowInTaskbar = false, ShowIcon = false, StartPosition = FormStartPosition.Manual,
        Location = new Point(-32000, -32000), Opacity = 0
    };

    private static void CheckPresentation(int scale, bool fp16, bool floatOutput)
    {
        using var form = Host();
        using var control = new RenderControl { Size = new Size(128, 96), SampleScale = scale, FloatContent = fp16, FloatOutput = floatOutput };
        form.Controls.Add(control);
        form.Show();
        using var content = new D3D_ControlSurface(control, D3D_RenderCore.DeviceManager);
        using var presenter = new D3D_HwndCompositionPresenter(control, D3D_RenderCore.DeviceManager);
        var bounds = control.ClientRectangle;
        Rectangle actualRegion = default;
        byte[] pixels = [];
        presenter.SurfaceProbe = (context, bitmap, region) =>
        {
            actualRegion = region;
            pixels = Read(context, bitmap, region);
        };

        Require(content.Render(control, new Rectangle(30, 20, 12, 10), null), "首帧内容准备失败。");
        Require(content.LastRenderedDirtyRegion == bounds, "首个内容表面必须完整初始化。");
        Require(presenter.Present(content, 0), "首帧发布失败。");
        Require(actualRegion.Size == bounds.Size, "首个 Composition surface 必须完整初始化。");

        var patch = new Rectangle(33, 27, 19, 13);
        control.Patches.Add((patch, Color.FromArgb(180, 210, 30, 90)));
        Require(content.Render(control, patch, null) && presenter.Present(content, 0), "局部更新失败。");
        Require(actualRegion.Width < bounds.Width && actualRegion.Height < bounds.Height, "必须实际进入局部 DComp 更新。");
        var dirty = content.LastRenderedDirtyRegion;
        dirty.Inflate(2, 2);
        dirty.Intersect(bounds);
        Require(actualRegion.Size == dirty.Size, "滤波扩展后的更新范围不匹配。");
        var partialPixels = pixels;

        Require(content.Render(control, bounds, null) && presenter.Present(content, 0), "完整参考帧失败。");
        var reference = Crop(pixels, bounds.Width, dirty, floatOutput ? 8 : 4);
        var difference = Array.FindIndex(partialPixels, index => index >= reference.Length || partialPixels[index] != reference[index]);
        Require(difference < 0, $"局部与整面像素不一致：SSAA={scale}, FP16={fp16}, dirty={dirty}, update={actualRegion}, byte={difference}, actual={(difference >= 0 && difference < partialPixels.Length ? partialPixels[difference] : -1)}, expected={(difference >= 0 && difference < reference.Length ? reference[difference] : -1)}。");

        control.Coverage = false;
        content.Render(control, patch, null);
        Require(content.LastRenderedDirtyRegion == bounds && presenter.Present(content, 0) && actualRegion.Size == bounds.Size,
            "未声明局部覆盖时必须沿用完整绘制。");
        control.Coverage = true;
        control.FailNextDraw = true;
        try { content.Render(control, patch, null); throw new Exception("测试绘制故障没有发生。"); }
        catch (TestDrawException) { }
        content.Render(control, patch, null);
        Require(content.LastRenderedDirtyRegion == bounds && presenter.Present(content, 0), "绘制中断后的内容必须完整重建。");

        control.Patches.Add((new Rectangle(8, 8, 8, 8), Color.Green));
        content.Render(control, new Rectangle(8, 8, 8, 8), null);
        control.Patches.Add((new Rectangle(80, 60, 8, 8), Color.Blue));
        content.Render(control, new Rectangle(80, 60, 8, 8), null);
        Require(presenter.Present(content, 0) && actualRegion.Size == bounds.Size, "跳过内容修订必须完整补交。");

        presenter.HandleDeviceLost();
        Require(presenter.Present(content, 0) && actualRegion.Size == bounds.Size, "呈现器重建必须完整补交。");
        content.ReleaseSurfaceResources();
        content.Render(control, patch, null);
        Require(content.LastRenderedDirtyRegion == bounds && presenter.Present(content, 0), "内容重建必须完整补交。");
        control.Size = new Size(140, 100);
        content.Render(control, patch, null);
        Require(presenter.Present(content, 0) && actualRegion.Size == control.Size, "尺寸变化必须完整补交。");
        control.RebuildHandle();
        content.Render(control, patch, null);
        Require(presenter.Present(content, 0) && actualRegion.Size == control.Size, "句柄重建必须完整补交。");
        D3D_RenderCore.DeviceManager.InvalidateDevice();
        content.Render(control, patch, null);
        Require(presenter.Present(content, 0) && actualRegion.Size == control.Size, "设备代次恢复失败。");

        long requestedPixels = 0;
        presenter.SurfaceProbe = (_, _, region) => requestedPixels += (long)region.Width * region.Height;
        var watch = Stopwatch.StartNew();
        const int count = 60;
        for (var i = 0; i < count; i++)
        {
            Require(content.Render(control, patch, null) && presenter.Present(content, 0), "连续局部发布失败。");
        }
        watch.Stop();
        var fullPixels = (long)control.Width * control.Height * count;
        Require(requestedPixels < fullPixels / 4, "局部呈现必须实际减少更新像素。");
        Console.WriteLine($"render SSAA={scale} FP16={fp16} floatOutput={floatOutput}: {count} frames, updatePixels={requestedPixels}/{fullPixels}, CPU={watch.Elapsed.TotalMilliseconds:F1}ms; pixel/lifecycle checks passed");
        presenter.SurfaceProbe = null;
    }

    internal static byte[] Read(ID2D1DeviceContext context, ID2D1Bitmap1 bitmap, Rectangle region)
    {
        var properties = new BitmapProperties1(bitmap.PixelFormat, 96, 96, BitmapOptions.CpuRead | BitmapOptions.CannotDraw);
        using var readback = context.CreateBitmap(new SizeI(region.Width, region.Height), IntPtr.Zero, 0, properties);
        readback.CopyFromBitmap(Point.Empty, bitmap, region).CheckError();
        var mapped = readback.Map(MapOptions.Read);
        try
        {
            var pixelBytes = bitmap.PixelFormat.Format == Format.R16G16B16A16_Float ? 8 : 4;
            var rowBytes = region.Width * pixelBytes;
            var data = new byte[rowBytes * region.Height];
            for (var y = 0; y < region.Height; y++)
                Marshal.Copy(mapped.Bits + checked((int)mapped.Pitch * y), data, y * rowBytes, rowBytes);
            return data;
        }
        finally { readback.Unmap(); }
    }

    internal static byte[] Crop(byte[] image, int width, Rectangle region, int pixelBytes = 4)
    {
        var result = new byte[region.Width * region.Height * pixelBytes];
        for (var y = 0; y < region.Height; y++)
            Array.Copy(image, ((region.Y + y) * width + region.X) * pixelBytes, result,
                y * region.Width * pixelBytes, region.Width * pixelBytes);
        return result;
    }

    private static void CheckBackgroundDependencies()
    {
        using var first = Host();
        using var second = Host();
        using var source = new RenderControl { Size = new Size(128, 96) };
        using var consumer = new RenderControl { Location = new Point(10, 8), Size = new Size(100, 70), Source = source };
        using var leaf = new RenderControl { Location = consumer.Location, Size = consumer.Size, Source = consumer };
        first.Controls.Add(source);
        first.Controls.Add(leaf);
        second.Controls.Add(consumer);
        first.Show(); second.Show();
        D3D_V5Presentation.RequestRender(source, default);
        D3D_V5Presentation.RequestRender(consumer, default);
        D3D_V5Presentation.RequestRender(leaf, default);
        D3D_V5Presentation.FlushPendingFrame(false);
        CheckStableDependencyCleanupAllocations(leaf);
        var presenter = D3D_V5Presentation.GetPresenterForDiagnostics(leaf);
        Rectangle updated = default;
        byte[] pixels = [];
        presenter.SurfaceProbe = (context, bitmap, region) => { updated = region; pixels = Read(context, bitmap, region); };
        var patch = new Rectangle(35, 28, 10, 10);
        source.Patches.Add((patch, Color.FromArgb(255, 230, 40, 90)));
        D3D_ControlSurfaceRegistry.MarkDirty(source, patch, true);
        var before = D3D_ControlSurfaceRegistry.GetRevision(leaf);
        D3D_V5Presentation.FlushPendingFrame(false);
        Require(D3D_ControlSurfaceRegistry.GetRevision(leaf) > before, "多级跨窗背景依赖必须传播到末级。");
        Require(updated.Width > 0 && updated.Width < leaf.Width && updated.Height < leaf.Height,
            "末级背景消费者必须实际局部更新。");
        var point = new Point(patch.X - consumer.Left, patch.Y - consumer.Top);
        var mapped = new Rectangle(point.X - 4, point.Y - 4, updated.Width, updated.Height);
        var partial = pixels;
        D3D_V5Presentation.RequestRender(leaf, default);
        Require(partial.SequenceEqual(Crop(pixels, leaf.Width, mapped)), "跨窗多级背景局部与整面结果不同。");
        var center = ((point.Y + 5) * leaf.Width + point.X + 5) * 4;
        Require(pixels[center] == 90 && pixels[center + 1] == 40 && pixels[center + 2] == 230,
            "末级消费者必须实际采样到来源的新颜色。");

        CheckDependencyInvalidationAllocations(source, patch);
        OuterToInnerRefreshScheduler.FlushPendingRequests();
        D3D_V5Presentation.FlushPendingFrame(false);

        var previous = D3D_ControlSurfaceRegistry.GetRevision(consumer);
        second.Location = new Point(-31990, -31990);
        D3D_V5Presentation.FlushPendingFrame(false);
        Require(D3D_ControlSurfaceRegistry.GetRevision(consumer) > previous, "窗口移动必须使背景坐标失效。");
        presenter.SurfaceProbe = null;
        Console.WriteLine("cross-window/multi-level background propagation and pixel comparison passed");
    }

    private static void CheckDependencyInvalidationAllocations(Control source, Rectangle dirty)
    {
        for (var i = 0; i < 16; i++) D3D_ControlSurfaceRegistry.MarkDirty(source, dirty, true);
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 128; i++) D3D_ControlSurfaceRegistry.MarkDirty(source, dirty, true);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Require(allocated <= 60000,
            $"来源失效扇出应省去每层消费者快照，128 次实测 {allocated} B；快照基线约 65,536 B。");
        Console.WriteLine($"dependency invalidation fan-out: 128 requests allocated {allocated} B");
    }

    private static void CheckStableDependencyCleanupAllocations(Control consumer)
    {
        const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        var registry = typeof(D3D_ControlSurfaceRegistry);
        var entries = (System.Collections.IDictionary)registry.GetField("_entries", PrivateStatic)!.GetValue(null)!;
        var entry = entries[consumer]!;
        var seenSources = entry.GetType()
            .GetField("SeenSources", BindingFlags.Instance | BindingFlags.Public)!.GetValue(entry)!;
        var seenSourceCount = (int)seenSources.GetType().GetProperty("Count")!.GetValue(seenSources)!;
        Require(seenSourceCount > 0, "稳定依赖分配探针必须先建立实际背景依赖。");

        var cleanup = registry.GetMethod("清理未使用依赖", PrivateStatic)!;
        var call = Expression.Lambda<Action>(Expression.Call(
            cleanup,
            Expression.Constant(consumer),
            Expression.Constant(entry, cleanup.GetParameters()[1].ParameterType))).Compile();
        for (var i = 0; i < 16; i++) call();
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 128; i++) call();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Require(allocated <= 1024,
            $"128 次稳定依赖拓扑检查不应分配临时过期数组，实测 {allocated} B。");
        Console.WriteLine($"stable dependency cleanup: 128 topology hits allocated {allocated} B");
    }

    private static void CheckPonderCloseButtonHoverPartialUpdate()
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        using var form = Host();
        using var ponder = new Ponder { AnimationEnabled = false };
        ponder.Items.Add(new Ponder.PonderItem { HighlightControl = form, Text = "Hover update" });
        form.Show();
        ponder.Start(form);
        var overlay = (Form)typeof(Ponder).GetField("_overlay", Private)!.GetValue(ponder)!;
        OuterToInnerRefreshScheduler.FlushPendingRequests();
        D3D_V5Presentation.RequestRender(overlay, default);
        D3D_V5Presentation.FlushPendingFrame(false);
        var presenter = D3D_V5Presentation.GetPresenterForDiagnostics(overlay);
        Require(presenter is not null, "Ponder 遮罩必须创建 GPU 呈现器。");

        var buttons = (System.Collections.IDictionary)overlay.GetType().GetField("_buttons", Private)!.GetValue(overlay)!;
        var close = (RectangleF)buttons["close"]!;
        Rectangle updated = default;
        Rectangle partialRegion = default;
        byte[] pixels = [];
        presenter!.SurfaceProbe = (context, bitmap, region) =>
        {
            updated = region;
            pixels = Read(context, bitmap, region);
        };
        try
        {
            var point = Point.Round(new PointF(close.Left + close.Width / 2, close.Top + close.Height / 2));
            var mouseMove = overlay.GetType().GetMethod("OnMouseMove", Private)!;
            mouseMove.Invoke(overlay, [new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, 0)]);
            OuterToInnerRefreshScheduler.FlushPendingRequests();
            D3D_V5Presentation.FlushPendingFrame(false);
            Require(updated.Width > 0 && (long)updated.Width * updated.Height <
                (long)overlay.ClientSize.Width * overlay.ClientSize.Height / 4,
                "Ponder close hover 必须局部更新而不重绘整窗。");
            partialRegion = updated;

            var surface = D3D_ControlSurfaceRegistry.RenderControl(overlay, (D3D_IGpuRenderable)overlay, default, null);
            var dirty = surface!.LastRenderedDirtyRegion;
            dirty.Inflate(2, 2);
            dirty.Intersect(overlay.ClientRectangle);
            Require(updated.Size == dirty.Size, "Ponder hover 呈现范围必须与局部内容脏区一致。");
            var partial = pixels;

            D3D_V5Presentation.RequestRender(overlay, overlay.ClientRectangle);
            var referenceRegion = updated;
            var reference = pixels;
            Require(referenceRegion.Size == overlay.ClientSize, "完整参考帧必须更新整个 Ponder 遮罩。");
            Require(partial.SequenceEqual(Crop(reference, overlay.ClientSize.Width, dirty)),
                "Ponder 按钮局部 hover 绘制必须与完整帧一致。");
        }
        finally
        {
            presenter.SurfaceProbe = null;
        }

        Console.WriteLine($"Ponder close hover: updated {partialRegion.Width * partialRegion.Height}/{overlay.ClientSize.Width * overlay.ClientSize.Height} pixels with full-frame parity");
    }

    private static void CheckPanelLifecycle()
    {
        using var form = Host();
        using var panel = new ModernPanel { Size = new Size(160, 100) };
        using var child = new ModernCheckBox { Size = new Size(90, 30), Location = new Point(8, 8) };
        panel.Controls.Add(child);
        form.Controls.Add(panel);
        form.Show();
        D3D_V5Presentation.RequestRender(panel, default);
        Require(D3D_V5Presentation.GetPresenterForDiagnostics(panel) != null, "普通面板必须有呈现器。");
        panel.BackColor = panel.BackColor1 = panel.OverlayColor = Color.Transparent;
        panel.TransparentRenderMode = ModernPanel.TransparentRenderModeEnum.SkipWhenFullyTransparent;
        Require(D3D_V5Presentation.GetPresenterForDiagnostics(panel) == null, "无渲染模式必须移除旧呈现器。");
        D3D_V5Presentation.RequestRender(child, default);
        Require(D3D_V5Presentation.GetPresenterForDiagnostics(child) != null, "无渲染面板的子控件仍需呈现。");
        Require(D3D_ControlSurfaceRegistry.ResolveNearestGpuAncestor(child) == form, "背景祖先必须跳过无渲染面板。");
        panel.BackColor1 = Color.Black;
        D3D_V5Presentation.RequestRender(panel, default);
        Require(D3D_V5Presentation.GetPresenterForDiagnostics(panel) != null, "退出无渲染模式必须恢复呈现器。");
        Require(D3D_ControlSurfaceRegistry.ResolveNearestGpuAncestor(child) == panel, "背景祖先必须恢复面板。");
        Console.WriteLine("panel lifecycle/child presentation passed");
    }

    private sealed class RenderControl : Control, D3D_IGpuRenderable, V5_IGpuPresentationSource,
        D3D_IGpuDirtyRegionCoverage, D3D_ISuperSamplingSource, D3D_IFloatRenderingSource, D3D_IFloatPresentationSource,
        D3D_IBackgroundSourceProvider
    {
        internal int SampleScale = 1;
        internal bool FloatContent;
        internal bool FloatOutput;
        internal bool Coverage = true;
        internal bool FailNextDraw;
        internal Control? Source;
        internal readonly List<(Rectangle Region, Color Color)> Patches = [];
        public RenderControl() { BackColor = Color.Black; }
        public GlobalOptions.SuperSamplingScaleEnum SuperSamplingScale => (GlobalOptions.SuperSamplingScaleEnum)SampleScale;
        public bool UseFloatRendering => FloatContent;
        public bool UseFloatPresentation => FloatOutput;
        public bool CoversDirtyRegion(Rectangle region) => Coverage;
        public bool TryGetBackgroundSource(ref Control source) { source = Source!; return Source != null; }
        public void RenderGpu(D3D_PaintContext context)
        {
            if (FailNextDraw) { FailNextDraw = false; throw new TestDrawException(); }
            context.FillRectangle(ClientRectangle, Color.FromArgb(30, 60, 90));
            if (Source != null) context.DrawBackgroundSource(this, Source, ClientRectangle);
            foreach (var (region, color) in Patches) context.FillRectangle(region, color);
        }
        internal void RebuildHandle() => RecreateHandle();
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }
    }

    private sealed class TestDrawException : Exception;
}
