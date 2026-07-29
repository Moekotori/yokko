using System;
using System.Linq;
using osuTK.Graphics;

namespace Yokko.Game.Skinning.OsuMania;

internal sealed class OsuManiaSkinConfiguration
{
    public const float LegacyPositionScaleFactor = 1.6f;

    public OsuManiaSkinConfiguration(
        int keys,
        float[] columnWidths,
        float[] columnSpacings,
        float[] columnLineWidths,
        float hitPosition,
        float scorePosition,
        float comboPosition,
        bool upsideDown,
        bool keysUnderNotes,
        int noteBodyStyle,
        Color4[] laneColours,
        Color4 columnLineColour,
        string[] keyImages,
        string[] pressedKeyImages,
        string[] noteImages,
        string[] holdHeadImages,
        string[] holdBodyImages,
        string[] holdTailImages,
        string stageHint,
        string hit0,
        string hit50,
        string hit100,
        string hit200,
        string hit300,
        string hit300g)
    {
        Keys = keys;
        ColumnWidths = columnWidths;
        ColumnSpacings = columnSpacings;
        ColumnLineWidths = columnLineWidths;
        HitPosition = hitPosition;
        ScorePosition = scorePosition;
        ComboPosition = comboPosition;
        UpsideDown = upsideDown;
        KeysUnderNotes = keysUnderNotes;
        NoteBodyStyles = Enumerable.Repeat(noteBodyStyle, keys).ToArray();
        LaneColours = laneColours;
        ColumnLineColour = columnLineColour;
        KeyImages = keyImages;
        PressedKeyImages = pressedKeyImages;
        NoteImages = noteImages;
        HoldHeadImages = holdHeadImages;
        HoldBodyImages = holdBodyImages;
        HoldTailImages = holdTailImages;
        StageHint = stageHint;
        Hit0 = hit0;
        Hit50 = hit50;
        Hit100 = hit100;
        Hit200 = hit200;
        Hit300 = hit300;
        Hit300g = hit300g;
        WidthForNoteHeightScale = columnWidths.Min();
        KeyFlipWhenUpsideDown = Enumerable.Repeat(true, keys).ToArray();
        PressedKeyFlipWhenUpsideDown = Enumerable.Repeat(true, keys).ToArray();
        NoteFlipWhenUpsideDown = Enumerable.Repeat(true, keys).ToArray();
        HoldHeadFlipWhenUpsideDown = Enumerable.Repeat(true, keys).ToArray();
        HoldBodyFlipWhenUpsideDown = Enumerable.Repeat(true, keys).ToArray();
        HoldTailFlipWhenUpsideDown = Enumerable.Repeat(true, keys).ToArray();
        LaneLightColours = Enumerable.Repeat(Color4.White, keys).ToArray();
    }

    public int Keys { get; }

    public float[] ColumnWidths { get; }

    public float[] ColumnSpacings { get; }

    public float[] ColumnLineWidths { get; }

    public float HitPosition { get; }

    public float ScorePosition { get; }

    public float ComboPosition { get; }

    public bool UpsideDown { get; }

    public bool KeysUnderNotes { get; }

    public int[] NoteBodyStyles { get; init; }

    public float WidthForNoteHeightScale { get; init; }

    public bool[] KeyFlipWhenUpsideDown { get; init; }

    public bool[] PressedKeyFlipWhenUpsideDown { get; init; }

    public bool[] NoteFlipWhenUpsideDown { get; init; }

    public bool[] HoldHeadFlipWhenUpsideDown { get; init; }

    public bool[] HoldBodyFlipWhenUpsideDown { get; init; }

    public bool[] HoldTailFlipWhenUpsideDown { get; init; }

    public Color4[] LaneColours { get; }

    public Color4 ColumnLineColour { get; }

    public string[] KeyImages { get; }

    public string[] PressedKeyImages { get; }

    public string[] NoteImages { get; }

    public string[] HoldHeadImages { get; }

    public string[] HoldBodyImages { get; }

    public string[] HoldTailImages { get; }

    public string StageHint { get; }

    public string Hit0 { get; }

    public string Hit50 { get; }

    public string Hit100 { get; }

    public string Hit200 { get; }

    public string Hit300 { get; }

    public string Hit300g { get; }

    public string LightImage { get; init; } = "mania-stage-light";

    public string ExplosionImage { get; init; } = "lightingN";

    public float LightPosition { get; init; } = 413;

    public int LightFramePerSecond { get; init; } = 60;

    public float ExplosionWidth { get; init; }

    public Color4[] LaneLightColours { get; init; }

    public float PlayfieldWidth
    {
        get
        {
            float width = 0;

            for (int lane = 0; lane < Keys; lane++)
            {
                width += ColumnWidths[lane];

                if (lane < Keys - 1)
                    width += ColumnSpacings[lane];
            }

            return Math.Max(1, width);
        }
    }

    public float GetLaneX(int lane)
    {
        float x = 0;

        for (int i = 0; i < lane; i++)
            x += ColumnWidths[i] + ColumnSpacings[i];

        return x;
    }

    public static OsuManiaSkinConfiguration CreateDefault(int keys)
    {
        string[] styles = defaultStyles(keys);
        var widths = new float[keys];
        var spacings = new float[keys];
        var lineWidths = new float[keys];
        var laneColours = new Color4[keys];
        var keyImages = new string[keys];
        var pressedKeyImages = new string[keys];
        var noteImages = new string[keys];
        var holdHeadImages = new string[keys];
        var holdBodyImages = new string[keys];
        var holdTailImages = new string[keys];

        for (int lane = 0; lane < keys; lane++)
        {
            string style = styles[lane];
            widths[lane] = 30;
            lineWidths[lane] = 2;
            laneColours[lane] = new Color4(0, 0, 0, 1);
            keyImages[lane] = $"mania-key{style}";
            pressedKeyImages[lane] = $"mania-key{style}D";
            noteImages[lane] = $"mania-note{style}";
            holdHeadImages[lane] = $"mania-note{style}H";
            holdBodyImages[lane] = $"mania-note{style}L";
            holdTailImages[lane] = $"mania-note{style}T";
        }

        return new OsuManiaSkinConfiguration(
            keys,
            widths,
            spacings,
            lineWidths,
            402,
            325,
            111,
            false,
            false,
            1,
            laneColours,
            Color4.White,
            keyImages,
            pressedKeyImages,
            noteImages,
            holdHeadImages,
            holdBodyImages,
            holdTailImages,
            "mania-stage-hint",
            "mania-hit0",
            "mania-hit50",
            "mania-hit100",
            "mania-hit200",
            "mania-hit300",
            "mania-hit300g");
    }

    private static string[] defaultStyles(int keys)
    {
        string layout = keys switch
        {
            1 => "S",
            2 => "11",
            3 => "1S1",
            4 => "1221",
            5 => "12S21",
            6 => "121121",
            7 => "121S121",
            8 => "12122121",
            9 => "1212S2121",
            _ => new string('1', keys),
        };

        var styles = new string[keys];

        for (int i = 0; i < keys; i++)
            styles[i] = layout[i].ToString();

        return styles;
    }
}
