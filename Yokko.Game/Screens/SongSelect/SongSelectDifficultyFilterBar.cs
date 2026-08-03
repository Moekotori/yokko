using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Difficulty;
using Yokko.Game.Presentation;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectDifficultyFilterBar : ClickableContainer
{
    private const float track_left = 184;
    private const float track_width = 276;
    private const float track_y = 20;

    private readonly Action<double> changed;
    private readonly Box activeTrack;
    private readonly Circle marker;
    private readonly SpriteText label;
    private readonly SpriteText valueText;

    private double maximum = 30;
    private double value;
    private double step = 0.25;
    private bool pressed;

    internal string DisplayedUnit => label.Text.ToString();
    internal double DisplayedValue => value;

    internal SongSelectDifficultyFilterBar(Action<double> changed)
    {
        this.changed = changed;
        Size = new Vector2(520, 40);
        Masking = true;
        CornerRadius = 10;
        BorderThickness = 1.25f;
        BorderColour = new Color4(
            SongSelectTheme.Cyan.R,
            SongSelectTheme.Cyan.G,
            SongSelectTheme.Cyan.B,
            0.24f);

        InternalChildren =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SongSelectSurface.Ivory(0.98f),
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 12,
                Size = new Vector2(15),
                Icon = FontAwesome.Solid.Signal,
                Colour = SongSelectTheme.Cyan,
            },
            label = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 36,
                Text = YokkoStrings.Get(
                    "song_select.difficulty_range",
                    ManiaDifficultyPresentation.Unit(
                        ManiaDifficultyRatingMode.EtternaMsd)),
                Font = HomeTypography.Display(12),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.80f),
            },
            new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 105,
                Size = new Vector2(70, 30),
                Masking = true,
                    CornerRadius = 8,
                Children =
                [
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SongSelectTheme.PaleCyan,
                    },
                    valueText = new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = YokkoStrings.Get("song_select.all"),
                        Font = HomeTypography.Control(15),
                        Colour = SongSelectTheme.Navy,
                    },
                ],
            },
            new Box
            {
                Position = new Vector2(track_left, track_y - 3),
                Size = new Vector2(track_width, 6),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.16f),
            },
            new FillFlowContainer
            {
                Position = new Vector2(track_left, track_y - 3),
                Size = new Vector2(track_width, 6),
                Direction = FillDirection.Horizontal,
                Children =
                [
                    segment(SongSelectTheme.Cyan),
                    segment(new Color4(0.55f, 0.9f, 0.45f, 1f)),
                    segment(SongSelectTheme.Yellow),
                    segment(new Color4(1f, 0.56f, 0.34f, 1f)),
                    segment(SongSelectTheme.Pink),
                    segment(new Color4(0.64f, 0.45f, 0.96f, 1f)),
                ],
            },
            activeTrack = new Box
            {
                Position = new Vector2(track_left, track_y - 3),
                Height = 6,
                Colour = SongSelectTheme.Navy,
                Alpha = 0.28f,
            },
            marker = new Circle
            {
                Origin = Anchor.Centre,
                Position = new Vector2(track_left, track_y),
                Size = new Vector2(17),
                BorderThickness = 2,
                BorderColour = SongSelectTheme.Cyan,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
            },
            new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -14,
                Text = "∞",
                Font = HomeTypography.Display(14),
                Colour = SongSelectTheme.Navy,
            },
        ];
    }

    internal void SetState(
        ManiaDifficultyRatingMode mode,
        double newValue)
    {
        maximum = mode == ManiaDifficultyRatingMode.EtternaMsd
            ? 30
            : 10;
        step = mode == ManiaDifficultyRatingMode.EtternaMsd
            ? 0.25
            : 0.1;
        label.Text = YokkoStrings.Get(
            "song_select.difficulty_range",
            ManiaDifficultyPresentation.Unit(mode));
        updateVisualValue(newValue);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left)
            return false;

        pressed = true;
        marker.ClearTransforms();
        marker.BorderColour = SongSelectTheme.Pink;
        marker.ScaleTo(1.16f, 80, Easing.OutQuint);
        updateFrom(e.ScreenSpaceMousePosition);
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e) => pressed;

    protected override void OnDrag(DragEvent e) =>
        updateFrom(e.ScreenSpaceMousePosition);

    protected override void OnMouseUp(MouseUpEvent e)
    {
        pressed = false;
        marker.BorderColour = SongSelectTheme.Cyan;
        marker.ScaleTo(IsHovered ? 1.08f : 1, 110, Easing.OutQuint);
        base.OnMouseUp(e);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!pressed)
            marker.ScaleTo(1.08f, 90, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        if (!pressed)
            marker.ScaleTo(1, 110, Easing.OutQuint);
    }

    private void updateFrom(Vector2 screenPosition)
    {
        double progress = Math.Clamp(
            (ToLocalSpace(screenPosition).X - track_left) / track_width,
            0,
            1);
        double nextValue = Math.Round(
            Math.Round(progress * maximum / step) * step,
            2);
        if (Math.Abs(nextValue - value) < 0.0001)
            return;

        updateVisualValue(nextValue);
        changed(nextValue);
    }

    private void updateVisualValue(double newValue)
    {
        value = Math.Clamp(newValue, 0, maximum);
        float x = (float)(value / maximum * track_width);
        activeTrack.Width = x;
        marker.X = track_left + x;
        LocalisableString displayedValue = value <= 0
            ? YokkoStrings.Get("song_select.all")
            : $"{value:0.00}+";
        valueText.Text = displayedValue;
        valueText.Colour = value <= 0
            ? SongSelectTheme.Navy
            : SongSelectTheme.Pink;
    }

    private static Box segment(Color4 colour) => new()
    {
        Width = track_width / 6,
        Height = 6,
        Colour = new Color4(colour.R, colour.G, colour.B, 0.62f),
    };
}
