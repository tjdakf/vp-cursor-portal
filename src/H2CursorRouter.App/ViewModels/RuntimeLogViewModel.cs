using System.Collections.ObjectModel;

namespace H2CursorRouter.App.ViewModels;

public sealed class RuntimeLogViewModel : ViewModelBase
{
    private string _runtimeStatus = "Routing disabled on startup.";
    private string _lastRoutingEvent = "Routing disabled on startup.";

    public RuntimeLogViewModel()
    {
        Logs = new ObservableCollection<string>();
        ValidationErrors = new ObservableCollection<string>();
    }

    public ObservableCollection<string> Logs { get; }
    public ObservableCollection<string> ValidationErrors { get; }

    public string RuntimeStatus
    {
        get => _runtimeStatus;
        set => SetProperty(ref _runtimeStatus, value);
    }

    public string LastRoutingEvent
    {
        get => _lastRoutingEvent;
        set => SetProperty(ref _lastRoutingEvent, value);
    }
}
