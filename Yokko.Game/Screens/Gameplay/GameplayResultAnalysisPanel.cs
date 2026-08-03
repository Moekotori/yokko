using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Scoring;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Gameplay;

internal partial class GameplayResultAnalysisPanel : CompositeDrawable
{
    private static readonly Color4 navy = new(7 / 255f, 27 / 255f, 120 / 255f, 1);
    private static readonly Color4 cyan = new(98 / 255f, 216 / 255f, 248 / 255f, 1);
    private static readonly Color4 pink = new(255 / 255f, 99 / 255f, 199 / 255f, 1);
    private static readonly Color4 yellow = new(255 / 255f, 230 / 255f, 111 / 255f, 1);
    private readonly IReadOnlyList<GameplayTimingSample> samples;
    private readonly Func<double, ManiaScoreResult> rejudge;
    private readonly Container plot;
    private readonly SpriteText laneText;
    private readonly SpriteText offsetText;
    private readonly SpriteText predictionText;
    private int laneFilter = -1;
    private double offset;

    internal int DisplayedSampleCount { get; private set; }
    internal double PreviewOffset => offset;
    internal string Prediction => predictionText.Text.ToString();

    internal GameplayResultAnalysisPanel(
        GameplayTimingStatistics timing,
        Func<double, ManiaScoreResult> rejudge,
        string practiceSummary = null)
    {
        samples = timing?.Samples ?? [];
        this.rejudge = rejudge;
        Size = new Vector2(660, 760);
        Masking = true;
        CornerRadius = 18;
        BorderThickness = 2;
        BorderColour = new Color4(navy.R, navy.G, navy.B, 0.16f);

        InternalChildren =
        [
            new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(1, 253 / 255f, 247 / 255f, 0.96f) },
            new SpriteText { Position = new Vector2(28, 24), Text = "TIMING LAB", Font = HomeTypography.Hero(38), Colour = navy },
            new SpriteText { Position = new Vector2(30, 73), Text = "TIME  ×  INPUT ERROR  //  OFFSET REJUDGE", Font = HomeTypography.Display(10), Colour = navy, Alpha = 0.58f },
            createButton("LANE", new Vector2(30, 108), cycleLane),
            laneText = new SpriteText { Position = new Vector2(126, 118), Font = HomeTypography.Display(12), Colour = navy },
            plot = new Container { Position = new Vector2(30, 164), Size = new Vector2(600, 350), Masking = true, CornerRadius = 8, BorderThickness = 1, BorderColour = new Color4(navy.R, navy.G, navy.B, 0.18f) },
            new SpriteText { Position = new Vector2(30, 530), Text = "OFFSET PREVIEW", Font = HomeTypography.Display(12), Colour = navy },
            createButton("−5", new Vector2(30, 564), () => adjustOffset(-5)),
            createButton("RESET", new Vector2(126, 564), () => setOffset(0)),
            createButton("+5", new Vector2(222, 564), () => adjustOffset(5)),
            offsetText = new SpriteText { Position = new Vector2(326, 574), Font = HomeTypography.Display(17), Colour = pink },
            predictionText = new SpriteText { Position = new Vector2(30, 628), Width = 600, Font = HomeTypography.Display(16), Colour = navy },
            new SpriteText
            {
                Position = new Vector2(30, 678),
                Width = 600,
                Text = string.IsNullOrWhiteSpace(practiceSummary)
                    ? rejudge == null
                        ? "REJUDGE REQUIRES A LOCAL REPLAY"
                        : "PREVIEW ONLY  //  STORED SCORE IS NEVER OVERWRITTEN"
                    : $"PRACTICE  //  {practiceSummary}",
                Font = HomeTypography.Display(9),
                Colour = navy,
                Alpha = 0.52f,
            },
        ];

