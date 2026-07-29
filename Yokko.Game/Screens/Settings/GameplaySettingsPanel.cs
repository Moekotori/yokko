using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Audio;
using Yokko.Core.Gameplay;
using Yokko.Core.Scoring;
using Yokko.Game.Audio;
using Yokko.Game.Gameplay;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal enum GameplaySettingsSection
{
    Input,
    Timing,
    Judgement,
    Feedback,
}

internal partial class GameplaySettingsPanel : CompositeDrawable, ISettingsTransientUi
{
    private readonly YokkoGameplaySettings settings;
    private readonly YokkoAudioSettings audioSettings;
    private readonly List<GameplaySectionTab> sectionTabs = new();
    private readonly List<GameplayBindingCard> bindingCards = new();
    private readonly List<Key> sequentialKeys = new();
    private readonly HashSet<Key> pressedKeys = new();
    private readonly Clipboard clipboard;
    private readonly AudioSettingsTestPlayer calibrationPlayer;
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private SpriteText statusTitle;
    private SpriteText statusMetadata;
    private Circle statusIconBackground;
    private GameplayCompactButton calibrationButton;
    private readonly Container contentHost;
    private GameplayBindingCard capturingCard;
    private SpriteText keyCaptureHint;
    private CancellationTokenSource calibrationRunCancellation;
    private GameplayCalibrationSession calibrationSession;
    private double? pendingCalibrationSuggestion;
    private double nextCalibrationStatusUpdate;
    private int lastCalibrationPulseBeat = -1;
    private bool calibrationPreparing;
    private bool calibrationResultVisible;
    private bool disposed;
    private bool sequentialCapture;
    private KeyMode selectedKeyMode = KeyMode.FourKey;

    internal GameplaySettingsSection CurrentSection { get; private set; } =
        GameplaySettingsSection.Input;

    internal KeyMode SelectedKeyMode => selectedKeyMode;

    internal bool IsCapturingKey => capturingCard != null;

    internal bool IsSequentialCapture => sequentialCapture;

    internal int SequentialCaptureIndex => sequentialKeys.Count;

    internal int VisibleBindingCardCount => bindingCards.Count;

    internal double CurrentScrollSpeed => settings.ScrollSpeed.Value;

    internal double QuaverScrollRateNormalization =>
        settings.QuaverScrollRateNormalization.Value;

    internal JudgementMode CurrentJudgementMode =>
        settings.JudgementMode.Value;

    internal int CurrentEtternaJustice =>
        settings.GetJudgementConfiguration().EtternaJustice;

    internal bool ShowLanePressFeedback =>
        settings.ShowLanePressFeedback.Value;

    internal bool ShowTimingBar => settings.ShowTimingBar.Value;

    internal bool KeysoundsEnabled => settings.KeysoundsEnabled.Value;

    internal bool MinesEnabled => settings.MinesEnabled.Value;

    internal bool PauseWhenUnfocused =>
        settings.PauseWhenUnfocused.Value;

    internal bool IsCalibrationActive =>
        calibrationPreparing || calibrationSession != null;

    internal int CalibrationSampleCount =>
        calibrationSession?.SampleCount ?? 0;

    internal int PressedKeyCount => pressedKeys.Count;

