# H2 Cursor Router

H2 Cursor Router is a Windows desktop MVP for one control PC connected to a NovaStar H Series / H2 video processor.

The app links two operations that normally happen separately:

1. Load an H2 preset over UDP JSON.
2. Apply a Windows cursor-routing layout that matches the visual output layout currently shown by the processor.

The practical problem is that Windows monitor topology can differ from the H2 visual layout. Windows may expose monitors as:

```text
[Monitor 1] [Monitor 2] [Monitor 3]
```

while the active H2 preset may visually show only:

```text
[Monitor 1] [Monitor 3]
```

In that case, the cursor should not disappear into Monitor 2. H2 Cursor Router keeps the cursor inside visible zones, reverts from hidden zones, and moves across configured portal edges using ratio-based coordinate mapping.

## Current MVP Status

Implemented:

- WPF desktop app targeting `.NET 10` / `net10.0-windows`.
- Separated projects for core domain logic, H2 UDP communication, Win32 integration, WPF app, and tests.
- H2 `W0605` preset load command and `R0600` preset enum command.
- UDP client with timeout handling and one in-flight H2 command at a time.
- ACK parsing for success, failure, malformed JSON, and unexpected responses.
- Pure cursor routing engine with visible-zone keep, hidden-zone revert, outside-zone revert, full-edge portals, segmented portals, and different-size visual rectangle mapping.
- Polling-based Win32 cursor routing runtime.
- Emergency unlock hotkey and UI action.
- Monitor topology detection and topology-change safety shutdown.
- Dashboard-first WPF workflow for profiles, H2 status, routing status, emergency unlock, layout editing, diagnostics, and logs.
- Layout editor with detected monitor coordinates, scaled canvas, drag/resize, snapping, and auto portal generation.
- Profile execution that can bind H2 preset only, cursor layout only, or both.
- Installer and portable Windows artifact generation through GitHub Actions.

Deferred:

- X100 Pro support.
- Generic TCP/UDP HEX console.
- HTTP API / Stream Deck integration.
- Low-level mouse hook mode.
- Auto-discovery of the H2 visual layout from the processor.
- Signed installer.

## Repository Layout

```text
H2CursorRouter.sln
README.md

.github/workflows/
  windows-build.yml

installer/inno/
  vp-cursor-portal.iss

scripts/
  publish-windows.ps1

src/
  H2CursorRouter.Core/
  H2CursorRouter.H2/
  H2CursorRouter.Windows/
  H2CursorRouter.App/

tests/
  H2CursorRouter.Core.Tests/
  H2CursorRouter.H2.Tests/
  H2CursorRouter.App.Tests/
```

Files intentionally not tracked:

- `AGENTS.md` - local agent/developer operating notes.
- `config.sample.json` - local-only sample or field-test config, if needed.
- `.gitkeep` - local placeholder files.
- runtime `config.json`, logs, and build artifacts.

The app no longer requires a tracked `config.sample.json`. On a clean install it uses the built-in empty configuration from `SampleConfiguration.Create()`.

## Project Responsibilities

### `H2CursorRouter.Core`

Pure domain, geometry, profile, and validation logic. This project must not depend on WPF, Win32, sockets, or real hardware.

Important folders and types:

- `Domain/`
  - `H2DeviceConfig`
  - `H2PresetRef`
- `Geometry/`
  - `CursorLayout`
  - `CursorZone`
  - `CursorPortal`
  - `CursorRoutingEngine`
  - `RoutingDecision`
- `Profiles/`
  - `ExecutionProfile`
  - `ProfileExecutionPlanner`
- `Validation/`
  - `AppConfigurationValidator`
  - `CursorLayoutValidator`
- `Configuration/`
  - runtime config models and JSON document mapping

Rule: add or update Core tests whenever cursor routing math, validation rules, profile planning, or config contracts change.

### `H2CursorRouter.H2`

NovaStar H Series / H2 UDP JSON communication.

Important types:

- `H2CommandBuilder` builds JSON commands:
  - `W0605` load preset
  - `R0600` get preset enum
- `H2ResponseParser` parses ACK responses.
- `H2PresetEnumParser` parses preset lists.
- `H2DeviceClient` sends UDP commands and serializes command execution.
- `IH2DeviceClient` is the app/test boundary.

Current H2 assumptions:

- UDP transport.
- Default H2 port `6000`.
- `ack:"Ok"` means success.
- `ack:"Error"`, missing ACK, timeout, malformed JSON, or unexpected command means failure.
- ACK comparison is case-insensitive and trimmed.

Rule: do not apply a cursor layout after H2 failure when the profile requires ACK.

### `H2CursorRouter.Windows`

Win32 integration behind interfaces.

Important types:

- `ICursorService` / `Win32CursorService`
- `IMonitorTopologyService` / `Win32MonitorTopologyService`
- `IHotkeyService` / `Win32HotkeyService`
- `IStartupRegistrationService` / `WindowsStartupRegistrationService`
- `ICursorRoutingRuntime` / `CursorRoutingRuntime`

The runtime:

- starts disabled,
- polls cursor position,
- evaluates the active layout through the pure `CursorRoutingEngine`,
- moves the cursor with `SetCursorPos`,
- clips the cursor only for single-visible-zone layouts,
- releases clipping on stop, topology change, emergency unlock, and app exit.

Rule: Win32 calls stay in this project. Core logic stays pure and testable.

### `H2CursorRouter.App`

WPF shell, app composition, row view models, dialogs, execution orchestration, and UI-facing services.

Important files:

- `App.xaml.cs`
  - creates Win32/H2 services,
  - loads user config,
  - wires `MainViewModel`,
  - starts monitor topology watching.
- `MainWindow.xaml`
  - dashboard-first WPF UI.
- `MainWindow.xaml.cs`
  - hotkey registration, tray behavior, event handlers.
- `ViewModels/MainViewModel.cs`
  - root facade/coordinator for UI bindings.
- `ViewModels/DevicePresetViewModel.cs`
  - H2 device rows, preset cache rows, preset fetch, H2 status.
- `ViewModels/LayoutEditorViewModel.cs`
  - layout, zone, portal, monitor, and canvas state.
- `ViewModels/ProfileListViewModel.cs`
  - profile add/edit/remove/filter/dashboard list.
- `ViewModels/RuntimeLogViewModel.cs`
  - runtime status, last routing event, visible log list, validation messages.
- `Services/ProfileExecutionService.cs`
  - profile execution workflow.
- `Services/ConfigurationCoordinator.cs`
  - row-to-config build, validation, and save coordination.
- `Services/ConfigurationRowMapper.cs`
  - converts runtime config to UI rows and back.
- `Services/LayoutEditingService.cs`
  - canvas move/resize/snap helpers.
- `Services/MonitorZoneMatcher.cs`
  - maps UI zones to detected monitor coordinates.
- `Assets/`
  - Windows app icon, tray icon, and icon source artwork.

`MainViewModel` intentionally remains a facade for existing WPF bindings. New behavior should usually go into a child ViewModel or service first, then be exposed through the facade only when the XAML needs it.

## Runtime Configuration

Runtime data is per-user:

```text
%AppData%\vp-cursor-portal\config.json
%AppData%\vp-cursor-portal\logs\
```

Load order:

1. `%AppData%\vp-cursor-portal\config.json`
2. legacy `config.json` beside the executable, if present
3. optional `config.sample.json` beside the executable, if a developer or tester manually placed one there
4. built-in empty configuration from `SampleConfiguration.Create()`

Invalid config files are moved aside with an `.invalid-{timestamp}` suffix and the app falls back to the next source.

Clean install behavior:

- no H2 devices,
- no cursor layouts,
- no profiles,
- routing disabled.

The app writes `config.json` only when save or auto-save succeeds. ZIP replacement or installer upgrade should not overwrite existing user config.

