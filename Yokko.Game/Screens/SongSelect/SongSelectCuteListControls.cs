using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Analysis;
using Yokko.Core.Difficulty;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>
/// Pooled song row following the osu!lazer carousel panel model
/// (ppy/osu osu.Game/Screens/SelectV2/PanelBeatmap.cs at commit
/// 83b8a64bec19e1463353645c2d6d10c75e275b43): the drawable tree is built
/// exactly once in the constructor, and <see cref="Bind"/> only rewrites
/// text, textures, colours and visibility when the pooled instance is
/// reused for another entry. Compact (package chart) and standalone
/// layouts live in two prebuilt layers whose visibility is toggled.
/// </summary>
internal partial class SongSelectSongRow : PoolableDrawable
{
    internal const float RowWidth = 980;
    internal const float CompactHeight = 56;
    internal const float StandaloneHeight = 132;
    internal static readonly Vector2 StandaloneArtworkSize = new(220, 124);
    internal const float CompactLeadingAccentWidth = 3;
    internal const float CompactLeadingAccentOpacity = 0.48f;
    internal const float CompactSelectionOutlineThickness = 1.25f;
    internal const float CompactSelectedFillOpacity = 0.18f;

    private readonly Drawable baseShadow;
    private readonly Box baseShadowFill;
    private readonly Drawable focusShadow;
    private readonly Box focusShadowFill;
    private readonly Container panel;
    private readonly Box surface;
    private readonly Box selectedSurface;
    private readonly Box leadingAccent;
    private readonly Box accentWash;
    private readonly Container selectionOutline;
    private readonly Box selectionSignalRail;
    private readonly SpriteIcon arrow;
    private readonly Sprite selectedSticker;

    private readonly Container compactLayer;
    private readonly Box compactTickVertical;
    private readonly Box compactTickHorizontal;
    private readonly SpriteText compactDifficultyUnit;
    private readonly SpriteText compactDifficultyValue;
    private readonly SpriteText compactPrimaryText;
    private readonly SpriteText compactSecondaryText;
    private readonly SpriteText compactPatternSummary;
    private readonly SongSelectProgressiveModePill compactModePill;

    private readonly Container standaloneLayer;
    private readonly Container standaloneArtworkFrame;
    private readonly Sprite artworkSprite;
    private readonly SpriteText standaloneTitle;
    private readonly SpriteText standaloneArtist;
    private readonly SpriteText standaloneCreator;
    private readonly SpriteText standalonePatternSummary;
    private readonly SpriteText standaloneModeText;
    private readonly SpriteText standaloneRatingUnit;
    private readonly SpriteText standaloneRatingValue;

    private SpriteText patternSummary;
    private readonly List<(Box Box, float Alpha)> accentBoxes = [];
    private readonly List<(Container Container, float Alpha)> accentBorders = [];
    private readonly List<SpriteText> difficultyValueTexts = [];
    private readonly List<SpriteText> difficultyUnitTexts = [];
    private readonly List<(SpriteText Text, Color4 Normal, Color4 Selected)>
        adaptiveTexts = [];
    private Color4 accent;
    private ManiaDifficultyRatings displayedDifficultyRatings;
    private bool selected;
    private bool compact;
    private float restingX;
    private float selectionIndent;

    public SongSelectEntry Entry { get; private set; }
    public Action Action { get; private set; }
    public Action DoubleClickAction { get; private set; }
    internal ManiaDifficultyRatings DisplayedDifficultyRatings =>
        displayedDifficultyRatings;
    internal float FocusShadowAlpha => focusShadow.Alpha;
    internal float SelectionIndent => selectionIndent;
    internal float ModePillWidth => compactModePill.Width;
    internal float ModePillX => compactModePill.X;
    internal float CompactModeTextAlpha =>
        compactModePill.CompactTextAlpha;
    internal float ExpandedModeTextAlpha =>
        compactModePill.ExpandedTextAlpha;
    internal string CompactPrimaryText =>
        compactPrimaryText.Text.ToString();
    internal string CompactSecondaryText =>
        compactSecondaryText.Text.ToString();
    internal string PatternSummaryText =>
        patternSummary?.Text.ToString() ?? string.Empty;
    internal float PatternSummaryAlpha => patternSummary?.Alpha ?? 0;
    internal Vector2 StandaloneArtworkFrameSize =>
        standaloneArtworkFrame.Size;
    internal float LeadingAccentWidth => leadingAccent.Width;
    internal float SelectionOutlineThickness =>
        selectionOutline.BorderThickness;
    internal float SelectedStickerAlpha => selectedSticker.Alpha;
    internal Vector2 SelectedStickerScale => selectedSticker.Scale;
    internal float SelectionSignalRailAlpha => selectionSignalRail.Alpha;
    internal float SelectionSignalRailWidth => selectionSignalRail.Width;

