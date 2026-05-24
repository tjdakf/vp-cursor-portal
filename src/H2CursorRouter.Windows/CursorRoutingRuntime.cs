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
    private CursorPoint? _pendingProgrammaticMoveTarget;
    private DateTimeOffset _lastDecisionLogAt = DateTimeOffset.MinValue;
    private string _lastDecisionLogSignature = "";

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
        var monitors = GetCurrentMonitors();
        var runtimeLayout = RebindLayoutToCurrentMonitorBounds(layout, monitors, out var reboundCount);
        if (reboundCount > 0)
        {
            LogMessage($"Updated {reboundCount} layout zone(s) from the current Windows monitor coordinates.");
        }

        runtimeLayout = AddHiddenTopologyZones(runtimeLayout, monitors);
        var validation = _layoutValidator.Validate(runtimeLayout);
        if (!validation.IsValid)
        {
            LogMessage($"Refused to activate invalid layout: {string.Join("; ", validation.Errors)}");
            return;
        }

        StopRouting(clearLayout: true);
        var safeStartPosition = _routingEngine.ResolveStartPosition(runtimeLayout, startPosition);
        _cursorService.SetPosition(safeStartPosition);
        ApplyClipIfSafe(runtimeLayout);
        _activeLayout = runtimeLayout;
        _lastValidPosition = safeStartPosition;
        _previousPosition = safeStartPosition;
        _pendingProgrammaticMoveTarget = safeStartPosition;
        StartRouting(pollInterval);
        LogMessage($"Activated cursor layout '{runtimeLayout.Name}'.");
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
                if (_pendingProgrammaticMoveTarget is CursorPoint moveTarget)
                {
                    _previousPosition = moveTarget;
                    _lastValidPosition = moveTarget;
                    _pendingProgrammaticMoveTarget = null;
                    if (current == moveTarget)
                    {
                        continue;
                    }
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
                            LogRoutingDecision(decision);
                            _cursorService.SetPosition(decision.Target.Value);
                            _previousPosition = decision.Target.Value;
                            _lastValidPosition = decision.Target.Value;
                            _pendingProgrammaticMoveTarget = decision.Target.Value;
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

    private IReadOnlyList<MonitorInfo> GetCurrentMonitors()
    {
        try
        {
            return _monitorTopologyService.GetMonitors();
        }
        catch (Exception ex)
        {
            LogMessage($"Could not read monitor topology: {ex.Message}");
            return [];
        }
    }

    private static CursorLayout RebindLayoutToCurrentMonitorBounds(
        CursorLayout layout,
        IReadOnlyList<MonitorInfo> monitors,
        out int reboundCount)
    {
        reboundCount = 0;
        if (monitors.Count == 0)
        {
            return layout;
        }

        var zones = new List<CursorZone>(layout.Zones.Count);
        foreach (var zone in layout.Zones)
        {
            var monitor = FindMatchingMonitor(zone, monitors);
            if (monitor is not null && zone.WindowsRect != monitor.Bounds)
            {
                zones.Add(zone with { WindowsRect = monitor.Bounds });
                reboundCount++;
                continue;
            }

            zones.Add(zone);
        }

        return reboundCount == 0 ? layout : layout with { Zones = zones };
    }

    private static MonitorInfo? FindMatchingMonitor(CursorZone zone, IReadOnlyList<MonitorInfo> monitors)
    {
        var zoneId = NormalizeDisplayKey(zone.Id);
        var displayName = NormalizeDisplayKey(zone.DisplayName);
        return monitors.FirstOrDefault(monitor =>
        {
            var monitorName = NormalizeDisplayKey(monitor.DeviceName);
            return string.Equals(monitorName, zoneId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(monitorName, displayName, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string NormalizeDisplayKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace(@"\\.\", "", StringComparison.OrdinalIgnoreCase).Trim();
        return new string(normalized
            .Where(character => char.IsLetterOrDigit(character) || character is '_' or '-')
            .ToArray());
    }

    private CursorLayout AddHiddenTopologyZones(CursorLayout layout, IReadOnlyList<MonitorInfo> monitors)
    {
        if (monitors.Count == 0)
        {
            return layout;
        }

        var zones = layout.Zones.ToList();
        foreach (var monitor in monitors)
        {
            if (zones.Any(zone => zone.WindowsRect == monitor.Bounds))
            {
                continue;
            }

            var id = CreateUniqueHiddenZoneId(monitor.DeviceName, zones);
            zones.Add(new CursorZone(
                id,
                $"{monitor.DeviceName} (hidden)",
                monitor.Bounds,
                new VisualRect(monitor.Bounds.Left, monitor.Bounds.Top, monitor.Bounds.Right, monitor.Bounds.Bottom),
                IsVisible: false));
        }

        return zones.Count == layout.Zones.Count
            ? layout
            : layout with { Zones = zones };
    }

    private void ApplyClipIfSafe(CursorLayout layout)
    {
        var visibleZones = layout.Zones.Where(zone => zone.IsVisible).ToArray();
        if (visibleZones.Length == 1)
        {
            _cursorService.ClipTo(visibleZones[0].WindowsRect);
            LogMessage($"Cursor clipped to visible zone '{visibleZones[0].Id}'.");
            return;
        }

        _cursorService.ReleaseClip();
    }

    private static string CreateUniqueHiddenZoneId(string deviceName, IReadOnlyList<CursorZone> zones)
    {
        var normalized = new string(deviceName
            .Where(character => char.IsLetterOrDigit(character) || character is '_' or '-')
            .ToArray());

        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "hidden-monitor";
        }

        var candidate = normalized;
        var index = 1;
        while (zones.Any(zone => string.Equals(zone.Id, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{normalized}-{index++}";
        }

        return candidate;
    }

    private void LogMessage(string message) => Log?.Invoke(this, message);

    private void LogRoutingDecision(RoutingDecision decision)
    {
        var target = decision.Target;
        var signature = $"{decision.Kind}:{decision.Reason}:{target?.X},{target?.Y}";
        var now = DateTimeOffset.UtcNow;
        if (string.Equals(signature, _lastDecisionLogSignature, StringComparison.Ordinal) &&
            now - _lastDecisionLogAt < TimeSpan.FromSeconds(1))
        {
            return;
        }

        _lastDecisionLogSignature = signature;
        _lastDecisionLogAt = now;
        if (target is null)
        {
            LogMessage(decision.Reason);
            return;
        }

        var prefix = decision.Kind == RoutingDecisionKind.MoveToTarget ? "Portal move" : "Cursor revert";
        LogMessage($"{prefix}: {decision.Reason} Target: {target.Value.X}, {target.Value.Y}.");
    }
}
