Imports System.ComponentModel
Imports System.Drawing.Drawing2D
Imports System.Numerics
Imports System.Runtime.InteropServices
Imports Vortice.Direct2D1
Imports Vortice.DirectWrite

''' <summary>
''' 任务管理器风格的内存监视器：显示 已用 / 已修改 / 备用 的堆叠历史曲线，
''' 顶部显示 总大小 与各占用容量，底部显示 速度 / 已提交 / DDR 类型。
''' 数据源与任务管理器一致：
'''   NtQuerySystemInformation(SystemMemoryListInformation) + GetPerformanceInfo + GlobalMemoryStatusEx
''' 硬件信息通过 GetSystemFirmwareTable("RSMB") 解析 SMBIOS 表 17 (Memory Device) 得到。
''' </summary>
<DefaultEvent("SampleUpdated")>
Public Class RamMonitor

#Region "D2D 资源"
    Private _dcRT As ID2D1DCRenderTarget
    Private ReadOnly _ssaaCache As New D2DHelper.BitmapRTCache()
    Private ReadOnly _brushCache As New D2DHelper.SolidColorBrushCache()
    Private ReadOnly _textFormatCache As New D2DHelper.TextFormatCache()

    Private Function GetOrCreateDCRenderTarget() As ID2D1DCRenderTarget
        If _dcRT Is Nothing Then _dcRT = D2DHelper.CreateDCRenderTarget()
        Return _dcRT
    End Function
#End Region

    Public Event SampleUpdated As EventHandler

    Public Sub New()
        InitializeComponent()
        Me.DoubleBuffered = True
        Me.SetStyle(ControlStyles.UserPaint Or
                    ControlStyles.AllPaintingInWmPaint Or
                    ControlStyles.OptimizedDoubleBuffer Or
                    ControlStyles.SupportsTransparentBackColor Or
                    ControlStyles.ResizeRedraw, True)
        AddHandler 采样定时器.Tick, AddressOf 采样定时器_Tick
    End Sub

#Region "采样器 - P/Invoke"
    ''' <summary>内存采样器：统一封装 GlobalMemoryStatusEx / GetPerformanceInfo /
    ''' NtQuerySystemInformation 的调用，并通过 SMBIOS 解析 DDR 类型与速度。</summary>
    Friend NotInheritable Class RamSampler

        <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Auto)>
        Private Structure MEMORYSTATUSEX
            Public dwLength As UInteger
            Public dwMemoryLoad As UInteger
            Public ullTotalPhys As ULong
            Public ullAvailPhys As ULong
            Public ullTotalPageFile As ULong
            Public ullAvailPageFile As ULong
            Public ullTotalVirtual As ULong
            Public ullAvailVirtual As ULong
            Public ullAvailExtendedVirtual As ULong
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure PERFORMANCE_INFORMATION
            Public cb As UInteger
            Public CommitTotal As UIntPtr
            Public CommitLimit As UIntPtr
            Public CommitPeak As UIntPtr
            Public PhysicalTotal As UIntPtr
            Public PhysicalAvailable As UIntPtr
            Public SystemCache As UIntPtr
            Public KernelTotal As UIntPtr
            Public KernelPaged As UIntPtr
            Public KernelNonpaged As UIntPtr
            Public PageSize As UIntPtr
            Public HandleCount As UInteger
            Public ProcessCount As UInteger
            Public ThreadCount As UInteger
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure SYSTEM_MEMORY_LIST_INFORMATION
            Public ZeroPageCount As UIntPtr
            Public FreePageCount As UIntPtr
            Public ModifiedPageCount As UIntPtr
            Public ModifiedNoWritePageCount As UIntPtr
            Public BadPageCount As UIntPtr
            Public PageCountByPriority0 As UIntPtr
            Public PageCountByPriority1 As UIntPtr
            Public PageCountByPriority2 As UIntPtr
            Public PageCountByPriority3 As UIntPtr
            Public PageCountByPriority4 As UIntPtr
            Public PageCountByPriority5 As UIntPtr
            Public PageCountByPriority6 As UIntPtr
            Public PageCountByPriority7 As UIntPtr
            Public RepurposedPagesByPriority0 As UIntPtr
            Public RepurposedPagesByPriority1 As UIntPtr
            Public RepurposedPagesByPriority2 As UIntPtr
            Public RepurposedPagesByPriority3 As UIntPtr
            Public RepurposedPagesByPriority4 As UIntPtr
            Public RepurposedPagesByPriority5 As UIntPtr
            Public RepurposedPagesByPriority6 As UIntPtr
            Public RepurposedPagesByPriority7 As UIntPtr
            Public ModifiedPageCountPageFile As UIntPtr
        End Structure

        Private Const SystemMemoryListInformation As Integer = 80
        Private Const STATUS_SUCCESS As UInteger = 0
        Private Const RSMB As UInteger = &H52534D42UI

        <DllImport("kernel32.dll", SetLastError:=True)>
        Private Shared Function GlobalMemoryStatusEx(ByRef lpBuffer As MEMORYSTATUSEX) As <MarshalAs(UnmanagedType.Bool)> Boolean
        End Function

        <DllImport("psapi.dll", SetLastError:=True)>
        Private Shared Function GetPerformanceInfo(ByRef pPerformanceInformation As PERFORMANCE_INFORMATION, cb As UInteger) As <MarshalAs(UnmanagedType.Bool)> Boolean
        End Function

        <DllImport("ntdll.dll")>
        Private Shared Function NtQuerySystemInformation(SystemInformationClass As Integer,
                                                         SystemInformation As IntPtr,
                                                         SystemInformationLength As UInteger,
                                                         ByRef ReturnLength As UInteger) As UInteger
        End Function

        <DllImport("kernel32.dll", SetLastError:=True)>
        Private Shared Function GetSystemFirmwareTable(FirmwareTableProviderSignature As UInteger,
                                                       FirmwareTableID As UInteger,
                                                       pFirmwareTableBuffer As IntPtr,
                                                       BufferSize As UInteger) As UInteger
        End Function

        Public Structure MemorySample
            Public TotalBytes As ULong
            Public AvailableBytes As ULong
            Public InUseBytes As ULong
            Public ModifiedBytes As ULong
            Public StandbyBytes As ULong
            Public FreeBytes As ULong
            Public CommittedBytes As ULong
            Public CommitLimitBytes As ULong
            Public PageSize As ULong
            Public MemoryLoadPercent As Integer
        End Structure

        Public Structure HardwareInfo
            Public SpeedMTs As Integer
            Public ConfiguredSpeedMTs As Integer
            Public DdrType As String
            Public TotalCapacity As ULong
            Public ModuleCount As Integer
        End Structure

        Private ReadOnly _hw As HardwareInfo

        Public Sub New()
            _hw = 解析SMBIOS内存信息()
        End Sub

        Public ReadOnly Property Hardware As HardwareInfo
            Get
                Return _hw
            End Get
        End Property

        Public Shared Function Sample() As MemorySample
            Dim r As New MemorySample()
            Dim mse As New MEMORYSTATUSEX() With {.dwLength = CUInt(Marshal.SizeOf(Of MEMORYSTATUSEX)())}
            If GlobalMemoryStatusEx(mse) Then
                r.TotalBytes = mse.ullTotalPhys
                r.AvailableBytes = mse.ullAvailPhys
                r.MemoryLoadPercent = CInt(mse.dwMemoryLoad)
            End If

            Dim pi As New PERFORMANCE_INFORMATION With {
                .cb = CUInt(Marshal.SizeOf(Of PERFORMANCE_INFORMATION)())
            }
            Dim pageSize As ULong = 4096
            If GetPerformanceInfo(pi, pi.cb) Then
                pageSize = pi.PageSize.ToUInt64()
                If pageSize = 0 Then pageSize = 4096
                r.CommittedBytes = pi.CommitTotal.ToUInt64() * pageSize
                r.CommitLimitBytes = pi.CommitLimit.ToUInt64() * pageSize
            End If
            r.PageSize = pageSize

            Dim sz As UInteger = CUInt(Marshal.SizeOf(Of SYSTEM_MEMORY_LIST_INFORMATION)())
            Dim buf As IntPtr = Marshal.AllocHGlobal(CInt(sz))
            Try
                Dim ret As UInteger = 0
                Dim status As UInteger = NtQuerySystemInformation(SystemMemoryListInformation, buf, sz, ret)
                If status = STATUS_SUCCESS Then
                    Dim info = Marshal.PtrToStructure(Of SYSTEM_MEMORY_LIST_INFORMATION)(buf)
                    Dim standby As ULong = 0UL
                    standby += info.PageCountByPriority0.ToUInt64()
                    standby += info.PageCountByPriority1.ToUInt64()
                    standby += info.PageCountByPriority2.ToUInt64()
                    standby += info.PageCountByPriority3.ToUInt64()
                    standby += info.PageCountByPriority4.ToUInt64()
                    standby += info.PageCountByPriority5.ToUInt64()
                    standby += info.PageCountByPriority6.ToUInt64()
                    standby += info.PageCountByPriority7.ToUInt64()
                    r.StandbyBytes = standby * pageSize
                    r.FreeBytes = (info.ZeroPageCount.ToUInt64() + info.FreePageCount.ToUInt64()) * pageSize
                    r.ModifiedBytes = info.ModifiedPageCount.ToUInt64() * pageSize
                End If
            Finally
                Marshal.FreeHGlobal(buf)
            End Try

            ' 任务管理器："使用中" = 总 - (备用 + 可用 + 已修改)
            Dim known As ULong = r.StandbyBytes + r.FreeBytes + r.ModifiedBytes
            If r.TotalBytes > known Then
                r.InUseBytes = r.TotalBytes - known
            ElseIf r.TotalBytes > r.AvailableBytes Then
                r.InUseBytes = r.TotalBytes - r.AvailableBytes
            End If
            Return r
        End Function

        Private Shared Function 解析SMBIOS内存信息() As HardwareInfo
            Dim info As New HardwareInfo() With {.DdrType = ""}
            Try
                Dim size As UInteger = GetSystemFirmwareTable(RSMB, 0, IntPtr.Zero, 0)
                If size = 0 Then Return info
                Dim buf As IntPtr = Marshal.AllocHGlobal(CInt(size))
                Try
                    Dim got As UInteger = GetSystemFirmwareTable(RSMB, 0, buf, size)
                    If got = 0 OrElse got > size Then Return info
                    ' RawSMBIOSData: Used20CallingMethod(1) MajorVer(1) MinorVer(1) DmiRevision(1) Length(4) SMBIOSTableData[Length]
                    Dim tableLen As Integer = Marshal.ReadInt32(buf, 4)
                    Dim tableStart As IntPtr = IntPtr.Add(buf, 8)
                    Dim ending As Long = tableStart.ToInt64() + tableLen
                    Dim ptr As IntPtr = tableStart

                    Dim bestSpeed As Integer = 0
                    Dim bestConfSpeed As Integer = 0
                    Dim firstType As Integer = 0
                    Dim total As ULong = 0UL
                    Dim modules As Integer = 0

                    Do While ptr.ToInt64() + 4 <= ending
                        Dim tType As Byte = Marshal.ReadByte(ptr, 0)
                        Dim tLen As Byte = Marshal.ReadByte(ptr, 1)
                        If tLen < 4 Then Exit Do
                        If tType = 127 Then Exit Do ' End-of-table

                        If tType = 17 Then ' Memory Device
                            Dim sizeField As Integer = CUShort(Marshal.ReadInt16(ptr, &HC)) And &HFFFF
                            Dim capBytes As ULong = 0UL
                            If sizeField = &H7FFF Then
                                If tLen >= &H20 Then
                                    Dim extSize As UInteger = CUInt(Marshal.ReadInt32(ptr, &H1C)) And &H7FFFFFFFUI
                                    capBytes = CULng(extSize) * 1024UL * 1024UL
                                End If
                            ElseIf sizeField <> 0 AndAlso sizeField <> &HFFFF Then
                                If (sizeField And &H8000) <> 0 Then
                                    capBytes = CULng(sizeField And &H7FFF) * 1024UL
                                Else
                                    capBytes = CULng(sizeField) * 1024UL * 1024UL
                                End If
                            End If

                            Dim memType As Byte = If(tLen > &H12, Marshal.ReadByte(ptr, &H12), CByte(0))
                            Dim speed As Integer = 0
                            If tLen > &H16 Then
                                speed = CUShort(Marshal.ReadInt16(ptr, &H15)) And &HFFFF
                                If speed = &HFFFF AndAlso tLen >= &H58 Then
                                    speed = Marshal.ReadInt32(ptr, &H54)
                                End If
                            End If
                            Dim confSpeed As Integer = 0
                            If tLen > &H22 Then
                                confSpeed = CUShort(Marshal.ReadInt16(ptr, &H20)) And &HFFFF
                                If confSpeed = &HFFFF AndAlso tLen >= &H5C Then
                                    confSpeed = Marshal.ReadInt32(ptr, &H58)
                                End If
                            End If

                            If capBytes > 0UL Then
                                modules += 1
                                total += capBytes
                                If firstType = 0 Then firstType = memType
                                If speed > bestSpeed Then bestSpeed = speed
                                If confSpeed > bestConfSpeed Then bestConfSpeed = confSpeed
                            End If
                        End If

                        ' 跳过结构主体及其字符串区（以双 NUL 结尾）
                        Dim p As IntPtr = IntPtr.Add(ptr, tLen)
                        Do While p.ToInt64() + 1 < ending
                            If Marshal.ReadByte(p, 0) = 0 AndAlso Marshal.ReadByte(p, 1) = 0 Then
                                p = IntPtr.Add(p, 2)
                                Exit Do
                            End If
                            p = IntPtr.Add(p, 1)
                        Loop
                        If p.ToInt64() <= ptr.ToInt64() Then Exit Do
                        ptr = p
                    Loop

                    info.SpeedMTs = bestSpeed
                    info.ConfiguredSpeedMTs = bestConfSpeed
                    info.DdrType = 内存类型转文本(firstType)
                    info.TotalCapacity = total
                    info.ModuleCount = modules
                Finally
                    Marshal.FreeHGlobal(buf)
                End Try
            Catch
            End Try
            Return info
        End Function

        Private Shared Function 内存类型转文本(t As Integer) As String
            Select Case t
                Case &H12 : Return "DDR"
                Case &H13 : Return "DDR2"
                Case &H14 : Return "DDR2 FB-DIMM"
                Case &H18 : Return "DDR3"
                Case &H1A : Return "DDR4"
                Case &H1B : Return "LPDDR"
                Case &H1C : Return "LPDDR2"
                Case &H1D : Return "LPDDR3"
                Case &H1E : Return "LPDDR4"
                Case &H20 : Return "HBM"
                Case &H21 : Return "HBM2"
                Case &H22 : Return "DDR5"
                Case &H23 : Return "LPDDR5"
                Case Else : Return ""
            End Select
        End Function
    End Class
#End Region

#Region "本地化"
    ''' <summary>字段标识。用于可自定义顶/底部文字与悬停读数面板。</summary>
    Public Enum RamTextField
        Total
        InUse
        Modified
        Standby
        Available
        Speed
        Committed
        DdrType
        SlotCount
    End Enum

    ''' <summary>文字槽位置。</summary>
    Public Enum TextSlotPosition
        Hidden
        Top
        Bottom
    End Enum

    ''' <summary>
    ''' 控件显示语言。<see cref="Custom"/> 表示完全由开发者接管——此时控件不会覆盖
    ''' <see cref="RamMonitorStrings"/> 中的任何字段，由使用方自行赋值。
    ''' </summary>
    Public Enum RamMonitorLanguage
        Chinese
        English
        Custom
    End Enum

    ''' <summary>
    ''' RamMonitor 使用的全部可本地化文本。
    ''' 使用 <see cref="RamMonitor.Language"/> 属性切换 Chinese / English 预设，
    ''' 或将其设为 <see cref="RamMonitorLanguage.Custom"/> 后直接对这里的字段赋值以对接任意语言。
    ''' </summary>
    Public NotInheritable Class RamMonitorStrings
        Private Sub New()
        End Sub

        Public Shared Property Total As String
        Public Shared Property InUse As String
        Public Shared Property Modified As String
        Public Shared Property Standby As String
        Public Shared Property Available As String
        Public Shared Property Speed As String
        Public Shared Property Committed As String
        Public Shared Property DdrType As String
        Public Shared Property SlotCount As String
        Public Shared Property UnitMTs As String
        Public Shared Property SlotsFormat As String

        Shared Sub New()
            ApplyChinese()
        End Sub

        ''' <summary>应用内置中文预设。</summary>
        Public Shared Sub ApplyChinese()
            Total = "总"
            InUse = "已用"
            Modified = "已修改"
            Standby = "备用"
            Available = "可用"
            Speed = "速度"
            Committed = "已提交"
            DdrType = "类型"
            SlotCount = "插槽"
            UnitMTs = "MT/s"
            SlotsFormat = "{0}/{1}"
        End Sub

        ''' <summary>应用内置英文预设。</summary>
        Public Shared Sub ApplyEnglish()
            Total = "Total"
            InUse = "In use"
            Modified = "Modified"
            Standby = "Standby"
            Available = "Available"
            Speed = "Speed"
            Committed = "Committed"
            DdrType = "Type"
            SlotCount = "Slots"
            UnitMTs = "MT/s"
            SlotsFormat = "{0} of {1}"
        End Sub
    End Class
