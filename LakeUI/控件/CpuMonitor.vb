Imports System.ComponentModel
Imports System.Numerics
Imports System.Runtime.InteropServices
Imports System.Threading
Imports Microsoft.Win32
Imports Vortice.Direct2D1
Imports Vortice.DirectWrite

''' <summary>
''' 任务管理器风格的 CPU 占用监视器（仅逻辑核心显示模式）。
''' 使用 NtQuerySystemInformation(SystemProcessorPerformanceInformation) 获取实时数据，
''' 通过枚举处理器组兼容 64 核以上多处理器高端主板。
''' </summary>
<DefaultEvent("SampleUpdated")>
Public Class CpuMonitor

#Region "D2D 资源"
    Private _当前合成器 As WindowCompositor
#End Region

    Public Event SampleUpdated As EventHandler

    ''' <summary>采样器首次初始化完成时触发，可用于刷新处理器组下拉列表等。仅触发一次。</summary>
    Public Event SamplerReady As EventHandler

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
    ''' <summary>CPU 占用采样器：基于 NtQuerySystemInformation 的稳定数据源，支持多处理器组。</summary>
    Friend NotInheritable Class CpuSampler

        <StructLayout(LayoutKind.Sequential)>
        Private Structure SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION
            Public IdleTime As Long
            Public KernelTime As Long ' 包含 IdleTime
            Public UserTime As Long
            Public Reserved1_0 As Long
            Public Reserved1_1 As Long
            Public Reserved2 As UInteger
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure GROUP_AFFINITY
            Public Mask As UIntPtr
            Public Group As UShort
            Public Reserved0 As UShort
            Public Reserved1 As UShort
            Public Reserved2 As UShort
        End Structure

        Private Const SystemProcessorPerformanceInformation As Integer = 8
        Private Const STATUS_SUCCESS As UInteger = 0
        Private Const RelationGroup As Integer = 4

        <DllImport("ntdll.dll")>
        Private Shared Function NtQuerySystemInformation(SystemInformationClass As Integer,
                                                         SystemInformation As IntPtr,
                                                         SystemInformationLength As UInteger,
                                                         ByRef ReturnLength As UInteger) As UInteger
        End Function

        <DllImport("kernel32.dll")>
        Private Shared Function GetLogicalProcessorInformationEx(RelationshipType As Integer,
                                                                 Buffer As IntPtr,
                                                                 ByRef ReturnedLength As UInteger) As <MarshalAs(UnmanagedType.Bool)> Boolean
        End Function

        <DllImport("kernel32.dll")>
        Private Shared Function GetCurrentThread() As IntPtr
        End Function

        <DllImport("kernel32.dll")>
        Private Shared Function SetThreadGroupAffinity(hThread As IntPtr,
                                                       ByRef GroupAffinity As GROUP_AFFINITY,
                                                       ByRef PreviousGroupAffinity As GROUP_AFFINITY) As <MarshalAs(UnmanagedType.Bool)> Boolean
        End Function

        Private ReadOnly Property 处理器组 As (Group As UShort, Count As Integer)()
        Private ReadOnly Property 总核心数 As Integer
        Private Property 上次Idle As Long()
        Private Property 上次总计 As Long()
        Private Property 当前占用 As Single()
        Private ReadOnly Property 采样锁 As New Object()

        Public Sub New()
            处理器组 = 枚举处理器组()
            总核心数 = 处理器组.Sum(Function(g) g.Count)
            If 总核心数 <= 0 Then 总核心数 = Environment.ProcessorCount
            ReDim 上次Idle(总核心数 - 1)
            ReDim 上次总计(总核心数 - 1)
            ReDim 当前占用(总核心数 - 1)
            Try
                Sample()
            Catch
            End Try
        End Sub

        Public ReadOnly Property LogicalProcessorCount As Integer
            Get
                Return 总核心数
            End Get
        End Property

        ''' <summary>返回所有处理器组 (Group, Count) 元组数组的副本。Count 为该组内活动逻辑核心数。</summary>
        Public Function GetGroups() As (Group As UShort, Count As Integer)()
            Return CType(处理器组.Clone(), (Group As UShort, Count As Integer)())
        End Function

        ''' <summary>将组索引映射为全局逻辑核心索引范围 (Start, Count)；索引无效时返回 (0, 0)。</summary>
        Public Function GetGroupRange(groupIndex As Integer) As (Start As Integer, Count As Integer)
            If groupIndex < 0 OrElse groupIndex >= 处理器组.Length Then Return (0, 0)
            Dim start As Integer = 0
            For i As Integer = 0 To groupIndex - 1
                start += 处理器组(i).Count
            Next
            Return (start, 处理器组(groupIndex).Count)
        End Function

        Public Function GetUsages() As Single()
            SyncLock 采样锁
                Return CType(当前占用.Clone(), Single())
            End SyncLock
        End Function

        Private Shared ReadOnly 单项大小 As Integer = Marshal.SizeOf(Of SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION)()
        Private 样本缓冲 As IntPtr = IntPtr.Zero
        Private 样本缓冲大小 As Integer = 0

        Protected Overrides Sub Finalize()
            Try
                If 样本缓冲 <> IntPtr.Zero Then
                    Marshal.FreeHGlobal(样本缓冲)
                    样本缓冲 = IntPtr.Zero
                End If
            Finally
                MyBase.Finalize()
            End Try
        End Sub

        Private Sub 确保缓冲(size As Integer)
            If 样本缓冲 <> IntPtr.Zero AndAlso 样本缓冲大小 >= size Then Return
            If 样本缓冲 <> IntPtr.Zero Then Marshal.FreeHGlobal(样本缓冲)
            样本缓冲 = Marshal.AllocHGlobal(size)
            样本缓冲大小 = size
        End Sub

        Public Sub Sample()
            Dim idleArr(总核心数 - 1) As Long
            Dim totalArr(总核心数 - 1) As Long

            If 处理器组.Length <= 1 Then
                查询当前组(idleArr, totalArr, 0)
            Else
                Dim 索引 As Integer = 0
                For Each g In 处理器组
                    Dim offset As Integer = 索引
                    Dim groupId As UShort = g.Group
                    Dim count As Integer = g.Count
                    Dim t As New Thread(
                        Sub()
                            Try
                                Dim ga As New GROUP_AFFINITY()
                                If count >= IntPtr.Size * 8 Then
                                    ga.Mask = New UIntPtr(UInt64.MaxValue)
                                Else
                                    ga.Mask = New UIntPtr((1UL << count) - 1UL)
                                End If
                                ga.Group = groupId
                                Dim prev As New GROUP_AFFINITY()
                                SetThreadGroupAffinity(GetCurrentThread(), ga, prev)
                                查询当前组(idleArr, totalArr, offset, count)
                            Catch
                            End Try
                        End Sub) With {
                        .IsBackground = True
                        }
                    t.Start()
                    t.Join()
                    索引 += count
                Next
            End If

            SyncLock 采样锁
                For i As Integer = 0 To 总核心数 - 1
                    Dim 总 As Long = totalArr(i)
                    Dim idle As Long = idleArr(i)
                    Dim dTotal As Long = 总 - 上次总计(i)
                    Dim dIdle As Long = idle - 上次Idle(i)
                    If dTotal > 0 Then
                        Dim busy As Single = CSng(1.0 - CDbl(dIdle) / CDbl(dTotal))
                        If busy < 0 Then busy = 0
                        If busy > 1 Then busy = 1
                        当前占用(i) = busy
                    End If
                    上次Idle(i) = idle
                    上次总计(i) = 总
                Next
            End SyncLock
        End Sub

        Private Function 查询当前组(idleArr As Long(), totalArr As Long(), offset As Integer,
                                     Optional expectedCount As Integer = -1) As Integer
            Dim maxCount As Integer = If(expectedCount > 0, expectedCount, idleArr.Length - offset)
            If maxCount <= 0 Then Return 0
            Dim bufSize As Integer = 单项大小 * maxCount
            确保缓冲(bufSize)
            Dim buf As IntPtr = 样本缓冲
            Dim ret As UInteger = 0
            Dim status As UInteger = NtQuerySystemInformation(SystemProcessorPerformanceInformation, buf, CUInt(bufSize), ret)
            If status <> STATUS_SUCCESS Then Return 0
            Dim count As Integer = CInt(ret) \ 单项大小
            If count > maxCount Then count = maxCount
            For i As Integer = 0 To count - 1
                Dim p As IntPtr = IntPtr.Add(buf, i * 单项大小)
                idleArr(offset + i) = Marshal.ReadInt64(p, 0)
                totalArr(offset + i) = Marshal.ReadInt64(p, 8) + Marshal.ReadInt64(p, 16)
            Next
            Return count
        End Function

        Private Shared Function 枚举处理器组() As (Group As UShort, Count As Integer)()
            Try
                Dim len As UInteger = 0
                GetLogicalProcessorInformationEx(RelationGroup, IntPtr.Zero, len)
                If len = 0 Then Return {(CUShort(0), Environment.ProcessorCount)}
                Dim buf As IntPtr = Marshal.AllocHGlobal(CInt(len))
                Try
                    If Not GetLogicalProcessorInformationEx(RelationGroup, buf, len) Then
                        Return {(CUShort(0), Environment.ProcessorCount)}
                    End If
                    Dim ptr As IntPtr = buf
                    Dim ending As IntPtr = IntPtr.Add(buf, CInt(len))
                    Dim result As New List(Of (UShort, Integer))()
                    Do While ptr.ToInt64() < ending.ToInt64()
                        Dim rel As Integer = Marshal.ReadInt32(ptr, 0)
                        Dim size As Integer = Marshal.ReadInt32(ptr, 4)
                        If rel = RelationGroup Then
                            Dim activeGroupCount As UShort = CUShort(Marshal.ReadInt16(ptr, 10))
                            Dim infoStart As Integer = 8 + 4 + 20
                            Dim itemSize As Integer = 1 + 1 + 38 + IntPtr.Size
                            For i As Integer = 0 To activeGroupCount - 1
                                Dim itemPtr As IntPtr = IntPtr.Add(ptr, infoStart + i * itemSize)
                                Dim activeProc As Byte = Marshal.ReadByte(itemPtr, 1)
                                result.Add((CUShort(i), CInt(activeProc)))
                            Next
                        End If
                        If size <= 0 Then Exit Do
                        ptr = IntPtr.Add(ptr, size)
                    Loop
                    If result.Count = 0 Then
                        Return {(CUShort(0), Environment.ProcessorCount)}
                    End If
                    Return result.ToArray()
                Finally
                    Marshal.FreeHGlobal(buf)
                End Try
            Catch
                Return {(CUShort(0), Environment.ProcessorCount)}
            End Try
        End Function
    End Class
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

        Using scope = D2DHelperV2.BeginPaint(e, Me, ssaa)
            If scope Is Nothing Then Return
            _当前合成器 = scope.Compositor
            Try
                Dim gRT As ID2D1RenderTarget = scope.GraphicsLayer
                Dim dcRT As ID2D1DCRenderTarget = scope.DCRenderTarget

                If 圆角半径值 > 0 OrElse MyBase.BackColor.A < 255 Then
                    If _backgroundSource IsNot Nothing Then
                        BackgroundPenetrationV2.PaintBackground(Me, scope, _backgroundSource)
                    End If
                    If MyBase.BackColor.A > 0 AndAlso MyBase.BackColor.A < 255 Then
                        Dim b = _当前合成器.BrushCache.Get(gRT, MyBase.BackColor)
                        gRT.FillRectangle(New Vortice.Mathematics.Rect(0, 0, Me.Width, Me.Height), b)
                    End If
                End If

                绘制图形内容_D2D(gRT)
                scope.FlushGraphics()
                绘制文字内容_D2D(dcRT)

                If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
                    Dim b = _当前合成器.BrushCache.Get(dcRT, 禁用时遮罩颜色)
                    dcRT.FillRectangle(New Vortice.Mathematics.Rect(0, 0, Me.Width, Me.Height), b)
                End If
            Finally
                _当前合成器 = Nothing
            End Try
        End Using
    End Sub

    Private Sub 绘制图形内容_D2D(rt As ID2D1RenderTarget)
        Dim s As Single = DpiScale()
        Dim range = 获取显示范围()
        Dim count As Integer = range.Count
        If count <= 0 Then Return
        Dim startIndex As Integer = range.Start

        Dim simplified As Boolean = 应使用简化模式(count)
        Dim cols As Integer = 计算列数(count, simplified)
        Dim rows As Integer = CInt(Math.Ceiling(count / CDbl(cols)))

        Dim pad As Padding = Me.Padding
        Dim gap As Single = 网格间距值 * s
        Dim cellW As Single = (Math.Max(0, Me.Width - (pad.Left + pad.Right) * s) - gap * (cols - 1)) / cols
        Dim cellH As Single = (Math.Max(0, Me.Height - (pad.Top + pad.Bottom) * s) - gap * (rows - 1)) / rows
        If cellW < 2 OrElse cellH < 2 Then Return

        Dim usages() As Single = 最近占用
        Dim history()() As Single = If(Not simplified, 获取历史快照(), Nothing)

        For slot As Integer = 0 To count - 1
            Dim i As Integer = startIndex + slot
            Dim x As Single = pad.Left * s + (slot Mod cols) * (cellW + gap)
            Dim y As Single = pad.Top * s + (slot \ cols) * (cellH + gap)
            Dim usage As Single = If(usages IsNot Nothing AndAlso i < usages.Length, usages(i), 0)
            Dim hist As Single() = If(history IsNot Nothing AndAlso i < history.Length, history(i), Nothing)
            Dim cellRect As New RectangleF(x, y, cellW, cellH)
            If simplified Then
                绘制简化核心_D2D(rt, cellRect, usage, s)
            Else
                绘制常规核心图形_D2D(rt, cellRect, hist, s)
            End If
        Next
    End Sub

    Private Sub 绘制文字内容_D2D(rt As ID2D1RenderTarget)
        Dim s As Single = DpiScale()
        Dim range = 获取显示范围()
        Dim count As Integer = range.Count
        If count <= 0 Then Return
        Dim startIndex As Integer = range.Start

        Dim simplified As Boolean = 应使用简化模式(count)
        Dim cols As Integer = 计算列数(count, simplified)
        Dim rows As Integer = CInt(Math.Ceiling(count / CDbl(cols)))

        Dim pad As Padding = Me.Padding
        Dim gap As Single = 网格间距值 * s
        Dim cellW As Single = (Math.Max(0, Me.Width - (pad.Left + pad.Right) * s) - gap * (cols - 1)) / cols
        Dim cellH As Single = (Math.Max(0, Me.Height - (pad.Top + pad.Bottom) * s) - gap * (rows - 1)) / rows
        If cellW < 2 OrElse cellH < 2 Then Return

        Dim usages() As Single = 最近占用
        For slot As Integer = 0 To count - 1
            Dim i As Integer = startIndex + slot
            Dim x As Single = pad.Left * s + (slot Mod cols) * (cellW + gap)
            Dim y As Single = pad.Top * s + (slot \ cols) * (cellH + gap)
            Dim usage As Single = If(usages IsNot Nothing AndAlso i < usages.Length, usages(i), 0)
            Dim cellRect As New RectangleF(x, y, cellW, cellH)
            If simplified Then
                绘制简化核心文字_D2D(rt, cellRect, usage, s)
            Else
                绘制常规核心文字_D2D(rt, cellRect, i, usage, s)
            End If
        Next
    End Sub

    ''' <summary>返回当前是否应按简化样式绘制（用户显式启用 SimplifiedMode，或显示核心数超过 NormalMaxCores 阈值）。</summary>
    Private Function 应使用简化模式(count As Integer) As Boolean
        If 简化模式值 Then Return True
        If 常规最大核心数值 > 0 AndAlso count > 常规最大核心数值 Then Return True
        Return False
    End Function

    ''' <summary>根据 DisplayedGroup 属性计算当前应显示的全局逻辑核心索引范围。DisplayedGroup 为 -1 表示全部。</summary>
    Private Function 获取显示范围() As (Start As Integer, Count As Integer)
        Dim total As Integer = 逻辑核心数
        If 显示处理器组值 < 0 OrElse 采样器实例 Is Nothing Then Return (0, total)
        Dim r = 采样器实例.GetGroupRange(显示处理器组值)
        If r.Count <= 0 Then Return (0, total)
        Return r
    End Function

    Private Sub 绘制常规核心图形_D2D(rt As ID2D1RenderTarget, rect As RectangleF, hist As Single(), s As Single)
        Dim inner As RectangleF = 计算内部矩形(rect, s)

        绘制核心背景与边框_D2D(rt, rect, s)
        If inner.Width <= 0 OrElse inner.Height <= 0 Then Return

        ' 计算文字条高度以便历史图表避让
        Dim tp As Padding = 文字内边距值
        Dim textStripH As Single = 0
        If 文字模式 <> TextModeEnum.None Then
            textStripH = Math.Min(inner.Height, 文本像素高度(s) + (tp.Top + tp.Bottom) * s)
        End If

        ' 垂直底部对齐 → 文字位于底部；其余 → 置于顶部
        Dim textAtBottom As Boolean = (文字对齐值 = ContentAlignment.BottomLeft OrElse
                                        文字对齐值 = ContentAlignment.BottomCenter OrElse
                                        文字对齐值 = ContentAlignment.BottomRight)

        Dim graphH As Single = Math.Max(0, inner.Height - textStripH)
        Dim graphY As Single = If(textAtBottom, inner.Y, inner.Y + textStripH)

        If 显示历史图表 AndAlso hist IsNot Nothing AndAlso hist.Length >= 2 AndAlso graphH >= 2 Then
            使用核心裁剪_D2D(rt, rect, s,
                Sub()
                    绘制历史图表_D2D(rt, New RectangleF(inner.X, graphY, inner.Width, graphH), hist, s)
                End Sub)
        End If
    End Sub

    Private Sub 绘制常规核心文字_D2D(rt As ID2D1RenderTarget, rect As RectangleF, index As Integer, usage As Single, s As Single)
        Dim inner As RectangleF = 计算内部矩形(rect, s)
        If inner.Width <= 0 OrElse inner.Height <= 0 Then Return

        Dim 文本 As String = 构造核心文字(index, usage)
        If String.IsNullOrEmpty(文本) Then Return

        Dim tp As Padding = 文字内边距值
        Dim textStripH As Single = Math.Min(inner.Height, 文本像素高度(s) + (tp.Top + tp.Bottom) * s)
        Dim textAtBottom As Boolean = (文字对齐值 = ContentAlignment.BottomLeft OrElse
                                        文字对齐值 = ContentAlignment.BottomCenter OrElse
                                        文字对齐值 = ContentAlignment.BottomRight)
        Dim textY As Single = If(textAtBottom, inner.Bottom - textStripH, inner.Y)
        绘制文字_D2D(rt, New RectangleF(inner.X, textY, inner.Width, textStripH), 文本, s)
    End Sub

    Private Sub 绘制简化核心_D2D(rt As ID2D1RenderTarget, rect As RectangleF, usage As Single, s As Single)
        Dim inner As RectangleF = 计算内部矩形(rect, s)

        绘制核心背景与边框_D2D(rt, rect, s)
        If inner.Width > 0 AndAlso inner.Height > 0 Then
            Dim fillH As Single = inner.Height * Math.Max(0.0F, Math.Min(1.0F, usage))
            If fillH > 0 Then
                使用核心裁剪_D2D(rt, rect, s,
                    Sub()
                        Dim fillRect As New RectangleF(inner.X, inner.Bottom - fillH, inner.Width, fillH)
                        Using b = rt.CreateSolidColorBrush(D2DHelper.ToColor4(选择占用颜色(usage)))
                            rt.FillRectangle(D2DHelper.ToD2DRect(fillRect), b)
                        End Using
                    End Sub)
            End If
        End If
    End Sub

    Private Sub 绘制简化核心文字_D2D(rt As ID2D1RenderTarget, rect As RectangleF, usage As Single, s As Single)
        Dim inner As RectangleF = 计算内部矩形(rect, s)
        If inner.Width > 0 AndAlso inner.Height > 0 Then
            绘制文字_D2D(rt, inner, CInt(Math.Round(usage * 100)).ToString() & "%", s)
        End If
    End Sub

    ''' <summary>根据占用率选择前景色。</summary>
    Private Function 选择占用颜色(usage As Single) As Color
        If usage >= 占满阈值值 Then Return 占满颜色值
        Return 当前条颜色值
    End Function

    ''' <summary>计算核心内部可绘制区域：同时考虑 CellPadding 与边框厚度，确保内容不会覆盖边框。</summary>
    Private Function 计算内部矩形(rect As RectangleF, s As Single) As RectangleF
        Dim pad As Single = Math.Max(核心内边距值, 核心边框粗细值) * s
        Return New RectangleF(rect.X + pad, rect.Y + pad,
                              Math.Max(0, rect.Width - pad * 2),
                              Math.Max(0, rect.Height - pad * 2))
    End Function

    Private Sub 绘制文字_D2D(rt As ID2D1RenderTarget, inner As RectangleF, text As String, s As Single)
        Dim tp As Padding = 文字内边距值
        Dim textRect As New RectangleF(
            inner.X + tp.Left * s,
            inner.Y + tp.Top * s,
            Math.Max(0, inner.Width - (tp.Left + tp.Right) * s),
            Math.Max(0, inner.Height - (tp.Top + tp.Bottom) * s))
        If textRect.Width <= 0 OrElse textRect.Height <= 0 Then Return

        Dim weight As FontWeight = If(Me.Font.Bold, FontWeight.Bold, FontWeight.Normal)
        Dim style As FontStyle = If(Me.Font.Italic, FontStyle.Italic, FontStyle.Normal)
        Dim fmt = _当前合成器.TextFormatCache.Get(Me.Font.FontFamily.Name, weight, style, 文本像素高度(s),
                                       转文本水平对齐(文字对齐值), 转文本垂直对齐(文字对齐值), True)
        Using layout = D2DHelper.GetDWriteFactory().CreateTextLayout(If(text, ""), fmt, textRect.Width, textRect.Height)
            Using b = rt.CreateSolidColorBrush(D2DHelper.ToColor4(Me.ForeColor))
                rt.DrawTextLayout(New Vector2(textRect.X, textRect.Y), layout, b)
            End Using
        End Using
    End Sub

    Private Sub 绘制核心背景与边框_D2D(rt As ID2D1RenderTarget, rect As RectangleF, s As Single)
        Dim bw As Single = 核心边框粗细值 * s
        Dim r As Single = 圆角半径值 * s
        Dim hasBorder As Boolean = 核心边框颜色值.A > 0 AndAlso bw > 0

        If 核心背景颜色值.A > 0 Then
            If r > 0 Then
                Using geo = RectangleRenderer.创建圆角矩形几何(rect, r)
                    RectangleRenderer.绘制圆角背景_D2D(rt, geo, rect, 核心背景颜色值, Color.Empty, System.Windows.Forms.Orientation.Vertical)
                End Using
            Else
                RectangleRenderer.绘制矩形背景_D2D(rt, rect, 核心背景颜色值, Color.Empty, System.Windows.Forms.Orientation.Vertical)
            End If
        End If

        If hasBorder Then
            Dim half As Single = bw * 0.5F
            Dim bRect As RectangleF = RectangleF.Inflate(rect, -half, -half)
            Using b = rt.CreateSolidColorBrush(D2DHelper.ToColor4(核心边框颜色值))
                If r > 0 Then
                    Using geo = RectangleRenderer.创建圆角矩形几何(bRect, Math.Max(0, r - half))
                        rt.DrawGeometry(geo, b, bw)
                    End Using
                Else
                    rt.DrawRectangle(D2DHelper.ToD2DRect(bRect), b, bw)
                End If
            End Using
        End If
    End Sub

    Private Sub 使用核心裁剪_D2D(rt As ID2D1RenderTarget, rect As RectangleF, s As Single, drawAction As Action)
        If drawAction Is Nothing Then Return
        If 圆角半径值 <= 0 Then
            drawAction()
            Return
        End If

        Using geo = RectangleRenderer.创建圆角矩形几何(rect, 圆角半径值 * s)
            D2DHelper.PushGeometryClip(rt, geo, rect)
            Try
                drawAction()
            Finally
                rt.PopLayer()
            End Try
        End Using
    End Sub

    Private Sub 绘制历史图表_D2D(rt As ID2D1RenderTarget, rect As RectangleF, hist As Single(), s As Single)
        If rect.Width < 2 OrElse rect.Height < 2 Then Return
        Dim n As Integer = hist.Length
        If n < 2 Then Return

        If 图表背景颜色值.A > 0 Then
            Using b = rt.CreateSolidColorBrush(D2DHelper.ToColor4(图表背景颜色值))
                rt.FillRectangle(D2DHelper.ToD2DRect(rect), b)
            End Using
        End If

        Dim step_ As Single = rect.Width / (n - 1)
        Dim pts(n - 1) As Vector2
        For i As Integer = 0 To n - 1
            Dim v As Single = hist(i)
            If v < 0 Then v = 0
            If v > 1 Then v = 1
            pts(i) = New Vector2(rect.X + i * step_, rect.Bottom - v * rect.Height)
        Next

        If 图表填充颜色值.A > 0 Then
            Using geo = 创建历史填充几何(rect, pts)
                Using b = rt.CreateSolidColorBrush(D2DHelper.ToColor4(图表填充颜色值))
                    rt.FillGeometry(geo, b)
                End Using
            End Using
        End If

        Dim lineW As Single = 图表线条粗细值 * s
        Using b = rt.CreateSolidColorBrush(D2DHelper.ToColor4(图表线条颜色值))
            For i As Integer = 0 To n - 2
                rt.DrawLine(pts(i), pts(i + 1), b, lineW)
            Next
        End Using

        Using b = rt.CreateSolidColorBrush(D2DHelper.ToColor4(占满颜色值))
            Dim tickW As Single = Math.Max(lineW, 1.5F * s)
            For i As Integer = 0 To n - 1
                If 历史满载(hist(i)) Then
                    rt.DrawLine(New Vector2(pts(i).X, pts(i).Y), New Vector2(pts(i).X, rect.Bottom), b, tickW)
                End If
            Next
        End Using
    End Sub

    Private Shared Function 创建历史填充几何(rect As RectangleF, pts As Vector2()) As ID2D1PathGeometry
        Dim geo As ID2D1PathGeometry = D2DHelper.GetD2DFactory().CreatePathGeometry()
        Dim sink As ID2D1GeometrySink = geo.Open()
        Try
            sink.BeginFigure(pts(0), FigureBegin.Filled)
            For i As Integer = 1 To pts.Length - 1
                sink.AddLine(pts(i))
            Next
            sink.AddLine(New Vector2(rect.Right, rect.Bottom))
            sink.AddLine(New Vector2(rect.X, rect.Bottom))
            sink.EndFigure(FigureEnd.Closed)
            sink.Close()
        Finally
            sink.Dispose()
        End Try
        Return geo
    End Function

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

    Private Shared Function 历史满载(usage As Single) As Boolean
        Return usage >= 0.99F
    End Function

    Private Function 构造核心文字(index As Integer, usage As Single) As String
        Select Case 文字模式
            Case TextModeEnum.None
                Return String.Empty
            Case TextModeEnum.IndexOnly
                Return "CPU " & index
            Case TextModeEnum.PercentOnly
                Return CInt(Math.Round(usage * 100)).ToString() & "%"
            Case Else ' IndexAndPercent
                Return "CPU " & index & " - " & CInt(Math.Round(usage * 100)).ToString() & "%"
        End Select
    End Function

    Private Function 计算列数(count As Integer, simplified As Boolean) As Integer
        Dim minC As Integer
        Dim maxC As Integer
        If simplified Then
            minC = 简化最小列数值
            maxC = 简化最大列数值
        Else
            minC = 常规最小列数值
            maxC = 常规最大列数值
        End If
        Dim auto As Integer = CInt(Math.Ceiling(Math.Sqrt(count)))
        If minC > 0 AndAlso auto < minC Then auto = minC
        If maxC > 0 AndAlso auto > maxC Then auto = maxC
        Return Math.Max(1, Math.Min(auto, count))
    End Function

#End Region

#Region "采样与数据"
    Private 采样器实例 As CpuSampler
    Private ReadOnly 采样定时器 As New System.Windows.Forms.Timer() With {.Interval = 1000}
    Private ReadOnly 历史数据 As New List(Of Single())()
    Private ReadOnly 历史锁 As New Object()
    Private 最近占用 As Single() = Array.Empty(Of Single)()

    Private ReadOnly Property 逻辑核心数 As Integer
        Get
            If 采样器实例 IsNot Nothing Then Return 采样器实例.LogicalProcessorCount
            Return Environment.ProcessorCount
        End Get
    End Property

    Private Sub 确保采样器()
        If 采样器实例 Is Nothing Then
            Try
                采样器实例 = New CpuSampler()
            Catch
                采样器实例 = Nothing
            End Try
            SyncLock 历史锁
                历史数据.Clear()
                For i As Integer = 0 To 逻辑核心数 - 1
#Disable Warning CA1861 ' 不要将常量数组作为参数
                    历史数据.Add(New Single(记录长度值 - 1) {})
#Enable Warning CA1861 ' 不要将常量数组作为参数
                Next
            End SyncLock
            最近占用 = New Single(逻辑核心数 - 1) {}
            If 采样器实例 IsNot Nothing Then
                RaiseEvent SamplerReady(Me, EventArgs.Empty)
            End If
        End If
    End Sub

    Private Sub 采样定时器_Tick(sender As Object, e As EventArgs)
        If DesignMode Then Return
        If 采样器实例 Is Nothing Then Return
        Try
            采样器实例.Sample()
        Catch
            Return
        End Try

        Dim usages() As Single = 采样器实例.GetUsages()
        最近占用 = usages

        ' 简化模式不记录历史
        If Not 简化模式值 AndAlso 启用历史记录 Then
            SyncLock 历史锁
                For i As Integer = 0 To Math.Min(usages.Length, 历史数据.Count) - 1
                    Dim arr = 历史数据(i)
                    If arr Is Nothing OrElse arr.Length <> 记录长度值 Then
                        arr = New Single(记录长度值 - 1) {}
                        历史数据(i) = arr
                    End If
                    If arr.Length > 1 Then
                        Array.Copy(arr, 1, arr, 0, arr.Length - 1)
                    End If
                    arr(arr.Length - 1) = usages(i)
                Next
            End SyncLock
        End If

        Me.Invalidate()
        RaiseEvent SampleUpdated(Me, EventArgs.Empty)
    End Sub

    Private Function 获取历史快照() As Single()()
        Dim n As Integer = 逻辑核心数
        Dim result(n - 1)() As Single
        SyncLock 历史锁
            For i As Integer = 0 To n - 1
                If Not 简化模式值 AndAlso 启用历史记录 AndAlso i < 历史数据.Count Then
                    ' 采样定时器 Tick 与 OnPaint 均运行在 UI 线程，无需克隆；
                    ' 仅交出只读引用以避免大核数下重复分配 Single() 数组。
                    result(i) = 历史数据(i)
                Else
                    result(i) = Nothing
                End If
            Next
        End SyncLock
        Return result
    End Function

    ''' <summary>手动立即执行一次采样（不等待定时器）。</summary>
    Public Sub ForceSample()
        If 采样器实例 Is Nothing Then Return
        Try
            采样器实例.Sample()
            最近占用 = 采样器实例.GetUsages()
            Me.Invalidate()
        Catch
        End Try
    End Sub

    ''' <summary>清除全部历史记录数据。</summary>
    Public Sub Reset()
        SyncLock 历史锁
            For i As Integer = 0 To 历史数据.Count - 1
                历史数据(i) = New Single(记录长度值 - 1) {}
            Next
        End SyncLock
        Me.Invalidate()
    End Sub

    ''' <summary>获取当前每核心占用（0.0~1.0）的只读快照。</summary>
    <Browsable(False)>
    Public ReadOnly Property CurrentUsages As Single()
        Get
            Return CType(最近占用.Clone(), Single())
        End Get
    End Property

    ''' <summary>处理器组总数（物理 CPU / NUMA 结构上的独立调度组）。</summary>
    <Browsable(False)>
    Public ReadOnly Property ProcessorGroupCount As Integer
        Get
            If 采样器实例 Is Nothing Then Return 1
            Return 采样器实例.GetGroups().Length
        End Get
    End Property

    ''' <summary>获取指定处理器组的全局逻辑核心索引范围 (Start, Count)。组索引越界时返回 (0, 0)。</summary>
    Public Function GetProcessorGroupRange(groupIndex As Integer) As (Start As Integer, Count As Integer)
        If 采样器实例 Is Nothing Then Return (0, 0)
        Return 采样器实例.GetGroupRange(groupIndex)
    End Function

    ''' <summary>计算当前布局下单个 cell 的实际高度（像素，含 DPI 缩放）。控件尺寸不足时返回 0。</summary>
    Private Function 计算Cell高度() As Single
        Dim count As Integer = 获取显示范围().Count
        If count <= 0 Then Return 0
        Dim s As Single = DpiScale()
        Dim cols As Integer = 计算列数(count, 应使用简化模式(count))
        Dim rows As Integer = CInt(Math.Ceiling(count / CDbl(cols)))
        Dim pad As Padding = Me.Padding
        Dim gap As Single = 网格间距值 * s
        Dim h As Single = (Math.Max(0, Me.Height - (pad.Top + pad.Bottom) * s) - gap * (rows - 1)) / rows
        Return If(h < 2, 0, h)
    End Function

    ''' <summary>返回第一行 cell 的实际渲染高度（像素，含 DPI 缩放）。控件尺寸不足时返回 0。</summary>
    Public Function GetFirstRowCellHeight() As Single
        Return 计算Cell高度()
    End Function

    ''' <summary>返回最后一行 cell 的实际渲染高度（像素，含 DPI 缩放）。控件尺寸不足时返回 0。</summary>
    Public Function GetLastRowCellHeight() As Single
        Return 计算Cell高度()
    End Function

    ''' <summary>返回指定全局逻辑核心索引对应的处理器名称，读自注册表 HKLM\HARDWARE\DESCRIPTION\System\CentralProcessor\{index}。</summary>
    Public Function GetProcessorName(globalLogicalIndex As Integer) As String
        Dim names = 确保处理器名称缓存()
        If names Is Nothing OrElse globalLogicalIndex < 0 OrElse globalLogicalIndex >= names.Length Then Return String.Empty
        Return If(names(globalLogicalIndex), String.Empty)
    End Function

    ''' <summary>返回指定处理器组对应的 CPU 名称（以该组内第一个逻辑核心的名称为准）。groupIndex = -1 时返回第一个物理 CPU 的名称。</summary>
    Public Function GetProcessorGroupName(groupIndex As Integer) As String
        If groupIndex < 0 Then Return GetProcessorName(0)
        Dim r = GetProcessorGroupRange(groupIndex)
        If r.Count <= 0 Then Return String.Empty
        Return GetProcessorName(r.Start)
    End Function

    ''' <summary>返回系统中所有物理 CPU 的名称列表（对每个处理器组按起始逻辑核心取名，保留顺序、不去重）。</summary>
    <Browsable(False)>
    Public ReadOnly Property ProcessorGroupNames As String()
        Get
            Dim n As Integer = ProcessorGroupCount
            Dim arr(n - 1) As String
            For i As Integer = 0 To n - 1
                arr(i) = GetProcessorGroupName(i)
            Next
            Return arr
        End Get
    End Property

    ''' <summary>返回系统中唯一的物理 CPU 名称集合（适合多插槽同型号审视）。</summary>
    <Browsable(False)>
    Public ReadOnly Property DistinctProcessorNames As String()
        Get
            Return ProcessorGroupNames.
                Where(Function(x) Not String.IsNullOrEmpty(x)).
                Distinct(StringComparer.OrdinalIgnoreCase).
                ToArray()
        End Get
    End Property

    ''' <summary>返回当前显示的处理器组的 CPU 名称；若 DisplayedGroup = -1则返回第一个物理 CPU 名称。</summary>
    <Browsable(False)>
    Public ReadOnly Property CurrentProcessorName As String
        Get
            Return GetProcessorGroupName(显示处理器组值)
        End Get
    End Property

    Private 处理器名称缓存 As String()
    Private Function 确保处理器名称缓存() As String()
        If 处理器名称缓存 IsNot Nothing Then Return 处理器名称缓存
        处理器名称缓存 = 读取处理器名称()
        Return 处理器名称缓存
    End Function

    Private Function 读取处理器名称() As String()
        Dim total As Integer = 逻辑核心数
        Dim result(Math.Max(0, total - 1)) As String
        Try
            Using root = Registry.LocalMachine.OpenSubKey("HARDWARE\DESCRIPTION\System\CentralProcessor", False)
                If root Is Nothing Then Return result
                For Each keyName As String In root.GetSubKeyNames()
                    Dim idx As Integer
                    If Not Integer.TryParse(keyName, idx) Then Continue For
                    If idx < 0 OrElse idx >= result.Length Then Continue For
                    Using sub_ = root.OpenSubKey(keyName, False)
                        If sub_ Is Nothing Then Continue For
                        Dim s = TryCast(sub_.GetValue("ProcessorNameString"), String)
                        If s IsNot Nothing Then result(idx) = s.Trim()
                    End Using
                Next
            End Using
        Catch
        End Try
        Return result
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

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        If Not DesignMode Then
            确保采样器()
            If 正在运行 Then 采样定时器.Start()
        End If
    End Sub

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        采样定时器.Stop()
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

    Protected Overrides Sub OnBackColorChanged(e As EventArgs)
        MyBase.OnBackColorChanged(e)
        Me.Invalidate()
    End Sub
