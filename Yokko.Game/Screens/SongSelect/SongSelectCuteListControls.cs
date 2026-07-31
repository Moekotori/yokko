using System;
using System.Collections.Generic;
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

    private Box surface;
    private Box selectedSurface;
    private Container selectionOutline;
    private SpriteIcon arrow;
    private Sprite selectedSticker;
    private readonly List<(Box Box, float Alpha)> accentBoxes = [];
    private readonly List<(Container Container, float Alpha)> accentBorders = [];
    private readonly List<SpriteText> difficultyValueTexts = [];
    private readonly List<SpriteText> difficultyUnitTexts = [];
    private readonly List<(SpriteText Text, Color4 Normal, Color4 Selected)>
        adaptiveTexts = [];
    private Color4 accent;
    private ManiaDifficultyRatings displayedDifficultyRatings;
    private bool selected;

    public SongSelectEntry Entry { get; private set; }
    public Action Action { get; private set; }
    public Action DoubleClickAction { get; private set; }
    internal ManiaDifficultyRatings DisplayedDifficultyRatings =>
        displayedDifficultyRatings;

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
            select,
            play);
    }

    public void Bind(
        SongSelectEntry entry,
        ManiaDifficultyRatings ratings,
        ManiaDifficultyRatingMode difficultyRatingMode,
        Texture wallpaper,
        Texture selectedStickerTexture,
        Action select,
        Action play)
    {
        ClearTransforms();
        ClearInternal(true);
        accentBoxes.Clear();
        accentBorders.Clear();
        difficultyValueTexts.Clear();
        difficultyUnitTexts.Clear();
        adaptiveTexts.Clear();
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
            SongSelectSurface.Ivory(0.98f),
            new Color4(accent.R, accent.G, accent.B, 0.38f),
            9,
            1);
        accentBorders.Add((panel, 0.38f));
        selectedSurface = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = compact
                ? new Color4(1f, 0.97f, 0.68f, 0.58f)
                : new Color4(1f, 0.98f, 0.78f, 1f),
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
                    compact ? 0.045f : 0.06f),
            }, compact ? 0.045f : 0.06f),
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
        arrow.FadeTo(0, 120, Easing.OutQuint);
        selectedSticker.FadeTo(0, 140, Easing.OutQuint);
        this.MoveToX(0, 170, Easing.OutQuint);
    }

    protected override bool OnHover(HoverEvent e)
    {
        surface.FadeColour(
            new Color4(
                SongSelectTheme.PaleCyan.R,
                SongSelectTheme.PaleCyan.G,
                SongSelectTheme.PaleCyan.B,
                1f),
            100);
        this.MoveToX(-3, 120, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        SetSelected(selected);

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
        ClearInternal(true);
        base.FreeAfterUse();
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
            528,
            15,
            SongSelectTheme.Navy,
            SongSelectTheme.Navy,
            true));
        children.Add(adaptiveLabel(
            $"{entry.Beatmap.DifficultyName} · mapped by "
            + entry.Beatmap.Creator,
            82,
            32,
            548,
            9,
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
        children.Add(createModePill(entry, 690, 10));
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
            Size = new Vector2(220, 68),
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
            244,
            10,
            360,
            15,
            SongSelectTheme.Navy,
            SongSelectTheme.Navy,
            true));
        children.Add(adaptiveLabel(
            entry.Beatmap.Artist,
            244,
            36,
            340,
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
            244,
            57,
            340,
            8,
            SongSelectTheme.Cyan,
            SongSelectTheme.Cyan,
            true,
            false));
        children.Add(createModePill(entry, 690, 22));
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
        SpriteText unitText = addDifficultyUnit(new SpriteText
        {
            Text = ManiaDifficultyPresentation.Unit(mode),
            Font = HomeTypography.Display(8),
            Colour = difficultyColour(ratings, mode),
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
            difficultyColour(ratings, mode),
            difficultyColour(ratings, mode)));
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

internal partial class SongSelectPackageHeader : PoolableDrawable
{
    private Action toggle;
    private Box stateBackground;

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
        toggle = toggleAction;
        Size = new Vector2(850, 84);
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
                Width = 280,
                Texture = wallpaper,
                FillMode = FillMode.Fill,
            },
            new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                RelativeSizeAxes = Axes.Y,
                Width = 570,
                Child = stateBackground = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = selected
                        ? SongSelectSurface.Ivory(0.995f)
                        : SongSelectSurface.Ivory(0.98f),
                },
            },
            new SpriteIcon
            {
                Position = new Vector2(292, 18),
                Size = new Vector2(16),
                Icon = FontAwesome.Solid.Star,
                Colour = SongSelectTheme.Yellow,
            },
            packageTitle(packageName),
            label(
                $"{songCount} SONGS · {chartCount} CHARTS",
                318,
                53,
                440,
                9,
                new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.64f)),
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

    public void SetSelected(bool selected)
    {
        BorderThickness = selected ? 2 : 1;
        BorderColour = selected
            ? SongSelectTheme.Cyan
            : new Color4(1f, 1f, 1f, 0.24f);
        if (stateBackground != null)
        {
            stateBackground.Colour = selected
                ? SongSelectSurface.Ivory(0.995f)
                : SongSelectSurface.Ivory(0.98f);
        }
    }

    protected override bool OnClick(ClickEvent e)
    {
        toggle?.Invoke();
        return true;
    }

    protected override void FreeAfterUse()
    {
        toggle = null;
        ClearInternal(true);
        base.FreeAfterUse();
    }

    private static Drawable packageTitle(string packageName)
    {
        string[] lines = SongSelectTextLayout.TwoLines(packageName, 27);
        var flow = new FillFlowContainer
        {
            Position = new Vector2(318, 11),
            Width = 430,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, -1),
        };
        foreach (string line in lines)
        {
            flow.Add(new SpriteText
            {
                Width = 430,
                Truncate = true,
                Text = line,
                Font = HomeTypography.Display(lines.Length == 1 ? 18 : 15),
                Colour = SongSelectTheme.Navy,
            });
        }

        return flow;
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
