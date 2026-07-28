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

    private static readonly Color4 ivory = new(0.986f, 0.982f, 0.956f, 1f);
    private static readonly Color4 cyan = new(0.29f, 0.84f, 0.96f, 1f);
    private static readonly Color4 cyanSoft = new(0.69f, 0.94f, 0.98f, 1f);
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
                    createBrandLockup(),
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
                Width = 585,
                Colour = ivory,
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 185,
                X = 525,
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
                Font = FontUsage.Default.With(size: 108, weight: "Bold"),
                Colour = new Color4(1f, 1f, 1f, 0.14f),
            },
            createDecorationIcon(FontAwesome.Solid.Plus, 680, 176, 18, Color4.White),
            createDecorationIcon(FontAwesome.Solid.Plus, 1214, 543, 19, Color4.White),
            createDecorationIcon(FontAwesome.Solid.Plus, 1176, 154, 14, yellow),
            new Sprite
            {
                X = 708,
                Y = 118,
                Size = new Vector2(500, 567),
                Texture = mascotTexture,
            },
            new HomeMascotBubble("Let's\nchart!")
            {
                X = 600,
                Y = 395,
            },
        },
    };

    private static Drawable createDecorationIcon(IconUsage icon, float x, float y, float size, Color4 colour) => new SpriteIcon
    {
        Position = new Vector2(x, y),
        Size = new Vector2(size),
        Icon = icon,
        Colour = colour,
        Alpha = 0.9f,
    };

    private static Drawable createBrandLockup() => new Container
    {
        Position = new Vector2(58, 63),
        Size = new Vector2(440, 132),
        Children = new Drawable[]
        {
            new SpriteText
            {
                Text = "YOKKO",
                Font = FontUsage.Default.With(size: 70, weight: "Bold"),
                Colour = navy,
            },
            new FillFlowContainer
            {
                Y = 80,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Children = "RHYTHM CHART STUDIO".Select(character => new SpriteText
                {
                    Text = character.ToString(),
                    Font = FontUsage.Default.With(size: 12, weight: "Bold"),
                    Colour = navy,
                }).Cast<Drawable>().ToArray(),
            },
            new Box
            {
                Y = 112,
                Width = 352,
                Height = 2,
                Colour = navy,
            },
            new SpriteIcon
            {
                Position = new Vector2(357, 98),
                Size = new Vector2(28),
                Icon = FontAwesome.Solid.Heartbeat,
                Colour = navy,
            },
        },
    };

    private Drawable createCommandArea() => new Container
    {
        Position = new Vector2(58, 218),
        Size = new Vector2(520, 385),
        Children = new Drawable[]
        {
            new SpriteText
            {
                Text = "Ready for\na check-up?",
                Font = FontUsage.Default.With(size: 49, weight: "Bold"),
                Colour = navy,
            },
            createDecorationIcon(FontAwesome.Solid.Plus, 438, 58, 15, pink),
            new HomePrimaryAction(
                "Open the editor",
                "Untitled chart  ·  4K",
                FontAwesome.Solid.Plus,
                () => this.Push(new EditorScreen()))
            {
                Y = 143,
            },
            new FillFlowContainer
            {
                Y = 278,
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
            new Box
            {
                X = 312,
                Width = 38,
                Height = 3,
                Colour = pink,
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
                Font = FontUsage.Default.With(size: 13, weight: "SemiBold"),
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
