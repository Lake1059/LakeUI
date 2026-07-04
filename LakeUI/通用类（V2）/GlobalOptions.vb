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

    ''' <summary>SSAA 离屏 RenderTarget 池的像素分桶粒度。</summary>
    ''' <remarks>默认值：64。控件图形层离屏尺寸会向上取整到该粒度，越大越容易复用但会增加桶内多余像素。</remarks>
    Public Shared Property SsaaBucketSize As Integer = 64

    ''' <summary>进程级 GPU 缓存总预算。</summary>
    ''' <remarks>
    ''' <para>默认值：256 MiB。用于统一约束 SSAA RT、Image D2D 上传、背景穿透 D2D 上传、Backdrop GPU 目标与 Markdown D2D 图片缓存。</para>
    ''' <para>预算按进程总量计算，不再按窗口、图片或背景源分别设置。</para>
    ''' </remarks>
    Public Shared Property GpuCacheBudgetBytes As Long = 256L * 1024L * 1024L

    ''' <summary>进程级 CPU 位图缓存总预算。</summary>
    ''' <remarks>
    ''' <para>默认值：128 MiB。用于统一约束背景穿透 backing bitmap、Markdown 已加载 Image 和 Backdrop 抓屏/当前/备用帧。</para>
    ''' <para>预算按进程总量计算，清理到预算内时按全局 LRU 释放可重建条目。</para>
    ''' </remarks>
    Public Shared Property CpuCacheBudgetBytes As Long = 128L * 1024L * 1024L

    ''' <summary>纯色 D2D 画刷缓存条目上限。</summary>
    ''' <remarks>默认值：256。可靠字节估算不可得，因此画刷仍按条目数限制。</remarks>
    Public Shared Property BrushCacheLimit As Integer = 256

    ''' <summary>DirectWrite TextFormat 缓存条目上限。</summary>
    ''' <remarks>默认值：128。TextFormat 与 RT 无关，按条目数限制以避免主题、字体或字号频繁变化时长期堆积。</remarks>
    Public Shared Property TextFormatCacheLimit As Integer = 128

    ''' <summary>DirectWrite 字体名称解析缓存条目上限。</summary>
    ''' <remarks>默认值：128。缓存仅保存字体族名称和样式映射，不持有 Font/GDI 对象。</remarks>
    Public Shared Property FontResolveCacheLimit As Integer = 128

    ''' <summary>背景穿透局部脏区上限。</summary>
    ''' <remarks>默认值：8。连续小区域失效会先合并；超过上限后改为完整重采。</remarks>
    Public Shared Property BackgroundDirtyRectLimit As Integer = 8

    ''' <summary>背景穿透局部脏区触发完整重采的面积比例。</summary>
    ''' <remarks>默认值：0.6。多个脏区合并后接近 source 面积时，完整重采通常更便宜。</remarks>
    Public Shared Property BackgroundFullDirtyRatio As Single = 0.6F

    Public Const 动画时长描述词 As String = "指定动画时长，单位为毫秒。0 表示禁用动画并立即跳到目标状态；100-180ms 适合按钮、开关、悬停等轻交互；200-300ms 适合列表滚动、展开收起和较明显的位置变化；超过 500ms 会显得拖沓，除非用于刻意的演示效果。"
    Public Const 动画帧率描述词 As String = "指定动画目标帧率，单位为 FPS。0 表示不限制，会尽可能快地请求重绘，容易明显增加 UI 线程和 D2D 绘制压力；30 适合低功耗或后台效果；60 适合大多数颜色、透明度和短距离位移动画；120 适合高刷新率屏幕上的精细滑动。超过显示器刷新率通常不会更顺滑，只会增加无效计算。"

End Class
