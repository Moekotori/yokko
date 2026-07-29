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
using osuTK;
using osuTK.Graphics;
using Yokko.Audio;
using Yokko.Game.Audio;
using Yokko.Game.Gameplay;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal partial class AudioSettingsPanel : CompositeDrawable, ISettingsTransientUi
{
    private static readonly int[] bufferSizes = { 64, 128, 256, 512 };

    private readonly YokkoAudioSettings settings;
    private readonly YokkoGameplaySettings gameplaySettings;
    private readonly List<SettingsSegmentedChoiceButton> backendButtons = new();
    private readonly List<SettingsSegmentedChoiceButton> bufferButtons = new();
    private readonly CancellationTokenSource deviceLoadCancellation = new();
    private readonly SpriteText statusMetadata;
    private readonly SpriteText statusTitle;
    private readonly SettingsAudioDeviceSelector deviceSelector;
    private readonly SettingsAudioTestControl testControl;
    private readonly AudioSettingsTestPlayer testPlayer;
    private IAudioEngine deviceEnumerator;
    private bool nativeAvailable;
    private bool devicesLoaded;
    private bool disposed;
    private bool testPlaying;

    internal AudioBackendKind CurrentBackend => settings.PreferredBackend.Value;
    internal string CurrentDeviceId => settings.DeviceId.Value;
    internal int CurrentBufferSize => settings.PreferredBufferSize.Value;
    internal double CurrentOffsetMilliseconds =>
        settings.UserOffsetMilliseconds.Value;
    internal double CurrentMasterVolume => settings.MasterVolume.Value;
    internal double CurrentMusicVolume => settings.MusicVolume.Value;
    internal double CurrentHitSoundVolume => settings.HitSoundVolume.Value;
    internal bool HitSoundsEnabled =>
        gameplaySettings.KeysoundsEnabled.Value;
    internal bool IsDeviceMenuOpen => deviceSelector.IsOpen;

    public AudioSettingsPanel(
        YokkoAudioSettings settings,
        YokkoGameplaySettings gameplaySettings,
        string testDirectory)
    {
        this.settings = settings;
        this.gameplaySettings = gameplaySettings;
        testPlayer = new AudioSettingsTestPlayer(
            settings,
            AudioEngineFactory.CreateDefault,
            testDirectory);
        RelativeSizeAxes = Axes.Both;
        nativeAvailable = NativeAudioEngine.IsAvailable;

        InternalChildren = new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(378, 42),
                Text = YokkoStrings.Get("settings.audio.title"),
                Font = HomeTypography.Display(58),
                Spacing = new Vector2(0.45f, 0),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(378, 105),
                Text = YokkoStrings.Get("settings.audio.subtitle"),
                Font = HomeTypography.Body(20),
                Spacing = new Vector2(0.2f, 0),
                Colour = SettingsTheme.MutedNavy,
            },
            createStatusCard(out statusTitle, out statusMetadata),
            createDivider(232),
            createSettingRow(
                236,
                YokkoStrings.Get("settings.audio.backend"),
                createBackendControl()),
            createDivider(292),
            createSettingRow(
                294,
                YokkoStrings.Get("settings.audio.device"),
                deviceSelector = new SettingsAudioDeviceSelector(
                    settings.DeviceId),
                -10),
            createDivider(350),
            createSettingRow(
                352,
                YokkoStrings.Get("settings.audio.buffer"),
                createBufferControl()),
            createDivider(408),
            createSettingRow(
                410,
                YokkoStrings.Get("settings.audio.master_volume"),
                new SettingsVolumeMixer(
                    settings.MasterVolume,
                    settings.MusicVolume,
                    settings.HitSoundVolume)),
            createDivider(466),
            createSettingRow(
                468,
                YokkoStrings.Get("settings.audio.hitsounds"),
                new SettingsAudioToggle(
                    gameplaySettings.KeysoundsEnabled)),
            createDivider(524),
            createSettingRow(
                526,
                YokkoStrings.Get("settings.audio.test"),
                testControl = new SettingsAudioTestControl(
                    () => StartAudioTest(AudioSettingsTestKind.Music),
                    () => StartAudioTest(AudioSettingsTestKind.HitSound))),
            createDivider(582),
            createSettingRow(
                584,
                YokkoStrings.Get("settings.audio.offset"),
                new SettingsOffsetStepper(
                    settings.UserOffsetMilliseconds)),
            new SettingsPanelFooter(
                YokkoStrings.Get("settings.audio.apply_next_playback")),
        };

        settings.PreferredBackend.BindValueChanged(onPreferenceChanged, true);
        settings.DeviceId.BindValueChanged(onPreferenceChanged);
        settings.PreferredBufferSize.BindValueChanged(onPreferenceChanged);
        settings.MasterVolume.BindValueChanged(onPreferenceChanged);
        settings.MusicVolume.BindValueChanged(onPreferenceChanged);
        settings.HitSoundVolume.BindValueChanged(onPreferenceChanged);
        settings.UserOffsetMilliseconds.BindValueChanged(onPreferenceChanged);
        _ = loadDevicesAsync(deviceLoadCancellation.Token);
    }

    internal void SelectBackend(AudioBackendKind backend)
    {
        if (backend != AudioBackendKind.WasapiExclusive
            && backend != AudioBackendKind.SharedWasapi)
            throw new ArgumentOutOfRangeException(
                nameof(backend),
                backend,
                "Only implemented backends can be selected.");

        settings.PreferredBackend.Value = backend;
    }

    internal void SelectBufferSize(int frames)
    {
        if (!bufferSizes.Contains(frames))
            throw new ArgumentOutOfRangeException(
                nameof(frames),
                frames,
                "Unsupported buffer profile.");

        settings.PreferredBufferSize.Value = frames;
    }

    internal void SetOffset(double milliseconds)
    {
        settings.UserOffsetMilliseconds.Value =
            Math.Clamp(Math.Round(milliseconds), -200, 200);
    }

    internal void SetMasterVolume(double volume) =>
        settings.MasterVolume.Value =
            Math.Clamp(Math.Round(volume * 100) / 100, 0, 1);

    internal void SetMusicVolume(double volume) =>
        settings.MusicVolume.Value =
            Math.Clamp(Math.Round(volume * 100) / 100, 0, 1);

    internal void SetHitSoundVolume(double volume) =>
        settings.HitSoundVolume.Value =
            Math.Clamp(Math.Round(volume * 100) / 100, 0, 1);

    internal void SetHitSoundsEnabled(bool enabled) =>
        gameplaySettings.KeysoundsEnabled.Value = enabled;

    internal void StartAudioTest(AudioSettingsTestKind kind)
    {
        if (testPlaying)
            return;

        if (kind == AudioSettingsTestKind.HitSound
            && !gameplaySettings.KeysoundsEnabled.Value)
        {
            statusTitle.Text = YokkoStrings.Get(
                "settings.audio.hitsounds_disabled");
            return;
        }

        _ = runAudioTestAsync(kind);
    }

    internal void ToggleDeviceMenu() => deviceSelector.Toggle();

    public bool DismissTransientUi() => deviceSelector.Dismiss();

    private Drawable createBackendControl()
    {
        var options = new[]
        {
            (
                AudioBackendKind.WasapiExclusive,
                YokkoStrings.Get("settings.audio.exclusive"),
                FontAwesome.Solid.Bolt),
            (
                AudioBackendKind.SharedWasapi,
                YokkoStrings.Get("settings.audio.shared"),
                FontAwesome.Solid.LayerGroup),
        };

        foreach ((AudioBackendKind backend, LocalisableString label, IconUsage icon) in options)
        {
            AudioBackendKind captured = backend;
            backendButtons.Add(new SettingsSegmentedChoiceButton(
                label,
                icon,
                () => SelectBackend(captured),
                299)
            {
                Value = backend,
            });
        }

        return segmentedControl(backendButtons);
    }

    private Drawable createBufferControl()
    {
        foreach (int frames in bufferSizes)
        {
            int captured = frames;
            bufferButtons.Add(new SettingsSegmentedChoiceButton(
                YokkoStrings.Get("settings.audio.frames", frames),
                frames <= 128 ? FontAwesome.Solid.Bolt : FontAwesome.Solid.ShieldAlt,
                () => SelectBufferSize(captured),
                149.5f)
            {
                Value = frames,
            });
        }

        return segmentedControl(bufferButtons);
    }

    private static Drawable segmentedControl(
        IReadOnlyList<SettingsSegmentedChoiceButton> buttons) =>
        new Container
        {
            Size = new Vector2(598, 54),
            Masking = true,
            CornerRadius = 7,
            BorderThickness = 1.4f,
            BorderColour = HomeControlColours.Navy,
            Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Children = buttons.Cast<Drawable>().ToArray(),
            },
        };

    private async Task loadDevicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            deviceEnumerator = AudioEngineFactory.CreateDefault();
            IReadOnlyList<AudioDeviceInfo> devices =
                await deviceEnumerator.GetOutputDevicesAsync(cancellationToken)
                                      .ConfigureAwait(false);
            var options = devices
                          .GroupBy(device => device.Id, StringComparer.Ordinal)
                          .Select(group => group.First())
                          .OrderByDescending(device => device.IsDefault)
                          .ThenBy(device => device.Name, StringComparer.Ordinal)
                          .Select(device => new AudioDeviceOption(
                              device.Id,
                              device.Name))
                          .ToArray();

            Schedule(() =>
            {
                if (disposed)
                    return;

                devicesLoaded = true;
                deviceSelector.SetDevices(options);
                refreshSelection();
            });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            Schedule(() =>
            {
                if (disposed)
                    return;

                devicesLoaded = true;
                nativeAvailable = false;
                deviceSelector.SetDevices(Array.Empty<AudioDeviceOption>());
                refreshSelection();
            });
        }
    }

    private static Drawable createStatusCard(
        out SpriteText title,
        out SpriteText metadata)
    {
        var result = new Container
        {
            Position = new Vector2(378, 150),
            Size = new Vector2(840, 70),
            Masking = true,
            CornerRadius = 8,
        };

        result.Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SettingsTheme.StatusCyan,
            },
            new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 48,
                Size = new Vector2(56),
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 48,
                Size = new Vector2(26),
                Icon = FontAwesome.Solid.Headphones,
                Colour = HomeControlColours.Navy,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 105,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    title = new SpriteText
                    {
                        Font = HomeTypography.Display(22),
                        Colour = HomeControlColours.Navy,
                    },
                    metadata = new SpriteText
                    {
                        Font = HomeTypography.Body(17),
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -34,
                Size = new Vector2(42),
                Icon = FontAwesome.Solid.WaveSquare,
                Colour = Color4.White,
            },
        };

        return result;
    }

    private static Drawable createSettingRow(
        float y,
        LocalisableString title,
        Drawable control,
        float depth = 0) =>
        new Container
        {
            Position = new Vector2(378, y),
            Size = new Vector2(840, 54),
            Depth = depth,
            Children = new Drawable[]
            {
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = title,
                    Font = HomeTypography.Display(24),
                    Colour = HomeControlColours.Navy,
                },
                new Container
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Size = new Vector2(598, 54),
                    Child = control,
                },
            },
        };

    private static Drawable createDivider(float y) =>
        new Box
        {
            Position = new Vector2(378, y),
            Width = 840,
            Height = 1,
            Colour = SettingsTheme.Divider,
        };

    private void onPreferenceChanged<T>(ValueChangedEvent<T> _) =>
        refreshSelection();

    private async Task runAudioTestAsync(AudioSettingsTestKind kind)
    {
        testPlaying = true;
        refreshSelection();
        testControl.SetPlaying(kind);
        bool failed = false;

        try
        {
            await testPlayer.PlayAsync(
                kind,
                gameplaySettings.KeysoundsEnabled.Value,
                deviceLoadCancellation.Token);
        }
        catch (OperationCanceledException)
            when (deviceLoadCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            failed = true;
            Logger.Error(
                ex,
                "The audio settings test signal could not be played.",
                LoggingTarget.Runtime);
            statusTitle.Text = YokkoStrings.Get(
                "settings.audio.test_failed");
        }
        finally
        {
            testPlaying = false;
            testControl.SetIdle();
            if (!failed)
                refreshSelection();
        }
    }

    private void refreshSelection()
    {
        foreach (SettingsSegmentedChoiceButton button in backendButtons)
        {
            button.SetSelected(
                button.Value is AudioBackendKind backend
                && backend == settings.PreferredBackend.Value);
        }

        foreach (SettingsSegmentedChoiceButton button in bufferButtons)
        {
            button.SetSelected(
                button.Value is int frames
                && frames == settings.PreferredBufferSize.Value);
        }

        statusTitle.Text = nativeAvailable
            ? YokkoStrings.Get("settings.audio.native_ready")
            : YokkoStrings.Get("settings.audio.native_unavailable");
        statusMetadata.Text = YokkoStrings.Get(
            "settings.audio.status_metadata",
            backendName(settings.PreferredBackend.Value),
            settings.PreferredBufferSize.Value,
            devicesLoaded
                ? deviceSelector.SelectedName
                : YokkoStrings.Get("settings.audio.loading_devices"));
    }

    private static LocalisableString backendName(AudioBackendKind backend) =>
        backend == AudioBackendKind.SharedWasapi
            ? YokkoStrings.Get("settings.audio.shared")
            : YokkoStrings.Get("settings.audio.exclusive");

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            disposed = true;
            deviceLoadCancellation.Cancel();
            deviceLoadCancellation.Dispose();
            settings.PreferredBackend.ValueChanged -= onPreferenceChanged;
            settings.DeviceId.ValueChanged -= onPreferenceChanged;
            settings.PreferredBufferSize.ValueChanged -= onPreferenceChanged;
            settings.MasterVolume.ValueChanged -= onPreferenceChanged;
            settings.MusicVolume.ValueChanged -= onPreferenceChanged;
            settings.HitSoundVolume.ValueChanged -= onPreferenceChanged;
            settings.UserOffsetMilliseconds.ValueChanged -= onPreferenceChanged;
            _ = testPlayer.DisposeAsync();
            if (deviceEnumerator != null)
                _ = deviceEnumerator.DisposeAsync();
        }

        base.Dispose(isDisposing);
    }
}