    public GameplaySettingsPanel(
        YokkoGameplaySettings settings,
        YokkoAudioSettings audioSettings,
        string testDirectory,
        Clipboard clipboard)
    {
        this.settings = settings;
        this.audioSettings = audioSettings;
        this.clipboard = clipboard;
        calibrationPlayer = new AudioSettingsTestPlayer(
            audioSettings,
            AudioEngineFactory.CreateDefault,
            testDirectory);
        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(378, 42),
                Text = YokkoStrings.Get("settings.gameplay.title"),
                Font = HomeTypography.Display(58),
                Spacing = new Vector2(0.45f, 0),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(378, 105),
                Text = YokkoStrings.Get("settings.gameplay.subtitle"),
                Font = HomeTypography.Body(20),
                Spacing = new Vector2(0.2f, 0),
                Colour = SettingsTheme.MutedNavy,
            },
            createCategoryMark(),
            createStatusCard(),
            createSectionTabs(),
            contentHost = new Container
            {
                Position = new Vector2(378, 320),
                Size = new Vector2(840, 296),
            },
            new SettingsPanelFooter(),
            new HomeDotCross
            {
                Position = new Vector2(1088, 594),
                Scale = new Vector2(1.1f),
            },
            createDecorationIcon(
                FontAwesome.Solid.Plus,
                1172,
                601,
                16,
                HomeControlColours.Pink),
            createDecorationIcon(
                FontAwesome.Solid.Plus,
                1200,
                637,
                12,
                HomeControlColours.Yellow),
        };

        foreach (Bindable<Key> binding in settings.SupportedKeyModes.SelectMany(
                     settings.GetBindableKeys))
        {
            binding.BindValueChanged(onBindingChanged);
        }

        refreshStatusMetadata();
        showSection(CurrentSection, false);
    }

    internal void SelectSection(GameplaySettingsSection section) =>
        showSection(section, true);

    internal void SelectKeyMode(KeyMode keyMode)
    {
        if (keyMode == selectedKeyMode)
            return;

        cancelCapture();
        clearPressedKeys();
        selectedKeyMode = keyMode;
        showInputSection(true);
    }

    internal void SelectAdjacentKeyMode(int direction)
    {
        if (direction == 0)
            return;

        int current = 0;
        for (int index = 0; index < settings.SupportedKeyModes.Count; index++)
        {
            if (settings.SupportedKeyModes[index] == selectedKeyMode)
            {
                current = index;
                break;
            }
        }
        int next = (current + Math.Sign(direction)
                    + settings.SupportedKeyModes.Count)
                   % settings.SupportedKeyModes.Count;
        SelectKeyMode(settings.SupportedKeyModes[next]);
    }

    internal void BeginKeyCapture(int lane)
    {
        if (CurrentSection != GameplaySettingsSection.Input)
            showSection(GameplaySettingsSection.Input, false);

        if ((uint)lane >= bindingCards.Count)
            throw new ArgumentOutOfRangeException(nameof(lane));

        cancelCapture();
        capturingCard = bindingCards[lane];
        capturingCard.SetCapturing(true);
    }

    internal void BeginSequentialKeyCapture()
    {
        if (CurrentSection != GameplaySettingsSection.Input)
            showSection(GameplaySettingsSection.Input, false);

        cancelCapture();
        sequentialCapture = true;
        sequentialKeys.Clear();
        capturingCard = bindingCards[0];
        capturingCard.SetCapturing(true);
        refreshSequentialHint();
    }

    internal void ResetSelectedBindings()
    {
        cancelCapture();
        settings.ResetBindings(selectedKeyMode);
        showInputMessage(YokkoStrings.Get(
            "settings.gameplay.preset_applied",
            YokkoStrings.Get("settings.gameplay.preset_standard")));
    }

    internal void ApplyBindingPreset(GameplayKeyPreset preset)
    {
        cancelCapture();
        settings.ApplyBindingPreset(selectedKeyMode, preset);
        showInputMessage(YokkoStrings.Get(
            "settings.gameplay.preset_applied",
            presetLabel(preset)));
    }

    internal void CopySelectedBindings()
    {
        cancelCapture();
        settings.CopyBindingsToOtherMode(selectedKeyMode);
        showInputMessage(YokkoStrings.Get(
            "settings.gameplay.profile_copied",
            selectedKeyMode == KeyMode.FourKey ? "4K" : "7K",
            selectedKeyMode == KeyMode.FourKey ? "7K" : "4K"));
    }

    internal void ExportKeyProfiles()
    {
        clipboard.SetText(GameplayKeyProfileCodec.Encode(settings));
        showInputMessage(YokkoStrings.Get(
            "settings.gameplay.profile_exported"));
    }

    internal bool ImportKeyProfiles()
    {
        try
        {
            GameplayKeyProfileCodec.DecodeAndApply(
                clipboard.GetText(),
                settings);
            showInputMessage(YokkoStrings.Get(
                "settings.gameplay.profile_imported"));
            return true;
        }
        catch (Exception ex) when (
            ex is FormatException
            or ArgumentException
            or InvalidOperationException)
        {
            showInputMessage(YokkoStrings.Get(
                "settings.gameplay.profile_import_failed"));
            return false;
        }
    }

    internal void StartCalibration()
    {
        if (IsCalibrationActive)
            return;

        cancelCapture();
        calibrationResultVisible = false;
        pendingCalibrationSuggestion = null;
        calibrationPreparing = true;
        calibrationRunCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                lifetimeCancellation.Token);
        refreshCalibrationStatus();
        _ = runCalibrationAsync(calibrationRunCancellation.Token);
    }

    internal void StartCalibrationForTest(double startTime)
    {
        cancelCalibration(false);
        calibrationSession = new GameplayCalibrationSession(startTime);
        calibrationPreparing = false;
        refreshCalibrationStatus(startTime);
    }

    internal double FinishCalibrationForTest(double currentTime)
    {
        if (calibrationSession == null)
            return 0;

        double suggestion =
            calibrationSession.SuggestedOffsetMilliseconds;
        finishCalibration(currentTime);
        return suggestion;
    }

    internal void SetScrollSpeed(double speed) =>
        settings.SetScrollSpeed(speed);

    internal void SetJudgementMode(JudgementMode mode) =>
        settings.JudgementMode.Value = mode;

    internal void SetEtternaJustice(int justice) =>
        settings.SetEtternaJustice(justice);

    internal Key GetBinding(KeyMode keyMode, int lane) =>
        settings.GetKeys(keyMode)[lane];

    internal void SetLanePressFeedback(bool enabled) =>
        settings.ShowLanePressFeedback.Value = enabled;

    internal void SetShowTimingBar(bool enabled) =>
        settings.ShowTimingBar.Value = enabled;

    internal void SetKeysoundsEnabled(bool enabled) =>
        settings.KeysoundsEnabled.Value = enabled;

    internal void SetMinesEnabled(bool enabled) =>
        settings.MinesEnabled.Value = enabled;

    internal void SetPauseWhenUnfocused(bool enabled) =>
        settings.PauseWhenUnfocused.Value = enabled;

    internal bool HandleKeyDown(Key key)
    {
        if (capturingCard != null)
        {
            if (key == Key.Escape)
            {
                cancelCapture();
                return true;
            }

            captureKey(key);
            return true;
        }

        if (key == Key.Escape && IsCalibrationActive)
        {
            cancelCalibration(true);
            return true;
        }

        if (!pressedKeys.Add(key))
            return findLane(key) >= 0;

        int lane = findLane(key);
        if (lane < 0)
        {
            refreshLiveInputStatus(key, -1);
            return false;
        }

        if (lane < bindingCards.Count)
            bindingCards[lane].SetPressed(true);

        if (calibrationSession?.TryRecordTap(Time.Current) == true)
            refreshCalibrationStatus(Time.Current);
        else if (!IsCalibrationActive)
            refreshLiveInputStatus(key, lane);

        return true;
    }

    internal void HandleKeyUp(Key key)
    {
        pressedKeys.Remove(key);
        int lane = findLane(key);
        if (lane >= 0 && lane < bindingCards.Count)
            bindingCards[lane].SetPressed(false);

        if (!IsCalibrationActive && pressedKeys.Count == 0)
            refreshStatusMetadata();
    }

    public bool DismissTransientUi()
    {
        if (capturingCard != null)
        {
            cancelCapture();
            return true;
        }

        if (IsCalibrationActive)
        {
            cancelCalibration(true);
            return true;
        }

        return false;
    }

    private Drawable createSectionTabs()
    {
        var flow = new FillFlowContainer
        {
            Position = new Vector2(378, 260),
            Size = new Vector2(840, 44),
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(9, 0),
        };

        addTab(
            flow,
            GameplaySettingsSection.Input,
            YokkoStrings.Get("settings.gameplay.section_input"),
            FontAwesome.Solid.Keyboard,
            203);
        addTab(
            flow,
            GameplaySettingsSection.Timing,
            YokkoStrings.Get("settings.gameplay.section_timing"),
            FontAwesome.Solid.WaveSquare,
            203);
        addTab(
            flow,
            GameplaySettingsSection.Judgement,
            YokkoStrings.Get("settings.gameplay.section_judgement"),
            FontAwesome.Solid.Bullseye,
            203);
        addTab(
            flow,
            GameplaySettingsSection.Feedback,
            YokkoStrings.Get("settings.gameplay.section_feedback"),
            FontAwesome.Solid.Heartbeat,
            203);

        return flow;
    }

    private void addTab(
        FillFlowContainer flow,
        GameplaySettingsSection section,
        LocalisableString label,
        IconUsage icon,
        float width)
    {
        var tab = new GameplaySectionTab(
            label,
            icon,
            () => showSection(section, true),
            width);
        tab.Value = section;
        sectionTabs.Add(tab);
        flow.Add(tab);
    }

    private void showSection(
        GameplaySettingsSection section,
        bool animate)
    {
        if (section != GameplaySettingsSection.Input)
            cancelCalibration(false);

        cancelCapture();
        clearPressedKeys();
        CurrentSection = section;

        foreach (GameplaySectionTab tab in sectionTabs)
            tab.SetSelected((GameplaySettingsSection)tab.Value == section);

        switch (section)
        {
            case GameplaySettingsSection.Input:
                showInputSection(animate);
                break;

            case GameplaySettingsSection.Timing:
                setContent(createTimingSection(), animate);
                break;

            case GameplaySettingsSection.Judgement:
                setContent(createJudgementSection(), animate);
                break;

            case GameplaySettingsSection.Feedback:
                setContent(createFeedbackSection(), animate);
                break;
        }
    }

    private void showInputSection(bool animate)
    {
        bindingCards.Clear();

        var panel = createPanel();
        keyCaptureHint = new SpriteText
        {
            Position = new Vector2(20, 258),
            Text = YokkoStrings.Get(
                "settings.gameplay.key_swap_hint"),
            Font = HomeTypography.Body(14),
            Colour = SettingsTheme.MutedNavy,
        };
        var children = new List<Drawable>();
        children.AddRange(new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(20, 18),
                Text = YokkoStrings.Get("settings.gameplay.key_profile"),
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Navy,
            },
            new GameplayCompactButton(
                "‹",
                () => SelectAdjacentKeyMode(-1),
                42)
            {
                Position = new Vector2(154, 10),
            },
            new GameplayCompactButton(
                OsuManiaKeyLayout.GetDisplayName(selectedKeyMode),
                () => SelectAdjacentKeyMode(1),
                124)
            {
                Position = new Vector2(202, 10),
                IsSelected = true,
            },
            new GameplayCompactButton(
                "›",
                () => SelectAdjacentKeyMode(1),
                42)
            {
                Position = new Vector2(332, 10),
            },
            new GameplayCompactButton(
                "4K",
                () => SelectKeyMode(KeyMode.FourKey),
                54)
            {
                Position = new Vector2(386, 10),
                IsSelected = selectedKeyMode == KeyMode.FourKey,
            },
            new GameplayCompactButton(
                "7K",
                () => SelectKeyMode(KeyMode.SevenKey),
                54)
            {
                Position = new Vector2(446, 10),
                IsSelected = selectedKeyMode == KeyMode.SevenKey,
            },
            new GameplayCompactButton(
                YokkoStrings.Get("settings.gameplay.edit_all"),
                BeginSequentialKeyCapture,
                150,
                FontAwesome.Solid.Keyboard)
            {
                Position = new Vector2(538, 10),
            },
            new GameplayCompactButton(
                YokkoStrings.Get("settings.gameplay.reset"),
                ResetSelectedBindings,
                122,
                FontAwesome.Solid.ArrowLeft)
            {
                Position = new Vector2(698, 10),
            },
            keyCaptureHint,
            createBindingCards(),
        });

        if (selectedKeyMode is KeyMode.FourKey or KeyMode.SevenKey)
        {
            children.AddRange(createPresetControls());
        }
        else
        {
            children.Add(new SpriteText
            {
                Position = new Vector2(20, 70),
                Text = YokkoStrings.Get(
                    "settings.gameplay.all_modes_hint"),
                Font = HomeTypography.Body(14),
                Colour = SettingsTheme.MutedNavy,
            });
        }

        children.AddRange(createProfileTransferControls(
            selectedKeyMode is KeyMode.FourKey or KeyMode.SevenKey));
        setPanelChildren(panel, children);

        setContent(panel, animate);
    }

    private IEnumerable<Drawable> createPresetControls()
    {
        yield return new SpriteText
        {
            Position = new Vector2(20, 70),
            Text = YokkoStrings.Get("settings.gameplay.presets"),
            Font = HomeTypography.Display(15),
            Colour = HomeControlColours.Navy,
        };
        yield return new GameplayCompactButton(
            YokkoStrings.Get("settings.gameplay.preset_standard"),
            () => ApplyBindingPreset(GameplayKeyPreset.Standard),
            76)
        {
            Position = new Vector2(98, 58),
        };
        yield return new GameplayCompactButton(
            YokkoStrings.Get("settings.gameplay.preset_left"),
            () => ApplyBindingPreset(GameplayKeyPreset.LeftHanded),
            76)
        {
            Position = new Vector2(180, 58),
        };
        yield return new GameplayCompactButton(
            YokkoStrings.Get("settings.gameplay.preset_split"),
            () => ApplyBindingPreset(GameplayKeyPreset.Split),
            76)
        {
            Position = new Vector2(262, 58),
        };
        yield return new GameplayCompactButton(
            YokkoStrings.Get("settings.gameplay.copy_other_mode"),
            CopySelectedBindings,
            140,
            FontAwesome.Solid.Copy)
        {
            Position = new Vector2(348, 58),
        };
    }

    private IEnumerable<Drawable> createProfileTransferControls(
        bool compactPosition)
    {
        float exportX = compactPosition ? 498 : 604;
        yield return new GameplayCompactButton(
            YokkoStrings.Get("settings.gameplay.export_profile"),
            ExportKeyProfiles,
            96,
            FontAwesome.Solid.Upload)
        {
            Position = new Vector2(exportX, 58),
        };
        yield return new GameplayCompactButton(
            YokkoStrings.Get("settings.gameplay.import_profile"),
            () => ImportKeyProfiles(),
            96,
            FontAwesome.Solid.Download)
        {
            Position = new Vector2(exportX + 106, 58),
        };
    }

    private Drawable createBindingCards()
    {
        IReadOnlyList<Bindable<Key>> bindings =
            settings.GetBindableKeys(selectedKeyMode);
        int rowCount = bindings.Count > 10 ? 2 : 1;
        int cardsPerRow = bindings.Count / rowCount;
        float spacing = rowCount == 1 ? 10 : 8;
        float width = (800 - spacing * (cardsPerRow - 1)) /
                      cardsPerRow;
        var host = new Container
        {
            Position = new Vector2(20, 108),
            Size = new Vector2(800, 132),
        };

        for (int row = 0; row < rowCount; row++)
        {
            var flow = new FillFlowContainer
            {
                Y = row * 70,
                Size = new Vector2(800, rowCount == 1 ? 132 : 62),
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(spacing, 0),
            };
            host.Add(flow);

            int firstLane = row * cardsPerRow;
            for (int index = 0; index < cardsPerRow; index++)
            {
                int lane = firstLane + index;
                int capturedLane = lane;
                var card = new GameplayBindingCard(
                    lane,
                    bindings[lane],
                    () => BeginKeyCapture(capturedLane),
                    width,
                    rowCount > 1);
                card.Height = rowCount == 1 ? 132 : 62;
                if (pressedKeys.Contains(bindings[lane].Value))
                    card.SetPressed(true);
                bindingCards.Add(card);
                flow.Add(card);
            }
        }

        return host;
    }

    private Drawable createTimingSection()
    {
        var panel = createPanel();
        setPanelChildren(panel, new Drawable[]
        {
            createControlLabel(
                YokkoStrings.Get("settings.gameplay.scroll_speed"),
                YokkoStrings.Get(
                    "settings.gameplay.scroll_speed_note"),
                20,
                18),
            new GameplayValueStepper(
                settings.ScrollSpeed,
                OsuManiaScrollSpeed.ShortcutStep,
                OsuManiaScrollSpeed.Minimum,
                OsuManiaScrollSpeed.Maximum,
                value =>
                    $"{(int)OsuManiaScrollSpeed.ComputeScrollTime(value)} ms  ·  {value:0.0}")
            {
                Position = new Vector2(430, 14),
            },
            new SpriteText
            {
                Position = new Vector2(20, 91),
                Text = YokkoStrings.Get(
                    "settings.gameplay.speed_presets"),
                Font = HomeTypography.Display(16),
                Colour = HomeControlColours.Navy,
            },
            createSpeedPresets(),
            new Box
            {
                Position = new Vector2(20, 143),
                Size = new Vector2(800, 1),
                Colour = SettingsTheme.Divider,
            },
            createControlLabel(
                YokkoStrings.Get(
                    "settings.gameplay.quaver_rate_normalization"),
                YokkoStrings.Get(
                    "settings.gameplay.quaver_rate_normalization_note"),
                20,
                157),
            new GameplayValueStepper(
                settings.QuaverScrollRateNormalization,
                10,
                0,
                100,
                value => $"{value:0}%")
            {
                Position = new Vector2(430, 153),
            },
            createControlLabel(
                YokkoStrings.Get("settings.gameplay.input_offset"),
                YokkoStrings.Get(
                    "settings.gameplay.input_offset_note"),
                20,
                225),
            new GameplayValueStepper(
                audioSettings.UserOffsetMilliseconds,
                1,
                -200,
                200,
                value => $"{value:+0;-0;0} ms")
            {
                Position = new Vector2(430, 221),
            },
        });

        return panel;
    }

    private Drawable createJudgementSection()
    {
        var panel = createPanel();
        setPanelChildren(panel, new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(20, 18),
                Text = YokkoStrings.Get(
                    "settings.gameplay.judgement_heading"),
                Font = HomeTypography.Display(19),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(20, 47),
                Text = YokkoStrings.Get(
                    "settings.gameplay.judgement_note"),
                Font = HomeTypography.Body(14),
                Colour = SettingsTheme.MutedNavy,
            },
            new GameplayJudgementModeSelector(settings.JudgementMode)
            {
                Position = new Vector2(20, 78),
            },
            new Box
            {
                Position = new Vector2(20, 145),
                Size = new Vector2(800, 1),
                Colour = SettingsTheme.Divider,
            },
            createControlLabel(
                YokkoStrings.Get("settings.gameplay.etterna_justice"),
                YokkoStrings.Get(
                    "settings.gameplay.etterna_justice_note"),
                20,
                162),
            new GameplayValueStepper(
                settings.EtternaJustice,
                1,
                JudgementConfiguration.MinimumEtternaJustice,
                JudgementConfiguration.MaximumEtternaJustice,
                value =>
                    Math.Round(value)
                    == JudgementConfiguration.MaximumEtternaJustice
                        ? "Justice · J9"
                        : $"J{Math.Round(value):0}")
            {
                Position = new Vector2(430, 157),
            },
            new SpriteText
            {
                Position = new Vector2(20, 236),
                Text = YokkoStrings.Get(
                    "settings.gameplay.etterna_boundaries"),
                Font = HomeTypography.Body(13),
                Colour = SettingsTheme.MutedNavy,
            },
        });

        return panel;
    }

    private Drawable createSpeedPresets()
    {
        double[] speeds = { 8, 15, 20, 30 };
        var flow = new FillFlowContainer
        {
            Position = new Vector2(180, 78),
            Size = new Vector2(640, 52),
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(8, 0),
        };

        foreach (double speed in speeds)
        {
            double capturedSpeed = speed;
            flow.Add(new GameplayCompactButton(
                $"{speed:0}",
                () => settings.SetScrollSpeed(capturedSpeed),
                154));
        }

        return flow;
    }

    private Drawable createFeedbackSection()
    {
        var panel = createPanel();
        setPanelChildren(panel, new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(20, 18),
                Text = YokkoStrings.Get(
                    "settings.gameplay.feedback_heading"),
                Font = HomeTypography.Display(19),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(20, 47),
                Text = YokkoStrings.Get(
                    "settings.gameplay.feedback_note"),
                Font = HomeTypography.Body(15),
                Colour = SettingsTheme.MutedNavy,
            },
            new FillFlowContainer
            {
                Position = new Vector2(20, 78),
                Size = new Vector2(826, 84),
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(14, 0),
                Children = new Drawable[]
                {
                    new GameplayToggleCard(
                        YokkoStrings.Get(
                            "settings.gameplay.show_lane_feedback"),
                        YokkoStrings.Get(
                            "settings.gameplay.show_lane_feedback_note"),
                        FontAwesome.Solid.Keyboard,
                        settings.ShowLanePressFeedback),
                    new GameplayToggleCard(
                        YokkoStrings.Get(
                            "settings.gameplay.mines"),
                        YokkoStrings.Get(
                            "settings.gameplay.mines_note"),
                        FontAwesome.Solid.Bomb,
                        settings.MinesEnabled),
                },
            },
            new FillFlowContainer
            {
                Position = new Vector2(20, 172),
                Size = new Vector2(826, 84),
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(14, 0),
                Children = new Drawable[]
                {
                    new GameplayToggleCard(
                        YokkoStrings.Get(
                            "settings.gameplay.keysounds"),
                        YokkoStrings.Get(
                            "settings.gameplay.keysounds_note"),
                        FontAwesome.Solid.VolumeUp,
                        settings.KeysoundsEnabled),
                    new GameplayToggleCard(
                        YokkoStrings.Get(
                            "settings.gameplay.pause_when_unfocused"),
                        YokkoStrings.Get(
                            "settings.gameplay.pause_when_unfocused_note"),
                        FontAwesome.Solid.PauseCircle,
                        settings.PauseWhenUnfocused),
                },
            },
            new GameplayInlineToggle(
                YokkoStrings.Get(
                    "settings.gameplay.show_timing_bar"),
                YokkoStrings.Get(
                    "settings.gameplay.show_timing_bar_note"),
                settings.ShowTimingBar)
            {
                Position = new Vector2(20, 264),
                Size = new Vector2(800, 26),
            },
        });

        return panel;
    }

    private static Container createPanel() => new()
    {
        RelativeSizeAxes = Axes.Both,
        Masking = true,
        CornerRadius = 8,
        BorderThickness = 1.2f,
        BorderColour = SettingsTheme.Divider,
    };

    private static void setPanelChildren(
        Container panel,
        IReadOnlyList<Drawable> children)
    {
        panel.Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
        }.Concat(children).ToArray();
    }

    private void setContent(Drawable content, bool animate)
    {
        content.Alpha = animate ? 0 : 1;
        content.X = animate ? 8 : 0;
        contentHost.Child = content;

        if (!animate)
            return;

        content.FadeIn(150, Easing.OutQuint);
        content.MoveToX(0, 170, Easing.OutQuint);
    }

    private static Drawable createControlLabel(
        LocalisableString title,
        LocalisableString note,
        float x,
        float y) => new FillFlowContainer
    {
        Position = new Vector2(x, y),
        Width = 390,
        AutoSizeAxes = Axes.Y,
        Direction = FillDirection.Vertical,
        Spacing = new Vector2(0, 5),
        Children = new Drawable[]
        {
            new SpriteText
            {
                Text = title,
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Text = note,
                Font = HomeTypography.Body(14),
                Colour = SettingsTheme.MutedNavy,
            },
        },
    };

    private static Drawable createCategoryMark() => new Container
    {
        Position = new Vector2(1094, 40),
        Size = new Vector2(124, 92),
        Children = new Drawable[]
        {
            new Circle
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(78),
                Colour = SettingsTheme.PaleCyan,
            },
            new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(34),
                Icon = FontAwesome.Solid.Gamepad,
                Colour = HomeControlColours.Navy,
            },
            new SpriteIcon
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-8, 8),
                Size = new Vector2(14),
                Icon = FontAwesome.Solid.Plus,
                Colour = HomeControlColours.Pink,
            },
        },
    };

    private Drawable createStatusCard() => new Container
    {
        Position = new Vector2(378, 150),
        Size = new Vector2(840, 92),
        Masking = true,
        CornerRadius = 9,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SettingsTheme.StatusCyan,
            },
            statusIconBackground = new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 52,
                Size = new Vector2(60),
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 52,
                Size = new Vector2(27),
                Icon = FontAwesome.Solid.Keyboard,
                Colour = HomeControlColours.Navy,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 96,
                Width = 535,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 3),
                Children = new Drawable[]
                {
                    statusTitle = new SpriteText
                    {
                        Font = HomeTypography.Display(22),
                        Colour = HomeControlColours.Navy,
                    },
                    statusMetadata = new SpriteText
                    {
                        Font = HomeTypography.Body(15),
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
            calibrationButton = new GameplayCompactButton(
                YokkoStrings.Get("settings.gameplay.calibration_start"),
                handleCalibrationButton,
                170,
                FontAwesome.Solid.Stopwatch)
            {
                Position = new Vector2(648, 25),
            },
        },
    };

    private void refreshStatusMetadata()
    {
        if (IsCalibrationActive
            || calibrationResultVisible
            || pressedKeys.Count > 0)
        {
            return;
        }

        statusTitle.Text = YokkoStrings.Get(
            "settings.gameplay.input_monitor");
        statusMetadata.Text = YokkoStrings.Get(
            "settings.gameplay.ready_metadata",
            formatKeys(settings.FourKeyBindings),
            formatKeys(settings.SevenKeyBindings));
        calibrationButton.SetText(YokkoStrings.Get(
            "settings.gameplay.calibration_start"));
        statusIconBackground.FadeColour(Color4.White, 120);
    }

    private static string formatKeys(
        IEnumerable<Bindable<Key>> bindings) =>
        string.Join("  ", bindings.Select(binding =>
            KeyModeBindings.FormatKey(binding.Value).ToUpperInvariant()));

    private void onBindingChanged(ValueChangedEvent<Key> _) =>
        refreshStatusMetadata();

    private void cancelCapture()
    {
        foreach (GameplayBindingCard card in bindingCards)
            card.SetCapturing(false);

        capturingCard = null;
        sequentialCapture = false;
        sequentialKeys.Clear();

        if (keyCaptureHint != null)
        {
            keyCaptureHint.Text = YokkoStrings.Get(
                "settings.gameplay.key_swap_hint");
        }
    }

    private void refreshSequentialHint()
    {
        keyCaptureHint.Text = YokkoStrings.Get(
            "settings.gameplay.sequence_hint",
            sequentialKeys.Count + 1,
            bindingCards.Count);
    }

    private void captureKey(Key key)
    {
        if (sequentialCapture)
        {
            if (sequentialKeys.Contains(key))
            {
                keyCaptureHint.Text = YokkoStrings.Get(
                    "settings.gameplay.sequence_duplicate");
                capturingCard.ShowDuplicate();
                return;
            }

            int sequenceLane = sequentialKeys.Count;
            sequentialKeys.Add(key);
            bindingCards[sequenceLane].SetPreviewKey(key);

            if (sequentialKeys.Count == bindingCards.Count)
            {
                settings.SetBindings(selectedKeyMode, sequentialKeys);
                sequentialCapture = false;
                capturingCard = null;

                foreach (GameplayBindingCard card in bindingCards)
                    card.SetCapturing(false);

                keyCaptureHint.Text = YokkoStrings.Get(
                    "settings.gameplay.sequence_saved",
                    selectedKeyMode == KeyMode.FourKey ? 4 : 7,
                    formatKeys(settings.GetBindableKeys(selectedKeyMode)));
                return;
            }

            capturingCard = bindingCards[sequentialKeys.Count];
            capturingCard.SetCapturing(true);
            refreshSequentialHint();
            return;
        }

        int lane = bindingCards.IndexOf(capturingCard);
        IReadOnlyList<Bindable<Key>> bindings =
            settings.GetBindableKeys(selectedKeyMode);
        int duplicateLane = bindings
                            .Select((binding, index) =>
                                (binding, index))
                            .Where(entry =>
                                entry.index != lane
                                && entry.binding.Value == key)
                            .Select(entry => entry.index)
                            .DefaultIfEmpty(-1)
                            .First();

        GameplayBindingCard capturedCard = capturingCard;
        settings.SetBinding(selectedKeyMode, lane, key);
        capturingCard = null;
        capturedCard.SetCapturing(false);

        if (duplicateLane >= 0)
        {
            capturedCard.ShowSwap();
            bindingCards[duplicateLane].ShowSwap();
            keyCaptureHint.Text = YokkoStrings.Get(
                "settings.gameplay.key_swap_notice",
                KeyModeBindings.FormatKey(key).ToUpperInvariant(),
                duplicateLane + 1,
                lane + 1);
        }
        else
        {
            keyCaptureHint.Text = YokkoStrings.Get(
                "settings.gameplay.single_saved",
                lane + 1,
                KeyModeBindings.FormatKey(key).ToUpperInvariant());
        }
    }

    private int findLane(Key key)
    {
        IReadOnlyList<Bindable<Key>> bindings =
            settings.GetBindableKeys(selectedKeyMode);
        for (int lane = 0; lane < bindings.Count; lane++)
        {
            if (bindings[lane].Value == key)
                return lane;
        }

        return -1;
    }

    private void refreshLiveInputStatus(Key key, int lane)
    {
        statusTitle.Text = lane >= 0
            ? YokkoStrings.Get(
                "settings.gameplay.input_detected",
                KeyModeBindings.FormatKey(key).ToUpperInvariant(),
                lane + 1)
            : YokkoStrings.Get(
                "settings.gameplay.input_unbound",
                KeyModeBindings.FormatKey(key).ToUpperInvariant());
        statusMetadata.Text = YokkoStrings.Get(
            "settings.gameplay.input_chord",
            pressedKeys.Count,
            settings.GetBindableKeys(selectedKeyMode).Count);
        statusIconBackground
            .FlashColour(Color4.White, 120, Easing.OutQuint);
    }

    private void clearPressedKeys()
    {
        pressedKeys.Clear();
        foreach (GameplayBindingCard card in bindingCards)
            card.SetPressed(false);
    }

    private void showInputMessage(LocalisableString message)
    {
        if (keyCaptureHint != null)
            keyCaptureHint.Text = message;
    }

    private static LocalisableString presetLabel(GameplayKeyPreset preset) =>
        YokkoStrings.Get(preset switch
        {
            GameplayKeyPreset.Standard =>
                "settings.gameplay.preset_standard",
            GameplayKeyPreset.LeftHanded =>
                "settings.gameplay.preset_left",
            GameplayKeyPreset.Split =>
                "settings.gameplay.preset_split",
            _ => throw new ArgumentOutOfRangeException(
                nameof(preset),
                preset,
                null),
        });

    private void handleCalibrationButton()
    {
        if (IsCalibrationActive)
            return;

        if (pendingCalibrationSuggestion.HasValue)
        {
            double suggestion = pendingCalibrationSuggestion.Value;
            audioSettings.UserOffsetMilliseconds.Value = suggestion;
            pendingCalibrationSuggestion = null;
            statusTitle.Text = YokkoStrings.Get(
                "settings.gameplay.calibration_applied");
            statusMetadata.Text = YokkoStrings.Get(
                "settings.gameplay.calibration_applied_note",
                suggestion);
            calibrationButton.SetText(YokkoStrings.Get(
                "settings.gameplay.calibration_again"));
            return;
        }

        StartCalibration();
    }

    private async Task runCalibrationAsync(CancellationToken token)
    {
        try
        {
            await calibrationPlayer.PlayCalibrationAsync(
                () => Scheduler.Add(() =>
                {
                    if (disposed || token.IsCancellationRequested)
                        return;

                    calibrationPreparing = false;
                    calibrationSession =
                        new GameplayCalibrationSession(Time.Current);
                    nextCalibrationStatusUpdate = 0;
                    lastCalibrationPulseBeat = -1;
                    refreshCalibrationStatus(Time.Current);
                }),
                token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.Error(
                ex,
                "The gameplay timing calibration could not be played.",
                LoggingTarget.Runtime);
            Scheduler.Add(() =>
            {
                if (disposed)
                    return;

                calibrationPreparing = false;
                calibrationSession = null;
                calibrationResultVisible = true;
                statusTitle.Text = YokkoStrings.Get(
                    "settings.gameplay.calibration_failed");
                statusMetadata.Text = YokkoStrings.Get(
                    "settings.gameplay.calibration_failed_note");
                calibrationButton.SetText(YokkoStrings.Get(
                    "settings.gameplay.calibration_again"));
            });
        }
        finally
        {
            Scheduler.Add(() =>
            {
                if (disposed || token.IsCancellationRequested)
                    return;

                calibrationPreparing = false;
                if (calibrationSession != null)
                    finishCalibration(Time.Current);
            });
        }
    }

    private void refreshCalibrationStatus(double currentTime = 0)
    {
        if (calibrationPreparing)
        {
            statusTitle.Text = YokkoStrings.Get(
                "settings.gameplay.calibration_preparing");
            statusMetadata.Text = YokkoStrings.Get(
                "settings.gameplay.calibration_preparing_note");
            calibrationButton.SetText(YokkoStrings.Get(
                "settings.gameplay.calibration_wait"));
            return;
        }

        if (calibrationSession == null)
            return;

        double elapsed = currentTime - calibrationSession.StartTime;
        int beat = elapsed < GameplayCalibrationSession.LeadInMilliseconds
            ? -1
            : (int)Math.Floor(
                (elapsed - GameplayCalibrationSession.LeadInMilliseconds)
                / GameplayCalibrationSession.BeatIntervalMilliseconds);
        if (beat != lastCalibrationPulseBeat)
        {
            lastCalibrationPulseBeat = beat;
            statusIconBackground
                .FlashColour(Color4.White, 180, Easing.OutQuint);
        }

        double remaining =
            calibrationSession.RemainingMilliseconds(currentTime);
        statusTitle.Text = YokkoStrings.Get(
            "settings.gameplay.calibration_running");
        statusMetadata.Text = calibrationSession.SampleCount == 0
            ? YokkoStrings.Get(
                "settings.gameplay.calibration_running_note")
            : YokkoStrings.Get(
                "settings.gameplay.calibration_sample",
                calibrationSession.SampleCount,
                calibrationSession.LatestTapOffsetMilliseconds);
        calibrationButton.SetText(YokkoStrings.Get(
            "settings.gameplay.calibration_countdown",
            Math.Max(0, (int)Math.Ceiling(remaining / 1000))));
    }

    private void finishCalibration(double currentTime)
    {
        GameplayCalibrationSession completed = calibrationSession;
        if (completed == null)
            return;

        calibrationSession = null;
        calibrationPreparing = false;
        calibrationResultVisible = true;

        if (completed.HasRecommendation)
        {
            pendingCalibrationSuggestion =
                completed.SuggestedOffsetMilliseconds;
            statusTitle.Text = YokkoStrings.Get(
                "settings.gameplay.calibration_complete");
            statusMetadata.Text = YokkoStrings.Get(
                "settings.gameplay.calibration_result",
                pendingCalibrationSuggestion.Value,
                completed.SampleCount);
            calibrationButton.SetText(YokkoStrings.Get(
                "settings.gameplay.calibration_apply",
                pendingCalibrationSuggestion.Value));
        }
        else
        {
            pendingCalibrationSuggestion = null;
            statusTitle.Text = YokkoStrings.Get(
                "settings.gameplay.calibration_incomplete");
            statusMetadata.Text = YokkoStrings.Get(
                "settings.gameplay.calibration_incomplete_note",
                completed.SampleCount,
                GameplayCalibrationSession.MinimumUsefulSamples);
            calibrationButton.SetText(YokkoStrings.Get(
                "settings.gameplay.calibration_again"));
        }
    }

    private void cancelCalibration(bool showMessage)
    {
        calibrationRunCancellation?.Cancel();
        calibrationRunCancellation?.Dispose();
        calibrationRunCancellation = null;
        calibrationPreparing = false;
        calibrationSession = null;
        pendingCalibrationSuggestion = null;
        calibrationResultVisible = showMessage;
        lastCalibrationPulseBeat = -1;

        if (showMessage)
        {
            statusTitle.Text = YokkoStrings.Get(
                "settings.gameplay.calibration_cancelled");
            statusMetadata.Text = YokkoStrings.Get(
                "settings.gameplay.calibration_cancelled_note");
            calibrationButton.SetText(YokkoStrings.Get(
                "settings.gameplay.calibration_again"));
        }
        else
        {
            refreshStatusMetadata();
        }
    }

    protected override void Update()
    {
        base.Update();

        if (calibrationSession == null)
            return;

        if (calibrationSession.IsComplete(Time.Current))
        {
            finishCalibration(Time.Current);
            return;
        }

        if (Time.Current >= nextCalibrationStatusUpdate)
        {
            refreshCalibrationStatus(Time.Current);
            nextCalibrationStatusUpdate = Time.Current + 100;
        }
    }

    private static Drawable createDecorationIcon(
        IconUsage icon,
        float x,
        float y,
        float size,
        Color4 colour) => new SpriteIcon
    {
        Position = new Vector2(x, y),
        Size = new Vector2(size),
        Icon = icon,
        Colour = colour,
    };

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            disposed = true;
            lifetimeCancellation.Cancel();
            calibrationRunCancellation?.Cancel();
            calibrationRunCancellation?.Dispose();
            lifetimeCancellation.Dispose();
            _ = calibrationPlayer.DisposeAsync();
            foreach (Bindable<Key> binding in settings.SupportedKeyModes
                         .SelectMany(settings.GetBindableKeys))
            {
                binding.ValueChanged -= onBindingChanged;
            }
        }

        base.Dispose(isDisposing);
    }
}

