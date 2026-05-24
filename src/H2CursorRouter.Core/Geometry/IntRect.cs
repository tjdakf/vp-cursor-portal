namespace H2CursorRouter.Core.Geometry;

public readonly record struct IntRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool IsValid => Width > 0 && Height > 0;

    public bool Contains(CursorPoint point) =>
        point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;

    public CursorPoint Center() => new(Left + Width / 2, Top + Height / 2);
}
