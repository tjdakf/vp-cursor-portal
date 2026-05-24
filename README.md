# vp-cursor-portal

`vp-cursor-portal` is a Windows desktop MVP for a single PC connected to a NovaStar H Series / H2 video processor. It links H2 preset loading over UDP JSON with a Windows cursor routing layout that matches the visual output layout currently shown by the processor.

## MVP Scope

- .NET 10 solution split into Core, H2, Windows, WPF App, and test projects.
- Pure cursor geometry and routing engine with visible zones, hidden-zone rejection, outside-zone rejection, full-edge portals, segmented portals, and ratio-based mapping.
- H2 command builder/parser for `W0605` load preset and `R0600` get preset enum JSON.
- UDP H2 client using `System.Net.Sockets`, timeouts, ACK validation, and serialized in-flight commands.
- WPF shell with a dashboard-first workflow, H2 device settings, presets, monitor diagnostics, cursor layouts, profiles, advanced portal editing, runtime controls, and logs.
- Win32 cursor, monitor topology, and hotkey integration behind interfaces.
- Mandatory emergency unlock path through `Ctrl+Alt+Shift+Esc` and the UI button.

## Requirements

- Windows for the WPF application and Win32 cursor routing.
- A NovaStar H Series / H2 reachable over UDP, normally on port `6000`.

The downloadable GitHub Actions artifact is self-contained for Windows x64, so the test PC does not need a separate .NET Desktop Runtime install. Building from source still requires the .NET 10 SDK.

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

## Windows Publish And Installer Artifacts

On a Windows machine with the .NET 10 SDK:

```powershell
.\scripts\publish-windows.ps1
```

This restores, builds, tests, publishes a self-contained Windows x64 WPF app, and writes the runnable files to:

```text
artifacts\vp-cursor-portal-win-x64
```

Run:

```text
artifacts\vp-cursor-portal-win-x64\H2CursorRouter.App.exe
```

To also build the installer locally, install Inno Setup 6 and run:

```powershell
.\scripts\publish-windows.ps1 -BuildInstaller
```

This additionally writes:

```text
artifacts\installer\vp-cursor-portal-setup.exe
```

If this repository is pushed to GitHub, the `Windows Build` workflow builds, tests, publishes, and uploads both artifacts from a Windows runner:

- `vp-cursor-portal-win-x64` - self-contained zip-style app folder
- `vp-cursor-portal-setup` - Program Files installer built with Inno Setup

Field-test handoff flow:

1. Open the GitHub Actions run named `Windows Build`.
2. Download either the `vp-cursor-portal-win-x64` artifact or the `vp-cursor-portal-setup` artifact.
3. For the zip-style artifact, unzip it and run `H2CursorRouter.App.exe`.
4. For the installer artifact, run `vp-cursor-portal-setup.exe`; it installs to `Program Files` and creates a Start Menu shortcut.
5. During uninstall, choose whether to also remove user configuration and logs.

## Configuration

The bundled `config.sample.json` starts empty by default: no H2 devices, no cursor layouts, and no profiles. Runtime configuration and logs are stored per user:

```text
%AppData%\vp-cursor-portal\config.json
%AppData%\vp-cursor-portal\logs\
```

The app falls back to a legacy `config.json` beside the executable only when no AppData config exists, then saves future changes to AppData. Presets use zero-based H2 IDs internally while display names can use friendly numbers, for example:

```text
Preset 1 / presetId 0
```

For profile execution with both an H2 preset and cursor layout, the app sends `W0605`, waits for `ack:"Ok"`, waits `postAckDelayMs`, applies the cursor layout, moves to the profile start position or fallback start point, then starts polling-based routing.

The dashboard reset action, when exposed in the UI, reloads an empty `SampleConfiguration.Create()` into memory only. It does not overwrite `config.json` until `Save Config` succeeds.

## Safety

Routing is disabled on startup. Emergency unlock disables routing, releases cursor clipping, clears the active layout, and logs the event. Monitor topology changes also disable routing so layouts can be revalidated before reuse.

Use a test profile and keep the emergency hotkey available while validating real monitor layouts. The MVP uses `SetCursorPos` only for routing moves and does not inject clicks or keyboard input.

## Deferred

- X100 Pro support
- HTTP API
- Stream Deck integration
- Drag-and-drop layout editor
- Low-level mouse hook mode
- Generic TCP/UDP HEX console
- Auto-discovery of H2 visual layouts
