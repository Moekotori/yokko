using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// Compact replay-only playback rail. Its pause and user-rate controls follow
/// ppy/osu's ReplayPlayer/PlaybackSettings at commit
/// 83b8a64bec19e1463353645c2d6d10c75e275b43 (MIT).
/// </summary>
internal partial class GameplayReplayControlsOverlay : CompositeDrawable
{
    private const float visual_scale = 0.72f;
    private static readonly Vector2 default_position = new(24);

    private static readonly YokkoBrandColourTokens colours =
        YokkoUiTheme.Default.Colours.Brand;
    private static readonly FontUsage cute_font = new(
        "Yokko",
        18,
        "Bold");
    private static readonly FontUsage replay_font = new(
        "ArchivoBlack",
        18);

    private readonly Action togglePause;
    private readonly Action seekBackward;
    private readonly Action seekForward;
    private readonly Action decreaseRate;
    private readonly Action increaseRate;
    private readonly Bindable<double> horizontalOffset;
    private readonly Bindable<double> verticalOffset;
    private readonly Action savePosition;
    private readonly SpriteIcon pauseIcon;
    private readonly SpriteText timeText;
    private readonly SpriteText rateText;
    private readonly GameplayReplayProgressBar progressBar;
    private readonly Sprite shell;
    private Vector2 dragStartPointer;
    private Vector2 dragStartPosition;
    private Vector2 lastParentDrawSize;
    private double lastAppliedHorizontalOffset = double.NaN;
    private double lastAppliedVerticalOffset = double.NaN;
    private bool draggingConsole;

    internal string TimeText => timeText.Text.ToString();
    internal string RateText => rateText.Text.ToString();
    internal bool ShowsPausedState { get; private set; }
    internal double DisplayedProgressMilliseconds =>
        progressBar.DisplayedMilliseconds;
    internal void PreviewProgressForTest(double progress) =>
        progressBar.BeginPreview(progress);
    internal void CommitProgressForTest() => progressBar.CommitPreview();
    internal void ActivateSeekBackward() => seekBackward();
    internal void ActivateSeekForward() => seekForward();
    internal void ActivateDecreaseRate() => decreaseRate();
    internal void ActivateIncreaseRate() => increaseRate();
    internal void ActivateTogglePause() => togglePause();

