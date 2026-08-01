using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Gameplay;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.Gameplay;

internal sealed record GameplayLayoutEditorSkinOption(
    string Id,
    string Name);

internal sealed record GameplayLayoutEditorLiveSettings(
    Func<IReadOnlyList<GameplayLayoutEditorSkinOption>> SkinOptions,
    Func<string> SelectedSkinId,
    Action<string> SelectSkin,
    Func<double> ScrollSpeed,
    Action<double> SetScrollSpeed,
    Func<ManiaScrollDirection> ScrollDirection,
    Action<ManiaScrollDirection> SetScrollDirection,
    Func<double> BackgroundDim,
    Action<double> SetBackgroundDim,
    Func<bool> LongNoteCutEnabled,
    Action<bool> SetLongNoteCutEnabled,
    Func<double> LongNoteCutAmount,
    Action<double> SetLongNoteCutAmount,
    Func<double> JudgementDisplayDuration,
    Action<double> SetJudgementDisplayDuration,
    Func<double> JudgementOpacity,
    Action<double> SetJudgementOpacity,
    Func<bool> ShowJudgementHitError,
    Action<bool> SetShowJudgementHitError,
    Func<bool> ShowTimingBar,
    Action<bool> SetShowTimingBar);

internal partial class GameplayLayoutEditorOverlay
{
    private LiveSettingsPanel liveSettingsPanel;

    private Drawable createLiveSettingsCard()
    {
        liveSettingsPanel = new LiveSettingsPanel(liveSettings);
        return liveSettingsPanel;
    }

    private partial class LiveSettingsPanel : CompositeDrawable
    {
        private readonly GameplayLayoutEditorLiveSettings settings;
        private readonly SpriteText skinName;
        private readonly SpriteText speedValue;
        private readonly SpriteText dimValue;
        private readonly SpriteText longNoteCutValue;
        private readonly CompactTextButton downscrollButton;
        private readonly CompactTextButton upscrollButton;
        private readonly CompactTextButton longNoteCutToggle;
        private readonly LongNoteCutPreview longNoteCutPreview;

        internal bool LongNoteCutPreviewEnabled =>
            longNoteCutPreview.IsCutEnabled;

        internal double LongNoteCutPreviewAmount =>
            longNoteCutPreview.CutAmount;

