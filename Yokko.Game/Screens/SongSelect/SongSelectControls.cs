using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Difficulty;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectSearchBox : BasicTextBox
{
    private readonly Action<string> queryChanged;

    protected override float LeftRightPadding => 42;

    public SongSelectSearchBox(Action<string> queryChanged)
    {
        this.queryChanged = queryChanged;
        Size = new Vector2(500, 40);
        Masking = true;
        CornerRadius = 4;
        BorderThickness = 1.2f;
        BorderColour = new Color4(1f, 1f, 1f, 0.78f);
        BackgroundUnfocused = new Color4(SongSelectTheme.DeepNavy.R, SongSelectTheme.DeepNavy.G, SongSelectTheme.DeepNavy.B, 0.72f);
        BackgroundFocused = new Color4(SongSelectTheme.Navy.R, SongSelectTheme.Navy.G, SongSelectTheme.Navy.B, 0.92f);
        FontSize = 16;
        PlaceholderText = "SEARCH";

        AddInternal(new SpriteIcon
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            X = 15,
            Size = new Vector2(17),
            Icon = FontAwesome.Solid.Search,
            Colour = SongSelectTheme.Ivory,
            Depth = -2,
        });

        Current.ValueChanged += onValueChanged;
    }

    private void onValueChanged(ValueChangedEvent<string> change) => queryChanged(change.NewValue);

    protected override Drawable GetDrawableCharacter(char c) => new SpriteText
    {
        Text = c.ToString(),
        Font = HomeTypography.Body(16),
        Colour = SongSelectTheme.Ivory,
    };

    protected override SpriteText CreatePlaceholder() => new()
    {
        Font = HomeTypography.Body(16),
        Colour = new Color4(1f, 1f, 1f, 0.68f),
    };

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        BorderColour = SongSelectTheme.Cyan;
        BorderThickness = 2;
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        BorderColour = new Color4(1f, 1f, 1f, 0.78f);
        BorderThickness = 1.2f;
    }
}

internal partial class SongSelectFilterButton : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteText label;
    private readonly Circle accentDot;
    private bool selected;

    public SongSelectFilterButton(LocalisableString text, float width, Action action, bool accentDot = false)
    {
        Action = action;
        Size = new Vector2(width, 30);
        Masking = true;
        CornerRadius = 3;
        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
            },
            label = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = text,
                Font = HomeTypography.Display(14),
            },
            this.accentDot = new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 11,
                Size = new Vector2(6),
                Colour = SongSelectTheme.Pink,
                Alpha = accentDot ? 1 : 0,
            },
        };
        SetSelected(false);
    }

    public void SetSelected(bool value)
    {
        selected = value;
        background.Colour = selected
            ? SongSelectTheme.Navy
            : new Color4(SongSelectTheme.Ivory.R, SongSelectTheme.Ivory.G, SongSelectTheme.Ivory.B, 0.92f);
        label.Colour = selected ? SongSelectTheme.Ivory : SongSelectTheme.Navy;
    }

    protected override bool OnHover(HoverEvent e)
    {
        this.ScaleTo(1.04f, 110, Easing.OutQuint);
        background.FadeColour(
            selected ? new Color4(0.055f, 0.14f, 0.52f, 1f) : SongSelectTheme.PaleCyan,
            110,
            Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.ScaleTo(1, 130, Easing.OutQuint);
        SetSelected(selected);
    }
}

internal partial class SongSelectSongRow : ClickableContainer
{
    private readonly Box tint;
    private readonly Box selectionCornerH;
    private readonly Box selectionCornerV;
    private readonly Box selectedTopBorder;
    private readonly Box selectedBottomBorder;
    private readonly Box selectedLeftBorder;
    private readonly Box selectedRightBorder;
    private readonly SpriteIcon selectionArrow;
    private readonly Container rowBackground;
    private readonly Container thumbnail;
    private readonly SpriteText title;
    private readonly SpriteText metadata;
    private readonly SpriteText mapper;
    private bool selected;

    public SongSelectEntry Entry { get; }

