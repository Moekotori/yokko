using System;

namespace Yokko.Game.Screens.Editor;

public sealed class EditorPreviewClock
{
    public double CurrentTimeMilliseconds { get; private set; }

    public bool IsPlaying { get; private set; }

    public void Play(double durationMilliseconds)
    {
        if (durationMilliseconds <= 0)
            return;

        if (CurrentTimeMilliseconds >= durationMilliseconds)
            CurrentTimeMilliseconds = 0;

        IsPlaying = true;
    }

    public void Pause()
    {
        IsPlaying = false;
    }

    public void Toggle(double durationMilliseconds)
    {
        if (IsPlaying)
            Pause();
        else
            Play(durationMilliseconds);
    }

    public void Stop()
    {
        IsPlaying = false;
        CurrentTimeMilliseconds = 0;
    }

    public void Seek(double timeMilliseconds, double durationMilliseconds)
    {
        CurrentTimeMilliseconds = Math.Clamp(timeMilliseconds, 0, Math.Max(0, durationMilliseconds));
    }

    public bool Update(double elapsedMilliseconds, double durationMilliseconds)
    {
        if (!IsPlaying || elapsedMilliseconds <= 0)
            return false;

        double previousTime = CurrentTimeMilliseconds;
        CurrentTimeMilliseconds = Math.Min(Math.Max(0, durationMilliseconds), CurrentTimeMilliseconds + elapsedMilliseconds);

        if (CurrentTimeMilliseconds >= durationMilliseconds)
            IsPlaying = false;

        return Math.Abs(CurrentTimeMilliseconds - previousTime) > 0.001;
    }
}
