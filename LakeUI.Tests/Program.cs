using System.Drawing;
using System.ComponentModel;
using System.Collections;
using System.Reflection;
using System.Windows.Forms;
using LakeUI;

static class Program
{
    private static void Main()
    {
        VerifyFenceParsing();
        VerifyBuiltInHighlighters();
        VerifySyntaxIndentation();
        VerifyRenderedIndentationOffset();
        VerifyMermaidCopyText();
        VerifyCustomHighlighterRegistration();
        VerifyAgentThinkingTagParsing();
        VerifyModernTextBoxPaddingDpiContract();
        VerifyV5MarkerCoverage();
        VerifyBackgroundSourceControlCoverage();
        VerifyAutomaticBackdropAncestorSearch();
        VerifyBackgroundDependencyLifecycle();
        VerifyTabListTransparentBackgroundFallback();
        VerifyTabListBackgroundSourceBrowsable();
        VerifyModernPanelOverlayRenderingContract();
        VerifyModernButtonAnimationDefaults();
        VerifyRenderCacheBudgetCoordinator();
        VerifyTextureCacheLifecycle();
        VerifyZeroCapacityDrawingCaches();
        VerifyAnimationOwnerRelease();
        VerifyVisibleControlSurfaceProtection();
        VerifyGlobalBudgetProperties();
        VerifyCleanupRecoveryTargets();
        VerifyCleanupRecoveryIncludesRegisteredSurface();
        VerifyGeometryInvalidatesBackdropConsumers();
        VerifyFullCleanupResumesVisibleControlRendering();
        VerifyFullCleanupRecreatesSharedFactories();
        VerifyV5DirtyRetryAndResetContracts();
        VerifyBackdropImageSnapshotSurvivesCallerDispose();
        VerifyHdrImageMappingUsesCachedLookup();
        VerifyV5ProbeApi();
        VerifyWindowCornerModeContract();
        VerifyOverlayConfirmationThenOwnerClosingDoesNotDeadlock();
        Console.WriteLine("LakeUI tests passed.");
    }

    private static void VerifyWindowCornerModeContract()
    {
        using var window = new ThisIsYourWindow();
        Assert(window.WindowCornerMode == DwmWindowStyle.CornerMode.Square,
            "ThisIsYourWindow must preserve the historical square-corner default.");
        window.WindowCornerMode = DwmWindowStyle.CornerMode.Round;
        Assert(window.WindowCornerMode == DwmWindowStyle.CornerMode.Round,
            "ThisIsYourWindow must expose a writable DWM corner preference.");
        Assert(DwmWindowStyle.IsCornerModeSupported == OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000),
            "Corner capability detection must match the Windows 11 build 22000 requirement.");
        Assert(DwmWindowStyle.GetCornerRadiusLogical(DwmWindowStyle.CornerMode.Round) == 8.0F,
            "Round must use the Windows 11 8px logical radius.");
        Assert(DwmWindowStyle.GetCornerRadiusLogical(DwmWindowStyle.CornerMode.RoundSmall) == 4.0F,
            "RoundSmall must use the Windows 11 4px logical radius.");
        Assert(DwmWindowStyle.GetCornerRadiusLogical(DwmWindowStyle.CornerMode.Square) == 0.0F,
            "Square must not apply rounded GPU geometry.");
        Assert(ExOverlayMsgBoxTheme.CreateLight().ButtonBorderRadius == 4,
            "Overlay message buttons must use the Windows 11 4px control radius.");

        using var form = new Form { ClientSize = new Size(640, 480) };
        _ = form.Handle;
        window.BorderSize = 0;
        window.Attach(form);
        Assert(form.Controls.OfType<ThisIsYourWindow.ChromeOverlayControl>().Count() == 1,
            "A borderless attached window must keep only its caption overlay.");

        foreach (var borderSize in new[] { 1, 2 })
        {
            window.BorderSize = borderSize;
            var overlays = form.Controls.OfType<ThisIsYourWindow.ChromeOverlayControl>().ToArray();
            Assert(overlays.Length == 5,
                $"BorderSize {borderSize} must keep the caption plus four border overlays.");
            var edgeBands = overlays.Where(control => control.Width == form.ClientSize.Width).ToArray();
            Assert(edgeBands.Length == 2 &&
                   edgeBands.Any(control => control.Top == 0) &&
                   edgeBands.Any(control => control.Bottom == form.ClientSize.Height),
                $"BorderSize {borderSize} must render the top and bottom borders in full-width overlays.");
            if (DwmWindowStyle.IsCornerModeSupported)
                Assert(edgeBands.All(control => control.Height > borderSize),
                    $"BorderSize {borderSize} must keep each rounded corner arc inside one edge overlay.");
        }

