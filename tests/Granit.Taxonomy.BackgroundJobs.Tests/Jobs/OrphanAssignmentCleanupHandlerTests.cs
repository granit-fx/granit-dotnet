using Granit.Taxonomy.BackgroundJobs.Jobs;
using NSubstitute;
using Xunit;

namespace Granit.Taxonomy.BackgroundJobs.Tests.Jobs;

public sealed class OrphanAssignmentCleanupHandlerTests
{
    [Fact]
    public async Task HandleAsync_DelegatesToSweepService()
    {
        IOrphanAssignmentSweepService sweep = Substitute.For<IOrphanAssignmentSweepService>();
        sweep.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(7);

        await OrphanAssignmentCleanupHandler.HandleAsync(
            new OrphanAssignmentCleanupJob(),
            sweep,
            TestContext.Current.CancellationToken);

        await sweep.Received(1).ExecuteAsync(Arg.Any<CancellationToken>());
    }
}
