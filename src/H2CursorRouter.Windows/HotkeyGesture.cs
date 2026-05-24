namespace H2CursorRouter.Windows;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008
}

public sealed record HotkeyGesture(HotkeyModifiers Modifiers, uint VirtualKey)
{
    public static HotkeyGesture CtrlAltShiftEsc { get; } = new(
        HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift,
        0x1B);
}
