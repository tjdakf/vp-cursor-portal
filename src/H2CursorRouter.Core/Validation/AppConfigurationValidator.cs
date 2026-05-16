using H2CursorRouter.Core.Configuration;

namespace H2CursorRouter.Core.Validation;

public sealed class AppConfigurationValidator
{
    private readonly CursorLayoutValidator _layoutValidator = new();

    public ValidationResult Validate(AppConfiguration configuration)
    {
        var errors = new List<string>();

        var deviceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in configuration.Devices)
        {
            if (string.IsNullOrWhiteSpace(device.Id))
            {
                errors.Add("Device id is required.");
                continue;
            }

            if (!deviceIds.Add(device.Id))
            {
                errors.Add($"Duplicate device id '{device.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(device.Host))
            {
                errors.Add($"Device '{device.Id}' host is required.");
            }

            if (device.Port <= 0 || device.Port > 65535)
            {
                errors.Add($"Device '{device.Id}' port must be between 1 and 65535.");
            }

            if (device.Timeout <= TimeSpan.Zero)
            {
                errors.Add($"Device '{device.Id}' timeout must be positive.");
            }
        }

        var layoutIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layout in configuration.CursorLayouts)
        {
            if (!layoutIds.Add(layout.Id))
            {
                errors.Add($"Duplicate cursor layout id '{layout.Id}'.");
            }

            errors.AddRange(_layoutValidator.Validate(layout).Errors);
        }

        var profileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in configuration.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id))
            {
                errors.Add("Profile id is required.");
                continue;
            }

            if (!profileIds.Add(profile.Id))
            {
                errors.Add($"Duplicate profile id '{profile.Id}'.");
            }

            if (profile.H2Preset is null && string.IsNullOrWhiteSpace(profile.CursorLayoutId))
            {
                errors.Add($"Profile '{profile.Id}' must reference an H2 preset, a cursor layout, or both.");
            }

            if (profile.H2Preset is not null && !deviceIds.Contains(profile.H2Preset.DeviceId))
            {
                errors.Add($"Profile '{profile.Id}' references missing device '{profile.H2Preset.DeviceId}'.");
            }

            if (!string.IsNullOrWhiteSpace(profile.CursorLayoutId) && !layoutIds.Contains(profile.CursorLayoutId))
            {
                errors.Add($"Profile '{profile.Id}' references missing cursor layout '{profile.CursorLayoutId}'.");
            }

            if (profile.PostAckDelayMs < 0)
            {
                errors.Add($"Profile '{profile.Id}' post-ack delay cannot be negative.");
            }
        }

        return ValidationResult.Failure(errors);
    }
}
