using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

internal partial class GameplayScrollSpeedOverlay : CompositeDrawable
{
    private const double displayDurationMilliseconds = 900;
    private const double fadeDurationMilliseconds = 180;

    private readonly Box accent;
    private readonly SpriteText label;
    private readonly SpriteText speedText;
    private readonly SpriteText detailText;
    private double hideAtMilliseconds;

    internal double DisplayedSpeed { get; private set; }

    internal int DisplayedTimeRangeMilliseconds { get; private set; }

    internal bool IsLocked { get; private set; }

    internal GameplayScrollSpeedOverlay()
    {
        Size = new Vector2(264, 78);
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1;
        BorderColour = YokkoPalette.Border;

        InternalChildren =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = YokkoPalette.Surface,
            },
            accent = new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 4,
                Colour = YokkoPalette.Cyan,
            },
            label = new SpriteText
            {
                Position = new Vector2(17, 11),
                Text = "SCROLL SPEED",
                Font = FontUsage.Default.With(
                    size: 12,
                    weight: "SemiBold"),
                Colour = YokkoPalette.Cyan,
            },
            speedText = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-17, 8),
                Font = FontUsage.Default.With(
                    size: 25,
                    weight: "Bold"),
                Colour = YokkoPalette.Text,
            },
            detailText = new SpriteText
            {
                Position = new Vector2(17, 46),
                Font = FontUsage.Default.With(size: 12),
                Colour = YokkoPalette.TextMuted,
            },
        ];

        Alpha = 0;
    }

    internal void Show(
        double speed,
        int timeRangeMilliseconds,
        bool locked = false)
    {
        DisplayedSpeed = speed;
        DisplayedTimeRangeMilliseconds = timeRangeMilliseconds;
        IsLocked = locked;

        Color4 colour = locked
            ? YokkoPalette.Rose
            : YokkoPalette.Cyan;
        accent.Colour = colour;
        label.Colour = colour;
        label.Text = locked
            ? "SCROLL SPEED LOCKED"
            : "SCROLL SPEED";
        speedText.Text = speed.ToString("0.0");
        detailText.Text = locked
            ? $"INTRO / BREAK ONLY  ·  {timeRangeMilliseconds} ms"
            : $"{timeRangeMilliseconds} ms visible";

        hideAtMilliseconds =
            Time.Current + displayDurationMilliseconds;
        Alpha = 1;
    }

    protected override void Update()
    {
        base.Update();

        double remaining = hideAtMilliseconds - Time.Current;
        if (remaining <= 0)
        {
            Alpha = 0;
            return;
        }

        Alpha = Math.Clamp(
            (float)(remaining / fadeDurationMilliseconds),
            0,
            1);
    }
}
