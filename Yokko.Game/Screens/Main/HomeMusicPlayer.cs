using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace Yokko.Game.Screens.Main;

/// <summary>
/// 主页角落的紧凑音乐播放器，交互参考 osu!lazer 的 MusicController：
/// 上一首 / 播放暂停 / 下一首、进度条、播放时漂浮音符。
/// 目前项目内还没有音频资源，播放进度由内置演示曲目驱动（无声）；
/// 接入真实音频时，把 <see cref="currentProgress"/> 换成 ITrackStore 的
/// CurrentTime / Length，并在切歌时加载对应 Track 即可。
/// </summary>
public partial class HomeMusicPlayer : CompositeDrawable
{
    private sealed record DemoTrack(string Title, string Artist, double Length);

    private static readonly DemoTrack[] tracks =
    {
        new("Pulse Bloom", "Sana Kagano", 154_000),
        new("Neon Drift", "Yokko Sound Team", 128_000),
        new("Binary Bloom", "Ctrl+Beat", 141_000),
    };

    private int trackIndex;
    private bool isPlaying = true;
    private double resumedAt;
    private double progressBeforePause;

    private SpriteText titleText;
    private SpriteText artistText;
    private Box progressFill;
    private SpriteIcon albumIcon;
    private Container albumTile;
    private PlayerButton playPauseButton;

    public HomeMusicPlayer()
    {
        Size = new Vector2(452, 72);

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(-2, -2),
                Size = new Vector2(456, 76),
                Masking = true,
                CornerRadius = 16,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.28f),
                },
            },
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 14,
                BorderThickness = 2,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(1f, 1f, 1f, 0.97f),
                    },
                    new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Position = new Vector2(12, -5),
                        Size = new Vector2(428, 3),
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
                        Size = new Vector2(428, 3),
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
            albumTile = new Container
            {
                Position = new Vector2(12, 12),
                Size = new Vector2(48),
                Masking = true,
                CornerRadius = 10,
                BorderThickness = 2,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = HomeControlColours.Navy,
                    },
                    albumIcon = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(20),
                        Icon = FontAwesome.Solid.Music,
                        Colour = HomeControlColours.Cyan,
                    },
                },
            },
            titleText = new SpriteText
            {
                Position = new Vector2(74, 10),
                Text = tracks[0].Title,
                Font = HomeTypography.Display(19),
                Colour = HomeControlColours.Navy,
            },
            artistText = new SpriteText
            {
                Position = new Vector2(74, 36),
                Text = tracks[0].Artist,
                Font = HomeTypography.Body(13),
                Colour = new Color4(0.18f, 0.28f, 0.58f, 1f),
            },
            new PlayerButton(FontAwesome.Solid.StepBackward, previousTrack)
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
                X = -92,
            },
            new PlayerButton(FontAwesome.Solid.StepForward, nextTrack)
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
                X = -16,
            },
            playPauseButton = new PlayerButton(FontAwesome.Solid.Pause, togglePlayPause, isPrimary: true)
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
                X = -52,
                Size = new Vector2(38),
            },
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        resumedAt = Time.Current;

        // 播放中周期性飘出音符。
        Scheduler.AddDelayed(spawnNote, 1100, true);
    }

    private double currentProgress => isPlaying
        ? progressBeforePause + (Time.Current - resumedAt)
        : progressBeforePause;

    private void togglePlayPause()
    {
        if (isPlaying)
        {
            progressBeforePause = currentProgress;
            isPlaying = false;
            playPauseButton.Icon.Icon = FontAwesome.Solid.Play;
            albumIcon.FadeColour(Color4.White, 200);
        }
        else
        {
            resumedAt = Time.Current;
            isPlaying = true;
            playPauseButton.Icon.Icon = FontAwesome.Solid.Pause;
            albumIcon.FadeColour(HomeControlColours.Cyan, 200);
        }
    }

    private void nextTrack() => switchTrack((trackIndex + 1) % tracks.Length);

    private void previousTrack() => switchTrack((trackIndex + tracks.Length - 1) % tracks.Length);

    private void switchTrack(int index)
    {
        trackIndex = index;
        progressBeforePause = 0;
        resumedAt = Time.Current;

        titleText.Text = tracks[index].Title;
        artistText.Text = tracks[index].Artist;
        titleText.FadeInFromZero(260);
        artistText.FadeInFromZero(340);
        albumTile.ScaleTo(0.86f, 90, Easing.Out)
                 .Then().ScaleTo(1f, 220, Easing.OutBack);
    }

    private void spawnNote()
    {
        if (!isPlaying)
            return;

        var note = new SpriteIcon
        {
            Position = new Vector2(30, 4),
            Size = new Vector2(13),
            Icon = FontAwesome.Solid.Music,
            Colour = Color4.White,
            Alpha = 0.85f,
        };

        AddInternal(note);
        note.MoveToOffset(new Vector2(26, -52), 1600, Easing.OutQuad);
        note.RotateTo(22, 1600, Easing.OutQuad);
        note.FadeOut(1600, Easing.InQuad).Expire();
    }

    protected override void Update()
    {
        base.Update();

        double length = tracks[trackIndex].Length;
        if (isPlaying && currentProgress >= length)
            nextTrack();

        progressFill.Width = (float)Math.Min(1, currentProgress / length);

        if (isPlaying)
            albumIcon.Rotation = (float)(Time.Current / 1000 * 40 % 360);
    }

    /// <summary>
    /// 播放器专用小圆钮，主按钮为实心藏青底。
    /// </summary>
    private partial class PlayerButton : ClickableContainer
    {
        public readonly SpriteIcon Icon;
        public readonly bool IsPrimary;

        private readonly Box background;

        public PlayerButton(IconUsage icon, Action action, bool isPrimary = false)
        {
            Action = action;
            IsPrimary = isPrimary;
            Size = new Vector2(30);

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 8,
                    BorderThickness = isPrimary ? 0 : 1.5f,
                    BorderColour = new Color4(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.45f),
                    Children = new Drawable[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = isPrimary ? HomeControlColours.Navy : Color4.White,
                        },
                        Icon = new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(isPrimary ? 15 : 13),
                            Icon = icon,
                            Colour = isPrimary ? Color4.White : HomeControlColours.Navy,
                        },
                    },
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeColour(IsPrimary ? new Color4(0.055f, 0.15f, 0.7f, 1f) : HomeControlColours.PaleCyan, 120, Easing.OutQuint);
            this.ScaleTo(1.12f, 130, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeColour(IsPrimary ? HomeControlColours.Navy : Color4.White, 150, Easing.OutQuint);
            this.ScaleTo(1f, 150, Easing.OutQuint);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            this.ScaleTo(0.9f, 400, Easing.OutQuint);
            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            this.ScaleTo(IsHovered ? 1.12f : 1f, 220, Easing.OutQuint);
            base.OnMouseUp(e);
        }
    }
}