    public GameplayReplayControlsOverlay(
        Action togglePause,
        Action seekBackward,
        Action seekForward,
        Action<double> seekTo,
        Action decreaseRate,
        Action increaseRate,
        Bindable<double> horizontalOffset,
        Bindable<double> verticalOffset,
        Action savePosition)
    {
        this.togglePause = togglePause
                           ?? throw new ArgumentNullException(
                               nameof(togglePause));
        this.seekBackward = seekBackward
                            ?? throw new ArgumentNullException(
                                nameof(seekBackward));
        this.seekForward = seekForward
                           ?? throw new ArgumentNullException(
                               nameof(seekForward));
        ArgumentNullException.ThrowIfNull(seekTo);
        this.decreaseRate = decreaseRate
                            ?? throw new ArgumentNullException(
                                nameof(decreaseRate));
        this.increaseRate = increaseRate
                            ?? throw new ArgumentNullException(
                                nameof(increaseRate));
        this.horizontalOffset = horizontalOffset
                                ?? throw new ArgumentNullException(
                                    nameof(horizontalOffset));
        this.verticalOffset = verticalOffset
                              ?? throw new ArgumentNullException(
                                  nameof(verticalOffset));
        this.savePosition = savePosition
                            ?? throw new ArgumentNullException(
                                nameof(savePosition));

        Anchor = Anchor.TopLeft;
        Origin = Anchor.TopLeft;
        Position = default_position;
        Size = new Vector2(720, 193);
        Scale = new Vector2(visual_scale);
        Depth = -260;

        InternalChildren =
        [
            shell = new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                FillMode = FillMode.Stretch,
            },
            new SpriteText
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(116, 44),
                Text = "REPLAY",
                Font = replay_font.With(size: 26),
                Spacing = new Vector2(0.5f, 0),
                Colour = colours.Ink,
            },
            timeText = new SpriteText
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(171, 111),
                Text = "00:00 / 00:00",
                Font = cute_font.With(size: 24),
                Colour = colours.Ink,
            },
            createTextButton(
                new Vector2(280, 88),
                "−5",
                seekBackward),
            createButton(
                new Vector2(367, 88),
                FontAwesome.Solid.Pause,
                this.togglePause,
                new Vector2(86),
                Color4.White,
                out pauseIcon),
            createTextButton(
                new Vector2(447, 88),
                "+5",
                seekForward),
            createButton(
                new Vector2(534, 84),
                FontAwesome.Solid.Minus,
                this.decreaseRate,
                new Vector2(44),
                colours.Ink),
            rateText = new SpriteText
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(588, 84),
                Text = "1.00x",
                Font = cute_font.With(size: 25),
                Colour = colours.Ink,
            },
            createButton(
                new Vector2(642, 84),
                FontAwesome.Solid.Plus,
                this.increaseRate,
                new Vector2(44),
                colours.Ink),
            progressBar = new GameplayReplayProgressBar(seekTo)
            {
                Position = new Vector2(70, 128),
                Size = new Vector2(580, 40),
            },
        ];
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        shell.Texture = textures.Get("Gameplay/replay-controller-shell");
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        applySavedPosition();
    }

    protected override void Update()
    {
        base.Update();

        if (draggingConsole || Parent == null)
            return;

        Vector2 parentDrawSize = Parent.DrawSize;
        if (parentDrawSize != lastParentDrawSize
            || horizontalOffset.Value != lastAppliedHorizontalOffset
            || verticalOffset.Value != lastAppliedVerticalOffset)
        {
            applySavedPosition();
        }
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left || Parent == null)
            return false;

        dragStartPointer = Parent.ToLocalSpace(e.ScreenSpaceMousePosition);
        dragStartPosition = Position;
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e)
    {
        draggingConsole = Parent != null;
        return draggingConsole;
    }

    protected override void OnDrag(DragEvent e)
    {
        if (!draggingConsole || Parent == null)
            return;

        Vector2 pointer = Parent.ToLocalSpace(e.ScreenSpaceMousePosition);
        Position = clampPosition(
            dragStartPosition + pointer - dragStartPointer);
    }

    protected override void OnDragEnd(DragEndEvent e)
    {
        if (draggingConsole)
        {
            draggingConsole = false;
            persistPosition();
        }

        base.OnDragEnd(e);
    }

    private void applySavedPosition()
    {
        if (Parent == null)
            return;

        lastParentDrawSize = Parent.DrawSize;
        lastAppliedHorizontalOffset = horizontalOffset.Value;
        lastAppliedVerticalOffset = verticalOffset.Value;
        Position = clampPosition(new Vector2(
            default_position.X
            + (float)(horizontalOffset.Value * Parent.DrawWidth),
            default_position.Y
            + (float)(verticalOffset.Value * Parent.DrawHeight)));
    }

    private Vector2 clampPosition(Vector2 position)
    {
        if (Parent == null)
            return position;

        float maximumHorizontal = Math.Max(
            0,
            Parent.DrawWidth - Size.X * Scale.X);
        float maximumVertical = Math.Max(
            0,
            Parent.DrawHeight - Size.Y * Scale.Y);
        return new Vector2(
            Math.Clamp(position.X, 0, maximumHorizontal),
            Math.Clamp(position.Y, 0, maximumVertical));
    }

    private void persistPosition()
    {
        if (Parent == null)
            return;

        horizontalOffset.Value =
            (Position.X - default_position.X)
            / Math.Max(1, Parent.DrawWidth);
        verticalOffset.Value =
            (Position.Y - default_position.Y)
            / Math.Max(1, Parent.DrawHeight);
        lastAppliedHorizontalOffset = horizontalOffset.Value;
        lastAppliedVerticalOffset = verticalOffset.Value;
        savePosition();
    }

    private static GameplayReplayTextButton createTextButton(
        Vector2 position,
        string text,
        Action action) => new(text, action)
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.Centre,
            Position = position,
        };

    internal void UpdateState(
        double currentMilliseconds,
        double durationMilliseconds,
        double playbackRate,
        bool paused)
    {
        currentMilliseconds = Math.Clamp(
            currentMilliseconds,
            0,
            Math.Max(0, durationMilliseconds));
        durationMilliseconds = Math.Max(0, durationMilliseconds);
        double displayedMilliseconds = progressBar.UpdateState(
            currentMilliseconds,
            durationMilliseconds);
        timeText.Text = $"{formatTime(displayedMilliseconds)} / "
                        + formatTime(durationMilliseconds);
        rateText.Text = $"{playbackRate:0.00}x";
        ShowsPausedState = paused;
        pauseIcon.Icon = paused
            ? FontAwesome.Solid.Play
            : FontAwesome.Solid.Pause;
    }

    internal void CompleteSeekPreview(double currentMilliseconds) =>
        progressBar.CompleteSeek(currentMilliseconds);

    internal void CancelSeekPreview() => progressBar.CancelPreview();

    private static GameplayReplayControlButton createButton(
        Vector2 position,
        IconUsage icon,
        Action action,
        Vector2 size,
        Color4 colour) => createButton(
        position,
        icon,
        action,
        size,
        colour,
        out _);

    private static GameplayReplayControlButton createButton(
        Vector2 position,
        IconUsage icon,
        Action action,
        Vector2 size,
        Color4 colour,
        out SpriteIcon spriteIcon)
    {
        var button = new GameplayReplayControlButton(
            icon,
            action,
            size,
            colour)
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.Centre,
            Position = position,
        };
        spriteIcon = button.Icon;
        return button;
    }

    private static string formatTime(double milliseconds)
    {
        var duration = TimeSpan.FromMilliseconds(milliseconds);
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }
}

