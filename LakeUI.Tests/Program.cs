using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LakeUI;

static class Program
{
    private const int WmSysCommand = 0x0112;
    private const int ScRestore = 0xF120;
    private const int ScMaximize = 0xF030;
    private const int DwmaExtendedFrameBounds = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr first, IntPtr second);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr window);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out NativeRect value, int size);

    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        VerifyStartupMaximizeUsesWorkingArea();
        Console.WriteLine("ThisIsYourWindow maximize geometry regression test passed.");
    }

    private static void VerifyStartupMaximizeUsesWorkingArea()
    {
        using var chrome = new ThisIsYourWindow();
        using var form = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Bounds = new Rectangle(120, 120, 800, 500),
            ShowInTaskbar = false
        };

        form.Load += (_, _) => chrome.Attach(form);
        form.Show();
        Application.DoEvents();

        var workingArea = Screen.FromHandle(form.Handle).WorkingArea;
        for (var cycle = 0; cycle < 3; cycle++)
        {
            SendMessage(form.Handle, WmSysCommand, new IntPtr(ScMaximize), IntPtr.Zero);
            Application.DoEvents();
            Assert(IsZoomed(form.Handle), $"Maximize cycle {cycle + 1} must enter the native maximized state.");
            AssertVisibleFrameMatchesWorkingArea(form, workingArea, cycle + 1);

            SendMessage(form.Handle, WmSysCommand, new IntPtr(ScRestore), IntPtr.Zero);
            Application.DoEvents();
            Assert(!IsZoomed(form.Handle), $"Restore cycle {cycle + 1} must leave the native maximized state.");
        }

        chrome.Detach(form);
    }

    private static void AssertVisibleFrameMatchesWorkingArea(Form form, Rectangle workingArea, int cycle)
    {
        var result = DwmGetWindowAttribute(form.Handle, DwmaExtendedFrameBounds,
            out var nativeFrame, Marshal.SizeOf<NativeRect>());
        Assert(result == 0, "DWM must provide the visible frame bounds for the test window.");
        var actual = nativeFrame.ToRectangle();
        Assert(actual.Left <= workingArea.Left && actual.Top <= workingArea.Top &&
               actual.Right >= workingArea.Right && actual.Bottom >= workingArea.Bottom &&
               actual.Width - workingArea.Width <= 20 && actual.Height - workingArea.Height <= 20,
            $"Maximize cycle {cycle} must cover the working area without extending beyond the native resize-frame inset. Expected {workingArea}; got {actual}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