    public SongSelectSongRow()
    {
        baseShadow = SongSelectSurface.CreateShadow(
            out baseShadowFill,
            9,
            0.04f,
            1);
        focusShadow = SongSelectSurface.CreateShadow(
            out focusShadowFill,
            9,
            0.12f,
            2);
        focusShadow.Alpha = 0;
        panel = SongSelectSurface.CreateCard(
            out surface,
            SongSelectSurface.Ivory(0.98f),
            SongSelectSurface.Border(0.12f),
            9,
            1);
        InternalChildren =
        [
            baseShadow,
            focusShadow,
            panel,
            leadingAccent = new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = CompactLeadingAccentWidth,
            },
            accentWash = new Box
            {
                RelativeSizeAxes = Axes.Both,
            },
            // Keep the selected surface genuinely ivory. Placing it after the
            // accent wash avoids the muddy blue tint seen in the first pass.
            selectedSurface = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
            },
            selectionOutline = new Container
            {
                Masking = true,
                CornerRadius = 8,
                Alpha = 0,
                Child = new Box
                {
                    Alpha = 0,
                },
            },
            selectionSignalRail = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Width = 0,
                Height = 2,
                Colour = ColourInfo.GradientHorizontal(
                    SongSelectTheme.Cyan,
                    SongSelectTheme.Pink),
                Alpha = 0,
            },
            arrow = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Size = new Vector2(12),
                Icon = FontAwesome.Solid.Play,
                Colour = SongSelectTheme.Pink,
                Alpha = 0,
            },
            selectedSticker = new Sprite
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-16, 5),
                Size = new Vector2(32),
                FillMode = FillMode.Fit,
                Alpha = 0,
            },
            compactLayer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                Children =
                [
                    compactTickVertical = new Box
                    {
                        Position = new Vector2(-7, -5),
                        Width = 1,
                        Height = CompactHeight + 5,
                    },
                    compactTickHorizontal = new Box
                    {
                        Position = new Vector2(-7, CompactHeight / 2),
                        Width = 7,
                        Height = 1,
                    },
                    new SongSelectInlineDifficultyRating(
                        compactDifficultyUnit = new SpriteText
                        {
                            Font = HomeTypography.Display(16),
                        },
                        compactDifficultyValue = new SpriteText
                        {
                            Font = HomeTypography.Display(21),
                            Colour = SongSelectTheme.Navy,
                        })
                    {
                        Position = new Vector2(650, 17),
                    },
                    compactPrimaryText = new SpriteText
                    {
                        Position = new Vector2(28, 5),
                        Width = 540,
                        Truncate = true,
                        Font = HomeTypography.Display(23),
                        Colour = SongSelectTheme.Navy,
                    },
                    compactSecondaryText = new SpriteText
                    {
                        Position = new Vector2(28, 33),
                        Width = 540,
                        Truncate = true,
                        Font = HomeTypography.Body(16),
                    },
                    compactPatternSummary = new SpriteText
                    {
                        Position = new Vector2(330, 34),
                        Width = 300,
                        Truncate = true,
                        Font = HomeTypography.Display(15),
                        Colour = SongSelectTheme.Navy,
                        Alpha = 0,
                    },
                    compactModePill = new SongSelectProgressiveModePill(13),
                ],
            },
            standaloneLayer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                Children =
                [
                    standaloneArtworkFrame = new Container
                    {
                        Position = new Vector2(8, 4),
                        Size = StandaloneArtworkSize,
                        Masking = true,
                        CornerRadius = 7,
                        BorderThickness = 1,
                        BorderColour = new Color4(1f, 1f, 1f, 0.52f),
                        Children =
                        [
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = SongSelectTheme.DeepNavy,
                            },
                            artworkSprite = SongSelectArtworkCrop.CreateFit(
                                null,
                                StandaloneArtworkSize),
                        ],
                    },
                    standaloneTitle = new SpriteText
                    {
                        Position = new Vector2(246, 15),
                        Width = 514,
                        Truncate = true,
                        Font = HomeTypography.Display(26),
                        Colour = SongSelectTheme.Navy,
                    },
                    standaloneArtist = new SpriteText
                    {
                        Position = new Vector2(246, 52),
                        Width = 500,
                        Truncate = true,
                        Font = HomeTypography.Display(17),
                    },
                    standaloneCreator = new SpriteText
                    {
                        Position = new Vector2(246, 77),
                        Width = 500,
                        Truncate = true,
                        Font = HomeTypography.Body(17),
                        Colour = SongSelectTheme.Cyan,
                    },
                    standalonePatternSummary = new SpriteText
                    {
                        Position = new Vector2(246, 104),
                        Width = 472,
                        Truncate = true,
                        Font = HomeTypography.Display(15),
                        Colour = SongSelectTheme.Navy,
                        Alpha = 0,
                    },
                    new Container
                    {
                        Position = new Vector2(728, 90),
                        Size = new Vector2(176, 32),
                        Masking = true,
                        CornerRadius = 7,
                        Children =
                        [
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = new Color4(
                                    SongSelectTheme.Pink.R,
                                    SongSelectTheme.Pink.G,
                                    SongSelectTheme.Pink.B,
                                    0.86f),
                            },
                            standaloneModeText = new SpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Width = 160,
                                Truncate = true,
                                Font = HomeTypography.Display(16),
                                Colour = Color4.White,
                            },
                        ],
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.BottomRight,
                        Origin = Anchor.BottomRight,
                        Position = new Vector2(-18, -18),
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(2, 0),
                        Children =
                        [
                            standaloneRatingUnit = new SpriteText
                            {
                                Font = HomeTypography.Display(15),
                            },
                            standaloneRatingValue = new SpriteText
                            {
                                Font = HomeTypography.Display(20),
                                Colour = SongSelectTheme.Ivory,
                            },
                        ],
                    },
                ],
            },
        ];
    }

    public void Bind(
        SongSelectEntry entry,
        ManiaDifficultyRatings ratings,
        ManiaDifficultyRatingMode difficultyRatingMode,
        Texture wallpaper,
        Texture selectedStickerTexture,
        string compactPrimaryTextOverride,
        Action select,
        Action play)
    {
        ClearTransforms(true);
        Alpha = 1;
        selected = false;
        selectionIndent = 0;
        Entry = entry;
        Action = select;
        DoubleClickAction = play;
        compact = entry.IsPackage;
        float rowHeight = compact ? CompactHeight : StandaloneHeight;
        restingX = compact ? 14 : 0;
        ArgumentNullException.ThrowIfNull(ratings);
        displayedDifficultyRatings = ratings;
        accent = DifficultyColour(
            ratings,
            difficultyRatingMode);
        X = restingX;
        Size = new Vector2(RowWidth - restingX, rowHeight);

        baseShadowFill.Colour = SongSelectSurface.ShadowTint(
            compact ? 0.04f : 0.22f);
        baseShadow.Y = compact ? 1 : 2;
        focusShadowFill.Colour = SongSelectSurface.ShadowTint(
            compact ? 0.12f : 0.27f);
        focusShadow.Y = compact ? 2 : 4;
        focusShadow.Alpha = 0;
        surface.Colour = SongSelectSurface.Ivory(0.98f);
        float panelBorderAlpha = compact ? 0.12f : 0.38f;
        panel.BorderColour = compact
            ? SongSelectSurface.Border(panelBorderAlpha)
            : withAlpha(accent, panelBorderAlpha);
        selectedSurface.Colour = compact
            ? new Color4(
                1f,
                0.96f,
                0.66f,
                CompactSelectedFillOpacity)
            : new Color4(1f, 0.98f, 0.78f, 1f);
        selectedSurface.Alpha = 0;
        leadingAccent.Width = compact ? CompactLeadingAccentWidth : 7;
        leadingAccent.Colour = compact
            ? withAlpha(accent, CompactLeadingAccentOpacity)
            : accent;
        accentWash.Colour = withAlpha(accent, compact ? 0.008f : 0.06f);
        selectionOutline.Position = compact ? new Vector2(1) : new Vector2(2);
        selectionOutline.Size = compact
            ? new Vector2(RowWidth - restingX - 2, rowHeight - 2)
            : new Vector2(RowWidth - restingX - 4, rowHeight - 4);
        selectionOutline.BorderThickness = compact
            ? CompactSelectionOutlineThickness
            : 2;
        selectionOutline.BorderColour = compact
            ? SongSelectTheme.Yellow
            : SongSelectTheme.Cyan;
        selectionOutline.Alpha = 0;
        selectionSignalRail.Position = new Vector2(compact ? 7 : 10, -1);
        selectionSignalRail.Width = 0;
        selectionSignalRail.Alpha = 0;
        arrow.X = compact ? -9 : 8;
        arrow.Alpha = 0;
        selectedSticker.Texture = selectedStickerTexture;
        selectedSticker.Alpha = 0;
        selectedSticker.Scale = Vector2.One;
        selectedSticker.Rotation = 0;

        accentBoxes.Clear();
        accentBorders.Clear();
        difficultyValueTexts.Clear();
        difficultyUnitTexts.Clear();
        adaptiveTexts.Clear();
        accentBoxes.Add((
            leadingAccent,
            compact ? CompactLeadingAccentOpacity : 1));
        accentBoxes.Add((accentWash, compact ? 0.008f : 0.06f));
        if (!compact)
            accentBorders.Add((panel, panelBorderAlpha));

        compactLayer.Alpha = compact ? 1 : 0;
        standaloneLayer.Alpha = compact ? 0 : 1;
        if (compact)
        {
            bindCompactContent(
                entry,
                difficultyRatingMode,
                compactPrimaryTextOverride);
        }
        else
        {
            bindStandaloneContent(
                entry,
                wallpaper,
                difficultyRatingMode);
        }
    }

    /// <summary>
    /// Swaps the placeholder for asynchronously decoded artwork with a fade.
    /// Compact (package chart) rows have no artwork and ignore the call.
    /// </summary>
    internal void SetArtwork(Texture texture)
    {
        if (Entry == null || compact || texture == null)
            return;

        artworkSprite.Texture = texture;
        artworkSprite.Size = SongSelectArtworkCrop.CalculateFitSize(
            new Vector2(texture.DisplayWidth, texture.DisplayHeight),
            StandaloneArtworkSize);
        artworkSprite.FadeInFromZero(180, Easing.OutQuint);
    }

    public void SetDifficulty(
        ManiaDifficultyRatings ratings,
        ManiaDifficultyRatingMode mode)
    {
        ArgumentNullException.ThrowIfNull(ratings);
        displayedDifficultyRatings = ratings;
        accent = DifficultyColour(ratings, mode);

        foreach (SpriteText text in difficultyValueTexts)
        {
            text.Text = ManiaDifficultyPresentation.FormatValue(
                ratings,
                mode);
        }

        foreach (SpriteText text in difficultyUnitTexts)
        {
            text.Text = ManiaDifficultyPresentation.Unit(mode);
            text.Colour = accent;
        }

        foreach ((Box box, float alpha) in accentBoxes)
            box.Colour = withAlpha(accent, alpha);
        foreach ((Container border, float alpha) in accentBorders)
            border.BorderColour = withAlpha(accent, alpha);
    }

    public void SetSelected(bool value) =>
        SetSelectionState(value, selectionIndent);

    internal void SetSelectionState(
        bool value,
        float neighbourIndent,
        bool animated = true)
    {
        // A virtualised row can leave the drawable tree while the input
        // manager is still dispatching HoverLost for its previous position.
        // FreeAfterUse() has already released its callbacks at that point.
        if (Entry == null)
            return;

        bool selectionChanged = selected != value;
        selected = value;
        selectionIndent = compact
            ? Math.Clamp(neighbourIndent, 0, 12)
            : 0;
        surface.FadeColour(
            SongSelectSurface.Ivory(0.98f),
            140,
            Easing.OutQuint);
        selectedSurface.FadeTo(selected ? 1 : 0, 140, Easing.OutQuint);
        foreach ((SpriteText text, Color4 normal, Color4 selectedColour)
                 in adaptiveTexts)
        {
            text.FadeColour(
                selected ? selectedColour : normal,
                140,
                Easing.OutQuint);
        }
        selectionOutline.FadeTo(selected ? 1 : 0, 140, Easing.OutQuint);
        updateSelectionSignalRail(selectionChanged, animated);
        focusShadow.FadeTo(selected ? 1 : 0, 170, Easing.OutQuint);
        arrow.FadeTo(selected && compact ? 1 : 0, 120, Easing.OutQuint);
        updateSelectedSticker(animated && selectionChanged, animated);
        if (compact)
            compactModePill.SetExpanded(selected, animated);
        updatePatternSummaryVisibility(animated);
        float targetX = selectionTargetX();
        if (animated)
        {
            this.MoveToX(targetX, 170, Easing.OutQuint);
            this.ResizeWidthTo(
                RowWidth - targetX,
                170,
                Easing.OutQuint);
        }
        else
        {
            X = targetX;
            Width = RowWidth - targetX;
        }
    }

    internal void SetPatternProfile(
        ManiaPatternProfile profile,
        bool animated = true)
    {
        if (patternSummary == null)
            return;

        patternSummary.Text = formatPatternSummary(profile);
        updatePatternSummaryVisibility(animated);
    }

    private void updatePatternSummaryVisibility(bool animated)
    {
        if (patternSummary == null)
            return;

        bool visible = selected
                       && patternSummary.Text.ToString().Length > 0;
        if (compact)
        {
            float targetMapperWidth = visible ? 290 : 540;
            if (animated)
                compactSecondaryText.ResizeWidthTo(targetMapperWidth, 140);
            else
                compactSecondaryText.Width = targetMapperWidth;
        }

        if (animated)
            patternSummary.FadeTo(visible ? 0.88f : 0, 120, Easing.OutQuint);
        else
            patternSummary.Alpha = visible ? 0.88f : 0;
    }

    private static string formatPatternSummary(ManiaPatternProfile profile)
    {
        if (profile == null)
            return string.Empty;

        (string Label, double Value)[] strengths =
        [
            ("JACK", profile.Jack),
            ("CHORD", profile.Chord),
            ("BURST", profile.Burst),
            ("ANCHOR", profile.Anchor),
            ("LN", profile.LongNote),
            ("RELEASE", profile.Release),
        ];
        (string Label, double Value)[] strongest = strengths
            .Where(item => item.Value > 0.5)
            .OrderByDescending(item => item.Value)
            .Take(2)
            .ToArray();
        return strongest.Length == 0
            ? string.Empty
            : "PATTERN  " + string.Join(
                "  ·  ",
                strongest.Select(item => $"{item.Label} {item.Value:0}"));
    }

    private void updateSelectionSignalRail(
        bool selectionChanged,
        bool animated)
    {
        selectionSignalRail.ClearTransforms();
        float targetWidth = Math.Max(
            0,
            RowWidth - selectionTargetX() - (compact ? 18 : 22));
        if (!selected)
        {
            if (animated)
            {
                selectionSignalRail.FadeOut(110, Easing.OutQuint)
                                   .ResizeWidthTo(0, 150, Easing.OutQuint);
            }
            else
            {
                selectionSignalRail.Alpha = 0;
                selectionSignalRail.Width = 0;
            }
            return;
        }

        if (animated && selectionChanged)
        {
            selectionSignalRail.Width = 0;
            selectionSignalRail.Alpha = 0.92f;
            selectionSignalRail.ResizeWidthTo(
                                   targetWidth,
                                   250,
                                   Easing.OutQuint)
                               .FadeTo(0.42f, 420, Easing.OutQuint);
        }
        else
        {
            selectionSignalRail.Width = targetWidth;
            selectionSignalRail.Alpha = 0.42f;
        }
    }

    private void updateSelectedSticker(bool playIntro, bool animated)
    {
        selectedSticker.ClearTransforms();
        if (!selected)
        {
            if (animated)
            {
                selectedSticker.FadeOut(100, Easing.OutQuint)
                               .ScaleTo(0.82f, 120, Easing.OutQuint)
                               .RotateTo(-8, 120, Easing.OutQuint);
            }
            else
            {
                selectedSticker.Alpha = 0;
                selectedSticker.Scale = new Vector2(0.82f);
                selectedSticker.Rotation = -8;
            }
            return;
        }

        float targetAlpha = compact ? 0.92f : 0.78f;
        double introDuration = playIntro ? 230 : 0;
        if (playIntro)
        {
            selectedSticker.Alpha = 0;
            selectedSticker.Scale = new Vector2(0.64f);
            selectedSticker.Rotation = -14;
            selectedSticker.FadeTo(
                targetAlpha,
                150,
                Easing.OutQuint);
        }
        else
        {
            selectedSticker.Alpha = targetAlpha;
            selectedSticker.Scale = Vector2.One;
            selectedSticker.Rotation = 3;
        }

        selectedSticker.ScaleTo(1, introDuration, Easing.OutBack)
                       .Then()
                       .ScaleTo(1.08f, 920, Easing.InOutSine)
                       .Then()
                       .ScaleTo(1, 920, Easing.InOutSine)
                       .Loop(320);
        selectedSticker.RotateTo(3, introDuration, Easing.OutBack)
                       .Then()
                       .RotateTo(6, 1180, Easing.InOutSine)
                       .Then()
                       .RotateTo(1, 1180, Easing.InOutSine)
                       .Loop(420);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (Entry == null)
            return false;

        surface.FadeColour(
            compact
                ? new Color4(0.95f, 0.99f, 1f, 0.99f)
                : SongSelectTheme.PaleCyan,
            100);
        focusShadow.FadeTo(selected ? 1 : 0.42f, 120, Easing.OutQuint);
        float targetX = selectionTargetX() - 3;
        this.MoveToX(targetX, 120, Easing.OutQuint);
        this.ResizeWidthTo(
            RowWidth - targetX,
            120,
            Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        SetSelected(selected);

    private float selectionTargetX() =>
        restingX
        + selectionIndent
        // Matches lazer Panel.active_x_offset: the active carousel item
        // projects towards the information side instead of only changing fill.
        - (selected ? 25 : 0);

    protected override bool OnClick(ClickEvent e)
    {
        Action?.Invoke();
        return true;
    }

    protected override bool OnDoubleClick(DoubleClickEvent e)
    {
        DoubleClickAction?.Invoke();
        return true;
    }

    protected override void FreeAfterUse()
    {
        Action = null;
        DoubleClickAction = null;
        Entry = null;
        patternSummary = null;
        // Release per-entry texture references so pooled rows do not keep
        // decoded artwork alive while resting in the pool.
        artworkSprite.Texture = null;
        selectedSticker.Texture = null;
        base.FreeAfterUse();
    }

    private void bindCompactContent(
        SongSelectEntry entry,
        ManiaDifficultyRatingMode difficultyRatingMode,
        string primaryText)
    {
        compactTickVertical.Colour = withAlpha(accent, 0.18f);
        compactTickHorizontal.Colour = withAlpha(accent, 0.22f);
        accentBoxes.Add((compactTickVertical, 0.18f));
        accentBoxes.Add((compactTickHorizontal, 0.22f));
        compactDifficultyUnit.Text = ManiaDifficultyPresentation.Unit(
            difficultyRatingMode);
        compactDifficultyUnit.Colour = accent;
        compactDifficultyValue.Text = ManiaDifficultyPresentation.FormatValue(
            displayedDifficultyRatings,
            difficultyRatingMode);
        difficultyUnitTexts.Add(compactDifficultyUnit);
        difficultyValueTexts.Add(compactDifficultyValue);
        bindAdaptiveText(
            compactPrimaryText,
            primaryText ?? entry.Beatmap.DifficultyName,
            SongSelectTheme.Navy,
            SongSelectTheme.Navy);
        bindAdaptiveText(
            compactSecondaryText,
            $"mapped by {entry.Beatmap.Creator}",
            withAlpha(SongSelectTheme.Navy, 0.82f),
            withAlpha(SongSelectTheme.Navy, 0.84f));
        compactSecondaryText.Width = 540;
        compactPatternSummary.Text = string.Empty;
        compactPatternSummary.Alpha = 0;
        patternSummary = compactPatternSummary;
        compactModePill.Bind(entry);
    }

    private void bindStandaloneContent(
        SongSelectEntry entry,
        Texture wallpaper,
        ManiaDifficultyRatingMode difficultyRatingMode)
    {
        artworkSprite.Texture = wallpaper;
        artworkSprite.Size = SongSelectArtworkCrop.CalculateFitSize(
            wallpaper == null
                ? StandaloneArtworkSize
                : new Vector2(
                    wallpaper.DisplayWidth,
                    wallpaper.DisplayHeight),
            StandaloneArtworkSize);
        artworkSprite.Alpha = 1;
        bindAdaptiveText(
            standaloneTitle,
            entry.Beatmap.Title,
            SongSelectTheme.Navy,
            SongSelectTheme.Navy);
        bindAdaptiveText(
            standaloneArtist,
            entry.Beatmap.Artist,
            withAlpha(SongSelectTheme.Navy, 0.72f),
            withAlpha(SongSelectTheme.Navy, 0.72f));
        bindAdaptiveText(
            standaloneCreator,
            $"mapped by {entry.Beatmap.Creator}",
            SongSelectTheme.Cyan,
            SongSelectTheme.Cyan);
        standalonePatternSummary.Text = string.Empty;
        standalonePatternSummary.Alpha = 0;
        patternSummary = standalonePatternSummary;
        standaloneModeText.Text = $"{(int)entry.Beatmap.KeyMode}K · "
                                  + entry.Beatmap.DifficultyName;
        standaloneRatingUnit.Text = ManiaDifficultyPresentation.Unit(
            difficultyRatingMode);
        standaloneRatingValue.Text = ManiaDifficultyPresentation.FormatValue(
            displayedDifficultyRatings,
            difficultyRatingMode);
        difficultyUnitTexts.Add(standaloneRatingUnit);
        difficultyValueTexts.Add(standaloneRatingValue);
        bindAdaptiveText(standaloneRatingUnit, null, accent, accent);
        bindAdaptiveText(
            standaloneRatingValue,
            null,
            SongSelectTheme.Ivory,
            SongSelectTheme.Navy);
    }

    /// <summary>
    /// Resets an adaptive text to its resting colour and registers it for the
    /// selection colour fade. A null value keeps the text set by the caller.
    /// </summary>
    private void bindAdaptiveText(
        SpriteText text,
        string value,
        Color4 normal,
        Color4 selectedColour)
    {
        if (value != null)
            text.Text = value;
        text.Colour = normal;
        adaptiveTexts.Add((text, normal, selectedColour));
    }

    private static Color4 withAlpha(Color4 colour, float alpha) => new(
        colour.R,
        colour.G,
        colour.B,
        alpha);

    internal static Color4 DifficultyColour(
        ManiaDifficultyRatings ratings,
        ManiaDifficultyRatingMode mode)
    {
        double value = ratings.Value(mode) ?? 0;
        if (!ratings.IsSuccess(mode))
            return SongSelectTheme.Muted;
        double[] thresholds = mode
            == ManiaDifficultyRatingMode.EtternaMsd
                ? [5, 10, 15, 20]
                : [2.2, 3.7, 5.2, 6.6];
        if (value < thresholds[0])
            return new Color4(0.42f, 0.72f, 0.86f, 1f);
        if (value < thresholds[1])
            return SongSelectTheme.Cyan;
        if (value < thresholds[2])
            return SongSelectTheme.Yellow;
        if (value < thresholds[3])
            return SongSelectTheme.Pink;
        return new Color4(0.64f, 0.47f, 1f, 1f);
    }

}

internal partial class SongSelectProgressiveModePill : CompositeDrawable
{
    private const float compact_width = 58;
    private const float expanded_width = 126;
    private const float right_edge = 930;

    private readonly Box surface;
    private readonly SpriteText compactText;
    private readonly SpriteText expandedText;

    internal float CompactTextAlpha => compactText.Alpha;
    internal float ExpandedTextAlpha => expandedText.Alpha;

    internal SongSelectProgressiveModePill(float y)
    {
        Position = new Vector2(right_edge - compact_width, y);
        Size = new Vector2(compact_width, 30);
        Masking = true;
        CornerRadius = 7;
        InternalChildren =
        [
            surface = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = quietSurfaceColour(),
            },
            compactText = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = 48,
                Truncate = true,
                Font = HomeTypography.Display(13),
                Colour = SongSelectTheme.Pink,
            },
            expandedText = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = 112,
                Truncate = true,
                Font = HomeTypography.Display(12),
                Colour = Color4.White,
                Alpha = 0,
            },
        ];
    }

    internal void Bind(SongSelectEntry entry)
    {
        compactText.Text = $"{(int)entry.Beatmap.KeyMode}K";
        expandedText.Text = $"{(int)entry.Beatmap.KeyMode}K · SELECTED";
        SetExpanded(false, false);
    }

    internal void SetExpanded(bool expanded, bool animated)
    {
        float targetWidth = expanded ? expanded_width : compact_width;
        float targetX = right_edge - targetWidth;
        Color4 targetColour = expanded
            ? expandedSurfaceColour()
            : quietSurfaceColour();

        if (animated)
        {
            this.MoveToX(targetX, 170, Easing.OutQuint);
            this.ResizeWidthTo(targetWidth, 170, Easing.OutQuint);
            surface.FadeColour(targetColour, 150, Easing.OutQuint);
            compactText.FadeTo(expanded ? 0 : 1, 90, Easing.OutQuint);
            expandedText.FadeTo(expanded ? 1 : 0, 130, Easing.OutQuint);
        }
        else
        {
            X = targetX;
            Width = targetWidth;
            surface.Colour = targetColour;
            compactText.Alpha = expanded ? 0 : 1;
            expandedText.Alpha = expanded ? 1 : 0;
        }
    }

    private static Color4 quietSurfaceColour() => new(
        SongSelectTheme.Pink.R,
        SongSelectTheme.Pink.G,
        SongSelectTheme.Pink.B,
        0.16f);

    private static Color4 expandedSurfaceColour() => new(
        SongSelectTheme.Pink.R,
        SongSelectTheme.Pink.G,
        SongSelectTheme.Pink.B,
        0.86f);
}

