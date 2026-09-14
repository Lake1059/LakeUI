# LakeUI D3D11 GPU Engine

此目录是 LakeUI V5 GPU 渲染核心的唯一实现区域；当前运行时主链路是每控件持久 GPU surface + HWND DirectComposition surface，同一批次由共享 composition device 一次 `Commit`。`OnPaint` 仅负责触发首次/恢复呈现。

## 主链路

## 初始化与整页刷新

从外到内是强制约束。`BeginRenderUpdate` 在 UI 线程累积初始化、布局和可见性变更，最外层
作用域退出时统一派发。初始化多控件批次先按深度准备内容表面及合成目标，再按相同顺序更新合成表面，最后统一发布；不会并行
执行 `RenderGpu`，也不会在后台访问控件。不要跨 `Await`、模态对话框或消息循环持有事务。

```vb
Using D3D_PaintBridge.BeginRenderUpdate(页面根控件)
    页面根控件.SuspendLayout()
    Try
        ' 批量设置属性、增加控件、绑定背景和数据。
    Finally
        页面根控件.ResumeLayout(True)
    End Try
End Using
```

两个选项卡控件已自动使用事务。存在旧页时，旧页保持在新页前方，整批提交尝试完成后才
切换层次并隐藏旧页；不截屏、不生成窗口级备份。同一 composition device 的更新通过一次事务发布，
但 `Commit` 返回不代表已经扫描上屏；设备丢失或句柄竞态仍由后续重试恢复。首个窗口显示、
原生控件绘制和 WinForms HWND 层次变化不属于这次合成事务。

`ModernTabListControl.ModernTabPage.BoundControlFactory` 仅在首次选中时创建页面，后续复用
`BoundControl`。工厂运行于 UI 线程，读取 `BoundControl` 或遍历隐藏页不会触发构造。
DEMO 使用该接口，启动只构造首个页面。

`D3D_ImagePreparation.LoadBitmapAsync` 使用最多两个后台工作任务读取文件并将首帧解码为
独占 PArgb 位图。返回值由调用方拥有；须在 UI 线程接收结果，并处理取消、页面释放及过期
结果。它不上传 GPU，也不预先应用 HDR；HDR 仍按当前配置在上传时处理。动画图片沿用原有
多帧路径。DEMO 的图片拖放展示了取消旧请求和释放旧结果的用法。

短文本测量结果缓存最多 512 项，包含字体、尺寸、格式、DPI 等键，参与现有 CPU 预算和
LRU 淘汰；格式失效与资源清理会清空缓存，不保留 Font、Control 或 COM 对象。

### 验证与计时

开启 `V5ProbeEnabled` 并调用 `ResetV5Probe()` 后，`GetV5RefreshTimings()` 返回有界、无控件
强引用的分段统计。`RenderGpu`、`PresenterResources`、`Present` 分别记录内容准备、
呈现资源和控件合成表面更新；`CompositionCommit` 记录整批发布，`CompositionPublished` 按控件
记录成功发布次数。只有 `Commit` 成功才增加 `SubmittedFrames` 和确认控件修订号。
这些都是 CPU 墙钟统计，不代表扫描上屏；`CompositionPublished` 的耗时共享同一次提交，不能跨控件相加。
全局帧间隔记录不同批次的发布时间，不再把同一批次的多个控件计为相邻屏幕帧。
`UpdateScope` 只计作用域内的变更，不包含退出时的提交。

```powershell
dotnet run --project LakeUI.Tests -c Release -- --refresh-probe
dotnet run --project LakeUI.Tests -c Release -- --demo-refresh <LakeUI.Demo.dll路径>
```

40 控件探针比较同步逐项请求与事务合并，并输出总同步耗时及首次到末次提交跨度。
旧的逐控件 DXGI Present 在本机出现总提交约 120 次/秒的瓶颈，五控件各约 24 FPS；
新后端每批只调用一次 `Commit`。引擎不会修改驱动配置，不设置 `ALLOW_TEARING`，由 DWM 默认同步合成。
设备故障保留 250ms 退避；重试复用已完成表面，并重新加入外到内批次。

### 持续动画

共享动画时钟保持 `PrecisionTimer.Blocking`：等待本次 UI 回调完成后再安排唤醒，
`OverrunPolicy.Queue` 在该模式下不生效，不存在补执行过期 Tick 的队列。
每次 Tick 先采样全部 helper，再经 `OuterToInnerRefreshScheduler.FlushPendingRequests`
按树深派发失效，最后由 `D3D_V5Presentation.FlushPendingFrame` 按相同深度规则提交。
这样动画不需要额外等待批次的 WinForms Timer，FPS=0 也不会只增加采样而延迟实际绘制。
持续动画使用单遍提交，初始化事务保留两阶段准备；外到内顺序、事务屏蔽和输入处理均须保留。

```powershell
dotnet build LakeUI.Tests/LakeUI.Tests.csproj -c Release --no-restore
dotnet LakeUI.Tests/bin/Release/net10.0-windows10.0.17763.0/LakeUI.Tests.dll --animation-probe 60
dotnet LakeUI.Tests/bin/Release/net10.0-windows10.0.17763.0/LakeUI.Tests.dll --animation-probe 0
dotnet LakeUI.Tests/bin/Release/net10.0-windows10.0.17763.0/LakeUI.Tests.dll --animation-probe 120 10
dotnet LakeUI.Tests/bin/Release/net10.0-windows10.0.17763.0/LakeUI.Tests.dll --composition-probe
```