    public SongSelectSongRow(SongSelectEntry entry, Texture wallpaper, Action select, Action play)
    {
        Entry = entry;
        Action = select;
        Size = new Vector2(585, 68);

        InternalChildren = new Drawable[]
        {
            rowBackground = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 1,
                Children = new Drawable[]
                {
                    new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Texture = wallpaper,
                        FillMode = FillMode.Fill,
                        Alpha = 0.22f,
                    },
                    tint = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(SongSelectTheme.DeepNavy.R, SongSelectTheme.DeepNavy.G, SongSelectTheme.DeepNavy.B, 0.76f),
                    },
                },
            },
            thumbnail = new Container
            {
                Position = new Vector2(0, 2),
                Size = new Vector2(90, 64),
                Masking = true,
                CornerRadius = 1,
                BorderThickness = 1,
                BorderColour = new Color4(1f, 1f, 1f, 0.68f),
                Child = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = wallpaper,
                    FillMode = FillMode.Fill,
                },
            },
            selectionCornerH = new Box
            {
                Position = new Vector2(3, 3),
                Size = new Vector2(21, 4),
                Colour = SongSelectTheme.Yellow,
                Alpha = 0,
            },
            selectionCornerV = new Box
            {
                Position = new Vector2(3, 3),
                Size = new Vector2(4, 21),
                Colour = SongSelectTheme.Yellow,
                Alpha = 0,
            },
            selectionArrow = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreRight,
                X = -8,
                Size = new Vector2(18),
                Icon = FontAwesome.Solid.Play,
                Colour = SongSelectTheme.Yellow,
                Alpha = 0,
            },
            new Container
            {
                X = 102,
                RelativeSizeAxes = Axes.Both,
                Width = 0.82f,
                Children = new Drawable[]
                {
                    title = new SpriteText
                    {
                        Y = 7,
                        Width = 300,
                        Truncate = true,
                        Text = entry.Beatmap.Title,
                        Font = HomeTypography.Display(19),
                        Colour = SongSelectTheme.Ivory,
                    },
                    new SpriteText
                    {
                        Y = 31,
                        Width = 270,
                        Truncate = true,
                        Text = entry.Beatmap.Artist,
                        Font = HomeTypography.Body(13),
                        Colour = SongSelectTheme.Ivory,
                    },
                    mapper = new SpriteText
                    {
                        Y = 48,
                        Width = 270,
                        Truncate = true,
                        Text = $"mapped by {entry.Beatmap.Creator}",
                        Font = HomeTypography.Body(11),
                        Colour = SongSelectTheme.PaleCyan,
                    },
                    metadata = new SpriteText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Y = 9,
                        Width = 160,
                        Truncate = true,
                        Text = $"{(int)entry.Beatmap.KeyMode}K · {entry.Beatmap.DifficultyName}",
                        Font = HomeTypography.Display(12),
                        Colour = entry.Beatmap.DifficultyName.Contains("Insane", StringComparison.OrdinalIgnoreCase)
                            ? new Color4(0.55f, 0.36f, 1f, 1f)
                            : SongSelectTheme.Pink,
                    },
                    createRowStarRating(entry.StarRating),
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -3,
                        Size = new Vector2(12),
                        Icon = FontAwesome.Solid.EllipsisV,
                        Colour = SongSelectTheme.Ivory,
                    },
                },
            },
            new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = new Color4(1f, 1f, 1f, 0.24f),
            },
            selectedTopBorder = new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 2,
                Colour = SongSelectTheme.Cyan,
                Alpha = 0,
            },
            selectedBottomBorder = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Height = 2,
                Colour = SongSelectTheme.Cyan,
                Alpha = 0,
            },
            selectedLeftBorder = new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 2,
                Colour = SongSelectTheme.Cyan,
                Alpha = 0,
            },
            selectedRightBorder = new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                RelativeSizeAxes = Axes.Y,
                Width = 2,
                Colour = SongSelectTheme.Cyan,
                Alpha = 0,
            },
        };

        DoubleClickAction = play;
    }

    public Action DoubleClickAction { get; }

    public void SetSelected(bool value)
    {
        selected = value;
        selectionCornerH.FadeTo(selected ? 1 : 0, 120, Easing.OutQuint);
        selectionCornerV.FadeTo(selected ? 1 : 0, 120, Easing.OutQuint);
        selectedTopBorder.FadeTo(selected ? 0.9f : 0, 120, Easing.OutQuint);
        selectedBottomBorder.FadeTo(selected ? 0.9f : 0, 120, Easing.OutQuint);
        selectedLeftBorder.FadeTo(selected ? 0.9f : 0, 120, Easing.OutQuint);
        selectedRightBorder.FadeTo(selected ? 0.9f : 0, 120, Easing.OutQuint);

        if (selected)
        {
            selectionArrow.FadeTo(1, 120, Easing.OutQuint);
            if (IsLoaded)
                startArrowPulse();
        }
        else
        {
            selectionArrow.ClearTransforms();
            selectionArrow.Scale = Vector2.One;
            selectionArrow.Alpha = 0;
        }
        rowBackground.Shear = Vector2.Zero;
        thumbnail.Shear = Vector2.Zero;
        tint.FadeColour(
            selected
                ? new Color4(SongSelectTheme.Navy.R, SongSelectTheme.Navy.G, SongSelectTheme.Navy.B, 0.58f)
                : new Color4(SongSelectTheme.DeepNavy.R, SongSelectTheme.DeepNavy.G, SongSelectTheme.DeepNavy.B, 0.76f),
            150,
            Easing.OutQuint);
        this.ResizeHeightTo(selected ? 74 : 68, 170, Easing.OutQuint);
        thumbnail.ResizeHeightTo(selected ? 70 : 64, 170, Easing.OutQuint);
        this.MoveToX(0, 170, Easing.OutQuint);
        title.Font = HomeTypography.Display(selected ? 22 : 19);
        mapper.Colour = selected ? SongSelectTheme.Cyan : SongSelectTheme.PaleCyan;
    }

    private void startArrowPulse()
    {
        selectionArrow.ScaleTo(1.18f, 380, Easing.InOutSine)
                      .Then().ScaleTo(1f, 380, Easing.InOutSine)
                      .Loop();
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        if (selected)
            startArrowPulse();
    }

    protected override bool OnHover(HoverEvent e)
    {
        tint.FadeColour(new Color4(SongSelectTheme.Navy.R, SongSelectTheme.Navy.G, SongSelectTheme.Navy.B, 0.72f), 110, Easing.OutQuint);
        this.MoveToX(6, 130, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) => SetSelected(selected);

    protected override bool OnDoubleClick(DoubleClickEvent e)
    {
        DoubleClickAction?.Invoke();
        return true;
    }

    private static Drawable createRowStarRating(
        ManiaStarRatingResult rating)
    {
        double value = rating.Value ?? 0;
        int filled = rating.IsSuccess ? (int)Math.Min(5, Math.Floor(value)) : 0;
        Color4 starColour = rating.IsSuccess ? SongSelectTheme.Yellow : SongSelectTheme.PaleCyan;

        var flow = new FillFlowContainer
        {
            Anchor = Anchor.BottomRight,
            Origin = Anchor.BottomRight,
            Position = new Vector2(-27, -10),
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(3, 0),
        };

        flow.Add(new SpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Text = rating.Value?.ToString("0.00") ?? "--",
            Font = HomeTypography.Display(15),
            Colour = rating.IsSuccess ? SongSelectTheme.Ivory : SongSelectTheme.PaleCyan,
        });

        for (int i = 0; i < 5; i++)
        {
            flow.Add(new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Size = new Vector2(11),
                Icon = i < filled ? FontAwesome.Solid.Star : FontAwesome.Regular.Star,
                Colour = starColour,
            });
        }

        return flow;
    }
}

