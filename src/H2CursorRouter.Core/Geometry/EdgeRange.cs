namespace H2CursorRouter.Core.Geometry;

public readonly record struct EdgeRange(double StartRatio, double EndRatio)
{
    public bool IsValid =>
        StartRatio >= 0.0 &&
        EndRatio <= 1.0 &&
        EndRatio > StartRatio;

    public bool Contains(double ratio) =>
        ratio >= StartRatio && ratio <= EndRatio;
}
