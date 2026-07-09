using System.Linq;
using osu.Framework.Audio;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osuTK;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Core.Scoring;
using Yokko.Game.Audio;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Gameplay;
using Yokko.Import;

namespace Yokko.Game.Screens.Main;

public partial class MainScreen : Screen
{
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
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 18),
                Padding = new MarginPadding { Horizontal = 72, Vertical = 36 },
                Children = new Drawable[]
                {
                    new MainHeader(),
                    createStatusRow(windows),
                    createCapabilityRow(audioEngine),
                    createLaunchRow(),
                    new MainFooter(),
                }
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
            new StatusPanel("Modes", "4K / 7K", "keyboard-first playfield", YokkoPalette.Cyan),
            new StatusPanel("Judgement", $"P {windows.PerfectMilliseconds:0}ms", "audio-clock accuracy path", YokkoPalette.Rose),
            new StatusPanel("Renderer", "1000 fps target", "batched 2D gameplay layer", YokkoPalette.Lime),
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
            new SectionPanel("Import Targets", KnownChartImporters.Capabilities.Select(formatDisplayName).ToArray()),
            new SectionPanel("Low Latency Audio", audioEngine.Backends.Select(backendDisplayName).ToArray()),
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
            new StartPanel("Start 4K Demo", "D F J K / timing skeleton", () => this.Push(new GameplayScreen(DemoBeatmaps.CreateFourKeyDemo()))),
            new StartPanel("Start 7K Demo", "S D F Space J K L", () => this.Push(new GameplayScreen(DemoBeatmaps.CreateSevenKeyDemo()))),
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
