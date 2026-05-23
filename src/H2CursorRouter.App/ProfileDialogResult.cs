namespace H2CursorRouter.App;

public sealed record ProfileDialogResult(
    string Name,
    string? Hotkey,
    string? CursorLayoutId,
    string? DeviceId = null,
    int? ScreenId = null,
    int? PresetId = null,
    string? PresetDisplayName = null);
