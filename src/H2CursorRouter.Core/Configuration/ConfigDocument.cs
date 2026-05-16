using H2CursorRouter.Core.Domain;
using H2CursorRouter.Core.Geometry;
using H2CursorRouter.Core.Profiles;

namespace H2CursorRouter.Core.Configuration;

public sealed record ConfigDocument(
    IReadOnlyList<ConfigDevice> Devices,
    IReadOnlyList<CursorLayout> CursorLayouts,
    IReadOnlyList<ExecutionProfile> Profiles,
    SafetySettings Safety)
{
    public AppConfiguration ToRuntime() => new(
        Devices.Select(device => device.ToRuntime()).ToArray(),
        CursorLayouts,
        Profiles,
        Safety);

    public static ConfigDocument FromRuntime(AppConfiguration configuration) => new(
        configuration.Devices.Select(ConfigDevice.FromRuntime).ToArray(),
        configuration.CursorLayouts,
        configuration.Profiles,
        configuration.Safety);
}

public sealed record ConfigDevice(
    string Id,
    string Name,
    string Host,
    int Port,
    int DeviceId,
    int TimeoutMs)
{
    public H2DeviceConfig ToRuntime() => new(
        Id,
        Name,
        Host,
        Port <= 0 ? H2DeviceConfig.DefaultPort : Port,
        DeviceId,
        TimeSpan.FromMilliseconds(TimeoutMs));

    public static ConfigDevice FromRuntime(H2DeviceConfig device) => new(
        device.Id,
        device.Name,
        device.Host,
        device.Port,
        device.DeviceId,
        (int)device.Timeout.TotalMilliseconds);
}
