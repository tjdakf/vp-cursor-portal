using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApplication = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfPanel = System.Windows.Controls.Panel;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace H2CursorRouter.App;

public sealed class DeviceDialogService : IDeviceDialogService
{
    public DeviceDialogResult? Prompt(string defaultName, string defaultHost, int defaultPort)
    {
        var nameInput = new WpfTextBox { Text = defaultName, MinWidth = 320 };
        var octets = CreateOctetInputs(defaultHost);
        var portInput = new WpfTextBox { Text = defaultPort.ToString(), MinWidth = 320 };
        ConfigureNumericInput(portInput);
        var errorText = new TextBlock
        {
            Foreground = WpfApplication.Current.TryFindResource("DangerBrush") as System.Windows.Media.Brush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var dialog = new AppDialogWindow(
            "Add device",
            CreateContent(nameInput, octets, portInput, errorText, out var okButton, out var cancelButton))
        {
            Width = 460
        };

        okButton.Click += (_, _) =>
        {
            if (!TryCreateResult(nameInput, octets, portInput, out var error, out _))
            {
                errorText.Text = error;
                return;
            }

            dialog.DialogResult = true;
        };
        cancelButton.Click += (_, _) => dialog.DialogResult = false;
        dialog.Loaded += (_, _) =>
        {
            nameInput.Focus();
            nameInput.SelectAll();
        };
        dialog.PreviewKeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter)
            {
                return;
            }

            okButton.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent));
            args.Handled = true;
        };

        return dialog.ShowDialog() == true && TryCreateResult(nameInput, octets, portInput, out _, out var result)
            ? result
            : null;
    }

    private static WpfTextBox[] CreateOctetInputs(string host)
    {
        var values = host.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (values.Length != 4 || values.Any(value => !int.TryParse(value, out var octet) || octet < 0 || octet > 255))
        {
            values = ["192", "168", "0", "11"];
        }

        return values.Select(value =>
        {
            var input = new WpfTextBox
            {
                Text = value,
                Width = 64,
                MaxLength = 3,
                HorizontalContentAlignment = WpfHorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            ConfigureNumericInput(input);
            return input;
        }).ToArray();
    }

    private static UIElement CreateContent(
        WpfTextBox nameInput,
        WpfTextBox[] octets,
        WpfTextBox portInput,
        TextBlock errorText,
        out WpfButton okButton,
        out WpfButton cancelButton)
    {
        var panel = new StackPanel();
        AddField(panel, "Device name", nameInput);
        AddField(panel, "IP address", CreateIpInput(octets));
        AddField(panel, "Port", portInput);
        panel.Children.Add(errorText);

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

    private static FrameworkElement CreateIpInput(IEnumerable<WpfTextBox> octets)
    {
        var panel = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal
        };
        var index = 0;
        foreach (var octet in octets)
        {
            panel.Children.Add(octet);
            if (++index < 4)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = ".",
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
        }

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

    private static void ConfigureNumericInput(WpfTextBox input)
    {
        input.PreviewTextInput += (_, args) => args.Handled = !IsDigitsOnly(args.Text);
        System.Windows.DataObject.AddPastingHandler(input, (_, args) =>
        {
            if (!args.DataObject.GetDataPresent(System.Windows.DataFormats.Text))
            {
                args.CancelCommand();
                return;
            }

            var data = args.DataObject.GetData(System.Windows.DataFormats.Text);
            if (data is not string text || !IsDigitsOnly(text))
            {
                args.CancelCommand();
            }
        });
    }

    private static bool IsDigitsOnly(string text) => text.All(char.IsDigit);

    private static bool TryCreateResult(
        WpfTextBox nameInput,
        IReadOnlyList<WpfTextBox> octets,
        WpfTextBox portInput,
        out string error,
        out DeviceDialogResult? result)
    {
        result = null;
        var name = nameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Device name is required.";
            return false;
        }

        var octetValues = new List<int>();
        foreach (var octet in octets)
        {
            if (!int.TryParse(octet.Text.Trim(), out var value) || value is < 0 or > 255)
            {
                error = "IP address must use four numbers from 0 to 255.";
                return false;
            }

            octetValues.Add(value);
        }

        if (!int.TryParse(portInput.Text.Trim(), out var port) || port is < 1 or > 65535)
        {
            error = "Port must be between 1 and 65535.";
            return false;
        }

        error = "";
        result = new DeviceDialogResult(name, string.Join(".", octetValues), port);
        return true;
    }
}
