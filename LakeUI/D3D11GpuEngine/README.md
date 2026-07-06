# LakeUI D3D11 GPU Engine

此目录是 LakeUI 新主版本 GPU 渲染核心的唯一新增区域。V3 核心、兼容辅助与迁移中的渲染工具文件统一放在本目录根级；当前运行时主链路是 WinForms per-control `OnPaint` + `D3D_PaintBridge`。

## 主链路

当前有效路线是：

`Control.OnPaint` -> `D3D_PaintBridge.PaintRenderable` -> `D3D_PaintScope.CreateContext` -> `V3_IGpuRenderable.RenderGpu` -> `D3D_PaintScope.Dispose` 合成回当前 Paint HDC。

关键约定：

- 每个控件只绘制自身坐标系内的像素；父子、兄弟和整窗重绘只通过 WinForms invalidation 合并。
- `RenderGpu` 只能使用传入的 `D3D_PaintContext`。不要缓存 context、device context、brush、bitmap、geometry、text format。
- 控件状态变化调用 `V3_InvalidationRouter.RequestRender`；它会进入 `OuterToInnerRefreshScheduler` 合并并按外到内顺序刷新。不要直接 `Update`，也不要触发旧的整树刷新。
- `D3D_WindowCompositor` 的 swap-chain/window-frame 代码只保留给核心验证和后续能力，不是控件刷新入口。

## 当前核心边界

- `D3D_` 类型负责 D3D11/DXGI/D2D1.1/DirectWrite、Form 级共享 GPU 缓存、文字、背景穿透、Backdrop 和最终 D3D->HDC 合成。
- `V3_` 类型只描述后续控件迁移契约、DPI、失效路由、树遍历和迁移标记，不隐藏 GPU 资源创建。
- 已迁移控件必须在自己的 `OnPaint` 中输出像素；状态变化只请求 `Invalidate`，不主动绘制整窗。
- 旧的窗口级 swap-chain/render-host/full-tree compositor 路线已从主链路移除；保留的 swap-chain 验证代码不得作为控件刷新入口。
- DirectComposition 宿主作为独立边界保留；当前不作为阻塞项。后续启用时必须继续遵守 device generation、UI 线程和 per-control paint 生命周期。

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

后续控件只允许通过 `V3_IGpuRenderable.RenderGpu(context As D3D_PaintContext)` 绘制当前控件自身，并通过 `V3_InvalidationRouter.RequestRender` 请求刷新。

禁止事项：

- 控件 `RenderGpu` 不得调用 `Graphics.GetHdc`、`BitBlt`、`PaintEventArgs`、旧 `PaintScopeV2` 或旧背景穿透路径；最终 HDC 合成只允许在 `D3D_PaintScope` 内部完成。
- 不得自行创建 D3D/D2D/DXGI/DirectWrite device、factory、swap chain 或 render target。
- 不得持有跨帧 `ID2D1Brush`、`ID2D1Bitmap`、`ID2D1Geometry`、`IDWriteTextFormat` 等 GPU/DirectWrite 对象；长期资源必须交给 `D3D_` 缓存。
- 不得在控件内提交 `Present` 或 DirectComposition `Commit`。
- 不得主动绘制父控件、兄弟控件或递归调用 WinForms paint。
- 不得把 `D3D_BackgroundGraph` 重新接回主链路；当前背景主链路只允许 `D3D_BackgroundPenetration`。
- 不得在 `RenderGpu` 内创建另一个 paint scope 或 HDC 路线，否则容易触发 reentrant factory/target 混用。

允许事项：

- 控件可以持有纯业务状态，例如颜色、文本、滚动位置、动画进度和数据模型。
- 控件可以在 `RenderGpu` 中调用 `D3D_PaintContext` 的矩形、图片、文字、clip 等绘制入口。
- 图片、文字格式、画刷、几何、背景上传和 blur intermediate 必须走 compositor 提供的缓存服务。

## 几何与 DPI

- 所有公开外观尺寸默认是逻辑像素；绘制前通过 `V3_DpiContext` 或所属模块的 DPI helper 转换。
- 边框按 D2D 中心线绘制。填充背景若要和边框视觉外缘一致，应使用与边框相同的中心线矩形，或显式使用 inset helper。
- `Padding` 参与文本/内容布局时必须和边框宽度一起计算：`content = bounds - border - padding`。
- 顶层 popup/tooltip 在句柄创建前不要从自身读取 DPI；应优先使用锚点控件或 owner form 的 DPI。
- DirectWrite 字号统一走 `D3D_D2DInterop.GetDWriteFontSizePx` / `D3D_TextRenderer`，不要手写 `font.SizeInPoints * dpi / 72`。

