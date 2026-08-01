using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Mods;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>
/// Legacy authored 1600x900 artwork coordinates hosted inside the responsive
/// 1920x1080 <see cref="GameplayModsScreen"/>. This is not a full-screen layout
/// reference. It intentionally owns only presentation and interaction;
/// <see cref="GameplayModsScreen"/> remains the single source of truth for mod
/// selection and configuration.
/// </summary>
internal partial class GameplayModsOrbitWorkspace : CompositeDrawable
{
    private const float orbit_host_resting_x = 335;
    private static readonly Vector2 authored_size = new(1600, 900);

    private static readonly ManiaModCategory[] pages =
    [
        ManiaModCategory.DifficultyReduction,
        ManiaModCategory.DifficultyIncrease,
        ManiaModCategory.Conversion,
        ManiaModCategory.Automation,
        ManiaModCategory.Fun,
    ];

    private readonly Action<ManiaModCategory> selectCategory;
    private readonly Action<ManiaModId> toggleMod;
    private readonly Action<IReadOnlyList<ManiaModId>> cycleModFamily;
    private readonly Action<ManiaModId> focusMod;
    private readonly Action<int> changeNoPauseAllowance;
    private readonly Action<double> previewRate;
    private readonly Action completeRate;
    private readonly Action reset;
    private readonly Action back;
    private readonly Action done;
    private readonly Func<ManiaModCategory, IReadOnlyList<ManiaModDefinition>>
        definitionsForCategory;
    private readonly Func<ManiaModId, bool> isSelectable;

    private readonly Dictionary<ManiaModCategory, OrbitCategoryButton>
        categoryButtons = new();
    private readonly Dictionary<ManiaModId, OrbitModNode> nodes = new();
    private readonly Dictionary<ManiaModId, IReadOnlyList<ManiaModId>>
        nodeFamilies = new();
    private readonly List<OrbitConnector> connectors = new();
    private readonly List<OrbitRatePresetButton> ratePresets = new();
    private readonly List<Circle> capacityDots = new();
    private readonly List<Action> loadAnimations = new();

    private Container orbitHost;
    private Container authoredContent;
    private Container nodeHost;
    private Container activeRows;
    private Container hero;
    private SpriteText heroAcronym;
    private SpriteText heroName;
    private TextFlowContainer heroDescription;
    private Box heroStateBackground;
    private Sprite heroStateIcon;
    private SpriteText heroState;
    private Container noPauseAllowanceControl;
    private SpriteText noPauseAllowanceValue;
    private OrbitSquareButton noPauseMinus;
    private OrbitSquareButton noPausePlus;
    private SpriteText pageIndicator;
    private SpriteText rateValue;
    private SpriteText activeCount;
    private SpriteText orbitTelemetryState;
    private SpriteText capacityTelemetry;
    private OrbitRateSlider rateSlider;
    private OrbitSignalScanner orbitScanner;
    private OrbitSquareButton rateMinus;
    private OrbitSquareButton ratePlus;
    private ManiaModCategory category;
    private ManiaModId focusedMod;
    private ManiaModSet selectedMods = ManiaModSet.Empty;
    private double displayedRate = 1;
    private bool built;
    private bool stateInitialized;

    internal IReadOnlyCollection<ManiaModId> VisibleMods => nodes.Keys;
    internal bool RepresentsMod(ManiaModId mod) =>
        nodeFamilies.Values.Any(family => family.Contains(mod));
    internal void CycleNode(ManiaModId mod)
    {
        ManiaModId? node = nodes.Keys
            .Cast<ManiaModId?>()
            .FirstOrDefault(candidate =>
                nodeRepresents(candidate!.Value, mod));
        if (node.HasValue)
            cycleModFamily(familyForNode(node.Value));
    }
    internal string ActiveCountText => activeCount?.Text.ToString() ?? string.Empty;
    internal string CapacityTelemetryText =>
        capacityTelemetry?.Text.ToString() ?? string.Empty;
    private ManiaModId? pendingTransitionMod;
    private bool pendingTransitionActive;

    internal GameplayModsOrbitWorkspace(
        Action<ManiaModCategory> selectCategory,
        Action<ManiaModId> toggleMod,
        Action<IReadOnlyList<ManiaModId>> cycleModFamily,
        Action<ManiaModId> focusMod,
        Action<int> changeNoPauseAllowance,
        Action<double> previewRate,
        Action completeRate,
        Action reset,
        Action back,
        Action done,
        Func<ManiaModCategory, IReadOnlyList<ManiaModDefinition>>
            definitionsForCategory,
        Func<ManiaModId, bool> isSelectable)
    {
        this.selectCategory = selectCategory;
        this.toggleMod = toggleMod;
        this.cycleModFamily = cycleModFamily;
        this.focusMod = focusMod;
        this.changeNoPauseAllowance = changeNoPauseAllowance;
        this.previewRate = previewRate;
        this.completeRate = completeRate;
        this.reset = reset;
        this.back = back;
        this.done = done;
        this.definitionsForCategory = definitionsForCategory;
        this.isSelectable = isSelectable;

        Size = authored_size;
    }

