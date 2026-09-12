using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using LakeUI;

static partial class Program
{
    private static void VerifyGeometryBurstCoalesces()
    {
        using var form = new Form { ClientSize = new Size(500, 300), ShowInTaskbar = false };
        using var control = new CountingGpuControl { Bounds = new Rectangle(10, 10, 100, 80) };
        form.Controls.Add(control);
        form.Show();
        Application.DoEvents();
        var before = control.RenderCount;
        for (var i = 0; i < 20; i++)
        {
            control.Location = new Point(20 + i, 25 + i);
            control.Size = new Size(120 + i, 90 + i);
        }
        Assert(control.RenderCount == before, "Geometry event bursts must not synchronously render intermediate layouts.");
        Assert(D3D_ControlSurfaceRegistry.IsDirty(control), "Coalescing geometry must retain surface/background invalidation.");
        D3D_V5Presentation.FlushPendingFrame();
        Assert(control.RenderCount == before + 1 && D3D_ControlSurfaceRegistry.HasCurrentSurface(control),
            "The final geometry must be rendered once by the ordered batch.");
        form.Close();
    }

    private static void VerifyRefreshTransactions()
    {
        using var form = new Form { ClientSize = new Size(320, 220), ShowInTaskbar = false };
        using var parent = new CountingGpuControl { Name = "parent", Bounds = new Rectangle(0, 0, 280, 180) };
        using var child = new CountingGpuControl { Name = "child", Bounds = new Rectangle(5, 5, 120, 80) };
        parent.Controls.Add(child);
        form.Controls.Add(parent);
        form.Show();
        Application.DoEvents();
        var order = new List<string>();
        parent.DuringRender = _ => order.Add("parent");
        child.DuringRender = _ => order.Add("child");
        D3D_PaintBridge.V5ProbeEnabled = true;
        D3D_PaintBridge.ResetV5Probe();
        using (D3D_PaintBridge.BeginRenderUpdate(form))
        {
            using (D3D_PaintBridge.BeginRenderUpdate(parent))
            {
                for (var i = 0; i < 8; i++)
                {
                    D3D_V5Presentation.RequestRender(child);
                    OuterToInnerRefreshScheduler.RequestFull(parent, true, true);
                }
                // Even an unexpected nested message pump must not present intermediate state.
                Application.DoEvents();
                Assert(order.Count == 0, "A live render transaction must defer Paint and explicit requests.");
            }
            Assert(order.Count == 0, "Nested scopes must not commit their outer transaction.");
        }
        Assert(order.SequenceEqual(new[] { "parent", "child" }),
            "The transaction must render each stable surface once, parent before child: " + string.Join(",", order));
        var timings = D3D_PaintBridge.GetV5RefreshTimings();
        var renders = timings.Where(t => t.Stage == "RenderGpu").ToArray();
        var presents = timings.Where(t => t.Stage == "Present").ToArray();
        Assert(renders.Length == 2 && presents.Length > 0 &&
               renders.Max(t => t.LastEndMilliseconds) <= presents.Min(t => t.FirstStartMilliseconds),
            "All surfaces must be prepared before the batch starts presenting.");
        var count = child.RenderCount;
        D3D_V5Presentation.RequestRenderBatched(child, markSurfaceDirty: false);
        D3D_V5Presentation.ResumeAfterRenderUpdate();
        Assert(child.RenderCount == count, "A presentation-only retry must reuse a clean surface.");

        var queueRetry = typeof(D3D_V5Presentation).GetMethod("排队重试", BindingFlags.Static | BindingFlags.NonPublic)!;
        var retryTimers = (System.Collections.IDictionary)typeof(D3D_V5Presentation)
            .GetField("_retryTimers", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        queueRetry.Invoke(null, new object[] { child, true });
        Assert(((System.Windows.Forms.Timer)retryTimers[child]!).Interval == 16,
            "Normal frame backpressure must not wait 250 ms.");
        queueRetry.Invoke(null, new object[] { child, false });
        Assert(((System.Windows.Forms.Timer)retryTimers[child]!).Interval == 250,
            "Device failure must keep its bounded recovery backoff.");
        parent.DuringRender = null;
        child.DuringRender = null;
        try
        {
            using var scope = D3D_PaintBridge.BeginRenderUpdate(form);
            throw new InvalidOperationException("test");
        }
        catch (InvalidOperationException) { }
        Assert(!D3D_RenderUpdate.IsActive && !D3D_RenderUpdate.IsCommitting,
            "Exception unwinding must release the transaction.");
        D3D_PaintBridge.V5ProbeEnabled = false;
    }

    private static void VerifyLazyPages()
    {
        using var form = new Form { ClientSize = new Size(360, 240), ShowInTaskbar = false };
        using var tabs = new ModernTabListControl { Dock = DockStyle.Fill };
        form.Controls.Add(tabs);
        var calls = new int[2];
        var coveredRenders = 0;
        var thread = Environment.CurrentManagedThreadId;
        for (var i = 0; i < 2; i++)
        {
            var index = i;
            tabs.Items.Add(new ModernTabListControl.ModernTabPage("Page " + i)
            {
                BoundControlFactory = () =>
                {
                    Assert(Environment.CurrentManagedThreadId == thread, "Page factories must run on the UI thread.");
                    calls[index]++;
                    var panel = new ModernPanel { Name = "page" + index };
                    if (index == 1)
                    {
                        var content = new CountingGpuControl { Bounds = new Rectangle(4, 4, 100, 70) };
                        content.DuringRender = _ =>
                        {
                            var previous = tabs.Items[0].BoundControl;
                            if (previous is { Visible: true } && previous.Parent == panel.Parent &&
                                panel.Parent!.Controls.GetChildIndex(previous) < panel.Parent.Controls.GetChildIndex(panel))
                                coveredRenders++;
                        };
                        panel.Controls.Add(content);
                    }
                    return panel;
                }
            });
        }
        form.Show();
        Application.DoEvents();
        Assert(calls[1] == 0, "Unselected pages must not be constructed.");
        tabs.SelectedIndex = 0;
        tabs.SelectedIndex = 1;
        var page = tabs.Items[1].BoundControl;
        Assert(calls[1] == 1 && page is { Visible: true }, "Selection must construct and display its page.");
        Assert(coveredRenders > 0 && !tabs.Items[0].BoundControl.Visible,
            "The old page must cover new GPU rendering, then hide only after the transaction commits.");
        tabs.SelectedIndex = 0;
        tabs.SelectedIndex = 1;
        Assert(calls[1] == 1 && ReferenceEquals(page, tabs.Items[1].BoundControl), "Revisiting a page must reuse it.");
        page!.Dispose();
        tabs.SelectedIndex = 0;
        tabs.SelectedIndex = 1;
        Assert(calls[1] == 2 && !tabs.Items[1].BoundControl.IsDisposed, "A disposed factory page must be recreated.");
    }

    private static void VerifyTextMeasurementReuse()
    {
        using var font = new Font("Segoe UI", 12);
        D3D_TextMeasurementCache.Clear();
        D3D_PaintBridge.V5ProbeEnabled = true;
        D3D_PaintBridge.ResetV5Probe();
        var flags = TextFormatFlags.WordBreak;
        var size = new Size(90, 400);
        var first = D3D_TextInterop.MeasureText("One two three four five", font, size, flags, 1);
        var again = D3D_TextInterop.MeasureText("One two three four five", font, size, flags, 1);
        Assert(first == again, "Cached text metrics must match the original measurement.");
        Assert(D3D_PaintBridge.GetV5RefreshTimings().Single(t => t.Stage == "TextMeasurement").Count == 1,
            "Identical text metrics must not construct another TextLayout.");
        D3D_TextInterop.MeasureText("One two three four five", font, new Size(180, 400), flags, 1);
        D3D_TextInterop.MeasureText("One two three four five", font, size, flags, 1.5f);
        Assert(D3D_PaintBridge.GetV5RefreshTimings().Single(t => t.Stage == "TextMeasurement").Count == 3,
            "Width and DPI changes must not reuse stale metrics.");
        D3D_PaintBridge.InvalidateTextFormatCache(null!);
        D3D_TextInterop.MeasureText("One two three four five", font, size, flags, 1);
        Assert(D3D_PaintBridge.GetV5RefreshTimings().Single(t => t.Stage == "TextMeasurement").Count == 4,
            "Explicit text invalidation must clear the metrics cache.");
        D3D_CpuCache.ReleaseAll();
        D3D_TextInterop.MeasureText("One two three four five", font, size, flags, 1);
        Assert(D3D_PaintBridge.GetV5RefreshTimings().Single(t => t.Stage == "TextMeasurement").Count == 5,
            "The metrics cache must participate in global CPU cache cleanup.");
        D3D_PaintBridge.V5ProbeEnabled = false;
    }

    private static void VerifyBackgroundImagePreparation()
    {
        var path = Path.Combine(Path.GetTempPath(), "lakeui-decode-" + Guid.NewGuid() + ".png");
        try
        {
            using (var source = new Bitmap(8, 6, PixelFormat.Format32bppArgb))
            {
                source.SetPixel(2, 3, Color.FromArgb(128, 100, 50, 20));
                source.Save(path, ImageFormat.Png);
            }
            var tasks = Enumerable.Range(0, 4).Select(_ => D3D_ImagePreparation.LoadBitmapAsync(path)).ToArray();
            var images = Task.WhenAll(tasks).GetAwaiter().GetResult();
            try
            {
                File.Delete(path);
                foreach (var image in images)
                {
                    Assert(image.Size == new Size(8, 6) && image.PixelFormat == PixelFormat.Format32bppPArgb,
                        "Background decode must return independent PArgb pixels without holding the file open.");
                    var pixel = image.GetPixel(2, 3);
                    Assert(pixel.A == 128 && Math.Abs(pixel.R - 100) <= 2,
                        "Background conversion must preserve alpha and color.");
                }
            }
            finally { foreach (var image in images) image.Dispose(); }
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            try
            {
                using var ignored = D3D_ImagePreparation.LoadBitmapAsync(path, cancelled.Token).GetAwaiter().GetResult();
                Assert(false, "Cancelled image loads must not decode.");
            }
            catch (OperationCanceledException) { }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static void ProbeRefreshBatch()
    {
        D3D_PaintBridge.V5ProbeEnabled = true;
        foreach (var transaction in new[] { false, true, false, true })
        {
            using var form = new Form { ClientSize = new Size(840, 480), ShowInTaskbar = false };
            var controls = Enumerable.Range(0, 40).Select(i => new ModernButton
            {
                Name = "button" + i, Text = "Control " + i,
                Bounds = new Rectangle(i % 8 * 104, i / 8 * 90, 100, 80)
            }).ToArray();
            form.Controls.AddRange(controls);
            form.Show();
            Application.DoEvents();
            D3D_PaintBridge.CleanupD2DResources(D3DCacheCleanupLevel.ReleaseRenderTargets, form);
            D3D_PaintBridge.ResetV5Probe();
            var watch = Stopwatch.StartNew();
            if (transaction)
            {
                using var scope = D3D_PaintBridge.BeginRenderUpdate(form);
                foreach (var control in controls) D3D_V5Presentation.RequestRender(control);
            }
            else
            {
                foreach (var control in controls) D3D_V5Presentation.RequestRender(control);
            }
            var synchronous = watch.Elapsed.TotalMilliseconds;
            PrintRefreshProbe(transaction ? "batched" : "immediate", synchronous);
        }
        D3D_PaintBridge.V5ProbeEnabled = false;
    }

    private static void PrintRefreshProbe(string label, double elapsed)
    {
        var timings = D3D_PaintBridge.GetV5RefreshTimings();
        var presents = timings.Where(t => t.Stage == "Present").ToArray();
        var spread = presents.Length == 0 ? 0 : presents.Max(t => t.LastEndMilliseconds) - presents.Min(t => t.FirstStartMilliseconds);
        var probe = D3D_PaintBridge.GetV5ProbeSnapshot();
        Console.WriteLine($"{label}: sync={elapsed:F2}ms submitSpan={spread:F2}ms submits={probe.Render.V5SubmittedFrames} latencySkips={probe.Render.V5FrameLatencySkips}");
        foreach (var stage in timings.GroupBy(t => t.Stage))
            Console.WriteLine($"  {stage.Key}: count={stage.Sum(t => t.Count)} total={stage.Sum(t => t.TotalMilliseconds):F2}ms peak={stage.Max(t => t.PeakMilliseconds):F2}ms");
    }

    private static void ProbeDemoRefresh(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            var candidate = Path.Combine(directory, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
        };
        var assembly = Assembly.LoadFrom(Path.GetFullPath(path));
        D3D_PaintBridge.V5ProbeEnabled = true;
        D3D_PaintBridge.ResetV5Probe();
        var watch = Stopwatch.StartNew();
        using var form = (Form)Activator.CreateInstance(assembly.GetTypes().Single(t => t.Name == "Form1"))!;
        var chrome = form.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(p => p.PropertyType == typeof(ThisIsYourWindow)).Select(p => p.GetValue(form)).OfType<ThisIsYourWindow>().FirstOrDefault();
        if (chrome != null) chrome.ShowAnimation = ThisIsYourWindow.WindowShowAnimationMode.None;
        form.Show();
        Application.DoEvents();
        PrintRefreshProbe("demo startup", watch.Elapsed.TotalMilliseconds);
        var tabs = form.Controls.OfType<ModernTabListControl>().Single();
        Assert(tabs.Items.Count(p => p.BoundControl != null) == 1, "DEMO startup must only construct its first page.");
        foreach (var index in new[] { 5, 6, 17, 40, 5 })
        {
            D3D_PaintBridge.ResetV5Probe();
            watch.Restart();
            tabs.SelectedIndex = index;
            Application.DoEvents();
            PrintRefreshProbe("demo page " + index, watch.Elapsed.TotalMilliseconds);
            Assert(tabs.Items[index].BoundControl is { Visible: true }, "Selected DEMO page must be visible.");
        }
        D3D_PaintBridge.V5ProbeEnabled = false;
    }

}