internal partial class GameplayReplayProgressBar : CompositeDrawable
{
    private const float track_padding = 4;

    private readonly Action<double> seekCommitted;
    private readonly Box track;
    private readonly Box fill;
    private readonly Sprite marker;
    private double durationMilliseconds;
    private double currentMilliseconds;
    private double previewMilliseconds;
    private double committedPreviewMilliseconds = double.NaN;
    private bool pressed;

    internal double DisplayedMilliseconds => pressed
        ? previewMilliseconds
        : double.IsFinite(committedPreviewMilliseconds)
            ? committedPreviewMilliseconds
            : currentMilliseconds;

    public GameplayReplayProgressBar(Action<double> seekCommitted)
    {
        this.seekCommitted = seekCommitted
                             ?? throw new ArgumentNullException(
                                 nameof(seekCommitted));
        InternalChildren =
        [
            track = new Box
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = track_padding,
                Height = 6,
                Alpha = 0,
            },
            fill = new Box
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = track_padding,
                Height = 6,
                Colour = YokkoUiTheme.Default.Colours.Brand.Cyan,
            },
            marker = new Sprite
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                Size = new Vector2(46, 42),
                FillMode = FillMode.Fit,
            },
        ];
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        marker.Texture = textures.Get("Gameplay/replay-progress-heart");
    }

    internal double UpdateState(
        double currentMilliseconds,
        double durationMilliseconds)
    {
        this.durationMilliseconds = Math.Max(0, durationMilliseconds);
        this.currentMilliseconds = Math.Clamp(
            currentMilliseconds,
            0,
            this.durationMilliseconds);
        updateVisual();
        return DisplayedMilliseconds;
    }

    internal void BeginPreview(double progress)
    {
        pressed = true;
        committedPreviewMilliseconds = double.NaN;
        previewMilliseconds = Math.Clamp(progress, 0, 1)
                              * durationMilliseconds;
        updateVisual();
    }

    internal void CommitPreview()
    {
        if (!pressed)
            return;

        pressed = false;
        committedPreviewMilliseconds = previewMilliseconds;
        updateVisual();
        seekCommitted(previewMilliseconds);
    }

    internal void CompleteSeek(double currentMilliseconds)
    {
        committedPreviewMilliseconds = double.NaN;
        this.currentMilliseconds = Math.Clamp(
            currentMilliseconds,
            0,
            durationMilliseconds);
        updateVisual();
    }

    internal void CancelPreview()
    {
        if (!pressed)
            return;

        pressed = false;
        previewMilliseconds = currentMilliseconds;
        marker.ScaleTo(IsHovered ? 1.12f : 1, 100, Easing.OutQuint);
        updateVisual();
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left)
            return false;

        BeginPreview(progressAt(e.ScreenSpaceMousePosition));
        marker.ScaleTo(1.25f, 80, Easing.OutQuint);
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e) => pressed;

    protected override void OnDrag(DragEvent e) =>
        BeginPreview(progressAt(e.ScreenSpaceMousePosition));

    protected override void OnMouseUp(MouseUpEvent e)
    {
        if (e.Button == MouseButton.Left)
            CommitPreview();

        marker.ScaleTo(IsHovered ? 1.12f : 1, 100, Easing.OutQuint);
        base.OnMouseUp(e);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!pressed)
            marker.ScaleTo(1.12f, 90, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        if (!pressed)
            marker.ScaleTo(1, 100, Easing.OutQuint);
    }

    private double progressAt(Vector2 screenPosition)
    {
        float trackWidth = Math.Max(1, DrawWidth - track_padding * 2);
        return Math.Clamp(
            (ToLocalSpace(screenPosition).X - track_padding) / trackWidth,
            0,
            1);
    }

    private void updateVisual()
    {
        double progress = durationMilliseconds <= 0
            ? 0
            : Math.Clamp(
                DisplayedMilliseconds / durationMilliseconds,
                0,
                1);
        float trackWidth = Math.Max(1, DrawWidth - track_padding * 2);
        float x = track_padding + (float)progress * trackWidth;
        track.Width = trackWidth;
        fill.Width = Math.Max(0, x - track_padding);
        marker.X = x;
    }
}

