namespace H2CursorRouter.Core.Domain;

public sealed record H2DeviceConfig(
    string Id,
    string Name,
    string Host,
    int Port,
    int DeviceId,
    TimeSpan Timeout)
{
    public const int DefaultPort = 6000;
}