internal partial class SongSelectInlineDifficultyRating : CompositeDrawable
{
    internal SpriteText UnitText { get; }
    internal SpriteText ValueText { get; }

    internal SongSelectInlineDifficultyRating(
        SpriteText unitText,
        SpriteText valueText)
    {
        UnitText = unitText;
        ValueText = valueText;
        Size = new Vector2(112, 22);
        UnitText.Width = 42;
        UnitText.Truncate = true;
        UnitText.Position = new Vector2(0, 6);
        ValueText.Anchor = Anchor.TopRight;
        ValueText.Origin = Anchor.TopRight;
        ValueText.Width = 66;
        ValueText.Truncate = true;
        ValueText.Y = 2;
        InternalChildren =
        [
            UnitText,
            ValueText,
        ];
    }
}

/// <summary>
/// Pooled package header following the same build-once carousel panel model
/// as <see cref="SongSelectSongRow"/> (ppy/osu
/// osu.Game/Screens/SelectV2/PanelBeatmapSet.cs at commit
/// 83b8a64bec19e1463353645c2d6d10c75e275b43): <see cref="Bind"/> retunes the
/// fixed drawable tree for the collapsed/expanded layout instead of
/// rebuilding it.
/// </summary>
internal partial class SongSelectPackageHeader : PoolableDrawable
{
    internal const float ExpandedHeight = 132;
    internal const float CollapsedHeight = 112;

