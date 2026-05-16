namespace H2CursorRouter.Core.Geometry;

public sealed record CursorPortal(
    string FromZoneId,
    Edge FromEdge,
    EdgeRange FromRange,
    string ToZoneId,
    Edge ToEdge,
    EdgeRange ToRange);
