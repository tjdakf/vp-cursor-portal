namespace H2CursorRouter.Core.Geometry;

public enum RoutingDecisionKind
{
    KeepCurrent,
    MoveToTarget,
    RevertToLastValid,
    RejectUnsafeLayout
}
