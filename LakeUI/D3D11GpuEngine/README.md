# LakeUI D3D11 GPU Engine

此目录是 LakeUI V5 GPU 渲染核心的唯一实现区域；当前运行时主链路是 per-control V5 HWND swap-chain，`OnPaint` 仅负责触发首次/恢复呈现。

## 主链路

## 核心整理约束（强制）

- 本目录只允许保守改动：必须保持公开接口、线程模型、设备代号语义和父到子提交顺序不变；任何行为变化都必须有对应测试或明确的故障证据。
- 临时变量、一次性临时对象和局部缓存名称统一使用中文；对外公开成员、框架类型名、API 名称、协议字段和已有序列化键不得改名。
- 所有新增或修改的注释必须使用中文。代码字符串、着色器关键字、协议文本和第三方 API 标识不属于注释，不做翻译。
- 禁止无依据增加 `Try/Catch`、`IsNothing`、重复状态检查或递归保护。只有跨线程、设备丢失、对象释放和 WinForms 句柄竞态等可证明边界才保留保护；保护失败必须有可观察的降级行为。
- 热路径禁止创建可避免的临时集合、重复排序、重复上传或同步全量扫描。缓存淘汰必须使用现有 LRU/预算协调器，Present、逐帧动画和表面注册入口不得强制全量维护。
- 局部脏区若与控件完全不相交，直接丢弃请求；只有未提供有效区域时才升级为整控件失效。
- 每次整理后必须执行 `dotnet build LakeUI.slnx --no-restore`；涉及渲染生命周期、缓存或失效路由时，还必须运行对应测试并检查设备丢失与句柄销毁路径。

当前有效路线是：

V5 控件：`Control.OnPaint` -> `D3D_PaintBridge.PaintRenderable` -> `D3D_V5Presentation.Paint` -> `D3D_ControlSurface` -> `D3D_IGpuRenderable.RenderGpu` -> `D3D_HwndSwapChainPresenter.Present(0)`。

V5 不提供 HDC/Graphics 兼容桥；未迁移的原生控件继续使用 WinForms 自身绘制，不会进入 GPU 引擎。

关键约定：

- 每个控件只绘制自身坐标系内的像素；父子、兄弟和整窗重绘只通过 WinForms invalidation 合并。
- `RenderGpu` 只能使用传入的 `D3D_PaintContext`。不要缓存 context、device context、brush、bitmap、geometry、text format。
- 控件状态变化调用 `D3D_InvalidationRouter.RequestRender`；它会进入 `OuterToInnerRefreshScheduler` 合并并按外到内顺序刷新。不要直接 `Update`，也不要触发旧的整树刷新。
- `D3D_WindowCompositor` 只保留 Form 级共享缓存、文字/图片/Backdrop 服务和设备失效协调，不再创建 swapchain 或渲染整窗。
- `ReleaseEverything` 是完整资源重建边界，语义等同于驱动变动后的恢复：必须释放共享 D3D/D2D/DWrite/DXGI 资源、V5 surface 和 HWND presenter 的 GPU 对象，通过既有设备失效路径推进 generation，再按需重建。`RecreateDevice` 同样重建设备，但仍保留独立于设备的共享工厂；`ReleaseEverything` 还必须释放这些工厂，不得把旧设备族对象带入恢复帧。
- 完整重建仍必须保留窗口合成器、控件注册表、背景依赖关系、控件渲染对象以及可恢复渲染所需的权威 CPU 源快照；这些是逻辑状态，不属于旧设备资源。Backdrop 的权威图片/当前 CPU 帧可以保留并重新上传，备用帧、映射帧、模糊中间结果、旧纹理、旧 geometry/brush/text format、render target 和 presenter 都不必保留。
- 可见控件的 surface 容器和 HWND presenter 在完整重建时释放其 GPU 资源并原位恢复；预算清理则只能收缩可重建的中间缓存，不得淘汰仍可见的工作集。

## 当前核心边界

