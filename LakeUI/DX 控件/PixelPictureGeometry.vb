Public Structure PixelPicturePoint
    Public Sub New(x As Double, y As Double)
        Me.X = x
        Me.Y = y
    End Sub

    Public ReadOnly Property X As Double
    Public ReadOnly Property Y As Double
End Structure

Public Structure PixelPictureRegion
    Public Sub New(x As Double, y As Double, width As Double, height As Double)
        Me.X = x
        Me.Y = y
        Me.Width = width
        Me.Height = height
    End Sub

    Public ReadOnly Property X As Double
    Public ReadOnly Property Y As Double
    Public ReadOnly Property Width As Double
    Public ReadOnly Property Height As Double
    Public ReadOnly Property Right As Double
        Get
            Return X + Width
        End Get
    End Property
    Public ReadOnly Property Bottom As Double
        Get
            Return Y + Height
        End Get
    End Property

    Public Function ToRectangleF() As RectangleF
        Return New RectangleF(CSng(X), CSng(Y), CSng(Width), CSng(Height))
    End Function
End Structure

''' <summary>可从后台触发；订阅控件负责将源完成通知合并到 UI 线程。</summary>
Public Interface IPixelPictureInvalidationSource
    Event ContentChanged As EventHandler
End Interface

''' <summary>要求控件及其 HWND 合成表面使用线性 scRGB FP16，避免 HDR 值被 8 位表面截断。</summary>
Public Interface D3D_IFloatPresentationSource
    ReadOnly Property UseFloatPresentation As Boolean
End Interface

Friend NotInheritable Class PixelPictureZoomMotion
    Public Value As Double
    Public Target As Double
    Public Velocity As Double

    Public Sub Reset(value As Double)
        Me.Value = value
        Target = value
        Velocity = 0
    End Sub

    Public Function Advance(seconds As Double, durationMilliseconds As Integer) As Boolean
        If durationMilliseconds <= 0 Then
            Reset(Target)
            Return False
        End If
        Dim angularFrequency = 8.0 / (durationMilliseconds / 1000.0)
        Dim displacement = Value - Target
        Dim coefficient = Velocity + angularFrequency * displacement
        Dim decay = Math.Exp(-angularFrequency * seconds)
        Value = Target + (displacement + coefficient * seconds) * decay
        Velocity = (Velocity - angularFrequency * coefficient * seconds) * decay
        If Math.Abs(Value - Target) < 0.0000001 AndAlso Math.Abs(Velocity) < 0.000001 Then
            Reset(Target)
            Return False
        End If
        Return True
    End Function
End Class
