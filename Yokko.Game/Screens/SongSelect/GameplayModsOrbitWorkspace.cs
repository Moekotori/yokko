using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
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
/// The authored 1600x900 gameplay-mod workspace. It intentionally owns only
/// presentation and interaction; <see cref="GameplayModsScreen"/> remains the
/// single source of truth for mod selection and configuration.
/// </summary>
internal partial class GameplayModsOrbitWorkspace : CompositeDrawable
{
    private const float orbit_host_resting_x = 335;

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
    private readonly Action<ManiaModId> focusMod;
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
    private readonly List<OrbitConnector> connectors = new();
    private readonly List<OrbitRatePresetButton> ratePresets = new();
    private readonly List<Action> loadAnimations = new();

    private Container orbitHost;
    private Container nodeHost;
    private Container activeRows;
    private Container hero;
    private SpriteText heroAcronym;
    private SpriteText heroName;
    private TextFlowContainer heroDescription;
    private Box heroStateBackground;
    private Sprite heroStateIcon;
    private SpriteText heroState;
    private SpriteText pageIndicator;
    private SpriteText rateValue;
    private SpriteText activeCount;
    private SpriteText orbitTelemetryState;
    private OrbitRateSlider rateSlider;
    private OrbitSignalScanner orbitScanner;
    private OrbitSquareButton rateMinus;
    private OrbitSquareButton ratePlus;
    private ManiaModCategory category;
    private ManiaModId focusedMod;
    private ManiaModSet selectedMods = ManiaModSet.Empty;
    private double displayedRate = 1;
    private bool built;