- `D3D_` 类型负责 D3D11/DXGI/D2D1.1/DirectWrite、Form 级共享 GPU 缓存、文字、背景穿透、Backdrop 以及 V5 swap-chain 呈现；D3D->HDC 合成只属于兼容桥。
- `D3D_` 类型负责控件契约、DPI、失效路由、树遍历和 GPU 资源生命周期。
- 已迁移控件必须在自己的 `OnPaint` 中输出像素；状态变化只请求 `Invalidate`，不主动绘制整窗。
- 旧的窗口级 swap-chain/render-host/full-tree compositor、HDR 子交换链镜像、DirectComposition 宿主和窗口级背景 snapshot 路线已从代码中移除。

## 设备丢失策略

设备丢失包括但不限于：驱动更新、TDR、系统休眠/恢复、远程桌面切换、显示适配器重置、`D2DERR_RECREATE_TARGET`、`DXGI_ERROR_DEVICE_REMOVED`、`DXGI_ERROR_DEVICE_RESET`、`DXGI_ERROR_DEVICE_HUNG`、`DXGI_ERROR_DRIVER_INTERNAL_ERROR`、`DXGI_ERROR_ACCESS_LOST`。

处理流程：

1. `D3D_DeviceManager.HandleDeviceLost` 将异常 `HResult` 规范化为 UInt32 后判断是否属于设备级错误，避免 DXGI 负数 HRESULT 比较失败。
2. `InvalidateDevice` 释放进程级 D3D/DXGI/D2D/DWrite 对象，并立即推进 `DeviceGeneration`。
3. `DeviceLost` 事件通知所有 Form 级 compositor 释放共享 target/context 和 GPU cache。
4. 如果错误发生在控件 `OnPaint` 的 D3D 绘制/合成过程中，本帧跳过并请求下一次 WinForms paint。
5. 下一次 `OnPaint` 会按新的 generation 重建设备、target 和缓存。

迁移控件必须把 `D3D_PaintContext.DeviceGeneration` 当作跨帧 GPU 资源有效性的唯一判据。`FrameGeneration` 只表示窗口帧序号，不能用于判断 D3D/D2D 资源是否还属于当前设备。

## 控件迁移规则

后续控件只允许通过 `D3D_IGpuRenderable.RenderGpu(context As D3D_PaintContext)` 绘制当前控件自身，并通过 `D3D_InvalidationRouter.RequestRender` 请求刷新。

禁止事项：

- 控件 `RenderGpu` 不得调用 `Graphics.GetHdc`、`BitBlt`、`PaintEventArgs` 或创建 HDC 目标。
- 不得自行创建 D3D/D2D/DXGI/DirectWrite device、factory、swap chain 或 render target。
- 不得持有跨帧 `ID2D1Brush`、`ID2D1Bitmap`、`ID2D1Geometry`、`IDWriteTextFormat` 等 GPU/DirectWrite 对象；长期资源必须交给 `D3D_` 缓存。
- 不得在控件内提交 `Present`、创建 swapchain 或 DirectComposition 宿主。
- 不得主动绘制父控件、兄弟控件或递归调用 WinForms paint。
- 不得重新引入窗口级 GPU 背景 snapshot；当前背景主链路只允许 `D3D_BackgroundPenetration`。
- 不得在 `RenderGpu` 内创建另一个 paint scope 或 HDC 路线，否则容易触发 reentrant factory/target 混用。

允许事项：

- 控件可以持有纯业务状态，例如颜色、文本、滚动位置、动画进度和数据模型。
- 控件可以在 `RenderGpu` 中调用 `D3D_PaintContext` 的矩形、图片、文字、clip 等绘制入口。
- 图片、文字格式、画刷、几何、背景上传和 blur intermediate 必须走 compositor 提供的缓存服务。

## 几何与 DPI

- 所有公开外观尺寸默认是逻辑像素；绘制前通过 `D3D_DpiContext` 或所属模块的 DPI helper 转换。
- 边框按 D2D 中心线绘制。填充背景若要和边框视觉外缘一致，应使用与边框相同的中心线矩形，或显式使用 inset helper。
- `Padding` 参与文本/内容布局时必须和边框宽度一起计算：`content = bounds - border - padding`。
- 顶层 popup/tooltip 在句柄创建前不要从自身读取 DPI；应优先使用锚点控件或 owner form 的 DPI。
- DirectWrite 字号统一走 `D3D_D2DInterop.GetDWriteFontSizePx` / `D3D_TextRenderer`，不要手写 `font.SizeInPoints * dpi / 72`。

