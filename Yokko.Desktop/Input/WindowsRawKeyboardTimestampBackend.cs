using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using osu.Framework.Platform;
using osuTK.Input;
using Yokko.Game.Input;

namespace Yokko.Desktop.Input;

internal sealed class WindowsRawKeyboardTimestampBackend :
    IKeyInputTimestampBackend,
    IKeyInputFastPathBackend
{
    private const uint wm_input = 0x00ff;
    private const uint rid_input = 0x10000003;
    private const uint rim_typekeyboard = 1;
    private const ushort hid_usage_page_generic = 0x01;
    private const ushort hid_usage_generic_keyboard = 0x06;
    private const uint ridev_remove = 0x00000001;
    private const ushort ri_key_break = 0x0001;
    private const ushort ri_key_e0 = 0x0002;
    private const ushort ri_key_e1 = 0x0004;
    private const ulong subclass_id_value = 0x594f4b4b;

    private static readonly UIntPtr subclass_id = new(subclass_id_value);
    private static readonly uint raw_input_header_size =
        (uint)Marshal.SizeOf<RawInputHeader>();
    private static readonly uint raw_input_size =
        (uint)Marshal.SizeOf<RawInput>();
    private static readonly uint raw_input_device_size =
        (uint)Marshal.SizeOf<RawInputDevice>();

    private readonly object sync = new();
    private const int max_pending_edges = 1024;

    private readonly TimestampedKeyInputBuffer pending =
        new(max_pending_edges);
    private readonly RawKeyState pressedKeys = new();
    private readonly SubclassProcedure subclassProcedure;

    private IntPtr windowHandle;
    private bool isCapturing;
    private bool isAvailable;
    private bool disposed;
    private int activeCaptureWriters;
    private IKeyInputFastPathSink fastPathSink;

    public WindowsRawKeyboardTimestampBackend()
    {
        subclassProcedure = windowProcedure;
    }

    public string Name => "Windows Raw Input";

    public bool IsAvailable
        => Volatile.Read(ref isAvailable);

    public KeyInputTimestampBackendStatus Status
    {
        get
        {
            return new KeyInputTimestampBackendStatus(
                Name,
                Volatile.Read(ref isAvailable),
                Volatile.Read(ref isCapturing),
                pending.Count,
                pending.CapturedEdgeCount,
                pending.DroppedEdgeCount);
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
                    raw_input_device_size))
            {
                RemoveWindowSubclass(
                    windowHandle,
                    subclassProcedure,
                    subclass_id);
                windowHandle = IntPtr.Zero;
                return false;
            }

            Volatile.Write(ref isAvailable, true);
            return true;
        }
    }

    public void BeginCapture()
    {
        lock (sync)
        {
            throwIfDisposed();
            stopCaptureAndWait();
            pending.Reset();
            pressedKeys.Clear();
            Volatile.Write(
                ref isCapturing,
                Volatile.Read(ref isAvailable));
        }
    }

    public void EndCapture()
    {
        lock (sync)
        {
            stopCaptureAndWait();
            pending.Clear();
            pressedKeys.Clear();
        }
    }

    public bool TryDequeue(out TimestampedKeyInput input)
    {
        return pending.TryDequeue(out input);
    }

    public void SetFastPathSink(IKeyInputFastPathSink sink) =>
        Volatile.Write(ref fastPathSink, sink);

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
        if (!tryEnterCapture())
            return;

        try
        {
            uint inputBytes = raw_input_size;
            uint inputResult = GetRawInputData(
                rawInputHandle,
                rid_input,
                out RawInput input,
                ref inputBytes,
                raw_input_header_size);
            if (inputResult == uint.MaxValue
                || input.Header.Type != rim_typekeyboard)
                return;

            RawKeyboard keyboard = input.Data.Keyboard;
            bool isPressed = (keyboard.Flags & ri_key_break) == 0;
            if (!WindowsVirtualKeyMapper.TryMap(
                    keyboard.VirtualKey,
                    keyboard.MakeCode,
                    keyboard.Flags,
                    out Key key))
                return;

            int identity = RawKeyState.IdentityIndex(
                keyboard.MakeCode,
                keyboard.Flags,
                keyboard.VirtualKey);
            bool changed = pressedKeys.Set(identity, isPressed);
            if (!changed)
                return;

            var fastPath = new KeyInputFastPathResult(-1, 0, 0);
            try
            {
                IKeyInputFastPathSink sink =
                    Volatile.Read(ref fastPathSink);
                if (sink != null
                    && !sink.TryDispatch(
                        key,
                        isPressed,
                        timestamp,
                        out fastPath))
                {
                    fastPath = new KeyInputFastPathResult(-1, 0, 0);
                }
            }
            catch
            {
                fastPath = new KeyInputFastPathResult(-1, 0, 0);
            }

            pending.Enqueue(new TimestampedKeyInput(
                key,
                isPressed,
                timestamp,
                fastPath.HitObjectIndex,
                fastPath.TriggeredSampleMask,
                fastPath.AudioEnqueueTimestamp));
        }
        finally
        {
            Interlocked.Decrement(ref activeCaptureWriters);
        }
    }

    private void detach()
    {
        stopCaptureAndWait();
        Volatile.Write(ref isAvailable, false);
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
            raw_input_device_size);
        windowHandle = IntPtr.Zero;
    }

    private void throwIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(
                nameof(WindowsRawKeyboardTimestampBackend));
    }

    private bool tryEnterCapture()
    {
        if (!Volatile.Read(ref isCapturing))
            return false;

        Interlocked.Increment(ref activeCaptureWriters);
        if (Volatile.Read(ref isCapturing))
            return true;

        Interlocked.Decrement(ref activeCaptureWriters);
        return false;
    }

    private void stopCaptureAndWait()
    {
        Volatile.Write(ref isCapturing, false);
        var spinner = new SpinWait();
        while (Volatile.Read(ref activeCaptureWriters) != 0)
            spinner.SpinOnce();
    }

    internal sealed class RawKeyState
    {
        private const int physical_key_count = 4096;
        private readonly ulong[] bits = new ulong[physical_key_count / 64];

        internal bool Set(int identity, bool isPressed)
        {
            int wordIndex = identity >> 6;
            ulong mask = 1UL << (identity & 63);
            ulong current = bits[wordIndex];
            bool wasPressed = (current & mask) != 0;
            if (wasPressed == isPressed)
                return false;

            bits[wordIndex] = isPressed
                ? current | mask
                : current & ~mask;
            return true;
        }

        internal void Clear() => Array.Clear(bits);

        internal static int IdentityIndex(
            ushort makeCode,
            ushort flags,
            ushort virtualKey)
        {
            if (makeCode == 0)
                return 2048 + (virtualKey & 0xff);

            int identity = makeCode & 0x1ff;
            if ((flags & ri_key_e0) != 0)
                identity |= 1 << 9;
            if ((flags & ri_key_e1) != 0)
                identity |= 1 << 10;
            return identity;
        }
    }

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
