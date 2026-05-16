using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using H2CursorRouter.App.ViewModels;
using H2CursorRouter.Windows;

namespace H2CursorRouter.App;

public partial class MainWindow : Window
{
    private const int EmergencyHotkeyId = 1;
    private const int ProfileHotkeyStartId = 100;
    private readonly MainViewModel _viewModel;
    private readonly IHotkeyService _hotkeyService;
    private HwndSource? _source;

    public MainWindow(MainViewModel viewModel, IHotkeyService hotkeyService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _hotkeyService = hotkeyService;
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

        for (var i = 0; i < _viewModel.Profiles.Count; i++)
        {
            var profile = _viewModel.Profiles[i];
            if (HotkeyParser.TryParse(profile.Hotkey, out var gesture))
            {
                _hotkeyService.RegisterHotkey(handle, ProfileHotkeyStartId + i, gesture);
            }
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
        _source?.RemoveHook(WndProc);
        _viewModel.Shutdown();
    }
}