        public LiveSettingsPanel(
            GameplayLayoutEditorLiveSettings settings)
        {
            this.settings = settings
                            ?? throw new ArgumentNullException(
                                nameof(settings));

            Anchor = Anchor.TopRight;
            Origin = Anchor.TopRight;
            Position = new Vector2(-18, 678);
            Size = new Vector2(420, 157);
            Depth = -100;
            Masking = true;
            CornerRadius = 8;
            BorderThickness = 1.5f;
            BorderColour = HomeControlColours.Navy;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = HomeControlColours.Ivory,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 4,
                    Colour = HomeControlColours.Yellow,
                },
                createSettingsRow(
                    new Vector2(8, 29),
                    new Vector2(404, 31),
                    HomeControlColours.Cyan),
                createSettingsRow(
                    new Vector2(8, 61),
                    new Vector2(404, 31),
                    HomeControlColours.Yellow),
                createSettingsRow(
                    new Vector2(8, 93),
                    new Vector2(404, 31),
                    HomeControlColours.Cyan),
                createSettingsRow(
                    new Vector2(8, 125),
                    new Vector2(404, 31),
                    HomeControlColours.Pink),
                new SpriteText
                {
                    Position = new Vector2(14, 7),
                    Text = YokkoStrings.Get(
                        "gameplay.layout_editor.live_settings"),
                    Font = LayoutEditorTypography.Bold(10),
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.CentreLeft,
                    Position = new Vector2(16, 44.5f),
                    Text = YokkoStrings.Get(
                        "gameplay.layout_editor.skin"),
                    Font = LayoutEditorTypography.Bold(9),
                    Colour = HomeControlColours.Navy,
                },
                new CompactIconButton(
                    FontAwesome.Solid.ChevronLeft,
                    () => cycleSkin(-1))
                {
                    Position = new Vector2(78, 29),
                    Size = new Vector2(31),
                },
                new Container
                {
                    Position = new Vector2(115, 29),
                    Size = new Vector2(251, 31),
                    Masking = true,
                    CornerRadius = 6,
                    BorderThickness = 1.25f,
                    BorderColour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.35f),
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.White,
                        },
                        skinName = new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            MaxWidth = 235,
                            Truncate = true,
                            Font = LayoutEditorTypography.Bold(9),
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                new CompactIconButton(
                    FontAwesome.Solid.ChevronRight,
                    () => cycleSkin(1))
                {
                    Position = new Vector2(373, 29),
                    Size = new Vector2(31),
                },
                new SpriteText
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.CentreLeft,
                    Position = new Vector2(16, 76.5f),
                    Text = YokkoStrings.Get(
                        "gameplay.layout_editor.scroll_speed"),
                    Font = LayoutEditorTypography.Bold(9),
                    Colour = HomeControlColours.Navy,
                },
                new CompactIconButton(
                    FontAwesome.Solid.Minus,
                    () => adjustSpeed(-1))
                {
                    Position = new Vector2(78, 61),
                    Size = new Vector2(31),
                },
                new Container
                {
                    Position = new Vector2(115, 61),
                    Size = new Vector2(58, 31),
                    Masking = true,
                    CornerRadius = 6,
                    BorderThickness = 1.25f,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.White,
                        },
                        speedValue = new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Font = LayoutEditorTypography.Bold(9),
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                new CompactIconButton(
                    FontAwesome.Solid.Plus,
                    () => adjustSpeed(1))
                {
                    Position = new Vector2(179, 61),
                    Size = new Vector2(31),
                },
                downscrollButton = new CompactTextButton(
                    YokkoStrings.Get(
                        "gameplay.layout_editor.downscroll"),
                    () => settings.SetScrollDirection(
                        ManiaScrollDirection.Downscroll))
                {
                    Position = new Vector2(264, 61),
                    Size = new Vector2(66, 31),
                },
                upscrollButton = new CompactTextButton(
                    YokkoStrings.Get(
                        "gameplay.layout_editor.upscroll"),
                    () => settings.SetScrollDirection(
                        ManiaScrollDirection.Upscroll))
                {
                    Position = new Vector2(336, 61),
                    Size = new Vector2(68, 31),
                },
                new SpriteText
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.CentreLeft,
                    Position = new Vector2(16, 108.5f),
                    Text = YokkoStrings.Get(
                        "gameplay.layout_editor.background_dim"),
                    Font = LayoutEditorTypography.Bold(9),
                    Colour = HomeControlColours.Navy,
                },
                new CompactIconButton(
                    FontAwesome.Solid.Minus,
                    () => adjustDim(-0.05))
                {
                    Position = new Vector2(126, 93),
                    Size = new Vector2(31),
                },
                new Container
                {
                    Position = new Vector2(163, 93),
                    Size = new Vector2(82, 31),
                    Masking = true,
                    CornerRadius = 6,
                    BorderThickness = 1.25f,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.White,
                        },
                        dimValue = new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Font = LayoutEditorTypography.Bold(9),
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                new CompactIconButton(
                    FontAwesome.Solid.Plus,
                    () => adjustDim(0.05))
                {
                    Position = new Vector2(251, 93),
                    Size = new Vector2(31),
                },
                new SpriteText
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.CentreLeft,
                    Position = new Vector2(16, 140.5f),
                    Text = YokkoStrings.Get(
                        "gameplay.layout_editor.ln_cut"),
                    Font = LayoutEditorTypography.Bold(9),
                    Colour = HomeControlColours.Navy,
                },
                longNoteCutToggle = new CompactTextButton(
                    YokkoStrings.Get("gameplay.layout_editor.off"),
                    () => settings.SetLongNoteCutEnabled(
                        !settings.LongNoteCutEnabled()))
                {
                    Position = new Vector2(72, 125),
                    Size = new Vector2(56, 31),
                },
                new CompactIconButton(
                    FontAwesome.Solid.Minus,
                    () => adjustLongNoteCut(-1))
                {
                    Position = new Vector2(136, 125),
                    Size = new Vector2(31),
                },
                new Container
                {
                    Position = new Vector2(173, 125),
                    Size = new Vector2(66, 31),
                    Masking = true,
                    CornerRadius = 6,
                    BorderThickness = 1.25f,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.White,
                        },
                        longNoteCutValue = new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Font = LayoutEditorTypography.Bold(9),
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                new CompactIconButton(
                    FontAwesome.Solid.Plus,
                    () => adjustLongNoteCut(1))
                {
                    Position = new Vector2(247, 125),
                    Size = new Vector2(31),
                },
                longNoteCutPreview = new LongNoteCutPreview
                {
                    Position = new Vector2(344, 92),
                    Size = new Vector2(60, 64),
                },
            };
        }

        private static Container createSettingsRow(
            Vector2 position,
            Vector2 size,
            Color4 accent) => new()
        {
            Position = position,
            Size = size,
            Masking = true,
            CornerRadius = 6,
            BorderThickness = 1,
            BorderColour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.12f),
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        accent.R,
                        accent.G,
                        accent.B,
                        0.09f),
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 3,
                    Colour = accent,
                },
            },
        };

        protected override void Update()
        {
            base.Update();
            refresh();
        }

        private void refresh()
        {
            IReadOnlyList<GameplayLayoutEditorSkinOption> options =
                settings.SkinOptions();
            string selectedId = settings.SelectedSkinId() ?? string.Empty;
            GameplayLayoutEditorSkinOption selected = options
                .FirstOrDefault(option =>
                    string.Equals(
                        option.Id,
                        selectedId,
                        StringComparison.OrdinalIgnoreCase))
                ?? options.FirstOrDefault();
            skinName.Text = string.IsNullOrWhiteSpace(selected?.Name)
                ? YokkoStrings.Get(
                    "gameplay.layout_editor.default_skin")
                : selected.Name;
            speedValue.Text = settings.ScrollSpeed().ToString("0.0");
            dimValue.Text =
                $"{Math.Round(settings.BackgroundDim() * 100):0}%";
            bool cutEnabled = settings.LongNoteCutEnabled();
            double cutAmount = settings.LongNoteCutAmount();
            longNoteCutToggle.SetSelected(cutEnabled);
            longNoteCutToggle.SetText(YokkoStrings.Get(
                cutEnabled
                    ? "gameplay.layout_editor.on"
                    : "gameplay.layout_editor.off"));
            longNoteCutValue.Text = $"{cutAmount:0.0}x";

            ManiaScrollDirection direction =
                settings.ScrollDirection();
            downscrollButton.SetSelected(
                direction == ManiaScrollDirection.Downscroll);
            upscrollButton.SetSelected(
                direction == ManiaScrollDirection.Upscroll);
            longNoteCutPreview.SetState(
                cutEnabled,
                cutAmount,
                direction);
        }

        private void cycleSkin(int direction)
        {
            IReadOnlyList<GameplayLayoutEditorSkinOption> options =
                settings.SkinOptions();
            if (options.Count == 0)
                return;

            string selectedId = settings.SelectedSkinId() ?? string.Empty;
            int index = -1;
            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(
                        options[i].Id,
                        selectedId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }

            int next = (index + direction + options.Count)
                       % options.Count;
            settings.SelectSkin(options[next].Id);
            refresh();
        }

        private void adjustSpeed(double direction)
        {
            settings.SetScrollSpeed(
                OsuManiaScrollSpeed.Adjust(
                    settings.ScrollSpeed(),
                    direction));
            refresh();
        }

        private void adjustDim(double amount)
        {
            settings.SetBackgroundDim(
                Math.Clamp(
                    settings.BackgroundDim() + amount,
                    YokkoGameplaySettings.MinimumBackgroundDim,
                    YokkoGameplaySettings.MaximumBackgroundDim));
            refresh();
        }

        private void adjustLongNoteCut(double direction)
        {
            double step = YokkoSkinSettings.LongNoteCutAmountStep;
            double next = Math.Round(
                (settings.LongNoteCutAmount() + direction * step)
                / step) * step;
            settings.SetLongNoteCutAmount(Math.Clamp(
                next,
                YokkoSkinSettings.MinimumLongNoteCutAmount,
                YokkoSkinSettings.MaximumLongNoteCutAmount));
            refresh();
        }
    }

    private partial class LongNoteCutPreview : CompositeDrawable
    {
        private readonly Box body;
        private readonly Box removedBody;
        private readonly Circle head;
        private readonly Circle tail;
        private readonly Circle originalTail;

        internal bool IsCutEnabled { get; private set; }

        internal double CutAmount { get; private set; }

        public LongNoteCutPreview()
        {
            Masking = true;
            CornerRadius = 7;
            BorderThickness = 1.25f;
            BorderColour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.35f);

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = HomeControlColours.PaleCyan,
                    Alpha = 0.48f,
                },
                removedBody = new Box
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Width = 5,
                    Colour = HomeControlColours.Pink,
                    Alpha = 0.24f,
                },
                body = new Box
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Width = 9,
                    Colour = HomeControlColours.Navy,
                },
                originalTail = new Circle
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(14),
                    Colour = HomeControlColours.Pink,
                    Alpha = 0.2f,
                },
                tail = new Circle
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(14),
                    Colour = HomeControlColours.Cyan,
                },
                head = new Circle
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(18),
                    Colour = HomeControlColours.Yellow,
                    BorderThickness = 1,
                    BorderColour = HomeControlColours.Navy,
                },
            };
        }

        internal void SetState(
            bool enabled,
            double amount,
            ManiaScrollDirection direction)
        {
            IsCutEnabled = enabled;
            CutAmount = Math.Clamp(
                amount,
                YokkoSkinSettings.MinimumLongNoteCutAmount,
                YokkoSkinSettings.MaximumLongNoteCutAmount);

            const float upperY = 11;
            const float lowerY = 53;
            bool upscroll = direction == ManiaScrollDirection.Upscroll;
            float headY = upscroll ? upperY : lowerY;
            float originalTailY = upscroll ? lowerY : upperY;
            float cutDistance = enabled
                ? (float)(CutAmount
                    / YokkoSkinSettings.MaximumLongNoteCutAmount * 31)
                : 0;
            float tailY = originalTailY
                          + Math.Sign(headY - originalTailY)
                          * cutDistance;

            head.Y = headY;
            tail.Y = tailY;
            originalTail.Y = originalTailY;

            body.Y = Math.Min(headY, tailY);
            body.Height = Math.Abs(headY - tailY);
            removedBody.Y = Math.Min(originalTailY, tailY);
            removedBody.Height = Math.Abs(originalTailY - tailY);
            removedBody.Alpha = enabled && cutDistance > 0
                ? 0.24f
                : 0;
            originalTail.Alpha = enabled && cutDistance > 0
                ? 0.2f
                : 0;
            this.Alpha = enabled ? 1 : 0.48f;
        }
    }

    private partial class CompactIconButton : ClickableContainer
    {
        private readonly Box background;

        public CompactIconButton(IconUsage icon, Action action)
        {
            Action = action;
            Masking = true;
            CornerRadius = 6;
            BorderThickness = 1.25f;
            BorderColour = HomeControlColours.Navy;
            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = HomeControlColours.PaleCyan,
                },
                new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(14),
                    Icon = icon,
                    Colour = HomeControlColours.Navy,
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeColour(
                HomeControlColours.Yellow,
                80,
                Easing.OutQuint);
            this.ScaleTo(1.06f, 90, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeColour(
                HomeControlColours.PaleCyan,
                100,
                Easing.OutQuint);
            this.ScaleTo(1, 110, Easing.OutQuint);
        }
    }

    private partial class CompactTextButton : ClickableContainer
    {
        private readonly Box background;
        private readonly SpriteText label;
        private bool selected;

        public CompactTextButton(LocalisableString text, Action action)
        {
            Action = action;
            Masking = true;
            CornerRadius = 6;
            BorderThickness = 1.25f;
            BorderColour = HomeControlColours.Navy;
            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
                label = new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = text,
                    Font = LayoutEditorTypography.Bold(9),
                    Colour = HomeControlColours.Navy,
                },
            };
        }

        internal void SetSelected(bool value)
        {
            selected = value;
            background.Colour = selected
                ? HomeControlColours.Yellow
                : Color4.White;
            BorderColour = selected
                ? HomeControlColours.Pink
                : HomeControlColours.Navy;
        }

        internal void SetText(LocalisableString text) =>
            label.Text = text;

        protected override bool OnHover(HoverEvent e)
        {
            if (!selected)
            {
                background.FadeColour(
                    HomeControlColours.PaleCyan,
                    80,
                    Easing.OutQuint);
            }

            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e) =>
            SetSelected(selected);
    }
}
