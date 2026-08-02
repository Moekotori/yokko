using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Diagnostics;

/// <summary>
/// Incremental live view over <see cref="YokkoDiagnostics"/>. It never reads
/// log files and never receives logger callbacks on the draw/update threads.
/// </summary>
internal partial class YokkoDebugConsoleOverlay : CompositeDrawable
{
    private const int maximum_visible_lines = 700;

    private readonly YokkoDiagnostics diagnostics;
    private readonly List<Drawable> renderedLines = new();
    private readonly FillFlowContainer lineFlow;
    private readonly BasicScrollContainer scroll;
    private readonly SpriteText statusText;
    private readonly SpriteText performanceText;
    private readonly SpriteText pauseButtonText;
    private readonly SpriteIcon pauseButtonIcon;
    private GameHost host;
    private Clipboard clipboard;
    private long lastSequence;
    private bool paused;
    private bool exportInProgress;
    private LocalisableString operationStatus;
    private bool hasOperationStatus;
    private double operationStatusUntil;

    public override bool HandlePositionalInput => true;
    internal bool IsPaused => paused;
    internal int RenderedLineCount => renderedLines.Count;
    internal bool ContainsRenderedText(string text) =>
        renderedLines.OfType<SpriteText>().Any(line =>
            line.Text.ToString().Contains(
                text,
                StringComparison.Ordinal));

