using System.Diagnostics;
using LakeUI;
using Vortice.Direct3D12;
using Vortice.DXGI;

internal static class TabBackdropRegressionTests
{
    internal static void Run(string backend)
    {
        var selected = backend switch
        {
            "DX11" => RenderBackend.Direct3D11,
            "DX12" => RenderBackend.Direct3D12,
            _ => throw new ArgumentException($"Unknown test backend: {backend}")
        };
        GlobalOptions.SetRenderBackendAsync(selected).GetAwaiter().GetResult();
        CheckForwardedBackground(selected);
        if (selected == RenderBackend.Direct3D12)
        {
            CheckBackgroundMapping();
            CheckAttachedWindowBackground();
            CheckBackgroundBeforeCommit();
        }
        CheckBoundPages(selected, vertical: true);
        CheckBoundPages(selected, vertical: false);
        Console.WriteLine($"Bound-page reuse and background regression ({backend}) passed.");
    }

    private static void CheckBoundPages(RenderBackend backend, bool vertical)
    {
        using var host = RenderingRegression.Host();
        using var first = new Form { BackColor = Color.Navy };
        using var second = new Form { BackColor = Color.Maroon };
        using var firstLayer = new ModernPanel
        {
            Dock = DockStyle.Fill, BackColor = Color.Transparent, BackColor1 = Color.Transparent,
            TransparentRenderMode = ModernPanel.TransparentRenderModeEnum.SkipWhenFullyTransparent
        };
        using var firstChild = new Surface { Bounds = new Rectangle(4, 4, 50, 30), Fill = Color.Blue };
        using var firstPassive = new Surface { Bounds = new Rectangle(61, 4, 50, 30), Fill = Color.Green };
        using var inherited = new Surface { Bounds = new Rectangle(116, 4, 40, 30) };
        using var secondChild = new Surface { Bounds = new Rectangle(4, 4, 50, 30), Fill = Color.Red };
        using var nestedFirst = new Surface { Fill = Color.Orange };
        using var nestedSecond = new Surface { Fill = Color.Purple };
        using var nested = new ModernTabControl { Bounds = new Rectangle(4, 58, 160, 105) };
        nested.Items.Add(new ModernTabControl.ModernTab { Text = "Nested A", BoundControl = nestedFirst });
        nested.Items.Add(new ModernTabControl.ModernTab { Text = "Nested B", BoundControl = nestedSecond });
        nested.RefreshItems();
        nested.SelectedIndex = 0;
        firstLayer.Controls.Add(nested);
        firstLayer.Controls.Add(firstChild);
        firstLayer.Controls.Add(firstPassive);
        firstLayer.Controls.Add(inherited);
        first.Controls.Add(firstLayer);
        second.Controls.Add(secondChild);

        using Control tabs = vertical ? new ModernTabListControl { TabStripWidth = 90 } : new ModernTabControl();
        tabs.Dock = DockStyle.Fill;
        host.Controls.Add(tabs);
        Action<int> select;
        if (tabs is ModernTabListControl list)
        {
            list.Items.Add(new ModernTabListControl.ModernTabPage { Text = "First", BoundControl = first });
            list.Items.Add(new ModernTabListControl.ModernTabPage { Text = "Second", BoundControl = second });
            list.RefreshItems();
            select = index => list.SelectedIndex = index;
        }
        else
        {
            var strip = (ModernTabControl)tabs;
            strip.Items.Add(new ModernTabControl.ModernTab { Text = "First", BoundControl = first });
            strip.Items.Add(new ModernTabControl.ModernTab { Text = "Second", BoundControl = second });
            strip.RefreshItems();
            select = index => strip.SelectedIndex = index;
        }

        select(0);
        host.Show();
        Drain();
        RequireFrame(firstChild, backend);
        RequireFrame(nestedFirst, backend);
        select(1);
        RequireFrame(secondChild, backend);
        Drain();
        var expected = (firstChild.DrawCount, firstPassive.DrawCount, secondChild.DrawCount, inherited.DrawCount, nestedFirst.DrawCount);
        for (var i = 0; i < 3; i++)
        {
            select(0);
            RequireFrame(firstChild, backend);
            Drain();
            select(1);
            RequireFrame(secondChild, backend);
            Drain();
        }
        var actual = (firstChild.DrawCount, firstPassive.DrawCount, secondChild.DrawCount, inherited.DrawCount, nestedFirst.DrawCount);
        RenderingRegression.Require(actual == expected,
            $"{tabs.GetType().Name} ({backend}) redrew unchanged pages: {expected} -> {actual}.");

        if (backend == RenderBackend.Direct3D12)
        {
            RenderingRegression.Require(D3D12StartupDispatch.FindPresenter(firstChild) is { Atlas: null, RetainedContent: not null },
                "A hidden bound page should retain just a texture, not an active atlas slot.");
            first.BackColor = Color.Purple;
        }
        firstChild.Fill = Color.Yellow;
        D3D_InvalidationRouter.RequestRender(firstChild, firstChild.ClientRectangle);
        Drain();
        RenderingRegression.Require(firstChild.DrawCount == expected.Item1, "Hidden controls should not run their renderer.");
        select(0);
        RequireFrame(firstChild, backend);
        Drain();
        RenderingRegression.Require(firstChild.DrawCount == expected.Item1 + 1 && firstPassive.DrawCount == expected.Item2,
            "A real hidden-page change must be rendered on reveal without redrawing other controls.");
        if (backend == RenderBackend.Direct3D12)
        {
            ExpectPixel(firstChild, 5, 5, Color.Yellow);
            ExpectPixel(inherited, 5, 5, Color.Purple);
            RenderingRegression.Require(inherited.DrawCount == expected.Item4 + 1, "Only consumers of the changed background should redraw.");
        }

        // A full native exposure of a clean surface is not a logical paint request.
        var beforeExposure = firstChild.DrawCount;
        host.Invalidate();
        firstChild.Update();
        Drain();
        RenderingRegression.Require(firstChild.DrawCount == beforeExposure, "Host exposure must not dirty an unchanged child surface.");

        if (backend == RenderBackend.Direct3D12 && vertical)
        {
            var originalBudget = GlobalOptions.GpuCacheBudgetBytes;
            try
            {
                GlobalOptions.GpuCacheBudgetBytes = 0;
                select(1);
                Drain();
                RenderingRegression.Require(D3D12StartupDispatch.FindPresenter(firstChild) is null,
                    "Hidden page textures must be evictable under the GPU cache budget.");
                select(0);
                RequireFrame(firstChild, backend);
                Drain();
                ExpectPixel(firstChild, 5, 5, Color.Yellow);
            }
            finally { GlobalOptions.GpuCacheBudgetBytes = originalBudget; }
        }
    }

