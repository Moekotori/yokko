using osuTK.Input;

namespace Yokko.Game.Input;

public readonly record struct KeyInputFastPathResult(
    int HitObjectIndex,
    ulong TriggeredSampleMask,
    long AudioEnqueueTimestamp);

public interface IKeyInputFastPathSink
{
    bool TryDispatch(
        Key key,
        bool isPressed,
        long captureTimestamp,
        out KeyInputFastPathResult result);
}

/// <summary>
/// Optional platform capability which observes a physical edge before the
/// framework update thread drains it. The authoritative input queue remains
/// unchanged and always receives the edge.
/// </summary>
public interface IKeyInputFastPathBackend
{
    void SetFastPathSink(IKeyInputFastPathSink? sink);
}
