using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Scoring;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// Displays recent input timing against the active mania hit windows.
/// Visual structure inspired by ppy/osu's BarHitErrorMeter.cs at
/// commit 1032a7c31581513c8be751e46f0940e1c95ed252 (MIT).
/// </summary>
public partial class GameplayTimingBar : CompositeDrawable
{
    private const float barWidth = 360;
    private const float barHeight = 6;
    private const float markerY = 54;
    private const float componentHeight = 92;
    private const int markerLifetimeMilliseconds = 2400;
    private const int maxConcurrentMarkers = 30;
    private const double trendWeight = 0.15;

    private readonly double maximumHitErrorMilliseconds;
    private readonly DrawablePool<TimingMarker> markerPool =
        new(maxConcurrentMarkers);
    private readonly Container<TimingMarker> markerLayer;
    private readonly Circle pressTrendMarker;
    private readonly Box releaseTrendMarker;
    private readonly SpriteText latestText;
    private bool hasPressTrend;
    private bool hasReleaseTrend;
    private double pressTrendMilliseconds;
    private double releaseTrendMilliseconds;

    internal int RecordedMarkerCount { get; private set; }

    internal int ActiveMarkerCount => markerLayer.Count;

    internal double? LatestHitErrorMilliseconds { get; private set; }

    internal double? PressTrendMilliseconds =>
        hasPressTrend ? pressTrendMilliseconds : null;

    internal double? ReleaseTrendMilliseconds =>
        hasReleaseTrend ? releaseTrendMilliseconds : null;

    internal float LatestMarkerPosition { get; private set; }

    internal float CentreMarkerPosition => barWidth / 2;

    internal float MaximumMarkerPosition => barWidth;

    internal JudgementPhase LatestPhase { get; private set; }

    internal string DisplayedDirectionKey { get; private set; } =
        string.Empty;

