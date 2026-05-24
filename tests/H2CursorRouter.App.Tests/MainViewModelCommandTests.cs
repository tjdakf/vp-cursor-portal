using H2CursorRouter.App;
using H2CursorRouter.App.ViewModels;
using H2CursorRouter.Core.Configuration;
using H2CursorRouter.Core.Domain;
using H2CursorRouter.Core.Geometry;
using H2CursorRouter.Core.Validation;
using H2CursorRouter.H2;
using H2CursorRouter.Windows;
using Xunit;

namespace H2CursorRouter.App.Tests;

public sealed class MainViewModelCommandTests
{
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

    private sealed class MainViewModelFixture : IDisposable
    {
        private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"h2-app-tests-{Guid.NewGuid():N}");

        public MainViewModel Create() => new(
            new AppConfiguration([], [], [], SafetySettings.Default),
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
