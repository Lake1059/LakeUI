using Vortice.WIC;
using Vortice.Direct2D1;
using LakeUI;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Forms;
using WicPixelFormat = Vortice.WIC.PixelFormat;

static class Program
{
    private static Exception? paintException;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, exception) => paintException ??= exception.Exception;
            Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
            Application.EnableVisualStyles();
            D3D_RenderDiagnostics.Enabled = true;
            VerifyPropertyDocumentation();
            VerifyHdrClassification();
            var (size, rgba) = LoadFixture(args);
            var expected = ToLinearPixels(rgba);
            var encoding = new PixelPictureColorEncoding();
            var providerCalls = 0;
            using var source = new PixelPictureTiledSource(size.Width, size.Height, (request, cancellation) =>
            {
                Interlocked.Increment(ref providerCalls);
                var width = (int)Math.Min(request.TileSize, Math.Ceiling(size.Width / request.Scale) - request.Column * request.TileSize);
                var height = (int)Math.Min(request.TileSize, Math.Ceiling(size.Height / request.Scale) - request.Row * request.TileSize);
                var pixels = new float[width * height * 4];
                for (var row = 0; row < height; row++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    for (var column = 0; column < width; column++)
                    {
                        var imageX = (int)Math.Min(size.Width - 1, (request.Column * request.TileSize + column) * request.Scale);
                        var imageY = (int)Math.Min(size.Height - 1, (request.Row * request.TileSize + row) * request.Scale);
                        var input = (imageY * size.Width + imageX) * 4;
                        var output = (row * width + column) * 4;
                        for (var channel = 0; channel < 4; channel++) pixels[output + channel] = rgba[input + channel] / 255f;
                    }
                }
                return Task.FromResult(new PixelPictureTile(width, height, pixels, encoding));
            }, encoding);
            using var form = new Form { ClientSize = new System.Drawing.Size(size.Width, size.Height), ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual, Location = new Point(20, 20) };
            using var picture = new PixelPictureBox { Dock = DockStyle.Fill, BorderSize = 0, ShowSelection = false, BackColor1 = Color.Black };
            form.Controls.Add(picture);
            form.Show();
            picture.Source = source;
            Console.WriteLine($"Control={picture.ClientSize}, source={source.Width}x{source.Height}, visible={picture.VisibleRegion.X},{picture.VisibleRegion.Y},{picture.VisibleRegion.Width},{picture.VisibleRegion.Height}");
            var expectedCalls = source.GetVisibleTileRequests(picture.VisibleRegion, picture.ImageToClient(picture.VisibleRegion), 0).Count + 1;
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < 10000)
            {
                Application.DoEvents();
                if (paintException is not null) throw new InvalidOperationException("Actual paint pipeline failed", paintException);
                if (source.LastError is not null) throw new InvalidOperationException("Tile preparation failed", source.LastError);
                if (Volatile.Read(ref providerCalls) >= expectedCalls && source.PendingTileCount == 0 && source.CpuCacheBytes > 0) break;
                Thread.Sleep(10);
            }
            Pump(50);
            Console.WriteLine($"Tile bytes={source.CpuCacheBytes}, pending={source.PendingTileCount}, zoom={picture.Zoom}, float={picture.UseFloatPresentation}");
            var diagnostics = D3D_RenderDiagnostics.GetV5ProbeSnapshot();
            Console.WriteLine($"Provider calls={providerCalls}, submitted frames={diagnostics.SubmittedFrames}, render failures={diagnostics.SurfaceRenderFailures}");
            if (providerCalls == 0 || diagnostics.SubmittedFrames == 0 || diagnostics.SurfaceRenderFailures != 0)
                throw new InvalidOperationException("Actual render pipeline did not load and submit image tiles");
            VerifyPicture(picture, expected, size.Width, size.Height, "sRGB tiled image");
            var pixel = new PixelPicturePixel();
            if (!picture.TryGetPixelAt(new Point(20, 200), ref pixel) || Math.Abs(pixel.Red - rgba[(200 * size.Width + 20) * 4] / 255f) > 0.000001f)
                throw new InvalidOperationException("Raw source pixel query failed");
            using var gpuContext = D3D_RenderCore.DeviceManager.CreateDeviceContext();
            using var gpuBitmap = UploadPixels(gpuContext, expected, size.Width, size.Height);
            var hdrExpected = expected.Select((value, index) => index % 4 == 3 ? value : value * 4).ToArray();
            using var hdrBitmap = UploadPixels(gpuContext, hdrExpected, size.Width, size.Height);
            picture.PlayAnimation = false;
            picture.Source = new PixelPictureGpuSource(size.Width, size.Height, new[] { new PixelPictureGpuFrame(gpuBitmap) });
            Pump(100);
            VerifyPicture(picture, expected, size.Width, size.Height, "SDR scRGB GPU image");
            using var hdrTiledSource = CreateLinearTiledSource(size.Width, size.Height, hdrExpected, true);
            picture.Source = hdrTiledSource;
            var tileWatch = Stopwatch.StartNew();
            do
            {
                Pump(10);
                if (hdrTiledSource.LastError is not null) throw new InvalidOperationException("HDR tile preparation failed", hdrTiledSource.LastError);
                if (hdrTiledSource.PendingTileCount == 0 && hdrTiledSource.CpuCacheBytes > 0) break;
            } while (tileWatch.ElapsedMilliseconds < 10000);
            Pump(50);
            VerifyPicture(picture, hdrExpected, size.Width, size.Height, "Explicit HDR scRGB tiled image", true);
            picture.Source = new PixelPictureGpuSource(size.Width, size.Height, new[] {
                new PixelPictureGpuFrame(gpuBitmap, 200), new PixelPictureGpuFrame(hdrBitmap, 200)
            }, isHdr: true);
            Pump(100);
            VerifyPicture(picture, expected, size.Width, size.Height, "HDR source frame 0", true);
            picture.PlayAnimation = true;
            var animationWatch = Stopwatch.StartNew();
            while (picture.FrameIndex != 1 && animationWatch.ElapsedMilliseconds < 3000) Pump(5);
            picture.PlayAnimation = false;
            if (picture.FrameIndex != 1) throw new InvalidOperationException("Animated HDR frame did not advance");
            VerifyPicture(picture, hdrExpected, size.Width, size.Height, "GPU HDR frame 1 (>1.0)", true);
            var wideExpected = ToDisplayP3Pixels(expected);
            using var wideBitmap = UploadPixels(gpuContext, wideExpected, size.Width, size.Height);
            picture.Source = new PixelPictureGpuSource(size.Width, size.Height, new[] { new PixelPictureGpuFrame(wideBitmap) });
            Pump(100);
            VerifyPicture(picture, wideExpected, size.Width, size.Height, "SDR wide-gamut GPU image (negative internal scRGB)");
            picture.Source = new PixelPictureGpuSource(size.Width, size.Height, new[] { new PixelPictureGpuFrame(wideBitmap) }, isHdr: true);
            Pump(100);
            VerifyPicture(picture, wideExpected, size.Width, size.Height, "HDR wide-gamut GPU image (negative scRGB)", true);
            var background = Color.FromArgb(64, 128, 192);
            picture.BackColor1 = background;
            var backgroundRgba = new byte[rgba.Length];
            for (var offset = 0; offset < backgroundRgba.Length; offset += 4)
            {
                backgroundRgba[offset] = background.R;
                backgroundRgba[offset + 1] = background.G;
                backgroundRgba[offset + 2] = background.B;
                backgroundRgba[offset + 3] = 255;
            }
            var backgroundExpected = ToLinearPixels(backgroundRgba);
            using var transparentBitmap = UploadPixels(gpuContext, new float[expected.Length], size.Width, size.Height);
            picture.Source = new PixelPictureGpuSource(size.Width, size.Height, new[] { new PixelPictureGpuFrame(transparentBitmap) });
            Pump(100);
            VerifyPicture(picture, backgroundExpected, size.Width, size.Height, "SDR transparent image over unchanged UI background");
            picture.Source = null;
            Pump(100);
            VerifyPicture(picture, backgroundExpected, size.Width, size.Height, "Empty source restores SDR rendering and presentation");
            VerifySelectionAndLetterbox();
            VerifyHugeImageRequestPlan();
            if (D3D_RenderDiagnostics.GetV5ProbeSnapshot().SurfaceRenderFailures != 0) throw new InvalidOperationException("Rendering failed during regression tests");
            form.Close();
            Console.WriteLine("PASS: SDR/HDR classification and output switching, tiled/GPU sources, HDR animation, wide gamut, raw pixel queries and actual composition pixels verified");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void VerifyPropertyDocumentation()
    {
        var path = Path.ChangeExtension(typeof(PixelPictureBox).Assembly.Location, ".xml");
        var document = System.Xml.Linq.XDocument.Load(path);
        var members = document.Descendants("member").ToDictionary(member => (string)member.Attribute("name")!);
        var types = new[] { typeof(PixelPictureBox), typeof(IPixelPictureSource), typeof(PixelPictureGpuSource),
            typeof(PixelPictureCallbackSource), typeof(PixelPicturePixel), typeof(PixelPicturePixelEventArgs),
            typeof(PixelPictureGpuFrame), typeof(PixelPictureTiledSource), typeof(PixelPictureTileRequest),
            typeof(PixelPictureTile), typeof(PixelPictureColorEncoding), typeof(PixelPicturePoint), typeof(PixelPictureRegion),
            typeof(D3D_IFloatRenderingSource), typeof(D3D_IFloatPresentationSource) };
        var count = 0;
        foreach (var type in types)
        {
            foreach (var property in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
            {
                var key = $"P:{type.FullName}.{property.Name}";
                if (!members.TryGetValue(key, out var member) || string.IsNullOrWhiteSpace(member.Element("summary")?.Value))
                    throw new InvalidOperationException($"Missing generated XML summary: {key}");
                count++;
            }
        }
        Console.WriteLine($"PASS: generated XML summaries for {count} public properties");
    }

    private static void VerifyHugeImageRequestPlan()
    {
        const long hugeWidth = 1_000_000_000_000L;
        const long hugeHeight = 900_000_000_000L;
        var providerCalls = 0;
        var encoding = new PixelPictureColorEncoding();
        using var source = new PixelPictureTiledSource(hugeWidth, hugeHeight, (request, cancellation) =>
        {
            Interlocked.Increment(ref providerCalls);
            var tileWidth = (int)Math.Min(request.TileSize, Math.Ceiling(hugeWidth / request.Scale) - request.Column * request.TileSize);
            var tileHeight = (int)Math.Min(request.TileSize, Math.Ceiling(hugeHeight / request.Scale) - request.Row * request.TileSize);
            if (tileWidth <= 0 || tileHeight <= 0) throw new InvalidOperationException("Huge image request produced an invalid tile");
            var pixels = new float[checked(tileWidth * tileHeight * 4)];
            for (var row = 0; row < tileHeight; row++)
            {
                cancellation.ThrowIfCancellationRequested();
                for (var column = 0; column < tileWidth; column++)
                {
                    var offset = (row * tileWidth + column) * 4;
                    pixels[offset] = 0.25f;
                    pixels[offset + 1] = 0.5f;
                    pixels[offset + 2] = 0.75f;
                    pixels[offset + 3] = 1;
                }
            }
            return Task.FromResult(new PixelPictureTile(tileWidth, tileHeight, pixels, encoding));
        }, encoding, tileSize: 256);
        using var form = new Form { ClientSize = new Size(320, 240), ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual, Location = new Point(20, 20) };
        using var picture = new PixelPictureBox { Dock = DockStyle.Fill, BorderSize = 0, ShowSelection = false };
        form.Controls.Add(picture);
        form.Show();
        picture.Source = source;
        var visible = picture.VisibleRegion;
        if (!double.IsFinite(visible.X) || !double.IsFinite(visible.Y) ||
            !double.IsFinite(visible.Width) || !double.IsFinite(visible.Height) ||
            visible.Width <= 0 || visible.Height <= 0 ||
            visible.Right > hugeWidth + 1 || visible.Bottom > hugeHeight + 1)
            throw new InvalidOperationException($"Huge image visible region is invalid: {visible}");
        var expectedRequests = source.GetVisibleTileRequests(visible, picture.ImageToClient(visible), 0).Count + 1;
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < 10000)
        {
            Pump(10);
            if (source.LastError is not null) throw new InvalidOperationException("Huge image tile preparation failed", source.LastError);
            if (Volatile.Read(ref providerCalls) >= expectedRequests && source.PendingTileCount == 0 && source.CpuCacheBytes > 0) break;
        }
        if (Volatile.Read(ref providerCalls) < expectedRequests || source.PendingTileCount != 0)
            throw new InvalidOperationException($"Huge image request plan did not settle: calls={providerCalls}, expected={expectedRequests}, pending={source.PendingTileCount}");
        if (source.LevelCount < 30 || source.LevelCount > 40) throw new InvalidOperationException($"Unexpected huge image pyramid depth: {source.LevelCount}");
        if (D3D_ControlSurfaceRegistry.RenderControl(picture, picture) is null)
            throw new InvalidOperationException("Huge image control surface was not rendered");
        Console.WriteLine($"PASS: {hugeWidth}x{hugeHeight} tiled image uses finite viewport coordinates, level {source.LevelCount}, {providerCalls} bounded tile requests");
        form.Close();
    }

    private static void VerifySelectionAndLetterbox()
    {
        using var form = new Form { ClientSize = new Size(360, 260), ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual, Location = new Point(20, 20) };
        using var backdrop = new ProbeBackdrop { Dock = DockStyle.Fill };
        using var picture = new ProbePictureBox { Bounds = new Rectangle(20, 20, 320, 240),
            BackColor = Color.White, BackColor1 = Color.FromArgb(40, 0, 0, 0), BorderSize = 0,
            AnimationDuration = 0, PlayAnimation = false };
        form.Controls.Add(backdrop);
        backdrop.Controls.Add(picture);
        form.Show();
        using var context = D3D_RenderCore.DeviceManager.CreateDeviceContext();
        using var selectionBitmap = UploadPixels(context, new float[320 * 240 * 4], 320, 240);
        picture.Source = new PixelPictureGpuSource(320, 240, new[] { new PixelPictureGpuFrame(selectionBitmap) });
        Pump(50);
        var changed = 0;
        picture.SelectionChanged += (_, _) => changed++;
        picture.Drag(MouseButtons.Right, new Point(80, 60), new Point(200, 140));
        var initial = new Rectangle(80, 60, 120, 80);
        if (picture.SelectionRect != initial) throw new InvalidOperationException($"Right-button selection failed: {picture.SelectionRect}");
        var cursorCases = new[] {
            (new Point(80, 60), Cursors.SizeNWSE),
            (new Point(140, 60), Cursors.SizeNS),
            (new Point(200, 60), Cursors.SizeNESW),
            (new Point(80, 100), Cursors.SizeWE),
            (new Point(200, 100), Cursors.SizeWE),
            (new Point(80, 140), Cursors.SizeNESW),
            (new Point(140, 140), Cursors.SizeNS),
            (new Point(200, 140), Cursors.SizeNWSE)
        };
        foreach (var (location, expectedCursor) in cursorCases)
        {
            picture.MoveCursor(location);
            if (picture.Cursor.Handle != expectedCursor.Handle)
                throw new InvalidOperationException($"Incorrect selection cursor at {location}: {picture.Cursor}, expected {expectedCursor}");
        }
        picture.MoveCursor(new Point(110, 60));
        if (picture.Cursor.Handle != Cursors.SizeAll.Handle) throw new InvalidOperationException("Selection border did not expose the move cursor");
        picture.MoveCursor(new Point(110, 100));
        if (picture.Cursor.Handle != Cursors.Default.Handle) throw new InvalidOperationException("Image interior did not restore the default cursor");
        var cases = new[] {
            (new Point(80, 60), new Point(60, 44), new Rectangle(60, 44, 140, 96)),
            (new Point(140, 60), new Point(140, 44), new Rectangle(80, 44, 120, 96)),
            (new Point(200, 60), new Point(220, 44), new Rectangle(80, 44, 140, 96)),
            (new Point(80, 100), new Point(60, 100), new Rectangle(60, 60, 140, 80)),
            (new Point(200, 100), new Point(220, 100), new Rectangle(80, 60, 140, 80)),
            (new Point(80, 140), new Point(60, 156), new Rectangle(60, 60, 140, 96)),
            (new Point(140, 140), new Point(140, 156), new Rectangle(80, 60, 120, 96)),
            (new Point(200, 140), new Point(220, 156), new Rectangle(80, 60, 140, 96))
        };
        foreach (var (start, end, expected) in cases)
        {
            picture.SelectionRect = initial;
            picture.Drag(MouseButtons.Left, start, end);
            if (picture.SelectionRect != expected) throw new InvalidOperationException($"Handle at {start} failed: {picture.SelectionRect}, expected {expected}");
        }
        picture.SetZoom(1.5, new Point(160, 120));
        foreach (var (start, end, expected) in cases)
        {
            picture.SelectionRect = initial;
            var clientStart = picture.ImageToClient(new PixelPictureRegion(start.X, start.Y, 0, 0));
            var clientEnd = picture.ImageToClient(new PixelPictureRegion(end.X, end.Y, 0, 0));
            picture.Drag(MouseButtons.Left, Point.Round(clientStart.Location), Point.Round(clientEnd.Location));
            if (picture.SelectionRect != expected) throw new InvalidOperationException($"Scaled handle at {start} failed: {picture.SelectionRect}, expected {expected}");
        }
        picture.ResetZoom();
        picture.SelectionRect = initial;
        picture.Drag(MouseButtons.Left, new Point(110, 60), new Point(126, 76));
        if (picture.SelectionRect != new Rectangle(96, 76, 120, 80)) throw new InvalidOperationException("Selection border movement failed");
        if (picture.Capture || changed < 10) throw new InvalidOperationException("Selection capture or completion notification failed");
        Console.WriteLine("PASS: right-button drawing, all eight resize handles at 1x/1.5x, border movement and capture release");
        picture.ShowSelection = false;
        var originalGlobalHdr = GlobalOptions.HDR.Enabled;
        try
        {
            GlobalOptions.HDR.Enabled = true;
            foreach (var isHdr in new[] { false, true })
            {
                var imagePixels = new float[320 * 160 * 4];
                for (var offset = 0; offset < imagePixels.Length; offset += 4)
                {
                    imagePixels[offset] = isHdr ? 2f : 0.2f;
                    imagePixels[offset + 1] = isHdr ? 1f : 0.1f;
                    imagePixels[offset + 2] = isHdr ? 0.5f : 0.05f;
                    imagePixels[offset + 3] = 0.5f;
                }
                using var bitmap = UploadPixels(context, imagePixels, 320, 160);
                picture.Source = new PixelPictureGpuSource(320, 160, new[] { new PixelPictureGpuFrame(bitmap) }, isHdr: isHdr);
                foreach (var explicitSource in new[] { false, true })
                {
                    picture.BackgroundSource = explicitSource ? backdrop : null;
                    D3D_ControlSurfaceRegistry.RenderControl(backdrop, backdrop);
                    Pump(50);
                    var parentSurface = D3D_ControlSurfaceRegistry.RenderControl(backdrop, backdrop) ?? throw new InvalidOperationException("Backdrop surface missing");
                    var parentRgba = new byte[360 * 260 * 4];
                    for (var row = 0; row < 260; row++)
                    {
                        for (var column = 0; column < 360; column++)
                        {
                            var color = column < 180 ? ProbeBackdrop.LeftColor : ProbeBackdrop.RightColor;
                            var offset = (row * 360 + column) * 4;
                            parentRgba[offset] = color.R;
                            parentRgba[offset + 1] = color.G;
                            parentRgba[offset + 2] = color.B;
                            parentRgba[offset + 3] = 255;
                        }
                    }
                    ProbeBitmap(context, parentSurface.Bitmap, new Rectangle(0, 0, 360, 260), parentRgba.Select(value => value / 255f).ToArray(), 360, 260, 1, true);
                    var parentLinear = ToLinearPixels(parentRgba);
                    var expected = new float[320 * 240 * 4];
                    for (var row = 0; row < 240; row++)
                    {
                        for (var column = 0; column < 320; column++)
                        {
                            var offset = (row * 320 + column) * 4;
                            var parentOffset = ((row + 20) * 360 + column + 20) * 4;
                            for (var channel = 0; channel < 3; channel++)
                            {
                                var background = parentLinear[parentOffset + channel] * (explicitSource ? 1 : 1 - 40 / 255f);
                                expected[offset + channel] = row >= 40 && row < 200 ? imagePixels[channel] + background * 0.5f : background;
                            }
                            expected[offset + 3] = 1;
                        }
                    }
                    VerifyPicture(picture, expected, 320, 240, $"{(isHdr ? "HDR" : "SDR")} letterbox and translucent image, {(explicitSource ? "explicit" : "automatic")} SDR backdrop", isHdr);
                }
                picture.Source = null;
            }
        }
        finally
        {
            GlobalOptions.HDR.Enabled = originalGlobalHdr;
            picture.Source = null;
            picture.BackgroundSource = null;
        }
        form.Close();
    }

    private sealed class ProbePictureBox : PixelPictureBox
    {
        public void MoveCursor(Point location)
        {
            OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, location.X, location.Y, 0));
        }

        public void Drag(MouseButtons button, Point start, Point end)
        {
            OnMouseDown(new MouseEventArgs(button, 1, start.X, start.Y, 0));
            if (!Capture) throw new InvalidOperationException("Mouse drag did not capture the control");
            OnMouseMove(new MouseEventArgs(button, 0, end.X, end.Y, 0));
            OnMouseUp(new MouseEventArgs(button, 1, end.X, end.Y, 0));
        }
    }

    private sealed class ProbeBackdrop : Control, D3D_IGpuRenderable, V5_IGpuPresentationSource
    {
        public static readonly Color LeftColor = Color.FromArgb(36, 64, 96);
        public static readonly Color RightColor = Color.FromArgb(120, 80, 48);

        public void RenderGpu(D3D_PaintContext context)
        {
            using var left = context.DeviceContext.CreateSolidColorBrush(D3D_PaintContext.ToRawColor4(LeftColor));
            using var right = context.DeviceContext.CreateSolidColorBrush(D3D_PaintContext.ToRawColor4(RightColor));
            context.DeviceContext.FillRectangle(new Vortice.RawRectF(0, 0, Width / 2, Height), left);
            context.DeviceContext.FillRectangle(new Vortice.RawRectF(Width / 2, 0, Width, Height), right);
        }
    }

    private static PixelPictureTiledSource CreateLinearTiledSource(int width, int height, float[] pixels, bool isHdr)
    {
        var encoding = new PixelPictureColorEncoding(PixelPictureColorSpace.ScRgb, PixelPictureTransferFunction.Linear, isHdr: isHdr);
        return new PixelPictureTiledSource(width, height, (request, cancellation) =>
        {
            var tileWidth = (int)Math.Min(request.TileSize, Math.Ceiling(width / request.Scale) - request.Column * request.TileSize);
            var tileHeight = (int)Math.Min(request.TileSize, Math.Ceiling(height / request.Scale) - request.Row * request.TileSize);
            var tilePixels = new float[tileWidth * tileHeight * 4];
            for (var row = 0; row < tileHeight; row++)
            {
                cancellation.ThrowIfCancellationRequested();
                for (var column = 0; column < tileWidth; column++)
                {
                    var imageX = (int)Math.Min(width - 1, (request.Column * request.TileSize + column) * request.Scale);
                    var imageY = (int)Math.Min(height - 1, (request.Row * request.TileSize + row) * request.Scale);
                    Array.Copy(pixels, (imageY * width + imageX) * 4, tilePixels, (row * tileWidth + column) * 4, 4);
                }
            }
            return Task.FromResult(new PixelPictureTile(tileWidth, tileHeight, tilePixels, encoding));
        }, encoding);
    }

    private static void VerifyHdrClassification()
    {
        foreach (var colorSpace in new[] { PixelPictureColorSpace.SRgb, PixelPictureColorSpace.ScRgb, PixelPictureColorSpace.DisplayP3, PixelPictureColorSpace.AdobeRgb, PixelPictureColorSpace.Rec2020 })
        {
            if (new PixelPictureColorEncoding(colorSpace, PixelPictureTransferFunction.Linear).IsHdr)
                throw new InvalidOperationException($"SDR color space {colorSpace} was classified as HDR");
        }
        if (!new PixelPictureColorEncoding(PixelPictureColorSpace.ScRgb, PixelPictureTransferFunction.Linear, isHdr: true).IsHdr)
            throw new InvalidOperationException("Explicit HDR scRGB data was classified as SDR");
        foreach (var transfer in new[] { PixelPictureTransferFunction.Pq, PixelPictureTransferFunction.Hlg })
        {
            if (!new PixelPictureColorEncoding(PixelPictureColorSpace.Rec2020, transfer).IsHdr)
                throw new InvalidOperationException($"HDR transfer function {transfer} was classified as SDR");
        }
        var callback = new PixelPictureCallbackSource(1, 1, (_, _, _, _, _) => false,
            colorSpace: PixelPictureColorSpace.ScRgb, transferFunction: PixelPictureTransferFunction.Linear);
        if (callback.IsHdr) throw new InvalidOperationException("SDR scRGB callback source was classified as HDR");
        var hdrCallback = new PixelPictureCallbackSource(1, 1, (_, _, _, _, _) => false,
            colorSpace: PixelPictureColorSpace.Rec2020, transferFunction: PixelPictureTransferFunction.Pq);
        if (!hdrCallback.IsHdr) throw new InvalidOperationException("PQ callback source was classified as SDR");
    }

    private static void VerifyPicture(PixelPictureBox picture, float[] expected, int width, int height, string label, bool isHdr = false)
    {
        Console.WriteLine($"Probe: {label}, HDR={picture.IsHdr}, float presentation={picture.UseFloatPresentation}");
        if (picture.IsHdr != isHdr || picture.UseFloatPresentation != isHdr)
            throw new InvalidOperationException("Presentation mode does not match the source HDR declaration");
        var surface = D3D_ControlSurfaceRegistry.RenderControl(picture, picture) ?? throw new InvalidOperationException("Control surface missing");
        Console.WriteLine($"Surface format={surface.SurfaceFormat}, scale={surface.SampleScale}, revision={surface.Revision}");
        using var readContext = D3D_RenderCore.DeviceManager.CreateDeviceContext();
        ProbeBitmap(readContext, surface.Bitmap, new Rectangle(Point.Empty, new System.Drawing.Size(surface.Bitmap.PixelSize.Width, surface.Bitmap.PixelSize.Height)), picture.UseFloatRendering ? expected : ToSrgbPixels(expected), width, height, surface.SampleScale, !picture.UseFloatRendering);
        var presenter = D3D_V5Presentation.GetPresenterForDiagnostics(picture) ?? throw new InvalidOperationException("Actual HWND presenter missing");
        if (!presenter.HasPresented(surface)) throw new InvalidOperationException("Latest image revision was not submitted to actual HWND presenter");
        var finalProbeCount = 0;
        presenter.SurfaceProbe = (context, bitmap, region) =>
        {
            var format = isHdr ? Vortice.DXGI.Format.R16G16B16A16_Float : Vortice.DXGI.Format.B8G8R8A8_UNorm;
            if (bitmap.PixelFormat.Format != format) throw new InvalidOperationException($"Incorrect SDR/HDR composition format: {bitmap.PixelFormat.Format}, expected {format}");
            Console.WriteLine($"Final composition bitmap={bitmap.PixelFormat.Format}, update={region}");
            ProbeBitmap(context, bitmap, region, isHdr ? expected : ToSrgbPixels(expected), width, height, 1, !isHdr);
            finalProbeCount++;
        };
        try
        {
            if (!presenter.Present(surface, 0)) throw new InvalidOperationException("Composition presentation failed");
        }
        finally
        {
            presenter.SurfaceProbe = null;
        }
        if (finalProbeCount != 1) throw new InvalidOperationException("Final composition pixels were not probed");
        Console.WriteLine($"Composition submitted={presenter.HasPresented(surface)}");
    }

    private static void Pump(int milliseconds)
    {
        var watch = Stopwatch.StartNew();
        do
        {
            Application.DoEvents();
            if (paintException is not null) throw new InvalidOperationException("Actual paint pipeline failed", paintException);
            Thread.Sleep(1);
        } while (watch.ElapsedMilliseconds < milliseconds);
    }

    private static (Vortice.Mathematics.SizeI Size, byte[] Rgba) LoadFixture(string[] args)
    {
        if (args.Length != 0)
        {
            var path = Path.GetFullPath(args[0]);
            using var factory = new IWICImagingFactory();
            using var decoder = factory.CreateDecoderFromFileName(path, FileAccess.Read, DecodeOptions.CacheOnLoad);
            using var frame = decoder.GetFrame(0);
            using var converter = factory.CreateFormatConverter();
            converter.Initialize(frame, WicPixelFormat.Format32bppRGBA).CheckError();
            var size = converter.Size;
            if (size.Width < 32 || size.Height < 256) throw new ArgumentException("The presentation fixture must be at least 32x256 pixels");
            var rgba = new byte[checked(size.Width * size.Height * 4)];
            converter.CopyPixels((uint)(size.Width * 4), rgba);
            Console.WriteLine($"Decoded via WIC outside control: {size.Width}x{size.Height}, {path}");
            return (size, rgba);
        }
        var fixtureSize = new Vortice.Mathematics.SizeI(1280, 720);
        var fixture = new byte[fixtureSize.Width * fixtureSize.Height * 4];
        for (var row = 0; row < fixtureSize.Height; row++)
        {
            for (var column = 0; column < fixtureSize.Width; column++)
            {
                var offset = (row * fixtureSize.Width + column) * 4;
                fixture[offset] = (byte)(column % 256);
                fixture[offset + 1] = (byte)(row % 256);
                fixture[offset + 2] = (byte)(((column / 8 + row / 8) % 2) * 255);
                fixture[offset + 3] = 255;
            }
        }
        Console.WriteLine("Generated deterministic 1280x720 fixture without GDI+");
        return (fixtureSize, fixture);
    }

    private static float[] ToLinearPixels(byte[] rgba)
    {
        var result = new float[rgba.Length];
        for (var offset = 0; offset < rgba.Length; offset += 4)
        {
            var alpha = rgba[offset + 3] / 255f;
            for (var channel = 0; channel < 3; channel++)
            {
                var encoded = rgba[offset + channel] / 255f;
                result[offset + channel] = (encoded <= 0.04045f ? encoded / 12.92f : MathF.Pow((encoded + 0.055f) / 1.055f, 2.4f)) * alpha;
            }
            result[offset + 3] = 1;
        }
        return result;
    }

    private static float[] ToDisplayP3Pixels(float[] linear)
    {
        var encoding = new PixelPictureColorEncoding(PixelPictureColorSpace.DisplayP3, PixelPictureTransferFunction.Linear);
        var result = new float[linear.Length];
        for (var offset = 0; offset < linear.Length; offset += 4)
        {
            var converted = encoding.ToLinearScRgb(linear[offset], linear[offset + 1], linear[offset + 2]);
            result[offset] = converted.X;
            result[offset + 1] = converted.Y;
            result[offset + 2] = converted.Z;
            result[offset + 3] = 1;
        }
        if (!result.Any(value => value < -0.01f)) throw new InvalidOperationException("Wide gamut fixture has no out-of-sRGB values");
        return result;
    }

    private static float[] ToSrgbPixels(float[] linear)
    {
        var result = new float[linear.Length];
        for (var offset = 0; offset < linear.Length; offset += 4)
        {
            for (var channel = 0; channel < 3; channel++)
            {
                var value = Math.Clamp(linear[offset + channel], 0, 1);
                result[offset + channel] = value <= 0.0031308f ? value * 12.92f : 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f;
            }
            result[offset + 3] = linear[offset + 3];
        }
        return result;
    }

    private static ID2D1Bitmap1 UploadPixels(ID2D1DeviceContext context, float[] pixels, int width, int height)
    {
        var upload = pixels.Select(value => (Half)value).ToArray();
        var pinned = GCHandle.Alloc(upload, GCHandleType.Pinned);
        try
        {
            return context.CreateBitmap(new Vortice.Mathematics.SizeI(width, height), pinned.AddrOfPinnedObject(), (uint)(width * 8),
                new BitmapProperties1(new Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.R16G16B16A16_Float, Vortice.DCommon.AlphaMode.Premultiplied), 96, 96, BitmapOptions.None));
        }
        finally
        {
            pinned.Free();
        }
    }

    private static void ProbeBitmap(ID2D1DeviceContext context, ID2D1Bitmap1 bitmap, Rectangle region, float[] expected, int width, int height, int scale, bool isSdr = false)
    {
        var format = isSdr ? Vortice.DXGI.Format.B8G8R8A8_UNorm : Vortice.DXGI.Format.R16G16B16A16_Float;
        if (bitmap.PixelFormat.Format != format) throw new InvalidOperationException($"Unexpected surface format: {bitmap.PixelFormat.Format}");
        using var readback = context.CreateBitmap(new Vortice.Mathematics.SizeI(region.Width, region.Height), IntPtr.Zero, 0,
            new BitmapProperties1(bitmap.PixelFormat, 96, 96,
                BitmapOptions.CpuRead | BitmapOptions.CannotDraw));
        readback.CopyFromBitmap(Point.Empty, bitmap, region).CheckError();
        var mapped = readback.Map(MapOptions.Read);
        try
        {
            var maxError = 0f;
            var worst = "";
            var rowBytes = new byte[region.Width * (isSdr ? 4 : 8)];
            for (var row = 0; row < height; row++)
            {
                Marshal.Copy(IntPtr.Add(mapped.Bits, checked((int)(row * scale * mapped.Pitch))), rowBytes, 0, rowBytes.Length);
                var actual = MemoryMarshal.Cast<byte, Half>(rowBytes.AsSpan());
                for (var column = 0; column < width; column++)
                {
                    for (var channel = 0; channel < 4; channel++)
                    {
                        var value = isSdr ? rowBytes[column * scale * 4 + (channel < 3 ? 2 - channel : channel)] / 255f : (float)actual[column * scale * 4 + channel];
                        var reference = expected[(row * width + column) * 4 + channel];
                        var error = Math.Abs(value - reference);
                        if (!float.IsFinite(value)) throw new InvalidOperationException("GPU output contains non-finite values");
                        if (error > maxError)
                        {
                            maxError = error;
                            worst = $"({column},{row}) channel={channel}, expected={reference}, actual={value}";
                        }
                    }
                }
            }
            Console.WriteLine($"GPU readback compared {width * height} pixels, max error={maxError}: {worst}");
            var tolerance = isSdr ? 1f / 255f + 0.000001f : 0.002f;
            if (maxError > tolerance) throw new InvalidOperationException($"GPU pixel mismatch, max error={maxError}: {worst}");
        }
        finally
        {
            readback.Unmap();
        }
    }
}
