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
using Yokko.Core.Difficulty;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectSongRow : PoolableDrawable
{
    private const float row_width = 850;
    internal const float CompactHeight = 44;
    internal const float CompactLeadingAccentWidth = 3;
    internal const float CompactLeadingAccentOpacity = 0.48f;
    internal const float CompactSelectionOutlineThickness = 1.25f;
    internal const float CompactSelectedFillOpacity = 0.18f;

    private Box surface;
    private Box selectedSurface;
    private Box leadingAccent;
    private Drawable focusShadow;
    private Container selectionOutline;
    private SpriteIcon arrow;
    private Sprite selectedSticker;
    private Container standaloneArtworkFrame;
    private SongSelectProgressiveModePill compactModePill;
    private SpriteText compactPrimaryText;
    private SpriteText compactSecondaryText;
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
    internal float FocusShadowAlpha => focusShadow?.Alpha ?? 0;
    internal float SelectionIndent => selectionIndent;
    internal float ModePillWidth => compactModePill?.Width ?? 0;
    internal float ModePillX => compactModePill?.X ?? 0;
    internal float CompactModeTextAlpha =>
        compactModePill?.CompactTextAlpha ?? 0;
    internal float ExpandedModeTextAlpha =>
        compactModePill?.ExpandedTextAlpha ?? 0;
    internal string CompactPrimaryText =>
        compactPrimaryText?.Text.ToString() ?? string.Empty;
    internal string CompactSecondaryText =>
        compactSecondaryText?.Text.ToString() ?? string.Empty;
    internal Vector2 StandaloneArtworkFrameSize =>
        standaloneArtworkFrame?.Size ?? Vector2.Zero;
    internal float LeadingAccentWidth => leadingAccent?.Width ?? 0;
    internal float SelectionOutlineThickness =>
        selectionOutline?.BorderThickness ?? 0;

    public SongSelectSongRow()
    {
    }

    public SongSelectSongRow(
        SongSelectEntry entry,
        ManiaDifficultyRatings ratings,
        ManiaDifficultyRatingMode difficultyRatingMode,
        Texture wallpaper,
        Texture selectedStickerTexture,
        Action select,
        Action play)
    {
        Bind(
            entry,
            ratings,
            difficultyRatingMode,
            wallpaper,
            selectedStickerTexture,
            null,
            select,
            play);
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
        ClearTransforms();
        ClearInternal(true);
        Alpha = 1;
        accentBoxes.Clear();
        accentBorders.Clear();
        difficultyValueTexts.Clear();
        difficultyUnitTexts.Clear();
        adaptiveTexts.Clear();
        compactModePill = null;
        compactPrimaryText = null;
        compactSecondaryText = null;
        standaloneArtworkFrame = null;
        leadingAccent = null;
        Entry = entry;
        Action = select;
        DoubleClickAction = play;
        compact = entry.IsPackage;
        float rowHeight = compact ? CompactHeight : 84;
        restingX = compact ? 14 : 0;
        selectionIndent = 0;
        ArgumentNullException.ThrowIfNull(ratings);
        displayedDifficultyRatings = ratings;
        accent = DifficultyColour(
            ratings,
            difficultyRatingMode);
        X = restingX;
        Size = new Vector2(row_width - restingX, rowHeight);

        float panelBorderAlpha = compact ? 0.12f : 0.38f;
        Color4 panelBorder = compact
            ? SongSelectSurface.Border(panelBorderAlpha)
            : new Color4(
                accent.R,
                accent.G,
                accent.B,
                panelBorderAlpha);
        Container panel = SongSelectSurface.CreateCard(
            out surface,
            SongSelectSurface.Ivory(0.98f),
            panelBorder,
            9,
            1);
        if (!compact)
            accentBorders.Add((panel, panelBorderAlpha));
        selectedSurface = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = compact
                ? new Color4(
                    1f,
                    0.96f,
                    0.66f,
                    CompactSelectedFillOpacity)
                : new Color4(1f, 0.98f, 0.78f, 1f),
            Alpha = 0,
        };
        focusShadow = SongSelectSurface.CreateShadow(
            9,
            compact ? 0.12f : 0.27f,
            compact ? 2 : 4);
        focusShadow.Alpha = 0;

        leadingAccent = addAccent(new Box
        {
            RelativeSizeAxes = Axes.Y,
            Width = compact ? CompactLeadingAccentWidth : 7,
            Colour = compact
                ? new Color4(
                    accent.R,
                    accent.G,
                    accent.B,
                    CompactLeadingAccentOpacity)
                : accent,
        }, compact ? CompactLeadingAccentOpacity : 1);

        var children = new System.Collections.Generic.List<Drawable>
        {
            SongSelectSurface.CreateShadow(
                9,
                compact ? 0.04f : 0.22f,
                compact ? 1 : 2),
            focusShadow,
            panel,
            leadingAccent,
            addAccent(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    accent.R,
                    accent.G,
                    accent.B,
                    compact ? 0.008f : 0.06f),
            }, compact ? 0.008f : 0.06f),
            // Keep the selected surface genuinely ivory. Placing it after the
            // accent wash avoids the muddy blue tint seen in the first pass.
            selectedSurface,
        };

        children.Add(selectionOutline = new Container
        {
            Position = compact ? new Vector2(1) : new Vector2(2),
            Size = compact
                ? new Vector2(row_width - restingX - 2, rowHeight - 2)
                : new Vector2(row_width - restingX - 4, rowHeight - 4),
            Masking = true,
            CornerRadius = 8,
            BorderThickness = compact
                ? CompactSelectionOutlineThickness
                : 2,
            BorderColour = compact
                ? SongSelectTheme.Yellow
                : SongSelectTheme.Cyan,
            Alpha = 0,
            Child = new Box
            {
                Alpha = 0,
            },
        });
        children.Add(arrow = new SpriteIcon
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            X = compact ? -9 : 8,
            Size = new Vector2(12),
            Icon = FontAwesome.Solid.Play,
            Colour = SongSelectTheme.Pink,
            Alpha = 0,
        });
        children.Add(selectedSticker = new Sprite
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.Centre,
            Position = new Vector2(-8, 2),
            Size = new Vector2(30),
            Texture = selectedStickerTexture,
            FillMode = FillMode.Fit,
            Alpha = 0,
        });

        if (compact)
        {
            children.Add(addAccent(new Box
            {
                Position = new Vector2(-7, -5),
                Width = 1,
                Height = rowHeight + 5,
                Colour = new Color4(
                    accent.R,
                    accent.G,
                    accent.B,
                    0.18f),
            }, 0.18f));
            children.Add(addAccent(new Box
            {
                Position = new Vector2(-7, rowHeight / 2),
                Width = 7,
                Height = 1,
                Colour = new Color4(
                    accent.R,
                    accent.G,
                    accent.B,
                    0.22f),
            }, 0.22f));
            addCompactContent(
                children,
                entry,
                difficultyRatingMode,
                compactPrimaryTextOverride);
        }
        else
        {
            addStandaloneContent(
                children,
                entry,
                wallpaper,
                difficultyRatingMode);
        }

        InternalChildren = children;
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
        {
            box.Colour = new Color4(
                accent.R,
                accent.G,
                accent.B,
                alpha);
        }
        foreach ((Container border, float alpha) in accentBorders)
        {
            border.BorderColour = new Color4(
                accent.R,
                accent.G,
                accent.B,
                alpha);
        }
    }

    public void SetSelected(bool value) =>
        SetSelectionState(value, selectionIndent);

    internal void SetSelectionState(
        bool value,
        float neighbourIndent,
        bool animated = true)
    {
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
        focusShadow.FadeTo(selected ? 1 : 0, 170, Easing.OutQuint);
        arrow.FadeTo(selected && compact ? 1 : 0, 120, Easing.OutQuint);
        selectedSticker.FadeTo(0, 140, Easing.OutQuint);
        compactModePill?.SetExpanded(selected, animated);
        float targetX = selectionTargetX();
        if (animated)
        {
            this.MoveToX(targetX, 170, Easing.OutQuint);
            this.ResizeWidthTo(
                row_width - targetX,
                170,
                Easing.OutQuint);
        }
        else
        {
            X = targetX;
            Width = row_width - targetX;
        }
    }

    protected override bool OnHover(HoverEvent e)
    {
        surface.FadeColour(
            compact
                ? new Color4(0.95f, 0.99f, 1f, 0.99f)
                : SongSelectTheme.PaleCyan,
            100);
        focusShadow.FadeTo(selected ? 1 : 0.42f, 120, Easing.OutQuint);
        float targetX = selectionTargetX() - 3;
        this.MoveToX(targetX, 120, Easing.OutQuint);
        this.ResizeWidthTo(
            row_width - targetX,
            120,
            Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        SetSelected(selected);

    private float selectionTargetX() =>
        restingX
        + selectionIndent
        - (selected && compact ? 3 : 0);

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
        focusShadow = null;
        compactModePill = null;
        compactPrimaryText = null;
        compactSecondaryText = null;
        standaloneArtworkFrame = null;
        leadingAccent = null;
        ClearInternal(true);
        base.FreeAfterUse();
    }

    private void addCompactContent(
        System.Collections.Generic.ICollection<Drawable> children,
        SongSelectEntry entry,
        ManiaDifficultyRatingMode difficultyRatingMode,
        string primaryText)
    {
        SpriteText difficultyUnit = addDifficultyUnit(new SpriteText
        {
            Text = ManiaDifficultyPresentation.Unit(
                difficultyRatingMode),
            Font = HomeTypography.Display(7),
            Colour = accent,
        });
        SpriteText difficultyValue = addDifficultyValue(new SpriteText
        {
            Text = ManiaDifficultyPresentation.FormatValue(
                displayedDifficultyRatings,
                difficultyRatingMode),
            Font = HomeTypography.Display(10),
            Colour = SongSelectTheme.Navy,
        });
        children.Add(new SongSelectInlineDifficultyRating(
            difficultyUnit,
            difficultyValue)
        {
            Position = new Vector2(628, 12),
        });
        children.Add(compactPrimaryText = adaptiveLabel(
            primaryText ?? entry.Beatmap.DifficultyName,
            24,
            3,
            560,
            15,
            SongSelectTheme.Navy,
            SongSelectTheme.Navy,
            true));
        children.Add(compactSecondaryText = adaptiveLabel(
            $"mapped by {entry.Beatmap.Creator}",
            24,
            25,
            560,
            8,
            new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.68f),
            new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.70f),
            true,
            false));
        children.Add(compactModePill = new SongSelectProgressiveModePill(
            entry,
            9));
    }

    private void addStandaloneContent(
        System.Collections.Generic.ICollection<Drawable> children,
        SongSelectEntry entry,
        Texture wallpaper,
        ManiaDifficultyRatingMode difficultyRatingMode)
    {
        children.Add(standaloneArtworkFrame = new Container
        {
            Position = new Vector2(8, 4),
            Size = new Vector2(76),
            Masking = true,
            CornerRadius = 7,
            BorderThickness = 1,
            BorderColour = new Color4(1f, 1f, 1f, 0.52f),
            Child = SongSelectArtworkCrop.Create(
                wallpaper,
                new Vector2(76)),
        });
        children.Add(adaptiveLabel(
            entry.Beatmap.Title,
            100,
            10,
            504,
            15,
            SongSelectTheme.Navy,
            SongSelectTheme.Navy,
            true));
        children.Add(adaptiveLabel(
            entry.Beatmap.Artist,
            100,
            36,
            484,
            10,
            new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.72f),
            new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.72f),
            true));
        children.Add(adaptiveLabel(
            $"mapped by {entry.Beatmap.Creator}",
            100,
            57,
            484,
            8,
            SongSelectTheme.Cyan,
            SongSelectTheme.Cyan,
            true,
            false));
        children.Add(createFullModePill(entry, 690, 22));
        children.Add(createDifficultyBadge(
            displayedDifficultyRatings,
            difficultyRatingMode,
            -14,
            -13));
    }

    private static Drawable createFullModePill(
        SongSelectEntry entry,
        float x,
        float y) => new Container
        {
            Position = new Vector2(x, y),
            Size = new Vector2(144, 26),
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
            new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = 130,
                Truncate = true,
                Text = $"{(int)entry.Beatmap.KeyMode}K · "
                       + entry.Beatmap.DifficultyName,
                Font = HomeTypography.Display(8),
                Colour = Color4.White,
            },
        ],
        };

    private SpriteText adaptiveLabel(
        string value,
        float x,
        float y,
        float width,
        float size,
        Color4 colour,
        Color4 selectedColour,
        bool truncate = false,
        bool strong = true)
    {
        var text = new SpriteText
        {
            Position = new Vector2(x, y),
            Width = width,
            Truncate = truncate,
            Text = value,
            Font = strong
            ? HomeTypography.Display(size)
            : HomeTypography.Body(size),
            Colour = colour,
        };
        adaptiveTexts.Add((text, colour, selectedColour));
        return text;
    }

    private Drawable createDifficultyBadge(
        ManiaDifficultyRatings ratings,
        ManiaDifficultyRatingMode mode,
        float x,
        float y)
    {
        var flow = new FillFlowContainer
        {
            Anchor = Anchor.BottomRight,
            Origin = Anchor.BottomRight,
            Position = new Vector2(x, y),
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(2, 0),
        };
        SpriteText unitText = addDifficultyUnit(new SpriteText
        {
            Text = ManiaDifficultyPresentation.Unit(mode),
            Font = HomeTypography.Display(8),
            Colour = DifficultyColour(ratings, mode),
        });
        SpriteText valueText = addDifficultyValue(new SpriteText
        {
            Text = ManiaDifficultyPresentation.FormatValue(
                ratings,
                mode),
            Font = HomeTypography.Display(10),
            Colour = SongSelectTheme.Ivory,
        });
        adaptiveTexts.Add((
            unitText,
            DifficultyColour(ratings, mode),
            DifficultyColour(ratings, mode)));
        adaptiveTexts.Add((
            valueText,
            SongSelectTheme.Ivory,
            SongSelectTheme.Navy));
        flow.Add(unitText);
        flow.Add(valueText);
        return flow;
    }

    private Box addAccent(Box box, float alpha = 1)
    {
        accentBoxes.Add((box, alpha));
        return box;
    }

    private SpriteText addDifficultyValue(SpriteText text)
    {
        difficultyValueTexts.Add(text);
        return text;
    }

    private SpriteText addDifficultyUnit(SpriteText text)
    {
        difficultyUnitTexts.Add(text);
        return text;
    }

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
    private const float compact_width = 54;
    private const float expanded_width = 116;
    private const float right_edge = 834;

    private readonly Box surface;
    private readonly SpriteText compactText;
    private readonly SpriteText expandedText;

    internal float CompactTextAlpha => compactText.Alpha;
    internal float ExpandedTextAlpha => expandedText.Alpha;

    internal SongSelectProgressiveModePill(
        SongSelectEntry entry,
        float y)
    {
        Position = new Vector2(right_edge - compact_width, y);
        Size = new Vector2(compact_width, 26);
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
                Width = 44,
                Truncate = true,
                Text = $"{(int)entry.Beatmap.KeyMode}K",
                Font = HomeTypography.Display(9),
                Colour = SongSelectTheme.Pink,
            },
            expandedText = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = 102,
                Truncate = true,
                Text = $"{(int)entry.Beatmap.KeyMode}K · SELECTED",
                Font = HomeTypography.Display(8),
                Colour = Color4.White,
                Alpha = 0,
            },
        ];
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
        Size = new Vector2(64, 20);
        UnitText.Position = new Vector2(0, 6);
        ValueText.Anchor = Anchor.TopRight;
        ValueText.Origin = Anchor.TopRight;
        ValueText.Y = 2;
        InternalChildren =
        [
            UnitText,
            ValueText,
        ];
    }
}

