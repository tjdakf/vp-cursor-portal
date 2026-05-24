using H2CursorRouter.App.ViewModels;
using H2CursorRouter.Core.Geometry;

namespace H2CursorRouter.App.Services;

internal sealed class LayoutEditingService
{
    public const double MinimumVisualSize = 120;
    public const double GridSize = 2;

    private const double EdgeSnapTolerance = 24;
    private const double SameLineSnapTolerance = 40;

    private enum SnapDirection
    {
        Left,
        Right,
        Top,
        Bottom
    }

    public void MoveZoneVisual(ZoneRow zone, IReadOnlyCollection<ZoneRow> zones, double deltaX, double deltaY)
    {
        var width = zone.VisualWidth;
        var height = zone.VisualHeight;
        var left = SnapHorizontal(zone, zones, zone.VisualLeft + deltaX, width);
        var top = SnapVertical(zone, zones, zone.VisualTop + deltaY, height);
        zone.VisualLeft = left;
        zone.VisualRight = left + width;
        zone.VisualTop = top;
        zone.VisualBottom = top + height;
    }

    public void ResizeZoneVisual(ZoneRow zone, IReadOnlyCollection<ZoneRow> zones, double deltaWidth, double deltaHeight)
    {
        var right = SnapHorizontalEdge(zone, zones, zone.VisualRight + deltaWidth);
        var bottom = SnapVerticalEdge(zone, zones, zone.VisualBottom + deltaHeight);
        zone.VisualWidth = Math.Max(MinimumVisualSize, right - zone.VisualLeft);
        zone.VisualHeight = Math.Max(MinimumVisualSize, bottom - zone.VisualTop);
    }

    public void AttachAllDraftZonesToNearest(IReadOnlyCollection<ZoneRow> zones)
    {
        foreach (var zone in zones.ToArray())
        {
            AttachZoneToNearest(zone, zones);
        }
    }

    public void AttachZoneToNearest(ZoneRow zone, IReadOnlyCollection<ZoneRow> zones)
    {
        if (zones.Count < 2)
        {
            return;
        }

        var width = zone.VisualWidth;
        var height = zone.VisualHeight;
        var target = zones
            .Where(other => !ReferenceEquals(other, zone))
            .OrderBy(other => RectGapScore(zone, other))
            .ThenBy(other => CenterDistanceScore(zone, other))
            .FirstOrDefault();
        if (target is null)
        {
            return;
        }

        var direction = DetermineSnapDirection(zone, target);
        var snappedLeft = zone.VisualLeft;
        var snappedTop = zone.VisualTop;

        switch (direction)
        {
            case SnapDirection.Right:
                snappedLeft = target.VisualRight;
                snappedTop = AlignStartForOverlap(zone.VisualTop, height, target.VisualTop, target.VisualBottom);
                break;
            case SnapDirection.Left:
                snappedLeft = target.VisualLeft - width;
                snappedTop = AlignStartForOverlap(zone.VisualTop, height, target.VisualTop, target.VisualBottom);
                break;
            case SnapDirection.Bottom:
                snappedLeft = AlignStartForOverlap(zone.VisualLeft, width, target.VisualLeft, target.VisualRight);
                snappedTop = target.VisualBottom;
                break;
            case SnapDirection.Top:
                snappedLeft = AlignStartForOverlap(zone.VisualLeft, width, target.VisualLeft, target.VisualRight);
                snappedTop = target.VisualTop - height;
                break;
        }

        zone.VisualLeft = SnapToGrid(snappedLeft);
        zone.VisualRight = zone.VisualLeft + width;
        zone.VisualTop = SnapToGrid(snappedTop);
        zone.VisualBottom = zone.VisualTop + height;
    }

    public void NormalizeVisualOrigin(IEnumerable<ZoneRow> sourceZones)
    {
        var zones = sourceZones.Where(zone => zone.IsVisible).ToArray();
        if (zones.Length == 0)
        {
            return;
        }

        var minLeft = zones.Min(zone => zone.VisualLeft);
        var minTop = zones.Min(zone => zone.VisualTop);
        foreach (var zone in zones)
        {
            var width = SnapSize(zone.VisualWidth);
            var height = SnapSize(zone.VisualHeight);
            var left = SnapToGrid(zone.VisualLeft - minLeft);
            var top = SnapToGrid(zone.VisualTop - minTop);
            zone.VisualLeft = left;
            zone.VisualTop = top;
            zone.VisualRight = left + width;
            zone.VisualBottom = top + height;
        }
    }

