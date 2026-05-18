using Granit.Bff.BackgroundJobs.Internal;
using Granit.Bff.BackgroundJobs.Jobs;
using Granit.Bff.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Bff.BackgroundJobs.Tests.Jobs;

public sealed class BffExpiredSessionCleanupHandlerTests
{
    /// <summary>
    /// Verifies that the service requests a DbContext from the factory.
    /// Full cleanup behavior requires a relational provider and is covered
    /// by integration tests.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_should_create_db_context_from_factory()
    {
        DbContextOptions<BffDbContext> options = new DbContextOptionsBuilder<BffDbContext>()
            .UseInMemoryDatabase($"BffTest_{Guid.NewGuid()}")
            .Options;

        var factory = new TrackingBffDbContextFactory(options);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        var service = new ExpiredSessionCleanupService(
            factory,
            clock,
            NullLogger<ExpiredSessionCleanupService>.Instance);

        // ExecuteDeleteAsync is not supported by InMemory — we expect the exception.
        // The test validates that the service correctly requests a DbContext.
        try
        {
            await service.ExecuteAsync(TestContext.Current.CancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ExecuteDelete"))
        {
            // Expected: InMemory provider does not support ExecuteDelete
        }

        factory.CreateCount.ShouldBe(1);
    }

    private sealed class TrackingBffDbContextFactory(DbContextOptions<BffDbContext> options)
        : IDbContextFactory<BffDbContext>
    {
        public int CreateCount { get; private set; }

        public BffDbContext CreateDbContext()
        {
            CreateCount++;
            return new BffDbContext(options, GranitDesignTime.CurrentTenant);
        }
    }
}
