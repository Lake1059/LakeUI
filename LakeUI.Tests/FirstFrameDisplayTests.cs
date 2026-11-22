using System.Diagnostics;
using System.Runtime.InteropServices;
using LakeUI;

internal static class FirstFrameDisplayTests
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out uint value, int size);

    internal static void Run(string scenario)
    {
        var fallback = scenario == "fallback";
        var standardChrome = scenario == "native";
        var requested = scenario is "DX12" or "fallback" ? RenderBackend.Direct3D12 : RenderBackend.Direct3D11;
        var expected = fallback ? RenderBackend.Direct3D11 : requested;
        GlobalOptions.SetRenderBackendAsync(requested).GetAwaiter().GetResult();

        using var form = RenderingRegression.Host();
        using var chrome = new ThisIsYourWindow { ShowAnimation = ThisIsYourWindow.WindowShowAnimationMode.None };
        using var panel = new Panel { Dock = DockStyle.Fill };
        using var first = new FrameControl(form) { Bounds = new Rectangle(8, 8, 90, 40) };
        using var second = new FrameControl(form) { Bounds = new Rectangle(8, 60, 90, 40) };
        using var hidden = new LegacyControl(form) { Size = new Size(80, 80), Visible = false };
        using var unsupported = new LegacyControl(form) { Bounds = new Rectangle(110, 10, 90, 40) };
        var nativePainted = false;
        panel.Paint += (_, _) => nativePainted = true;
        form.Controls.Add(panel);
        panel.Controls.Add(first);
        panel.Controls.Add(hidden);

        Task<bool>? ready = standardChrome ? GlobalOptions.DeferWindowDisplayUntilFirstFrameAsync(form) : null;
        form.Load += (_, _) =>
        {
            if (!standardChrome)
            {
                chrome.Attach(form);
                RenderingRegression.Require(WindowFirstFrame.IsPending(form), "Attach must defer the first display.");
                ready = GlobalOptions.DeferWindowDisplayUntilFirstFrameAsync(form, fallback);
            }
            panel.Controls.Add(second);
            if (fallback) panel.Controls.Add(unsupported);
        };
        form.Show();

        RenderingRegression.Require(form.Visible && IsAppCloaked(form), "The HWND must be cloaked without breaking WinForms visibility.");
        RenderingRegression.Require(AwaitOnUiThread(ready!) && !IsAppCloaked(form),
            $"The form did not finish first display via {expected}. {GlobalOptions.BackendStartupError}");
        RenderingRegression.Require(GlobalOptions.ActiveBackend == expected && nativePainted && hidden.DrawCount == 0,
            "First display must include native painting and skip hidden controls.");
        foreach (var participant in new[] { first, second })
        {
            RenderingRegression.Require(participant.DrawCount > 0 && participant.FirstDrawWasCloaked &&
                D3D_V5Presentation.HasPublishedFrame(participant, expected),
                "Every visible participant must publish while the form is cloaked, including controls added during Load.");
        }

        if (scenario == "DX12")
        {
            var hwnd = form.Handle;
            foreach (var target in new[] { RenderBackend.Direct3D11, RenderBackend.Direct3D12 })
            {
                var result = AwaitOnUiThread(GlobalOptions.SetRenderBackendAsync(target));
                RenderingRegression.Require(result == BackendSelectionResult.Switched && GlobalOptions.ActiveBackend == target &&
                    form.Handle == hwnd && !IsAppCloaked(form),
                    $"Switch to {target} did not publish in the existing HWND. {result}: {GlobalOptions.LastBackendSwitch?.Cause}");
            }
        }
        Console.WriteLine($"First display ({scenario}) passed with {expected}.");
    }

    private static T AwaitOnUiThread<T>(Task<T> task)
    {
        var started = Stopwatch.GetTimestamp();
        while (!task.IsCompleted && Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(15))
        {
            Application.DoEvents();
            Thread.Sleep(1);
        }
        RenderingRegression.Require(task.IsCompleted, "The UI operation timed out.");
        return task.GetAwaiter().GetResult();
    }

    private static bool IsAppCloaked(Form form)
    {
        Marshal.ThrowExceptionForHR(DwmGetWindowAttribute(form.Handle, 14, out var flags, sizeof(uint)));
        return (flags & 1) != 0;
    }

    private class LegacyControl(Form form) : Control, V5_IGpuPresentationSource, D3D_IGpuRenderable
    {
        internal int DrawCount;
        internal bool FirstDrawWasCloaked;

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!D3D_PaintBridge.PaintRenderable(e, this, this)) base.OnPaint(e);
        }

        public void RenderGpu(D3D_PaintContext context)
        {
            RecordDraw();
            context.FillRectangle(ClientRectangle, Color.CornflowerBlue);
        }

        protected void RecordDraw()
        {
            if (DrawCount++ == 0) FirstDrawWasCloaked = IsAppCloaked(form);
        }
    }

    private sealed class FrameControl(Form form) : LegacyControl(form), ID3D12Renderable
    {
        public void RenderGpu12(D3D12_PaintContext context)
        {
            RecordDraw();
            context.FillRectangle(ClientRectangle, Color.CornflowerBlue);
        }
    }
}
