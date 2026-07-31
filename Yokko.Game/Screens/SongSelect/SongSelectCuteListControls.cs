using System;
using osu.Framework.Graphics;
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
    private readonly Box surface;
    private readonly Container card;
    private readonly Container selectionOutline;
    private readonly Container thumbnail;
    private readonly SpriteIcon arrow;
    private readonly Sprite selectedSticker;
    private readonly SpriteText title;
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
        Size = new Vector2(540, 84);

        Container panel = SongSelectSurface.CreateCard(
            out surface,
            SongSelectSurface.Ivory(),
            SongSelectSurface.Border(),
            9);

        InternalChildren =
        [
            SongSelectSurface.CreateShadow(9, 0.25f, 3),
            card = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children =
                [
                    panel,
                    selectionOutline = new Container
                    {
                        Position = new Vector2(2),
                        Size = new Vector2(536, 80),
                        Masking = true,
                        CornerRadius = 8,
                        BorderThickness = 2,
                        BorderColour = SongSelectTheme.Cyan,
                        Alpha = 0,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0,
                        },
                    },
                ],
            },
            thumbnail = new Container
            {
                Position = new Vector2(8),
                Size = new Vector2(142, 68),
                Masking = true,
                CornerRadius = 7,
                BorderThickness = 1,
                BorderColour = new Color4(1f, 1f, 1f, 0.8f),
                Child = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = wallpaper,
                    FillMode = FillMode.Fill,
                },
            },
            arrow = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreRight,
                X = -7,
                Size = new Vector2(17),
                Icon = FontAwesome.Solid.Play,
                Colour = SongSelectTheme.Pink,
                Alpha = 0,
            },
            selectedSticker = new Sprite
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-12, 4),
                Size = new Vector2(38),
                Texture = selectedStickerTexture,
                FillMode = FillMode.Fit,
                Alpha = 0,
            },
            title = label(entry.Beatmap.Title, 164, 8, 354, 19,
                SongSelectTheme.Navy, true),
            label(entry.Beatmap.Artist, 164, 37, 202, 13,
                SongSelectTheme.Navy, true),
            label($"mapped by {entry.Beatmap.Creator}", 164, 58, 202, 10,
                SongSelectTheme.Cyan, true),
            label($"{(int)entry.Beatmap.KeyMode}K · {entry.Beatmap.DifficultyName}",
                388, 37, 130, 11, SongSelectTheme.Pink, true),
            createStars(entry.StarRating),
            new SpriteIcon
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-7, 8),
                Size = new Vector2(10),
                Icon = FontAwesome.Solid.Plus,
                Colour = SongSelectTheme.Cyan,
            },
        ];
    }

    public void SetSelected(bool value)
    {
        selected = value;
        surface.FadeColour(
            selected
                ? new Color4(1f, 0.94f, 0.72f, 0.99f)
                : SongSelectSurface.Ivory(),
            140,
            Easing.OutQuint);
        selectionOutline.FadeTo(selected ? 1 : 0, 140, Easing.OutQuint);
        selectionOutline.ResizeHeightTo(selected ? 86 : 80, 160, Easing.OutQuint);
        arrow.FadeTo(selected ? 1 : 0, 120, Easing.OutQuint);
        selectedSticker.FadeTo(selected ? 1 : 0, 140, Easing.OutQuint);
        this.ResizeHeightTo(selected ? 90 : 84, 160, Easing.OutQuint);
        thumbnail.ResizeHeightTo(selected ? 74 : 68, 160, Easing.OutQuint);
    }

    protected override bool OnHover(HoverEvent e)
    {
        surface.FadeColour(new Color4(0.90f, 0.98f, 1f, 0.99f), 100);
        this.MoveToX(-3, 120, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.MoveToX(0, 120, Easing.OutQuint);
        SetSelected(selected);
    }

    protected override bool OnDoubleClick(DoubleClickEvent e)
    {
        DoubleClickAction?.Invoke();
        return true;
    }

    private static SpriteText label(
        string value,
        float x,
        float y,
        float width,
        float size,
        Color4 colour,
        bool truncate = false) => new()
    {
        Position = new Vector2(x, y),
        Width = width,
        Truncate = truncate,
        Text = value,
        Font = HomeTypography.Display(size),
        Colour = colour,
    };

    private static Drawable createStars(ManiaStarRatingResult rating)
    {
        var flow = new FillFlowContainer
        {
            Anchor = Anchor.BottomRight,
            Origin = Anchor.BottomRight,
            Position = new Vector2(-13, -9),
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(2, 0),
        };
        flow.Add(label(
            ManiaStarRatingPresentation.FormatValue(rating),
            0, 0, 38, 12, SongSelectTheme.Navy));
        int filled = rating.IsSuccess
            ? (int)Math.Min(5, Math.Floor(rating.Value ?? 0))
            : 0;
        for (int i = 0; i < 5; i++)
        {
            flow.Add(new SpriteIcon
            {
                Size = new Vector2(9),
                Icon = i < filled
                    ? FontAwesome.Solid.Star
                    : FontAwesome.Regular.Star,
                Colour = SongSelectTheme.Yellow,
            });
        }
        return flow;
    }
}

internal partial class SongSelectPackageHeader : ClickableContainer
{
    public SongSelectPackageHeader(
        string packageName,
        int songCount,
        int chartCount,
        bool collapsed,
        Action toggle)
    {
        Action = toggle;
        Size = new Vector2(540, 38);

        Container panel = SongSelectSurface.CreateCard(
            out _,
            SongSelectSurface.Ivory(0.98f),
            new Color4(
                SongSelectTheme.Cyan.R,
                SongSelectTheme.Cyan.G,
                SongSelectTheme.Cyan.B,
                0.48f),
            8,
            1);

        InternalChildren =
        [
            SongSelectSurface.CreateShadow(8, 0.22f, 2),
            panel,
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 4,
                Colour = SongSelectTheme.Yellow,
            },
            new Container
            {
                Position = new Vector2(4, 3),
                Size = new Vector2(532, 32),
                Masking = true,
                CornerRadius = 6,
                BorderThickness = 1,
                BorderColour = SongSelectTheme.Cyan,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                },
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 13,
                Size = new Vector2(12),
                Icon = FontAwesome.Solid.LayerGroup,
                Colour = SongSelectTheme.Yellow,
            },
            label(packageName, 35, 11, 290, 13),
            label($"{songCount} SONGS · {chartCount} CHARTS", 348, 12, 152, 9),
            new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
                X = -15,
                Size = new Vector2(11),
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
        float size) => new()
    {
        Position = new Vector2(x, y),
        Width = width,
        Truncate = true,
        Text = value,
        Font = HomeTypography.Display(size),
        Colour = SongSelectTheme.Navy,
    };
}
