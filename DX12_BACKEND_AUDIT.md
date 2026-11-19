# LakeUI DX12 Backend Audit

Audit baseline: 2026-11-18. Acceptance updated on 2026-11-19. Native control entry points use `ID3D12Renderable.RenderGpu12`. Probe commands in this record describe historical runs before the requested cleanup.

## Probe Baseline

- `dotnet build LakeUI.Tests/LakeUI.Tests.csproj --no-restore`: passed, 0 warnings/errors.
- `dotnet run --project LakeUI.Tests/LakeUI.Tests.csproj --no-build --no-restore`: passed. The suite performs numeric GPU readback comparisons; it does not use screenshots or visual inspection.
- `dotnet run --project LakeUI.Tests/LakeUI.Tests.csproj --no-build --no-restore -- --d3d12-device-smoke`: passed on the current machine. It initialized a native D3D12 device, DIRECT queue, allocator, and completed a fence signal/wait.
- `--d3d12-presenter-smoke`: passed. A DIRECT-queue composition swap chain attached to a real WinForms HWND, resized, fully initialized both flip buffers, applied a clipped partial update, and rendered convex and concave simple polygons in both windings, a rotated/translated concave polygon, a native half-alpha rectangle, a vertical gradient, an antialiased rounded rectangle and a rectangle stroke into both. Singular transforms and self-intersecting star/bow-tie paths, degenerate/zero-length-edge and non-finite polygons were rejected. The test copied both actual swap-chain images through D3D12 readback heaps, honored `GetCopyableFootprints` row pitch, and compared all 49,152 BGRA pixels numerically, including transformed coverage, preservation and alpha blending. No screenshot or visual check was used.
- `--backend-clear-benchmark`: same NVIDIA adapter for both paths, 320x240 solid fill, 16 warm-up and 180 measured frames. Latest run: DX12 render/published-interval P95 were 0.4%/0.5% above DX11; P99 were 1.0%/1.8% above, all under the 10% gates. DX12 local-memory peak delta was 13,852 KiB vs DX11 12,016 KiB. This is only an early clear-pass comparison, not a final full-workload performance acceptance.
- The normal test process asserts zero device-factory attempts before any test work and zero D3D12 attempts throughout the DX11 regressions.

## Cold-start and frame probes

`LakeUI.Tests/BackendStartup.Demo.ps1` runs five fresh Form1 processes. Default and explicit DX11 each use one foreground DX11 device. The DX12 solid, tab-strip and complete homepage each use one DX12 device with zero foreground/background DX11 attempts. Each process entered Load with an empty factory history. The homepage runs the normal Attach and lazy registration sequence, selects page 1 and checks publication counters for every visible GPU participant, including Markdown and five chrome HWNDs.

`--startup-recovery` injects failure after the native control records its first frame. Explicit DX11 selection then publishes on the original HWND. `--startup-late-attach` exercises the unconfigured DX11 default after Shown. `--dx12-control-frame` compares twelve successive full frames on a 73×39 child HWND with a moving translucent rectangle, checks one Present per frame and verifies recovery from a cancelled recording. The atlas probe checks same-host multi-HWND batches.

## Render Entry Inventory

Dependencies are grouped as follows: **G** = primitive/geometry and brush caches; **T** = DirectWrite layout and text rasterization; **I** = decoded image and GPU image cache; **S** = per-control surface, dirty-region and presentation; **B** = backdrop/cross-window source; **P** = popup/overlay HWND lifecycle; **A** = animation/timer state. CPU-only model, layout, input and decoding may be shared; all device-dependent resources must be recreated per backend and generation.

