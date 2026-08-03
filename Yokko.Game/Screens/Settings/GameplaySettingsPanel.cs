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
using osu.Framework.Input.Bindings;
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
    PlaybackRate,
    Judgement,
    Feedback,
}

internal partial class GameplaySettingsPanel : CompositeDrawable, ISettingsTransientUi
{
    private readonly YokkoGameplaySettings settings;
    private readonly YokkoAudioSettings audioSettings;
    private readonly List<GameplaySectionTab> sectionTabs = new();
    private readonly List<GameplayBindingCard> bindingCards = new();
    private readonly List<InputKey> sequentialKeys = new();
    private readonly HashSet<InputKey> pressedKeys = new();
    private readonly Clipboard clipboard;
    private readonly AudioSettingsTestPlayer calibrationPlayer;
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private SpriteText statusTitle;
    private SpriteText statusMetadata;
    private Circle statusIconBackground;
    private GameplayCompactButton calibrationButton;
    private readonly SettingsContentScrollContainer contentHost;
    private GameplayBindingCard capturingCard;
    private GameplayCompactButton captureToggleButton;
    private GameplayCompactButton resetBindingsButton;
    private GameplayEtternaJusticeControls etternaJusticeControls;
    private Container judgementNextGameNotice;
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
    private bool bmsProfileSelected;
    private bool bmsDoublePlayProfileSelected;
    private bool resetBindingsPending;
    private InputKey[] resetUndoSnapshot;
    private string resetProfileKey;
    private KeyMode selectedKeyMode = KeyMode.FourKey;

    internal GameplaySettingsSection CurrentSection { get; private set; } =
        GameplaySettingsSection.Input;

    internal KeyMode SelectedKeyMode => selectedKeyMode;

    internal bool IsBmsProfileSelected => bmsProfileSelected;

    internal bool IsBmsDoublePlayProfileSelected =>
        bmsProfileSelected && bmsDoublePlayProfileSelected;

    internal bool IsCapturingKey => capturingCard != null;

    internal bool IsSequentialCapture => sequentialCapture;

    internal bool IsResetBindingsPending =>
        resetBindingsPending && resetProfileKey == selectedProfileKey;

    internal bool CanUndoBindingReset =>
        resetUndoSnapshot != null && resetProfileKey == selectedProfileKey;

    internal int SequentialCaptureIndex => sequentialKeys.Count;

    internal int VisibleBindingCardCount => bindingCards.Count;

    internal double CurrentScrollSpeed => settings.ScrollSpeed.Value;

    internal ScrollSpeedAdjustmentMode CurrentScrollSpeedAdjustmentMode =>
        settings.ScrollSpeedAdjustmentMode.Value;

    internal ManiaScrollDirection CurrentScrollDirection =>
        settings.ScrollDirection.Value;

    internal double QuaverScrollRateNormalization =>
        settings.QuaverScrollRateNormalization.Value;

    internal JudgementMode CurrentJudgementMode =>
        settings.JudgementMode.Value;

    internal int CurrentEtternaJustice =>
        settings.GetJudgementConfiguration().EtternaJustice;

    internal bool IsEtternaJusticeControlEnabled =>
        etternaJusticeControls?.IsEnabled
        ?? settings.JudgementMode.Value == JudgementMode.Etterna;

    internal bool ShowsJudgementNextGameNotice =>
        judgementNextGameNotice != null;

    internal bool ShowLanePressFeedback =>
        settings.ShowLanePressFeedback.Value;

    internal bool ShowTimingBar => settings.ShowTimingBar.Value;

    internal bool KeysoundsEnabled => settings.KeysoundsEnabled.Value;

    internal bool MinesEnabled => settings.MinesEnabled.Value;

    internal bool PauseWhenUnfocused =>
        settings.PauseWhenUnfocused.Value;

    internal bool ResumeCountdownEnabled =>
        settings.ResumeCountdownEnabled.Value;

    internal double ResumeCountdownMilliseconds =>
        settings.ResumeCountdownMilliseconds.Value;

    internal AudioPitchMode ManualPlaybackRatePitchMode =>
        audioSettings.ManualPlaybackRatePitchMode.Value;

    internal bool IsCalibrationActive =>
        calibrationPreparing || calibrationSession != null;

    internal int CalibrationSampleCount =>
        calibrationSession?.SampleCount ?? 0;

    internal int PressedKeyCount => pressedKeys.Count;

    internal double ContentScrollableExtent =>
        contentHost?.ScrollableExtent ?? 0;

    internal double ContentScrollPosition => contentHost?.Current ?? 0;

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
            SettingsChrome.CreateHeader(
                YokkoStrings.Get("settings.gameplay.title"),
                YokkoStrings.Get("settings.gameplay.subtitle"),
                FontAwesome.Solid.Gamepad,
                4),
            createStatusCard(),
            createSectionTabs(),
            contentHost = new SettingsContentScrollContainer()
            {
                Position = new Vector2(378, 320),
                Size = new Vector2(840, 328),
            },
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

        settings.BindingsChanged += onBindingsChanged;

