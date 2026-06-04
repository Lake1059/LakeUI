''' <summary>
''' 弹出控件共用的毛玻璃背景适配层。
''' 底层仍使用 <see cref="BackdropRenderer"/>，确保与 <see cref="ThisIsYourWindow"/> 的抓屏、模糊和噪点逻辑保持同步。
''' </summary>
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

Public Enum PopupBackdropMode
    None = 0
    Auto = 1
    Image = 2
End Enum