    private static void CheckForwardedBackground(RenderBackend backend)
    {
        using var host = RenderingRegression.Host();
        using var source = new Surface { Bounds = new Rectangle(0, 0, 100, 60), Fill = Color.Red };
        using var replacement = new Surface { Bounds = source.Bounds, Fill = Color.Orange };
        using var forwarder = new ModernPanel
        {
            Bounds = new Rectangle(12, 12, 45, 30), BackColor = Color.Transparent, BackColor1 = Color.Transparent,
            TransparentRenderMode = ModernPanel.TransparentRenderModeEnum.SkipWhenFullyTransparent,
            BackgroundSource = source
        };
        using var consumer = new Surface { Bounds = forwarder.Bounds, Source = forwarder };
        host.Controls.AddRange([source, replacement, forwarder, consumer]);
        host.Show();
        Drain();
        ExpectPixel(consumer, 5, 5, Color.Red, backend);
        forwarder.BackgroundSource = replacement;
        Drain();
        ExpectPixel(consumer, 5, 5, Color.Orange, backend);
    }

    private static void CheckBackgroundBeforeCommit()
    {
        using var host = RenderingRegression.Host();
        using var source = new Surface { Bounds = new Rectangle(8, 8, 70, 40), Fill = Color.DarkOrange };
        using var middle = new Surface { Bounds = source.Bounds, Source = source };
        using var consumer = new Surface { Bounds = source.Bounds, Source = middle };
        host.Show();
        using (D3D_PaintBridge.BeginRenderUpdate(host))
        {
            // Same depth, with both consumers queued before their sources.
            host.Controls.AddRange([consumer, middle, source]);
            D3D_V5Presentation.RequestRenderBatched(consumer);
            D3D_V5Presentation.RequestRenderBatched(middle);
            D3D_V5Presentation.RequestRenderBatched(source);
            D3D_RenderUpdate.AfterCommit(() => ExpectPixel(consumer, 6, 6, Color.DarkOrange));
        }
    }

