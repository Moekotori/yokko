namespace Yokko.Core.Mods;

/// <summary>
/// osu!stable FastRandom implementation. Ported from ppy/osu LegacyRandom.cs
/// at 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// </summary>
internal sealed class LegacyRandom
{
    private const double intToReal = 1d / (int.MaxValue + 1d);
    private const uint intMask = 0x7fffffff;
    private uint x;
    private uint y = 842502087;
    private uint z = 3579807591;
    private uint w = 273326509;

    internal LegacyRandom(int seed)
    {
        x = (uint)seed;
    }

    internal uint NextUInt()
    {
        uint t = x ^ (x << 11);
        x = y;
        y = z;
        z = w;
        return w = w ^ (w >> 19) ^ t ^ (t >> 8);
    }

    internal int Next(int lowerBound, int upperBound) =>
        (int)(lowerBound + NextDouble() * (upperBound - lowerBound));

    internal double NextDouble() =>
        intToReal * (int)(intMask & NextUInt());
}
