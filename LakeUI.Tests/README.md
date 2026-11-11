# LakeUI.Tests

持久回归集运行 UltraDetailListView 复选框默认值、继承、布局、输入和事件测试，以及 GPU 局部呈现、跨窗多级背景失效、Panel 透明模式、标题文字/按钮局部更新、ThisIsYourWindow 配置合并、阴影样式去重和毛玻璃帧缓存复用测试。

渲染测试使用真实 D3D11 / DirectComposition surface，并以 GPU readback 比较局部更新和完整帧像素；覆盖 1x/2x/4x SSAA、FP16、FP16 输出、局部覆盖回退、绘制失败恢复、窗口句柄/尺寸/设备恢复和跨窗多级背景映射。测试不截图、不进行视觉判断。

附加回归验证图片流式快照、源 Image 在 GPU 纹理缓存保留时仍可回收、动画失效分配量与语义、稳定背景依赖零分配、Ponder 按钮局部像素等价、RamMonitor 历史工作区复用、折线几何缓存重建，以及 V5 批次顺序、缓冲复用、重入和控件引用清理。测试使用内部反射探针、CPU 分配计数与 GPU readback，不做截图或视觉判断。

项目需要 Windows 和 .NET 11 SDK。

```powershell
dotnet run --project LakeUI.Tests
```
