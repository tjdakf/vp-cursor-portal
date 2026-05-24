using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApplication = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace H2CursorRouter.App;

public sealed class ConfirmationDialogService : IConfirmationDialogService
{
    public bool Confirm(string title, string message)
    {
        var dialog = new AppDialogWindow(title, CreateContent(message, out var okButton, out var cancelButton))
        {
            Width = 440
        };

        okButton.Click += (_, _) => dialog.DialogResult = true;
        cancelButton.Click += (_, _) => dialog.DialogResult = false;
        dialog.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                dialog.DialogResult = false;
            }
        };

        return dialog.ShowDialog() == true;
    }

    private static UIElement CreateContent(string message, out WpfButton okButton, out WpfButton cancelButton)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = WpfApplication.Current.TryFindResource("MutedTextBrush") as System.Windows.Media.Brush
        });

        var buttons = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };

        cancelButton = new WpfButton
        {
            Content = "Cancel",
            MinWidth = 86,
            Style = WpfApplication.Current.TryFindResource("SecondaryButtonStyle") as Style,
            IsCancel = true,
            Margin = new Thickness(0, 0, 8, 0)
        };
        okButton = new WpfButton
        {
            Content = "OK",
            MinWidth = 86,
            IsDefault = true,
            Margin = new Thickness(0)
        };
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(okButton);
        panel.Children.Add(buttons);

        return panel;
    }
}
