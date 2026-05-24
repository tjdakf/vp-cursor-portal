namespace H2CursorRouter.App;

public interface IDeviceDialogService
{
    DeviceDialogResult? Prompt(string defaultName, string defaultHost, int defaultPort);
}
