using System.Text.Json;

namespace H2CursorRouter.H2;

public sealed class H2PresetEnumParser
{
    public IReadOnlyList<H2PresetInfo> Parse(string responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return Array.Empty<H2PresetInfo>();
        }

        using var document = JsonDocument.Parse(responseJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<H2PresetInfo>();
        }

        var presets = new List<H2PresetInfo>();
        foreach (var screen in document.RootElement.EnumerateArray())
        {
            if (screen.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var deviceId = TryGetInt32(screen, "deviceId") ?? 0;
            var screenId = TryGetInt32(screen, "screenId") ?? 0;
            if (!screen.TryGetProperty("presets", out var presetsElement) ||
                presetsElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var presetElement in presetsElement.EnumerateArray())
            {
                if (presetElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var presetId = TryGetInt32(presetElement, "presetId");
                if (presetId is null)
                {
                    continue;
                }

                var name = presetElement.TryGetProperty("name", out var nameElement)
                    ? nameElement.GetString()
                    : null;
                presets.Add(new H2PresetInfo(deviceId, screenId, presetId.Value, name));
            }
        }

        return presets;
    }

    private static int? TryGetInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            _ => null
        };
    }
}
