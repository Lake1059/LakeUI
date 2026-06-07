using System.Collections.ObjectModel;

namespace LakeUI.Notifications;

/// <summary>
/// 通知初始化和注册选项。
/// </summary>
public sealed class LakeNotificationRegistrationOptions
{
    /// <summary>
    /// 通知在系统设置和通知中心里显示的应用名称。
    /// </summary>
    public string DisplayName { get; set; } = AppDomain.CurrentDomain.FriendlyName;

    /// <summary>
    /// 通知使用的应用图标 URI。WinForms 程序通常传入 ico/png 文件的绝对路径 URI。
    /// </summary>
    public Uri? IconUri { get; set; }

    /// <summary>
    /// Windows App Runtime 缺失时是否允许 Windows App SDK 弹出安装提示界面。
    /// </summary>
    public bool ShowRuntimeInstallerUi { get; set; } = true;
}

/// <summary>
/// 通知正文中的一段文本。按添加顺序显示，通常第一段作为标题，第二段作为正文。
/// </summary>
public sealed class LakeNotificationText
{
    /// <summary>
    /// 创建空文本段。
    /// </summary>
    public LakeNotificationText()
    {
    }

    /// <summary>
    /// 使用指定文本创建文本段。
    /// </summary>
    /// <param name="text">要显示的文本。</param>
    public LakeNotificationText(string text)
    {
        Text = text;
    }

    /// <summary>
    /// 文本内容。
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 可选语言标签，例如 zh-CN 或 en-US。
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// 可选最大显示行数。
    /// </summary>
    public int? MaxLines { get; set; }

    /// <summary>
    /// 是否使用来电通知的文本对齐方式。
    /// </summary>
    public bool IncomingCallAlignment { get; set; }
}

/// <summary>
/// 通知图片配置，可用于应用 Logo、横幅图或内联图片。
/// </summary>
public sealed class LakeNotificationImage
{
    /// <summary>
    /// 图片 URI。建议使用绝对文件 URI 或系统可访问的 http/https URI。
    /// </summary>
    public Uri? Uri { get; set; }

    /// <summary>
    /// 图片替代文本，用于辅助功能。
    /// </summary>
    public string? AlternateText { get; set; }

    /// <summary>
    /// 图片裁剪方式。Logo 常用圆形裁剪，其他图片通常保持默认。
    /// </summary>
    public LakeNotificationImageCrop Crop { get; set; } = LakeNotificationImageCrop.Default;
}

/// <summary>
/// 通知按钮配置。按钮可以携带参数、提交输入框、打开 URI 或放入右键菜单。
/// </summary>
public sealed class LakeNotificationButton
{
    /// <summary>
    /// 创建空按钮。
    /// </summary>
    public LakeNotificationButton()
    {
    }

    /// <summary>
    /// 使用指定按钮文字创建按钮。
    /// </summary>
    /// <param name="content">按钮显示文本。</param>
    public LakeNotificationButton(string content)
    {
        Content = content;
    }

    /// <summary>
    /// 按钮显示文本。
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 点击按钮后通过 <see cref="LakeNotificationManager.NotificationActivated"/> 返回的参数集合。
    /// </summary>
    public Dictionary<string, string> Arguments { get; } = new();

    /// <summary>
    /// 按钮图标 URI。
    /// </summary>
    public Uri? IconUri { get; set; }

    /// <summary>
    /// 点击按钮时直接打开的 URI。设置后按钮参数不会作为本应用回调传入。
    /// </summary>
    public Uri? InvokeUri { get; set; }

    /// <summary>
    /// 打开 URI 时可选的目标 AppUserModelId。
    /// </summary>
    public string? TargetAppId { get; set; }

    /// <summary>
    /// 关联的输入框 ID。设置后，点击按钮会提交该输入框以及其他输入控件的值。
    /// </summary>
    public string? InputId { get; set; }

