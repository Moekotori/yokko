using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Yokko.Audio.Decoding;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class DecodedAudioSourceTest
{
    // 200 ms mono sine encoded by FFmpeg/libmp3lame. Its Xing/Lavc tag
    // declares 576 encoder-delay and 972 end-padding samples.
    private const string gapless_mp3_data =
        "SUQzBAAAAAAAI1RTU0UAAAAPAAADTGF2ZjYyLjEyLjEwMgAAAAAAAAAAAAAA//tAwAAAAAAAAAAAAAAAAAAAAAAAWGluZwAAAA8AAAAJAAAH1QB0dHR0dHR0dHR0dIiIiIiIiIiIiIiIlZWVlZWVlZWVlZWmpqampqampqamprOzs7Ozs7Ozs7OzwMDAwMDAwMDAwMDR0dHR0dHR0dHR0eXl5eXl5eXl5eXl//////////////8AAAAATGF2YzYyLjI4AAAAAAAAAAAAAAAAJAPMAAAAAAAAB9WTn/WKAAAAAAD/+8DEAAAMpCdM9MCAKfAVaL85IEAAId+tt69evXr1792zypXBMAMAMA4jn6wAAAR4eH/8AAEf8cP//AHeZ/+Bv/45n/+f/+YA74AGH/0PABHgAGHnxw8AAGAADDx8cPAABwAjDz/HgAAAAAGHh4eHgAAAAAGHh4eHgA3xAYACWAyByE4YEAYFAACNwFAAgAoMAdcQAEwuKDCwxuGCU95gEjmrDQYDBRivRkAONYlQvWARoHShYkI/w1aOUM0LhICVOHxDki5RWpAi8fMfHKFzC5iZHNWylP8ipkXiaMS7/+RUyLxeMS6Xf4iCoKiI9/rBURBUFREqFAAAHH4wAAH/+8G1LygIAVHEKAFmBECqYQwRZhEIJmS4JcYJoCIKApBAAa1AJAIh8fVP2PmEsKOFE/uaPAugA3H44//9sAlFE4CDQDGMiXoBcYiXufkC4AgtUufm08LDndlzIn76C0U8U/hdoQjKA+hBYHoAAAH//ouAKqIQWKkMAhIom0xgsDkmkgBcGAijQAzUYvAceq5f9EqKakCZoBf/jjxdC/C8AaVpSdSFpQB0YLwCg9NCUAkJRt5D8Nyi7p0XpesogVJrT+XYZakDbABcfDAAAeGTAFmBAAIqHCBjiPANxMGYjI1KAPwcCogeyR33Lhinwqv7spBJ9FP6pigHWFAAN3/96bEJXMBDOdtE92AAV5iBhh9cMJgsACYbXH/Z27kvwp63oqC3I/1zNQBGUAIoD8cAACaIjqwJnq9FBsIEcTBbRDgQaQAARddcjW4fllvg1naJrP9SlNSBNAE+PcGVWlCJXIUAFbRUBUqb4YIDiVAEQ7swfeH5BR8p/fjU0AJIAZx8OAABNkUmmqbIOgA6aQWyYO+McXj6YAgMlS6U3C3lkVke3QKiFiVo/UKG2IA3gBvH44//9sYeIu6YTJEgAKmQIWYa1weSimBgeR9d6zEndh6zRf647W0flZqmA//7MMTyAEeYSym95QAgxwlldY6JVGhAvHw4AACnYQJI0QgUBLSphGgpiO4R/WFoYGSvYVOOg8MSqr6YsxH+Wv2gzKgABQH445qJpvKypLK2l2WbDwBJgvgmB00ZQCUlg38OQxN3NsifsmMFyFjvlY5iKhv8AFx+MAABzRdAUBGUUUoqPJaeCiGCyNaaTYFQYCOlw0+HHYjdRotxdf/7EMT+AEXISSuseEqgwwkldR8I5BJT7/x1yOgAAADx/qO1GGMQZiLAMDAAAGAQRsBQB5gRg3mEqDaYNqPZsCihGCSEiYK4E5gTgQoxkbAcy5uzodJ9Rwi3mmh92w5EMZFWaB0RGdXp//sgxPgAxmBJKal4SqC5iSV5jolU9nc4D+6QRWFnsIKQCmoy0AsQtQpAtDnQVjQaCMIgyoyM+SAoBpIhUAYeBiAz+ZeIw43bBETuPhHEzAHMBHJboBxmQoqqg7EbAQYZpZkjpiyq1HYjYDgwUQpo6nxJhyw3cv01JTBgheNAO0ldq//7EMT9gMXELS3gY6LglwklkR6IrLZVf///2TqaO5LLUMY1o0/0zGf///34Xezt12WSyxDOW8a0ajX////1oYljlv/F34sYVaWl/eOVNTf/////6qWKent2OfrDHHH/x1lW2d+Aw+ch//sQxP0ARhAtK6BjouC5CSW1jolUgHzXEVVMQU1FMy4xMDBVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVX/+yDE9wBFjC0toGOi4MOJJXwfCHRVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV//swxP6ARewrK6DjwuFri+R+vPAFVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV//tQxPyAFsUnPfm8gAAAADSDgAAEVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVQ==";

    private static readonly string gapless_mp3 = expandSilenceRuns(
        gapless_mp3_data);

    private static string expandSilenceRuns(string encoded)
    {
        int index = 0;
        int[] lengths = { 91, 145, 180, 249 };
        return Regex.Replace(
            encoded,
            "V{10,}",
            _ => new string('V', lengths[index++]));
    }

    [Test]
    public void Mp3GaplessMetadataDefinesLogicalStartAndDuration()
    {
        string path = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"{TestContext.CurrentContext.Test.ID}.mp3");
        File.WriteAllBytes(path, Convert.FromBase64String(gapless_mp3));

        try
        {
            using DecodedAudioSource source = DecodedAudioSource.Open(path);
            var samples = new float[4096];
            int read = source.Read(samples);
            int firstSignal = Array.FindIndex(
                samples,
                0,
                read,
                sample => Math.Abs(sample) >= 0.02f);

            Assert.That(source.SampleRate, Is.EqualTo(44100));
            Assert.That(source.TotalTime.TotalMilliseconds, Is.EqualTo(200).Within(0.001));
            Assert.That(
                firstSignal / 2,
                Is.InRange(0, 4),
                "Encoder and decoder priming must not appear on the logical timeline.");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
