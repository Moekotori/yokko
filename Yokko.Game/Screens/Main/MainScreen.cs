using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Editor;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Screens.Main;

public partial class MainScreen : Screen
{
    private const float designedWidth = 1280;
    private const float designedHeight = 720;

    private static readonly Color4 ivory = new(0.992f, 0.992f, 0.988f, 1f);
    private static readonly Color4 cyan = new(0.29f, 0.81f, 0.94f, 1f);
    private static readonly Color4 navy = new(0.035f, 0.085f, 0.54f, 1f);
    private static readonly Color4 yellow = new(1f, 0.91f, 0.42f, 1f);
    private static readonly Color4 pink = new(1f, 0.22f, 0.65f, 1f);
    private static readonly Color4 mutedNavy = new(0.18f, 0.28f, 0.58f, 1f);
    private static readonly Color4 paleCyan = new(0.54f, 0.91f, 0.98f, 1f);

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        string availableBackend = AudioEngineFactory.AvailableBackends
                                                    .Where(backend => backend.IsAvailable)
                                                    .Select(backendDisplayName)
                                                    .FirstOrDefault();
        LocalisableString audioStatus = availableBackend ?? YokkoStrings.Get("main.audio_unavailable");

        Texture mascotTexture = textures.Get("yokko")
                                        .Crop(new RectangleF(80, 1840, 1200, 1360));
        Texture logoTexture = textures.Get("home-logo");

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = cyan,
            },
            createIvoryStage(),
            new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(designedWidth, designedHeight),
                Children = new Drawable[]
                {
                    createRightStage(mascotTexture),
                    createDecorationIcon(FontAwesome.Solid.Plus, 55, 36, 12, pink),
                    createDecorationIcon(FontAwesome.Solid.Plus, 116, 29, 10, cyan),
                    createDecorationIcon(FontAwesome.Solid.Plus, 42, 67, 9, yellow),
                    new HomeCornerBracket
                    {
                        Position = new Vector2(30, 218),
                        Height = 232,
                    },
                    new HomeConnectorPlus
                    {
                        Position = new Vector2(28, 438),
                    },
                    new HomeMicroLine
                    {
                        Position = new Vector2(525, 31),
                        Width = 142,
                    },
                    new HomeDotCross
                    {
                        Position = new Vector2(-12, 606),
                        Alpha = 0.62f,
                    },
                    createBrandLockup(logoTexture),
                    createCommandArea(),
                    createUtilityArea(),
                    createFooter(audioStatus),
                },
            },
        };
    }

    private static Drawable createIvoryStage() => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 510,
                Colour = ivory,
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 220,
                X = 510,
                Y = -20,
                Height = 1.14f,
                Rotation = 11,
                Colour = ivory,
            },
        },
    };

    private Drawable createRightStage(Texture mascotTexture) => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Children = new Drawable[]
        {
            new SpriteText
            {
                X = 690,
                Y = 20,
                Text = "YOKKO",
                Font = HomeTypography.Brand(102),
                Colour = new Color4(1f, 1f, 1f, 0.14f),
            },
            createDecorationIcon(FontAwesome.Solid.Plus, 730, 166, 18, Color4.White),
            createDecorationIcon(FontAwesome.Solid.Plus, 1220, 552, 19, Color4.White),
            createDecorationIcon(FontAwesome.Solid.Plus, 1190, 151, 14, yellow),
            createDecorationIcon(FontAwesome.Solid.Plus, 1225, 666, 10, pink),
            createStageLine(1128, 235, 180, -28),
            createStageLine(1060, 532, 250, -28),
            new HomeDotField
            {
                Position = new Vector2(1186, 16),
                Size = new Vector2(72, 44),
                Colour = new Color4(1f, 1f, 1f, 0.24f),
            },
            new HomeDotField
            {
                Position = new Vector2(1184, 610),
                Size = new Vector2(72, 58),
                Colour = new Color4(1f, 1f, 1f, 0.22f),
            },
            new Sprite
            {
                X = 600,
                Y = 14,
                Size = new Vector2(675, 765),
                Texture = mascotTexture,
            },
            new HomeMascotBubble(YokkoStrings.Get("main.lets_play"))
            {
                X = 632,
                Y = 365,
            },
        },
    };

    private static Drawable createStageLine(float x, float y, float width, float rotation) => new Box
    {
        Position = new Vector2(x, y),
        Width = width,
        Height = 2,
        Rotation = rotation,
        Colour = new Color4(1f, 1f, 1f, 0.22f),
    };

    private static Drawable createDecorationIcon(IconUsage icon, float x, float y, float size, Color4 colour) => new SpriteIcon
    {
        Position = new Vector2(x, y),
        Size = new Vector2(size),
        Icon = icon,
        Colour = colour,
        Alpha = 0.9f,
    };

    private static Drawable createBrandLockup(Texture logoTexture) => new Sprite
    {
        Position = new Vector2(56, 46),
        Size = new Vector2(500, 169),
        Texture = logoTexture,
    };

    private Drawable createCommandArea() => new Container
    {
        Position = new Vector2(72, 208),
        Size = new Vector2(520, 394),
        Children = new Drawable[]
        {
            new FillFlowContainer
            {
                X = 34,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, -11),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("main.hero_line_1"),
                        Font = HomeTypography.Hero(72),
                        Scale = new Vector2(1.08f, 1),
                        Colour = navy,
                    },
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("main.hero_line_2"),
                        Font = HomeTypography.Hero(72),
                        Scale = new Vector2(1.08f, 1),
                        Colour = navy,
                    },
                },
            },
            new HomeDotCross
            {
                Position = new Vector2(480, 18),
            },
            createDecorationIcon(FontAwesome.Solid.Plus, 508, 67, 15, pink),
            new HomePrimaryAction(
                YokkoStrings.Get("main.play"),
                YokkoStrings.Get("main.song_select"),
                FontAwesome.Solid.Play,
                () => this.Push(new GameplayScreen(DemoBeatmaps.CreateFourKeyDemo())))
            {
                Y = 162,
            },
            new HomeConnectorPlus
            {
                Position = new Vector2(536, 209),
            },
            new FillFlowContainer
            {
                Y = 302,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(16, 0),
                Children = new Drawable[]
                {
                    new HomeSecondaryAction(YokkoStrings.Get("main.editor"), FontAwesome.Solid.Edit,
                        () => this.Push(new EditorScreen())),
                    new HomeSecondaryAction(YokkoStrings.Get("main.settings"), FontAwesome.Solid.Cog,
                        () => this.Push(new SettingsScreen())),
                },
            },
        },
    };

    private Drawable createUtilityArea() => new Container
    {
        Position = new Vector2(1176, 24),
        Size = new Vector2(72),
        Child = new HomeUtilityButton(string.Empty, FontAwesome.Solid.FolderOpen,
            () => this.Push(new EditorScreen(true)), 72),
    };

    private static Drawable createFooter(LocalisableString audioStatus) => new Container
    {
        Position = new Vector2(60, 655),
        Size = new Vector2(530, 48),
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = new Color4(navy.R, navy.G, navy.B, 0.24f),
            },
            new Box
            {
                Width = 165,
                Height = 1,
                X = 116,
                Colour = new Color4(paleCyan.R, paleCyan.G, paleCyan.B, 0.72f),
            },
            new SpriteIcon
            {
                Position = new Vector2(260, -10),
                Size = new Vector2(23),
                Icon = FontAwesome.Solid.Heartbeat,
                Colour = navy,
            },
            new FillFlowContainer
            {
                Y = 17,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(25, 0),
                Children = new Drawable[]
                {
                    createFooterItem(FontAwesome.Solid.Keyboard, "D  F  J  K"),
                    createFooterItem(FontAwesome.Solid.SignOutAlt, YokkoStrings.Get("common.esc_back")),
                    createFooterItem(FontAwesome.Solid.VolumeUp, audioStatus),
                },
            },
        },
    };

    private static Drawable createFooterItem(IconUsage icon, LocalisableString text) => new FillFlowContainer
    {
        AutoSizeAxes = Axes.Both,
        Direction = FillDirection.Horizontal,
        Spacing = new Vector2(8, 0),
        Children = new Drawable[]
        {
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Size = new Vector2(14),
                Icon = icon,
                Colour = navy,
            },
            new SpriteText
            {
                Text = text,
                Font = HomeTypography.Display(14),
                Colour = mutedNavy,
            },
        },
    };

    private static string backendDisplayName(AudioBackendCapabilities backend) => backend.Kind switch
    {
        AudioBackendKind.SharedWasapi => "WASAPI Shared",
        AudioBackendKind.WasapiExclusive => "WASAPI Exclusive",
        AudioBackendKind.Asio => "ASIO",
        _ => backend.Kind.ToString(),
    };
}
