using osuTK.Input;

namespace Yokko.Game.Input;

public readonly record struct TimestampedKeyInput(
    Key Key,
    bool IsPressed,
    long Timestamp);
