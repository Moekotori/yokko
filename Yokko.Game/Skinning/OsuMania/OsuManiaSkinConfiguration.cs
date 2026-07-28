using System;
using osuTK.Graphics;

namespace Yokko.Game.Skinning.OsuMania;

internal sealed class OsuManiaSkinConfiguration
{
    public OsuManiaSkinConfiguration(
        int keys,
        float[] columnWidths,
        float[] columnSpacings,
        float[] columnLineWidths,
        float hitPosition,
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
        string stageHint)
    {
        Keys = keys;
        ColumnWidths = columnWidths;
        ColumnSpacings = columnSpacings;
        ColumnLineWidths = columnLineWidths;
        HitPosition = hitPosition;
        UpsideDown = upsideDown;
        KeysUnderNotes = keysUnderNotes;
        NoteBodyStyle = noteBodyStyle;
        LaneColours = laneColours;
        ColumnLineColour = columnLineColour;
        KeyImages = keyImages;
        PressedKeyImages = pressedKeyImages;
        NoteImages = noteImages;
        HoldHeadImages = holdHeadImages;
        HoldBodyImages = holdBodyImages;
        HoldTailImages = holdTailImages;
        StageHint = stageHint;
    }

    public int Keys { get; }

    public float[] ColumnWidths { get; }

    public float[] ColumnSpacings { get; }

    public float[] ColumnLineWidths { get; }

    public float HitPosition { get; }

    public bool UpsideDown { get; }

    public bool KeysUnderNotes { get; }

    public int NoteBodyStyle { get; }

    public Color4[] LaneColours { get; }

    public Color4 ColumnLineColour { get; }

    public string[] KeyImages { get; }

    public string[] PressedKeyImages { get; }

    public string[] NoteImages { get; }

    public string[] HoldHeadImages { get; }

    public string[] HoldBodyImages { get; }

    public string[] HoldTailImages { get; }

    public string StageHint { get; }

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
            "mania-stage-hint");
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
