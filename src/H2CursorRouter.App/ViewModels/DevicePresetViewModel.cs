using System.Collections.ObjectModel;

namespace H2CursorRouter.App.ViewModels;

public sealed class DevicePresetViewModel : ViewModelBase
{
    private DeviceRow? _selectedDevice;
    private PresetRow? _selectedPreset;
    private string _h2ConnectionStatus = "Not checked yet.";

    public DevicePresetViewModel(IEnumerable<DeviceRow> devices, IEnumerable<PresetRow> presets)
    {
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
}