internal sealed record AudioDeviceOption(string Id, string Name);

internal partial class SettingsAudioDeviceSelector : CompositeDrawable
{
    private readonly Bindable<string> deviceId;
    private readonly Box headerBackground;
    private readonly SpriteText valueText;
    private readonly SpriteIcon chevron;
    private readonly Container menu;
    private readonly FillFlowContainer optionFlow;
    private bool open;
    private IReadOnlyList<AudioDeviceOption> devices =
        new[] { new AudioDeviceOption(string.Empty, string.Empty) };

    internal bool IsOpen => open;

    internal LocalisableString SelectedName
    {
        get
        {
            if (string.IsNullOrEmpty(deviceId.Value))
                return YokkoStrings.Get("settings.audio.default_device");

            return devices.FirstOrDefault(device => device.Id == deviceId.Value)?.Name
                   ?? YokkoStrings.Get("settings.audio.default_device");
        }
    }

    public SettingsAudioDeviceSelector(Bindable<string> deviceId)
    {
        this.deviceId = deviceId;
        Size = new Vector2(598, 54);

        var header = new SettingsDropdownHeader(
            () => open,
            Toggle)
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = 7,
            BorderThickness = 1.4f,
            BorderColour = HomeControlColours.Navy,
            Children = new Drawable[]
            {
                headerBackground = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
                valueText = new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 18,
                    Width = 520,
                    Truncate = true,
                    Font = HomeTypography.Body(18),
                    Colour = HomeControlColours.Navy,
                },
                chevron = new SpriteIcon
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -20,
                    Size = new Vector2(15),
                    Icon = FontAwesome.Solid.ChevronDown,
                    Colour = HomeControlColours.Pink,
                },
            },
        };
        header.Background = headerBackground;

        optionFlow = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
        };
        menu = new Container
        {
            Y = 59,
            Width = 598,
            Masking = true,
            CornerRadius = 7,
            BorderThickness = 1.4f,
            BorderColour = HomeControlColours.Navy,
            Alpha = 0,
            Scale = new Vector2(1, 0.96f),
            Depth = -20,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
                new BasicScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ScrollbarVisible = false,
                    Child = optionFlow,
                },
            },
        };

        InternalChildren = new Drawable[] { header, menu };
        deviceId.BindValueChanged(onDeviceChanged, true);
        rebuildOptions();
    }

    public void SetDevices(IReadOnlyList<AudioDeviceOption> availableDevices)
    {
        devices = new[] { new AudioDeviceOption(string.Empty, string.Empty) }
                  .Concat(availableDevices)
                  .ToArray();
        if (!devices.Any(device => device.Id == deviceId.Value))
            deviceId.Value = string.Empty;
        rebuildOptions();
        refresh();
    }

    internal void Toggle() => setOpen(!open);

    public bool Dismiss()
    {
        if (!open)
            return false;

        setOpen(false);
        return true;
    }

    private void setOpen(bool shouldOpen)
    {
        open = shouldOpen;
        headerBackground.FadeColour(
            open ? SettingsTheme.PaleCyan : Color4.White,
            120,
            Easing.OutQuint);
        chevron.RotateTo(open ? 180 : 0, 160, Easing.OutQuint);

        if (open)
        {
            menu.Show();
            menu.FadeTo(1, 140, Easing.OutQuint);
            menu.ScaleTo(1, 140, Easing.OutQuint);
        }
        else
        {
            menu.FadeOut(100, Easing.OutQuint);
            menu.ScaleTo(new Vector2(1, 0.96f), 100, Easing.OutQuint);
        }
    }

    private void rebuildOptions()
    {
        optionFlow.Children = devices
                              .Select(device =>
                              {
                                  AudioDeviceOption captured = device;
                                  return (Drawable)new SettingsAudioDeviceOption(
                                      device.Id,
                                      string.IsNullOrEmpty(device.Id)
                                          ? YokkoStrings.Get("settings.audio.default_device")
                                          : device.Name,
                                      () => select(captured.Id));
                              })
                              .ToArray();
        menu.Height = Math.Min(
            devices.Count * SettingsAudioDeviceOption.RowHeight,
            SettingsAudioDeviceOption.RowHeight * 5);
        refreshOptions();
    }

    private void select(string id)
    {
        deviceId.Value = id;
        setOpen(false);
    }

    private void refresh()
    {
        valueText.Text = SelectedName;
        refreshOptions();
    }

    private void refreshOptions()
    {
        foreach (SettingsAudioDeviceOption option in optionFlow.Children)
            option.SetSelected(option.Value == deviceId.Value);
    }

    private void onDeviceChanged(ValueChangedEvent<string> _) => refresh();

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            deviceId.ValueChanged -= onDeviceChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class SettingsAudioDeviceOption : ClickableContainer
{
    public const float RowHeight = 40;

    private readonly Box background;
    private readonly SpriteIcon check;

    public string Value { get; }

    public SettingsAudioDeviceOption(
        string value,
        LocalisableString label,
        Action action)
    {
        Value = value;
        Action = action;
        RelativeSizeAxes = Axes.X;
        Height = RowHeight;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 18,
                Width = 520,
                Truncate = true,
                Text = label,
                Font = HomeTypography.Body(17),
                Colour = HomeControlColours.Navy,
            },
            check = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -20,
                Size = new Vector2(13),
                Icon = FontAwesome.Solid.Check,
                Colour = HomeControlColours.Pink,
                Alpha = 0,
            },
            new Box
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = SettingsTheme.Divider,
            },
        };
    }

    public void SetSelected(bool selected) =>
        check.FadeTo(selected ? 1 : 0, 100, Easing.OutQuint);

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(
            SettingsTheme.PaleCyan,
            100,
            Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        background.FadeColour(Color4.White, 120, Easing.OutQuint);
}

