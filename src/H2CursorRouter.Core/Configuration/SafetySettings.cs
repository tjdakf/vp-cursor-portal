namespace H2CursorRouter.Core.Configuration;

public sealed record SafetySettings(
    string EmergencyHotkey,
    bool DisableRoutingOnMonitorTopologyChange,
    bool StartWithRoutingDisabled)
{
    public static SafetySettings Default { get; } = new(
        "Ctrl+Alt+Shift+Esc",
        DisableRoutingOnMonitorTopologyChange: true,
        StartWithRoutingDisabled: true);
}
