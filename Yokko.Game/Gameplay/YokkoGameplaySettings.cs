using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osuTK.Input;
using Yokko.Core.Gameplay;
using Yokko.Core.Scoring;

namespace Yokko.Game.Gameplay;

public enum GameplayKeyPreset
{
    Standard,
    LeftHanded,
    Split,
}

public enum ScrollSpeedAdjustmentMode
{
    OsuManiaScale,
    Milliseconds,
}

public enum ManiaScrollDirection
{
    Downscroll,
    Upscroll,
}

public enum ManiaShortcutAction
{
    PauseOrBack,
    ToggleLayoutEditorUi,
    SkipIntro,
    QuickRetry,
    DecreaseScrollSpeed,
    IncreaseScrollSpeed,
    MenuPrevious,
    MenuPreviousAlternate,
    MenuNext,
    MenuNextAlternate,
    Confirm,
    ConfirmAlternate,
    Retry,
    WatchReplay,
}

public readonly record struct ManiaShortcutBindingChange(
    ManiaShortcutAction Action,
    Key PreviousKey,
    Key NewKey,
    ManiaShortcutAction? SwappedAction);

/// <summary>
/// Application-owned gameplay preferences. Chart rules and scoring windows stay
/// in Yokko.Core; this type only owns player input and presentation choices.
/// </summary>
public sealed class YokkoGameplaySettings
{
    [Flags]
    private enum ShortcutContext
    {
        ActiveGameplay = 1,
        Intro = 2,
        PauseMenu = 4,
        Failure = 8,
        Results = 16,
        LayoutEditor = 32,
    }

    private static readonly ManiaShortcutAction[] supportedShortcutActions =
        Enum.GetValues<ManiaShortcutAction>();

    private static readonly Key[] defaultFourKeyBindings =
        OsuManiaKeyLayout.GetDefaultKeys(KeyMode.FourKey);

    private static readonly Key[] defaultSevenKeyBindings =
        OsuManiaKeyLayout.GetDefaultKeys(KeyMode.SevenKey);

    private static readonly IReadOnlyDictionary<
        (KeyMode Mode, GameplayKeyPreset Preset),
        Key[]> bindingPresets =
        new Dictionary<(KeyMode, GameplayKeyPreset), Key[]>
        {
            [(KeyMode.FourKey, GameplayKeyPreset.Standard)] =
                defaultFourKeyBindings,
            [(KeyMode.FourKey, GameplayKeyPreset.LeftHanded)] =
                new[] { Key.A, Key.S, Key.D, Key.F },
            [(KeyMode.FourKey, GameplayKeyPreset.Split)] =
                new[] { Key.Z, Key.X, Key.Period, Key.Slash },
            [(KeyMode.SevenKey, GameplayKeyPreset.Standard)] =
                defaultSevenKeyBindings,
            [(KeyMode.SevenKey, GameplayKeyPreset.LeftHanded)] =
                new[] { Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J },
            [(KeyMode.SevenKey, GameplayKeyPreset.Split)] =
                new[]
                {
                    Key.Z,
                    Key.X,
                    Key.C,
                    Key.Space,
                    Key.Comma,
                    Key.Period,
                    Key.Slash,
                },
        };

    private readonly Dictionary<KeyMode, Bindable<Key>[]> bindingsByMode;

    public IReadOnlyList<KeyMode> SupportedKeyModes =>
        OsuManiaKeyLayout.SupportedModes;

    public IReadOnlyList<Bindable<Key>> FourKeyBindings =>
        bindingsByMode[KeyMode.FourKey];

    public IReadOnlyList<Bindable<Key>> SevenKeyBindings =>
        bindingsByMode[KeyMode.SevenKey];

    internal event Action BindingsChanged;

    public readonly Bindable<double> ScrollSpeed =
        new(OsuManiaScrollSpeed.Default);

    public readonly Bindable<ScrollSpeedAdjustmentMode>
        ScrollSpeedAdjustmentMode =
            new(global::Yokko.Game.Gameplay
                .ScrollSpeedAdjustmentMode.OsuManiaScale);