| Existing DX11 entry | DX12 entry to add | Dependencies | Probe acceptance |
|---|---|---|---|
| `无界面组件/Ponder.vb` | `D3D12/PonderRenderer` | G, T, P, A | Connector, node and button pixels; hit-test and hover invalidation parity. |
| `D3D11GpuEngine/D3D_ControlContracts.vb` | Extend contracts with an explicit backend-renderer contract | S | Missing DX12 renderer is reported; never dispatch to `RenderGpu`. |
| `D3D11GpuEngine/D3D_ControlSurfaceRegistry.vb` | Backend-neutral CPU dependency graph plus DX12 surface registry | S, B | Dirty propagation order and mapped rectangles match numeric reference. |
| `D3D11GpuEngine/FloatingToolTipForm.vb` | `D3D12/FloatingToolTipRenderer` | G, T, I, P | Tooltip creation, text/selection pixels, close and HWND lifecycle. |
| `D3D11GpuEngine/MarkdownViewerCore.vb` | `D3D12/MarkdownViewerRenderer` | G, T, I, S, A | Paragraph, table, code, image, selection and scroll pixel/input parity. |
| `DX 对话框/ExFloatingBox.vb` | `D3D12/ExFloatingBoxRenderer` | G, T, I, P | Popup frame, image, text, resize and owner-close lifecycle. |
| `DX 对话框/ExFloatingTip.vb` | `D3D12/ExFloatingTipRenderer` | G, T, P, A | Timed show/hide, placement and alpha pixel parity. |
| `DX 对话框/ExInputBox.vb` | `D3D12/ExInputBoxRenderer` | G, T, P | Input, selection, caret, IME and button-state parity. |
| `DX 对话框/ExMsgBox.vb` | `D3D12/ExMsgBoxRenderer` | G, T, P | Text wrapping, button results, focus and close lifecycle. |
| `DX 对话框/ExOverlayMsgBox.vb` | `D3D12/ExOverlayMsgBoxRenderer` | G, T, P, B | Overlay composition and underlying-window pixel preservation. |
| `DX 对话框/ModernColorDialog.vb` | `D3D12/ModernColorDialogRenderer` | G, T, I, P | Picker, alpha, swatch and keyboard interaction parity. |
| `DX 对话框/ModernFontDialog.vb` | `D3D12/ModernFontDialogRenderer` | G, T, P | Font sample, list, selection and text-quality probe parity. |
| `DX 控件/AgentDialogueList.vb` | `D3D12/AgentDialogueListRenderer` | G, T, I, S, A | Streaming text, message rows, selection and scroll parity. |
| `DX 控件/AgentRoom.vb` | `D3D12/AgentRoomRenderer` | G, T, I, S, A | Conversation state, status and incremental-update parity. |
| `DX 控件/BooleanSwitch.vb` | `D3D12/BooleanSwitchRenderer` | G, T, S, A | On/off, hover and animation reference pixels. |
| `DX 控件/BreadcrumbNavigationBar.vb` | `D3D12/BreadcrumbNavigationBarRenderer` | G, T, S | Segment clipping, truncation, hover and click parity. |
| `DX 控件/CpuMonitor.vb` | `D3D12/CpuMonitorRenderer` | G, T, S, A | History values, grid and resize/scroll numeric pixel parity. |
| `DX 控件/EasyStatesPanel.vb` | `D3D12/EasyStatesPanelRenderer` | G, T, S | State transitions and child clipping parity. |
| `DX 控件/ExcellentProgressBar.vb` | `D3D12/ExcellentProgressBarRenderer` | G, T, S, A | Progress endpoints, indeterminate animation and alpha parity. |
| `DX 控件/ExcellentTrackBar.vb` | `D3D12/ExcellentTrackBarRenderer` | G, T, S, A | Thumb/track values, drag, focus and hover parity. |
| `DX 控件/HtmlColorLabel.vb` | `D3D12/HtmlColorLabelRenderer` | G, T, S | Parsed color runs, clipping and link-state parity. |
| `DX 控件/JustEmptyControl.vb` | `D3D12/JustEmptyControlRenderer` | S | Empty/transparent surface and lifecycle behavior. |
| `DX 控件/MemberWall.vb` | `D3D12/MemberWallRenderer` | G, T, I, S | Member image, text, spacing and scroll parity. |
| `DX 控件/ModernButton.vb` | `D3D12/ModernButtonRenderer` | G, T, I, S, A | State matrix, icon/text, rounded edges and disabled alpha. |
| `DX 控件/ModernCheckBox.vb` | `D3D12/ModernCheckBoxRenderer` | G, T, S, A | Checked/indeterminate/focus and input parity. |
| `DX 控件/ModernComboBox.vb` | `D3D12/ModernComboBoxRenderer` | G, T, I, S, P | Closed/open popup, selected row, scroll and owner HWND behavior. |
| `DX 控件/ModernContextMenu.vb` | `D3D12/ModernContextMenuRenderer` | G, T, I, P | Menu placement, selection, nested items and dismissal parity. |
| `DX 控件/ModernListBox.vb` | `D3D12/ModernListBoxRenderer` | G, T, I, S | Selection, hover, scroll and item virtualization parity. |
| `DX 控件/ModernNumericUpDown.vb` | `D3D12/ModernNumericUpDownRenderer` | G, T, S, A | Value text, spinner states, bounds and keyboard parity. |
| `DX 控件/ModernPanel.vb` | `D3D12/ModernPanelRenderer` | G, I, S, B | Transparent/background-source pixels and child order parity. |
| `DX 控件/ModernTabControl.vb` | `D3D12/ModernTabControlRenderer` | G, T, I, S, A | Selection, scroll, close buttons and lazy page lifecycle. |
| `DX 控件/ModernTabListControl.vb` | `D3D12/ModernTabListControlRenderer` | G, T, I, S, A | Selected index, clipped strip, scroll and bound-page lifecycle. |
| `DX 控件/ModernTextBox.vb` | `D3D12/ModernTextBoxRenderer` | G, T, I, S | Selection, caret, IME, password and text quality probes. |
| `DX 控件/PixelPictureBox.vb` | `D3D12/PixelPictureBoxRenderer` | I, S | Tile uploads, decode cancellation, zoom and color-format parity. |
| `DX 控件/ProgressRing.vb` | `D3D12/ProgressRingRenderer` | G, S, A | Rotation, stroke, resize and frame cadence probes. |
| `DX 控件/QuantumSwitch.vb` | `D3D12/QuantumSwitchRenderer` | G, T, S, A | State, animation, hover and text pixels. |
| `DX 控件/RamMonitor.vb` | `D3D12/RamMonitorRenderer` | G, T, S, A | History buffer, curve, labels and resize numeric parity. |
| `DX 控件/RoundDashBoard.vb` | `D3D12/RoundDashBoardRenderer` | G, T, S, A | Arc endpoints, labels, scale and alpha parity. |
| `DX 控件/ThisIsYourWindow.vb` | `D3D12/ThisIsYourWindowRenderer` | G, T, I, B, P | Window chrome, backdrop and overlay interaction without HWND changes. |
| `DX 控件/Ultra2DChart.vb` | `D3D12/Ultra2DChartRenderer` | G, T, I, S, A | Plot, axis, legend, geometry-cache and SSAA pixel parity. |
| `DX 控件/UltraDetailListView.vb` | `D3D12/UltraDetailListViewRenderer` | G, T, I, S, A | Rows, headers, selection, checkbox, scroll and virtualization parity. |

