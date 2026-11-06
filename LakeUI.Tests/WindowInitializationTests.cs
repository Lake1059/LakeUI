using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using LakeUI;

internal static class WindowInitializationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private)!.GetValue(target)!;
    private static object? Invoke(object target, string name, params object?[] args) =>
        target.GetType().GetMethod(name, Private)!.Invoke(target, args);

    internal static void Run()
    {
        CheckShadowStyle();
        using var form = RenderingRegression.Host();
        using var chrome = new ThisIsYourWindow();
        chrome.IconSource = ThisIsYourWindow.IconSourceEnum.None;
        _ = form.Handle;
        var enabled = D3D_PaintBridge.V5ProbeEnabled;
        D3D_PaintBridge.V5ProbeEnabled = true;
        try
        {
            D3D_PaintBridge.ResetV5Probe();
            var watch = Stopwatch.StartNew();
            chrome.Attach(form);
            watch.Stop();
            var attached = D3D_PaintBridge.GetV5ProbeSnapshot();
            RenderingRegression.Require(attached.ChromeOverlayCreated > 0 && attached.ChromeOverlayCreateFailures == 0,
                "初始化必须成功创建 chrome。");
            var original = form.Padding;
            D3D_PaintBridge.ResetV5Probe();
            const int iterations = 20;
            for (var i = 0; i < iterations; i++) chrome.CaptionHeight = 33 + i;
            var immediate = D3D_PaintBridge.GetV5ProbeSnapshot().ChromeOverlayLayoutUpdates;
            D3D_PaintBridge.ResetV5Probe();
            using (chrome.BeginUpdate())
            {
                using (chrome.BeginUpdate())
                {
                    for (var i = 0; i < iterations; i++) chrome.CaptionHeight = 60 + i;
                    chrome.BorderSize = 3;
                    chrome.BorderColor = Color.Crimson;
                    chrome.WindowCornerMode = DwmWindowStyle.CornerMode.Round;
                }
                RenderingRegression.Require(D3D_PaintBridge.GetV5ProbeSnapshot().ChromeOverlayLayoutUpdates == 0,
                    "嵌套配置作用域内部不能提前更新 overlay。");
            }
            var batched = D3D_PaintBridge.GetV5ProbeSnapshot().ChromeOverlayLayoutUpdates;
            RenderingRegression.Require(batched > 0 && batched < immediate, "批量配置必须实际减少布局次数。");
            RenderingRegression.Require(form.Padding.Top == original.Top + chrome.CaptionHeight - 32 + 2,
                "批量配置的最终 Padding 必须正确。");

            using var image = new Bitmap(16, 16);
            using (chrome.BeginUpdate())
            {
                chrome.BackdropImage = image;
                chrome.BackdropMode = ThisIsYourWindow.BackdropModeEnum.Image;
            }
            var states = Field<Dictionary<IntPtr, ThisIsYourWindow.PerFormState>>(chrome, "_forms");
            var state = states[form.Handle];
            var renderer = state.Renderer;
            RenderingRegression.Require(renderer != null && renderer.WaitForIdle(2000) && renderer.HasFrame,
                "图片背景必须完成后台准备。");
            var frame = Field<int>(renderer!, "_frameVersion");
            var effect = Field<int>(renderer!, "_effectSettingsVersion");
            Invoke(chrome, "应用毛玻璃状态", state);
            RenderingRegression.Require(renderer!.WaitForIdle(2000) && Field<int>(renderer, "_frameVersion") == frame,
                "同源状态重应用不能丢失或重新生成当前帧。");
            renderer.ApplyParameters(chrome.BackdropBlurRadius, chrome.BackdropBlurPasses,
                chrome.BackdropDownsampleFactor, chrome.BackdropNoiseScale);
            RenderingRegression.Require(Field<int>(renderer, "_effectSettingsVersion") == effect,
                "相同参数不能重复失效效果缓存。");
            using var replacement = new Bitmap(24, 24);
            chrome.BackdropImage = replacement;
            RenderingRegression.Require(renderer.WaitForIdle(2000) && Field<int>(renderer, "_frameVersion") > frame,
                "不同图片必须生成新背景帧。");
            chrome.BackdropMode = ThisIsYourWindow.BackdropModeEnum.None;
            CheckCaptionPixels(form, chrome, state);
            chrome.Detach(form);
            ThisIsYourWindow? attachedOwner = null;
            RenderingRegression.Require(!ThisIsYourWindow.TryGetAttached(form, ref attachedOwner), "分离必须解除附加状态。");
            Console.WriteLine($"window init: Attach CPU={watch.Elapsed.TotalMilliseconds:F1}ms, layout updates={immediate}->{batched}; backdrop reuse/source replacement passed");
        }
        finally { D3D_PaintBridge.V5ProbeEnabled = enabled; }
    }

    private static void CheckCaptionPixels(Form form, ThisIsYourWindow chrome, ThisIsYourWindow.PerFormState state)
    {
        form.Show();
        D3D_V5Presentation.FlushPendingFrame(false);
        var caption = state.ChromeOverlays[0];
        D3D_V5Presentation.RequestRender(caption, default);
        var presenter = D3D_V5Presentation.GetPresenterForDiagnostics(caption);
        Rectangle updated = default;
        byte[] pixels = [];
        presenter.SurfaceProbe = (context, bitmap, region) =>
        {
            updated = region;
            pixels = RenderingRegression.Read(context, bitmap, region);
        };
        try
        {
            chrome.TitleTextPrivateProtocol = "局部标题回归";
            D3D_V5Presentation.FlushPendingFrame(false);
            RenderingRegression.Require(updated.Width > 0 && (long)updated.Width * updated.Height < (long)caption.ClientSize.Width * caption.ClientSize.Height,
                "标题文字变化必须实际缩小更新范围。");
            var surface = D3D_ControlSurfaceRegistry.RenderControl(caption, caption, default, null);
            var dirty = surface.LastRenderedDirtyRegion;
            dirty.Inflate(2, 2);
            dirty.Intersect(caption.ClientRectangle);
            RenderingRegression.Require(updated.Size == dirty.Size, "标题局部目标与请求矩形不一致。");
            var partial = pixels;
            D3D_V5Presentation.RequestRender(caption, default);
            var refCrop = RenderingRegression.Crop(pixels, caption.ClientSize.Width, dirty);
            RenderingRegression.Require(partial.SequenceEqual(refCrop), "局部标题绘制必须与完整绘制一致。");

            Invoke(chrome, "InvalidateCaptionButtons", form, 20, 20);
            D3D_V5Presentation.FlushPendingFrame(false);
            RenderingRegression.Require(updated.Width < caption.ClientSize.Width &&
                (long)updated.Width * updated.Height < (long)caption.ClientSize.Width * caption.ClientSize.Height / 2,
                "按钮悬停/按压失效必须限制到按钮区域。");
            var buttonSurface = D3D_ControlSurfaceRegistry.RenderControl(caption, caption, default, null);
            var buttonRegion = buttonSurface.LastRenderedDirtyRegion;
            buttonRegion.Inflate(2, 2);
            buttonRegion.Intersect(caption.ClientRectangle);
            RenderingRegression.Require(updated.Size == buttonRegion.Size, "按钮局部目标与请求矩形不一致。");
            partial = pixels;
            D3D_V5Presentation.RequestRender(caption, default);
            RenderingRegression.Require(partial.SequenceEqual(RenderingRegression.Crop(pixels, caption.ClientSize.Width, buttonRegion)),
                "局部按钮绘制必须与完整绘制一致。");
        }
        finally { presenter.SurfaceProbe = null; }
    }

    private static void CheckShadowStyle()
    {
        using var host = RenderingRegression.Host();
        using var shadow = new ShadowWindow();
        shadow.HostHandle = host.Handle;
        _ = shadow.Handle;
        var messages = new StyleMessages();
        messages.AssignHandle(shadow.Handle);
        try
        {
            shadow.UpdateHitTestTransparency();
            var initial = messages.Changes;
            for (var i = 0; i < 40; i++) shadow.UpdateHitTestTransparency();
            RenderingRegression.Require(messages.Changes == initial, "相同阴影命中样式不得反复写入。");
            shadow.ResizeWidth = 4;
            shadow.UpdateHitTestTransparency();
            RenderingRegression.Require(messages.Changes > initial, "命中模式变化必须更新原生样式。");
            var changed = messages.Changes;
            shadow.ResizeWidth = 6;
            shadow.UpdateHitTestTransparency();
            RenderingRegression.Require(messages.Changes == changed, "同为可命中时，热区宽度变化不需要重写窗口样式。");
            shadow.ResizeWidth = 0;
            shadow.UpdateHitTestTransparency();
            RenderingRegression.Require(messages.Changes > changed, "恢复穿透必须更新原生样式。");
            shadow.SyncVirtualDesktopWithHost();
            var manager = Field<object?>(shadow, "_桌面管理器");
            shadow.SyncVirtualDesktopWithHost();
            RenderingRegression.Require(manager == null || ReferenceEquals(manager, Field<object?>(shadow, "_桌面管理器")),
                "Shell 正常时应复用当前阴影窗口的桌面管理器。");
        }
        finally { messages.ReleaseHandle(); }
    }


    private sealed class StyleMessages : NativeWindow
    {
        internal int Changes;
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x7D) Changes++;
            base.WndProc(ref message);
        }
    }

}
