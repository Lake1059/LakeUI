using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using LakeUI;

static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            CheckDefaults();
            CheckColorInheritance();
            CheckLayout();
            CheckInteraction();
            CheckEvents();
            Console.WriteLine("UltraDetailListView checkbox regression checks passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static object Call(object target, string name, params object?[] args) =>
        typeof(UltraDetailListView).GetMethod(name, Private)!.Invoke(target, args)!;

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, Private | BindingFlags.Public)!.GetValue(target)!;

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Near(float actual, float expected, string message) =>
        Assert(Math.Abs(actual - expected) < 0.02f, $"{message}: {actual} != {expected}");

    private static TestList CreateList()
    {
        var list = new TestList
        {
            Size = new Size(520, 620), HeaderVisible = false, ContentPadding = Padding.Empty,
            ItemPadding = new Padding(11, 7, 9, 13), WordWrap = false, AnimationDuration = 0
        };
        list.Columns.Add(new UltraDetailListView.ListColumn { Width = 220 });
        list.Columns.Add(new UltraDetailListView.ListColumn { Width = 180 });
        return list;
    }

    private static void CheckDefaults()
    {
        using var list = CreateList();
        using var check = new ModernCheckBox();
        string[] names = ["BoxSize", "BoxBorderRadius", "BoxBorderSize", "BoxBorderColor",
            "BoxTextSpacing", "BoxInnerPadding", "BoxCheckedBackColor", "BoxUncheckedBackColor",
            "CheckMarkColor", "CheckMarkWidth", "HoverCheckedBackColor", "HoverUncheckedBackColor",
            "HoverBoxBorderColor", "HoverCheckMarkColor", "PressedCheckedBackColor",
            "PressedUncheckedBackColor", "PressedBoxBorderColor", "PressedCheckMarkColor",
            "DisabledOverlayColor", "ClickAnywhere"];
        foreach (var name in names)
        {
            var renamed = name switch
            {
                "HoverBoxBorderColor" => "CheckBoxHoverBorderColor",
                "PressedBoxBorderColor" => "CheckBoxPressedBorderColor",
                _ => "CheckBox" + (name.StartsWith("Box") ? name[3..] : name)
            };
            var actual = TypeDescriptor.GetProperties(list)[renamed]!;
            var original = TypeDescriptor.GetProperties(check)[name]!;
            Assert(actual.PropertyType == original.PropertyType && Equals(actual.GetValue(list), original.GetValue(check)),
                $"ModernCheckBox property default differs: {name}");
            Assert(actual.IsBrowsable, $"Property must be available in designer: {renamed}");
            Assert(actual.Category == "LakeUI - CheckBox", $"Checkbox category differs: {renamed}");
        }
        Assert(!list.CheckBoxVisible, "Checkbox mode must be opt-in.");
        foreach (var model in new object[] { new UltraDetailListView.ListItem(), new UltraDetailListView.ListGroup() })
        {
            var properties = TypeDescriptor.GetProperties(model);
            Assert(properties["CheckBoxCheckedBackColor"]!.IsBrowsable, "Checked override must be editable.");
            Assert(properties["CheckBoxSize"] is null && properties["CheckBoxBorderColor"] is null &&
                properties["CheckBoxUncheckedBackColor"] is null && properties["CheckBoxTextSpacing"] is null,
                "Unchecked colors and geometry must stay global.");
        }
    }

    private static void CheckColorInheritance()
    {
        using var list = CreateList();
        var group = new UltraDetailListView.ListGroup("g");
        var item = new UltraDetailListView.ListItem(new UltraDetailListView.ListSubItem("grouped")) { GroupName = "g" };
        list.Groups.Add(group);
        list.Items.Add(item);
        foreach (var name in new[] { "CheckBoxCheckedBackColor", "CheckBoxCheckMarkColor", "CheckBoxHoverCheckedBackColor",
            "CheckBoxPressedCheckedBackColor", "CheckBoxHoverCheckMarkColor", "CheckBoxPressedCheckMarkColor" })
        {
            var property = typeof(UltraDetailListView.CheckBoxCheckedAppearance).GetProperty(name)!;
            Func<UltraDetailListView.CheckBoxCheckedAppearance, Color> selector = a => (Color)property.GetValue(a)!;
            Color Resolve() => (Color)Call(list, "获取继承勾选颜色", item, group, selector, Color.Blue);
            Assert(Resolve() == Color.Blue, $"Global fallback failed: {name}");
            property.SetValue(group, Color.Green);
            Assert(Resolve() == Color.Green, $"Group fallback failed: {name}");
            property.SetValue(item, Color.Red);
            Assert(Resolve() == Color.Red, $"Item priority failed: {name}");
            property.SetValue(item, Color.Transparent);
            Assert(Resolve() == Color.Transparent, $"Transparent is an override, not inheritance: {name}");
            property.SetValue(item, Color.Empty);
            Assert(Resolve() == Color.Green, $"Reset to group failed: {name}");
            property.SetValue(group, Color.Empty);
        }
        var rows = (IList)typeof(UltraDetailListView).GetField("_displayRows", Private)!.GetValue(list)!;
        Assert(ReferenceEquals(Field<UltraDetailListView.ListGroup>(rows[1]!, "Group"), group),
            "Grouped item rows must retain their group for color resolution.");
        group.IsCollapsed = true;
        list.RefreshItems();
        item.Checked = true;
        Assert(list.CheckedItems.Contains(item), "Collapsed checked items must retain their state.");
        group.IsCollapsed = false;
        list.RefreshItems();
        Assert(item.Checked, "Group expansion must not reset checks.");
    }

    private static RectangleF Box(TestList list, UltraDetailListView.ListItem item, int row = 0) =>
        (RectangleF)Call(list, "获取项复选框区域", item, (int)Call(list, "获取行Y坐标", row));

    private static void CheckLayout()
    {
        using var list = CreateList();
        using var icon = new Bitmap(32, 32);
        var item = new UltraDetailListView.ListItem(new UltraDetailListView.ListSubItem("first"),
            new UltraDetailListView.ListSubItem("second")) { Icon = icon };
        list.Items.Add(item);
        var plainHeight = (int)Call(list, "计算项高度", item);
        var plainPrefix = (int)Call(list, "获取首列前缀宽度", item);
        list.CheckBoxVisible = true;
        var box = Box(list, item);
        var scale = (float)Call(list, "DpiScale");
        var padding = (Padding)typeof(UltraDetailListView).GetMethod("Dpi", Private, [typeof(Padding)])!.Invoke(list, [list.ItemPadding])!;
        var firstX = (int[])typeof(UltraDetailListView).GetField("_columnXCache", Private)!.GetValue(list)!;
        Near(box.Left, firstX[0] + padding.Left + list.CheckBoxBorderSize * scale / 2, "ItemPadding.Left must position box outer edge");
        var upperHeight = Field<int>(item, "CachedUpperPartHeight");
        Near(box.Top + box.Height / 2, (int)Call(list, "获取行Y坐标", 0) + padding.Top + upperHeight / 2f,
            "Box must center on upper content");
        Assert(firstX[1] - firstX[0] == 220 && list.Columns[0].Width == 220 && list.Columns[1].Width == 180,
            "Checkbox must not move column boundaries.");
        item.BottomLines.Add(new UltraDetailListView.TextLine("bottom line one"));
        item.BottomLines.Add(new UltraDetailListView.TextLine("bottom line two"));
        item.InvalidateCache();
        list.RefreshItems();
        Near(Box(list, item).Top, box.Top, "BottomLines must not move checkbox center");
        var bottomHeight = Field<int>(item, "CachedBottomLinesHeight");
        Assert(bottomHeight > 0, "BottomLines fixture must add height.");
        var prefix = (int)Call(list, "获取首列前缀宽度", item);
        list.CheckBoxTextSpacing += 10;
        Assert((int)Call(list, "获取首列前缀宽度", item) - prefix == (int)Math.Ceiling(10 * scale),
            "CheckBoxTextSpacing must participate in first-column layout.");
        list.CheckBoxSize = 48;
        Assert(Field<int>(item, "CachedUpperPartHeight") >= (int)Math.Ceiling(49 * scale), "Large box must fit upper content.");
        list.CheckBoxVisible = false;
        Assert((int)Call(list, "获取首列前缀宽度", item) == plainPrefix, "Hiding checkbox must restore prefix width.");
        Assert((int)Call(list, "计算项高度", item) == plainHeight + bottomHeight, "Hiding checkbox must restore upper height.");
        list.CheckBoxVisible = true;
        list.Columns[0].Width = 20;
        var clip = (RectangleF)Call(list, "获取复选框裁剪区域", item, (int)Call(list, "获取行Y坐标", 0));
        Assert(clip.Right <= firstX[0] + 20, "Checkbox clip must remain inside first column.");
        list.Items.Clear();
        var empty = new UltraDetailListView.ListItem();
        list.Items.Add(empty);
        Assert(Box(list, empty).Height == 48 * scale, "Empty items must still display checkbox.");
    }

    private static void CheckInteraction()
    {
        using var list = CreateList();
        list.CheckBoxVisible = true;
        var item = new UltraDetailListView.ListItem(new UltraDetailListView.ListSubItem("click"));
        list.Items.Add(item);
        int changes = 0;
        list.ItemCheckedChanged += (_, e) => { Assert(ReferenceEquals(e.Item, item), "Wrong event item."); changes++; };
        // Creating a native handle exercises mouse capture and cancellation without opening a window.
        _ = list.Handle;
        var box = Box(list, item);
        var point = Point.Round(new PointF(box.Left + box.Width / 2, box.Top + box.Height / 2));
        list.Down(point);
        list.Up(point);
        Assert(item.Checked && changes == 1, "Checkbox click must toggle once and raise one event.");
        list.Down(point);
        list.Up(new Point(180, point.Y));
        Assert(item.Checked && changes == 1, "Release outside checkbox must cancel.");
        list.Down(point);
        list.Capture = false;
        list.Up(point);
        Assert(item.Checked && changes == 1, "Lost capture must cancel check click.");
        item.Checked = false;
        list.CheckBoxClickAnywhere = true;
        list.Down(new Point(180, point.Y));
        list.Up(new Point(180, point.Y));
        Assert(item.Checked && changes == 3, "CheckBoxClickAnywhere must toggle row clicks.");
        list.Enabled = false;
        list.Down(point);
        list.Up(point);
        Assert(item.Checked && changes == 3, "Disabled list must not toggle.");
        list.Enabled = true;
        item.Selected = true;
        list.Key(Keys.Space);
        Assert(!item.Checked && changes == 4, "Space must toggle selected items.");
        list.CheckBoxVisible = false;
        list.Key(Keys.Space);
        Assert(!item.Checked && changes == 4, "Hidden checkbox mode must not handle check toggles.");
        list.Items.Remove(item);
        item.Checked = true;
        Assert(changes == 4, "Detached items must not notify former owner.");
    }

    private sealed class TestList : UltraDetailListView
    {
        public void Down(Point pt) => OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, pt.X, pt.Y, 0));
        public void Up(Point pt) => OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, pt.X, pt.Y, 0));
        public void Key(Keys key) => OnKeyDown(new KeyEventArgs(key));
    }

    private static void CheckEvents()
    {
        using var list = CreateList();
        var item = new UltraDetailListView.ListItem(new UltraDetailListView.ListSubItem("events"));
        list.Items.Add(item);
        var events = new List<string>();
        bool cancel = true;
        bool replace = false;
        list.ItemCheck += (_, e) =>
        {
            Assert(e.CurrentValue == item.Checked, "ItemCheck must run before state changes.");
            Assert(e.DisplayRowIndex == 0 && e.ColumnIndex == 0, "Check event must identify first-column item.");
            events.Add("before");
            e.Cancel = cancel;
            if (replace) e.NewValue = e.CurrentValue;
            // A same-item recursive change must not recurse or emit extra events.
            item.Checked = e.NewValue;
        };
        list.ItemCheckedChanged += (_, e) =>
        {
            Assert(item.Checked && ReferenceEquals(e.Item, item), "Changed event must observe committed state.");
            events.Add("after");
        };
        item.Checked = true;
        Assert(!item.Checked && events.SequenceEqual(["before"]), "Cancel must prevent change and after event.");
        events.Clear();
        cancel = false;
        replace = true;
        item.Checked = true;
        Assert(!item.Checked && events.SequenceEqual(["before"]), "Replacing target with current value must suppress after event.");
        events.Clear();
        replace = false;
        item.Checked = true;
        Assert(item.Checked && events.SequenceEqual(["before", "after"]), "Check events must fire in order.");
        item.Checked = true;
        Assert(events.Count == 2, "Setting the current state must not emit events.");
    }
}