## Shared Engine Paths

| Existing path | Required DX12 counterpart | Current risk / required probe |
|---|---|---|
| `D3D_DeviceManager` and `D3D_DeviceGlobals` | Independent D3D12 device owner and generation | Cold-start probes counted both DX11 creation sites and verified the device gate before selection. |
| `D3D_HwndCompositionPresenter`, `D3D_ControlSurface`, `D3D_V5Presentation` | Native DX12 surface/presenter and batch publisher | A native DIRECT-queue composition chain now binds a HWND, resizes, tracks two allocator/RTV/back-buffer sets, clears and presents both buffers, commits, and fence-retires resources. Numeric GPU readback verifies full initialization, clipped partial-region changes and unchanged pixels outside the region on both buffers. The same-host multi-HWND probe also verified one flip per batch, alpha, and partial-update preservation. |
| `D3D_PaintContext`, brush/geometry/image/text caches | DX12-specific context and caches; no D2D/DX11 resource reuse | A D3D12 triangle PSO now renders simple polygons via ear clipping, a transformed polygon prototype, solid/alpha rectangles, rectangle strokes, antialiased rounded rectangles and a vertical linear gradient using root constants. Numeric readback checks convex/concave polygon coverage in both windings, transformed coverage, singular-transform and invalid-path rejection, clipped edges, alpha blend, stroke/rounded transition pixels and gradient interpolation. The remaining gap here is general transform and per-control parity coverage. |
| `D3D_RectangleRenderer` / primitive helpers | DX12 graphics PSO, shader compilation, root constants and geometry upload | Native solid rectangle probe checks exact coverage, clipped scissor, premultiplied alpha and preservation across both flip buffers. Other primitive types and cache/lifetime budgets remain to be added. |
| `D3D_BackdropRenderer`, `D3D_BackdropSurfaceRenderer`, `D3D_BackgroundPenetration` | DX12 effect, sampling and cross-window-source pipeline | Assert no `D3D_DeviceGlobals` calls while DX12 is selected; compare source revisions and readback bytes. |
| device reset, cleanup and async frame production | Generation/fence-owned retirement and stale-result rejection | Inject loss, timeout and resize/cancel races; assert no submission from retired generations. |

## Current Implementation State

