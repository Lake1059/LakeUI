''' <summary>
''' LakeUI 全局渲染与缓存选项。
''' </summary>
''' <remarks>
''' <para>术语约定：“Image 图片”特指由 System.Drawing.Image / Bitmap 或文件、流、图标等外部图片资源进入控件的位图内容，例如图标、背景图片、Markdown 图片、列表缩略图。</para>
''' <para>“绘制的图形”特指控件代码用 D2D 画出来的圆角矩形、线条、路径、渐变、阴影、边框、填充色等矢量/图元内容；它们不是 Image 图片，也不会进入 Image 上传缓存。</para>
''' <para>“背景穿透/Backdrop 的采样位图”是为了复现父级或窗口背景而临时截取、模糊或上传的中间结果；它可能以 Bitmap 形式存在，但不等同于业务传入的 Image 图片。</para>
''' </remarks>
Public Class GlobalOptions
    ''' <summary>
    ''' SSAA 超采样倍率。
    ''' </summary>
    ''' <remarks>
    ''' <para>数值同时表示图形层离屏渲染的宽高放大倍数：x2 = 2 倍宽高，x3 = 3 倍宽高，x4 = 4 倍宽高。</para>
    ''' <para>实际像素数、离屏 RT 显存与一次回采的像素量约按倍率平方增长：x2 约 4 倍，x3 约 9 倍，x4 约 16 倍。</para>
    ''' <para>影响对象是控件自己绘制的 D2D 图形层，例如圆角、边框、线条、路径和填充；不代表 Image 图片会以更高分辨率重新解码，也不改变 Image 图片缓存预算。</para>
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

    Public Const 超采样抗锯齿描述词 As String = "使用 SSAA 超采样抗锯齿显著改善控件自己绘制的 D2D 图形边缘，例如线条、圆角、弧线、路径、边框和填充形状；x2/x3/x4 的离屏像素量约为 4/9/16 倍，因此会按平方级增加显存、填充率和回采开销。该设置只作用于 V2 图形层，不是 Image 图片解码倍率，也不改变图标、背景图片、Markdown 图片等 Image 图片缓存；文字层和背景穿透层保持 1x，以避免 ClearType/DirectWrite 与第三方文字渲染兼容问题。"

    ''' <summary>
    ''' 全局 SSAA 倍率。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：OFF。</para>
    ''' <para>范围：OFF、x2、x3、x4。OFF 表示不强制全局倍率；x2/x3/x4 会在下一次控件重绘时作为全局 SSAA 设置参与计算。</para>
    ''' <para>影响范围：使用 D2DHelperV2.BeginPaint 的控件图形层，主要是代码绘制出来的圆角、边框、路径、线条、填充、阴影等 D2D 图形。多数控件会取控件自身 SuperSamplingScale 与 GlobalSSAA 中较高的倍率；少数特化控件可能只读取全局值或明确不参与全局 SSAA。</para>
    ''' <para>不影响范围：不改变 System.Drawing.Image / Bitmap 的原始尺寸、解码方式、RAM 常驻，也不改变 Image -&gt; D2D Bitmap 上传缓存预算；图片只是作为内容被绘制到当前图形层中。</para>
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
    ''' <para>该设置只影响控件代码绘制的几何图形、线条、矩形、路径等 D2D 图形边缘，不影响 DirectWrite 文字质量；文字由 GlobalTextQuality 控制。</para>
    ''' <para>它也不影响 Image 图片的像素内容或缩放采样质量：图标、背景图、Markdown 图片等位图仍按对应图片绘制逻辑处理。</para>
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
    ''' 是否启用外到内刷新调度器。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：True。</para>
    ''' <para>启用后，部分 resize / layout / 背景穿透产生的刷新请求会被合并到下一次 UI 消息循环，并按控件树深度从外层容器到内层子控件派发。</para>
    ''' <para>该选项不改变控件绘制内容，只减少同一轮尺寸变化中重复、乱序的 Invalidate 风暴。若外部项目依赖立即 Invalidate 的边缘行为，可临时关闭。</para>
    ''' </remarks>
    Public Shared Property OuterToInnerRefreshSchedulerEnabled As Boolean = True

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
    ''' <para>具体影响：控件图形层需要的 SSAA 离屏尺寸会向上取整到该粒度。例如需要 301x101 像素且本值为 64 时，会租用 320x128 的 BitmapRenderTarget。</para>
    ''' <para>这里的 BitmapRenderTarget 是 D2D 绘制图形用的离屏画布，不是 System.Drawing.Image 图片缓存；它保存的是本帧图形层绘制结果。</para>
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
    ''' <para>影响对象：仅保留控件绘制图形层使用过的 D2D BitmapRenderTarget。它不保留业务 Image 图片对象，也不限制 Image 图片的 RAM 缓存。</para>
    ''' <para>调大：可保留更多不同尺寸的离屏 RT，减少频繁创建 Direct2D BitmapRenderTarget 带来的卡顿，代价是窗口空闲时也会占用更多显存。</para>
    ''' <para>调小：释放更积极，降低显存常驻量；当控件数量多、尺寸多或窗口频繁缩放时，可能增加 RT 重建次数。</para>
    ''' </remarks>
    Public Shared Property SsaaRenderTargetPoolBudgetBytes As Long = 256L * 1024L * 1024L

    ''' <summary>
    ''' 单个 Image 图片的 D2D 多 RenderTarget 上传缓存预算。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：64 MiB，单位为字节。该预算作用于每个 D2DBitmapCache，也就是同一个 System.Drawing.Image / Bitmap 在不同 RenderTarget 上的 D2D 上传副本缓存。</para>
    ''' <para>有效范围：0 到 Long.MaxValue。使用处会按至少 0 处理；0 表示每次绘制后尽量不保留上传位图。</para>
    ''' <para>估算公式：单次上传缓存约为 Image.Width x Image.Height x 4 字节。例如 256x256 图标约 256 KiB，1920x1080 背景图约 7.9 MiB。</para>
    ''' <para>影响对象：图标、背景图片、Markdown 图片、列表缩略图等真实 Image 图片。它不缓存控件自己画出来的圆角、线条、路径、填充、阴影等 D2D 绘制图形。</para>
    ''' <para>调大：图标、背景图、列表缩略图在 DC RT、SSAA RT 或不同窗口 RT 之间切换时更容易复用，减少 GDI Image 到 D2D Bitmap 的重复上传。</para>
    ''' <para>调小：降低长期持有的 D2D 位图显存；图片很多或超大背景图较多时更稳，但滚动和重绘时可能出现更多上传开销。</para>
    ''' </remarks>
    Public Shared Property D2DBitmapCacheBudgetBytes As Long = 64L * 1024L * 1024L

    ''' <summary>
    ''' 每个窗口 compositor 可保留的 Image 图片上传缓存索引数量。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：256。该限制只控制 System.Drawing.Image / Bitmap -&gt; D2DBitmapCache 的强引用索引数量，不改变单个 Image 图片的上传缓存预算。</para>
    ''' <para>这主要用于运行时频繁替换图标 / 背景图 / Markdown 图片 / 缩略图的场景：旧 Image 若只被 compositor 字典持有，会在超过上限后按 LRU 释放。</para>
    ''' <para>不影响对象：控件自己绘制的 D2D 图形没有 Image 索引，不会因为本值增大而被缓存成图片；圆角、线条、路径等仍按绘制逻辑生成。</para>
    ''' <para>调小会更快释放旧 Image 引用，但图片反复切换时会增加重新上传次数；调大则更偏向复用。</para>
    ''' </remarks>
    Public Shared Property D2DBitmapCacheMaxImagesPerCompositor As Integer = 256

    ''' <summary>
    ''' 每个 RenderTarget 可保留的纯色 D2D 画刷数量。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：256。动画插值颜色可能产生大量只用一两帧的 ARGB，超过上限后按近似 LRU 释放旧画刷。</para>
    ''' <para>影响对象：SolidColorBrush 这类绘制图形用资源，例如填充色、边框色、路径颜色；它不是 Image 图片缓存，也不会持有 Bitmap / Image 对象。</para>
    ''' <para>该设置不影响颜色精度或视觉效果；命中率下降时只会增加少量 CreateSolidColorBrush 开销。</para>
    ''' </remarks>
    Public Shared Property D2DBrushCacheMaxEntriesPerRenderTarget As Integer = 256

    ''' <summary>
    ''' 每个窗口 compositor 可保留的 DirectWrite TextFormat 数量。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：128。TextFormat 与 RT 无关，适合复用；但主题/字体/字号频繁变化时也需要上限避免长期堆积。</para>
    ''' <para>影响对象：DirectWrite 字体族、字号、字重、样式等文本格式描述；它不保存文本内容，不保存 TextLayout，也不涉及 Image 图片或 D2D 绘制图形缓存。</para>
    ''' <para>该设置不缓存 TextLayout，因此不会改变文字排版结果；超过上限只会让较久未用的 TextFormat 在下次需要时重建。</para>
    ''' </remarks>
    Public Shared Property DWriteTextFormatCacheMaxEntriesPerCompositor As Integer = 128

    ''' <summary>
    ''' 全局 DirectWrite 字体名称解析缓存上限。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：128。缓存只保存字体族名称和样式映射，不持有 Font/GDI 对象；动态字体名超过上限后按 LRU 移除。</para>
    ''' <para>影响对象仅是字体名称解析结果，不保存 Image 图片，也不保存控件绘制出来的几何图形。</para>
    ''' <para>该设置不改变字体回退结果，缓存未命中时仅重新查询一次系统字体集合。</para>
    ''' </remarks>
    Public Shared Property DWriteFontResolveCacheMaxEntries As Integer = 128

    ''' <summary>
    ''' BackgroundPenetrationV2 可保留的 CPU 源位图预算。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：64 MiB，单位为字节。该预算只作用于背景穿透为复现父级背景而采样得到的 CPU backing bitmap，不影响 D2D 裁剪上传缓存数量。</para>
    ''' <para>这里的 backing bitmap 是背景采样中间结果：它可能来自父控件绘制、窗口背景、纯色或图片背景的组合，但不是业务直接传入的 Image 图片缓存，也不是控件的圆角/路径等绘制图形缓存。</para>
    ''' <para>纯色源会在识别后立即丢弃 backing bitmap；非纯色源超过预算时按 LRU 丢弃，下一次绘制会完整重采，视觉结果不变。</para>
    ''' <para>兼容说明：BackgroundPenetrationV2 会把 1 到 1024 的正数视为 MiB 档位，以兼容旧设置界面把 16/32/64 这类档位直接写入 bytes 属性的情况。</para>
    ''' </remarks>
    Public Shared Property BackgroundPenetrationSourceBitmapBudgetBytes As Long = 64L * 1024L * 1024L

    ''' <summary>
    ''' BackgroundPenetrationV2 每个背景源保留的局部脏区上限。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：8。连续小区域失效会先合并相交矩形；超过上限后改为一次完整重采，避免维护大量脏区列表和多次 InvokePaint。</para>
    ''' <para>影响对象是背景采样区域的刷新策略，不是 Image 图片数量，也不是 D2D 图形缓存数量。</para>
    ''' <para>该设置不改变视觉结果，只在局部失效过于碎片化时用更少的 CPU 管理成本换取一次较大的重采。</para>
    ''' </remarks>
    Public Shared Property BackgroundPenetrationDirtyRectMaxCount As Integer = 8

    ''' <summary>
    ''' BackgroundPenetrationV2 局部脏区合并后触发完整重采的面积比例。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：0.6。当多个脏区外接矩形面积已经接近 source 面积时，继续逐块重采通常比完整重采更贵。</para>
    ''' <para>影响对象是背景穿透的局部采样合并策略；它决定何时从多块局部背景采样切换为整块背景重采，不处理 Image 图片缓存，也不改变控件绘制图形的抗锯齿质量。</para>
    ''' <para>有效范围按 0.05 到 1.0 夹取；视觉结果不变。</para>
    ''' </remarks>
    Public Shared Property BackgroundPenetrationFullDirtyAreaRatio As Single = 0.6F

    ''' <summary>
    ''' BackdropRenderer CPU blur 字节缓冲的保留上限。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：32 MiB，单位为字节，分别作用于 BoxBlur 的两块 scratch buffer。</para>
    ''' <para>影响对象是 CPU 模糊算法临时使用的字节数组，用于存放当前帧采样像素和中间计算结果；它不是 Image 图片缓存，也不是 D2D 绘制图形资源。</para>
    ''' <para>GPU blur 可用时这些缓冲不是画质所需；CPU fallback 用完后若容量明显超出当前帧需求，会被缩回以释放 RAM。</para>
    ''' </remarks>
    Public Shared Property BackdropCpuBlurBufferRetainBytes As Long = 32L * 1024L * 1024L

    ''' <summary>
    ''' BackgroundPenetrationV2 可保留的 D2D 裁剪上传缓存全局预算。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：64 MiB，单位为字节。该预算按所有 BackgroundSource 的 D2D source crop 上传副本总量计算，而不是按单个背景源分别计算。</para>
    ''' <para>生命周期释放仍优先于预算回收：隐藏、离开可见控件链、句柄销毁或 Dispose 的 source 会主动释放缓存；仍处于可渲染状态的 source 超过预算时按 LRU 丢弃旧裁剪。</para>
    ''' <para>该设置只影响背景穿透从 CPU backing bitmap 上传到 RenderTarget 的局部裁剪 D2D 位图，不影响业务 Image 图片缓存，也不影响 CPU backing bitmap 预算。</para>
    ''' <para>调大：多个已初始化页面或同一背景源下大量透明子控件来回切换时更容易命中缓存，代价是更高的显存常驻量。</para>
    ''' <para>调小：显存释放更积极，但局部背景会更频繁重新上传。</para>
    ''' </remarks>
    Public Shared Property BackgroundPenetrationCropCacheBudgetBytes As Long = 64L * 1024L * 1024L

    ''' <summary>
    ''' 每个 BackgroundSource 可保留的 D2D 裁剪上传缓存数量。
    ''' </summary>
    ''' <remarks>
    ''' <para>默认值：32，单位为条目数。每个条目对应一个背景源局部裁剪 source crop 到当前 RenderTarget 的 D2D 上传位图。</para>
    ''' <para>有效范围：0 到 Integer.MaxValue。使用处会按至少 0 处理；0 表示不保留裁剪上传缓存，每次透明背景穿透都重新上传当前裁剪区域。</para>
    ''' <para>估算公式：单个条目的显存约为 裁剪宽 x 裁剪高 x 4 字节。例如 300x80 的透明控件背景裁剪约 94 KiB，800x300 约 938 KiB。</para>
    ''' <para>影响对象：背景穿透已经采样出的局部背景位图上传副本。它不是业务 Image 图片缓存，也不是圆角、边框、路径等控件绘制图形缓存。</para>
    ''' <para>调大：同一背景源下多个透明子控件、悬停动画脏区、小区域反复重绘时更容易命中缓存，减少裁剪 Bitmap 到 D2D Bitmap 的重复上传。</para>
    ''' <para>调小：降低每个背景源的裁剪缓存常驻量；透明控件很多且区域变化大时更省显存，但重复重绘的上传成本会增加。</para>
    ''' </remarks>
    Public Shared Property BackgroundPenetrationCropCacheMaxEntriesPerSource As Integer = 32

    Public Const 动画时长描述词 As String = "指定动画时长，单位为毫秒。0 表示禁用动画并立即跳到目标状态；100-180ms 适合按钮、开关、悬停等轻交互；200-300ms 适合列表滚动、展开收起和较明显的位置变化；超过 500ms 会显得拖沓，除非用于刻意的演示效果。"
    Public Const 动画帧率描述词 As String = "指定动画目标帧率，单位为 FPS。0 表示不限制，会尽可能快地请求重绘，容易明显增加 UI 线程和 D2D 绘制压力；30 适合低功耗或后台效果；60 适合大多数颜色、透明度和短距离位移动画；120 适合高刷新率屏幕上的精细滑动。超过显示器刷新率通常不会更顺滑，只会增加无效计算。"

End Class
