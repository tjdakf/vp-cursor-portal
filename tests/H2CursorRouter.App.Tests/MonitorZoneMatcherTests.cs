using H2CursorRouter.App.Services;
using H2CursorRouter.App.ViewModels;
using Xunit;

namespace H2CursorRouter.App.Tests;

public sealed class MonitorZoneMatcherTests
{
    [Fact]
    public void NormalizeZoneIdKeepsOnlyLettersAndDigits()
    {
        Assert.Equal("DISPLAY2", MonitorZoneMatcher.NormalizeZoneId(@"\\.\DISPLAY2"));
        Assert.Equal("MONITOR", MonitorZoneMatcher.NormalizeZoneId("!@#"));
    }

    [Fact]
    public void FindMonitorForZonePrefersNormalizedDisplayName()
    {
        var matcher = new MonitorZoneMatcher();
        var zone = new ZoneRow { Id = "DISPLAY2" };
        var monitors = new[]
        {
            CreateMonitor(@"\\.\DISPLAY1", 0, 0, 100, 100),
            CreateMonitor(@"\\.\DISPLAY2", 100, 0, 200, 100)
        };

        var monitor = matcher.FindMonitorForZone(zone, monitors);

        Assert.NotNull(monitor);
        Assert.Equal(@"\\.\DISPLAY2", monitor.DeviceName);
    }

    [Fact]
    public void FindMonitorForZoneFallsBackToWindowsRectangle()
    {
        var matcher = new MonitorZoneMatcher();
        var zone = new ZoneRow
        {
            Id = "UNKNOWN",
            WindowsLeft = 100,
            WindowsTop = 0,
            WindowsRight = 200,
            WindowsBottom = 100
        };
        var monitors = new[]
        {
            CreateMonitor(@"\\.\DISPLAY1", 0, 0, 100, 100),
            CreateMonitor(@"\\.\DISPLAY2", 100, 0, 200, 100)
        };

        var monitor = matcher.FindMonitorForZone(zone, monitors);

        Assert.NotNull(monitor);
        Assert.Equal(@"\\.\DISPLAY2", monitor.DeviceName);
    }

    [Fact]
    public void RefreshWindowsCoordinatesUpdatesStaleZoneBounds()
    {
        var matcher = new MonitorZoneMatcher();
        var zones = new[]
        {
            new ZoneRow
            {
                Id = "DISPLAY2",
                WindowsLeft = 0,
                WindowsTop = 0,
                WindowsRight = 100,
                WindowsBottom = 100
            }
        };
        var monitors = new[] { CreateMonitor(@"\\.\DISPLAY2", 100, 0, 200, 100) };

        var changed = matcher.RefreshWindowsCoordinatesFromDetectedDisplays(zones, monitors);

        Assert.Equal(1, changed);
        Assert.Equal(100, zones[0].WindowsLeft);
        Assert.Equal(200, zones[0].WindowsRight);
    }

    [Fact]
    public void CreateZoneFromMonitorUsesNormalizedIdAndDisplayName()
    {
        var matcher = new MonitorZoneMatcher();
        var monitor = CreateMonitor(@"\\.\DISPLAY2", 100, 0, 200, 100);

        var zone = matcher.CreateZoneFromMonitor("layout", monitor);

        Assert.Equal("layout", zone.LayoutId);
        Assert.Equal("DISPLAY2", zone.Id);
        Assert.Equal(@"\\.\DISPLAY2", zone.DisplayName);
        Assert.Equal(100, zone.WindowsLeft);
        Assert.Equal(200, zone.WindowsRight);
        Assert.True(zone.IsVisible);
    }

    private static MonitorRow CreateMonitor(string deviceName, int left, int top, int right, int bottom) => new()
    {
        DeviceName = deviceName,
        Left = left,
        Top = top,
        Right = right,
        Bottom = bottom
    };
}
