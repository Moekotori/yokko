using NUnit.Framework;
using osuTK.Input;
using Yokko.Desktop.Input;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class WindowsVirtualKeyMapperTest
{
    [TestCase(0x44, 0x20, 0, Key.D)]
    [TestCase(0x46, 0x21, 0, Key.F)]
    [TestCase(0x4a, 0x24, 0, Key.J)]
    [TestCase(0x4b, 0x25, 0, Key.K)]
    [TestCase(0x20, 0x39, 0, Key.Space)]
    [TestCase(0x0d, 0x1c, 0, Key.Enter)]
    [TestCase(0x0d, 0x1c, 0x0002, Key.KeypadEnter)]
    [TestCase(0x11, 0x1d, 0x0002, Key.ControlRight)]
    [TestCase(0x10, 0x36, 0, Key.ShiftRight)]
    public void MapsWindowsKeyboardIdentity(
        int virtualKey,
        int makeCode,
        int flags,
        Key expected)
    {
        Assert.That(
            WindowsVirtualKeyMapper.TryMap(
                (ushort)virtualKey,
                (ushort)makeCode,
                (ushort)flags,
                out Key actual),
            Is.True);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(0x2d, 0x52, 0, Key.Keypad0)]
    [TestCase(0x2d, 0x52, 0x0002, Key.Insert)]
    [TestCase(0x26, 0x48, 0, Key.Keypad8)]
    [TestCase(0x26, 0x48, 0x0002, Key.Up)]
    public void DistinguishesNavigationKeysFromNumpad(
        int virtualKey,
        int makeCode,
        int flags,
        Key expected)
    {
        Assert.That(
            WindowsVirtualKeyMapper.TryMap(
                (ushort)virtualKey,
                (ushort)makeCode,
                (ushort)flags,
                out Key actual),
            Is.True);
        Assert.That(actual, Is.EqualTo(expected));
    }
}
