Imports System.Drawing.Drawing2D

Public Class RectangleRenderer

    Public Shared Function 创建圆角矩形路径(区域 As RectangleF, 半径 As Integer) As GraphicsPath
        Dim path As New GraphicsPath()
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

    Public Shared Sub 绘制圆角背景(g As Graphics, 路径 As GraphicsPath, 极限矩形区域 As RectangleF, 背景颜色 As Color, 渐变颜色 As Color, 渐变方向 As Orientation)
        If 渐变颜色 <> Color.Empty Then
            Dim angle As Single = If(渐变方向 = Orientation.Vertical, 90.0F, 0.0F)
            Using brush As New LinearGradientBrush(极限矩形区域, 背景颜色, 渐变颜色, angle)
                g.FillPath(brush, 路径)
            End Using
        Else
            Using brush As New SolidBrush(背景颜色)
                g.FillPath(brush, 路径)
            End Using
        End If
    End Sub

    Public Shared Sub 绘制矩形背景(g As Graphics, 区域 As RectangleF, 背景颜色 As Color, 渐变颜色 As Color, 渐变方向 As Orientation)
        If 渐变颜色 <> Color.Empty Then
            Dim angle As Single = If(渐变方向 = Orientation.Vertical, 90.0F, 0.0F)
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
                pen.LineJoin = LineJoin.Round
                g.DrawPath(pen, 路径)
            End Using
        End If
    End Sub

    Public Shared Sub 绘制矩形边框(g As Graphics, 区域 As RectangleF, 边框颜色 As Color, 边框宽度 As Single)
        If 边框宽度 > 0 Then
            Using pen As New Pen(边框颜色, 边框宽度)
                pen.Alignment = PenAlignment.Inset
                g.DrawRectangle(pen, 区域.X, 区域.Y, 区域.Width, 区域.Height)
            End Using
        End If
    End Sub

End Class
