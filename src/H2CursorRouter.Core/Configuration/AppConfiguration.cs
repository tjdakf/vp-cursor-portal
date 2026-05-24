using H2CursorRouter.Core.Domain;
using H2CursorRouter.Core.Geometry;
using H2CursorRouter.Core.Profiles;

namespace H2CursorRouter.Core.Configuration;

public sealed record AppConfiguration(
    IReadOnlyList<H2DeviceConfig> Devices,
    IReadOnlyList<CursorLayout> CursorLayouts,
    IReadOnlyList<ExecutionProfile> Profiles,
    SafetySettings Safety,
    IReadOnlyList<CachedH2Preset>? CachedPresets = null)
{
    public IReadOnlyList<CachedH2Preset> PresetCache => CachedPresets ?? Array.Empty<CachedH2Preset>();
}

public sealed record CachedH2Preset(
    string DeviceConfigId,
    string DeviceName,
    int H2DeviceId,
    int ScreenId,
    int FriendlyPresetNumber,
    int PresetId,
    string DisplayName,
    DateTimeOffset? LastFetchedAtUtc);
