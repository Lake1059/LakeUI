Imports System.Runtime.InteropServices
Imports Vortice.Direct2D1

Friend Module D3D_NoiseTexture
    Friend Const Edge As Integer = 128

    Friend Function Create(target As ID2D1RenderTarget) As ID2D1Bitmap
        Dim pixels(Edge * Edge - 1) As Integer
        Dim rng As New Random(1059)
        For i = 0 To pixels.Length - 1
            pixels(i) = &HFF000000 Or (rng.Next(256) * &H10101)
        Next
        Dim pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned)
        Try
            Dim props As New BitmapProperties(New Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied))
            Return target.CreateBitmap(New Vortice.Mathematics.SizeI(Edge, Edge), pinned.AddrOfPinnedObject(), CUInt(Edge * 4), props)
        Finally
            pinned.Free()
        End Try
    End Function
End Module