        rebuildPlot();
        setOffset(0);
    }

    private void cycleLane()
    {
        int laneCount = samples.Count == 0 ? 0 : samples.Max(static sample => sample.Lane) + 1;
        laneFilter++;
        if (laneFilter >= laneCount)
            laneFilter = -1;
        rebuildPlot();
    }

    private void rebuildPlot()
    {
        plot.Clear();
        plot.Add(new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(cyan.R, cyan.G, cyan.B, 0.08f) });
        plot.Add(new Box { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, RelativeSizeAxes = Axes.X, Height = 2, Colour = navy, Alpha = 0.25f });

        GameplayTimingSample[] filtered = samples
            .Where(sample => laneFilter < 0 || sample.Lane == laneFilter)
            .Where(static sample => double.IsFinite(sample.ErrorMilliseconds))
            .ToArray();
        laneText.Text = laneFilter < 0 ? "ALL LANES" : $"K{laneFilter + 1}";
        DisplayedSampleCount = filtered.Length;
        if (filtered.Length == 0)
        {
            plot.Add(new SpriteText { Anchor = Anchor.Centre, Origin = Anchor.Centre, Text = "NO TIMING SAMPLES", Font = HomeTypography.Display(13), Colour = navy, Alpha = 0.48f });
            return;
        }

        double minTime = filtered
            .Select(static sample => sample.TimeMilliseconds)
            .Where(static time => time is double value && double.IsFinite(value))
            .Select(static time => time!.Value)
            .DefaultIfEmpty(0)
            .Min();
        double maxTime = filtered
            .Select(static sample => sample.TimeMilliseconds)
            .Where(static time => time is double value && double.IsFinite(value))
            .Select(static time => time!.Value)
            .DefaultIfEmpty(filtered.Length - 1)
            .Max();
        double span = Math.Max(1, maxTime - minTime);
        double maxError = Math.Max(40, filtered.Max(static sample => Math.Abs(sample.ErrorMilliseconds)));
        int stride = Math.Max(1, (int)Math.Ceiling(filtered.Length / 700d));
        for (int i = 0; i < filtered.Length; i += stride)
        {
            GameplayTimingSample sample = filtered[i];
            double time = sample.TimeMilliseconds is double sampleTime
                          && double.IsFinite(sampleTime)
                ? sampleTime
                : minTime + span * i / Math.Max(1, filtered.Length - 1);
            float x = 6 + 588 * (float)((time - minTime) / span);
            float y = 175 + 164 * (float)(sample.ErrorMilliseconds / maxError);
            plot.Add(new CircularContainer
            {
                Position = new Vector2(x, y),
                Origin = Anchor.Centre,
                Size = new Vector2(5),
                Masking = true,
                Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colourFor(sample.Rating) },
            });
        }
    }

    private void adjustOffset(double delta) => setOffset(offset + delta);

    private void setOffset(double value)
    {
        offset = Math.Clamp(value, -200, 200);
        offsetText.Text = offset.ToString("+0;-0;0", CultureInfo.InvariantCulture) + " ms";
        if (rejudge == null)
        {
            predictionText.Text = "NO REPLAY  //  SCATTER VIEW ONLY";
            return;
        }

        try
        {
            ManiaScoreResult result = rejudge(offset);
            predictionText.Text = $"ACC {result.Accuracy:P2}   SCORE {result.Score:N0}   MISS {result.Miss:N0}";
        }
        catch
        {
            predictionText.Text = "REJUDGE UNAVAILABLE";
        }
    }

    private static Drawable createButton(string text, Vector2 position, Action action) =>
        new LabButton(text, action) { Position = position };

    private static Color4 colourFor(JudgementRating? rating) => rating switch
    {
        JudgementRating.Perfect => cyan,
        JudgementRating.Great => new Color4(0.28f, 0.74f, 0.43f, 1),
        JudgementRating.Good => yellow,
        JudgementRating.Ok or JudgementRating.Meh => new Color4(1, 0.55f, 0.18f, 1),
        JudgementRating.Miss => pink,
        _ => navy,
    };

    private partial class LabButton : ClickableContainer
    {
        public LabButton(string text, Action action)
        {
            Action = action;
            Size = new Vector2(82, 38);
            Masking = true;
            CornerRadius = 5;
            InternalChildren =
            [
                new Box { RelativeSizeAxes = Axes.Both, Colour = navy },
                new SpriteText { Anchor = Anchor.Centre, Origin = Anchor.Centre, Text = text, Font = HomeTypography.Display(10), Colour = Color4.White },
            ];
        }

        protected override bool OnClick(ClickEvent e)
        {
            Action?.Invoke();
            return true;
        }
    }
}
