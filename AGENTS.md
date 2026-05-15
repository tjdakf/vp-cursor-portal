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
| Layout editor | MVP uses numeric fields, not drag-and-drop |
| Mapping | Ratio-based mapping with edge-segment portals |
| Hidden monitors | Return cursor immediately to last valid position unless a configured portal applies |
| Start cursor position | Use profile-specific start position; fallback to center of first visible zone |
| Mouse tracking | Polling first; low-level mouse hook deferred |
| HTTP API | Deferred; keep execution service reusable for future HTTP/Stream Deck integration |
| DPI | App should be DPI-aware; field recommendation is 100% scaling where possible |
| Safety | Mandatory from the first version |

---

## 3. Non-goals for MVP

Do **not** implement these in the first MVP:

- X100 Pro support
- Generic TCP/UDP HEX console
- HTTP API
- Stream Deck integration
- Drag-and-drop visual layout editor
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

Use simple WPF views first. Do not overbuild the UI.

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

Numeric editor for:

- zone ID
- display name
- Windows rect
- visual rect
- visible flag

Numeric editor for portals:

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

Use this shape as a starting point for `config.sample.json`.

```json
{
  "devices": [
    {
      "id": "h2-main",
      "name": "Main H2",
      "host": "192.168.0.100",
      "port": 6000,
      "deviceId": 0,
      "timeoutMs": 1000
    }
  ],
  "cursorLayouts": [
    {
      "id": "layout-monitor-1-3",
      "name": "Monitor 1 to Monitor 3",
      "zones": [
        {
          "id": "MONITOR_1",
          "displayName": "Monitor 1",
          "windowsRect": { "left": 0, "top": 0, "right": 1920, "bottom": 1080 },
          "visualRect": { "left": 0, "top": 0, "right": 1920, "bottom": 1080 },
          "isVisible": true
        },
        {
          "id": "MONITOR_3",
          "displayName": "Monitor 3",
          "windowsRect": { "left": 3840, "top": 0, "right": 5760, "bottom": 1080 },
          "visualRect": { "left": 1920, "top": 0, "right": 3840, "bottom": 1080 },
          "isVisible": true
        }
      ],
      "portals": [
        {
          "fromZoneId": "MONITOR_1",
          "fromEdge": "Right",
          "fromRange": { "startRatio": 0.0, "endRatio": 1.0 },
          "toZoneId": "MONITOR_3",
          "toEdge": "Left",
          "toRange": { "startRatio": 0.0, "endRatio": 1.0 }
        },
        {
          "fromZoneId": "MONITOR_3",
          "fromEdge": "Left",
          "fromRange": { "startRatio": 0.0, "endRatio": 1.0 },
          "toZoneId": "MONITOR_1",
          "toEdge": "Right",
          "toRange": { "startRatio": 0.0, "endRatio": 1.0 }
        }
      ]
    }
  ],
  "profiles": [
    {
      "id": "preset-1-layout-1-3",
      "name": "H2 Preset 1 + Monitor 1/3 Cursor",
      "hotkey": "Ctrl+Alt+1",
      "h2Preset": {
        "deviceId": "h2-main",
        "screenId": 0,
        "presetId": 0,
        "displayName": "Preset 1 / presetId 0"
      },
      "cursorLayoutId": "layout-monitor-1-3",
      "startPosition": { "x": 960, "y": 540 },
      "postAckDelayMs": 500,
      "requireH2AckBeforeCursorLayout": true
    }
  ],
  "safety": {
    "emergencyHotkey": "Ctrl+Alt+Shift+Esc",
    "disableRoutingOnMonitorTopologyChange": true,
    "startWithRoutingDisabled": true
  }
}
```

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
