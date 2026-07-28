using System;
using System.Collections.Generic;
using System.Diagnostics;
using osu.Framework.Platform;
using osuTK.Input;

namespace Yokko.Game.Input;

/// <summary>
/// Captures keyboard edge timestamps on the SDL window thread, before
/// osu!framework drains its pending input queue on the update thread.
/// </summary>
internal sealed class KeyInputTimestampSource : IDisposable
{
    private readonly object sync = new();
    private readonly Dictionary<(Key Key, bool IsPressed), Queue<long>> pending = new();
    private readonly HashSet<Key> pressedKeys = new();

    private ISDLWindow window;
    private bool isCapturing;
    private bool disposed;

    public void Attach(IWindow hostWindow)
    {
        lock (sync)
        {
            throwIfDisposed();
            detachWindow();
            window = hostWindow as ISDLWindow;

            if (window == null)
                return;

            window.KeyDown += onKeyDown;
            window.KeyUp += onKeyUp;
        }
    }

    public void BeginCapture()
    {
        lock (sync)
        {
            throwIfDisposed();
            pending.Clear();
            pressedKeys.Clear();
            isCapturing = true;
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

    public bool TryTake(Key key, bool isPressed, out long timestamp)
    {
        lock (sync)
        {
            if (pending.TryGetValue((key, isPressed), out Queue<long> timestamps)
                && timestamps.Count > 0)
            {
                timestamp = timestamps.Dequeue();
                if (timestamps.Count == 0)
                    pending.Remove((key, isPressed));

                return true;
            }
        }

        timestamp = 0;
        return false;
    }

    internal void Record(Key key, bool isPressed, long timestamp)
    {
        lock (sync)
        {
            if (!isCapturing)
                return;

            bool changed = isPressed
                ? pressedKeys.Add(key)
                : pressedKeys.Remove(key);
            if (!changed)
                return;

            if (!pending.TryGetValue((key, isPressed), out Queue<long> timestamps))
            {
                timestamps = new Queue<long>();
                pending.Add((key, isPressed), timestamps);
            }

            timestamps.Enqueue(timestamp);
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
                return;

            disposed = true;
            isCapturing = false;
            pending.Clear();
            pressedKeys.Clear();
            detachWindow();
        }
    }

    private void onKeyDown(Key key) =>
        Record(key, true, Stopwatch.GetTimestamp());

    private void onKeyUp(Key key) =>
        Record(key, false, Stopwatch.GetTimestamp());

    private void detachWindow()
    {
        if (window == null)
            return;

        window.KeyDown -= onKeyDown;
        window.KeyUp -= onKeyUp;
        window = null;
    }

    private void throwIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(KeyInputTimestampSource));
    }
}
