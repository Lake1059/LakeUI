Public Class Form_GpuMonitor

    Private WithEvents 刷新定时器 As New Timer With {.Interval = 1000}

    Private Sub ModernButton5_Click(sender As Object, e As EventArgs) Handles ModernButton5.Click
        '开始刷新
        ModernTextBox1.PreserveScrollPosition = True
        刷新定时器.Start()
        采样并显示()
    End Sub

    Private Sub ModernButton6_Click(sender As Object, e As EventArgs) Handles ModernButton6.Click
        '停止刷新
        刷新定时器.Stop()
    End Sub

    Private Sub 刷新定时器_Tick(sender As Object, e As EventArgs) Handles 刷新定时器.Tick
        采样并显示()
    End Sub

    Private Sub 采样并显示()
        Dim gpus = GpuMonitor.Sample()
        Dim sb As New System.Text.StringBuilder()
        sb.AppendLine($"═══════════ GPU Monitor ═══════════")
        sb.AppendLine($"检测到 {gpus.Length} 张显卡")
        sb.AppendLine()
        For Each gpu In gpus
            sb.AppendLine($"━━━ [{gpu.Index}] {gpu.Name} ({gpu.Vendor}) ━━━")
            sb.AppendLine()
            sb.AppendLine($"  LUID: 0x{gpu.LuidHigh:X8}_0x{gpu.LuidLow:X8}")
            sb.AppendLine($"  专用显存容量: {格式化字节(gpu.DedicatedVideoMemoryBytes)}")
            sb.AppendLine($"  专用系统内存: {格式化字节(gpu.DedicatedSystemMemoryBytes)}")
            sb.AppendLine($"  共享系统内存: {格式化字节(gpu.SharedSystemMemoryBytes)}")
            sb.AppendLine($"  整体占用率: {gpu.OverallUsage:P1}")
            If gpu.EngineUsages.Count > 0 Then
                sb.AppendLine($"  引擎占用:")
                For Each kv In gpu.EngineUsages
                    sb.AppendLine($"    {kv.Key}: {kv.Value:P1}")
                Next
            End If
            sb.AppendLine()
            sb.AppendLine($"  已用专用显存: {格式化字节(gpu.DedicatedMemoryUsedBytes)}")
            sb.AppendLine($"  已用共享内存: {格式化字节(gpu.SharedMemoryUsedBytes)}")
            sb.AppendLine($"  功耗: {格式化可空(gpu.PowerWatts, " W")}  上限: {格式化可空(gpu.PowerLimitWatts, " W")}  占比: {格式化可空(gpu.PowerPercent, "%")}")
            sb.AppendLine($"  核心频率: {格式化频率(gpu.CoreFrequencyHz)}  最大: {格式化频率(gpu.MaxCoreFrequencyHz)}")
            sb.AppendLine($"  显存频率: {格式化频率(gpu.MemoryFrequencyHz)}  最大: {格式化频率(gpu.MaxMemoryFrequencyHz)}")
            sb.AppendLine($"  视频编码: {格式化可空百分比(gpu.VideoEncoderUsage)}")
            sb.AppendLine($"  视频解码: {格式化可空百分比(gpu.VideoDecoderUsage)}")
            sb.AppendLine($"  驱动版本: {If(gpu.DriverVersion, "N/A")}")
            sb.AppendLine($"  VBIOS版本: {If(gpu.VBiosVersion, "N/A")}")
        Next
        sb.AppendLine($"═══════════ {DateTime.Now:HH:mm:ss} ═══════════")
        ModernTextBox1.Text = sb.ToString()
    End Sub

    Private Shared Function 格式化字节(bytes As ULong) As String
        If bytes >= 1073741824UL Then Return $"{bytes / 1073741824.0:F2} GB"
        If bytes >= 1048576UL Then Return $"{bytes / 1048576.0:F2} MB"
        If bytes >= 1024UL Then Return $"{bytes / 1024.0:F2} KB"
        Return $"{bytes} B"
    End Function

    Private Shared Function 格式化可空(value As Single?, unit As String) As String
        If Not value.HasValue Then Return "N/A"
        Return $"{value.Value:F1}{unit}"
    End Function

    Private Shared Function 格式化可空百分比(value As Single?) As String
        If Not value.HasValue Then Return "N/A"
        Return $"{value.Value:P1}"
    End Function

    Private Shared Function 格式化频率(hz As ULong?) As String
        If Not hz.HasValue Then Return "N/A"
        If hz.Value >= 1000000000UL Then Return $"{hz.Value / 1000000000.0:F2} GHz"
        If hz.Value >= 1000000UL Then Return $"{hz.Value / 1000000.0:F0} MHz"
        Return $"{hz.Value} Hz"
    End Function

    Private Sub Form_GpuMonitor_Load(sender As Object, e As EventArgs) Handles MyBase.Load

    End Sub
End Class