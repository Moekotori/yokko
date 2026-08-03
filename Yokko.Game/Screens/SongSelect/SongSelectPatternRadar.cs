using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using Yokko.Core.Analysis;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectPatternRadar : CompositeDrawable
{
    private static readonly string[] labels =
        ["JACK", "CHORD", "BURST", "ANCHOR", "LN", "RELEASE"];

    internal SongSelectPatternRadar(ManiaPatternProfile profile)
    {
        Size = new Vector2(522, 58);
        double[] values =
        [
            profile.Jack,
            profile.Chord,
            profile.Burst,
            profile.Anchor,
            profile.LongNote,
            profile.Release,
        ];

        var radar = new Container
        {
            Position = new Vector2(2, 0),
            Size = new Vector2(58),
        };
        for (int axis = 0; axis < values.Length; axis++)
        {
            double angle = -Math.PI / 2 + axis * Math.PI / 3;
            float degrees = (float)(angle * 180 / Math.PI);
            radar.Add(new Box
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.CentreLeft,
                Size = new Vector2(25, 1),
                Rotation = degrees,
                Colour = SongSelectTheme.Navy,
                Alpha = 0.17f,
            });
            float radius = 5 + 20 * (float)(values[axis] / 100);
            radar.Add(new CircularContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Position = new Vector2(
                    (float)Math.Cos(angle) * radius,
                    (float)Math.Sin(angle) * radius),
                Size = new Vector2(5),
                Masking = true,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = axis % 2 == 0
                        ? SongSelectTheme.Pink
                        : SongSelectTheme.Cyan,
                },
            });
        }

        var metrics = new FillFlowContainer
        {
            Position = new Vector2(74, 7),
            Size = new Vector2(448, 44),
            Direction = FillDirection.Full,
            Spacing = new Vector2(6, 5),
        };
        for (int axis = 0; axis < values.Length; axis++)
        {
            metrics.Add(new Container
            {
                Size = new Vector2(140, 17),
                Children =
                [
                    new SpriteText
                    {
                        Text = labels[axis],
                        Font = HomeTypography.Display(8),
                        Colour = SongSelectTheme.Cyan,
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Text = values[axis].ToString("0"),
                        Font = HomeTypography.Display(10),
                        Colour = SongSelectTheme.Navy,
                    },
                    new Box
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Width = (float)(values[axis] / 100),
                        Height = 2,
                        Colour = axis % 2 == 0
                            ? SongSelectTheme.Pink
                            : SongSelectTheme.Cyan,
                    },
                ],
            });
        }

        InternalChildren = [radar, metrics];
    }
}
