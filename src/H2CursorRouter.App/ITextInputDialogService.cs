namespace H2CursorRouter.App;

public interface ITextInputDialogService
{
    string? Prompt(string title, string message, string defaultValue);
}
