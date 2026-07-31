using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Mods;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>
/// The authored 1600x900 gameplay-mod workspace. It intentionally owns only
/// presentation and interaction; <see cref="GameplayModsScreen"/> remains the
/// single source of truth for mod selection and configuration.
/// </summary>
internal partial class GameplayModsOrbitWorkspace : CompositeDrawable
{
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

    private Container orbitHost;
    private Container nodeHost;
    private Container activeRows;
    private Container hero;
    private SpriteText heroAcronym;
    private SpriteText heroName;
    private TextFlowContainer heroDescription;
    private Box heroStateBackground;
    private SpriteText heroState;
    private SpriteText pageIndicator;
    private SpriteText rateValue;
    private SpriteText activeCount;
    private GameplayModsRateSlider rateSlider;
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

    internal void Build(Texture logo)
    {
        if (built)
            return;

        built = true;
        InternalChildren =
        [
            createHeader(logo),
            createCategoryRail(),
            createOrbit(),
            createRightPanel(),
            createDecorations(),
            createFooter(),
        ];
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

        updateHero();
        updateActiveRows();
        updateRate(selectedMods.PlaybackRate, selectedMods.FixedRateMod != null);
    }

    internal void PreviewRate(double value)
    {
        displayedRate = value;
        rateValue.Text = $"{value:0.00}x";
        rateSlider.SetState(true, 0.5, 2, value);
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
            .MoveToX(-Math.Sign(direction) * 24, 115, Easing.InCubic)
            .FadeOut(90, Easing.OutQuint);
    }

    internal void TransitionIn(int direction)
    {
        orbitHost.ClearTransforms();
        orbitHost.X = Math.Sign(direction) * 34;
        orbitHost.Alpha = 0;
        orbitHost
            .FadeIn(135, Easing.OutQuint)
            .MoveToX(0, 210, Easing.OutQuint);
    }

