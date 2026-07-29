using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Mods;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>
/// Configures the settings exposed by osu!lazer's Mania Cover and Flashlight
/// Mods at the pinned parity commit.
/// </summary>
internal partial class SongSelectVisibilitySettings : CompositeDrawable
{
    private readonly SpriteText title;
    private readonly SpriteText settingLabel;
    private readonly SpriteText valueText;
    private readonly SpriteText minimumText;
    private readonly SpriteText maximumText;
    private readonly VisibilitySlider slider;
    private readonly OptionButton primaryOption;
    private readonly OptionButton secondaryOption;
    private readonly SpriteText statusText;
    private readonly Action<double> coverCoverageChanged;
    private readonly Action<ManiaCoverDirection> coverDirectionChanged;
    private readonly Action<double> flashlightSizeChanged;
    private readonly Action<bool> flashlightComboBasedChanged;
    private ManiaModId activeMod = ManiaModId.Cover;
    private bool enabled;
    private ManiaCoverDirection coverDirection;
    private bool flashlightComboBased;

    internal SongSelectVisibilitySettings(
        Action<double> coverCoverageChanged,
        Action<ManiaCoverDirection> coverDirectionChanged,
        Action<double> flashlightSizeChanged,
        Action<bool> flashlightComboBasedChanged)
    {
        this.coverCoverageChanged = coverCoverageChanged;
        this.coverDirectionChanged = coverDirectionChanged;
        this.flashlightSizeChanged = flashlightSizeChanged;
        this.flashlightComboBasedChanged =
            flashlightComboBasedChanged;
        Size = new Vector2(202, 224);

        InternalChildren = new Drawable[]
        {
            title = new SpriteText
            {
                Font = HomeTypography.Display(12),
                Spacing = new Vector2(0.35f, 0),
                Colour = SongSelectTheme.Ivory,
            },
            settingLabel = new SpriteText
            {
                Y = 24,
                Font = HomeTypography.Body(10),
                Colour = SongSelectTheme.Muted,
            },
            valueText = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Y = 19,
                Font = HomeTypography.Display(19),
                Colour = SongSelectTheme.Yellow,
            },
            slider = new VisibilitySlider(onSliderChanged)
            {
                Y = 50,
            },
            minimumText = new SpriteText
            {
                Y = 85,
                Font = HomeTypography.Body(9),
                Colour = SongSelectTheme.Muted,
            },
            maximumText = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Y = 85,
                Font = HomeTypography.Body(9),
                Colour = SongSelectTheme.Muted,
            },
            new Box
            {
                Y = 108,
                Size = new Vector2(202, 1),
                Colour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.25f),
            },
            primaryOption = new OptionButton(onPrimaryOption)
            {
                Position = new Vector2(0, 128),
            },
            secondaryOption = new OptionButton(onSecondaryOption)
            {
                Position = new Vector2(105, 128),
            },
            statusText = new SpriteText
            {
                Y = 195,
                Font = HomeTypography.Body(10),
                Colour = SongSelectTheme.Cyan,
            },
        };

        SetState(
            ManiaModId.Cover,
            false,
            0.5,
            ManiaCoverDirection.AlongScroll,
            1,
            false);
    }

    internal void SetState(
        ManiaModId mod,
        bool isEnabled,
        double coverCoverage,
        ManiaCoverDirection direction,
        double flashlightSizeMultiplier,
        bool comboBasedSize)
    {
        if (mod is not ManiaModId.Cover
            and not ManiaModId.Flashlight)
        {
            throw new ArgumentOutOfRangeException(nameof(mod));
        }

        activeMod = mod;
        enabled = isEnabled;
        coverDirection = direction;
        flashlightComboBased = comboBasedSize;
        bool cover = mod == ManiaModId.Cover;

        title.Text = cover ? "COVER" : "FLASHLIGHT";
        settingLabel.Text = cover
            ? "COVERAGE"
            : "WINDOW SIZE";
        double value = cover
            ? coverCoverage
            : flashlightSizeMultiplier;
        valueText.Text = cover
            ? $"{value * 100:0}%"
            : $"{value:0.0}x";
        slider.SetState(
            isEnabled,
            cover ? 0.2 : 0.5,
            cover ? 0.8 : 3,
            0.1,
            value);
        minimumText.Text = cover ? "20%" : "0.5x";
        maximumText.Text = cover ? "80%" : "3.0x";

        primaryOption.SetLabel(
            cover ? "WITH SCROLL" : "FIXED SIZE");
        secondaryOption.SetLabel(
            cover ? "AGAINST" : "COMBO SIZE");
        primaryOption.SetState(
            isEnabled,
            cover
                ? direction == ManiaCoverDirection.AlongScroll
                : !comboBasedSize);
        secondaryOption.SetState(
            isEnabled,
            cover
                ? direction == ManiaCoverDirection.AgainstScroll
                : comboBasedSize);
        statusText.Text = !isEnabled
            ? $"SELECT {(cover ? "CO" : "FL")} TO CONFIGURE"
            : cover
                ? direction == ManiaCoverDirection.AlongScroll
                    ? "COVER EXPANDS WITH SCROLL"
                    : "COVER EXPANDS AGAINST SCROLL"
                : comboBasedSize
                    ? "WINDOW SHRINKS WITH COMBO"
                    : "WINDOW SIZE STAYS FIXED";
        this.ClearTransforms();
        this.FadeTo(isEnabled ? 1 : 0.42f, 120, Easing.OutQuint);
    }

    private void onSliderChanged(double value)
    {
        if (!enabled)
            return;

        if (activeMod == ManiaModId.Cover)
            coverCoverageChanged(value);
        else
            flashlightSizeChanged(value);
    }

    private void onPrimaryOption()
    {
        if (!enabled)
            return;

        if (activeMod == ManiaModId.Cover)
        {
            coverDirection =
                ManiaCoverDirection.AlongScroll;
            coverDirectionChanged(coverDirection);
        }
        else
        {
            flashlightComboBased = false;
            flashlightComboBasedChanged(false);
        }
    }

    private void onSecondaryOption()
    {
        if (!enabled)
            return;

        if (activeMod == ManiaModId.Cover)
        {
            coverDirection =
                ManiaCoverDirection.AgainstScroll;
            coverDirectionChanged(coverDirection);
        }
        else
        {
            flashlightComboBased = true;
            flashlightComboBasedChanged(true);
        }
    }

    private partial class VisibilitySlider : CompositeDrawable
    {
        private const float track_width = 202;
        private readonly Action<double> changed;
        private readonly Box fill;
        private readonly Circle knob;
        private bool enabled;
        private double minimum;
        private double maximum;
        private double precision;

        internal VisibilitySlider(Action<double> changed)
        {
            this.changed = changed;
            Size = new Vector2(track_width, 28);
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    Y = 11,
                    Size = new Vector2(track_width, 5),
                    Colour = new Color4(
                        SongSelectTheme.Navy.R,
                        SongSelectTheme.Navy.G,
                        SongSelectTheme.Navy.B,
                        0.9f),
                },
                fill = new Box
                {
                    Y = 11,
                    Height = 5,
                    Colour = SongSelectTheme.Pink,
                },
                knob = new Circle
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.Centre,
                    Y = 2,
                    Size = new Vector2(14),
                    Colour = SongSelectTheme.Ivory,
                    BorderThickness = 2.5f,
                    BorderColour = SongSelectTheme.Pink,
                },
            };
        }

        internal void SetState(
            bool isEnabled,
            double minimum,
            double maximum,
            double precision,
            double value)
        {
            enabled = isEnabled;
            this.minimum = minimum;
            this.maximum = maximum;
            this.precision = precision;
            double progress =
                Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);
            float x = (float)(progress * track_width);
            fill.Width = x;
            knob.X = x;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            updateFrom(e.ScreenSpaceMousePosition);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e) => enabled;

        protected override void OnDrag(DragEvent e) =>
            updateFrom(e.ScreenSpaceMousePosition);

        private void updateFrom(Vector2 screenPosition)
        {
            if (!enabled)
                return;

            double progress = Math.Clamp(
                ToLocalSpace(screenPosition).X / track_width,
                0,
                1);
            double raw = minimum + progress * (maximum - minimum);
            double value = Math.Round(raw / precision) * precision;
            changed(Math.Clamp(value, minimum, maximum));
        }
    }

    private partial class OptionButton : ClickableContainer
    {
        private readonly Box background;
        private readonly SpriteText label;
        private bool enabled;

        internal OptionButton(Action action)
        {
            Action = () =>
            {
                if (enabled)
                    action();
            };
            Size = new Vector2(97, 42);
            Masking = true;
            CornerRadius = 5;
            BorderThickness = 1.5f;
            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                label = new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Font = HomeTypography.Display(9),
                },
            };
        }

        internal void SetLabel(string text) =>
            label.Text = text;

        internal void SetState(bool isEnabled, bool selected)
        {
            enabled = isEnabled;
            BorderColour = selected
                ? SongSelectTheme.Yellow
                : SongSelectTheme.Cyan;
            background.Colour = selected
                ? SongSelectTheme.Pink
                : new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.72f);
            label.Colour = selected
                ? SongSelectTheme.DeepNavy
                : SongSelectTheme.PaleCyan;
        }
    }
}
