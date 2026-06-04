using H2CursorRouter.App.Services;
using H2CursorRouter.Core.Geometry;
using Xunit;

namespace H2CursorRouter.App.Tests;

public sealed class LayoutEditingServiceTests
{
    [Fact]
    public void NormalizeVisualOriginMovesVisibleZonesToZeroOrigin()
    {
        var service = new LayoutEditingService();
        var zones = new[]
        {
            new TestZone("left", 100, 50, 300, 250),
            new TestZone("right", 300, 50, 500, 250),
            new TestZone("hidden", 900, 900, 1100, 1100) { IsVisible = false }
        };

        service.NormalizeVisualOrigin(zones);

        Assert.Equal(0, zones[0].VisualLeft);
        Assert.Equal(0, zones[0].VisualTop);
        Assert.Equal(200, zones[0].VisualRight);
        Assert.Equal(200, zones[0].VisualBottom);
        Assert.Equal(200, zones[1].VisualLeft);
        Assert.Equal(0, zones[1].VisualTop);
        Assert.Equal(400, zones[1].VisualRight);
        Assert.Equal(200, zones[1].VisualBottom);
        Assert.Equal(900, zones[2].VisualLeft);
        Assert.Equal(900, zones[2].VisualTop);
    }

    [Fact]
    public void GeneratePortalsCreatesBidirectionalVerticalAdjacency()
    {
        var service = new LayoutEditingService();
        var zones = new[]
        {
            new TestZone("left", 0, 0, 200, 400),
            new TestZone("right", 200, 100, 400, 300)
        };

        var portals = service.GeneratePortalsFromVisualAdjacency(zones);

        Assert.Equal(2, portals.Count);
        var forward = Assert.Single(portals, portal => portal.FromZoneId == "left");
        Assert.Equal(Edge.Right, forward.FromEdge);
        Assert.Equal(0.25, forward.FromStartRatio, precision: 3);
        Assert.Equal(0.75, forward.FromEndRatio, precision: 3);
        Assert.Equal("right", forward.ToZoneId);
        Assert.Equal(Edge.Left, forward.ToEdge);
        Assert.Equal(0, forward.ToStartRatio, precision: 3);
        Assert.Equal(1, forward.ToEndRatio, precision: 3);

        var reverse = Assert.Single(portals, portal => portal.FromZoneId == "right");
        Assert.Equal(Edge.Left, reverse.FromEdge);
        Assert.Equal("left", reverse.ToZoneId);
        Assert.Equal(Edge.Right, reverse.ToEdge);
    }

    [Fact]
    public void GeneratePortalsCreatesBidirectionalHorizontalAdjacency()
    {
        var service = new LayoutEditingService();
        var zones = new[]
        {
            new TestZone("top", 0, 0, 400, 200),
            new TestZone("bottom", 100, 200, 300, 400)
        };

        var portals = service.GeneratePortalsFromVisualAdjacency(zones);

        Assert.Equal(2, portals.Count);
        var forward = Assert.Single(portals, portal => portal.FromZoneId == "top");
        Assert.Equal(Edge.Bottom, forward.FromEdge);
        Assert.Equal(0.25, forward.FromStartRatio, precision: 3);
        Assert.Equal(0.75, forward.FromEndRatio, precision: 3);
        Assert.Equal("bottom", forward.ToZoneId);
        Assert.Equal(Edge.Top, forward.ToEdge);
    }

    [Fact]
    public void GeneratePortalsCreatesSegmentedRatiosForStackedRightZones()
    {
        var service = new LayoutEditingService();
        var zones = new[]
        {
            new TestZone("large", 0, 0, 200, 400),
            new TestZone("upper", 200, 0, 400, 200),
            new TestZone("lower", 200, 200, 400, 400)
        };

        var portals = service.GeneratePortalsFromVisualAdjacency(zones);

        var largeToUpper = Assert.Single(portals, portal => portal.FromZoneId == "large" && portal.ToZoneId == "upper");
        Assert.Equal(Edge.Right, largeToUpper.FromEdge);
        Assert.Equal(0, largeToUpper.FromStartRatio, precision: 3);
        Assert.Equal(0.5, largeToUpper.FromEndRatio, precision: 3);
        Assert.Equal(0, largeToUpper.ToStartRatio, precision: 3);
        Assert.Equal(1, largeToUpper.ToEndRatio, precision: 3);

        var largeToLower = Assert.Single(portals, portal => portal.FromZoneId == "large" && portal.ToZoneId == "lower");
        Assert.Equal(0.5, largeToLower.FromStartRatio, precision: 3);
        Assert.Equal(1, largeToLower.FromEndRatio, precision: 3);
    }

    [Fact]
    public void GeneratePortalsIgnoresHiddenZones()
    {
        var service = new LayoutEditingService();
        var zones = new[]
        {
            new TestZone("visible", 0, 0, 200, 200),
            new TestZone("hidden", 200, 0, 400, 200) { IsVisible = false }
        };

        var portals = service.GeneratePortalsFromVisualAdjacency(zones);

        Assert.Empty(portals);
    }

