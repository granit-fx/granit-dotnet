// =============================================================================
// Tests — RunMigrationBatchHandler (Wolverine adapter)
// =============================================================================
// Verifies the thin Wolverine handler adapter that delegates to
// MigrationBatchExecutor and returns the cascade array.
// Uses real MigrationBatchExecutor with mocked dependencies.
// =============================================================================

using Granit.Guids;
using Granit.Persistence.EntityFrameworkCore.Migrations.Internal;
using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;
using Granit.Persistence.EntityFrameworkCore.Migrations.Wolverine.Internal;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Wolverine.Tests;

public sealed class RunMigrationBatchHandlerTests : IDisposable
{
    private readonly MigrationProgressDbContext _progressContext;

    public RunMigrationBatchHandlerTests()
    {
        DbContextOptions<MigrationProgressDbContext> options =
            new DbContextOptionsBuilder<MigrationProgressDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        _progressContext = new MigrationProgressDbContext(options);
    }

    public void Dispose() => _progressContext.Dispose();

    private RunMigrationBatchHandler BuildHandler(
        string cycleId,
        BatchMigrationDelegate migration)
    {
        IMigrationCycleRegistry registry = Substitute.For<IMigrationCycleRegistry>();
        registry.Find(cycleId).Returns(new MigrationCycleRegistration(
            cycleId, typeof(StubDbContext), migration));

        StubDbContext stubContext = new(
            new DbContextOptionsBuilder<StubDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        ServiceCollection services = new();
        services.AddSingleton(stubContext);
        ServiceProvider sp = services.BuildServiceProvider();

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        MigrationBatchExecutor executor = new(
            registry,
            sp,
            _progressContext,
            Substitute.For<ITenantDbIsolator>(),
            clock,
            new SimpleGuidGenerator(),
            NullLogger<MigrationBatchExecutor>.Instance);

        return new RunMigrationBatchHandler(executor);
    }

    [Fact]
    public async Task HandleAsync_LastBatch_ReturnsEmptyArray()
    {
        string cycleId = "last-batch";
        RunMigrationBatchHandler handler = BuildHandler(
            cycleId,
            (_, _, _) => Task.FromResult(new MigrationBatchResult(10, null)));

        object[] result = await handler.HandleAsync(
            new RunMigrationBatchCommand(cycleId, Guid.Empty, null, 100),
            TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MidBatch_ReturnsCascadeCommandInArray()
    {
        string cycleId = "mid-batch";
        RunMigrationBatchHandler handler = BuildHandler(
            cycleId,
            (_, _, _) => Task.FromResult(new MigrationBatchResult(50, "cursor-next")));

        object[] result = await handler.HandleAsync(
            new RunMigrationBatchCommand(cycleId, Guid.Empty, null, 100),
            TestContext.Current.CancellationToken);

        result.Length.ShouldBe(1);
        RunMigrationBatchCommand cascade = result[0].ShouldBeOfType<RunMigrationBatchCommand>();
        cascade.CycleId.ShouldBe(cycleId);
        cascade.Cursor.ShouldBe("cursor-next");
    }

    [Fact]
    public async Task HandleAsync_UnknownCycle_ReturnsEmptyArray()
    {
        IMigrationCycleRegistry registry = Substitute.For<IMigrationCycleRegistry>();
        registry.Find(Arg.Any<string>()).Returns((MigrationCycleRegistration?)null);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        MigrationBatchExecutor executor = new(
            registry,
            new ServiceCollection().BuildServiceProvider(),
            _progressContext,
            Substitute.For<ITenantDbIsolator>(),
            clock,
            new SimpleGuidGenerator(),
            NullLogger<MigrationBatchExecutor>.Instance);

        RunMigrationBatchHandler handler = new(executor);

        object[] result = await handler.HandleAsync(
            new RunMigrationBatchCommand("missing", Guid.Empty, null, 100),
            TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    private sealed class StubDbContext(DbContextOptions<StubDbContext> options) : DbContext(options);
}
