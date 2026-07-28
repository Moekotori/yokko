using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.Stores;

namespace Yokko.Game.Importing;

internal sealed class ChartArtworkResourceStore : IResourceStore<byte[]>
{
    private const long maximum_file_size = 64L * 1024 * 1024;

    public byte[] Get(string name)
    {
        using Stream stream = GetStream(name);
        if (stream == null)
            return null;

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    public Task<byte[]> GetAsync(
        string name,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Get(name), cancellationToken);

    public Stream GetStream(string name)
    {
        if (string.IsNullOrWhiteSpace(name)
            || !Path.IsPathRooted(name)
            || !File.Exists(name))
            return null;

        var file = new FileInfo(name);
        if (file.Length <= 0 || file.Length > maximum_file_size)
            return null;

        return File.Open(name, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public IEnumerable<string> GetAvailableResources() => [];

    public void Dispose()
    {
    }
}
