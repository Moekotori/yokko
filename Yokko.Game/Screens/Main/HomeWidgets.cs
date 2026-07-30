using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;

namespace Yokko.Game.Screens.Main;

/// <summary>
/// 左上角品牌标志。悬停时黄色十字徽章旋转一周、标志轻轻弹动。
/// </summary>
public partial class HomeBrandLockup : CompositeDrawable
{
    private readonly Container plusBadge;
    private readonly Sprite logo;
    private double lastSpin = double.MinValue;

    public HomeBrandLockup(Texture logoTexture, Color4 logoColour, Color4 badgeColour)
    {
        Size = new Vector2(500, 169);

        InternalChildren = new Drawable[]
        {
            logo = new Sprite
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Texture = logoTexture,
                Colour = logoColour,
            },
            plusBadge = new Container
            {
                Position = new Vector2(136 + 9.5f, 41 + 9.5f),
                Origin = Anchor.Centre,
                Size = new Vector2(19),
                Children = new Drawable[]
                {
                    new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(19, 7),
                        Masking = true,
                        CornerRadius = 3.5f,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = badgeColour,
                        },
                    },
                    new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(7, 19),
                        Masking = true,
                        CornerRadius = 3.5f,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = badgeColour,
                        },
                    },
                },
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        // 反复掠过时别让动画叠得太疯。
        if (Time.Current - lastSpin > 520)
        {
            lastSpin = Time.Current;
            plusBadge.RotateTo(0).RotateTo(360, 520, Easing.OutQuint);
            logo.ScaleTo(1.03f, 120, Easing.Out)
                .Then().ScaleTo(1f, 380, Easing.OutBack);
        }

        return true;
    }
}

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
/// 竖向虚线数据脊，把同列排布的遥测装饰在视觉上串成一条线。
/// </summary>
public partial class HomeDottedRail : CompositeDrawable
{
    public HomeDottedRail(float length, Color4 colour, float dash = 5, float gap = 9)
    {
        Size = new Vector2(2, length);

        for (float y = 0; y + dash <= length; y += dash + gap)
        {
            AddInternal(new Box
            {
                Y = y,
                Size = new Vector2(2, dash),
                Colour = colour,
            });
        }
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        this.FadeTo(0.6f, 2600, Easing.InOutSine)
            .Then().FadeTo(1f, 2600, Easing.InOutSine)
            .Loop();
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
    private readonly Box[] bars = new Box[barHeights.Length];

    public HomeSignalWave(Color4 colour)
    {
        Size = new Vector2(barHeights.Length * 8, 30);

        for (int i = 0; i < barHeights.Length; i++)
        {
            AddInternal(bars[i] = new Box
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

        for (int i = 0; i < bars.Length; i++)
        {
            double delay = i * 55;
            double duration = 360 + i % 4 * 55;

            bars[i].Delay(delay)
                   .ScaleTo(new Vector2(1, 0.42f), duration, Easing.InOutSine)
                   .Then().ScaleTo(new Vector2(1, 1.12f), duration, Easing.InOutSine)
                   .Then().ScaleTo(Vector2.One, duration, Easing.InOutSine)
                   .Loop(340);
        }
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

/// <summary>
/// 沿角色外围缓慢公转的节拍节点；节点各自呼吸，轨道整体旋转。
/// </summary>
public partial class HomeOrbitNodes : CompositeDrawable
{
    private readonly Container[] nodes;

    public HomeOrbitNodes(float radius, Color4 colour, Color4 accent, int count = 5)
    {
        Size = new Vector2(radius * 2);
        Origin = Anchor.Centre;
        nodes = new Container[count];

        for (int i = 0; i < count; i++)
        {
            float angle = i / (float)count * MathF.PI * 2;
            float degrees = i / (float)count * 360;
            bool accented = i % 2 == 0;

            AddInternal(nodes[i] = new Container
            {
                Origin = Anchor.Centre,
                Position = new Vector2(
                    radius + MathF.Cos(angle) * radius,
                    radius + MathF.Sin(angle) * radius),
                Size = new Vector2(accented ? 16 : 12),
                Rotation = degrees,
                Children = new Drawable[]
                {
                    new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(colour.R, colour.G, colour.B, 0.2f),
                    },
                    new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(accented ? 6 : 4),
                        Colour = accented ? accent : colour,
                    },
                    new Box
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -8,
                        Width = accented ? 28 : 20,
                        Height = 2,
                        Colour = new Color4(colour.R, colour.G, colour.B, 0.55f),
                    },
                },
            });
        }
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        this.RotateTo(0)
            .RotateTo(360, 18000, Easing.None)
            .Loop();

        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i].Delay(i * 120)
                    .ScaleTo(1.28f, 520, Easing.InOutSine)
                    .Then().ScaleTo(0.88f, 520, Easing.InOutSine)
                    .Then().ScaleTo(1f, 300, Easing.OutQuint)
                    .Loop(760);
        }
    }
}

