''' <summary>
''' V3_MigrationAnnotations 提供后续控件迁移诊断标记。
''' 这些类型不持有 GPU 对象，不改变运行时行为，只帮助标记哪些控件可迁移、阻塞原因和迁移批次。
''' </summary>
Friend Module V3_MigrationAnnotations
End Module

<AttributeUsage(AttributeTargets.Class, AllowMultiple:=False, Inherited:=True)>
Friend NotInheritable Class V3_GpuMigrationCandidateAttribute
    Inherits Attribute

    Public Sub New(Optional note As String = Nothing)
        Me.Note = note
    End Sub

    Public ReadOnly Property Note As String
End Class

<AttributeUsage(AttributeTargets.Class, AllowMultiple:=True, Inherited:=True)>
Friend NotInheritable Class V3_GpuMigrationBlockedAttribute
    Inherits Attribute

    Public Sub New(reason As String)
        Me.Reason = reason
    End Sub

    Public ReadOnly Property Reason As String
End Class
