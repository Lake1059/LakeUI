Imports System.Globalization
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading

''' <summary>
''' 当前主应用进程的资源占用计数器。
''' 默认不启动任何后台线程；调用 <see cref="Enable"/> 后才会在后台采样，所有 Get 方法只返回最近一次采样快照中的值。
''' </summary>
''' <remarks>
''' <para>内存数据对应任务管理器详细信息页的活动专用工作集、专用工作集、共享工作集和提交大小。</para>
''' <para>CPU 数据来自 GetProcessTimes 与高精度墙钟差分，仅保留当前进程总体占用百分比。</para>
''' <para>GPU 数据来自 Windows PDH 的 GPU Process Memory / GPU Engine 计数器，仅保留 3D、专用显存和共享显存。</para>
''' </remarks>
Public NotInheritable Class MainAppUsageCounter

    ''' <summary>静态工具类，不允许实例化。</summary>
    Private Sub New()
    End Sub

#Region "公开类型"
    ''' <summary>
    ''' 当前主应用进程的一次资源占用采样快照。
    ''' </summary>
    Public Structure UsageSnapshot
        ''' <summary>被采样的进程 ID。</summary>
        Public ProcessId As UInteger

        ''' <summary>本次采样完成时的 UTC 时间。</summary>
        Public TimestampUtc As DateTime

        ''' <summary>活动的专用工作集大小，单位：字节。</summary>
        Public ActivePrivateWorkingSetBytes As ULong

        ''' <summary>专用工作集大小，单位：字节。</summary>
        Public PrivateWorkingSetBytes As ULong

        ''' <summary>共享工作集大小，单位：字节。</summary>
        Public SharedWorkingSetBytes As ULong

        ''' <summary>提交大小，单位：字节。</summary>
        Public CommitSizeBytes As ULong

        ''' <summary>兼容旧 API：物理内存工作集大小，单位：字节。</summary>
        Public PhysicalMemoryBytes As ULong

        ''' <summary>兼容旧 API：虚拟地址空间占用大小，单位：字节。新采样路径无法取得时回退为提交大小。</summary>
        Public VirtualMemoryBytes As ULong

        ''' <summary>兼容旧 API：进程私有提交内存大小，单位：字节。</summary>
        Public PrivateMemoryBytes As ULong

        ''' <summary>兼容旧 API：进程提交内存大小，单位：字节。</summary>
        Public CommitMemoryBytes As ULong

        ''' <summary>专用显存占用，单位：字节。</summary>
        Public GpuDedicatedMemoryBytes As ULong

        ''' <summary>共享显存占用，单位：字节。</summary>
        Public GpuSharedMemoryBytes As ULong

        ''' <summary>兼容旧 API：对外展示用的单值显存占用，单位：字节。</summary>
        Public GpuMemoryBytes As ULong

        ''' <summary>兼容旧 API：本地显存占用，单位：字节。当前映射为专用显存。</summary>
        Public GpuLocalMemoryBytes As ULong

        ''' <summary>兼容旧 API：非本地显存占用，单位：字节。当前映射为共享显存。</summary>
        Public GpuNonLocalMemoryBytes As ULong

        ''' <summary>兼容旧 API：GPU 总提交显存，单位：字节。当前由专用显存 + 共享显存估算。</summary>
        Public GpuTotalCommittedMemoryBytes As ULong

        ''' <summary>CPU 总体占用百分比，范围 0 到 100，已经按全部活动逻辑处理器归一化。</summary>
        Public CpuUsagePercent As Single

        ''' <summary>GPU 3D 引擎占用百分比，范围 0 到 100，来自当前进程所有 3D 引擎计数器聚合。</summary>
        Public Gpu3DUsagePercent As Single

        ''' <summary>兼容旧 API：CPU 占用，范围 0.0 到 1.0。</summary>
        Public CpuUsage As Single

        ''' <summary>兼容旧 API：GPU 3D 引擎占用，范围 0.0 到 1.0。</summary>
        Public Gpu3DUsage As Single

        ''' <summary>本次快照是否拿到了内存数据。</summary>
        Public HasMemoryData As Boolean

        ''' <summary>本次快照是否拿到了 CPU 占用数据。</summary>
        Public HasCpuUsageData As Boolean

        ''' <summary>本次快照是否拿到了 GPU 显存数据。</summary>
        Public HasGpuMemoryData As Boolean

        ''' <summary>本次快照是否拿到了 GPU 3D 占用数据。</summary>
        Public HasGpu3DData As Boolean
    End Structure
#End Region

#Region "公开接口"
    ''' <summary>
    ''' 后台采样间隔，单位：毫秒。最小值为 100，默认值为 1000。
    ''' </summary>
    Public Shared Property SampleIntervalMilliseconds As Integer
        Get
            SyncLock 状态锁
                Return 采样间隔毫秒
            End SyncLock
        End Get
        Set(value As Integer)
            SyncLock 状态锁
                采样间隔毫秒 = Math.Max(100, value)
            End SyncLock
        End Set
    End Property

    ''' <summary>
    ''' 当前是否已经启用后台采样线程。
    ''' </summary>
    Public Shared ReadOnly Property IsEnabled As Boolean
        Get
            SyncLock 状态锁
                Return 是否启用
            End SyncLock
        End Get
    End Property

    ''' <summary>
    ''' 启用后台采样线程。
    ''' </summary>
    ''' <param name="waitForFirstSample">是否等待首次采样完成。默认最多等待一个很短的时间，方便立即读取到第一份快照。</param>
    Public Shared Sub Enable(Optional waitForFirstSample As Boolean = True)
        Dim 需要等待首次采样 As Boolean = False

        SyncLock 状态锁
            是否启用 = True

            If 采样线程 Is Nothing OrElse Not 采样线程.IsAlive Then
                首次采样完成事件.Reset()
                停止采样事件.Reset()
                采样线程 = New Thread(AddressOf 后台采样循环) With {
                    .IsBackground = True,
                    .Name = "LakeUI MainAppUsageCounter"
                }
                采样线程.Start()
                需要等待首次采样 = waitForFirstSample
            Else
                需要等待首次采样 = waitForFirstSample AndAlso Not 首次采样完成事件.IsSet
            End If
        End SyncLock

        If 需要等待首次采样 AndAlso Not Object.ReferenceEquals(Thread.CurrentThread, 采样线程) Then
            Try
                首次采样完成事件.Wait(首次采样等待毫秒)
            Catch
            End Try
        End If
    End Sub

    ''' <summary>
    ''' 禁用后台采样线程，并释放 PDH 查询等本地资源。最近一次快照会被保留。
    ''' </summary>
    Public Shared Sub Disable()
        Dim 待等待线程 As Thread = Nothing

        SyncLock 状态锁
            是否启用 = False
            停止采样事件.Set()
            待等待线程 = 采样线程
        End SyncLock

        If 待等待线程 IsNot Nothing AndAlso Not Object.ReferenceEquals(待等待线程, Thread.CurrentThread) Then
            Try
                待等待线程.Join(1000)
            Catch
            End Try
        End If
    End Sub

    ''' <summary>
    ''' 启用后台采样线程。保留该方法名是为了兼容常见 Start/Shutdown 风格调用。
    ''' </summary>
    Public Shared Sub Start()
        Enable()
    End Sub

    ''' <summary>
    ''' 禁用后台采样线程。保留该方法名是为了兼容常见 Start/Shutdown 风格调用。
    ''' </summary>
    Public Shared Sub Shutdown()
        Disable()
    End Sub

    ''' <summary>
    ''' 返回最近一次采样快照。未启用采样时不会自动启动后台线程。
    ''' </summary>
    Public Shared Function GetSnapshot() As UsageSnapshot
        SyncLock 快照锁
            Dim snapshot = 最新快照
            FillCompatibilitySnapshot(snapshot)
            Return snapshot
        End SyncLock
    End Function

    ''' <summary>返回最近一次采样的活动专用工作集，单位：字节。</summary>
    Public Shared Function GetActivePrivateWorkingSetBytes() As ULong
        Return GetSnapshot().ActivePrivateWorkingSetBytes
    End Function

    ''' <summary>返回最近一次采样的专用工作集，单位：字节。</summary>
    Public Shared Function GetPrivateWorkingSetBytes() As ULong
        Return GetSnapshot().PrivateWorkingSetBytes
    End Function

    ''' <summary>返回最近一次采样的共享工作集，单位：字节。</summary>
    Public Shared Function GetSharedWorkingSetBytes() As ULong
        Return GetSnapshot().SharedWorkingSetBytes
    End Function

    ''' <summary>返回最近一次采样的提交大小，单位：字节。</summary>
    Public Shared Function GetCommitSizeBytes() As ULong
        Return GetSnapshot().CommitSizeBytes
    End Function

    ''' <summary>返回最近一次采样的物理内存工作集，单位：字节。</summary>
    Public Shared Function GetPhysicalMemoryBytes() As ULong
        Return GetSnapshot().PhysicalMemoryBytes
    End Function

    ''' <summary>返回最近一次采样的虚拟地址空间占用，单位：字节。</summary>
    Public Shared Function GetVirtualMemoryBytes() As ULong
        Return GetSnapshot().VirtualMemoryBytes
    End Function

    ''' <summary>返回最近一次采样的私有提交内存，单位：字节。</summary>
    Public Shared Function GetPrivateMemoryBytes() As ULong
        Return GetSnapshot().PrivateMemoryBytes
    End Function

    ''' <summary>返回最近一次采样的提交内存，单位：字节。</summary>
    Public Shared Function GetCommitMemoryBytes() As ULong
        Return GetSnapshot().CommitMemoryBytes
    End Function

    ''' <summary>返回最近一次采样的专用显存占用，单位：字节。</summary>
    Public Shared Function GetGpuDedicatedMemoryBytes() As ULong
        Return GetSnapshot().GpuDedicatedMemoryBytes
    End Function

    ''' <summary>返回最近一次采样的共享显存占用，单位：字节。</summary>
    Public Shared Function GetGpuSharedMemoryBytes() As ULong
        Return GetSnapshot().GpuSharedMemoryBytes
    End Function

    ''' <summary>返回最近一次采样的兼容单值显存占用，单位：字节。</summary>
    Public Shared Function GetGpuMemoryBytes() As ULong
        Return GetSnapshot().GpuMemoryBytes
    End Function

    ''' <summary>返回最近一次采样的本地显存占用，单位：字节。</summary>
    Public Shared Function GetGpuLocalMemoryBytes() As ULong
        Return GetSnapshot().GpuLocalMemoryBytes
    End Function

    ''' <summary>返回最近一次采样的非本地显存占用，单位：字节。</summary>
    Public Shared Function GetGpuNonLocalMemoryBytes() As ULong
        Return GetSnapshot().GpuNonLocalMemoryBytes
    End Function

    ''' <summary>返回最近一次采样的 GPU 总提交显存，单位：字节。</summary>
    Public Shared Function GetGpuTotalCommittedMemoryBytes() As ULong
        Return GetSnapshot().GpuTotalCommittedMemoryBytes
    End Function

    ''' <summary>返回最近一次采样的 CPU 占用，范围 0.0 到 1.0。</summary>
    Public Shared Function GetCpuUsage() As Single
        Return GetSnapshot().CpuUsage
    End Function

    ''' <summary>返回最近一次采样的 CPU 占用百分比，范围 0 到 100。</summary>
    Public Shared Function GetCpuUsagePercent() As Single
        Return GetSnapshot().CpuUsagePercent
    End Function

    ''' <summary>返回最近一次采样的 GPU 3D 引擎占用，范围 0.0 到 1.0。</summary>
    Public Shared Function GetGpu3DUsage() As Single
        Return GetSnapshot().Gpu3DUsage
    End Function

    ''' <summary>返回最近一次采样的 GPU 3D 引擎占用百分比，范围 0 到 100。</summary>
    Public Shared Function GetGpu3DUsagePercent() As Single
        Return GetSnapshot().Gpu3DUsagePercent
    End Function

    ''' <summary>最近一次快照是否包含内存数据。</summary>
    Public Shared Function HasMemoryData() As Boolean
        Return GetSnapshot().HasMemoryData
    End Function

    ''' <summary>最近一次快照是否包含 CPU 占用数据。</summary>
    Public Shared Function HasCpuUsageData() As Boolean
        Return GetSnapshot().HasCpuUsageData
    End Function

    ''' <summary>最近一次快照是否包含 GPU 显存数据。</summary>
    Public Shared Function HasGpuMemoryData() As Boolean
        Return GetSnapshot().HasGpuMemoryData
    End Function

    ''' <summary>最近一次快照是否包含 GPU 3D 占用数据。</summary>
    Public Shared Function HasGpu3DData() As Boolean
        Return GetSnapshot().HasGpu3DData
    End Function
#End Region

#Region "后台采样"
    ''' <summary>保护启用状态、采样线程和采样间隔的锁。</summary>
    Private Shared ReadOnly 状态锁 As New Object()

    ''' <summary>保护最近一次采样快照的锁。</summary>
    Private Shared ReadOnly 快照锁 As New Object()

    ''' <summary>首次采样完成信号，用于 Enable 后短暂等待第一份数据。</summary>
    Private Shared ReadOnly 首次采样完成事件 As New ManualResetEventSlim(False)

    ''' <summary>后台采样线程停止信号。</summary>
    Private Shared ReadOnly 停止采样事件 As New ManualResetEventSlim(False)

    ''' <summary>当前是否处于启用状态。</summary>
    Private Shared 是否启用 As Boolean

    ''' <summary>后台采样线程实例。</summary>
    Private Shared 采样线程 As Thread

    ''' <summary>后台采样间隔，单位：毫秒。</summary>
    Private Shared 采样间隔毫秒 As Integer = 1000

    ''' <summary>最近一次采样快照。未启用时保持默认值或禁用前最后一次值。</summary>
    Private Shared 最新快照 As UsageSnapshot

    ''' <summary>Enable 默认等待首次采样的最大时间，单位：毫秒。</summary>
    Private Const 首次采样等待毫秒 As Integer = 350

    ''' <summary>
    ''' 后台采样线程主体。线程只在 Enable 后启动，Disable 后退出并释放 PDH 资源。
    ''' </summary>
    Private Shared Sub 后台采样循环()
        Dim 当前进程编号 As UInteger = 获取当前进程编号()
        Dim 高精度频率 As Long = 0
        Dim 使用高精度计时 As Boolean = 查询高精度频率(高精度频率) AndAlso 高精度频率 > 0
        Dim 活动逻辑处理器数 As UInteger = 取活动逻辑处理器数()

        Dim 上次进程时间100纳秒 As ULong = 0UL
        Dim 上次墙钟计数 As Long = 0
        Dim 已有上次CPU样本 As Boolean = False

        Try
            Do While Not 停止采样事件.IsSet
                Dim 本次快照 As New UsageSnapshot With {
                    .ProcessId = 当前进程编号,
                    .TimestampUtc = DateTime.UtcNow
                }

                填充内存数据(本次快照)
                填充CPU数据(本次快照, 活动逻辑处理器数, 使用高精度计时, 高精度频率, 上次进程时间100纳秒, 上次墙钟计数, 已有上次CPU样本)
                填充GPU数据(本次快照, 当前进程编号)
                FillCompatibilitySnapshot(本次快照)

                SyncLock 快照锁
                    最新快照 = 本次快照
                End SyncLock

                首次采样完成事件.Set()

                Dim 本轮间隔 As Integer
                SyncLock 状态锁
                    本轮间隔 = 采样间隔毫秒
                End SyncLock
                停止采样事件.Wait(本轮间隔)
            Loop
        Finally
            关闭性能查询()
            SyncLock 状态锁
                If Object.ReferenceEquals(采样线程, Thread.CurrentThread) Then
                    采样线程 = Nothing
                End If
            End SyncLock
        End Try
    End Sub
#End Region

#Region "本地内存采样"
    ''' <summary>NtQueryInformationProcess 返回的进程虚拟内存计数器扩展结构，用于兼容旧版内存字段。</summary>
    <StructLayout(LayoutKind.Sequential)>
    Private Structure 虚拟内存计数器扩展
        Public 峰值虚拟大小 As UIntPtr
        Public 虚拟大小 As UIntPtr
        Public 缺页次数 As UInteger
        Public 峰值工作集大小 As UIntPtr
        Public 工作集大小 As UIntPtr
        Public 峰值分页池配额 As UIntPtr
        Public 分页池配额 As UIntPtr
        Public 峰值非分页池配额 As UIntPtr
        Public 非分页池配额 As UIntPtr
        Public 页面文件用量 As UIntPtr
        Public 峰值页面文件用量 As UIntPtr
        Public 私有用量 As UIntPtr
    End Structure

    ''' <summary>GetProcessMemoryInfo 返回的进程内存计数器 EX2 结构。</summary>
    <StructLayout(LayoutKind.Sequential)>
    Private Structure 进程内存计数器扩展2
        Public 结构大小 As UInteger
        Public 缺页次数 As UInteger
        Public 峰值工作集大小 As UIntPtr
        Public 工作集大小 As UIntPtr
        Public 峰值分页池配额 As UIntPtr
        Public 分页池配额 As UIntPtr
        Public 峰值非分页池配额 As UIntPtr
        Public 非分页池配额 As UIntPtr
        Public 页面文件用量 As UIntPtr
        Public 峰值页面文件用量 As UIntPtr
        Public 私有用量 As UIntPtr
        Public 专用工作集大小 As UIntPtr
        Public 共享提交用量 As ULong
    End Structure

    ''' <summary>GetProcessMemoryInfo 返回的进程内存计数器 EX 结构。</summary>
    <StructLayout(LayoutKind.Sequential)>
    Private Structure 进程内存计数器扩展
        Public 结构大小 As UInteger
        Public 缺页次数 As UInteger
        Public 峰值工作集大小 As UIntPtr
        Public 工作集大小 As UIntPtr
        Public 峰值分页池配额 As UIntPtr
        Public 分页池配额 As UIntPtr
        Public 峰值非分页池配额 As UIntPtr
        Public 非分页池配额 As UIntPtr
        Public 页面文件用量 As UIntPtr
        Public 峰值页面文件用量 As UIntPtr
        Public 私有用量 As UIntPtr
    End Structure

    ''' <summary>NTSTATUS 成功码。</summary>
    Private Const 本地状态成功 As Integer = 0

    ''' <summary>ProcessInformationClass.ProcessVmCounters。</summary>
    Private Const 进程虚拟内存计数器信息类 As Integer = 3

    ''' <summary>查询进程底层虚拟内存信息。</summary>
    <DllImport("ntdll.dll", EntryPoint:="NtQueryInformationProcess")>
    Private Shared Function 查询进程信息(进程句柄 As IntPtr,
                                          信息类别 As Integer,
                                          ByRef 进程信息 As 虚拟内存计数器扩展,
                                          信息长度 As UInteger,
                                          ByRef 返回长度 As UInteger) As Integer
    End Function

    ''' <summary>查询进程内存计数器 EX2。</summary>
    <DllImport("psapi.dll", EntryPoint:="GetProcessMemoryInfo", SetLastError:=True)>
    Private Shared Function 获取进程内存信息2(进程句柄 As IntPtr,
                                             ByRef 内存计数器 As 进程内存计数器扩展2,
                                             结构大小 As UInteger) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    ''' <summary>查询进程内存计数器 EX。</summary>
    <DllImport("psapi.dll", EntryPoint:="GetProcessMemoryInfo", SetLastError:=True)>
    Private Shared Function 获取进程内存信息(进程句柄 As IntPtr,
                                            ByRef 内存计数器 As 进程内存计数器扩展,
                                            结构大小 As UInteger) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    ''' <summary>
    ''' 填充当前进程内存数据，对齐任务管理器详细信息页的四个内存列。
    ''' </summary>
    Private Shared Sub 填充内存数据(ByRef 快照 As UsageSnapshot)
        Try
            Dim 内存计数器2 As New 进程内存计数器扩展2 With {
                .结构大小 = CUInt(Marshal.SizeOf(Of 进程内存计数器扩展2)())
            }

            If 获取进程内存信息2(获取当前进程伪句柄(), 内存计数器2, 内存计数器2.结构大小) Then
                Dim 工作集大小 = 指针大小转无符号64位(内存计数器2.工作集大小)
                Dim 专用工作集大小 = 指针大小转无符号64位(内存计数器2.专用工作集大小)

                If 专用工作集大小 > 0UL OrElse 工作集大小 = 0UL Then
                    快照.ActivePrivateWorkingSetBytes = 专用工作集大小
                    快照.PrivateWorkingSetBytes = 专用工作集大小
                    快照.SharedWorkingSetBytes = 饱和相减(工作集大小, 专用工作集大小)
                    快照.CommitSizeBytes = 指针大小转无符号64位(内存计数器2.私有用量)
                    填充虚拟内存兼容数据(快照)
                    快照.HasMemoryData = True
                    Return
                End If
            End If
        Catch
        End Try

        Try
            Dim 内存计数器 As New 进程内存计数器扩展 With {
                .结构大小 = CUInt(Marshal.SizeOf(Of 进程内存计数器扩展)())
            }

            If 获取进程内存信息(获取当前进程伪句柄(), 内存计数器, 内存计数器.结构大小) Then
                Dim 工作集大小 = 指针大小转无符号64位(内存计数器.工作集大小)
                Dim 提交大小 = 指针大小转无符号64位(内存计数器.私有用量)

                快照.PrivateWorkingSetBytes = Math.Min(工作集大小, 提交大小)
                快照.ActivePrivateWorkingSetBytes = 快照.PrivateWorkingSetBytes
                快照.SharedWorkingSetBytes = 饱和相减(工作集大小, 快照.PrivateWorkingSetBytes)
                快照.CommitSizeBytes = 提交大小
                填充虚拟内存兼容数据(快照)
                快照.HasMemoryData = True
            End If
        Catch
        End Try
    End Sub

    Private Shared Sub 填充虚拟内存兼容数据(ByRef 快照 As UsageSnapshot)
        Try
            Dim 虚拟计数器 As New 虚拟内存计数器扩展()
            Dim 返回长度 As UInteger = 0UI
            Dim 结构大小 As UInteger = CUInt(Marshal.SizeOf(Of 虚拟内存计数器扩展)())

            If 查询进程信息(获取当前进程伪句柄(), 进程虚拟内存计数器信息类, 虚拟计数器, 结构大小, 返回长度) = 本地状态成功 Then
                快照.VirtualMemoryBytes = 指针大小转无符号64位(虚拟计数器.虚拟大小)
                快照.PhysicalMemoryBytes = 指针大小转无符号64位(虚拟计数器.工作集大小)
                快照.CommitMemoryBytes = 指针大小转无符号64位(虚拟计数器.页面文件用量)
                快照.PrivateMemoryBytes = 指针大小转无符号64位(虚拟计数器.私有用量)
            End If
        Catch
        End Try
    End Sub
