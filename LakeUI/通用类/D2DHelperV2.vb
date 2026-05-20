''' <summary>
''' D2D 渲染管线 V2 入口（路线 E：窗口级共享 + 控件级临时 SSAA）。
'''
''' === 设计目标 ===
''' • 控件数 × SSAA 倍率不再线性堆显存：DC RT、文字 / 笔刷 / 位图缓存、SSAA BitmapRT 统统按
'''   "顶层 Form 一份" 集中到 <see cref="WindowCompositor"/>。
''' • 透明背景穿透改为显式 source（<see cref="BackgroundPenetrationV2"/>），杜绝隐式递归采样。
''' • 控件类自身不再持有 D2D 资源字段（_dcRT / _ssaaCache / _backImageCache 等），迁移时全部删除。
'''
''' === 设计理念 ===
''' • 共享 DC RT + SSAA 池化 → GPU 资源生命周期与 Form 对齐，PaintScope 只绑定一次 HDC。
''' • D2DHelper（D2DGlobals.vb）仅保留无 Form 上下文的全局工厂/缓存/质量策略。
'''
''' === 控件接入清单（迁移到 V2 必做） ===
''' 1. 删除自身字段：_dcRT / _ssaaCache / _brushCache / _textFormatCache / _backImageCache。
''' 2. 删除 OnHandleDestroyed 内对上述字段的 Dispose（compositor 自管）。
''' 3. <c>OnPaintBackground</c> 留空（V2 自己负责画底）。
''' 4. <c>OnPaint</c> 改用下方 BeginPaint 模板：
'''      Protected Overrides Sub OnPaint(e As PaintEventArgs)
'''          Using scope = D2DHelperV2.BeginPaint(e, Me, ssaa:=2)
'''              If scope Is Nothing Then  ' 设计期 / Parent=Nothing：可选回退到 V1 或直接退出
'''                  MyBase.OnPaint(e) : Return
'''              End If
'''              ' (a) 背景层：BackColor 与背景穿透二选一（见下方"BackColor 协议"）
'''              If _backgroundSource IsNot Nothing Then
'''                  BackgroundPenetrationV2.PaintBackground(Me, scope, _backgroundSource)
'''              ElseIf BackColor.A > 0 Then
'''                  scope.BackgroundLayer.Clear(D2DHelper.ToD2DColor(BackColor))
'''              End If
'''              ' (b) 图形层（SSAA RT，回采时按 SsaaScale 缩小）
'''              绘制图形_D2D(scope.GraphicsLayer, scope.Compositor)
'''              scope.FlushGraphics()
'''              ' (c) 文字层（DC RT 子像素抗锯齿，必须在这一层画文字）
'''              绘制文字_D2D(scope.TextLayer, scope.Compositor)
'''          End Using
'''      End Sub
''' 5. 笔刷 / TextFormat 取自 <c>scope.Compositor.BrushCache</c> / <c>TextFormatCache</c>，
'''    切勿在控件内再建 cache 实例。
'''
''' === BackColor 协议（V2 强约束） ===
''' • 指定 BackgroundSource → 跳过 BackColor，直接画穿透层。
''' • 未指定 BackgroundSource：BackColor.A=255 直接 Clear；A 介于 (0,255) 用半透明覆盖；A=0 不画。
''' • 控件不要再覆盖 SetStyle(SupportsTransparentBackColor)：V2 的透明语义由 BackgroundSource 决定。
'''
''' === 生命周期 ===
''' • compositor 由 Form 拥有，<c>Form.HandleDestroyed</c> 触发 <see cref="WindowCompositor.Dispose"/>
'''   并自动从注册表注销。
''' • 控件除常规清理外不需要释放任何 V2 对象；持有 PaintScopeV2 必须在同一 OnPaint 内 Using 释放。
'''
''' === 线程要求 ===
''' • 所有方法必须在 UI 线程调用（D2D / GDI HDC 强制要求）。
''' • compositor 注册表本身有锁，但 RT / cache 的访问不加锁。
''' </summary>
Public Module D2DHelperV2

    <ThreadStatic>
    Private _backgroundSamplingPaintDepth As Integer

    Friend Function EnterBackgroundSamplingPaint() As IDisposable
        _backgroundSamplingPaintDepth += 1
        Return New BackgroundSamplingPaintScope()
    End Function

    Private NotInheritable Class BackgroundSamplingPaintScope
        Implements IDisposable

        Private _disposed As Boolean

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then Return
            _disposed = True
            If _backgroundSamplingPaintDepth > 0 Then _backgroundSamplingPaintDepth -= 1
        End Sub
    End Class

#Region "WindowCompositor 注册表"

    Private ReadOnly _compositors As New Dictionary(Of Form, WindowCompositor)
    Private ReadOnly _compositorsLock As New Object()

    ''' <summary>
    ''' 取得控件所属顶层 Form 的 <see cref="WindowCompositor"/>，按需创建。
    ''' </summary>
    ''' <param name="ctrl">任一隶属于目标 Form 的控件，可以是被绘控件本人。</param>
    ''' <returns>
    ''' 设计期、控件已 Dispose、控件尚未挂载到 Form、Form 已 Dispose 时返回 <c>Nothing</c>。
    ''' 调用方应在 <c>Nothing</c> 情形下回退到 V1 路径或直接跳过 V2 自绘。
    ''' </returns>
    ''' <remarks>
    ''' 注册表对 Form 引用是强引用，但 compositor 自身订阅 <c>Form.HandleDestroyed</c> 触发自销毁
    ''' 并调用 <see cref="UnregisterCompositor"/>，因此 Form 释放后注册表不会泄漏。
    ''' </remarks>
    Public Function GetCompositor(ctrl As Control) As WindowCompositor
        If ctrl Is Nothing Then Return Nothing
        If ctrl.IsDisposed Then Return Nothing
        Dim form As Form = ctrl.FindForm()
        If form Is Nothing Then Return Nothing
        If form.IsDisposed Then Return Nothing

        SyncLock _compositorsLock
            Dim comp As WindowCompositor = Nothing
            If _compositors.TryGetValue(form, comp) Then
                If comp.IsDisposed Then
                    _compositors.Remove(form)
                Else
                    Return comp
                End If
            End If
            comp = New WindowCompositor(form)
            _compositors(form) = comp
            Return comp
        End SyncLock
    End Function

    ''' <summary>compositor 自销毁时回调，从注册表移除。</summary>
    Friend Sub UnregisterCompositor(form As Form)
        If form Is Nothing Then Return
        SyncLock _compositorsLock
            _compositors.Remove(form)
        End SyncLock
    End Sub

