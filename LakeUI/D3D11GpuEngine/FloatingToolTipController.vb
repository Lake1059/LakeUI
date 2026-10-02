Imports System.ComponentModel

Friend NotInheritable Class FloatingToolTipTarget
    Public ReadOnly Text As String
    Public ReadOnly Bounds As Rectangle

    Public Sub New(text As String, bounds As Rectangle)
        Me.Text = If(text, "")
        Me.Bounds = bounds
    End Sub
End Class

Friend NotInheritable Class FloatingToolTipController
    Implements IDisposable

    Private ReadOnly _owner As Control
    Private ReadOnly _targetProvider As Func(Of Point, FloatingToolTipTarget)
    Private ReadOnly _styleProvider As Func(Of FloatingToolTipStyle)
    Private ReadOnly _showTimer As New Timer()
    Private ReadOnly _autoPopTimer As New Timer()
    Private _tipForm As FloatingToolTipForm
    Private _ownerForm As Form
    Private _target As FloatingToolTipTarget
    Private _pointer As Point
    Private _initialDelay As Integer = 500
    Private _autoPopDelay As Integer
    Private _autoPopExpired As Boolean
    Private _disposed As Boolean

    Public Sub New(owner As Control, targetProvider As Func(Of Point, FloatingToolTipTarget), styleProvider As Func(Of FloatingToolTipStyle))
        _owner = owner
        _targetProvider = targetProvider
        _styleProvider = styleProvider
        AddHandler _showTimer.Tick, AddressOf ShowTimerTick
        AddHandler _autoPopTimer.Tick, AddressOf AutoPopTimerTick
        AddHandler owner.HandleDestroyed, AddressOf OwnerStateChanged
        AddHandler owner.VisibleChanged, AddressOf OwnerStateChanged
        AddHandler owner.EnabledChanged, AddressOf OwnerStateChanged
        AddHandler owner.LocationChanged, AddressOf OwnerStateChanged
        AddHandler owner.SizeChanged, AddressOf OwnerStateChanged
        AddHandler owner.ParentChanged, AddressOf OwnerParentChanged
        AddHandler owner.FontChanged, AddressOf OwnerStyleChanged
        AddHandler owner.DpiChangedAfterParent, AddressOf OwnerStateChanged
        AddHandler owner.Disposed, AddressOf OwnerDisposed
        BindOwnerForm()
    End Sub

    Public Property InitialDelay As Integer
        Get
            Return _initialDelay
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If _initialDelay = value Then Return
            _initialDelay = value
            If _showTimer.Enabled Then
                Hide()
                Update(_owner.PointToClient(Cursor.Position))
            End If
        End Set
    End Property

    Public Property AutoPopDelay As Integer
        Get
            Return _autoPopDelay
        End Get
        Set(value As Integer)
            _autoPopDelay = Math.Max(0, value)
            RestartAutoPopTimer()
        End Set
    End Property

    Public Sub Update(pointer As Point)
        If Not CanShow() Then
            Hide()
            Return
        End If
        BindOwnerForm()
        Dim target = _targetProvider(pointer)
        If target Is Nothing OrElse String.IsNullOrEmpty(target.Text) OrElse Not target.Bounds.Contains(pointer) Then
            Leave()
            Return
        End If
        _pointer = pointer
        If SameTarget(target, _target) Then
            If TipVisible() Then _tipForm.CancelScheduledClose()
            If _autoPopExpired OrElse TipVisible() OrElse _showTimer.Enabled Then Return
        Else
            Hide()
            _target = target
        End If
        If _initialDelay = 0 Then
            ShowTarget()
        Else
            _showTimer.Interval = _initialDelay
            _showTimer.Start()
        End If
    End Sub

    Public Sub Leave()
        If _disposed Then Return
        _showTimer.Stop()
        _autoPopTimer.Stop()
        Dim relatedBounds = If(_target Is Nothing, Rectangle.Empty, _owner.RectangleToScreen(_target.Bounds))
        _target = Nothing
        _autoPopExpired = False
        If TipVisible() Then _tipForm.ScheduleCloseIfPointerOutside(180, relatedBounds)
    End Sub

    Public Sub Hide()
        If _disposed Then Return
        _showTimer.Stop()
        _autoPopTimer.Stop()
        _target = Nothing
        _autoPopExpired = False
        CloseTip()
    End Sub

    Public Sub RefreshStyle()
        If Not _disposed AndAlso TipVisible() AndAlso _target IsNot Nothing Then
            _tipForm.ShowTip(_target.Text, TipLocation(), _styleProvider())
        End If
    End Sub

    Private Function CanShow() As Boolean
        Return Not _disposed AndAlso Not _owner.IsDisposed AndAlso Not _owner.Disposing AndAlso
            _owner.IsHandleCreated AndAlso _owner.Visible AndAlso _owner.Enabled AndAlso
            Not _owner.Capture AndAlso Control.MouseButtons = MouseButtons.None AndAlso
            LicenseManager.UsageMode <> LicenseUsageMode.Designtime
    End Function

    Private Shared Function SameTarget(first As FloatingToolTipTarget, second As FloatingToolTipTarget) As Boolean
        Return first IsNot Nothing AndAlso second IsNot Nothing AndAlso first.Bounds = second.Bounds AndAlso
            String.Equals(first.Text, second.Text, StringComparison.Ordinal)
    End Function

    Private Function TipVisible() As Boolean
        Return _tipForm IsNot Nothing AndAlso Not _tipForm.IsDisposed AndAlso _tipForm.Visible
    End Function

    Private Function TipLocation() As Point
        Dim scale = D3D_DpiContext.FromControl(_owner).Scale
        Return _owner.PointToScreen(New Point(_pointer.X + CInt(12 * scale), _pointer.Y + CInt(20 * scale)))
    End Function

    Private Sub ShowTimerTick(sender As Object, e As EventArgs)
        _showTimer.Stop()
        ShowTarget()
    End Sub

    Private Sub ShowTarget()
        If Not CanShow() OrElse _target Is Nothing Then
            Hide()
            Return
        End If
        Dim pointer = _owner.PointToClient(Cursor.Position)
        If Not _owner.ClientRectangle.Contains(pointer) OrElse Not SameTarget(_targetProvider(pointer), _target) Then
            Hide()
            Return
        End If
        _pointer = pointer
        If _tipForm Is Nothing OrElse _tipForm.IsDisposed Then _tipForm = New FloatingToolTipForm(_owner)
        _tipForm.ShowTip(_target.Text, TipLocation(), _styleProvider())
        RestartAutoPopTimer()
    End Sub

    Private Sub RestartAutoPopTimer()
        If _disposed Then Return
        _autoPopTimer.Stop()
        If _autoPopDelay > 0 AndAlso TipVisible() Then
            _autoPopTimer.Interval = _autoPopDelay
            _autoPopTimer.Start()
        End If
    End Sub

    Private Sub AutoPopTimerTick(sender As Object, e As EventArgs)
        _autoPopTimer.Stop()
        If Not TipVisible() OrElse _tipForm.HasSelectedText OrElse _tipForm.ContainsScreenPoint(Cursor.Position) Then Return
        CloseTip()
        _autoPopExpired = True
    End Sub

    Private Sub CloseTip()
        If _tipForm IsNot Nothing AndAlso Not _tipForm.IsDisposed Then
            _tipForm.Close()
            _tipForm.Dispose()
        End If
        _tipForm = Nothing
    End Sub

    Private Sub BindOwnerForm()
        Dim ownerForm = _owner.FindForm()
        If ReferenceEquals(_ownerForm, ownerForm) Then Return
        UnbindOwnerForm()
        _ownerForm = ownerForm
        If _ownerForm Is Nothing Then Return
        AddHandler _ownerForm.Deactivate, AddressOf OwnerStateChanged
        AddHandler _ownerForm.VisibleChanged, AddressOf OwnerStateChanged
        AddHandler _ownerForm.EnabledChanged, AddressOf OwnerStateChanged
        AddHandler _ownerForm.LocationChanged, AddressOf OwnerStateChanged
        AddHandler _ownerForm.HandleDestroyed, AddressOf OwnerStateChanged
    End Sub

    Private Sub UnbindOwnerForm()
        If _ownerForm Is Nothing Then Return
        RemoveHandler _ownerForm.Deactivate, AddressOf OwnerStateChanged
        RemoveHandler _ownerForm.VisibleChanged, AddressOf OwnerStateChanged
        RemoveHandler _ownerForm.EnabledChanged, AddressOf OwnerStateChanged
        RemoveHandler _ownerForm.LocationChanged, AddressOf OwnerStateChanged
        RemoveHandler _ownerForm.HandleDestroyed, AddressOf OwnerStateChanged
        _ownerForm = Nothing
    End Sub

    Private Sub OwnerParentChanged(sender As Object, e As EventArgs)
        Hide()
        BindOwnerForm()
    End Sub

    Private Sub OwnerStateChanged(sender As Object, e As EventArgs)
        Hide()
    End Sub

    Private Sub OwnerStyleChanged(sender As Object, e As EventArgs)
        RefreshStyle()
    End Sub

    Private Sub OwnerDisposed(sender As Object, e As EventArgs)
        Dispose()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        Hide()
        _disposed = True
        UnbindOwnerForm()
        RemoveHandler _owner.HandleDestroyed, AddressOf OwnerStateChanged
        RemoveHandler _owner.VisibleChanged, AddressOf OwnerStateChanged
        RemoveHandler _owner.EnabledChanged, AddressOf OwnerStateChanged
        RemoveHandler _owner.LocationChanged, AddressOf OwnerStateChanged
        RemoveHandler _owner.SizeChanged, AddressOf OwnerStateChanged
        RemoveHandler _owner.ParentChanged, AddressOf OwnerParentChanged
        RemoveHandler _owner.FontChanged, AddressOf OwnerStyleChanged
        RemoveHandler _owner.DpiChangedAfterParent, AddressOf OwnerStateChanged
        RemoveHandler _owner.Disposed, AddressOf OwnerDisposed
        RemoveHandler _showTimer.Tick, AddressOf ShowTimerTick
        RemoveHandler _autoPopTimer.Tick, AddressOf AutoPopTimerTick
        _showTimer.Dispose()
        _autoPopTimer.Dispose()
    End Sub
End Class
