using H2CursorRouter.Core.Geometry;

namespace H2CursorRouter.Windows;

public interface IMonitorTopologyService : IDisposable
{
    event EventHandler? TopologyChanged;
    IReadOnlyList<MonitorInfo> GetMonitors();
    string GetTopologySignature();
    void StartWatching(TimeSpan interval);
}