## Profile Execution Flow

A profile can reference:

- only an H2 preset,
- only a cursor layout,
- both an H2 preset and cursor layout.

For a profile with both H2 preset and cursor layout:

```text
1. Stop current routing if needed.
2. Validate configuration.
3. Send H2 W0605 preset load command.
4. Wait for ACK or timeout.
5. If ACK is required and not successful, do not apply the cursor layout.
6. If ACK succeeded, wait profile PostAckDelayMs.
7. Resolve start position.
8. Activate cursor layout.
9. Move cursor to start position.
10. Start polling-based routing.
```

The UI shows the active profile/layout, H2 status, routing state, last routing event, and logs.

## Cursor Routing Model

A cursor layout contains:

- visible zones,
- hidden zones,
- Windows rectangles,
- H2 visual rectangles,
- portal edge mappings,
- optional default start position.

The routing engine is pure. It receives the active layout, previous cursor position, current cursor position, and last valid cursor position. It returns a `RoutingDecision`:

- keep current position,
- move to mapped portal target,
- revert to last valid position,
- reject unsafe layout.

Portal mapping uses visual-ratio mapping rather than copying raw pixels. This lets a large visual source map correctly to a smaller or segmented target source.

High-risk behavior:

- hidden monitor handling,
- virtual desktop boundary crossings,
- segmented edge portals,
- single-visible-zone clipping,
- topology-change shutdown.

Any change in these areas should include focused Core or Windows/App tests.

## Logging Policy

Logs are meant for field diagnosis, not raw cursor tracing.

Logged:

- app startup and recovery warnings,
- H2 command failures and important H2 successes,
- profile execution start and routing start/failure,
- emergency unlock,
- topology changes,
- config validation/save failures,
- layout/profile/device changes.

Not accumulated in the visible log list or log files:

- high-frequency `Portal move` diagnostics,
- high-frequency `Cursor revert` diagnostics,
- repeated identical display-detection results,
- successful auto-save noise,
- successful raw H2 ACK JSON.

High-frequency routing diagnostics still update `LastRoutingEvent` so the dashboard can show the latest movement/revert without flooding the log.

Visible UI logs are capped at 300 entries. File logs are written under AppData and files older than 30 days are deleted on startup.

## Safety Requirements

Safety is mandatory.

- Routing starts disabled.
- Emergency unlock hotkey: `Ctrl+Alt+Shift+Esc`.
- Emergency unlock button is available in the UI.
- Emergency unlock disables routing, releases cursor clipping, clears active layout, and logs the event.
- App exit releases routing and clipping.
- Monitor topology changes disable routing.
- Invalid layouts are refused.
- H2 failure prevents cursor-layout activation when ACK is required.

Do not remove or hide emergency controls from normal operation paths.

## Requirements

Runtime:

- Windows x64 for the WPF app and Win32 cursor routing.
- NovaStar H Series / H2 reachable over UDP, normally port `6000`.

Development:

- .NET 10 SDK.
- Windows is required to build and run the WPF app normally.
- Inno Setup 6 is required only for local installer builds.

The GitHub Actions artifacts are self-contained Windows x64 builds, so the target PC does not need a separate .NET Desktop Runtime.

## Build, Test, Run

On Windows:

```powershell
dotnet restore H2CursorRouter.sln
dotnet build H2CursorRouter.sln
dotnet test H2CursorRouter.sln
dotnet run --project src\H2CursorRouter.App\H2CursorRouter.App.csproj
```

On non-Windows machines, the WPF app may not build or run. Validate cross-platform logic directly:

```bash
dotnet test tests/H2CursorRouter.Core.Tests/H2CursorRouter.Core.Tests.csproj
dotnet test tests/H2CursorRouter.H2.Tests/H2CursorRouter.H2.Tests.csproj
```

Do not downgrade the target framework from .NET 10 to make a local machine pass.

## Publishing

Local Windows publish:

```powershell
.\scripts\publish-windows.ps1
```