动画探针同时运行两种加载动画、进度条、仪表盘和按钮涟漪，使用真实消息循环，默认两秒自动关闭，
可指定持续秒数，另有独立超时保护。输出每控件绘制/成功发布次数、发布速率和 DWM 合成刷新率。
超过两秒的测试使用足够长的缓出动画段，避免动画结束或缓入阶段低于既有失效阈值造成主动跳帧。
UI 的 15ms 采样只用于检测卡顿，不能用其观察次数判断 120 FPS。
合成探针通过内部 GPU 读回验证多层及超容器背景映射、来源独立失效、几何变化和句柄/设备恢复，
再验证 30/60/120 混合动画及五个嵌套 120 FPS 动画；不截图、不修改系统显示设置。
控件的动画状态独立到期，背景来源变化仍可使低帧率消费者额外重绘；依赖重绘不会推进其动画状态。
达到 120 FPS 仍要求整批绘制和合成工作能在约 8.33ms 内完成，不能保证任意数量/复杂度控件跑满。
本机默认同步、DWM 120Hz 下，十秒测试五个真实控件各绘制并成功发布 1201 帧（各约 120.0 FPS），
共享提交 1201 次。GPU 读回和修订号检查通过，混合帧率约 30/60/119 FPS；
这些内部探针验证内容及成功发布，不把 DWM 刷新率或 `Commit` 返回当成逐控件扫描上屏证明。

### 静态内容与几何变化

上下文菜单和下拉框在展开/关闭时保持完整 HWND 和 GPU 表面尺寸，只更新窗口 Region。
完整表面已经包含全部内容，因此纯裁剪变化不再显式请求重绘或 Present。内容、字体、
真实尺寸和背景来源变化仍通过原有失效路径更新，不冻结背景映射，不引入 CPU 截图或
窗口级合成器。阴影只在外观配置变化时强制清空，尺寸及圆角变化继续由原有缓存检测。

所有 V5 控件的位置、尺寸、父级与可见性事件统一合并到外到内提交批次。隐藏子树仍释放
资源；几何变化仍将表面及背景坐标标脏，同一轮布局不再同步提交每个中间状态。显式
RequestRender 的同步语义保持不变。

```powershell
dotnet LakeUI.Tests/bin/Release/net10.0-windows10.0.17763.0/LakeUI.Tests.dll --menu-animation-probe shadow
dotnet LakeUI.Tests/bin/Release/net10.0-windows10.0.17763.0/LakeUI.Tests.dll --dropdown-animation-probe
```

两个探针均使用自动关闭及超时保护，不截图。分别验证父菜单焦点持续更新、子菜单裁剪
完成、Classic/Overlay 裁剪完成、展开时真实内容失效仍生效，以及静态内容不逐帧重建。

## 核心整理约束（强制）

- 本目录只允许保守改动：必须保持公开接口、线程模型、设备代号语义和父到子提交顺序不变；任何行为变化都必须有对应测试或明确的故障证据。
- 临时变量、一次性临时对象和局部缓存名称统一使用中文；对外公开成员、框架类型名、API 名称、协议字段和已有序列化键不得改名。
- 所有新增或修改的注释必须使用中文。代码字符串、着色器关键字、协议文本和第三方 API 标识不属于注释，不做翻译。
- 禁止无依据增加 `Try/Catch`、`IsNothing`、重复状态检查或递归保护。只有跨线程、设备丢失、对象释放和 WinForms 句柄竞态等可证明边界才保留保护；保护失败必须有可观察的降级行为。
- 热路径禁止创建可避免的临时集合、重复排序、重复上传或同步全量扫描。缓存淘汰必须使用现有 LRU/预算协调器，Present、逐帧动画和表面注册入口不得强制全量维护。
- 局部脏区若与控件完全不相交，直接丢弃请求；只有未提供有效区域时才升级为整控件失效。
- 每次整理后必须执行 `dotnet build LakeUI.slnx --no-restore`；涉及渲染生命周期、缓存或失效路由时，还必须运行对应测试并检查设备丢失与句柄销毁路径。

当前有效路线是：

V5 控件：`Control.OnPaint` -> `D3D_PaintBridge.PaintRenderable` -> `D3D_V5Presentation.Paint` -> `D3D_ControlSurface` -> `D3D_IGpuRenderable.RenderGpu` -> `D3D_HwndCompositionPresenter.Present` -> 批次末尾 `IDCompositionDevice.Commit`。

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

- `D3D_` 类型负责 D3D11/DXGI/D2D1.1/DirectWrite、Form 级共享 GPU 缓存、文字、背景穿透、Backdrop 以及 V5 DirectComposition 呈现；D3D->HDC 合成只属于兼容桥。
- `D3D_` 类型负责控件契约、DPI、失效路由、树遍历和 GPU 资源生命周期。
- 已迁移控件必须在自己的 `OnPaint` 中输出像素；状态变化只请求 `Invalidate`，不主动绘制整窗。
- 旧的窗口级 swap-chain/render-host/full-tree compositor、HDR 子交换链镜像和窗口级背景 snapshot 路线不再使用。当前 DirectComposition 只为各控件 HWND 提供独立目标和表面，不建立整窗背景副本。

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
- 每个可见 HWND 的内容 surface 与 composition surface 仍属于独立显示工作集；共享的是设备和发布事务。合成表面按逻辑像素字节计量，DWM 内部额外缓冲不属于可观测缓存；不得合并成整窗背景副本。
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
