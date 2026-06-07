using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace LakeUI.Notifications;

/// <summary>
/// LakeUI 的 Windows App Notification 入口类。用于初始化 Windows App Runtime、注册通知、显示通知、更新进度、移除通知并接收通知激活回调。
/// </summary>
/// <remarks>
/// 推荐在应用启动时先调用 <see cref="Initialize(LakeNotificationRegistrationOptions)"/>，再调用 <see cref="Show(LakeNotificationRequest)"/>。
/// 未显式初始化时，显示、查询、移除等方法会使用默认参数自动初始化。
/// </remarks>
public static class LakeNotificationManager
{
    private const uint 运行时主次版本 = 0x00020001;
    private const string 运行时版本标签 = "";
    private static readonly PackageVersion 运行时最低版本 = new(0x0002000100030000);
    private static readonly object 同步锁 = new();
    private static bool 已初始化引导;
    private static bool 已注册;
    private static bool 已挂接激活事件;

    /// <summary>
    /// 当用户点击通知正文、按钮或提交输入框时触发。事件参数包含启动参数、按钮参数和用户输入值。
    /// </summary>
    public static event EventHandler<LakeNotificationActivatedEventArgs>? NotificationActivated;

    /// <summary>
    /// 获取当前系统是否支持 Windows App Notification。
    /// </summary>
    /// <remarks>
    /// 读取此属性会尝试以静默方式初始化 Windows App Runtime Bootstrap；如果目标机器缺少运行时会抛出 <see cref="LakeNotificationException"/>。
    /// </remarks>
    public static bool IsSupported
    {
        get
        {
            确保引导已初始化(new LakeNotificationRegistrationOptions { ShowRuntimeInstallerUi = false });
            return AppNotificationManager.IsSupported();
        }
    }

    /// <summary>
    /// 获取当前应用通知开关状态，例如已启用、被用户禁用、被组策略禁用或系统不支持。
    /// </summary>
    public static LakeNotificationSetting Setting
    {
        get
        {
            确保引导已初始化(new LakeNotificationRegistrationOptions { ShowRuntimeInstallerUi = false });
            return 映射通知设置(AppNotificationManager.Default.Setting);
        }
    }

    /// <summary>
    /// 获取当前进程是否已经向 <see cref="AppNotificationManager"/> 完成注册。
    /// </summary>
    public static bool IsRegistered
    {
        get
        {
            lock (同步锁)
            {
                return 已注册;
            }
        }
    }

    /// <summary>
    /// 尝试初始化通知功能，并用返回值代替异常。适合在启动流程中做兼容性探测。
    /// </summary>
    /// <param name="options">注册选项，包括显示名称、图标和运行时缺失时是否显示安装界面。</param>
    /// <param name="errorMessage">初始化失败时返回异常消息；成功时为空字符串。</param>
    /// <returns>初始化成功返回 <see langword="true"/>，失败返回 <see langword="false"/>。</returns>
    public static bool TryInitialize(LakeNotificationRegistrationOptions options, out string errorMessage)
    {
        try
        {
            Initialize(options);
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception 异常)
        {
            errorMessage = 异常.Message;
            return false;
        }
    }

    /// <summary>
    /// 使用应用显示名称和图标路径尝试初始化通知功能。
    /// </summary>
    /// <param name="displayName">通知在系统设置或通知中心中显示的应用名称。</param>
    /// <param name="iconUri">应用图标 URI 字符串；为空时使用系统默认注册方式。</param>
    /// <param name="errorMessage">初始化失败时返回异常消息；成功时为空字符串。</param>
    /// <returns>初始化成功返回 <see langword="true"/>，失败返回 <see langword="false"/>。</returns>
    public static bool TryInitialize(string displayName, string? iconUri, out string errorMessage)
    {
        return TryInitialize(new LakeNotificationRegistrationOptions
        {
            DisplayName = displayName,
            IconUri = string.IsNullOrWhiteSpace(iconUri) ? null : new Uri(iconUri)
        }, out errorMessage);
    }

