using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

public partial class GameplayHud : CompositeDrawable
{
    private readonly SpriteText timeText;
    private readonly SpriteText comboText;
    private readonly SpriteText accuracyText;
    private readonly SpriteText countsText;
    private readonly SpriteText audioText;
    private AudioReadoutState displayedAudioState;
    private bool hasDisplayedAudioState;

    internal string DisplayedAudioStatus =>
        audioText?.Text.ToString() ?? string.Empty;

    public GameplayHud(
        YokkoBeatmap beatmap,
        ManiaModSet mods = null)
    {
        mods ??= ManiaModSet.Empty;
        string modSummary = mods.IsEmpty
            ? string.Empty
            : $" · {string.Join(' ', mods.Acronyms)}";
        Width = 340;
        Height = 204;
        Masking = true;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.035f, 0.045f, 0.065f, 0.92f),
            },
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 10),
                Padding = new MarginPadding(18),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text =
                            $"{beatmap.Title} [{beatmap.DifficultyName}]"
                            + modSummary,
                        Font = FontUsage.Default.With(size: 20),
                        Colour = YokkoPalette.Text,
                    },
                    timeText = createLine(),
                    comboText = createLine(),
                    accuracyText = createLine(),
                    countsText = createLine(16),
                    audioText = createLine(14),
                },
            },
        };
    }

    public void UpdateState(double gameplayTimeMilliseconds, BeatmapJudgementState state)
    {
        timeText.Text = $"Time {Math.Max(0, gameplayTimeMilliseconds / 1000):0.00}s";
        comboText.Text =
            $"Score {state.Score:0000000}  Combo {state.Combo} / Max {state.MaxCombo}";
        string rank = state.Rank == ScoreRank.X
            ? "SS"
            : state.Rank.ToString();
        accuracyText.Text =
            $"Accuracy {state.Accuracy * 100:0.00}%  Rank {rank}";
        countsText.Text =
            $"P {state.Counts.Perfect}  G {state.Counts.Great}  "
            + $"Good {state.Counts.Good}  Ok {state.Counts.Ok}  "
            + $"Meh {state.Counts.Meh}  M {state.Counts.Miss}";
    }

    public void UpdateAudioStatus(
        AudioEngineStatus status,
        AudioBackendKind requestedBackend)
    {
        var nextState = new AudioReadoutState(
            requestedBackend,
            status.ActiveBackend,
            status.SampleRate,
            status.BufferSize,
            status.EstimatedOutputLatencyMilliseconds,
            status.IsFaulted,
            status.HasUnderrun,
            status.CallbackDeadlineMissCount > 0,
            status.CallbackCadenceMissCount > 0);
        if (hasDisplayedAudioState
            && nextState == displayedAudioState)
            return;

        displayedAudioState = nextState;
        hasDisplayedAudioState = true;

        bool fellBack =
            requestedBackend == AudioBackendKind.WasapiExclusive
            && status.ActiveBackend == AudioBackendKind.SharedWasapi;
        bool unhealthy =
            status.IsFaulted
            || status.HasUnderrun
            || status.CallbackDeadlineMissCount > 0
            || status.CallbackCadenceMissCount > 0;
        string backend = status.ActiveBackend switch
        {
            AudioBackendKind.WasapiExclusive => "WASAPI EXCLUSIVE",
            AudioBackendKind.SharedWasapi => "WASAPI SHARED",
            AudioBackendKind.Asio => "ASIO",
            _ => "AUDIO STARTING",
        };
        string state = status.IsFaulted
            ? " · FAULT"
            : fellBack
                ? " · FALLBACK"
                : unhealthy
                    ? " · UNSTABLE"
                    : string.Empty;
        string timing = status.SampleRate > 0 && status.BufferSize > 0
            ? $" · {status.BufferSize}f · "
              + $"{status.EstimatedOutputLatencyMilliseconds:0.00} ms"
            : string.Empty;

        audioText.Text = backend + timing + state;
        audioText.Colour = unhealthy || fellBack
            ? YokkoPalette.Rose
            : YokkoPalette.TextMuted;
    }

    public void ShowFrameClock()
    {
        hasDisplayedAudioState = false;
        audioText.Text = "NO AUDIO · FRAME CLOCK";
        audioText.Colour = YokkoPalette.TextMuted;
    }

    private readonly record struct AudioReadoutState(
        AudioBackendKind RequestedBackend,
        AudioBackendKind ActiveBackend,
        int SampleRate,
        int BufferSize,
        double EstimatedOutputLatencyMilliseconds,
        bool IsFaulted,
        bool HasUnderrun,
        bool HasCallbackDeadlineMiss,
        bool HasCallbackCadenceMiss);

    private static SpriteText createLine(float size = 18) => new()
    {
        Font = FontUsage.Default.With(size: size),
        Colour = YokkoPalette.TextMuted,
    };
}
