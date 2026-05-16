namespace H2CursorRouter.App.ViewModels;

public sealed record PresetRow(
    string DeviceId,
    int ScreenId,
    int FriendlyPresetNumber,
    int PresetId,
    string? DisplayName);

public sealed record MonitorRow(
    string DeviceName,
    int Left,
    int Top,
    int Right,
    int Bottom,
    bool IsPrimary);