#End Region

#Region "BeginPaint 入口"

    ''' <summary>
    ''' 启动一次 V2 绘制作用域。返回的 <see cref="PaintScopeV2"/> 必须在同一 <c>OnPaint</c> 内 Using 释放，
    ''' 跨方法保存会导致 HDC 泄漏与下一帧 BindDC 失败。
    ''' </summary>
    ''' <param name="e">控件 OnPaint 收到的事件参数。</param>
    ''' <param name="control">被绘控件，用来定位 compositor 与传入 Width / Height。</param>
    ''' <param name="ssaaScale">
    ''' 图形层 SSAA 倍率：&lt;=1 表示禁用（图形层即 DC RT）；建议常用 2 或 3，&gt;4 已无收益。
    ''' SSAA 仅作用于 <see cref="PaintScopeV2.GraphicsLayer"/>，背景层与文字层始终为 1×。
    ''' </param>
    ''' <returns>
    ''' 设计期、无 Form、compositor 创建失败、或同一 Form 内正在进行 V2 绘制重入时返回 <c>Nothing</c>；
    ''' 调用方应在 <c>Nothing</c> 时回退到 V1、base.OnPaint，或跳过本次 V2 自绘。
    ''' </returns>
    Public Function BeginPaint(e As PaintEventArgs, control As Control, ssaaScale As Integer) As PaintScopeV2
        If e Is Nothing OrElse control Is Nothing Then Return Nothing
        Dim comp = GetCompositor(control)
        If comp Is Nothing Then Return Nothing
        Dim scope = comp.BeginPaint(e, control, ssaaScale)
        If scope IsNot Nothing Then Return scope
        If _backgroundSamplingPaintDepth <= 0 Then Return Nothing

        Dim form As Form = control.FindForm()
        If form Is Nothing OrElse form.IsDisposed Then Return Nothing
        Dim tempComp As New WindowCompositor(form, unregisterOnDispose:=False)
        Return tempComp.BeginPaint(e, control, ssaaScale, disposeCompositorWithScope:=True)
    End Function

#End Region

End Module
