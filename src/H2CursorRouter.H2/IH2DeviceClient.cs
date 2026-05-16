using H2CursorRouter.Core.Domain;

namespace H2CursorRouter.H2;

public interface IH2DeviceClient
{
    Task<H2CommandResult> LoadPresetAsync(
        H2DeviceConfig device,
        int screenId,
        int presetId,
        CancellationToken cancellationToken = default);

    Task<H2CommandResult> GetPresetEnumAsync(
        H2DeviceConfig device,
        int param0 = 0,
        int param1 = 0,
        CancellationToken cancellationToken = default);
}
