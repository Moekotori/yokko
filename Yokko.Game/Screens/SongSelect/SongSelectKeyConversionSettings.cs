using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Mods;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectKeyConversionSettings : CompositeDrawable
{
    private readonly KeyButton[] buttons;
    private readonly SpriteText status;
    private readonly DualButton dualButton;
    private bool canConvert;

    internal int? SelectedKeyCount { get; private set; }
    internal bool CanConvert => canConvert;

    internal SongSelectKeyConversionSettings(
        Action<ManiaModId> toggle)
    {
        Size = new Vector2(202, 224);
        buttons = Enumerable.Range(1, 10)
            .Select(keyCount => new KeyButton(
                keyCount,
                () =>
                {
                    if (canConvert)
                        toggle(modFor(keyCount));
                }))
            .ToArray();
        InternalChildren =
        [
            new SpriteText
            {
                Text = "KEY CONVERSION",
                Font = HomeTypography.Display(12),
                Colour = SongSelectTheme.Ivory,
            },
            new FillFlowContainer
            {
                Y = 31,
                Width = 202,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(6, 6),
                Children = buttons,
            },
            dualButton = new DualButton(() =>
            {
                if (canConvert)
                    toggle(ManiaModId.DualStages);
            })
            {
                Y = 108,
            },
            status = new SpriteText
            {
                Y = 147,
                Font = HomeTypography.Body(9),
                Colour = SongSelectTheme.Cyan,
            },
            new SpriteText
            {
                Y = 172,
                Text = "REGENERATES FROM ORIGINAL OBJECTS",
                Font = HomeTypography.Body(8),
                Colour = SongSelectTheme.Muted,
            },
            new SpriteText
            {
                Y = 190,
                Text = "NATIVE MANIA CHARTS STAY UNCHANGED",
                Font = HomeTypography.Body(8),
                Colour = SongSelectTheme.Muted,
            },
        ];
    }

    internal void SetState(
        bool isConvertible,
        int? keyCount,
        bool dualStages)
    {
        canConvert = isConvertible;
        SelectedKeyCount = keyCount;
        foreach (KeyButton button in buttons)
        {
            button.SetState(
                isConvertible,
                button.KeyCount == keyCount);
        }
        dualButton.SetState(isConvertible, dualStages);

        status.Text = isConvertible
            ? keyCount is int selected
                ? $"TARGET · {selected}{(dualStages ? " + " + selected : string.Empty)} KEYS"
                : dualStages
                    ? "TARGET · DUAL LAZER DEFAULT"
                    : "TARGET · LAZER DEFAULT"
            : "CURRENT SOURCE · MANIA NATIVE";
        status.Colour = isConvertible
            ? SongSelectTheme.Cyan
            : SongSelectTheme.Pink;
    }

    private partial class DualButton : ClickableContainer
    {
        private readonly Box background;
        private readonly SpriteText state;

        internal DualButton(Action action)
        {
            Action = action;
            Size = new Vector2(202, 29);
            Masking = true;
            CornerRadius = 4;
            BorderThickness = 1;
            InternalChildren =
            [
                background = new Box { RelativeSizeAxes = Axes.Both },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 9,
                    Text = "DUAL STAGES",
                    Font = HomeTypography.Body(10),
                    Colour = SongSelectTheme.Ivory,
                },
                state = new SpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -9,
                    Font = HomeTypography.Display(9),
                },
            ];
        }

        internal void SetState(bool enabled, bool selected)
        {
            BorderColour = selected
                ? SongSelectTheme.Yellow
                : SongSelectTheme.Cyan;
            background.Colour = selected
                ? SongSelectTheme.Pink
                : SongSelectTheme.Navy;
            state.Text = selected ? "2×" : "1×";
            state.Colour = selected
                ? SongSelectTheme.DeepNavy
                : SongSelectTheme.Muted;
            Alpha = enabled ? 1 : 0.42f;
        }
    }

    private static ManiaModId modFor(int keyCount) =>
        keyCount switch
        {
            1 => ManiaModId.Key1,
            2 => ManiaModId.Key2,
            3 => ManiaModId.Key3,
            4 => ManiaModId.Key4,
            5 => ManiaModId.Key5,
            6 => ManiaModId.Key6,
            7 => ManiaModId.Key7,
            8 => ManiaModId.Key8,
            9 => ManiaModId.Key9,
            10 => ManiaModId.Key10,
            _ => throw new ArgumentOutOfRangeException(nameof(keyCount)),
        };

    private partial class KeyButton : ClickableContainer
    {
        private readonly Box background;
        private readonly SpriteText label;

        internal int KeyCount { get; }

        internal KeyButton(int keyCount, Action action)
        {
            KeyCount = keyCount;
            Action = action;
            Size = new Vector2(35.6f, 34);
            Masking = true;
            CornerRadius = 4;
            BorderThickness = 1;
            InternalChildren =
            [
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                label = new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = $"{keyCount}K",
                    Font = HomeTypography.Display(10),
                },
            ];
        }

        internal void SetState(bool enabled, bool selected)
        {
            BorderColour = selected
                ? SongSelectTheme.Yellow
                : SongSelectTheme.Cyan;
            background.Colour = selected
                ? SongSelectTheme.Pink
                : SongSelectTheme.Navy;
            label.Colour = selected
                ? SongSelectTheme.DeepNavy
                : enabled
                    ? SongSelectTheme.PaleCyan
                    : SongSelectTheme.Muted;
            Alpha = enabled ? 1 : 0.42f;
        }
    }
}
