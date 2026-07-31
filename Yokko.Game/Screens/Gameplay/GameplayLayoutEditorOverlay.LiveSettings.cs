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
    Action<ManiaScrollDirection> SetScrollDirection);

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
        private readonly CompactTextButton downscrollButton;
        private readonly CompactTextButton upscrollButton;

        public LiveSettingsPanel(
            GameplayLayoutEditorLiveSettings settings)
        {
            this.settings = settings
                            ?? throw new ArgumentNullException(
                                nameof(settings));

            Anchor = Anchor.TopRight;
            Origin = Anchor.TopRight;
            Position = new Vector2(-18, 650);
            Size = new Vector2(320, 104);
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
                new SpriteText
                {
                    Position = new Vector2(12, 8),
                    Text = YokkoStrings.Get(
                        "gameplay.layout_editor.live_settings"),
                    Font = LayoutEditorTypography.Bold(9),
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.CentreLeft,
                    Position = new Vector2(12, 48),
                    Text = YokkoStrings.Get(
                        "gameplay.layout_editor.skin"),
                    Font = LayoutEditorTypography.Bold(8),
                    Colour = HomeControlColours.Navy,
                },
                new CompactIconButton(
                    FontAwesome.Solid.ChevronLeft,
                    () => cycleSkin(-1))
                {
                    Position = new Vector2(70, 32),
                    Size = new Vector2(28),
                },
                new Container
                {
                    Position = new Vector2(104, 32),
                    Size = new Vector2(170, 28),
                    Masking = true,
                    CornerRadius = 5,
                    BorderThickness = 1,
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
                            MaxWidth = 156,
                            Truncate = true,
                            Font = LayoutEditorTypography.Bold(8),
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                new CompactIconButton(
                    FontAwesome.Solid.ChevronRight,
                    () => cycleSkin(1))
                {
                    Position = new Vector2(280, 32),
                    Size = new Vector2(28),
                },
                new SpriteText
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.CentreLeft,
                    Position = new Vector2(12, 84),
                    Text = YokkoStrings.Get(
                        "gameplay.layout_editor.scroll_speed"),
                    Font = LayoutEditorTypography.Bold(8),
                    Colour = HomeControlColours.Navy,
                },
                new CompactIconButton(
                    FontAwesome.Solid.Minus,
                    () => adjustSpeed(-1))
                {
                    Position = new Vector2(58, 68),
                    Size = new Vector2(28),
                },
                new Container
                {
                    Position = new Vector2(92, 68),
                    Size = new Vector2(46, 28),
                    Masking = true,
                    CornerRadius = 5,
                    BorderThickness = 1,
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
                            Font = LayoutEditorTypography.Bold(8),
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                new CompactIconButton(
                    FontAwesome.Solid.Plus,
                    () => adjustSpeed(1))
                {
                    Position = new Vector2(144, 68),
                    Size = new Vector2(28),
                },
                downscrollButton = new CompactTextButton(
                    YokkoStrings.Get(
                        "gameplay.layout_editor.downscroll"),
                    () => settings.SetScrollDirection(
                        ManiaScrollDirection.Downscroll))
                {
                    Position = new Vector2(180, 68),
                    Size = new Vector2(60, 28),
                },
                upscrollButton = new CompactTextButton(
                    YokkoStrings.Get(
                        "gameplay.layout_editor.upscroll"),
                    () => settings.SetScrollDirection(
                        ManiaScrollDirection.Upscroll))
                {
                    Position = new Vector2(246, 68),
                    Size = new Vector2(62, 28),
                },
            };
        }

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

            ManiaScrollDirection direction =
                settings.ScrollDirection();
            downscrollButton.SetSelected(
                direction == ManiaScrollDirection.Downscroll);
            upscrollButton.SetSelected(
                direction == ManiaScrollDirection.Upscroll);
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
    }

    private partial class CompactIconButton : ClickableContainer
    {
        private readonly Box background;

        public CompactIconButton(IconUsage icon, Action action)
        {
            Action = action;
            Masking = true;
            CornerRadius = 5;
            BorderThickness = 1;
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
                    Size = new Vector2(12),
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
        private bool selected;

        public CompactTextButton(LocalisableString text, Action action)
        {
            Action = action;
            Masking = true;
            CornerRadius = 5;
            BorderThickness = 1;
            BorderColour = HomeControlColours.Navy;
            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = text,
                    Font = LayoutEditorTypography.Bold(8),
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
