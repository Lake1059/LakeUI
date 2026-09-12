using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using LakeUI;

static partial class Program
{
    private static void ProbeDropDownAnimation(ModernComboBox.DropDownDisplayMode mode)
    {
        using var watchdog = new System.Threading.Timer(_ => Environment.FailFast("Dropdown probe timed out."), null, 12000, Timeout.Infinite);
        using var host = new Form { ClientSize = new Size(500, 400), ShowInTaskbar = false };
        using var combo = new ModernComboBox { Bounds = new Rectangle(50, 50, 240, 40),
            DropDownAnimationDuration = 1000, DropDownMode = mode, DropDownShadowEnabled = true };
        combo.Items.AddRange(Enumerable.Range(0, 8).Select(i => "Item " + i));
        combo.SelectedIndex = 3;
        host.Controls.Add(combo);
        Form? popup = null;
        Size size = Size.Empty;
        var clock = new Stopwatch();
        var clips = new HashSet<Rectangle>();
        var changed = false;
        var redrawn = false;
        long revision = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 20 };
        host.Shown += (_, _) =>
        {
            D3D_PaintBridge.V5ProbeEnabled = true;
            D3D_PaintBridge.ResetV5Probe();
            combo.DroppedDown = true;
            popup = (Form)typeof(ModernComboBox).GetField("_dropDownForm", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(combo)!;
            size = popup.ClientSize;
            clock.Start();
            timer.Start();
        };
        timer.Tick += (_, _) =>
        {
            if (popup != null && !popup.IsDisposed)
            {
                clips.Add((Rectangle)popup.GetType().GetField("_animationClipRect", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(popup)!);
                Assert(size == popup.ClientSize, "Dropdown reveal must keep its mapped surface dimensions stable.");
                if (!changed && clock.ElapsedMilliseconds >= 400)
                {
                    revision = D3D_ControlSurfaceRegistry.GetRevision(popup);
                    combo.DropDownBackColor = Color.DarkGreen;
                    popup.Invalidate();
                    changed = true;
                }
                else if (changed && D3D_ControlSurfaceRegistry.GetRevision(popup) > revision) redrawn = true;
            }
            if (clock.ElapsedMilliseconds >= 1300) host.Close();
        };
        try
        {
            Application.Run(host);
            var renders = D3D_PaintBridge.GetV5RefreshTimings()
                .Where(t => t.Stage == "RenderGpu" && t.ControlId == RuntimeHelpers.GetHashCode(popup!)).Sum(t => t.Count);
            Console.WriteLine($"Dropdown {mode}: renders={renders}, distinctClips={clips.Count}, contentUpdated={redrawn}");
            Assert(clips.Count >= 10 && clips.Contains(Rectangle.Empty), "Dropdown must progressively reveal to its complete size.");
            Assert(renders <= 5 && redrawn, "Dropdown must reuse static content while honoring real content invalidation.");
        }
        finally
        {
            timer.Stop();
            combo.DropDownAnimationDuration = 0;
            combo.DroppedDown = false;
            popup?.Dispose();
            D3D_PaintBridge.V5ProbeEnabled = false;
            host.Close();
        }
    }

    private static void ProbeMenuAnimations(bool shadow)
    {
        using var watchdog = new System.Threading.Timer(_ => Environment.FailFast("Menu probe timed out."), null, 12000, Timeout.Infinite);
        using var host = new Form { ClientSize = new Size(800, 500), ShowInTaskbar = false };
        using var menu = new ModernContextMenu { AnimationDuration = 0, AnimationFPS = 120, ShadowEnabled = shadow };
        using var childMenu = new ModernContextMenu { AnimationDuration = 1400, AnimationFPS = 120, ShadowEnabled = shadow };
        menu.Items.Add(new ModernContextMenu.ModernMenuItem("First"));
        menu.Items.Add(new ModernContextMenu.ModernMenuItem("Submenu") { SubMenu = childMenu });
        for (var i = 0; i < 8; i++) childMenu.Items.Add(new ModernContextMenu.ModernMenuItem("Child " + i));
        ModernContextMenu.MenuPopupForm? popup = null;
        ModernContextMenu.MenuPopupForm? child = null;
        var clock = new Stopwatch();
        var samples = 0;
        var clipSamples = 0;
        var previousClip = -1;
        var fullSize = Size.Empty;
        var appearanceChanged = false;
        long revisionBeforeChange = 0;
        var appearanceRendered = false;
        float initialY = 0, lastY = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 20 };
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Field(object obj, string name) => obj.GetType().GetField(name, flags)!.GetValue(obj);
        void Hover(int index)
        {
            var rows = (List<Rectangle>)Field(popup!, "项目区域列表")!;
            popup!.GetType().GetMethod("OnMouseMove", flags)!.Invoke(popup,
                new object[] { new MouseEventArgs(MouseButtons.None, 0, rows[index].X + 10, rows[index].Y + 10, 0) });
        }
        host.Shown += (_, _) =>
        {
            menu.Show(host, new Point(30, 30));
            popup = (ModernContextMenu.MenuPopupForm)typeof(ModernContextMenu).GetField("当前弹出窗口", flags)!.GetValue(menu)!;
            popup.Name = "root-menu";
            Hover(0);
            menu.AnimationDuration = 1400;
            initialY = lastY = (float)Field(popup, "动画当前Y")!;
            D3D_PaintBridge.V5ProbeEnabled = true;
            D3D_PaintBridge.ResetV5Probe();
            Hover(1);
            child = (ModernContextMenu.MenuPopupForm?)Field(popup, "子菜单弹窗");
            if (child != null)
            {
                child.Name = "child-menu";
                fullSize = child.ClientSize;
            }
            clock.Start();
            timer.Start();
        };
        timer.Tick += (_, _) =>
        {
            if (popup != null && !popup.IsDisposed)
            {
                var y = (float)Field(popup, "动画当前Y")!;
                if (Math.Abs(y - lastY) > 0.001f) samples++;
                lastY = y;
            }
            if (child != null && !child.IsDisposed)
            {
                var clip = (int)Field(child, "动画裁剪高度")!;
                if (clip != previousClip) clipSamples++;
                previousClip = clip;
                Assert(child.ClientSize == fullSize, "Reveal animation must not resize the HWND or its mapped surface.");
                if (!appearanceChanged && clock.ElapsedMilliseconds >= 500)
                {
                    revisionBeforeChange = D3D_ControlSurfaceRegistry.GetRevision(child);
                    childMenu.BackColor = Color.FromArgb(45, 65, 85);
                    child.RefreshAppearance();
                    appearanceChanged = true;
                }
                else if (appearanceChanged && D3D_ControlSurfaceRegistry.GetRevision(child) > revisionBeforeChange)
                    appearanceRendered = true;
            }
            if (clock.ElapsedMilliseconds >= 1700) host.Close();
        };
        try
        {
            Application.Run(host);
            Console.WriteLine($"shadow={shadow} hoverUpdates={samples} y={initialY:F1}->{lastY:F1} elapsed={clock.ElapsedMilliseconds}ms");
            foreach (var group in D3D_PaintBridge.GetV5RefreshTimings().GroupBy(t => (t.Stage, t.ControlName)))
                if (group.Key.Stage is "RenderGpu" or "Present")
                    Console.WriteLine($"{group.Key}: count={group.Sum(t => t.Count)} total={group.Sum(t => t.TotalMilliseconds):F1}ms");
            Assert(samples >= 10 && child != null, "Parent hover must progress while its submenu opens.");
            Assert(clipSamples >= 10 && previousClip == -1, "The submenu must reveal progressively and finish with the full region.");
            Assert(appearanceRendered, "A real appearance invalidation during reveal must update the persistent surface.");
            var childRenders = D3D_PaintBridge.GetV5RefreshTimings()
                .Where(t => t.Stage == "RenderGpu" && t.ControlId == RuntimeHelpers.GetHashCode(child!)).Sum(t => t.Count);
            Assert(childRenders <= 4, "Region-only animation must not rebuild and present unchanged menu content on every tick.");
        }
        finally
        {
            timer.Stop();
            menu.AnimationDuration = childMenu.AnimationDuration = 0;
            menu.Close();
            child?.Dispose();
            popup?.Dispose();
            D3D_PaintBridge.V5ProbeEnabled = false;
            host.Close();
        }
        Assert(popup!.IsDisposed && child!.IsDisposed, "The entire menu chain must be released on completion.");
    }
}
