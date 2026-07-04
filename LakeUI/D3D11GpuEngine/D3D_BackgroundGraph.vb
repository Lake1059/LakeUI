Imports Vortice.Direct2D1

''' <summary>
''' D3D_BackgroundGraph 维护 LakeUI GPU 控件之间的背景 source/consumer 关系和 GPU snapshot 模型。
''' 它不再通过 InvokePaint 反射重绘 source，不长期保存 source 的 CPU bitmap，普通 WinForms 控件不能作为高质量背景 source。
''' source 与 consumer 出现循环依赖时，使用上一帧 snapshot 或直接跳过本帧背景采样。
''' 本阶段只定义核心图模型，不迁移任何现有 BackgroundSource 属性。
''' source/consumer 关系是 CPU 迁移状态，设备丢失时必须保留；只有 background:* GPU snapshot 需要随 generation 释放。
''' </summary>
Public NotInheritable Class D3D_BackgroundGraph
    Implements IDisposable

    Private ReadOnly _textureCache As D3D_TextureCache
    Private ReadOnly _relations As New Dictionary(Of Control, Control)()
    Private _disposed As Boolean

    Public Sub New(textureCache As D3D_TextureCache)
        _textureCache = textureCache
    End Sub

    Public Sub RegisterConsumer(consumer As Control, source As Control)
        If consumer Is Nothing Then Return
        If source Is Nothing Then
            _relations.Remove(consumer)
        Else
            _relations(consumer) = source
        End If
    End Sub

    Public Function TryGetSource(consumer As Control, ByRef source As Control) As Boolean
        source = Nothing
        If consumer Is Nothing Then Return False
        Return _relations.TryGetValue(consumer, source) AndAlso source IsNot Nothing
    End Function

    ''' <summary>
    ''' 获取或创建背景 source snapshot target。该方法只分配 GPU bitmap，不递归绘制 source。
    ''' 真正的 source 渲染必须由 D3D_WindowCompositor 在 frame graph 的 source 阶段显式完成：
    ''' 先把 source 写入 snapshot target，再让 consumer 调用 DrawBackgroundSource 采样。
    ''' 这样可以避免 InvokePaint/父子控件递归绘制，并能在循环依赖时选择上一帧 snapshot 或跳过本帧采样。
    ''' </summary>
    Public Function GenerateBackgroundSnapshot(context As D3D_PaintContext,
                                               source As Control,
                                               snapshotSize As Size,
                                               Optional snapshotVersion As Integer = 0) As ID2D1Bitmap1
        If context Is Nothing OrElse source Is Nothing Then Return Nothing
        If snapshotSize.Width <= 0 OrElse snapshotSize.Height <= 0 Then Return Nothing

        Dim version = If(snapshotVersion > 0, snapshotVersion, context.FrameGeneration)
        Dim key = "background:" &
                  Runtime.CompilerServices.RuntimeHelpers.GetHashCode(source).ToString(Globalization.CultureInfo.InvariantCulture) & ":" &
                  snapshotSize.Width.ToString(Globalization.CultureInfo.InvariantCulture) & "x" &
                  snapshotSize.Height.ToString(Globalization.CultureInfo.InvariantCulture) & ":" &
                  version.ToString(Globalization.CultureInfo.InvariantCulture) & ":" &
                  context.DeviceGeneration.ToString(Globalization.CultureInfo.InvariantCulture)

        Dim bytes = CLng(snapshotSize.Width) * CLng(snapshotSize.Height) * 4L
        Return _textureCache.AcquireTexture(Of ID2D1Bitmap1)(
            key,
            context.DeviceGeneration,
            bytes,
            Function()
                Dim props As New BitmapProperties1(
                    New Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                    96.0F,
                    96.0F,
                    BitmapOptions.Target)
                Return context.DeviceContext.CreateBitmap(New Vortice.Mathematics.SizeI(snapshotSize.Width, snapshotSize.Height), IntPtr.Zero, 0UI, props)
            End Function)
    End Function

    ''' <summary>
    ''' 从 source snapshot 绘制背景。source texture 必须由 frame graph 先于 consumer 生成；缺失时本帧跳过采样。
    ''' </summary>
    Public Sub DrawBackgroundSource(context As D3D_PaintContext, snapshot As ID2D1Bitmap1, destination As RectangleF)
        If context Is Nothing OrElse snapshot Is Nothing Then Return
        Dim dst As Vortice.RawRectF? = D3D_PaintContext.ToRawRect(destination)
        context.DeviceContext.DrawBitmap(snapshot, dst, 1.0F, InterpolationMode.Linear, Nothing, Nothing)
    End Sub

    ''' <summary>
    ''' 释放 background snapshot GPU 资源但保留 source/consumer 关系。
    ''' 驱动更新、TDR 或休眠恢复后，下一帧应沿用同一关系重新生成 snapshot，而不是要求控件重新注册。
    ''' </summary>
    Public Sub Invalidate()
        _textureCache.ReleaseByPrefix("background:")
    End Sub

    Public Sub ClearRelations()
        _relations.Clear()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        Invalidate()
        ClearRelations()
        GC.SuppressFinalize(Me)
    End Sub
End Class