    public GameplayTimingBar(JudgementWindows windows)
    {
        ArgumentNullException.ThrowIfNull(windows);

        maximumHitErrorMilliseconds = Math.Max(1, windows.MissMilliseconds);
        Size = new Vector2(barWidth, componentHeight);

        var colourBar = new Container
        {
            Position = new Vector2(0, markerY - barHeight / 2),
            Size = new Vector2(barWidth, barHeight),
            Masking = true,
            CornerRadius = barHeight / 2,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.08f, 0.1f, 0.15f, 0.92f),
                },
                createWindow(windows.MissMilliseconds, JudgementRating.Miss),
                createWindow(windows.MehMilliseconds, JudgementRating.Meh),
                createWindow(windows.OkMilliseconds, JudgementRating.Ok),
                createWindow(windows.GoodMilliseconds, JudgementRating.Good),
                createWindow(windows.GreatMilliseconds, JudgementRating.Great),
                createWindow(
                    windows.PerfectMilliseconds,
                    JudgementRating.Perfect),
            },
        };

        InternalChildren = new Drawable[]
        {
            markerPool,
            colourBar,
            new Box
            {
                Position = new Vector2(barWidth / 2 - 3, markerY - 13),
                Size = new Vector2(6, 26),
                Colour = new Color4(
                    YokkoPalette.Cyan.R,
                    YokkoPalette.Cyan.G,
                    YokkoPalette.Cyan.B,
                    0.18f),
                Blending = BlendingParameters.Additive,
            },
            new Box
            {
                Position = new Vector2(barWidth / 2 - 0.75f, markerY - 12),
                Size = new Vector2(1.5f, 24),
                Colour = Color4.White,
            },
            markerLayer = new Container<TimingMarker>
            {
                Size = new Vector2(barWidth, componentHeight),
            },
            pressTrendMarker = new Circle
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(barWidth / 2, markerY - 13),
                Size = new Vector2(8, 3),
                Colour = YokkoPalette.Cyan,
                Alpha = 0,
            },
            releaseTrendMarker = new Box
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(barWidth / 2, markerY - 13),
                Size = new Vector2(5),
                Rotation = 45,
                Colour = YokkoPalette.Violet,
                Alpha = 0,
            },
            new SpriteText
            {
                Name = "Timing early limit",
                Anchor = Anchor.TopLeft,
                Origin = Anchor.CentreRight,
                Position = new Vector2(-12, markerY),
                Text = YokkoStrings.Get(
                    "gameplay.timing.early_limit",
                    maximumHitErrorMilliseconds),
                Font = new FontUsage("NotoSansCJK").With(size: 11.5f),
                Colour = YokkoPalette.TextMuted,
                Spacing = new Vector2(0.25f, 0),
            },
            new SpriteText
            {
                Name = "Timing late limit",
                Anchor = Anchor.TopRight,
                Origin = Anchor.CentreLeft,
                Position = new Vector2(12, markerY),
                Text = YokkoStrings.Get(
                    "gameplay.timing.late_limit",
                    maximumHitErrorMilliseconds),
                Font = new FontUsage("NotoSansCJK").With(size: 11.5f),
                Colour = YokkoPalette.TextMuted,
                Spacing = new Vector2(0.25f, 0),
            },
            latestText = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = markerY + 15,
                Text = string.Empty,
                Font = new FontUsage("NotoSansCJK").With(size: 11.5f),
                Colour = YokkoPalette.TextMuted,
            },
        };
    }

    public void Show(JudgementInputEvent input)
    {
        if (input.Phase is not JudgementPhase.Tap
            and not JudgementPhase.HoldHead
            and not JudgementPhase.HoldTail
            || !double.IsFinite(input.HitErrorMilliseconds)
            || !double.IsFinite(input.TimingWindowScale)
            || input.TimingWindowScale <= 0)
        {
            return;
        }

        bool release = input.Phase == JudgementPhase.HoldTail;
        double releaseWindowScale = release ? input.TimingWindowScale : 1;
        double hitError = input.HitErrorMilliseconds;
        double mappedError = release
            ? hitError / releaseWindowScale
            : hitError;
        float markerPosition = positionFor(mappedError);

        if (markerLayer.Count >= maxConcurrentMarkers)
        {
            TimingMarker oldest = markerLayer.Children.FirstOrDefault();
            oldest?.ClearTransforms();
            oldest?.Expire();
        }

        markerPool.Get(marker =>
        {
            marker.Position = new Vector2(markerPosition, markerY);
            markerLayer.Add(marker);
            marker.Show(
                release,
                RatingColours.For(input.Rating),
                markerLifetimeMilliseconds);
        });

        if (release)
        {
            updateTrend(
                ref hasReleaseTrend,
                ref releaseTrendMilliseconds,
                hitError);
            moveTrendMarker(
                releaseTrendMarker,
                releaseTrendMilliseconds / releaseWindowScale);
        }
        else
        {
            updateTrend(
                ref hasPressTrend,
                ref pressTrendMilliseconds,
                hitError);
            moveTrendMarker(pressTrendMarker, pressTrendMilliseconds);
        }
        string directionKey = hitError switch
        {
            < -0.05 => "gameplay.timing.early",
            > 0.05 => "gameplay.timing.late",
            _ => "gameplay.timing.on_time",
        };
        DisplayedDirectionKey = directionKey;
        latestText.Text = YokkoStrings.Get(
            "gameplay.timing.latest_compact",
            YokkoStrings.Get(release
                ? "gameplay.timing.release"
                : "gameplay.timing.press"),
            hitError);
        latestText.Colour = RatingColours.For(input.Rating);
        latestText.FinishTransforms();
        latestText.Alpha = 0;
        latestText.Scale = new Vector2(0.96f);
        latestText.FadeIn(70, Easing.OutQuint)
                  .Then()
                  .Delay(1080)
                  .FadeOut(350, Easing.OutQuint);
        latestText.ScaleTo(1.035f, 70, Easing.OutQuint)
                  .Then()
                  .ScaleTo(1, 130, Easing.OutBack);

        RecordedMarkerCount++;
        LatestHitErrorMilliseconds = hitError;
        LatestMarkerPosition = markerPosition;
        LatestPhase = input.Phase;
    }

    public void Clear()
    {
        foreach (TimingMarker marker in markerLayer.Children.ToArray())
        {
            marker.ClearTransforms();
            marker.Expire();
        }

        hasPressTrend = false;
        hasReleaseTrend = false;
        pressTrendMilliseconds = 0;
        releaseTrendMilliseconds = 0;
        pressTrendMarker.Alpha = 0;
        releaseTrendMarker.Alpha = 0;
        latestText.Text = string.Empty;
        latestText.Alpha = 0;
        RecordedMarkerCount = 0;
        LatestHitErrorMilliseconds = null;
        LatestMarkerPosition = 0;
        LatestPhase = default;
    }

    private void moveTrendMarker(Drawable marker, double error)
    {
        marker.FadeTo(0.9f, 100, Easing.OutQuint)
              .MoveToX(
                  positionFor(error),
                  180,
                  Easing.OutQuint);
    }

    private static void updateTrend(
        ref bool hasTrend,
        ref double trend,
        double hitError)
    {
        if (!hasTrend)
        {
            trend = hitError;
            hasTrend = true;
            return;
        }

        trend = trend * (1 - trendWeight) + hitError * trendWeight;
    }

    private Drawable createWindow(
        double hitWindowMilliseconds,
        JudgementRating rating)
    {
        Color4 colour = RatingColours.For(rating);
        return new Box
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = new Vector2(
                barWidth
                * (float)Math.Clamp(
                    hitWindowMilliseconds
                    / maximumHitErrorMilliseconds,
                    0,
                    1),
                barHeight),
            Colour = new Color4(colour.R, colour.G, colour.B, 0.72f),
        };
    }

    private float positionFor(double hitErrorMilliseconds) =>
        barWidth
        * (float)((Math.Clamp(
                       hitErrorMilliseconds,
                       -maximumHitErrorMilliseconds,
                       maximumHitErrorMilliseconds)
                   / maximumHitErrorMilliseconds
                   + 1)
                  / 2);

    private partial class TimingMarker : PoolableDrawable
    {
        private readonly Circle glow;
        private readonly Circle core;
        private readonly Circle detail;

        public TimingMarker()
        {
            Anchor = Anchor.TopLeft;
            Origin = Anchor.Centre;
            InternalChildren = new Drawable[]
            {
                glow = new Circle
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Scale = new Vector2(1.8f),
                    Alpha = 0.28f,
                    Blending = BlendingParameters.Additive,
                },
                core = new Circle
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
                detail = new Circle
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(3),
                    Alpha = 0,
                },
            };
        }

        public void Show(
            bool release,
            Color4 ratingColour,
            int lifetimeMilliseconds)
        {
            Size = release
                ? new Vector2(8)
                : new Vector2(3, 20);
            Colour = Color4.White;
            glow.Colour = ratingColour;
            core.Colour = Color4.White;
            detail.Colour = ratingColour;
            detail.Alpha = release ? 1 : 0;
            ClearTransforms();
            Alpha = 0;
            Scale = new Vector2(0.65f);
            this.FadeTo(1, 70, Easing.OutQuint)
                .Then()
                .FadeTo(
                    0.42f,
                    lifetimeMilliseconds - 970,
                    Easing.OutQuint)
                .Then()
                .FadeOut(900, Easing.InQuint)
                .Expire();
            this.ScaleTo(1.15f, 70, Easing.OutQuint)
                .Then()
                .ScaleTo(1, 130, Easing.OutBack);
        }
    }
}
