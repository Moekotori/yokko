using System;
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
    private const float row_width = 760;

    private readonly Box surface;
    private readonly Container selectionOutline;
    private readonly SpriteIcon arrow;
    private readonly Sprite selectedSticker;
    private readonly Color4 accent;
    private bool selected;

    public SongSelectEntry Entry { get; }
    public Action DoubleClickAction { get; }

    public SongSelectSongRow(
        SongSelectEntry entry,
        Texture wallpaper,
        Texture selectedStickerTexture,
        Action select,
        Action play)
    {
        Entry = entry;
        Action = select;
        DoubleClickAction = play;
        bool compact = entry.IsPackage;
        float rowHeight = compact ? 54 : 76;
        accent = difficultyColour(entry.StarRating);
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

        var children = new System.Collections.Generic.List<Drawable>
        {
            SongSelectSurface.CreateShadow(9, 0.22f, 2),
            panel,
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 5,
                Colour = accent,
            },
            new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                RelativeSizeAxes = Axes.Y,
                Width = 226,
                Colour = new Color4(
                    accent.R,
                    accent.G,
                    accent.B,
                    0.17f),
            },
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
            addCompactContent(children, entry);
        else
            addStandaloneContent(children, entry, wallpaper);

        InternalChildren = children;
    }

    public void SetSelected(bool value)
    {
        selected = value;
        surface.FadeColour(
            selected
                ? new Color4(
                    SongSelectTheme.SurfaceRaised.R,
                    SongSelectTheme.SurfaceRaised.G,
                    SongSelectTheme.SurfaceRaised.B,
                    0.98f)
                : new Color4(
                    SongSelectTheme.Surface.R,
                    SongSelectTheme.Surface.G,
                    SongSelectTheme.Surface.B,
                    0.90f),
            140,
            Easing.OutQuint);
        selectionOutline.FadeTo(selected ? 1 : 0, 140, Easing.OutQuint);
        arrow.FadeTo(selected ? 1 : 0, 120, Easing.OutQuint);
        selectedSticker.FadeTo(selected ? 1 : 0, 140, Easing.OutQuint);
        this.MoveToX(selected ? -4 : 0, 170, Easing.OutQuint);
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
        this.MoveToX(-5, 120, Easing.OutQuint);
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
        SongSelectEntry entry)
    {
        children.Add(new Container
        {
            Position = new Vector2(24, 10),
            Size = new Vector2(46, 34),
            Masking = true,
            CornerRadius = 9,
            BorderThickness = 1,
            BorderColour = accent,
            Children =
            [
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        accent.R,
                        accent.G,
                        accent.B,
                        0.86f),
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = ManiaStarRatingPresentation.FormatValue(
                        entry.StarRating),
                    Font = HomeTypography.Display(10),
                    Colour = SongSelectTheme.DeepNavy,
                },
            ],
        });
        children.Add(label(
            entry.Beatmap.Title,
            84,
            5,
            458,
            15,
            Color4.White,
            true));
        children.Add(label(
            $"{entry.Beatmap.DifficultyName} · mapped by "
            + entry.Beatmap.Creator,
            84,
            29,
            478,
            9,
            SongSelectTheme.PaleCyan,
            true,
            false));
        children.Add(createModePill(entry, 584, 8));
        children.Add(createStars(entry.StarRating, -18, -10));
    }

    private void addStandaloneContent(
        System.Collections.Generic.ICollection<Drawable> children,
        SongSelectEntry entry,
        Texture wallpaper)
    {
        children.Add(new Container
        {
            Position = new Vector2(8),
            Size = new Vector2(128, 60),
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
        children.Add(label(
            entry.Beatmap.Title,
            150,
            8,
            390,
            15,
            Color4.White,
            true));
        children.Add(label(
            entry.Beatmap.Artist,
            150,
            32,
            322,
            10,
            SongSelectTheme.PaleCyan,
            true));
        children.Add(label(
            $"mapped by {entry.Beatmap.Creator}",
            150,
            50,
            322,
            8,
            SongSelectTheme.Cyan,
            true,
            false));
        children.Add(createModePill(entry, 584, 18));
        children.Add(createStars(entry.StarRating, -18, -11));
    }

    private static Drawable createModePill(
        SongSelectEntry entry,
        float x,
        float y) => new Container
    {
        Position = new Vector2(x, y),
        Size = new Vector2(144, 22),
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

    private static SpriteText label(
        string value,
        float x,
        float y,
        float width,
        float size,
        Color4 colour,
        bool truncate = false,
        bool strong = true) => new()
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

    private static Drawable createStars(
        ManiaStarRatingResult rating,
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
        int filled = rating.IsSuccess
            ? (int)Math.Min(5, Math.Floor(rating.Value ?? 0))
            : 0;
        for (int i = 0; i < 5; i++)
        {
            flow.Add(new SpriteIcon
            {
                Size = new Vector2(8),
                Icon = i < filled
                    ? FontAwesome.Solid.Star
                    : FontAwesome.Regular.Star,
                Colour = difficultyColour(rating),
            });
        }
        return flow;
    }

    private static Color4 difficultyColour(
        ManiaStarRatingResult rating)
    {
        double value = rating.Value ?? 0;
        if (!rating.IsSuccess)
            return SongSelectTheme.Muted;
        if (value < 2.2)
            return SongSelectTheme.Cyan;
        if (value < 3.7)
            return new Color4(0.52f, 0.94f, 0.36f, 1f);
        if (value < 5.2)
            return SongSelectTheme.Yellow;
        if (value < 6.6)
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
        Size = new Vector2(760, 84);
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
                RelativeSizeAxes = Axes.Both,
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
                    selected ? 0.48f : 0.64f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = ColourInfo.GradientHorizontal(
                    new Color4(
                        SongSelectTheme.DeepNavy.R,
                        SongSelectTheme.DeepNavy.G,
                        SongSelectTheme.DeepNavy.B,
                        0.92f),
                    new Color4(
                        SongSelectTheme.DeepNavy.R,
                        SongSelectTheme.DeepNavy.G,
                        SongSelectTheme.DeepNavy.B,
                        0.18f)),
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 6,
                Colour = selected
                    ? SongSelectTheme.Cyan
                    : SongSelectTheme.Yellow,
            },
            new Container
            {
                Position = new Vector2(18, 14),
                Size = new Vector2(54, 20),
                Masking = true,
                CornerRadius = 7,
                Children =
                [
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SongSelectTheme.Yellow,
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = "PACK",
                        Font = HomeTypography.Display(8),
                        Colour = SongSelectTheme.DeepNavy,
                    },
                ],
            },
            label(packageName, 88, 10, 598, 18, Color4.White),
            label(
                $"{songCount} SONGS · {chartCount} CHARTS",
                88,
                43,
                460,
                9,
                SongSelectTheme.PaleCyan),
            new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
                X = -23,
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