    /// <summary>
    /// 初始化 Windows App Runtime Bootstrap 并注册通知管理器。建议在 WinForms/VB 应用启动时调用一次。
    /// </summary>
    /// <param name="options">注册选项。传入图标 URI 时，会用显示名称和图标注册通知身份。</param>
    public static void Initialize(LakeNotificationRegistrationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        确保引导已初始化(options);

        lock (同步锁)
        {
            var 通知管理器 = AppNotificationManager.Default;
            挂接通知激活事件(通知管理器);

            if (已注册)
            {
                return;
            }

            if (options.IconUri is null)
            {
                通知管理器.Register();
            }
            else
            {
                通知管理器.Register(options.DisplayName, options.IconUri);
            }

            已注册 = true;
        }
    }

    /// <summary>
    /// 使用应用显示名称和可选图标路径初始化通知功能。
    /// </summary>
    /// <param name="displayName">通知在系统设置或通知中心中显示的应用名称。</param>
    /// <param name="iconUri">应用图标 URI 字符串；为空时使用系统默认注册方式。</param>
    public static void Initialize(string displayName, string? iconUri = null)
    {
        Initialize(new LakeNotificationRegistrationOptions
        {
            DisplayName = displayName,
            IconUri = string.IsNullOrWhiteSpace(iconUri) ? null : new Uri(iconUri)
        });
    }

    /// <summary>
    /// 关闭当前进程内的通知事件挂接。可选地注销当前通知注册。
    /// </summary>
    /// <param name="unregister">为 <see langword="true"/> 时调用 Windows App SDK 注销当前注册。</param>
    public static void Shutdown(bool unregister = false)
    {
        lock (同步锁)
        {
            解绑通知激活事件();

            if (unregister && 已注册)
            {
                AppNotificationManager.Default.Unregister();
            }

            已注册 = false;
        }
    }

    /// <summary>
    /// 注销当前通知注册并解绑通知激活事件。
    /// </summary>
    public static void Unregister()
    {
        确保引导已初始化(new LakeNotificationRegistrationOptions { ShowRuntimeInstallerUi = false });
        lock (同步锁)
        {
            解绑通知激活事件();
            AppNotificationManager.Default.Unregister();
            已注册 = false;
        }
    }

    /// <summary>
    /// 注销当前应用的全部通知注册并解绑通知激活事件。
    /// </summary>
    public static void UnregisterAll()
    {
        确保引导已初始化(new LakeNotificationRegistrationOptions { ShowRuntimeInstallerUi = false });
        lock (同步锁)
        {
            解绑通知激活事件();
            AppNotificationManager.Default.UnregisterAll();
            已注册 = false;
        }
    }

    /// <summary>
    /// 显示一条只包含标题和正文的基础通知。
    /// </summary>
    /// <param name="title">通知标题。</param>
    /// <param name="body">通知正文。</param>
    /// <param name="tag">可选标签，用于后续更新或移除通知。</param>
    /// <param name="group">可选分组，用于批量移除通知。</param>
    /// <returns>Windows App SDK 分配的通知 ID。</returns>
    public static uint Show(string title, string body, string? tag = null, string? group = null)
    {
        var 请求 = new LakeNotificationRequest
        {
            Tag = tag,
            Group = group
        };
        请求.Texts.Add(new LakeNotificationText(title));
        请求.Texts.Add(new LakeNotificationText(body));
        return Show(请求);
    }

    /// <summary>
    /// 根据结构化请求显示通知，支持文本、图片、输入框、下拉框、按钮、声音、场景和进度条。
    /// </summary>
    /// <param name="request">通知请求对象。</param>
    /// <returns>Windows App SDK 分配的通知 ID。</returns>
    public static uint Show(LakeNotificationRequest request)
    {
        确保已注册();
        var 通知 = 构建通知(request);
        AppNotificationManager.Default.Show(通知);
        return 通知.Id;
    }

