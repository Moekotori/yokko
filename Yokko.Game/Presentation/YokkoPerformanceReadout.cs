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
    internal const float CardWidth = 156;
    internal const float CardHeight = 50;

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
            CornerRadius = 8,
            BorderThickness = 2,
            BorderColour = HomeControlColours.Navy,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = HomeControlColours.Ivory,
                },
                new Container
                {
                    Position = new Vector2(4),
                    Size = new Vector2(
                        CardWidth - 8,
                        CardHeight - 8),
                    Masking = true,
                    CornerRadius = 6,
                    BorderThickness = 1,
                    BorderColour = HomeControlColours.Cyan,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                    },
                },
                new SpriteText
                {
                    Position = new Vector2(13, 5),
                    Text = "FPS",
                    Font = HomeTypography.Display(8),
                    Spacing = new Vector2(1, 0),
                    Colour = HomeControlColours.Pink,
                },
                framesPerSecondText = new SpriteText
                {
                    Position = new Vector2(12, 16),
                    Width = 58,
                    Font = HomeTypography.Display(18)
                        .With(fixedWidth: true),
                    UseFullGlyphHeight = true,
                    Colour = HomeControlColours.Navy,
                },
                new Box
                {
                    Position = new Vector2(77, 8),
                    Size = new Vector2(2, 34),
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(88, 5),
                    Text = "FRAME",
                    Font = HomeTypography.Display(8),
                    Spacing = new Vector2(0.6f, 0),
                    Colour = HomeControlColours.Cyan,
                },
                frameTimeText = new SpriteText
                {
                    Position = new Vector2(87, 17),
                    Width = 40,
                    Font = HomeTypography.Display(16)
                        .With(fixedWidth: true),
                    UseFullGlyphHeight = true,
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(126, 23),
                    Text = "ms",
                    Font = HomeTypography.Display(9),
                    Colour = HomeControlColours.Navy,
                },
                createDotField(),
                createCornerDiamond(),
            },
        };
    }

    private static Drawable createDotField()
    {
        var dots = new Container
        {
            Position = new Vector2(12, 38),
            Size = new Vector2(14, 10),
        };

        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                dots.Add(new Circle
                {
                    Position = new Vector2(column * 5, row * 4),
                    Size = new Vector2(2),
                    Colour = HomeControlColours.Cyan,
                });
            }
        }

        return dots;
    }

    private static Drawable createCornerDiamond() => new Box
    {
        Position = new Vector2(142, 7),
        Size = new Vector2(7),
        Rotation = 45,
        Colour = HomeControlColours.Yellow,
    };

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
