using H2CursorRouter.Core.Configuration;
using H2CursorRouter.Core.Domain;
using H2CursorRouter.Core.Geometry;
using H2CursorRouter.Core.Profiles;
using H2CursorRouter.Core.Validation;
using Xunit;

namespace H2CursorRouter.Core.Tests;

public sealed class ValidationAndProfileTests
{
    [Fact]
    public void ProfileValidationRequiresAnAction()
    {
        var configuration = new AppConfiguration(
            [],
            [],
            [new ExecutionProfile("empty", "Empty", null, null, null, null, 0, true)],
            SafetySettings.Default);

        var result = new AppConfigurationValidator().Validate(configuration);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("must reference", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void H2FailurePreventsCursorLayoutWhenAckIsRequired()
    {
        var profile = new ExecutionProfile(
            "profile",
            "Profile",
            null,
            new H2PresetRef("h2-main", 0, 0, null),
            "layout",
            null,
            500,
            true);

        Assert.False(ProfileExecutionPlanner.ShouldApplyCursorLayout(profile, h2AckOk: false));
    }

    [Fact]
    public void CursorOnlyProfileCanApplyCursorLayout()
    {
        var profile = new ExecutionProfile("profile", "Profile", null, null, "layout", null, 0, true);
        Assert.True(ProfileExecutionPlanner.ShouldApplyCursorLayout(profile, h2AckOk: null));
    }

    [Fact]
    public void ValidProfileConfigurationPassesValidation()
    {
        var configuration = new AppConfiguration(
            [new H2DeviceConfig("h2-main", "Main H2", "192.168.0.100", 6000, 0, TimeSpan.FromSeconds(1))],
            [
                new CursorLayout(
                    "layout",
                    "Layout",
                    [new CursorZone("zone", "Zone", new IntRect(0, 0, 100, 100), new VisualRect(0, 0, 100, 100), true)],
                    [])
            ],
            [
                new ExecutionProfile(
                    "profile",
                    "Profile",
                    "Ctrl+Alt+1",
                    new H2PresetRef("h2-main", 0, 0, "Preset 1 / presetId 0"),
                    "layout",
                    null,
                    500,
                    true)
            ],
            SafetySettings.Default);

        var result = new AppConfigurationValidator().Validate(configuration);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void ConfigDocumentHandlesMissingCollectionsBeforeValidation()
    {
        var configuration = new ConfigDocument(null, null, null, null).ToRuntime();

        Assert.Empty(configuration.Devices);
        Assert.Empty(configuration.CursorLayouts);
        Assert.Empty(configuration.Profiles);
        Assert.Equal(SafetySettings.Default, configuration.Safety);
    }

    [Fact]
    public void ValidationRejectsDuplicateProfileHotkeys()
    {
        var configuration = new AppConfiguration(
            [],
            [],
            [
                new ExecutionProfile("one", "One", "Ctrl+Alt+1", null, "layout-a", null, 0, true),
                new ExecutionProfile("two", "Two", "Control+Alt+1", null, "layout-b", null, 0, true)
            ],
            SafetySettings.Default);

        var result = new AppConfigurationValidator().Validate(configuration);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("same hotkey", StringComparison.OrdinalIgnoreCase));
    }
}