Output:

```text
artifacts\vp-cursor-portal-win-x64\
```

Run:

```text
artifacts\vp-cursor-portal-win-x64\H2CursorRouter.App.exe
```

Local installer build:

```powershell
.\scripts\publish-windows.ps1 -BuildInstaller
```

Output:

```text
artifacts\installer\vp-cursor-portal-setup.exe
```

The installer:

- installs under `Program Files`,
- creates a Start Menu shortcut,
- optionally creates a desktop shortcut,
- asks during uninstall whether to delete `%AppData%\vp-cursor-portal`.

## GitHub Actions Artifacts

Workflow: `.github/workflows/windows-build.yml`

Triggers:

- pushes to `main`, `master`, or `codex-initial-mvp`,
- pull requests,
- manual dispatch,
- version tags matching `v*`.

The workflow:

1. installs .NET 10,
2. restores,
3. builds,
4. tests,
5. publishes a self-contained Windows x64 app,
6. builds the Inno Setup installer,
7. uploads artifacts.

Artifacts:

- `vp-cursor-portal-win-x64` - portable self-contained app folder
- `vp-cursor-portal-setup` - Program Files installer

GitHub Release assets are uploaded only for tags like `v0.1.0`.

## Release Checklist

Before creating a release:

1. Merge the PR branch.
2. Confirm the latest `Windows Build` workflow passes.
3. Download and run the installer artifact on a Windows test PC.
4. Confirm install path, Start Menu shortcut, app launch, and uninstall behavior.
5. Test emergency unlock before routing field tests.
6. Confirm existing `%AppData%\vp-cursor-portal\config.json` behavior: keep, migrate, or delete intentionally.
7. Create and push a version tag, for example:

```bash
git tag v0.1.0
git push origin v0.1.0
```

The tag workflow creates release assets.

## Development Guidelines

Use this order when changing behavior:

1. Add or update tests in Core/H2/App as close to the behavior as possible.
2. Change pure logic first when possible.
3. Keep Win32 and WPF integration behind interfaces.
4. Keep `MainViewModel` as a facade; place new responsibility in a child ViewModel or service.
5. Preserve safety behavior before visual polish.
6. Run `dotnet test H2CursorRouter.sln` on Windows or rely on GitHub Actions if local OS lacks WPF support.

Avoid:

- real H2 or real cursor movement in automated tests,
- UI-only validation for routing math,
- applying cursor layouts after failed H2 ACK when ACK is required,
- replacing existing user config during install/update,
- logging high-frequency cursor movement into files.

## Test Coverage Map

Core tests:

- cursor routing decisions,
- hidden/outside-zone rejection,
- portal selection,
- full-edge and segmented mapping,
- validation,
- profile planning.

H2 tests:

- command serialization,
- ACK parsing,
- malformed responses,
- fake UDP integration cases.

App tests:

- row/config mapping,
- profile execution service,
- layout editing helpers,
- monitor-zone matching,
- XAML binding surface,
- log retention/noise policy,
- MainViewModel facade behavior.

## License, Notices, And About Dialog

No open-source license is currently declared in this repository. Without a license, copyright is retained by the owner by default and third parties should not assume redistribution or modification rights.

Before a public or customer-facing release, choose one of these policies:

- Open source: add a `LICENSE` file such as MIT or Apache-2.0.
- Proprietary/internal: add a clear proprietary license, EULA, or private repository notice.
- Customer handoff: include a short license/EULA in the installer and release notes.

The app includes an About dialog from the dashboard Startup section. It currently shows:

- product name: `vp-cursor-portal`,
- app version,
- informational version/build metadata when present,
- license summary,
- config/log path,
- app icon.

Before a broader release, consider adding publisher/company name, copyright, support/contact details, and a third-party notices link or bundled notice file.

The project currently uses the .NET runtime, WPF/WinForms platform libraries, GitHub Actions, and Inno Setup for installer generation. Review their license requirements before a formal public release.