    private const float artwork_width = 228;
    private const float content_start = 244;
    private const float action_safe_right = 926;
    private const int maximum_title_lines = 2;

    private Action toggle;
    private readonly Box stateBackground;
    private readonly Box selectedRail;
    private readonly Box childGuideStem;
    private readonly Box expandedRail;
    private readonly Container hoverLayer;
    private readonly Box hoverSurface;
    private readonly Box chevronSurface;
    private readonly SpriteIcon chevronIcon;
    private readonly SpriteIcon favouriteIcon;
    private readonly SpriteIcon selectedIndicator;
    private readonly Container artworkFrame;
    private readonly Container artworkImageFrame;
    private readonly Sprite artworkSprite;
    private Vector2 artworkSpriteFrameSize;
    private readonly Container chevronFrame;
    private readonly Container packageSummaryLayer;
    private readonly SpriteText collectionLabel;
    private readonly Box collectionDivider;
    private readonly FillFlowContainer packageTitleFlow;
    private readonly SpriteText[] packageTitleTexts;
    private int packageTitleLineCount;
    private readonly SpriteText countsLabel;
    private readonly Container selectedSummaryLayer;
    private readonly Container selectedModePill;
    private readonly SpriteText selectedTitle;
    private readonly SpriteText selectedByline;
    private readonly SpriteText selectedModeText;
    private readonly SpriteText selectedRatingUnit;
    private readonly SpriteText selectedRatingValue;
    private bool expanded;
    private bool selectedState;

