Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Globalization
Imports System.Numerics
Imports D2D = Vortice.Direct2D1
Imports DW = Vortice.DirectWrite

<DefaultEvent("ChartChanged")>
Public Class Ultra2DChart
    Implements V3_IGpuRenderable, V3_IGpuInvalidationSource

    Public Event ChartChanged As EventHandler

#Region "内部类型"
    Public Enum ChartSeriesTypeEnum
        Column
        Line
    End Enum

    Public Enum MarkerShapeEnum
        None
        Circle
        Square
        Diamond
    End Enum

    Public Enum LegendPositionEnum
        None
        Top
        Bottom
        Left
        Right
    End Enum

    Public Enum AxisRangeModeEnum
        Auto
        Fixed
    End Enum

    Public Enum SeriesValueLabelModeEnum
        Inherit
        Show
        Hide
    End Enum

    <TypeConverter(GetType(ExpandableObjectConverter))>
    Public Class ChartPoint
        Private _value As Double = 0.0
        Private _label As String = ""
        Private _color As Color = Color.Empty

        Friend Property OwnerSeries As ChartSeries

        <DefaultValue(0.0), Description("数据值。设置为 Double.NaN 时视为缺失值，不参与绘制。")>
        Public Property Value As Double
            Get
                Return _value
            End Get
            Set(value As Double)
                If _value.Equals(value) Then Return
                _value = value
                OwnerSeries?.NotifyDataChanged()
            End Set
        End Property

        <DefaultValue(""), Description("点标签；分类轴未提供对应文本时可作为横轴标签。")>
        Public Property Label As String
            Get
                Return _label
            End Get
            Set(value As String)
                value = If(value, "")
                If String.Equals(_label, value, StringComparison.Ordinal) Then Return
                _label = value
                OwnerSeries?.NotifyDataChanged()
            End Set
        End Property

        <DefaultValue(GetType(Color), ""), Description("单点颜色；Empty 时使用所属系列颜色。")>
        Public Property Color As Color
            Get
                Return _color
            End Get
            Set(value As Color)
                If _color = value Then Return
                _color = value
                OwnerSeries?.NotifyStyleChanged()
            End Set
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Tag As Object = Nothing

        Public Sub New()
        End Sub

        Public Sub New(value As Double)
            _value = value
        End Sub

        Public Sub New(value As Double, label As String)
            _value = value
            _label = If(label, "")
        End Sub

        Public Overrides Function ToString() As String
            If String.IsNullOrEmpty(Label) Then Return Value.ToString(CultureInfo.CurrentCulture)
            Return $"{Label}: {Value.ToString(CultureInfo.CurrentCulture)}"
        End Function
    End Class

    Public Class ChartPointCollection
        Inherits Collection(Of ChartPoint)

        Private ReadOnly _ownerSeries As ChartSeries

        Friend Sub New(ownerSeries As ChartSeries)
            _ownerSeries = ownerSeries
        End Sub

        Public Overloads Sub AddRange(collection As IEnumerable(Of ChartPoint))
            If collection Is Nothing Then Return
            _ownerSeries.Owner?.BeginUpdate()
            Try
                For Each chartPoint In collection
                    Add(chartPoint)
                Next
            Finally
                _ownerSeries.Owner?.EndUpdate()
            End Try
        End Sub

        Public Overloads Sub AddRange(ParamArray values() As Double)
            If values Is Nothing Then Return
            _ownerSeries.Owner?.BeginUpdate()
            Try
                For Each value In values
                    Add(New ChartPoint(value))
                Next
            Finally
                _ownerSeries.Owner?.EndUpdate()
            End Try
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, chartPoint As ChartPoint)
            If chartPoint Is Nothing Then chartPoint = New ChartPoint()
            chartPoint.OwnerSeries = _ownerSeries
            MyBase.InsertItem(index, chartPoint)
            _ownerSeries.NotifyDataChanged()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            Dim oldItem = Me(index)
            If oldItem IsNot Nothing AndAlso Object.ReferenceEquals(oldItem.OwnerSeries, _ownerSeries) Then oldItem.OwnerSeries = Nothing
            MyBase.RemoveItem(index)
            _ownerSeries.NotifyDataChanged()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, chartPoint As ChartPoint)
            Dim oldItem = Me(index)
            If oldItem IsNot Nothing AndAlso Object.ReferenceEquals(oldItem.OwnerSeries, _ownerSeries) Then oldItem.OwnerSeries = Nothing
            If chartPoint Is Nothing Then chartPoint = New ChartPoint()
            chartPoint.OwnerSeries = _ownerSeries
            MyBase.SetItem(index, chartPoint)
            _ownerSeries.NotifyDataChanged()
        End Sub

        Protected Overrides Sub ClearItems()
            For Each chartPoint In Me
                If chartPoint IsNot Nothing AndAlso Object.ReferenceEquals(chartPoint.OwnerSeries, _ownerSeries) Then chartPoint.OwnerSeries = Nothing
            Next
            MyBase.ClearItems()
            _ownerSeries.NotifyDataChanged()
        End Sub
    End Class

    <TypeConverter(GetType(ExpandableObjectConverter))>
    Public Class ChartSeries
        Private _name As String = "Series"
        Private _chartType As ChartSeriesTypeEnum = ChartSeriesTypeEnum.Column
        Private _visible As Boolean = True
        Private _color As Color = Color.Empty
        Private _gradientColor As Color = Color.Empty
        Private _borderColor As Color = Color.Empty
        Private _borderThickness As Single = 0.0F
        Private _lineThickness As Single = 2.0F
        Private _markerShape As MarkerShapeEnum = MarkerShapeEnum.Circle
        Private _markerSize As Single = 7.0F
        Private _markerFillColor As Color = Color.Empty
        Private _markerBorderColor As Color = Color.Empty
        Private _markerBorderThickness As Single = 1.0F
        Private _showValueLabels As SeriesValueLabelModeEnum = SeriesValueLabelModeEnum.Inherit
        Private _valueLabelFormat As String = ""
        Private _valueLabelColor As Color = Color.Empty
        Private _columnCornerRadius As Single = 3.0F

        Friend Property Owner As Ultra2DChart

        Public Sub New()
            Points = New ChartPointCollection(Me)
        End Sub

        Public Sub New(name As String, chartType As ChartSeriesTypeEnum, ParamArray values() As Double)
            Me.New()
            _name = If(name, "")
            _chartType = chartType
            Points.AddRange(values)
        End Sub

        Friend Sub NotifyDataChanged()
            Owner?.NotifyDataChanged()
        End Sub

        Friend Sub NotifyStyleChanged()
            Owner?.NotifyStyleChanged()
        End Sub

        <DefaultValue("Series"), Description("系列名称，用于图例显示。")>
        Public Property Name As String
            Get
                Return _name
            End Get
            Set(value As String)
                value = If(value, "")
                If String.Equals(_name, value, StringComparison.Ordinal) Then Return
                _name = value
                NotifyStyleChanged()
            End Set
        End Property

        <DefaultValue(GetType(ChartSeriesTypeEnum), "Column"), Description("系列图表类型。")>
        Public Property ChartType As ChartSeriesTypeEnum
            Get
                Return _chartType
            End Get
            Set(value As ChartSeriesTypeEnum)
                If _chartType = value Then Return
                _chartType = value
                NotifyStyleChanged()
            End Set
        End Property

        <DefaultValue(True), Description("是否显示该系列。")>
        Public Property Visible As Boolean
            Get
                Return _visible
            End Get
            Set(value As Boolean)
                If _visible = value Then Return
                _visible = value
                NotifyDataChanged()
            End Set
        End Property

        <DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
         Editor("System.ComponentModel.Design.CollectionEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing"),
         Description("数据点集合。")>
        Public ReadOnly Property Points As ChartPointCollection

        <DefaultValue(GetType(Color), ""), Description("系列主颜色；Empty 时使用控件调色板。")>
        Public Property Color As Color
            Get
                Return _color
            End Get
            Set(value As Color)
                If _color = value Then Return
                _color = value
                NotifyStyleChanged()
            End Set
        End Property

        <DefaultValue(GetType(Color), ""), Description("柱状图渐变颜色；Empty 时使用纯色。")>
        Public Property GradientColor As Color
            Get
                Return _gradientColor
            End Get
            Set(value As Color)
                If _gradientColor = value Then Return
                _gradientColor = value
                NotifyStyleChanged()
            End Set
        End Property

        <DefaultValue(GetType(Color), ""), Description("柱状图边框颜色；Empty 时使用系列主色。")>
        Public Property BorderColor As Color
            Get
                Return _borderColor
            End Get
            Set(value As Color)
                If _borderColor = value Then Return
                _borderColor = value
                NotifyStyleChanged()
            End Set
        End Property

        <DefaultValue(0.0F), Description("柱状图边框粗细。")>
        Public Property BorderThickness As Single
            Get
                Return _borderThickness
            End Get
            Set(value As Single)
                value = Math.Max(0.0F, value)
                If _borderThickness = value Then Return
                _borderThickness = value
                NotifyStyleChanged()
            End Set
        End Property

        <DefaultValue(2.0F), Description("折线粗细。")>
        Public Property LineThickness As Single
            Get
                Return _lineThickness
            End Get
            Set(value As Single)
                value = Math.Max(0.1F, value)
                If _lineThickness = value Then Return
                _lineThickness = value
                NotifyStyleChanged()
            End Set
        End Property

        <DefaultValue(GetType(MarkerShapeEnum), "Circle"), Description("折线点标记形状。")>
        Public Property MarkerShape As MarkerShapeEnum
            Get
                Return _markerShape
            End Get
            Set(value As MarkerShapeEnum)
                If _markerShape = value Then Return
                _markerShape = value
                NotifyStyleChanged()
            End Set
        End Property

        <DefaultValue(7.0F), Description("折线点标记大小。")>
        Public Property MarkerSize As Single
            Get
                Return _markerSize
            End Get
            Set(value As Single)
                value = Math.Max(0.0F, value)
                If _markerSize = value Then Return
                _markerSize = value
                NotifyStyleChanged()
            End Set
        End Property

        <DefaultValue(GetType(Color), ""), Description("折线点填充色；Empty 时使用系列主色。")>
        Public Property MarkerFillColor As Color
            Get
                Return _markerFillColor
            End Get
            Set(value As Color)
                If _markerFillColor = value Then Return
                _markerFillColor = value
                NotifyStyleChanged()
            End Set
        End Property

        <DefaultValue(GetType(Color), ""), Description("折线点边框颜色；Empty 时使用绘图区背景色。")>
        Public Property MarkerBorderColor As Color
            Get
                Return _markerBorderColor
            End Get
            Set(value As Color)
                If _markerBorderColor = value Then Return
                _markerBorderColor = value
                NotifyStyleChanged()
            End Set
        End Property

        <DefaultValue(1.0F), Description("折线点边框粗细。")>
        Public Property MarkerBorderThickness As Single
            Get
                Return _markerBorderThickness
            End Get
            Set(value As Single)
                value = Math.Max(0.0F, value)
                If _markerBorderThickness = value Then Return
                _markerBorderThickness = value
                NotifyStyleChanged()
            End Set
        End Property

        <DefaultValue(GetType(SeriesValueLabelModeEnum), "Inherit"), Description("该系列值标签显示模式。")>
        Public Property ShowValueLabels As SeriesValueLabelModeEnum
            Get
                Return _showValueLabels
            End Get
            Set(value As SeriesValueLabelModeEnum)
                If _showValueLabels = value Then Return
                _showValueLabels = value
                NotifyStyleChanged()
            End Set
        End Property

        <DefaultValue(""), Description("该系列值标签格式；为空时使用控件 ValueLabelFormat。")>
        Public Property ValueLabelFormat As String
            Get
                Return _valueLabelFormat
            End Get
            Set(value As String)
                value = If(value, "")
                If String.Equals(_valueLabelFormat, value, StringComparison.Ordinal) Then Return
                _valueLabelFormat = value
                NotifyStyleChanged()
            End Set
        End Property

        <DefaultValue(GetType(Color), ""), Description("该系列值标签颜色；Empty 时使用控件 ValueLabelColor。")>
        Public Property ValueLabelColor As Color
            Get
                Return _valueLabelColor
            End Get
            Set(value As Color)
                If _valueLabelColor = value Then Return
                _valueLabelColor = value
                NotifyStyleChanged()
            End Set
        End Property

        <DefaultValue(3.0F), Description("柱状图圆角半径。")>
        Public Property ColumnCornerRadius As Single
            Get
                Return _columnCornerRadius
            End Get
            Set(value As Single)
                value = Math.Max(0.0F, value)
                If _columnCornerRadius = value Then Return
                _columnCornerRadius = value
                NotifyStyleChanged()
            End Set
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Tag As Object = Nothing

        Public Overrides Function ToString() As String
            Return If(String.IsNullOrEmpty(Name), "(未命名系列)", $"{Name} ({ChartType})")
        End Function
    End Class

    Public Class ChartSeriesCollection
        Inherits Collection(Of ChartSeries)

        Private ReadOnly _owner As Ultra2DChart

        Friend Sub New(owner As Ultra2DChart)
            _owner = owner
        End Sub

        Public Overloads Sub AddRange(collection As IEnumerable(Of ChartSeries))
            If collection Is Nothing Then Return
            _owner.BeginUpdate()
            Try
                For Each seriesItem In collection
                    Add(seriesItem)
                Next
            Finally
                _owner.EndUpdate()
            End Try
        End Sub

        Public Overloads Sub AddRange(ParamArray series() As ChartSeries)
            AddRange(CType(series, IEnumerable(Of ChartSeries)))
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, seriesItem As ChartSeries)
            If seriesItem Is Nothing Then seriesItem = New ChartSeries()
            seriesItem.Owner = _owner
            For Each point In seriesItem.Points
                If point IsNot Nothing Then point.OwnerSeries = seriesItem
            Next
            MyBase.InsertItem(index, seriesItem)
            _owner.NotifyDataChanged()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            Dim oldItem = Me(index)
            Detach(oldItem)
            MyBase.RemoveItem(index)
            _owner.NotifyDataChanged()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, seriesItem As ChartSeries)
            Detach(Me(index))
            If seriesItem Is Nothing Then seriesItem = New ChartSeries()
            seriesItem.Owner = _owner
            For Each point In seriesItem.Points
                If point IsNot Nothing Then point.OwnerSeries = seriesItem
            Next
            MyBase.SetItem(index, seriesItem)
            _owner.NotifyDataChanged()
        End Sub

        Protected Overrides Sub ClearItems()
            For Each seriesItem In Me
                Detach(seriesItem)
            Next
            MyBase.ClearItems()
            _owner.NotifyDataChanged()
        End Sub

        Private Shared Sub Detach(seriesItem As ChartSeries)
            If seriesItem Is Nothing Then Return
            seriesItem.Owner = Nothing
            For Each point In seriesItem.Points
                If point IsNot Nothing AndAlso Object.ReferenceEquals(point.OwnerSeries, seriesItem) Then point.OwnerSeries = Nothing
            Next
        End Sub
    End Class

    Public Class ChartCategoryCollection
        Inherits Collection(Of String)

        Private ReadOnly _owner As Ultra2DChart

        Friend Sub New(owner As Ultra2DChart)
            _owner = owner
        End Sub

        Public Overloads Sub AddRange(collection As IEnumerable(Of String))
            If collection Is Nothing Then Return
            _owner.BeginUpdate()
            Try
                For Each labelText In collection
                    Add(labelText)
                Next
            Finally
                _owner.EndUpdate()
            End Try
        End Sub

        Public Overloads Sub AddRange(ParamArray labels() As String)
            AddRange(CType(labels, IEnumerable(Of String)))
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, labelText As String)
            MyBase.InsertItem(index, If(labelText, ""))
            _owner.NotifyDataChanged()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            MyBase.RemoveItem(index)
            _owner.NotifyDataChanged()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, labelText As String)
            MyBase.SetItem(index, If(labelText, ""))
            _owner.NotifyDataChanged()
        End Sub

        Protected Overrides Sub ClearItems()
            MyBase.ClearItems()
            _owner.NotifyDataChanged()
        End Sub
    End Class

    Public Class ChartColorCollection
        Inherits Collection(Of Color)

        Private ReadOnly _owner As Ultra2DChart

        Friend Sub New(owner As Ultra2DChart)
            _owner = owner
        End Sub

        Public Overloads Sub AddRange(collection As IEnumerable(Of Color))
            If collection Is Nothing Then Return
            _owner.BeginUpdate()
            Try
                For Each colorValue In collection
                    Add(colorValue)
                Next
            Finally
                _owner.EndUpdate()
            End Try
        End Sub

        Public Overloads Sub AddRange(ParamArray colors() As Color)
            AddRange(CType(colors, IEnumerable(Of Color)))
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, colorValue As Color)
            MyBase.InsertItem(index, colorValue)
            _owner.NotifyStyleChanged()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            MyBase.RemoveItem(index)
            _owner.NotifyStyleChanged()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, colorValue As Color)
            MyBase.SetItem(index, colorValue)
            _owner.NotifyStyleChanged()
        End Sub

        Protected Overrides Sub ClearItems()
            MyBase.ClearItems()
            _owner.NotifyStyleChanged()
        End Sub
    End Class

    Private NotInheritable Class ChartLayoutInfo
        Public Width As Integer
        Public Height As Integer
        Public Dpi As Integer
        Public DataVersion As Integer
        Public StyleVersion As Integer
        Public PlotRect As RectangleF
        Public ChartRect As RectangleF
        Public TitleRect As RectangleF
        Public XAxisTitleRect As RectangleF
        Public YAxisTitleRect As RectangleF
        Public TotalCategoryCount As Integer
        Public CategoryCount As Integer
        Public ViewStartIndex As Integer
        Public ViewEndIndex As Integer
        Public ValueMin As Double
        Public ValueMax As Double
        Public EffectiveYAxisLabelFormat As String
        Public TickValues As List(Of Double)
        Public TickLabels As List(Of TickLabelInfo)
        Public CategoryLabels As List(Of CategoryLabelInfo)
        Public SeriesDraws As List(Of SeriesDrawInfo)
        Public LegendItems As List(Of LegendItemInfo)
        Public ValueLabels As List(Of ValueLabelInfo)
        Public EffectiveXAxisLabelInterval As Integer = 1
        Public EffectiveValueLabelInterval As Integer = 1
        Public EffectiveDataPointInterval As Integer = 1
        Public ZeroY As Single
        Public Scale As Single
    End Class

    Private Structure TickLabelInfo
        Public Value As Double
        Public Y As Single
        Public Text As String
        Public TextRect As RectangleF
    End Structure

    Private Structure CategoryLabelInfo
        Public Index As Integer
        Public X As Single
        Public Text As String
        Public TextRect As RectangleF
        Public Rotated As Boolean
    End Structure

    Private NotInheritable Class SeriesDrawInfo
        Public Series As ChartSeries
        Public SeriesIndex As Integer
        Public Color As Color
        Public Columns As New List(Of ColumnDrawInfo)
        Public LineSegments As New List(Of List(Of PointF))
        Public Markers As New List(Of MarkerDrawInfo)
    End Class

    Private Structure ColumnDrawInfo
        Public Rect As RectangleF
        Public FillColor As Color
        Public GradientColor As Color
        Public BorderColor As Color
        Public BorderThickness As Single
        Public CornerRadius As Single
    End Structure

    Private Structure MarkerDrawInfo
        Public Center As PointF
        Public Shape As MarkerShapeEnum
        Public Size As Single
        Public FillColor As Color
        Public BorderColor As Color
        Public BorderThickness As Single
    End Structure

    Private Structure LegendItemInfo
        Public Series As ChartSeries
        Public SeriesIndex As Integer
        Public Color As Color
        Public MarkerRect As RectangleF
        Public TextRect As RectangleF
        Public Text As String
    End Structure

    Private Structure ValueLabelInfo
        Public Text As String
        Public Rect As RectangleF
        Public Color As Color
    End Structure

    Private Structure HoverTargetInfo
        Public HasTarget As Boolean
        Public SeriesIndex As Integer
        Public CategoryIndex As Integer
        Public Value As Double
        Public Point As PointF
        Public Color As Color
        Public TooltipText As String
        Public TooltipRect As RectangleF
        Public TextRect As RectangleF
    End Structure
