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
        string? selectedHotkey,
        IReadOnlyList<LayoutRow> layouts,
        string? selectedLayoutId,
        bool isCursorLayoutOnly,
        IReadOnlyList<DeviceRow> devices,
        IReadOnlyList<PresetRow> presets,
        string? selectedDeviceId,
        int? selectedScreenId,
        int? selectedPresetId,
        string? selectedPresetDisplayName)
    {
        var nameInput = new WpfTextBox { Text = defaultName, MinWidth = 320 };
        var hotkeyInput = new WpfTextBox
        {
            Text = selectedHotkey ?? "",
            MinWidth = 212,
            IsReadOnly = true
        };
        var recordHotkeyButton = new WpfButton
        {
            Content = "Record",
            MinWidth = 76,
            Style = WpfApplication.Current.TryFindResource("SecondaryButtonStyle") as Style,
            Margin = new Thickness(8, 0, 8, 0)
        };
        var clearHotkeyButton = new WpfButton
        {
            Content = "Clear",
            MinWidth = 64,
            Style = WpfApplication.Current.TryFindResource("SecondaryButtonStyle") as Style,
            Margin = new Thickness(0)
        };
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
            IsChecked = isCursorLayoutOnly,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var deviceChoices = devices.ToArray();
        var deviceInput = new WpfComboBox
        {
            ItemsSource = deviceChoices,
            DisplayMemberPath = nameof(DeviceRow.Name),
            SelectedValuePath = nameof(DeviceRow.Id),
            SelectedValue = selectedDeviceId,
            MinWidth = 320,
            IsEnabled = false
        };
        if (deviceInput.SelectedIndex < 0 && deviceChoices.Length > 0)
        {
            deviceInput.SelectedIndex = 0;
        }

        var presetInput = new WpfComboBox
        {
            DisplayMemberPath = nameof(PresetRow.DisplayName),
            MinWidth = 320,
            IsEnabled = false
        };

        void RefreshPresetChoices()
        {
            var currentDeviceId = deviceInput.SelectedValue as string;
            var presetChoices = presets
                .Where(preset => string.Equals(preset.DeviceConfigId, currentDeviceId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(preset => preset.ScreenId)
                .ThenBy(preset => preset.FriendlyPresetNumber)
                .ToList();
            if (presetChoices.All(preset => preset.ScreenId != selectedScreenId || preset.PresetId != selectedPresetId) &&
                !string.IsNullOrWhiteSpace(currentDeviceId) &&
                string.Equals(currentDeviceId, selectedDeviceId, StringComparison.OrdinalIgnoreCase) &&
                selectedScreenId is not null &&
                selectedPresetId is not null)
            {
                var deviceName = devices.FirstOrDefault(device => string.Equals(device.Id, currentDeviceId, StringComparison.OrdinalIgnoreCase))?.Name ?? currentDeviceId;
                presetChoices.Add(new PresetRow
                {
                    DeviceConfigId = currentDeviceId,
                    DeviceName = deviceName,
                    ScreenId = selectedScreenId.Value,
                    FriendlyPresetNumber = selectedPresetId.Value + 1,
                    PresetId = selectedPresetId.Value,
                    DisplayName = string.IsNullOrWhiteSpace(selectedPresetDisplayName)
                        ? $"Preset {selectedPresetId.Value + 1}"
                        : selectedPresetDisplayName
                });
            }

            presetInput.ItemsSource = presetChoices.ToArray();
            presetInput.SelectedItem = !string.IsNullOrWhiteSpace(currentDeviceId) &&
                                       string.Equals(currentDeviceId, selectedDeviceId, StringComparison.OrdinalIgnoreCase)
                ? presetInput.Items
                    .OfType<PresetRow>()
                    .FirstOrDefault(preset => preset.ScreenId == selectedScreenId && preset.PresetId == selectedPresetId)
                : null;
            if (presetInput.SelectedIndex < 0)
            {
                presetInput.SelectedIndex = presetInput.Items.Count > 0 ? 0 : -1;
            }
        }

        void RefreshH2Enabled()
        {
            var enabled = cursorOnlyInput.IsChecked != true;
            deviceInput.IsEnabled = enabled;
            presetInput.IsEnabled = enabled;
            RefreshPresetChoices();
        }

        var dialog = new AppDialogWindow(title, CreateContent(nameInput, hotkeyInput, recordHotkeyButton, clearHotkeyButton, layoutInput, cursorOnlyInput, deviceInput, presetInput, out var okButton, out var cancelButton))
        {
            Width = 460,
        };
        var isRecordingHotkey = false;
        recordHotkeyButton.Click += (_, _) =>
        {
            isRecordingHotkey = true;
            recordHotkeyButton.Content = "Press keys";
            hotkeyInput.Text = "";
            hotkeyInput.Focus();
        };
        clearHotkeyButton.Click += (_, _) =>
        {
            isRecordingHotkey = false;
            recordHotkeyButton.Content = "Record";
            hotkeyInput.Text = "";
        };
        dialog.PreviewKeyDown += (_, args) =>
        {
            if (!isRecordingHotkey)
            {
                return;
            }

            var formatted = FormatHotkey(args);
            if (formatted is null)
            {
                recordHotkeyButton.Content = "Press keys";
                args.Handled = true;
                return;
            }

            hotkeyInput.Text = formatted;
            isRecordingHotkey = false;
            recordHotkeyButton.Content = "Record";
            args.Handled = true;
        };
        cursorOnlyInput.Checked += (_, _) => RefreshH2Enabled();
        cursorOnlyInput.Unchecked += (_, _) => RefreshH2Enabled();
        deviceInput.SelectionChanged += (_, _) => RefreshPresetChoices();
        RefreshH2Enabled();

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
        WpfButton recordHotkeyButton,
        WpfButton clearHotkeyButton,
        WpfComboBox layoutInput,
        WpfCheckBox cursorOnlyInput,
        WpfComboBox deviceInput,
        WpfComboBox presetInput,
        out WpfButton okButton,
        out WpfButton cancelButton)
    {
        var panel = new StackPanel();
        AddField(panel, "Profile name", nameInput);
        AddField(panel, "Hotkey", CreateHotkeyInput(hotkeyInput, recordHotkeyButton, clearHotkeyButton));
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

    private static FrameworkElement CreateHotkeyInput(WpfTextBox hotkeyInput, WpfButton recordHotkeyButton, WpfButton clearHotkeyButton)
    {
        var panel = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal
        };
        panel.Children.Add(hotkeyInput);
        panel.Children.Add(recordHotkeyButton);
        panel.Children.Add(clearHotkeyButton);
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

    private static string? FormatHotkey(System.Windows.Input.KeyEventArgs args)
    {
        var key = args.Key == Key.System ? args.SystemKey : args.Key;
        key = key == Key.ImeProcessed ? args.ImeProcessedKey : key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return null;
        }

        var modifiers = Keyboard.Modifiers;
        if (key == Key.Escape && modifiers == ModifierKeys.None)
        {
            return null;
        }

        if (key == Key.Escape &&
            modifiers.HasFlag(ModifierKeys.Control) &&
            modifiers.HasFlag(ModifierKeys.Alt) &&
            modifiers.HasFlag(ModifierKeys.Shift))
        {
            return null;
        }

        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        if (parts.Count == 0)
        {
            return null;
        }

        var keyText = FormatKey(key);
        if (string.IsNullOrWhiteSpace(keyText))
        {
            return null;
        }

        parts.Add(keyText);
        var hotkey = string.Join("+", parts);
        return hotkey.Equals("Ctrl+Alt+Shift+Esc", StringComparison.OrdinalIgnoreCase)
            ? null
            : hotkey;
    }

    private static string FormatKey(Key key)
    {
        if (key == Key.Escape)
        {
            return "Esc";
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            return ((int)key - (int)Key.D0).ToString();
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            return ((int)key - (int)Key.NumPad0).ToString();
        }

        if (key is >= Key.A and <= Key.Z)
        {
            return key.ToString();
        }

        if (key is >= Key.F1 and <= Key.F24)
        {
            return key.ToString();
        }

        return "";
    }
}
