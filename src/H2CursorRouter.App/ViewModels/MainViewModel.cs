using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using H2CursorRouter.App;
using H2CursorRouter.App.Services;
using H2CursorRouter.Core.Configuration;
using H2CursorRouter.Core.Domain;
using H2CursorRouter.Core.Geometry;
using H2CursorRouter.Core.Validation;
using H2CursorRouter.H2;
using H2CursorRouter.Windows;

namespace H2CursorRouter.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly string _configPath;
    private readonly string _executablePath;
    private readonly ConfigFileService _configFileService = new();
    private readonly LayoutEditingService _layoutEditingService = new();
    private readonly IStartupRegistrationService _startupRegistrationService;
    private readonly FileLogService _fileLogService;
    private readonly IH2DeviceClient _h2DeviceClient;
    private readonly IDisplayIdentificationService _displayIdentificationService;
    private readonly ITextInputDialogService _textInputDialogService;
    private readonly IConfirmationDialogService _confirmationDialogService;
    private readonly IProfileDialogService _profileDialogService;
    private readonly IDeviceDialogService _deviceDialogService;
    private readonly ICursorService _cursorService;
    private readonly IMonitorTopologyService _monitorTopologyService;
    private readonly CursorRoutingRuntime _routingRuntime;
    private readonly CursorRoutingEngine _routingEngine;
    private readonly AppConfigurationValidator _configurationValidator;
    private readonly ProfileExecutionService _profileExecutionService;
    private readonly H2PresetEnumParser _presetEnumParser = new();
    private readonly SemaphoreSlim _profileExecutionLock = new(1, 1);
    private readonly SemaphoreSlim _configurationSaveLock = new(1, 1);
    private DeviceRow? _selectedDevice;
    private PresetRow? _selectedPreset;
    private LayoutRow? _selectedLayout;
    private ZoneRow? _selectedZone;
    private MonitorRow? _selectedAvailableMonitor;
    private PortalRow? _selectedPortal;
    private ProfileRow? _selectedProfile;
    private string _runtimeStatus = "Routing disabled on startup.";
    private string _currentCursorPosition = "";
    private string _currentCursorZone = "";
    private string _activeProfileName = "";
    private string _lastH2AckStatus = "No H2 command sent yet.";
    private string _h2ConnectionStatus = "Not checked yet.";
    private string _lastRoutingEvent = "Routing disabled on startup.";
    private string _profileFilter = "";
    private bool _startWithWindows;
    private bool _isCheckingH2Connection;
    private double _layoutPreviewScale = 0.16;
    private double _displayPreviewCanvasWidth = 640;
    private double _displayPreviewCanvasHeight = 320;
    private CursorPoint? _selectedLayoutDraftStartPosition;

    public MainViewModel(
        AppConfiguration configuration,
        string configPath,
        string executablePath,
        IStartupRegistrationService startupRegistrationService,
        FileLogService fileLogService,
        IH2DeviceClient h2DeviceClient,
        IDisplayIdentificationService displayIdentificationService,
        ITextInputDialogService textInputDialogService,
        IConfirmationDialogService confirmationDialogService,
        IProfileDialogService profileDialogService,
        IDeviceDialogService deviceDialogService,
        ICursorService cursorService,
        IMonitorTopologyService monitorTopologyService,
        CursorRoutingRuntime routingRuntime,
        CursorRoutingEngine routingEngine,
        AppConfigurationValidator configurationValidator)
    {
        _configPath = configPath;
        _executablePath = executablePath;
        _startupRegistrationService = startupRegistrationService;
        _fileLogService = fileLogService;
        _h2DeviceClient = h2DeviceClient;
        _displayIdentificationService = displayIdentificationService;
        _textInputDialogService = textInputDialogService;
        _confirmationDialogService = confirmationDialogService;
        _profileDialogService = profileDialogService;
        _deviceDialogService = deviceDialogService;
        _cursorService = cursorService;
        _monitorTopologyService = monitorTopologyService;
        _routingRuntime = routingRuntime;
        _routingEngine = routingEngine;
        _configurationValidator = configurationValidator;
        _profileExecutionService = new ProfileExecutionService(
            _h2DeviceClient,
            _routingRuntime,
            _routingEngine,
            _configurationValidator);

        Devices = new ObservableCollection<DeviceRow>(configuration.Devices.Select(DeviceRow.FromModel));
        Layouts = new ObservableCollection<LayoutRow>(configuration.CursorLayouts.Select(LayoutRow.FromModel));
        Zones = new ObservableCollection<ZoneRow>(configuration.CursorLayouts.SelectMany(layout =>
            layout.Zones.Select(zone => ZoneRow.FromModel(layout.Id, zone))));
        Portals = new ObservableCollection<PortalRow>(configuration.CursorLayouts.SelectMany(layout =>
            layout.Portals.Select(portal => PortalRow.FromModel(layout.Id, portal))));
        Profiles = new ObservableCollection<ProfileRow>(configuration.Profiles.Select(ProfileRow.FromModel));
        DashboardProfiles = new ObservableCollection<ProfileRow>();
        FilteredProfiles = CollectionViewSource.GetDefaultView(Profiles);
        FilteredProfiles.Filter = FilterProfile;
        Presets = new ObservableCollection<PresetRow>(configuration.PresetCache.Select(PresetRow.FromCachedPreset));
        Monitors = new ObservableCollection<MonitorRow>();
        SelectedLayoutZones = new ObservableCollection<ZoneRow>();
        SelectedLayoutPortals = new ObservableCollection<PortalRow>();
        AvailableLayoutDisplays = new ObservableCollection<MonitorRow>();
        Logs = new ObservableCollection<string>();
        ValidationErrors = new ObservableCollection<string>();
        _startWithWindows = _startupRegistrationService.IsRegistered();

        SelectedDevice = Devices.FirstOrDefault();
        SelectedPreset = Presets.FirstOrDefault();
        SelectedLayout = Layouts.FirstOrDefault();
        SelectedProfile = Profiles.FirstOrDefault();

        AddDeviceCommand = new RelayCommand(AddDevice);
        RemoveDeviceCommand = new RelayCommand(RemoveSelectedDevice, () => SelectedDevice is not null);
        GetAllPresetsCommand = new AsyncRelayCommand(GetAllPresetsAsync, () => Devices.Count > 0);
        GetPresetsCommand = new AsyncRelayCommand(GetPresetsAsync, () => SelectedDevice is not null);
        AddLayoutCommand = new RelayCommand(AddLayout);
        RemoveLayoutCommand = new RelayCommand(RemoveSelectedLayout, () => SelectedLayout is not null && IsLayoutPersisted(SelectedLayout));
        DeleteLayoutCommand = new RelayCommand<LayoutRow>(DeleteLayout, IsLayoutPersisted);
        AddZoneCommand = new RelayCommand(AddZone, () => SelectedLayout is not null);
        RemoveZoneCommand = new RelayCommand(RemoveSelectedZone, () => SelectedZone is not null);
        AddDisplayToCanvasCommand = new RelayCommand(AddSelectedDisplayToCanvas, () => SelectedAvailableMonitor is not null);
        AddPortalCommand = new RelayCommand(AddPortal, () => SelectedLayout is not null);
        RemovePortalCommand = new RelayCommand(RemoveSelectedPortal, () => SelectedPortal is not null);
        CreateLayoutFromMonitorsCommand = new RelayCommand(CreateLayoutFromMonitors, () => Monitors.Count > 0);
        ApplyDetectedMonitorCoordinatesCommand = new RelayCommand(ApplyDetectedMonitorCoordinates, () => SelectedLayout is not null && Monitors.Count > 0);
        ApplyCanvasLayoutCommand = new RelayCommand(ApplyCanvasLayout, () => SelectedLayout is not null);
        SaveLayoutAsNewCommand = new RelayCommand(SaveSelectedLayoutAsNew, () => SelectedLayout is not null);
        OverwriteSelectedLayoutCommand = new RelayCommand(OverwriteSelectedLayout, () => SelectedLayout is not null && IsLayoutPersisted(SelectedLayout));
        AddProfileCommand = new RelayCommand(AddProfile);
        RemoveProfileCommand = new RelayCommand(RemoveSelectedProfile, () => SelectedProfile is not null);
        DuplicateProfileCommand = new RelayCommand(DuplicateSelectedProfile, () => SelectedProfile is not null);
        SetCurrentCursorAsStartCommand = new RelayCommand(SetCurrentCursorAsProfileStart, () => SelectedProfile is not null);
        ApplySelectedPresetToProfileCommand = new RelayCommand(ApplySelectedPresetToProfile, () => SelectedPreset is not null && SelectedProfile is not null);
        ApplySelectedLayoutToProfileCommand = new RelayCommand(ApplySelectedLayoutToProfile, () => SelectedLayout is not null && IsLayoutPersisted(SelectedLayout) && SelectedProfile is not null);
        GeneratePortalsCommand = new RelayCommand(GeneratePortalsFromVisualAdjacency, () => SelectedLayout is not null);
        ValidateConfigurationCommand = new RelayCommand(ValidateConfiguration);
        SaveConfigurationCommand = new AsyncRelayCommand(SaveConfigurationAsync);
        ExecuteSelectedProfileCommand = new AsyncRelayCommand(ExecuteSelectedProfileAsync, () => SelectedProfile is not null);
        ExecuteProfileCommand = new AsyncRelayCommand<ProfileRow>(ExecuteProfileFromCommandAsync, profile => profile is not null);
        EmergencyUnlockCommand = new RelayCommand(EmergencyUnlock);
        StopRoutingCommand = new RelayCommand(() => StopRouting(clearLayout: true));
        RefreshDiagnosticsCommand = new RelayCommand(() => RefreshDiagnostics(log: true));
        IdentifyDisplaysCommand = new AsyncRelayCommand(IdentifyDisplaysAsync, () => Monitors.Count > 0);
        ResetToSampleConfigurationCommand = new RelayCommand(ResetToSampleConfiguration);

        _routingRuntime.Log += (_, message) => Dispatch(() => AddLog(message));
        _monitorTopologyService.TopologyChanged += OnMonitorTopologyChanged;
        RefreshDiagnostics(log: true);
        RefreshProfileLayoutNames();
        RefreshDashboardProfiles();
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
    public ObservableCollection<ProfileRow> DashboardProfiles { get; }
    public ICollectionView FilteredProfiles { get; }
    public ObservableCollection<MonitorRow> Monitors { get; }
    public ObservableCollection<ZoneRow> SelectedLayoutZones { get; }
    public ObservableCollection<PortalRow> SelectedLayoutPortals { get; }
    public ObservableCollection<MonitorRow> AvailableLayoutDisplays { get; }
    public ObservableCollection<string> Logs { get; }
    public ObservableCollection<string> ValidationErrors { get; }

    public ICommand AddDeviceCommand { get; }
    public ICommand RemoveDeviceCommand { get; }
    public ICommand GetAllPresetsCommand { get; }
    public ICommand GetPresetsCommand { get; }
    public ICommand AddLayoutCommand { get; }
    public ICommand RemoveLayoutCommand { get; }
    public ICommand DeleteLayoutCommand { get; }
    public ICommand AddZoneCommand { get; }
    public ICommand RemoveZoneCommand { get; }
    public ICommand AddDisplayToCanvasCommand { get; }
    public ICommand AddPortalCommand { get; }
    public ICommand RemovePortalCommand { get; }
    public ICommand CreateLayoutFromMonitorsCommand { get; }
    public ICommand ApplyDetectedMonitorCoordinatesCommand { get; }
    public ICommand ApplyCanvasLayoutCommand { get; }
    public ICommand SaveLayoutAsNewCommand { get; }
    public ICommand OverwriteSelectedLayoutCommand { get; }
    public ICommand AddProfileCommand { get; }
    public ICommand RemoveProfileCommand { get; }
    public ICommand DuplicateProfileCommand { get; }
    public ICommand SetCurrentCursorAsStartCommand { get; }
    public ICommand ApplySelectedPresetToProfileCommand { get; }
    public ICommand ApplySelectedLayoutToProfileCommand { get; }
    public ICommand GeneratePortalsCommand { get; }
    public ICommand ValidateConfigurationCommand { get; }
    public ICommand SaveConfigurationCommand { get; }
    public ICommand ExecuteSelectedProfileCommand { get; }
    public ICommand ExecuteProfileCommand { get; }
    public ICommand EmergencyUnlockCommand { get; }
    public ICommand StopRoutingCommand { get; }
    public ICommand RefreshDiagnosticsCommand { get; }
    public ICommand IdentifyDisplaysCommand { get; }
    public ICommand ResetToSampleConfigurationCommand { get; }

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
                RefreshSelectedLayoutCollections();
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
            if (ReferenceEquals(_selectedZone, value))
            {
                return;
            }

            if (_selectedZone is not null)
            {
                _selectedZone.IsSelected = false;
            }

            if (SetProperty(ref _selectedZone, value))
            {
                if (_selectedZone is not null)
                {
                    _selectedZone.IsSelected = true;
                }

                OnPropertyChanged(nameof(HasSelectedZone));
                RaiseCommandStates();
            }
        }
    }

    public bool HasSelectedZone => SelectedZone is not null;

    public MonitorRow? SelectedAvailableMonitor
    {
        get => _selectedAvailableMonitor;
        set
        {
            if (SetProperty(ref _selectedAvailableMonitor, value))
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

    public double DisplayPreviewCanvasWidth
    {
        get => _displayPreviewCanvasWidth;
        private set => SetProperty(ref _displayPreviewCanvasWidth, value);
    }

    public double DisplayPreviewCanvasHeight
    {
        get => _displayPreviewCanvasHeight;
        private set => SetProperty(ref _displayPreviewCanvasHeight, value);
    }

    public string ActiveProfileName
    {
        get => _activeProfileName;
        private set => SetProperty(ref _activeProfileName, value);
    }

    public string ActiveLayoutId => _routingRuntime.ActiveLayoutId ?? "";
    public string ActiveLayoutName
    {
        get
        {
            var activeLayoutId = _routingRuntime.ActiveLayoutId;
            if (string.IsNullOrWhiteSpace(activeLayoutId))
            {
                return "None";
            }

            return Layouts.FirstOrDefault(layout => string.Equals(layout.Id, activeLayoutId, StringComparison.OrdinalIgnoreCase))?.Name
                   ?? activeLayoutId;
        }
    }

    public bool IsRoutingEnabled => _routingRuntime.IsRoutingEnabled;
    public string RoutingStateText => IsRoutingEnabled ? "Enabled" : "Disabled";
    public string ConfigPath => _configPath;
    public string ArtifactWorkflowHint => "GitHub Actions > Windows Build > vp-cursor-portal-win-x64 artifact";

    public string LastH2AckStatus
    {
        get => _lastH2AckStatus;
        private set => SetProperty(ref _lastH2AckStatus, value);
    }

    public string H2ConnectionStatus
    {
        get => _h2ConnectionStatus;
        private set
        {
            if (SetProperty(ref _h2ConnectionStatus, value))
            {
                OnPropertyChanged(nameof(IsH2Online));
                OnPropertyChanged(nameof(IsOnline));
            }
        }
    }

    public bool IsH2Online => H2ConnectionStatus.StartsWith("Online:", StringComparison.OrdinalIgnoreCase);
    public bool IsOnline => IsH2Online;

    public string LastRoutingEvent
    {
        get => _lastRoutingEvent;
        private set => SetProperty(ref _lastRoutingEvent, value);
    }

    public string ProfileFilter
    {
        get => _profileFilter;
        set
        {
            if (SetProperty(ref _profileFilter, value))
            {
                FilteredProfiles.Refresh();
            }
        }
    }

    public double LayoutPreviewScale
    {
        get => _layoutPreviewScale;
        set => SetProperty(ref _layoutPreviewScale, Math.Clamp(value, 0.08, 0.5));
    }

    public double LayoutPreviewCanvasWidth =>
        Math.Max(3840, SelectedLayoutZones.Count == 0 ? 3840 : SelectedLayoutZones.Max(zone => zone.VisualRight) + LayoutPreviewGridSize * 2);

    public double LayoutPreviewCanvasHeight =>
        Math.Max(2160, SelectedLayoutZones.Count == 0 ? 2160 : SelectedLayoutZones.Max(zone => zone.VisualBottom) + LayoutPreviewGridSize * 2);

    public double LayoutPreviewGridSize => LayoutEditingService.GridSize;

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (SetProperty(ref _startWithWindows, value))
            {
                try
                {
                    _startupRegistrationService.SetRegistered(value, _executablePath, "--tray");
                    AddLog(value
                        ? "Registered app to start with Windows in tray mode."
                        : "Removed app from Windows startup.");
                }
                catch (Exception exception)
                {
                    AddLog($"Failed to update Windows startup registration: {exception.Message}");
                    _startWithWindows = _startupRegistrationService.IsRegistered();
                    OnPropertyChanged();
                }
            }
        }
    }

    public async Task ExecuteProfileByIndexAsync(int index)
    {
        if (index < 0 || index >= Profiles.Count)
        {
            return;
        }

        var profile = Profiles[index];
        SelectedProfile = profile;
        await ExecuteProfileAsync(profile);
    }

    public async Task ExecuteSelectedProfileAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        await ExecuteProfileAsync(SelectedProfile);
    }

    public async Task RefreshDashboardStatusAsync()
    {
        RefreshDiagnostics(log: false);
        await RefreshH2ConnectionStatusAsync();
    }

    public void RefreshDisplays() => RefreshDiagnostics(log: true);

    private void OnMonitorTopologyChanged(object? sender, EventArgs e) =>
        Dispatch(() =>
        {
            RefreshDiagnostics(log: false);
            AddLog("Display topology changed. Display list refreshed.");
        });

    private async Task ExecuteProfileFromCommandAsync(ProfileRow? profile)
    {
        if (profile is null)
        {
            return;
        }

        SelectedProfile = profile;
        await ExecuteProfileAsync(profile);
    }

    private async Task ExecuteProfileAsync(ProfileRow profileRow)
    {
        await _profileExecutionLock.WaitAsync();
        try
        {
            var configuration = BuildConfiguration();
            var profile = profileRow.ToModel();
            await _profileExecutionService.ExecuteAsync(
                new ProfileExecutionRequest(configuration, profile),
                new ProfileExecutionCallbacks
                {
                    ShowValidation = ShowValidation,
                    SetActiveProfileName = value => ActiveProfileName = value,
                    SetLastH2AckStatus = value => LastH2AckStatus = value,
                    SetH2ConnectionStatus = value => H2ConnectionStatus = value,
                    SetDeviceOnline = SetDeviceOnline,
                    SelectLayout = SelectLayout,
                    StopRouting = StopRouting,
                    AddLog = AddLog,
                    RefreshRuntimeState = RefreshRuntimeState
                });
        }
        finally
        {
            _profileExecutionLock.Release();
        }
    }

    public async Task GetPresetsAsync()
    {
        if (SelectedDevice is null)
        {
            AddLog("No H2 device selected.");
            return;
        }

        if (await GetPresetsForDeviceAsync(SelectedDevice))
        {
            foreach (var row in Presets
                         .Where(row => !string.Equals(row.DeviceConfigId, SelectedDevice.Id, StringComparison.OrdinalIgnoreCase))
                         .ToArray())
            {
                Presets.Remove(row);
            }

            RefreshSelectedPreset(SelectedDevice.Id);
            AutoSaveConfiguration("Auto-saved configuration after refreshing presets.");
        }
    }

    public async Task GetAllPresetsAsync()
    {
        if (Devices.Count == 0)
        {
            AddLog("No H2 devices configured.");
            return;
        }

        var loadedAny = false;
        foreach (var deviceRow in Devices.ToArray())
        {
            loadedAny |= await GetPresetsForDeviceAsync(deviceRow);
        }

        if (loadedAny)
        {
            RefreshSelectedPreset(SelectedDevice?.Id);
            AutoSaveConfiguration("Auto-saved configuration after refreshing all presets.");
        }
    }

    private async Task<bool> GetPresetsForDeviceAsync(DeviceRow deviceRow)
    {
        var device = deviceRow.ToModel();
        AddLog($"Sending R0600 to {device.Host}:{device.Port}.");
        var result = await _h2DeviceClient.GetPresetEnumAsync(device, device.DeviceId, deviceRow.PresetEnumScreenId);

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.ResponseJson))
        {
            deviceRow.IsOnline = false;
            H2ConnectionStatus = $"No response: {result.Message}";
            AddLog($"Preset enum request failed for H2 device '{deviceRow.Name}': {result.Message}");
            return false;
        }

        try
        {
            var parsed = _presetEnumParser.Parse(result.ResponseJson);
            foreach (var row in Presets.Where(row => string.Equals(row.DeviceConfigId, deviceRow.Id, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                Presets.Remove(row);
            }

            var fetchedAt = DateTimeOffset.UtcNow;
            foreach (var preset in parsed)
            {
                Presets.Add(new PresetRow
                {
                    DeviceConfigId = deviceRow.Id,
                    DeviceName = deviceRow.Name,
                    H2DeviceId = preset.DeviceId,
                    ScreenId = preset.ScreenId,
                    FriendlyPresetNumber = preset.FriendlyPresetNumber,
                    PresetId = preset.PresetId,
                    DisplayName = string.IsNullOrWhiteSpace(preset.Name) ? "(unnamed)" : preset.Name,
                    LastFetchedAtUtc = fetchedAt
                });
            }

            deviceRow.IsOnline = true;
            H2ConnectionStatus = $"Online: {device.Host}:{device.Port}";
            AddLog($"Loaded {parsed.Count} presets from H2 device '{deviceRow.Name}'.");
            return true;
        }
        catch (Exception exception)
        {
            deviceRow.IsOnline = false;
            H2ConnectionStatus = $"Unexpected response: {exception.Message}";
            AddLog($"Preset enum response from H2 device '{deviceRow.Name}' could not be parsed: {exception.Message}");
            AddLog($"Raw preset enum response: {result.ResponseJson}");
            return false;
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
        _fileLogService.Append(message);
        RuntimeStatus = message;
        if (IsRoutingEvent(message))
        {
            LastRoutingEvent = message;
        }

        RefreshRuntimeState();
    }

    private void AddDevice()
    {
        var result = _deviceDialogService.Prompt(
            "",
            "192.168.0.11",
            H2DeviceConfig.DefaultPort);
        if (result is null)
        {
            return;
        }

        var row = new DeviceRow
        {
            Id = CreateUniqueDeviceId(result.Name),
            Name = result.Name,
            Host = result.Host,
            Port = result.Port,
            DeviceId = 0,
            TimeoutMs = 1000
        };
        Devices.Add(row);
        SelectedDevice = row;
        AddLog($"Added H2 device '{row.Name}' at {row.Host}:{row.Port}.");
        AutoSaveConfiguration("Auto-saved configuration after adding device.");
    }

    private async Task RefreshH2ConnectionStatusAsync()
    {
        if (_isCheckingH2Connection)
        {
            return;
        }

        var selectedDevice = SelectedDevice ?? Devices.FirstOrDefault();
        if (selectedDevice is null)
        {
            H2ConnectionStatus = "No H2 device configured.";
            return;
        }

        _isCheckingH2Connection = true;
        try
        {
            var device = selectedDevice.ToModel();
            H2ConnectionStatus = $"Checking {device.Host}:{device.Port}...";
            var result = await _h2DeviceClient.GetPresetEnumAsync(device, device.DeviceId, selectedDevice.PresetEnumScreenId);
            selectedDevice.IsOnline = result.IsSuccess;
            H2ConnectionStatus = result.IsSuccess
                ? $"Online: {device.Host}:{device.Port}"
                : $"No response: {result.Message}";
        }
        catch (Exception exception)
        {
            selectedDevice.IsOnline = false;
            H2ConnectionStatus = $"Check failed: {exception.Message}";
        }
        finally
        {
            _isCheckingH2Connection = false;
        }
    }

    private void RemoveSelectedDevice()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        var removedDeviceId = SelectedDevice.Id;
        Devices.Remove(SelectedDevice);
        foreach (var row in Presets.Where(row => string.Equals(row.DeviceConfigId, removedDeviceId, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            Presets.Remove(row);
        }

        SelectedDevice = Devices.FirstOrDefault();
        AddLog("Removed H2 device.");
        AutoSaveConfiguration("Auto-saved configuration after removing device.");
    }

    private void AddLayout()
    {
        var index = Layouts.Count + 1;
        var row = new LayoutRow { Id = $"layout-{index}", Name = $"Layout {index}" };
        row.Displays = "No displays";
        Layouts.Add(row);
        SelectedLayout = row;
    }

    private void RemoveSelectedLayout()
    {
        if (SelectedLayout is null)
        {
            return;
        }

        DeleteLayout(SelectedLayout);
    }

    private void DeleteLayout(LayoutRow layout)
    {
        if (!IsLayoutPersisted(layout))
        {
            return;
        }

        var affectedProfiles = Profiles
            .Where(profile => string.Equals(profile.CursorLayoutId, layout.Id, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var h2BackedProfiles = affectedProfiles.Count(ProfileHasH2Preset);
        var layoutOnlyProfiles = affectedProfiles.Length - h2BackedProfiles;
        var profileMessage = affectedProfiles.Length == 0
            ? ""
            : $"{Environment.NewLine}{Environment.NewLine}Profiles using this layout: {affectedProfiles.Length}. H2 preset profiles will keep only the preset. Layout-only profiles removed: {layoutOnlyProfiles}.";

        if (!_confirmationDialogService.Confirm(
                "Delete layout",
                $"Delete layout '{layout.Name}'? This cannot be undone.{profileMessage}"))
        {
            return;
        }

        DeleteLayoutWithoutConfirmation(layout);
    }

    private void DeleteLayoutWithoutConfirmation(LayoutRow layout)
    {
        var layoutId = layout.Id;
        if (string.Equals(ActiveLayoutId, layoutId, StringComparison.OrdinalIgnoreCase))
        {
            StopRouting(clearLayout: true);
        }

        foreach (var zone in Zones.Where(zone => zone.LayoutId == layoutId).ToArray())
        {
            Zones.Remove(zone);
        }

        foreach (var portal in Portals.Where(portal => portal.LayoutId == layoutId).ToArray())
        {
            Portals.Remove(portal);
        }

        foreach (var profile in Profiles.Where(profile => string.Equals(profile.CursorLayoutId, layoutId, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            if (ProfileHasH2Preset(profile))
            {
                profile.CursorLayoutId = null;
                profile.StartX = null;
                profile.StartY = null;
            }
            else
            {
                Profiles.Remove(profile);
            }
        }

        Layouts.Remove(layout);
        SelectedLayout = Layouts.FirstOrDefault();
        FilteredProfiles.Refresh();
        RefreshProfileLayoutNames();
        RefreshDashboardProfiles();
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
        AddLog($"Deleted layout '{layout.Name}'.");
        AutoSaveConfiguration("Auto-saved configuration after deleting layout.");
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
        SelectedLayoutZones.Add(row);
        SelectedZone = row;
        NormalizeSelectedLayoutVisualOrigin();
        RefreshLayoutPreviewCanvasSize();
    }

    private void RemoveSelectedZone()
    {
        if (SelectedZone is null)
        {
            return;
        }

        foreach (var portal in SelectedLayoutPortals.Where(portal =>
                     portal.LayoutId == SelectedZone.LayoutId &&
                     (string.Equals(portal.FromZoneId, SelectedZone.Id, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(portal.ToZoneId, SelectedZone.Id, StringComparison.OrdinalIgnoreCase))).ToArray())
        {
            SelectedLayoutPortals.Remove(portal);
        }

        SelectedLayoutZones.Remove(SelectedZone);
        SelectedZone = SelectedLayoutZones.FirstOrDefault();
        RefreshAvailableLayoutDisplays();
        RefreshLayoutPreviewCanvasSize();
    }

    private void AddSelectedDisplayToCanvas()
    {
        if (SelectedAvailableMonitor is null)
        {
            return;
        }

        EnsureTemporaryLayoutCanvas();
        if (SelectedLayout is null)
        {
            return;
        }

        var zone = CreateZoneFromMonitor(SelectedLayout.Id, SelectedAvailableMonitor);
        SelectedLayoutZones.Add(zone);
        SelectedZone = zone;
        SelectedAvailableMonitor = null;
        NormalizeSelectedLayoutVisualOrigin();
        RefreshAvailableLayoutDisplays();
        RefreshLayoutPreviewCanvasSize();
        AddLog($"Added display '{zone.DisplayName}' to layout canvas.");
    }

    private void EnsureTemporaryLayoutCanvas()
    {
        if (SelectedLayout is not null)
        {
            return;
        }

        SelectedLayout = new LayoutRow
        {
            Id = $"layout-draft-{DateTime.Now:HHmmss}",
            Name = GetNextLayoutName(),
            Displays = "No displays"
        };
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
        SelectedLayoutPortals.Add(row);
        SelectedPortal = row;
    }

    private void RemoveSelectedPortal()
    {
        if (SelectedPortal is null)
        {
            return;
        }

        SelectedLayoutPortals.Remove(SelectedPortal);
        SelectedPortal = SelectedLayoutPortals.FirstOrDefault();
    }

    private void CreateLayoutFromMonitors()
    {
        var layout = new LayoutRow
        {
            Id = $"layout-detected-{DateTime.Now:HHmmss}",
            Name = GetNextLayoutName(),
            Displays = "No displays"
        };
        SelectedLayout = layout;

        var ordered = Monitors.OrderBy(monitor => monitor.Left).ThenBy(monitor => monitor.Top).ToArray();
        foreach (var monitor in ordered)
        {
            var zone = CreateZoneFromMonitor(layout.Id, monitor);
            SelectedLayoutZones.Add(zone);
        }

        NormalizeSelectedLayoutVisualOrigin();
        SelectedZone = SelectedLayoutZones.FirstOrDefault();
        RefreshAvailableLayoutDisplays();
        RefreshLayoutPreviewCanvasSize();
        AddLog("Loaded detected Windows displays into a temporary layout canvas. Use Save As New Layout to add it to the layout list.");
    }

    private void ApplyDetectedMonitorCoordinates()
    {
        if (SelectedLayout is null || Monitors.Count == 0)
        {
            return;
        }

        var orderedMonitors = Monitors.OrderBy(monitor => monitor.Left).ThenBy(monitor => monitor.Top).ToArray();
        var changed = 0;
        foreach (var zone in SelectedLayoutZones)
        {
            var monitor = FindMonitorForZone(zone, orderedMonitors);
            if (monitor is null)
            {
                continue;
            }

            zone.WindowsLeft = monitor.Left;
            zone.WindowsTop = monitor.Top;
            zone.WindowsRight = monitor.Right;
            zone.WindowsBottom = monitor.Bottom;
            zone.DisplayName = monitor.DeviceName;
            changed++;
        }

        CollectionViewSource.GetDefaultView(SelectedLayoutZones)?.Refresh();
        RefreshCursorZone();
        AddLog($"Applied detected Windows monitor coordinates to {changed} zone(s) in layout '{SelectedLayout.Name}'.");
    }

    private void ApplyCanvasLayout()
    {
        if (PrepareSelectedLayoutDraft())
        {
            AddLog($"Prepared layout '{SelectedLayout?.Name}': attached zones, normalized origin, generated portals, and updated the draft start position.");
        }
    }

    private bool PrepareSelectedLayoutDraft()
    {
        if (SelectedLayout is null)
        {
            return false;
        }

        RefreshSelectedLayoutWindowsCoordinatesFromDetectedDisplays();
        AttachAllDraftZonesToNearest();
        var visibleZones = SelectedLayoutZones.Where(zone => zone.IsVisible).ToArray();
        if (visibleZones.Length == 0)
        {
            AddLog("Cannot apply canvas layout because no visible zones are selected.");
            return false;
        }

        NormalizeSelectedLayoutVisualOrigin();

        var firstVisible = SelectedLayoutZones
            .Where(zone => zone.IsVisible)
            .OrderBy(zone => zone.VisualTop)
            .ThenBy(zone => zone.VisualLeft)
            .First();
        _selectedLayoutDraftStartPosition = new CursorPoint(
            firstVisible.WindowsLeft + (firstVisible.WindowsRight - firstVisible.WindowsLeft) / 2,
            firstVisible.WindowsTop + (firstVisible.WindowsBottom - firstVisible.WindowsTop) / 2);
        GeneratePortalsFromVisualAdjacency();
        RefreshLayoutPreviewCanvasSize();
        return true;
    }

    private void SaveSelectedLayoutAsNew()
    {
        if (SelectedLayout is null)
        {
            return;
        }

        var name = _textInputDialogService.Prompt(
            "Save layout",
            "Enter a name for the new layout.",
            GetNextLayoutName());
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (!PrepareSelectedLayoutDraft())
        {
            return;
        }

        var newId = CreateUniqueLayoutId(name);
        var newLayout = new LayoutRow
        {
            Id = newId,
            Name = name.Trim(),
            Description = SelectedLayout.Description,
            DefaultStartX = _selectedLayoutDraftStartPosition?.X,
            DefaultStartY = _selectedLayoutDraftStartPosition?.Y,
            Displays = FormatLayoutDisplays(SelectedLayoutZones)
        };
        Layouts.Add(newLayout);

        foreach (var zone in SelectedLayoutZones)
        {
            var copy = ZoneRow.FromModel(newId, zone.ToModel());
            copy.LayoutId = newId;
            Zones.Add(copy);
        }

        foreach (var portal in SelectedLayoutPortals)
        {
            var copy = PortalRow.FromModel(newId, portal.ToModel());
            copy.LayoutId = newId;
            Portals.Add(copy);
        }

        SelectedLayout = newLayout;
        RefreshProfileLayoutNames();
        AddLog($"Saved new layout '{newLayout.Name}' with generated portals.");
        AutoSaveConfiguration("Auto-saved configuration after saving layout.");
    }

    private void OverwriteSelectedLayout()
    {
        if (SelectedLayout is null)
        {
            return;
        }

        if (!IsLayoutPersisted(SelectedLayout))
        {
            AddLog("Temporary canvas layouts cannot be overwritten. Use Save As New Layout to add it to the layout list.");
            return;
        }

        if (!PrepareSelectedLayoutDraft())
        {
            return;
        }

        foreach (var zone in Zones.Where(zone => string.Equals(zone.LayoutId, SelectedLayout.Id, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            Zones.Remove(zone);
        }

        foreach (var portal in Portals.Where(portal => string.Equals(portal.LayoutId, SelectedLayout.Id, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            Portals.Remove(portal);
        }

        foreach (var zone in SelectedLayoutZones)
        {
            var copy = ZoneRow.FromModel(SelectedLayout.Id, zone.ToModel());
            copy.LayoutId = SelectedLayout.Id;
            Zones.Add(copy);
        }

        foreach (var portal in SelectedLayoutPortals)
        {
            var copy = PortalRow.FromModel(SelectedLayout.Id, portal.ToModel());
            copy.LayoutId = SelectedLayout.Id;
            Portals.Add(copy);
        }

        SelectedLayout.DefaultStartX = _selectedLayoutDraftStartPosition?.X;
        SelectedLayout.DefaultStartY = _selectedLayoutDraftStartPosition?.Y;
        SelectedLayout.Displays = FormatLayoutDisplays(SelectedLayoutZones);
        OnPropertyChanged(nameof(SelectedLayout));
        RefreshProfileLayoutNames();
        AddLog($"Overwrote layout '{SelectedLayout.Name}' with generated portals.");
        AutoSaveConfiguration("Auto-saved configuration after overwriting layout.");
    }

    private void AddProfile()
    {
        var profileName = GetNextProfileName();
        var result = _profileDialogService.Prompt(
            "Add profile",
            profileName,
            null,
            Layouts.ToArray(),
            SelectedLayout is not null && IsLayoutPersisted(SelectedLayout) ? SelectedLayout.Id : null,
            false,
            Devices.ToArray(),
            Presets.ToArray(),
            null,
            null,
            null,
            null);
        if (result is null)
        {
            return;
        }

        var start = CalculateLayoutStart(result.CursorLayoutId);
        var row = new ProfileRow
        {
            Id = CreateUniqueProfileId(result.Name),
            Name = result.Name,
            Hotkey = result.Hotkey,
            DeviceId = result.DeviceId,
            ScreenId = result.ScreenId,
            PresetId = result.PresetId,
            PresetDisplayName = result.PresetDisplayName,
            CursorLayoutId = result.CursorLayoutId,
            StartX = start?.X,
            StartY = start?.Y,
            PostAckDelayMs = 500,
            RequireH2AckBeforeCursorLayout = true
        };
        Profiles.Add(row);
        FilteredProfiles.Refresh();
        RefreshProfileLayoutNames();
        RefreshDashboardProfiles();
        SelectedProfile = row;
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
        AddLog($"Added profile '{row.Name}'.");
        AutoSaveConfiguration("Auto-saved configuration after adding profile.");
    }

    public void EditProfile(ProfileRow profile)
    {
        var result = _profileDialogService.Prompt(
            "Edit profile",
            profile.Name,
            profile.Hotkey,
            Layouts.ToArray(),
            profile.CursorLayoutId,
            !ProfileHasH2Preset(profile),
            Devices.ToArray(),
            Presets.ToArray(),
            profile.DeviceId,
            profile.ScreenId,
            profile.PresetId,
            profile.PresetDisplayName);
        if (result is null)
        {
            return;
        }

        var start = CalculateLayoutStart(result.CursorLayoutId);
        profile.Name = result.Name;
        profile.Hotkey = result.Hotkey;
        profile.DeviceId = result.DeviceId;
        profile.ScreenId = result.ScreenId;
        profile.PresetId = result.PresetId;
        profile.PresetDisplayName = result.PresetDisplayName;
        profile.CursorLayoutId = result.CursorLayoutId;
        profile.StartX = start?.X;
        profile.StartY = start?.Y;
        profile.PostAckDelayMs = 500;
        profile.RequireH2AckBeforeCursorLayout = true;
        SelectedProfile = profile;
        FilteredProfiles.Refresh();
        RefreshProfileLayoutNames();
        RefreshDashboardProfiles();
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
        AddLog($"Updated profile '{profile.Name}'.");
        AutoSaveConfiguration("Auto-saved configuration after updating profile.");
    }

    private void RemoveSelectedProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        Profiles.Remove(SelectedProfile);
        FilteredProfiles.Refresh();
        RefreshProfileLayoutNames();
        RefreshDashboardProfiles();
        SelectedProfile = Profiles.FirstOrDefault();
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
        AddLog("Removed profile.");
        AutoSaveConfiguration("Auto-saved configuration after removing profile.");
    }

    private void DuplicateSelectedProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var copy = new ProfileRow
        {
            Id = $"{SelectedProfile.Id}-copy-{DateTime.Now:HHmmss}",
            Name = $"{SelectedProfile.Name} Copy",
            Hotkey = null,
            DeviceId = SelectedProfile.DeviceId,
            ScreenId = SelectedProfile.ScreenId,
            PresetId = SelectedProfile.PresetId,
            PresetDisplayName = SelectedProfile.PresetDisplayName,
            CursorLayoutId = SelectedProfile.CursorLayoutId,
            StartX = SelectedProfile.StartX,
            StartY = SelectedProfile.StartY,
            PostAckDelayMs = SelectedProfile.PostAckDelayMs,
            RequireH2AckBeforeCursorLayout = SelectedProfile.RequireH2AckBeforeCursorLayout
        };
        Profiles.Add(copy);
        FilteredProfiles.Refresh();
        RefreshProfileLayoutNames();
        RefreshDashboardProfiles();
        SelectedProfile = copy;
        AddLog($"Duplicated profile '{copy.Name}'. Assign a hotkey before saving if needed.");
    }

    private void ResetToSampleConfiguration()
    {
        StopRouting(clearLayout: true);
        LoadConfigurationIntoRows(SampleConfiguration.Create());
        ActiveProfileName = "";
        LastH2AckStatus = "No H2 command sent since configuration reset.";
        LastRoutingEvent = "Empty bundled configuration loaded in memory; Save Config has not been run.";
        RefreshDiagnostics(log: true);
        RefreshProfileLayoutNames();
        ValidateConfiguration();
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
        AddLog("Loaded empty bundled configuration in memory. Use Save Config to write it to config.json.");
    }

    private void SetCurrentCursorAsProfileStart()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var position = _cursorService.GetPosition();
        SelectedProfile.StartX = position.X;
        SelectedProfile.StartY = position.Y;
        AddLog($"Set profile '{SelectedProfile.Name}' start position to {position.X}, {position.Y}.");
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
        RefreshProfileLayoutNames();
        OnPropertyChanged(nameof(SelectedProfile));
        AddLog($"Mapped layout '{SelectedLayout.Name}' to profile '{SelectedProfile.Name}'.");
    }

    private void GeneratePortalsFromVisualAdjacency()
    {
        if (SelectedLayout is null)
        {
            return;
        }

        SelectedLayoutPortals.Clear();
        var generated = _layoutEditingService.GeneratePortalsFromVisualAdjacency(SelectedLayoutZones);

        foreach (var portal in generated)
        {
            SelectedLayoutPortals.Add(portal);
        }

        AddLog($"Generated {generated.Count} draft portals for layout '{SelectedLayout.Name}' from visual adjacency.");
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
        await SaveConfigurationCoreAsync($"Saved configuration to {_configPath}. Hotkeys were refreshed.");
    }

    private void AutoSaveConfiguration(string successMessage)
    {
        _ = SaveConfigurationCoreAsync(successMessage);
    }

    private async Task SaveConfigurationCoreAsync(string successMessage)
    {
        try
        {
            var configuration = BuildConfiguration();
            var validation = _configurationValidator.Validate(configuration);
            ShowValidation(validation);
            if (!validation.IsValid)
            {
                AddLog("Configuration save was skipped because validation failed.");
                return;
            }

            await _configurationSaveLock.WaitAsync();
            try
            {
                await _configFileService.SaveAsync(configuration, _configPath);
            }
            finally
            {
                _configurationSaveLock.Release();
            }

            HotkeysChanged?.Invoke(this, EventArgs.Empty);
            AddLog(successMessage);
        }
        catch (Exception exception)
        {
            AddLog($"Configuration save failed: {exception.Message}");
        }
    }

    private void StopRouting(bool clearLayout)
    {
        _routingRuntime.StopRouting(clearLayout);
        AddLog("Routing stopped and cursor clipping released.");
        RefreshRuntimeState();
    }

    private void SetDeviceOnline(string deviceId, bool isOnline)
    {
        var deviceRow = Devices.FirstOrDefault(row => string.Equals(row.Id, deviceId, StringComparison.OrdinalIgnoreCase));
        if (deviceRow is not null)
        {
            deviceRow.IsOnline = isOnline;
        }
    }

    private void SelectLayout(string layoutId)
    {
        SelectedLayout = Layouts.FirstOrDefault(row => string.Equals(row.Id, layoutId, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshDiagnostics(bool log)
    {
        Monitors.Clear();
        var monitors = _monitorTopologyService.GetMonitors();
        foreach (var monitor in monitors)
        {
            Monitors.Add(MonitorRow.FromModel(monitor));
        }

        RefreshDisplayPreview();
        if (log)
        {
            var monitorSummary = Monitors.Count == 0
                ? "none"
                : string.Join("; ", Monitors.Select(monitor => $"{monitor.DeviceName} {monitor.BoundsText}"));
            AddLog($"Detected {Monitors.Count} active display(s): {monitorSummary}");
        }
        RefreshAvailableLayoutDisplays();
        RaiseCommandStates();
    }

    private async Task IdentifyDisplaysAsync()
    {
        RefreshDiagnostics(log: true);
        await _displayIdentificationService.IdentifyAsync(Monitors.ToArray(), TimeSpan.FromSeconds(3));
        AddLog("Displayed monitor identification overlays.");
    }

    private void RefreshDisplayPreview()
    {
        if (Monitors.Count == 0)
        {
            DisplayPreviewCanvasWidth = 640;
            DisplayPreviewCanvasHeight = 320;
            return;
        }

        var minLeft = Monitors.Min(monitor => monitor.Left);
        var minTop = Monitors.Min(monitor => monitor.Top);
        var maxRight = Monitors.Max(monitor => monitor.Right);
        var maxBottom = Monitors.Max(monitor => monitor.Bottom);
        var virtualWidth = Math.Max(1, maxRight - minLeft);
        var virtualHeight = Math.Max(1, maxBottom - minTop);
        const double maxPreviewWidth = 1120;
        const double maxPreviewHeight = 360;
        const double padding = 20;
        var scale = Math.Min(maxPreviewWidth / virtualWidth, maxPreviewHeight / virtualHeight);

        foreach (var monitor in Monitors)
        {
            monitor.PreviewLeft = (monitor.Left - minLeft) * scale + padding;
            monitor.PreviewTop = (monitor.Top - minTop) * scale + padding;
            monitor.PreviewWidth = Math.Max(90, monitor.Width * scale);
            monitor.PreviewHeight = Math.Max(70, monitor.Height * scale);
        }

        DisplayPreviewCanvasWidth = virtualWidth * scale + padding * 2;
        DisplayPreviewCanvasHeight = virtualHeight * scale + padding * 2;
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

    private AppConfiguration BuildConfiguration() => new(
        Devices.Select(device => device.ToModel()).ToArray(),
        Layouts.Select(BuildLayout).ToArray(),
        Profiles.Select(profile => profile.ToModel()).ToArray(),
        SafetySettings.Default,
        Presets.Select(preset => preset.ToCachedPreset())
            .ToArray());

    private CursorLayout BuildLayout(LayoutRow layout)
    {
        var start = layout.DefaultStartX is not null && layout.DefaultStartY is not null
            ? new CursorPoint(layout.DefaultStartX.Value, layout.DefaultStartY.Value)
            : (CursorPoint?)null;

        return new CursorLayout(
            layout.Id,
            layout.Name,
            Zones.Where(zone => string.Equals(zone.LayoutId, layout.Id, StringComparison.OrdinalIgnoreCase))
                .Select(BuildZone)
                .ToArray(),
            Portals.Where(portal => string.Equals(portal.LayoutId, layout.Id, StringComparison.OrdinalIgnoreCase))
                .Select(portal => portal.ToModel())
                .ToArray(),
            start,
            string.IsNullOrWhiteSpace(layout.Description) ? null : layout.Description);
    }

    private CursorZone BuildZone(ZoneRow zone)
    {
        var model = zone.ToModel();
        var monitor = FindMonitorForZone(zone, Monitors);
        return monitor is null
            ? model
            : model with
            {
                WindowsRect = new IntRect(monitor.Left, monitor.Top, monitor.Right, monitor.Bottom),
                DisplayName = zone.DisplayName
            };
    }

    private static string FormatLayoutDisplays(IEnumerable<ZoneRow> zones) =>
        LayoutRow.FormatDisplays(zones.Select(zone => zone.ToModel()));

    private bool IsLayoutPersisted(LayoutRow? layout) =>
        layout is not null && Layouts.Any(existing => ReferenceEquals(existing, layout));

    private static bool ProfileHasH2Preset(ProfileRow profile) =>
        !string.IsNullOrWhiteSpace(profile.DeviceId) &&
        profile.ScreenId is not null &&
        profile.PresetId is not null;

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
        RefreshCursorZone();
        OnPropertyChanged(nameof(ActiveLayoutId));
        OnPropertyChanged(nameof(ActiveLayoutName));
        OnPropertyChanged(nameof(IsRoutingEnabled));
        OnPropertyChanged(nameof(RoutingStateText));
    }

    private void LoadConfigurationIntoRows(AppConfiguration configuration)
    {
        Devices.Clear();
        foreach (var device in configuration.Devices.Select(DeviceRow.FromModel))
        {
            Devices.Add(device);
        }

        Layouts.Clear();
        foreach (var layout in configuration.CursorLayouts.Select(LayoutRow.FromModel))
        {
            Layouts.Add(layout);
        }

        Zones.Clear();
        foreach (var zone in configuration.CursorLayouts.SelectMany(layout => layout.Zones.Select(zone => ZoneRow.FromModel(layout.Id, zone))))
        {
            Zones.Add(zone);
        }

        Portals.Clear();
        foreach (var portal in configuration.CursorLayouts.SelectMany(layout => layout.Portals.Select(portal => PortalRow.FromModel(layout.Id, portal))))
        {
            Portals.Add(portal);
        }

        Profiles.Clear();
        foreach (var profile in configuration.Profiles.Select(ProfileRow.FromModel))
        {
            Profiles.Add(profile);
        }

        Presets.Clear();
        foreach (var preset in configuration.PresetCache)
        {
            Presets.Add(PresetRow.FromCachedPreset(preset));
        }

        FilteredProfiles.Refresh();
        RefreshProfileLayoutNames();
        RefreshDashboardProfiles();
        SelectedDevice = Devices.FirstOrDefault();
        RefreshSelectedPreset(SelectedDevice?.Id);
        SelectedLayout = Layouts.FirstOrDefault();
        SelectedProfile = Profiles.FirstOrDefault();
    }

    private void RefreshSelectedPreset(string? preferredDeviceId)
    {
        var selectedStillExists = SelectedPreset is not null &&
                                  Presets.Any(preset => ReferenceEquals(preset, SelectedPreset));
        if (selectedStillExists &&
            (string.IsNullOrWhiteSpace(preferredDeviceId) ||
             string.Equals(SelectedPreset!.DeviceConfigId, preferredDeviceId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        SelectedPreset = Presets
                             .OrderBy(preset => string.Equals(preset.DeviceConfigId, preferredDeviceId, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                             .ThenBy(preset => preset.ScreenId)
                             .ThenBy(preset => preset.FriendlyPresetNumber)
                             .FirstOrDefault();
    }

    private void RefreshDashboardProfiles()
    {
        DashboardProfiles.Clear();
        foreach (var profile in Profiles)
        {
            DashboardProfiles.Add(profile);
        }
    }

    private void RefreshProfileLayoutNames()
    {
        var layoutNames = Layouts.ToDictionary(
            layout => layout.Id,
            layout => layout.Name,
            StringComparer.OrdinalIgnoreCase);
        foreach (var profile in Profiles)
        {
            profile.CursorLayoutName = !string.IsNullOrWhiteSpace(profile.CursorLayoutId) &&
                                       layoutNames.TryGetValue(profile.CursorLayoutId, out var layoutName)
                ? layoutName
                : null;
        }
    }

    public void MoveZoneVisual(ZoneRow zone, double deltaX, double deltaY)
    {
        _layoutEditingService.MoveZoneVisual(zone, SelectedLayoutZones, deltaX, deltaY);
        SelectedZone = zone;
        RefreshLayoutPreviewCanvasSize();
    }

    public void ResizeZoneVisual(ZoneRow zone, double deltaWidth, double deltaHeight)
    {
        _layoutEditingService.ResizeZoneVisual(zone, SelectedLayoutZones, deltaWidth, deltaHeight);
        SelectedZone = zone;
        RefreshLayoutPreviewCanvasSize();
    }

    public void CompleteZoneVisualEdit(ZoneRow zone)
    {
        _layoutEditingService.AttachZoneToNearest(zone, SelectedLayoutZones);
        NormalizeSelectedLayoutVisualOrigin();
        RefreshLayoutPreviewCanvasSize();
    }

    private void AttachAllDraftZonesToNearest()
    {
        _layoutEditingService.AttachAllDraftZonesToNearest(SelectedLayoutZones);
    }

    private void NormalizeSelectedLayoutVisualOrigin()
    {
        _layoutEditingService.NormalizeVisualOrigin(SelectedLayoutZones);
    }

    private void RaiseCommandStates()
    {
        foreach (var command in new ICommand[]
        {
            RemoveDeviceCommand,
            GetAllPresetsCommand,
            GetPresetsCommand,
            RemoveLayoutCommand,
            DeleteLayoutCommand,
            AddZoneCommand,
            RemoveZoneCommand,
            AddDisplayToCanvasCommand,
            AddPortalCommand,
            RemovePortalCommand,
            CreateLayoutFromMonitorsCommand,
            ApplyDetectedMonitorCoordinatesCommand,
            ApplyCanvasLayoutCommand,
            RemoveProfileCommand,
            DuplicateProfileCommand,
            SetCurrentCursorAsStartCommand,
            ApplySelectedPresetToProfileCommand,
            ApplySelectedLayoutToProfileCommand,
            GeneratePortalsCommand,
            SaveLayoutAsNewCommand,
            OverwriteSelectedLayoutCommand,
            ExecuteSelectedProfileCommand,
            ExecuteProfileCommand,
            IdentifyDisplaysCommand
        })
        {
            switch (command)
            {
                case RelayCommand relay:
                    relay.RaiseCanExecuteChanged();
                    break;
                case RelayCommand<LayoutRow> layoutRelay:
                    layoutRelay.RaiseCanExecuteChanged();
                    break;
                case AsyncRelayCommand async:
                    async.RaiseCanExecuteChanged();
                    break;
                case AsyncRelayCommand<ProfileRow> asyncProfile:
                    asyncProfile.RaiseCanExecuteChanged();
                    break;
            }
        }
    }

    private bool FilterProfile(object item)
    {
        if (item is not ProfileRow profile || string.IsNullOrWhiteSpace(ProfileFilter))
        {
            return true;
        }

        return Contains(profile.Id, ProfileFilter) ||
               Contains(profile.Name, ProfileFilter) ||
               Contains(profile.Hotkey, ProfileFilter) ||
               Contains(profile.DeviceId, ProfileFilter) ||
               Contains(profile.CursorLayoutId, ProfileFilter) ||
               Contains(profile.LayoutSummary, ProfileFilter) ||
               Contains(profile.PresetDisplayName, ProfileFilter);
    }

    private void RefreshSelectedLayoutCollections()
    {
        if (_selectedZone is not null)
        {
            _selectedZone.IsSelected = false;
        }

        _selectedZone = null;
        _selectedLayoutDraftStartPosition = null;
        SelectedLayoutZones.Clear();
        SelectedLayoutPortals.Clear();
        if (SelectedLayout is null)
        {
            OnPropertyChanged(nameof(SelectedZone));
            OnPropertyChanged(nameof(HasSelectedZone));
            RefreshAvailableLayoutDisplays();
            return;
        }

        foreach (var zone in Zones.Where(zone => string.Equals(zone.LayoutId, SelectedLayout.Id, StringComparison.OrdinalIgnoreCase)))
        {
            var copy = ZoneRow.FromModel(SelectedLayout.Id, zone.ToModel());
            copy.LayoutId = SelectedLayout.Id;
            SelectedLayoutZones.Add(copy);
        }

        foreach (var portal in Portals.Where(portal => string.Equals(portal.LayoutId, SelectedLayout.Id, StringComparison.OrdinalIgnoreCase)))
        {
            var copy = PortalRow.FromModel(SelectedLayout.Id, portal.ToModel());
            copy.LayoutId = SelectedLayout.Id;
            SelectedLayoutPortals.Add(copy);
        }

        SelectedZone = SelectedLayoutZones.FirstOrDefault();
        RefreshAvailableLayoutDisplays();
        RefreshLayoutPreviewCanvasSize();
    }

    private void RefreshAvailableLayoutDisplays()
    {
        var selectedIds = SelectedLayoutZones
            .Select(zone => NormalizeZoneId(zone.Id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        AvailableLayoutDisplays.Clear();
        foreach (var monitor in Monitors.Where(monitor => !selectedIds.Contains(NormalizeZoneId(monitor.DeviceName))))
        {
            AvailableLayoutDisplays.Add(monitor);
        }

        if (SelectedAvailableMonitor is null ||
            !AvailableLayoutDisplays.Contains(SelectedAvailableMonitor))
        {
            SelectedAvailableMonitor = AvailableLayoutDisplays.FirstOrDefault();
        }
    }

    private static ZoneRow CreateZoneFromMonitor(string layoutId, MonitorRow monitor)
    {
        return new ZoneRow
        {
            LayoutId = layoutId,
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
    }

    private string GetNextLayoutName()
    {
        var existing = Layouts
            .Select(layout => layout.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; ; i++)
        {
            var name = $"layout{i}";
            if (!existing.Contains(name))
            {
                return name;
            }
        }
    }

    private string CreateUniqueLayoutId(string name)
    {
        var baseId = NormalizeZoneId(name).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = "layout";
        }

        var id = baseId.StartsWith("layout-", StringComparison.OrdinalIgnoreCase)
            ? baseId
            : $"layout-{baseId}";
        var existing = Layouts.Select(layout => layout.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(id))
        {
            return id;
        }

        for (var i = 2; ; i++)
        {
            var candidate = $"{id}-{i}";
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private string GetNextProfileName()
    {
        var existing = Profiles
            .Select(profile => profile.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; ; i++)
        {
            var name = $"profile{i}";
            if (!existing.Contains(name))
            {
                return name;
            }
        }
    }

    private string CreateUniqueProfileId(string name)
    {
        var baseId = NormalizeZoneId(name).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = "profile";
        }

        var id = baseId.StartsWith("profile-", StringComparison.OrdinalIgnoreCase)
            ? baseId
            : $"profile-{baseId}";
        var existing = Profiles.Select(profile => profile.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(id))
        {
            return id;
        }

        for (var i = 2; ; i++)
        {
            var candidate = $"{id}-{i}";
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private string CreateUniqueDeviceId(string name)
    {
        var baseId = NormalizeZoneId(name).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = "device";
        }

        var id = baseId.StartsWith("h2-", StringComparison.OrdinalIgnoreCase)
            ? baseId
            : $"h2-{baseId}";
        var existing = Devices.Select(device => device.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(id))
        {
            return id;
        }

        for (var i = 2; ; i++)
        {
            var candidate = $"{id}-{i}";
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private CursorPoint? CalculateLayoutStart(string? layoutId)
    {
        if (string.IsNullOrWhiteSpace(layoutId))
        {
            return null;
        }

        var visibleZone = Zones
            .Where(zone => string.Equals(zone.LayoutId, layoutId, StringComparison.OrdinalIgnoreCase) && zone.IsVisible)
            .OrderBy(zone => zone.WindowsTop)
            .ThenBy(zone => zone.WindowsLeft)
            .FirstOrDefault();
        return visibleZone is null
            ? null
            : new CursorPoint(
                visibleZone.WindowsLeft + (visibleZone.WindowsRight - visibleZone.WindowsLeft) / 2,
                visibleZone.WindowsTop + (visibleZone.WindowsBottom - visibleZone.WindowsTop) / 2);
    }

    private void RefreshLayoutPreviewCanvasSize()
    {
        OnPropertyChanged(nameof(LayoutPreviewCanvasWidth));
        OnPropertyChanged(nameof(LayoutPreviewCanvasHeight));
    }

    private MonitorRow? FindMonitorForZone(ZoneRow zone, IReadOnlyList<MonitorRow> orderedMonitors)
    {
        var zoneId = NormalizeZoneId(zone.Id);
        var direct = orderedMonitors.FirstOrDefault(monitor =>
            string.Equals(NormalizeZoneId(monitor.DeviceName), zoneId, StringComparison.OrdinalIgnoreCase));
        if (direct is not null)
        {
            return direct;
        }

        return orderedMonitors.FirstOrDefault(monitor =>
            zone.WindowsLeft == monitor.Left &&
            zone.WindowsTop == monitor.Top &&
            zone.WindowsRight == monitor.Right &&
            zone.WindowsBottom == monitor.Bottom);
    }

    private int RefreshSelectedLayoutWindowsCoordinatesFromDetectedDisplays()
    {
        if (Monitors.Count == 0)
        {
            return 0;
        }

        var changed = 0;
        foreach (var zone in SelectedLayoutZones)
        {
            var monitor = FindMonitorForZone(zone, Monitors);
            if (monitor is null)
            {
                continue;
            }

            if (zone.WindowsLeft == monitor.Left &&
                zone.WindowsTop == monitor.Top &&
                zone.WindowsRight == monitor.Right &&
                zone.WindowsBottom == monitor.Bottom)
            {
                continue;
            }

            zone.WindowsLeft = monitor.Left;
            zone.WindowsTop = monitor.Top;
            zone.WindowsRight = monitor.Right;
            zone.WindowsBottom = monitor.Bottom;
            changed++;
        }

        return changed;
    }

    private static bool Contains(string? value, string filter) =>
        value?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsRoutingEvent(string message) =>
        message.Contains("Portal mapped", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Target:", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Cursor entered hidden zone", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("outside every known zone", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Activated cursor layout", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Activating cursor layout", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Routing started", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Routing did not start", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Routing stopped", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Emergency unlock", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Monitor topology changed", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Cursor clipped", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeZoneId(string text)
    {
        var chars = text.Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 ? "MONITOR" : new string(chars).ToUpperInvariant();
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
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
