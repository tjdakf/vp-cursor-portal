using H2CursorRouter.Core.Geometry;
using Xunit;

namespace H2CursorRouter.Core.Tests;

public sealed class CursorRoutingEngineTests
{
    private readonly CursorRoutingEngine _engine = new();

    [Fact]
    public void VisibleZoneDetectionKeepsCurrentPosition()
    {
        var layout = SingleVisibleZoneLayout();
        var decision = _engine.Evaluate(layout, new CursorPoint(10, 10), new CursorPoint(20, 20), new CursorPoint(10, 10));
        Assert.Equal(RoutingDecisionKind.KeepCurrent, decision.Kind);
    }

    [Fact]
    public void HiddenZoneRejectionRevertsToLastValidPosition()
    {
        var layout = new CursorLayout(
            "layout",
            "layout",
            [
                new CursorZone("visible", "Visible", new IntRect(0, 0, 100, 100), new VisualRect(0, 0, 100, 100), true),
                new CursorZone("hidden", "Hidden", new IntRect(100, 0, 200, 100), new VisualRect(100, 0, 200, 100), false)
            ],
            []);

        var lastValid = new CursorPoint(50, 50);
        var decision = _engine.Evaluate(layout, new CursorPoint(90, 50), new CursorPoint(120, 50), lastValid);

        Assert.Equal(RoutingDecisionKind.RevertToLastValid, decision.Kind);
        Assert.Equal(lastValid, decision.Target);
    }

    [Fact]
    public void OutsideZoneRejectionRevertsToLastValidPosition()
    {
        var layout = SingleVisibleZoneLayout();
        var lastValid = new CursorPoint(50, 50);
        var decision = _engine.Evaluate(layout, new CursorPoint(90, 50), new CursorPoint(300, 50), lastValid);

        Assert.Equal(RoutingDecisionKind.RevertToLastValid, decision.Kind);
        Assert.Equal(lastValid, decision.Target);
    }

    [Fact]
    public void FullEdgePortalMapsByRatio()
    {
        var layout = new CursorLayout(
            "layout",
            "layout",
            [
                new CursorZone("left", "Left", new IntRect(0, 0, 100, 100), new VisualRect(0, 0, 100, 100), true),
                new CursorZone("right", "Right", new IntRect(200, 0, 300, 100), new VisualRect(100, 0, 200, 100), true)
            ],
            [
                new CursorPortal("left", Edge.Right, new EdgeRange(0, 1), "right", Edge.Left, new EdgeRange(0, 1))
            ]);

        var decision = _engine.Evaluate(layout, new CursorPoint(99, 50), new CursorPoint(101, 50), new CursorPoint(99, 50));

        Assert.Equal(RoutingDecisionKind.MoveToTarget, decision.Kind);
        Assert.Equal(new CursorPoint(200, 50), decision.Target);
    }

    [Fact]
    public void PortalMapsWhenCursorContactsVirtualDesktopRightBoundary()
    {
        var layout = new CursorLayout(
            "layout",
            "layout",
            [
                new CursorZone("rightmost", "Rightmost", new IntRect(3840, 0, 5760, 1080), new VisualRect(0, 0, 1920, 1080), true),
                new CursorZone("leftmost", "Leftmost", new IntRect(0, 0, 1920, 1080), new VisualRect(1920, 0, 3840, 1080), true)
            ],
            [
                new CursorPortal("rightmost", Edge.Right, new EdgeRange(0, 1), "leftmost", Edge.Left, new EdgeRange(0, 1))
            ]);

        var decision = _engine.Evaluate(layout, new CursorPoint(5758, 540), new CursorPoint(5759, 540), new CursorPoint(5758, 540));

        Assert.Equal(RoutingDecisionKind.MoveToTarget, decision.Kind);
        Assert.Equal(new CursorPoint(0, 540), decision.Target);
    }

    [Fact]
    public void PortalMapsWhenCursorContactsVirtualDesktopLeftBoundary()
    {
        var layout = new CursorLayout(
            "layout",
            "layout",
            [
                new CursorZone("leftmost", "Leftmost", new IntRect(0, 0, 1920, 1080), new VisualRect(1920, 0, 3840, 1080), true),
                new CursorZone("rightmost", "Rightmost", new IntRect(3840, 0, 5760, 1080), new VisualRect(0, 0, 1920, 1080), true)
            ],
            [
                new CursorPortal("leftmost", Edge.Left, new EdgeRange(0, 1), "rightmost", Edge.Right, new EdgeRange(0, 1))
            ]);

        var decision = _engine.Evaluate(layout, new CursorPoint(1, 540), new CursorPoint(0, 540), new CursorPoint(1, 540));

        Assert.Equal(RoutingDecisionKind.MoveToTarget, decision.Kind);
        Assert.Equal(new CursorPoint(5759, 540), decision.Target);
    }

