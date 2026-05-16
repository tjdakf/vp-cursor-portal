namespace H2CursorRouter.Windows;

public interface IStartupRegistrationService
{
    bool IsRegistered();
    void SetRegistered(bool enabled, string executablePath, string arguments);
}
