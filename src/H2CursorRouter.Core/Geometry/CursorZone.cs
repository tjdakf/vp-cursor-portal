namespace H2CursorRouter.Core.Geometry;

public sealed record CursorZone(
    string Id,
    string DisplayName,
    IntRect WindowsRect,
    VisualRect VisualRect,
    bool IsVisible);