    [Fact]
    public void GeneratePortalsUsesToleranceForSmallGapsOnly()
    {
        var service = new LayoutEditingService();
        var withinTolerance = new[]
        {
            new TestZone("left", 0, 0, 200, 200),
            new TestZone("right", 202, 0, 402, 200)
        };
        var outsideTolerance = new[]
        {
            new TestZone("left", 0, 0, 200, 200),
            new TestZone("right", 203, 0, 403, 200)
        };

        Assert.NotEmpty(service.GeneratePortalsFromVisualAdjacency(withinTolerance));
        Assert.Empty(service.GeneratePortalsFromVisualAdjacency(outsideTolerance));
    }

    [Fact]
    public void AttachZoneToNearestClosesRowGapAtDropTime()
    {
        var service = new LayoutEditingService();
        var zones = new[]
        {
            new TestZone("display8", 0, 0, 1920, 1080),
            new TestZone("display1", 1940, 0, 3860, 1080)
        };

        service.AttachZoneToNearest(zones[1], zones);

        Assert.Equal(1920, zones[1].VisualLeft);
        Assert.Equal(3840, zones[1].VisualRight);
    }

    [Fact]
    public void GeneratePortalsCreatesPortalAfterDropTimeSnap()
    {
        var service = new LayoutEditingService();
        var zones = new[]
        {
            new TestZone("display5", 0, 0, 1920, 1080),
            new TestZone("display8", 1920, 0, 3840, 1080),
            new TestZone("display1", 3860, 0, 5780, 1080),
            new TestZone("display2", 5780, 0, 7700, 1080),
            new TestZone("display7", 7700, 0, 9620, 1080)
        };

        service.AttachZoneToNearest(zones[2], zones);
        var portals = service.GeneratePortalsFromVisualAdjacency(zones);

        Assert.Equal(3840, zones[2].VisualLeft);
        Assert.Equal(5760, zones[2].VisualRight);
        Assert.Contains(portals, portal =>
            portal.FromZoneId == "display8" &&
            portal.FromEdge == Edge.Right &&
            portal.ToZoneId == "display1" &&
            portal.ToEdge == Edge.Left);
    }

    [Fact]
    public void AttachZoneToNearestIgnoresDistantZones()
    {
        var service = new LayoutEditingService();
        var zone = new TestZone("moving", 240, 0, 340, 100);
        var target = new TestZone("target", 100, 0, 200, 100);

        service.AttachZoneToNearest(zone, [zone, target]);

        Assert.Equal(240, zone.VisualLeft);
        Assert.Equal(340, zone.VisualRight);
    }

    [Fact]
    public void GeneratePortalsDoesNotCreatePortalForCornerTouch()
    {
        var service = new LayoutEditingService();
        var zones = new[]
        {
            new TestZone("topLeft", 0, 0, 200, 200),
            new TestZone("bottomRight", 200, 200, 400, 400)
        };

        var portals = service.GeneratePortalsFromVisualAdjacency(zones);

        Assert.Empty(portals);
    }

    [Fact]
    public void MoveZoneVisualRoundsMovementToGrid()
    {
        var service = new LayoutEditingService();
        var zone = new TestZone("moving", 0, 0, 200, 200);
        var target = new TestZone("target", 220, 0, 420, 200);

        service.MoveZoneVisual(zone, [zone, target], deltaX: 19, deltaY: 3);

        Assert.Equal(20, zone.VisualLeft);
        Assert.Equal(220, zone.VisualRight);
        Assert.Equal(4, zone.VisualTop);
        Assert.Equal(204, zone.VisualBottom);
    }

    [Fact]
    public void ResizeZoneVisualHonorsMinimumSize()
    {
        var service = new LayoutEditingService();
        var zone = new TestZone("zone", 0, 0, 200, 200);

        service.ResizeZoneVisual(zone, [zone], deltaWidth: -500, deltaHeight: -500);

        Assert.Equal(LayoutEditingService.MinimumVisualSize, zone.VisualWidth);
        Assert.Equal(LayoutEditingService.MinimumVisualSize, zone.VisualHeight);
    }

    private sealed class TestZone : IVisualLayoutZone
    {
        public TestZone(string id, double left, double top, double right, double bottom)
        {
            Id = id;
            VisualLeft = left;
            VisualTop = top;
            VisualRight = right;
            VisualBottom = bottom;
        }

        public string Id { get; }
        public bool IsVisible { get; set; } = true;
        public double VisualLeft { get; set; }
        public double VisualTop { get; set; }
        public double VisualRight { get; set; }
        public double VisualBottom { get; set; }
        public double VisualWidth
        {
            get => VisualRight - VisualLeft;
            set => VisualRight = VisualLeft + value;
        }

        public double VisualHeight
        {
            get => VisualBottom - VisualTop;
            set => VisualBottom = VisualTop + value;
        }
    }
}
