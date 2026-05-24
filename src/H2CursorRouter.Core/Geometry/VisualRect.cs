namespace H2CursorRouter.Core.Geometry;

public readonly record struct VisualRect(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;
    public double Height => Bottom - Top;
    public bool IsValid => Width > 0 && Height > 0;
}
