Public Class GlobalOptions
    ''' <summary>
    ''' SSAA 超采样倍率。
    ''' </summary>
    ''' <remarks>
    ''' <para>数值同时表示图形层离屏渲染的宽高放大倍数：x2 = 2 倍宽高，x3 = 3 倍宽高，x4 = 4 倍宽高。</para>
    ''' <para>实际像素数、离屏 RT 显存与一次回采的像素量约按倍率平方增长：x2 约 4 倍，x3 约 9 倍，x4 约 16 倍。</para>
    ''' <para>OFF 的底层值为 1，表示不做全局强制，控件仍可使用自己的 SuperSamplingScale 设置。</para>
    ''' </remarks>
    Public Enum SuperSamplingScaleEnum
        ''' <summary>关闭全局 SSAA；图形层按 1x 绘制。</summary>
        OFF = 1
        ''' <summary>图形层以 2x2 像素绘制后缩回控件尺寸，约 4 倍像素量。</summary>
        x2 = 2
        ''' <summary>图形层以 3x3 像素绘制后缩回控件尺寸，约 9 倍像素量。</summary>
        x3 = 3
        ''' <summary>图形层以 4x4 像素绘制后缩回控件尺寸，约 16 倍像素量。</summary>
        x4 = 4
    End Enum

    Public Const 超采样抗锯齿描述词 As String = "使用 SSAA 超采样抗锯齿显著改善线条、圆角、弧线和几何边缘的观感；x2/x3/x4 的离屏像素量约为 4/9/16 倍，因此会按平方级增加显存、填充率和回采开销。该设置只作用于 V2 图形层；文字层和背景穿透层保持 1x，以避免 ClearType/DirectWrite 与第三方文字渲染兼容问题。"

    ''' <summary>
    ''' 全局 SSAA 倍率。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：OFF。</para>
    ''' <para>范围：OFF、x2、x3、x4。OFF 表示不强制全局倍率；x2/x3/x4 会在下一次控件重绘时作为全局 SSAA 设置参与计算。</para>
    ''' <para>影响范围：使用 D2DHelperV2.BeginPaint 的控件图形层。多数控件会取控件自身 SuperSamplingScale 与 GlobalSSAA 中较高的倍率；少数特化控件可能只读取全局值或明确不参与全局 SSAA。</para>
    ''' <para>性能影响：x2/x3/x4 的图形层离屏像素数约为 4/9/16 倍。</para>
    ''' </remarks>
    Public Shared Property GlobalSSAA As SuperSamplingScaleEnum = SuperSamplingScaleEnum.OFF

    ''' <summary>
    ''' D2D / DirectWrite 文本渲染质量模式。
    ''' </summary>
    ''' <remarks>
    ''' <para>该枚举只影响 D2DGlobals.ApplyGlobalQuality 设置到 RenderTarget 上的文字抗锯齿策略，不改变 SSAA 图形层倍率。</para>
    ''' <para>ClearType 最锐利，适合普通 WinForms 文本；Grayscale 更稳定，适合透明/半透明背景；Aliased 仅适合像素风或极小字号；Outline 会完全跳过 TrueType hinting，统一走几何轮廓渲染。</para>
    ''' </remarks>
    Public Enum TextQualityMode
        ''' <summary>ClearType 子像素渲染。默认值；锐度最高，并尽量兼容 MacType 等第三方文字渲染钩子。</summary>
        ClearType
        ''' <summary>灰度抗锯齿。不使用子像素颜色信息，彩边更少，透明背景和截图场景更稳定，但文字锐度略低。</summary>
        Grayscale
        ''' <summary>关闭文字抗锯齿。仅建议用于像素字体、像素艺术或刻意追求硬边的极小字号。</summary>
        Aliased
        ''' <summary>DirectWrite Outline 几何渲染。所有字号都按字形轮廓抗锯齿，跳过字体 hinting；小字号可能更柔，不能使用 ClearType 子像素。</summary>
        Outline
    End Enum

    ''' <summary>
    ''' 全局 D2D 图形抗锯齿模式。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：PerPrimitive。</para>
    ''' <para>影响范围：所有通过 D2DGlobals.ApplyGlobalQuality 初始化的 D2D RenderTarget，包括共享 DC RT 与 SSAA BitmapRT。</para>
    ''' <para>该设置只影响几何图形、线条、矩形、路径等 D2D 图形边缘，不影响 DirectWrite 文字质量；文字由 GlobalTextQuality 控制。</para>
    ''' <para>PerPrimitive 会对单个图元做抗锯齿，视觉质量较好；Aliased 可减少少量边缘计算但会明显增加锯齿，通常只适合像素级绘制。</para>
    ''' </remarks>
    Public Shared Property GlobalAntialiasMode As Vortice.Direct2D1.AntialiasMode = Vortice.Direct2D1.AntialiasMode.PerPrimitive

    ''' <summary>
    ''' 全局 D2D / DirectWrite 文本质量。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：ClearType。</para>
    ''' <para>影响范围：所有通过 D2DGlobals.ApplyGlobalQuality 初始化的 D2D RenderTarget 上的 DirectWrite 文本绘制。</para>
    ''' <para>ClearType 适合不透明背景和常规 UI；Grayscale 适合半透明背景、截图输出和避免彩边；Aliased 适合像素风；Outline 适合希望忽略字体 hinting、让小字号也强制走几何轮廓的场景。</para>
    ''' <para>性能影响通常小于 SSAA。Outline 会启用自定义 RenderingParams，文字边缘更统一，但极小字号可能更虚，且不会获得 ClearType 子像素锐度。</para>
    ''' </remarks>
    Public Shared Property GlobalTextQuality As TextQualityMode = TextQualityMode.ClearType

    ''' <summary>
    ''' Outline 文本质量模式的 DirectWrite 参数。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：Gamma=1.4、EnhancedContrast=0.5、GrayscaleEnhancedContrast=1.0、ClearTypeLevel=0、PixelGeometry=Rgb、RenderingMode=Outline、GridFitMode=Disabled。</para>
    ''' <para>这些参数只在 GlobalTextQuality = Outline 时生效。修改字段后请调用 GlobalOptions.InvalidateOutlineRenderingParams，让 D2DGlobals 在下一帧重建 DirectWrite RenderingParams。</para>
    ''' <para>Gamma 越高边缘过渡越重；EnhancedContrast / GrayscaleEnhancedContrast 越高笔画边缘越硬；ClearTypeLevel 在 Outline 模式通常保持 0，因为 Outline 采用灰度覆盖而不是子像素 ClearType。</para>
    ''' <para>GridFitMode.Disabled 会避免 TrueType 贴格带来的小字号硬折线，使几何轮廓更一致；如果需要更贴近系统默认小字号，可改为 Enabled 后刷新 RenderingParams。</para>
    ''' </remarks>
    Public Class OutlineTextOptions
        Public Property Gamma As Single = 1.4F
        Public Property EnhancedContrast As Single = 0.5F
        Public Property GrayscaleEnhancedContrast As Single = 1.0F
        Public Property ClearTypeLevel As Single = 0.0F
        Public Property PixelGeometry As Vortice.DirectWrite.PixelGeometry = Vortice.DirectWrite.PixelGeometry.Rgb
        Public Property RenderingMode As Vortice.DirectWrite.RenderingMode = Vortice.DirectWrite.RenderingMode.Outline
        Public Property GridFitMode As Vortice.DirectWrite.GridFitMode = Vortice.DirectWrite.GridFitMode.Disabled
    End Class

    ''' <summary>
    ''' Outline 文本质量模式的全局参数实例。
    ''' </summary>
    Public Shared ReadOnly Property OutlineText As New OutlineTextOptions()

    ''' <summary>
    ''' 丢弃 Outline 文本质量模式已缓存的 DirectWrite RenderingParams，让下一次绘制按当前 OutlineText 参数重建。
    ''' </summary>
    Public Shared Sub InvalidateOutlineRenderingParams()
        D2DGlobals.InvalidateOutlineRenderingParams()
    End Sub

    ''' <summary>
    ''' SSAA 离屏 RenderTarget 池的像素分桶粒度。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：64，单位为像素。</para>
    ''' <para>有效范围：建议 16 到 256。使用处会按至少 1 处理；设置为 0 或负数不会崩溃，但等价于 1，通常没有实际收益。</para>
    ''' <para>具体影响：控件需要的 SSAA 离屏尺寸会向上取整到该粒度。例如需要 301x101 像素且本值为 64 时，会租用 320x128 的 BitmapRenderTarget。</para>
    ''' <para>调大：更多相近尺寸会命中同一个桶，减少每帧创建/释放 RT 的抖动，但桶内多出来的像素会增加显存占用与清屏/回采成本。</para>
    ''' <para>调小：尺寸更贴合控件，节省显存与像素处理量，但不同尺寸更容易落入不同桶，窗口 resize、动画布局或列表项尺寸变化时复用率会下降。</para>
    ''' </remarks>
    Public Shared Property SsaaRenderTargetPoolBucketSize As Integer = 64

    ''' <summary>
    ''' 每个窗口 compositor 可保留的 SSAA 离屏 RenderTarget 池预算。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：256 MiB，单位为字节。该预算按窗口计算，每个顶层 Form 的 WindowCompositor 各自维护一份 SSAA RT 池。</para>
    ''' <para>有效范围：0 到 Long.MaxValue。使用处会按至少 0 处理；0 表示归还的 SSAA RT 不进入池，会立即释放。</para>
    ''' <para>估算公式：单个离屏 RT 显存约为 宽 x 高 x 4 字节，其中宽高已经包含 SSAA 倍率和分桶取整。例如 800x200 控件在 x2 下约为 1600x400x4 = 2.44 MiB。</para>
    ''' <para>调大：可保留更多不同尺寸的离屏 RT，减少频繁创建 Direct2D BitmapRenderTarget 带来的卡顿，代价是窗口空闲时也会占用更多显存。</para>
    ''' <para>调小：释放更积极，降低显存常驻量；当控件数量多、尺寸多或窗口频繁缩放时，可能增加 RT 重建次数。</para>
    ''' </remarks>
    Public Shared Property SsaaRenderTargetPoolBudgetBytes As Long = 256L * 1024L * 1024L

    ''' <summary>
    ''' 单个 Image 的 D2D 多 RenderTarget 上传缓存预算。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：64 MiB，单位为字节。该预算作用于每个 D2DBitmapCache，也就是同一 Image 在不同 RenderTarget 上的上传缓存。</para>
    ''' <para>有效范围：0 到 Long.MaxValue。使用处会按至少 0 处理；0 表示每次绘制后尽量不保留上传位图。</para>
    ''' <para>估算公式：单次上传缓存约为 Image.Width x Image.Height x 4 字节。例如 256x256 图标约 256 KiB，1920x1080 背景图约 7.9 MiB。</para>
    ''' <para>调大：图标、背景图、列表缩略图在 DC RT、SSAA RT 或不同窗口 RT 之间切换时更容易复用，减少 GDI Image 到 D2D Bitmap 的重复上传。</para>
    ''' <para>调小：降低长期持有的 D2D 位图显存；图片很多或超大背景图较多时更稳，但滚动和重绘时可能出现更多上传开销。</para>
    ''' </remarks>
    Public Shared Property D2DBitmapCacheBudgetBytes As Long = 64L * 1024L * 1024L

    ''' <summary>
    ''' 每个窗口 compositor 可保留的 Image 上传缓存索引数量。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：256。该限制只控制 Image -&gt; D2DBitmapCache 的强引用索引数量，不改变单个 Image 的上传缓存预算。</para>
    ''' <para>这主要用于运行时频繁替换图标 / 背景图的场景：旧 Image 若只被 compositor 字典持有，会在超过上限后按 LRU 释放。</para>
    ''' <para>调小会更快释放旧 Image 引用，但图片反复切换时会增加重新上传次数；调大则更偏向复用。</para>
    ''' </remarks>
    Public Shared Property D2DBitmapCacheMaxImagesPerCompositor As Integer = 256

    ''' <summary>
    ''' 每个 RenderTarget 可保留的纯色 D2D 画刷数量。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：256。动画插值颜色可能产生大量只用一两帧的 ARGB，超过上限后按近似 LRU 释放旧画刷。</para>
    ''' <para>该设置不影响颜色精度或视觉效果；命中率下降时只会增加少量 CreateSolidColorBrush 开销。</para>
    ''' </remarks>
    Public Shared Property D2DBrushCacheMaxEntriesPerRenderTarget As Integer = 256

    ''' <summary>
    ''' 每个窗口 compositor 可保留的 DirectWrite TextFormat 数量。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：128。TextFormat 与 RT 无关，适合复用；但主题/字体/字号频繁变化时也需要上限避免长期堆积。</para>
    ''' <para>该设置不缓存 TextLayout，因此不会改变文字排版结果；超过上限只会让较久未用的 TextFormat 在下次需要时重建。</para>
    ''' </remarks>
    Public Shared Property DWriteTextFormatCacheMaxEntriesPerCompositor As Integer = 128

    ''' <summary>
    ''' BackgroundPenetrationV2 可保留的 CPU 源位图预算。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：64 MiB，单位为字节。该预算只作用于 GDI backing bitmap，不影响 D2D 裁剪上传缓存数量。</para>
    ''' <para>纯色源会在识别后立即丢弃 backing bitmap；非纯色源超过预算时按 LRU 丢弃，下一次绘制会完整重采，视觉结果不变。</para>
    ''' </remarks>
    Public Shared Property BackgroundPenetrationSourceBitmapBudgetBytes As Long = 64L * 1024L * 1024L

    ''' <summary>
    ''' BackdropRenderer CPU blur 字节缓冲的保留上限。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：32 MiB，单位为字节，分别作用于 BoxBlur 的两块 scratch buffer。</para>
    ''' <para>GPU blur 可用时这些缓冲不是画质所需；CPU fallback 用完后若容量明显超出当前帧需求，会被缩回以释放 RAM。</para>
    ''' </remarks>
    Public Shared Property BackdropCpuBlurBufferRetainBytes As Long = 32L * 1024L * 1024L

    ''' <summary>
    ''' 每个 BackgroundSource 可保留的 D2D 裁剪上传缓存数量。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：24，单位为条目数。每个条目对应一个 source crop 到当前 RenderTarget 的 D2D 上传位图。</para>
    ''' <para>有效范围：0 到 Integer.MaxValue。使用处会按至少 0 处理；0 表示不保留裁剪上传缓存，每次透明背景穿透都重新上传当前裁剪区域。</para>
    ''' <para>估算公式：单个条目的显存约为 裁剪宽 x 裁剪高 x 4 字节。例如 300x80 的透明控件背景裁剪约 94 KiB，800x300 约 938 KiB。</para>
    ''' <para>调大：同一背景源下多个透明子控件、悬停动画脏区、小区域反复重绘时更容易命中缓存，减少裁剪 Bitmap 到 D2D Bitmap 的重复上传。</para>
    ''' <para>调小：降低每个背景源的裁剪缓存常驻量；透明控件很多且区域变化大时更省显存，但重复重绘的上传成本会增加。</para>
    ''' </remarks>
    Public Shared Property BackgroundPenetrationCropCacheMaxEntriesPerSource As Integer = 24


    Public Const 动画时长描述词 As String = "指定动画时长，单位为毫秒。0 表示禁用动画并立即跳到目标状态；100-180ms 适合按钮、开关、悬停等轻交互；200-300ms 适合列表滚动、展开收起和较明显的位置变化；超过 500ms 会显得拖沓，除非用于刻意的演示效果。"
    Public Const 动画帧率描述词 As String = "指定动画目标帧率，单位为 FPS。0 表示不限制，会尽可能快地请求重绘，容易明显增加 UI 线程和 D2D 绘制压力；30 适合低功耗或后台效果；60 适合大多数颜色、透明度和短距离位移动画；120 适合高刷新率屏幕上的精细滑动。超过显示器刷新率通常不会更顺滑，只会增加无效计算。"

End Class
