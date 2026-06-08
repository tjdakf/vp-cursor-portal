using H2CursorRouter.App.ViewModels;
using H2CursorRouter.Core.Configuration;
using H2CursorRouter.Core.Geometry;

namespace H2CursorRouter.App.Services;

internal sealed class ConfigurationRowMapper
{
    private readonly MonitorZoneMatcher _monitorZoneMatcher;

    public ConfigurationRowMapper(MonitorZoneMatcher monitorZoneMatcher)
    {
        _monitorZoneMatcher = monitorZoneMatcher;
    }

    public ConfigurationRows ToRows(AppConfiguration configuration) => new(
        configuration.Devices.Select(DeviceRow.FromModel).ToArray(),
        configuration.CursorLayouts.Select(LayoutRow.FromModel).ToArray(),
        configuration.CursorLayouts
            .SelectMany(layout => layout.Zones.Select(zone => ZoneRow.FromModel(layout.Id, zone)))
            .ToArray(),
        configuration.CursorLayouts
            .SelectMany(layout => layout.Portals.Select(portal => PortalRow.FromModel(layout.Id, portal)))
            .ToArray(),
        configuration.Profiles.Select(ProfileRow.FromModel).ToArray(),
        configuration.PresetCache.Select(PresetRow.FromCachedPreset).ToArray(),
        configuration.DisplayAliasEntries.Select(DisplayAliasRow.FromModel).ToArray());

    public AppConfiguration BuildConfiguration(
        IEnumerable<DeviceRow> devices,
        IEnumerable<LayoutRow> layouts,
        IEnumerable<ZoneRow> zones,
        IEnumerable<PortalRow> portals,
        IEnumerable<ProfileRow> profiles,
        IEnumerable<PresetRow> presets,
        IEnumerable<DisplayAliasRow> displayAliases,
        IReadOnlyList<MonitorRow> monitors,
        SafetySettings safetySettings) =>
        new(
            devices.Select(device => device.ToModel()).ToArray(),
            layouts.Select(layout => BuildLayout(layout, zones, portals, monitors)).ToArray(),
            profiles.Select(profile => profile.ToModel()).ToArray(),
            safetySettings,
            presets.Select(preset => preset.ToCachedPreset()).ToArray(),
            displayAliases
                .Where(alias => !string.IsNullOrWhiteSpace(alias.DeviceName))
                .GroupBy(alias => MonitorZoneMatcher.NormalizeZoneId(alias.DeviceName), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last().ToModel())
                .ToArray());

    public CursorLayout BuildLayout(
        LayoutRow layout,
        IEnumerable<ZoneRow> zones,
        IEnumerable<PortalRow> portals,
        IReadOnlyList<MonitorRow> monitors)
    {
        var start = layout.DefaultStartX is not null && layout.DefaultStartY is not null
            ? new CursorPoint(layout.DefaultStartX.Value, layout.DefaultStartY.Value)
            : (CursorPoint?)null;

        return new CursorLayout(
            layout.Id,
            layout.Name,
            zones.Where(zone => string.Equals(zone.LayoutId, layout.Id, StringComparison.OrdinalIgnoreCase))
                .Select(zone => BuildZone(zone, monitors))
                .ToArray(),
            portals.Where(portal => string.Equals(portal.LayoutId, layout.Id, StringComparison.OrdinalIgnoreCase))
                .Select(portal => portal.ToModel())
                .ToArray(),
            start,
            string.IsNullOrWhiteSpace(layout.Description) ? null : layout.Description);
    }

    public CursorZone BuildZone(ZoneRow zone, IReadOnlyList<MonitorRow> monitors)
    {
        var model = zone.ToModel();
        var monitor = _monitorZoneMatcher.FindMonitorForZone(zone, monitors);
        return monitor is null
            ? model
            : model with
            {
                WindowsRect = new IntRect(monitor.Left, monitor.Top, monitor.Right, monitor.Bottom),
                DisplayName = zone.DisplayName
            };
    }

    public static string FormatLayoutDisplays(IEnumerable<ZoneRow> zones) =>
        LayoutRow.FormatDisplays(zones.Select(zone => zone.ToModel()));
}

internal sealed record ConfigurationRows(
    IReadOnlyList<DeviceRow> Devices,
    IReadOnlyList<LayoutRow> Layouts,
    IReadOnlyList<ZoneRow> Zones,
    IReadOnlyList<PortalRow> Portals,
    IReadOnlyList<ProfileRow> Profiles,
    IReadOnlyList<PresetRow> Presets,
    IReadOnlyList<DisplayAliasRow> DisplayAliases);