#End Region

#Region "采样与数据"
    Private 采样器实例 As RamSampler
    Private ReadOnly 采样定时器 As New System.Windows.Forms.Timer() With {.Interval = 1000}
    Private ReadOnly 历史数据 As New List(Of HistoryPoint)()
    Private ReadOnly 历史锁 As New Object()
    Private 最近样本 As RamSampler.MemorySample

    ' 图表几何（用于悬停命中测试）
    Private _图表矩形 As RectangleF
    Private _图表样本数 As Integer

    ' 悬停读数状态
    Private _悬停X As Single
    Private _悬停有效 As Boolean
    Private _悬停样本索引 As Integer = -1

    ' 字段位置 + 顺序配置
    Private Structure FieldLayout
        Public Position As TextSlotPosition
        Public Order As Integer
    End Structure
    Private ReadOnly 字段配置 As New Dictionary(Of RamTextField, FieldLayout) From {
        {RamTextField.Total, New FieldLayout() With {.Position = TextSlotPosition.Top, .Order = 0}},
        {RamTextField.InUse, New FieldLayout() With {.Position = TextSlotPosition.Top, .Order = 1}},
        {RamTextField.Modified, New FieldLayout() With {.Position = TextSlotPosition.Top, .Order = 2}},
        {RamTextField.Standby, New FieldLayout() With {.Position = TextSlotPosition.Top, .Order = 3}},
        {RamTextField.Available, New FieldLayout() With {.Position = TextSlotPosition.Top, .Order = 4}},
        {RamTextField.Speed, New FieldLayout() With {.Position = TextSlotPosition.Bottom, .Order = 0}},
        {RamTextField.Committed, New FieldLayout() With {.Position = TextSlotPosition.Bottom, .Order = 1}},
        {RamTextField.DdrType, New FieldLayout() With {.Position = TextSlotPosition.Bottom, .Order = 2}},
        {RamTextField.SlotCount, New FieldLayout() With {.Position = TextSlotPosition.Bottom, .Order = 3}}
    }

    Private Structure HistoryPoint
        Public TotalBytes As ULong
        Public InUseBytes As ULong
        Public ModifiedBytes As ULong
        Public StandbyBytes As ULong
        Public FreeBytes As ULong
    End Structure

    Private Sub 确保采样器()
        If 采样器实例 Is Nothing Then
            Try
                采样器实例 = New RamSampler()
                最近样本 = RamSampler.Sample()
            Catch
                采样器实例 = Nothing
            End Try
            SyncLock 历史锁
                历史数据.Clear()
            End SyncLock
        End If
    End Sub

    Private Sub 采样定时器_Tick(sender As Object, e As EventArgs)
        If DesignMode OrElse 采样器实例 Is Nothing Then Return
        Try
            最近样本 = RamSampler.Sample()
        Catch
            Return
        End Try

        If 启用历史记录 Then
            Dim hp As New HistoryPoint() With {
                .TotalBytes = 最近样本.TotalBytes,
                .InUseBytes = 最近样本.InUseBytes,
                .ModifiedBytes = 最近样本.ModifiedBytes,
                .StandbyBytes = 最近样本.StandbyBytes,
                .FreeBytes = 最近样本.FreeBytes
            }
            SyncLock 历史锁
                历史数据.Add(hp)
                Dim overflow As Integer = 历史数据.Count - 记录长度值
                If overflow > 0 Then 历史数据.RemoveRange(0, overflow)
            End SyncLock
        End If

        Me.Invalidate()
        RaiseEvent SampleUpdated(Me, EventArgs.Empty)
    End Sub

    Private Function 获取历史快照() As HistoryPoint()
        SyncLock 历史锁
            Return 历史数据.ToArray()
        End SyncLock
    End Function

    ''' <summary>手动立即执行一次采样。</summary>
    Public Sub ForceSample()
        If 采样器实例 Is Nothing Then Return
        Try
            最近样本 = RamSampler.Sample()
            Me.Invalidate()
        Catch
        End Try
    End Sub

    ''' <summary>清空全部历史记录。</summary>
    Public Sub Reset()
        SyncLock 历史锁
            历史数据.Clear()
        End SyncLock
        Me.Invalidate()
    End Sub

    ''' <summary>当前采样的只读快照。</summary>
    <Browsable(False)>
    Friend ReadOnly Property CurrentSample As RamSampler.MemorySample
        Get
            Return 最近样本
        End Get
    End Property
#End Region

#Region "绘制"
    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        If 圆角半径值 > 0 OrElse MyBase.BackColor.A < 255 Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Me.Width <= 0 OrElse Me.Height <= 0 Then Return

        Dim ssaa As Integer = Math.Max(1, CInt(超采样倍率))
        If Class1.GlobalSSAA <> Class1.SuperSamplingScaleEnum.OFF Then ssaa = Math.Max(ssaa, CInt(Class1.GlobalSSAA))

        Using scope = D2DHelper.BeginPaint(e, Me, GetOrCreateDCRenderTarget(), ssaa, _ssaaCache)
            Dim gRT As ID2D1RenderTarget = scope.GraphicsRenderTarget
            Dim dcRT As ID2D1DCRenderTarget = scope.DCRenderTarget

            If 圆角半径值 > 0 OrElse MyBase.BackColor.A < 255 Then
                TransparentBackgroundCache.PaintBackgroundFor_D2D(Me, gRT, _backgroundSource)
                If MyBase.BackColor.A > 0 AndAlso MyBase.BackColor.A < 255 Then
                    Using b = gRT.CreateSolidColorBrush(D2DHelper.ToColor4(MyBase.BackColor))
                        gRT.FillRectangle(New Vortice.Mathematics.Rect(0, 0, Me.Width, Me.Height), b)
                    End Using
                End If
            End If

            绘制图形内容_D2D(gRT)
            scope.FlushGraphics()
            绘制文字内容_D2D(dcRT)

            If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
                绘制禁用遮罩_D2D(dcRT)
            End If
        End Using
    End Sub

    Private Sub 绘制图形内容_D2D(rt As ID2D1RenderTarget)
        Dim s As Single = DpiScale()
        Dim pad As Padding = Me.Padding
        Dim rect As New RectangleF(pad.Left * s, pad.Top * s,
                                   Math.Max(0, Me.Width - (pad.Left + pad.Right) * s),
                                   Math.Max(0, Me.Height - (pad.Top + pad.Bottom) * s))
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return

        绘制背景与边框_D2D(rt, rect, s)

        Dim innerPad As Single = 核心内边距值 * s
        Dim inner As New RectangleF(rect.X + innerPad, rect.Y + innerPad,
                                    Math.Max(0, rect.Width - innerPad * 2),
                                    Math.Max(0, rect.Height - innerPad * 2))
        If inner.Width <= 0 OrElse inner.Height <= 0 Then Return

        Dim fontHeight As Single = 文本像素高度(s)
        Dim tp As Padding = 文字内边距值
        Dim stripH As Single = fontHeight + (tp.Top + tp.Bottom) * s
        Dim topText As String = 构造槽位文字(TextSlotPosition.Top)
        Dim bottomText As String = 构造槽位文字(TextSlotPosition.Bottom)
        Dim topStripH As Single = If(Not String.IsNullOrEmpty(topText), Math.Min(stripH, inner.Height), 0)
        Dim bottomStripH As Single = If(Not String.IsNullOrEmpty(bottomText), Math.Min(stripH, Math.Max(0, inner.Height - topStripH)), 0)
        Dim graphRect As New RectangleF(inner.X, inner.Y + topStripH,
                                        inner.Width,
                                        Math.Max(0, inner.Height - topStripH - bottomStripH))

        If graphRect.Height >= 2 AndAlso graphRect.Width >= 2 Then
            Dim hasClip As Boolean = 圆角半径值 > 0
            If hasClip Then
                Using geo = RectangleRenderer.创建圆角矩形几何(rect, 圆角半径值 * s)
                    D2DHelper.PushGeometryClip(rt, geo, rect)
                    Try
                        绘制历史图表_D2D(rt, graphRect, s)
                    Finally
                        rt.PopLayer()
                    End Try
                End Using
            Else
                绘制历史图表_D2D(rt, graphRect, s)
            End If
        End If
    End Sub

    Private Sub 绘制历史图表_D2D(rt As ID2D1RenderTarget, rect As RectangleF, s As Single)
        If 图表背景颜色值.A > 0 Then
            Using b = rt.CreateSolidColorBrush(D2DHelper.ToColor4(图表背景颜色值))
                rt.FillRectangle(D2DHelper.ToD2DRect(rect), b)
            End Using
        End If

        Dim hist = 获取历史快照()
        Dim n As Integer = hist.Length
        Dim cap As Integer = Math.Max(2, 记录长度值)
        _图表矩形 = rect
        _图表样本数 = n
        If n < 2 Then Return

        Dim step_ As Single = rect.Width / CSng(cap - 1)

        Dim inUseTop(n - 1) As Vector2
        Dim modTop(n - 1) As Vector2
        Dim standbyTop(n - 1) As Vector2
        For i As Integer = 0 To n - 1
            Dim x As Single = rect.Right - CSng(n - 1 - i) * step_
            Dim total As Double = If(hist(i).TotalBytes > 0UL, CDbl(hist(i).TotalBytes), 0.0)
            Dim a As Single = 0, bm As Single = 0, c As Single = 0
            If total > 0.0 Then
                a = CSng(hist(i).InUseBytes / total)
                bm = CSng(hist(i).ModifiedBytes / total)
                c = CSng(hist(i).StandbyBytes / total)
            End If
            a = Math.Max(0, Math.Min(1, a))
            bm = Math.Max(0, Math.Min(1 - a, bm))
            c = Math.Max(0, Math.Min(1 - a - bm, c))
            If bm * rect.Height < 1.0F Then bm = 0
            inUseTop(i) = New Vector2(x, rect.Bottom - a * rect.Height)
            modTop(i) = New Vector2(x, rect.Bottom - (a + bm) * rect.Height)
            standbyTop(i) = New Vector2(x, rect.Bottom - (a + bm + c) * rect.Height)
        Next

        填充堆叠区_D2D(rt, 备用填充颜色值, standbyTop, rect)
        填充堆叠区_D2D(rt, 已修改填充颜色值, modTop, rect)
        填充堆叠区_D2D(rt, 已用填充颜色值, inUseTop, rect)

        If 图表线条颜色值.A > 0 AndAlso 图表线条粗细值 > 0 Then
            Using b = rt.CreateSolidColorBrush(D2DHelper.ToColor4(图表线条颜色值))
                Dim lineW As Single = 图表线条粗细值 * s
                For i As Integer = 0 To standbyTop.Length - 2
                    rt.DrawLine(standbyTop(i), standbyTop(i + 1), b, lineW)
                Next
            End Using
        End If
    End Sub

    Private Shared Sub 填充堆叠区_D2D(rt As ID2D1RenderTarget, color As Color, topPts As Vector2(), rect As RectangleF)
        If color.A = 0 Then Return
        If topPts Is Nothing OrElse topPts.Length < 2 Then Return
        Using geo = 创建堆叠填充几何(rect, topPts)
            Using b = rt.CreateSolidColorBrush(D2DHelper.ToColor4(color))
                rt.FillGeometry(geo, b)
            End Using
        End Using
    End Sub

    Private Shared Function 创建堆叠填充几何(rect As RectangleF, topPts As Vector2()) As ID2D1PathGeometry
        Dim geo As ID2D1PathGeometry = D2DHelper.GetD2DFactory().CreatePathGeometry()
        Dim sink As ID2D1GeometrySink = geo.Open()
        Try
            sink.BeginFigure(topPts(0), FigureBegin.Filled)
            For i As Integer = 1 To topPts.Length - 1
                sink.AddLine(topPts(i))
            Next
            sink.AddLine(New Vector2(topPts(topPts.Length - 1).X, rect.Bottom))
            sink.AddLine(New Vector2(topPts(0).X, rect.Bottom))
            sink.EndFigure(FigureEnd.Closed)
            sink.Close()
        Finally
            sink.Dispose()
        End Try
        Return geo
    End Function

    Private Sub 绘制背景与边框_D2D(rt As ID2D1RenderTarget, rect As RectangleF, s As Single)
        Dim r As Single = 圆角半径值 * s
        If 核心背景颜色值.A <= 0 Then Return
        If r > 0 Then
            Using geo = RectangleRenderer.创建圆角矩形几何(rect, r)
                RectangleRenderer.绘制圆角背景_D2D(rt, geo, rect, 核心背景颜色值, Color.Empty, System.Windows.Forms.Orientation.Vertical)
            End Using
        Else
            RectangleRenderer.绘制矩形背景_D2D(rt, rect, 核心背景颜色值, Color.Empty, System.Windows.Forms.Orientation.Vertical)
        End If
    End Sub

    Private Sub 绘制禁用遮罩_D2D(rt As ID2D1RenderTarget)
        Dim s As Single = DpiScale()
        Dim pad As Padding = Me.Padding
        Dim rect As New RectangleF(pad.Left * s, pad.Top * s,
                                   Math.Max(0, Me.Width - (pad.Left + pad.Right) * s),
                                   Math.Max(0, Me.Height - (pad.Top + pad.Bottom) * s))
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim r As Single = 圆角半径值 * s
        If r > 0 Then
            Using geo = RectangleRenderer.创建圆角矩形几何(rect, r)
                RectangleRenderer.绘制圆角背景_D2D(rt, geo, rect, 禁用时遮罩颜色, Color.Empty, System.Windows.Forms.Orientation.Vertical)
            End Using
        Else
            RectangleRenderer.绘制矩形背景_D2D(rt, rect, 禁用时遮罩颜色, Color.Empty, System.Windows.Forms.Orientation.Vertical)
        End If
    End Sub

    Private Sub 绘制文字内容_D2D(rt As ID2D1RenderTarget)
        Dim s As Single = DpiScale()
        Dim pad As Padding = Me.Padding
        Dim rect As New RectangleF(pad.Left * s, pad.Top * s,
                                   Math.Max(0, Me.Width - (pad.Left + pad.Right) * s),
                                   Math.Max(0, Me.Height - (pad.Top + pad.Bottom) * s))
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return

        Dim innerPad As Single = 核心内边距值 * s
        Dim inner As New RectangleF(rect.X + innerPad, rect.Y + innerPad,
                                    Math.Max(0, rect.Width - innerPad * 2),
                                    Math.Max(0, rect.Height - innerPad * 2))
        If inner.Width <= 0 OrElse inner.Height <= 0 Then Return

        Dim fontHeight As Single = 文本像素高度(s)
        Dim tp As Padding = 文字内边距值
        Dim stripH As Single = fontHeight + (tp.Top + tp.Bottom) * s
        Dim topText As String = 构造槽位文字(TextSlotPosition.Top)
        Dim bottomText As String = 构造槽位文字(TextSlotPosition.Bottom)
        Dim topStripH As Single = If(Not String.IsNullOrEmpty(topText), Math.Min(stripH, inner.Height), 0)
        Dim bottomStripH As Single = If(Not String.IsNullOrEmpty(bottomText), Math.Min(stripH, Math.Max(0, inner.Height - topStripH)), 0)
        Dim graphRect As New RectangleF(inner.X, inner.Y + topStripH,
                                        inner.Width,
                                        Math.Max(0, inner.Height - topStripH - bottomStripH))

        If topStripH > 0 Then
            绘制文字_D2D(rt, New RectangleF(inner.X, inner.Y, inner.Width, topStripH), topText, s, 顶部文字对齐值)
        End If
        If bottomStripH > 0 Then
            绘制文字_D2D(rt, New RectangleF(inner.X, inner.Bottom - bottomStripH, inner.Width, bottomStripH), bottomText, s, 底部文字对齐值)
        End If
        If 启用悬停读数值 AndAlso _悬停有效 AndAlso graphRect.Height >= 2 AndAlso graphRect.Width >= 2 Then
            绘制悬停读数_D2D(rt, graphRect, s)
        End If
    End Sub

    Private Function 文本像素高度(s As Single) As Single
        Return Me.Font.SizeInPoints * (96.0F / 72.0F) * s
    End Function

    Private Shared Function 转文本水平对齐(a As ContentAlignment) As TextAlignment
        If a = ContentAlignment.TopLeft OrElse a = ContentAlignment.MiddleLeft OrElse a = ContentAlignment.BottomLeft Then Return TextAlignment.Leading
        If a = ContentAlignment.TopRight OrElse a = ContentAlignment.MiddleRight OrElse a = ContentAlignment.BottomRight Then Return TextAlignment.Trailing
        Return TextAlignment.Center
    End Function

    Private Shared Function 转文本垂直对齐(a As ContentAlignment) As ParagraphAlignment
        If a = ContentAlignment.TopLeft OrElse a = ContentAlignment.TopCenter OrElse a = ContentAlignment.TopRight Then Return ParagraphAlignment.Near
        If a = ContentAlignment.BottomLeft OrElse a = ContentAlignment.BottomCenter OrElse a = ContentAlignment.BottomRight Then Return ParagraphAlignment.Far
        Return ParagraphAlignment.Center
    End Function

    Private Sub 绘制文字_D2D(rt As ID2D1RenderTarget, inner As RectangleF, text As String, s As Single, align As ContentAlignment)
        If String.IsNullOrEmpty(text) Then Return
        Dim tp As Padding = 文字内边距值
        Dim textRect As New RectangleF(
            inner.X + tp.Left * s,
            inner.Y + tp.Top * s,
            Math.Max(0, inner.Width - (tp.Left + tp.Right) * s),
            Math.Max(0, inner.Height - (tp.Top + tp.Bottom) * s))
        If textRect.Width <= 0 OrElse textRect.Height <= 0 Then Return

        Dim weight As FontWeight = If(Me.Font.Bold, FontWeight.Bold, FontWeight.Normal)
        Dim style As FontStyle = If(Me.Font.Italic, FontStyle.Italic, FontStyle.Normal)
        Dim fmt = _textFormatCache.Get(Me.Font.FontFamily.Name, weight, style, 文本像素高度(s),
                                       转文本水平对齐(align), 转文本垂直对齐(align), True)
        Using layout = D2DHelper.GetDWriteFactory().CreateTextLayout(If(text, ""), fmt, textRect.Width, textRect.Height)
            Using b = rt.CreateSolidColorBrush(D2DHelper.ToColor4(Me.ForeColor))
                rt.DrawTextLayout(New Vector2(textRect.X, textRect.Y), layout, b)
            End Using
        End Using
    End Sub

    Private Sub 绘制悬停读数_D2D(rt As ID2D1RenderTarget, graphRect As RectangleF, s As Single)
        Dim hist = 获取历史快照()
        Dim n As Integer = hist.Length
        If n < 1 Then Return
        Dim idx As Integer = 样本索引从X坐标(_悬停X, graphRect)
        If idx < 0 OrElse idx >= n Then Return
        Dim hp = hist(idx)
        Dim total As ULong = hp.TotalBytes
        If total = 0UL Then Return

        Dim cap As Integer = Math.Max(2, 记录长度值)
        Dim step_ As Single = graphRect.Width / CSng(cap - 1)
        Dim xAtSample As Single = graphRect.Right - CSng(n - 1 - idx) * step_

        If 悬停线颜色值.A > 0 Then
            Using b = rt.CreateSolidColorBrush(D2DHelper.ToColor4(悬停线颜色值))
                rt.DrawLine(New Vector2(xAtSample, graphRect.Top), New Vector2(xAtSample, graphRect.Bottom), b, Math.Max(1.0F, 悬停线粗细值 * s))
            End Using
        End If

        Dim lines As New List(Of (label As String, value As String)) From {
            (RamMonitorStrings.InUse, 格式化字节(hp.InUseBytes)),
            (RamMonitorStrings.Standby, 格式化字节(hp.StandbyBytes)),
            (RamMonitorStrings.Available, 格式化字节(hp.FreeBytes + hp.StandbyBytes))
        }

        Dim weight As FontWeight = If(Me.Font.Bold, FontWeight.Bold, FontWeight.Normal)
        Dim style As FontStyle = If(Me.Font.Italic, FontStyle.Italic, FontStyle.Normal)
        Dim fmt = _textFormatCache.Get(Me.Font.FontFamily.Name, weight, style, 文本像素高度(s), TextAlignment.Leading, ParagraphAlignment.Near, False)
        Dim lineH As Single = 文本像素高度(s)
        Dim maxLabel As Single = 0, maxValue As Single = 0
        For i As Integer = 0 To lines.Count - 1
            maxLabel = Math.Max(maxLabel, 测量单行文字宽度(fmt, lines(i).label))
            maxValue = Math.Max(maxValue, 测量单行文字宽度(fmt, lines(i).value))
        Next

        Dim panelPad As Single = 6 * s
        Dim gap As Single = 4 * s
        Dim innerGap As Single = 6 * s
        Dim panelW As Single = panelPad * 2 + maxLabel + innerGap + maxValue
        Dim panelH As Single = panelPad * 2 + lineH * lines.Count
        Dim showOnLeft As Boolean = (xAtSample - gap - panelW) >= graphRect.Left
        Dim panelX As Single = If(showOnLeft, xAtSample - gap - panelW, xAtSample + gap)
        If Not showOnLeft AndAlso panelX + panelW > graphRect.Right Then panelX = graphRect.Right - panelW
        If panelX < graphRect.Left Then panelX = graphRect.Left
        Dim panelY As Single = graphRect.Top + (graphRect.Height - panelH) / 2.0F
        If panelY < graphRect.Top Then panelY = graphRect.Top
        If panelY + panelH > graphRect.Bottom Then panelY = graphRect.Bottom - panelH
        Dim panelRect As New RectangleF(panelX, panelY, panelW, panelH)

        If 悬停面板背景值.A > 0 Then
            Using b = rt.CreateSolidColorBrush(D2DHelper.ToColor4(悬停面板背景值))
                rt.FillRectangle(D2DHelper.ToD2DRect(panelRect), b)
            End Using
        End If
        If 悬停面板边框值.A > 0 Then
            Using b = rt.CreateSolidColorBrush(D2DHelper.ToColor4(悬停面板边框值))
                rt.DrawRectangle(D2DHelper.ToD2DRect(panelRect), b, 1.0F)
            End Using
        End If

        Dim fore As Color = If(悬停面板前景值.A > 0, 悬停面板前景值, Me.ForeColor)
        Using b = rt.CreateSolidColorBrush(D2DHelper.ToColor4(fore))
            For i As Integer = 0 To lines.Count - 1
                Dim rowY As Single = panelRect.Y + panelPad + i * lineH
                If showOnLeft Then
                    绘制悬停单行_D2D(rt, fmt, b, lines(i).value, panelRect.Right - panelPad - maxLabel - innerGap - maxValue, rowY, maxValue, lineH)
                    绘制悬停单行_D2D(rt, fmt, b, lines(i).label, panelRect.Right - panelPad - maxLabel, rowY, maxLabel, lineH)
                Else
                    绘制悬停单行_D2D(rt, fmt, b, lines(i).label, panelRect.X + panelPad, rowY, maxLabel, lineH)
                    绘制悬停单行_D2D(rt, fmt, b, lines(i).value, panelRect.X + panelPad + maxLabel + innerGap, rowY, maxValue, lineH)
                End If
            Next
        End Using
    End Sub

    Private Shared Function 测量单行文字宽度(fmt As IDWriteTextFormat, text As String) As Single
        Using layout = D2DHelper.GetDWriteFactory().CreateTextLayout(If(text, ""), fmt, 10000.0F, 1000.0F)
            Return CSng(Math.Ceiling(layout.Metrics.WidthIncludingTrailingWhitespace))
        End Using
    End Function

    Private Shared Sub 绘制悬停单行_D2D(rt As ID2D1RenderTarget, fmt As IDWriteTextFormat, brush As ID2D1Brush,
                                      text As String, x As Single, y As Single, w As Single, h As Single)
        If w <= 0 OrElse h <= 0 Then Return
        Using layout = D2DHelper.GetDWriteFactory().CreateTextLayout(If(text, ""), fmt, w, h)
            rt.DrawTextLayout(New Vector2(x, y), layout, brush)
        End Using
    End Sub

    Private Sub 绘制图形内容(g As Graphics, drawGfx As Boolean, drawText As Boolean)
        If drawGfx Then
            g.SmoothingMode = Class1.GlobalSmoothingMode
            g.PixelOffsetMode = Class1.GlobalPixelOffsetMode
            g.InterpolationMode = Class1.GlobalInterpolationMode
        End If
        If drawText Then
            g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit
        End If

        Dim s As Single = DpiScale()
        Dim pad As Padding = Me.Padding
        Dim rect As New RectangleF(pad.Left * s, pad.Top * s,
                                   Math.Max(0, Me.Width - (pad.Left + pad.Right) * s),
                                   Math.Max(0, Me.Height - (pad.Top + pad.Bottom) * s))
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return

        If drawGfx Then 绘制背景与边框(g, rect, s)

        Dim innerPad As Single = 核心内边距值 * s
        Dim inner As New RectangleF(rect.X + innerPad, rect.Y + innerPad,
                                    Math.Max(0, rect.Width - innerPad * 2),
                                    Math.Max(0, rect.Height - innerPad * 2))
        If inner.Width <= 0 OrElse inner.Height <= 0 Then Return

        Dim fontHeight As Single = Me.Font.GetHeight(g)
        Dim tp As Padding = 文字内边距值
        Dim stripH As Single = fontHeight + (tp.Top + tp.Bottom) * s
        Dim topText As String = 构造槽位文字(TextSlotPosition.Top)
        Dim bottomText As String = 构造槽位文字(TextSlotPosition.Bottom)
        Dim topStripH As Single = If(Not String.IsNullOrEmpty(topText), Math.Min(stripH, inner.Height), 0)
        Dim bottomStripH As Single = If(Not String.IsNullOrEmpty(bottomText), Math.Min(stripH, Math.Max(0, inner.Height - topStripH)), 0)
        Dim graphRect As New RectangleF(inner.X, inner.Y + topStripH,
                                        inner.Width,
                                        Math.Max(0, inner.Height - topStripH - bottomStripH))

        If drawGfx AndAlso graphRect.Height >= 2 AndAlso graphRect.Width >= 2 Then
            Dim oldClip As Region = 应用圆角裁剪(g, rect, s)
            Try
                绘制历史图表(g, graphRect, s)
            Finally
                恢复裁剪(g, oldClip)
            End Try
        End If

        If drawText Then
            If topStripH > 0 Then
                绘制文字(g, New RectangleF(inner.X, inner.Y, inner.Width, topStripH), topText, s, 顶部文字对齐值)
            End If
            If bottomStripH > 0 Then
                绘制文字(g, New RectangleF(inner.X, inner.Bottom - bottomStripH, inner.Width, bottomStripH), bottomText, s, 底部文字对齐值)
            End If
            ' 悬停读数覆盖层（文字层之后绘制以便显示在所有内容之上）
            If 启用悬停读数值 AndAlso _悬停有效 AndAlso graphRect.Height >= 2 AndAlso graphRect.Width >= 2 Then
                绘制悬停读数(g, graphRect, s)
            End If
        End If
    End Sub

    Private Sub 绘制历史图表(g As Graphics, rect As RectangleF, s As Single)
        If 图表背景颜色值.A > 0 Then
            Using b As New SolidBrush(图表背景颜色值)
                g.FillRectangle(b, rect)
            End Using
        End If

        Dim hist = 获取历史快照()
        Dim n As Integer = hist.Length
        Dim cap As Integer = Math.Max(2, 记录长度值)
        ' 记录几何信息供悬停使用
        _图表矩形 = rect
        _图表样本数 = n
        If n < 2 Then Exit Sub

        Dim step_ As Single = rect.Width / CSng(cap - 1)

        ' 转成比例后堆叠：从底向上 = 已用 / 已修改 / 备用
        Dim inUseTop(n - 1) As PointF
        Dim modTop(n - 1) As PointF
        Dim standbyTop(n - 1) As PointF
        ' 已修改厚度按像素判断：每个样本若其换算厚度 < 1px 则视为 0（避免紧贴已用顶部出现不可察觉的色带）
        For i As Integer = 0 To n - 1
            Dim x As Single = rect.Right - CSng(n - 1 - i) * step_
            Dim total As Double = If(hist(i).TotalBytes > 0UL, CDbl(hist(i).TotalBytes), 0.0)
            Dim a As Single = 0, bm As Single = 0, c As Single = 0
            If total > 0.0 Then
                a = CSng(hist(i).InUseBytes / total)
                bm = CSng(hist(i).ModifiedBytes / total)
                c = CSng(hist(i).StandbyBytes / total)
            End If
            a = Math.Max(0, Math.Min(1, a))
            bm = Math.Max(0, Math.Min(1 - a, bm))
            c = Math.Max(0, Math.Min(1 - a - bm, c))
            If bm * rect.Height < 1.0F Then bm = 0
            inUseTop(i) = New PointF(x, rect.Bottom - a * rect.Height)
            modTop(i) = New PointF(x, rect.Bottom - (a + bm) * rect.Height)
            standbyTop(i) = New PointF(x, rect.Bottom - (a + bm + c) * rect.Height)
        Next

        ' 堆叠填充（从上层往下画）
        填充堆叠区(g, 备用填充颜色值, standbyTop, rect)
        填充堆叠区(g, 已修改填充颜色值, modTop, rect)
        填充堆叠区(g, 已用填充颜色值, inUseTop, rect)

        ' 线条：只画 "总占用 = 已用 + 已修改 + 备用" 的顶线
        If 图表线条颜色值.A > 0 AndAlso 图表线条粗细值 > 0 Then
            Using p As New Pen(图表线条颜色值, 图表线条粗细值 * s)
                p.LineJoin = System.Drawing.Drawing2D.LineJoin.Round
                g.DrawLines(p, standbyTop)
            End Using
        End If
    End Sub

    Private Shared Sub 填充堆叠区(g As Graphics, color As Color, topPts As PointF(), rect As RectangleF)
        If color.A = 0 Then Return
        If topPts Is Nothing OrElse topPts.Length < 2 Then Return
        Dim n As Integer = topPts.Length
        Dim fill(n + 1) As PointF
        Array.Copy(topPts, fill, n)
        fill(n) = New PointF(topPts(n - 1).X, rect.Bottom)
        fill(n + 1) = New PointF(topPts(0).X, rect.Bottom)
        Using b As New SolidBrush(color)
            g.FillPolygon(b, fill)
        End Using
    End Sub

    Private Sub 绘制悬停读数(g As Graphics, graphRect As RectangleF, s As Single)
        Dim hist = 获取历史快照()
        Dim n As Integer = hist.Length
        If n < 1 Then Return
        Dim idx As Integer = 样本索引从X坐标(_悬停X, graphRect)
        If idx < 0 OrElse idx >= n Then Return
        Dim hp = hist(idx)
        Dim total As ULong = hp.TotalBytes
        If total = 0UL Then Return

        ' 对齐到确切的采样 X
        Dim cap As Integer = Math.Max(2, 记录长度值)
        Dim step_ As Single = graphRect.Width / CSng(cap - 1)
        Dim xAtSample As Single = graphRect.Right - CSng(n - 1 - idx) * step_

        ' 绘制垂直刻度线
        If 悬停线颜色值.A > 0 Then
            Using p As New Pen(悬停线颜色值, Math.Max(1.0F, 悬停线粗细值 * s))
                g.DrawLine(p, xAtSample, graphRect.Top, xAtSample, graphRect.Bottom)
            End Using
        End If

        ' 构造三行：已用、备用、可用
        Dim lines As New List(Of (label As String, value As String)) From {
            (RamMonitorStrings.InUse, 格式化字节(hp.InUseBytes)),
            (RamMonitorStrings.Standby, 格式化字节(hp.StandbyBytes)),
            (RamMonitorStrings.Available, 格式化字节(hp.FreeBytes + hp.StandbyBytes))
        }

        Dim pad As Integer = CInt(Math.Round(6 * s))
        Dim gap As Single = 4 * s
        Dim lineH As Integer = CInt(Math.Ceiling(Me.Font.GetHeight(g)))
        ' 测量每行宽度（标签和值之间留一空格），取最大
        Dim maxLabel As Integer = 0, maxValue As Integer = 0
        Dim labelSizes(lines.Count - 1) As Size
        Dim valueSizes(lines.Count - 1) As Size
        For i As Integer = 0 To lines.Count - 1
            labelSizes(i) = TextRenderer.MeasureText(g, lines(i).label, Me.Font, New Size(Integer.MaxValue, lineH), TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
            valueSizes(i) = TextRenderer.MeasureText(g, lines(i).value, Me.Font, New Size(Integer.MaxValue, lineH), TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
            If labelSizes(i).Width > maxLabel Then maxLabel = labelSizes(i).Width
            If valueSizes(i).Width > maxValue Then maxValue = valueSizes(i).Width
        Next
        Dim innerGap As Integer = CInt(Math.Round(6 * s))
        Dim panelW As Integer = pad * 2 + maxLabel + innerGap + maxValue
        Dim panelH As Integer = pad * 2 + lineH * lines.Count

        ' 决定左右侧显示
        Dim showOnLeft As Boolean = (xAtSample - gap - panelW) >= graphRect.Left
        Dim panelX As Single
        If showOnLeft Then
            panelX = xAtSample - gap - panelW
        Else
            panelX = xAtSample + gap
            If panelX + panelW > graphRect.Right Then panelX = graphRect.Right - panelW
        End If
        If panelX < graphRect.Left Then panelX = graphRect.Left

        Dim panelY As Single = graphRect.Top + (graphRect.Height - panelH) / 2.0F
        If panelY < graphRect.Top Then panelY = graphRect.Top
        If panelY + panelH > graphRect.Bottom Then panelY = graphRect.Bottom - panelH

        Dim panelRect As New RectangleF(panelX, panelY, panelW, panelH)

        ' 背景
        If 悬停面板背景值.A > 0 Then
            Using b As New SolidBrush(悬停面板背景值)
                g.FillRectangle(b, panelRect)
            End Using
        End If
        If 悬停面板边框值.A > 0 Then
            Using p As New Pen(悬停面板边框值, 1)
                g.DrawRectangle(p, panelRect.X, panelRect.Y, panelRect.Width - 1, panelRect.Height - 1)
            End Using
        End If

        ' 文字：左侧显示→右对齐，标签在右、值在左；右侧显示→左对齐，标签在左、值在右
        Dim fore As Color = If(悬停面板前景值.A > 0, 悬停面板前景值, Me.ForeColor)
        For i As Integer = 0 To lines.Count - 1
            Dim rowY As Integer = CInt(panelRect.Y) + pad + i * lineH
            If showOnLeft Then
                ' value 在左，label 在右（右对齐到面板右边）
                Dim labelRect As New Rectangle(CInt(panelRect.Right) - pad - labelSizes(i).Width, rowY, labelSizes(i).Width, lineH)
                Dim valueRect As New Rectangle(labelRect.Left - innerGap - valueSizes(i).Width, rowY, valueSizes(i).Width, lineH)
                TextRenderer.DrawText(g, lines(i).value, Me.Font, valueRect, fore, Color.Transparent, TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.Left)
                TextRenderer.DrawText(g, lines(i).label, Me.Font, labelRect, fore, Color.Transparent, TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.Left)
            Else
                ' label 在左，value 在右（左对齐，值贴近面板右侧）
                Dim labelRect As New Rectangle(CInt(panelRect.X) + pad, rowY, labelSizes(i).Width, lineH)
                Dim valueRect As New Rectangle(labelRect.Right + innerGap, rowY, maxValue, lineH)
                TextRenderer.DrawText(g, lines(i).label, Me.Font, labelRect, fore, Color.Transparent, TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.Left)
                TextRenderer.DrawText(g, lines(i).value, Me.Font, valueRect, fore, Color.Transparent, TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.Left)
            End If
        Next
    End Sub

    Private Function 样本索引从X坐标(x As Single, rect As RectangleF) As Integer
        Dim cap As Integer = Math.Max(2, 记录长度值)
        Dim step_ As Single = rect.Width / CSng(cap - 1)
        If step_ <= 0 Then Return -1
        Dim n As Integer = _图表样本数
        If n < 1 Then Return -1
        ' 样本 i 的 X：right - (n-1-i)*step
        Dim idx As Integer = CInt(Math.Round((x - (rect.Right - CSng(n - 1) * step_)) / step_))
        If idx < 0 Then idx = 0
        If idx > n - 1 Then idx = n - 1
        Return idx
    End Function

    Private Sub 绘制背景与边框(g As Graphics, rect As RectangleF, s As Single)
        Dim r As Single = 圆角半径值 * s
        If 核心背景颜色值.A > 0 Then
            Using b As New SolidBrush(核心背景颜色值)
                If r > 0 Then
                    Using path = 构造圆角路径(rect, r)
                        g.FillPath(b, path)
                    End Using
                Else
                    g.FillRectangle(b, rect)
                End If
            End Using
        End If
    End Sub

    Private Function 应用圆角裁剪(g As Graphics, rect As RectangleF, s As Single) As Region
        If 圆角半径值 <= 0 Then Return Nothing
        Dim old As Region = g.Clip
        Using path = 构造圆角路径(rect, 圆角半径值 * s)
            g.SetClip(path, System.Drawing.Drawing2D.CombineMode.Intersect)
        End Using
        Return old
    End Function

    Private Shared Sub 恢复裁剪(g As Graphics, old As Region)
        If old Is Nothing Then Return
        g.Clip = old
        old.Dispose()
    End Sub

    Private Shared Function 构造圆角路径(rect As RectangleF, radius As Single) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim d As Single = radius * 2
        If d > rect.Width Then d = rect.Width
        If d > rect.Height Then d = rect.Height
        If d <= 0 Then
            path.AddRectangle(rect)
            Return path
        End If
        path.AddArc(rect.X, rect.Y, d, d, 180, 90)
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90)
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90)
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90)
        path.CloseFigure()
        Return path
    End Function

    Private Sub 绘制文字(g As Graphics, inner As RectangleF, text As String, s As Single, align As ContentAlignment)
        If String.IsNullOrEmpty(text) Then Return
        Dim tp As Padding = 文字内边距值
        Dim textRect As New Rectangle(
            CInt(Math.Round(inner.X + tp.Left * s)),
            CInt(Math.Round(inner.Y + tp.Top * s)),
            Math.Max(0, CInt(Math.Round(inner.Width - (tp.Left + tp.Right) * s))),
            Math.Max(0, CInt(Math.Round(inner.Height - (tp.Top + tp.Bottom) * s))))
        If textRect.Width <= 0 OrElse textRect.Height <= 0 Then Return
        TextRenderer.DrawText(g, text, Me.Font, textRect, Me.ForeColor, Color.Transparent,
                              对齐转标志(align) Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis)
    End Sub

    Private Shared Function 对齐转标志(a As ContentAlignment) As TextFormatFlags
        Dim h As TextFormatFlags = If(a = ContentAlignment.TopLeft OrElse a = ContentAlignment.MiddleLeft OrElse a = ContentAlignment.BottomLeft, TextFormatFlags.Left,
                                   If(a = ContentAlignment.TopRight OrElse a = ContentAlignment.MiddleRight OrElse a = ContentAlignment.BottomRight, TextFormatFlags.Right,
                                      TextFormatFlags.HorizontalCenter))
        Dim v As TextFormatFlags = If(a = ContentAlignment.TopLeft OrElse a = ContentAlignment.TopCenter OrElse a = ContentAlignment.TopRight, TextFormatFlags.Top,
                                   If(a = ContentAlignment.BottomLeft OrElse a = ContentAlignment.BottomCenter OrElse a = ContentAlignment.BottomRight, TextFormatFlags.Bottom,
                                      TextFormatFlags.VerticalCenter))
        Return h Or v
    End Function

    Private Function 构造槽位文字(slot As TextSlotPosition) As String
        Dim fields = 枚举槽位字段(slot)
        If fields.Count = 0 Then Return String.Empty
        Dim sb As New System.Text.StringBuilder()
        Dim sep As String = If(String.IsNullOrEmpty(字段分隔符值), "    ", 字段分隔符值)
        For Each f In fields
            Dim piece As String = 构造字段文本(f)
            If Not String.IsNullOrEmpty(piece) Then
                If sb.Length > 0 Then sb.Append(sep)
                sb.Append(piece)
            End If
        Next
        Return sb.ToString()
    End Function

    Private Function 枚举槽位字段(slot As TextSlotPosition) As List(Of RamTextField)
        Dim list As New List(Of (字段 As RamTextField, 顺序 As Integer))()
        For Each kv In 字段配置
            If kv.Value.Position = slot Then list.Add((kv.Key, kv.Value.Order))
        Next
        list.Sort(Function(x, y) x.顺序.CompareTo(y.顺序))
        Return list.ConvertAll(Function(t) t.字段)
    End Function

    Private Function 构造字段文本(f As RamTextField) As String
        Dim s = 最近样本
        Dim hw As RamSampler.HardwareInfo = If(采样器实例 IsNot Nothing, 采样器实例.Hardware, Nothing)
        Dim hasMem As Boolean = s.TotalBytes > 0UL
        Select Case f
            Case RamTextField.Total
                If Not hasMem Then Return String.Empty
                Return RamMonitorStrings.Total & " " & 格式化字节(s.TotalBytes)
            Case RamTextField.InUse
                If Not hasMem Then Return String.Empty
                Return RamMonitorStrings.InUse & " " & 格式化字节(s.InUseBytes)
            Case RamTextField.Modified
                If Not hasMem Then Return String.Empty
                Return RamMonitorStrings.Modified & " " & 格式化字节(s.ModifiedBytes)
            Case RamTextField.Standby
                If Not hasMem Then Return String.Empty
                Return RamMonitorStrings.Standby & " " & 格式化字节(s.StandbyBytes)
            Case RamTextField.Available
                If Not hasMem Then Return String.Empty
                Return RamMonitorStrings.Available & " " & 格式化字节(s.FreeBytes + s.StandbyBytes)
            Case RamTextField.Speed
                Dim sp As Integer = If(hw.ConfiguredSpeedMTs > 0, hw.ConfiguredSpeedMTs, hw.SpeedMTs)
                If sp <= 0 Then Return String.Empty
                Return RamMonitorStrings.Speed & " " & sp.ToString() & " " & RamMonitorStrings.UnitMTs
            Case RamTextField.Committed
                If s.CommitLimitBytes = 0UL Then Return String.Empty
                Return RamMonitorStrings.Committed & " " & 格式化字节(s.CommittedBytes) & " / " & 格式化字节(s.CommitLimitBytes)
            Case RamTextField.DdrType
                Return If(String.IsNullOrEmpty(hw.DdrType), String.Empty, hw.DdrType)
            Case RamTextField.SlotCount
                If hw.ModuleCount <= 0 Then Return String.Empty
                Return RamMonitorStrings.SlotCount & " " &
                       String.Format(RamMonitorStrings.SlotsFormat, hw.ModuleCount, Math.Max(hw.ModuleCount, 插槽总数值))
        End Select
        Return String.Empty
    End Function

    Private Shared Function 格式化字节(v As ULong) As String
        If v = 0UL Then Return "0 B"
        Dim d As Double = v
        Dim units() As String = {"B", "KB", "MB", "GB", "TB", "PB"}
        Dim i As Integer = 0
        Do While d >= 1024.0 AndAlso i < units.Length - 1
            d /= 1024.0
            i += 1
        Loop
        If d >= 100 Then
            Return d.ToString("0") & " " & units(i)
        ElseIf d >= 10 Then
            Return d.ToString("0.0") & " " & units(i)
        Else
            Return d.ToString("0.00") & " " & units(i)
        End If
    End Function
#End Region

#Region "通用"
    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            Me.Invalidate()
        End If
    End Sub

    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If Not 启用悬停读数值 Then Return
        Dim wasValid As Boolean = _悬停有效
        Dim prevIdx As Integer = _悬停样本索引
        _悬停X = e.X
        _悬停有效 = _图表矩形.Width > 0 AndAlso _图表矩形.Height > 0 AndAlso
                    e.X >= _图表矩形.Left AndAlso e.X <= _图表矩形.Right AndAlso
                    e.Y >= _图表矩形.Top AndAlso e.Y <= _图表矩形.Bottom
        Dim newIdx As Integer = If(_悬停有效, 样本索引从X坐标(_悬停X, _图表矩形), -1)
        _悬停样本索引 = newIdx
        ' 只在命中的样本索引变化或悬停有效性切换时才重绘，避免每像素移动都触发重绘
        If _悬停有效 <> wasValid OrElse newIdx <> prevIdx Then Me.Invalidate()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If _悬停有效 Then
            _悬停有效 = False
            _悬停样本索引 = -1
            Me.Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        If Not DesignMode Then
            确保采样器()
            If 正在运行 Then 采样定时器.Start()
        End If
    End Sub

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        采样定时器.Stop()
        采样定时器.Dispose()
        Try : _ssaaCache.Dispose() : Catch : End Try
        Try : _brushCache.Dispose() : Catch : End Try
        Try : _textFormatCache.Dispose() : Catch : End Try
        If _dcRT IsNot Nothing Then
            Try : _dcRT.Dispose() : Catch : End Try
            _dcRT = Nothing
        End If
        MyBase.OnHandleDestroyed(e)
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        Me.Invalidate()
    End Sub

    Protected Overrides Sub OnForeColorChanged(e As EventArgs)
        MyBase.OnForeColorChanged(e)
        Me.Invalidate()
    End Sub
#End Region

#Region "属性"
    ' ====== 运行控制 ======
    Private 正在运行 As Boolean = False
    <Category("LakeUI"), Description("是否启动采样；默认不工作，需手动设为 True 以开始监控。"), DefaultValue(False), Browsable(True)>
    Public Property Running As Boolean
        Get
            Return 正在运行
        End Get
        Set(value As Boolean)
            If value = 正在运行 Then Return
            正在运行 = value
            If value Then
                确保采样器()
                If Me.IsHandleCreated AndAlso Not DesignMode Then 采样定时器.Start()
            Else
                采样定时器.Stop()
            End If
            Me.Invalidate()
        End Set
    End Property

    Private 采样间隔值 As Integer = 1000
    <Category("LakeUI"), Description("采样间隔 (毫秒)，与任务管理器一致默认 1000。"), DefaultValue(1000), Browsable(True)>
    Public Property SampleInterval As Integer
        Get
            Return 采样间隔值
        End Get
        Set(value As Integer)
            value = Math.Max(50, value)
            If value = 采样间隔值 Then Return
            采样间隔值 = value
            采样定时器.Interval = value
        End Set
    End Property

    Private 当前语言值 As RamMonitorLanguage = RamMonitorLanguage.Chinese
    <Category("LakeUI"), Description("显示语言。选择 Custom 时，控件不会覆盖 RamMonitorStrings 的共享字段，由开发者自行对接。"), DefaultValue(GetType(RamMonitorLanguage), "Chinese"), Browsable(True)>
    Public Property Language As RamMonitorLanguage
        Get
            Return 当前语言值
        End Get
        Set(value As RamMonitorLanguage)
            当前语言值 = value
            Select Case value
                Case RamMonitorLanguage.Chinese : RamMonitorStrings.ApplyChinese()
                Case RamMonitorLanguage.English : RamMonitorStrings.ApplyEnglish()
            End Select
            Me.Invalidate()
        End Set
    End Property

    ' ====== 布局 ======
    Private 核心内边距值 As Single = 0
    <Category("LakeUI"), Description("控件内部内容距离边框的内边距。"), DefaultValue(0.0F), Browsable(True)>
    Public Property CellPadding As Single
        Get
            Return 核心内边距值
        End Get
        Set(value As Single)
            SetValue(核心内边距值, Math.Max(0, value))
        End Set
    End Property

    Private 圆角半径值 As Single = 0
    <Category("LakeUI"), Description("控件区域的圆角半径，0 = 直角。"), DefaultValue(0.0F), Browsable(True)>
    Public Property CellCornerRadius As Single
        Get
            Return 圆角半径值
        End Get
        Set(value As Single)
            SetValue(圆角半径值, Math.Max(0, value))
        End Set
    End Property

    ' ====== 颜色 - 容器 ======
    Private 核心背景颜色值 As Color = Color.FromArgb(48, 48, 48)
    <Category("LakeUI"), Description("控件区域背景颜色。"), DefaultValue(GetType(Color), "48, 48, 48"), Browsable(True)>
    Public Property CellBackColor As Color
        Get
            Return 核心背景颜色值
        End Get
        Set(value As Color)
            SetValue(核心背景颜色值, value)
        End Set
    End Property

    Private 禁用时遮罩颜色 As Color = Color.FromArgb(120, 0, 0, 0)
    <Category("LakeUI"), Description("禁用（Enabled = False）时覆盖在主体区域上的遮罩颜色（受圆角裁剪，不影响圆角外的透明区域）。"), DefaultValue(GetType(Color), "120, 0, 0, 0"), Browsable(True)>
    Public Property DisabledOverlayColor As Color
        Get
            Return 禁用时遮罩颜色
        End Get
        Set(value As Color)
            SetValue(禁用时遮罩颜色, value)
        End Set
    End Property

    ' ====== 文字 ======
    Private Sub 设置字段位置(f As RamTextField, value As TextSlotPosition)
        Dim cur = 字段配置(f)
        If cur.Position = value Then Return
        cur.Position = value
        字段配置(f) = cur
        Me.Invalidate()
    End Sub
    Private Sub 设置字段顺序(f As RamTextField, value As Integer)
        Dim cur = 字段配置(f)
        If cur.Order = value Then Return
        cur.Order = value
        字段配置(f) = cur
        Me.Invalidate()
    End Sub
    Private Function 取字段位置(f As RamTextField) As TextSlotPosition
        Return 字段配置(f).Position
    End Function
    Private Function 取字段顺序(f As RamTextField) As Integer
        Return 字段配置(f).Order
    End Function

    <Category("LakeUI 文字 - 总大小"), Description("『总』字段显示位置。"), DefaultValue(GetType(TextSlotPosition), "Top"), Browsable(True)>
    Public Property TotalPosition As TextSlotPosition
        Get
            Return 取字段位置(RamTextField.Total)
        End Get
        Set(value As TextSlotPosition)
            设置字段位置(RamTextField.Total, value)
        End Set
    End Property
    <Category("LakeUI 文字 - 总大小"), Description("『总』字段在槽内的排序（数值越小越靠前）。"), DefaultValue(0), Browsable(True)>
    Public Property TotalOrder As Integer
        Get
            Return 取字段顺序(RamTextField.Total)
        End Get
        Set(value As Integer)
            设置字段顺序(RamTextField.Total, value)
        End Set
    End Property

    <Category("LakeUI 文字 - 已用"), Description("『已用』字段显示位置。"), DefaultValue(GetType(TextSlotPosition), "Top"), Browsable(True)>
    Public Property InUsePosition As TextSlotPosition
        Get
            Return 取字段位置(RamTextField.InUse)
        End Get
        Set(value As TextSlotPosition)
            设置字段位置(RamTextField.InUse, value)
        End Set
    End Property
    <Category("LakeUI 文字 - 已用"), Description("『已用』字段顺序。"), DefaultValue(1), Browsable(True)>
    Public Property InUseOrder As Integer
        Get
            Return 取字段顺序(RamTextField.InUse)
        End Get
        Set(value As Integer)
            设置字段顺序(RamTextField.InUse, value)
        End Set
    End Property

    <Category("LakeUI 文字 - 已修改"), Description("『已修改』字段显示位置。"), DefaultValue(GetType(TextSlotPosition), "Top"), Browsable(True)>
    Public Property ModifiedPosition As TextSlotPosition
        Get
            Return 取字段位置(RamTextField.Modified)
        End Get
        Set(value As TextSlotPosition)
            设置字段位置(RamTextField.Modified, value)
        End Set
    End Property
    <Category("LakeUI 文字 - 已修改"), Description("『已修改』字段顺序。"), DefaultValue(2), Browsable(True)>
    Public Property ModifiedOrder As Integer
        Get
            Return 取字段顺序(RamTextField.Modified)
        End Get
        Set(value As Integer)
            设置字段顺序(RamTextField.Modified, value)
        End Set
    End Property

    <Category("LakeUI 文字 - 备用"), Description("『备用』字段显示位置。"), DefaultValue(GetType(TextSlotPosition), "Top"), Browsable(True)>
    Public Property StandbyPosition As TextSlotPosition
        Get
            Return 取字段位置(RamTextField.Standby)
        End Get
        Set(value As TextSlotPosition)
            设置字段位置(RamTextField.Standby, value)
        End Set
    End Property
    <Category("LakeUI 文字 - 备用"), Description("『备用』字段顺序。"), DefaultValue(3), Browsable(True)>
    Public Property StandbyOrder As Integer
        Get
            Return 取字段顺序(RamTextField.Standby)
        End Get
        Set(value As Integer)
            设置字段顺序(RamTextField.Standby, value)
        End Set
    End Property

    <Category("LakeUI 文字 - 可用"), Description("『可用』字段显示位置。"), DefaultValue(GetType(TextSlotPosition), "Top"), Browsable(True)>
    Public Property AvailablePosition As TextSlotPosition
        Get
            Return 取字段位置(RamTextField.Available)
        End Get
        Set(value As TextSlotPosition)
            设置字段位置(RamTextField.Available, value)
        End Set
    End Property
    <Category("LakeUI 文字 - 可用"), Description("『可用』字段顺序。"), DefaultValue(4), Browsable(True)>
    Public Property AvailableOrder As Integer
        Get
            Return 取字段顺序(RamTextField.Available)
        End Get
        Set(value As Integer)
            设置字段顺序(RamTextField.Available, value)
        End Set
    End Property

    <Category("LakeUI 文字 - 速度"), Description("『速度』字段显示位置。"), DefaultValue(GetType(TextSlotPosition), "Bottom"), Browsable(True)>
    Public Property SpeedPosition As TextSlotPosition
        Get
            Return 取字段位置(RamTextField.Speed)
        End Get
        Set(value As TextSlotPosition)
            设置字段位置(RamTextField.Speed, value)
        End Set
    End Property
    <Category("LakeUI 文字 - 速度"), Description("『速度』字段顺序。"), DefaultValue(0), Browsable(True)>
    Public Property SpeedOrder As Integer
        Get
            Return 取字段顺序(RamTextField.Speed)
        End Get
        Set(value As Integer)
            设置字段顺序(RamTextField.Speed, value)
        End Set
    End Property

    <Category("LakeUI 文字 - 已提交"), Description("『已提交』字段显示位置。"), DefaultValue(GetType(TextSlotPosition), "Bottom"), Browsable(True)>
    Public Property CommittedPosition As TextSlotPosition
        Get
            Return 取字段位置(RamTextField.Committed)
        End Get
        Set(value As TextSlotPosition)
            设置字段位置(RamTextField.Committed, value)
        End Set
    End Property
    <Category("LakeUI 文字 - 已提交"), Description("『已提交』字段顺序。"), DefaultValue(1), Browsable(True)>
    Public Property CommittedOrder As Integer
        Get
            Return 取字段顺序(RamTextField.Committed)
        End Get
        Set(value As Integer)
            设置字段顺序(RamTextField.Committed, value)
        End Set
    End Property

    <Category("LakeUI 文字 - 内存类型"), Description("『内存类型（DDR）』字段显示位置。"), DefaultValue(GetType(TextSlotPosition), "Bottom"), Browsable(True)>
    Public Property DdrTypePosition As TextSlotPosition
        Get
            Return 取字段位置(RamTextField.DdrType)
        End Get
        Set(value As TextSlotPosition)
            设置字段位置(RamTextField.DdrType, value)
        End Set
    End Property
    <Category("LakeUI 文字 - 内存类型"), Description("『内存类型』字段顺序。"), DefaultValue(2), Browsable(True)>
    Public Property DdrTypeOrder As Integer
        Get
            Return 取字段顺序(RamTextField.DdrType)
        End Get
        Set(value As Integer)
            设置字段顺序(RamTextField.DdrType, value)
        End Set
    End Property

    <Category("LakeUI 文字 - 插槽"), Description("『插槽使用情况』字段显示位置。"), DefaultValue(GetType(TextSlotPosition), "Bottom"), Browsable(True)>
    Public Property SlotCountPosition As TextSlotPosition
        Get
            Return 取字段位置(RamTextField.SlotCount)
        End Get
        Set(value As TextSlotPosition)
            设置字段位置(RamTextField.SlotCount, value)
        End Set
    End Property
    <Category("LakeUI 文字 - 插槽"), Description("『插槽』字段顺序。"), DefaultValue(3), Browsable(True)>
    Public Property SlotCountOrder As Integer
        Get
            Return 取字段顺序(RamTextField.SlotCount)
        End Get
        Set(value As Integer)
            设置字段顺序(RamTextField.SlotCount, value)
        End Set
    End Property

    Private 插槽总数值 As Integer = 0
    <Category("LakeUI 文字 - 插槽"), Description("主板物理内存插槽总数；0 表示使用探测到的已用模块数。"), DefaultValue(0), Browsable(True)>
    Public Property TotalSlotCount As Integer
        Get
            Return 插槽总数值
        End Get
        Set(value As Integer)
            SetValue(插槽总数值, Math.Max(0, value))
        End Set
    End Property

    Private 字段分隔符值 As String = "    "
    <Category("LakeUI"), Description("同一槽内字段之间的分隔符（默认 4 空格）。"), DefaultValue("    "), Browsable(True)>
    Public Property FieldSeparator As String
        Get
            Return 字段分隔符值
        End Get
        Set(value As String)
            SetValue(字段分隔符值, If(value, ""))
        End Set
    End Property

    Private 顶部文字对齐值 As ContentAlignment = ContentAlignment.MiddleLeft
    <Category("LakeUI"), Description("顶部文字对齐方式。"), DefaultValue(GetType(ContentAlignment), "MiddleLeft"), Browsable(True)>
    Public Property TopTextAlign As ContentAlignment
        Get
            Return 顶部文字对齐值
        End Get
        Set(value As ContentAlignment)
            SetValue(顶部文字对齐值, value)
        End Set
    End Property

    Private 底部文字对齐值 As ContentAlignment = ContentAlignment.MiddleLeft
    <Category("LakeUI"), Description("底部文字对齐方式。"), DefaultValue(GetType(ContentAlignment), "MiddleLeft"), Browsable(True)>
    Public Property BottomTextAlign As ContentAlignment
        Get
            Return 底部文字对齐值
        End Get
        Set(value As ContentAlignment)
            SetValue(底部文字对齐值, value)
        End Set
    End Property

    Private 文字内边距值 As New Padding(10)
    <Category("LakeUI"), Description("顶部与底部文字区域的内边距。"), Browsable(True)>
    Public Property TextPadding As Padding
        Get
            Return 文字内边距值
        End Get
        Set(value As Padding)
            SetValue(文字内边距值, value)
        End Set
    End Property

    Private Function ShouldSerializeTextPadding() As Boolean
        Return 文字内边距值 <> New Padding(10)
    End Function
    Private Sub ResetTextPadding()
        TextPadding = New Padding(10)
    End Sub

    ' ====== 历史图表 ======
    Private 启用历史记录 As Boolean = True
    <Category("LakeUI"), Description("是否启用历史记录采样。"), DefaultValue(True), Browsable(True)>
    Public Property EnableHistory As Boolean
        Get
            Return 启用历史记录
        End Get
        Set(value As Boolean)
            启用历史记录 = value
            Me.Invalidate()
        End Set
    End Property

    Private 记录长度值 As Integer = 60
    <Category("LakeUI"), Description("历史记录长度（样本数，默认 60 ≈ 1 分钟）。"), DefaultValue(60), Browsable(True)>
    Public Property HistoryLength As Integer
        Get
            Return 记录长度值
        End Get
        Set(value As Integer)
            value = Math.Max(2, value)
            If value = 记录长度值 Then Return
            记录长度值 = value
            SyncLock 历史锁
                Dim overflow As Integer = 历史数据.Count - value
                If overflow > 0 Then 历史数据.RemoveRange(0, overflow)
            End SyncLock
            Me.Invalidate()
        End Set
    End Property

    Private 图表背景颜色值 As Color = Color.FromArgb(24, 24, 24)
    <Category("LakeUI"), Description("历史图表背景色；Alpha=0 时不绘制。"), DefaultValue(GetType(Color), "24, 24, 24"), Browsable(True)>
    Public Property GraphBackColor As Color
        Get
            Return 图表背景颜色值
        End Get
        Set(value As Color)
            SetValue(图表背景颜色值, value)
        End Set
    End Property

    Private 图表线条颜色值 As Color = Color.FromArgb(0, 200, 255)
    <Category("LakeUI"), Description("顶部轮廓线颜色（表示 已用+已修改+备用 的总体内存占用）。"), DefaultValue(GetType(Color), "0, 200, 255"), Browsable(True)>
    Public Property GraphLineColor As Color
        Get
            Return 图表线条颜色值
        End Get
        Set(value As Color)
            SetValue(图表线条颜色值, value)
        End Set
    End Property

    Private 图表线条粗细值 As Single = 1
    <Category("LakeUI"), Description("顶部轮廓线粗细。"), DefaultValue(1.0F), Browsable(True)>
    Public Property GraphLineThickness As Single
        Get
            Return 图表线条粗细值
        End Get
        Set(value As Single)
            SetValue(图表线条粗细值, Math.Max(0.1F, value))
        End Set
    End Property

    Private 已用填充颜色值 As Color = Color.FromArgb(200, 0, 120, 215)
    <Category("LakeUI"), Description("已用（In Use）填充颜色。"), DefaultValue(GetType(Color), "200, 0, 120, 215"), Browsable(True)>
    Public Property InUseFillColor As Color
        Get
            Return 已用填充颜色值
        End Get
        Set(value As Color)
            SetValue(已用填充颜色值, value)
        End Set
    End Property

    Private 已修改填充颜色值 As Color = Color.FromArgb(180, 215, 120, 60)
    <Category("LakeUI"), Description("已修改（Modified）填充颜色。"), DefaultValue(GetType(Color), "180, 215, 120, 60"), Browsable(True)>
    Public Property ModifiedFillColor As Color
        Get
            Return 已修改填充颜色值
        End Get
        Set(value As Color)
            SetValue(已修改填充颜色值, value)
        End Set
    End Property

    Private 备用填充颜色值 As Color = Color.FromArgb(120, 0, 200, 255)
    <Category("LakeUI"), Description("备用（Standby）填充颜色。"), DefaultValue(GetType(Color), "120, 0, 200, 255"), Browsable(True)>
    Public Property StandbyFillColor As Color
        Get
            Return 备用填充颜色值
        End Get
        Set(value As Color)
            SetValue(备用填充颜色值, value)
        End Set
    End Property

    ' ====== 悬停读数 ======
    Private 启用悬停读数值 As Boolean = True
    <Category("LakeUI 悬停"), Description("是否启用鼠标悬停读数：在图表上显示垂直刻度线与当前样本的 已用/备用/可用 读数。"), DefaultValue(True), Browsable(True)>
    Public Property EnableHoverReadout As Boolean
        Get
            Return 启用悬停读数值
        End Get
        Set(value As Boolean)
            启用悬停读数值 = value
            If Not value Then _悬停有效 = False
            Me.Invalidate()
        End Set
    End Property

    Private 悬停线颜色值 As Color = Color.FromArgb(200, 255, 255, 255)
    <Category("LakeUI 悬停"), Description("悬停垂直刻度线颜色。"), DefaultValue(GetType(Color), "200, 255, 255, 255"), Browsable(True)>
    Public Property HoverLineColor As Color
        Get
            Return 悬停线颜色值
        End Get
        Set(value As Color)
            SetValue(悬停线颜色值, value)
        End Set
    End Property

    Private 悬停线粗细值 As Single = 1
    <Category("LakeUI 悬停"), Description("悬停垂直刻度线粗细。"), DefaultValue(1.0F), Browsable(True)>
    Public Property HoverLineThickness As Single
        Get
            Return 悬停线粗细值
        End Get
        Set(value As Single)
            SetValue(悬停线粗细值, Math.Max(0.1F, value))
        End Set
    End Property

    Private 悬停面板背景值 As Color = Color.FromArgb(220, 20, 20, 20)
    <Category("LakeUI 悬停"), Description("悬停读数面板背景色。"), DefaultValue(GetType(Color), "220, 20, 20, 20"), Browsable(True)>
    Public Property HoverPanelBackColor As Color
        Get
            Return 悬停面板背景值
        End Get
        Set(value As Color)
            SetValue(悬停面板背景值, value)
        End Set
    End Property

    Private 悬停面板边框值 As Color = Color.FromArgb(120, 255, 255, 255)
    <Category("LakeUI 悬停"), Description("悬停读数面板边框色。"), DefaultValue(GetType(Color), "120, 255, 255, 255"), Browsable(True)>
    Public Property HoverPanelBorderColor As Color
        Get
            Return 悬停面板边框值
        End Get
        Set(value As Color)
            SetValue(悬停面板边框值, value)
        End Set
    End Property

    Private 悬停面板前景值 As Color = Color.Silver
    <Category("LakeUI 悬停"), Description("悬停读数面板前景（文字）色。"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property HoverPanelForeColor As Color
        Get
            Return 悬停面板前景值
        End Get
        Set(value As Color)
            SetValue(悬停面板前景值, value)
        End Set
    End Property

    ' ====== SSAA ======
    Private 超采样倍率 As Integer = 1
    <Category("LakeUI"), Description(Class1.超采样抗锯齿描述词), DefaultValue(GetType(Class1.SuperSamplingScaleEnum), "OFF"), Browsable(True)>
    Public Property SuperSamplingScale As Class1.SuperSamplingScaleEnum
        Get
            Return 超采样倍率
        End Get
        Set(value As Class1.SuperSamplingScaleEnum)
            SetValue(超采样倍率, value)
        End Set
    End Property

    Private _backgroundSource As Control = Nothing
    ''' <summary>
    ''' 背景采样源（超容器背景映射）。透明背景模式下用于采样父级或指定控件作为底图。
    ''' </summary>
    <Category("LakeUI"),
     Description("背景采样源（超容器背景映射）。设置后将跨越任意层级直接采样此控件的绘制内容作为透明背景；为空时自动选择首个不透明祖先。"),
     DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource IsNot value Then
                _backgroundSource = value
                Me.Invalidate()
            End If
        End Set
    End Property
#End Region

#Region "禁用属性"
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property AutoScroll As Boolean
        Get
            Return Nothing
        End Get
        Set(value As Boolean)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property AutoSize As Boolean
        Get
            Return Nothing
        End Get
        Set(value As Boolean)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property BorderStyle As BorderStyle
        Get
            Return Nothing
        End Get
        Set(value As BorderStyle)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property BackgroundImage As Image
        Get
            Return Nothing
        End Get
        Set(value As Image)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property BackgroundImageLayout As ImageLayout
        Get
            Return Nothing
        End Get
        Set(value As ImageLayout)
        End Set
    End Property
#End Region

End Class
