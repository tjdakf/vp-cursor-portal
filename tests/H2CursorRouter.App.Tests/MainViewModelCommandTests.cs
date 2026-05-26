using System.IO;
using H2CursorRouter.App;
using H2CursorRouter.App.ViewModels;
using H2CursorRouter.Core.Configuration;
using H2CursorRouter.Core.Domain;
using H2CursorRouter.Core.Geometry;
using H2CursorRouter.Core.Profiles;
using H2CursorRouter.Core.Validation;
using H2CursorRouter.H2;
using H2CursorRouter.Windows;
using Xunit;

namespace H2CursorRouter.App.Tests;

public sealed class MainViewModelCommandTests
{
    [Fact]
    public void FacadeCollectionsAreBackedByChildViewModels()
    {
        using var fixture = new MainViewModelFixture();
        var viewModel = fixture.Create();

        Assert.Same(viewModel.DevicePresets.Devices, viewModel.Devices);
        Assert.Same(viewModel.DevicePresets.Presets, viewModel.Presets);
        Assert.Same(viewModel.LayoutEditor.Layouts, viewModel.Layouts);
        Assert.Same(viewModel.LayoutEditor.SelectedLayoutZones, viewModel.SelectedLayoutZones);
        Assert.Same(viewModel.ProfileList.Profiles, viewModel.Profiles);
        Assert.Same(viewModel.RuntimeLog.Logs, viewModel.Logs);
    }

    [Fact]
    public void SelectedLayoutRefreshesFacadeLayoutCollections()
    {
        using var fixture = new MainViewModelFixture();
        var viewModel = fixture.Create();
        var layout = new LayoutRow { Id = "layout", Name = "Layout" };
        viewModel.Layouts.Add(layout);
        viewModel.Zones.Add(new ZoneRow
        {
            LayoutId = "layout",
            Id = "DISPLAY1",
            DisplayName = "Monitor 1",
            WindowsRight = 100,
            WindowsBottom = 100,
            VisualRight = 100,
            VisualBottom = 100,
            IsVisible = true
        });

        viewModel.SelectedLayout = layout;

        var selectedZone = Assert.Single(viewModel.SelectedLayoutZones);
        Assert.Same(selectedZone, viewModel.SelectedZone);
        Assert.True(selectedZone.IsSelected);
        Assert.True(viewModel.HasSelectedZone);
    }

    [Fact]
    public void SelectedZoneUpdatesSelectionStateThroughFacade()
    {
        using var fixture = new MainViewModelFixture();
        var viewModel = fixture.Create();
        var first = new ZoneRow { Id = "DISPLAY1" };
        var second = new ZoneRow { Id = "DISPLAY2" };

        viewModel.SelectedZone = first;
        viewModel.SelectedZone = second;

        Assert.False(first.IsSelected);
        Assert.True(second.IsSelected);
        Assert.Same(second, viewModel.LayoutEditor.SelectedZone);
    }

    [Fact]
    public void ConstructorHandlesExistingProfilesWhenFilterIsAttached()
    {
        using var fixture = new MainViewModelFixture();
        var viewModel = fixture.Create(new AppConfiguration(
            [],
            [],
            [
                new ExecutionProfile(
                    "profile",
                    "Profile",
                    "Ctrl+Alt+1",
                    null,
                    null,
                    null,
                    500,
                    true)
            ],
            SafetySettings.Default));

        Assert.Single(viewModel.Profiles);
        viewModel.ProfileFilter = "profile";
        Assert.True(viewModel.FilteredProfiles.Cast<object>().Any());
    }

    [Fact]
    public void ChildViewModelPropertyChangesRelayToFacadeBindings()
    {
        using var fixture = new MainViewModelFixture();
        var viewModel = fixture.Create();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.RuntimeLog.RuntimeStatus = "Changed";
        viewModel.DevicePresets.H2ConnectionStatus = "Online: 192.168.0.11:6000";
        viewModel.LayoutEditor.SelectedZone = new ZoneRow { Id = "DISPLAY1" };

        Assert.Contains(nameof(MainViewModel.RuntimeStatus), changed);
        Assert.Contains(nameof(MainViewModel.H2ConnectionStatus), changed);
        Assert.Contains(nameof(MainViewModel.IsH2Online), changed);
        Assert.Contains(nameof(MainViewModel.SelectedZone), changed);
        Assert.Contains(nameof(MainViewModel.HasSelectedZone), changed);
    }