#End Region

#Region "本地CPU采样"
    ''' <summary>Win32 FILETIME 结构。</summary>
    <StructLayout(LayoutKind.Sequential)>
    Private Structure 原生文件时间
        Public 低32位 As UInteger
        Public 高32位 As UInteger
    End Structure

    ''' <summary>读取进程创建、退出、内核态和用户态累计时间。</summary>
    <DllImport("kernel32.dll", EntryPoint:="GetProcessTimes")>
    Private Shared Function 获取进程时间(进程句柄 As IntPtr,
                                        ByRef 创建时间 As 原生文件时间,
                                        ByRef 退出时间 As 原生文件时间,
                                        ByRef 内核时间 As 原生文件时间,
                                        ByRef 用户时间 As 原生文件时间) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    ''' <summary>读取高精度计时器当前值。</summary>
    <DllImport("kernel32.dll", EntryPoint:="QueryPerformanceCounter")>
    Private Shared Function 查询高精度计数器(ByRef 计数器值 As Long) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    ''' <summary>读取高精度计时器频率。</summary>
    <DllImport("kernel32.dll", EntryPoint:="QueryPerformanceFrequency")>
    Private Shared Function 查询高精度频率(ByRef 频率 As Long) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    ''' <summary>读取指定处理器组的活动逻辑处理器数量。</summary>
    <DllImport("kernel32.dll", EntryPoint:="GetActiveProcessorCount")>
    Private Shared Function 获取活动处理器数量(处理器组 As UShort) As UInteger
    End Function

    ''' <summary>GetActiveProcessorCount 的全部处理器组常量。</summary>
    Private Const 全部处理器组 As UShort = &HFFFFUS

    ''' <summary>
    ''' 根据进程累计 CPU 时间与墙钟差分填充 CPU 占用。
    ''' 首次采样没有差分基准，因此不会设置 HasCpuUsageData。
    ''' </summary>
    Private Shared Sub 填充CPU数据(ByRef 快照 As UsageSnapshot,
                                  活动逻辑处理器数 As UInteger,
                                  使用高精度计时 As Boolean,
                                  高精度频率 As Long,
                                  ByRef 上次进程时间100纳秒 As ULong,
                                  ByRef 上次墙钟计数 As Long,
                                  ByRef 已有上次CPU样本 As Boolean)
        Dim 当前进程时间100纳秒 As ULong = 0UL
        If Not 取进程CPU总时间(当前进程时间100纳秒) Then Return

        Dim 当前墙钟计数 As Long = 取墙钟计数(使用高精度计时)
        If 已有上次CPU样本 AndAlso 当前进程时间100纳秒 >= 上次进程时间100纳秒 AndAlso 当前墙钟计数 > 上次墙钟计数 Then
            Dim 经过秒数 As Double
            If 使用高精度计时 Then
                经过秒数 = CDbl(当前墙钟计数 - 上次墙钟计数) / CDbl(高精度频率)
            Else
                经过秒数 = CDbl(当前墙钟计数 - 上次墙钟计数) / 1000.0R
            End If

            If 经过秒数 > 0 Then
                Dim 进程CPU秒数 As Double = CDbl(当前进程时间100纳秒 - 上次进程时间100纳秒) / 10000000.0R
                Dim CPU占用 As Double = 进程CPU秒数 / 经过秒数 / Math.Max(1.0R, CDbl(活动逻辑处理器数))
                快照.CpuUsagePercent = 限制到0到100(CPU占用 * 100.0R)
                快照.HasCpuUsageData = True
            End If
        End If

        上次进程时间100纳秒 = 当前进程时间100纳秒
        上次墙钟计数 = 当前墙钟计数
        已有上次CPU样本 = True
    End Sub

    ''' <summary>读取当前进程内核态与用户态 CPU 累计时间，单位为 100 纳秒。</summary>
    Private Shared Function 取进程CPU总时间(ByRef 总时间100纳秒 As ULong) As Boolean
        Dim 创建时间 As 原生文件时间 = Nothing
        Dim 退出时间 As 原生文件时间 = Nothing
        Dim 内核时间 As 原生文件时间 = Nothing
        Dim 用户时间 As 原生文件时间 = Nothing

        If Not 获取进程时间(获取当前进程伪句柄(), 创建时间, 退出时间, 内核时间, 用户时间) Then Return False
        总时间100纳秒 = 饱和相加(文件时间转无符号64位(内核时间), 文件时间转无符号64位(用户时间))
        Return True
    End Function

    ''' <summary>读取当前墙钟计数。高精度计时不可用时退回 Environment.TickCount64。</summary>
    Private Shared Function 取墙钟计数(使用高精度计时 As Boolean) As Long
        If 使用高精度计时 Then
            Dim 当前值 As Long = 0
            If 查询高精度计数器(当前值) Then Return 当前值
        End If
        Return Environment.TickCount64
    End Function

    ''' <summary>读取全部处理器组的活动逻辑处理器数量，失败时退回 Environment.ProcessorCount。</summary>
    Private Shared Function 取活动逻辑处理器数() As UInteger
        Try
            Dim 数量 = 获取活动处理器数量(全部处理器组)
            If 数量 > 0UI Then Return 数量
        Catch
        End Try
        Return CUInt(Math.Max(1, Environment.ProcessorCount))
    End Function
#End Region

#Region "PDH显卡采样"
    ''' <summary>内部 PDH 计数器用途类型。</summary>
    Private Enum 性能计数器类型
        专用显存
        共享显存
        三维占用
    End Enum

    ''' <summary>已添加到 PDH 查询中的一个计数器。</summary>
    Private NotInheritable Class 性能计数器项
        ''' <summary>PDH 计数器句柄。</summary>
        Public Property 句柄 As IntPtr

        ''' <summary>该计数器代表的数据类型。</summary>
        Public Property 类型 As 性能计数器类型
    End Class

    ''' <summary>待添加到 PDH 查询中的计数器路径。</summary>
    Private Structure 性能计数器路径
        ''' <summary>完整英文计数器路径。</summary>
        Public 路径 As String

        ''' <summary>该计数器代表的数据类型。</summary>
        Public 类型 As 性能计数器类型
    End Structure

    ''' <summary>PDH 格式化计数器值。doubleValue 与 largeValue 共用同一块联合字段。</summary>
    <StructLayout(LayoutKind.Explicit, Size:=16)>
    Private Structure 性能计数器格式化值
        <FieldOffset(0)> Public 状态 As Integer
        <FieldOffset(8)> Public 双精度值 As Double
        <FieldOffset(8)> Public 长整数值 As Long
    End Structure

    ''' <summary>打开一个 PDH 查询。</summary>
    <DllImport("pdh.dll", EntryPoint:="PdhOpenQueryW", CharSet:=CharSet.Unicode)>
    Private Shared Function 打开性能查询(数据源 As String, 用户数据 As IntPtr, ByRef 查询句柄 As IntPtr) As UInteger
    End Function

    ''' <summary>向 PDH 查询添加英文计数器路径，避免系统区域语言影响计数器名称。</summary>
    <DllImport("pdh.dll", EntryPoint:="PdhAddEnglishCounterW", CharSet:=CharSet.Unicode)>
    Private Shared Function 添加英文性能计数器(查询句柄 As IntPtr, 完整计数器路径 As String, 用户数据 As IntPtr, ByRef 计数器句柄 As IntPtr) As UInteger
    End Function

    ''' <summary>采集 PDH 查询中的所有计数器数据。</summary>
    <DllImport("pdh.dll", EntryPoint:="PdhCollectQueryData")>
    Private Shared Function 采集性能查询数据(查询句柄 As IntPtr) As UInteger
    End Function

    ''' <summary>读取一个格式化后的 PDH 计数器值。</summary>
    <DllImport("pdh.dll", EntryPoint:="PdhGetFormattedCounterValue")>
    Private Shared Function 获取格式化计数器值(计数器句柄 As IntPtr,
                                              格式 As UInteger,
                                              ByRef 类型输出 As UInteger,
                                              ByRef 值 As 性能计数器格式化值) As UInteger
    End Function

    ''' <summary>关闭 PDH 查询并释放其下所有计数器句柄。</summary>
    <DllImport("pdh.dll", EntryPoint:="PdhCloseQuery")>
    Private Shared Function 关闭性能查询句柄(查询句柄 As IntPtr) As UInteger
    End Function

    ''' <summary>枚举 PDH 对象的计数器和实例列表。</summary>
    <DllImport("pdh.dll", EntryPoint:="PdhEnumObjectItemsW", CharSet:=CharSet.Unicode)>
    Private Shared Function 枚举性能对象条目(数据源 As String,
                                            机器名 As String,
                                            对象名 As String,
                                            计数器列表 As IntPtr,
                                            ByRef 计数器列表长度 As UInteger,
                                            实例列表 As IntPtr,
                                            ByRef 实例列表长度 As UInteger,
                                            详细级别 As UInteger,
                                            标志 As UInteger) As UInteger
    End Function

    ''' <summary>Win32 ERROR_SUCCESS。</summary>
    Private Const 性能操作成功 As UInteger = 0UI

    ''' <summary>PDH_MORE_DATA，表示需要调用方分配更大的缓冲区。</summary>
    Private Const 性能需要更多数据 As UInteger = &H800007D2UI

    ''' <summary>PDH double 格式化标志。</summary>
    Private Const 性能格式双精度 As UInteger = &H200UI

    ''' <summary>PDH large integer 格式化标志。</summary>
    Private Const 性能格式长整数 As UInteger = &H400UI

    ''' <summary>重新枚举 GPU PDH 实例的最小间隔，单位：毫秒。</summary>
    Private Const 性能计数器刷新间隔毫秒 As Long = 2000

    ''' <summary>当前 PDH 查询句柄。</summary>
    Private Shared 性能查询句柄 As IntPtr = IntPtr.Zero

    ''' <summary>当前 PDH 查询中已添加的计数器列表。</summary>
    Private Shared 性能计数器列表 As New List(Of 性能计数器项)()

    ''' <summary>当前 PDH 计数器路径签名，用于判断是否需要重建查询。</summary>
    Private Shared 性能计数器签名 As String = ""

    ''' <summary>上一次重建 PDH 查询的 Environment.TickCount64 毫秒值。</summary>
    Private Shared 上次重建性能查询毫秒 As Long = Long.MinValue

    ''' <summary>
    ''' 填充 GPU 显存和 GPU 3D 占用数据。PDH 实例按当前进程 PID 过滤。
    ''' </summary>
    Private Shared Sub 填充GPU数据(ByRef 快照 As UsageSnapshot, 当前进程编号 As UInteger)
        确保性能查询(当前进程编号, False)
        If 性能查询句柄 = IntPtr.Zero OrElse 性能计数器列表.Count = 0 Then Return

        If 采集性能查询数据(性能查询句柄) <> 性能操作成功 Then
            确保性能查询(当前进程编号, True)
            If 性能查询句柄 = IntPtr.Zero Then Return
            采集性能查询数据(性能查询句柄)
        End If

        Dim 专用显存字节 As ULong = 0UL
        Dim 共享显存字节 As ULong = 0UL
        Dim 三维占用百分比 As Double = 0.0R
        Dim 有显存数据 As Boolean = False
        Dim 有三维数据 As Boolean = False

        For Each 计数器 In 性能计数器列表
            Select Case 计数器.类型
                Case 性能计数器类型.三维占用
                    Dim 值 As Double = 读取双精度计数器(计数器.句柄)
                    If Not Double.IsNaN(值) Then
                        三维占用百分比 += Math.Max(0.0R, 值)
                        有三维数据 = True
                    End If

                Case Else
                    Dim 字节数 As ULong = 读取长整数计数器(计数器.句柄)
                    If 字节数 > 0UL Then 有显存数据 = True

                    Select Case 计数器.类型
                        Case 性能计数器类型.专用显存
                            专用显存字节 = 饱和相加(专用显存字节, 字节数)
                        Case 性能计数器类型.共享显存
                            共享显存字节 = 饱和相加(共享显存字节, 字节数)
                    End Select
            End Select
        Next

        快照.GpuDedicatedMemoryBytes = 专用显存字节
        快照.GpuSharedMemoryBytes = 共享显存字节
        快照.HasGpuMemoryData = 有显存数据 OrElse 专用显存字节 > 0UL OrElse 共享显存字节 > 0UL

        If 有三维数据 Then
            快照.Gpu3DUsagePercent = 限制到0到100(三维占用百分比)
            快照.HasGpu3DData = True
        End If
    End Sub

    ''' <summary>
    ''' 按当前进程 PID 枚举 GPU PDH 实例，并在实例变化时重建查询。
    ''' </summary>
    Private Shared Sub 确保性能查询(当前进程编号 As UInteger, 强制重建 As Boolean)
        Dim 当前毫秒 As Long = Environment.TickCount64
        If Not 强制重建 AndAlso 性能查询句柄 <> IntPtr.Zero AndAlso 性能计数器列表.Count > 0 AndAlso 当前毫秒 - 上次重建性能查询毫秒 < 性能计数器刷新间隔毫秒 Then Return

        Dim 路径列表 As New List(Of 性能计数器路径)()

        For Each 实例名 In 枚举性能实例("GPU Process Memory")
            If Not 是当前进程实例(实例名, 当前进程编号) Then Continue For
            路径列表.Add(New 性能计数器路径 With {.路径 = "\GPU Process Memory(" & 实例名 & ")\Dedicated Usage", .类型 = 性能计数器类型.专用显存})
            路径列表.Add(New 性能计数器路径 With {.路径 = "\GPU Process Memory(" & 实例名 & ")\Shared Usage", .类型 = 性能计数器类型.共享显存})
        Next

        For Each 实例名 In 枚举性能实例("GPU Engine")
            If Not 是当前进程实例(实例名, 当前进程编号) Then Continue For
            If Not 是三维引擎实例(实例名) Then Continue For
            路径列表.Add(New 性能计数器路径 With {.路径 = "\GPU Engine(" & 实例名 & ")\Utilization Percentage", .类型 = 性能计数器类型.三维占用})
        Next

        Dim 新签名 = 计算路径签名(路径列表)
        If Not 强制重建 AndAlso 性能查询句柄 <> IntPtr.Zero AndAlso String.Equals(新签名, 性能计数器签名, StringComparison.Ordinal) Then
            上次重建性能查询毫秒 = 当前毫秒
            Return
        End If

        Dim 新查询句柄 As IntPtr = IntPtr.Zero
        Dim 新计数器列表 As New List(Of 性能计数器项)()
        If 打开性能查询(Nothing, IntPtr.Zero, 新查询句柄) <> 性能操作成功 Then Return

        Try
            For Each 路径 In 路径列表
                Dim 新计数器句柄 As IntPtr = IntPtr.Zero
                If 添加英文性能计数器(新查询句柄, 路径.路径, IntPtr.Zero, 新计数器句柄) = 性能操作成功 Then
                    新计数器列表.Add(New 性能计数器项 With {.句柄 = 新计数器句柄, .类型 = 路径.类型})
                End If
            Next

            采集性能查询数据(新查询句柄)
            Thread.Sleep(20)
            采集性能查询数据(新查询句柄)

            Dim 旧查询句柄 = 性能查询句柄
            性能查询句柄 = 新查询句柄
            性能计数器列表 = 新计数器列表
            性能计数器签名 = 新签名
            上次重建性能查询毫秒 = 当前毫秒
            新查询句柄 = IntPtr.Zero

            If 旧查询句柄 <> IntPtr.Zero Then
                Try : 关闭性能查询句柄(旧查询句柄) : Catch : End Try
            End If
        Finally
            If 新查询句柄 <> IntPtr.Zero Then
                Try : 关闭性能查询句柄(新查询句柄) : Catch : End Try
            End If
        End Try
    End Sub

    ''' <summary>枚举指定 PDH 对象的实例名。</summary>
    Private Shared Function 枚举性能实例(对象名 As String) As String()
        Dim 计数器列表长度 As UInteger = 0UI
        Dim 实例列表长度 As UInteger = 0UI
        Dim 状态 = 枚举性能对象条目(Nothing, Nothing, 对象名, IntPtr.Zero, 计数器列表长度, IntPtr.Zero, 实例列表长度, 0UI, 0UI)

        If 状态 <> 性能需要更多数据 AndAlso 状态 <> 性能操作成功 Then Return Array.Empty(Of String)()
        If 实例列表长度 = 0UI Then Return Array.Empty(Of String)()

        Dim 计数器缓冲 As IntPtr = IntPtr.Zero
        Dim 实例缓冲 As IntPtr = IntPtr.Zero
        Try
            If 计数器列表长度 > 0UI Then 计数器缓冲 = Marshal.AllocHGlobal(CInt(计数器列表长度) * 2)
            实例缓冲 = Marshal.AllocHGlobal(CInt(实例列表长度) * 2)

            状态 = 枚举性能对象条目(Nothing, Nothing, 对象名, 计数器缓冲, 计数器列表长度, 实例缓冲, 实例列表长度, 0UI, 0UI)
            If 状态 <> 性能操作成功 Then Return Array.Empty(Of String)()
            Return 解析多字符串(实例缓冲, CInt(实例列表长度))
        Catch
            Return Array.Empty(Of String)()
        Finally
            If 计数器缓冲 <> IntPtr.Zero Then Marshal.FreeHGlobal(计数器缓冲)
            If 实例缓冲 <> IntPtr.Zero Then Marshal.FreeHGlobal(实例缓冲)
        End Try
    End Function

    ''' <summary>读取 PDH double 类型计数器。</summary>
    Private Shared Function 读取双精度计数器(计数器句柄 As IntPtr) As Double
        Dim 类型输出 As UInteger = 0UI
        Dim 值 As 性能计数器格式化值 = Nothing
        If 获取格式化计数器值(计数器句柄, 性能格式双精度, 类型输出, 值) <> 性能操作成功 Then Return Double.NaN
        If 值.状态 <> 0 Then Return Double.NaN
        Return 值.双精度值
    End Function

    ''' <summary>读取 PDH large integer 类型计数器。</summary>
    Private Shared Function 读取长整数计数器(计数器句柄 As IntPtr) As ULong
        Dim 类型输出 As UInteger = 0UI
        Dim 值 As 性能计数器格式化值 = Nothing
        If 获取格式化计数器值(计数器句柄, 性能格式长整数, 类型输出, 值) <> 性能操作成功 Then Return 0UL
        If 值.状态 <> 0 OrElse 值.长整数值 <= 0L Then Return 0UL
        Return CULng(值.长整数值)
    End Function

    ''' <summary>判断 PDH 实例名是否属于当前进程。</summary>
    Private Shared Function 是当前进程实例(实例名 As String, 当前进程编号 As UInteger) As Boolean
        If String.IsNullOrEmpty(实例名) Then Return False
        Dim 前缀 = "pid_" & 当前进程编号.ToString(CultureInfo.InvariantCulture) & "_"
        Return 实例名.StartsWith(前缀, StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>判断 GPU Engine 实例是否为 3D 引擎。</summary>
    Private Shared Function 是三维引擎实例(实例名 As String) As Boolean
        Dim 索引 = 实例名.IndexOf("engtype_", StringComparison.OrdinalIgnoreCase)
        If 索引 < 0 Then Return False

        Dim 起点 = 索引 + "engtype_".Length
        Dim 终点 = 实例名.IndexOf("_"c, 起点)
        Dim 引擎类型 = If(终点 >= 0, 实例名.Substring(起点, 终点 - 起点), 实例名.Substring(起点))
        Return 引擎类型.Equals("3D", StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>根据计数器路径计算稳定签名，用于判断 PDH 查询是否需要重建。</summary>
    Private Shared Function 计算路径签名(路径列表 As List(Of 性能计数器路径)) As String
        If 路径列表 Is Nothing OrElse 路径列表.Count = 0 Then Return ""
        Dim 路径数组 = 路径列表.Select(Function(项) 项.路径).OrderBy(Function(路径) 路径, StringComparer.Ordinal).ToArray()
        Return String.Join(vbLf, 路径数组)
    End Function

    ''' <summary>解析 Windows MULTI_SZ 字符串缓冲区。</summary>
    Private Shared Function 解析多字符串(指针 As IntPtr, 字符数量 As Integer) As String()
        Dim 列表 As New List(Of String)()
        Dim 构建器 As New StringBuilder()

        For 索引 As Integer = 0 To 字符数量 - 1
            Dim 字符 = ChrW(Marshal.ReadInt16(指针, 索引 * 2))
            If 字符 = ChrW(0) Then
                If 构建器.Length = 0 Then Exit For
                列表.Add(构建器.ToString())
                构建器.Clear()
            Else
                构建器.Append(字符)
            End If
        Next

        Return 列表.ToArray()
    End Function

    ''' <summary>关闭当前 PDH 查询，并清空计数器缓存。</summary>
    Private Shared Sub 关闭性能查询()
        If 性能查询句柄 <> IntPtr.Zero Then
            Try : 关闭性能查询句柄(性能查询句柄) : Catch : End Try
        End If

        性能查询句柄 = IntPtr.Zero
        性能计数器列表 = New List(Of 性能计数器项)()
        性能计数器签名 = ""
        上次重建性能查询毫秒 = Long.MinValue
    End Sub
#End Region

#Region "通用本地辅助"
    ''' <summary>填充旧版字段，保证优化后的精简采样不会破坏既有二进制/源码调用预期。</summary>
    Private Shared Sub FillCompatibilitySnapshot(ByRef 快照 As UsageSnapshot)
        If 快照.PhysicalMemoryBytes = 0UL Then
            快照.PhysicalMemoryBytes = 饱和相加(快照.PrivateWorkingSetBytes, 快照.SharedWorkingSetBytes)
        End If
        If 快照.PrivateMemoryBytes = 0UL Then 快照.PrivateMemoryBytes = 快照.CommitSizeBytes
        If 快照.CommitMemoryBytes = 0UL Then 快照.CommitMemoryBytes = 快照.CommitSizeBytes
        If 快照.VirtualMemoryBytes = 0UL Then 快照.VirtualMemoryBytes = 快照.CommitSizeBytes

        If 快照.GpuLocalMemoryBytes = 0UL Then 快照.GpuLocalMemoryBytes = 快照.GpuDedicatedMemoryBytes
        If 快照.GpuNonLocalMemoryBytes = 0UL Then 快照.GpuNonLocalMemoryBytes = 快照.GpuSharedMemoryBytes
        If 快照.GpuTotalCommittedMemoryBytes = 0UL Then
            快照.GpuTotalCommittedMemoryBytes = 饱和相加(快照.GpuDedicatedMemoryBytes, 快照.GpuSharedMemoryBytes)
        End If
        If 快照.GpuMemoryBytes = 0UL Then 快照.GpuMemoryBytes = 快照.GpuTotalCommittedMemoryBytes

        快照.CpuUsage = 限制到0到1(快照.CpuUsagePercent / 100.0R)
        快照.Gpu3DUsage = 限制到0到1(快照.Gpu3DUsagePercent / 100.0R)
    End Sub

    ''' <summary>获取当前进程伪句柄。</summary>
    <DllImport("kernel32.dll", EntryPoint:="GetCurrentProcess")>
    Private Shared Function 获取当前进程伪句柄() As IntPtr
    End Function

    ''' <summary>获取当前进程 ID。</summary>
    <DllImport("kernel32.dll", EntryPoint:="GetCurrentProcessId")>
    Private Shared Function 获取当前进程编号() As UInteger
    End Function

    ''' <summary>将 UIntPtr 转成 ULong。</summary>
    Private Shared Function 指针大小转无符号64位(值 As UIntPtr) As ULong
        Return 值.ToUInt64()
    End Function

    ''' <summary>将 FILETIME 结构转成 64 位无符号整数。</summary>
    Private Shared Function 文件时间转无符号64位(值 As 原生文件时间) As ULong
        Return (CULng(值.高32位) << 32) Or CULng(值.低32位)
    End Function

    ''' <summary>将浮点值限制到 0.0 到 1.0 范围内。</summary>
    Private Shared Function 限制到0到1(值 As Double) As Single
        If Double.IsNaN(值) OrElse Double.IsInfinity(值) Then Return 0.0F
        If 值 < 0.0R Then Return 0.0F
        If 值 > 1.0R Then Return 1.0F
        Return CSng(值)
    End Function

    ''' <summary>将浮点值限制到 0 到 100 范围内。</summary>
    Private Shared Function 限制到0到100(值 As Double) As Single
        If Double.IsNaN(值) OrElse Double.IsInfinity(值) Then Return 0.0F
        If 值 < 0.0R Then Return 0.0F
        If 值 > 100.0R Then Return 100.0F
        Return CSng(值)
    End Function

    ''' <summary>执行 ULong 饱和加法，避免计数器聚合时溢出。</summary>
    Private Shared Function 饱和相加(左值 As ULong, 右值 As ULong) As ULong
        If ULong.MaxValue - 左值 < 右值 Then Return ULong.MaxValue
        Return 左值 + 右值
    End Function

    ''' <summary>执行 ULong 饱和减法，避免计数器短暂不一致时下溢。</summary>
    Private Shared Function 饱和相减(左值 As ULong, 右值 As ULong) As ULong
        If 左值 < 右值 Then Return 0UL
        Return 左值 - 右值
    End Function
#End Region

End Class
