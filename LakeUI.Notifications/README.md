# LakeUI.Notifications

> 本文件由 GPT-5.5 编写

`LakeUI.Notifications` 是 LakeUI 面向 WinForms / VB / C# 桌面程序封装的 Windows 10/11 通知库。内部使用 Windows App SDK 的 `Microsoft.Windows.AppNotifications`，外部只暴露普通 .NET 类型，适合让 VB 主程序通过 NuGet 引用，而不是直接处理 WinRT 通知对象。推荐以 NuGet 包引用本项目，因为 Windows App SDK 还需要运行时依赖、构建属性和相关资源参与发布流程。

`LakeUI.Notifications` 不依赖 `LakeUI` 主体，也不包含在主体的付费计划中，所以是一个可免费商用的扩展包，尽管其许可证是 GPL-3.0，但商用无需问我，直接用就完事了，也不需要遵守许可证的开源要求。毕竟这个扩展包完全是 AI 写的，收费有点说不过去了。

## 环境要求

- 主程序目标框架必须是 Windows TFM，例如 `net10.0-windows10.0.17763.0`。
- 目标系统建议为 Windows 10 1809 及以上或 Windows 11。
- 目标机器需要安装对应架构的 Windows App Runtime 2.1.3。
- 未打包 WinForms 程序会在库内部执行 Windows App Runtime Bootstrap 初始化。
- VB 主程序需要关闭 Windows App SDK 自动初始化，本包会通过 `buildTransitive\LakeUI.Notifications.props` 自动设置。

## 引用方式

把 `LakeUI.Notifications.*.nupkg` 放到本地 NuGet 源，或者直接放入解决方案使用的本地包目录，然后在 WinForms 主项目中引用：

```xml
<PackageReference Include="LakeUI.Notifications" Version="1.0.0" />
```

主项目建议显式写平台版本：

```xml
<TargetFramework>net10.0-windows10.0.17763.0</TargetFramework>
```

## 项目文件 XML 配置

只要通过 NuGet 引用 `LakeUI.Notifications`，主项目通常只需要保留最基础的 WinForms 配置：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.17763.0</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <WindowsPackageType>None</WindowsPackageType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="LakeUI.Notifications" Version="1.0.0" />
  </ItemGroup>
</Project>
```

本包会自动导入 `buildTransitive\LakeUI.Notifications.props`，为 VB/WinForms 主项目关闭 Windows App SDK 的自动初始化和自动生成内容：

```xml
<WindowsAppSdkBootstrapInitialize>false</WindowsAppSdkBootstrapInitialize>
<WindowsAppSdkDeploymentManagerInitialize>false</WindowsAppSdkDeploymentManagerInitialize>
<WindowsAppSdkAutoInitialize>false</WindowsAppSdkAutoInitialize>
<WindowsAppSdkIncludeVersionInfo>false</WindowsAppSdkIncludeVersionInfo>
<WindowsAppSdkUndockedRegFreeWinRTInitialize>false</WindowsAppSdkUndockedRegFreeWinRTInitialize>
```

这些属性是必要的，原因是本库会在代码中手动执行 Bootstrap 初始化；如果让 Windows App SDK 自动初始化，VB 项目可能会拿到 C# 生成文件或重复初始化逻辑，导致编译或运行问题。正常使用 NuGet 包时不需要在主项目里重复写这一组属性，除非你直接用 `ProjectReference` 引用源码项目，或者需要覆盖包内默认值。

如果主程序要发布单文件，还需要在主程序项目中显式声明 RID 和单文件相关属性：

```xml
<PropertyGroup>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>false</SelfContained>
</PropertyGroup>

<PropertyGroup Condition="'$(PublishSingleFile)'=='true'">
  <EnableMsixTooling>true</EnableMsixTooling>
  <IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
</PropertyGroup>
```

`EnableMsixTooling` 和 `IncludeAllContentForSelfExtract` 只建议在单文件发布时启用，它们会让 Windows App SDK 相关文件按单文件发布要求参与打包。Demo 加入 Notifications 后体积明显变大，主要原因就是 Windows App SDK 运行时相关依赖和资源被打入单文件 exe，而不是这几行 XML 本身造成的。

如果你不强制单文件，可以不设置 `PublishSingleFile=true`，发布目录会包含 exe、dll、deps、runtimeconfig 和 Windows App SDK 相关文件；总体部署文件数会变多，但主 exe 不会暴涨到把依赖都塞进去。发行给终端用户时，更稳妥的路线仍是“框架依赖程序 + Windows App Runtime 安装器”，或者直接使用安装程序管理这些依赖。

## 最小用法（VB）

```vb
Imports LakeUI.Notifications