internal partial class GameplaySectionTab : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteIcon icon;
    private readonly SpriteText text;
    private readonly Box accent;
    private bool selected;

    public object Value { get; set; }

    public GameplaySectionTab(
        LocalisableString label,
        IconUsage itemIcon,
        Action action,
        float width)
    {
        Action = action;
        Size = new Vector2(width, 44);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1.2f;
        BorderColour = SettingsTheme.Divider;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            accent = new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 5,
                Colour = HomeControlColours.Pink,
                Alpha = 0,
            },
            icon = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 18,
                Size = new Vector2(16),
                Icon = itemIcon,
                Colour = HomeControlColours.Navy,
            },
            text = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 46,
                Text = label,
                Font = HomeTypography.Display(16),
                Colour = HomeControlColours.Navy,
            },
        };
    }

    public void SetSelected(bool isSelected)
    {
        selected = isSelected;
        background.FadeColour(
            selected ? HomeControlColours.Navy : Color4.White,
            130,
            Easing.OutQuint);
        icon.FadeColour(
            selected ? Color4.White : HomeControlColours.Navy,
            130,
            Easing.OutQuint);
        text.FadeColour(
            selected ? Color4.White : HomeControlColours.Navy,
            130,
            Easing.OutQuint);
        accent.FadeTo(selected ? 1 : 0, 130, Easing.OutQuint);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!selected)
            background.FadeColour(SettingsTheme.PaleCyan, 100, Easing.OutQuint);

        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        if (!selected)
            background.FadeColour(Color4.White, 120, Easing.OutQuint);
    }
}