#End Region

#Region "构造"
    Private ReadOnly _categories As ChartCategoryCollection
    Private ReadOnly _series As ChartSeriesCollection
    Private ReadOnly _palette As ChartColorCollection
    Private _layoutCache As ChartLayoutInfo
    Private _dataVersion As Integer
    Private _styleVersion As Integer
    Private _updateCount As Integer
    Private _pendingInvalidate As Boolean
    Private _pendingChartChanged As Boolean
    Private _isPanningXAxis As Boolean
    Private _panStartMouseX As Integer
    Private _panStartViewStart As Integer
    Private _hasHoverPoint As Boolean
    Private _hoverClientPoint As Point

    Public Sub New()
        InitializeComponent()
        SetStyle(ControlStyles.UserPaint Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.Selectable Or
                 ControlStyles.ResizeRedraw, True)
        TabStop = True
        _categories = New ChartCategoryCollection(Me)
        _series = New ChartSeriesCollection(Me)
        _palette = New ChartColorCollection(Me)
        ResetPalette()
    End Sub
#End Region

#Region "公共数据 API"
    <Category("LakeUI"), Description("分类横轴标签集合。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
     Editor("System.Windows.Forms.Design.StringCollectionEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")>
    Public ReadOnly Property Categories As ChartCategoryCollection
        Get
            Return _categories
        End Get
    End Property

    <Category("LakeUI"), Description("图表系列集合。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
     Editor("System.ComponentModel.Design.CollectionEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")>
    Public ReadOnly Property Series As ChartSeriesCollection
        Get
            Return _series
        End Get
    End Property

    <Category("LakeUI"), Description("默认系列调色板。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
     Editor("System.ComponentModel.Design.CollectionEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")>
    Public ReadOnly Property Palette As ChartColorCollection
        Get
            Return _palette
        End Get
    End Property

    Public Sub SetCategories(ParamArray labels() As String)
        BeginUpdate()
        Try
            _categories.Clear()
            If labels IsNot Nothing Then _categories.AddRange(labels)
        Finally
            EndUpdate()
        End Try
    End Sub

    Public Function AddSeries(name As String, chartType As ChartSeriesTypeEnum, ParamArray values() As Double) As ChartSeries
        Dim seriesItem As New ChartSeries(name, chartType)
        If values IsNot Nothing Then seriesItem.Points.AddRange(values)
        _series.Add(seriesItem)
        Return seriesItem
    End Function

    Public Sub ClearData()
        BeginUpdate()
        Try
            _categories.Clear()
            _series.Clear()
        Finally
            EndUpdate()
        End Try
    End Sub

    Public Sub BeginUpdate()
        _updateCount += 1
    End Sub

    Public Sub EndUpdate()
        _updateCount -= 1
        If _updateCount > 0 Then Return
        _updateCount = 0

        Dim raiseChanged = _pendingChartChanged
        Dim invalidateNow = _pendingInvalidate
        _pendingChartChanged = False
        _pendingInvalidate = False
        If raiseChanged Then RaiseEvent ChartChanged(Me, EventArgs.Empty)
        If invalidateNow AndAlso AutoRefresh Then 请求V3渲染()
    End Sub

    Public Sub RefreshChart(Optional rebuildLayout As Boolean = True)
        If rebuildLayout Then _layoutCache = Nothing
        请求V3渲染()
    End Sub

    Public Sub ResetPalette()
        BeginUpdate()
        Try
            _palette.Clear()
            _palette.AddRange(
                Color.FromArgb(68, 114, 196),
                Color.FromArgb(237, 125, 49),
                Color.FromArgb(165, 165, 165),
                Color.FromArgb(255, 192, 0),
                Color.FromArgb(91, 155, 213),
                Color.FromArgb(112, 173, 71),
                Color.FromArgb(38, 68, 120),
                Color.FromArgb(158, 72, 14))
        Finally
            EndUpdate()
        End Try
    End Sub

    Public Sub SetXAxisView(startIndex As Integer, categoryCount As Integer)
        设置X轴视图(startIndex, categoryCount)
    End Sub

    Public Sub ResetXAxisView()
        设置X轴视图(0, 0)
    End Sub
#End Region

#Region "绘制"
    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me, 1) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements V3_IGpuRenderable.RenderGpu
        If Me.Width <= 0 OrElse Me.Height <= 0 Then Return

        If _backgroundSource IsNot Nothing Then
            context.DrawBackgroundSource(Me, _backgroundSource, New RectangleF(0, 0, Me.Width, Me.Height))
        ElseIf MyBase.BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), MyBase.BackColor)
        End If

        Dim layout = 获取布局()
        绘制图形层_GPU(context, layout)
        绘制文字层_GPU(context, layout)

        If Not Enabled AndAlso 禁用时遮罩颜色.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Me.Width, Me.Height), 禁用时遮罩颜色)
        End If
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements V3_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Me.Size)
    End Function

    Private Sub 绘制图形层_GPU(context As D3D_PaintContext, layout As ChartLayoutInfo)
        If layout Is Nothing Then Return
        Dim s As Single = layout.Scale

        If 绘图区背景颜色.A > 0 Then context.FillRectangle(layout.PlotRect, 绘图区背景颜色)

        绘制网格与坐标轴_GPU(context, layout, s)

        For Each draw In layout.SeriesDraws
            If draw.Series.ChartType = ChartSeriesTypeEnum.Column Then
                For Each col In draw.Columns
                    If col.Rect.Width <= 0 OrElse col.Rect.Height <= 0 Then Continue For
                    填充圆角矩形_GPU(context, col.Rect, col.CornerRadius, col.FillColor)
                    If col.BorderThickness > 0 Then 绘制圆角边框_GPU(context, col.Rect, col.CornerRadius, col.BorderColor, col.BorderThickness)
                Next
            End If
        Next

        For Each draw In layout.SeriesDraws
            If draw.Series.ChartType <> ChartSeriesTypeEnum.Line Then Continue For
            Dim lineBrush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, draw.Color, context.DeviceGeneration)
            If lineBrush IsNot Nothing Then
                For Each segment In draw.LineSegments
                    If segment.Count < 2 Then Continue For
                    Using geo = 创建折线几何(segment)
                        context.DeviceContext.DrawGeometry(geo, lineBrush, draw.Series.LineThickness * s)
                    End Using
                Next
            End If

            For Each marker In draw.Markers
                绘制标记_GPU(context, marker)
            Next
        Next

        If 绘图区边框粗细 > 0 AndAlso 绘图区边框颜色.A > 0 Then
            context.DrawRectangle(layout.PlotRect, 绘图区边框颜色, 绘图区边框粗细 * s)
        End If

        绘制折线悬停十字线_GPU(context, layout, s)
    End Sub

    Private Sub 绘制网格与坐标轴_GPU(context As D3D_PaintContext, layout As ChartLayoutInfo, s As Single)
        Dim plot = layout.PlotRect
        If plot.Width <= 0 OrElse plot.Height <= 0 Then Return

        If 显示水平网格线 AndAlso 网格线颜色.A > 0 AndAlso 网格线粗细 > 0 Then
            Dim gridBrush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, 网格线颜色, context.DeviceGeneration)
            If gridBrush IsNot Nothing Then
                For Each tick In layout.TickLabels
                    context.DeviceContext.DrawLine(New Vector2(plot.Left, tick.Y), New Vector2(plot.Right, tick.Y), gridBrush, 网格线粗细 * s)
                Next
            End If
        End If

        If 显示垂直网格线 AndAlso 网格线颜色.A > 0 AndAlso 网格线粗细 > 0 AndAlso layout.CategoryCount > 0 Then
            Dim gridBrush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, 网格线颜色, context.DeviceGeneration)
            If gridBrush IsNot Nothing Then
                For Each cat In layout.CategoryLabels
                    context.DeviceContext.DrawLine(New Vector2(cat.X, plot.Top), New Vector2(cat.X, plot.Bottom), gridBrush, 网格线粗细 * s)
                Next
            End If
        End If

        If 坐标轴线颜色.A > 0 AndAlso 坐标轴线粗细 > 0 Then
            Dim axisBrush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, 坐标轴线颜色, context.DeviceGeneration)
            If axisBrush IsNot Nothing Then
                context.DeviceContext.DrawLine(New Vector2(plot.Left, plot.Bottom), New Vector2(plot.Right, plot.Bottom), axisBrush, 坐标轴线粗细 * s)
                context.DeviceContext.DrawLine(New Vector2(plot.Left, plot.Top), New Vector2(plot.Left, plot.Bottom), axisBrush, 坐标轴线粗细 * s)
                If layout.ValueMin < 0 AndAlso layout.ValueMax > 0 Then
                    context.DeviceContext.DrawLine(New Vector2(plot.Left, layout.ZeroY), New Vector2(plot.Right, layout.ZeroY), axisBrush, 坐标轴线粗细 * s)
                End If
            End If
        End If

        If 坐标轴刻度线颜色.A > 0 AndAlso 坐标轴刻度线粗细 > 0 Then
            Dim tickBrush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, 坐标轴刻度线颜色, context.DeviceGeneration)
            If tickBrush IsNot Nothing Then
                Dim tickLen As Single = 主要刻度长度 * s
                If 显示Y轴刻度线 Then
                    For Each tick In layout.TickLabels
                        context.DeviceContext.DrawLine(New Vector2(plot.Left - tickLen, tick.Y), New Vector2(plot.Left, tick.Y), tickBrush, 坐标轴刻度线粗细 * s)
                    Next
                End If
                If 显示X轴刻度线 Then
                    For Each cat In layout.CategoryLabels
                        context.DeviceContext.DrawLine(New Vector2(cat.X, plot.Bottom), New Vector2(cat.X, plot.Bottom + tickLen), tickBrush, 坐标轴刻度线粗细 * s)
                    Next
                End If
            End If
        End If
    End Sub

    Private Sub 绘制标记_GPU(context As D3D_PaintContext, marker As MarkerDrawInfo)
        If marker.Shape = MarkerShapeEnum.None OrElse marker.Size <= 0 Then Return
        Dim half As Single = marker.Size / 2.0F
        Dim rect As New RectangleF(marker.Center.X - half, marker.Center.Y - half, marker.Size, marker.Size)
        Dim fillBrush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, marker.FillColor, context.DeviceGeneration)
        Dim borderBrush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, marker.BorderColor, context.DeviceGeneration)

        Select Case marker.Shape
            Case MarkerShapeEnum.Circle
                Dim e As New D2D.Ellipse(New Vector2(marker.Center.X, marker.Center.Y), half, half)
                If fillBrush IsNot Nothing AndAlso marker.FillColor.A > 0 Then context.DeviceContext.FillEllipse(e, fillBrush)
                If borderBrush IsNot Nothing AndAlso marker.BorderColor.A > 0 AndAlso marker.BorderThickness > 0 Then context.DeviceContext.DrawEllipse(e, borderBrush, marker.BorderThickness)
            Case MarkerShapeEnum.Square
                If fillBrush IsNot Nothing AndAlso marker.FillColor.A > 0 Then context.DeviceContext.FillRectangle(D3D_PaintContext.ToRawRect(rect), fillBrush)
                If borderBrush IsNot Nothing AndAlso marker.BorderColor.A > 0 AndAlso marker.BorderThickness > 0 Then context.DeviceContext.DrawRectangle(D3D_PaintContext.ToRawRect(rect), borderBrush, marker.BorderThickness)
            Case MarkerShapeEnum.Diamond
                Using geo = 创建菱形几何(marker.Center, half)
                    If fillBrush IsNot Nothing AndAlso marker.FillColor.A > 0 Then context.DeviceContext.FillGeometry(geo, fillBrush)
                    If borderBrush IsNot Nothing AndAlso marker.BorderColor.A > 0 AndAlso marker.BorderThickness > 0 Then context.DeviceContext.DrawGeometry(geo, borderBrush, marker.BorderThickness)
                End Using
        End Select
    End Sub

    Private Sub 绘制文字层_GPU(context As D3D_PaintContext, layout As ChartLayoutInfo)
        If layout Is Nothing Then Return

        If 显示标题 AndAlso Not String.IsNullOrEmpty(标题文本) Then
            绘制文本_GPU(context, 标题文本, 获取标题字体(), 标题颜色, layout.TitleRect, DW.TextAlignment.Center, DW.ParagraphAlignment.Center)
        End If

        If 显示Y轴标签 Then
            For Each tick In layout.TickLabels
                绘制文本_GPU(context, tick.Text, 获取坐标轴标签字体(), 坐标轴标签颜色, tick.TextRect, DW.TextAlignment.Trailing, DW.ParagraphAlignment.Center)
            Next
        End If

        If 显示X轴标签 Then
            For Each cat In layout.CategoryLabels
                If cat.Rotated Then
                    绘制旋转文本_GPU(context, cat.Text, 获取坐标轴标签字体(), 坐标轴标签颜色, cat.TextRect, X轴标签旋转角度)
                Else
                    绘制文本_GPU(context, cat.Text, 获取坐标轴标签字体(), 坐标轴标签颜色, cat.TextRect, DW.TextAlignment.Center, DW.ParagraphAlignment.Near)
                End If
            Next
        End If

        If Not String.IsNullOrEmpty(X轴标题文本) Then
            绘制文本_GPU(context, X轴标题文本, 获取坐标轴标题字体(), 坐标轴标题颜色, layout.XAxisTitleRect, DW.TextAlignment.Center, DW.ParagraphAlignment.Center)
        End If

        If Not String.IsNullOrEmpty(Y轴标题文本) Then
            绘制垂直标题_GPU(context, Y轴标题文本, 获取坐标轴标题字体(), 坐标轴标题颜色, layout.YAxisTitleRect)
        End If

        If 图例位置 <> LegendPositionEnum.None Then
            绘制图例_GPU(context, layout)
        End If

        For Each label In layout.ValueLabels
            绘制文本_GPU(context, label.Text, 获取值标签字体(), label.Color, label.Rect, DW.TextAlignment.Center, DW.ParagraphAlignment.Center)
        Next

        Dim hoverTarget = 获取折线悬停目标(layout)
        If hoverTarget.HasTarget Then
            绘制文本_GPU(context, hoverTarget.TooltipText, 获取悬停提示字体(), 折线悬停提示文字颜色, hoverTarget.TextRect, DW.TextAlignment.Leading, DW.ParagraphAlignment.Center)
        End If
    End Sub

    Private Sub 绘制折线悬停十字线_GPU(context As D3D_PaintContext, layout As ChartLayoutInfo, s As Single)
        Dim hoverTarget = 获取折线悬停目标(layout)
        If Not hoverTarget.HasTarget Then Return

        Dim plot = layout.PlotRect
        Dim lineBrush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, 折线悬停十字线颜色, context.DeviceGeneration)
        If lineBrush IsNot Nothing AndAlso 折线悬停十字线粗细 > 0 Then
            context.DeviceContext.DrawLine(New Vector2(hoverTarget.Point.X, plot.Top), New Vector2(hoverTarget.Point.X, plot.Bottom), lineBrush, 折线悬停十字线粗细 * s)
            context.DeviceContext.DrawLine(New Vector2(plot.Left, hoverTarget.Point.Y), New Vector2(plot.Right, hoverTarget.Point.Y), lineBrush, 折线悬停十字线粗细 * s)
        End If

        Dim highlightBrush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, hoverTarget.Color, context.DeviceGeneration)
        Dim borderBrush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, 折线悬停吸附点边框颜色, context.DeviceGeneration)
        Dim radius As Single = Math.Max(2.0F, 折线悬停吸附点半径 * s)
        Dim ellipse As New D2D.Ellipse(New Vector2(hoverTarget.Point.X, hoverTarget.Point.Y), radius, radius)
        If highlightBrush IsNot Nothing Then context.DeviceContext.FillEllipse(ellipse, highlightBrush)
        If borderBrush IsNot Nothing AndAlso 折线悬停吸附点边框粗细 > 0 Then context.DeviceContext.DrawEllipse(ellipse, borderBrush, 折线悬停吸附点边框粗细 * s)

        If 折线悬停提示背景颜色.A > 0 Then context.FillRectangle(hoverTarget.TooltipRect, 折线悬停提示背景颜色)
        If 折线悬停提示边框颜色.A > 0 AndAlso 折线悬停提示边框粗细 > 0 Then
            context.DrawRectangle(hoverTarget.TooltipRect, 折线悬停提示边框颜色, 折线悬停提示边框粗细 * s)
        End If
    End Sub

    Private Sub 绘制图例_GPU(context As D3D_PaintContext, layout As ChartLayoutInfo)
        For Each item In layout.LegendItems
            Dim markerBrush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, item.Color, context.DeviceGeneration)
            If markerBrush IsNot Nothing Then
                If item.Series.ChartType = ChartSeriesTypeEnum.Column Then
                    context.DeviceContext.FillRectangle(D3D_PaintContext.ToRawRect(item.MarkerRect), markerBrush)
                Else
                    Dim cy As Single = item.MarkerRect.Y + item.MarkerRect.Height / 2.0F
                    context.DeviceContext.DrawLine(New Vector2(item.MarkerRect.Left, cy), New Vector2(item.MarkerRect.Right, cy), markerBrush, Math.Max(1.0F, item.Series.LineThickness * layout.Scale))
                    Dim r As Single = Math.Min(item.MarkerRect.Width, item.MarkerRect.Height) / 3.0F
                    context.DeviceContext.FillEllipse(New D2D.Ellipse(New Vector2(item.MarkerRect.X + item.MarkerRect.Width / 2.0F, cy), r, r), markerBrush)
                End If
            End If
            绘制文本_GPU(context, item.Text, 获取图例字体(), 图例文字颜色, item.TextRect, DW.TextAlignment.Leading, DW.ParagraphAlignment.Center)
        Next
    End Sub

    Private Sub 绘制文本_GPU(context As D3D_PaintContext, text As String, font As Font, color As Color, rect As RectangleF, align As DW.TextAlignment, paraAlign As DW.ParagraphAlignment)
        If String.IsNullOrEmpty(text) OrElse color.A <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        context.DrawText(text, font, color, rect, align, paraAlign)
    End Sub

    Private Sub 绘制旋转文本_GPU(context As D3D_PaintContext, text As String, font As Font, color As Color, rect As RectangleF, angle As Single)
        If String.IsNullOrEmpty(text) OrElse color.A <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim oldTransform = context.DeviceContext.Transform
        Dim rad As Single = angle * CSng(Math.PI / 180.0)
        Dim anchor As New Vector2(rect.X + rect.Width / 2.0F, rect.Y)
        Try
            context.DeviceContext.Transform = Matrix3x2.CreateRotation(rad, anchor) * oldTransform
            context.DrawText(text, font, color, rect, DW.TextAlignment.Center, DW.ParagraphAlignment.Center)
        Finally
            context.DeviceContext.Transform = oldTransform
        End Try
    End Sub

    Private Sub 绘制垂直标题_GPU(context As D3D_PaintContext, text As String, font As Font, color As Color, rect As RectangleF)
        If String.IsNullOrEmpty(text) OrElse color.A <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim oldTransform = context.DeviceContext.Transform
        Try
            context.DeviceContext.Transform = Matrix3x2.CreateRotation(-CSng(Math.PI / 2.0F)) * Matrix3x2.CreateTranslation(rect.X, rect.Bottom) * oldTransform
            context.DrawText(text, font, color, New RectangleF(0, 0, rect.Height, rect.Width), DW.TextAlignment.Center, DW.ParagraphAlignment.Center)
        Finally
            context.DeviceContext.Transform = oldTransform
        End Try
    End Sub

    Private Sub 填充圆角矩形_GPU(context As D3D_PaintContext, rect As RectangleF, radius As Single, color As Color)
        If color.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If radius <= 0 Then
            context.DeviceContext.FillRectangle(D3D_PaintContext.ToRawRect(rect), brush)
            Return
        End If
        Using geo = D3D_RenderCore.DeviceManager.D2DFactory.CreateRoundedRectangleGeometry(New D2D.RoundedRectangle(rect, radius, radius))
            context.DeviceContext.FillGeometry(geo, brush)
        End Using
    End Sub

    Private Sub 绘制圆角边框_GPU(context As D3D_PaintContext, rect As RectangleF, radius As Single, color As Color, strokeWidth As Single)
        If color.A = 0 OrElse strokeWidth <= 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext, color, context.DeviceGeneration)
        If radius <= 0 Then
            context.DeviceContext.DrawRectangle(D3D_PaintContext.ToRawRect(rect), brush, strokeWidth)
            Return
        End If
        Using geo = D3D_RenderCore.DeviceManager.D2DFactory.CreateRoundedRectangleGeometry(New D2D.RoundedRectangle(rect, radius, radius))
            context.DeviceContext.DrawGeometry(geo, brush, strokeWidth)
        End Using
    End Sub



    Private Sub 绘制网格与坐标轴(rt As D2D.ID2D1RenderTarget, brushCache As D3D_D2DInterop.SolidColorBrushCache, layout As ChartLayoutInfo, s As Single)
        Dim plot = layout.PlotRect
        If plot.Width <= 0 OrElse plot.Height <= 0 Then Return

        If 显示水平网格线 AndAlso 网格线颜色.A > 0 AndAlso 网格线粗细 > 0 Then
            Dim gridBrush = brushCache.Get(rt, 网格线颜色)
            If gridBrush IsNot Nothing Then
                For Each tick In layout.TickLabels
                    rt.DrawLine(New Vector2(plot.Left, tick.Y), New Vector2(plot.Right, tick.Y), gridBrush, 网格线粗细 * s)
                Next
            End If
        End If

        If 显示垂直网格线 AndAlso 网格线颜色.A > 0 AndAlso 网格线粗细 > 0 AndAlso layout.CategoryCount > 0 Then
            Dim gridBrush = brushCache.Get(rt, 网格线颜色)
            If gridBrush IsNot Nothing Then
                For Each cat In layout.CategoryLabels
                    rt.DrawLine(New Vector2(cat.X, plot.Top), New Vector2(cat.X, plot.Bottom), gridBrush, 网格线粗细 * s)
                Next
            End If
        End If

        If 坐标轴线颜色.A > 0 AndAlso 坐标轴线粗细 > 0 Then
            Dim axisBrush = brushCache.Get(rt, 坐标轴线颜色)
            If axisBrush IsNot Nothing Then
                rt.DrawLine(New Vector2(plot.Left, plot.Bottom), New Vector2(plot.Right, plot.Bottom), axisBrush, 坐标轴线粗细 * s)
                rt.DrawLine(New Vector2(plot.Left, plot.Top), New Vector2(plot.Left, plot.Bottom), axisBrush, 坐标轴线粗细 * s)
                If layout.ValueMin < 0 AndAlso layout.ValueMax > 0 Then
                    rt.DrawLine(New Vector2(plot.Left, layout.ZeroY), New Vector2(plot.Right, layout.ZeroY), axisBrush, 坐标轴线粗细 * s)
                End If
            End If
        End If

        If 坐标轴刻度线颜色.A > 0 AndAlso 坐标轴刻度线粗细 > 0 Then
            Dim tickBrush = brushCache.Get(rt, 坐标轴刻度线颜色)
            If tickBrush IsNot Nothing Then
                Dim tickLen As Single = 主要刻度长度 * s
                If 显示Y轴刻度线 Then
                    For Each tick In layout.TickLabels
                        rt.DrawLine(New Vector2(plot.Left - tickLen, tick.Y), New Vector2(plot.Left, tick.Y), tickBrush, 坐标轴刻度线粗细 * s)
                    Next
                End If
                If 显示X轴刻度线 Then
                    For Each cat In layout.CategoryLabels
                        rt.DrawLine(New Vector2(cat.X, plot.Bottom), New Vector2(cat.X, plot.Bottom + tickLen), tickBrush, 坐标轴刻度线粗细 * s)
                    Next
                End If
            End If
        End If
    End Sub

    Private Sub 绘制标记(rt As D2D.ID2D1RenderTarget, brushCache As D3D_D2DInterop.SolidColorBrushCache, marker As MarkerDrawInfo)
        If marker.Shape = MarkerShapeEnum.None OrElse marker.Size <= 0 Then Return
        Dim half As Single = marker.Size / 2.0F
        Dim rect As New RectangleF(marker.Center.X - half, marker.Center.Y - half, marker.Size, marker.Size)
        Dim fillBrush = brushCache.Get(rt, marker.FillColor)
        Dim borderBrush = brushCache.Get(rt, marker.BorderColor)

        Select Case marker.Shape
            Case MarkerShapeEnum.Circle
                Dim e As New D2D.Ellipse(New Vector2(marker.Center.X, marker.Center.Y), half, half)
                If fillBrush IsNot Nothing AndAlso marker.FillColor.A > 0 Then rt.FillEllipse(e, fillBrush)
                If borderBrush IsNot Nothing AndAlso marker.BorderColor.A > 0 AndAlso marker.BorderThickness > 0 Then rt.DrawEllipse(e, borderBrush, marker.BorderThickness)
            Case MarkerShapeEnum.Square
                If fillBrush IsNot Nothing AndAlso marker.FillColor.A > 0 Then rt.FillRectangle(D3D_D2DInterop.ToD2DRect(rect), fillBrush)
                If borderBrush IsNot Nothing AndAlso marker.BorderColor.A > 0 AndAlso marker.BorderThickness > 0 Then rt.DrawRectangle(D3D_D2DInterop.ToD2DRect(rect), borderBrush, marker.BorderThickness)
            Case MarkerShapeEnum.Diamond
                Using geo = 创建菱形几何(marker.Center, half)
                    If fillBrush IsNot Nothing AndAlso marker.FillColor.A > 0 Then rt.FillGeometry(geo, fillBrush)
                    If borderBrush IsNot Nothing AndAlso marker.BorderColor.A > 0 AndAlso marker.BorderThickness > 0 Then rt.DrawGeometry(geo, borderBrush, marker.BorderThickness)
                End Using
        End Select
    End Sub


    Private Sub 绘制折线悬停十字线(rt As D2D.ID2D1RenderTarget, brushCache As D3D_D2DInterop.SolidColorBrushCache, layout As ChartLayoutInfo, s As Single)
        Dim hoverTarget = 获取折线悬停目标(layout)
        If Not hoverTarget.HasTarget Then Return

        Dim plot = layout.PlotRect
        Dim lineBrush = brushCache.Get(rt, 折线悬停十字线颜色)
        If lineBrush IsNot Nothing AndAlso 折线悬停十字线粗细 > 0 Then
            Dim stroke = D3D_D2DInterop.GetRoundStrokeStyle(roundDashCap:=True)
            rt.DrawLine(New Vector2(hoverTarget.Point.X, plot.Top), New Vector2(hoverTarget.Point.X, plot.Bottom), lineBrush, 折线悬停十字线粗细 * s, stroke)
            rt.DrawLine(New Vector2(plot.Left, hoverTarget.Point.Y), New Vector2(plot.Right, hoverTarget.Point.Y), lineBrush, 折线悬停十字线粗细 * s, stroke)
        End If

        Dim highlightBrush = brushCache.Get(rt, hoverTarget.Color)
        Dim borderBrush = brushCache.Get(rt, 折线悬停吸附点边框颜色)
        Dim radius As Single = Math.Max(2.0F, 折线悬停吸附点半径 * s)
        Dim ellipse As New D2D.Ellipse(New Vector2(hoverTarget.Point.X, hoverTarget.Point.Y), radius, radius)
        If highlightBrush IsNot Nothing Then rt.FillEllipse(ellipse, highlightBrush)
        If borderBrush IsNot Nothing AndAlso 折线悬停吸附点边框粗细 > 0 Then rt.DrawEllipse(ellipse, borderBrush, 折线悬停吸附点边框粗细 * s)

        If 折线悬停提示背景颜色.A > 0 Then
            D3D_RectangleRenderer.绘制矩形背景_D2D(rt, hoverTarget.TooltipRect, 折线悬停提示背景颜色, Color.Empty, Orientation.Vertical, brushCache)
        End If
        If 折线悬停提示边框颜色.A > 0 AndAlso 折线悬停提示边框粗细 > 0 Then
            D3D_RectangleRenderer.绘制矩形边框_D2D(rt, hoverTarget.TooltipRect, 折线悬停提示边框颜色, 折线悬停提示边框粗细 * s, brushCache)
        End If
    End Sub




