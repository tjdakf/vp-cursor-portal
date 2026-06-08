using H2CursorRouter.App.Services;
using H2CursorRouter.App.ViewModels;
using H2CursorRouter.Core.Configuration;
using H2CursorRouter.Core.Domain;
using H2CursorRouter.Core.Geometry;
using H2CursorRouter.Core.Profiles;
using Xunit;

namespace H2CursorRouter.App.Tests;

public sealed class ConfigurationRowMapperTests
{
    [Fact]
    public void ToRowsPreservesConfigurationCollections()
    {
        var mapper = new ConfigurationRowMapper(new MonitorZoneMatcher());
        var fetchedAt = DateTimeOffset.Parse("2026-05-24T00:00:00Z");
        var configuration = new AppConfiguration(
            [new H2DeviceConfig("h2", "H2", "192.168.0.11", 6000, 0, TimeSpan.FromMilliseconds(500))],
            [CreateLayout()],
            [
                new ExecutionProfile(
                    "profile",
                    "Profile",
                    "Ctrl+Alt+1",
                    new H2PresetRef("h2", 0, 0, "Preset 1 / presetId 0"),
                    "layout",
                    new CursorPoint(50, 50),
                    250,
                    true)
            ],
            SafetySettings.Default,
            [new CachedH2Preset("h2", "H2", 0, 0, 1, 0, "Preset 1", fetchedAt)]);

        var rows = mapper.ToRows(configuration);

        Assert.Single(rows.Devices);
        Assert.Single(rows.Layouts);
        Assert.Single(rows.Zones);
        Assert.Single(rows.Portals);
        Assert.Single(rows.Profiles);
        Assert.Single(rows.Presets);
        Assert.Equal("Layout description", rows.Layouts[0].Description);
        Assert.Equal(50, rows.Layouts[0].DefaultStartX);
        Assert.Equal("Preset 1", rows.Presets[0].DisplayName);
    }

    [Fact]
    public void BuildConfigurationGroupsRowsByLayoutId()
    {
        var mapper = new ConfigurationRowMapper(new MonitorZoneMatcher());
        var device = new DeviceRow
        {
            Id = "h2",
            Name = "H2",
            Host = "192.168.0.11",
            Port = 6000,
            DeviceId = 0,
            TimeoutMs = 500
        };
        var layout = new LayoutRow
        {
            Id = "layout",
            Name = "Layout",
            Description = "Layout description",
            DefaultStartX = 50,
            DefaultStartY = 60
        };
        var zone = new ZoneRow
        {
            LayoutId = "layout",
            Id = "DISPLAY2",
            DisplayName = "Monitor 1",
            WindowsLeft = 0,
            WindowsTop = 0,
            WindowsRight = 100,
            WindowsBottom = 100,
            VisualLeft = 0,
            VisualTop = 0,
            VisualRight = 100,
            VisualBottom = 100,
            IsVisible = true
        };
        var portal = new PortalRow
        {
            LayoutId = "layout",
            FromZoneId = "DISPLAY2",
            ToZoneId = "DISPLAY2"
        };
        var profile = new ProfileRow
        {
            Id = "profile",
            Name = "Profile",
            CursorLayoutId = "layout",
            PostAckDelayMs = 250,
            RequireH2AckBeforeCursorLayout = true
        };
        var preset = new PresetRow
        {
            DeviceConfigId = "h2",
            DeviceName = "H2",
            H2DeviceId = 0,
            ScreenId = 0,
            FriendlyPresetNumber = 1,
            PresetId = 0,
            DisplayName = "Preset 1"
        };

        var configuration = mapper.BuildConfiguration(
            [device],
            [layout],
            [zone],
            [portal],
            [profile],
            [preset],
            [],
            [],
            SafetySettings.Default);

        var builtLayout = Assert.Single(configuration.CursorLayouts);
        Assert.Equal("layout", builtLayout.Id);
        Assert.Equal(new CursorPoint(50, 60), builtLayout.DefaultStartPosition);
        Assert.Equal("Layout description", builtLayout.Description);
        Assert.Single(builtLayout.Zones);
        Assert.Single(builtLayout.Portals);
        Assert.Single(configuration.PresetCache);
    }