internal partial class SongSelectPackageHeader : PoolableDrawable
{
    internal const float ExpandedHeight = 132;
    internal const float CollapsedHeight = 96;

    private const float expanded_content_start = 156;
    private const float collapsed_content_start = 120;
    private const float action_safe_right = 796;

    private Action toggle;
    private Box stateBackground;
    private Box selectedRail;
    private Box childGuideStem;
    private Box expandedRail;
    private Box hoverSurface;
    private Box chevronSurface;
    private SpriteIcon favouriteIcon;
    private SpriteIcon selectedIndicator;
    private Container artworkFrame;
    private Container artworkImageFrame;
    private Container chevronFrame;
    private Container packageSummaryLayer;
    private Container selectedSummaryLayer;
    private SpriteText[] packageTitleTexts;
    private SpriteText selectedTitle;
    private SpriteText selectedByline;
    private SpriteText selectedModeText;
    private SpriteText selectedRatingUnit;
    private SpriteText selectedRatingValue;
    private bool expanded;

    internal float ChildGuideStemAlpha => childGuideStem?.Alpha ?? 0;
    internal float SelectedRailHeight => selectedRail?.Height ?? 0;
    internal float SelectedIndicatorAlpha => selectedIndicator?.Alpha ?? 0;
    internal Vector2 ArtworkFrameSize => artworkFrame?.Size ?? Vector2.Zero;
    internal Vector2 ArtworkImageFrameSize =>
        artworkImageFrame?.Size ?? Vector2.Zero;
    internal float ArtworkImageCornerRadius =>
        artworkImageFrame?.CornerRadius ?? 0;
    internal float ExpandedRailAlpha => expandedRail?.Alpha ?? 0;
    internal float ChevronSurfaceAlpha => chevronSurface?.Alpha ?? 0;
    internal float PackageSummaryAlpha => packageSummaryLayer?.Alpha ?? 0;
    internal float SelectedSummaryAlpha => selectedSummaryLayer?.Alpha ?? 0;
    internal string SelectedContextTitle =>
        selectedTitle?.Text.ToString() ?? string.Empty;
    internal string SelectedContextByline =>
        selectedByline?.Text.ToString() ?? string.Empty;
    internal string SelectedContextMode =>
        selectedModeText?.Text.ToString() ?? string.Empty;
    internal string SelectedContextRating =>
        selectedRatingValue?.Text.ToString() ?? string.Empty;
    internal int PackageTitleLineCount => packageTitleTexts?.Length ?? 0;
    internal bool PackageTitleUsesTruncation =>
        packageTitleTexts?.All(text => text.Truncate) == true;
    internal bool IsExpanded => expanded;
    internal float PackageContentStart { get; private set; }
    internal Anchor FavouriteIconAnchor =>
        favouriteIcon?.Anchor ?? Anchor.TopLeft;
    internal Vector2 FavouriteIconPosition =>
        favouriteIcon?.Position ?? Vector2.Zero;
    internal Anchor ChevronFrameAnchor =>
        chevronFrame?.Anchor ?? Anchor.TopLeft;
    internal Vector2 ChevronFramePosition =>
        chevronFrame?.Position ?? Vector2.Zero;

