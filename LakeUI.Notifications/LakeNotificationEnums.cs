namespace LakeUI.Notifications;

/// <summary>
/// 当前应用通知设置状态。
/// </summary>
public enum LakeNotificationSetting
{
    /// <summary>
    /// 通知已启用。
    /// </summary>
    Enabled,

    /// <summary>
    /// 当前应用通知被禁用。
    /// </summary>
    DisabledForApplication,

    /// <summary>
    /// 当前用户关闭了通知。
    /// </summary>
    DisabledForUser,

    /// <summary>
    /// 通知被组策略禁用。
    /// </summary>
    DisabledByGroupPolicy,

    /// <summary>
    /// 通知因清单或注册信息限制而不可用。
    /// </summary>
    DisabledByManifest,

    /// <summary>
    /// 当前系统不支持 Windows App Notification。
    /// </summary>
    Unsupported,

    /// <summary>
    /// 未知状态。
    /// </summary>
    Unknown
}

/// <summary>
/// 通知优先级。
/// </summary>
public enum LakeNotificationPriority
{
    /// <summary>
    /// 默认优先级。
    /// </summary>
    Default,

    /// <summary>
    /// 高优先级。
    /// </summary>
    High
}

/// <summary>
/// 通知横幅显示时长。
/// </summary>
public enum LakeNotificationDuration
{
    /// <summary>
    /// 系统默认时长。
    /// </summary>
    Default,

    /// <summary>
    /// 较长显示时长。
    /// </summary>
    Long
}

/// <summary>
/// 通知场景。不同场景会影响系统显示、声音和优先级行为。
/// </summary>
public enum LakeNotificationScenario
{
    /// <summary>
    /// 默认普通通知。
    /// </summary>
    Default,

    /// <summary>
    /// 闹钟场景。
    /// </summary>
    Alarm,

    /// <summary>
    /// 提醒场景。
    /// </summary>
    Reminder,

    /// <summary>
    /// 来电场景。
    /// </summary>
    IncomingCall,

    /// <summary>
    /// 紧急通知场景。使用前可调用 <see cref="LakeNotificationManager.IsUrgentScenarioSupported"/> 判断支持情况。
    /// </summary>
    Urgent
}

/// <summary>
/// 通知图片裁剪方式。
/// </summary>
public enum LakeNotificationImageCrop
{
    /// <summary>
    /// 默认裁剪。
    /// </summary>
    Default,

    /// <summary>
    /// 圆形裁剪。
    /// </summary>
    Circle
}

/// <summary>
/// 通知声音循环方式。
/// </summary>
public enum LakeNotificationAudioLooping
{
    /// <summary>
    /// 不循环。
    /// </summary>
    None,

    /// <summary>
    /// 循环播放。
    /// </summary>
    Loop
}

/// <summary>
/// Windows 内置通知声音事件。
/// </summary>
public enum LakeNotificationSoundEvent
{
    /// <summary>
    /// 默认通知声音。
    /// </summary>
    Default,

    /// <summary>
    /// 即时消息声音。
    /// </summary>
    IM,

    /// <summary>
    /// 邮件声音。
    /// </summary>
    Mail,

    /// <summary>
    /// 提醒声音。
    /// </summary>
    Reminder,

    /// <summary>
    /// 短信声音。
    /// </summary>
    SMS,

    /// <summary>
    /// 闹钟声音 1。
    /// </summary>
    Alarm,

    /// <summary>
    /// 闹钟声音 2。
    /// </summary>
    Alarm2,

    /// <summary>
    /// 闹钟声音 3。
    /// </summary>
    Alarm3,

    /// <summary>
    /// 闹钟声音 4。
    /// </summary>
    Alarm4,

    /// <summary>
    /// 闹钟声音 5。
    /// </summary>
    Alarm5,

    /// <summary>
    /// 闹钟声音 6。
    /// </summary>
    Alarm6,

    /// <summary>
    /// 闹钟声音 7。
    /// </summary>
    Alarm7,

    /// <summary>
    /// 闹钟声音 8。
    /// </summary>
    Alarm8,

    /// <summary>
    /// 闹钟声音 9。
    /// </summary>
    Alarm9,

    /// <summary>
    /// 闹钟声音 10。
    /// </summary>
    Alarm10,

    /// <summary>
    /// 来电声音 1。
    /// </summary>
    Call,

    /// <summary>
    /// 来电声音 2。
    /// </summary>
    Call2,

    /// <summary>
    /// 来电声音 3。
    /// </summary>
    Call3,

    /// <summary>
    /// 来电声音 4。
    /// </summary>
    Call4,

    /// <summary>
    /// 来电声音 5。
    /// </summary>
    Call5,

    /// <summary>
    /// 来电声音 6。
    /// </summary>
    Call6,

    /// <summary>
    /// 来电声音 7。
    /// </summary>
    Call7,

    /// <summary>
    /// 来电声音 8。
    /// </summary>
    Call8,

    /// <summary>
    /// 来电声音 9。
    /// </summary>
    Call9,

    /// <summary>
    /// 来电声音 10。
    /// </summary>
    Call10
}

/// <summary>
/// 通知按钮样式。
/// </summary>
public enum LakeNotificationButtonStyle
{
    /// <summary>
    /// 默认按钮样式。
    /// </summary>
    Default,

    /// <summary>
    /// 成功/确认样式。使用前可调用 <see cref="LakeNotificationManager.IsButtonStyleSupported"/> 判断支持情况。
    /// </summary>
    Success,

    /// <summary>
    /// 危险/严重样式。使用前可调用 <see cref="LakeNotificationManager.IsButtonStyleSupported"/> 判断支持情况。
    /// </summary>
    Critical
}

/// <summary>
/// 通知进度更新结果。
/// </summary>
public enum LakeNotificationProgressResult
{
    /// <summary>
    /// 更新成功。
    /// </summary>
    Succeeded,

    /// <summary>
    /// 没有找到匹配的通知。
    /// </summary>
    AppNotificationNotFound,

    /// <summary>
    /// 当前系统或通知不支持进度更新。
    /// </summary>
    Unsupported,

    /// <summary>
    /// 未知结果。
    /// </summary>
    Unknown
}