    internal void Build(
        Texture logo,
        Texture paperTexture,
        Texture waveformTexture)
    {
        if (built)
            return;

        built = true;
        InternalChildren =
        [
            new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                Texture = paperTexture,
                FillMode = FillMode.Fill,
                Alpha = 0.16f,
            },
            authoredContent = new Container
            {
                Size = authored_size,
                Children =
                [
                    createHeader(logo),
                    createCategoryRail(),
                    createOrbit(waveformTexture),
                    createRightPanel(),
                    createDecorations(waveformTexture),
                ],
            },
            createFooter(),
        ];
    }

    internal void SetViewportSize(Vector2 viewport)
    {
        Size = new Vector2(
            MathF.Max(viewport.X, authored_size.X),
            MathF.Max(viewport.Y, authored_size.Y));

        if (authoredContent != null)
            authoredContent.Position = CalculateAuthoredContentOffset(Size);
    }

    internal static Vector2 CalculateAuthoredContentOffset(
        Vector2 workspaceSize) =>
        new(
            MathF.Max(workspaceSize.X - authored_size.X, 0) / 2,
            MathF.Max(workspaceSize.Y - authored_size.Y, 0) / 2);

    protected override void LoadComplete()
    {
        base.LoadComplete();
        foreach (Action animation in loadAnimations)
            animation();
        loadAnimations.Clear();
    }

    internal void QueueModTransition(ManiaModId mod, bool active)
    {
        pendingTransitionMod = mod;
        pendingTransitionActive = active;
    }

    internal void SetState(
        ManiaModCategory nextCategory,
        ManiaModId nextFocusedMod,
        ManiaModSet nextSelectedMods)
    {
        if (!built)
            return;

        HashSet<ManiaModId> previousActive = selectedMods.Mods
            .Where(mod => !isKeyConversionMod(mod))
            .ToHashSet();
        HashSet<ManiaModId> nextActive = nextSelectedMods.Mods
            .Where(mod => !isKeyConversionMod(mod))
            .ToHashSet();
        HashSet<ManiaModId> activated = stateInitialized
            ? nextActive.Except(previousActive).ToHashSet()
            : [];
        HashSet<ManiaModId> deactivated = stateInitialized
            ? previousActive.Except(nextActive).ToHashSet()
            : [];
        if (stateInitialized
            && pendingTransitionMod is ManiaModId pendingMod)
        {
            if (pendingTransitionActive)
                activated.Add(pendingMod);
            else
                deactivated.Add(pendingMod);
        }
        pendingTransitionMod = null;
        bool activeSelectionChanged =
            !previousActive.SetEquals(nextActive);
        bool rebuildOrbit = category != nextCategory || nodes.Count == 0;
        category = nextCategory;
        focusedMod = nextFocusedMod;
        selectedMods = nextSelectedMods;

        updateCategorySelection(category);

        if (rebuildOrbit)
            rebuildOrbitNodes();

        foreach ((ManiaModId mod, OrbitModNode node) in nodes)
        {
            node.SetPresentation(OsuManiaModParityCatalog.Get(
                presentationModForNode(mod)));
            node.SetState(
                isNodeActive(mod),
                nodeRepresents(mod, focusedMod),
                isSelectable(mod),
                transitionContains(activated, mod),
                transitionContains(deactivated, mod));
        }

        foreach (OrbitConnector connector in connectors)
        {
            bool startActivated =
                transitionContains(activated, connector.StartMod);
            bool endActivated =
                transitionContains(activated, connector.EndMod);
            bool startDeactivated =
                transitionContains(deactivated, connector.StartMod);
            bool endDeactivated =
                transitionContains(deactivated, connector.EndMod);
            bool hasTransition =
                startActivated || endActivated
                || startDeactivated || endDeactivated;
            connector.SetState(
                isNodeActive(connector.StartMod)
                || isNodeActive(connector.EndMod),
                nodeRepresents(connector.StartMod, focusedMod)
                || nodeRepresents(connector.EndMod, focusedMod),
                hasTransition,
                startActivated || startDeactivated,
                startActivated || endActivated);
        }

        ManiaModDefinition focusedDefinition =
            OsuManiaModParityCatalog.Get(
                presentationModForNode(focusedMod));
        orbitTelemetryState.Text =
            $"FOCUS {focusedDefinition.Acronym}  //  ACTIVE {selectedMods.Mods.Count:00}";
        orbitScanner.SetAccent(accentFor(focusedDefinition));

        updateHero(
            transitionContains(activated, focusedMod),
            transitionContains(deactivated, focusedMod));
        if (!stateInitialized || activeSelectionChanged)
            updateActiveRows(activated);
        updateRate(selectedMods.PlaybackRate, selectedMods.FixedRateMod != null);
        stateInitialized = true;
    }

    internal void PreviewRate(double value)
    {
        displayedRate = value;
        rateValue.Text = $"{value:0.00}x";
        rateSlider.SetState(true, 0.5, 2, value);
        updateRatePresetState(value);
    }

    internal void PreviewCategorySelection(ManiaModCategory nextCategory)
    {
        if (!built || !pages.Contains(nextCategory))
            return;

        updateCategorySelection(nextCategory);
    }

    internal ManiaModId? GetAdjacentMod(ManiaModId current, int offset)
    {
        ManiaModId[] visible = nodes.Keys.ToArray();
        if (visible.Length == 0)
            return null;

        current = nodes.Keys.FirstOrDefault(
            node => nodeRepresents(node, current),
            current);

        int currentIndex = Array.IndexOf(visible, current);
        if (currentIndex < 0)
            return visible[0];

        return visible[
            (currentIndex + Math.Sign(offset) + visible.Length)
            % visible.Length];
    }

    private ManiaModId presentationModForNode(ManiaModId nodeMod)
    {
        IReadOnlyList<ManiaModId> family = familyForNode(nodeMod);
        ManiaModId? active = family
            .Cast<ManiaModId?>()
            .FirstOrDefault(mod => selectedMods.Contains(mod!.Value));
        if (active.HasValue)
            return active.Value;

        return family.Contains(focusedMod) ? focusedMod : nodeMod;
    }

    private bool isNodeActive(ManiaModId nodeMod) =>
        familyForNode(nodeMod).Any(selectedMods.Contains);

    private bool nodeRepresents(
        ManiaModId nodeMod,
        ManiaModId representedMod) =>
        familyForNode(nodeMod).Contains(representedMod);

    private bool transitionContains(
        IReadOnlySet<ManiaModId> transitions,
        ManiaModId nodeMod) =>
        familyForNode(nodeMod).Any(transitions.Contains);

    private IReadOnlyList<ManiaModId> familyForNode(ManiaModId nodeMod) =>
        nodeFamilies.GetValueOrDefault(nodeMod) ?? [nodeMod];

    internal void TransitionOut(int direction)
    {
        orbitHost.ClearTransforms();
        orbitHost
            .MoveToX(
                orbit_host_resting_x - Math.Sign(direction) * 18,
                82,
                Easing.OutQuint)
            .FadeTo(0.06f, 72, Easing.OutQuint);
    }

    internal void TransitionIn(int direction)
    {
        orbitHost.ClearTransforms();
        orbitHost.X =
            orbit_host_resting_x + Math.Sign(direction) * 26;
        orbitHost.Alpha = 0;
        orbitHost
            .FadeIn(118, Easing.OutQuint)
            .MoveToX(
                orbit_host_resting_x,
                172,
                Easing.OutQuint);
    }

    internal float OrbitContentX => orbitHost?.X ?? orbit_host_resting_x;

    private void updateCategorySelection(ManiaModCategory selectedCategory)
    {
        foreach ((ManiaModCategory page, OrbitCategoryButton button)
                 in categoryButtons)
        {
            button.SetSelected(page == selectedCategory);
        }

        pageIndicator.Text =
            $"{Array.IndexOf(pages, selectedCategory) + 1:00} / {pages.Length:00}";
    }

    private Drawable createHeader(Texture logo) => new Container
    {
        RelativeSizeAxes = Axes.X,
        Height = 132,
        Children =
        [
            new Sprite
            {
                Position = new Vector2(74, 28),
                Size = new Vector2(350, 119),
                Texture = logo,
            },
            new Box
            {
                Position = new Vector2(410, 36),
                Size = new Vector2(1.5f, 74),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.35f),
            },
            new SpriteText
            {
                Position = new Vector2(438, 34),
                Text = YokkoStrings.Get("mods.title"),
                Font = HomeTypography.Hero(44),
                Scale = new Vector2(1.02f, 1),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(440, 88),
                Text = YokkoStrings.Get("mods.subtitle"),
                Font = HomeTypography.Body(19),
                Colour = HomeControlColours.Cyan,
            },
        ],
    };

    private Drawable createCategoryRail()
    {
        var container = new Container
        {
            Position = new Vector2(30, 202),
            Size = new Vector2(292, 540),
        };

        container.Add(new Box
        {
            Position = new Vector2(49, 20),
            Size = new Vector2(1, 445),
            Colour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.34f),
        });

        LocalisableString[] labels =
        [
            YokkoStrings.Get("mods.category.difficulty_down"),
            YokkoStrings.Get("mods.category.difficulty_up"),
            YokkoStrings.Get("mods.category.conversion"),
            YokkoStrings.Get("mods.category.automation"),
            YokkoStrings.Get("mods.category.fun"),
        ];
        IconUsage[] icons =
        [
            FontAwesome.Solid.ChevronDown,
            FontAwesome.Solid.ChevronUp,
            FontAwesome.Solid.LayerGroup,
            FontAwesome.Solid.Cog,
            FontAwesome.Solid.Star,
        ];
        Color4[] accents =
        [
            HomeControlColours.Cyan,
            HomeControlColours.Pink,
            HomeControlColours.Cyan,
            HomeControlColours.Yellow,
            HomeControlColours.Pink,
        ];

        for (int i = 0; i < pages.Length; i++)
        {
            ManiaModCategory page = pages[i];
            var button = new OrbitCategoryButton(
                i + 1,
                labels[i],
                icons[i],
                accents[i],
                () => selectCategory(page))
            {
                Y = i * 92,
            };
            categoryButtons[page] = button;
            container.Add(button);
        }

        container.Add(new OrbitRailArrow(
            FontAwesome.Solid.ChevronLeft,
            () => selectRelativePage(-1))
        {
            Position = new Vector2(7, 489),
        });
        container.Add(pageIndicator = new SpriteText
        {
            Position = new Vector2(76, 494),
            Font = HomeTypography.Display(18),
            Colour = HomeControlColours.Pink,
        });
        container.Add(new OrbitRailArrow(
            FontAwesome.Solid.ChevronRight,
            () => selectRelativePage(1))
        {
            Position = new Vector2(190, 489),
        });

        return container;
    }

    private Drawable createOrbit(Texture waveformTexture)
    {
        orbitHost = new Container
        {
            Position = new Vector2(orbit_host_resting_x, 128),
            Size = new Vector2(790, 620),
        };

        orbitHost.Add(createRing(new Vector2(54, 122), 390, 1f, 0.28f));
        orbitHost.Add(createRing(new Vector2(72, 140), 354, 1.2f, 0.48f));
        orbitHost.Add(createRing(new Vector2(94, 162), 310, 1f, 0.2f));
        orbitHost.Add(createRing(new Vector2(119, 187), 260, 1f, 0.16f));
        orbitHost.Add(createRingArc(
            new Vector2(-35, 30),
            570,
            new Vector2(20, 45),
            new Vector2(350, 75),
            new Color4(HomeControlColours.Cyan.R, HomeControlColours.Cyan.G, HomeControlColours.Cyan.B, 0.68f),
            1.2f));
        orbitHost.Add(createRingArc(
            new Vector2(15, 80),
            470,
            new Vector2(15, 190),
            new Vector2(45, 210),
            new Color4(HomeControlColours.Pink.R, HomeControlColours.Pink.G, HomeControlColours.Pink.B, 0.64f),
            1.2f));
        orbitHost.Add(createRingArc(
            new Vector2(-35, 30),
            570,
            new Vector2(65, 480),
            new Vector2(360, 70),
            new Color4(HomeControlColours.Cyan.R, HomeControlColours.Cyan.G, HomeControlColours.Cyan.B, 0.68f),
            1.2f));
        orbitHost.Add(createPulseMarker(
            new Vector2(102, 238),
            HomeControlColours.Cyan,
            0));
        orbitHost.Add(createPulseMarker(
            new Vector2(247, 118),
            HomeControlColours.Cyan,
            420));
        orbitHost.Add(createPulseMarker(
            new Vector2(394, 292),
            HomeControlColours.Pink,
            840));
        var healthPulse = new SpriteIcon
        {
            Position = new Vector2(403, 283),
            Size = new Vector2(18),
            Icon = FontAwesome.Solid.Heartbeat,
            Colour = HomeControlColours.Pink,
        };
        loadAnimations.Add(() =>
            healthPulse.ScaleTo(0.9f)
                       .Then().ScaleTo(1.16f, 420, Easing.OutQuint)
                       .Then().ScaleTo(0.9f, 520, Easing.InOutSine)
                       .Loop(760));
        orbitHost.Add(healthPulse);
        orbitHost.Add(createOrbitScale());
        orbitHost.Add(createOrbitTelemetry());
        orbitHost.Add(orbitScanner = new OrbitSignalScanner
        {
            Position = new Vector2(249, 317),
        });
        orbitHost.Add(hero = createHero(waveformTexture));
        orbitHost.Add(nodeHost = new Container
        {
            RelativeSizeAxes = Axes.Both,
        });
        return orbitHost;
    }

    private Drawable createOrbitScale()
    {
        var scale = new Container
        {
            RelativeSizeAxes = Axes.Both,
        };
        Vector2 centre = new(249, 317);
        const float radius = 205;
        for (int i = 0; i < 24; i++)
        {
            float angle = MathF.PI * 2 * i / 24;
            bool major = i % 6 == 0;
            Vector2 direction = new(MathF.Sin(angle), -MathF.Cos(angle));
            scale.Add(new Box
            {
                Origin = Anchor.Centre,
                Position = centre + direction * radius,
                Size = new Vector2(major ? 13 : 6, major ? 1.4f : 0.8f),
                Rotation = i * 15,
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    major ? 0.52f : 0.22f),
            });
        }

        (string Text, Vector2 Position)[] labels =
        [
            ("N 00", new Vector2(228, 92)),
            ("E 09", new Vector2(463, 306)),
            ("S 18", new Vector2(229, 526)),
            ("W 27", new Vector2(18, 306)),
        ];
        foreach ((string text, Vector2 position) in labels)
        {
            scale.Add(new SpriteText
            {
                Position = position,
                Text = text,
                Font = HomeTypography.Display(9),
                Spacing = new Vector2(0.8f, 0),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.38f),
            });
        }
        return scale;
    }

    private Drawable createOrbitTelemetry() => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Children =
        [
            new SpriteText
            {
                Position = new Vector2(74, 106),
                Text = "SYNC // MOD MATRIX",
                Font = HomeTypography.Display(11),
                Spacing = new Vector2(1.1f, 0),
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    0.66f),
            },
            orbitTelemetryState = new SpriteText
            {
                Position = new Vector2(74, 88),
                Text = "FOCUS --  //  ACTIVE 00",
                Font = HomeTypography.Display(10),
                Spacing = new Vector2(1.1f, 0),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.48f),
            },
            new SpriteText
            {
                Position = new Vector2(360, 520),
                Text = "SIGNAL  06",
                Font = HomeTypography.Display(10),
                Spacing = new Vector2(1.1f, 0),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.48f),
            },
            new Box
            {
                Position = new Vector2(72, 120),
                Size = new Vector2(54, 1),
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    0.38f),
            },
            new Box
            {
                Position = new Vector2(422, 519),
                Size = new Vector2(28, 1),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.24f),
            },
        ],
    };

    private Drawable createRing(
        Vector2 position,
        float size,
        float thickness,
        float alpha) => new Circle
    {
        Position = position,
        Size = new Vector2(size),
        Masking = true,
        BorderThickness = thickness,
        BorderColour = new Color4(
            HomeControlColours.Cyan.R,
            HomeControlColours.Cyan.G,
            HomeControlColours.Cyan.B,
            alpha),
        Child = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = Color4.Transparent,
        },
    };

    private Drawable createPulseMarker(
        Vector2 position,
        Color4 colour,
        double delay)
    {
        var marker = new Circle
        {
            Position = position,
            Size = new Vector2(10),
            Masking = true,
            BorderThickness = 1.5f,
            BorderColour = colour,
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
        };
        loadAnimations.Add(() =>
            marker.Delay(delay)
                  .ScaleTo(0.72f)
                  .FadeTo(0.4f)
                  .Then().ScaleTo(1.18f, 620, Easing.OutQuint)
                  .FadeTo(1, 620, Easing.OutQuint)
                  .Then().ScaleTo(0.72f, 620, Easing.InOutSine)
                  .FadeTo(0.4f, 620, Easing.InOutSine)
                  .Loop(720));
        return marker;
    }

    private Drawable createRingArc(
        Vector2 ringPosition,
        float ringSize,
        Vector2 cropPosition,
        Vector2 cropSize,
        Color4 colour,
        float thickness) => new Container
    {
        Position = cropPosition,
        Size = cropSize,
        Masking = true,
        Child = new Circle
        {
            Position = ringPosition - cropPosition,
            Size = new Vector2(ringSize),
            Masking = true,
            BorderThickness = thickness,
            BorderColour = colour,
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Transparent,
            },
        },
    };

    private Container createHero(Texture waveformTexture)
    {
        var result = new OrbitHeroPanel(
            () => toggleMod(focusedMod))
        {
            Position = new Vector2(105, 160),
            Size = new Vector2(300, 266),
        };
        result.Children =
        [
            heroAcronym = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 5,
                Font = HomeTypography.Hero(146),
                Colour = HomeControlColours.Pink,
            },
            heroName = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 132,
                Font = HomeTypography.Display(26),
                Colour = HomeControlColours.Navy,
            },
            heroDescription = new TextFlowContainer(text =>
            {
                text.Font = HomeTypography.Body(18);
                text.Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.72f);
            })
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 166),
                Width = 218,
                AutoSizeAxes = Axes.Y,
                TextAnchor = Anchor.TopCentre,
            },
            heroStateBackground = new Box
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 226),
                Size = new Vector2(138, 36),
                Colour = HomeControlColours.Pink,
            },
            heroState = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 226,
                Font = HomeTypography.Display(16),
                Padding = new MarginPadding
                {
                    Horizontal = 14,
                    Vertical = 7,
                },
                Colour = Color4.White,
            },
            heroStateIcon = new Sprite
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(-45, 235),
                Size = new Vector2(36, 10),
                Texture = waveformTexture,
                Colour = Color4.White,
            },
            noPauseAllowanceControl = new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 220),
                Size = new Vector2(218, 42),
                Alpha = 0,
                Children =
                [
                    noPauseMinus = new OrbitSquareButton(
                        FontAwesome.Solid.Minus,
                        () => changeNoPauseAllowance(
                            Math.Max(0, selectedMods.NoPauseAllowedPauses - 1)))
                    {
                        Size = new Vector2(42),
                    },
                    noPauseAllowanceValue = new SpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Y = 10,
                        Font = HomeTypography.Display(17),
                        Colour = HomeControlColours.Navy,
                    },
                    noPausePlus = new OrbitSquareButton(
                        FontAwesome.Solid.Plus,
                        () => changeNoPauseAllowance(
                            Math.Min(10, selectedMods.NoPauseAllowedPauses + 1)))
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Size = new Vector2(42),
                    },
                ],
            },
        ];
        return result;
    }

    private Drawable createRightPanel()
    {
        var panel = new Container
        {
            Position = new Vector2(1145, 132),
            Size = new Vector2(425, 600),
        };
        panel.Children =
        [
            new Box
            {
                Size = new Vector2(1, 600),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.22f),
            },
            new Box
            {
                Position = new Vector2(26, 18),
                Size = new Vector2(18, 1),
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    0.38f),
            },
            new Box
            {
                Position = new Vector2(26, 18),
                Size = new Vector2(1, 18),
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    0.38f),
            },
            new Box
            {
                Position = new Vector2(384, 232),
                Size = new Vector2(16, 1),
                Colour = new Color4(
                    HomeControlColours.Pink.R,
                    HomeControlColours.Pink.G,
                    HomeControlColours.Pink.B,
                    0.34f),
            },
            new Box
            {
                Position = new Vector2(399, 217),
                Size = new Vector2(1, 16),
                Colour = new Color4(
                    HomeControlColours.Pink.R,
                    HomeControlColours.Pink.G,
                    HomeControlColours.Pink.B,
                    0.34f),
            },
            new SpriteText
            {
                Position = new Vector2(41, 28),
                Text = YokkoStrings.Get("mods.speed_multiplier"),
                Font = HomeTypography.Display(19),
                Spacing = new Vector2(1.1f, 0),
                Colour = HomeControlColours.Cyan,
            },
            new Box
            {
                Position = new Vector2(213, 40),
                Size = new Vector2(173, 1),
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    0.72f),
            },
            rateValue = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 72),
                Text = "1.00x",
                Font = HomeTypography.Hero(68),
                Colour = HomeControlColours.Navy,
            },
            rateMinus = new OrbitSquareButton(
                FontAwesome.Solid.Minus,
                () => previewRate(Math.Max(0.5, displayedRate - 0.01)))
            {
                Position = new Vector2(32, 162),
            },
            rateSlider = new OrbitRateSlider(
                previewRate,
                completeRate)
            {
                Position = new Vector2(77, 169),
            },
            ratePlus = new OrbitSquareButton(
                FontAwesome.Solid.Plus,
                () => previewRate(Math.Min(2, displayedRate + 0.01)))
            {
                Position = new Vector2(371, 162),
            },
            createRatePreset(0.75, 126),
            createRatePreset(1.00, 187),
            createRatePreset(1.50, 248),
            new SpriteText
            {
                Position = new Vector2(31, 207),
                Text = "0.50x",
                Font = HomeTypography.Body(15),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 207),
                Text = "1.00x",
                Font = HomeTypography.Body(15),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-5, 207),
                Text = "2.00x",
                Font = HomeTypography.Body(15),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(41, 266),
                Text = YokkoStrings.Get("mods.active_mods"),
                Font = HomeTypography.Display(20),
                Spacing = new Vector2(1.1f, 0),
                Colour = HomeControlColours.Cyan,
            },
            new Box
            {
                Position = new Vector2(168, 278),
                Size = new Vector2(191, 1),
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    0.72f),
            },
            activeCount = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-5, 266),
                Font = HomeTypography.Display(19),
                Colour = HomeControlColours.Cyan,
            },
            activeRows = new Container
            {
                Position = new Vector2(31, 300),
                Size = new Vector2(365, 264),
            },
            createRightCapacityRail(),
            new OrbitMicroBarGraph
            {
                Position = new Vector2(373, 47),
            },
        ];
        return panel;
    }

    private Drawable createRightCapacityRail()
    {
        var rail = new Container
        {
            Position = new Vector2(41, 572),
            Size = new Vector2(345, 28),
        };
        rail.Add(capacityTelemetry = new SpriteText
        {
            Text = "MOD BUS // 00 ACTIVE",
            Font = HomeTypography.Display(12),
            Spacing = new Vector2(0.8f, 0),
            Colour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.46f),
        });
        rail.Add(new Box
        {
            Position = new Vector2(128, 6),
            Size = new Vector2(132, 1),
            Colour = new Color4(
                HomeControlColours.Cyan.R,
                HomeControlColours.Cyan.G,
                HomeControlColours.Cyan.B,
                0.28f),
        });
        for (int i = 0; i < 5; i++)
        {
            var dot = new Circle
            {
                Position = new Vector2(272 + i * 15, 2),
                Size = new Vector2(7),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.16f),
            };
            capacityDots.Add(dot);
            rail.Add(dot);
        }
        return rail;
    }

    private Drawable createRatePreset(double value, float x)
    {
        var button = new OrbitRatePresetButton(
            value,
            () => previewRate(value))
        {
            Position = new Vector2(x, 137),
        };
        ratePresets.Add(button);
        return button;
    }

    private Drawable createDecorations(Texture waveformTexture)
    {
        var topDots = new HomeDotField
        {
            Position = new Vector2(1210, 34),
            Size = new Vector2(55, 32),
            Colour = HomeControlColours.Cyan,
            Alpha = 0.62f,
        };
        var waveformEcho = new Sprite
        {
            Origin = Anchor.Centre,
            Position = new Vector2(1432, 50),
            Size = new Vector2(288, 46),
            Texture = waveformTexture,
            Colour = HomeControlColours.Cyan,
            Alpha = 0.12f,
        };
        var waveform = new Sprite
        {
            Origin = Anchor.Centre,
            Position = new Vector2(1432, 50),
            Size = new Vector2(288, 46),
            Texture = waveformTexture,
        };
        var pinkPlus = new SpriteText
        {
            Origin = Anchor.Centre,
            Position = new Vector2(729, 89),
            Text = "+",
            Font = HomeTypography.Display(20),
            Colour = HomeControlColours.Pink,
        };
        var lowerDots = new HomeDotField
        {
            Position = new Vector2(260, 596),
            Size = new Vector2(45, 45),
            Colour = HomeControlColours.Cyan,
            Alpha = 0.52f,
        };

        loadAnimations.Add(() =>
        {
            topDots.FadeTo(0.34f)
                   .Then().FadeTo(0.68f, 1300, Easing.InOutSine)
                   .Then().FadeTo(0.34f, 1300, Easing.InOutSine)
                   .Loop();
            waveformEcho.ScaleTo(0.96f)
                        .FadeTo(0.06f)
                        .Then().ScaleTo(1.05f, 1450, Easing.InOutSine)
                        .FadeTo(0.22f, 1450, Easing.InOutSine)
                        .Then().ScaleTo(0.96f, 1450, Easing.InOutSine)
                        .FadeTo(0.06f, 1450, Easing.InOutSine)
                        .Loop();
            waveform.FadeTo(0.74f)
                    .Then().FadeTo(0.88f, 1100, Easing.InOutSine)
                    .Then().FadeTo(0.74f, 1100, Easing.InOutSine)
                    .Loop();
            pinkPlus.RotateTo(-7)
                    .Then().RotateTo(7, 1800, Easing.InOutSine)
                    .Then().RotateTo(-7, 1800, Easing.InOutSine)
                    .Loop();
            lowerDots.FadeTo(0.38f)
                     .Then().FadeTo(0.66f, 1700, Easing.InOutSine)
                     .Then().FadeTo(0.38f, 1700, Easing.InOutSine)
                     .Loop();
        });

        return new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children =
            [
            new Box
            {
                Position = new Vector2(18, 18),
                Size = new Vector2(1, 82),
                Colour = HomeControlColours.Navy,
            },
            new Box
            {
                Position = new Vector2(18, 18),
                Size = new Vector2(22, 1),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(12, 91),
                Text = "+",
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Cyan,
            },
            new Box
            {
                Position = new Vector2(18, 130),
                Size = new Vector2(1, 565),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.5f),
            },
            new SpriteText
            {
                Position = new Vector2(37, 141),
                Text = "+",
                Font = HomeTypography.Display(22),
                Colour = HomeControlColours.Yellow,
            },
            new SpriteIcon
            {
                Position = new Vector2(18, 718),
                Size = new Vector2(13),
                Icon = FontAwesome.Regular.Heart,
                Colour = HomeControlColours.Pink,
            },
            topDots,
            waveformEcho,
            waveform,
            new SpriteText
            {
                Position = new Vector2(1572, 39),
                Text = "+",
                Font = HomeTypography.Display(24),
                Colour = HomeControlColours.Yellow,
            },
            pinkPlus,
            lowerDots,
            new OrbitTechnicalBadge("INPUT ROUTE // 05")
            {
                Position = new Vector2(962, 92),
            },
            new OrbitEdgeTicks
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Position = new Vector2(-18, 386),
            },
            new SpriteText
            {
                Position = new Vector2(1518, 683),
                Text = "LIVE // 120HZ",
                Font = HomeTypography.Display(10),
                Spacing = new Vector2(0.8f, 0),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.42f),
            },
            new HomeDotField
            {
                Position = new Vector2(1040, 654),
                Size = new Vector2(42, 30),
                Colour = HomeControlColours.Yellow,
            },
            ],
        };
    }

    private Drawable createFooter()
    {
        var scanLine = new Box
        {
            Position = new Vector2(0, 3),
            Size = new Vector2(70, 2),
            Colour = Color4.White,
            Alpha = 0.28f,
        };
        var footerDots = new HomeDotField
        {
            Position = new Vector2(532, 82),
            Size = new Vector2(42, 28),
            Colour = new Color4(1, 1, 1, 0.38f),
        };

        loadAnimations.Add(() =>
        {
            scanLine.MoveToX(0)
                    .Then().MoveToX(1530, 4200, Easing.InOutSine)
                    .Loop(500);
            scanLine.FadeTo(0.12f)
                    .Then().FadeTo(0.7f, 900, Easing.InOutSine)
                    .Then().FadeTo(0.12f, 900, Easing.InOutSine)
                    .Loop();
            footerDots.FadeTo(0.3f)
                      .Then().FadeTo(0.78f, 1600, Easing.InOutSine)
                      .Then().FadeTo(0.3f, 1600, Easing.InOutSine)
                      .Loop();
        });

        return new Container
        {
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.BottomLeft,
            RelativeSizeAxes = Axes.X,
            Height = 130,
            Children =
            [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = ColourInfo.GradientHorizontal(
                    new Color4(0.13f, 0.72f, 0.90f, 1),
                    new Color4(0.24f, 0.82f, 0.93f, 1)),
            },
            new HomeDotField
            {
                Position = new Vector2(8, 75),
                Size = new Vector2(58, 38),
                Colour = new Color4(1, 1, 1, 0.52f),
            },
            new SpriteText
            {
                Position = new Vector2(626, 45),
                Text = "+",
                Font = HomeTypography.Display(24),
                Colour = Color4.White,
            },
            footerDots,
            scanLine,
            new Box
            {
                Position = new Vector2(548, 16),
                Size = new Vector2(1.5f, 98),
                Colour = new Color4(1, 1, 1, 0.68f),
            },
            new Box
            {
                Position = new Vector2(662, 32),
                Size = new Vector2(6),
                Colour = HomeControlColours.Pink,
            },
            new Box
            {
                Position = new Vector2(662, 50),
                Size = new Vector2(3, 9),
                Colour = new Color4(1, 1, 1, 0.74f),
            },
            new Box
            {
                Position = new Vector2(662, 65),
                Size = new Vector2(3, 9),
                Colour = new Color4(1, 1, 1, 0.74f),
            },
            new Box
            {
                Position = new Vector2(1138, 24),
                Size = new Vector2(8),
                Colour = HomeControlColours.Yellow,
            },
            new Box
            {
                Position = new Vector2(258, 22),
                Size = new Vector2(14),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
            },
            new Box
            {
                Position = new Vector2(1420, 4),
                Size = new Vector2(72, 7),
                Colour = HomeControlColours.Yellow,
            },
            new HomeHazardStripes(
                120,
                new Color4(1, 1, 1, 0.82f))
            {
                Position = new Vector2(112, 116),
            },
            new HomeHazardStripes(
                62,
                new Color4(1, 1, 1, 0.72f))
            {
                Position = new Vector2(905, 112),
            },
            new OrbitFooterButton(
                YokkoStrings.Get("mods.back"),
                FontAwesome.Solid.ChevronRight,
                back,
                OrbitFooterButtonStyle.Back,
                "ESC")
            {
                Position = new Vector2(88, 30),
            },
            new OrbitFooterButton(
                YokkoStrings.Get("mods.reset"),
                FontAwesome.Solid.Undo,
                reset,
                OrbitFooterButtonStyle.Reset)
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-376, 35),
            },
            new OrbitFooterButton(
                YokkoStrings.Get("mods.done"),
                FontAwesome.Solid.Play,
                done,
                OrbitFooterButtonStyle.Primary)
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-82, 29),
            },
            ],
        };
    }

    private void rebuildOrbitNodes()
    {
        nodeHost.Clear();
        nodes.Clear();
        nodeFamilies.Clear();
        connectors.Clear();

        IReadOnlyList<IReadOnlyList<ManiaModId>> families =
            orbitFamilies(category);
        IReadOnlyList<Vector2> positions =
            [
                new(365, 10),
                new(498, 109),
                new(530, 248),
                new(505, 386),
                new(430, 468),
                new(150, 524),
            ];

        Vector2? previousNodeCentre = null;
        ManiaModId? previousMod = null;
        for (int i = 0; i < families.Count; i++)
        {
            IReadOnlyList<ManiaModId> family = families[i];
            ManiaModDefinition definition =
                OsuManiaModParityCatalog.Get(family[0]);
            Vector2 position = positions[i];
            Vector2 nodeCentre = position + new Vector2(42);
            nodeFamilies[definition.Id] = family;
            if (previousNodeCentre.HasValue && previousMod.HasValue)
            {
                var connector = new OrbitConnector(
                    previousMod.Value,
                    definition.Id,
                    previousNodeCentre.Value,
                    nodeCentre,
                    i * 0.13);
                connectors.Add(connector);
                nodeHost.Add(connector);
            }
            previousNodeCentre = nodeCentre;
            previousMod = definition.Id;

            var node = new OrbitModNode(
                definition,
                accentFor(definition),
                i + 1,
                () => cycleModFamily(family),
                () => focusMod(definition.Id),
                false)
            {
                Position = position,
            };
            nodes[definition.Id] = node;
            nodeHost.Add(node);
        }
    }

    private IReadOnlyList<IReadOnlyList<ManiaModId>> orbitFamilies(
        ManiaModCategory page)
    {
        ManiaModId[][] preferredFamilies = page switch
        {
            ManiaModCategory.DifficultyIncrease =>
            [
                [ManiaModId.HardRock],
                [ManiaModId.SuddenDeath, ManiaModId.Perfect],
                [ManiaModId.DoubleTime, ManiaModId.Nightcore],
                [
                    ManiaModId.Hidden,
                    ManiaModId.Flashlight,
                    ManiaModId.FadeIn,
                    ManiaModId.Cover,
                ],
                [ManiaModId.AccuracyChallenge],
                [ManiaModId.NoPause],
            ],
            ManiaModCategory.Conversion =>
            [
                [ManiaModId.Random],
                [ManiaModId.DualStages],
                [ManiaModId.Mirror],
                [ManiaModId.DifficultyAdjust],
                [ManiaModId.Classic],
                [ManiaModId.Invert, ManiaModId.HoldOff],
                [ManiaModId.ConstantSpeed],
            ],
            _ => [],
        };

        ManiaModId[] available = definitionsForCategory(page)
            .Where(definition =>
                isSelectable(definition.Id))
            .Select(definition => definition.Id)
            .ToArray();
        if (preferredFamilies.Length == 0)
            return available.Select(mod =>
                (IReadOnlyList<ManiaModId>)[mod]).ToArray();

        var remaining = available.ToHashSet();
        var result = new List<IReadOnlyList<ManiaModId>>();
        foreach (ManiaModId[] preferredFamily in preferredFamilies)
        {
            ManiaModId[] family = preferredFamily
                .Where(remaining.Remove)
                .ToArray();
            if (family.Length > 0)
                result.Add(family);
        }

        result.AddRange(available
            .Where(remaining.Contains)
            .Select(mod => (IReadOnlyList<ManiaModId>)[mod]));
        return result;
    }

    private void updateHero(bool activated, bool deactivated)
    {
        ManiaModDefinition definition =
            OsuManiaModParityCatalog.Get(
                presentationModForNode(focusedMod));
        bool active = selectedMods.Contains(definition.Id);
        bool showNoPauseAllowance =
            definition.Id == ManiaModId.NoPause && active;
        Color4 accent = accentFor(definition);
        heroAcronym.Text = definition.Acronym;
        heroAcronym.Colour = accent;
        heroName.Text = YokkoStrings.ModName(definition);
        heroDescription.Clear();
        heroDescription.AddText(YokkoStrings.ModDescription(definition));
        heroState.Text = YokkoStrings.Get(
            active ? "mods.active" : "mods.activate_hint");
        heroState.X = active ? 9 : 0;
        heroState.Colour = active ? Color4.White : HomeControlColours.Cyan;
        heroStateBackground.Colour = active ? accent : Color4.Transparent;
        heroStateIcon.Alpha = active ? 1 : 0;
        heroStateBackground.Alpha = showNoPauseAllowance ? 0 : 1;
        heroState.Alpha = showNoPauseAllowance ? 0 : 1;
        heroStateIcon.Alpha = showNoPauseAllowance ? 0 : heroStateIcon.Alpha;
        noPauseAllowanceControl.Alpha = showNoPauseAllowance ? 1 : 0;
        noPauseAllowanceValue.Text = YokkoStrings.Get(
            "mods.no_pause.allowance",
            selectedMods.NoPauseAllowedPauses);
        noPauseMinus.SetEnabled(selectedMods.NoPauseAllowedPauses > 0);
        noPausePlus.SetEnabled(selectedMods.NoPauseAllowedPauses < 10);
        hero.FadeTo(1, 80);

        if (activated && !showNoPauseAllowance)
        {
            heroAcronym.ClearTransforms();
            heroAcronym.ScaleTo(0.9f)
                       .Then().ScaleTo(1.08f, 150, Easing.OutBack)
                       .Then().ScaleTo(1, 120, Easing.OutQuint);
            heroStateBackground.ClearTransforms();
            heroStateBackground.Alpha = 1;
            heroStateBackground.Scale = new Vector2(0.12f, 1);
            heroStateBackground.ScaleTo(Vector2.One, 210, Easing.OutQuint);
            heroState.ClearTransforms();
            heroState.ScaleTo(0.78f)
                     .Then().ScaleTo(1.08f, 180, Easing.OutBack)
                     .Then().ScaleTo(1, 100, Easing.OutQuint);
            heroStateIcon.ClearTransforms();
            heroStateIcon.Alpha = 0;
            heroStateIcon.MoveToX(-59);
            heroStateIcon.Delay(85)
                         .FadeIn(110, Easing.OutQuint)
                         .MoveToX(-45, 150, Easing.OutQuint);
        }
        else if (deactivated)
        {
            heroAcronym.ClearTransforms();
            heroAcronym.ScaleTo(1.05f, 70, Easing.OutQuint)
                       .Then().ScaleTo(0.98f, 100, Easing.InOutSine)
                       .Then().ScaleTo(1, 100, Easing.OutQuint);
            heroState.ClearTransforms();
            heroState.FadeTo(0.38f, 70)
                     .Then().FadeIn(130, Easing.OutQuint);
        }
    }

    private void updateActiveRows(IReadOnlySet<ManiaModId> activated)
    {
        activeRows.Clear();
        ManiaModId[] allActive = selectedMods.Mods
            .Where(mod => !isKeyConversionMod(mod))
            .OrderBy(mod => (int)mod)
            .ToArray();
        ManiaModId[] visibleActive = allActive.Take(5).ToArray();
        activeCount.Text = $"({allActive.Length} ACTIVE)";
        capacityTelemetry.Text = $"MOD BUS // {allActive.Length:00} ACTIVE";
        for (int i = 0; i < capacityDots.Count; i++)
        {
            capacityDots[i].FadeColour(
                i < Math.Min(allActive.Length, capacityDots.Count)
                    ? HomeControlColours.Cyan
                    : new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.16f),
                110);
        }

        for (int i = 0; i < 5; i++)
        {
            if (i < visibleActive.Length)
            {
                ManiaModDefinition definition =
                    OsuManiaModParityCatalog.Get(visibleActive[i]);
                var row = new OrbitActiveModRow(
                    definition,
                    accentFor(definition),
                    () =>
                    {
                        if (category != definition.Category)
                            selectCategory(definition.Category);
                        focusMod(definition.Id);
                    },
                    () => Scheduler.AddDelayed(
                        () => toggleMod(definition.Id),
                        135))
                {
                    Y = i * 53,
                };
                activeRows.Add(row);
                if (activated.Contains(definition.Id))
                    row.PlayActivationEntry();
            }
            else
            {
                activeRows.Add(new OrbitEmptySlot(() =>
                {
                    if (!selectedMods.Contains(focusedMod)
                        && isSelectable(focusedMod))
                    {
                        toggleMod(focusedMod);
                    }
                })
                {
                    Y = i * 53,
                });
            }
        }
    }

    private void updateRate(double value, bool enabled)
    {
        displayedRate = value;
        rateValue.Text = $"{value:0.00}x";
        rateSlider.SetState(true, 0.5, 2, value);
        rateMinus.SetEnabled(enabled || value > 0.5);
        ratePlus.SetEnabled(enabled || value < 2);
        updateRatePresetState(value);
    }

    private void updateRatePresetState(double value)
    {
        foreach (OrbitRatePresetButton preset in ratePresets)
            preset.SetSelected(Math.Abs(preset.Value - value) < 0.005);
    }

    private void selectRelativePage(int offset)
    {
        int currentIndex = Array.IndexOf(pages, category);
        int nextIndex = Math.Clamp(
            currentIndex + Math.Sign(offset),
            0,
            pages.Length - 1);
        if (nextIndex != currentIndex)
            selectCategory(pages[nextIndex]);
    }

    private static Color4 accentFor(ManiaModDefinition definition) =>
        definition.Id switch
        {
            ManiaModId.HardRock or ManiaModId.Perfect
                or ManiaModId.Nightcore or ManiaModId.DoubleTime
                or ManiaModId.Hidden or ManiaModId.Cover
                or ManiaModId.AccuracyChallenge => HomeControlColours.Pink,
            ManiaModId.SuddenDeath or ManiaModId.FadeIn
                or ManiaModId.Flashlight => HomeControlColours.Yellow,
            _ => HomeControlColours.Cyan,
        };

    private static bool isKeyConversionMod(ManiaModId mod) =>
        mod is >= ManiaModId.Key1 and <= ManiaModId.Key10;
}