    public readonly Bindable<ManiaScrollDirection> ScrollDirection =
        new(ManiaScrollDirection.Downscroll);

    /// <summary>
    /// Matches Quaver's optional playback-rate scroll normalization.
    /// Zero keeps the same real-time approach duration while 100% makes
    /// rate mods scale the visual approach together with chart time.
    /// </summary>
    public readonly Bindable<double> QuaverScrollRateNormalization =
        new(0);

    public readonly Bindable<JudgementMode> JudgementMode =
        new(Yokko.Core.Scoring.JudgementMode.Yokko);

    public readonly Bindable<double> EtternaJustice =
        new(JudgementConfiguration.DefaultEtternaJustice);

    public readonly BindableBool ShowLanePressFeedback = new(true);

    public readonly BindableBool ShowTimingBar = new(true);

    public const double MinimumJudgementDisplayDurationMilliseconds = 100;

    public const double MaximumJudgementDisplayDurationMilliseconds = 2000;

    public const double DefaultJudgementDisplayDurationMilliseconds = 400;

    public const double JudgementDisplayDurationStepMilliseconds = 50;

    public const double MinimumJudgementOpacity = 0.2;

    public const double MaximumJudgementOpacity = 1;

    public const double JudgementOpacityStep = 0.1;

    public readonly Bindable<double> JudgementDisplayDurationMilliseconds =
        new(DefaultJudgementDisplayDurationMilliseconds);

    public readonly Bindable<double> JudgementOpacity =
        new(MaximumJudgementOpacity);

    public readonly BindableBool ShowJudgementHitError = new(true);

    public const double MinimumLayoutOffset = -0.75;

    public const double MaximumLayoutOffset = 0.75;

    public const double MinimumPlayfieldWidthScale = 0.2;

    public const double MaximumPlayfieldWidthScale = 2.5;

    public const double MinimumLayoutScale = 0.25;

    public const double MaximumLayoutScale = 2.5;

    public const double MinimumPerformanceReadoutOffset = -1;

    public const double MaximumPerformanceReadoutOffset = 0;

    public const double MaximumTopCoverRatio = 0.75;

    public const double MaximumBottomCoverRatio = 0.5;

    public const double MinimumBackgroundDim = 0;

    public const double MaximumBackgroundDim = 1;

    public const double DefaultBackgroundDim = MaximumBackgroundDim;

    public const double BackgroundDimStep = 0.05;

    /// <summary>
    /// Normalised presentation offsets. These are relative to the gameplay
    /// viewport so a saved layout remains usable across resolutions.
    /// </summary>
    public readonly Bindable<double> LayoutPlayfieldOffsetX = new(0);

    public readonly Bindable<double> LayoutPlayfieldOffsetY = new(0);

    public readonly Bindable<double> LayoutHudOffsetX = new(0);

    public readonly Bindable<double> LayoutHudOffsetY = new(0);

    public readonly Bindable<double> LayoutPlayfieldWidthScale = new(1);

    public readonly Bindable<double> LayoutPlayfieldHeightScale = new(1);

    public readonly Bindable<double> LayoutHudScaleX = new(1);

    public readonly Bindable<double> LayoutHudScaleY = new(1);

    public readonly Bindable<double> LayoutAccuracyOffsetX = new(0);

    public readonly Bindable<double> LayoutAccuracyOffsetY = new(0);

    public readonly Bindable<double> LayoutAccuracyScaleX = new(1);

    public readonly Bindable<double> LayoutAccuracyScaleY = new(1);

    public readonly Bindable<double> LayoutProgressOffsetX = new(0);

    public readonly Bindable<double> LayoutProgressOffsetY = new(0);

    public readonly Bindable<double> LayoutProgressScaleX = new(1);

    public readonly Bindable<double> LayoutProgressScaleY = new(1);

    public readonly Bindable<double> LayoutTimingBarOffsetX = new(0);

    public readonly Bindable<double> LayoutTimingBarOffsetY = new(0);