    public YokkoDebugConsoleOverlay(YokkoDiagnostics diagnostics)
    {
        this.diagnostics = diagnostics
                           ?? throw new ArgumentNullException(nameof(diagnostics));
        RelativeSizeAxes = Axes.Both;
        Depth = float.MinValue;
        Alpha = 0;

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                RelativeSizeAxes = Axes.X,
                Height = 455,
                Masking = true,
                CornerRadius = 12,
                BorderThickness = 2,
                BorderColour = HomeControlColours.Cyan,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(0.015f, 0.025f, 0.09f, 0.97f),
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 54,
                        Colour = new Color4(0.025f, 0.065f, 0.16f, 1),
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.CentreLeft,
                        Position = new Vector2(20, 27),
                        Size = new Vector2(22),
                        Icon = FontAwesome.Solid.Terminal,
                        Colour = HomeControlColours.Cyan,
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.CentreLeft,
                        Position = new Vector2(54, 27),
                        Text = YokkoStrings.Get("debug_console.title"),
                        Font = HomeTypography.Display(20),
                        Colour = Color4.White,
                    },
                    statusText = new SpriteText
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.CentreLeft,
                        Position = new Vector2(290, 27),
                        Width = 176,
                        Truncate = true,
                        Font = HomeTypography.Body(14),
                        Colour = new Color4(0.65f, 0.84f, 0.92f, 1),
                    },
                    createButton(
                        470,
                        94,
                        FontAwesome.Solid.Pause,
                        YokkoStrings.Get("debug_console.pause"),
                        togglePause,
                        out pauseButtonIcon,
                        out pauseButtonText),
                    createButton(
                        570,
                        82,
                        FontAwesome.Solid.Trash,
                        YokkoStrings.Get("debug_console.clear"),
                        clear),
                    createButton(
                        658,
                        82,
                        FontAwesome.Solid.Copy,
                        YokkoStrings.Get("debug_console.copy"),
                        copy),
                    createButton(
                        746,
                        100,
                        FontAwesome.Solid.FileExport,
                        YokkoStrings.Get("debug_console.export"),
                        export),
                    createButton(
                        852,
                        108,
                        FontAwesome.Solid.FolderOpen,
                        YokkoStrings.Get("debug_console.open_logs"),
                        openLogs),
                    createButton(
                        966,
                        82,
                        FontAwesome.Solid.Times,
                        YokkoStrings.Get("debug_console.close"),
                        () => diagnostics.ConsoleVisible.Value = false),
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Position = new Vector2(0, 54),
                        Height = 38,
                        Colour = new Color4(0.018f, 0.04f, 0.1f, 1),
                    },
                    performanceText = new SpriteText
                    {
                        RelativeSizeAxes = Axes.X,
                        Position = new Vector2(18, 65),
                        Width = -36,
                        Truncate = true,
                        Text = "PERFORMANCE · waiting for samples",
                        Font = new FontUsage("NotoSansCJK").With(size: 12, fixedWidth: true),
                        Colour = new Color4(0.58f, 0.86f, 0.93f, 1),
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding
                        {
                            Left = 14,
                            Right = 14,
                            Top = 102,
                            Bottom = 14,
                        },
                        Child = scroll = new BasicScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            ScrollbarVisible = true,
                            Child = lineFlow = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 2),
                                Padding = new MarginPadding { Right = 12 },
                            },
                        },
                    },
                },
            },
        };

        diagnostics.ConsoleVisible.BindValueChanged(onVisibilityChanged, true);
    }

    [BackgroundDependencyLoader]
    private void load(GameHost host, Clipboard clipboard)
    {
        this.host = host;
        this.clipboard = clipboard;
    }

    protected override void Update()
    {
        base.Update();

        if (!diagnostics.ConsoleVisible.Value)
        {
            refreshStatus();
            return;
        }

        refreshPerformance();

        if (paused)
        {
            refreshStatus();
            return;
        }

        append(diagnostics.GetEntriesAfter(lastSequence));
        refreshStatus();
    }

    private void append(IReadOnlyList<YokkoDiagnosticEntry> entries)
    {
        if (entries.Count == 0)
            return;

        foreach (YokkoDiagnosticEntry entry in entries)
        {
            lastSequence = Math.Max(lastSequence, entry.Sequence);
            var line = new SpriteText
            {
                RelativeSizeAxes = Axes.X,
                Height = 18,
                Text = entry.ToDisplayText(),
                Font = new FontUsage("NotoSansCJK").With(size: 13, fixedWidth: true),
                Colour = colourFor(entry.Level),
                Truncate = true,
            };
            renderedLines.Add(line);
            lineFlow.Add(line);
        }

        while (renderedLines.Count > maximum_visible_lines)
        {
            Drawable oldest = renderedLines[0];
            renderedLines.RemoveAt(0);
            lineFlow.Remove(oldest, true);
        }

        scroll.ScrollToEnd(false);
    }

    private void onVisibilityChanged(ValueChangedEvent<bool> change)
    {
        if (change.NewValue)
        {
            append(diagnostics.GetEntriesAfter(lastSequence));
            this.FadeIn(120, Easing.OutQuint);
        }
        else
        {
            this.FadeOut(100, Easing.OutQuint);
        }
    }

    private void togglePause()
    {
        paused = !paused;
        pauseButtonIcon.Icon = paused
            ? FontAwesome.Solid.Play
            : FontAwesome.Solid.Pause;
        pauseButtonText.Text = YokkoStrings.Get(
            paused
                ? "debug_console.resume"
                : "debug_console.pause");

        if (!paused)
            append(diagnostics.GetEntriesAfter(lastSequence));
        refreshStatus();
    }

    private void clear()
    {
        diagnostics.Clear();
        lastSequence = diagnostics.CurrentSequence;
        foreach (Drawable line in renderedLines)
            lineFlow.Remove(line, true);
        renderedLines.Clear();
        refreshStatus();
    }

    private void copy()
    {
        clipboard?.SetText(diagnostics.ExportText());
        diagnostics.Trace(
            "CONSOLE",
            "copied",
            $"entries={diagnostics.EntryCount}");
    }

    private void openLogs()
    {
        if (host == null || string.IsNullOrWhiteSpace(diagnostics.LogDirectory))
            return;

        host.OpenFileExternally(diagnostics.LogDirectory);
        diagnostics.Trace(
            "CONSOLE",
            "opened-log-directory",
            diagnostics.LogDirectory);
    }

    private void export()
    {
        if (exportInProgress)
            return;

        exportInProgress = true;
        operationStatus = YokkoStrings.Get(
            "debug_console.status_exporting");
        hasOperationStatus = true;
        operationStatusUntil = double.PositiveInfinity;
        refreshStatus();

        _ = Task.Run(diagnostics.ExportBundle).ContinueWith(task =>
        {
            Scheduler.Add(() =>
            {
                exportInProgress = false;
                operationStatusUntil = Time.Current + 5000;

                if (task.IsCompletedSuccessfully)
                {
                    string path = task.Result;
                    clipboard?.SetText(path);
                    operationStatus = YokkoStrings.Get(
                        "debug_console.status_exported");
                    string directory = Path.GetDirectoryName(path);
                    if (host != null && !string.IsNullOrWhiteSpace(directory))
                        host.OpenFileExternally(directory);
                    diagnostics.Trace(
                        "CONSOLE",
                        "diagnostics-exported",
                        path,
                        LogLevel.Important);
                }
                else
                {
                    string error = task.Exception?
                                       .GetBaseException().Message
                                   ?? "unknown error";
                    operationStatus = YokkoStrings.Get(
                        "debug_console.status_export_failed");
                    Logger.Log(
                        $"[CONSOLE] diagnostics export failed | {error}",
                        "diagnostics",
                        LogLevel.Error);
                }

                refreshStatus();
            });
        }, TaskScheduler.Default);
    }

    private void refreshPerformance()
    {
        if (!diagnostics.TryGetLatestPerformance(
                out YokkoPerformanceSnapshot snapshot))
            return;

        string value = snapshot.ToDisplayText();
        if (performanceText.Text.ToString() != value)
            performanceText.Text = value;

        performanceText.Colour = snapshot.Health switch
        {
            YokkoPerformanceHealth.Critical =>
                new Color4(1f, 0.42f, 0.45f, 1),
            YokkoPerformanceHealth.Warning => HomeControlColours.Yellow,
            _ => new Color4(0.58f, 0.86f, 0.93f, 1),
        };
    }

    private void refreshStatus()
    {
        if (hasOperationStatus && Time.Current <= operationStatusUntil)
        {
            statusText.Text = operationStatus;
            return;
        }

        hasOperationStatus = false;
        long pending = Math.Max(0, diagnostics.CurrentSequence - lastSequence);
        statusText.Text = paused
            ? YokkoStrings.Get("debug_console.status_paused", pending)
            : YokkoStrings.Get("debug_console.status_live", diagnostics.EntryCount);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key == Key.F12 && !e.Repeat)
        {
            diagnostics.Toggle();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            diagnostics.ConsoleVisible.ValueChanged -= onVisibilityChanged;
        base.Dispose(isDisposing);
    }

    private static Drawable createButton(
        float x,
        float width,
        IconUsage icon,
        LocalisableString label,
        Action action) =>
        createButton(x, width, icon, label, action, out _, out _);

    private static Drawable createButton(
        float x,
        float width,
        IconUsage icon,
        LocalisableString label,
        Action action,
        out SpriteIcon buttonIcon,
        out SpriteText buttonText)
    {
        var button = new ConsoleButton(icon, label, action, out buttonIcon, out buttonText)
        {
            Position = new Vector2(x, 9),
            Size = new Vector2(width, 36),
        };
        return button;
    }

    private static Color4 colourFor(LogLevel level) => level switch
    {
        LogLevel.Error => new Color4(1f, 0.4f, 0.43f, 1),
        LogLevel.Important => HomeControlColours.Yellow,
        LogLevel.Debug => new Color4(0.58f, 0.72f, 0.8f, 1),
        _ => new Color4(0.76f, 0.9f, 0.94f, 1),
    };

    private sealed partial class ConsoleButton : ClickableContainer
    {
        private readonly Box background;

        public ConsoleButton(
            IconUsage icon,
            LocalisableString label,
            Action action,
            out SpriteIcon buttonIcon,
            out SpriteText buttonText)
        {
            Action = action;
            Masking = true;
            CornerRadius = 5;
            BorderThickness = 1;
            BorderColour = new Color4(0.2f, 0.65f, 0.78f, 0.8f);
            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.04f, 0.12f, 0.22f, 1),
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(7, 0),
                    Children = new Drawable[]
                    {
                        buttonIcon = new SpriteIcon
                        {
                            Size = new Vector2(14),
                            Icon = icon,
                            Colour = HomeControlColours.Cyan,
                        },
                        buttonText = new SpriteText
                        {
                            Text = label,
                            Font = HomeTypography.Body(13),
                            Colour = Color4.White,
                        },
                    },
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeColour(new Color4(0.06f, 0.22f, 0.34f, 1), 100);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e) =>
            background.FadeColour(new Color4(0.04f, 0.12f, 0.22f, 1), 120);
    }
}