internal partial class OrbitHeroPanel : ClickableContainer
{
    private readonly Action activate;

    internal OrbitHeroPanel(Action action)
    {
        activate = action;
        Action = activate;
    }

    internal void ActivateForTest() => activate();

    protected override bool OnHover(HoverEvent e)
    {
        this.ScaleTo(1.025f, 110, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        this.ScaleTo(1, 140, Easing.OutQuint);

    protected override bool OnClick(ClickEvent e)
    {
        this.ScaleTo(0.98f, 45, Easing.OutQuint)
            .Then().ScaleTo(1.025f, 150, Easing.OutBack);
        return base.OnClick(e);
    }
}

internal partial class OrbitCategoryButton : ClickableContainer
{
    private readonly Color4 accent;
    private readonly Box background;
    private readonly Circle marker;
    private readonly SpriteText number;
    private readonly SpriteIcon icon;
    private readonly SpriteText label;
    private readonly Box selectionDiamond;
    private bool selected;

    internal OrbitCategoryButton(
        int page,
        LocalisableString text,
        IconUsage iconUsage,
        Color4 accent,
        Action action)
    {
        this.accent = accent;
        Action = action;
        Size = new Vector2(292, 80);
        InternalChildren =
        [
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Transparent,
            },
            number = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 15,
                Text = $"{page:00}",
                Font = HomeTypography.Display(20),
                Colour = HomeControlColours.Navy,
            },
            marker = new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 50,
                Size = new Vector2(8),
                Colour = HomeControlColours.Navy,
            },
            new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 98,
                Size = new Vector2(44),
                Colour = accent,
            },
            icon = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(98, 0),
                Size = new Vector2(19),
                Icon = iconUsage,
                Colour = Color4.White,
            },
            label = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 140,
                Text = text,
                Font = HomeTypography.Display(20),
                Colour = HomeControlColours.Navy,
            },
            selectionDiamond = new Box
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-4, 0),
                Size = new Vector2(10),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
                Alpha = 0,
            },
        ];
    }

    internal void SetSelected(bool selected)
    {
        if (this.selected == selected)
            return;

        this.selected = selected;
        background.ClearTransforms();
        marker.ClearTransforms();
        number.ClearTransforms();
        icon.ClearTransforms();
        label.ClearTransforms();
        background.FadeColour(
            selected
                ? new Color4(
                    HomeControlColours.PaleCyan.R,
                    HomeControlColours.PaleCyan.G,
                    HomeControlColours.PaleCyan.B,
                    0.56f)
                : Color4.Transparent,
            110);
        marker.FadeColour(selected ? accent : HomeControlColours.Navy, 110);
        number.FadeColour(selected ? accent : HomeControlColours.Navy, 110);
        icon.ScaleTo(selected ? 1.08f : 1, 110, Easing.OutQuint);
        label.FadeColour(
            selected ? accent : HomeControlColours.Navy,
            120,
            Easing.OutQuint);
        label.MoveToX(
            selected ? 143 : 140,
            155,
            Easing.OutQuint);
        selectionDiamond.ClearTransforms();
        if (selected)
        {
            selectionDiamond.FadeIn(110);
            if (IsLoaded)
                startSelectionPulse();
        }
        else
        {
            selectionDiamond.ScaleTo(1);
            selectionDiamond.FadeOut(110);
        }
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        if (selected)
            startSelectionPulse();
    }

    private void startSelectionPulse()
    {
        selectionDiamond.ClearTransforms();
        selectionDiamond.Alpha = 1;
        selectionDiamond.ScaleTo(0.86f)
                        .Then().ScaleTo(1.08f, 750, Easing.InOutSine)
                        .Then().ScaleTo(0.86f, 750, Easing.InOutSine)
                        .Loop();
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(
            new Color4(
                HomeControlColours.PaleCyan.R,
                HomeControlColours.PaleCyan.G,
                HomeControlColours.PaleCyan.B,
                0.44f),
            80);
        label.MoveToX(141, 100, Easing.OutQuint);
        label.FadeColour(accent, 80);
        icon.ScaleTo(1.13f, 90, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        label.MoveToX(selected ? 143 : 140, 120, Easing.OutQuint);
        label.FadeColour(
            selected ? accent : HomeControlColours.Navy,
            110);
        icon.ScaleTo(selected ? 1.08f : 1, 110, Easing.OutQuint);
        background.FadeColour(
            selected
                ? new Color4(
                    HomeControlColours.PaleCyan.R,
                    HomeControlColours.PaleCyan.G,
                    HomeControlColours.PaleCyan.B,
                    0.56f)
                : Color4.Transparent,
            110);
    }
}