    public IReadOnlyList<PortalRow> GeneratePortalsFromVisualAdjacency(IEnumerable<ZoneRow> sourceZones)
    {
        var zones = sourceZones
            .Where(zone => zone.IsVisible)
            .ToArray();
        var generated = new List<PortalRow>();
        const double tolerance = 2.0;

        for (var i = 0; i < zones.Length; i++)
        {
            for (var j = i + 1; j < zones.Length; j++)
            {
                var a = zones[i];
                var b = zones[j];
                AddVerticalAdjacency(a, b, tolerance, generated);
                AddVerticalAdjacency(b, a, tolerance, generated);
                AddHorizontalAdjacency(a, b, tolerance, generated);
                AddHorizontalAdjacency(b, a, tolerance, generated);
            }
        }

        return generated;
    }

    private double SnapHorizontal(ZoneRow zone, IEnumerable<ZoneRow> zones, double proposedLeft, double width)
    {
        var candidates = zones
            .Where(other => !ReferenceEquals(other, zone))
            .SelectMany(other => new[] { other.VisualLeft, other.VisualRight, other.VisualLeft - width, other.VisualRight - width });
        return SnapToNearest(proposedLeft, candidates);
    }

    private double SnapVertical(ZoneRow zone, IEnumerable<ZoneRow> zones, double proposedTop, double height)
    {
        var candidates = zones
            .Where(other => !ReferenceEquals(other, zone))
            .SelectMany(other => new[] { other.VisualTop, other.VisualBottom, other.VisualTop - height, other.VisualBottom - height });
        return SnapToNearest(proposedTop, candidates);
    }

    private double SnapHorizontalEdge(ZoneRow zone, IEnumerable<ZoneRow> zones, double proposedRight)
    {
        var candidates = zones
            .Where(other => !ReferenceEquals(other, zone))
            .SelectMany(other => new[] { other.VisualLeft, other.VisualRight });
        return Math.Max(zone.VisualLeft + MinimumVisualSize, SnapToNearest(proposedRight, candidates));
    }

    private double SnapVerticalEdge(ZoneRow zone, IEnumerable<ZoneRow> zones, double proposedBottom)
    {
        var candidates = zones
            .Where(other => !ReferenceEquals(other, zone))
            .SelectMany(other => new[] { other.VisualTop, other.VisualBottom });
        return Math.Max(zone.VisualTop + MinimumVisualSize, SnapToNearest(proposedBottom, candidates));
    }

    private double SnapToNearest(double value, IEnumerable<double> candidates)
    {
        var snapped = SnapToGrid(value);
        foreach (var candidate in candidates)
        {
            if (Math.Abs(value - candidate) < Math.Abs(value - snapped) && Math.Abs(value - candidate) <= EdgeSnapTolerance)
            {
                snapped = candidate;
            }
        }

        return snapped;
    }

    private static double SnapToGrid(double value) =>
        Math.Round(value / GridSize, MidpointRounding.AwayFromZero) * GridSize;

    private static double SnapSize(double value) =>
        Math.Max(MinimumVisualSize, SnapToGrid(value));

    private static double AxisGap(double firstStart, double firstEnd, double secondStart, double secondEnd)
    {
        if (firstEnd >= secondStart && secondEnd >= firstStart)
        {
            return 0;
        }

        return firstEnd < secondStart
            ? secondStart - firstEnd
            : firstStart - secondEnd;
    }

    private static double RectGapScore(ZoneRow first, ZoneRow second)
    {
        var horizontalGap = AxisGap(first.VisualLeft, first.VisualRight, second.VisualLeft, second.VisualRight);
        var verticalGap = AxisGap(first.VisualTop, first.VisualBottom, second.VisualTop, second.VisualBottom);
        return horizontalGap * horizontalGap + verticalGap * verticalGap;
    }

    private static double CenterDistanceScore(ZoneRow first, ZoneRow second)
    {
        var dx = CenterX(first) - CenterX(second);
        var dy = CenterY(first) - CenterY(second);
        return dx * dx + dy * dy;
    }

    private static SnapDirection DetermineSnapDirection(ZoneRow zone, ZoneRow target)
    {
        var dx = CenterX(zone) - CenterX(target);
        var dy = CenterY(zone) - CenterY(target);
        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            return dx >= 0 ? SnapDirection.Right : SnapDirection.Left;
        }

