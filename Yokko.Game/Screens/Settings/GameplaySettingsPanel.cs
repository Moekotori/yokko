using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Gameplay;
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
    Feedback,
}

internal partial class GameplaySettingsPanel : CompositeDrawable, ISettingsTransientUi
{
    private readonly YokkoGameplaySettings settings;
    private readonly YokkoAudioSettings audioSettings;
    private readonly List<GameplaySectionTab> sectionTabs = new();
    private readonly List<GameplayBindingCard> bindingCards = new();
    private readonly List<Key> sequentialKeys = new();
    private readonly SpriteText statusMetadata;
    private readonly Container contentHost;
    private GameplayBindingCard capturingCard;
    private SpriteText keyCaptureHint;
    private bool sequentialCapture;
    private KeyMode selectedKeyMode = KeyMode.FourKey;

    internal GameplaySettingsSection CurrentSection { get; private set; } =
        GameplaySettingsSection.Input;

    internal KeyMode SelectedKeyMode => selectedKeyMode;

    internal bool IsCapturingKey => capturingCard != null;

    internal bool IsSequentialCapture => sequentialCapture;

    internal int SequentialCaptureIndex => sequentialKeys.Count;

    internal double CurrentScrollSpeed => settings.ScrollSpeed.Value;

    internal bool ShowLanePressFeedback =>
        settings.ShowLanePressFeedback.Value;

    public GameplaySettingsPanel(
        YokkoGameplaySettings settings,
        YokkoAudioSettings audioSettings)
    {
        this.settings = settings;
        this.audioSettings = audioSettings;
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
            new Container
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
                    new Circle
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
                        Icon = FontAwesome.Solid.Gamepad,
                        Colour = HomeControlColours.Navy,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 96,
                        Width = 600,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 3),
                        Children = new Drawable[]
                        {
                            new SpriteText
                            {
                                Text = YokkoStrings.Get(
                                    "settings.gameplay.ready"),
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
                    createLiveBadge(),
                },
            },
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

