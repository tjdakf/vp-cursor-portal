namespace H2CursorRouter.Core.Geometry;

public sealed record CursorLayout(
    string Id,
    string Name,
    IReadOnlyList<CursorZone> Zones,
    IReadOnlyList<CursorPortal> Portals,
    CursorPoint? DefaultStartPosition = null,
    string? Description = null);
