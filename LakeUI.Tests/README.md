# PixelPictureBox GPU 呈现回归探针

需要 Windows 和能运行 LakeUI D3D11/Direct2D/DirectComposition 核心的 GPU。本测试不使用视觉检查、截图、GDI+ 图片解码或软件渲染回退。

运行自包含的合成测试图：

```powershell
dotnet run --project LakeUI.Tests
```

用外部测试图复现：

```powershell
dotnet run --project LakeUI.Tests -- "C:\Users\DT1059\Desktop\测试图.png"
```

文件由 Test 项目通过 WIC 解码为 RGBA 数据，控件只接收已解码的 tile 或 GPU 帧。文件测试图至少需要 32×256 像素。

探针读取正常 V5 渲染链路注册的控件表面，并复用真实 HWND 的呈现器。最终表面的探针在 DirectComposition `BeginDraw` 更新范围内、Direct2D `EndDraw` 后执行 GPU 读回，正确处理更新偏移，不创建第二个 HWND 合成目标。

检查项：

- tile 加载数、缓存字节、真实提交帧数、渲染失败计数。
- 控件内部 FP16 表面、SDR 最终 BGRA8 表面及 HDR 最终 FP16 表面的每一个 RGBA 像素。
- sRGB 分块图片、SDR scRGB GPU 图片、显式 HDR 分块图片、HDR 动图帧中大于 1 的值、宽色域转换后的负 scRGB 值。
- SDR → HDR → SDR 切换、透明图片背景颜色，以及清空 Source 后恢复 SDR 渲染。
- 右键绘制像素选区、八个手柄在 1 倍和 1.5 倍缩放下的缩放结果、边框移动及鼠标捕获释放。
- 八个手柄分别返回 NW-SE、N-S、NE-SW、W-E 方向光标，选区边线返回四向移动光标，图片内部恢复默认光标。
- 启用全局 HDR 配置时，自动父级与显式 SDR 背景采样、图片留白区和半透明图片的正确线性混合；包含非零控件位置及 DirectComposition 更新偏移。
- 编译产物中 113 个公开属性的 XML summary，覆盖控件、数据源、色彩编码与分块诊断信息。
- 分块呈现复用目标集合、直接使用稳定 GPU key、无 LINQ 排序修剪缓存，并验证 1000000000000 × 900000000000 图像的有限视口坐标、金字塔层级和有界 tile 请求。
- 原始像素查询、帧切换和最新 surface revision 的提交状态。

FP16 最大绝对误差阈值为 0.002；SDR 阈值为一个 8 位码值（加浮点比较余量），允许 GPU sRGB 曲线查表和量化误差。加载超时、缺帧、像素不匹配、非有限值、错误呈现格式及渲染失败均返回非零退出码，WinForms paint 异常也会直接导致测试失败。

HDR 身份与内部像素格式分离：scRGB、浮点格式和宽色域不自动代表 HDR。PQ/HLG 自动识别为 HDR；外部提供的线性 HDR 数据需要在 `PixelPictureColorEncoding` 或 `PixelPictureGpuSource` 上显式传入 `isHdr: true`。这避免为判断 HDR 而扫描巨幅图片或所有动图帧。

有图片时，内部继续使用线性 FP16 保留色域与计算精度，但只对 HDR Source 使用 FP16 合成输出。SDR Source 使用 GPU 曲线将线性 scRGB 转换为 sRGB，再提交 BGRA8 SDR 表面；SDR 目标范围外的颜色会被裁剪，原始数据及内部表面保持不变。

最终表面读回证明合成提交的像素内容正确，不等同于验证显示器实际亮度、系统 HDR 映射或面板色域。内部 `SurfaceProbe` 默认为空，仅 Test 使用时产生读回开销。
