using H2CursorRouter.Core.Domain;
using H2CursorRouter.Core.Geometry;

namespace H2CursorRouter.Core.Profiles;

public sealed record ExecutionProfile(
    string Id,
    string Name,
    string? Hotkey,
    H2PresetRef? H2Preset,
    string? CursorLayoutId,
    CursorPoint? StartPosition,
    int PostAckDelayMs,
    bool RequireH2AckBeforeCursorLayout);
