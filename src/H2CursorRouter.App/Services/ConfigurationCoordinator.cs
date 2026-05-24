using H2CursorRouter.App.ViewModels;
using H2CursorRouter.Core.Configuration;
using H2CursorRouter.Core.Validation;

namespace H2CursorRouter.App.Services;

internal sealed class ConfigurationCoordinator
{
    private readonly ConfigFileService _configFileService = new();
    private readonly ConfigurationRowMapper _rowMapper;
    private readonly AppConfigurationValidator _validator;

    public ConfigurationCoordinator(ConfigurationRowMapper rowMapper, AppConfigurationValidator validator)
    {
        _rowMapper = rowMapper;
        _validator = validator;
    }

    public ConfigurationRows ToRows(AppConfiguration configuration) =>
        _rowMapper.ToRows(configuration);

    public AppConfiguration BuildConfiguration(
        IEnumerable<DeviceRow> devices,
        IEnumerable<LayoutRow> layouts,
        IEnumerable<ZoneRow> zones,
        IEnumerable<PortalRow> portals,
        IEnumerable<ProfileRow> profiles,
        IEnumerable<PresetRow> presets,
        IReadOnlyList<MonitorRow> monitors,
        SafetySettings safetySettings) =>
        _rowMapper.BuildConfiguration(devices, layouts, zones, portals, profiles, presets, monitors, safetySettings);

    public ValidationResult Validate(AppConfiguration configuration) =>
        _validator.Validate(configuration);

    public Task SaveAsync(AppConfiguration configuration, string path) =>
        _configFileService.SaveAsync(configuration, path);
}