        var originalPopupMode = DwmWindowStyle.PopupCornerMode;
        try
        {
            Assert(originalPopupMode == DwmWindowStyle.CornerMode.Round,
                "LakeUI popup windows must preserve the historical rounded default.");
            DwmWindowStyle.PopupCornerMode = DwmWindowStyle.CornerMode.Square;
            Assert(!DwmWindowStyle.PopupUsesRoundedCorners,
                "Square popup mode must disable rounded popup geometry.");
            DwmWindowStyle.PopupCornerMode = DwmWindowStyle.CornerMode.RoundSmall;
            Assert(DwmWindowStyle.PopupUsesRoundedCorners,
                "RoundSmall popup mode must be treated as rounded popup geometry.");
        }
        finally
        {
            DwmWindowStyle.PopupCornerMode = originalPopupMode;
        }
    }
    private static void VerifyOverlayConfirmationThenOwnerClosingDoesNotDeadlock()
    {
        using var completed = new ManualResetEventSlim();
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            using var form = new Form { Width = 320, Height = 200, StartPosition = FormStartPosition.CenterScreen };
            form.Shown += (_, _) =>
            {
                var timer = new System.Windows.Forms.Timer { Interval = 100 };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    foreach (Form openForm in Application.OpenForms)
                    {
                        if (openForm.GetType().Name != "ExOverlayMsgBoxForm") continue;

                        foreach (Control control in openForm.Controls)
                        {
                            if (control is ModernButton button)
                            {
                                typeof(ModernButton).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                                    .Invoke(button, new object[] { EventArgs.Empty });
                                return;
                            }
                        }
                    }

                    throw new InvalidOperationException("Overlay message-box card was not shown.");
                };
                timer.Start();

                var worker = new Thread(() =>
                {
                    try
                    {
                        LakeUI.ExOverlayMsgBoxModule.ExOverlayMsgBox(form, "owner closing regression", (int)MessageBoxButtons.OK);
                        completed.Set();
                        form.BeginInvoke((System.Windows.Forms.MethodInvoker)(() => form.Close()));
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                        completed.Set();
                    }
                })
                {
                    IsBackground = true
                };
                worker.Start();
            };
            Application.Run(form);
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert(completed.Wait(TimeSpan.FromSeconds(5)),
            "Overlay message box must return after its confirmation button closes the card from a background caller.");
        Assert(thread.Join(TimeSpan.FromSeconds(2)),
            "Overlay background-caller regression thread did not terminate.");
        Assert(failure is null, $"Overlay background-caller regression failed: {failure}");
    }

    private static void VerifyVisibleControlSurfaceProtection()
    {
        using var form = new Form();
        using var control = new ModernPanel();
        form.Controls.Add(control);
        form.Show();
        Application.DoEvents();

        var surfaceType = typeof(MarkdownViewerCore).Assembly.GetType("LakeUI.D3D_ControlSurface")!;
        var oldest = surfaceType.GetProperty("OldestUseTick", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var trim = surfaceType.GetMethod("TrimOldest", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var manager = typeof(D3D_RenderCore).GetProperty("DeviceManager", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!;
        var surface = Activator.CreateInstance(surfaceType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { control, manager },
            culture: null)!;
        try
        {
            var allocated = surfaceType.GetField("_allocatedBytes", BindingFlags.Instance | BindingFlags.NonPublic)!;
            allocated.SetValue(surface, 4096L);
            var lastUsed = surfaceType.GetField("_lastUsed", BindingFlags.Instance | BindingFlags.NonPublic)!;
            lastUsed.SetValue(surface, 1L);

            Assert((long)oldest.GetValue(surface)! != 1L,
                "Visible control surfaces must not become LRU victims.");
            Assert(!(bool)trim.Invoke(surface, null)!,
                "Visible control surfaces must reject budget trimming.");
        }
        finally
        {
            form.Hide();
            (surface as IDisposable)?.Dispose();
        }
    }

    private static void VerifyAgentThinkingTagParsing()
    {
        var parser = new AgentThinkingTextParser();
        var visible = new System.Text.StringBuilder();
        var thinking = new System.Text.StringBuilder();
        foreach (var part in new[] { "<thi", "nk>first", "</thi", "nk>answer<think>second</think>end" })
        {
            var chunk = parser.Append(part);
            visible.Append(chunk.VisibleText);
            thinking.Append(chunk.ThinkingText);
        }

        var tail = parser.Complete();
        visible.Append(tail.VisibleText);
        thinking.Append(tail.ThinkingText);
        Assert(visible.ToString() == "answerend", "Thinking tags must not leak into the visible answer.");
        Assert(thinking.ToString() == "firstsecond", "Thinking text must remain available for the collapsed activity.");
    }

    private static void VerifyModernTextBoxPaddingDpiContract()
    {
        using var textBox = new ModernTextBox
        {
            Width = 240,
            Height = 100,
            BorderSize = 0,
            BorderRadius = 0,
            Padding = new Padding(15)
        };

        // Simulate a 150% monitor after WinForms has already scaled Control.Padding.
        typeof(ModernTextBox).GetField("_cachedDpiScale", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(textBox, 1.5F);

        var viewport = (int)typeof(ModernTextBox)
            .GetMethod("TextViewportHeight", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(textBox, null)!;
        Assert(viewport == 70,
            "ModernTextBox must use the already DPI-scaled Padding value exactly once.");

        var textAreaWidth = (int)typeof(ModernTextBox)
            .GetMethod("TextAreaWidth", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(textBox, null)!;
        Assert(textAreaWidth == 210,
            "ModernTextBox must apply the already DPI-scaled horizontal Padding exactly once.");
    }

    private static void VerifyV5MarkerCoverage()
    {
        var assembly = typeof(MarkdownViewerCore).Assembly;
        var types = assembly.GetTypes();
        var renderable = types.FirstOrDefault(type => type.Name == "D3D_IGpuRenderable");
        var marker = types.FirstOrDefault(type => type.Name == "V5_IGpuPresentationSource");
        var backgroundProvider = types.FirstOrDefault(type => type.Name == "D3D_IBackgroundSourceProvider");
        Assert(renderable is not null && marker is not null && backgroundProvider is not null, "GPU migration contracts must be present.");
        var emptyPlaceholder = types.FirstOrDefault(type => type.Name == "JustEmptyControl");
        Assert(emptyPlaceholder is not null && renderable!.IsAssignableFrom(emptyPlaceholder),
            "JustEmptyControl must participate in V5 GPU rendering so transparent backgrounds can map their nearest ancestor.");

        foreach (var type in types)
        {
            if (type.IsAbstract || !typeof(Control).IsAssignableFrom(type) || !renderable!.IsAssignableFrom(type))
                continue;
            Assert(marker!.IsAssignableFrom(type),
                $"GPU-renderable control {type.FullName} must implement V5_IGpuPresentationSource.");
        }

        foreach (var type in types)
        {
            if (type.IsAbstract || !typeof(Control).IsAssignableFrom(type) || !backgroundProvider!.IsAssignableFrom(type))
                continue;
            Assert(marker!.IsAssignableFrom(type),
                $"Background source provider {type.FullName} must expose a V5 GPU surface.");
        }
    }

    private static void VerifyAutomaticBackdropAncestorSearch()
    {
        using var source = new ModernPanel();
        using var middle = new Panel();
        using var label = new HtmlColorLabel();
        source.Controls.Add(middle);
        middle.Controls.Add(label);

        var registry = typeof(MarkdownViewerCore).Assembly.GetType("LakeUI.D3D_ControlSurfaceRegistry");
        var finder = registry?.GetMethod("查找最近GPU祖先", BindingFlags.Static | BindingFlags.NonPublic);
        var resolved = finder?.Invoke(null, new object[] { label }) as Control;
        Assert(ReferenceEquals(resolved, source),
            "Automatic GPU backdrop must resolve the nearest V5 ancestor through ordinary containers.");

        var cycleCheck = registry?.GetMethod("形成背景循环", BindingFlags.Static | BindingFlags.NonPublic);
        var selfCycle = (bool?)cycleCheck?.Invoke(null, new object[] { label, label });
        Assert(selfCycle == true, "Background sampling must reject a control as its own source.");
    }

    private static void VerifyBackgroundSourceControlCoverage()
    {
        var assembly = typeof(MarkdownViewerCore).Assembly;
        var backgroundProvider = assembly.GetTypes().First(type => type.Name == "D3D_IBackgroundSourceProvider");
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || (!type.IsPublic && !type.IsNestedPublic) ||
                !typeof(Control).IsAssignableFrom(type) || !backgroundProvider.IsAssignableFrom(type))
                continue;

            var property = type.GetProperty("BackgroundSource", BindingFlags.Instance | BindingFlags.Public);
            Assert(property is not null && property.CanRead && property.CanWrite &&
                   typeof(Control).IsAssignableFrom(property.PropertyType),
                $"V5 background consumer {type.FullName} must expose a writable Control BackgroundSource property.");
            var browsable = property!.GetCustomAttribute<BrowsableAttribute>();
            Assert(browsable?.Browsable != false,
                $"V5 background consumer {type.FullName}.BackgroundSource must remain available in the designer.");
        }
    }

    private static void VerifyBackgroundDependencyLifecycle()
    {
        using var sourceHost = new Panel();
        using var consumerHost = new Panel();
        using var source = new ModernPanel();
        using var consumer = new ModernButton();
        sourceHost.Controls.Add(source);
        consumerHost.Controls.Add(consumer);

        var registry = typeof(MarkdownViewerCore).Assembly.GetType("LakeUI.D3D_ControlSurfaceRegistry")!;
        var register = registry.GetMethod("注册依赖", BindingFlags.Static | BindingFlags.NonPublic)!;
        var remove = registry.GetMethod("移除控件", BindingFlags.Static | BindingFlags.NonPublic)!;
        var handleDestroyed = registry.GetMethod("控件句柄已销毁", BindingFlags.Static | BindingFlags.NonPublic)!;
        var sourcesField = registry.GetField("_consumerSources", BindingFlags.Static | BindingFlags.NonPublic)!;
        var coordinatesField = registry.GetField("_consumerCoordinateControls", BindingFlags.Static | BindingFlags.NonPublic)!;

        register.Invoke(null, new object[] { consumer, source, new RectangleF(1, 2, 10, 12) });
        Assert(GetRegistryCollectionCount(sourcesField, consumer) == 1,
            "A successful V5 backdrop sample must register its source dependency.");
        Assert(GetRegistryCollectionCount(coordinatesField, consumer) > 0,
            "Cross-container backdrop mapping must track coordinate-space ancestors.");

        D3D_BackgroundPenetration.SetBackgroundSource(consumer, source, null!);
        Assert(GetRegistryCollectionCount(sourcesField, consumer) == 0 &&
               GetRegistryCollectionCount(coordinatesField, consumer) == 0,
            "Changing BackgroundSource must detach stale source and coordinate dependencies immediately.");

        register.Invoke(null, new object[] { consumer, source, RectangleF.Empty });
        handleDestroyed.Invoke(null, new object[] { source, EventArgs.Empty });
        Assert(GetRegistryCollectionCount(sourcesField, consumer) == 1,
            "Temporary handle destruction must preserve logical backdrop dependencies for recreation.");
        remove.Invoke(null, new object[] { source });
        Assert(GetRegistryCollectionCount(sourcesField, consumer) == 0 &&
               GetRegistryCollectionCount(coordinatesField, consumer) == 0,
            "Removing a backdrop source must detach and wake its consumers without retaining stale controls.");
    }

    private static int GetRegistryCollectionCount(FieldInfo field, Control key)
    {
        var dictionary = (IDictionary)field.GetValue(null)!;
        if (!dictionary.Contains(key)) return 0;
        var collection = dictionary[key]!;
        return (int)collection.GetType().GetProperty("Count")!.GetValue(collection)!;
    }

    private static void VerifyV5ProbeApi()
    {
        D3D_PaintBridge.ResetV5Probe();
        D3D_PaintBridge.V5ProbeEnabled = true;
        var snapshot = D3D_PaintBridge.GetV5ProbeSnapshot();
        Assert(snapshot.Enabled, "V5 probe must report its enabled state.");
        Assert(snapshot.BackdropAttempts == 0 && snapshot.ChromeOverlayCreateFailures == 0,
            "V5 probe reset must clear runtime counters.");
        Assert(snapshot.ChromeOverlayFallbackPaints == 0 && snapshot.SubmittedFrames == 0 &&
               snapshot.FrameIntervalP99 == 0 && snapshot.CrossFormBackdropSuccesses == 0,
            "V5 probe reset must clear chrome fallback and frame timing state.");
        D3D_PaintBridge.V5ProbeEnabled = false;
    }

    private static void VerifyBackdropImageSnapshotSurvivesCallerDispose()
    {
        var textureCache = new D3D_TextureCache();
        var imageCache = new D3D_ImageCache(textureCache);
        var renderer = new D3D_BackdropRenderer(imageCache, D3D_RenderCore.DeviceManager);
        var source = new Bitmap(7, 5);
        try
        {
            using (var graphics = Graphics.FromImage(source))
                graphics.Clear(Color.FromArgb(255, 40, 80, 120));

            renderer.SetImage(source);
            source.Dispose();

            var imageField = renderer.GetType().GetField("_image", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var snapshot = (Image?)imageField.GetValue(renderer);
            Assert(snapshot is not null && snapshot.Width == 7 && snapshot.Height == 5,
                "Backdrop renderer must retain a stable image snapshot after the caller disposes its source.");

            var CPU所有者 = (D3D_IRenderCacheOwner)renderer.GetType().GetField("_CPU缓存所有者", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
            Assert(CPU所有者.CacheBytes == 7 * 5 * 4 && CPU所有者.OldestUseTick == long.MaxValue,
                "Backdrop CPU accounting must include and protect the authoritative source snapshot.");
            renderer.BeginFrameUse();
            using (var 替换源 = new Bitmap(3, 2)) renderer.SetImage(替换源);
            Assert(CPU所有者.CacheBytes == (7 * 5 + 3 * 2) * 4,
                "Retired source snapshots must remain in CPU accounting until the active frame ends.");
            renderer.EndFrameUse();
            Assert(CPU所有者.CacheBytes == 3 * 2 * 4, "Ending image use must release retired CPU snapshots.");
            typeof(D3D_BackdropRenderer).GetMethod("EnsureNoiseBitmap", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(renderer, null);
            Assert(CPU所有者.CacheBytes == (3 * 2 + 128 * 128) * 4 && CPU所有者.TrimOldest(),
                "Reconstructible noise must be accounted and reclaimable without dropping the source.");
            CPU所有者.ReleaseAll();
            Assert(CPU所有者.CacheBytes == 3 * 2 * 4, "CPU cache release must preserve the authoritative image.");

            renderer.SetImage(null);
            Assert((D3D_BackdropMode)renderer.GetType().GetProperty("Mode")!.GetValue(renderer)! == D3D_BackdropMode.None,
                "Clearing a backdrop image must release the snapshot and return to None mode.");
        }
        finally
        {
            renderer.Dispose();
            imageCache.Dispose();
            textureCache.Dispose();
            try { source.Dispose(); } catch { }
        }
    }

    private static void VerifyHdrImageMappingUsesCachedLookup()
    {
        var options = GlobalOptions.HDR;
        var previousEnabled = options.Enabled;
        var previousImages = options.MapImages;
        try
        {
            options.MapImages = true;
            options.Enabled = true;
            using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(bitmap))
                graphics.Clear(Color.FromArgb(255, 128, 160, 192));
            D3D_HdrOutput.MapBitmapForImageUpload(bitmap);
            var red = D3D_HdrOutput.MapColor4(Color.FromArgb(255, 255, 0, 0));
            var green = D3D_HdrOutput.MapColor4(Color.FromArgb(255, 0, 255, 0));
            var blue = D3D_HdrOutput.MapColor4(Color.FromArgb(255, 0, 0, 255));
            Assert(red.R > red.G && red.R > red.B && green.G > green.R && green.G > green.B &&
                   blue.B > blue.R && blue.B > blue.G,
                "HDR RGB cache keys must preserve independent red, green, and blue channels.");
            Assert(bitmap.GetPixel(0, 0).A == 255,
                "HDR image mapping must preserve opaque alpha while using the cached lookup path.");
        }
        finally
        {
            options.Enabled = previousEnabled;
            options.MapImages = previousImages;
        }
    }

    private static void VerifyTabListTransparentBackgroundFallback()
    {
        using var tabList = new ModernTabListControl
        {
            BackColor = Color.FromArgb(24, 24, 24),
            TabStripBackColor = Color.Transparent
        };
        var method = typeof(ModernTabListControl).GetMethod(
            "获取标签栏有效背景颜色",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var effective = (Color?)method?.Invoke(tabList, null);
        Assert(effective == Color.FromArgb(24, 24, 24),
            "Transparent TabStripBackColor must inherit the control background for V5 surfaces.");
    }

    private static void VerifyTabListBackgroundSourceBrowsable()
    {
        var property = typeof(ModernTabListControl).GetProperty("BackgroundSource");
        var browsable = property?.GetCustomAttribute<BrowsableAttribute>();
        Assert(property is not null && browsable?.Browsable == true,
            "ModernTabListControl.BackgroundSource must remain visible in the designer property grid.");
    }

    private static void VerifyModernPanelOverlayRenderingContract()
    {
        using var panel = new ModernPanel
        {
            BackColor = Color.FromArgb(255, 20, 20, 20),
            BackColor1 = Color.FromArgb(255, 30, 30, 30),
            OverlayColor = Color.FromArgb(64, 255, 255, 255)
        };
        var method = typeof(ModernPanel).GetMethod(
            "需要自绘背景",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var requiresGpuBackground = (bool?)method?.Invoke(panel, null);
        Assert(requiresGpuBackground == true,
            "ModernPanel with a translucent OverlayColor must use the V5 background composition path.");

    }

    private static void VerifyModernButtonAnimationDefaults()
    {
        using var button = new ModernButton();
        Assert(button.AnimationDuration == 300,
            "ModernButton.AnimationDuration must default to 300 milliseconds.");
        var defaultValue = typeof(ModernButton).GetProperty("AnimationDuration")?
            .GetCustomAttribute<DefaultValueAttribute>();
        Assert((int?)defaultValue?.Value == 300,
            "ModernButton.AnimationDuration designer metadata must match its runtime default.");

        Assert(button.RippleAnimationDuration == 1200,
            "ModernButton.RippleAnimationDuration must default to 1200 milliseconds.");
        var rippleDefaultValue = typeof(ModernButton).GetProperty("RippleAnimationDuration")?
            .GetCustomAttribute<DefaultValueAttribute>();
        Assert((int?)rippleDefaultValue?.Value == 1200,
            "ModernButton.RippleAnimationDuration designer metadata must match its runtime default.");

        button.AnimationDuration = 450;
        Assert(button.AnimationDuration == 450 && button.RippleAnimationDuration == 1200,
            "ModernButton.AnimationDuration must not change the ripple animation duration.");
        button.RippleAnimationDuration = 750;
        Assert(button.AnimationDuration == 450 && button.RippleAnimationDuration == 750,
            "ModernButton.RippleAnimationDuration must be independently configurable.");
    }

    private static void VerifyCleanupRecoveryTargets()
    {
        using var form = new Form();
        using var otherForm = new Form();
        using var panel = new ModernPanel();
        using var button = new ModernButton();
        using var otherButton = new ModernButton();
        form.Controls.Add(panel);
        panel.Controls.Add(button);
        otherForm.Controls.Add(otherButton);

        form.Show();
        otherForm.Show();
        Application.DoEvents();

        _ = form.Handle;
        _ = panel.Handle;
        _ = button.Handle;
        _ = otherForm.Handle;
        _ = otherButton.Handle;

        var assembly = typeof(MarkdownViewerCore).Assembly;
        var presentation = assembly.GetType("LakeUI.D3D_V5Presentation")!;
        var createPresenter = presentation.GetMethod(
            "获取或创建呈现器", BindingFlags.Static | BindingFlags.NonPublic)!;
        createPresenter.Invoke(null, new object[] { panel });
        createPresenter.Invoke(null, new object[] { button });
        createPresenter.Invoke(null, new object[] { otherButton });

        var getTargets = presentation.GetMethod(
            "获取清理恢复目标", BindingFlags.Static | BindingFlags.NonPublic)!;
        var targets = (Control[])getTargets.Invoke(null, new object[] { form })!;
        Assert(targets.Length == 2 && ReferenceEquals(targets[0], panel) && ReferenceEquals(targets[1], button),
            "Full cleanup recovery must include only the target form's V5 presenters in outer-to-inner order.");
        form.Hide();
        otherForm.Hide();
    }

    private static void VerifyFullCleanupResumesVisibleControlRendering()
    {
        using var form = new Form();
        using var panel = new ModernPanel { Dock = DockStyle.Fill };
        form.Controls.Add(panel);
        form.Show();
        Application.DoEvents();

        D3D_PaintBridge.ResetV5Probe();
        D3D_PaintBridge.V5ProbeEnabled = true;
        panel.Invalidate();
        Application.DoEvents();
        var before = D3D_PaintBridge.GetV5ProbeSnapshot();
        Assert(before.SubmittedFrames > 0, "Probe control must submit a frame before full cleanup.");

        D3D_PaintBridge.CleanupD2DResources(D3DCacheCleanupLevel.ReleaseEverything, form);
        PumpUntil(() => D3D_PaintBridge.GetV5ProbeSnapshot().SubmittedFrames > before.SubmittedFrames);
        var after = D3D_PaintBridge.GetV5ProbeSnapshot();
        Assert(after.SubmittedFrames > before.SubmittedFrames,
            "A visible V5 control must resume frame submission after full cleanup.");
        D3D_PaintBridge.V5ProbeEnabled = false;
        form.Hide();
    }

    private static void VerifyCleanupRecoveryIncludesRegisteredSurface()
    {
        using var form = new Form();
        using var panel = new ModernPanel();
        form.Controls.Add(panel);
        form.Show();
        Application.DoEvents();
        _ = form.Handle;
        _ = panel.Handle;

        var assembly = typeof(MarkdownViewerCore).Assembly;
        var registry = assembly.GetType("LakeUI.D3D_ControlSurfaceRegistry")!;
        registry.GetMethod("获取或创建项目", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { panel });
        var presentation = assembly.GetType("LakeUI.D3D_V5Presentation")!;
        var getTargets = presentation.GetMethod("获取清理恢复目标", BindingFlags.Static | BindingFlags.NonPublic)!;
        var targets = (Control[])getTargets.Invoke(null, new object[] { form })!;
        Assert(targets.Any(target => ReferenceEquals(target, panel)),
            "Cleanup recovery must include registered visible V5 surfaces even without a presenter entry.");
        form.Hide();
    }

    private static void VerifyGeometryInvalidatesBackdropConsumers()
    {
        using var form = new Form();
        using var source = new ModernPanel();
        using var consumer = new ModernButton();
        form.Controls.Add(source);
        form.Controls.Add(consumer);
        _ = form.Handle;
        _ = source.Handle;
        _ = consumer.Handle;

        var registry = typeof(MarkdownViewerCore).Assembly.GetType("LakeUI.D3D_ControlSurfaceRegistry")!;
        var ensureEntry = registry.GetMethod("获取或创建项目", BindingFlags.Static | BindingFlags.NonPublic)!;
        ensureEntry.Invoke(null, new object[] { source });
        var consumerEntry = ensureEntry.Invoke(null, new object[] { consumer })!;
        var dirty = consumerEntry.GetType().GetField("Dirty", BindingFlags.Instance | BindingFlags.Public)!;
        var pendingDirty = consumerEntry.GetType().GetField("PendingDirty", BindingFlags.Instance | BindingFlags.Public)!;
        dirty.SetValue(consumerEntry, false);
        pendingDirty.SetValue(consumerEntry, Rectangle.Empty);

        var register = registry.GetMethod("注册依赖", BindingFlags.Static | BindingFlags.NonPublic)!;
        register.Invoke(null, new object[] { consumer, source, RectangleF.Empty });
        var geometryChanged = registry.GetMethod("控件几何已变化", BindingFlags.Static | BindingFlags.NonPublic)!;
        geometryChanged.Invoke(null, new object[] { source, EventArgs.Empty });
        Assert((bool)dirty.GetValue(consumerEntry)!,
            "A source geometry change must invalidate backdrop consumers.");
    }

    private static void VerifyFullCleanupRecreatesSharedFactories()
    {
        using var form = new Form();
        using var panel = new ModernPanel { Dock = DockStyle.Fill };
        using var 另一窗体 = new Form();
        using var 另一控件 = new CountingGpuControl { Dock = DockStyle.Fill };
        另一窗体.Controls.Add(另一控件);
        另一窗体.Show();
        form.Controls.Add(panel);
        form.Show();
        Application.DoEvents();

        var interop = typeof(MarkdownViewerCore).Assembly.GetType("LakeUI.D3D_D2DInterop")!;
        var factoryField = interop.GetField("_d2dFactory", BindingFlags.Static | BindingFlags.NonPublic)!;
        var before = factoryField.GetValue(null);
        Assert(before is not null, "V5 rendering must initialize the shared D2D factory.");
        var 原代次 = D3D_RenderCore.DeviceManager.DeviceGeneration;
        var 原设备 = D3D_RenderCore.DeviceManager.D3DDevice;
        var 文字工厂字段 = interop.GetField("_dwFactory", BindingFlags.Static | BindingFlags.NonPublic)!;
        var 原文字工厂 = 文字工厂字段.GetValue(null);
        var 另一控件次数 = 另一控件.RenderCount;
        var 原合成器 = D3D_RenderCore.GetWindowCompositor(form);
        using (var 源图 = new Bitmap(7, 5))
            原合成器.BackdropRenderer.SetImage(源图);
        var 图像字段 = typeof(D3D_BackdropRenderer).GetField("_image", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var 原快照 = (Image)图像字段.GetValue(原合成器.BackdropRenderer)!;

        D3D_PaintBridge.CleanupD2DResources(D3DCacheCleanupLevel.ReleaseEverything, form);
        Assert(D3D_RenderCore.DeviceManager.DeviceGeneration > 原代次,
            "ReleaseEverything must invalidate the shared device generation.");
        Assert(factoryField.GetValue(null) is null,
            "ReleaseEverything must release the old D2D factory before recovery.");
        Assert(文字工厂字段.GetValue(null) is null && 原设备.NativePointer == IntPtr.Zero,
            "Full reset must also release DirectWrite and the old D3D device.");
        PumpUntil(() => factoryField.GetValue(null) is not null);
        PumpUntil(() => 另一控件.RenderCount > 另一控件次数);
        Assert(!ReferenceEquals(before, factoryField.GetValue(null)),
            "Recovery must create a new D2D factory rather than reuse the old resource family.");
        Assert(文字工厂字段.GetValue(null) is not null && !ReferenceEquals(原文字工厂, 文字工厂字段.GetValue(null)),
            "Recovery must recreate DirectWrite together with the shared resource family.");
        Assert(ReferenceEquals(原合成器, D3D_RenderCore.GetWindowCompositor(form)) &&
               ReferenceEquals(原快照, 图像字段.GetValue(原合成器.BackdropRenderer)) && 原快照.Width == 7,
            "Full reset must retain the window service and its authoritative image snapshot.");
        form.Hide();
        另一窗体.Hide();
    }

    private static void PumpUntil(Func<bool> 条件)
    {
        var 等待 = System.Diagnostics.Stopwatch.StartNew();
        while (!条件() && 等待.ElapsedMilliseconds < 3000)
        {
            Application.DoEvents();
            System.Threading.Thread.Sleep(10);
        }
        Assert(条件(), "GPU recovery did not complete within three seconds.");
    }

    private static void VerifyRenderCacheBudgetCoordinator()
    {
        var lruCoordinator = new D3D_RenderCacheBudgetCoordinator();
        var newer = new FakeCacheOwner(400, 20);
        var older = new FakeCacheOwner(400, 10);
        lruCoordinator.Register(newer);
        lruCoordinator.Register(older);
        lruCoordinator.TrimToBudget(500, null!, null!);
        Assert(older.CacheBytes == 0 && newer.CacheBytes == 400,
            "Render cache budget must evict the globally oldest owner first.");

        var busyCoordinator = new D3D_RenderCacheBudgetCoordinator();
        var busy = new FakeCacheOwner(400, 1, canTrim: false);
        var evictable = new FakeCacheOwner(400, 2);
        busyCoordinator.Register(busy);
        busyCoordinator.Register(evictable);
        busyCoordinator.TrimToBudget(500, null!, null!);
        Assert(busy.TrimAttempts == 1 && busy.CacheBytes == 400 && evictable.CacheBytes == 0,
            "A temporarily busy oldest owner must not stop eviction of other eligible owners.");

        var protectedCoordinator = new D3D_RenderCacheBudgetCoordinator();
        var protectedOwner = new FakeCacheOwner(400, 1);
        var otherOwner = new FakeCacheOwner(400, 2);
        protectedCoordinator.Register(protectedOwner);
        protectedCoordinator.Register(otherOwner);
        protectedCoordinator.TrimToBudget(500, protectedOwner, null!);
        Assert(protectedOwner.CacheBytes == 400 && otherOwner.CacheBytes == 0,
            "The owner producing the current frame must remain protected while other caches are evicted.");

        var 注册协调器 = new D3D_RenderCacheBudgetCoordinator();
        var 第一项 = new FakeCacheOwner(400, 1);
        var 新增项 = new FakeCacheOwner(400, 2);
        注册协调器.Register(第一项);
        注册协调器.Register(第一项);
        Assert(注册协调器.TotalCacheBytes() == 400, "Registration must deduplicate by owner identity.");
        注册协调器.TrimToBudget(0, null!, () => 注册协调器.Register(新增项));
        Assert(第一项.TrimAttempts == 1 && 新增项.TrimAttempts == 1,
            "Owners registered during eviction must join the same maintenance pass.");
    }

    private static void VerifyTextureCacheLifecycle()
    {
        var 原预算 = GlobalOptions.GpuCacheBudgetBytes;
        using var 消息控件 = new Control();
        _ = 消息控件.Handle;
        using var 缓存 = new D3D_TextureCache();
        try
        {
            GlobalOptions.GpuCacheBudgetBytes = long.MaxValue;
            var 第一项 = 缓存.AcquireTexture("first", 1, 64, () => new TrackedResource());
            var 第二项 = 缓存.AcquireTexture("second", 1, 64, () => new TrackedResource());
            Assert(ReferenceEquals(第一项, 缓存.AcquireTexture("first", 1, 64, () => new TrackedResource())),
                "A texture hit must retain the original resource.");
            Assert(((D3D_IRenderCacheOwner)缓存).TrimOldest() && 第二项.DisposeCount == 1 && 第一项.DisposeCount == 0,
                "Linked texture LRU must evict the untouched resource.");
            var 新代次 = 缓存.AcquireTexture("first", 2, 64, () => new TrackedResource());
            Assert(第一项.DisposeCount == 1 && 新代次.DisposeCount == 0,
                "A generation change must replace and dispose the old resource once.");
            缓存.ReleaseAll();

            GlobalOptions.GpuCacheBudgetBytes = 64;
            缓存.BeginFrameUse();
            var 帧内旧项 = 缓存.AcquireTexture("older", 2, 64, () => new TrackedResource());
            var 帧内新项 = 缓存.AcquireTexture("newer", 2, 64, () => new TrackedResource());
            Assert(缓存.TotalGpuBytes == 128 && !((D3D_IRenderCacheOwner)缓存).TrimOldest(),
                "Active frame resources must not be evicted.");
            缓存.EndFrameUse();
            Assert(缓存.TotalGpuBytes == 128, "Frame completion must not synchronously scan or trim caches.");
            PumpUntil(() => 缓存.TotalGpuBytes == 64);
            Assert(帧内旧项.DisposeCount == 1 && 帧内新项.DisposeCount == 0,
                "Deferred UI maintenance must service a pending budget request in LRU order.");

            缓存.BeginFrameUse();
            缓存.Release("newer");
            Assert(缓存.TotalGpuBytes == 64 && 帧内新项.DisposeCount == 0,
                "Retired resources must remain counted and alive until frame completion.");
            缓存.EndFrameUse();
            PumpUntil(() => 缓存.TotalGpuBytes == 0);
            Assert(帧内新项.DisposeCount == 1, "A retired resource must be disposed exactly once.");
            var 超预算项 = 缓存.AcquireTexture("large", 2, 128, () => new TrackedResource());
            Assert(超预算项.DisposeCount == 0, "The resource being returned must survive even when over budget.");
            缓存.ReleaseAll();
            Assert(超预算项.DisposeCount == 1 && ((D3D_IRenderCacheOwner)缓存).OldestUseTick == long.MaxValue,
                "Explicit release must empty both texture storage and LRU state.");
        }
        finally
        {
            GlobalOptions.GpuCacheBudgetBytes = 原预算;
        }
    }

    private sealed class TrackedResource : IDisposable
    {
        private int _释放次数;
        public int DisposeCount => System.Threading.Volatile.Read(ref _释放次数);
        public void Dispose() => System.Threading.Interlocked.Increment(ref _释放次数);
    }

    private static void VerifyZeroCapacityDrawingCaches()
    {
        var 原画刷容量 = GlobalOptions.BrushCacheLimit;
        var 原格式容量 = GlobalOptions.TextFormatCacheLimit;
        using var 上下文 = D3D_RenderCore.DeviceManager.CreateDeviceContext();
        using var 画刷缓存 = new D3D_BrushCache();
        using var 文字缓存 = new D3D_TextRenderer(D3D_RenderCore.DeviceManager);
        using var 字体 = new Font("Segoe UI", 12);
        try
        {
            GlobalOptions.BrushCacheLimit = 0;
            GlobalOptions.TextFormatCacheLimit = 0;
            var 第一画刷 = 画刷缓存.GetSolidBrush(上下文, Color.Red, D3D_RenderCore.DeviceManager.DeviceGeneration);
            Assert(第一画刷.NativePointer != IntPtr.Zero, "Zero capacity must not dispose the returned brush.");
            var 第二画刷 = 画刷缓存.GetSolidBrush(上下文, Color.Blue, D3D_RenderCore.DeviceManager.DeviceGeneration);
            Assert(第一画刷.NativePointer == IntPtr.Zero && 第二画刷.NativePointer != IntPtr.Zero,
                "The next brush miss must release the previously protected resource.");
            var 获取格式 = typeof(D3D_TextRenderer).GetMethod("GetTextFormat", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var 第一格式 = (Vortice.DirectWrite.IDWriteTextFormat)获取格式.Invoke(文字缓存,
                new object[] { 字体, 1f, Vortice.DirectWrite.TextAlignment.Leading, Vortice.DirectWrite.ParagraphAlignment.Near, false, false })!;
            var 第二格式 = (Vortice.DirectWrite.IDWriteTextFormat)获取格式.Invoke(文字缓存,
                new object[] { 字体, 1f, Vortice.DirectWrite.TextAlignment.Center, Vortice.DirectWrite.ParagraphAlignment.Near, false, false })!;
            Assert(第一格式.NativePointer == IntPtr.Zero && 第二格式.NativePointer != IntPtr.Zero,
                "Zero-capacity text cache must retain only the format used by the current draw.");
        }
        finally
        {
            GlobalOptions.BrushCacheLimit = 原画刷容量;
            GlobalOptions.TextFormatCacheLimit = 原格式容量;
        }
    }

    private static void VerifyAnimationOwnerRelease()
    {
        using var 控件 = new Control();
        _ = 控件.Handle;
        using var 动画 = new D3D_AnimationHelper(控件);
        动画.StartFrameLoop((发送者, 事件) => { });
        动画.StopFrameLoop();
        var 调度器 = typeof(D3D_AnimationHelper).GetField("_threadScheduler", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        Assert(调度器.GetType().GetField("_syncOwner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(调度器) is null,
            "An idle animation scheduler must not retain its former UI owner.");
    }

    private static void VerifyV5DirtyRetryAndResetContracts()
    {
        using var 窗体 = new Form();
        using var 控件 = new CountingGpuControl { Dock = DockStyle.Fill };
        窗体.Controls.Add(控件);
        窗体.Show();
        Application.DoEvents();
        var 原次数 = 控件.RenderCount;
        var 越界区域 = new Rectangle(控件.Width + 10, 10, 5, 5);
        D3D_InvalidationRouter.RequestRender(控件, 越界区域);
        D3D_ControlSurfaceRegistry.MarkDirty(控件, 越界区域);
        D3D_V5Presentation.RequestRender(控件, 越界区域);
        Application.DoEvents();
        Assert(!D3D_ControlSurfaceRegistry.IsDirty(控件) && 控件.RenderCount == 原次数,
            "Completely outside dirty rectangles must be discarded by every V5 entry point.");
        D3D_V5Presentation.RequestRender(控件, Rectangle.Empty);
        Assert(控件.RenderCount > 原次数, "A missing dirty rectangle must still request a full render.");
        原次数 = 控件.RenderCount;
        typeof(D3D_V5Presentation).GetMethod("排队重试", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { 控件 });
        var 重试 = (IDictionary)typeof(D3D_V5Presentation).GetField("_retryTimers", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        PumpUntil(() => !重试.Contains(控件));
        Assert(控件.RenderCount == 原次数, "A presentation retry must reuse an unchanged surface.");

        var 原代次 = D3D_RenderCore.DeviceManager.DeviceGeneration;
        控件.DuringRender = 绘制上下文 =>
        {
            控件.DuringRender = null;
            D3D_PaintBridge.CleanupD2DResources(D3DCacheCleanupLevel.ReleaseEverything, 窗体);
            Assert(D3D_RenderCore.DeviceManager.DeviceGeneration == 原代次 && 绘制上下文.DeviceContext.NativePointer != IntPtr.Zero,
                "Cleanup requested inside RenderGpu must wait until the active paint ends.");
        };
        D3D_V5Presentation.RequestRender(控件);
        PumpUntil(() => D3D_RenderCore.DeviceManager.DeviceGeneration > 原代次);
        Application.DoEvents();
        原次数 = 控件.RenderCount;
        原代次 = D3D_RenderCore.DeviceManager.DeviceGeneration;
        D3D_RenderCore.DeviceManager.InvalidateDevice();
        PumpUntil(() => 控件.RenderCount > 原次数 && !重试.Contains(控件));
        Assert(D3D_RenderCore.DeviceManager.DeviceGeneration > 原代次,
            "Device-lost notification must recover the visible control on a new generation.");
        for (var 次数 = 0; 次数 < 5; 次数++)
        {
            控件.Hide();
            控件.Show();
            Application.DoEvents();
        }
        控件.RebuildHandle();
        Application.DoEvents();
        原次数 = 控件.RenderCount;
        D3D_V5Presentation.RequestRender(控件);
        Assert(控件.RenderCount > 原次数, "Handle recreation must restore rendering.");
        控件.Dispose();
        var 订阅 = typeof(D3D_V5Presentation).GetField("_已订阅控件", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        Assert(!(bool)订阅.GetType().GetMethod("Contains")!.Invoke(订阅, new object[] { 控件 })!,
            "Disposed controls must leave the presentation subscription registry.");
        窗体.Hide();
    }

    private sealed class CountingGpuControl : Control, D3D_IGpuRenderable, V5_IGpuPresentationSource
    {
        public int RenderCount { get; private set; }
        public Action<D3D_PaintContext>? DuringRender;
        public void RebuildHandle() => RecreateHandle();
        public void RenderGpu(D3D_PaintContext 上下文)
        {
            RenderCount++;
            DuringRender?.Invoke(上下文);
        }
        protected override void OnPaint(PaintEventArgs 事件) => D3D_V5Presentation.Paint(this, this);
    }

    private static void VerifyGlobalBudgetProperties()
    {
        var oldGpuBudget = GlobalOptions.GpuCacheBudgetBytes;
        var oldCpuBudget = GlobalOptions.CpuCacheBudgetBytes;
        try
        {
            GlobalOptions.GpuCacheBudgetBytes = long.MaxValue;
            var gpuOwner = new FakeCacheOwner(700, D3D_GpuCache.NextTick());
            D3D_GpuCache.Register(gpuOwner);
            GlobalOptions.GpuCacheBudgetBytes = 600;
            Assert(gpuOwner.CacheBytes == 0,
                "Lowering the global GPU budget must immediately trim existing cache owners.");

            GlobalOptions.CpuCacheBudgetBytes = long.MaxValue;
            var cpuOwner = new FakeCacheOwner(700, D3D_CpuCache.NextTick());
            D3D_CpuCache.Register(cpuOwner);
            GlobalOptions.CpuCacheBudgetBytes = 600;
            Assert(cpuOwner.CacheBytes == 0,
                "Lowering the global CPU budget must immediately trim existing cache owners.");

            GlobalOptions.GpuCacheBudgetBytes = -1;
            GlobalOptions.CpuCacheBudgetBytes = -1;
            Assert(GlobalOptions.GpuCacheBudgetBytes == 0 && GlobalOptions.CpuCacheBudgetBytes == 0,
                "Negative global cache budgets must normalize to zero.");
        }
        finally
        {
            GlobalOptions.GpuCacheBudgetBytes = oldGpuBudget;
            GlobalOptions.CpuCacheBudgetBytes = oldCpuBudget;
        }
    }

    private sealed class FakeCacheOwner : D3D_IRenderCacheOwner
    {
        private readonly bool _canTrim;

        public FakeCacheOwner(long cacheBytes, long oldestUseTick, bool canTrim = true)
        {
            CacheBytes = cacheBytes;
            OldestUseTick = oldestUseTick;
            _canTrim = canTrim;
        }

        public long CacheBytes { get; private set; }
        public long OldestUseTick { get; }
        public int TrimAttempts { get; private set; }

        public bool TrimOldest()
        {
            TrimAttempts++;
            if (!_canTrim || CacheBytes <= 0) return false;
            CacheBytes = 0;
            return true;
        }

        public void ReleaseAll()
        {
            CacheBytes = 0;
        }
    }

    private static void VerifyFenceParsing()
    {
        var parser = new MarkdownViewerCore.MarkdownParser();
        var markdown = "```csharp\npublic class Sample { }\n```\n\n```mermaid\nsequenceDiagram\nparticipant Client\nparticipant API\nClient->>API: Request\nAPI-->>Client: Response\n```";
        var document = parser.Parse(markdown);
        var codeBlock = document.Blocks[0];
        Assert(codeBlock.Kind == MarkdownViewerCore.BlockKind.CodeBlock, "Expected a fenced code block.");
        Assert(codeBlock.Language == "csharp", "Expected csharp fence language.");
        var mermaidBlock = document.Blocks.Find(block => block.Language == "mermaid");
        Assert(mermaidBlock is not null && mermaidBlock.IsMermaidSequenceDiagram, "Expected Mermaid sequence diagram recognition.");

        var emptySequence = parser.Parse("```mermaid\nsequenceDiagram\nparticipant Client as Browser\n```").Blocks[0];
        Assert(emptySequence.IsMermaidSequenceDiagram, "A participant-only Mermaid sequence diagram must still use the sequence renderer.");
    }

    private static void VerifyBuiltInHighlighters()
    {
        var cases = new (string Language, string Line)[]
        {
            ("csharp", "public class Sample { return 42; }"),
            ("vbnet", "Public Class Sample : End Class"),
            ("cpp", "class Sample { public: int value; };"),
            ("c", "static int value = 42;"),
            ("python", "def sample(value): return value"),
            ("java", "public sealed class Sample<T> { private int value = 0x2A; return true; }"),
            ("xml", "<item id=\"42\">&amp;</item>"),
            ("html", "<!doctype html><main class=\"content\">Hello</main>"),
            ("vb6", "Private Sub Sample(): End Sub"),
            ("json", "{ \"value\": true, \"count\": 42 }"),
            ("asm", "mov eax, 42 ; load value")
        };

        foreach (var test in cases)
        {
            var highlighter = CodeSyntaxHighlighterRegistry.GetHighlighter(test.Language);
            Assert(highlighter is not null, $"Missing built-in highlighter for {test.Language}.");
            var result = highlighter!.HighlightLine(0, test.Line, 0);
            Assert(result.Tokens is { Count: > 0 }, $"Expected color tokens for {test.Language}.");
        }

        Assert(CodeSyntaxHighlighterRegistry.GetHighlighter("javascript") is null, "Unsupported languages must not receive implicit built-in highlighting.");

        var java = CodeSyntaxHighlighterRegistry.GetHighlighter("java")!;
        var comment = java.HighlightLine(0, "/* Java block", 0);
        Assert(comment.EndState == 1 && comment.Tokens.Count == 1, "Java block comments must carry state across lines.");
        var commentEnd = java.HighlightLine(1, " comment */ int value = 42;", comment.EndState);
        Assert(commentEnd.EndState == 0 && commentEnd.Tokens.Count >= 2, "Java block comments must resume normal scanning after closing.");
        var textBlock = java.HighlightLine(0, "String json = \"\"\"", 0);
        Assert(textBlock.EndState == 2, "Java text blocks must carry a dedicated multiline state.");
        var textBlockEnd = java.HighlightLine(1, "{\"value\": 1}\"\"\";", textBlock.EndState);
        Assert(textBlockEnd.EndState == 0 && textBlockEnd.Tokens.Count == 1, "Java text block content must remain a single string token.");

        VerifyCurrentLanguageKeywords();
        VerifyMarkupHighlighting();
    }

    private static void VerifyCurrentLanguageKeywords()
    {
        AssertHighlightedWords("csharp", "file extension Sample { required string Name { get; init; } field = value; }", "file", "extension", "required", "init", "field");
        AssertHighlightedWords("cpp", "template<class T> concept Value = requires(T value) { value; }; co_await task;", "template", "concept", "requires", "co_await");
        AssertHighlightedWords("c", "constexpr typeof_unqual(int) value = nullptr;", "constexpr", "typeof_unqual", "nullptr");
        AssertHighlightedWords("python", "type Alias = int | None", "type", "int", "None");
        AssertHighlightedWords("python", "assert value is not None", "assert", "is", "not", "None");
        AssertHighlightedWords("vbnet", "Public Async Iterator Function Values() As IEnumerable(Of Integer)", "Async", "Iterator", "Function", "Integer");
    }

    private static void VerifyMarkupHighlighting()
    {
        foreach (var alias in new[] { "xml", "xsd", "xsl", "xslt", "html", "htm", "xhtml", "svg" })
            Assert(CodeSyntaxHighlighterRegistry.GetHighlighter(alias) is not null, $"Missing markup highlighter alias {alias}.");

        var xml = CodeSyntaxHighlighterRegistry.GetHighlighter("xml")!;
        var tag = xml.HighlightLine(0, "<book", 0);
        Assert(tag.EndState == 2, "An XML start tag must carry its state across lines.");
        var tagEnd = xml.HighlightLine(1, " id=\"42\">Text &amp;</book>", tag.EndState);
        Assert(tagEnd.EndState == 0 && tagEnd.Tokens.Count >= 7, "XML attributes, entities, and closing tags must be highlighted.");

        var comment = xml.HighlightLine(0, "<!-- comment", 0);
        Assert(comment.EndState == 1, "XML/HTML comments must carry state across lines.");
        Assert(xml.HighlightLine(1, "continued -->", comment.EndState).EndState == 0, "XML/HTML comment state must end at -->.");
        var cdata = xml.HighlightLine(0, "<![CDATA[<not-a-tag>", 0);
        Assert(cdata.EndState == 6, "XML CDATA sections must use their own multiline state.");
        Assert(xml.HighlightLine(1, "]]>", cdata.EndState).EndState == 0, "XML CDATA state must end at ]]>.");

        var opening = CodeIndentationAnalyzer.Analyze("html", "<main>", 0);
        var voidElement = CodeIndentationAnalyzer.Analyze("html", "<img src=\"cover.png\">", opening.NextIndentLevel);
        var closing = CodeIndentationAnalyzer.Analyze("html", "</main>", voidElement.NextIndentLevel);
        Assert(opening.NextIndentLevel == 1 && voidElement.NextIndentLevel == 1 && closing.NextIndentLevel == 0,
            "HTML indentation must handle container and void elements.");
    }

    private static void AssertHighlightedWords(string language, string line, params string[] words)
    {
        var result = CodeSyntaxHighlighterRegistry.GetHighlighter(language)!.HighlightLine(0, line, 0);
        foreach (var word in words)
            Assert(result.Tokens.Any(token => line.Substring(token.StartCol, token.Length) == word),
                $"Expected {language} keyword '{word}' to be highlighted.");
    }

    private static void VerifyCustomHighlighterRegistration()
    {
        var replacement = new SingleTokenHighlighter();
        CodeSyntaxHighlighterRegistry.Register(replacement, "csharp");
        Assert(ReferenceEquals(CodeSyntaxHighlighterRegistry.GetHighlighter("csharp"), replacement), "Custom registration must override a built-in language mapping.");
        var result = replacement.HighlightLine(0, "custom", 0);
        Assert(result.Tokens.Count == 1 && result.Tokens[0].ForeColor == Color.Magenta, "Custom highlighter result was not preserved.");
    }

    private static void VerifySyntaxIndentation()
    {
        var first = CodeIndentationAnalyzer.Analyze("csharp", "        if (ready) {", 0);
        Assert(first.Text == "if (ready) {" && first.IndentLevel == 0 && first.NextIndentLevel == 1,
            "C# indentation must be syntax-derived instead of source-whitespace-derived.");
        var closing = CodeIndentationAnalyzer.Analyze("csharp", "\t}", first.NextIndentLevel);
        Assert(closing.Text == "}" && closing.IndentLevel == 0 && closing.NextIndentLevel == 0,
            "Closing braces must reduce syntax indentation.");
        var structure = CodeIndentationAnalyzer.Analyze("vbnet", "Public Structure CodeIndentationResult", 0);
        Assert(structure.IndentLevel == 0 && structure.NextIndentLevel == 1,
            "VB.NET declarations with access modifiers must open a syntax indentation level.");
        var structureEnd = CodeIndentationAnalyzer.Analyze("vbnet", "End Structure", structure.NextIndentLevel);
        Assert(structureEnd.IndentLevel == 0 && structureEnd.NextIndentLevel == 0,
            "VB.NET End Structure must close the syntax indentation level.");
        var plain = CodeIndentationAnalyzer.Analyze("", "    plain", 3);
        Assert(plain.Text == "plain", "Indentation analyzer should still normalize text for an active custom highlighter.");
    }

    private static void VerifyRenderedIndentationOffset()
    {
        using var viewer = new MarkdownViewerCore
        {
            EmbeddedContentMode = true,
            Width = 640,
            CodeIndentSize = 4
        };
        viewer.SetMarkdownImmediate("```csharp\nif (ready) {\nreturn;\n}\n```");
        var field = typeof(MarkdownViewerCore).GetField("_visualLines", BindingFlags.Instance | BindingFlags.NonPublic);
        var lines = (IList?)field?.GetValue(viewer);
        Assert(lines is { Count: >= 3 }, "Expected laid out code lines.");
        var firstFragments = (IList?)lines![0]!.GetType().GetField("Fragments")!.GetValue(lines[0]);
        var nestedFragments = (IList?)lines[1]!.GetType().GetField("Fragments")!.GetValue(lines[1]);
        var xField = firstFragments![0]!.GetType().GetField("X")!;
        var firstX = (int)xField.GetValue(firstFragments[0])!;
        var nestedX = (int)xField.GetValue(nestedFragments![0])!;
        Assert(nestedX > firstX, "Syntax indentation must change the rendered fragment X position.");
    }

    private static void VerifyMermaidCopyText()
    {
        using var viewer = new MarkdownViewerCore { EmbeddedContentMode = true, Width = 640 };
        viewer.SetMarkdownImmediate("```mermaid\nsequenceDiagram\nparticipant Client\nparticipant API\nClient->>API: Request\n```");
        var selectAll = typeof(MarkdownViewerCore).GetMethod("SelectAllEmbeddedText", BindingFlags.Instance | BindingFlags.NonPublic);
        selectAll!.Invoke(viewer, null);
        var selected = viewer.GetSelectedText();
        Assert(selected.Contains("sequenceDiagram") && selected.Contains("Client->>API: Request"),
            "Mermaid source text must be available through the existing copy selection path.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class SingleTokenHighlighter : ICodeSyntaxHighlighter
    {
        public CodeSyntaxHighlightResult HighlightLine(int lineIndex, string lineText, int previousLineState)
        {
            return new CodeSyntaxHighlightResult(new List<CodeSyntaxToken> { new(0, lineText.Length, Color.Magenta) }, 0);
        }
    }
}