## 背景与 Backdrop

`D3D_ControlSurfaceRegistry` 是当前唯一背景穿透主链路。它只采样 source 的持久 GPU surface，不创建 CPU backing bitmap，不生成窗口级截图。

Form 级 HDR/swapchain 呈现后端已移除，不再保留 `EnableHdrForForm`、HDR 状态查询或交换链验证入口。当前 HDR 只作为 V5 per-control 输出映射存在。

HDR 映射强度使用常见显示档位配置，不再直接暴露曝光/饱和度系数；默认值为 `HDR400`，可选 `HDR200` 到 `HDR1000` 的每 100 档位：

```vb
GlobalOptions.HDR.Enabled = True
GlobalOptions.HDR.Profile = GlobalOptions.HdrOutputProfile.HDR400
```

`D3D_BackdropRenderer` 当前实现 Image 模式 GPU 路线。Auto/CaptionOnly 的 Desktop Duplication 路线保留为后续核心能力，不能为了兼容普通 WinForms 控件重新引入 CPU 截图或 HDC 回贴。

背景穿透约定：

- 控件属性 setter 必须通过 `D3D_BackgroundPenetration.SetBackgroundSource` 注册 source；直接赋字段会丢失失效传播。
- `OnPaintBackground` 中若存在 `_backgroundSource` 应直接返回，避免 WinForms 先用 BackColor 清掉采样底图。
- `RenderGpu` 中的顺序是：`DrawBackgroundSource` -> 半透明 `BackColor` 遮罩 -> 控件自身主背景 -> 内容 -> 边框。
- `DrawBackgroundSource(consumer, source, destination)` 的 destination 是控件本地目标矩形；传 `0,0,w,h` 表示全控件，局部目标不要依赖隐式全控件回退。
- 防自照靠两点：显式 source 不采自己；背景采样内部排除当前 consumer。不要把 consumer 自身或其透明转发链错误设为 source。
- source 变化和 consumer 变化是两类失效。只有 source 内容变更才应置脏背景缓存；consumer hover/press 通常只请求自身重绘。
- source 的位置、大小、父级或 DPI 变化也必须传播到全部背景映射 consumer；映射矩形处于 source 坐标系，不能只重绘 source 自身。

图片背景的所有权约定：

- `D3D_BackdropRenderer.SetImage` 会在设置时复制调用方图片为 renderer-owned 32bpp 快照。调用方可以在 setter 返回后立即 Dispose 原图，后续 V5 延迟绘制只访问快照。
- 旧快照在当前 frame-use 计数归零后才释放，并同时从 `D3D_ImageCache` 移除；不得把调用方 `Image` 直接保存为跨帧 GPU 资源的唯一来源。

HDR 性能约定：

- HDR 图片映射发生在首次 GPU 上传的 CPU staging 阶段，并按图片 identity 与 HDR revision 缓存；不得在每帧主动失效图片上传缓存。
- HDR 曲线表由配置变更后的后台预热任务建立，绘制路径只保留同步兜底；新增 HDR 处理不能在 `RenderGpu` 中重建全局查表。
- HDR 矢量颜色缓存按 RGB 而不是完整 ARGB 复用；透明度动画只更新 alpha，不得因每帧 alpha 变化重复计算同一组 RGB 曲线。
- GPU/CPU 预算 LRU 扫描是节流维护入口，不能在 `Present`、surface 注册、背景帧结束或普通动画帧结束等热路径中强制全量扫描；由新资源创建触发的低频维护和显式降低预算/清理 API 负责回收。
- 动态动画颜色可能持续产生新画刷；`D3D_BrushCache` 必须使用 O(1) 命中/淘汰的 LRU 链表，禁止在每次颜色 miss 时对完整画刷字典排序或线性扫描。

### 纹理共享与预算

