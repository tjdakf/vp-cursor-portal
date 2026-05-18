# AGENTS.md — H2 Cursor Router Project Guide

This file is the working guide for agents and developers building **H2 Cursor Router**.
Keep this file at the repository root so Codex can use it as project-specific guidance.

---

## 1. Project summary

**H2 Cursor Router** is a Windows desktop application for a single Windows PC connected to a NovaStar H Series / H2 video processor.

The program links two independent concepts:

1. **H2 preset control** — send a preset load command to the H2.
2. **Windows cursor routing** — restrict and tunnel the mouse cursor according to the visual output layout shown by the video processor.

The app exists because Windows may think monitors are arranged like this:

```text
[Monitor 1] [Monitor 2] [Monitor 3] [Monitor 4]
```

But the H2 output preset may show only this:

```text
[Monitor 1] [Monitor 3]
```

In that case, the cursor should not disappear into Monitor 2. When the user moves from the right edge of Monitor 1, the cursor should jump to the left edge of Monitor 3, using ratio-based coordinate mapping.

---

## 2. Current product decisions

These decisions are fixed unless the user explicitly changes them.

| Topic | Decision |
|---|---|
| Target device | NovaStar H Series / H2 first |
| Other devices | X100 Pro and generic devices are deferred |
| Runtime environment | One Windows PC |
| UI framework | WPF |
| Language/runtime | C# / .NET 10 |
| H2 protocol | UDP JSON commands |
| H2 device count | MVP supports one device, config should allow future multiple devices |
| Preset numbering | Show both friendly number and raw preset ID, e.g. `Preset 1 / presetId 0` |
| H2 response policy | Cursor layout is applied only after `ack:"Ok"` and configurable post-ack delay |
| Presets vs cursor layouts | Managed separately; user chooses how to bind them in execution profiles |
| Layout editor | MVP has numeric fields plus a scaled drag/resize preview with snapping and auto portal generation |
| Mapping | Ratio-based mapping with edge-segment portals |
| Hidden monitors | Return cursor immediately to last valid position unless a configured portal applies |
| Start cursor position | Use profile-specific start position; fallback to center of first visible zone |
| Mouse tracking | Polling first; low-level mouse hook deferred |
| HTTP API | Deferred; keep execution service reusable for future HTTP/Stream Deck integration |
| DPI | App should be DPI-aware; field recommendation is 100% scaling where possible |
| Safety | Mandatory from the first version |
| Current H2 test host | `192.168.0.11` |
| Current test topology | `DISPLAY2 -> DISPLAY1 -> DISPLAY3` in Windows coordinates |
| Current default profiles | `Ctrl+Alt+1` through `Ctrl+Alt+5`, all calling H2 preset 1 / `presetId 0` |

---

## 3. Non-goals for MVP

Do **not** implement these in the first MVP:

- X100 Pro support
- Generic TCP/UDP HEX console
- HTTP API
- Stream Deck integration
- Polished production-grade visual layout editor
- Low-level mouse hook mode
- Installer packaging
- Auto-discovery of H2 visual layout from the processor

It is acceptable to create extension points for these, but do not add implementation complexity before the MVP works.

---

## 4. Recommended architecture

Use a modular solution:

```text
H2CursorRouter.sln
README.md
AGENTS.md
config.sample.json

src/
  H2CursorRouter.Core/
    Domain/
    Geometry/
    Profiles/
    Validation/

  H2CursorRouter.H2/
    H2DeviceClient.cs
    H2CommandBuilder.cs
    H2ResponseParser.cs
    H2CommandQueue.cs

  H2CursorRouter.Windows/
    ICursorService.cs
    IMonitorTopologyService.cs
    IHotkeyService.cs
    Win32CursorService.cs
    Win32MonitorTopologyService.cs
    Win32HotkeyService.cs

  H2CursorRouter.App/
    WPF app
    ViewModels/
    Views/

tests/
  H2CursorRouter.Core.Tests/
  H2CursorRouter.H2.Tests/
```

### Design rule

The core routing logic must not depend on WPF, Win32, network sockets, or real hardware.

Good:

```text
CursorRoutingEngine receives previous/current point and layout, then returns a decision.
```

Bad:

