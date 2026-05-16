using H2CursorRouter.Core.Geometry;
using H2CursorRouter.Core.Validation;

namespace H2CursorRouter.Windows;

public sealed class CursorRoutingRuntime : IDisposable
{
    private readonly ICursorService _cursorService;
    private readonly IMonitorTopologyService _monitorTopologyService;
    private readonly CursorRoutingEngine _routingEngine;
    private readonly CursorLayoutValidator _layoutValidator = new();
    private readonly object _sync = new();
    private CancellationTokenSource? _routingCts;
    private Task? _routingTask;
    private CursorLayout? _activeLayout;
    private CursorPoint _lastValidPosition;
    private CursorPoint _previousPosition;
    private bool _ignoreNextPollAfterMove;

    public CursorRoutingRuntime(
        ICursorService cursorService,
        IMonitorTopologyService monitorTopologyService,
        CursorRoutingEngine routingEngine)
    {
        _cursorService = cursorService;
        _monitorTopologyService = monitorTopologyService;
        _routingEngine = routingEngine;
        _lastValidPosition = _cursorService.GetPosition();
        _previousPosition = _lastValidPosition;
        _monitorTopologyService.TopologyChanged += OnTopologyChanged;
    }

    public event EventHandler<string>? Log;
    public bool IsRoutingEnabled { get; private set; }
    public string? ActiveLayoutId => _activeLayout?.Id;

    public void ActivateLayout(CursorLayout layout, CursorPoint startPosition, TimeSpan pollInterval)
    {
        var validation = _layoutValidator.Validate(layout);
        if (!validation.IsValid)
        {
            LogMessage($"Refused to activate invalid layout: {string.Join("; ", validation.Errors)}");
            return;
        }

        StopRouting(clearLayout: true);
        _cursorService.SetPosition(startPosition);
        _activeLayout = layout;
        _lastValidPosition = startPosition;
        _previousPosition = startPosition;
        _ignoreNextPollAfterMove = true;
        StartRouting(pollInterval);
        LogMessage($"Activated cursor layout '{layout.Name}'.");
    }

    public void StopRouting(bool clearLayout)
    {
        lock (_sync)
        {
            _routingCts?.Cancel();
            _routingCts?.Dispose();
            _routingCts = null;
            _routingTask = null;
            IsRoutingEnabled = false;
            if (clearLayout)
            {
                _activeLayout = null;
            }
        }

        _cursorService.ReleaseClip();
    }

    public void EmergencyUnlock()
    {
        StopRouting(clearLayout: true);
        LogMessage("Emergency unlock: routing disabled, cursor clipping released, active layout cleared.");
    }

    public void Dispose()
    {
        _monitorTopologyService.TopologyChanged -= OnTopologyChanged;
        StopRouting(clearLayout: true);
    }

    private void StartRouting(TimeSpan pollInterval)
    {
        lock (_sync)
        {
            _routingCts = new CancellationTokenSource();
            IsRoutingEnabled = true;
            _routingTask = Task.Run(() => PollLoopAsync(pollInterval, _routingCts.Token));
        }
    }

    private async Task PollLoopAsync(TimeSpan pollInterval, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(pollInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                CursorLayout? layout;
                lock (_sync)
                {
                    layout = _activeLayout;
                }

                if (layout is null)
                {
                    continue;
                }

                var current = _cursorService.GetPosition();
                if (_ignoreNextPollAfterMove)
                {
                    _previousPosition = current;
                    _lastValidPosition = current;
                    _ignoreNextPollAfterMove = false;
                    continue;
                }

                var decision = _routingEngine.Evaluate(layout, _previousPosition, current, _lastValidPosition);
                switch (decision.Kind)
                {
                    case RoutingDecisionKind.KeepCurrent:
                        _previousPosition = current;
                        _lastValidPosition = current;
                        break;
                    case RoutingDecisionKind.MoveToTarget:
                    case RoutingDecisionKind.RevertToLastValid:
                        if (decision.Target is not null)
                        {
                            _cursorService.SetPosition(decision.Target.Value);
                            _previousPosition = decision.Target.Value;
                            _lastValidPosition = decision.Target.Value;
                            _ignoreNextPollAfterMove = true;
                        }

                        break;
                    case RoutingDecisionKind.RejectUnsafeLayout:
                        LogMessage($"Routing disabled because active layout is unsafe: {decision.Reason}");
                        StopRouting(clearLayout: true);
                        return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void OnTopologyChanged(object? sender, EventArgs e)
    {
        StopRouting(clearLayout: true);
        LogMessage("Monitor topology changed: routing disabled until layouts are revalidated.");
    }

    private void LogMessage(string message) => Log?.Invoke(this, message);
}