internal partial class OrbitConnector : CompositeDrawable
{
    private static readonly Vector2 orbitCentre = new(249, 317);

    private readonly Vector2 start;
    private readonly Vector2 control;
    private readonly Vector2 end;
    private readonly double phase;
    private readonly SmoothPath glow;
    private readonly SmoothPath main;
    private readonly SmoothPath outerRail;
    private readonly Circle signal;
    private readonly Circle transitionPulse;
    private readonly Circle transitionEcho;
    private readonly Circle startJoint;
    private readonly Circle endJoint;
    private readonly List<Box> dataTicks = new();
    private bool active;
    private bool focused;
    private double? transitionStarted;
    private bool transitionFromStart;
    private bool transitionActivating;

    internal ManiaModId StartMod { get; }
    internal ManiaModId EndMod { get; }
    internal bool TransitionRunning => transitionStarted.HasValue;
    internal ManiaModId? TransitionOriginMod => transitionStarted.HasValue
        ? transitionFromStart ? StartMod : EndMod
        : null;
    internal bool TransitionIsActivation =>
        transitionStarted.HasValue && transitionActivating;

    internal OrbitConnector(
        ManiaModId startMod,
        ManiaModId endMod,
        Vector2 startCentre,
        Vector2 endCentre,
        double phase)
    {
        StartMod = startMod;
        EndMod = endMod;
        this.phase = phase;
        RelativeSizeAxes = Axes.Both;
        Depth = 2;

        Vector2 midpoint = (startCentre + endCentre) / 2;
        Vector2 outward = normalisedOrFallback(
            midpoint - orbitCentre,
            new Vector2(1, 0));
        Vector2 rawControl = midpoint + outward * 24;
        start = quadratic(startCentre, rawControl, endCentre, 0.17f);
        end = quadratic(startCentre, rawControl, endCentre, 0.83f);
        control = rawControl;

        Vector2 railOffset = outward * 5;
        glow = createPath(start, control, end, Vector2.Zero, 2.8f);
        main = createPath(start, control, end, Vector2.Zero, 0.85f);
        outerRail = createPath(
            start,
            control,
            end,
            railOffset,
            0.45f);

        startJoint = createJoint(start);
        endJoint = createJoint(end);
        signal = new Circle
        {
            Origin = Anchor.Centre,
            Position = start,
            Size = new Vector2(7),
            Colour = Color4.White,
            Masking = true,
            BorderThickness = 2,
            BorderColour = HomeControlColours.Cyan,
        };
        transitionPulse = createTransitionPulse(12);
        transitionEcho = createTransitionPulse(7);

        InternalChildren =
        [
            glow,
            outerRail,
            main,
            startJoint,
            endJoint,
            signal,
            transitionEcho,
            transitionPulse,
        ];

        for (int i = 1; i <= 5; i++)
        {
            float t = i / 6f;
            Vector2 point = quadratic(start, control, end, t)
                            + railOffset;
            Vector2 tangent = quadraticTangent(start, control, end, t);
            var tick = new Box
            {
                Origin = Anchor.Centre,
                Position = point,
                Size = new Vector2(i == 3 ? 8 : 4, 1.2f),
                Rotation = MathF.Atan2(tangent.Y, tangent.X)
                           * 180 / MathF.PI,
            };
            dataTicks.Add(tick);
            AddInternal(tick);
        }

        SetState(false, false);
    }

