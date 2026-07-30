using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using osu.Framework.Platform;
using osuTK.Input;

namespace Yokko.Game.Input;

/// <summary>
/// Captures keyboard edge timestamps on the SDL window thread, before
/// osu!framework drains its pending input queue on the update thread.
/// </summary>
internal sealed class KeyInputTimestampSource : IDisposable
{
    private const int max_pending_timestamps_per_edge = 16;

    private readonly object sync = new();
    private readonly Dictionary<(Key Key, bool IsPressed), Queue<long>> pending = new();
    private readonly HashSet<Key> pressedKeys = new();

    private object window;
    private EventInfo keyDownEvent;
    private EventInfo keyUpEvent;
    private Action<Key> keyDownHandler;
    private Action<Key> keyUpHandler;
    private readonly IKeyInputTimestampBackend platformBackend;
    private bool isCapturing;
    private bool disposed;
    private long frameworkCapturedEdgeCount;
    private long frameworkDroppedEdgeCount;

    public KeyInputTimestampSource(IKeyInputTimestampBackend platformBackend = null)
    {
        this.platformBackend = platformBackend;
    }

    public bool IsRawInputAvailable =>
        platformBackend?.IsAvailable == true;

    internal KeyInputTimestampBackendStatus Status
    {
        get
        {
            if (platformBackend?.IsAvailable == true)
                return platformBackend.Status;

            lock (sync)
            {
                int pendingEdgeCount = 0;
                foreach (Queue<long> timestamps in pending.Values)
                    pendingEdgeCount += timestamps.Count;

                return new KeyInputTimestampBackendStatus(
                    "SDL window fallback",
                    window != null,
                    isCapturing,
                    pendingEdgeCount,
                    frameworkCapturedEdgeCount,
                    frameworkDroppedEdgeCount);
            }
        }
    }

    public void Attach(IWindow hostWindow) => AttachWindowEvents(hostWindow);

    internal bool AttachWindowEvents(object hostWindow)
    {
        lock (sync)
        {
            throwIfDisposed();
            detachWindow();
            window = hostWindow;
            bool platformAttached =
                hostWindow is IWindow frameworkWindow
                && platformBackend?.Attach(frameworkWindow) == true;

            if (window == null)
                return platformAttached;

            Type windowType = window.GetType();
            keyDownEvent = windowType.GetEvent(
                "KeyDown",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            keyUpEvent = windowType.GetEvent(
                "KeyUp",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (keyDownEvent?.EventHandlerType != typeof(Action<Key>)
                || keyUpEvent?.EventHandlerType != typeof(Action<Key>))
            {
                keyDownEvent = null;
                keyUpEvent = null;
                window = null;
                return platformAttached;
            }

            keyDownHandler = onKeyDown;
            keyUpHandler = onKeyUp;
            keyDownEvent.AddEventHandler(window, keyDownHandler);
            keyUpEvent.AddEventHandler(window, keyUpHandler);
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
            frameworkCapturedEdgeCount = 0;
            frameworkDroppedEdgeCount = 0;
            isCapturing = true;
            platformBackend?.BeginCapture();
        }
    }

    public void EndCapture()
    {
        lock (sync)
        {
            isCapturing = false;
            pending.Clear();
            pressedKeys.Clear();
            platformBackend?.EndCapture();
        }
    }

    public bool TryTake(Key key, bool isPressed, out long timestamp)
    {
        return TryTake(key, isPressed, out timestamp, out _);
    }

    public bool TryTake(
        Key key,
        bool isPressed,
        out long timestamp,
        out KeyInputTimestampKind kind)
    {
        lock (sync)
        {
            if (pending.TryGetValue((key, isPressed), out Queue<long> timestamps)
                && timestamps.Count > 0)
            {
                timestamp = timestamps.Dequeue();
                if (timestamps.Count == 0)
                    pending.Remove((key, isPressed));

                kind = KeyInputTimestampKind.FrameworkWindow;
                return true;
            }
        }

        timestamp = 0;
        kind = KeyInputTimestampKind.None;
        return false;
    }

    public bool TryDequeueRaw(out TimestampedKeyInput input)
    {
        if (platformBackend?.IsAvailable == true)
            return platformBackend.TryDequeue(out input);

        input = default;
        return false;
    }

    internal void SetRawInputFastPathSink(IKeyInputFastPathSink sink)
    {
        if (platformBackend is IKeyInputFastPathBackend fastPathBackend)
            fastPathBackend.SetFastPathSink(sink);
    }

    internal void Record(Key key, bool isPressed, long timestamp)
    {
        lock (sync)
        {
            if (!isCapturing
                || platformBackend?.IsAvailable == true)
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

            if (timestamps.Count >= max_pending_timestamps_per_edge)
            {
                timestamps.Dequeue();
                frameworkDroppedEdgeCount++;
            }

            timestamps.Enqueue(timestamp);
            frameworkCapturedEdgeCount++;
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
            SetRawInputFastPathSink(null);
            platformBackend?.Dispose();
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

        keyDownEvent?.RemoveEventHandler(window, keyDownHandler);
        keyUpEvent?.RemoveEventHandler(window, keyUpHandler);
        keyDownHandler = null;
        keyUpHandler = null;
        keyDownEvent = null;
        keyUpEvent = null;
        window = null;
    }

    private void throwIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(KeyInputTimestampSource));
    }
}
