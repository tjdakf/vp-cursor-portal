using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using H2CursorRouter.App.ViewModels;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfPanel = System.Windows.Controls.Panel;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfApplication = System.Windows.Application;

namespace H2CursorRouter.App;

public sealed class ProfileDialogService : IProfileDialogService
{
    public ProfileDialogResult? Prompt(
        string title,
        string defaultName,
        IReadOnlyList<LayoutRow> layouts,
        string? selectedLayoutId,
        IReadOnlyList<DeviceRow> devices,
        IReadOnlyList<PresetRow> presets)
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
        var cursorOnlyInput = new WpfCheckBox
        {
            Content = "Cursor layout only",
            IsChecked = true,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var onlineDevices = devices.Where(device => device.IsOnline).DefaultIfEmpty(devices.FirstOrDefault()).Where(device => device is not null).ToArray();
        var deviceInput = new WpfComboBox
        {
            ItemsSource = onlineDevices,
            DisplayMemberPath = nameof(DeviceRow.Name),
            SelectedValuePath = nameof(DeviceRow.Id),
            SelectedIndex = onlineDevices.Length > 0 ? 0 : -1,
            MinWidth = 320,
            IsEnabled = false
        };
        var presetInput = new WpfComboBox
        {
            DisplayMemberPath = nameof(PresetRow.DisplayName),
            MinWidth = 320,
            IsEnabled = false
        };

        void RefreshPresetChoices()
        {
            var selectedDeviceId = deviceInput.SelectedValue as string;
            presetInput.ItemsSource = presets
                .Where(preset => string.Equals(preset.DeviceConfigId, selectedDeviceId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(preset => preset.ScreenId)
                .ThenBy(preset => preset.FriendlyPresetNumber)
                .ToArray();
            presetInput.SelectedIndex = presetInput.Items.Count > 0 ? 0 : -1;
        }

        void RefreshH2Enabled()
        {
            var enabled = cursorOnlyInput.IsChecked != true;
            deviceInput.IsEnabled = enabled;
            presetInput.IsEnabled = enabled;
            RefreshPresetChoices();
        }

        var dialog = new AppDialogWindow(title, CreateContent(nameInput, hotkeyInput, layoutInput, cursorOnlyInput, deviceInput, presetInput, out var okButton, out var cancelButton))
        {
            Width = 460,
        };
        cursorOnlyInput.Checked += (_, _) => RefreshH2Enabled();
        cursorOnlyInput.Unchecked += (_, _) => RefreshH2Enabled();
        deviceInput.SelectionChanged += (_, _) => RefreshPresetChoices();
        RefreshPresetChoices();

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

        var selectedPreset = cursorOnlyInput.IsChecked == true ? null : presetInput.SelectedItem as PresetRow;
        return new ProfileDialogResult(
            name,
            string.IsNullOrWhiteSpace(hotkeyInput.Text) ? null : hotkeyInput.Text.Trim(),
            layoutInput.SelectedValue as string,
            selectedPreset?.DeviceConfigId,
            selectedPreset?.ScreenId,
            selectedPreset?.PresetId,
            selectedPreset?.DisplayName);
    }

    private static UIElement CreateContent(
        WpfTextBox nameInput,
        WpfTextBox hotkeyInput,
        WpfComboBox layoutInput,
        WpfCheckBox cursorOnlyInput,
        WpfComboBox deviceInput,
        WpfComboBox presetInput,
        out WpfButton okButton,
        out WpfButton cancelButton)
    {
        var panel = new StackPanel();
        AddField(panel, "Profile name", nameInput);
        AddField(panel, "Hotkey", hotkeyInput);
        AddField(panel, "Cursor layout", layoutInput);
        panel.Children.Add(cursorOnlyInput);
        AddField(panel, "Video processor", deviceInput);
        AddField(panel, "Preset", presetInput);

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
            Style = WpfApplication.Current.TryFindResource("SecondaryButtonStyle") as Style,
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
            Foreground = WpfApplication.Current.TryFindResource("TextBrush") as System.Windows.Media.Brush,
            Margin = new Thickness(0, 12, 0, 4)
        });
        panel.Children.Add(input);
    }
}