    internal void SetState(
        bool active,
        bool focused,
        bool animateTransition = false,
        bool transitionFromStart = true,
        bool transitionActivating = true)
    {
        this.active = active;
        this.focused = focused;

        Color4 colour = focused
            ? HomeControlColours.Pink
            : HomeControlColours.Cyan;
        float mainAlpha = focused ? 0.96f : active ? 0.8f : 0.52f;

        main.Colour = new Color4(
            colour.R,
            colour.G,
            colour.B,
            mainAlpha);
        glow.Colour = new Color4(
            colour.R,
            colour.G,
            colour.B,
            focused ? 0.18f : active ? 0.12f : 0.055f);
        outerRail.Colour = new Color4(
            HomeControlColours.Navy.R,
            HomeControlColours.Navy.G,
            HomeControlColours.Navy.B,
            focused ? 0.34f : 0.2f);
        startJoint.BorderColour = colour;
        endJoint.BorderColour = colour;
        startJoint.Alpha = focused ? 1 : active ? 0.86f : 0.62f;
        endJoint.Alpha = startJoint.Alpha;
        signal.BorderColour = colour;
        signal.Alpha = focused ? 1 : active ? 0.82f : 0.32f;
        signal.Size = new Vector2(focused ? 9 : 7);

        foreach (Box tick in dataTicks)
        {
            tick.Colour = new Color4(
                colour.R,
                colour.G,
                colour.B,
                focused ? 0.72f : active ? 0.5f : 0.26f);
        }

        if (animateTransition)
        {
            this.transitionFromStart = transitionFromStart;
            this.transitionActivating = transitionActivating;
            transitionStarted = Time.Current;
            Color4 transitionColour = transitionActivating
                ? colour
                : HomeControlColours.Yellow;
            transitionPulse.BorderColour = transitionColour;
            transitionEcho.BorderColour = transitionColour;
            transitionPulse.Colour = Color4.White;
            transitionEcho.Colour = Color4.White;
            transitionPulse.Position =
                transitionFromStart ? start : end;
            transitionEcho.Position =
                transitionFromStart ? start : end;
            transitionPulse.Alpha = 0;
            transitionEcho.Alpha = 0;

            glow.ClearTransforms();
            glow.Alpha = transitionActivating ? 0.18f : 1;
            glow.FadeTo(1, transitionActivating ? 210 : 130, Easing.OutQuint);
            main.ClearTransforms();
            main.Alpha = transitionActivating ? 0.48f : 1;
            main.FadeTo(1, transitionActivating ? 175 : 125, Easing.OutQuint);
            outerRail.ClearTransforms();
            outerRail.MoveToY(transitionActivating ? -2 : 2);
            outerRail.MoveToY(0, 190, Easing.OutBack);
            signal.ClearTransforms();
            signal.ScaleTo(transitionActivating ? 0.45f : 1.35f)
                  .Then().ScaleTo(
                      transitionActivating ? 1.45f : 0.8f,
                      135,
                      Easing.OutBack)
                  .Then().ScaleTo(1, 120, Easing.OutQuint);
            Circle originJoint =
                transitionFromStart ? startJoint : endJoint;
            Circle destinationJoint =
                transitionFromStart ? endJoint : startJoint;
            originJoint.ClearTransforms();
            destinationJoint.ClearTransforms();
            originJoint.ScaleTo(0.62f)
                       .Then().ScaleTo(1.38f, 135, Easing.OutBack)
                       .Then().ScaleTo(1, 105, Easing.OutQuint);
            destinationJoint.Delay(125)
                            .ScaleTo(0.7f)
                            .Then().ScaleTo(
                                1.32f,
                                145,
                                Easing.OutBack)
                            .Then().ScaleTo(
                                1,
                                105,
                                Easing.OutQuint);

            for (int i = 0; i < dataTicks.Count; i++)
            {
                int order = transitionFromStart
                    ? i
                    : dataTicks.Count - 1 - i;
                Box tick = dataTicks[i];
                tick.ClearTransforms();
                tick.Alpha = 0.3f;
                tick.Delay(order * 34)
                    .FadeIn(60, Easing.OutQuint)
                    .ScaleTo(new Vector2(1.55f, 1), 85, Easing.OutBack)
                    .Then()
                    .ScaleTo(Vector2.One, 110, Easing.OutQuint);
            }
        }
    }

    protected override void Update()
    {
        base.Update();

        double speed = focused ? 1180 : active ? 1650 : 2500;
        float t = (float)((Time.Current / speed + phase) % 1);
        float eased = t * t * (3 - 2 * t);
        signal.Position = quadratic(start, control, end, eased);

        float pulse = 0.72f
                      + 0.28f * MathF.Sin((float)(Time.Current / 170));
        signal.Alpha = (focused ? 0.8f : active ? 0.62f : 0.2f)
                       + pulse * (focused ? 0.2f : active ? 0.18f : 0.1f);

        if (transitionStarted is not double started)
            return;

        const double duration = 520;
        double elapsed = Time.Current - started;
        if (elapsed >= duration)
        {
            transitionStarted = null;
            transitionPulse.Alpha = 0;
            transitionEcho.Alpha = 0;
            return;
        }

        float progress = (float)Math.Clamp(elapsed / duration, 0, 1);
        float directedProgress = transitionFromStart
            ? progress
            : 1 - progress;
        transitionPulse.Position =
            quadratic(start, control, end, directedProgress);
        transitionPulse.Alpha =
            MathF.Sin(progress * MathF.PI)
            * (transitionActivating ? 1 : 0.82f);
        transitionPulse.Scale = new Vector2(
            0.72f + MathF.Sin(progress * MathF.PI) * 0.65f);

        float echoProgress = (float)Math.Clamp(
            (elapsed - 78) / (duration - 78),
            0,
            1);
        float directedEchoProgress = transitionFromStart
            ? echoProgress
            : 1 - echoProgress;
        transitionEcho.Position =
            quadratic(start, control, end, directedEchoProgress);
        transitionEcho.Alpha = elapsed < 78
            ? 0
            : MathF.Sin(echoProgress * MathF.PI) * 0.56f;
        transitionEcho.Scale = new Vector2(
            0.68f + MathF.Sin(echoProgress * MathF.PI) * 0.38f);
    }

    private static SmoothPath createPath(
        Vector2 start,
        Vector2 control,
        Vector2 end,
        Vector2 offset,
        float radius)
    {
        var path = new SmoothPath
        {
            PathRadius = radius,
        };
        const int segmentCount = 18;
        for (int i = 0; i <= segmentCount; i++)
        {
            path.AddVertex(
                quadratic(start, control, end, i / (float)segmentCount)
                + offset);
        }
        return path;
    }

    private static Circle createJoint(Vector2 position) => new()
    {
        Origin = Anchor.Centre,
        Position = position,
        Size = new Vector2(9),
        Masking = true,
        BorderThickness = 1.5f,
        BorderColour = HomeControlColours.Cyan,
        Child = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = Color4.White,
        },
    };

    private static Circle createTransitionPulse(float size) => new()
    {
        Origin = Anchor.Centre,
        Size = new Vector2(size),
        Colour = Color4.White,
        Alpha = 0,
        Masking = true,
        BorderThickness = 2,
        BorderColour = HomeControlColours.Cyan,
    };

    private static Vector2 quadratic(
        Vector2 a,
        Vector2 b,
        Vector2 c,
        float t)
    {
        float inverse = 1 - t;
        return inverse * inverse * a
               + 2 * inverse * t * b
               + t * t * c;
    }

    private static Vector2 quadraticTangent(
        Vector2 a,
        Vector2 b,
        Vector2 c,
        float t) =>
        2 * (1 - t) * (b - a) + 2 * t * (c - b);

    private static Vector2 normalisedOrFallback(
        Vector2 value,
        Vector2 fallback) =>
        value.LengthSquared > 0.001f ? value.Normalized() : fallback;
}

internal partial class OrbitTechnicalBadge : CompositeDrawable
{
    private readonly Circle pulse;

    internal OrbitTechnicalBadge(string text)
    {
        Size = new Vector2(178, 26);
        InternalChildren =
        [
            pulse = new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(4, 0),
                Size = new Vector2(7),
                Masking = true,
                BorderThickness = 1.2f,
                BorderColour = HomeControlColours.Pink,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Transparent,
                },
            },
            new Box
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Position = new Vector2(13, 0),
                Size = new Vector2(48, 1),
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    0.42f),
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Position = new Vector2(68, 0),
                Text = text,
                Font = HomeTypography.Display(8),
                Spacing = new Vector2(0.8f, 0),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.44f),
            },
        ];
    }

    protected override void Update()
    {
        base.Update();
        float wave = 0.5f
                     + 0.5f * MathF.Sin((float)(Time.Current / 320));
        pulse.Scale = new Vector2(0.8f + wave * 0.32f);
        pulse.Alpha = 0.45f + wave * 0.45f;
    }
}

