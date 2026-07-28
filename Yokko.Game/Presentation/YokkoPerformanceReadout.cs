using System;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osuTK;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Presentation;

internal interface IFrameTimingSource
{
    bool TryRead(
        out double frameMarker,
        out double frameTimeMilliseconds);
}

internal sealed class GameHostDrawFrameTimingSource : IFrameTimingSource
{
    private readonly GameHost host;

    public GameHostDrawFrameTimingSource(GameHost host)
    {
        this.host = host;
    }

    public bool TryRead(
        out double frameMarker,
        out double frameTimeMilliseconds)
    {
        if (host.DrawThread?.Clock == null)
        {
            frameMarker = 0;
            frameTimeMilliseconds = 0;
            return false;
        }

        frameMarker = host.DrawThread.Clock.CurrentTime;
        frameTimeMilliseconds =
            host.DrawThread.Clock.ElapsedFrameTime;
        return frameMarker > 0 && frameTimeMilliseconds > 0;
    }
}

internal partial class YokkoPerformanceReadout : CompositeDrawable
{
    internal const float CardWidth = 118;
    internal const float CardHeight = 22;

    private const double display_refresh_milliseconds = 500;

    private readonly FrameTimingTracker tracker = new();
    private IFrameTimingSource timingSource;
    private SpriteText frameTimeText;
    private SpriteText framesPerSecondText;
    private double lastFrameMarker;
    private double elapsedSinceDisplayRefresh;
    private double displayedFrameTimeMilliseconds;

    internal YokkoPerformanceReadout(
        IFrameTimingSource timingSource = null)
    {
        this.timingSource = timingSource;
        Size = new Vector2(CardWidth, CardHeight);

        InternalChild = createCard();
    }

    internal string DisplayedFrameTime =>
        frameTimeText == null
            ? string.Empty
            : $"{frameTimeText.Text} ms";

    internal string DisplayedFramesPerSecond =>
        framesPerSecondText?.Text.ToString() ?? string.Empty;

    [BackgroundDependencyLoader]
    private void load(GameHost host)
    {
        timingSource ??= new GameHostDrawFrameTimingSource(host);
        refreshDisplay();
    }

    protected override void Update()
    {
        base.Update();

        if (timingSource?.TryRead(
                out double frameMarker,
                out double frameTimeMilliseconds) == true
            && frameMarker != lastFrameMarker)
        {
            lastFrameMarker = frameMarker;
            tracker.Record(frameTimeMilliseconds);
        }

        elapsedSinceDisplayRefresh += Time.Elapsed;

        if (elapsedSinceDisplayRefresh >= display_refresh_milliseconds)
        {
            elapsedSinceDisplayRefresh %= display_refresh_milliseconds;
            refreshDisplay();
        }
    }

    internal void RefreshForTesting() => refreshDisplay();

    private Drawable createCard()
    {
        return new Container
        {
            Size = new Vector2(CardWidth, CardHeight),
            Masking = true,
            CornerRadius = 3,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = HomeControlColours.Ivory,
                    Alpha = 0.94f,
                },
                new Box
                {
                    Size = new Vector2(CardWidth, 2),
                    Colour = HomeControlColours.Cyan,
                },
                new Box
                {
                    Position = new Vector2(7, 10),
                    Size = new Vector2(3),
                    Colour = HomeControlColours.Pink,
                },
                framesPerSecondText = new SpriteText
                {
                    Position = new Vector2(15, 4),
                    Width = 22,
                    Font = HomeTypography.Display(8)
                        .With(fixedWidth: true),
                    UseFullGlyphHeight = true,
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(39, 6),
                    Text = "FPS",
                    Font = HomeTypography.Display(5),
                    Spacing = new Vector2(0.4f, 0),
                    Colour = HomeControlColours.Navy,
                },
                new Box
                {
                    Position = new Vector2(60, 5),
                    Size = new Vector2(1, 12),
                    Colour = HomeControlColours.Navy,
                },
                frameTimeText = new SpriteText
                {
                    Position = new Vector2(68, 4),
                    Width = 25,
                    Font = HomeTypography.Display(8)
                        .With(fixedWidth: true),
                    UseFullGlyphHeight = true,
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(96, 6),
                    Text = "ms",
                    Font = HomeTypography.Display(5),
                    Colour = HomeControlColours.Navy,
                },
            },
        };
    }

    private void refreshDisplay()
    {
        FrameTimingSnapshot snapshot = tracker.Snapshot();
        if (snapshot.Count == 0)
        {
            setTextIfChanged(frameTimeText, "—");
            setTextIfChanged(framesPerSecondText, "—");
            return;
        }

        if (!FrameTimingTracker.ShouldUpdateDisplay(
                displayedFrameTimeMilliseconds,
                snapshot.FrameTimeMilliseconds))
            return;

        displayedFrameTimeMilliseconds =
            snapshot.FrameTimeMilliseconds;
        setTextIfChanged(
            frameTimeText,
            displayedFrameTimeMilliseconds.ToString(
                "0.0",
                CultureInfo.InvariantCulture));
        setTextIfChanged(
            framesPerSecondText,
            FrameTimingTracker.QuantizeFramesPerSecond(
                snapshot.FramesPerSecond).ToString(
                    CultureInfo.InvariantCulture));
    }

    private static void setTextIfChanged(
        SpriteText target,
        string value)
    {
        if (target.Text.ToString() != value)
            target.Text = value;
    }
}