    internal GameplayModsOrbitWorkspace(
        Action<ManiaModCategory> selectCategory,
        Action<ManiaModId> toggleMod,
        Action<ManiaModId> focusMod,
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
        this.focusMod = focusMod;
        this.previewRate = previewRate;
        this.completeRate = completeRate;
        this.reset = reset;
        this.back = back;
        this.done = done;
        this.definitionsForCategory = definitionsForCategory;
        this.isSelectable = isSelectable;

        Size = new Vector2(1600, 900);
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
                Alpha = 0.22f,
            },
            createHeader(logo),
            createCategoryRail(),
            createOrbit(waveformTexture),
            createRightPanel(),
            createDecorations(waveformTexture),
            createFooter(),
        ];
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        foreach (Action animation in loadAnimations)
            animation();
        loadAnimations.Clear();
    }

    internal void SetState(
        ManiaModCategory nextCategory,
        ManiaModId nextFocusedMod,
        ManiaModSet nextSelectedMods)
    {
        if (!built)
            return;

        bool rebuildOrbit = category != nextCategory || nodes.Count == 0;
        category = nextCategory;
        focusedMod = nextFocusedMod;
        selectedMods = nextSelectedMods;

        foreach ((ManiaModCategory page, OrbitCategoryButton button)
                 in categoryButtons)
        {
            button.SetSelected(page == category);
        }

        pageIndicator.Text =
            $"{Array.IndexOf(pages, category) + 1:00} / {pages.Length:00}";

        if (rebuildOrbit)
            rebuildOrbitNodes();

        foreach ((ManiaModId mod, OrbitModNode node) in nodes)
        {
            node.SetState(
                selectedMods.Contains(mod),
                mod == focusedMod,
                isSelectable(mod));
        }

        foreach (OrbitConnector connector in connectors)
        {
            connector.SetState(
                selectedMods.Contains(connector.StartMod)
                || selectedMods.Contains(connector.EndMod),
                connector.StartMod == focusedMod
                || connector.EndMod == focusedMod);
        }

        ManiaModDefinition focusedDefinition =
            OsuManiaModParityCatalog.Get(focusedMod);
        orbitTelemetryState.Text =
            $"FOCUS {focusedDefinition.Acronym}  //  ACTIVE {selectedMods.Mods.Count:00}";
        orbitScanner.SetAccent(accentFor(focusedDefinition));

        updateHero();
        updateActiveRows();
        updateRate(selectedMods.PlaybackRate, selectedMods.FixedRateMod != null);
    }

    internal void PreviewRate(double value)
    {
        displayedRate = value;
        rateValue.Text = $"{value:0.00}x";
        rateSlider.SetState(true, 0.5, 2, value);
        updateRatePresetState(value);
    }

    internal ManiaModId? GetAdjacentMod(ManiaModId current, int offset)
    {
        ManiaModId[] visible = nodes.Keys.ToArray();
        if (visible.Length == 0)
            return null;

        int currentIndex = Array.IndexOf(visible, current);
        if (currentIndex < 0)
            return visible[0];

        return visible[
            (currentIndex + Math.Sign(offset) + visible.Length)
            % visible.Length];
    }

    internal void TransitionOut(int direction)
    {
        orbitHost.ClearTransforms();
        orbitHost
            .MoveToX(
                orbit_host_resting_x - Math.Sign(direction) * 24,
                115,
                Easing.InCubic)
            .FadeOut(90, Easing.OutQuint);
    }

    internal void TransitionIn(int direction)
    {
        orbitHost.ClearTransforms();
        orbitHost.X =
            orbit_host_resting_x + Math.Sign(direction) * 34;
        orbitHost.Alpha = 0;
        orbitHost
            .FadeIn(135, Easing.OutQuint)
            .MoveToX(
                orbit_host_resting_x,
                210,
                Easing.OutQuint);
    }

    internal float OrbitContentX => orbitHost?.X ?? orbit_host_resting_x;

    private Drawable createHeader(Texture logo) => new Container
    {
        RelativeSizeAxes = Axes.X,
        Height = 132,
        Children =
        [
            new Sprite
            {
                Position = new Vector2(70, 26),
                Size = new Vector2(380, 129),
                Texture = logo,
            },
            new Box
            {
                Position = new Vector2(418, 34),
                Size = new Vector2(1.5f, 79),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.35f),
            },
            new SpriteText
            {
                Position = new Vector2(450, 32),
                Text = YokkoStrings.Get("mods.title"),
                Font = HomeTypography.Hero(43),
                Scale = new Vector2(1.02f, 1),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(452, 88),
                Text = YokkoStrings.Get("mods.subtitle"),
                Font = HomeTypography.Body(15),
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
            Font = HomeTypography.Display(15),
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

        orbitHost.Add(createRing(new Vector2(54, 122), 390, 1f, 0.38f));
        orbitHost.Add(createRing(new Vector2(72, 140), 354, 1.2f, 0.62f));
        orbitHost.Add(createRing(new Vector2(94, 162), 310, 1f, 0.25f));
        orbitHost.Add(createRing(new Vector2(119, 187), 260, 1f, 0.2f));
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

    private Drawable createOrbitTelemetry() => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Children =
        [
            new SpriteText
            {
                Position = new Vector2(74, 106),
                Text = "SYNC // MOD MATRIX",
                Font = HomeTypography.Display(9),
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
                Font = HomeTypography.Display(9),
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
                Font = HomeTypography.Display(9),
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
                Y = 0,
                Font = HomeTypography.Hero(154),
                Colour = HomeControlColours.Pink,
            },
            heroName = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 132,
                Font = HomeTypography.Display(20),
                Colour = HomeControlColours.Navy,
            },
            heroDescription = new TextFlowContainer(text =>
            {
                text.Font = HomeTypography.Body(16);
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
                Width = 175,
                AutoSizeAxes = Axes.Y,
                TextAnchor = Anchor.TopCentre,
            },
            heroStateBackground = new Box
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 226),
                Size = new Vector2(126, 34),
                Colour = HomeControlColours.Pink,
            },
            heroState = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 225,
                Font = HomeTypography.Display(14),
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
                Position = new Vector2(-40, 234),
                Size = new Vector2(34, 10),
                Texture = waveformTexture,
                Colour = Color4.White,
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
                    0.31f),
            },
            new SpriteText
            {
                Position = new Vector2(41, 28),
                Text = YokkoStrings.Get("mods.speed_multiplier"),
                Font = HomeTypography.Display(14),
                Spacing = new Vector2(1.6f, 0),
                Colour = HomeControlColours.Cyan,
            },
            new Box
            {
                Position = new Vector2(205, 38),
                Size = new Vector2(181, 1),
                Colour = HomeControlColours.Cyan,
            },
            rateValue = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 72),
                Text = "1.00x",
                Font = HomeTypography.Hero(62),
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
                Font = HomeTypography.Body(12),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 207),
                Text = "1.00x",
                Font = HomeTypography.Body(12),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-5, 207),
                Text = "2.00x",
                Font = HomeTypography.Body(12),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(41, 266),
                Text = YokkoStrings.Get("mods.active_mods"),
                Font = HomeTypography.Display(14),
                Spacing = new Vector2(1.6f, 0),
                Colour = HomeControlColours.Cyan,
            },
            new Box
            {
                Position = new Vector2(159, 276),
                Size = new Vector2(200, 1),
                Colour = HomeControlColours.Cyan,
            },
            activeCount = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-5, 266),
                Font = HomeTypography.Display(13),
                Colour = HomeControlColours.Cyan,
            },
            activeRows = new Container
            {
                Position = new Vector2(31, 300),
                Size = new Vector2(365, 250),
            },
        ];
        return panel;
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
        };

        loadAnimations.Add(() =>
        {
            topDots.FadeTo(0.34f)
                   .Then().FadeTo(1, 1300, Easing.InOutSine)
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
                    .Then().FadeTo(1, 1100, Easing.InOutSine)
                    .Then().FadeTo(0.74f, 1100, Easing.InOutSine)
                    .Loop();
            pinkPlus.RotateTo(-7)
                    .Then().RotateTo(7, 1800, Easing.InOutSine)
                    .Then().RotateTo(-7, 1800, Easing.InOutSine)
                    .Loop();
            lowerDots.FadeTo(0.38f)
                     .Then().FadeTo(1, 1700, Easing.InOutSine)
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
                    0.7f),
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
            Position = new Vector2(642, 72),
            Size = new Vector2(34, 30),
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
                Colour = HomeControlColours.Cyan,
            },
            new HomeDotField
            {
                Position = new Vector2(8, 78),
                Size = new Vector2(72, 34),
                Colour = new Color4(1, 1, 1, 0.52f),
            },
            new SpriteText
            {
                Position = new Vector2(431, 35),
                Text = "+",
                Font = HomeTypography.Display(24),
                Colour = Color4.White,
            },
            footerDots,
            scanLine,
            new Box
            {
                Position = new Vector2(299, 17),
                Size = new Vector2(17),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
            },
            new OrbitFooterButton(
                YokkoStrings.Get("mods.back"),
                FontAwesome.Solid.ChevronLeft,
                back,
                205,
                false,
                "ESC")
            {
                Position = new Vector2(108, 26),
            },
            new OrbitFooterButton(
                YokkoStrings.Get("mods.reset"),
                FontAwesome.Solid.Undo,
                reset,
                175)
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 26),
            },
            new OrbitFooterButton(
                YokkoStrings.Get("mods.done"),
                FontAwesome.Solid.Play,
                done,
                440,
                true)
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-22, 17),
            },
            ],
        };
    }

    private void rebuildOrbitNodes()
    {
        nodeHost.Clear();
        nodes.Clear();
        connectors.Clear();

        IReadOnlyList<ManiaModDefinition> definitions =
            orbitDefinitions(category);
        Vector2[] positions =
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
        for (int i = 0; i < definitions.Count && i < positions.Length; i++)
        {
            ManiaModDefinition definition = definitions[i];
            Vector2 position = positions[i];
            Vector2 nodeCentre = position + new Vector2(42);
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
                () => toggleMod(definition.Id),
                () => focusMod(definition.Id))
            {
                Position = position,
            };
            nodes[definition.Id] = node;
            nodeHost.Add(node);
        }
    }

    private IReadOnlyList<ManiaModDefinition> orbitDefinitions(
        ManiaModCategory page)
    {
        ManiaModId[] showcase = page switch
        {
            ManiaModCategory.DifficultyIncrease =>
            [
                ManiaModId.Easy,
                ManiaModId.HalfTime,
                ManiaModId.HardRock,
                ManiaModId.Hidden,
                ManiaModId.DoubleTime,
                ManiaModId.Flashlight,
            ],
            ManiaModCategory.DifficultyReduction =>
            [
                ManiaModId.Easy,
                ManiaModId.HalfTime,
                ManiaModId.NoFail,
                ManiaModId.NoRelease,
                ManiaModId.Daycore,
                ManiaModId.SuddenDeath,
            ],
            _ => [],
        };

        if (showcase.Length > 0)
        {
            return showcase
                .Select(OsuManiaModParityCatalog.Get)
                .ToArray();
        }

        return definitionsForCategory(page)
            .Where(definition => isSelectable(definition.Id))
            .Take(6)
            .ToArray();
    }

    private void updateHero()
    {
        ManiaModDefinition definition =
            OsuManiaModParityCatalog.Get(focusedMod);
        bool active = selectedMods.Contains(focusedMod);
        Color4 accent = accentFor(definition);
        heroAcronym.Text = definition.Acronym;
        heroAcronym.Colour = accent;
        heroName.Text = YokkoStrings.ModName(definition)
            .ToString()
            .ToUpperInvariant();
        heroDescription.Clear();
        heroDescription.AddText(YokkoStrings.ModDescription(definition));
        heroState.Text = YokkoStrings.Get(
            active ? "mods.active" : "mods.activate_hint");
        heroState.X = active ? 9 : 0;
        heroState.Colour = active ? Color4.White : HomeControlColours.Cyan;
        heroStateBackground.Colour = active ? accent : Color4.Transparent;
        heroStateIcon.Alpha = active ? 1 : 0;
        hero.FadeTo(1, 80);
    }

    private void updateActiveRows()
    {
        activeRows.Clear();
        ManiaModId[] active = selectedMods.Mods
            .Where(mod => !isKeyConversionMod(mod))
            .OrderBy(mod => mod == focusedMod ? 0 : 1)
            .Take(4)
            .ToArray();
        activeCount.Text = $"({active.Length}/5)";

        for (int i = 0; i < 4; i++)
        {
            if (i < active.Length)
            {
                ManiaModDefinition definition =
                    OsuManiaModParityCatalog.Get(active[i]);
                activeRows.Add(new OrbitActiveModRow(
                    definition,
                    accentFor(definition),
                    () => toggleMod(definition.Id))
                {
                    Y = i * 66,
                });
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
                    Y = i * 66,
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
                Font = HomeTypography.Display(15),
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
                Size = new Vector2(38),
                Colour = accent,
            },
            icon = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(98, 0),
                Size = new Vector2(16),
                Icon = iconUsage,
                Colour = Color4.White,
            },
            label = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 135,
                Text = text,
                Font = HomeTypography.Display(14),
                Colour = HomeControlColours.Navy,
            },
            selectionDiamond = new Box
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-4, 0),
                Size = new Vector2(14),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
                Alpha = 0,
            },
        ];
    }

    internal void SetSelected(bool selected)
    {
        this.selected = selected;
        background.FadeColour(
            selected
                ? new Color4(
                    HomeControlColours.PaleCyan.R,
                    HomeControlColours.PaleCyan.G,
                    HomeControlColours.PaleCyan.B,
                    0.7f)
                : Color4.Transparent,
            110);
        marker.FadeColour(selected ? accent : HomeControlColours.Navy, 110);
        number.FadeColour(selected ? accent : HomeControlColours.Navy, 110);
        icon.ScaleTo(selected ? 1.08f : 1, 110, Easing.OutQuint);
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
        label.MoveToX(135, 120, Easing.OutQuint);
        label.FadeColour(HomeControlColours.Navy, 110);
        icon.ScaleTo(selected ? 1.08f : 1, 110, Easing.OutQuint);
        background.FadeColour(
            selected
                ? new Color4(
                    HomeControlColours.PaleCyan.R,
                    HomeControlColours.PaleCyan.G,
                    HomeControlColours.PaleCyan.B,
                    0.7f)
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
    private readonly Circle startJoint;
    private readonly Circle endJoint;
    private readonly List<Box> dataTicks = new();
    private bool active;
    private bool focused;

    internal ManiaModId StartMod { get; }
    internal ManiaModId EndMod { get; }

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

        InternalChildren =
        [
            glow,
            outerRail,
            main,
            startJoint,
            endJoint,
            signal,
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

    internal void SetState(bool active, bool focused)
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
    private readonly Color4 accent;
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

    internal OrbitModNode(
        ManiaModDefinition definition,
        Color4 accent,
        Action action,
        Action focus)
    {
        this.accent = accent;
        this.focus = focus;
        Action = action;
        Size = new Vector2(220, 86);
        InternalChildren =
        [
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
            surface = new Circle
            {
                Size = new Vector2(84),
                Masking = true,
                BorderThickness = 1.5f,
                BorderColour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.18f),
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
                    0.16f),
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
                Font = HomeTypography.Display(31),
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
                Position = new Vector2(104, 15),
                Text = YokkoStrings.ModName(definition)
                    .ToString()
                    .ToUpperInvariant(),
                Font = HomeTypography.Display(15),
                Colour = HomeControlColours.Navy,
            },
            description = new TextFlowContainer(text =>
            {
                text.Font = HomeTypography.Body(14);
                text.Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.68f);
            })
            {
                Position = new Vector2(104, 38),
                Width = 150,
                AutoSizeAxes = Axes.Y,
            },
        ];
        description.AddText(YokkoStrings.ModDescription(definition));
    }

    internal void SetState(bool active, bool focused, bool enabled)
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
                0.18f);
        surface.BorderThickness = active ? 2.3f : focused ? 1.8f : 1.5f;
        innerRing.BorderColour = active
            ? new Color4(accent.R, accent.G, accent.B, 0.7f)
            : new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.16f);
        acronym.Colour = active || focused ? accent : HomeControlColours.Navy;
        this.ScaleTo(focused ? 1.08f : 1, 125, Easing.OutQuint);
        stateGlyph.Icon = active
            ? FontAwesome.Solid.Check
            : FontAwesome.Solid.Plus;
        stateBadge.FadeTo(active || focused ? 1 : 0, 90);
        stateGlyph.FadeTo(active || focused ? 1 : 0, 90);
        stateBadge.ScaleTo(active ? 1.06f : 1, 110, Easing.OutQuint);

        if (activeChanged)
        {
            halo.ClearTransforms();
            if (active)
            {
                halo.Alpha = 0.42f;
                if (IsLoaded)
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
        if (activeState)
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

    protected override bool OnHover(HoverEvent e)
    {
        focus();
        this.ScaleTo(1.08f, 100, Easing.OutQuint);
        description.FadeTo(1, 80);
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
        this.ScaleTo(focusedState ? 1.08f : 1, 130, Easing.OutQuint);
        surface.BorderColour = activeState || focusedState
            ? accent
            : new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.18f);
        surface.BorderThickness = activeState ? 2.3f : focusedState ? 1.8f : 1.5f;
        if (!activeState)
            halo.FadeOut(110);
    }

    protected override bool OnClick(ClickEvent e)
    {
        stateBadge.ScaleTo(0.76f, 45, Easing.OutQuint)
                  .Then().ScaleTo(1.08f, 150, Easing.OutBack);
        this.ScaleTo(0.97f, 45, Easing.OutQuint)
            .Then().ScaleTo(1.08f, 150, Easing.OutBack);
        return base.OnClick(e);
    }

    private static string shorten(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum] + "…";
}