    private Drawable createHeader(Texture logo) => new Container
    {
        RelativeSizeAxes = Axes.X,
        Height = 132,
        Children =
        [
            new Sprite
            {
                Position = new Vector2(70, 26),
                Size = new Vector2(430, 146),
                Texture = logo,
            },
            new Box
            {
                Position = new Vector2(416, 34),
                Size = new Vector2(1.5f, 79),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.35f),
            },
            new SpriteText
            {
                Position = new Vector2(446, 34),
                Text = "GAMEPLAY MODS",
                Font = HomeTypography.Hero(39),
                Scale = new Vector2(1.02f, 1),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(448, 87),
                Text = "Customize your play experience.",
                Font = HomeTypography.Body(13),
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

        string[] labels =
        [
            "DIFFICULTY DOWN",
            "DIFFICULTY UP",
            "CONVERSION",
            "AUTOMATION",
            "FUN",
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

        container.Add(new SpriteIcon
        {
            Position = new Vector2(24, 502),
            Size = new Vector2(11),
            Icon = FontAwesome.Solid.ChevronLeft,
            Colour = HomeControlColours.Cyan,
        });
        container.Add(pageIndicator = new SpriteText
        {
            Position = new Vector2(76, 494),
            Font = HomeTypography.Display(13),
            Colour = HomeControlColours.Pink,
        });
        container.Add(new SpriteIcon
        {
            Position = new Vector2(200, 502),
            Size = new Vector2(11),
            Icon = FontAwesome.Solid.ChevronRight,
            Colour = HomeControlColours.Cyan,
        });

        return container;
    }

    private Drawable createOrbit()
    {
        orbitHost = new Container
        {
            Position = new Vector2(335, 128),
            Size = new Vector2(790, 620),
        };

        orbitHost.Add(createRing(new Vector2(34, 102), 430, 1.2f, 0.72f));
        orbitHost.Add(createRing(new Vector2(59, 127), 380, 1f, 0.27f));
        orbitHost.Add(createRing(new Vector2(91, 159), 316, 1f, 0.22f));
        orbitHost.Add(createRing(new Vector2(12, 79), 476, 1f, 0.52f));
        orbitHost.Add(hero = createHero());
        orbitHost.Add(nodeHost = new Container
        {
            RelativeSizeAxes = Axes.Both,
        });
        return orbitHost;
    }

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

    private Container createHero()
    {
        var result = new Container
        {
            Position = new Vector2(105, 178),
            Size = new Vector2(300, 266),
        };
        result.Children =
        [
            heroAcronym = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 0,
                Font = HomeTypography.Hero(92),
                Colour = HomeControlColours.Pink,
            },
            heroName = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 115,
                Font = HomeTypography.Display(20),
                Colour = HomeControlColours.Navy,
            },
            heroDescription = new TextFlowContainer(text =>
            {
                text.Font = HomeTypography.Body(14);
                text.Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.72f);
            })
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 153),
                Width = 240,
                AutoSizeAxes = Axes.Y,
                TextAnchor = Anchor.TopCentre,
            },
            heroStateBackground = new Box
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 220),
                Size = new Vector2(126, 34),
                Colour = HomeControlColours.Pink,
            },
            heroState = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 225,
                Font = HomeTypography.Display(12),
                Padding = new MarginPadding
                {
                    Horizontal = 14,
                    Vertical = 7,
                },
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
                Text = "SPEED MULTIPLIER",
                Font = HomeTypography.Display(11),
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
                Position = new Vector2(0, 76),
                Text = "1.00x",
                Font = HomeTypography.Hero(55),
                Colour = HomeControlColours.Navy,
            },
            rateMinus = new OrbitSquareButton(
                FontAwesome.Solid.Minus,
                () => previewRate(Math.Max(0.5, displayedRate - 0.01)))
            {
                Position = new Vector2(32, 162),
            },
            rateSlider = new GameplayModsRateSlider(
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
            new SpriteText
            {
                Position = new Vector2(72, 207),
                Text = "0.50x",
                Font = HomeTypography.Body(10),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(220, 207),
                Text = "1.00x",
                Font = HomeTypography.Body(10),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(391, 207),
                Text = "2.00x",
                Font = HomeTypography.Body(10),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(41, 266),
                Text = "ACTIVE MODS",
                Font = HomeTypography.Display(11),
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
                Font = HomeTypography.Display(11),
                Colour = HomeControlColours.Cyan,
            },
            activeRows = new Container
            {
                Position = new Vector2(31, 303),
                Size = new Vector2(365, 250),
            },
        ];
        return panel;
    }

    private Drawable createDecorations() => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Children =
        [
            new HomeDotField
            {
                Position = new Vector2(1160, 34),
                Size = new Vector2(55, 32),
                Colour = HomeControlColours.Cyan,
            },
            new HomeMicroLine
            {
                Position = new Vector2(1235, 52),
                Width = 268,
                Colour = HomeControlColours.Cyan,
            },
            new SpriteText
            {
                Position = new Vector2(1530, 39),
                Text = "+",
                Font = HomeTypography.Display(24),
                Colour = HomeControlColours.Yellow,
            },
            new SpriteText
            {
                Position = new Vector2(711, 79),
                Text = "+",
                Font = HomeTypography.Display(20),
                Colour = HomeControlColours.Pink,
            },
            new HomeDotField
            {
                Position = new Vector2(260, 596),
                Size = new Vector2(45, 45),
                Colour = HomeControlColours.Cyan,
            },
            new HomeDotField
            {
                Position = new Vector2(1040, 654),
                Size = new Vector2(42, 30),
                Colour = HomeControlColours.Yellow,
            },
        ],
    };

    private Drawable createFooter() => new Container
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
            new OrbitFooterButton(
                "BACK",
                FontAwesome.Solid.ChevronLeft,
                back,
                205)
            {
                Position = new Vector2(108, 26),
            },
            new OrbitFooterButton(
                "RESET",
                FontAwesome.Solid.Undo,
                reset,
                175)
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 26),
            },
            new OrbitFooterButton(
                "DONE",
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

    private void rebuildOrbitNodes()
    {
        nodeHost.Clear();
        nodes.Clear();

        IReadOnlyList<ManiaModDefinition> definitions =
            orbitDefinitions(category);
        Vector2[] positions =
        [
            new(343, 10),
            new(468, 109),
            new(512, 248),
            new(478, 386),
            new(344, 486),
            new(210, 535),
        ];

        Vector2? previousNodeCentre = null;
        for (int i = 0; i < definitions.Count && i < positions.Length; i++)
        {
            ManiaModDefinition definition = definitions[i];
            Vector2 position = positions[i];
            Vector2 nodeCentre = position + new Vector2(42);
            if (previousNodeCentre.HasValue)
            {
                nodeHost.Add(createConnector(
                    previousNodeCentre.Value,
                    nodeCentre));
            }
            previousNodeCentre = nodeCentre;

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

    private Drawable createConnector(Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        return new Box
        {
            Position = start,
            Origin = Anchor.CentreLeft,
            Size = new Vector2(delta.Length, 1.2f),
            Rotation = MathF.Atan2(delta.Y, delta.X) * 180 / MathF.PI,
            Colour = new Color4(
                HomeControlColours.Cyan.R,
                HomeControlColours.Cyan.G,
                HomeControlColours.Cyan.B,
                0.58f),
        };
    }

    private void updateHero()
    {
        ManiaModDefinition definition =
            OsuManiaModParityCatalog.Get(focusedMod);
        bool active = selectedMods.Contains(focusedMod);
        Color4 accent = accentFor(definition);
        heroAcronym.Text = definition.Acronym;
        heroAcronym.Colour = accent;
        heroName.Text = definition.Name.ToUpperInvariant();
        heroDescription.Clear();
        heroDescription.AddText(definition.Description);
        heroState.Text = active ? "ACTIVE" : "SPACE · ACTIVATE";
        heroState.Colour = active ? Color4.White : HomeControlColours.Cyan;
        heroStateBackground.Colour = active ? accent : Color4.Transparent;
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
                activeRows.Add(new OrbitEmptySlot
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

internal partial class OrbitCategoryButton : ClickableContainer
{
    private readonly Color4 accent;
    private readonly Box background;
    private readonly Circle marker;
    private readonly SpriteText number;
    private readonly SpriteIcon icon;
    private readonly SpriteText label;

    internal OrbitCategoryButton(
        int page,
        string text,
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
                Font = HomeTypography.Display(12),
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
                Font = HomeTypography.Display(11),
                Colour = HomeControlColours.Navy,
            },
        ];
    }

    internal void SetSelected(bool selected)
    {
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
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        label.MoveToX(135, 120, Easing.OutQuint);
}

internal partial class OrbitModNode : ClickableContainer
{
    private readonly Color4 accent;
    private readonly Circle surface;
    private readonly Circle innerRing;
    private readonly SpriteText acronym;
    private readonly SpriteText name;
    private readonly SpriteText description;
    private readonly Action focus;

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
                Font = HomeTypography.Display(27),
                Colour = accent,
            },
            name = new SpriteText
            {
                Position = new Vector2(104, 15),
                Text = definition.Name.ToUpperInvariant(),
                Font = HomeTypography.Display(11),
                Colour = HomeControlColours.Navy,
            },
            description = new SpriteText
            {
                Position = new Vector2(104, 39),
                Text = shorten(definition.Description, 29),
                Font = HomeTypography.Body(9.5f),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.68f),
            },
        ];
    }

    internal void SetState(bool active, bool focused, bool enabled)
    {
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
    }

    protected override bool OnHover(HoverEvent e)
    {
        focus();
        this.ScaleTo(1.08f, 100, Easing.OutQuint);
        description.FadeTo(1, 80);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        this.ScaleTo(1, 130, Easing.OutQuint);

    private static string shorten(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum] + "…";
}

internal partial class OrbitActiveModRow : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteIcon remove;

    internal OrbitActiveModRow(
        ManiaModDefinition definition,
        Color4 accent,
        Action action)
    {
        Action = action;
        Size = new Vector2(365, 54);
        Masking = true;
        CornerRadius = 4;
        BorderThickness = 1.2f;
        BorderColour = accent;
        InternalChildren =
        [
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(1, 1, 1, 0.9f),
            },
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
                Text = definition.Name.ToUpperInvariant(),
                Font = HomeTypography.Display(11),
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
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(Color4.White, 110);
        remove.RotateTo(0, 120, Easing.OutQuint);
    }
}

