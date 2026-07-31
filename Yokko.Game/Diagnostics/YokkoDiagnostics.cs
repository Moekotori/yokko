using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace Yokko.Game.Diagnostics;

internal readonly record struct YokkoDiagnosticEntry(
    long Sequence,
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Source,
    string Message,
    string Exception)
{
    public string ToDisplayText()
    {
        string message = Message
                         .Replace("\r\n", " ↵ ", StringComparison.Ordinal)
                         .Replace('\n', ' ')
                         .Replace('\r', ' ');
        return $"{Timestamp.ToLocalTime():HH:mm:ss.fff}  {levelCode(Level),-3}  {Source,-11}  {message}";
    }

    public string ToExportText()
    {
        string line = $"{Timestamp:O} [{Level}] [{Source}] {Message}";
        return string.IsNullOrWhiteSpace(Exception)
            ? line
            : $"{line}{Environment.NewLine}{Exception}";
    }

    private static string levelCode(LogLevel level) => level switch
    {
        LogLevel.Error => "ERR",
        LogLevel.Important => "IMP",
        LogLevel.Debug => "DBG",
        _ => "VRB",
    };
}

/// <summary>
/// Thread-safe bounded history shared by framework logger callbacks and the
/// update-thread console. The audio and import workers never touch drawables.
/// </summary>
internal sealed class YokkoDiagnosticBuffer
{
    private readonly object sync = new();
    private readonly Queue<YokkoDiagnosticEntry> entries;
    private readonly int capacity;
    private long sequence;

    public YokkoDiagnosticBuffer(int capacity = 5000)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        this.capacity = capacity;
        entries = new Queue<YokkoDiagnosticEntry>(capacity);
    }

    public long CurrentSequence
    {
        get
        {
            lock (sync)
                return sequence;
        }
    }

    public int Count
    {
        get
        {
            lock (sync)
                return entries.Count;
        }
    }

    public YokkoDiagnosticEntry Add(
        LogLevel level,
        string source,
        string message,
        Exception exception = null)
    {
        lock (sync)
        {
            var entry = new YokkoDiagnosticEntry(
                ++sequence,
                DateTimeOffset.UtcNow,
                level,
                string.IsNullOrWhiteSpace(source) ? "Runtime" : source,
                message ?? string.Empty,
                exception?.ToString() ?? string.Empty);
            entries.Enqueue(entry);
            while (entries.Count > capacity)
                entries.Dequeue();
            return entry;
        }
    }

    public IReadOnlyList<YokkoDiagnosticEntry> GetAfter(long afterSequence)
    {
        lock (sync)
            return entries.Where(entry => entry.Sequence > afterSequence).ToArray();
    }

    public IReadOnlyList<YokkoDiagnosticEntry> Snapshot()
    {
        lock (sync)
            return entries.ToArray();
    }

    public void Clear()
    {
        lock (sync)
            entries.Clear();
    }
}

/// <summary>
/// Captures every osu!framework log entry for a live in-game console and adds
/// Yokko-specific structured trace points while diagnostics are enabled.
/// </summary>
internal sealed class YokkoDiagnostics : IDisposable
{
    private readonly YokkoDiagnosticBuffer buffer = new();
    private LogLevel loggerLevelBeforeEnable;
    private bool loggerLevelOverridden;
    private bool disposed;
    private string logDirectory = string.Empty;
    private string exportDirectory = string.Empty;
    private string sessionLogPrefix = string.Empty;
    private readonly object performanceSync = new();
    private YokkoPerformanceSnapshot latestPerformance;
    private DateTimeOffset lastPerformanceLogTime;
    private YokkoPerformanceHealth lastPerformanceHealth;
    private bool hasPerformance;
    private bool hasLoggedPerformance;

    public readonly BindableBool ConsoleVisible = new(false);

    public bool IsEnabled => ConsoleVisible.Value;
    public string LogDirectory => logDirectory;
    public int EntryCount => buffer.Count;
    public long CurrentSequence => buffer.CurrentSequence;
    public string ExportDirectory => exportDirectory;

    public YokkoDiagnostics()
    {
        Logger.NewEntry += onLoggerEntry;
        ConsoleVisible.BindValueChanged(onConsoleVisibleChanged);
    }

    public void Initialise(GameHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        logDirectory = host.Storage.GetFullPath("logs", true);
        exportDirectory = host.Storage.GetFullPath(
            "diagnostic-exports",
            true);
        sessionLogPrefix = getSessionLogPrefix();

        string version = Assembly.GetEntryAssembly()?
                                 .GetName().Version?.ToString()
                         ?? "unknown";
        Logger.Log(
            "[SESSION] started"
            + $" | version={version}"
            + $" | runtime={RuntimeInformation.FrameworkDescription}"
            + $" | os={RuntimeInformation.OSDescription}"
            + $" | process={RuntimeInformation.ProcessArchitecture}"
            + $" | cpu={Environment.ProcessorCount}"
            + $" | logs={logDirectory}",
            "diagnostics",
            LogLevel.Important);
    }