```text
CursorRoutingEngine calls SetCursorPos directly.
```

---

## 5. Core vocabulary

### H2 device

A network target that accepts H Series control commands.

```csharp
public sealed record H2DeviceConfig(
    string Id,
    string Name,
    string Host,
    int Port,
    int DeviceId,
    TimeSpan Timeout);
```

Default H2 UDP port: `6000`.

### H2 preset reference

A reference to a preset on a specific H2 screen.

```csharp
public sealed record H2PresetRef(
    string DeviceId,
    int ScreenId,
    int PresetId,
    string? DisplayName);
```

Display convention:

```text
Preset 1 / presetId 0
Preset 2 / presetId 1
```

### Cursor layout

A cursor layout describes how the Windows cursor should behave when a specific video layout is active.

A layout has:

- visible zones
- Windows rectangles
- H2 visual rectangles
- portals
- optional default start position

### Zone

A zone represents a Windows monitor/output area that may or may not be visible in the active H2 layout.

```csharp
public sealed record CursorZone(
    string Id,
    string DisplayName,
    IntRect WindowsRect,
    VisualRect VisualRect,
    bool IsVisible);
```

`WindowsRect` is the actual virtual-screen rectangle in Windows coordinates.

`VisualRect` is the rectangle where that source appears in the H2 output layout. It is not necessarily the same size as the Windows monitor resolution.

### Portal

A portal maps one edge segment to another edge segment.

```csharp
public enum Edge
{
    Left,
    Right,
    Top,
    Bottom
}

public sealed record EdgeRange(double StartRatio, double EndRatio);

public sealed record CursorPortal(
    string FromZoneId,
    Edge FromEdge,
    EdgeRange FromRange,
    string ToZoneId,
    Edge ToEdge,
    EdgeRange ToRange);
```

Example:

```text
Monitor 1 right edge, upper half -> Monitor 3 left edge, full height
Monitor 1 right edge, lower half -> Monitor 4 left edge, full height
```

This supports layouts where one source is large and two sources are smaller.

### Execution profile

An execution profile is a user action. It may reference an H2 preset, a cursor layout, or both.

```csharp
public sealed record ExecutionProfile(
    string Id,
    string Name,
    string? Hotkey,
    H2PresetRef? H2Preset,
    string? CursorLayoutId,
    CursorPoint? StartPosition,
    int PostAckDelayMs,
    bool RequireH2AckBeforeCursorLayout);
```

This supports:

- H2 preset only
- cursor layout only
- H2 preset + cursor layout

---

## 6. H2 communication guide

### Protocol assumptions for MVP

H2 / H Series public control material describes UDP communication where the control PC sends JSON commands to the video wall splicer. The expected MVP transport is UDP.

### Load preset command

```json
[{"cmd":"W0605","deviceId":0,"screenId":0,"presetId":0}]
```

Meaning:

```text
screenId: 0  -> first screen
presetId: 0  -> first preset
```

### Get preset enum command

```json
[{"cmd":"R0600","param0":0,"param1":0}]
```

Expected response shape:

```json
[
  {
    "deviceId": 0,
    "screenId": 0,
    "presets": [
      { "name": "preset1", "presetId": 0 },
      { "name": "preset2", "presetId": 1 }
    ]
  }
]
```

### ACK handling

Success example:

```json
[{"cmd":"W0605","deviceId":0,"ack":"Ok"}]
```

Rules:

- `ack:"Ok"` means success.
- `ack:"Error"` means failure.
- Missing ACK, timeout, invalid JSON, or unexpected command means failure.
- ACK comparison should be case-insensitive and trim whitespace.
- Only one H2 command should be in-flight at a time.

### Execution order

For profile execution with both H2 preset and cursor layout:

```text
1. Disable or pause current cursor routing if needed.
2. Send H2 load preset command.
3. Wait for response or timeout.
4. If `ack:"Ok"`, wait `PostAckDelayMs`.
5. Activate cursor layout.
6. Move cursor to profile start position or fallback start position.
7. Start cursor routing.
```

If H2 fails and `RequireH2AckBeforeCursorLayout == true`, do not apply the cursor layout.

---

## 7. Cursor routing engine

The routing engine should be pure and testable.

### Inputs

