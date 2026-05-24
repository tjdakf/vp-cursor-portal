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

    public void MoveZoneVisual(IVisualLayoutZone zone, IEnumerable<IVisualLayoutZone> zones, double deltaX, double deltaY)
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

    public void ResizeZoneVisual(IVisualLayoutZone zone, IEnumerable<IVisualLayoutZone> zones, double deltaWidth, double deltaHeight)
    {
        var right = SnapHorizontalEdge(zone, zones, zone.VisualRight + deltaWidth);
        var bottom = SnapVerticalEdge(zone, zones, zone.VisualBottom + deltaHeight);
        zone.VisualWidth = Math.Max(MinimumVisualSize, right - zone.VisualLeft);
        zone.VisualHeight = Math.Max(MinimumVisualSize, bottom - zone.VisualTop);
    }

    public void AttachAllDraftZonesToNearest(IEnumerable<IVisualLayoutZone> sourceZones)
    {
        var zones = sourceZones.ToArray();
        foreach (var zone in zones)
        {
            AttachZoneToNearest(zone, zones);
        }
    }

    public void AttachZoneToNearest(IVisualLayoutZone zone, IEnumerable<IVisualLayoutZone> sourceZones)
    {
        var zones = sourceZones.ToArray();
        if (zones.Length < 2)
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

    public void NormalizeVisualOrigin(IEnumerable<IVisualLayoutZone> sourceZones)
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

    public IReadOnlyList<GeneratedPortal> GeneratePortalsFromVisualAdjacency(IEnumerable<IVisualLayoutZone> sourceZones)
    {
        var zones = sourceZones
            .Where(zone => zone.IsVisible)
            .ToArray();
        var generated = new List<GeneratedPortal>();
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

    private double SnapHorizontal(IVisualLayoutZone zone, IEnumerable<IVisualLayoutZone> zones, double proposedLeft, double width)
    {
        var candidates = zones
            .Where(other => !ReferenceEquals(other, zone))
            .SelectMany(other => new[] { other.VisualLeft, other.VisualRight, other.VisualLeft - width, other.VisualRight - width });
        return SnapToNearest(proposedLeft, candidates);
    }

    private double SnapVertical(IVisualLayoutZone zone, IEnumerable<IVisualLayoutZone> zones, double proposedTop, double height)
    {
        var candidates = zones
            .Where(other => !ReferenceEquals(other, zone))
            .SelectMany(other => new[] { other.VisualTop, other.VisualBottom, other.VisualTop - height, other.VisualBottom - height });
        return SnapToNearest(proposedTop, candidates);
    }

    private double SnapHorizontalEdge(IVisualLayoutZone zone, IEnumerable<IVisualLayoutZone> zones, double proposedRight)
    {
        var candidates = zones
            .Where(other => !ReferenceEquals(other, zone))
            .SelectMany(other => new[] { other.VisualLeft, other.VisualRight });
        return Math.Max(zone.VisualLeft + MinimumVisualSize, SnapToNearest(proposedRight, candidates));
    }

    private double SnapVerticalEdge(IVisualLayoutZone zone, IEnumerable<IVisualLayoutZone> zones, double proposedBottom)
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

    private static double RectGapScore(IVisualLayoutZone first, IVisualLayoutZone second)
    {
        var horizontalGap = AxisGap(first.VisualLeft, first.VisualRight, second.VisualLeft, second.VisualRight);
        var verticalGap = AxisGap(first.VisualTop, first.VisualBottom, second.VisualTop, second.VisualBottom);
        return horizontalGap * horizontalGap + verticalGap * verticalGap;
    }

    private static double CenterDistanceScore(IVisualLayoutZone first, IVisualLayoutZone second)
    {
        var dx = CenterX(first) - CenterX(second);
        var dy = CenterY(first) - CenterY(second);
        return dx * dx + dy * dy;
    }

    private static SnapDirection DetermineSnapDirection(IVisualLayoutZone zone, IVisualLayoutZone target)
    {
        var dx = CenterX(zone) - CenterX(target);
        var dy = CenterY(zone) - CenterY(target);
        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            return dx >= 0 ? SnapDirection.Right : SnapDirection.Left;
        }

        return dy >= 0 ? SnapDirection.Bottom : SnapDirection.Top;
    }

    private static double CenterX(IVisualLayoutZone zone) =>
        zone.VisualLeft + zone.VisualWidth / 2;

    private static double CenterY(IVisualLayoutZone zone) =>
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

    private static void AddVerticalAdjacency(IVisualLayoutZone left, IVisualLayoutZone right, double tolerance, ICollection<GeneratedPortal> portals)
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

        portals.Add(new GeneratedPortal(
            left.Id,
            Edge.Right,
            Ratio(overlapTop, left.VisualTop, left.VisualBottom),
            Ratio(overlapBottom, left.VisualTop, left.VisualBottom),
            right.Id,
            Edge.Left,
            Ratio(overlapTop, right.VisualTop, right.VisualBottom),
            Ratio(overlapBottom, right.VisualTop, right.VisualBottom)));
        portals.Add(new GeneratedPortal(
            right.Id,
            Edge.Left,
            Ratio(overlapTop, right.VisualTop, right.VisualBottom),
            Ratio(overlapBottom, right.VisualTop, right.VisualBottom),
            left.Id,
            Edge.Right,
            Ratio(overlapTop, left.VisualTop, left.VisualBottom),
            Ratio(overlapBottom, left.VisualTop, left.VisualBottom)));
    }

    private static void AddHorizontalAdjacency(IVisualLayoutZone top, IVisualLayoutZone bottom, double tolerance, ICollection<GeneratedPortal> portals)
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

        portals.Add(new GeneratedPortal(
            top.Id,
            Edge.Bottom,
            Ratio(overlapLeft, top.VisualLeft, top.VisualRight),
            Ratio(overlapRight, top.VisualLeft, top.VisualRight),
            bottom.Id,
            Edge.Top,
            Ratio(overlapLeft, bottom.VisualLeft, bottom.VisualRight),
            Ratio(overlapRight, bottom.VisualLeft, bottom.VisualRight)));
        portals.Add(new GeneratedPortal(
            bottom.Id,
            Edge.Top,
            Ratio(overlapLeft, bottom.VisualLeft, bottom.VisualRight),
            Ratio(overlapRight, bottom.VisualLeft, bottom.VisualRight),
            top.Id,
            Edge.Bottom,
            Ratio(overlapLeft, top.VisualLeft, top.VisualRight),
            Ratio(overlapRight, top.VisualLeft, top.VisualRight)));
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

public interface IVisualLayoutZone
{
    string Id { get; }
    bool IsVisible { get; }
    double VisualLeft { get; set; }
    double VisualTop { get; set; }
    double VisualRight { get; set; }
    double VisualBottom { get; set; }
    double VisualWidth { get; set; }
    double VisualHeight { get; set; }
}

public sealed record GeneratedPortal(
    string FromZoneId,
    Edge FromEdge,
    double FromStartRatio,
    double FromEndRatio,
    string ToZoneId,
    Edge ToEdge,
    double ToStartRatio,
    double ToEndRatio);
