using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.Stores;

namespace Yokko.Game.Audio;

internal sealed class SingleFileResourceStore : IResourceStore<byte[]>
{
    private readonly string path;
    private readonly string resourceName;

    public SingleFileResourceStore(string path)
    {
        this.path = Path.GetFullPath(path);
        resourceName = Path.GetFileName(this.path);
    }

    public byte[] Get(string name)
        => matches(name) ? File.ReadAllBytes(path) : null;

    public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
        => matches(name)
            ? File.ReadAllBytesAsync(path, cancellationToken)
            : Task.FromResult<byte[]>(null);

    public Stream GetStream(string name)
        => matches(name) ? File.OpenRead(path) : null;

    public IEnumerable<string> GetAvailableResources()
    {
        yield return resourceName;
    }

    public void Dispose()
    {
    }

    private bool matches(string name)
        => string.Equals(name, resourceName, StringComparison.OrdinalIgnoreCase)
           || string.Equals(Path.GetFullPath(name), path, StringComparison.OrdinalIgnoreCase);
}
