using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using osu.Framework.Logging;
using Yokko.Game.Diagnostics;

namespace Yokko.Desktop.Diagnostics;

/// <summary>
/// Presents live framework logs in a separate native Windows window without
/// adding a WindowsDesktop/WinForms runtime dependency to the game executable.
/// </summary>
internal sealed class WindowsDebugConsoleWindow : IDebugConsoleWindow, IDisposable
{
    private const int historyCapacity = 5000;
    private const string windowClassName = "YokkoLiveDebugLogWindow";
    private const int errorClassAlreadyExists = 1410;

    private const uint wsOverlappedWindow = 0x00CF0000;
    private const uint wsChild = 0x40000000;
    private const uint wsVisible = 0x10000000;
    private const uint wsVScroll = 0x00200000;
    private const uint wsHScroll = 0x00100000;
    private const uint esMultiline = 0x0004;
    private const uint esAutoVScroll = 0x0040;
    private const uint esAutoHScroll = 0x0080;
    private const uint esReadOnly = 0x0800;
    private const uint wsExClientEdge = 0x00000200;
    private const int cwUseDefault = unchecked((int)0x80000000);
    private const int swHide = 0;
    private const int swShow = 5;
    private const int ansiFixedFont = 11;

    private const uint wmDestroy = 0x0002;
    private const uint wmSize = 0x0005;
    private const uint wmClose = 0x0010;
    private const uint wmSetFont = 0x0030;
    private const uint emSetSel = 0x00B1;
    private const uint emReplaceSel = 0x00C2;
    private const uint emSetLimitText = 0x00C5;
    private const uint wmAppShow = 0x8001;
    private const uint wmAppAppendLogs = 0x8002;
    private const uint wmAppShutdown = 0x8003;

    private readonly object sync = new();
    private readonly Queue<string> history = new(historyCapacity);
    private readonly ConcurrentQueue<string> pending = new();
    private readonly Thread windowThread;
    private readonly WindowProcedure windowProcedure;
    private nint window;
    private nint logView;
    private bool rebuildRequired;
    private bool requestedVisible;
    private bool disposing;

    public event Action CloseRequested;

    internal nint WindowHandle
    {
        get
        {
            lock (sync)
                return window;
        }
    }

    internal int WindowCreationError { get; private set; }

    internal bool WindowThreadAlive => windowThread?.IsAlive == true;

    public WindowsDebugConsoleWindow()
    {
        windowProcedure = handleWindowMessage;
        if (!OperatingSystem.IsWindows())
            return;

        Logger.NewEntry += onLoggerEntry;
        windowThread = new Thread(runWindow)
        {
            IsBackground = true,
            Name = "Yokko debug log window",
        };
        windowThread.SetApartmentState(ApartmentState.STA);
        windowThread.Start();
    }

    public void SetVisible(bool visible)
    {
        if (!OperatingSystem.IsWindows())
            return;

        nint currentWindow;
        lock (sync)
        {
            if (disposing)
                return;

            requestedVisible = visible;
            currentWindow = window;
        }

        if (currentWindow != 0)
            PostMessage(currentWindow, wmAppShow, visible ? 1 : 0, 0);
    }

    private void runWindow()
    {
        nint instance = GetModuleHandle(null);
        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            WindowProcedure = windowProcedure,
            Instance = instance,
            Cursor = LoadCursor(0, (nint)32512),
            BackgroundBrush = (nint)6,
            ClassName = windowClassName,
        };

        if (RegisterClassEx(ref windowClass) == 0
            && Marshal.GetLastWin32Error() != errorClassAlreadyExists)
        {
            WindowCreationError = Marshal.GetLastWin32Error();
            Logger.Log(
                $"Could not register Yokko debug window class (Win32 error {WindowCreationError}).",
                LoggingTarget.Runtime,
                LogLevel.Error);
            return;
        }

        nint createdWindow = CreateWindowEx(
            0,
            windowClassName,
            "Yokko - Live Debug Console",
            wsOverlappedWindow,
            cwUseDefault,
            cwUseDefault,
            1000,
            640,
            0,
            0,
            instance,
            0);
        if (createdWindow == 0)
        {
            WindowCreationError = Marshal.GetLastWin32Error();
            Logger.Log(
                $"Could not create Yokko debug window (Win32 error {WindowCreationError}).",
                LoggingTarget.Runtime,
                LogLevel.Error);
            return;
        }

        nint createdLogView = CreateWindowEx(
            wsExClientEdge,
            "EDIT",
            string.Empty,
            wsChild | wsVisible | wsVScroll | wsHScroll
            | esMultiline | esAutoVScroll | esAutoHScroll | esReadOnly,
            0,
            0,
            0,
            0,
            createdWindow,
            0,
            instance,
            0);
        if (createdLogView == 0)
        {
            WindowCreationError = Marshal.GetLastWin32Error();
            Logger.Log(
                $"Could not create Yokko debug log view (Win32 error {WindowCreationError}).",
                LoggingTarget.Runtime,
                LogLevel.Error);
            DestroyWindow(createdWindow);
            return;
        }

        SendMessage(createdLogView, wmSetFont, GetStockObject(ansiFixedFont), 1);
        SendMessage(createdLogView, emSetLimitText, 64 * 1024 * 1024, 0);

        bool showInitially;
        bool stopImmediately;
        lock (sync)
        {
            stopImmediately = disposing;
            if (stopImmediately)
            {
                showInitially = false;
            }
            else
            {
                window = createdWindow;
                logView = createdLogView;
                rebuildRequired = true;
                showInitially = requestedVisible;
            }
        }

        if (stopImmediately)
        {
            DestroyWindow(createdWindow);
            return;
        }

