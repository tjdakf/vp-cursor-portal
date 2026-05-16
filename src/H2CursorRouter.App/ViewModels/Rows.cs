using H2CursorRouter.Core.Domain;
using H2CursorRouter.Core.Geometry;
using H2CursorRouter.Core.Profiles;
using H2CursorRouter.Windows;

namespace H2CursorRouter.App.ViewModels;

public sealed class DeviceRow
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = H2DeviceConfig.DefaultPort;
    public int DeviceId { get; set; }
    public int TimeoutMs { get; set; } = 1000;

    public static DeviceRow FromModel(H2DeviceConfig device) => new()
    {
        Id = device.Id,
        Name = device.Name,
        Host = device.Host,
        Port = device.Port,
        DeviceId = device.DeviceId,
        TimeoutMs = (int)device.Timeout.TotalMilliseconds
    };

    public H2DeviceConfig ToModel() => new(
        Id,
        Name,
        Host,
        Port,
        DeviceId,
        TimeSpan.FromMilliseconds(TimeoutMs));
}

public sealed class PresetRow
{
    public string DeviceConfigId { get; set; } = "";
    public int H2DeviceId { get; set; }
    public int ScreenId { get; set; }
    public int FriendlyPresetNumber { get; set; }
    public int PresetId { get; set; }
    public string DisplayName { get; set; } = "";
    public string Source { get; set; } = "";
}

public sealed class LayoutRow
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int? DefaultStartX { get; set; }
    public int? DefaultStartY { get; set; }

    public static LayoutRow FromModel(CursorLayout layout) => new()
    {
        Id = layout.Id,
        Name = layout.Name,
        DefaultStartX = layout.DefaultStartPosition?.X,
        DefaultStartY = layout.DefaultStartPosition?.Y
    };
}

public sealed class ZoneRow
{
    public string LayoutId { get; set; } = "";
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int WindowsLeft { get; set; }
    public int WindowsTop { get; set; }
    public int WindowsRight { get; set; }
    public int WindowsBottom { get; set; }
    public double VisualLeft { get; set; }
    public double VisualTop { get; set; }
    public double VisualRight { get; set; }
    public double VisualBottom { get; set; }
    public bool IsVisible { get; set; } = true;

    public static ZoneRow FromModel(string layoutId, CursorZone zone) => new()
    {
        LayoutId = layoutId,
        Id = zone.Id,
        DisplayName = zone.DisplayName,
        WindowsLeft = zone.WindowsRect.Left,
        WindowsTop = zone.WindowsRect.Top,
        WindowsRight = zone.WindowsRect.Right,
        WindowsBottom = zone.WindowsRect.Bottom,
        VisualLeft = zone.VisualRect.Left,
        VisualTop = zone.VisualRect.Top,
        VisualRight = zone.VisualRect.Right,
        VisualBottom = zone.VisualRect.Bottom,
        IsVisible = zone.IsVisible
    };

    public CursorZone ToModel() => new(
        Id,
        DisplayName,
        new IntRect(WindowsLeft, WindowsTop, WindowsRight, WindowsBottom),
        new VisualRect(VisualLeft, VisualTop, VisualRight, VisualBottom),
        IsVisible);
}

public sealed class PortalRow
{
    public string LayoutId { get; set; } = "";
    public string FromZoneId { get; set; } = "";
    public Edge FromEdge { get; set; } = Edge.Right;
    public double FromStartRatio { get; set; }
    public double FromEndRatio { get; set; } = 1.0;
    public string ToZoneId { get; set; } = "";
    public Edge ToEdge { get; set; } = Edge.Left;
    public double ToStartRatio { get; set; }
    public double ToEndRatio { get; set; } = 1.0;

    public static PortalRow FromModel(string layoutId, CursorPortal portal) => new()
    {
        LayoutId = layoutId,
        FromZoneId = portal.FromZoneId,
        FromEdge = portal.FromEdge,
        FromStartRatio = portal.FromRange.StartRatio,
        FromEndRatio = portal.FromRange.EndRatio,
        ToZoneId = portal.ToZoneId,
        ToEdge = portal.ToEdge,
        ToStartRatio = portal.ToRange.StartRatio,
        ToEndRatio = portal.ToRange.EndRatio
    };

    public CursorPortal ToModel() => new(
        FromZoneId,
        FromEdge,
        new EdgeRange(FromStartRatio, FromEndRatio),
        ToZoneId,
        ToEdge,
        new EdgeRange(ToStartRatio, ToEndRatio));
}

public sealed class ProfileRow
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Hotkey { get; set; }
    public string? DeviceId { get; set; }
    public int? ScreenId { get; set; }
    public int? PresetId { get; set; }
    public string? PresetDisplayName { get; set; }
    public string? CursorLayoutId { get; set; }
    public int? StartX { get; set; }
    public int? StartY { get; set; }
    public int PostAckDelayMs { get; set; } = 500;
    public bool RequireH2AckBeforeCursorLayout { get; set; } = true;

    public static ProfileRow FromModel(ExecutionProfile profile) => new()
    {
        Id = profile.Id,
        Name = profile.Name,
        Hotkey = profile.Hotkey,
        DeviceId = profile.H2Preset?.DeviceId,
        ScreenId = profile.H2Preset?.ScreenId,
        PresetId = profile.H2Preset?.PresetId,
        PresetDisplayName = profile.H2Preset?.DisplayName,
        CursorLayoutId = profile.CursorLayoutId,
        StartX = profile.StartPosition?.X,
        StartY = profile.StartPosition?.Y,
        PostAckDelayMs = profile.PostAckDelayMs,
        RequireH2AckBeforeCursorLayout = profile.RequireH2AckBeforeCursorLayout
    };

    public ExecutionProfile ToModel()
    {
        H2PresetRef? preset = null;
        if (!string.IsNullOrWhiteSpace(DeviceId) && ScreenId is not null && PresetId is not null)
        {
            preset = new H2PresetRef(DeviceId, ScreenId.Value, PresetId.Value, PresetDisplayName);
        }

        var start = StartX is not null && StartY is not null
            ? new CursorPoint(StartX.Value, StartY.Value)
            : (CursorPoint?)null;

        return new ExecutionProfile(
            Id,
            Name,
            Hotkey,
            preset,
            string.IsNullOrWhiteSpace(CursorLayoutId) ? null : CursorLayoutId,
            start,
            PostAckDelayMs,
            RequireH2AckBeforeCursorLayout);
    }
}

public sealed record MonitorRow(
    string DeviceName,
    int Left,
    int Top,
    int Right,
    int Bottom,
    bool IsPrimary)
{
    public static MonitorRow FromModel(MonitorInfo monitor) => new(
        monitor.DeviceName,
        monitor.Bounds.Left,
        monitor.Bounds.Top,
        monitor.Bounds.Right,
        monitor.Bounds.Bottom,
        monitor.IsPrimary);
}