LakeNotificationManager.Initialize(New LakeNotificationRegistrationOptions With {
    .DisplayName = "LakeUI Demo",
    .IconUri = New Uri("C:\Path\App.ico"),
    .ShowRuntimeInstallerUi = False
})

Dim id As UInteger = LakeNotificationManager.Show(
    "通知标题",
    "通知正文",
    "stable-tag",
    "demo-group")
```

如果没有主动调用 `Initialize`，`Show`、`GetAllAsync`、`Remove...Async`、`UpdateProgressAsync` 会使用默认参数自动初始化。正式应用建议在启动时显式初始化，这样可以控制通知显示名称、图标，以及 Windows App Runtime 缺失时是否弹出安装界面。

`IconUri` 必须指向真实存在且系统可访问的图标文件。如果文件不存在，Windows App SDK 会在注册通知身份时抛出 `FileNotFoundException`。单文件发布时尤其要注意：被打进 exe 的资源文件不等于发布目录里存在同名文件；要么把图标文件复制到输出目录，要么在传入 `IconUri` 前先判断文件是否存在。

## 初始化和状态探测

`Initialize` 失败时会抛出 `LakeNotificationException` 或相关异常；如果你希望启动流程不被异常打断，可以使用 `TryInitialize`。

```vb
Dim errorMessage As String = ""
Dim ok = LakeNotificationManager.TryInitialize(
    New LakeNotificationRegistrationOptions With {
        .DisplayName = "LakeUI Demo",
        .IconUri = New Uri("C:\Path\App.ico"),
        .ShowRuntimeInstallerUi = False
    },
    errorMessage)

If Not ok Then
    MessageBox.Show(errorMessage, "通知初始化失败")
    Return
End If

If LakeNotificationManager.IsSupported Then
    Dim setting = LakeNotificationManager.Setting
End If
```

常用属性：

- `IsSupported`：当前系统是否支持 Windows App Notification。
- `Setting`：当前应用通知设置状态，例如已启用、被用户禁用、被组策略禁用或系统不支持。
- `IsRegistered`：当前进程是否已经完成通知注册。

## 基础通知

`Show(title, body, tag, group)` 用于显示只有标题和正文的通知。

```vb
Dim id = LakeNotificationManager.Show(
    "构建完成",
    "LakeUI.Demo.exe 已经生成。",
    "build-finished",
    "publish")
```

`tag` 和 `group` 可选，但只要后续需要更新、查询或移除指定通知，就建议设置稳定的 `tag`；如果同类通知需要批量处理，可以再设置 `group`。

## 结构化通知

复杂通知通过 `LakeNotificationRequest` 构建，支持文本、图片、归属文本、输入框、下拉框、按钮、声音、场景、优先级、进度条和通知元数据。

```vb
Dim request As New LakeNotificationRequest With {
    .Tag = "download-progress",
    .Group = "downloads",
    .AttributionText = "LakeUI",
    .Duration = LakeNotificationDuration.Long,
    .Priority = LakeNotificationPriority.High,
    .Expiration = DateTimeOffset.Now.AddHours(1),
    .ExpiresOnReboot = True
}

request.Texts.Add(New LakeNotificationText("下载任务") With {
    .Language = "zh-CN",
    .MaxLines = 1
})
request.Texts.Add(New LakeNotificationText("正在下载 LakeUI 发行文件。"))
request.Arguments("source") = "demo"
request.Arguments("action") = "open"

request.AppLogoOverride = New LakeNotificationImage With {
    .Uri = New Uri("C:\Path\AppLogo.png"),
    .AlternateText = "LakeUI",
    .Crop = LakeNotificationImageCrop.Circle
}
request.HeroImage = New LakeNotificationImage With {
    .Uri = New Uri("C:\Path\Hero.png"),
    .AlternateText = "横幅图"
}
request.InlineImage = New LakeNotificationImage With {
    .Uri = New Uri("C:\Path\Inline.png"),
    .AlternateText = "内联图"
}