internal partial class OrbitEdgeTicks : CompositeDrawable
{
    internal OrbitEdgeTicks()
    {
        Size = new Vector2(28, 270);
        for (int i = 0; i < 18; i++)
        {
            bool major = i % 4 == 0;
            AddInternal(new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(0, i * 15),
                Size = new Vector2(major ? 22 : 10, major ? 1.4f : 0.8f),
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    major ? 0.56f : 0.24f),
            });
        }
    }
}

internal partial class OrbitFooterSignal : CompositeDrawable
{
    private readonly Box[] bars = new Box[6];

    internal OrbitFooterSignal()
    {
        Size = new Vector2(76, 20);
        for (int i = 0; i < bars.Length; i++)
        {
            bars[i] = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Position = new Vector2(i * 11, 0),
                Size = new Vector2(4, 6),
                Colour = new Color4(1, 1, 1, 0.56f),
            };
        }
        InternalChildren = bars;
    }

    protected override void Update()
    {
        base.Update();
        for (int i = 0; i < bars.Length; i++)
        {
            float wave = 0.5f + 0.5f * MathF.Sin(
                (float)(Time.Current / 175 + i * 0.9));
            bars[i].Height = 4 + wave * 15;
            bars[i].Alpha = 0.34f + wave * 0.54f;
        }
    }
}

internal partial class OrbitMicroBarGraph : CompositeDrawable
{
    private readonly Box[] bars = new Box[7];

    internal OrbitMicroBarGraph()
    {
        Size = new Vector2(24, 76);
        for (int i = 0; i < bars.Length; i++)
        {
            bars[i] = new Box
            {
                Position = new Vector2(0, i * 10),
                Size = new Vector2(6 + i % 3 * 4, 2),
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    0.34f),
            };
        }
        InternalChildren = bars;
    }

    protected override void Update()
    {
        base.Update();
        for (int i = 0; i < bars.Length; i++)
        {
            float wave = 0.5f + 0.5f * MathF.Sin(
                (float)(Time.Current / 240 + i * 0.85));
            bars[i].Width = 5 + wave * 16;
            bars[i].Alpha = 0.28f + wave * 0.58f;
        }
    }
}

internal partial class OrbitSignalScanner : CompositeDrawable
{
    private readonly Box diamond;
    private readonly Box trail;
    private readonly Circle pulse;
    private Color4 accent = HomeControlColours.Cyan;

    internal OrbitSignalScanner()
    {
        Origin = Anchor.Centre;
        Size = new Vector2(390);
        Alpha = 0.8f;
        InternalChildren =
        [
            trail = new Box
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, -5),
                Size = new Vector2(1.2f, 22),
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    0.32f),
            },
            pulse = new Circle
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.Centre,
                Position = new Vector2(0, -5),
                Size = new Vector2(13),
                Masking = true,
                BorderThickness = 1.5f,
                BorderColour = HomeControlColours.Cyan,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
            },
            diamond = new Box
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.Centre,
                Position = new Vector2(0, -5),
                Size = new Vector2(5),
                Rotation = 45,
                Colour = HomeControlColours.Cyan,
            },
        ];
    }

    internal void SetAccent(Color4 value)
    {
        accent = value;
        trail.Colour = new Color4(value.R, value.G, value.B, 0.32f);
        pulse.BorderColour = value;
        diamond.Colour = value;
    }

    protected override void Update()
    {
        base.Update();
        Rotation = (float)(Time.Current / 28 % 360);
        float breathing =
            0.82f + 0.18f * MathF.Sin((float)(Time.Current / 210));
        pulse.Scale = new Vector2(breathing);
        pulse.Alpha = 0.48f + breathing * 0.38f;
        diamond.Colour = accent;
    }
}

internal partial class OrbitModNode : ClickableContainer
{
    private readonly bool compact;
    private readonly Color4 accent;
    private readonly Circle shadow;
    private readonly Circle activationBurst;
    private readonly Circle activationCore;
    private readonly Circle halo;
    private readonly Circle surface;
    private readonly Circle innerRing;
    private readonly SpriteText acronym;
    private readonly SpriteText name;
    private readonly TextFlowContainer description;
    private readonly Circle stateBadge;
    private readonly SpriteIcon stateGlyph;
    private readonly Action focus;
    private bool activeState;
    private bool focusedState;
    private bool activationTransitionRunning;
    private bool activationTransitionPending;
    private int activationTransitionVersion;
    private ManiaModId presentationMod;

    internal ManiaModId ModId { get; }
    internal ManiaModId PresentationMod => presentationMod;
    internal bool ActivationTransitionRunning =>
        activationTransitionRunning || activationTransitionPending;

    internal OrbitModNode(
        ManiaModDefinition definition,
        Color4 accent,
        int index,
        Action action,
        Action focus,
        bool compact = false)
    {
        ModId = definition.Id;
        presentationMod = definition.Id;
        this.accent = accent;
        this.focus = focus;
        this.compact = compact;
        Action = action;
        Size = compact ? new Vector2(88) : new Vector2(284, 86);
        InternalChildren =
        [
            shadow = new Circle
            {
                Position = new Vector2(2, 6),
                Size = new Vector2(88),
                Colour = HomeControlColours.Navy,
                Alpha = 0.055f,
            },
            activationBurst = new Circle
            {
                Position = new Vector2(-12),
                Size = new Vector2(108),
                Masking = true,
                BorderThickness = 2,
                BorderColour = accent,
                Alpha = 0,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Transparent,
                },
            },
            activationCore = new Circle
            {
                Position = new Vector2(5),
                Size = new Vector2(74),
                Colour = accent,
                Alpha = 0,
            },
            halo = new Circle
            {
                Position = new Vector2(-7),
                Size = new Vector2(98),
                Masking = true,
                BorderThickness = 1.2f,
                BorderColour = accent,
                Alpha = 0,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Transparent,
                },
            },
            new SpriteText
            {
                Position = new Vector2(4, -13),
                Text = $"NODE {index:00}",
                Font = HomeTypography.Display(9),
                Spacing = new Vector2(0.7f, 0),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.38f),
            },
            surface = new Circle
            {
                Size = new Vector2(84),
                Masking = true,
                BorderThickness = 1.5f,
                BorderColour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.24f),
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
            },
            innerRing = new Circle
            {
                Position = new Vector2(5),
                Size = new Vector2(74),
                Masking = true,
                BorderThickness = 1,
                BorderColour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.19f),
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Transparent,
                },
            },
            acronym = new SpriteText
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(42),
                Text = definition.Acronym,
                Font = HomeTypography.Display(35),
                Colour = accent,
            },
            stateBadge = new Circle
            {
                Position = new Vector2(64, 62),
                Size = new Vector2(22),
                Masking = true,
                BorderThickness = 1.3f,
                BorderColour = accent,
                Alpha = 0,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
            },
            stateGlyph = new SpriteIcon
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(75, 73),
                Size = new Vector2(8),
                Icon = FontAwesome.Solid.Plus,
                Colour = accent,
                Alpha = 0,
            },
            name = new SpriteText
            {
                Position = new Vector2(106, 12),
                Text = YokkoStrings.ModName(definition),
                Font = HomeTypography.Display(19),
                Colour = HomeControlColours.Navy,
            },
            description = new TextFlowContainer(text =>
            {
                text.Font = HomeTypography.Body(16);
                text.Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.68f);
            })
            {
                Position = new Vector2(106, 39),
                Width = 178,
                AutoSizeAxes = Axes.Y,
            },
        ];
        description.AddText(YokkoStrings.ModDescription(definition));
        if (compact)
        {
            name.Alpha = 0;
            description.Alpha = 0;
        }
    }

    internal void SetPresentation(ManiaModDefinition definition)
    {
        if (presentationMod == definition.Id)
            return;

        presentationMod = definition.Id;
        acronym.Text = definition.Acronym;
        name.Text = YokkoStrings.ModName(definition);
        description.Clear();
        description.AddText(YokkoStrings.ModDescription(definition));
        acronym.ClearTransforms();
        acronym.ScaleTo(0.82f)
               .Then().ScaleTo(1.08f, 150, Easing.OutBack)
               .Then().ScaleTo(1, 90, Easing.OutQuint);
        if (!compact)
            name.FlashColour(accent, 220);
    }

    internal void SetState(
        bool active,
        bool focused,
        bool enabled,
        bool animateActivation = false,
        bool animateDeactivation = false)
    {
        bool activeChanged = activeState != active;
        activeState = active;
        focusedState = focused;
        Alpha = enabled ? 1 : 0.35f;
        surface.BorderColour = active || focused
            ? accent
            : new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.24f);
        surface.BorderThickness = active ? 2.3f : focused ? 1.8f : 1.5f;
        innerRing.BorderColour = active
            ? new Color4(accent.R, accent.G, accent.B, 0.7f)
            : new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.19f);
        acronym.Colour = active || focused ? accent : HomeControlColours.Navy;
        this.ScaleTo(focused ? 1.05f : 1, 125, Easing.OutQuint);
        shadow.FadeTo(active ? 0.16f : focused ? 0.11f : 0.055f, 120);
        shadow.MoveTo(new Vector2(
            focused ? 4 : 2,
            focused ? 8 : 6), 120, Easing.OutQuint);
        stateGlyph.Icon = active
            ? FontAwesome.Solid.Check
            : FontAwesome.Solid.Plus;
        stateBadge.FadeTo(active || focused ? 1 : 0, 90);
        stateGlyph.FadeTo(active || focused ? 1 : 0, 90);
        stateBadge.ScaleTo(active ? 1.06f : 1, 110, Easing.OutQuint);

        if (active && animateActivation)
        {
            halo.ClearTransforms();
            if (IsLoaded)
                playActivationTransition();
            else
                activationTransitionPending = true;
        }
        else if (!active && animateDeactivation)
        {
            activationTransitionVersion++;
            activationTransitionRunning = false;
            activationTransitionPending = false;
            halo.ClearTransforms();
            halo.ScaleTo(1);
            halo.FadeOut(190);
            if (IsLoaded)
                playDeactivationTransition();
        }
        else if (activeChanged)
        {
            halo.ClearTransforms();
            if (active)
            {
                halo.Alpha = 0.42f;
                startActivePulse();
            }
            else
            {
                halo.ScaleTo(1);
                halo.FadeOut(100);
            }
        }
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        if (activationTransitionPending)
        {
            activationTransitionPending = false;
            playActivationTransition();
        }
        else if (activeState)
            startActivePulse();
    }

    private void startActivePulse()
    {
        halo.ClearTransforms();
        halo.ScaleTo(0.86f);
        halo.FadeTo(0.42f);
        halo.ScaleTo(1.16f, 1050, Easing.OutQuint)
            .Loop(360);
        halo.FadeOut(1050, Easing.OutQuint)
            .Loop(360);
    }

    private void playActivationTransition()
    {
        int transitionVersion = ++activationTransitionVersion;
        activationTransitionRunning = true;
        activationBurst.ClearTransforms();
        activationBurst.Scale = new Vector2(0.64f);
        activationBurst.Alpha = 0.88f;
        activationBurst.ScaleTo(1.42f, 340, Easing.OutQuint)
                       .FadeOut(320, Easing.OutQuint);

        activationCore.ClearTransforms();
        activationCore.Scale = new Vector2(0.78f);
        activationCore.Alpha = 0.16f;
        activationCore.ScaleTo(1.18f, 240, Easing.OutQuint)
                      .FadeOut(230, Easing.OutQuint);

        surface.ClearTransforms();
        surface.ScaleTo(0.88f, 45, Easing.OutQuint)
               .Then().ScaleTo(1.07f, 135, Easing.OutBack)
               .Then().ScaleTo(1, 110, Easing.OutQuint);
        acronym.ClearTransforms();
        acronym.ScaleTo(0.74f)
               .Then().ScaleTo(1.14f, 160, Easing.OutBack)
               .Then().ScaleTo(1, 110, Easing.OutQuint);
        stateBadge.ClearTransforms();
        stateBadge.ScaleTo(0.35f)
                  .Then().ScaleTo(1.2f, 190, Easing.OutBack)
                  .Then().ScaleTo(1.06f, 90, Easing.OutQuint);
        stateGlyph.ClearTransforms();
        stateGlyph.RotateTo(-90);
        stateGlyph.RotateTo(0, 220, Easing.OutBack);

        Scheduler.AddDelayed(() =>
        {
            if (transitionVersion != activationTransitionVersion)
                return;

            activationTransitionRunning = false;
            if (activeState)
                startActivePulse();
        }, 340);
    }

    private void playDeactivationTransition()
    {
        activationBurst.ClearTransforms();
        activationBurst.Scale = new Vector2(1.08f);
        activationBurst.Alpha = 0.34f;
        activationBurst.ScaleTo(0.72f, 190, Easing.InQuint)
                       .FadeOut(180, Easing.OutQuint);
        surface.ClearTransforms();
        surface.ScaleTo(1.05f, 60, Easing.OutQuint)
               .Then().ScaleTo(0.96f, 95, Easing.InOutSine)
               .Then().ScaleTo(1, 100, Easing.OutQuint);
        acronym.ClearTransforms();
        acronym.FadeTo(0.35f, 70)
               .Then().FadeIn(130, Easing.OutQuint);
    }

    protected override bool OnHover(HoverEvent e)
    {
        focus();
        this.ScaleTo(1.05f, 100, Easing.OutQuint);
        if (!compact)
            description.FadeTo(1, 80);
        shadow.FadeTo(0.17f, 90);
        shadow.MoveTo(new Vector2(5, 9), 100, Easing.OutQuint);
        surface.BorderColour = accent;
        surface.BorderThickness = 2.3f;
        if (!activeState)
        {
            halo.ClearTransforms();
            halo.ScaleTo(1.04f, 100, Easing.OutQuint);
            halo.FadeTo(0.28f, 80);
        }
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.ScaleTo(focusedState ? 1.05f : 1, 130, Easing.OutQuint);
        surface.BorderColour = activeState || focusedState
            ? accent
            : new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.24f);
        surface.BorderThickness = activeState ? 2.3f : focusedState ? 1.8f : 1.5f;
        shadow.FadeTo(activeState ? 0.16f : focusedState ? 0.11f : 0.055f, 110);
        shadow.MoveTo(
            focusedState ? new Vector2(4, 8) : new Vector2(2, 6),
            120,
            Easing.OutQuint);
        if (!activeState)
            halo.FadeOut(110);
    }

    protected override bool OnClick(ClickEvent e)
    {
        stateBadge.ScaleTo(0.76f, 45, Easing.OutQuint)
                  .Then().ScaleTo(1.08f, 150, Easing.OutBack);
        this.ScaleTo(0.97f, 45, Easing.OutQuint)
            .Then().ScaleTo(1.05f, 150, Easing.OutBack);
        return base.OnClick(e);
    }

    private static string shorten(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum] + "…";
}

