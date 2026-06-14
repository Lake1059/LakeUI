Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Drawing.Imaging
Imports Vortice.Direct2D1

''' <summary>
''' 继承 Panel 的现代化面板控件，支持自定义滚动条、边框圆角和 AutoSize。
''' 可在设计器中像原版 Panel 一样直接添加子控件，子控件自动获得高 DPI 缩放支持。
''' </summary>
<Docking(DockingBehavior.Ask)>
<DefaultEvent("Scroll")>
Public Class ModernPanel

    ''' <summary>
    ''' 滚动策略枚举。
    ''' </summary>
    Public Enum ScrollMode
        ''' <summary>不显示滚动条。</summary>
        None = 0
        ''' <summary>仅垂直滚动。</summary>
        Vertical = 1
        ''' <summary>仅水平滚动。</summary>
        Horizontal = 2
        ''' <summary>同时支持垂直和水平滚动。</summary>
        Both = 3
    End Enum

    ''' <summary>
    ''' 背景图片填充模式枚举。
    ''' </summary>
    Public Enum ImageFillMode
        ''' <summary>保持比例缩放，完整显示图片，可能留有空白（信箱模式）。</summary>
        Zoom = 0
        ''' <summary>保持比例缩放，以图片中心为基准撑满控件，超出部分裁切。</summary>
        Fill = 1
    End Enum

    ''' <summary>
    ''' 子控件排布模式。
    ''' </summary>
    Public Enum LayoutModeEnum
        ''' <summary>绝对定位：使用子控件自身 Location，等同于原版 Panel。</summary>
        Absolute = 0
        ''' <summary>流式排布：按 FlowDirection 自动排列子控件，等同于 FlowLayoutPanel。</summary>
        Flow = 1
    End Enum

    ''' <summary>
    ''' 流式排布方向。
    ''' </summary>
    Public Enum FlowDirectionEnum
        ''' <summary>从左到右排列，超出宽度时换到下一行。</summary>
        LeftToRight = 0
        ''' <summary>从上到下排列，超出高度时换到下一列。</summary>
        TopDown = 1
    End Enum

#Region "构造"

    Public Sub New()
        InitializeComponent()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.ContainerControl Or
                 ControlStyles.SupportsTransparentBackColor, True)
        UpdateStyles()
        Me.AutoScroll = False
        _lastDeviceDpi = Me.DeviceDpi
        AddHandler Me.Disposed, Sub() 停止图片动画()
    End Sub

#End Region

#Region "辅助方法"

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If Not EqualityComparer(Of T).Default.Equals(field, value) Then
            field = value
            _contentSizeDirty = True
            更新滚动区域()
            Me.PerformLayout()
            OuterToInnerRefreshScheduler.RequestFull(Me)
        End If
    End Sub

    Private Function DpiScale() As Single
        Return Me.DeviceDpi / 96.0F
    End Function

    Private Function 获取边框内边距() As Integer
        Dim s As Single = DpiScale()
        Dim scaledBorder As Integer = CInt(Math.Round(边框宽度 * s))
        Dim scaledRadius As Integer = CInt(Math.Round(边框圆角半径 * s))
        Return Math.Max(scaledBorder, If(边框圆角半径 > 0, scaledRadius \ 2, 0))
    End Function

    ''' <summary>获取有效内边距：边框内缩 + 用户 Padding，Padding 表示边框内侧到内容的间距。
    ''' 右/下额外 +1 px 补偿 Rectangle 坐标不对称（起点含、终点不含）。</summary>
    Private Function 获取有效内边距() As Padding
        Dim inset As Integer = 获取边框内边距()
        Dim endFix As Integer = If(边框宽度 > 0, 1, 0)
        Return New Padding(
            inset + Me.Padding.Left,
            inset + Me.Padding.Top,
            inset + Me.Padding.Right + endFix,
            inset + Me.Padding.Bottom + endFix)
    End Function

    ''' <summary>重写 DisplayRectangle，使 Dock 子控件自动避开边框和滚动条区域。</summary>
    Public Overrides ReadOnly Property DisplayRectangle As Rectangle
        Get
            Dim ep As Padding = 获取有效内边距()
            Dim sbReserve As Integer = CInt(Math.Round(滚动条宽度 * DpiScale())) + ScrollBarRenderer.Margin
            Return New Rectangle(
                ep.Left,
                ep.Top,
                Math.Max(0, Me.ClientSize.Width - ep.Horizontal - If(_showVScroll, sbReserve, 0)),
                Math.Max(0, Me.ClientSize.Height - ep.Vertical - If(_showHScroll, sbReserve, 0)))
        End Get
    End Property

    ''' <summary>计算所有子控件的包围矩形（设计坐标系下的内容总大小）。
    ''' 不包含子控件 Margin，避免默认 Margin(3) 在子控件刚好填满视口时触发伪滚动条。</summary>
    Private Function 计算内容总大小() As Size
        Dim maxRight As Integer = 0
        Dim maxBottom As Integer = 0
        Dim ep As Padding = 获取有效内边距()
        For Each ctrl As Control In Me.Controls
            If Not ctrl.Visible Then Continue For
            Dim tag As ChildLayoutInfo = Nothing
            Dim dl, dt As Integer
            If _childLayouts.TryGetValue(ctrl, tag) Then
                dl = tag.DesignLeft
                dt = tag.DesignTop
            Else
                dl = ctrl.Left - ep.Left + _hScrollOffset
                dt = ctrl.Top - ep.Top + _vScrollOffset
            End If
            Dim r As Integer = dl + ctrl.Width
            Dim b As Integer = dt + ctrl.Height
            If r > maxRight Then maxRight = r
            If b > maxBottom Then maxBottom = b
        Next
        Return New Size(maxRight, maxBottom)
    End Function

    ''' <summary>获取扣除滚动条保留区域后的有效视口大小。</summary>
    Private Function 获取有效视口大小() As Size
        Dim ep As Padding = 获取有效内边距()
        Dim sbReserve As Integer = CInt(Math.Round(滚动条宽度 * DpiScale())) + ScrollBarRenderer.Margin
        Dim w As Integer = Me.Width - ep.Horizontal - If(_showVScroll, sbReserve, 0)
        Dim h As Integer = Me.Height - ep.Vertical - If(_showHScroll, sbReserve, 0)
        Return New Size(Math.Max(0, w), Math.Max(0, h))
    End Function

#End Region

#Region "外观属性 - 边框"

    Private 边框颜色 As Color = Color.Gray
    <Category("LakeUI"), Description("边框颜色"), DefaultValue(GetType(Color), "Gray"), Browsable(True)>
    Public Property BorderColor As Color
        Get
            Return 边框颜色
        End Get
        Set(value As Color)
            SetValue(边框颜色, value)
        End Set
    End Property

    Private 边框宽度 As Integer = 1
    <Category("LakeUI"), Description("边框宽度"), DefaultValue(GetType(Integer), "1"), Browsable(True)>
    Public Property BorderSize As Integer
        Get
            Return 边框宽度
        End Get
        Set(value As Integer)
            清除图片缓存()
            SetValue(边框宽度, value)
        End Set
    End Property

    Private 边框圆角半径 As Integer = 0
    <Category("LakeUI"), Description("边框圆角半径"), DefaultValue(GetType(Integer), "0"), Browsable(True)>
    Public Property BorderRadius As Integer
        Get
            Return 边框圆角半径
        End Get
        Set(value As Integer)
            清除图片缓存()
            SetValue(边框圆角半径, value)
        End Set
    End Property

    Private 圆角位置 As RoundCorners = RoundCorners.All
    <Category("LakeUI"), Description("指定哪些角启用圆角，可展开分别设置四个角"), Browsable(True)>
    Public Property BorderRoundCorners As RoundCorners
        Get
            Return 圆角位置
        End Get
        Set(value As RoundCorners)
            清除图片缓存()
            SetValue(圆角位置, value)
        End Set
    End Property

    Private Function ShouldSerializeBorderRoundCorners() As Boolean
        Return 圆角位置 <> RoundCorners.All
    End Function

    Private Sub ResetBorderRoundCorners()
        BorderRoundCorners = RoundCorners.All
    End Sub

#End Region

#Region "外观属性 - 背景"

    Private 背景颜色 As Color = Color.FromArgb(36, 36, 36)
    <Category("LakeUI"), Description("背景颜色"), DefaultValue(GetType(Color), "36, 36, 36"), Browsable(True)>
    Public Property BackColor1 As Color
        Get
            Return 背景颜色
        End Get
        Set(value As Color)
            SetValue(背景颜色, value)
        End Set
    End Property

#End Region

