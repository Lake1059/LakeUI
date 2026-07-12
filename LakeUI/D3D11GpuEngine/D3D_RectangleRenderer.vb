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
' • V3 路径只保留 D2D geometry 构造，实际绘制由 D3D_PaintContext 完成。
'
' 调用原则：
' • 创建出来的 GDI+ Path / D2D Geometry 归调用方所有，必须 Using/Dispose。
' • 文字统一走 D3D_PaintContext / D3D_TextRenderer。
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
#End Region

End Class
