using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Beatmaps;
using Yokko.Core.Editing;
using Yokko.Core.Timing;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Editor;

public partial class EditorSignalStrip : CompositeDrawable
{
    private const int barCount = 96;

    private readonly EditableBeatmap beatmap;
    private readonly TimelineViewport viewport;
    private readonly Func<EditorAudioWaveform> waveformProvider;
    private readonly Action<double> seekPreview;
    private Box playheadLine;

    public EditorSignalStrip(
        EditableBeatmap beatmap,
        TimelineViewport viewport,
        Func<EditorAudioWaveform> waveformProvider,
        Action<double> seekPreview)
    {
        this.beatmap = beatmap;
        this.viewport = viewport;
        this.waveformProvider = waveformProvider;
        this.seekPreview = seekPreview;

        Width = beatmap.LaneCount == 4 ? 500 : 760;
        Height = 70;
        Masking = true;
        CornerRadius = 6;

        Refresh();
    }

    public void Refresh()
    {
        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.035f, 0.045f, 0.064f, 1f),
            },
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = createBeatMarkers(),
            },
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = createBars(),
            },
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = createScrollVelocityTrack(),
            },
            playheadLine = new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 2,
                Alpha = 0,
                Colour = YokkoPalette.Lime,
            },
            new SpriteText
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                X = 12,
                Y = 8,
                Text = $"{formatSeconds(beatmap.TimeAtRow(viewport.StartRow))} - {formatSeconds(beatmap.TimeAtRow(viewport.EndRowExclusive))}",
                Font = FontUsage.Default.With(size: 14),
                Colour = YokkoPalette.TextMuted,
            },
            new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                X = -12,
                Y = 8,
                Text = waveformProvider().Label,
                Font = FontUsage.Default.With(size: 13),
                Colour = waveformProvider().HasAudio ? YokkoPalette.Lime : YokkoPalette.TextDim,
            },
        };
    }

    public void SetPlayheadTime(double timeMilliseconds)
    {
        double startMilliseconds = beatmap.TimeAtRow(viewport.StartRow);
        double endMilliseconds = beatmap.TimeAtRow(viewport.EndRowExclusive);

        if (timeMilliseconds < startMilliseconds || timeMilliseconds > endMilliseconds)
        {
            playheadLine.Alpha = 0;
            return;
        }

        double progress = (timeMilliseconds - startMilliseconds) / Math.Max(1, endMilliseconds - startMilliseconds);
        playheadLine.X = Math.Clamp(12 + (float)progress * (Width - 24), 12, Width - 12);
        playheadLine.Alpha = 0.95f;
    }

    private Drawable[] createBeatMarkers()
    {
        var markers = new List<Drawable>();

        for (int row = viewport.StartRow; row < viewport.EndRowExclusive; row++)
        {
            if (!beatmap.TimingMap.IsBeatRow(row))
                continue;

            float x = rowToX(row);
            bool strong = beatmap.TimingMap.IsMeasureRow(row);

            markers.Add(new Box
            {
                X = x,
                Width = strong ? 2 : 1,
                RelativeSizeAxes = Axes.Y,
                Colour = new Color4(1f, 1f, 1f, strong ? 0.16f : 0.08f),
            });
        }

        return markers.ToArray();
    }

    private Drawable[] createBars()
    {
        EditorAudioWaveform waveform = waveformProvider();
        float[] notePeaks = createNotePeaks();
        var bars = new Drawable[barCount];
        float usableWidth = Width - 24;
        float stepWidth = usableWidth / barCount;
        float barWidth = Math.Max(2, stepWidth - 2);
        float baseY = Height - 12;
        double startMilliseconds = beatmap.TimeAtRow(viewport.StartRow);
        double endMilliseconds = beatmap.TimeAtRow(viewport.EndRowExclusive);
        double windowMilliseconds = Math.Max(1, endMilliseconds - startMilliseconds);
        double fallbackDuration = Math.Max(beatmap.TimeAtRow(beatmap.Rows), endMilliseconds);

        for (int i = 0; i < barCount; i++)
        {
            double segmentStart = startMilliseconds + i / (double)barCount * windowMilliseconds;
            double segmentEnd = startMilliseconds + (i + 1) / (double)barCount * windowMilliseconds;
            EditorWaveformSample audioSample = waveform.Sample(segmentStart, segmentEnd, fallbackDuration);
            float notePeak = notePeaks[i];
            float peak = waveform.HasAudio
                ? Math.Clamp(audioSample.Peak * 0.9f + notePeak * 0.22f, 0.03f, 1f)
                : notePeak;
            float barHeight = 6 + peak * 43;
            Color4 colour = createBarColour(audioSample, notePeak, waveform.HasAudio);

            bars[i] = new Box
            {
                X = 12 + i * stepWidth + (stepWidth - barWidth) / 2,
                Y = baseY - barHeight,
                Width = barWidth,
                Height = barHeight,
                Colour = colour,
            };
        }

        return bars;
    }

    private Drawable[] createScrollVelocityTrack()
    {
        double startMilliseconds = beatmap.TimeAtRow(viewport.StartRow);
        double endMilliseconds = beatmap.TimeAtRow(viewport.EndRowExclusive);
        var map = new ScrollVelocityMap(
            beatmap.ScrollVelocities,
            beatmap.InitialScrollVelocity);
        var drawables = new List<Drawable>();
        YokkoScrollVelocity[] visibleChanges = beatmap.ScrollVelocities
                                                         .Where(velocity =>
                                                             velocity.TimeMilliseconds > startMilliseconds
                                                             && velocity.TimeMilliseconds < endMilliseconds)
                                                         .OrderBy(static velocity => velocity.TimeMilliseconds)
                                                         .ToArray();
        double segmentStart = startMilliseconds;
        double multiplier = map.MultiplierAt(segmentStart);

        foreach (YokkoScrollVelocity change in visibleChanges)
        {
            addSegment(segmentStart, change.TimeMilliseconds, multiplier);
            addChangeMarker(change.TimeMilliseconds, change.Multiplier);
            segmentStart = change.TimeMilliseconds;
            multiplier = change.Multiplier;
        }

        addSegment(segmentStart, endMilliseconds, multiplier);
        return drawables.ToArray();

        void addSegment(double start, double end, double value)
        {
            float startX = timeToX(start, startMilliseconds, endMilliseconds);
            float endX = timeToX(end, startMilliseconds, endMilliseconds);

            drawables.Add(new Box
            {
                X = startX,
                Y = Height - 6,
                Width = Math.Max(1, endX - startX),
                Height = 4,
                Colour = scrollVelocityColour(value, 0.82f),
            });
        }

        void addChangeMarker(double time, double value)
        {
            drawables.Add(new Box
            {
                X = timeToX(time, startMilliseconds, endMilliseconds),
                Y = 28,
                Width = 2,
                Height = Height - 32,
                Colour = scrollVelocityColour(value, 0.92f),
            });
        }
    }

    private static Color4 createBarColour(EditorWaveformSample sample, float notePeak, bool hasAudio)
    {
        if (!hasAudio)
        {
            return notePeak > 0.48f
                ? new Color4(1f, 0.42f, 0.52f, 0.9f)
                : new Color4(0.2f, 0.88f, 0.95f, 0.68f);
        }

        float low = Math.Clamp(sample.Low, 0, 1);
        float mid = Math.Clamp(sample.Mid, 0, 1);
        float high = Math.Clamp(sample.High, 0, 1);
        float noteGlow = Math.Clamp(notePeak, 0, 1);

        return new Color4(
            0.18f + high * 0.62f + noteGlow * 0.2f,
            0.58f + low * 0.28f,
            0.78f + mid * 0.2f,
            0.72f + noteGlow * 0.18f);
    }

    private float[] createNotePeaks()
    {
        var peaks = new float[barCount];
        double startMilliseconds = beatmap.TimeAtRow(viewport.StartRow);
        double endMilliseconds = beatmap.TimeAtRow(viewport.EndRowExclusive);
        double windowMilliseconds = Math.Max(1, endMilliseconds - startMilliseconds);

        foreach (EditableNote note in beatmap.Notes)
        {
            if (note.StartTimeMilliseconds < startMilliseconds || note.StartTimeMilliseconds >= endMilliseconds)
                continue;

            int index = Math.Clamp((int)((note.StartTimeMilliseconds - startMilliseconds) / windowMilliseconds * barCount), 0, barCount - 1);
            peaks[index] += note.Kind == HitObjectKind.Hold ? 0.46f : 0.34f;
        }

        for (int i = 0; i < peaks.Length; i++)
        {
            float neighbourEnergy = Math.Max(
                i > 0 ? peaks[i - 1] : 0,
                i < peaks.Length - 1 ? peaks[i + 1] : 0);
            float idleShape = 0.08f + (float)Math.Sin(i * 0.42f + beatmap.LaneCount) * 0.025f;

            peaks[i] = Math.Clamp(idleShape + peaks[i] + neighbourEnergy * 0.25f, 0.06f, 0.95f);
        }

        return peaks;
    }

    private float rowToX(int row)
    {
        float progress = (row - viewport.StartRow) / (float)viewport.VisibleRows;
        return Math.Clamp(12 + progress * (Width - 24), 12, Width - 12);
    }

    private float timeToX(
        double timeMilliseconds,
        double startMilliseconds,
        double endMilliseconds)
    {
        double progress = (timeMilliseconds - startMilliseconds)
                          / Math.Max(1, endMilliseconds - startMilliseconds);
        return Math.Clamp(
            12 + (float)progress * (Width - 24),
            12,
            Width - 12);
    }

    private static Color4 scrollVelocityColour(
        double multiplier,
        float alpha)
    {
        if (multiplier < 0)
            return new Color4(1f, 0.32f, 0.52f, alpha);

        if (multiplier == 0)
            return new Color4(1f, 0.72f, 0.2f, alpha);

        float intensity = Math.Clamp(
            0.52f + (float)Math.Log2(Math.Max(0.125, multiplier)) * 0.09f,
            0.25f,
            0.95f);
        return new Color4(
            0.18f,
            0.68f + intensity * 0.25f,
            0.82f + intensity * 0.16f,
            alpha);
    }

    private static string formatSeconds(double milliseconds) => $"{milliseconds / 1000:0.00}s";

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        seekFromScreenSpace(e.ScreenSpaceMousePosition);
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e)
        => true;

    protected override void OnDrag(DragEvent e)
    {
        seekFromScreenSpace(e.ScreenSpaceMousePosition);
    }

    private void seekFromScreenSpace(Vector2 screenSpacePosition)
    {
        Vector2 localPosition = ToLocalSpace(screenSpacePosition);
        float progress = Math.Clamp((localPosition.X - 12) / Math.Max(1, Width - 24), 0, 1);
        double startMilliseconds = beatmap.TimeAtRow(viewport.StartRow);
        double endMilliseconds = beatmap.TimeAtRow(viewport.EndRowExclusive);

        seekPreview(startMilliseconds + progress * (endMilliseconds - startMilliseconds));
    }
}
