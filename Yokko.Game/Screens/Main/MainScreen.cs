using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Game.Audio;
using Yokko.Game.Screens.Editor;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Screens.Main;

public partial class MainScreen : Screen
{
    private const float designedWidth = 1280;
    private const float designedHeight = 720;

    private static readonly Color4 ivory = new(0.992f, 0.992f, 0.988f, 1f);
    private static readonly Color4 cyan = new(0.29f, 0.84f, 0.96f, 1f);
    private static readonly Color4 navy = new(0.035f, 0.085f, 0.54f, 1f);
    private static readonly Color4 yellow = new(1f, 0.91f, 0.42f, 1f);
    private static readonly Color4 pink = new(1f, 0.22f, 0.65f, 1f);
    private static readonly Color4 mutedNavy = new(0.18f, 0.28f, 0.58f, 1f);

    [Resolved]
    private AudioManager audioManager { get; set; }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        var audioEngine = new OsuFrameworkAudioEngine(audioManager);
        string audioStatus = audioEngine.Backends
                                        .Where(backend => backend.IsAvailable)
                                        .Select(backendDisplayName)
                                        .FirstOrDefault() ?? "Audio unavailable";

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
                    createDecorationIcon(FontAwesome.Solid.Plus, 54, 28, 10, pink),
                    createDecorationIcon(FontAwesome.Solid.Plus, 116, 28, 10, cyan),
                    new HomeDotCross
                    {
                        Position = new Vector2(-12, 610),
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
                Width = 520,
                Colour = ivory,
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 218,
                X = 410,
                Y = -36,
                Height = 1.14f,
                Rotation = -6,
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
                X = 684,
                Y = 18,
                Text = "YOKKO",
                Font = HomeTypography.Brand(108),
                Colour = new Color4(1f, 1f, 1f, 0.14f),
            },
            createDecorationIcon(FontAwesome.Solid.Plus, 680, 176, 18, Color4.White),
            createDecorationIcon(FontAwesome.Solid.Plus, 1214, 543, 19, Color4.White),
            createDecorationIcon(FontAwesome.Solid.Plus, 1176, 154, 14, yellow),
            createStageLine(1126, 238, 180, -28),
            createStageLine(1070, 530, 250, -28),
            new Sprite
            {
                X = 620,
                Y = 50,
                Size = new Vector2(650, 737),
                Texture = mascotTexture,
            },
            new HomeMascotBubble("Let's chart!")
            {
                X = 570,
                Y = 408,
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
        Position = new Vector2(48, 42),
        Size = new Vector2(450, 153),
        Texture = logoTexture,
    };

    private Drawable createCommandArea() => new Container
    {
        Position = new Vector2(58, 195),
        Size = new Vector2(520, 385),
        Children = new Drawable[]
        {
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, -8),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = "Ready for",
                        Font = HomeTypography.Hero(76),
                        Spacing = new Vector2(0.6f, 0),
                        Colour = navy,
                    },
                    new SpriteText
                    {
                        Text = "a check-up?",
                        Font = HomeTypography.Hero(76),
                        Spacing = new Vector2(0.6f, 0),
                        Colour = navy,
                    },
                },
            },
            new HomeDotCross
            {
                Position = new Vector2(410, 12),
            },
            createDecorationIcon(FontAwesome.Solid.Plus, 438, 58, 15, pink),
            new HomePrimaryAction(
                "Open the editor",
                "Untitled chart  ·  4K",
                FontAwesome.Solid.Plus,
                () => this.Push(new EditorScreen()))
            {
                Y = 150,
            },
            new FillFlowContainer
            {
                Y = 295,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(10, 0),
                Children = new Drawable[]
                {
                    new HomeDemoAction("Play 4K demo", "D F J K",
                        () => this.Push(new GameplayScreen(DemoBeatmaps.CreateFourKeyDemo()))),
                    new HomeDemoAction("Play 7K demo", "S D F  SPACE  J K L",
                        () => this.Push(new GameplayScreen(DemoBeatmaps.CreateSevenKeyDemo()))),
                },
            },
            new HomeConnectorPlus
            {
                Position = new Vector2(250, 323),
            },
        },
    };

    private Drawable createUtilityArea() => new FillFlowContainer
    {
        Position = new Vector2(1002, 30),
        AutoSizeAxes = Axes.Both,
        Direction = FillDirection.Horizontal,
        Spacing = new Vector2(10, 0),
        Children = new Drawable[]
        {
            new HomeUtilityButton("Import .osu", FontAwesome.Solid.FolderOpen,
                () => this.Push(new EditorScreen(true)), 154),
            new HomeUtilityButton(string.Empty, FontAwesome.Solid.Cog,
                () => this.Push(new SettingsScreen()), 50),
        },
    };

    private static Drawable createFooter(string audioStatus) => new Container
    {
        Position = new Vector2(58, 648),
        Size = new Vector2(520, 48),
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = new Color4(navy.R, navy.G, navy.B, 0.24f),
            },
            new SpriteIcon
            {
                Position = new Vector2(310, -10),
                Size = new Vector2(23),
                Icon = FontAwesome.Solid.Heartbeat,
                Colour = navy,
            },
            new FillFlowContainer
            {
                Y = 17,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(28, 0),
                Children = new Drawable[]
                {
                    createFooterItem(FontAwesome.Solid.Keyboard, "D  F  J  K"),
                    createFooterItem(FontAwesome.Solid.SignOutAlt, "Esc back"),
                    createFooterItem(FontAwesome.Solid.VolumeUp, audioStatus),
                },
            },
        },
    };

    private static Drawable createFooterItem(IconUsage icon, string text) => new FillFlowContainer
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
