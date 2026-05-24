namespace H2CursorRouter.Windows;

public static class HotkeyParser
{
    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = HotkeyGesture.CtrlAltShiftEsc;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        uint key = 0;
        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifiers.Control;
            }
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifiers.Alt;
            }
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifiers.Shift;
            }
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifiers.Win;
            }
            else if (part.Equals("Esc", StringComparison.OrdinalIgnoreCase) ||
                     part.Equals("Escape", StringComparison.OrdinalIgnoreCase))
            {
                key = 0x1B;
            }
            else if (part.Length == 1)
            {
                key = char.ToUpperInvariant(part[0]);
            }
            else if (part.StartsWith('F') && uint.TryParse(part[1..], out var functionKey) && functionKey is >= 1 and <= 24)
            {
                key = 0x70 + functionKey - 1;
            }
            else if (uint.TryParse(part, out var numericKey) && numericKey <= 9)
            {
                key = 0x30 + numericKey;
            }
        }

        if (key == 0)
        {
            return false;
        }

        gesture = new HotkeyGesture(modifiers, key);
        return true;
    }
}
