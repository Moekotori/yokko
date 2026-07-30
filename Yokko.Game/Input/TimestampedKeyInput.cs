using osuTK.Input;

namespace Yokko.Game.Input;

public readonly record struct TimestampedKeyInput(
    Key Key,
    bool IsPressed,
    long Timestamp,
    int FastPathHitObjectIndex = -1,
    ulong FastPathTriggeredSampleMask = 0,
    long FastPathAudioEnqueueTimestamp = 0);
