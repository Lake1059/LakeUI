using System.Drawing;
using System.Windows.Forms;
using LakeUI;

static partial class Program
{
    private static void CheckModernPanelTransparentMode()
    {
        using var panel = new ModernPanel
        {
            BackColor = Color.Transparent, BackColor1 = Color.Transparent,
            OverlayColor = Color.Transparent,
            TransparentRenderMode = ModernPanel.TransparentRenderModeEnum.SkipWhenFullyTransparent
        };
        RenderingRegression.Require(panel.IsTransparentNoRenderActive && !D3D_V5Presentation.IsV5Control(panel), "透明面板必须退出自身呈现。");
        using var child = new ModernButton();
        panel.Controls.Add(child);
        RenderingRegression.Require(D3D_V5Presentation.IsV5Control(child), "透明面板不能禁用子控件呈现。");
        panel.BorderSize = 8;
        panel.ScrollBarMode = ModernPanel.ScrollMode.Both;
        using var image = new Bitmap(2, 2);
        panel.Image = image;
        RenderingRegression.Require(panel.IsTransparentNoRenderActive, "图片、边框、滚动条均服从面板无渲染模式。");
        panel.Image = null;
        foreach (var name in new[] { nameof(panel.BackColor), nameof(panel.BackColor1), nameof(panel.OverlayColor) })
        {
            var property = typeof(ModernPanel).GetProperty(name)!;
            property.SetValue(panel, Color.FromArgb(1, 1, 2, 3));
            RenderingRegression.Require(!panel.IsTransparentNoRenderActive && D3D_V5Presentation.IsV5Control(panel), $"{name} 必须恢复呈现。");
            property.SetValue(panel, Color.Transparent);
            RenderingRegression.Require(panel.IsTransparentNoRenderActive, $"{name} 必须恢复无渲染模式。");
        }
        panel.TransparentRenderMode = ModernPanel.TransparentRenderModeEnum.Normal;
        RenderingRegression.Require(D3D_V5Presentation.IsV5Control(panel), "默认模式必须保留原渲染路径。");
    }

    private static void CheckDependencyDirtyMapping()
    {
        using var host = new Panel { Size = new Size(240, 180) };
        using var source = new Panel { Location = new Point(10, 10), Size = new Size(100, 80) };
        using var consumer = new Panel { Location = new Point(15, 15), Size = new Size(100, 80) };
        host.Controls.AddRange([source, consumer]);
        _ = host.Handle; _ = source.Handle; _ = consumer.Handle;
        var mapped = Rectangle.Empty;
        RenderingRegression.Require(D3D_ControlSurfaceRegistry.TryMapDependencyDirtyRectangle(consumer, source,
            new RectangleF(0, 0, 100, 80), new Rectangle(10, 10, 20, 20), ref mapped) &&
            mapped == new Rectangle(5, 5, 20, 20), "来源脏区必须按采样坐标映射。");
        RenderingRegression.Require(D3D_ControlSurfaceRegistry.TryMapDependencyDirtyRectangle(consumer, source,
            new RectangleF(12, 12, 4, 5), new Rectangle(10, 10, 20, 20), ref mapped) &&
            mapped == new Rectangle(7, 7, 4, 5), "必须裁剪到实际采样区域。");
        using var missing = new Panel();
        RenderingRegression.Require(!D3D_ControlSurfaceRegistry.TryMapDependencyDirtyRectangle(consumer, missing,
            RectangleF.Empty, new Rectangle(0, 0, 5, 5), ref mapped), "缺少句柄必须回退且不得创建句柄。");
        RenderingRegression.Require(!missing.IsHandleCreated, "映射不应主动创建句柄。");
    }
}
