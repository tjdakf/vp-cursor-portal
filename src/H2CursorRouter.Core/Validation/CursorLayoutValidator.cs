using H2CursorRouter.Core.Geometry;

namespace H2CursorRouter.Core.Validation;

public sealed class CursorLayoutValidator
{
    public ValidationResult Validate(CursorLayout? layout)
    {
        var errors = new List<string>();

        if (layout is null)
        {
            return ValidationResult.Failure(["Cursor layout is missing."]);
        }

        if (string.IsNullOrWhiteSpace(layout.Id))
        {
            errors.Add("Cursor layout id is required.");
        }

        if (layout.Zones.Count == 0)
        {
            errors.Add($"Cursor layout '{layout.Id}' must contain at least one zone.");
        }

        var zoneIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var zone in layout.Zones)
        {
            if (string.IsNullOrWhiteSpace(zone.Id))
            {
                errors.Add("Zone id is required.");
                continue;
            }

            if (!zoneIds.Add(zone.Id))
            {
                errors.Add($"Duplicate zone id '{zone.Id}'.");
            }

            if (!zone.WindowsRect.IsValid)
            {
                errors.Add($"Zone '{zone.Id}' has an invalid Windows rectangle.");
            }

            if (!zone.VisualRect.IsValid)
            {
                errors.Add($"Zone '{zone.Id}' has an invalid visual rectangle.");
            }
        }

        if (!layout.Zones.Any(zone => zone.IsVisible))
        {
            errors.Add($"Cursor layout '{layout.Id}' must contain at least one visible zone.");
        }

        foreach (var portal in layout.Portals)
        {
            if (!zoneIds.Contains(portal.FromZoneId))
            {
                errors.Add($"Portal references missing from-zone '{portal.FromZoneId}'.");
            }

            if (!zoneIds.Contains(portal.ToZoneId))
            {
                errors.Add($"Portal references missing to-zone '{portal.ToZoneId}'.");
            }

            if (!portal.FromRange.IsValid)
            {
                errors.Add($"Portal from-range is invalid for '{portal.FromZoneId}' to '{portal.ToZoneId}'.");
            }

            if (!portal.ToRange.IsValid)
            {
                errors.Add($"Portal to-range is invalid for '{portal.FromZoneId}' to '{portal.ToZoneId}'.");
            }
        }

        return ValidationResult.Failure(errors);
    }
}
