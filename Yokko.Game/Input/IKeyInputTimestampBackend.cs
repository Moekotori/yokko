using System;
using osu.Framework.Platform;
using osuTK.Input;

namespace Yokko.Game.Input;

public readonly record struct KeyInputTimestampBackendStatus(
    string Name,
    bool IsAvailable,
    bool IsCapturing,
    int PendingEdgeCount,
    long CapturedEdgeCount,
    long DroppedEdgeCount);

/// <summary>
/// Platform-owned source of keyboard edges captured before framework dispatch.
/// </summary>
public interface IKeyInputTimestampBackend : IDisposable
{
    string Name { get; }

    bool IsAvailable { get; }

    KeyInputTimestampBackendStatus Status { get; }

    bool Attach(IWindow window);

    void BeginCapture();

    void EndCapture();

    bool TryDequeue(out TimestampedKeyInput input);
}