internal partial class GameplayBindingCard : ClickableContainer
{
    private readonly Bindable<Key> binding;
    private readonly Box background;
    private readonly SpriteText laneText;
    private readonly SpriteText keyText;
    private readonly SpriteText actionText;
    private readonly bool compact;
    private bool capturing;
    private bool pressed;

    public GameplayBindingCard(
        int lane,
        Bindable<Key> binding,
        Action action,
        float width,
        bool compact = false)
    {
        this.binding = binding;
        this.compact = compact;
        Action = action;
        Size = new Vector2(width, compact ? 62 : 140);
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1.2f;
        BorderColour = SettingsTheme.Divider;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SettingsTheme.PaleCyan,
            },
            laneText = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = compact ? 4 : 13,
                Text = YokkoStrings.Get(
                    "settings.gameplay.lane",
                    lane + 1),
                Font = HomeTypography.Body(compact ? 10 : 14),
                Colour = SettingsTheme.MutedNavy,
            },
            keyText = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Y = compact ? 5 : -3,
                Font = HomeTypography.Display(compact ? 18 : 28),
                Colour = HomeControlColours.Navy,
            },
            actionText = new SpriteText
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = compact ? -3 : -14,
                Text = YokkoStrings.Get(
                    "settings.gameplay.click_to_change"),
                Font = HomeTypography.Body(compact ? 9 : 14),
                Colour = SettingsTheme.MutedNavy,
            },
        };

        binding.BindValueChanged(onBindingChanged, true);
    }

    public void SetCapturing(bool isCapturing)
    {
        capturing = isCapturing;
        background.FadeColour(
            capturing || pressed
                ? HomeControlColours.Navy
                : SettingsTheme.PaleCyan,
            140,
            Easing.OutQuint);
        laneText.FadeColour(
            capturing ? SettingsTheme.StatusCyan : SettingsTheme.MutedNavy,
            120,
            Easing.OutQuint);
        keyText.Text = capturing
            ? YokkoStrings.Get("settings.gameplay.press_key")
            : displayKey(binding.Value);
        keyText.Font = HomeTypography.Display(
            capturing
                ? compact ? 9 : 15
                : compact ? 18 : 28);
        keyText.FadeColour(
            capturing || pressed ? Color4.White : HomeControlColours.Navy,
            120,
            Easing.OutQuint);
        actionText.Text = YokkoStrings.Get(capturing
            ? "settings.gameplay.esc_cancel"
            : "settings.gameplay.click_to_change");
        actionText.FadeColour(
            capturing ? SettingsTheme.StatusCyan : SettingsTheme.MutedNavy,
            120,
            Easing.OutQuint);

        if (capturing)
            this.ScaleTo(1.035f, 130, Easing.OutQuint);
        else
            this.ScaleTo(1, 130, Easing.OutQuint);
    }

    public void SetPressed(bool isPressed)
    {
        pressed = isPressed;
        if (capturing)
            return;

        background.FadeColour(
            pressed ? HomeControlColours.Navy : SettingsTheme.PaleCyan,
            80,
            Easing.OutQuint);
        laneText.FadeColour(
            pressed ? SettingsTheme.StatusCyan : SettingsTheme.MutedNavy,
            80,
            Easing.OutQuint);
        keyText.FadeColour(
            pressed ? Color4.White : HomeControlColours.Navy,
            80,
            Easing.OutQuint);
        actionText.Text = YokkoStrings.Get(pressed
            ? "settings.gameplay.input_active"
            : "settings.gameplay.click_to_change");
        actionText.FadeColour(
            pressed ? SettingsTheme.StatusCyan : SettingsTheme.MutedNavy,
            80,
            Easing.OutQuint);
        this.ScaleTo(pressed ? 1.025f : 1, 80, Easing.OutQuint);
    }

    public void SetPreviewKey(Key key)
    {
        capturing = false;
        background.FadeColour(
            SettingsTheme.PaleCyan,
            120,
            Easing.OutQuint);
        laneText.FadeColour(
            SettingsTheme.MutedNavy,
            120,
            Easing.OutQuint);
        keyText.Text = displayKey(key);
        keyText.Font = HomeTypography.Display(compact ? 18 : 28);
        keyText.FadeColour(
            HomeControlColours.Navy,
            120,
            Easing.OutQuint);
        actionText.Text = YokkoStrings.Get(
            "settings.gameplay.sequence_captured");
        actionText.FadeColour(
            HomeControlColours.Pink,
            120,
            Easing.OutQuint);
        this.ScaleTo(1, 120, Easing.OutQuint);
    }

    public void ShowDuplicate()
    {
        this.FlashColour(
            HomeControlColours.Pink,
            260,
            Easing.OutQuint);
        this.ScaleTo(1.05f, 80, Easing.OutQuint)
            .Then()
            .ScaleTo(1.035f, 110, Easing.OutQuint);
    }

    public void ShowSwap()
    {
        actionText.Text = YokkoStrings.Get(
            "settings.gameplay.key_swapped");
        actionText.FadeColour(HomeControlColours.Pink, 100);
        this.FlashColour(
            HomeControlColours.Yellow,
            420,
            Easing.OutQuint);
        Scheduler.AddDelayed(() =>
        {
            if (capturing || pressed)
                return;

            actionText.Text = YokkoStrings.Get(
                "settings.gameplay.click_to_change");
            actionText.FadeColour(SettingsTheme.MutedNavy, 120);
        }, 1200);
    }

    private void onBindingChanged(ValueChangedEvent<Key> change)
    {
        if (!capturing)
            keyText.Text = displayKey(change.NewValue);
    }

    private static string displayKey(Key key) =>
        KeyModeBindings.FormatKey(key).ToUpperInvariant();

    protected override bool OnHover(HoverEvent e)
    {
        if (!capturing && !pressed)
            background.FadeColour(SettingsTheme.StatusCyan, 110, Easing.OutQuint);

        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        if (!capturing && !pressed)
            background.FadeColour(SettingsTheme.PaleCyan, 130, Easing.OutQuint);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            binding.ValueChanged -= onBindingChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayCompactButton : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteText text;
    private readonly SpriteIcon icon;
    private bool isSelected;
    private bool isEnabled = true;
    private bool hasFocus;

    public override bool AcceptsFocus => isEnabled;

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            isSelected = value;
            refresh();
        }
    }

    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            isEnabled = value;
            refresh();
        }
    }

    public GameplayCompactButton(
        LocalisableString label,
        Action action,
        float width,
        IconUsage? itemIcon = null)
    {
        Action = action;
        Size = new Vector2(width, 42);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1.2f;
        BorderColour = SettingsTheme.Divider;

        background = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = Color4.White,
        };
        var children = new List<Drawable> { background };

        if (itemIcon.HasValue)
        {
            children.Add(icon = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 14,
                Size = new Vector2(13),
                Icon = itemIcon.Value,
                Colour = HomeControlColours.Pink,
            });
        }

        children.Add(text = new SpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            X = itemIcon.HasValue ? 8 : 0,
            Text = label,
            Font = HomeTypography.Display(14),
            Colour = HomeControlColours.Navy,
        });

        InternalChildren = children.ToArray();
        refresh();
    }

    public void SetText(LocalisableString label) => text.Text = label;

    private void refresh()
    {
        if (background == null)
            return;

        background.Colour = isSelected
            ? HomeControlColours.Navy
            : Color4.White;
        text.Colour = !isEnabled
            ? SettingsTheme.MutedNavy
            : isSelected
                ? Color4.White
                : HomeControlColours.Navy;
        Alpha = isEnabled ? 1 : 0.55f;
        BorderColour = hasFocus
            ? HomeControlColours.Pink
            : SettingsTheme.Divider;
        BorderThickness = hasFocus ? 2.4f : 1.2f;

        if (icon != null)
        {
            icon.Colour = !isEnabled
                ? SettingsTheme.MutedNavy
                : isSelected
                    ? SettingsTheme.StatusCyan
                    : HomeControlColours.Pink;
        }
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (isEnabled && !isSelected)
            background.FadeColour(SettingsTheme.PaleCyan, 100, Easing.OutQuint);

        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        if (isEnabled && !isSelected)
            background.FadeColour(Color4.White, 120, Easing.OutQuint);
    }

    protected override bool OnClick(ClickEvent e)
    {
        if (!isEnabled)
            return true;

        return base.OnClick(e);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (isEnabled && e.Key is Key.Enter or Key.Space)
        {
            Action?.Invoke();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        hasFocus = true;
        refresh();
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        hasFocus = false;
        refresh();
    }
}

