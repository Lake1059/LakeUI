''' <summary>
''' D3D_ControlContracts 定义控件渲染与失效的非渲染契约。
''' 这些接口不能隐藏 D3D/D2D 资源创建，也不能替代 D3D_ 缓存类。
''' </summary>
Friend Module D3D_ControlContracts
End Module

''' <summary>
''' GPU 控件绘制契约。RenderGpu 只能绘制当前控件自身，不主动绘制兄弟或父控件。
''' 控件不能自己提交 Present/Commit，不能持有跨帧 ID2D1Brush、ID2D1Bitmap 等 GPU 对象；跨帧资源必须交给 D3D_ 缓存类。
''' 控件可以持有纯业务状态，例如颜色、文本、滚动位置、动画进度。
''' </summary>
Public Interface D3D_IGpuRenderable
    ''' <summary>向当前 GPU 绘制上下文发出控件自身的绘制命令。</summary>
    ''' <param name="context">当前帧上下文；仅在调用期间有效，禁止保存其 GPU 资源。</param>
    Sub RenderGpu(context As D3D_PaintContext)
End Interface

''' <summary>
''' GPU 整控件超采样倍率来源。返回 1 表示关闭；核心会与 <see cref="GlobalOptions.GlobalSSAA"/> 取较大值。
''' </summary>
Public Interface D3D_ISuperSamplingSource
    ''' <summary>返回控件请求的超采样倍率；有效范围由引擎限制为 1 到 4。</summary>
    ReadOnly Property SuperSamplingScale As GlobalOptions.SuperSamplingScaleEnum
End Interface

''' <summary>
''' 仅内部审计后的渲染器才能声明：其一次 RenderGpu 调用会完整重建本次脏区的像素，
''' 包括透明背景采样、alpha 图像和边缘抗锯齿所需的覆盖内容。未实现本接口时，
''' D3D_ControlSurface 继续整面清除并按原有完整绘制路径执行，保证既有语义不变。
''' </summary>
Friend Interface D3D_IGpuDirtyRegionCoverage
    Function CoversDirtyRegion(dirtyRegion As Rectangle) As Boolean
End Interface

Public Interface D3D_IGpuInvalidationSource
    ''' <summary>返回控件本地坐标中的有效绘制边界。</summary>
    Function GetRenderBounds() As Rectangle
End Interface

Public Interface D3D_IBackgroundSourceProvider
    ''' <summary>获取显式 GPU 背景来源；返回 <c>False</c> 或空来源表示使用自动祖先解析。</summary>
    ''' <param name="source">输出背景来源控件；返回 <c>False</c> 时必须设为 <c>Nothing</c>。</param>
    Function TryGetBackgroundSource(ByRef source As Control) As Boolean
End Interface

''' <summary>
''' V5 纯 GPU HWND 呈现契约。实现此接口的控件由 D3D_V5Presentation 直接呈现到自身 HWND，
''' 不经过 PaintEventArgs、Graphics、HDC 或 BitBlt。V5 路径不可用时不会回退到 CPU 绘制。
''' </summary>
Public Interface V5_IGpuPresentationSource
End Interface

''' <summary>
''' V5 控件在批量更新位置/尺寸时暂时抑制几何事件触发的即时呈现。
''' 批量操作完成后由调用方提交一次最新几何帧，避免中间尺寸被合成目标短暂呈现。
''' </summary>
Friend Interface V5_IGeometryUpdateSource
    ReadOnly Property IsGeometryUpdateInProgress As Boolean
End Interface