LakeNotificationManager.Show(request)
```

`SuppressDisplay = True` 时通知只进入通知中心，不弹出横幅。

## 文本和图片

`LakeNotificationText`：

- `Text`：显示文本。
- `Language`：语言标签，例如 `zh-CN`。
- `MaxLines`：最大显示行数。
- `IncomingCallAlignment`：使用来电场景的文本对齐方式。

`LakeNotificationImage`：

- `Uri`：图片 URI，推荐使用绝对文件路径 URI。
- `AlternateText`：辅助功能替代文本。
- `Crop`：图片裁剪方式，`Circle` 常用于应用 Logo。

图片可放在三个位置：

- `AppLogoOverride`：通知应用 Logo。
- `HeroImage`：通知顶部横幅图。
- `InlineImage`：通知正文中的内联图。

## 按钮

按钮可以回传参数、提交输入，也可以直接打开 URI。

```vb
Dim openButton As New LakeNotificationButton("打开目录")
openButton.Arguments("action") = "open-folder"
openButton.IconUri = New Uri("C:\Path\Open.png")

If LakeNotificationManager.IsButtonStyleSupported() Then
    openButton.ButtonStyle = LakeNotificationButtonStyle.Success
End If

If LakeNotificationManager.IsButtonToolTipSupported() Then
    openButton.ToolTip = "打开发行目录"
End If

request.Buttons.Add(openButton)
```

打开 URI 的按钮：

```vb
request.Buttons.Add(New LakeNotificationButton("访问官网") With {
    .InvokeUri = New Uri("https://lakeui.top")
})
```

设置了 `InvokeUri` 后，按钮会交给系统打开 URI，不会作为本应用通知回调传入。

右键菜单按钮：

```vb
Dim menuButton As New LakeNotificationButton("稍后处理") With {
    .ContextMenuPlacement = True
}
menuButton.Arguments("action") = "later"
request.Buttons.Add(menuButton)
```

## 输入框和下拉框

输入控件需要配合按钮提交。按钮的 `InputId` 可以关联主要输入框，点击按钮后文本框和下拉框的值会通过 `NotificationActivated` 的 `UserInput` 返回。

```vb
request.TextBoxes.Add(New LakeNotificationTextBox("reply") With {
    .Title = "备注",
    .Placeholder = "输入备注"
})

Dim level As New LakeNotificationComboBox("level") With {
    .Title = "优先级",
    .SelectedItem = "normal"
}
level.Items("normal") = "普通"
level.Items("high") = "重要"
request.ComboBoxes.Add(level)

Dim submitButton As New LakeNotificationButton("提交") With {
    .InputId = "reply"
}
submitButton.Arguments("action") = "submit"
request.Buttons.Add(submitButton)
```

回调中读取：

```vb
AddHandler LakeNotificationManager.NotificationActivated,
    Sub(sender, e)
        For Each item In e.Arguments
            Debug.WriteLine(item.Key & "=" & item.Value)
        Next

        For Each input In e.UserInput
            Debug.WriteLine(input.Key & "=" & input.Value)
        Next
    End Sub
```

`Arguments` 来自通知正文参数和按钮参数；`UserInput` 来自文本框与下拉框，键名就是控件 ID。

## 声音、时长和场景

```vb
request.AudioEvent = LakeNotificationSoundEvent.Mail
request.AudioLooping = LakeNotificationAudioLooping.None
request.Duration = LakeNotificationDuration.Long
request.Scenario = LakeNotificationScenario.Reminder
```

自定义声音：

```vb
request.AudioUri = New Uri("C:\Path\Notify.wav")
```

静音：

```vb
request.MuteAudio = True
```

场景：

- `Default`：普通通知。
- `Alarm`：闹钟场景。
- `Reminder`：提醒场景。
- `IncomingCall`：来电场景。
- `Urgent`：紧急通知场景，使用前建议调用 `IsUrgentScenarioSupported()`。

声音事件：

- `Default`
- `IM`
- `Mail`
- `Reminder`
- `SMS`
- `Alarm` 到 `Alarm10`
- `Call` 到 `Call10`

## 进度条

要做可更新进度，通知必须设置稳定的 `Tag`；如果设置了 `Group`，更新时也要传入相同的 `Group`。

```vb
Dim request As New LakeNotificationRequest With {
    .Tag = "download-progress",
    .Group = "downloads",
    .InitialProgress = New LakeNotificationProgressUpdate With {
        .SequenceNumber = 1UI,
        .Title = "下载任务",
        .Status = "正在准备",
        .Value = 0.1,
        .ValueStringOverride = "10%"
    }
}