        refreshStatusMetadata();
        showSection(CurrentSection, false);
    }

    internal void SelectSection(GameplaySettingsSection section) =>
        showSection(section, true);

    internal void ScrollContentBy(double offset) =>
        contentHost.ScrollBy(offset, false);

    internal void SelectKeyMode(KeyMode keyMode)
    {
        if (!bmsProfileSelected && keyMode == selectedKeyMode)
            return;

        cancelCapture();
        clearPressedKeys();
        resetBindingsPending = false;
        bmsProfileSelected = false;
        bmsDoublePlayProfileSelected = false;
        selectedKeyMode = keyMode;
        showInputSection(true);
        refreshStatusMetadata();
    }

    internal void SelectBmsProfile(bool doublePlay = false)
    {
        if (bmsProfileSelected
            && bmsDoublePlayProfileSelected == doublePlay)
            return;

        cancelCapture();
        clearPressedKeys();
        resetBindingsPending = false;
        bmsProfileSelected = true;
        bmsDoublePlayProfileSelected = doublePlay;
        showInputSection(true);
        refreshStatusMetadata();
    }

    internal void SelectAdjacentKeyMode(int direction)
    {
        if (direction == 0)
            return;

        int current = 0;
        if (!bmsProfileSelected)
        {
            for (int index = 0; index < settings.SupportedKeyModes.Count; index++)
            {
                if (settings.SupportedKeyModes[index] == selectedKeyMode)
                {
                    current = index;
                    break;
                }
            }
        }
        else
        {
            current = direction > 0
                ? -1
                : settings.SupportedKeyModes.Count;
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

        if (capturingCard == bindingCards[lane])
        {
            cancelCapture();
            return;
        }

        cancelCapture();
        capturingCard = bindingCards[lane];
        capturingCard.SetCapturing(true);
        keyCaptureHint.Text = YokkoStrings.Get(
            "settings.gameplay.capture_target",
            selectedLaneLabel(lane));
        refreshCaptureControls();
    }

    internal void BeginSequentialKeyCapture()
    {
        if (CurrentSection != GameplaySettingsSection.Input)
            showSection(GameplaySettingsSection.Input, false);

        if (capturingCard != null)
        {
            cancelCapture();
            return;
        }

        cancelCapture();
        sequentialCapture = true;
        sequentialKeys.Clear();
        capturingCard = bindingCards[0];
        capturingCard.SetCapturing(true);
        refreshSequentialHint();
        refreshCaptureControls();
    }

    internal void ResetSelectedBindings()
    {
        cancelCapture();
        if (CanUndoBindingReset)
        {
            InputKey[] snapshot = resetUndoSnapshot;
            resetUndoSnapshot = null;
            resetBindingsPending = false;
            setSelectedInputBindings(snapshot);
            showInputSection(false);
            showInputMessage(YokkoStrings.Get(
                "settings.gameplay.binding_reset_undone"));
            refreshStatusMetadata();
            return;
        }

        if (!IsResetBindingsPending)
        {
            resetProfileKey = selectedProfileKey;
            resetBindingsPending = true;
            showInputSection(false);
            showInputMessage(YokkoStrings.Get(
                "settings.gameplay.binding_reset_confirm"));
            return;
        }

        resetUndoSnapshot = getSelectedInputKeys().ToArray();
        resetBindingsPending = false;
        if (bmsProfileSelected)
        {
            if (bmsDoublePlayProfileSelected)
                settings.ResetBmsDoublePlayBindings();
            else
                settings.ResetBmsBindings();
        }
        else
            settings.ResetBindings(selectedKeyMode);
        showInputSection(false);
        showInputMessage(YokkoStrings.Get(
            "settings.gameplay.binding_reset_done"));
        refreshStatusMetadata();
    }

    internal void ApplyBindingPreset(GameplayKeyPreset preset)
    {
        cancelCapture();
        invalidateResetRecovery();
        if (bmsProfileSelected)
        {
            if (bmsDoublePlayProfileSelected)
                settings.ResetBmsDoublePlayBindings();
            else
                settings.ResetBmsBindings();
            showInputMessage(YokkoStrings.Get(
                "settings.gameplay.preset_applied",
                YokkoStrings.Get("settings.gameplay.preset_standard")));
            return;
        }
        settings.ApplyBindingPreset(selectedKeyMode, preset);
        showInputMessage(YokkoStrings.Get(
            "settings.gameplay.preset_applied",
            presetLabel(preset)));
    }

    internal void CopySelectedBindings()
    {
        cancelCapture();
        if (bmsProfileSelected)
            return;
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
            invalidateResetRecovery();
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

    internal void SetScrollTimeMilliseconds(double milliseconds) =>
        settings.SetScrollTimeMilliseconds(milliseconds);

    internal void SetScrollSpeedAdjustmentMode(
        ScrollSpeedAdjustmentMode mode) =>
        settings.ScrollSpeedAdjustmentMode.Value = mode;

    internal void SetScrollDirection(ManiaScrollDirection direction) =>
        settings.ScrollDirection.Value = direction;

    internal void SetJudgementMode(JudgementMode mode) =>
        settings.JudgementMode.Value = mode;

    internal void SetEtternaJustice(int justice) =>
        settings.SetEtternaJustice(justice);

    internal Key GetBinding(KeyMode keyMode, int lane) =>
        settings.GetKeys(keyMode)[lane];

    internal InputKey GetInputBinding(KeyMode keyMode, int lane) =>
        settings.GetInputKeys(keyMode)[lane];

    internal InputKey GetBmsInputBinding(int lane) =>
        settings.GetBmsInputKeys()[lane];

    internal InputKey GetBmsDoublePlayInputBinding(int lane) =>
        settings.GetBmsDoublePlayInputKeys()[lane];

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

    internal void SetResumeCountdownEnabled(bool enabled) =>
        settings.ResumeCountdownEnabled.Value = enabled;

    internal void SetResumeCountdownMilliseconds(double milliseconds) =>
        settings.ResumeCountdownMilliseconds.Value = milliseconds;

    internal void SetManualPlaybackRatePitchMode(AudioPitchMode mode) =>
        audioSettings.ManualPlaybackRatePitchMode.Value = mode;

    internal bool HandleKeyDown(Key key)
    {
        if (capturingCard != null)
        {
            if (key == Key.Escape)
            {
                cancelCapture();
                return true;
            }

            captureInput(KeyCombination.FromKey(key));
            return true;
        }

        if (key == Key.Escape && IsCalibrationActive)
        {
            cancelCalibration(true);
            return true;
        }

        InputKey inputKey = KeyCombination.FromKey(key);
        if (!pressedKeys.Add(inputKey))
            return findLane(inputKey) >= 0;

        int lane = findLane(inputKey);
        if (lane < 0)
        {
            refreshLiveInputStatus(inputKey, -1);
            return false;
        }

        if (lane < bindingCards.Count)
            bindingCards[lane].SetPressed(true);

        if (calibrationSession?.TryRecordTap(Time.Current) == true)
            refreshCalibrationStatus(Time.Current);
        else if (!IsCalibrationActive)
            refreshLiveInputStatus(inputKey, lane);

        return true;
    }

    internal void HandleKeyUp(Key key)
    {
        HandleInputUp(KeyCombination.FromKey(key));
    }

    internal bool HandleInputDown(InputKey key)
    {
        if (capturingCard != null)
        {
            captureInput(key);
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

    internal void HandleInputUp(InputKey key)
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

        if (IsResetBindingsPending)
        {
            resetBindingsPending = false;
            showInputSection(false);
            showInputMessage(YokkoStrings.Get(
                "settings.gameplay.binding_reset_cancelled"));
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
            160.8f);
        addTab(
            flow,
            GameplaySettingsSection.Timing,
            YokkoStrings.Get("settings.gameplay.section_timing"),
            FontAwesome.Solid.WaveSquare,
            160.8f);
        addTab(
            flow,
            GameplaySettingsSection.PlaybackRate,
            YokkoStrings.Get("settings.gameplay.section_playback_rate"),
            FontAwesome.Solid.Bolt,
            160.8f);
        addTab(
            flow,
            GameplaySettingsSection.Judgement,
            YokkoStrings.Get("settings.gameplay.section_judgement"),
            FontAwesome.Solid.Bullseye,
            160.8f);
        addTab(
            flow,
            GameplaySettingsSection.Feedback,
            YokkoStrings.Get("settings.gameplay.section_feedback"),
            FontAwesome.Solid.Heartbeat,
            160.8f);

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

            case GameplaySettingsSection.PlaybackRate:
                setContent(createPlaybackRateSection(), animate);
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
            Font = HomeTypography.Body(16),
            Colour = SettingsTheme.MutedNavy,
        };
        var children = new List<Drawable>();
        children.AddRange(new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(20, 18),
                Text = YokkoStrings.Get("settings.gameplay.key_profile"),
                Font = HomeTypography.Display(20),
                Colour = HomeControlColours.Navy,
            },
            new GameplayCompactButton(
                "‹",
                () => SelectAdjacentKeyMode(-1),
                42)
            {
                Position = new Vector2(104, 10),
            },
            new GameplayProfileBadge(
                bmsProfileSelected
                    ? "BMS"
                    : OsuManiaKeyLayout.GetDisplayName(selectedKeyMode),
                106)
            {
                Position = new Vector2(152, 10),
            },
            new GameplayCompactButton(
                "›",
                () => SelectAdjacentKeyMode(1),
                42)
            {
                Position = new Vector2(264, 10),
            },
            new GameplayCompactButton(
                "4K",
                () => SelectKeyMode(KeyMode.FourKey),
                46)
            {
                Position = new Vector2(318, 10),
                IsSelected = !bmsProfileSelected
                             && selectedKeyMode == KeyMode.FourKey,
            },
            new GameplayCompactButton(
                "7K",
                () => SelectKeyMode(KeyMode.SevenKey),
                46)
            {
                Position = new Vector2(370, 10),
                IsSelected = !bmsProfileSelected
                             && selectedKeyMode == KeyMode.SevenKey,
            },
            new GameplayCompactButton(
                "BMS",
                () => SelectBmsProfile(),
                72)
            {
                Position = new Vector2(422, 10),
                IsSelected = bmsProfileSelected,
            },
            captureToggleButton = new GameplayCompactButton(
                YokkoStrings.Get("settings.gameplay.edit_all"),
                BeginSequentialKeyCapture,
                112,
                FontAwesome.Solid.Keyboard)
            {
                Position = new Vector2(566, 10),
            },
            resetBindingsButton = new GameplayCompactButton(
                resetButtonLabel(),
                ResetSelectedBindings,
                132,
                CanUndoBindingReset
                    ? FontAwesome.Solid.Undo
                    : FontAwesome.Solid.ArrowLeft)
            {
                Position = new Vector2(688, 10),
                IsSelected = IsResetBindingsPending,
            },
            keyCaptureHint,
            createBindingCards(),
        });

        if (bmsProfileSelected)
        {
            children.AddRange(createBmsModeControls());
        }
        else if (selectedKeyMode is KeyMode.FourKey or KeyMode.SevenKey)
        {
            children.AddRange(createPresetControls());
        }
        else
        {
            children.Add(new SpriteText
            {
                Position = new Vector2(20, 70),
                Text = YokkoStrings.Get("settings.gameplay.all_modes_hint"),
                Font = HomeTypography.Body(16),
                Colour = SettingsTheme.MutedNavy,
            });
        }

        children.AddRange(createProfileTransferControls(
            !bmsProfileSelected
            && selectedKeyMode is KeyMode.FourKey or KeyMode.SevenKey));
        setPanelChildren(panel, children);

        setContent(panel, animate);
    }

    private IEnumerable<Drawable> createBmsModeControls()
    {
        yield return new SpriteText
        {
            Position = new Vector2(20, 70),
            Text = YokkoStrings.Get("settings.gameplay.bms_mode"),
            Font = HomeTypography.Display(17),
            Colour = HomeControlColours.Navy,
        };
        yield return new GameplayCompactButton(
            YokkoStrings.Get("settings.gameplay.bms_single_play"),
            () => SelectBmsProfile(),
            110)
        {
            Position = new Vector2(112, 58),
            IsSelected = !bmsDoublePlayProfileSelected,
        };
        yield return new GameplayCompactButton(
            YokkoStrings.Get("settings.gameplay.bms_double_play"),
            () => SelectBmsProfile(doublePlay: true),
            110)
        {
            Position = new Vector2(228, 58),
            IsSelected = bmsDoublePlayProfileSelected,
        };
        yield return new SpriteText
        {
            Position = new Vector2(354, 70),
            Text = YokkoStrings.Get("settings.gameplay.bms_layout_note"),
            Font = HomeTypography.Body(14),
            Colour = SettingsTheme.MutedNavy,
        };
    }

    private IEnumerable<Drawable> createPresetControls()
    {
        yield return new SpriteText
        {
            Position = new Vector2(20, 70),
            Text = YokkoStrings.Get("settings.gameplay.presets"),
            Font = HomeTypography.Display(17),
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
            YokkoStrings.Get(
                "settings.gameplay.copy_to_mode",
                selectedKeyMode == KeyMode.FourKey ? "7K" : "4K"),
            CopySelectedBindings,
            140)
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
            96)
        {
            Position = new Vector2(exportX, 58),
        };
        yield return new GameplayCompactButton(
            YokkoStrings.Get("settings.gameplay.import_profile"),
            () => ImportKeyProfiles(),
            96)
        {
            Position = new Vector2(exportX + 106, 58),
        };
    }

    private Drawable createBindingCards()
    {
        IReadOnlyList<Bindable<Key>> bindings = getSelectedKeyboardBindings();
        IReadOnlyList<Bindable<InputKey>> deviceBindings =
            getSelectedDeviceBindings();
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
                    deviceBindings[lane],
                    () => BeginKeyCapture(capturedLane),
                    width,
                    rowCount > 1,
                    selectedLaneLabel(lane),
                    bmsProfileSelected
                    && (lane == 0
                        || bmsDoublePlayProfileSelected && lane == 8));
                card.Height = rowCount == 1 ? 132 : 62;
                if (pressedKeys.Contains(getSelectedInputKeys()[lane]))
                    card.SetPressed(true);
                bindingCards.Add(card);
                flow.Add(card);
            }
        }

        return host;
    }

    private string selectedProfileKey => bmsProfileSelected
        ? bmsDoublePlayProfileSelected ? "BMS-DP" : "BMS-SP"
        : $"{(int)selectedKeyMode}K";

    private LocalisableString selectedProfileLabel => bmsProfileSelected
        ? bmsDoublePlayProfileSelected ? "BMS DP" : "BMS SP"
        : OsuManiaKeyLayout.GetDisplayName(selectedKeyMode);

    private LocalisableString selectedLaneLabel(int lane)
    {
        if (!bmsProfileSelected)
        {
            return YokkoStrings.Get(
                "settings.gameplay.lane",
                lane + 1);
        }

        if (!bmsDoublePlayProfileSelected)
        {
            return lane == 0
                ? YokkoStrings.Get("settings.gameplay.bms_scratch")
                : YokkoStrings.Get("settings.gameplay.bms_key", lane);
        }

        return lane % 8 == 0
            ? YokkoStrings.Get(
                "settings.gameplay.bms_stage_scratch",
                lane / 8 + 1)
            : YokkoStrings.Get(
                "settings.gameplay.bms_stage_key",
                lane / 8 + 1,
                lane % 8);
    }

    private LocalisableString resetButtonLabel() => YokkoStrings.Get(
        CanUndoBindingReset
            ? "settings.gameplay.undo_reset"
            : IsResetBindingsPending
                ? "settings.gameplay.confirm_reset"
                : "settings.gameplay.reset");

    private IReadOnlyList<Bindable<Key>> getSelectedKeyboardBindings() =>
        bmsProfileSelected
            ? bmsDoublePlayProfileSelected
                ? settings.BmsDoublePlayBindings
                : settings.BmsBindings
            : settings.GetBindableKeys(selectedKeyMode);

    private IReadOnlyList<Bindable<InputKey>> getSelectedDeviceBindings() =>
        bmsProfileSelected
            ? bmsDoublePlayProfileSelected
                ? settings.BmsDoublePlayDeviceBindings
                : settings.BmsDeviceBindings
            : settings.GetDeviceBindings(selectedKeyMode);

    private IReadOnlyList<InputKey> getSelectedInputKeys() =>
        bmsProfileSelected
            ? bmsDoublePlayProfileSelected
                ? settings.GetBmsDoublePlayInputKeys()
                : settings.GetBmsInputKeys()
            : settings.GetInputKeys(selectedKeyMode);

    private void setSelectedInputBinding(int lane, InputKey key)
    {
        if (bmsProfileSelected)
        {
            if (bmsDoublePlayProfileSelected)
                settings.SetBmsDoublePlayInputBinding(lane, key);
            else
                settings.SetBmsInputBinding(lane, key);
        }
        else
            settings.SetInputBinding(selectedKeyMode, lane, key);
    }

    private void setSelectedInputBindings(IReadOnlyList<InputKey> keys)
    {
        if (bmsProfileSelected)
        {
            if (bmsDoublePlayProfileSelected)
                settings.SetBmsDoublePlayInputBindings(keys);
            else
                settings.SetBmsInputBindings(keys);
        }
        else
            settings.SetInputBindings(selectedKeyMode, keys);
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
            new GameplayScrollSpeedSlider(
                settings.ScrollSpeed,
                value =>
                    $"{Math.Round(OsuManiaScrollSpeed.ComputeScrollTime(value)):0} ms  ·  "
                    + (settings.ScrollSpeedAdjustmentMode.Value
                        == ScrollSpeedAdjustmentMode.Milliseconds
                        ? $"{value:0.000}"
                        : $"{value:0}"),
                settings.ScrollSpeedAdjustmentMode,
                settings.AdjustScrollTimeMilliseconds)
            {
                Position = new Vector2(430, 14),
            },
            new SpriteText
            {
                Position = new Vector2(20, 91),
                Text = YokkoStrings.Get(
                    "settings.gameplay.speed_presets"),
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Navy,
            },
            createSpeedPresets(),
            createControlLabel(
                YokkoStrings.Get(
                    "settings.gameplay.scroll_direction"),
                YokkoStrings.Get(
                    "settings.gameplay.scroll_direction_note"),
                20,
                143),
            new GameplayScrollDirectionSelector(
                settings.ScrollDirection)
            {
                Position = new Vector2(430, 137),
            },
            createControlLabel(
                YokkoStrings.Get(
                    "settings.gameplay.quaver_rate_normalization"),
                YokkoStrings.Get(
                    "settings.gameplay.quaver_rate_normalization_note"),
                20,
                205),
            new GameplayValueStepper(
                settings.QuaverScrollRateNormalization,
                10,
                0,
                100,
                value => $"{value:0}%")
            {
                Position = new Vector2(430, 199),
            },
            createControlLabel(
                YokkoStrings.Get("settings.gameplay.input_offset"),
                YokkoStrings.Get(
                    "settings.gameplay.input_offset_note"),
                20,
                263),
            new GameplayValueStepper(
                audioSettings.UserOffsetMilliseconds,
                1,
                -200,
                200,
                value => $"{value:+0;-0;0} ms")
            {
                Position = new Vector2(430, 257),
            },
        });

        return panel;
    }

    private Drawable createPlaybackRateSection()
    {
        var panel = createPanel();
        setPanelChildren(panel, new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(20, 18),
                Text = YokkoStrings.Get(
                    "settings.gameplay.playback_rate_heading"),
                Font = HomeTypography.Display(21),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(20, 47),
                Text = YokkoStrings.Get(
                    "settings.gameplay.playback_rate_note"),
                Font = HomeTypography.Body(16),
                Colour = SettingsTheme.MutedNavy,
            },
            new GameplayRatePitchModeSelector(
                audioSettings.ManualPlaybackRatePitchMode)
            {
                Position = new Vector2(20, 82),
            },
            new Box
            {
                Position = new Vector2(20, 154),
                Size = new Vector2(800, 1),
                Colour = SettingsTheme.Divider,
            },
            createControlLabel(
                YokkoStrings.Get(
                    "settings.gameplay.playback_rate_shortcut"),
                YokkoStrings.Get(
                    "settings.gameplay.playback_rate_shortcut_note"),
                20,
                172),
            createControlLabel(
                YokkoStrings.Get(
                    "settings.gameplay.playback_rate_mod_priority"),
                YokkoStrings.Get(
                    "settings.gameplay.playback_rate_mod_priority_note"),
                430,
                172),
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
                Font = HomeTypography.Display(21),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(20, 47),
                Text = YokkoStrings.Get(
                    "settings.gameplay.judgement_note"),
                Font = HomeTypography.Body(16),
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
            etternaJusticeControls = new GameplayEtternaJusticeControls(
                settings.JudgementMode,
                settings.EtternaJustice,
                value => Math.Round(value)
                         == JudgementConfiguration.MaximumEtternaJustice
                    ? "Justice · J9"
                    : $"J{Math.Round(value):0}")
            {
                Position = new Vector2(20, 157),
            },
            judgementNextGameNotice = new Container
            {
                Position = new Vector2(20, 274),
                Size = new Vector2(800, 36),
                Masking = true,
                CornerRadius = 6,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(1f, 0.84f, 0.2f, 0.18f),
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Position = new Vector2(12, 0),
                        Size = new Vector2(14),
                        Icon = FontAwesome.Solid.Clock,
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Position = new Vector2(36, 0),
                        Text = YokkoStrings.Get(
                            "settings.gameplay.judgement_apply_next_game"),
                        Font = HomeTypography.Body(14),
                        Colour = HomeControlColours.Navy,
                    },
                },
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
                Font = HomeTypography.Display(21),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(20, 47),
                Text = YokkoStrings.Get(
                    "settings.gameplay.feedback_note"),
                Font = HomeTypography.Body(16),
                Colour = SettingsTheme.MutedNavy,
            },
            new FillFlowContainer
            {
                Position = new Vector2(20, 74),
                Size = new Vector2(800, 76),
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
                        settings.ShowLanePressFeedback,
                        76),
                    new GameplayToggleCard(
                        YokkoStrings.Get(
                            "settings.gameplay.mines"),
                        YokkoStrings.Get(
                            "settings.gameplay.mines_note"),
                        FontAwesome.Solid.Bomb,
                        settings.MinesEnabled,
                        76),
                },
            },
            new FillFlowContainer
            {
                Position = new Vector2(20, 156),
                Size = new Vector2(800, 76),
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
                        settings.KeysoundsEnabled,
                        76),
                    new GameplayToggleCard(
                        YokkoStrings.Get(
                            "settings.gameplay.pause_when_unfocused"),
                        YokkoStrings.Get(
                            "settings.gameplay.pause_when_unfocused_note"),
                        FontAwesome.Solid.PauseCircle,
                        settings.PauseWhenUnfocused,
                        76),
                },
            },
            new GameplayInlineToggle(
                YokkoStrings.Get(
                    "settings.gameplay.show_timing_bar"),
                YokkoStrings.Get(
                    "settings.gameplay.show_timing_bar_note"),
                settings.ShowTimingBar)
            {
                Position = new Vector2(20, 240),
                Size = new Vector2(800, 26),
            },
            new GameplayCountdownSettingRow(
                YokkoStrings.Get(
                    "settings.gameplay.resume_countdown"),
                YokkoStrings.Get(
                    "settings.gameplay.resume_countdown_note"),
                settings.ResumeCountdownEnabled,
                settings.ResumeCountdownMilliseconds)
            {
                Position = new Vector2(20, 272),
                Size = new Vector2(800, 26),
            },
        });

        return panel;
    }

    private static Container createPanel(float height = 328) => new()
    {
        RelativeSizeAxes = Axes.X,
        Height = height,
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
        contentHost.ScrollToStart(false);

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
                Font = HomeTypography.Display(20),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Text = note,
                Font = HomeTypography.Body(16),
                Colour = SettingsTheme.MutedNavy,
            },
        },
    };

    private Drawable createStatusCard() => SettingsChrome.CreateStickerFrame(new Container
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
                        Font = HomeTypography.Display(24),
                        Colour = HomeControlColours.Navy,
                    },
                    statusMetadata = new SpriteText
                    {
                        Font = HomeTypography.Body(17),
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
    });

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
        IReadOnlyList<InputKey> selectedInputs = getSelectedInputKeys();
        statusMetadata.Text = selectedInputs.Count <= 8
            ? YokkoStrings.Get(
                "settings.gameplay.selected_profile_ready",
                selectedProfileLabel,
                formatSelectedInputKeys())
            : YokkoStrings.Get(
                "settings.gameplay.selected_profile_ready_many",
                selectedProfileLabel,
                selectedInputs.Count);
        calibrationButton.SetText(YokkoStrings.Get(
            "settings.gameplay.calibration_start"));
        statusIconBackground.FadeColour(Color4.White, 120);
    }

    private string formatSelectedInputKeys() =>
        string.Join("  ", getSelectedInputKeys().Select(binding =>
            KeyModeBindings.FormatKey(binding).ToUpperInvariant()));

    private void onBindingsChanged() => refreshStatusMetadata();

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

        refreshCaptureControls();
    }

    private void refreshSequentialHint()
    {
        keyCaptureHint.Text = YokkoStrings.Get(
            "settings.gameplay.sequence_hint",
            sequentialKeys.Count + 1,
            bindingCards.Count,
            selectedLaneLabel(sequentialKeys.Count));
    }

    private void refreshCaptureControls()
    {
        if (captureToggleButton == null)
            return;

        bool isCapturing = capturingCard != null;
        captureToggleButton.SetText(YokkoStrings.Get(isCapturing
            ? "settings.gameplay.cancel_capture"
            : "settings.gameplay.edit_all"));
        captureToggleButton.SetIcon(isCapturing
            ? FontAwesome.Solid.Times
            : FontAwesome.Solid.Keyboard);
        captureToggleButton.IsSelected = isCapturing;
    }

    private void captureInput(InputKey key)
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
                invalidateResetRecovery();
                setSelectedInputBindings(sequentialKeys);
                sequentialCapture = false;
                capturingCard = null;

                foreach (GameplayBindingCard card in bindingCards)
                    card.SetCapturing(false);

                refreshCaptureControls();

                keyCaptureHint.Text = bmsProfileSelected
                    ? YokkoStrings.Get(
                        bmsDoublePlayProfileSelected
                            ? "settings.gameplay.bms_dp_profile_saved"
                            : "settings.gameplay.bms_profile_saved",
                        formatSelectedInputKeys())
                    : YokkoStrings.Get(
                        "settings.gameplay.sequence_saved",
                        (int)selectedKeyMode,
                        formatSelectedInputKeys());
                return;
            }

            capturingCard = bindingCards[sequentialKeys.Count];
            capturingCard.SetCapturing(true);
            refreshSequentialHint();
            return;
        }

        int lane = bindingCards.IndexOf(capturingCard);
        IReadOnlyList<InputKey> bindings = getSelectedInputKeys();
        int duplicateLane = bindings
                            .Select((binding, index) => (binding, index))
                            .Where(entry =>
                                entry.index != lane
                                && entry.binding == key)
                            .Select(entry => entry.index)
                            .DefaultIfEmpty(-1)
                            .First();

        GameplayBindingCard capturedCard = capturingCard;
        invalidateResetRecovery();
        setSelectedInputBinding(lane, key);
        capturingCard = null;
        capturedCard.SetCapturing(false);
        refreshCaptureControls();

        if (duplicateLane >= 0)
        {
            capturedCard.ShowSwap();
            bindingCards[duplicateLane].ShowSwap();
            keyCaptureHint.Text = YokkoStrings.Get(
                "settings.gameplay.key_swap_notice",
                KeyModeBindings.FormatKey(key).ToUpperInvariant(),
                selectedLaneLabel(duplicateLane),
                selectedLaneLabel(lane));
        }
        else
        {
            keyCaptureHint.Text = YokkoStrings.Get(
                "settings.gameplay.single_saved",
                selectedLaneLabel(lane),
                KeyModeBindings.FormatKey(key).ToUpperInvariant());
        }
    }

    private int findLane(InputKey key)
    {
        IReadOnlyList<InputKey> bindings = getSelectedInputKeys();
        for (int lane = 0; lane < bindings.Count; lane++)
        {
            if (bindings[lane] == key)
                return lane;
        }

        return -1;
    }

    private void refreshLiveInputStatus(InputKey key, int lane)
    {
        statusTitle.Text = lane >= 0
            ? YokkoStrings.Get(
                "settings.gameplay.input_detected",
                KeyModeBindings.FormatKey(key).ToUpperInvariant(),
                selectedLaneLabel(lane))
            : YokkoStrings.Get(
                "settings.gameplay.input_unbound",
                KeyModeBindings.FormatKey(key).ToUpperInvariant());
        statusMetadata.Text = YokkoStrings.Get(
            "settings.gameplay.input_chord",
            pressedKeys.Count,
            getSelectedInputKeys().Count);
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

    private void invalidateResetRecovery()
    {
        resetBindingsPending = false;
        resetUndoSnapshot = null;
        resetProfileKey = null;
        refreshResetControl();
    }

    private void refreshResetControl()
    {
        if (resetBindingsButton == null)
            return;

        resetBindingsButton.SetText(resetButtonLabel());
        resetBindingsButton.SetIcon(CanUndoBindingReset
            ? FontAwesome.Solid.Undo
            : FontAwesome.Solid.ArrowLeft);
        resetBindingsButton.IsSelected = IsResetBindingsPending;
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
            settings.BindingsChanged -= onBindingsChanged;
        }

        base.Dispose(isDisposing);
    }
}

