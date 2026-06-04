Imports System.Globalization
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading

''' <summary>
''' 通用 SHA-256 前导零 Proof-of-Work 求解器。
''' </summary>
Public Class Sha256ProofOfWork

    Public Shared Function GetDefaultWorkerCount() As Integer
        Dim processorCount As Integer

        Try
            processorCount = Environment.ProcessorCount
        Catch
            processorCount = 0
        End Try

        If processorCount <= 0 Then Return 4
        Return Math.Max(1, processorCount \ 2)
    End Function

    Public Shared Function Solve(canonicalFactory As Func(Of ULong, String),
                                 leadingZeroBits As Integer,
                                 Optional workerCount As Integer = 0,
                                 Optional cancellationToken As CancellationToken = Nothing) As String
        Return SolveNonce(canonicalFactory, leadingZeroBits, workerCount, cancellationToken).ToString(CultureInfo.InvariantCulture)
    End Function

    Public Shared Function SolveNonce(canonicalFactory As Func(Of ULong, String),
                                      leadingZeroBits As Integer,
                                      Optional workerCount As Integer = 0,
                                      Optional cancellationToken As CancellationToken = Nothing) As ULong
        ArgumentNullException.ThrowIfNull(canonicalFactory)
        If leadingZeroBits < 0 OrElse leadingZeroBits > 256 Then Throw New ArgumentOutOfRangeException(NameOf(leadingZeroBits), "leadingZeroBits 必须在 0 到 256 之间。")

        Dim workers As Integer = If(workerCount > 0, workerCount, GetDefaultWorkerCount())
        workers = Math.Max(1, workers)

        Using linkedCts As CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            Dim result As ULong = 0
            Dim found As Integer = 0
            Dim tasks As New List(Of Task)()

            For workerIndex As Integer = 0 To workers - 1
                Dim startNonce As ULong = CULng(workerIndex)
                tasks.Add(Task.Run(Sub()
                                       Dim nonce As ULong = startNonce

                                       Do While Volatile.Read(found) = 0
                                           linkedCts.Token.ThrowIfCancellationRequested()

                                           Dim canonical As String = canonicalFactory(nonce)
                                           Dim digest As Byte() = SHA256.HashData(Encoding.UTF8.GetBytes(canonical))

                                           If HasLeadingZeroBits(digest, leadingZeroBits) Then
                                               If Interlocked.CompareExchange(found, 1, 0) = 0 Then
                                                   result = nonce
                                                   linkedCts.Cancel()
                                               End If

                                               Exit Do
                                           End If

                                           nonce += CULng(workers)
                                       Loop
                                   End Sub, linkedCts.Token))
            Next

            Try
                Task.WaitAll(tasks.ToArray(), cancellationToken)
            Catch ex As AggregateException
                ex.Handle(Function(inner)
                              Return TypeOf inner Is OperationCanceledException AndAlso Volatile.Read(found) <> 0
                          End Function)
            Catch ex As OperationCanceledException When Volatile.Read(found) <> 0
            End Try

            cancellationToken.ThrowIfCancellationRequested()
            If Volatile.Read(found) = 0 Then Throw New OperationCanceledException("PoW 求解已取消。", cancellationToken)

            Return result
        End Using
    End Function

    Public Shared Function HasLeadingZeroBits(digest As Byte(), leadingZeroBits As Integer) As Boolean
        ArgumentNullException.ThrowIfNull(digest)
        If leadingZeroBits < 0 OrElse leadingZeroBits > digest.Length * 8 Then Return False

        Dim fullZeroBytes As Integer = leadingZeroBits \ 8
        Dim remainingZeroBits As Integer = leadingZeroBits Mod 8

        For index As Integer = 0 To fullZeroBytes - 1
            If digest(index) <> 0 Then Return False
        Next

        If remainingZeroBits = 0 Then Return True
        If fullZeroBytes >= digest.Length Then Return False

        Dim mask As Integer = &HFF << (8 - remainingZeroBits)
        Return (digest(fullZeroBytes) And mask) = 0
    End Function

End Class