/// <summary>
/// 周期性向外扩散的同步信标。
/// </summary>
public partial class HomePulseBeacon : CompositeDrawable
{
    private readonly Drawable[] pulseRings;
    private readonly Circle core;

    public HomePulseBeacon(float size, Color4 colour, Color4 accent)
    {
        Size = new Vector2(size);
        Origin = Anchor.Centre;

        pulseRings = new[]
        {
            createRing(size, colour),
            createRing(size, colour),
        };

        InternalChildren = new Drawable[]
        {
            pulseRings[0],
            pulseRings[1],
            core = new Circle
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(7),
                Colour = accent,
            },
        };
    }

    private static Drawable createRing(float size, Color4 colour) =>
        new Container
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = new Vector2(size),
            Masking = true,
            CornerRadius = size / 2,
            BorderThickness = 2,
            BorderColour = colour,
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                // Alpha 为 0 时子节点被剔除会导致边框一并消失，给一个趋近于 0 的值保住描边。
                Alpha = 0.01f,
            },
        };

    protected override void LoadComplete()
    {
        base.LoadComplete();

        for (int i = 0; i < pulseRings.Length; i++)
        {
            double delay = i * 720;

            pulseRings[i].ScaleTo(0.32f, 0)
                         .Delay(delay)
                         .ScaleTo(1.12f, 1050, Easing.OutQuint)
                         .Loop(560);
            pulseRings[i].FadeTo(0, 0)
                         .Delay(delay)
                         .FadeTo(0.7f, 100, Easing.OutQuint)
                         .Then().FadeOut(950, Easing.OutQuint)
                         .Loop(560);
        }

        core.ScaleTo(1.45f, 480, Easing.InOutSine)
            .Then().ScaleTo(0.8f, 480, Easing.InOutSine)
            .Then().ScaleTo(1f, 260, Easing.OutBack)
            .Loop(420);
    }
}

/// <summary>
/// 带移动扫描块、刻度与标签的小型遥测轨。
/// </summary>
public partial class HomeTelemetryRail : CompositeDrawable
{
    private readonly Box scanner;
    private readonly float travel;

    public HomeTelemetryRail(
        float width,
        LocalisableString label,
        Color4 colour,
        Color4 accent)
    {
        Width = width;
        Height = 34;
        travel = width - 34;

        AddInternal(new Box
        {
            Y = 5,
            Width = width,
            Height = 1.5f,
            Colour = new Color4(colour.R, colour.G, colour.B, 0.5f),
        });

        for (int i = 0; i <= 8; i++)
        {
            AddInternal(new Box
            {
                X = i / 8f * width,
                Y = i % 4 == 0 ? 1 : 3,
                Width = 1.5f,
                Height = i % 4 == 0 ? 9 : 5,
                Colour = new Color4(colour.R, colour.G, colour.B, 0.55f),
            });
        }

        AddInternal(scanner = new Box
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.CentreLeft,
            Y = 5.5f,
            Width = 34,
            Height = 3,
            Colour = accent,
        });
        AddInternal(new SpriteText
        {
            Y = 16,
            Text = label,
            Font = HomeTypography.Display(9),
            Spacing = new Vector2(1.4f, 0),
            Colour = new Color4(colour.R, colour.G, colour.B, 0.72f),
        });
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        scanner.MoveToX(travel, 1650, Easing.InOutSine)
               .Then().FadeOut(90)
               .MoveToX(0)
               .Then().FadeIn(90)
               .Loop();
    }
}
