Public Class GlobalOptions
    Public Enum SuperSamplingScaleEnum
        OFF = 1
        x2 = 2
        x3 = 3
        x4 = 4
    End Enum
    Public Const 超采样抗锯齿描述词 As String = "使用 SSAA 超采样抗锯齿显著改善线条观感，但会消耗额外性能；仅对图形生效，文字需要兼容第三方渲染接口所以走的是单独渲染."
    ''' <summary>
    ''' 强制全局启用 SSAA，除非值为 OFF，否则所有控件将在下一次刷新时调整到对应的 SSAA 级别。
    ''' </summary>
    Public Shared Property GlobalSSAA As SuperSamplingScaleEnum = SuperSamplingScaleEnum.OFF

    Private Shared _ssaaRenderTargetPoolBucketSize As Integer = 64
    Private Shared _ssaaRenderTargetPoolBudgetBytes As Long = 256L * 1024L * 1024L
    Private Shared _d2dBitmapCacheBudgetBytes As Long = 64L * 1024L * 1024L
    Private Shared _backgroundPenetrationCropCacheMaxEntriesPerSource As Integer = 24

    ''' <summary>
    ''' SSAA 离屏 RenderTarget 池的分桶粒度（像素）。值越大，越容易复用不同尺寸的 RT，但会多占一点显存。
    ''' </summary>
    Public Shared Property SsaaRenderTargetPoolBucketSize As Integer
        Get
            Return _ssaaRenderTargetPoolBucketSize
        End Get
        Set(value As Integer)
            _ssaaRenderTargetPoolBucketSize = Math.Max(1, value)
        End Set
    End Property

    ''' <summary>
    ''' 每个窗口 compositor 可保留的 SSAA 离屏 RenderTarget 池预算（字节）。0 表示不保留归还的 SSAA RT。
    ''' </summary>
    Public Shared Property SsaaRenderTargetPoolBudgetBytes As Long
        Get
            Return _ssaaRenderTargetPoolBudgetBytes
        End Get
        Set(value As Long)
            _ssaaRenderTargetPoolBudgetBytes = Math.Max(0L, value)
        End Set
    End Property

    ''' <summary>
    ''' 单个 Image 的 D2D 多 RenderTarget 上传缓存预算（字节）。预算越大，图标/背景图在多个 SSAA RT 间越少重复上传。
    ''' </summary>
    Public Shared Property D2DBitmapCacheBudgetBytes As Long
        Get
            Return _d2dBitmapCacheBudgetBytes
        End Get
        Set(value As Long)
            _d2dBitmapCacheBudgetBytes = Math.Max(0L, value)
        End Set
    End Property

    ''' <summary>
    ''' 每个 BackgroundSource 可保留的 D2D 裁剪上传缓存数量。值越大，多个透明子控件/小区域重绘越少重复上传。
    ''' </summary>
    Public Shared Property BackgroundPenetrationCropCacheMaxEntriesPerSource As Integer
        Get
            Return _backgroundPenetrationCropCacheMaxEntriesPerSource
        End Get
        Set(value As Integer)
            _backgroundPenetrationCropCacheMaxEntriesPerSource = Math.Max(0, value)
        End Set
    End Property


    Public Const 动画时长描述词 As String = "指定动画的时长 (毫秒)，0 = 无动画"
    Public Const 动画帧率描述词 As String = "指定动画的帧率，0 = 不限制，此时会大幅消耗 UI 线程性能。假定显示器刷新率足够，绝大多数动画仅需 120 帧即可满足极致丝滑，对于颜色渐变动画仅需 60 帧。"

End Class