request.Texts.Add(New LakeNotificationText("下载任务"))
request.Texts.Add(New LakeNotificationText("正在准备下载。"))
request.ProgressBars.Add(New LakeNotificationProgressBar With {
    .BindTitle = True,
    .BindStatus = True,
    .BindValue = True,
    .BindValueStringOverride = True
})

LakeNotificationManager.Show(request)
```

更新进度：

```vb
Dim result = Await LakeNotificationManager.UpdateProgressAsync(
    "download-progress",
    "downloads",
    New LakeNotificationProgressUpdate With {
        .SequenceNumber = 2UI,
        .Title = "下载任务",
        .Status = "正在下载",
        .Value = 0.6,
        .ValueStringOverride = "60%"
    })
```

`SequenceNumber` 用于防止旧进度覆盖新进度，后续更新应递增。传入 `0` 时库会自动按 `1` 处理。

`UpdateProgressAsync` 返回 `LakeNotificationProgressResult`：

- `Succeeded`：更新成功。
- `AppNotificationNotFound`：未找到匹配通知。
- `Unsupported`：当前系统或通知不支持进度更新。
- `Unknown`：未知结果。

## 查询和移除

```vb
Dim shown = Await LakeNotificationManager.GetAllAsync()

If shown.Count > 0 Then
    Await LakeNotificationManager.RemoveByIdAsync(shown(0).Id)
End If

Await LakeNotificationManager.RemoveByTagAsync("download-progress")
Await LakeNotificationManager.RemoveByGroupAsync("downloads")
Await LakeNotificationManager.RemoveByTagAndGroupAsync("download-progress", "downloads")
Await LakeNotificationManager.RemoveAllAsync()
```

`GetAllAsync` 返回 `LakeShownNotification` 集合，包含：

- `Id`：Windows App SDK 分配的通知 ID。
- `Payload`：通知 XML。
- `Tag`：通知标签。
- `Group`：通知分组。

## 原始 XML 和调试

如果结构化 API 不够用，可以直接显示原始 Windows App Notification XML：

```vb
Dim raw As New LakeRawNotificationRequest("
<toast>
  <visual>
    <binding template=""ToastGeneric"">
      <text>原始 XML 通知</text>
      <text>这条通知直接使用 XML Payload。</text>
    </binding>
  </visual>
</toast>") With {
    .Tag = "raw-toast",
    .Group = "demo",
    .Priority = LakeNotificationPriority.High
}

LakeNotificationManager.ShowRaw(raw)
```

只构建 XML，不显示通知：

```vb
Dim payload As String = LakeNotificationManager.BuildPayload(request)
Debug.WriteLine(payload)
```

`ShowRaw` 使用 `LakeRawNotificationRequest`，可设置 `Payload`、`Tag`、`Group`、`Priority`、`Expiration`、`ExpiresOnReboot`、`SuppressDisplay` 和 `InitialProgress`。

## 注销和关闭

应用退出时通常不需要手动注销。如果你需要解绑当前进程内的通知激活事件，可以调用：

```vb
LakeNotificationManager.Shutdown()
```

同时注销当前注册：

```vb
LakeNotificationManager.Shutdown(unregister:=True)
```

也可以显式调用：

```vb
LakeNotificationManager.Unregister()
LakeNotificationManager.UnregisterAll()
```

`Unregister` 注销当前通知注册；`UnregisterAll` 注销当前应用的全部通知注册。调用后如果继续显示通知，库会再次初始化并注册。

## C# 示例

```csharp
using LakeUI.Notifications;

LakeNotificationManager.Initialize(new LakeNotificationRegistrationOptions
{
    DisplayName = "LakeUI Demo",
    IconUri = new Uri(@"C:\Path\App.ico"),
    ShowRuntimeInstallerUi = false
});

var request = new LakeNotificationRequest
{
    Tag = "csharp-demo",
    Group = "demo",
    Duration = LakeNotificationDuration.Long
};

