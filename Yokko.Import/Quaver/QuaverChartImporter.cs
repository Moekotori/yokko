using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;

namespace Yokko.Import.Quaver;

public sealed class QuaverChartImporter : IChartImporter
{
    public ChartImportCapability Capability { get; } =
        new(ChartSourceFormat.Quaver, "Quaver", [".qua", ".qp"], true, false);

    public bool CanImport(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".qua", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".qp", StringComparison.OrdinalIgnoreCase);
    }

    public ValueTask<ChartImportResult> ImportAsync(ChartImportRequest request)
    {
        request.CancellationToken.ThrowIfCancellationRequested();
        if (Path.GetExtension(request.Path).Equals(
                ".qp",
                StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<ChartImportResult> results =
                ImportAllAsync(request).AsTask().GetAwaiter().GetResult();
            ChartImportResult result = results[0];
            IReadOnlyList<string> warnings = results.Count > 1
                ? [$"This .qp contains {results.Count} charts; imported {result.Beatmap.DifficultyName}.", .. result.Warnings]
                : result.Warnings;
            return ValueTask.FromResult(result with { Warnings = warnings });
        }

        return ValueTask.FromResult(importChartFile(request.Path));
    }

    public ValueTask<IReadOnlyList<ChartImportResult>> ImportAllAsync(
        ChartImportRequest request)
    {
        request.CancellationToken.ThrowIfCancellationRequested();
        if (!Path.GetExtension(request.Path).Equals(
                ".qp",
                StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult<IReadOnlyList<ChartImportResult>>(
                [importChartFile(request.Path)]);
        }

        IReadOnlyList<string> charts =
            ChartArchive.ExtractCharts(request.Path, ".qua");
        var results = new List<ChartImportResult>();
        var failures = new List<Exception>();

        foreach (string chart in charts)
        {
            request.CancellationToken.ThrowIfCancellationRequested();

            try
            {
                results.Add(importChartFile(chart));
            }
            catch (InvalidDataException ex)
            {
                failures.Add(ex);
            }
        }

        if (results.Count == 0)
        {
            throw new InvalidDataException(
                "The .qp package does not contain a supported 4K/7K Quaver chart.",
                failures.FirstOrDefault());
        }

        if (failures.Count > 0)
        {
            string warning =
                $"Skipped {failures.Count} unsupported chart{(failures.Count == 1 ? string.Empty : "s")} in this .qp package.";
            for (int i = 0; i < results.Count; i++)
                results[i] = results[i] with
                {
                    Warnings = [warning, .. results[i].Warnings],
                };
        }

        return ValueTask.FromResult<IReadOnlyList<ChartImportResult>>(results);
    }

    private static ChartImportResult importChartFile(string path)
    {
        ParsedQua parsed = parse(File.ReadAllLines(path));
        var warnings = new List<string>();

        KeyMode keyMode = parsed.Mode.ToUpperInvariant() switch
        {
            "KEYS4" => KeyMode.FourKey,
            "KEYS7" => KeyMode.SevenKey,
            _ => throw new InvalidDataException($"Unsupported Quaver mode: {parsed.Mode}."),
        };

        IReadOnlyList<YokkoTimingPoint> timingPoints = parsed.TimingPoints.Count == 0
            ? [YokkoTimingPoint.Default]
            : parsed.TimingPoints.Select(static point => new YokkoTimingPoint(
                                           point.StartTime,
                                           point.Bpm > 0 ? 60000 / point.Bpm : 500,
                                           point.Meter))
                                 .ToArray();

        YokkoHitObject[] hitObjects = parsed.HitObjects.Select(note =>
        {
            int lane = note.Lane - 1;
            if (lane < 0 || lane >= (int)keyMode)
                throw new InvalidDataException($"Quaver lane {note.Lane} is outside {(int)keyMode}K.");

            bool isHold = note.EndTime > note.StartTime;
            return new YokkoHitObject(
                lane,
                note.StartTime,
                isHold ? note.EndTime : null,
                isHold ? HitObjectKind.Hold : HitObjectKind.Tap,
                string.IsNullOrWhiteSpace(note.HitSound) ? null : note.HitSound,
                normalizeScrollProfileId(note.TimingGroup));
        }).OrderBy(static note => note.StartTimeMilliseconds)
          .ThenBy(static note => note.Lane)
          .ToArray();
        YokkoScrollVelocity[] sourceSliderVelocities = parsed.SliderVelocities
                                                            .Select(static velocity =>
                                                                new YokkoScrollVelocity(
                                                                    velocity.StartTime,
                                                                    velocity.Multiplier))
                                                            .ToArray();
        YokkoScrollSpeedFactor[] scrollSpeedFactors =
            parsed.ScrollSpeedFactors
                  .Select(static factor => new YokkoScrollSpeedFactor(
                      factor.StartTime,
                      factor.Multiplier))
                  .ToArray();
        bool normalized = parseBoolean(
            parsed.Values.GetValueOrDefault(
                "BPMDoesNotAffectScrollVelocity"));
        double initialScrollVelocity = ImportParsing.Double(
            parsed.Values.GetValueOrDefault("InitialScrollVelocity"),
            normalized ? 0 : 1);
        ScrollVelocityProfile scrollVelocity =
            ScrollVelocityConversion.FromQuaver(
                timingPoints,
                hitObjects,
                sourceSliderVelocities,
                normalized,
                initialScrollVelocity);
        (YokkoScrollProfile defaultProfile,
            IReadOnlyDictionary<string, YokkoScrollProfile> scrollProfiles) =
            buildScrollProfiles(
                parsed,
                timingPoints,
                hitObjects,
                normalized,
                scrollVelocity,
                scrollSpeedFactors);

        string[] missingProfileIds = hitObjects
                                     .Select(static hitObject =>
                                         hitObject.ScrollProfileId)
                                     .Where(profileId =>
                                         profileId != null
                                         && !scrollProfiles.ContainsKey(
                                             profileId))
                                     .Distinct(StringComparer.Ordinal)
                                     .ToArray()!;

        if (missingProfileIds.Length > 0)
        {
            warnings.Add(
                $"Quaver timing groups were missing: {string.Join(", ", missingProfileIds)}. Those notes use the default scroll profile.");
        }

        var beatmap = new YokkoBeatmap(
            parsed.Values.GetValueOrDefault("Title", "Untitled"),
            parsed.Values.GetValueOrDefault("Artist", "Unknown Artist"),
            parsed.Values.GetValueOrDefault("Creator", "Unknown Creator"),
            parsed.Values.GetValueOrDefault("DifficultyName", $"{(int)keyMode}K"),
            keyMode,
            ChartSourceFormat.Quaver,
            timingPoints,
            ImportParsing.ResolveAdjacentAsset(path, parsed.Values.GetValueOrDefault("AudioFile")),
            hitObjects,
            ScrollVelocities: defaultProfile.ScrollVelocities,
            InitialScrollVelocity: defaultProfile.InitialScrollVelocity,
            ScrollSpeedFactors: defaultProfile.ScrollSpeedFactors,
            ScrollProfiles: scrollProfiles);

        string? artworkPath = ImportParsing.ResolveAdjacentAsset(
            path,
            parsed.Values.GetValueOrDefault("BackgroundFile"));
        return new ChartImportResult(beatmap, warnings, artworkPath);
    }

    private static ParsedQua parse(IEnumerable<string> lines)
    {
        var parsed = new ParsedQua();
        string section = string.Empty;
        QuaTimingPoint? timingPoint = null;
        QuaSliderVelocity? sliderVelocity = null;
        QuaScrollSpeedFactor? scrollSpeedFactor = null;
        QuaScrollGroup? scrollGroup = null;
        string timingGroupSubsection = string.Empty;
        QuaSliderVelocity? groupSliderVelocity = null;
        QuaScrollSpeedFactor? groupScrollSpeedFactor = null;
        QuaHitObject? hitObject = null;

        foreach (string rawLine in lines)
        {
            string trimmed = rawLine.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            bool isTopLevel = !char.IsWhiteSpace(rawLine, 0) && !trimmed.StartsWith('-');
            if (isTopLevel)
            {
                int topLevelSeparator = trimmed.IndexOf(':');
                if (topLevelSeparator < 0)
                    continue;

                string topLevelKey = trimmed[..topLevelSeparator].Trim();
                string topLevelValue = ImportParsing.Scalar(trimmed[(topLevelSeparator + 1)..]);
                bool isCollection = topLevelKey is "TimingPoints" or "SliderVelocities"
                    or "ScrollSpeedFactors" or "HitObjects"
                    or "TimingGroups" or "SoundEffects" or "EditorLayers"
                    or "Bookmarks" or "CustomAudioSamples";

                if (!isCollection)
                {
                    section = string.Empty;
                    parsed.Values[topLevelKey] = topLevelValue;
                    continue;
                }

                section = topLevelKey;
                timingPoint = null;
                sliderVelocity = null;
                scrollSpeedFactor = null;
                scrollGroup = null;
                timingGroupSubsection = string.Empty;
                groupSliderVelocity = null;
                groupScrollSpeedFactor = null;
                hitObject = null;

                continue;
            }

            int separator = trimmed.IndexOf(':');
            if (separator < 0)
                continue;

            string key = trimmed.TrimStart('-').Trim()[..trimmed.TrimStart('-').Trim().IndexOf(':')].Trim();
            string value = ImportParsing.Scalar(trimmed[(separator + 1)..]);
            bool startsItem = trimmed.StartsWith('-');
            int indentation = rawLine.Length - rawLine.TrimStart().Length;

            if (section.Equals("TimingGroups", StringComparison.OrdinalIgnoreCase))
            {
                if (indentation <= 2
                    && !startsItem
                    && value.Contains(
                        "!ScrollGroup",
                        StringComparison.OrdinalIgnoreCase))
                {
                    string id = ImportParsing.Scalar(key);
                    scrollGroup = new QuaScrollGroup(id);
                    parsed.ScrollGroups[id] = scrollGroup;
                    timingGroupSubsection = string.Empty;
                    groupSliderVelocity = null;
                    groupScrollSpeedFactor = null;
                    continue;
                }

                if (scrollGroup == null)
                    continue;

                if (indentation <= 4
                    && key.Equals(
                        "ScrollVelocities",
                        StringComparison.OrdinalIgnoreCase))
                {
                    timingGroupSubsection = "ScrollVelocities";
                    groupSliderVelocity = null;
                    continue;
                }

                if (indentation <= 4
                    && key.Equals(
                        "ScrollSpeedFactors",
                        StringComparison.OrdinalIgnoreCase))
                {
                    timingGroupSubsection = "ScrollSpeedFactors";
                    groupScrollSpeedFactor = null;
                    continue;
                }

                if (indentation > 4
                    && timingGroupSubsection == "ScrollVelocities")
                {
                    if (startsItem)
                    {
                        groupSliderVelocity = new QuaSliderVelocity();
                        scrollGroup.SliderVelocities.Add(
                            groupSliderVelocity);
                    }

                    groupSliderVelocity ??= addSliderVelocity(scrollGroup);
                    assignSliderVelocity(groupSliderVelocity, key, value);
                    continue;
                }

                if (indentation > 4
                    && timingGroupSubsection == "ScrollSpeedFactors")
                {
                    if (startsItem)
                    {
                        groupScrollSpeedFactor =
                            new QuaScrollSpeedFactor();
                        scrollGroup.ScrollSpeedFactors.Add(
                            groupScrollSpeedFactor);
                    }

                    groupScrollSpeedFactor ??=
                        addScrollSpeedFactor(scrollGroup);
                    assignScrollSpeedFactor(
                        groupScrollSpeedFactor,
                        key,
                        value);
                    continue;
                }

                timingGroupSubsection = string.Empty;
                groupSliderVelocity = null;
                groupScrollSpeedFactor = null;

                if (key.Equals(
                        "InitialScrollVelocity",
                        StringComparison.OrdinalIgnoreCase))
                {
                    scrollGroup.InitialScrollVelocity =
                        ImportParsing.Double(value);
                }

                continue;
            }

            if (section.Equals("SliderVelocities", StringComparison.OrdinalIgnoreCase))
            {
                if (startsItem)
                {
                    sliderVelocity = new QuaSliderVelocity();
                    parsed.SliderVelocities.Add(sliderVelocity);
                }

                sliderVelocity ??= addSliderVelocity(parsed);
                assignSliderVelocity(sliderVelocity, key, value);
                continue;
            }

            if (section.Equals("ScrollSpeedFactors", StringComparison.OrdinalIgnoreCase))
            {
                if (startsItem)
                {
                    scrollSpeedFactor = new QuaScrollSpeedFactor();
                    parsed.ScrollSpeedFactors.Add(scrollSpeedFactor);
                }

                scrollSpeedFactor ??= addScrollSpeedFactor(parsed);
                assignScrollSpeedFactor(scrollSpeedFactor, key, value);
                continue;
            }

            if (section.Equals("TimingPoints", StringComparison.OrdinalIgnoreCase))
            {
                if (startsItem)
                {
                    timingPoint = new QuaTimingPoint();
                    parsed.TimingPoints.Add(timingPoint);
                }

                timingPoint ??= addTimingPoint(parsed);
                assignTimingPoint(timingPoint, key, value);
                continue;
            }

            if (section.Equals("HitObjects", StringComparison.OrdinalIgnoreCase))
            {
                if (startsItem)
                {
                    hitObject = new QuaHitObject();
                    parsed.HitObjects.Add(hitObject);
                }

                hitObject ??= addHitObject(parsed);
                assignHitObject(hitObject, key, value);
                continue;
            }

        }

        parsed.Mode = parsed.Values.GetValueOrDefault("Mode", string.Empty);
        return parsed;
    }

    private static QuaTimingPoint addTimingPoint(ParsedQua parsed)
    {
        var point = new QuaTimingPoint();
        parsed.TimingPoints.Add(point);
        return point;
    }

    private static QuaHitObject addHitObject(ParsedQua parsed)
    {
        var note = new QuaHitObject();
        parsed.HitObjects.Add(note);
        return note;
    }

    private static QuaSliderVelocity addSliderVelocity(ParsedQua parsed)
    {
        var velocity = new QuaSliderVelocity();
        parsed.SliderVelocities.Add(velocity);
        return velocity;
    }

    private static QuaSliderVelocity addSliderVelocity(QuaScrollGroup group)
    {
        var velocity = new QuaSliderVelocity();
        group.SliderVelocities.Add(velocity);
        return velocity;
    }

    private static QuaScrollSpeedFactor addScrollSpeedFactor(ParsedQua parsed)
    {
        var factor = new QuaScrollSpeedFactor();
        parsed.ScrollSpeedFactors.Add(factor);
        return factor;
    }

    private static QuaScrollSpeedFactor addScrollSpeedFactor(
        QuaScrollGroup group)
    {
        var factor = new QuaScrollSpeedFactor();
        group.ScrollSpeedFactors.Add(factor);
        return factor;
    }

    private static void assignTimingPoint(QuaTimingPoint point, string key, string value)
    {
        if (key.Equals("StartTime", StringComparison.OrdinalIgnoreCase))
            point.StartTime = ImportParsing.Double(value);
        else if (key.Equals("Bpm", StringComparison.OrdinalIgnoreCase))
            point.Bpm = ImportParsing.Double(value, 120);
        else if (key.Equals("TimeSignature", StringComparison.OrdinalIgnoreCase)
                 || key.Equals("Signature", StringComparison.OrdinalIgnoreCase))
        {
            string numerator = value.Split(['/', '|'])[0];
            point.Meter = Math.Max(1, ImportParsing.Int(numerator, 4));
        }
    }

    private static void assignHitObject(QuaHitObject note, string key, string value)
    {
        if (key.Equals("StartTime", StringComparison.OrdinalIgnoreCase))
            note.StartTime = ImportParsing.Double(value);
        else if (key.Equals("EndTime", StringComparison.OrdinalIgnoreCase))
            note.EndTime = ImportParsing.Double(value);
        else if (key.Equals("Lane", StringComparison.OrdinalIgnoreCase))
            note.Lane = ImportParsing.Int(value);
        else if (key.Equals("HitSound", StringComparison.OrdinalIgnoreCase))
            note.HitSound = value;
        else if (key.Equals("TimingGroup", StringComparison.OrdinalIgnoreCase))
            note.TimingGroup = value;
    }

    private static void assignSliderVelocity(
        QuaSliderVelocity velocity,
        string key,
        string value)
    {
        if (key.Equals("StartTime", StringComparison.OrdinalIgnoreCase))
            velocity.StartTime = ImportParsing.Double(value);
        else if (key.Equals("Multiplier", StringComparison.OrdinalIgnoreCase))
            velocity.Multiplier = ImportParsing.Double(value, 1);
    }

    private static void assignScrollSpeedFactor(
        QuaScrollSpeedFactor factor,
        string key,
        string value)
    {
        if (key.Equals("StartTime", StringComparison.OrdinalIgnoreCase))
            factor.StartTime = ImportParsing.Double(value);
        else if (key.Equals("Multiplier", StringComparison.OrdinalIgnoreCase))
            factor.Multiplier = ImportParsing.Double(value, 1);
    }

    private static bool parseBoolean(string? value)
        => bool.TryParse(value, out bool parsed)
            ? parsed
            : ImportParsing.Int(value) != 0;

    private static string? normalizeScrollProfileId(string? timingGroup)
    {
        if (string.IsNullOrWhiteSpace(timingGroup)
            || timingGroup.Equals(
                "$Default",
                StringComparison.Ordinal))
        {
            return null;
        }

        return timingGroup;
    }

    private static (
        YokkoScrollProfile DefaultProfile,
        IReadOnlyDictionary<string, YokkoScrollProfile> Profiles)
        buildScrollProfiles(
            ParsedQua parsed,
            IReadOnlyList<YokkoTimingPoint> timingPoints,
            IReadOnlyList<YokkoHitObject> hitObjects,
            bool normalized,
            ScrollVelocityProfile defaultVelocity,
            IReadOnlyList<YokkoScrollSpeedFactor> defaultFactors)
    {
        YokkoScrollProfile? globalProfile = null;

        if (parsed.ScrollGroups.TryGetValue(
                "$Global",
                out QuaScrollGroup? globalGroup))
        {
            globalProfile = createScrollProfile(
                globalGroup,
                timingPoints,
                hitObjects,
                normalized,
                globalInitialIsIgnored: true);
        }

        YokkoScrollProfile defaultProfile = mergeGlobalProfile(
            new YokkoScrollProfile(
                defaultVelocity.InitialMultiplier,
                defaultVelocity.Changes,
                defaultFactors),
            globalProfile);
        var profiles =
            new Dictionary<string, YokkoScrollProfile>(
                StringComparer.Ordinal);

        foreach ((string id, QuaScrollGroup group) in parsed.ScrollGroups)
        {
            if (id == "$Global" || id == "$Default")
                continue;

            profiles[id] = mergeGlobalProfile(
                createScrollProfile(
                    group,
                    timingPoints,
                    hitObjects,
                    normalized,
                    globalInitialIsIgnored: false),
                globalProfile);
        }

        return (defaultProfile, profiles);
    }

    private static YokkoScrollProfile createScrollProfile(
        QuaScrollGroup group,
        IReadOnlyList<YokkoTimingPoint> timingPoints,
        IReadOnlyList<YokkoHitObject> hitObjects,
        bool normalized,
        bool globalInitialIsIgnored)
    {
        YokkoScrollVelocity[] rawVelocities = group.SliderVelocities
                                                   .Select(static velocity =>
                                                       new YokkoScrollVelocity(
                                                           velocity.StartTime,
                                                           velocity.Multiplier))
                                                   .ToArray();
        ScrollVelocityProfile velocity =
            ScrollVelocityConversion.FromQuaver(
                timingPoints,
                hitObjects,
                rawVelocities,
                normalized,
                group.InitialScrollVelocity);
        IReadOnlyList<YokkoScrollVelocity> changes = velocity.Changes;

        if (globalInitialIsIgnored && rawVelocities.Length > 0)
        {
            double firstTime = rawVelocities.Min(
                static item => item.TimeMilliseconds);
            var normalizedMap = new ScrollVelocityMap(
                velocity.Changes,
                velocity.InitialMultiplier);
            changes =
            [
                new YokkoScrollVelocity(
                    firstTime,
                    normalizedMap.MultiplierAt(firstTime)),
                .. velocity.Changes.Where(change =>
                    change.TimeMilliseconds != firstTime),
            ];
        }

        return new YokkoScrollProfile(
            velocity.InitialMultiplier,
            changes,
            group.ScrollSpeedFactors
                 .Select(static factor => new YokkoScrollSpeedFactor(
                     factor.StartTime,
                     factor.Multiplier))
                 .ToArray());
    }

    private static YokkoScrollProfile mergeGlobalProfile(
        YokkoScrollProfile profile,
        YokkoScrollProfile? globalProfile)
    {
        if (globalProfile == null)
            return profile;

        YokkoScrollVelocity[] velocities = profile.ScrollVelocities
                                                   .Concat(
                                                       globalProfile.ScrollVelocities)
                                                   .OrderBy(static velocity =>
                                                       velocity.TimeMilliseconds)
                                                   .GroupBy(static velocity =>
                                                       velocity.TimeMilliseconds)
                                                   .Select(static group =>
                                                       group.Last())
                                                   .ToArray();
        YokkoScrollSpeedFactor[] factors = profile.ScrollSpeedFactors
                                                   .Concat(
                                                       globalProfile.ScrollSpeedFactors)
                                                   .OrderBy(static factor =>
                                                       factor.TimeMilliseconds)
                                                   .GroupBy(static factor =>
                                                       factor.TimeMilliseconds)
                                                   .Select(static group =>
                                                       group.Last())
                                                   .ToArray();
        return profile with
        {
            ScrollVelocities = velocities,
            ScrollSpeedFactors = factors,
        };
    }

    private sealed class ParsedQua
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<QuaTimingPoint> TimingPoints { get; } = [];
        public List<QuaSliderVelocity> SliderVelocities { get; } = [];
        public List<QuaScrollSpeedFactor> ScrollSpeedFactors { get; } = [];
        public Dictionary<string, QuaScrollGroup> ScrollGroups { get; } =
            new(StringComparer.Ordinal);
        public List<QuaHitObject> HitObjects { get; } = [];
        public string Mode { get; set; } = string.Empty;
    }

    private sealed class QuaSliderVelocity
    {
        public double StartTime { get; set; }
        public double Multiplier { get; set; } = 1;
    }

    private sealed class QuaScrollSpeedFactor
    {
        public double StartTime { get; set; }
        public double Multiplier { get; set; } = 1;
    }

    private sealed class QuaScrollGroup
    {
        public QuaScrollGroup(string id)
        {
            Id = id;
        }

        public string Id { get; }
        public double InitialScrollVelocity { get; set; }
        public List<QuaSliderVelocity> SliderVelocities { get; } = [];
        public List<QuaScrollSpeedFactor> ScrollSpeedFactors { get; } = [];
    }

    private sealed class QuaTimingPoint
    {
        public double StartTime { get; set; }
        public double Bpm { get; set; } = 120;
        public int Meter { get; set; } = 4;
    }

    private sealed class QuaHitObject
    {
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public int Lane { get; set; }
        public string? HitSound { get; set; }
        public string? TimingGroup { get; set; }
    }
}
