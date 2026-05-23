using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using H2CursorRouter.App.ViewModels;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfPanel = System.Windows.Controls.Panel;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace H2CursorRouter.App;

public sealed class ProfileDialogService : IProfileDialogService
{
    public ProfileDialogResult? Prompt(
        string title,
        string defaultName,
        IReadOnlyList<LayoutRow> layouts,
        string? selectedLayoutId)
    {
        var nameInput = new WpfTextBox { Text = defaultName, MinWidth = 320 };
        var hotkeyInput = new WpfTextBox { Text = "", MinWidth = 320 };
        var layoutInput = new WpfComboBox
        {
            ItemsSource = layouts,
            DisplayMemberPath = nameof(LayoutRow.Name),
            SelectedValuePath = nameof(LayoutRow.Id),
            SelectedValue = selectedLayoutId,
            MinWidth = 320
        };

        var dialog = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Content = CreateContent(nameInput, hotkeyInput, layoutInput, out var okButton, out var cancelButton)
        };

        dialog.Loaded += (_, _) =>
        {
            nameInput.Focus();
            nameInput.SelectAll();
        };
        nameInput.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                dialog.DialogResult = true;
            }
        };
        okButton.Click += (_, _) => dialog.DialogResult = true;
        cancelButton.Click += (_, _) => dialog.DialogResult = false;

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        var name = nameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new ProfileDialogResult(
            name,
            string.IsNullOrWhiteSpace(hotkeyInput.Text) ? null : hotkeyInput.Text.Trim(),
            layoutInput.SelectedValue as string);
    }

    private static UIElement CreateContent(
        WpfTextBox nameInput,
        WpfTextBox hotkeyInput,
        WpfComboBox layoutInput,
        out WpfButton okButton,
        out WpfButton cancelButton)
    {
        var panel = new StackPanel { Margin = new Thickness(18) };
        AddField(panel, "Profile name", nameInput);
        AddField(panel, "Hotkey", hotkeyInput);
        AddField(panel, "Cursor layout", layoutInput);

        var note = new TextBlock
        {
            Text = "H2 preset binding will be added in the next step. New profiles use cursor-layout-only mode for now.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0),
            Opacity = 0.72
        };
        panel.Children.Add(note);

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

    private static void AddField(WpfPanel panel, string label, FrameworkElement input)
    {
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 4)
        });
        panel.Children.Add(input);
    }
}
