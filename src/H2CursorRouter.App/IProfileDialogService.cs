using H2CursorRouter.App.ViewModels;

namespace H2CursorRouter.App;

public interface IProfileDialogService
{
    ProfileDialogResult? Prompt(
        string title,
        string defaultName,
        string? selectedHotkey,
        IReadOnlyList<LayoutRow> layouts,
        string? selectedLayoutId,
        bool isCursorLayoutOnly,
        IReadOnlyList<DeviceRow> devices,
        IReadOnlyList<PresetRow> presets,
        string? selectedDeviceId,
        int? selectedScreenId,
        int? selectedPresetId,
        string? selectedPresetDisplayName);
}
