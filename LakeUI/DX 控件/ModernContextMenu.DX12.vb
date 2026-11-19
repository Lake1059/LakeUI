Imports System.Numerics

Partial Public Class ModernContextMenu
    Partial Friend Class MenuPopupForm
        Implements ID3D12Renderable

        Public Sub RenderGpu12(context As D3D12_PaintContext) Implements ID3D12Renderable.RenderGpu12
            If ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then Return

            Dim hasBackdrop = DrawBackdrop_DX12(context)
            DrawGraphicsContent_DX12(context, hasBackdrop)
            DrawAllText_DX12(context)
        End Sub

        Private Function DrawBackdrop_DX12(context As D3D12_PaintContext) As Boolean
            If _backdrop Is Nothing Then Return False
            Return _backdrop.Draw12(context, New RectangleF(0, 0, ClientSize.Width, ClientSize.Height))
        End Function

        Private Sub DrawGraphicsContent_DX12(context As D3D12_PaintContext, hasBackdrop As Boolean)
            Dim bounds As New RectangleF(0, 0, ClientSize.Width, ClientSize.Height)
            Dim explicitBackdrop As Boolean = 菜单.毛玻璃模式 <> BackdropModeEnum.None

            Dim radius As Single = 获取菜单圆角()
            If Not hasBackdrop Then
                If 菜单.背景底色.A > 0 Then context.FillRoundedRectangle(bounds, radius, 菜单.背景底色)
            ElseIf Not explicitBackdrop AndAlso 菜单.背景底色.A > 0 Then
                context.FillRoundedRectangle(bounds, radius, 菜单.背景底色)
            End If

            If 菜单.背景颜色.A > 0 Then context.FillRoundedRectangle(bounds, radius, 菜单.背景颜色)

            If 菜单.边框宽度 > 0 Then
                Dim bw As Single = Math.Max(1.0F, 菜单.边框宽度 * DpiScale())
                MessageDialogRendering12.DrawRoundedRectangle(context, bounds, 菜单.边框颜色, bw, radius)
            End If

            DrawHoverHighlight_DX12(context)

            For i = 0 To 菜单.项目列表.Count - 1
                If i >= 项目区域列表.Count Then Exit For
                Dim item = 菜单.项目列表(i)
                Dim rect = 项目区域列表(i)
                If item.IsSeparator Then
                    DrawSeparator_DX12(context, rect)
                ElseIf Not item.IsDescription Then
                    DrawItemGraphics_DX12(context, item, rect)
                End If
            Next
        End Sub

        Private Sub DrawSeparator_DX12(context As D3D12_PaintContext, rect As Rectangle)
            Dim lineY As Single = rect.Y + (rect.Height - 1) / 2.0F
            context.FillRectangle(New RectangleF(rect.X, lineY, rect.Width, 1.0F), 菜单.分割线颜色)
        End Sub

        Private Sub DrawHoverHighlight_DX12(context As D3D12_PaintContext)
            If Not 动画显示高亮 OrElse 项目区域列表.Count = 0 Then Return
            Dim highlightRect As New RectangleF(
                项目区域列表(0).X, 动画当前Y,
                项目区域列表(0).Width, 动画当前高度)
            Dim highlightColor As Color = If(鼠标按下, 菜单.按下背景颜色, 菜单.悬停背景颜色)
            If highlightColor.A = 0 Then Return

            If 菜单.悬停圆角半径 > 0 Then
                Dim radius As Single = Math.Min(菜单.悬停圆角半径 * DpiScale(), highlightRect.Height / 2.0F)
                context.FillRoundedRectangle(highlightRect, radius, highlightColor)
            Else
                context.FillRectangle(highlightRect, highlightColor)
            End If
        End Sub

        Private Sub DrawItemGraphics_DX12(context As D3D12_PaintContext, item As ModernMenuItem, rect As Rectangle)
            Dim s As Single = DpiScale()
            Dim ipL As Integer = CInt(菜单.项目内边距.Left * s)
            Dim iconCol As Integer = CInt(菜单.有效图标列宽度 * s)

            If iconCol > 0 Then
                Dim iconX As Integer = rect.X + ipL
                Dim iconY As Integer = rect.Y + (rect.Height - iconCol) \ 2
                Dim iconRect As New RectangleF(iconX, iconY, iconCol, iconCol)

                If item.Checked Then DrawCheckMark_DX12(context, iconRect)
                If item.Icon IsNot Nothing Then context.DrawImage(item.Icon, iconRect)
            End If

            If item.SubMenu IsNot Nothing Then
                Dim arrowW As Integer = CInt(16 * s)
                DrawArrow_DX12(context, New Rectangle(rect.Right - arrowW, rect.Y, arrowW, rect.Height))
            End If
        End Sub

        Private Sub DrawAllText_DX12(context As D3D12_PaintContext)
            Dim s As Single = DpiScale()
            Dim iconCol As Integer = CInt(菜单.有效图标列宽度 * s)
            Dim ipL As Integer = CInt(菜单.项目内边距.Left * s)
            Dim ipR As Integer = CInt(菜单.项目内边距.Right * s)
            Dim ipT As Integer = CInt(菜单.项目内边距.Top * s)
            Dim ipB As Integer = CInt(菜单.项目内边距.Bottom * s)
            Dim iconTextGap As Integer = If(iconCol > 0, CInt(菜单.图标文字间距 * s), 0)

            For i = 0 To 菜单.项目列表.Count - 1
                If i >= 项目区域列表.Count Then Exit For
                Dim item = 菜单.项目列表(i)
                If item.IsSeparator Then Continue For
                Dim rect = 项目区域列表(i)
                Dim x As Integer = rect.X + ipL + iconCol + iconTextGap
                Dim font As Font
                Dim foreColor As Color
                If item.IsDescription Then
                    font = If(item.Font, 菜单.说明字体)
                    foreColor = If(item.ForeColor <> Color.Empty, item.ForeColor, 菜单.说明文本颜色)
                Else
                    font = If(item.Font, 菜单.菜单字体)
                    foreColor = If(item.ForeColor <> Color.Empty, item.ForeColor, 菜单.文本颜色)
                End If
                If font Is Nothing OrElse foreColor.A = 0 Then Continue For

                Dim arrowSpace As Integer = If(Not item.IsDescription AndAlso item.SubMenu IsNot Nothing, CInt(20 * s), 0)
                Dim textRect As New RectangleF(x, rect.Y + ipT, rect.Width - ipL - iconCol - iconTextGap - ipR - arrowSpace, rect.Height - ipT - ipB)
                If textRect.Width <= 0 OrElse textRect.Height <= 0 Then Continue For

                context.DrawText(If(item.Text, ""), font, foreColor, textRect, s,
                                 Vortice.DirectWrite.TextAlignment.Leading,
                                 Vortice.DirectWrite.ParagraphAlignment.Center)
            Next
        End Sub

        Private Sub DrawCheckMark_DX12(context As D3D12_PaintContext, rect As RectangleF)
            Dim cx As Single = rect.X + rect.Width / 2.0F
            Dim cy As Single = rect.Y + rect.Height / 2.0F
            Dim markSize As Single = 菜单.勾选大小 * DpiScale()
            Dim s As Single = markSize / 2.0F
            Dim pw As Single = Math.Max(1.6F * DpiScale(), markSize * 0.2F)
            Dim start As New Vector2(cx - s, cy)
            Dim middle As New Vector2(cx - s * 0.35F, cy + s * 0.85F)
            Dim finish As New Vector2(cx + s, cy - s)
            context.DrawRoundLine(start, middle, 菜单.勾选颜色, pw)
            context.DrawRoundLine(middle, finish, 菜单.勾选颜色, pw)
        End Sub

        Private Sub DrawArrow_DX12(context As D3D12_PaintContext, rect As Rectangle)
            Dim cx As Single = rect.X + rect.Width / 2.0F
            Dim cy As Single = rect.Y + rect.Height / 2.0F
            Dim arrSize As Single = 菜单.箭头大小 * DpiScale()
            Dim arrH As Single = arrSize
            Dim arrW As Single = CSng(arrSize * Math.Sqrt(3.0) / 2.0)
            context.FillRoundedTriangle({New Vector2(cx - arrW / 2, cy - arrH / 2), New Vector2(cx - arrW / 2, cy + arrH / 2),
                                         New Vector2(cx + arrW / 2, cy)}, 0, 菜单.箭头颜色)
        End Sub
    End Class
End Class