    [Fact]
    public void HighFrequencyRoutingDiagnosticsUpdateLastEventWithoutVisibleLogEntry()
    {
        using var fixture = new MainViewModelFixture();
        var viewModel = fixture.Create();
        var initialLogCount = viewModel.Logs.Count;

        viewModel.AddLog("Portal move: Portal mapped 'DISPLAY1' Right 0-1 to 'DISPLAY3' Left. Target: 3840, 540.");

        Assert.Equal(initialLogCount, viewModel.Logs.Count);
        Assert.Equal("Portal move: Portal mapped 'DISPLAY1' Right 0-1 to 'DISPLAY3' Left. Target: 3840, 540.", viewModel.LastRoutingEvent);
    }

    [Fact]
    public void AddPortalUsesSelectedDraftLayoutZones()
    {
        using var fixture = new MainViewModelFixture();
        var viewModel = fixture.Create();
        viewModel.SelectedLayout = new LayoutRow { Id = "draft", Name = "Draft" };
        viewModel.SelectedLayoutZones.Add(new ZoneRow
        {
            LayoutId = "draft",
            Id = "DISPLAY1",
            DisplayName = "Monitor 1",
            WindowsLeft = 0,
            WindowsTop = 0,
            WindowsRight = 100,
            WindowsBottom = 100,
            VisualLeft = 0,
            VisualTop = 0,
            VisualRight = 100,
            VisualBottom = 100,
            IsVisible = true
        });
        viewModel.SelectedLayoutZones.Add(new ZoneRow
        {
            LayoutId = "draft",
            Id = "DISPLAY2",
            DisplayName = "Monitor 2",
            WindowsLeft = 100,
            WindowsTop = 0,
            WindowsRight = 200,
            WindowsBottom = 100,
            VisualLeft = 100,
            VisualTop = 0,
            VisualRight = 200,
            VisualBottom = 100,
            IsVisible = true
        });

        viewModel.AddPortalCommand.Execute(null);

        var portal = Assert.Single(viewModel.SelectedLayoutPortals);
        Assert.Equal("draft", portal.LayoutId);
        Assert.Equal("DISPLAY1", portal.FromZoneId);
        Assert.Equal("DISPLAY2", portal.ToZoneId);
        Assert.Same(portal, viewModel.SelectedPortal);
    }

