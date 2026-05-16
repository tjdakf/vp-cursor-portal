using H2CursorRouter.Core.Geometry;

namespace H2CursorRouter.Windows;

public sealed record MonitorInfo(string DeviceName, IntRect Bounds, bool IsPrimary);
