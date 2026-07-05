Imports System.ComponentModel
Imports System.ComponentModel.Design.Serialization
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.Numerics
Imports Vortice.Direct2D1

' 几何图形与圆角矩形的共用绘制工具。
'
' 本文件同时服务两条路径：
' • GDI+ 路径：创建 GraphicsPath，供 Region、裁剪、分层窗口阴影或旧控件使用。
' • D2D 路径：创建几何、渐变与圆角矩形绘制逻辑，供兼容绘制作用域在 D3D_PaintScope.GraphicsLayer 上复用。
'
' 调用原则：
' • 创建出来的 GDI+ Path / D2D Geometry 归调用方所有，必须 Using/Dispose。
' • 文字不要画在本工具产生的 SSAA 图形层里；文字仍应走 D3D_TextInterop 和 D3D_PaintScope.TextLayer。
' • 半径会被夹到矩形短边一半以内，避免路径自交；调用方不需要提前裁剪。
'
' 坑点：
' • RoundCorners 是设计器友好的值类型。不要把它改成类，否则属性面板序列化和 DefaultValue 语义都会变化。
' • D2D 的 stroke 居中绘制；如果需要边框完全落在矩形内部，调用方应先把绘制区域按线宽内缩。

''' <summary>指定哪些角启用圆角，可在设计器属性面板中像 Padding 一样展开编辑。</summary>
<TypeConverter(GetType(RoundCornersConverter))>
Public Structure RoundCorners
    Implements IEquatable(Of RoundCorners)

    ''' <summary>全部四角启用圆角。</summary>
    Public Shared ReadOnly All As New RoundCorners(True, True, True, True)
    ''' <summary>全部四角禁用圆角。</summary>
    Public Shared ReadOnly None As New RoundCorners(False, False, False, False)

    Public Sub New(topLeft As Boolean, topRight As Boolean, bottomRight As Boolean, bottomLeft As Boolean)
        Me.TopLeft = topLeft
        Me.TopRight = topRight
        Me.BottomRight = bottomRight
        Me.BottomLeft = bottomLeft
    End Sub

    Public Sub New(all As Boolean)
        Me.New(all, all, all, all)
    End Sub

    <Description("左上角是否启用圆角"), DefaultValue(True), NotifyParentProperty(True)>
    Public Property TopLeft As Boolean

    <Description("右上角是否启用圆角"), DefaultValue(True), NotifyParentProperty(True)>
    Public Property TopRight As Boolean

    <Description("右下角是否启用圆角"), DefaultValue(True), NotifyParentProperty(True)>
    Public Property BottomRight As Boolean

    <Description("左下角是否启用圆角"), DefaultValue(True), NotifyParentProperty(True)>
    Public Property BottomLeft As Boolean

    ''' <summary>是否全部角都启用了圆角。</summary>
    <Browsable(False)>
    Public ReadOnly Property IsAll As Boolean
        Get
            Return TopLeft AndAlso TopRight AndAlso BottomRight AndAlso BottomLeft
        End Get
    End Property

    ''' <summary>是否全部角都未启用圆角。</summary>
    <Browsable(False)>
    Public ReadOnly Property IsNone As Boolean
        Get
            Return Not TopLeft AndAlso Not TopRight AndAlso Not BottomRight AndAlso Not BottomLeft
        End Get
    End Property

    Public Overrides Function GetHashCode() As Integer
        Return (If(TopLeft, 1, 0)) Or
               (If(TopRight, 2, 0)) Or
               (If(BottomRight, 4, 0)) Or
               (If(BottomLeft, 8, 0))
    End Function

    Public Overrides Function Equals(obj As Object) As Boolean
        If TypeOf obj Is RoundCorners Then Return Equals(DirectCast(obj, RoundCorners))
        Return False
    End Function

    Public Overloads Function Equals(other As RoundCorners) As Boolean Implements IEquatable(Of RoundCorners).Equals
        Return TopLeft = other.TopLeft AndAlso TopRight = other.TopRight AndAlso
               BottomRight = other.BottomRight AndAlso BottomLeft = other.BottomLeft
    End Function

    Public Shared Operator =(a As RoundCorners, b As RoundCorners) As Boolean
        Return a.Equals(b)
    End Operator

    Public Shared Operator <>(a As RoundCorners, b As RoundCorners) As Boolean
        Return Not a.Equals(b)
    End Operator

    Public Overrides Function ToString() As String
        If IsAll Then Return "All"
        If IsNone Then Return "None"
        Dim parts As New List(Of String)
        If TopLeft Then parts.Add("TopLeft")
        If TopRight Then parts.Add("TopRight")
        If BottomRight Then parts.Add("BottomRight")
        If BottomLeft Then parts.Add("BottomLeft")
        Return String.Join(", ", parts)
    End Function
End Structure

''' <summary>为 <see cref="RoundCorners"/> 提供 Padding 风格的属性面板展开/折叠交互。</summary>
Friend Class RoundCornersConverter
    Inherits ExpandableObjectConverter

    Public Overrides Function CanConvertFrom(context As ITypeDescriptorContext, sourceType As Type) As Boolean
        If sourceType Is GetType(String) Then Return True
        Return MyBase.CanConvertFrom(context, sourceType)
    End Function

    Public Overrides Function ConvertFrom(context As ITypeDescriptorContext, culture As CultureInfo, value As Object) As Object
        If TypeOf value Is String Then
            Dim text = DirectCast(value, String).Trim()
            If text.Equals("All", StringComparison.OrdinalIgnoreCase) Then Return RoundCorners.All
            If text.Equals("None", StringComparison.OrdinalIgnoreCase) Then Return RoundCorners.None
            Dim names = text.Split(","c).Select(Function(s) s.Trim().ToLowerInvariant()).ToArray()
            Return New RoundCorners(
                names.Contains("topleft"),
                names.Contains("topright"),
                names.Contains("bottomright"),
                names.Contains("bottomleft"))
        End If
        Return MyBase.ConvertFrom(context, culture, value)
    End Function

    Public Overrides Function ConvertTo(context As ITypeDescriptorContext, culture As CultureInfo, value As Object, destinationType As Type) As Object
        If destinationType Is GetType(String) AndAlso TypeOf value Is RoundCorners Then
            Return DirectCast(value, RoundCorners).ToString()
        End If
        If destinationType Is GetType(InstanceDescriptor) AndAlso TypeOf value Is RoundCorners Then
            Dim rc = DirectCast(value, RoundCorners)
            Dim ctor = GetType(RoundCorners).GetConstructor({GetType(Boolean), GetType(Boolean), GetType(Boolean), GetType(Boolean)})
            Return New InstanceDescriptor(ctor, New Object() {rc.TopLeft, rc.TopRight, rc.BottomRight, rc.BottomLeft})
        End If
        Return MyBase.ConvertTo(context, culture, value, destinationType)
    End Function

    Public Overrides Function CanConvertTo(context As ITypeDescriptorContext, destinationType As Type) As Boolean
        If destinationType Is GetType(InstanceDescriptor) Then Return True
        Return MyBase.CanConvertTo(context, destinationType)
    End Function

    Public Overrides Function CreateInstance(context As ITypeDescriptorContext, propertyValues As IDictionary) As Object
        Return New RoundCorners(
            CBool(propertyValues("TopLeft")),
            CBool(propertyValues("TopRight")),
            CBool(propertyValues("BottomRight")),
            CBool(propertyValues("BottomLeft")))
    End Function

    Public Overrides Function GetCreateInstanceSupported(context As ITypeDescriptorContext) As Boolean
        Return True
    End Function

    Friend Shared ReadOnly names As String() = New String() {"TopLeft", "TopRight", "BottomRight", "BottomLeft"}

    Public Overrides Function GetProperties(context As ITypeDescriptorContext, value As Object, attributes() As Attribute) As PropertyDescriptorCollection
        Dim props = TypeDescriptor.GetProperties(GetType(RoundCorners), attributes)
        Return props.Sort(names)
    End Function

    Public Overrides Function GetPropertiesSupported(context As ITypeDescriptorContext) As Boolean
        Return True
    End Function
