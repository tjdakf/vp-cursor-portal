using H2CursorRouter.App.ViewModels;

namespace H2CursorRouter.App.Services;

internal sealed class MonitorZoneMatcher
{
    public static string NormalizeZoneId(string text)
    {
        var chars = text.Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 ? "MONITOR" : new string(chars).ToUpperInvariant();
    }

    public ZoneRow CreateZoneFromMonitor(string layoutId, MonitorRow monitor) => new()
    {
        LayoutId = layoutId,
        Id = NormalizeZoneId(monitor.DeviceName),
        DisplayName = monitor.DeviceName,
        DisplayAlias = monitor.DisplayAlias,
        WindowsLeft = monitor.Left,
        WindowsTop = monitor.Top,
        WindowsRight = monitor.Right,
        WindowsBottom = monitor.Bottom,
        VisualLeft = monitor.Left,
        VisualTop = monitor.Top,
        VisualRight = monitor.Right,
        VisualBottom = monitor.Bottom,
        IsVisible = true
    };

    public MonitorRow? FindMonitorForZone(ZoneRow zone, IReadOnlyList<MonitorRow> monitors)
    {
        var zoneId = NormalizeZoneId(zone.Id);
        var direct = monitors.FirstOrDefault(monitor =>
            string.Equals(NormalizeZoneId(monitor.DeviceName), zoneId, StringComparison.OrdinalIgnoreCase));
        if (direct is not null)
        {
            return direct;
        }

        return monitors.FirstOrDefault(monitor =>
            zone.WindowsLeft == monitor.Left &&
            zone.WindowsTop == monitor.Top &&
            zone.WindowsRight == monitor.Right &&
            zone.WindowsBottom == monitor.Bottom);
    }

    public int RefreshWindowsCoordinatesFromDetectedDisplays(
        IEnumerable<ZoneRow> zones,
        IReadOnlyList<MonitorRow> monitors)
    {
        if (monitors.Count == 0)
        {
            return 0;
        }

        var changed = 0;
        foreach (var zone in zones)
        {
            var monitor = FindMonitorForZone(zone, monitors);
            if (monitor is null)
            {
                continue;
            }

            if (zone.WindowsLeft == monitor.Left &&
                zone.WindowsTop == monitor.Top &&
                zone.WindowsRight == monitor.Right &&
                zone.WindowsBottom == monitor.Bottom)
            {
                continue;
            }

            zone.WindowsLeft = monitor.Left;
            zone.WindowsTop = monitor.Top;
            zone.WindowsRight = monitor.Right;
            zone.WindowsBottom = monitor.Bottom;
            changed++;
        }

        return changed;
    }
}
