using H2CursorRouter.Core.Validation;

namespace H2CursorRouter.Core.Geometry;

public sealed class CursorRoutingEngine
{
    private readonly CursorLayoutValidator _validator = new();

    public RoutingDecision Evaluate(
        CursorLayout layout,
        CursorPoint previousPosition,
        CursorPoint currentPosition,
        CursorPoint lastValidPosition)
    {
        var validation = _validator.Validate(layout);
        if (!validation.IsValid)
        {
            return RoutingDecision.RejectUnsafeLayout(string.Join("; ", validation.Errors));
        }

        var previousZone = FindZone(layout, previousPosition);
        if (previousZone is not null)
        {
            var portalDecision = TryMapPortal(layout, previousZone, previousPosition, currentPosition);
            if (portalDecision is not null)
            {
                return portalDecision;
            }
        }

        var currentZone = FindZone(layout, currentPosition);
        if (currentZone is null)
        {
            return RoutingDecision.RevertToLastValid(lastValidPosition, "Cursor is outside every known zone.");
        }

        if (!currentZone.IsVisible)
        {
            return RoutingDecision.RevertToLastValid(lastValidPosition, $"Cursor entered hidden zone '{currentZone.Id}'.");
        }

        return RoutingDecision.KeepCurrent();
    }

    public CursorPoint ResolveStartPosition(CursorLayout layout, CursorPoint? profileStartPosition)
    {
        if (profileStartPosition is not null)
        {
            return profileStartPosition.Value;
        }

        if (layout.DefaultStartPosition is not null)
        {
            return layout.DefaultStartPosition.Value;
        }

        var firstVisibleZone = layout.Zones.FirstOrDefault(zone => zone.IsVisible)
            ?? throw new InvalidOperationException("Cannot resolve start position for a layout with no visible zones.");
        return firstVisibleZone.WindowsRect.Center();
    }

    public CursorZone? FindZone(CursorLayout layout, CursorPoint point) =>
        layout.Zones.FirstOrDefault(zone => zone.WindowsRect.Contains(point));

    private RoutingDecision? TryMapPortal(
        CursorLayout layout,
        CursorZone previousZone,
        CursorPoint previousPosition,
        CursorPoint currentPosition)
    {
        foreach (var portal in layout.Portals.Where(portal =>
            string.Equals(portal.FromZoneId, previousZone.Id, StringComparison.OrdinalIgnoreCase)))
        {
            if (!TryGetCrossingRatio(previousZone, portal.FromEdge, previousPosition, currentPosition, out var sourceRatio))
            {
                continue;
            }

            if (!portal.FromRange.Contains(sourceRatio))
            {
                continue;
            }

            var targetZone = layout.Zones.First(zone =>
                string.Equals(zone.Id, portal.ToZoneId, StringComparison.OrdinalIgnoreCase));
            var targetRatio = MapRange(sourceRatio, portal.FromRange, portal.ToRange);
            var target = MapTargetPoint(targetZone.WindowsRect, portal.ToEdge, targetRatio);
            return RoutingDecision.MoveToTarget(target, $"Portal mapped '{portal.FromZoneId}' {portal.FromEdge} to '{portal.ToZoneId}' {portal.ToEdge}.");
        }

        return null;
    }

    private static bool TryGetCrossingRatio(
        CursorZone zone,
        Edge edge,
        CursorPoint previousPosition,
        CursorPoint currentPosition,
        out double ratio)
    {
        ratio = 0;

        return edge switch
        {
            Edge.Right when previousPosition.X < zone.WindowsRect.Right && currentPosition.X >= zone.WindowsRect.Right =>
                TryInterpolateRatio(
                    previousPosition.X,
                    previousPosition.Y,
                    currentPosition.X,
                    currentPosition.Y,
                    zone.WindowsRect.Right,
                    zone.WindowsRect.Top,
                    zone.WindowsRect.Height,
                    out ratio),
            Edge.Left when previousPosition.X >= zone.WindowsRect.Left && currentPosition.X < zone.WindowsRect.Left =>
                TryInterpolateRatio(
                    previousPosition.X,
                    previousPosition.Y,
                    currentPosition.X,
                    currentPosition.Y,
                    zone.WindowsRect.Left,
                    zone.WindowsRect.Top,
                    zone.WindowsRect.Height,
                    out ratio),
            Edge.Bottom when previousPosition.Y < zone.WindowsRect.Bottom && currentPosition.Y >= zone.WindowsRect.Bottom =>
                TryInterpolateRatio(
                    previousPosition.Y,
                    previousPosition.X,
                    currentPosition.Y,
                    currentPosition.X,
                    zone.WindowsRect.Bottom,
                    zone.WindowsRect.Left,
                    zone.WindowsRect.Width,
                    out ratio),
            Edge.Top when previousPosition.Y >= zone.WindowsRect.Top && currentPosition.Y < zone.WindowsRect.Top =>
                TryInterpolateRatio(
                    previousPosition.Y,
                    previousPosition.X,
                    currentPosition.Y,
                    currentPosition.X,
                    zone.WindowsRect.Top,
                    zone.WindowsRect.Left,
                    zone.WindowsRect.Width,
                    out ratio),
            _ => false
        };
    }

    private static bool TryInterpolateRatio(
        int previousPrimary,
        int previousSecondary,
        int currentPrimary,
        int currentSecondary,
        int boundary,
        int secondaryStart,
        int secondaryLength,
        out double ratio)
    {
        ratio = 0;
        var primaryDelta = currentPrimary - previousPrimary;
        if (primaryDelta == 0 || secondaryLength <= 0)
        {
            return false;
        }

        var t = (boundary - previousPrimary) / (double)primaryDelta;
        if (t < 0.0 || t > 1.0)
        {
            return false;
        }

        var secondaryAtBoundary = previousSecondary + (currentSecondary - previousSecondary) * t;
        ratio = Clamp01((secondaryAtBoundary - secondaryStart) / secondaryLength);
        return true;
    }

    private static double MapRange(double sourceRatio, EdgeRange fromRange, EdgeRange toRange)
    {
        var normalized = (sourceRatio - fromRange.StartRatio) / (fromRange.EndRatio - fromRange.StartRatio);
        return toRange.StartRatio + normalized * (toRange.EndRatio - toRange.StartRatio);
    }

    private static CursorPoint MapTargetPoint(IntRect targetRect, Edge targetEdge, double targetRatio)
    {
        targetRatio = Clamp01(targetRatio);
        return targetEdge switch
        {
            Edge.Left => new CursorPoint(targetRect.Left, RatioToCoordinate(targetRect.Top, targetRect.Height, targetRatio)),
            Edge.Right => new CursorPoint(targetRect.Right - 1, RatioToCoordinate(targetRect.Top, targetRect.Height, targetRatio)),
            Edge.Top => new CursorPoint(RatioToCoordinate(targetRect.Left, targetRect.Width, targetRatio), targetRect.Top),
            Edge.Bottom => new CursorPoint(RatioToCoordinate(targetRect.Left, targetRect.Width, targetRatio), targetRect.Bottom - 1),
            _ => throw new ArgumentOutOfRangeException(nameof(targetEdge), targetEdge, null)
        };
    }

    private static int RatioToCoordinate(int start, int length, double ratio)
    {
        if (ratio >= 1.0)
        {
            return start + length - 1;
        }

        return start + (int)Math.Round(length * ratio, MidpointRounding.AwayFromZero);
    }

    private static double Clamp01(double value) =>
        value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;
}
