using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using osu.Framework.Platform;
using osuTK.Input;
using Yokko.Game.Input;

namespace Yokko.Desktop.Input;

internal sealed class WindowsRawKeyboardTimestampBackend : IKeyInputTimestampBackend
{
    private const uint wm_input = 0x00ff;
    private const uint rid_input = 0x10000003;
    private const uint rid_header = 0x10000005;
    private const uint rim_typekeyboard = 1;
    private const ushort hid_usage_page_generic = 0x01;
    private const ushort hid_usage_generic_keyboard = 0x06;
    private const uint ridev_remove = 0x00000001;
    private const ushort ri_key_break = 0x0001;
    private const ushort ri_key_e0 = 0x0002;
    private const ushort ri_key_e1 = 0x0004;
    private const ulong subclass_id_value = 0x594f4b4b;

    private static readonly UIntPtr subclass_id = new(subclass_id_value);

    private readonly object sync = new();
    private const int max_pending_edges = 1024;

    private readonly Queue<TimestampedKeyInput> pending = new();
    private readonly HashSet<RawKeyIdentity> pressedKeys = new();
    private readonly SubclassProcedure subclassProcedure;

    private IntPtr windowHandle;
    private bool isCapturing;
    private bool isAvailable;
    private bool disposed;

    public WindowsRawKeyboardTimestampBackend()
    {
        subclassProcedure = windowProcedure;
    }

    public string Name => "Windows Raw Input";

    public bool IsAvailable
    {
        get
        {
            lock (sync)
                return isAvailable;
        }
    }

    public bool Attach(IWindow window)
    {
        if (!OperatingSystem.IsWindows() || window == null)
            return false;

        lock (sync)
        {
            throwIfDisposed();
            detach();

            PropertyInfo handleProperty = window.GetType().GetProperty(
                "WindowHandle",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (handleProperty?.PropertyType != typeof(IntPtr))
                return false;

            windowHandle = (IntPtr)handleProperty.GetValue(window);
            if (windowHandle == IntPtr.Zero)
                return false;

            if (!SetWindowSubclass(
                    windowHandle,
                    subclassProcedure,
                    subclass_id,
                    UIntPtr.Zero))
            {
                windowHandle = IntPtr.Zero;
                return false;
            }

            var device = new RawInputDevice
            {
                UsagePage = hid_usage_page_generic,
                Usage = hid_usage_generic_keyboard,
                Flags = 0,
                Target = windowHandle,
            };

            if (!RegisterRawInputDevices(
                    new[] { device },
                    1,
                    (uint)Marshal.SizeOf<RawInputDevice>()))
            {
                RemoveWindowSubclass(
                    windowHandle,
                    subclassProcedure,
                    subclass_id);
                windowHandle = IntPtr.Zero;
                return false;
            }

            isAvailable = true;
            return true;
        }
    }

    public void BeginCapture()
    {
        lock (sync)
        {
            throwIfDisposed();
            pending.Clear();
            pressedKeys.Clear();
            isCapturing = isAvailable;
        }
    }

    public void EndCapture()
    {
        lock (sync)
        {
            isCapturing = false;
            pending.Clear();
            pressedKeys.Clear();
        }
    }

    public bool TryDequeue(out TimestampedKeyInput input)
    {
        lock (sync)
        {
            if (pending.Count > 0)
            {
                input = pending.Dequeue();
                return true;
            }
        }

        input = default;
        return false;
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
                return;

            disposed = true;
            detach();
        }
    }

    private IntPtr windowProcedure(
        IntPtr window,
        uint message,
        UIntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        UIntPtr referenceData)
    {
        if (message == wm_input)
        {
            long timestamp = Stopwatch.GetTimestamp();
            try
            {
                captureRawKeyboardEdge(lParam, timestamp);
            }
            catch
            {
                // Native window procedures must never unwind into Win32.
            }
        }

        return DefSubclassProc(window, message, wParam, lParam);
    }

    private void captureRawKeyboardEdge(IntPtr rawInputHandle, long timestamp)
    {
        uint headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        uint headerBytes = headerSize;
        uint headerResult = GetRawInputData(
            rawInputHandle,
            rid_header,
            out RawInputHeader header,
            ref headerBytes,
            headerSize);
        if (headerResult == uint.MaxValue
            || header.Type != rim_typekeyboard)
            return;

        uint inputBytes = (uint)Marshal.SizeOf<RawInput>();
        uint inputResult = GetRawInputData(
            rawInputHandle,
            rid_input,
            out RawInput input,
            ref inputBytes,
            headerSize);
        if (inputResult == uint.MaxValue)
            return;

        RawKeyboard keyboard = input.Data.Keyboard;
        bool isPressed = (keyboard.Flags & ri_key_break) == 0;
        if (!WindowsVirtualKeyMapper.TryMap(
                keyboard.VirtualKey,
                keyboard.MakeCode,
                keyboard.Flags,
                out Key key))
            return;

        var identity = new RawKeyIdentity(
            keyboard.MakeCode,
            (ushort)(keyboard.Flags & (ri_key_e0 | ri_key_e1)),
            keyboard.VirtualKey);

        lock (sync)
        {
            if (!isCapturing)
                return;

            bool changed = isPressed
                ? pressedKeys.Add(identity)
                : pressedKeys.Remove(identity);
            if (!changed)
                return;

            if (pending.Count >= max_pending_edges)
                pending.Dequeue();

            pending.Enqueue(new TimestampedKeyInput(
                key,
                isPressed,
                timestamp));
        }
    }

    private void detach()
    {
        isCapturing = false;
        isAvailable = false;
        pending.Clear();
        pressedKeys.Clear();

        if (windowHandle == IntPtr.Zero)
            return;

        RemoveWindowSubclass(
            windowHandle,
            subclassProcedure,
            subclass_id);

        var device = new RawInputDevice
        {
            UsagePage = hid_usage_page_generic,
            Usage = hid_usage_generic_keyboard,
            Flags = ridev_remove,
            Target = IntPtr.Zero,
        };
        RegisterRawInputDevices(
            new[] { device },
            1,
            (uint)Marshal.SizeOf<RawInputDevice>());
        windowHandle = IntPtr.Zero;
    }

    private void throwIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(
                nameof(WindowsRawKeyboardTimestampBackend));
    }

    private readonly record struct RawKeyIdentity(
        ushort MakeCode,
        ushort ExtendedFlags,
        ushort VirtualKey);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr SubclassProcedure(
        IntPtr window,
        uint message,
        UIntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        UIntPtr referenceData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr Parameter;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawKeyboard
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VirtualKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct RawInputData
    {
        [FieldOffset(0)]
        public RawKeyboard Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInput
    {
        public RawInputHeader Header;
        public RawInputData Data;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(
        RawInputDevice[] devices,
        uint numberOfDevices,
        uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr rawInput,
        uint command,
        out RawInputHeader data,
        ref uint size,
        uint headerSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr rawInput,
        uint command,
        out RawInput data,
        ref uint size,
        uint headerSize);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        IntPtr window,
        SubclassProcedure procedure,
        UIntPtr subclassId,
        UIntPtr referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        IntPtr window,
        SubclassProcedure procedure,
        UIntPtr subclassId);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(
        IntPtr window,
        uint message,
        UIntPtr wParam,
        IntPtr lParam);
}
