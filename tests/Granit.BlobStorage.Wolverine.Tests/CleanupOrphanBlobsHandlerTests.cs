using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Wolverine.Tests;

public sealed class CleanupOrphanBlobsHandlerTests
{
    [Fact]
    public async Task HandleAsync_should_delegate_to_CleanupOrphansAsync()
    {
        // Arrange
        IBlobStorage blobStorage = Substitute.For<IBlobStorage>();
        blobStorage.CleanupOrphansAsync(Arg.Any<CancellationToken>()).Returns(3);

        // Act
        await CleanupOrphanBlobsHandler.HandleAsync(
            new CleanupOrphanBlobsCommand(),
            blobStorage,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        // Assert
        await blobStorage.Received(1).CleanupOrphansAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_when_no_orphans_should_not_throw()
    {
        // Arrange
        IBlobStorage blobStorage = Substitute.For<IBlobStorage>();
        blobStorage.CleanupOrphansAsync(Arg.Any<CancellationToken>()).Returns(0);

        // Act & Assert
        await Should.NotThrowAsync(() =>
            CleanupOrphanBlobsHandler.HandleAsync(
                new CleanupOrphanBlobsCommand(),
                blobStorage,
                NullLogger.Instance,
                TestContext.Current.CancellationToken));
    }
}
