# LakeUI.Tests

`Program.Main` 执行 UltraDetailListView 复选框回归检查：`CheckBoxXXX` 属性与 ModernCheckBox 的默认值对应、`LakeUI - CheckBox` 分类、item/group/global 颜色继承、第一列布局和 ItemPadding、排除 BottomLines 的居中、模式开关、点击和空格键交互，以及 ItemCheck 的取消/改写和 ItemCheckedChanged 的触发顺序。

项目保留对 LakeUI 的引用及 WinForms 支持，需要 Windows 和 .NET 10 SDK。

```powershell
dotnet run --project LakeUI.Tests
```
