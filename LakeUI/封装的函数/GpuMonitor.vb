Imports System.Globalization
Imports System.Runtime.InteropServices
Imports System.Text

''' <summary>
''' 任务管理器风格的 GPU 信息读取 —— 静态函数集合。<br/>
''' 数据源：<br/>
''' • <b>D3DKMT</b> (gdi32.dll)：枚举适配器、功耗百分比、显存/核心频率。<br/>
''' • <b>PDH</b> (GPU Engine / GPU Adapter Memory)：每引擎占用率、已用显存。<br/>
''' • <b>NVML</b> (nvml.dll，可选)：仅 NVIDIA 提供功耗、频率、进阶视频编解码占用。<br/>
''' AMD/Intel 由 PDH 引擎名匹配出视频编/解码占用；其余传感器依赖 D3DKMT。
''' 当驱动没实现对应 KMTQAITYPE 时该字段为 <c>Nothing</c>（AMD/Intel WDDM 驱动的常态）。
''' </summary>
Public NotInheritable Class GpuMonitor

    Private Sub New()
    End Sub

#Region "公开类型"
    ''' <summary>显卡厂商（按驱动描述字符串推断）。</summary>
    Public Enum GpuVendor
        Unknown
        NVIDIA
        AMD
        Intel
    End Enum

    ''' <summary>单张显卡的一次采样快照。所有可空字段：驱动未报告时为 <c>Nothing</c>。</summary>
    Public NotInheritable Class GpuInfo
        ' --- 标识 ---
        Public Property Index As Integer
        Public Property Name As String
        Public Property Vendor As GpuVendor
        Public Property LuidLow As Integer
        Public Property LuidHigh As Integer

        ' --- 显存容量（采样间不变） ---
        Public Property DedicatedVideoMemoryBytes As ULong
        Public Property DedicatedSystemMemoryBytes As ULong
        Public Property SharedSystemMemoryBytes As ULong

        ' --- 引擎占用 / 显存占用 ---
        ''' <summary>各引擎节点的占用率（键为引擎类型名）；值范围 0.0 ~ 1.0。</summary>
        Public ReadOnly Property EngineUsages As New Dictionary(Of String, Single)(StringComparer.OrdinalIgnoreCase)
        ''' <summary>整卡总占用率：所有引擎占用率的最大值；范围 0.0 ~ 1.0。</summary>
        Public Property OverallUsage As Single
        Public Property DedicatedMemoryUsedBytes As ULong
        Public Property SharedMemoryUsedBytes As ULong

        ' --- 通用传感器 ---
        ''' <summary>当前功耗，瓦 (W)；NVML 提供。</summary>
        Public Property PowerWatts As Single?
        ''' <summary>功耗上限，瓦 (W)；NVML 提供。</summary>
        Public Property PowerLimitWatts As Single?
        ''' <summary>当前功耗占 TDP 的百分比 (0~100)。</summary>
        Public Property PowerPercent As Single?

        ' --- 频率（赫兹 Hz） ---
        Public Property CoreFrequencyHz As ULong?
        Public Property MaxCoreFrequencyHz As ULong?
        Public Property MemoryFrequencyHz As ULong?
        Public Property MaxMemoryFrequencyHz As ULong?

        ' --- 视频编解码（NVML 提供主路径，AMD/Intel 由 PDH 引擎名映射） ---
        Public Property VideoEncoderUsage As Single?
        Public Property VideoDecoderUsage As Single?

        ' --- 版本字符串（NVML 提供） ---
        Public Property VBiosVersion As String
        Public Property DriverVersion As String

        ''' <summary>PDH 实例匹配用：小写形如 "0xHHHHHHHH_0xLLLLLLLL"。</summary>
        Friend ReadOnly Property LuidKey As String
            Get
                Return String.Format(CultureInfo.InvariantCulture, "0x{0:x8}_0x{1:x8}", 转无符号32位位模式(LuidHigh), 转无符号32位位模式(LuidLow))
            End Get
        End Property

        ''' <summary>把 <paramref name="src"/> 全部字段深拷贝到当前实例。</summary>
        Friend Sub CopyFrom(src As GpuInfo)
            Index = src.Index : Name = src.Name : Vendor = src.Vendor
            LuidHigh = src.LuidHigh : LuidLow = src.LuidLow
            DedicatedVideoMemoryBytes = src.DedicatedVideoMemoryBytes
            DedicatedSystemMemoryBytes = src.DedicatedSystemMemoryBytes
            SharedSystemMemoryBytes = src.SharedSystemMemoryBytes
            DedicatedMemoryUsedBytes = src.DedicatedMemoryUsedBytes
            SharedMemoryUsedBytes = src.SharedMemoryUsedBytes
            OverallUsage = src.OverallUsage
            PowerWatts = src.PowerWatts
            PowerLimitWatts = src.PowerLimitWatts
            PowerPercent = src.PowerPercent
            CoreFrequencyHz = src.CoreFrequencyHz
            MaxCoreFrequencyHz = src.MaxCoreFrequencyHz
            MemoryFrequencyHz = src.MemoryFrequencyHz
            MaxMemoryFrequencyHz = src.MaxMemoryFrequencyHz
            VideoEncoderUsage = src.VideoEncoderUsage
            VideoDecoderUsage = src.VideoDecoderUsage
            VBiosVersion = src.VBiosVersion
            DriverVersion = src.DriverVersion
            EngineUsages.Clear()
            For Each kv In src.EngineUsages
                EngineUsages(kv.Key) = kv.Value
            Next
        End Sub
    End Class
#End Region