#End Region

#Region "属性"
    Public Enum TextModeEnum
        ''' <summary>不显示文字</summary>
        None
        ''' <summary>仅显示核心编号</summary>
        IndexOnly
        ''' <summary>仅显示百分比</summary>
        PercentOnly
        ''' <summary>显示编号与百分比</summary>
        IndexAndPercent
    End Enum

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

    ' ====== 简化模式 ======
    Private 简化模式值 As Boolean = False
    <Category("LakeUI"), Description("简化模式：仅显示居中百分比和从下向上的占用填充，不显示核心名称和历史图表，切换时自动清除历史记录。"), DefaultValue(False), Browsable(True)>
    Public Property SimplifiedMode As Boolean
        Get
            Return 简化模式值
        End Get
        Set(value As Boolean)
            If value = 简化模式值 Then Return
            简化模式值 = value
            If value Then Reset()
            Me.Invalidate()
        End Set
    End Property

    ' ====== 多处理器组 ======
    Private 显示处理器组值 As Integer = -1
    <Category("LakeUI"), Description("指定要显示的处理器组（物理 CPU / NUMA 调度组）索引；-1 = 显示全部核心。双路 Threadripper 等多 CPU 系统可用它在 0 / 1 间切换。"), DefaultValue(-1), Browsable(True)>
    Public Property DisplayedGroup As Integer
        Get
            Return 显示处理器组值
        End Get
        Set(value As Integer)
            If value < -1 Then value = -1
            If value = 显示处理器组值 Then Return
            显示处理器组值 = value
            Me.Invalidate()
        End Set
    End Property

    ' ====== 采样 ======
    Private 采样间隔值 As Integer = 1000
    <Category("LakeUI"), Description("采样间隔 (毫秒)，与任务管理器一致默认为 1000。"), DefaultValue(1000), Browsable(True)>
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

    ' ====== 布局 - 常规模式列数范围 ======
    Private 常规最小列数值 As Integer = 0
    <Category("LakeUI"), Description("常规模式每行最小列数；0 = 不限。"), DefaultValue(0), Browsable(True)>
    Public Property NormalMinColumns As Integer
        Get
            Return 常规最小列数值
        End Get
        Set(value As Integer)
            SetValue(常规最小列数值, Math.Max(0, value))
        End Set
    End Property

    Private 常规最大列数值 As Integer = 0
    <Category("LakeUI"), Description("常规模式每行最大列数；0 = 不限。"), DefaultValue(0), Browsable(True)>
    Public Property NormalMaxColumns As Integer
        Get
            Return 常规最大列数值
        End Get
        Set(value As Integer)
            SetValue(常规最大列数值, Math.Max(0, value))
        End Set
    End Property

    ' ====== 布局 - 简化模式列数范围 ======
    Private 简化最小列数值 As Integer = 0
    <Category("LakeUI"), Description("简化模式每行最小列数；0 = 不限。"), DefaultValue(0), Browsable(True)>
    Public Property SimplifiedMinColumns As Integer
        Get
            Return 简化最小列数值
        End Get
        Set(value As Integer)
            SetValue(简化最小列数值, Math.Max(0, value))
        End Set
    End Property

    Private 简化最大列数值 As Integer = 0
    <Category("LakeUI"), Description("简化模式每行最大列数；0 = 不限。"), DefaultValue(0), Browsable(True)>
    Public Property SimplifiedMaxColumns As Integer
        Get
            Return 简化最大列数值
        End Get
        Set(value As Integer)
            SetValue(简化最大列数值, Math.Max(0, value))
        End Set
    End Property

    ' ====== 布局 - 常规模式核心数上限（自动降级） ======
    Private 常规最大核心数值 As Integer = -1
    <Category("LakeUI"), Description("常规模式允许显示的最大核心数；当前显示核心数超过此值时，自动按简化样式绘制（不修改 SimplifiedMode 属性值，也不清除历史记录）。-1 = 关闭（始终遵循 SimplifiedMode）。例如设为 20 时，显示核心数 ≥ 21 即自动简化。"), DefaultValue(-1), Browsable(True)>
    Public Property NormalMaxCores As Integer
        Get
            Return 常规最大核心数值
        End Get
        Set(value As Integer)
            If value < -1 Then value = -1
            SetValue(常规最大核心数值, value)
        End Set
    End Property

    ' ====== 布局 - 其他 ======
    Private 网格间距值 As Single = 10
    <Category("LakeUI"), Description("各核心之间的间距。"), DefaultValue(10.0F), Browsable(True)>
    Public Property CellSpacing As Single
        Get
            Return 网格间距值
        End Get
        Set(value As Single)
            SetValue(网格间距值, Math.Max(0, value))
        End Set
    End Property

    Private 核心内边距值 As Single = 4
    <Category("LakeUI"), Description("每个核心内部的内边距。"), DefaultValue(4.0F), Browsable(True)>
    Public Property CellPadding As Single
        Get
            Return 核心内边距值
        End Get
        Set(value As Single)
            SetValue(核心内边距值, Math.Max(0, value))
        End Set
    End Property

    Private 圆角半径值 As Single = 0
    <Category("LakeUI"), Description("每个核心区域的圆角半径，0 = 直角。"), DefaultValue(0.0F), Browsable(True)>
    Public Property CellCornerRadius As Single
        Get
            Return 圆角半径值
        End Get
        Set(value As Single)
            SetValue(圆角半径值, Math.Max(0, value))
        End Set
    End Property

    ' ====== 颜色 ======
    Private 核心背景颜色值 As Color = Color.FromArgb(30, 30, 30)
    <Category("LakeUI"), Description("单个核心区域背景颜色。"), DefaultValue(GetType(Color), "30, 30, 30"), Browsable(True)>
    Public Property CellBackColor As Color
        Get
            Return 核心背景颜色值
        End Get
        Set(value As Color)
            SetValue(核心背景颜色值, value)
        End Set
    End Property

    Private 核心边框颜色值 As Color = Color.FromArgb(60, 60, 60)
    <Category("LakeUI"), Description("单个核心区域边框颜色。"), DefaultValue(GetType(Color), "60, 60, 60"), Browsable(True)>
    Public Property CellBorderColor As Color
        Get
            Return 核心边框颜色值
        End Get
        Set(value As Color)
            SetValue(核心边框颜色值, value)
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

    Private 核心边框粗细值 As Single = 1
    <Category("LakeUI"), Description("单个核心区域边框粗细。"), DefaultValue(1.0F), Browsable(True)>
    Public Property CellBorderThickness As Single
        Get
            Return 核心边框粗细值
        End Get
        Set(value As Single)
            SetValue(核心边框粗细值, Math.Max(0, value))
        End Set
    End Property

    Private 文字模式 As TextModeEnum = TextModeEnum.IndexAndPercent
    <Category("LakeUI"), Description("常规模式的核心文字内容模式。"), DefaultValue(GetType(TextModeEnum), "IndexAndPercent"), Browsable(True)>
    Public Property TextMode As TextModeEnum
        Get
            Return 文字模式
        End Get
        Set(value As TextModeEnum)
            SetValue(文字模式, value)
        End Set
    End Property

    Private 文字内边距值 As New Padding(2)
    <Category("LakeUI"), Description("核心内文字区域的内边距。"), Browsable(True)>
    Public Property TextPadding As Padding
        Get
            Return 文字内边距值
        End Get
        Set(value As Padding)
            SetValue(文字内边距值, value)
        End Set
    End Property

    Private Function ShouldSerializeTextPadding() As Boolean
        Return 文字内边距值 <> New Padding(2)
    End Function
    Private Sub ResetTextPadding()
        TextPadding = New Padding(2)
    End Sub

    Private 文字对齐值 As ContentAlignment = ContentAlignment.TopCenter
    <Category("LakeUI"), Description("文字对齐方式；常规模式默认顶部居中，简化模式建议设为 MiddleCenter。"), DefaultValue(GetType(ContentAlignment), "TopCenter"), Browsable(True)>
    Public Property TextAlign As ContentAlignment
        Get
            Return 文字对齐值
        End Get
        Set(value As ContentAlignment)
            SetValue(文字对齐值, value)
        End Set
    End Property

    Private 当前条颜色值 As Color = Color.FromArgb(0, 120, 215)
    <Category("LakeUI"), Description("占用前景色（简化模式填充色，常规模式达到阈值前的占用色参考）。"), DefaultValue(GetType(Color), "0, 120, 215"), Browsable(True)>
    Public Property CurrentBarColor As Color
        Get
            Return 当前条颜色值
        End Get
        Set(value As Color)
            SetValue(当前条颜色值, value)
        End Set
    End Property

    Private 占满颜色值 As Color = Color.FromArgb(215, 60, 60)
    <Category("LakeUI"), Description("占用达到阈值后的特殊颜色（占用条 / 简化模式填充）。"), DefaultValue(GetType(Color), "215, 60, 60"), Browsable(True)>
    Public Property FullUsageColor As Color
        Get
            Return 占满颜色值
        End Get
        Set(value As Color)
            SetValue(占满颜色值, value)
        End Set
    End Property

    Private 占满阈值值 As Single = 0.99F
    <Category("LakeUI"), Description("切换到 FullUsageColor 的占用阈值（0.0~1.0）。"), DefaultValue(0.99F), Browsable(True)>
    Public Property FullUsageThreshold As Single
        Get
            Return 占满阈值值
        End Get
        Set(value As Single)
            SetValue(占满阈值值, Math.Max(0.0F, Math.Min(1.0F, value)))
        End Set
    End Property

    ' ====== 历史图表（仅常规模式）======
    Private 启用历史记录 As Boolean = True
    <Category("LakeUI"), Description("常规模式是否启用每核心的历史记录。"), DefaultValue(True), Browsable(True)>
    Public Property EnableHistory As Boolean
        Get
            Return 启用历史记录
        End Get
        Set(value As Boolean)
            启用历史记录 = value
            Me.Invalidate()
        End Set
    End Property

    Private 显示历史图表 As Boolean = True
    <Category("LakeUI"), Description("常规模式是否在每个核心中绘制历史占用图表。"), DefaultValue(True), Browsable(True)>
    Public Property ShowHistoryGraph As Boolean
        Get
            Return 显示历史图表
        End Get
        Set(value As Boolean)
            SetValue(显示历史图表, value)
        End Set
    End Property

    Private 记录长度值 As Integer = 60
    <Category("LakeUI"), Description("每核心历史记录长度（样本数，默认 60 ≈ 1 分钟）。"), DefaultValue(60), Browsable(True)>
    Public Property HistoryLength As Integer
        Get
            Return 记录长度值
        End Get
        Set(value As Integer)
            value = Math.Max(2, value)
            If value = 记录长度值 Then Return
            记录长度值 = value
            SyncLock 历史锁
                For i As Integer = 0 To 历史数据.Count - 1
                    Dim newArr(value - 1) As Single
                    Dim old = 历史数据(i)
                    If old IsNot Nothing Then
                        Dim copy As Integer = Math.Min(old.Length, value)
                        Array.Copy(old, old.Length - copy, newArr, value - copy, copy)
                    End If
                    历史数据(i) = newArr
                Next
            End SyncLock
            Me.Invalidate()
        End Set
    End Property

    Private 图表背景颜色值 As Color = Color.Empty
    <Category("LakeUI"), Description("历史图表背景色；Empty 时使用核心区域背景色。"), DefaultValue(GetType(Color), ""), Browsable(True)>
    Public Property GraphBackColor As Color
        Get
            Return 图表背景颜色值
        End Get
        Set(value As Color)
            SetValue(图表背景颜色值, value)
        End Set
    End Property

    Private 图表线条颜色值 As Color = Color.FromArgb(0, 200, 255)
    <Category("LakeUI"), Description("历史图表折线颜色。"), DefaultValue(GetType(Color), "0, 200, 255"), Browsable(True)>
    Public Property GraphLineColor As Color
        Get
            Return 图表线条颜色值
        End Get
        Set(value As Color)
            SetValue(图表线条颜色值, value)
        End Set
    End Property

    Private 图表线条粗细值 As Single = 1
    <Category("LakeUI"), Description("历史图表折线粗细。"), DefaultValue(1.0F), Browsable(True)>
    Public Property GraphLineThickness As Single
        Get
            Return 图表线条粗细值
        End Get
        Set(value As Single)
            SetValue(图表线条粗细值, Math.Max(0.1F, value))
        End Set
    End Property

    Private 图表填充颜色值 As Color = Color.FromArgb(80, 0, 120, 215)
    <Category("LakeUI"), Description("历史图表区域填充色，Alpha=0 时关闭填充。"), DefaultValue(GetType(Color), "80, 0, 120, 215"), Browsable(True)>
    Public Property GraphFillColor As Color
        Get
            Return 图表填充颜色值
        End Get
        Set(value As Color)
            SetValue(图表填充颜色值, value)
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
    Public Shadows Property AutoScrollMargin As Size
        Get
            Return Nothing
        End Get
        Set(value As Size)
        End Set
    End Property
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Shadows Property AutoScrollMinSize As Size
        Get
            Return Nothing
        End Get
        Set(value As Size)
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
    Public Shadows Property AutoSizeMode As AutoSizeMode
        Get
            Return Nothing
        End Get
        Set(value As AutoSizeMode)
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
