using System.Globalization;
using System.Text;
using SharpCompress.Compressors.LZMA;

namespace Yokko.Import.Osu;

public readonly record struct OsuReplayFrame(
    double TimeMilliseconds,
    int PressedKeys);

public sealed record OsuReplay(
    int GameVersion,
    string BeatmapHash,
    string PlayerName,
    int Mods,
    IReadOnlyList<OsuReplayFrame> Frames);

/// <summary>
/// Reads legacy osu! replay files. Only osu!mania frame data is accepted.
/// The binary layout and frame correction rules follow ppy/osu's
/// LegacyScoreDecoder at commit 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0.
/// </summary>
public static class OsuReplayIO
{
    private const byte maniaRulesetId = 3;
    private const int maximumCompressedBytes = 32 * 1024 * 1024;
    private const int maximumExpandedBytes = 128 * 1024 * 1024;
    private const int maximumManiaKeyMask = (1 << 20) - 1;

    public static OsuReplay ReadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using FileStream stream = File.OpenRead(path);
        return Read(stream);
    }

    public static OsuReplay Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            using var reader = new BinaryReader(
                stream,
                Encoding.UTF8,
                leaveOpen: true);

            byte rulesetId = reader.ReadByte();
            if (rulesetId != maniaRulesetId)
            {
                throw new InvalidDataException(
                    "Only osu!mania replay files are supported.");
            }

            int gameVersion = reader.ReadInt32();
            string beatmapHash = readLegacyString(reader)
                                 ?? throw new InvalidDataException(
                                     "The replay does not identify a beatmap.");
            string playerName = readLegacyString(reader) ?? string.Empty;

            // Replay hash and stable score statistics are metadata only.
            readLegacyString(reader);
            for (int i = 0; i < 6; i++)
                reader.ReadUInt16();
            reader.ReadInt32();
            reader.ReadUInt16();
            reader.ReadBoolean();
            int mods = reader.ReadInt32();
            readLegacyString(reader);
            reader.ReadInt64();

            int compressedLength = reader.ReadInt32();
            if (compressedLength is < 0 or > maximumCompressedBytes)
            {
                throw new InvalidDataException(
                    "The replay frame stream has an invalid size.");
            }

            byte[] compressedFrames = reader.ReadBytes(compressedLength);
            if (compressedFrames.Length != compressedLength)
                throw new EndOfStreamException();

            string frameText = decompress(compressedFrames);
            IReadOnlyList<OsuReplayFrame> frames = parseFrames(frameText);

            return new OsuReplay(
                gameVersion,
                beatmapHash,
                playerName,
                mods,
                frames);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is EndOfStreamException
                or IOException
                or OverflowException
                or FormatException)
        {
            throw new InvalidDataException(
                "The osu! replay file is incomplete or malformed.",
                ex);
        }
    }

    private static string? readLegacyString(BinaryReader reader)
    {
        byte marker = reader.ReadByte();
        if (marker == 0)
            return null;
        if (marker != 0x0b)
            throw new InvalidDataException("The replay contains an invalid string marker.");

        return reader.ReadString();
    }

    private static string decompress(byte[] data)
    {
        if (data.Length == 0)
            return string.Empty;
        if (data.Length < 13)
            throw new InvalidDataException("The replay LZMA stream is too short.");

        using var input = new MemoryStream(data, writable: false);
        byte[] properties = new byte[5];
        input.ReadExactly(properties);

        long expandedSize = 0;
        for (int i = 0; i < sizeof(long); i++)
            expandedSize |= (long)(byte)input.ReadByte() << (8 * i);

        if (expandedSize > maximumExpandedBytes)
            throw new InvalidDataException("The replay expands beyond the 128 MB safety limit.");

        long compressedSize = input.Length - input.Position;
        using var lzma = LzmaStream.Create(
            properties,
            input,
            compressedSize,
            expandedSize);
        using var output = new MemoryStream();

        byte[] buffer = new byte[16 * 1024];
        while (true)
        {
            int read = lzma.Read(buffer, 0, buffer.Length);
            if (read == 0)
                break;

            if (output.Length + read > maximumExpandedBytes)
            {
                throw new InvalidDataException(
                    "The replay expands beyond the 128 MB safety limit.");
            }

            output.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(output.GetBuffer(), 0, checked((int)output.Length));
    }

    private static IReadOnlyList<OsuReplayFrame> parseFrames(string frameText)
    {
        var frames = new List<MutableReplayFrame>();
        double lastTime = 0;

        foreach (string encodedFrame in frameText.Split(','))
        {
            string[] parts = encodedFrame.Split('|');
            if (parts.Length < 4 || parts[0] == "-12345")
                continue;

            double delta = parseFrameDelta(parts[0]);
            double mouseX = parseFiniteDouble(parts[1], "key state");
            double mouseY = parseFiniteDouble(parts[2], "pointer position");

            if (mouseX is < 0 or > maximumManiaKeyMask)
                throw new InvalidDataException("The replay contains an invalid mania key state.");

            lastTime += delta;
            if (!double.IsFinite(lastTime))
                throw new InvalidDataException("The replay contains an invalid frame time.");

            frames.Add(new MutableReplayFrame(
                lastTime,
                (int)mouseX,
                mouseX,
                mouseY));
        }

        applyStableFrameCorrections(frames);

        var result = new List<OsuReplayFrame>(frames.Count);
        double? previousTime = null;

        foreach (MutableReplayFrame frame in frames)
        {
            if (previousTime.HasValue && frame.Time < previousTime.Value)
                continue;

            result.Add(new OsuReplayFrame(frame.Time, frame.PressedKeys));
            previousTime = frame.Time;
        }

        return result;
    }

    private static double parseFrameDelta(string value)
    {
        if (int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int integer))
        {
            return integer;
        }

        return Math.Round(parseFiniteDouble(value, "frame delta"));
    }

    private static double parseFiniteDouble(string value, string field)
    {
        if (!double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double result)
            || !double.IsFinite(result))
        {
            throw new InvalidDataException($"The replay contains an invalid {field}.");
        }

        return result;
    }

    private static void applyStableFrameCorrections(
        List<MutableReplayFrame> frames)
    {
        if (frames.Count >= 2 && frames[1].Time < frames[0].Time)
        {
            frames[1].Time = frames[0].Time;
            frames[0].Time = 0;
        }

        if (frames.Count >= 3 && frames[0].Time > frames[2].Time)
            frames[0].Time = frames[1].Time = frames[2].Time;

        if (frames.Count >= 2 && frames[1].IsStableIntroFrame)
            frames.RemoveAt(1);
        if (frames.Count >= 1 && frames[0].IsStableIntroFrame)
            frames.RemoveAt(0);
    }

    private sealed class MutableReplayFrame(
        double time,
        int pressedKeys,
        double mouseX,
        double mouseY)
    {
        public double Time { get; set; } = time;
        public int PressedKeys { get; } = pressedKeys;
        public bool IsStableIntroFrame =>
            mouseX == 256 && mouseY == -500;
    }
}
