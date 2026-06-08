using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using H2CursorRouter.App.ViewModels;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;

namespace H2CursorRouter.App;

public sealed class DisplayIdentificationService : IDisplayIdentificationService
{
    public async Task IdentifyAsync(IReadOnlyList<MonitorRow> monitors, TimeSpan duration)
    {
        if (monitors.Count == 0)
        {
            return;
        }

        var windows = monitors.Select(CreateWindow).ToArray();
        try
        {
            foreach (var window in windows)
            {
                window.Show();
            }

            await Task.Delay(duration);
        }
        finally
        {
            foreach (var window in windows)
            {
                window.Close();
            }
        }
    }

    private static Window CreateWindow(MonitorRow monitor)
    {
        const double width = 320;
        const double height = 180;

        var window = new Window
        {
            Width = width,
            Height = height,
            Left = monitor.Left + (monitor.Width - width) / 2.0,
            Top = monitor.Top + (monitor.Height - height) / 2.0,
            WindowStartupLocation = WindowStartupLocation.Manual,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = MediaBrushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            Focusable = false,
            Content = CreateContent(monitor)
        };
        window.SourceInitialized += (_, _) =>
        {
            CenterWindowInMonitorBounds(window, monitor);
            window.Dispatcher.BeginInvoke(
                () => CenterWindowInMonitorBounds(window, monitor),
                DispatcherPriority.Loaded);
        };

        return window;
    }

    private static void CenterWindowInMonitorBounds(Window window, MonitorRow monitor)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rect))
        {
            return;
        }

        var width = Math.Max(1, rect.Right - rect.Left);
        var height = Math.Max(1, rect.Bottom - rect.Top);
        var left = monitor.Left + (monitor.Width - width) / 2;
        var top = monitor.Top + (monitor.Height - height) / 2;

        SetWindowPos(
            handle,
            HwndTopmost,
            left,
            top,
            0,
            0,
            SetWindowPosFlags.NoSize |
            SetWindowPosFlags.NoActivate |
            SetWindowPosFlags.NoOwnerZOrder);
    }

    private static UIElement CreateContent(MonitorRow monitor)
    {
        var primaryLabel = monitor.IsPrimary ? "Primary display" : "Detected display";
        var boundsLabel = $"{monitor.Left},{monitor.Top} -> {monitor.Right},{monitor.Bottom}";

        var title = new TextBlock
        {
            Text = monitor.DisplayLabel,
            Foreground = MediaBrushes.White,
            FontSize = 42,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        var subtitle = new TextBlock
        {
            Text = primaryLabel,
            Foreground = new MediaSolidColorBrush(MediaColor.FromRgb(204, 251, 241)),
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var bounds = new TextBlock
        {
            Text = boundsLabel,
            Foreground = new MediaSolidColorBrush(MediaColor.FromRgb(203, 213, 225)),
            FontSize = 13,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var stack = new StackPanel
        {
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(title);
        stack.Children.Add(subtitle);
        stack.Children.Add(bounds);

        return new Border
        {
            Background = new MediaSolidColorBrush(MediaColor.FromArgb(235, 15, 23, 42)),
            BorderBrush = new MediaSolidColorBrush(MediaColor.FromRgb(45, 212, 191)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(22),
            Child = stack
        };
    }

    private static readonly IntPtr HwndTopmost = new(-1);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        SetWindowPosFlags flags);

    [Flags]
    private enum SetWindowPosFlags : uint
    {
        NoSize = 0x0001,
        NoActivate = 0x0010,
        NoOwnerZOrder = 0x0200
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
