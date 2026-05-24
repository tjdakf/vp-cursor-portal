using System.Collections.ObjectModel;

namespace H2CursorRouter.App.ViewModels;

public sealed class LayoutEditorViewModel : ViewModelBase
{
    private LayoutRow? _selectedLayout;
    private ZoneRow? _selectedZone;
    private MonitorRow? _selectedAvailableMonitor;
    private PortalRow? _selectedPortal;
    private double _layoutPreviewScale = 0.16;
    private double _displayPreviewCanvasWidth = 640;
    private double _displayPreviewCanvasHeight = 320;

    public LayoutEditorViewModel(
        IEnumerable<LayoutRow> layouts,
        IEnumerable<ZoneRow> zones,
        IEnumerable<PortalRow> portals)
    {
        Layouts = new ObservableCollection<LayoutRow>(layouts);
        Zones = new ObservableCollection<ZoneRow>(zones);
        Portals = new ObservableCollection<PortalRow>(portals);
        Monitors = new ObservableCollection<MonitorRow>();
        SelectedLayoutZones = new ObservableCollection<ZoneRow>();
        SelectedLayoutPortals = new ObservableCollection<PortalRow>();
        AvailableLayoutDisplays = new ObservableCollection<MonitorRow>();
    }

    public ObservableCollection<LayoutRow> Layouts { get; }
    public ObservableCollection<ZoneRow> Zones { get; }
    public ObservableCollection<PortalRow> Portals { get; }
    public ObservableCollection<MonitorRow> Monitors { get; }
    public ObservableCollection<ZoneRow> SelectedLayoutZones { get; }
    public ObservableCollection<PortalRow> SelectedLayoutPortals { get; }
    public ObservableCollection<MonitorRow> AvailableLayoutDisplays { get; }

    public LayoutRow? SelectedLayout
    {
        get => _selectedLayout;
        set => SetProperty(ref _selectedLayout, value);
    }

    public ZoneRow? SelectedZone
    {
        get => _selectedZone;
        set => SetProperty(ref _selectedZone, value);
    }

    public MonitorRow? SelectedAvailableMonitor
    {
        get => _selectedAvailableMonitor;
        set => SetProperty(ref _selectedAvailableMonitor, value);
    }

    public PortalRow? SelectedPortal
    {
        get => _selectedPortal;
        set => SetProperty(ref _selectedPortal, value);
    }

    public double LayoutPreviewScale
    {
        get => _layoutPreviewScale;
        set => SetProperty(ref _layoutPreviewScale, Math.Clamp(value, 0.08, 0.5));
    }

    public double DisplayPreviewCanvasWidth
    {
        get => _displayPreviewCanvasWidth;
        set => SetProperty(ref _displayPreviewCanvasWidth, value);
    }

    public double DisplayPreviewCanvasHeight
    {
        get => _displayPreviewCanvasHeight;
        set => SetProperty(ref _displayPreviewCanvasHeight, value);
    }
}
