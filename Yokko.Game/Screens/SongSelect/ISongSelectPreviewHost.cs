using System.Threading.Tasks;
using Yokko.Audio;
using Yokko.Core.Beatmaps;

namespace Yokko.Game.Screens.SongSelect;

internal interface ISongSelectPreviewHost
{
    IAudioEngine AudioEngine { get; }

    void AdoptPreview(YokkoBeatmap beatmap);

    void CompletePreviewHandoff(Task playbackSettled);
}
