using System;
using osu.Framework.Platform;
using osuTK.Input;

namespace Yokko.Game.Input;

/// <summary>
/// Platform-owned source of keyboard edges captured before framework dispatch.
/// </summary>
public interface IKeyInputTimestampBackend : IDisposable
{
    string Name { get; }

    bool IsAvailable { get; }

    bool Attach(IWindow window);

    void BeginCapture();

    void EndCapture();

    bool TryDequeue(out TimestampedKeyInput input);
}
