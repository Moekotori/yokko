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
using Yokko.Game.Gameplay;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Gameplay;

internal partial class GameplayPauseOverlay : CompositeDrawable
{
    private const float designedWidth = 1280;
    private const float designedHeight = 720;

    private readonly YokkoBeatmap beatmap;
    private readonly YokkoGameplaySettings gameplaySettings;
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
        YokkoGameplaySettings gameplaySettings,
        Action resume,
        Action retry,
        Action openSettings,
        Action exitGameplay)
    {
        this.beatmap = beatmap;
        this.gameplaySettings = gameplaySettings;
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
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    0.82f),
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
                    createMascot(textures.Get("yokko")),
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
        if (matches(ManiaShortcutAction.PauseOrBack, key))
        {
            resume();
            return true;
        }

        if (matches(ManiaShortcutAction.MenuPrevious, key)
            || matches(
                ManiaShortcutAction.MenuPreviousAlternate,
                key))
        {
            selectAction((selectedAction + actions.Length - 1) % actions.Length);
            return true;
        }

        if (matches(ManiaShortcutAction.MenuNext, key)
            || matches(
                ManiaShortcutAction.MenuNextAlternate,
                key))
        {
            selectAction((selectedAction + 1) % actions.Length);
            return true;
        }

        if (matches(ManiaShortcutAction.Confirm, key)
            || matches(ManiaShortcutAction.ConfirmAlternate, key))
        {
            actions[selectedAction].Trigger();
            return true;
        }

        if (matches(ManiaShortcutAction.Retry, key))
            retry();

        return true;
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
                    Width = 470,
                    Colour = HomeControlColours.Ivory,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 220,
                    X = 420,
                    Y = -40,
                    Height = 1.2f,
                    Rotation = 7.5f,
                    Colour = HomeControlColours.Ivory,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    X = 590,
                    Y = -18,
                    Width = 2,
                    Height = 1.1f,
                    Rotation = 7.5f,
                    Colour = HomeControlColours.Navy,
                },
            },
        };

    private Drawable createHeader(Texture logoTexture) =>
        new Container
        {
            Position = new Vector2(68, 40),
            Size = new Vector2(430, 310),
            Children = new Drawable[]
            {
                new SpriteIcon
                {
                    Position = new Vector2(0, 4),
                    Size = new Vector2(12),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = HomeControlColours.Pink,
                },
                new SpriteIcon
                {
                    Position = new Vector2(43, 4),
                    Size = new Vector2(10),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = HomeControlColours.Cyan,
                },
                new Sprite
                {
                    Position = new Vector2(0, 28),
                    Size = new Vector2(236, 74),
                    FillMode = FillMode.Fit,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Texture = logoTexture,
                },
                new SpriteText
                {
                    Position = new Vector2(0, 146),
                    Text = YokkoStrings.Get("gameplay.pause.title"),
                    Font = HomeTypography.Hero(54),
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(2, 212),
                    Text = YokkoStrings.Get("gameplay.pause.subtitle"),
                    Font = HomeTypography.Display(32),
                    Colour = HomeControlColours.Navy,
                },
                new Box
                {
                    Position = new Vector2(2, 252),
                    Size = new Vector2(190, 5),
                    Colour = HomeControlColours.Yellow,
                },
                new SpriteText
                {
                    Position = new Vector2(2, 278),
                    Text = "P A U S E D",
                    Font = HomeTypography.Display(16),
                    Colour = HomeControlColours.Cyan,
                },
                new HomeMicroLine
                {
                    Position = new Vector2(272, 286),
                    Width = 108,
                    Colour = HomeControlColours.Cyan,
                },
            },
        };

    private Drawable createActionColumn()
    {
        actions[0] = new PauseActionButton(
            YokkoStrings.Get("gameplay.pause.resume"),
            $"{formatKey(ManiaShortcutAction.PauseOrBack)} TO RESUME",
            FontAwesome.Solid.Play,
            true,
            HomeControlColours.Pink,
            resume,
            () => selectAction(0))
        {
            Position = new Vector2(68, 360),
        };
        actions[1] = new PauseActionButton(
            YokkoStrings.Get("gameplay.pause.retry"),
            string.Empty,
            FontAwesome.Solid.Redo,
            false,
            HomeControlColours.Pink,
            retry,
            () => selectAction(1))
        {
            Position = new Vector2(68, 490),
        };
        actions[2] = new PauseActionButton(
            YokkoStrings.Get("gameplay.pause.settings"),
            string.Empty,
            FontAwesome.Solid.Cog,
            false,
            HomeControlColours.Cyan,
            openSettings,
            () => selectAction(2))
        {
            Position = new Vector2(221.3f, 490),
        };
        actions[3] = new PauseActionButton(
            YokkoStrings.Get("gameplay.pause.exit"),
            string.Empty,
            FontAwesome.Solid.SignOutAlt,
            false,
            HomeControlColours.Pink,
            exitGameplay,
            () => selectAction(3))
        {
            Position = new Vector2(374.6f, 490),
        };

        return new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(68, 496),
                    Size = new Vector2(460, 68),
                    Masking = true,
                    CornerRadius = 10,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(
                            HomeControlColours.Navy.R,
                            HomeControlColours.Navy.G,
                            HomeControlColours.Navy.B,
                            0.22f),
                    },
                },
                new Container
                {
                    Position = new Vector2(68, 490),
                    Size = new Vector2(460, 68),
                    Masking = true,
                    CornerRadius = 10,
                    BorderThickness = 2,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = HomeControlColours.Ivory,
                        },
                        new Box
                        {
                            Position = new Vector2(153.3f, 14),
                            Size = new Vector2(1.5f, 40),
                            Colour = new Color4(
                                HomeControlColours.Navy.R,
                                HomeControlColours.Navy.G,
                                HomeControlColours.Navy.B,
                                0.55f),
                        },
                        new Box
                        {
                            Position = new Vector2(306.6f, 14),
                            Size = new Vector2(1.5f, 40),
                            Colour = new Color4(
                                HomeControlColours.Navy.R,
                                HomeControlColours.Navy.G,
                                HomeControlColours.Navy.B,
                                0.55f),
                        },
                    },
                },
                actions[0],
                actions[1],
                actions[2],
                actions[3],
            },
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
            Position = new Vector2(-28, 22),
            Size = new Vector2(350, 76),
            Children = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(2, 6),
                    Size = new Vector2(348, 70),
                    Masking = true,
                    CornerRadius = 14,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(
                            HomeControlColours.Navy.R,
                            HomeControlColours.Navy.G,
                            HomeControlColours.Navy.B,
                            0.22f),
                    },
                },
                new Container
                {
                    Size = new Vector2(350, 70),
                    Masking = true,
                    CornerRadius = 14,
                    BorderThickness = 2,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = HomeControlColours.Ivory,
                        },
                    },
                },
                new Container
                {
                    Position = new Vector2(14, 11),
                    Size = new Vector2(48),
                    Masking = true,
                    CornerRadius = 9,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(
                                HomeControlColours.Cyan.R,
                                HomeControlColours.Cyan.G,
                                HomeControlColours.Cyan.B,
                                0.18f),
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
                    Position = new Vector2(78, 12),
                    Text = beatmap.Title,
                    Font = HomeTypography.Display(18),
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(78, 41),
                    Text = difficulty,
                    Font = HomeTypography.Body(12),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.78f),
                },
                new Container
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -22,
                    Size = new Vector2(55, 8),
                    Children = new Drawable[]
                    {
                        new Circle
                        {
                            Position = new Vector2(0, 1),
                            Size = new Vector2(6),
                            Colour = HomeControlColours.Cyan,
                        },
                        new Circle
                        {
                            Position = new Vector2(13, 1),
                            Size = new Vector2(6),
                            Colour = HomeControlColours.Cyan,
                        },
                        new Circle
                        {
                            Position = new Vector2(26, 1),
                            Size = new Vector2(6),
                            Colour = HomeControlColours.Cyan,
                        },
                        new Circle
                        {
                            Position = new Vector2(39, 1),
                            Size = new Vector2(6),
                            Colour = HomeControlColours.Pink,
                        },
                    },
                },
            },
        };
    }

    private Drawable createMascot(Texture mascotTexture) =>
        new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new Sprite
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Position = new Vector2(210, -2),
                    Size = new Vector2(700, 800),
                    Texture = mascotTexture,
                },
                new HomeMascotBubble(
                    YokkoStrings.Get("gameplay.pause.bubble"))
                {
                    Position = new Vector2(956, 334),
                },
            },
        };

    private Drawable createDecorations() =>
        new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new Box
                {
                    Position = new Vector2(635, 34),
                    Size = new Vector2(2, 560),
                    Colour = new Color4(1f, 1f, 1f, 0.22f),
                },
                new Box
                {
                    Position = new Vector2(730, 34),
                    Size = new Vector2(2, 560),
                    Colour = new Color4(1f, 1f, 1f, 0.22f),
                },
                new Box
                {
                    Position = new Vector2(825, 34),
                    Size = new Vector2(2, 560),
                    Colour = new Color4(1f, 1f, 1f, 0.22f),
                },
                new Box
                {
                    Position = new Vector2(920, 34),
                    Size = new Vector2(2, 560),
                    Colour = new Color4(1f, 1f, 1f, 0.22f),
                },
                new Box
                {
                    Position = new Vector2(610, 558),
                    Size = new Vector2(392, 2),
                    Colour = new Color4(1f, 1f, 1f, 0.34f),
                },
                new HomeRing(72, 3, new Color4(1f, 1f, 1f, 0.55f))
                {
                    Position = new Vector2(658, 558),
                },
                new HomeRing(72, 3, new Color4(1f, 1f, 1f, 0.55f))
                {
                    Position = new Vector2(753, 558),
                },
                new HomeRing(72, 3, new Color4(1f, 1f, 1f, 0.55f))
                {
                    Position = new Vector2(848, 558),
                },
                new HomeRing(72, 3, new Color4(1f, 1f, 1f, 0.55f))
                {
                    Position = new Vector2(943, 558),
                },
                new HomeDotField
                {
                    Position = new Vector2(366, 625),
                    Size = new Vector2(68, 42),
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.36f),
                },
                new HomeBarcode("NO.004-KEY")
                {
                    Position = new Vector2(68, 628),
                },
                new Container
                {
                    Position = new Vector2(582, 266),
                    Size = new Vector2(14),
                    Rotation = 45,
                    Masking = true,
                    BorderThickness = 2,
                    BorderColour = HomeControlColours.Navy,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = HomeControlColours.Yellow,
                    },
                },
            },
        };

    private void selectAction(int index)
    {
        selectedAction = index;

        for (int i = 0; i < actions.Length; i++)
            actions[i]?.SetSelected(i == selectedAction);
    }

    private bool matches(ManiaShortcutAction action, Key key) =>
        gameplaySettings.GetShortcutBinding(action) == key;

    private string formatKey(ManiaShortcutAction action) =>
        KeyModeBindings.FormatKey(
            gameplaySettings.GetShortcutBinding(action)).ToUpperInvariant();

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
            Color4 accentColour,
            Action action,
            Action hoverAction)
        {
            this.primary = primary;
            this.hoverAction = hoverAction;
            Action = action;
            Size = primary
                ? new Vector2(460, 112)
                : new Vector2(153.4f, 68);

            float iconSize = primary ? 72 : 42;
            float iconInset = primary ? 20 : 10;
            float textX = primary ? 118 : 58;

            InternalChildren = new Drawable[]
            {
                primary
                    ? new Container
                    {
                        Position = new Vector2(0, 7),
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 14,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(
                                HomeControlColours.Navy.R,
                                HomeControlColours.Navy.G,
                                HomeControlColours.Navy.B,
                                0.24f),
                        },
                    }
                    : new Container
                    {
                        Alpha = 0,
                    },
                primary
                    ? new Container
                    {
                        Position = new Vector2(-3, -3),
                        Size = new Vector2(466, 112),
                        Masking = true,
                        CornerRadius = 16,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = HomeControlColours.Cyan,
                        },
                    }
                    : new Container
                    {
                        Alpha = 0,
                    },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = primary ? 14 : 0,
                    BorderThickness = primary ? 2 : 0,
                    BorderColour = primary
                        ? HomeControlColours.Navy
                        : Color4.Transparent,
                    Children = new Drawable[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = primary
                                ? HomeControlColours.Navy
                                : Color4.Transparent,
                        },
                        primary
                            ? new HomeDotField
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                X = -82,
                                Size = new Vector2(82, 52),
                                Colour = new Color4(
                                    HomeControlColours.Cyan.R,
                                    HomeControlColours.Cyan.G,
                                    HomeControlColours.Cyan.B,
                                    0.2f),
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
                            CornerRadius = primary ? 10 : 8,
                            BorderThickness = primary ? 2 : 0,
                            BorderColour = primary
                                ? Color4.White
                                : Color4.Transparent,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = primary
                                        ? Color4.White
                                        : new Color4(
                                            HomeControlColours.PaleCyan.R,
                                            HomeControlColours.PaleCyan.G,
                                            HomeControlColours.PaleCyan.B,
                                            0.8f),
                                },
                                new SpriteIcon
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Size = primary
                                        ? new Vector2(30)
                                        : new Vector2(21),
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
                                ? HomeTypography.Display(32)
                                : HomeTypography.Display(14),
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
                                ? new Vector2(24)
                                : Vector2.Zero,
                            Icon = FontAwesome.Solid.ChevronRight,
                            Colour = HomeControlColours.Yellow,
                            Alpha = primary ? 1 : 0,
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
                    Colour = accentColour,
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
                        : Color4.Transparent,
                100,
                Easing.OutQuint);
            accent.ResizeWidthTo(
                selected
                    ? primary ? 250 : 62
                    : primary ? 110 : 42,
                130,
                Easing.OutQuint);
            if (primary)
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
