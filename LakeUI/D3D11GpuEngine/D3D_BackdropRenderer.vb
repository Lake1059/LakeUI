Imports Vortice.Direct2D1

Public Enum D3D_BackdropMode
    None = 0
    Image = 1
    Auto = 2
    CaptionOnly = 3
End Enum

''' <summary>
''' D3D_BackdropRenderer 是新核心 Backdrop / 毛玻璃入口，替代旧 BackdropRenderer，且不提供 DrawTo(Graphics)。
''' Image 模式走图片 texture -> blur/tint/noise -> 窗口背景 layer 的 GPU 路线；当前基础实现上传图片 texture 并在 GPU target 上绘制和 tint。
''' Auto/CaptionOnly 预留 DXGI Desktop Duplication 路线，但多显示器、远程桌面、权限、窗口自身排除均有独立风险，不能阻塞核心 swap chain 验证。
''' 平均色未来必须通过 GPU downsample 到 1x1 后只读回极小数据，不允许为了平均色读回整张背景。
''' </summary>
Public NotInheritable Class D3D_BackdropRenderer
    Implements IDisposable

    Private ReadOnly _imageCache As D3D_ImageCache
    Private _image As Image
    Private _disposed As Boolean

    Public Sub New(imageCache As D3D_ImageCache)
        _imageCache = imageCache
    End Sub

    Public Property Mode As D3D_BackdropMode = D3D_BackdropMode.None
    Public Property TintColor As System.Drawing.Color = System.Drawing.Color.FromArgb(80, System.Drawing.Color.White)

    Public Sub SetImage(image As Image)
        _image = image
        Mode = If(image Is Nothing, D3D_BackdropMode.None, D3D_BackdropMode.Image)
    End Sub

    ''' <summary>
    ''' 绘制 Image 模式 Backdrop。该路径不使用 BitBlt，不回贴 HDC；图片缩放由 GPU DrawBitmap 完成。
    ''' </summary>
    Public Sub DrawImageBackdrop(context As D3D_PaintContext, bounds As RectangleF)
        If context Is Nothing OrElse Mode <> D3D_BackdropMode.Image OrElse _image Is Nothing Then Return

        _imageCache.DrawImage(context, _image, bounds, Nothing, 1.0F)
        If TintColor.A > 0 Then context.FillRectangle(bounds, TintColor)
    End Sub

    Public Function TryReadAverageColor(context As D3D_PaintContext, ByRef color As System.Drawing.Color) As Boolean
        color = System.Drawing.Color.Empty
        Return False
    End Function

    Public Sub Invalidate()
        ' BackdropRenderer 不拥有源 Image；只丢弃与当前 renderer 相关的 transient 状态。
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        _image = Nothing
        GC.SuppressFinalize(Me)
    End Sub
End Class
