using H2CursorRouter.App.ViewModels;

namespace H2CursorRouter.App;

public interface IProfileDialogService
{
    ProfileDialogResult? Prompt(
        string title,
        string defaultName,
        IReadOnlyList<LayoutRow> layouts,
        string? selectedLayoutId,
        IReadOnlyList<DeviceRow> devices,
        IReadOnlyList<PresetRow> presets);
}
