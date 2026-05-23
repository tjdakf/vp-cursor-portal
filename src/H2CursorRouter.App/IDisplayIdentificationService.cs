using H2CursorRouter.App.ViewModels;

namespace H2CursorRouter.App;

public interface IDisplayIdentificationService
{
    Task IdentifyAsync(IReadOnlyList<MonitorRow> monitors, TimeSpan duration);
}
