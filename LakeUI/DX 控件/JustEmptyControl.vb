Public Class JustEmptyControl
    Implements D3D_IGpuRenderable, D3D_IGpuInvalidationSource, D3D_IGpuDirtyRegionCoverage, V5_IGpuPresentationSource
    Public Sub New()
        InitializeComponent()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.SupportsTransparentBackColor, True)
        UpdateStyles()
    End Sub

#Region "绘制"
    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' V5 surface owns the pixels; transparent controls sample the nearest GPU ancestor.
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements D3D_IGpuRenderable.RenderGpu
        If BackColor.A > 0 Then context.FillRectangle(New RectangleF(0, 0, Width, Height), BackColor)
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements D3D_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, ClientSize)
    End Function

    Public Function CoversDirtyRegion(dirtyRegion As Rectangle) As Boolean Implements D3D_IGpuDirtyRegionCoverage.CoversDirtyRegion
        Return dirtyRegion.Width > 0 AndAlso dirtyRegion.Height > 0
    End Function
#End Region
End Class
