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
    private readonly string _configPath;
    private readonly ConfigFileService _configFileService = new();
    private readonly IH2DeviceClient _h2DeviceClient;
    private readonly ICursorService _cursorService;
    private readonly IMonitorTopologyService _monitorTopologyService;
    private readonly CursorRoutingRuntime _routingRuntime;
    private readonly CursorRoutingEngine _routingEngine;
    private readonly AppConfigurationValidator _configurationValidator;
    private readonly H2PresetEnumParser _presetEnumParser = new();
    private DeviceRow? _selectedDevice;
    private PresetRow? _selectedPreset;
    private LayoutRow? _selectedLayout;
    private ZoneRow? _selectedZone;
    private PortalRow? _selectedPortal;
    private ProfileRow? _selectedProfile;
    private string _runtimeStatus = "Routing disabled on startup.";
    private string _currentCursorPosition = "";
    private string _currentCursorZone = "";
    private string _activeProfileName = "";

    public MainViewModel(
        AppConfiguration configuration,
        string configPath,
        IH2DeviceClient h2DeviceClient,
        ICursorService cursorService,
        IMonitorTopologyService monitorTopologyService,
        CursorRoutingRuntime routingRuntime,
        CursorRoutingEngine routingEngine,
        AppConfigurationValidator configurationValidator)
    {
        _configPath = configPath;
        _h2DeviceClient = h2DeviceClient;
        _cursorService = cursorService;
        _monitorTopologyService = monitorTopologyService;
        _routingRuntime = routingRuntime;
        _routingEngine = routingEngine;
        _configurationValidator = configurationValidator;

        Devices = new ObservableCollection<DeviceRow>(configuration.Devices.Select(DeviceRow.FromModel));
        Layouts = new ObservableCollection<LayoutRow>(configuration.CursorLayouts.Select(LayoutRow.FromModel));
        Zones = new ObservableCollection<ZoneRow>(configuration.CursorLayouts.SelectMany(layout =>
            layout.Zones.Select(zone => ZoneRow.FromModel(layout.Id, zone))));
        Portals = new ObservableCollection<PortalRow>(configuration.CursorLayouts.SelectMany(layout =>
            layout.Portals.Select(portal => PortalRow.FromModel(layout.Id, portal))));
        Profiles = new ObservableCollection<ProfileRow>(configuration.Profiles.Select(ProfileRow.FromModel));
        Presets = new ObservableCollection<PresetRow>();
        Monitors = new ObservableCollection<MonitorRow>();
        Logs = new ObservableCollection<string>();
        ValidationErrors = new ObservableCollection<string>();

        RebuildPresetRowsFromProfiles();
        SelectedDevice = Devices.FirstOrDefault();
        SelectedLayout = Layouts.FirstOrDefault();
        SelectedProfile = Profiles.FirstOrDefault();

        AddDeviceCommand = new RelayCommand(AddDevice);
        RemoveDeviceCommand = new RelayCommand(RemoveSelectedDevice, () => SelectedDevice is not null);
        GetPresetsCommand = new AsyncRelayCommand(GetPresetsAsync, () => SelectedDevice is not null);
        AddLayoutCommand = new RelayCommand(AddLayout);
        RemoveLayoutCommand = new RelayCommand(RemoveSelectedLayout, () => SelectedLayout is not null);
        AddZoneCommand = new RelayCommand(AddZone, () => SelectedLayout is not null);
        RemoveZoneCommand = new RelayCommand(RemoveSelectedZone, () => SelectedZone is not null);
        AddPortalCommand = new RelayCommand(AddPortal, () => SelectedLayout is not null);
        RemovePortalCommand = new RelayCommand(RemoveSelectedPortal, () => SelectedPortal is not null);
        CreateLayoutFromMonitorsCommand = new RelayCommand(CreateLayoutFromMonitors, () => Monitors.Count > 0);
        AddProfileCommand = new RelayCommand(AddProfile);
        RemoveProfileCommand = new RelayCommand(RemoveSelectedProfile, () => SelectedProfile is not null);
        ApplySelectedPresetToProfileCommand = new RelayCommand(ApplySelectedPresetToProfile, () => SelectedPreset is not null && SelectedProfile is not null);
        ApplySelectedLayoutToProfileCommand = new RelayCommand(ApplySelectedLayoutToProfile, () => SelectedLayout is not null && SelectedProfile is not null);
        ValidateConfigurationCommand = new RelayCommand(ValidateConfiguration);
        SaveConfigurationCommand = new AsyncRelayCommand(SaveConfigurationAsync);
        ExecuteSelectedProfileCommand = new AsyncRelayCommand(ExecuteSelectedProfileAsync, () => SelectedProfile is not null);
        EmergencyUnlockCommand = new RelayCommand(EmergencyUnlock);
        StopRoutingCommand = new RelayCommand(() => StopRouting(clearLayout: true));
        RefreshDiagnosticsCommand = new RelayCommand(RefreshDiagnostics);

        _routingRuntime.Log += (_, message) => Dispatch(() => AddLog(message));
        RefreshDiagnostics();
        ValidateConfiguration();
        AddLog($"Application started with routing disabled. Config path: {_configPath}");
    }

    public event EventHandler? HotkeysChanged;

    public ObservableCollection<DeviceRow> Devices { get; }
    public ObservableCollection<PresetRow> Presets { get; }
    public ObservableCollection<LayoutRow> Layouts { get; }
    public ObservableCollection<ZoneRow> Zones { get; }
    public ObservableCollection<PortalRow> Portals { get; }
    public ObservableCollection<ProfileRow> Profiles { get; }
    public ObservableCollection<MonitorRow> Monitors { get; }
    public ObservableCollection<string> Logs { get; }
    public ObservableCollection<string> ValidationErrors { get; }

    public ICommand AddDeviceCommand { get; }
    public ICommand RemoveDeviceCommand { get; }
    public ICommand GetPresetsCommand { get; }
    public ICommand AddLayoutCommand { get; }
    public ICommand RemoveLayoutCommand { get; }
    public ICommand AddZoneCommand { get; }
    public ICommand RemoveZoneCommand { get; }
    public ICommand AddPortalCommand { get; }
    public ICommand RemovePortalCommand { get; }
    public ICommand CreateLayoutFromMonitorsCommand { get; }
    public ICommand AddProfileCommand { get; }
    public ICommand RemoveProfileCommand { get; }
    public ICommand ApplySelectedPresetToProfileCommand { get; }
    public ICommand ApplySelectedLayoutToProfileCommand { get; }
    public ICommand ValidateConfigurationCommand { get; }
    public ICommand SaveConfigurationCommand { get; }
    public ICommand ExecuteSelectedProfileCommand { get; }
    public ICommand EmergencyUnlockCommand { get; }
    public ICommand StopRoutingCommand { get; }
    public ICommand RefreshDiagnosticsCommand { get; }

    public DeviceRow? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public PresetRow? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public LayoutRow? SelectedLayout
    {
        get => _selectedLayout;
        set
        {
            if (SetProperty(ref _selectedLayout, value))
            {
                RefreshCursorZone();
                RaiseCommandStates();
            }
        }
    }

    public ZoneRow? SelectedZone
    {
        get => _selectedZone;
        set
        {
            if (SetProperty(ref _selectedZone, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public PortalRow? SelectedPortal
    {
        get => _selectedPortal;
        set
        {
            if (SetProperty(ref _selectedPortal, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public ProfileRow? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                ActiveProfileName = value?.Name ?? "";
                RaiseCommandStates();
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

        var configuration = BuildConfiguration();
        var validation = _configurationValidator.Validate(configuration);
        ShowValidation(validation);
        if (!validation.IsValid)
        {
            AddLog("Configuration validation failed. Fix validation messages before executing.");
            return;
        }

        var profile = SelectedProfile.ToModel();
        ActiveProfileName = profile.Name;
        AddLog($"Executing profile '{profile.Name}'.");
        StopRouting(clearLayout: profile.CursorLayoutId is not null);

        bool? h2AckOk = null;
        if (profile.H2Preset is not null)
        {
            var device = configuration.Devices.FirstOrDefault(device =>
                string.Equals(device.Id, profile.H2Preset.DeviceId, StringComparison.OrdinalIgnoreCase));
            if (device is null)
            {
                AddLog($"Profile references missing device '{profile.H2Preset.DeviceId}'.");
                return;
            }

            AddLog($"Sending W0605 to {device.Host}:{device.Port} screenId={profile.H2Preset.ScreenId} presetId={profile.H2Preset.PresetId}.");
            var result = await _h2DeviceClient.LoadPresetAsync(device, profile.H2Preset.ScreenId, profile.H2Preset.PresetId);
            h2AckOk = result.IsSuccess;
            AddLog(result.IsSuccess ? "H2 preset load acknowledged." : $"H2 preset load failed: {result.Message}");
            if (!string.IsNullOrWhiteSpace(result.ResponseJson))
            {
                AddLog($"H2 response: {result.ResponseJson}");
            }
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
            var layout = configuration.CursorLayouts.FirstOrDefault(layout =>
                string.Equals(layout.Id, profile.CursorLayoutId, StringComparison.OrdinalIgnoreCase));
            if (layout is null)
            {
                AddLog($"Profile references missing cursor layout '{profile.CursorLayoutId}'.");
                return;
            }

            var startPosition = _routingEngine.ResolveStartPosition(layout, profile.StartPosition);
            _routingRuntime.ActivateLayout(layout, startPosition, TimeSpan.FromMilliseconds(15));
            SelectedLayout = Layouts.FirstOrDefault(row => string.Equals(row.Id, layout.Id, StringComparison.OrdinalIgnoreCase));
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

        var device = SelectedDevice.ToModel();
        AddLog($"Sending R0600 to {device.Host}:{device.Port}.");
        var result = await _h2DeviceClient.GetPresetEnumAsync(device);
        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.ResponseJson))
        {
            AddLog($"Preset enum request failed: {result.Message}");
            return;
        }

        try
        {
            var parsed = _presetEnumParser.Parse(result.ResponseJson);
            var removed = Presets.Where(row => row.Source == "H2 Query").ToArray();
            foreach (var row in removed)
            {
                Presets.Remove(row);
            }

            foreach (var preset in parsed)
            {
                Presets.Add(new PresetRow
                {
                    DeviceConfigId = SelectedDevice.Id,
                    H2DeviceId = preset.DeviceId,
                    ScreenId = preset.ScreenId,
                    FriendlyPresetNumber = preset.FriendlyPresetNumber,
                    PresetId = preset.PresetId,
                    DisplayName = preset.DisplayName,
                    Source = "H2 Query"
                });
            }

            AddLog($"Loaded {parsed.Count} presets from H2.");
        }
        catch (Exception exception)
        {
            AddLog($"Preset enum response could not be parsed: {exception.Message}");
            AddLog($"Raw preset enum response: {result.ResponseJson}");
        }
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

    private void AddDevice()
    {
        var index = Devices.Count + 1;
        var row = new DeviceRow
        {
            Id = index == 1 ? "h2-main" : $"h2-{index}",
            Name = index == 1 ? "Main H2" : $"H2 {index}",
            Host = "192.168.0.100",
            Port = H2DeviceConfig.DefaultPort,
            TimeoutMs = 1000
        };
        Devices.Add(row);
        SelectedDevice = row;
    }

    private void RemoveSelectedDevice()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        Devices.Remove(SelectedDevice);
        SelectedDevice = Devices.FirstOrDefault();
    }

    private void AddLayout()
    {
        var index = Layouts.Count + 1;
        var row = new LayoutRow { Id = $"layout-{index}", Name = $"Layout {index}" };
        Layouts.Add(row);
        SelectedLayout = row;
    }

    private void RemoveSelectedLayout()
    {
        if (SelectedLayout is null)
        {
            return;
        }

        var layoutId = SelectedLayout.Id;
        foreach (var zone in Zones.Where(zone => zone.LayoutId == layoutId).ToArray())
        {
            Zones.Remove(zone);
        }

        foreach (var portal in Portals.Where(portal => portal.LayoutId == layoutId).ToArray())
        {
            Portals.Remove(portal);
        }

        Layouts.Remove(SelectedLayout);
        SelectedLayout = Layouts.FirstOrDefault();
    }

    private void AddZone()
    {
        if (SelectedLayout is null)
        {
            return;
        }

        var index = Zones.Count(zone => zone.LayoutId == SelectedLayout.Id) + 1;
        var row = new ZoneRow
        {
            LayoutId = SelectedLayout.Id,
            Id = $"ZONE_{index}",
            DisplayName = $"Zone {index}",
            WindowsRight = 1920,
            WindowsBottom = 1080,
            VisualRight = 1920,
            VisualBottom = 1080,
            IsVisible = true
        };
        Zones.Add(row);
        SelectedZone = row;
    }

    private void RemoveSelectedZone()
    {
        if (SelectedZone is null)
        {
            return;
        }

        Zones.Remove(SelectedZone);
        SelectedZone = Zones.FirstOrDefault(zone => zone.LayoutId == SelectedLayout?.Id);
    }

    private void AddPortal()
    {
        if (SelectedLayout is null)
        {
            return;
        }

        var zones = Zones.Where(zone => zone.LayoutId == SelectedLayout.Id).ToArray();
        var row = new PortalRow
        {
            LayoutId = SelectedLayout.Id,
            FromZoneId = zones.ElementAtOrDefault(0)?.Id ?? "",
            ToZoneId = zones.ElementAtOrDefault(1)?.Id ?? zones.ElementAtOrDefault(0)?.Id ?? ""
        };
        Portals.Add(row);
        SelectedPortal = row;
    }

    private void RemoveSelectedPortal()
    {
        if (SelectedPortal is null)
        {
            return;
        }

        Portals.Remove(SelectedPortal);
        SelectedPortal = Portals.FirstOrDefault(portal => portal.LayoutId == SelectedLayout?.Id);
    }

    private void CreateLayoutFromMonitors()
    {
        var layout = new LayoutRow
        {
            Id = $"layout-detected-{DateTime.Now:HHmmss}",
            Name = "Detected Windows Monitors"
        };
        Layouts.Add(layout);
        SelectedLayout = layout;

        var ordered = Monitors.OrderBy(monitor => monitor.Left).ThenBy(monitor => monitor.Top).ToArray();
        foreach (var monitor in ordered)
        {
            var zone = new ZoneRow
            {
                LayoutId = layout.Id,
                Id = NormalizeZoneId(monitor.DeviceName),
                DisplayName = monitor.DeviceName,
                WindowsLeft = monitor.Left,
                WindowsTop = monitor.Top,
                WindowsRight = monitor.Right,
                WindowsBottom = monitor.Bottom,
                VisualLeft = monitor.Left,
                VisualTop = monitor.Top,
                VisualRight = monitor.Right,
                VisualBottom = monitor.Bottom,
                IsVisible = true
            };
            Zones.Add(zone);
        }

        AddLog("Created an editable layout from detected Windows monitors. Adjust visible flags and visual rectangles for the active H2 layout.");
    }

    private void AddProfile()
    {
        var index = Profiles.Count + 1;
        var row = new ProfileRow
        {
            Id = $"profile-{index}",
            Name = $"Profile {index}",
            Hotkey = $"Ctrl+Alt+{Math.Min(index, 9)}",
            CursorLayoutId = SelectedLayout?.Id,
            PostAckDelayMs = 500,
            RequireH2AckBeforeCursorLayout = true
        };
        Profiles.Add(row);
        SelectedProfile = row;
    }

    private void RemoveSelectedProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        Profiles.Remove(SelectedProfile);
        SelectedProfile = Profiles.FirstOrDefault();
    }

    private void ApplySelectedPresetToProfile()
    {
        if (SelectedPreset is null || SelectedProfile is null)
        {
            return;
        }

        SelectedProfile.DeviceId = SelectedPreset.DeviceConfigId;
        SelectedProfile.ScreenId = SelectedPreset.ScreenId;
        SelectedProfile.PresetId = SelectedPreset.PresetId;
        SelectedProfile.PresetDisplayName = SelectedPreset.DisplayName;
        OnPropertyChanged(nameof(SelectedProfile));
        AddLog($"Mapped preset '{SelectedPreset.DisplayName}' to profile '{SelectedProfile.Name}'.");
    }

    private void ApplySelectedLayoutToProfile()
    {
        if (SelectedLayout is null || SelectedProfile is null)
        {
            return;
        }

        SelectedProfile.CursorLayoutId = SelectedLayout.Id;
        OnPropertyChanged(nameof(SelectedProfile));
        AddLog($"Mapped layout '{SelectedLayout.Name}' to profile '{SelectedProfile.Name}'.");
    }

    private void ValidateConfiguration()
    {
        var validation = _configurationValidator.Validate(BuildConfiguration());
        ShowValidation(validation);
        AddLog(validation.IsValid
            ? "Configuration validation passed."
            : $"Configuration validation failed with {validation.Errors.Count} issue(s).");
    }

    private async Task SaveConfigurationAsync()
    {
        var configuration = BuildConfiguration();
        var validation = _configurationValidator.Validate(configuration);
        ShowValidation(validation);
        if (!validation.IsValid)
        {
            AddLog("Configuration was not saved because validation failed.");
            return;
        }

        await _configFileService.SaveAsync(configuration, _configPath);
        RebuildPresetRowsFromProfiles();
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
        AddLog($"Saved configuration to {_configPath}. Hotkeys were refreshed.");
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
            Monitors.Add(MonitorRow.FromModel(monitor));
        }

        var position = _cursorService.GetPosition();
        CurrentCursorPosition = $"{position.X}, {position.Y}";
        RefreshCursorZone();
        RaiseCommandStates();
    }

    private void RefreshCursorZone()
    {
        if (SelectedLayout is null)
        {
            CurrentCursorZone = "";
            return;
        }

        var layout = BuildLayout(SelectedLayout);
        var position = _cursorService.GetPosition();
        CurrentCursorPosition = $"{position.X}, {position.Y}";
        var zone = _routingEngine.FindZone(layout, position);
        CurrentCursorZone = zone is null ? "Outside known zones" : $"{zone.Id} ({(zone.IsVisible ? "visible" : "hidden")})";
    }

    private void RebuildPresetRowsFromProfiles()
    {
        foreach (var row in Presets.Where(row => row.Source == "Profile").ToArray())
        {
            Presets.Remove(row);
        }

        foreach (var profile in Profiles.Where(profile => !string.IsNullOrWhiteSpace(profile.DeviceId) && profile.ScreenId is not null && profile.PresetId is not null))
        {
            Presets.Add(new PresetRow
            {
                DeviceConfigId = profile.DeviceId!,
                ScreenId = profile.ScreenId!.Value,
                PresetId = profile.PresetId!.Value,
                FriendlyPresetNumber = profile.PresetId.Value + 1,
                DisplayName = profile.PresetDisplayName ?? $"Preset {profile.PresetId.Value + 1} / presetId {profile.PresetId.Value}",
                Source = "Profile"
            });
        }
    }

    private AppConfiguration BuildConfiguration() => new(
        Devices.Select(device => device.ToModel()).ToArray(),
        Layouts.Select(BuildLayout).ToArray(),
        Profiles.Select(profile => profile.ToModel()).ToArray(),
        SafetySettings.Default);

    private CursorLayout BuildLayout(LayoutRow layout)
    {
        var start = layout.DefaultStartX is not null && layout.DefaultStartY is not null
            ? new CursorPoint(layout.DefaultStartX.Value, layout.DefaultStartY.Value)
            : (CursorPoint?)null;

        return new CursorLayout(
            layout.Id,
            layout.Name,
            Zones.Where(zone => string.Equals(zone.LayoutId, layout.Id, StringComparison.OrdinalIgnoreCase))
                .Select(zone => zone.ToModel())
                .ToArray(),
            Portals.Where(portal => string.Equals(portal.LayoutId, layout.Id, StringComparison.OrdinalIgnoreCase))
                .Select(portal => portal.ToModel())
                .ToArray(),
            start);
    }

    private void ShowValidation(ValidationResult validation)
    {
        ValidationErrors.Clear();
        foreach (var error in validation.Errors)
        {
            ValidationErrors.Add(error);
        }

        if (validation.IsValid)
        {
            ValidationErrors.Add("Configuration is valid.");
        }
    }

    private void RefreshRuntimeState()
    {
        OnPropertyChanged(nameof(ActiveLayoutId));
        OnPropertyChanged(nameof(IsRoutingEnabled));
    }

    private void RaiseCommandStates()
    {
        foreach (var command in new ICommand[]
        {
            RemoveDeviceCommand,
            GetPresetsCommand,
            RemoveLayoutCommand,
            AddZoneCommand,
            RemoveZoneCommand,
            AddPortalCommand,
            RemovePortalCommand,
            CreateLayoutFromMonitorsCommand,
            RemoveProfileCommand,
            ApplySelectedPresetToProfileCommand,
            ApplySelectedLayoutToProfileCommand,
            ExecuteSelectedProfileCommand
        })
        {
            switch (command)
            {
                case RelayCommand relay:
                    relay.RaiseCanExecuteChanged();
                    break;
                case AsyncRelayCommand async:
                    async.RaiseCanExecuteChanged();
                    break;
            }
        }
    }

    private static string NormalizeZoneId(string text)
    {
        var chars = text.Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 ? "MONITOR" : new string(chars).ToUpperInvariant();
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