internal partial class SongSelectPackageHeader : ClickableContainer
{
    private readonly SpriteIcon chevron;

    public SongSelectPackageHeader(
        string packageName,
        int songCount,
        int chartCount,
        bool collapsed,
        Action toggle)
    {
        Action = toggle;
        Size = new Vector2(585, 38);
        Masking = true;
        CornerRadius = 2;
        BorderThickness = 1;
        BorderColour = new Color4(
            SongSelectTheme.Cyan.R,
            SongSelectTheme.Cyan.G,
            SongSelectTheme.Cyan.B,
            0.72f);

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    SongSelectTheme.DeepNavy.R,
                    SongSelectTheme.DeepNavy.G,
                    SongSelectTheme.DeepNavy.B,
                    0.9f),
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 13,
                Size = new Vector2(14),
                Icon = FontAwesome.Solid.LayerGroup,
                Colour = SongSelectTheme.Yellow,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 40,
                Width = 350,
                Truncate = true,
                Text = packageName,
                Font = HomeTypography.Display(15),
                Colour = SongSelectTheme.Ivory,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -40,
                Text = $"{songCount} SONG{(songCount == 1 ? string.Empty : "S")} · {chartCount} CHART{(chartCount == 1 ? string.Empty : "S")}",
                Font = HomeTypography.Display(9),
                Colour = SongSelectTheme.PaleCyan,
            },
            chevron = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
                X = -17,
                Size = new Vector2(13),
                Icon = FontAwesome.Solid.ChevronDown,
                Colour = SongSelectTheme.Cyan,
            },
        };

        chevron.Rotation = collapsed ? -90 : 0;
    }

    protected override bool OnHover(HoverEvent e)
    {
        this.FadeColour(new Color4(1f, 1f, 1f, 0.86f), 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        this.FadeColour(Color4.White, 100, Easing.OutQuint);
}

internal partial class SongSelectRankingPanel : CompositeDrawable
{
    private readonly Container content;
    private readonly SpriteText selectorText;
    private readonly SpriteIcon selectorIcon;
    private readonly SongSelectEntry entry;
    private readonly TextureStore textures;
    private SongSelectScoreView view;

    public SongSelectScoreView View => view;

    public SongSelectRankingPanel(SongSelectEntry entry, TextureStore textures, Action<SongSelectScoreView> viewChanged)
    {
        this.entry = entry;
        this.textures = textures;
        Size = new Vector2(375, 190);

        InternalChildren = new Drawable[]
        {
            new ClickableContainer
            {
                Size = new Vector2(375, 30),
                Masking = true,
                CornerRadius = 2,
                Action = () =>
                {
                    SetView(view == SongSelectScoreView.GlobalRanking
                        ? SongSelectScoreView.Personal
                        : SongSelectScoreView.GlobalRanking);
                    viewChanged(View);
                },
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(
                            SongSelectTheme.PaleCyan.R,
                            SongSelectTheme.PaleCyan.G,
                            SongSelectTheme.PaleCyan.B,
                            0.7f),
                    },
                    selectorText = new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 12,
                        Font = HomeTypography.Display(13),
                        Colour = SongSelectTheme.Navy,
                    },
                    selectorIcon = new SpriteIcon
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -10,
                        Size = new Vector2(12),
                        Icon = FontAwesome.Solid.ChevronDown,
                        Colour = SongSelectTheme.Cyan,
                    },
                },
            },
            content = new Container
            {
                Y = 36,
                Size = new Vector2(375, 154),
            },
        };

        SetView(SongSelectScoreView.GlobalRanking, textures);
    }

    public void SetView(SongSelectScoreView newView, TextureStore textures = null)
    {
        textures ??= this.textures;
        view = newView;
        selectorText.Text = view == SongSelectScoreView.GlobalRanking
            ? "GLOBAL RANKING"
            : "MY RECORD";
        selectorIcon.RotateTo(view == SongSelectScoreView.GlobalRanking ? 0 : 180, 150, Easing.OutQuint);
        content.Clear();

        if (view == SongSelectScoreView.Personal)
            content.Add(createPersonalRecord());
        else
            content.Add(createRanking(textures));
    }

    private Drawable createPersonalRecord() => new Container
    {
        Size = new Vector2(375, 150),
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    SongSelectTheme.PaleCyan.R,
                    SongSelectTheme.PaleCyan.G,
                    SongSelectTheme.PaleCyan.B,
                    0.62f),
            },
            new SpriteText
            {
                Position = new Vector2(16, 16),
                Text = YokkoStrings.Get("song_select.local_best"),
                Font = HomeTypography.Display(14),
                Colour = SongSelectTheme.Cyan,
            },
            new SpriteText
            {
                Position = new Vector2(16, 44),
                Text = $"{entry.BestScore:N0}",
                Font = HomeTypography.Display(36),
                Colour = SongSelectTheme.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(210, 51),
                Text = $"{entry.BestAccuracy:P2}",
                Font = HomeTypography.Display(24),
                Colour = SongSelectTheme.Pink,
            },
        },
    };

    private Drawable createRanking(TextureStore textures)
    {
        if (entry.Ranking.Count == 0)
        {
            return new Container
            {
                Size = new Vector2(375, 58),
                Masking = true,
                CornerRadius = 2,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(
                            SongSelectTheme.PaleCyan.R,
                            SongSelectTheme.PaleCyan.G,
                            SongSelectTheme.PaleCyan.B,
                            0.62f),
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 16,
                        Size = new Vector2(15),
                        Icon = FontAwesome.Solid.Trophy,
                        Colour = new Color4(
                            SongSelectTheme.Navy.R,
                            SongSelectTheme.Navy.G,
                            SongSelectTheme.Navy.B,
                            0.58f),
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 44,
                        Text = "NO RANKING DATA · PLAY TO SET A RECORD",
                        Font = HomeTypography.Display(10),
                        Colour = new Color4(
                            SongSelectTheme.Navy.R,
                            SongSelectTheme.Navy.G,
                            SongSelectTheme.Navy.B,
                            0.68f),
                    },
                },
            };
        }

        var flow = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 1),
        };

        foreach (SongSelectScore score in entry.Ranking.Take(5))
        {
            Texture avatar = score.IsCurrentPlayer
                ? textures.Get("yokko").Crop(new RectangleF(270, 2200, 850, 850))
                : textures.Get(score.AvatarTexture);
            flow.Add(createRankingRow(score, avatar));
        }

        return flow;
    }

    private static Drawable createRankingRow(SongSelectScore score, Texture avatar)
    {
        Color4 accent = score.IsCurrentPlayer ? SongSelectTheme.Pink : score.Rank == 1 ? SongSelectTheme.Yellow : SongSelectTheme.Cyan;
        var row = new Container
        {
            Size = new Vector2(375, 29),
            Masking = true,
            CornerRadius = 2,
            BorderThickness = score.IsCurrentPlayer ? 1 : 0,
            BorderColour = accent,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        SongSelectTheme.PaleCyan.R,
                        SongSelectTheme.PaleCyan.G,
                        SongSelectTheme.PaleCyan.B,
                        score.IsCurrentPlayer ? 0.76f : 0.46f),
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 9,
                    Text = score.Rank.ToString(),
                    Font = HomeTypography.Display(16),
                    Colour = accent,
                },
                new Container
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 30,
                    Size = new Vector2(22),
                    Masking = true,
                    CornerRadius = 11,
                    BorderThickness = 1,
                    BorderColour = accent,
                    Child = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Texture = avatar,
                        FillMode = FillMode.Fill,
                    },
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 60,
                    Width = 78,
                    Truncate = true,
                    Text = score.PlayerName,
                    Font = HomeTypography.Display(12),
                    Colour = score.IsCurrentPlayer ? SongSelectTheme.Pink : SongSelectTheme.Navy,
                },
                new Container
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 143,
                    Size = new Vector2(32, 22),
                    Masking = true,
                    CornerRadius = 4,
                    BorderThickness = 1,
                    BorderColour = gradeColour(score.Grade),
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(
                                SongSelectTheme.Ivory.R,
                                SongSelectTheme.Ivory.G,
                                SongSelectTheme.Ivory.B,
                                0.9f),
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = score.Grade == Yokko.Core.Scoring.ScoreRank.X ? "SS" : score.Grade.ToString(),
                            Font = HomeTypography.Display(14),
                            Colour = gradeColour(score.Grade),
                        },
                    },
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -54,
                    Y = -3,
                    Text = $"{score.Score:N0}",
                    Font = HomeTypography.Display(12),
                    Colour = SongSelectTheme.Navy,
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -54,
                    Y = 9,
                    Text = $"{score.Accuracy:P2}",
                    Font = HomeTypography.Display(9),
                    Colour = SongSelectTheme.Cyan,
                },
                createMods(score.Mods),
            },
        };

        if (score.IsCurrentPlayer)
        {
            row.Add(new SpriteText
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                X = 116,
                Y = 2,
                Text = YokkoStrings.Get("song_select.you"),
                Font = HomeTypography.Display(8),
                Colour = SongSelectTheme.Pink,
            });
        }

        return row;
    }

    private static Drawable createMods(IReadOnlyList<string> mods)
    {
        var flow = new FillFlowContainer
        {
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
            X = -6,
            Y = 6,
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(3, 0),
        };

        foreach (string mod in mods.Take(2))
        {
            flow.Add(new Container
            {
                Size = new Vector2(22, 15),
                Masking = true,
                CornerRadius = 3,
                BorderThickness = 1,
                BorderColour = mod == "DT" ? SongSelectTheme.Pink : SongSelectTheme.Cyan,
                Child = new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = mod,
                    Font = HomeTypography.Display(8),
                    Colour = mod == "DT" ? SongSelectTheme.Pink : SongSelectTheme.Cyan,
                },
            });
        }

        return flow;
    }

    private static Color4 gradeColour(Yokko.Core.Scoring.ScoreRank grade) => grade switch
    {
        Yokko.Core.Scoring.ScoreRank.X => SongSelectTheme.PaleCyan,
        Yokko.Core.Scoring.ScoreRank.S => SongSelectTheme.Cyan,
        Yokko.Core.Scoring.ScoreRank.A => new Color4(0.56f, 0.95f, 0.34f, 1f),
        Yokko.Core.Scoring.ScoreRank.B => SongSelectTheme.Yellow,
        _ => SongSelectTheme.Pink,
    };
}
