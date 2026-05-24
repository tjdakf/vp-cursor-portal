using System.Windows;

namespace H2CursorRouter.App;

public sealed class ConfirmationDialogService : IConfirmationDialogService
{
    public bool Confirm(string title, string message) =>
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel) == MessageBoxResult.OK;
}