    public readonly Bindable<double> LayoutTimingBarScaleX = new(1);

    public readonly Bindable<double> LayoutTimingBarScaleY = new(1);

    public readonly Bindable<double> LayoutComboOffsetX = new(0);

    public readonly Bindable<double> LayoutComboOffsetY = new(0);

    public readonly Bindable<double> LayoutComboScaleX = new(1);

    public readonly Bindable<double> LayoutComboScaleY = new(1);

    public readonly Bindable<double> LayoutJudgementOffsetX = new(0);

    public readonly Bindable<double> LayoutJudgementOffsetY = new(0);

    public readonly Bindable<double> LayoutJudgementScaleX = new(1);

    public readonly Bindable<double> LayoutJudgementScaleY = new(1);

    public readonly Bindable<double> LayoutPerformanceReadoutOffsetX = new(0);

    public readonly Bindable<double> LayoutPerformanceReadoutOffsetY = new(0);

    public readonly Bindable<double> ReplayControlsOffsetX = new(0);

    public readonly Bindable<double> ReplayControlsOffsetY = new(0);

    public readonly Bindable<double> LayoutTopCoverRatio = new(0);

    public readonly Bindable<double> LayoutBottomCoverRatio = new(0);

    public readonly Bindable<double> BackgroundDim = new(DefaultBackgroundDim);

    internal IEnumerable<GameplayHudLayoutSetting> HudLayoutSettings
    {
        get
        {
            yield return layoutSetting("playfieldOffsetX", LayoutPlayfieldOffsetX);
            yield return layoutSetting("playfieldOffsetY", LayoutPlayfieldOffsetY);
            yield return layoutSetting("hudOffsetX", LayoutHudOffsetX);
            yield return layoutSetting("hudOffsetY", LayoutHudOffsetY);
            yield return layoutSetting("playfieldWidthScale", LayoutPlayfieldWidthScale, MinimumPlayfieldWidthScale, MaximumPlayfieldWidthScale);
            yield return layoutSetting("playfieldHeightScale", LayoutPlayfieldHeightScale, MinimumLayoutScale, MaximumLayoutScale);
            yield return layoutSetting("hudScaleX", LayoutHudScaleX, MinimumLayoutScale, MaximumLayoutScale);
            yield return layoutSetting("hudScaleY", LayoutHudScaleY, MinimumLayoutScale, MaximumLayoutScale);
            yield return layoutSetting("accuracyOffsetX", LayoutAccuracyOffsetX);
            yield return layoutSetting("accuracyOffsetY", LayoutAccuracyOffsetY);
            yield return layoutSetting("accuracyScaleX", LayoutAccuracyScaleX, MinimumLayoutScale, MaximumLayoutScale);
            yield return layoutSetting("accuracyScaleY", LayoutAccuracyScaleY, MinimumLayoutScale, MaximumLayoutScale);
            yield return layoutSetting("progressOffsetX", LayoutProgressOffsetX);
            yield return layoutSetting("progressOffsetY", LayoutProgressOffsetY);
            yield return layoutSetting("progressScaleX", LayoutProgressScaleX, MinimumLayoutScale, MaximumLayoutScale);
            yield return layoutSetting("progressScaleY", LayoutProgressScaleY, MinimumLayoutScale, MaximumLayoutScale);
            yield return layoutSetting("timingBarOffsetX", LayoutTimingBarOffsetX);
            yield return layoutSetting("timingBarOffsetY", LayoutTimingBarOffsetY);
            yield return layoutSetting("timingBarScaleX", LayoutTimingBarScaleX, MinimumLayoutScale, MaximumLayoutScale);
            yield return layoutSetting("timingBarScaleY", LayoutTimingBarScaleY, MinimumLayoutScale, MaximumLayoutScale);
            yield return layoutSetting("comboOffsetX", LayoutComboOffsetX);
            yield return layoutSetting("comboOffsetY", LayoutComboOffsetY);
            yield return layoutSetting("comboScaleX", LayoutComboScaleX, MinimumLayoutScale, MaximumLayoutScale);
            yield return layoutSetting("comboScaleY", LayoutComboScaleY, MinimumLayoutScale, MaximumLayoutScale);
            yield return layoutSetting("judgementOffsetX", LayoutJudgementOffsetX);
            yield return layoutSetting("judgementOffsetY", LayoutJudgementOffsetY);
            yield return layoutSetting("judgementScaleX", LayoutJudgementScaleX, MinimumLayoutScale, MaximumLayoutScale);
            yield return layoutSetting("judgementScaleY", LayoutJudgementScaleY, MinimumLayoutScale, MaximumLayoutScale);
            yield return layoutSetting("performanceReadoutOffsetX", LayoutPerformanceReadoutOffsetX, MinimumPerformanceReadoutOffset, MaximumPerformanceReadoutOffset);
            yield return layoutSetting("performanceReadoutOffsetY", LayoutPerformanceReadoutOffsetY, MinimumPerformanceReadoutOffset, MaximumPerformanceReadoutOffset);
            yield return layoutSetting("replayControlsOffsetX", ReplayControlsOffsetX);
            yield return layoutSetting("replayControlsOffsetY", ReplayControlsOffsetY);
            yield return new GameplayHudLayoutSetting("topCoverRatio", LayoutTopCoverRatio, 0, MaximumTopCoverRatio);
            yield return new GameplayHudLayoutSetting("bottomCoverRatio", LayoutBottomCoverRatio, 0, MaximumBottomCoverRatio);
            yield return new GameplayHudLayoutSetting("backgroundDim", BackgroundDim, MinimumBackgroundDim, MaximumBackgroundDim);
        }
    }

