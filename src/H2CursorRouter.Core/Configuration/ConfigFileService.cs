using System.Text.Json;
using System.Text.Json.Serialization;
using H2CursorRouter.Core.Validation;

namespace H2CursorRouter.Core.Configuration;

public sealed class ConfigFileService
{
    private static readonly JsonSerializerOptions Options = CreateOptions();
    private readonly AppConfigurationValidator _validator = new();

    public async Task<AppConfiguration> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<ConfigDocument>(stream, Options, cancellationToken)
            ?? throw new InvalidOperationException($"Configuration file '{path}' is empty or invalid.");

        var configuration = document.ToRuntime();
        var validation = _validator.Validate(configuration);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Configuration file '{path}' is invalid: {string.Join("; ", validation.Errors)}");
        }

        return configuration;
    }

    public async Task SaveAsync(AppConfiguration configuration, string path, CancellationToken cancellationToken = default)
    {
        var validation = _validator.Validate(configuration);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Cannot save invalid configuration: {string.Join("; ", validation.Errors)}");
        }

        await using var stream = File.Create(path);
        var document = ConfigDocument.FromRuntime(configuration);
        await JsonSerializer.SerializeAsync(stream, document, Options, cancellationToken);
    }

    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