- The native DX12 route is opt-in through `GlobalOptions.SetRenderBackendAsync`; DX11 is the application default. Transient upload memory and shader-visible SRV pages are reused behind frame fences. A blocked-queue probe verified deferred retirement after the old device manager is disposed. The HWND atlas shares one composition chain per top-level host and tracks slot revisions per buffer. The two-HWND probe verified eight single-flip batches, a resize, and preservation of an unchanged slot. Hardware scanout and atomicity across separate top-level hosts remain outside that probe's coverage.
- GPU readback verified SSAA 1× through 4×, BGRA8, FP16 blending with SDR resolve, mixed SDR/scRGB output, and values above 1 in the FP16 swap chain. The HDR tiled-image DEMO page published through DX12. Third-party picture sources need the `IPixelPictureSource12` contract.
- DirectWrite grayscale, aliased and outline text use the DX12 compositor. Outline coverage uses device-independent CPU geometry and is cached separately from text color. ClearType uses separate RGB coverage and dual-source blending when the draw region has an opaque SDR background at 1×. The text-quality probe verifies color and opacity changes against the per-channel blend equation. Transparent, transformed, supersampled and floating-point text uses grayscale coverage in ClearType mode. Fonts, mixed-script rendering and per-control parity need broader probe coverage.
- The five cold-start scenarios and all 40 registered lazy pages passed. Numeric readback covered rotated chart labels, the vertical axis title, and series composition. Popup probes published combo, context menu, message, input, color, font, floating box, floating tip, and overlay forms. The floating box returned the selected button and closed. An occluding window exercised real desktop capture with temporary and persistent exclusion, two source revisions, DX12 blur, and restoration of the previous display affinity.
- Both DEMO switch directions published on the original HWNDs. Synthetic probes covered candidate rejection, bidirectional first-frame rollback, request coalescing and new device generations. Native DX12 device removal recovered on the original HWND. The 100-switch fixture on RTX 5060 Ti measured 20,021,248 / 21,848,064 / 20,021,248 bytes of process-local VRAM at start / peak / end. Switch P95 was 114.5 ms in the November 19 rerun on that fixture. The DEMO round trips measured individual switches of 149–271 ms.

## Earlier acceptance results before probe removal

On November 19, 2026, the Release acceptance run compared the same DEMO homepage and license-page transitions on an RTX 5060 Ti, driver 32.0.16.2124. Each of three paired rounds alternated backend order and independently launched each backend. After 16 warm-up transitions, 64 measured inputs included four scheduled image arrivals. Every process loaded all four images. Both backends sampled PCIe Gen4 ×8. Device-wide sampled peak GPU utilization was 4–10%, and peak device-wide VRAM use was 1,685–1,998 MiB. Link state and sampled utilization do not measure PCIe throughput.

| Metric | DX11 | DX12 |
|---|---:|---:|
| Input to publication P95 | 42.09 ms | 20.90 ms |
| Input to publication P99 | 51.80 ms | 39.74 ms |
| Publication interval P95 | 49.38 ms | 26.00 ms |
| Publication interval P99 | 53.84 ms | 41.88 ms |
| Time to first frame | 507.69 ms | 636.90 ms |
| Peak local VRAM delta | 93,687,808 B | 110,551,040 B |

The table reports the median of each metric across the three runs per backend. The four latency metrics passed the 10% regression gate. DX12 first-frame time was 25.5% higher and process-local peak VRAM delta was 18.0% higher. The preceding three-round, 256-input tab-strip benchmark passed the same gate. Its input P99 was 9.47 ms on DX11 and 8.76 ms on DX12, and publication-interval P99 was 8.74 ms for both. An earlier controlled page run reproduced DX12 input and publication P99 regressions of 66.8% and 39.6%. The renderer now bins outline crossings by subpixel scanline and reuses glyph coverage across integer translations. An attempted network-dependent run produced inconsistent badge inputs and was excluded. Game-concurrent performance of the final revision remains unmeasured.

The acceptance scope covers software publication and numeric GPU readback on the tested machine. Hardware scanout timing, cross-top-level-window atomicity, and per-control pixel parity for every supported input remain outside that coverage.

The task-specific capture fixtures, failure injection points, timing instrumentation, demo report writer, and benchmark scripts were removed after acceptance. The source retains the backend implementations and public selection, first-frame, and switch diagnostics. Both Release projects built with zero warnings and errors after cleanup. A temporary public-API smoke test then verified default DX11 startup, explicit DX12 selection in Load, and both switch directions from each startup backend on the original HWNDs. The original DX11 regression suite passed again. The DEMO then passed the queued-switch race from both starting backends, with resize, lazy-page selection, and backdrop changes applied before the update transaction closed. Closing the primary HWND while a switch was queued completed the request with `NoVisibleParticipants`. The temporary race fixture was removed and the clean DEMO rebuilt. Commands and thresholds described earlier in this audit refer to the pre-cleanup fixtures.

## Final follow-up

The native WinForms form-source path passed 864 numeric BGRA comparisons across two source revisions and a clipped offset. The final instrumented page comparison after the form-source and chrome changes passed the 10% latency gate. It recorded 27.76 vs 29.04 ms for the controlled-arrival input P99 and 32.01 vs 17.80 ms for the steady input P99, in DX11/DX12 order. See [DX12 performance report](F:/Git/LakeUI/DX12_PERFORMANCE_REPORT.md) for timings, hardware conditions and optimization priorities. Temporary source and measurement hooks were removed again after this run.