#Region "外观属性 - 背景图片"

    Private _image As Image = Nothing
    ''' <summary>面板背景图片，填充模式由 ImageMode 控制，圆角区域自动裁切。
    ''' 若图片为多帧动画（如 GIF），会按原始帧延迟驱动逐帧重绘。</summary>
    <Category("LakeUI"), Description("背景图片（填充模式由 ImageMode 控制，自动圆角裁切，支持 GIF 等动图）"), DefaultValue(GetType(Image), Nothing), Browsable(True)>
    Public Property Image As Image
        Get
            Return _image
        End Get
        Set(value As Image)
            If _image Is value Then Return
            停止图片动画()
            _image = value
            清除图片缓存()
            启动图片动画()
            OuterToInnerRefreshScheduler.RequestFull(Me)
        End Set
    End Property

    ' GIF / 多帧动画支持：直接读取原始帧延迟并按帧推进，避免 ImageAnimator 的全局调度节拍抖动。
    Private Const PropertyTagFrameDelay As Integer = &H5100
    Private Const PropertyTagLoopCount As Integer = &H5101
    Private Const DefaultAnimationFrameDelayMs As Integer = 100
    Private Const MinimumAnimationFrameDelayMs As Integer = 10

    Private _animatedImage As Image = Nothing
    Private _animationTimer As PrecisionTimer = Nothing
    Private _animationFrameDimension As FrameDimension = Nothing
    Private _animationFrameDelays As Integer() = Array.Empty(Of Integer)()
    Private _animationFrameCount As Integer = 0
    Private _animationFrameIndex As Integer = 0
    Private _animationTimerIntervalMs As Integer = DefaultAnimationFrameDelayMs
    Private _animationNextFrameTicks As Long = 0
    Private _animationLoopLimit As Integer = 0
    Private _animationCompletedLoops As Integer = 0
    Private _animationHostForm As Form = Nothing
    Private ReadOnly _animationVisibilitySources As New List(Of Control)()
    Private _animationFrameImage As Bitmap = Nothing
    Private _animationFrameImageSource As Image = Nothing
    Private _animationFrameImageIndex As Integer = -1

    Private _scaledImageBitmap As ID2D1Bitmap = Nothing
    Private _scaledImageRenderTarget As ID2D1RenderTarget = Nothing
    Private _scaledImageSource As Image = Nothing
    Private _scaledImageSourceRect As Rectangle = Rectangle.Empty
    Private _scaledImageSize As Size = Size.Empty

    Private Sub 启动图片动画()
        If Not 准备图片动画(_image) Then Return
        更新图片动画运行状态()
    End Sub

    Private Sub 停止图片动画()
        停止图片动画计时器(True)
        更新图片动画宿主窗体订阅(Nothing)
        清除图片动画可见性订阅()
        清除动画帧图片缓存()
        If _animatedImage IsNot Nothing AndAlso _animatedImage IsNot _image Then
            Try : _animatedImage.Dispose() : Catch : End Try
        End If
        _animatedImage = Nothing
        _animationFrameDimension = Nothing
        _animationFrameDelays = Array.Empty(Of Integer)()
        _animationFrameCount = 0
        _animationFrameIndex = 0
        _animationLoopLimit = 0
        _animationCompletedLoops = 0
    End Sub

    Private Function 准备图片动画(img As Image) As Boolean
        _animatedImage = Nothing
        _animationFrameDimension = Nothing
        _animationFrameDelays = Array.Empty(Of Integer)()
        _animationFrameCount = 0
        _animationFrameIndex = 0
        _animationLoopLimit = 0
        _animationCompletedLoops = 0
        _animationNextFrameTicks = 0

        If img Is Nothing Then Return False

        Dim dimension As FrameDimension = 获取图片动画维度(img)
        If dimension Is Nothing Then Return False

        Dim frameCount As Integer
        Try
            frameCount = img.GetFrameCount(dimension)
        Catch
            Return False
        End Try
        If frameCount <= 1 Then Return False

        Dim animationImage As Image
        Try
            animationImage = CType(img.Clone(), Image)
            If animationImage.GetFrameCount(dimension) <= 1 Then
                animationImage.Dispose()
                Return False
            End If
        Catch
            Return False
        End Try

        _animatedImage = animationImage
        _animationFrameDimension = dimension
        _animationFrameCount = frameCount
        _animationFrameDelays = 读取图片动画帧延迟(img, frameCount)
        _animationTimerIntervalMs = 计算图片动画计时器间隔(_animationFrameDelays)
        _animationLoopLimit = 读取图片动画循环次数(img)

        Return True
    End Function

    Private Function 获取图片动画维度(img As Image) As FrameDimension
        Try
            For Each guid In img.FrameDimensionsList
                Dim dimension As New FrameDimension(guid)
                If img.GetFrameCount(dimension) > 1 Then Return dimension
            Next
        Catch
        End Try
        Return Nothing
    End Function

    Private Function 读取图片动画帧延迟(img As Image, frameCount As Integer) As Integer()
        Dim delays(frameCount - 1) As Integer
        For i As Integer = 0 To delays.Length - 1
            delays(i) = DefaultAnimationFrameDelayMs
        Next

        Try
            If Array.IndexOf(img.PropertyIdList, PropertyTagFrameDelay) < 0 Then Return delays
            Dim item = img.GetPropertyItem(PropertyTagFrameDelay)
            If item Is Nothing OrElse item.Value Is Nothing Then Return delays

            Dim available As Integer = Math.Min(frameCount, item.Value.Length \ 4)
            For i As Integer = 0 To available - 1
                Dim hundredths As Integer = BitConverter.ToInt32(item.Value, i * 4)
                If hundredths > 0 Then
                    delays(i) = Math.Max(MinimumAnimationFrameDelayMs, hundredths * 10)
                Else
                    delays(i) = MinimumAnimationFrameDelayMs
                End If
            Next
        Catch
        End Try

        Return delays
    End Function

    Private Function 读取图片动画循环次数(img As Image) As Integer
        Try
            If Array.IndexOf(img.PropertyIdList, PropertyTagLoopCount) < 0 Then Return 0
            Dim item = img.GetPropertyItem(PropertyTagLoopCount)
            If item Is Nothing OrElse item.Value Is Nothing OrElse item.Value.Length < 2 Then Return 0
            Return CInt(BitConverter.ToUInt16(item.Value, 0))
        Catch
            Return 0
        End Try
    End Function

    Private Function 计算图片动画计时器间隔(delays As Integer()) As Integer
        If delays Is Nothing OrElse delays.Length = 0 Then Return DefaultAnimationFrameDelayMs
        Dim interval As Integer = Math.Max(1, delays(0))
        For i As Integer = 1 To delays.Length - 1
            interval = 最大公约数(interval, Math.Max(1, delays(i)))
        Next
        Return Math.Max(MinimumAnimationFrameDelayMs, interval)
    End Function

    Private Shared Function 最大公约数(a As Integer, b As Integer) As Integer
        a = Math.Abs(a)
        b = Math.Abs(b)
        If a = 0 Then Return Math.Max(1, b)
        If b = 0 Then Return Math.Max(1, a)
        While b <> 0
            Dim t As Integer = a Mod b
            a = b
            b = t
        End While
        Return Math.Max(1, a)
    End Function

    Private Sub 更新图片动画运行状态()
        更新图片动画宿主窗体订阅(If(Me.IsHandleCreated, Me.FindForm(), Nothing))

        Dim img = _animatedImage
        If img Is Nothing OrElse _animationFrameCount <= 1 Then
            停止图片动画计时器(False)
            Return
        End If

        If 图片动画可以渲染() Then
            恢复图片动画渲染()
        Else
            停止图片动画计时器(False)
        End If
    End Sub

    Private Function 图片动画可以渲染() As Boolean
        If Me.IsDisposed OrElse Not Me.IsHandleCreated Then Return False
        If Me.Width <= 0 OrElse Me.Height <= 0 Then Return False

        Dim ctrl As Control = Me
        While ctrl IsNot Nothing
            If Not ctrl.Visible Then Return False
            ctrl = ctrl.Parent
        End While

        Dim host = Me.FindForm()
        If host IsNot Nothing Then
            If Not host.Visible OrElse host.WindowState = FormWindowState.Minimized Then Return False
        End If

        Return True
    End Function

    Private Sub 恢复图片动画渲染()
        If _animationTimer Is Nothing Then
            _animationTimer = New PrecisionTimer() With {
                .DispatchMode = PrecisionTimer.DispatchModeEnum.NonBlocking,
                .OverrunPolicy = PrecisionTimer.OverrunPolicyEnum.Drop,
                .WorkerThreadCount = 1,
                .SynchronizingObject = Me,
                .AutoReset = True
            }
            AddHandler _animationTimer.Tick, AddressOf 图片动画计时器触发
        End If

        _animationTimer.Interval = Math.Max(1, _animationTimerIntervalMs)
        If _animationTimer.IsRunning Then Return

        _animationNextFrameTicks = Stopwatch.GetTimestamp() + 毫秒转StopwatchTicks(获取当前图片动画帧延迟())
        Try
            _animationTimer.Start()
        Catch
            停止图片动画计时器(True)
        End Try
        OuterToInnerRefreshScheduler.Request(Me, 获取背景图片刷新区域())
    End Sub

    Private Sub 停止图片动画计时器(disposeTimer As Boolean)
        _animationNextFrameTicks = 0
        If _animationTimer Is Nothing Then Return

        Try
            _animationTimer.Stop()
        Catch
        End Try

        If disposeTimer Then
            Try
                RemoveHandler _animationTimer.Tick, AddressOf 图片动画计时器触发
                _animationTimer.Dispose()
            Catch
            End Try
            _animationTimer = Nothing
        End If
    End Sub

    Private Sub 图片动画计时器触发(sender As Object, e As EventArgs)
        If Me.IsDisposed Then Return
        If Not 图片动画可以渲染() Then
            停止图片动画计时器(False)
            Return
        End If

        If _animatedImage Is Nothing OrElse _animationFrameDimension Is Nothing OrElse _animationFrameCount <= 1 Then Return

        Dim nowTicks As Long = Stopwatch.GetTimestamp()
        If _animationNextFrameTicks <= 0 Then
            _animationNextFrameTicks = nowTicks + 毫秒转StopwatchTicks(获取当前图片动画帧延迟())
            Return
        End If
        If nowTicks < _animationNextFrameTicks Then Return

        Dim frameChanged As Boolean = False
        Dim maxCatchUpFrames As Integer = Math.Max(1, _animationFrameCount * 2)
        Dim advanced As Integer = 0

        Do
            If Not 推进图片动画一帧() Then Exit Do
            frameChanged = True
            advanced += 1
            _animationNextFrameTicks += 毫秒转StopwatchTicks(获取当前图片动画帧延迟())
        Loop While advanced < maxCatchUpFrames AndAlso nowTicks >= _animationNextFrameTicks

        If advanced >= maxCatchUpFrames AndAlso nowTicks >= _animationNextFrameTicks Then
            _animationNextFrameTicks = nowTicks + 毫秒转StopwatchTicks(获取当前图片动画帧延迟())
        End If

        If frameChanged Then
            失效背景图片上传缓存()
            OuterToInnerRefreshScheduler.Request(Me, 获取背景图片刷新区域())
        End If
    End Sub

    Private Function 推进图片动画一帧() As Boolean
        Dim img = _animatedImage
        If img Is Nothing OrElse _animationFrameDimension Is Nothing OrElse _animationFrameCount <= 1 Then Return False

        Dim nextIndex As Integer = _animationFrameIndex + 1
        If nextIndex >= _animationFrameCount Then
            If _animationLoopLimit > 0 Then
                _animationCompletedLoops += 1
                If _animationCompletedLoops >= _animationLoopLimit Then
                    停止图片动画计时器(False)
                    Return False
                End If
            End If
            nextIndex = 0
        End If

        _animationFrameIndex = nextIndex
        Return True
    End Function

    Private Function 获取当前图片动画帧延迟() As Integer
        If _animationFrameDelays Is Nothing OrElse _animationFrameDelays.Length = 0 Then Return DefaultAnimationFrameDelayMs
        Dim index As Integer = Math.Max(0, Math.Min(_animationFrameIndex, _animationFrameDelays.Length - 1))
        Return Math.Max(1, _animationFrameDelays(index))
    End Function

    Private Shared Function 毫秒转StopwatchTicks(milliseconds As Integer) As Long
        Return Math.Max(1L, CLng(Stopwatch.Frequency * (Math.Max(1, milliseconds) / 1000.0)))
    End Function

    Private Sub 失效背景图片上传缓存()
        清除动画帧图片缓存()
        清除缩放图片缓存()
    End Sub

    Private Function 获取背景图片刷新区域() As Rectangle
        If _image Is Nothing OrElse Me.ClientSize.Width <= 0 OrElse Me.ClientSize.Height <= 0 Then Return Me.ClientRectangle

        Dim s As Single = DpiScale()
        Dim area As New RectangleF(0, 0, Me.Width, Me.Height)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * s / 2.0F
            area.Inflate(-half, -half)
        End If

        Dim srcW As Single = _image.Width
        Dim srcH As Single = _image.Height
        If srcW <= 0 OrElse srcH <= 0 OrElse area.Width <= 0 OrElse area.Height <= 0 Then Return Me.ClientRectangle

        Dim destRect As RectangleF = 计算背景图片目标矩形(area, srcW, srcH, _imageFillMode)

        Dim invalidRect As Rectangle = Rectangle.Ceiling(destRect)
        invalidRect.Inflate(2, 2)
        invalidRect = Rectangle.Intersect(Me.ClientRectangle, invalidRect)
        If invalidRect.Width <= 0 OrElse invalidRect.Height <= 0 Then Return Me.ClientRectangle
        Return invalidRect
    End Function

    Private Sub 更新图片动画宿主窗体订阅(host As Form)
        If _animationHostForm Is host Then Return

        If _animationHostForm IsNot Nothing Then
            RemoveHandler _animationHostForm.VisibleChanged, AddressOf 图片动画宿主状态变更
            RemoveHandler _animationHostForm.Resize, AddressOf 图片动画宿主状态变更
            RemoveHandler _animationHostForm.Disposed, AddressOf 图片动画宿主状态变更
        End If

        _animationHostForm = host

        If _animationHostForm IsNot Nothing Then
            AddHandler _animationHostForm.VisibleChanged, AddressOf 图片动画宿主状态变更
            AddHandler _animationHostForm.Resize, AddressOf 图片动画宿主状态变更
            AddHandler _animationHostForm.Disposed, AddressOf 图片动画宿主状态变更
        End If
    End Sub

    Private Sub 图片动画宿主状态变更(sender As Object, e As EventArgs)
        更新图片动画运行状态()
    End Sub

    Private Sub 更新图片动画可见性订阅()
        清除图片动画可见性订阅()

        Dim ctrl As Control = Me.Parent
        While ctrl IsNot Nothing
            If Not TypeOf ctrl Is Form Then
                AddHandler ctrl.VisibleChanged, AddressOf 图片动画父级状态变更
                AddHandler ctrl.ParentChanged, AddressOf 图片动画父级状态变更
                AddHandler ctrl.Disposed, AddressOf 图片动画父级状态变更
                _animationVisibilitySources.Add(ctrl)
            End If
            ctrl = ctrl.Parent
        End While
    End Sub

    Private Sub 清除图片动画可见性订阅()
        For Each ctrl In _animationVisibilitySources
            Try
                RemoveHandler ctrl.VisibleChanged, AddressOf 图片动画父级状态变更
                RemoveHandler ctrl.ParentChanged, AddressOf 图片动画父级状态变更
                RemoveHandler ctrl.Disposed, AddressOf 图片动画父级状态变更
            Catch
            End Try
        Next
        _animationVisibilitySources.Clear()
    End Sub

    Private Sub 图片动画父级状态变更(sender As Object, e As EventArgs)
        更新图片动画可见性订阅()
        更新图片动画运行状态()
    End Sub

    Private Sub 清除动画帧图片缓存()
        If _animationFrameImage IsNot Nothing Then
            Try : _animationFrameImage.Dispose() : Catch : End Try
            _animationFrameImage = Nothing
        End If
        _animationFrameImageSource = Nothing
        _animationFrameImageIndex = -1
    End Sub

    Private Function 获取背景图片位图_D2D(rt As ID2D1RenderTarget, compositor As WindowCompositor,
                                       ByRef shouldDispose As Boolean) As ID2D1Bitmap
        shouldDispose = False
        If _image Is Nothing OrElse rt Is Nothing Then Return Nothing

        If _animatedImage IsNot Nothing AndAlso _animationFrameDimension IsNot Nothing AndAlso _animationFrameCount > 1 Then
            shouldDispose = True
            Return 获取动画帧位图_D2D(rt)
        End If

        If compositor Is Nothing Then Return Nothing
        Dim cache = compositor.GetBitmapCache(_image)
        Return If(cache Is Nothing, Nothing, cache.GetBitmap(rt, _image))
    End Function

    Private Function 获取动画帧位图_D2D(rt As ID2D1RenderTarget) As ID2D1Bitmap
        If _image Is Nothing OrElse rt Is Nothing Then Return Nothing

        Dim frameImage As Bitmap = 获取动画帧图像()
        If frameImage Is Nothing Then Return Nothing

        Return D2DGlobals.CreateBitmapFromGdi(rt, frameImage)
    End Function

    Private Function 获取动画帧图像() As Bitmap
        Dim img = _animatedImage
        If img Is Nothing OrElse _animationFrameDimension Is Nothing Then Return Nothing

        If _animationFrameImage IsNot Nothing AndAlso
           _animationFrameImageSource Is img AndAlso
           _animationFrameImageIndex = _animationFrameIndex Then
            Return _animationFrameImage
        End If

        清除动画帧图片缓存()

        Dim frameImage As Bitmap = Nothing
        Try
            SyncLock img
                img.SelectActiveFrame(_animationFrameDimension, _animationFrameIndex)
                frameImage = New Bitmap(img.Width, img.Height, PixelFormat.Format32bppPArgb)
                frameImage.SetResolution(img.HorizontalResolution, img.VerticalResolution)
                Using g = Graphics.FromImage(frameImage)
                    g.DrawImage(img, 0, 0, img.Width, img.Height)
                End Using
            End SyncLock

            _animationFrameImage = frameImage
            frameImage = Nothing
            _animationFrameImageSource = img
            _animationFrameImageIndex = _animationFrameIndex
        Catch
            If frameImage IsNot Nothing Then
                Try : frameImage.Dispose() : Catch : End Try
            End If
            清除动画帧图片缓存()
        End Try

        Return _animationFrameImage
    End Function

    Private Shared Function 计算背景图片目标矩形(area As RectangleF, srcW As Single, srcH As Single, mode As ImageFillMode) As RectangleF
        If srcW <= 0 OrElse srcH <= 0 OrElse area.Width <= 0 OrElse area.Height <= 0 Then Return RectangleF.Empty

        Dim ratioW As Single = area.Width / srcW
        Dim ratioH As Single = area.Height / srcH
        Dim ratio As Single = If(mode = ImageFillMode.Fill, Math.Max(ratioW, ratioH), Math.Min(ratioW, ratioH))
        If ratio <= 0 Then Return RectangleF.Empty

        Dim drawW As Single = srcW * ratio
        Dim drawH As Single = srcH * ratio
        Return New RectangleF(
            area.X + (area.Width - drawW) / 2.0F,
            area.Y + (area.Height - drawH) / 2.0F,
            drawW,
            drawH)
    End Function

    Private Shared Function 计算可见源矩形(destRect As RectangleF, visibleDest As RectangleF, ratio As Single, srcW As Integer, srcH As Integer) As Rectangle
        If ratio <= 0 OrElse srcW <= 0 OrElse srcH <= 0 OrElse visibleDest.Width <= 0 OrElse visibleDest.Height <= 0 Then
            Return Rectangle.Empty
        End If

        Dim srcLeft As Integer = 限制整数(CInt(Math.Floor((visibleDest.Left - destRect.Left) / ratio)), 0, srcW)
        Dim srcTop As Integer = 限制整数(CInt(Math.Floor((visibleDest.Top - destRect.Top) / ratio)), 0, srcH)
        Dim srcRight As Integer = 限制整数(CInt(Math.Ceiling((visibleDest.Right - destRect.Left) / ratio)), 0, srcW)
        Dim srcBottom As Integer = 限制整数(CInt(Math.Ceiling((visibleDest.Bottom - destRect.Top) / ratio)), 0, srcH)

        If srcRight <= srcLeft OrElse srcBottom <= srcTop Then Return Rectangle.Empty
        Return New Rectangle(srcLeft, srcTop, srcRight - srcLeft, srcBottom - srcTop)
    End Function

    Private Shared Function 计算缩放位图尺寸(visibleDest As RectangleF, renderScale As Integer) As Size
        Dim scale As Integer = Math.Max(1, renderScale)
        Return New Size(
            Math.Max(1, CInt(Math.Round(visibleDest.Width * scale))),
            Math.Max(1, CInt(Math.Round(visibleDest.Height * scale))))
    End Function

    Private Shared Function 限制整数(value As Integer, minimum As Integer, maximum As Integer) As Integer
        If value < minimum Then Return minimum
        If value > maximum Then Return maximum
        Return value
    End Function

    Private Function 获取高质量缩放背景位图_D2D(rt As ID2D1RenderTarget, img As Image, sourceRect As Rectangle, scaledSize As Size) As ID2D1Bitmap
        If rt Is Nothing OrElse img Is Nothing Then Return Nothing
        If sourceRect.Width <= 0 OrElse sourceRect.Height <= 0 OrElse scaledSize.Width <= 0 OrElse scaledSize.Height <= 0 Then Return Nothing

        If _scaledImageBitmap IsNot Nothing AndAlso
           _scaledImageRenderTarget Is rt AndAlso
           _scaledImageSource Is img AndAlso
           _scaledImageSourceRect.Equals(sourceRect) AndAlso
           _scaledImageSize.Equals(scaledSize) Then
            Return _scaledImageBitmap
        End If

        清除缩放图片缓存()

        Dim scaledBitmap As Bitmap = Nothing
        Dim uploadedBitmap As ID2D1Bitmap = Nothing
        Try
            scaledBitmap = 创建高质量缩放位图(img, sourceRect, scaledSize)
            uploadedBitmap = D2DGlobals.CreateBitmapFromGdi(rt, scaledBitmap)
            If uploadedBitmap Is Nothing Then Return Nothing

            _scaledImageBitmap = uploadedBitmap
            uploadedBitmap = Nothing
            _scaledImageRenderTarget = rt
            _scaledImageSource = img
            _scaledImageSourceRect = sourceRect
            _scaledImageSize = scaledSize
            Return _scaledImageBitmap
        Catch
            清除缩放图片缓存()
            Return Nothing
        Finally
            If uploadedBitmap IsNot Nothing Then
                Try : uploadedBitmap.Dispose() : Catch : End Try
            End If
            If scaledBitmap IsNot Nothing Then
                Try : scaledBitmap.Dispose() : Catch : End Try
            End If
        End Try
    End Function

    Private Shared Function 创建高质量缩放位图(img As Image, sourceRect As Rectangle, scaledSize As Size) As Bitmap
        Dim scaled As New Bitmap(scaledSize.Width, scaledSize.Height, PixelFormat.Format32bppPArgb)
        Try
            Try : scaled.SetResolution(img.HorizontalResolution, img.VerticalResolution) : Catch : End Try
            Using g = Graphics.FromImage(scaled)
                g.CompositingMode = Drawing2D.CompositingMode.SourceCopy
                g.CompositingQuality = Drawing2D.CompositingQuality.HighQuality
                g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                g.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
                g.SmoothingMode = Drawing2D.SmoothingMode.HighQuality

                Using attr As New ImageAttributes()
                    attr.SetWrapMode(Drawing2D.WrapMode.TileFlipXY)
                    g.DrawImage(
                        img,
                        New Rectangle(0, 0, scaledSize.Width, scaledSize.Height),
                        sourceRect.X,
                        sourceRect.Y,
                        sourceRect.Width,
                        sourceRect.Height,
                        GraphicsUnit.Pixel,
                        attr)
                End Using
            End Using
            Return scaled
        Catch
            scaled.Dispose()
            Throw
        End Try
    End Function

    Private Sub 清除缩放图片缓存()
        If _scaledImageBitmap IsNot Nothing Then
            Try : _scaledImageBitmap.Dispose() : Catch : End Try
            _scaledImageBitmap = Nothing
        End If
        _scaledImageRenderTarget = Nothing
        _scaledImageSource = Nothing
        _scaledImageSourceRect = Rectangle.Empty
        _scaledImageSize = Size.Empty
    End Sub

    Private _imageFillMode As ImageFillMode = ImageFillMode.Fill
    ''' <summary>背景图片填充模式：Zoom（完整显示）或 Fill（撑满裁切）。</summary>
    <Category("LakeUI"), Description("背景图片填充模式：Zoom = 完整显示可能留白，Fill = 居中撑满裁切多余部分"), DefaultValue(GetType(ImageFillMode), "Fill"), Browsable(True)>
    Public Property ImageMode As ImageFillMode
        Get
            Return _imageFillMode
        End Get
        Set(value As ImageFillMode)
            If _imageFillMode <> value Then
                _imageFillMode = value
                清除图片缓存()
                OuterToInnerRefreshScheduler.RequestFull(Me)
            End If
        End Set
    End Property

    ' 屏蔽继承自 Panel 的 BackgroundImage / BackgroundImageLayout，
    ' 防止基类 OnPaintBackground 自行绘制图片绕过我们的圆角裁切逻辑。
    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Overrides Property BackgroundImage As Image
        Get
            Return Nothing
        End Get
        Set(value As Image)
        End Set
    End Property

    <Browsable(False), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Overrides Property BackgroundImageLayout As ImageLayout
        Get
            Return ImageLayout.None
        End Get
        Set(value As ImageLayout)
        End Set
    End Property

#End Region

#Region "外观属性 - 遮罩"

    Private 遮罩颜色 As Color = Color.Transparent
    <Category("LakeUI"), Description("背景遮罩半透明颜色，绘制在背景与边框之间，对包括父容器背景图片在内的所有内容生效"), DefaultValue(GetType(Color), "Transparent"), Browsable(True)>
    Public Property OverlayColor As Color
        Get
            Return 遮罩颜色
        End Get
        Set(value As Color)
            SetValue(遮罩颜色, value)
        End Set
    End Property

#End Region

#Region "外观属性 - 透明背景源"

    Private _backgroundSource As Control = Nothing
    ''' <summary>
    ''' 背景采样源（超容器背景映射）。透明背景模式下，控件会调用此控件的绘制流程取像素作为底图，
    ''' 从而实现跨越任意层级的"穿透显示"效果。
    ''' 典型场景：当本控件位于一个不透明的祖先（如 ModernTabListControl 的 BoundControl 独立窗体）内，
    ''' 但希望透出更外层（如顶层窗体）的内容时，将其设置为目标控件即可。
    ''' 为 Nothing 时自动沿祖先链查找首个不透明祖先（默认行为）。
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
                解除背景穿透消费者()
                _backgroundSource = BackgroundPenetrationV2.SetConsumerSource(Me, _backgroundSource, value)
                OuterToInnerRefreshScheduler.RequestFull(Me)
            End If
        End Set
    End Property

    Private Sub 解除背景穿透消费者()
        If _backgroundSource Is Nothing Then Return
        Try : BackgroundPenetrationV2.UnregisterConsumer(Me, _backgroundSource) : Catch : End Try
    End Sub

    Friend Function TryGetTransparentBackgroundForward(ByRef source As Control) As Boolean
        source = Nothing
        If _backgroundSource Is Nothing Then Return False
        If _image IsNot Nothing Then Return False
        If MyBase.BackColor.A <> 0 OrElse 背景颜色.A <> 0 Then Return False
        If 遮罩颜色.A <> 0 Then Return False
        If 边框宽度 <> 0 OrElse 边框圆角半径 <> 0 Then Return False
        If _showVScroll OrElse _showHScroll Then Return False
        source = _backgroundSource
        Return True
    End Function

#End Region

#Region "外观属性 - 滚动条"

    Private 滚动条宽度 As Integer = 10
    <Category("LakeUI"), Description("滚动条宽度"), DefaultValue(GetType(Integer), "10"), Browsable(True)>
    Public Property ScrollBarWidth As Integer
        Get
            Return 滚动条宽度
        End Get
        Set(value As Integer)
            SetValue(滚动条宽度, value)
        End Set
    End Property

    Private Shared ReadOnly 默认滚动条轨道颜色 As Color = Color.FromArgb(20, 20, 20)
    Private 滚动条轨道颜色 As Color = 默认滚动条轨道颜色
    <Category("LakeUI"), Description("滚动条轨道颜色"), Browsable(True)>
    Public Property ScrollBarTrackColor As Color
        Get
            Return 滚动条轨道颜色
        End Get
        Set(value As Color)
            SetValue(滚动条轨道颜色, value)
        End Set
    End Property

    Private Function ShouldSerializeScrollBarTrackColor() As Boolean
        Return 滚动条轨道颜色 <> 默认滚动条轨道颜色
    End Function

    Private Sub ResetScrollBarTrackColor()
        ScrollBarTrackColor = 默认滚动条轨道颜色
    End Sub

    Private Shared ReadOnly 默认滚动条滑块颜色 As Color = Color.FromArgb(80, 80, 80)
    Private 滚动条滑块颜色 As Color = 默认滚动条滑块颜色
    <Category("LakeUI"), Description("滚动条滑块颜色"), Browsable(True)>
    Public Property ScrollBarThumbColor As Color
        Get
            Return 滚动条滑块颜色
        End Get
        Set(value As Color)
            SetValue(滚动条滑块颜色, value)
        End Set
    End Property

    Private Function ShouldSerializeScrollBarThumbColor() As Boolean
        Return 滚动条滑块颜色 <> 默认滚动条滑块颜色
    End Function

    Private Sub ResetScrollBarThumbColor()
        ScrollBarThumbColor = 默认滚动条滑块颜色
    End Sub

    Private Shared ReadOnly 默认滚动条悬停颜色 As Color = Color.FromArgb(120, 120, 120)
    Private 滚动条悬停颜色 As Color = 默认滚动条悬停颜色
    <Category("LakeUI"), Description("滚动条滑块悬停颜色"), Browsable(True)>
    Public Property ScrollBarThumbHoverColor As Color
        Get
            Return 滚动条悬停颜色
        End Get
        Set(value As Color)
            SetValue(滚动条悬停颜色, value)
        End Set
    End Property

    Private Function ShouldSerializeScrollBarThumbHoverColor() As Boolean
        Return 滚动条悬停颜色 <> 默认滚动条悬停颜色
    End Function

    Private Sub ResetScrollBarThumbHoverColor()
        ScrollBarThumbHoverColor = 默认滚动条悬停颜色
    End Sub

#End Region

#Region "外观属性 - SSAA"

    Private 超采样倍率 As Integer = 1
    <Category("LakeUI"), Description(GlobalOptions.超采样抗锯齿描述词), DefaultValue(GetType(GlobalOptions.SuperSamplingScaleEnum), "OFF"), Browsable(True)>
    Public Property SuperSamplingScale As GlobalOptions.SuperSamplingScaleEnum
        Get
            Return 超采样倍率
        End Get
        Set(value As GlobalOptions.SuperSamplingScaleEnum)
            SetValue(超采样倍率, value)
        End Set
    End Property

#End Region

#Region "行为属性"

    Private 滚动模式 As ScrollMode = ScrollMode.None
    <Category("LakeUI"), Description("滚动条策略：None/Vertical/Horizontal/Both"), DefaultValue(GetType(ScrollMode), "None"), Browsable(True)>
    Public Property ScrollBarMode As ScrollMode
        Get
            Return 滚动模式
        End Get
        Set(value As ScrollMode)
            SetValue(滚动模式, value)
        End Set
    End Property

    Private 垂直滚动步长 As Integer = 40
    <Category("LakeUI"), Description("垂直滚动每次像素步长"), DefaultValue(GetType(Integer), "40"), Browsable(True)>
    Public Property VerticalScrollStep As Integer
        Get
            Return 垂直滚动步长
        End Get
        Set(value As Integer)
            垂直滚动步长 = Math.Max(1, value)
        End Set
    End Property

    Private 水平滚动步长 As Integer = 40
    <Category("LakeUI"), Description("水平滚动每次像素步长"), DefaultValue(GetType(Integer), "40"), Browsable(True)>
    Public Property HorizontalScrollStep As Integer
        Get
            Return 水平滚动步长
        End Get
        Set(value As Integer)
            水平滚动步长 = Math.Max(1, value)
        End Set
    End Property

    Private _layoutMode As LayoutModeEnum = LayoutModeEnum.Absolute
    <Category("LakeUI"), Description("子控件排布模式：Absolute = 绝对定位（等同原版 Panel）；Flow = 流式排布（等同 FlowLayoutPanel）"), DefaultValue(GetType(LayoutModeEnum), "Absolute"), Browsable(True)>
    Public Property LayoutMode As LayoutModeEnum
        Get
            Return _layoutMode
        End Get
        Set(value As LayoutModeEnum)
            If _layoutMode <> value Then
                _layoutMode = value
                _childLayouts.Clear()
                _contentSizeDirty = True
                Me.PerformLayout()
                OuterToInnerRefreshScheduler.RequestFull(Me)
            End If
        End Set
    End Property

    Private _flowDirection As FlowDirectionEnum = FlowDirectionEnum.LeftToRight
    <Category("LakeUI"), Description("流式排布方向（仅在 LayoutMode=Flow 时生效）"), DefaultValue(GetType(FlowDirectionEnum), "LeftToRight"), Browsable(True)>
    Public Property FlowDirection As FlowDirectionEnum
        Get
            Return _flowDirection
        End Get
        Set(value As FlowDirectionEnum)
            If _flowDirection <> value Then
                _flowDirection = value
                _contentSizeDirty = True
                Me.PerformLayout()
                OuterToInnerRefreshScheduler.RequestFull(Me)
            End If
        End Set
    End Property

    Private _wrapContents As Boolean = True
    <Category("LakeUI"), Description("流式排布是否自动换行/换列（仅在 LayoutMode=Flow 时生效）"), DefaultValue(True), Browsable(True)>
    Public Property WrapContents As Boolean
        Get
            Return _wrapContents
        End Get
        Set(value As Boolean)
            If _wrapContents <> value Then
                _wrapContents = value
                _contentSizeDirty = True
                Me.PerformLayout()
                OuterToInnerRefreshScheduler.RequestFull(Me)
            End If
        End Set
    End Property

#End Region

#Region "内部状态"

    Private _vScrollOffset As Integer = 0
    Private ReadOnly _vScrollBar As New ScrollBarRenderer()

    Private _hScrollOffset As Integer = 0
    Private ReadOnly _hScrollBar As New ScrollBarRenderer()

    Private _contentSize As Size = Size.Empty
    Private _contentSizeDirty As Boolean = True

    Private _showVScroll As Boolean = False
    Private _showHScroll As Boolean = False

    Private ReadOnly _childLayouts As New Dictionary(Of Control, ChildLayoutInfo)()

    Private _inScrollUpdate As Boolean = False

    Private _lastDeviceDpi As Integer = 96
    Private _inDpiChange As Boolean = False

    ' V2：D2D 资源全部移到 WindowCompositor（Form 级共享），控件本身不再持有任何 D2D 字段。

    ''' <summary>清理控件级动图帧上传缓存；静态背景图缓存仍由 WindowCompositor 共享管理。</summary>
    Private Sub 清除图片缓存()
        清除动画帧图片缓存()
    End Sub