    private static GameplayHudLayoutSetting layoutSetting(
        string name,
        Bindable<double> bindable,
        double minimum = MinimumLayoutOffset,
        double maximum = MaximumLayoutOffset) =>
        new(name, bindable, minimum, maximum);

    public readonly BindableBool KeysoundsEnabled = new(false);

    public readonly BindableBool MinesEnabled = new(true);

    public readonly BindableBool PauseWhenUnfocused = new(true);

    /// <summary>
    /// Minimum buffered resume countdown. Anything shorter reads as an
    /// instant resume rather than a readable countdown.
    /// </summary>
    public const double MinimumResumeCountdownMilliseconds = 300;

    public const double MaximumResumeCountdownMilliseconds = 3000;

    public const double DefaultResumeCountdownMilliseconds = 1050;

    public const double ResumeCountdownStepMilliseconds = 50;

    public readonly BindableBool ResumeCountdownEnabled = new(true);

    public readonly Bindable<double> ResumeCountdownMilliseconds =
        new(DefaultResumeCountdownMilliseconds);

    public readonly Bindable<Key> DecreaseScrollSpeedKey = new(Key.F3);

    public readonly Bindable<Key> IncreaseScrollSpeedKey = new(Key.F4);

    public readonly Bindable<Key> PauseOrBackKey = new(Key.Escape);

    public readonly Bindable<Key> ToggleLayoutEditorUiKey =
        new(Key.BackSlash);

    public readonly Bindable<Key> SkipIntroKey = new(Key.Space);

    public readonly Bindable<Key> QuickRetryKey = new(Key.Tilde);

    public readonly Bindable<Key> MenuPreviousKey = new(Key.Up);

    public readonly Bindable<Key> MenuPreviousAlternateKey = new(Key.W);

    public readonly Bindable<Key> MenuNextKey = new(Key.Down);

    public readonly Bindable<Key> MenuNextAlternateKey = new(Key.S);

    public readonly Bindable<Key> ConfirmKey = new(Key.Enter);

    public readonly Bindable<Key> ConfirmAlternateKey = new(Key.Space);

    public readonly Bindable<Key> RetryKey = new(Key.R);

    public readonly Bindable<Key> WatchReplayKey = new(Key.V);

    public IReadOnlyList<ManiaShortcutAction> SupportedShortcutActions =>
        supportedShortcutActions;

