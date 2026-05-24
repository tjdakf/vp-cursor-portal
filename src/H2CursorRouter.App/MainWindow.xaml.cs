using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using H2CursorRouter.App.ViewModels;
using H2CursorRouter.Windows;
using Forms = System.Windows.Forms;

namespace H2CursorRouter.App;

public partial class MainWindow : Window
{
    private const int EmergencyHotkeyId = 1;
    private const int ProfileHotkeyStartId = 100;
    private readonly MainViewModel _viewModel;
    private readonly IHotkeyService _hotkeyService;
    private readonly bool _startInTray;
    private readonly List<int> _registeredProfileHotkeys = new();
    private HwndSource? _source;
    private Forms.NotifyIcon? _notifyIcon;
    private System.Drawing.Icon? _trayIcon;
    private bool _allowExit;
    private bool _isInitialDashboardSelection = true;

    public MainWindow(MainViewModel viewModel, IHotkeyService hotkeyService, bool startInTray)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _hotkeyService = hotkeyService;
        _startInTray = startInTray;
        _viewModel.HotkeysChanged += OnHotkeysChanged;
        DataContext = viewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WndProc);

        if (!_hotkeyService.RegisterHotkey(handle, EmergencyHotkeyId, HotkeyGesture.CtrlAltShiftEsc))
        {
            _viewModel.AddLog("Failed to register emergency hotkey Ctrl+Alt+Shift+Esc.");
        }

        RegisterProfileHotkeys(handle);
        InitializeTrayIcon();
        _ = _viewModel.RefreshDashboardStatusAsync();

        if (_startInTray)
        {
            HideToTray();
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_hotkeyService.IsHotkeyMessage(hwnd, msg, wParam, lParam, out var id))
        {
            handled = true;
            if (id == EmergencyHotkeyId)
            {
                _viewModel.EmergencyUnlock();
            }
            else if (id >= ProfileHotkeyStartId)
            {
                _ = _viewModel.ExecuteProfileByIndexAsync(id - ProfileHotkeyStartId);
            }
        }

        return IntPtr.Zero;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_allowExit)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        _viewModel.HotkeysChanged -= OnHotkeysChanged;
        _source?.RemoveHook(WndProc);
        _notifyIcon?.Dispose();
        _trayIcon?.Dispose();
        _viewModel.Shutdown();
    }

    private void OnHotkeysChanged(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        RegisterProfileHotkeys(handle);
    }

    private void RegisterProfileHotkeys(IntPtr handle)
    {
        foreach (var id in _registeredProfileHotkeys)
        {
            _hotkeyService.UnregisterHotkey(handle, id);
        }

        _registeredProfileHotkeys.Clear();
        for (var i = 0; i < _viewModel.Profiles.Count; i++)
        {
            var profile = _viewModel.Profiles[i];
            if (HotkeyParser.TryParse(profile.Hotkey, out var gesture))
            {
                var id = ProfileHotkeyStartId + i;
                if (_hotkeyService.RegisterHotkey(handle, id, gesture))
                {
                    _registeredProfileHotkeys.Add(id);
                }
                else
                {
                    _viewModel.AddLog($"Failed to register profile hotkey '{profile.Hotkey}' for '{profile.Name}'.");
                }
            }
        }
    }

    private void InitializeTrayIcon()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowFromTray());
        menu.Items.Add("Stop Routing", null, (_, _) => _viewModel.StopRoutingCommand.Execute(null));
        menu.Items.Add("Emergency Unlock", null, (_, _) => _viewModel.EmergencyUnlock());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _allowExit = true;
            Close();
        });

        _trayIcon = LoadTrayIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _trayIcon,
            Text = "vp-cursor-portal",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/tray.ico", UriKind.Absolute));
        if (resource is not null)
        {
            return new System.Drawing.Icon(resource.Stream);
        }

        var executableIcon = !string.IsNullOrWhiteSpace(Environment.ProcessPath)
            ? System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath)
            : null;
        return executableIcon ?? (System.Drawing.Icon)SystemIcons.Application.Clone();
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        _viewModel.AddLog("Window hidden to tray; routing state is unchanged.");
    }

    private void ShowFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AboutButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AppDialogWindow("About vp-cursor-portal", CreateAboutContent())
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private UIElement CreateAboutContent()
    {
        var version = Assembly.GetExecutingAssembly()
                          .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                          ?.InformationalVersion
                      ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                      ?? "unknown";

        var panel = new StackPanel
        {
            Width = 560
        };

        panel.Children.Add(new System.Windows.Controls.Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute)),
            Width = 72,
            Height = 72,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 14)
        });
        panel.Children.Add(CreateAboutText("vp-cursor-portal", 24, FontWeights.SemiBold, "TextBrush"));
        panel.Children.Add(CreateAboutText($"Version {version}", 13, FontWeights.Normal, "MutedTextBrush"));
        panel.Children.Add(CreateAboutText(
            "Windows cursor routing and H2 preset control for a single NovaStar H Series / H2 processor setup.",
            14,
            FontWeights.Normal,
            "TextBrush",
            new Thickness(0, 16, 0, 14)));

        panel.Children.Add(CreateAboutText("Paths", 15, FontWeights.SemiBold, "TextBrush"));
        panel.Children.Add(CreateAboutText($"Config: {_viewModel.ConfigPath}", 12, FontWeights.Normal, "MutedTextBrush"));
        panel.Children.Add(CreateAboutText($"Logs: {_viewModel.LogDirectory}", 12, FontWeights.Normal, "MutedTextBrush", new Thickness(0, 2, 0, 14)));

        panel.Children.Add(CreateAboutText("License", 15, FontWeights.SemiBold, "TextBrush"));
        panel.Children.Add(CreateAboutText(
            "No public open-source license is declared yet. Treat this build as proprietary/internal unless a separate license is provided.",
            12,
            FontWeights.Normal,
            "MutedTextBrush"));

        return panel;
    }

    private TextBlock CreateAboutText(
        string text,
        double fontSize,
        FontWeight fontWeight,
        string brushResource,
        Thickness? margin = null) =>
        new()
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = fontWeight,
            TextWrapping = TextWrapping.Wrap,
            Foreground = TryFindResource(brushResource) as System.Windows.Media.Brush,
            Margin = margin ?? new Thickness(0, 0, 0, 4)
        };

    private void MainTabs_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, MainTabs))
        {
            return;
        }

        if (MainTabs.SelectedIndex == 0)
        {
            if (_isInitialDashboardSelection)
            {
                _isInitialDashboardSelection = false;
                return;
            }

            _ = _viewModel.RefreshDashboardStatusAsync();
        }
        else if (MainTabs.SelectedIndex == 4)
        {
            _viewModel.RefreshDisplays();
        }
    }

    private void EditProfileButton_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as System.Windows.Controls.Button)?.Tag is ProfileRow profile)
        {
            _viewModel.SelectedProfile = profile;
            _viewModel.EditProfile(profile);
        }
    }

    private void ProfilesGrid_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if ((sender as System.Windows.Controls.DataGrid)?.SelectedItem is ProfileRow profile)
        {
            _viewModel.EditProfile(profile);
        }
    }

    private void ZoneMoveThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ZoneRow zone)
        {
            _viewModel.MoveZoneVisual(zone, e.HorizontalChange, e.VerticalChange);
        }
    }

    private void ZoneMoveThumb_OnDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ZoneRow zone)
        {
            _viewModel.CompleteZoneVisualEdit(zone);
        }
    }

    private void ZoneResizeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ZoneRow zone)
        {
            _viewModel.ResizeZoneVisual(zone, e.HorizontalChange, e.VerticalChange);
        }
    }

    private void ZoneVisual_OnPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ZoneRow zone)
        {
            _viewModel.SelectedZone = zone;
            e.Handled = false;
        }
    }

    private void LayoutNumberTextBox_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter || sender is not System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        if (textBox.DataContext is MainViewModel { SelectedZone: not null } viewModel)
        {
            viewModel.CompleteZoneVisualEdit(viewModel.SelectedZone);
        }

        e.Handled = true;
    }

    private void CanvasScrollViewer_OnPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        LayoutTabScrollViewer.ScrollToVerticalOffset(LayoutTabScrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }
}