    internal float ChildGuideStemAlpha => childGuideStem.Alpha;
    internal float SelectedRailHeight => selectedRail.Height;
    internal float SelectedIndicatorAlpha => selectedIndicator.Alpha;
    internal Vector2 ArtworkFrameSize => artworkFrame.Size;
    internal Vector2 ArtworkImageFrameSize => artworkImageFrame.Size;
    internal float ArtworkImageCornerRadius => artworkImageFrame.CornerRadius;
    internal float ExpandedRailAlpha => expandedRail.Alpha;
    internal float ChevronSurfaceAlpha => chevronSurface.Alpha;
    internal float PackageSummaryAlpha => packageSummaryLayer.Alpha;
    internal float SelectedSummaryAlpha => selectedSummaryLayer.Alpha;
    internal string SelectedContextTitle => selectedTitle.Text.ToString();
    internal string SelectedContextByline => selectedByline.Text.ToString();
    internal string SelectedContextMode => selectedModeText.Text.ToString();
    internal string SelectedContextRating =>
        selectedRatingValue.Text.ToString();
    internal Vector2 SelectedModePillPosition => selectedModePill.Position;
    internal Vector2 SelectedModePillSize => selectedModePill.Size;
    internal Vector2 SelectedRatingPosition => selectedRatingValue.Position;
    internal int PackageTitleLineCount => packageTitleLineCount;
    internal bool PackageTitleUsesTruncation =>
        packageTitleLineCount > 0
        && packageTitleTexts.Take(packageTitleLineCount)
                            .All(text => text.Truncate);
    internal bool IsExpanded => expanded;
    internal float PackageContentStart { get; private set; }
    internal Anchor FavouriteIconAnchor => favouriteIcon.Anchor;
    internal Vector2 FavouriteIconPosition => favouriteIcon.Position;
    internal Anchor ChevronFrameAnchor => chevronFrame.Anchor;
    internal Vector2 ChevronFramePosition => chevronFrame.Position;

