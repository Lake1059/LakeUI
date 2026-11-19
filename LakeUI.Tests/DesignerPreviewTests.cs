using System.ComponentModel;
using LakeUI;

internal static class DesignerPreviewTests
{
    internal static void Run(string backend)
    {
        var runtime = backend == "runtime";
        var expected = backend switch
        {
            "default" or "DX11" or "runtime" => RenderBackend.Direct3D11,
            "DX12" => RenderBackend.Direct3D12,
            _ => throw new ArgumentException($"Unknown designer backend: {backend}")
        };
        var originalContext = LicenseManager.CurrentContext;
        try
        {
            if (backend != "default")
                AppContext.SetData("LakeUI.DesignerBackend", runtime ? "DX12" : backend);
            if (backend == "default")
                LicenseManager.CurrentContext = new DesignLicenseContext();

            using var host = RenderingRegression.Host();
            if (!runtime && backend != "default") host.Site = new DesignSite(host);
            using var container = new Panel { Dock = DockStyle.Fill };
            using var control = new PreviewControl { Size = new Size(100, 60) };
            using var sibling = new PreviewControl { Bounds = new Rectangle(110, 0, 100, 60) };
            host.Controls.Add(container);
            container.Controls.Add(control);
            container.Controls.Add(sibling);
            using var legacy = new LegacyPreviewControl { Bounds = new Rectangle(0, 70, 100, 60) };
            if (expected == RenderBackend.Direct3D12) container.Controls.Add(legacy);

            var firstFrame = GlobalOptions.WaitForFirstFrameAsync();
            RenderingRegression.Require(GlobalOptions.RequestedBackend is null, "Backend startup must wait for the form to show or for a designer paint.");
            host.Show();
            if (expected == RenderBackend.Direct3D12) legacy.Refresh();
            control.Refresh();
            sibling.Refresh();
            D3D_V5Presentation.FlushPendingFrame();

            RenderingRegression.Require(RenderBackendStartup.IsDesignerPreview == !runtime,
                "Only a design-time control may start a designer preview.");
            RenderingRegression.Require(firstFrame.IsCompletedSuccessfully && firstFrame.Result == expected,
                $"The form failed to publish a {expected} frame. {GlobalOptions.BackendStartupError}");
            foreach (var participant in new[] { control, sibling })
            {
                RenderingRegression.Require(runtime || participant.BasePaints > 0,
                    "The designer must still receive WinForms painting.");
                RenderingRegression.Require(
                    expected == RenderBackend.Direct3D11
                        ? participant.Dx11Paints > 0 && participant.Dx12Paints == 0
                        : participant.Dx12Paints > 0 && participant.Dx11Paints == 0,
                    "The control used a different backend than expected.");
            }
            if (expected == RenderBackend.Direct3D12)
                RenderingRegression.Require(legacy.BasePaints > 0 && legacy.Dx11Paints == 0,
                    "A DX11-only control must retain its WinForms painting in a DX12 designer.");
            Console.WriteLine($"Rendering check ({backend}) published via {expected}.");
        }
        finally
        {
            LicenseManager.CurrentContext = originalContext;
        }
    }

    private sealed class DesignLicenseContext : LicenseContext
    {
        public override LicenseUsageMode UsageMode => LicenseUsageMode.Designtime;
    }

    private sealed class DesignSite(IComponent component) : ISite
    {
        public IComponent Component => component;
        public IContainer? Container => null;
        public bool DesignMode => true;
        public string? Name { get; set; }
        public object? GetService(Type serviceType) => null;
    }

    private class LegacyPreviewControl : Control, V5_IGpuPresentationSource, D3D_IGpuRenderable
    {
        internal int Dx11Paints;
        internal int BasePaints;

        protected override void OnPaint(PaintEventArgs e)
        {
            if (D3D_PaintBridge.PaintRenderable(e, this, this)) return;
            base.OnPaint(e);
            BasePaints++;
        }

        public void RenderGpu(D3D_PaintContext context)
        {
            Dx11Paints++;
            context.FillRectangle(ClientRectangle, Color.CornflowerBlue);
        }
    }

    private sealed class PreviewControl : LegacyPreviewControl, ID3D12Renderable
    {
        internal int Dx12Paints;

        public void RenderGpu12(D3D12_PaintContext context)
        {
            Dx12Paints++;
            context.FillRectangle(ClientRectangle, Color.CornflowerBlue);
        }
    }
}