        resizeLogView(createdWindow);
        flushPendingLogs();
        ShowWindow(createdWindow, showInitially ? swShow : swHide);
        if (showInitially)
            UpdateWindow(createdWindow);

        while (GetMessage(out WindowMessage message, 0, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }

        lock (sync)
        {
            window = 0;
            logView = 0;
        }
    }

    private nint handleWindowMessage(
        nint handle,
        uint message,
        nint wordParameter,
        nint longParameter)
    {
        switch (message)
        {
            case wmSize:
                resizeLogView(handle);
                return 0;

            case wmClose:
                lock (sync)
                    requestedVisible = false;
                ShowWindow(handle, swHide);
                CloseRequested?.Invoke();
                return 0;

            case wmAppShow:
                bool visible = wordParameter != 0;
                ShowWindow(handle, visible ? swShow : swHide);
                if (visible)
                    UpdateWindow(handle);
                return 0;

            case wmAppAppendLogs:
                flushPendingLogs();
                return 0;

            case wmAppShutdown:
                DestroyWindow(handle);
                return 0;

            case wmDestroy:
                PostQuitMessage(0);
                return 0;
        }

        return DefWindowProc(handle, message, wordParameter, longParameter);
    }

    private void resizeLogView(nint handle)
    {
        nint currentLogView;
        lock (sync)
            currentLogView = logView;
        if (currentLogView == 0 || !GetClientRect(handle, out Rectangle area))
            return;

        MoveWindow(
            currentLogView,
            0,
            0,
            Math.Max(1, area.Right - area.Left),
            Math.Max(1, area.Bottom - area.Top),
            true);
    }

    private void onLoggerEntry(LogEntry entry)
    {
        if (entry == null)
            return;

        string source = entry.Target?.ToString()
                        ?? entry.LoggerName
                        ?? "Runtime";
        string line = $"{DateTimeOffset.Now:HH:mm:ss.fff}  {levelCode(entry.Level),-3}  {source,-11}  {entry.Message}";
        if (entry.Exception != null)
            line += Environment.NewLine + entry.Exception;

        nint currentWindow;
        lock (sync)
        {
            if (disposing)
                return;

            history.Enqueue(line);
            if (history.Count > historyCapacity)
            {
                history.Dequeue();
                rebuildRequired = true;
            }
            else
            {
                pending.Enqueue(line);
            }

            currentWindow = window;
        }

        if (currentWindow != 0)
            PostMessage(currentWindow, wmAppAppendLogs, 0, 0);
    }

    private void flushPendingLogs()
    {
        nint currentLogView;
        string replacement = null;
        List<string> additions = null;

        lock (sync)
        {
            currentLogView = logView;
            if (currentLogView == 0)
                return;

            if (rebuildRequired)
            {
                replacement = string.Join("\r\n", history.Select(normalizeLine));
                if (replacement.Length > 0)
                    replacement += "\r\n";
                rebuildRequired = false;
                while (pending.TryDequeue(out _))
                {
                }
            }
            else
            {
                while (pending.TryDequeue(out string line))
                    (additions ??= []).Add(line);
            }
        }

        if (replacement != null)
        {
            SetWindowText(currentLogView, replacement);
            SendMessage(currentLogView, emSetSel, -1, -1);
            return;
        }

        if (additions == null)
            return;

        var text = new StringBuilder();
        foreach (string addition in additions)
            text.Append(normalizeLine(addition)).Append("\r\n");
        SendMessage(currentLogView, emSetSel, -1, -1);
        SendMessage(currentLogView, emReplaceSel, 0, text.ToString());
    }

    private static string normalizeLine(string line) =>
        line.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", "\r\n", StringComparison.Ordinal);

    private static string levelCode(LogLevel level) => level switch
    {
        LogLevel.Error => "ERR",
        LogLevel.Important => "IMP",
        LogLevel.Debug => "DBG",
        _ => "VRB",
    };

    public void Dispose()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Logger.NewEntry -= onLoggerEntry;

        nint currentWindow;
        lock (sync)
        {
            if (disposing)
                return;

            disposing = true;
            currentWindow = window;
        }

        if (currentWindow != 0)
            PostMessage(currentWindow, wmAppShutdown, 0, 0);
        windowThread.Join(TimeSpan.FromSeconds(1));
    }

    private delegate nint WindowProcedure(
        nint window,
        uint message,
        nint wordParameter,
        nint longParameter);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public WindowProcedure WindowProcedure;
        public int ClassExtraBytes;
        public int WindowExtraBytes;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint BackgroundBrush;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string MenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string ClassName;
        public nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowMessage
    {
        public nint Window;
        public uint Message;
        public nuint WordParameter;
        public nint LongParameter;
        public uint Time;
        public Point Point;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string moduleName);

    [DllImport("gdi32.dll")]
    private static extern nint GetStockObject(int objectIndex);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WindowClass windowClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(
        nint window,
        uint message,
        nint wordParameter,
        nint longParameter);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    private static extern bool UpdateWindow(nint window);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(nint window, out Rectangle rectangle);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(
        nint window,
        int x,
        int y,
        int width,
        int height,
        bool repaint);

    [DllImport("user32.dll")]
    private static extern nint LoadCursor(nint instance, nint cursorName);

    [DllImport("user32.dll")]
    private static extern int GetMessage(
        out WindowMessage message,
        nint window,
        uint minimumMessage,
        uint maximumMessage);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref WindowMessage message);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(ref WindowMessage message);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(
        nint window,
        uint message,
        nint wordParameter,
        nint longParameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessage(
        nint window,
        uint message,
        nint wordParameter,
        nint longParameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessage(
        nint window,
        uint message,
        nint wordParameter,
        string longParameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowText(nint window, string text);
}
