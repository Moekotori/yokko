using System.Threading.Tasks;

namespace Yokko.Game.Resources;

/// <summary>
/// Platform folder picker used when changing the persistent resource root.
/// </summary>
public interface IResourceDirectoryPicker
{
    bool IsAvailable { get; }

    Task<string> PickAsync(string initialPath);
}

internal sealed class UnavailableResourceDirectoryPicker : IResourceDirectoryPicker
{
    public bool IsAvailable => false;

    public Task<string> PickAsync(string initialPath) =>
        Task.FromResult<string>(null);
}