    public YokkoGameplaySettings()
    {
        bindingsByMode = OsuManiaKeyLayout.SupportedModes.ToDictionary(
            mode => mode,
            mode => createBindings(OsuManiaKeyLayout.GetDefaultKeys(mode)));

        foreach (Bindable<Key> binding in bindingsByMode.Values.SelectMany(
                     modeBindings => modeBindings))
        {
            binding.ValueChanged += _ => BindingsChanged?.Invoke();
        }
        foreach (ManiaShortcutAction action in supportedShortcutActions)
        {
            shortcutBindable(action).ValueChanged +=
                _ => BindingsChanged?.Invoke();
        }
    }

    public IReadOnlyList<Bindable<Key>> GetBindableKeys(KeyMode keyMode) =>
        bindingsByMode.TryGetValue(keyMode, out Bindable<Key>[] bindings)
            ? bindings
            : throw new ArgumentOutOfRangeException(
                nameof(keyMode),
                keyMode,
                "Unsupported key mode.");

    public IReadOnlyList<Key> GetKeys(KeyMode keyMode) =>
        GetBindableKeys(keyMode)
            .Select(binding => binding.Value)
            .ToArray();

    /// <summary>
    /// Assigns a key without allowing duplicate lanes. When the requested key is
    /// already in the active profile, both lanes are swapped so the operation is
    /// immediate and never leaves the profile unusable.
    /// </summary>
    public void SetBinding(KeyMode keyMode, int lane, Key key)
    {
        if (key == Key.Escape)
            throw new ArgumentException("Escape is reserved for navigation.", nameof(key));

        IReadOnlyList<Bindable<Key>> bindings = GetBindableKeys(keyMode);

        if ((uint)lane >= bindings.Count)
            throw new ArgumentOutOfRangeException(nameof(lane));

        int duplicateLane = -1;

        for (int index = 0; index < bindings.Count; index++)
        {
            if (index != lane && bindings[index].Value == key)
            {
                duplicateLane = index;
                break;
            }
        }

        Key previous = bindings[lane].Value;
        bindings[lane].Value = key;

        if (duplicateLane >= 0)
            bindings[duplicateLane].Value = previous;
    }

    /// <summary>
    /// Replaces a complete key profile as one validated operation. This is used
    /// by sequential capture so cancelling halfway never leaves a partial map.
    /// </summary>
    public void SetBindings(KeyMode keyMode, IReadOnlyList<Key> keys)
    {
        IReadOnlyList<Bindable<Key>> bindings = GetBindableKeys(keyMode);

        if (keys == null || keys.Count != bindings.Count)
            throw new ArgumentException(
                $"{keyMode} requires exactly {bindings.Count} keys.",
                nameof(keys));

        if (keys.Any(key => key == Key.Escape))
            throw new ArgumentException(
                "Escape is reserved for navigation.",
                nameof(keys));

        if (keys.Distinct().Count() != keys.Count)
            throw new ArgumentException(
                "A gameplay key profile cannot contain duplicate keys.",
                nameof(keys));

        for (int index = 0; index < bindings.Count; index++)
            bindings[index].Value = keys[index];
    }

    public void ResetBindings(KeyMode keyMode)
    {
        Key[] defaults = OsuManiaKeyLayout.GetDefaultKeys(keyMode);
        IReadOnlyList<Bindable<Key>> bindings = GetBindableKeys(keyMode);

        for (int index = 0; index < defaults.Length; index++)
            bindings[index].Value = defaults[index];
    }

    public Key GetShortcutBinding(ManiaShortcutAction action) =>
        shortcutBindable(action).Value;

    public Key GetDefaultShortcutBinding(ManiaShortcutAction action) =>
        defaultShortcutKey(action);

    public bool IsShortcutBindingDefault(ManiaShortcutAction action) =>
        GetShortcutBinding(action) == GetDefaultShortcutBinding(action);

    public int ModifiedShortcutBindingCount =>
        supportedShortcutActions.Count(action =>
            !IsShortcutBindingDefault(action));

