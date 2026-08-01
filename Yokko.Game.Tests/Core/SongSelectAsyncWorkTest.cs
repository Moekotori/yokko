using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class SongSelectAsyncWorkTest
{
    [Test]
    public async Task ImmediateActionBypassesSelectionDebounce()
    {
        var signal = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task waiting = SongSelectScreen.WaitForPlayableBeatmapStartAsync(
            signal.Task,
            30_000,
            CancellationToken.None);

        Assert.That(waiting.IsCompleted, Is.False);
        signal.SetResult(true);

        await waiting.WaitAsync(CancellationToken.None);
        Assert.That(waiting.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public void CancelledSelectionDoesNotStartMaterialisation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await SongSelectScreen.WaitForPlayableBeatmapStartAsync(
                Task.CompletedTask,
                150,
                cancellation.Token));
    }
}