    /// <summary>
    /// 按钮提示文本。使用前可调用 <see cref="LakeNotificationManager.IsButtonToolTipSupported"/> 判断系统是否支持。
    /// </summary>
    public string? ToolTip { get; set; }

    /// <summary>
    /// 是否将按钮放入通知右键菜单。
    /// </summary>
    public bool ContextMenuPlacement { get; set; }

    /// <summary>
    /// 按钮样式。使用前可调用 <see cref="LakeNotificationManager.IsButtonStyleSupported"/> 判断系统是否支持。
    /// </summary>
    public LakeNotificationButtonStyle ButtonStyle { get; set; } = LakeNotificationButtonStyle.Default;
}

/// <summary>
/// 通知中的文本输入框。
/// </summary>
public sealed class LakeNotificationTextBox
{
    /// <summary>
    /// 创建空输入框。
    /// </summary>
    public LakeNotificationTextBox()
    {
    }

    /// <summary>
    /// 使用指定 ID 创建输入框。
    /// </summary>
    /// <param name="id">输入框 ID，需在同一条通知中唯一。</param>
    public LakeNotificationTextBox(string id)
    {
        Id = id;
    }

    /// <summary>
    /// 输入框 ID。提交后会作为用户输入键名返回。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 输入框标题。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 输入框占位文本。
    /// </summary>
    public string? Placeholder { get; set; }
}

/// <summary>
/// 通知中的下拉选择框。
/// </summary>
public sealed class LakeNotificationComboBox
{
    /// <summary>
    /// 创建空下拉框。
    /// </summary>
    public LakeNotificationComboBox()
    {
    }

    /// <summary>
    /// 使用指定 ID 创建下拉框。
    /// </summary>
    /// <param name="id">下拉框 ID，需在同一条通知中唯一。</param>
    public LakeNotificationComboBox(string id)
    {
        Id = id;
    }

    /// <summary>
    /// 下拉框 ID。提交后会作为用户输入键名返回。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 下拉框标题。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 默认选中项 ID。
    /// </summary>
    public string? SelectedItem { get; set; }

    /// <summary>
    /// 下拉项集合。Key 是项 ID，Value 是显示文本。
    /// </summary>
    public Dictionary<string, string> Items { get; } = new();
}

/// <summary>
/// 通知进度条配置。可以使用固定值，也可以通过 Bind 系列属性绑定到后续进度更新数据。
/// </summary>
public sealed class LakeNotificationProgressBar
{
    /// <summary>
    /// 固定进度条标题。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 固定进度条状态文本。
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 固定进度值显示文本，例如 40%。
    /// </summary>
    public string? ValueStringOverride { get; set; }

    /// <summary>
    /// 固定进度值，范围通常为 0.0 到 1.0。
    /// </summary>
    public double? Value { get; set; }

    /// <summary>
    /// 是否将标题绑定到 <see cref="LakeNotificationProgressUpdate.Title"/>。
    /// </summary>
    public bool BindTitle { get; set; }

    /// <summary>
    /// 是否将状态文本绑定到 <see cref="LakeNotificationProgressUpdate.Status"/>。
    /// </summary>
    public bool BindStatus { get; set; }

    /// <summary>
    /// 是否将进度值绑定到 <see cref="LakeNotificationProgressUpdate.Value"/>。
    /// </summary>
    public bool BindValue { get; set; }

    /// <summary>
    /// 是否将进度值显示文本绑定到 <see cref="LakeNotificationProgressUpdate.ValueStringOverride"/>。
    /// </summary>
    public bool BindValueStringOverride { get; set; }
}

/// <summary>
/// 结构化通知请求。将需要显示的文本、图片、输入控件、按钮和元数据填入此对象后调用 <see cref="LakeNotificationManager.Show(LakeNotificationRequest)"/>。
/// </summary>
public sealed class LakeNotificationRequest
{
    /// <summary>
    /// 通知文本段集合。通常第一段为标题，第二段为正文。
    /// </summary>
    public List<LakeNotificationText> Texts { get; } = new();