#End Region

#Region "滚动区域计算"

    Private Function 需要垂直滚动条() As Boolean
        Return _showVScroll
    End Function

    Private Function 需要水平滚动条() As Boolean
        Return _showHScroll
    End Function

    Private Sub 更新滚动区域()
        If _inScrollUpdate OrElse _inDpiChange Then Return
        _inScrollUpdate = True
        Try
            更新滚动区域Core()
        Finally
            _inScrollUpdate = False
        End Try
    End Sub

    ''' <summary>仅更新滚动偏移和子控件位置，不重新计算内容大小和滚动条可见性。用于拖动/滚轮等高频场景。</summary>
    Private Sub 快速滚动更新()
        If _inScrollUpdate OrElse _inDpiChange Then Return
        _inScrollUpdate = True
        Try
            Dim s As Single = DpiScale()
            Dim scaledBorderWidth As Integer = CInt(Math.Round(边框宽度 * s))
            Dim scaledBorderRadius As Integer = CInt(Math.Round(边框圆角半径 * s))
            Dim scaledScrollBarWidth As Integer = CInt(Math.Round(滚动条宽度 * s))
            Dim inset As Integer = 获取边框内边距()
            Dim ep As Padding = 获取有效内边距()
            Dim sbReserve As Integer = scaledScrollBarWidth + ScrollBarRenderer.Margin
            Dim viewW As Integer = Math.Max(0, Me.Width - ep.Horizontal - If(_showVScroll, sbReserve, 0))
            Dim viewH As Integer = Math.Max(0, Me.Height - ep.Vertical - If(_showHScroll, sbReserve, 0))

            If _showVScroll Then
                Dim maxOff As Integer = Math.Max(0, _contentSize.Height - viewH)
                _vScrollOffset = Math.Max(0, Math.Min(_vScrollOffset, maxOff))
                _vScrollBar.ComputeLayout(Me.Width, Me.Height, scaledBorderWidth, scaledBorderRadius,
                    ep.Top - inset, ep.Bottom - inset + If(_showHScroll, sbReserve, 0), scaledScrollBarWidth,
                    _contentSize.Height, viewH, _vScrollOffset)
            End If

            If _showHScroll Then
                Dim maxOff As Integer = Math.Max(0, _contentSize.Width - viewW)
                _hScrollOffset = Math.Max(0, Math.Min(_hScrollOffset, maxOff))
                Dim vsbReserved As Integer = If(_showVScroll, Me.Width - inset - _vScrollBar.VisualLeft, 0)
                _hScrollBar.ComputeHorizontalLayout(Me.Width, Me.Height, scaledBorderWidth, scaledBorderRadius,
                    ep.Left - inset, ep.Right - inset + vsbReserved, scaledScrollBarWidth,
                    _contentSize.Width, viewW, _hScrollOffset)
            End If

            应用子控件偏移()
        Finally
            _inScrollUpdate = False
        End Try
    End Sub

    Private Sub 更新滚动区域Core()
        If _contentSizeDirty Then
            _contentSize = 计算内容总大小()
            _contentSizeDirty = False
        End If

        Dim s As Single = DpiScale()
        Dim scaledBorderWidth As Integer = CInt(Math.Round(边框宽度 * s))
        Dim scaledBorderRadius As Integer = CInt(Math.Round(边框圆角半径 * s))
        Dim scaledScrollBarWidth As Integer = CInt(Math.Round(滚动条宽度 * s))

        Dim inset As Integer = 获取边框内边距()
        Dim ep As Padding = 获取有效内边距()
        Dim sbReserve As Integer = scaledScrollBarWidth + ScrollBarRenderer.Margin

        ' 不含滚动条的完整视口
        Dim fullW As Integer = Me.Width - ep.Horizontal
        Dim fullH As Integer = Me.Height - ep.Vertical

        ' 两轮检测：先各自判断，再考虑对方占用空间后重新判断
        Dim needV As Boolean = (滚动模式 And ScrollMode.Vertical) <> 0 AndAlso _contentSize.Height > fullH
        Dim needH As Boolean = (滚动模式 And ScrollMode.Horizontal) <> 0 AndAlso _contentSize.Width > fullW

        If needV AndAlso Not needH Then
            If (滚动模式 And ScrollMode.Horizontal) <> 0 AndAlso _contentSize.Width > (fullW - sbReserve) Then
                needH = True
            End If
        End If
        If needH AndAlso Not needV Then
            If (滚动模式 And ScrollMode.Vertical) <> 0 AndAlso _contentSize.Height > (fullH - sbReserve) Then
                needV = True
            End If
        End If

        _showVScroll = needV
        _showHScroll = needH

        ' 有效视口（扣除滚动条保留区域：scrollBarWidth + Margin，内容边缘对齐滚动条视觉边缘）
        Dim viewW As Integer = Math.Max(0, fullW - If(needV, sbReserve, 0))
        Dim viewH As Integer = Math.Max(0, fullH - If(needH, sbReserve, 0))

        ' 垂直
        If needV Then
            _vScrollBar.ComputeLayout(Me.Width, Me.Height, scaledBorderWidth, scaledBorderRadius,
                ep.Top - inset, ep.Bottom - inset + If(needH, sbReserve, 0), scaledScrollBarWidth,
                _contentSize.Height, viewH, _vScrollOffset)
            Dim maxOff As Integer = Math.Max(0, _contentSize.Height - viewH)
            _vScrollOffset = Math.Max(0, Math.Min(_vScrollOffset, maxOff))
        Else
            _vScrollOffset = 0
            _vScrollBar.ThumbRect = Rectangle.Empty
            _vScrollBar.TrackRect = Rectangle.Empty
            _vScrollBar.VisualLeft = Me.Width
        End If

        ' 水平
        If needH Then
            Dim vsbReserved As Integer = If(needV, Me.Width - inset - _vScrollBar.VisualLeft, 0)
            _hScrollBar.ComputeHorizontalLayout(Me.Width, Me.Height, scaledBorderWidth, scaledBorderRadius,
                ep.Left - inset, ep.Right - inset + vsbReserved, scaledScrollBarWidth,
                _contentSize.Width, viewW, _hScrollOffset)
            Dim maxOff As Integer = Math.Max(0, _contentSize.Width - viewW)
            _hScrollOffset = Math.Max(0, Math.Min(_hScrollOffset, maxOff))
        Else
            _hScrollOffset = 0
            _hScrollBar.ThumbRect = Rectangle.Empty
            _hScrollBar.TrackRect = Rectangle.Empty
            _hScrollBar.VisualTop = Me.Height
        End If

        应用子控件偏移()
    End Sub

    Private Sub 应用子控件偏移()
        _suppressLocationSync = True
        SuspendLayout()
        Try
            Dim ep As Padding = 获取有效内边距()
            Dim ox As Integer = ep.Left - _hScrollOffset
            Dim oy As Integer = ep.Top - _vScrollOffset

            For Each ctrl As Control In Me.Controls
                Dim tag As ChildLayoutInfo = Nothing
                If Not _childLayouts.TryGetValue(ctrl, tag) Then
                    tag = New ChildLayoutInfo With {
                        .DesignLeft = ctrl.Left - ep.Left + _hScrollOffset,
                        .DesignTop = ctrl.Top - ep.Top + _vScrollOffset
                    }
                    _childLayouts(ctrl) = tag
                End If
                Dim newLeft As Integer = ox + tag.DesignLeft
                Dim newTop As Integer = oy + tag.DesignTop
                If ctrl.Left <> newLeft OrElse ctrl.Top <> newTop Then
                    ctrl.SetBounds(newLeft, newTop, 0, 0, BoundsSpecified.Location)
                End If
            Next
        Finally
            ResumeLayout(False)
            _suppressLocationSync = False
        End Try
    End Sub

    Private Class ChildLayoutInfo
        Public DesignLeft As Integer
        Public DesignTop As Integer
    End Class

#End Region

#Region "绘制"

    Private Function 需要自绘背景() As Boolean
        ' V2 契约：以下任一为真都不能交给基类绘制背景：
        '   1) 圆角 > 0（圆角外空白要透出底色 / 背景源）
        '   2) BackColor1 (背景颜色) 半透明 → 自身实色填充层有 alpha
        '   3) MyBase.BackColor 半透明 → 走 .NET 自身透明逻辑（OnPaintBackground 仍交回基类）
        '      —— 但 BackgroundSource 已设置时跳过 BackColor 逻辑直接画背景穿透。
        Return 边框圆角半径 > 0 OrElse 背景颜色.A < 255 OrElse
               _backgroundSource IsNot Nothing OrElse MyBase.BackColor.A < 255
    End Function

    Private Function 需要D2D绘制() As Boolean
        If _backgroundSource IsNot Nothing Then Return True
        If _image IsNot Nothing Then Return True
        If 背景颜色.A > 0 Then Return True
        If 遮罩颜色.A > 0 Then Return True
        If 边框圆角半径 > 0 Then Return True
        If 边框宽度 > 0 AndAlso 边框颜色.A > 0 Then Return True
        If MyBase.BackColor.A > 0 AndAlso MyBase.BackColor.A < 255 Then Return True
        If _showVScroll OrElse _showHScroll Then Return True
        Return False
    End Function

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' V2 契约：
        '   • BackgroundSource 已设置 → 跳过 BackColor 整个逻辑，背景由 OnPaint 内 BackgroundPenetrationV2 绘制；
        '   • 否则一律走 .NET 自身透明逻辑（基类负责合成父级背景或填充不透明 BackColor）。
        '     半透明 BackColor 的"颜色覆盖"会在 OnPaint 的背景层上再叠加一次。
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Me.Width < 1 OrElse Me.Height < 1 Then Return

        If Not 需要D2D绘制() Then
            MyBase.OnPaint(e)
            Return
        End If

        Dim ssaa As Integer = D2DHelperV2.GetEffectiveSsaaScale(超采样倍率)

        Using scope = D2DHelperV2.BeginPaint(e, Me, ssaa)
            If scope Is Nothing Then Return  ' 设计期或无 Form 上下文，直接跳过自绘

            ' 1) 背景层（1× 直绘）：
            '    • 若设置 BackgroundSource → 直接画穿透底图（BackColor 被跳过，OnPaintBackground 也已 Return）；
            '    • 否则若 MyBase.BackColor 半透明 → 基类 OnPaintBackground 已把父级背景画到 DC RT 上，
            '      这里再叠一层 BackColor 作为半透明遮罩（"颜色覆盖在上面"）。
            If _backgroundSource IsNot Nothing Then
                BackgroundPenetrationV2.PaintBackground(Me, scope, _backgroundSource)
            ElseIf MyBase.BackColor.A > 0 AndAlso MyBase.BackColor.A < 255 Then
                Dim bgLayer = scope.BackgroundLayer
                Dim brush = scope.Compositor.BrushCache.[Get](bgLayer, MyBase.BackColor)
                If brush IsNot Nothing Then
                    bgLayer.FillRectangle(D2DGlobals.ToD2DRect(New RectangleF(0, 0, Me.Width, Me.Height)), brush)
                End If
            End If

            ' 2) 图形层（SSAA）：背景颜色 / 图片 / 遮罩 / 边框 / 滚动条
            Dim gRT As ID2D1RenderTarget = scope.GraphicsLayer
            绘制背景与边框_D2D(gRT, scope.Compositor, scope.SsaaScale)
            绘制垂直滚动条_D2D(gRT, scope.Compositor.BrushCache)
            绘制水平滚动条_D2D(gRT, scope.Compositor.BrushCache)

            scope.FlushGraphics()
        End Using
    End Sub

    ''' <summary>
    ''' （已废弃）透明背景贴底图的 GDI 路径。V2 不再使用，保留方法占位避免外部引用编译失败。
    ''' </summary>
    Private Sub 绘制父容器背景(g As Graphics)
        ' no-op
    End Sub

    Private Sub 绘制背景与边框_D2D(rt As ID2D1RenderTarget, compositor As WindowCompositor, renderScale As Integer)
        Dim s As Single = DpiScale()
        Dim brushCache = compositor?.BrushCache
        Dim boundsRect As New RectangleF(0, 0, Me.Width, Me.Height)
        If 边框宽度 > 0 Then
            Dim half As Single = 边框宽度 * s / 2.0F
            boundsRect.Inflate(-half, -half)
        End If

        Dim scaledRadius As Single = 边框圆角半径 * s
        Dim 是否有圆角 As Boolean = 边框圆角半径 > 0
        ' 控件本体的 BackColor 在新契约下作为"带颜色的半透明遮罩"层，叠加在采样底图之上、
        ' BackColor1（=背景颜色）之下；A=0 时退化为不绘制。详见 TransparentBackgroundCache 顶部契约。
        Dim backColorMask As Color = MyBase.BackColor

        If 是否有圆角 Then
            Using geo As ID2D1Geometry = RectangleRenderer.创建圆角矩形几何(boundsRect, scaledRadius, 圆角位置)
                If backColorMask.A > 0 AndAlso backColorMask.A < 255 Then
                    RectangleRenderer.绘制圆角背景_D2D(rt, geo, boundsRect, backColorMask, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
                End If
                If 背景颜色.A > 0 Then
                    RectangleRenderer.绘制圆角背景_D2D(rt, geo, boundsRect, 背景颜色, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
                End If
                绘制背景图片_D2D(rt, compositor, boundsRect, geo, renderScale)
                If 遮罩颜色.A > 0 Then
                    RectangleRenderer.绘制圆角背景_D2D(rt, geo, boundsRect, 遮罩颜色, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
                End If
                If 边框颜色.A > 0 AndAlso 边框宽度 > 0 Then
                    RectangleRenderer.绘制圆角边框_D2D(rt, geo, 边框颜色, 边框宽度 * s, brushCache)
                End If
            End Using
        Else
            If backColorMask.A > 0 AndAlso backColorMask.A < 255 Then
                RectangleRenderer.绘制矩形背景_D2D(rt, boundsRect, backColorMask, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
            End If
            If 背景颜色.A > 0 Then
                RectangleRenderer.绘制矩形背景_D2D(rt, boundsRect, 背景颜色, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
            End If
            绘制背景图片_D2D(rt, compositor, boundsRect, Nothing, renderScale)
            If 遮罩颜色.A > 0 Then
                RectangleRenderer.绘制矩形背景_D2D(rt, boundsRect, 遮罩颜色, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
            End If
            If 边框颜色.A > 0 AndAlso 边框宽度 > 0 Then
                RectangleRenderer.绘制矩形边框_D2D(rt, boundsRect, 边框颜色, 边框宽度 * s, brushCache)
            End If
        End If
    End Sub

    ''' <summary>D2D 绘制背景图片：按 ImageMode 计算目标矩形，圆角下用几何裁切。</summary>
    Private Sub 绘制背景图片_D2D(rt As ID2D1RenderTarget, compositor As WindowCompositor, area As RectangleF, geo As ID2D1Geometry, renderScale As Integer)
        If _image Is Nothing Then Return
        Dim srcW As Integer = _image.Width
        Dim srcH As Integer = _image.Height
        If srcW <= 0 OrElse srcH <= 0 OrElse area.Width <= 0 OrElse area.Height <= 0 Then Return

        Dim destRect As RectangleF = 计算背景图片目标矩形(area, srcW, srcH, _imageFillMode)
        If destRect.Width <= 0 OrElse destRect.Height <= 0 Then Return

        Dim visibleDest As RectangleF = RectangleF.Intersect(destRect, area)
        If visibleDest.Width <= 0 OrElse visibleDest.Height <= 0 Then Return

        Dim ratio As Single = destRect.Width / srcW
        Dim sourceRect As New Rectangle(0, 0, srcW, srcH)
        Dim drawBitmap As ID2D1Bitmap = Nothing
        Dim drawDestRect As RectangleF = destRect
        Dim drawSourceRect As RectangleF = New RectangleF(0, 0, srcW, srcH)
        Dim shouldDisposeBitmap As Boolean = False

        Try
            If _animatedImage Is Nothing AndAlso ratio < 0.999F Then
                sourceRect = 计算可见源矩形(destRect, visibleDest, ratio, srcW, srcH)
                If Not sourceRect.IsEmpty Then
                    Dim scaledSize As Size = 计算缩放位图尺寸(visibleDest, renderScale)
                    drawBitmap = 获取高质量缩放背景位图_D2D(rt, _image, sourceRect, scaledSize)
                    If drawBitmap IsNot Nothing Then
                        drawDestRect = visibleDest
                        drawSourceRect = New RectangleF(0, 0, scaledSize.Width, scaledSize.Height)
                    End If
                End If
            End If

            If drawBitmap Is Nothing Then
                drawBitmap = 获取背景图片位图_D2D(rt, compositor, shouldDisposeBitmap)
                If drawBitmap Is Nothing Then Return
            End If

            Dim hasMask As Boolean = geo IsNot Nothing
            If hasMask Then D2DGlobals.PushGeometryClip(rt, geo, area)
            Try
                rt.DrawBitmap(drawBitmap, D2DGlobals.ToD2DRect(drawDestRect), 1.0F, BitmapInterpolationMode.Linear, D2DGlobals.ToD2DRect(drawSourceRect))
            Finally
                If hasMask Then rt.PopLayer()
            End Try
        Finally
            If shouldDisposeBitmap Then
                Try : drawBitmap.Dispose() : Catch : End Try
            End If
        End Try
    End Sub

    Private Sub 绘制垂直滚动条_D2D(rt As ID2D1RenderTarget, brushCache As D2DGlobals.SolidColorBrushCache)
        If _vScrollBar.TrackRect.IsEmpty Then Return
        Dim s As Single = DpiScale()
        _vScrollBar.Draw_D2D(rt, Me.Width, Me.Height, CInt(Math.Round(边框宽度 * s)), CInt(Math.Round(边框圆角半径 * s)),
            CInt(Math.Round(滚动条宽度 * s)), 滚动条轨道颜色, 滚动条滑块颜色, 滚动条悬停颜色, brushCache)
    End Sub

    Private Sub 绘制水平滚动条_D2D(rt As ID2D1RenderTarget, brushCache As D2DGlobals.SolidColorBrushCache)
        If _hScrollBar.TrackRect.IsEmpty Then Return
        Dim s As Single = DpiScale()
        _hScrollBar.DrawHorizontal_D2D(rt, Me.Width, Me.Height, CInt(Math.Round(边框宽度 * s)), CInt(Math.Round(边框圆角半径 * s)),
            CInt(Math.Round(滚动条宽度 * s)), 滚动条轨道颜色, 滚动条滑块颜色, 滚动条悬停颜色, brushCache)
    End Sub

#End Region

#Region "鼠标交互"

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)

        If _vScrollBar.IsDragging Then
            Dim viewport As Size = 获取有效视口大小()
            Dim newOff As Integer = _vScrollBar.DragMove(e.Y, _contentSize.Height, viewport.Height)
            If newOff <> _vScrollOffset Then
                _vScrollOffset = newOff
                快速滚动更新()
                OuterToInnerRefreshScheduler.RequestFull(Me)
            End If
            Return
        End If

        If _hScrollBar.IsDragging Then
            Dim viewport As Size = 获取有效视口大小()
            Dim newOff As Integer = _hScrollBar.DragMoveHorizontal(e.X, _contentSize.Width, viewport.Width)
            If newOff <> _hScrollOffset Then
                _hScrollOffset = newOff
                快速滚动更新()
                OuterToInnerRefreshScheduler.RequestFull(Me)
            End If
            Return
        End If

        Dim needInvalidate As Boolean = False
        If _vScrollBar.UpdateHover(e.Location) Then needInvalidate = True
        If _hScrollBar.UpdateHover(e.Location) Then needInvalidate = True
        If needInvalidate Then OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)

        Dim viewport As Size = 获取有效视口大小()

        If _vScrollBar.BeginDrag(e.Location, _vScrollOffset) Then Return
        If Not _vScrollBar.TrackRect.IsEmpty Then
            Dim newOff = _vScrollBar.TrackClick(e.Location, _vScrollOffset, _contentSize.Height, viewport.Height)
            If newOff <> _vScrollOffset Then
                _vScrollOffset = newOff
                快速滚动更新()
                OuterToInnerRefreshScheduler.RequestFull(Me)
                Return
            End If
        End If

        If _hScrollBar.BeginDragHorizontal(e.Location, _hScrollOffset) Then Return
        If Not _hScrollBar.TrackRect.IsEmpty Then
            Dim newHOff = _hScrollBar.TrackClickHorizontal(e.Location, _hScrollOffset, _contentSize.Width, viewport.Width)
            If newHOff <> _hScrollOffset Then
                _hScrollOffset = newHOff
                快速滚动更新()
                OuterToInnerRefreshScheduler.RequestFull(Me)
                Return
            End If
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        _vScrollBar.EndDrag()
        _hScrollBar.EndDrag()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        Dim needInvalidate As Boolean = False
        If _vScrollBar.ResetHover() Then needInvalidate = True
        If _hScrollBar.ResetHover() Then needInvalidate = True
        If needInvalidate Then OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)

        Dim viewport As Size = 获取有效视口大小()

        If (Control.ModifierKeys And Keys.Shift) = Keys.Shift Then
            If _showHScroll Then
                Dim newHOff = ScrollBarRenderer.HandleHorizontalWheel(e.Delta, _hScrollOffset, _contentSize.Width, viewport.Width, 水平滚动步长)
                If newHOff <> _hScrollOffset Then
                    _hScrollOffset = newHOff
                    快速滚动更新()
                    OuterToInnerRefreshScheduler.RequestFull(Me)
                End If
            End If
            Return
        End If

        If _showVScroll Then
            Dim newOff = ScrollBarRenderer.HandleHorizontalWheel(e.Delta, _vScrollOffset, _contentSize.Height, viewport.Height, 垂直滚动步长)
            If newOff <> _vScrollOffset Then
                _vScrollOffset = newOff
                快速滚动更新()
                OuterToInnerRefreshScheduler.RequestFull(Me)
            End If
        End If
    End Sub

#End Region

#Region "生命周期"

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        更新图片动画可见性订阅()
        更新图片动画运行状态()
    End Sub

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        解除背景穿透消费者()
        停止图片动画计时器(False)
        更新图片动画宿主窗体订阅(Nothing)
        清除图片动画可见性订阅()
        MyBase.OnHandleDestroyed(e)
    End Sub

    Protected Overrides Sub OnVisibleChanged(e As EventArgs)
        MyBase.OnVisibleChanged(e)
        If Not Me.Visible Then 解除背景穿透消费者()
        更新图片动画运行状态()
    End Sub

    Protected Overrides Sub OnParentChanged(e As EventArgs)
        MyBase.OnParentChanged(e)
        If Me.Parent Is Nothing Then 解除背景穿透消费者()
        更新图片动画可见性订阅()
        更新图片动画运行状态()
    End Sub

    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        _contentSizeDirty = True
        更新滚动区域()
    End Sub

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        清除图片缓存()
        _contentSizeDirty = True
        更新滚动区域()
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub

    Protected Overrides Sub OnDpiChangedBeforeParent(e As EventArgs)
        _inDpiChange = True
        MyBase.OnDpiChangedBeforeParent(e)
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)

        Dim newDpi As Integer = Me.DeviceDpi
        Dim oldDpi As Integer = _lastDeviceDpi
        _lastDeviceDpi = newDpi

        If oldDpi > 0 AndAlso oldDpi <> newDpi Then
            Dim ratio As Single = CSng(newDpi) / CSng(oldDpi)

            ' 按 DPI 比例缩放滚动偏移（像素坐标）
            _vScrollOffset = CInt(Math.Round(_vScrollOffset * ratio))
            _hScrollOffset = CInt(Math.Round(_hScrollOffset * ratio))

            ' 清空设计坐标缓存，由框架缩放后的子控件位置重新捕获
            _childLayouts.Clear()
        End If

        清除图片缓存()
        _contentSizeDirty = True
        _inDpiChange = False
        更新滚动区域()
        Me.PerformLayout()
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        _contentSizeDirty = True
        更新滚动区域()
        Me.PerformLayout()
        D2DHelperV2.RefreshFontDependentRendering(Me)
    End Sub

    Protected Overrides Sub OnControlAdded(e As ControlEventArgs)
        MyBase.OnControlAdded(e)
        If e.Control.Dock = DockStyle.None Then
            Dim ep As Padding = 获取有效内边距()
            Dim tag As New ChildLayoutInfo With {
                .DesignLeft = e.Control.Left - ep.Left + _hScrollOffset,
                .DesignTop = e.Control.Top - ep.Top + _vScrollOffset
            }
            _childLayouts(e.Control) = tag
        End If
        AddHandler e.Control.SizeChanged, AddressOf 子控件布局变更
        AddHandler e.Control.LocationChanged, AddressOf 子控件位置变更
        _contentSizeDirty = True
        更新滚动区域()
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub

    Protected Overrides Sub OnControlRemoved(e As ControlEventArgs)
        MyBase.OnControlRemoved(e)
        _childLayouts.Remove(e.Control)
        RemoveHandler e.Control.SizeChanged, AddressOf 子控件布局变更
        RemoveHandler e.Control.LocationChanged, AddressOf 子控件位置变更
        _contentSizeDirty = True
        更新滚动区域()
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub

    Private _suppressLocationSync As Boolean = False

    Private Sub 子控件布局变更(sender As Object, e As EventArgs)
        If _inDpiChange Then Return
        ' 即使处于 _inScrollUpdate 重入窗口（例如 MyBase.OnLayout 过程中子控件 SizeChanged），
        ' 也必须置脏，否则随后的 更新滚动区域() 会用陈旧 _contentSize 误判 needV/needH。
        _contentSizeDirty = True
        If _inScrollUpdate Then Return
        更新滚动区域()
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub

    Private Sub 子控件位置变更(sender As Object, e As EventArgs)
        If _suppressLocationSync OrElse _inDpiChange Then Return
        Dim ctrl = DirectCast(sender, Control)
        If ctrl.Dock <> DockStyle.None Then Return
        If _layoutMode = LayoutModeEnum.Flow Then
            ' 流式模式下子控件位置由布局决定，用户拖动后重新排布
            Me.PerformLayout()
            Return
        End If
        Dim tag As ChildLayoutInfo = Nothing
        If Not _childLayouts.TryGetValue(ctrl, tag) Then Return
        Dim ep As Padding = 获取有效内边距()
        tag.DesignLeft = ctrl.Left - ep.Left + _hScrollOffset
        tag.DesignTop = ctrl.Top - ep.Top + _vScrollOffset
    End Sub

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        _inScrollUpdate = True
        Try
            MyBase.OnLayout(levent)
        Finally
            _inScrollUpdate = False
        End Try

        ' DPI 变化期间跳过设计坐标捕获和滚动区域更新，由 OnDpiChangedAfterParent 统一处理
        If _inDpiChange Then Return

        ' 兜底：基类布局过程中子控件的 SizeChanged 在 _inScrollUpdate 守卫下未递归更新，
        ' 但脏标记已在 子控件布局变更() 内被置 True。这里再显式置一次脏，确保即使
        ' 将来调整重入逻辑，更新滚动区域() 也会重新计算内容大小，避免使用陈旧 _contentSize。
        _contentSizeDirty = True

        If _layoutMode = LayoutModeEnum.Flow Then
            执行流式排布()
        Else
            ' 布局引擎完成后，捕获停靠控件的设计坐标（此时它们处于未偏移的视口位置）
            Dim ep As Padding = 获取有效内边距()
            For Each ctrl As Control In Me.Controls
                If ctrl.Dock = DockStyle.None Then Continue For
                If Not ctrl.Visible Then Continue For
                Dim tag As ChildLayoutInfo = Nothing
                If Not _childLayouts.TryGetValue(ctrl, tag) Then
                    tag = New ChildLayoutInfo()
                    _childLayouts(ctrl) = tag
                End If
                tag.DesignLeft = ctrl.Left - ep.Left
                tag.DesignTop = ctrl.Top - ep.Top
            Next
        End If

        更新滚动区域()

        If AutoSize Then
            PerformAutoSize()
        End If
    End Sub

    ''' <summary>按 FlowDirection/WrapContents 将子控件顺序摆放到 _childLayouts，供后续滚动/偏移管线使用。
    ''' Dock != None 的子控件不参与流式排布。</summary>
    Private Sub 执行流式排布()
        Dim s As Single = DpiScale()
        Dim scaledScrollBarWidth As Integer = CInt(Math.Round(滚动条宽度 * s))
        Dim sbReserve As Integer = scaledScrollBarWidth + ScrollBarRenderer.Margin
        Dim ep As Padding = 获取有效内边距()

        ' 稳定的换行/换列边界：使用可能显示滚动条后的视口尺寸，避免布局-滚动条反馈环。
        Dim wrapWidth As Integer = Math.Max(1, Me.Width - ep.Horizontal -
            If((滚动模式 And ScrollMode.Vertical) <> 0, sbReserve, 0))
        Dim wrapHeight As Integer = Math.Max(1, Me.Height - ep.Vertical -
            If((滚动模式 And ScrollMode.Horizontal) <> 0, sbReserve, 0))

        Dim x As Integer = 0
        Dim y As Integer = 0
        Dim rowMaxH As Integer = 0
        Dim colMaxW As Integer = 0

        ' 与原版 FlowLayoutPanel 行为一致：按 Controls 集合索引 0 → Count-1 正序排布。
        ' Controls.Add 把新控件追加到末尾（Count-1，Z 序顶层），BringToFront 也移到 Count-1，
        ' 因此新加入或 BringToFront 的控件会落在流式末尾；SendToBack 的则落在最前。
        For Each ctrl As Control In Me.Controls
            If ctrl.Dock <> DockStyle.None Then Continue For
            If Not ctrl.Visible Then Continue For

            Dim m As Padding = ctrl.Margin
            Dim blockW As Integer = m.Left + ctrl.Width + m.Right
            Dim blockH As Integer = m.Top + ctrl.Height + m.Bottom

            If _flowDirection = FlowDirectionEnum.LeftToRight Then
                If _wrapContents AndAlso x > 0 AndAlso (x + blockW) > wrapWidth Then
                    x = 0
                    y += rowMaxH
                    rowMaxH = 0
                End If
            Else ' TopDown
                If _wrapContents AndAlso y > 0 AndAlso (y + blockH) > wrapHeight Then
                    y = 0
                    x += colMaxW
                    colMaxW = 0
                End If
            End If

            Dim tag As ChildLayoutInfo = Nothing
            If Not _childLayouts.TryGetValue(ctrl, tag) Then
                tag = New ChildLayoutInfo()
                _childLayouts(ctrl) = tag
            End If
            tag.DesignLeft = x + m.Left
            tag.DesignTop = y + m.Top

            If _flowDirection = FlowDirectionEnum.LeftToRight Then
                x += blockW
                If blockH > rowMaxH Then rowMaxH = blockH
            Else
                y += blockH
                If blockW > colMaxW Then colMaxW = blockW
            End If
        Next

        _contentSizeDirty = True
    End Sub

#End Region

#Region "AutoSize"

    <Category("LakeUI"), Description("是否根据子控件自动调整面板大小"), DefaultValue(False), Browsable(True)>
    Public Overrides Property AutoSize As Boolean
        Get
            Return MyBase.AutoSize
        End Get
        Set(value As Boolean)
            MyBase.AutoSize = value
            If value Then PerformAutoSize()
        End Set
    End Property

    Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
        Dim content As Size = 计算内容总大小()
        Dim ep As Padding = 获取有效内边距()
        Return New Size(
            content.Width + ep.Horizontal,
            content.Height + ep.Vertical)
    End Function

    Private Sub PerformAutoSize()
        Dim preferred As Size = GetPreferredSize(Size.Empty)
        If Me.Size <> preferred Then
            Me.Size = preferred
        End If
    End Sub

#End Region

#Region "公开方法"

    ''' <summary>滚动到指定的像素偏移位置。</summary>
    Public Sub ScrollTo(horizontalOffset As Integer, verticalOffset As Integer)
        _hScrollOffset = Math.Max(0, horizontalOffset)
        _vScrollOffset = Math.Max(0, verticalOffset)
        更新滚动区域()
        OuterToInnerRefreshScheduler.RequestFull(Me)
    End Sub

    ''' <summary>获取当前垂直滚动偏移。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property VerticalScrollOffset As Integer
        Get
            Return _vScrollOffset
        End Get
    End Property

    ''' <summary>获取当前水平滚动偏移。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property HorizontalScrollOffset As Integer
        Get
            Return _hScrollOffset
        End Get
    End Property

#End Region

End Class
