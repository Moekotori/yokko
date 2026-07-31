using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
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

internal partial class SongSelectSongRow : ClickableContainer
{
    private const float row_width = 730;

    private readonly Box surface;
    private readonly Box selectedSurface;
    private readonly Container selectionOutline;
    private readonly SpriteIcon arrow;
    private readonly Sprite selectedSticker;
    private readonly List<(Box Box, float Alpha)> accentBoxes = [];
    private readonly List<(Container Container, float Alpha)> accentBorders = [];
    private readonly List<SpriteText> difficultyValueTexts = [];
    private readonly List<SpriteText> difficultyUnitTexts = [];
    private readonly List<(SpriteText Text, Color4 Normal, Color4 Selected)>
        adaptiveTexts = [];
    private Color4 accent;
    private ManiaDifficultyRatings displayedDifficultyRatings;
    private bool selected;

    public SongSelectEntry Entry { get; }
    public Action DoubleClickAction { get; }
    internal ManiaDifficultyRatings DisplayedDifficultyRatings =>
        displayedDifficultyRatings;

    public SongSelectSongRow(
        SongSelectEntry entry,
        ManiaDifficultyRatings ratings,
        ManiaDifficultyRatingMode difficultyRatingMode,
        Texture wallpaper,
        Texture selectedStickerTexture,
        Action select,
        Action play)
    {
        Entry = entry;
        Action = select;
        DoubleClickAction = play;
        bool compact = entry.IsPackage;
        float rowHeight = compact ? 58 : 84;
        ArgumentNullException.ThrowIfNull(ratings);
        displayedDifficultyRatings = ratings;
        accent = difficultyColour(
            ratings,
            difficultyRatingMode);
        Size = new Vector2(row_width, rowHeight);

        Container panel = SongSelectSurface.CreateCard(
            out surface,
            new Color4(
                SongSelectTheme.Surface.R,
                SongSelectTheme.Surface.G,
                SongSelectTheme.Surface.B,
                0.90f),
            new Color4(accent.R, accent.G, accent.B, 0.38f),
            9,
            1);
        accentBorders.Add((panel, 0.38f));
        selectedSurface = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = SongSelectSurface.Ivory(0.99f),
            Alpha = 0,
        };

