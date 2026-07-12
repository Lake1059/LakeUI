''' <summary>
''' V3_ControlContracts 定义后续长期控件迁移的非渲染契约；本阶段不要求任何现有控件实现。
''' 这些接口不能隐藏 D3D/D2D 资源创建，也不能替代 D3D_ 缓存类。
''' </summary>
Friend Module V3_ControlContracts
End Module

''' <summary>
''' 后续 GPU 控件绘制契约。RenderGpu 只能绘制当前控件自身，不主动绘制兄弟或父控件。
''' 控件不能自己提交 Present/Commit，不能持有跨帧 ID2D1Brush、ID2D1Bitmap 等 GPU 对象；跨帧资源必须交给 D3D_ 缓存类。
''' 控件可以持有纯业务状态，例如颜色、文本、滚动位置、动画进度。
''' </summary>
Public Interface V3_IGpuRenderable
    Sub RenderGpu(context As D3D_PaintContext)
End Interface

''' <summary>
''' V3 整控件超采样倍率来源。返回 1 表示关闭；核心会与 <see cref="GlobalOptions.GlobalSSAA"/> 取较大值。
''' </summary>
Public Interface V3_ISuperSamplingSource
    ReadOnly Property SuperSamplingScale As GlobalOptions.SuperSamplingScaleEnum
End Interface

''' <summary>
''' 仅内部审计后的渲染器才能声明：其一次 RenderGpu 调用会以不透明像素覆盖本次脏区。
''' 未实现本接口时，PaintScope 始终将当前 HDC 内容拷入 GPU target，保证背景映射、
''' alpha 图像、毛玻璃和原生子控件的既有语义不变。
''' </summary>
Friend Interface V3_IGpuDirtyRegionCoverage
    Function CoversDirtyRegion(dirtyRegion As Rectangle) As Boolean
End Interface

Public Interface V3_IGpuInvalidationSource
    Function GetRenderBounds() As Rectangle
End Interface

Public Interface V3_IBackgroundSourceProvider
    Function TryGetBackgroundSource(ByRef source As Control) As Boolean
End Interface