- 同一来源控件（包括 `ThisIsYourWindow` 宿主 Form）在 `D3D_ControlSurfaceRegistry` 中只创建一个持久 GPU surface；多个 Form/控件作为消费者时直接采样该 surface，不得为每个消费者复制底图或重新执行背景合成。
- 每个可见 HWND 的内容 surface 与 swap-chain 仍属于独立显示工作集。除非同时改变 HWND 呈现契约，否则不得把多个可见窗口的目标表面合并成一张共享 render target。
- `D3D_TextureCache` 在帧使用期间移除的纹理先进入退役队列，退役字节继续计入 GPU 预算，帧结束后统一释放；设备代次变化和显式清理必须清空该队列。
- 帧内新纹理触发的预算维护必须合并投递到原 WinForms UI 上下文，在绘制结束后的独立消息中执行；帧结束本身不扫描全局预算，不能直接丢弃尚未执行的维护请求。
- 画刷和文字格式的容量为零时仍保护即将返回给当前绘制的一个资源，后续不同资源请求或显式清理再释放它；不能返回已经 Dispose 的对象。
- 动画或窗口拖动帧结束只允许摘取退役队列并投递后台释放，不得在 UI 帧内执行全局预算扫描或批量 COM `Dispose`；预算扫描只能由新资源创建、显式清理或节流维护入口触发。
- CPU 预算统计必须覆盖背景源快照、当前/备用帧、映射帧、截图缓冲、待释放源图和模糊读回字节数组；任何新增 CPU staging 都必须同时接入统计和淘汰路径。

## 窗口铬与对话框

`ThisIsYourWindow` 挂接普通 Form 时，WinForms `Paint` 事件可以绘制标题栏。但 Form 自身若实现 `D3D_IGpuRenderable` 并在 `OnPaint` 成功后不调用 `MyBase.OnPaint`，挂接的 Paint 事件不会再执行。

这类窗体必须在自身 `RenderGpu` 内调用 `ThisIsYourWindow.TryRenderAttachedChrome(context, Me)`，让标题栏、按钮和边框进入同一次 V5 paint pass。客户区底色不能因为 `Padding` 被标题栏占用就跳过，只有 `ThisIsYourWindow.AttachedBackdropCoversClient(Me)` 为 True 时才可保持透明。

## Popup 与浮动提示

- `PopupForm` / `FloatingToolTipForm` / `ExFloatingTip` 是顶层 popup，不参与宿主控件的子控件树。
- popup 的 DPI 应来自 owner/anchor；句柄创建前自身 DPI 常常还不可靠。
- popup 的边框、圆角、padding、最大宽度、锚点间距、动画位移都按逻辑像素定义，显示前统一缩放。
- 边框要么按中心线 inset 后绘制，要么用填充四边，避免高 DPI 下半条边落到窗口外。
- popup backdrop 使用 `D3D_PopupBackdropRenderer` 的 GPU image backdrop；不要复用宿主 `ThisIsYourWindow` 的帧。

## WrongFactory 坑点

D2D 对象必须来自同一个 factory/device context 家族。典型错误是：用旧 helper 创建 geometry/brush/text format，再交给 V5 device context 绘制，最终在 `EndDraw` 抛 `D2DERR_WRONG_FACTORY`。

规则：

- V5 绘制使用 `D3D_RenderCore.DeviceManager.D2DFactory` 创建短期 geometry。
- brush 走 `context.Compositor.BrushCache.GetSolidBrush(...)`。
- text 走 `context.DrawText` / `D3D_TextRenderer`。
- 图片走 `context.DrawImage` / compositor image cache。
- 旧 `D3D_D2DInterop.GetD2DFactory()` 仅用于测量，不能和 V5 `ID2D1DeviceContext` 交叉使用。

## 文字路线

`D3D_TextRenderer` 是唯一文字绘制入口。每次绘制会话都会通过 `D3D_D2DInterop.ApplyGlobalQuality` 应用 `GlobalOptions.GlobalTextQuality`；Outline 会使用全局缓存的自定义 DirectWrite RenderingParams 强制走字形轮廓。后续文字策略仍必须在 `D3D_TextRenderer` 内扩展，迁移控件不能自建旧文字管线。
