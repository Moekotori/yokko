using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osuTK;
using Yokko.Audio;
using Yokko.Core.Scoring;
using Yokko.Game.Presentation;
using Yokko.Import;

namespace Yokko.Game.Screens.Main;

public partial class MainScreen : Screen
{
    [BackgroundDependencyLoader]
    private void load()
    {
        JudgementWindows windows = JudgementWindows.DefaultMania;
        var audioEngine = new NullAudioEngine();

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
                Spacing = new Vector2(0, 30),
                Padding = new MarginPadding { Horizontal = 72, Vertical = 52 },
                Children = new Drawable[]
                {
                    new MainHeader(),
                    createStatusRow(windows),
                    createCapabilityRow(audioEngine),
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
            new SectionPanel("Import Targets", KnownChartImporters.Capabilities.Select(capability => capability.DisplayName).ToArray()),
            new SectionPanel("Low Latency Audio", audioEngine.Backends.Select(backend => backend.Kind.ToString()).ToArray()),
        },
    };
}
