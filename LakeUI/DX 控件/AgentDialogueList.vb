Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Drawing
Imports System.Numerics
Imports System.Windows.Forms
Imports Vortice.Direct2D1
Imports Vortice.DirectWrite

''' <summary>
''' Agent 对话管理列表。控件只管理文件夹、对话的顺序和呈现，不承载对话内容控件。
''' </summary>
<DefaultEvent("DialogueSelected")>
Public Class AgentDialogueList
    Implements D3D_IGpuRenderable, D3D_IGpuInvalidationSource, D3D_ISuperSamplingSource,
               D3D_IBackgroundSourceProvider, V5_IGpuPresentationSource

#Region "公开数据模型"
    Public Class AgentDialogueActionButton
        Private _image As Image
        Private _toolTipText As String = ""
        Private _name As String = ""
        Private _id As String = Guid.NewGuid().ToString("N")
        Private _enabled As Boolean = True
        Private _tag As Object

        <Category("LakeUI"), Description("按钮图像。")>
        Public Property Image As Image
            Get
                Return _image
            End Get
            Set(value As Image)
                _image = value
                NotifyOwner()
            End Set
        End Property

        <Category("LakeUI"), Description("按钮图标，Image 的别名。")>
        Public Property Icon As Image
            Get
                Return Image
            End Get
            Set(value As Image)
                Image = value
            End Set
        End Property

        <Category("LakeUI - ToolTip"), Description("鼠标悬停时显示的工具提示。"), DefaultValue("")>
        Public Property ToolTipText As String
            Get
                Return _toolTipText
            End Get
            Set(value As String)
                _toolTipText = If(value, "")
                NotifyOwner()
            End Set
        End Property

        <Category("LakeUI"), Description("按钮的业务名称，便于在公共 ActionButtonClick 事件中识别。"), DefaultValue("")>
        Public Property Name As String
            Get
                Return _name
            End Get
            Set(value As String)
                _name = If(value, "").Trim()
                NotifyOwner()
            End Set
        End Property

        <Category("LakeUI"), Description("按钮的稳定标识；文件夹和对话项通过此标识引用共享按钮。")>
        Public Property Id As String
            Get
                Return _id
            End Get
            Set(value As String)
                value = If(value, "").Trim()
                If String.IsNullOrWhiteSpace(value) Then Throw New ArgumentException("Action button Id cannot be empty.", NameOf(value))
                If String.Equals(_id, value, StringComparison.Ordinal) Then Return
                If OwnerList IsNot Nothing Then
                    Throw New InvalidOperationException("Set an action button Id before adding the definition to ActionButtons.")
                End If
                OwnerList?.ValidateActionButtonId(Me, value)
                _id = value
                NotifyOwner()
            End Set
        End Property

        <Category("LakeUI"), Description("是否允许触发该按钮。"), DefaultValue(True)>
        Public Property Enabled As Boolean
            Get
                Return _enabled
            End Get
            Set(value As Boolean)
                If _enabled = value Then Return
                _enabled = value
                NotifyOwner()
            End Set
        End Property

        <Category("LakeUI"), Description("业务层可保存的任意关联对象。"), Browsable(True)>
        Public Property Tag As Object
            Get
                Return _tag
            End Get
            Set(value As Object)
                _tag = value
            End Set
        End Property

        ''' <summary>业务层绑定的触发委托。控件点击按钮时会调用它。</summary>
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Trigger As Action(Of AgentDialogueActionButtonEventArgs)

        Public Event Click As EventHandler(Of AgentDialogueActionButtonEventArgs)

        Friend Property OwnerList As AgentDialogueList

        Public Sub New()
        End Sub

        Public Sub New(name As String, image As Image, toolTipText As String)
            _name = If(name, "").Trim()
            If Not String.IsNullOrWhiteSpace(_name) Then _id = _name
            _image = image
            _toolTipText = If(toolTipText, "")
        End Sub

        Public Sub PerformClick()
            If Not _enabled Then Return
            Dim args As New AgentDialogueActionButtonEventArgs(OwnerList, Nothing, Nothing, Me)
            Trigger?.Invoke(args)
            RaiseEvent Click(Me, args)
        End Sub

        Friend Sub PerformClick(folder As AgentDialogueFolder, dialogue As AgentDialogue)
            If Not _enabled Then Return
            Dim args As New AgentDialogueActionButtonEventArgs(OwnerList, folder, dialogue, Me)
            Trigger?.Invoke(args)
            RaiseEvent Click(Me, args)
        End Sub

        Private Sub NotifyOwner()
            OwnerList?.ModelChanged()
        End Sub

        Public Overrides Function ToString() As String
            Return If(String.IsNullOrEmpty(_name), "ActionButton", _name)
        End Function
    End Class

    <TypeConverter(GetType(ExpandableObjectConverter))>
    Public Class AgentDialogueActionButtonCollection
        Inherits Collection(Of AgentDialogueActionButton)

        Private ReadOnly _owner As AgentDialogueList

        Friend Sub New(owner As AgentDialogueList)
            _owner = owner
        End Sub

        Public Sub New()
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As AgentDialogueActionButton)
            If item Is Nothing Then Return
            _owner?.ValidateActionButtonId(Nothing, item.Id)
            If item.OwnerList IsNot Nothing AndAlso item.OwnerList IsNot _owner Then
                Throw New InvalidOperationException("An action button definition cannot belong to multiple lists.")
            End If
            MyBase.InsertItem(index, item)
            item.OwnerList = _owner
            NotifyOwner()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, item As AgentDialogueActionButton)
            If item Is Nothing Then Return
            _owner?.ValidateActionButtonId(Me(index), item.Id)
            If item.OwnerList IsNot Nothing AndAlso item.OwnerList IsNot _owner Then
                Throw New InvalidOperationException("An action button definition cannot belong to multiple lists.")
            End If
            Me(index).OwnerList = Nothing
            MyBase.SetItem(index, item)
            item.OwnerList = _owner
            NotifyOwner()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            Me(index).OwnerList = Nothing
            MyBase.RemoveItem(index)
            NotifyOwner()
        End Sub

        Protected Overrides Sub ClearItems()
            For i As Integer = 0 To Count - 1
                Me(i).OwnerList = Nothing
            Next
            MyBase.ClearItems()
            NotifyOwner()
        End Sub

        Private Sub NotifyOwner()
            _owner?.ModelChanged()
        End Sub
    End Class

    ''' <summary>保存共享按钮定义的 Id；图片和触发器由 AgentDialogueList.ActionButtons 集中持有。</summary>
    <TypeConverter(GetType(ExpandableObjectConverter))>
    Public Class AgentDialogueActionButtonIdCollection
        Inherits Collection(Of String)

        Private ReadOnly _owner As Object

        Friend Sub New(owner As Object)
            _owner = owner
        End Sub

        Public Sub New()
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As String)
            item = If(item, "").Trim()
            If item.Length = 0 OrElse ContainsId(item) Then Return
            MyBase.InsertItem(index, item)
            NotifyOwner()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, item As String)
            item = If(item, "").Trim()
            If item.Length = 0 Then Return
            For i As Integer = 0 To Count - 1
                If i <> index AndAlso String.Equals(Me(i), item, StringComparison.Ordinal) Then Return
            Next
            MyBase.SetItem(index, item)
            NotifyOwner()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            MyBase.RemoveItem(index)
            NotifyOwner()
        End Sub

        Protected Overrides Sub ClearItems()
            MyBase.ClearItems()
            NotifyOwner()
        End Sub

        Private Function ContainsId(id As String) As Boolean
            For Each existing In Me
                If String.Equals(existing, id, StringComparison.Ordinal) Then Return True
            Next
            Return False
        End Function

        Private Sub NotifyOwner()
            If TypeOf _owner Is AgentDialogueFolder Then
                DirectCast(_owner, AgentDialogueFolder).OwnerList?.ModelChanged()
            ElseIf TypeOf _owner Is AgentDialogue Then
                DirectCast(_owner, AgentDialogue).OwnerList?.ModelChanged()
            End If
        End Sub
    End Class

    Public Class AgentDialogue
        Private _id As String = Guid.NewGuid().ToString("N")
        Private _text As String = "New dialogue"
        Private _subtitle As String = ""
        Private _icon As Image
        Private _toolTipText As String = ""
        Private _visible As Boolean = True
        Private _isInProgress As Boolean
        Private _font As Font
        Private _foreColor As Color = Color.Empty
        Private _selectedForeColor As Color = Color.Empty
        Private ReadOnly _actionButtonIds As AgentDialogueActionButtonIdCollection

        Friend Property OwnerList As AgentDialogueList
        Friend Property ParentFolder As AgentDialogueFolder

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public ReadOnly Property Folder As AgentDialogueFolder
            Get
                Return ParentFolder
            End Get
        End Property

        <Category("LakeUI"), Description("稳定的业务标识。")>
        Public Property Id As String
            Get
                Return _id
            End Get
            Set(value As String)
                _id = If(value, "")
                OwnerList?.ModelChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("对话标题。"), DefaultValue("New dialogue")>
        Public Property Text As String
            Get
                Return _text
            End Get
            Set(value As String)
                _text = If(value, "")
                OwnerList?.ModelChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("标题下方的可选辅助文本。"), DefaultValue("")>
        Public Property Subtitle As String
            Get
                Return _subtitle
            End Get
            Set(value As String)
                _subtitle = If(value, "")
                OwnerList?.ModelChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("对话图标。")>
        Public Property Icon As Image
            Get
                Return _icon
            End Get
            Set(value As Image)
                _icon = value
                OwnerList?.ModelChanged()
            End Set
        End Property

        <Category("LakeUI - ToolTip"), Description("对话项工具提示。"), DefaultValue("")>
        Public Property ToolTipText As String
            Get
                Return _toolTipText
            End Get
            Set(value As String)
                _toolTipText = If(value, "")
                OwnerList?.ModelChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("是否显示该对话。"), DefaultValue(True)>
        Public Property Visible As Boolean
            Get
                Return _visible
            End Get
            Set(value As Boolean)
                If _visible = value Then Return
                _visible = value
                OwnerList?.ModelChanged()
            End Set
        End Property

        <Category("LakeUI - Agent"), Description("是否正在进行 Agent 工作；启用后在对话文本左侧显示 Win11 风格 ProgressRing。"), DefaultValue(False)>
        Public Property IsInProgress As Boolean
            Get
                Return _isInProgress
            End Get
            Set(value As Boolean)
                If _isInProgress = value Then Return
                _isInProgress = value
                OwnerList?.ModelChanged()
            End Set
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property IsRunning As Boolean
            Get
                Return IsInProgress
            End Get
            Set(value As Boolean)
                IsInProgress = value
            End Set
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property IsProcessing As Boolean
            Get
                Return IsInProgress
            End Get
            Set(value As Boolean)
                IsInProgress = value
            End Set
        End Property

        <Category("LakeUI"), Description("该对话标题的字体，为 Nothing 时使用控件字体。")>
        Public Property DialogueFont As Font
            Get
                Return _font
            End Get
            Set(value As Font)
                _font = value
                OwnerList?.ModelChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("该对话未选中时的文字颜色，为 Empty 时使用控件颜色。"), DefaultValue(GetType(Color), "")>
        Public Property ForeColor As Color
            Get
                Return _foreColor
            End Get
            Set(value As Color)
                _foreColor = value
                OwnerList?.ModelChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("该对话选中时的文字颜色，为 Empty 时使用控件颜色。"), DefaultValue(GetType(Color), "")>
        Public Property SelectedForeColor As Color
            Get
                Return _selectedForeColor
            End Get
            Set(value As Color)
                _selectedForeColor = value
                OwnerList?.ModelChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("此对话显示的共享操作按钮 Id；按钮图像和触发逻辑统一在 AgentDialogueList.ActionButtons 中定义。"), DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
        Public ReadOnly Property ActionButtonIds As AgentDialogueActionButtonIdCollection
            Get
                Return _actionButtonIds
            End Get
        End Property

        Public Sub New()
            _actionButtonIds = New AgentDialogueActionButtonIdCollection(Me)
        End Sub

        Public Sub New(text As String)
            Me.New()
            _text = If(text, "")
        End Sub

        Public Overrides Function ToString() As String
            Return If(String.IsNullOrEmpty(_text), "New dialogue", _text)
        End Function
    End Class

    Public Class AgentDialogueCollection
        Inherits Collection(Of AgentDialogue)

        Private ReadOnly _owner As AgentDialogueFolder

        Friend Sub New(owner As AgentDialogueFolder)
            _owner = owner
        End Sub

        Public Sub New()
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As AgentDialogue)
            If item Is Nothing Then Return
            MyBase.InsertItem(index, item)
            Attach(item)
            _owner?.OwnerList?.ModelChanged()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, item As AgentDialogue)
            If item Is Nothing Then Return
            MyBase.SetItem(index, item)
            Attach(item)
            _owner?.OwnerList?.ModelChanged()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            Dim old = If(index >= 0 AndAlso index < Count, Me(index), Nothing)
            MyBase.RemoveItem(index)
            If old IsNot Nothing Then
                old.ParentFolder = Nothing
                old.OwnerList = Nothing
            End If
            _owner?.OwnerList?.ModelChanged()
        End Sub

        Protected Overrides Sub ClearItems()
            For i As Integer = 0 To Count - 1
                Dim item As AgentDialogue = Me(i)
                If item IsNot Nothing Then
                    item.ParentFolder = Nothing
                    item.OwnerList = Nothing
                End If
            Next
            MyBase.ClearItems()
            _owner?.OwnerList?.ModelChanged()
        End Sub

        Private Sub Attach(item As AgentDialogue)
            item.ParentFolder = _owner
            item.OwnerList = _owner?.OwnerList
        End Sub
    End Class

    Public Class AgentDialogueFolder
        Private _id As String = Guid.NewGuid().ToString("N")
        Private _text As String = "Project"
        Private _icon As Image
        Private _toolTipText As String = ""
        Private _isExpanded As Boolean = True
        Private _visible As Boolean = True
        Private _font As Font
        Private _foreColor As Color = Color.Empty
        Private ReadOnly _dialogues As AgentDialogueCollection
        Private ReadOnly _actionButtonIds As AgentDialogueActionButtonIdCollection

        Friend Property OwnerList As AgentDialogueList
        Friend Property IsBuiltInUnassigned As Boolean

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public ReadOnly Property IsUnassigned As Boolean
            Get
                Return IsBuiltInUnassigned
            End Get
        End Property

        <Category("LakeUI"), Description("稳定的业务标识。")>
        Public Property Id As String
            Get
                Return _id
            End Get
            Set(value As String)
                _id = If(value, "")
                OwnerList?.ModelChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("文件夹标题。"), DefaultValue("Project")>
        Public Property Text As String
            Get
                Return _text
            End Get
            Set(value As String)
                _text = If(value, "")
                OwnerList?.ModelChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("文件夹图标。")>
        Public Property Icon As Image
            Get
                Return _icon
            End Get
            Set(value As Image)
                _icon = value
                OwnerList?.ModelChanged()
            End Set
        End Property

        <Category("LakeUI - ToolTip"), Description("文件夹项工具提示。"), DefaultValue("")>
        Public Property ToolTipText As String
            Get
                Return _toolTipText
            End Get
            Set(value As String)
                _toolTipText = If(value, "")
                OwnerList?.ModelChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("是否展开显示文件夹内的对话。"), DefaultValue(True)>
        Public Property IsExpanded As Boolean
            Get
                Return _isExpanded
            End Get
            Set(value As Boolean)
                If _isExpanded = value Then Return
                _isExpanded = value
                OwnerList?.FolderExpansionChanged(Me)
            End Set
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property IsCollapsed As Boolean
            Get
                Return Not _isExpanded
            End Get
            Set(value As Boolean)
                IsExpanded = Not value
            End Set
        End Property

        <Category("LakeUI"), Description("是否显示该文件夹。"), DefaultValue(True)>
        Public Property Visible As Boolean
            Get
                Return _visible
            End Get
            Set(value As Boolean)
                If _visible = value Then Return
                _visible = value
                OwnerList?.ModelChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("该文件夹标题字体，为 Nothing 时使用控件字体。")>
        Public Property FolderFont As Font
            Get
                Return _font
            End Get
            Set(value As Font)
                _font = value
                OwnerList?.ModelChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("该文件夹标题颜色，为 Empty 时使用控件颜色。"), DefaultValue(GetType(Color), "")>
        Public Property ForeColor As Color
            Get
                Return _foreColor
            End Get
            Set(value As Color)
                _foreColor = value
                OwnerList?.ModelChanged()
            End Set
        End Property

        <Category("LakeUI"), Description("文件夹中的对话集合。"), DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
        Public ReadOnly Property Dialogues As AgentDialogueCollection
            Get
                Return _dialogues
            End Get
        End Property

        <Category("LakeUI"), Description("此文件夹显示的共享操作按钮 Id；按钮图像和触发逻辑统一在 AgentDialogueList.ActionButtons 中定义。"), DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
        Public ReadOnly Property ActionButtonIds As AgentDialogueActionButtonIdCollection
            Get
                Return _actionButtonIds
            End Get
        End Property

        Public Sub New()
            _dialogues = New AgentDialogueCollection(Me)
            _actionButtonIds = New AgentDialogueActionButtonIdCollection(Me)
        End Sub

        Public Sub New(text As String)
            Me.New()
            _text = If(text, "")
        End Sub

        Public Overrides Function ToString() As String
            Return If(String.IsNullOrEmpty(_text), "Project", _text)
        End Function
    End Class

    Public Class AgentDialogueFolderCollection
        Inherits Collection(Of AgentDialogueFolder)

        Private ReadOnly _owner As AgentDialogueList

        Friend Sub New(owner As AgentDialogueList)
            _owner = owner
        End Sub

        Public Sub New()
        End Sub

        Protected Overrides Sub InsertItem(index As Integer, item As AgentDialogueFolder)
            If item Is Nothing OrElse item.IsBuiltInUnassigned Then Return
            MyBase.InsertItem(index, item)
            Attach(item)
            _owner?.ModelChanged()
        End Sub

        Protected Overrides Sub SetItem(index As Integer, item As AgentDialogueFolder)
            If item Is Nothing OrElse item.IsBuiltInUnassigned Then Return
            MyBase.SetItem(index, item)
            Attach(item)
            _owner?.ModelChanged()
        End Sub

        Protected Overrides Sub RemoveItem(index As Integer)
            Dim old = If(index >= 0 AndAlso index < Count, Me(index), Nothing)
            MyBase.RemoveItem(index)
            If old IsNot Nothing Then
                old.OwnerList = Nothing
                For Each dialogue In old.Dialogues
                    dialogue.OwnerList = Nothing
                    dialogue.ParentFolder = Nothing
                Next
            End If
            _owner?.ModelChanged()
        End Sub

        Protected Overrides Sub ClearItems()
            For Each folder In Me
                If folder IsNot Nothing Then
                    folder.OwnerList = Nothing
                    For Each dialogue In folder.Dialogues
                        dialogue.OwnerList = Nothing
                        dialogue.ParentFolder = Nothing
                    Next
                End If
            Next
            MyBase.ClearItems()
            _owner?.ModelChanged()
        End Sub

        Private Sub Attach(folder As AgentDialogueFolder)
            folder.OwnerList = _owner
            For Each dialogue In folder.Dialogues
                dialogue.ParentFolder = folder
                dialogue.OwnerList = _owner
            Next
        End Sub
    End Class

    ' 提供简短模型名称，便于代码中快速构造数据项。
    Public Class Dialogue
        Inherits AgentDialogue
        Public Sub New()
            MyBase.New()
        End Sub
        Public Sub New(text As String)
            MyBase.New(text)
        End Sub
    End Class

    Public Class Folder
        Inherits AgentDialogueFolder
        Public Sub New()
            MyBase.New()
        End Sub
        Public Sub New(text As String)
            MyBase.New(text)
        End Sub
    End Class
#End Region

#Region "事件参数与事件"
    Public Class AgentDialogueActionButtonEventArgs
        Inherits EventArgs
        Public ReadOnly Property List As AgentDialogueList
        Public ReadOnly Property Folder As AgentDialogueFolder
        Public ReadOnly Property Dialogue As AgentDialogue
        Public ReadOnly Property Button As AgentDialogueActionButton

        Friend Sub New(list As AgentDialogueList, folder As AgentDialogueFolder,
                       dialogue As AgentDialogue, button As AgentDialogueActionButton)
            Me.List = list
            Me.Folder = folder
            Me.Dialogue = dialogue
            Me.Button = button
        End Sub
    End Class

    Public Class AgentDialogueEventArgs
        Inherits EventArgs
        Public ReadOnly Property Dialogue As AgentDialogue
        Public ReadOnly Property Folder As AgentDialogueFolder
        Public ReadOnly Property Index As Integer

        Friend Sub New(dialogue As AgentDialogue, folder As AgentDialogueFolder, index As Integer)
            Me.Dialogue = dialogue
            Me.Folder = folder
            Me.Index = index
        End Sub
    End Class

    Public Class AgentDialogueMoveEventArgs
        Inherits AgentDialogueEventArgs
        Public ReadOnly Property SourceFolder As AgentDialogueFolder
        Public ReadOnly Property TargetFolder As AgentDialogueFolder
        Public ReadOnly Property OldIndex As Integer
        Public ReadOnly Property NewIndex As Integer

        Friend Sub New(dialogue As AgentDialogue, sourceFolder As AgentDialogueFolder,
                       targetFolder As AgentDialogueFolder, oldIndex As Integer, newIndex As Integer)
            MyBase.New(dialogue, targetFolder, newIndex)
            Me.SourceFolder = sourceFolder
            Me.TargetFolder = targetFolder
            Me.OldIndex = oldIndex
            Me.NewIndex = newIndex
        End Sub
    End Class

    Public Class AgentDialogueFolderMoveEventArgs
        Inherits EventArgs

        Public ReadOnly Property Folder As AgentDialogueFolder
        Public ReadOnly Property OldIndex As Integer
        Public ReadOnly Property NewIndex As Integer

        Friend Sub New(folder As AgentDialogueFolder, oldIndex As Integer, newIndex As Integer)
            Me.Folder = folder
            Me.OldIndex = oldIndex
            Me.NewIndex = newIndex
        End Sub
    End Class

    Public Enum RightClickTargetKind
        None
        Folder
        Dialogue
    End Enum

    ''' <summary>AgentDialogueList 右键命中的数据。可直接判断右键发生在文件夹、对话或空白区域。</summary>
    Public Class AgentDialogueRightClickEventArgs
        Inherits EventArgs

        Public ReadOnly Property TargetKind As RightClickTargetKind
        Public ReadOnly Property Folder As AgentDialogueFolder
        Public ReadOnly Property Dialogue As AgentDialogue
        Public ReadOnly Property ActionButton As AgentDialogueActionButton
        Public ReadOnly Property Location As Point
        Public ReadOnly Property MouseArgs As MouseEventArgs

        Public ReadOnly Property IsFolder As Boolean
            Get
                Return TargetKind = RightClickTargetKind.Folder
            End Get
        End Property

        Public ReadOnly Property IsDialogue As Boolean
            Get
                Return TargetKind = RightClickTargetKind.Dialogue
            End Get
        End Property

        Friend Sub New(targetKind As RightClickTargetKind,
                       folder As AgentDialogueFolder,
                       dialogue As AgentDialogue,
                       actionButton As AgentDialogueActionButton,
                       location As Point,
                       mouseArgs As MouseEventArgs)
            Me.TargetKind = targetKind
            Me.Folder = folder
            Me.Dialogue = dialogue
            Me.ActionButton = actionButton
            Me.Location = location
            Me.MouseArgs = mouseArgs
        End Sub
    End Class

    Public Event DialogueSelected As EventHandler(Of AgentDialogueEventArgs)
    Public Event FolderSelected As EventHandler(Of AgentDialogueEventArgs)
    Public Event DialogueOrderChanged As EventHandler(Of AgentDialogueMoveEventArgs)
    Public Event DialogueFolderChanged As EventHandler(Of AgentDialogueMoveEventArgs)
    Public Event FolderOrderChanged As EventHandler(Of AgentDialogueFolderMoveEventArgs)
    Public Event FolderExpandedChanged As EventHandler(Of AgentDialogueEventArgs)
    Public Event ActionButtonClick As EventHandler(Of AgentDialogueActionButtonEventArgs)
    Public Event FolderActionButtonClick As EventHandler(Of AgentDialogueActionButtonEventArgs)
    Public Event ItemRightClick As EventHandler(Of AgentDialogueRightClickEventArgs)
    Public Event RightClick As EventHandler(Of AgentDialogueRightClickEventArgs)
    Public Event ItemsChanged As EventHandler
    Public Event SelectedIndexChanged As EventHandler