internal partial class GameplayReplayControlButton : ClickableContainer
{
    internal SpriteIcon Icon { get; }
    private readonly Color4 iconColour;

    public GameplayReplayControlButton(
        IconUsage icon,
        Action action,
        Vector2 size,
        Color4 iconColour)
    {
        this.iconColour = iconColour;
        Action = action;
        Size = size;
        Child = Icon = new SpriteIcon
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = size.X >= 80 ? new Vector2(40) : new Vector2(22),
            Icon = icon,
            Colour = iconColour,
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        Icon.FadeColour(YokkoUiTheme.Default.Colours.Brand.Pink, 100);
        this.ScaleTo(1.05f, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        Icon.FadeColour(iconColour, 120);
        this.ScaleTo(1, 120, Easing.OutQuint);
    }
}

internal partial class GameplayReplayTextButton : ClickableContainer
{
    private static readonly Color4 ink = YokkoUiTheme.Default.Colours.Brand.Ink;
    private readonly SpriteText text;

    public GameplayReplayTextButton(string label, Action action)
    {
        Action = action;
        Size = new Vector2(58, 58);
        Child = text = new SpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Text = label,
            Font = new FontUsage(
                "Yokko",
                32,
                "Bold"),
            Colour = ink,
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        text.FadeColour(YokkoUiTheme.Default.Colours.Brand.Pink, 100);
        this.ScaleTo(1.05f, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        text.FadeColour(ink, 120);
        this.ScaleTo(1, 120, Easing.OutQuint);
    }
}