internal partial class OrbitActiveModRow : ClickableContainer
{
    private readonly Container background;
    private readonly SpriteIcon remove;

    internal OrbitActiveModRow(
        ManiaModDefinition definition,
        Color4 accent,
        Action action)
    {
        Action = action;
        Size = new Vector2(365, 54);
        background = createHexagonLayer(
            Color4.White,
            new Vector2(362, 51));
        background.Position = new Vector2(1.5f);
        InternalChildren =
        [
            createHexagonLayer(accent, new Vector2(365, 54)),
            background,
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 34,
                Text = definition.Acronym,
                Font = HomeTypography.Display(20),
                Colour = accent,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 102,
                Text = YokkoStrings.ModName(definition)
                    .ToString()
                    .ToUpperInvariant(),
                Font = HomeTypography.Display(14),
                Colour = HomeControlColours.Navy,
            },
            remove = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -24,
                Size = new Vector2(11),
                Icon = FontAwesome.Solid.Times,
                Colour = HomeControlColours.Pink,
            },
        ];
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(HomeControlColours.PaleCyan, 80);
        remove.RotateTo(90, 120, Easing.OutQuint);
        remove.ScaleTo(1.24f, 100, Easing.OutQuint);
        this.MoveToX(4, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(Color4.White, 110);
        remove.RotateTo(0, 120, Easing.OutQuint);
        remove.ScaleTo(1, 110, Easing.OutQuint);
        this.MoveToX(0, 120, Easing.OutQuint);
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

internal partial class OrbitEmptySlot : ClickableContainer
{
    private readonly Container border;
    private readonly SpriteIcon plus;
    private readonly SpriteText hint;

    internal OrbitEmptySlot(Action action)
    {
        Action = action;
        Size = new Vector2(365, 54);
        border = new Container
        {
            RelativeSizeAxes = Axes.Both,
        };
        Color4 dashColour = new(
            HomeControlColours.Cyan.R,
            HomeControlColours.Cyan.G,
            HomeControlColours.Cyan.B,
            0.55f);
        for (int x = 14; x < 350; x += 20)
        {
            border.Add(new Box
            {
                Position = new Vector2(x, 0),
                Size = new Vector2(12, 1.2f),
                Colour = dashColour,
            });
            border.Add(new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Position = new Vector2(x, 0),
                Size = new Vector2(12, 1.2f),
                Colour = dashColour,
            });
        }
        for (int y = 7; y < 48; y += 11)
        {
            border.Add(new Box
            {
                Position = new Vector2(7, y),
                Size = new Vector2(1.2f, 6),
                Rotation = 18,
                Colour = dashColour,
            });
            border.Add(new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-7, y),
                Size = new Vector2(1.2f, 6),
                Rotation = -18,
                Colour = dashColour,
            });
        }
        InternalChildren =
        [
            border,
            plus = new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(13),
                Icon = FontAwesome.Solid.Plus,
                Colour = HomeControlColours.Cyan,
            },
            hint = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Position = new Vector2(18, 0),
                Text = "ADD FOCUSED MOD",
                Font = HomeTypography.Display(10),
                Spacing = new Vector2(0.7f, 0),
                Colour = HomeControlColours.Cyan,
                Alpha = 0,
            },
        ];
    }

    internal void ActivateForTest() => Action?.Invoke();

    protected override bool OnHover(HoverEvent e)
    {
        border.FadeColour(HomeControlColours.PaleCyan, 90);
        border.FadeTo(0.72f, 90);
        plus.MoveToX(-61, 110, Easing.OutQuint);
        plus.RotateTo(90, 130, Easing.OutQuint);
        hint.FadeIn(100);
        this.ScaleTo(1.015f, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        border.FadeColour(Color4.White, 110);
        border.FadeTo(1, 110);
        plus.MoveToX(0, 120, Easing.OutQuint);
        plus.RotateTo(0, 130, Easing.OutQuint);
        hint.FadeOut(80);
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
    private readonly SpriteText label;
    private bool selected;

    internal double Value { get; }

    internal OrbitRatePresetButton(double value, Action action)
    {
        Value = value;
        Action = action;
        Size = new Vector2(54, 20);
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
            label = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = $"{value:0.00}x",
                Font = HomeTypography.Display(9),
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
        Size = new Vector2(36);
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
                Size = new Vector2(12),
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
    private double value = 1;
    private bool pressed;

    internal OrbitRateSlider(
        Action<double> changed,
        Action completed)
    {
        this.changed = changed;
        this.completed = completed;
        Size = new Vector2(track_width, 28);

        var ticks = new Container
        {
            RelativeSizeAxes = Axes.Both,
        };
        for (int i = 0; i <= 28; i++)
        {
            bool major = i % 7 == 0;
            ticks.Add(new Box
            {
                Position = new Vector2(i * track_width / 28, major ? 9 : 11),
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
                Y = 13,
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
                Y = 13,
                Height = 2,
                Colour = HomeControlColours.Cyan,
            },
            marker = new Circle
            {
                Origin = Anchor.Centre,
                Y = 14,
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
        marker.ScaleTo(1.18f, 70, Easing.OutQuint);
        fill.ResizeHeightTo(4, 70, Easing.OutQuint)
            .MoveToY(12, 70, Easing.OutQuint)
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
        marker.ScaleTo(IsHovered ? 1.08f : 1, 100, Easing.OutQuint);
        fill.ResizeHeightTo(IsHovered ? 3 : 2, 100, Easing.OutQuint)
            .MoveToY(IsHovered ? 12.5f : 13, 100, Easing.OutQuint)
            .FadeColour(HomeControlColours.Cyan, 100);
        base.OnMouseUp(e);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!pressed)
        {
            marker.ScaleTo(1.08f, 80, Easing.OutQuint);
            fill.ResizeHeightTo(3, 80, Easing.OutQuint)
                .MoveToY(12.5f, 80, Easing.OutQuint);
        }
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        if (!pressed)
        {
            marker.ScaleTo(1, 100, Easing.OutQuint);
            fill.ResizeHeightTo(2, 100, Easing.OutQuint)
                .MoveToY(13, 100, Easing.OutQuint);
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
    }
}

internal partial class OrbitFooterButton : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteIcon chevron;
    private readonly Box underline;
    private readonly bool primary;

    internal OrbitFooterButton(
        LocalisableString text,
        IconUsage icon,
        Action action,
        float width,
        bool primary = false,
        string badgeText = null)
    {
        this.primary = primary;
        Action = action;
        Size = new Vector2(width, primary ? 96 : 76);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = primary ? 0 : 1.5f;
        BorderColour = HomeControlColours.Navy;
        InternalChildren =
        [
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = primary ? HomeControlColours.Navy : Color4.White,
            },
            new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = primary ? 18 : 13,
                Size = new Vector2(primary ? 76 : 48),
                Masking = true,
                CornerRadius = 6,
                BorderThickness = primary ? 0 : 1.2f,
                BorderColour = HomeControlColours.Navy,
                Children =
                [
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                    },
                    badgeText == null
                        ? new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(primary ? 30 : 17),
                            Icon = icon,
                            Colour = text.ToString() == "RESET"
                                ? HomeControlColours.Cyan
                                : HomeControlColours.Navy,
                        }
                        : new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = badgeText,
                            Font = HomeTypography.Display(11),
                            Colour = HomeControlColours.Navy,
                        },
                ],
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = primary ? 126 : 74,
                Text = text,
                Font = HomeTypography.Display(primary ? 29 : 22),
                Colour = primary ? Color4.White : HomeControlColours.Navy,
            },
            chevron = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -24,
                Size = new Vector2(primary ? 18 : 11),
                Icon = FontAwesome.Solid.ChevronRight,
                Colour = primary
                    ? HomeControlColours.Yellow
                    : HomeControlColours.Pink,
            },
            underline = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Position = new Vector2(primary ? 128 : 0, -1),
                Size = new Vector2(primary ? 84 : 0, 2),
                Colour = HomeControlColours.Pink,
                Alpha = primary ? 1 : 0,
            },
        ];
    }

    protected override bool OnHover(HoverEvent e)
    {
        this.ScaleTo(1.018f, 90, Easing.OutQuint);
        background.FadeColour(
            primary
                ? new Color4(0.02f, 0.06f, 0.43f, 1)
                : HomeControlColours.PaleCyan,
            90);
        chevron.MoveToX(-16, 100, Easing.OutQuint);
        if (primary)
            underline.ResizeWidthTo(118, 130, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.ScaleTo(1, 120, Easing.OutQuint);
        background.FadeColour(
            primary ? HomeControlColours.Navy : Color4.White,
            120);
        chevron.MoveToX(-24, 120, Easing.OutQuint);
        if (primary)
            underline.ResizeWidthTo(84, 120, Easing.OutQuint);
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