```text
- active cursor layout
- previous cursor position
- current cursor position
- last valid cursor position
```

### Outputs

```text
- keep current position
- move cursor to target position
- revert to last valid position
- disable routing if layout/config is unsafe
```

### Main rules

1. If current position is inside a visible zone and no configured edge crossing occurred, keep it.
2. If movement crosses a configured portal edge, move to the mapped target edge.
3. If current position is inside a hidden zone, return to the last valid position.
4. If current position is outside every known zone, return to the last valid position.
5. Use visual ratio mapping, not raw pixel copying.

### Ratio mapping

When crossing an edge, compute the source ratio based on the source zone's visual rectangle.

For vertical edges:

```text
sourceRatio = (sourceVisualY - fromVisualRect.Top) / fromVisualRect.Height
```

For horizontal edges:

```text
sourceRatio = (sourceVisualX - fromVisualRect.Left) / fromVisualRect.Width
```

For segmented portals:

```text
normalizedWithinFromRange = (sourceRatio - fromRange.StartRatio) / (fromRange.EndRatio - fromRange.StartRatio)
targetRatio = toRange.StartRatio + normalizedWithinFromRange * (toRange.EndRatio - toRange.StartRatio)
```

Then convert the target visual ratio into the target zone's Windows coordinate.

### Example: one large zone and two small zones

Visual layout:

```text
Canvas 3840 x 2160

[ Monitor 1 large        ][ Monitor 3 small ]
[ Monitor 1 large        ][ Monitor 4 small ]
```

Portal rules:

```text
Monitor 1 right edge [0.0, 0.5] -> Monitor 3 left edge [0.0, 1.0]
Monitor 1 right edge [0.5, 1.0] -> Monitor 4 left edge [0.0, 1.0]
```

---

## 8. Windows integration guide

Keep all Win32 calls behind interfaces.

### Cursor service

Responsibilities:

- get cursor position
- set cursor position
- optionally apply/release rectangular clipping

Use `SetCursorPos` for cursor movement.

Do not inject clicks or keyboard input in the MVP.

### Monitor topology service

Responsibilities:

- enumerate monitors
- return Windows virtual-screen rectangles
- identify current monitor topology
- detect topology changes

Use monitor information to populate the UI and validate cursor layout configs.

### Hotkey service

Responsibilities:

- register global hotkeys
- unregister hotkeys
- route hotkey events to profile execution
- always reserve emergency unlock hotkey

### Important distinction about input

This app does not need to simulate user keyboard/mouse input.

However, it must still care about:

- reading cursor position
- moving cursor position
- registering global hotkeys
- emergency unlock
- focus-independent operation
- releasing restrictions on exit
- avoiding cursor traps

So input injection is not a goal, but input safety is a core requirement.

---

## 9. UI guide

The first MVP uses simple WPF views and DataGrid-heavy editing. This is acceptable for testing, but the next product phase should reduce direct exposure of raw IDs, raw coordinates, and portal internals.

Recommended tabs/sections:

### H2 Device Settings

Fields:

- host
- port
- device ID
- timeout
- test/get presets button

### H2 Presets

Show:

- screen ID
- friendly preset number
- raw `presetId`
- preset name
- load/test button

### Monitor Diagnostics

Show:

- detected Windows monitors
- each monitor's virtual-screen rectangle
- current cursor position
- current cursor zone under active layout

### Cursor Layouts

MVP editor:

- zone ID
- display name
- Windows rect
- visual rect
- visible flag
- scaled visual preview
- drag/resize blocks with snapping
- apply detected monitor coordinates
- apply canvas layout and auto-generate portals

Advanced portal editor:

- from zone
- from edge
- from range
- to zone
- to edge
- to range

### Execution Profiles

Fields:

- name
- optional H2 preset
- optional cursor layout
- hotkey
- start position
- post-ack delay
- require H2 ACK before cursor layout
- execute button

### Runtime

Controls:

- active profile display
- routing enabled/disabled
- emergency unlock button
- logs

### Next UI direction

Move toward this simplified structure:

```text
Dashboard
  - large profile execution buttons
  - active profile/layout status
  - H2 ACK status
  - routing state
  - emergency unlock

Profiles
  - user-facing profile names and hotkeys
  - preset binding
  - cursor layout binding
  - hide raw ids by default

Layout Editor
  - show Monitor 1 / Monitor 2 / Monitor 3 labels
  - keep DISPLAY ids as internal/advanced data
  - make "arrange visual layout -> generate portals -> validate -> save" one workflow
  - hide portal table unless Advanced is expanded

Settings
  - H2 host/port and connection test
  - startup/tray options
  - advanced raw config access

Logs
  - H2 command results
  - profile execution
  - routing safety events
  - portal mapping diagnostics
```

Design tone should be a calm desktop operations tool, not a marketing page:

- light neutral workspace background
- restrained blue/teal accent for active/runnable controls
- red only for emergency/destructive controls
- consistent button heights and spacing
- fewer full-width raw DataGrids in the main workflow
- cards or rows for profile execution, not giant tables
- show advanced details only when they help debugging

---

## 10. Safety requirements

Safety is not optional.

Implement:

1. Cursor routing is disabled on app startup.
2. Emergency unlock hotkey: `Ctrl+Alt+Shift+Esc`.
3. Emergency unlock button in the UI.
4. On emergency unlock:
   - disable routing
   - release any cursor clipping
   - clear active layout
   - log the event
5. On app exit:
   - release cursor clipping
   - disable routing
6. If monitor topology changes:
   - disable routing
   - warn/log that layout must be revalidated
7. If config validation fails:
   - do not activate routing
8. If H2 ACK fails and the profile requires ACK:
   - do not apply bound cursor layout

---

## 11. Configuration sample shape

`config.sample.json` is the source of truth for the current test-ready MVP sample.

Current default H2 settings:

```text
host: 192.168.0.11
port: 6000
deviceId: 0
screenId: 0
presetId: 0
display: Preset 1 / presetId 0
```

Current Windows test topology:

```text
DISPLAY2 -> DISPLAY1 -> DISPLAY3

DISPLAY2: 0,0 to 1920,1080    = user-facing Monitor 1
DISPLAY1: 1920,0 to 3840,1080 = user-facing Monitor 2
DISPLAY3: 3840,0 to 5760,1080 = user-facing Monitor 3
```

Current default profiles:

```text
Ctrl+Alt+1: Preset 1 + Monitor 1/3 Tunnel
Ctrl+Alt+2: Preset 1 + Monitor 3/1 Tunnel Reversed
Ctrl+Alt+3: Preset 1 + Monitor 2 Only
Ctrl+Alt+4: Preset 1 + Monitor 1/2 Tunnel
Ctrl+Alt+5: Preset 1 + Monitor 1 Large 2/3 Stack
```

Important config rules:

- Keep `cursorLayouts[].zones[].id` unique inside a layout.
- Portal `fromZoneId` and `toZoneId` must match zone IDs exactly, ignoring case.
- User-facing labels such as "Monitor 1" may differ from Windows device names such as `DISPLAY2`.
- Prefer stable internal IDs based on detected display names when possible.
- For the current test PC, `DISPLAY2` is the left monitor, `DISPLAY1` is the middle monitor, and `DISPLAY3` is the right monitor.
- Existing `config.json` takes precedence over `config.sample.json`; replacing the application ZIP does not overwrite an existing runtime config.
- If default profiles change and a test PC already has `config.json`, delete or migrate the existing `config.json`.

---

## 12. Test guidance

Prioritize tests around behavior, not UI.

Must-have tests:

- `W0605` command serialization
- `R0600` command serialization
- ACK parser success with `Ok`
- ACK parser failure with `Error`
- ACK parser failure with malformed JSON
- visible zone detection
- hidden zone rejection
- portal selection by edge and range
- full-edge ratio mapping
- segmented portal ratio mapping
- different-size visual rectangle mapping
- default start position fallback
- profile validation
- H2 failure prevents cursor layout when ACK is required

Integration-style tests:

- fake UDP H2 server replies with `ack:"Ok"`
- fake UDP H2 server replies with `ack:"Error"`
- fake UDP H2 server does not reply and client times out

Avoid tests that require a real H2 device or real cursor movement.

---

## 13. Build and validation commands

Use these commands where available:

```bash
dotnet restore
dotnet build
dotnet test
```