        foreach (Bindable<Key> binding in settings.FourKeyBindings)
            binding.BindValueChanged(onBindingChanged);
        foreach (Bindable<Key> binding in settings.SevenKeyBindings)
            binding.BindValueChanged(onBindingChanged);

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
        selectedKeyMode = keyMode;
        showInputSection(true);
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
    }

    internal void SetScrollSpeed(double speed) =>
        settings.SetScrollSpeed(speed);

    internal Key GetBinding(KeyMode keyMode, int lane) =>
        settings.GetKeys(keyMode)[lane];

    internal void SetLanePressFeedback(bool enabled) =>
        settings.ShowLanePressFeedback.Value = enabled;

    internal bool HandleKeyDown(Key key)
    {
        if (capturingCard == null)
            return false;

        if (key == Key.Escape)
        {
            cancelCapture();
            return true;
        }

        if (sequentialCapture)
        {
            if (sequentialKeys.Contains(key))
            {
                keyCaptureHint.Text = YokkoStrings.Get(
                    "settings.gameplay.sequence_duplicate");
                capturingCard.ShowDuplicate();
                return true;
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
                return true;
            }

            capturingCard = bindingCards[sequentialKeys.Count];
            capturingCard.SetCapturing(true);
            refreshSequentialHint();
            return true;
        }

        int lane = bindingCards.IndexOf(capturingCard);
        settings.SetBinding(selectedKeyMode, lane, key);
        cancelCapture();
        return true;
    }

    public bool DismissTransientUi()
    {
        if (capturingCard == null)
            return false;

        cancelCapture();
        return true;
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
            FontAwesome.Solid.Keyboard);
        addTab(
            flow,
            GameplaySettingsSection.Timing,
            YokkoStrings.Get("settings.gameplay.section_timing"),
            FontAwesome.Solid.WaveSquare);
        addTab(
            flow,
            GameplaySettingsSection.Feedback,
            YokkoStrings.Get("settings.gameplay.section_feedback"),
            FontAwesome.Solid.Heartbeat);

        return flow;
    }

    private void addTab(
        FillFlowContainer flow,
        GameplaySettingsSection section,
        LocalisableString label,
        IconUsage icon)
    {
        var tab = new GameplaySectionTab(
            label,
            icon,
            () => showSection(section, true),
            274);
        tab.Value = section;
        sectionTabs.Add(tab);
        flow.Add(tab);
    }

    private void showSection(
        GameplaySettingsSection section,
        bool animate)
    {
        cancelCapture();
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

            case GameplaySettingsSection.Feedback:
                setContent(createFeedbackSection(), animate);
                break;
        }
    }

    private void showInputSection(bool animate)
    {
        bindingCards.Clear();

        var panel = createPanel();
        setPanelChildren(panel, new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(20, 18),
                Text = YokkoStrings.Get("settings.gameplay.key_profile"),
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Navy,
            },
            new GameplayCompactButton(
                "4K",
                () => SelectKeyMode(KeyMode.FourKey),
                72)
            {
                Position = new Vector2(154, 10),
                IsSelected = selectedKeyMode == KeyMode.FourKey,
            },
            new GameplayCompactButton(
                "7K",
                () => SelectKeyMode(KeyMode.SevenKey),
                72)
            {
                Position = new Vector2(232, 10),
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
            keyCaptureHint = new SpriteText
            {
                Position = new Vector2(20, 66),
                Text = YokkoStrings.Get(
                    "settings.gameplay.key_capture_hint"),
                Font = HomeTypography.Body(15),
                Colour = SettingsTheme.MutedNavy,
            },
            createBindingCards(),
            new SpriteText
            {
                Position = new Vector2(20, 253),
                Text = YokkoStrings.Get(
                    "settings.gameplay.key_swap_hint"),
                Font = HomeTypography.Body(14),
                Colour = SettingsTheme.MutedNavy,
            },
        });

        setContent(panel, animate);
    }

    private Drawable createBindingCards()
    {
        IReadOnlyList<Bindable<Key>> bindings =
            settings.GetBindableKeys(selectedKeyMode);
        float spacing = 10;
        float width = (800 - spacing * (bindings.Count - 1)) /
                      bindings.Count;
        var flow = new FillFlowContainer
        {
            Position = new Vector2(20, 96),
            Size = new Vector2(800, 140),
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(spacing, 0),
        };

        for (int lane = 0; lane < bindings.Count; lane++)
        {
            int capturedLane = lane;
            var card = new GameplayBindingCard(
                lane,
                bindings[lane],
                () => BeginKeyCapture(capturedLane),
                width);
            bindingCards.Add(card);
            flow.Add(card);
        }

        return flow;
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
                Position = new Vector2(20, 154),
                Size = new Vector2(800, 1),
                Colour = SettingsTheme.Divider,
            },
            createControlLabel(
                YokkoStrings.Get("settings.gameplay.input_offset"),
                YokkoStrings.Get(
                    "settings.gameplay.input_offset_note"),
                20,
                178),
            new GameplayValueStepper(
                audioSettings.UserOffsetMilliseconds,
                1,
                -200,
                200,
                value => $"{value:+0;-0;0} ms")
            {
                Position = new Vector2(430, 174),
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
                Position = new Vector2(20, 88),
                Size = new Vector2(252, 158),
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
                },
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

    private static Drawable createLiveBadge() => new Container
    {
        Anchor = Anchor.CentreRight,
        Origin = Anchor.CentreRight,
        X = -22,
        Size = new Vector2(116, 30),
        Masking = true,
        CornerRadius = 15,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = YokkoStrings.Get("settings.gameplay.live"),
                Font = HomeTypography.Display(14),
                Spacing = new Vector2(0.8f, 0),
                Colour = HomeControlColours.Navy,
            },
        },
    };

    private void refreshStatusMetadata()
    {
        statusMetadata.Text = YokkoStrings.Get(
            "settings.gameplay.ready_metadata",
            formatKeys(settings.FourKeyBindings),
            formatKeys(settings.SevenKeyBindings));
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
                "settings.gameplay.key_capture_hint");
        }
    }

    private void refreshSequentialHint()
    {
        keyCaptureHint.Text = YokkoStrings.Get(
            "settings.gameplay.sequence_hint",
            sequentialKeys.Count + 1,
            bindingCards.Count);
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
            foreach (Bindable<Key> binding in settings.FourKeyBindings)
                binding.ValueChanged -= onBindingChanged;
            foreach (Bindable<Key> binding in settings.SevenKeyBindings)
                binding.ValueChanged -= onBindingChanged;
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
    private bool capturing;

    public GameplayBindingCard(
        int lane,
        Bindable<Key> binding,
        Action action,
        float width)
    {
        this.binding = binding;
        Action = action;
        Size = new Vector2(width, 140);
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
                Y = 13,
                Text = YokkoStrings.Get(
                    "settings.gameplay.lane",
                    lane + 1),
                Font = HomeTypography.Body(14),
                Colour = SettingsTheme.MutedNavy,
            },
            keyText = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Y = -3,
                Font = HomeTypography.Display(28),
                Colour = HomeControlColours.Navy,
            },
            actionText = new SpriteText
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = -14,
                Text = YokkoStrings.Get(
                    "settings.gameplay.click_to_change"),
                Font = HomeTypography.Body(14),
                Colour = SettingsTheme.MutedNavy,
            },
        };

        binding.BindValueChanged(onBindingChanged, true);
    }

    public void SetCapturing(bool isCapturing)
    {
        capturing = isCapturing;
        background.FadeColour(
            capturing ? HomeControlColours.Navy : SettingsTheme.PaleCyan,
            140,
            Easing.OutQuint);
        laneText.FadeColour(
            capturing ? SettingsTheme.StatusCyan : SettingsTheme.MutedNavy,
            120,
            Easing.OutQuint);
        keyText.Text = capturing
            ? YokkoStrings.Get("settings.gameplay.press_key")
            : displayKey(binding.Value);
        keyText.Font = HomeTypography.Display(capturing ? 15 : 28);
        keyText.FadeColour(
            capturing ? Color4.White : HomeControlColours.Navy,
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
        keyText.Font = HomeTypography.Display(28);
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

    private void onBindingChanged(ValueChangedEvent<Key> change)
    {
        if (!capturing)
            keyText.Text = displayKey(change.NewValue);
    }

    private static string displayKey(Key key) =>
        KeyModeBindings.FormatKey(key).ToUpperInvariant();

    protected override bool OnHover(HoverEvent e)
    {
        if (!capturing)
            background.FadeColour(SettingsTheme.StatusCyan, 110, Easing.OutQuint);

        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        if (!capturing)
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

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            isSelected = value;
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

    private void refresh()
    {
        if (background == null)
            return;

        background.Colour = isSelected
            ? HomeControlColours.Navy
            : Color4.White;
        text.Colour = isSelected ? Color4.White : HomeControlColours.Navy;

        if (icon != null)
            icon.Colour = isSelected ? SettingsTheme.StatusCyan : HomeControlColours.Pink;
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!isSelected)
            background.FadeColour(SettingsTheme.PaleCyan, 100, Easing.OutQuint);

        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        if (!isSelected)
            background.FadeColour(Color4.White, 120, Easing.OutQuint);
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

internal partial class GameplayStepperButton : ClickableContainer
{
    private readonly Box background;

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
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(SettingsTheme.PaleCyan, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        background.FadeColour(Color4.Transparent, 120, Easing.OutQuint);
}

internal partial class GameplayToggleCard : ClickableContainer
{
    private readonly BindableBool value;
    private readonly Box background;
    private readonly Box switchTrack;
    private readonly Circle switchThumb;
    private readonly SpriteText stateText;

    public GameplayToggleCard(
        LocalisableString title,
        LocalisableString note,
        IconUsage itemIcon,
        BindableBool value)
    {
        this.value = value;
        Action = () => value.Value = !value.Value;
        Size = new Vector2(257, 158);
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
                Position = new Vector2(18, 16),
                Size = new Vector2(34),
                Colour = SettingsTheme.PaleCyan,
            },
            new SpriteIcon
            {
                Position = new Vector2(26, 24),
                Size = new Vector2(18),
                Icon = itemIcon,
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(62, 19),
                Text = title,
                Font = HomeTypography.Display(16),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(18, 63),
                Text = note,
                Font = HomeTypography.Body(14),
                Colour = SettingsTheme.MutedNavy,
            },
            new Container
            {
                Position = new Vector2(18, 112),
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
                Position = new Vector2(78, 114),
                Font = HomeTypography.Display(14),
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

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            value.ValueChanged -= onValueChanged;

        base.Dispose(isDisposing);
    }
}