    public SongSelectPackageHeader()
    {
        Size = new Vector2(SongSelectSongRow.RowWidth, ExpandedHeight);
        Masking = true;
        CornerRadius = 10;
        BorderThickness = 1;
        BorderColour = new Color4(
            SongSelectTheme.Cyan.R,
            SongSelectTheme.Cyan.G,
            SongSelectTheme.Cyan.B,
            0.18f);

        packageTitleTexts = new SpriteText[maximum_title_lines];
        for (int i = 0; i < packageTitleTexts.Length; i++)
        {
            packageTitleTexts[i] = new SpriteText
            {
                Truncate = true,
                Colour = SongSelectTheme.Navy,
                Alpha = 0,
            };
        }

        InternalChildren =
        [
            stateBackground = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SongSelectSurface.Ivory(0.98f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = ColourInfo.GradientHorizontal(
                    Color4.Transparent,
                    new Color4(
                        SongSelectTheme.Cyan.R,
                        SongSelectTheme.Cyan.G,
                        SongSelectTheme.Cyan.B,
                        0.045f)),
            },
            artworkFrame = new Container
            {
                Size = new Vector2(artwork_width, ExpandedHeight),
                Child = artworkImageFrame = new Container
                {
                    Position = new Vector2(4),
                    Size = SongSelectSongRow.StandaloneArtworkSize,
                    Masking = true,
                    CornerRadius = 8,
                    BorderThickness = 1,
                    BorderColour = new Color4(1f, 1f, 1f, 0.58f),
                    Children =
                    [
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = SongSelectTheme.DeepNavy,
                        },
                        artworkSprite = SongSelectArtworkCrop.CreateFit(
                            null,
                            SongSelectSongRow.StandaloneArtworkSize),
                    ],
                },
            },
            hoverLayer = new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                RelativeSizeAxes = Axes.Y,
                Width = SongSelectSongRow.RowWidth - artwork_width,
                Children =
                [
                    hoverSurface = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SongSelectTheme.PaleCyan,
                        Alpha = 0,
                    },
                ],
            },
            childGuideStem = new Box
            {
                Width = 2,
                Height = 24,
                Colour = SongSelectTheme.Cyan,
                Alpha = 0,
            },
            favouriteIcon = new SpriteIcon
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-18, 8),
                Size = new Vector2(16),
                Icon = FontAwesome.Solid.LayerGroup,
                Colour = SongSelectTheme.Cyan,
                Alpha = 0.56f,
            },
            selectedIndicator = new SpriteIcon
            {
                Size = new Vector2(11),
                Icon = FontAwesome.Solid.Play,
                Colour = SongSelectTheme.Pink,
                Alpha = 0,
                Scale = new Vector2(0.72f),
            },
            packageSummaryLayer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children =
                [
                    collectionLabel = new SpriteText
                    {
                        Text = "CHART COLLECTION",
                        Font = HomeTypography.Display(14),
                        Spacing = new Vector2(0.8f, 0),
                        Colour = new Color4(
                            SongSelectTheme.Navy.R,
                            SongSelectTheme.Navy.G,
                            SongSelectTheme.Navy.B,
                            0.86f),
                    },
                    collectionDivider = new Box
                    {
                        Size = new Vector2(34, 2),
                        Colour = SongSelectTheme.Pink,
                        Alpha = 0.72f,
                    },
                    packageTitleFlow = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, -1),
                        Children = packageTitleTexts,
                    },
                    countsLabel = label(
                        string.Empty,
                        content_start,
                        98,
                        action_safe_right - content_start,
                        15,
                        new Color4(
                            SongSelectTheme.Navy.R,
                            SongSelectTheme.Navy.G,
                            SongSelectTheme.Navy.B,
                            0.88f)),
                ],
            },
            selectedSummaryLayer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                Children =
                [
                    selectedTitle = label(
                        string.Empty,
                        content_start,
                        16,
                        574,
                        25,
                        SongSelectTheme.Navy),
                    selectedByline = label(
                        string.Empty,
                        content_start,
                        49,
                        490,
                        16,
                        new Color4(
                            SongSelectTheme.Navy.R,
                            SongSelectTheme.Navy.G,
                            SongSelectTheme.Navy.B,
                            0.82f)),
                    selectedModePill = new Container
                    {
                        Position = new Vector2(content_start, 98),
                        Size = new Vector2(276, 30),
                        Masking = true,
                        CornerRadius = 7,
                        BorderThickness = 1,
                        BorderColour = new Color4(
                            SongSelectTheme.Pink.R,
                            SongSelectTheme.Pink.G,
                            SongSelectTheme.Pink.B,
                            0.24f),
                        Children =
                        [
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = new Color4(
                                    SongSelectTheme.PaleCyan.R,
                                    SongSelectTheme.PaleCyan.G,
                                    SongSelectTheme.PaleCyan.B,
                                    0.82f),
                            },
                            selectedModeText = new SpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Width = 260,
                                Truncate = true,
                                Font = HomeTypography.Display(16),
                                Colour = SongSelectTheme.Pink,
                            },
                        ],
                    },
                    selectedRatingUnit = label(
                        string.Empty,
                        648,
                        106,
                        44,
                        16,
                        SongSelectTheme.Cyan),
                    selectedRatingValue = new SpriteText
                    {
                        Origin = Anchor.TopRight,
                        Position = new Vector2(766, 100),
                        Width = 68,
                        Truncate = true,
                        Font = HomeTypography.Display(21),
                        Colour = SongSelectTheme.Navy,
                    },
                ],
            },
            chevronFrame = new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-11, ExpandedHeight - 36),
                Size = new Vector2(30),
                Masking = true,
                CornerRadius = 7,
                Children =
                [
                    chevronSurface = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SongSelectTheme.PaleCyan,
                        Alpha = 0.52f,
                    },
                    chevronIcon = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(12),
                        Icon = FontAwesome.Solid.ChevronDown,
                        Colour = SongSelectTheme.Cyan,
                    },
                ],
            },
            expandedRail = new Box
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Width = SongSelectSongRow.RowWidth - artwork_width,
                Height = 2,
                Colour = SongSelectTheme.Cyan,
                Alpha = 0,
            },
            selectedRail = new Box
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Width = 0,
                Height = 0,
                Colour = SongSelectTheme.Cyan,
            },
        ];
    }

    public void Bind(
        string packageName,
        int songCount,
        int chartCount,
        bool collapsed,
        Texture wallpaper,
        bool selected,
        Action toggleAction)
    {
        ClearTransforms(true);
        Alpha = 1;
        toggle = toggleAction;
        expanded = !collapsed;
        selectedState = selected;
        float headerHeight = collapsed ? CollapsedHeight : ExpandedHeight;
        float artworkWidth = collapsed ? 193 : artwork_width;
        Vector2 artworkSize = collapsed
            ? new Vector2(185, headerHeight - 8)
            : SongSelectSongRow.StandaloneArtworkSize;
        float contentStart = collapsed ? 209 : content_start;
        PackageContentStart = contentStart;
        artworkSpriteFrameSize = artworkSize;
        Size = new Vector2(SongSelectSongRow.RowWidth, headerHeight);
        BorderThickness = selected ? 1.5f : 1;
        BorderColour = selected
            ? SongSelectTheme.Cyan
            : new Color4(
                SongSelectTheme.Cyan.R,
                SongSelectTheme.Cyan.G,
                SongSelectTheme.Cyan.B,
                0.18f);

        stateBackground.Colour = selected
            ? SongSelectSurface.Ivory(0.995f)
            : SongSelectSurface.Ivory(0.98f);
        artworkFrame.Size = new Vector2(artworkWidth, headerHeight);
        artworkImageFrame.Size = artworkSize;
        artworkSprite.Texture = wallpaper;
        artworkSprite.Size = SongSelectArtworkCrop.CalculateFitSize(
            wallpaper == null
                ? artworkSize
                : new Vector2(
                    wallpaper.DisplayWidth,
                    wallpaper.DisplayHeight),
            artworkSize);
        artworkSprite.Alpha = 1;
        hoverLayer.Width = SongSelectSongRow.RowWidth - artworkWidth;
        hoverSurface.Alpha = 0;
        childGuideStem.Position = new Vector2(5, headerHeight - 24);
        childGuideStem.Alpha = expanded
            ? selected ? 0.92f : 0.34f
            : 0;
        selectedIndicator.Position = new Vector2(
            selected
                ? artworkWidth + 8
                : artworkWidth + 4,
            headerHeight - 27);
        selectedIndicator.Alpha = selected ? 1 : 0;
        selectedIndicator.Scale = selected ? Vector2.One : new Vector2(0.72f);
        bindPackageSummary(
            packageName,
            songCount,
            chartCount,
            collapsed,
            contentStart);
        bindSelectedSummary(contentStart);
        chevronFrame.Position = new Vector2(-11, headerHeight - 36);
        chevronFrame.Scale = Vector2.One;
        chevronSurface.Alpha = expanded ? 0.78f : 0.52f;
        chevronIcon.Rotation = collapsed ? -90 : 0;
        expandedRail.Width = SongSelectSongRow.RowWidth - artworkWidth;
        expandedRail.Alpha = expanded ? 0.32f : 0;
        selectedRail.Width = selected
            ? SongSelectSongRow.RowWidth - artworkWidth
            : 0;
        selectedRail.Height = selected ? 3 : 0;
    }

    /// <summary>
    /// Swaps the placeholder for asynchronously decoded artwork with a fade.
    /// </summary>
    internal void SetArtwork(Texture texture)
    {
        if (texture == null)
            return;

        artworkSprite.Texture = texture;
        artworkSprite.Size = SongSelectArtworkCrop.CalculateFitSize(
            new Vector2(texture.DisplayWidth, texture.DisplayHeight),
            artworkSpriteFrameSize);
        artworkSprite.FadeInFromZero(180, Easing.OutQuint);
    }

    public void SetSelected(bool selected) => SetSelected(
        selected,
        null,
        null,
        ManiaDifficultyRatingMode.EtternaMsd);

    internal void SetSelected(
        bool selected,
        SongSelectEntry context,
        ManiaDifficultyRatings ratings,
        ManiaDifficultyRatingMode mode,
        bool animated = true)
    {
        bool selectionChanged = selectedState != selected;
        selectedState = selected;
        bool showContext = expanded
                           && selected
                           && context != null
                           && ratings != null;
        if (showContext)
            updateSelectedContext(context, ratings, mode);

        BorderThickness = selected ? 1.5f : 1;
        BorderColour = selected
            ? SongSelectTheme.Cyan
            : new Color4(
                SongSelectTheme.Cyan.R,
                SongSelectTheme.Cyan.G,
                SongSelectTheme.Cyan.B,
                0.18f);

        packageSummaryLayer.ClearTransforms();
        selectedSummaryLayer.ClearTransforms();
        if (animated)
        {
            packageSummaryLayer.FadeTo(
                showContext ? 0 : 1,
                100,
                Easing.OutQuint);
            selectedSummaryLayer.FadeTo(
                showContext ? 1 : 0,
                140,
                Easing.OutQuint);
        }
        else
        {
            packageSummaryLayer.Alpha = showContext ? 0 : 1;
            selectedSummaryLayer.Alpha = showContext ? 1 : 0;
        }

        Color4 backgroundTarget = selected
            ? SongSelectSurface.Ivory(0.995f)
            : SongSelectSurface.Ivory(0.98f);
        if (animated)
            stateBackground.FadeColour(backgroundTarget, 140, Easing.OutQuint);
        else
            stateBackground.Colour = backgroundTarget;

        if (animated)
        {
            selectedIndicator.ClearTransforms();
            if (selected && selectionChanged)
            {
                selectedIndicator.Scale = new Vector2(0.68f);
                selectedIndicator.ScaleTo(1.24f, 130, Easing.OutBack)
                                 .Then()
                                 .ScaleTo(1, 140, Easing.OutQuint);
            }
            else if (!selected)
            {
                selectedIndicator.ScaleTo(
                    0.72f,
                    100,
                    Easing.OutQuint);
            }
            selectedIndicator.FadeTo(
                selected ? 1 : 0,
                120,
                Easing.OutQuint);
            selectedIndicator.MoveToX(
                selected
                    ? artworkFrame.Width + 8
                    : artworkFrame.Width + 4,
                140,
                Easing.OutQuint);
        }
        else
        {
            selectedIndicator.Alpha = selected ? 1 : 0;
            selectedIndicator.Scale = selected
                ? Vector2.One
                : new Vector2(0.72f);
            selectedIndicator.X = selected
                ? artworkFrame.Width + 8
                : artworkFrame.Width + 4;
        }

        float targetWidth = SongSelectSongRow.RowWidth
                            - artworkFrame.Width;
        selectedRail.ClearTransforms();
        if (animated && selected && selectionChanged)
        {
            selectedRail.Width = 0;
            selectedRail.Height = 3;
            selectedRail.ResizeWidthTo(
                targetWidth,
                260,
                Easing.OutQuint);
        }
        else if (animated)
        {
            selectedRail.ResizeHeightTo(
                            selected ? 3 : 0,
                            170,
                            Easing.OutQuint)
                        .ResizeWidthTo(
                            selected ? targetWidth : 0,
                            190,
                            Easing.OutQuint);
        }
        else
        {
            selectedRail.Height = selected ? 3 : 0;
            selectedRail.Width = selected ? targetWidth : 0;
        }

        float stemTarget = expanded
            ? selected ? 0.92f : 0.34f
            : 0;
        if (animated)
            childGuideStem.FadeTo(stemTarget, 140, Easing.OutQuint);
        else
            childGuideStem.Alpha = stemTarget;
    }

    protected override bool OnClick(ClickEvent e)
    {
        toggle?.Invoke();
        return true;
    }

    protected override bool OnHover(HoverEvent e)
    {
        hoverSurface.FadeTo(0.52f, 110, Easing.OutQuint);
        chevronSurface.FadeTo(1, 110, Easing.OutQuint);
        chevronFrame.MoveToY(chevronFrame.Y - 2, 120, Easing.OutQuint)
                    .ScaleTo(1.08f, 150, Easing.OutBack);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        hoverSurface.FadeOut(130, Easing.OutQuint);
        chevronSurface.FadeTo(
            expanded ? 0.78f : 0.52f,
            130,
            Easing.OutQuint);
        float restingY = Height - 36;
        chevronFrame.MoveToY(restingY, 160, Easing.OutQuint)
                    .ScaleTo(1, 180, Easing.OutQuint);
    }

    protected override void FreeAfterUse()
    {
        toggle = null;
        // Release the per-package texture so pooled headers do not keep
        // decoded artwork alive while resting in the pool.
        artworkSprite.Texture = null;
        artworkSpriteFrameSize = Vector2.Zero;
        expanded = false;
        selectedState = false;
        PackageContentStart = 0;
        packageTitleLineCount = 0;
        base.FreeAfterUse();
    }

    private void bindPackageSummary(
        string packageName,
        int songCount,
        int chartCount,
        bool collapsed,
        float contentStart)
    {
        packageSummaryLayer.Alpha = 1;
        collectionLabel.Position = new Vector2(contentStart, 8);
        collectionDivider.Position = new Vector2(contentStart, 22);

        string[] lines = collapsed
            ? [packageName]
            : SongSelectTextLayout.TwoLines(packageName, 34);
        packageTitleLineCount = Math.Min(
            lines.Length,
            packageTitleTexts.Length);
        float titleWidth = action_safe_right - contentStart;
        packageTitleFlow.Position = new Vector2(
            contentStart,
            collapsed ? 30 : 28);
        packageTitleFlow.Width = titleWidth;
        for (int i = 0; i < packageTitleTexts.Length; i++)
        {
            SpriteText title = packageTitleTexts[i];
            if (i < packageTitleLineCount)
            {
                title.Width = titleWidth;
                title.Text = lines[i];
                title.Font = HomeTypography.Display(
                    collapsed ? 20 : packageTitleLineCount == 1 ? 22 : 19);
                title.Alpha = 1;
            }
            else
            {
                title.Text = string.Empty;
                title.Alpha = 0;
            }
        }

        countsLabel.Text = $"{songCount} SONGS · {chartCount} CHARTS";
        countsLabel.Position = new Vector2(
            contentStart,
            collapsed ? 70 : 98);
        countsLabel.Width = action_safe_right - contentStart;
    }

    private void bindSelectedSummary(float contentStart)
    {
        selectedSummaryLayer.Alpha = 0;
        selectedTitle.Position = new Vector2(contentStart, 16);
        selectedTitle.Text = string.Empty;
        selectedByline.Position = new Vector2(contentStart, 49);
        selectedByline.Text = string.Empty;
        selectedModePill.Position = new Vector2(contentStart, 98);
        selectedModeText.Text = string.Empty;
        selectedRatingUnit.Text = string.Empty;
        selectedRatingValue.Text = string.Empty;
    }

    private void updateSelectedContext(
        SongSelectEntry entry,
        ManiaDifficultyRatings ratings,
        ManiaDifficultyRatingMode mode)
    {
        selectedTitle.Text = entry.Beatmap.Title;
        selectedByline.Text = $"{entry.Beatmap.Artist} · mapped by "
                              + entry.Beatmap.Creator;
        selectedModeText.Text = $"{(int)entry.Beatmap.KeyMode}K · "
                                + entry.Beatmap.DifficultyName;
        selectedRatingUnit.Text = ManiaDifficultyPresentation.Unit(mode);
        selectedRatingUnit.Colour =
            SongSelectSongRow.DifficultyColour(ratings, mode);
        selectedRatingValue.Text = ManiaDifficultyPresentation.FormatValue(
            ratings,
            mode);
    }

    private static SpriteText label(
        string value,
        float x,
        float y,
        float width,
        float size,
        Color4 colour) => new()
        {
            Position = new Vector2(x, y),
            Width = width,
            Truncate = true,
            Text = value,
            Font = HomeTypography.Display(size),
            Colour = colour,
        };
}