#End Region

#Region "布局"
    Private Function 获取布局() As ChartLayoutInfo
        Dim currentDpi As Integer = V3_DpiContext.FromControl(Me).Dpi
        Dim cached = _layoutCache
        If cached IsNot Nothing AndAlso
           cached.Width = Me.Width AndAlso
           cached.Height = Me.Height AndAlso
           cached.Dpi = currentDpi AndAlso
           cached.DataVersion = _dataVersion AndAlso
           cached.StyleVersion = _styleVersion Then
            Return cached
        End If

        Dim s As Single = DpiScale()
        Dim layout As New ChartLayoutInfo With {
            .Width = Me.Width,
            .Height = Me.Height,
            .Dpi = currentDpi,
            .DataVersion = _dataVersion,
            .StyleVersion = _styleVersion,
            .TickValues = New List(Of Double)(),
            .TickLabels = New List(Of TickLabelInfo)(),
            .CategoryLabels = New List(Of CategoryLabelInfo)(),
            .SeriesDraws = New List(Of SeriesDrawInfo)(),
            .LegendItems = New List(Of LegendItemInfo)(),
            .ValueLabels = New List(Of ValueLabelInfo)(),
            .Scale = s
        }

        Dim outer As New RectangleF(Padding.Left, Padding.Top, Math.Max(0, Me.Width - Padding.Horizontal), Math.Max(0, Me.Height - Padding.Vertical))
        layout.ChartRect = outer
        If outer.Width <= 4 OrElse outer.Height <= 4 Then
            _layoutCache = layout
            Return layout
        End If

        Dim content = outer
        Dim titleFont = 获取标题字体()
        If 显示标题 AndAlso Not String.IsNullOrEmpty(标题文本) Then
            Dim titleH = 测量文本尺寸(标题文本, titleFont).Height + 标题下间距 * s
            layout.TitleRect = New RectangleF(content.X, content.Y, content.Width, Math.Min(content.Height, titleH - 标题下间距 * s))
            content.Y += titleH
            content.Height -= titleH
        End If

        Dim visibleSeries = 获取可见系列()
        Dim legendReserve As RectangleF = RectangleF.Empty
        If 图例位置 <> LegendPositionEnum.None AndAlso visibleSeries.Count > 0 Then
            legendReserve = 预留图例区域(content, visibleSeries, s)
            content = 扣除图例区域(content, legendReserve)
        End If

        layout.TotalCategoryCount = 获取分类数量()
        Dim viewRange = 获取规范化X轴视图(layout.TotalCategoryCount, X轴视图起始索引, X轴视图分类数量)
        layout.ViewStartIndex = viewRange.StartIndex
        layout.ViewEndIndex = viewRange.EndIndex
        layout.CategoryCount = viewRange.Count

        Dim axisLabelFont = 获取坐标轴标签字体()
        Dim axisTitleFont = 获取坐标轴标题字体()
        Dim valueRange = 计算值范围(visibleSeries, layout)
        layout.ValueMin = valueRange.Minimum
        layout.ValueMax = valueRange.Maximum
        layout.TickValues = 生成刻度值(layout.ValueMin, layout.ValueMax)
        layout.EffectiveYAxisLabelFormat = 获取有效Y轴标签格式(layout.ValueMin, layout.ValueMax, layout.TickValues)

        Dim yLabelW As Single = 0
        If 显示Y轴标签 Then
            For Each tickValue In layout.TickValues
                yLabelW = Math.Max(yLabelW, 测量文本尺寸(格式化数值(tickValue, layout.EffectiveYAxisLabelFormat), axisLabelFont).Width)
            Next
        End If
        Dim yTitleW As Single = 0
        If Not String.IsNullOrEmpty(Y轴标题文本) Then yTitleW = 测量文本尺寸(Y轴标题文本, axisTitleFont).Height + 坐标轴标题间距 * s

        Dim xLabelH As Single = 0
        If 显示X轴标签 AndAlso layout.CategoryCount > 0 Then
            Dim sampleH As Single = 测量文本尺寸("Ag", axisLabelFont).Height
            xLabelH = sampleH
            If Math.Abs(X轴标签旋转角度) > 0.1F Then
                Dim longest As Single = 0
                For i As Integer = layout.ViewStartIndex To layout.ViewEndIndex - 1
                    longest = Math.Max(longest, 测量文本尺寸(获取分类标签(i), axisLabelFont).Width)
                Next
                xLabelH = Math.Min(Math.Max(sampleH, longest * 0.72F), X轴最大标签高度 * s)
            End If
        End If
        Dim xTitleH As Single = 0
        If Not String.IsNullOrEmpty(X轴标题文本) Then xTitleH = 测量文本尺寸(X轴标题文本, axisTitleFont).Height + 坐标轴标题间距 * s

        Dim tickLen As Single = 主要刻度长度 * s
        Dim plotLeft As Single = content.Left + yTitleW + If(显示Y轴标签, yLabelW + 坐标轴标签间距 * s, 0) + If(显示Y轴刻度线, tickLen, 0)
        Dim plotBottomReserve As Single = If(显示X轴刻度线, tickLen, 0) + If(显示X轴标签, xLabelH + 坐标轴标签间距 * s, 0) + xTitleH
        Dim plotRect As New RectangleF(
            plotLeft,
            content.Top + 绘图区内上边距 * s,
            Math.Max(1.0F, content.Right - plotLeft),
            Math.Max(1.0F, content.Height - plotBottomReserve - 绘图区内上边距 * s))
        If plotRect.Width < 1 Then plotRect.Width = 1
        If plotRect.Height < 1 Then plotRect.Height = 1
        layout.PlotRect = plotRect

        Dim categoryWidth As Single = If(layout.CategoryCount > 0, plotRect.Width / layout.CategoryCount, plotRect.Width)
        layout.EffectiveXAxisLabelInterval = 获取自动间隔(X轴标签间隔, categoryWidth, X轴标签最小间距 * s)
        layout.EffectiveValueLabelInterval = 获取自动间隔(1, categoryWidth, 值标签最小间距 * s)
        layout.EffectiveDataPointInterval = 获取自动间隔(1, categoryWidth, 数据点最小间距 * s)

        If Not String.IsNullOrEmpty(Y轴标题文本) Then
            layout.YAxisTitleRect = New RectangleF(content.Left, plotRect.Top, Math.Max(1, yTitleW - 坐标轴标题间距 * s), plotRect.Height)
        End If
        If Not String.IsNullOrEmpty(X轴标题文本) Then
            layout.XAxisTitleRect = New RectangleF(plotRect.Left, plotRect.Bottom + If(显示X轴刻度线, tickLen, 0) + If(显示X轴标签, xLabelH + 坐标轴标签间距 * s, 0), plotRect.Width, Math.Max(1, xTitleH - 坐标轴标题间距 * s))
        End If

        生成轴标签(layout, yLabelW, xLabelH, tickLen, axisLabelFont, s)
        生成系列绘制数据(layout, visibleSeries, s)
        If 图例位置 <> LegendPositionEnum.None AndAlso visibleSeries.Count > 0 Then 生成图例(layout, legendReserve, visibleSeries, s)

        _layoutCache = layout
        Return layout
    End Function

    Private Sub 生成轴标签(layout As ChartLayoutInfo, yLabelW As Single, xLabelH As Single, tickLen As Single, axisLabelFont As Font, s As Single)
        Dim plot = layout.PlotRect
        For Each tickValue In layout.TickValues
            Dim y = 值到Y坐标(tickValue, layout)
            layout.TickLabels.Add(New TickLabelInfo With {
                .Value = tickValue,
                .Y = y,
                .Text = 格式化数值(tickValue, layout.EffectiveYAxisLabelFormat),
                .TextRect = New RectangleF(plot.Left - tickLen - 坐标轴标签间距 * s - yLabelW, y - 10 * s, yLabelW, 20 * s)})
        Next
        layout.ZeroY = 获取柱基线Y坐标(layout)

        If layout.CategoryCount <= 0 Then Return
        Dim catW As Single = plot.Width / layout.CategoryCount
        Dim interval As Integer = Math.Max(1, layout.EffectiveXAxisLabelInterval)
        For i As Integer = layout.ViewStartIndex To layout.ViewEndIndex - 1
            Dim relativeIndex = i - layout.ViewStartIndex
            Dim x As Single = plot.Left + catW * (relativeIndex + 0.5F)
            If Not 应显示间隔项(relativeIndex, layout.CategoryCount, interval) Then Continue For
            Dim text = 获取分类标签(i)
            Dim textWidth As Single = 测量文本尺寸(text, axisLabelFont).Width + 4 * s
            Dim labelSlotWidth As Single = catW * Math.Max(1, interval) * 0.9F
            Dim rectW As Single = Math.Max(Math.Max(1, labelSlotWidth), textWidth)
            Dim rect As RectangleF
            Dim rotated As Boolean = Math.Abs(X轴标签旋转角度) > 0.1F
            If rotated Then
                rect = New RectangleF(x - rectW / 2.0F, plot.Bottom + tickLen + 坐标轴标签间距 * s, rectW, xLabelH)
            Else
                rect = New RectangleF(x - rectW / 2.0F, plot.Bottom + tickLen + 坐标轴标签间距 * s, rectW, xLabelH)
            End If
            layout.CategoryLabels.Add(New CategoryLabelInfo With {.Index = i, .X = x, .Text = text, .TextRect = rect, .Rotated = rotated})
        Next
    End Sub

    Private Sub 生成系列绘制数据(layout As ChartLayoutInfo, visibleSeries As List(Of ChartSeries), s As Single)
        If layout.CategoryCount <= 0 OrElse visibleSeries.Count = 0 Then Return
        Dim plot = layout.PlotRect
        Dim catW As Single = plot.Width / layout.CategoryCount
        Dim dataInterval As Integer = Math.Max(1, layout.EffectiveDataPointInterval)
        Dim columnSeries = visibleSeries.Where(Function(x) x.ChartType = ChartSeriesTypeEnum.Column).ToList()
        Dim columnCount As Integer = columnSeries.Count
        Dim seriesIndex As Integer = 0

        For Each ser In visibleSeries
            Dim color = 解析系列颜色(ser, seriesIndex)
            Dim draw As New SeriesDrawInfo With {.Series = ser, .SeriesIndex = seriesIndex, .Color = color}
            If ser.ChartType = ChartSeriesTypeEnum.Column Then
                Dim columnIndex As Integer = columnSeries.IndexOf(ser)
                For i As Integer = layout.ViewStartIndex To layout.ViewEndIndex - 1
                    Dim relativeIndex = i - layout.ViewStartIndex
                    Dim drawDataPoint = 应显示间隔项(relativeIndex, layout.CategoryCount, dataInterval)
                    Dim drawValueLabel = 系列显示值标签(ser) AndAlso 应显示间隔项(relativeIndex, layout.CategoryCount, layout.EffectiveValueLabelInterval)
                    If Not drawDataPoint AndAlso Not drawValueLabel Then Continue For
                    If i >= ser.Points.Count Then Continue For
                    Dim point = ser.Points(i)
                    If point Is Nothing OrElse Not 是有效数值(point.Value) Then Continue For

                    Dim groupW As Single = catW * Math.Max(0.05F, Math.Min(1.0F, 柱组宽度比例))
                    Dim gap As Single = Math.Max(0.0F, 柱系列间距 * s)
                    If columnCount > 1 Then gap = Math.Min(gap, Math.Max(0.0F, groupW / columnCount * 0.35F))
                    Dim colW As Single = If(columnCount <= 0, groupW, (groupW - gap * (columnCount - 1)) / columnCount)
                    colW = Math.Max(1.0F, colW)
                    Dim groupLeft As Single = plot.Left + catW * relativeIndex + (catW - groupW) / 2.0F
                    Dim x As Single = groupLeft + columnIndex * (colW + gap)
                    Dim yValue = 值到Y坐标(point.Value, layout)
                    Dim yZero = layout.ZeroY
                    Dim top As Single = Math.Min(yValue, yZero)
                    Dim h As Single = Math.Abs(yZero - yValue)
                    If h < 1.0F AndAlso point.Value <> 0 Then h = 1.0F
                    Dim fill = 解析点颜色(point, ser, seriesIndex)
                    Dim border = If(ser.BorderColor <> Color.Empty, ser.BorderColor, 调整亮度(fill, -0.2F))
                    draw.Columns.Add(New ColumnDrawInfo With {
                        .Rect = New RectangleF(x, top, colW, h),
                        .FillColor = fill,
                        .GradientColor = ser.GradientColor,
                        .BorderColor = border,
                        .BorderThickness = ser.BorderThickness * s,
                        .CornerRadius = ser.ColumnCornerRadius * s})
                    添加值标签(layout, ser, i, point.Value, New PointF(x + colW / 2.0F, If(point.Value >= 0, top, top + h)), fill, s)
                Next
            Else
                Dim currentSegment As New List(Of PointF)()
                For i As Integer = layout.ViewStartIndex To layout.ViewEndIndex - 1
                    If i >= ser.Points.Count OrElse ser.Points(i) Is Nothing OrElse Not 是有效数值(ser.Points(i).Value) Then
                        If currentSegment.Count > 0 Then
                            draw.LineSegments.Add(currentSegment)
                            currentSegment = New List(Of PointF)()
                        End If
                        Continue For
                    End If
                    Dim relativeIndex = i - layout.ViewStartIndex
                    Dim drawDataPoint = 应显示间隔项(relativeIndex, layout.CategoryCount, dataInterval)
                    Dim drawValueLabel = 系列显示值标签(ser) AndAlso 应显示间隔项(relativeIndex, layout.CategoryCount, layout.EffectiveValueLabelInterval)
                    If Not drawDataPoint AndAlso Not drawValueLabel Then Continue For
                    Dim point = ser.Points(i)
                    Dim x As Single = plot.Left + catW * (relativeIndex + 0.5F)
                    Dim y As Single = 值到Y坐标(point.Value, layout)
                    currentSegment.Add(New PointF(x, y))
                    If ser.MarkerShape <> MarkerShapeEnum.None AndAlso ser.MarkerSize > 0 Then
                        Dim fill = If(ser.MarkerFillColor <> Color.Empty, ser.MarkerFillColor, 解析点颜色(point, ser, seriesIndex))
                        Dim border = If(ser.MarkerBorderColor <> Color.Empty, ser.MarkerBorderColor, 绘图区背景颜色)
                        draw.Markers.Add(New MarkerDrawInfo With {
                            .Center = New PointF(x, y),
                            .Shape = ser.MarkerShape,
                            .Size = ser.MarkerSize * s,
                            .FillColor = fill,
                            .BorderColor = border,
                            .BorderThickness = ser.MarkerBorderThickness * s})
                    End If
                    添加值标签(layout, ser, i, point.Value, New PointF(x, y), color, s)
                Next
                If currentSegment.Count > 0 Then draw.LineSegments.Add(currentSegment)
            End If
            layout.SeriesDraws.Add(draw)
            seriesIndex += 1
        Next
    End Sub

    Private Sub 添加值标签(layout As ChartLayoutInfo, ser As ChartSeries, categoryIndex As Integer, value As Double, anchor As PointF, fallbackColor As Color, s As Single)
        If Not 系列显示值标签(ser) Then Return
        Dim relativeIndex = categoryIndex - layout.ViewStartIndex
        If Not 应显示间隔项(relativeIndex, layout.CategoryCount, layout.EffectiveValueLabelInterval) Then Return
        Dim labelFont = 获取值标签字体()
        Dim format = If(String.IsNullOrEmpty(ser.ValueLabelFormat), 值标签格式, ser.ValueLabelFormat)
        Dim text = 格式化数值(value, format)
        Dim size = 测量文本尺寸(text, labelFont)
        Dim yOffset As Single = 值标签偏移 * s
        Dim rect As New RectangleF(anchor.X - size.Width / 2.0F, anchor.Y - size.Height - yOffset, size.Width + 2 * s, size.Height + 2 * s)
        If value < 0 Then rect.Y = anchor.Y + yOffset
        layout.ValueLabels.Add(New ValueLabelInfo With {
            .Text = text,
            .Rect = rect,
            .Color = If(ser.ValueLabelColor <> Color.Empty, ser.ValueLabelColor, 值标签颜色)})
    End Sub

    Private Function 预留图例区域(content As RectangleF, visibleSeries As List(Of ChartSeries), s As Single) As RectangleF
        Dim legendFont = 获取图例字体()
        Dim lineH As Single = Math.Max(16 * s, 测量文本尺寸("Ag", legendFont).Height + 6 * s)
        Select Case 图例位置
            Case LegendPositionEnum.Top
                Dim h As Single = 计算水平图例高度(content.Width, visibleSeries, lineH, s)
                Return New RectangleF(content.Left, content.Top, content.Width, h)
            Case LegendPositionEnum.Bottom
                Dim h As Single = 计算水平图例高度(content.Width, visibleSeries, lineH, s)
                Return New RectangleF(content.Left, content.Bottom - h, content.Width, h)
            Case LegendPositionEnum.Left
                Dim w As Single = 计算垂直图例宽度(content.Width, visibleSeries, s)
                Return New RectangleF(content.Left, content.Top, w, content.Height)
            Case LegendPositionEnum.Right
                Dim w As Single = 计算垂直图例宽度(content.Width, visibleSeries, s)
                Return New RectangleF(content.Right - w, content.Top, w, content.Height)
            Case Else
                Return RectangleF.Empty
        End Select
    End Function

    Private Function 扣除图例区域(content As RectangleF, legendRect As RectangleF) As RectangleF
        If legendRect.IsEmpty Then Return content
        Dim gap As Single = 图例间距 * DpiScale()
        Select Case 图例位置
            Case LegendPositionEnum.Top
                Return New RectangleF(content.Left, legendRect.Bottom + gap, content.Width, Math.Max(1, content.Bottom - legendRect.Bottom - gap))
            Case LegendPositionEnum.Bottom
                Return New RectangleF(content.Left, content.Top, content.Width, Math.Max(1, legendRect.Top - gap - content.Top))
            Case LegendPositionEnum.Left
                Return New RectangleF(legendRect.Right + gap, content.Top, Math.Max(1, content.Right - legendRect.Right - gap), content.Height)
            Case LegendPositionEnum.Right
                Return New RectangleF(content.Left, content.Top, Math.Max(1, legendRect.Left - gap - content.Left), content.Height)
            Case Else
                Return content
        End Select
    End Function

    Private Sub 生成图例(layout As ChartLayoutInfo, legendRect As RectangleF, visibleSeries As List(Of ChartSeries), s As Single)
        If legendRect.IsEmpty Then Return
        Dim legendFont = 获取图例字体()
        Dim markerW As Single = 18 * s
        Dim gap As Single = 6 * s
        Dim itemGap As Single = 16 * s
        Dim lineH As Single = Math.Max(16 * s, 测量文本尺寸("Ag", legendFont).Height + 6 * s)
        Dim x As Single = legendRect.Left
        Dim y As Single = legendRect.Top
        Dim seriesIndex As Integer = 0
        For Each ser In visibleSeries
            Dim text = If(String.IsNullOrEmpty(ser.Name), $"Series {seriesIndex + 1}", ser.Name)
            Dim textSize = 测量文本尺寸(text, legendFont)
            Dim itemW As Single = markerW + gap + textSize.Width + itemGap
            If 图例位置 = LegendPositionEnum.Top OrElse 图例位置 = LegendPositionEnum.Bottom Then
                If x > legendRect.Left AndAlso x + itemW > legendRect.Right Then
                    x = legendRect.Left
                    y += lineH
                End If
            End If
            Dim markerRect As New RectangleF(x, y + (lineH - 10 * s) / 2.0F, markerW, 10 * s)
            Dim textRect As New RectangleF(x + markerW + gap, y, Math.Max(1, textSize.Width + 4 * s), lineH)
            layout.LegendItems.Add(New LegendItemInfo With {
                .Series = ser,
                .SeriesIndex = seriesIndex,
                .Color = 解析系列颜色(ser, seriesIndex),
                .MarkerRect = markerRect,
                .TextRect = textRect,
                .Text = text})
            If 图例位置 = LegendPositionEnum.Left OrElse 图例位置 = LegendPositionEnum.Right Then
                y += lineH
            Else
                x += itemW
            End If
            seriesIndex += 1
        Next
    End Sub

    Private Function 计算水平图例高度(width As Single, visibleSeries As List(Of ChartSeries), lineH As Single, s As Single) As Single
        Dim legendFont = 获取图例字体()
        Dim x As Single = 0
        Dim rows As Integer = 1
        For i As Integer = 0 To visibleSeries.Count - 1
            Dim text = If(String.IsNullOrEmpty(visibleSeries(i).Name), $"Series {i + 1}", visibleSeries(i).Name)
            Dim w As Single = 18 * s + 6 * s + 测量文本尺寸(text, legendFont).Width + 16 * s
            If x > 0 AndAlso x + w > width Then
                rows += 1
                x = 0
            End If
            x += w
        Next
        Return rows * lineH
    End Function

    Private Function 计算垂直图例宽度(width As Single, visibleSeries As List(Of ChartSeries), s As Single) As Single
        Dim legendFont = 获取图例字体()
        Dim maxW As Single = 90 * s
        For i As Integer = 0 To visibleSeries.Count - 1
            Dim text = If(String.IsNullOrEmpty(visibleSeries(i).Name), $"Series {i + 1}", visibleSeries(i).Name)
            maxW = Math.Max(maxW, 18 * s + 6 * s + 测量文本尺寸(text, legendFont).Width + 12 * s)
        Next
        Return Math.Min(maxW, width * 0.35F)
    End Function