    /// <summary>
    /// Changes a Mania action key. If another action uses the same key in an
    /// overlapping gameplay context, both bindings are swapped.
    /// </summary>
    public void SetShortcutBinding(
        ManiaShortcutAction action,
        Key key) =>
        SetShortcutBindingWithResult(action, key);

    public ManiaShortcutBindingChange SetShortcutBindingWithResult(
        ManiaShortcutAction action,
        Key key)
    {
        if (key == Key.Unknown)
        {
            throw new ArgumentException(
                "Unknown keys cannot be Mania shortcuts.",
                nameof(key));
        }

        Bindable<Key> target = shortcutBindable(action);
        Key previous = target.Value;
        ManiaShortcutAction? conflict = supportedShortcutActions
            .Where(other => other != action)
            .Where(other =>
                (shortcutContexts(other) & shortcutContexts(action)) != 0)
            .Select(other => (ManiaShortcutAction?)other)
            .FirstOrDefault(other =>
                shortcutBindable(other.Value).Value == key);

        target.Value = key;
        if (conflict.HasValue)
            shortcutBindable(conflict.Value).Value = previous;

        return new ManiaShortcutBindingChange(
            action,
            previous,
            key,
            conflict);
    }

    public void ResetShortcutBinding(ManiaShortcutAction action) =>
        SetShortcutBinding(action, defaultShortcutKey(action));

    public ManiaShortcutBindingChange ResetShortcutBindingWithResult(
        ManiaShortcutAction action) =>
        SetShortcutBindingWithResult(action, defaultShortcutKey(action));

    public void ResetShortcutBindings()
    {
        foreach (ManiaShortcutAction action in supportedShortcutActions)
            shortcutBindable(action).Value = defaultShortcutKey(action);
    }

    public void ApplyBindingPreset(
        KeyMode keyMode,
        GameplayKeyPreset preset)
    {
        if (!bindingPresets.TryGetValue((keyMode, preset), out Key[] keys))
        {
            throw new ArgumentOutOfRangeException(
                nameof(preset),
                preset,
                $"No {preset} preset exists for {keyMode}.");
        }

        SetBindings(keyMode, keys);
    }

