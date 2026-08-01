using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using osu.Framework.Logging;
using Yokko.Game.Diagnostics;

namespace Yokko.Desktop.Diagnostics;

/// <summary>
/// Presents live framework logs in a separate native Windows console without
/// adding a WindowsDesktop/WinForms runtime dependency to the game executable.
/// </summary>
internal sealed class WindowsDebugConsoleWindow : IDebugConsoleWindow, IDisposable
{
    private const int historyCapacity = 5000;
    private const int genericRead = unchecked((int)0x80000000);
    private const int genericWrite = 0x40000000;
    private const int fileShareRead = 0x00000001;
    private const int fileShareWrite = 0x00000002;
    private const int openExisting = 3;
    private const int swHide = 0;
    private const int swShow = 5;
    private const uint scClose = 0xF060;
    private const uint mfByCommand = 0x00000000;

    private readonly object sync = new();
    private readonly Queue<string> history = new(historyCapacity);
    private readonly ConcurrentQueue<string> pending = new();
    private readonly AutoResetEvent logAvailable = new(false);
    private readonly Thread writerThread;
    private TextWriter output;
    private nint consoleWindow;
    private bool consoleAllocated;
    private bool disposing;

    public WindowsDebugConsoleWindow()
    {
        Logger.NewEntry += onLoggerEntry;
        writerThread = new Thread(runWriter)
        {
            IsBackground = true,
            Name = "Yokko debug console writer",
        };
        writerThread.Start();
    }

    public void SetVisible(bool visible)
    {
        lock (sync)
        {
            if (disposing)
                return;

            if (visible && !consoleAllocated)
                allocateConsole();

            if (consoleAllocated)
                ShowWindow(consoleWindow, visible ? swShow : swHide);
        }
    }

    private void allocateConsole()
    {
        if (!AllocConsole())
            return;

        SetConsoleTitle("Yokko - Live Debug Console");
        consoleWindow = GetConsoleWindow();

        // Closing a process-owned Win32 console also terminates the game. Keep
        // the window safely hideable through F12/the setting instead.
        nint systemMenu = GetSystemMenu(consoleWindow, false);
        if (systemMenu != 0)
            DeleteMenu(systemMenu, scClose, mfByCommand);

        SafeFileHandle handle = CreateFile(
            "CONOUT$",
            genericRead | genericWrite,
            fileShareRead | fileShareWrite,
            0,
            openExisting,
            0,
            0);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            FreeConsole();
            consoleWindow = 0;
            return;
        }

        output = new StreamWriter(
            new FileStream(handle, FileAccess.Write),
            new UTF8Encoding(false))
        {
            AutoFlush = true,
        };
        consoleAllocated = true;

        foreach (string line in history)
            pending.Enqueue(line);
        logAvailable.Set();
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

        lock (sync)
        {
            if (disposing)
                return;

            history.Enqueue(line);
            while (history.Count > historyCapacity)
                history.Dequeue();

            if (!consoleAllocated)
                return;

            pending.Enqueue(line);
        }

        logAvailable.Set();
    }

    private void runWriter()
    {
        while (true)
        {
            logAvailable.WaitOne();

            TextWriter currentOutput;
            lock (sync)
            {
                if (disposing)
                    return;

                currentOutput = output;
            }

            if (currentOutput == null)
                continue;

            try
            {
                while (pending.TryDequeue(out string line))
                    currentOutput.WriteLine(line);
            }
            catch (IOException)
            {
                // The process may be shutting down while the console detaches.
            }
        }
    }

    private static string levelCode(LogLevel level) => level switch
    {
        LogLevel.Error => "ERR",
        LogLevel.Important => "IMP",
        LogLevel.Debug => "DBG",
        _ => "VRB",
    };

    public void Dispose()
    {
        Logger.NewEntry -= onLoggerEntry;

        lock (sync)
        {
            if (disposing)
                return;

            disposing = true;
        }

        logAvailable.Set();
        writerThread.Join(TimeSpan.FromSeconds(1));

        lock (sync)
        {
            output?.Dispose();
            output = null;
            if (consoleAllocated)
                FreeConsole();
            consoleAllocated = false;
            consoleWindow = 0;
        }

        logAvailable.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll")]
    private static extern nint GetConsoleWindow();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetConsoleTitle(string title);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        int desiredAccess,
        int shareMode,
        nint securityAttributes,
        int creationDisposition,
        int flagsAndAttributes,
        nint templateFile);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    private static extern nint GetSystemMenu(nint window, bool revert);

    [DllImport("user32.dll")]
    private static extern bool DeleteMenu(nint menu, uint position, uint flags);
}
