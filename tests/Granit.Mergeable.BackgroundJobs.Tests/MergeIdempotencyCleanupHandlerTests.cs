using Granit.Mergeable.BackgroundJobs.Jobs;
using Granit.Mergeable.BackgroundJobs.Services;
using NSubstitute;
using Xunit;

namespace Granit.Mergeable.BackgroundJobs.Tests;

/// <summary>
/// Smoke tests for the cleanup handler. The full delete behaviour is covered by the Postgres
/// integration test in <c>Granit.Parties.Mergeable.Tests.Integration</c> — SQLite cannot
/// translate <c>DateTimeOffset</c> comparisons used by the production sweep, and EF in-memory
/// does not implement <c>ExecuteDeleteAsync</c>.
/// </summary>
public sealed class MergeIdempotencyCleanupHandlerTests
{
    [Fact]
    public async Task HandleAsync_DelegatesToSweeper()
    {
        IMergeIdempotencySweeper sweeper = Substitute.For<IMergeIdempotencySweeper>();
        sweeper.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await MergeIdempotencyCleanupHandler.HandleAsync(
            new MergeIdempotencyCleanupJob(),
            sweeper,
            TestContext.Current.CancellationToken);

        await sweeper.Received(1).ExecuteAsync(Arg.Any<CancellationToken>());
    }
}
