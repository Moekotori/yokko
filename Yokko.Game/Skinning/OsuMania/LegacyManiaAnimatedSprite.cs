using System;
using System.Collections.Generic;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace Yokko.Game.Skinning.OsuMania;

/// <summary>
/// A sprite-backed legacy animation which keeps the geometry controls needed
/// by repeating osu!mania long-note bodies.
/// </summary>
internal partial class LegacyManiaAnimatedSprite : Sprite
{
    private IReadOnlyList<Texture> frames = Array.Empty<Texture>();
    private double elapsed;
    private double frameDuration = 1000.0 / 60;
    private int frameIndex;

    public LegacyManiaAnimatedSprite(
        IReadOnlyList<Texture> frames,
        double frameDuration = 1000.0 / 60)
    {
        SetFrames(frames, frameDuration);
    }

    public bool IsPlaying { get; set; } = true;

    public int FrameCount => frames.Count;

    public int CurrentFrameIndex => frameIndex;

    public event Action<Texture> FrameChanged;

    public void SetFrames(
        IReadOnlyList<Texture> newFrames,
        double newFrameDuration)
    {
        frames = newFrames ?? Array.Empty<Texture>();
        frameDuration = double.IsFinite(newFrameDuration)
            ? Math.Max(0.001, newFrameDuration)
            : 1000.0 / 60;
        GotoFrame(0);
    }

    public void Restart()
    {
        IsPlaying = true;
        GotoFrame(0);
    }

    public void GotoFrame(int index)
    {
        elapsed = 0;

        if (frames.Count == 0)
        {
            frameIndex = 0;
            Texture = null;
            return;
        }

        frameIndex = Math.Clamp(index, 0, frames.Count - 1);
        Texture = frames[frameIndex];
        FrameChanged?.Invoke(Texture);
    }

    protected override void Update()
    {
        base.Update();

        if (!IsPlaying || frames.Count <= 1)
            return;

        elapsed += Math.Max(0, Time.Elapsed);

        if (elapsed < frameDuration)
            return;

        int advancedFrames = (int)(elapsed / frameDuration);
        elapsed %= frameDuration;
        frameIndex = (frameIndex + advancedFrames) % frames.Count;
        Texture = frames[frameIndex];
        FrameChanged?.Invoke(Texture);
    }
}
