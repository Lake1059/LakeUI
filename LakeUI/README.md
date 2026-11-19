## 湖界 LakeUI - NuGet

LakeUI 是专为 WinForms 项目设计的一套精致交互控件，以我的昵称命名，官方中文名称 “湖界”。主打简单易用、精致美观、高度定制、高效性能，新手强推。

### 选择 Visual Studio 设计器的渲染后端

在使用 LakeUI 的 WinForms 项目 `.vbproj` 或 `.csproj` 中添加以下配置。

```xml
<PropertyGroup>
  <LakeUIDesignerBackend>DX12</LakeUIDesignerBackend>
</PropertyGroup>
```

可选值为 `DX11` 和 `DX12`。项目省略该属性时，设计器使用 `DX11`。保存项目文件后，关闭并重新打开解决方案，让 VS 启动新的设计器进程并读取配置。

通过 NuGet 安装 LakeUI 时会自动导入配置支持。通过 `ProjectReference` 引用 LakeUI 源码的项目需要导入 `LakeUI/buildTransitive/LakeUI.targets`，可参考仓库中的 `LakeUI.Demo.vbproj`。

GitHub：https://github.com/Lake1059/LakeUI

LakeUI 是收费软件，要在任何公开的项目中使用，请查阅 GitHub README 和销售平台以选择并购买合适的许可证。企业请联系以定制订阅价格。