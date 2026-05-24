using System.Collections.ObjectModel;
using H2CursorRouter.App.Services;
using H2CursorRouter.Core.Configuration;
using H2CursorRouter.H2;

namespace H2CursorRouter.App.ViewModels;

public sealed class DevicePresetViewModel : ViewModelBase
{
    private DeviceRow? _selectedDevice;
    private PresetRow? _selectedPreset;
    private string _h2ConnectionStatus = "Not checked yet.";
    private bool _isCheckingH2Connection;
    private readonly IH2DeviceClient _h2DeviceClient;
    private readonly IDeviceDialogService _deviceDialogService;
    private readonly H2PresetEnumParser _presetEnumParser = new();
    private readonly Action<string> _addLog;
    private readonly Action<string> _autoSaveConfiguration;

    public DevicePresetViewModel(
        IEnumerable<DeviceRow> devices,
        IEnumerable<PresetRow> presets,
        IH2DeviceClient h2DeviceClient,
        IDeviceDialogService deviceDialogService,
        Action<string> addLog,
        Action<string> autoSaveConfiguration)
    {
        _h2DeviceClient = h2DeviceClient;
        _deviceDialogService = deviceDialogService;
        _addLog = addLog;
        _autoSaveConfiguration = autoSaveConfiguration;
        Devices = new ObservableCollection<DeviceRow>(devices);
        Presets = new ObservableCollection<PresetRow>(presets);
    }

    public ObservableCollection<DeviceRow> Devices { get; }
    public ObservableCollection<PresetRow> Presets { get; }

    public DeviceRow? SelectedDevice
    {
        get => _selectedDevice;
        set => SetProperty(ref _selectedDevice, value);
    }

    public PresetRow? SelectedPreset
    {
        get => _selectedPreset;
        set => SetProperty(ref _selectedPreset, value);
    }

    public string H2ConnectionStatus
    {
        get => _h2ConnectionStatus;
        set
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

    public void AddDevice(Func<string, string> createUniqueDeviceId)
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
            Id = createUniqueDeviceId(result.Name),
            Name = result.Name,
            Host = result.Host,
            Port = result.Port,
            DeviceId = 0,
            TimeoutMs = 1000
        };
        Devices.Add(row);
        SelectedDevice = row;
        _addLog($"Added H2 device '{row.Name}' at {row.Host}:{row.Port}.");
        _autoSaveConfiguration("Auto-saved configuration after adding device.");
    }

    public void RemoveSelectedDevice()
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
        _addLog("Removed H2 device.");
        _autoSaveConfiguration("Auto-saved configuration after removing device.");
    }

    public async Task GetPresetsAsync()
    {
        if (SelectedDevice is null)
        {
            _addLog("No H2 device selected.");
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
            _autoSaveConfiguration("Auto-saved configuration after refreshing presets.");
        }
    }

    public async Task GetAllPresetsAsync()
    {
        if (Devices.Count == 0)
        {
            _addLog("No H2 devices configured.");
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
            _autoSaveConfiguration("Auto-saved configuration after refreshing all presets.");
        }
    }

    public async Task RefreshH2ConnectionStatusAsync()
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

    public void RefreshSelectedPreset(string? preferredDeviceId)
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

    public void SetDeviceOnline(string deviceId, bool isOnline)
    {
        var deviceRow = Devices.FirstOrDefault(row => string.Equals(row.Id, deviceId, StringComparison.OrdinalIgnoreCase));
        if (deviceRow is not null)
        {
            deviceRow.IsOnline = isOnline;
        }
    }

    private async Task<bool> GetPresetsForDeviceAsync(DeviceRow deviceRow)
    {
        var device = deviceRow.ToModel();
        _addLog($"Sending R0600 to {device.Host}:{device.Port}.");
        var result = await _h2DeviceClient.GetPresetEnumAsync(device, device.DeviceId, deviceRow.PresetEnumScreenId);

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.ResponseJson))
        {
            deviceRow.IsOnline = false;
            H2ConnectionStatus = $"No response: {result.Message}";
            _addLog($"Preset enum request failed for H2 device '{deviceRow.Name}': {result.Message}");
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
            _addLog($"Loaded {parsed.Count} presets from H2 device '{deviceRow.Name}'.");
            return true;
        }
        catch (Exception exception)
        {
            deviceRow.IsOnline = false;
            H2ConnectionStatus = $"Unexpected response: {exception.Message}";
            _addLog($"Preset enum response from H2 device '{deviceRow.Name}' could not be parsed: {exception.Message}");
            _addLog($"Raw preset enum response: {result.ResponseJson}");
            return false;
        }
    }
}