internal partial class OrbitActiveModRow : ClickableContainer
{
    private readonly Container background;
    private readonly Box scanLine;
    private readonly Circle statusDot;
    private readonly SpriteText acronym;
    private readonly SpriteText name;
    private readonly SpriteIcon focusChevron;
    private readonly Action remove;
    private bool removalPending;

    internal ManiaModId ModId { get; }

    internal OrbitActiveModRow(
        ManiaModDefinition definition,
        Color4 accent,
        Action focus,
        Action remove)
    {
        ModId = definition.Id;
        Action = focus;
        this.remove = remove;
        Size = new Vector2(365, 48);
        background = createHexagonLayer(
            Color4.White,
            new Vector2(362, 45));
        background.Position = new Vector2(1.5f);
        InternalChildren =
        [
            createHexagonLayer(accent, new Vector2(365, 48)),
            background,
            scanLine = new Box
            {
                Position = new Vector2(18, 6),
                Size = new Vector2(2, 36),
                Colour = new Color4(accent.R, accent.G, accent.B, 0.48f),
                Alpha = 0,
            },
            statusDot = new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Position = new Vector2(17, 0),
                Size = new Vector2(6),
                Colour = accent,
            },
            acronym = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 34,
                Text = definition.Acronym,
                Font = HomeTypography.Display(22),
                Colour = accent,
            },
            name = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 102,
                Text = YokkoStrings.ModName(definition),
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Navy,
            },
            focusChevron = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -65,
                Size = new Vector2(9),
                Icon = FontAwesome.Solid.ChevronRight,
                Colour = accent,
                Alpha = 0.28f,
            },
            new OrbitActiveModRemoveButton(beginRemove),
        ];
    }

    internal void ActivateForTest() => Action?.Invoke();

    internal void RemoveForTest() => beginRemove();

    internal void PlayActivationEntry()
    {
        ClearTransforms();
        Alpha = 0;
        X = 18;
        Scale = new Vector2(0.97f);
        this.FadeIn(150, Easing.OutQuint)
            .MoveToX(0, 230, Easing.OutQuint)
            .ScaleTo(1, 210, Easing.OutBack);
        background.FlashColour(HomeControlColours.PaleCyan, 280);
        statusDot.ClearTransforms();
        statusDot.ScaleTo(0.25f)
                 .Then().ScaleTo(1.75f, 180, Easing.OutBack)
                 .Then().ScaleTo(1, 120, Easing.OutQuint);
        scanLine.ClearTransforms();
        scanLine.X = 18;
        scanLine.FadeTo(0.5f, 45)
                .MoveToX(337, 380, Easing.OutQuint)
                .Then().FadeOut(80);
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(HomeControlColours.PaleCyan, 80);
        acronym.ScaleTo(1.08f, 110, Easing.OutQuint);
        name.MoveToX(106, 110, Easing.OutQuint);
        focusChevron.FadeIn(90);
        focusChevron.MoveToX(-58, 120, Easing.OutQuint);
        statusDot.ScaleTo(1.55f, 110, Easing.OutQuint);
        scanLine.ClearTransforms();
        scanLine.X = 18;
        scanLine.FadeTo(0.48f, 45)
                .MoveToX(337, 360, Easing.OutQuint)
                .Then().FadeOut(90);
        this.MoveToX(4, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(Color4.White, 110);
        acronym.ScaleTo(1, 110, Easing.OutQuint);
        name.MoveToX(102, 120, Easing.OutQuint);
        focusChevron.FadeTo(0.28f, 100);
        focusChevron.MoveToX(-65, 120, Easing.OutQuint);
        statusDot.ScaleTo(1, 120, Easing.OutQuint);
        scanLine.FadeOut(70);
        this.MoveToX(0, 120, Easing.OutQuint);
    }

    protected override bool OnClick(ClickEvent e)
    {
        focusChevron.ScaleTo(0.7f, 45, Easing.OutQuint)
                      .Then().ScaleTo(1, 130, Easing.OutBack);
        this.ScaleTo(0.985f, 45, Easing.OutQuint)
            .Then().ScaleTo(1, 130, Easing.OutBack);
        return base.OnClick(e);
    }

    private void beginRemove()
    {
        if (removalPending)
            return;

        removalPending = true;
        ClearTransforms();
        this.MoveToX(20, 150, Easing.InQuint)
            .FadeOut(130, Easing.OutQuint)
            .ScaleTo(0.96f, 140, Easing.InOutSine);
        remove();
    }

    private static Container createHexagonLayer(
        Color4 colour,
        Vector2 size) => new()
    {
        Size = size,
        Children =
        [
            new Box
            {
                Position = new Vector2(10, 0),
                Size = new Vector2(size.X - 20, size.Y),
                Colour = colour,
            },
            new osu.Framework.Graphics.Shapes.Triangle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(10, 0),
                Size = new Vector2(size.Y, 20),
                Rotation = -90,
                Colour = colour,
            },
            new osu.Framework.Graphics.Shapes.Triangle
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-10, 0),
                Size = new Vector2(size.Y, 20),
                Rotation = 90,
                Colour = colour,
            },
        ],
    };
}

internal partial class OrbitActiveModRemoveButton : ClickableContainer
{
    private readonly Circle background;
    private readonly SpriteIcon icon;

    internal OrbitActiveModRemoveButton(Action action)
    {
        Action = action;
        Anchor = Anchor.CentreRight;
        Origin = Anchor.CentreRight;
        Size = new Vector2(48);
        InternalChildren =
        [
            background = new Circle
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(30),
                Colour = HomeControlColours.PaleCyan,
                Alpha = 0,
            },
            icon = new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(12),
                Icon = FontAwesome.Solid.Times,
                Colour = HomeControlColours.Pink,
            },
        ];
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeIn(80);
        icon.RotateTo(90, 130, Easing.OutBack);
        icon.ScaleTo(1.2f, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeOut(100);
        icon.RotateTo(0, 120, Easing.OutQuint);
        icon.ScaleTo(1, 100, Easing.OutQuint);
    }

    protected override bool OnClick(ClickEvent e)
    {
        this.ScaleTo(0.82f, 45, Easing.OutQuint)
            .Then().ScaleTo(1, 130, Easing.OutBack);
        return base.OnClick(e);
    }
}

internal partial class OrbitEmptySlot : ClickableContainer
{
    private readonly Container border;
    private readonly Box scanLine;
    private readonly SpriteIcon plus;
    private readonly SpriteText hint;

    internal OrbitEmptySlot(Action action)
    {
        Action = action;
        Size = new Vector2(365, 48);
        border = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Alpha = 0.34f,
            Children =
            [
                new Box
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Position = new Vector2(12, 0),
                    Size = new Vector2(145, 1.2f),
                    Colour = HomeControlColours.Cyan,
                },
                new Box
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Position = new Vector2(-12, 0),
                    Size = new Vector2(145, 1.2f),
                    Colour = HomeControlColours.Cyan,
                },
                new Box
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.Centre,
                    Position = new Vector2(12, 0),
                    Size = new Vector2(1.2f, 9),
                    Colour = HomeControlColours.Cyan,
                },
                new Box
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-12, 0),
                    Size = new Vector2(1.2f, 9),
                    Colour = HomeControlColours.Cyan,
                },
            ],
        };
        InternalChildren =
        [
            border,
            scanLine = new Box
            {
                Position = new Vector2(16, 7),
                Size = new Vector2(2, 34),
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    0.46f),
                Alpha = 0,
            },
            plus = new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(14),
                Icon = FontAwesome.Solid.Plus,
                Colour = HomeControlColours.Cyan,
            },
            hint = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Position = new Vector2(18, 0),
                Text = "ADD FOCUSED MOD",
                Font = HomeTypography.Display(11),
                Spacing = new Vector2(0.7f, 0),
                Colour = HomeControlColours.Cyan,
                Alpha = 0,
            },
        ];
    }

    internal void ActivateForTest() => Action?.Invoke();

    protected override bool OnHover(HoverEvent e)
    {
        border.FadeTo(0.82f, 90);
        plus.MoveToX(-61, 110, Easing.OutQuint);
        plus.RotateTo(90, 130, Easing.OutQuint);
        hint.FadeIn(100);
        scanLine.ClearTransforms();
        scanLine.X = 16;
        scanLine.FadeTo(0.46f, 45)
                .MoveToX(347, 390, Easing.OutQuint)
                .Then().FadeOut(80);
        this.ScaleTo(1.015f, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        border.FadeTo(0.34f, 110);
        plus.MoveToX(0, 120, Easing.OutQuint);
        plus.RotateTo(0, 130, Easing.OutQuint);
        hint.FadeOut(80);
        scanLine.FadeOut(70);
        this.ScaleTo(1, 120, Easing.OutQuint);
    }

    protected override bool OnClick(ClickEvent e)
    {
        this.ScaleTo(0.985f, 45, Easing.OutQuint)
            .Then().ScaleTo(1.015f, 130, Easing.OutBack);
        return base.OnClick(e);
    }
}

internal partial class OrbitRatePresetButton : ClickableContainer
{
    private readonly Box background;
    private readonly Box selectionBar;
    private readonly SpriteText label;
    private bool selected;

    internal double Value { get; }

    internal OrbitRatePresetButton(double value, Action action)
    {
        Value = value;
        Action = action;
        Size = new Vector2(56, 22);
        Masking = true;
        CornerRadius = 3;
        BorderThickness = 1;
        BorderColour = new Color4(
            HomeControlColours.Cyan.R,
            HomeControlColours.Cyan.G,
            HomeControlColours.Cyan.B,
            0.42f);
        InternalChildren =
        [
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            selectionBar = new Box
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Size = new Vector2(0, 2),
                Colour = HomeControlColours.Pink,
            },
            label = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = $"{value:0.00}x",
                Font = HomeTypography.Display(12),
                Colour = HomeControlColours.Navy,
            },
        ];
    }

    internal void SetSelected(bool value)
    {
        selected = value;
        background.FadeColour(
            value ? HomeControlColours.PaleCyan : Color4.White,
            90);
        label.FadeColour(
            value ? HomeControlColours.Cyan : HomeControlColours.Navy,
            90);
        selectionBar.ResizeWidthTo(value ? 28 : 0, 120, Easing.OutQuint);
        BorderColour = value
            ? HomeControlColours.Cyan
            : new Color4(
                HomeControlColours.Cyan.R,
                HomeControlColours.Cyan.G,
                HomeControlColours.Cyan.B,
                0.42f);
    }

    internal void ActivateForTest() => Action?.Invoke();

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(HomeControlColours.PaleCyan, 75);
        label.FadeColour(HomeControlColours.Cyan, 75);
        selectionBar.ResizeWidthTo(selected ? 36 : 22, 100, Easing.OutQuint);
        this.ScaleTo(1.06f, 80, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(
            selected ? HomeControlColours.PaleCyan : Color4.White,
            90);
        label.FadeColour(
            selected ? HomeControlColours.Cyan : HomeControlColours.Navy,
            90);
        selectionBar.ResizeWidthTo(selected ? 28 : 0, 110, Easing.OutQuint);
        this.ScaleTo(1, 100, Easing.OutQuint);
    }

    protected override bool OnClick(ClickEvent e)
    {
        this.ScaleTo(0.94f, 40, Easing.OutQuint)
            .Then().ScaleTo(1.06f, 120, Easing.OutBack);
        return base.OnClick(e);
    }
}