internal partial class SettingsVolumeMixer : CompositeDrawable
{
    public SettingsVolumeMixer(
        Bindable<double> master,
        Bindable<double> music,
        Bindable<double> hitSound)
    {
        Size = new Vector2(598, 54);
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
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Children = new Drawable[]
                {
                    new SettingsVolumeSlider(
                        "MASTER",
                        master,
                        true),
                    new SettingsVolumeSlider(
                        "MUSIC",
                        music,
                        true),
                    new SettingsVolumeSlider(
                        "HIT",
                        hitSound,
                        false),
                },
            },
        };
    }
}

internal partial class SettingsVolumeSlider : CompositeDrawable
{
    private const float track_x = 14;
    private const float track_y = 36;
    private const float track_width = 598f / 3 - track_x * 2;
    private const double drag_step = 0.01;
    private const double wheel_step = 0.05;
    private readonly Bindable<double> volume;
    private readonly Box track;
    private readonly Box fill;
    private readonly Circle knob;
    private readonly SpriteText valueText;

    public SettingsVolumeSlider(
        string label,
        Bindable<double> volume,
        bool showDivider)
    {
        this.volume = volume;
        Size = new Vector2(598f / 3, 54);

        InternalChildren = new Drawable[]
        {
            showDivider
                ? new Box
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Width = 1,
                    RelativeSizeAxes = Axes.Y,
                    Colour = SettingsTheme.Divider,
                }
                : new Container(),
            new SpriteText
            {
                Position = new Vector2(track_x, 5),
                Text = label,
                Font = HomeTypography.Body(12),
                Colour = SettingsTheme.MutedNavy,
            },
            valueText = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-track_x, 3),
                Font = HomeTypography.Display(15),
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
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(track_x, track_y + 2.5f),
                Size = new Vector2(14),
                Colour = Color4.White,
                BorderThickness = 2.5f,
                BorderColour = HomeControlColours.Pink,
            },
        };

        volume.BindValueChanged(onVolumeChanged, true);
    }

    internal static double ValueFromProgress(double progress) =>
        Math.Clamp(
            Math.Round(Math.Clamp(progress, 0, 1) / drag_step)
            * drag_step,
            0,
            1);

    internal static double AdjustForScroll(
        double value,
        float scrollDelta) =>
        Math.Clamp(
            Math.Round(
                (value + Math.Sign(scrollDelta) * wheel_step)
                / drag_step)
            * drag_step,
            0,
            1);

    internal static bool AcceptsWheelAt(float localY) =>
        localY is >= 24 and <= 54;

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        Vector2 local = ToLocalSpace(e.ScreenSpaceMousePosition);
        if (local.Y < 24)
            return false;

        updateFrom(local.X);
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e) => true;

    protected override void OnDrag(DragEvent e) =>
        updateFrom(ToLocalSpace(e.ScreenSpaceMousePosition).X);

    protected override bool OnScroll(ScrollEvent e)
    {
        Vector2 local = ToLocalSpace(e.ScreenSpaceMousePosition);
        if (!AcceptsWheelAt(local.Y) || e.ScrollDelta.Y == 0)
            return false;

        volume.Value = AdjustForScroll(
            volume.Value,
            e.ScrollDelta.Y);
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

    private void updateFrom(float localX) =>
        volume.Value = ValueFromProgress(
            (localX - track_x) / track_width);

    private void onVolumeChanged(ValueChangedEvent<double> change)
    {
        float progress = (float)Math.Clamp(change.NewValue, 0, 1);
        fill.Width = progress * track_width;
        knob.X = track_x + progress * track_width;
        valueText.Text = $"{change.NewValue * 100:0}%";
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            volume.ValueChanged -= onVolumeChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class SettingsAudioTestControl : CompositeDrawable
{
    private readonly SettingsAudioTestButton musicButton;
    private readonly SettingsAudioTestButton hitSoundButton;

    public SettingsAudioTestControl(
        Action playMusic,
        Action playHitSound)
    {
        Size = new Vector2(598, 54);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1.4f;
        BorderColour = HomeControlColours.Navy;

        InternalChildren = new Drawable[]
        {
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Children = new Drawable[]
                {
                    musicButton = new SettingsAudioTestButton(
                        "MUSIC",
                        FontAwesome.Solid.Music,
                        playMusic),
                    hitSoundButton = new SettingsAudioTestButton(
                        "HIT",
                        FontAwesome.Solid.VolumeUp,
                        playHitSound),
                },
            },
        };
    }

    internal void SetPlaying(AudioSettingsTestKind kind)
    {
        musicButton.SetPlaying(kind == AudioSettingsTestKind.Music);
        hitSoundButton.SetPlaying(kind == AudioSettingsTestKind.HitSound);
    }

    internal void SetIdle()
    {
        musicButton.SetPlaying(false);
        hitSoundButton.SetPlaying(false);
    }
}

internal partial class SettingsAudioTestButton : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteIcon icon;
    private readonly SpriteText text;
    private bool playing;

    public SettingsAudioTestButton(
        string label,
        IconUsage iconUsage,
        Action action)
    {
        Action = action;
        Size = new Vector2(299, 54);

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new Box
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Width = 1,
                RelativeSizeAxes = Axes.Y,
                Colour = SettingsTheme.Divider,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(12, 0),
                Children = new Drawable[]
                {
                    icon = new SpriteIcon
                    {
                        Size = new Vector2(16),
                        Icon = iconUsage,
                        Colour = HomeControlColours.Navy,
                    },
                    text = new SpriteText
                    {
                        Text = label,
                        Font = HomeTypography.Display(16),
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
        };
    }

    internal void SetPlaying(bool value)
    {
        playing = value;
        background.FadeColour(
            value ? HomeControlColours.Navy : Color4.White,
            100,
            Easing.OutQuint);
        icon.Colour = value
            ? SettingsTheme.StatusCyan
            : HomeControlColours.Navy;
        text.Colour = value ? Color4.White : HomeControlColours.Navy;
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!playing)
        {
            background.FadeColour(
                SettingsTheme.PaleCyan,
                100,
                Easing.OutQuint);
        }

        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        if (!playing)
            background.FadeColour(Color4.White, 120, Easing.OutQuint);
    }
}

