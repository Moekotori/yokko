using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
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
    internal const float CardWidth = 150;
    internal const float CardHeight = 42;
    internal const float AccentOffset = 3;

    private const double display_refresh_milliseconds = 100;

    private readonly FrameTimingTracker tracker = new();
    private readonly Box[] graphBars = new Box[5];
    private IFrameTimingSource timingSource;
    private SpriteText frameTimeText;
    private SpriteText framesPerSecondText;
    private double lastFrameMarker;
    private double elapsedSinceDisplayRefresh;

    internal YokkoPerformanceReadout(
        IFrameTimingSource timingSource = null)
    {
        this.timingSource = timingSource;
        Size = new Vector2(
            CardWidth + AccentOffset,
            CardHeight + AccentOffset);

        InternalChildren = new Drawable[]
        {
            createAccentLayer(),
            createCard(),
            createCornerDiamond(),
        };
    }

    internal string DisplayedFrameTime =>
        frameTimeText?.Text.ToString() ?? string.Empty;

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
        if (elapsedSinceDisplayRefresh < display_refresh_milliseconds)
            return;

        elapsedSinceDisplayRefresh %= display_refresh_milliseconds;
        refreshDisplay();
    }

    internal void RefreshForTesting() => refreshDisplay();

    private Drawable createAccentLayer() => new Container
    {
        Position = new Vector2(AccentOffset),
        Size = new Vector2(CardWidth, CardHeight),
        Masking = true,
        CornerRadius = 8,
        Child = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = HomeControlColours.Cyan,
        },
    };

    private Drawable createCard()
    {
        var graph = new Container
        {
            Position = new Vector2(111, 8),
            Size = new Vector2(31, 27),
        };

        for (int index = 0; index < graphBars.Length; index++)
        {
            graph.Add(graphBars[index] = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                X = index * 6,
                Width = 4,
                Height = 7 + index * 3,
                Colour = index == 2
                    ? HomeControlColours.Pink
                    : HomeControlColours.Cyan,
            });
        }

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
                    Position = new Vector2(7, 8),
                    Size = new Vector2(25, 26),
                    Masking = true,
                    CornerRadius = 6,
                    BorderThickness = 1.5f,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = HomeControlColours.PaleCyan,
                        },
                        new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(16),
                            Icon = FontAwesome.Solid.Heartbeat,
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                frameTimeText = new SpriteText
                {
                    Position = new Vector2(38, 2),
                    Font = HomeTypography.Display(15),
                    Colour = HomeControlColours.Navy,
                },
                framesPerSecondText = new SpriteText
                {
                    Position = new Vector2(39, 23),
                    Font = HomeTypography.Display(7),
                    Spacing = new Vector2(0.6f, 0),
                    Colour = HomeControlColours.Navy,
                },
                graph,
            },
        };
    }

    private Drawable createCornerDiamond() => new Container
    {
        Position = new Vector2(141, -1),
        Size = new Vector2(9),
        Rotation = 45,
        Masking = true,
        CornerRadius = 1.5f,
        BorderThickness = 1.5f,
        BorderColour = HomeControlColours.Navy,
        Child = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = HomeControlColours.Yellow,
        },
    };

    private void refreshDisplay()
    {
        FrameTimingSnapshot snapshot = tracker.Snapshot();
        if (snapshot.Count == 0)
        {
            frameTimeText.Text = "— ms";
            framesPerSecondText.Text = "— FPS";
            return;
        }

        frameTimeText.Text =
            $"{snapshot.FrameTimeMilliseconds:0.0} ms";
        framesPerSecondText.Text =
            $"{snapshot.FramesPerSecond} FPS";
        refreshGraph(snapshot.RecentFrameTimes);
    }

    private void refreshGraph(double[] samples)
    {
        if (samples.Length == 0)
            return;

        double minimum = double.MaxValue;
        double maximum = double.MinValue;
        foreach (double sample in samples)
        {
            minimum = Math.Min(minimum, sample);
            maximum = Math.Max(maximum, sample);
        }

        for (int index = 0; index < graphBars.Length; index++)
        {
            double sample = samples[
                Math.Max(0, samples.Length - graphBars.Length + index)];
            double normalized = maximum - minimum < 0.05
                ? 0.45
                : (sample - minimum) / (maximum - minimum);
            graphBars[index].Height = (float)(7 + normalized * 17);
            graphBars[index].Colour =
                sample == maximum && maximum - minimum >= 0.05
                    ? HomeControlColours.Pink
                    : HomeControlColours.Cyan;
        }
    }
}
