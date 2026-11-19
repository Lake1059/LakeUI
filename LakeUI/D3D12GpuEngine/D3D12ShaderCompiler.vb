Friend Module D3D12ShaderCompiler
    Private ReadOnly blobs As New Dictionary(Of String, Byte())()

    Friend Function Load(name As String) As Byte()
        SyncLock blobs
            Dim bytes As Byte() = Nothing
            If blobs.TryGetValue(name, bytes) Then Return bytes
            Using stream = GetType(D3D12_ImagePipeline).Assembly.GetManifestResourceStream("LakeUI.DX12." & name & ".cso")
                If stream Is Nothing Then Throw New InvalidOperationException("Missing built-in shader " & name)
                Using memory As New IO.MemoryStream()
                    stream.CopyTo(memory)
                    bytes = memory.ToArray()
                End Using
            End Using
            blobs.Add(name, bytes)
            Return bytes
        End SyncLock
    End Function
End Module