#End Region

#Region "绘制和内部状态"
    Private Enum RowKind
        Folder
        Dialogue
    End Enum

    Private Class LayoutRow
        Public Kind As RowKind
        Public Folder As AgentDialogueFolder
        Public Dialogue As AgentDialogue
        Public Bounds As RectangleF
        Public ButtonBounds As New List(Of RectangleF)()
        Public ButtonItems As New List(Of AgentDialogueActionButton)()
    End Class

    Private ReadOnly _folders As AgentDialogueFolderCollection
    Private ReadOnly _actionButtons As AgentDialogueActionButtonCollection
    Private ReadOnly _unassignedFolder As AgentDialogueFolder
    Private ReadOnly _rows As New List(Of LayoutRow)()
    Private ReadOnly _scrollBar As New D3D_ScrollBarRenderer()
    Private ReadOnly _toolTip As FloatingToolTipController
    Private ReadOnly _progressAnimation As New D3D_AnimationHelper(Me)
    Private ReadOnly _progressStopwatch As New Stopwatch()
    Private _progressAnimationRunning As Boolean
    Private _scrollOffset As Integer
    Private _hoverRow As LayoutRow
    Private _hoverButton As AgentDialogueActionButton
    Private _pressedButton As AgentDialogueActionButton
    Private _pressedRow As LayoutRow
    Private _dragDialogue As AgentDialogue
    Private _dragSourceFolder As AgentDialogueFolder
    Private _dragFolder As AgentDialogueFolder
    Private _pressedFolder As AgentDialogueFolder
    Private _dragStart As Point
    Private _isDragging As Boolean
    Private _dropFolder As AgentDialogueFolder
    Private _dropIndex As Integer = -1
    Private _dropFolderIndex As Integer = -1
    Private _selectedDialogue As AgentDialogue
    Private _selectedFolder As AgentDialogueFolder
    Private _batchDepth As Integer
    Private _layoutDirty As Boolean = True
    Private _layoutPadding As Padding
    Private _backgroundSource As Control

    Private _selectedDialogueBackColor As Color = Color.FromArgb(48, 48, 48)
    Private _hoverBackColor As Color = Color.FromArgb(48, 48, 48)
    Private _folderBackColor As Color = Color.Transparent
    Private _backColor1 As Color = Color.FromArgb(32, 32, 32)
    Private _dialogueForeColor As Color = Color.Silver
    Private _selectedDialogueForeColor As Color = Color.White
    Private _folderForeColor As Color = Color.Gainsboro
    Private _subtitleForeColor As Color = Color.FromArgb(150, 150, 150)
    Private _buttonBackColor As Color = Color.Transparent
    Private _buttonHoverBackColor As Color = Color.FromArgb(70, 70, 70)
    Private _dropIndicatorColor As Color = Color.DodgerBlue
    Private _borderColor As Color = Color.Empty
    Private _borderWidth As Integer
    Private _scrollBarTrackColor As Color = Color.FromArgb(24, 24, 24)
    Private _scrollBarThumbColor As Color = Color.FromArgb(88, 88, 88)
    Private _scrollBarThumbHoverColor As Color = Color.FromArgb(135, 135, 135)
    Private _folderFont As Font
    Private _dialogueFont As Font
    Private _subtitleFont As Font
    Private _selectedIndex As Integer = -1
    Private _superSamplingScale As GlobalOptions.SuperSamplingScaleEnum = GlobalOptions.SuperSamplingScaleEnum.OFF

    Private _folderItemHeight As Integer = 34
    Private _dialogueItemHeight As Integer = 42
    Private _itemSpacing As Integer = 2
    Private _dialogueIndent As Integer = 18
    Private _iconSize As Integer = 20
    Private _iconTextSpacing As Integer = 8
    Private _folderCollapseIconSize As Integer = 12
    Private _actionButtonSize As Integer = 24
    Private _actionButtonSpacing As Integer = 2
    Private _itemTextPadding As Integer = 8
    Private _scrollBarWidth As Integer = 6
    Private _cornerRadius As Integer = 6
    Private _dropIndicatorHeight As Integer = 2
    Private _unassignedFolderIcon As Image
    Private _toolTipBackColor As Color = Color.FromArgb(50, 50, 50)
    Private _toolTipForeColor As Color = Color.Silver
    Private _progressRingColor As Color = Color.FromArgb(0, 120, 215)
    Private _progressRingSize As Integer = 18
    Private _progressRingThickness As Single = 3.0F
    Private _progressRingAnimationDuration As Integer = 1500
    Private _progressRingAnimationFPS As Integer = 60

    Public Sub New()
        InitializeComponent()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw Or ControlStyles.Selectable Or
                 ControlStyles.SupportsTransparentBackColor, True)
        DoubleBuffered = False
        AllowDrop = True
        _progressAnimation.FPS = _progressRingAnimationFPS
        _progressAnimation.SetDirtyRectProvider(Function() New Rectangle(Point.Empty, Size))
        _folders = New AgentDialogueFolderCollection(Me)
        _actionButtons = New AgentDialogueActionButtonCollection(Me)
        _unassignedFolder = New AgentDialogueFolder("No project")
        _unassignedFolder.IsBuiltInUnassigned = True
        _unassignedFolder.OwnerList = Me
        _unassignedFolder.IsExpanded = True
        _toolTip = New FloatingToolTipController(Me, AddressOf GetToolTipTarget, AddressOf CreateToolTipStyle) With {.AutoPopDelay = 10000}
    End Sub

    <Category("LakeUI"), Description("项目文件夹集合。对话内容不由控件承载。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
    Public ReadOnly Property Folders As AgentDialogueFolderCollection
        Get
            Return _folders
        End Get
    End Property

    <Category("LakeUI"), Description("共享操作按钮定义。每个图像、提示和触发器只在此处保存一份；文件夹/对话通过 Id 引用。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
    Public ReadOnly Property ActionButtons As AgentDialogueActionButtonCollection
        Get
            Return _actionButtons
        End Get
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property SharedActionButtons As AgentDialogueActionButtonCollection
        Get
            Return _actionButtons
        End Get
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property UnassignedFolder As AgentDialogueFolder
        Get
            Return _unassignedFolder
        End Get
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property NoProjectFolder As AgentDialogueFolder
        Get
            Return _unassignedFolder
        End Get
    End Property

    <Category("LakeUI"), Description("无项目分组要显示的共享操作按钮 Id。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
    Public ReadOnly Property UnassignedActionButtonIds As AgentDialogueActionButtonIdCollection
        Get
            Return _unassignedFolder.ActionButtonIds
        End Get
    End Property

    <Category("LakeUI"), Description("无项目分组中的常规对话集合。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
    Public ReadOnly Property UnassignedDialogues As AgentDialogueCollection
        Get
            Return _unassignedFolder.Dialogues
        End Get
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property ProjectFolders As AgentDialogueFolderCollection
        Get
            Return _folders
        End Get
    End Property

    <Category("LakeUI"), Description("无项目文件夹的自定义标题，与 UnassignedFolder.Text 保持同步。"), DefaultValue("No project")>
    Public Property UnassignedFolderText As String
        Get
            Return _unassignedFolder.Text
        End Get
        Set(value As String)
            _unassignedFolder.Text = If(value, "")
        End Set
    End Property

    <Category("LakeUI"), Description("无项目分组图标。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property UnassignedFolderIcon As Image
        Get
            Return _unassignedFolderIcon
        End Get
        Set(value As Image)
            _unassignedFolderIcon = value
            _unassignedFolder.Icon = value
            ModelChanged()
        End Set
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SelectedDialogue As AgentDialogue
        Get
            Return _selectedDialogue
        End Get
        Set(value As AgentDialogue)
            If value IsNot Nothing AndAlso (value.ParentFolder Is Nothing OrElse
                                            (value.ParentFolder IsNot _unassignedFolder AndAlso Not _folders.Contains(value.ParentFolder))) Then
                value = Nothing
            End If
            SetSelectedDialogue(value)
        End Set
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property SelectedFolder As AgentDialogueFolder
        Get
            Return _selectedFolder
        End Get
    End Property

    <Category("LakeUI"), Description("当前选中的可见对话索引，-1 表示没有选中项。"), DefaultValue(-1)>
    Public Property SelectedIndex As Integer
        Get
            Return _selectedIndex
        End Get
        Set(value As Integer)
            EnsureLayout()
            If value < 0 Then
                SetSelectedDialogue(Nothing)
                Return
            End If
            Dim current = 0
            For Each row In _rows
                If row.Kind = RowKind.Dialogue Then
                    If current = value Then
                        SetSelectedDialogue(row.Dialogue)
                        Return
                    End If
                    current += 1
                End If
            Next
            SetSelectedDialogue(Nothing)
        End Set
    End Property

    Public Sub RefreshItems()
        ModelChanged()
    End Sub

    Public Sub BeginUpdate()
        _batchDepth += 1
    End Sub

    Public Sub EndUpdate()
        If _batchDepth > 0 Then _batchDepth -= 1
        If _batchDepth = 0 Then ModelChanged()
    End Sub

    ''' <summary>将对话移动到目标文件夹的最终索引位置；索引以移除源项后的集合为准。</summary>
    Public Function MoveDialogue(dialogue As AgentDialogue, targetFolder As AgentDialogueFolder,
                                 targetIndex As Integer) As Boolean
        If dialogue Is Nothing Then Return False
        If targetFolder Is Nothing Then targetFolder = _unassignedFolder
        If targetFolder IsNot _unassignedFolder AndAlso Not _folders.Contains(targetFolder) Then Return False
        Dim source = dialogue.ParentFolder
        If source Is Nothing Then Return False
        Dim sourceIndex = source.Dialogues.IndexOf(dialogue)
        If sourceIndex < 0 Then Return False
        Dim targetCountAfterRemoval = targetFolder.Dialogues.Count - If(source Is targetFolder, 1, 0)
        targetIndex = Math.Max(0, Math.Min(Math.Max(0, targetCountAfterRemoval), targetIndex))
        If source Is targetFolder AndAlso sourceIndex = targetIndex Then Return False

        Dim oldIndex = sourceIndex
        BeginUpdate()
        Try
            source.Dialogues.RemoveAt(sourceIndex)
            targetFolder.Dialogues.Insert(targetIndex, dialogue)
        Finally
            EndUpdate()
        End Try
        Dim args As New AgentDialogueMoveEventArgs(dialogue, source, targetFolder, oldIndex, targetIndex)
        RaiseEvent DialogueOrderChanged(Me, args)
        If source IsNot targetFolder Then RaiseEvent DialogueFolderChanged(Me, args)
        Return True
    End Function

    Public Function MoveDialogue(dialogue As AgentDialogue, targetFolder As AgentDialogueFolder) As Boolean
        If targetFolder Is Nothing Then targetFolder = _unassignedFolder
        Return MoveDialogue(dialogue, targetFolder, targetFolder.Dialogues.Count)
    End Function

    ''' <summary>按最终索引调整项目文件夹顺序。</summary>
    Public Function MoveFolder(folder As AgentDialogueFolder, targetIndex As Integer) As Boolean
        If folder Is Nothing OrElse folder Is _unassignedFolder Then Return False
        Dim oldIndex = _folders.IndexOf(folder)
        If oldIndex < 0 Then Return False
        targetIndex = Math.Max(0, Math.Min(_folders.Count - 1, targetIndex))
        If oldIndex = targetIndex Then Return False
        Dim selectedBefore = _selectedFolder
        BeginUpdate()
        Try
            _folders.RemoveAt(oldIndex)
            targetIndex = Math.Min(targetIndex, _folders.Count)
            _folders.Insert(targetIndex, folder)
        Finally
            EndUpdate()
        End Try
        _selectedFolder = selectedBefore
        RaiseEvent FolderOrderChanged(Me, New AgentDialogueFolderMoveEventArgs(folder, oldIndex, targetIndex))
        RequestRender()
        Return True
    End Function

    Private Sub SetSelectedDialogue(value As AgentDialogue)
        If Object.ReferenceEquals(_selectedDialogue, value) Then Return
        If value IsNot Nothing Then EnsureLayout()
        _selectedDialogue = value
        _selectedFolder = If(value Is Nothing, Nothing, value.ParentFolder)
        _selectedIndex = If(value Is Nothing, -1, GetGlobalDialogueIndex(value))
        RequestRender()
        RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
        If value IsNot Nothing Then
            RaiseEvent DialogueSelected(Me, New AgentDialogueEventArgs(value, value.ParentFolder,
                                                                       value.ParentFolder.Dialogues.IndexOf(value)))
        End If
    End Sub

    Private Function GetGlobalDialogueIndex(value As AgentDialogue) As Integer
        Dim index = 0
        For Each row In _rows
            If row.Kind = RowKind.Dialogue Then
                If row.Dialogue Is value Then Return index
                index += 1
            End If
        Next
        Return -1
    End Function

    Friend Sub ModelChanged()
        _layoutDirty = True
        If _batchDepth > 0 Then Return
        If _selectedFolder IsNot Nothing AndAlso _selectedFolder IsNot _unassignedFolder AndAlso
           Not _folders.Contains(_selectedFolder) Then
            _selectedFolder = Nothing
        End If
        If _selectedDialogue IsNot Nothing AndAlso _selectedDialogue.ParentFolder Is Nothing Then
            SetSelectedDialogue(Nothing)
        ElseIf _selectedDialogue IsNot Nothing Then
            EnsureLayout()
            _selectedFolder = _selectedDialogue.ParentFolder
            _selectedIndex = GetGlobalDialogueIndex(_selectedDialogue)
        End If
        RaiseEvent ItemsChanged(Me, EventArgs.Empty)
        UpdateProgressAnimationState()
        RequestRender()
    End Sub

    Friend Sub ValidateActionButtonId(except As AgentDialogueActionButton, candidateId As String)
        If _actionButtons Is Nothing Then Return
        For Each existing In _actionButtons
            If existing Is Nothing OrElse Object.ReferenceEquals(existing, except) Then Continue For
            If String.Equals(existing.Id, candidateId, StringComparison.Ordinal) Then
                Throw New InvalidOperationException($"Action button Id '{candidateId}' is already in use by this list.")
            End If
        Next
    End Sub

    Private Function FindActionButton(id As String) As AgentDialogueActionButton
        If String.IsNullOrWhiteSpace(id) Then Return Nothing
        For Each button In _actionButtons
            If button IsNot Nothing AndAlso String.Equals(button.Id, id, StringComparison.Ordinal) Then Return button
        Next
        Return Nothing
    End Function

    Friend Sub FolderExpansionChanged(folder As AgentDialogueFolder)
        _layoutDirty = True
        If _batchDepth > 0 Then Return
        If _selectedDialogue IsNot Nothing Then
            EnsureLayout()
            _selectedFolder = _selectedDialogue.ParentFolder
            _selectedIndex = GetGlobalDialogueIndex(_selectedDialogue)
        End If
        RaiseEvent FolderExpandedChanged(Me, New AgentDialogueEventArgs(Nothing, folder, -1))
        RaiseEvent ItemsChanged(Me, EventArgs.Empty)
        UpdateProgressAnimationState()
        RequestRender()
    End Sub

    Private Sub RequestRender(Optional dirty As Rectangle = Nothing)
        If IsDisposed Then Return
        Dim r = If(dirty.IsEmpty, New Rectangle(Point.Empty, Size), dirty)
        D3D_InvalidationRouter.RequestRender(Me, r)
    End Sub

    Private Function HasVisibleProgressDialogue() As Boolean
        If _unassignedFolder.IsExpanded Then
            For Each dialogue In _unassignedFolder.Dialogues
                If dialogue IsNot Nothing AndAlso dialogue.Visible AndAlso dialogue.IsInProgress Then Return True
            Next
        End If
        For Each folder In _folders
            If folder Is Nothing OrElse Not folder.Visible OrElse Not folder.IsExpanded Then Continue For
            For Each dialogue In folder.Dialogues
                If dialogue IsNot Nothing AndAlso dialogue.Visible AndAlso dialogue.IsInProgress Then Return True
            Next
        Next
        Return False
    End Function

    Private Function ShouldRunProgressAnimation() As Boolean
        Return Not DesignMode AndAlso IsHandleCreated AndAlso Not IsDisposed AndAlso
               Visible AndAlso Enabled AndAlso HasVisibleProgressDialogue()
    End Function

    Private Sub UpdateProgressAnimationState()
        Dim shouldRun = ShouldRunProgressAnimation()
        If shouldRun Then
            If _progressAnimationRunning Then Return
            _progressAnimationRunning = True
            _progressStopwatch.Restart()
            _progressAnimation.FPS = _progressRingAnimationFPS
            _progressAnimation.StartFrameLoop(AddressOf ProgressAnimationTick)
        ElseIf _progressAnimationRunning Then
            _progressAnimationRunning = False
            _progressAnimation.StopFrameLoop()
            _progressStopwatch.Stop()
        End If
    End Sub

    Private Sub ProgressAnimationTick(sender As Object, e As EventArgs)
        If Not ShouldRunProgressAnimation() Then UpdateProgressAnimationState()
        ' D3D_AnimationHelper submits the dirty rectangle after this callback.
    End Sub

    Private Function DpiScale() As Single
        Return D3D_DpiContext.FromControl(Me).Scale
    End Function

    Private Function S(value As Integer) As Single
        Return value * DpiScale()
    End Function

    Private Sub EnsureLayout()
        If _layoutPadding <> Padding Then _layoutDirty = True
        If Not _layoutDirty Then Return
        _layoutDirty = False
        _layoutPadding = Padding
        Dim scale = DpiScale()
        Dim pad = Padding
        Dim x = CSng(pad.Left)
        Dim scrollReserve = If(_scrollBarWidth > 0, _scrollBarWidth + D3D_ScrollBarRenderer.Margin * 2 + 2, 0)
        Dim availableWidth = Math.Max(0.0F, Width - pad.Horizontal)
        Dim viewportHeight = Math.Max(0.0F, Height - pad.Vertical)
        Dim yContent As Single
        BuildVisibleRows(x, availableWidth, scale, yContent)
        Dim totalHeight = Math.Max(0.0F, yContent - pad.Top)
        Dim totalPixels = CInt(Math.Ceiling(totalHeight))
        Dim viewportPixels = CInt(Math.Floor(viewportHeight))
        Dim maxOffset = Math.Max(0, totalPixels - viewportPixels)
        _scrollOffset = Math.Max(0, Math.Min(maxOffset, _scrollOffset))
        If _scrollBarWidth > 0 AndAlso totalPixels > viewportPixels Then
            ' 只有实际出现滚动条时才为它预留宽度。
            availableWidth = Math.Max(0.0F, availableWidth - S(scrollReserve))
            BuildVisibleRows(x, availableWidth, scale, yContent)
            _scrollBar.ComputeLayout(Width, Height, 0, 0, pad.Top, pad.Bottom,
                                     CInt(S(_scrollBarWidth)), totalPixels, viewportPixels, _scrollOffset)
        Else
            _scrollBar.ThumbRect = Rectangle.Empty
            _scrollBar.TrackRect = Rectangle.Empty
        End If
        For Each row In _rows
            row.Bounds = New RectangleF(row.Bounds.X, row.Bounds.Y - _scrollOffset,
                                        row.Bounds.Width, row.Bounds.Height)
            For i = 0 To row.ButtonBounds.Count - 1
                Dim b = row.ButtonBounds(i)
                row.ButtonBounds(i) = New RectangleF(b.X, b.Y - _scrollOffset, b.Width, b.Height)
            Next
        Next
    End Sub

    Private Sub BuildVisibleRows(x As Single, availableWidth As Single, scale As Single,
                                 ByRef yContent As Single)
        _rows.Clear()
        yContent = Padding.Top
        AddFolderRows(_unassignedFolder, x, availableWidth, yContent, scale)
        For Each folder In _folders
            If folder Is Nothing OrElse Not folder.Visible Then Continue For
            AddFolderRows(folder, x, availableWidth, yContent, scale)
        Next
    End Sub

    Private Sub AddFolderRows(folder As AgentDialogueFolder, x As Single, width As Single,
                              ByRef y As Single, scale As Single)
        If folder Is Nothing Then Return
        Dim row As New LayoutRow With {.Kind = RowKind.Folder, .Folder = folder,
                                       .Bounds = New RectangleF(x, y, width, S(_folderItemHeight))}
        AddButtonBounds(row, folder.ActionButtonIds, scale)
        _rows.Add(row)
        y += S(_folderItemHeight) + S(_itemSpacing)
        If Not folder.IsExpanded Then Return
        For Each dialogue In folder.Dialogues
            If dialogue Is Nothing OrElse Not dialogue.Visible Then Continue For
            Dim dialogueX = x + S(_dialogueIndent)
            Dim dialogueWidth = Math.Max(0.0F, width - S(_dialogueIndent))
            Dim drow As New LayoutRow With {.Kind = RowKind.Dialogue, .Folder = folder,
                .Dialogue = dialogue, .Bounds = New RectangleF(dialogueX, y, dialogueWidth, S(_dialogueItemHeight))}
            AddButtonBounds(drow, dialogue.ActionButtonIds, scale)
            _rows.Add(drow)
            y += S(_dialogueItemHeight) + S(_itemSpacing)
        Next
    End Sub

    Private Sub AddButtonBounds(row As LayoutRow, buttonIds As AgentDialogueActionButtonIdCollection, scale As Single)
        If buttonIds Is Nothing OrElse buttonIds.Count = 0 Then Return
        Dim right = row.Bounds.Right - S(4)
        For i = buttonIds.Count - 1 To 0 Step -1
            Dim button = FindActionButton(buttonIds(i))
            If button Is Nothing Then Continue For
            Dim left = right - S(_actionButtonSize)
            row.ButtonBounds.Insert(0, New RectangleF(left, row.Bounds.Y + (row.Bounds.Height - S(_actionButtonSize)) / 2.0F,
                                                       S(_actionButtonSize), S(_actionButtonSize)))
            row.ButtonItems.Insert(0, button)
            right = left - S(_actionButtonSpacing)
        Next
    End Sub

    Private Function HitTest(pt As Point, ByRef hitRow As LayoutRow, ByRef hitButton As AgentDialogueActionButton) As Boolean
        EnsureLayout()
        hitRow = Nothing
        hitButton = Nothing
        For Each row In _rows
            If Not row.Bounds.Contains(pt.X, pt.Y) Then Continue For
            hitRow = row
            For i = 0 To row.ButtonBounds.Count - 1
                If row.ButtonBounds(i).Contains(pt.X, pt.Y) Then
                    hitButton = row.ButtonItems(i)
                    Exit For
                End If
            Next
            Return True
        Next
        Return False
    End Function

    Private Function IsRowHovered(row As LayoutRow) As Boolean
        If row Is Nothing OrElse _hoverRow Is Nothing OrElse row.Kind <> _hoverRow.Kind Then Return False
        If row.Kind = RowKind.Folder Then Return Object.ReferenceEquals(row.Folder, _hoverRow.Folder)
        Return Object.ReferenceEquals(row.Dialogue, _hoverRow.Dialogue)
    End Function

    Private Sub UpdateDropTarget(pt As Point)
        _dropFolder = Nothing
        _dropIndex = -1
        _dropFolderIndex = -1
        Dim row As LayoutRow = Nothing
        Dim button As AgentDialogueActionButton = Nothing
        If Not HitTest(pt, row, button) Then Return
        If _dragFolder IsNot Nothing Then
            ' Folder ordering uses gaps between project headers. Dropping over a
            ' dialogue resolves to its owning folder's gap, so the whole group
            ' stays together even when expanded.
            Dim hitFolder = row.Folder
            If hitFolder Is _unassignedFolder Then
                If _folders.Count = 0 Then Return
                _dropFolderIndex = If(pt.Y > row.Bounds.Top + row.Bounds.Height / 2.0F, 0, 0)
                Return
            End If
            Dim hitIndex = _folders.IndexOf(hitFolder)
            If hitIndex < 0 Then Return
            Dim groupTop As Single = Single.MaxValue
            Dim groupBottom As Single = Single.MinValue
            For Each candidate In _rows
                If candidate.Folder IsNot hitFolder Then Continue For
                groupTop = Math.Min(groupTop, candidate.Bounds.Top)
                groupBottom = Math.Max(groupBottom, candidate.Bounds.Bottom)
            Next
            Dim midpoint = If(groupTop = Single.MaxValue, row.Bounds.Top + row.Bounds.Height / 2.0F,
                              (groupTop + groupBottom) / 2.0F)
            _dropFolderIndex = If(pt.Y >= midpoint, hitIndex + 1, hitIndex)
            Return
        End If
        If row.Kind = RowKind.Folder Then
            _dropFolder = row.Folder
            _dropIndex = row.Folder.Dialogues.Count
            Return
        End If
        _dropFolder = row.Folder
        _dropIndex = row.Folder.Dialogues.IndexOf(row.Dialogue)
        If pt.Y > row.Bounds.Top + row.Bounds.Height / 2.0F Then _dropIndex += 1
    End Sub
#End Region

#Region "GPU 绘制"
    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        If _backgroundSource IsNot Nothing Then Return
        MyBase.OnPaintBackground(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Not D3D_PaintBridge.PaintRenderable(e, Me, Me) Then MyBase.OnPaint(e)
    End Sub

    Public Sub RenderGpu(context As D3D_PaintContext) Implements D3D_IGpuRenderable.RenderGpu
        If Width <= 0 OrElse Height <= 0 Then Return
        EnsureLayout()
        If _backgroundSource IsNot Nothing Then
            If Not context.DrawBackgroundSource(Me, _backgroundSource, New RectangleF(0, 0, Width, Height)) AndAlso BackColor.A > 0 Then
                context.FillRectangle(New RectangleF(0, 0, Width, Height), BackColor)
            End If
        ElseIf BackColor.A > 0 Then
            context.FillRectangle(New RectangleF(0, 0, Width, Height), BackColor)
        End If
        context.FillRectangle(New RectangleF(0, 0, Width, Height), _backColor1)

        Dim viewport As New RectangleF(Padding.Left, Padding.Top,
                                       Math.Max(0, Width - Padding.Horizontal),
                                       Math.Max(0, Height - Padding.Vertical))
        Using context.PushClip(viewport)
            For Each row In _rows
                If row.Bounds.Bottom < viewport.Top OrElse row.Bounds.Top > viewport.Bottom Then Continue For
                DrawRow(context, row)
            Next
            If _isDragging AndAlso ((_dragFolder IsNot Nothing AndAlso _dropFolderIndex >= 0) OrElse
                                    (_dragDialogue IsNot Nothing AndAlso _dropFolder IsNot Nothing AndAlso _dropIndex >= 0)) Then
                DrawDropIndicator(context)
            End If
        End Using
        DrawScrollBar(context)
        If _borderWidth > 0 AndAlso _borderColor.A > 0 Then
            Dim borderRect As New RectangleF(_borderWidth / 2.0F, _borderWidth / 2.0F,
                                             Math.Max(0, Width - _borderWidth), Math.Max(0, Height - _borderWidth))
            context.DrawRoundedRectangle(borderRect, S(_cornerRadius), _borderColor, S(_borderWidth))
        End If
    End Sub

    Public Function GetRenderBounds() As Rectangle Implements D3D_IGpuInvalidationSource.GetRenderBounds
        Return New Rectangle(Point.Empty, Size)
    End Function

    Private Sub DrawRow(context As D3D_PaintContext, row As LayoutRow)
        Dim isHover = IsRowHovered(row)
        Dim isSelected = row.Kind = RowKind.Dialogue AndAlso Object.ReferenceEquals(row.Dialogue, _selectedDialogue)
        Dim bg = If(isSelected, _selectedDialogueBackColor,
                     If(isHover, _hoverBackColor,
                        If(row.Kind = RowKind.Folder, _folderBackColor, Color.Transparent)))
        If bg.A > 0 Then FillRounded(context, row.Bounds, S(_cornerRadius), bg)

        Dim textLeft = row.Bounds.Left + S(_itemTextPadding)
        If row.Kind = RowKind.Dialogue AndAlso row.Dialogue.IsInProgress Then
            Dim ringSize = Math.Min(S(_progressRingSize), Math.Max(0.0F, row.Bounds.Height - S(8)))
            If ringSize > 0.0F Then
                Dim ringRect As New RectangleF(textLeft,
                                               row.Bounds.Top + (row.Bounds.Height - ringSize) / 2.0F,
                                               ringSize, ringSize)
                ProgressRingRenderer.RenderWin11(context, ringRect, _progressRingColor,
                                               _progressRingThickness * DpiScale(),
                                               _progressAnimationRunning,
                                               _progressStopwatch.Elapsed.TotalMilliseconds,
                                               DesignMode, _progressRingAnimationDuration)
                textLeft += ringSize + S(_iconTextSpacing)
            End If
        End If
        If row.Kind = RowKind.Folder Then
            ' 用两段线绘制直角折叠箭头：展开时向下，折叠时向右，
            ' 并固定显示在文件夹文本左侧。
            Dim glyphSize = S(_folderCollapseIconSize)
            Dim glyphCenterX = CInt(textLeft + glyphSize / 2.0F)
            Dim glyphCenterY = CInt(row.Bounds.Top + row.Bounds.Height / 2.0F)
            DrawChevronLines_GPU(context, glyphCenterX, glyphCenterY,
                                 glyphSize, row.Folder.IsExpanded,
                                 If(row.Folder.ForeColor = Color.Empty, _folderForeColor, row.Folder.ForeColor),
                                 Math.Max(1.0F, S(2)))
            textLeft += glyphSize + S(_iconTextSpacing)
        End If

        Dim icon As Image = If(row.Kind = RowKind.Folder, row.Folder.Icon, row.Dialogue.Icon)
        If icon IsNot Nothing Then
            Dim iconSize = Math.Min(S(_iconSize), Math.Max(0, row.Bounds.Height - S(8)))
            context.DrawImage(icon, New RectangleF(textLeft, row.Bounds.Top + (row.Bounds.Height - iconSize) / 2.0F,
                                                  iconSize, iconSize))
            textLeft += iconSize + S(_iconTextSpacing)
        End If

        Dim showActionButtons = IsRowHovered(row)
        Dim buttonWidth = If(Not showActionButtons OrElse row.ButtonBounds.Count = 0, 0.0F,
                             (S(_actionButtonSize) + S(_actionButtonSpacing)) * row.ButtonBounds.Count)
        Dim textRight = row.Bounds.Right - S(_itemTextPadding) - buttonWidth
        If row.Kind = RowKind.Folder Then
            context.DrawText(row.Folder.Text, If(row.Folder.FolderFont, If(_folderFont, Font)),
                             If(row.Folder.ForeColor = Color.Empty, _folderForeColor, row.Folder.ForeColor),
                             New RectangleF(textLeft, row.Bounds.Top, Math.Max(0, textRight - textLeft), row.Bounds.Height),
                             TextAlignment.Leading, ParagraphAlignment.Center)
        Else
            Dim titleHeight = If(String.IsNullOrEmpty(row.Dialogue.Subtitle), row.Bounds.Height, row.Bounds.Height * 0.58F)
            context.DrawText(row.Dialogue.Text, If(row.Dialogue.DialogueFont, If(_dialogueFont, Font)),
                             If(isSelected AndAlso row.Dialogue.SelectedForeColor <> Color.Empty,
                                row.Dialogue.SelectedForeColor,
                                If(isSelected, _selectedDialogueForeColor,
                                   If(row.Dialogue.ForeColor = Color.Empty, _dialogueForeColor, row.Dialogue.ForeColor))),
                             New RectangleF(textLeft, row.Bounds.Top, Math.Max(0, textRight - textLeft), titleHeight),
                             TextAlignment.Leading, ParagraphAlignment.Center)
            If Not String.IsNullOrEmpty(row.Dialogue.Subtitle) Then
                context.DrawText(row.Dialogue.Subtitle, If(_subtitleFont, Font), _subtitleForeColor,
                                 New RectangleF(textLeft, row.Bounds.Top + titleHeight - S(2), Math.Max(0, textRight - textLeft), row.Bounds.Height - titleHeight),
                                 TextAlignment.Leading, ParagraphAlignment.Center)
            End If
        End If

        If Not showActionButtons Then Return
        For i = 0 To row.ButtonBounds.Count - 1
            Dim button = row.ButtonItems(i)
            Dim b = row.ButtonBounds(i)
            If Object.ReferenceEquals(button, _hoverButton) AndAlso _buttonHoverBackColor.A > 0 Then
                FillRounded(context, b, S(4), _buttonHoverBackColor)
            ElseIf _buttonBackColor.A > 0 Then
                FillRounded(context, b, S(4), _buttonBackColor)
            End If
            If button.Image IsNot Nothing Then
                Dim inset = S(4)
                context.DrawImage(button.Image, New RectangleF(b.X + inset, b.Y + inset,
                                                               Math.Max(0, b.Width - inset * 2), Math.Max(0, b.Height - inset * 2)))
            End If
        Next
    End Sub

    ' 两条线在顶点处形成 90 度夹角，并用小方块补齐 Direct2D 线段连接处。
    Private Shared Sub DrawChevronLines_GPU(context As D3D_PaintContext,
                                            cx As Integer, cy As Integer, size As Single,
                                            expanded As Boolean, color As Color,
                                            strokeWidth As Single)
        If color.A = 0 Then Return
        Dim brush = context.Compositor.BrushCache.GetSolidBrush(context.DeviceContext,
                                                                 color, context.DeviceGeneration)
        Dim halfSize = size / 2.0F
        Dim firstPoint As Vector2
        Dim vertex As Vector2
        Dim lastPoint As Vector2
        If expanded Then
            firstPoint = New Vector2(cx - halfSize, cy - halfSize / 2.0F)
            vertex = New Vector2(cx, cy + halfSize / 2.0F)
            lastPoint = New Vector2(cx + halfSize, cy - halfSize / 2.0F)
        Else
            firstPoint = New Vector2(cx - halfSize / 2.0F, cy - halfSize)
            vertex = New Vector2(cx + halfSize / 2.0F, cy)
            lastPoint = New Vector2(cx - halfSize / 2.0F, cy + halfSize)
        End If
        context.DeviceContext.DrawLine(firstPoint, vertex, brush, strokeWidth)
        context.DeviceContext.DrawLine(vertex, lastPoint, brush, strokeWidth)
        Dim jointSize = Math.Max(1.0F, strokeWidth)
        context.FillRectangle(New RectangleF(vertex.X - jointSize / 2.0F,
                                             vertex.Y - jointSize / 2.0F,
                                             jointSize, jointSize), color)
    End Sub

    Private Sub DrawDropIndicator(context As D3D_PaintContext)
        If _dragFolder IsNot Nothing Then
            DrawFolderDropIndicator(context)
            Return
        End If
        Dim target As LayoutRow = Nothing
        Dim insertionBefore As Boolean = False
        For Each row In _rows
            If row.Kind = RowKind.Folder AndAlso row.Folder Is _dropFolder Then
                If _dropIndex = row.Folder.Dialogues.Count AndAlso row.Folder.Dialogues.Count = 0 Then target = row
            ElseIf row.Kind = RowKind.Dialogue AndAlso row.Folder Is _dropFolder Then
                Dim index = row.Folder.Dialogues.IndexOf(row.Dialogue)
                If index = _dropIndex OrElse index = _dropIndex - 1 Then
                    If index = _dropIndex Then
                        target = row
                        insertionBefore = True
                    End If
                End If
            End If
        Next
        If target Is Nothing AndAlso _dropFolder IsNot Nothing AndAlso
           _dropIndex = _dropFolder.Dialogues.Count Then
            For i As Integer = _rows.Count - 1 To 0 Step -1
                Dim row = _rows(i)
                If row.Kind = RowKind.Dialogue AndAlso row.Folder Is _dropFolder Then
                    target = row
                    Exit For
                End If
            Next
        End If
        If target Is Nothing Then Return
        Dim y = If(insertionBefore, target.Bounds.Top, target.Bounds.Bottom)
        context.FillRectangle(New RectangleF(target.Bounds.Left, y - S(_dropIndicatorHeight) / 2.0F,
                                             target.Bounds.Width, S(_dropIndicatorHeight)), _dropIndicatorColor)
    End Sub

    Private Sub DrawFolderDropIndicator(context As D3D_PaintContext)
        If _dropFolderIndex < 0 Then Return
        Dim target As LayoutRow = Nothing
        Dim y As Single
        If _dropFolderIndex < _folders.Count Then
            Dim targetFolder = _folders(_dropFolderIndex)
            For Each row In _rows
                If row.Kind = RowKind.Folder AndAlso row.Folder Is targetFolder Then
                    target = row
                    Exit For
                End If
            Next
            If target Is Nothing Then Return
            y = target.Bounds.Top
        Else
            Dim lastFolder = If(_folders.Count > 0, _folders(_folders.Count - 1), Nothing)
            If lastFolder Is Nothing Then Return
            For i As Integer = _rows.Count - 1 To 0 Step -1
                Dim row = _rows(i)
                If row.Folder Is lastFolder Then
                    target = row
                    Exit For
                End If
            Next
            If target Is Nothing Then Return
            y = target.Bounds.Bottom
        End If

        context.FillRectangle(New RectangleF(target.Bounds.Left,
                                             y - S(_dropIndicatorHeight) / 2.0F,
                                             target.Bounds.Width,
                                             S(_dropIndicatorHeight)),
                              _dropIndicatorColor)
    End Sub

    Private Sub DrawScrollBar(context As D3D_PaintContext)
        If _scrollBar.TrackRect.IsEmpty Then Return
        Dim width = Math.Max(1.0F, S(_scrollBarWidth))
        Dim track = New RectangleF(_scrollBar.VisualLeft, _scrollBar.TrackRect.Y, width, _scrollBar.TrackRect.Height)
        Dim thumb = New RectangleF(_scrollBar.VisualLeft, _scrollBar.ThumbRect.Y, width, _scrollBar.ThumbRect.Height)
        FillRounded(context, track, width / 2.0F, _scrollBarTrackColor)
        FillRounded(context, thumb, width / 2.0F,
                    If(_scrollBar.IsHover OrElse _scrollBar.IsDragging, _scrollBarThumbHoverColor, _scrollBarThumbColor))
    End Sub

    Private Shared Sub FillRounded(context As D3D_PaintContext, rect As RectangleF, radius As Single, color As Color)
        If color.A = 0 OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then Return
        If radius <= 0 Then
            context.FillRectangle(rect, color)
        Else
            context.FillRoundedRectangle(rect, radius, color)
        End If
    End Sub

    Private Function EffectiveFont(value As Font, fallback As Font) As Font
        Return If(value, If(fallback, Font))
    End Function
#End Region

#Region "鼠标交互"
    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        EnsureLayout()
        If _scrollBar.IsDragging Then
            _toolTip.Hide()
            Dim totalPixels = GetContentHeightPixels()
            Dim viewportPixels = GetViewportHeightPixels()
            _scrollOffset = _scrollBar.DragMove(e.Y, totalPixels, viewportPixels)
            _layoutDirty = True
            RequestRender()
            Return
        End If

        If (_dragDialogue IsNot Nothing OrElse
            (_pressedFolder IsNot Nothing AndAlso _pressedFolder IsNot _unassignedFolder)) AndAlso Not _isDragging Then
            Dim dragSize = SystemInformation.DragSize
            If Math.Abs(e.X - _dragStart.X) >= dragSize.Width OrElse Math.Abs(e.Y - _dragStart.Y) >= dragSize.Height Then
                _isDragging = True
                _dragFolder = _pressedFolder
                Capture = True
            End If
        End If
        If _isDragging Then
            _toolTip.Hide()
            UpdateDropTarget(e.Location)
            RequestRender()
            Return
        End If

        Dim row As LayoutRow = Nothing
        Dim button As AgentDialogueActionButton = Nothing
        HitTest(e.Location, row, button)
        If Not Object.ReferenceEquals(_hoverRow, row) OrElse Not Object.ReferenceEquals(_hoverButton, button) Then
            _hoverRow = row
            _hoverButton = button
            RequestRender()
        End If
        _toolTip.Update(e.Location)
        If _scrollBar.UpdateHover(e.Location) Then RequestRender()
    End Sub

    Private Function GetToolTipTarget(pointer As Point) As FloatingToolTipTarget
        If _isDragging OrElse _scrollBar.IsDragging Then Return Nothing
        EnsureLayout()
        Dim row As LayoutRow = Nothing
        Dim button As AgentDialogueActionButton = Nothing
        HitTest(pointer, row, button)
        If row Is Nothing Then Return Nothing
        Dim text = ""
        Dim bounds = Rectangle.Ceiling(row.Bounds)
        If button IsNot Nothing Then
            text = button.ToolTipText
            Dim index = row.ButtonItems.IndexOf(button)
            If index >= 0 Then bounds = Rectangle.Ceiling(row.ButtonBounds(index))
        Else
            text = If(row.Kind = RowKind.Folder, row.Folder.ToolTipText, row.Dialogue.ToolTipText)
        End If
        Return New FloatingToolTipTarget(text, bounds)
    End Function

    Private Function CreateToolTipStyle() As FloatingToolTipStyle
        Return New FloatingToolTipStyle With {.Font = Me.Font, .BackColor = _toolTipBackColor, .ForeColor = _toolTipForeColor}
    End Function

    Private Sub RaiseRightClick(e As MouseEventArgs)
        Dim row As LayoutRow = Nothing
        Dim button As AgentDialogueActionButton = Nothing
        HitTest(e.Location, row, button)
        If Not IsRowHovered(row) Then button = Nothing

        Dim targetKind = RightClickTargetKind.None
        Dim folder As AgentDialogueFolder = Nothing
        Dim dialogue As AgentDialogue = Nothing
        If row IsNot Nothing Then
            folder = row.Folder
            If row.Kind = RowKind.Folder Then
                targetKind = RightClickTargetKind.Folder
            Else
                targetKind = RightClickTargetKind.Dialogue
                dialogue = row.Dialogue
            End If
        End If

        Dim args As New AgentDialogueRightClickEventArgs(targetKind, folder, dialogue,
                                                         button, e.Location, e)
        RaiseEvent ItemRightClick(Me, args)
        RaiseEvent RightClick(Me, args)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        _toolTip.Leave()
        If Not _isDragging Then
            _hoverRow = Nothing
            _hoverButton = Nothing
            _scrollBar.ResetHover()
            RequestRender()
        End If
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        _toolTip.Hide()
        If e.Button <> MouseButtons.Left Then Return
        Focus()
        EnsureLayout()
        If _scrollBar.BeginDrag(e.Location, _scrollOffset) Then
            Capture = True
            Return
        End If
        If Not _scrollBar.TrackRect.IsEmpty Then
            Dim newOffset = _scrollBar.TrackClick(e.Location, _scrollOffset,
                                                   GetContentHeightPixels(), GetViewportHeightPixels())
            If newOffset <> _scrollOffset Then
                _scrollOffset = newOffset
                _layoutDirty = True
                RequestRender()
                Return
            End If
        End If
        Dim row As LayoutRow = Nothing
        Dim button As AgentDialogueActionButton = Nothing
        If Not HitTest(e.Location, row, button) Then Return
        If Not IsRowHovered(row) Then button = Nothing
        _pressedRow = row
        _pressedButton = button
        _pressedFolder = Nothing
        If button IsNot Nothing Then
            Capture = True
            Return
        End If
        If row.Kind = RowKind.Folder Then
            _pressedFolder = row.Folder
            _dragStart = e.Location
            Capture = True
            Return
        End If
        SetSelectedDialogue(row.Dialogue)
        _dragDialogue = row.Dialogue
        _dragSourceFolder = row.Folder
        _dragStart = e.Location
        Capture = True
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If e.Button = MouseButtons.Right Then
            RaiseRightClick(e)
            Return
        End If
        If _scrollBar.IsDragging Then
            _scrollBar.EndDrag()
            Capture = False
            RequestRender()
            Return
        End If
        If _isDragging Then
            If _dragFolder IsNot Nothing AndAlso _dropFolderIndex >= 0 Then
                Dim oldIndex = _folders.IndexOf(_dragFolder)
                Dim targetIndex = _dropFolderIndex
                If oldIndex >= 0 AndAlso oldIndex < targetIndex Then targetIndex -= 1
                MoveFolder(_dragFolder, targetIndex)
            ElseIf _dragDialogue IsNot Nothing AndAlso _dropFolder IsNot Nothing Then
                Dim targetIndex = _dropIndex
                If _dragSourceFolder Is _dropFolder Then
                    Dim sourceIndex = _dragSourceFolder.Dialogues.IndexOf(_dragDialogue)
                    If sourceIndex >= 0 AndAlso sourceIndex < targetIndex Then targetIndex -= 1
                End If
                MoveDialogue(_dragDialogue, _dropFolder, targetIndex)
            End If
            _isDragging = False
            _dragFolder = Nothing
            _pressedFolder = Nothing
            _dragDialogue = Nothing
            _dragSourceFolder = Nothing
            _dropFolder = Nothing
            _dropIndex = -1
            _dropFolderIndex = -1
            Capture = False
            RequestRender()
            Return
        End If
        If _pressedFolder IsNot Nothing Then
            Dim row As LayoutRow = Nothing
            Dim button As AgentDialogueActionButton = Nothing
            HitTest(e.Location, row, button)
            If row IsNot Nothing AndAlso row.Kind = RowKind.Folder AndAlso row.Folder Is _pressedFolder Then
                _selectedFolder = _pressedFolder
                _pressedFolder.IsExpanded = Not _pressedFolder.IsExpanded
                RaiseEvent FolderSelected(Me, New AgentDialogueEventArgs(Nothing, _pressedFolder, -1))
            End If
            _pressedFolder = Nothing
            _pressedRow = Nothing
            Capture = False
            Return
        End If
        If _pressedButton IsNot Nothing Then
            Dim row As LayoutRow = Nothing
            Dim button As AgentDialogueActionButton = Nothing
            HitTest(e.Location, row, button)
            If Object.ReferenceEquals(_pressedButton, button) AndAlso _pressedButton.Enabled Then
                If row IsNot Nothing AndAlso row.Kind = RowKind.Folder Then
                    _pressedButton.PerformClick(row.Folder, Nothing)
                    RaiseEvent FolderActionButtonClick(Me, New AgentDialogueActionButtonEventArgs(Me, row.Folder, Nothing, _pressedButton))
                ElseIf row IsNot Nothing Then
                    _pressedButton.PerformClick(row.Folder, row.Dialogue)
                    RaiseEvent ActionButtonClick(Me, New AgentDialogueActionButtonEventArgs(Me, row.Folder, row.Dialogue, _pressedButton))
                End If
            End If
        End If
        _pressedButton = Nothing
        _pressedRow = Nothing
        _dragDialogue = Nothing
        _pressedFolder = Nothing
        Capture = False
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        _toolTip.Hide()
        EnsureLayout()
        Dim maxOffset = Math.Max(0, GetContentHeightPixels() - GetViewportHeightPixels())
        If maxOffset <= 0 Then Return
        Dim stepSize = Math.Max(1, CInt(S(_dialogueItemHeight) / 2.0F))
        _scrollOffset = Math.Max(0, Math.Min(maxOffset, _scrollOffset - Math.Sign(e.Delta) * stepSize))
        _layoutDirty = True
        RequestRender()
    End Sub

    Private Function GetContentHeightPixels() As Integer
        EnsureLayout()
        If _rows.Count = 0 Then Return 0
        Dim last = _rows(_rows.Count - 1)
        Return CInt(Math.Ceiling(last.Bounds.Bottom + _scrollOffset - Padding.Top))
    End Function

    Private Function GetViewportHeightPixels() As Integer
        Return Math.Max(0, Height - Padding.Vertical)
    End Function
#End Region

#Region "控件属性与生命周期"
    <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SuperSamplingScale As GlobalOptions.SuperSamplingScaleEnum Implements D3D_ISuperSamplingSource.SuperSamplingScale
        Get
            Return _superSamplingScale
        End Get
        Set(value As GlobalOptions.SuperSamplingScaleEnum)
            If _superSamplingScale = value Then Return
            _superSamplingScale = value
            RequestRender()
        End Set
    End Property

    <Browsable(True), Category("LakeUI"), Description("用于超容器背景映射的 GPU 控件源。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property BackgroundSource As Control
        Get
            Return _backgroundSource
        End Get
        Set(value As Control)
            If _backgroundSource Is value Then Return
            _backgroundSource = D3D_BackgroundPenetration.SetBackgroundSource(Me, _backgroundSource, value)
            RequestRender()
        End Set
    End Property

    Public Function TryGetBackgroundSource(ByRef source As Control) As Boolean Implements D3D_IBackgroundSourceProvider.TryGetBackgroundSource
        source = _backgroundSource
        Return source IsNot Nothing
    End Function

    <Category("LakeUI"), Description("主体填充颜色，支持在背景采样上叠加半透明颜色。"), DefaultValue(GetType(Color), "32, 32, 32")>
    Public Property BackColor1 As Color
        Get
            Return _backColor1
        End Get
        Set(value As Color)
            _backColor1 = value
            RequestRender()
        End Set
    End Property

    <Category("LakeUI"), Description("选中对话背景颜色。"), DefaultValue(GetType(Color), "48, 48, 48")>
    Public Property SelectedDialogueBackColor As Color
        Get
            Return _selectedDialogueBackColor
        End Get
        Set(value As Color)
            _selectedDialogueBackColor = value
            RequestRender()
        End Set
    End Property

    <Category("LakeUI"), Description("鼠标悬停项背景颜色。"), DefaultValue(GetType(Color), "48, 48, 48")>
    Public Property HoverBackColor As Color
        Get
            Return _hoverBackColor
        End Get
        Set(value As Color)
            _hoverBackColor = value
            RequestRender()
        End Set
    End Property

    <Category("LakeUI"), Description("文件夹背景颜色。"), DefaultValue(GetType(Color), "Transparent")>
    Public Property FolderBackColor As Color
        Get
            Return _folderBackColor
        End Get
        Set(value As Color)
            _folderBackColor = value
            RequestRender()
        End Set
    End Property

    <Category("LakeUI"), Description("对话文字颜色。"), DefaultValue(GetType(Color), "Silver")>
    Public Property DialogueForeColor As Color
        Get
            Return _dialogueForeColor
        End Get
        Set(value As Color)
            _dialogueForeColor = value
            RequestRender()
        End Set
    End Property

    <Category("LakeUI"), Description("选中对话文字颜色。"), DefaultValue(GetType(Color), "White")>
    Public Property SelectedDialogueForeColor As Color
        Get
            Return _selectedDialogueForeColor
        End Get
        Set(value As Color)
            _selectedDialogueForeColor = value
            RequestRender()
        End Set
    End Property

    <Category("LakeUI"), Description("文件夹文字颜色。"), DefaultValue(GetType(Color), "Gainsboro")>
    Public Property FolderForeColor As Color
        Get
            Return _folderForeColor
        End Get
        Set(value As Color)
            _folderForeColor = value
            RequestRender()
        End Set
    End Property

    <Category("LakeUI"), Description("副标题文字颜色。"), DefaultValue(GetType(Color), "150, 150, 150")>
    Public Property SubtitleForeColor As Color
        Get
            Return _subtitleForeColor
        End Get
        Set(value As Color)
            _subtitleForeColor = value
            RequestRender()
        End Set
    End Property

    <Category("LakeUI"), Description("按钮默认背景颜色。"), DefaultValue(GetType(Color), "Transparent")>
    Public Property ActionButtonBackColor As Color
        Get
            Return _buttonBackColor
        End Get
        Set(value As Color)
            _buttonBackColor = value
            RequestRender()
        End Set
    End Property

    <Category("LakeUI"), Description("按钮悬停背景颜色。"), DefaultValue(GetType(Color), "70, 70, 70")>
    Public Property ActionButtonHoverBackColor As Color
        Get
            Return _buttonHoverBackColor
        End Get
        Set(value As Color)
            _buttonHoverBackColor = value
            RequestRender()
        End Set
    End Property

    <Category("LakeUI"), Description("拖放插入指示线颜色。"), DefaultValue(GetType(Color), "DodgerBlue")>
    Public Property DropIndicatorColor As Color
        Get
            Return _dropIndicatorColor
        End Get
        Set(value As Color)
            _dropIndicatorColor = value
            RequestRender()
        End Set
    End Property

    <Category("LakeUI"), Description("控件外边框颜色，为 Empty 时不绘制。"), DefaultValue(GetType(Color), "")>
    Public Property BorderColor As Color
        Get
            Return _borderColor
        End Get
        Set(value As Color)
            _borderColor = value
            RequestRender()
        End Set
    End Property

    <Category("LakeUI"), Description("控件外边框宽度。"), DefaultValue(0)>
    Public Property BorderWidth As Integer
        Get
            Return _borderWidth
        End Get
        Set(value As Integer)
            _borderWidth = Math.Max(0, value)
            RequestRender()
        End Set
    End Property

    <Category("LakeUI"), Description("文件夹和对话的默认字体。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property FolderFont As Font
        Get
            Return _folderFont
        End Get
        Set(value As Font)
            _folderFont = value
            InvalidateFontResources()
            RequestRender()
        End Set
    End Property

    <Category("LakeUI"), Description("对话标题默认字体。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property DialogueFont As Font
        Get
            Return _dialogueFont
        End Get
        Set(value As Font)
            _dialogueFont = value
            InvalidateFontResources()
            RequestRender()
        End Set
    End Property

    <Category("LakeUI"), Description("副标题默认字体。"),
     DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SubtitleFont As Font
        Get
            Return _subtitleFont
        End Get
        Set(value As Font)
            _subtitleFont = value
            InvalidateFontResources()
            RequestRender()
        End Set
    End Property

    <Category("LakeUI - Layout"), Description("文件夹项高度。"), DefaultValue(34)>
    Public Property FolderItemHeight As Integer
        Get
            Return _folderItemHeight
        End Get
        Set(value As Integer)
            _folderItemHeight = Math.Max(16, value)
            LayoutChanged()
        End Set
    End Property

    <Category("LakeUI - Layout"), Description("文件夹项左侧折叠图标的尺寸。"), DefaultValue(12)>
    Public Property FolderCollapseIconSize As Integer
        Get
            Return _folderCollapseIconSize
        End Get
        Set(value As Integer)
            value = Math.Max(4, value)
            If _folderCollapseIconSize = value Then Return
            _folderCollapseIconSize = value
            RequestRender()
        End Set
    End Property

    <Category("LakeUI - Layout"), Description("对话项高度。"), DefaultValue(42)>
    Public Property DialogueItemHeight As Integer
        Get
            Return _dialogueItemHeight
        End Get
        Set(value As Integer)
            _dialogueItemHeight = Math.Max(18, value)
            LayoutChanged()
        End Set
    End Property

    <Category("LakeUI - Layout"), Description("列表项之间的间距。"), DefaultValue(2)>
    Public Property ItemSpacing As Integer
        Get
            Return _itemSpacing
        End Get
        Set(value As Integer)
            _itemSpacing = Math.Max(0, value)
            LayoutChanged()
        End Set
    End Property

    <Category("LakeUI - Layout"), Description("对话相对于文件夹的缩进。"), DefaultValue(18)>
    Public Property DialogueIndent As Integer
        Get
            Return _dialogueIndent
        End Get
        Set(value As Integer)
            _dialogueIndent = Math.Max(0, value)
            LayoutChanged()
        End Set
    End Property

    <Category("LakeUI - Layout"), Description("图标尺寸。"), DefaultValue(20)>
    Public Property IconSize As Integer
        Get
            Return _iconSize
        End Get
        Set(value As Integer)
            _iconSize = Math.Max(0, value)
            LayoutChanged()
        End Set
    End Property

    <Category("LakeUI - Layout"), Description("图标与文本间距。"), DefaultValue(8)>
    Public Property IconTextSpacing As Integer
        Get
            Return _iconTextSpacing
        End Get
        Set(value As Integer)
            _iconTextSpacing = Math.Max(0, value)
            RequestRender()
        End Set
    End Property

    <Category("LakeUI - Layout"), Description("操作按钮尺寸。"), DefaultValue(24)>
    Public Property ActionButtonSize As Integer
        Get
            Return _actionButtonSize
        End Get
        Set(value As Integer)
            _actionButtonSize = Math.Max(12, value)
            LayoutChanged()
        End Set
    End Property

    <Category("LakeUI - Layout"), Description("操作按钮之间的间距。"), DefaultValue(2)>
    Public Property ActionButtonSpacing As Integer
        Get
            Return _actionButtonSpacing
        End Get
        Set(value As Integer)
            _actionButtonSpacing = Math.Max(0, value)
            LayoutChanged()
        End Set
    End Property

    <Category("LakeUI - Layout"), Description("文件夹和对话项文本区域的左右内边距。"), DefaultValue(8)>
    Public Property ItemTextPadding As Integer
        Get
            Return _itemTextPadding
        End Get
        Set(value As Integer)
            _itemTextPadding = Math.Max(0, value)
            RequestRender()
        End Set
    End Property

    <Category("LakeUI - ToolTip"), Description("按钮和项工具提示背景颜色。"), DefaultValue(GetType(Color), "50, 50, 50")>
    Public Property ToolTipBackColor As Color
        Get
            Return _toolTipBackColor
        End Get
        Set(value As Color)
            _toolTipBackColor = value
            _toolTip?.RefreshStyle()
        End Set
    End Property

    <Category("LakeUI - ToolTip"), Description("按钮和项工具提示文字颜色。"), DefaultValue(GetType(Color), "Silver")>
    Public Property ToolTipForeColor As Color
        Get
            Return _toolTipForeColor
        End Get
        Set(value As Color)
            _toolTipForeColor = value
            _toolTip?.RefreshStyle()
        End Set
    End Property

    <Category("LakeUI - ToolTip"), Description("工具提示初始延迟（毫秒）。"), DefaultValue(500)>
    Public Property ToolTipInitialDelay As Integer
        Get
            Return _toolTip.InitialDelay
        End Get
        Set(value As Integer)
            If _toolTip IsNot Nothing Then _toolTip.InitialDelay = Math.Max(0, value)
        End Set
    End Property

    <Category("LakeUI - ToolTip"), Description("工具提示自动关闭延迟（毫秒）。"), DefaultValue(10000)>
    Public Property ToolTipAutoPopDelay As Integer
        Get
            Return _toolTip.AutoPopDelay
        End Get
        Set(value As Integer)
            If _toolTip IsNot Nothing Then _toolTip.AutoPopDelay = Math.Max(1, value)
        End Set
    End Property

    <Category("LakeUI - Layout"), Description("项目圆角半径。"), DefaultValue(6)>
    Public Property CornerRadius As Integer
        Get
            Return _cornerRadius
        End Get
        Set(value As Integer)
            _cornerRadius = Math.Max(0, value)
            RequestRender()
        End Set
    End Property

    <Category("LakeUI - ScrollBar"), Description("滚动条宽度。"), DefaultValue(6)>
    Public Property ScrollBarWidth As Integer
        Get
            Return _scrollBarWidth
        End Get
        Set(value As Integer)
            _scrollBarWidth = Math.Max(0, value)
            LayoutChanged()
        End Set
    End Property

    <Category("LakeUI - ScrollBar"), Description("滚动条轨道颜色。"), DefaultValue(GetType(Color), "24, 24, 24")>
    Public Property ScrollBarTrackColor As Color
        Get
            Return _scrollBarTrackColor
        End Get
        Set(value As Color)
            _scrollBarTrackColor = value
            RequestRender()
        End Set
    End Property

    <Category("LakeUI - ScrollBar"), Description("滚动条滑块颜色。"), DefaultValue(GetType(Color), "88, 88, 88")>
    Public Property ScrollBarThumbColor As Color
        Get
            Return _scrollBarThumbColor
        End Get
        Set(value As Color)
            _scrollBarThumbColor = value
            RequestRender()
        End Set
    End Property

    <Category("LakeUI - ScrollBar"), Description("滚动条滑块悬停颜色。"), DefaultValue(GetType(Color), "135, 135, 135")>
    Public Property ScrollBarThumbHoverColor As Color
        Get
            Return _scrollBarThumbHoverColor
        End Get
        Set(value As Color)
            _scrollBarThumbHoverColor = value
            RequestRender()
        End Set
    End Property

    <Category("LakeUI"), Description("拖放插入线高度。"), DefaultValue(2)>
    Public Property DropIndicatorHeight As Integer
        Get
            Return _dropIndicatorHeight
        End Get
        Set(value As Integer)
            _dropIndicatorHeight = Math.Max(1, value)
            RequestRender()
        End Set
    End Property

    <Category("LakeUI - Agent"), Description("进行中对话左侧 Win11 ProgressRing 的颜色。"), DefaultValue(GetType(Color), "0, 120, 215")>
    Public Property ProgressRingColor As Color
        Get
            Return _progressRingColor
        End Get
        Set(value As Color)
            _progressRingColor = value
            RequestRender()
        End Set
    End Property

    <Category("LakeUI - Agent"), Description("进行中对话左侧 ProgressRing 的尺寸。"), DefaultValue(18)>
    Public Property ProgressRingSize As Integer
        Get
            Return _progressRingSize
        End Get
        Set(value As Integer)
            _progressRingSize = Math.Max(8, value)
            LayoutChanged()
        End Set
    End Property

    <Category("LakeUI - Agent"), Description("进行中对话左侧 ProgressRing 的弧线厚度。"), DefaultValue(3.0F)>
    Public Property ProgressRingThickness As Single
        Get
            Return _progressRingThickness
        End Get
        Set(value As Single)
            _progressRingThickness = Math.Max(1.0F, value)
            RequestRender()
        End Set
    End Property

    <Category("LakeUI - Agent"), Description("进行中对话 ProgressRing 的动画周期（毫秒）。"), DefaultValue(1500)>
    Public Property ProgressRingAnimationDuration As Integer
        Get
            Return _progressRingAnimationDuration
        End Get
        Set(value As Integer)
            _progressRingAnimationDuration = Math.Max(100, value)
            RequestRender()
        End Set
    End Property

    <Category("LakeUI - Agent"), Description("进行中对话 ProgressRing 的动画帧率。"), DefaultValue(60)>
    Public Property ProgressRingAnimationFPS As Integer
        Get
            Return _progressRingAnimationFPS
        End Get
        Set(value As Integer)
            _progressRingAnimationFPS = Math.Max(0, value)
            If _progressAnimationRunning Then _progressAnimation.FPS = _progressRingAnimationFPS
        End Set
    End Property

    Private Sub LayoutChanged()
        _toolTip?.Hide()
        _layoutDirty = True
        RequestRender()
    End Sub

    Private Sub InvalidateFontResources()
        D3D_RenderCore.InvalidateExistingTextResources(Me)
        _layoutDirty = True
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        InvalidateFontResources()
        RequestRender()
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        LayoutChanged()
    End Sub

    Protected Overrides ReadOnly Property DefaultPadding As Padding
        Get
            Return New Padding(8)
        End Get
    End Property

    Protected Overrides Sub OnPaddingChanged(e As EventArgs)
        MyBase.OnPaddingChanged(e)
        LayoutChanged()
    End Sub

    Protected Overrides Sub OnLayout(e As LayoutEventArgs)
        MyBase.OnLayout(e)
        LayoutChanged()
    End Sub

    Protected Overrides Sub OnBackColorChanged(e As EventArgs)
        MyBase.OnBackColorChanged(e)
        RequestRender()
    End Sub

    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        InvalidateFontResources()
        LayoutChanged()
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        UpdateProgressAnimationState()
    End Sub

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        _progressAnimation.StopFrameLoop()
        _progressAnimationRunning = False
        _progressStopwatch.Stop()
        Try : D3D_BackgroundPenetration.UnregisterConsumer(Me) : Catch : End Try
        MyBase.OnHandleDestroyed(e)
    End Sub

    Protected Overrides Sub OnVisibleChanged(e As EventArgs)
        MyBase.OnVisibleChanged(e)
        If Not Visible Then
            Try : D3D_BackgroundPenetration.UnregisterConsumer(Me) : Catch : End Try
        End If
        UpdateProgressAnimationState()
    End Sub

    Protected Overrides Sub OnParentChanged(e As EventArgs)
        MyBase.OnParentChanged(e)
        If Parent Is Nothing Then
            Try : D3D_BackgroundPenetration.UnregisterConsumer(Me) : Catch : End Try
        End If
        UpdateProgressAnimationState()
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        UpdateProgressAnimationState()
        RequestRender()
    End Sub

#End Region
End Class
