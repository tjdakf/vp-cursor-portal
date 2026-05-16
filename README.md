# vp-cursor-portal

`vp-cursor-portal` is a Windows desktop MVP for a single PC connected to a NovaStar H Series / H2 video processor. It links H2 preset loading over UDP JSON with a Windows cursor routing layout that matches the visual output layout currently shown by the processor.

## MVP Scope

- .NET 10 solution split into Core, H2, Windows, WPF App, and test projects.
- Pure cursor geometry and routing engine with visible zones, hidden-zone rejection, outside-zone rejection, full-edge portals, segmented portals, and ratio-based mapping.
- H2 command builder/parser for `W0605` load preset and `R0600` get preset enum JSON.
- UDP H2 client using `System.Net.Sockets`, timeouts, ACK validation, and serialized in-flight commands.
- WPF shell with sections for H2 device settings, presets, monitor diagnostics, cursor layouts, portals, profiles, runtime controls, and logs.
- Win32 cursor, monitor topology, and hotkey integration behind interfaces.
- Mandatory emergency unlock path through `Ctrl+Alt+Shift+Esc` and the UI button.

## Requirements

- Windows for the WPF application and Win32 cursor routing.
- .NET 10 SDK.
- A NovaStar H Series / H2 reachable over UDP, normally on port `6000`.

The Core and H2 projects are intentionally cross-platform. The WPF and Win32 projects target `net10.0-windows`.

## Build, Test, Run

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/H2CursorRouter.App/H2CursorRouter.App.csproj
```

If validating on a non-Windows machine, build or test the cross-platform projects directly:

```bash
dotnet test tests/H2CursorRouter.Core.Tests/H2CursorRouter.Core.Tests.csproj
dotnet test tests/H2CursorRouter.H2.Tests/H2CursorRouter.H2.Tests.csproj
```

## Windows Publish Artifact

On a Windows machine with the .NET 10 SDK:

```powershell
.\scripts\publish-windows.ps1
```

This restores, builds, tests, publishes the WPF app, and writes the runnable files to:

```text
artifacts\vp-cursor-portal-win-x64
```

Run:

```text
artifacts\vp-cursor-portal-win-x64\H2CursorRouter.App.exe
```

If this repository is pushed to GitHub, the `Windows Build` workflow also builds, tests, publishes, and uploads a `vp-cursor-portal-win-x64` artifact from a Windows runner.

## Configuration

Start from `config.sample.json` and create a local `config.json` beside the executable or run directory. Presets use zero-based H2 IDs while display names should show both forms, for example:

```text
Preset 1 / presetId 0
```

For profile execution with both an H2 preset and cursor layout, the app sends `W0605`, waits for `ack:"Ok"`, waits `postAckDelayMs`, applies the cursor layout, moves to the profile start position or fallback start point, then starts polling-based routing.

## Safety

Routing is disabled on startup. Emergency unlock disables routing, releases cursor clipping, clears the active layout, and logs the event. Monitor topology changes also disable routing so layouts can be revalidated before reuse.

Use a test profile and keep the emergency hotkey available while validating real monitor layouts. The MVP uses `SetCursorPos` only for routing moves and does not inject clicks or keyboard input.

## Deferred

- X100 Pro support
- HTTP API
- Stream Deck integration
- Drag-and-drop layout editor
- Low-level mouse hook mode
- Installer packaging
- Generic TCP/UDP HEX console
- Auto-discovery of H2 visual layouts