    public IReadOnlyList<YokkoDiagnosticEntry> GetEntriesAfter(
        long afterSequence) => buffer.GetAfter(afterSequence);

    public IReadOnlyList<YokkoDiagnosticEntry> Snapshot() => buffer.Snapshot();

    public string ExportText()
    {
        IReadOnlyList<YokkoDiagnosticEntry> snapshot = Snapshot();
        return string.Join(
            Environment.NewLine,
            snapshot.Select(entry => entry.ToExportText()));
    }

    public string ExportBundle() => YokkoDiagnosticExporter.Export(
        exportDirectory,
        logDirectory,
        sessionLogPrefix,
        ExportText(),
        TryGetLatestPerformance(out YokkoPerformanceSnapshot performance)
            ? performance
            : null);

    internal void InitialiseForTesting(osu.Framework.Platform.Storage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        logDirectory = storage.GetFullPath("logs", true);
        exportDirectory = storage.GetFullPath("diagnostic-exports", true);
        sessionLogPrefix = getSessionLogPrefix();
    }

    public void ReportPerformance(YokkoPerformanceSnapshot snapshot)
    {
        bool shouldLog;
        bool recovered;

        lock (performanceSync)
        {
            latestPerformance = snapshot;
            hasPerformance = true;
            recovered = hasLoggedPerformance
                        && lastPerformanceHealth != YokkoPerformanceHealth.Stable
                        && snapshot.Health == YokkoPerformanceHealth.Stable;
            bool healthChanged = hasLoggedPerformance
                                 && lastPerformanceHealth != snapshot.Health;
            shouldLog = IsEnabled
                        && (!hasLoggedPerformance
                            || healthChanged
                            || snapshot.Timestamp - lastPerformanceLogTime
                            >= TimeSpan.FromSeconds(1));

            if (shouldLog)
            {
                lastPerformanceLogTime = snapshot.Timestamp;
                lastPerformanceHealth = snapshot.Health;
                hasLoggedPerformance = true;
            }
        }

        if (!shouldLog)
            return;

        string eventName = recovered
            ? "frame-pacing-recovered"
            : snapshot.Health == YokkoPerformanceHealth.Stable
                ? "sample"
                : "frame-pacing-alert";
        LogLevel level = snapshot.Health == YokkoPerformanceHealth.Stable
                         && !recovered
            ? LogLevel.Verbose
            : LogLevel.Important;
        Logger.Log(
            $"[PERFORMANCE] {eventName} | {snapshot.ToLogDetails()}",
            "diagnostics",
            level);
    }

    public bool TryGetLatestPerformance(
        out YokkoPerformanceSnapshot snapshot)
    {
        lock (performanceSync)
        {
            snapshot = latestPerformance;
            return hasPerformance;
        }
    }

    public void Clear() => buffer.Clear();

    public void Toggle() => ConsoleVisible.Value = !ConsoleVisible.Value;

    public void Trace(
        string area,
        string eventName,
        string details = null,
        LogLevel level = LogLevel.Verbose)
    {
        if (!IsEnabled)
            return;

        string suffix = string.IsNullOrWhiteSpace(details)
            ? string.Empty
            : $" | {details}";
        Logger.Log(
            $"[{area}] {eventName}{suffix}",
            "diagnostics",
            level);
    }

    private void onLoggerEntry(LogEntry entry)
    {
        if (disposed || entry == null)
            return;

        string source = entry.Target?.ToString()
                        ?? entry.LoggerName
                        ?? "Runtime";
        buffer.Add(
            entry.Level,
            source,
            entry.Message,
            entry.Exception);
    }

    private void onConsoleVisibleChanged(ValueChangedEvent<bool> change)
    {
        if (change.NewValue)
        {
            if (!loggerLevelOverridden)
            {
                loggerLevelBeforeEnable = Logger.Level;
                loggerLevelOverridden = true;
            }

            Logger.Level = LogLevel.Debug;
            Logger.Log(
                "[DIAGNOSTICS] live console enabled; verbose framework input logging active",
                "diagnostics",
                LogLevel.Important);
        }
        else
        {
            Logger.Log(
                "[DIAGNOSTICS] live console disabled",
                "diagnostics",
                LogLevel.Important);
            restoreLoggerLevel();
        }
    }

    private void restoreLoggerLevel()
    {
        if (!loggerLevelOverridden)
            return;

        Logger.Level = loggerLevelBeforeEnable;
        loggerLevelOverridden = false;
    }

    private static string getSessionLogPrefix()
    {
        string filename = Logger.GetLogger("diagnostics").Filename;
        int separator = filename.IndexOf('.');
        return separator > 0
            ? filename[..separator]
            : Path.GetFileNameWithoutExtension(filename);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        Logger.NewEntry -= onLoggerEntry;
        ConsoleVisible.ValueChanged -= onConsoleVisibleChanged;
        restoreLoggerLevel();
    }
}
