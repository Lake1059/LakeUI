Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Threading.Tasks
Imports Vortice.Direct2D1
Imports Vortice.Mathematics

Public Structure PixelPictureTileRequest
    Implements IEquatable(Of PixelPictureTileRequest)

    Public Sub New(level As Integer, column As Long, row As Long, frameIndex As Integer, tileSize As Integer)
        Me.Level = level
        Me.Column = column
        Me.Row = row
        Me.FrameIndex = frameIndex
        Me.TileSize = tileSize
    End Sub

    ''' <summary>获取请求的金字塔层级；0 为原始分辨率，每升一级单个分块像素覆盖的源像素边长翻倍。</summary>
    Public ReadOnly Property Level As Integer
    ''' <summary>获取所请求金字塔层中分块的零基列索引。</summary>
    Public ReadOnly Property Column As Long
    ''' <summary>获取所请求金字塔层中分块的零基行索引。</summary>
    Public ReadOnly Property Row As Long
    ''' <summary>获取当前动图帧的零基索引；没有源或源为静态图片时为 0。</summary>
    Public ReadOnly Property FrameIndex As Integer
    ''' <summary>获取单个分块在所请求金字塔层中的最大像素边长；图像边缘分块可以更小。</summary>
    Public ReadOnly Property TileSize As Integer
    ''' <summary>获取所请求层每个像素覆盖的原始图像像素边长，等于 2 的 Level 次方。</summary>
    Public ReadOnly Property Scale As Double
        Get
            Return Math.Pow(2, Level)
        End Get
    End Property
    ''' <summary>获取分块覆盖的原始图像区域；边缘区域可能超出图像，提供方应按图像边界裁剪。</summary>
    Public ReadOnly Property SourceRegion As PixelPictureRegion
        Get
            Return New PixelPictureRegion(Column * TileSize * Scale, Row * TileSize * Scale, TileSize * Scale, TileSize * Scale)
        End Get
    End Property

    Public Overloads Function Equals(other As PixelPictureTileRequest) As Boolean Implements IEquatable(Of PixelPictureTileRequest).Equals
        Return Level = other.Level AndAlso Column = other.Column AndAlso Row = other.Row AndAlso FrameIndex = other.FrameIndex AndAlso TileSize = other.TileSize
    End Function
    Public Overrides Function Equals(obj As Object) As Boolean
        Return TypeOf obj Is PixelPictureTileRequest AndAlso Equals(DirectCast(obj, PixelPictureTileRequest))
    End Function
    Public Overrides Function GetHashCode() As Integer
        Return HashCode.Combine(Level, Column, Row, FrameIndex, TileSize)
    End Function
End Structure

''' <summary>
''' 已解码 tile 的提供器在后台执行；必须支持从 0 到 levelCount-1 的二倍金字塔。
''' 返回完整合成帧而非 GIF disposal/delta 数据。此类只管理呈现、色彩解释及缓存，不读取编码文件。
''' </summary>
Public NotInheritable Class PixelPictureTiledSource
    Implements IPixelPictureSource, IPixelPictureInvalidationSource, IDisposable

    Public Delegate Function TileProvider(request As PixelPictureTileRequest, cancellationToken As CancellationToken) As Task(Of PixelPictureTile)

    Private NotInheritable Class TileEntry
        Public ReadOnly Tile As PixelPictureTile
        Public ReadOnly Upload As Byte()
        Public ReadOnly GpuKey As New Object()
        Public LastUse As Long
        Public Sub New(tile As PixelPictureTile, upload As Byte(), lastUse As Long)
            Me.Tile = tile
            Me.Upload = upload
            Me.LastUse = lastUse
        End Sub
        Public ReadOnly Property Bytes As Long
            Get
                Return CLng(Tile.Width) * Tile.Height * 24L
            End Get
        End Property
    End Class

    Private ReadOnly _gate As New Object()
    Private ReadOnly _cache As New Dictionary(Of PixelPictureTileRequest, TileEntry)()
    Private ReadOnly _pending As New Dictionary(Of PixelPictureTileRequest, CancellationTokenSource)()
    Private ReadOnly _failed As New HashSet(Of PixelPictureTileRequest)()
    Private _wanted As New HashSet(Of PixelPictureTileRequest)()
    Private ReadOnly _provider As TileProvider
    Private ReadOnly _encoding As PixelPictureColorEncoding
    Private ReadOnly _delays As Integer()
    Private ReadOnly _width As Long
    Private ReadOnly _height As Long
    Private ReadOnly _tileSize As Integer
    Private ReadOnly _levelCount As Integer
    Private ReadOnly _cacheBudget As Long
    Private ReadOnly _loopCount As Integer
    Private _cacheBytes As Long
    Private _clock As Long
    Private _revision As Integer
    Private _disposed As Boolean
    Private _lastError As Exception
    Private _sampleRequest As PixelPictureTileRequest?

    Public Event ContentChanged As EventHandler Implements IPixelPictureInvalidationSource.ContentChanged

    Public Sub New(width As Long, height As Long, provider As TileProvider,
                   Optional encoding As PixelPictureColorEncoding = Nothing,
                   Optional tileSize As Integer = 256,
                   Optional frameDelaysMilliseconds As IEnumerable(Of Integer) = Nothing,
                   Optional loopCount As Integer = 0,
                   Optional cpuCacheBudgetBytes As Long = 64L * 1024 * 1024)
        If width <= 0 OrElse width > (1L << 40) Then Throw New ArgumentOutOfRangeException(NameOf(width))
        If height <= 0 OrElse height > (1L << 40) Then Throw New ArgumentOutOfRangeException(NameOf(height))
        If tileSize < 32 OrElse tileSize > 1024 OrElse (tileSize And (tileSize - 1)) <> 0 Then Throw New ArgumentOutOfRangeException(NameOf(tileSize))
        If cpuCacheBudgetBytes < CLng(tileSize) * tileSize * 24L * 5L Then Throw New ArgumentOutOfRangeException(NameOf(cpuCacheBudgetBytes))
        ArgumentNullException.ThrowIfNull(provider)
        _width = width
        _height = height
        _tileSize = tileSize
        _provider = provider
        _encoding = If(encoding, New PixelPictureColorEncoding())
        _cacheBudget = cpuCacheBudgetBytes
        _loopCount = Math.Max(0, loopCount)
        _levelCount = 1 + CInt(Math.Max(0, Math.Ceiling(Math.Log2(Math.Max(width, height) / CDbl(tileSize)))))
        Dim delays = If(frameDelaysMilliseconds Is Nothing, New List(Of Integer) From {100}, New List(Of Integer)(frameDelaysMilliseconds))
        If delays.Count = 0 Then Throw New ArgumentException("至少需要一帧。", NameOf(frameDelaysMilliseconds))
        _delays = delays.Select(Function(value) Math.Max(1, value)).ToArray()
    End Sub

    ''' <summary>获取原始图像或区域的宽度，单位为原始像素。</summary>
    Public ReadOnly Property Width As Long Implements IPixelPictureSource.Width
        Get
            Return _width
        End Get
    End Property
    ''' <summary>获取原始图像或区域的高度，单位为原始像素。</summary>
    Public ReadOnly Property Height As Long Implements IPixelPictureSource.Height
        Get
            Return _height
        End Get
    End Property
    ''' <summary>获取源包含的可呈现帧数；1 表示静态图像。</summary>
    Public ReadOnly Property FrameCount As Integer Implements IPixelPictureSource.FrameCount
        Get
            Return _delays.Length
        End Get
    End Property
    ''' <summary>获取源 RGB 数值的色域声明；色域与动态范围独立，scRGB 和宽色域本身不代表 HDR。</summary>
    Public ReadOnly Property ColorSpace As PixelPictureColorSpace Implements IPixelPictureSource.ColorSpace
        Get
            Return _encoding.ColorSpace
        End Get
    End Property
    ''' <summary>获取源 RGB 分量的传递函数，用于区分线性、sRGB、Gamma、PQ 和 HLG 数值。</summary>
    Public ReadOnly Property TransferFunction As PixelPictureTransferFunction Implements IPixelPictureSource.TransferFunction
        Get
            Return _encoding.TransferFunction
        End Get
    End Property
    ''' <summary>获取源提供方声明的原始像素格式；不等于最终 GPU 合成表面的格式。</summary>
    Public ReadOnly Property PixelFormat As PixelPicturePixelFormat Implements IPixelPictureSource.PixelFormat
        Get
            Return PixelPicturePixelFormat.Rgba32Float
        End Get
    End Property
    ''' <summary>获取源数据是否声明为 HDR；PQ/HLG 为 HDR，线性数据由提供方显式声明，不依据浮点格式或宽色域推断。</summary>
    Public ReadOnly Property IsHdr As Boolean Implements IPixelPictureSource.IsHdr
        Get
            Return _encoding.IsHdr
        End Get
    End Property
    ''' <summary>获取动图总播放次数；0 表示无限循环，正值表示完成相应次数后停在末帧。</summary>
    Public ReadOnly Property LoopCount As Integer Implements IPixelPictureSource.LoopCount
        Get
            Return _loopCount
        End Get
    End Property
    ''' <summary>获取图像金字塔层数，包括原始分辨率层与最低分辨率概览层。</summary>
    Public ReadOnly Property LevelCount As Integer
        Get
            Return _levelCount
        End Get
    End Property
    ''' <summary>获取已缓存的原始与预处理分块数据总字节数；不包括 GPU 缓存。</summary>
    Public ReadOnly Property CpuCacheBytes As Long
        Get
            SyncLock _gate
                Return _cacheBytes
            End SyncLock
        End Get
    End Property
    ''' <summary>获取当前尚未完成的异步分块请求数。</summary>
    Public ReadOnly Property PendingTileCount As Integer
        Get
            SyncLock _gate
                Return _pending.Count
            End SyncLock
        End Get
    End Property
    ''' <summary>获取最近一次分块请求失败的异常；没有错误时为 Nothing。</summary>
    Public ReadOnly Property LastError As Exception
        Get
            SyncLock _gate
                Return _lastError
            End SyncLock
        End Get
    End Property

    Public Function GetFrameDelayMilliseconds(frameIndex As Integer) As Integer Implements IPixelPictureSource.GetFrameDelayMilliseconds
        Return _delays(Math.Clamp(frameIndex, 0, _delays.Length - 1))
    End Function

    Public Function GetVisibleTileRequests(source As PixelPictureRegion, destination As RectangleF, frameIndex As Integer) As IReadOnlyList(Of PixelPictureTileRequest)
        If source.Width <= 0 OrElse source.Height <= 0 OrElse destination.Width <= 0 OrElse destination.Height <= 0 Then Return Array.Empty(Of PixelPictureTileRequest)()
        frameIndex = Math.Clamp(frameIndex, 0, FrameCount - 1)
        Dim ratio = Math.Max(source.Width / destination.Width, source.Height / destination.Height)
        Dim level = Math.Clamp(CInt(Math.Max(0, Math.Floor(Math.Log2(Math.Max(1, ratio))))), 0, _levelCount - 1)
        Dim requests As List(Of PixelPictureTileRequest)
        Do
            requests = RequestsAtLevel(source, frameIndex, level)
            If requests IsNot Nothing Then Exit Do
            If level >= _levelCount - 1 Then
                Return New List(Of PixelPictureTileRequest) From {
                    New PixelPictureTileRequest(level, 0, 0, frameIndex, _tileSize)
                }
            End If
            level += 1
        Loop
        Return requests.AsReadOnly()
    End Function

    Private Function RequestsAtLevel(region As PixelPictureRegion, frameIndex As Integer, level As Integer) As List(Of PixelPictureTileRequest)
        Dim result As New List(Of PixelPictureTileRequest)()
        Dim edge = _tileSize * Math.Pow(2, level)
        Dim left = Math.Max(0, region.X)
        Dim top = Math.Max(0, region.Y)
        Dim right = Math.Min(_width, region.Right)
        Dim bottom = Math.Min(_height, region.Bottom)
        If right <= left OrElse bottom <= top Then Return result
        Dim startColumn = CLng(Math.Floor(left / edge))
        Dim startRow = CLng(Math.Floor(top / edge))
        Dim endColumn = CLng(Math.Ceiling(right / edge)) - 1
        Dim endRow = CLng(Math.Ceiling(bottom / edge)) - 1
        If (endColumn - startColumn + 1) * CDbl(endRow - startRow + 1) > 128 Then
            Return Nothing
        End If
        For row = startRow To endRow
            For column = startColumn To endColumn
                result.Add(New PixelPictureTileRequest(level, column, row, frameIndex, _tileSize))
            Next
        Next
        Return result
    End Function

    Public Function Present(context As D3D_PaintContext, destination As RectangleF, source As PixelPictureRegion, frameIndex As Integer,
                            interpolation As InterpolationMode) As Boolean Implements IPixelPictureSource.Present
        If context Is Nothing Then Return False
        Dim requests = GetVisibleTileRequests(source, destination, frameIndex)
        If requests.Count = 0 Then Return False
        Dim overview = New PixelPictureTileRequest(_levelCount - 1, 0, 0, Math.Clamp(frameIndex, 0, FrameCount - 1), _tileSize)
        SyncLock _gate
            If _disposed Then Return False
            _wanted.Clear()
            For Each request As PixelPictureTileRequest In requests
                _wanted.Add(request)
            Next
            _wanted.Add(overview)
            If _sampleRequest.HasValue Then _wanted.Add(_sampleRequest.Value)
            For Each pending In _pending
                If Not _wanted.Contains(pending.Key) Then pending.Value.Cancel()
            Next
        End SyncLock
        EnsureRequested(overview)
        If _sampleRequest.HasValue Then EnsureRequested(_sampleRequest.Value)
        For Each request As PixelPictureTileRequest In requests
            EnsureRequested(request)
        Next
        Dim drawn = DrawTile(context, overview, destination, source, interpolation)
        For Each request As PixelPictureTileRequest In requests
            If request.Equals(overview) Then Continue For
            drawn = DrawTile(context, request, destination, source, interpolation) OrElse drawn
        Next
        Return drawn
    End Function

    Private Sub EnsureRequested(request As PixelPictureTileRequest)
        Dim cancellation As CancellationTokenSource
        Dim revision As Integer
        SyncLock _gate
            If _disposed OrElse _cache.ContainsKey(request) OrElse _pending.ContainsKey(request) OrElse _failed.Contains(request) OrElse _pending.Count >= 4 Then Return
            cancellation = New CancellationTokenSource()
            _pending.Add(request, cancellation)
            revision = _revision
        End SyncLock
        Dim operation = Task.Run(Function() LoadTileAsync(request, cancellation, revision))
    End Sub

    Private Async Function LoadTileAsync(request As PixelPictureTileRequest, cancellation As CancellationTokenSource, revision As Integer) As Task
        Try
            Dim tile = Await _provider(request, cancellation.Token).ConfigureAwait(False)
            cancellation.Token.ThrowIfCancellationRequested()
            If tile Is Nothing Then Throw New InvalidOperationException("Tile provider 返回空数据。")
            Dim expectedWidth = CInt(Math.Min(_tileSize, Math.Ceiling(_width / request.Scale) - request.Column * _tileSize))
            Dim expectedHeight = CInt(Math.Min(_tileSize, Math.Ceiling(_height / request.Scale) - request.Row * _tileSize))
            If tile.Width <> expectedWidth OrElse tile.Height <> expectedHeight Then Throw New InvalidOperationException("Tile 尺寸与金字塔请求不一致。")
            If tile.Encoding.ColorSpace <> _encoding.ColorSpace OrElse tile.Encoding.TransferFunction <> _encoding.TransferFunction OrElse tile.Encoding.ReferenceWhiteNits <> _encoding.ReferenceWhiteNits OrElse tile.Encoding.HlgPeakNits <> _encoding.HlgPeakNits OrElse tile.Encoding.IsHdr <> _encoding.IsHdr Then Throw New InvalidOperationException("Tile 色彩编码与源声明不一致。")
            Dim upload = tile.PrepareUpload()
            cancellation.Token.ThrowIfCancellationRequested()
            SyncLock _gate
                If _disposed OrElse revision <> _revision OrElse Not _wanted.Contains(request) Then Return
                _clock += 1
                Dim entry As New TileEntry(tile, upload, _clock)
                _cache.Add(request, entry)
                _cacheBytes += entry.Bytes
                TrimCache()
            End SyncLock
        Catch ex As OperationCanceledException
        Catch ex As Exception
            SyncLock _gate
                If Not _disposed AndAlso revision = _revision AndAlso _wanted.Contains(request) Then
                    _lastError = ex
                    _failed.Add(request)
                    If _failed.Count > 256 Then
                        Dim oldestFailed As PixelPictureTileRequest
                        For Each failedRequest In _failed
                            oldestFailed = failedRequest
                            Exit For
                        Next
                        _failed.Remove(oldestFailed)
                    End If
                End If
            End SyncLock
        Finally
            SyncLock _gate
                Dim existing As CancellationTokenSource = Nothing
                If _pending.TryGetValue(request, existing) AndAlso ReferenceEquals(existing, cancellation) Then _pending.Remove(request)
            End SyncLock
            cancellation.Dispose()
            NotifyChanged()
        End Try
    End Function

    Private Sub TrimCache()
        While _cacheBytes > _cacheBudget AndAlso _cache.Count > 0
            Dim oldestKey As PixelPictureTileRequest
            Dim oldestEntry As TileEntry = Nothing
            For Each candidate In _cache
                If oldestEntry Is Nothing OrElse candidate.Value.LastUse < oldestEntry.LastUse Then
                    oldestKey = candidate.Key
                    oldestEntry = candidate.Value
                End If
            Next
            If oldestEntry Is Nothing Then Exit While
            _cache.Remove(oldestKey)
            _cacheBytes -= oldestEntry.Bytes
        End While
    End Sub

    Private Function DrawTile(context As D3D_PaintContext, request As PixelPictureTileRequest, destination As RectangleF,
                              visible As PixelPictureRegion, interpolation As InterpolationMode) As Boolean
        Dim entry As TileEntry = Nothing
        SyncLock _gate
            If _disposed OrElse Not _cache.TryGetValue(request, entry) Then Return False
            _clock += 1
            entry.LastUse = _clock
        End SyncLock
        Dim tileRegion = request.SourceRegion
        Dim left = Math.Max(visible.X, tileRegion.X)
        Dim top = Math.Max(visible.Y, tileRegion.Y)
        Dim right = Math.Min(visible.Right, Math.Min(_width, tileRegion.Right))
        Dim bottom = Math.Min(visible.Bottom, Math.Min(_height, tileRegion.Bottom))
        If right <= left OrElse bottom <= top Then Return False
        Dim scaleX = destination.Width / visible.Width
        Dim scaleY = destination.Height / visible.Height
        Dim target As New RectangleF(CSng(destination.X + (left - visible.X) * scaleX), CSng(destination.Y + (top - visible.Y) * scaleY), CSng((right - left) * scaleX), CSng((bottom - top) * scaleY))
        Dim local As New RectangleF(CSng((left - tileRegion.X) / request.Scale), CSng((top - tileRegion.Y) / request.Scale), CSng((right - left) / request.Scale), CSng((bottom - top) / request.Scale))
        context.BeginTextureUse()
        Dim bitmap = context.Compositor.TextureCache.AcquireTexture(Of ID2D1Bitmap1)(entry.GpuKey, context.DeviceGeneration,
            CLng(entry.Tile.Width) * entry.Tile.Height * 8L, Function() UploadTile(context.DeviceContext, entry))
        context.DeviceContext.DrawBitmap(bitmap, D3D_PaintContext.ToRawRect(target), 1.0F, interpolation, D3D_PaintContext.ToRawRect(local), New Nullable(Of System.Numerics.Matrix4x4)())
        Return True
    End Function

    Private Shared Function UploadTile(context As ID2D1DeviceContext, entry As TileEntry) As ID2D1Bitmap1
        Dim pinned = GCHandle.Alloc(entry.Upload, GCHandleType.Pinned)
        Try
            Dim properties As New BitmapProperties1(New Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.R16G16B16A16_Float, Vortice.DCommon.AlphaMode.Premultiplied), 96, 96, BitmapOptions.None)
            Return context.CreateBitmap(New SizeI(entry.Tile.Width, entry.Tile.Height), pinned.AddrOfPinnedObject(), CUInt(entry.Tile.Width * 8), properties)
        Finally
            pinned.Free()
        End Try
    End Function

    Public Function TryGetPixel(x As Long, y As Long, frameIndex As Integer, ByRef pixel As PixelPicturePixel) As Boolean Implements IPixelPictureSource.TryGetPixel
        pixel = Nothing
        If x < 0 OrElse y < 0 OrElse x >= _width OrElse y >= _height OrElse frameIndex < 0 OrElse frameIndex >= FrameCount Then Return False
        Dim request As New PixelPictureTileRequest(0, x \ _tileSize, y \ _tileSize, frameIndex, _tileSize)
        SyncLock _gate
            If _disposed Then Return False
            Dim entry As TileEntry = Nothing
            If _cache.TryGetValue(request, entry) Then
                _clock += 1
                entry.LastUse = _clock
                pixel = entry.Tile.Sample(CInt(x Mod _tileSize), CInt(y Mod _tileSize), x, y)
                Return True
            End If
            _sampleRequest = request
            _wanted.Add(request)
        End SyncLock
        EnsureRequested(request)
        Return False
    End Function

    Public Sub InvalidateTiles()
        SyncLock _gate
            If _disposed Then Return
            _revision += 1
            For Each pending In _pending.Values
                pending.Cancel()
            Next
            _cache.Clear()
            _cacheBytes = 0
            _failed.Clear()
            _lastError = Nothing
        End SyncLock
        NotifyChanged()
    End Sub

    Private Sub NotifyChanged()
        SyncLock _gate
            If _disposed Then Return
        End SyncLock
        Try
            RaiseEvent ContentChanged(Me, EventArgs.Empty)
        Catch ex As InvalidOperationException
        End Try
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        SyncLock _gate
            If _disposed Then Return
            _disposed = True
            For Each pending In _pending.Values
                pending.Cancel()
            Next
            _cache.Clear()
            _cacheBytes = 0
            _wanted.Clear()
        End SyncLock
    End Sub
End Class