        var children = new System.Collections.Generic.List<Drawable>
        {
            SongSelectSurface.CreateShadow(9, 0.22f, 2),
            panel,
            addAccent(new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 7,
                Colour = accent,
            }),
            addAccent(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    accent.R,
                    accent.G,
                    accent.B,
                    0.32f),
            }, 0.32f),
            // Keep the selected surface genuinely ivory. Placing it after the
            // accent wash avoids the muddy blue tint seen in the first pass.
            selectedSurface,
        };

        children.Add(selectionOutline = new Container
        {
            Position = new Vector2(2),
            Size = new Vector2(row_width - 4, rowHeight - 4),
            Masking = true,
            CornerRadius = 8,
            BorderThickness = 2,
            BorderColour = SongSelectTheme.Cyan,
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
            X = 8,
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
            addCompactContent(
                children,
                entry,
                difficultyRatingMode);
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
        accent = difficultyColour(ratings, mode);

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

    public void SetSelected(bool value)
    {
        selected = value;
        surface.FadeColour(new Color4(
            SongSelectTheme.Surface.R,
            SongSelectTheme.Surface.G,
            SongSelectTheme.Surface.B,
            0.90f), 140, Easing.OutQuint);
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
        arrow.FadeTo(0, 120, Easing.OutQuint);
        selectedSticker.FadeTo(0, 140, Easing.OutQuint);
        this.MoveToX(0, 170, Easing.OutQuint);
    }

    protected override bool OnHover(HoverEvent e)
    {
        surface.FadeColour(
            new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.96f),
            100);
        this.MoveToX(-3, 120, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        SetSelected(selected);

    protected override bool OnDoubleClick(DoubleClickEvent e)
    {
        DoubleClickAction?.Invoke();
        return true;
    }

    private void addCompactContent(
        System.Collections.Generic.ICollection<Drawable> children,
        SongSelectEntry entry,
        ManiaDifficultyRatingMode difficultyRatingMode)
    {
        var difficultyContainer = new Container
        {
            Position = new Vector2(18, 10),
            Size = new Vector2(48, 38),
            Masking = true,
            CornerRadius = 9,
            BorderThickness = 1,
            BorderColour = accent,
            Children =
            [
                addAccent(new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        accent.R,
                        accent.G,
                        accent.B,
                        0.86f),
                }, 0.86f),
                addDifficultyValue(new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = ManiaDifficultyPresentation.FormatValue(
                        displayedDifficultyRatings,
                        difficultyRatingMode),
                    Font = HomeTypography.Display(10),
                    Colour = SongSelectTheme.DeepNavy,
                }),
            ],
        };
        accentBorders.Add((difficultyContainer, 1));
        children.Add(difficultyContainer);
        children.Add(adaptiveLabel(
            entry.Beatmap.Title,
            82,
            6,
            422,
            15,
            Color4.White,
            SongSelectTheme.Navy,
            true));
        children.Add(adaptiveLabel(
            $"{entry.Beatmap.DifficultyName} · mapped by "
            + entry.Beatmap.Creator,
            82,
            32,
            442,
            9,
            SongSelectTheme.PaleCyan,
            new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.70f),
            true,
            false));
        children.Add(createModePill(entry, 570, 10));
        children.Add(createDifficultyBadge(
            displayedDifficultyRatings,
            difficultyRatingMode,
            -14,
            -12));
    }

    private void addStandaloneContent(
        System.Collections.Generic.ICollection<Drawable> children,
        SongSelectEntry entry,
        Texture wallpaper,
        ManiaDifficultyRatingMode difficultyRatingMode)
    {
        children.Add(new Container
        {
            Position = new Vector2(8),
            Size = new Vector2(140, 68),
            Masking = true,
            CornerRadius = 7,
            BorderThickness = 1,
            BorderColour = new Color4(1f, 1f, 1f, 0.52f),
            Child = new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                Texture = wallpaper,
                FillMode = FillMode.Fill,
            },
        });
        children.Add(adaptiveLabel(
            entry.Beatmap.Title,
            164,
            10,
            360,
            15,
            Color4.White,
            SongSelectTheme.Navy,
            true));
        children.Add(adaptiveLabel(
            entry.Beatmap.Artist,
            164,
            36,
            340,
            10,
            SongSelectTheme.PaleCyan,
            new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.72f),
            true));
        children.Add(adaptiveLabel(
            $"mapped by {entry.Beatmap.Creator}",
            164,
            57,
            340,
            8,
            SongSelectTheme.Cyan,
            SongSelectTheme.Cyan,
            true,
            false));
        children.Add(createModePill(entry, 570, 22));
        children.Add(createDifficultyBadge(
            displayedDifficultyRatings,
            difficultyRatingMode,
            -14,
            -13));
    }

    private static Drawable createModePill(
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
        flow.Add(addDifficultyUnit(new SpriteText
        {
            Text = ManiaDifficultyPresentation.Unit(mode),
            Font = HomeTypography.Display(8),
            Colour = difficultyColour(ratings, mode),
        }));
        flow.Add(addDifficultyValue(new SpriteText
        {
            Text = ManiaDifficultyPresentation.FormatValue(
                ratings,
                mode),
            Font = HomeTypography.Display(10),
            Colour = SongSelectTheme.Ivory,
        }));
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

    private static Color4 difficultyColour(
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
            return SongSelectTheme.Cyan;
        if (value < thresholds[1])
            return new Color4(0.52f, 0.94f, 0.36f, 1f);
        if (value < thresholds[2])
            return SongSelectTheme.Yellow;
        if (value < thresholds[3])
            return SongSelectTheme.Pink;
        return new Color4(0.64f, 0.47f, 1f, 1f);
    }

}

internal partial class SongSelectPackageHeader : ClickableContainer
{
    public SongSelectPackageHeader(
        string packageName,
        int songCount,
        int chartCount,
        bool collapsed,
        Texture wallpaper,
        bool selected,
        Action toggle)
    {
        Action = toggle;
        Size = new Vector2(730, 120);
        Masking = true;
        CornerRadius = 10;
        BorderThickness = selected ? 2 : 1;
        BorderColour = selected
            ? SongSelectTheme.Cyan
            : new Color4(1f, 1f, 1f, 0.24f);

        InternalChildren =
        [
            new Sprite
            {
                RelativeSizeAxes = Axes.Y,
                Width = 300,
                Texture = wallpaper,
                FillMode = FillMode.Fill,
            },
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    SongSelectTheme.DeepNavy.R,
                    SongSelectTheme.DeepNavy.G,
                    SongSelectTheme.DeepNavy.B,
                    selected ? 0.28f : 0.40f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = ColourInfo.GradientHorizontal(
                    new Color4(
                        SongSelectTheme.DeepNavy.R,
                        SongSelectTheme.DeepNavy.G,
                        SongSelectTheme.DeepNavy.B,
                        0.34f),
                    new Color4(
                        SongSelectTheme.DeepNavy.R,
                        SongSelectTheme.DeepNavy.G,
                        SongSelectTheme.DeepNavy.B,
                        0.96f)),
            },
            new SpriteIcon
            {
                Position = new Vector2(278, 25),
                Size = new Vector2(16),
                Icon = FontAwesome.Solid.Star,
                Colour = SongSelectTheme.Yellow,
            },
            label(packageName, 310, 21, 355, 18, Color4.White),
            label(
                $"{songCount} SONGS · {chartCount} CHARTS",
                310,
                76,
                350,
                9,
                SongSelectTheme.PaleCyan),
            new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
                X = -22,
                Size = new Vector2(14),
                Rotation = collapsed ? -90 : 0,
                Icon = FontAwesome.Solid.ChevronDown,
                Colour = SongSelectTheme.Cyan,
            },
        ];
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