request.Texts.Add(new LakeNotificationText("C# 通知"));
request.Texts.Add(new LakeNotificationText("LakeUI.Notifications 也可以直接在 C# 中使用。"));
request.Buttons.Add(new LakeNotificationButton("确认")
{
    ButtonStyle = LakeNotificationButtonStyle.Success
});

LakeNotificationManager.Show(request);
```

## API 速查

`LakeNotificationManager`：

- `NotificationActivated`：通知正文、按钮或输入提交后的激活事件。
- `IsSupported`：系统是否支持通知。
- `Setting`：当前应用通知设置状态。
- `IsRegistered`：当前进程是否已经注册。
- `TryInitialize(...)`：尝试初始化，失败时返回错误消息。
- `Initialize(...)`：初始化 Windows App Runtime Bootstrap 并注册通知。
- `Shutdown(...)`：解绑事件，可选注销当前注册。
- `Unregister()`：注销当前注册。
- `UnregisterAll()`：注销全部注册。
- `Show(title, body, tag, group)`：显示基础通知。
- `Show(request)`：显示结构化通知。
- `ShowRaw(request)`：显示原始 XML 通知。
- `GetAllAsync()`：获取通知中心中的当前应用通知。
- `RemoveAllAsync()`：移除全部通知。
- `RemoveByIdAsync(id)`：按 ID 移除。
- `RemoveByTagAsync(tag)`：按标签移除。
- `RemoveByGroupAsync(group)`：按分组移除。
- `RemoveByTagAndGroupAsync(tag, group)`：按标签和分组移除。
- `UpdateProgressAsync(tag, update)`：按标签更新进度。
- `UpdateProgressAsync(tag, group, update)`：按标签和分组更新进度。
- `BuildPayload(request)`：构建 XML Payload。
- `IsUrgentScenarioSupported()`：是否支持紧急场景。
- `IsButtonStyleSupported()`：是否支持按钮样式。
- `IsButtonToolTipSupported()`：是否支持按钮提示文本。

主要模型：

- `LakeNotificationRegistrationOptions`：初始化选项。
- `LakeNotificationRequest`：结构化通知请求。
- `LakeRawNotificationRequest`：原始 XML 通知请求。
- `LakeNotificationText`：通知文本。
- `LakeNotificationImage`：通知图片。
- `LakeNotificationButton`：通知按钮。
- `LakeNotificationTextBox`：文本输入框。
- `LakeNotificationComboBox`：下拉选择框。
- `LakeNotificationProgressBar`：进度条显示配置。
- `LakeNotificationProgressUpdate`：进度更新数据。
- `LakeShownNotification`：通知中心中的通知快照。
- `LakeNotificationActivatedEventArgs`：通知激活事件参数。
- `LakeNotificationException`：通知专用异常。

枚举：

- `LakeNotificationSetting`
- `LakeNotificationPriority`
- `LakeNotificationDuration`
- `LakeNotificationScenario`
- `LakeNotificationImageCrop`
- `LakeNotificationAudioLooping`
- `LakeNotificationSoundEvent`
- `LakeNotificationButtonStyle`
- `LakeNotificationProgressResult`

## 部署注意事项

本库依赖 Windows App Runtime Singleton 包。发布到用户机器前，需要随安装程序安装 Windows App Runtime 2.1.3 对应架构版本，或者确认目标系统已经安装。

本 NuGet 包会自动导入以下属性，避免 Windows App SDK 自动生成文件进入 VB 编译流程：

```xml
<WindowsAppSdkBootstrapInitialize>false</WindowsAppSdkBootstrapInitialize>
<WindowsAppSdkDeploymentManagerInitialize>false</WindowsAppSdkDeploymentManagerInitialize>
<WindowsAppSdkAutoInitialize>false</WindowsAppSdkAutoInitialize>
<WindowsAppSdkIncludeVersionInfo>false</WindowsAppSdkIncludeVersionInfo>
<WindowsAppSdkUndockedRegFreeWinRTInitialize>false</WindowsAppSdkUndockedRegFreeWinRTInitialize>
```

当前版本没有封装计划通知或定时通知 API。如需延时显示，请在主程序中用计时器、任务计划或后台逻辑到期后调用 `Show`。
