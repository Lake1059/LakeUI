''' <summary>
''' 弹出控件共用的毛玻璃背景适配层。
''' 底层仍使用 <see cref="BackdropRenderer"/>，确保与 <see cref="ThisIsYourWindow"/> 的抓屏、模糊和噪点逻辑保持同步。
''' </summary>
''' <remarks>
''' 本类是 popup / message dialog 的轻量 façade，不拥有任何窗口消息，也不自动定时刷新。
''' 调用方负责在弹出、移动、尺寸变化或图片源变化时调用 <see cref="Prepare"/>，然后在 Paint 中调用
''' <see cref="Draw"/>。
'''
''' 与 ThisIsYourWindow 的差异：
''' • popup 是独立顶层窗口，不能直接采样主窗口已有 backdrop 帧；捕获区域必须以 popup 自己的 bounds
'''   为准。
''' • Auto 模式只在 Prepare 时抓一帧，不做持续 timer；持续刷新会让浮层 hover 产生不必要的后台抓屏。
''' • Image 模式仍会通过 BackdropRenderer 生成模糊帧，因此 SourceImage 变化后必须再次 Prepare。
'''
''' 坑点：
''' • <see cref="WaitForFrame"/> 只等待 worker 空闲；它不是强制同步重绘。调用方仍需 Invalidate / Paint。
''' • <see cref="Draw"/> 使用 GDI Graphics 路径，适合顶层 popup；V2 控件内的图片背景穿透应走
'''   <see cref="BackgroundPenetrationV2"/> 的 D2D 快路径。
''' </remarks>
Friend NotInheritable Class PopupBackdropRenderer
    Implements IDisposable

    Private ReadOnly _host As Form
    Private _renderer As BackdropRenderer

    Public Property Mode As PopupBackdropMode = PopupBackdropMode.None
    Public Property SourceImage As Image = Nothing
    Public Property TintColor As Color = Color.FromArgb(120, 32, 32, 32)
    Public Property BlurRadius As Integer = 24
    Public Property BlurPasses As Integer = 3
    Public Property DownsampleFactor As Integer = 4
    Public Property NoiseOpacity As Byte = 18
    Public Property NoiseScale As Single = 1.0F
    Public Property TransientExcludeOnCapture As Boolean = False

    Public Sub New(host As Form)
        _host = host
    End Sub

    Public ReadOnly Property HasFrame As Boolean
        Get
            Return Mode <> PopupBackdropMode.None AndAlso _renderer IsNot Nothing AndAlso _renderer.HasFrame
        End Get
    End Property

    Public Sub Configure(mode As PopupBackdropMode,
                         sourceImage As Image,
                         tintColor As Color,
                         blurRadius As Integer,
                         blurPasses As Integer,
                         downsampleFactor As Integer,
                         noiseOpacity As Byte,
                         noiseScale As Single)
        Me.Mode = mode
        Me.SourceImage = sourceImage
        Me.TintColor = tintColor
        Me.BlurRadius = blurRadius
        Me.BlurPasses = blurPasses
        Me.DownsampleFactor = downsampleFactor
        Me.NoiseOpacity = noiseOpacity
        Me.NoiseScale = noiseScale
    End Sub

    Public Sub Prepare(formBounds As Rectangle, Optional commitAverage As Boolean = True)
        If Mode = PopupBackdropMode.None OrElse formBounds.Width <= 0 OrElse formBounds.Height <= 0 Then
            ResetRenderer()
            Return
        End If

        If _renderer Is Nothing Then _renderer = New BackdropRenderer(_host)
        _renderer.ApplyParameters(BlurRadius, BlurPasses, DownsampleFactor,
                                  NoiseScale)
        _renderer.SetSource(Mode = PopupBackdropMode.Image, SourceImage)
        _renderer.SetTransientExcludeOnCapture(TransientExcludeOnCapture)
        _renderer.RequestFrame(formBounds, commitAverage)
    End Sub

    Public Function WaitForFrame(Optional timeoutMilliseconds As Integer = 500) As Boolean
        If Mode = PopupBackdropMode.None OrElse _renderer Is Nothing Then Return True
        Return _renderer.WaitForIdle(timeoutMilliseconds) AndAlso _renderer.HasFrame
    End Function

    Public Sub Draw(g As Graphics, target As Rectangle)
        If Not HasFrame OrElse target.Width <= 0 OrElse target.Height <= 0 Then Return

        _renderer.DrawTo(g, target)
        If TintColor.A > 0 Then
            Using brush As New SolidBrush(TintColor)
                g.FillRectangle(brush, target)
            End Using
        End If
        If BlurPasses > 0 AndAlso NoiseOpacity > 0 Then
            _renderer.DrawNoise(g, target, NoiseOpacity)
        End If
    End Sub

    Private Sub ResetRenderer()
        If _renderer Is Nothing Then Return
        _renderer.Dispose()
        _renderer = Nothing
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        ResetRenderer()
    End Sub

End Class

''' <summary>弹出层毛玻璃来源模式。</summary>
Public Enum PopupBackdropMode
    ''' <summary>不绘制弹出层玻璃背景。</summary>
    None = 0
    ''' <summary>从弹出层背后的桌面区域抓屏并模糊。</summary>
    Auto = 1
    ''' <summary>使用调用方提供的图片作为虚拟背景并模糊。</summary>
    Image = 2
End Enum