#Region "D3DKMT P/Invoke + 结构"
    ' KMTQUERYADAPTERINFOTYPE 取值（d3dkmthk.h）。Win10 1709 以前对传感器类未实现，会返回 STATUS_INVALID_PARAMETER。
    Private Const KMTQAITYPE_GETSEGMENTSIZE As Integer = 3
    Private Const KMTQAITYPE_ADAPTERREGISTRYINFO As Integer = 8
    Private Const KMTQAITYPE_NODEMETADATA As Integer = 32
    Private Const KMTQAITYPE_DRIVER_DESCRIPTION As Integer = 63
    Private Const KMTQAITYPE_NODEPERFDATA As Integer = 64
    Private Const KMTQAITYPE_ADAPTERPERFDATA As Integer = 65
    Private Const DXGK_ENGINE_TYPE_3D As UInteger = 1
    Private Const MAX_PATH As Integer = 260

    <StructLayout(LayoutKind.Sequential)>
    Private Structure LUID_NATIVE
        Public LowPart As UInteger
        Public HighPart As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure D3DKMT_ADAPTERINFO
        Public hAdapter As UInteger
        Public AdapterLuid As LUID_NATIVE
        Public NumOfSources As UInteger
        Public bPrecisePresentRegionsPreferred As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure D3DKMT_ENUMADAPTERS2
        Public NumAdapters As UInteger
        Public pAdapters As IntPtr
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure D3DKMT_CLOSEADAPTER
        Public hAdapter As UInteger
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure D3DKMT_QUERYADAPTERINFO
        Public hAdapter As UInteger
        Public Type As Integer
        Public pPrivateDriverData As IntPtr
        Public PrivateDriverDataSize As UInteger
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure D3DKMT_SEGMENTSIZEINFO
        Public DedicatedVideoMemorySize As ULong
        Public DedicatedSystemMemorySize As ULong
        Public SharedSystemMemorySize As ULong
    End Structure

    ''' <summary>Temperature 单位为 deci-℃；Power 单位为 TDP 百分比 × 10。</summary>
    <StructLayout(LayoutKind.Sequential, Pack:=8)>
    Private Structure D3DKMT_ADAPTER_PERFDATA
        Public PhysicalAdapterIndex As UInteger
        Public MemoryFrequency As ULong
        Public MaxMemoryFrequency As ULong
        Public MaxMemoryFrequencyOC As ULong
        Public MemoryBandwidth As ULong
        Public PCIEBandwidth As ULong
        Public FanRPM As UInteger
        Public Power As UInteger
        Public Temperature As UInteger
        Public PowerStateOverride As Byte
    End Structure

    <StructLayout(LayoutKind.Sequential, Pack:=8)>
    Private Structure D3DKMT_ADAPTER_PERFDATACAPS
        Public PhysicalAdapterIndex As UInteger
        Public MaxMemoryBandwidth As ULong
        Public MaxPCIEBandwidth As ULong
        Public MaxFanRPM As UInteger
        Public TemperatureMax As UInteger
        Public TemperatureWarning As UInteger
    End Structure

    ''' <summary>NodeOrdinalAndAdapterIndex：低 16 = NodeOrdinal，高 16 = PhysicalAdapterIndex。</summary>
    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
    Private Structure D3DKMT_NODEMETADATA
        Public NodeOrdinalAndAdapterIndex As UInteger
        Public EngineType As UInteger
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=32)>
        Public FriendlyName As String
        Public Flags As UInteger
        Public GpuMmuSupported As Byte
        Public IoMmuSupported As Byte
    End Structure

    <StructLayout(LayoutKind.Sequential, Pack:=8)>
    Private Structure D3DKMT_NODE_PERFDATA
        Public NodeOrdinal As UInteger
        Public PhysicalAdapterIndex As UInteger
        Public Frequency As ULong
        Public MaxFrequency As ULong
        Public MaxFrequencyOC As ULong
        Public Voltage As UInteger
        Public VoltageMax As UInteger
        Public VoltageMaxOC As UInteger
        Public MaxTransitionLatency As ULong
    End Structure

    <DllImport("gdi32.dll")>
    Private Shared Function D3DKMTEnumAdapters2(ByRef p As D3DKMT_ENUMADAPTERS2) As Integer
    End Function

    <DllImport("gdi32.dll")>
    Private Shared Function D3DKMTQueryAdapterInfo(ByRef p As D3DKMT_QUERYADAPTERINFO) As Integer
    End Function

    <DllImport("gdi32.dll")>
    Private Shared Function D3DKMTCloseAdapter(ByRef p As D3DKMT_CLOSEADAPTER) As Integer
    End Function
#End Region

