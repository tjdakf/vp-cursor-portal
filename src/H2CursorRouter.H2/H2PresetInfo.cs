namespace H2CursorRouter.H2;

public sealed record H2PresetInfo(
    int DeviceId,
    int ScreenId,
    int PresetId,
    string? Name)
{
    public int FriendlyPresetNumber => PresetId + 1;
    public string DisplayName => string.IsNullOrWhiteSpace(Name)
        ? $"Preset {FriendlyPresetNumber} / presetId {PresetId}"
        : $"{Name} / Preset {FriendlyPresetNumber} / presetId {PresetId}";
}
