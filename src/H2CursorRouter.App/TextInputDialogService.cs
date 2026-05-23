using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfButton = System.Windows.Controls.Button;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace H2CursorRouter.App;

public sealed class TextInputDialogService : ITextInputDialogService
{
    public string? Prompt(string title, string message, string defaultValue)
    {
        var input = new WpfTextBox
        {
            Text = defaultValue,
            MinWidth = 320,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Content = CreateContent(message, input, out var okButton, out var cancelButton)
        };

        dialog.Loaded += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
        };
        input.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                dialog.DialogResult = true;
            }
        };
        okButton.Click += (_, _) => dialog.DialogResult = true;
        cancelButton.Click += (_, _) => dialog.DialogResult = false;

        return dialog.ShowDialog() == true ? input.Text.Trim() : null;
    }

    private static UIElement CreateContent(string message, WpfTextBox input, out WpfButton okButton, out WpfButton cancelButton)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(18)
        };

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(input);

        var buttons = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        okButton = new WpfButton
        {
            Content = "OK",
            MinWidth = 82,
            IsDefault = true,
            Margin = new Thickness(0, 0, 8, 0)
        };
        cancelButton = new WpfButton
        {
            Content = "Cancel",
            MinWidth = 82,
            IsCancel = true
        };
        buttons.Children.Add(okButton);
        buttons.Children.Add(cancelButton);
        panel.Children.Add(buttons);

        return panel;
    }
}