    [Fact]
    public void SegmentedPortalMapsWithinSelectedRange()
    {
        var layout = new CursorLayout(
            "layout",
            "layout",
            [
                new CursorZone("left", "Left", new IntRect(0, 0, 100, 200), new VisualRect(0, 0, 200, 400), true),
                new CursorZone("upper", "Upper", new IntRect(200, 0, 300, 100), new VisualRect(200, 0, 300, 100), true),
                new CursorZone("lower", "Lower", new IntRect(200, 100, 300, 200), new VisualRect(200, 100, 300, 200), true)
            ],
            [
                new CursorPortal("left", Edge.Right, new EdgeRange(0.0, 0.5), "upper", Edge.Left, new EdgeRange(0.0, 1.0)),
                new CursorPortal("left", Edge.Right, new EdgeRange(0.5, 1.0), "lower", Edge.Left, new EdgeRange(0.0, 1.0))
            ]);

        var upper = _engine.Evaluate(layout, new CursorPoint(99, 50), new CursorPoint(101, 50), new CursorPoint(99, 50));
        var lower = _engine.Evaluate(layout, new CursorPoint(99, 150), new CursorPoint(101, 150), new CursorPoint(99, 150));

        Assert.Equal(new CursorPoint(200, 50), upper.Target);
        Assert.Equal(new CursorPoint(200, 150), lower.Target);
    }

    [Fact]
    public void DifferentSizeVisualRectanglesStillMapByVisualRatio()
    {
        var layout = new CursorLayout(
            "layout",
            "layout",
            [
                new CursorZone("source", "Source", new IntRect(0, 0, 100, 100), new VisualRect(0, 0, 1000, 400), true),
                new CursorZone("target", "Target", new IntRect(200, 0, 300, 200), new VisualRect(1000, 0, 1200, 1000), true)
            ],
            [
                new CursorPortal("source", Edge.Right, new EdgeRange(0, 1), "target", Edge.Left, new EdgeRange(0, 1))
            ]);

        var decision = _engine.Evaluate(layout, new CursorPoint(99, 25), new CursorPoint(101, 25), new CursorPoint(99, 25));

        Assert.Equal(new CursorPoint(200, 50), decision.Target);
    }

    [Fact]
    public void DefaultStartPositionFallsBackToCenterOfFirstVisibleZone()
    {
        var layout = SingleVisibleZoneLayout();
        var start = _engine.ResolveStartPosition(layout, null);
        Assert.Equal(new CursorPoint(50, 50), start);
    }

    [Fact]
    public void ProfileStartPositionOutsideVisibleZonesFallsBackToCenterOfFirstVisibleZone()
    {
        var layout = new CursorLayout(
            "layout",
            "layout",
            [
                new CursorZone("visible", "Visible", new IntRect(0, 0, 100, 100), new VisualRect(0, 0, 100, 100), true),
                new CursorZone("hidden", "Hidden", new IntRect(100, 0, 200, 100), new VisualRect(100, 0, 200, 100), false)
            ],
            []);

        var start = _engine.ResolveStartPosition(layout, new CursorPoint(150, 50));

        Assert.Equal(new CursorPoint(50, 50), start);
    }

    [Fact]
    public void DefaultStartPositionOutsideVisibleZonesFallsBackToCenterOfFirstVisibleZone()
    {
        var layout = new CursorLayout(
            "layout",
            "layout",
            [
                new CursorZone("visible", "Visible", new IntRect(0, 0, 100, 100), new VisualRect(0, 0, 100, 100), true),
                new CursorZone("hidden", "Hidden", new IntRect(100, 0, 200, 100), new VisualRect(100, 0, 200, 100), false)
            ],
            [],
            new CursorPoint(150, 50));

        var start = _engine.ResolveStartPosition(layout, null);

        Assert.Equal(new CursorPoint(50, 50), start);
    }

    private static CursorLayout SingleVisibleZoneLayout() => new(
        "layout",
        "layout",
        [new CursorZone("visible", "Visible", new IntRect(0, 0, 100, 100), new VisualRect(0, 0, 100, 100), true)],
        []);
}
