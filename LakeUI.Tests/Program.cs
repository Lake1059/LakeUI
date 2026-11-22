using LakeUI;

internal static partial class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args is ["--designer-preview", var backend])
            {
                DesignerPreviewTests.Run(backend);
                return 0;
            }

            if (args is ["--first-display", var scenario])
            {
                FirstFrameDisplayTests.Run(scenario);
                return 0;
            }

            GlobalOptions.SetRenderBackendAsync(RenderBackend.Direct3D11).GetAwaiter().GetResult();
            UltraDetailListViewTests.Run();
            CheckModernPanelTransparentMode();
            CheckDependencyDirtyMapping();
            RenderingRegression.Run();
            LowCostOptimizationTests.Run();
            RenderBatchRegressionTests.Run();
            ChartCacheRegressionTests.Run();
            Console.WriteLine("LakeUI regression checks passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
