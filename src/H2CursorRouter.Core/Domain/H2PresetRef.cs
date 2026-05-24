namespace H2CursorRouter.Core.Domain;

public sealed record H2PresetRef(
    string DeviceId,
    int ScreenId,
    int PresetId,
    string? DisplayName);
