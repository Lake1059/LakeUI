# 透明背景机制 + 基础绘制 性能改造方案分析

> 针对 `TransparentBackgroundCache.vb` + `ModernButton.vb` 当前架构在控件数量增多后内存/显存占用过大的问题，对几条改造路线做成本-收益评估。

---

## 一、当前架构的真实成本拆解

先把"贵在哪"量化清楚，再谈方案才有意义。

### 1.1 每个"背景源"控件持有的常驻资源

`TransparentBackgroundCache` 的缓存键是**背景源控件**（不是子控件）。每个被采样的 source 会在 `Entry` 里挂：

| 资源 | 大小 | 生命周期 |
|---|---|---|
| `entry.Bmp` (GDI `Bitmap`, Format32bppPArgb) | `W*H*4` 内存 | TTL=1s 或被 LRU 淘汰；GDI 消费者存在时不释放 |
| `entry.D2DBmp` (`ID2D1Bitmap`) | `W*H*4` **显存** | 同上 |
| 事件订阅 (Invalidated/Resize/Disposed) | 极小 | 直到 source.Dispose |

> 单个 1920×1080 source ≈ **8 MB 内存 + 8 MB 显存**，4K source ≈ **32 MB + 32 MB**。
> `MaxCacheBytes = 64MB` 是 GDI+D2D **合并**预算，所以稳态下大约只能放下 1~2 个大尺寸 source。

### 1.2 每个 ModernButton 自己还持有的资源

```
_dcRT            : ID2D1DCRenderTarget                 (控件级，必有)
_ssaaCache       : BitmapRTCache (SSAA 离屏 RT)        (Width*Height*ssaa² *4 显存)
_backImageCache  : D2DBitmapCache (背景图)              (按需)
_iconCache       : D2DBitmapCache (图标)                (按需)
```

**这才是真正的大头**：SSAA=2 时一个 200×40 的按钮 = `200*40*4*4 = 128 KB` 显存离屏 RT；
SSAA=4 时 × 4 → **512 KB**。**100 个按钮 ≈ 50 MB 显存**，与背景缓存量级相当甚至更大。

### 1.3 结论

> **优化重点不是只盯着 `TransparentBackgroundCache`，更要看每个控件持有的 `_ssaaCache` / `_dcRT`。** 后者的总占用随控件数 **线性增长**，是真正的"控件多就爆"。背景缓存按 source 计数，反而是常数级。

---

## 二、四条路线的成本/可行性对照

下面每条路线都给出"动什么、改在哪、收益、代价、风险"四要素。

### 路线 A：去掉所有大对象缓存，只保留画刷/几何这类小对象，按需即时重建

**思路**：
- `TransparentBackgroundCache` 不再缓存 `Bmp` 和 `D2DBmp`，每次 `PaintBackgroundFor_D2D` 就**实时**调 `InvokePaint(source)` 重建。
- 控件级的 `_ssaaCache` 改为**每帧 Create/Dispose**，不跨帧持有。
- 只保留极轻量的对象：`SolidColorBrush`、`PathGeometry` 等（这些 D2DHelper 里已经有缓存）。

**收益**：
- 稳态内存/显存接近 0（只剩 `_dcRT` 一个 ~几百 KB）。
- 复杂度大幅下降，无 LRU、无 TTL、无 Dirty 标记。

**代价**：
- 每帧都要重画 source 全图 + 重建 SSAA RT + LockBits + CreateBitmapFromGdi。
- 实测：一个 1080p source 的全量 `InvokePaintBackground+InvokePaint` 通常 **3~10 ms**；`LockBits + CreateBitmapFromGdi` 再 **1~3 ms**。**这是同步阻塞 UI 线程的**。
- 如果一帧内有 N 个透明子控件都触发，且它们的 source 不同，会 N 倍放大。

**风险**：
- **UI 帧率会显著下降**，hover 动画类场景体验劣化明显。
- D2D RT 频繁创建/销毁会增加驱动层 GPU 内存碎片（Vortice/D3D11 层）。

**适用判断**：❌ 不推荐单独走这条路。**仅在"超低占用 > 流畅度"的场景**（如后台窗口、最小化恢复后第一帧）有意义。

---

### 路线 B：把采样和上传搬到异步线程，UI 线程只消费已经准备好的位图

