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
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectSongRow : ClickableContainer
{
    private readonly Box paper;
    private readonly Container card;
    private readonly Container thumbnail;
    private readonly SpriteIcon arrow;
    private readonly SpriteText title;
    private bool selected;

    public SongSelectEntry Entry { get; }
    public Action DoubleClickAction { get; }

    public SongSelectSongRow(
        SongSelectEntry entry,
        Texture wallpaper,
        Action select,
        Action play)
    {
        Entry = entry;
        Action = select;
        DoubleClickAction = play;
        Size = new Vector2(540, 88);

        InternalChildren =
        [
            card = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 7,
                BorderThickness = 1.2f,
                BorderColour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.18f),
                Children =
                [
                    paper = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(1f, 0.985f, 0.94f, 0.96f),
                    },
                ],
            },
            thumbnail = new Container
            {
                Position = new Vector2(7, 7),
                Size = new Vector2(145, 74),
                Masking = true,
                CornerRadius = 5,
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
            title = label(entry.Beatmap.Title, 166, 8, 350, 20,
                SongSelectTheme.Navy, true),
            label(entry.Beatmap.Artist, 166, 37, 202, 13,
                SongSelectTheme.Navy, true),
            label($"mapped by {entry.Beatmap.Creator}", 166, 58, 202, 10,
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
        paper.FadeColour(
            selected
                ? new Color4(1f, 0.95f, 0.82f, 0.99f)
                : new Color4(1f, 0.985f, 0.94f, 0.96f),
            140,
            Easing.OutQuint);
        card.BorderThickness = selected ? 2 : 1.2f;
        card.BorderColour = selected
            ? SongSelectTheme.Cyan
            : new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.18f);
        arrow.FadeTo(selected ? 1 : 0, 120, Easing.OutQuint);
        title.Colour = selected ? SongSelectTheme.Navy : SongSelectTheme.Navy;
        this.ResizeHeightTo(selected ? 96 : 88, 160, Easing.OutQuint);
        thumbnail.ResizeHeightTo(selected ? 82 : 74, 160, Easing.OutQuint);
    }

    protected override bool OnHover(HoverEvent e)
    {
        paper.FadeColour(new Color4(0.9f, 0.98f, 1f, 0.98f), 100);
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
            rating.Value?.ToString("0.00") ?? "--",
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
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1;
        BorderColour = SongSelectTheme.Cyan;
        InternalChildren =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(1f, 0.985f, 0.94f, 0.96f),
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
