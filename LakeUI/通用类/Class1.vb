Public Class Class1
    Public Enum SuperSamplingScaleEnum
        OFF = 1
        x2 = 2
        x3 = 3
        x4 = 4
    End Enum
    Public Const 超采样抗锯齿描述词 As String = "使用 SSAA 超采样抗锯齿显著改善线条观感，但会消耗额外性能；仅对图形生效，文字需要兼容第三方渲染接口所以走的是单独渲染"
    ''' <summary>
    ''' 强制全局启用 SSAA，除非值为 OFF，否则所有控件将在下一次刷新时调整到对应的 SSAA 级别。
    ''' </summary>
    Public Shared Property GlobalSSAA As SuperSamplingScaleEnum = SuperSamplingScaleEnum.OFF

End Class