    /// <summary>
    /// 使用原始 App Notification XML Payload 显示通知。
    /// </summary>
    /// <param name="request">原始 XML 通知请求。Payload 不能为空。</param>
    /// <returns>Windows App SDK 分配的通知 ID。</returns>
    public static uint ShowRaw(LakeRawNotificationRequest request)
    {
        确保已注册();
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Payload))
        {
            throw new ArgumentException("Notification payload cannot be empty.", nameof(request));
        }

        var 通知 = new AppNotification(request.Payload);
        应用通知元数据(通知, request.Tag, request.Group, request.Priority, request.Expiration, request.ExpiresOnReboot, request.SuppressDisplay, request.InitialProgress);
        AppNotificationManager.Default.Show(通知);
        return 通知.Id;
    }

    /// <summary>
    /// 获取当前应用仍保留在通知中心中的通知列表。
    /// </summary>
    /// <returns>通知中心中的通知快照。</returns>
    public static async Task<IReadOnlyList<LakeShownNotification>> GetAllAsync()
    {
        确保已注册();
        var 通知列表 = await AppNotificationManager.Default.GetAllAsync();
        return 通知列表
            .Select(通知 => new LakeShownNotification
            {
                Id = 通知.Id,
                Payload = 通知.Payload,
                Tag = 通知.Tag,
                Group = 通知.Group
            })
            .ToArray();
    }

    /// <summary>
    /// 从通知中心移除当前应用的全部通知。
    /// </summary>
    public static async Task RemoveAllAsync()
    {
        确保已注册();
        await AppNotificationManager.Default.RemoveAllAsync();
    }

    /// <summary>
    /// 根据通知 ID 从通知中心移除通知。
    /// </summary>
    /// <param name="id">由 <see cref="Show(LakeNotificationRequest)"/> 或 <see cref="ShowRaw(LakeRawNotificationRequest)"/> 返回的通知 ID。</param>
    public static async Task RemoveByIdAsync(uint id)
    {
        确保已注册();
        await AppNotificationManager.Default.RemoveByIdAsync(id);
    }

    /// <summary>
    /// 根据标签从通知中心移除通知。
    /// </summary>
    /// <param name="tag">通知标签。</param>
    public static async Task RemoveByTagAsync(string tag)
    {
        确保已注册();
        await AppNotificationManager.Default.RemoveByTagAsync(tag);
    }

    /// <summary>
    /// 根据分组从通知中心移除通知。
    /// </summary>
    /// <param name="group">通知分组。</param>
    public static async Task RemoveByGroupAsync(string group)
    {
        确保已注册();
        await AppNotificationManager.Default.RemoveByGroupAsync(group);
    }

    /// <summary>
    /// 根据标签和分组从通知中心移除通知。
    /// </summary>
    /// <param name="tag">通知标签。</param>
    /// <param name="group">通知分组。</param>
    public static async Task RemoveByTagAndGroupAsync(string tag, string group)
    {
        确保已注册();
        await AppNotificationManager.Default.RemoveByTagAndGroupAsync(tag, group);
    }

    /// <summary>
    /// 根据标签更新通知进度条数据。
    /// </summary>
    /// <param name="tag">要更新的通知标签。</param>
    /// <param name="update">进度更新数据。SequenceNumber 用于控制更新顺序，0 会自动修正为 1。</param>
    /// <returns>进度更新结果。</returns>
    public static async Task<LakeNotificationProgressResult> UpdateProgressAsync(
        string tag,
        LakeNotificationProgressUpdate update)
    {
        确保已注册();
        ArgumentNullException.ThrowIfNull(update);
        var 结果 = await AppNotificationManager.Default.UpdateAsync(构建进度数据(update), tag);
        return 映射进度结果(结果);
    }

    /// <summary>
    /// 根据标签和分组更新通知进度条数据。
    /// </summary>
    /// <param name="tag">要更新的通知标签。</param>
    /// <param name="group">要更新的通知分组。</param>
    /// <param name="update">进度更新数据。SequenceNumber 用于控制更新顺序，0 会自动修正为 1。</param>
    /// <returns>进度更新结果。</returns>
    public static async Task<LakeNotificationProgressResult> UpdateProgressAsync(
        string tag,
        string group,
        LakeNotificationProgressUpdate update)
    {
        确保已注册();
        ArgumentNullException.ThrowIfNull(update);
        var 结果 = await AppNotificationManager.Default.UpdateAsync(构建进度数据(update), tag, group);
        return 映射进度结果(结果);
    }

    /// <summary>
    /// 仅构建通知 XML Payload，不显示通知。适合调试或保存模板。
    /// </summary>
    /// <param name="request">结构化通知请求。</param>
    /// <returns>Windows App Notification XML Payload。</returns>
    public static string BuildPayload(LakeNotificationRequest request)
    {
        确保引导已初始化(new LakeNotificationRegistrationOptions { ShowRuntimeInstallerUi = false });
        return 构建通知(request).Payload;
    }

    /// <summary>
    /// 获取当前 Windows App SDK/系统版本是否支持 Urgent 场景。
    /// </summary>
    public static bool IsUrgentScenarioSupported() => AppNotificationBuilder.IsUrgentScenarioSupported();

    /// <summary>
    /// 获取当前 Windows App SDK/系统版本是否支持按钮样式。
    /// </summary>
    public static bool IsButtonStyleSupported() => AppNotificationButton.IsButtonStyleSupported();

    /// <summary>
    /// 获取当前 Windows App SDK/系统版本是否支持按钮提示文本。
    /// </summary>
    public static bool IsButtonToolTipSupported() => AppNotificationButton.IsToolTipSupported();

    // 任何会访问通知中心或显示通知的方法，都必须先完成 AppNotificationManager 注册。
    private static void 确保已注册()
    {
        lock (同步锁)
        {
            if (已注册)
            {
                return;
            }
        }

        Initialize(new LakeNotificationRegistrationOptions());
    }

    // Windows App SDK 在未打包 WinForms 程序中需要先执行 Bootstrap 初始化。
    private static void 确保引导已初始化(LakeNotificationRegistrationOptions 选项)
    {
        lock (同步锁)
        {
            if (已初始化引导)
            {
                return;
            }

            Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);

            var 引导选项 = 选项.ShowRuntimeInstallerUi
                ? Bootstrap.InitializeOptions.OnNoMatch_ShowUI
                : Bootstrap.InitializeOptions.None;

            if (!Bootstrap.TryInitialize(运行时主次版本, 运行时版本标签, 运行时最低版本, 引导选项, out var 错误码))
            {
                throw new LakeNotificationException($"Windows App Runtime 2.1.3 is unavailable. Bootstrap HRESULT: 0x{错误码:X8}.");
            }

            已初始化引导 = true;
        }
    }

    // 注册只需要挂接一次事件，否则同一条通知点击会触发多次回调。
    private static void 挂接通知激活事件(AppNotificationManager 通知管理器)
    {
        if (已挂接激活事件)
        {
            return;
        }

        通知管理器.NotificationInvoked += 处理通知激活;
        已挂接激活事件 = true;
    }

    // 注销和关闭时共用事件解绑逻辑，避免状态不一致。
    private static void 解绑通知激活事件()
    {
        if (!已挂接激活事件)
        {
            return;
        }

        AppNotificationManager.Default.NotificationInvoked -= 处理通知激活;
        已挂接激活事件 = false;
    }

    // 将 LakeUI 的结构化通知对象转换为 Windows App SDK 的 AppNotification。
    private static AppNotification 构建通知(LakeNotificationRequest 请求)
    {
        ArgumentNullException.ThrowIfNull(请求);
        var 构建器 = new AppNotificationBuilder();

        foreach (var 参数 in 请求.Arguments)
        {
            构建器.AddArgument(参数.Key, 参数.Value);
        }

        foreach (var 文本 in 请求.Texts)
        {
            if (文本.Language is null && 文本.MaxLines is null && !文本.IncomingCallAlignment)
            {
                构建器.AddText(文本.Text);
                continue;
            }

            var 文本属性 = new AppNotificationTextProperties();
            if (文本.Language is not null)
            {
                文本属性.SetLanguage(文本.Language);
            }

            if (文本.MaxLines is not null)
            {
                文本属性.SetMaxLines(文本.MaxLines.Value);
            }

            if (文本.IncomingCallAlignment)
            {
                文本属性.SetIncomingCallAlignment();
            }

            构建器.AddText(文本.Text, 文本属性);
        }

        if (请求.AppLogoOverride?.Uri is not null)
        {
            设置应用标志图(构建器, 请求.AppLogoOverride);
        }

        if (请求.HeroImage?.Uri is not null)
        {
            设置横幅图片(构建器, 请求.HeroImage);
        }

        if (请求.InlineImage?.Uri is not null)
        {
            设置内联图片(构建器, 请求.InlineImage);
        }

        if (请求.AttributionText is not null)
        {
            if (请求.AttributionLanguage is null)
            {
                构建器.SetAttributionText(请求.AttributionText);
            }
            else
            {
                构建器.SetAttributionText(请求.AttributionText, 请求.AttributionLanguage);
            }
        }

        foreach (var 输入框 in 请求.TextBoxes)
        {
            if (输入框.Title is null && 输入框.Placeholder is null)
            {
                构建器.AddTextBox(输入框.Id);
            }
            else
            {
                构建器.AddTextBox(输入框.Id, 输入框.Placeholder ?? string.Empty, 输入框.Title ?? string.Empty);
            }
        }

        foreach (var 下拉框 in 请求.ComboBoxes)
        {
            var 下拉项 = new AppNotificationComboBox(下拉框.Id);
            if (下拉框.Title is not null)
            {
                下拉项.SetTitle(下拉框.Title);
            }

            if (下拉框.SelectedItem is not null)
            {
                下拉项.SetSelectedItem(下拉框.SelectedItem);
            }

            foreach (var 项 in 下拉框.Items)
            {
                下拉项.AddItem(项.Key, 项.Value);
            }

            构建器.AddComboBox(下拉项);
        }

        foreach (var 进度条 in 请求.ProgressBars)
        {
            构建器.AddProgressBar(构建进度条(进度条));
        }

        foreach (var 按钮 in 请求.Buttons)
        {
            构建器.AddButton(构建按钮(按钮));
        }

        if (请求.MuteAudio)
        {
            构建器.MuteAudio();
        }
        else if (请求.AudioUri is not null)
        {
            构建器.SetAudioUri(请求.AudioUri, 映射音频循环(请求.AudioLooping));
        }
        else if (请求.AudioEvent is not null)
        {
            构建器.SetAudioEvent(映射声音事件(请求.AudioEvent.Value), 映射音频循环(请求.AudioLooping));
        }

        if (请求.Duration != LakeNotificationDuration.Default)
        {
            构建器.SetDuration(映射显示时长(请求.Duration));
        }

        if (请求.Scenario != LakeNotificationScenario.Default)
        {
            构建器.SetScenario(映射场景(请求.Scenario));
        }

        var 通知 = 构建器.BuildNotification();
        应用通知元数据(通知, 请求.Tag, 请求.Group, 请求.Priority, 请求.Expiration, 请求.ExpiresOnReboot, 请求.SuppressDisplay, 请求.InitialProgress);
        return 通知;
    }

    // AppNotificationBuilder 只负责 XML 内容；Tag、Group、优先级等运行时元数据要设置到 AppNotification 实例。
    private static void 应用通知元数据(
        AppNotification 通知,
        string? 标签,
        string? 分组,
        LakeNotificationPriority 优先级,
        DateTimeOffset? 过期时间,
        bool 重启后过期,
        bool 仅入通知中心,
        LakeNotificationProgressUpdate? 初始进度)
    {
        if (标签 is not null)
        {
            通知.Tag = 标签;
        }

        if (分组 is not null)
        {
            通知.Group = 分组;
        }

        通知.Priority = 映射优先级(优先级);
        通知.ExpiresOnReboot = 重启后过期;
        通知.SuppressDisplay = 仅入通知中心;

        if (过期时间 is not null)
        {
            通知.Expiration = 过期时间.Value;
        }

        if (初始进度 is not null)
        {
            通知.Progress = 构建进度数据(初始进度);
        }
    }

    // 应用标志图、横幅图和内联图的 SDK 方法重载不同，分别保留小方法能减少主构建流程的分支噪音。
    private static void 设置应用标志图(AppNotificationBuilder 构建器, LakeNotificationImage 图片)
    {
        if (图片.AlternateText is not null)
        {
            构建器.SetAppLogoOverride(图片.Uri!, 映射图片裁剪(图片.Crop), 图片.AlternateText);
        }
        else if (图片.Crop != LakeNotificationImageCrop.Default)
        {
            构建器.SetAppLogoOverride(图片.Uri!, 映射图片裁剪(图片.Crop));
        }
        else
        {
            构建器.SetAppLogoOverride(图片.Uri!);
        }
    }

    private static void 设置横幅图片(AppNotificationBuilder 构建器, LakeNotificationImage 图片)
    {
        if (图片.AlternateText is not null)
        {
            构建器.SetHeroImage(图片.Uri!, 图片.AlternateText);
        }
        else
        {
            构建器.SetHeroImage(图片.Uri!);
        }
    }

    private static void 设置内联图片(AppNotificationBuilder 构建器, LakeNotificationImage 图片)
    {
        if (图片.AlternateText is not null)
        {
            构建器.SetInlineImage(图片.Uri!, 映射图片裁剪(图片.Crop), 图片.AlternateText);
        }
        else if (图片.Crop != LakeNotificationImageCrop.Default)
        {
            构建器.SetInlineImage(图片.Uri!, 映射图片裁剪(图片.Crop));
        }
        else
        {
            构建器.SetInlineImage(图片.Uri!);
        }
    }

    // 按钮有“回调参数”和“直接打开 URI”两种互斥行为，InvokeUri 优先。
    private static AppNotificationButton 构建按钮(LakeNotificationButton 按钮)
    {
        var 结果 = new AppNotificationButton(按钮.Content);

        if (按钮.IconUri is not null)
        {
            结果.SetIcon(按钮.IconUri);
        }

        if (按钮.InvokeUri is not null)
        {
            if (按钮.TargetAppId is null)
            {
                结果.SetInvokeUri(按钮.InvokeUri);
            }
            else
            {
                结果.SetInvokeUri(按钮.InvokeUri, 按钮.TargetAppId);
            }
        }
        else
        {
            foreach (var 参数 in 按钮.Arguments)
            {
                结果.AddArgument(参数.Key, 参数.Value);
            }
        }

        if (按钮.InputId is not null)
        {
            结果.SetInputId(按钮.InputId);
        }

        if (按钮.ToolTip is not null)
        {
            结果.SetToolTip(按钮.ToolTip);
        }

        if (按钮.ContextMenuPlacement)
        {
            结果.SetContextMenuPlacement();
        }

        if (按钮.ButtonStyle != LakeNotificationButtonStyle.Default)
        {
            结果.SetButtonStyle(映射按钮样式(按钮.ButtonStyle));
        }

        return 结果;
    }

    // 进度条支持固定值和绑定值；绑定值需要后续 UpdateProgressAsync 提供数据。
    private static AppNotificationProgressBar 构建进度条(LakeNotificationProgressBar 进度条)
    {
        var 结果 = new AppNotificationProgressBar();

        if (进度条.BindTitle)
        {
            结果.BindTitle();
        }
        else if (进度条.Title is not null)
        {
            结果.SetTitle(进度条.Title);
        }

        if (进度条.BindStatus)
        {
            结果.BindStatus();
        }
        else if (进度条.Status is not null)
        {
            结果.SetStatus(进度条.Status);
        }

        if (进度条.BindValue)
        {
            结果.BindValue();
        }
        else if (进度条.Value is not null)
        {
            结果.SetValue(进度条.Value.Value);
        }

        if (进度条.BindValueStringOverride)
        {
            结果.BindValueStringOverride();
        }
        else if (进度条.ValueStringOverride is not null)
        {
            结果.SetValueStringOverride(进度条.ValueStringOverride);
        }

        return 结果;
    }

    // SequenceNumber 不能为 0；这里兜底修正，降低调用方踩坑概率。
    private static AppNotificationProgressData 构建进度数据(LakeNotificationProgressUpdate 更新)
    {
        var 序号 = 更新.SequenceNumber == 0 ? 1 : 更新.SequenceNumber;
        var 数据 = new AppNotificationProgressData(序号);

        if (更新.Title is not null)
        {
            数据.Title = 更新.Title;
        }

        if (更新.Status is not null)
        {
            数据.Status = 更新.Status;
        }

        if (更新.Value is not null)
        {
            数据.Value = 更新.Value.Value;
        }

        if (更新.ValueStringOverride is not null)
        {
            数据.ValueStringOverride = 更新.ValueStringOverride;
        }

        return 数据;
    }

    private static void 处理通知激活(AppNotificationManager 发送者, AppNotificationActivatedEventArgs 参数)
    {
        NotificationActivated?.Invoke(null, new LakeNotificationActivatedEventArgs(
            参数.Argument,
            转为字典(参数.Arguments),
            转为字典(参数.UserInput)));
    }

    private static Dictionary<string, string> 转为字典(IDictionary<string, string> 值集合)
    {
        var 结果 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var 项 in 值集合)
        {
            结果[项.Key] = 项.Value ?? string.Empty;
        }

        return 结果;
    }

    private static AppNotificationPriority 映射优先级(LakeNotificationPriority 值) =>
        值 == LakeNotificationPriority.High ? AppNotificationPriority.High : AppNotificationPriority.Default;

    private static LakeNotificationSetting 映射通知设置(AppNotificationSetting 值) =>
        值 switch
        {
            AppNotificationSetting.Enabled => LakeNotificationSetting.Enabled,
            AppNotificationSetting.DisabledForApplication => LakeNotificationSetting.DisabledForApplication,
            AppNotificationSetting.DisabledForUser => LakeNotificationSetting.DisabledForUser,
            AppNotificationSetting.DisabledByGroupPolicy => LakeNotificationSetting.DisabledByGroupPolicy,
            AppNotificationSetting.DisabledByManifest => LakeNotificationSetting.DisabledByManifest,
            AppNotificationSetting.Unsupported => LakeNotificationSetting.Unsupported,
            _ => LakeNotificationSetting.Unknown
        };

    private static LakeNotificationProgressResult 映射进度结果(AppNotificationProgressResult 值) =>
        值 switch
        {
            AppNotificationProgressResult.Succeeded => LakeNotificationProgressResult.Succeeded,
            AppNotificationProgressResult.AppNotificationNotFound => LakeNotificationProgressResult.AppNotificationNotFound,
            AppNotificationProgressResult.Unsupported => LakeNotificationProgressResult.Unsupported,
            _ => LakeNotificationProgressResult.Unknown
        };

    private static AppNotificationDuration 映射显示时长(LakeNotificationDuration 值) =>
        值 == LakeNotificationDuration.Long ? AppNotificationDuration.Long : AppNotificationDuration.Default;

    private static AppNotificationScenario 映射场景(LakeNotificationScenario 值) =>
        值 switch
        {
            LakeNotificationScenario.Alarm => AppNotificationScenario.Alarm,
            LakeNotificationScenario.Reminder => AppNotificationScenario.Reminder,
            LakeNotificationScenario.IncomingCall => AppNotificationScenario.IncomingCall,
            LakeNotificationScenario.Urgent => AppNotificationScenario.Urgent,
            _ => AppNotificationScenario.Default
        };

    private static AppNotificationImageCrop 映射图片裁剪(LakeNotificationImageCrop 值) =>
        值 == LakeNotificationImageCrop.Circle ? AppNotificationImageCrop.Circle : AppNotificationImageCrop.Default;

    private static AppNotificationAudioLooping 映射音频循环(LakeNotificationAudioLooping 值) =>
        值 == LakeNotificationAudioLooping.Loop ? AppNotificationAudioLooping.Loop : AppNotificationAudioLooping.None;

    private static AppNotificationButtonStyle 映射按钮样式(LakeNotificationButtonStyle 值) =>
        值 switch
        {
            LakeNotificationButtonStyle.Success => AppNotificationButtonStyle.Success,
            LakeNotificationButtonStyle.Critical => AppNotificationButtonStyle.Critical,
            _ => AppNotificationButtonStyle.Default
        };

    private static AppNotificationSoundEvent 映射声音事件(LakeNotificationSoundEvent 值) =>
        值 switch
        {
            LakeNotificationSoundEvent.IM => AppNotificationSoundEvent.IM,
            LakeNotificationSoundEvent.Mail => AppNotificationSoundEvent.Mail,
            LakeNotificationSoundEvent.Reminder => AppNotificationSoundEvent.Reminder,
            LakeNotificationSoundEvent.SMS => AppNotificationSoundEvent.SMS,
            LakeNotificationSoundEvent.Alarm => AppNotificationSoundEvent.Alarm,
            LakeNotificationSoundEvent.Alarm2 => AppNotificationSoundEvent.Alarm2,
            LakeNotificationSoundEvent.Alarm3 => AppNotificationSoundEvent.Alarm3,
            LakeNotificationSoundEvent.Alarm4 => AppNotificationSoundEvent.Alarm4,
            LakeNotificationSoundEvent.Alarm5 => AppNotificationSoundEvent.Alarm5,
            LakeNotificationSoundEvent.Alarm6 => AppNotificationSoundEvent.Alarm6,
            LakeNotificationSoundEvent.Alarm7 => AppNotificationSoundEvent.Alarm7,
            LakeNotificationSoundEvent.Alarm8 => AppNotificationSoundEvent.Alarm8,
            LakeNotificationSoundEvent.Alarm9 => AppNotificationSoundEvent.Alarm9,
            LakeNotificationSoundEvent.Alarm10 => AppNotificationSoundEvent.Alarm10,
            LakeNotificationSoundEvent.Call => AppNotificationSoundEvent.Call,
            LakeNotificationSoundEvent.Call2 => AppNotificationSoundEvent.Call2,
            LakeNotificationSoundEvent.Call3 => AppNotificationSoundEvent.Call3,
            LakeNotificationSoundEvent.Call4 => AppNotificationSoundEvent.Call4,
            LakeNotificationSoundEvent.Call5 => AppNotificationSoundEvent.Call5,
            LakeNotificationSoundEvent.Call6 => AppNotificationSoundEvent.Call6,
            LakeNotificationSoundEvent.Call7 => AppNotificationSoundEvent.Call7,
            LakeNotificationSoundEvent.Call8 => AppNotificationSoundEvent.Call8,
            LakeNotificationSoundEvent.Call9 => AppNotificationSoundEvent.Call9,
            LakeNotificationSoundEvent.Call10 => AppNotificationSoundEvent.Call10,
            _ => AppNotificationSoundEvent.Default
        };
}
