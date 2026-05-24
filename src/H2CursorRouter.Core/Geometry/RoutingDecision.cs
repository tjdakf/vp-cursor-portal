namespace H2CursorRouter.Core.Geometry;

public sealed record RoutingDecision(
    RoutingDecisionKind Kind,
    CursorPoint? Target,
    string Reason)
{
    public static RoutingDecision KeepCurrent(string reason = "Cursor is inside a visible zone.") =>
        new(RoutingDecisionKind.KeepCurrent, null, reason);

    public static RoutingDecision MoveToTarget(CursorPoint target, string reason) =>
        new(RoutingDecisionKind.MoveToTarget, target, reason);

    public static RoutingDecision RevertToLastValid(CursorPoint target, string reason) =>
        new(RoutingDecisionKind.RevertToLastValid, target, reason);

    public static RoutingDecision RejectUnsafeLayout(string reason) =>
        new(RoutingDecisionKind.RejectUnsafeLayout, null, reason);
}