    [Fact]
    public void BuildConfigurationPreservesLoadedSafetySettings()
    {
        var mapper = new ConfigurationRowMapper(new MonitorZoneMatcher());
        var safety = new SafetySettings(
            "Ctrl+Alt+Shift+F12",
            DisableRoutingOnMonitorTopologyChange: false,
            StartWithRoutingDisabled: false);

        var configuration = mapper.BuildConfiguration([], [], [], [], [], [], [], [], safety);

        Assert.Equal(safety, configuration.Safety);
    }

    [Fact]
    public void BuildConfigurationPreservesDisplayAliases()
    {
        var mapper = new ConfigurationRowMapper(new MonitorZoneMatcher());
        var lastSeenAt = DateTimeOffset.Parse("2026-05-24T00:00:00Z");
        var alias = new DisplayAliasRow
        {
            DeviceName = "DISPLAY1",
            Alias = "Main wall",
            LastSeenAtUtc = lastSeenAt
        };

        var configuration = mapper.BuildConfiguration([], [], [], [], [], [], [alias], [], SafetySettings.Default);

        var savedAlias = Assert.Single(configuration.DisplayAliasEntries);
        Assert.Equal("DISPLAY1", savedAlias.DeviceName);
        Assert.Equal("Main wall", savedAlias.Alias);
        Assert.Equal(lastSeenAt, savedAlias.LastSeenAtUtc);
    }

    [Fact]
    public void BuildConfigurationPreservesDisplayRowsWithEmptyAliases()
    {
        var mapper = new ConfigurationRowMapper(new MonitorZoneMatcher());
        var lastSeenAt = DateTimeOffset.Parse("2026-05-24T00:00:00Z");
        var alias = new DisplayAliasRow
        {
            DeviceName = "DISPLAY2",
            Alias = "   ",
            LastSeenAtUtc = lastSeenAt
        };

        var configuration = mapper.BuildConfiguration([], [], [], [], [], [], [alias], [], SafetySettings.Default);

        var savedAlias = Assert.Single(configuration.DisplayAliasEntries);
        Assert.Equal("DISPLAY2", savedAlias.DeviceName);
        Assert.Equal("", savedAlias.Alias);
        Assert.Equal(lastSeenAt, savedAlias.LastSeenAtUtc);
    }

    [Fact]
    public void BuildConfigurationRebindsZoneBoundsFromDetectedMonitor()
    {
        var mapper = new ConfigurationRowMapper(new MonitorZoneMatcher());
        var layout = new LayoutRow { Id = "layout", Name = "Layout" };
        var zone = new ZoneRow
        {
            LayoutId = "layout",
            Id = "DISPLAY2",
            DisplayName = "Monitor 1",
            WindowsLeft = 0,
            WindowsTop = 0,
            WindowsRight = 100,
            WindowsBottom = 100,
            VisualLeft = 0,
            VisualTop = 0,
            VisualRight = 100,
            VisualBottom = 100,
            IsVisible = true
        };
        var monitor = new MonitorRow
        {
            DeviceName = @"\\.\DISPLAY2",
            Left = 100,
            Top = 0,
            Right = 200,
            Bottom = 100
        };

        var configuration = mapper.BuildConfiguration([], [layout], [zone], [], [], [], [], [monitor], SafetySettings.Default);

        var builtZone = Assert.Single(Assert.Single(configuration.CursorLayouts).Zones);
        Assert.Equal(new IntRect(100, 0, 200, 100), builtZone.WindowsRect);
    }

    private static CursorLayout CreateLayout() => new(
        "layout",
        "Layout",
        [
            new CursorZone(
                "DISPLAY2",
                "Monitor 1",
                new IntRect(0, 0, 100, 100),
                new VisualRect(0, 0, 100, 100),
                true)
        ],
        [
            new CursorPortal(
                "DISPLAY2",
                Edge.Right,
                new EdgeRange(0, 1),
                "DISPLAY2",
                Edge.Left,
                new EdgeRange(0, 1))
        ],
        new CursorPoint(50, 60),
        "Layout description");
}
