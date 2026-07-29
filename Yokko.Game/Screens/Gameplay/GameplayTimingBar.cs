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
    private const float barWidth = 340;
    private const float barHeight = 8;
    private const float barLeft = 54;
    private const float markerY = 28;
    private const int markerLifetimeMilliseconds = 4200;
    private const int maxConcurrentMarkers = 50;
    private const double trendWeight = 0.15;

    private readonly double maximumHitErrorMilliseconds;
    private readonly DrawablePool<TimingMarker> markerPool =
        new(maxConcurrentMarkers);
    private readonly Container<TimingMarker> markerLayer;
    private readonly Circle pressTrendMarker;
    private readonly Circle releaseTrendMarker;
    private readonly SpriteText trendText;
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
        Size = new Vector2(barWidth + barLeft * 2, 66);

        var colourBar = new Container
        {
            Position = new Vector2(barLeft, markerY - barHeight / 2),
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
            trendText = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Text = string.Empty,
                Font = FontUsage.Default.With(size: 11, weight: "SemiBold"),
                Colour = YokkoPalette.TextMuted,
                Alpha = 0,
            },
            new SpriteText
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.CentreLeft,
                Position = new Vector2(0, markerY),
                Text = YokkoStrings.Get("gameplay.timing.early"),
                Font = FontUsage.Default.With(size: 13, weight: "SemiBold"),
                Colour = YokkoPalette.TextMuted,
            },
            new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.CentreRight,
                Position = new Vector2(0, markerY),
                Text = YokkoStrings.Get("gameplay.timing.late"),
                Font = FontUsage.Default.With(size: 13, weight: "SemiBold"),
                Colour = YokkoPalette.TextMuted,
            },
            colourBar,
            markerLayer = new Container<TimingMarker>
            {
                Position = new Vector2(barLeft, 0),
                Size = new Vector2(barWidth, 48),
            },
            new Box
            {
                Position = new Vector2(
                    barLeft + barWidth / 2 - 1,
                    markerY - 8),
                Size = new Vector2(2, 16),
                Colour = Color4.White,
            },
            pressTrendMarker = new Circle
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(barLeft + barWidth / 2, markerY + 10),
                Size = new Vector2(10, 4),
                Colour = Color4.White,
                Alpha = 0,
            },
            releaseTrendMarker = new Circle
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(barLeft + barWidth / 2, markerY + 10),
                Size = new Vector2(4, 10),
                Colour = YokkoPalette.TextMuted,
                Alpha = 0,
            },
            latestText = new SpriteText
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Text = string.Empty,
                Font = FontUsage.Default.With(size: 12, weight: "SemiBold"),
                Colour = YokkoPalette.TextMuted,
            },
        };
    }

    public void Show(JudgementEvent judgement)
    {
        if (judgement.HitTimeMilliseconds is null
            || judgement.Phase is not JudgementPhase.Tap
            and not JudgementPhase.HoldHead
            and not JudgementPhase.HoldTail
            || !judgement.Rating.IsScorable())
        {
            return;
        }

        bool release = judgement.Phase == JudgementPhase.HoldTail;
        double hitError = judgement.HitErrorMilliseconds;
        double mappedError = release
            ? hitError / BeatmapJudgementState.HoldReleaseWindowLenience
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
            marker.Colour = RatingColours.For(judgement.Rating);
            markerLayer.Add(marker);
            marker.Show(release, markerLifetimeMilliseconds);
        });

        if (release)
        {
            updateTrend(
                ref hasReleaseTrend,
                ref releaseTrendMilliseconds,
                hitError);
            moveTrendMarker(
                releaseTrendMarker,
                releaseTrendMilliseconds
                / BeatmapJudgementState.HoldReleaseWindowLenience);
        }
        else
        {
            updateTrend(
                ref hasPressTrend,
                ref pressTrendMilliseconds,
                hitError);
            moveTrendMarker(pressTrendMarker, pressTrendMilliseconds);
        }
        updateTrendText();

        string directionKey = hitError switch
        {
            < -0.05 => "gameplay.timing.early",
            > 0.05 => "gameplay.timing.late",
            _ => "gameplay.timing.on_time",
        };
        DisplayedDirectionKey = directionKey;
        latestText.Text = YokkoStrings.Get(
            "gameplay.timing.latest",
            YokkoStrings.Get(release
                ? "gameplay.timing.release"
                : "gameplay.timing.press"),
            YokkoStrings.Get(directionKey),
            hitError);
        latestText.Colour = RatingColours.For(judgement.Rating);
        latestText.FinishTransforms();
        latestText.Alpha = 1;
        latestText.Delay(1000).FadeOut(500, Easing.OutQuint);

        RecordedMarkerCount++;
        LatestHitErrorMilliseconds = hitError;
        LatestMarkerPosition = markerPosition;
        LatestPhase = judgement.Phase;
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
        trendText.Text = string.Empty;
        trendText.Alpha = 0;
        latestText.Text = string.Empty;
        latestText.Alpha = 0;
    }

    private void moveTrendMarker(Circle marker, double error)
    {
        marker.FadeTo(0.9f, 100, Easing.OutQuint)
              .MoveToX(
                  barLeft + positionFor(error),
                  180,
                  Easing.OutQuint);
    }

    private void updateTrendText()
    {
        trendText.Text = hasPressTrend && hasReleaseTrend
            ? YokkoStrings.Get(
                "gameplay.timing.trend_both",
                pressTrendMilliseconds,
                releaseTrendMilliseconds)
            : hasReleaseTrend
                ? YokkoStrings.Get(
                    "gameplay.timing.trend_release",
                    releaseTrendMilliseconds)
                : YokkoStrings.Get(
                    "gameplay.timing.trend_press",
                    pressTrendMilliseconds);
        trendText.Alpha = 0.82f;
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
            Colour = new Color4(colour.R, colour.G, colour.B, 0.82f),
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
        public TimingMarker()
        {
            Anchor = Anchor.TopLeft;
            Origin = Anchor.Centre;
            InternalChild = new Circle
            {
                RelativeSizeAxes = Axes.Both,
            };
        }

        public void Show(bool release, int lifetimeMilliseconds)
        {
            Size = release
                ? new Vector2(9, 4)
                : new Vector2(3, 18);
            Alpha = 0;
            this.FadeTo(0.95f, 80, Easing.OutQuint)
                .Then()
                .Delay(lifetimeMilliseconds - 980)
                .FadeOut(900, Easing.InQuint)
                .Expire();
        }
    }
}