    /// <summary>
    /// Copies the four central gameplay columns between 4K and 7K. Expanding to
    /// 7K preserves the current outer and centre keys wherever possible.
    /// </summary>
    public void CopyBindingsToOtherMode(KeyMode sourceMode)
    {
        switch (sourceMode)
        {
            case KeyMode.FourKey:
                copyFourKeyToSevenKey();
                break;

            case KeyMode.SevenKey:
                SetBindings(
                    KeyMode.FourKey,
                    new[]
                    {
                        SevenKeyBindings[1].Value,
                        SevenKeyBindings[2].Value,
                        SevenKeyBindings[4].Value,
                        SevenKeyBindings[5].Value,
                    });
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(sourceMode),
                    sourceMode,
                    "Only 4K and 7K profiles can be copied.");
        }
    }

    public void SetScrollSpeed(double speed) =>
        ScrollSpeed.Value = OsuManiaScrollSpeed.Clamp(speed);

    public void SetScrollTimeMilliseconds(double milliseconds) =>
        ScrollSpeed.Value =
            OsuManiaScrollSpeed.ComputeScrollSpeed(milliseconds);

    public void SetJudgementDisplayDuration(double milliseconds) =>
        JudgementDisplayDurationMilliseconds.Value = Math.Clamp(
            Math.Round(
                milliseconds
                / JudgementDisplayDurationStepMilliseconds)
            * JudgementDisplayDurationStepMilliseconds,
            MinimumJudgementDisplayDurationMilliseconds,
            MaximumJudgementDisplayDurationMilliseconds);

    public void SetJudgementOpacity(double opacity) =>
        JudgementOpacity.Value = Math.Clamp(
            Math.Round(opacity / JudgementOpacityStep)
            * JudgementOpacityStep,
            MinimumJudgementOpacity,
            MaximumJudgementOpacity);

    public void ResetGameplayLayout()
    {
        LayoutPlayfieldOffsetX.SetDefault();
        LayoutPlayfieldOffsetY.SetDefault();
        LayoutHudOffsetX.SetDefault();
        LayoutHudOffsetY.SetDefault();
        LayoutPlayfieldWidthScale.SetDefault();
        LayoutPlayfieldHeightScale.SetDefault();
        LayoutHudScaleX.SetDefault();
        LayoutHudScaleY.SetDefault();
        LayoutAccuracyOffsetX.SetDefault();
        LayoutAccuracyOffsetY.SetDefault();
        LayoutAccuracyScaleX.SetDefault();
        LayoutAccuracyScaleY.SetDefault();
        LayoutProgressOffsetX.SetDefault();
        LayoutProgressOffsetY.SetDefault();
        LayoutProgressScaleX.SetDefault();
        LayoutProgressScaleY.SetDefault();
        LayoutTimingBarOffsetX.SetDefault();
        LayoutTimingBarOffsetY.SetDefault();
        LayoutTimingBarScaleX.SetDefault();
        LayoutTimingBarScaleY.SetDefault();
        LayoutComboOffsetX.SetDefault();
        LayoutComboOffsetY.SetDefault();
        LayoutComboScaleX.SetDefault();
        LayoutComboScaleY.SetDefault();
        LayoutJudgementOffsetX.SetDefault();
        LayoutJudgementOffsetY.SetDefault();
        LayoutJudgementScaleX.SetDefault();
        LayoutJudgementScaleY.SetDefault();
        LayoutPerformanceReadoutOffsetX.SetDefault();
        LayoutPerformanceReadoutOffsetY.SetDefault();
        ReplayControlsOffsetX.SetDefault();
        ReplayControlsOffsetY.SetDefault();
        LayoutTopCoverRatio.SetDefault();
        LayoutBottomCoverRatio.SetDefault();
        BackgroundDim.SetDefault();
    }

    public void AdjustScrollSpeed(double amount) =>
        ScrollSpeed.Value = OsuManiaScrollSpeed.AdjustWholeStep(
            ScrollSpeed.Value,
            amount);

    public void AdjustScrollTimeMilliseconds(double deltaMilliseconds) =>
        ScrollSpeed.Value = OsuManiaScrollSpeed.AdjustScrollTime(
            ScrollSpeed.Value,
            deltaMilliseconds);

    public void SetEtternaJustice(double justice) =>
        EtternaJustice.Value = Math.Clamp(
            Math.Round(justice),
            JudgementConfiguration.MinimumEtternaJustice,
            JudgementConfiguration.MaximumEtternaJustice);

    public JudgementConfiguration GetJudgementConfiguration() =>
        new(
            JudgementMode.Value,
            (int)Math.Clamp(
                Math.Round(EtternaJustice.Value),
                JudgementConfiguration.MinimumEtternaJustice,
                JudgementConfiguration.MaximumEtternaJustice));

    private static Bindable<Key>[] createBindings(IEnumerable<Key> defaults) =>
        defaults.Select(key => new Bindable<Key>(key)).ToArray();

    private Bindable<Key> shortcutBindable(ManiaShortcutAction action) =>
        action switch
        {
            ManiaShortcutAction.PauseOrBack => PauseOrBackKey,
            ManiaShortcutAction.ToggleLayoutEditorUi =>
                ToggleLayoutEditorUiKey,
            ManiaShortcutAction.SkipIntro => SkipIntroKey,
            ManiaShortcutAction.QuickRetry => QuickRetryKey,
            ManiaShortcutAction.DecreaseScrollSpeed =>
                DecreaseScrollSpeedKey,
            ManiaShortcutAction.IncreaseScrollSpeed =>
                IncreaseScrollSpeedKey,
            ManiaShortcutAction.MenuPrevious => MenuPreviousKey,
            ManiaShortcutAction.MenuPreviousAlternate =>
                MenuPreviousAlternateKey,
            ManiaShortcutAction.MenuNext => MenuNextKey,
            ManiaShortcutAction.MenuNextAlternate =>
                MenuNextAlternateKey,
            ManiaShortcutAction.Confirm => ConfirmKey,
            ManiaShortcutAction.ConfirmAlternate => ConfirmAlternateKey,
            ManiaShortcutAction.Retry => RetryKey,
            ManiaShortcutAction.WatchReplay => WatchReplayKey,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

    private static Key defaultShortcutKey(ManiaShortcutAction action) =>
        action switch
        {
            ManiaShortcutAction.PauseOrBack => Key.Escape,
            ManiaShortcutAction.ToggleLayoutEditorUi => Key.BackSlash,
            ManiaShortcutAction.SkipIntro => Key.Space,
            ManiaShortcutAction.QuickRetry => Key.Tilde,
            ManiaShortcutAction.DecreaseScrollSpeed => Key.F3,
            ManiaShortcutAction.IncreaseScrollSpeed => Key.F4,
            ManiaShortcutAction.MenuPrevious => Key.Up,
            ManiaShortcutAction.MenuPreviousAlternate => Key.W,
            ManiaShortcutAction.MenuNext => Key.Down,
            ManiaShortcutAction.MenuNextAlternate => Key.S,
            ManiaShortcutAction.Confirm => Key.Enter,
            ManiaShortcutAction.ConfirmAlternate => Key.Space,
            ManiaShortcutAction.Retry => Key.R,
            ManiaShortcutAction.WatchReplay => Key.V,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

    private static ShortcutContext shortcutContexts(
        ManiaShortcutAction action) =>
        action switch
        {
            ManiaShortcutAction.PauseOrBack =>
                ShortcutContext.ActiveGameplay
                | ShortcutContext.PauseMenu
                | ShortcutContext.Failure
                | ShortcutContext.Results,
            ManiaShortcutAction.ToggleLayoutEditorUi =>
                ShortcutContext.LayoutEditor,
            ManiaShortcutAction.SkipIntro => ShortcutContext.Intro,
            ManiaShortcutAction.QuickRetry
                or ManiaShortcutAction.DecreaseScrollSpeed
                or ManiaShortcutAction.IncreaseScrollSpeed =>
                    ShortcutContext.ActiveGameplay,
            ManiaShortcutAction.MenuPrevious
                or ManiaShortcutAction.MenuPreviousAlternate
                or ManiaShortcutAction.MenuNext
                or ManiaShortcutAction.MenuNextAlternate =>
                    ShortcutContext.PauseMenu,
            ManiaShortcutAction.Confirm
                or ManiaShortcutAction.ConfirmAlternate
                or ManiaShortcutAction.Retry =>
                    ShortcutContext.PauseMenu
                    | ShortcutContext.Failure
                    | ShortcutContext.Results,
            ManiaShortcutAction.WatchReplay => ShortcutContext.Results,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

    private void copyFourKeyToSevenKey()
    {
        Key[] fourKeys = GetKeys(KeyMode.FourKey).ToArray();
        var used = new HashSet<Key>(fourKeys);
        Key[] fallback =
        {
            SevenKeyBindings[0].Value,
            SevenKeyBindings[3].Value,
            SevenKeyBindings[6].Value,
            Key.S,
            Key.Space,
            Key.L,
            Key.A,
            Key.Semicolon,
            Key.G,
            Key.H,
        };
        var extras = fallback.Where(used.Add).Take(3).ToArray();

        if (extras.Length != 3)
        {
            throw new InvalidOperationException(
                "Could not create a unique 7K profile from the 4K keys.");
        }

        SetBindings(
            KeyMode.SevenKey,
            new[]
            {
                extras[0],
                fourKeys[0],
                fourKeys[1],
                extras[1],
                fourKeys[2],
                fourKeys[3],
                extras[2],
            });
    }
}

internal readonly record struct GameplayHudLayoutSetting(
    string Name,
    Bindable<double> Bindable,
    double Minimum,
    double Maximum);
