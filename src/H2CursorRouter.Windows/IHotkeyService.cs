namespace H2CursorRouter.Windows;

public interface IHotkeyService : IDisposable
{
    bool RegisterHotkey(IntPtr windowHandle, int id, HotkeyGesture gesture);
    void UnregisterHotkey(IntPtr windowHandle, int id);
    bool IsHotkeyMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, out int id);
}
