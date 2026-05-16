using System.Runtime.InteropServices;
using H2CursorRouter.Core.Geometry;

namespace H2CursorRouter.Windows;

public sealed class Win32CursorService : ICursorService
{
    public CursorPoint GetPosition()
    {
        if (!GetCursorPos(out var point))
        {
            throw new InvalidOperationException("GetCursorPos failed.");
        }

        return new CursorPoint(point.X, point.Y);
    }

    public void SetPosition(CursorPoint position)
    {
        if (!SetCursorPos(position.X, position.Y))
        {
            throw new InvalidOperationException("SetCursorPos failed.");
        }
    }

    public void ClipTo(IntRect rect)
    {
        var nativeRect = new Rect(rect.Left, rect.Top, rect.Right, rect.Bottom);
        if (!ClipCursor(ref nativeRect))
        {
            throw new InvalidOperationException("ClipCursor failed.");
        }
    }

    public void ReleaseClip()
    {
        if (!ClipCursor(IntPtr.Zero))
        {
            throw new InvalidOperationException("ClipCursor release failed.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ClipCursor(ref Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ClipCursor(IntPtr rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public Rect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
