using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using H2CursorRouter.Core.Configuration;
using H2CursorRouter.Core.Domain;
using H2CursorRouter.Core.Geometry;
using H2CursorRouter.Core.Profiles;
using H2CursorRouter.Core.Validation;
using H2CursorRouter.H2;
using H2CursorRouter.Windows;

namespace H2CursorRouter.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly AppConfiguration _configuration;
    private readonly IH2DeviceClient _h2DeviceClient;
    private readonly ICursorService _cursorService;
    private readonly IMonitorTopologyService _monitorTopologyService;
    private readonly CursorRoutingRuntime _routingRuntime;
    private readonly CursorRoutingEngine _routingEngine;
    private readonly AppConfigurationValidator _configurationValidator;
    private H2DeviceConfig? _selectedDevice;
    private CursorLayout? _selectedLayout;
    private ExecutionProfile? _selectedProfile;
    private string _runtimeStatus = "Routing disabled on startup.";
    private string _currentCursorPosition = "";
    private string _currentCursorZone = "";
    private string _activeProfileName = "";

    public MainViewModel(
        AppConfiguration configuration,
        IH2DeviceClient h2DeviceClient,
        ICursorService cursorService,
        IMonitorTopologyService monitorTopologyService,
        CursorRoutingRuntime routingRuntime,
        CursorRoutingEngine routingEngine,
        AppConfigurationValidator configurationValidator)
    {
        _configuration = configuration;
        _h2DeviceClient = h2DeviceClient;
        _cursorService = cursorService;
        _monitorTopologyService = monitorTopologyService;
        _routingRuntime = routingRuntime;
        _routingEngine = routingEngine;
        _configurationValidator = configurationValidator;

        Devices = new ObservableCollection<H2DeviceConfig>(configuration.Devices);
        CursorLayouts = new ObservableCollection<CursorLayout>(configuration.CursorLayouts);
        Profiles = new ObservableCollection<ExecutionProfile>(configuration.Profiles);
        Presets = new ObservableCollection<PresetRow>(configuration.Profiles
            .Where(profile => profile.H2Preset is not null)
            .Select(profile => new PresetRow(
                profile.H2Preset!.DeviceId,
                profile.H2Preset.ScreenId,
                profile.H2Preset.PresetId + 1,
                profile.H2Preset.PresetId,
                profile.H2Preset.DisplayName)));
        Monitors = new ObservableCollection<MonitorRow>();
        Logs = new ObservableCollection<string>();

        SelectedDevice = Devices.FirstOrDefault();
        SelectedLayout = CursorLayouts.FirstOrDefault();
        SelectedProfile = Profiles.FirstOrDefault();

        ExecuteSelectedProfileCommand = new AsyncRelayCommand(ExecuteSelectedProfileAsync, () => SelectedProfile is not null);
        GetPresetsCommand = new AsyncRelayCommand(GetPresetsAsync, () => SelectedDevice is not null);
        EmergencyUnlockCommand = new RelayCommand(EmergencyUnlock);
        StopRoutingCommand = new RelayCommand(() => StopRouting(clearLayout: true));
        RefreshDiagnosticsCommand = new RelayCommand(RefreshDiagnostics);

        _routingRuntime.Log += (_, message) => Dispatch(() => AddLog(message));
        RefreshDiagnostics();
        AddLog("Application started with routing disabled.");
    }

    public ObservableCollection<H2DeviceConfig> Devices { get; }
    public ObservableCollection<CursorLayout> CursorLayouts { get; }
    public ObservableCollection<ExecutionProfile> Profiles { get; }
    public ObservableCollection<PresetRow> Presets { get; }
    public ObservableCollection<MonitorRow> Monitors { get; }
    public ObservableCollection<string> Logs { get; }

    public ICommand ExecuteSelectedProfileCommand { get; }
    public ICommand GetPresetsCommand { get; }
    public ICommand EmergencyUnlockCommand { get; }
    public ICommand StopRoutingCommand { get; }
    public ICommand RefreshDiagnosticsCommand { get; }

    public H2DeviceConfig? SelectedDevice
    {
        get => _selectedDevice;
        set => SetProperty(ref _selectedDevice, value);
    }

    public CursorLayout? SelectedLayout
    {
        get => _selectedLayout;
        set
        {
            if (SetProperty(ref _selectedLayout, value))
            {
                RefreshCursorZone();
            }
        }
    }

    public ExecutionProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                ActiveProfileName = value?.Name ?? "";
            }
        }
    }

    public string RuntimeStatus
    {
        get => _runtimeStatus;
        private set => SetProperty(ref _runtimeStatus, value);
    }

    public string CurrentCursorPosition
    {
        get => _currentCursorPosition;
        private set => SetProperty(ref _currentCursorPosition, value);
    }

    public string CurrentCursorZone
    {
        get => _currentCursorZone;
        private set => SetProperty(ref _currentCursorZone, value);
    }

    public string ActiveProfileName
    {
        get => _activeProfileName;
        private set => SetProperty(ref _activeProfileName, value);
    }

    public string ActiveLayoutId => _routingRuntime.ActiveLayoutId ?? "";
    public bool IsRoutingEnabled => _routingRuntime.IsRoutingEnabled;

    public async Task ExecuteProfileByIndexAsync(int index)
    {
        if (index < 0 || index >= Profiles.Count)
        {
            return;
        }

        SelectedProfile = Profiles[index];
        await ExecuteSelectedProfileAsync();
    }

    public async Task ExecuteSelectedProfileAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var validation = _configurationValidator.Validate(_configuration);
        if (!validation.IsValid)
        {
            AddLog($"Configuration validation failed: {string.Join("; ", validation.Errors)}");
            return;
        }

        var profile = SelectedProfile;
        ActiveProfileName = profile.Name;
        AddLog($"Executing profile '{profile.Name}'.");
        StopRouting(clearLayout: profile.CursorLayoutId is not null);

        bool? h2AckOk = null;
        if (profile.H2Preset is not null)
        {
            var device = Devices.FirstOrDefault(device =>
                string.Equals(device.Id, profile.H2Preset.DeviceId, StringComparison.OrdinalIgnoreCase));
            if (device is null)
            {
                AddLog($"Profile references missing device '{profile.H2Preset.DeviceId}'.");
                return;
            }

            var result = await _h2DeviceClient.LoadPresetAsync(device, profile.H2Preset.ScreenId, profile.H2Preset.PresetId);
            h2AckOk = result.IsSuccess;
            AddLog(result.IsSuccess ? "H2 preset load acknowledged." : $"H2 preset load failed: {result.Message}");
        }

        if (!ProfileExecutionPlanner.ShouldApplyCursorLayout(profile, h2AckOk))
        {
            if (profile.CursorLayoutId is not null)
            {
                AddLog("Cursor layout was not applied because H2 ACK is required and did not succeed.");
            }

            RefreshRuntimeState();
            return;
        }

        if (profile.H2Preset is not null && h2AckOk == true && profile.PostAckDelayMs > 0)
        {
            await Task.Delay(profile.PostAckDelayMs);
        }

        if (profile.CursorLayoutId is not null)
        {
            var layout = CursorLayouts.FirstOrDefault(layout =>
                string.Equals(layout.Id, profile.CursorLayoutId, StringComparison.OrdinalIgnoreCase));
            if (layout is null)
            {
                AddLog($"Profile references missing cursor layout '{profile.CursorLayoutId}'.");
                return;
            }

            var startPosition = _routingEngine.ResolveStartPosition(layout, profile.StartPosition);
            _routingRuntime.ActivateLayout(layout, startPosition, TimeSpan.FromMilliseconds(15));
            SelectedLayout = layout;
        }

        RefreshRuntimeState();
    }

    public async Task GetPresetsAsync()
    {
        if (SelectedDevice is null)
        {
            AddLog("No H2 device selected.");
            return;
        }

        var result = await _h2DeviceClient.GetPresetEnumAsync(SelectedDevice);
        AddLog(result.IsSuccess
            ? $"Preset enum response: {result.ResponseJson}"
            : $"Preset enum request failed: {result.Message}");
    }

    public void EmergencyUnlock()
    {
        _routingRuntime.EmergencyUnlock();
        ActiveProfileName = "";
        RefreshRuntimeState();
    }

    public void Shutdown()
    {
        _routingRuntime.EmergencyUnlock();
    }

    public void AddLog(string message)
    {
        Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        RuntimeStatus = message;
        RefreshRuntimeState();
    }

    private void StopRouting(bool clearLayout)
    {
        _routingRuntime.StopRouting(clearLayout);
        AddLog("Routing stopped and cursor clipping released.");
        RefreshRuntimeState();
    }

    private void RefreshDiagnostics()
    {
        Monitors.Clear();
        foreach (var monitor in _monitorTopologyService.GetMonitors())
        {
            Monitors.Add(new MonitorRow(
                monitor.DeviceName,
                monitor.Bounds.Left,
                monitor.Bounds.Top,
                monitor.Bounds.Right,
                monitor.Bounds.Bottom,
                monitor.IsPrimary));
        }

        var position = _cursorService.GetPosition();
        CurrentCursorPosition = $"{position.X}, {position.Y}";
        RefreshCursorZone();
    }

    private void RefreshCursorZone()
    {
        if (SelectedLayout is null)
        {
            CurrentCursorZone = "";
            return;
        }

        var position = _cursorService.GetPosition();
        CurrentCursorPosition = $"{position.X}, {position.Y}";
        var zone = _routingEngine.FindZone(SelectedLayout, position);
        CurrentCursorZone = zone is null ? "Outside known zones" : $"{zone.Id} ({(zone.IsVisible ? "visible" : "hidden")})";
    }

    private void RefreshRuntimeState()
    {
        OnPropertyChanged(nameof(ActiveLayoutId));
        OnPropertyChanged(nameof(IsRoutingEnabled));
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }
}
