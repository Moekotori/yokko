using System;
using System.IO;
using System.Threading;
using Yokko.Game.Presentation;

namespace Yokko.Game.Tests.Development;

/// <summary>
/// Test-browser-only file watcher for theme development.
/// </summary>
internal sealed class YokkoThemeFileHotReload : IDisposable
{
    private const int debounce_milliseconds = 200;

    private readonly YokkoUiThemeStore themeStore;
    private readonly Action<Action> schedule;
    private readonly string fullPath;
    private readonly FileSystemWatcher watcher;
    private readonly Timer debounce;
    private readonly object syncRoot = new();
    private volatile bool disposed;
    private int generation;

    public string WatchedPath => fullPath;

    public YokkoThemeFileHotReload(
        string path,
        YokkoUiThemeStore themeStore,
        Action<Action> schedule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(themeStore);
        ArgumentNullException.ThrowIfNull(schedule);

        this.themeStore = themeStore;
        this.schedule = schedule;
        fullPath = Path.GetFullPath(
            Environment.ExpandEnvironmentVariables(path.Trim()));
        string directory = Path.GetDirectoryName(fullPath)
                           ?? throw new InvalidOperationException(
                               "Theme file path has no parent directory.");
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"Theme directory does not exist: {directory}");
        }

        debounce = new Timer(
            _ => loadCurrentGeneration(),
            null,
            Timeout.Infinite,
            Timeout.Infinite);
        watcher = new FileSystemWatcher(
            directory,
            Path.GetFileName(fullPath))
        {
            NotifyFilter = NotifyFilters.FileName
                           | NotifyFilters.LastWrite
                           | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        watcher.Created += onChanged;
        watcher.Changed += onChanged;
        watcher.Deleted += onChanged;
        watcher.Renamed += onRenamed;
        watcher.Error += onError;

        ReloadNow();
    }

    public void ReloadNow()
    {
        lock (syncRoot)
        {
            if (disposed)
                return;

            Interlocked.Increment(ref generation);
            debounce.Change(0, Timeout.Infinite);
        }
    }

    public void Dispose()
    {
        lock (syncRoot)
        {
            if (disposed)
                return;

            disposed = true;
            Interlocked.Increment(ref generation);
            watcher.EnableRaisingEvents = false;
            watcher.Created -= onChanged;
            watcher.Changed -= onChanged;
            watcher.Deleted -= onChanged;
            watcher.Renamed -= onRenamed;
            watcher.Error -= onError;
            watcher.Dispose();
            debounce.Dispose();
        }
    }

    private void onChanged(object sender, FileSystemEventArgs e) =>
        queueReload();

    private void onRenamed(object sender, RenamedEventArgs e) =>
        queueReload();

    private void onError(object sender, ErrorEventArgs e) =>
        queueReload();

    private void queueReload()
    {
        lock (syncRoot)
        {
            if (disposed)
                return;

            Interlocked.Increment(ref generation);
            debounce.Change(debounce_milliseconds, Timeout.Infinite);
        }
    }

    private void loadCurrentGeneration()
    {
        int requestedGeneration = Volatile.Read(ref generation);
        if (disposed)
            return;

        try
        {
            YokkoUiThemeFileResult result =
                YokkoUiThemeFile.Load(fullPath);
            scheduleIfCurrent(requestedGeneration, () =>
            {
                themeStore.Apply(result.Theme, result.Name, fullPath);
            });
        }
        catch (Exception exception)
        {
            scheduleIfCurrent(requestedGeneration, () =>
            {
                themeStore.ReportLoadError(fullPath, exception.Message);
            });
        }
    }

    private void scheduleIfCurrent(
        int requestedGeneration,
        Action action)
    {
        if (disposed
            || requestedGeneration != Volatile.Read(ref generation))
            return;

        try
        {
            schedule(() =>
            {
                if (disposed
                    || requestedGeneration != Volatile.Read(ref generation))
                    return;

                action();
            });
        }
        catch (Exception) when (disposed)
        {
        }
    }
}