internal partial class OrbitSquareButton : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteIcon glyph;
    private bool enabled = true;

    internal OrbitSquareButton(IconUsage icon, Action action)
    {
        Action = action;
        Size = new Vector2(38);
        Masking = true;
        CornerRadius = 4;
        BorderThickness = 1.2f;
        BorderColour = HomeControlColours.Cyan;
        InternalChildren =
        [
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            glyph = new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(13),
                Icon = icon,
                Colour = HomeControlColours.Cyan,
            },
        ];
    }

    internal void SetEnabled(bool value)
    {
        enabled = value;
        Alpha = value ? 1 : 0.4f;
    }

    protected override bool OnClick(ClickEvent e)
    {
        if (!enabled)
            return false;
        this.ScaleTo(0.94f, 45, Easing.OutQuint)
            .Then().ScaleTo(1.08f, 120, Easing.OutBack);
        return base.OnClick(e);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!enabled)
            return false;
        background.FadeColour(HomeControlColours.PaleCyan, 80);
        this.ScaleTo(1.08f, 80, Easing.OutQuint);
        glyph.ScaleTo(1.12f, 80, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(Color4.White, 100);
        this.ScaleTo(1, 100, Easing.OutQuint);
        glyph.ScaleTo(1, 100, Easing.OutQuint);
    }
}

internal partial class OrbitRateSlider : ClickableContainer
{
    private const float track_width = 284;

    private readonly Action<double> changed;
    private readonly Action completed;
    private readonly Box fill;
    private readonly Circle marker;
    private readonly Container valuePopup;
    private readonly SpriteText valuePopupText;
    private double value = 1;
    private bool pressed;

    internal OrbitRateSlider(
        Action<double> changed,
        Action completed)
    {
        this.changed = changed;
        this.completed = completed;
        Size = new Vector2(track_width, 44);

        var ticks = new Container
        {
            RelativeSizeAxes = Axes.Both,
        };
        for (int i = 0; i <= 28; i++)
        {
            bool major = i % 7 == 0;
            ticks.Add(new Box
            {
                Position = new Vector2(
                    i * track_width / 28,
                    major ? 17 : 19),
                Size = new Vector2(1, major ? 10 : 6),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    major ? 0.25f : 0.14f),
            });
        }

        InternalChildren =
        [
            new Box
            {
                Y = 21,
                Size = new Vector2(track_width, 2),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.15f),
            },
            ticks,
            fill = new Box
            {
                Y = 21,
                Height = 2,
                Colour = HomeControlColours.Cyan,
            },
            valuePopup = new Container
            {
                Origin = Anchor.BottomCentre,
                Position = new Vector2(0, 13),
                Size = new Vector2(52, 22),
                Masking = true,
                CornerRadius = 4,
                BorderThickness = 1,
                BorderColour = HomeControlColours.Cyan,
                Alpha = 0,
                Children =
                [
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                    },
                    valuePopupText = new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = HomeTypography.Display(10),
                        Colour = HomeControlColours.Navy,
                    },
                ],
            },
            marker = new Circle
            {
                Origin = Anchor.Centre,
                Y = 22,
                Size = new Vector2(18),
                Masking = true,
                BorderThickness = 2,
                BorderColour = HomeControlColours.Pink,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
            },
        ];
    }

    internal void SetState(
        bool enabled,
        double minimum,
        double maximum,
        double nextValue)
    {
        Alpha = enabled ? 1 : 0.42f;
        setVisualValue(Math.Clamp(nextValue, 0.5, 2));
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left)
            return false;

        pressed = true;
        valuePopup.FadeIn(65);
        valuePopup.ScaleTo(1.04f, 70, Easing.OutQuint);
        marker.ScaleTo(1.18f, 70, Easing.OutQuint);
        fill.ResizeHeightTo(4, 70, Easing.OutQuint)
            .MoveToY(20, 70, Easing.OutQuint)
            .FadeColour(HomeControlColours.Pink, 70);
        updateFrom(e.ScreenSpaceMousePosition);
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e) => pressed;

    protected override void OnDrag(DragEvent e) =>
        updateFrom(e.ScreenSpaceMousePosition);

    protected override void OnMouseUp(MouseUpEvent e)
    {
        if (pressed && e.Button == MouseButton.Left)
            completed?.Invoke();

        pressed = false;
        if (!IsHovered)
            valuePopup.FadeOut(90);
        valuePopup.ScaleTo(1, 100, Easing.OutQuint);
        marker.ScaleTo(IsHovered ? 1.08f : 1, 100, Easing.OutQuint);
        fill.ResizeHeightTo(IsHovered ? 3 : 2, 100, Easing.OutQuint)
            .MoveToY(IsHovered ? 20.5f : 21, 100, Easing.OutQuint)
            .FadeColour(HomeControlColours.Cyan, 100);
        base.OnMouseUp(e);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!pressed)
        {
            valuePopup.FadeIn(80);
            marker.ScaleTo(1.08f, 80, Easing.OutQuint);
            fill.ResizeHeightTo(3, 80, Easing.OutQuint)
                .MoveToY(20.5f, 80, Easing.OutQuint);
        }
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        if (!pressed)
        {
            valuePopup.FadeOut(80);
            marker.ScaleTo(1, 100, Easing.OutQuint);
            fill.ResizeHeightTo(2, 100, Easing.OutQuint)
                .MoveToY(21, 100, Easing.OutQuint);
        }
    }

    private void updateFrom(Vector2 screenPosition)
    {
        double progress = Math.Clamp(
            ToLocalSpace(screenPosition).X / track_width,
            0,
            1);
        double nextValue = progress <= 0.5
            ? 0.5 + progress
            : 1 + (progress - 0.5) * 2;
        nextValue = Math.Round(nextValue, 2);
        if (Math.Abs(nextValue - value) < 0.0001)
            return;

        setVisualValue(nextValue);
        changed(nextValue);
    }

    private void setVisualValue(double nextValue)
    {
        value = nextValue;
        double progress = nextValue <= 1
            ? nextValue - 0.5
            : 0.5 + (nextValue - 1) / 2;
        float x = (float)(Math.Clamp(progress, 0, 1) * track_width);
        fill.Width = x;
        marker.X = x;
        valuePopup.X = x;
        valuePopupText.Text = $"{nextValue:0.00}x";
    }
}

internal enum OrbitFooterButtonStyle
{
    Back,
    Reset,
    Primary,
}

internal partial class OrbitFooterButton : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteIcon chevron;
    private readonly Box underline;
    private readonly OrbitFooterButtonStyle style;

    internal OrbitFooterButton(
        LocalisableString text,
        IconUsage icon,
        Action action,
        OrbitFooterButtonStyle style,
        string badgeText = null)
    {
        this.style = style;
        Action = action;
        Size = style switch
        {
            OrbitFooterButtonStyle.Back => new Vector2(220, 70),
            OrbitFooterButtonStyle.Reset => new Vector2(60),
            _ => new Vector2(280, 72),
        };

        var shadow = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Position = new Vector2(0, 4),
            Colour = style == OrbitFooterButtonStyle.Primary
                ? new Color4(0.01f, 0.04f, 0.28f, 0.34f)
                : new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.20f),
        };
        var surface = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = 4,
            BorderThickness = 2,
            BorderColour = HomeControlColours.Navy,
            Children =
            [
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = style == OrbitFooterButtonStyle.Primary
                        ? HomeControlColours.Navy
                        : HomeControlColours.Ivory,
                },
                new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = 18,
                    Colour = style == OrbitFooterButtonStyle.Primary
                        ? new Color4(0.01f, 0.03f, 0.30f, 0.26f)
                        : new Color4(
                            HomeControlColours.PaleCyan.R,
                            HomeControlColours.PaleCyan.G,
                            HomeControlColours.PaleCyan.B,
                            0.30f),
                },
            ],
        };

        var children = new List<Drawable>
        {
            shadow,
            surface,
        };

        if (style == OrbitFooterButtonStyle.Reset)
        {
            children.Add(new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(36),
                Icon = icon,
                Colour = HomeControlColours.Navy,
            });
            chevron = null;
            underline = null;
        }
        else
        {
            bool primary =
                style == OrbitFooterButtonStyle.Primary;
            if (badgeText != null)
            {
                children.Add(new Container
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 14,
                    Size = new Vector2(46),
                    Masking = true,
                    CornerRadius = 7,
                    BorderThickness = 1.8f,
                    BorderColour = HomeControlColours.Navy,
                    Children =
                    [
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.White,
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = badgeText,
                            Font = HomeTypography.Display(16),
                            Colour = HomeControlColours.Navy,
                        },
                    ],
                });
            }
            else
            {
                children.Add(new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.Centre,
                    Position = new Vector2(50, 0),
                    Size = new Vector2(32),
                    Icon = icon,
                    Colour = Color4.White,
                });
            }

            children.Add(new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                X = primary ? 16 : 16,
                Y = 4,
                Text = text,
                Font = new FontUsage(
                    "Yokko",
                    primary ? 56 : 50,
                    "Bold"),
                Colour = primary
                    ? Color4.White
                    : HomeControlColours.Navy,
            });
            children.Add(chevron = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -18,
                Size = new Vector2(primary ? 18 : 16),
                Icon = FontAwesome.Solid.ChevronRight,
                Colour = primary
                    ? HomeControlColours.Yellow
                    : HomeControlColours.Pink,
            });

            if (primary)
            {
                children.Add(underline = new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Position = new Vector2(45, -1),
                    Size = new Vector2(84, 3),
                    Colour = HomeControlColours.Pink,
                });
                children.Add(new HomeHazardStripes(
                    84,
                    new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.76f))
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Position = new Vector2(-24, -12),
                });
            }
            else
            {
                underline = null;
            }
        }

        var cornerCuts = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
        };
        foreach (Anchor anchor in new[]
                 {
                     Anchor.TopLeft,
                     Anchor.TopRight,
                     Anchor.BottomLeft,
                     Anchor.BottomRight,
                 })
        {
            cornerCuts.Add(new Box
            {
                Anchor = anchor,
                Origin = Anchor.Centre,
                Size = new Vector2(14),
                Rotation = 45,
                Colour = style switch
                {
                    OrbitFooterButtonStyle.Back =>
                        new Color4(0.15f, 0.74f, 0.91f, 1),
                    OrbitFooterButtonStyle.Reset =>
                        new Color4(0.185f, 0.77f, 0.915f, 1),
                    _ => new Color4(0.22f, 0.80f, 0.925f, 1),
                },
            });
        }
        children.Add(cornerCuts);

        InternalChildren = children.ToArray();
    }

    protected override bool OnHover(HoverEvent e)
    {
        this.ScaleTo(1.018f, 90, Easing.OutQuint);
        background.FadeColour(
            style == OrbitFooterButtonStyle.Primary
                ? new Color4(0.02f, 0.06f, 0.43f, 1)
                : HomeControlColours.PaleCyan,
            90);
        chevron?.MoveToX(-12, 100, Easing.OutQuint);
        underline?.ResizeWidthTo(108, 130, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.ScaleTo(1, 120, Easing.OutQuint);
        background.FadeColour(
            style == OrbitFooterButtonStyle.Primary
                ? HomeControlColours.Navy
                : Color4.White,
            120);
        chevron?.MoveToX(-18, 120, Easing.OutQuint);
        underline?.ResizeWidthTo(84, 120, Easing.OutQuint);
    }

    protected override bool OnClick(ClickEvent e)
    {
        this.ScaleTo(0.985f, 45, Easing.OutQuint)
            .Then().ScaleTo(1.018f, 120, Easing.OutBack);
        return base.OnClick(e);
    }
}

internal partial class OrbitRailArrow : ClickableContainer
{
    private readonly SpriteIcon icon;

    internal OrbitRailArrow(IconUsage iconUsage, Action action)
    {
        Action = action;
        Size = new Vector2(36);
        Child = icon = new SpriteIcon
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = new Vector2(11),
            Icon = iconUsage,
            Colour = HomeControlColours.Cyan,
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        icon.ScaleTo(1.25f, 80, Easing.OutQuint);
        icon.FadeColour(HomeControlColours.Pink, 80);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        icon.ScaleTo(1, 110, Easing.OutQuint);
        icon.FadeColour(HomeControlColours.Cyan, 110);
    }
}