    private static void CheckBackgroundMapping()
    {
        using var host = RenderingRegression.Host();
        using var other = RenderingRegression.Host();
        host.BackColor = Color.Lime;
        using var source = new Surface { Bounds = new Rectangle(20, 20, 54, 60), Fill = Color.Red };
        using var bridge = new ModernPanel
        {
            Bounds = new Rectangle(19, 15, 40, 50), BackColor = Color.Transparent, BackColor1 = Color.Transparent,
            TransparentRenderMode = ModernPanel.TransparentRenderModeEnum.SkipWhenFullyTransparent
        };
        using var consumer = new Surface
        {
            Bounds = new Rectangle(6, 25, 100, 40), Source = bridge, Offset = new Point(8, 0),
            SourceClip = new Rectangle(0, 0, 88, 40)
        };
        using var leaf = new Surface { Bounds = new Rectangle(8, 3, 84, 24) };
        using var embedded = new Form { TopLevel = false, FormBorderStyle = FormBorderStyle.None, Bounds = new Rectangle(142, 24, 75, 60), BackColor = Color.Blue };
        using var explicitForm = new ModernPanel { Bounds = new Rectangle(6, 6, 44, 30), BackColor = Color.Transparent, BackColor1 = Color.Transparent, BackgroundSource = host };
        using var nativeFormChild = new Surface { Bounds = new Rectangle(55, 6, 11, 12) };
        host.Controls.Add(consumer);
        host.Controls.Add(embedded);
        consumer.Controls.Add(leaf);
        embedded.Controls.Add(explicitForm);
        embedded.Controls.Add(nativeFormChild);
        other.Controls.Add(source);
        source.Controls.Add(bridge);
        embedded.Show();
        host.Show();
        other.Show();
        // The explicit source lives in another HWND tree and may publish after its consumers.
        Drain();
        ExpectPixel(leaf, 1, 4, Color.Lime); // outside the partial explicit source
        ExpectPixel(leaf, 15, 4, Color.Red);
        ExpectPixel(leaf, 76, 4, Color.Lime);
        ExpectPixel(explicitForm, 8, 8, Color.Lime);
        ExpectPixel(nativeFormChild, 3, 3, Color.Blue);

        host.Location = new Point(-31999, -32000);
        host.Location = new Point(-32000, -32000);
        Drain();
        ExpectPixel(leaf, 15, 4, Color.Red);
        var count = (consumer.DrawCount, leaf.DrawCount);
        source.UpdatePatch(new Rectangle(0, 50, 8, 8), Color.Fuchsia);
        Drain();
        RenderingRegression.Require((consumer.DrawCount, leaf.DrawCount) == count,
            "Damage outside the sampled region must not wake background consumers.");
        source.UpdatePatch(new Rectangle(9, 8, 12, 12), Color.Yellow);
        Drain();
        ExpectPixel(leaf, 18, 10, Color.Yellow);
        ExpectPixel(leaf, 35, 10, Color.Red);

        using var alternate = new Surface { Bounds = source.Bounds, Fill = Color.Orange };
        other.Controls.Add(alternate);
        bridge.BackgroundSource = alternate;
        Drain();
        ExpectPixel(leaf, 35, 10, Color.Orange);

        // A hidden source may still be required by a visible consumer, even at a zero cache budget.
        var originalBudget = GlobalOptions.GpuCacheBudgetBytes;
        try
        {
            GlobalOptions.GpuCacheBudgetBytes = 0;
            alternate.Hide();
            D3D_V5Presentation.ReleaseHiddenSubtreeResources(alternate);
            Drain();
            RenderingRegression.Require(D3D12StartupDispatch.FindPresenter(alternate) is { Atlas: null, RetainedContent: not null },
                "A sampled source must not be evicted when hidden.");
            alternate.UpdatePatch(new Rectangle(14, 12, 8, 8), Color.Pink);
            Drain();
            ExpectPixel(leaf, 22, 10, Color.Pink);
        }
        finally { GlobalOptions.GpuCacheBudgetBytes = originalBudget; }

        alternate.Dispose();
        Drain();
        ExpectPixel(leaf, 35, 10, Color.Red);
        host.BackColor = Color.Cyan;
        Drain();
        ExpectPixel(leaf, 1, 4, Color.Cyan);
        ExpectPixel(explicitForm, 8, 8, Color.Cyan);
        ExpectPixel(nativeFormChild, 3, 3, Color.Blue);
    }