If running in a non-Windows environment and the WPF app cannot build, keep the core and H2 projects cross-platform and validate them with:

```bash
dotnet test tests/H2CursorRouter.Core.Tests/H2CursorRouter.Core.Tests.csproj
dotnet test tests/H2CursorRouter.H2.Tests/H2CursorRouter.H2.Tests.csproj
```

Do not silently downgrade from .NET 10. If the environment lacks .NET 10, document the limitation in the final response and still create the correct target framework files.

---

## 14. Review guidelines

When reviewing code, focus on:

- cursor trap risks
- hidden monitor handling
- failure to release cursor restrictions
- H2 ACK/timeout correctness
- command queue correctness
- geometry mapping correctness
- accidental coupling between WPF and core logic
- assumptions that prevent later HTTP API or X100 Pro support
- config validation gaps
- missing tests for edge cases

Treat safety regressions as high-priority issues.

---

## 15. Implementation principles

- Keep domain logic explicit.
- Make geometric decisions testable.
- Keep hardware/network failures visible in logs.
- Never apply cursor routing if the active layout is invalid.
- Do not let a failed H2 command create a mismatch between the actual video layout and the cursor layout.
- Prefer a simple working MVP over a polished but fragile UI.

---

## 16. Current MVP status

The first MVP is functional enough for real H2 and multi-monitor testing.

Implemented:

- .NET 10 solution with separated Core, H2, Windows, WPF App, and test projects.
- H2 UDP JSON command builder/client/parser for `W0605` and `R0600`.
- ACK parsing for `Ok`, `Error`, malformed JSON, and unexpected responses.
- One-command-in-flight H2 UDP handling.
- Pure cursor routing engine with visible/hidden/outside-zone handling.
- Full-edge and segmented-edge portal mapping.
- Ratio-based mapping for different-size visual rectangles.
- Virtual desktop boundary handling, including rightmost/leftmost edge contact cases.
- Segmented portal priority when overlapping full-edge portals exist.
- Windows cursor, monitor topology, hotkey, and startup integration behind interfaces.
- Polling cursor routing runtime with safe start/stop.
- Emergency unlock hotkey and button.
- Cursor clipping for single-visible-zone layouts.
- Monitor topology change safety shutdown.
- Tray icon, hide-to-tray, and startup checkbox.
- WPF shell with a dashboard-first workflow, profile execution cards, simplified layout workflow, H2/preset controls, diagnostics, advanced raw zone/portal editing, validation, runtime status, and logs.
- Scaled layout preview with drag/resize, snapping, detected-coordinate application, and auto portal generation.
- Throttled routing decision diagnostics and dashboard display for last H2 ACK / last routing event.
- In-memory reset to bundled sample configuration; `config.json` is only written by explicit save.
- Default test profiles for the current H2 test setup.
- GitHub Actions Windows build/test/publish artifact flow.

Known MVP limitations:

- UI is improved but still uses WPF DataGrid controls for advanced editing.
- Layout editing works through a scaled canvas, but it is not yet a polished production-grade editor.
- There is no installer or signed executable.
- Existing `config.json` can hide changes made to `config.sample.json`.
- Monitor naming is clearer in the main workflow, but raw `DISPLAY1`, `DISPLAY2`, `DISPLAY3` IDs remain visible in Advanced/debug areas.
- No low-level mouse hook yet; routing remains polling-based by design.
- No automatic H2 layout discovery.

---

## 17. Recommended next work order

Do not start with visual restyling alone. First simplify user workflows, then apply the new UI tone.

### Phase 1: Product workflow cleanup

Goal: make the app understandable without knowing raw IDs, Windows rectangles, or portal internals.

Status: first pass completed. Keep refining based on field-test feedback.

Tasks:

- Create a dashboard-first experience.
- Make profile execution the primary first screen.
- Show large buttons or rows for profiles `1` through `5`.
- Show active profile, active layout, routing enabled, H2 ACK result, and emergency unlock in one place.
- Hide raw `deviceId`, `screenId`, `presetId`, `zone.Id`, `fromZoneId`, and `toZoneId` from the normal workflow.
- Keep an Advanced/Debug area for raw IDs, raw coordinates, and portal rows.

### Phase 2: Layout editor UX