End Class

''' <summary>
''' 供控件共享的矩形、圆角、边框、渐变和 D2D 几何绘制工具。
''' </summary>
''' <remarks>
''' 此类不保存状态；所有 Shared 方法要么返回调用方负责释放的对象，要么直接画到调用方传入的
''' Graphics / RenderTarget。
''' </remarks>
Public Class D3D_RectangleRenderer

    Public Shared Function 创建圆角矩形路径(区域 As RectangleF, 半径 As Single) As GraphicsPath
        Dim path As New GraphicsPath()
        If 半径 <= 0 OrElse 区域.Width < 1 OrElse 区域.Height < 1 Then
            path.AddRectangle(区域)
            Return path
        End If
        ' 将半径限制在短边的一半以内，防止弧线重叠导致路径自交叉
        半径 = Math.Min(半径, Math.Min(区域.Width / 2.0F, 区域.Height / 2.0F))
        Dim 直径 As Single = 半径 * 2.0F
        Dim arc As New RectangleF(区域.Location, New SizeF(直径, 直径))
        path.AddArc(arc, 180, 90) ' 左上角
        arc.X = 区域.Right - 直径
        path.AddArc(arc, 270, 90) ' 右上角
        arc.Y = 区域.Bottom - 直径
        path.AddArc(arc, 0, 90) ' 右下角
        arc.X = 区域.Left
        path.AddArc(arc, 90, 90) ' 左下角
        path.CloseFigure()
        Return path
    End Function

    ''' <summary>创建可按角选择圆角的矩形路径。未启用圆角的角以直角绘制。</summary>
    Public Shared Function 创建圆角矩形路径(区域 As RectangleF, 半径 As Single, 圆角位置 As RoundCorners) As GraphicsPath
        Dim path As New GraphicsPath()
        If 半径 <= 0 OrElse 区域.Width < 1 OrElse 区域.Height < 1 OrElse 圆角位置.IsNone Then
            path.AddRectangle(区域)
            Return path
        End If
        If 圆角位置.IsAll Then
            Return 创建圆角矩形路径(区域, 半径)
        End If
        半径 = Math.Min(半径, Math.Min(区域.Width / 2.0F, 区域.Height / 2.0F))
        Dim 直径 As Single = 半径 * 2.0F

        ' 左上角
        If 圆角位置.TopLeft Then
            path.AddArc(New RectangleF(区域.X, 区域.Y, 直径, 直径), 180, 90)
        Else
            path.AddLine(区域.X, 区域.Y + 半径, 区域.X, 区域.Y)
            path.AddLine(区域.X, 区域.Y, 区域.X + 半径, 区域.Y)
        End If

        ' 右上角
        If 圆角位置.TopRight Then
            path.AddArc(New RectangleF(区域.Right - 直径, 区域.Y, 直径, 直径), 270, 90)
        Else
            path.AddLine(区域.Right - 半径, 区域.Y, 区域.Right, 区域.Y)
            path.AddLine(区域.Right, 区域.Y, 区域.Right, 区域.Y + 半径)
        End If

        ' 右下角
        If 圆角位置.BottomRight Then
            path.AddArc(New RectangleF(区域.Right - 直径, 区域.Bottom - 直径, 直径, 直径), 0, 90)
        Else
            path.AddLine(区域.Right, 区域.Bottom - 半径, 区域.Right, 区域.Bottom)
            path.AddLine(区域.Right, 区域.Bottom, 区域.Right - 半径, 区域.Bottom)
        End If

        ' 左下角
        If 圆角位置.BottomLeft Then
            path.AddArc(New RectangleF(区域.X, 区域.Bottom - 直径, 直径, 直径), 90, 90)
        Else
            path.AddLine(区域.X + 半径, 区域.Bottom, 区域.X, 区域.Bottom)
            path.AddLine(区域.X, 区域.Bottom, 区域.X, 区域.Bottom - 半径)
        End If

        path.CloseFigure()
        Return path
    End Function

    Public Shared Sub 绘制圆角背景(g As Graphics, 路径 As GraphicsPath, 极限矩形区域 As RectangleF, 背景颜色 As Color, 渐变颜色 As Color, 渐变方向 As System.Windows.Forms.Orientation)
        If 渐变颜色 <> Color.Empty Then
            Dim angle As Single = If(渐变方向 = System.Windows.Forms.Orientation.Vertical, 90.0F, 0.0F)
            Using brush As New LinearGradientBrush(极限矩形区域, 背景颜色, 渐变颜色, angle)
                g.FillPath(brush, 路径)
            End Using
        Else
            Using brush As New SolidBrush(背景颜色)
                g.FillPath(brush, 路径)
            End Using
        End If
    End Sub

    Public Shared Sub 绘制矩形背景(g As Graphics, 区域 As RectangleF, 背景颜色 As Color, 渐变颜色 As Color, 渐变方向 As System.Windows.Forms.Orientation)
        If 渐变颜色 <> Color.Empty Then
            Dim angle As Single = If(渐变方向 = System.Windows.Forms.Orientation.Vertical, 90.0F, 0.0F)
            Using brush As New LinearGradientBrush(区域, 背景颜色, 渐变颜色, angle)
                g.FillRectangle(brush, 区域)
            End Using
        Else
            Using brush As New SolidBrush(背景颜色)
                g.FillRectangle(brush, 区域)
            End Using
        End If
    End Sub

    Public Shared Sub 绘制圆角边框(g As Graphics, 路径 As GraphicsPath, 边框颜色 As Color, 边框宽度 As Single)
        If 边框宽度 > 0 Then
            Using pen As New Pen(边框颜色, 边框宽度)
                pen.Alignment = PenAlignment.Center
                pen.LineJoin = Drawing2D.LineJoin.Round
                g.DrawPath(pen, 路径)
            End Using
        End If
    End Sub

    Public Shared Sub 绘制矩形边框(g As Graphics, 区域 As RectangleF, 边框颜色 As Color, 边框宽度 As Single)
        If 边框宽度 > 0 Then
            Using pen As New Pen(边框颜色, 边框宽度)
                pen.Alignment = PenAlignment.Center
                g.DrawRectangle(pen, 区域.X, 区域.Y, 区域.Width, 区域.Height)
            End Using
        End If
    End Sub

    ''' <summary>创建椭圆路径（当控件为正方形时即为正圆）。</summary>
    Public Shared Function 创建椭圆路径(区域 As RectangleF) As GraphicsPath
        Dim path As New GraphicsPath()
        path.AddEllipse(区域)
        Return path
    End Function

    ''' <summary>绘制椭圆边框（当控件为正方形时即为正圆边框）。</summary>
    Public Shared Sub 绘制椭圆边框(g As Graphics, 区域 As RectangleF, 边框颜色 As Color, 边框宽度 As Single)
        If 边框宽度 > 0 Then
            Using pen As New Pen(边框颜色, 边框宽度)
                pen.Alignment = PenAlignment.Center
                g.DrawEllipse(pen, 区域)
            End Using
        End If
    End Sub

