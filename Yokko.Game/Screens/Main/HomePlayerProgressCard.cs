using System.Globalization;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Localisation;

namespace Yokko.Game.Screens.Main;

internal sealed record HomePlayerSummary(
    string PlayerName,
    int Rank,
    int Level,
    int NextLevelPercent,
    int HighestCombo,
    int PlayedSongs);

internal partial class HomePlayerProgressCard : CompositeDrawable
{
    public const float FullHeight = 148;
    public const float CompactHeight = 74;

    private readonly Container fullContent;
    private readonly Container compactContent;

    public bool IsCompact { get; private set; }

    public HomePlayerProgressCard(Texture avatar, HomePlayerSummary summary)
    {
        Size = new Vector2(520, FullHeight);

        InternalChildren = new Drawable[]
        {
            fullContent = createFullContent(avatar, summary),
            compactContent = createCompactContent(avatar, summary),
        };

        SetCompact(false);
    }

    public void SetCompact(bool compact)
    {
        IsCompact = compact;
        Height = compact ? CompactHeight : FullHeight;
        fullContent.Alpha = compact ? 0 : 1;
        compactContent.Alpha = compact ? 1 : 0;
    }

    private static Container createFullContent(
        Texture avatar,
        HomePlayerSummary summary)
    {
        LocalisableString rank = YokkoStrings.Get(
            "main.player.rank",
            summary.Rank.ToString("00", CultureInfo.InvariantCulture));
        LocalisableString nextLevel = YokkoStrings.Get(
            "main.player.next_level",
            summary.NextLevelPercent);

        return new Container
        {
            RelativeSizeAxes = Axes.X,
            Height = FullHeight,
            Children = new Drawable[]
            {
                createCardSurface(FullHeight),
                createAvatar(avatar, new Vector2(18, 20), 88),
                new SpriteText
                {
                    Position = new Vector2(122, 18),
                    Text = summary.PlayerName,
                    Font = HomeTypography.Display(21),
                    Spacing = new Vector2(1.2f, 0),
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(122, 47),
                    Text = rank,
                    Font = HomeTypography.Display(12),
                    Spacing = new Vector2(2.1f, 0),
                    Colour = new Color4(0.12f, 0.47f, 0.82f, 1f),
                },
                new SpriteText
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(-18, 15),
                    Text = $"LV. {summary.Level}",
                    Font = HomeTypography.Display(29),
                    Colour = HomeControlColours.Navy,
                },
                new Box
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(-17, 51),
                    Size = new Vector2(83, 3),
                    Colour = HomeControlColours.Pink,
                },
                new Box
                {
                    Position = new Vector2(122, 74),
                    Size = new Vector2(378, 2),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.78f),
                },
                new SpriteIcon
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(310, 75),
                    Size = new Vector2(27),
                    Icon = FontAwesome.Solid.Heartbeat,
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(122, 83),
                    Text = nextLevel,
                    Font = HomeTypography.Body(13),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.74f),
                },
                createStat(
                    new Vector2(23, 112),
                    FontAwesome.Solid.Heartbeat,
                    YokkoStrings.Get("main.player.highest_combo"),
                    summary.HighestCombo.ToString("N0", CultureInfo.InvariantCulture)),
                new Box
                {
                    Position = new Vector2(260, 112),
                    Size = new Vector2(1, 25),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.22f),
                },
                createStat(
                    new Vector2(288, 112),
                    FontAwesome.Solid.Music,
                    YokkoStrings.Get("main.player.played_songs"),
                    summary.PlayedSongs.ToString(CultureInfo.InvariantCulture)),
                createCornerDiamond(),
            },
        };
    }

    private static Container createCompactContent(
        Texture avatar,
        HomePlayerSummary summary)
    {
        LocalisableString rank = YokkoStrings.Get(
            "main.player.rank",
            summary.Rank.ToString("00", CultureInfo.InvariantCulture));
        LocalisableString nextLevel = YokkoStrings.Get(
            "main.player.next_level",
            summary.NextLevelPercent);

        return new Container
        {
            RelativeSizeAxes = Axes.X,
            Height = CompactHeight,
            Children = new Drawable[]
            {
                createCardSurface(CompactHeight),
                createAvatar(avatar, new Vector2(10, 9), 56),
                new SpriteText
                {
                    Position = new Vector2(80, 10),
                    Text = summary.PlayerName,
                    Font = HomeTypography.Display(18),
                    Spacing = new Vector2(1, 0),
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(80, 36),
                    Text = rank,
                    Font = HomeTypography.Display(11),
                    Spacing = new Vector2(1.7f, 0),
                    Colour = new Color4(0.12f, 0.47f, 0.82f, 1f),
                },
                new SpriteText
                {
                    Position = new Vector2(226, 10),
                    Text = $"LV. {summary.Level}",
                    Font = HomeTypography.Display(22),
                    Colour = HomeControlColours.Navy,
                },
                new Box
                {
                    Position = new Vector2(226, 40),
                    Size = new Vector2(82, 2),
                    Colour = HomeControlColours.Pink,
                },
                new SpriteText
                {
                    Position = new Vector2(226, 45),
                    Text = nextLevel,
                    Font = HomeTypography.Body(11),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.72f),
                },
                createCompactStat(
                    new Vector2(340, 11),
                    FontAwesome.Solid.Heartbeat,
                    YokkoStrings.Get("main.player.highest_combo"),
                    summary.HighestCombo.ToString("N0", CultureInfo.InvariantCulture)),
                createCompactStat(
                    new Vector2(432, 11),
                    FontAwesome.Solid.Music,
                    YokkoStrings.Get("main.player.played_songs"),
                    summary.PlayedSongs.ToString(CultureInfo.InvariantCulture)),
                createCornerDiamond(),
            },
        };
    }

    private static Drawable createCardSurface(float height) => new Container
    {
        RelativeSizeAxes = Axes.X,
        Height = height,
        Children = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(0, 5),
                RelativeSizeAxes = Axes.X,
                Height = height - 2,
                Masking = true,
                CornerRadius = 9,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.015f, 0.045f, 0.28f, 0.3f),
                },
            },
            new Container
            {
                Position = new Vector2(-2, -2),
                RelativeSizeAxes = Axes.X,
                Width = 1.008f,
                Height = height,
                Masking = true,
                CornerRadius = 11,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.5f),
                },
            },
            new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = height - 4,
                Masking = true,
                CornerRadius = 9,
                BorderThickness = 2,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                    },
                    new Container
                    {
                        Position = new Vector2(4),
                        RelativeSizeAxes = Axes.X,
                        Width = 0.985f,
                        Height = height - 12,
                        Masking = true,
                        CornerRadius = 6,
                        BorderThickness = 1,
                        BorderColour = new Color4(
                            HomeControlColours.Cyan.R,
                            HomeControlColours.Cyan.G,
                            HomeControlColours.Cyan.B,
                            0.32f),
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0,
                        },
                    },
                },
            },
        },
    };

    private static Drawable createAvatar(
        Texture avatar,
        Vector2 position,
        float size) => new Container
    {
        Position = position,
        Size = new Vector2(size),
        Masking = true,
        CornerRadius = size / 2,
        BorderThickness = 2,
        BorderColour = HomeControlColours.Navy,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = HomeControlColours.PaleCyan,
            },
            new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                FillMode = FillMode.Fill,
                Texture = avatar,
            },
        },
    };

    private static Drawable createStat(
        Vector2 position,
        IconUsage icon,
        LocalisableString label,
        string value) => new Container
    {
        Position = position,
        Size = new Vector2(205, 30),
        Children = new Drawable[]
        {
            new SpriteIcon
            {
                Origin = Anchor.CentreLeft,
                Position = new Vector2(0, 14),
                Size = new Vector2(22),
                Icon = icon,
                Colour = HomeControlColours.Pink,
            },
            new SpriteText
            {
                Position = new Vector2(35, -1),
                Text = label,
                Font = HomeTypography.Body(12),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(35, 13),
                Text = value,
                Font = HomeTypography.Display(20),
                Colour = HomeControlColours.Navy,
            },
        },
    };

    private static Drawable createCompactStat(
        Vector2 position,
        IconUsage icon,
        LocalisableString label,
        string value) => new Container
    {
        Position = position,
        Size = new Vector2(86, 53),
        Children = new Drawable[]
        {
            new SpriteIcon
            {
                Position = new Vector2(0, 2),
                Size = new Vector2(15),
                Icon = icon,
                Colour = HomeControlColours.Pink,
            },
            new SpriteText
            {
                Position = new Vector2(21, 0),
                Text = label,
                Font = HomeTypography.Body(9),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(21, 20),
                Text = value,
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Navy,
            },
        },
    };

    private static Drawable createCornerDiamond() => new Box
    {
        Anchor = Anchor.TopRight,
        Origin = Anchor.Centre,
        Size = new Vector2(15),
        Rotation = 45,
        Colour = HomeControlColours.Yellow,
    };
}