#End Region

#Region "计算辅助"
    Private Function 获取可见系列() As List(Of ChartSeries)
        Return _series.Where(Function(s) s IsNot Nothing AndAlso s.Visible).ToList()
    End Function

    Private Function 获取分类数量() As Integer
        Dim count = _categories.Count
        For Each ser In _series
            If ser IsNot Nothing AndAlso ser.Visible Then count = Math.Max(count, ser.Points.Count)
        Next
        Return count
    End Function

    Private Function 获取分类标签(index As Integer) As String
        If index >= 0 AndAlso index < _categories.Count AndAlso Not String.IsNullOrEmpty(_categories(index)) Then Return _categories(index)
        For Each ser In _series
            If ser IsNot Nothing AndAlso index >= 0 AndAlso index < ser.Points.Count Then
                Dim label = ser.Points(index)?.Label
                If Not String.IsNullOrEmpty(label) Then Return label
            End If
        Next
        Return (index + 1).ToString(CultureInfo.CurrentCulture)
    End Function

    Private Function 获取规范化X轴视图(totalCount As Integer, startIndex As Integer, categoryCount As Integer) As (StartIndex As Integer, EndIndex As Integer, Count As Integer)
        If totalCount <= 0 Then Return (0, 0, 0)

        Dim minCount As Integer = Math.Min(totalCount, Math.Max(1, X轴最少可见分类数))
        If categoryCount <= 0 OrElse categoryCount >= totalCount Then Return (0, totalCount, totalCount)

        categoryCount = Math.Max(minCount, Math.Min(totalCount, categoryCount))
        startIndex = Math.Max(0, Math.Min(startIndex, totalCount - categoryCount))
        Return (startIndex, startIndex + categoryCount, categoryCount)
    End Function

    Private Sub 设置X轴视图(startIndex As Integer, categoryCount As Integer)
        startIndex = Math.Max(0, startIndex)
        categoryCount = Math.Max(0, categoryCount)

        Dim totalCount = 获取分类数量()
        If totalCount > 0 Then
            Dim view = 获取规范化X轴视图(totalCount, startIndex, categoryCount)
            startIndex = view.StartIndex
            categoryCount = If(view.Count >= totalCount, 0, view.Count)
        End If

        If X轴视图起始索引 = startIndex AndAlso X轴视图分类数量 = categoryCount Then Return
        X轴视图起始索引 = startIndex
        X轴视图分类数量 = categoryCount
        NotifyStyleChanged()
    End Sub

    Private Function 获取自动间隔(baseInterval As Integer, categoryWidth As Single, minimumSpacing As Single) As Integer
        Dim interval As Integer = Math.Max(1, baseInterval)
        If Not 自动跳过过密标签 OrElse minimumSpacing <= 0 OrElse categoryWidth <= 0 Then Return interval
        Return Math.Max(interval, CInt(Math.Ceiling(minimumSpacing / Math.Max(0.01F, categoryWidth))))
    End Function

    Private Shared Function 应显示间隔项(relativeIndex As Integer, totalCount As Integer, interval As Integer) As Boolean
        If totalCount <= 0 OrElse relativeIndex < 0 OrElse relativeIndex >= totalCount Then Return False
        If interval <= 1 Then Return True
        Return relativeIndex = 0 OrElse relativeIndex = totalCount - 1 OrElse relativeIndex Mod interval = 0
    End Function

    Private Function 获取柱基线Y坐标(layout As ChartLayoutInfo) As Single
        If layout.ValueMin <= 0 AndAlso layout.ValueMax >= 0 Then Return 值到Y坐标(0, layout)
        If layout.ValueMin > 0 Then Return layout.PlotRect.Bottom
        Return layout.PlotRect.Top
    End Function

    Private Function 计算值范围(visibleSeries As List(Of ChartSeries), layout As ChartLayoutInfo) As (Minimum As Double, Maximum As Double)
        Dim hasValue As Boolean
        Dim minVal As Double = Double.PositiveInfinity
        Dim maxVal As Double = Double.NegativeInfinity
        For Each ser In visibleSeries
            For i As Integer = layout.ViewStartIndex To layout.ViewEndIndex - 1
                If i < 0 OrElse i >= ser.Points.Count Then Continue For
                Dim point = ser.Points(i)
                If point Is Nothing OrElse Not 是有效数值(point.Value) Then Continue For
                hasValue = True
                minVal = Math.Min(minVal, point.Value)
                maxVal = Math.Max(maxVal, point.Value)
            Next
        Next

        If Not hasValue Then
            minVal = 0
            maxVal = 1
        End If

        If Y轴范围模式 = AxisRangeModeEnum.Fixed Then
            minVal = Y轴最小值
            maxVal = Y轴最大值
            If maxVal <= minVal Then maxVal = minVal + 1
            Return (minVal, maxVal)
        End If

        If maxVal <= minVal Then
            Dim delta As Double = Math.Max(Math.Abs(maxVal) * Math.Max(0.0, Y轴自动余量比例), Double.Epsilon)
            If delta <= Double.Epsilon Then delta = 0.5
            minVal -= delta
            maxVal += delta
        Else
            Dim padding As Double = (maxVal - minVal) * Math.Max(0.0, Y轴自动余量比例)
            minVal -= padding
            maxVal += padding
        End If

        If Y轴包含零 Then
            minVal = Math.Min(0, minVal)
            maxVal = Math.Max(0, maxVal)
        End If
        If maxVal <= minVal Then
            Dim delta As Double = Math.Max(Math.Abs(maxVal) * Math.Max(0.0, Y轴自动余量比例), Double.Epsilon)
            If delta <= Double.Epsilon Then delta = 0.5
            minVal -= delta
            maxVal += delta
        End If

        Dim targetTicks = Math.Max(2, Y轴目标刻度数)
        Dim interval = If(Y轴主刻度间隔 > 0, Y轴主刻度间隔, NiceNumber((maxVal - minVal) / (targetTicks - 1), True))
        If interval <= 0 OrElse Double.IsInfinity(interval) OrElse Double.IsNaN(interval) Then interval = 1
        Dim niceMin = Math.Floor(minVal / interval) * interval
        Dim niceMax = Math.Ceiling(maxVal / interval) * interval
        If niceMax <= niceMin Then niceMax = niceMin + interval
        Return (niceMin, niceMax)
    End Function

    Private Function 生成刻度值(minimum As Double, maximum As Double) As List(Of Double)
        Dim result As New List(Of Double)
        Dim interval As Double = Y轴主刻度间隔
        If interval <= 0 Then interval = NiceNumber((maximum - minimum) / Math.Max(1, Y轴目标刻度数 - 1), True)
        If interval <= 0 OrElse Double.IsNaN(interval) OrElse Double.IsInfinity(interval) Then interval = 1

        Dim startValue = Math.Ceiling(minimum / interval) * interval
        Dim value = startValue
        Dim guard As Integer = 0
        While value <= maximum + interval * 0.001 AndAlso guard < 200
            If Math.Abs(value) < interval * 0.000001 Then value = 0
            result.Add(value)
            value += interval
            guard += 1
        End While
        If result.Count = 0 Then
            result.Add(minimum)
            result.Add(maximum)
        End If
        Return result
    End Function

    Private Function 获取有效Y轴标签格式(minimum As Double, maximum As Double, tickValues As List(Of Double)) As String
        If Not Y轴自动小数精度 Then Return Y轴标签格式
        If Y轴范围模式 = AxisRangeModeEnum.Fixed AndAlso Not String.IsNullOrEmpty(Y轴标签格式) Then Return Y轴标签格式
        If Not String.IsNullOrEmpty(Y轴标签格式) AndAlso Y轴标签格式.Contains("{0", StringComparison.Ordinal) Then Return Y轴标签格式

        Dim interval As Double = 0
        If tickValues IsNot Nothing AndAlso tickValues.Count >= 2 Then
            interval = Math.Abs(tickValues(1) - tickValues(0))
        Else
            interval = Math.Abs(maximum - minimum)
        End If
        If interval <= 0 OrElse Double.IsNaN(interval) OrElse Double.IsInfinity(interval) Then Return If(String.IsNullOrEmpty(Y轴标签格式), "G4", Y轴标签格式)

        Dim decimals As Integer = Math.Max(0, CInt(Math.Ceiling(-Math.Log10(interval))) + 1)
        decimals = Math.Min(8, decimals)
        If decimals <= 0 Then Return If(String.IsNullOrEmpty(Y轴标签格式), "G4", Y轴标签格式)

        Return "0." & New String("0"c, decimals)
    End Function

    Private Shared Function NiceNumber(value As Double, roundValue As Boolean) As Double
        If value <= 0 OrElse Double.IsNaN(value) OrElse Double.IsInfinity(value) Then Return 1
        Dim exponent = Math.Floor(Math.Log10(value))
        Dim fraction = value / Math.Pow(10, exponent)
        Dim niceFraction As Double
        If roundValue Then
            If fraction < 1.5 Then
                niceFraction = 1
            ElseIf fraction < 3 Then
                niceFraction = 2
            ElseIf fraction < 7 Then
                niceFraction = 5
            Else
                niceFraction = 10
            End If
        Else
            If fraction <= 1 Then
                niceFraction = 1
            ElseIf fraction <= 2 Then
                niceFraction = 2
            ElseIf fraction <= 5 Then
                niceFraction = 5
            Else
                niceFraction = 10
            End If
        End If
        Return niceFraction * Math.Pow(10, exponent)
    End Function

    Private Function 值到Y坐标(value As Double, layout As ChartLayoutInfo) As Single
        Dim range = layout.ValueMax - layout.ValueMin
        If range <= 0 Then Return layout.PlotRect.Bottom
        Dim ratio = (value - layout.ValueMin) / range
        Return CSng(layout.PlotRect.Bottom - ratio * layout.PlotRect.Height)
    End Function

    Private Function 获取折线悬停目标(layout As ChartLayoutInfo) As HoverTargetInfo
        Dim result As New HoverTargetInfo()
        If Not 显示折线悬停十字线 OrElse Not _hasHoverPoint OrElse layout Is Nothing OrElse layout.CategoryCount <= 0 Then Return result

        Dim plot = layout.PlotRect
        If Not plot.Contains(_hoverClientPoint) Then Return result

        Dim catW As Single = plot.Width / layout.CategoryCount
        If catW <= 0 Then Return result

        Dim snapDistance As Single = Math.Max(1.0F, 折线悬停吸附半径 * layout.Scale)
        Dim minRelativeIndex As Integer = Math.Max(0, CInt(Math.Floor((_hoverClientPoint.X - snapDistance - plot.Left) / catW - 0.5F)))
        Dim maxRelativeIndex As Integer = Math.Min(layout.CategoryCount - 1, CInt(Math.Ceiling((_hoverClientPoint.X + snapDistance - plot.Left) / catW - 0.5F)))
        If maxRelativeIndex < minRelativeIndex Then Return result

        Dim bestDistance As Double = Double.PositiveInfinity
        Dim bestSeries As ChartSeries = Nothing
        Dim bestSeriesIndex As Integer = -1
        Dim bestCategoryIndex As Integer = -1
        Dim bestValue As Double = 0
        Dim bestPoint As PointF = PointF.Empty
        Dim bestColor As Color = Color.Empty

        Dim visibleSeries = 获取可见系列()
        For seriesIndex As Integer = 0 To visibleSeries.Count - 1
            Dim ser = visibleSeries(seriesIndex)
            If ser Is Nothing OrElse ser.ChartType <> ChartSeriesTypeEnum.Line Then Continue For

            For relativeIndex As Integer = minRelativeIndex To maxRelativeIndex
                Dim i As Integer = layout.ViewStartIndex + relativeIndex
                If i < 0 OrElse i >= ser.Points.Count Then Continue For
                Dim point = ser.Points(i)
                If point Is Nothing OrElse Not 是有效数值(point.Value) Then Continue For

                Dim x As Single = plot.Left + catW * (relativeIndex + 0.5F)
                Dim y As Single = 值到Y坐标(point.Value, layout)
                Dim dx As Double = Math.Abs(_hoverClientPoint.X - x)
                If dx > snapDistance Then Continue For

                Dim dy As Double = Math.Abs(_hoverClientPoint.Y - y)
                Dim distance As Double = Math.Sqrt(dx * dx + dy * dy)
                If distance > snapDistance OrElse distance >= bestDistance Then Continue For

                bestDistance = distance
                bestSeries = ser
                bestSeriesIndex = seriesIndex
                bestCategoryIndex = i
                bestValue = point.Value
                bestPoint = New PointF(x, y)
                bestColor = 解析点颜色(point, ser, seriesIndex)
            Next
        Next

        If bestSeries Is Nothing Then Return result

        Dim text = 格式化悬停提示文本(bestSeries, bestCategoryIndex, bestValue)
        Dim font = 获取悬停提示字体()
        Dim textSize = 测量文本尺寸(text, font)
        Dim padding As Single = 折线悬停提示内边距 * layout.Scale
        Dim gap As Single = 折线悬停提示间距 * layout.Scale
        Dim tooltipW As Single = Math.Max(1.0F, textSize.Width + padding * 2)
        Dim tooltipH As Single = Math.Max(1.0F, textSize.Height + padding * 2)

        Dim tooltipX As Single = If(bestPoint.X + gap + tooltipW <= plot.Right, bestPoint.X + gap, bestPoint.X - gap - tooltipW)
        Dim tooltipY As Single = bestPoint.Y - gap - tooltipH
        If tooltipY < plot.Top Then tooltipY = bestPoint.Y + gap
        If tooltipY + tooltipH > plot.Bottom Then tooltipY = Math.Max(plot.Top, plot.Bottom - tooltipH)
        tooltipX = Math.Max(plot.Left, Math.Min(plot.Right - tooltipW, tooltipX))

        result.HasTarget = True
        result.SeriesIndex = bestSeriesIndex
        result.CategoryIndex = bestCategoryIndex
        result.Value = bestValue
        result.Point = bestPoint
        result.Color = bestColor
        result.TooltipText = text
        result.TooltipRect = New RectangleF(tooltipX, tooltipY, tooltipW, tooltipH)
        result.TextRect = New RectangleF(tooltipX + padding, tooltipY + padding, Math.Max(1.0F, tooltipW - padding * 2), Math.Max(1.0F, tooltipH - padding * 2))
        Return result
    End Function

    Private Function 格式化悬停提示文本(series As ChartSeries, categoryIndex As Integer, value As Double) As String
        Dim valueText = 格式化数值(value, If(String.IsNullOrEmpty(折线悬停值格式), If(String.IsNullOrEmpty(series.ValueLabelFormat), 值标签格式, series.ValueLabelFormat), 折线悬停值格式))
        Dim categoryText = 获取分类标签(categoryIndex)

        If String.IsNullOrEmpty(折线悬停提示格式) Then
            If String.IsNullOrEmpty(series.Name) Then Return $"{categoryText}: {valueText}"
            Return $"{series.Name}  {categoryText}: {valueText}"
        End If

        Try
            Return String.Format(CultureInfo.CurrentCulture, 折线悬停提示格式, series.Name, categoryText, value, valueText)
        Catch
            Return valueText
        End Try
    End Function

    Private Function 解析系列颜色(series As ChartSeries, seriesIndex As Integer) As Color
        If series IsNot Nothing AndAlso series.Color <> Color.Empty Then Return series.Color
        If _palette.Count > 0 Then Return _palette(Math.Abs(seriesIndex) Mod _palette.Count)
        Return Color.FromArgb(68, 114, 196)
    End Function

    Private Function 解析点颜色(point As ChartPoint, series As ChartSeries, seriesIndex As Integer) As Color
        If point IsNot Nothing AndAlso point.Color <> Color.Empty Then Return point.Color
        Return 解析系列颜色(series, seriesIndex)
    End Function

    Private Function 系列显示值标签(series As ChartSeries) As Boolean
        Select Case series.ShowValueLabels
            Case SeriesValueLabelModeEnum.Show
                Return True
            Case SeriesValueLabelModeEnum.Hide
                Return False
            Case Else
                Return 显示值标签
        End Select
    End Function

    Private Shared Function 是有效数值(value As Double) As Boolean
        Return Not Double.IsNaN(value) AndAlso Not Double.IsInfinity(value)
    End Function

    Private Function 格式化数值(value As Double, format As String) As String
        If String.IsNullOrEmpty(format) Then Return value.ToString("G4", CultureInfo.CurrentCulture)
        Try
            If format.Contains("{0", StringComparison.Ordinal) Then Return String.Format(CultureInfo.CurrentCulture, format, value)
            Return value.ToString(format, CultureInfo.CurrentCulture)
        Catch
            Return value.ToString("G4", CultureInfo.CurrentCulture)
        End Try
    End Function

    Private Shared Function 调整亮度(color As Color, amount As Single) As Color
        Dim r = Math.Clamp(CInt(color.R + 255 * amount), 0, 255)
        Dim g = Math.Clamp(CInt(color.G + 255 * amount), 0, 255)
        Dim b = Math.Clamp(CInt(color.B + 255 * amount), 0, 255)
        Return Color.FromArgb(color.A, r, g, b)
    End Function

    Private Function DpiScale() As Single
        Return V3_DpiContext.FromControl(Me).Scale
    End Function

    Private Sub 请求V3渲染(Optional immediate As Boolean = False)
        请求V3渲染(New Rectangle(Point.Empty, Me.Size), immediate)
    End Sub

    Private Sub 请求V3渲染(dirtyRect As Rectangle, Optional immediate As Boolean = False)
        If Me.IsDisposed Then Return
        V3_InvalidationRouter.RequestRender(Me, dirtyRect)
    End Sub

    Private Function 测量文本尺寸(text As String, font As Font) As Size
        If String.IsNullOrEmpty(text) Then Return Size.Empty
        Return TextRenderer.MeasureText(text, font, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
    End Function


    Private Shared Function 创建折线几何(points As IList(Of PointF)) As D2D.ID2D1PathGeometry
        Dim path = D3D_RenderCore.DeviceManager.D2DFactory.CreatePathGeometry()
        Dim sink = path.Open()
        Try
            sink.BeginFigure(New Vector2(points(0).X, points(0).Y), D2D.FigureBegin.Hollow)
            For i As Integer = 1 To points.Count - 1
                sink.AddLine(New Vector2(points(i).X, points(i).Y))
            Next
            sink.EndFigure(D2D.FigureEnd.Open)
            sink.Close()
        Finally
            sink.Dispose()
        End Try
        Return path
    End Function

    Private Shared Function 创建菱形几何(center As PointF, half As Single) As D2D.ID2D1PathGeometry
        Dim path = D3D_RenderCore.DeviceManager.D2DFactory.CreatePathGeometry()
        Dim sink = path.Open()
        Try
            sink.BeginFigure(New Vector2(center.X, center.Y - half), D2D.FigureBegin.Filled)
            sink.AddLine(New Vector2(center.X + half, center.Y))
            sink.AddLine(New Vector2(center.X, center.Y + half))
            sink.AddLine(New Vector2(center.X - half, center.Y))
            sink.EndFigure(D2D.FigureEnd.Closed)
            sink.Close()
        Finally
            sink.Dispose()
        End Try
        Return path
    End Function
#End Region

#Region "变更通知"
    Friend Sub NotifyDataChanged()
        _dataVersion += 1
        _layoutCache = Nothing
        标记图表变化()
    End Sub

    Friend Sub NotifyStyleChanged()
        _styleVersion += 1
        _layoutCache = Nothing
        标记图表变化()
    End Sub

    Private Sub 标记图表变化()
        If _updateCount > 0 Then
            _pendingInvalidate = True
            _pendingChartChanged = True
            Return
        End If
        RaiseEvent ChartChanged(Me, EventArgs.Empty)
        If AutoRefresh Then 请求V3渲染()
    End Sub

    Private Sub SetValue(Of T)(ByRef field As T, value As T)
        If EqualityComparer(Of T).Default.Equals(field, value) Then Return
        field = value
        NotifyStyleChanged()
    End Sub
#End Region

#Region "属性 - 常规"
    Private _autoRefresh As Boolean = True
    <Category("LakeUI"), Description("数据或样式变化时是否自动刷新。关闭后可调用 RefreshChart 手动刷新。"), DefaultValue(True), Browsable(True)>
    Public Property AutoRefresh As Boolean
        Get
            Return _autoRefresh
        End Get
        Set(value As Boolean)
            If _autoRefresh = value Then Return
            _autoRefresh = value
            If value Then 请求V3渲染()
        End Set
    End Property

    Private 超采样倍率 As Integer = 1
    <Category("LakeUI"), Description(GlobalOptions.超采样抗锯齿描述词), DefaultValue(GetType(GlobalOptions.SuperSamplingScaleEnum), "OFF"), Browsable(True)>
    Public Property SuperSamplingScale As GlobalOptions.SuperSamplingScaleEnum
        Get
            Return CType(超采样倍率, GlobalOptions.SuperSamplingScaleEnum)
        End Get
        Set(value As GlobalOptions.SuperSamplingScaleEnum)
            SetValue(超采样倍率, CInt(value))
        End Set
    End Property

    Private _backgroundSource As Control = Nothing
    <Category("LakeUI"),
     Description("背景采样源（超容器背景映射）。设置后将跨越任意层级直接采样此控件的绘制内容作为透明背景；为空时不进行背景采样。"),
     DefaultValue(GetType(Control), Nothing), Browsable(True)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource Is value Then Return
            _backgroundSource = D3D_BackgroundPenetration.SetBackgroundSource(Me, _backgroundSource, value)
            NotifyStyleChanged()
        End Set
    End Property

    Private 禁用时遮罩颜色 As Color = Color.FromArgb(120, 0, 0, 0)
    <Category("LakeUI"), Description("禁用时覆盖在图表上的遮罩颜色。"), DefaultValue(GetType(Color), "120, 0, 0, 0"), Browsable(True)>
    Public Property DisabledOverlayColor As Color
        Get
            Return 禁用时遮罩颜色
        End Get
        Set(value As Color)
            SetValue(禁用时遮罩颜色, value)
        End Set
    End Property
#End Region

#Region "属性 - 标题与文字"
    Private 显示标题 As Boolean = True
    <Category("LakeUI"), Description("是否显示标题。"), DefaultValue(True), Browsable(True)>
    Public Property ShowTitle As Boolean
        Get
            Return 显示标题
        End Get
        Set(value As Boolean)
            SetValue(显示标题, value)
        End Set
    End Property

    Private 标题文本 As String = ""
    <Category("LakeUI"), Description("图表标题。"), DefaultValue(""), Browsable(True)>
    Public Property Title As String
        Get
            Return 标题文本
        End Get
        Set(value As String)
            SetValue(标题文本, If(value, ""))
        End Set
    End Property

    Private 标题颜色 As Color = Color.Gainsboro
    <Category("LakeUI"), Description("标题颜色。"), DefaultValue(GetType(Color), "Gainsboro"), Browsable(True)>
    Public Property TitleColor As Color
        Get
            Return 标题颜色
        End Get
        Set(value As Color)
            SetValue(标题颜色, value)
        End Set
    End Property

    Private _titleFont As Font = Nothing
    <Category("LakeUI"), Description("标题字体；为空时使用控件 Font。"), Browsable(True)>
    Public Property TitleFont As Font
        Get
            Return _titleFont
        End Get
        Set(value As Font)
            If Object.ReferenceEquals(_titleFont, value) Then Return
            _titleFont = value
            NotifyStyleChanged()
        End Set
    End Property

    Private Function ShouldSerializeTitleFont() As Boolean
        Return _titleFont IsNot Nothing
    End Function

    Private Sub ResetTitleFont()
        TitleFont = Nothing
    End Sub

    Private 标题下间距 As Single = 10.0F
    <Category("LakeUI"), Description("标题与图表主体的间距。"), DefaultValue(10.0F), Browsable(True)>
    Public Property TitleSpacing As Single
        Get
            Return 标题下间距
        End Get
        Set(value As Single)
            SetValue(标题下间距, Math.Max(0.0F, value))
        End Set
    End Property

    Private 显示值标签 As Boolean = False
    <Category("LakeUI"), Description("是否默认显示数据值标签。各系列可单独覆盖。"), DefaultValue(False), Browsable(True)>
    Public Property ShowValueLabels As Boolean
        Get
            Return 显示值标签
        End Get
        Set(value As Boolean)
            SetValue(显示值标签, value)
        End Set
    End Property

    Private 值标签格式 As String = ""
    <Category("LakeUI"), Description("值标签格式；支持数值格式或 String.Format 形式，例如 {0:P1}。"), DefaultValue(""), Browsable(True)>
    Public Property ValueLabelFormat As String
        Get
            Return 值标签格式
        End Get
        Set(value As String)
            SetValue(值标签格式, If(value, ""))
        End Set
    End Property

    Private 值标签颜色 As Color = Color.Gainsboro
    <Category("LakeUI"), Description("值标签颜色。"), DefaultValue(GetType(Color), "Gainsboro"), Browsable(True)>
    Public Property ValueLabelColor As Color
        Get
            Return 值标签颜色
        End Get
        Set(value As Color)
            SetValue(值标签颜色, value)
        End Set
    End Property

    Private _valueLabelFont As Font = Nothing
    <Category("LakeUI"), Description("值标签字体；为空时使用控件 Font。"), Browsable(True)>
    Public Property ValueLabelFont As Font
        Get
            Return _valueLabelFont
        End Get
        Set(value As Font)
            If Object.ReferenceEquals(_valueLabelFont, value) Then Return
            _valueLabelFont = value
            NotifyStyleChanged()
        End Set
    End Property

    Private Function ShouldSerializeValueLabelFont() As Boolean
        Return _valueLabelFont IsNot Nothing
    End Function

    Private Sub ResetValueLabelFont()
        ValueLabelFont = Nothing
    End Sub

    Private 值标签偏移 As Single = 4.0F
    <Category("LakeUI"), Description("值标签距离数据点/柱顶的偏移。"), DefaultValue(4.0F), Browsable(True)>
    Public Property ValueLabelOffset As Single
        Get
            Return 值标签偏移
        End Get
        Set(value As Single)
            SetValue(值标签偏移, Math.Max(0.0F, value))
        End Set
    End Property
#End Region

#Region "属性 - 坐标轴"
    Private X轴标题文本 As String = ""
    <Category("LakeUI"), Description("X 轴标题。"), DefaultValue(""), Browsable(True)>
    Public Property XAxisTitle As String
        Get
            Return X轴标题文本
        End Get
        Set(value As String)
            SetValue(X轴标题文本, If(value, ""))
        End Set
    End Property

    Private Y轴标题文本 As String = ""
    <Category("LakeUI"), Description("Y 轴标题。"), DefaultValue(""), Browsable(True)>
    Public Property YAxisTitle As String
        Get
            Return Y轴标题文本
        End Get
        Set(value As String)
            SetValue(Y轴标题文本, If(value, ""))
        End Set
    End Property

    Private 坐标轴标题颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("坐标轴标题颜色。"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property AxisTitleColor As Color
        Get
            Return 坐标轴标题颜色
        End Get
        Set(value As Color)
            SetValue(坐标轴标题颜色, value)
        End Set
    End Property

    Private _axisTitleFont As Font = Nothing
    <Category("LakeUI"), Description("坐标轴标题字体；为空时使用控件 Font。"), Browsable(True)>
    Public Property AxisTitleFont As Font
        Get
            Return _axisTitleFont
        End Get
        Set(value As Font)
            If Object.ReferenceEquals(_axisTitleFont, value) Then Return
            _axisTitleFont = value
            NotifyStyleChanged()
        End Set
    End Property

    Private Function ShouldSerializeAxisTitleFont() As Boolean
        Return _axisTitleFont IsNot Nothing
    End Function

    Private Sub ResetAxisTitleFont()
        AxisTitleFont = Nothing
    End Sub

    Private 坐标轴标签颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("坐标轴标签颜色。"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property AxisLabelColor As Color
        Get
            Return 坐标轴标签颜色
        End Get
        Set(value As Color)
            SetValue(坐标轴标签颜色, value)
        End Set
    End Property

    Private _axisLabelFont As Font = Nothing
    <Category("LakeUI"), Description("坐标轴标签字体；为空时使用控件 Font。"), Browsable(True)>
    Public Property AxisLabelFont As Font
        Get
            Return _axisLabelFont
        End Get
        Set(value As Font)
            If Object.ReferenceEquals(_axisLabelFont, value) Then Return
            _axisLabelFont = value
            NotifyStyleChanged()
        End Set
    End Property

    Private Function ShouldSerializeAxisLabelFont() As Boolean
        Return _axisLabelFont IsNot Nothing
    End Function

    Private Sub ResetAxisLabelFont()
        AxisLabelFont = Nothing
    End Sub

    Private 坐标轴线颜色 As Color = Color.FromArgb(110, 110, 110)
    <Category("LakeUI"), Description("坐标轴线颜色。"), DefaultValue(GetType(Color), "110, 110, 110"), Browsable(True)>
    Public Property AxisLineColor As Color
        Get
            Return 坐标轴线颜色
        End Get
        Set(value As Color)
            SetValue(坐标轴线颜色, value)
        End Set
    End Property

    Private 坐标轴线粗细 As Single = 1.0F
    <Category("LakeUI"), Description("坐标轴线粗细。"), DefaultValue(1.0F), Browsable(True)>
    Public Property AxisLineThickness As Single
        Get
            Return 坐标轴线粗细
        End Get
        Set(value As Single)
            SetValue(坐标轴线粗细, Math.Max(0.0F, value))
        End Set
    End Property

    Private 坐标轴刻度线颜色 As Color = Color.FromArgb(130, 130, 130)
    <Category("LakeUI"), Description("坐标轴刻度线颜色。"), DefaultValue(GetType(Color), "130, 130, 130"), Browsable(True)>
    Public Property TickLineColor As Color
        Get
            Return 坐标轴刻度线颜色
        End Get
        Set(value As Color)
            SetValue(坐标轴刻度线颜色, value)
        End Set
    End Property

    Private 坐标轴刻度线粗细 As Single = 1.0F
    <Category("LakeUI"), Description("坐标轴刻度线粗细。"), DefaultValue(1.0F), Browsable(True)>
    Public Property TickLineThickness As Single
        Get
            Return 坐标轴刻度线粗细
        End Get
        Set(value As Single)
            SetValue(坐标轴刻度线粗细, Math.Max(0.0F, value))
        End Set
    End Property

    Private 主要刻度长度 As Single = 4.0F
    <Category("LakeUI"), Description("坐标轴主刻度线长度。"), DefaultValue(4.0F), Browsable(True)>
    Public Property MajorTickLength As Single
        Get
            Return 主要刻度长度
        End Get
        Set(value As Single)
            SetValue(主要刻度长度, Math.Max(0.0F, value))
        End Set
    End Property

    Private 显示X轴标签 As Boolean = True
    <Category("LakeUI"), Description("是否显示 X 轴标签。"), DefaultValue(True), Browsable(True)>
    Public Property ShowXAxisLabels As Boolean
        Get
            Return 显示X轴标签
        End Get
        Set(value As Boolean)
            SetValue(显示X轴标签, value)
        End Set
    End Property

    Private 显示Y轴标签 As Boolean = True
    <Category("LakeUI"), Description("是否显示 Y 轴标签。"), DefaultValue(True), Browsable(True)>
    Public Property ShowYAxisLabels As Boolean
        Get
            Return 显示Y轴标签
        End Get
        Set(value As Boolean)
            SetValue(显示Y轴标签, value)
        End Set
    End Property

    Private 显示X轴刻度线 As Boolean = True
    <Category("LakeUI"), Description("是否显示 X 轴刻度线。"), DefaultValue(True), Browsable(True)>
    Public Property ShowXAxisTicks As Boolean
        Get
            Return 显示X轴刻度线
        End Get
        Set(value As Boolean)
            SetValue(显示X轴刻度线, value)
        End Set
    End Property

    Private 显示Y轴刻度线 As Boolean = True
    <Category("LakeUI"), Description("是否显示 Y 轴刻度线。"), DefaultValue(True), Browsable(True)>
    Public Property ShowYAxisTicks As Boolean
        Get
            Return 显示Y轴刻度线
        End Get
        Set(value As Boolean)
            SetValue(显示Y轴刻度线, value)
        End Set
    End Property

    Private X轴标签间隔 As Integer = 1
    <Category("LakeUI"), Description("X 轴标签显示间隔。1 表示每个分类都显示。"), DefaultValue(1), Browsable(True)>
    Public Property XAxisLabelInterval As Integer
        Get
            Return X轴标签间隔
        End Get
        Set(value As Integer)
            SetValue(X轴标签间隔, Math.Max(1, value))
        End Set
    End Property

    Private X轴标签旋转角度 As Single = 0.0F
    <Category("LakeUI"), Description("X 轴标签旋转角度，范围 -90 到 90。"), DefaultValue(0.0F), Browsable(True)>
    Public Property XAxisLabelRotation As Single
        Get
            Return X轴标签旋转角度
        End Get
        Set(value As Single)
            SetValue(X轴标签旋转角度, Math.Max(-90.0F, Math.Min(90.0F, value)))
        End Set
    End Property

    Private X轴最大标签高度 As Single = 72.0F
    <Category("LakeUI"), Description("X 轴标签旋转时最多预留的标签高度。"), DefaultValue(72.0F), Browsable(True)>
    Public Property XAxisMaxLabelHeight As Single
        Get
            Return X轴最大标签高度
        End Get
        Set(value As Single)
            SetValue(X轴最大标签高度, Math.Max(12.0F, value))
        End Set
    End Property

    Private X轴视图起始索引 As Integer = 0
    <Category("LakeUI"), Description("X 轴当前视图起始分类索引。"), DefaultValue(0), Browsable(True)>
    Public Property XAxisViewStartIndex As Integer
        Get
            Return X轴视图起始索引
        End Get
        Set(value As Integer)
            设置X轴视图(value, X轴视图分类数量)
        End Set
    End Property

    Private X轴视图分类数量 As Integer = 0
    <Category("LakeUI"), Description("X 轴当前视图显示的分类数量。0 表示显示全部。"), DefaultValue(0), Browsable(True)>
    Public Property XAxisViewCategoryCount As Integer
        Get
            Return X轴视图分类数量
        End Get
        Set(value As Integer)
            设置X轴视图(X轴视图起始索引, value)
        End Set
    End Property

    Private 允许X轴缩放 As Boolean = True
    <Category("LakeUI"), Description("是否允许鼠标滚轮缩放 X 轴视图。"), DefaultValue(True), Browsable(True)>
    Public Property EnableXAxisZoom As Boolean
        Get
            Return 允许X轴缩放
        End Get
        Set(value As Boolean)
            SetValue(允许X轴缩放, value)
        End Set
    End Property

    Private 允许X轴拖动 As Boolean = True
    <Category("LakeUI"), Description("是否允许按住鼠标左键拖动 X 轴视图。"), DefaultValue(True), Browsable(True)>
    Public Property EnableXAxisPan As Boolean
        Get
            Return 允许X轴拖动
        End Get
        Set(value As Boolean)
            SetValue(允许X轴拖动, value)
        End Set
    End Property

    Private X轴最少可见分类数 As Integer = 2
    <Category("LakeUI"), Description("X 轴缩放时最少保留的可见分类数量。"), DefaultValue(2), Browsable(True)>
    Public Property XAxisMinimumViewCategoryCount As Integer
        Get
            Return X轴最少可见分类数
        End Get
        Set(value As Integer)
            SetValue(X轴最少可见分类数, Math.Max(1, value))
        End Set
    End Property

    Private X轴滚轮缩放比例 As Double = 1.25
    <Category("LakeUI"), Description("每次滚轮缩放 X 轴视图的比例。"), DefaultValue(1.25), Browsable(True)>
    Public Property XAxisWheelZoomRatio As Double
        Get
            Return X轴滚轮缩放比例
        End Get
        Set(value As Double)
            SetValue(X轴滚轮缩放比例, Math.Max(1.01, value))
        End Set
    End Property

    Private 自动跳过过密标签 As Boolean = True
    <Category("LakeUI"), Description("密集数据下是否自动跳过部分 X 轴标签、值标签和过密绘制点。"), DefaultValue(True), Browsable(True)>
    Public Property AutoSkipDenseData As Boolean
        Get
            Return 自动跳过过密标签
        End Get
        Set(value As Boolean)
            SetValue(自动跳过过密标签, value)
        End Set
    End Property

    Private X轴标签最小间距 As Single = 48.0F
    <Category("LakeUI"), Description("自动跳过时 X 轴标签之间的最小像素间距。"), DefaultValue(48.0F), Browsable(True)>
    Public Property XAxisLabelMinimumSpacing As Single
        Get
            Return X轴标签最小间距
        End Get
        Set(value As Single)
            SetValue(X轴标签最小间距, Math.Max(0.0F, value))
        End Set
    End Property

    Private 值标签最小间距 As Single = 56.0F
    <Category("LakeUI"), Description("自动跳过时数据值标签之间的最小像素间距。"), DefaultValue(56.0F), Browsable(True)>
    Public Property ValueLabelMinimumSpacing As Single
        Get
            Return 值标签最小间距
        End Get
        Set(value As Single)
            SetValue(值标签最小间距, Math.Max(0.0F, value))
        End Set
    End Property

    Private 数据点最小间距 As Single = 0.75F
    <Category("LakeUI"), Description("自动跳过时折线/柱状绘制点之间的最小像素间距。0 表示不因密度跳过绘制点。"), DefaultValue(0.75F), Browsable(True)>
    Public Property DataPointMinimumSpacing As Single
        Get
            Return 数据点最小间距
        End Get
        Set(value As Single)
            SetValue(数据点最小间距, Math.Max(0.0F, value))
        End Set
    End Property

    Private Y轴范围模式 As AxisRangeModeEnum = AxisRangeModeEnum.Auto
    <Category("LakeUI"), Description("Y 轴范围模式。"), DefaultValue(GetType(AxisRangeModeEnum), "Auto"), Browsable(True)>
    Public Property YAxisRangeMode As AxisRangeModeEnum
        Get
            Return Y轴范围模式
        End Get
        Set(value As AxisRangeModeEnum)
            SetValue(Y轴范围模式, value)
        End Set
    End Property

    Private Y轴最小值 As Double = 0
    <Category("LakeUI"), Description("Y 轴固定最小值。"), DefaultValue(0.0), Browsable(True)>
    Public Property YAxisMinimum As Double
        Get
            Return Y轴最小值
        End Get
        Set(value As Double)
            SetValue(Y轴最小值, value)
        End Set
    End Property

    Private Y轴最大值 As Double = 100
    <Category("LakeUI"), Description("Y 轴固定最大值。"), DefaultValue(100.0), Browsable(True)>
    Public Property YAxisMaximum As Double
        Get
            Return Y轴最大值
        End Get
        Set(value As Double)
            SetValue(Y轴最大值, value)
        End Set
    End Property

    Private Y轴主刻度间隔 As Double = 0
    <Category("LakeUI"), Description("Y 轴主刻度间隔。0 表示自动。"), DefaultValue(0.0), Browsable(True)>
    Public Property YAxisMajorInterval As Double
        Get
            Return Y轴主刻度间隔
        End Get
        Set(value As Double)
            SetValue(Y轴主刻度间隔, Math.Max(0.0, value))
        End Set
    End Property

    Private Y轴目标刻度数 As Integer = 6
    <Category("LakeUI"), Description("Y 轴自动刻度的目标数量。"), DefaultValue(6), Browsable(True)>
    Public Property YAxisTargetTickCount As Integer
        Get
            Return Y轴目标刻度数
        End Get
        Set(value As Integer)
            SetValue(Y轴目标刻度数, Math.Max(2, value))
        End Set
    End Property

    Private Y轴包含零 As Boolean = False
    <Category("LakeUI"), Description("Y 轴自动范围是否强制包含 0。"), DefaultValue(False), Browsable(True)>
    Public Property YAxisIncludeZero As Boolean
        Get
            Return Y轴包含零
        End Get
        Set(value As Boolean)
            SetValue(Y轴包含零, value)
        End Set
    End Property

    Private Y轴自动余量比例 As Double = 0.08
    <Category("LakeUI"), Description("Y 轴自动范围在数据最小值和最大值外侧追加的余量比例。"), DefaultValue(0.08), Browsable(True)>
    Public Property YAxisAutoPaddingRatio As Double
        Get
            Return Y轴自动余量比例
        End Get
        Set(value As Double)
            SetValue(Y轴自动余量比例, Math.Max(0.0, value))
        End Set
    End Property

    Private Y轴标签格式 As String = ""
    <Category("LakeUI"), Description("Y 轴标签格式；支持数值格式或 String.Format 形式。"), DefaultValue(""), Browsable(True)>
    Public Property YAxisLabelFormat As String
        Get
            Return Y轴标签格式
        End Get
        Set(value As String)
            SetValue(Y轴标签格式, If(value, ""))
        End Set
    End Property

    Private Y轴自动小数精度 As Boolean = True
    <Category("LakeUI"), Description("Y 轴自动范围较小时，是否自动提高刻度标签的小数精度。"), DefaultValue(True), Browsable(True)>
    Public Property YAxisAutoDecimalPrecision As Boolean
        Get
            Return Y轴自动小数精度
        End Get
        Set(value As Boolean)
            SetValue(Y轴自动小数精度, value)
        End Set
    End Property

    Private 坐标轴标签间距 As Single = 6.0F
    <Category("LakeUI"), Description("坐标轴标签与坐标轴之间的间距。"), DefaultValue(6.0F), Browsable(True)>
    Public Property AxisLabelSpacing As Single
        Get
            Return 坐标轴标签间距
        End Get
        Set(value As Single)
            SetValue(坐标轴标签间距, Math.Max(0.0F, value))
        End Set
    End Property

    Private 坐标轴标题间距 As Single = 8.0F
    <Category("LakeUI"), Description("坐标轴标题与坐标轴标签之间的间距。"), DefaultValue(8.0F), Browsable(True)>
    Public Property AxisTitleSpacing As Single
        Get
            Return 坐标轴标题间距
        End Get
        Set(value As Single)
            SetValue(坐标轴标题间距, Math.Max(0.0F, value))
        End Set
    End Property
#End Region

#Region "属性 - 网格与绘图区"
    Private 显示水平网格线 As Boolean = True
    <Category("LakeUI"), Description("是否显示水平网格线。"), DefaultValue(True), Browsable(True)>
    Public Property ShowHorizontalGridLines As Boolean
        Get
            Return 显示水平网格线
        End Get
        Set(value As Boolean)
            SetValue(显示水平网格线, value)
        End Set
    End Property

    Private 显示垂直网格线 As Boolean = False
    <Category("LakeUI"), Description("是否显示垂直网格线。"), DefaultValue(False), Browsable(True)>
    Public Property ShowVerticalGridLines As Boolean
        Get
            Return 显示垂直网格线
        End Get
        Set(value As Boolean)
            SetValue(显示垂直网格线, value)
        End Set
    End Property

    Private 网格线颜色 As Color = Color.FromArgb(48, 48, 48)
    <Category("LakeUI"), Description("网格线颜色。"), DefaultValue(GetType(Color), "48, 48, 48"), Browsable(True)>
    Public Property GridLineColor As Color
        Get
            Return 网格线颜色
        End Get
        Set(value As Color)
            SetValue(网格线颜色, value)
        End Set
    End Property

    Private 网格线粗细 As Single = 1.0F
    <Category("LakeUI"), Description("网格线粗细。"), DefaultValue(1.0F), Browsable(True)>
    Public Property GridLineThickness As Single
        Get
            Return 网格线粗细
        End Get
        Set(value As Single)
            SetValue(网格线粗细, Math.Max(0.0F, value))
        End Set
    End Property

    Private 绘图区背景颜色 As Color = Color.FromArgb(18, 18, 18)
    <Category("LakeUI"), Description("绘图区背景颜色。"), DefaultValue(GetType(Color), "18, 18, 18"), Browsable(True)>
    Public Property PlotBackColor As Color
        Get
            Return 绘图区背景颜色
        End Get
        Set(value As Color)
            SetValue(绘图区背景颜色, value)
        End Set
    End Property

    Private 绘图区边框颜色 As Color = Color.FromArgb(60, 60, 60)
    <Category("LakeUI"), Description("绘图区边框颜色。"), DefaultValue(GetType(Color), "60, 60, 60"), Browsable(True)>
    Public Property PlotBorderColor As Color
        Get
            Return 绘图区边框颜色
        End Get
        Set(value As Color)
            SetValue(绘图区边框颜色, value)
        End Set
    End Property

    Private 绘图区边框粗细 As Single = 1.0F
    <Category("LakeUI"), Description("绘图区边框粗细。"), DefaultValue(1.0F), Browsable(True)>
    Public Property PlotBorderThickness As Single
        Get
            Return 绘图区边框粗细
        End Get
        Set(value As Single)
            SetValue(绘图区边框粗细, Math.Max(0.0F, value))
        End Set
    End Property

    Private 绘图区内上边距 As Single = 4.0F
    <Category("LakeUI"), Description("绘图区顶部额外预留空间。"), DefaultValue(4.0F), Browsable(True)>
    Public Property PlotTopPadding As Single
        Get
            Return 绘图区内上边距
        End Get
        Set(value As Single)
            SetValue(绘图区内上边距, Math.Max(0.0F, value))
        End Set
    End Property
#End Region

#Region "属性 - 图例"
    Private 图例位置 As LegendPositionEnum = LegendPositionEnum.Bottom
    <Category("LakeUI"), Description("图例位置。"), DefaultValue(GetType(LegendPositionEnum), "Bottom"), Browsable(True)>
    Public Property LegendPosition As LegendPositionEnum
        Get
            Return 图例位置
        End Get
        Set(value As LegendPositionEnum)
            SetValue(图例位置, value)
        End Set
    End Property

    Private 图例文字颜色 As Color = Color.Silver
    <Category("LakeUI"), Description("图例文字颜色。"), DefaultValue(GetType(Color), "Silver"), Browsable(True)>
    Public Property LegendForeColor As Color
        Get
            Return 图例文字颜色
        End Get
        Set(value As Color)
            SetValue(图例文字颜色, value)
        End Set
    End Property

    Private _legendFont As Font = Nothing
    <Category("LakeUI"), Description("图例字体；为空时使用控件 Font。"), Browsable(True)>
    Public Property LegendFont As Font
        Get
            Return _legendFont
        End Get
        Set(value As Font)
            If Object.ReferenceEquals(_legendFont, value) Then Return
            _legendFont = value
            NotifyStyleChanged()
        End Set
    End Property

    Private Function ShouldSerializeLegendFont() As Boolean
        Return _legendFont IsNot Nothing
    End Function

    Private Sub ResetLegendFont()
        LegendFont = Nothing
    End Sub

    Private 图例间距 As Single = 10.0F
    <Category("LakeUI"), Description("图例与图表主体之间的间距。"), DefaultValue(10.0F), Browsable(True)>
    Public Property LegendSpacing As Single
        Get
            Return 图例间距
        End Get
        Set(value As Single)
            SetValue(图例间距, Math.Max(0.0F, value))
        End Set
    End Property
#End Region

#Region "属性 - 柱状图与折线"
    Private 柱组宽度比例 As Single = 0.72F
    <Category("LakeUI"), Description("每个分类内柱组占用宽度比例，范围 0.05 到 1。"), DefaultValue(0.72F), Browsable(True)>
    Public Property ColumnGroupWidthRatio As Single
        Get
            Return 柱组宽度比例
        End Get
        Set(value As Single)
            SetValue(柱组宽度比例, Math.Max(0.05F, Math.Min(1.0F, value)))
        End Set
    End Property

    Private 柱系列间距 As Single = 3.0F
    <Category("LakeUI"), Description("同一分类内多组柱之间的间距。"), DefaultValue(3.0F), Browsable(True)>
    Public Property ColumnSeriesSpacing As Single
        Get
            Return 柱系列间距
        End Get
        Set(value As Single)
            SetValue(柱系列间距, Math.Max(0.0F, value))
        End Set
    End Property
#End Region

#Region "属性 - 折线悬停"
    Private 显示折线悬停十字线 As Boolean = False
    <Category("LakeUI"), Description("是否在鼠标悬停折线点附近时显示十字线和值提示。"), DefaultValue(False), Browsable(True)>
    Public Property ShowLineHoverCrosshair As Boolean
        Get
            Return 显示折线悬停十字线
        End Get
        Set(value As Boolean)
            If 显示折线悬停十字线 = value Then Return
            显示折线悬停十字线 = value
            If Not value Then _hasHoverPoint = False
            NotifyStyleChanged()
        End Set
    End Property

    Private 折线悬停吸附半径 As Single = 18.0F
    <Category("LakeUI"), Description("折线悬停吸附半径，单位为像素。"), DefaultValue(18.0F), Browsable(True)>
    Public Property LineHoverSnapRadius As Single
        Get
            Return 折线悬停吸附半径
        End Get
        Set(value As Single)
            SetValue(折线悬停吸附半径, Math.Max(1.0F, value))
        End Set
    End Property

    Private 折线悬停十字线颜色 As Color = Color.FromArgb(150, 220, 220, 220)
    <Category("LakeUI"), Description("折线悬停十字线颜色。"), DefaultValue(GetType(Color), "150, 220, 220, 220"), Browsable(True)>
    Public Property LineHoverCrosshairColor As Color
        Get
            Return 折线悬停十字线颜色
        End Get
        Set(value As Color)
            SetValue(折线悬停十字线颜色, value)
        End Set
    End Property

    Private 折线悬停十字线粗细 As Single = 1.0F
    <Category("LakeUI"), Description("折线悬停十字线粗细。"), DefaultValue(1.0F), Browsable(True)>
    Public Property LineHoverCrosshairThickness As Single
        Get
            Return 折线悬停十字线粗细
        End Get
        Set(value As Single)
            SetValue(折线悬停十字线粗细, Math.Max(0.0F, value))
        End Set
    End Property

    Private 折线悬停吸附点半径 As Single = 4.5F
    <Category("LakeUI"), Description("折线悬停吸附点半径。"), DefaultValue(4.5F), Browsable(True)>
    Public Property LineHoverPointRadius As Single
        Get
            Return 折线悬停吸附点半径
        End Get
        Set(value As Single)
            SetValue(折线悬停吸附点半径, Math.Max(0.0F, value))
        End Set
    End Property

    Private 折线悬停吸附点边框颜色 As Color = Color.FromArgb(240, 255, 255, 255)
    <Category("LakeUI"), Description("折线悬停吸附点边框颜色。"), DefaultValue(GetType(Color), "240, 255, 255, 255"), Browsable(True)>
    Public Property LineHoverPointBorderColor As Color
        Get
            Return 折线悬停吸附点边框颜色
        End Get
        Set(value As Color)
            SetValue(折线悬停吸附点边框颜色, value)
        End Set
    End Property

    Private 折线悬停吸附点边框粗细 As Single = 1.5F
    <Category("LakeUI"), Description("折线悬停吸附点边框粗细。"), DefaultValue(1.5F), Browsable(True)>
    Public Property LineHoverPointBorderThickness As Single
        Get
            Return 折线悬停吸附点边框粗细
        End Get
        Set(value As Single)
            SetValue(折线悬停吸附点边框粗细, Math.Max(0.0F, value))
        End Set
    End Property

    Private 折线悬停提示背景颜色 As Color = Color.FromArgb(230, 24, 24, 24)
    <Category("LakeUI"), Description("折线悬停提示背景颜色。"), DefaultValue(GetType(Color), "230, 24, 24, 24"), Browsable(True)>
    Public Property LineHoverTooltipBackColor As Color
        Get
            Return 折线悬停提示背景颜色
        End Get
        Set(value As Color)
            SetValue(折线悬停提示背景颜色, value)
        End Set
    End Property

    Private 折线悬停提示边框颜色 As Color = Color.FromArgb(90, 255, 255, 255)
    <Category("LakeUI"), Description("折线悬停提示边框颜色。"), DefaultValue(GetType(Color), "90, 255, 255, 255"), Browsable(True)>
    Public Property LineHoverTooltipBorderColor As Color
        Get
            Return 折线悬停提示边框颜色
        End Get
        Set(value As Color)
            SetValue(折线悬停提示边框颜色, value)
        End Set
    End Property

    Private 折线悬停提示边框粗细 As Single = 1.0F
    <Category("LakeUI"), Description("折线悬停提示边框粗细。"), DefaultValue(1.0F), Browsable(True)>
    Public Property LineHoverTooltipBorderThickness As Single
        Get
            Return 折线悬停提示边框粗细
        End Get
        Set(value As Single)
            SetValue(折线悬停提示边框粗细, Math.Max(0.0F, value))
        End Set
    End Property

    Private 折线悬停提示文字颜色 As Color = Color.White
    <Category("LakeUI"), Description("折线悬停提示文字颜色。"), DefaultValue(GetType(Color), "White"), Browsable(True)>
    Public Property LineHoverTooltipForeColor As Color
        Get
            Return 折线悬停提示文字颜色
        End Get
        Set(value As Color)
            SetValue(折线悬停提示文字颜色, value)
        End Set
    End Property

    Private _lineHoverTooltipFont As Font = Nothing
    <Category("LakeUI"), Description("折线悬停提示字体；为空时使用控件 Font。"), Browsable(True)>
    Public Property LineHoverTooltipFont As Font
        Get
            Return _lineHoverTooltipFont
        End Get
        Set(value As Font)
            If Object.ReferenceEquals(_lineHoverTooltipFont, value) Then Return
            _lineHoverTooltipFont = value
            NotifyStyleChanged()
        End Set
    End Property

    Private Function ShouldSerializeLineHoverTooltipFont() As Boolean
        Return _lineHoverTooltipFont IsNot Nothing
    End Function

    Private Sub ResetLineHoverTooltipFont()
        LineHoverTooltipFont = Nothing
    End Sub

    Private 折线悬停提示内边距 As Single = 7.0F
    <Category("LakeUI"), Description("折线悬停提示内部边距。"), DefaultValue(7.0F), Browsable(True)>
    Public Property LineHoverTooltipPadding As Single
        Get
            Return 折线悬停提示内边距
        End Get
        Set(value As Single)
            SetValue(折线悬停提示内边距, Math.Max(0.0F, value))
        End Set
    End Property

    Private 折线悬停提示间距 As Single = 10.0F
    <Category("LakeUI"), Description("折线悬停提示与目标点之间的间距。"), DefaultValue(10.0F), Browsable(True)>
    Public Property LineHoverTooltipSpacing As Single
        Get
            Return 折线悬停提示间距
        End Get
        Set(value As Single)
            SetValue(折线悬停提示间距, Math.Max(0.0F, value))
        End Set
    End Property

    Private 折线悬停值格式 As String = ""
    <Category("LakeUI"), Description("折线悬停值格式；为空时使用系列或控件的值标签格式。"), DefaultValue(""), Browsable(True)>
    Public Property LineHoverValueFormat As String
        Get
            Return 折线悬停值格式
        End Get
        Set(value As String)
            SetValue(折线悬停值格式, If(value, ""))
        End Set
    End Property

    Private 折线悬停提示格式 As String = ""
    <Category("LakeUI"), Description("折线悬停提示格式。可用 {0}=系列名，{1}=分类标签，{2}=原始值，{3}=格式化值。"), DefaultValue(""), Browsable(True)>
    Public Property LineHoverTooltipFormat As String
        Get
            Return 折线悬停提示格式
        End Get
        Set(value As String)
            SetValue(折线悬停提示格式, If(value, ""))
        End Set
    End Property
#End Region

#Region "字体辅助"
    Private Function 获取标题字体() As Font
        Return If(_titleFont, Me.Font)
    End Function

    Private Function 获取坐标轴标题字体() As Font
        Return If(_axisTitleFont, Me.Font)
    End Function

    Private Function 获取坐标轴标签字体() As Font
        Return If(_axisLabelFont, Me.Font)
    End Function

    Private Function 获取图例字体() As Font
        Return If(_legendFont, Me.Font)
    End Function

    Private Function 获取值标签字体() As Font
        Return If(_valueLabelFont, Me.Font)
    End Function

    Private Function 获取悬停提示字体() As Font
        Return If(_lineHoverTooltipFont, Me.Font)
    End Function
#End Region

#Region "生命周期"
    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        If 允许X轴缩放 Then
            Dim layout = 获取布局()
            If layout IsNot Nothing AndAlso
               layout.TotalCategoryCount > 0 AndAlso
               layout.CategoryCount > 0 AndAlso
               layout.PlotRect.Contains(e.X, e.Y) Then

                Dim oldCount = layout.CategoryCount
                Dim zoomRatio As Double = If(e.Delta > 0, 1.0 / X轴滚轮缩放比例, X轴滚轮缩放比例)
                Dim newCount As Integer = CInt(Math.Round(oldCount * zoomRatio))
                Dim minCount As Integer = Math.Min(layout.TotalCategoryCount, Math.Max(1, X轴最少可见分类数))
                newCount = Math.Max(minCount, Math.Min(layout.TotalCategoryCount, newCount))

                If newCount <> oldCount Then
                    Dim anchorRatio As Double = Math.Clamp((e.X - layout.PlotRect.Left) / Math.Max(1.0F, layout.PlotRect.Width), 0.0, 1.0)
                    Dim anchorIndex As Double = layout.ViewStartIndex + anchorRatio * oldCount
                    Dim newStart As Integer = CInt(Math.Round(anchorIndex - anchorRatio * newCount))
                    设置X轴视图(newStart, If(newCount >= layout.TotalCategoryCount, 0, newCount))
                    Dim handled = TryCast(e, HandledMouseEventArgs)
                    If handled IsNot Nothing Then handled.Handled = True
                    Return
                End If
            End If
        End If

        MyBase.OnMouseWheel(e)
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button <> MouseButtons.Left Then Return
        Focus()

        If Not 允许X轴拖动 Then Return
        Dim layout = 获取布局()
        If layout Is Nothing OrElse
           layout.TotalCategoryCount <= layout.CategoryCount OrElse
           Not layout.PlotRect.Contains(e.X, e.Y) Then Return

        _isPanningXAxis = True
        _panStartMouseX = e.X
        _panStartViewStart = layout.ViewStartIndex
        清除折线悬停点()
        Capture = True
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If _isPanningXAxis Then
            Dim layout = 获取布局()
            If layout Is Nothing OrElse layout.CategoryCount <= 0 Then Return

            Dim categoryWidth = layout.PlotRect.Width / layout.CategoryCount
            If categoryWidth <= 0 Then Return

            Dim deltaCategories As Integer = CInt(Math.Round((_panStartMouseX - e.X) / categoryWidth))
            设置X轴视图(_panStartViewStart + deltaCategories, layout.CategoryCount)
            Return
        End If

        更新折线悬停点(e.Location)
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If e.Button = MouseButtons.Left AndAlso _isPanningXAxis Then 结束X轴拖动()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If _isPanningXAxis AndAlso Not Capture Then 结束X轴拖动()
        清除折线悬停点()
    End Sub

    Private Sub 结束X轴拖动()
        _isPanningXAxis = False
        If Capture Then Capture = False
    End Sub

    Private Sub 更新折线悬停点(location As Point)
        If Not 显示折线悬停十字线 Then Return

        Dim layout = 获取布局()
        If layout Is Nothing OrElse Not layout.PlotRect.Contains(location) Then
            清除折线悬停点()
            Return
        End If

        If _hasHoverPoint AndAlso _hoverClientPoint = location Then Return
        _hasHoverPoint = True
        _hoverClientPoint = location
        请求V3渲染()
    End Sub

    Private Sub 清除折线悬停点()
        If Not _hasHoverPoint Then Return
        _hasHoverPoint = False
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        NotifyStyleChanged()
    End Sub

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        NotifyStyleChanged()
    End Sub

    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        NotifyStyleChanged()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        NotifyStyleChanged()
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        请求V3渲染()
    End Sub

    Protected Overrides Sub OnForeColorChanged(e As EventArgs)
        MyBase.OnForeColorChanged(e)
        NotifyStyleChanged()
    End Sub

    Protected Overrides Sub OnBackColorChanged(e As EventArgs)
        MyBase.OnBackColorChanged(e)
        请求V3渲染()
    End Sub
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
