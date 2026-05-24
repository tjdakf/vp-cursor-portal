using System.Runtime.InteropServices;

namespace H2CursorRouter.Windows;

public sealed class Win32HotkeyService : IHotkeyService
{
    private const int WmHotkey = 0x0312;
    private readonly HashSet<(IntPtr Window, int Id)> _registered = new();

    public bool RegisterHotkey(IntPtr windowHandle, int id, HotkeyGesture gesture)
    {
        var ok = RegisterHotKey(windowHandle, id, (uint)gesture.Modifiers, gesture.VirtualKey);
        if (ok)
        {
            _registered.Add((windowHandle, id));
        }

        return ok;
    }

    public void UnregisterHotkey(IntPtr windowHandle, int id)
    {
        UnregisterHotKey(windowHandle, id);
        _registered.Remove((windowHandle, id));
    }

    public bool IsHotkeyMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, out int id)
    {
        id = 0;
        if (message != WmHotkey)
        {
            return false;
        }

        id = wParam.ToInt32();
        return true;
    }

    public void Dispose()
    {
        foreach (var registration in _registered.ToArray())
        {
            UnregisterHotKey(registration.Window, registration.Id);
        }

        _registered.Clear();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
