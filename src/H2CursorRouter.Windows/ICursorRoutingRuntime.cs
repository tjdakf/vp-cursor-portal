using H2CursorRouter.Core.Geometry;

namespace H2CursorRouter.Windows;

public interface ICursorRoutingRuntime : IDisposable
{
    event EventHandler<string>? Log;

    bool IsRoutingEnabled { get; }
    string? ActiveLayoutId { get; }

    void ActivateLayout(CursorLayout layout, CursorPoint startPosition, TimeSpan pollInterval);
    void StopRouting(bool clearLayout);
    void EmergencyUnlock();
}
