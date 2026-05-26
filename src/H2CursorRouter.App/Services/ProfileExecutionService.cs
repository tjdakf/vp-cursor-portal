using H2CursorRouter.Core.Configuration;
using H2CursorRouter.Core.Geometry;
using H2CursorRouter.Core.Profiles;
using H2CursorRouter.Core.Validation;
using H2CursorRouter.H2;
using H2CursorRouter.Windows;

namespace H2CursorRouter.App.Services;

public sealed class ProfileExecutionService
{
    private readonly IH2DeviceClient _h2DeviceClient;
    private readonly ICursorRoutingRuntime _routingRuntime;
    private readonly CursorRoutingEngine _routingEngine;
    private readonly AppConfigurationValidator _configurationValidator;

    public ProfileExecutionService(
        IH2DeviceClient h2DeviceClient,
        ICursorRoutingRuntime routingRuntime,
        CursorRoutingEngine routingEngine,
        AppConfigurationValidator configurationValidator)
    {
        _h2DeviceClient = h2DeviceClient;
        _routingRuntime = routingRuntime;
        _routingEngine = routingEngine;
        _configurationValidator = configurationValidator;
    }

    public async Task ExecuteAsync(ProfileExecutionRequest request, ProfileExecutionCallbacks callbacks)
    {
        var validation = _configurationValidator.Validate(request.Configuration);
        callbacks.ShowValidation(validation);
        if (!validation.IsValid)
        {
            callbacks.AddLog("Configuration validation failed. Fix validation messages before executing.");
            return;
        }

        var profile = request.Profile;
        callbacks.AddLog($"Executing profile '{profile.Name}'.");

        bool? h2AckOk = null;
        if (profile.H2Preset is not null)
        {
            var device = request.Configuration.Devices.FirstOrDefault(device =>
                string.Equals(device.Id, profile.H2Preset.DeviceId, StringComparison.OrdinalIgnoreCase));
            if (device is null)
            {
                callbacks.AddLog($"Profile references missing device '{profile.H2Preset.DeviceId}'.");
                return;
            }

            callbacks.AddLog($"Sending W0605 to {device.Host}:{device.Port} screenId={profile.H2Preset.ScreenId} presetId={profile.H2Preset.PresetId}.");
            var result = await _h2DeviceClient.LoadPresetAsync(device, profile.H2Preset.ScreenId, profile.H2Preset.PresetId);
            callbacks.SetDeviceOnline(profile.H2Preset.DeviceId, result.IsSuccess);

            h2AckOk = result.IsSuccess;
            callbacks.SetH2ConnectionStatus(result.IsSuccess
                ? $"Online: {device.Host}:{device.Port}"
                : $"No response: {result.Message}");
            callbacks.AddLog(result.IsSuccess ? "H2 preset load acknowledged." : $"H2 preset load failed: {result.Message}");
            if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ResponseJson))
            {
                callbacks.AddLog($"H2 response: {result.ResponseJson}");
            }
        }

        if (!ProfileExecutionPlanner.ShouldApplyCursorLayout(profile, h2AckOk))
        {
            if (profile.CursorLayoutId is not null)
            {
                callbacks.AddLog("Cursor layout was not applied because H2 ACK is required and did not succeed.");
            }

            callbacks.RefreshRuntimeState();
            return;
        }

        if (profile.H2Preset is not null && h2AckOk == true && profile.PostAckDelayMs > 0)
        {
            await Task.Delay(profile.PostAckDelayMs);
        }

        if (profile.CursorLayoutId is not null)
        {
            var layout = request.Configuration.CursorLayouts.FirstOrDefault(layout =>
                string.Equals(layout.Id, profile.CursorLayoutId, StringComparison.OrdinalIgnoreCase));
            if (layout is null)
            {
                callbacks.AddLog($"Profile references missing cursor layout '{profile.CursorLayoutId}'.");
                return;
            }

            var startPosition = _routingEngine.ResolveStartPosition(layout, profile.StartPosition);
            callbacks.StopRouting(clearLayout: true);
            _routingRuntime.ActivateLayout(layout, startPosition, TimeSpan.FromMilliseconds(15));
            callbacks.SelectLayout(layout.Id);
            callbacks.AddLog(_routingRuntime.IsRoutingEnabled
                ? $"Routing started for layout '{layout.Name}'."
                : $"Routing did not start for layout '{layout.Name}'. See runtime log for details.");
        }

        callbacks.RefreshRuntimeState();
    }
}

public sealed record ProfileExecutionRequest(
    AppConfiguration Configuration,
    ExecutionProfile Profile);

public sealed class ProfileExecutionCallbacks
{
    public required Action<ValidationResult> ShowValidation { get; init; }
    public required Action<string> SetH2ConnectionStatus { get; init; }
    public required Action<string, bool> SetDeviceOnline { get; init; }
    public required Action<string> SelectLayout { get; init; }
    public required Action<bool> StopRouting { get; init; }
    public required Action<string> AddLog { get; init; }
    public required Action RefreshRuntimeState { get; init; }
}