    /// <summary>
    /// 通知按钮集合。
    /// </summary>
    public List<LakeNotificationButton> Buttons { get; } = new();

    /// <summary>
    /// 文本输入框集合。
    /// </summary>
    public List<LakeNotificationTextBox> TextBoxes { get; } = new();

    /// <summary>
    /// 下拉选择框集合。
    /// </summary>
    public List<LakeNotificationComboBox> ComboBoxes { get; } = new();

    /// <summary>
    /// 进度条集合。
    /// </summary>
    public List<LakeNotificationProgressBar> ProgressBars { get; } = new();

    /// <summary>
    /// 通知显示时附带的初始进度数据。需要与绑定型进度条配合使用。
    /// </summary>
    public LakeNotificationProgressUpdate? InitialProgress { get; set; }

    /// <summary>
    /// 点击通知正文后通过激活回调返回的参数集合。
    /// </summary>
    public Dictionary<string, string> Arguments { get; } = new();

    /// <summary>
    /// 应用 Logo 覆盖图片。
    /// </summary>
    public LakeNotificationImage? AppLogoOverride { get; set; }

    /// <summary>
    /// 通知顶部横幅图片。
    /// </summary>
    public LakeNotificationImage? HeroImage { get; set; }

    /// <summary>
    /// 通知正文中的内联图片。
    /// </summary>
    public LakeNotificationImage? InlineImage { get; set; }

    /// <summary>
    /// 通知归属文本，通常用于显示来源。
    /// </summary>
    public string? AttributionText { get; set; }

    /// <summary>
    /// 通知归属文本语言标签。
    /// </summary>
    public string? AttributionLanguage { get; set; }

    /// <summary>
    /// 自定义通知声音 URI。
    /// </summary>
    public Uri? AudioUri { get; set; }

    /// <summary>
    /// 系统内置通知声音事件。
    /// </summary>
    public LakeNotificationSoundEvent? AudioEvent { get; set; }

    /// <summary>
    /// 声音是否循环播放。
    /// </summary>
    public LakeNotificationAudioLooping AudioLooping { get; set; } = LakeNotificationAudioLooping.None;

    /// <summary>
    /// 是否静音。
    /// </summary>
    public bool MuteAudio { get; set; }

    /// <summary>
    /// 通知显示时长。
    /// </summary>
    public LakeNotificationDuration Duration { get; set; } = LakeNotificationDuration.Default;

    /// <summary>
    /// 通知场景。Alarm、Reminder、IncomingCall、Urgent 会影响系统展示行为。
    /// </summary>
    public LakeNotificationScenario Scenario { get; set; } = LakeNotificationScenario.Default;

    /// <summary>
    /// 通知标签。建议为需要后续更新或移除的通知设置稳定标签。
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// 通知分组。适合批量移除同一组通知。
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// 通知优先级。
    /// </summary>
    public LakeNotificationPriority Priority { get; set; } = LakeNotificationPriority.Default;

    /// <summary>
    /// 通知过期时间。过期后系统会自动移除。
    /// </summary>
    public DateTimeOffset? Expiration { get; set; }

    /// <summary>
    /// 是否在系统重启后过期。
    /// </summary>
    public bool ExpiresOnReboot { get; set; }

    /// <summary>
    /// 是否仅写入通知中心而不弹出横幅。
    /// </summary>
    public bool SuppressDisplay { get; set; }
}

/// <summary>
/// 原始 XML 通知请求。适合直接使用 Windows App Notification XML Payload。
/// </summary>
public sealed class LakeRawNotificationRequest
{
    /// <summary>
    /// 创建空原始通知请求。
    /// </summary>
    public LakeRawNotificationRequest()
    {
    }

