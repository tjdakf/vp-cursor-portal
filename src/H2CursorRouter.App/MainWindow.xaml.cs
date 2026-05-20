using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
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
    private bool _allowExit;

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

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "vp-cursor-portal",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => ShowFromTray();
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

    private void MainTabs_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, MainTabs) || MainTabs.SelectedIndex != 0)
        {
            return;
        }

        _ = _viewModel.RefreshDashboardStatusAsync();
    }

    private void EditProfileButton_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ProfileRow profile)
        {
            _viewModel.SelectedProfile = profile;
            MainTabs.SelectedIndex = 2;
        }
    }

    private void ZoneMoveThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ZoneRow zone)
        {
            _viewModel.MoveZoneVisual(zone, e.HorizontalChange, e.VerticalChange);
        }
    }

    private void ZoneResizeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ZoneRow zone)
        {
            _viewModel.ResizeZoneVisual(zone, e.HorizontalChange, e.VerticalChange);
        }
    }
}