internal partial class SettingsContentScrollContainer
    : ScrollContainer<Drawable>
{
    public SettingsContentScrollContainer()
        : base(Direction.Vertical)
    {
        ScrollbarOverlapsContent = true;
        ClampExtension = 0;
    }

    protected override ScrollbarContainer CreateScrollbar(
        Direction direction) => new SettingsScrollbar(direction);

    protected override bool OnScroll(ScrollEvent e)
    {
        if (ScrollableExtent <= 0.5)
        {
            ScrollToStart(false);
            return false;
        }

        return base.OnScroll(e);
    }

    private partial class SettingsScrollbar : ScrollbarContainer
    {
        private const float thickness = 4;

        public SettingsScrollbar(Direction direction)
            : base(direction)
        {
            Alpha = 0.55f;
            Colour = HomeControlColours.Cyan;
            CornerRadius = thickness / 2;
            Masking = true;
            Margin = new MarginPadding
            {
                Left = direction == Direction.Vertical ? 3 : 0,
                Right = direction == Direction.Vertical ? 3 : 0,
                Top = direction == Direction.Horizontal ? 3 : 0,
                Bottom = direction == Direction.Horizontal ? 3 : 0,
            };
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
            };
            ResizeTo(1);
        }

        public override void ResizeTo(
            float value,
            int duration = 0,
            Easing easing = Easing.None)
        {
            var size = new Vector2(thickness)
            {
                [(int)ScrollDirection] = value,
            };
            this.ResizeTo(size, duration, easing);
        }

        protected override bool OnHover(HoverEvent e)
        {
            this.FadeTo(0.95f, 100, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e) =>
            this.FadeTo(0.55f, 120, Easing.OutQuint);
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
                Font = HomeTypography.Display(18),
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
    private readonly Bindable<Key> keyboardBinding;
    private readonly Bindable<InputKey> deviceBinding;
    private readonly Box background;
    private readonly SpriteText laneText;
    private readonly SpriteText keyText;
    private readonly SpriteText actionText;
    private readonly bool compact;
    private readonly float captureKeyFontSize;
    private readonly float idleKeyFontSize;
    private readonly Color4 idleBorderColour;
    private readonly Color4 idleLaneColour;
    private bool capturing;
    private bool hasFocus;
    private bool pressed;

    public override bool AcceptsFocus => true;

    public GameplayBindingCard(
        int lane,
        Bindable<Key> keyboardBinding,
        Bindable<InputKey> deviceBinding,
        Action action,
        float width,
        bool compact = false,
        LocalisableString? customLaneLabel = null,
        bool isScratchLane = false)
    {
        this.keyboardBinding = keyboardBinding;
        this.deviceBinding = deviceBinding;
        this.compact = compact;
        idleKeyFontSize = compact || width < 120
            ? width < 85 ? 17 : 19
            : 30;
        captureKeyFontSize = compact || width < 120 ? 10 : 17;
        Action = action;
        Size = new Vector2(width, compact ? 62 : 140);
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1.2f;
        BorderColour = idleBorderColour = isScratchLane
            ? HomeControlColours.Pink
            : SettingsTheme.Divider;
        idleLaneColour = isScratchLane
            ? HomeControlColours.Pink
            : SettingsTheme.MutedNavy;

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
                Text = customLaneLabel ?? YokkoStrings.Get(
                    "settings.gameplay.lane",
                    lane + 1),
                Font = HomeTypography.Body(
                    compact ? 11 : width < 120 ? 13 : 16),
                Colour = idleLaneColour,
            },
            keyText = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Y = compact ? 5 : -3,
                Font = HomeTypography.Display(idleKeyFontSize),
                Colour = HomeControlColours.Navy,
            },
            actionText = new SpriteText
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = compact ? -3 : -14,
                Text = YokkoStrings.Get(
                    "settings.gameplay.click_to_change"),
                Font = HomeTypography.Body(
                    compact ? 10 : width < 120 ? 12 : 16),
                Colour = SettingsTheme.MutedNavy,
            },
        };

        keyboardBinding.BindValueChanged(onKeyboardBindingChanged, true);
        deviceBinding.BindValueChanged(onDeviceBindingChanged, true);
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
            capturing ? SettingsTheme.StatusCyan : idleLaneColour,
            120,
            Easing.OutQuint);
        keyText.Text = capturing
            ? YokkoStrings.Get("settings.gameplay.press_key")
            : displayKey(currentBinding);
        keyText.Font = HomeTypography.Display(
            capturing ? captureKeyFontSize : idleKeyFontSize);
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
            pressed ? SettingsTheme.StatusCyan : idleLaneColour,
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

    public void SetPreviewKey(InputKey key)
    {
        capturing = false;
        background.FadeColour(
            SettingsTheme.PaleCyan,
            120,
            Easing.OutQuint);
        laneText.FadeColour(
            idleLaneColour,
            120,
            Easing.OutQuint);
        keyText.Text = displayKey(key);
        keyText.Font = HomeTypography.Display(idleKeyFontSize);
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

    private InputKey currentBinding => deviceBinding.Value != InputKey.None
        ? deviceBinding.Value
        : KeyCombination.FromKey(keyboardBinding.Value);

    private void onKeyboardBindingChanged(ValueChangedEvent<Key> _)
    {
        if (!capturing)
            keyText.Text = displayKey(currentBinding);
    }

    private void onDeviceBindingChanged(ValueChangedEvent<InputKey> _)
    {
        if (!capturing)
            keyText.Text = displayKey(currentBinding);
    }

    private static string displayKey(InputKey key) =>
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
        hasFocus = true;
        refreshFocusBorder();
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        hasFocus = false;
        refreshFocusBorder();
    }

    private void refreshFocusBorder()
    {
        BorderColour = hasFocus ? HomeControlColours.Navy : idleBorderColour;
        BorderThickness = hasFocus ? 2.4f : 1.2f;
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            keyboardBinding.ValueChanged -= onKeyboardBindingChanged;
            deviceBinding.ValueChanged -= onDeviceBindingChanged;
        }

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayProfileBadge : CompositeDrawable
{
    public GameplayProfileBadge(LocalisableString label, float width)
    {
        Size = new Vector2(width, 42);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1.2f;
        BorderColour = HomeControlColours.Navy;
        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SettingsTheme.PaleCyan,
            },
            new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = label,
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Navy,
            },
        };
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
            Font = HomeTypography.Control(16),
            Colour = HomeControlColours.Navy,
        });

        InternalChildren = children.ToArray();
        refresh();
    }

    public void SetText(LocalisableString label) => text.Text = label;

    public void SetIcon(IconUsage itemIcon)
    {
        if (icon != null)
            icon.Icon = itemIcon;
    }

    private void refresh()
    {
        if (background == null)
            return;

        background.ClearTransforms();
        text.ClearTransforms();
        icon?.ClearTransforms();

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

internal partial class GameplayScrollSpeedSlider : CompositeDrawable
{
    private const float track_x = 18;
    private const float track_width = 354;
    private const float track_y = 40;

    private readonly Bindable<double> value;
    private readonly Func<double, string> formatter;
    private readonly Bindable<ScrollSpeedAdjustmentMode> adjustmentMode;
    private readonly Action<double> adjustScrollTime;
    private readonly Box track;
    private readonly Box fill;
    private readonly Circle knob;
    private readonly SpriteText valueText;

    public override bool AcceptsFocus => true;

    internal GameplayScrollSpeedSlider(
        Bindable<double> value,
        Func<double, string> formatter,
        Bindable<ScrollSpeedAdjustmentMode> adjustmentMode,
        Action<double> adjustScrollTime,
        bool placeModeBelow = false)
    {
        this.value = value;
        this.formatter = formatter;
        this.adjustmentMode = adjustmentMode;
        this.adjustScrollTime = adjustScrollTime;
        Size = new Vector2(390, 54);

        var modeButton = new GameplayStepperModeButton(
            adjustmentMode,
            placeModeBelow)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Position = placeModeBelow
                ? new Vector2(-2, 60)
                : new Vector2(-12, 7),
            Size = placeModeBelow
                ? new Vector2(148, 30)
                : new Vector2(112, 18),
        };

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(0, 4),
                Size = new Vector2(390, 50),
                Masking = true,
                CornerRadius = 8,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.015f, 0.045f, 0.28f, 0.2f),
                },
            },
            new Container
            {
                Position = new Vector2(-1.5f, -1.5f),
                Size = new Vector2(393, 57),
                Masking = true,
                CornerRadius = 8,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.4f),
                },
            },
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 7,
                BorderThickness = 1.6f,
                BorderColour = HomeControlColours.Navy,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
            },
            valueText = new SpriteText
            {
                Position = new Vector2(track_x, 8),
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Navy,
            },
            track = new Box
            {
                Position = new Vector2(track_x, track_y),
                Size = new Vector2(track_width, 5),
                Colour = SettingsTheme.Divider,
            },
            fill = new Box
            {
                Position = new Vector2(track_x, track_y),
                Height = 5,
                Colour = HomeControlColours.Pink,
            },
            knob = new Circle
            {
                Origin = Anchor.Centre,
                Position = new Vector2(track_x, track_y + 2.5f),
                Size = new Vector2(15),
                Colour = Color4.White,
                BorderThickness = 2.5f,
                BorderColour = HomeControlColours.Pink,
            },
            modeButton,
        };

        value.BindValueChanged(onValueChanged, true);
        adjustmentMode.BindValueChanged(onAdjustmentModeChanged, true);
    }

    internal static double ValueFromProgress(
        double progress,
        ScrollSpeedAdjustmentMode mode)
    {
        double raw = OsuManiaScrollSpeed.Minimum
                     + Math.Clamp(progress, 0, 1)
                     * (OsuManiaScrollSpeed.Maximum
                        - OsuManiaScrollSpeed.Minimum);
        double clamped = OsuManiaScrollSpeed.Clamp(raw);
        return mode == ScrollSpeedAdjustmentMode.Milliseconds
            ? clamped
            : OsuManiaScrollSpeed.SnapToWholeStep(clamped);
    }

    internal static double AdjustForScroll(
        double currentValue,
        float scrollDelta) =>
        OsuManiaScrollSpeed.AdjustWholeStep(
            currentValue,
            Math.Sign(scrollDelta) * OsuManiaScrollSpeed.ShortcutStep);

    internal static double FineScrollTimeDeltaForDirection(double direction) =>
        -Math.Sign(direction)
        * OsuManiaScrollSpeed.ScrollTimeStepMilliseconds;

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left)
            return false;

        Vector2 local = ToLocalSpace(e.ScreenSpaceMousePosition);
        if (local.Y < 28)
            return false;

        updateFrom(local.X);
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e) => true;

    protected override void OnDrag(DragEvent e) =>
        updateFrom(ToLocalSpace(e.ScreenSpaceMousePosition).X);

    protected override bool OnScroll(ScrollEvent e)
    {
        if (e.ScrollDelta.Y == 0)
            return false;

        if (adjustmentMode.Value == ScrollSpeedAdjustmentMode.Milliseconds)
            adjustScrollTime(
                FineScrollTimeDeltaForDirection(e.ScrollDelta.Y));
        else
            value.Value = AdjustForScroll(value.Value, e.ScrollDelta.Y);

        return true;
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        int direction = e.Key switch
        {
            Key.Left or Key.Down => -1,
            Key.Right or Key.Up => 1,
            _ => 0,
        };

        if (e.Key == Key.Home)
            value.Value = OsuManiaScrollSpeed.Minimum;
        else if (e.Key == Key.End)
            value.Value = OsuManiaScrollSpeed.Maximum;
        else if (direction != 0)
        {
            if (adjustmentMode.Value == ScrollSpeedAdjustmentMode.Milliseconds)
                adjustScrollTime(FineScrollTimeDeltaForDirection(direction));
            else
                value.Value = OsuManiaScrollSpeed.Adjust(
                    value.Value,
                    direction * OsuManiaScrollSpeed.ShortcutStep);
        }
        else
            return base.OnKeyDown(e);

        return true;
    }

    protected override bool OnHover(HoverEvent e)
    {
        track.FadeColour(SettingsTheme.PaleCyan, 100, Easing.OutQuint);
        knob.ScaleTo(1.18f, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        track.FadeColour(SettingsTheme.Divider, 120, Easing.OutQuint);
        knob.ScaleTo(1, 120, Easing.OutQuint);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        valueText.FadeColour(HomeControlColours.Pink, 100, Easing.OutQuint);
        knob.BorderColour = HomeControlColours.Cyan;
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        valueText.FadeColour(HomeControlColours.Navy, 100, Easing.OutQuint);
        knob.BorderColour = HomeControlColours.Pink;
    }

    private void updateFrom(float localX) =>
        value.Value = ValueFromProgress(
            (localX - track_x) / track_width,
            adjustmentMode.Value);

    private void onValueChanged(ValueChangedEvent<double> change)
    {
        float progress = (float)(
            (change.NewValue - OsuManiaScrollSpeed.Minimum)
            / (OsuManiaScrollSpeed.Maximum - OsuManiaScrollSpeed.Minimum));
        fill.Width = progress * track_width;
        knob.X = track_x + progress * track_width;
        valueText.Text = formatter(change.NewValue);
    }

    private void onAdjustmentModeChanged(
        ValueChangedEvent<ScrollSpeedAdjustmentMode> change)
    {
        if (change.NewValue == ScrollSpeedAdjustmentMode.OsuManiaScale)
        {
            value.Value = OsuManiaScrollSpeed.SnapToWholeStep(value.Value);
        }

        valueText.Text = formatter(value.Value);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            value.ValueChanged -= onValueChanged;
            adjustmentMode.ValueChanged -= onAdjustmentModeChanged;
        }

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayValueStepper : CompositeDrawable
{
    private readonly Bindable<double> value;
    private readonly double step;
    private readonly double minimum;
    private readonly double maximum;
    private readonly Func<double, string> formatter;
    private readonly Action<double> adjustValue;
    private readonly Bindable<ScrollSpeedAdjustmentMode> adjustmentMode;
    private readonly Action<double> alternateAdjustValue;
    private readonly Func<double, string> alternateFormatter;
    private readonly GameplayStepperButton decreaseButton;
    private readonly GameplayStepperButton increaseButton;
    private readonly GameplayStepperModeButton modeButton;
    private readonly SpriteText valueText;
    private bool isEnabled = true;

    internal bool IsEnabled => isEnabled;

    public GameplayValueStepper(
        Bindable<double> value,
        double step,
        double minimum,
        double maximum,
        Func<double, string> formatter,
        Action<double> adjustValue = null,
        Bindable<ScrollSpeedAdjustmentMode> adjustmentMode = null,
        Action<double> alternateAdjustValue = null,
        Func<double, string> alternateFormatter = null)
    {
        this.value = value;
        this.step = step;
        this.minimum = minimum;
        this.maximum = maximum;
        this.formatter = formatter;
        this.adjustValue = adjustValue;
        this.adjustmentMode = adjustmentMode;
        this.alternateAdjustValue = alternateAdjustValue;
        this.alternateFormatter = alternateFormatter;
        Size = new Vector2(390, 54);

        decreaseButton = createButton(
            FontAwesome.Solid.Minus,
            Anchor.CentreLeft,
            -step);
        valueText = new SpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Y = adjustmentMode == null ? 0 : -7,
                Font = HomeTypography.Display(20),
            Colour = HomeControlColours.Navy,
        };
        increaseButton = createButton(
            FontAwesome.Solid.Plus,
            Anchor.CentreRight,
            step);

        var children = new List<Drawable>
        {
            new Container
            {
                Position = new Vector2(0, 4),
                Size = new Vector2(390, 50),
                Masking = true,
                CornerRadius = 8,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.015f, 0.045f, 0.28f, 0.2f),
                },
            },
            new Container
            {
                Position = new Vector2(-1.5f, -1.5f),
                Size = new Vector2(393, 57),
                Masking = true,
                CornerRadius = 8,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.4f),
                },
            },
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 7,
                BorderThickness = 1.6f,
                BorderColour = HomeControlColours.Navy,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
            },
            decreaseButton,
            valueText,
            increaseButton,
        };

        if (adjustmentMode != null)
        {
            if (alternateAdjustValue == null || alternateFormatter == null)
            {
                throw new ArgumentException(
                    "An alternate scroll-speed mode requires an adjuster and formatter.");
            }

            children.Add(modeButton = new GameplayStepperModeButton(
                adjustmentMode));
            adjustmentMode.BindValueChanged(onAdjustmentModeChanged);
        }

        InternalChildren = children.ToArray();
        value.BindValueChanged(onValueChanged, true);
    }

    internal void SetEnabled(bool enabled)
    {
        if (isEnabled == enabled)
            return;

        isEnabled = enabled;
        decreaseButton.SetEnabled(enabled);
        increaseButton.SetEnabled(enabled);
        modeButton?.SetEnabled(enabled);
    }

    private GameplayStepperButton createButton(
        IconUsage itemIcon,
        Anchor anchor,
        double delta) => new GameplayStepperButton(
        itemIcon,
        anchor,
        () =>
        {
            if (adjustmentMode?.Value
                == ScrollSpeedAdjustmentMode.Milliseconds)
            {
                alternateAdjustValue(delta);
                return;
            }

            if (adjustValue != null)
            {
                adjustValue(delta);
                return;
            }

            double next = Math.Clamp(value.Value + delta, minimum, maximum);
            value.Value = Math.Round(next / step) * step;
        });

    private void onValueChanged(ValueChangedEvent<double> change) =>
        valueText.Text = activeFormatter(change.NewValue);

    private void onAdjustmentModeChanged(
        ValueChangedEvent<ScrollSpeedAdjustmentMode> _) =>
        valueText.Text = activeFormatter(value.Value);

    private string activeFormatter(double currentValue) =>
        adjustmentMode?.Value == ScrollSpeedAdjustmentMode.Milliseconds
            ? alternateFormatter(currentValue)
            : formatter(currentValue);

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            value.ValueChanged -= onValueChanged;
            if (adjustmentMode != null)
            {
                adjustmentMode.ValueChanged -=
                    onAdjustmentModeChanged;
            }
        }

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayStepperModeButton : ClickableContainer
{
    private readonly Bindable<ScrollSpeedAdjustmentMode> mode;
    private readonly bool prominent;
    private readonly Box background;
    private readonly Box switchTrack;
    private readonly Circle switchThumb;
    private readonly SpriteText text;
    private bool isEnabled = true;

    public override bool AcceptsFocus => isEnabled;

    internal ScrollSpeedAdjustmentMode DisplayedMode => mode.Value;
    internal bool IsFineAdjustmentEnabled =>
        mode.Value == ScrollSpeedAdjustmentMode.Milliseconds;

    public GameplayStepperModeButton(
        Bindable<ScrollSpeedAdjustmentMode> mode,
        bool prominent = false)
    {
        this.mode = mode;
        this.prominent = prominent;
        Anchor = prominent ? Anchor.TopRight : Anchor.BottomCentre;
        Origin = prominent ? Anchor.TopRight : Anchor.BottomCentre;
        Y = prominent ? 0 : -2;
        Size = prominent
            ? new Vector2(148, 30)
            : new Vector2(124, 22);
        Masking = true;
        CornerRadius = prominent ? 15 : 5;
        BorderThickness = prominent ? 1.5f : 1;
        BorderColour = prominent
            ? HomeControlColours.Navy
            : SettingsTheme.Divider;
        Action = toggleMode;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SettingsTheme.PaleCyan,
            },
            text = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = prominent ? 13 : 7,
                Text = YokkoStrings.Get(
                    "settings.general.fine_adjustment"),
                Font = HomeTypography.Control(prominent ? 16 : 14),
                Colour = HomeControlColours.Navy,
            },
            new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = prominent ? -7 : -5,
                Size = prominent
                    ? new Vector2(46, 22)
                    : new Vector2(32, 16),
                Masking = true,
                CornerRadius = prominent ? 11 : 8,
                BorderThickness = prominent ? 1.5f : 1,
                BorderColour = HomeControlColours.Navy,
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
                        X = prominent ? 11 : 8,
                        Size = new Vector2(prominent ? 16 : 11),
                        Colour = Color4.White,
                    },
                },
            },
        };

        mode.BindValueChanged(onModeChanged, true);
    }

    internal void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
        Alpha = enabled ? 1 : 0.55f;
    }

    private void toggleMode()
    {
        if (!isEnabled)
            return;

        mode.Value = mode.Value == ScrollSpeedAdjustmentMode.OsuManiaScale
            ? ScrollSpeedAdjustmentMode.Milliseconds
            : ScrollSpeedAdjustmentMode.OsuManiaScale;
    }

    private void onModeChanged(
        ValueChangedEvent<ScrollSpeedAdjustmentMode> change)
    {
        bool milliseconds =
            change.NewValue == ScrollSpeedAdjustmentMode.Milliseconds;
        background.Colour = milliseconds
            ? SettingsTheme.StatusCyan
            : prominent ? Color4.White : SettingsTheme.PaleCyan;
        switchTrack.Colour = milliseconds
            ? HomeControlColours.Navy
            : SettingsTheme.Divider;
        switchThumb.MoveToX(
            milliseconds
                ? prominent ? 35 : 24
                : prominent ? 11 : 8,
            180,
            Easing.OutBack);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (isEnabled)
            background.FadeColour(Color4.White, 100, Easing.OutQuint);

        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        bool milliseconds =
            mode.Value == ScrollSpeedAdjustmentMode.Milliseconds;
        background.FadeColour(
            milliseconds
                ? SettingsTheme.StatusCyan
                : prominent ? Color4.White : SettingsTheme.PaleCyan,
            120,
            Easing.OutQuint);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (isEnabled && e.Key is Key.Enter or Key.Space)
        {
            toggleMode();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            mode.ValueChanged -= onModeChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayEtternaJusticeControls : CompositeDrawable
{
    private readonly Bindable<JudgementMode> mode;
    private readonly GameplayValueStepper stepper;

    internal bool IsEnabled { get; private set; }

    public GameplayEtternaJusticeControls(
        Bindable<JudgementMode> mode,
        Bindable<double> value,
        Func<double, string> formatter)
    {
        this.mode = mode;
        Size = new Vector2(800, 104);

        InternalChildren = new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(0, 5),
                Text = YokkoStrings.Get(
                    "settings.gameplay.etterna_justice"),
                Font = HomeTypography.Display(19),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(0, 34),
                Text = YokkoStrings.Get(
                    "settings.gameplay.etterna_justice_note"),
                Font = HomeTypography.Body(15),
                Colour = SettingsTheme.MutedNavy,
            },
            stepper = new GameplayValueStepper(
                value,
                1,
                JudgementConfiguration.MinimumEtternaJustice,
                JudgementConfiguration.MaximumEtternaJustice,
                formatter)
            {
                Position = new Vector2(410, 0),
            },
            new SpriteText
            {
                Position = new Vector2(0, 79),
                Text = YokkoStrings.Get(
                    "settings.gameplay.etterna_boundaries"),
                Font = HomeTypography.Body(15),
                Colour = SettingsTheme.MutedNavy,
            },
        };

        mode.BindValueChanged(onModeChanged, true);
    }

    private void onModeChanged(ValueChangedEvent<JudgementMode> change)
    {
        IsEnabled = change.NewValue == JudgementMode.Etterna;
        stepper.SetEnabled(IsEnabled);
        this.FadeTo(IsEnabled ? 1 : 0.42f, 120, Easing.OutQuint);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            mode.ValueChanged -= onModeChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayJudgementModeSelector : CompositeDrawable
{
    private readonly Bindable<JudgementMode> mode;
    private readonly SettingsSegmentedChoiceButton lazerButton;
    private readonly SettingsSegmentedChoiceButton stableButton;
    private readonly SettingsSegmentedChoiceButton etternaButton;
    private readonly SettingsSegmentedChoiceButton bmsButton;

    public GameplayJudgementModeSelector(
        Bindable<JudgementMode> mode)
    {
        this.mode = mode;
        Size = new Vector2(800, 54);

        var card = new SettingsStickerCard(new Vector2(800, 54), 8);
        card.SetContent(new FillFlowContainer
        {
            RelativeSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Children = new Drawable[]
            {
                lazerButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.judgement_yokko"),
                    FontAwesome.Solid.Gamepad,
                    () => mode.Value = JudgementMode.Yokko,
                    800f / 4),
                stableButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.judgement_osu_stable"),
                    FontAwesome.Solid.Clock,
                    () => mode.Value = JudgementMode.OsuStable,
                    800f / 4),
                etternaButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.judgement_etterna"),
                    FontAwesome.Solid.Bullseye,
                    () => mode.Value = JudgementMode.Etterna,
                    800f / 4),
                bmsButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.judgement_bms_beatoraja"),
                    FontAwesome.Solid.CompactDisc,
                    () => mode.Value = JudgementMode.BmsBeatoraja,
                    800f / 4),
            },
        });
        InternalChild = card;

        mode.BindValueChanged(onModeChanged, true);
    }

    private void onModeChanged(
        ValueChangedEvent<JudgementMode> change)
    {
        lazerButton.SetSelected(change.NewValue == JudgementMode.Yokko);
        stableButton.SetSelected(
            change.NewValue == JudgementMode.OsuStable);
        etternaButton.SetSelected(
            change.NewValue == JudgementMode.Etterna);
        bmsButton.SetSelected(
            change.NewValue == JudgementMode.BmsBeatoraja);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            mode.ValueChanged -= onModeChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayScrollDirectionSelector : CompositeDrawable
{
    private readonly Bindable<ManiaScrollDirection> direction;
    private readonly SettingsSegmentedChoiceButton downscrollButton;
    private readonly SettingsSegmentedChoiceButton upscrollButton;

    public GameplayScrollDirectionSelector(
        Bindable<ManiaScrollDirection> direction)
    {
        this.direction = direction;
        Size = new Vector2(390, 54);

        var card = new SettingsStickerCard(new Vector2(390, 54), 8);
        card.SetContent(new FillFlowContainer
        {
            RelativeSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Children = new Drawable[]
            {
                downscrollButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.scroll_direction_down"),
                    FontAwesome.Solid.ChevronDown,
                    () => direction.Value =
                        ManiaScrollDirection.Downscroll,
                    195),
                upscrollButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.scroll_direction_up"),
                    FontAwesome.Solid.ChevronUp,
                    () => direction.Value =
                        ManiaScrollDirection.Upscroll,
                    195),
            },
        });
        InternalChild = card;

        direction.BindValueChanged(onDirectionChanged, true);
    }

    private void onDirectionChanged(
        ValueChangedEvent<ManiaScrollDirection> change)
    {
        downscrollButton.SetSelected(
            change.NewValue == ManiaScrollDirection.Downscroll);
        upscrollButton.SetSelected(
            change.NewValue == ManiaScrollDirection.Upscroll);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            direction.ValueChanged -= onDirectionChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayRatePitchModeSelector : CompositeDrawable
{
    private readonly Bindable<AudioPitchMode> mode;
    private readonly SettingsSegmentedChoiceButton doubleTimeButton;
    private readonly SettingsSegmentedChoiceButton nightcoreButton;

    public GameplayRatePitchModeSelector(
        Bindable<AudioPitchMode> mode)
    {
        this.mode = mode;
        Size = new Vector2(800, 54);

        var card = new SettingsStickerCard(new Vector2(800, 54), 8);
        card.SetContent(new FillFlowContainer
        {
            RelativeSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Children = new Drawable[]
            {
                doubleTimeButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.playback_rate_dt"),
                    FontAwesome.Solid.Clock,
                    () => mode.Value = AudioPitchMode.Preserve,
                    400),
                nightcoreButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.playback_rate_nc"),
                    FontAwesome.Solid.Bolt,
                    () => mode.Value = AudioPitchMode.ScaleWithRate,
                    400),
            },
        });
        InternalChild = card;

        mode.BindValueChanged(onModeChanged, true);
    }

    private void onModeChanged(
        ValueChangedEvent<AudioPitchMode> change)
    {
        doubleTimeButton.SetSelected(
            change.NewValue == AudioPitchMode.Preserve);
        nightcoreButton.SetSelected(
            change.NewValue == AudioPitchMode.ScaleWithRate);
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
    private readonly SpriteIcon icon;
    private bool isEnabled = true;

    public override bool AcceptsFocus => isEnabled;

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
            icon = new SpriteIcon
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

    public void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
        icon.FadeTo(enabled ? 1 : 0.7f, 100, Easing.OutQuint);
        focusLine.FadeOut(80, Easing.OutQuint);
        background.FadeColour(Color4.Transparent, 80, Easing.OutQuint);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (isEnabled)
            background.FadeColour(SettingsTheme.PaleCyan, 100, Easing.OutQuint);

        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        background.FadeColour(Color4.Transparent, 120, Easing.OutQuint);

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (isEnabled && e.Key is Key.Enter or Key.Space)
        {
            Action?.Invoke();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override bool OnClick(ClickEvent e)
    {
        if (!isEnabled)
            return true;

        return base.OnClick(e);
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
                Font = HomeTypography.Display(17),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 142,
                Text = note,
                Font = HomeTypography.Body(15),
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
                Font = HomeTypography.Display(15),
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
        BindableBool value,
        float height = 84)
    {
        this.value = value;
        Action = () => value.Value = !value.Value;
        Size = new Vector2(393, height);
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
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(60, 42),
                Text = note,
                Font = HomeTypography.Body(15),
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
                Font = HomeTypography.Display(15),
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

/// <summary>
/// Feedback-section row combining the resume-countdown toggle with a compact
/// duration stepper. Visually follows <see cref="GameplayInlineToggle"/> and
/// dims the stepper while the countdown is disabled.
/// </summary>
internal partial class GameplayCountdownSettingRow : ClickableContainer
{
    private readonly BindableBool enabled;
    private readonly Bindable<double> duration;
    private readonly Box switchTrack;
    private readonly Circle switchThumb;
    private readonly SpriteText stateText;
    private readonly SpriteText titleText;
    private readonly SpriteText valueText;
    private readonly Container stepperHost;

    public override bool AcceptsFocus => true;

    public GameplayCountdownSettingRow(
        LocalisableString title,
        LocalisableString note,
        BindableBool enabled,
        Bindable<double> duration)
    {
        this.enabled = enabled;
        this.duration = duration;
        Action = () => enabled.Value = !enabled.Value;

        InternalChildren = new Drawable[]
        {
            titleText = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = title,
                Font = HomeTypography.Display(17),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 142,
                Text = note,
                Font = HomeTypography.Body(15),
                Colour = SettingsTheme.MutedNavy,
            },
            stepperHost = new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -142,
                Size = new Vector2(136, 24),
                Children = new Drawable[]
                {
                    new GameplayCountdownStepButton(
                        FontAwesome.Solid.Minus,
                        Anchor.CentreLeft,
                        () => adjust(
                            -YokkoGameplaySettings
                                .ResumeCountdownStepMilliseconds)),
                    valueText = new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = HomeTypography.Display(15),
                        Colour = HomeControlColours.Navy,
                    },
                    new GameplayCountdownStepButton(
                        FontAwesome.Solid.Plus,
                        Anchor.CentreRight,
                        () => adjust(
                            YokkoGameplaySettings
                                .ResumeCountdownStepMilliseconds)),
                },
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
                Font = HomeTypography.Display(15),
                Colour = HomeControlColours.Navy,
            },
        };

        enabled.BindValueChanged(onEnabledChanged, true);
        duration.BindValueChanged(onDurationChanged, true);
    }

    internal void AdjustDuration(double delta) => adjust(delta);

    private void adjust(double delta)
    {
        if (!enabled.Value)
            return;

        double step =
            YokkoGameplaySettings.ResumeCountdownStepMilliseconds;
        double next = Math.Clamp(
            duration.Value + delta,
            YokkoGameplaySettings.MinimumResumeCountdownMilliseconds,
            YokkoGameplaySettings.MaximumResumeCountdownMilliseconds);
        duration.Value = Math.Round(next / step) * step;
    }

    private void onEnabledChanged(ValueChangedEvent<bool> change)
    {
        switchTrack.FadeColour(
            change.NewValue ? HomeControlColours.Navy : SettingsTheme.Divider,
            120,
            Easing.OutQuint);
        switchThumb.MoveToX(
            change.NewValue ? 36 : 12,
            120,
            Easing.OutQuint);
        stepperHost.FadeTo(
            change.NewValue ? 1 : 0.35f,
            120,
            Easing.OutQuint);
        stateText.Text = YokkoStrings.Get(change.NewValue
            ? "settings.gameplay.enabled"
            : "settings.gameplay.disabled");
    }

    private void onDurationChanged(ValueChangedEvent<double> change) =>
        valueText.Text = $"{change.NewValue:0} ms";

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
        {
            enabled.ValueChanged -= onEnabledChanged;
            duration.ValueChanged -= onDurationChanged;
        }

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayCountdownStepButton : ClickableContainer
{
    private readonly Box background;

    public GameplayCountdownStepButton(
        IconUsage itemIcon,
        Anchor anchor,
        Action action)
    {
        Anchor = anchor;
        Origin = anchor;
        Size = new Vector2(26, 24);
        Masking = true;
        CornerRadius = 6;
        BorderThickness = 1.2f;
        BorderColour = SettingsTheme.Divider;
        Action = action;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(11),
                Icon = itemIcon,
                Colour = HomeControlColours.Pink,
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(SettingsTheme.PaleCyan, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        background.FadeColour(Color4.White, 120, Easing.OutQuint);
}