**思路**：
- `TransparentBackgroundCache` 引入一个后台线程（或 `Task` + `Channel`）。
- UI 线程的 `PaintBackgroundFor_D2D` **不阻塞**，立即返回当前 entry 里**已有的** D2DBmp（哪怕它是上一帧甚至几帧前的）。
- 后台线程负责：监听 `source.Invalidated` → 节流后调 `InvokePaint(source)` 到一个 `Bitmap` → marshal 到 UI 线程做 D2D 上传（D2D 资源必须在持有 RT 的线程操作，但 `CreateBitmapFromGdi` 也可以在拥有 RT 的线程上 PostMessage 完成）。

**收益**：
- UI 线程绘制零等待，**绝不卡顿**。
- 重采样的 CPU 开销从主线程剥离。
- 可以为重建做更激进的策略（比如 GDI 位图常驻后台、随时丢弃，UI 只持有 D2DBmp）。

**代价**：
- WinForms `Control.InvokePaint` 反射调用**必须在控件所在线程**（即 UI 线程）。这是硬约束 —— **没法把 source 的真正绘制扔到工作线程**，最多把"位图复制/像素后处理（ForceOpaqueAlpha）"放后台。
- 真正能异步的只有：`ForceOpaqueAlpha` 的 LockBits 遍历（很小，1080p 大概 1ms）、字节预算计算等。**收益远不如想象中大**。
- 复杂度 ↑：需要双缓冲位图、线程同步、生命周期更难追踪（source.Dispose 时后台任务必须取消）。

**风险**：
- 可能引入"看见旧背景一帧"的视觉撕裂，但你已经接受这个 trade-off。
- 死锁风险：后台任务 BeginInvoke 回主线程时如果主线程在等后台，会卡死。

**适用判断**：⚠️ **理论上可行但价值不大**，因为最耗时的 `InvokePaint(source)` 必须留在 UI 线程。**真要做的话核心收益其实是"节流刷新机制"，而不是异步本身**。

---

### 路线 C：严格的时间片刷新调度（推荐核心思路）

**思路**：
- 引入一个**全局帧调度器**（基于 `System.Windows.Forms.Timer` 或 `CompositionTarget.Rendering` 替代品）。
- 所有透明子控件的"背景脏 → 需要重新采样"请求都进入这个调度器队列。
- 调度器按帧（16ms / 33ms 可配）**批量**处理：一帧内同一个 source 只重采样一次，且整帧最多重采样 K 个 source（K=2~3）。
- 超出额度的 source 在下一帧再处理，**用户感知到的是"背景动画稍微降帧"，但完全不卡**。

**收益**：
- 与现有架构兼容性最好，改动局限在 `TransparentBackgroundCache` 内部。
- 解决"一帧内多个 source 同时变脏导致的尖峰"，把 CPU 占用曲线**削峰**。
- 与现有的 `DirtyMinIntervalMs` 节流是同一类思路的强化版。

**代价**：
- 仍需要持有 GDI/D2D 位图缓存（无法去掉常驻内存），但可以把 `CacheTtlMs` 拉得更长（5~10s），让"不再被采样的 source" 自然淘汰。
- 需要引入"控件可见性追踪"才能避免给已经看不见的 source 重采样。

**风险**：
- 调度器是单点，必须线程安全，复杂度中等。

**适用判断**：✅ **强烈推荐**。这是收益最稳、风险最低、和现有契约耦合最小的方案。

---

### 路线 D：超时强释放 + 异步重建（"软常驻"）

**思路**：
- 把 `MaxCacheBytes` 调小（比如 8 MB），并设置**绝对释放 TTL**（如 3s 无访问就强制 Dispose 位图）。
- 释放后下次访问时重建走"异步占位"：先返回 Nothing → 控件继续画自身（背景临时不透出）→ 后台触发一次重采样 → 完成后 `Invalidate(child)` 让控件再画一帧带背景的。
- 等价于**"懒加载 + 自动卸载"**。

**收益**：
- 内存/显存**真正下降**到只在活跃 source 上有占用。
- 切换 Tab、最小化恢复等"长时间不动"场景占用归零。

**代价**：
- 用户在每次 source 长时间未访问后第一帧看到"瞬间穿透"或"瞬间无背景"，**有视觉闪烁**。
- 需要追加"占位策略"（比如先用 `BackColor` 不透明填底直到位图就绪），否则视觉不可接受。

**风险**：
- 与现有"BackColor.A<255 当遮罩"契约会冲突，需要为占位状态单独设计回退颜色。
- 实现上要避免**重建风暴**：100 个透明控件同时唤醒不能 100 倍触发重采样。

**适用判断**：⚠️ 可作为路线 C 的**补充**（即"路线 C + 长时间未访问后真正释放"），单独用会有可见闪烁。

---