## 背景与 Backdrop

`D3D_BackgroundPenetration` 是当前唯一背景穿透主链路。它只在显式 `BackgroundSource` 存在时采样 source，使用 CPU backing bitmap + D2D 上传缓存绘制到当前控件 paint scope，不生成窗口级 GPU snapshot，也不递归绘制整棵控件树。

`D3D_BackdropRenderer` 当前实现 Image 模式 GPU 路线。Auto/CaptionOnly 的 Desktop Duplication 路线保留为后续核心能力，不能为了兼容普通 WinForms 控件重新引入 CPU 截图或 HDC 回贴。

背景穿透约定：

- 控件属性 setter 必须通过 `D3D_BackgroundPenetration.SetBackgroundSource` 注册 source；直接赋字段会丢失失效传播。
- `OnPaintBackground` 中若存在 `_backgroundSource` 应直接返回，避免 WinForms 先用 BackColor 清掉采样底图。
- `RenderGpu` 中的顺序是：`DrawBackgroundSource` -> 半透明 `BackColor` 遮罩 -> 控件自身主背景 -> 内容 -> 边框。
- `DrawBackgroundSource(consumer, source, destination)` 的 destination 是控件本地目标矩形；传 `0,0,w,h` 表示全控件，局部目标不要依赖隐式全控件回退。
- 防自照靠两点：显式 source 不采自己；背景采样内部排除当前 consumer。不要把 consumer 自身或其透明转发链错误设为 source。
- source 变化和 consumer 变化是两类失效。只有 source 内容变更才应置脏背景缓存；consumer hover/press 通常只请求自身重绘。

## 窗口铬与对话框

`ThisIsYourWindow` 挂接普通 Form 时，WinForms `Paint` 事件可以绘制标题栏。但 Form 自身若实现 `V3_IGpuRenderable` 并在 `OnPaint` 成功后不调用 `MyBase.OnPaint`，挂接的 Paint 事件不会再执行。

这类窗体必须在自身 `RenderGpu` 内调用 `ThisIsYourWindow.TryRenderAttachedChrome(context, Me)`，让标题栏、按钮和边框进入同一次 V3 paint pass。客户区底色不能因为 `Padding` 被标题栏占用就跳过，只有 `ThisIsYourWindow.AttachedBackdropCoversClient(Me)` 为 True 时才可保持透明。

## Popup 与浮动提示

- `PopupForm` / `FloatingToolTipForm` / `ExFloatingTip` 是顶层 popup，不参与宿主控件的子控件树。
- popup 的 DPI 应来自 owner/anchor；句柄创建前自身 DPI 常常还不可靠。
- popup 的边框、圆角、padding、最大宽度、锚点间距、动画位移都按逻辑像素定义，显示前统一缩放。
- 边框要么按中心线 inset 后绘制，要么用填充四边，避免高 DPI 下半条边落到窗口外。
- popup backdrop 使用 `D3D_PopupBackdropRenderer` 或 V3 image backdrop；不要复用宿主 `ThisIsYourWindow` 的帧。

## WrongFactory 坑点

D2D 对象必须来自同一个 factory/device context 家族。典型错误是：用旧 V2 helper 创建 geometry/brush/text format，再交给 V3 device context 绘制，最终在 `EndDraw` 或 scope dispose 抛 `D2DERR_WRONG_FACTORY`。

规则：

- V3 绘制使用 `D3D_RenderCore.DeviceManager.D2DFactory` 创建短期 geometry。
- brush 走 `context.Compositor.BrushCache.GetSolidBrush(...)`。
- text 走 `context.DrawText` / `D3D_TextRenderer`。
- 图片走 `context.DrawImage` / compositor image cache。
- 旧 `D3D_D2DInterop.GetD2DFactory()` 仅用于兼容测量或旧 D2D 路径，不能和 V3 `ID2D1DeviceContext` 交叉使用。

## 文字路线

`D3D_TextRenderer` 是唯一文字绘制入口。ClearTypeCompatible、Grayscale 和 Auto 已集中在这里切换；Outline 当前作为高质量策略位保留，真正几何描边或独立 text layer 后续必须继续在 `D3D_TextRenderer` 内扩展，迁移控件不能自建旧文字管线。