#Region "PDH P/Invoke"
    Private Const PDH_FMT_DOUBLE As UInteger = &H200UI
    Private Const PDH_FMT_LARGE As UInteger = &H400UI
    Private Const ERROR_SUCCESS As UInteger = 0UI
    Private Const PDH_MORE_DATA As UInteger = &H800007D2UI

    <StructLayout(LayoutKind.Explicit, Size:=16)>
    Private Structure PDH_FMT_COUNTERVALUE
        <FieldOffset(0)> Public CStatus As Integer
        <FieldOffset(8)> Public doubleValue As Double
        <FieldOffset(8)> Public largeValue As Long
    End Structure

    <DllImport("pdh.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function PdhOpenQueryW(src As String, userData As IntPtr, ByRef hQuery As IntPtr) As UInteger
    End Function

    <DllImport("pdh.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function PdhAddEnglishCounterW(hQuery As IntPtr, path As String, userData As IntPtr, ByRef hCounter As IntPtr) As UInteger
    End Function

    <DllImport("pdh.dll")>
    Private Shared Function PdhCollectQueryData(hQuery As IntPtr) As UInteger
    End Function

    <DllImport("pdh.dll")>
    Private Shared Function PdhGetFormattedCounterValue(hCounter As IntPtr, fmt As UInteger, ByRef typeOut As UInteger, ByRef value As PDH_FMT_COUNTERVALUE) As UInteger
    End Function

    <DllImport("pdh.dll")>
    Private Shared Function PdhCloseQuery(hQuery As IntPtr) As UInteger
    End Function

    <DllImport("pdh.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function PdhEnumObjectItemsW(src As String, machine As String, obj As String,
                                                counters As IntPtr, ByRef counterLen As UInteger,
                                                instances As IntPtr, ByRef instanceLen As UInteger,
                                                detailLevel As UInteger, flags As UInteger) As UInteger
    End Function
#End Region

#Region "内部状态"
    Private NotInheritable Class AdapterEntry
        Public Info As GpuInfo
        Public hAdapter As UInteger
        Public ThreeDNodeOrdinal As UInteger?
        Public NvmlDevice As IntPtr
    End Class

    Private NotInheritable Class EngineCounter
        Public LuidKey As String
        Public EngType As String
        Public Handle As IntPtr
    End Class

    Private Shared ReadOnly 状态锁 As New Object()
    Private Shared 适配器 As List(Of AdapterEntry)
    Private Shared 引擎计数器 As List(Of EngineCounter)
    Private Shared 显存专用计数器 As Dictionary(Of String, IntPtr)
    Private Shared 显存共享计数器 As Dictionary(Of String, IntPtr)
    Private Shared 查询句柄 As IntPtr = IntPtr.Zero
    Private Shared 已初始化 As Boolean
    Private Shared 首次采样 As Boolean = True

    ''' <summary>最近一次采样中每种 KMTQAITYPE 查询的 NTSTATUS（0 = 成功）。键形如 "hAdapter=0x...,type=N"。</summary>
    Public Shared ReadOnly Property LastQueryStatuses As New Dictionary(Of String, Integer)
#End Region

#Region "公开 API"
    ''' <summary>执行一次采样并返回所有显卡的快照（线程安全）。首次调用会自动初始化。</summary>
    Public Shared Function Sample() As GpuInfo()
        SyncLock 状态锁
            确保初始化()
            If 适配器 Is Nothing OrElse 适配器.Count = 0 Then Return Array.Empty(Of GpuInfo)()

            ' PDH 首次采样需间隔 10ms 跑两轮，差分计数才有值
            If 查询句柄 <> IntPtr.Zero Then
                Dim unused = PdhCollectQueryData(查询句柄)
                If 首次采样 Then
                    Threading.Thread.Sleep(10)
                    Dim unused1 = PdhCollectQueryData(查询句柄)
                    首次采样 = False
                End If
            End If

            Dim engAgg = 聚合引擎占用()
            Dim result(适配器.Count - 1) As GpuInfo
            For i = 0 To 适配器.Count - 1
                Dim ad = 适配器(i)
                Dim snap As New GpuInfo()
                snap.CopyFrom(ad.Info)                                ' 静态字段
                填充PDH占用(ad, snap, engAgg)                          ' 引擎/整体/显存
                填充D3DKMT传感器(ad, snap)                             ' AdapterPerfData + NodePerfData
                If ad.NvmlDevice <> IntPtr.Zero Then NvmlInterop.填充动态信息(ad.NvmlDevice, snap)
                按厂商填充视频编解码(snap)                              ' 由 PDH 引擎名或 NVML 写入编/解码占用
                ad.Info.CopyFrom(snap)                                 ' 缓存最新动态值，供 GetGpus 读
                result(i) = snap
            Next
            Return result
        End SyncLock
    End Function

    ''' <summary>最近一次 <see cref="Sample"/> 的快照（不会重新采样）。</summary>
    Public Shared Function GetGpus() As GpuInfo()
        SyncLock 状态锁
            确保初始化()
            If 适配器 Is Nothing Then Return Array.Empty(Of GpuInfo)()
            Return 适配器.Select(Function(a)
                                  Dim c As New GpuInfo()
                                  c.CopyFrom(a.Info)
                                  Return c
                              End Function).ToArray()
        End SyncLock
    End Function

    ''' <summary>已发现的显卡数量。首次访问会触发初始化。</summary>
    Public Shared ReadOnly Property GpuCount As Integer
        Get
            SyncLock 状态锁
                确保初始化()
                Return If(适配器?.Count, 0)
            End SyncLock
        End Get
    End Property

    ''' <summary>强制重新枚举适配器与计数器（适用于显卡热插拔、驱动重载后）。</summary>
    Public Shared Sub Refresh()
        SyncLock 状态锁
            释放() : 确保初始化()
        End SyncLock
    End Sub

    ''' <summary>关闭 PDH 查询与 D3DKMT 适配器句柄，释放所有资源。</summary>
    Public Shared Sub Shutdown()
        SyncLock 状态锁
            释放()
        End SyncLock
    End Sub
#End Region

#Region "初始化 / 枚举 / 释放"
    Private Shared Sub 确保初始化()
        If 已初始化 Then Return
        已初始化 = True
        Try
            枚举适配器()
            建立PDH查询()
        Catch
            释放()
        End Try
    End Sub

    Private Shared Sub 枚举适配器()
        适配器 = New List(Of AdapterEntry)

        ' D3DKMTEnumAdapters2 不支持 NULL 探询长度；32 足以覆盖任何主板
        Const MAX_ADAPTERS As Integer = 32
        Dim itemSize = Marshal.SizeOf(Of D3DKMT_ADAPTERINFO)()
        Dim buf = 分配清零内存(MAX_ADAPTERS * itemSize)
        Try
            Dim req As New D3DKMT_ENUMADAPTERS2 With {.NumAdapters = CUInt(MAX_ADAPTERS), .pAdapters = buf}
            If D3DKMTEnumAdapters2(req) <> 0 Then Return

            For i = 0 To CInt(req.NumAdapters) - 1
                Dim ai = Marshal.PtrToStructure(Of D3DKMT_ADAPTERINFO)(IntPtr.Add(buf, i * itemSize))
                If ai.hAdapter = 0UI Then Continue For

                Dim desc = 查询字符串字段(ai.hAdapter, KMTQAITYPE_DRIVER_DESCRIPTION, MAX_PATH * 2)
                If String.IsNullOrWhiteSpace(desc) Then desc = 查询字符串字段(ai.hAdapter, KMTQAITYPE_ADAPTERREGISTRYINFO, 4 * MAX_PATH * 2)
                If String.IsNullOrWhiteSpace(desc) Then
                    ' Basic Render Driver / 虚拟显示适配器：仍需 CloseAdapter 防 GDI 句柄泄露
                    关闭适配器句柄(ai.hAdapter)
                    Continue For
                End If

                Dim info As New GpuInfo With {
                    .Index = 适配器.Count,
                    .LuidLow = 转有符号32位位模式(ai.AdapterLuid.LowPart),
                    .LuidHigh = ai.AdapterLuid.HighPart,
                    .Name = desc,
                    .Vendor = 识别厂商(desc)
                }

                Dim seg As D3DKMT_SEGMENTSIZEINFO = Nothing
                If 查询结构(ai.hAdapter, KMTQAITYPE_GETSEGMENTSIZE, seg) Then
                    info.DedicatedVideoMemoryBytes = seg.DedicatedVideoMemorySize
                    info.DedicatedSystemMemoryBytes = seg.DedicatedSystemMemorySize
                    info.SharedSystemMemoryBytes = seg.SharedSystemMemorySize
                End If

                适配器.Add(New AdapterEntry With {
                    .Info = info,
                    .hAdapter = ai.hAdapter,
                    .ThreeDNodeOrdinal = 查找3D节点(ai.hAdapter)
                })
            Next
        Finally
            Marshal.FreeHGlobal(buf)
        End Try

        ' NVML：按 LUID 顺序绑定 NVIDIA 设备到对应适配器
        NvmlInterop.确保初始化()
        If NvmlInterop.可用 Then
            For Each ad In 适配器
                Dim dev = NvmlInterop.分配设备(转无符号32位位模式(ad.Info.LuidLow), ad.Info.LuidHigh)
                If dev <> IntPtr.Zero Then
                    ad.NvmlDevice = dev
                    NvmlInterop.填充静态信息(dev, ad.Info)
                End If
            Next
        End If
    End Sub

    Private Shared Sub 建立PDH查询()
        引擎计数器 = New List(Of EngineCounter)
        显存专用计数器 = New Dictionary(Of String, IntPtr)(StringComparer.OrdinalIgnoreCase)
        显存共享计数器 = New Dictionary(Of String, IntPtr)(StringComparer.OrdinalIgnoreCase)

        If PdhOpenQueryW(Nothing, IntPtr.Zero, 查询句柄) <> ERROR_SUCCESS Then
            查询句柄 = IntPtr.Zero
            Return
        End If

        ' 仅添加当前已知 LUID 对应的实例，跳过已禁用适配器
        Dim luidSet As New HashSet(Of String)(适配器.Select(Function(a) a.Info.LuidKey), StringComparer.OrdinalIgnoreCase)

        For Each inst In PdhEnumInstances("GPU Engine")
            Dim k = 提取LuidKey(inst)
            If k Is Nothing OrElse Not luidSet.Contains(k) Then Continue For
            Dim engType = 提取EngType(inst)
            If engType Is Nothing Then Continue For
            Dim h As IntPtr
            If PdhAddEnglishCounterW(查询句柄, "\GPU Engine(" & inst & ")\Utilization Percentage", IntPtr.Zero, h) = ERROR_SUCCESS Then
                引擎计数器.Add(New EngineCounter With {.LuidKey = k, .EngType = engType, .Handle = h})
            End If
        Next

        For Each inst In PdhEnumInstances("GPU Adapter Memory")
            Dim k = 提取LuidKey(inst)
            If k Is Nothing OrElse Not luidSet.Contains(k) Then Continue For
            Dim h1, h2 As IntPtr
            If PdhAddEnglishCounterW(查询句柄, "\GPU Adapter Memory(" & inst & ")\Dedicated Usage", IntPtr.Zero, h1) = ERROR_SUCCESS Then 显存专用计数器(k) = h1
            If PdhAddEnglishCounterW(查询句柄, "\GPU Adapter Memory(" & inst & ")\Shared Usage", IntPtr.Zero, h2) = ERROR_SUCCESS Then 显存共享计数器(k) = h2
        Next

        首次采样 = True
    End Sub

    Private Shared Sub 释放()
        If 查询句柄 <> IntPtr.Zero Then
            Try : Dim unused = PdhCloseQuery(查询句柄) : Catch : End Try
            查询句柄 = IntPtr.Zero
        End If
        If 适配器 IsNot Nothing Then
            For Each a In 适配器 : 关闭适配器句柄(a.hAdapter) : Next
        End If
        引擎计数器 = Nothing
        显存专用计数器 = Nothing
        显存共享计数器 = Nothing
        适配器 = Nothing
        已初始化 = False
        首次采样 = True
        NvmlInterop.关闭()
    End Sub

    Private Shared Sub 关闭适配器句柄(hAdapter As UInteger)
        If hAdapter = 0UI Then Return
        Dim c As New D3DKMT_CLOSEADAPTER With {.hAdapter = hAdapter}
        Try : Dim unused = D3DKMTCloseAdapter(c) : Catch : End Try
    End Sub
#End Region

#Region "采样辅助"
    Private Shared Function 聚合引擎占用() As Dictionary(Of String, Dictionary(Of String, Single))
        Dim agg As New Dictionary(Of String, Dictionary(Of String, Single))(StringComparer.OrdinalIgnoreCase)
        If 引擎计数器 Is Nothing Then Return agg
        For Each ec In 引擎计数器
            Dim v = 读取Double计数器(ec.Handle)
            If Double.IsNaN(v) Then Continue For
            Dim dict As Dictionary(Of String, Single) = Nothing
            If Not agg.TryGetValue(ec.LuidKey, dict) Then
                dict = New Dictionary(Of String, Single)(StringComparer.OrdinalIgnoreCase)
                agg(ec.LuidKey) = dict
            End If
            Dim cur As Single = 0
            dict.TryGetValue(ec.EngType, cur)
            dict(ec.EngType) = cur + CSng(v)
        Next
        Return agg
    End Function

    Private Shared Sub 填充PDH占用(ad As AdapterEntry, snap As GpuInfo, agg As Dictionary(Of String, Dictionary(Of String, Single)))
        snap.EngineUsages.Clear()
        snap.OverallUsage = 0F
        Dim dict As Dictionary(Of String, Single) = Nothing
        If agg.TryGetValue(ad.Info.LuidKey, dict) Then
            For Each kv In dict
                Dim v = Math.Min(1.0F, Math.Max(0F, kv.Value / 100.0F))
                snap.EngineUsages(kv.Key) = v
                If v > snap.OverallUsage Then snap.OverallUsage = v
            Next
        End If

        snap.DedicatedMemoryUsedBytes = 0UL
        snap.SharedMemoryUsedBytes = 0UL
        Dim h As IntPtr
        If 显存专用计数器.TryGetValue(ad.Info.LuidKey, h) Then snap.DedicatedMemoryUsedBytes = 读取Large计数器(h)
        If 显存共享计数器.TryGetValue(ad.Info.LuidKey, h) Then snap.SharedMemoryUsedBytes = 读取Large计数器(h)
    End Sub

    Private Shared Sub 填充D3DKMT传感器(ad As AdapterEntry, snap As GpuInfo)
        Dim pd As New D3DKMT_ADAPTER_PERFDATA()
        If 查询结构(ad.hAdapter, KMTQAITYPE_ADAPTERPERFDATA, pd) Then
            If pd.Power > 0UI Then snap.PowerPercent = pd.Power / 10.0F
            If pd.MemoryFrequency > 0UL Then snap.MemoryFrequencyHz = pd.MemoryFrequency
            If pd.MaxMemoryFrequency > 0UL Then snap.MaxMemoryFrequencyHz = pd.MaxMemoryFrequency
        End If

        If ad.ThreeDNodeOrdinal.HasValue Then
            Dim npd As New D3DKMT_NODE_PERFDATA With {.NodeOrdinal = ad.ThreeDNodeOrdinal.Value}
            If 查询结构(ad.hAdapter, KMTQAITYPE_NODEPERFDATA, npd) Then
                If npd.Frequency > 0UL Then snap.CoreFrequencyHz = npd.Frequency
                If npd.MaxFrequency > 0UL Then snap.MaxCoreFrequencyHz = npd.MaxFrequency
            End If
        End If
    End Sub

    ''' <summary>由驱动描述字符串推断厂商。</summary>
    Private Shared Function 识别厂商(name As String) As GpuVendor
        If String.IsNullOrEmpty(name) Then Return GpuVendor.Unknown
        Dim n = name.ToLowerInvariant()
        If n.Contains("nvidia") OrElse n.Contains("geforce") OrElse n.Contains("quadro") OrElse
           n.Contains("tesla") OrElse n.Contains("rtx") OrElse n.Contains("gtx") OrElse
           n.Contains("nvs ") OrElse n.Contains("titan") Then Return GpuVendor.NVIDIA
        If n.Contains("amd") OrElse n.Contains("radeon") OrElse n.StartsWith("ati") OrElse
           n.Contains("vega") OrElse n.Contains("firepro") OrElse n.Contains("instinct") Then Return GpuVendor.AMD
        If n.Contains("intel") OrElse n.Contains(" arc ") OrElse n.EndsWith(" arc") OrElse
           n.Contains("uhd graphics") OrElse n.Contains("hd graphics") OrElse n.Contains("iris") Then Return GpuVendor.Intel
        Return GpuVendor.Unknown
    End Function

    ''' <summary>
    ''' 双向同步独立编/解码字段与 <see cref="GpuInfo.EngineUsages"/> 字典。<br/>
    ''' 1. 若 NVML 已填写独立字段 → 写回字典对应键（覆盖 PDH 的旧值）。<br/>
    ''' 2. 否则从字典按厂商键名克隆到独立字段。<br/>
    ''' • NVIDIA：编码 = <c>VideoEncode</c>，解码 = <c>VideoDecode</c>。<br/>
    ''' • AMD：编码 = 解码 = <c>Video Codec*</c>（编解码共用同一引擎）。<br/>
    ''' • Intel：编码 = <c>VideoProcessing</c>，解码 = <c>VideoDecode</c>。
    ''' </summary>
    Private Shared Sub 按厂商填充视频编解码(snap As GpuInfo)
        Select Case snap.Vendor
            Case GpuVendor.NVIDIA
                同步编解码(snap, "VideoEncode", "VideoDecode")

            Case GpuVendor.AMD
                ' AMD VCN：编解码共用 "Video Codec 0" / "Video Codec 1" 等
                Dim codecKey = 查找引擎键前缀(snap, "Video Codec")
                同步编解码(snap, codecKey, codecKey)

            Case GpuVendor.Intel
                同步编解码(snap, "VideoProcessing", "VideoDecode")

            Case Else
                同步编解码(snap, "VideoEncode", "VideoDecode")
        End Select
    End Sub

    ''' <summary>双向：独立字段有值则写回字典，否则从字典读到独立字段。</summary>
    Private Shared Sub 同步编解码(snap As GpuInfo, encKey As String, decKey As String)
        ' 编码
        If snap.VideoEncoderUsage.HasValue Then
            If encKey IsNot Nothing Then snap.EngineUsages(encKey) = snap.VideoEncoderUsage.Value
        ElseIf encKey IsNot Nothing Then
            snap.VideoEncoderUsage = 查找引擎值(snap, encKey)
        End If
        ' 解码
        If snap.VideoDecoderUsage.HasValue Then
            If decKey IsNot Nothing Then snap.EngineUsages(decKey) = snap.VideoDecoderUsage.Value
        ElseIf decKey IsNot Nothing Then
            snap.VideoDecoderUsage = 查找引擎值(snap, decKey)
        End If
    End Sub

    ''' <summary>精确匹配键（不区分大小写）。</summary>
    Private Shared Function 查找引擎值(snap As GpuInfo, key As String) As Single?
        Dim v As Single
        If snap.EngineUsages.TryGetValue(key, v) Then Return v
        Return Nothing
    End Function

    ''' <summary>前缀匹配（不区分大小写），返回值最大的那个键名。用于 AMD "Video Codec 0" 等。</summary>
    Private Shared Function 查找引擎键前缀(snap As GpuInfo, prefix As String) As String
        Dim max As Single = -1.0F
        Dim bestKey As String = Nothing
        For Each kv In snap.EngineUsages
            If kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
                If kv.Value > max Then max = kv.Value : bestKey = kv.Key
            End If
        Next
        Return bestKey
    End Function
#End Region

#Region "D3DKMT 通用查询"
    ''' <summary>泛型 D3DKMTQueryAdapterInfo 包装：按值结构进出，成功返回 True，并把 NTSTATUS 写入诊断字典。</summary>
    Private Shared Function 查询结构(Of T As Structure)(hAdapter As UInteger, type As Integer, ByRef data As T) As Boolean
        Dim size = Marshal.SizeOf(Of T)()
        Dim p = Marshal.AllocHGlobal(size)
        Try
            Marshal.StructureToPtr(data, p, False)
            Dim q As New D3DKMT_QUERYADAPTERINFO With {
                .hAdapter = hAdapter, .Type = type,
                .pPrivateDriverData = p, .PrivateDriverDataSize = CUInt(size)
            }
            Dim status = D3DKMTQueryAdapterInfo(q)
            LastQueryStatuses(String.Format(CultureInfo.InvariantCulture, "hAdapter=0x{0:x8},type={1}", hAdapter, type)) = status
            If status <> 0 Then Return False
            data = Marshal.PtrToStructure(Of T)(p)
            Return True
        Catch
            Return False
        Finally
            Try : Marshal.DestroyStructure(Of T)(p) : Catch : End Try
            Marshal.FreeHGlobal(p)
        End Try
    End Function

    ''' <summary>查询返回 Unicode 字符串的 KMTQAITYPE 类（DRIVER_DESCRIPTION / ADAPTERREGISTRYINFO 等）。</summary>
    Private Shared Function 查询字符串字段(hAdapter As UInteger, type As Integer, byteSize As Integer) As String
        Dim p = 分配清零内存(byteSize)
        Try
            Dim q As New D3DKMT_QUERYADAPTERINFO With {
                .hAdapter = hAdapter, .Type = type,
                .pPrivateDriverData = p, .PrivateDriverDataSize = CUInt(byteSize)
            }
            If D3DKMTQueryAdapterInfo(q) <> 0 Then Return Nothing
            Return Marshal.PtrToStringUni(p)
        Finally
            Marshal.FreeHGlobal(p)
        End Try
    End Function

    Private Shared Function 查找3D节点(hAdapter As UInteger) As UInteger?
        For ordinal As UInteger = 0UI To 31UI
            Dim meta As New D3DKMT_NODEMETADATA With {
                .NodeOrdinalAndAdapterIndex = ordinal,
                .FriendlyName = String.Empty
            }
            If Not 查询结构(hAdapter, KMTQAITYPE_NODEMETADATA, meta) Then Exit For
            If meta.EngineType = DXGK_ENGINE_TYPE_3D Then Return ordinal
        Next
        Return Nothing
    End Function

    ''' <summary>分配并清零的非托管内存。</summary>
    Private Shared Function 分配清零内存(byteSize As Integer) As IntPtr
        Dim p = Marshal.AllocHGlobal(byteSize)
        For i = 0 To byteSize - 1 : Marshal.WriteByte(p, i, 0) : Next
        Return p
    End Function

    Private Shared Function 转有符号32位位模式(value As UInteger) As Integer
        Return BitConverter.ToInt32(BitConverter.GetBytes(value), 0)
    End Function

    Private Shared Function 转无符号32位位模式(value As Integer) As UInteger
        Return BitConverter.ToUInt32(BitConverter.GetBytes(value), 0)
    End Function
#End Region

#Region "PDH 通用查询"
    Private Shared Function 读取Double计数器(h As IntPtr) As Double
        Dim t As UInteger = 0
        Dim v As PDH_FMT_COUNTERVALUE
        If PdhGetFormattedCounterValue(h, PDH_FMT_DOUBLE, t, v) <> ERROR_SUCCESS Then Return Double.NaN
        If v.CStatus <> 0 Then Return Double.NaN
        Return v.doubleValue
    End Function

    Private Shared Function 读取Large计数器(h As IntPtr) As ULong
        Dim t As UInteger = 0
        Dim v As PDH_FMT_COUNTERVALUE
        If PdhGetFormattedCounterValue(h, PDH_FMT_LARGE, t, v) <> ERROR_SUCCESS Then Return 0UL
        If v.CStatus <> 0 OrElse v.largeValue <= 0L Then Return 0UL
        Return CULng(v.largeValue)
    End Function

    Private Shared Function PdhEnumInstances(objectName As String) As String()
        Dim counterLen As UInteger = 0, instanceLen As UInteger = 0
        Dim s = PdhEnumObjectItemsW(Nothing, Nothing, objectName, IntPtr.Zero, counterLen, IntPtr.Zero, instanceLen, 0UI, 0UI)
        If s <> PDH_MORE_DATA AndAlso s <> ERROR_SUCCESS Then Return Array.Empty(Of String)()
        If instanceLen = 0 Then Return Array.Empty(Of String)()
        Dim cBuf = Marshal.AllocHGlobal(CInt(counterLen) * 2)
        Dim iBuf = Marshal.AllocHGlobal(CInt(instanceLen) * 2)
        Try
            If PdhEnumObjectItemsW(Nothing, Nothing, objectName, cBuf, counterLen, iBuf, instanceLen, 0UI, 0UI) <> ERROR_SUCCESS Then Return Array.Empty(Of String)()
            Return 解析多字符串(iBuf, CInt(instanceLen))
        Finally
            Marshal.FreeHGlobal(cBuf)
            Marshal.FreeHGlobal(iBuf)
        End Try
    End Function

    Private Shared Function 解析多字符串(ptr As IntPtr, charCount As Integer) As String()
        Dim list As New List(Of String)
        Dim sb As New StringBuilder()
        For i = 0 To charCount - 1
            Dim ch = ChrW(Marshal.ReadInt16(ptr, i * 2))
            If ch = ChrW(0) Then
                If sb.Length = 0 Then Exit For
                list.Add(sb.ToString()) : sb.Clear()
            Else
                sb.Append(ch)
            End If
        Next
        Return list.ToArray()
    End Function

    ''' <summary>从 PDH 实例名抽出 "0xHH_0xLL"（小写）。</summary>
    Private Shared Function 提取LuidKey(instance As String) As String
        Dim idx = instance.IndexOf("luid_", StringComparison.OrdinalIgnoreCase)
        If idx < 0 Then Return Nothing
        Dim parts = instance.Substring(idx + 5).Split("_"c)
        If parts.Length < 2 Then Return Nothing
        If Not parts(0).StartsWith("0x", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        If Not parts(1).StartsWith("0x", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Return (parts(0) & "_" & parts(1)).ToLowerInvariant()
    End Function

    Private Shared Function 提取EngType(instance As String) As String
        Dim idx = instance.IndexOf("engtype_", StringComparison.OrdinalIgnoreCase)
        If idx < 0 Then Return Nothing
        Dim tail = instance.Substring(idx + 8)
        Dim us = tail.IndexOf("_"c)
        Return If(us >= 0, tail.Substring(0, us), tail)
    End Function
#End Region

End Class

''' <summary>
''' 通过 nvml.dll 读取 NVIDIA 显卡传感器。运行时 GetProcAddress 加载，缺失时整模块禁用，不会抛异常。
''' </summary>
Friend NotInheritable Class NvmlInterop

    Private Sub New()
    End Sub

#Region "P/Invoke + 常量"
    <DllImport("kernel32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Shared Function LoadLibrary(lpFileName As String) As IntPtr
    End Function

#Disable Warning CA2101 ' 指定对 P/Invoke 字符串参数进行封送处理
    <DllImport("kernel32.dll", CharSet:=CharSet.Ansi, SetLastError:=True)>
    Private Shared Function GetProcAddress(hModule As IntPtr, procName As String) As IntPtr
#Enable Warning CA2101 ' 指定对 P/Invoke 字符串参数进行封送处理
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function FreeLibrary(hModule As IntPtr) As Boolean
    End Function

    Private Const NVML_SUCCESS As Integer = 0
    Private Const NVML_CLOCK_GRAPHICS As Integer = 0
    Private Const NVML_CLOCK_MEM As Integer = 2

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NvmlUtilization
        Public Gpu As UInteger
        Public Memory As UInteger
    End Structure

    Private Delegate Function D_Init() As Integer
    Private Delegate Function D_Shutdown() As Integer
    Private Delegate Function D_DevCount(ByRef count As UInteger) As Integer
    Private Delegate Function D_DevByIndex(index As UInteger, ByRef device As IntPtr) As Integer
    Private Delegate Function D_DevUInt(device As IntPtr, ByRef value As UInteger) As Integer
    Private Delegate Function D_DevClock(device As IntPtr, type As Integer, ByRef mhz As UInteger) As Integer
    Private Delegate Function D_DevUtil(device As IntPtr, ByRef u As NvmlUtilization) As Integer
    Private Delegate Function D_DevUtilPair(device As IntPtr, ByRef util As UInteger, ByRef sampling As UInteger) As Integer
    Private Delegate Function D_SysVer(buf As Byte(), length As UInteger) As Integer
    Private Delegate Function D_DevVer(device As IntPtr, buf As Byte(), length As UInteger) As Integer
#End Region

#Region "状态"
    Private Shared 句柄 As IntPtr = IntPtr.Zero
    Private Shared 已初始化 As Boolean

    Private Shared p_Init As D_Init, p_Shutdown As D_Shutdown
    Private Shared p_GetCount As D_DevCount, p_GetByIndex As D_DevByIndex
    Private Shared p_Power As D_DevUInt, p_PowerLimit As D_DevUInt
    Private Shared p_Clock As D_DevClock, p_MaxClock As D_DevClock
    Private Shared p_Util As D_DevUtil
    Private Shared p_EncUtil As D_DevUtilPair, p_DecUtil As D_DevUtilPair
    Private Shared p_DriverVer As D_SysVer
    Private Shared p_VbiosVer As D_DevVer

    ' NVML 没有 GetByLuid，按 PCI 顺序与 D3DKMT 一一对应（与任务管理器一致）
    Private Shared 待分配 As Queue(Of IntPtr)
    Private Shared ReadOnly 已分配 As New Dictionary(Of ULong, IntPtr)

    Public Shared ReadOnly Property 可用 As Boolean
        Get
            Return 已初始化 AndAlso 句柄 <> IntPtr.Zero
        End Get
    End Property
#End Region

#Region "初始化 / 关闭"
    Public Shared Sub 确保初始化()
        If 已初始化 Then Return
        已初始化 = True
        Try
            句柄 = 加载NVML()
            If 句柄 = IntPtr.Zero Then Return

            p_Init = If(取委托(Of D_Init)("nvmlInit_v2"), 取委托(Of D_Init)("nvmlInit"))
            p_Shutdown = 取委托(Of D_Shutdown)("nvmlShutdown")
            p_GetCount = If(取委托(Of D_DevCount)("nvmlDeviceGetCount_v2"), 取委托(Of D_DevCount)("nvmlDeviceGetCount"))
            p_GetByIndex = If(取委托(Of D_DevByIndex)("nvmlDeviceGetHandleByIndex_v2"), 取委托(Of D_DevByIndex)("nvmlDeviceGetHandleByIndex"))
            p_Power = 取委托(Of D_DevUInt)("nvmlDeviceGetPowerUsage")
            p_PowerLimit = 取委托(Of D_DevUInt)("nvmlDeviceGetPowerManagementLimit")
            p_Clock = 取委托(Of D_DevClock)("nvmlDeviceGetClockInfo")
            p_MaxClock = 取委托(Of D_DevClock)("nvmlDeviceGetMaxClockInfo")
            p_Util = 取委托(Of D_DevUtil)("nvmlDeviceGetUtilizationRates")
            p_EncUtil = 取委托(Of D_DevUtilPair)("nvmlDeviceGetEncoderUtilization")
            p_DecUtil = 取委托(Of D_DevUtilPair)("nvmlDeviceGetDecoderUtilization")
            p_DriverVer = 取委托(Of D_SysVer)("nvmlSystemGetDriverVersion")
            p_VbiosVer = 取委托(Of D_DevVer)("nvmlDeviceGetVbiosVersion")

            If p_Init Is Nothing OrElse p_Init() <> NVML_SUCCESS Then 关闭() : Return
            构建设备队列()
        Catch
            关闭()
        End Try
    End Sub

    Public Shared Sub 关闭()
        Try
            If p_Shutdown IsNot Nothing AndAlso 句柄 <> IntPtr.Zero Then p_Shutdown()
        Catch
        End Try
        If 句柄 <> IntPtr.Zero Then
            Try : FreeLibrary(句柄) : Catch : End Try
            句柄 = IntPtr.Zero
        End If
        p_Init = Nothing : p_Shutdown = Nothing : p_GetCount = Nothing : p_GetByIndex = Nothing
        p_Power = Nothing : p_PowerLimit = Nothing
        p_Clock = Nothing : p_MaxClock = Nothing : p_Util = Nothing
        p_EncUtil = Nothing : p_DecUtil = Nothing
        p_DriverVer = Nothing : p_VbiosVer = Nothing
        待分配 = Nothing
        已分配.Clear()
        已初始化 = False
    End Sub

    Private Shared Function 加载NVML() As IntPtr
        Dim h = LoadLibrary("nvml.dll")
        If h <> IntPtr.Zero Then Return h
        Dim sys = Environment.GetFolderPath(Environment.SpecialFolder.System)
        h = LoadLibrary(IO.Path.Combine(sys, "nvml.dll"))
        If h <> IntPtr.Zero Then Return h
        Dim pf = Environment.GetEnvironmentVariable("ProgramW6432")
        If Not String.IsNullOrEmpty(pf) Then
            h = LoadLibrary(IO.Path.Combine(pf, "NVIDIA Corporation\NVSMI\nvml.dll"))
        End If
        Return h
    End Function

    Private Shared Function 取委托(Of TDel As Class)(name As String) As TDel
        Dim addr = GetProcAddress(句柄, name)
        If addr = IntPtr.Zero Then Return Nothing
        Return TryCast(CType(Marshal.GetDelegateForFunctionPointer(addr, GetType(TDel)), Object), TDel)
    End Function

    Private Shared Sub 构建设备队列()
        待分配 = New Queue(Of IntPtr)
        已分配.Clear()
        Dim count As UInteger = 0
        If p_GetCount Is Nothing OrElse p_GetCount(count) <> NVML_SUCCESS Then Return
        If count = 0UI Then Return
        For i As UInteger = 0 To count - 1UI
            Dim dev As IntPtr
            If p_GetByIndex IsNot Nothing AndAlso p_GetByIndex(i, dev) = NVML_SUCCESS Then 待分配.Enqueue(dev)
        Next
    End Sub
#End Region

#Region "设备匹配 / 字段填充"
    ''' <summary>按 LUID 分配 NVML 设备（按 PCI 顺序与 D3DKMT 一一对应；同 LUID 缓存）。</summary>
    Public Shared Function 分配设备(luidLow As UInteger, luidHigh As Integer) As IntPtr
        If Not 可用 Then Return IntPtr.Zero
        Dim key = (CULng(转无符号32位位模式(luidHigh)) << 32) Or CULng(luidLow)
        Dim dev As IntPtr
        If 已分配.TryGetValue(key, dev) Then Return dev
        If 待分配 Is Nothing OrElse 待分配.Count = 0 Then Return IntPtr.Zero
        dev = 待分配.Dequeue()
        已分配(key) = dev
        Return dev
    End Function

    Private Shared Function 转无符号32位位模式(value As Integer) As UInteger
        Return BitConverter.ToUInt32(BitConverter.GetBytes(value), 0)
    End Function

    Public Shared Sub 填充静态信息(device As IntPtr, info As GpuMonitor.GpuInfo)
        If Not 可用 Then Return
        Dim u As UInteger
        If p_PowerLimit IsNot Nothing AndAlso p_PowerLimit(device, u) = NVML_SUCCESS Then info.PowerLimitWatts = u / 1000.0F
        If p_MaxClock IsNot Nothing Then
            If p_MaxClock(device, NVML_CLOCK_GRAPHICS, u) = NVML_SUCCESS Then info.MaxCoreFrequencyHz = CULng(u) * 1_000_000UL
            If p_MaxClock(device, NVML_CLOCK_MEM, u) = NVML_SUCCESS Then info.MaxMemoryFrequencyHz = CULng(u) * 1_000_000UL
        End If
        If p_DriverVer IsNot Nothing Then
            Dim buf(95) As Byte
            If p_DriverVer(buf, CUInt(buf.Length)) = NVML_SUCCESS Then info.DriverVersion = Encoding.ASCII.GetString(buf).TrimEnd(ChrW(0))
        End If
        If p_VbiosVer IsNot Nothing Then
            Dim buf(31) As Byte
            If p_VbiosVer(device, buf, CUInt(buf.Length)) = NVML_SUCCESS Then info.VBiosVersion = Encoding.ASCII.GetString(buf).TrimEnd(ChrW(0))
        End If
    End Sub

    Public Shared Sub 填充动态信息(device As IntPtr, snap As GpuMonitor.GpuInfo)
        If Not 可用 Then Return
        Dim u As UInteger, u2 As UInteger
        If p_Power IsNot Nothing AndAlso p_Power(device, u) = NVML_SUCCESS Then
            snap.PowerWatts = u / 1000.0F
            If snap.PowerLimitWatts.HasValue AndAlso snap.PowerLimitWatts.Value > 0 Then
                snap.PowerPercent = snap.PowerWatts.Value / snap.PowerLimitWatts.Value * 100.0F
            End If
        End If
        If p_Clock IsNot Nothing Then
            If p_Clock(device, NVML_CLOCK_GRAPHICS, u) = NVML_SUCCESS Then snap.CoreFrequencyHz = CULng(u) * 1_000_000UL
            If p_Clock(device, NVML_CLOCK_MEM, u) = NVML_SUCCESS Then snap.MemoryFrequencyHz = CULng(u) * 1_000_000UL
        End If

        If p_EncUtil IsNot Nothing AndAlso p_EncUtil(device, u, u2) = NVML_SUCCESS Then snap.VideoEncoderUsage = u / 100.0F
        If p_DecUtil IsNot Nothing AndAlso p_DecUtil(device, u, u2) = NVML_SUCCESS Then snap.VideoDecoderUsage = u / 100.0F

        ' 整体占用：PDH 的 GPU Engine 计数器在部分场景（独占全屏、DWM 合成、驱动内部任务）会漏计，
        ' NVML 直接读取 GPU 工作负载比例更贴近专业软件/LHM，二者取较大值。
        If p_Util IsNot Nothing Then
            Dim ut As NvmlUtilization
            If p_Util(device, ut) = NVML_SUCCESS Then
                Dim nvUsage = ut.Gpu / 100.0F
                If nvUsage > snap.OverallUsage Then snap.OverallUsage = nvUsage
            End If
        End If
    End Sub
#End Region

End Class
