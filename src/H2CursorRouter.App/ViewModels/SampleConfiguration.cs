using H2CursorRouter.Core.Configuration;
using H2CursorRouter.Core.Domain;
using H2CursorRouter.Core.Geometry;
using H2CursorRouter.Core.Profiles;

namespace H2CursorRouter.App.ViewModels;

public static class SampleConfiguration
{
    public static AppConfiguration Create()
    {
        var device = new H2DeviceConfig(
            "h2-main",
            "Main H2",
            "192.168.0.100",
            H2DeviceConfig.DefaultPort,
            0,
            TimeSpan.FromMilliseconds(1000));

        var layout = new CursorLayout(
            "layout-monitor-1-3",
            "Monitor 1 to Monitor 3",
            [
                new CursorZone(
                    "MONITOR_1",
                    "Monitor 1",
                    new IntRect(0, 0, 1920, 1080),
                    new VisualRect(0, 0, 1920, 1080),
                    true),
                new CursorZone(
                    "MONITOR_3",
                    "Monitor 3",
                    new IntRect(3840, 0, 5760, 1080),
                    new VisualRect(1920, 0, 3840, 1080),
                    true)
            ],
            [
                new CursorPortal("MONITOR_1", Edge.Right, new EdgeRange(0.0, 1.0), "MONITOR_3", Edge.Left, new EdgeRange(0.0, 1.0)),
                new CursorPortal("MONITOR_3", Edge.Left, new EdgeRange(0.0, 1.0), "MONITOR_1", Edge.Right, new EdgeRange(0.0, 1.0))
            ]);

        var profile = new ExecutionProfile(
            "preset-1-layout-1-3",
            "H2 Preset 1 + Monitor 1/3 Cursor",
            "Ctrl+Alt+1",
            new H2PresetRef("h2-main", 0, 0, "Preset 1 / presetId 0"),
            "layout-monitor-1-3",
            new CursorPoint(960, 540),
            500,
            true);

        return new AppConfiguration([device], [layout], [profile], SafetySettings.Default);
    }
}