    public SongSelectPackageHeader()
    {
    }

    public SongSelectPackageHeader(
        string packageName,
        int songCount,
        int chartCount,
        bool collapsed,
        Texture wallpaper,
        bool selected,
        Action toggle)
    {
        Bind(
            packageName,
            songCount,
            chartCount,
            collapsed,
            wallpaper,
            selected,
            toggle);
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
        ClearTransforms();
        ClearInternal(true);
        Alpha = 1;
        toggle = toggleAction;
        expanded = !collapsed;
        float headerHeight = collapsed ? CollapsedHeight : ExpandedHeight;
        float artworkSize = headerHeight;
        float contentStart = collapsed
            ? collapsed_content_start
            : expanded_content_start;
        PackageContentStart = contentStart;
        Size = new Vector2(850, headerHeight);
        Masking = true;
        CornerRadius = 10;
        BorderThickness = selected ? 2 : 1;
        BorderColour = selected
            ? SongSelectTheme.Cyan
            : new Color4(
                SongSelectTheme.Cyan.R,
                SongSelectTheme.Cyan.G,
                SongSelectTheme.Cyan.B,
                0.18f);

        InternalChildren =
        [
            stateBackground = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = selected
                    ? SongSelectSurface.Ivory(0.995f)
                    : SongSelectSurface.Ivory(0.98f),
            },
            artworkFrame = new Container
            {
                Size = new Vector2(artworkSize),
                Child = artworkImageFrame = new Container
                {
                    Position = new Vector2(5),
                    Size = new Vector2(artworkSize - 10),
                    Masking = true,
                    CornerRadius = 8,
                    BorderThickness = 1,
                    BorderColour = new Color4(1f, 1f, 1f, 0.58f),
                    Child = SongSelectArtworkCrop.Create(
                        wallpaper,
                        new Vector2(artworkSize - 10)),
                },
            },
            new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                RelativeSizeAxes = Axes.Y,
                Width = 850 - artworkSize,
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
                Position = new Vector2(5, headerHeight - 24),
                Width = 2,
                Height = 24,
                Colour = SongSelectTheme.Cyan,
                Alpha = expanded
                    ? selected ? 0.92f : 0.34f
                    : 0,
            },
            favouriteIcon = new SpriteIcon
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-18, 8),
                Size = new Vector2(16),
                Icon = FontAwesome.Solid.Star,
                Colour = SongSelectTheme.Yellow,
            },
            selectedIndicator = new SpriteIcon
            {
                Position = new Vector2(
                    selected
                        ? artworkSize + 8
                        : artworkSize + 4,
                    collapsed
                        ? headerHeight - 29
                        : headerHeight - 33),
                Size = new Vector2(11),
                Icon = FontAwesome.Solid.Play,
                Colour = SongSelectTheme.Pink,
                Alpha = selected ? 1 : 0,
            },
            packageSummaryLayer = createPackageSummary(
                packageName,
                songCount,
                chartCount,
                collapsed,
                contentStart),
            selectedSummaryLayer = createSelectedSummary(contentStart),
            chevronFrame = new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-11, headerHeight - 36),
                Size = new Vector2(30),
                Masking = true,
                CornerRadius = 7,
                Children =
                [
                    chevronSurface = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SongSelectTheme.PaleCyan,
                        Alpha = expanded ? 0.78f : 0.52f,
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(12),
                        Rotation = collapsed ? -90 : 0,
                        Icon = FontAwesome.Solid.ChevronDown,
                        Colour = SongSelectTheme.Cyan,
                    },
                ],
            },
            expandedRail = new Box
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Width = 850 - artworkSize,
                Height = 2,
                Colour = SongSelectTheme.Cyan,
                Alpha = expanded ? 0.32f : 0,
            },
            selectedRail = new Box
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Width = 850 - artworkSize,
                Height = selected ? 3 : 0,
                Colour = SongSelectTheme.Cyan,
            },
        ];
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
        bool showContext = expanded
                           && selected
                           && context != null
                           && ratings != null;
        if (showContext)
            updateSelectedContext(context, ratings, mode);

        BorderThickness = selected ? 2 : 1;
        BorderColour = selected
            ? SongSelectTheme.Cyan
            : new Color4(
                SongSelectTheme.Cyan.R,
                SongSelectTheme.Cyan.G,
                SongSelectTheme.Cyan.B,
                0.18f);

        if (packageSummaryLayer != null && selectedSummaryLayer != null)
        {
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
        }

        if (stateBackground != null)
        {
            Color4 target = selected
                ? SongSelectSurface.Ivory(0.995f)
                : SongSelectSurface.Ivory(0.98f);
            if (animated)
                stateBackground.FadeColour(target, 140, Easing.OutQuint);
            else
                stateBackground.Colour = target;
        }
        if (selectedIndicator != null)
        {
            if (animated)
            {
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
                selectedIndicator.X = selected
                    ? artworkFrame.Width + 8
                    : artworkFrame.Width + 4;
            }
        }
        if (selectedRail != null)
        {
            if (animated)
                selectedRail.ResizeHeightTo(
                    selected ? 3 : 0,
                    170,
                    Easing.OutQuint);
            else
                selectedRail.Height = selected ? 3 : 0;
        }
        if (childGuideStem != null)
        {
            float target = expanded
                ? selected ? 0.92f : 0.34f
                : 0;
            if (animated)
                childGuideStem.FadeTo(target, 140, Easing.OutQuint);
            else
                childGuideStem.Alpha = target;
        }
    }

    protected override bool OnClick(ClickEvent e)
    {
        toggle?.Invoke();
        return true;
    }

    protected override bool OnHover(HoverEvent e)
    {
        hoverSurface?.FadeTo(0.52f, 110, Easing.OutQuint);
        chevronSurface?.FadeTo(1, 110, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        hoverSurface?.FadeOut(130, Easing.OutQuint);
        chevronSurface?.FadeTo(
            expanded ? 0.78f : 0.52f,
            130,
            Easing.OutQuint);
    }

    protected override void FreeAfterUse()
    {
        toggle = null;
        selectedIndicator = null;
        selectedRail = null;
        childGuideStem = null;
        expandedRail = null;
        hoverSurface = null;
        chevronSurface = null;
        favouriteIcon = null;
        artworkFrame = null;
        artworkImageFrame = null;
        chevronFrame = null;
        packageSummaryLayer = null;
        selectedSummaryLayer = null;
        packageTitleTexts = null;
        selectedTitle = null;
        selectedByline = null;
        selectedModeText = null;
        selectedRatingUnit = null;
        selectedRatingValue = null;
        expanded = false;
        PackageContentStart = 0;
        ClearInternal(true);
        base.FreeAfterUse();
    }

    private Drawable packageTitle(
        string packageName,
        bool collapsed,
        float contentStart)
    {
        string[] lines = collapsed
            ? [packageName]
            : SongSelectTextLayout.TwoLines(packageName, 34);
        float titleWidth = action_safe_right - contentStart;
        var flow = new FillFlowContainer
        {
            Position = new Vector2(contentStart, collapsed ? 22 : 18),
            Width = titleWidth,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, -1),
        };
        packageTitleTexts = new SpriteText[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            var title = new SpriteText
            {
                Width = titleWidth,
                Truncate = true,
                Text = lines[i],
                Font = HomeTypography.Display(
                    collapsed ? 16 : lines.Length == 1 ? 18 : 15),
                Colour = SongSelectTheme.Navy,
            };
            packageTitleTexts[i] = title;
            flow.Add(title);
        }

        return flow;
    }

    private Container createPackageSummary(
        string packageName,
        int songCount,
        int chartCount,
        bool collapsed,
        float contentStart) => new()
        {
            RelativeSizeAxes = Axes.Both,
            Children =
            [
                packageTitle(packageName, collapsed, contentStart),
                label(
                    $"{songCount} SONGS · {chartCount} CHARTS",
                    contentStart,
                    collapsed ? 62 : 84,
                    action_safe_right - contentStart,
                    collapsed ? 8 : 9,
                    new Color4(
                        SongSelectTheme.Navy.R,
                        SongSelectTheme.Navy.G,
                        SongSelectTheme.Navy.B,
                        0.64f)),
            ],
        };

    private Container createSelectedSummary(float contentStart) => new()
    {
        RelativeSizeAxes = Axes.Both,
        Alpha = 0,
        Children =
        [
            selectedTitle = label(
                string.Empty,
                contentStart,
                14,
                590,
                17,
                SongSelectTheme.Navy),
            selectedByline = label(
                string.Empty,
                contentStart,
                44,
                500,
                9,
                new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.68f)),
            new Container
            {
                Position = new Vector2(contentStart, 92),
                Size = new Vector2(212, 24),
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
                            0.88f),
                    },
                    selectedModeText = new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Width = 196,
                        Truncate = true,
                        Font = HomeTypography.Display(8),
                        Colour = Color4.White,
                    },
                ],
            },
            selectedRatingUnit = label(
                string.Empty,
                650,
                98,
                40,
                7,
                SongSelectTheme.Cyan),
            selectedRatingValue = new SpriteText
            {
                Origin = Anchor.TopRight,
                Position = new Vector2(746, 92),
                Width = 54,
                Truncate = true,
                Font = HomeTypography.Display(11),
                Colour = SongSelectTheme.Navy,
            },
        ],
    };

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
