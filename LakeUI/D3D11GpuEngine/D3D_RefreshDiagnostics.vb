Imports System.Diagnostics
Imports System.Runtime.CompilerServices

''' <summary>ResetV5Probe 之后的 CPU 墙钟计时；不代表 GPU 执行时间。</summary>
Public NotInheritable Class D3D_RefreshTiming
    Public Property Stage As String
    Public Property ControlName As String
    Public Property ControlId As Integer
    Public Property Count As Long
    Public Property TotalMilliseconds As Double
    Public Property PeakMilliseconds As Double
    Public Property FirstStartMilliseconds As Double
    Public Property LastEndMilliseconds As Double
End Class

Friend Module D3D_RefreshDiagnostics
    Private ReadOnly _sync As New Object()
    Private ReadOnly _samples As New Dictionary(Of (String, Integer), D3D_RefreshTiming)()
    Private _origin As Long = Stopwatch.GetTimestamp()

    Friend Function Start() As Long
        Return If(D3D_RenderDiagnostics.Enabled, Stopwatch.GetTimestamp(), 0L)
    End Function

    Friend Sub Record(started As Long, stage As String, Optional control As Control = Nothing)
        If started = 0 OrElse Not D3D_RenderDiagnostics.Enabled Then Return
        Dim 结束时间 = Stopwatch.GetTimestamp()
        Dim 控件标识 = If(control Is Nothing, 0, RuntimeHelpers.GetHashCode(control))
        SyncLock _sync
            If started < _origin Then Return
            Dim 样本 As D3D_RefreshTiming = Nothing
            If Not _samples.TryGetValue((stage, 控件标识), 样本) Then
                If _samples.Count >= 2048 Then Return
                样本 = New D3D_RefreshTiming With {
                    .Stage = stage, .ControlId = 控件标识,
                    .ControlName = If(control Is Nothing, "", control.GetType().Name & ":" & control.Name),
                    .FirstStartMilliseconds = Stopwatch.GetElapsedTime(_origin, started).TotalMilliseconds
                }
                _samples.Add((stage, 控件标识), 样本)
            End If
            Dim 耗时 = Stopwatch.GetElapsedTime(started, 结束时间).TotalMilliseconds
            样本.Count += 1
            样本.TotalMilliseconds += 耗时
            样本.PeakMilliseconds = Math.Max(样本.PeakMilliseconds, 耗时)
            样本.LastEndMilliseconds = Stopwatch.GetElapsedTime(_origin, 结束时间).TotalMilliseconds
        End SyncLock
    End Sub

    Friend Sub Reset()
        SyncLock _sync
            _samples.Clear()
            _origin = Stopwatch.GetTimestamp()
        End SyncLock
    End Sub

    Friend Function Snapshot() As D3D_RefreshTiming()
        SyncLock _sync
            Return _samples.Values.Select(Function(s) New D3D_RefreshTiming With {
                .Stage = s.Stage, .ControlName = s.ControlName, .ControlId = s.ControlId,
                .Count = s.Count, .TotalMilliseconds = s.TotalMilliseconds,
                .PeakMilliseconds = s.PeakMilliseconds,
                .FirstStartMilliseconds = s.FirstStartMilliseconds,
                .LastEndMilliseconds = s.LastEndMilliseconds
            }).ToArray()
        End SyncLock
    End Function
End Module
