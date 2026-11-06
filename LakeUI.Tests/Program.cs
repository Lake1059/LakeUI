using LakeUI;

internal static partial class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            UltraDetailListViewTests.Run();
            CheckModernPanelTransparentMode();
            CheckDependencyDirtyMapping();
            RenderingRegression.Run();
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
