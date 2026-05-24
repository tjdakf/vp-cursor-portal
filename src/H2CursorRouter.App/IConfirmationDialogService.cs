namespace H2CursorRouter.App;

public interface IConfirmationDialogService
{
    bool Confirm(string title, string message);
}
