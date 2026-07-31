using StarRatingRebirth;

namespace Yokko.Core.Difficulty;

[Flags]
public enum ManiaStarRatingAdjustments
{
    None = 0,
    LengthCalibration = 1 << 0,
    LinkedLongNotesCalibrated = 1 << 1,
    InvertLongNotesCalibrated = 1 << 2,
}

internal sealed record ManiaStarRatingCalibrationResult(
    double Value,
    ManiaStarRatingAdjustments Adjustments,
    double LongNoteCalibrationFactor,
    double EffectiveActionCount);

/// <summary>
/// Applies Yokko-owned calibration after Rebirth has measured pattern
/// intensity. The LN caps follow the two problem cases documented at
/// https://github.com/sunnyxxy/Star-Rating-Rebirth/issues/5 and are never
/// blanket multipliers for ordinary mixed charts.
/// </summary>
internal static class ManiaStarRatingCalibration
{
    private const double upstream_final_scale = 0.975;
    private const double length_denominator = 60;
    private const double invert_long_note_factor = 0.90;
    private const double connected_long_note_maximum_reduction = 0.12;
    private const int connected_tail_window_milliseconds = 150;
    private const double long_note_risk_start = 0.50;
    private const double long_note_risk_full = 0.90;

    public static ManiaStarRatingCalibrationResult Apply(
        double upstreamRating,
        ManiaData data,
        bool releaseJudgementsRequired,
        bool invertApplied)
    {
        double patternIntensity = removeUpstreamPostProcessing(
            upstreamRating,
            upstreamEffectiveNoteCount(data));
        ManiaStarRatingAdjustments adjustments =
            ManiaStarRatingAdjustments.LengthCalibration;
        double appliedLongNoteFactor = 1;
        int longNoteCount = data.Notes.Count(
            static note => note.IsLong);

        if (longNoteCount > 0)
        {
            double risk = connectedLongNoteRisk(data);
            double longNoteFactor = invertApplied
                ? invert_long_note_factor
                : 1
                  - connected_long_note_maximum_reduction
                  * risk;

            if (longNoteFactor < 1)
            {
                ManiaData headOnlyData = withoutLongNoteTails(data);
                double headOnlyRating =
                    SRCalculator.Calculate(headOnlyData);
                double headOnlyIntensity =
                    removeUpstreamPostProcessing(
                        headOnlyRating,
                        upstreamEffectiveNoteCount(headOnlyData));
                double calibratedIntensity = Math.Max(
                    headOnlyIntensity,
                    patternIntensity * longNoteFactor);

                if (calibratedIntensity < patternIntensity)
                {
                    appliedLongNoteFactor =
                        calibratedIntensity / patternIntensity;
                    patternIntensity = calibratedIntensity;
                    adjustments |= invertApplied
                        ? ManiaStarRatingAdjustments
                            .InvertLongNotesCalibrated
                        : ManiaStarRatingAdjustments
                            .LinkedLongNotesCalibrated;
                }
            }
        }

        double effectiveActionCount = (double)data.Notes.Count
                                      + (releaseJudgementsRequired
                                          ? longNoteCount
                                          : 0);
        double lengthFactor = Math.Sqrt(
            effectiveActionCount
            / (effectiveActionCount + length_denominator));
        double value = applyUpstreamFinalScale(
            patternIntensity * lengthFactor);
        return new ManiaStarRatingCalibrationResult(
            value,
            adjustments,
            appliedLongNoteFactor,
            effectiveActionCount);
    }

    private static double connectedLongNoteRisk(ManiaData data)
    {
        int longNoteCount = data.Notes.Count(
            static note => note.IsLong);
        if (longNoteCount == 0)
            return 0;

        int eligibleLongNotes = 0;
        int connectedLongNotes = 0;
        foreach (IGrouping<int, Note> column in data.Notes
                     .GroupBy(static note => note.Key))
        {
            Note[] notes = column
                           .OrderBy(static note => note.Head)
                           .ThenBy(static note => note.Tail)
                           .ToArray();
            for (int index = 0; index < notes.Length - 1; index++)
            {
                Note current = notes[index];
                if (!current.IsLong)
                    continue;

                eligibleLongNotes++;
                long tailToNextHead =
                    (long)notes[index + 1].Head - current.Tail;
                if (Math.Abs(tailToNextHead)
                    <= connected_tail_window_milliseconds)
                {
                    connectedLongNotes++;
                }
            }
        }

        if (eligibleLongNotes == 0)
            return 0;

        double longNoteShare =
            (double)longNoteCount / data.Notes.Count;
        double connectedShare =
            (double)connectedLongNotes / eligibleLongNotes;
        return smoothStep(
                   long_note_risk_start,
                   long_note_risk_full,
                   longNoteShare)
               * smoothStep(
                   long_note_risk_start,
                   long_note_risk_full,
                   connectedShare);
    }

    private static double smoothStep(
        double lower,
        double upper,
        double value)
    {
        double t = Math.Clamp(
            (value - lower) / (upper - lower),
            0,
            1);
        return t * t * (3 - 2 * t);
    }

    private static ManiaData withoutLongNoteTails(ManiaData data) =>
        new()
        {
            CS = data.CS,
            OD = data.OD,
            Notes = data.Notes
                        .Select(static note =>
                            new Note(note.Key, note.Head, -1))
                        .ToList(),
        };

    private static double upstreamEffectiveNoteCount(
        ManiaData data)
    {
        double total = data.Notes.Count;
        foreach (Note note in data.Notes)
        {
            if (!note.IsLong)
                continue;

            int duration = Math.Max(0, note.Tail - note.Head);
            total += 0.5 * Math.Min(duration, 1000) / 200.0;
        }

        return total;
    }

    private static double removeUpstreamPostProcessing(
        double rating,
        double effectiveNoteCount)
    {
        double afterLengthFactor =
            inverseRescaleHigh(rating / upstream_final_scale);
        double upstreamLengthFactor =
            effectiveNoteCount
            / (effectiveNoteCount + length_denominator);
        return afterLengthFactor / upstreamLengthFactor;
    }

    private static double applyUpstreamFinalScale(double rating) =>
        rescaleHigh(rating) * upstream_final_scale;

    private static double rescaleHigh(double rating) =>
        rating <= 9
            ? rating
            : 9 + (rating - 9) / 1.2;

    private static double inverseRescaleHigh(double rating) =>
        rating <= 9
            ? rating
            : 9 + (rating - 9) * 1.2;
}
