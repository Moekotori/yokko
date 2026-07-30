using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Gameplay;
using Yokko.Game.Gameplay;
using Yokko.Game.Screens.Gameplay;

namespace Yokko.Game.Screens.Main;

/// <summary>
/// 铺在舞台最底层的点击涟漪层：点击空白处荡开圆环并溅出碎点。
/// 永远返回 false，不拦截按钮与播放器等上方控件的输入。
/// </summary>
public partial class HomeTapRippleLayer : Container
{
    // 大致的象牙色左面板分界，用来挑选在底色上清晰的涟漪颜色。
    private const float ivory_edge_x = 560;

    private static readonly Color4 ivoryRing = new(
        HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.55f);

    public HomeTapRippleLayer()
    {
        RelativeSizeAxes = Axes.Both;
    }

    public override bool HandlePositionalInput => true;

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button == MouseButton.Left)
            spawnRipple(ToLocalSpace(e.ScreenSpaceMousePosition));

        return false;
    }

    private void spawnRipple(Vector2 position)
    {
        bool onIvory = position.X < ivory_edge_x;
        Color4 ringColour = onIvory ? ivoryRing : new Color4(1f, 1f, 1f, 0.85f);
        Color4 sparkColour = onIvory ? HomeControlColours.Pink : HomeControlColours.Yellow;

        var ring = new Container
        {
            Origin = Anchor.Centre,
            Position = position,
            Size = new Vector2(14),
            Masking = true,
            CornerRadius = 7,
            BorderThickness = 2.5f,
            BorderColour = ringColour,
            Alpha = 0,
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                // 子节点 Alpha 为 0 会被剔除并连带描边消失，给趋近 0 的值保住描边。
                Alpha = 0.01f,
            },
        };

        Add(ring);
        ring.FadeTo(1f, 50)
            .ResizeTo(74, 520, Easing.OutQuint);
        ring.Delay(120).FadeOut(400, Easing.OutQuint).Expire();

        for (int i = 0; i < 4; i++)
        {
            float angle = MathF.PI / 4 + i * MathF.PI / 2;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

            var spark = new Circle
            {
                Origin = Anchor.Centre,
                Position = position,
                Size = new Vector2(4),
                Colour = sparkColour,
            };

            Add(spark);
            spark.MoveTo(position + direction * 26, 380, Easing.OutQuint)
                 .FadeOut(380, Easing.InQuart)
                 .Expire();
        }
    }
}

/// <summary>
/// 顶缘的滚动字幕带，循环展示工作室微文案；悬停时几乎停住，方便阅读。
/// </summary>
public partial class HomeMarqueeTicker : CompositeDrawable
{
    private const float scroll_speed = 34;

    private readonly FillFlowContainer track;
    private readonly FillFlowContainer firstSegment;
    private readonly Box band;
    private float segmentWidth;
    private float speedScale = 1;
    private bool hovering;

    public HomeMarqueeTicker()
    {
        RelativeSizeAxes = Axes.X;
        Height = 18;
        Masking = true;

        InternalChildren = new Drawable[]
        {
            band = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.78f),
            },
            track = new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Children = new Drawable[]
                {
                    firstSegment = createSegment(),
                    createSegment(),
                },
            },
        };
    }

    public override bool HandlePositionalInput => true;

    private static FillFlowContainer createSegment()
    {
        SpriteText createText(string text) => new()
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Text = text,
            Font = HomeTypography.Display(9),
            Spacing = new Vector2(1.8f, 0),
            Colour = new Color4(1f, 1f, 1f, 0.82f),
        };

        SpriteIcon createStar(Color4 colour) => new()
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Size = new Vector2(8),
            Icon = FontAwesome.Solid.Star,
            Colour = colour,
        };

        return new FillFlowContainer
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(16, 0),
            Children = new Drawable[]
            {
                createStar(HomeControlColours.Pink),
                createText("YOKKO RHYTHM STATION // 4K MANIA"),
                createStar(HomeControlColours.Yellow),
                createText("CHART LAB // FEEL THE BEAT"),
                createStar(new Color4(1f, 1f, 1f, 0.7f)),
                createText("RHYTHM CHART STUDIO // EST. 2025 // VOL.01"),
                new Box
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(26, 2),
                    Alpha = 0,
                },
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        hovering = true;
        band.FadeColour(new Color4(0.07f, 0.15f, 0.72f, 0.92f), 180, Easing.OutQuint);
        return base.OnHover(e);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        hovering = false;
        band.FadeColour(new Color4(
            HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.78f), 240, Easing.OutQuint);
        base.OnHoverLost(e);
    }

    protected override void Update()
    {
        base.Update();

        if (segmentWidth <= 0)
        {
            segmentWidth = firstSegment.DrawWidth;
            if (segmentWidth <= 0)
                return;
        }

        float target = hovering ? 0.08f : 1f;
        float blend = 1f - MathF.Exp((float)(-Clock.ElapsedFrameTime / 160));
        speedScale = float.Lerp(speedScale, target, blend);

        track.X -= scroll_speed * speedScale * (float)(Clock.ElapsedFrameTime / 1000);
        if (track.X <= -segmentWidth)
            track.X += segmentWidth;
    }
}

