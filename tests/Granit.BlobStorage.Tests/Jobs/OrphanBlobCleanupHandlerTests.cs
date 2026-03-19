using Granit.BlobStorage.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests.Jobs;

public sealed class OrphanBlobCleanupHandlerTests
{
    [Fact]
    public async Task HandleAsync_should_delegate_to_CleanupOrphansAsync()
    {
        IBlobStorage blobStorage = Substitute.For<IBlobStorage>();
        blobStorage.CleanupOrphansAsync(Arg.Any<CancellationToken>()).Returns(3);

        await OrphanBlobCleanupHandler.HandleAsync(
            new OrphanBlobCleanupJob(),
            blobStorage,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        await blobStorage.Received(1).CleanupOrphansAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_when_no_orphans_should_not_throw()
    {
        IBlobStorage blobStorage = Substitute.For<IBlobStorage>();
        blobStorage.CleanupOrphansAsync(Arg.Any<CancellationToken>()).Returns(0);

        await Should.NotThrowAsync(() =>
            OrphanBlobCleanupHandler.HandleAsync(
                new OrphanBlobCleanupJob(),
                blobStorage,
                NullLogger.Instance,
                TestContext.Current.CancellationToken));
    }
}
