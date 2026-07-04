# LakeUI D3D11 GPU Engine

此目录是 LakeUI 新主版本 GPU 渲染核心的唯一新增区域。旧 V2 渲染文件保持原位，不搬迁、不改名、不作为本核心的运行时基类。

## 当前核心边界

- `D3D_` 类型负责 D3D11/DXGI/D2D1.1/DirectWrite、窗口 swap chain、GPU 缓存、文字、背景图模型、Backdrop 和帧调度。
- `V3_` 类型只描述后续控件迁移契约、DPI、失效路由、树遍历和迁移标记，不隐藏 GPU 资源创建。
- 当前阶段不迁移任何现有控件，不修改 `LakeUI.Demo`，不重新引入 HDC/D2D 回退后端。
- 可验证呈现路线为 `D3D11 device -> D2D1.1 DeviceContext -> DXGI HWND swap chain -> DWM`。
- `D3D_RenderCore.RenderValidationFrame(form)` 是核心级内部验证入口；返回 `False` 表示窗口不可用、最小化或本帧因设备丢失未提交。
- DirectComposition 宿主作为独立边界保留；当前不作为阻塞项。后续启用时必须继续遵守 device generation、UI 线程和 GPU-only 生命周期。

## 设备丢失策略

设备丢失包括但不限于：驱动更新、TDR、系统休眠/恢复、远程桌面切换、显示适配器重置、`D2DERR_RECREATE_TARGET`、`DXGI_ERROR_DEVICE_REMOVED`、`DXGI_ERROR_DEVICE_RESET`、`DXGI_ERROR_DEVICE_HUNG`、`DXGI_ERROR_DRIVER_INTERNAL_ERROR`、`DXGI_ERROR_ACCESS_LOST`。

处理流程：

1. `D3D_DeviceManager.HandleDeviceLost` 将异常 `HResult` 规范化为 UInt32 后判断是否属于设备级错误，避免 DXGI 负数 HRESULT 比较失败。
2. `InvalidateDevice` 释放进程级 D3D/DXGI/D2D/DWrite 对象，并立即推进 `DeviceGeneration`。
3. `DeviceLost` 事件通知所有窗口 compositor 释放窗口级 target、D2D context 和 GPU cache。
4. 如果错误发生在 `BeginDraw/EndDraw/Present` 当前帧内部，`D3D_WindowCompositor` 只设置 pending 标记；退出帧后再释放资源，避免在 D2D 调用栈中 Dispose target。
5. `EndFrame` 只有在 `Present` 成功返回时才返回 `True`；窗口不可用、最小化、绘制异常、`present=False` 或设备丢失都会返回 `False`。
6. 下一次 `RequestRender` 或 scheduler tick 会按新的 generation 重建设备、swap chain 和缓存。

迁移控件必须把 `D3D_PaintContext.DeviceGeneration` 当作跨帧 GPU 资源有效性的唯一判据。`FrameGeneration` 只表示窗口帧序号，不能用于判断 D3D/D2D 资源是否还属于当前设备。

## 后续控件迁移规则

后续控件只允许通过 `V3_IGpuRenderable.RenderGpu(context As D3D_PaintContext)` 绘制当前控件自身，并通过 `V3_InvalidationRouter.RequestRender` 请求刷新。

禁止事项：

- 不得调用 `Graphics.GetHdc`、`BitBlt`、`PaintEventArgs`、旧 `PaintScopeV2` 或旧背景穿透路径。
- 不得自行创建 D3D/D2D/DXGI/DirectWrite device、factory、swap chain 或 render target。
- 不得持有跨帧 `ID2D1Brush`、`ID2D1Bitmap`、`ID2D1Geometry`、`IDWriteTextFormat` 等 GPU/DirectWrite 对象；长期资源必须交给 `D3D_` 缓存。
- 不得在控件内提交 `Present` 或 DirectComposition `Commit`。
- 不得主动绘制父控件、兄弟控件或递归调用 WinForms paint。

允许事项：

- 控件可以持有纯业务状态，例如颜色、文本、滚动位置、动画进度和数据模型。
- 控件可以在 `RenderGpu` 中调用 `D3D_PaintContext` 的矩形、图片、文字、clip 等绘制入口。
- 图片、文字格式、画刷、几何、背景 snapshot 和 blur intermediate 必须走 compositor 提供的缓存服务。

## 背景与 Backdrop

`D3D_BackgroundGraph.GenerateBackgroundSnapshot` 只获取或创建 GPU snapshot target，不递归绘制 source。真正的 source 渲染必须由窗口 compositor 按 `D3D_FrameGraph` 顺序驱动：先生成 source texture，再让 consumer 采样。遇到循环依赖时使用上一帧 snapshot 或跳过本帧背景采样。设备丢失时只释放 `background:*` GPU snapshot，保留 source/consumer 关系，下一帧按同一关系重建。

`D3D_BackdropRenderer` 当前实现 Image 模式 GPU 路线。Auto/CaptionOnly 的 Desktop Duplication 路线保留为后续核心能力，不能为了兼容普通 WinForms 控件重新引入 CPU 截图或 HDC 回贴。

## 文字路线

`D3D_TextRenderer` 是唯一文字绘制入口。ClearTypeCompatible、Grayscale 和 Auto 已集中在这里切换；Outline 当前作为高质量策略位保留，真正几何描边或独立 text layer 后续必须继续在 `D3D_TextRenderer` 内扩展，迁移控件不能自建旧文字管线。