    [Fact]
    public void SaveLayoutAsNewDoesNotAttachSeparatedZonesBeforeGeneratingPortals()
    {
        using var fixture = new MainViewModelFixture();
        var viewModel = fixture.Create();
        viewModel.SelectedLayout = new LayoutRow { Id = "draft", Name = "Draft" };
        viewModel.SelectedLayoutZones.Add(CreateVisibleZone("draft", "DISPLAY1", 0, 0, 120, 120));
        viewModel.SelectedLayoutZones.Add(CreateVisibleZone("draft", "DISPLAY2", 120, 0, 240, 120));
        viewModel.SelectedLayoutZones.Add(CreateVisibleZone("draft", "DISPLAY3", 280, 0, 400, 120));

        viewModel.SaveLayoutAsNewCommand.Execute(null);

        Assert.DoesNotContain(viewModel.SelectedLayoutPortals, portal =>
            string.Equals(portal.FromZoneId, "DISPLAY3", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(portal.ToZoneId, "DISPLAY3", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, viewModel.SelectedLayoutPortals.Count);
    }

    private static ZoneRow CreateVisibleZone(string layoutId, string id, int left, int top, int right, int bottom) => new()
    {
        LayoutId = layoutId,
        Id = id,
        DisplayName = id,
        WindowsLeft = left,
        WindowsTop = top,
        WindowsRight = right,
        WindowsBottom = bottom,
        VisualLeft = left,
        VisualTop = top,
        VisualRight = right,
        VisualBottom = bottom,
        IsVisible = true
    };

    private sealed class MainViewModelFixture : IDisposable
    {
        private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"h2-app-tests-{Guid.NewGuid():N}");

        public MainViewModel Create(AppConfiguration? configuration = null) => new(
            configuration ?? new AppConfiguration([], [], [], SafetySettings.Default),
            Path.Combine(_tempDirectory, "config.json"),
            "app.exe",
            new StartupRegistrationStub(),
            new FileLogService(Path.Combine(_tempDirectory, "logs")),
            new H2DeviceClientStub(),
            new DisplayIdentificationStub(),
            new TextInputDialogStub(),
            new ConfirmationDialogStub(),
            new ProfileDialogStub(),
            new DeviceDialogStub(),
            new MonitorTopologyStub(),
            new CursorRoutingRuntimeStub(),
            new CursorRoutingEngine(),
            new AppConfigurationValidator());

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
    }

    private sealed class StartupRegistrationStub : IStartupRegistrationService
    {
        public bool IsRegistered() => false;
        public void SetRegistered(bool enabled, string executablePath, string arguments)
        {
        }
    }

    private sealed class H2DeviceClientStub : IH2DeviceClient
    {
        public Task<H2CommandResult> LoadPresetAsync(H2DeviceConfig device, int screenId, int presetId, CancellationToken cancellationToken = default) =>
            Task.FromResult(H2CommandResult.Failure("not implemented"));

        public Task<H2CommandResult> GetPresetEnumAsync(H2DeviceConfig device, int param0 = 0, int param1 = 0, CancellationToken cancellationToken = default) =>
            Task.FromResult(H2CommandResult.Failure("not implemented"));
    }

    private sealed class DisplayIdentificationStub : IDisplayIdentificationService
    {
        public Task IdentifyAsync(IReadOnlyList<MonitorRow> monitors, TimeSpan duration) => Task.CompletedTask;
    }

    private sealed class TextInputDialogStub : ITextInputDialogService
    {
        public string? Prompt(string title, string message, string defaultValue) => defaultValue;
    }

    private sealed class ConfirmationDialogStub : IConfirmationDialogService
    {
        public bool Confirm(string title, string message) => true;
    }

    private sealed class ProfileDialogStub : IProfileDialogService
    {
        public ProfileDialogResult? Prompt(
            string title,
            string defaultName,
            string? selectedHotkey,
            IReadOnlyList<LayoutRow> layouts,
            string? selectedLayoutId,
            bool isCursorLayoutOnly,
            IReadOnlyList<DeviceRow> devices,
            IReadOnlyList<PresetRow> presets,
            string? selectedDeviceId,
            int? selectedScreenId,
            int? selectedPresetId,
            string? selectedPresetDisplayName) =>
            null;
    }

    private sealed class DeviceDialogStub : IDeviceDialogService
    {
        public DeviceDialogResult? Prompt(string defaultName, string defaultHost, int defaultPort) => null;
    }

    private sealed class MonitorTopologyStub : IMonitorTopologyService
    {
        public event EventHandler? TopologyChanged;
        public IReadOnlyList<MonitorInfo> GetMonitors() => [];
        public string GetTopologySignature() => "";
        public void StartWatching(TimeSpan interval) => TopologyChanged?.Invoke(this, EventArgs.Empty);
        public void Dispose()
        {
        }
    }

    private sealed class CursorRoutingRuntimeStub : ICursorRoutingRuntime
    {
        public event EventHandler<string>? Log;
        public bool IsRoutingEnabled => false;
        public string? ActiveLayoutId => null;
        public void ActivateLayout(CursorLayout layout, CursorPoint startPosition, TimeSpan pollInterval) =>
            Log?.Invoke(this, $"Activated cursor layout '{layout.Name}'.");

        public void StopRouting(bool clearLayout)
        {
        }

        public void EmergencyUnlock()
        {
        }

        public void Dispose()
        {
        }
    }
}
