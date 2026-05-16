using H2CursorRouter.Core.Geometry;

namespace H2CursorRouter.Windows;

public interface ICursorService
{
    CursorPoint GetPosition();
    void SetPosition(CursorPoint position);
    void ClipTo(IntRect rect);
    void ReleaseClip();
}