#Region "D2D 渲染（Vortice）"

    ''' <summary>创建一个统一圆角的 D2D 几何（调用方负责 Dispose）。</summary>
    Public Shared Function 创建圆角矩形几何(区域 As RectangleF, 半径 As Single) As ID2D1Geometry
        If 半径 <= 0 OrElse 区域.Width < 1 OrElse 区域.Height < 1 Then
            Return D3D_D2DInterop.GetD2DFactory().CreateRectangleGeometry(区域)
        End If
        半径 = Math.Min(半径, Math.Min(区域.Width / 2.0F, 区域.Height / 2.0F))
        Return D3D_D2DInterop.GetD2DFactory().CreateRoundedRectangleGeometry(
            New RoundedRectangle(区域, 半径, 半径))
    End Function

    ''' <summary>创建可按角选择的 D2D 圆角矩形几何（PathGeometry，调用方负责 Dispose）。</summary>
    Public Shared Function 创建圆角矩形几何(区域 As RectangleF, 半径 As Single, 圆角位置 As RoundCorners) As ID2D1Geometry
        If 半径 <= 0 OrElse 区域.Width < 1 OrElse 区域.Height < 1 OrElse 圆角位置.IsNone Then
            Return D3D_D2DInterop.GetD2DFactory().CreateRectangleGeometry(区域)
        End If
        If 圆角位置.IsAll Then
            Return 创建圆角矩形几何(区域, 半径)
        End If
        半径 = Math.Min(半径, Math.Min(区域.Width / 2.0F, 区域.Height / 2.0F))
        Dim path As ID2D1PathGeometry = D3D_D2DInterop.GetD2DFactory().CreatePathGeometry()
        Dim sink As ID2D1GeometrySink = path.Open()
        Try
            Dim left As Single = 区域.X, top As Single = 区域.Y
            Dim right As Single = 区域.Right, bottom As Single = 区域.Bottom

            Dim startPt As New Vector2(left + If(圆角位置.TopLeft, 半径, 0F), top)
            sink.BeginFigure(startPt, FigureBegin.Filled)

            ' 顶边 → 右上角
            sink.AddLine(New Vector2(right - If(圆角位置.TopRight, 半径, 0F), top))
            If 圆角位置.TopRight Then
                sink.AddArc(New ArcSegment With {
                    .Point = New Vector2(right, top + 半径),
                    .Size = New Vortice.Mathematics.Size(半径, 半径),
                    .RotationAngle = 0,
                    .SweepDirection = SweepDirection.Clockwise,
                    .ArcSize = ArcSize.Small})
            End If

            ' 右边 → 右下角
            sink.AddLine(New Vector2(right, bottom - If(圆角位置.BottomRight, 半径, 0F)))
            If 圆角位置.BottomRight Then
                sink.AddArc(New ArcSegment With {
                    .Point = New Vector2(right - 半径, bottom),
                    .Size = New Vortice.Mathematics.Size(半径, 半径),
                    .RotationAngle = 0,
                    .SweepDirection = SweepDirection.Clockwise,
                    .ArcSize = ArcSize.Small})
            End If

            ' 底边 → 左下角
            sink.AddLine(New Vector2(left + If(圆角位置.BottomLeft, 半径, 0F), bottom))
            If 圆角位置.BottomLeft Then
                sink.AddArc(New ArcSegment With {
                    .Point = New Vector2(left, bottom - 半径),
                    .Size = New Vortice.Mathematics.Size(半径, 半径),
                    .RotationAngle = 0,
                    .SweepDirection = SweepDirection.Clockwise,
                    .ArcSize = ArcSize.Small})
            End If

            ' 左边 → 左上角
            sink.AddLine(New Vector2(left, top + If(圆角位置.TopLeft, 半径, 0F)))
            If 圆角位置.TopLeft Then
                sink.AddArc(New ArcSegment With {
                    .Point = New Vector2(left + 半径, top),
                    .Size = New Vortice.Mathematics.Size(半径, 半径),
                    .RotationAngle = 0,
                    .SweepDirection = SweepDirection.Clockwise,
                    .ArcSize = ArcSize.Small})
            End If

            sink.EndFigure(FigureEnd.Closed)
            sink.Close()
        Finally
            sink.Dispose()
        End Try
        Return path
    End Function

    ''' <summary>D2D 圆角背景填充（支持纯色 / 双色线性渐变）。</summary>
    ''' <param name="brushCache">可选的 SolidColorBrush 缓存；仅纯色路径复用。Nothing 时退回原逻辑。</param>
    Public Shared Sub 绘制圆角背景_D2D(rt As ID2D1RenderTarget, 几何 As ID2D1Geometry, 极限矩形区域 As RectangleF,
                                   背景颜色 As Color, 渐变颜色 As Color, 渐变方向 As System.Windows.Forms.Orientation,
                                   Optional brushCache As D3D_D2DInterop.SolidColorBrushCache = Nothing)
        If 几何 Is Nothing Then Return
        If 背景颜色.A = 0 AndAlso (渐变颜色 = Color.Empty OrElse 渐变颜色.A = 0) Then Return
        If brushCache IsNot Nothing AndAlso (渐变颜色 = Color.Empty OrElse 渐变颜色.A = 0) Then
            rt.FillGeometry(几何, brushCache.Get(rt, 背景颜色))
            Return
        End If
        Using brush = 创建背景画刷(rt, 极限矩形区域, 背景颜色, 渐变颜色, 渐变方向)
            rt.FillGeometry(几何, brush)
        End Using
    End Sub

    ''' <summary>D2D 统一圆角背景便捷重载；VB 下 FillRoundedRectangle 重载不明确，内部仍走 Geometry 路径。</summary>
    Public Shared Sub 绘制圆角背景_D2D(rt As ID2D1RenderTarget, 区域 As RectangleF, 半径 As Single,
                                   背景颜色 As Color, 渐变颜色 As Color, 渐变方向 As System.Windows.Forms.Orientation,
                                   Optional brushCache As D3D_D2DInterop.SolidColorBrushCache = Nothing)
        If rt Is Nothing OrElse 区域.Width < 1 OrElse 区域.Height < 1 Then Return
        If 背景颜色.A = 0 AndAlso (渐变颜色 = Color.Empty OrElse 渐变颜色.A = 0) Then Return
        半径 = Math.Min(Math.Max(0.0F, 半径), Math.Min(区域.Width / 2.0F, 区域.Height / 2.0F))
        If 半径 <= 0 Then
            绘制矩形背景_D2D(rt, 区域, 背景颜色, 渐变颜色, 渐变方向, brushCache)
            Return
        End If

        Using geo = 创建圆角矩形几何(区域, 半径)
            绘制圆角背景_D2D(rt, geo, 区域, 背景颜色, 渐变颜色, 渐变方向, brushCache)
        End Using
    End Sub

    ''' <summary>D2D 直角矩形背景填充（支持纯色 / 双色线性渐变）。</summary>
    ''' <param name="brushCache">可选的 SolidColorBrush 缓存；仅纯色路径复用。Nothing 时退回原逻辑。</param>
    Public Shared Sub 绘制矩形背景_D2D(rt As ID2D1RenderTarget, 区域 As RectangleF,
                                  背景颜色 As Color, 渐变颜色 As Color, 渐变方向 As System.Windows.Forms.Orientation,
                                  Optional brushCache As D3D_D2DInterop.SolidColorBrushCache = Nothing)
        If rt Is Nothing OrElse 区域.Width < 1 OrElse 区域.Height < 1 Then Return
        If 背景颜色.A = 0 AndAlso (渐变颜色 = Color.Empty OrElse 渐变颜色.A = 0) Then Return
        If brushCache IsNot Nothing AndAlso (渐变颜色 = Color.Empty OrElse 渐变颜色.A = 0) Then
            rt.FillRectangle(D3D_D2DInterop.ToD2DRect(区域), brushCache.Get(rt, 背景颜色))
            Return
        End If
        Using brush = 创建背景画刷(rt, 区域, 背景颜色, 渐变颜色, 渐变方向)
            rt.FillRectangle(D3D_D2DInterop.ToD2DRect(区域), brush)
        End Using
    End Sub

    ''' <summary>D2D 圆角边框描边。</summary>
    ''' <param name="brushCache">可选的 SolidColorBrush 缓存；Nothing 时退回原逻辑。</param>
    Public Shared Sub 绘制圆角边框_D2D(rt As ID2D1RenderTarget, 几何 As ID2D1Geometry, 边框颜色 As Color, 边框宽度 As Single,
                                   Optional brushCache As D3D_D2DInterop.SolidColorBrushCache = Nothing)
        If 几何 Is Nothing OrElse 边框宽度 <= 0 OrElse 边框颜色.A = 0 Then Return
        If brushCache IsNot Nothing Then
            rt.DrawGeometry(几何, brushCache.Get(rt, 边框颜色), 边框宽度)
            Return
        End If
        Using b = rt.CreateSolidColorBrush(D3D_D2DInterop.ToColor4(边框颜色))
            rt.DrawGeometry(几何, b, 边框宽度)
        End Using
    End Sub

    ''' <summary>D2D 统一圆角边框直绘，不创建临时 Geometry。</summary>
    Public Shared Sub 绘制圆角边框_D2D(rt As ID2D1RenderTarget, 区域 As RectangleF, 半径 As Single,
                                   边框颜色 As Color, 边框宽度 As Single,
                                   Optional brushCache As D3D_D2DInterop.SolidColorBrushCache = Nothing)
        If rt Is Nothing OrElse 区域.Width < 1 OrElse 区域.Height < 1 OrElse 边框宽度 <= 0 OrElse 边框颜色.A = 0 Then Return
        半径 = Math.Min(Math.Max(0.0F, 半径), Math.Min(区域.Width / 2.0F, 区域.Height / 2.0F))
        If 半径 <= 0 Then
            绘制矩形边框_D2D(rt, 区域, 边框颜色, 边框宽度, brushCache)
            Return
        End If

        Dim rounded As New RoundedRectangle(区域, 半径, 半径)
        If brushCache IsNot Nothing Then
            rt.DrawRoundedRectangle(rounded, DirectCast(brushCache.Get(rt, 边框颜色), ID2D1Brush), 边框宽度)
            Return
        End If
        Using b = rt.CreateSolidColorBrush(D3D_D2DInterop.ToColor4(边框颜色))
            rt.DrawRoundedRectangle(rounded, DirectCast(b, ID2D1Brush), 边框宽度)
        End Using
    End Sub

    ''' <summary>D2D 直角矩形边框描边。</summary>
    ''' <param name="brushCache">可选的 SolidColorBrush 缓存；Nothing 时退回原逻辑。</param>
    Public Shared Sub 绘制矩形边框_D2D(rt As ID2D1RenderTarget, 区域 As RectangleF, 边框颜色 As Color, 边框宽度 As Single,
                                  Optional brushCache As D3D_D2DInterop.SolidColorBrushCache = Nothing)
        If 边框宽度 <= 0 OrElse 边框颜色.A = 0 Then Return
        If brushCache IsNot Nothing Then
            rt.DrawRectangle(D3D_D2DInterop.ToD2DRect(区域), brushCache.Get(rt, 边框颜色), 边框宽度)
            Return
        End If
        Using b = rt.CreateSolidColorBrush(D3D_D2DInterop.ToColor4(边框颜色))
            rt.DrawRectangle(D3D_D2DInterop.ToD2DRect(区域), b, 边框宽度)
        End Using
    End Sub

    Private Shared Function 创建背景画刷(rt As ID2D1RenderTarget, 区域 As RectangleF,
                                   背景颜色 As Color, 渐变颜色 As Color, 渐变方向 As System.Windows.Forms.Orientation) As ID2D1Brush
        If 渐变颜色 = Color.Empty OrElse 渐变颜色.A = 0 Then
            Return rt.CreateSolidColorBrush(D3D_D2DInterop.ToColor4(背景颜色))
        End If
        Dim startPt As Vector2, endPt As Vector2
        If 渐变方向 = System.Windows.Forms.Orientation.Vertical Then
            startPt = New Vector2(区域.X, 区域.Y)
            endPt = New Vector2(区域.X, 区域.Bottom)
        Else
            startPt = New Vector2(区域.X, 区域.Y)
            endPt = New Vector2(区域.Right, 区域.Y)
        End If
        Dim stops = {
            New GradientStop(0.0F, D3D_D2DInterop.ToColor4(背景颜色)),
            New GradientStop(1.0F, D3D_D2DInterop.ToColor4(渐变颜色))}
        Dim gsc = rt.CreateGradientStopCollection(stops)
        Try
            Return rt.CreateLinearGradientBrush(New LinearGradientBrushProperties(startPt, endPt), gsc)
        Finally
            gsc.Dispose()
        End Try
    End Function

    ''' <summary>D2D 填充椭圆（区域 = 外切矩形）。</summary>
    Public Shared Sub 填充椭圆_D2D(rt As ID2D1RenderTarget, 区域 As RectangleF, 颜色 As Color,
                                   Optional brushCache As D3D_D2DInterop.SolidColorBrushCache = Nothing)
        If 区域.Width < 1 OrElse 区域.Height < 1 OrElse 颜色.A = 0 Then Return
        Dim cx As Single = 区域.X + 区域.Width / 2.0F
        Dim cy As Single = 区域.Y + 区域.Height / 2.0F
        Dim e As New Ellipse(New Vector2(cx, cy), 区域.Width / 2.0F, 区域.Height / 2.0F)
        If brushCache IsNot Nothing Then
            rt.FillEllipse(e, brushCache.[Get](rt, 颜色))
            Return
        End If
        Using b = rt.CreateSolidColorBrush(D3D_D2DInterop.ToColor4(颜色))
            rt.FillEllipse(e, b)
        End Using
    End Sub

    ''' <summary>D2D 描边椭圆。</summary>
    Public Shared Sub 描边椭圆_D2D(rt As ID2D1RenderTarget, 区域 As RectangleF, 颜色 As Color, 宽度 As Single,
                                   Optional brushCache As D3D_D2DInterop.SolidColorBrushCache = Nothing)
        If 区域.Width < 1 OrElse 区域.Height < 1 OrElse 颜色.A = 0 OrElse 宽度 <= 0 Then Return
        Dim cx As Single = 区域.X + 区域.Width / 2.0F
        Dim cy As Single = 区域.Y + 区域.Height / 2.0F
        Dim e As New Ellipse(New Vector2(cx, cy), 区域.Width / 2.0F, 区域.Height / 2.0F)
        If brushCache IsNot Nothing Then
            rt.DrawEllipse(e, brushCache.[Get](rt, 颜色), 宽度)
            Return
        End If
        Using b = rt.CreateSolidColorBrush(D3D_D2DInterop.ToColor4(颜色))
            rt.DrawEllipse(e, b, 宽度)
        End Using
    End Sub

    ''' <summary>D2D 画一条线段。</summary>
    Public Shared Sub 画线_D2D(rt As ID2D1RenderTarget, x1 As Single, y1 As Single, x2 As Single, y2 As Single,
                                颜色 As Color, 宽度 As Single,
                                Optional brushCache As D3D_D2DInterop.SolidColorBrushCache = Nothing)
        If 颜色.A = 0 OrElse 宽度 <= 0 Then Return
        Dim p1 As New Vector2(x1, y1)
        Dim p2 As New Vector2(x2, y2)
        If brushCache IsNot Nothing Then
            rt.DrawLine(p1, p2, brushCache.[Get](rt, 颜色), 宽度)
            Return
        End If
        Using b = rt.CreateSolidColorBrush(D3D_D2DInterop.ToColor4(颜色))
            rt.DrawLine(p1, p2, b, 宽度)
        End Using
    End Sub

    ''' <summary>D2D 多段折线（用 PathGeometry 描边，支持 Round Cap/Join）。</summary>
    Public Shared Sub 画折线_D2D(rt As ID2D1RenderTarget, points As IList(Of PointF), 颜色 As Color, 宽度 As Single,
                                  Optional brushCache As D3D_D2DInterop.SolidColorBrushCache = Nothing)
        If points Is Nothing OrElse points.Count < 2 OrElse 颜色.A = 0 OrElse 宽度 <= 0 Then Return
        Dim factory = D3D_D2DInterop.GetD2DFactory()
        Dim path As ID2D1PathGeometry = factory.CreatePathGeometry()
        Try
            Using sink = path.Open()
                sink.BeginFigure(New Vector2(points(0).X, points(0).Y), FigureBegin.Hollow)
                For i As Integer = 1 To points.Count - 1
                    sink.AddLine(New Vector2(points(i).X, points(i).Y))
                Next
                sink.EndFigure(FigureEnd.Open)
                sink.Close()
            End Using
            Dim ss = D3D_D2DInterop.GetRoundStrokeStyle(roundDashCap:=True)
            If brushCache IsNot Nothing Then
                rt.DrawGeometry(path, brushCache.[Get](rt, 颜色), 宽度, ss)
            Else
                Using b = rt.CreateSolidColorBrush(D3D_D2DInterop.ToColor4(颜色))
                    rt.DrawGeometry(path, b, 宽度, ss)
                End Using
            End If
        Finally
            path.Dispose()
        End Try
    End Sub

    ''' <summary>D2D 圆角矩形便捷重载：内部按 RoundCorners 选择全/部分圆角并填充。</summary>
    Public Shared Sub 绘制圆角矩形_D2D(rt As ID2D1RenderTarget, 区域 As RectangleF, 半径 As Single, 圆角 As RoundCorners, 颜色 As Color,
                                        Optional brushCache As D3D_D2DInterop.SolidColorBrushCache = Nothing)
        If 颜色.A = 0 OrElse 区域.Width < 1 OrElse 区域.Height < 1 Then Return
        If 圆角.IsAll Then
            绘制圆角背景_D2D(rt, 区域, 半径, 颜色, Color.Empty, System.Windows.Forms.Orientation.Horizontal, brushCache)
            Return
        End If
        Using geo = 创建圆角矩形几何(区域, 半径, 圆角)
            If brushCache IsNot Nothing Then
                rt.FillGeometry(geo, brushCache.[Get](rt, 颜色))
            Else
                Using b = rt.CreateSolidColorBrush(D3D_D2DInterop.ToColor4(颜色))
                    rt.FillGeometry(geo, b)
                End Using
            End If
        End Using
    End Sub

#End Region

End Class