internal partial class OrbitEmptySlot : CompositeDrawable
{
    internal OrbitEmptySlot()
    {
        Size = new Vector2(365, 54);
        Masking = true;
        CornerRadius = 4;
        BorderThickness = 1.1f;
        BorderColour = new Color4(
            HomeControlColours.Cyan.R,
            HomeControlColours.Cyan.G,
            HomeControlColours.Cyan.B,
            0.48f);
        InternalChildren =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Transparent,
            },
            new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = "+",
                Font = HomeTypography.Display(21),
                Colour = HomeControlColours.Cyan,
            },
        ];
    }
}

internal partial class OrbitSquareButton : ClickableContainer
{
    private readonly Box background;
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
            new SpriteIcon
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
        return base.OnClick(e);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!enabled)
            return false;
        background.FadeColour(HomeControlColours.PaleCyan, 80);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        background.FadeColour(Color4.White, 100);
}

internal partial class OrbitFooterButton : ClickableContainer
{
    private readonly Box background;
    private readonly bool primary;

    internal OrbitFooterButton(
        string text,
        IconUsage icon,
        Action action,
        float width,
        bool primary = false)
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
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(primary ? 30 : 17),
                        Icon = icon,
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
                Font = HomeTypography.Display(primary ? 29 : 18),
                Colour = primary ? Color4.White : HomeControlColours.Navy,
            },
            new SpriteIcon
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
        ];
    }

    protected override bool OnHover(HoverEvent e)
    {
        this.ScaleTo(1.012f, 90, Easing.OutQuint);
        background.FadeColour(
            primary
                ? new Color4(0.02f, 0.06f, 0.43f, 1)
                : HomeControlColours.PaleCyan,
            90);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.ScaleTo(1, 120, Easing.OutQuint);
        background.FadeColour(
            primary ? HomeControlColours.Navy : Color4.White,
            120);
    }
}
