using System;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;
using Yokko.Desktop.Diagnostics;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class WindowsDebugConsoleWindowTest
{
    private const uint wmClose = 0x0010;

    [Test]
    public void ClosingWindowRaisesCloseRequestWithoutTerminatingProcess()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Ignore("Native debug window is Windows-only.");

        using var closeRequested = new ManualResetEventSlim();
        using var console = new WindowsDebugConsoleWindow();
        console.CloseRequested += closeRequested.Set;
        console.SetVisible(true);

        Assert.That(
            SpinWait.SpinUntil(
                () => console.WindowHandle != 0
                      || console.WindowCreationError != 0,
                TimeSpan.FromSeconds(2)),
            Is.True,
            "The native debug window did not finish creating.");
        Assert.That(
            console.WindowCreationError,
            Is.Zero,
            $"The native debug window failed with Win32 error {console.WindowCreationError}.");

        Assert.That(PostMessage(console.WindowHandle, wmClose, 0, 0), Is.True);
        Assert.That(closeRequested.Wait(TimeSpan.FromSeconds(2)), Is.True);
    }

    [Test]
    public void ImmediateDisposeStopsWindowThread()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Ignore("Native debug window is Windows-only.");

        var console = new WindowsDebugConsoleWindow();
        console.Dispose();

        Assert.That(console.WindowThreadAlive, Is.False);
    }

    [DllImport("user32.dll")]
    private static extern bool PostMessage(
        nint window,
        uint message,
        nint wordParameter,
        nint longParameter);
}
