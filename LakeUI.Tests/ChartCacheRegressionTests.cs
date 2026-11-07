using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using LakeUI;

internal static class ChartCacheRegressionTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private sealed record SegmentSnapshot(string?[] Keys, Delegate?[] Factories);

    internal static void Run()
    {
        CheckStableKeysAndGeometryRebuild();
    }

    private static void CheckStableKeysAndGeometryRebuild()
    {
        using var firstForm = RenderingRegression.Host();
        using var secondForm = RenderingRegression.Host();
        using var first = CreateChart();
        using var second = CreateChart();
        first.AutoRefresh = false;
        second.AutoRefresh = false;
        firstForm.Controls.Add(first);
        secondForm.Controls.Add(second);
        firstForm.Show();
        secondForm.Show();

        var firstCompositor = D3D_RenderCore.GetWindowCompositor(firstForm);
        var secondCompositor = D3D_RenderCore.GetWindowCompositor(secondForm);
        RenderAndRead(first);
        RenderAndRead(second);

        var firstPrefix = Field<string>(first, "_geometryCachePrefix");
        var secondPrefix = Field<string>(second, "_geometryCachePrefix");
        RenderingRegression.Require(!StringComparer.Ordinal.Equals(firstPrefix, secondPrefix),
            "同版本图表必须使用互不重叠的 geometry 前缀。");
        var firstEntryCount = CountPrefix(firstCompositor.GeometryCache, firstPrefix);
        var secondEntryCount = CountPrefix(secondCompositor.GeometryCache, secondPrefix);
        RenderingRegression.Require(firstEntryCount > 0 && secondEntryCount > 0,
            "首次 GPU 呈现必须实际创建折线几何缓存。");

        var firstSnapshot = CaptureSegments(first);
        var layout = Field<object>(first, "_layoutCache");
        var prefix = firstPrefix;
        long allocatedStart = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 8; i++) RenderWithoutReadback(first);
        var stableAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedStart;
        var stableSnapshot = CaptureSegments(first);
        RenderingRegression.Require(ReferenceEquals(layout, Field<object>(first, "_layoutCache")) &&
            ReferenceEquals(prefix, Field<string>(first, "_geometryCachePrefix")),
            "稳定重绘必须复用布局和 geometry 前缀对象。");
        RequireSameReferences(firstSnapshot.Keys, stableSnapshot.Keys, "稳定重绘不能重建 geometry key 字符串。");
        RequireSameReferences(firstSnapshot.Factories, stableSnapshot.Factories, "稳定重绘不能重建 geometry factory 委托。");
        RenderingRegression.Require(CountPrefix(firstCompositor.GeometryCache, prefix) == firstEntryCount,
            "稳定重绘必须命中已有 geometry，而不是额外创建缓存项。");

        var beforeEviction = RenderAndRead(first);
        firstCompositor.GeometryCache.ReleaseByPrefix(prefix);
        RenderingRegression.Require(CountPrefix(firstCompositor.GeometryCache, prefix) == 0,
            "显式淘汰必须释放图表前缀下的几何。");
        var afterEviction = RenderAndRead(first);
        RenderingRegression.Require(CountPrefix(firstCompositor.GeometryCache, prefix) == firstEntryCount,
            "被预算淘汰的几何必须通过缓存 factory 重建。");
        RenderingRegression.Require(beforeEviction.SequenceEqual(afterEviction),
            "几何缓存淘汰并重建后必须保持相同的 GPU 输出像素。");
        RenderingRegression.Require(CountPrefix(secondCompositor.GeometryCache, secondPrefix) == secondEntryCount,
            "淘汰一个图表的前缀不得影响同 compositor 外的另一图表。");

        first.Size = new Size(first.Width + 37, first.Height + 19);
        var resizedPixels = RenderAndRead(first);
        var resizedPrefix = Field<string>(first, "_geometryCachePrefix");
        RenderingRegression.Require(!StringComparer.Ordinal.Equals(prefix, resizedPrefix),
            "图表宽高变化必须使用新 geometry key 前缀。");
        RenderingRegression.Require(CountPrefix(firstCompositor.GeometryCache, prefix) == 0 &&
            CountPrefix(firstCompositor.GeometryCache, resizedPrefix) == firstEntryCount,
            "尺寸变化必须释放旧尺寸几何并创建新尺寸几何。");
        RenderingRegression.Require(resizedPixels.Length == first.Width * first.Height * 4,
            "尺寸变化后的 GPU 表面必须使用新布局大小。");

        var resizedSnapshot = CaptureSegments(first);
        secondForm.Controls.Add(first);
        RenderAndRead(first);
        RenderingRegression.Require(CountPrefix(firstCompositor.GeometryCache, resizedPrefix) == 0,
            "控件移入另一窗体后必须释放旧 compositor 中的几何。");
        RenderingRegression.Require(CountPrefix(secondCompositor.GeometryCache, resizedPrefix) == firstEntryCount,
            "控件移入另一窗体后必须在新 compositor 中重建几何。");
        RenderingRegression.Require(CountPrefix(secondCompositor.GeometryCache, secondPrefix) == secondEntryCount,
            "迁移图表不能删除同 compositor 中另一图表的几何。");
        RenderingRegression.Require(resizedSnapshot.Keys.Length > 0,
            "测试必须覆盖真实折线段。");

        Console.WriteLine($"chart geometry cache: stable 8 redraws allocated {stableAllocatedBytes} B; eviction rebuild pixels matched; resize and compositor isolation passed");
    }

    private static Ultra2DChart CreateChart()
    {
        var chart = new Ultra2DChart
        {
            Size = new Size(220, 150),
            BackColor = Color.White,
            ShowHorizontalGridLines = false,
            ShowVerticalGridLines = false,
            LegendPosition = Ultra2DChart.LegendPositionEnum.None
        };
        var firstValues = Enumerable.Range(0, 48).Select(i => 20.0 + Math.Sin(i * 0.21) * 13.0 + i * 0.12).ToArray();
        var secondValues = Enumerable.Range(0, 48).Select(i => 45.0 - Math.Cos(i * 0.17) * 11.0 - i * 0.08).ToArray();
        var firstSeries = chart.AddSeries("first", Ultra2DChart.ChartSeriesTypeEnum.Line, firstValues);
        firstSeries.Color = Color.Crimson;
        firstSeries.MarkerShape = Ultra2DChart.MarkerShapeEnum.None;
        var secondSeries = chart.AddSeries("second", Ultra2DChart.ChartSeriesTypeEnum.Line, secondValues);
        secondSeries.Color = Color.DodgerBlue;
        secondSeries.MarkerShape = Ultra2DChart.MarkerShapeEnum.None;
        return chart;
    }

    private static byte[] RenderAndRead(Ultra2DChart chart)
    {
        D3D_V5Presentation.RequestRender(chart, chart.ClientRectangle);
        var presenter = D3D_V5Presentation.GetPresenterForDiagnostics(chart);
        RenderingRegression.Require(presenter is not null, "图表必须实际创建 GPU presenter。");
        Rectangle region = default;
        byte[] pixels = [];
        presenter!.SurfaceProbe = (context, bitmap, updated) =>
        {
            region = updated;
            pixels = RenderingRegression.Read(context, bitmap, updated);
        };
        try
        {
            D3D_V5Presentation.RequestRender(chart, chart.ClientRectangle);
            D3D_V5Presentation.FlushPendingFrame(false);
            RenderingRegression.Require(region.Size == chart.ClientSize,
                "参考帧必须读回完整图表表面。");
            return pixels;
        }
        finally
        {
            presenter.SurfaceProbe = null;
        }
    }

    private static void RenderWithoutReadback(Ultra2DChart chart)
    {
        D3D_V5Presentation.RequestRender(chart, chart.ClientRectangle);
        D3D_V5Presentation.FlushPendingFrame(false);
    }

    private static SegmentSnapshot CaptureSegments(Ultra2DChart chart)
    {
        var layout = Field<object>(chart, "_layoutCache");
        var seriesDraws = (IEnumerable)Field<object>(layout, "SeriesDraws");
        var keys = new List<string?>();
        var factories = new List<Delegate?>();
        foreach (var draw in seriesDraws)
        {
            var segments = (IEnumerable)Field<object>(draw!, "LineSegments");
            foreach (var segment in segments)
            {
                keys.Add((string?)Field<object?>(segment!, "GeometryKey"));
                factories.Add((Delegate?)Field<object?>(segment!, "GeometryFactory"));
            }
        }
        RenderingRegression.Require(keys.Count > 0 && keys.All(key => !string.IsNullOrEmpty(key)),
            "布局中的每条折线都必须已有预生成的 geometry key。");
        return new SegmentSnapshot(keys.ToArray(), factories.ToArray());
    }

    private static int CountPrefix(D3D_GeometryCache cache, string prefix)
    {
        var entries = (IDictionary)cache.GetType().GetField("_geometries", InstancePrivate)!.GetValue(cache)!;
        var count = 0;
        foreach (string key in entries.Keys)
            if (key.StartsWith(prefix, StringComparison.Ordinal)) count++;
        return count;
    }

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, InstanceFields)!.GetValue(target)!;

    private static void RequireSameReferences<T>(T?[] expected, T?[] actual, string message) where T : class
    {
        RenderingRegression.Require(expected.Length == actual.Length, message);
        for (var i = 0; i < expected.Length; i++)
            RenderingRegression.Require(ReferenceEquals(expected[i], actual[i]), message);
    }
}
