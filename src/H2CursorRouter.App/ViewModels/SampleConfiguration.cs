using H2CursorRouter.Core.Configuration;

namespace H2CursorRouter.App.ViewModels;

public static class SampleConfiguration
{
    public static AppConfiguration Create() => new([], [], [], SafetySettings.Default);
}
