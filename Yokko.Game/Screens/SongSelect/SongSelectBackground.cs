using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>
/// Two-layer artwork dissolve. Rapid navigation replaces only the pending
/// request, never the pixels of a layer which is already visible.
/// </summary>
internal partial class SongSelectBackground : CompositeDrawable
{
    internal const double DissolveDuration = 180;
    private readonly Sprite first;
    private readonly Sprite second;
    private readonly Func<bool> reduceMotion;
    private Sprite current;
    private Func<Texture> pendingTexture;
    private string displayedKey;
    private string requestedKey;
    private bool transitioning;
    private int transitionVersion;

    internal float CoverageAlpha => Math.Max(first.Alpha, second.Alpha);
    internal bool IsTransitioning => transitioning;
    internal string DisplayedKey => displayedKey;
    internal string RequestedKey => requestedKey;

    internal SongSelectBackground(string key, Texture texture, Func<bool> reduceMotion)
    {
        this.reduceMotion = reduceMotion;
        displayedKey = requestedKey = key;
        RelativeSizeAxes = Axes.Both;
        Masking = true;
        InternalChildren =
        [
            first = createLayer(),
            second = createLayer(),
        ];
        current = first;
        first.Texture = texture;
        second.Alpha = 0;
    }

    internal void Request(string key, Func<Texture> loadTexture)
    {
        if (string.Equals(key, requestedKey, StringComparison.OrdinalIgnoreCase))
            return;

        requestedKey = key;
        pendingTexture = loadTexture;
        if (reduceMotion())
        {
            ++transitionVersion;
            first.ClearTransforms();
            second.ClearTransforms();
            first.Scale = second.Scale = Vector2.One;
            current.Texture = pendingTexture();
            current.Alpha = 1;
            (current == first ? second : first).Alpha = 0;
            displayedKey = requestedKey;
            pendingTexture = null;
            transitioning = false;
        }
        else if (!transitioning)
            beginDissolve();
    }

    private void beginDissolve()
    {
        if (pendingTexture == null || string.Equals(displayedKey, requestedKey, StringComparison.OrdinalIgnoreCase))
        {
            pendingTexture = null;
            return;
        }

        string destinationKey = requestedKey;
        Texture texture = pendingTexture();
        pendingTexture = null;
        Sprite outgoing = current;
        Sprite incoming = current == first ? second : first;
        incoming.ClearTransforms();
        incoming.Texture = texture;
        incoming.Alpha = 0;
        incoming.Scale = new Vector2(1.012f);
        // Keep the lower layer opaque: fading both sprites exposes the wash
        // beneath them and produces a bright pulse halfway through a dissolve.
        outgoing.Alpha = 1;
        ChangeInternalChildDepth(outgoing, 0);
        ChangeInternalChildDepth(incoming, -1);
        transitioning = true;
        int version = ++transitionVersion;
        incoming.FadeIn(DissolveDuration, Easing.OutSine);
        incoming.ScaleTo(1, 480, Easing.OutQuint);
        Scheduler.AddDelayed(() =>
        {
            if (version != transitionVersion)
                return;
            // Parent scheduler callbacks run before child transforms for this
            // frame. Finish opacity explicitly before retiring the lower layer.
            incoming.ClearTransforms(targetMember: nameof(Drawable.Alpha));
            incoming.Alpha = 1;
            outgoing.Alpha = 0;
            current = incoming;
            displayedKey = destinationKey;
            transitioning = false;
            // Intermediate key repeats never decode a full-size wallpaper.
            beginDissolve();
        }, DissolveDuration);
    }

    private static Sprite createLayer() => new()
    {
        Anchor = Anchor.Centre,
        Origin = Anchor.Centre,
        RelativeSizeAxes = Axes.Both,
        FillMode = FillMode.Fill,
    };
}