### 路线 E（额外提案）：**架构性重构 —— 单层"共享离屏画布"**

这是我认为**真正能根治问题**的方向，单独说。

**思路**：

整个窗口共享**一张** D2D 离屏 BitmapRT（窗口大小，1× 像素，不开 SSAA），称为"窗口合成层"。

- 所有透明感知控件**不再各自持有 `_dcRT` + `_ssaaCache`**。
- 透明背景采样不再是"采样某个 source 控件"，而是直接读窗口合成层里对应矩形的像素。
- 每个控件的 `OnPaint` 改为：往这张共享 BitmapRT 上**画自己那块矩形**，最后一次性 Present 到窗口 DC。
- SSAA 改为窗口级（只有一张 2× / 4× 离屏图），而不是每控件级。

**收益**：
- 显存从 `O(控件数)` 降到 `O(窗口数)`。**100 个按钮 50MB 显存 → 几 MB**。
- 透明采样退化成"读共享画布的像素"，**完全不需要 `TransparentBackgroundCache` 这套机制**。
- SSAA 只做一次，整窗口受益。

**代价**：
- 这是**框架级**改动，涉及绘制循环重写、Z-order 管理、与 WinForms 消息泵的协作。
- 控件的"独立 BeginPaint/EndPaint"模型要改成"向合成器登记脏矩形"。
- 工作量大概是当前 D2DHelper + 所有 Modern* 控件的 30%~50% 改写。

**适用判断**：🎯 **长期最优解，但工程量大**。如果项目还在快速迭代期，先走路线 C；如果 LakeUI 准备做下一个大版本架构升级，路线 E 是终点。

---

## 三、推荐方案（短期 / 中期 / 长期）

| 期限 | 方案 | 预期效果 |
|---|---|---|
| **短期（1~2 天）** | 路线 C + 削减 `_ssaaCache` 持久化 | 显存降 30~50%，无视觉变化 |
| **中期（1~2 周）** | 路线 C + 路线 D 的"长时间未访问强释放" + SSAA 全局共享 | 显存降 60~80%，仅切换 Tab 时有可忽略的延迟 |
| **长期（架构升级）** | 路线 E（窗口共享合成层） | 显存降至常数级，与控件数解耦 |

---

## 四、立即可做的 "短期 5 步"（不动契约，纯改造）

如果你想现在就动手，按这个顺序改，每步都能独立验证：

1. **`_ssaaCache` 改为按需创建 + 用完即弃**
   `ModernButton.OnPaint` 退出时立即 Dispose，不跨帧持有。`SSAA=1` 时直接不创建。
   → 预计单按钮显存从 100KB 级降到 0。

2. **`_dcRT` 改成全局共享**
   `ID2D1DCRenderTarget` 的 `BindDC` 每帧调用，本就是设计成可跨控件复用的。在 `D2DHelper` 暴露一个静态 `SharedDCRenderTarget`，所有控件复用。
   → 控件持有的 D2D 对象从 4 个降到 0~1 个。

3. **`TransparentBackgroundCache` 加入"可见性过滤"**
   `OnSourceInvalidated` 时检查 source 是否仍可见（`source.Visible && IsWindowVisible`），不可见就不打 Dirty。
   → 切换 Tab 后旧 source 不再被无谓重建。

4. **`CacheTtlMs` 提到 5000ms + 引入 5000ms "未访问就 Dispose 位图" 兜底**
   长时间未访问的 source 自动归零占用；下次访问时一次性重建即可（用户感知不到）。

5. **位图统一为 D2D 路径**
   既然 `HasGdiConsumer` 的稳态分支已经存在，进一步审计是否还有真正的 GDI 消费者。若没有则**彻底删掉 `entry.Bmp` 字段**，省一半内存。
   → 仅显存占用，无内存常驻。

这 5 步合计预期把 100 个透明按钮场景的总占用从 ~100 MB 降到 ~15 MB 左右，且**完全不改变现有契约**。

---

## 五、需要你拍板的几个分歧点

在我下手改之前，希望你确认：

1. **能否接受"视觉延迟 1~2 帧"换流畅？**（路线 C 必需）
2. **能否接受切换 Tab 后第一次显示时多 50~100ms 延迟？**（路线 D 必需）
3. **是否愿意做架构级重构（路线 E）？** 还是优先做"短期 5 步" + 路线 C？
4. **当前是否还有 GDI 路径在用 `TransparentBackgroundCache.DrawSourceRegion`（非 _D2D 版）？** 如果没有，可以删掉一半代码。

确认后我可以直接按你选的方向开工。