    /// <summary>
    /// 使用指定 XML Payload 创建原始通知请求。
    /// </summary>
    /// <param name="payload">Windows App Notification XML Payload。</param>
    public LakeRawNotificationRequest(string payload)
    {
        Payload = payload;
    }

    /// <summary>
    /// Windows App Notification XML Payload。
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// 通知标签。
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// 通知分组。
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// 通知优先级。
    /// </summary>
    public LakeNotificationPriority Priority { get; set; } = LakeNotificationPriority.Default;

    /// <summary>
    /// 通知过期时间。
    /// </summary>
    public DateTimeOffset? Expiration { get; set; }

    /// <summary>
    /// 是否在系统重启后过期。
    /// </summary>
    public bool ExpiresOnReboot { get; set; }

    /// <summary>
    /// 是否仅写入通知中心而不弹出横幅。
    /// </summary>
    public bool SuppressDisplay { get; set; }

    /// <summary>
    /// 通知显示时附带的初始进度数据。
    /// </summary>
    public LakeNotificationProgressUpdate? InitialProgress { get; set; }
}

/// <summary>
/// 通知中心中已经显示的通知快照。
/// </summary>
public sealed class LakeShownNotification
{
    /// <summary>
    /// Windows App SDK 分配的通知 ID。
    /// </summary>
    public uint Id { get; init; }

    /// <summary>
    /// 通知 XML Payload。
    /// </summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>
    /// 通知标签。
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// 通知分组。
    /// </summary>
    public string? Group { get; init; }
}

/// <summary>
/// 通知进度条更新数据。用于 <see cref="LakeNotificationManager.UpdateProgressAsync(string, LakeNotificationProgressUpdate)"/>。
/// </summary>
public sealed class LakeNotificationProgressUpdate
{
    /// <summary>
    /// 进度更新序号。系统会忽略旧序号更新；设置为 0 时会自动按 1 处理。
    /// </summary>
    public uint SequenceNumber { get; set; }

    /// <summary>
    /// 进度条标题。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 进度状态文本。
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 进度值，范围通常为 0.0 到 1.0。
    /// </summary>
    public double? Value { get; set; }

    /// <summary>
    /// 进度值显示文本，例如 80%。
    /// </summary>
    public string? ValueStringOverride { get; set; }
}

/// <summary>
/// 通知激活事件参数，包含通知启动参数、按钮参数和用户输入。
/// </summary>
public sealed class LakeNotificationActivatedEventArgs : EventArgs
{
    /// <summary>
    /// 创建通知激活事件参数。
    /// </summary>
    /// <param name="argument">Windows App SDK 原始启动参数字符串。</param>
    /// <param name="arguments">通知正文或按钮携带的参数。</param>
    /// <param name="userInput">用户提交的输入框或下拉框值。</param>
    public LakeNotificationActivatedEventArgs(
        string argument,
        IReadOnlyDictionary<string, string> arguments,
        IReadOnlyDictionary<string, string> userInput)
    {
        Argument = argument;
        Arguments = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(arguments));
        UserInput = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(userInput));
    }

    /// <summary>
    /// Windows App SDK 原始启动参数字符串。
    /// </summary>
    public string Argument { get; }

    /// <summary>
    /// 通知正文或按钮携带的参数。
    /// </summary>
    public IReadOnlyDictionary<string, string> Arguments { get; }

    /// <summary>
    /// 用户提交的输入框或下拉框值。
    /// </summary>
    public IReadOnlyDictionary<string, string> UserInput { get; }
}

/// <summary>
/// LakeUI.Notifications 抛出的通知专用异常。
/// </summary>
public sealed class LakeNotificationException : Exception
{
    /// <summary>
    /// 使用异常消息创建通知异常。
    /// </summary>
    /// <param name="message">异常消息。</param>
    public LakeNotificationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 使用异常消息和内部异常创建通知异常。
    /// </summary>
    /// <param name="message">异常消息。</param>
    /// <param name="innerException">内部异常。</param>
    public LakeNotificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
