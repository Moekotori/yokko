using System;
using System.Globalization;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>
/// Exposes osu!lazer Random's optional integer seed. Yokko resolves a concrete
/// seed before applying the beatmap transform so retry and replay stay exact.
/// </summary>
internal partial class SongSelectRandomSettings : CompositeDrawable
{
    private readonly Action<int> seedChanged;
    private readonly SeedTextBox seedTextBox;
    private readonly SpriteText statusText;
    private readonly RerollButton rerollButton;
    private bool enabled;
    private bool updatingState;

    internal int Seed { get; private set; }

    internal SongSelectRandomSettings(Action<int> seedChanged)
    {
        this.seedChanged = seedChanged;
        Size = new Vector2(202, 224);

        InternalChildren = new Drawable[]
        {
            new SpriteText
            {
                Text = "RANDOM",
                Font = HomeTypography.Display(12),
                Spacing = new Vector2(0.35f, 0),
                Colour = SongSelectTheme.Ivory,
            },
            new SpriteText
            {
                Y = 24,
                Text = "CUSTOM SEED",
                Font = HomeTypography.Body(10),
                Colour = SongSelectTheme.Muted,
            },
            seedTextBox = new SeedTextBox
            {
                Y = 48,
            },
            new Box
            {
                Y = 105,
                Size = new Vector2(202, 1),
                Colour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.25f),
            },
            rerollButton = new RerollButton(reroll)
            {
                Y = 124,
            },
            statusText = new SpriteText
            {
                Y = 190,
                Font = HomeTypography.Body(10),
                Colour = SongSelectTheme.Cyan,
            },
        };

        seedTextBox.Current.ValueChanged += onSeedTextChanged;
        SetState(false, 0);
    }

    internal void SetState(bool isEnabled, int seed)
    {
        enabled = isEnabled;
        Seed = seed;
        updatingState = true;
        seedTextBox.Current.Value =
            seed.ToString(CultureInfo.InvariantCulture);
        updatingState = false;
        seedTextBox.ReadOnly = !isEnabled;
        rerollButton.SetEnabled(isEnabled);
        statusText.Text = isEnabled
            ? "SAME SEED · SAME LANE SHUFFLE"
            : "SELECT RD TO CONFIGURE";
        statusText.Colour = SongSelectTheme.Cyan;
        this.ClearTransforms();
        this.FadeTo(isEnabled ? 1 : 0.42f, 120, Easing.OutQuint);
    }

    private void onSeedTextChanged(ValueChangedEvent<string> change)
    {
        if (updatingState || !enabled)
            return;

        if (!int.TryParse(
                change.NewValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int seed))
        {
            statusText.Text = "ENTER A 32-BIT INTEGER";
            statusText.Colour = SongSelectTheme.Pink;
            return;
        }

        Seed = seed;
        statusText.Text = "CUSTOM SEED APPLIED";
        statusText.Colour = SongSelectTheme.Cyan;
        seedChanged(seed);
    }

    private void reroll()
    {
        if (!enabled)
            return;

        int seed = Random.Shared.Next();
        SetState(true, seed);
        seedChanged(seed);
    }

    private partial class SeedTextBox : BasicTextBox
    {
        protected override float LeftRightPadding => 12;

        internal SeedTextBox()
        {
            Size = new Vector2(202, 42);
            Masking = true;
            CornerRadius = 5;
            BorderThickness = 1.5f;
            BorderColour = SongSelectTheme.Cyan;
            BackgroundUnfocused = new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.72f);
            BackgroundFocused = new Color4(
                SongSelectTheme.DeepNavy.R,
                SongSelectTheme.DeepNavy.G,
                SongSelectTheme.DeepNavy.B,
                0.96f);
            FontSize = 16;
            PlaceholderText = "integer seed";
        }

        protected override Drawable GetDrawableCharacter(char c) =>
            new SpriteText
            {
                Text = c.ToString(),
                Font = HomeTypography.Body(16),
                Colour = SongSelectTheme.Ivory,
            };

        protected override SpriteText CreatePlaceholder() => new()
        {
            Font = HomeTypography.Body(14),
            Colour = SongSelectTheme.Muted,
        };

        protected override void OnFocus(FocusEvent e)
        {
            base.OnFocus(e);
            BorderColour = SongSelectTheme.Yellow;
        }

        protected override void OnFocusLost(FocusLostEvent e)
        {
            base.OnFocusLost(e);
            BorderColour = SongSelectTheme.Cyan;
        }
    }

    private partial class RerollButton : ClickableContainer
    {
        private readonly Box background;
        private readonly SpriteText label;
        private bool enabled;

        internal RerollButton(Action action)
        {
            Action = () =>
            {
                if (enabled)
                    action();
            };
            Size = new Vector2(202, 42);
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
                    Text = "GENERATE NEW SEED",
                    Font = HomeTypography.Display(10),
                },
            };
        }

        internal void SetEnabled(bool value)
        {
            enabled = value;
            BorderColour = SongSelectTheme.Cyan;
            background.Colour = new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.72f);
            label.Colour = value
                ? SongSelectTheme.PaleCyan
                : SongSelectTheme.Muted;
        }
    }
}