/// <summary>
/// 4K 键位试玩盘：点击或按下 D/F/J/K 点亮键帽，上方判定线闪烁并累计敲击数。
/// </summary>
public partial class HomeKeyTestPad : CompositeDrawable
{
    private const float cap_pitch = 34;
    private const double combo_window_milliseconds = 1200;
    private const int combo_display_threshold = 4;

    private static readonly Key[] laneKeys = { Key.D, Key.F, Key.J, Key.K };
    private static readonly string[] laneLabels = { "D", "F", "J", "K" };
    private static readonly Color4 hintColour = new(1f, 1f, 1f, 0.65f);
    private static readonly Color4[] laneAccents =
    {
        HomeControlColours.Pink,
        HomeControlColours.Yellow,
        HomeControlColours.Yellow,
        HomeControlColours.Pink,
    };

    private readonly HomeKeycap[] caps = new HomeKeycap[4];
    private readonly Box[] lineSegments = new Box[4];
    private readonly SpriteText hitCounter;
    private readonly SpriteText hintText;
    private int hitCount;
    private int comboCount;
    private double lastHitTime = double.MinValue;

    public int HitCount => hitCount;

    internal int ComboCount => comboCount;

    public HomeKeyTestPad()
    {
        Size = new Vector2(150, 74);

        // 错位阴影层，与其他卡片同一套贴纸语言。
        AddInternal(new Container
        {
            Position = new Vector2(-5, -4),
            Size = new Vector2(166, 88),
            Masking = true,
            CornerRadius = 10,
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.38f),
            },
        });

        // 主表面：深海军蓝 + 浅色描边，在象牙与青色背景上都清晰。
        AddInternal(new Container
        {
            Position = new Vector2(-8, -8),
            Size = new Vector2(166, 88),
            Masking = true,
            CornerRadius = 10,
            BorderThickness = 2,
            BorderColour = new Color4(1f, 1f, 1f, 0.75f),
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.86f),
            },
        });

        // 角落装饰：黄色角标 + 粉色小点，呼应按钮卡片。
        AddInternal(new Box
        {
            Position = new Vector2(152, -8),
            Size = new Vector2(7),
            Rotation = 45,
            Colour = HomeControlColours.Yellow,
        });
        AddInternal(new Circle
        {
            Position = new Vector2(-8, 74),
            Size = new Vector2(5),
            Colour = HomeControlColours.Pink,
        });

        AddInternal(new SpriteIcon
        {
            Position = new Vector2(1, 1),
            Size = new Vector2(10),
            Icon = FontAwesome.Solid.Keyboard,
            Colour = new Color4(1f, 1f, 1f, 0.78f),
        });
        AddInternal(new SpriteText
        {
            Position = new Vector2(17, 1),
            Text = "KEY TEST",
            Font = HomeTypography.Display(9),
            Spacing = new Vector2(1.6f, 0),
            Colour = new Color4(1f, 1f, 1f, 0.78f),
        });
        AddInternal(hitCounter = new SpriteText
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Y = 1,
            Text = "HITS 0000",
            Font = HomeTypography.Display(9),
            Spacing = new Vector2(1.2f, 0),
            Colour = new Color4(
                HomeControlColours.Yellow.R, HomeControlColours.Yellow.G, HomeControlColours.Yellow.B, 0.92f),
        });

        // 判定线分段：每按一次对应分段闪一下。
        for (int i = 0; i < 4; i++)
        {
            AddInternal(lineSegments[i] = new Box
            {
                Position = new Vector2(i * cap_pitch, 20),
                Size = new Vector2(26, 3),
                Colour = laneAccents[i],
                Alpha = 0.3f,
            });
        }

        for (int i = 0; i < 4; i++)
        {
            int lane = i;
            AddInternal(caps[i] = new HomeKeycap(laneLabels[i])
            {
                Position = new Vector2(i * cap_pitch, 30),
                Action = () =>
                {
                    PressLane(lane);
                    Scheduler.AddDelayed(() => caps[lane].SetPressed(false), 110);
                },
            });
        }

        AddInternal(hintText = new SpriteText
        {
            Position = new Vector2(0, 60),
            Text = "TAP OR PRESS KEYS",
            Font = HomeTypography.Display(8),
            Spacing = new Vector2(1.4f, 0),
            Colour = hintColour,
        });
    }

    /// <summary>
    /// 尝试把一次键盘输入映射到某个键道；命中返回 true。
    /// </summary>
    public bool TryHandleKey(Key key, bool pressed)
    {
        int lane = Array.IndexOf(laneKeys, key);
        if (lane < 0)
            return false;

        if (pressed)
            PressLane(lane);
        else
            ReleaseLane(lane);

        return true;
    }

    public void PressLane(int lane)
    {
        caps[lane].SetPressed(true);

        lineSegments[lane].FadeTo(1f, 30)
                          .Then()
                          .FadeTo(0.3f, 280, Easing.OutQuint);

        var note = new Circle
        {
            Origin = Anchor.Centre,
            Position = new Vector2(lane * cap_pitch + 13, 28),
            Size = new Vector2(5),
            Colour = laneAccents[lane],
        };
        AddInternal(note);
        note.MoveToOffset(new Vector2(0, -26), 300, Easing.OutQuint)
            .FadeOut(300, Easing.InQuart)
            .Expire();

        hitCount++;
        hitCounter.Text = $"HITS {hitCount % 10000:0000}";

        double now = Clock.CurrentTime;
        comboCount = now - lastHitTime <= combo_window_milliseconds
            ? comboCount + 1
            : 1;
        lastHitTime = now;

        if (comboCount % 10 == 0)
            playComboMilestone();

        updateComboHint();
    }

    public void ReleaseLane(int lane) => caps[lane].SetPressed(false);

    protected override void Update()
    {
        base.Update();

        if (comboCount > 0
            && Clock.CurrentTime - lastHitTime > combo_window_milliseconds)
        {
            comboCount = 0;
            updateComboHint();
        }
    }

    private void updateComboHint()
    {
        if (comboCount >= combo_display_threshold)
        {
            hintText.Text = $"COMBO x{comboCount}";
            hintText.Colour = HomeControlColours.Pink;
            return;
        }

        hintText.Text = "TAP OR PRESS KEYS";
        hintText.Colour = hintColour;
    }

    /// <summary>
    /// 每 10 连击庆祝一次：盘体轻弹、计数器闪粉、向上迸发星星。
    /// </summary>
    private void playComboMilestone()
    {
        this.ScaleTo(1.06f, 90, Easing.Out)
            .Then().ScaleTo(1f, 320, Easing.OutBack);
        hitCounter.FlashColour(HomeControlColours.Pink, 320, Easing.OutQuint);

        for (int i = 0; i < 5; i++)
        {
            float angle = -MathF.PI / 2 + (i - 2) * 0.42f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

            var star = new SpriteIcon
            {
                Origin = Anchor.Centre,
                Position = new Vector2(75, 10),
                Size = new Vector2(11),
                Icon = FontAwesome.Solid.Star,
                Colour = i % 2 == 0 ? HomeControlColours.Yellow : Color4.White,
            };

            AddInternal(star);
            star.MoveToOffset(direction * 46, 520, Easing.OutQuint);
            star.RotateTo(120, 520);
            star.FadeOut(520, Easing.InQuart).Expire();
        }
    }
}

/// <summary>
/// 悬停时会放大旋转的装饰小图标；环境呼吸动效作用于内部 Icon，互不干扰。
/// Position 视为中心。
/// </summary>
public partial class HomeSparkIcon : CompositeDrawable
{
    public readonly SpriteIcon Icon;

    public HomeSparkIcon(IconUsage icon, float size, Color4 colour)
    {
        Size = new Vector2(size);
        Origin = Anchor.Centre;

        InternalChild = Icon = new SpriteIcon
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = new Vector2(size),
            Icon = icon,
            Colour = colour,
            Alpha = 0.9f,
        };
    }

    public override bool HandlePositionalInput => true;

    protected override bool OnHover(HoverEvent e)
    {
        this.ScaleTo(1.45f, 260, Easing.OutBack)
            .RotateTo(90, 300, Easing.OutQuint);
        return base.OnHover(e);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.ScaleTo(1f, 300, Easing.OutQuint)
            .RotateTo(0, 340, Easing.OutQuint);
        base.OnHoverLost(e);
    }
}
