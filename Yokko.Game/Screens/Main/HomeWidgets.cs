using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;

namespace Yokko.Game.Screens.Main;

/// <summary>
/// 右上角的实时时钟小卡片，与工具按钮同一套错位描边风格。
/// 分钟变化时才刷新文本，秒点持续闪烁提示时间在走。
/// </summary>
public partial class HomeClock : CompositeDrawable
{
    private readonly SpriteText timeText;
    private readonly SpriteText dateText;
    private readonly Circle secondDot;
    private string lastMinute = string.Empty;

    public HomeClock()
    {
        Size = new Vector2(240, 46);

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(0, 4),
                Size = new Vector2(240, 42),
                Masking = true,
                CornerRadius = 11,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.015f, 0.045f, 0.28f, 0.28f),
                },
            },
            new Container
            {
                Position = new Vector2(-2, -2),
                Size = new Vector2(244, 48),
                Masking = true,
                CornerRadius = 12,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(HomeControlColours.Cyan.R, HomeControlColours.Cyan.G, HomeControlColours.Cyan.B, 0.55f),
                },
            },
            new Container
            {
                Size = new Vector2(240, 44),
                Masking = true,
                CornerRadius = 11,
                BorderThickness = 2,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(1f, 1f, 1f, 0.96f),
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 16,
                        Size = new Vector2(15),
                        Icon = FontAwesome.Solid.Clock,
                        Colour = HomeControlColours.Navy,
                    },
                    timeText = new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 42,
                        Font = HomeTypography.Display(21),
                        Colour = HomeControlColours.Navy,
                    },
                    secondDot = new Circle
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 108,
                        Size = new Vector2(5),
                        Colour = HomeControlColours.Pink,
                    },
                    dateText = new SpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -16,
                        Font = HomeTypography.Body(13),
                        Colour = new Color4(0.18f, 0.28f, 0.58f, 0.85f),
                    },
                },
            },
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        updateTime(true);

        // 秒点闪烁，给静态卡片一点“活着”的感觉。
        secondDot.FadeTo(0.15f, 500, Easing.InOutSine)
                 .Then().FadeTo(1f, 500, Easing.InOutSine)
                 .Loop();
    }

    protected override void Update()
    {
        base.Update();
        updateTime(false);
    }

    private void updateTime(bool force)
    {
        string minute = DateTime.Now.ToString("HH:mm");
        if (!force && minute == lastMinute)
            return;

        lastMinute = minute;
        timeText.Text = minute;
        dateText.Text = DateTime.Now.ToString("MM·dd");
    }
}

/// <summary>
/// 警示带风格的斜纹装饰条，纯色斜杠等距排列，整体缓慢呼吸。
/// </summary>
public partial class HomeHazardStripes : CompositeDrawable
{
    public HomeHazardStripes(float width, Color4 colour)
    {
        Size = new Vector2(width, 8);
        Masking = true;

        var stripes = new FillFlowContainer
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            RelativeSizeAxes = Axes.Y,
            AutoSizeAxes = Axes.X,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(8, 0),
        };

        int count = (int)(width / 11) + 1;
        for (int i = 0; i < count; i++)
        {
            stripes.Add(new Box
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                Size = new Vector2(3.5f, 13),
                Rotation = 25,
                Colour = colour,
            });
        }

        InternalChild = stripes;
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        this.FadeTo(0.55f, 2200, Easing.InOutSine)
            .Then().FadeTo(1f, 2200, Easing.InOutSine)
            .Loop();
    }
}

/// <summary>
/// 长按 Esc 退出时的进度提示胶囊，仅按住期间显示。
/// </summary>
public partial class HomeExitHoldIndicator : CompositeDrawable
{
    private readonly Box progressFill;
    private bool isShown;

    public HomeExitHoldIndicator(LocalisableString text)
    {
        Size = new Vector2(340, 42);
        Alpha = 0;

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(-2, -2),
                Size = new Vector2(344, 46),
                Masking = true,
                CornerRadius = 12,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.015f, 0.045f, 0.28f, 0.32f),
                },
            },
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 10,
                BorderThickness = 2,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(1f, 1f, 1f, 0.97f),
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 15,
                        Size = new Vector2(14),
                        Icon = FontAwesome.Solid.PowerOff,
                        Colour = HomeControlColours.Pink,
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 40,
                        Text = text,
                        Font = HomeTypography.Display(14),
                        Colour = HomeControlColours.Navy,
                    },
                    new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Position = new Vector2(12, -5),
                        Size = new Vector2(316, 3),
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.14f),
                        },
                    },
                    new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Position = new Vector2(12, -5),
                        Size = new Vector2(316, 3),
                        Child = progressFill = new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 3,
                            Width = 0,
                            Colour = HomeControlColours.Pink,
                        },
                    },
                },
            },
        };
    }

    public void SetProgress(float progress) =>
        progressFill.Width = Math.Clamp(progress, 0, 1);

    public void Reveal()
    {
        if (isShown)
            return;

        isShown = true;
        SetProgress(0);
        this.FadeIn(140, Easing.OutQuint);
    }

    public void Conceal()
    {
        if (!isShown)
            return;

        isShown = false;
        this.FadeOut(180, Easing.OutQuint);
    }
}

/// <summary>
/// 主页青色舞台上的轻量节拍波形装饰。
/// </summary>
public partial class HomeSignalWave : CompositeDrawable
{
    private static readonly float[] barHeights =
    {
        7, 12, 19, 10, 24, 15, 8, 20, 28, 17, 11, 23, 14, 8,
    };

    public HomeSignalWave(Color4 colour)
    {
        Size = new Vector2(barHeights.Length * 8, 30);

        for (int i = 0; i < barHeights.Length; i++)
        {
            AddInternal(new Box
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(i * 8 + 2, 0),
                Size = new Vector2(3, barHeights[i]),
                Colour = colour,
            });
        }
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        this.FadeTo(0.45f, 1600, Easing.InOutSine)
            .Then().FadeTo(1f, 1600, Easing.InOutSine)
            .Loop();
    }
}

/// <summary>
/// 以错峰呼吸表现八拍循环的小型节拍指示灯。
/// </summary>
public partial class HomeBeatPips : CompositeDrawable
{
    public HomeBeatPips(Color4 colour, Color4 accent)
    {
        Size = new Vector2(112, 10);

        for (int i = 0; i < 8; i++)
        {
            bool accented = i is 0 or 4;
            AddInternal(new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(i * 15 + 4, 0),
                Size = new Vector2(accented ? 7 : 4),
                Colour = accented ? accent : colour,
            });
        }
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        for (int i = 0; i < InternalChildren.Count; i++)
        {
            InternalChildren[i]
                .Delay(i * 90)
                .FadeTo(0.35f, 520, Easing.InOutSine)
                .Then()
                .FadeTo(1f, 520, Easing.InOutSine)
                .Loop();
        }
    }
}
