using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using LakeUI;

internal static class RenderBatchRegressionTests
{
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly FieldInfo BatchState = typeof(D3D_V5Presentation).GetField("_renderBatch", StaticPrivate)!;
    private static readonly MethodInfo FlushBatch = typeof(D3D_V5Presentation).GetMethod("FlushBatchedRenders", StaticPrivate)!;

    internal static void Run()
    {
        CheckOrderingAndReuse();
        CheckNestedFlush(false);
        CheckNestedFlush(true);
        Console.WriteLine("render batch order/reuse/reentrant flush/reference cleanup passed");
    }

    private static void CheckOrderingAndReuse()
    {
        using var form = RenderingRegression.Host();
        var calls = new List<string>();
        using var parent = new BatchControl("parent", calls);
        using var child = new BatchControl("child", calls) { Location = new Point(4, 4) };
        using var first = new BatchControl("first", calls) { Left = 110 };
        using var second = new BatchControl("second", calls) { Left = 210 };
        parent.Controls.Add(child);
        form.Controls.AddRange([parent, first, second]);
        form.Show();
        D3D_V5Presentation.FlushPendingFrame(false);
        calls.Clear();

        Queue(child, second, parent, first);
        var state = State();
        var batch = Field<object>(state, "Batch");
        D3D_V5Presentation.FlushPendingFrame(false);
        ExpectOrder(calls, "second", "parent", "first", "child");
        RequireCleared(state);

        calls.Clear();
        Queue(child, first, parent, second);
        D3D_V5Presentation.FlushPendingFrame(false);
        ExpectOrder(calls, "first", "parent", "second", "child");
        RenderingRegression.Require(ReferenceEquals(batch, Field<object>(state, "Batch")), "后续批次必须复用原有缓冲。");
        RequireCleared(state);
    }

    private static void CheckNestedFlush(bool prepareSurfaces)
    {
        using var form = RenderingRegression.Host();
        var calls = new List<string>();
        using var first = new BatchControl("first", calls);
        using var second = new BatchControl("second", calls) { Left = 110 };
        using var deferred = new BatchControl("deferred", calls) { Left = 210 };
        form.Controls.AddRange([first, second, deferred]);
        form.Show();
        D3D_V5Presentation.FlushPendingFrame(false);
        calls.Clear();
        object? state = null;
        var entered = false;
        first.OnRender = () =>
        {
            if (entered) return;
            entered = true;
            Queue(deferred);
            RenderingRegression.Require(Field<bool>(state!, "Flushing"), "RenderGpu 期间必须保持批次门闩。");
            FlushBatch.Invoke(null, [state, false]);
            RenderingRegression.Require(calls.SequenceEqual(["first"]), "嵌套计时器 flush 不得打断外层批次。");
            RenderingRegression.Require(((IDictionary)Field<object>(state!, "Pending")).Contains(deferred), "嵌套请求必须保留到下一批。");
        };

        Queue(first, second);
        state = State();
        D3D_V5Presentation.FlushPendingFrame(prepareSurfaces);
        RenderingRegression.Require(entered, "测试必须实际进入 RenderGpu 重入路径。");
        ExpectOrder(calls, "first", "second");
        RenderingRegression.Require(((IDictionary)Field<object>(state, "Pending")).Contains(deferred), "外层完成前不得消费新批次。");
        RequireCleared(state);

        first.OnRender = null;
        D3D_V5Presentation.FlushPendingFrame(false);
        ExpectOrder(calls, "first", "second", "deferred");
        RenderingRegression.Require(((IDictionary)Field<object>(state, "Pending")).Count == 0, "下一批必须消费重入请求。");
        RequireCleared(state);
    }

    private static void Queue(params Control[] controls)
    {
        foreach (var control in controls) D3D_V5Presentation.RequestRenderBatched(control, control.ClientRectangle, true);
    }

    private static object State() => BatchState.GetValue(null) ?? throw new InvalidOperationException("批次状态未创建。");
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Fields)!.GetValue(target)!;
    private static void ExpectOrder(List<string> actual, params string[] expected) =>
        RenderingRegression.Require(actual.SequenceEqual(expected), $"批次顺序不符：expected={string.Join(",", expected)}, actual={string.Join(",", actual)}。");

    private static void RequireCleared(object state)
    {
        var batch = (IList)Field<object>(state, "Batch");
        RenderingRegression.Require(batch.Count == 0 && !Field<bool>(state, "Flushing"), "已完成批次必须清空并解除门闩。");
        // Count=0 不足以证明没有滞留：确认 List.Clear 清除了已用 backing slots 中的 Control key。
        var slots = (Array)batch.GetType().GetField("_items", Fields)!.GetValue(batch)!;
        foreach (var slot in slots)
            RenderingRegression.Require(slot!.GetType().GetProperty("Key")!.GetValue(slot) is null, "复用批次缓冲不得保留已完成的 Control 引用。");
    }

    private sealed class BatchControl(string id, List<string> calls) : Control, D3D_IGpuRenderable, V5_IGpuPresentationSource
    {
        internal Action? OnRender;
        protected override Size DefaultSize => new(90, 70);
        public void RenderGpu(D3D_PaintContext context)
        {
            calls.Add(id);
            OnRender?.Invoke();
            context.FillRectangle(ClientRectangle, Color.Black);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }
    }
}
