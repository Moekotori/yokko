using System.Linq;
using osu.Framework.Audio;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Screens;
using osuTK;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Core.Scoring;
using Yokko.Game.Audio;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Editor;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Settings;
using Yokko.Import;

namespace Yokko.Game.Screens.Main;

public partial class MainScreen : Screen
{
    private const float designedWidth = 1120;
    private const float designedHeight = 660;

    [Resolved]
    private AudioManager audioManager { get; set; }

    [BackgroundDependencyLoader]
    private void load()
    {
        JudgementWindows windows = JudgementWindows.DefaultMania;
        var audioEngine = new OsuFrameworkAudioEngine(audioManager);

        InternalChildren = new Drawable[]
        {
            new Box
            {
                Colour = YokkoPalette.Background,
                RelativeSizeAxes = Axes.Both,
            },
            new MainBackdrop(),
            new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(designedWidth, designedHeight),
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 18),
                    Padding = new MarginPadding { Horizontal = 24, Vertical = 18 },
                    Children = new Drawable[]
                    {
                        new MainHeader(() => this.Push(new SettingsScreen())),
                        createLaunchRow(),
                        createStatusRow(windows),
                        createCapabilityRow(audioEngine),
                        new MainFooter(),
                    }
                },
            },
        };
    }

    private static Drawable createStatusRow(JudgementWindows windows) => new FillFlowContainer
    {
        RelativeSizeAxes = Axes.X,
        AutoSizeAxes = Axes.Y,
        Direction = FillDirection.Horizontal,
        Spacing = new Vector2(18, 0),
        Children = new Drawable[]
        {
            new StatusPanel("CREATE", "4K / 7K editor", "Build notes on a clear timeline", YokkoPalette.Lime),
            new StatusPanel("PLAY", "DFJK / SDF JKL", "Familiar keys, instant feedback", YokkoPalette.Cyan),
            new StatusPanel("IMPROVE", $"Perfect ±{windows.PerfectMilliseconds:0}ms", "Timing you can understand", YokkoPalette.Rose),
        },
    };

    private static Drawable createCapabilityRow(IAudioEngine audioEngine) => new FillFlowContainer
    {
        RelativeSizeAxes = Axes.X,
        AutoSizeAxes = Axes.Y,
        Direction = FillDirection.Horizontal,
        Spacing = new Vector2(18, 0),
        Children = new Drawable[]
        {
            new SectionPanel("A simple charting flow", new[]
            {
                "New 4K",
                "New 7K",
                "Import .osu",
                "Export .osu",
                "Playtest",
            }),
            new SectionPanel("Works with your setup", KnownChartImporters.Capabilities.Select(formatDisplayName)
                                                  .Concat(audioEngine.Backends.Select(backendDisplayName))
                                                  .ToArray()),
        },
    };

    private Drawable createLaunchRow() => new FillFlowContainer
    {
        RelativeSizeAxes = Axes.X,
        AutoSizeAxes = Axes.Y,
        Direction = FillDirection.Horizontal,
        Spacing = new Vector2(18, 0),
        Children = new Drawable[]
        {
            new StartPanel("START HERE", "Open the editor", "Create from scratch or bring in an osu!mania chart.", FontAwesome.Solid.Pen,
                () => this.Push(new EditorScreen()), YokkoPalette.Lime, 480, true),
            new StartPanel("QUICK PLAY", "4K demo", "Use D F J K", FontAwesome.Solid.Keyboard,
                () => this.Push(new GameplayScreen(DemoBeatmaps.CreateFourKeyDemo())), YokkoPalette.Cyan, 279),
            new StartPanel("QUICK PLAY", "7K demo", "Use S D F Space J K L", FontAwesome.Solid.Music,
                () => this.Push(new GameplayScreen(DemoBeatmaps.CreateSevenKeyDemo())), YokkoPalette.Rose, 279),
        },
    };

    private static string formatDisplayName(ChartImportCapability capability) => capability.DisplayName switch
    {
        "LR2 library" => "LR2",
        _ => capability.DisplayName,
    };

    private static string backendDisplayName(AudioBackendCapabilities backend) => backend.Kind switch
    {
        AudioBackendKind.SharedWasapi => "WASAPI Shared",
        AudioBackendKind.WasapiExclusive => "WASAPI Excl.",
        AudioBackendKind.Asio => "ASIO",
        _ => backend.Kind.ToString(),
    };
}
