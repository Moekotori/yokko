using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Beatmaps;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Gameplay;

internal partial class GameplayPauseOverlay : CompositeDrawable
{
    private const float designedWidth = 1280;
    private const float designedHeight = 720;

    private readonly YokkoBeatmap beatmap;
    private readonly Action resume;
    private readonly Action retry;
    private readonly Action openSettings;
    private readonly Action exitGameplay;
    private readonly PauseActionButton[] actions = new PauseActionButton[4];

    private Container stage;
    private int selectedAction;

    internal int ActionCount => actions.Length;
    internal int SelectedAction => selectedAction;

    public GameplayPauseOverlay(
        YokkoBeatmap beatmap,
        Action resume,
        Action retry,
        Action openSettings,
        Action exitGameplay)
    {
        this.beatmap = beatmap;
        this.resume = resume;
        this.retry = retry;
        this.openSettings = openSettings;
        this.exitGameplay = exitGameplay;

        RelativeSizeAxes = Axes.Both;
        Depth = -1000;
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.78f),
            },
            stage = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(designedWidth, designedHeight),
                Alpha = 0,
                Children = new Drawable[]
                {
                    createIvoryStage(),
                    createDecorations(),
                    createHeader(textures.Get("home-logo-hd")),
                    createActionColumn(),
                    createSongBadge(),
                },
            },
        };

        selectAction(0);
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        stage.FadeInFromZero(180, Easing.OutQuint)
             .MoveToX(-18)
             .MoveToX(0, 360, Easing.OutQuint);
    }

    public bool HandleKey(Key key)
    {
        switch (key)
        {
            case Key.Escape:
                resume();
                return true;

            case Key.Up:
            case Key.W:
                selectAction((selectedAction + actions.Length - 1) % actions.Length);
                return true;

            case Key.Down:
            case Key.S:
                selectAction((selectedAction + 1) % actions.Length);
                return true;

            case Key.Enter:
            case Key.Space:
                actions[selectedAction].Trigger();
                return true;

            case Key.R:
                retry();
                return true;

            default:
                return true;
        }
    }

    internal void SelectNext() =>
        selectAction((selectedAction + 1) % actions.Length);

    internal void TriggerSelected() => actions[selectedAction].Trigger();

    private Drawable createIvoryStage() =>
        new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 455,
                    Colour = HomeControlColours.Ivory,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 235,
                    X = 420,
                    Y = -24,
                    Height = 1.16f,
                    Rotation = 12,
                    Colour = HomeControlColours.Ivory,
                },
            },
        };

    private Drawable createHeader(Texture logoTexture) =>
        new Container
        {
            Position = new Vector2(58, 38),
            Size = new Vector2(520, 272),
            Children = new Drawable[]
            {
                new Sprite
                {
                    Position = new Vector2(0, 0),
                    Size = new Vector2(290, 96),
                    FillMode = FillMode.Fit,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Texture = logoTexture,
                },
                new SpriteText
                {
                    Position = new Vector2(0, 125),
                    Text = YokkoStrings.Get("gameplay.pause.title"),
                    Font = HomeTypography.Hero(62),
                    Colour = HomeControlColours.Navy,
                },
                new Box
                {
                    Position = new Vector2(4, 189),
                    Size = new Vector2(246, 15),
                    Colour = new Color4(
                        HomeControlColours.Yellow.R,
                        HomeControlColours.Yellow.G,
                        HomeControlColours.Yellow.B,
                        0.58f),
                },
                new SpriteText
                {
                    Position = new Vector2(4, 218),
                    Text = "P A U S E D",
                    Font = HomeTypography.Display(18),
                    Colour = HomeControlColours.Cyan,
                },
                new Box
                {
                    Position = new Vector2(232, 241),
                    Size = new Vector2(116, 2),
                    Colour = HomeControlColours.Cyan,
                },
                new SpriteIcon
                {
                    Position = new Vector2(342, 228),
                    Size = new Vector2(28),
                    Icon = FontAwesome.Solid.Heartbeat,
                    Colour = HomeControlColours.Cyan,
                },
            },
        };

    private Drawable createActionColumn()
    {
        actions[0] = new PauseActionButton(
            YokkoStrings.Get("gameplay.pause.resume"),
            YokkoStrings.Get("gameplay.pause.resume_hint"),
            FontAwesome.Solid.Play,
            true,
            resume,
            () => selectAction(0))
        {
            Position = new Vector2(58, 319),
        };
        actions[1] = new PauseActionButton(
            YokkoStrings.Get("gameplay.pause.retry"),
            string.Empty,
            FontAwesome.Solid.Redo,
            false,
            retry,
            () => selectAction(1))
        {
            Position = new Vector2(58, 454),
        };
        actions[2] = new PauseActionButton(
            YokkoStrings.Get("gameplay.pause.settings"),
            string.Empty,
            FontAwesome.Solid.Cog,
            false,
            openSettings,
            () => selectAction(2))
        {
            Position = new Vector2(58, 519),
        };
        actions[3] = new PauseActionButton(
            YokkoStrings.Get("gameplay.pause.exit"),
            string.Empty,
            FontAwesome.Solid.SignOutAlt,
            false,
            exitGameplay,
            () => selectAction(3))
        {
            Position = new Vector2(58, 584),
        };

        return new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = actions,
        };
    }

    private Drawable createSongBadge()
    {
        string difficulty = string.IsNullOrWhiteSpace(beatmap.DifficultyName)
            ? $"{(int)beatmap.KeyMode}K"
            : beatmap.DifficultyName;

        return new Container
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Position = new Vector2(-36, 34),
            Size = new Vector2(300, 76),
            Masking = true,
            CornerRadius = 10,
            BorderThickness = 2,
            BorderColour = HomeControlColours.Cyan,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.9f),
                },
                new Container
                {
                    Position = new Vector2(15, 13),
                    Size = new Vector2(48),
                    Masking = true,
                    CornerRadius = 8,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(
                                HomeControlColours.Cyan.R,
                                HomeControlColours.Cyan.G,
                                HomeControlColours.Cyan.B,
                                0.16f),
                        },
                        new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(24),
                            Icon = FontAwesome.Solid.Music,
                            Colour = HomeControlColours.Cyan,
                        },
                    },
                },
                new SpriteText
                {
                    Position = new Vector2(78, 13),
                    Text = beatmap.Title,
                    Font = HomeTypography.Display(18),
                    Colour = Color4.White,
                },
                new SpriteText
                {
                    Position = new Vector2(78, 42),
                    Text = difficulty,
                    Font = HomeTypography.Body(13),
                    Colour = HomeControlColours.PaleCyan,
                },
                new Box
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Size = new Vector2(17),
                    Rotation = 45,
                    Colour = HomeControlColours.Yellow,
                },
            },
        };
    }

    private Drawable createDecorations() =>
        new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new HomeDotField
                {
                    Position = new Vector2(374, 53),
                    Size = new Vector2(72, 48),
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.25f),
                },
                new HomeMicroLine
                {
                    Position = new Vector2(340, 30),
                    Width = 170,
                },
                new HomeBarcode("NO.004-KEY")
                {
                    Position = new Vector2(470, 660),
                },
                new SpriteIcon
                {
                    Position = new Vector2(470, 500),
                    Size = new Vector2(15),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = HomeControlColours.Pink,
                },
                new SpriteIcon
                {
                    Position = new Vector2(1188, 612),
                    Size = new Vector2(18),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = HomeControlColours.Pink,
                },
                new HomeTickRuler(410)
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -17,
                    Colour = HomeControlColours.Cyan,
                },
            },
        };

    private void selectAction(int index)
    {
        selectedAction = index;

        for (int i = 0; i < actions.Length; i++)
            actions[i]?.SetSelected(i == selectedAction);
    }

    private partial class PauseActionButton : ClickableContainer
    {
        private readonly bool primary;
        private readonly Action hoverAction;
        private readonly Box background;
        private readonly Box accent;
        private readonly SpriteIcon chevron;

        public PauseActionButton(
            LocalisableString title,
            LocalisableString hint,
            IconUsage icon,
            bool primary,
            Action action,
            Action hoverAction)
        {
            this.primary = primary;
            this.hoverAction = hoverAction;
            Action = action;
            Size = primary
                ? new Vector2(520, 116)
                : new Vector2(372, 56);

            float iconSize = primary ? 72 : 42;
            float iconInset = primary ? 28 : 8;
            float textX = primary ? 126 : 66;

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = primary ? 10 : 8,
                    BorderThickness = 2,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = primary
                                ? HomeControlColours.Navy
                                : HomeControlColours.Ivory,
                        },
                        primary
                            ? new HomeDotField
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                X = -58,
                                Size = new Vector2(120, 70),
                                Colour = new Color4(
                                    HomeControlColours.Cyan.R,
                                    HomeControlColours.Cyan.G,
                                    HomeControlColours.Cyan.B,
                                    0.28f),
                            }
                            : new Container
                            {
                                Alpha = 0,
                            },
                        new Container
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = iconInset,
                            Size = new Vector2(iconSize),
                            Masking = true,
                            CornerRadius = primary ? 9 : 7,
                            BorderThickness = primary ? 2 : 1.5f,
                            BorderColour = primary
                                ? Color4.White
                                : HomeControlColours.Navy,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = primary
                                        ? Color4.White
                                        : HomeControlColours.PaleCyan,
                                },
                                new SpriteIcon
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Size = primary
                                        ? new Vector2(30)
                                        : new Vector2(20),
                                    Icon = icon,
                                    Colour = HomeControlColours.Navy,
                                },
                            },
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = textX,
                            Text = title,
                            Font = primary
                                ? HomeTypography.Display(42)
                                : HomeTypography.Display(18),
                            Colour = primary
                                ? Color4.White
                                : HomeControlColours.Navy,
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Position = new Vector2(textX, -15),
                            Text = hint,
                            Font = HomeTypography.Body(12),
                            Colour = HomeControlColours.Cyan,
                            Alpha = primary ? 1 : 0,
                        },
                        chevron = new SpriteIcon
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            X = -20,
                            Size = primary
                                ? new Vector2(26)
                                : new Vector2(15),
                            Icon = FontAwesome.Solid.ChevronRight,
                            Colour = primary
                                ? HomeControlColours.Yellow
                                : HomeControlColours.Pink,
                        },
                    },
                },
                accent = new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    X = primary ? 18 : 12,
                    Width = primary ? 110 : 42,
                    Height = primary ? 4 : 3,
                    Colour = HomeControlColours.Pink,
                },
                new Box
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Size = new Vector2(primary ? 18 : 14),
                    Rotation = 45,
                    Colour = HomeControlColours.Yellow,
                },
            };
        }

        public void SetSelected(bool selected)
        {
            background.FadeColour(
                primary
                    ? selected
                        ? new Color4(0.055f, 0.15f, 0.7f, 1f)
                        : HomeControlColours.Navy
                    : selected
                        ? HomeControlColours.PaleCyan
                        : HomeControlColours.Ivory,
                100,
                Easing.OutQuint);
            accent.ResizeWidthTo(
                selected
                    ? primary ? 250 : 112
                    : primary ? 110 : 42,
                130,
                Easing.OutQuint);
            chevron.MoveToX(selected ? -12 : -20, 130, Easing.OutQuint);
            this.ScaleTo(selected ? 1.01f : 1f, 100, Easing.OutQuint);
        }

        public void Trigger() => Action?.Invoke();

        protected override bool OnHover(HoverEvent e)
        {
            hoverAction();
            return true;
        }
    }
}
