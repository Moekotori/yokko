using System.Text;
using Yokko.Core.Mods;

namespace Yokko.Import.Malody;

public readonly record struct MalodyReplayEvent(
    int TimeMilliseconds,
    int Lane,
    bool IsPressed);

[Flags]
public enum MalodyReplayMods
{
    None = 0,
    Luck = 1 << 1,
    Flip = 1 << 2,
    Constant = 1 << 3,
    Dash = 1 << 4,
    Rush = 1 << 5,
    Hide = 1 << 6,
    Slow = 1 << 8,
    Death = 1 << 9,
}

public sealed record MalodyReplay(
    string BeatmapHash,
    string DifficultyName,
    string SongTitle,
    string SongArtist,
    int Score,
    int MaxCombo,
    int Best,
    int Cool,
    int Good,
    int Miss,
    int HoldBreaks,
    MalodyReplayMods Mods,
    int Judge,
    DateTimeOffset? PlayedAt,
    IReadOnlyList<MalodyReplayEvent> Events);

/// <summary>
/// Reads Malody 4.x Key-mode .mr files. The binary layout follows
/// Mania-Visualization-Project/Mania-Replay-Master's MalodyReplayReader.kt,
/// commit 99c0a672771c443739a01f27963f7a2a9ed74432 (Apache-2.0).
/// </summary>
public static class MalodyReplayIO
{
    public const string FileExtension = ".mr";

    private const string header_marker = "mr format head";
    private const string data_marker = "mr data";
    private const long maximum_file_bytes = 128L * 1024 * 1024;
    private const int maximum_string_bytes = 1024 * 1024;
    private const int maximum_event_count = 10_000_000;

    public static MalodyReplay ReadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = File.OpenRead(path);
        return Read(stream);
    }

    public static MalodyReplay Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("The replay stream is not readable.", nameof(stream));
        if (stream.CanSeek && stream.Length > maximum_file_bytes)
            throw new InvalidDataException("The Malody replay is too large.");

        try
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            requireMarker(reader, header_marker);
            readExactly(reader, 4); // Malody replay schema version.

            string beatmapHash = readString(reader);
            if (!isMd5(beatmapHash))
                throw new InvalidDataException("The Malody replay beatmap hash is invalid.");

            string difficultyName = readString(reader);
            string songTitle = readString(reader);
            string songArtist = readString(reader);
            int score = readNonNegative(reader, "score");
            int maxCombo = readNonNegative(reader, "maximum combo");
            int best = readNonNegative(reader, "BEST count");
            int cool = readNonNegative(reader, "COOL count");
            int good = readNonNegative(reader, "GOOD count");
            int miss = readNonNegative(reader, "MISS count");
            int holdBreaks = readNonNegative(reader, "hold-break count");
            var mods = (MalodyReplayMods)reader.ReadInt32();
            int judge = reader.ReadInt32();
            if (judge is < 0 or > 4)
                throw new InvalidDataException("The Malody replay judge level is invalid.");

            requireMarker(reader, data_marker);
            readExactly(reader, 4); // Event payload version.
            int eventCount = reader.ReadInt32();
            if (eventCount is < 0 or > maximum_event_count)
                throw new InvalidDataException("The Malody replay event count is invalid.");

            reader.ReadByte(); // Platform marker used by the legacy client.
            int playedAtSeconds = reader.ReadInt32();
            readExactly(reader, 4); // Reserved payload field.

            var events = new List<(MalodyReplayEvent Event, int Order)>(eventCount);
            for (int i = 0; i < eventCount; i++)
            {
                int time = reader.ReadInt32();
                byte action = reader.ReadByte();
                byte lane = reader.ReadByte();
                if (action is not 1 and not 2)
                    throw new InvalidDataException("The Malody replay contains an invalid key action.");
                if (lane >= 64)
                    throw new InvalidDataException("The Malody replay contains an invalid lane.");
                if (time < 0)
                    continue;

                events.Add((new MalodyReplayEvent(time, lane, action == 1), i));
            }

            MalodyReplayEvent[] orderedEvents = events
                .OrderBy(static item => item.Event.TimeMilliseconds)
                // Malody resolves a release before a new press at the same timestamp.
                .ThenBy(static item => item.Event.IsPressed ? 1 : 0)
                .ThenBy(static item => item.Order)
                .Select(static item => item.Event)
                .ToArray();

            DateTimeOffset? playedAt = null;
            if (playedAtSeconds > 0)
            {
                try
                {
                    playedAt = DateTimeOffset.FromUnixTimeSeconds(playedAtSeconds);
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Old clients occasionally left this field undefined.
                }
            }

            return new MalodyReplay(
                beatmapHash.ToLowerInvariant(),
                difficultyName,
                songTitle,
                songArtist,
                score,
                maxCombo,
                best,
                cool,
                good,
                miss,
                holdBreaks,
                mods,
                judge,
                playedAt,
                orderedEvents);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is EndOfStreamException
                or IOException
                or OverflowException)
        {
            throw new InvalidDataException(
                "The Malody replay file is incomplete or malformed.",
                exception);
        }
    }

    public static ManiaModSet ConvertMods(MalodyReplayMods legacyMods)
    {
        var mods = new List<ManiaModId>();
        if (legacyMods.HasFlag(MalodyReplayMods.Flip))
            mods.Add(ManiaModId.Mirror);
        if (legacyMods.HasFlag(MalodyReplayMods.Constant))
            mods.Add(ManiaModId.ConstantSpeed);
        if (legacyMods.HasFlag(MalodyReplayMods.Hide))
            mods.Add(ManiaModId.Hidden);
        if (legacyMods.HasFlag(MalodyReplayMods.Death))
            mods.Add(ManiaModId.SuddenDeath);

        double? rate = null;
        ManiaModId? rateMod = null;
        if (legacyMods.HasFlag(MalodyReplayMods.Dash))
        {
            rate = 1.2;
            rateMod = ManiaModId.DoubleTime;
        }
        else if (legacyMods.HasFlag(MalodyReplayMods.Rush))
        {
            rate = 1.5;
            rateMod = ManiaModId.DoubleTime;
        }
        else if (legacyMods.HasFlag(MalodyReplayMods.Slow))
        {
            rate = 0.8;
            rateMod = ManiaModId.HalfTime;
        }

        if (rateMod is ManiaModId selectedRateMod)
            mods.Add(selectedRateMod);

        return mods.Count == 0
            ? ManiaModSet.Empty
            : new ManiaModSet(mods, fixedRateSpeedChange: rate);
    }

    private static int readNonNegative(BinaryReader reader, string field)
    {
        int value = reader.ReadInt32();
        if (value < 0)
            throw new InvalidDataException($"The Malody replay {field} is invalid.");
        return value;
    }

    private static void requireMarker(BinaryReader reader, string expected)
    {
        if (!string.Equals(readString(reader), expected, StringComparison.Ordinal))
            throw new InvalidDataException("The file is not a supported Malody replay.");
    }

    private static string readString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length is < 0 or > maximum_string_bytes)
            throw new InvalidDataException("The Malody replay contains an invalid string length.");

        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
            throw new EndOfStreamException();
        return Encoding.UTF8.GetString(bytes);
    }

    private static void readExactly(BinaryReader reader, int count)
    {
        if (reader.ReadBytes(count).Length != count)
            throw new EndOfStreamException();
    }

    private static bool isMd5(string value) =>
        value.Length == 32
        && value.All(static character =>
            character is >= '0' and <= '9'
                or >= 'a' and <= 'f'
                or >= 'A' and <= 'F');
}
