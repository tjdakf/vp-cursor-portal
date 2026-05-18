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
            "192.168.0.11",
            H2DeviceConfig.DefaultPort,
            0,
            TimeSpan.FromMilliseconds(1000));

        var preset = new H2PresetRef("h2-main", 0, 0, "Preset 1 / presetId 0");

        var monitor1 = new CursorZone(
            "DISPLAY2",
            "Monitor 1",
            new IntRect(0, 0, 1920, 1080),
            new VisualRect(0, 0, 1920, 1080),
            true);
        var monitor2 = new CursorZone(
            "DISPLAY1",
            "Monitor 2",
            new IntRect(1920, 0, 3840, 1080),
            new VisualRect(1920, 0, 3840, 1080),
            true);
        var monitor3 = new CursorZone(
            "DISPLAY3",
            "Monitor 3",
            new IntRect(3840, 0, 5760, 1080),
            new VisualRect(1920, 0, 3840, 1080),
            true);

        var layout13 = new CursorLayout(
            "layout-monitor-1-3",
            "Monitor 1 <-> Monitor 3",
            [
                monitor1,
                monitor3
            ],
            [
                new CursorPortal("DISPLAY2", Edge.Right, new EdgeRange(0.0, 1.0), "DISPLAY3", Edge.Left, new EdgeRange(0.0, 1.0)),
                new CursorPortal("DISPLAY3", Edge.Left, new EdgeRange(0.0, 1.0), "DISPLAY2", Edge.Right, new EdgeRange(0.0, 1.0))
            ],
            new CursorPoint(960, 540));

        var layout31Reversed = new CursorLayout(
            "layout-monitor-3-1-reversed",
            "Monitor 3 <-> Monitor 1 Reversed",
            [
                monitor3 with { VisualRect = new VisualRect(0, 0, 1920, 1080) },
                monitor1 with { VisualRect = new VisualRect(1920, 0, 3840, 1080) }
            ],
            [
                new CursorPortal("DISPLAY3", Edge.Right, new EdgeRange(0.0, 1.0), "DISPLAY2", Edge.Left, new EdgeRange(0.0, 1.0)),
                new CursorPortal("DISPLAY2", Edge.Left, new EdgeRange(0.0, 1.0), "DISPLAY3", Edge.Right, new EdgeRange(0.0, 1.0))
            ],
            new CursorPoint(4800, 540));

        var layout2 = new CursorLayout(
            "layout-monitor-2",
            "Monitor 2 Only",
            [
                monitor2 with { VisualRect = new VisualRect(0, 0, 1920, 1080) }
            ],
            [],
            new CursorPoint(2880, 540));

        var layout12 = new CursorLayout(
            "layout-monitor-1-2",
            "Monitor 1 <-> Monitor 2",
            [
                monitor1,
                monitor2
            ],
            [
                new CursorPortal("DISPLAY2", Edge.Right, new EdgeRange(0.0, 1.0), "DISPLAY1", Edge.Left, new EdgeRange(0.0, 1.0)),
                new CursorPortal("DISPLAY1", Edge.Left, new EdgeRange(0.0, 1.0), "DISPLAY2", Edge.Right, new EdgeRange(0.0, 1.0))
            ],
            new CursorPoint(960, 540));

        var profiles = new[]
        {
            new ExecutionProfile(
                "preset-1-layout-1-3",
                "Preset 1 + Monitor 1/3 Tunnel",
                "Ctrl+Alt+1",
                preset,
                "layout-monitor-1-3",
                new CursorPoint(960, 540),
                500,
                true),
            new ExecutionProfile(
                "preset-1-layout-3-1-reversed",
                "Preset 1 + Monitor 3/1 Tunnel Reversed",
                "Ctrl+Alt+2",
                preset,
                "layout-monitor-3-1-reversed",
                new CursorPoint(4800, 540),
                500,
                true),
            new ExecutionProfile(
                "preset-1-layout-2",
                "Preset 1 + Monitor 2 Only",
                "Ctrl+Alt+3",
                preset,
                "layout-monitor-2",
                new CursorPoint(2880, 540),
                500,
                true),
            new ExecutionProfile(
                "preset-1-layout-1-2",
                "Preset 1 + Monitor 1/2 Tunnel",
                "Ctrl+Alt+4",
                preset,
                "layout-monitor-1-2",
                new CursorPoint(960, 540),
                500,
                true)
        };

        return new AppConfiguration([device], [layout13, layout31Reversed, layout2, layout12], profiles, SafetySettings.Default);
    }
}
