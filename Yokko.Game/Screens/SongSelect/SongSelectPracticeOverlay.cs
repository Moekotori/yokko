using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Gameplay;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectPracticeOverlay : CompositeDrawable
{
    private const float timelineWidth = 920;
    private readonly double duration;
    private readonly Action<GameplayPracticePlan> start;
    private readonly Action cancel;
    private readonly Box rangeFill;
    private readonly PracticeHandle startHandle;
    private readonly PracticeHandle endHandle;
    private readonly SpriteText rangeText;
    private readonly SpriteText repetitionText;
    private double rangeStart;
    private double rangeEnd;
    private int repetitions = 5;

    internal double RangeStart => rangeStart;
    internal double RangeEnd => rangeEnd;
    internal int Repetitions => repetitions;

    internal SongSelectPracticeOverlay(
        double durationMilliseconds,
        double initialStart,
        double initialEnd,
        Action<GameplayPracticePlan> start,
        Action cancel)
    {
        duration = Math.Max(1000, durationMilliseconds);
        this.start = start ?? throw new ArgumentNullException(nameof(start));
        this.cancel = cancel ?? throw new ArgumentNullException(nameof(cancel));
        rangeStart = Math.Clamp(initialStart, 0, duration - 500);
        rangeEnd = Math.Clamp(initialEnd, rangeStart + 500, duration);
        RelativeSizeAxes = Axes.Both;
        Depth = -1000;

        var panel = new Container
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = new Vector2(1120, 390),
            Masking = true,
            CornerRadius = 18,
            BorderThickness = 2,
            BorderColour = SongSelectTheme.Navy,
            Children =
            [
                new Box { RelativeSizeAxes = Axes.Both, Colour = SongSelectTheme.Ivory },
                new SpriteText { Position = new Vector2(54, 38), Text = "A–B PRACTICE", Font = HomeTypography.Hero(46), Colour = SongSelectTheme.Navy },
                new SpriteText { Position = new Vector2(56, 94), Text = "DRAG BOTH MARKERS  //  EACH LOOP HAS AN INDEPENDENT RESULT", Font = HomeTypography.Display(10), Colour = SongSelectTheme.Navy, Alpha = 0.55f },
                new Container
                {
                    Position = new Vector2(100, 164),
                    Size = new Vector2(timelineWidth, 44),
                    Children =
                    [
                        new Box { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Size = new Vector2(timelineWidth, 8), Colour = new Color4(SongSelectTheme.Navy.R, SongSelectTheme.Navy.G, SongSelectTheme.Navy.B, 0.15f) },
                        rangeFill = new Box { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Height = 12, Colour = SongSelectTheme.Cyan },
                        startHandle = new PracticeHandle("A", delta => moveStart(delta)),
                        endHandle = new PracticeHandle("B", delta => moveEnd(delta)),
                    ],
                },
                rangeText = new SpriteText { Position = new Vector2(100, 226), Font = HomeTypography.Display(18), Colour = SongSelectTheme.Navy },
                new SpriteText { Position = new Vector2(100, 278), Text = "LOOPS", Font = HomeTypography.Display(11), Colour = SongSelectTheme.Cyan },
                createButton("−", new Vector2(190, 266), () => setRepetitions(repetitions - 1), 58),
                repetitionText = new SpriteText { Position = new Vector2(301, 276), Origin = Anchor.TopCentre, Font = HomeTypography.Display(18), Colour = SongSelectTheme.Navy },
                createButton("+", new Vector2(350, 266), () => setRepetitions(repetitions + 1), 58),
                createButton("CANCEL", new Vector2(690, 300), cancel, 150),
                createButton("START PRACTICE", new Vector2(860, 300), startPractice, 210, primary: true),
            ],
        };

        InternalChildren =
        [
            new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(0.01f, 0.02f, 0.08f, 0.72f) },
            panel,
        ];
        updateDisplay();
    }

    internal void TriggerStart() => startPractice();

    private void moveStart(float deltaPixels)
    {
        rangeStart = Math.Clamp(
            rangeStart + deltaPixels / timelineWidth * duration,
            0,
            rangeEnd - 500);
        updateDisplay();
    }

    private void moveEnd(float deltaPixels)
    {
        rangeEnd = Math.Clamp(
            rangeEnd + deltaPixels / timelineWidth * duration,
            rangeStart + 500,
            duration);
        updateDisplay();
    }

    private void setRepetitions(int value)
    {
        repetitions = Math.Clamp(value, 1, 20);
        updateDisplay();
    }

    private void updateDisplay()
    {
        float startX = timelineWidth * (float)(rangeStart / duration);
        float endX = timelineWidth * (float)(rangeEnd / duration);
        startHandle.X = startX;
        endHandle.X = endX;
        rangeFill.X = startX;
        rangeFill.Width = Math.Max(1, endX - startX);
        rangeText.Text = $"A  {formatTime(rangeStart)}     B  {formatTime(rangeEnd)}     SPAN  {formatTime(rangeEnd - rangeStart)}";
        repetitionText.Text = repetitions.ToString();
    }

    private void startPractice() => start(new GameplayPracticePlan(
        rangeStart,
        rangeEnd,
        repetitions));

    private static string formatTime(double milliseconds) =>
        TimeSpan.FromMilliseconds(milliseconds).ToString(@"mm\:ss\.fff");

    private static Drawable createButton(
        string text,
        Vector2 position,
        Action action,
        float width,
        bool primary = false) => new YokkoButton(
        text,
        action,
        width,
        46,
        primary ? YokkoButtonStyle.Primary : YokkoButtonStyle.Secondary)
    {
        Position = position,
    };

    private partial class PracticeHandle : ClickableContainer
    {
        private readonly Action<float> dragged;

        public PracticeHandle(string label, Action<float> dragged)
        {
            this.dragged = dragged;
            Origin = Anchor.Centre;
            Position = new Vector2(0, 22);
            Size = new Vector2(34, 54);
            InternalChildren =
            [
                new Box { Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(4, 44), Colour = SongSelectTheme.Pink },
                new CircularContainer
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Size = new Vector2(28),
                    Masking = true,
                    Children =
                    [
                        new Box { RelativeSizeAxes = Axes.Both, Colour = SongSelectTheme.Navy },
                        new SpriteText { Anchor = Anchor.Centre, Origin = Anchor.Centre, Text = label, Font = HomeTypography.Display(11), Colour = Color4.White },
                    ],
                },
            ];
        }

        protected override bool OnDragStart(DragStartEvent e) => true;

        protected override void OnDrag(DragEvent e) => dragged(e.Delta.X);
    }
}
