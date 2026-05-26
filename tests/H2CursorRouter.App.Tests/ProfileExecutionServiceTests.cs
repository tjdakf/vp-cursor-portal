using H2CursorRouter.App.Services;
using H2CursorRouter.Core.Configuration;
using H2CursorRouter.Core.Domain;
using H2CursorRouter.Core.Geometry;
using H2CursorRouter.Core.Profiles;
using H2CursorRouter.Core.Validation;
using H2CursorRouter.H2;
using H2CursorRouter.Windows;
using Xunit;

namespace H2CursorRouter.App.Tests;

public sealed class ProfileExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_DoesNotActivateLayoutWhenRequiredH2AckFails()
    {
        var h2Client = new FakeH2DeviceClient(H2CommandResult.Failure("ack error"));
        var runtime = new FakeCursorRoutingRuntime();
        var service = new ProfileExecutionService(h2Client, runtime, new CursorRoutingEngine(), new AppConfigurationValidator());
        var callbacks = new CallbackRecorder();
        var request = new ProfileExecutionRequest(
            CreateConfiguration(requireAck: true),
            CreateProfile(requireAck: true));

        await service.ExecuteAsync(request, callbacks.ToCallbacks());

        Assert.Equal(1, h2Client.LoadPresetCallCount);
        Assert.False(runtime.ActivateLayoutCalled);
        Assert.False(callbacks.StoppedRouting);
        Assert.Contains(callbacks.Logs, log => log.Contains("H2 preset load failed", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(callbacks.Logs, log => log.Contains("was not applied", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_ActivatesLayoutAfterSuccessfulH2Ack()
    {
        var h2Client = new FakeH2DeviceClient(H2CommandResult.Success("request", """[{"ack":"Ok"}]"""));
        var runtime = new FakeCursorRoutingRuntime();
        var service = new ProfileExecutionService(h2Client, runtime, new CursorRoutingEngine(), new AppConfigurationValidator());
        var callbacks = new CallbackRecorder();
        var request = new ProfileExecutionRequest(
            CreateConfiguration(requireAck: true),
            CreateProfile(requireAck: true));

        await service.ExecuteAsync(request, callbacks.ToCallbacks());

        Assert.Equal(1, h2Client.LoadPresetCallCount);
        Assert.True(runtime.ActivateLayoutCalled);
        Assert.Equal("layout", runtime.ActivatedLayout?.Id);
        Assert.Equal("layout", callbacks.SelectedLayoutId);
        Assert.True(callbacks.DeviceOnline);
        Assert.Equal("Online: 192.168.0.11:6000", callbacks.H2ConnectionStatus);
        Assert.DoesNotContain(callbacks.Logs, log => log.StartsWith("H2 response:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_ActivatesCursorOnlyProfileWithoutH2Command()
    {
        var h2Client = new FakeH2DeviceClient(H2CommandResult.Failure("should not be called"));
        var runtime = new FakeCursorRoutingRuntime();
        var service = new ProfileExecutionService(h2Client, runtime, new CursorRoutingEngine(), new AppConfigurationValidator());
        var callbacks = new CallbackRecorder();
        var configuration = CreateConfiguration(requireAck: true);
        var profile = new ExecutionProfile("profile", "Profile", null, null, "layout", null, 0, true);

        await service.ExecuteAsync(new ProfileExecutionRequest(configuration, profile), callbacks.ToCallbacks());

        Assert.Equal(0, h2Client.LoadPresetCallCount);
        Assert.True(runtime.ActivateLayoutCalled);
        Assert.Equal("layout", callbacks.SelectedLayoutId);
    }

    [Fact]
    public async Task ExecuteAsync_H2OnlyProfileSendsPresetWithoutActivatingLayout()
    {
        var h2Client = new FakeH2DeviceClient(H2CommandResult.Success("request", """[{"ack":"Ok"}]"""));
        var runtime = new FakeCursorRoutingRuntime();
        var service = new ProfileExecutionService(h2Client, runtime, new CursorRoutingEngine(), new AppConfigurationValidator());
        var callbacks = new CallbackRecorder();
        var configuration = CreateConfiguration(requireAck: true);
        var profile = new ExecutionProfile(
            "profile",
            "Profile",
            null,
            new H2PresetRef("h2", 0, 0, "Preset 1 / presetId 0"),
            null,
            null,
            0,
            true);

        await service.ExecuteAsync(new ProfileExecutionRequest(configuration, profile), callbacks.ToCallbacks());

        Assert.Equal(1, h2Client.LoadPresetCallCount);
        Assert.Equal("h2", h2Client.DeviceId);
        Assert.Equal(0, h2Client.ScreenId);
        Assert.Equal(0, h2Client.PresetId);
        Assert.True(callbacks.StoppedRouting);
        Assert.False(callbacks.StopClearedLayout);
        Assert.False(runtime.ActivateLayoutCalled);
        Assert.Null(callbacks.SelectedLayoutId);
    }

    [Fact]
    public async Task ExecuteAsync_H2OnlyProfileDoesNotStopRoutingWhenPresetFails()
    {
        var h2Client = new FakeH2DeviceClient(H2CommandResult.Failure("ack error"));
        var runtime = new FakeCursorRoutingRuntime();
        var service = new ProfileExecutionService(h2Client, runtime, new CursorRoutingEngine(), new AppConfigurationValidator());
        var callbacks = new CallbackRecorder();
        var configuration = CreateConfiguration(requireAck: true);
        var profile = new ExecutionProfile(
            "profile",
            "Profile",
            null,
            new H2PresetRef("h2", 0, 0, "Preset 1 / presetId 0"),
            null,
            null,
            0,
            true);

        await service.ExecuteAsync(new ProfileExecutionRequest(configuration, profile), callbacks.ToCallbacks());

        Assert.Equal(1, h2Client.LoadPresetCallCount);
        Assert.False(callbacks.StoppedRouting);
        Assert.False(runtime.ActivateLayoutCalled);
        Assert.Contains(callbacks.Logs, log => log.Contains("H2 preset load failed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_AckFailureCanStillApplyLayoutWhenAckIsNotRequired()
    {
        var h2Client = new FakeH2DeviceClient(H2CommandResult.Failure("ack error"));
        var runtime = new FakeCursorRoutingRuntime();
        var service = new ProfileExecutionService(h2Client, runtime, new CursorRoutingEngine(), new AppConfigurationValidator());
        var callbacks = new CallbackRecorder();
        var request = new ProfileExecutionRequest(
            CreateConfiguration(requireAck: false),
            CreateProfile(requireAck: false));

        await service.ExecuteAsync(request, callbacks.ToCallbacks());

        Assert.Equal(1, h2Client.LoadPresetCallCount);
        Assert.True(runtime.ActivateLayoutCalled);
        Assert.Equal("layout", callbacks.SelectedLayoutId);
        Assert.Contains(callbacks.Logs, log => log.Contains("H2 preset load failed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_RuntimeDisabledAfterActivationLogsDidNotStart()
    {
        var h2Client = new FakeH2DeviceClient(H2CommandResult.Success("request", """[{"ack":"Ok"}]"""));
        var runtime = new FakeCursorRoutingRuntime { EnableOnActivate = false };
        var service = new ProfileExecutionService(h2Client, runtime, new CursorRoutingEngine(), new AppConfigurationValidator());
        var callbacks = new CallbackRecorder();

        await service.ExecuteAsync(
            new ProfileExecutionRequest(CreateConfiguration(requireAck: true), CreateProfile(requireAck: true)),
            callbacks.ToCallbacks());

        Assert.True(runtime.ActivateLayoutCalled);
        Assert.Contains(callbacks.Logs, log => log.Contains("Routing did not start", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidConfigurationDoesNotStopOrCallH2()
    {
        var h2Client = new FakeH2DeviceClient(H2CommandResult.Success("request", """[{"ack":"Ok"}]"""));
        var runtime = new FakeCursorRoutingRuntime();
        var service = new ProfileExecutionService(h2Client, runtime, new CursorRoutingEngine(), new AppConfigurationValidator());
        var callbacks = new CallbackRecorder();
        var profile = new ExecutionProfile("profile", "Profile", null, null, null, null, 0, true);
        var configuration = new AppConfiguration([], [], [profile], SafetySettings.Default);

        await service.ExecuteAsync(new ProfileExecutionRequest(configuration, profile), callbacks.ToCallbacks());

        Assert.Equal(0, h2Client.LoadPresetCallCount);
        Assert.False(callbacks.StoppedRouting);
        Assert.False(runtime.ActivateLayoutCalled);
        Assert.Contains(callbacks.Logs, log => log.Contains("must reference", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_StopsBeforeActivatingBoundLayout()
    {
        var h2Client = new FakeH2DeviceClient(H2CommandResult.Success("request", """[{"ack":"Ok"}]"""));
        var runtime = new FakeCursorRoutingRuntime();
        var service = new ProfileExecutionService(h2Client, runtime, new CursorRoutingEngine(), new AppConfigurationValidator());
        var callbacks = new CallbackRecorder();

        await service.ExecuteAsync(
            new ProfileExecutionRequest(CreateConfiguration(requireAck: true), CreateProfile(requireAck: true)),
            callbacks.ToCallbacks());

        Assert.True(callbacks.StoppedRouting);
        Assert.True(callbacks.StopClearedLayout);
        Assert.True(runtime.ActivateLayoutCalled);
    }

    private static AppConfiguration CreateConfiguration(bool requireAck) => new(
        [new H2DeviceConfig("h2", "H2", "192.168.0.11", 6000, 0, TimeSpan.FromMilliseconds(250))],
        [CreateLayout()],
        [CreateProfile(requireAck)],
        SafetySettings.Default);

    private static ExecutionProfile CreateProfile(bool requireAck) => new(
        "profile",
        "Profile",
        null,
        new H2PresetRef("h2", 0, 0, "Preset 1 / presetId 0"),
        "layout",
        null,
        0,
        requireAck);

    private static CursorLayout CreateLayout() => new(
        "layout",
        "Layout",
        [
            new CursorZone(
                "DISPLAY1",
                "Monitor 1",
                new IntRect(0, 0, 1920, 1080),
                new VisualRect(0, 0, 1920, 1080),
                true)
        ],
        []);

    private sealed class FakeH2DeviceClient(H2CommandResult loadPresetResult) : IH2DeviceClient
    {
        public int LoadPresetCallCount { get; private set; }
        public string? DeviceId { get; private set; }
        public int? ScreenId { get; private set; }
        public int? PresetId { get; private set; }

        public Task<H2CommandResult> LoadPresetAsync(
            H2DeviceConfig device,
            int screenId,
            int presetId,
            CancellationToken cancellationToken = default)
        {
            LoadPresetCallCount++;
            DeviceId = device.Id;
            ScreenId = screenId;
            PresetId = presetId;
            return Task.FromResult(loadPresetResult);
        }

        public Task<H2CommandResult> GetPresetEnumAsync(
            H2DeviceConfig device,
            int param0 = 0,
            int param1 = 0,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(H2CommandResult.Failure("not implemented"));
    }

    private sealed class FakeCursorRoutingRuntime : ICursorRoutingRuntime
    {
        public event EventHandler<string>? Log;

        public bool IsRoutingEnabled { get; private set; }
        public bool EnableOnActivate { get; init; } = true;
        public string? ActiveLayoutId => ActivatedLayout?.Id;
        public bool ActivateLayoutCalled { get; private set; }
        public CursorLayout? ActivatedLayout { get; private set; }

        public void ActivateLayout(CursorLayout layout, CursorPoint startPosition, TimeSpan pollInterval)
        {
            ActivateLayoutCalled = true;
            ActivatedLayout = layout;
            IsRoutingEnabled = EnableOnActivate;
            Log?.Invoke(this, $"Activated cursor layout '{layout.Name}'.");
        }

        public void StopRouting(bool clearLayout)
        {
            IsRoutingEnabled = false;
            if (clearLayout)
            {
                ActivatedLayout = null;
            }
        }

        public void EmergencyUnlock() => StopRouting(clearLayout: true);
        public void Dispose()
        {
        }
    }

    private sealed class CallbackRecorder
    {
        public List<string> Logs { get; } = [];
        public bool StoppedRouting { get; private set; }
        public bool StopClearedLayout { get; private set; }
        public bool DeviceOnline { get; private set; }
        public string? SelectedLayoutId { get; private set; }
        public string? H2ConnectionStatus { get; private set; }

        public ProfileExecutionCallbacks ToCallbacks() => new()
        {
            ShowValidation = result =>
            {
                if (!result.IsValid)
                {
                    Logs.AddRange(result.Errors);
                }
            },
            SetH2ConnectionStatus = value => H2ConnectionStatus = value,
            SetDeviceOnline = (_, online) => DeviceOnline = online,
            SelectLayout = layoutId => SelectedLayoutId = layoutId,
            StopRouting = clearLayout =>
            {
                StoppedRouting = true;
                StopClearedLayout = clearLayout;
            },
            AddLog = Logs.Add,
            RefreshRuntimeState = () => { }
        };
    }
}