        return dy >= 0 ? SnapDirection.Bottom : SnapDirection.Top;
    }

    private static double CenterX(ZoneRow zone) =>
        zone.VisualLeft + zone.VisualWidth / 2;

    private static double CenterY(ZoneRow zone) =>
        zone.VisualTop + zone.VisualHeight / 2;

    private static double AlignStartForOverlap(double currentStart, double size, double targetStart, double targetEnd)
    {
        var currentEnd = currentStart + size;
        if (Math.Abs(currentStart - targetStart) <= SameLineSnapTolerance)
        {
            return targetStart;
        }

        if (Math.Abs(currentEnd - targetEnd) <= SameLineSnapTolerance)
        {
            return targetEnd - size;
        }

        if (currentEnd <= targetStart)
        {
            return targetStart;
        }

        if (currentStart >= targetEnd)
        {
            return targetEnd - size;
        }

        return currentStart;
    }

    private static void AddVerticalAdjacency(ZoneRow left, ZoneRow right, double tolerance, ICollection<PortalRow> portals)
    {
        if (Math.Abs(left.VisualRight - right.VisualLeft) > tolerance)
        {
            return;
        }

        var overlapTop = Math.Max(left.VisualTop, right.VisualTop);
        var overlapBottom = Math.Min(left.VisualBottom, right.VisualBottom);
        if (overlapBottom <= overlapTop)
        {
            return;
        }

        portals.Add(new PortalRow
        {
            LayoutId = left.LayoutId,
            FromZoneId = left.Id,
            FromEdge = Edge.Right,
            FromStartRatio = Ratio(overlapTop, left.VisualTop, left.VisualBottom),
            FromEndRatio = Ratio(overlapBottom, left.VisualTop, left.VisualBottom),
            ToZoneId = right.Id,
            ToEdge = Edge.Left,
            ToStartRatio = Ratio(overlapTop, right.VisualTop, right.VisualBottom),
            ToEndRatio = Ratio(overlapBottom, right.VisualTop, right.VisualBottom)
        });
        portals.Add(new PortalRow
        {
            LayoutId = left.LayoutId,
            FromZoneId = right.Id,
            FromEdge = Edge.Left,
            FromStartRatio = Ratio(overlapTop, right.VisualTop, right.VisualBottom),
            FromEndRatio = Ratio(overlapBottom, right.VisualTop, right.VisualBottom),
            ToZoneId = left.Id,
            ToEdge = Edge.Right,
            ToStartRatio = Ratio(overlapTop, left.VisualTop, left.VisualBottom),
            ToEndRatio = Ratio(overlapBottom, left.VisualTop, left.VisualBottom)
        });
    }

    private static void AddHorizontalAdjacency(ZoneRow top, ZoneRow bottom, double tolerance, ICollection<PortalRow> portals)
    {
        if (Math.Abs(top.VisualBottom - bottom.VisualTop) > tolerance)
        {
            return;
        }

        var overlapLeft = Math.Max(top.VisualLeft, bottom.VisualLeft);
        var overlapRight = Math.Min(top.VisualRight, bottom.VisualRight);
        if (overlapRight <= overlapLeft)
        {
            return;
        }

        portals.Add(new PortalRow
        {
            LayoutId = top.LayoutId,
            FromZoneId = top.Id,
            FromEdge = Edge.Bottom,
            FromStartRatio = Ratio(overlapLeft, top.VisualLeft, top.VisualRight),
            FromEndRatio = Ratio(overlapRight, top.VisualLeft, top.VisualRight),
            ToZoneId = bottom.Id,
            ToEdge = Edge.Top,
            ToStartRatio = Ratio(overlapLeft, bottom.VisualLeft, bottom.VisualRight),
            ToEndRatio = Ratio(overlapRight, bottom.VisualLeft, bottom.VisualRight)
        });
        portals.Add(new PortalRow
        {
            LayoutId = top.LayoutId,
            FromZoneId = bottom.Id,
            FromEdge = Edge.Top,
            FromStartRatio = Ratio(overlapLeft, bottom.VisualLeft, bottom.VisualRight),
            FromEndRatio = Ratio(overlapRight, bottom.VisualLeft, bottom.VisualRight),
            ToZoneId = top.Id,
            ToEdge = Edge.Bottom,
            ToStartRatio = Ratio(overlapLeft, top.VisualLeft, top.VisualRight),
            ToEndRatio = Ratio(overlapRight, top.VisualLeft, top.VisualRight)
        });
    }

    private static double Ratio(double value, double start, double end)
    {
        var length = end - start;
        if (length <= 0)
        {
            return 0;
        }

        var ratio = (value - start) / length;
        return Math.Clamp(ratio, 0, 1);
    }
}