internal partial class GameplayValueStepper : CompositeDrawable
{
    private readonly Bindable<double> value;
    private readonly double step;
    private readonly double minimum;
    private readonly double maximum;
    private readonly Func<double, string> formatter;
    private readonly SpriteText valueText;

    public GameplayValueStepper(
        Bindable<double> value,
        double step,
        double minimum,
        double maximum,
        Func<double, string> formatter)
    {
        this.value = value;
        this.step = step;
        this.minimum = minimum;
        this.maximum = maximum;
        this.formatter = formatter;
        Size = new Vector2(390, 54);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1.4f;
        BorderColour = HomeControlColours.Navy;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            createButton(FontAwesome.Solid.Minus, Anchor.CentreLeft, -step),
            valueText = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Font = HomeTypography.Display(19),
                Colour = HomeControlColours.Navy,
            },
            createButton(FontAwesome.Solid.Plus, Anchor.CentreRight, step),
        };

        value.BindValueChanged(onValueChanged, true);
    }

    private Drawable createButton(
        IconUsage itemIcon,
        Anchor anchor,
        double delta) => new GameplayStepperButton(
        itemIcon,
        anchor,
        () =>
        {
            double next = Math.Clamp(value.Value + delta, minimum, maximum);
            value.Value = Math.Round(next / step) * step;
        });

    private void onValueChanged(ValueChangedEvent<double> change) =>
        valueText.Text = formatter(change.NewValue);

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            value.ValueChanged -= onValueChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayJudgementModeSelector : CompositeDrawable
{
    private readonly Bindable<JudgementMode> mode;
    private readonly SettingsSegmentedChoiceButton yokkoButton;
    private readonly SettingsSegmentedChoiceButton etternaButton;

    public GameplayJudgementModeSelector(
        Bindable<JudgementMode> mode)
    {
        this.mode = mode;
        Size = new Vector2(800, 54);

        InternalChild = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Children = new Drawable[]
            {
                yokkoButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.judgement_yokko"),
                    FontAwesome.Solid.Gamepad,
                    () => mode.Value = JudgementMode.Yokko,
                    400),
                etternaButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.judgement_etterna"),
                    FontAwesome.Solid.Bullseye,
                    () => mode.Value = JudgementMode.Etterna,
                    400),
            },
        };

        mode.BindValueChanged(onModeChanged, true);
    }

    private void onModeChanged(
        ValueChangedEvent<JudgementMode> change)
    {
        yokkoButton.SetSelected(change.NewValue == JudgementMode.Yokko);
        etternaButton.SetSelected(
            change.NewValue == JudgementMode.Etterna);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            mode.ValueChanged -= onModeChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayStepperButton : ClickableContainer
{
    private readonly Box background;
    private readonly Box focusLine;

    public override bool AcceptsFocus => true;

    public GameplayStepperButton(
        IconUsage itemIcon,
        Anchor anchor,
        Action action)
    {
        Anchor = anchor;
        Origin = anchor;
        Width = 68;
        RelativeSizeAxes = Axes.Y;
        Action = action;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Transparent,
            },
            new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(16),
                Icon = itemIcon,
                Colour = HomeControlColours.Pink,
            },
            focusLine = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Height = 3,
                Colour = HomeControlColours.Pink,
                Alpha = 0,
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(SettingsTheme.PaleCyan, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        background.FadeColour(Color4.Transparent, 120, Easing.OutQuint);

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            Action?.Invoke();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        focusLine.FadeIn(100, Easing.OutQuint);
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        focusLine.FadeOut(100, Easing.OutQuint);
    }
}

