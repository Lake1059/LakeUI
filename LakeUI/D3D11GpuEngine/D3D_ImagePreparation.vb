Imports System.Drawing.Imaging
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks

''' <summary>后台解码静态图片快照，返回位图由调用方负责释放。</summary>
Public Module D3D_ImagePreparation
    Private ReadOnly _workers As New SemaphoreSlim(2, 2)

    ''' <summary>
    ''' 将首帧解码为 PArgb 像素，不访问控件和 GPU 资源，完成前关闭源文件。
    ''' 须在 UI 线程接收结果并释放过期位图；动画图片继续使用原有多帧加载路径。
    ''' </summary>
    Public Async Function LoadBitmapAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of Bitmap)
        ArgumentException.ThrowIfNullOrWhiteSpace(path)
        Await _workers.WaitAsync(cancellationToken).ConfigureAwait(False)
        Try
            Dim 编码数据 = Await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(False)
            Return Await Task.Run(
                Function()
                    cancellationToken.ThrowIfCancellationRequested()
                    Dim 开始时间 = D3D_RefreshDiagnostics.Start()
                    Using 数据流 As New MemoryStream(编码数据, writable:=False), 源图 = Image.FromStream(数据流)
                        Dim 结果位图 As New Bitmap(源图.Width, 源图.Height, PixelFormat.Format32bppPArgb)
                        Try
                            结果位图.SetResolution(96, 96)
                            Using 绘图对象 = Drawing.Graphics.FromImage(结果位图)
                                绘图对象.CompositingMode = Drawing2D.CompositingMode.SourceCopy
                                绘图对象.DrawImage(源图, New Rectangle(0, 0, 结果位图.Width, 结果位图.Height),
                                                   0, 0, 源图.Width, 源图.Height, GraphicsUnit.Pixel)
                            End Using
                            cancellationToken.ThrowIfCancellationRequested()
                            Return 结果位图
                        Catch
                            结果位图.Dispose()
                            Throw
                        Finally
                            D3D_RefreshDiagnostics.Record(开始时间, "ImageDecode")
                        End Try
                    End Using
                End Function, cancellationToken).ConfigureAwait(False)
        Finally
            _workers.Release()
        End Try
    End Function
End Module
