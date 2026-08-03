namespace Yokko.Core.Mods;

/// <summary>
/// Metadata for one entry in the osu!mania parity target catalogue.
/// Runtime implementations and configurable values intentionally live outside
/// this type so listing a target never implies that it is already playable.
/// </summary>
public sealed record ManiaModDefinition(
    ManiaModId Id,
    string Key,
    string Acronym,
    string Name,
    ManiaModCategory Category)
{
    /// <summary>
    /// Short player-facing explanation used by the mod selector.
    /// </summary>
    public string Description => Id switch
    {
        ManiaModId.Easy => "Forgiving difficulty and gentler health drain.",
        ManiaModId.NoFail => "Keep playing even when your health reaches zero.",
        ManiaModId.NoPause => "Limit how many times gameplay may be paused.",
        ManiaModId.IidxHardGauge =>
            "Use IIDX Hard health changes independently of judgement timing.",
        ManiaModId.Lr2HardGauge =>
            "Use LR2 Hard health changes independently of judgement timing.",
        ManiaModId.BeatorajaHardGauge =>
            "Use beatoraja Hard health changes independently of judgement timing.",
        ManiaModId.HalfTime => "Slow the song down to 75% speed.",
        ManiaModId.Daycore => "Slow down with a lower-pitched soundtrack.",
        ManiaModId.NoRelease => "Ignore judgements when hold notes are released.",
        ManiaModId.HardRock => "Raise the difficulty and health drain.",
        ManiaModId.SuddenDeath => "A single miss ends the run.",
        ManiaModId.Perfect => "Any judgement below Great ends the run.",
        ManiaModId.DoubleTime => "Speed the song up to 150%.",
        ManiaModId.Nightcore => "Speed up with a higher-pitched soundtrack.",
        ManiaModId.FadeIn => "Notes appear gradually as they approach.",
        ManiaModId.Hidden => "Notes fade before reaching the judgement line.",
        ManiaModId.Cover => "Hide part of the playfield with a cover.",
        ManiaModId.Flashlight => "See notes only through a limited viewing area.",
        ManiaModId.AccuracyChallenge => "Fail when accuracy drops below your target.",
        ManiaModId.Random => "Shuffle note columns with a repeatable seed.",
        ManiaModId.DualStages => "Split converted charts across two playfields.",
        ManiaModId.Mirror => "Reverse every note column.",
        ManiaModId.DifficultyAdjust => "Customise health drain and judgement difficulty.",
        ManiaModId.Classic => "Use classic mania scoring and behaviour.",
        ManiaModId.Invert => "Swap tap notes and hold-note bodies.",
        ManiaModId.ConstantSpeed => "Keep the visual scroll velocity constant.",
        ManiaModId.HoldOff => "Convert hold notes into regular tap notes.",
        >= ManiaModId.Key1 and <= ManiaModId.Key10 =>
            "Convert a standard-mode chart to this key count.",
        ManiaModId.Autoplay =>
            "Automatically play, then save the replay and local score.",
        ManiaModId.Cinema => "Watch an automated performance without the playfield.",
        ManiaModId.WindUp => "Gradually increase playback speed.",
        ManiaModId.WindDown => "Gradually decrease playback speed.",
        ManiaModId.Muted => "Play with configurable audio cues muted.",
        ManiaModId.AdaptiveSpeed => "Change speed in response to recent accuracy.",
        ManiaModId.ScoreV2 => "Use the modern score calculation.",
        _ => string.Empty,
    };
}
