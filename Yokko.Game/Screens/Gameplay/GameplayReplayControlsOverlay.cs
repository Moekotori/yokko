using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// Compact replay-only playback rail. Its pause and user-rate controls follow
/// ppy/osu's ReplayPlayer/PlaybackSettings at commit
/// 83b8a64bec19e1463353645c2d6d10c75e275b43 (MIT).
/// </summary>
internal partial class GameplayReplayControlsOverlay : CompositeDrawable
{
    private readonly Action togglePause;
    private readonly Action decreaseRate;
    private readonly Action increaseRate;
    private readonly SpriteIcon pauseIcon;
    private readonly SpriteText timeText;
    private readonly SpriteText rateText;

    internal string TimeText => timeText.Text.ToString();
    internal string RateText => rateText.Text.ToString();
    internal bool ShowsPausedState { get; private set; }
    internal void ActivateDecreaseRate() => decreaseRate();
    internal void ActivateIncreaseRate() => increaseRate();
    internal void ActivateTogglePause() => togglePause();

    public GameplayReplayControlsOverlay(
        Action togglePause,
        Action decreaseRate,
        Action increaseRate)
    {
        this.togglePause = togglePause
                           ?? throw new ArgumentNullException(
                               nameof(togglePause));
        this.decreaseRate = decreaseRate
                            ?? throw new ArgumentNullException(
                                nameof(decreaseRate));
        this.increaseRate = increaseRate
                            ?? throw new ArgumentNullException(
                                nameof(increaseRate));

        Anchor = Anchor.TopCentre;
        Origin = Anchor.TopCentre;
        Position = new Vector2(0, 24);
        Size = new Vector2(540, 58);
        Depth = -260;

        InternalChild = new CircularContainer
        {
            RelativeSizeAxes = Axes.Both,
            CornerRadius = 14,
            Masking = true,
            BorderThickness = 1.5f,
            BorderColour = YokkoPalette.Cyan,
            Children =
            [
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.025f, 0.05f, 0.18f, 0.92f),
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 18,
                    Text = "REPLAY",
                    Font = FontUsage.Default.With(size: 15, weight: "Bold"),
                    Colour = YokkoPalette.Rose,
                },
                timeText = new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 92,
                    Text = "00:00 / 00:00",
                    Font = FontUsage.Default.With(size: 14, weight: "SemiBold"),
                    Colour = Color4.White,
                },
                createButton(
                    Anchor.CentreRight,
                    new Vector2(-188, 0),
                    FontAwesome.Solid.Minus,
                    this.decreaseRate),
                new Container
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-139, 0),
                    Size = new Vector2(58, 38),
                    Children =
                    [
                        rateText = new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = "1.00x",
                            Font = FontUsage.Default.With(
                                size: 14,
                                weight: "Bold"),
                            Colour = YokkoPalette.Cyan,
                        },
                    ],
                },
                createButton(
                    Anchor.CentreRight,
                    new Vector2(-90, 0),
                    FontAwesome.Solid.Plus,
                    this.increaseRate),
                createButton(
                    Anchor.CentreRight,
                    new Vector2(-38, 0),
                    FontAwesome.Solid.Pause,
                    this.togglePause,
                    out pauseIcon),
            ],
        };
    }

    internal void UpdateState(
        double currentMilliseconds,
        double durationMilliseconds,
        double playbackRate,
        bool paused)
    {
        currentMilliseconds = Math.Clamp(
            currentMilliseconds,
            0,
            Math.Max(0, durationMilliseconds));
        durationMilliseconds = Math.Max(0, durationMilliseconds);
        timeText.Text = $"{formatTime(currentMilliseconds)} / "
                        + formatTime(durationMilliseconds);
        rateText.Text = $"{playbackRate:0.00}x";
        ShowsPausedState = paused;
        pauseIcon.Icon = paused
            ? FontAwesome.Solid.Play
            : FontAwesome.Solid.Pause;
    }

    private static GameplayReplayControlButton createButton(
        Anchor anchor,
        Vector2 position,
        IconUsage icon,
        Action action) => createButton(
        anchor,
        position,
        icon,
        action,
        out _);

    private static GameplayReplayControlButton createButton(
        Anchor anchor,
        Vector2 position,
        IconUsage icon,
        Action action,
        out SpriteIcon spriteIcon)
    {
        var button = new GameplayReplayControlButton(
            icon,
            action)
        {
            Anchor = anchor,
            Origin = Anchor.Centre,
            Position = position,
        };
        spriteIcon = button.Icon;
        return button;
    }

    private static string formatTime(double milliseconds)
    {
        var duration = TimeSpan.FromMilliseconds(milliseconds);
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }
}

internal partial class GameplayReplayControlButton : ClickableContainer
{
    internal SpriteIcon Icon { get; }

    public GameplayReplayControlButton(IconUsage icon, Action action)
    {
        Action = action;
        Size = new Vector2(40, 40);
        CornerRadius = 10;
        Masking = true;
        Children =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(1, 1, 1, 0.09f),
            },
            Icon = new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(16),
                Icon = icon,
                Colour = Color4.White,
            },
        ];
    }
}
