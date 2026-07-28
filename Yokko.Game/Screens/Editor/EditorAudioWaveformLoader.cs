using System;
using System.Threading;
using System.Threading.Tasks;
using Yokko.Audio;

namespace Yokko.Game.Screens.Editor;

public static class EditorAudioWaveformLoader
{
    public const int DefaultPointCount = 16384;

    public static async Task<EditorAudioWaveform> LoadAsync(string audioPath, CancellationToken cancellationToken = default)
    {
        try
        {
            AudioWaveformAnalysis waveform = await AudioWaveformAnalyzer.AnalyzeAsync(
                audioPath,
                DefaultPointCount,
                cancellationToken).ConfigureAwait(false);
            if (waveform.Peaks.Length == 0)
                return EditorAudioWaveform.Failed(audioPath, "Audio waveform empty");

            return EditorAudioWaveform.Ready(
                audioPath,
                waveform.DurationMilliseconds,
                waveform.Peaks,
                waveform.LowIntensity,
                waveform.MidIntensity,
                waveform.HighIntensity);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return EditorAudioWaveform.Failed(audioPath, ex.Message);
        }
    }

}
