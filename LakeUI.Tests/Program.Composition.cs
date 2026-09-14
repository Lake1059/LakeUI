using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LakeUI;

static partial class Program
{
    private sealed class CompositionProbeControl : Control, D3D_IGpuRenderable,
        V5_IGpuPresentationSource, D3D_IBackgroundSourceProvider
    {
        public Control? Source;
        public Color LeftColor = Color.FromArgb(35, 110, 220);
        public Color RightColor = Color.FromArgb(230, 70, 80);
        public bool Stripe;
        public int Samples;
        public long SampledRevision;

        public bool TryGetBackgroundSource(ref Control source)
        {
            source = Source!;
            return source is not null;
        }

        public void RenderGpu(D3D_PaintContext context)
        {
            if (Source is null)
            {
                context.FillRectangle(new RectangleF(0, 0, Width / 2, Height), LeftColor);
                context.FillRectangle(new RectangleF(Width / 2, 0, Width - Width / 2, Height), RightColor);
            }
            else
            {
                Assert(D3D_ControlSurfaceRegistry.TryDrawBackground(this, Source, context, ClientRectangle),
                    "The nested control must sample its explicit GPU background.");
                SampledRevision = D3D_ControlSurfaceRegistry.GetRevision(Source);
            }
            if (Stripe) context.FillRectangle(new RectangleF(0, 0, Width, 25), Color.Lime);
        }

        public void RebuildHandle() => RecreateHandle();
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) => D3D_V5Presentation.Paint(this, this);
    }

    private static void VerifyCompositionCommitAccounting()
    {
        using var form = new Form { ClientSize = new Size(240, 160), ShowInTaskbar = false };
        using var outer = new CountingGpuControl { Bounds = new Rectangle(0, 0, 220, 140) };
        using var inner = new CountingGpuControl { Bounds = new Rectangle(10, 10, 100, 60) };
        outer.Controls.Add(inner);
        form.Controls.Add(outer);
        form.Show();
        Application.DoEvents();
        D3D_PaintBridge.V5ProbeEnabled = true;
        D3D_PaintBridge.ResetV5Probe();
        inner.DuringRender = _ => Assert(D3D_PaintBridge.GetV5ProbeSnapshot().SubmittedFrames == 0,
            "Prepared outer pixels must not be reported as published before the shared commit.");
        try
        {
            D3D_V5Presentation.RequestRenderBatched(inner);
            D3D_V5Presentation.RequestRenderBatched(outer);
            D3D_V5Presentation.FlushPendingFrame();
            var timings = D3D_PaintBridge.GetV5RefreshTimings();
            Assert(timings.Where(t => t.Stage == "CompositionCommit").Sum(t => t.Count) == 1,
                "A nested control batch must issue exactly one composition commit.");
            Assert(D3D_PaintBridge.GetV5ProbeSnapshot().SubmittedFrames == 2,
                "Both control revisions must be acknowledged after the shared commit succeeds.");
        }
        finally
        {
            inner.DuringRender = null;
            D3D_PaintBridge.V5ProbeEnabled = false;
            form.Close();
        }
    }

    private static Color ReadSurfaceColor(CompositionProbeControl control, int x, int y)
    {
        Assert(D3D_ControlSurfaceRegistry.HasCurrentSurface(control), "Readback must inspect a completed GPU surface.");
        var surface = D3D_ControlSurfaceRegistry.RenderControl(control, control);
        using var context = D3D_RenderCore.DeviceManager.CreateDeviceContext();
        var properties = new Vortice.Direct2D1.BitmapProperties1(surface.Bitmap.PixelFormat, 96, 96,
            Vortice.Direct2D1.BitmapOptions.CpuRead | Vortice.Direct2D1.BitmapOptions.CannotDraw);
        using var readback = context.CreateBitmap(surface.Bitmap.PixelSize, IntPtr.Zero, 0, properties);
        readback.CopyFromBitmap(surface.Bitmap);
        var mapped = readback.Map(Vortice.Direct2D1.MapOptions.Read);
        try
        {
            var offset = checked((int)(y * surface.SampleScale * mapped.Pitch + x * surface.SampleScale * 4));
            return Color.FromArgb(Marshal.ReadInt32(mapped.Bits, offset));
        }
        finally { readback.Unmap(); }
    }

    private static void AssertSurfaceColor(CompositionProbeControl control, int x, int y, Color expected, string label)
    {
        var actual = ReadSurfaceColor(control, x, y);
        Assert(Math.Abs(actual.R - expected.R) <= 8 && Math.Abs(actual.G - expected.G) <= 8 && Math.Abs(actual.B - expected.B) <= 8,
            $"{label}: expected {expected}, actual {actual} at ({x}, {y}).");
    }

    private static void ProbeCompositionPixels()
    {
        using var watchdog = new System.Threading.Timer(_ => Environment.FailFast("Composition pixel probe exceeded 15 seconds."), null, 15000, Timeout.Infinite);
        var hdr = GlobalOptions.HDR.Enabled;
        GlobalOptions.HDR.Enabled = false;
        using var form = new Form { ClientSize = new Size(640, 360), ShowInTaskbar = false };
        using var outer = new CompositionProbeControl { Name = "source", Bounds = new Rectangle(10, 10, 600, 320) };
        using var middle = new CompositionProbeControl { Name = "middle", Source = outer, Stripe = true, Bounds = new Rectangle(40, 40, 480, 220) };
        using var inner = new CompositionProbeControl { Name = "mapped", Source = outer, Bounds = new Rectangle(30, 5, 380, 150) };
        middle.Controls.Add(inner);
        outer.Controls.Add(middle);
        form.Controls.Add(outer);
        try
        {
            form.Show();
            Application.DoEvents();
            void RefreshAll()
            {
                using (D3D_PaintBridge.BeginRenderUpdate(form))
                {
                    D3D_V5Presentation.RequestRender(inner);
                    D3D_V5Presentation.RequestRender(middle);
                    D3D_V5Presentation.RequestRender(outer);
                }
            }
            void Verify(string phase)
            {
                RefreshAll();
                AssertSurfaceColor(outer, 5, 5, outer.LeftColor, phase + " outer");
                AssertSurfaceColor(middle, 5, 5, Color.Lime, phase + " middle stripe");
                AssertSurfaceColor(inner, 5, 5, outer.LeftColor, phase + " bypass immediate parent");
                AssertSurfaceColor(inner, inner.Width - 5, 5, outer.RightColor, phase + " mapped coordinates");
                Assert(inner.SampledRevision == D3D_ControlSurfaceRegistry.GetRevision(outer),
                    phase + " consumer must sample the current source revision.");
            }
            Verify("initial");
            outer.LeftColor = Color.FromArgb(170, 45, 190);
            outer.RightColor = Color.FromArgb(25, 190, 130);
            D3D_ControlSurfaceRegistry.MarkDirty(outer);
            D3D_V5Presentation.RequestRenderBatched(outer);
            D3D_V5Presentation.FlushPendingFrame();
            AssertSurfaceColor(inner, 5, 5, outer.LeftColor, "source-only invalidation");
            Verify("source changed");
            middle.Left += 12;
            inner.Width -= 20;
            Verify("geometry changed");
            middle.Hide();
            middle.Show();
            inner.RebuildHandle();
            Verify("handle recreated");
            D3D_PaintBridge.CleanupD2DResources(D3DCacheCleanupLevel.ReleaseEverything, form);
            Verify("device recreated");
            Console.WriteLine("Composition GPU readback passed: nested mapping, coordinates, visibility, handle/device recovery.");
        }
        finally
        {
            form.Close();
            GlobalOptions.HDR.Enabled = hdr;
        }
    }

    private static void ProbeMixedCompositionRates()
    {
        using var watchdog = new System.Threading.Timer(_ => Environment.FailFast("Mixed rate probe exceeded 12 seconds."), null, 12000, Timeout.Infinite);
        using var form = new Form { ClientSize = new Size(500, 140), ShowInTaskbar = false };
        var rates = new[] { 30, 60, 120 };
        var controls = rates.Select((rate, i) => new CompositionProbeControl { Name = $"rate{rate}", Bounds = new Rectangle(i * 160, 0, 150, 130) }).ToArray();
        form.Controls.AddRange(controls);
        var helpers = controls.Select((control, i) => new D3D_AnimationHelper(control) { FPS = rates[i] }).ToArray();
        var clock = new Stopwatch();
        form.FormClosing += (_, _) => clock.Stop();
        using var stop = new System.Windows.Forms.Timer { Interval = 2000 };
        stop.Tick += (_, _) => form.Close();
        form.Shown += (_, _) =>
        {
            D3D_PaintBridge.V5ProbeEnabled = true;
            D3D_PaintBridge.ResetV5Probe();
            for (var i = 0; i < helpers.Length; i++)
            {
                var control = controls[i];
                helpers[i].StartFrameLoop((_, _) =>
                {
                    control.Samples++;
                    control.LeftColor = Color.FromArgb(30 + control.Samples % 200, 90, 170);
                });
            }
            clock.Start();
            stop.Start();
        };
        try
        {
            Application.Run(form);
            var timings = D3D_PaintBridge.GetV5RefreshTimings();
            for (var i = 0; i < controls.Length; i++)
            {
                var published = timings.Where(t => t.Stage == "CompositionPublished" && t.ControlName.EndsWith(":" + controls[i].Name)).Sum(t => t.Count);
                var rate = published / clock.Elapsed.TotalSeconds;
                Console.WriteLine($"Mixed rate target={rates[i]} samples={controls[i].Samples} published={published} fps={rate:F1}");
                Assert(rate >= rates[i] * 0.90 && rate <= rates[i] * 1.10,
                    "Independent animation rates must not divide a shared submission budget.");
            }
        }
        finally
        {
            foreach (var helper in helpers) helper.Dispose();
            D3D_PaintBridge.V5ProbeEnabled = false;
            form.Close();
        }
    }

    private static void ProbeMappedCompositionRates()
    {
        using var watchdog = new System.Threading.Timer(_ => Environment.FailFast("Mapped rate probe exceeded 12 seconds."), null, 12000, Timeout.Infinite);
        using var form = new Form { ClientSize = new Size(640, 360), ShowInTaskbar = false };
        using var outer = new CompositionProbeControl { Name = "animatedSource", Dock = DockStyle.Fill };
        using var middle = new CompositionProbeControl { Name = "animatedMiddle", Source = outer, Bounds = new Rectangle(20, 20, 580, 300) };
        var leaves = Enumerable.Range(0, 3).Select(i => new CompositionProbeControl {
            Name = $"animatedMapped{i}", Source = outer, Bounds = new Rectangle(10 + i * 180, 15, 160, 180)
        }).ToArray();
        middle.Controls.AddRange(leaves);
        outer.Controls.Add(middle);
        form.Controls.Add(outer);
        var controls = new[] { outer, middle }.Concat(leaves).ToArray();
        var helpers = controls.Select(control => new D3D_AnimationHelper(control) { FPS = 120 }).ToArray();
        var clock = new Stopwatch();
        form.FormClosing += (_, _) => clock.Stop();
        using var stop = new System.Windows.Forms.Timer { Interval = 3000 };
        stop.Tick += (_, _) => form.Close();
        form.Shown += (_, _) =>
        {
            D3D_PaintBridge.V5ProbeEnabled = true;
            D3D_PaintBridge.ResetV5Probe();
            foreach (var pair in helpers.Zip(controls))
            {
                pair.First.StartFrameLoop((_, _) =>
                {
                    pair.Second.Samples++;
                    pair.Second.LeftColor = Color.FromArgb(30 + pair.Second.Samples % 200, 90, 170);
                });
            }
            clock.Start();
            stop.Start();
        };
        try
        {
            Application.Run(form);
            var timings = D3D_PaintBridge.GetV5RefreshTimings();
            foreach (var control in controls)
            {
                var published = timings.Where(t => t.Stage == "CompositionPublished" && t.ControlName.EndsWith(":" + control.Name)).Sum(t => t.Count);
                var rate = published / clock.Elapsed.TotalSeconds;
                Console.WriteLine($"Mapped target=120 {control.Name}: samples={control.Samples} published={published} fps={rate:F1}");
                Assert(rate >= 114 && rate <= 126, "All five nested animations must publish at approximately 120 FPS.");
            }
            var commits = timings.Where(t => t.Stage == "CompositionCommit").Sum(t => t.Count);
            Assert(commits < 3 * 126 + 15, "Nested animations must share commits, not issue one commit per HWND.");
        }
        finally
        {
            foreach (var helper in helpers) helper.Dispose();
            D3D_PaintBridge.V5ProbeEnabled = false;
            form.Close();
        }
    }
}
