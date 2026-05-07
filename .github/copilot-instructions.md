# Copilot Instructions

## 项目指南
- 在本仓库中，D2D + DirectWrite 文字绘制路径的字号必须使用 Font.SizeInPoints * (96.0F / 72.0F) * DpiScale() 施加 DPI 乘数，参考 ModernButton.vb 的实现。
- D2D 绘制 GDI Bitmap/Image 时：
  - 若线条/几何能显示但图片主体绘制不出来，优先检查图片上传路径和像素格式（upload path & pixel format）。
  - 不要直接用不匹配的 CreateBitmapFromGdi 上传 Format32bppArgb。
  - 优先按 ModernPanel 的做法使用 D2DHelper.D2DBitmapCache.GetBitmap，让源图转换为 Format32bppPArgb 后再上传。
  - 在 DrawBitmap 时显式传入 source/destination rect，确保正确的区域与缩放。