    private static void CheckAttachedWindowBackground()
    {
        using var host = RenderingRegression.Host();
        using var image = new Bitmap(1, 1);
        using var replacement = new Bitmap(1, 1);
        image.SetPixel(0, 0, Color.Red);
        replacement.SetPixel(0, 0, Color.Blue);
        using var chrome = new ThisIsYourWindow
        {
            ShowAnimation = ThisIsYourWindow.WindowShowAnimationMode.None,
            BackdropMode = ThisIsYourWindow.BackdropModeEnum.Image, BackdropImage = image,
            BackdropTintColor = Color.Transparent, BackdropNoiseOpacity = 0, BackdropBlurPasses = 0
        };
        using var embedded = new Form { TopLevel = false, FormBorderStyle = FormBorderStyle.None, Bounds = new Rectangle(30, 60, 170, 90) };
        using var layer = new ModernPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, BackColor1 = Color.Transparent, BackgroundSource = host };
        using var child = new Surface { Bounds = new Rectangle(10, 10, 40, 30) };
        host.Controls.Add(embedded);
        embedded.Controls.Add(layer);
        layer.Controls.Add(child);
        embedded.Show();
        host.Load += (_, _) => chrome.Attach(host);
        host.Show();
        Drain();
        ExpectPixel(child, 5, 5, Color.Red);
        chrome.BackdropImage = replacement;
        Drain();
        ExpectPixel(child, 5, 5, Color.Blue);
    }

    private static void Drain()
    {
        var start = Stopwatch.GetTimestamp();
        do
        {
            Application.DoEvents();
            OuterToInnerRefreshScheduler.FlushPendingRequests();
            D3D_V5Presentation.FlushPendingFrame(false);
        } while (Stopwatch.GetElapsedTime(start) < TimeSpan.FromMilliseconds(40));
        RenderingRegression.Require(GlobalOptions.BackendStartupError is null, $"Renderer failed: {GlobalOptions.BackendStartupError}");
    }

    private static void RequireFrame(Control control, RenderBackend backend) => RenderingRegression.Require(
        D3D_V5Presentation.HasPublishedFrame(control, backend), $"{backend} did not publish a frame before revealing a page.");

    private static void ExpectPixel(Control control, int x, int y, Color expected, RenderBackend backend = RenderBackend.Direct3D12)
    {
        byte[] pixel;
        if (backend == RenderBackend.Direct3D11)
        {
            pixel = [];
            var presenter = D3D_V5Presentation.GetPresenterForDiagnostics(control);
            presenter.SurfaceProbe = (context, bitmap, area) =>
                pixel = RenderingRegression.Read(context, bitmap, new Rectangle(area.X + x, area.Y + y, 1, 1));
            try { D3D_V5Presentation.RequestRender(control); }
            finally { presenter.SurfaceProbe = null; }
        }
        else
        {
            pixel = ReadDx12Pixel(control, x, y);
        }
        RenderingRegression.Require(pixel.Length == 4, "Expected one BGRA pixel from the presented surface.");
        var actual = Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
        RenderingRegression.Require(actual.ToArgb() == expected.ToArgb(),
            $"{backend} {control.GetType().Name} at ({x},{y}) expected {expected.ToArgb():X8}, got {actual.ToArgb():X8}.");
    }

    private static byte[] ReadDx12Pixel(Control control, int x, int y)
    {
        var texture = D3D12StartupDispatch.FindPresenter(control)?.RetainedContent
            ?? throw new InvalidOperationException($"No retained surface for {control.GetType().Name}.");
        var manager = D3D12StartupDispatch.CurrentGroup;
        RenderingRegression.Require(texture.Description.Format == Format.B8G8R8A8_UNorm, "Expected an SDR surface for pixel readback.");
        using var buffer = manager.Device.CreateCommittedResource(HeapType.Readback, ResourceDescription.Buffer(256), ResourceStates.CopyDest);
        using var allocator = manager.Device.CreateCommandAllocator(CommandListType.Direct);
        using var commands = manager.Device.CreateCommandList<ID3D12GraphicsCommandList>(0, CommandListType.Direct, allocator, null);
        var layout = new PlacedSubresourceFootPrint { Footprint = new SubresourceFootPrint
        {
            Format = Format.B8G8R8A8_UNorm, Width = 1, Height = 1, Depth = 1, RowPitch = 256
        } };
        commands.ResourceBarrierTransition(texture, ResourceStates.PixelShaderResource, ResourceStates.CopySource);
        commands.CopyTextureRegion(new TextureCopyLocation(buffer, layout), 0, 0, 0,
            new TextureCopyLocation(texture, 0), new Vortice.Mathematics.Box(x, y, 0, x + 1, y + 1, 1));
        commands.ResourceBarrierTransition(texture, ResourceStates.CopySource, ResourceStates.PixelShaderResource);
        commands.Close();
        manager.DirectQueue.ExecuteCommandLists([commands]);
        RenderingRegression.Require(manager.WaitForIdle(TimeSpan.FromSeconds(3)), "DX12 readback timed out.");
        var bytes = buffer.Map<byte>(0, 4);
        try { return bytes.ToArray(); }
        finally { buffer.Unmap(0); }
    }

    private sealed class Surface : Control, V5_IGpuPresentationSource, D3D_IGpuRenderable, ID3D12Renderable,
        D3D_IBackgroundSourceProvider, D3D_IBackgroundOpacitySource, D3D_IGpuDirtyRegionCoverage
    {
        private readonly List<(Rectangle Region, Color Color)> patches = [];
        internal Color Fill = Color.Transparent;
        internal Control? Source;
        internal Point Offset;
        internal Rectangle SourceClip;
        internal int DrawCount;
        public bool IsBackgroundOpaque => Fill.A == 255;
        public bool CoversDirtyRegion(Rectangle dirtyRegion) => true;
        public bool TryGetBackgroundSource(ref Control source) { source = Source!; return Source is not null; }
        internal void UpdatePatch(Rectangle region, Color color)
        {
            patches.Add((region, color));
            D3D_V5Presentation.RequestRender(this, region);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) => D3D_PaintBridge.PaintRenderable(e, this, this);
        public void RenderGpu(D3D_PaintContext context)
        {
            DrawCount++;
            if (Source is not null) context.DrawBackgroundSource(this, Source, ClientRectangle);
            if (Fill.A > 0) context.FillRectangle(ClientRectangle, Fill);
            foreach (var (region, color) in patches) context.FillRectangle(region, color);
        }
        public void RenderGpu12(D3D12_PaintContext context)
        {
            DrawCount++;
            if (Source is not null) context.AtOffset(Offset, () => context.DrawBackgroundSource(Source, SourceClip));
            if (Fill.A > 0) context.FillRectangle(ClientRectangle, Fill);
            foreach (var (region, color) in patches) context.FillRectangle(region, color);
        }
    }
}