internal partial class SettingsAudioToggle : ClickableContainer
{
    private readonly BindableBool value;
    private readonly Box background;
    private readonly Box switchTrack;
    private readonly Circle switchThumb;
    private readonly SpriteText stateText;

    public SettingsAudioToggle(BindableBool value)
    {
        this.value = value;
        Action = () => value.Value = !value.Value;
        Size = new Vector2(598, 54);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1.4f;
        BorderColour = HomeControlColours.Navy;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            stateText = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 18,
                Font = HomeTypography.Display(17),
                Colour = HomeControlColours.Navy,
            },
            new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -16,
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
        };

        value.BindValueChanged(onValueChanged, true);
    }

    private void onValueChanged(ValueChangedEvent<bool> change)
    {
        switchTrack.FadeColour(
            change.NewValue
                ? HomeControlColours.Navy
                : SettingsTheme.Divider,
            120,
            Easing.OutQuint);
        switchThumb.MoveToX(
            change.NewValue ? 36 : 12,
            120,
            Easing.OutQuint);
        stateText.Text = YokkoStrings.Get(
            change.NewValue
                ? "settings.audio.enabled"
                : "settings.audio.disabled");
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(
            SettingsTheme.PaleCyan,
            120,
            Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        background.FadeColour(Color4.White, 120, Easing.OutQuint);

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            value.ValueChanged -= onValueChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class SettingsOffsetStepper : CompositeDrawable
{
    private readonly Bindable<double> offset;
    private readonly SpriteText valueText;

    public SettingsOffsetStepper(Bindable<double> offset)
    {
        this.offset = offset;
        Size = new Vector2(598, 54);
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
            createButton(FontAwesome.Solid.Minus, Anchor.CentreLeft, -1),
            valueText = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Font = HomeTypography.Display(19),
                Colour = HomeControlColours.Navy,
            },
            createButton(FontAwesome.Solid.Plus, Anchor.CentreRight, 1),
        };

        offset.BindValueChanged(onOffsetChanged, true);
    }

    private Drawable createButton(IconUsage icon, Anchor anchor, double delta) =>
        new ClickableContainer
        {
            Anchor = anchor,
            Origin = anchor,
            Width = 72,
            RelativeSizeAxes = Axes.Y,
            Action = () => offset.Value =
                Math.Clamp(Math.Round(offset.Value + delta), -200, 200),
            Child = new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(16),
                Icon = icon,
                Colour = HomeControlColours.Pink,
            },
        };

    private void refresh()
    {
        valueText.Text = $"{offset.Value:+0;-0;0} ms";
    }

    private void onOffsetChanged(ValueChangedEvent<double> _) => refresh();

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            offset.ValueChanged -= onOffsetChanged;

        base.Dispose(isDisposing);
    }
}
