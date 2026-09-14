using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using LakeUI;

static partial class Program
{
    private static void ProbeConcurrentAnimations(int fps, int seconds = 2)
    {
        using var watchdog = new System.Threading.Timer(_ => Environment.FailFast("Animation probe timed out."), null, (seconds + 10) * 1000, Timeout.Infinite);
        using var form = new Form { ClientSize = new Size(560, 300), ShowInTaskbar = false };
        var animationDuration = (seconds + 2) * 2000;
        using var win11 = new ProgressRing { Name = "win11", Bounds = new Rectangle(0, 0, 90, 90), AutoStart = false, AnimationFPS = fps };
        using var win10 = new ProgressRing { Name = "win10", Bounds = new Rectangle(100, 0, 90, 90), AutoStart = false, AnimationFPS = fps, AnimationStyle = ProgressRing.StyleEnum.Win10 };
        using var bar = new ExcellentProgressBar { Name = "bar", Bounds = new Rectangle(0, 110, 250, 35), AnimationFPS = fps, AnimationDuration = animationDuration };
        using var gauge = new RoundDashBoard { Name = "gauge", Bounds = new Rectangle(280, 0, 200, 200), AnimationFPS = fps, AnimationDuration = animationDuration };
        using var button = new ModernButton { Name = "button", Bounds = new Rectangle(0, 200, 180, 65), Text = "Animation probe", AnimationFPS = fps, RippleAnimationDuration = animationDuration };
        var single = Environment.GetEnvironmentVariable("LAKEUI_PROBE_SINGLE") == "1";
        var controls = single ? new Control[] { win11 } : new Control[] { win11, win10, bar, gauge, button };
        form.Controls.AddRange(controls);
        var revisions = new long[controls.Length];
        var updates = new int[controls.Length];
        var last = new double[controls.Length];
        var gaps = Enumerable.Range(0, controls.Length).Select(_ => new List<double>()).ToArray();
        var clock = new Stopwatch();
        form.FormClosing += (_, _) => clock.Stop();
        using var sample = new System.Windows.Forms.Timer { Interval = 15 };
        sample.Tick += (_, _) =>
        {
            var now = clock.Elapsed.TotalMilliseconds;
            for (var i = 0; i < controls.Length; i++)
            {
                var revision = D3D_ControlSurfaceRegistry.GetRevision(controls[i]);
                if (revision == revisions[i]) continue;
                if (updates[i] > 0) gaps[i].Add(now - last[i]);
                last[i] = now;
                updates[i]++;
                revisions[i] = revision;
            }
            if (now >= seconds * 1000) form.Close();
        };
        form.Shown += (_, _) =>
        {
            // 长时探针使用持续可辨的缓出段，避免缓入段每步小于既有失效阈值而主动跳过重绘。
            if (seconds > 2)
                foreach (var control in new Control[] { bar, gauge })
                    foreach (var field in control.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Where(f => f.FieldType == typeof(D3D_AnimationHelper)))
                        ((D3D_AnimationHelper)field.GetValue(control)!).EasingMode = D3D_AnimationHelper.EasingModeEnum.EaseOut;
            using (D3D_PaintBridge.BeginRenderUpdate(form))
            {
                win11.StartAnimation();
                if (!single)
                {
                    win10.StartAnimation();
                    bar.Value = 100;
                    gauge.Value = 100;
                    typeof(ModernButton).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(button, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 40, 30, 0) });
                }
            }
            D3D_PaintBridge.V5ProbeEnabled = true;
            D3D_PaintBridge.ResetV5Probe();
            clock.Start();
            sample.Start();
        };
        try
        {
            Application.Run(form);
            var timings = D3D_PaintBridge.GetV5RefreshTimings();
            D3D_RenderCore.DeviceManager.CompositionDevice.GetFrameStatistics(out var composition).CheckError();
            Console.WriteLine($"DWM composition rate={composition.CurrentCompositionRate.Numerator / (double)composition.CurrentCompositionRate.Denominator:F2}Hz (synchronized composition, no tearing flags)");
            Console.WriteLine($"FPS={fps} elapsed={clock.ElapsedMilliseconds}ms ticks={D3D_AnimationHelper.GetThreadSchedulerSnapshot().TickCount}");
            for (var i = 0; i < controls.Length; i++)
            {
                var render = timings.Where(t => t.Stage == "RenderGpu" && t.ControlName.EndsWith(":" + controls[i].Name)).Sum(t => t.Count);
                var present = timings.Where(t => t.Stage == "CompositionPublished" && t.ControlName.EndsWith(":" + controls[i].Name)).Sum(t => t.Count);
                Console.WriteLine($"{controls[i].Name}: renders={render} published={present} fps={present / clock.Elapsed.TotalSeconds:F1} observed={updates[i]} maxGap={gaps[i].DefaultIfEmpty().Max():F1}ms");
                if (fps == 120)
                    Assert(present / clock.Elapsed.TotalSeconds >= 114,
                        $"{controls[i].Name} must sustain approximately 120 published frames per second.");
            }
            foreach (var stage in timings.GroupBy(t => t.Stage))
                Console.WriteLine($"{stage.Key}: {stage.Sum(t => t.TotalMilliseconds):F1}ms / {stage.Sum(t => t.Count)} calls");
            Assert(clock.ElapsedMilliseconds < seconds * 1000 + 4000, "Animation must not starve the closing timer.");
            Assert(updates.All(count => count >= 10), "All concurrent animations must advance while the window is stationary.");
        }
        finally
        {
            sample.Stop();
            win11.StopAnimation();
            win10.StopAnimation();
            D3D_PaintBridge.V5ProbeEnabled = false;
            form.Close();
        }
    }

    private static void VerifyAnimationTickCommitsAllOwners()
    {
        using var form = new Form { ClientSize = new Size(300, 180), ShowInTaskbar = false };
        using var outer = new CountingGpuControl { Bounds = new Rectangle(0, 0, 280, 160) };
        using var inner = new CountingGpuControl { Bounds = new Rectangle(10, 10, 120, 80) };
        outer.Controls.Add(inner);
        form.Controls.Add(outer);
        form.Show();
        Application.DoEvents();
        using var outerAnimation = new D3D_AnimationHelper(outer) { FPS = 0 };
        using var innerAnimation = new D3D_AnimationHelper(inner) { FPS = 0 };
        var order = new List<string>();
        var ticks = 0;
        outer.DuringRender = _ => order.Add("outer");
        inner.DuringRender = _ => order.Add("inner");
        try
        {
            innerAnimation.StartFrameLoop((_, _) => ticks++);
            outerAnimation.StartFrameLoop((_, _) => ticks++);
            var scheduler = typeof(D3D_AnimationHelper).GetField("_threadScheduler", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            var tick = scheduler.GetType().GetMethod("RunTick", BindingFlags.Instance | BindingFlags.NonPublic)!;
            for (var i = 0; i < 3; i++)
            {
                order.Clear();
                var before = ticks;
                tick.Invoke(scheduler, null);
                Assert(ticks == before + 2, "Each animation must sample once in the same scheduler tick.");
                Assert(order.SequenceEqual(new[] { "outer", "inner" }),
                    "A completed tick must commit both animations outer-to-inner without waiting for WM_TIMER.");
            }
            order.Clear();
            using (D3D_PaintBridge.BeginRenderUpdate(form))
            {
                tick.Invoke(scheduler, null);
                Assert(order.Count == 0, "A render transaction must still suppress animation commits.");
            }
            Assert(order.SequenceEqual(new[] { "outer", "inner" }), "Transaction completion must retain animation depth ordering.");
        }
        finally
        {
            innerAnimation.StopFrameLoop();
            outerAnimation.StopFrameLoop();
            inner.DuringRender = null;
            outer.DuringRender = null;
            form.Close();
        }
    }
}
