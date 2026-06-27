Imports System.Numerics
Imports Vortice.Direct2D1

''' <summary>
''' V2 绘制作用域。生命周期 = 一次 <c>OnPaint</c> 调用，必须 <c>Using</c> 释放。
'''
''' === 三层绘制结构 ===
''' • <see cref="BackgroundLayer"/> = <see cref="DCRenderTarget"/>（1× 直接绘制）。
'''   用于 BackColor 填底 / 背景穿透 / 背景图。不做 SSAA。
''' • <see cref="GraphicsLayer"/>：
'''     - SSAA &gt; 1 时为临时离屏 <see cref="ID2D1BitmapRenderTarget"/>（从 compositor 池借用）；
'''     - SSAA = 1 时等同 DC RT。
'''   控件的几何 / 边框 / 遮罩 / 自绘图标都在这一层。<see cref="FlushGraphics"/> 后会切回 DC RT。
''' • <see cref="TextLayer"/> = <see cref="DCRenderTarget"/>。
'''   所有 DirectWrite 文字绘制必须在这一层，才能利用 GDI HDC 子像素抗锯齿（ClearType）。
'''
''' === 调用顺序约定 ===
'''   1) 在 BackgroundLayer 上画底（可选）。
'''   2) 在 GraphicsLayer 上画图形（必要时 Transform / Clip 都在这一层）。
'''   3) <see cref="FlushGraphics"/> 把 SSAA 内容回采到 DC RT；之后 GraphicsLayer 自动指向 DC RT。
'''   4) 在 TextLayer 上画文字。
''' Step 3 是 SSAA 模式下的必要步骤；SSAA 关闭时调用为 no-op，也可省略。
'''
''' === 与 V1 PaintScope 的差异 ===
''' • 不在控件上跨帧持有 BitmapRT：用完归还 compositor 池，没有 N 控件 × 1 张 SSAA 的显存堆积。
''' • 不持有 DC RT：从 <see cref="WindowCompositor"/> 借用共享 DC RT，BindDC 一次即生效。
''' • Dispose 内自动 FlushGraphics 兜底，控件忘记调用 Flush 也不会泄漏 SSAA RT。
''' • Dispose 结束时通知 compositor 退出活动绘制状态，避免共享 DC RT 被嵌套 BindDC。
'''
''' === 线程要求 ===
''' UI 线程（OnPaint 上下文）专用。
''' </summary>
Public NotInheritable Class PaintScopeV2
    Implements IDisposable

    Private ReadOnly _compositor As WindowCompositor
    Private ReadOnly _g As Graphics
    Private ReadOnly _hdc As IntPtr
    Private ReadOnly _w As Integer
    Private ReadOnly _h As Integer
    Private ReadOnly _ssaa As Integer
    Private ReadOnly _clipRect As Rectangle
    Private ReadOnly _ssaaLogicalW As Integer
    Private ReadOnly _ssaaLogicalH As Integer
    Private ReadOnly _ssaaPixelW As Integer
    Private ReadOnly _ssaaPixelH As Integer
    Private _rentedPixelW As Integer
    Private _rentedPixelH As Integer
    Private _bitmapRT As ID2D1BitmapRenderTarget
    Private _graphicsLayer As ID2D1RenderTarget
    Private _disposed As Boolean
    Private ReadOnly _disposeCompositorWithScope As Boolean
    Private ReadOnly _returnCompositorToBackgroundSamplingPool As Boolean
    Private _dcClipPushed As Boolean

    ''' <summary>所属窗口 compositor，可用来访问共享 brush / textformat / bitmap 缓存。</summary>
    Public ReadOnly Property Compositor As WindowCompositor
        Get
            Return _compositor
        End Get
    End Property

    ''' <summary>背景层 / 文字层共用的 DC 渲染目标（1× 直绘）。</summary>
    Public ReadOnly Property DCRenderTarget As ID2D1DCRenderTarget

    ''' <summary>背景层 = DC RT。</summary>
    Public ReadOnly Property BackgroundLayer As ID2D1RenderTarget
        Get
            Return DCRenderTarget
        End Get
    End Property

    ''' <summary>图形层：SSAA 启用时为高分辨率 BitmapRT，否则为 DC RT。FlushGraphics 之后访问会回到 DC RT。</summary>
    Public ReadOnly Property GraphicsLayer As ID2D1RenderTarget
        Get
            Return _graphicsLayer
        End Get
    End Property

    ''' <summary>文字层 = DC RT。</summary>
    Public ReadOnly Property TextLayer As ID2D1RenderTarget
        Get
            Return DCRenderTarget
        End Get
    End Property

    ''' <summary>
    ''' 本窗口共享的 <see cref="ID2D1DeviceContext"/>（D2D 1.1 入口，阶段 A 基础设施）。
    ''' <para>
    ''' 仅用于愿意走 D2D 1.1 effect / 跨 Form bitmap 缓存等高级路径的代码；常规控件继续使用
    ''' <see cref="BackgroundLayer"/> / <see cref="GraphicsLayer"/> / <see cref="TextLayer"/> 即可。
    ''' LakeUI 不提供 GPU 不支持时的显示回退路线；除 compositor 已释放外，创建失败会直接向调用方抛出。
    ''' </para>
    ''' <para>
    ''' <b>其他控件未来接入指南（可选 / 完全自愿）</b>：
    ''' <list type="number">
    '''   <item>在 <c>OnPaint</c> 中先按常规走 <see cref="D2DHelperV2.BeginPaint"/> 拿到 <see cref="PaintScopeV2"/>。</item>
    '''   <item>访问本属性拿 <see cref="ID2D1DeviceContext"/>；调用方只需要处理运行时设备丢失。</item>
    '''   <item>把要画的内容渲染到自己创建的 <c>ID2D1Bitmap1</c>（带 <c>BitmapOptions.Target Or GdiCompatible</c>）：
    '''         <c>ctx.Target = bmp1 → ctx.BeginDraw → ... → ctx.EndDraw</c>。
    '''         注意保存并恢复 <c>ctx.Target</c>，避免污染其他控件后续使用。</item>
    '''   <item>通过 <c>ctx.QueryInterface(Of ID2D1GdiInteropRenderTarget)</c> 拿 HDC，
    '''         用 <c>BitBlt</c> 把结果合成到当前 <c>PaintEventArgs.Graphics</c>（参考 <c>ThisIsYourWindow</c> 的探测实现）；
    '''         <b>或</b>在阶段 B 完成后改为直接画到 DeviceContext 自带的目标位图（届时 DC RT 与 DeviceContext 资源互通）。</item>
    '''   <item>把所有 D2D 1.1 调用包在 Try 内；Catch 后调用
    '''         <see cref="WindowCompositor.NotifyDeviceContextException"/>，<c>True</c> 返回值表示是设备丢失，
    '''         应吞掉本帧异常并 <c>Invalidate</c> 请求下一帧重画。</item>
    '''   <item>不要长期缓存 DeviceContext 引用；它会随 <see cref="D3D11Globals.DeviceLost"/> 自动重建，
    '''         每帧通过 <see cref="PaintScopeV2.DeviceContext"/> 重新取即可。</item>
    ''' </list>
    ''' </para>
    ''' <para>
    ''' 阶段 A 限制：DeviceContext 与 DC RT 不共享资源，请勿把同一 <see cref="ID2D1Bitmap"/> /
    ''' 笔刷在两者之间通用。
    ''' </para>
    ''' </summary>
    Public ReadOnly Property DeviceContext As ID2D1DeviceContext
        Get
            Return _compositor?.DeviceContext
        End Get
    End Property

    ''' <summary>SSAA 倍率（1 表示禁用）。</summary>
    Public ReadOnly Property SsaaScale As Integer
        Get
            Return _ssaa
        End Get
    End Property

    ''' <summary>控件宽度（逻辑像素）。</summary>
    Public ReadOnly Property Width As Integer
        Get
            Return _w
        End Get
    End Property

    ''' <summary>控件高度（逻辑像素）。</summary>
    Public ReadOnly Property Height As Integer
        Get
            Return _h
        End Get
    End Property

    ''' <summary>本次 PaintEventArgs 提供的有效失效区域（控件逻辑像素）。</summary>
    Public ReadOnly Property ClipRectangle As Rectangle
        Get
            Return _clipRect
        End Get
    End Property

    Friend Sub New(compositor As WindowCompositor, g As Graphics, hdc As IntPtr,
                   dcRT As ID2D1DCRenderTarget, w As Integer, h As Integer, ssaa As Integer,
                   clipRect As Rectangle,
                   Optional disposeCompositorWithScope As Boolean = False,
                   Optional returnCompositorToBackgroundSamplingPool As Boolean = False)
        _compositor = compositor
        _g = g
        _hdc = hdc
        _w = w
        _h = h
        _ssaa = Math.Max(1, ssaa)
        _clipRect = NormalizeClipRect(clipRect, w, h)
        _ssaaLogicalW = Math.Max(1, _clipRect.Width)
        _ssaaLogicalH = Math.Max(1, _clipRect.Height)
        _ssaaPixelW = Math.Max(1, _ssaaLogicalW * _ssaa)
        _ssaaPixelH = Math.Max(1, _ssaaLogicalH * _ssaa)
        _disposeCompositorWithScope = disposeCompositorWithScope
        _returnCompositorToBackgroundSamplingPool = returnCompositorToBackgroundSamplingPool
        DCRenderTarget = dcRT

        dcRT.BindDC(hdc, New Vortice.RawRect(0, 0, w, h))
        dcRT.BeginDraw()
        ' 一旦 BeginDraw 成功，后续任何异常都必须在向外抛出前先 EndDraw，否则共享 DC RT
        ' 会卡在"已 BeginDraw 未 EndDraw"状态，下一次 BindDC 会 D2DERR_WRONG_STATE，
        ' 整个 Form 的 V2 绘制就此失效。
        Try
            D2DGlobals.ApplyGlobalQuality(dcRT)
            dcRT.Transform = Matrix3x2.Identity
            dcRT.PushAxisAlignedClip(ToRawRectF(_clipRect), AntialiasMode.Aliased)
            _dcClipPushed = True
            ' 共享 brush 缓存：RT 引用即将作为绘图目标，确保和当前 RT 绑定一致。
            ' （SolidColorBrushCache 内部按 RT 自动失效，这里无需手动操作。）

            If _ssaa > 1 Then
                _bitmapRT = _compositor.RentSsaaRT(dcRT, _ssaaPixelW, _ssaaPixelH, _rentedPixelW, _rentedPixelH)
                If _bitmapRT Is Nothing Then Throw New InvalidOperationException("SSAA render target allocation failed.")
                Try
                    D2DGlobals.ApplyGlobalQuality(_bitmapRT)
                    _bitmapRT.BeginDraw()
                    _bitmapRT.Clear(New Vortice.Mathematics.Color4(0F, 0F, 0F, 0F))
                    _bitmapRT.PushAxisAlignedClip(
                        New Vortice.RawRectF(0.0F, 0.0F, _ssaaPixelW, _ssaaPixelH),
                        AntialiasMode.Aliased)
                    _bitmapRT.Transform =
                        Matrix3x2.CreateTranslation(-_clipRect.X, -_clipRect.Y) *
                        Matrix3x2.CreateScale(_ssaa)
                Catch
                    ' SSAA RT 初始化失败：直接丢弃这块 RT（不归还池，避免污染），
                    ' 然后把异常交给外层把 DC RT 也 EndDraw。
                    Try : _bitmapRT.Dispose() : Catch : End Try
                    _bitmapRT = Nothing
                    Throw
                End Try
                _graphicsLayer = _bitmapRT
            Else
                _graphicsLayer = dcRT
            End If
        Catch ex As Exception
            Try
                If _dcClipPushed Then
                    dcRT.PopAxisAlignedClip()
                    _dcClipPushed = False
                End If
                dcRT.EndDraw()
            Catch endEx As Exception
                _compositor.NotifyDCRenderTargetException(endEx)
            End Try
            Try : _compositor.NotifyDCRenderTargetException(ex) : Catch : End Try
            Throw
        End Try
    End Sub

    Private Shared Function NormalizeClipRect(rect As Rectangle, w As Integer, h As Integer) As Rectangle
        Dim bounds As New Rectangle(0, 0, Math.Max(0, w), Math.Max(0, h))
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return New Rectangle(0, 0, 1, 1)
        If rect.Width <= 0 OrElse rect.Height <= 0 Then Return bounds
        Dim clipped = Rectangle.Intersect(bounds, rect)
        If clipped.Width <= 0 OrElse clipped.Height <= 0 Then Return bounds
        Return clipped
    End Function

    Private Shared Function ToRawRectF(rect As Rectangle) As Vortice.RawRectF
        Return New Vortice.RawRectF(rect.Left, rect.Top, rect.Right, rect.Bottom)
    End Function

    ''' <summary>
    ''' 把 SSAA BitmapRT 的图形结果回采到 DC RT，并把 BitmapRT 归还 compositor 池（不 Dispose）。
    ''' </summary>
    ''' <remarks>
    ''' • SSAA = 1 时为 no-op。
    ''' • 回采使用 <see cref="BitmapInterpolationMode.Linear"/> 缩放；source = SSAA 像素尺寸，dest = 逻辑像素尺寸。
    ''' • 调用后 <see cref="GraphicsLayer"/> 自动指向 DC RT；如果还要继续画几何也是可以的，
    '''   只是不再享受 SSAA。
    ''' • <see cref="Dispose"/> 内会兜底再调用一次本方法，重复调用安全。
    ''' </remarks>
    Public Sub FlushGraphics()
        If _bitmapRT Is Nothing Then Return
        Dim healthy As Boolean = True
        Try
            _bitmapRT.Transform = Matrix3x2.Identity
            _bitmapRT.PopAxisAlignedClip()
            _bitmapRT.EndDraw()
            Dim bmp = _bitmapRT.Bitmap
            DCRenderTarget.DrawBitmap(
                bmp,
                New Vortice.Mathematics.Rect(_clipRect.Left, _clipRect.Top, _clipRect.Width, _clipRect.Height),
                1.0F,
                BitmapInterpolationMode.Linear,
                New Vortice.Mathematics.Rect(0, 0, _ssaaPixelW, _ssaaPixelH))
        Catch ex As Exception
            ' EndDraw / DrawBitmap 失败（如设备丢失）：这块 RT 状态已不可信，
            ' 不归还池避免污染下一帧；吞掉异常让 Dispose 路径继续清理 DC RT。
            healthy = False
            Try : _compositor.NotifyDCRenderTargetException(ex) : Catch : End Try
        Finally
            If healthy Then
                ' V2 改进：SSAA RT 归还 compositor 池复用，避免每帧 GPU 资源分配/释放的卡顿。
                _compositor.ReturnSsaaRT(_bitmapRT, _rentedPixelW, _rentedPixelH)
            Else
                Try : _bitmapRT.Dispose() : Catch : End Try
            End If
            _bitmapRT = Nothing
            _graphicsLayer = DCRenderTarget
        End Try
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        Try
            Try
                FlushGraphics()
            Catch
            End Try
            Try
                If _dcClipPushed Then
                    DCRenderTarget.PopAxisAlignedClip()
                    _dcClipPushed = False
                End If
                DCRenderTarget.EndDraw()
            Catch ex As Exception
                Try : _compositor.NotifyDCRenderTargetException(ex) : Catch : End Try
            End Try
            Try
                _g.ReleaseHdc(_hdc)
            Catch
            End Try
        Finally
            _compositor.EndPaintScope()
            If _returnCompositorToBackgroundSamplingPool Then
                Try : D2DHelperV2.ReturnBackgroundSamplingCompositor(_compositor) : Catch : End Try
            ElseIf _disposeCompositorWithScope Then
                Try : _compositor.Dispose() : Catch : End Try
            End If
        End Try
    End Sub
End Class