Goal: users should arrange visible monitor blocks, apply the layout, validate, save, and test.

Status: first pass completed with scaled drag/resize canvas and advanced raw tables. Continue refining labels/templates.

Tasks:

- Replace raw zone table as the main editing surface with monitor cards/blocks.
- Use user-facing labels: Monitor 1, Monitor 2, Monitor 3.
- Show detected mapping as secondary text, e.g. `Monitor 1 -> DISPLAY2`.
- Add a clear workflow:

```text
Detect monitors -> choose visible monitors -> arrange blocks -> apply layout -> validate -> save
```

- Keep "Use Detected Coordinates" and "Apply Canvas Layout", but rename them to user-facing actions.
- Add runtime portal diagnostics such as:

```text
Portal: DISPLAY2 Right 0.5-1.0 -> DISPLAY3 Left
Target: 3840,540
```

- Add a one-click preset layout template for the current common layouts:
  - 1/3 tunnel
  - 3/1 reversed
  - 2 only
  - 1/2 tunnel
  - 1 large + 2/3 stack

### Phase 3: UI visual redesign

Goal: make the app feel like a calm, reliable Windows operations tool.

Status: first pass completed with neutral surfaces, accent/danger button styles, dashboard cards, and fewer raw tables in the main workflow.

Tasks:

- Move away from the current raw WPF/DataGrid default look.
- Use a left navigation or clearer top-level sections instead of many cramped tabs.
- Use consistent button sizes, spacing, typography, and state colors.
- Use a light neutral surface with restrained accent color.
- Reserve red only for emergency unlock and destructive actions.
- Make the active/running state visually obvious.
- Avoid decorative UI that distracts from device control and safety.

### Phase 4: Runtime reliability and diagnostics

Goal: make field testing easier when routing or H2 behavior is wrong.

Status: partially completed. Profile steps, H2 ACK, active layout/routing state, current cursor zone, sample reset, and throttled routing decision logs are visible. Diagnostic bundle export is still open.

Tasks:

- Log every profile execution step:
  - H2 command sent
  - ACK result
  - post-ack delay
  - layout activated
  - routing started
- Log portal decisions at a throttled rate.
- Show last portal decision in the runtime screen.
- Add a visible "current cursor zone" under the active layout.
- Add config migration or "reset to bundled sample config" action.
- Add a "copy diagnostic bundle" action with config, logs, monitor topology, and app version.

### Phase 5: Distribution and release hygiene

Goal: make handoff to the Windows test PC repeatable.

Tasks:

- Keep GitHub Actions artifact generation.
- Add optional GitHub Release asset workflow for login-free public downloads.
- Add version number and build SHA in the app UI.
- Consider signing only after the MVP stabilizes.
- Keep installer packaging deferred until user workflows are stable.

---

## 18. Project workflow recommendations

Use this working rhythm:

1. Keep each change tied to one field-test problem.
2. Add or update a core/H2 test when behavior changes.
3. Do not rely on UI-only testing for cursor routing math.
4. Push to the PR branch and let GitHub Actions validate Windows build/test/publish.
5. Download the latest Windows artifact to the test PC.
6. If a test PC already has `config.json`, decide explicitly whether to keep, migrate, or replace it.
7. Record every field-test issue with:

```text
profile used:
active layout:
monitor topology:
expected cursor transition:
actual cursor transition:
whether H2 ACK succeeded:
artifact id / commit SHA:
```

When modifying the UI:

- Prefer hiding complexity over deleting useful advanced controls.
- Do not remove safety controls from the main surface.
- Keep emergency unlock always visible or one click away.
- Keep raw configuration available in Advanced/Debug until the simplified UI is fully proven.
- Preserve the current working MVP behavior while redesigning the surface.

When modifying routing:

- Treat cursor trap risk as highest priority.
- Add tests for every new geometry shape.
- Include virtual desktop boundary cases when a zone is at the outer edge of Windows coordinates.
- Prefer deterministic pure-core fixes before changing Win32 runtime behavior.

When modifying H2 behavior:

- Keep cursor layout activation gated by ACK when `RequireH2AckBeforeCursorLayout` is true.
- Log H2 failures clearly.
- Avoid applying a cursor layout that could mismatch the actual H2 visual output.
