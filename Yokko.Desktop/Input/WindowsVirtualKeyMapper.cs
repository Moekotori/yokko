using osuTK.Input;

namespace Yokko.Desktop.Input;

internal static class WindowsVirtualKeyMapper
{
    private const ushort key_e0 = 0x0002;

    public static bool TryMap(
        ushort virtualKey,
        ushort makeCode,
        ushort flags,
        out Key key)
    {
        bool extended = (flags & key_e0) != 0;

        if (virtualKey is >= 0x41 and <= 0x5a)
        {
            key = (Key)((int)Key.A + virtualKey - 0x41);
            return true;
        }

        if (virtualKey is >= 0x30 and <= 0x39)
        {
            key = (Key)((int)Key.Number0 + virtualKey - 0x30);
            return true;
        }

        if (virtualKey is >= 0x70 and <= 0x87)
        {
            key = (Key)((int)Key.F1 + virtualKey - 0x70);
            return true;
        }

        if (virtualKey is >= 0x60 and <= 0x69)
        {
            key = (Key)((int)Key.Keypad0 + virtualKey - 0x60);
            return true;
        }

        key = virtualKey switch
        {
            0x08 => Key.BackSpace,
            0x09 => Key.Tab,
            0x0c => Key.Clear,
            0x0d when extended => Key.KeypadEnter,
            0x0d => Key.Enter,
            0x10 when makeCode == 0x36 => Key.ShiftRight,
            0x10 => Key.ShiftLeft,
            0x11 when extended => Key.ControlRight,
            0x11 => Key.ControlLeft,
            0x12 when extended => Key.AltRight,
            0x12 => Key.AltLeft,
            0x13 => Key.Pause,
            0x14 => Key.CapsLock,
            0x1b => Key.Escape,
            0x20 => Key.Space,
            0x21 when !extended => Key.Keypad9,
            0x21 => Key.PageUp,
            0x22 when !extended => Key.Keypad3,
            0x22 => Key.PageDown,
            0x23 when !extended => Key.Keypad1,
            0x23 => Key.End,
            0x24 when !extended => Key.Keypad7,
            0x24 => Key.Home,
            0x25 when !extended => Key.Keypad4,
            0x25 => Key.Left,
            0x26 when !extended => Key.Keypad8,
            0x26 => Key.Up,
            0x27 when !extended => Key.Keypad6,
            0x27 => Key.Right,
            0x28 when !extended => Key.Keypad2,
            0x28 => Key.Down,
            0x2c => Key.PrintScreen,
            0x2d when !extended => Key.Keypad0,
            0x2d => Key.Insert,
            0x2e when !extended => Key.KeypadDecimal,
            0x2e => Key.Delete,
            0x5b => Key.WinLeft,
            0x5c => Key.WinRight,
            0x5d => Key.Menu,
            0x5f => Key.Sleep,
            0x6a => Key.KeypadMultiply,
            0x6b => Key.KeypadAdd,
            0x6d => Key.KeypadSubtract,
            0x6e => Key.KeypadDecimal,
            0x6f => Key.KeypadDivide,
            0x90 => Key.NumLock,
            0x91 => Key.ScrollLock,
            0xa0 => Key.ShiftLeft,
            0xa1 => Key.ShiftRight,
            0xa2 => Key.ControlLeft,
            0xa3 => Key.ControlRight,
            0xa4 => Key.AltLeft,
            0xa5 => Key.AltRight,
            0xad => Key.Mute,
            0xae => Key.VolumeDown,
            0xaf => Key.VolumeUp,
            0xb0 => Key.TrackNext,
            0xb1 => Key.TrackPrevious,
            0xb2 => Key.Stop,
            0xb3 => Key.PlayPause,
            0xba => Key.Semicolon,
            0xbb => Key.Plus,
            0xbc => Key.Comma,
            0xbd => Key.Minus,
            0xbe => Key.Period,
            0xbf => Key.Slash,
            0xc0 => Key.Grave,
            0xdb => Key.BracketLeft,
            0xdc => Key.BackSlash,
            0xdd => Key.BracketRight,
            0xde => Key.Quote,
            0xe2 => Key.NonUSBackSlash,
            _ => Key.Unknown,
        };

        return key != Key.Unknown;
    }
}