internal partial class GameplayInlineToggle : ClickableContainer
{
    private readonly BindableBool value;
    private readonly Box switchTrack;
    private readonly Circle switchThumb;
    private readonly SpriteText stateText;
    private readonly SpriteText titleText;

    public override bool AcceptsFocus => true;

    public GameplayInlineToggle(
        LocalisableString title,
        LocalisableString note,
        BindableBool value)
    {
        this.value = value;
        Action = () => value.Value = !value.Value;

        InternalChildren = new Drawable[]
        {
            titleText = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = title,
                Font = HomeTypography.Display(15),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 142,
                Text = note,
                Font = HomeTypography.Body(13),
                Colour = SettingsTheme.MutedNavy,
            },
            new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -82,
                Size = new Vector2(48, 24),
                Masking = true,
                CornerRadius = 12,
                Children = new Drawable[]
                {
                    switchTrack = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SettingsTheme.Divider,
                    },
                    switchThumb = new Circle
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        X = 12,
                        Size = new Vector2(18),
                        Colour = Color4.White,
                    },
                },
            },
            stateText = new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Font = HomeTypography.Display(13),
                Colour = HomeControlColours.Navy,
            },
        };

        value.BindValueChanged(onValueChanged, true);
    }

    private void onValueChanged(ValueChangedEvent<bool> change)
    {
        switchTrack.FadeColour(
            change.NewValue ? HomeControlColours.Navy : SettingsTheme.Divider,
            120,
            Easing.OutQuint);
        switchThumb.MoveToX(
            change.NewValue ? 36 : 12,
            120,
            Easing.OutQuint);
        stateText.Text = YokkoStrings.Get(change.NewValue
            ? "settings.gameplay.enabled"
            : "settings.gameplay.disabled");
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            TriggerClick();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        titleText.FadeColour(HomeControlColours.Pink, 100);
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        titleText.FadeColour(HomeControlColours.Navy, 100);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            value.ValueChanged -= onValueChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayToggleCard : ClickableContainer
{
    private readonly BindableBool value;
    private readonly Box background;
    private readonly Box switchTrack;
    private readonly Circle switchThumb;
    private readonly SpriteText stateText;

    public override bool AcceptsFocus => true;

    public GameplayToggleCard(
        LocalisableString title,
        LocalisableString note,
        IconUsage itemIcon,
        BindableBool value)
    {
        this.value = value;
        Action = () => value.Value = !value.Value;
        Size = new Vector2(406, 84);
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1.2f;
        BorderColour = SettingsTheme.Divider;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new Circle
            {
                Position = new Vector2(16, 14),
                Size = new Vector2(34),
                Colour = SettingsTheme.PaleCyan,
            },
            new SpriteIcon
            {
                Position = new Vector2(24, 22),
                Size = new Vector2(18),
                Icon = itemIcon,
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(60, 13),
                Text = title,
                Font = HomeTypography.Display(16),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(60, 42),
                Text = note,
                Font = HomeTypography.Body(13),
                Colour = SettingsTheme.MutedNavy,
            },
            new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -18,
                Y = -10,
                Size = new Vector2(48, 24),
                Masking = true,
                CornerRadius = 12,
                Children = new Drawable[]
                {
                    switchTrack = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SettingsTheme.Divider,
                    },
                    switchThumb = new Circle
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        X = 12,
                        Size = new Vector2(18),
                        Colour = Color4.White,
                    },
                },
            },
            stateText = new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -18,
                Y = 20,
                Font = HomeTypography.Display(13),
                Colour = HomeControlColours.Navy,
            },
        };

        value.BindValueChanged(onValueChanged, true);
    }

    private void onValueChanged(ValueChangedEvent<bool> change)
    {
        switchTrack.FadeColour(
            change.NewValue ? HomeControlColours.Navy : SettingsTheme.Divider,
            120,
            Easing.OutQuint);
        switchThumb.MoveToX(
            change.NewValue ? 36 : 12,
            120,
            Easing.OutQuint);
        stateText.Text = YokkoStrings.Get(change.NewValue
            ? "settings.gameplay.enabled"
            : "settings.gameplay.disabled");
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(SettingsTheme.PaleCyan, 110, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        background.FadeColour(Color4.White, 130, Easing.OutQuint);

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            Action?.Invoke();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        BorderColour = HomeControlColours.Pink;
        BorderThickness = 2.4f;
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        BorderColour = SettingsTheme.Divider;
        BorderThickness = 1.2f;
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            value.ValueChanged -= onValueChanged;

        base.Dispose(isDisposing);
    }
}
